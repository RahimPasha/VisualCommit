// Check A3 of part A (docs/test-reports/phase-5.md): the activity log's height, its default, a drag
// of its splitter, the height kept across a restart of the app, and the splitter's limits. A plain
// [Fact] with two app starts on one data folder (AD13), as phase 0's check 6 restarts the app.
using Avalonia;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksA
{
    /// <summary>
    /// Check A3: the panel opens at its default height, a drag of the splitter sets it and saves it
    /// on release, a restart reads it from the session (the panel itself closed at start), and the
    /// splitter stops at the maximum (the graph area's height less 165, AD1) and the minimum (120).
    /// </summary>
    [Fact]
    public async Task Check_A3_panel_height()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assembly = typeof(Phase5ChecksA).Assembly;
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        var status = await GitLinesAsync(repo, "status", "--porcelain");

        // First start: the default height, then the splitter 100 up.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = await OpenAsync(data, repo);
                var rows = (Working: app.WorkingRowPoint().Y, First: app.GraphRowPoint(0).Y);

                // Step 1: the panel at its default height, its list empty and without text (AD2).
                app.ClickActivityLogToggle();
                app.Settle();
                var shot = Shot(app, PartA.A3DefaultHeight);
                var log = AssertPanelAt(app, PartA.DefaultHeight, shot);
                AssertHeader(app, log, PartA.HistoryRepo, showReads: false);
                AssertEmptyList(app, log);
                AssertToggle(app, enabled: true, selected: true, shot);
                Assert.Null(SavedHeight(data));
                Assert.Contains("\"activityLogHeight\": null", File.ReadAllText(new AppPaths(data.Path).SessionFile), StringComparison.Ordinal);
                Assert.Equal(rows, (app.WorkingRowPoint().Y, app.GraphRowPoint(0).Y));

                // Step 2: the splitter dragged from its centre 100 up, in 5 steps; letting go saved it.
                Assert.Equal(new Point(480, 471.5), app.BoundsOf(app.FindByAutomationId("ActivityLogSplitter")).Center);
                app.DragActivityLogSplitter(PartA.FirstDrag, PartA.DragSteps);
                shot = Shot(app, PartA.A3DraggedTo300);
                AssertPanelAt(app, PartA.DraggedHeight, shot);
                Assert.Equal(PartA.DraggedHeight, SavedHeight(data)!.Value, PartA.Tolerance);
                Assert.Equal(rows, (app.WorkingRowPoint().Y, app.GraphRowPoint(0).Y));

                // Step 3: the app closes (the driver's Dispose).
                return true;
            },
            cancellationToken);

        Assert.Equal(PartA.DraggedHeight, SavedHeight(data)!.Value, PartA.Tolerance);

        // Second start, on the same data folder, writing no session: the tab and the height come from it.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                // Step 4: the panel closed at start; opened, it is 300 high and empty (D173).
                using var app = ShellDriver.Start(data.Path);
                await app.WaitForRepositoryAsync();
                app.MoveToNeutral();
                AssertTabs(app, PartA.HistoryRepo, PartA.HistoryRepo);
                AssertNoPanel(app);
                AssertToggle(app, enabled: true, selected: false);
                app.ClickActivityLogToggle();
                app.Settle();
                var shot = Shot(app, PartA.A3Restarted);
                var log = AssertPanelAt(app, PartA.DraggedHeight, shot);
                AssertHeader(app, log, PartA.HistoryRepo, showReads: false);
                AssertEmptyList(app, log);

                // Step 5: dragged 300 up, it stops at its maximum, 421: the content above keeps 160.
                app.DragActivityLogSplitter(PartA.DragToMaximum, PartA.DragSteps);
                shot = Shot(app, PartA.A3AtTheMaximum);
                AssertPanelAt(app, PartA.MaximumHeight, shot);
                Assert.Equal(PartA.MaximumHeight, SavedHeight(data)!.Value, PartA.Tolerance);

                // Step 6: dragged 400 down, it stops at its minimum, 120: the header and 90 px of list.
                Assert.Equal(new Point(480, 250.5), app.BoundsOf(app.FindByAutomationId("ActivityLogSplitter")).Center);
                app.DragActivityLogSplitter(PartA.DragToMinimum, PartA.DragSteps);
                shot = Shot(app, PartA.A3AtTheMinimum);
                log = AssertPanelAt(app, PartA.MinimumHeight, shot);
                Assert.Equal(PartA.MinimumHeight - PartA.HeaderHeight, log.ListBounds.Height, PartA.Tolerance);
                Assert.Equal(PartA.MinimumHeight, SavedHeight(data)!.Value, PartA.Tolerance);
                await AssertNoDialogAsync(app);
                return true;
            },
            cancellationToken);

        // Nothing ran in git.
        Assert.Equal(status, await GitLinesAsync(repo, "status", "--porcelain"));
    }

    /// <summary>The panel's height in the session file, as <c>JsonSessionStore</c> reads it (null for the default).</summary>
    private static double? SavedHeight(TempDirectory data) => new JsonSessionStore(new AppPaths(data.Path).SessionFile).Current.ActivityLogHeight;

    /// <summary>An empty log (AD2): no row, and no text in the list.</summary>
    private static void AssertEmptyList(ShellDriver app, VisualCommit.App.Views.Shell.ActivityLogSnapshot log)
    {
        Assert.Empty(log.Rows);
        Assert.Empty(TextsIn(app.FindByAutomationId("ActivityLogList")));
    }
}
