using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.Threading;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.Views.Graph;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Headless tests of the commit graph control and the repository graph view (D49), drawn with
/// Skia and checked pixel by pixel against the phase 1 test report. The expected values (the
/// graph scenario's table, the colours, the sizes) are copied from the report, not from the app.
/// </summary>
public class CommitGraphControlTests
{
    /// <summary>The graph area at 1100×700: 440 wide, rows area 558 high under the 28 of the headers.</summary>
    private const double AreaWidth = 440;

    private const double RowsHeight = 558;

    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    private static readonly DateDisplay Utc = new(TimeZoneInfo.Utc);

    /// <summary>The report's table "The graph scenario's graph".</summary>
    private static readonly ScenarioRow[] GraphScenario =
    [
        new([("stash@{0}", RefLabelKind.Stash)], 0, 0, Node.Ring, "On main: Work in progress on README", "2026-01-01 12:12", "95daa96"),
        new([("feature/search", RefLabelKind.LocalBranch)], 1, 1, Node.Commit, "Highlight matches", "2026-01-01 12:11", "f463b85"),
        new([("main", RefLabelKind.CurrentBranch)], 0, 0, Node.Commit, "Add settings page", "2026-01-01 12:10", "47e6ec7"),
        new([("origin/main", RefLabelKind.RemoteBranch)], 2, 2, Node.Commit, "Fix typo in docs", "2026-01-01 12:09", "974bd83"),
        new([("v0.2", RefLabelKind.Tag)], 0, 0, Node.Commit, "Bump version", "2026-01-01 12:08", "2775228"),
        new([("bugfix/crash-on-start", RefLabelKind.LocalBranch)], 2, 3, Node.Commit, "Fix crash on start", "2026-01-01 12:07", "72267a3"),
        new([("origin/feature/search", RefLabelKind.RemoteBranch)], 1, 1, Node.Commit, "Add search box", "2026-01-01 12:06", "6aab2e4"),
        new([], 0, 0, Node.Merge, "Merge branch 'feature/login'", "2026-01-01 12:05", "f7919b8"),
        new([], 0, 0, Node.Commit, "Update README", "2026-01-01 12:04", "10bcd42"),
        new([("feature/login", RefLabelKind.LocalBranch)], 1, 4, Node.Commit, "Validate passwords", "2026-01-01 12:03", "4a62ef9"),
        new([], 1, 4, Node.Commit, "Add login form", "2026-01-01 12:02", "7b230d3"),
        new([("v0.1", RefLabelKind.Tag)], 0, 0, Node.Commit, "Add app skeleton", "2026-01-01 12:01", "933250d"),
        new([], 0, 0, Node.Commit, "Initial commit", "2026-01-01 12:00", "53347b3"),
    ];

    private enum Node
    {
        Commit,
        Merge,
        Ring,
    }

    public static TheoryData<string> Themes => ["Dark", "Light"];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task Each_row_of_the_graph_scenario_has_its_node_in_its_lane_and_colour(string themeName)
    {
        var theme = Expected.For(themeName);
        var data = await GraphScenarioAsync();
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, theme.Variant);

        var shot = host.Capture();

        Assert.Equal(GraphScenario.Length, data.Count);
        for (var row = 0; row < GraphScenario.Length; row++)
        {
            var expected = GraphScenario[row];
            Assert.Equal(expected.Message, data.CommitAt(row).Subject);
            Assert.Equal(expected.Sha, data.CommitAt(row).ShortSha);
            Assert.Equal(expected.Date, Utc.Format(data.CommitAt(row).AuthorDate));

            var center = new Point(control.LaneCenterX(expected.Lane), control.RowBounds(row).Center.Y);
            var lane = theme.Lanes[expected.Color];
            var where = $"row {row}";
            switch (expected.Node)
            {
                case Node.Ring:
                    // A ring 10 across and 2 wide, with the row's background inside.
                    AssertColor(lane, shot.PixelAt(host.ToWindow(control, center + new Vector(0, -4))), where + " ring");
                    AssertColor(theme.Background, shot.PixelAt(host.ToWindow(control, center)), where + " inside the ring");
                    break;
                case Node.Merge:
                    // A filled circle 7 across: the centre is the lane colour, 4 to the right is not.
                    AssertColor(lane, shot.PixelAt(host.ToWindow(control, center)), where);
                    AssertColor(theme.Background, shot.PixelAt(host.ToWindow(control, center + new Vector(4, 0))), where + " outside the merge node");
                    break;
                default:
                    // A filled circle 10 across.
                    AssertColor(lane, shot.PixelAt(host.ToWindow(control, center)), where);
                    AssertColor(lane, shot.PixelAt(host.ToWindow(control, center + new Vector(3, 0))), where + " 3 right of the centre");
                    break;
            }
        }

        // Lines, from "Lines:" under the table: lane 0 through row 1, lane 1 (colour 1) through
        // row 3, lane 2 (colour 3) through row 6, and lane 1 (colour 4) through row 8.
        AssertColor(theme.Lanes[0], shot.PixelAt(host.ToWindow(control, new Point(control.LaneCenterX(0), control.RowBounds(1).Y + 3))), "lane 0 in row 1");
        AssertColor(theme.Lanes[1], shot.PixelAt(host.ToWindow(control, new Point(control.LaneCenterX(1), control.RowBounds(3).Center.Y))), "lane 1 in row 3");
        AssertColor(theme.Lanes[3], shot.PixelAt(host.ToWindow(control, new Point(control.LaneCenterX(2), control.RowBounds(6).Center.Y))), "lane 2 in row 6");
        AssertColor(theme.Lanes[4], shot.PixelAt(host.ToWindow(control, new Point(control.LaneCenterX(1), control.RowBounds(8).Center.Y))), "lane 1 in row 8");

