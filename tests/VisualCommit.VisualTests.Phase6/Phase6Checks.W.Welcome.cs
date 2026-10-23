// Check W1 of part W (docs/test-reports/phase-6.md): workspaces from the welcome page, in two
// methods (WD6): part A, a new "Web" from the welcome page, its first repository and the scan's
// checklist; part B, Add open tabs, Rename workspace… and Delete workspace… on "Web" less web-app
// and docs with their tabs open (WD19).
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksW
{
    /// <summary>
    /// Check W1, part A (steps 1 to 8): the welcome page's Workspaces section with none; a new
    /// "Web", whose dashboard takes the empty tab's place, empty; <c>Add repository…</c> on api,
    /// added at once, selected and read with no F5; on the copy's root the scan's checklist of nine
    /// with api already in it, <c>Add 8 repositories</c>, the eight read with no F5; the section
    /// again, listing "Web", whose item activates the open dashboard.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_W1_workspace_from_the_welcome_page()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");
        var paths = new AppPaths(data.Path);

        // Step 1: a fresh data folder, no workspaces.json and no session; the app writes nothing before it starts.
        using var app = ShellDriver.Start(data.Path, 1100, 700);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        var shot = Shot(app, PartW.W1aWelcomeWorkspaces);
        var strip = app.TabStrip();
        var newTab = Assert.Single(strip.Tabs);
        Assert.Equal(("RepoTab", PartW.NewTab, false), (newTab.AutomationId, newTab.Title, newTab.ShowsCloseButton));
        Assert.Equal(PartW.WelcomeWithoutWorkspaces, TextsIn(app.ActiveWelcomeView));
        AssertWorkspacesSection(app, workspaces: 0, shot);
        foreach (var button in new[] { "FetchButton", "PullButton", "PushButton", "BranchButton", "StashButton", "PopButton", "UndoButton", "RedoButton", "SearchButton" })
        {
            Assert.False(ToolbarButton(app, button).IsEffectivelyEnabled, $"{button} is enabled on an empty tab.");
        }

        AssertButton(app, "SettingsButton", true, PartW.SettingsToolTip);
        AssertButton(app, "ThemeSwitch", true, PartW.ThemeToLight);
        Assert.False(File.Exists(paths.WorkspacesFile), "workspaces.json exists before anything changed (1.3).");

        // Step 2: New workspace…: the name dialog, read before the typing; then "Web" typed (WD9).
        app.Click(app.Find<Button>("NewWorkspaceButton"));
        var dialog = await app.WaitForDialogAsync<WorkspaceNameDialogViewModel>();
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The name dialog has no view.");
        Assert.Equal(PartW.NewWorkspaceTitle, ((TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        Assert.False(app.FindByAutomationId(view, "NameDialogContext").IsEffectivelyVisible, "The new workspace's dialog shows a context line (WD28).");
        Assert.False(app.FindByAutomationId(view, "DialogRepository").IsEffectivelyVisible, "The new workspace's dialog shows a repository line (WD11).");
        var nameBox = (TextBox)app.FindByAutomationId(view, "NameBox");
        await app.WaitForAsync(() => nameBox.IsFocused, "the name box to have the keyboard focus");
        Assert.True(string.IsNullOrEmpty(nameBox.Text), $"The name box holds \"{nameBox.Text}\".");
        Assert.Equal(PartW.NamePlaceholder, nameBox.PlaceholderText);
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(PartW.Create, confirm.Content);
        Assert.False(confirm.IsEffectivelyEnabled, "Create is enabled with an empty name.");
        Assert.False(app.FindByAutomationId(view, "NameError").IsEffectivelyVisible, "NameError shows before the typing.");
        app.Type(PartW.Workspace);
        Assert.Equal(PartW.Workspace, nameBox.Text);
        Assert.False(app.FindByAutomationId(view, "NameError").IsEffectivelyVisible, "NameError shows for \"Web\".");
        Assert.True(confirm.IsEffectivelyEnabled, "Create is disabled for \"Web\".");
        Assert.Equal(["Cancel", PartW.Create], DialogButtons(app, view));
        Shot(app, PartW.W1bNewWorkspaceDialog);

        // Step 3: Create: the empty tab became the dashboard, empty.
        app.Click(confirm);
        await app.WaitForAsync(() => app.ActiveDashboard is not null && app.Session.Services.DialogHost.Current is null, "the new workspace's dashboard to show");
        Assert.True(dialog.IsClosed, "The name dialog did not close.");
        app.MoveToNeutral();
        shot = Shot(app, PartW.W1cEmptyDashboard);
        strip = app.TabStrip();
        Assert.Single(strip.Tabs);
        var tab = AssertDashboardTab(app, strip, 0, PartW.Workspace, active: true);
        Assert.True(Math.Abs(tab.Bounds.Width - 120) <= PartW.Tolerance, $"The dashboard's tab is {tab.Bounds.Width} wide, not 120 (13.4).");
        Assert.True(strip.AddButtonBounds.Left >= tab.Bounds.Right - 0.5, "\"+\" is not after the dashboard's tab.");
        AssertSpansTheWindow(app);
        AssertHeaderLayout(app);
        var palette = PaletteOf(app);
        var header = Control<Border>(app, "DashboardHeader");
        Assert.Equal(palette.Chrome, ((ISolidColorBrush)header.Background!).Color);
        AssertColour(palette.Border, shot, new Point(550, PartW.HeaderBottom - 0.5), "header's bottom line (VcBorderBrush)");
        AssertDashboardHeader(app, new ExpectedDashboardHeader(
            PartW.Workspace,
            PartW.Count(0),
            ExpectedHeaderButton.Enabled(PartW.AddRepositoryToolTip(PartW.Workspace)),
            ExpectedHeaderButton.Disabled(PartW.NoRepositories(PartW.Workspace)),
            ExpectedHeaderButton.Disabled(PartW.NoRepositories(PartW.Workspace)),
            ExpectedHeaderButton.Enabled(PartW.RefreshToolTip(PartW.Workspace))));
        Assert.False(app.FindByAutomationId("DashboardColumnHeaders").IsEffectivelyVisible, "An empty workspace shows the column headings (WD2).");
        Assert.False(app.FindByAutomationId("DashboardList").IsEffectivelyVisible, "An empty workspace shows the list (WD2).");
        AssertEmptyState(app);
        AssertButton(app, "FetchButton", false, PartW.NoRepositories(PartW.Workspace));
        AssertButton(app, "PullButton", false, PartW.NoRepositories(PartW.Workspace));
        foreach (var disabled in PartW.DisabledWithoutToolTip)
        {
            AssertButton(app, disabled, false, null);
        }

        AssertButton(app, "SettingsButton", true, PartW.SettingsToolTip);
        AssertButton(app, "ThemeSwitch", true, PartW.ThemeToLight);
        AssertDashboardStatusBar(app, PartW.Workspace, PartW.Ready);
        AssertActivityToggle(app, enabled: false, PartW.NoRepositories(PartW.Workspace));
        var id = AssertWorkspacesFile(data, copy, null, PartW.Workspace, []);
        var session = ReadSession(data);
        Assert.Equal([new TabState(null, id)], session.Tabs);
        Assert.Equal(0, session.ActiveTab);

        // Step 4: Add repository… on api's folder: appended at once, selected, read with no F5 (trigger 6).
        app.Folders.Answer(copy.PathOf("api"));
        var logMark = app.LogMark();
        var calls = app.CallMark();
        app.Click(app.FindByAutomationId("DashboardEmptyAddRepository"));
        await app.WaitForDashboardReadAsync(since: logMark, timeout: OperationChainTimeout);
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        app.MoveToNeutral();
        shot = Shot(app, PartW.W1dApiAdded);
        Assert.Equal([PartW.AddRepositoryFolderTitle(PartW.Workspace)], app.Folders.Titles);
        await AssertNoDialogAsync(app);
        Assert.False(app.FindByAutomationId("DashboardEmpty").IsEffectivelyVisible, "The empty state still shows.");
        AssertHeadings(app, PartW.Columns1100, shot);
        var w0 = W0Of(copy);
        AssertRows(app, copy, [w0["api"]], selected: "api");
        AssertRowPlaces(app, ["api"]);
        AssertColour(palette.Selection, shot, new Point(1090, PartW.RowCentre(0)), "selected row's background (VcSelectionBrush)");
        AssertOnePass(app, logMark, 1);
        var since = app.CallsSince(calls);
        Assert.All(since, call => Assert.True(call.WorkingDirectory is { } folder && SameFolder(copy, copy.PathOf("api"), folder), $"\"{call.CommandText}\" ran in {call.WorkingDirectory}, not in api's folder."));
        AssertOpeningAndSummaryRead(since, "Add repository… on api");
        AssertTd(app, PartW.Workspace, PartW.Count(1));
        AssertActivityToggle(app, enabled: true, PartW.ShowActivityLog);
        AssertWorkspacesFile(data, copy, id, PartW.Workspace, ["api"]);

        // Step 5: Add repository… on the copy's root: no repository, so scanned; the checklist done.
        app.Folders.Answer(copy.Root);
        logMark = app.LogMark();
        app.Click(app.FindByAutomationId("DashboardAddRepository"));
        var checklist = await app.WaitForAddRepositoriesDoneAsync(OperationChainTimeout);
        Shot(app, PartW.W1eScanChecklist);
        Assert.Equal([PartW.AddRepositoryFolderTitle(PartW.Workspace), PartW.AddRepositoryFolderTitle(PartW.Workspace)], app.Folders.Titles);
        AssertChecklist(app, checklist, copy);
        AssertWorkspacesFile(data, copy, id, PartW.Workspace, ["api"]);
        var opened = LinesWith(app, logMark, PartW.OpenedTheRepositoryAt);
        Assert.True(opened.Count == 9, $"The scan logged {opened.Count} \"Opened the repository at …\" lines, not 9 (1.8):\n" + string.Join('\n', opened));

        // Step 6: Add 8 repositories: WA, api still selected (WD1), the eight read with no F5.
        logMark = app.LogMark();
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is null, "the checklist to close");
        await app.WaitForDashboardReadAsync(since: logMark, timeout: OperationChainTimeout);
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        app.MoveToNeutral();
        Shot(app, PartW.W1fNineRows);
        AssertRows(app, copy, [.. PartW.WaOrder.Select(name => w0[name])], selected: "api");
        AssertRowPlaces(app, PartW.WaOrder);
        AssertDashboardHeader(app, ExpectedDashboardHeader.Idle(PartW.Workspace, PartW.Count(9)));
        AssertOnePass(app, logMark, 8);
        AssertWorkspacesFile(data, copy, id, PartW.Workspace, PartW.WaOrder);

        // Step 7: "+": a new tab whose welcome page lists "Web".
        app.Click(app.Find<Button>("AddRepoButton"));
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 2 && app.Session.Shell.ActiveTab.ShowsWelcome, "the new tab's welcome page");
        app.MoveToNeutral();
        shot = Shot(app, PartW.W1gWelcomeListsWeb);
        strip = app.TabStrip();
        Assert.Equal(2, strip.Tabs.Count);
        AssertDashboardTab(app, strip, 0, PartW.Workspace, active: false);
        Assert.Equal(("RepoTab", PartW.NewTab, true), (strip.Tabs[1].AutomationId, strip.Tabs[1].Title, strip.Tabs[1].IsActive));
        Assert.Equal(PartW.WelcomeWithWeb, TextsIn(app.ActiveWelcomeView));
        AssertWorkspacesSection(app, workspaces: 1, shot);
        session = ReadSession(data);
        Assert.Equal([new TabState(null, id), new TabState(null)], session.Tabs);
        Assert.Equal(1, session.ActiveTab);

        // Step 8: the item "Web": its open dashboard is activated (D227), nothing else opens (D188).
        var item = app.ActiveWelcomeView.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "WorkspaceItem");
        app.Click(item);
        app.Settle();
        strip = app.TabStrip();
        Assert.Equal(2, strip.Tabs.Count);
        AssertDashboardTab(app, strip, 0, PartW.Workspace, active: true);
        Assert.Equal(("RepoTab", PartW.NewTab, false), (strip.Tabs[1].AutomationId, strip.Tabs[1].Title, strip.Tabs[1].IsActive));
        AssertRows(app, copy, [.. PartW.WaOrder.Select(name => w0[name])], selected: "api");
        await app.WaitForAsync(() => app.FocusIsWithin(app.Find<ListBox>("DashboardList")), "the dashboard's list to have the keyboard focus");
        AssertTd(app, PartW.Workspace, PartW.Count(9));
        session = ReadSession(data);
        Assert.Equal([new TabState(null, id), new TabState(null)], session.Tabs);
        Assert.Equal(0, session.ActiveTab);
    }

    /// <summary>
    /// Check W1, part B (steps 9 to 13): on "Web" less web-app and docs with their tabs restored
    /// behind the dashboard (WD19), <c>Add open tabs</c> appends the two rows, which follow their
    /// tabs with no git call (WD35, WD37); <c>Rename workspace…</c> to <c>Web team</c>;
    /// <c>Delete workspace…</c> closes the dashboard and changes nothing on disk.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_W1_add_open_tabs_rename_and_delete()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");

        // Step 9: before the start the exact state of the nine and {root}'s folders; "Web" less web-app and docs, and the session.
        var exact = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in PartW.WithFolder)
        {
            using var repo = copy.Repo(name);
            exact[name] = await repo.ExactStateAsync();
        }

        var folders = Directory.GetFileSystemEntries(copy.Root).Order(StringComparer.Ordinal).ToList();
        var workspace = copy.ToWorkspace() with
        {
            Repositories = [.. PartW.Wb8Order.Select(name => new WorkspaceRepository(copy.PathOf(name)))],
        };
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [workspace] });
        using var app = ShellDriver.StartWithTabs(
            data.Path,
            [new TabState(null, PartW.WorkspaceId), new TabState(copy.PathOf("web-app")), new TabState(copy.PathOf("docs"))],
            0,
            1100,
            700);
        await app.WaitUntilReadyAsync();
        await app.WaitForDashboardReadAsync(timeout: OperationChainTimeout);
        var webApp = await app.WaitForTabOpenedAsync(copy.PathOf("web-app"), OperationChainTimeout);
        var docs = await app.WaitForTabOpenedAsync(copy.PathOf("docs"), OperationChainTimeout);
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        app.MoveToNeutral();
        Assert.Equal([PartW.Workspace, "web-app", "docs"], TabTitles(app));
        Assert.Same(app.Session.Shell.Tabs[0], app.Session.Shell.ActiveTab);
        var w0 = W0Of(copy);
        AssertRows(app, copy, [.. PartW.Wb8Order.Select(name => w0[name])]);
        AssertDashboardHeader(app, ExpectedDashboardHeader.Idle(PartW.Workspace, PartW.Count(8)));
        var more = app.FindByAutomationId("DashboardMore");
        app.Click(more);
        var menu = app.Menu();
        Shot(app, PartW.W1hMoreMenu);
        AssertMenu(menu, PartW.MoreMenu(PartW.Workspace, null));
        AssertHighlighted(menu, PartW.AddOpenTabs);
        AssertMenuShape(app, menu);
        Assert.Equal(119, menu.Bounds.Height, PartW.Tolerance);
        var moreBounds = app.BoundsOf(more);
        Assert.True(Math.Abs(menu.Bounds.Right - (app.Window.ClientSize.Width - 4)) <= PartW.Tolerance, $"The More menu ends at x {menu.Bounds.Right}, not 4 inside the window's right edge (WD10).");
        Assert.True(Math.Abs(menu.Bounds.Top - moreBounds.Bottom) <= PartW.Tolerance, $"The More menu's top is y {menu.Bounds.Top}, not below DashboardMore ({moreBounds.Bottom}).");
        Assert.Equal(PartW.Workspace, menu.Header);

        // Step 10: Add open tabs: the two rows appended in tab order, following their tabs with no read.
        var calls = app.CallMark();
        var logMark = app.LogMark();
        app.ClickMenuItem(PartW.AddOpenTabs);
        app.Settle();
        AssertNothingReads(app);
        AssertNoSummaryFor(app, copy, "web-app");
        AssertNoSummaryFor(app, copy, "docs");
        await app.WaitForRowAsync("web-app", row => row.Branch.Text == "main");
        await app.WaitForRowAsync("docs", row => row.Branch.Text == "main");
        app.MoveToNeutral();
        Shot(app, PartW.W1iOpenTabsAdded);
        AssertRows(app, copy, [.. PartW.Wb10Order.Select(name => w0[name])]);
        AssertRowPlaces(app, PartW.Wb10Order);
        AssertDashboardHeader(app, ExpectedDashboardHeader.Idle(PartW.Workspace, PartW.Count(10)));
        Assert.Same(webApp, app.ActiveDashboard!.Rows.Single(row => row.Name == "web-app").Repository);
        Assert.Same(docs, app.ActiveDashboard.Rows.Single(row => row.Name == "docs").Repository);
        Assert.Empty(LinesWith(app, logMark, PartW.SummaryRead("web-app")));
        Assert.Empty(LinesWith(app, logMark, PartW.SummaryRead("docs")));
        Assert.Empty(PassLines(app, logMark));
        var since = CallsInWorkspace(app, copy, calls);
        Assert.True(since.Count == 0, "Add open tabs made git calls in the workspace's folders (3.5):\n" + string.Join('\n', since.Select(call => $"{call.CommandText} in {call.WorkingDirectory}")));
        AssertWorkspacesFile(data, copy, PartW.WorkspaceId, PartW.Workspace, PartW.Wb10Order);

        // Step 11: the More menu again, read and closed with Esc; then Rename workspace…, its dialog read, "Web team" typed (WD16).
        app.Click(more);
        menu = app.Menu();
        AssertMenu(menu, PartW.MoreMenu(PartW.Workspace, PartW.EveryOpenRepositoryIsIn));
        AssertHighlighted(menu, PartW.RenameWorkspace);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        app.Click(more);
        app.ClickMenuItem(PartW.RenameWorkspace);
        var rename = await app.WaitForDialogAsync<WorkspaceNameDialogViewModel>();
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The rename dialog has no view.");
        Assert.Equal(PartW.RenameWorkspaceTitle, ((TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        var context = (TextBlock)app.FindByAutomationId(view, "NameDialogContext");
        Assert.True(context.IsEffectivelyVisible, "The rename shows no context line.");
        Assert.Equal(PartW.RenameContext(PartW.Workspace), context.Text);
        Assert.False(app.FindByAutomationId(view, "DialogRepository").IsEffectivelyVisible, "The rename shows a repository line (WD11).");
        var nameBox = (TextBox)app.FindByAutomationId(view, "NameBox");
        await app.WaitForAsync(() => nameBox.IsFocused, "the name box to have the keyboard focus");
        Assert.Equal(PartW.Workspace, nameBox.Text);
        Assert.Equal((0, PartW.Workspace.Length), (Math.Min(nameBox.SelectionStart, nameBox.SelectionEnd), Math.Max(nameBox.SelectionStart, nameBox.SelectionEnd)));
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(PartW.Rename, confirm.Content);
        Assert.False(confirm.IsEffectivelyEnabled, "Rename is enabled with the name unchanged (1.6).");
        app.Type(PartW.RenamedWorkspace);
        Assert.Equal(PartW.RenamedWorkspace, nameBox.Text);
        Assert.True(confirm.IsEffectivelyEnabled, "Rename is disabled for \"Web team\".");
        Assert.False(app.FindByAutomationId(view, "NameError").IsEffectivelyVisible, "NameError shows for \"Web team\".");
        Shot(app, PartW.W1jRenameDialog);

        // Step 12: Rename: the tab, the header, the status bar and the tooltips follow; then Delete workspace…'s question.
        app.Click(confirm);
        await app.WaitForAsync(() => rename.IsClosed && app.ActiveDashboard?.Name == PartW.RenamedWorkspace, "the workspace to be renamed");
        app.MoveToNeutral();
        var strip = app.TabStrip();
        Assert.Equal([PartW.RenamedWorkspace, "web-app", "docs"], strip.Tabs.Select(candidate => candidate.Title));
        var tab = AssertDashboardTab(app, strip, 0, PartW.RenamedWorkspace, active: true);
        var natural = Math.Clamp(69 + tab.TitleBounds.Width, 120, 220);
        Assert.True(Math.Abs(tab.Bounds.Width - natural) <= PartW.Tolerance, $"The tab is {tab.Bounds.Width} wide, not clamp(69 + {tab.TitleBounds.Width}, 120, 220) = {natural} (13.4).");
        Assert.True(strip.Tabs[1].Bounds.Left >= tab.Bounds.Right - 0.5 && strip.Tabs[2].Bounds.Left >= strip.Tabs[1].Bounds.Right - 0.5 && strip.AddButtonBounds.Left >= strip.Tabs[2].Bounds.Right - 0.5, "web-app's and docs' tabs and \"+\" do not follow the renamed tab.");
        Assert.Equal(PartW.RenamedWorkspace, Control<TextBlock>(app, "DashboardTitle").Text);
        AssertBranch(app, PartW.RenamedWorkspace);
        AssertDashboardToolbar(app, PartW.RenamedWorkspace);
        AssertWorkspacesFile(data, copy, PartW.WorkspaceId, PartW.RenamedWorkspace, PartW.Wb10Order);
        Assert.Equal(new TabState(null, PartW.WorkspaceId), ReadSession(data).Tabs[0]);
        app.Click(more);
        app.ClickMenuItem(PartW.DeleteWorkspace);
        await AssertConfirmationAsync(app, PartW.DeleteTitle, PartW.DeleteQuestion(PartW.RenamedWorkspace, 10), PartW.DeleteDetail, PartW.Delete, destructive: true);
        Assert.False(app.FindByAutomationId(app.TopDialogView!, "DialogRepository") is { IsEffectivelyVisible: true }, "The deletion's question shows a repository line (WD11).");
        Shot(app, PartW.W1kDeleteQuestion);

        // Step 13: Delete: the dashboard closes as by its close button; nothing on disk changed.
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 2, "the strip to hold two tabs");
        await app.WaitForRepositoryAsync(OperationChainTimeout);
        app.MoveToNeutral();
        var picture = Shot(app, PartW.W1lDashboardDeleted);
        Assert.Equal(["web-app", "docs"], TabTitles(app));
        Assert.Same(app.Session.Shell.Tabs[0], app.Session.Shell.ActiveTab);
        Assert.Same(webApp, app.Session.Shell.ActiveTab.Repository);
        Assert.Same(docs, app.Session.Shell.Tabs[1].Repository);
        AssertGraph(app, [.. PartW.WebApp0.Select(row => new CommitRow(row.Message, row.Lane, row.Colour, row.Labels, row.Sha))], lanes: 1, picture: picture);
        var (version, workspaces) = ReadWorkspacesFile(data);
        Assert.Equal((1, 0), (version, workspaces.Count));
        var session = ReadSession(data);
        Assert.Equal(2, session.Tabs.Count);
        Assert.True(session.Tabs.All(saved => saved.WorkspaceId is null), "session.json still holds the dashboard.");
        Assert.True(SameFolder(copy, copy.PathOf("web-app"), session.Tabs[0].RepositoryPath!) && SameFolder(copy, copy.PathOf("docs"), session.Tabs[1].RepositoryPath!), "session.json's tabs are not web-app's and docs'.");
        Assert.Equal(0, session.ActiveTab);
        Assert.Equal(UndoRedoButtons.Nothing, app.UndoRedoState());
        foreach (var name in PartW.WithFolder)
        {
            using var repo = copy.Repo(name);
            Assert.True(exact[name] == await repo.ExactStateAsync(), $"{name} changed.");
        }

        Assert.Equal(folders, Directory.GetFileSystemEntries(copy.Root).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The welcome page's Workspaces section (13.1, W1 steps 1 and 7): between Init's row and
    /// <c>Recent repositories</c>, the heading row 24 below the buttons' row (the stack's 8 and its
    /// own 16), 20 high, <c>WorkspacesHeading</c> (11, SemiBold, secondary) at the stack's left and
    /// <c>NewWorkspaceButton</c> at its right (a 12×12 <c>IconPlus</c>, 4, <c>New workspace…</c>,
    /// secondary, no background); then, 8 below, <c>No workspaces</c> or one <c>WorkspaceItem</c>
    /// per workspace (26 high, the stack's width, the name primary at its left, the count secondary
    /// at its right); no notice; the section 68 or 52 + 26 N high; the stack 392 wide, x 284 to 676,
    /// centred in the graph area.
    /// </summary>
    private static void AssertWorkspacesSection(ShellDriver app, int workspaces, Screenshot picture)
    {
        var palette = PaletteOf(app);
        var view = app.ActiveWelcomeView;
        var section = app.WelcomeWorkspaces();
        var stack = (StackPanel)view.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "WelcomeButtons").GetVisualParent()!;
        var stackBounds = app.BoundsOf(stack);
        Assert.True(Math.Abs(stackBounds.Left - PartW.StackLeft) <= PartW.Tolerance && Math.Abs(stackBounds.Right - PartW.StackRight) <= PartW.Tolerance, $"The welcome page's stack is at {stackBounds}, not x 284 to 676.");
        var top = stackBounds.Top - PartW.GraphAreaTop;
        var bottom = PartW.GraphAreaBottom - stackBounds.Bottom;
        Assert.True(Math.Abs(top - bottom) <= 1, $"The stack is not centred in the graph area (y 88 to 674): {top} above it, {bottom} below.");

        var buttonsRow = app.BoundsOf(app.Find<Button>("OpenRepoButton").GetVisualParent<StackPanel>()!);
        var headingRow = app.BoundsOf(view.GetVisualDescendants().OfType<DockPanel>().Single(panel => panel.Name == "WorkspacesHeadingRow"));
        Assert.True(Math.Abs(headingRow.Top - buttonsRow.Bottom - 24) <= PartW.Tolerance, $"The heading row's top is {headingRow.Top - buttonsRow.Bottom} below the buttons' row, not 24.");
        Assert.True(Math.Abs(headingRow.Height - 20) <= PartW.Tolerance, $"The heading row is {headingRow.Height} high, not 20.");
        Assert.Equal(PartW.WorkspacesHeading, section.Heading);
        Assert.True(Math.Abs(section.HeadingBounds.Left - stackBounds.Left) <= PartW.Tolerance, "WorkspacesHeading is not at the stack's left.");
        var heading = (TextBlock)view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "WorkspacesHeading");
        Assert.Equal((11d, FontWeight.SemiBold), (heading.FontSize, heading.FontWeight));
        Assert.Equal(palette.TextSecondary, (section.HeadingForeground as ISolidColorBrush)?.Color);
        Assert.True(Math.Abs(section.NewWorkspaceBounds.Right - stackBounds.Right) <= PartW.Tolerance, "NewWorkspaceButton is not at the stack's right.");
        Assert.True(section.NewWorkspaceEnabled, "NewWorkspaceButton is disabled.");
        Assert.Equal((PartW.NewWorkspaceName, PartW.NewWorkspaceToolTip, PartW.NewWorkspaceToolTip), (section.NewWorkspaceName, section.NewWorkspaceToolTip, section.NewWorkspaceHelpText));
        var button = app.Find<Button>("NewWorkspaceButton");
        var plus = button.GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>().Single();
        var label = button.GetVisualDescendants().OfType<TextBlock>().Single(text => text.IsEffectivelyVisible);
        Assert.True(ReferenceEquals(Application.Current!.FindResource("IconPlus"), plus.Data), "NewWorkspaceButton's icon is not IconPlus.");
        Assert.Equal((12d, 12d), (plus.Bounds.Width, plus.Bounds.Height));
        Assert.True(Math.Abs(app.BoundsOf(label).Left - app.BoundsOf(plus).Right - 4) <= PartW.Tolerance, "NewWorkspaceButton's label is not 4 after its icon.");
        Assert.Equal(PartW.NewWorkspace, label.Text);
        Assert.Equal(palette.TextSecondary, (label.Foreground as ISolidColorBrush)?.Color);
        var inside = new Point(section.NewWorkspaceBounds.Right - 2, section.NewWorkspaceBounds.Center.Y);
        var outside = new Point(section.NewWorkspaceBounds.Left - 4, section.NewWorkspaceBounds.Center.Y);
        Assert.True(picture.PixelAt(inside) == picture.PixelAt(outside), $"NewWorkspaceButton has a background: {picture.PixelAt(inside)} inside, {picture.PixelAt(outside)} beside it.");
        Assert.Null(section.Notice);

        double sectionBottom;
        if (workspaces == 0)
        {
            Assert.True(section.ShowsNoWorkspaces, "NoWorkspacesText does not show.");
            Assert.Empty(section.Items);
            var none = app.BoundsOf(view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "NoWorkspacesText"));
            Assert.True(Math.Abs(none.Top - headingRow.Bottom - 8) <= PartW.Tolerance, "NoWorkspacesText is not 8 below the heading row.");
            Assert.Equal(PartW.NoWorkspaces, view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "NoWorkspacesText").Text);
            sectionBottom = none.Bottom;
        }
        else
        {
            Assert.False(section.ShowsNoWorkspaces, "NoWorkspacesText shows beside a workspace.");
            var item = Assert.Single(section.Items);
            Assert.Equal((PartW.Workspace, PartW.Count(9), PartW.WaItemToolTip, PartW.Workspace), (item.Name, item.CountText, item.ToolTip, item.AutomationName));
            Assert.True(Math.Abs(item.Bounds.Height - 26) <= PartW.Tolerance && Math.Abs(item.Bounds.Left - stackBounds.Left) <= PartW.Tolerance && Math.Abs(item.Bounds.Right - stackBounds.Right) <= PartW.Tolerance, $"The workspace item is at {item.Bounds}, not 26 high across the stack.");
            Assert.True(Math.Abs(item.Bounds.Top - headingRow.Bottom - 8) <= PartW.Tolerance, "The workspace item is not 8 below the heading row.");
            Assert.True(item.NameBounds.Left < item.CountBounds.Left && Math.Abs(item.CountBounds.Right - (item.Bounds.Right - 8)) <= PartW.Tolerance, "The item's name is not at its left and its count at its right.");
            Assert.Equal(palette.TextPrimary, (item.NameForeground as ISolidColorBrush)?.Color);
            Assert.Equal(palette.TextSecondary, (item.CountForeground as ISolidColorBrush)?.Color);
            sectionBottom = item.Bounds.Bottom;
        }

        var height = sectionBottom - buttonsRow.Bottom;
        Assert.True(Math.Abs(height - PartW.SectionHeight(workspaces)) <= PartW.Tolerance, $"The Workspaces section is {height} high, not {PartW.SectionHeight(workspaces)} (13.1).");
    }

    /// <summary>The dashboard spans the window's width (2.4): no left panel, no right panel, no splitters.</summary>
    private static void AssertSpansTheWindow(ShellDriver app)
    {
        var dashboard = app.BoundsOf((Control)app.FindByAutomationId("Dashboard"));
        Assert.True(Math.Abs(dashboard.Left) <= 0.5 && Math.Abs(dashboard.Right - app.Window.ClientSize.Width) <= 0.5, $"The dashboard is at {dashboard}, not across the window.");
        Assert.False(app.Find<GridSplitter>("LeftSplitter").IsEffectivelyVisible, "The left splitter shows on a dashboard tab.");
        Assert.False(app.Find<GridSplitter>("RightSplitter").IsEffectivelyVisible, "The right splitter shows on a dashboard tab.");
        Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<LeftPanelView>(), panel => panel.IsEffectivelyVisible);
        Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<RightPanelView>(), panel => panel.IsEffectivelyVisible);
        Assert.DoesNotContain(app.Window.GetVisualDescendants().OfType<WorkingChangesView>(), panel => panel.IsEffectivelyVisible);
    }

    /// <summary>
    /// The empty state (2.5, WD2, WD21): centred in the area below the header, at most 440 wide;
    /// <c>Web has no repositories yet.</c> (secondary, centred), then <c>Add repository…</c>
    /// (enabled) and <c>Add open tabs</c> (disabled, its reason as its tooltip) side by side.
    /// </summary>
    private static void AssertEmptyState(ShellDriver app)
    {
        var palette = PaletteOf(app);
        var empty = (StackPanel)app.FindByAutomationId("DashboardEmpty");
        Assert.True(empty.IsEffectivelyVisible, "The empty state does not show.");
        var bounds = app.BoundsOf(empty);
        var dashboard = app.BoundsOf((Control)app.FindByAutomationId("Dashboard"));
        Assert.True(bounds.Width <= 440.5, $"The empty state is {bounds.Width} wide, more than 440.");
        Assert.True(Math.Abs(bounds.Center.X - dashboard.Center.X) <= PartW.Tolerance && Math.Abs(bounds.Center.Y - ((PartW.HeaderBottom + dashboard.Bottom) / 2)) <= PartW.Tolerance, $"The empty state at {bounds} is not centred below the header.");
        var text = empty.GetVisualDescendants().OfType<TextBlock>().First(block => block.IsEffectivelyVisible);
        Assert.Equal(PartW.EmptyText(PartW.Workspace), text.Text);
        Assert.Equal(TextAlignment.Center, text.TextAlignment);
        Assert.Equal(palette.TextSecondary, (text.Foreground as ISolidColorBrush)?.Color);
        var add = Control<Button>(app, "DashboardEmptyAddRepository");
        var tabs = Control<Button>(app, "DashboardEmptyAddOpenTabs");
        Assert.True(add.IsEffectivelyEnabled, "The empty state's Add repository… is disabled.");
        Assert.False(tabs.IsEffectivelyEnabled, "The empty state's Add open tabs is enabled with no tab open.");
        Assert.Equal(PartW.NoTabHasARepositoryOpen, TipOf(tabs));
        Assert.True(ToolTip.GetShowOnDisabled(tabs), "Add open tabs does not show its reason while disabled (WD21).");
        Assert.True(Math.Abs(app.BoundsOf(add).Center.Y - app.BoundsOf(tabs).Center.Y) <= 0.5 && app.BoundsOf(add).Right < app.BoundsOf(tabs).Left, "The empty state's buttons are not side by side.");
        Assert.True(app.BoundsOf(add).Top > app.BoundsOf(text).Bottom - 0.5, "The empty state's buttons are not below its line.");
    }

    /// <summary>
    /// The scan's checklist in its done state (1.9, W1 step 5; WD30): the wide card, 460 wide, x 320
    /// to 780; its title; <c>Found 9 repositories in {root}.</c> with every drawn line but the last
    /// ending with a separator or a space, none <c>C:</c> alone (13.2); no progress, stop or failure
    /// text; the nine rows by relative path, api already in, the other eight checked; eight in view,
    /// web-app below them; <c>Cancel</c> and <c>Add 8 repositories</c>.
    /// </summary>
    private static void AssertChecklist(ShellDriver app, AddRepositoriesSnapshot checklist, TempWorkspace copy)
    {
        Assert.Equal(PartW.AddRepositoriesTitle(PartW.Workspace), checklist.Title);
        var view = app.TopDialogView!;
        Assert.Equal(PartW.AddRepositoriesTitle(PartW.Workspace), ((TextBlock)app.FindByAutomationId(view, "AddRepositoriesTitle")).Text);
        Assert.True(Math.Abs(checklist.CardBounds.Left - PartW.ChecklistLeft) <= PartW.Tolerance && Math.Abs(checklist.CardBounds.Width - PartW.ChecklistWidth) <= PartW.Tolerance, $"The checklist's card is at {checklist.CardBounds}, not 460 wide from x 320.");
        Assert.Equal(PartW.FoundIn(9, copy.Root), checklist.FolderText);
        var lines = checklist.FolderLines.Select(line => line.Text).ToList();
        Assert.All(lines.Take(lines.Count - 1), line => Assert.True(line.EndsWith(Path.DirectorySeparatorChar) || line.EndsWith('/') || line.EndsWith(' '), $"The folder's line \"{line}\" ends inside a name (13.2)."));
        Assert.All(lines, line => Assert.False(OperatingSystem.IsWindows() && line.Trim().Length == 2 && line.Trim()[1] == ':', $"The folder's line \"{line}\" is a drive alone (13.2)."));
        Assert.Equal(checklist.FolderText, string.Concat(lines));
        Assert.Null(checklist.ProgressText);
        Assert.Null(checklist.StoppedText);
        Assert.Null(checklist.FailedText);
        Assert.Equal(PartW.Checklist, checklist.Rows.Select(row => row.Name));
        foreach (var (row, index) in checklist.Rows.Select((row, index) => (row, index)))
        {
            Assert.True(Math.Abs(row.Bounds.Height - PartW.ChecklistRowHeight) <= 0.5, $"The checklist's row {row.Name} is {row.Bounds.Height} high, not 28.");
            Assert.Equal(row.Name, row.AutomationName);
            Assert.Equal(index < 8, row.InView);
            if (row.Name == "api")
            {
                Assert.Equal((PartW.AlreadyInThisWorkspace, true, false), (row.Detail, row.IsChecked, row.IsEnabled));
            }
            else
            {
                Assert.Equal((row.Name, true, true), (row.Detail, row.IsChecked, row.IsEnabled));
            }
        }

        Assert.True(checklist.ListBounds is { } list && Math.Abs(list.Height - PartW.ChecklistListHeight) <= 0.5, $"The checklist's list is {checklist.ListBounds}, not 232 high.");
        Assert.Equal((PartW.Add8Repositories, true), (checklist.ConfirmText, checklist.ConfirmEnabled));
        Assert.Equal([PartW.Cancel, PartW.Add8Repositories], DialogButtons(app, view));
    }

    /// <summary>The visible buttons of a dialog's card, left to right, by their labels.</summary>
    private static List<string> DialogButtons(ShellDriver app, Control view) =>
    [
        .. view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && button.Content is string)
            .OrderBy(button => app.BoundsOf(button).X)
            .Select(button => (string)button.Content!),
    ];
}
