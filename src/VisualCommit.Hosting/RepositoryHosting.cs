using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.Hosting;

/// <summary>
/// The hosting of one repository tab (docs/phase-7a-design.md 7.8; D251, D289, D290, D293): its
/// remotes on GitHub, the repository whose pull requests are listed and the list, the bindings of
/// git's credentials to the account, and the refs' pull requests. Its state changes on the UI
/// thread (<see cref="Update"/>, the account's changes, the refreshes' answers through the post
/// delegate); network work runs off it under the hosting's lifetime, which <see cref="Dispose"/>
/// ends. <see cref="State"/>, <see cref="Bindings"/> and <see cref="Avatars"/> are swapped whole and
/// may be read from any thread; <see cref="Changed"/> comes through the post delegate, only when one
/// of them or <see cref="WantsUrlRewrites"/> differs.
/// </summary>
public sealed partial class RepositoryHosting : IRepositoryHosting
{
    private static readonly HostingFailure NoRemote = new(HostingFailureKind.NotFound, "This repository has no GitHub remote.");

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
    private TaskCompletionSource? _running;
    private TaskCompletionSource? _again;

    // Under _infoLock: A3's answer for a candidate, and the read in flight.
    private (HostedRepository Candidate, RepositoryInfo Info)? _info;
    private (HostedRepository Candidate, Task<HostingResult<RepositoryInfo>> Read)? _reading;

    private RepositoryHostingState _state = RepositoryHostingState.NotGitHub;
    private IReadOnlyList<CredentialBinding> _bindings = [];
    private AuthorAvatars _avatars = AuthorAvatars.None;

    // On the UI thread: WantsUrlRewrites as last raised.
    private bool _wantsRewrites;
    private volatile bool _disposed;

    internal RepositoryHosting(HostingSession session, string workingDirectory)
    {
        _session = session;
        _name = Path.GetFileName(Path.TrimEndingDirectorySeparator(workingDirectory));
        _lifetimeSource = CancellationTokenSource.CreateLinkedTokenSource(session.Lifetime);
        _lifetime = _lifetimeSource.Token;
        _wantsRewrites = WantsUrlRewrites;
    }

    public RepositoryHostingState State => Volatile.Read(ref _state);

    public IReadOnlyList<CredentialBinding> Bindings => Volatile.Read(ref _bindings);

    public AuthorAvatars Avatars => Volatile.Read(ref _avatars);

    /// <summary>The account is signed in (with or without "Use for git over HTTPS"): the tab reads the URL rewrites with its refs.</summary>
    public bool WantsUrlRewrites => IsSignedIn;

    public event EventHandler? Changed;

    private bool IsSignedIn => _session.Slot is { IsSignedIn: true };

