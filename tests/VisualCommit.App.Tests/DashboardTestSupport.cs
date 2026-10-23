using System.Collections.Concurrent;
using Avalonia.Threading;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the dashboard's and summary mode's tests share (docs/phase-6-design.md 2.12, 3.9): a
/// counting fake repository with gates on its reads, a counting watcher, a provider of fakes by
/// folder, the session's services around them, and a wait that lets the UI thread run.
/// </summary>
internal static class DashboardTestSupport
{
    public static readonly string Head = new('a', 40);

    public static readonly string Upstream = new('b', 40);

    /// <summary>Waits, letting the UI thread run, until <paramref name="condition"/> holds; fails after 30 s.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Lets the UI thread run what was posted, a few rounds, so that a step that started nothing can be told from one that did.</summary>
    public static async Task SettleAsync()
    {
        for (var round = 0; round < 5; round++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The refs of a branch <c>main</c> on <see cref="Head"/>, with <paramref name="ahead"/> and <paramref name="behind"/> against <c>origin/main</c>.</summary>
    public static RepoRefs MainRefs(int ahead = 0, int behind = 0, bool remote = true) =>
        new(
            new HeadState("main", Head),
            [
                new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Head, Upstream: remote ? "origin/main" : null, Ahead: ahead, Behind: behind, IsHead: true, UpstreamRef: remote ? "refs/remotes/origin/main" : null, UpstreamRemote: remote ? "origin" : null, UpstreamRemoteRef: remote ? "refs/heads/main" : null),
                .. remote ? [new GitRef("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, Upstream, RemoteName: "origin")] : Array.Empty<GitRef>(),
            ],
            [],
            remote ? ["origin"] : []);

    /// <summary>The session's services around <paramref name="provider"/>, a store holding <paramref name="workspaces"/>, the read clock <paramref name="readClock"/> and the folder dialog <paramref name="folders"/> (none chosen when null).</summary>
    public static TabServices Services(IRepositoryProvider provider, IWorkspaceStore? store = null, TimeProvider? readClock = null, RecordingLog? log = null, ISessionStore? session = null, IFolderPicker? folders = null)
    {
        var settings = new MainWindowViewModelTests.FakeSettings();
        return new TabServices(
            provider,
            folders ?? new NoFolders(),
            settings,
            session ?? new MemorySession(),
            new DateDisplay(TimeZoneInfo.Utc),
            log,
            workspaces: store ?? new InMemoryWorkspaceStore(),
            readClock: readClock);
    }

    /// <summary>A store with one workspace "Web" (id <c>web</c>) of these folders.</summary>
    public static InMemoryWorkspaceStore Store(params string[] folders) =>
        new(new WorkspaceList { Workspaces = [new Workspace("web", "Web", [.. folders.Select(folder => new WorkspaceRepository(folder))])] });

    /// <summary>The window around <paramref name="services"/>, with git found at once.</summary>
    public static MainWindowViewModel Window(TabServices services, ISessionStore? session = null) =>
        new(
            services.Settings,
            new MainWindowViewModelTests.FakeThemes(),
            _ => Task.FromResult(new GitDetection(GitAvailability.Available, "git", new GitVersion(2, 47, 1), null)),
            services.Log,
            services,
            session ?? services.Session);

    internal sealed class MemorySession(SessionState? state = null) : ISessionStore
    {
        public SessionState Current { get; private set; } = state ?? new SessionState();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }

    private sealed class NoFolders : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}

/// <summary>
/// Opens fakes by folder (<see cref="WorkspacePaths.Same"/>): a folder it holds no fake for is not
/// a repository. Each fake's folder and git folder are made on disk under a folder of the
/// provider's own (<see cref="Root"/>, deleted on dispose), so that a row's folder checks find them.
/// Counts its opens; <see cref="Gate"/>, when set, holds every open until it opens.
/// </summary>
internal sealed class FakeProvider : IRepositoryProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, CountingRepository> _repositories = new(StringComparer.OrdinalIgnoreCase);
    private readonly TempDirectory _root = new("dashboard");

    private int _opens;

    public int Opens => _opens;

    /// <summary>The folder the fakes' folders are made in.</summary>
    public string Root => _root.Path;

    /// <summary>Holds every open until it completes.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>A meter the opens share with the fakes' refs reads: git calls at once, held by its gate too.</summary>
    public ReadMeter? Meter { get; set; }

    /// <summary>A fake repository at <c>{Root}/{name}</c>, its folder and git folder made on disk.</summary>
    public CountingRepository Add(string name, RepoRefs? refs = null, WorkingTreeStatus? status = null)
    {
        var repository = new CountingRepository(PathOf(name), refs, status);
        Directory.CreateDirectory(repository.GitDirectory);
        _repositories[repository.WorkingDirectory] = repository;
        return repository;
    }

    /// <summary>The folder of the fake <paramref name="name"/>, made or not.</summary>
    public string PathOf(string name) => Path.Combine(Root, name);

    public CountingRepository this[string name] => _repositories[PathOf(name)];

    /// <summary>
    /// The folder <paramref name="innerName"/> (made) lies inside <paramref name="outer"/>'s working
    /// tree: an open of it gives the outer repository, opened below its top (1.2, 0.8), as a new fake
    /// object whose reads are counted apart.
    /// </summary>
    public CountingRepository AddInside(string innerName, CountingRepository outer)
    {
        ArgumentNullException.ThrowIfNull(outer);
        Directory.CreateDirectory(PathOf(innerName));
        var around = new CountingRepository(outer.WorkingDirectory) { AtTop = false };
        _repositories[PathOf(innerName)] = around;
        return around;
    }

    /// <summary>The folder of <paramref name="name"/> is no repository any more: an open of it fails as git's would.</summary>
    public void Forget(string name) => _repositories.TryRemove(PathOf(name), out _);

    /// <summary>An open of <paramref name="name"/>'s folder fails with <paramref name="failure"/> (git refused it), its folder made.</summary>
    public void Refuse(string name, Exception failure)
    {
        Directory.CreateDirectory(PathOf(name));
        _refusals[PathOf(name)] = failure;
    }

    private readonly ConcurrentDictionary<string, Exception> _refusals = new(StringComparer.OrdinalIgnoreCase);

    public void Dispose() => _root.Dispose();

    public async Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _opens);
        var meter = Meter;
        meter?.Enter();
        try
        {
            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (meter?.Gate is { } shared)
            {
                await shared.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (_refusals.FirstOrDefault(refusal => WorkspacePaths.Same(refusal.Key, path)).Value is { } failure)
            {
                throw failure;
            }

            var repository = _repositories.FirstOrDefault(entry => WorkspacePaths.Same(entry.Key, path)).Value
                ?? throw new NotARepositoryException(path);
            if (repository.OpenGate is { } own)
            {
                await own.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return repository;
        }
        finally
        {
            meter?.Leave();
        }
    }

    public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IGitRepository> CloneAsync(
        string url,
        string destination,
        IProgress<CloneProgress>? progress,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string?>? environment = null) =>
        throw new NotSupportedException();
}

/// <summary>
/// A fake repository that counts its reads (refs, status, history, watchers, the backups' and the
/// merge tool's reads) and can hold its refs and status reads on gates; the reads at once are
/// counted too (<see cref="MaxReadsAtOnce"/>).
/// </summary>
internal sealed class CountingRepository(string folder, RepoRefs? refs = null, WorkingTreeStatus? status = null) : FakeRepositoryBase
{
    private int _refsReads;
    private int _statusReads;
    private int _historyLoads;
    private int _watchers;
    private int _backupFinds;
    private int _mergeToolReads;
    private int _readsNow;
    private int _maxReadsAtOnce;

    public override string WorkingDirectory { get; } = folder;

    public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

    public override string CommonDirectory => Common ?? GitDirectory;

    /// <summary>Another worktree's git folder as this one's common folder (a linked worktree), or null for its own.</summary>
    public string? Common { get; set; }

    public override string Name => Path.GetFileName(WorkingDirectory);

    public RepoRefs Refs { get; set; } = refs ?? DashboardTestSupport.MainRefs();

    public WorkingTreeStatus Status { get; set; } = status ?? WorkingTreeStatus.Clean;

    /// <summary>When set, the provider's open of this repository's folder waits for it (a row's probe, 2.10).</summary>
    public TaskCompletionSource? OpenGate { get; set; }

    /// <summary>When set, a refs read waits for it.</summary>
    public TaskCompletionSource? RefsGate { get; set; }

    /// <summary>When set, a status read waits for it.</summary>
    public TaskCompletionSource? StatusGate { get; set; }

    /// <summary>When set, a refs read fails with it.</summary>
    public Exception? RefsFailure { get; set; }

    /// <summary>When set, a status read fails with it.</summary>
    public Exception? StatusFailure { get; set; }

    public int RefsReads => _refsReads;

    public int StatusReads => _statusReads;

    public int HistoryLoads => _historyLoads;

    public int Watchers => _watchers;

    public int BackupFinds => _backupFinds;

    public int MergeToolReads => _mergeToolReads;

    public int MaxReadsAtOnce => _maxReadsAtOnce;

    /// <summary>The watchers made, in order.</summary>
    public ConcurrentQueue<CountingWatcher> MadeWatchers { get; } = new();

    /// <summary>When set, a history load waits for it.</summary>
    public TaskCompletionSource? HistoryGate { get; set; }

    /// <summary>What <see cref="OpenedAtTopAsync"/> answers: false for a folder inside another working tree (1.2).</summary>
    public bool AtTop { get; set; } = true;

    /// <summary>A meter several fakes share: their refs reads at once, across the repositories, and a gate for all of them.</summary>
    public ReadMeter? Meter { get; set; }

    public override Task<bool> OpenedAtTopAsync(CancellationToken cancellationToken = default) => Task.FromResult(AtTop);

    /// <summary>The refs a read returns are the ones set when it began.</summary>
    public override async Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _refsReads);
        var now = Interlocked.Increment(ref _readsNow);
        InterlockedMax(ref _maxReadsAtOnce, now);
        var refs = Refs;
        var failure = RefsFailure;
        var meter = Meter;
        meter?.Enter();
        try
        {
            if (RefsGate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (meter?.Gate is { } shared)
            {
                await shared.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            await Task.Yield();
            return failure is not null ? throw failure : refs;
        }
        finally
        {
            meter?.Leave();
            Interlocked.Decrement(ref _readsNow);
        }
    }

    /// <summary>The status a read returns is the one set when it began.</summary>
    public override async Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _statusReads);
        var status = Status;
        var failure = StatusFailure;
        if (StatusGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        await Task.Yield();
        return failure is not null ? throw failure : status;
    }

    public override async Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _historyLoads);
        if (HistoryGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public override IRepositoryWatcher CreateWatcher()
    {
        Interlocked.Increment(ref _watchers);
        var watcher = new CountingWatcher();
        MadeWatchers.Enqueue(watcher);
        return watcher;
    }

    public override Task<IReadOnlyList<string>> FindBackupsToRemoveAsync(BackupRetention retention, IReadOnlySet<string> keep, NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _backupFinds);
        return base.FindBackupsToRemoveAsync(retention, keep, options, cancellationToken);
    }

