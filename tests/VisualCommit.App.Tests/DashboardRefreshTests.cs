using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// When a dashboard's rows are read (docs/phase-6-design.md 2.10, D207; WD27, WD35, WD37): each
/// trigger with the 10 s rule by the read clock, rows that follow tabs never read nor counted, four
/// git calls at once, the folder checks off the UI thread and the waits for a closed queue that
/// hold nothing and a pass that ends without them, the probe's states, the row's token, Locate…
/// and Refresh, and a closed dashboard that reacts to nothing.
/// </summary>
public class DashboardRefreshTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public async Task Becoming_active_reads_rows_never_read_and_again_only_ten_seconds_after_their_last_read()
    {
        using var provider = new FakeProvider();
        var (api, docs) = (provider.Add("api"), provider.Add("docs"));
        var (dashboard, log, clock) = Dashboard(provider, [api.WorkingDirectory, docs.WorkingDirectory]);

        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);
        Assert.Equal(["Web: read 2 repositories in"], PassLines(log));
        Assert.Equal((1, 1), (api.RefsReads, docs.RefsReads));
        Assert.All(dashboard.Rows, row => Assert.Equal(DashboardTexts.UpToDateText, row.AheadBehind.Text));

        // Within 10 s: no row to read, so no pass and no line (WD35), decided at once (WD37).
        dashboard.OnBecameActive();
        Assert.False(dashboard.IsPassRunning);
        Assert.DoesNotContain(dashboard.Rows, row => row.IsReading);
        clock.Advance(TimeSpan.FromSeconds(10) - TimeSpan.FromTicks(1));
        dashboard.OnBecameActive();
        await DashboardTestSupport.SettleAsync();
        Assert.Single(PassLines(log));
        Assert.Equal((1, 1), (api.RefsReads, docs.RefsReads));

        clock.Advance(TimeSpan.FromTicks(1));
        dashboard.OnBecameActive();
        Assert.True(dashboard.IsPassRunning);
        await WaitForPassesAsync(log, 2);
        Assert.Equal((2, 2), (api.RefsReads, docs.RefsReads));
    }

    [AvaloniaFact]
    public async Task The_windows_activation_follows_the_same_ten_seconds()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var (dashboard, log, clock) = Dashboard(provider, [api.WorkingDirectory]);
        dashboard.OnWindowActivated();
        await WaitForPassesAsync(log, 1);
        Assert.Equal(["Web: read 1 repository in"], PassLines(log));

        api.Status = new WorkingTreeStatus([], [new ChangedFile("todo.txt", FileChangeKind.Added)], new HashSet<string>(StringComparer.Ordinal) { "todo.txt" });
        dashboard.OnWindowActivated();
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(("Clean", 1), (dashboard.Rows[0].Changes.Text, api.StatusReads));

        clock.Advance(TimeSpan.FromSeconds(10));
        dashboard.OnWindowActivated();
        await WaitForPassesAsync(log, 2);
        Assert.Equal("1 untracked", dashboard.Rows[0].Changes.Text);
    }

    [AvaloniaFact]
    public async Task Refresh_reads_every_row_without_a_tab_at_any_time()
    {
        using var provider = new FakeProvider();
        var (api, docs) = (provider.Add("api"), provider.Add("docs"));
        var services = Services(provider, [api.WorkingDirectory, docs.WorkingDirectory], out var log, out _);
        using var tab = Tab(services, docs);
        await tab.StartAsync();
        var dashboard = new DashboardViewModel(services.Workspaces.Current.Find("web")!, services, () => true);
        Assert.True(dashboard.Rows[1].FollowsTab);

        await dashboard.RefreshCommand.ExecuteAsync(null);
        await dashboard.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["Web: read 1 repository in", "Web: read 1 repository in"], PassLines(log));
        Assert.Equal(2, api.RefsReads);

        // The row that follows docs' tab was never read by the dashboard: its one refs read is the tab's.
        Assert.Equal(1, docs.RefsReads);
        Assert.Same(tab, dashboard.Rows[1].Repository);
        Assert.DoesNotContain(services.OpenRepositories.Summaries, summary => summary.Name == "docs");
    }

    [AvaloniaFact]
    public async Task A_row_that_follows_a_tab_reads_reading_until_the_tabs_first_read()
    {
        using var provider = new FakeProvider();
        var docs = provider.Add("docs", DashboardTestSupport.MainRefs(behind: 3));
        docs.RefsGate = new TaskCompletionSource();
        var services = Services(provider, [docs.WorkingDirectory], out var log, out _);
        using var tab = Tab(services, docs);
        var start = tab.StartAsync();
        var dashboard = new DashboardViewModel(services.Workspaces.Current.Find("web")!, services, () => true);
        var row = dashboard.Rows[0];

        Assert.True(row.FollowsTab);
        Assert.True(row.IsNotReadYet);
        Assert.Equal(DashboardTexts.ReadingText, row.State.Text);
        Assert.Equal(DashboardTexts.WaitToBeRead("docs"), dashboard.MenuStateOf(row).FetchReason);

        // The only row follows a tab: activation reads nothing and logs nothing.
        dashboard.OnBecameActive();
        Assert.False(dashboard.IsPassRunning);

        docs.RefsGate.SetResult();
        await start.WaitAsync(Patience);
        await DashboardTestSupport.WaitUntilAsync(() => !row.IsNotReadYet, "the tab's first read in the row");
        Assert.Equal(("main", "3 behind", string.Empty), (row.BranchText, row.AheadBehind.AutomationName, row.State.Text));
        Assert.Empty(PassLines(log));
        Assert.Equal(1, docs.RefsReads);
    }

    [AvaloniaFact]
    public async Task Rows_the_workspace_gains_while_active_are_read_at_once()
    {
        using var provider = new FakeProvider();
        var (api, docs) = (provider.Add("api"), provider.Add("docs"));
        var (dashboard, log, _) = Dashboard(provider, [api.WorkingDirectory]);
        var store = (InMemoryWorkspaceStore)Store(dashboard);
        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);

        store.Update(list => WorkspaceEdits.Append(list, "web", [docs.WorkingDirectory]));

        Assert.Equal(2, dashboard.Rows.Count);
        await WaitForPassesAsync(log, 2);
        Assert.Equal(["Web: read 1 repository in", "Web: read 1 repository in"], PassLines(log));
        Assert.Equal((1, 1), (api.RefsReads, docs.RefsReads));
        Assert.Equal("main", dashboard.Rows[1].BranchText);
    }

    [AvaloniaFact]
    public async Task A_row_gained_in_the_background_waits_for_the_dashboard_to_become_active()
    {
        using var provider = new FakeProvider();
        var docs = provider.Add("docs");
        var (dashboard, log, _) = Dashboard(provider, [], active: false);
        var store = (InMemoryWorkspaceStore)Store(dashboard);

        store.Update(list => WorkspaceEdits.Append(list, "web", [docs.WorkingDirectory]));
        await DashboardTestSupport.SettleAsync();

        Assert.Single(dashboard.Rows);
        Assert.False(dashboard.IsPassRunning);
        Assert.Equal((0, 0), (docs.RefsReads, provider.Opens));
        Assert.Equal(DashboardTexts.ReadingText, dashboard.Rows[0].State.Text);

        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);
        Assert.Equal(1, docs.RefsReads);
    }

    [AvaloniaFact]
    public async Task Rows_added_from_open_tabs_follow_them_without_a_git_call_and_without_a_pass()
    {
        using var provider = new FakeProvider();
        var (webApp, docs) = (provider.Add("web-app"), provider.Add("docs"));
        var (dashboard, log, _) = Dashboard(provider, []);
        var services = ServicesOf(dashboard);
        using var webAppTab = Tab(services, webApp);
        using var docsTab = Tab(services, docs);
        await webAppTab.StartAsync();
        await docsTab.StartAsync();
        var opens = provider.Opens;

        Store(dashboard).Update(list => WorkspaceEdits.Append(list, "web", [webApp.WorkingDirectory, docs.WorkingDirectory]));

        // WD37: decided at once, on the UI thread: nothing reads, no pass.
        Assert.False(dashboard.IsPassRunning);
        Assert.DoesNotContain(dashboard.Rows, row => row.IsReading);
        Assert.All(dashboard.Rows, row => Assert.True(row.FollowsTab));
        await DashboardTestSupport.SettleAsync();
        Assert.Empty(PassLines(log));
        Assert.Equal((1, 1, opens), (webApp.RefsReads, docs.RefsReads, provider.Opens));
        Assert.Empty(services.OpenRepositories.Summaries);
        Assert.Equal("main", dashboard.Rows[0].BranchText);
    }

    [AvaloniaFact]
    public async Task A_located_folder_is_read_at_once_with_a_new_token()
    {
        using var provider = new FakeProvider();
        var gone = provider.PathOf("old-site");
        var notes = provider.Add("archive-old-site");
        var (dashboard, log, _) = Dashboard(provider, [gone]);
        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);
        var row = dashboard.Rows[0];
        dashboard.SelectedRow = row;
        Assert.Equal("Folder not found", row.State.Text);

        Store(dashboard).Update(list => WorkspaceEdits.Replace(list, "web", gone, notes.WorkingDirectory));

        Assert.Same(row, Assert.Single(dashboard.Rows));
        Assert.Same(row, dashboard.SelectedRow);
        Assert.Equal(notes.WorkingDirectory, row.Path);
        await WaitForPassesAsync(log, 2);
        Assert.Equal(("archive-old-site", "main", string.Empty), (row.Name, row.BranchText, row.State.Text));
        Assert.Equal(1, notes.RefsReads);
    }

    [AvaloniaFact]
    public async Task At_most_four_rows_are_read_at_once()
    {
        // Every git call of a read holds a slot (2.10): the probe's open as the summary's refs read.
        using var provider = new FakeProvider();
        var meter = new ReadMeter { Gate = new TaskCompletionSource() };
        provider.Meter = meter;
        var folders = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            var repository = provider.Add($"repo{i}");
            repository.Meter = meter;
            folders.Add(repository.WorkingDirectory);
        }

        var (dashboard, log, _) = Dashboard(provider, [.. folders]);
        dashboard.OnBecameActive();

        await DashboardTestSupport.WaitUntilAsync(() => meter.Now == DashboardViewModel.MaxReads, "four opens held");
        await DashboardTestSupport.SettleAsync();
        Assert.Equal((4, 4, 4), (meter.Entered, meter.Now, provider.Opens));
        Assert.Equal(6, dashboard.Rows.Count(row => row.IsReading));

        meter.Gate.SetResult();
        await WaitForPassesAsync(log, 1);
        Assert.Equal(["Web: read 6 repositories in"], PassLines(log));

        // Six opens and six refs reads, never more than four at once.
        Assert.Equal((12, 4), (meter.Entered, meter.Max));
    }

    [AvaloniaFact]
    public async Task A_pass_whose_reads_are_all_held_in_folder_checks_ends_after_the_patience_and_later_passes_read()
    {
        using var provider = new FakeProvider();
        var docs = provider.Add("docs");
        var held = provider.PathOf("share");
        var gate = new ManualResetEventSlim();
        var holding = true;
        var heldCalls = 0;
        bool Exists(string path)
        {
            if (WorkspacePaths.Same(path, held) && Volatile.Read(ref holding))
            {
                Interlocked.Increment(ref heldCalls);
                gate.Wait(TimeSpan.FromMinutes(2));
                return false;
            }

            return Directory.Exists(path);
        }

        // The share's row and docs, whose tab is open: the start reads the share's row alone.
        var (dashboard, log, clock) = Dashboard(provider, [held, docs.WorkingDirectory], exists: Exists);
        var services = ServicesOf(dashboard);
        using var docsTab = Tab(services, docs);
        await docsTab.StartAsync();
        var row = dashboard.Rows[0];
        try
        {
            var start = dashboard.ReadAtStartAsync();
            await DashboardTestSupport.WaitUntilAsync(() => Volatile.Read(ref heldCalls) == 1, "the share's folder check");
            await DashboardTestSupport.SettleAsync();
            Assert.False(start.IsCompleted);
            Assert.True(dashboard.IsPassRunning);

            // No read finished: the pass ends after CheckPatience by the read clock, reading none, logging nothing.
            clock.Advance(DashboardViewModel.CheckPatience);
            await start.WaitAsync(Patience);
            Assert.False(dashboard.IsPassRunning);
            Assert.True(row.IsReading);
            Assert.Empty(PassLines(log));

            // The check returns: the row shows its state; F5, whose other row follows a tab, ends the same way.
            gate.Set();
            await DashboardTestSupport.WaitUntilAsync(() => !row.IsReading, "the held check to return");
            Assert.Equal("Folder not found", row.State.Text);
            gate.Reset();
            var refresh = dashboard.RefreshCommand.ExecuteAsync(null);
            await DashboardTestSupport.WaitUntilAsync(() => Volatile.Read(ref heldCalls) == 2, "the share's second check");
            await DashboardTestSupport.SettleAsync();
            Assert.False(refresh.IsCompleted);
            clock.Advance(DashboardViewModel.CheckPatience);
            await refresh.WaitAsync(Patience);
            Assert.Empty(PassLines(log));

            // Nothing was left folded for good: once the share answers, F5 reads it and logs it.
            Volatile.Write(ref holding, false);
            gate.Set();
            await DashboardTestSupport.WaitUntilAsync(() => !row.IsReading, "the second check to return");
            await dashboard.RefreshCommand.ExecuteAsync(null).WaitAsync(Patience);
            Assert.Equal(["Web: read 1 repository in"], PassLines(log));
            Assert.Equal(1, docs.RefsReads);
        }
        finally
        {
            gate.Set();
        }
    }

    [AvaloniaFact]
    public async Task A_folder_state_row_whose_folder_gained_a_tab_follows_it_on_F5_and_the_pass_ends()
    {
        using var provider = new FakeProvider();
        var plain = provider.PathOf("plain");
        Directory.CreateDirectory(plain);
        var (dashboard, log, _) = Dashboard(provider, [plain]);
        var services = ServicesOf(dashboard);
        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);
        var row = dashboard.Rows[0];
        Assert.Equal("Not a repository", row.State.Text);

        // The folder became a repository and a tab opened it: the row keeps its state until a trigger.
        var repository = provider.Add("plain");
        using var tab = Tab(services, repository);
        await tab.StartAsync();
        Assert.False(row.FollowsTab);

        // F5: the row's read follows the tab at once, so the pass ends at once, and reads nothing.
        await dashboard.RefreshCommand.ExecuteAsync(null).WaitAsync(Patience);
        Assert.True(row.FollowsTab);
        Assert.False(dashboard.IsPassRunning);
        Assert.Equal(("main", string.Empty), (row.BranchText, row.State.Text));
        Assert.Single(PassLines(log));
        Assert.Equal(1, repository.RefsReads);
    }

    [AvaloniaFact]
    public async Task A_rows_wait_for_a_closed_queue_holds_nothing_and_its_pass_ends_without_it()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var meter = new ReadMeter { Gate = new TaskCompletionSource() };
        var others = Enumerable.Range(1, DashboardViewModel.MaxReads).Select(i => provider.Add($"repo{i}")).ToList();
        others.ForEach(other => other.Meter = meter);
        var (dashboard, log, clock) = Dashboard(provider, [api.WorkingDirectory, .. others.Select(other => other.WorkingDirectory)]);
        var services = ServicesOf(dashboard);

        // api's tab runs its merge tool, then closes: its queue still runs on the worktree (3.3).
        var tab = Tab(services, api);
        await tab.StartAsync();
        var (running, finish) = (new TaskCompletionSource(), new TaskCompletionSource());
        var tool = tab.Queue.RunAsync(new GitOperation("Merge tool for notes.txt", "Waiting for kdiff3 to close…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            running.SetResult();
            await finish.Task;
            return OperationReport.Done("Resolved notes.txt");
        }));
        await running.Task.WaitAsync(Patience);
        services.OpenRepositories.UnregisterTab(tab);
        tab.Dispose();
        var row = dashboard.Rows[0];
        Assert.False(row.FollowsTab);
        var reads = api.RefsReads;

        // F5: api's read opens the folder, then waits for the closed queue holding no slot: the
        // four other rows read in the four slots meanwhile.
        var refresh = dashboard.RefreshCommand.ExecuteAsync(null);
        await DashboardTestSupport.WaitUntilAsync(() => meter.Now == DashboardViewModel.MaxReads, "the four other reads held");
        Assert.True(row.IsReading);
        Assert.Equal(reads, api.RefsReads);

        // Once they end, only api's wait is left: the pass ends after CheckPatience without it.
        meter.Gate.SetResult();
        await DashboardTestSupport.WaitUntilAsync(() => others.All(other => other.RefsReads == 1) && dashboard.Rows.Skip(1).All(other => !other.IsReading), "the four other reads");
        await DashboardTestSupport.SettleAsync();
        Assert.False(refresh.IsCompleted);
        clock.Advance(DashboardViewModel.CheckPatience);
        await refresh.WaitAsync(Patience);
        Assert.Equal(["Web: read 4 repositories in"], PassLines(log));
        Assert.True(row.IsReading);

        // The tool closes: the row takes its summary and reads it.
        finish.SetResult();
        await tool.WaitAsync(Patience);
        await DashboardTestSupport.WaitUntilAsync(() => !row.IsReading && row.HasRepository, "the row's read after the tool");
        Assert.Equal(reads + 1, api.RefsReads);
        Assert.Contains(row.Repository!, services.OpenRepositories.Summaries);
    }

    [AvaloniaFact]
    public async Task A_summary_a_tab_adopts_during_the_rows_check_is_not_read_by_the_dashboard()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var gate = new ManualResetEventSlim();
        var holding = false;
        var heldCalls = 0;
        bool Exists(string path)
        {
            if (WorkspacePaths.Same(path, api.WorkingDirectory) && Volatile.Read(ref holding))
            {
                Interlocked.Increment(ref heldCalls);
                gate.Wait(TimeSpan.FromMinutes(2));
            }

            return Directory.Exists(path);
        }

        var (dashboard, log, _) = Dashboard(provider, [api.WorkingDirectory], exists: Exists);
        var services = ServicesOf(dashboard);
        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);
        var row = dashboard.Rows[0];
        var summary = row.Repository!;
        Assert.False(row.FollowsTab);
        try
        {
            // F5: the row's own view model is checked first; during the check a tab adopts it (3.3).
            Volatile.Write(ref holding, true);
            var refresh = dashboard.RefreshCommand.ExecuteAsync(null);
            await DashboardTestSupport.WaitUntilAsync(() => Volatile.Read(ref heldCalls) == 1, "the row's check");
            var key = OpenRepositories.KeyOf(summary);
            Assert.Same(summary, services.OpenRepositories.TryAdopt(key));
            summary.Promote(new System.Diagnostics.Stopwatch());
            services.OpenRepositories.RegisterTab(summary);
            Assert.True(row.FollowsTab);
            var reads = (api.RefsReads, api.StatusReads);

            gate.Set();
            await refresh.WaitAsync(Patience);
            await DashboardTestSupport.SettleAsync();

            // The followed view model was not read by the dashboard, and the pass read nothing.
            Assert.Equal(reads, (api.RefsReads, api.StatusReads));
            Assert.Single(PassLines(log));
            summary.Dispose();
        }
        finally
        {
            gate.Set();
        }
    }

    [AvaloniaFact]
    public async Task Locate_onto_the_same_folder_reads_the_row_again()
    {
        using var provider = new FakeProvider();
        var plain = provider.PathOf("api");
        Directory.CreateDirectory(plain);
        var log = new RecordingLog();
        var folders = new FakeFolderPicker();
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(plain), new ManualTimeProvider(), log, folders: folders);
        var dashboard = new DashboardViewModel(services.Workspaces.Current.Find("web")!, services, () => true);
        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);
        var row = dashboard.Rows[0];
        Assert.Equal("Not a repository", row.State.Text);

        // The folder is a repository now (say, safe.directory was fixed): Locate… on the same folder.
        provider.Add("api");
        folders.Answer(plain);
        await dashboard.LocateAsync(row);
        await WaitForPassesAsync(log, 2);

        Assert.Equal(plain, Assert.Single(services.Workspaces.Current.Find("web")!.Repositories).Path);
        Assert.Same(row, Assert.Single(dashboard.Rows));
        Assert.True(row.HasRepository);
        Assert.Equal(("main", string.Empty), (row.BranchText, row.State.Text));
        Assert.Equal(["Web: read 1 repository in", "Web: read 1 repository in"], PassLines(log));
        dashboard.Dispose();
    }

    [AvaloniaFact]
    public async Task Refresh_stays_enabled_during_its_own_pass_and_folds_into_one_more()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        api.RefsGate = new TaskCompletionSource();
        var (dashboard, log, _) = Dashboard(provider, [api.WorkingDirectory]);

        var first = dashboard.RefreshCommand.ExecuteAsync(null);
        await DashboardTestSupport.WaitUntilAsync(() => api.RefsReads == 1, "the first read");
        Assert.True(dashboard.IsPassRunning);
        Assert.True(dashboard.RefreshCommand.CanExecute(null));
        var second = dashboard.RefreshCommand.ExecuteAsync(null);
        var third = dashboard.RefreshCommand.ExecuteAsync(null);

        api.RefsGate.SetResult();
        await Task.WhenAll(first, second, third).WaitAsync(Patience);
        Assert.Equal(["Web: read 1 repository in", "Web: read 1 repository in"], PassLines(log));
        Assert.Equal(2, api.RefsReads);
    }

    /// <summary>
    /// A request that folds into "one more pass" judges the 10 s rule as of the moment it was made
    /// (2.10, the lead's ruling of 2026-10-23): the window's activation during a first pass of ten
    /// rows that takes over 10 s by the read clock reads none of them again, so the start logs one
    /// line, <c>read 10</c>, however long that pass takes. The tenth row's two git calls take 6 s
    /// and 5 s, so that neither runs past the git call's patience (2.10, the ruling at status step
    /// 22), which would end the pass without the row.
    /// </summary>
    [AvaloniaFact]
    public async Task An_activation_during_a_pass_of_over_ten_seconds_reads_no_row_that_pass_read_again()
    {
        using var provider = new FakeProvider();
        string[] names = ["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch", "notes", "archive"];
        var repositories = names.Select(name => provider.Add(name)).ToList();
        var held = repositories[^1];
        held.OpenGate = new TaskCompletionSource();
        held.RefsGate = new TaskCompletionSource();
        var (dashboard, log, clock) = Dashboard(provider, [.. repositories.Select(repository => repository.WorkingDirectory)]);

        var start = dashboard.ReadAtStartAsync();
        await DashboardTestSupport.WaitUntilAsync(
            () => dashboard.Rows.Take(9).All(row => row.LastRead is not null) && provider.Opens == 10,
            "nine rows read and the tenth held in its probe");
        Assert.True(dashboard.IsPassRunning);

        // The activation folds into one more pass; the tenth row's probe ends 6 s later by the read
        // clock and its summary read 5 s after that: the first pass ends 11 s after the request.
        dashboard.OnWindowActivated();
        clock.Advance(TimeSpan.FromSeconds(6));
        held.OpenGate.SetResult();
        await DashboardTestSupport.WaitUntilAsync(() => held.RefsReads == 1, "the tenth row's summary read");
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.True(dashboard.IsPassRunning);
        held.RefsGate.SetResult();
        await start.WaitAsync(Patience);
        await DashboardTestSupport.SettleAsync();

        Assert.False(dashboard.IsPassRunning);
        Assert.DoesNotContain(dashboard.Rows, row => row.IsReading);
        Assert.Equal(["Web: read 10 repositories in"], PassLines(log));
        Assert.All(repositories, repository => Assert.Equal(1, repository.RefsReads));
    }

    /// <summary>
    /// A folded request still reads a row that was 10 s old or more when it was made (2.10): api,
    /// read 11 s before the window's activation, is read once a pass of docs alone, held meanwhile,
    /// has ended; docs, read by that pass after the request, is not read again.
    /// </summary>
    [AvaloniaFact]
    public async Task A_folded_request_reads_a_row_that_was_eleven_seconds_old_when_it_was_made()
    {
        using var provider = new FakeProvider();
        var (api, docs) = (provider.Add("api"), provider.Add("docs"));
        var (dashboard, log, clock) = Dashboard(provider, [api.WorkingDirectory]);
        var store = (InMemoryWorkspaceStore)Store(dashboard);
        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);

        // docs joins 5 s later and is read at once (trigger 6), held in its refs read.
        clock.Advance(TimeSpan.FromSeconds(5));
        docs.RefsGate = new TaskCompletionSource();
        store.Update(list => WorkspaceEdits.Append(list, "web", [docs.WorkingDirectory]));
        await DashboardTestSupport.WaitUntilAsync(() => docs.RefsReads == 1, "docs' read held");
        Assert.True(dashboard.IsPassRunning);

        // 11 s after api's read the window is activated: the request folds in, api stale by then.
        clock.Advance(TimeSpan.FromSeconds(6));
        dashboard.OnWindowActivated();
        docs.RefsGate.SetResult();
        await WaitForPassesAsync(log, 3);
        await DashboardTestSupport.WaitUntilAsync(() => !dashboard.IsPassRunning, "the folded pass to end");

        Assert.Equal(["Web: read 1 repository in", "Web: read 1 repository in", "Web: read 1 repository in"], PassLines(log));
        Assert.Equal((2, 1), (api.RefsReads, docs.RefsReads));
    }

    [AvaloniaFact]
    public async Task A_rows_deleted_git_folder_releases_its_summary_and_the_folder_is_probed_again()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var (dashboard, log, _) = Dashboard(provider, [api.WorkingDirectory]);
        var services = ServicesOf(dashboard);
        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);
        var summary = Assert.Single(services.OpenRepositories.Summaries);
        Assert.Same(summary, dashboard.Rows[0].Repository);

        Directory.Delete(api.GitDirectory, recursive: true);
        provider.Forget("api");
        await dashboard.RefreshCommand.ExecuteAsync(null);

        Assert.True(summary.IsClosed);
        Assert.Empty(services.OpenRepositories.Summaries);
        Assert.Null(dashboard.Rows[0].Repository);
        Assert.Equal(("Not a repository", $"{api.WorkingDirectory} is not a git repository"), (dashboard.Rows[0].State.Text, dashboard.Rows[0].State.ToolTip));
        Assert.Equal(1, api.RefsReads);
    }

    [AvaloniaFact]
    public async Task The_probes_states_without_git_for_a_gone_folder_and_without_reading_an_outer_repository()
    {
        using var provider = new FakeProvider();
        var gone = provider.PathOf("old-site");
        var plain = provider.PathOf("plain");
        Directory.CreateDirectory(plain);
        var outer = provider.Add("outer");
        var inner = provider.PathOf(Path.Combine("outer", "inner"));
        var aroundInner = provider.AddInside(Path.Combine("outer", "inner"), outer);
        provider.Refuse("dubious", new GitException("git rev-parse", 128, "fatal: detected dubious ownership in repository at 'x'\nmore"));
        var (dashboard, log, _) = Dashboard(provider, [gone, plain, inner, provider.PathOf("dubious")]);

        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);

        Assert.Equal(["Web: read 4 repositories in"], PassLines(log));
        var states = dashboard.Rows.Select(row => (row.State.Text, row.State.ToolTip)).ToList();
        Assert.Equal(("Folder not found", $"{gone} does not exist"), states[0]);
        Assert.Equal(("Not a repository", $"{plain} is not a git repository"), states[1]);
        Assert.Equal(("Not a repository", $"{inner} is not a git repository of its own: it lies inside {outer.WorkingDirectory}"), states[2]);
        Assert.Equal(("Git could not open it", $"Git could not open {provider.PathOf("dubious")}: fatal: detected dubious ownership in repository at 'x'"), states[3]);

        // Three opens (the gone folder needs no git), and no read of the repository found around the inner folder.
        Assert.Equal(3, provider.Opens);
        Assert.Equal((0, 0, 0, 0), (aroundInner.RefsReads, aroundInner.StatusReads, outer.RefsReads, outer.StatusReads));
        Assert.Empty(ServicesOf(dashboard).OpenRepositories.Summaries);
        Assert.All(dashboard.Rows, row => Assert.Equal(string.Empty, row.BranchText));
    }

    [AvaloniaFact]
    public async Task A_folder_check_that_does_not_return_holds_nothing_and_runs_off_the_ui_thread()
    {
        using var provider = new FakeProvider();
        var (api, docs) = (provider.Add("api"), provider.Add("docs"));
        var held = provider.PathOf("share");
        var gate = new ManualResetEventSlim();
        var heldCalls = 0;
        var onUiThread = false;
        bool Exists(string path)
        {
            onUiThread |= Dispatcher.UIThread.CheckAccess();
            if (WorkspacePaths.Same(path, held))
            {
                Interlocked.Increment(ref heldCalls);
                gate.Wait(TimeSpan.FromMinutes(2));
                return false;
            }

            return Directory.Exists(path);
        }

        var (dashboard, log, clock) = Dashboard(provider, [api.WorkingDirectory, held, docs.WorkingDirectory], exists: Exists);
        try
        {
            dashboard.OnBecameActive();
            await DashboardTestSupport.WaitUntilAsync(() => !dashboard.Rows[0].IsReading && !dashboard.Rows[2].IsReading, "api and docs read");

            // Only the check that has not returned is left: the pass waits CheckPatience by the
            // read clock, then ends without it and logs the two rows that finished.
            await DashboardTestSupport.SettleAsync();
            Assert.True(dashboard.IsPassRunning);
            clock.Advance(DashboardViewModel.CheckPatience);
            await WaitForPassesAsync(log, 1);
            Assert.Equal(["Web: read 2 repositories in"], PassLines(log));
            var row = dashboard.Rows[1];
            Assert.True(row.IsReading);
            Assert.Equal(DashboardTexts.ReadingText, row.State.Text);
            Assert.Equal(1, Volatile.Read(ref heldCalls));

            // F5 and the start's read complete while it is held; neither starts a second check.
            await dashboard.RefreshCommand.ExecuteAsync(null).WaitAsync(Patience);
            await dashboard.ReadAtStartAsync().WaitAsync(Patience);
            clock.Advance(TimeSpan.FromSeconds(10));
            dashboard.OnBecameActive();
            await WaitForPassesAsync(log, 3);
            await DashboardTestSupport.SettleAsync();
            Assert.Equal(1, Volatile.Read(ref heldCalls));

            gate.Set();
            await DashboardTestSupport.WaitUntilAsync(() => !row.IsReading, "the held check to return");
            Assert.Equal("Folder not found", row.State.Text);
            Assert.False(onUiThread);
        }
        finally
        {
            gate.Set();
        }
    }

    [AvaloniaFact]
    public async Task A_git_read_still_running_after_ten_seconds_no_longer_holds_its_pass()
    {
        // The lead's ruling at status step 22 (2.10): a share that answered its folder check and
        // then stopped answering git keeps its slot, and F5's pass ends and logs without it.
        using var provider = new FakeProvider();
        var (api, share, docs) = (provider.Add("api"), provider.Add("share"), provider.Add("docs"));
        var (dashboard, log, clock) = Dashboard(provider, [api.WorkingDirectory, share.WorkingDirectory, docs.WorkingDirectory]);
        dashboard.OnBecameActive();
        await WaitForPassesAsync(log, 1);
        var row = dashboard.Rows[1];
        Assert.Equal(0, clock.ArmedTimers);

        share.StatusGate = new TaskCompletionSource();
        var refresh = dashboard.RefreshCommand.ExecuteAsync(null);
        await DashboardTestSupport.WaitUntilAsync(
            () => share.StatusReads == 2 && api.StatusReads == 2 && docs.StatusReads == 2 && !dashboard.Rows[0].IsReading && !dashboard.Rows[2].IsReading,
            "api and docs read and share's status read held");

        // The held call's patience is armed on the read clock, and nothing else holds the pass.
        await DashboardTestSupport.WaitUntilAsync(() => clock.ArmedTimers == 1, "the held call's patience");
        Assert.False(refresh.IsCompleted);
        Assert.True(row.IsReading);
        clock.Advance(DashboardViewModel.GitCallPatience - TimeSpan.FromTicks(1));
        Assert.Equal(1, clock.ArmedTimers);
        Assert.False(refresh.IsCompleted);

        clock.Advance(TimeSpan.FromTicks(1));
        await refresh.WaitAsync(Patience);
        Assert.Equal(["Web: read 3 repositories in", "Web: read 2 repositories in"], PassLines(log));
        Assert.True(row.IsReading);
        Assert.False(dashboard.IsPassRunning);

        // F5 again: the held row starts no second read, the others are read.
        await dashboard.RefreshCommand.ExecuteAsync(null).WaitAsync(Patience);
        Assert.Equal(["Web: read 3 repositories in", "Web: read 2 repositories in", "Web: read 2 repositories in"], PassLines(log));
        Assert.Equal((3, 2), (api.StatusReads, share.StatusReads));

        // The call ends: the row's read completes then, logging no pass of its own.
        share.Status = WorkingTreeStatus.Clean;
        share.StatusGate.SetResult();
        await DashboardTestSupport.WaitUntilAsync(() => !row.IsReading, "the held read to end");
        Assert.Equal(("main", "Clean", string.Empty), (row.BranchText, row.Changes.Text, row.State.Text));
        Assert.NotNull(row.LastRead);
        Assert.Equal(3, PassLines(log).Count);
    }

    [AvaloniaFact]
    public async Task Closing_the_dashboard_while_a_probe_is_held_leaves_no_summary_and_no_git_call()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        provider.Gate = new TaskCompletionSource();
        var (dashboard, _, _) = Dashboard(provider, [api.WorkingDirectory]);
        var services = ServicesOf(dashboard);
        dashboard.OnBecameActive();
        await DashboardTestSupport.WaitUntilAsync(() => provider.Opens == 1, "the probe's open");

        dashboard.Dispose();
        provider.Gate.SetResult();
        await DashboardTestSupport.SettleAsync();

        Assert.Empty(services.OpenRepositories.Summaries);
        Assert.Equal((0, 0), (api.RefsReads, api.StatusReads));
    }

    [AvaloniaFact]
    public async Task Removing_a_row_or_locating_it_elsewhere_while_its_probe_is_held_leaves_no_summary_and_no_git_call()
    {
        using var provider = new FakeProvider();
        var (api, docs, other) = (provider.Add("api"), provider.Add("docs"), provider.Add("other"));
        provider.Gate = new TaskCompletionSource();
        var (dashboard, log, _) = Dashboard(provider, [api.WorkingDirectory, docs.WorkingDirectory]);
        var services = ServicesOf(dashboard);
        dashboard.OnBecameActive();
        await DashboardTestSupport.WaitUntilAsync(() => provider.Opens == 2, "the probes' opens");

        Store(dashboard).Update(list => WorkspaceEdits.Remove(list, "web", api.WorkingDirectory));
        Store(dashboard).Update(list => WorkspaceEdits.Replace(list, "web", docs.WorkingDirectory, other.WorkingDirectory));
        provider.Gate.SetResult();
        await WaitForPassesAsync(log, 1);
        await DashboardTestSupport.WaitUntilAsync(() => other.RefsReads == 1, "the located folder's read");
        await DashboardTestSupport.SettleAsync();

        Assert.Equal((0, 0, 0, 0), (api.RefsReads, api.StatusReads, docs.RefsReads, docs.StatusReads));
        var summary = Assert.Single(services.OpenRepositories.Summaries);
        Assert.Equal("other", summary.Name);
        Assert.Same(summary, Assert.Single(dashboard.Rows).Repository);
    }

    [AvaloniaFact]
    public async Task A_closed_dashboard_reacts_to_nothing()
    {
        using var provider = new FakeProvider();
        var (api, docs) = (provider.Add("api"), provider.Add("docs"));
        var (dashboard, log, _) = Dashboard(provider, [api.WorkingDirectory]);
        var services = ServicesOf(dashboard);
        using var apiTab = Tab(services, api);
        await apiTab.StartAsync();
        var row = dashboard.Rows[0];
        Assert.True(row.FollowsTab);
        var shownRefs = row.Refs;

        dashboard.Dispose();

        Store(dashboard).Update(list => WorkspaceEdits.Append(list, "web", [docs.WorkingDirectory]));
        Assert.Single(dashboard.Rows);
        using var docsTab = Tab(services, docs);
        services.OpenRepositories.UnregisterTab(apiTab);
        services.OpenRepositories.RegisterTab(apiTab);
        api.Refs = DashboardTestSupport.MainRefs(ahead: 5);
        await apiTab.RefreshAsync();
        await DashboardTestSupport.SettleAsync();

        Assert.Same(shownRefs, row.Refs);
        Assert.Null(row.Repository);
        Assert.Empty(PassLines(log));
        Assert.Equal(0, docs.RefsReads);
    }

    private static (DashboardViewModel Dashboard, RecordingLog Log, ManualTimeProvider Clock) Dashboard(
        FakeProvider provider,
        string[] folders,
        bool active = true,
        Func<string, bool>? exists = null)
    {
        var services = Services(provider, folders, out var log, out var clock);
        var dashboard = new DashboardViewModel(services.Workspaces.Current.Find("web")!, services, () => active, exists);
        Holders.AddOrUpdate(dashboard, services);
        return (dashboard, log, clock);
    }

    private static TabServices Services(FakeProvider provider, string[] folders, out RecordingLog log, out ManualTimeProvider clock)
    {
        log = new RecordingLog();
        clock = new ManualTimeProvider();
        return DashboardTestSupport.Services(provider, DashboardTestSupport.Store(folders), clock, log);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DashboardViewModel, TabServices> Holders = [];

    private static TabServices ServicesOf(DashboardViewModel dashboard) => Holders.TryGetValue(dashboard, out var services) ? services : throw new InvalidOperationException();

    private static IWorkspaceStore Store(DashboardViewModel dashboard) => ServicesOf(dashboard).Workspaces;

    /// <summary>A tab's full view model on <paramref name="repository"/>, registered as a tab registers it.</summary>
    private static RepositoryViewModel Tab(TabServices services, IGitRepository repository)
    {
        var tab = new RepositoryViewModel(repository, services.Settings, services.Dates, services.Log, dialogs: services.Dialogs, services: services);
        services.OpenRepositories.RegisterTab(tab);
        return tab;
    }

    private static List<string> PassLines(RecordingLog log) =>
        [.. log.Messages.Where(message => message.StartsWith("Web: read ", StringComparison.Ordinal)).Select(message => message[..message.IndexOf(" in ", StringComparison.Ordinal)] + " in")];

    private static Task WaitForPassesAsync(RecordingLog log, int passes) =>
        DashboardTestSupport.WaitUntilAsync(() => PassLines(log).Count >= passes, $"{passes} read passes");
}
