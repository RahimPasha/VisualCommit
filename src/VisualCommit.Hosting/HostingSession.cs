using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Hosting.Tokens;

namespace VisualCommit.Hosting;

/// <summary>
/// The app session's hosting (docs/phase-7a-design.md 7.3 to 7.7): GitHub's provider, the one
/// account (D263) and its token, the bindings of git's credentials to it (D255) and git's requests
/// for them, and the hosting of each repository tab. The account is held in one slot that is
/// swapped whole and read without a lock, so git's requests and the bindings never wait on the UI
/// thread; the start's read, a sign-in's second half, a sign-out, a refusal and the switch run one
/// at a time under a gate, and no network call runs under it. Events reach the UI thread through
/// the post delegate. Off (<see cref="HostingOptions.GitHub"/> null), nothing is read, recognised
/// or asked.
/// </summary>
public sealed class HostingSession : IHostingSession
{
    /// <summary>How long a request for a token waits for the start's keychain read (ruling 6); git's cannot come before it ends (<see cref="GetAsync"/>).</summary>
    internal static readonly TimeSpan CredentialWait = TimeSpan.FromSeconds(10);

    private static readonly HostingFailure NotSignedIn = new(HostingFailureKind.BadCredentials, "Not signed in.");

    private static readonly HostingFailure TurnedOff = new(HostingFailureKind.Validation, "GitHub is turned off.");

    private readonly AppPaths _paths;
    private readonly HostingOptions _options;
    private readonly IAppLog _log;
    private readonly Action<Action> _post;
    private readonly GitHubProvider? _provider;
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
    private AccountSlot? _slot;

    private readonly AuthorMapStore? _authorMaps;
    private AvatarCache? _avatarCache;

    // Under _lock.
    private readonly HashSet<Task> _avatarRuns = [];
    private CancellationTokenSource _avatarWork = new();
    private Task? _refusalCheck;
    private Task _switching = Task.CompletedTask;
    private int _startCalled;
    private volatile bool _disposed;

    public HostingSession(AppPaths paths, HostingOptions options, ITokenStore tokens, IAppLog log, Action<Action> post, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(clock);
        _paths = paths;
        _options = options;
        _log = log;
        _post = post;
        Clock = clock;
        _accounts = new JsonAccountStore(paths.AccountsFile, log);
        if (options.GitHub is { } endpoints)
        {
            _provider = new GitHubProvider(endpoints, options.CreateHandler, log, clock);
            _vault = new TokenVault(tokens, log);
            _avatarCache = NewAvatarCache(options);
            _authorMaps = new AuthorMapStore(Path.Combine(paths.AvatarsDirectory, "authors"), clock, log);
            log.Info($"GitHub: {endpoints.Host}.");
        }
        else
        {
            log.Info("GitHub: off.");
        }
    }

    public bool IsOff => _provider is null;

    public IHostingProvider? GitHub => _provider;

    public AccountStatus? Account => Volatile.Read(ref _slot)?.Status;

    public event EventHandler? AccountChanged;

    public bool BindsGit => _provider is not null && Volatile.Read(ref _slot) is { BindsGit: true };

    /// <summary>The account as held now, for the repositories' hostings; null without one.</summary>
    internal AccountSlot? Slot => Volatile.Read(ref _slot);

    /// <summary>GitHub's endpoints; only while on.</summary>
    internal GitHubEndpoints Endpoints => _provider?.Endpoints ?? throw new InvalidOperationException("GitHub is off.");

    internal IAppLog Log => _log;

    internal TimeProvider Clock { get; }

    /// <summary>The session's lifetime: cancelled at <see cref="Dispose"/>.</summary>
    internal CancellationToken Lifetime => _lifetime.Token;

    /// <summary>The avatars' images (6.4); a new one after each sign-out.</summary>
    internal AvatarCache AvatarCache => Volatile.Read(ref _avatarCache) ?? throw new InvalidOperationException("GitHub is off.");

    /// <summary>The e-mail maps and the lookups' budget (7.9).</summary>
    internal AuthorMapStore AuthorMaps => _authorMaps ?? throw new InvalidOperationException("GitHub is off.");

    /// <summary>The last "Use for git over HTTPS" change's write, for the tests.</summary>
    internal Task Switching
    {
        get
        {
            lock (_lock)
            {
                return _switching;
            }
        }
    }

    /// <summary>The last check of git's refusal, null before the first, for the tests.</summary>
    internal Task? RefusalCheck
    {
        get
        {
            lock (_lock)
            {
                return _refusalCheck;
            }
        }
    }