        // Below the last row the graph area is empty.
        AssertColor(theme.Background, shot.PixelAt(host.ToWindow(control, new Point(control.LaneCenterX(0), control.RowBounds(12).Bottom + 5))), "below the last row");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task Labels_of_the_graph_scenario_are_placed_and_coloured_as_the_report_says(string themeName)
    {
        var theme = Expected.For(themeName);
        var data = await GraphScenarioAsync();
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, theme.Variant);
        var shot = host.Capture();

        for (var row = 0; row < GraphScenario.Length; row++)
        {
            var expected = GraphScenario[row];
            var labels = control.LabelsInRow(row);
            var rowTop = control.RowBounds(row).Y;
            Assert.Equal(expected.Labels.Length, labels.Count);
            for (var i = 0; i < labels.Count; i++)
            {
                var (name, kind) = expected.Labels[i];
                var label = labels[i];
                Assert.Equal(name, label.FullText);
                Assert.Equal(kind, label.Kind);
                Assert.False(label.IsOverflow);
                Assert.Equal(6, label.Bounds.X);
                Assert.Equal(rowTop + 4, label.Bounds.Y);
                Assert.Equal(18, label.Bounds.Height);
                Assert.True(label.Bounds.Right <= 130, $"The label {name} ends at {label.Bounds.Right}, outside the Branch / Tag column.");

                // Whether the name is cut follows from its width in the label font (the report).
                var icon = kind is RefLabelKind.CurrentBranch or RefLabelKind.Tag ? 13 : 0;
                var fits = 6 + 6 + icon + LabelTextWidth(name, control.FontFamily) + 6 <= 130;
                if (fits)
                {
                    Assert.Equal(name, label.Text);
                }
                else
                {
                    Assert.EndsWith("…", label.Text, StringComparison.Ordinal);
                    Assert.StartsWith(label.Text[..^1], name, StringComparison.Ordinal);
                }
            }
        }

        // Fills and borders, sampled inside each kind of label at a spot its text leaves free
        // (the padding at its right end) and on its left border.
        var lane0 = theme.Lanes[0];
        var main = control.LabelsInRow(2)[0];
        AssertColor(lane0, shot.PixelAt(host.ToWindow(control, new Point(main.Bounds.Right - 2, main.Bounds.Center.Y))), "inside main's label");

        var remote = control.LabelsInRow(3)[0];
        AssertColor(theme.Lanes[2], shot.PixelAt(host.ToWindow(control, new Point(remote.Bounds.X, remote.Bounds.Center.Y))), "origin/main's border");
        AssertColor(theme.Background, shot.PixelAt(host.ToWindow(control, new Point(remote.Bounds.Right - 3, remote.Bounds.Center.Y))), "inside origin/main's label");

        var tag = control.LabelsInRow(4)[0];
        AssertColor(theme.Border, shot.PixelAt(host.ToWindow(control, new Point(tag.Bounds.X, tag.Bounds.Center.Y))), "v0.2's border");
        AssertColor(theme.ControlBackground, shot.PixelAt(host.ToWindow(control, new Point(tag.Bounds.Right - 3, tag.Bounds.Center.Y))), "inside v0.2's label");

        var stash = control.LabelsInRow(0)[0];
        AssertColor(theme.Border, shot.PixelAt(host.ToWindow(control, new Point(stash.Bounds.X, stash.Bounds.Center.Y))), "stash@{0}'s border");
        AssertColor(theme.ControlBackground, shot.PixelAt(host.ToWindow(control, new Point(stash.Bounds.Right - 3, stash.Bounds.Center.Y))), "inside stash@{0}'s label");

