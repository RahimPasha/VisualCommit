using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.Views.Graph;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Phase 5's drawing of the graph (package WP2b): a stash's line dashed down to its base
/// (docs/phase-5-design.md section 10, D161), a search's marks and dimmed rows (3.3, D157), and
/// the bar that runs while the graph reloads for a filter (8.2). The expected values are the phase
/// 5 report's (part F's scenarios and decisions FD2, FD6 and FD15), given there in the window of
/// the app at 1100×700, where the graph control's top-left corner is at (260, 116): the tests take
/// those positions less that corner, as the control here fills the window from its top-left.
/// </summary>
public partial class CommitGraphControlTests
{
    /// <summary>The x of the graph control's left edge in the report's 1100×700 window.</summary>
    private const int ReportLeft = 260;

    /// <summary>The y of the graph control's top edge in the report's 1100×700 window, with no banner and no search bar.</summary>
    private const int ReportTop = 116;

    /// <summary>The colours of a search: the match's mark (<c>VcSearchMatchBrush</c>), per theme.</summary>
    private static readonly Color DarkSearchMatch = Color.Parse("#F2C94C");

    private static readonly Color LightSearchMatch = Color.Parse("#B98900");

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

    [AvaloniaFact]
    public async Task Without_an_active_search_the_graph_draws_as_without_one()
    {
        var data = await GraphScenarioAsync();
        var control = new CommitGraphControl { Data = data, Dates = Utc, SelectedIndex = 4 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        var plain = PixelsOf(host.Capture());
        Assert.Same(GraphSearchMarks.None, control.Search);

        // Matches of a search that is not active (an empty box) mark nothing.
        control.Search = new GraphSearchMarks([data.CommitAt(2).Sha, data.CommitAt(6).Sha], isActive: false);
        Assert.Equal(plain, PixelsOf(host.Capture()));
        Assert.All(Enumerable.Range(0, data.Count), row => Assert.Equal(GraphSearchRowState.None, control.SearchStateOf(row)));

        control.Search = new GraphSearchMarks([data.CommitAt(2).Sha], isActive: true);
        Assert.NotEqual(plain, PixelsOf(host.Capture()));

        control.Search = GraphSearchMarks.None;
        Assert.Equal(plain, PixelsOf(host.Capture()));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task A_search_marks_its_matches_and_dims_the_nodes_labels_and_texts_of_every_other_commit_row(string themeName)
    {
        var theme = Expected.For(themeName);
        var data = await GraphScenarioAsync();
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, theme.Variant);
        var plain = host.Capture();
        var plainInk = InkOf(plain, MessageCell(control, 2), theme.Background);

        // Add settings page (main) and Add search box (origin/feature/search) match.
        control.Search = new GraphSearchMarks([data.CommitAt(2).Sha, data.CommitAt(6).Sha], isActive: true);
        var shot = host.Capture();

        Assert.Equal(
            Enumerable.Range(0, data.Count).Select(row => row is 2 or 6 ? GraphSearchRowState.Marked : GraphSearchRowState.Dimmed),
            Enumerable.Range(0, data.Count).Select(control.SearchStateOf));
        Assert.Equal(GraphSearchRowState.None, control.SearchStateOf(-1));
        Assert.Equal(GraphSearchRowState.None, control.SearchStateOf(data.Count));

        // The mark: 3 px of VcSearchMatchBrush at the left edge of a match's row, over its full
        // height; nothing on the other rows (the report samples x 261, here 1).
        var match = themeName == "Light" ? LightSearchMatch : DarkSearchMatch;
        foreach (var row in new[] { 2, 6 })
        {
            var bounds = control.RowBounds(row);
            AssertColor(match, shot.PixelAt(1, (int)bounds.Center.Y), $"row {row}'s mark");
            AssertColor(match, shot.PixelAt(0, (int)bounds.Y), $"row {row}'s mark at its top");
            AssertColor(match, shot.PixelAt(2, (int)bounds.Bottom - 1), $"row {row}'s mark at its bottom");
            AssertColor(theme.Background, shot.PixelAt(3, (int)bounds.Center.Y), $"right of row {row}'s mark");
        }

        AssertColor(theme.Background, shot.PixelAt(1, (int)control.RowBounds(3).Center.Y), "row 3, no match, unmarked");

        // A match's node at full strength; another row's at 35 % over the background (FD6), also
        // at its centre, where the lines that meet in it are covered as by a full node.
        AssertColor(theme.Lanes[0], NodePixel(shot, control, 2, 3), "row 2's node");
        AssertColor(theme.Lanes[1], NodePixel(shot, control, 6, 3), "row 6's node");
        AssertColor(Faded(theme.Lanes[1], theme.Background), NodePixel(shot, control, 1, 3), "row 1's dimmed node");
        AssertColor(Faded(theme.Lanes[2], theme.Background), NodePixel(shot, control, 3, 3), "row 3's dimmed node");
        AssertColor(Faded(theme.Lanes[0], theme.Background), NodePixel(shot, control, 4, 3), "row 4's dimmed node");
        AssertColor(Faded(theme.Lanes[0], theme.Background), NodePixel(shot, control, 4, 0), "row 4's dimmed node at its centre");
        AssertColor(Faded(theme.Lanes[0], theme.Background), shot.PixelAt(146, (int)control.RowBounds(0).Center.Y - 4), "row 0's dimmed ring");
        AssertColor(theme.Background, shot.PixelAt(146, (int)control.RowBounds(0).Center.Y), "inside row 0's dimmed ring");
        if (themeName == "Light")
        {
            AssertColor(Color.Parse("#BDC3F1"), NodePixel(shot, control, 4, 3), "the report's light colour 0, dimmed");
        }
        else
        {
            AssertColor(Color.Parse("#383F6B"), NodePixel(shot, control, 4, 3), "the report's dark colour 0, dimmed");
            AssertColor(Color.Parse("#234F3E"), NodePixel(shot, control, 1, 3), "the report's dark colour 1, dimmed");
            AssertColor(Color.Parse("#5B4726"), NodePixel(shot, control, 3, 3), "the report's dark colour 2, dimmed");
        }

        // The lanes passing through a dimmed row stay at full strength.
        AssertColor(theme.Lanes[0], shot.PixelAt(146, (int)control.RowBounds(3).Center.Y), "lane 0 through row 3");
        AssertColor(theme.Lanes[1], shot.PixelAt(162, (int)control.RowBounds(3).Center.Y), "lane 1 through row 3");

        // Labels: a match's at full strength, another row's faded.
        var main = control.LabelsInRow(2)[0];
        AssertColor(theme.Lanes[0], shot.PixelAt((int)main.Bounds.Right - 2, (int)main.Bounds.Center.Y), "inside main's label");
        var tag = control.LabelsInRow(4)[0];
        AssertColor(Faded(theme.ControlBackground, theme.Background), shot.PixelAt((int)tag.Bounds.Right - 3, (int)tag.Bounds.Center.Y), "inside v0.2's dimmed label");
        var remote = control.LabelsInRow(3)[0];
        AssertColor(Faded(theme.Lanes[2], theme.Background), shot.PixelAt((int)remote.Bounds.X, (int)remote.Bounds.Center.Y), "origin/main's dimmed border");
        var login = control.LabelsInRow(9)[0];
        AssertColor(Faded(theme.Lanes[4], theme.Background), shot.PixelAt((int)login.Bounds.Right - 2, (int)login.Bounds.Center.Y), "inside feature/login's dimmed label");

        // Texts: a dimmed row's ink is about 35 % of a full row's; a match's message is semibold,
        // so it reaches further right than the same message in the regular weight.
        var fullInk = InkOf(shot, MessageCell(control, 2), theme.Background);
        var dimmedInk = InkOf(shot, MessageCell(control, 3), theme.Background);
        Assert.InRange(dimmedInk.Strength, 0.25 * fullInk.Strength, 0.45 * fullInk.Strength);
        var fullDate = InkOf(shot, DateCell(control, 2), theme.Background);
        var dimmedDate = InkOf(shot, DateCell(control, 3), theme.Background);
        Assert.InRange(dimmedDate.Strength, 0.25 * fullDate.Strength, 0.45 * fullDate.Strength);
        Assert.InRange(fullInk.Strength, 0.9 * plainInk.Strength, 1.1 * plainInk.Strength);
        AssertBolder(fullInk, plainInk, "The matched message");
    }

    [AvaloniaFact]
    public async Task The_working_changes_row_is_neither_marked_nor_dimmed_and_selection_and_hover_keep_their_backgrounds()
    {
        var theme = Expected.Dark;
        var data = await GraphScenarioAsync();
        var control = new CommitGraphControl { Data = data, Dates = Utc, ShowsWorkingRow = true, SelectedIndex = 3 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, theme.Variant);
        var working = control.WorkingRowBounds;
        var plainInk = InkOf(host.Capture(), new Rect(control.Columns.Message.X, working.Y, control.Columns.Message.Width, working.Height), theme.Background);

        control.Search = new GraphSearchMarks([data.CommitAt(2).Sha], isActive: true);
        host.MoveMouse(control, control.RowBounds(5).Center);
        var shot = host.Capture();

        // FD2: the working-changes row is no commit and is not searched.
        AssertColor(theme.Background, shot.PixelAt(1, (int)working.Center.Y), "left of the working-changes row");
        AssertColor(theme.Lanes[0], shot.PixelAt(146, (int)working.Center.Y - 4), "the working-changes row's ring");
        var ink = InkOf(shot, new Rect(control.Columns.Message.X, working.Y, control.Columns.Message.Width, working.Height), theme.Background);
        Assert.InRange(ink.Strength, 0.9 * plainInk.Strength, 1.1 * plainInk.Strength);

        // A selected row that does not match keeps the selection's background, its node faded over
        // it; the hovered row keeps the hover's.
        var selected = control.RowBounds(3);
        AssertColor(theme.Selection, shot.PixelAt((int)control.Columns.Message.Right - 4, (int)selected.Y + 4), "the selected row's background");
        AssertColor(Faded(theme.Lanes[2], theme.Selection), NodePixel(shot, control, 3, 3), "the selected row's dimmed node");
        var hovered = control.RowBounds(5);
        AssertColor(theme.RowHover, shot.PixelAt((int)control.Columns.Message.Right - 4, (int)hovered.Y + 4), "the hovered row's background");
        AssertColor(DarkSearchMatch, shot.PixelAt(1, (int)control.RowBounds(2).Center.Y), "row 2's mark");
    }

    [AvaloniaFact]
    public async Task New_matches_move_the_marks_and_the_semibold_with_them()
    {
        var theme = Expected.Dark;
        var data = await GraphScenarioAsync();
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, theme.Variant);
        var plain = host.Capture();
        var plainRow2 = InkOf(plain, MessageCell(control, 2), theme.Background);
        var plainRow8 = InkOf(plain, MessageCell(control, 8), theme.Background);

        control.Search = new GraphSearchMarks([data.CommitAt(2).Sha], isActive: true);
        var first = host.Capture();
        control.Search = new GraphSearchMarks([data.CommitAt(8).Sha], isActive: true);
        var second = host.Capture();

        AssertColor(DarkSearchMatch, first.PixelAt(1, (int)control.RowBounds(2).Center.Y), "row 2 marked first");
        AssertBolder(InkOf(first, MessageCell(control, 2), theme.Background), plainRow2, "Row 2's message while it matches");
        AssertColor(theme.Background, second.PixelAt(1, (int)control.RowBounds(2).Center.Y), "row 2 no longer marked");
        var row2 = InkOf(second, MessageCell(control, 2), theme.Background);
        Assert.Equal(plainRow2.Right, row2.Right);
        Assert.InRange(row2.Strength, 0.25 * plainRow2.Strength, 0.45 * plainRow2.Strength);
        AssertColor(DarkSearchMatch, second.PixelAt(1, (int)control.RowBounds(8).Center.Y), "row 8 marked second");
        AssertBolder(InkOf(second, MessageCell(control, 8), theme.Background), plainRow8, "Row 8's message once it matches");
        Assert.Equal(GraphSearchRowState.Dimmed, control.SearchStateOf(2));
        Assert.Equal(GraphSearchRowState.Marked, control.SearchStateOf(8));
    }

