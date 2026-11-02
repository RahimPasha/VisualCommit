using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.Hosting;

/// <summary>
/// The hosting of one repository tab (docs/phase-7a-design.md 7.8; D251, D289, D290, D293;
/// docs/phase-7b-design.md 9.7): its remotes recognised by any provider on, the repository whose pull
/// requests are listed (the first recognised remote's, ruling 5) and the list, whose provider's account
/// decides the state, the requests and the refresh, the union of the accounts' bindings of git's
/// credentials, and the refs' pull requests. Its state changes on the UI
/// thread (<see cref="Update"/>, the account's changes, the refreshes' answers through the post
/// delegate); network work runs off it under the hosting's lifetime, which <see cref="Dispose"/>
/// ends. <see cref="State"/>, <see cref="Bindings"/> and <see cref="Avatars"/> are swapped whole and
/// may be read from any thread; <see cref="Changed"/> comes through the post delegate, only when one
/// of them, <see cref="WantsUrlRewrites"/> or the listed provider's account differs.
/// </summary>
public sealed partial class RepositoryHosting : IRepositoryHosting
{
    private static readonly HostingFailure NoRemote = new(HostingFailureKind.NotFound, "This repository has no hosted remote.");

    private static readonly IReadOnlyDictionary<string, PullRequest> NoPullRequests = new Dictionary<string, PullRequest>(StringComparer.Ordinal);

    private readonly HostingSession _session;
    private readonly string _name;
    private readonly CancellationTokenSource _lifetimeSource;
    private readonly CancellationToken _lifetime;
    private readonly Lock _infoLock = new();

    // On the UI thread.
    private RepoRefs? _refs;
    private IReadOnlyList<RemoteEffectiveUrls> _effective = [];
    private bool _rulesKnown;
    private IReadOnlyDictionary<string, HostedRepository> _remotes = new Dictionary<string, HostedRepository>(StringComparer.Ordinal);
    private HostedRepository? _candidate;
    private PullRequestList? _list;
    private HostingFailure? _failure;

    // The account the run whose answer is _list or _failure asked with: neither is shown, kept or
    // added to while the candidate's provider holds another (D348: one user swapped for another).
    private AccountKey? _listAccount;
    private Run? _running;
    private TaskCompletionSource? _again;

    // Under _infoLock: A3's answer for a candidate with the account it was read with, and the read
    // in flight with the account it was asked for: another account of the candidate's provider
    // (D348) uses neither, since what one account may read another may not.
    private (HostedRepository Candidate, AccountKey Account, RepositoryInfo Info)? _info;
    private (HostedRepository Candidate, AccountKey? Account, Task<HostingResult<RepositoryInfo>> Read)? _reading;

    private RepositoryHostingState _state = RepositoryHostingState.NoHostedRemote;
    private IReadOnlyList<CredentialBinding> _bindings = [];
    private AuthorAvatars _avatars = AuthorAvatars.None;

    // On the UI thread: WantsUrlRewrites as last raised, and the listed provider's account (its key
    // and its login as given) as last raised. While a Changed is delivered, _raising holds the value that
    // Changed carries (1 wanted, 0 not, -1 none delivered), which WantsUrlRewrites gives then.
    private bool _wantsRewrites;
    private volatile int _raising = -1;
    private (AccountKey Key, string Login)? _account;
    private volatile bool _disposed;

    internal RepositoryHosting(HostingSession session, string workingDirectory)
    {
        _session = session;
        _name = Path.GetFileName(Path.TrimEndingDirectorySeparator(workingDirectory));
        _lifetimeSource = CancellationTokenSource.CreateLinkedTokenSource(session.Lifetime);
        _lifetime = _lifetimeSource.Token;
        _wantsRewrites = session.AnySignedIn;
    }

    public RepositoryHostingState State => Volatile.Read(ref _state);

    public IReadOnlyList<CredentialBinding> Bindings => Volatile.Read(ref _bindings);

    public AuthorAvatars Avatars => Volatile.Read(ref _avatars);

    /// <summary>
    /// Some provider's account is signed in (with or without "Use for git over HTTPS"): the tab reads
    /// the URL rewrites with its refs, since the rules decide every provider's recognition (D290).
    /// It is the value last raised with <see cref="Changed"/> (set on the UI thread), never the
    /// session's live one, and within a handler of <see cref="Changed"/> the value that event
    /// carries: a handler sees an unwanted value raised before a wanted one even when the wanted one
    /// was raised before the unwanted was delivered, so the tab forgets its rules and reads them
    /// again, and <see cref="Update"/> counts them known by the same value the tab read (7.10).
    /// </summary>
    public bool WantsUrlRewrites => _raising switch
    {
        0 => false,
        1 => true,
        _ => _wantsRewrites,
    };

