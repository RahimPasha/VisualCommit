// Part K's check K13: the keys in the conflict resolver (method 1) and on a dashboard (methods 2
// and 3, KD5) (docs/test-reports/phase-6.md, part K). The class's map is in Phase6Checks.K.cs, what
// the checks share in Phase6Checks.K.Helpers.cs.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views;
using VisualCommit.Core;
using VisualCommit.Core.Conflicts;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksK
{
    /// <summary>
    /// Check K13, method 1 (D199, D213; design 6.3, 6.5, 6.8; phase 4 design 11.10): "conflicts"
    /// stopped in a merge beside "ops". The resolver's <c>Next conflict (Alt+Down)</c> tooltip and its
    /// other buttons' texts, the disabled file buttons' kept but not shown (k13a); Ctrl+F consumed
    /// while Search is disabled; Ctrl+Tab leaving the result and keeping the tab; Ctrl+PageDown
    /// switching tabs from the result and Ctrl+PageUp back to the resolver as it was.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K13_keys_in_the_conflict_resolver()
    {
        using var conflicts = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var ops = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [conflicts.Path, ops.Path], 0);
        await app.WaitForRepositoryAsync();
        app.MoveToNeutral();

        // Step 1: GC-M with its banner; the resolver on notes.txt (FM); Search disabled; two tabs.
        AssertGraph(app, PartK.Gcm, PartK.GcmLanes, PartK.GcmWorking, showsDate: false);
        Assert.Equal(PartK.MergeBanner, ((TextBlock)app.FindByAutomationId("BannerText")).Text);
        app.Click(app.WorkingRowPoint());
        app.Click(await app.FileRowAsync("UnstagedFileList", PartK.NotesFile));
        await app.WaitForResolverAsync(PartK.NotesFile);
        var fm = app.Resolver();
        Assert.Equal(PartK.Fm, fm.Result);
        Assert.Equal(20, fm.Result!.TrimEnd('\n').Split('\n').Length);
        Assert.Equal(PartK.TwoConflictsLeft, fm.ResultCount);
        var search = ToolbarButton(app, "SearchButton");
        Assert.False(search.IsEffectivelyEnabled, "Search is enabled with the resolver open.");
        Assert.Equal(PartK.CloseResolverFirst, ToolTip.GetTip(search) as string);
        Assert.Equal([PartK.Conflicts, PartK.Ops], TabTitles(app));

        // Step 2: the pointer on Next conflict (k13a); the other four buttons read, no pointer on them.
        var next = app.FindByAutomationId("ResolverNextConflict");
        var nextText = Words(app, PartK.ResolverButtons[0].ToolTip!);
        Assert.Equal(nextText, await app.RestPointerOnAsync(next));
        var k13a = Shot(app, "k13a-next-conflict-tooltip", keepPointer: true);
        AssertToolTipLook(app, next, nextText, k13a);
        Assert.Equal(PartK.Fm, app.Resolver().Result);
        foreach (var expected in PartK.ResolverButtons)
        {
            var button = app.FindByAutomationId(expected.Id);
            AssertKeyed(app, button, expected);
            if (!expected.Enabled)
            {
                Assert.False(ToolTip.GetShowOnDisabled(button), $"The disabled {expected.Id} shows its tooltip (6.8: kept, not shown).");
                Assert.False(ToolTip.GetIsOpen(button), $"The disabled {expected.Id}'s tooltip is open.");
            }
        }

        // Step 3: Ctrl+F is consumed while Search is disabled.
        var sequence = app.CallMark();
        PressMod(app, Key.F);
        app.Settle();
        Assert.Null(app.SearchBar());
        Assert.Equal(PartK.Fm, app.Resolver().Result);
        Assert.Equal(PartK.TwoConflictsLeft, app.Resolver().ResultCount);
        Assert.NotNull(app.ShownResolver);
        AssertNoCall(app, sequence, "Ctrl+F in the resolver");

        // Step 4: Use ours on block 1 puts the focus in the result (R1); Ctrl+Tab leaves the result, the tab kept.
        app.ClickBarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs);
        await app.WaitForResultParsedAsync();
        var r1 = app.Resolver();
        Assert.Equal(PartK.R1, r1.Result);
        Assert.Equal(16, r1.Result!.TrimEnd('\n').Split('\n').Length);
        Assert.Equal(PartK.OneConflictLeft, r1.ResultCount);
        Assert.True(app.FocusIsWithin(app.ResultEditor), "Use ours did not put the keyboard focus in the result.");
        PressControl(app, Key.Tab);
        Assert.False(app.ResultEditor.IsKeyboardFocusWithin, "Ctrl+Tab left the focus in the result.");
        Assert.Equal(PartK.R1, app.Resolver().Result);
        Assert.Equal(PartK.Conflicts, ActiveTitle(app));
        Assert.Equal(0, SavedActiveTab(data));

        // Step 5: a click in the result's first line; Ctrl+PageDown, which the router takes there too, makes ops active.
        app.ClickResultAt(1, 1);
        Assert.True(app.FocusIsWithin(app.ResultEditor), "The click did not put the focus in the result.");
        PressControl(app, Key.PageDown);
        Assert.Equal(PartK.Ops, ActiveTitle(app));
        await app.WaitForRepositoryAsync();
        AssertGraph(app, PartK.Ops0, PartK.Ops0Lanes);
        AssertBranch(app, "main");
        await app.WaitForAsync(() => SavedActiveTab(data) == 1, "session.json's active tab to be 1");
        var conflictsTab = app.Session.Shell.Tabs[0].Repository!;
        Assert.True(conflictsTab.IsResolverOpen, "The conflicts tab closed its resolver.");

        // Step 6: Ctrl+PageUp: back to the resolver as it was; nothing written.
        PressControl(app, Key.PageUp);
        Assert.Equal(PartK.Conflicts, ActiveTitle(app));
        await app.WaitForResolverAsync(PartK.NotesFile);
        Assert.Equal(PartK.R1, app.Resolver().Result);
        Assert.Equal(PartK.OneConflictLeft, app.Resolver().ResultCount);
        Assert.Equal([PartK.ConflictedNotes], await GitLinesAsync(conflicts, "status", "--porcelain"));
        Assert.StartsWith(PartK.NotesBlobAtStop, (await GitLinesAsync(conflicts, "hash-object", PartK.NotesFile)).Single(), StringComparison.Ordinal);
        Assert.Equal(PartK.TheirsEditsSha, await RevParseAsync(conflicts, "MERGE_HEAD"));
    }

    /// <summary>
    /// Check K13, method 2 (D208; design 6.5, 6.9, 2.10, 3.4, 4.10): "Web" and "Tools" as two
    /// dashboards. Ctrl+Shift+F runs Web's Fetch all (K-WEBf); Ctrl+Shift+D goes to Tools, which
    /// shows the shared results (k13b, KD12), and round to Web again.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K13_fetch_all_and_next_dashboard_by_keys()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        await PrepareSameFolderAsync(copy);
        using var app = await StartWebAndToolsAsync(data, copy);
        var oldSite = copy.PathOf("old-site");

        // Step 8: Ctrl+Shift+F: the dashboard toolbar's Fetch, Fetch all (6.9).
        var mark = app.LogMark();
        var sequence = app.CallMark();
        var operations = app.Session.Operations.Snapshot().Count;
        PressMod(app, Key.F, shift: true);
        await app.WaitForBulkRunAsync(OperationChainTimeout);
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        await app.WaitForLogLineAsync(AppLogFile.Containing(PartK.FetchAllFinished), mark: mark, timeout: OperationChainTimeout);
        Assert.Single(Messages(app, mark, PartK.FetchAllStarted));
        var fetches = OperationsSince(app, operations).Where(record => record.Title == PartK.Fetch).ToList();
        Assert.Equal(PartK.Fetched.Length, fetches.Count);
        Assert.All(fetches, record => Assert.Equal(OperationOutcome.Succeeded, record.Outcome));
        var calls = app.CallsSince(sequence).Where(call => call.CommandText == PartK.FetchOrigin).ToList();
        foreach (var name in PartK.Fetched)
        {
            var fetch = Assert.Single(calls, call => call.WorkingDirectory is { } folder && SameFolder(copy, copy.PathOf(name), folder));
            Assert.True(fetch.Succeeded, $"{name}'s fetch exited {fetch.ExitCode}:\n{fetch.StandardError}");
        }

        Assert.Equal(PartK.Fetched.Length, calls.Count);
        var atOnce = calls.Max(call => calls.Count(other => other.StartedAt <= call.StartedAt && other.StartedAt + other.Duration > call.StartedAt));
        Assert.True(atOnce <= 4, $"{atOnce} fetches ran at once; Fetch all runs at most four (4.3).");
        foreach (var (name, sha) in PartK.FetchedOriginMain)
        {
            using var repo = copy.Repo(name);
            Assert.Equal(sha, await RevParseAsync(repo, "refs/remotes/origin/main"));
        }

        AssertRows(app, PartK.WebFetched(oldSite));
        AssertDashboardHeader(app, ExpectedDashboardHeader.Idle(PartK.Workspace, PartK.Count(10)) with { RunText = PartK.FetchAllResult });
        AssertStatus(app, PartK.FetchAllResult);
        AssertDashboardBranchPart(app, PartK.Workspace);

        // Step 9: Ctrl+Shift+D: the next dashboard, Tools, read, showing the shared results (k13b).
        mark = app.LogMark();
        PressMod(app, Key.D, shift: true);
        Assert.Equal(PartK.Tools, ActiveTitle(app));
        Assert.Equal([PartK.Workspace, PartK.Tools], TabTitles(app));
        await app.WaitForDashboardReadAsync(since: mark, timeout: OperationChainTimeout);
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        Assert.Contains(Messages(app, mark, "Tools: read "), line => line.StartsWith(PartK.ReadPassStart(PartK.Tools, 2), StringComparison.Ordinal));
        var k13b = Shot(app, "k13b-next-dashboard");
        Assert.NotNull(k13b);
        AssertRows(app, PartK.ToolsFetched);
        AssertDashboardHeader(app, ExpectedDashboardHeader.Idle(PartK.Tools, PartK.Count(2)) with { PullAll = ExpectedHeaderButton.Disabled(PartK.NoRepositoryOfToolsCanBePulled) });
        AssertKeyed(app, new PartK.Keyed("FetchButton", true, PartK.FetchEveryRepository(PartK.Tools), "Ctrl+Shift+F"));
        AssertKeyed(app, new PartK.Keyed("PullButton", false, PartK.NoRepositoryOfToolsCanBePulled, "Ctrl+Shift+L"));
        AssertStatus(app, PartK.Ready);
        AssertDashboardBranchPart(app, PartK.Tools);

        // Step 10: Ctrl+Shift+D again: round to Web, the focus in its list, no write since step 9.
        sequence = app.CallMark();
        operations = app.Session.Operations.Snapshot().Count;
        PressMod(app, Key.D, shift: true);
        Assert.Equal(PartK.Workspace, ActiveTitle(app));
        Assert.Equal([PartK.Workspace, PartK.Tools], TabTitles(app));
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("DashboardList")), $"The focus is on {app.FocusedAutomationId()}, not in DashboardList.");
        AssertRows(app, PartK.WebFetched(oldSite));
        AssertNoWrite(app, sequence, operations, "Going round the dashboards");
    }

    /// <summary>
    /// Check K13, method 3 (D204, D207, D208; design 2.3, 2.7, 2.9, 2.10, 3.3, 3.5, 6.9): on "Web"
    /// beside "Tools", F5 reading every row; F6, Home and Down in the list; Ctrl+B and Ctrl+Z
    /// consumed; Ctrl+J showing the selected row's activity; Alt+Down; Enter opening docs beside the
    /// dashboards; Ctrl+1; End and Delete removing old-site. No picture.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K13_rows_by_keys_on_a_dashboard()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        await PrepareSameFolderAsync(copy);
        using var app = await StartWebAndToolsAsync(data, copy);
        var oldSite = copy.PathOf("old-site");
        var web = PartK.Web(oldSite);

        // Step 12: F5 reads every row.
        var mark = app.LogMark();
        var sequence = app.CallMark();
        var operations = app.Session.Operations.Snapshot().Count;
        app.PressKey(Key.F5);
        await app.WaitForDashboardReadAsync(since: mark, timeout: OperationChainTimeout);
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        Assert.Contains(Messages(app, mark, "Web: read "), line => line.StartsWith(PartK.ReadPassStart(PartK.Workspace, 10), StringComparison.Ordinal));
        foreach (var name in PartK.WithFolder)
        {
            var reads = app.CallsSince(sequence, folder => SameFolder(copy, copy.PathOf(name), folder));
            Assert.True(
                reads.Any(call => call.CommandText.StartsWith("git for-each-ref", StringComparison.Ordinal)) && reads.Any(call => call.CommandText.StartsWith("git status", StringComparison.Ordinal)),
                $"F5 did not read {name}'s refs and status:\n" + string.Join('\n', reads.Select(call => call.CommandText)));
        }

        Assert.Empty(app.CallsSince(sequence, folder => SameFolder(copy, oldSite, folder)));
        AssertNoWrite(app, sequence, operations, "F5 on the dashboard");
        AssertRows(app, web);

        // Step 13: F6 into the list; Home; Down twice: docs.
        app.PressKey(Key.F6);
        Assert.Equal(KeyRegionRole.GraphArea, app.FocusedRegion());
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("DashboardList")), $"The focus is on {app.FocusedAutomationId()}, not in DashboardList.");
        app.PressKey(Key.Home);
        await app.WaitForAsync(() => app.DashboardSnapshot().SelectedRow == "api", "api to be selected");
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        await app.WaitForAsync(() => app.DashboardSnapshot().SelectedRow == PartK.Docs, "docs to be selected");
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("DashboardList")), "The focus left DashboardList.");
        var docsRow = app.DashboardRowControl(PartK.Docs);
        AssertColour(PaletteOf(app).Selection, app.Capture(), new Point(app.BoundsOf(docsRow).Right - 4, app.BoundsOf(docsRow).Center.Y), "docs' row's background (VcSelectionBrush)");
        AssertKeyed(app, PartK.ShowLog);

        // Step 14: Ctrl+B and Ctrl+Z consumed; Ctrl+J shows docs' activity, and hides it.
        sequence = app.CallMark();
        operations = app.Session.Operations.Snapshot().Count;
        var rows = app.DashboardSnapshot().Rows.Select(row => row.ToString()).ToList();
        PressMod(app, Key.B);
        PressMod(app, Key.Z);
        app.Settle();
        Assert.False(app.Session.Services.DialogHost.IsOpen, "A dialog opened.");
        AssertNoCall(app, sequence, "Ctrl+B and Ctrl+Z on the dashboard");
        AssertNoWrite(app, sequence, operations, "Ctrl+B and Ctrl+Z on the dashboard");
        Assert.Equal(PartK.Docs, app.DashboardSnapshot().SelectedRow);
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("DashboardList")), "The focus left DashboardList.");
        Assert.Equal(rows, app.DashboardSnapshot().Rows.Select(row => row.ToString()));
        PressMod(app, Key.J);
        await app.WaitForAsync(() => app.Session.Shell.ActivityLog.IsShown, "the activity log to show");
        var log = app.ActivityLog();
        AssertNear(PartK.LogTop, log.Bounds.Top, PartK.Tolerance, "The activity log's top");
        AssertNear(PartK.LogBottom, log.Bounds.Bottom, PartK.Tolerance, "The activity log's bottom");
        Assert.Equal(("Activity log", PartK.Docs, false), (log.Title, log.RepositoryName, log.ShowReads));
        Assert.Equal(PaletteOf(app).TextSecondary, log.RepositoryNameColor);
        Assert.DoesNotContain(log.Rows, row => row.Kind == ActivityLogRowKind.Operation);
        var list = app.BoundsOf(app.FindByAutomationId("DashboardList"));
        AssertNear(152, list.Top, PartK.Tolerance, "The dashboard's list's top");
        AssertNear(PartK.GraphContentBottom, list.Bottom, PartK.Tolerance, "The dashboard's list's bottom above the log");
        AssertKeyed(app, PartK.ShowLog with { ToolTip = PartK.HideActivityLog });
        PressMod(app, Key.J);
        await app.WaitForAsync(() => !app.Session.Shell.ActivityLog.IsShown, "the activity log to hide");
        app.Settle();
        list = app.BoundsOf(app.FindByAutomationId("DashboardList"));
        AssertNear(PartK.LogBottom, list.Bottom, PartK.Tolerance, "The dashboard's list's bottom without the log");
        AssertKeyed(app, PartK.ShowLog);
        Assert.Equal(PartK.Docs, app.DashboardSnapshot().SelectedRow);
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("DashboardList")), "The focus left DashboardList.");

        // Step 15: Alt+Down moves docs below mobile; docs keeps the selection and the focus (KD11).
        sequence = app.CallMark();
        app.PressKey(Key.Down, RawInputModifiers.Alt);
        await app.WaitForAsync(() => app.DashboardSnapshot().Rows.Select(row => row.Name).SequenceEqual(PartK.MovedOrder), "docs below mobile");
        Assert.Equal(PartK.Docs, app.DashboardSnapshot().SelectedRow);
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("DashboardList")), "The focus left DashboardList.");
        await app.WaitForAsync(() => WorkspacePathsOf(data, PartK.WorkspaceId).Count == PartK.MovedOrder.Length && WorkspacePathsOf(data, PartK.WorkspaceId).Zip(PartK.MovedOrder).All(pair => SameFolder(copy, copy.PathOf(pair.Second), pair.First)), "workspaces.json to hold Web's new order");
        AssertToolsUnchanged(data, copy);
        AssertNoCall(app, sequence, "Alt+Down");

        // Step 16: Enter opens docs in a new tab after the dashboards (2.3), adopting the row's view model.
        var row = app.ActiveDashboard!.Rows.Single(candidate => candidate.Name == PartK.Docs);
        app.PressKey(Key.Enter);
        var docs = await app.WaitForTabOpenedAsync(copy.PathOf(PartK.Docs), OperationChainTimeout);
        Assert.Equal([PartK.Workspace, PartK.Tools, PartK.Docs], TabTitles(app));
        Assert.Equal(PartK.Docs, ActiveTitle(app));
        Assert.Same(row.Repository, docs);
        AssertGraph(app, PartK.DocsGraph, 1);
        Assert.Equal(3, RefItem(app, "Local branches", "main").Behind);
        AssertBranch(app, "main");
        await app.WaitForAsync(
            () => new JsonSessionStore(new AppPaths(data.Path).SessionFile).Current is { ActiveTab: 2 } session && session.Tabs.Count == 3,
            "session.json to hold the two dashboards and docs, docs active");
        var saved = new JsonSessionStore(new AppPaths(data.Path).SessionFile).Current;
        Assert.Equal((PartK.WorkspaceId, PartK.ToolsId), (saved.Tabs[0].WorkspaceId, saved.Tabs[1].WorkspaceId));
        Assert.True(saved.Tabs[2].RepositoryPath is { } docsPath && SameFolder(copy, copy.PathOf(PartK.Docs), docsPath), $"session.json's third tab is {saved.Tabs[2]}.");

        // Step 17: Ctrl+1: the first tab, Web, the focus in its list, docs still selected.
        PressMod(app, Key.D1);
        Assert.Equal(PartK.Workspace, ActiveTitle(app));
        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        await app.WaitForAsync(() => app.FocusIsWithin(app.FindByAutomationId("DashboardList")), "the focus in DashboardList");
        Assert.Equal(PartK.Docs, app.DashboardSnapshot().SelectedRow);
        Assert.Equal(3, app.DashboardSnapshot().Rows.ToList().FindIndex(candidate => candidate.Name == PartK.Docs));

        // Step 18: End selects old-site; Delete removes it at once (K-WEBm).
        sequence = app.CallMark();
        app.PressKey(Key.End);
        await app.WaitForAsync(() => app.DashboardSnapshot().SelectedRow == "old-site", "old-site to be selected");
        app.PressKey(Key.Delete);
        await app.WaitForAsync(() => app.DashboardSnapshot().Rows.Count == PartK.RemovedOrder.Length, "old-site to be removed");
        Assert.False(app.Session.Services.DialogHost.IsOpen, "Delete asked a question.");
        var removed = PartK.RemovedOrder.Select(name => web.Single(candidate => candidate.Name == name)).ToList();
        AssertRows(app, removed);
        Assert.Equal(PartK.Count(9), app.DashboardSnapshot().Header!.Count!.Text);
        Assert.True(app.FocusIsWithin(app.FindByAutomationId("DashboardList")), $"The focus is on {app.FocusedAutomationId()}, not on DashboardList.");
        await app.WaitForAsync(() => WorkspacePathsOf(data, PartK.WorkspaceId).Count == PartK.RemovedOrder.Length, "workspaces.json to lose old-site");
        Assert.True(WorkspacePathsOf(data, PartK.WorkspaceId).Zip(PartK.RemovedOrder).All(pair => SameFolder(copy, copy.PathOf(pair.Second), pair.First)), "workspaces.json's Web is not K-WEBm:\n" + string.Join('\n', WorkspacePathsOf(data, PartK.WorkspaceId)));
        AssertToolsUnchanged(data, copy);
        Assert.False(Directory.Exists(oldSite), "old-site exists on disk.");
        Assert.All(PartK.WithFolder, name => Assert.True(Directory.Exists(copy.PathOf(name)), $"{name}'s folder is gone."));
        AssertNoCall(app, sequence, "End and Delete");
    }

    /// <summary>
    /// K13's start on "Web" and "Tools" (step 7 and step 11): both in <c>workspaces.json</c>, the
    /// session's tabs their dashboards, Web active; waits for Web's first read and its rows idle;
    /// then asserts step 7: two dashboard tabs, Web's rows as scenario facts 2.3, its pass line, the
    /// toolbar's and the header's texts, Tools not read.
    /// </summary>
    private static async Task<ShellDriver> StartWebAndToolsAsync(TempDirectory data, TempWorkspace copy)
    {
        var tools = new Workspace(PartK.ToolsId, PartK.Tools, [.. PartK.ToolsRepositories.Select(name => new WorkspaceRepository(copy.PathOf(name)))]);
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [copy.ToWorkspace(), tools] });
        var app = ShellDriver.StartWithTabs(data.Path, [new TabState(null, PartK.WorkspaceId), new TabState(null, PartK.ToolsId)], 0);
        try
        {
            await app.WaitUntilReadyAsync();
            await app.WaitForDashboardReadAsync(timeout: OperationChainTimeout);
            await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
            app.MoveToNeutral();

            var strip = app.TabStrip();
            Assert.Equal([PartK.Workspace, PartK.Tools], strip.Tabs.Select(tab => tab.Title));
            Assert.All(strip.Tabs, tab => Assert.True(tab.IsDashboard && tab.AutomationId == "DashboardTab" && tab.IconBounds is not null, $"The tab {tab.Title} is not a dashboard's tab with its icon."));
            Assert.Equal(PartK.Workspace, ActiveTitle(app));
            AssertRows(app, PartK.Web(copy.PathOf("old-site")));
            Assert.Contains(Messages(app, 0, "Web: read "), line => line.StartsWith(PartK.ReadPassStart(PartK.Workspace, 10), StringComparison.Ordinal));
            AssertKeyed(app, new PartK.Keyed("FetchButton", true, PartK.FetchEveryRepository(PartK.Workspace), "Ctrl+Shift+F"));
            AssertKeyed(app, new PartK.Keyed("PullButton", true, PartK.PullEveryRepository(PartK.Workspace), "Ctrl+Shift+L"));
            Assert.Equal(PartK.ReadAgain(PartK.Workspace), ToolTip.GetTip(app.FindByAutomationId("DashboardRefresh")) as string);
            Assert.Empty(Messages(app, 0, "Tools: read "));
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>The status bar's branch part on a dashboard tab (2.8): <c>StatusWorkspaceIcon</c> shown, then the workspace's name.</summary>
    private static void AssertDashboardBranchPart(ShellDriver app, string workspace)
    {
        var icon = (Icon)app.FindByAutomationId("StatusWorkspaceIcon");
        Assert.True(icon.IsEffectivelyVisible, "StatusWorkspaceIcon does not show on a dashboard tab.");
        Assert.True(ReferenceEquals(Application.Current!.FindResource("IconWorkspace"), icon.Data), "StatusWorkspaceIcon is not IconWorkspace.");
        AssertBranch(app, workspace);
        Assert.True(app.BoundsOf(icon).Right <= app.BoundsOf(app.FindByAutomationId("CurrentBranch")).Left, "The workspace's name is not after its icon.");
    }

    /// <summary>"Tools" in <c>workspaces.json</c> as written: infra and scratch.</summary>
    private static void AssertToolsUnchanged(TempDirectory data, TempWorkspace copy)
    {
        var tools = WorkspacePathsOf(data, PartK.ToolsId);
        Assert.True(
            tools.Count == PartK.ToolsRepositories.Length && tools.Zip(PartK.ToolsRepositories).All(pair => SameFolder(copy, copy.PathOf(pair.Second), pair.First)),
            "workspaces.json's Tools changed:\n" + string.Join('\n', tools));
    }
}
