using System.Diagnostics;
using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// <see cref="RepositoryViewModel.IsWatcherQuiet"/>, which the harnesses wait for before they mark
/// a check's next calls (the review's finding on part S's read-backs, macOS's late watcher
/// refresh): false while the watcher holds a change, while a change it raised waits for its
/// refresh, and while the refresh it starts, or a status read, still runs; true otherwise. With a
/// watcher and reads the test holds.
/// </summary>
public class RepositoryWatcherQuietTests
{
    private const string Main = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task A_change_is_not_quiet_from_the_watchers_wait_to_the_end_of_its_refresh()
    {
        var repository = new HeldRepository();
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        await viewModel.StartAsync();
        Assert.True(viewModel.IsWatcherQuiet, "A started view model with nothing to do is not quiet.");

        // A change waiting for the watcher's quiet period.
        repository.Watcher.Quiet = false;
        Assert.False(viewModel.IsWatcherQuiet);
        repository.Watcher.Quiet = true;
        Assert.True(viewModel.IsWatcherQuiet);

        // A change in the git folder: its refresh's refs read held, then its status read held.
        var (refsBefore, statusBefore) = (repository.RefsReads, repository.StatusReads);
        var refs = repository.HoldRefs();
        var status = repository.HoldStatus();
        await Task.Run(() => repository.Watcher.Raise(gitFolder: true), TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => repository.RefsReads > refsBefore, "the refresh's refs read");
        Assert.False(viewModel.IsWatcherQuiet, "The refresh's refs read runs, and the view model is quiet.");
        refs.SetResult(repository.Refs);
        await WaitUntilAsync(() => repository.StatusReads > statusBefore, "the refresh's status read");
        Assert.False(viewModel.IsWatcherQuiet, "The refresh's status read runs, and the view model is quiet.");
        status.SetResult(WorkingTreeStatus.Clean);
        await WaitUntilAsync(() => viewModel.IsWatcherQuiet, "the refresh to end");

        // A change in the working tree: the status read alone.
        (refsBefore, statusBefore) = (repository.RefsReads, repository.StatusReads);
        status = repository.HoldStatus();
        await Task.Run(() => repository.Watcher.Raise(gitFolder: false), TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => repository.StatusReads > statusBefore, "the status read");
        Assert.False(viewModel.IsWatcherQuiet, "The status read runs, and the view model is quiet.");
        Assert.Equal(refsBefore, repository.RefsReads);
        status.SetResult(WorkingTreeStatus.Clean);
        await WaitUntilAsync(() => viewModel.IsWatcherQuiet, "the status read to end");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var waited = Stopwatch.StartNew();
        while (!condition())
        {
            if (waited.Elapsed > Timeout)
            {
                throw new TimeoutException($"Waited {Timeout.TotalSeconds:F0} s for {what}.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A repository with one commit on main, clean, whose next refs or status read the test may hold.</summary>
    private sealed class HeldRepository : FakeRepositoryBase
    {
        private TaskCompletionSource<RepoRefs>? _refs;
        private TaskCompletionSource<WorkingTreeStatus>? _status;
        private int _refsReads;
        private int _statusReads;

        public RepoRefs Refs { get; } = new(new HeadState("main", Main), [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Main, IsHead: true)], [], []);

        public RaisingWatcher Watcher { get; } = new();

        public int RefsReads => Volatile.Read(ref _refsReads);

        public int StatusReads => Volatile.Read(ref _statusReads);

        public override string WorkingDirectory => Path.Combine(Path.GetTempPath(), "watcher-quiet");

        public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

        public override string CommonDirectory => GitDirectory;

        public override string Name => "watcher-quiet";

        public TaskCompletionSource<RepoRefs> HoldRefs() => _refs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<WorkingTreeStatus> HoldStatus() => _status = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _refsReads);
            return Interlocked.Exchange(ref _refs, null)?.Task ?? Task.FromResult(Refs);
        }

        public override Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _statusReads);
            return Interlocked.Exchange(ref _status, null)?.Task ?? Task.FromResult(WorkingTreeStatus.Clean);
        }

        public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override IRepositoryWatcher CreateWatcher() => Watcher;
    }

    /// <summary>A watcher whose quiet state the test sets and whose Changed it raises.</summary>
    private sealed class RaisingWatcher : IRepositoryWatcher
    {
        public event EventHandler<RepositoryChangedEventArgs>? Changed;

        public bool Quiet { get; set; } = true;

        public bool IsQuiet => Quiet;

        public void Raise(bool gitFolder) => Changed?.Invoke(this, new RepositoryChangedEventArgs(gitFolder, !gitFolder));

        public void Start()
        {
        }

        public IDisposable Pause() => new Resume();

        public void Dispose()
        {
        }

        private sealed class Resume : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
