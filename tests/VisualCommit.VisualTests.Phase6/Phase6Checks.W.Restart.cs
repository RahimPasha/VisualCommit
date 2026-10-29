// Check W9 of part W (docs/test-reports/phase-6.md): a restart keeps the workspace and the open
// dashboard. A plain [Fact] with two app starts on one data folder (D32, phase 5's A3 pattern).
using Avalonia.Controls;
using VisualCommit.Core;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksW
{
    /// <summary>
    /// Check W9 (design 1.3, 1.11, 2.1, 2.10; D189, D205, D207): <c>workspaces.json</c> and
    /// <c>session.json</c> after the first start; the second start restores the dashboard first and
    /// api's tab after it, api active; the dashboard in the background calls no git until it is
    /// activated, then reads its rows.
    /// </summary>
    [Fact]
    public async Task Check_W9_restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assembly = typeof(Phase6ChecksW).Assembly;
        using var copy = await Scenarios.WorkspaceAsync();
        await PrepareSameFolderAsync(copy);
        using var data = new TempDirectory("data");

        // First start: steps 1 to 3.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                // Step 1.
                using var app = await OpenWorkspaceAsync(data, copy);

                // Step 2: api opened by a double-click, adopting its row's view model.
                var model = RowModel(app, "api");
                app.DoubleClick(RowPoint(app, "api"));
                var api = await app.WaitForRepositoryAsync(OperationChainTimeout);
                var picture = Shot(app, PartW.W9aApiOpened);
                Assert.Equal([PartW.Workspace, "api"], TabTitles(app));
                Assert.Same(app.Session.Shell.Tabs[1], app.Session.Shell.ActiveTab);
                AssertAdopted(app, copy, model, api, "api");
                AssertGraph(app, Rows(PartW.A0), lanes: 1, picture: picture);

                // Step 3: the app closes (the driver's Dispose).
                return true;
            },
            cancellationToken);

        var session = ReadSession(data);
        Assert.Equal(2, session.Tabs.Count);
        Assert.Equal(new TabState(null, PartW.WorkspaceId), session.Tabs[0]);
        Assert.True(session.Tabs[1].WorkspaceId is null && session.Tabs[1].RepositoryPath is { } saved && SameFolder(copy, copy.PathOf("api"), saved), $"session.json's second tab is {session.Tabs[1]}, not api's.");
        Assert.Equal(1, session.ActiveTab);
        var (version, workspaces) = ReadWorkspacesFile(data);
        Assert.Equal(1, version);
        var web = Assert.Single(workspaces);
        Assert.Equal((PartW.WorkspaceId, PartW.Workspace), (web.Id, web.Name));
        Assert.Equal(PartW.Names.Select(copy.PathOf), web.Paths);

        // Second start, on the same data folder, writing nothing: steps 4 and 5.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                // Step 4: the dashboard restored first, in the background; api active; nothing read by the dashboard.
                // The app log of the data folder holds the first start's lines: the second start's are those after them.
                var startMark = AppLogFile.ReadLines(data.Path).Count;
                using var app = ShellDriver.Start(data.Path, 1100, 700, readClock: new ManualTimeProvider());
                var api = await app.WaitForRepositoryAsync(OperationChainTimeout);
                var picture = Shot(app, PartW.W9bRestarted);
                Assert.True(app.Session.Initialization.IsCompletedSuccessfully, "The app's start-up has not completed.");
                var strip = app.TabStrip();
                Assert.Equal([PartW.Workspace, "api"], strip.Tabs.Select(tab => tab.Title));
                AssertDashboardTab(app, strip, 0, PartW.Workspace, active: false);
                Assert.Same(app.Session.Shell.Tabs[1], app.Session.Shell.ActiveTab);
                AssertGraph(app, Rows(PartW.A0), lanes: 1, picture: picture);
                AssertNothingReads(app, PartW.WorkspaceId);
                Assert.Empty(PassLines(app, startMark));
                foreach (var name in PartW.WithFolder.Where(name => name != "api"))
                {
                    Assert.Empty(CallsIn(app, copy, 0, name));
                }

                // Step 5: the Web tab: one pass of the nine rows without a tab (old-site's folder check among them).
                var mark = app.CallMark();
                var logMark = app.LogMark();
                app.Click(strip.Tabs[0].Bounds.Center);
                await app.WaitForDashboardReadAsync(since: logMark, timeout: OperationChainTimeout);
                await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
                app.MoveToNeutral();
                Shot(app, PartW.W9cDashboardRead);
                Assert.NotNull(app.ActiveDashboard);
                AssertOnePass(app, logMark, 9);
                AssertRows(app, copy, PartW.W0(copy.PathOf("old-site")));
                Assert.Same(api, RowModel(app, "api"));
                await app.WaitForAsync(() => app.FocusIsWithin(app.Find<ListBox>("DashboardList")), "the dashboard's list to have the keyboard focus");
                foreach (var name in PartW.WithFolder.Where(name => name != "api"))
                {
                    Assert.True(CallsIn(app, copy, mark, name).Count > 0, $"The dashboard read nothing in {name}.");
                }

                Assert.Empty(CallsIn(app, copy, mark, "old-site"));
                AssertTd(app, PartW.Workspace, PartW.Count(10));
                return true;
            },
            cancellationToken);
    }
}
