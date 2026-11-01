using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Hosting.GitLab;
using VisualCommit.Hosting.Http;
using VisualCommit.Hosting.Tokens;

namespace VisualCommit.Hosting;

/// <summary>
/// The app session's hosting (docs/phase-7a-design.md 7.3 to 7.7; docs/phase-7b-design.md 9, D357):
/// a provider per endpoint the options give, in <see cref="HostingProviders.InAccountsOrder"/>, each
/// with one account (D348) and its token, the bindings of git's credentials to the accounts (D255,
/// D359) and git's requests for them, and the hosting of each repository tab. Each provider's account
/// is held in one slot that is swapped whole and read without a lock, so git's requests and the
/// bindings never wait on the UI thread; the start's read and every provider's sign-in second half,
/// sign-out, refusal and switch run one at a time under one gate, so <c>accounts.json</c> changes one
/// write at a time, and no network call runs under it. Events reach the UI thread through the post
/// delegate, one per provider whose account changed. All off, nothing is read, recognised or asked.
/// GitHub's avatars and author maps exist only while GitHub is on (D349).
/// </summary>
public sealed class HostingSession : IHostingSession
{
    /// <summary>How long a request for a token waits for the start's keychain read (ruling 6); git's cannot come before it ends (<see cref="GetAsync"/>).</summary>
    internal static readonly TimeSpan CredentialWait = TimeSpan.FromSeconds(10);

    private readonly AppPaths _paths;
    private readonly HostingOptions _options;
    private readonly IAppLog _log;
    private readonly Action<Action> _post;
    private readonly Entry[] _entries;
    private readonly HostingProvider[] _providers;
    private readonly GitHubProvider? _gitHub;
    private readonly TokenVault? _vault;
    private readonly JsonAccountStore _accounts;

    // Cancelled at Dispose and never disposed itself: work still running links to its token.
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _lock = new();
    private readonly List<RepositoryHosting> _hostings = [];

    // Under _gate: the file as last read or written.
    private AccountsFile _file = AccountsFile.Empty;

    private readonly AuthorMapStore? _authorMaps;
    private AvatarCache? _avatarCache;

    // Under _lock.
    private readonly HashSet<Task> _avatarRuns = [];
    private CancellationTokenSource _avatarWork = new();
    private int _startCalled;
    private volatile bool _disposed;

    /// <summary>
    /// The session over <paramref name="options"/>: one provider per endpoint given, each with its
    /// handler (a null one refuses every request, as <see cref="HostingOptions.Off"/>'s), disposed
    /// with the session; one Info line per provider, <c>{Name}: {host}.</c> or <c>{Name}: off.</c>,
    /// in D353's order (docs/phase-7b-design.md 9.2).
    /// </summary>
    /// <exception cref="ArgumentException">Two providers are on one web origin: the broker answers a prompt from the first binding on an origin.</exception>
    public HostingSession(AppPaths paths, HostingOptions options, ITokenStore tokens, IAppLog log, Action<Action> post, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(clock);
        RefuseSharedOrigins(options);
        _paths = paths;
        _options = options;
        _log = log;
        _post = post;
        Clock = clock;
        _accounts = new JsonAccountStore(paths.AccountsFile, log);
        if (!options.AllOff)
        {
            _vault = new TokenVault(tokens, log);
        }

        var entries = new List<Entry>();
        foreach (var kind in HostingProviders.InAccountsOrder)
        {
            IHostingProvider? provider = null;
            switch (kind)
            {
                case HostingProvider.GitHub when options.GitHub is { } gitHub:
                    _gitHub = new GitHubProvider(gitHub, options.CreateHandler, log, clock);
                    _avatarCache = NewAvatarCache(options);
                    _authorMaps = new AuthorMapStore(Path.Combine(paths.AvatarsDirectory, "authors"), clock, log);
                    provider = _gitHub;
                    break;
                case HostingProvider.GitLab when options.GitLab is { } gitLab:
                    provider = new GitLabProvider(gitLab, options.CreateGitLabHandler ?? HostingOptions.RefusingHandler, log, clock);
                    break;
                case HostingProvider.AzureDevOps when options.AzureDevOps is { } azureDevOps:
                    provider = new AzureDevOpsProvider(azureDevOps, options.CreateAzureDevOpsHandler ?? HostingOptions.RefusingHandler, log, clock);
                    break;
            }

            if (provider is null)
            {
                log.Info($"{HostingProviders.DisplayName(kind)}: off.");
                continue;
            }

            entries.Add(new Entry(kind, provider));
            log.Info($"{HostingProviders.DisplayName(kind)}: {provider.Host}.");
        }

        _entries = [.. entries];
        _providers = [.. entries.Select(entry => entry.Kind)];
    }

    /// <summary>Every provider is off.</summary>
    public bool IsOff => _entries.Length == 0;

    /// <summary>The providers on, in <see cref="HostingProviders.InAccountsOrder"/>.</summary>
    public IReadOnlyList<HostingProvider> Providers => _providers;

    public event EventHandler<HostingAccountChangedEventArgs>? AccountChanged;

    public bool BindsGit => _entries.Any(entry => entry.Slot is { BindsGit: true });

    /// <summary>GitHub's account as held now (7a's member, for the tests); null without one.</summary>
    internal AccountSlot? Slot => SlotOf(HostingProvider.GitHub);