    [AvaloniaFact]
    public async Task The_view_passes_the_search_marks_on_and_shows_the_reload_bar_under_the_headers_while_the_graph_reloads()
    {
        using var viewModel = await StartAsync(FakeRepository.From(await GraphScenarioAsync()));
        var view = new RepositoryGraphView { DataContext = viewModel };
        using var host = Host.Show(view, AreaWidth, 28 + RowsHeight, ThemeVariant.Dark);
        var rows = view.FindControl<CommitGraphControl>("GraphRows")!;
        var bar = view.FindControl<ProgressBar>("GraphReloading")!;
        var before = host.Capture();

        Assert.Same(viewModel.SearchMarks, rows.Search);
        Assert.False(bar.IsVisible);
        Assert.False(bar.IsIndeterminate);
        Assert.Equal("GraphReloading", AutomationProperties.GetAutomationId(bar));

        // The lead's join sets these (WP7's reload of a filter, the search's view model); their
        // setters are the view model's own, so the test sets them as that code will.
        SetViewModelProperty(viewModel, nameof(RepositoryViewModel.IsReloadingGraph), true);
        var reloading = host.Capture();

        // 8.2: a 2-px indeterminate bar in the accent colour under the column headers, over the
        // rows' top edge: the headers and the rows keep their places and, below the bar, their
        // pixels.
        Assert.True(bar.IsEffectivelyVisible);
        Assert.True(bar.IsIndeterminate);
        Assert.Equal(new Rect(0, 28, AreaWidth, 2), host.BoundsOf(bar));
        Assert.Equal(new Rect(0, 28, AreaWidth, RowsHeight), host.BoundsOf(rows));
        Assert.Equal(Color.Parse("#7C8CFF"), Assert.IsAssignableFrom<ISolidColorBrush>(bar.Foreground).Color);
        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(bar.Background).Color);
        for (var y = 0; y < 28 + RowsHeight; y += y is >= 26 and < 32 ? 1 : 7)
        {
            if (y is 28 or 29)
            {
                continue;
            }

            for (var x = 0; x < AreaWidth; x += 3)
            {
                Assert.True(before.PixelAt(x, y) == reloading.PixelAt(x, y), $"({x}, {y}) changed from {before.PixelAt(x, y)} to {reloading.PixelAt(x, y)} with the reload bar.");
            }
        }

