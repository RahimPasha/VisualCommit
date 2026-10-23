using System.Collections.Concurrent;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the bulk runs' tests share (docs/phase-6-design.md 4.13): fake repositories whose reads,
/// fetches and pulls a test holds on gates and counts (the fetches running at once across them
/// too), a provider of them by folder, and a dashboard over them with its session's services,
/// its folder check as a seam. Real git's runs are in <see cref="BulkRunTests"/>' window tests.
/// </summary>
internal sealed class BulkProvider : IRepositoryProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, BulkRepository> _repositories = new(StringComparer.OrdinalIgnoreCase);
    private readonly TempDirectory _root = new("bulk");
    private int _fetchesNow;
    private int _maxFetches;

    /// <summary>The folder the fakes' folders are made in.</summary>
    public string Root => _root.Path;

    /// <summary>The most fetches that ran at once, over every fake.</summary>
    public int MaxFetchesAtOnce => Volatile.Read(ref _maxFetches);

    /// <summary>The fetches running now, over every fake.</summary>
    public int FetchesNow => Volatile.Read(ref _fetchesNow);

    /// <summary>A fake at <c>{Root}/{name}</c> on <c>main</c> tracking <c>origin/main</c> (or as <paramref name="refs"/> says), its folders made on disk.</summary>
    public BulkRepository Add(string name, RepoRefs? refs = null, WorkingTreeStatus? status = null)
    {
        var repository = new BulkRepository(this, Path.Combine(Root, name), refs, status);
        Directory.CreateDirectory(repository.GitDirectory);
        _repositories[repository.WorkingDirectory] = repository;
        return repository;
    }

    public BulkRepository this[string name] => _repositories[Path.Combine(Root, name)];

    public string PathOf(string name) => Path.Combine(Root, name);

    public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) =>
        _opensAs.FirstOrDefault(entry => WorkspacePaths.Same(entry.Key, path)).Value is { } other
            ? Task.FromResult(other)
            : _repositories.FirstOrDefault(entry => WorkspacePaths.Same(entry.Key, path)).Value is { } repository
                ? Task.FromResult<IGitRepository>(repository)
                : Task.FromException<IGitRepository>(new NotARepositoryException(path));

    /// <summary>
    /// From now on an open of <paramref name="name"/>'s folder gives <paramref name="repository"/>:
    /// a repository git finds around the folder once its own <c>.git</c> is gone (D204, 0.8).
    /// </summary>
    public void OpenAs(string name, IGitRepository repository) => _opensAs[Path.Combine(Root, name)] = repository;

    private readonly ConcurrentDictionary<string, IGitRepository> _opensAs = new(StringComparer.OrdinalIgnoreCase);

    public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IGitRepository> CloneAsync(
        string url,
        string destination,
        IProgress<CloneProgress>? progress,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string?>? environment = null) => throw new NotSupportedException();

    public void Dispose() => _root.Dispose();

    internal void FetchStarted()
    {
        var now = Interlocked.Increment(ref _fetchesNow);
        int seen;
        while ((seen = Volatile.Read(ref _maxFetches)) < now && Interlocked.CompareExchange(ref _maxFetches, now, seen) != seen)
        {
        }
    }

    internal void FetchEnded() => Interlocked.Decrement(ref _fetchesNow);
}

/// <summary>
/// A fake repository for bulk runs: its refs and status as the test sets them (reads held on
/// <see cref="RefsGate"/> and <see cref="StatusGate"/>), a fetch held on <see cref="FetchGate"/>
/// (cancelled by its token, as git is killed), failing with <see cref="FetchFailure"/>, and a
/// pull's integration held on <see cref="PullGate"/>, ending as <see cref="Pulled"/> or failing
/// with <see cref="PullFailure"/>.
/// </summary>
internal sealed class BulkRepository(BulkProvider provider, string folder, RepoRefs? refs, WorkingTreeStatus? status) : FakeRepositoryBase
{
    private int _fetches;
    private int _pulls;
    private int _refsReads;

