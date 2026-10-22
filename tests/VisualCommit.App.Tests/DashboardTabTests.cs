using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The dashboard tab in the window (docs/phase-6-design.md 2.1 to 2.3, 2.8; D205): opening a
/// workspace, opening a row's repository beside it, closing, a deleted or renamed workspace, and
/// the toolbar and the status bar a dashboard tab gives.
/// </summary>
public class DashboardTabTests
{
    [AvaloniaFact]
    public async Task The_welcome_pages_empty_tab_becomes_the_dashboard()
    {
        using var provider = new FakeProvider();
        var store = DashboardTestSupport.Store(provider.Add("api").WorkingDirectory);
        var services = DashboardTestSupport.Services(provider, store);
        var window = DashboardTestSupport.Window(services);
        var welcome = window.ActiveTab;

        services.WorkspaceHost!.ShowWorkspace("web", welcome);

        var tab = Assert.Single(window.Tabs);
        Assert.Same(welcome, tab);
        Assert.Same(tab, window.ActiveTab);
        Assert.True(tab.IsDashboard);
        Assert.Equal(("Web", "Web", "web"), (tab.Title, tab.BranchText, tab.SavedWorkspaceId));
        Assert.Equal(("DashboardTab", false, false, true, 0, 3), (tab.TabAutomationId, tab.ShowsWelcome, tab.ShowsLeftPanel, tab.ShowsDashboard, tab.GraphColumn, tab.GraphColumnSpan));
        Assert.False(tab.ShowsRightPanel || tab.ShowsDetailsPanel || tab.ShowsStagePanel);
        Assert.True(tab.CanClose);
        Assert.Single(services.Session.Current.Tabs);
        await DashboardTestSupport.WaitUntilAsync(() => tab.Dashboard!.Rows[0].HasRepository, "the row's first read");
    }

