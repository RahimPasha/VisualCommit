using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A dashboard row's view model in summary mode (docs/phase-6-design.md 3.1, 3.2, D209): it reads
/// the refs and the status as a tab reads them and nothing else, and its queue, operations,
/// toolbar, status and undo history work as a tab's.
/// </summary>
public class SummaryModeTests
{
    private static readonly string GitPath = GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_summary_read_is_one_refs_read_and_one_status_read_and_nothing_else()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        var log = new RecordingLog();
        var session = new CountingSession();
        var services = DashboardTestSupport.Services(provider, log: log, session: session);
        var updatesBefore = session.Updates;
        using var summary = RepositoryViewModel.ForSummary(repository, services);
        Assert.True(summary.IsSummary);
        Assert.False(summary.HasFirstRead);

        await summary.StartAsync();

        Assert.Equal((1, 1), (repository.RefsReads, repository.StatusReads));
        Assert.Equal((0, 0, 0), (repository.Watchers, repository.HistoryLoads, repository.BackupFinds));
        Assert.True(summary.HasFirstRead);
        Assert.True(summary.FirstRead.IsCompletedSuccessfully);
        Assert.Equal("main", summary.BranchText);
        Assert.Same(repository.Refs, summary.Refs);
        Assert.Same(CommitGraphData.Empty, summary.Graph);
        Assert.Null(summary.Filters);

        // The session is neither read for a filter nor written (no Touch, no Write).
        Assert.Equal(updatesBefore, session.Updates);

        // One summary line, at Debug, and none of the lines a tab's opening logs (Q1's, the history's).
        Assert.Single(log.Messages, message => message.StartsWith("api: summary read in ", StringComparison.Ordinal) && message.EndsWith(" ms", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Messages, message =>
            message.Contains("opened after", StringComparison.Ordinal)
            || message.Contains("refs read after", StringComparison.Ordinal)
            || message.Contains("first commits from git after", StringComparison.Ordinal)
            || message.Contains("loaded ", StringComparison.Ordinal)
            || message.Contains("first graph rows drawn", StringComparison.Ordinal)
            || message.Contains("working tree:", StringComparison.Ordinal));

        // A second start reads nothing more: one read, shared.
        await summary.StartAsync();
        Assert.Equal((1, 1), (repository.RefsReads, repository.StatusReads));
    }

    [Fact]
    public async Task A_refresh_reads_the_refs_and_the_status_and_loads_no_history()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("docs", DashboardTestSupport.MainRefs(behind: 3));
        var log = new RecordingLog();
        using var summary = RepositoryViewModel.ForSummary(repository, DashboardTestSupport.Services(provider, log: log));
        await summary.StartAsync();

        repository.Refs = DashboardTestSupport.MainRefs(behind: 4);
        await summary.RefreshAsync();
        await summary.RefreshStatusAsync();