    public event EventHandler? Changed;

    /// <summary>The candidate's provider's account is signed in and covers it (docs/phase-7b-design.md 9.7): its list can be asked for.</summary>
    private bool CanList => _candidate is { } candidate && CanListFor(candidate);

    /// <summary>GitHub's account is signed in: the only one avatars are read with (D349).</summary>
    private bool GitHubSignedIn => _session.SlotOf(HostingProvider.GitHub) is { IsSignedIn: true };

    /// <summary>
    /// After each refs read, on the UI thread (7.8): the remotes recognised, the candidate, the
    /// bindings and the state again; a new candidate drops the list and, while signed in, starts a
    /// refresh (D289); no candidate left drops the avatars too. A new candidate of another provider
    /// than the run in flight cancels that run, whose answer then applies nothing, so that its list
    /// waits on no other provider's answer (docs/phase-7b-design.md 9.7); within one provider the run
    /// in flight ends first, as in 7a. The rewrites count as known only when they were wanted for
    /// this read (D290).
    /// </summary>
    public void Update(RepoRefs refs, UrlRewrites rewrites)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(rewrites);
        if (_disposed)
        {
            return;
        }

        _refs = refs;
        _effective = rewrites.Apply(refs.RemoteUrls);
        _rulesKnown = WantsUrlRewrites;
        _remotes = HostingRules.Remotes(refs.RemoteUrls, _effective, url => _session.Recognise(url));
        var candidate = HostingRules.ListedCandidate(_remotes);
        var changed = !Equals(candidate, _candidate);
        if (changed)
        {
            _candidate = candidate;
            ForgetList();
            if (_running is { } running && candidate is not null && running.Candidate.Provider != candidate.Provider)
            {
                // Its answer, or its failure's warning, is of a repository listed no more, and this
                // candidate's run would wait behind it up to that provider's time-out. The waits
                // asked of it meanwhile end with this candidate's run (RefreshPullRequestsAsync).
                _running = null;
                running.Stop.Cancel();
                if (!CanList && _again is { } waiting)
                {
                    _again = null;
                    waiting.TrySetResult();
                }
            }
        }

        if (changed && candidate?.Provider != HostingProvider.GitHub)
        {
            // No GitHub repository listed any more (D252, 11.3, D367): none, or another provider's.
            // The avatars go, and a run in flight brings none. A change to another GitHub repository
            // keeps them: the tab sends the same set again only after NoHostedRemote or a sign-out,
            // so dropping them there would leave none.
            StopAvatars();
            Apply(AuthorAvatars.None);
        }
        else
        {
            Apply();
        }

