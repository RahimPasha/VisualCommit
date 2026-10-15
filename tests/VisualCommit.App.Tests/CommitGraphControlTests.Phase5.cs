using System.Globalization;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Phase 5's drawing of the graph (package WP2b): a stash's line dashed down to its base
/// (docs/phase-5-design.md section 10, D161). The expected values are the phase 5 report's (part
/// F's scenarios and decision FD15), given there in the window of the app at 1100×700, where the
/// graph control's top-left corner is at (260, 116): the tests take those positions less that
/// corner, as the control here fills the window from its top-left.
/// </summary>
public partial class CommitGraphControlTests
{
    /// <summary>The x of the graph control's left edge in the report's 1100×700 window.</summary>
    private const int ReportLeft = 260;

    /// <summary>The y of the graph control's top edge in the report's 1100×700 window, with no banner and no search bar.</summary>
    private const int ReportTop = 116;

    private static readonly Lazy<Task<CommitGraphData>> FiltersScenarioData = new(() => LoadScenarioAsync(Scenarios.FiltersAsync));

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task The_graph_scenarios_stash_line_is_dashed_down_to_its_base_and_the_other_lines_stay_solid(string themeName)
    {
        var theme = Expected.For(themeName);
        var data = await GraphScenarioAsync();
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, theme.Variant);
        var shot = host.Capture();
        Assert.Equal(default, host.ToWindow(control, default));
        Assert.Equal(406 - ReportLeft, control.LaneCenterX(0));

        // GS0 (report part F, F15): lane 0 from 2 below the ring's bottom (134) to 1 above the
        // base's node (176), across the row boundaries at 142 and 168, at least 5 dashes.
        AssertDashedInReport(shot, 406, 136, 175, theme.Lanes[0], theme.Background, minimumDashes: 5);

        // The pattern is the working-changes row's (D60): a stash in the first row starts with a
        // whole dash at its ring's bottom, then a gap of 3.
        AssertColor(theme.Lanes[0], shot.PixelAt(146, 18), "the first dash under the ring");
        AssertColor(theme.Lanes[0], shot.PixelAt(146, 20), "the end of the first dash");
        AssertColor(theme.Background, shot.PixelAt(146, 21), "the first gap");
        AssertColor(theme.Background, shot.PixelAt(146, 23), "the end of the first gap");