        var marks = new GraphSearchMarks([viewModel.Graph.CommitAt(2).Sha], isActive: true);
        SetViewModelProperty(viewModel, nameof(RepositoryViewModel.SearchMarks), marks);
        SetViewModelProperty(viewModel, nameof(RepositoryViewModel.IsReloadingGraph), false);
        host.Capture();

        Assert.Same(marks, rows.Search);
        Assert.Equal(GraphSearchRowState.Marked, rows.SearchStateOf(2));
        Assert.False(bar.IsVisible);
        Assert.False(bar.IsIndeterminate);
    }

    /// <summary>Sets one of the view model's properties whose setter is its own (the lead's joins set them).</summary>
    private static void SetViewModelProperty(RepositoryViewModel viewModel, string name, object value) =>
        typeof(RepositoryViewModel).GetProperty(name)!.SetValue(viewModel, value);

    /// <summary>Asserts FD15's dash rule (<see cref="ShellDriver.DashedPixelProblems"/>) at the report's window positions.</summary>
    private static void AssertDashedInReport(Screenshot shot, int x, int top, int bottom, Color lane, Color background, int minimumDashes) =>
        Assert.Empty(ShellDriver.DashedPixelProblems(shot, x - ReportLeft, top - ReportTop, bottom - ReportTop, lane, background, minimumDashes));

    /// <summary>Asserts a solid line (<see cref="ShellDriver.SolidPixelProblems"/>) at the report's window positions.</summary>
    private static void AssertSolidInReport(Screenshot shot, int x, int top, int bottom, Color lane) =>
        Assert.Empty(ShellDriver.SolidPixelProblems(shot, x - ReportLeft, top - ReportTop, bottom - ReportTop, lane));

    /// <summary>A colour at the 35 % a search dims a row to (3.3, FD6), over a background.</summary>
    private static Color Faded(Color color, Color background)
    {
        static byte Mix(byte over, byte under) => (byte)Math.Round((CommitGraphControl.SearchDimmedOpacity * over) + ((1 - CommitGraphControl.SearchDimmedOpacity) * under));
        return Color.FromRgb(Mix(color.R, background.R), Mix(color.G, background.G), Mix(color.B, background.B));
    }

    private static int Distance(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    /// <summary>The pixel <paramref name="right"/> px right of row <paramref name="row"/>'s node's centre.</summary>
    private static Color NodePixel(Screenshot shot, CommitGraphControl control, int row, int right)
    {
        var data = control.Data!;
        var center = new Point(control.LaneCenterX(data.RowAt(row).NodeLane), control.RowBounds(row).Center.Y);
        return shot.PixelAt((int)center.X + right, (int)center.Y);
    }

    private static Rect MessageCell(CommitGraphControl control, int row) =>
        new(control.Columns.Message.X, control.RowBounds(row).Y, control.Columns.Message.Width, CommitGraphControl.RowHeight);

    private static Rect DateCell(CommitGraphControl control, int row) =>
        new(control.Columns.Date.X, control.RowBounds(row).Y, control.Columns.Date.Width, CommitGraphControl.RowHeight);

    /// <summary>
    /// How the text in an area stands out from the background: the largest difference of a pixel's
    /// channels from it, summed (<c>Strength</c>); the rightmost x where a pixel differs by more than
    /// a third of that (<c>Right</c>); and the differences of all pixels added up (<c>Mass</c>), which
    /// a semibold text has more of than the same text in the regular weight.
    /// </summary>
    private static (int Strength, int Right, long Mass) InkOf(Screenshot shot, Rect area, Color background)
    {
        var strength = 0;
        var mass = 0L;
        for (var y = (int)area.Y; y < (int)area.Bottom; y++)
        {
            for (var x = (int)area.X; x < (int)area.Right; x++)
            {
                var distance = Distance(shot.PixelAt(x, y), background);
                strength = Math.Max(strength, distance);
                mass += distance;
            }
        }

        var right = -1;
        for (var y = (int)area.Y; y < (int)area.Bottom; y++)
        {
            for (var x = (int)area.X; x < (int)area.Right; x++)
            {
                if (Distance(shot.PixelAt(x, y), background) > strength / 3)
                {
                    right = Math.Max(right, x);
                }
            }
        }

        return (strength, right, mass);
    }

    /// <summary>Asserts that a message is drawn in semibold where it was regular: it reaches further right and holds more ink.</summary>
    private static void AssertBolder((int Strength, int Right, long Mass) ink, (int Strength, int Right, long Mass) regular, string what)
    {
        Assert.True(ink.Right > regular.Right, $"{what} ends at {ink.Right}, the regular one at {regular.Right}: not semibold.");
        Assert.True(ink.Mass > regular.Mass * 1.1, $"{what} has {ink.Mass} of ink, the regular one {regular.Mass}: not semibold.");
    }

    /// <summary>Every pixel of a screenshot, to compare two frames.</summary>
    private static Color[] PixelsOf(Screenshot shot)
    {
        var pixels = new Color[shot.Width * shot.Height];
        for (var y = 0; y < shot.Height; y++)
        {
            for (var x = 0; x < shot.Width; x++)
            {
                pixels[(y * shot.Width) + x] = shot.PixelAt(x, y);
            }
        }

        return pixels;
    }

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