        if (changed && CanList)
        {
            _ = RefreshPullRequestsAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Reads the listed repository's open pull requests (7.8): A3 of the candidate once, then A4 of
    /// it or of its parent. One run at a time; a call during a run asks for one more, whose applied
    /// answer completes its task. <paramref name="ct"/> ends only the caller's wait. Without a
    /// candidate, or unless its provider's account is signed in and covers it, nothing is asked
    /// (D257, D356).
    /// </summary>
    public Task RefreshPullRequestsAsync(CancellationToken ct)
    {
        if (_disposed || !CanList)
        {
            return Task.CompletedTask;
        }

        if (_running is not null)
        {
            _again ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _again.Task.WaitAsync(ct);
        }

        // Waits left by a run that another provider's candidate cancelled (Update) end with this run.
        var done = _again ?? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _again = null;
        StartRun(done);
        return done.Task.WaitAsync(ct);
    }

    /// <summary>A3 of the candidate, kept per candidate (a failure is not kept; an answer whose names hold the token is malformed, D382); on the UI thread.</summary>
    public Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(CancellationToken ct) =>
        _candidate is { } candidate ? ReadInfoAsync(candidate, ct) : Task.FromResult(HostingResult<RepositoryInfo>.Fail(NoRemote));

    /// <summary>A5 on <paramref name="baseRepository"/> (D293): once created, on top of the held list when that is the listed repository.</summary>
    public async Task<HostingResult<PullRequest>> CreatePullRequestAsync(HostedRepository baseRepository, NewPullRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(baseRepository);
        ArgumentNullException.ThrowIfNull(request);
        AccountKey? createdWith = null;
        var result = await _session.CallAsync(
            baseRepository.Provider,
            $"creating a pull request on {baseRepository.FullName}",
            baseRepository,
            (provider, token, account, linked) =>
            {
                createdWith = account.Key;
                return provider.CreatePullRequestAsync(token, baseRepository, request, linked);
            },
            ct).ConfigureAwait(false);
        if (result.Value is { } created && createdWith is { } key)
        {
            _session.Post(() => AddCreated(baseRepository, created, key));
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetimeSource.Cancel();
        _session.Remove(this);
        DisposeAvatars();
        _running?.Done.TrySetResult();
        _again?.TrySetResult();
        _lifetimeSource.Dispose();
    }

    /// <summary>
    /// <paramref name="provider"/>'s account changed, on the UI thread (7.8; docs/phase-7b-design.md
    /// 9.7): with no account signed in, the rewrites count as unknown until the next
    /// <see cref="Update"/> that wants them. The candidate's provider: listable, the state and the
    /// bindings again and a refresh; else its list, failure and refs' pull requests go. A list or a
    /// failure another account read goes either way, so the state is loading until this account's
    /// answer (D348). Another provider: the state and the bindings again alone. GitHub not signed
    /// in: the avatars go (9.8).
    /// </summary>
    internal void OnAccountChanged(HostingProvider provider)
    {
        if (_disposed)
        {
            return;
        }

        if (!_session.AnySignedIn)
        {
            _rulesKnown = false;
        }

        AuthorAvatars? avatars = null;
        if (provider == HostingProvider.GitHub && !GitHubSignedIn)
        {
            StopAvatars();
            avatars = AuthorAvatars.None;
        }

        if (_candidate is { } candidate && candidate.Provider == provider)
        {
            ForgetInfoOfOthers(candidate);
            if (!HeldIs(candidate, _listAccount))
            {
                ForgetList();
            }

            if (CanList)
            {
                Apply(avatars);
                _ = RefreshPullRequestsAsync(CancellationToken.None);
                return;
            }

            ForgetList();
        }

        Apply(avatars);
    }

    private static bool SameState(RepositoryHostingState a, RepositoryHostingState b) =>
        ReferenceEquals(a, b)
        || (a.PullRequests == b.PullRequests
            && Equals(a.Repository, b.Repository)
            && Equals(a.Failure, b.Failure)
            && SameList(a.List, b.List)
            && SameDictionary(a.Remotes, b.Remotes)
            && SameDictionary(a.ByRef, b.ByRef));

    private static bool SameList(PullRequestList? a, PullRequestList? b) =>
        ReferenceEquals(a, b) || (a is not null && b is not null && a.MoreExist == b.MoreExist && a.PullRequests.SequenceEqual(b.PullRequests));

    private static bool SameDictionary<T>(IReadOnlyDictionary<string, T> a, IReadOnlyDictionary<string, T> b) =>
        a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var other) && Equals(pair.Value, other));

    /// <summary>
    /// The repository listed for <paramref name="candidate"/> with A3's answer for it (7.8): its
    /// parent when A3 says it is a fork with one, else A3's repository, which is the candidate under
    /// the name GitHub gives it now (renamed or transferred, the client having followed GitHub's
    /// 301: 6.3); the candidate itself before A3 has answered.
    /// <para>
    /// Not covered: a parent renamed or transferred behind an <c>upstream</c> remote. A3 is read for
    /// the candidate only, so that remote keeps the name in its URL and is not taken for the parent;
    /// only GitHub's ids, or an A3 for each remote, would cover it.
    /// </para>
    /// </summary>
    private static HostedRepository ListedOf(HostedRepository candidate, RepositoryInfo? info) => info switch
    {
        { IsFork: true, Parent: { } parent } => parent,
        { } read => read.Repository,
        null => candidate,
    };

    /// <summary>
    /// A3's answer for <paramref name="candidate"/>, once it has come; null before, and while the
    /// candidate's provider holds another account than the one it was read with (a sign-out keeps it,
    /// as 7a did, until another account signs in).
    /// </summary>
    private RepositoryInfo? InfoOf(HostedRepository candidate)
    {
        lock (_infoLock)
        {
            return _info is { } kept && kept.Candidate.Equals(candidate) && HeldOrNone(candidate, kept.Account) ? kept.Info : null;
        }
    }