        // From the base on, lane 0 is main's own; lane 1 is feature/search's.
        AssertSolidInReport(shot, 406, 187, 219, theme.Lanes[0]);
        AssertSolidInReport(shot, 422, 161, 219, theme.Lanes[1]);
    }

    [AvaloniaFact]
    public async Task The_filters_scenarios_stash_lines_are_dashed_and_one_keeps_its_pattern_across_ten_rows()
    {
        var theme = Expected.Dark;
        var data = await FiltersScenarioData.Value;
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, theme.Variant);
        var shot = host.Capture();

        // FL0 (report part F): 15 rows in 5 lanes; the alpha stash's lane 0 dashed from its ring
        // into Alpha one's node (rows 0 to 11), the beta stash's lane 1 into Beta six's (rows 1, 2).
        Assert.Equal(15, data.Count);
        Assert.Equal(5, data.MaxLaneCount);
        var dashed = Enumerable.Range(0, data.Count)
            .SelectMany(row => data.RowAt(row).Lines.Where(line => line.IsDashed).Select(line => $"{row} {line.Kind} {line.FromLane}>{line.ToLane}"))
            .ToList();
        Assert.Equal(
            [
                "0 OutOfNode 0>0",
                "1 PassThrough 0>0", "1 OutOfNode 1>1",
                "2 PassThrough 0>0", "2 IntoNode 1>1",
                .. Enumerable.Range(3, 8).Select(row => $"{row} PassThrough 0>0"),
                "11 IntoNode 0>0",
            ],
            dashed);

        AssertColor(theme.Lanes[0], shot.PixelAt(146, 13 - 4), "the alpha stash's ring");
        AssertColor(theme.Background, shot.PixelAt(146, 13), "inside the alpha stash's ring");
        AssertColor(theme.Lanes[1], shot.PixelAt(162, 39 - 4), "the beta stash's ring");

        // The line drawn row by row is one pattern from 136 to 404, which ends 2 below row 11's top
        // because lane 2's curve comes into Alpha one there (FD15).
        AssertDashedInReport(shot, 406, 136, 404, theme.Lanes[0], theme.Background, minimumDashes: 40);
        AssertDashedInReport(shot, 422, 162, 175, theme.Lanes[1], theme.Background, minimumDashes: 2);
        AssertSolidInReport(shot, 422, 187, 201, theme.Lanes[1]);
        AssertSolidInReport(shot, 406, 421, 452, theme.Lanes[0]);
    }

    [AvaloniaFact]
    public void A_stashs_dashes_keep_their_places_wherever_the_view_cuts_the_line()
    {
        // A stash made on B, 21 rows down: its lane passes the 20 commits of another branch.
        var theme = Expected.Dark;
        var data = StashGraph(
            [
                ("s", ["b"], true),
                .. Enumerable.Range(1, 20).Select(n => ($"x{n:D2}", new[] { n < 20 ? $"x{n + 1:D2}" : "b" }, false)),
                ("b", ["a"], false),
                ("a", [], false),
            ]);
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, 200, theme.Variant);
        var x = (int)control.LaneCenterX(0);

        // Scrolled by nothing, by less than a row, by a few rows, and with the stash's row out of
        // view: the dashes stay at the same places of the whole line, counted from the ring's
        // bottom in the first row (18), as one line drawn in one piece would have them.
        foreach (var offset in new[] { 0, 4, 13, 100, 301, 398 })
        {
            control.ScrollOffset = offset;
            var shot = host.Capture();
            var wrong = new List<string>();
            for (var y = 0; y < 200; y++)
            {
                var along = y + offset;
                if (along < 19 || along > 545)
                {
                    // The ring above, and the base's row, where the other lane's curve comes in.
                    continue;
                }

                var isDash = ((along - 18) % 6) < 3;
                var pixel = shot.PixelAt(x, y);
                if ((Distance(pixel, theme.Lanes[0]) < Distance(pixel, theme.Background)) != isDash)
                {
                    wrong.Add(string.Create(CultureInfo.InvariantCulture, $"{along} ({pixel})"));
                }
            }

            Assert.True(wrong.Count == 0, $"Scrolled by {offset}, these y of the line are not {(wrong.Count > 0 ? "as the pattern says" : string.Empty)}: {string.Join(", ", wrong)}.");
        }
    }

    [AvaloniaFact]
    public async Task A_stash_on_heads_commit_and_the_working_changes_rows_line_dash_as_one()
    {
        // Phase 1's graph scenario with working changes (phase 2's check 23): both lines run in
        // lane 0 from the stash's ring (display row 1) down to main's node (display row 3).
        var theme = Expected.Dark;
        var data = await GraphScenarioAsync();
        var control = new CommitGraphControl { Data = data, Dates = Utc, ShowsWorkingRow = true };
        using var host = Host.Show(control, AreaWidth, RowsHeight, theme.Variant);
        var shot = host.Capture();
        var x = (int)control.LaneCenterX(0);
        var stash = control.RowBounds(0).Center.Y;
        var main = control.RowBounds(2).Center.Y;

        Assert.Empty(ShellDriver.DashedPixelProblems(shot, x, 20, (int)stash - 7, theme.Lanes[0], theme.Background, minimumDashes: 2));
        Assert.Empty(ShellDriver.DashedPixelProblems(shot, x, (int)stash + 7, (int)main - 6, theme.Lanes[0], theme.Background, minimumDashes: 6));

        // One pattern from the working-changes row's ring to main's node, through the stash's ring.
        for (var y = 19; y < main - 5; y++)
        {
            if (Math.Abs(y - stash) <= 5.5)
            {
                continue;
            }

            var pixel = shot.PixelAt(x, y);
            var isDash = ((y - 18) % 6) < 3;
            Assert.True((Distance(pixel, theme.Lanes[0]) < Distance(pixel, theme.Background)) == isDash, $"y {y} is {pixel}, expected {(isDash ? "a dash" : "a gap")}.");
        }
    }

    [AvaloniaFact]
    public void A_stashs_dashed_curve_into_its_base_has_gaps_where_a_solid_curve_has_none()
    {
        // The stash's lane waits beside C's for B, so its line curves into B's node (lane 1 to 0);
        // in the second graph a commit takes the stash's place and its curve is solid.
        var theme = Expected.Dark;
        var dashed = StashGraph([("c", ["b"], false), ("s", ["b"], true), ("b", ["a"], false), ("a", [], false)]);
        var solid = StashGraph([("c", ["b"], false), ("d", ["b"], false), ("b", ["a"], false), ("a", [], false)]);
        Assert.Contains(dashed.RowAt(2).Lines, line => line is { Kind: GraphLineKind.IntoNode, FromLane: 1, ToLane: 0, IsDashed: true });
        Assert.Contains(solid.RowAt(2).Lines, line => line is { Kind: GraphLineKind.IntoNode, FromLane: 1, ToLane: 0, IsDashed: false });

        var control = new CommitGraphControl { Data = dashed, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, theme.Variant);
        var (dashedGaps, dashedDashes) = CurvePixels(host.Capture(), control, theme);
        control.Data = solid;
        var (solidGaps, solidDashes) = CurvePixels(host.Capture(), control, theme);

        Assert.True(dashedGaps >= 3 && dashedDashes >= 3, $"The dashed curve has {dashedGaps} gap and {dashedDashes} dash samples.");
        Assert.Equal(0, solidGaps);
        Assert.True(solidDashes > 10, $"The solid curve has {solidDashes} samples in its colour.");
    }

    /// <summary>Asserts FD15's dash rule (<see cref="ShellDriver.DashedPixelProblems"/>) at the report's window positions.</summary>
    private static void AssertDashedInReport(Screenshot shot, int x, int top, int bottom, Color lane, Color background, int minimumDashes) =>
        Assert.Empty(ShellDriver.DashedPixelProblems(shot, x - ReportLeft, top - ReportTop, bottom - ReportTop, lane, background, minimumDashes));

    /// <summary>Asserts a solid line (<see cref="ShellDriver.SolidPixelProblems"/>) at the report's window positions.</summary>
    private static void AssertSolidInReport(Screenshot shot, int x, int top, int bottom, Color lane) =>
        Assert.Empty(ShellDriver.SolidPixelProblems(shot, x - ReportLeft, top - ReportTop, bottom - ReportTop, lane));

    private static int Distance(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    /// <summary>
    /// Samples the curve from lane 1 at row 2's top into lane 0's node at its middle (the shape
    /// <c>DrawConnection</c> draws) outside the node, and counts the samples nearer to lane 1's
    /// colour (dashes) and nearer to the background (gaps).
    /// </summary>
    private static (int Gaps, int Dashes) CurvePixels(Screenshot shot, CommitGraphControl control, Expected theme)
    {
        var top = control.RowBounds(2).Y;
        var from = new Point(control.LaneCenterX(1), top);
        var to = new Point(control.LaneCenterX(0), top + (CommitGraphControl.RowHeight / 2));
        var middleY = (from.Y + to.Y) / 2;
        var (gaps, dashes) = (0, 0);
        for (var step = 0; step <= 60; step++)
        {
            var t = step / 60.0;
            var u = 1 - t;
            var x = (u * u * u * from.X) + (3 * u * u * t * from.X) + (3 * u * t * t * to.X) + (t * t * t * to.X);
            var y = (u * u * u * from.Y) + (3 * u * u * t * middleY) + (3 * u * t * t * middleY) + (t * t * t * to.Y);
            if (Math.Sqrt(((x - to.X) * (x - to.X)) + ((y - to.Y) * (y - to.Y))) < CommitGraphControl.NodeDiameter / 2 + 1)
            {
                break;
            }

            var pixel = shot.PixelAt((int)Math.Floor(x), (int)Math.Floor(y));
            if (Distance(pixel, theme.Lanes[1]) < Distance(pixel, theme.Background))
            {
                dashes++;
            }
            else
            {
                gaps++;
            }
        }

        return (gaps, dashes);
    }

    /// <summary>
    /// A complete graph of the commits given newest first, each as its id, its parents' ids and
    /// whether it is a stash, laid out as the app lays them out, with HEAD on <c>main</c> at the
    /// first commit that is not a stash.
    /// </summary>
    private static CommitGraphData StashGraph(IReadOnlyList<(string Name, string[] Parents, bool IsStash)> commits)
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var infos = commits
            .Select((commit, row) => new CommitInfo(
                commit.Name,
                commit.Parents,
                "Test Author",
                "author@example.com",
                start.AddMinutes(commits.Count - row),
                start.AddMinutes(commits.Count - row),
                (commit.IsStash ? "On main: " : string.Empty) + commit.Name,
                commit.IsStash ? CommitKind.Stash : CommitKind.Commit))
            .ToList();
        var head = infos.First(info => info.Kind == CommitKind.Commit).Sha;
        var data = new CommitGraphData(new RepoRefs(
            new HeadState("main", head),
            [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, head, IsHead: true)],
            [],
            []));
        var layout = new GraphLayout();
        data.Append(infos, infos.Select(layout.Add).ToList());
        data.Complete();
        return data;
    }
}
