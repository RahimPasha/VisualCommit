// Check F15 of part F (docs/test-reports/phase-5.md): every stash's line drawn dashed down to the
// commit it was made on (design 10, D161), on phase 1's graph scenario and on the filters
// scenario, whose alpha stash's line crosses ten rows in one unbroken pattern; every other line
// solid. The layout's IsDashed is read from the graph's data, the pattern sampled from the
// pictures by FD15's rule (ShellDriver.AssertDashedPixels).
using Avalonia.Headless.XUnit;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksF
{
    /// <summary>Check F15, step 1: phase 1's "graph" scenario, its stash's line dashed from the ring into Add settings page.</summary>
    [AvaloniaFact]
    public async Task Check_F15_graph_scenario_stash_dashed()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: GS0, phase 1's rows, lanes and colours unchanged; the stash a ring in lane 0.
        var shot = Shot(app, PartF.F15GraphStash);
        AssertGraph(app, PartF.GS0, PartF.GraphLanes, working: null, showsDate: true, picture: shot);
        AssertNodeAt(app, 0, 0, PartF.GS0RingAt);
        AssertDashedLines(app, PartF.GS0Dashed);
        AssertStretches(app, shot, PartF.GS0Stretches);
        AssertNoClippedText(app);
    }

    /// <summary>Check F15, step 2: the filters scenario's two stashes, one of whose lines crosses ten rows.</summary>
    [AvaloniaFact]
    public async Task Check_F15_filters_stashes_dashed()
    {
        using var repo = await Scenarios.FiltersAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 2: FL0, rings in colours 0 and 1; lane 0 dashed from row 0 into Alpha one, lane 1
        // from row 1 into Beta six; the line drawn row by row keeps its phase.
        var shot = Shot(app, PartF.F15FiltersStashes);
        AssertGraph(app, PartF.FL0, PartF.FiltersLanes, working: null, showsDate: false, picture: shot);
        foreach (var (ring, row) in PartF.FL0Rings.Select((ring, row) => (ring, row)))
        {
            Assert.Equal(ring.Colour, PartF.FL0[row].Colour);
            AssertNodeAt(app, row, PartF.FL0[row].Lane, ring.At);
        }

        AssertDashedLines(app, PartF.FL0Dashed);
        AssertStretches(app, shot, PartF.FL0Stretches);
        AssertNoClippedText(app);
    }
}