    /// <summary>The candidate's provider holds <paramref name="account"/>, or no account at all.</summary>
    private bool HeldOrNone(HostedRepository candidate, AccountKey? account) =>
        _session.SlotOf(candidate.Provider) is not { } slot || slot.Key == account;

    /// <summary>The candidate's provider holds <paramref name="account"/>, which is not null.</summary>
    private bool HeldIs(HostedRepository candidate, AccountKey? account) =>
        account is not null && _session.SlotOf(candidate.Provider)?.Key == account;

    // On the UI thread: the list and the failure go, with the account they were read with.
    private void ForgetList()
    {
        _list = null;
        _failure = null;
        _listAccount = null;
    }

    // On the UI thread, at the candidate's provider's change of account: A3's answer and the read in
    // flight of another account are dropped (InfoOf would not use them either).
    private void ForgetInfoOfOthers(HostedRepository candidate)
    {
        lock (_infoLock)
        {
            if (_info is { } kept && !HeldOrNone(candidate, kept.Account))
            {
                _info = null;
            }

            if (_reading is { } reading && !HeldOrNone(candidate, reading.Account))
            {
                _reading = null;
            }
        }
    }

    /// <summary>
    /// The remotes as the state gives them (7.8): once A3 has answered for <paramref name="candidate"/>
    /// under another name, every remote that names the candidate names A3's repository instead, so
    /// that the refs' pull requests, a pull request's checkout and the create compare one name with
    /// GitHub's answers. <c>_remotes</c> itself stays as recognised from the URLs, so that
    /// <see cref="Update"/>'s candidate does not change with A3's answer.
    /// </summary>
    private IReadOnlyDictionary<string, HostedRepository> RemotesOf(HostedRepository candidate, RepositoryInfo? info)
    {
        if (info?.Repository is not { } current || current.Equals(candidate))
        {
            return _remotes;
        }

        var remotes = new Dictionary<string, HostedRepository>(StringComparer.Ordinal);
        foreach (var (name, repository) in _remotes)
        {
            remotes[name] = repository.Equals(candidate) ? current : repository;
        }

        return remotes;
    }

    // On the UI thread: the state and the bindings as the fields say, with the avatars given;
    // Changed posted when any of them differs, or WantsUrlRewrites: a sign-in that changes nothing
    // else (a remote GitHub's only through the user's insteadOf) still starts the tab's rules read;
    // or the listed provider's account, which the tab shows (D356: another organization's account
    // swapped for a third leaves the state as it was).
    private void Apply(AuthorAvatars? avatars = null)
    {
        var changed = false;
        if (_session.AnySignedIn != _wantsRewrites)
        {
            _wantsRewrites = !_wantsRewrites;
            if (!_wantsRewrites)
            {
                // Raised as unwanted: the tab forgets its rules, so they count as unknown here too.
                _rulesKnown = false;
            }

            changed = true;
        }

        var account = _candidate is { } listed && _session.SlotOf(listed.Provider)?.Status.Account is { } held
            ? (held.Key, held.Login)
            : ((AccountKey Key, string Login)?)null;
        if (!Nullable.Equals(account, _account))
        {
            _account = account;
            changed = true;
        }

        var state = StateOf();
        var bindings = _session.BindingsOf(_effective, _rulesKnown);

        if (!SameState(_state, state))
        {
            Volatile.Write(ref _state, state);
            changed = true;
        }

        if (!_bindings.SequenceEqual(bindings))
        {
            Volatile.Write(ref _bindings, bindings);
            changed = true;
        }

        if (avatars is not null && !SameDictionary(_avatars.FileByEmail, avatars.FileByEmail))
        {
            Volatile.Write(ref _avatars, avatars);
            changed = true;
        }

        if (changed)
        {
            // Each Changed carries the value raised with it: WantsUrlRewrites within its handlers.
            var raising = _wantsRewrites ? 1 : 0;
            _session.Post(() =>
            {
                if (!_disposed)
                {
                    _raising = raising;
                    try
                    {
                        Changed?.Invoke(this, EventArgs.Empty);
                    }
                    finally
                    {
                        _raising = -1;
                    }
                }
            });
        }
    }