    [AvaloniaFact]
    public void An_open_dashboard_is_activated_and_no_tab_is_made()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store());
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        window.NewTabCommand.Execute(null);
        var newTab = window.ActiveTab;

        // From the welcome page of the new tab, the workspace's command, and the palette's.
        services.WorkspaceCommands.Open("web", newTab);

        Assert.Equal(2, window.Tabs.Count);
        Assert.True(window.ActiveTab.IsDashboard);
        Assert.Same(newTab, window.Tabs[1]);
        Assert.False(newTab.IsDashboard);
    }

    [AvaloniaFact]
    public async Task A_busy_tab_is_left_alone_and_a_dashboard_tab_goes_after_the_open_dashboards()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var store = new InMemoryWorkspaceStore(new WorkspaceList
        {
            Workspaces =
            [
                new Workspace("web", "Web", []),
                new Workspace("tools", "Tools", []),
            ],
        });
        var services = DashboardTestSupport.Services(provider, store);
        var window = DashboardTestSupport.Window(services);
        var repoTab = window.ActiveTab;
        Assert.True(await repoTab.OpenAsync(api.WorkingDirectory));

        services.WorkspaceHost!.ShowWorkspace("web", repoTab);
        services.WorkspaceHost.ShowWorkspace("tools", window.ActiveTab);

        Assert.Equal(["Web", "Tools", "api"], window.Tabs.Select(tab => tab.Title));
        Assert.Same(window.Tabs[1], window.ActiveTab);
        Assert.False(repoTab.IsDashboard);

        // A workspace the store does not hold opens nothing.
        services.WorkspaceHost.ShowWorkspace("gone", window.ActiveTab);
        Assert.Equal(3, window.Tabs.Count);
    }

    [AvaloniaFact]
    public async Task A_tab_whose_restore_failed_counts_as_empty_and_loses_its_folder()
    {
        using var provider = new FakeProvider();
        var missing = provider.PathOf("not-mounted");
        var session = new DashboardTestSupport.MemorySession(new SessionState { Tabs = [new TabState(missing)] });
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(), session: session);
        var window = DashboardTestSupport.Window(services, session);
        await window.InitializeAsync(TestContext.Current.CancellationToken);
        var failed = window.ActiveTab;
        Assert.Equal(missing, failed.SavedPath);

        services.WorkspaceHost!.ShowWorkspace("web", failed);

        Assert.Same(failed, Assert.Single(window.Tabs));
        Assert.True(failed.IsDashboard);
        Assert.Null(failed.SavedPath);
    }

    [AvaloniaFact]
    public async Task Rows_open_beside_their_dashboard_in_the_order_of_the_example()
    {
        // 2.3's example: in [Web, Tools, api], api opened from Tools, opening web-app from Tools
        // gives [Web, Tools, api, web-app], then docs from Web gives [Web, Tools, docs, api, web-app].
        using var provider = new FakeProvider();
        var (api, webApp, docs) = (provider.Add("api"), provider.Add("web-app"), provider.Add("docs"));
        var store = new InMemoryWorkspaceStore(new WorkspaceList
        {
            Workspaces =
            [
                new Workspace("web", "Web", [new WorkspaceRepository(docs.WorkingDirectory)]),
                new Workspace("tools", "Tools", [new WorkspaceRepository(api.WorkingDirectory), new WorkspaceRepository(webApp.WorkingDirectory)]),
            ],
        });
        var services = DashboardTestSupport.Services(provider, store);
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var web = window.ActiveTab.Dashboard!;
        await DashboardTestSupport.WaitUntilAsync(() => web.Rows.All(row => row.HasRepository), "Web's rows");
        services.WorkspaceHost.ShowWorkspace("tools", window.ActiveTab);
        var tools = window.ActiveTab.Dashboard!;
        await DashboardTestSupport.WaitUntilAsync(() => tools.Rows.All(row => row.HasRepository), "Tools' rows");

        tools.OpenRow(tools.Rows[0]);
        await DashboardTestSupport.WaitUntilAsync(() => window.ActiveTab.Repository is not null, "api's tab");
        Assert.Equal(["Web", "Tools", "api"], window.Tabs.Select(tab => tab.Title));

        // The tab adopted the row's summary-mode view model: one queue for the row and the tab.
        Assert.Same(window.ActiveTab.Repository, tools.Rows[0].Repository);
        Assert.False(window.ActiveTab.Repository!.IsSummary);
        Assert.True(tools.Rows[0].FollowsTab);
        await DashboardTestSupport.WaitUntilAsync(() => api.HistoryLoads == 1, "api's history, loaded by the promoted view model");

        tools.OpenRow(tools.Rows[1]);
        await DashboardTestSupport.WaitUntilAsync(() => window.ActiveTab.Title == "web-app" && window.ActiveTab.Repository is not null, "web-app's tab");
        Assert.Equal(["Web", "Tools", "api", "web-app"], window.Tabs.Select(tab => tab.Title));

        web.OpenRow(web.Rows[0]);
        await DashboardTestSupport.WaitUntilAsync(() => window.ActiveTab.Title == "docs" && window.ActiveTab.Repository is not null, "docs' tab");
        Assert.Equal(["Web", "Tools", "docs", "api", "web-app"], window.Tabs.Select(tab => tab.Title));

        // Opening a row whose worktree a tab shows activates that tab, the leftmost.
        tools.OpenRow(tools.Rows[0]);
        Assert.Equal("api", window.ActiveTab.Title);
        Assert.Equal(5, window.Tabs.Count);

        // Closing a dashboard closes only it: the tabs opened from it stay.
        window.CloseTabCommand.Execute(window.Tabs[1]);
        Assert.True(tools.IsClosed);
        Assert.Equal(["Web", "docs", "api", "web-app"], window.Tabs.Select(tab => tab.Title));
        await DashboardTestSupport.SettleAsync();
        Assert.Equal(1, api.HistoryLoads);
    }

    [AvaloniaFact]
    public async Task A_rows_pending_tab_is_found_by_the_same_folder_and_activated()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var spelled = api.WorkingDirectory + Path.DirectorySeparatorChar;
        var session = new DashboardTestSupport.MemorySession(new SessionState { Tabs = [new TabState(null), new TabState(spelled)], ActiveTab = 0 });
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(api.WorkingDirectory), session: session);
        var window = DashboardTestSupport.Window(services, session);
        var pending = window.Tabs[1];
        Assert.Equal(spelled, pending.PendingPath);

        services.WorkspaceHost!.ShowWorkspace("web", window.Tabs[0]);
        var dashboard = window.ActiveTab.Dashboard!;
        await DashboardTestSupport.WaitUntilAsync(() => dashboard.Rows[0].HasRepository, "api's row");

        dashboard.OpenRow(dashboard.Rows[0]);

        Assert.Same(pending, window.ActiveTab);
        Assert.Equal(2, window.Tabs.Count);
    }

    [AvaloniaFact]
    public async Task A_row_without_a_repository_opens_nothing()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(provider.PathOf("old-site")));
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var dashboard = window.ActiveTab.Dashboard!;
        await DashboardTestSupport.WaitUntilAsync(() => dashboard.Rows[0].FolderState is not null, "old-site's folder check");
        var opened = 0;
        dashboard.OpenRequested += (_, _) => opened++;

        dashboard.OpenRow(dashboard.Rows[0]);

        Assert.Equal(0, opened);
        Assert.Single(window.Tabs);
        Assert.Equal("Folder not found", dashboard.Rows[0].State.Text);
    }

    [AvaloniaFact]
    public void A_lone_dashboard_can_be_closed_and_closing_it_leaves_a_new_tab()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store());
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var dashboard = window.ActiveTab;
        Assert.True(dashboard.CanClose);

        window.CloseTabCommand.Execute(dashboard);

        var fresh = Assert.Single(window.Tabs);
        Assert.False(fresh.IsDashboard);
        Assert.Equal("New tab", fresh.Title);
        Assert.True(dashboard.Dashboard!.IsClosed);
    }

    [AvaloniaFact]
    public void A_renamed_workspace_renames_its_tab_and_a_deleted_one_closes_it()
    {
        using var provider = new FakeProvider();
        var store = DashboardTestSupport.Store();
        var services = DashboardTestSupport.Services(provider, store);
        var window = DashboardTestSupport.Window(services);
        window.NewTabCommand.Execute(null);
        var other = window.ActiveTab;
        services.WorkspaceHost!.ShowWorkspace("web", window.Tabs[0]);
        var tab = window.ActiveTab;

        store.Update(list => WorkspaceEdits.Rename(list, "web", "Web team"));

        Assert.Equal(("Web team", "Web team", "Web team"), (tab.Title, tab.BranchText, tab.Dashboard!.Name));

        store.Update(list => WorkspaceEdits.Delete(list, "web"));

        Assert.Same(other, Assert.Single(window.Tabs));
        Assert.True(tab.Dashboard.IsClosed);
    }

    [AvaloniaFact]
    public async Task A_dashboard_tabs_toolbar_and_status_bar_are_its_runs_and_a_repo_tabs_its_repositorys()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store());
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var dashboardTab = window.ActiveTab;
        window.NewTabCommand.Execute(null);
        var repoTab = window.ActiveTab;
        Assert.True(await repoTab.OpenAsync(api.WorkingDirectory));

        var dashboard = dashboardTab.Dashboard!;
        Assert.Same(dashboard.Toolbar, dashboardTab.Toolbar);
        Assert.Same(dashboard.Bulk, dashboardTab.Toolbar.Dashboard);
        Assert.Same(dashboard.Bulk, dashboardTab.OperationStatus);
        Assert.Equal("Ready", dashboardTab.OperationStatus.Text);
        Assert.Null(dashboardTab.Repository);

        Assert.Same(repoTab.Repository!.Toolbar, repoTab.Toolbar);
        Assert.Same(repoTab.Repository.OperationStatus, repoTab.OperationStatus);
        Assert.Equal("main", repoTab.BranchText);
        Assert.Equal(("RepoTab", true, 1, 1), (repoTab.TabAutomationId, repoTab.ShowsLeftPanel, repoTab.GraphColumn, repoTab.GraphColumnSpan));
    }
}
