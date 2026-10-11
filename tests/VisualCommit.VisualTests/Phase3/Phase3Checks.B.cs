// Part B of phase 3's scripted walk-through (docs/test-reports/phase-3.md, "Part B: merge, the
// conflict banner, reset, cherry-pick, revert and the detached HEAD"): checks 8, 9, 9b, 10, 11,
// 11b, 12 and 32. This class is part B's alone; its expected texts and graphs (G0 to G12, E0, E1)
// go into Phase3Expectations.B.cs (Phase3Expectations.PartB), and further files of its own may
// hold more of the class (Phase3Checks.B.<topic>.cs). Everything the checks share is in
// Phase3Checks.Support.cs; read its header for the conventions (one test method per app start,
// pictures through Shot, no fixed waits). While a banner is shown, rows are found from the graph
// control's bounds (ShellDriver.GraphRowPoint, WorkingRowPoint, GraphLabelBounds), never at a
// fixed y (D122).
using Avalonia.Headless.XUnit;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>The checks of part B: merge, the conflict banner, reset, cherry-pick, revert and the detached HEAD.</summary>
public sealed partial class Phase3ChecksB : Phase3Checks
{
    /// <summary>
    /// Check 8, step 1 (the skeleton's example; part B's writer goes on from here): the ops
    /// scenario at 1400×900 shows G0 with all six columns, nothing selected, status "Ready".
    /// </summary>
    [AvaloniaFact]
    public async Task Check_08_merges_fast_forward()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);

        Shot(app, "08a-before");

        var graph = Graph(app);
        Assert.Equal(PartB.G0Subjects, Enumerable.Range(0, graph.Data!.Count).Select(row => graph.Data.CommitAt(row).Subject));
        Assert.True(graph.Columns.Author.IsVisible && graph.Columns.Date.IsVisible, "At 1400×900 the graph shows all six columns.");
        Assert.Equal(WindowLayout.GraphColumnWidth(3), graph.Columns.Graph.Width);
        Assert.Equal(-1, graph.SelectedIndex);
        AssertStatus(app, "Ready");
        AssertBranch(app, "main");
        AssertNoClippedText(app);
    }
}