    // The state (7.8; docs/phase-7b-design.md 9.7): no candidate, NoHostedRemote; an account of the
    // candidate's provider held, signed in or needing sign-in, that does not cover it, on Azure
    // DevOps OtherOrganization (D356, D366): the candidate, no list, failure or ByRef, nothing
    // asked; else 7a's by that provider's account.
    private RepositoryHostingState StateOf()
    {
        if (_candidate is not { } candidate || _refs is not { } refs)
        {
            return RepositoryHostingState.NoHostedRemote;
        }

        var slot = _session.SlotOf(candidate.Provider);
        if (slot is not null && _session.ProviderOf(candidate.Provider) is { } provider && !provider.Covers(candidate, slot.Key))
        {
            return new(
                candidate.Provider == HostingProvider.AzureDevOps ? PullRequestsState.OtherOrganization : PullRequestsState.SignedOut,
                _remotes,
                candidate,
                null,
                null,
                NoPullRequests);
        }

        var info = InfoOf(candidate);
        var listed = ListedOf(candidate, info);
        var remotes = RemotesOf(candidate, info);
        var held = HeldIs(candidate, _listAccount);
        return slot is not { IsSignedIn: true } ? new(PullRequestsState.SignedOut, remotes, listed, null, null, NoPullRequests)
            : _list is { } list && held ? new(PullRequestsState.Loaded, remotes, listed, list, null, HostingRules.ByRef(refs, remotes, list))
            : _failure is { } failure && held ? new(PullRequestsState.Failed, remotes, listed, null, failure, NoPullRequests)
            : new(PullRequestsState.Loading, remotes, listed, null, null, NoPullRequests);
    }

