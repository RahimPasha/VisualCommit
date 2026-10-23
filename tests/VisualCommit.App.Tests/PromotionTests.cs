using System.Diagnostics;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A tab adopts a dashboard row's summary-mode view model and promotes it (docs/phase-6-design.md
/// 3.3, 4.11, D209): the full start from the tab's stopwatch, no second history during its first
/// load, the watcher's pause owned by the view model, a tab that waits for a released summary's
/// queue, and auto-fetch counting from the promotion.
/// </summary>
public class PromotionTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public async Task After_promote_the_start_is_a_tabs_full_start_from_the_given_stopwatch()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        var log = new RecordingLog();
        var services = DashboardTestSupport.Services(provider, log: log);
        using var summary = RepositoryViewModel.ForSummary(repository, services);
        await summary.StartAsync();
        Assert.Equal((0, 0, 0), (repository.Watchers, repository.HistoryLoads, repository.BackupFinds));

        // The promotion makes no I/O.
        summary.Promote(new Stopwatch());
        Assert.False(summary.IsSummary);
        Assert.Equal((1, 1), (repository.RefsReads, repository.StatusReads));

        await summary.StartAsync();

        Assert.Equal(1, repository.Watchers);
        Assert.True(repository.MadeWatchers.Single().Started);
        Assert.Equal((2, 2, 1), (repository.RefsReads, repository.StatusReads, repository.HistoryLoads));
        await DashboardTestSupport.WaitUntilAsync(() => repository.BackupFinds == 1, "the clean-up at opening's read");

        // Q1's lines count from the stopwatch the tab gave, which never ran: 0 ms.
        Assert.Contains("api: opened after 0 ms", log.Messages);
        Assert.Contains("api: refs read after 0 ms", log.Messages);
        Assert.Contains(log.Messages, message => message.StartsWith("api: working tree: 0 staged, 0 unstaged", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task Refreshes_asked_for_during_the_first_load_run_once_after_it_without_a_second_history()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        var services = DashboardTestSupport.Services(provider);
        using var summary = RepositoryViewModel.ForSummary(repository, services);
        await summary.StartAsync();
        summary.Promote(Stopwatch.StartNew());
        repository.HistoryGate = new TaskCompletionSource();

        var start = summary.StartAsync();
        await DashboardTestSupport.WaitUntilAsync(() => repository.HistoryLoads == 1, "the first load");
        var watcher = repository.MadeWatchers.Single();

        // The queue's refresh (an adopted operation's end), a git-folder event and a working-tree
        // event, all during the first load: put off.
        var refreshed = summary.RefreshAsync();
        Assert.True(refreshed.IsCompleted);
        await summary.RefreshStatusAsync();
        Assert.True(watcher.Raise(gitFolder: true));
        Assert.True(watcher.Raise(gitFolder: false));
        await DashboardTestSupport.SettleAsync();
        Assert.Equal((2, 2, 1), (repository.RefsReads, repository.StatusReads, repository.HistoryLoads));

        repository.HistoryGate.SetResult();
        await start.WaitAsync(Patience);

        // One refresh after the load: the refs and the status once more, and the same refs load no history.
        Assert.Equal((3, 3, 1), (repository.RefsReads, repository.StatusReads, repository.HistoryLoads));
    }

    [AvaloniaFact]
    public async Task A_summary_read_overtaken_by_the_promotion_applies_nothing_and_leaves_first_read_to_the_full_start()
    {
        using var provider = new FakeProvider();
        var stale = DashboardTestSupport.MainRefs(behind: 1);
        var repository = provider.Add("api", stale);
        repository.RefsGate = new TaskCompletionSource();
        var held = repository.RefsGate;
        using var summary = RepositoryViewModel.ForSummary(repository, DashboardTestSupport.Services(provider));

        var summaryRead = summary.StartAsync();
        await DashboardTestSupport.WaitUntilAsync(() => repository.RefsReads == 1 && repository.StatusReads == 1, "the summary read");
        summary.Promote(Stopwatch.StartNew());
        Assert.False(summary.HasFirstRead);

        // The full start begins, held in its status read; the summary read ends meanwhile and
        // applies nothing, its first read left to the full start.
        var fresh = DashboardTestSupport.MainRefs(behind: 7);
        repository.Refs = fresh;
        repository.RefsGate = null;
        repository.StatusGate = new TaskCompletionSource();
        var full = summary.StartAsync();
        await DashboardTestSupport.WaitUntilAsync(() => repository.StatusReads == 2 && summary.Refs is not null, "the full start's refs, its status read held");
        Assert.Same(fresh, summary.Refs);
        held.SetResult();
        await summaryRead.WaitAsync(Patience);
        Assert.False(summary.HasFirstRead);
        Assert.False(full.IsCompleted);
        Assert.Same(fresh, summary.Refs);

        repository.StatusGate.SetResult();
        await full.WaitAsync(Patience);
        Assert.True(summary.HasFirstRead);
        Assert.Same(fresh, summary.Refs);
    }

    [AvaloniaFact]
    public async Task A_promotion_after_a_failed_summary_read_drops_its_error_and_its_rows_wait_for_the_full_start()
    {
        // 3.1's step 5 completed FirstRead with the failure; the tab that adopts the view model
        // shows no error over its graph, and the row that follows it reads Reading… until the full
        // start applies, Open enabled (it activates the tab), no run deciding it meanwhile.
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        repository.StatusFailure = new GitException("git status --porcelain=v2 --branch", 128, "fatal: index file corrupt\n");
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(repository.WorkingDirectory), new ManualTimeProvider(), new RecordingLog());
        var dashboard = new DashboardViewModel(services.Workspaces.Current.Find("web")!, services, () => true);
        var row = dashboard.Rows[0];
        dashboard.OnBecameActive();
        await DashboardTestSupport.WaitUntilAsync(() => row.ErrorText is not null && !row.IsReading, "the failed summary read");
        var summary = row.Repository!;
        Assert.True(summary.HasFirstRead);
        Assert.Equal(ViewModels.Workspaces.WorkspaceFolderProbe.NotOpenedText, row.State.Text);
        Assert.NotNull(dashboard.MenuStateOf(row).OpenReason);

        // The status reads again now; the full start is held in its refs read.
        repository.StatusFailure = null;
        repository.RefsGate = new TaskCompletionSource();
        using var tab = new RepoTabViewModel(services);
        var opening = tab.OpenAsync(repository.WorkingDirectory);
        await DashboardTestSupport.WaitUntilAsync(() => tab.Repository is not null, "the adoption");

        Assert.Same(summary, tab.Repository);
        Assert.Equal((null, null, false), (summary.ErrorText, summary.StatusErrorText, summary.HasFirstRead));
        Assert.True(row.FollowsTab);
        Assert.Equal((null, null), (row.FolderState, row.ErrorText));
        Assert.True(row.IsNotReadYet);
        Assert.False(row.HasRepository);
        Assert.Equal(ViewModels.Workspaces.DashboardTexts.ReadingText, row.State.Text);
        Assert.Null(dashboard.MenuStateOf(row).OpenReason);

        repository.RefsGate.SetResult();
        Assert.True(await opening.WaitAsync(Patience));
        Assert.True(summary.HasFirstRead);
        Assert.Null(summary.ErrorText);
        Assert.True(row.HasRepository);
        Assert.Equal(("main", string.Empty), (row.BranchText, row.State.Text));
        dashboard.Dispose();
    }

    [AvaloniaFact]
    public async Task The_watcher_a_promotion_makes_while_an_adopted_pull_runs_is_paused_until_that_run_ends()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        using var summary = RepositoryViewModel.ForSummary(repository, DashboardTestSupport.Services(provider));
        await summary.StartAsync();
        var running = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var pull = summary.Queue.RunAsync(new GitOperation("Pull", "Pulling origin/main into main…", OperationKind.Network, RefreshScope.Refs, async _ =>
        {
            running.SetResult();
            await finish.Task;
            return OperationReport.Done("main is already up to date");
        }));
        await running.Task.WaitAsync(Patience);

        summary.Promote(Stopwatch.StartNew());
        await summary.StartAsync().WaitAsync(Patience);
        var watcher = repository.MadeWatchers.Single();
        Assert.True(watcher.PausedAtStart);
        Assert.Equal(1, watcher.Paused);
        var (refs, status, history) = (repository.RefsReads, repository.StatusReads, repository.HistoryLoads);

        // An event during the pull reads nothing: the watcher is paused.
        Assert.False(watcher.Raise(gitFolder: true));
        await DashboardTestSupport.SettleAsync();
        Assert.Equal((refs, status, history), (repository.RefsReads, repository.StatusReads, repository.HistoryLoads));

        // The run ends: the pause goes, and the queue's one refresh follows.
        finish.SetResult();
        await pull.WaitAsync(Patience);
        await DashboardTestSupport.WaitUntilAsync(() => repository.RefsReads == refs + 1 && repository.StatusReads == status + 1, "the refresh after the pull");
        Assert.Equal(0, watcher.Paused);
        await DashboardTestSupport.SettleAsync();
        Assert.Equal((refs + 1, status + 1, history), (repository.RefsReads, repository.StatusReads, repository.HistoryLoads));

        // A later operation pauses and resumes it as in a tab opened directly.
        var pauses = watcher.Pauses;
        var during = -1;
        await summary.Queue.RunAsync(new GitOperation("Stage", "Staging…", OperationKind.Local, RefreshScope.None, _ =>
        {
            during = watcher.Paused;
            return Task.FromResult(OperationReport.Done(string.Empty));
        })).WaitAsync(Patience);
        Assert.Equal((pauses + 1, 1, 0), (watcher.Pauses, during, watcher.Paused));
    }

    [AvaloniaFact]
    public async Task An_operation_running_at_the_adoption_ends_in_the_tabs_status_bar()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        var services = DashboardTestSupport.Services(provider);
        var holder = new object();
        var summary = (await services.OpenRepositories.AcquireSummaryAsync(repository, holder, CancellationToken.None))!;
        await summary.StartAsync();
        var running = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var fetch = summary.Queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.Refs, async _ =>
        {
            running.SetResult();
            await finish.Task;
            return OperationReport.Done("Fetched from all remotes");
        }));
        await running.Task.WaitAsync(Patience);

        using var tab = new RepoTabViewModel(services);
        Assert.True(await tab.OpenAsync(repository.WorkingDirectory).WaitAsync(Patience));

        Assert.Same(summary, tab.Repository);
        Assert.False(summary.IsSummary);
        Assert.Empty(services.OpenRepositories.Summaries);
        Assert.Same(summary, services.OpenRepositories.TabFor(OpenRepositoriesKey(summary)));
        Assert.Equal(("Fetching from all remotes…", true), (tab.OperationStatus.Text, tab.OperationStatus.IsRunning));

        finish.SetResult();
        await fetch.WaitAsync(Patience);
        await DashboardTestSupport.WaitUntilAsync(() => tab.OperationStatus.Text == "Fetched from all remotes", "the fetch's result in the tab");
    }

    [AvaloniaFact]
    public async Task A_tab_opened_while_a_released_summarys_pull_integrates_waits_for_it()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        var services = DashboardTestSupport.Services(provider);
        var holder = new object();
        var summary = (await services.OpenRepositories.AcquireSummaryAsync(repository, holder, CancellationToken.None))!;
        await summary.StartAsync();
        var integrating = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var pull = summary.Queue.RunAsync(new GitOperation("Pull", "Pulling origin/main into main…", OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            integrating.SetResult();
            await finish.Task;
            return OperationReport.Done("main is already up to date");
        }));
        await integrating.Task.WaitAsync(Patience);

        // The dashboard closes: the summary is released while its local pull runs.
        services.OpenRepositories.ReleaseSummary(summary, holder);
        Assert.Same(summary, Assert.Single(services.OpenRepositories.Closing));

        using var tab = new RepoTabViewModel(services);
        var opening = tab.OpenAsync(repository.WorkingDirectory);
        await DashboardTestSupport.WaitUntilAsync(() => tab.WaitingText is not null, "the tab to wait");
        Assert.Equal("Waiting for Pull to finish…", tab.WaitingText);
        Assert.True(tab.IsOpening);
        Assert.False(tab.ShowsWelcome);
        Assert.Null(tab.Repository);
        Assert.Equal(1, repository.RefsReads);

        finish.SetResult();
        await pull.WaitAsync(Patience);
        Assert.True(await opening.WaitAsync(Patience));
        Assert.Null(tab.WaitingText);
        Assert.NotNull(tab.Repository);
        Assert.NotSame(summary, tab.Repository);
        Assert.Empty(services.OpenRepositories.Closing);
    }

    [AvaloniaFact]
    public async Task Auto_fetchs_first_fetch_comes_one_interval_after_the_promotion()
    {
        using var provider = new FakeProvider();
        var repository = provider.Add("api");
        var services = DashboardTestSupport.Services(provider);
        using var summary = RepositoryViewModel.ForSummary(repository, services);
        await summary.StartAsync();

        // The tab's stopwatch began with the user's open: here it has not run at all, while the
        // summary's own has run since the dashboard made it.
        summary.Promote(new Stopwatch());
        await summary.StartAsync();
        var clock = new ManualTimeProvider();
        var fetches = 0;
        summary.Queue.Finished += (_, e) =>
        {
            if (e.Operation.Title == AutoFetchScheduler.Title)
            {
                fetches++;
            }
        };
        using var scheduler = new AutoFetchScheduler(() => summary, () => false, services.Settings, clock, new RecordingLog())
        {
            ReadEnvironment = name => name == AutoFetchScheduler.MinutesVariable ? "5" : null,
        };

        var opened = clock.GetUtcNow();
        scheduler.Tick(opened);
        scheduler.Tick(opened + TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(0, fetches);

        scheduler.Tick(opened + TimeSpan.FromMinutes(5));
        await DashboardTestSupport.WaitUntilAsync(() => fetches == 1, "the first auto-fetch");
    }

    [AvaloniaFact]
    public async Task After_promote_a_save_of_gits_settings_reads_the_merge_tool_once()
    {
        using var provider = new FakeProvider();
        var conflicted = new WorkingTreeStatus([], [new ChangedFile("notes.txt", FileChangeKind.Conflicted)], new HashSet<string>(StringComparer.Ordinal))
        {
            Conflicts = [new ConflictEntry("notes.txt", ConflictKind.BothModified, false, "100644", "100644", "100644", "1", "2", "3")],
        };
        var repository = provider.Add("api", status: conflicted);
        var services = DashboardTestSupport.Services(provider);
        using var summary = RepositoryViewModel.ForSummary(repository, services);
        await summary.StartAsync();

        // In summary mode a save reads nothing; the positive control, a tab on the same services
        // told after it, reads merge.tool.
        var control = await GitSettingsChangedTests.ControlAsync(provider, services);
        services.RaiseGitSettingsChanged();
        await control.ToldAsync();
        Assert.Equal(0, repository.MergeToolReads);

        summary.Promote(Stopwatch.StartNew());
        await summary.StartAsync();
        await DashboardTestSupport.WaitUntilAsync(() => repository.MergeToolReads == 1, "the full start's read of merge.tool");

        services.RaiseGitSettingsChanged();
        await DashboardTestSupport.WaitUntilAsync(() => repository.MergeToolReads == 2, "the read after the save");
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(2, repository.MergeToolReads);
    }

    private static string OpenRepositoriesKey(RepositoryViewModel repository) => ViewModels.Workspaces.OpenRepositories.KeyOf(repository);
}
