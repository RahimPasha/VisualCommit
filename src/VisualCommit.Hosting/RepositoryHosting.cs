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
    // (D348) uses neither, since what one account may read another may not. _readings holds every
    // read in flight, _reading's and those replaced or forgotten that still run, until each ends, in
    // the order they started; _listedProvider, the provider of the last candidate listed (null before
    // any), whose reads alone may start (DropReadingOfOthers).
    private (HostedRepository Candidate, AccountKey Account, RepositoryInfo Info)? _info;
    private Reading? _reading;
    private readonly List<Reading> _readings = [];
    private HostingProvider? _listedProvider;

    private RepositoryHostingState _state = RepositoryHostingState.NoHostedRemote;
    private IReadOnlyList<CredentialBinding> _bindings = [];
    private AuthorAvatars _avatars = AuthorAvatars.None;

    // On the UI thread: WantsUrlRewrites as last raised, and the listed provider's account (its key,
    // its login as given and whether it is signed in) as last raised. While a Changed is delivered,
    // _raising holds the value that Changed carries (1 wanted, 0 not, -1 none delivered), which
    // WantsUrlRewrites gives then.
    private bool _wantsRewrites;
    private volatile int _raising = -1;
    private (AccountKey Key, string Login, bool SignedIn)? _account;
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
    /// than the run in flight cancels that run, whose answer then applies nothing and whose wait ends
    /// at once, so that its list waits on no other provider's answer, and drops every A3 read in
    /// flight of another provider (the tab's, and any replaced or forgotten one still running), each
    /// then keeping nothing and given up unless a caller of <see cref="ReadRepositoryAsync"/> waits on
    /// it, before the run's and the avatar run's stops free that provider's turn to a read waiting
    /// behind them (docs/phase-7b-design.md 9.7); within one provider the run in flight ends first,
    /// as in 7a. The rewrites count as known only when they were wanted for this read (D290).
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
        Run? cancelled = null;
        if (changed)
        {
            _candidate = candidate;
            ForgetList();
            if (_running is { } running && candidate is not null && running.Candidate.Provider != candidate.Provider)
            {
                // Its answer, or its A4's failure's warning, is of a repository listed no more, and
                // this candidate's run would wait behind it up to that provider's time-out. Marked
                // first, so that whatever ends it below, its A3 given up or its stop, applies and
                // warns nothing.
                _running = null;
                running.Cancelled = true;
                cancelled = running;
            }
        }

        // No GitHub repository listed any more (D252, 11.3, D367): none, or another provider's. The
        // avatars go, and a run in flight brings none. A change to another GitHub repository keeps
        // them: the tab sends the same set again only after NoHostedRemote or a sign-out, so
        // dropping them there would leave none.
        var avatarsGo = changed && candidate?.Provider != HostingProvider.GitHub;
        if (avatarsGo)
        {
            DropAvatarRun();
        }

        if (changed && candidate is not null)
        {
            // Before the run's stop and the avatar run's: cancelling a request in flight hands its
            // provider's turn to the next waiting, which may be one of these reads. Before this
            // candidate's run, which would otherwise take a read's place.
            DropReadingOfOthers(candidate.Provider);
        }

        if (cancelled is not null)
        {
            // Its own wait ends now: its Finish comes through the session's post, which drops it once
            // the session is disposed, and the tab's Dispose ends only the run that is the tab's then.
            // The waits asked of it meanwhile end with this candidate's run (RefreshPullRequestsAsync).
            cancelled.Stop.Cancel();
            cancelled.Done.TrySetResult();
            if (!CanList && _again is { } waiting)
            {
                _again = null;
                waiting.TrySetResult();
            }
        }

        if (avatarsGo)
        {
            CancelAvatarRun();
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

    /// <summary>
    /// A3 of the candidate, kept per candidate (a failure is not kept; an answer whose names hold the
    /// token is malformed, D382); on the UI thread. Another provider's candidate meanwhile never
    /// cancels the read this call waits on (<see cref="Update"/>): it ends with its answer, kept no more.
    /// </summary>
    public Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(CancellationToken ct) =>
        _candidate is { } candidate ? ReadInfoAsync(candidate, ct, asked: true) : Task.FromResult(HostingResult<RepositoryInfo>.Fail(NoRemote));

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
            async (provider, token, account, linked) =>
            {
                createdWith = account.Key;
                var answer = await provider.CreatePullRequestAsync(token, baseRepository, request, linked).ConfigureAwait(false);

                // D382 as for A4: a created request that names the token is not added or shown.
                return answer.Value is { } created && EchoedIn(baseRepository.Provider, token, [created]) is { } echoed
                    ? HostingResult<PullRequest>.Fail(echoed)
                    : answer;
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
    /// in: the avatars go (9.8). A run in flight of <paramref name="provider"/> whose account that
    /// provider no longer holds signed in is stopped first, as <see cref="Update"/> stops another
    /// provider's, also when its candidate went meanwhile: its pages and fork lookups would go on with
    /// a token signed out or replaced, for an answer Finish drops (D257, D348;
    /// docs/phase-7b-design.md 12's As built). A sign-out stops the run at once. A sign-in that
    /// replaces the account stops it only after the request in flight, because the new token's check
    /// waits behind that request (7a's one request at a time per provider), so only the later pages
    /// and lookups are spared.
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

        // Before the candidate's check: a run whose candidate went meanwhile (no hosted remote left,
        // which Update leaves running) still sends its pages with the account it asked with.
        var stopped = StopRunOfAnotherAccount(provider);
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

        if (stopped && !CanList && _again is { } waiting)
        {
            // The waits asked of the stopped run end with it: nothing can be listed now.
            _again = null;
            waiting.TrySetResult();
        }

        Apply(avatars);
    }

    // On the UI thread, at the provider's change of account: the run in flight of the
    // candidate of that provider, unless it asked with the account the provider holds signed in now,
    // is stopped and marked as Update marks another provider's, so that its end applies and warns
    // nothing; its wait ends now. True when one was stopped.
    private bool StopRunOfAnotherAccount(HostingProvider provider)
    {
        if (_running is not { } running || running.Candidate.Provider != provider
            || (_session.SlotOf(provider) is { IsSignedIn: true } slot && slot.Key == running.Asked))
        {
            return false;
        }

        _running = null;
        running.Cancelled = true;
        running.Stop.Cancel();
        running.Done.TrySetResult();
        return true;
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
    // flight of another account are forgotten (InfoOf would not use them either); that read keeps no
    // answer, so that it never takes the place of this account's, and stays in _readings until it
    // ends, so that another provider's candidate gives it up (DropReadingOfOthers).
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
                reading.Superseded = true;
            }
        }
    }

    // On the UI thread, at a new candidate (Update): every A3 read in flight of another provider,
    // the tab's and any replaced or forgotten one that still runs (one waiting behind another in its
    // provider's one request at a time, say), before Update stops the run and the avatar run, which
    // it has marked so that the reads' ends warn nothing. Their answers, or their failures'
    // warnings, would be of repositories listed no more. Each is dropped and keeps no answer, so that
    // it never takes the place of this candidate's (7.8: a fork's listed repository is its parent),
    // and is given up at once, or when the last caller of ReadRepositoryAsync waiting on it is done
    // (WaitAskedAsync); from here no read of another provider starts (ReadInfoAsync). Newest first:
    // the oldest is the one whose request is in flight, and giving it up hands its provider's turn
    // to the next waiting, which by then is given up too, so that it sends nothing.
    private void DropReadingOfOthers(HostingProvider provider)
    {
        GiveUpObserver?.Invoke();
        List<Reading>? givenUp = null;
        lock (_infoLock)
        {
            _listedProvider = provider;
            if (_reading is { } reading && reading.Candidate.Provider != provider)
            {
                _reading = null;
            }

            foreach (var other in _readings)
            {
                if (other.Candidate.Provider != provider && !other.Dropped)
                {
                    other.Superseded = true;
                    other.Dropped = true;
                    if (other.Callers == 0)
                    {
                        (givenUp ??= []).Add(other);
                    }
                }
            }
        }

        for (var i = (givenUp?.Count ?? 0) - 1; i >= 0; i--)
        {
            givenUp![i].GiveUp();
            GiveUpObserver?.Invoke();
        }
    }

    /// <summary>
    /// A seam for the tests, null in the app: called on the UI thread as <c>DropReadingOfOthers</c>
    /// starts and after each A3 read it gives up, where a test stands for a UI thread that is late.
    /// </summary>
    internal Action? GiveUpObserver { get; set; }

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
    // swapped for a third leaves the state as it was), and whether it is signed in (D366: it coming to
    // need sign-in leaves OtherOrganization as it was, yet the row says it; the review before the gate).
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

        var account = _candidate is { } listed && _session.SlotOf(listed.Provider) is { Status.Account: { } held } slot
            ? (held.Key, held.Login, slot.IsSignedIn)
            : ((AccountKey Key, string Login, bool SignedIn)?)null;
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
        var asked = _session.SlotOf(candidate.Provider)?.Key;
        var run = new Run(candidate, done, _lifetime) { Asked = asked };
        var stop = run.Stop.Token;
        _running = run;
        _failure = null;
        Apply();
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
                        async (provider, token, account, linked) =>
                        {
                            listedWith = account.Key;
                            var answer = await provider.ListPullRequestsAsync(token, listed, linked).ConfigureAwait(false);

                            // D382 as for A3: a request whose texts hold the token (a server that
                            // echoes it) would reach the rows, a checkout's branch and git's
                            // arguments, the refs and the log lines, so the answer is malformed.
                            return answer.Value is { } list && EchoedIn(candidate.Provider, token, list.PullRequests) is { } echoed
                                ? HostingResult<PullRequestList>.Fail(echoed)
                                : answer;
                        },
                        stop).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested || run.Cancelled)
            {
                // Closed, or another provider's repository listed (its A3 given up before its stop,
                // or refused): nothing is applied.
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
            // Cancelled for another provider's candidate, whose run is the tab's now: Update ended
            // its wait already, so this does nothing.
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
    // flight for that account, or a new read (docs/phase-7b-design.md 9.7: per account), which a read
    // of another candidate or account in flight gives its place to (that one keeps no answer, and
    // stays in _readings until it ends, for DropReadingOfOthers). None once ct is cancelled, or for a
    // candidate of another provider than the one listed: a run or an avatar run that Update stopped,
    // or is about to stop, starts no read, since DropReadingOfOthers and this check both hold the
    // lock, so a read one of them started first is dropped there. A caller of ReadRepositoryAsync
    // (asked), whose candidate is the listed one, is counted on the read while it waits.
    private Task<HostingResult<RepositoryInfo>> ReadInfoAsync(HostedRepository candidate, CancellationToken ct, bool asked = false)
    {
        Reading reading;
        lock (_infoLock)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled<HostingResult<RepositoryInfo>>(ct);
            }

            if (_listedProvider is { } listed && candidate.Provider != listed)
            {
                return Task.FromCanceled<HostingResult<RepositoryInfo>>(new CancellationToken(canceled: true));
            }

            if (_info is { } kept && kept.Candidate.Equals(candidate) && HeldOrNone(candidate, kept.Account))
            {
                return Task.FromResult(HostingResult<RepositoryInfo>.Ok(kept.Info));
            }

            var account = _session.SlotOf(candidate.Provider)?.Key;
            if (_reading is { } current && current.Candidate.Equals(candidate) && current.Account == account)
            {
                reading = current;
            }
            else
            {
                if (_reading is { } replaced)
                {
                    replaced.Superseded = true;
                }

                // Listed before it starts: its end, which takes the lock, removes it.
                var started = new Reading(candidate, account);
                _readings.Add(started);
                started.Read = Task.Run(() => ReadInfoCoreAsync(started));
                _reading = reading = started;
            }

            if (asked)
            {
                reading.Callers++;
            }
        }

        return asked ? WaitAskedAsync(reading, ct) : reading.Read.WaitAsync(ct);
    }

    // A caller of ReadRepositoryAsync's wait, counted on the read meanwhile: the last to be done with
    // a read that DropReadingOfOthers dropped gives it up.
    private async Task<HostingResult<RepositoryInfo>> WaitAskedAsync(Reading reading, CancellationToken ct)
    {
        try
        {
            return await reading.Read.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            bool giveUp;
            lock (_infoLock)
            {
                giveUp = --reading.Callers == 0 && reading.Dropped;
            }

            if (giveUp)
            {
                reading.GiveUp();
            }
        }
    }

    private async Task<HostingResult<RepositoryInfo>> ReadInfoCoreAsync(Reading reading)
    {
        var candidate = reading.Candidate;
        HostingResult<RepositoryInfo>? result = null;
        AccountKey? readWith = null;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(reading.Stop.Token, _lifetime);
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
                    // the log lines, A4's request and the state, so the answer is malformed; so would
                    // the default branches, which a new request's form shows, logs and posts (13.3).
                    return read.Value is { } answered
                        && _session.EchoedToken(candidate.Provider, token, answered.Repository.FullName, answered.Parent?.FullName, answered.DefaultBranch, answered.ParentDefaultBranch) is { } echoed
                        ? HostingResult<RepositoryInfo>.Fail(echoed)
                        : read;
                },
                stop.Token).ConfigureAwait(false);
            return result;
        }
        finally
        {
            lock (_infoLock)
            {
                // Kept with the account the request was sent with, which only that account uses,
                // unless the read was superseded: its answer would take the current one's place.
                if (!reading.Superseded && result?.Value is { } info && readWith is { } key)
                {
                    _info = (candidate, key, info);
                }

                if (ReferenceEquals(_reading, reading))
                {
                    _reading = null;
                }

                _readings.Remove(reading);
            }
        }
    }

    // D382 for A4's and A5's requests: every text of theirs the tab keeps, shows, logs or hands to
    // git (the title, the author, both branches and the head's SHA, the head and base repositories'
    // names, the page's address as given and percent-decoded).
    private HostingFailure? EchoedIn(HostingProvider provider, string token, IEnumerable<PullRequest> pullRequests) =>
        _session.EchoedToken(
            provider,
            token,
            [
                .. pullRequests.SelectMany(pull => new[]
                {
                    pull.Title, pull.Author, pull.HeadRef, pull.HeadSha, pull.BaseRef,
                    pull.HeadRepository?.FullName, pull.HeadRepository?.Host, pull.BaseRepository.FullName,
                    pull.HtmlUrl.OriginalString, Uri.UnescapeDataString(pull.HtmlUrl.AbsoluteUri),
                }),
            ]);

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
    /// repository becomes the candidate (docs/phase-7b-design.md 9.7), completing its waits there and
    /// then; disposed at its Finish. <see cref="Cancelled"/> is set there first, before the A3 reads
    /// it may wait on are given up, so that a read given up or refused ends it as its stop would.
    /// </summary>
    private sealed class Run(HostedRepository candidate, TaskCompletionSource done, CancellationToken lifetime)
    {
        private volatile bool _cancelled;

        public HostedRepository Candidate { get; } = candidate;

        /// <summary>The account of the candidate's provider the run asks with (OnAccountChanged).</summary>
        public AccountKey? Asked { get; init; }

        public TaskCompletionSource Done { get; } = done;

        public CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(lifetime);

        /// <summary>Set on the UI thread, read where the run ends.</summary>
        public bool Cancelled
        {
            get => _cancelled;
            set => _cancelled = value;
        }
    }

    /// <summary>
    /// A3's read in flight of a candidate for an account (7.8; docs/phase-7b-design.md 9.7): its
    /// request under its own stop beside the tab's lifetime (linked in the read alone, so the stop
    /// needs no disposal and may be cancelled from any thread at any time); the rest under
    /// <c>_infoLock</c>, in whose <c>_readings</c> it is listed from its start to its end.
    /// <see cref="Superseded"/>: replaced, of another account, or dropped, it keeps no answer.
    /// <see cref="Dropped"/>: of another provider than a new candidate, replaced or not, given up
    /// once no caller of <see cref="ReadRepositoryAsync"/> waits on it (<see cref="Callers"/>), and
    /// before any stop that would hand its provider's turn to it.
    /// </summary>
    private sealed class Reading(HostedRepository candidate, AccountKey? account)
    {
        public HostedRepository Candidate { get; } = candidate;

        public AccountKey? Account { get; } = account;

        public CancellationTokenSource Stop { get; } = new();

        /// <summary>The read itself, set as it starts (<see cref="ReadInfoAsync"/>).</summary>
        public Task<HostingResult<RepositoryInfo>> Read { get; set; } = null!;

        public int Callers { get; set; }

        public bool Superseded { get; set; }

        public bool Dropped { get; set; }

        /// <summary>
        /// Its request is given up: the HTTP core throws, warning nothing, and sends nothing when it
        /// had not had its turn (docs/phase-7b-design.md 5); a read that has ended has none left.
        /// </summary>
        public void GiveUp() => Stop.Cancel();
    }
}