    /// <summary>Some provider's account is signed in with its token in hand: the tabs read the URL rewrites (docs/phase-7b-design.md 9.7).</summary>
    internal bool AnySignedIn => _entries.Any(entry => entry.Slot is { IsSignedIn: true });

    /// <summary>GitHub's typed provider while it is on, for A6 (D349).</summary>
    internal GitHubProvider? GitHubProvider => _gitHub;

    internal IAppLog Log => _log;

    internal TimeProvider Clock { get; }

    /// <summary>The session's lifetime: cancelled at <see cref="Dispose"/>.</summary>
    internal CancellationToken Lifetime => _lifetime.Token;

    /// <summary>GitHub's avatars' images (6.4); a new one after each of GitHub's sign-outs.</summary>
    internal AvatarCache AvatarCache => Volatile.Read(ref _avatarCache) ?? throw new InvalidOperationException("GitHub is off.");

    /// <summary>GitHub's e-mail maps and the lookups' budget (7.9).</summary>
    internal AuthorMapStore AuthorMaps => _authorMaps ?? throw new InvalidOperationException("GitHub is off.");

    /// <summary>GitHub's last "Use for git over HTTPS" change's write, for the tests (7a's member).</summary>
    internal Task Switching => SwitchingOf(HostingProvider.GitHub);

    /// <summary>GitHub's last check of git's refusal, null before the first, for the tests (7a's member).</summary>
    internal Task? RefusalCheck => RefusalCheckOf(HostingProvider.GitHub);

    /// <summary>How a sign-out deletes one file of <c>avatars/</c>: <see cref="File.Delete"/>; the tests give one that fails.</summary>
    internal Action<string> DeleteAvatarFile { get; set; } = File.Delete;

    /// <summary>How many repositories' hostings are told of the accounts' changes, for the tests.</summary>
    internal int HostingCount
    {
        get
        {
            lock (_lock)
            {
                return _hostings.Count;
            }
        }
    }

    public bool IsOn(HostingProvider provider) => EntryOf(provider) is not null;

    public IHostingProvider? ProviderOf(HostingProvider provider) => EntryOf(provider)?.Provider;

    public AccountStatus? AccountOf(HostingProvider provider) => EntryOf(provider)?.Slot?.Status;

    /// <summary><paramref name="provider"/>'s account as held now; null without one or while off.</summary>
    internal AccountSlot? SlotOf(HostingProvider provider) => EntryOf(provider)?.Slot;

    /// <summary><paramref name="provider"/>'s last "Use for git over HTTPS" change's write, for the tests.</summary>
    internal Task SwitchingOf(HostingProvider provider)
    {
        lock (_lock)
        {
            return EntryOf(provider)?.Switching ?? Task.CompletedTask;
        }
    }

    /// <summary><paramref name="provider"/>'s last check of git's refusal, null before the first, for the tests.</summary>
    internal Task? RefusalCheckOf(HostingProvider provider)
    {
        lock (_lock)
        {
            return EntryOf(provider)?.RefusalCheck;
        }
    }