    public override Task<string?> ReadMergeToolAsync(NetworkOptions? options = null, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _mergeToolReads);
        return Task.FromResult<string?>("fake");
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }
}

/// <summary>Git calls at once (opens and refs reads) across several fakes, and a gate that holds them all (2.10's four reads at once).</summary>
internal sealed class ReadMeter
{
    private int _now;
    private int _max;
    private int _entered;

    /// <summary>When set, every metered refs read waits for it.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public int Now => Volatile.Read(ref _now);

    public int Max => Volatile.Read(ref _max);

    /// <summary>The metered reads begun so far.</summary>
    public int Entered => Volatile.Read(ref _entered);

    public void Enter()
    {
        Interlocked.Increment(ref _entered);
        var now = Interlocked.Increment(ref _now);
        int seen;
        while ((seen = Volatile.Read(ref _max)) < now && Interlocked.CompareExchange(ref _max, now, seen) != seen)
        {
        }
    }

    public void Leave() => Interlocked.Decrement(ref _now);
}

/// <summary>A watcher that counts its starts and pauses and raises a change when the test asks.</summary>
internal sealed class CountingWatcher : IRepositoryWatcher
{
    private int _paused;
    private int _pauses;

    public event EventHandler<RepositoryChangedEventArgs>? Changed;

    /// <summary>The pauses taken so far.</summary>
    public int Pauses => _pauses;

    /// <summary>The pauses out now: above 0 while it is paused.</summary>
    public int Paused => _paused;

    public bool Started { get; private set; }

    /// <summary>Whether it was paused when it started.</summary>
    public bool PausedAtStart { get; private set; }

    public void Start()
    {
        PausedAtStart = Volatile.Read(ref _paused) > 0;
        Started = true;
    }

    public IDisposable Pause()
    {
        Interlocked.Increment(ref _pauses);
        Interlocked.Increment(ref _paused);
        return new Resume(this);
    }

    /// <summary>
    /// A burst of changes in the git folder or the working tree: reported unless the watcher is
    /// paused, as the real watcher stops reporting changes while paused (D71). Returns whether it was.
    /// </summary>
    public bool Raise(bool gitFolder)
    {
        if (Volatile.Read(ref _paused) > 0)
        {
            return false;
        }

        Changed?.Invoke(this, new RepositoryChangedEventArgs(gitFolder, !gitFolder));
        return true;
    }

    public void Dispose()
    {
    }

    private sealed class Resume(CountingWatcher watcher) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Decrement(ref watcher._paused);
            }
        }
    }
}