    public override string WorkingDirectory { get; } = folder;

    public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

    public override string CommonDirectory => Common ?? GitDirectory;

    /// <summary>Another worktree's git folder as this one's common folder, or null for its own.</summary>
    public string? Common { get; set; }

    public override string Name => Path.GetFileName(WorkingDirectory);

    public RepoRefs Refs { get; set; } = refs ?? DashboardTestSupport.MainRefs();

    public WorkingTreeStatus Status { get; set; } = status ?? WorkingTreeStatus.Clean;

    /// <summary>What <see cref="OpenedAtTopAsync"/> answers: false for a repository opened from a folder inside its working tree (1.2).</summary>
    public bool AtTop { get; set; } = true;

    public TaskCompletionSource? RefsGate { get; set; }

    public TaskCompletionSource? StatusGate { get; set; }

    public Exception? RefsFailure { get; set; }

    /// <summary>When set, a fetch waits for it (or for its cancellation).</summary>
    public TaskCompletionSource? FetchGate { get; set; }

    /// <summary>Completes when a fetch has started and waits at <see cref="FetchGate"/>.</summary>
    public TaskCompletionSource FetchWaiting { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Exception? FetchFailure { get; set; }

    /// <summary>When set, a pull's integration waits for it (past its cancel window).</summary>
    public TaskCompletionSource? PullGate { get; set; }

    /// <summary>Completes when a pull's integration has started.</summary>
    public TaskCompletionSource PullIntegrating { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PullResult Pulled { get; set; } = new(PullOutcome.UpToDate, 0);

    /// <summary>When set, the status a pull's integration leaves (a rebase stopped, say).</summary>
    public WorkingTreeStatus? StatusAfterPull { get; set; }

    public Exception? PullFailure { get; set; }

    public int Fetches => Volatile.Read(ref _fetches);

    public int Pulls => Volatile.Read(ref _pulls);

    public int RefsReads => Volatile.Read(ref _refsReads);

    public override Task<bool> OpenedAtTopAsync(CancellationToken cancellationToken = default) => Task.FromResult(AtTop);

    public override async Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _refsReads);
        var (read, failure) = (Refs, RefsFailure);
        if (RefsGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        await Task.Yield();
        return failure is not null ? throw failure : read;
    }

    public override async Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        var read = Status;
        if (StatusGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        await Task.Yield();
        return read;
    }

    public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public override IRepositoryWatcher CreateWatcher() => new CountingWatcher();

    public override async Task FetchAsync(string? remote, NetworkOptions options, bool noAutoMaintenance = false, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _fetches);
        provider.FetchStarted();
        try
        {
            if (FetchGate is { } gate)
            {
                FetchWaiting.TrySetResult();
                await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (FetchFailure is { } failure)
            {
                throw failure;
            }
        }
        finally
        {
            provider.FetchEnded();
        }
    }

    public override async Task<PullResult> IntegratePullAsync(
        GitRef branch,
        PullIntegration integration,
        Func<string, Task>? beforeRebase,
        NetworkOptions? options = null,
        PullIntegrationOptions? pullOptions = null,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _pulls);
        PullIntegrating.TrySetResult();
        if (PullGate is { } gate)
        {
            await gate.Task.ConfigureAwait(false);
        }

        await Task.Yield();
        if (StatusAfterPull is { } after)
        {
            Status = after;
        }

        return PullFailure is { } failure ? throw failure : Pulled;
    }

    /// <summary>Makes the next fetch's wait observable again.</summary>
    public void ResetWaits()
    {
        FetchWaiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PullIntegrating = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>git's refusal of a pull of diverged branches under <c>pull.ff=only</c> (design 4.8's E; Git 2.36 and 2.55: exit 128).</summary>
    public static GitException NotFastForward() =>
        new("git merge --ff-only --no-overwrite-ignore FETCH_HEAD", 128, "fatal: Not possible to fast-forward, aborting.\n");