    /// <summary>
    /// After each refs read, on the UI thread (7.8): the remotes recognised, the candidate, the
    /// bindings and the state again; a new candidate drops the list and, while signed in, starts a
    /// refresh (D289). The rewrites count as known only when they were wanted for this read (D290).
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
            _list = null;
            _failure = null;
        }

        Apply();
        if (changed && candidate is not null && IsSignedIn)
        {
            _ = RefreshPullRequestsAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Reads the listed repository's open pull requests (7.8): A3 of the candidate once, then A4 of
    /// it or of its parent. One run at a time; a call during a run asks for one more, whose applied
    /// answer completes its task. <paramref name="ct"/> ends only the caller's wait. Signed out or
    /// without a candidate, nothing is asked (D257).
    /// </summary>
    public Task RefreshPullRequestsAsync(CancellationToken ct)
    {
        if (_disposed || _candidate is null || !IsSignedIn)
        {
            return Task.CompletedTask;
        }

        if (_running is not null)
        {
            _again ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _again.Task.WaitAsync(ct);
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StartRun(done);
        return done.Task.WaitAsync(ct);
    }

    /// <summary>A3 of the candidate, kept per candidate (a failure is not kept); on the UI thread.</summary>
    public Task<HostingResult<RepositoryInfo>> ReadRepositoryAsync(CancellationToken ct) =>
        _candidate is { } candidate ? ReadInfoAsync(candidate, ct) : Task.FromResult(HostingResult<RepositoryInfo>.Fail(NoRemote));

    /// <summary>A5 on <paramref name="baseRepository"/> (D293): once created, on top of the held list when that is the listed repository.</summary>
    public async Task<HostingResult<PullRequest>> CreatePullRequestAsync(HostedRepository baseRepository, NewPullRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(baseRepository);
        ArgumentNullException.ThrowIfNull(request);
        var result = await _session.CallAsync(
            $"creating a pull request on {baseRepository.FullName}",
            (provider, token, linked) => provider.CreatePullRequestAsync(token, baseRepository, request, linked),
            ct).ConfigureAwait(false);
        if (result.Value is { } created)
        {
            _session.Post(() => AddCreated(baseRepository, created));
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
        _running?.TrySetResult();
        _again?.TrySetResult();
        _lifetimeSource.Dispose();
    }

    /// <summary>
    /// The account changed, on the UI thread (7.8): signed in, the bindings and the state again and
    /// a refresh; else the list, its failure, the refs' pull requests, the avatars and the bindings
    /// go, and the rewrites count as unknown until the next <see cref="Update"/> that wants them.
    /// </summary>
    internal void OnAccountChanged()
    {
        if (_disposed)
        {
            return;
        }

        if (IsSignedIn)
        {
            Apply();
            if (_candidate is not null)
            {
                _ = RefreshPullRequestsAsync(CancellationToken.None);
            }

            return;
        }

        _list = null;
        _failure = null;
        _rulesKnown = false;
        StopAvatars();
        Apply(AuthorAvatars.None);
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

    /// <summary>A3's answer for <paramref name="candidate"/>, once it has come; null before.</summary>
    private RepositoryInfo? InfoOf(HostedRepository candidate)
    {
        lock (_infoLock)
        {
            return _info is { } kept && kept.Candidate.Equals(candidate) ? kept.Info : null;
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
    // else (a remote GitHub's only through the user's insteadOf) still starts the tab's rules read.
    private void Apply(AuthorAvatars? avatars = null)
    {
        var slot = _session.Slot;
        var state = StateOf(slot);
        var bindings = HostingRules.Bindings(slot?.Status, slot?.IsSignedIn == true, _rulesKnown, _effective, _session.Endpoints.WebBase);
        var changed = false;
        if (WantsUrlRewrites != _wantsRewrites)
        {
            _wantsRewrites = !_wantsRewrites;
            changed = true;
        }

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
            _session.Post(() =>
            {
                if (!_disposed)
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            });
        }
    }

    private RepositoryHostingState StateOf(AccountSlot? slot)
    {
        if (_candidate is not { } candidate || _refs is not { } refs)
        {
            return RepositoryHostingState.NotGitHub;
        }

        var info = InfoOf(candidate);
        var listed = ListedOf(candidate, info);
        var remotes = RemotesOf(candidate, info);
        return slot is not { IsSignedIn: true } ? new(PullRequestsState.SignedOut, remotes, listed, null, null, NoPullRequests)
            : _list is { } list ? new(PullRequestsState.Loaded, remotes, listed, list, null, HostingRules.ByRef(refs, remotes, list))
            : _failure is { } failure ? new(PullRequestsState.Failed, remotes, listed, null, failure, NoPullRequests)
            : new(PullRequestsState.Loading, remotes, listed, null, null, NoPullRequests);
    }

    // On the UI thread: a run of A3 and A4 for the candidate; a failure shown before is cleared,
    // a list held stays until the answer.
    private void StartRun(TaskCompletionSource done)
    {
        _running = done;
        _failure = null;
        Apply();
        var candidate = _candidate!;
        _ = Task.Run(async () =>
        {
            HostedRepository? listed = null;
            HostingResult<PullRequestList>? result = null;
            try
            {
                var info = await ReadInfoAsync(candidate, _lifetime).ConfigureAwait(false);
                if (info.Failure is { } failure)
                {
                    result = HostingResult<PullRequestList>.Fail(failure);
                }
                else
                {
                    listed = ListedOf(candidate, info.Value);
                    result = await _session.CallAsync(
                        $"listing the pull requests of {listed.FullName}",
                        (provider, token, linked) => provider.ListPullRequestsAsync(token, listed, linked),
                        _lifetime).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                // Closed: nothing is applied.
            }
            catch (Exception ex)
            {
                // Shown as the run's failure, so that the section does not stay loading.
                _session.Log.Warning($"{_name}: the pull requests could not be read: {ex.Message}");
                result = HostingResult<PullRequestList>.Fail(new HostingFailure(HostingFailureKind.Network, ex.Message));
            }

            _session.Post(() => Finish(candidate, listed, result, done));
        });
    }

    // On the UI thread: the run's answer applied when it is still the candidate's and the account
    // is signed in; then the run asked for meanwhile, if any.
    private void Finish(HostedRepository candidate, HostedRepository? listed, HostingResult<PullRequestList>? result, TaskCompletionSource done)
    {
        if (!_disposed && result is not null && Equals(candidate, _candidate) && IsSignedIn)
        {
            _list = result.Value;
            _failure = result.Failure;
            Apply();
            if (result.Value is { } list && listed is not null)
            {
                // After the Changed above, so that the line follows the state's being applied (1.9, D322).
                _session.Post(() => _session.Log.Debug($"{_name}: listed {list.PullRequests.Count} open pull requests of {listed.FullName}"));
            }
        }

        _running = null;
        done.TrySetResult();
        if (_again is { } again)
        {
            _again = null;
            if (!_disposed && _candidate is not null && IsSignedIn)
            {
                StartRun(again);
            }
            else
            {
                again.TrySetResult();
            }
        }
    }

    private Task<HostingResult<RepositoryInfo>> ReadInfoAsync(HostedRepository candidate, CancellationToken ct)
    {
        Task<HostingResult<RepositoryInfo>> read;
        lock (_infoLock)
        {
            if (_info is { } kept && kept.Candidate.Equals(candidate))
            {
                return Task.FromResult(HostingResult<RepositoryInfo>.Ok(kept.Info));
            }

            if (_reading is { } reading && reading.Candidate.Equals(candidate))
            {
                read = reading.Read;
            }
            else
            {
                read = Task.Run(() => ReadInfoCoreAsync(candidate));
                _reading = (candidate, read);
            }
        }

        return read.WaitAsync(ct);
    }

    private async Task<HostingResult<RepositoryInfo>> ReadInfoCoreAsync(HostedRepository candidate)
    {
        HostingResult<RepositoryInfo>? result = null;
        try
        {
            result = await _session.CallAsync(
                $"reading {candidate.FullName}",
                (provider, token, linked) => provider.ReadRepositoryAsync(token, candidate, linked),
                _lifetime).ConfigureAwait(false);
            return result;
        }
        finally
        {
            lock (_infoLock)
            {
                if (result?.Value is { } info)
                {
                    _info = (candidate, info);
                }

                if (_reading is { } reading && reading.Candidate.Equals(candidate))
                {
                    _reading = null;
                }
            }
        }
    }

    // On the UI thread: a created pull request on top of the held list of its base (D293).
    private void AddCreated(HostedRepository baseRepository, PullRequest created)
    {
        if (_disposed || _list is not { } list || !Equals(State.Repository, baseRepository) || list.PullRequests.Any(pull => pull.Number == created.Number))
        {
            return;
        }

        _list = new PullRequestList([created, .. list.PullRequests], list.MoreExist);
        Apply();
    }
}
