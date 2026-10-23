using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.App.Views.Shell;
using VisualCommit.App.Views.Workspaces;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The dashboard drawn (docs/phase-6-design.md 2.4 to 2.8, 15.5; D206): the header and the columns
/// where DashboardColumns places them at 1100 and 1920, every text of the "workspace" scenario
/// whole, the side panels hidden and back, the keys and the pointer on the list, the status bar's
/// branch part, a row's Cancel, the empty state, the failed save's notice, and the header's line
/// with a long name during a run.
/// </summary>
public class DashboardViewTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [AvaloniaFact]
    public async Task At_1100_the_ten_rows_lie_in_their_columns_whole_and_the_side_panels_hide()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        using var app = await StartAsync(copy, data, 1100, 700);
        var window = app.Window;
        var mainArea = window.FindControl<Grid>("MainArea")!;
        var (left, right) = (mainArea.ColumnDefinitions[0].ActualWidth, mainArea.ColumnDefinitions[2].ActualWidth);

        // The dashboard spans the main area's three columns; no side panel, no splitter (2.4).
        var root = app.FindByAutomationId("Dashboard");
        Assert.Equal(new Rect(0, 88, 1100, 586), app.BoundsOf(root));
        Assert.Equal("Web", AutomationProperties.GetName(root));
        Assert.False(window.GetVisualDescendants().OfType<LeftPanelView>().Single().IsEffectivelyVisible);
        Assert.False(window.FindControl<GridSplitter>("LeftSplitter")!.IsEffectivelyVisible);
        Assert.False(window.FindControl<GridSplitter>("RightSplitter")!.IsEffectivelyVisible);
        Assert.False(window.GetVisualDescendants().OfType<RightPanelView>().Single().IsEffectivelyVisible);

        // The header (2.5).
        AssertBounds(app, "DashboardHeader", 0, 88, 1100, 36);
        Assert.Equal(1092, app.BoundsOf(app.FindByAutomationId("DashboardMore")).Right, 1);
        Assert.Equal("More", AutomationProperties.GetName(app.FindByAutomationId("DashboardMore")));
        Assert.Equal("More", ToolTip.GetTip(app.FindByAutomationId("DashboardMore")));
        var title = app.FindByAutomationId("DashboardTitle");
        Assert.Equal(28, app.BoundsOf(title).X, 1);
        Assert.Equal("10 repositories", ((TextBlock)app.FindByAutomationId("DashboardCount")).Text);
        var buttons = new[] { "DashboardAddRepository", "DashboardFetchAll", "DashboardPullAll", "DashboardRefresh", "DashboardMore" }
            .Select(id => app.BoundsOf(app.FindByAutomationId(id)))
            .ToList();
        for (var i = 1; i < buttons.Count; i++)
        {
            Assert.Equal(buttons[i - 1].Right + 6, buttons[i].X, 1);
        }

        // The headings and the rows by DashboardColumns (2.6).
        var columns = DashboardColumns.Compute(1100);
        AssertBounds(app, "DashboardColumnHeaders", 0, 124, 1100, 28);
        var headings = app.FindByAutomationId("DashboardColumnHeaders").GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).ToList();
        Assert.Equal(["Repository", "Branch", "Ahead / behind", "Changes", "State", "Operation"], headings.Select(text => text.Text));
        foreach (var (heading, column) in headings.Zip(new[] { DashboardColumn.Repository, DashboardColumn.Branch, DashboardColumn.AheadBehind, DashboardColumn.Changes, DashboardColumn.State, DashboardColumn.Operation }))
        {
            Assert.Equal(columns[column].X + 8, app.BoundsOf(heading).X, 1);
        }

        var snapshot = app.DashboardSnapshot();
        Assert.Equal(TempWorkspace.DashboardOrder, snapshot.Rows.Select(row => row.Name));
        for (var k = 0; k < 10; k++)
        {
            var row = app.DashboardRowControl(snapshot.Rows[k].Name);
            Assert.Equal(new Rect(0, 152 + (26 * k), 1100, 26), app.BoundsOf(row));
            Assert.Equal(columns[DashboardColumn.Repository].X + 8, app.BoundsOf(Cell(row, "DashboardRowName")).X, 1);
            Assert.Equal(columns[DashboardColumn.Branch].X + 8, app.BoundsOf(Cell(row, "DashboardRowBranch")).X, 1);
            Assert.Equal(columns[DashboardColumn.AheadBehind].X + 8, app.BoundsOf(Cell(row, "DashboardRowAheadBehind")).X, 1);
            Assert.Equal(columns[DashboardColumn.Changes].X + 8, app.BoundsOf(Cell(row, "DashboardRowChanges")).X, 1);
            Assert.False(Cell(row, "DashboardRowFolder").IsEffectivelyVisible);
        }

        // Every text of the scenario whole (2.6's measure), and infra's Pull all reason in its Operation cell.
        Assert.Empty(TrimmedIn(app.FindByAutomationId("DashboardList")));
        var infra = app.ActiveDashboard!.Rows.Single(row => row.Name == "infra");
        infra.BulkText = new DashboardCellText("HEAD is detached: check out a branch to pull", DashboardTone.Secondary, "HEAD is detached: check out a branch to pull");
        app.Settle();
        Assert.Empty(TrimmedIn(app.FindByAutomationId("DashboardList")));
        infra.BulkText = null;

        // 2.8: the branch part shows the workspace after its icon, on a dashboard only.
        Assert.True(app.FindByAutomationId("StatusWorkspaceIcon").IsEffectivelyVisible);
        Assert.Equal("Web", ((TextBlock)app.FindByAutomationId("CurrentBranch")).Text);
        Assert.Equal("Ready", ((TextBlock)app.FindByAutomationId("OperationStatus")).Text);

        // The list has the focus (it took it when the dashboard became active), no row selected.
        Assert.True(FocusIsIn(window, "DashboardList"));
        Assert.Null(snapshot.SelectedRow);

        // Ten rows in view with the activity log open (2.4).
        app.Click(app.DashboardRowPoint("api"));
        app.Session.Shell.ActivityLog.Open();
        app.Settle();
        var list = app.FindByAutomationId("DashboardList");
        Assert.Equal(new Rect(0, 152, 1100, 317), app.BoundsOf(list));
        foreach (var name in TempWorkspace.DashboardOrder)
        {
            var rowBounds = app.BoundsOf(app.DashboardRowControl(name));
            Assert.True(rowBounds.Bottom <= 469, $"{name} lies in view");
        }

        app.Session.Shell.ActivityLog.Close();
        app.Settle();

        // Enter opens the selected row (2.3, 2.7): its tab, with its panels as they were.
        app.PressKey(Key.Enter);
        await app.WaitForRepositoryAsync(Patience);
        Assert.Equal("api", app.Session.Shell.ActiveTab.Title);
        Assert.True(window.GetVisualDescendants().OfType<LeftPanelView>().Single().IsEffectivelyVisible);
        Assert.True(window.FindControl<GridSplitter>("LeftSplitter")!.IsEffectivelyVisible);
        Assert.Equal((left, right), (mainArea.ColumnDefinitions[0].ActualWidth, mainArea.ColumnDefinitions[2].ActualWidth));
        Assert.False(app.FindByAutomationId("StatusWorkspaceIcon").IsEffectivelyVisible);
        Assert.Equal("main", ((TextBlock)app.FindByAutomationId("CurrentBranch")).Text);

        // Back on the dashboard, its list has the focus again, on the row selected before.
        app.Session.Shell.ActivateTabCommand.Execute(app.Session.Shell.Tabs[0]);
        app.Settle();
        await app.WaitForAsync(() => app.ActiveDashboard is not null && FocusIsIn(window, "DashboardList"), "the dashboard active with the list focused");
        Assert.Equal("api", app.ActiveDashboard!.SelectedRow?.Name);

        // Shift+F10 opens the selected row's menu at its bottom-left corner (2.7, phase 3's 4a).
        app.Click(app.DashboardRowPoint("docs"));
        var docs = app.BoundsOf(app.DashboardRowControl("docs"));
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var menu = app.Menu();
        Assert.Equal("docs", menu.Header);
        Assert.Equal((MenuOpenedBy.Keyboard, new MenuPosition(docs.X, docs.Bottom)), (app.Session.Shell.Menu.OpenedBy, app.Session.Shell.Menu.Position));
        app.PressKey(Key.Escape);
        Assert.Null(app.Session.Shell.Menu.Current);

        // A double-click opens a row, its tab after the tabs this dashboard opened (2.7).
        await app.OpenRowAsync("web-app");
        await app.WaitForRepositoryAsync(Patience);
        Assert.Equal(["Web", "api", "web-app"], app.Session.Shell.Tabs.Select(tab => tab.Title));
    }

    [AvaloniaFact]
    public async Task Each_cell_shows_its_tooltip_under_the_pointer_and_a_cell_still_selects_opens_and_menus_its_row()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        using var app = await StartAsync(copy, data, 1100, 700);
        var dashboard = app.ActiveDashboard!;
        var infra = dashboard.Rows.Single(row => row.Name == "infra");
        infra.BulkText = new DashboardCellText("HEAD is detached: check out a branch to pull", DashboardTone.Secondary, "HEAD is detached: check out a branch to pull");
        app.Settle();

        // 2.6, 15.5 (the report's W2 step 2): the pointer finds each kind of cell, over its text or
        // its arrows, and the cell's own tooltip opens there.
        foreach (var (name, id) in new[]
        {
            ("api", "DashboardRowName"), ("api", "DashboardRowAheadBehind"), ("mobile", "DashboardRowAheadBehind"),
            ("billing", "DashboardRowChanges"), ("old-site", "DashboardRowState"), ("infra", "DashboardRowOperation"),
        })
        {
            var cell = Cell(app.DashboardRowControl(name), id);
            var tip = ToolTip.GetTip(cell) as string;
            Assert.False(string.IsNullOrEmpty(tip), $"{name}'s {id} has a tooltip.");
            Assert.IsType<DashboardCell>(cell);
            Assert.Equal(tip, await app.RestPointerOnAsync(cell));
            Assert.True(ToolTip.GetIsOpen(cell), $"{name}'s {id}'s own tooltip is open.");
            app.MoveToNeutral();
            await app.WaitForAsync(() => !ToolTip.GetIsOpen(cell), $"{name}'s {id}'s tooltip to close");
        }

        // On a cell, a click still selects its row, a right-click opens the row's menu and a
        // double-click opens the row (2.7).
        var docs = app.DashboardRowControl("docs");
        app.Click(Cell(docs, "DashboardRowAheadBehind"));
        Assert.Equal("docs", dashboard.SelectedRow?.Name);
        app.RightClick(Cell(docs, "DashboardRowChanges"));
        Assert.Equal("docs", app.Menu().Header);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
        app.DoubleClick(Cell(docs, "DashboardRowName"));
        await app.WaitForRepositoryAsync(Patience);
        Assert.Equal("docs", app.Session.Shell.ActiveTab.Title);
    }

    [AvaloniaFact]
    public async Task The_More_menu_opens_below_its_button_moved_inside_the_window_not_flipped()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        using var app = await StartAsync(copy, data, 1100, 700);

        // 2.5, WD10 (the report's W1 step 9): its top at the button's bottom, its right edge 4
        // inside the window, as a button's menu (MenuOpenedBy.Button), not opened to the left of
        // the button's corner as a pointer's menu would be.
        var more = app.BoundsOf(app.FindByAutomationId("DashboardMore"));
        app.Click(app.FindByAutomationId("DashboardMore"));
        var menu = app.Menu();
        Assert.Equal(MenuOpenedBy.Button, app.Session.Shell.Menu.OpenedBy);
        Assert.Equal(new MenuPosition(more.X, more.Bottom), app.Session.Shell.Menu.Position);
        Assert.Equal(1096, menu.Bounds.Right, 1.0);
        Assert.Equal(more.Bottom, menu.Bounds.Top, 1.0);
        Assert.True(menu.Bounds.Left < more.X, $"The menu at {menu.Bounds} was moved left of the button's corner {more.X}.");
        TestPictures.Save(app, "wpk-dashboard-more-menu");
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");

        // The rule alone: moved, never flipped, also at the bottom edge.
        Assert.Equal(new Point(896, 124), MenuLayer.PlaceMenu(new Size(1100, 700), new Point(1060, 124), new Size(200, 120), MenuOpenedBy.Button));
        Assert.Equal(new Point(896, 576), MenuLayer.PlaceMenu(new Size(1100, 700), new Point(1060, 650), new Size(200, 120), MenuOpenedBy.Button));
    }

    [AvaloniaFact]
    public async Task At_1920_light_the_folder_column_shows_every_saved_folder_whole()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        using var app = await StartAsync(copy, data, 1920, 1080);
        app.Session.Shell.UseTheme(AppTheme.Light);
        app.Settle();

        var columns = DashboardColumns.Compute(1920);
        var headings = app.FindByAutomationId("DashboardColumnHeaders").GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text);
        Assert.Equal(["Repository", "Folder", "Branch", "Ahead / behind", "Changes", "State", "Operation"], headings);
        foreach (var name in TempWorkspace.DashboardOrder)
        {
            var row = app.DashboardRowControl(name);
            var folder = (TextBlock)Cell(row, "DashboardRowFolder");
            Assert.True(folder.IsEffectivelyVisible);
            Assert.Equal(copy.PathOf(name), folder.Text);
            Assert.Equal(columns[DashboardColumn.Folder].X + 8, app.BoundsOf(folder).X, 1);
            Assert.Equal(columns[DashboardColumn.Operation].X + 8, app.BoundsOf(Cell(row, "DashboardRowOperation")).X, 1);
        }

        // Every cell whole but the Folder cells, which hold the saved text and are cut exactly when
        // it does not fit their column: whole on the development machine, cut on CI's macOS, whose
        // temporary folder is longer (WD29).
        Assert.Empty(TrimmedIn(app.FindByAutomationId("DashboardList"), except: "DashboardRowFolder"));
        var room = columns[DashboardColumn.Folder].Width - (2 * DashboardColumns.TextInset);
        foreach (var name in TempWorkspace.DashboardOrder)
        {
            var folder = (TrimmedTextBlock)Cell(app.DashboardRowControl(name), "DashboardRowFolder");
            var cut = NaturalWidth(folder) > room;
            Assert.True(cut == folder.IsTrimmed, $"{name}'s folder is {(folder.IsTrimmed ? "cut" : "whole")} at {NaturalWidth(folder)} of {room}");
            Assert.Equal(cut ? folder.Text : null, ToolTip.GetTip(folder) as string);
        }

        Assert.Equal(1912, app.BoundsOf(app.FindByAutomationId("DashboardMore")).Right, 1);

        // The driver's git calls in one folder match git's spelling of it (on macOS /private/var/…).
        var api = app.ActiveDashboard!.Rows.Single(row => row.Name == "api").Repository!.Repository.WorkingDirectory;
        var calls = app.CallsSince(0, folder => WorkspacePaths.Same(folder, api));
        Assert.Contains(calls, call => call.CommandText.StartsWith("git status", StringComparison.Ordinal));
        Assert.All(calls, call => Assert.True(WorkspacePaths.Same(call.WorkingDirectory!, api)));
        Assert.True(calls.Count < app.CallsSince(0).Count);
        Assert.Empty(app.CallsSince(app.CallMark()));
    }

    [AvaloniaFact]
    public async Task After_a_removal_no_row_is_selected_and_the_focus_stays_in_the_list_or_goes_to_add_repository()
    {
        using var provider = new FakeProvider();
        using var host = await HostAsync(provider, "api", "docs");
        var dashboard = host.Dashboard;
        dashboard.SelectedRow = dashboard.Rows[0];
        host.Settle();
        host.FocusSelected();

        // Remove from workspace (1.10), from the row's menu: the row goes, none is selected, the
        // focus stays in the list.
        dashboard.Remove(dashboard.Rows[0]);
        host.Settle();

        Assert.Equal(["docs"], dashboard.Rows.Select(row => row.Name));
        Assert.Null(dashboard.SelectedRow);
        Assert.True(FocusIsIn(host.Window, "DashboardList"));

        // The last row goes: the focus goes to the empty state's Add repository….
        dashboard.Remove(dashboard.Rows[0]);
        host.Settle();

        Assert.Empty(dashboard.Rows);
        Assert.True(host.Find("DashboardEmpty").IsEffectivelyVisible);
        Assert.True(host.Window.FocusManager?.GetFocusedElement() is Button { Name: "DashboardEmptyAddRepository" });
    }

    [AvaloniaFact]
    public async Task Delete_removes_the_selected_row_and_a_second_Delete_changes_nothing()
    {
        using var provider = new FakeProvider();
        using var host = await HostAsync(provider, "api", "docs", "notes");
        var dashboard = host.Dashboard;
        dashboard.SelectedRow = dashboard.Rows[1];
        host.Settle();
        host.FocusSelected();

        host.PressKey(Key.Delete);

        Assert.Equal(["api", "notes"], dashboard.Rows.Select(row => row.Name));
        Assert.Equal(2, host.Store.Current.Find("web")!.Repositories.Count);
        Assert.Null(dashboard.SelectedRow);
        Assert.True(FocusIsIn(host.Window, "DashboardList"));

        // No row is selected: a second Delete removes nothing.
        host.PressKey(Key.Delete);
        Assert.Equal(["api", "notes"], dashboard.Rows.Select(row => row.Name));
        Assert.Equal(2, host.Store.Current.Find("web")!.Repositories.Count);
    }

    [AvaloniaFact]
    public async Task Delete_on_the_only_row_puts_the_focus_on_the_empty_states_Add_repository()
    {
        using var provider = new FakeProvider();
        using var host = await HostAsync(provider, "api");
        host.Dashboard.SelectedRow = host.Dashboard.Rows[0];
        host.Settle();
        host.FocusSelected();

        host.PressKey(Key.Delete);

        Assert.Empty(host.Dashboard.Rows);
        Assert.True(host.Find("DashboardEmpty").IsEffectivelyVisible);
        Assert.True(host.Window.FocusManager?.GetFocusedElement() is Button { Name: "DashboardEmptyAddRepository" });
    }

    [AvaloniaFact]
    public async Task Cmd_Backspace_removes_the_selected_row_with_the_macOS_keys_only()
    {
        using var provider = new FakeProvider();
        using var host = await HostAsync(provider, "api", "docs");
        var dashboard = host.Dashboard;
        dashboard.SelectedRow = dashboard.Rows[0];
        host.Settle();
        host.FocusSelected();

        // With Windows' and Linux's keys, Cmd+Backspace is no gesture of the list.
        host.PressKey(Key.Back, RawInputModifiers.Meta);
        Assert.Equal(2, dashboard.Rows.Count);

        // With macOS's keys it removes the row (2.7): this view's own, so that no other test sees
        // them (the app's KeyTexts.Current stays as it is).
        ((DashboardView)host.Window.Content!).KeysPlatform = CommandKey.Cmd;
        host.PressKey(Key.Back, RawInputModifiers.Meta);

        Assert.Equal(["docs"], dashboard.Rows.Select(row => row.Name));
        Assert.Null(dashboard.SelectedRow);
    }

    [AvaloniaFact]
    public async Task Alt_Up_and_Alt_Down_move_the_selected_row_which_stays_selected_with_the_focus_in_the_list()
    {
        using var provider = new FakeProvider();
        using var host = await HostAsync(provider, "api", "docs", "notes");
        var dashboard = host.Dashboard;
        var notes = dashboard.Rows[2];
        dashboard.SelectedRow = notes;
        host.Settle();
        host.FocusSelected();

        host.PressKey(Key.Up, RawInputModifiers.Alt);

        Assert.Equal(["api", "notes", "docs"], dashboard.Rows.Select(row => row.Name));
        Assert.Equal(["api", "notes", "docs"], host.Store.Current.Find("web")!.Repositories.Select(repository => Path.GetFileName(repository.Path)));
        Assert.Same(notes, dashboard.SelectedRow);
        Assert.True(FocusIsIn(host.Window, "DashboardList"));

        host.PressKey(Key.Up, RawInputModifiers.Alt);
        Assert.Equal(["notes", "api", "docs"], dashboard.Rows.Select(row => row.Name));

        // The first row moves up no further (It is the first repository).
        host.PressKey(Key.Up, RawInputModifiers.Alt);
        Assert.Equal(["notes", "api", "docs"], dashboard.Rows.Select(row => row.Name));

        host.PressKey(Key.Down, RawInputModifiers.Alt);
        Assert.Equal(["api", "notes", "docs"], dashboard.Rows.Select(row => row.Name));
        Assert.Same(notes, dashboard.SelectedRow);
        Assert.True(FocusIsIn(host.Window, "DashboardList"));
        Assert.True(host.Window.FocusManager?.GetFocusedElement() is ListBoxItem { DataContext: DashboardRowViewModel focused } && ReferenceEquals(focused, notes));
    }

    [AvaloniaFact]
    public async Task A_rows_cancel_says_what_it_stops()
    {
        using var provider = new FakeProvider();
        using var host = await HostAsync(provider, "api");
        var row = host.Dashboard.Rows[0];
        var queue = row.Repository!.Queue;

        var (started, finish) = (new TaskCompletionSource(), new TaskCompletionSource());
        var fetch = queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.None, async context =>
        {
            started.SetResult();
            await finish.Task.WaitAsync(context.CancellationToken);
            return OperationReport.Done("Fetched from all remotes");
        }));
        await started.Task.WaitAsync(Patience);
        await DashboardTestSupport.WaitUntilAsync(() => row.ShowsCancel, "the row's Cancel");
        host.Settle();
        var cancel = host.Find("DashboardRowCancel");
        Assert.True(cancel.IsEffectivelyVisible);
        Assert.Equal(("Cancel", "Stop git"), (AutomationProperties.GetName(cancel), ToolTip.GetTip(cancel) as string));
        Assert.False(host.Find("DashboardRowDetails").IsEffectivelyVisible);
        Assert.Equal("Fetching from all remotes…", ((TextBlock)host.Find("DashboardRowOperation").GetVisualDescendants().OfType<TextBlock>().First()).Text);
        finish.SetResult();
        await fetch.WaitAsync(Patience);

        var toolStarted = new TaskCompletionSource();
        var tool = queue.RunAsync(new GitOperation("Merge tool for notes.txt", "Waiting for kdiff3 to close…", OperationKind.Local, RefreshScope.None, async context =>
        {
            toolStarted.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Resolved notes.txt");
        })
        {
            CanBeCancelled = true,
        });
        await toolStarted.Task.WaitAsync(Patience);
        await DashboardTestSupport.WaitUntilAsync(() => row.ShowsCancel, "the row's Cancel for the merge tool");
        host.Settle();
        Assert.Equal("Stop git and the merge tool", ToolTip.GetTip(host.Find("DashboardRowCancel")) as string);

        // The button cancels it, without selecting the row.
        host.Click(host.Find("DashboardRowCancel"));
        Assert.Equal(OperationOutcome.Cancelled, (await tool.WaitAsync(Patience)).Outcome);
        Assert.Null(host.Dashboard.SelectedRow);
    }

    [AvaloniaFact]
    public void An_empty_workspace_shows_its_empty_state_without_headings_or_list()
    {
        using var provider = new FakeProvider();
        using var host = Host.Show(provider, DashboardTestSupport.Store(), 1100);

        Assert.True(host.Find("DashboardEmpty").IsEffectivelyVisible);
        Assert.False(host.Find("DashboardColumnHeaders").IsEffectivelyVisible);
        Assert.False(host.Find("DashboardList").IsEffectivelyVisible);
        Assert.Equal(
            ["Web", "No repositories", "Add repository…", "Fetch all", "Pull all", "Refresh", "Web has no repositories yet.", "Add repository…", "Add open tabs"],
            Texts(host.Window));
        // Centred (to the layout's rounding) and at most 440 wide.
        var empty = host.BoundsOf(host.Find("DashboardEmpty"));
        Assert.True(empty.Width <= 440);
        Assert.InRange(empty.Center.X, 549, 551);
    }

    [AvaloniaFact]
    public void The_failed_saves_notice_shows_in_the_warning_colour_while_the_last_save_failed()
    {
        using var provider = new FakeProvider();
        var store = new FailingStore(DashboardTestSupport.Store().Current);
        using var host = Host.Show(provider, store, 1100);
        var notice = (TextBlock)host.Find("DashboardNotice");
        Assert.False(notice.IsEffectivelyVisible);

        store.Fail(true);
        host.Settle();

        Assert.True(notice.IsEffectivelyVisible);
        Assert.Equal(DashboardTexts.SaveFailedNotice, notice.Text);
        Assert.Equal(Brush(host.Window, "VcWarningBrush"), Color(notice.Foreground));
        Assert.Equal(new Point(8, host.BoundsOf(host.Find("DashboardHeader")).Bottom + 4), host.BoundsOf(notice).Position);

        store.Fail(false);
        host.Settle();
        Assert.False(notice.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task A_long_name_during_a_run_gives_way_so_that_the_runs_text_keeps_120_on_one_line()
    {
        using var provider = new FakeProvider();
        const string name = "Every web service, every library they share and every tool that deploys them";
        var folders = Enumerable.Range(0, 10).Select(i => provider.Add($"repo{i}").WorkingDirectory).ToList();
        var store = new FailingStore(new WorkspaceList { Workspaces = [new Workspace("web", name, [.. folders.Select(folder => new WorkspaceRepository(folder))])] });
        using var host = Host.Show(provider, store, 1100);
        host.Dashboard.OnBecameActive();
        await DashboardTestSupport.WaitUntilAsync(() => host.Dashboard.Rows.All(row => row.HasRepository), "the rows' reads");

        // A real Fetch all (2.5, 4.9): each row's queue busy with an operation of its own, so that
        // the run's fetches wait behind them and the run runs while the header is measured.
        var hold = new TaskCompletionSource();
        var held = host.Dashboard.Rows.Select(row => row.Repository!.Queue.RunAsync(new GitOperation("Held", "Holding…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            await hold.Task;
            return OperationReport.Done("Held");
        }))).ToList();
        var fetchAll = host.Dashboard.Bulk.FetchAllAsync();
        await DashboardTestSupport.WaitUntilAsync(() => host.Dashboard.Bulk.Current!.RunningRows == BulkSlots.Count, "the run's four turns");
        host.Settle();
        var text = (TrimmedTextBlock)host.Find("DashboardBulkText");
        Assert.Equal("Fetching 10 repositories…", text.Text);
        Assert.All(new[] { text, host.Find("DashboardBulkProgress"), host.Find("DashboardCancelAll") }, part => Assert.True(part.IsEffectivelyVisible));

        var header = host.BoundsOf(host.Find("DashboardHeader"));
        var title = (TrimmedTextBlock)host.Find("DashboardTitle");
        var (titleBounds, count) = (host.BoundsOf(title), host.BoundsOf(host.Find("DashboardCount")));
        var run = host.BoundsOf(text.GetVisualParent()!);
        var bar = host.BoundsOf(host.Find("DashboardBulkProgress"));
        var cancelAll = host.BoundsOf(host.Find("DashboardCancelAll"));
        var (add, more) = (host.BoundsOf(host.Find("DashboardAddRepository")), host.BoundsOf(host.Find("DashboardMore")));

        // The run's text keeps 120 (its slot, left of the bar's gap of 8), right-aligned in it.
        Assert.True(bar.X - 8 - run.X >= 120, $"the run's text has {bar.X - 8 - run.X}");
        Assert.Equal(bar.X - 8, host.BoundsOf(text).Right, 1);

        // The title gave way below its 240, cut with its ellipsis.
        Assert.True(titleBounds.Width < DashboardHeaderPanel.MaxTitleWidth, $"the title has {titleBounds.Width}");
        Assert.True(title.IsTrimmed);

        // One line, left to right without overlap, the buttons at the right edge.
        Assert.Equal(36, header.Height);
        Assert.All(new[] { titleBounds, count, run, bar, cancelAll, add, more }, bounds => Assert.True(bounds.Y >= header.Y && bounds.Bottom <= header.Bottom, $"{bounds} lies inside the header"));
        Assert.Equal(titleBounds.Right + 10, count.X, 1);
        Assert.True(count.Right <= run.X, $"the count ends at {count.Right}, the run starts at {run.X}");
        Assert.Equal(cancelAll.Right + 16, add.X, 1);
        Assert.Equal(1092, more.Right, 1);

        // Cancel all ends the run: the rows in line at once, the four waiting fetches unrun.
        host.Click(host.Find("DashboardCancelAll"));
        await fetchAll.WaitAsync(Patience);
        hold.SetResult();
        await Task.WhenAll(held).WaitAsync(Patience);
        host.Settle();
        Assert.Equal("Fetched no repositories; 10 cancelled", text.Text);
        Assert.False(host.Find("DashboardCancelAll").IsEffectivelyVisible);
        Assert.False(host.Find("DashboardBulkProgress").IsEffectivelyVisible);
    }

    private static async Task<ShellDriver> StartAsync(TempWorkspace copy, TempDirectory data, int width, int height)
    {
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [copy.ToWorkspace()] });
        var app = ShellDriver.Start(data.Path, width, height);
        await app.WaitUntilReadyAsync();
        var mark = app.LogMark();
        app.Session.Shell.ShowWorkspace("web", app.Session.Shell.ActiveTab);
        await app.WaitForDashboardReadAsync(mark, Patience);
        await app.WaitForAsync(() => app.ActiveDashboard!.Rows.All(row => row.Repository is not { } repository || repository.Queue.Current is null), "every row's queue idle", Patience);
        app.MoveToNeutral();
        return app;
    }

    private static async Task<Host> HostAsync(FakeProvider provider, params string[] names)
    {
        var store = DashboardTestSupport.Store([.. names.Select(name => provider.Add(name).WorkingDirectory)]);
        var host = Host.Show(provider, store, 1100);
        host.Dashboard.OnBecameActive();
        await DashboardTestSupport.WaitUntilAsync(() => host.Dashboard.Rows.All(row => row.HasRepository), "the rows' reads");
        host.Settle();
        return host;
    }

    private static Control Cell(Visual row, string automationId) =>
        row.GetVisualDescendants().OfType<Control>().First(control => AutomationProperties.GetAutomationId(control) == automationId);

    private static void AssertBounds(ShellDriver app, string automationId, double x, double y, double width, double height) =>
        Assert.Equal(new Rect(x, y, width, height), app.BoundsOf(app.FindByAutomationId(automationId)));

    private static List<string> TrimmedIn(Visual scope, string? except = null) =>
        [.. scope.GetVisualDescendants().OfType<TrimmedTextBlock>()
            .Where(text => text.IsEffectivelyVisible && text.IsTrimmed && (except is null || AutomationProperties.GetAutomationId(text) != except))
            .Select(text => text.Text ?? string.Empty)];

    /// <summary>The width of <paramref name="text"/>'s whole text on one line, in its font.</summary>
    private static double NaturalWidth(TextBlock text) =>
        new Avalonia.Media.TextFormatting.TextLayout(
            text.Text,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
            text.FontSize,
            foreground: null).WidthIncludingTrailingWhitespace;

    private static bool FocusIsIn(TopLevel window, string name) =>
        window.FocusManager?.GetFocusedElement() is Visual focused && focused.GetSelfAndVisualAncestors().OfType<Control>().Any(control => control.Name == name);

    private static List<string> Texts(Visual scope) =>
        [.. scope.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
            .Select(text => (text.Text!, Point: text.TranslatePoint(default, scope) ?? default))
            .OrderBy(item => Math.Round(item.Point.Y / 6))
            .ThenBy(item => item.Point.X)
            .Select(item => item.Item1)];

    private static Color Brush(Control scope, string key) =>
        scope.TryFindResource(key, scope.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush.Color : default;

    private static Color Color(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : default;

    /// <summary>A dashboard view on its own, in a window as wide as the main window and as high as its main area, with the app's theme and a window view model as the menus' owner.</summary>
    private sealed class Host : IDisposable
    {
        private Host(Window window, DashboardViewModel dashboard, IWorkspaceStore store)
        {
            Window = window;
            Dashboard = dashboard;
            Store = store;
        }

        public Window Window { get; }

        public DashboardViewModel Dashboard { get; }

        public IWorkspaceStore Store { get; }

        public static Host Show(FakeProvider provider, IWorkspaceStore store, double width)
        {
            var services = DashboardTestSupport.Services(provider, store);
            var shell = DashboardTestSupport.Window(services);
            var dashboard = new DashboardViewModel(store.Current.Find("web")!, services, () => true);
            var view = new DashboardView { DataContext = dashboard };
            var window = new Window { Width = width, Height = 586, FontSize = 12.5, Content = view, DataContext = shell };
            window.Show();
            var host = new Host(window, dashboard, store);
            host.Settle();
            return host;
        }

        public void Settle()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public Control Find(string automationId) =>
            Window.GetVisualDescendants().OfType<Control>().First(control => AutomationProperties.GetAutomationId(control) == automationId);

        public Rect BoundsOf(Visual visual) =>
            new(visual.TranslatePoint(default, Window) ?? throw new InvalidOperationException("Not in the window."), visual.Bounds.Size);

        public void FocusSelected()
        {
            var list = Window.GetVisualDescendants().OfType<ListBox>().Single(box => box.Name == "DashboardList");
            (list.ContainerFromIndex(list.SelectedIndex) as InputElement ?? list).Focus(NavigationMethod.Tab);
            Settle();
        }

        public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPress(key, modifiers, PhysicalKey.None, null);
            Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
            Settle();
        }

        public void Click(Visual visual)
        {
            var centre = BoundsOf(visual).Center;
            Window.MouseMove(centre);
            Window.MouseDown(centre, MouseButton.Left);
            Window.MouseUp(centre, MouseButton.Left);
            Settle();
        }

        public void Dispose()
        {
            Dashboard.Dispose();
            Window.Close();
        }
    }

    /// <summary>A store whose saves fail while the test says so (1.3's <c>SaveFailed</c>).</summary>
    private sealed class FailingStore(WorkspaceList initial) : IWorkspaceStore
    {
        public WorkspaceList Current { get; private set; } = initial;

        public bool IsReadOnly => false;

        public WorkspaceReadOnlyCause? ReadOnlyCause => null;

        public string? Notice => SaveFailed ? DashboardTexts.SaveFailedNotice : null;

        public bool SaveFailed { get; private set; }

        public event EventHandler? Changed;

        public void Fail(bool failed)
        {
            SaveFailed = failed;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Update(Func<WorkspaceList, WorkspaceList> change)
        {
            var next = change(Current);
            if (!ReferenceEquals(next, Current))
            {
                Current = next;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
