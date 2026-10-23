// Part W's real-window steps (docs/test-reports/phase-6.md, part W, the RW steps in the Notes of W1,
// W2, W4 and W9): W1 part A, steps 1 to 7, with Windows' own folder dialog answering both of the
// app's requests; W2 steps 1 and 3 (the ten rows, a row menu by a real right-click); W4 steps 1 to 3
// (Fetch all over the folder server in the test process, three fetches held at 25, 75 and 45
// percent); W9 steps 1 to 5 as a real restart of the process. Each launch on a fresh copy of "Web"
// and a new data folder at 1100×700, dark. The rows, the header and the status bar are read through
// UI Automation (a row by its name, its cells by id); the cells' tones and tooltips are the scripted
// run's. Show in folder and Copy path are never clicked.
using VisualCommit.Core;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase6RealWindowPass
{
    /// <summary>
    /// Check W1, part A, steps 1 to 7, in a real process on a fresh data folder: the welcome page's
    /// Workspaces section with none (<c>w1a-welcome-workspaces</c>); <c>New workspace…</c> and "Web"
    /// typed on the real keyboard (<c>w1b-new-workspace-dialog</c>); Create: the empty dashboard in the
    /// empty tab's place (<c>w1c-empty-dashboard</c>); <c>Add repository…</c> answered with api's folder
    /// in Windows' folder dialog, added and read with no F5 (<c>w1d-api-added</c>); <c>Add repository…</c>
    /// answered with the copy's root, scanned on the real disk (<c>w1e-scan-checklist</c>, within D33's
    /// 3 percent though its temporary folder differs); <c>Add 8 repositories</c>
    /// (<c>w1f-nine-rows</c>); "+" and the new tab's welcome page listing "Web"
    /// (<c>w1g-welcome-lists-web</c>). Step 8 and part B are scripted only (the report says why).
    /// </summary>
    [Fact]
    public async Task Check_W1_workspace_from_the_welcome_page()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var data = Run.NewDataDirectory();
        var paths = new AppPaths(data);
        var w0 = W0Of(copy);
        var title = PartW.AddRepositoryFolderTitle(PartW.Workspace);

        // Step 1: no workspaces.json and no session: one empty tab, its welcome page.
        using var app = Sized(RealApp.Launch(data));
        app.WaitForGitFound(LoadTimeout);
        app.WaitForStripTabs(StepTimeout, PartW.NewTab);
        Assert.False(app.StripTabs()[0].IsDashboard, "The empty tab is a dashboard's.");
        Assert.Empty(app.ShownElements("CloseTabButton"));
        AssertWelcome(app, workspace: null);
        foreach (var button in new[] { "FetchButton", "PullButton", "PushButton", "BranchButton", "StashButton", "PopButton", "UndoButton", "RedoButton", "SearchButton" })
        {
            AssertButton(app, button, false, helpText: null);
        }

        AssertButton(app, "SettingsButton", true, PartW.SettingsToolTip);
        AssertButton(app, "ThemeSwitch", true, PartW.ThemeToLight);
        Assert.False(File.Exists(paths.WorkspacesFile), "workspaces.json exists before anything changed (1.3).");
        AssertOwnGitConfiguration(app);
        Compare(app, PartW.W1aWelcomeWorkspaces);

        // Step 2: New workspace…: the name dialog, read before the typing; then "Web" typed key by key (WD9).
        app.ClickWithMouse(app.Find("NewWorkspaceButton"));
        app.WaitForText("NameDialogTitle", PartW.NewWorkspaceTitle, StepTimeout);
        Assert.Empty(app.ShownElements("NameDialogContext"));
        Assert.Empty(app.ShownElements("DialogRepository"));
        app.WaitFor(() => app.HasFocusOnScreen("NameBox"), "the name box to have the keyboard focus", StepTimeout);
        Assert.Equal(string.Empty, app.BoxText("NameBox"));
        Assert.Equal(PartW.NamePlaceholder, app.Find("NameBox").Name);
        var create = app.Find("ConfirmButton");
        Assert.Equal(PartW.Create, create.Name);
        Assert.False(create.IsEnabled, "Create is enabled with an empty name.");
        Assert.Empty(app.ShownElements("NameError"));
        app.TypeText(PartW.Workspace);
        app.WaitFor(() => app.BoxText("NameBox") == PartW.Workspace, $"the name box to hold \"{PartW.Workspace}\"", StepTimeout);
        app.WaitFor(() => app.Find("ConfirmButton").IsEnabled, "Create to be enabled for \"Web\"", StepTimeout);
        Assert.Empty(app.ShownElements("NameError"));
        Assert.Equal([PartW.Cancel], app.NamesOnScreen("CancelButton"));
        Compare(app, PartW.W1bNewWorkspaceDialog);

        // Step 3: Create: the empty tab became the dashboard, empty.
        app.ClickWithMouse(app.Find("ConfirmButton"));
        app.WaitUntilGone("NameDialog", StepTimeout);
        app.WaitFor(() => app.IsDashboardShown, "the new workspace's dashboard", StepTimeout);
        app.WaitForStripTabs(StepTimeout, PartW.Workspace);
        var tab = app.StripTabs()[0];
        Assert.True(tab.IsDashboard, "The new workspace's tab is no dashboard's.");
        Assert.Equal(PartW.ItemStatusWorkspace, tab.ItemStatus);
        AssertDashboardHeader(app, PartW.Workspace, PartW.Count(0), fetchPullReason: PartW.NoRepositories(PartW.Workspace));
        Assert.Empty(app.ShownElements("DashboardList"));
        Assert.Empty(app.DashboardTextsBetween((float)PartW.HeaderBottom, (float)PartW.HeadingsBottom));
        Assert.Contains(PartW.EmptyText(PartW.Workspace), app.Texts());
        Assert.True(app.Find("DashboardEmptyAddRepository").IsEnabled, "The empty state's Add repository… is disabled.");
        Assert.False(app.Find("DashboardEmptyAddOpenTabs").IsEnabled, "The empty state's Add open tabs is enabled with no tab open.");
        AssertDashboardToolbar(app, PartW.Workspace, reason: PartW.NoRepositories(PartW.Workspace));
        AssertDashboardStatusBar(app, PartW.Workspace, PartW.Ready);
        AssertActivityToggle(app, enabled: false, PartW.NoRepositories(PartW.Workspace));
        var id = WaitForWorkspacesFile(app, data, id: null, PartW.Workspace, []);
        WaitForSession(app, data, [new TabState(null, id)], activeTab: 0);
        Compare(app, PartW.W1cEmptyDashboard);

        // Step 4: Add repository… answered with api's folder in Windows' folder dialog: appended at once, selected, read with no F5.
        var mark = app.LogMark();
        app.ClickWithMouse(app.Find("DashboardEmptyAddRepository"));
        app.ChooseFolderInDialog(title, copy.PathOf("api"), StepTimeout);
        WaitForPass(app, mark, 1);
        AssertNoDialog(app);
        Assert.Empty(app.ShownElements("DashboardEmptyAddRepository"));
        AssertHeadings(app);
        AssertRows(app, [w0["api"]], selected: "api");
        AssertTd(app, PartW.Count(1));
        AssertActivityToggle(app, enabled: true, PartW.ShowActivityLog);
        WaitForWorkspacesFile(app, data, id, PartW.Workspace, [copy.PathOf("api")]);
        Compare(app, PartW.W1dApiAdded);

        // Step 5: Add repository… answered with the copy's root: no repository, so scanned on the disk; the checklist done.
        mark = app.LogMark();
        app.ClickWithMouse(app.Find("DashboardAddRepository"));
        app.ChooseFolderInDialog(title, copy.Root, StepTimeout);
        app.WaitFor(
            () => app.NamesOnScreen("ScanFolderText") is [var text] && text.StartsWith("Found", StringComparison.Ordinal),
            "the checklist's search to end",
            OperationTimeout);
        AssertChecklist(app, copy);
        WaitForWorkspacesFile(app, data, id, PartW.Workspace, [copy.PathOf("api")]);
        var opened = app.LogLinesSince(mark).Count(line => line.Contains(PartW.OpenedTheRepositoryAt, StringComparison.Ordinal));
        Assert.True(opened == 9, $"The scan logged {opened} \"{PartW.OpenedTheRepositoryAt}…\" lines, not 9 (1.8).");
        Compare(app, PartW.W1eScanChecklist);

        // Step 6: Add 8 repositories: WA, api still selected (WD1), the eight read with no F5.
        mark = app.LogMark();
        app.ClickWithMouse(app.FindIn(app.ShownElements("AddRepositoriesDialog")[0], "ConfirmButton"));
        app.WaitUntilGone("AddRepositoriesDialog", StepTimeout);
        WaitForPass(app, mark, 8);
        AssertRows(app, [.. PartW.WaOrder.Select(name => w0[name])], selected: "api");
        AssertTd(app, PartW.Count(9));
        WaitForWorkspacesFile(app, data, id, PartW.Workspace, [.. PartW.WaOrder.Select(copy.PathOf)]);
        Compare(app, PartW.W1fNineRows);

        // Step 7: "+": a new tab whose welcome page lists "Web".
        app.ClickWithMouse(app.Find("AddRepoButton"));
        app.WaitForStripTabs(StepTimeout, PartW.Workspace, PartW.NewTab);
        app.WaitFor(() => app.ShownElements("NewWorkspaceButton").Count == 1, "the new tab's welcome page", StepTimeout);
        Assert.Equal([true, false], app.StripTabs().Select(candidate => candidate.IsDashboard));
        AssertWelcome(app, workspace: PartW.Workspace);
        WaitForSession(app, data, [new TabState(null, id), new TabState(null)], activeTab: 1);
        Compare(app, PartW.W1gWelcomeListsWeb);

        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Check W2, steps 1 and 3, in a real process: the dashboard of the ten rows started from the
    /// files the pass writes (<see cref="RealApp.WriteWorkspaces"/>, the session of its dashboard),
    /// the rows' texts read through UI Automation (<c>w2a-ten-rows</c>); web-app's menu by a real
    /// right-click, its items read (<c>w2c-row-menu</c>). Nothing runs in git. Steps 2 and 4 to 8 are
    /// scripted only (the report says why).
    /// </summary>
    [Fact]
    public async Task Check_W2_ten_repositories()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var exact = await ExactStatesAsync(copy);
        using var app = LaunchOnWorkspace(copy);

        // Step 1: W0, no row selected, the one pass of the ten (WD27), the nine summary reads.
        AssertDashboardHeader(app, PartW.Workspace, PartW.Count(10));
        AssertHeadings(app);
        AssertRows(app, PartW.W0(copy.PathOf("old-site")));
        AssertTd(app, PartW.Count(10));
        AssertActivityToggle(app, enabled: false, PartW.SelectRepository);
        Assert.Equal([PartW.Workspace], app.StripTabs().Select(tab => tab.Title));
        foreach (var name in PartW.WithFolder)
        {
            AssertOneLogLine(app, 0, "] " + PartW.SummaryRead(name));
        }

        Assert.DoesNotContain(app.LogLines(), line => line.Contains("] " + PartW.SummaryRead("old-site"), StringComparison.Ordinal));
        AssertOwnGitConfiguration(app);
        Compare(app, PartW.W2aTenRows);

        // Step 3: web-app's row menu by a real right-click (2.7), which selects the row.
        app.MoveToNeutral();
        var mark = app.LogMark();
        var point = app.DashboardRowPoint("web-app");
        Assert.True(Math.Abs(point.Y - PartW.RowCentre(1)) <= PartW.Tolerance, $"web-app's row is centred at y {point.Y}, not {PartW.RowCentre(1)}.");
        app.RightClickAt(point.X, point.Y);
        var menu = AssertMenu(app, PartW.ReadyRowMenu("web-app"), MenuHeight(8, 3));
        AssertFocusedItem(app, PartW.Open);
        Assert.True(
            Math.Abs(menu.Left - (point.X + WindowLayout.MenuPointerGap)) <= PartW.Tolerance && Math.Abs(menu.Top - (point.Y + WindowLayout.MenuPointerGap)) <= PartW.Tolerance,
            $"web-app's menu is at {menu}; its top-left corner should be 2 right of and 2 below the pointer at {point}.");
        app.WaitFor(() => app.DashboardRow("web-app").IsSelected, "web-app's row to be selected by the right-click", StepTimeout);
        AssertOneLogLine(app, mark, PartW.MenuOpened("web-app"));
        Compare(app, PartW.W2cRowMenu, keepPointer: true);
        CloseMenuWithEscape(app);

        Assert.Equal(0, app.Close());
        await AssertExactStatesAsync(copy, exact);
    }

    /// <summary>
    /// Check W4, steps 1 to 3, in a real process: docs, mobile and design-system fetched over HTTP
    /// from the folder server in the test process (47393 or its fallback, which run.txt names), each
    /// held at its percentage; <c>DashboardFetchAll</c> clicked with the real mouse; the three rows'
    /// percentages and Cancel, the five others fetched, the run's text, bar and Cancel all in the
    /// header and the status bar read through UI Automation (<c>w4a-held-at-25-75-45</c>); released,
    /// the run's result and WF (<c>w4b-fetched</c>).
    /// </summary>
    [Fact]
    public async Task Check_W4_fetch_all_progress()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await using var server = await Scenarios.ServeWorkspaceAsync(copy, requireAuth: false, "docs", "mobile", "design-system");
        NotePort(server);
        var holds = PartW.W4Holds.Select(hold => server.HoldNextFetchPack(hold.Name, hold.Percent)).ToArray();
        try
        {
            using var app = LaunchOnWorkspace(copy);
            var w0 = PartW.W0(copy.PathOf("old-site"));

            // Step 1: W0; the three origins on the folder server, the others on the file remotes.
            AssertRows(app, w0);
            foreach (var name in PartW.WithRemote)
            {
                using var repo = copy.Repo(name);
                var url = (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim();
                var expected = PartW.W4Holds.Any(hold => hold.Name == name)
                    ? WithPort($"http://127.0.0.1:47393/{name}.git", server.Port)
                    : $"../remotes/{name}.git";
                Assert.Equal(expected, url);
            }

            AssertTd(app, PartW.Count(10));

            // Step 2: Fetch all by a real click; the three holds taken, the five others fetched.
            var mark = app.LogMark();
            app.ClickWithMouse(app.Find("DashboardFetchAll"));
            await Task.WhenAll(holds).WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
            foreach (var (name, percent) in PartW.W4Holds)
            {
                app.WaitForDashboardRow(name, row => row.Operation == PartW.FetchingAt(percent), $"Operation \"{PartW.FetchingAt(percent)}\"", NetworkTimeout);
            }

            foreach (var name in new[] { "api", "web-app", "billing", "infra", "scratch" })
            {
                app.WaitForDashboardRow(name, row => row.Operation == PartW.FetchedAll && !row.ShowsCancel, $"Operation \"{PartW.FetchedAll}\"", NetworkTimeout);
            }

            AssertOneLogLine(app, mark, PartW.FetchStarted);
            var held = PartW.W4Holds.ToDictionary(hold => hold.Name, hold => hold.Percent, StringComparer.Ordinal);
            AssertRows(
                app,
                [.. w0.Select(row => row.Name switch
                {
                    "docs" or "mobile" or "design-system" => row with { Operation = new PartW.Cell(PartW.FetchingAt(held[row.Name]), PartW.Secondary, PartW.FetchingAtToolTip(held[row.Name])) },
                    "notes" => row with { Operation = PartW.Reason(PartW.NoRemotesText) },
                    "old-site" => row,
                    _ => row with { Operation = new PartW.Cell(PartW.FetchedAll, PartW.Secondary) },
                })],
                cancel: ["docs", "mobile", "design-system"]);
            Assert.DoesNotContain(app.DashboardRows(), row => row.Operation == PartW.WaitingToFetch);
            var running = PartW.Fetching(8, 5);
            var percentDone = PartW.Percent(5, 8);
            AssertDashboardToolbar(app, PartW.Workspace, reason: PartW.WaitForFetchAll);
            AssertDashboardHeader(app, PartW.Workspace, PartW.Count(10), runText: running, progress: percentDone, cancelAll: true, fetchPullReason: PartW.WaitForFetchAll);
            AssertDashboardStatusBar(app, PartW.Workspace, running, progress: percentDone, cancel: true);
            AssertNoDialog(app);
            await AssertRefsAsync(copy, PartW.W0Refs);
            Compare(app, PartW.W4aHeldAt257545);

            // Step 3: released: the run's result, WF.
            server.Release();
            WaitTelling(
                app,
                () => app.DashboardHeaderText("DashboardBulkText") == PartW.Fetched8Skipped2 && app.ShownElements("DashboardCancelAll").Count == 0 && !app.DashboardRows().Any(row => row.ShowsCancel),
                $"the run to end with \"{PartW.Fetched8Skipped2}\"",
                () => $"The header reads \"{app.DashboardHeaderText("DashboardBulkText")}\"; the rows: {app.DescribeDashboardRows()}.",
                NetworkTimeout);
            AssertRows(
                app,
                [.. w0.Select(row => row.Name switch
                {
                    "notes" => row with { Operation = PartW.Reason(PartW.NoRemotesText) },
                    "old-site" => row,
                    _ => row with { Operation = new PartW.Cell(PartW.FetchedAll, PartW.Secondary), AheadBehind = PartW.WfAheadBehind.GetValueOrDefault(row.Name) ?? row.AheadBehind },
                })]);
            AssertTd(app, PartW.Count(10), PartW.Fetched8Skipped2);
            AssertOneLogLine(app, mark, PartW.FetchEnded(PartW.Fetched8Skipped2));
            AssertNoDialog(app);
            Compare(app, PartW.W4bFetched);

            Assert.Equal(0, app.Close());
            await AssertRefsAsync(copy, PartW.WfRefs);
        }
        finally
        {
            server.Release();
        }
    }

    /// <summary>
    /// Check W9, steps 1 to 5, as a real restart: the files the pass writes, api opened by a real
    /// double-click on its row (<c>w9a-api-opened</c>); the app closed; <c>session.json</c> and
    /// <c>workspaces.json</c> read between the launches; the app launched again on the same data
    /// folder, writing nothing: the dashboard restored first and in the background, api active, no
    /// <c>Web: read</c> line in the new process's log (<c>w9b-restarted</c>); a real click on the Web
    /// tab: one pass of the nine rows without a tab (<c>w9c-dashboard-read</c>).
    /// </summary>
    [Fact]
    public async Task Check_W9_restart()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        string data;

        // First start. Step 1, then step 2: api opened by a real double-click on its row.
        using (var app = LaunchOnWorkspace(copy))
        {
            data = app.DataDirectory;
            app.DoubleClickDashboardRow("api");
            app.WaitForLogLine(Loaded("api"), LoadTimeout);
            app.WaitForLogLine(AppLogFile.WorkingTreeRead("api"), LoadTimeout);
            app.WaitForStripTabs(StepTimeout, PartW.Workspace, "api");
            Assert.Equal([true, false], app.StripTabs().Select(tab => tab.IsDashboard));
            app.WaitFor(() => !app.IsDashboardShown, "api's tab to be active", StepTimeout);
            app.WaitForText("CurrentBranch", "main", StepTimeout);
            Compare(app, PartW.W9aApiOpened);

            // Step 3: the app closes.
            Assert.Equal(0, app.Close());
        }

        // Between the launches: the session and the workspaces as the first process left them (1.11, 1.3).
        var session = SavedSession(data) ?? throw new Xunit.Sdk.XunitException("session.json cannot be read after the first start.");
        Assert.Equal(2, session.Tabs.Count);
        Assert.Equal(new TabState(null, PartW.WorkspaceId), session.Tabs[0]);
        Assert.True(session.Tabs[1].WorkspaceId is null && session.Tabs[1].RepositoryPath is { } saved && SameFolder(copy.PathOf("api"), saved), $"session.json's second tab is {session.Tabs[1]}, not api's.");
        Assert.Equal(1, session.ActiveTab);
        var workspaces = SavedWorkspaces(data) ?? throw new Xunit.Sdk.XunitException("workspaces.json cannot be read after the first start.");
        Assert.Equal(1, workspaces.Version);
        var web = Assert.Single(workspaces.Workspaces);
        Assert.Equal((PartW.WorkspaceId, PartW.Workspace), (web.Id, web.Name));
        Assert.Equal(PartW.Names.Select(copy.PathOf), web.Paths);

        // Second start, on the same data folder, writing nothing. Step 4: the dashboard restored
        // first, in the background; api active; nothing read by the dashboard. The data folder's log
        // holds the first process's lines: the second's are those after them.
        var startMark = AppLogFile.ReadLines(data).Count;
        using (var app = Relaunch(data, "api"))
        {
            app.WaitForStripTabs(StepTimeout, PartW.Workspace, "api");
            Assert.Equal([true, false], app.StripTabs().Select(tab => tab.IsDashboard));
            Assert.False(app.IsDashboardShown, "The restored dashboard is the active tab.");
            app.WaitForText("CurrentBranch", "main", StepTimeout);
            Assert.DoesNotContain(app.LogLinesSince(startMark), line => AnyPass.IsMatch(line));
            Compare(app, PartW.W9bRestarted);
            Assert.DoesNotContain(app.LogLinesSince(startMark), line => AnyPass.IsMatch(line));

            // Step 5: a real click on the Web tab: one pass of the nine rows without a tab (old-site's folder check among them).
            var mark = app.LogMark();
            app.ClickWithMouse(app.TabTitled(PartW.Workspace).Element);
            app.WaitFor(() => app.IsDashboardShown, "the dashboard to be active", StepTimeout);
            WaitForPass(app, mark, 9);
            AssertRows(app, PartW.W0(copy.PathOf("old-site")));
            AssertTd(app, PartW.Count(10));
            Compare(app, PartW.W9cDashboardRead);

            Assert.Equal(0, app.Close());
        }
    }

    // ---- Part W's helpers ----

    /// <summary>
    /// Starts the real app again on <paramref name="data"/>, writing no session, so that the session
    /// the last process saved opens its tabs (a real restart, W9), and waits until the repository
    /// <paramref name="repository"/> has opened in the new process: the data folder's log already
    /// holds the last process's lines, so the new process's are counted after them.
    /// </summary>
    private RealApp Relaunch(string data, string repository)
    {
        var lines = AppLogFile.ReadLines(data);
        var loadsBefore = lines.Count(line => Loaded(repository).IsMatch(line));
        var readsBefore = lines.Count(line => AppLogFile.WorkingTreeRead(repository).IsMatch(line));
        return Opened(Sized(RealApp.Launch(data)), repository, loadsBefore, readsBefore);
    }

    /// <summary>
    /// The welcome page of the active empty tab (13.1, W1 steps 1 and 7) as UI Automation reads it:
    /// its texts (those inside buttons are the buttons' names), <c>New workspace…</c> named <c>New
    /// workspace</c> with its help text, and, with no workspace, <c>No workspaces</c>; with
    /// <paramref name="workspace"/>, its one item named by it and no <c>No workspaces</c>.
    /// </summary>
    private static void AssertWelcome(RealApp app, string? workspace)
    {
        var texts = app.Texts();
        var expected = workspace is null ? PartW.WelcomeWithoutWorkspaces : PartW.WelcomeWithWeb;
        var inButtons = new[] { "Open", "Clone", "Init", PartW.NewWorkspace, PartW.Workspace, PartW.Count(9) };
        Assert.All(expected.Where(text => !inButtons.Contains(text)), text => Assert.Contains(text, texts));
        Assert.Equal(["Open", "Clone", "Init"], new[] { "OpenRepoButton", "CloneRepoButton", "InitRepoButton" }.Select(id => app.Find(id).Name));
        Assert.Equal([PartW.WorkspacesHeading], app.NamesOnScreen("WorkspacesHeading"));
        Assert.Empty(app.ShownElements("WorkspacesNotice"));
        var button = app.Find("NewWorkspaceButton");
        Assert.Equal((PartW.NewWorkspaceName, true, PartW.NewWorkspaceToolTip), (button.Name, button.IsEnabled, RealApp.HelpTextOf(button)));
        if (workspace is null)
        {
            Assert.Equal([PartW.NoWorkspaces], app.NamesOnScreen("NoWorkspacesText"));
            Assert.Empty(app.ShownElements("WorkspaceItem"));
        }
        else
        {
            Assert.Empty(app.ShownElements("NoWorkspacesText"));
            Assert.Equal([workspace], app.NamesOnScreen("WorkspaceItem"));
        }
    }

    /// <summary>
    /// The scan's checklist in its done state (1.9, W1 step 5) as UI Automation reads it: its title;
    /// <c>Found 9 repositories in {root}.</c>, the folder the one the dialog answered (git's and the
    /// dialog's spellings matched as folders); no progress, stop or failure text; the nine rows by
    /// relative path, api's box checked and disabled, the other eight checked and enabled;
    /// <c>Cancel</c> and <c>Add 8 repositories</c>, enabled. The drawn lines of the folder (13.2) are
    /// the scripted run's.
    /// </summary>
    private static void AssertChecklist(RealApp app, TempWorkspace copy)
    {
        var dialog = Assert.Single(app.ShownElements("AddRepositoriesDialog"));
        Assert.Equal(PartW.AddRepositoriesTitle(PartW.Workspace), app.FindIn(dialog, "AddRepositoriesTitle").Name);
        var folderText = app.FindIn(dialog, "ScanFolderText").Name;
        const string Start = "Found 9 repositories in ";
        Assert.True(
            folderText.StartsWith(Start, StringComparison.Ordinal) && folderText.EndsWith('.') && SameFolder(copy.Root, folderText[Start.Length..^1]),
            $"The checklist's folder line is \"{folderText}\", not \"{PartW.FoundIn(9, copy.Root)}\".");
        foreach (var id in new[] { "ScanProgressText", "ScanStoppedText", "ScanFailedText" })
        {
            Assert.Empty(app.ShownElements(id));
        }

        // In the list's order, as UI Automation lists them: the ninth row lies below the eight in
        // view (WD30), where its place may not be reported, so the rows are not ordered by place.
        var boxes = dialog.FindAllDescendants(condition => condition.ByAutomationId("FoundRepositoryCheck")).ToList();
        Assert.Equal(PartW.Checklist, boxes.Select(box => box.Name));
        foreach (var box in boxes)
        {
            var api = box.Name == "api";
            Assert.True(RealApp.IsOn(box), $"{box.Name}'s box is not checked.");
            Assert.True(box.IsEnabled != api, $"{box.Name}'s box is {(box.IsEnabled ? "enabled" : "disabled")}.");
        }

        Assert.Contains(PartW.AlreadyInThisWorkspace, RealApp.TextsIn(dialog));
        var confirm = app.FindIn(dialog, "ConfirmButton");
        Assert.Equal((PartW.Add8Repositories, true), (confirm.Name, confirm.IsEnabled));
        Assert.Equal(PartW.Cancel, app.FindIn(dialog, "CancelButton").Name);
    }
}