        var local = control.LabelsInRow(9)[0];
        AssertColor(theme.Lanes[4], shot.PixelAt(host.ToWindow(control, new Point(local.Bounds.Right - 2, local.Bounds.Center.Y))), "inside feature/login's label");
    }

    [AvaloniaFact]
    public void Labels_that_do_not_fit_become_one_plus_label_with_every_name_in_the_tooltip()
    {
        var data = Linear(30, [Branch("main", head: true), Remote("origin/main"), Tag("release-2026")]);
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        host.Capture();

        var labels = control.LabelsInRow(0);

        Assert.Equal(2, labels.Count);
        Assert.Equal(("main", RefLabelKind.CurrentBranch, false), (labels[0].Text, labels[0].Kind, labels[0].IsOverflow));
        Assert.Equal("+2", labels[1].Text);
        Assert.True(labels[1].IsOverflow);
        Assert.Equal("origin/main\nrelease-2026", labels[1].FullText);
        Assert.Equal(labels[0].Bounds.Right + 4, labels[1].Bounds.X, 3);
        Assert.True(labels[1].Bounds.Right <= 130);

        // Hovering the labels shows every name in full, one per line; elsewhere, no tooltip.
        host.MoveMouse(control, labels[1].Bounds.Center);
        Assert.Equal("main\norigin/main\nrelease-2026", ToolTip.GetTip(control));
        host.MoveMouse(control, new Point(300, labels[1].Bounds.Center.Y));
        Assert.Null(ToolTip.GetTip(control));
        host.MoveMouse(control, control.RowBounds(1).Center);
        Assert.Null(ToolTip.GetTip(control));
    }

    [AvaloniaFact]
    public void A_name_too_long_for_the_column_is_cut_with_an_ellipsis_and_still_shown()
    {
        const string longName = "feature/a-branch-name-much-too-long-for-the-column";
        var data = Linear(3, [Branch(longName, head: true)]);
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        host.Capture();

        var label = Assert.Single(control.LabelsInRow(0));

        Assert.Equal(longName, label.FullText);
        Assert.EndsWith("…", label.Text, StringComparison.Ordinal);
        Assert.StartsWith(label.Text[..^1], longName, StringComparison.Ordinal);
        Assert.True(label.Text.Length > 5, $"The cut name '{label.Text}' keeps too little of the name.");
        Assert.InRange(label.Bounds.Right, 110, 130);

        host.MoveMouse(control, label.Bounds.Center);
        Assert.Equal(longName, ToolTip.GetTip(control));
    }

    [AvaloniaFact]
    public void A_long_first_name_is_cut_to_leave_room_for_the_plus_label()
    {
        const string longName = "feature/a-branch-name-much-too-long-for-the-column";
        var data = Linear(3, [Branch(longName, head: true), Remote("origin/other")]);
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        host.Capture();

        var labels = control.LabelsInRow(0);

        Assert.Equal(2, labels.Count);
        Assert.EndsWith("…", labels[0].Text, StringComparison.Ordinal);
        Assert.Equal("+1", labels[1].Text);
        Assert.Equal("origin/other", labels[1].FullText);
        Assert.True(labels[1].Bounds.Right <= 130);
        Assert.True(labels[0].Bounds.Right + 4 <= labels[1].Bounds.X + 0.001);
    }

    [AvaloniaFact]
    public void A_detached_head_is_a_filled_head_label_before_all_others()
    {
        var data = Linear(3, [Tag("v1"), Branch("dev")], detachedAt: 0);
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        var shot = host.Capture();

        var labels = control.LabelsInRow(0);

        Assert.Equal(["HEAD", "dev", "v1"], labels.Select(label => label.Text));
        Assert.Equal(
            [RefLabelKind.DetachedHead, RefLabelKind.LocalBranch, RefLabelKind.Tag],
            labels.Select(label => label.Kind));
        Assert.Equal(6, labels[0].Bounds.X);
        AssertColor(Expected.Dark.Lanes[0], shot.PixelAt(host.ToWindow(control, new Point(labels[0].Bounds.Right - 2, labels[0].Bounds.Center.Y))), "inside the HEAD label");
    }

    [AvaloniaFact]
    public void A_click_selects_the_row_under_the_mouse_and_focuses_the_graph()
    {
        var data = Linear(30);
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);

        host.Click(control, control.RowBounds(3).Center + new Vector(80, 0));
        var shot = host.Capture();

        Assert.Equal(3, control.SelectedIndex);
        Assert.True(control.IsFocused);
        // The selection background spans the whole width: sampled in the Branch / Tag column
        // and at the far right, both free of text.
        AssertColor(Expected.Dark.Selection, shot.PixelAt(host.ToWindow(control, new Point(60, control.RowBounds(3).Center.Y))), "selected row, left");
        AssertColor(Expected.Dark.Selection, shot.PixelAt(host.ToWindow(control, new Point(AreaWidth - 2, control.RowBounds(3).Center.Y))), "selected row, right");
        AssertColor(Expected.Dark.Background, shot.PixelAt(host.ToWindow(control, new Point(60, control.RowBounds(5).Center.Y))), "an unselected row");
    }

    [AvaloniaFact]
    public void The_row_under_the_mouse_has_the_hover_background()
    {
        var data = Linear(30);
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Light);

        host.MoveMouse(control, new Point(60, control.RowBounds(4).Center.Y));
        var shot = host.Capture();

        AssertColor(Expected.Light.RowHover, shot.PixelAt(host.ToWindow(control, new Point(60, control.RowBounds(4).Center.Y))), "hovered row");
        AssertColor(Expected.Light.Background, shot.PixelAt(host.ToWindow(control, new Point(60, control.RowBounds(5).Center.Y))), "the row below");
    }

    [AvaloniaFact]
    public void Keys_move_the_selection_and_keep_it_in_view()
    {
        var data = Linear(200);
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        control.Focus();

        host.PressKey(Key.Down);
        Assert.Equal(0, control.SelectedIndex);

        host.PressKey(Key.Down);
        host.PressKey(Key.Down);
        host.PressKey(Key.Up);
        Assert.Equal(1, control.SelectedIndex);

        // 558 high: 21 whole rows per page.
        host.PressKey(Key.PageDown);
        Assert.Equal(22, control.SelectedIndex);
        Assert.Equal(control.ViewportHeight, control.RowBounds(22).Bottom, 1);

        host.PressKey(Key.End);
        Assert.Equal(199, control.SelectedIndex);
        Assert.Equal(control.MaxScrollOffset, control.ScrollOffset);
        Assert.Equal((200 * 26) - RowsHeight, control.MaxScrollOffset);
        Assert.Equal(RowsHeight, control.RowBounds(199).Bottom, 1);

        host.PressKey(Key.PageUp);
        Assert.Equal(178, control.SelectedIndex);
        Assert.Equal(0, control.RowBounds(178).Y, 1);

        host.PressKey(Key.Home);
        Assert.Equal(0, control.SelectedIndex);
        Assert.Equal(0, control.ScrollOffset);
    }

    [AvaloniaFact]
    public void With_nothing_selected_up_selects_the_first_row()
    {
        var control = new CommitGraphControl { Data = Linear(10), Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        control.Focus();

        host.PressKey(Key.Up);

        Assert.Equal(0, control.SelectedIndex);
    }

    [AvaloniaFact]
    public void Ten_wheel_notches_scroll_thirty_rows_and_back()
    {
        var control = new CommitGraphControl { Data = Linear(200), Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        var over = control.RowBounds(5).Center;

        for (var notch = 0; notch < 10; notch++)
        {
            host.Wheel(control, over, -1);
        }

        Assert.Equal(30, control.FirstVisibleRow);
        Assert.Equal(780, control.ScrollOffset);

        for (var notch = 0; notch < 4; notch++)
        {
            host.Wheel(control, over, 1);
        }

        Assert.Equal(18, control.FirstVisibleRow);

        // The wheel stops at the ends.
        for (var notch = 0; notch < 20; notch++)
        {
            host.Wheel(control, over, 1);
        }

        Assert.Equal(0, control.ScrollOffset);
    }

    [AvaloniaFact]
    public void Reveal_puts_the_row_nearest_the_middle_of_the_view_and_stops_at_the_ends()
    {
        var control = new CommitGraphControl { Data = Linear(400), Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);

        control.Reveal(200);

        var middle = control.ViewportHeight / 2;
        Assert.InRange(control.RowBounds(200).Center.Y, middle - 13, middle + 13);

        control.Reveal(2);
        Assert.Equal(0, control.ScrollOffset);

        control.Reveal(399);
        Assert.Equal(control.MaxScrollOffset, control.ScrollOffset);
    }

    [AvaloniaFact]
    public void A_reveal_before_the_graph_has_a_size_happens_once_it_has_one()
    {
        var control = new CommitGraphControl { Data = Linear(400), Dates = Utc };

        control.Reveal(300);
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);

        Assert.InRange(control.RowBounds(300).Center.Y, (RowsHeight / 2) - 13, (RowsHeight / 2) + 13);
    }

    [AvaloniaFact]
    public void Scroll_into_view_scrolls_as_little_as_possible()
    {
        var control = new CommitGraphControl { Data = Linear(400), Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);

        control.ScrollIntoView(10);
        Assert.Equal(0, control.ScrollOffset);

        control.ScrollIntoView(30);
        Assert.Equal(RowsHeight, control.RowBounds(30).Bottom, 1);

        control.ScrollIntoView(5);
        Assert.Equal(0, control.RowBounds(5).Y, 1);
    }

    [AvaloniaFact]
    public void Drawing_rows_raises_rows_drawn_and_frame_drawn_with_the_time_it_took()
    {
        var control = new CommitGraphControl { Data = Linear(50), Dates = Utc };
        var rowsDrawn = 0;
        var frames = new List<TimeSpan>();
        control.RowsDrawn += (_, _) => rowsDrawn++;
        control.FrameDrawn += (_, duration) => frames.Add(duration);
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);

        host.Capture();
        host.Wheel(control, control.RowBounds(2).Center, -1);
        host.Capture();

        Assert.True(rowsDrawn >= 2, $"RowsDrawn was raised {rowsDrawn} times.");
        Assert.Equal(rowsDrawn, frames.Count);
        Assert.All(frames, duration => Assert.True(duration > TimeSpan.Zero));
    }

    [AvaloniaFact]
    public void Nothing_is_drawn_and_no_rows_are_reported_without_commits()
    {
        var control = new CommitGraphControl { Data = Linear(0), Dates = Utc };
        var rowsDrawn = 0;
        control.RowsDrawn += (_, _) => rowsDrawn++;
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);

        var shot = host.Capture();

        Assert.Equal(0, rowsDrawn);
        Assert.Equal(0, control.MaxScrollOffset);
        AssertColor(Expected.Dark.Background, shot.PixelAt(host.ToWindow(control, new Point(146, 13))), "the empty graph");
    }

    [AvaloniaFact]
    public void Rows_appended_while_loading_extend_the_scroll_range_and_a_new_graph_keeps_the_offset()
    {
        var data = new CommitGraphData(new RepoRefs(new HeadState("main", null), [], [], []));
        var control = new CommitGraphControl { Data = data, Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        var commits = LinearCommits(300);
        var layout = new GraphLayout();

        data.Append(commits[..100], commits[..100].Select(layout.Add).ToList());
        Assert.Equal((100 * 26) - RowsHeight, control.MaxScrollOffset);
        data.Append(commits[100..], commits[100..].Select(layout.Add).ToList());
        Assert.Equal((300 * 26) - RowsHeight, control.MaxScrollOffset);

        control.ScrollOffset = 2600;
        control.Data = Linear(300);
        Assert.Equal(2600, control.ScrollOffset);

        // A shorter history clamps the offset to its own end.
        control.Data = Linear(40);
        Assert.Equal((40 * 26) - RowsHeight, control.ScrollOffset);

        control.ScrollOffset = -50;
        Assert.Equal(0, control.ScrollOffset);
    }

    [AvaloniaFact]
    public void Switching_the_theme_redraws_with_the_other_palette()
    {
        var control = new CommitGraphControl { Data = Linear(5), Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        var node = host.ToWindow(control, new Point(control.LaneCenterX(0), control.RowBounds(1).Center.Y));
        AssertColor(Expected.Dark.Lanes[0], host.Capture().PixelAt(node), "dark node");

        host.Window.RequestedThemeVariant = ThemeVariant.Light;
        var shot = host.Capture();

        AssertColor(Expected.Light.Lanes[0], shot.PixelAt(node), "light node");
        AssertColor(Expected.Light.Background, shot.PixelAt(host.ToWindow(control, new Point(60, control.RowBounds(1).Center.Y))), "light background");
    }

    [AvaloniaFact]
    public void The_graph_is_one_ui_automation_element_with_its_name()
    {
        var control = new CommitGraphControl { Data = Linear(5) };

        var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(control);

        Assert.Equal("GraphRows", peer.GetAutomationId());
        Assert.Equal("Commit graph", peer.GetName());
        Assert.True(peer.IsControlElement());
        Assert.True(control.Focusable);
    }

    [AvaloniaFact]
    public async Task The_view_hides_the_author_and_sha_headers_at_440_and_shows_all_six_at_1260()
    {
        using var viewModel = await StartAsync(FakeRepository.From(await GraphScenarioAsync()));
        var view = new RepositoryGraphView { DataContext = viewModel };
        using var host = Host.Show(view, AreaWidth, 28 + RowsHeight, ThemeVariant.Dark);
        var rows = view.FindControl<CommitGraphControl>("GraphRows")!;
        string[] names = ["HeaderRefs", "HeaderGraph", "HeaderMessage", "HeaderAuthor", "HeaderDate", "HeaderSha"];
        TextBlock Header(string name) => view.FindControl<TextBlock>(name)!;

        host.Capture();

        Assert.Equal(new Rect(0, 28, AreaWidth, RowsHeight), host.BoundsOf(rows));
        Assert.Equal(13, rows.Data!.Count);
        Assert.Equal(64, rows.Columns.Graph.Width);
        Assert.Equal(
            [true, true, true, false, true, false],
            names.Select(name => Header(name).IsVisible));
        Assert.Equal(["Branch / Tag", "Graph", "Message", "Author", "Date", "SHA"], names.Select(name => Header(name).Text));
        Assert.Equal(8, host.BoundsOf(Header("HeaderRefs")).X, 1);
        Assert.Equal(130 + 8, host.BoundsOf(Header("HeaderGraph")).X, 1);
        Assert.Equal(194 + 8, host.BoundsOf(Header("HeaderMessage")).X, 1);
        Assert.Equal(320 + 8, host.BoundsOf(Header("HeaderDate")).X, 1);
        Assert.Empty(LayoutAudit.FindClippedText(host.Window));
        Assert.False(view.FindControl<ScrollBar>("GraphScrollBar")!.IsVisible);
        Assert.False(view.FindControl<StackPanel>("NoCommits")!.IsVisible);
        Assert.False(view.FindControl<TextBlock>("GraphError")!.IsVisible);

        host.Window.Width = 1260;
        host.Capture();

        Assert.All(names, name => Assert.True(Header(name).IsVisible, $"{name} is hidden at 1260."));
        Assert.Equal(734, rows.Columns.Message.Width);
        Assert.Equal(928 + 8, host.BoundsOf(Header("HeaderAuthor")).X, 1);
        Assert.Equal(1068 + 8, host.BoundsOf(Header("HeaderDate")).X, 1);
        Assert.Equal(1188 + 8, host.BoundsOf(Header("HeaderSha")).X, 1);
        Assert.Empty(LayoutAudit.FindClippedText(host.Window));
    }

    [AvaloniaFact]
    public async Task The_view_selects_through_the_view_model_and_reveals_what_it_asks_for()
    {
        var repository = FakeRepository.Linear(400);
        using var viewModel = await StartAsync(repository);
        var view = new RepositoryGraphView { DataContext = viewModel };
        using var host = Host.Show(view, AreaWidth, 28 + RowsHeight, ThemeVariant.Dark);
        var rows = view.FindControl<CommitGraphControl>("GraphRows")!;
        Assert.Equal(400, viewModel.Graph.Count);

        host.Click(rows, rows.RowBounds(4).Center);
        Assert.Equal(4, viewModel.SelectedIndex);

        viewModel.Select(repository.Commits[250].Sha, reveal: true);
        Assert.Equal(250, rows.SelectedIndex);
        Assert.InRange(rows.RowBounds(250).Center.Y, (RowsHeight / 2) - 13, (RowsHeight / 2) + 13);
        Assert.Equal(rows.ScrollOffset, viewModel.ScrollOffset);

        host.Wheel(rows, rows.RowBounds(3).Center, -2);
        Assert.Equal(rows.ScrollOffset, viewModel.ScrollOffset);

        var scrollBar = view.FindControl<ScrollBar>("GraphScrollBar")!;
        Assert.True(scrollBar.IsVisible);
        Assert.Equal(rows.MaxScrollOffset, scrollBar.Maximum);
        Assert.Equal(RowsHeight, scrollBar.ViewportSize);
        Assert.Equal(rows.ScrollOffset, scrollBar.Value);
        Assert.Equal(AreaWidth, host.BoundsOf(scrollBar).Right);
        Assert.Equal(AreaWidth, rows.Bounds.Width);

        scrollBar.Value = 1300;
        Assert.Equal(1300, rows.ScrollOffset);
        Assert.Equal(1300, viewModel.ScrollOffset);
        Assert.Equal(50, rows.FirstVisibleRow);

        // The wheel over the scroll bar's strip scrolls the rows like the wheel anywhere else.
        host.Wheel(scrollBar, new Point(scrollBar.Bounds.Width / 2, 200), -1);
        Assert.Equal(1300 + 78, rows.ScrollOffset);
        Assert.Equal(1300 + 78, scrollBar.Value);
    }

    [AvaloniaFact]
    public async Task Each_tab_keeps_its_own_selection_and_scroll_offset_when_the_view_switches()
    {
        using var large = await StartAsync(FakeRepository.Linear(400));
        using var small = await StartAsync(FakeRepository.Linear(10));
        using var other = await StartAsync(FakeRepository.Linear(500));
        var view = new RepositoryGraphView { DataContext = large };
        using var host = Host.Show(view, AreaWidth, 28 + RowsHeight, ThemeVariant.Dark);
        var rows = view.FindControl<CommitGraphControl>("GraphRows")!;
        var over = rows.RowBounds(2).Center;
        host.Click(rows, over);
        for (var notch = 0; notch < 10; notch++)
        {
            host.Wheel(rows, over, -1);
        }

        other.ScrollOffset = 9000;
        other.SelectedIndex = 360;

        view.DataContext = small;
        host.Capture();
        Assert.Same(small.Graph, rows.Data);
        Assert.Equal(0, rows.ScrollOffset);
        Assert.Equal(-1, rows.SelectedIndex);
        Assert.Equal(780, large.ScrollOffset);
        Assert.Equal(2, large.SelectedIndex);

        view.DataContext = other;
        host.Capture();
        Assert.Equal(9000, rows.ScrollOffset);
        Assert.Equal(360, rows.SelectedIndex);
        Assert.Equal(9000, other.ScrollOffset);

        view.DataContext = large;
        host.Capture();
        Assert.Equal(780, rows.ScrollOffset);
        Assert.Equal(2, rows.SelectedIndex);
        Assert.Equal(30, rows.FirstVisibleRow);
    }

    [AvaloniaFact]
    public async Task A_repository_without_commits_says_so_under_the_headers()
    {
        var repository = FakeRepository.Linear(0);
        using var viewModel = await StartAsync(repository);
        var view = new RepositoryGraphView { DataContext = viewModel };
        using var host = Host.Show(view, AreaWidth, 28 + RowsHeight, ThemeVariant.Dark);
        host.Capture();

        var title = view.FindControl<TextBlock>("NoCommitsTitle")!;
        var text = view.FindControl<TextBlock>("NoCommitsText")!;

        Assert.True(viewModel.HasNoCommits);
        Assert.True(view.FindControl<StackPanel>("NoCommits")!.IsVisible);
        Assert.Equal("No commits yet", title.Text);
        Assert.Equal(16, title.FontSize);
        Assert.Equal(FontWeight.SemiBold, title.FontWeight);
        Assert.Equal("Make the first commit in this repository to see it here.", text.Text);
        Assert.Contains("secondary", text.Classes);
        Assert.True(view.FindControl<Border>("ColumnHeaders")!.IsVisible);
        var titleBounds = host.BoundsOf(title);
        Assert.InRange(titleBounds.Center.X, (AreaWidth / 2) - 1, (AreaWidth / 2) + 1);
        Assert.True(titleBounds.Y > 28);
        Assert.Empty(LayoutAudit.FindClippedText(host.Window));
    }

    [AvaloniaFact]
    public async Task A_repository_that_cannot_be_read_shows_the_error()
    {
        var repository = FakeRepository.Linear(0);
        repository.Failure = new InvalidOperationException("fatal: bad object HEAD");
        using var viewModel = await StartAsync(repository);
        var view = new RepositoryGraphView { DataContext = viewModel };
        using var host = Host.Show(view, AreaWidth, 28 + RowsHeight, ThemeVariant.Dark);
        host.Capture();

        var error = view.FindControl<TextBlock>("GraphError")!;

        Assert.True(error.IsVisible);
        Assert.Equal("fatal: bad object HEAD", error.Text);
        Assert.Contains("error", error.Classes);
        Assert.False(view.FindControl<StackPanel>("NoCommits")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task Rows_drawn_by_the_view_are_measured_by_the_view_model()
    {
        var log = new RecordingLog();
        var viewModel = await StartAsync(FakeRepository.From(await GraphScenarioAsync()), log);
        var view = new RepositoryGraphView { DataContext = viewModel };
        using (var host = Host.Show(view, AreaWidth, 28 + RowsHeight, ThemeVariant.Dark))
        {
            host.Capture();
            host.Window.Height = 28 + 200;
            host.Capture();
        }

        viewModel.Dispose();

        Assert.Single(log.Messages, message => message.StartsWith("fake: first graph rows drawn after ", StringComparison.Ordinal));
        var frames = Assert.Single(log.Messages, message => message.StartsWith("fake: graph frames: ", StringComparison.Ordinal));
        Assert.Matches(@"^fake: graph frames: \d+ drawn, 95th percentile \d+\.\d ms, longest \d+\.\d ms$", frames);
    }

    [AvaloniaFact]
    public async Task The_100k_commit_repo_draws_every_row_in_its_lane_and_colour_at_the_middle_and_the_end()
    {
        var path = await LargeHistory.GetAsync();
        using var viewModel = await OpenAsync(path);
        var view = new RepositoryGraphView { DataContext = viewModel };
        using var host = Host.Show(view, AreaWidth, 28 + RowsHeight, ThemeVariant.Dark);
        var rows = view.FindControl<CommitGraphControl>("GraphRows")!;
        var frames = new List<TimeSpan>();
        rows.FrameDrawn += (_, duration) => frames.Add(duration);

        Assert.Equal(LargeHistory.CommitCount, viewModel.Graph.Count);
        Assert.Equal(48, rows.Columns.Graph.Width);
        Assert.Equal(["Merge commit 100000", "Feature commit 99999"], new[] { viewModel.Graph.CommitAt(0).Subject, viewModel.Graph.CommitAt(1).Subject });
        AssertLargeHistoryRows(host, rows, "top");
        Assert.Equal(["main"], rows.LabelsInRow(0).Select(label => label.Text));
        Assert.Equal(["feature"], rows.LabelsInRow(1).Select(label => label.Text));

        viewModel.Select(viewModel.Graph.CommitAt(LargeHistory.CommitCount / 2).Sha, reveal: true);
        Assert.Equal(50_000, rows.SelectedIndex);
        Assert.Equal(["middle"], rows.LabelsInRow(50_000).Select(label => label.Text));
        Assert.InRange(rows.RowBounds(50_000).Center.Y, (RowsHeight / 2) - 13, (RowsHeight / 2) + 13);
        AssertLargeHistoryRows(host, rows, "middle");

        rows.Focus();
        host.PressKey(Key.End);
        Assert.Equal(LargeHistory.CommitCount - 1, rows.SelectedIndex);
        Assert.Equal("Main commit 1", viewModel.Graph.CommitAt(rows.SelectedIndex).Subject);
        Assert.Equal(RowsHeight, rows.RowBounds(rows.SelectedIndex).Bottom, 1);
        Assert.True(rows.RowBounds(rows.FirstVisibleRow).Y < 0, "The top row of the view should be cut at the end.");
        AssertLargeHistoryRows(host, rows, "end");

        host.PressKey(Key.Home);
        Assert.Equal(0, rows.SelectedIndex);
        Assert.Equal(0, rows.ScrollOffset);
        AssertLargeHistoryRows(host, rows, "back at the top");

        // An indication only (D50): the real-window pass judges frame times on the Windows machine.
        TestContext.Current.TestOutputHelper?.WriteLine(
            "100k graph frames: " + string.Join(", ", frames.Select(frame => frame.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture))) + " ms");
    }

    /// <summary>
    /// Every row whose centre is in view has its node in the lane and colour that the report's
    /// "Scenario repos" gives the 100k-commit repo: lane 1 for the feature commits, in colour
    /// <c>(100000 - m) / 50 + 1</c> below "Merge commit m"; lane 0 in colour 0 for the rest.
    /// </summary>
    private static void AssertLargeHistoryRows(Host host, CommitGraphControl rows, string where)
    {
        var shot = host.Capture();
        var checkedRows = 0;
        for (var row = rows.FirstVisibleRow; row < LargeHistory.CommitCount; row++)
        {
            var bounds = rows.RowBounds(row);
            if (bounds.Y >= rows.ViewportHeight)
            {
                break;
            }

            if (bounds.Center.Y < 0 || bounds.Center.Y >= rows.ViewportHeight)
            {
                continue;
            }

            var number = LargeHistory.CommitInRow(row);
            var lane = LargeHistory.LaneOf(number);
            var color = 0;
            if (LargeHistory.IsFeature(number))
            {
                var merge = number - (number % LargeHistory.CycleLength) + LargeHistory.CycleLength;
                color = (((LargeHistory.CommitCount - merge) / LargeHistory.CycleLength) + 1) % 8;
            }

            var center = new Point(rows.LaneCenterX(lane), bounds.Center.Y);
            AssertColor(Expected.Dark.Lanes[color], shot.PixelAt(host.ToWindow(rows, center)), $"{where}: row {row} ({LargeHistory.SubjectOf(number)})");
            checkedRows++;
        }

        Assert.InRange(checkedRows, 21, 22);
    }

    /// <summary>
    /// A label's text width in the label font: 11 pixels, semibold, in the window's family. The
    /// family must be the window's (Inter through the app's composite family): measured with
    /// <see cref="FontFamily.Default"/>, semibold is not found and the text comes out narrower.
    /// </summary>
    private static double LabelTextWidth(string text, FontFamily family) =>
        new TextLayout(text, new Typeface(family, FontStyle.Normal, FontWeight.SemiBold), 11, Brushes.Black).WidthIncludingTrailingWhitespace;

    private static void AssertColor(Color expected, Color actual, string where, int tolerance = 3)
    {
        var close = Math.Abs(expected.R - actual.R) <= tolerance
            && Math.Abs(expected.G - actual.G) <= tolerance
            && Math.Abs(expected.B - actual.B) <= tolerance;
        Assert.True(close, $"{where}: expected {expected}, found {actual}.");
    }

    /// <summary>
    /// The graph scenario's graph data, read with real git once per test process: reading it
    /// takes a few seconds of git calls, and the data never changes once complete, so every test
    /// can show the same instance.
    /// </summary>
    private static Task<CommitGraphData> GraphScenarioAsync() => GraphScenarioData.Value;

    private static readonly Lazy<Task<CommitGraphData>> GraphScenarioData = new(LoadGraphScenarioAsync);

    /// <summary>Loads the graph scenario's data the way the view model does, without a view model.</summary>
    private static async Task<CommitGraphData> LoadGraphScenarioAsync()
    {
        using var repo = await Scenarios.GraphAsync().ConfigureAwait(false);
        var repository = await GitRepository.OpenAsync(Runner, repo.Path).ConfigureAwait(false);
        var refs = await repository.ReadRefsAsync().ConfigureAwait(false);
        var pages = new List<IReadOnlyList<CommitInfo>>();
        await repository.LoadCommitsAsync(refs, page =>
        {
            lock (pages)
            {
                pages.Add(page);
            }
        }).ConfigureAwait(false);

        // CommitGraphData is filled on the UI thread in the app; nothing observes it yet here.
        var data = new CommitGraphData(refs);
        var layout = new GraphLayout();
        foreach (var page in pages)
        {
            data.Append(page, page.Select(layout.Add).ToList());
        }

        data.Complete();
        return data;
    }

    private static async Task<RepositoryViewModel> OpenAsync(string path, RecordingLog? log = null)
    {
        var repository = await GitRepository.OpenAsync(Runner, path, TestContext.Current.CancellationToken);
        return await StartAsync(repository, log);
    }

    private static async Task<RepositoryViewModel> StartAsync(IGitRepository repository, RecordingLog? log = null)
    {
        var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), Utc, log);
        await viewModel.StartAsync();
        return viewModel;
    }

    /// <summary>A history of <paramref name="count"/> commits in a line, newest first, made in memory.</summary>
    private static CommitInfo[] LinearCommits(int count)
    {
        var start = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var commits = new CommitInfo[count];
        for (var row = 0; row < count; row++)
        {
            var number = count - row;
            var parents = number > 1 ? new[] { ShaOf(number - 1) } : [];
            var date = start.AddMinutes(number - 1);
            commits[row] = new CommitInfo(ShaOf(number), parents, "Test Author", "author@example.com", date, date, $"Commit {number}");
        }

        return commits;
    }

    private static string ShaOf(int number) => number.ToString("x40", CultureInfo.InvariantCulture);

    /// <summary>
    /// A complete graph of <see cref="LinearCommits"/>, with <paramref name="refsOnTop"/> pointing
    /// at the newest commit and HEAD on the first branch among them, or detached at row
    /// <paramref name="detachedAt"/>.
    /// </summary>
    private static CommitGraphData Linear(int count, IReadOnlyList<Func<string, GitRef>>? refsOnTop = null, int detachedAt = -1)
    {
        var commits = LinearCommits(count);
        var top = count > 0 ? commits[0].Sha : ShaOf(1);
        var refs = (refsOnTop ?? []).Select(make => make(top)).ToList();
        var head = detachedAt >= 0
            ? new HeadState(null, commits[detachedAt].Sha)
            : new HeadState(refs.FirstOrDefault(gitRef => gitRef.IsHead)?.Name ?? "main", count > 0 ? top : null);
        var data = new CommitGraphData(new RepoRefs(head, refs, [], ["origin"]));
        var layout = new GraphLayout();
        if (count > 0)
        {
            data.Append(commits, commits.Select(layout.Add).ToList());
        }

        data.Complete();
        return data;
    }

    private static Func<string, GitRef> Branch(string name, bool head = false) =>
        sha => new GitRef("refs/heads/" + name, name, RefKind.LocalBranch, sha, IsHead: head);

    private static Func<string, GitRef> Remote(string name) =>
        sha => new GitRef("refs/remotes/" + name, name, RefKind.RemoteBranch, sha, RemoteName: name.Split('/')[0]);

    private static Func<string, GitRef> Tag(string name) =>
        sha => new GitRef("refs/tags/" + name, name, RefKind.Tag, sha);

    private sealed record ScenarioRow((string Name, RefLabelKind Kind)[] Labels, int Lane, int Color, Node Node, string Message, string Date, string Sha);

    /// <summary>The colours of the report and the token table, per theme.</summary>
    private sealed record Expected(ThemeVariant Variant, Color[] Lanes, Color Background, Color Selection, Color RowHover, Color ControlBackground, Color Border)
    {
        public static readonly Expected Dark = new(
            ThemeVariant.Dark,
            Parse("#7C8CFF", "#3FB97F", "#E0A23B", "#E5606B", "#4FB3D9", "#B07CE8", "#D97EB6", "#8FB84A"),
            Color.Parse("#14161B"),
            Color.Parse("#2A3150"),
            Color.Parse("#1C1F27"),
            Color.Parse("#272C36"),
            Color.Parse("#323845"));

        public static readonly Expected Light = new(
            ThemeVariant.Light,
            Parse("#4353D8", "#1E8E5A", "#B7791F", "#C93A46", "#1F86B0", "#8048C7", "#B54A8C", "#5F8A1E"),
            Color.Parse("#FFFFFF"),
            Color.Parse("#DDE1FA"),
            Color.Parse("#F3F4F7"),
            Color.Parse("#FFFFFF"),
            Color.Parse("#D2D6DE"));

        public static Expected For(string name) => name == "Light" ? Light : Dark;

        private static Color[] Parse(params string[] colors) => colors.Select(Color.Parse).ToArray();
    }

    /// <summary>A window that shows one control or view, at a size and in a theme of its own, and drives it with simulated input.</summary>
    private sealed class Host : IDisposable
    {
        private Host(Window window)
        {
            Window = window;
        }

        public Window Window { get; }

        public static Host Show(Control content, double width, double height, ThemeVariant theme)
        {
            // The theme is set on the window, not the application: other tests on the same UI
            // thread may change the application's theme while this one waits.
            var window = new Window { Width = width, Height = height, Content = content, RequestedThemeVariant = theme };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            return new Host(window);
        }

        public Screenshot Capture()
        {
            Dispatcher.UIThread.RunJobs();
            var frame = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The window has not rendered a frame.");
            return new Screenshot(frame);
        }

        public Point ToWindow(Visual visual, Point point) =>
            visual.TranslatePoint(point, Window) ?? throw new InvalidOperationException("The control is not in the window.");

        public Rect BoundsOf(Visual visual) => new(ToWindow(visual, default), visual.Bounds.Size);

        public void MoveMouse(Visual visual, Point point)
        {
            Window.MouseMove(ToWindow(visual, point));
            Dispatcher.UIThread.RunJobs();
        }

        public void Click(Visual visual, Point point)
        {
            var position = ToWindow(visual, point);
            Window.MouseMove(position);
            Window.MouseDown(position, MouseButton.Left);
            Window.MouseUp(position, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        public void Wheel(Visual visual, Point point, double deltaY)
        {
            var position = ToWindow(visual, point);
            Window.MouseMove(position);
            Window.MouseWheel(position, new Vector(0, deltaY));
            Dispatcher.UIThread.RunJobs();
        }

        public void PressKey(Key key)
        {
            Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
            Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }

    /// <summary>
    /// A repository served from memory, for view tests that need a view model but not git:
    /// quick, and the same on every machine. It holds a line of commits, or the commits and refs
    /// of graph data read once from a real repository.
    /// </summary>
    private sealed class FakeRepository(IReadOnlyList<CommitInfo> commits, RepoRefs refs) : IGitRepository
    {
        public IReadOnlyList<CommitInfo> Commits => commits;

        /// <summary>When set, reading the refs fails with it, as git failing would.</summary>
        public Exception? Failure { get; set; }

        public string WorkingDirectory => Path.Combine(Path.GetTempPath(), "fake");

        public string GitDirectory => Path.Combine(WorkingDirectory, ".git");

        public string CommonDirectory => GitDirectory;

        public string Name => "fake";

        /// <summary><paramref name="count"/> commits in a line, with <c>main</c> (HEAD) on the newest.</summary>
        public static FakeRepository Linear(int count)
        {
            var line = LinearCommits(count);
            var top = count > 0 ? line[0].Sha : null;
            List<GitRef> branches = top is null ? [] : [new("refs/heads/main", "main", RefKind.LocalBranch, top, IsHead: true)];
            return new FakeRepository(line, new RepoRefs(new HeadState("main", top), branches, [], []));
        }

        /// <summary>The commits and refs of loaded graph data.</summary>
        public static FakeRepository From(CommitGraphData data) =>
            new(Enumerable.Range(0, data.Count).Select(data.CommitAt).ToList(), data.Refs);

        public Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) =>
            Failure is null ? Task.FromResult(refs) : Task.FromException<RepoRefs>(Failure);

        public Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default)
        {
            for (var start = 0; start < commits.Count; start += 100)
            {
                onPage(commits.Skip(start).Take(100).ToList());
            }

            return Task.CompletedTask;
        }

        public Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default)
        {
            var commit = Commits.First(candidate => candidate.Sha == sha);
            return Task.FromResult(new CommitDetails(
                commit.Sha,
                commit.Parents,
                commit.AuthorName,
                commit.AuthorEmail,
                commit.AuthorDate,
                commit.AuthorName,
                commit.AuthorEmail,
                commit.CommitDate,
                commit.Subject,
                string.Empty,
                []));
        }

        public IRepositoryWatcher CreateWatcher() => new QuietWatcher();

        private sealed class QuietWatcher : IRepositoryWatcher
        {
            public event EventHandler? Changed
            {
                add { }
                remove { }
            }

            public void Start()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
