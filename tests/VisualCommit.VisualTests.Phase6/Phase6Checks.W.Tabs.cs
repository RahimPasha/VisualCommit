// Check W8 of part W (docs/test-reports/phase-6.md): rows opening into tabs (one queue each: the
// tabs adopt the rows' view models), a commit in a tab updating its row with no read, the
// dashboard's reads under the session's manual read clock (2.10's 10 s rule, Q4), and the activity
// log following the selected row.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Shell;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksW
{
    /// <summary>
    /// Check W8: web-app opened by a double-click after the dashboard, docs by Enter after web-app,
    /// each tab adopting its row's view model; mobile's held Fetch from its row menu, then mobile
    /// opened: the fetch goes on in the tab and ends there, one Fetch record; a commit in web-app's
    /// tab updates its row with no read; under the session's manual read clock a file written in
    /// scratch shows only once 10 s have passed and the window is activated; the activity log
    /// following the selected row, and its reasons.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_W8_rows_open_into_tabs_and_reads()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");

        // Step 1: the hold in mobile; the session's read clock a manual one (17.2, W8 alone).
        using var hold = FetchHold.Install(data.Path);
        try
        {
            hold.HoldIn(copy.PathOf("mobile"));
            var clock = new ManualTimeProvider();
            using var app = await OpenWorkspaceAsync(data, copy, readClock: clock);
            Assert.Same(clock, app.ReadClock);
            var w0 = W0Of(copy);
            var toggle = Control<Button>(app, "ActivityLogToggle");
            var tip = await app.RestPointerOnAsync(toggle);
            var shot = Shot(app, PartW.W8aSelectARepository, keepPointer: true);
            Assert.Equal(PartW.SelectRepository, tip);
            AssertActivityToggle(app, enabled: false, PartW.SelectRepository);
            AssertToolTipInWindow(app, shot);
            AssertRows(app, copy, [.. w0.Values]);
            AssertTd(app, PartW.Workspace, PartW.Count(10));

            // Step 2: a double-click opens web-app after the dashboard (2.3), its tab adopting the row's view model (3.3).
            app.MoveToNeutral();
            var webAppModel = RowModel(app, "web-app");
            app.DoubleClick(RowPoint(app, "web-app"));
            var webApp = await app.WaitForRepositoryAsync(OperationChainTimeout);
            var picture = Shot(app, PartW.W8bWebAppOpened);
            Assert.Equal([PartW.Workspace, "web-app"], TabTitles(app));
            Assert.Same(app.Session.Shell.Tabs[1], app.Session.Shell.ActiveTab);
            AssertAdopted(app, copy, webAppModel, webApp, "web-app");
            AssertGraph(app, Rows(PartW.WebApp0), lanes: 1, picture: picture);
            Assert.True(app.Window.GetVisualDescendants().OfType<LeftPanelView>().Any(panel => panel.IsEffectivelyVisible), "web-app's tab shows no left panel.");
            Assert.True(app.Window.GetVisualDescendants().OfType<RightPanelView>().Any(panel => panel.IsEffectivelyVisible), "web-app's tab shows no details panel.");
            Assert.False(app.FindByAutomationId("StatusWorkspaceIcon").IsEffectivelyVisible, "A repository's tab shows the workspace's icon.");
            AssertBranch(app, "main");
            AssertStatus(app, PartW.Ready);
            Assert.True(SameFolder(copy, copy.PathOf("web-app"), ReadSession(data).Recent[0].Path), "The recent list's top is not web-app.");

            // Step 3: the Web tab, then docs clicked and Enter: docs after web-app.
            app.Click(app.TabStrip().Tabs[0].Bounds.Center);
            await app.WaitForAsync(() => app.FocusIsWithin(app.Find<ListBox>("DashboardList")), "the list to take the focus when the dashboard became active");
            app.Click(RowPoint(app, "docs"));
            Assert.Equal("docs", app.DashboardSnapshot().SelectedRow);
            var docsModel = RowModel(app, "docs");
            app.PressKey(Key.Enter);
            var docs = await app.WaitForRepositoryAsync(OperationChainTimeout);
            picture = Shot(app, PartW.W8cDocsOpened);
            Assert.Equal([PartW.Workspace, "web-app", "docs"], TabTitles(app));
            Assert.Same(app.Session.Shell.Tabs[2], app.Session.Shell.ActiveTab);
            AssertAdopted(app, copy, docsModel, docs, "docs");
            AssertGraph(app, Rows(PartW.Docs0), lanes: 1, picture: picture);

            // Step 4: mobile's own Fetch from its row menu, held: no run (4.7, WD3).
            app.Click(app.TabStrip().Tabs[0].Bounds.Center);
            app.Settle();
            var menu = await app.RowMenuAsync("mobile");
            Assert.Equal("mobile", menu.Header);
            var mobileModel = RowModel(app, "mobile");
            app.ClickMenuItem(PartW.Fetch);
            await hold.WaitForHeldAsync(1);
            await app.WaitForRowAsync("mobile", row => row.Operation.Text == PartW.FetchingAll);
            app.MoveToNeutral();
            Shot(app, PartW.W8dRowFetching);
            Assert.Equal("mobile", app.DashboardSnapshot().SelectedRow);
            AssertRows(app, copy, [w0["mobile"] with { Operation = Shown(PartW.FetchingAll) }], snapshot: OnlyRow(app, "mobile"), cancel: ["mobile"]);
            AssertRowButton(app, "mobile", "DashboardRowCancel");
            var markers = Directory.GetFiles(hold.Folder, FetchHold.MarkerPrefix + "*").Select(Path.GetFileName).OfType<string>().ToList();
            Assert.True(markers.Count == 1 && markers[0].StartsWith(FetchHold.MarkerPrefix + "mobile.git.", StringComparison.Ordinal), "The held fetches are not mobile's one: " + string.Join(", ", markers));
            AssertDashboardHeader(app, ExpectedDashboardHeader.Idle(PartW.Workspace, PartW.Count(10)));
            AssertDashboardStatusBar(app, PartW.Workspace, PartW.Ready);
            Assert.DoesNotContain(CallsIn(app, copy, 0, "mobile"), call => call.CommandText == PartW.FetchCommand);

            // Step 5: mobile opened while its fetch runs: the tab adopts the running fetch (3.3).
            app.DoubleClick(RowPoint(app, "mobile"));
            var mobileTab = app.Session.Shell.ActiveTab;
            await app.WaitForAsync(
                () => mobileTab.Repository is { } repository && !ReferenceEquals(repository.Graph, CommitGraphData.Empty) && repository.Graph.IsComplete
                    && app.Window.GetVisualDescendants().OfType<CommitGraphControl>().FirstOrDefault(graph => graph.Name == "GraphRows") is { } control && ReferenceEquals(control.Data, repository.Graph),
                "mobile's graph to be drawn",
                OperationChainTimeout);
            picture = Shot(app, PartW.W8eFetchInTheTab);
            Assert.Equal([PartW.Workspace, "web-app", "docs", "mobile"], TabTitles(app));
            Assert.Same(app.Session.Shell.Tabs[3], mobileTab);
            Assert.Same(mobileModel, mobileTab.Repository);
            Assert.False(mobileModel!.IsSummary, "mobile's tab holds a summary-mode view model.");
            AssertGraph(app, Rows(PartW.Mobile0), lanes: 2, picture: picture);
            AssertBranch(app, "main");
            AssertStatus(app, PartW.FetchingAll);
            var progress = Control<ProgressBar>(app, "OperationProgress");
            Assert.True(progress.IsEffectivelyVisible && progress.IsIndeterminate, "The tab's status bar shows no indeterminate bar for the running fetch.");
            var cancel = Control<Button>(app, "OperationCancelButton");
            Assert.True(cancel.IsEffectivelyVisible, "The tab's status bar shows no Cancel for the running fetch.");
            Assert.Equal(PartW.StopGit, TipOf(cancel));
            Assert.False(app.FindByAutomationId("TabWaitingText").IsEffectivelyVisible, "mobile's tab waits for a closed queue (it should not: the queue is its own).");

            // Step 6: released: the fetch ends in the tab; one Fetch record, one fetch call.
            hold.Release();
            await app.WaitForLogLineAsync(AppLogFile.OperationEnded("mobile", PartW.Fetch, OperationOutcome.Succeeded, PartW.FetchedAll), mark: 0, timeout: NetworkTimeout);
            await app.WaitForIdleAsync(timeout: NetworkTimeout);
            AssertStatus(app, PartW.FetchedAll);
            AssertGraph(app, Rows(PartW.MobileFetched), lanes: 2);
            var main = app.RefRowModel("Local branches", "main") as RefItemRow ?? throw new Xunit.Sdk.XunitException("Local branches › main is no branch's row.");
            Assert.Equal((1, 3), (main.Ahead, main.Behind));
            Assert.Single(app.Session.Operations.Snapshot(), record => record.Title == PartW.Fetch && record.Repository == "mobile");
            var fetch = Assert.Single(CallsIn(app, copy, 0, "mobile"), call => call.CommandText == PartW.FetchCommand);
            Assert.Equal(0, fetch.ExitCode);

            // Step 7: in web-app's tab, contact.html written outside the app, staged and committed (WD17); then the Web tab: its row follows (3.5), no read.
            app.Click(app.TabStrip().Tabs[1].Bounds.Center);
            await app.WaitForRepositoryAsync(OperationChainTimeout);
            File.WriteAllText(Path.Combine(copy.PathOf("web-app"), PartW.ContactFile), PartW.ContactText);
            await app.WaitForAsync(() => Graph(app).ShowsWorkingRow, "web-app's graph to show the working-changes row", OutsideChangeTimeout);
            await app.WaitForIdleAsync(timeout: OutsideChangeTimeout);
            app.Click(app.WorkingRowPoint());
            app.Click(app.FindByAutomationId("StageAllButton"));
            await app.WaitForIdleAsync(timeout: OperationChainTimeout);
            app.Click(app.FindByAutomationId("CommitSummary"));
            app.Type(PartW.ContactSummary);
            app.Click(app.FindByAutomationId("CommitButton"));
            await app.WaitForIdleAsync(timeout: OperationChainTimeout);
            await app.WaitForAsync(() => !Graph(app).ShowsWorkingRow && Graph(app).Data?.Count == 4, "web-app's graph to show the commit", OutsideChangeTimeout);
            AssertGraph(app, Rows(PartW.WebAppCommitted), lanes: 1);
            using (var repo = copy.Repo("web-app"))
            {
                Assert.Equal(PartW.ContactCommit, (await repo.GitWithEnvironmentAsync(OptionalLocksOff, "rev-parse", "HEAD")).StandardOutput.Trim());
            }

            var mark = app.CallMark();
            var logMark = app.LogMark();
            app.Click(app.TabStrip().Tabs[0].Bounds.Center);
            app.Settle();
            AssertNothingReads(app);
            app.MoveToNeutral();
            Shot(app, PartW.W8fRowFollowsItsTab);
            Assert.Equal("mobile", app.DashboardSnapshot().SelectedRow);
            AssertActivityToggle(app, enabled: true, PartW.ShowActivityLog);
            var w8 = w0.Values.Select(row => row.Name switch
            {
                "web-app" => row with { AheadBehind = PartW.Arrows(3, 0) },
                "mobile" => row with { AheadBehind = PartW.Arrows(1, 3), Operation = Shown(PartW.FetchedAll) },
                _ => row,
            }).ToList();
            AssertRows(app, copy, w8, selected: "mobile");
            Assert.Empty(LinesWith(app, logMark, "summary read in"));
            Assert.Empty(PassLines(app, logMark));
            var elsewhere = app.CallsSince(mark, folder => copy.Names.Where(name => name != "web-app").Any(name => SameFolder(copy, copy.PathOf(name), folder)));
            Assert.True(elsewhere.Count == 0, "Activating the dashboard made calls outside web-app's folder:\n" + string.Join('\n', elsewhere.Select(call => $"{call.CommandText} in {call.WorkingDirectory}")));

            // Step 8: todo.txt written in scratch; the window activated: nothing read (the clock has not moved, 2.10).
            File.WriteAllText(Path.Combine(copy.PathOf("scratch"), PartW.TodoFile), PartW.TodoText);
            var mark2 = app.CallMark();
            var logMark2 = app.LogMark();
            app.ActivateWindow();
            app.Settle();
            AssertNothingReads(app);
            Assert.Empty(PassLines(app, logMark2));
            Assert.Empty(LinesWith(app, logMark2, "summary read in"));
            Assert.Empty(CallsIn(app, copy, mark2, "scratch"));
            AssertRows(app, copy, w8, selected: "mobile");

            // Step 9: the read clock 10 s on, the window activated: one pass of the seven rows without a tab (WD27, WD35).
            var mark3 = app.CallMark();
            var logMark3 = app.LogMark();
            clock.Advance(TimeSpan.FromSeconds(10));
            app.ActivateWindow();
            await app.WaitForDashboardReadAsync(since: logMark3, timeout: OperationChainTimeout);
            await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
            app.MoveToNeutral();
            Shot(app, PartW.W8gReadAfterTenSeconds);
            AssertOnePass(app, logMark3, 7);
            w8 = [.. w8.Select(row => row.Name == "scratch" ? row with { Changes = new PartW.Cell(PartW.OneUntracked, PartW.Primary, PartW.OneUntracked) } : row)];
            AssertRows(app, copy, w8, selected: "mobile");
            var scratchCalls = CallsIn(app, copy, mark2, "scratch");
            foreach (var form in PartW.SummaryReadForms)
            {
                Assert.True(scratchCalls.Any(call => call.CommandText.StartsWith(form, StringComparison.Ordinal) && call.Sequence > mark3), $"scratch's \"{form}…\" was not read after the clock moved:\n" + string.Join('\n', scratchCalls.Select(call => call.CommandText)));
            }

            Assert.Empty(CallsIn(app, copy, mark2, "docs"));
            Assert.Empty(CallsIn(app, copy, mark2, "mobile"));
            AssertActivityToggle(app, enabled: true, PartW.ShowActivityLog);

            // Step 10: mobile's row, then the toggle: the log under the list, following mobile.
            app.Click(RowPoint(app, "mobile"));
            AssertActivityToggle(app, enabled: true, PartW.ShowActivityLog);
            app.ClickActivityLogToggle();
            app.Settle();
            picture = Shot(app, PartW.W8hLogFollowsTheRow);
            Assert.Equal("mobile", app.DashboardSnapshot().SelectedRow);
            var list = app.BoundsOf(app.Find<ListBox>("DashboardList"));
            Assert.True(Math.Abs(list.Top - 152) <= PartW.Tolerance && Math.Abs(list.Bottom - 469) <= PartW.Tolerance, $"The list is at {list}, not y 152 to 469 (2.4).");
            var splitter = app.BoundsOf((Control)app.FindByAutomationId("ActivityLogSplitter"));
            Assert.True(Math.Abs(splitter.Top - 469) <= PartW.Tolerance && Math.Abs(splitter.Bottom - 474) <= PartW.Tolerance, $"The splitter is at {splitter}, not y 469 to 474.");
            var log = app.ActivityLog();
            Assert.True(Math.Abs(log.Bounds.Top - 474) <= PartW.Tolerance && Math.Abs(log.Bounds.Bottom - 674) <= PartW.Tolerance, $"The log is at {log.Bounds}, not y 474 to 674.");
            Assert.Equal(("Activity log", "mobile"), (log.Title, log.RepositoryName));
            var operation = Assert.Single(log.Rows);
            Assert.Equal(ActivityLogRowKind.Operation, operation.Kind);
            Assert.Equal(("IconCheck", PartW.MobileFetchLogRow.Time, PartW.MobileFetchLogRow.Title, PartW.MobileFetchLogRow.Message, PartW.MobileFetchLogRow.Duration), (operation.Glyph, operation.Time, operation.Title, operation.Message, operation.Duration));
            AssertActivityToggle(app, enabled: true, PartW.HideActivityLog, selected: true);
            Assert.True(PartW.Names.All(name => app.BoundsOf(app.DashboardRowControl(name)).Bottom <= list.Bottom + 0.5), "The ten rows are not in view above the log.");

            // Step 11: old-site: the panel hidden, the toggle disabled with its State's tooltip (WD32).
            app.Click(RowPoint(app, "old-site"));
            app.Settle();
            Assert.False(app.ActivityLogView.IsEffectivelyVisible, "The activity log shows for old-site (WD32).");
            Assert.False(app.FindByAutomationId("ActivityLogSplitter").IsEffectivelyVisible, "The log's splitter shows for old-site (WD32).");
            AssertActivityToggle(app, enabled: false, PartW.DoesNotExist(copy.PathOf("old-site")));
            list = app.BoundsOf(app.Find<ListBox>("DashboardList"));
            Assert.True(Math.Abs(list.Top - 152) <= PartW.Tolerance && Math.Abs(list.Bottom - 674) <= PartW.Tolerance, $"The list is at {list}, not y 152 to 674 (WD32).");

            // Step 12: scratch: the panel again (its open state is the window's), empty.
            app.Click(RowPoint(app, "scratch"));
            app.Settle();
            log = app.ActivityLog();
            Assert.True(app.ActivityLogView.IsEffectivelyVisible, "The activity log does not show again for scratch.");
            Assert.Equal("scratch", log.RepositoryName);
            Assert.Empty(log.Rows);
            AssertActivityToggle(app, enabled: true, PartW.HideActivityLog, selected: true);
        }
        finally
        {
            hold.Dispose();
        }
    }

    /// <summary>The view model behind the active dashboard's row <paramref name="name"/> before a tab adopts it (3.3).</summary>
    private static RepositoryViewModel? RowModel(ShellDriver app, string name) =>
        app.ActiveDashboard!.Rows.Single(row => row.Name == name).Repository;

    /// <summary>
    /// The tab adopted the row's summary-mode view model (3.3): its repository is the object the row
    /// held, no longer a summary, and the registry holds no summary for its folder.
    /// </summary>
    private static void AssertAdopted(ShellDriver app, TempWorkspace copy, RepositoryViewModel? rowModel, RepositoryViewModel tab, string name)
    {
        Assert.NotNull(rowModel);
        Assert.Same(rowModel, tab);
        Assert.False(tab.IsSummary, $"{name}'s tab holds a summary-mode view model.");
        AssertNoSummaryFor(app, copy, name);
    }

    /// <summary>A graph of PartW as <see cref="CommitRow"/>s.</summary>
    private static List<CommitRow> Rows((string Message, int Lane, int Colour, string[] Labels, string Sha)[] rows) =>
        [.. rows.Select(row => new CommitRow(row.Message, row.Lane, row.Colour, row.Labels, row.Sha))];
}