    /// <summary>How many repositories' hostings are told of the account's changes, for the tests.</summary>
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

    /// <summary>
    /// Reads <c>accounts.json</c> and, for a record whose token is in the keychain, the keychain once
    /// (7.4, ruling 6, D287); makes no request to GitHub. Without a record for this host no store is
    /// touched. Runs on the thread pool and reaches the UI thread only through the post delegate.
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _startCalled, 1) == 1)
        {
            await _started.Task.WaitAsync(ct).ConfigureAwait(false);
            return;
        }

        var changed = false;
        try
        {
            if (_provider is not null)
            {
                changed = await StartCoreAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _started.TrySetResult();
        }

        if (changed)
        {
            PostAccountChanged();
        }
    }

    public async Task<bool> KeychainAvailableAsync(CancellationToken ct) =>
        _vault is not null && await _vault.KeychainAvailableAsync(ct).ConfigureAwait(false);

    /// <summary>
    /// Signs in with <paramref name="token"/> (7.5; D256, D263, D288): A1 first, honouring
    /// <paramref name="ct"/>; a failure changes nothing. Once GitHub has answered, the rest
    /// completes under the session's lifetime: the previous account's key forgotten when it differs,
    /// the token kept (the keychain, else the session), the record written, the account replaced.
    /// </summary>
    public async Task<SignInResult> SignInAsync(string token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (_provider is null || _vault is null)
        {
            return new SignInResult(null, TurnedOff, null);
        }

        HostingResult<HostingAccount> checkedToken;
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token))
        {
            checkedToken = await _provider.CheckTokenAsync(token, linked.Token).ConfigureAwait(false);
        }

        if (checkedToken.Failure is { } failure)
        {
            _log.Warning($"GitHub: signing in on {Host} failed: {Describe(failure)}");
            return new SignInResult(null, failure, null);
        }

        var lifetime = _lifetime.Token;
        await _started.Task.WaitAsync(lifetime).ConfigureAwait(false);
        await _gate.WaitAsync(lifetime).ConfigureAwait(false);
        try
        {
            var checkedAccount = checkedToken.Value!;
            var key = new AccountKey(HostingProvider.GitHub, Host, checkedAccount.Login);
            var previous = _file.GitHubOn(Host);
            if (previous is { Login: { } oldLogin } && !string.IsNullOrWhiteSpace(oldLogin)
                && new AccountKey(HostingProvider.GitHub, Host, oldLogin) is var oldKey && oldKey != key
                && (await _vault.ForgetAsync(TokenKey.Of(oldKey), lifetime).ConfigureAwait(false)
                    ?? await KeptUnreachedAsync(oldKey, AccountTokenStates.Of(previous.TokenState)).ConfigureAwait(false)) is { } kept)
            {
                _log.Warning($"GitHub account {oldLogin} on {Host}: the keychain kept its entry: {kept}.");
            }

            var account = checkedAccount with { Key = key, UseForGit = previous?.UseForGit ?? true };
            var (place, reason) = await _vault.KeepAsync(TokenKey.Of(key), token, lifetime).ConfigureAwait(false);

            // No keychain at all (D253) is told apart from a save that failed (ruling 19) by the
            // vault's reason: only the latter is a "save failed" for the dialog.
            var (tokenState, noteKind, note, saveFailedReason) = (place, reason) switch
            {
                (TokenPlace.Keychain, _) => (AccountTokenStates.Keychain, AccountNoteKind.None, (string?)null, (string?)null),
                (_, null or TokenStoreReasons.NoKeychain) => (AccountTokenStates.Session, AccountNoteKind.NoKeychain, null, null),
                _ => (AccountTokenStates.Session, AccountNoteKind.SaveFailed, reason, reason),
            };
            var slot = new AccountSlot(new AccountStatus(account, AccountState.SignedIn, place, noteKind, note), token, tokenState);
            SetSlotLocked(slot);
            _log.Info(place == TokenPlace.Keychain
                ? $"GitHub account {account.Login} on {Host}: signed in; the token is kept in the keychain."
                : $"GitHub account {account.Login} on {Host}: signed in; the token is kept for this session: {reason ?? TokenStoreReasons.NoKeychain}.");
            PostAccountChanged();
            return new SignInResult(slot.Status, null, saveFailedReason);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Signs out (7.5, D291): the keychain's entry forgotten (null, or why it was kept; without a
    /// keychain in this run, <see cref="TokenStoreReasons.NoKeychain"/> for a record whose token an
    /// earlier run saved there), the record removed even when the entry was kept, the account
    /// cleared. Runs on the thread pool, never on the caller's thread (the vault answers at once
    /// without a keychain). <paramref name="ct"/> stops the waits before the gate only: once begun,
    /// the sign-out completes when the session is disposed meanwhile, but nothing keeps the process
    /// alive for it. An exit while the store's delete runs leaves the record and its entry, and the
    /// next start is signed in. Only an exit just after the delete, or a macOS <c>security</c>
    /// child that ends after the app, leaves the record without its entry (the next start says the
    /// token is missing) or <c>avatars/</c> without a record.
    /// </summary>
    public Task<string?> SignOutAsync(CancellationToken ct) =>
        _provider is null || _vault is null ? Task.FromResult<string?>(null) : Task.Run(() => SignOutCoreAsync(ct), CancellationToken.None);

    /// <summary>
    /// "Use for git over HTTPS" at the settings screen's Save (D216): on the thread pool after the
    /// change before it, the value compared under the gate (no slot or the same value: nothing);
    /// the record written and the events posted.
    /// </summary>
    public void SetUseForGit(bool useForGit)
    {
        if (_provider is null || _disposed)
        {
            return;
        }

        lock (_lock)
        {
            var earlier = _switching;
            _switching = Task.Run(() => SwitchAfterAsync(earlier, useForGit), CancellationToken.None);
        }
    }

    public HostedRepository? Recognise(string url) => _provider?.Recognise(url);

    public bool SourceCanExist(string remoteUrl, string remoteRef) =>
        _provider is null || _provider.Recognise(remoteUrl) is null || _provider.SourceCanExist(remoteRef);

    /// <summary>A clone of <paramref name="url"/> (after the user's <c>insteadOf</c>, 9.3, D331): bound while the account binds git.</summary>
    public IReadOnlyList<CredentialBinding> BindingsForClone(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return _provider is not null && Volatile.Read(ref _slot) is { BindsGit: true } slot
            ? CredentialBinding.Select([new RemoteEffectiveUrls("origin", [url], [url])], _provider.Endpoints.WebBase, slot.Key)
            : [];
    }

    public IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites)
    {
        ArgumentNullException.ThrowIfNull(remoteUrls);
        ArgumentNullException.ThrowIfNull(rewrites);
        return _provider is not null && Volatile.Read(ref _slot) is { BindsGit: true } slot
            ? HostingRules.Bindings(slot.Status, hasToken: true, rulesKnown: true, rewrites.Apply(remoteUrls), _provider.Endpoints.WebBase)
            : [];
    }

    /// <summary>A2 with the account's token; signed out a failure without a request (D257).</summary>
    public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(CancellationToken ct) =>
        CallAsync("listing the account's repositories", (provider, token, linked) => provider.ListRepositoriesAsync(token, linked), ct);

    /// <summary>A new hosting for the tab of <paramref name="workingDirectory"/>, live until its <c>Dispose</c>; off, <see cref="NullRepositoryHosting.Instance"/>.</summary>
    public IRepositoryHosting ForRepository(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (_provider is null)
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
    /// is the signed-in account and "Use for git over HTTPS" is on; else null. Never UI. It waits
    /// for the start's read, at most 10 s by the session's clock, but through git no get comes
    /// before that read ends: nothing is bound until the read has put the token in the slot (7.4)
    /// and the tab's next refs read has read the rules, so an operation started meanwhile goes
    /// D82's order. The wait guards a direct call.
    /// </summary>
    public async Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (_provider is null)
        {
            return null;
        }

        try
        {
            await _started.Task.WaitAsync(CredentialWait, Clock, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _log.Warning($"GitHub: the keychain's read had not ended after 10 s; git's request for {account.Host} went without the account's token.");
            return null;
        }

        return Volatile.Read(ref _slot) is { BindsGit: true, Token: { } token } slot && slot.Key == account
            ? new GitCredential(token)
            : null;
    }

    /// <summary>
    /// git's <c>erase</c> to the app's helper (7.6, D330): returns at once. For the signed-in
    /// account, one A1 with its token checks it, at most one at a time (a refusal meanwhile joins
    /// it): a 401 marks the account refused; a 200 or any other answer keeps it, with a warning.
    /// </summary>
    public void Refused(AccountKey account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (_provider is null || Volatile.Read(ref _slot) is not { IsSignedIn: true } slot || slot.Key != account)
        {
            return;
        }

        lock (_lock)
        {
            if (_refusalCheck is { IsCompleted: false } || _disposed)
            {
                return;
            }

            _refusalCheck = Task.Run(() => CheckRefusalAsync(slot));
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

        _provider?.Dispose();
        _avatarCache?.Dispose();
    }

    /// <summary>
    /// One call to GitHub with the account's token (7.6): none without a signed-in account (D257);
    /// <paramref name="ct"/> linked to the session's lifetime; a 401 marks the account refused, any
    /// other failure is a warning naming <paramref name="what"/>.
    /// </summary>
    internal async Task<HostingResult<T>> CallAsync<T>(string what, Func<GitHubProvider, string, CancellationToken, Task<HostingResult<T>>> call, CancellationToken ct)
    {
        if (_provider is null)
        {
            return HostingResult<T>.Fail(TurnedOff);
        }

        if (Volatile.Read(ref _slot) is not { IsSignedIn: true, Token: { } token } slot)
        {
            return HostingResult<T>.Fail(NotSignedIn);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        var result = await call(_provider, token, linked.Token).ConfigureAwait(false);
        if (result.Failure is { Kind: HostingFailureKind.BadCredentials } refused)
        {
            await MarkRefusedAsync(slot.Key, token, $"the API, request {refused.RequestId ?? "without an id"}", linked.Token).ConfigureAwait(false);
        }
        else if (result.Failure is { } failure)
        {
            _log.Warning($"GitHub: {what} failed: {Describe(failure)}");
        }

        return result;
    }

    /// <summary>
    /// Runs a tab's avatar work (7.9) on the thread pool under the tab's lifetime and the session's
    /// avatar work, which a sign-out cancels and waits for before it deletes <c>avatars/</c> (D291).
    /// Null, and nothing runs, once the session is disposed or the account is not signed in.
    /// </summary>
    internal Task? RunAvatarWork(Func<CancellationToken, Task> work, CancellationToken hostingLifetime)
    {
        lock (_lock)
        {
            // A sign-out clears the slot before it takes the runs under this lock: a run that comes
            // in after them finds the slot cleared, one that came before is among them (D291).
            if (_disposed || Volatile.Read(ref _slot) is not { IsSignedIn: true })
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

    /// <summary>A hosting's <c>Dispose</c>: it is told of the account no more.</summary>
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

    private string Host => Endpoints.Host;

    private static string Describe(HostingFailure failure) =>
        $"{failure.Kind}, {failure.Message}{(failure.RequestId is null ? string.Empty : $" (request {failure.RequestId})")}";

    private static AccountSlot NeedsSignIn(HostingAccount account, AccountNoteKind noteKind, string? note, string tokenState) =>
        new(new AccountStatus(account, AccountState.NeedsSignIn, null, noteKind, note), null, tokenState);

    private async Task<bool> StartCoreAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            _file = _accounts.Load();
            if (_file.GitHubOn(Host) is not { Login: { } login } record || string.IsNullOrWhiteSpace(login))
            {
                _log.Info($"GitHub: no account on {Host}.");
                return false;
            }

            var account = new HostingAccount(
                new AccountKey(HostingProvider.GitHub, Host, login),
                login,
                record.Id,
                record.Name,
                Uri.TryCreate(record.AvatarUrl, UriKind.Absolute, out var avatar) ? avatar : null,
                record.UseForGit);
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
                outcome = "GitHub refused its token; it needs signing in";
            }
            else
            {
                // The run's only keychain read (ruling 6).
                var read = await _vault!.ReadAsync(TokenKey.Of(account.Key), linked.Token).ConfigureAwait(false);
                (slot, outcome) = read switch
                {
                    { Outcome: TokenStoreOutcome.Done, Token: { Length: > 0 } token } =>
                        (new AccountSlot(new AccountStatus(account, AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None, null), token, tokenState), "token read from the keychain"),
                    { Outcome: TokenStoreOutcome.Failed } =>
                        (NeedsSignIn(account, AccountNoteKind.ReadFailed, read.Reason, tokenState), $"the keychain's token could not be read ({read.Reason}); it needs signing in"),
                    _ => (NeedsSignIn(account, AccountNoteKind.TokenMissing, null, tokenState), "no token in the keychain; it needs signing in"),
                };
            }

            Volatile.Write(ref _slot, slot);
            _log.Info($"GitHub account {login} on {Host}: {outcome}.");
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Under _gate: the record written, then the slot replaced (7.5 step 7), so that whoever sees
    // the new slot finds its record on the disk.
    private void SetSlotLocked(AccountSlot slot)
    {
        var account = slot.Status.Account;
        _file = _file.WithGitHub(new AccountRecord
        {
            Provider = AccountRecord.GitHubProvider,
            Host = Host,
            Login = account.Login,
            Id = account.Id,
            Name = account.Name,
            AvatarUrl = account.AvatarUrl?.AbsoluteUri,
            UseForGit = account.UseForGit,
            TokenState = slot.TokenState,
        });
        _accounts.Save(_file);
        Volatile.Write(ref _slot, slot);
    }

    private void PostAccountChanged() =>
        Post(() =>
        {
            AccountChanged?.Invoke(this, EventArgs.Empty);
            foreach (var hosting in Hostings())
            {
                hosting.OnAccountChanged();
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

    private async Task<string?> SignOutCoreAsync(CancellationToken ct)
    {
        await _started.Task.WaitAsync(ct).ConfigureAwait(false);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _slot) is not { } slot)
            {
                return null;
            }

            // Not cut off by the session's Dispose (the process's exit still ends it; see above): a
            // record without its token would need signing in at the next start, and avatars/ would
            // stay (D291). The vault's delete is bounded by the store's own limits.
            var reason = await _vault!.ForgetAsync(TokenKey.Of(slot.Key), CancellationToken.None).ConfigureAwait(false)
                ?? await KeptUnreachedAsync(slot.Key, slot.TokenState).ConfigureAwait(false);
            _file = _file.WithoutGitHub(Host);
            _accounts.Save(_file);
            Volatile.Write(ref _slot, null);
            _log.Info(reason is null
                ? $"GitHub account {slot.Status.Account.Login} on {Host}: signed out."
                : $"GitHub account {slot.Status.Account.Login} on {Host}: signed out; the keychain kept its entry: {reason}.");
            PostAccountChanged();
            await ForgetAvatarsAsync().ConfigureAwait(false);
            return reason;
        }
        finally
        {
            _gate.Release();
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

    // Under _gate, at a sign-out (7.5, D291): the avatar work cancelled and awaited, the maps
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
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warning($"{file} could not be deleted at the sign-out: {ex.Message}");
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

    private async Task SwitchAfterAsync(Task earlier, bool useForGit)
    {
        await earlier.ConfigureAwait(false);
        try
        {
            var lifetime = _lifetime.Token;
            await _started.Task.WaitAsync(lifetime).ConfigureAwait(false);
            await _gate.WaitAsync(lifetime).ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref _slot) is not { } slot || slot.Status.Account.UseForGit == useForGit)
                {
                    return;
                }

                var status = slot.Status with { Account = slot.Status.Account with { UseForGit = useForGit } };
                SetSlotLocked(new AccountSlot(status, slot.Token, slot.TokenState));
                _log.Info($"GitHub account {status.Account.Login} on {Host}: {(useForGit ? "used" : "not used")} for git over HTTPS.");
                PostAccountChanged();
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

    private async Task CheckRefusalAsync(AccountSlot slot)
    {
        var login = slot.Status.Account.Login;
        try
        {
            var checkedToken = await _provider!.CheckTokenAsync(slot.Token!, _lifetime.Token).ConfigureAwait(false);
            if (checkedToken.Failure is { Kind: HostingFailureKind.BadCredentials })
            {
                await MarkRefusedAsync(slot.Key, slot.Token!, "git", _lifetime.Token).ConfigureAwait(false);
            }
            else if (checkedToken.Failure is { } failure)
            {
                _log.Warning($"GitHub account {login} on {Host}: git was refused; the token could not be checked: {failure.Message}");
            }
            else
            {
                _log.Warning($"GitHub account {login} on {Host}: git was refused, yet the token is valid (an http.extraHeader, a proxy or a redirect?).");
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The session closed first.
        }
        catch (Exception ex)
        {
            _log.Warning($"GitHub account {login} on {Host}: git was refused; the token could not be checked: {ex.Message}");
        }
    }

    // A 401 with the account's token (7.6): when it is still the signed-in account's token, the
    // account needs signing in, its token is dropped and its record says refused.
    private async Task MarkRefusedAsync(AccountKey key, string token, string source, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _slot) is not { IsSignedIn: true } slot || slot.Key != key || !string.Equals(slot.Token, token, StringComparison.Ordinal))
            {
                return;
            }

            SetSlotLocked(NeedsSignIn(slot.Status.Account, AccountNoteKind.Refused, null, AccountTokenStates.Refused));
            _log.Warning($"GitHub account {slot.Status.Account.Login} on {Host}: GitHub refused the token ({source}); it needs signing in again.");
            PostAccountChanged();
        }
        finally
        {
            _gate.Release();
        }
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