    // On the UI thread: a run of A3 and A4 for the candidate, under its own stop; a failure shown
    // before is cleared, a list held stays until the answer.
    private void StartRun(TaskCompletionSource done)
    {
        var candidate = _candidate!;
        var run = new Run(candidate, done, _lifetime);
        var stop = run.Stop.Token;
        _running = run;
        _failure = null;
        Apply();
        var asked = _session.SlotOf(candidate.Provider)?.Key;
        _ = Task.Run(async () =>
        {
            HostedRepository? listed = null;
            HostingResult<PullRequestList>? result = null;
            AccountKey? listedWith = null;
            try
            {
                var info = await ReadInfoAsync(candidate, stop).ConfigureAwait(false);
                if (info.Failure is { } failure)
                {
                    result = HostingResult<PullRequestList>.Fail(failure);
                }
                else
                {
                    listed = ListedOf(candidate, info.Value);
                    result = await _session.CallAsync(
                        candidate.Provider,
                        $"listing the pull requests of {listed.FullName}",
                        listed,
                        (provider, token, account, linked) =>
                        {
                            listedWith = account.Key;
                            return provider.ListPullRequestsAsync(token, listed, linked);
                        },
                        stop).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                // Closed, or another provider's repository listed: nothing is applied.
            }
            catch (Exception ex)
            {
                // Shown as the run's failure, so that the section does not stay loading. Neither the
                // line nor the failure holds the exception's text, which can quote what the server
                // or a proxy sent: the line names it by its type and codes alone (D382, H1's rule),
                // the failure has the provider's text for no answer.
                _session.Log.Warning($"{_name}: the pull requests could not be read ({Http.HostingHttpClient.Describe(ex)}).");
                result = HostingResult<PullRequestList>.Fail(new HostingFailure(HostingFailureKind.Network, Http.HostingHttpTexts.Network(candidate.Provider)) { Provider = candidate.Provider });
            }

            _session.Post(() => Finish(run, listed, result, asked, listedWith));
        });
    }

    // On the UI thread: the run's answer applied when it is still the tab's run (Update cancels
    // another provider's) and the candidate's, its provider's account can list it, and that account
    // is the one the run asked with and A4 was sent with (D348: another user's answer is dropped;
    // the refresh that user's sign-in asked for lists for it); then the run asked for meanwhile, if any.
    private void Finish(Run run, HostedRepository? listed, HostingResult<PullRequestList>? result, AccountKey? asked, AccountKey? listedWith)
    {
        var candidate = run.Candidate;
        var current = ReferenceEquals(_running, run);
        if (current && !_disposed && result is not null && Equals(candidate, _candidate) && CanList
            && HeldIs(candidate, asked) && (listedWith is null || listedWith == asked))
        {
            _list = result.Value;
            _failure = result.Failure;
            _listAccount = asked;
            Apply();
            if (result.Value is { } list && listed is not null)
            {
                // After the Changed above, so that the line follows the state's being applied (1.9,
                // D322); an address in the name, which A3 can give, as the core writes one (D368).
                var name = Http.HostingHttpClient.ScreenEmail(listed.FullName);
                _session.Post(() => _session.Log.Debug($"{_name}: listed {list.PullRequests.Count} open pull requests of {name}"));
            }
        }

        run.Stop.Dispose();
        if (!current)
        {
            // Cancelled for another provider's candidate, whose run is the tab's now.
            run.Done.TrySetResult();
            return;
        }

        _running = null;
        run.Done.TrySetResult();
        if (_again is { } again)
        {
            _again = null;
            if (!_disposed && CanList)
            {
                StartRun(again);
            }
            else
            {
                again.TrySetResult();
            }
        }
    }

    // A3 of the candidate as the candidate's provider's account reads it now: kept, the read in
    // flight for that account, or a new read (docs/phase-7b-design.md 9.7: per account).
    private Task<HostingResult<RepositoryInfo>> ReadInfoAsync(HostedRepository candidate, CancellationToken ct)
    {
        Task<HostingResult<RepositoryInfo>> read;
        lock (_infoLock)
        {
            if (_info is { } kept && kept.Candidate.Equals(candidate) && HeldOrNone(candidate, kept.Account))
            {
                return Task.FromResult(HostingResult<RepositoryInfo>.Ok(kept.Info));
            }

            var account = _session.SlotOf(candidate.Provider)?.Key;
            if (_reading is { } reading && reading.Candidate.Equals(candidate) && reading.Account == account)
            {
                read = reading.Read;
            }
            else
            {
                read = Task.Run(() => ReadInfoCoreAsync(candidate, account));
                _reading = (candidate, account, read);
            }
        }

        return read.WaitAsync(ct);
    }

    private async Task<HostingResult<RepositoryInfo>> ReadInfoCoreAsync(HostedRepository candidate, AccountKey? asked)
    {
        HostingResult<RepositoryInfo>? result = null;
        AccountKey? readWith = null;
        try
        {
            result = await _session.CallAsync(
                candidate.Provider,
                $"reading {candidate.FullName}",
                candidate,
                async (provider, token, account, linked) =>
                {
                    readWith = account.Key;
                    var read = await provider.ReadRepositoryAsync(token, candidate, linked).ConfigureAwait(false);

                    // D382: names that hold the request's token (a server that echoes it) would reach
                    // the log lines, A4's request and the state, so the answer is malformed.
                    return read.Value is { } answered && _session.EchoedToken(candidate.Provider, token, answered.Repository.FullName, answered.Parent?.FullName) is { } echoed
                        ? HostingResult<RepositoryInfo>.Fail(echoed)
                        : read;
                },
                _lifetime).ConfigureAwait(false);
            return result;
        }
        finally
        {
            lock (_infoLock)
            {
                // Kept with the account the request was sent with, which only that account uses.
                if (result?.Value is { } info && readWith is { } key)
                {
                    _info = (candidate, key, info);
                }

                if (_reading is { } reading && reading.Candidate.Equals(candidate) && reading.Account == asked)
                {
                    _reading = null;
                }
            }
        }
    }

    private bool CanListFor(HostedRepository candidate) =>
        _session.SlotOf(candidate.Provider) is { IsSignedIn: true } slot
        && _session.ProviderOf(candidate.Provider) is { } provider
        && provider.Covers(candidate, slot.Key);

    // On the UI thread: a created pull request on top of the held list of its base (D293), when the
    // account that created it read that list and is still held.
    private void AddCreated(HostedRepository baseRepository, PullRequest created, AccountKey createdWith)
    {
        if (_disposed || _list is not { } list || !Equals(State.Repository, baseRepository) || list.PullRequests.Any(pull => pull.Number == created.Number)
            || createdWith != _listAccount || _candidate is not { } candidate || !HeldIs(candidate, createdWith))
        {
            return;
        }

        _list = new PullRequestList([created, .. list.PullRequests], list.MoreExist);
        Apply();
    }

    /// <summary>
    /// A run of A3 and A4 (7.8): the candidate it lists, the waits its end completes, and its stop,
    /// linked to the tab's lifetime, which <see cref="Update"/> cancels when another provider's
    /// repository becomes the candidate (docs/phase-7b-design.md 9.7); disposed at its Finish.
    /// </summary>
    private sealed class Run(HostedRepository candidate, TaskCompletionSource done, CancellationToken lifetime)
    {
        public HostedRepository Candidate { get; } = candidate;

        public TaskCompletionSource Done { get; } = done;

        public CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
    }
}