    /// <summary>git's failure of a fetch from a remote it cannot reach (design 0.2's J, 2.36 and 2.55: exit 128), with D109's line.</summary>
    public static GitException CannotFetch(string folder) =>
        new(
            "git fetch --progress origin",
            128,
            $"fatal: '{folder}' does not appear to be a git repository\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\nerror: Could not fetch origin\n");
}

/// <summary>A dashboard over a <see cref="BulkProvider"/>'s fakes, with its services and log, read once.</summary>
internal sealed class BulkHost : IDisposable
{
    private BulkHost(BulkProvider provider, TabServices services, DashboardViewModel dashboard, RecordingLog log, HashSet<string> gone)
    {
        Provider = provider;
        Services = services;
        Dashboard = dashboard;
        Log = log;
        Gone = gone;
    }

    public BulkProvider Provider { get; }

    public TabServices Services { get; }

    public DashboardViewModel Dashboard { get; }

    public DashboardBulkViewModel Bulk => Dashboard.Bulk;

    public RecordingLog Log { get; }

    /// <summary>Folders the dashboard's folder check finds gone (its seam).</summary>
    public HashSet<string> Gone { get; }

    /// <summary>When set, the folder check of a folder it holds waits for it, off the UI thread.</summary>
    public ConcurrentDictionary<string, TaskCompletionSource> HeldChecks { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The dashboard's rows by name.</summary>
    public DashboardRowViewModel this[string name] => Dashboard.Rows.Single(row => row.Name == name);

    /// <summary>
    /// A dashboard of <paramref name="names"/> (the provider's fakes, or folders it does not hold),
    /// active, its rows read; a window view model around the services when
    /// <paramref name="window"/>, for tests whose tabs register.
    /// </summary>
    public static async Task<BulkHost> StartAsync(BulkProvider provider, string[] names, bool read = true, TabServices? services = null, bool active = true)
    {
        var log = new RecordingLog();
        var gone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        services ??= DashboardTestSupport.Services(provider, DashboardTestSupport.Store([.. names.Select(provider.PathOf)]), new ManualTimeProvider(), log);
        BulkHost? host = null;
        var dashboard = new DashboardViewModel(services.Workspaces.Current.Find("web")!, services, () => active, path => host!.Exists(path));
        host = new BulkHost(provider, services, dashboard, services.Log as RecordingLog ?? log, gone);
        if (read)
        {
            dashboard.OnBecameActive();
            await host.WaitForReadsAsync();
        }

        return host;
    }

    /// <summary>Waits until no row is being read.</summary>
    public Task WaitForReadsAsync() =>
        DashboardTestSupport.WaitUntilAsync(() => !Dashboard.IsPassRunning && Dashboard.Rows.All(row => !row.IsReading && !row.IsNotReadYet), "the rows' reads");

    /// <summary>Waits until the bulk run has ended, letting the UI thread run.</summary>
    public async Task WaitForRunAsync()
    {
        await DashboardTestSupport.WaitUntilAsync(() => Bulk.Current is { IsRunning: false }, "the run's end");
        await DashboardTestSupport.SettleAsync();
    }

    /// <summary>The Operation cell's text of <paramref name="name"/>'s row.</summary>
    public string Operation(string name) => this[name].Operation.Text;

    public void Dispose()
    {
        foreach (var check in HeldChecks.Values)
        {
            check.TrySetResult();
        }

        Dashboard.Dispose();
    }

    private bool Exists(string path)
    {
        if (HeldChecks.FirstOrDefault(entry => WorkspacePaths.Same(entry.Key, path) || path.StartsWith(entry.Key + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).Value is { } held)
        {
            Assert.False(Avalonia.Threading.Dispatcher.UIThread.CheckAccess(), "A folder check ran on the UI thread.");
            held.Task.Wait(TimeSpan.FromSeconds(60));
        }

        return !Gone.Any(folder => WorkspacePaths.Same(folder, path) || path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            && Directory.Exists(path);
    }
}