    /// <summary>
    /// Reads <c>accounts.json</c> once and, for each provider on with a record whose token is in the
    /// keychain, the keychain once, one after another (7.4, ruling 6, D287; docs/phase-7b-design.md
    /// 9.4); makes no request. All off, nothing is read; without a record for a provider no store is
    /// touched for it. Runs on the thread pool and reaches the UI thread only through the post
    /// delegate: one event per provider whose account was read, in D353's order.
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _startCalled, 1) == 1)
        {
            await _started.Task.WaitAsync(ct).ConfigureAwait(false);
            return;
        }

        IReadOnlyList<HostingProvider> changed = [];
        try
        {
            if (_entries.Length > 0)
            {
                changed = await StartCoreAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _started.TrySetResult();
        }

        foreach (var provider in changed)
        {
            PostAccountChanged(provider);
        }
    }

    public async Task<bool> KeychainAvailableAsync(CancellationToken ct) =>
        _vault is not null && await _vault.KeychainAvailableAsync(ct).ConfigureAwait(false);

    /// <summary>
    /// Signs in to <paramref name="request"/>'s provider (7.5 per provider; D256, D348, D288): off, a
    /// <see cref="HostingFailureKind.Validation"/> failure without a request. Else A1 first, honouring
    /// <paramref name="ct"/>; a failure changes nothing, and so does an answer that names the token
    /// itself (D382: a malformed answer, before anything is kept or logged). Once the service has
    /// answered, the rest completes under the session's lifetime: that provider's previous account's
    /// key forgotten when it differs, the token kept (the keychain, else the session), the record
    /// written, the account replaced. Other providers are untouched.
    /// </summary>
    public async Task<SignInResult> SignInAsync(SignInRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Token);
        if (EntryOf(request.Provider) is not { } entry || _vault is null)
        {
            return new SignInResult(null, TurnedOff(request.Provider), null);
        }

        HostingResult<HostingAccount> checkedToken;
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token))
        {
            checkedToken = await entry.Provider.CheckTokenAsync(request.Token, request.Organization, linked.Token).ConfigureAwait(false);
        }

        if ((checkedToken.Failure ?? EchoedToken(entry.Kind, checkedToken.Value!, request.Token)) is { } failure)
        {
            _log.Warning($"{entry.Name}: signing in on {entry.Host} failed: {Describe(failure)}");
            return new SignInResult(null, failure, null);
        }

        var lifetime = _lifetime.Token;
        await _started.Task.WaitAsync(lifetime).ConfigureAwait(false);
        await _gate.WaitAsync(lifetime).ConfigureAwait(false);
        try
        {
            var checkedAccount = checkedToken.Value!;
            var key = new AccountKey(entry.Kind, entry.Host, checkedAccount.Login);
            var previous = _file.For(entry.Kind, entry.Host);
            if (previous is { Login: { } oldLogin } && !string.IsNullOrWhiteSpace(oldLogin)
                && new AccountKey(entry.Kind, entry.Host, oldLogin) is var oldKey && oldKey != key
                && (await _vault.ForgetAsync(TokenKey.Of(oldKey), lifetime).ConfigureAwait(false)
                    ?? await KeptUnreachedAsync(oldKey, AccountTokenStates.Of(previous.TokenState)).ConfigureAwait(false)) is { } kept)
            {
                _log.Warning($"{entry.Name} account {oldLogin} on {entry.Host}: the keychain kept its entry: {kept}.");
            }

            var account = checkedAccount with { Key = key, UseForGit = previous?.UseForGit ?? true };
            var (place, reason) = await _vault.KeepAsync(TokenKey.Of(key), request.Token, lifetime).ConfigureAwait(false);

            // No keychain at all (D253) is told apart from a save that failed (ruling 19) by the
            // vault's reason: only the latter is a "save failed" for the dialog.
            var (tokenState, noteKind, note, saveFailedReason) = (place, reason) switch
            {
                (TokenPlace.Keychain, _) => (AccountTokenStates.Keychain, AccountNoteKind.None, (string?)null, (string?)null),
                (_, null or TokenStoreReasons.NoKeychain) => (AccountTokenStates.Session, AccountNoteKind.NoKeychain, null, null),
                _ => (AccountTokenStates.Session, AccountNoteKind.SaveFailed, reason, reason),
            };
            var slot = new AccountSlot(new AccountStatus(account, AccountState.SignedIn, place, noteKind, note), request.Token, tokenState);
            SetSlotLocked(entry, slot);
            _log.Info(place == TokenPlace.Keychain
                ? $"{entry.Name} account {account.Login} on {entry.Host}: signed in; the token is kept in the keychain."
                : $"{entry.Name} account {account.Login} on {entry.Host}: signed in; the token is kept for this session: {reason ?? TokenStoreReasons.NoKeychain}.");
            PostAccountChanged(entry.Kind);
            return new SignInResult(slot.Status, null, saveFailedReason);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Signs <paramref name="provider"/>'s account out (7.5, D291): the keychain's entry forgotten
    /// (null, or why it was kept; without a keychain in this run, <see cref="TokenStoreReasons.NoKeychain"/>
    /// for a record whose token an earlier run saved there), the record removed even when the entry
    /// was kept, the account cleared, the provider's kept answers forgotten; only GitHub's deletes
    /// <c>avatars/</c> (D349). Runs on the thread pool, never on the caller's thread.
    /// <paramref name="ct"/> stops the waits before the gate only: once begun, the sign-out completes
    /// when the session is disposed meanwhile, but nothing keeps the process alive for it. Off or
    /// without an account, null and nothing happens.
    /// </summary>
    public Task<string?> SignOutAsync(HostingProvider provider, CancellationToken ct) =>
        EntryOf(provider) is not { } entry || _vault is null
            ? Task.FromResult<string?>(null)
            : Task.Run(() => SignOutCoreAsync(entry, ct), CancellationToken.None);

    /// <summary>
    /// "Use for git over HTTPS" of <paramref name="provider"/>'s account at the settings screen's
    /// Save (D216): on the thread pool after that provider's change before it, the value compared
    /// under the gate (no account or the same value: nothing); one record written and the events posted.
    /// </summary>
    public void SetUseForGit(HostingProvider provider, bool useForGit)
    {
        if (EntryOf(provider) is not { } entry || _disposed)
        {
            return;
        }

        lock (_lock)
        {
            var earlier = entry.Switching;
            entry.Switching = Task.Run(() => SwitchAfterAsync(entry, earlier, useForGit), CancellationToken.None);
        }
    }

    /// <summary>A2 with <paramref name="provider"/>'s account's token; off or signed out a failure without a request (D257).</summary>
    public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(HostingProvider provider, CancellationToken ct) =>
        CallAsync(provider, "listing the account's repositories", null, (hosted, token, account, linked) => hosted.ListRepositoriesAsync(token, account, linked), ct);

    /// <summary>The first answer of the providers on, in <see cref="HostingProviders.InAccountsOrder"/>; null when none recognises <paramref name="url"/>.</summary>
    public HostedRepository? Recognise(string url)
    {
        foreach (var entry in _entries)
        {
            if (entry.Provider.Recognise(url) is { } repository)
            {
                return repository;
            }
        }

        return null;
    }

    /// <summary>The push guard (ruling 14): the rule of the provider that recognises <paramref name="remoteUrl"/> (D279), another organization's included; true when none does.</summary>
    public bool SourceCanExist(string remoteUrl, string remoteRef)
    {
        foreach (var entry in _entries)
        {
            if (entry.Provider.Recognise(remoteUrl) is not null)
            {
                return entry.Provider.SourceCanExist(remoteRef);
            }
        }

        return true;
    }

    /// <summary>A clone of <paramref name="url"/> (after the user's <c>insteadOf</c>, 9.3, D331): the union of the bindings of every account that binds git.</summary>
    public IReadOnlyList<CredentialBinding> BindingsForClone(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return BindingsOf([new RemoteEffectiveUrls("origin", [url], [url])], rulesKnown: true);
    }

    /// <summary>A dashboard row without a tab (4.7, D332): the union, in D353's order, of every account's bindings that binds git.</summary>
    public IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites)
    {
        ArgumentNullException.ThrowIfNull(remoteUrls);
        ArgumentNullException.ThrowIfNull(rewrites);
        return BindsGit ? BindingsOf(rewrites.Apply(remoteUrls), rulesKnown: true) : [];
    }

    /// <summary>A new hosting for the tab of <paramref name="workingDirectory"/>, live until its <c>Dispose</c>; all off, <see cref="NullRepositoryHosting.Instance"/>.</summary>
    public IRepositoryHosting ForRepository(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (_entries.Length == 0)
        {
            return NullRepositoryHosting.Instance;
        }

        var hosting = new RepositoryHosting(this, workingDirectory);
        lock (_lock)
        {
            _hostings.Add(hosting);
        }

        return hosting;
    }

    /// <summary>
    /// git's <c>get</c> for a bound URL (7.6, ruling 6): the token while <paramref name="account"/>
    /// is the signed-in account of its provider and "Use for git over HTTPS" is on; else null. Never
    /// UI. It waits for the start's read, at most 10 s by the session's clock, but through git no get
    /// comes before that read ends: nothing is bound until the read has put the token in the slot
    /// (7.4), and a tab's bindings also wait for its next refs read with the rules, so an operation
    /// started meanwhile goes D82's order. The wait guards a direct call.
    /// </summary>
    public async Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (EntryOf(account.Provider) is not { } entry)
        {
            return null;
        }

        try
        {
            await _started.Task.WaitAsync(CredentialWait, Clock, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _log.Warning($"{entry.Name}: the keychain's read had not ended after 10 s; git's request for {account.Host} went without the account's token.");
            return null;
        }

        return entry.Slot is { BindsGit: true, Token: { } token } slot && slot.Key == account
            ? new GitCredential(token)
            : null;
    }

    /// <summary>
    /// git's <c>erase</c> to the app's helper (7.6, D330; docs/phase-7b-design.md 9.5): returns at
    /// once. For the signed-in account of <paramref name="account"/>'s provider, one A1 with its token
    /// (Azure DevOps' with its organization) checks it, at most one at a time per provider (a refusal
    /// meanwhile joins it), so one provider's check never waits on another's: a 401 marks the account
    /// refused; a 200 for another user (D357: Azure DevOps' user id, GitHub's and GitLab's id) marks it
    /// too; the same user keeps it with a warning, and a record without the user (a 7a save) takes the
    /// check's; any other answer keeps it, with a warning.
    /// </summary>
    public void Refused(AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (EntryOf(account.Provider) is not { } entry || entry.Slot is not { IsSignedIn: true } slot || slot.Key != account)
        {
            return;
        }

        lock (_lock)
        {
            if (entry.RefusalCheck is { IsCompleted: false } || _disposed)
            {
                return;
            }

            entry.RefusalCheck = Task.Run(() => CheckRefusalAsync(entry, slot));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        lock (_lock)
        {
            _avatarWork.Cancel();
        }

        foreach (var entry in _entries)
        {
            (entry.Provider as IDisposable)?.Dispose();
        }

        _avatarCache?.Dispose();
    }

    /// <summary>
    /// One call to <paramref name="provider"/> with its account's token (7.6; docs/phase-7b-design.md
    /// 9.6): without a request when it is off, when its account is not signed in (D257), or when
    /// <paramref name="about"/> is a repository the account does not cover (D350), each failure with
    /// <see cref="HostingFailure.Provider"/> set; <paramref name="ct"/> linked to the session's
    /// lifetime; a 401 marks the account refused, any other failure is a warning naming <paramref name="what"/>.
    /// </summary>
    internal async Task<HostingResult<T>> CallAsync<T>(
        HostingProvider provider,
        string what,
        HostedRepository? about,
        Func<IHostingProvider, string, HostingAccount, CancellationToken, Task<HostingResult<T>>> call,
        CancellationToken ct)
    {
        if (EntryOf(provider) is not { } entry)
        {
            return HostingResult<T>.Fail(TurnedOff(provider));
        }

        if (entry.Slot is not { IsSignedIn: true, Token: { } token } slot)
        {
            return HostingResult<T>.Fail(new HostingFailure(HostingFailureKind.BadCredentials, "Not signed in.") { Provider = provider });
        }

        if (about is not null && !entry.Provider.Covers(about, slot.Key))
        {
            return HostingResult<T>.Fail(new HostingFailure(HostingFailureKind.Validation, "Not the signed-in account's repository.") { Provider = provider });
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        var result = await call(entry.Provider, token, slot.Status.Account, linked.Token).ConfigureAwait(false);
        if (result.Failure is { Kind: HostingFailureKind.BadCredentials } refused)
        {
            await MarkRefusedAsync(entry, slot.Key, token, $"the API, request {refused.RequestId ?? "without an id"}", linked.Token).ConfigureAwait(false);
        }
        else if (result.Failure is { } failure)
        {
            _log.Warning($"{entry.Name}: {what} failed: {Describe(failure)}");
        }

        return result;
    }

    /// <summary><see cref="CallAsync{T}"/> on GitHub with its typed provider, for A6 (D349).</summary>
    internal Task<HostingResult<T>> CallGitHubAsync<T>(
        string what,
        HostedRepository? about,
        Func<GitHubProvider, string, CancellationToken, Task<HostingResult<T>>> call,
        CancellationToken ct) =>
        CallAsync(HostingProvider.GitHub, what, about, (provider, token, _, linked) => call((GitHubProvider)provider, token, linked), ct);

    /// <summary>
    /// The union of the accounts' bindings of <paramref name="effective"/> (docs/phase-7b-design.md
    /// 9.6): for each provider on, in D353's order, <see cref="HostingRules.Bindings(AccountStatus?, bool, bool, IReadOnlyList{RemoteEffectiveUrls}, Func{AccountKey, GitCoverage})"/>
    /// with its coverage, concatenated.
    /// </summary>
    internal IReadOnlyList<CredentialBinding> BindingsOf(IReadOnlyList<RemoteEffectiveUrls> effective, bool rulesKnown)
    {
        List<CredentialBinding>? union = null;
        IReadOnlyList<CredentialBinding> only = [];
        foreach (var entry in _entries)
        {
            var slot = entry.Slot;
            var bindings = HostingRules.Bindings(slot?.Status, slot?.IsSignedIn == true, rulesKnown, effective, entry.Provider.CoverageOf);
            if (bindings.Count == 0)
            {
                continue;
            }

            if (only.Count == 0 && union is null)
            {
                only = bindings;
                continue;
            }

            union ??= [.. only];
            union.AddRange(bindings);
        }

        return union ?? only;
    }

    /// <summary>
    /// Runs a tab's avatar work (7.9) on the thread pool under the tab's lifetime and the session's
    /// avatar work, which GitHub's sign-out cancels and waits for before it deletes <c>avatars/</c>
    /// (D291). Null, and nothing runs, once the session is disposed or GitHub's account is not signed in.
    /// </summary>
    internal Task? RunAvatarWork(Func<CancellationToken, Task> work, CancellationToken hostingLifetime)
    {
        lock (_lock)
        {
            // A sign-out clears the slot before it takes the runs under this lock: a run that comes
            // in after them finds the slot cleared, one that came before is among them (D291).
            if (_disposed || SlotOf(HostingProvider.GitHub) is not { IsSignedIn: true })
            {
                return null;
            }

            var linked = CancellationTokenSource.CreateLinkedTokenSource(hostingLifetime, _avatarWork.Token);
            var run = Task.Run(() => work(linked.Token), CancellationToken.None);
            _avatarRuns.Add(run);
            _ = run.ContinueWith(
                ended =>
                {
                    lock (_lock)
                    {
                        _avatarRuns.Remove(ended);
                    }

                    linked.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return run;
        }
    }

    /// <summary>A hosting's <c>Dispose</c>: it is told of the accounts no more.</summary>
    internal void Remove(RepositoryHosting hosting)
    {
        lock (_lock)
        {
            _hostings.Remove(hosting);
        }
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread through the post delegate, unless the session is disposed by then.</summary>
    internal void Post(Action action)
    {
        if (_disposed)
        {
            return;
        }

        _post(() =>
        {
            if (!_disposed)
            {
                action();
            }
        });
    }

    /// <summary>"{Name} is turned off.", GitHub's 7a's (docs/phase-7b-design.md 9.4).</summary>
    private static HostingFailure TurnedOff(HostingProvider provider) =>
        new(HostingFailureKind.Validation, $"{HostingProviders.DisplayName(provider)} is turned off.") { Provider = provider };

    private static string Describe(HostingFailure failure) =>
        $"{failure.Kind}, {failure.Message}{(failure.RequestId is null ? string.Empty : $" (request {failure.RequestId})")}";

    private static AccountSlot NeedsSignIn(HostingAccount account, AccountNoteKind noteKind, string? note, string tokenState) =>
        new(new AccountStatus(account, AccountState.NeedsSignIn, null, noteKind, note), null, tokenState);

    /// <summary>
    /// D382: an A1 answer that names the token itself, ignoring case, in the login (Azure DevOps'
    /// organization), the display name or the avatar's host is the provider's malformed answer, so
    /// that no record, slot or sign-in line carries an echoed token; null for any other answer.
    /// </summary>
    private static HostingFailure? EchoedToken(HostingProvider provider, HostingAccount account, string token)
    {
        if (token.Length == 0)
        {
            return null;
        }

        string?[] texts = [account.Login, account.Key.Login, account.Name, account.AvatarUrl?.Host];
        return texts.Any(text => text is not null && text.Contains(token, StringComparison.OrdinalIgnoreCase))
            ? new HostingFailure(HostingFailureKind.Malformed, HostingHttpTexts.Malformed(provider)) { Provider = provider }
            : null;
    }

    /// <summary>
    /// The same user as held (D357): Azure DevOps by its user id without case, GitHub and GitLab by
    /// their id; null when the held account has none (a 7a save: <c>userId</c> dropped, an id of 0)
    /// or the check gave none.
    /// </summary>
    private static bool? SameUser(HostingProvider provider, HostingAccount held, HostingAccount answered) =>
        provider == HostingProvider.AzureDevOps
            ? string.IsNullOrEmpty(held.UserId) || string.IsNullOrEmpty(answered.UserId) ? null : string.Equals(held.UserId, answered.UserId, StringComparison.OrdinalIgnoreCase)
            : held.Id == 0 || answered.Id == 0 ? null : held.Id == answered.Id;

    /// <summary>Two providers on one web origin (scheme, host and port, without case): the broker answers from the first binding on an origin (<c>AskPassBroker</c>), so one origin serves one account.</summary>
    private static void RefuseSharedOrigins(HostingOptions options)
    {
        Uri?[] webBases = [options.GitHub?.WebBase, options.GitLab?.WebBase, options.AzureDevOps?.WebBase];
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var webBase in webBases)
        {
            if (webBase is not null && !origins.Add(FormattableString.Invariant($"{webBase.Scheme}://{webBase.Host}:{webBase.Port}")))
            {
                throw new ArgumentException($"Two hosting providers are on {webBase.GetLeftPart(UriPartial.Authority)}: each web origin serves one provider.", nameof(options));
            }
        }
    }

    private Entry? EntryOf(HostingProvider provider)
    {
        foreach (var entry in _entries)
        {
            if (entry.Kind == provider)
            {
                return entry;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<HostingProvider>> StartCoreAsync(CancellationToken ct)
    {
        var changed = new List<HostingProvider>();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            _file = _accounts.Load();
            foreach (var entry in _entries)
            {
                if (await ReadAccountAsync(entry, linked.Token).ConfigureAwait(false))
                {
                    changed.Add(entry.Kind);
                }
            }

            return changed;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Under _gate, at the start: one provider's record taken in 7a's states by its token state,
    // a "keychain" record with the run's only keychain read for it (ruling 6).
    private async Task<bool> ReadAccountAsync(Entry entry, CancellationToken ct)
    {
        if (_file.For(entry.Kind, entry.Host) is not { Login: { } login } record || string.IsNullOrWhiteSpace(login))
        {
            _log.Info($"{entry.Name}: no account on {entry.Host}.");
            return false;
        }

        // The new providers' records have no avatar (D349): one a hand-edited file gives is not read.
        var avatar = entry.Kind == HostingProvider.GitHub && Uri.TryCreate(record.AvatarUrl, UriKind.Absolute, out var read) ? read : null;
        var account = new HostingAccount(new AccountKey(entry.Kind, entry.Host, login), login, record.Id, record.Name, avatar, record.UseForGit, record.UserId);
        var tokenState = AccountTokenStates.Of(record.TokenState);
        AccountSlot slot;
        string outcome;
        if (tokenState == AccountTokenStates.Session)
        {
            slot = NeedsSignIn(account, AccountNoteKind.SessionEnded, null, tokenState);
            outcome = "its token was kept for the last session only; it needs signing in";
        }
        else if (tokenState == AccountTokenStates.Refused)
        {
            slot = NeedsSignIn(account, AccountNoteKind.Refused, null, tokenState);
            outcome = $"{entry.Name} refused its token; it needs signing in";
        }
        else
        {
            var token = await _vault!.ReadAsync(TokenKey.Of(account.Key), ct).ConfigureAwait(false);
            (slot, outcome) = token switch
            {
                { Outcome: TokenStoreOutcome.Done, Token: { Length: > 0 } value } =>
                    (new AccountSlot(new AccountStatus(account, AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None, null), value, tokenState), "token read from the keychain"),
                { Outcome: TokenStoreOutcome.Failed } =>
                    (NeedsSignIn(account, AccountNoteKind.ReadFailed, token.Reason, tokenState), $"the keychain's token could not be read ({token.Reason}); it needs signing in"),
                _ => (NeedsSignIn(account, AccountNoteKind.TokenMissing, null, tokenState), "no token in the keychain; it needs signing in"),
            };
        }

        entry.Slot = slot;
        _log.Info($"{entry.Name} account {login} on {entry.Host}: {outcome}.");
        return true;
    }

    // Under _gate: the record written, then the slot replaced (7.5 step 7), so that whoever sees
    // the new slot finds its record on the disk. One provider's record; the others are kept.
    private void SetSlotLocked(Entry entry, AccountSlot slot)
    {
        var account = slot.Status.Account;
        _file = _file.With(new AccountRecord
        {
            Provider = HostingProviders.Id(entry.Kind),
            Host = entry.Host,
            Login = account.Login,
            Id = account.Id,
            Name = account.Name,
            AvatarUrl = account.AvatarUrl?.AbsoluteUri,
            UseForGit = account.UseForGit,
            TokenState = slot.TokenState,
            UserId = account.UserId,
        });
        _accounts.Save(_file);
        entry.Slot = slot;
    }

    private void PostAccountChanged(HostingProvider provider) =>
        Post(() =>
        {
            AccountChanged?.Invoke(this, new HostingAccountChangedEventArgs(provider));
            foreach (var hosting in Hostings())
            {
                hosting.OnAccountChanged(provider);
            }
        });

    private RepositoryHosting[] Hostings()
    {
        lock (_lock)
        {
            return [.. _hostings];
        }
    }

    private AvatarCache NewAvatarCache(HostingOptions options) => new(_paths.AvatarsDirectory, options.CreateHandler, Clock, _log);

    private async Task<string?> SignOutCoreAsync(Entry entry, CancellationToken ct)
    {
        await _started.Task.WaitAsync(ct).ConfigureAwait(false);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (entry.Slot is not { } slot)
            {
                return null;
            }

            // Not cut off by the session's Dispose (the process's exit still ends it): a record
            // without its token would need signing in at the next start, and avatars/ would stay
            // (D291). The vault's delete is bounded by the store's own limits.
            var reason = await _vault!.ForgetAsync(TokenKey.Of(slot.Key), CancellationToken.None).ConfigureAwait(false)
                ?? await KeptUnreachedAsync(slot.Key, slot.TokenState).ConfigureAwait(false);
            _file = _file.Without(entry.Kind, entry.Host);
            _accounts.Save(_file);
            entry.Slot = null;

            // No request starts with the token from here on; what the service answered to it goes too.
            ForgetKept(entry.Provider);
            _log.Info(reason is null
                ? $"{entry.Name} account {slot.Status.Account.Login} on {entry.Host}: signed out."
                : $"{entry.Name} account {slot.Status.Account.Login} on {entry.Host}: signed out; the keychain kept its entry: {reason}.");
            PostAccountChanged(entry.Kind);
            if (entry.Kind == HostingProvider.GitHub)
            {
                await ForgetAvatarsAsync().ConfigureAwait(false);
            }

            return reason;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void ForgetKept(IHostingProvider provider)
    {
        switch (provider)
        {
            case GitHubProvider gitHub:
                gitHub.ForgetKept();
                break;
            case GitLabProvider gitLab:
                gitLab.ForgetKept();
                break;
            case AzureDevOpsProvider azureDevOps:
                azureDevOps.ForgetKept();
                break;
        }
    }

    // After the vault's forget answered null (7.5): without a keychain in this run it deletes
    // nothing (D272), yet a record whose token state is "keychain" was saved there by an earlier
    // run, so its entry may stay. The log names it for whoever removes it by hand, and the reason
    // goes to the caller as the keychain's. Null when the keychain was reached or the record never
    // used it. The probe was made by the forget, so this answers at once.
    private async Task<string?> KeptUnreachedAsync(AccountKey key, string tokenState)
    {
        if (tokenState != AccountTokenStates.Keychain || await _vault!.KeychainAvailableAsync(CancellationToken.None).ConfigureAwait(false))
        {
            return null;
        }

        var tokenKey = TokenKey.Of(key);
        var entry = TokenEntryNames.TryFor(tokenKey, TokenEntryNames.Application, out var names) ? names.Entry : TokenEntryNames.ForLog(tokenKey);
        _log.Warning($"The keychain could not be reached in this run; it may still hold the entry {entry}. Remove it by hand.");
        return TokenStoreReasons.NoKeychain;
    }

    // Under _gate, at GitHub's sign-out (7.5, D291): the avatar work cancelled and awaited, the maps
    // dropped from memory, a fresh cache, and avatars/ deleted whole; a file that stays gets one
    // warning and the rest still goes.
    private async Task ForgetAvatarsAsync()
    {
        Task[] runs;
        CancellationTokenSource ended;
        lock (_lock)
        {
            ended = _avatarWork;
            _avatarWork = new CancellationTokenSource();
            runs = [.. _avatarRuns];
        }

        await ended.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(runs).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Each run logs its own failure; a cancelled one ended as asked.
        }

        _authorMaps!.Forget();
        Interlocked.Exchange(ref _avatarCache, _disposed ? null : NewAvatarCache(_options))?.Dispose();
        var directory = _paths.AvatarsDirectory;
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true }))
        {
            try
            {
                DeleteAvatarFile(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // By its type alone, as every log call of an exception here (D382).
                _log.Warning($"{file} could not be deleted at the sign-out ({HostingHttpClient.Describe(ex)}).");
            }
        }

        foreach (var folder in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length).Append(directory))
        {
            try
            {
                Directory.Delete(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder that still holds a file that stays; that file was named above.
            }
        }
    }

    private async Task SwitchAfterAsync(Entry entry, Task earlier, bool useForGit)
    {
        await earlier.ConfigureAwait(false);
        try
        {
            var lifetime = _lifetime.Token;
            await _started.Task.WaitAsync(lifetime).ConfigureAwait(false);
            await _gate.WaitAsync(lifetime).ConfigureAwait(false);
            try
            {
                if (entry.Slot is not { } slot || slot.Status.Account.UseForGit == useForGit)
                {
                    return;
                }

                var status = slot.Status with { Account = slot.Status.Account with { UseForGit = useForGit } };
                SetSlotLocked(entry, new AccountSlot(status, slot.Token, slot.TokenState));
                _log.Info($"{entry.Name} account {status.Account.Login} on {entry.Host}: {(useForGit ? "used" : "not used")} for git over HTTPS.");
                PostAccountChanged(entry.Kind);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The session closed first.
        }
    }

    private async Task CheckRefusalAsync(Entry entry, AccountSlot slot)
    {
        var held = slot.Status.Account;
        var login = held.Login;
        try
        {
            // Azure DevOps' account is its organization's: the check names it (D360).
            var organization = entry.Kind == HostingProvider.AzureDevOps ? login : null;
            var checkedToken = await entry.Provider.CheckTokenAsync(slot.Token!, organization, _lifetime.Token).ConfigureAwait(false);
            if (checkedToken.Failure is { Kind: HostingFailureKind.BadCredentials })
            {
                await MarkRefusedAsync(entry, slot.Key, slot.Token!, "git", _lifetime.Token).ConfigureAwait(false);
            }
            else if (checkedToken.Failure is { } failure)
            {
                _log.Warning($"{entry.Name} account {login} on {entry.Host}: git was refused; the token could not be checked: {failure.Message}");
            }
            else if (SameUser(entry.Kind, held, checkedToken.Value!) is false)
            {
                await MarkRefusedAsync(entry, slot.Key, slot.Token!, "git, another user", _lifetime.Token).ConfigureAwait(false);
            }
            else
            {
                _log.Warning($"{entry.Name} account {login} on {entry.Host}: git was refused, yet the token is valid (an http.extraHeader, a proxy or a redirect?).");
                if (SameUser(entry.Kind, held, checkedToken.Value!) is null)
                {
                    await TakeUserAsync(entry, slot, checkedToken.Value!, _lifetime.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The session closed first.
        }
        catch (Exception ex)
        {
            // The exception by its type and codes alone, never its text, which can quote what the
            // server or a proxy sent (D382, H1's rule).
            _log.Warning($"{entry.Name} account {login} on {entry.Host}: git was refused; the token could not be checked ({HostingHttpClient.Describe(ex)}).");
        }
    }

    // A check of git's refusal answered for an account held without its user (a 7a save: no
    // userId, an id of 0; docs/phase-7b-design.md 9.5): the check's user is taken, the record
    // written and the event posted, while the slot is still that account's with that token.
    private async Task TakeUserAsync(Entry entry, AccountSlot slot, HostingAccount answered, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(answered.UserId) && answered.Id == 0)
        {
            return;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (entry.Slot is not { IsSignedIn: true } current || current.Key != slot.Key || !string.Equals(current.Token, slot.Token, StringComparison.Ordinal))
            {
                return;
            }

            var account = current.Status.Account;
            var updated = entry.Kind == HostingProvider.AzureDevOps
                ? account with { UserId = string.IsNullOrEmpty(account.UserId) ? answered.UserId : account.UserId }
                : account with { Id = account.Id == 0 ? answered.Id : account.Id };
            if (updated == account)
            {
                return;
            }

            SetSlotLocked(entry, new AccountSlot(current.Status with { Account = updated }, current.Token, current.TokenState));
            PostAccountChanged(entry.Kind);
        }
        finally
        {
            _gate.Release();
        }
    }

    // A 401 with the account's token, or git's refusal checked for another user (7.6, 9.5): when it
    // is still the signed-in account's token, the account needs signing in, its token is dropped and
    // its record says refused.
    private async Task MarkRefusedAsync(Entry entry, AccountKey key, string token, string source, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (entry.Slot is not { IsSignedIn: true } slot || slot.Key != key || !string.Equals(slot.Token, token, StringComparison.Ordinal))
            {
                return;
            }

            SetSlotLocked(entry, NeedsSignIn(slot.Status.Account, AccountNoteKind.Refused, null, AccountTokenStates.Refused));
            _log.Warning($"{entry.Name} account {slot.Status.Account.Login} on {entry.Host}: {entry.Name} refused the token ({source}); it needs signing in again.");
            PostAccountChanged(entry.Kind);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>One provider that is on (docs/phase-7b-design.md 9.2): its provider, its account's slot, its refusal check and its switch chain.</summary>
    private sealed class Entry(HostingProvider kind, IHostingProvider provider)
    {
        private AccountSlot? _slot;

        public HostingProvider Kind { get; } = kind;

        public IHostingProvider Provider { get; } = provider;

        public string Name => HostingProviders.DisplayName(Kind);

        /// <summary>Its accounts' <see cref="AccountKey.Host"/>.</summary>
        public string Host => Provider.Host;

        /// <summary>The account as held now; swapped whole, read without a lock, written under the gate.</summary>
        public AccountSlot? Slot
        {
            get => Volatile.Read(ref _slot);
            set => Volatile.Write(ref _slot, value);
        }

        /// <summary>The last check of git's refusal; under the session's lock.</summary>
        public Task? RefusalCheck { get; set; }

        /// <summary>The last "Use for git over HTTPS" change's write; under the session's lock.</summary>
        public Task Switching { get; set; } = Task.CompletedTask;

        public override string ToString() => $"Entry({Kind}, {Host})";
    }
}

/// <summary>
/// The account as the session holds it (docs/phase-7a-design.md 7.3): its status, its token and
/// the record's token state. A class, not a record, whose generated text would show the token: its
/// text is the login and the state only.
/// </summary>
internal sealed class AccountSlot(AccountStatus status, string? token, string tokenState)
{
    public AccountStatus Status { get; } = status;

    /// <summary>The token, while the account is signed in; null otherwise.</summary>
    public string? Token { get; } = token;

    /// <summary>The record's <c>tokenState</c> (<see cref="AccountTokenStates"/>).</summary>
    public string TokenState { get; } = tokenState;

    public AccountKey Key => Status.Account.Key;

    /// <summary>Signed in with the token in hand.</summary>
    public bool IsSignedIn => Status.State == AccountState.SignedIn && Token is not null;

    /// <summary>Signed in, the token in hand and "Use for git over HTTPS" on.</summary>
    public bool BindsGit => IsSignedIn && Status.Account.UseForGit;

    public override string ToString() => $"AccountSlot({Status.Account.Login}, {Status.State})";
}
