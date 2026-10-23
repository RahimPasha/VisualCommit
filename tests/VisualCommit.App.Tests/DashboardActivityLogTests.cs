using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Workspaces;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The activity log on a dashboard tab (docs/phase-6-design.md 2.9, D208): it follows the selected
/// row's worktree, its records keyed by worktree whichever queue ran them, and with nothing to
/// follow its toggle is disabled with the dashboard's reason, the first that holds.
/// </summary>
public class DashboardActivityLogTests
{
    [AvaloniaFact]
    public async Task The_log_follows_the_selected_rows_worktree_and_hides_with_no_row()
    {
        using var provider = new FakeProvider();
        var (api, docs) = (provider.Add("api"), provider.Add("docs"));
        var (window, dashboard) = await OpenAsync(provider, api.WorkingDirectory, docs.WorkingDirectory);
        var log = window.ActivityLog;
        log.Open();
        Assert.True(log.IsOpen);
        Assert.Null(log.Current);
        Assert.False(log.IsShown);
        Assert.False(log.CanToggle);

        dashboard.SelectedRow = dashboard.Rows[1];

        Assert.True(log.IsShown);
        Assert.Equal("docs", log.RepositoryName);
        Assert.Equal(dashboard.Rows[1].Repository!.Path, log.Current!.Source.WorkingTree);
        Assert.Equal(ShellToolTips.HideActivityLog, log.ToggleToolTip);

        dashboard.SelectedRow = dashboard.Rows[0];
        Assert.Equal("api", log.RepositoryName);

        dashboard.SelectedRow = null;
        Assert.False(log.IsShown);
        Assert.True(log.IsOpen);
        Assert.Equal(DashboardTexts.SelectRepositoryText, log.ToggleToolTip);
    }

    [AvaloniaFact]
    public async Task A_dashboards_operation_is_in_the_records_of_the_tab_opened_afterwards()
    {
        using var provider = new FakeProvider();
        var docs = provider.Add("docs");
        var (window, dashboard) = await OpenAsync(provider, docs.WorkingDirectory);
        var row = dashboard.Rows[0];
        await row.Repository!.Queue.RunAsync(new GitOperation("Pull", "Pulling origin/main into main…", OperationKind.Network, RefreshScope.None, _ =>
            Task.FromResult(OperationReport.Done("Pulled 4 commits into main (fast-forward)"))));

        dashboard.OpenRow(row);
        await DashboardTestSupport.WaitUntilAsync(() => window.ActiveTab.Repository is not null, "docs' tab");
        window.ActivityLog.Open();

        Assert.Equal("docs", window.ActivityLog.RepositoryName);
        var pull = Assert.Single(window.ActivityLog.Current!.Rows.OfType<ActivityLogOperationRow>());
        Assert.Equal("Pull", pull.Title);
    }

    [AvaloniaFact]
    public async Task The_toggles_reasons_come_in_their_order()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var gone = provider.PathOf("old-site");

        // 1. A workspace without rows.
        var empty = DashboardTestSupport.Services(provider, DashboardTestSupport.Store());
        var emptyWindow = DashboardTestSupport.Window(empty);
        empty.WorkspaceHost!.ShowWorkspace("web", emptyWindow.ActiveTab);
        Assert.Equal("Web has no repositories", emptyWindow.ActivityLog.ToggleToolTip);
        Assert.False(emptyWindow.ActivityLog.CanToggle);

        var (window, dashboard) = await OpenAsync(provider, api.WorkingDirectory, gone);
        var log = window.ActivityLog;

        // 2. No row selected.
        Assert.Equal("Select a repository to see its activity", log.ToggleToolTip);

        // 4. The selected row is in a folder state: its State tooltip.
        dashboard.SelectedRow = dashboard.Rows[1];
        Assert.Equal($"{gone} does not exist", log.ToggleToolTip);
        Assert.False(log.CanToggle);

        // Elsewhere the toggle's own texts.
        dashboard.SelectedRow = dashboard.Rows[0];
        Assert.True(log.CanToggle);
        Assert.Equal(ShellToolTips.ShowActivityLog, log.ToggleToolTip);
    }

    [AvaloniaFact]
    public async Task Waiting_for_a_row_to_be_read_turns_into_an_enabled_toggle_when_its_view_model_arrives()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        provider.Gate = new TaskCompletionSource();
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(api.WorkingDirectory));
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var dashboard = window.ActiveTab.Dashboard!;
        dashboard.SelectedRow = dashboard.Rows[0];

        // 3. The selected row is not read yet.
        Assert.Equal("Wait for api to be read", window.ActivityLog.ToggleToolTip);
        Assert.False(window.ActivityLog.CanToggle);

        provider.Gate.SetResult();
        await DashboardTestSupport.WaitUntilAsync(() => window.ActivityLog.CanToggle, "the row's view model");
        Assert.Equal(ShellToolTips.ShowActivityLog, window.ActivityLog.ToggleToolTip);
        Assert.Equal("api", window.ActivityLog.RepositoryName);
    }

    [AvaloniaFact]
    public async Task Reasons_three_and_four_follow_one_row_and_no_selection_comes_before_both()
    {
        // 2.9's order: (2) no row selected, before what any row is; (3) the selected row not read
        // yet; (4) its folder state, once its read found one. api is held in its probe, then its
        // folder turns out to be no repository; old-site is gone from the start.
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var gone = provider.PathOf("old-site");
        provider.Gate = new TaskCompletionSource();
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(api.WorkingDirectory, gone));
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var dashboard = window.ActiveTab.Dashboard!;
        var log = window.ActivityLog;
        await DashboardTestSupport.WaitUntilAsync(() => dashboard.Rows[1].FolderState is not null && provider.Opens == 1, "old-site's state and api's probe");

        Assert.Equal(DashboardTexts.SelectRepositoryText, log.ToggleToolTip);

        dashboard.SelectedRow = dashboard.Rows[0];
        Assert.Equal("Wait for api to be read", log.ToggleToolTip);
        dashboard.SelectedRow = dashboard.Rows[1];
        Assert.Equal($"{gone} does not exist", log.ToggleToolTip);
        dashboard.SelectedRow = null;
        Assert.Equal(DashboardTexts.SelectRepositoryText, log.ToggleToolTip);

        // The selected row goes from reason 3 to reason 4 when its read ends in a folder state.
        dashboard.SelectedRow = dashboard.Rows[0];
        provider.Forget("api");
        provider.Gate.SetResult();
        await DashboardTestSupport.WaitUntilAsync(() => dashboard.Rows[0].FolderState is not null, "api's read");
        Assert.Equal($"{api.WorkingDirectory} is not a git repository", log.ToggleToolTip);
        Assert.False(log.CanToggle);
    }

    [AvaloniaFact]
    public void On_an_empty_tab_the_toggle_keeps_its_own_text()
    {
        using var provider = new FakeProvider();
        var window = DashboardTestSupport.Window(DashboardTestSupport.Services(provider, DashboardTestSupport.Store()));

        Assert.Equal(ShellToolTips.ShowActivityLog, window.ActivityLog.ToggleToolTip);
        Assert.False(window.ActivityLog.CanToggle);
    }

    private static async Task<(MainWindowViewModel Window, DashboardViewModel Dashboard)> OpenAsync(FakeProvider provider, params string[] folders)
    {
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(folders));
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var dashboard = window.ActiveTab.Dashboard!;
        await DashboardTestSupport.WaitUntilAsync(() => !dashboard.IsPassRunning && dashboard.Rows.All(row => !row.IsReading && !row.IsNotReadYet), "the dashboard's first read");
        return (window, dashboard);
    }
}