        Assert.Equal((2, 3, 0), (repository.RefsReads, repository.StatusReads, repository.HistoryLoads));
        Assert.Equal(4, summary.Refs!.Refs[0].Behind);
        Assert.Equal(2, log.Messages.Count(message => message.StartsWith("docs: summary read in ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task First_read_completes_after_a_failed_read_with_the_error()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        repository.RefsFailure = new GitException("git for-each-ref", 128, "fatal: detected dubious ownership in repository at 'x'");
        using var summary = RepositoryViewModel.ForSummary(repository, DashboardTestSupport.Services(provider));

        await summary.StartAsync();

        Assert.True(summary.HasFirstRead);
        Assert.Equal("fatal: detected dubious ownership in repository at 'x'", summary.ErrorText);

        // A later read that works clears the error.
        repository.RefsFailure = null;
        await summary.RefreshAsync();
        Assert.Null(summary.ErrorText);
        Assert.NotNull(summary.Refs);
    }

    [Fact]
    public async Task First_read_waits_for_both_the_refs_and_the_status()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        repository.StatusGate = new TaskCompletionSource();
        using var summary = RepositoryViewModel.ForSummary(repository, DashboardTestSupport.Services(provider));

        var start = summary.StartAsync();
        await DashboardTestSupport.WaitUntilAsync(() => summary.Refs is not null, "the refs applied");
        Assert.False(summary.HasFirstRead);

        repository.StatusGate.SetResult();
        await start;
        Assert.True(summary.HasFirstRead);
    }

    [Fact]
    public async Task The_toolbars_states_and_reasons_are_a_full_view_models()
    {
        using var provider = new FakeProvider();
        var detached = new RepoRefs(new HeadState(null, DashboardTestSupport.Head), [], [], ["origin"]);
        var merging = WorkingTreeStatus.Clean with { InProgress = new InProgressState(InProgressKind.Merge, DashboardTestSupport.Upstream, "Merge branch 'x'", null, null, 0, false) };
        foreach (var (refs, status) in new[]
        {
            (DashboardTestSupport.MainRefs(ahead: 2), WorkingTreeStatus.Clean),
            (DashboardTestSupport.MainRefs(remote: false), WorkingTreeStatus.Clean),
            (detached, WorkingTreeStatus.Clean),
            (DashboardTestSupport.MainRefs(), merging),
        })
        {
            var repository = provider.Add("repo-" + Guid.NewGuid().ToString("N")[..6], refs, status);
            var services = DashboardTestSupport.Services(provider);
            using var summary = RepositoryViewModel.ForSummary(repository, services);
            using var full = new RepositoryViewModel(repository, services.Settings, services.Dates, services.Log, dialogs: services.Dialogs, services: services);
            await summary.StartAsync();
            await full.StartAsync();

            Assert.Equal(Toolbar(full.Toolbar), Toolbar(summary.Toolbar));
        }

        static object Toolbar(ToolbarViewModel toolbar) => (
            toolbar.CanFetch, toolbar.FetchToolTip, toolbar.FetchDisabledReason,
            toolbar.CanPull, toolbar.PullToolTip, toolbar.PullDisabledReason,
            toolbar.CanPush, toolbar.PushToolTip,
            toolbar.CanBranch, toolbar.CanStash, toolbar.StashToolTip, toolbar.CanPop, toolbar.PopToolTip);
    }

    [Fact]
    public async Task Disposing_cancels_a_running_fetch_and_lets_a_local_operation_finish()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        var summary = RepositoryViewModel.ForSummary(repository, DashboardTestSupport.Services(provider));
        await summary.StartAsync();
        var started = new TaskCompletionSource();
        var fetch = summary.Queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.None, async context =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Fetched from all remotes");
        }));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        summary.Dispose();
        Assert.Equal(OperationOutcome.Cancelled, (await fetch.WaitAsync(TimeSpan.FromSeconds(30), Cancelled)).Outcome);

        var other = RepositoryViewModel.ForSummary(provider.Add("web-app"), DashboardTestSupport.Services(provider));
        await other.StartAsync();
        var running = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var commit = other.Queue.RunAsync(new GitOperation("Commit", "Committing…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            running.SetResult();
            await finish.Task;
            return OperationReport.Done("Committed");
        }));
        await running.Task.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        other.Dispose();
        finish.SetResult();
        Assert.Equal(OperationOutcome.Succeeded, (await commit.WaitAsync(TimeSpan.FromSeconds(30), Cancelled)).Outcome);
    }

    [Fact]
    public async Task A_fetch_runs_with_the_toolbars_title_and_result()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var (summary, services) = await SummaryAsync(copy.PathOf("docs"));
        using (summary)
        {
            Assert.Equal(3, summary.Refs!.Refs.Single(r => r.FullName == "refs/heads/main").Behind);
            string? title = null;
            summary.Queue.Finished += (_, e) => title = e.Operation.Title;

            await summary.Operations.FetchAllAsync();

            Assert.Equal(OperationTexts.FetchAll.Title, title);
            Assert.Equal(("Fetched from all remotes", OperationOutcome.Succeeded), (summary.Queue.LastResult!.Message, summary.Queue.LastResult.Outcome));
            Assert.Equal(("Fetched from all remotes", OperationStatusTone.Secondary), (summary.OperationStatus.CompactText, summary.OperationStatus.CompactTone));

            // The fetch's refresh read the refs and the status again, and still no history.
            Assert.Equal(4, summary.Refs!.Refs.Single(r => r.FullName == "refs/heads/main").Behind);
            Assert.Same(CommitGraphData.Empty, summary.Graph);
        }
    }

    [Fact]
    public async Task A_pull_is_recorded_in_the_worktrees_history_and_a_tab_opened_later_offers_its_undo()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var (summary, services) = await SummaryAsync(copy.PathOf("docs"));
        using (summary)
        {
            await summary.Operations.PullAsync();
            Assert.Equal("Pulled 4 commits into main (fast-forward)", summary.Queue.LastResult!.Message);
        }

        // A tab opened afterwards in the same session shares the worktree's history (D154).
        var git = await GitRepository.OpenAsync(new GitRunner(GitPath, new GitCallLog()), copy.PathOf("docs"), Cancelled);
        using var tab = new RepositoryViewModel(git, services.Settings, services.Dates, services.Log, dialogs: services.Dialogs, services: services);
        await tab.StartAsync();
        Assert.True(tab.Toolbar.CanUndo);
        Assert.StartsWith("Undo pull into main", tab.Toolbar.UndoToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pull_stopped_with_conflicts_leaves_its_pending_entry_and_the_banner()
    {
        using var repo = await TempRepo.CreateAsync("pulling");
        await repo.CommitFileAsync("a.txt", "base\n", "Base");
        var bare = await repo.AddBareRemoteAsync();
        await repo.PushAsync("origin", true, "main");
        var other = await repo.CloneSiblingAsync("other", bare);
        await other.GitAsync("checkout", "--quiet", "-B", "main", "origin/main");
        await other.CommitFileAsync("a.txt", "theirs\n", "Theirs");
        await other.PushAsync("origin", false, "main");
        await repo.CommitFileAsync("a.txt", "ours\n", "Ours");

        var (summary, _) = await SummaryAsync(repo.Path);
        using (summary)
        {
            await summary.Operations.PullAsync();

            Assert.Equal(("Pull stopped: 1 file has conflicts", OperationOutcome.Conflicts), (summary.Queue.LastResult!.Message, summary.Queue.LastResult.Outcome));
            Assert.Equal(InProgressKind.Merge, summary.Status.InProgress.Kind);
            Assert.True(summary.Banner.IsVisible);
            Assert.False(summary.Toolbar.CanUndo);
            Assert.Equal("Finish or abort the merge first", summary.Toolbar.UndoToolTip);
        }
    }

    /// <summary>A summary-mode view model on the real repository at <paramref name="folder"/>, read.</summary>
    private static async Task<(RepositoryViewModel Summary, TabServices Services)> SummaryAsync(string folder)
    {
        var calls = new GitCallLog();
        var git = await GitRepository.OpenAsync(new GitRunner(GitPath, calls), folder, Cancelled);
        var services = new TabServices(
            new NoRepositories(),
            new NoFolders(),
            new MainWindowViewModelTests.FakeSettings(),
            new DashboardTestSupport.MemorySession(),
            new Core.DateDisplay(TimeZoneInfo.Utc),
            new RecordingLog(),
            gitCalls: calls);
        var summary = RepositoryViewModel.ForSummary(git, services);
        await summary.StartAsync();
        Assert.True(summary.HasFirstRead);
        Assert.Null(summary.ErrorText);
        return (summary, services);
    }

    private sealed class CountingSession : ISessionStore
    {
        public SessionState Current { get; private set; } = new();

        public int Updates { get; private set; }

        public void Update(Func<SessionState, SessionState> change)
        {
            Updates++;
            Current = change(Current);
        }
    }

    private sealed class NoRepositories : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) => throw new NotSupportedException();
    }

    private sealed class NoFolders : Services.IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
