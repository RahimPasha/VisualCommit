using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// When a dashboard's rows are read (docs/phase-6-design.md 2.10, D207; WD35, WD37): each trigger
/// with the 10 s rule by the read clock, rows that follow tabs never read, four reads at once, the
/// folder checks off the UI thread that hold nothing, the probe's states, the row's token, and a
/// closed dashboard that reacts to nothing.
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
        using var provider = new FakeProvider();
        var meter = new ReadMeter { Gate = new TaskCompletionSource() };
        var folders = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            var repository = provider.Add($"repo{i}");
            repository.Meter = meter;
            folders.Add(repository.WorkingDirectory);
        }

        var (dashboard, log, _) = Dashboard(provider, [.. folders]);
        dashboard.OnBecameActive();

        await DashboardTestSupport.WaitUntilAsync(() => meter.Now == DashboardViewModel.MaxReads, "four reads held");
        await DashboardTestSupport.SettleAsync();
        Assert.Equal((4, 4), (meter.Entered, meter.Now));
        Assert.Equal(6, dashboard.Rows.Count(row => row.IsReading));

        meter.Gate.SetResult();
        await WaitForPassesAsync(log, 1);
        Assert.Equal(["Web: read 6 repositories in"], PassLines(log));
        Assert.Equal((6, 4), (meter.Entered, meter.Max));
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
