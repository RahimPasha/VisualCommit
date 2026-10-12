using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Conflicts;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Headless tests of the conflict resolver's panes (<see cref="ConflictPanes"/>,
/// docs/phase-4-design.md 11.2, 11.3, 11.5): the ours and theirs files side by side on phase 2's
/// diff panes, rows lined up with fillers, a bar with its buttons before each block, one number
/// column and no sign column, scrolling together. At the top half's size at 1100×700 (840 wide).
/// </summary>
public class ConflictPanesTests
{
    private const double Width = 840;
    private const double Height = 300;

    private static readonly Color DarkOurs = Color.Parse("#15323A");
    private static readonly Color DarkTheirs = Color.Parse("#2C2140");
    private static readonly Color LightOurs = Color.Parse("#DCF1F5");
    private static readonly Color LightTheirs = Color.Parse("#F0E6FA");

    public static TheoryData<string> Themes => ["Dark", "Light"];

    /// <summary>
    /// A file with two blocks whose sides differ in length: the first has one ours line and three
    /// theirs lines, the second two ours lines and one theirs line.
    /// </summary>
    private static IReadOnlyList<ConflictPanesPart> TwoBlocks() =>
    [
        ConflictPanesPart.Common("public static class Calculator", "{"),
        ConflictPanesPart.Block(
            ["    public static int Add(int a, int b) => a + b;"],
            ["    // Adds two numbers.", "    public static int Add(int a, int b)", "        => checked(a + b);"]),
        ConflictPanesPart.Common(string.Empty, "    public static int Twice(int a) => a * 2;"),
        ConflictPanesPart.Block(
            ["    // Ours: halves.", "    public static int Half(int a) => a / 2;"],
            ["    public static int Third(int a) => a / 3;"]),
        ConflictPanesPart.Common("}"),
    ];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Rows_line_up_with_fillers_and_bars_and_each_side_counts_its_own_lines(string themeName)
    {
        var colors = DiffColors.For(themeName);
        var panes = new ConflictPanes { Parts = TwoBlocks(), FilePath = "src/Calculator.cs" };
        using var host = DiffHost.Show(panes, Width, Height, colors.Variant);
        var shot = host.Capture();
        TestPictures.Save(host, $"conflict-panes-{themeName.ToLowerInvariant()}");

        var ours = panes.RowsOf(ConflictPaneSide.Ours);
        var theirs = panes.RowsOf(ConflictPaneSide.Theirs);
        Assert.Equal(
            [
                ("public static class Calculator", ConflictTint.None, false, (int?)1),
                ("{", ConflictTint.None, false, 2),
                (string.Empty, ConflictTint.Bar, true, null),
                ("    public static int Add(int a, int b) => a + b;", ConflictTint.Ours, false, 3),
                (string.Empty, ConflictTint.Filler, false, null),
                (string.Empty, ConflictTint.Filler, false, null),
                (string.Empty, ConflictTint.None, false, 4),
                ("    public static int Twice(int a) => a * 2;", ConflictTint.None, false, 5),
                (string.Empty, ConflictTint.Bar, true, null),
                ("    // Ours: halves.", ConflictTint.Ours, false, 6),
                ("    public static int Half(int a) => a / 2;", ConflictTint.Ours, false, 7),
                ("}", ConflictTint.None, false, 8),
            ],
            ours.Select(row => (row.Text, row.Tint, row.IsBar, row.Number)));
        Assert.Equal(
            [
                ("public static class Calculator", ConflictTint.None, false, (int?)1),
                ("{", ConflictTint.None, false, 2),
                (string.Empty, ConflictTint.Bar, true, null),
                ("    // Adds two numbers.", ConflictTint.Theirs, false, 3),
                ("    public static int Add(int a, int b)", ConflictTint.Theirs, false, 4),
                ("        => checked(a + b);", ConflictTint.Theirs, false, 5),
                (string.Empty, ConflictTint.None, false, 6),
                ("    public static int Twice(int a) => a * 2;", ConflictTint.None, false, 7),
                (string.Empty, ConflictTint.Bar, true, null),
                ("    public static int Third(int a) => a / 3;", ConflictTint.Theirs, false, 8),
                (string.Empty, ConflictTint.Filler, false, null),
                ("}", ConflictTint.None, false, 9),
            ],
            theirs.Select(row => (row.Text, row.Tint, row.IsBar, row.Number)));

        // Each row is at the same height in both panes: a bar 26 high, a line 15.84 (on whole pixels).
        for (var row = 0; row < ours.Count; row++)
        {
            var left = panes.RowBounds(ConflictPaneSide.Ours, row) ?? throw new InvalidOperationException($"Ours row {row} is not in view.");
            var right = panes.RowBounds(ConflictPaneSide.Theirs, row) ?? throw new InvalidOperationException($"Theirs row {row} is not in view.");
            Assert.Equal(left.Y, right.Y);
            Assert.Equal(left.Height, right.Height);
            if (ours[row].IsBar)
            {
                Assert.Equal(26, left.Height);
            }
            else
            {
                Assert.InRange(left.Height, 15, 16);
            }
        }

        Assert.Equal(26 + (2 * 15.84), panes.RowBounds(ConflictPaneSide.Ours, 3)!.Value.Y, 0.5);
        Assert.Equal((2 * 26) + (9 * 15.84), panes.RowBounds(ConflictPaneSide.Ours, 11)!.Value.Y, 0.5);

        // One number column (8 + 7.2 × 2 + 8 = 30.4) and no sign column: each pane's text starts
        // 30.4 from its left edge (layout rounding puts the text view on a whole pixel). The
        // halves are 419.5 wide with the 1-pixel line between them: the theirs pane starts at
        // 420.5, on a whole pixel.
        var theirsLeft = panes.RowBounds(ConflictPaneSide.Theirs, 0)!.Value.X;
        Assert.Equal(0, panes.RowBounds(ConflictPaneSide.Ours, 0)!.Value.X);
        Assert.Equal(420.5, theirsLeft, 1.0);
        Assert.Equal(30.4, panes.TextLeft(ConflictPaneSide.Ours)!.Value, 1.0);
        Assert.Equal(30.4, panes.TextLeft(ConflictPaneSide.Theirs)!.Value - theirsLeft, 1.0);

        // Backgrounds, gutter included: sampled 2 from each pane's left edge and 20 from its right
        // edge, where no text reaches.
        var (oursTint, theirsTint) = themeName == "Light" ? (LightOurs, LightTheirs) : (DarkOurs, DarkTheirs);
        foreach (var (side, row, color, what) in new[]
        {
            (ConflictPaneSide.Ours, 0, colors.Background, "a common line, ours"),
            (ConflictPaneSide.Ours, 2, colors.Hunk, "the first bar, ours"),
            (ConflictPaneSide.Ours, 3, oursTint, "the first block's ours line"),
            (ConflictPaneSide.Ours, 4, colors.Filler, "the first block's first filler, ours"),
            (ConflictPaneSide.Theirs, 3, theirsTint, "the first block's first theirs line"),
            (ConflictPaneSide.Theirs, 5, theirsTint, "the first block's last theirs line"),
            (ConflictPaneSide.Theirs, 8, colors.Hunk, "the second bar, theirs"),
            (ConflictPaneSide.Theirs, 10, colors.Filler, "the second block's filler, theirs"),
            (ConflictPaneSide.Theirs, 11, colors.Background, "the last line, theirs"),
        })
        {
            var bounds = panes.RowBounds(side, row)!.Value;
            var y = bounds.Center.Y;
            DiffColors.AssertColor(color, shot.PixelAt(host.ToWindow(panes, new Point(bounds.X + 2, y))), $"{what}, in the gutter");
            DiffColors.AssertColor(color, shot.PixelAt(host.ToWindow(panes, new Point(bounds.Right - 20, y))), $"{what}, right of the text");
        }
    }

    [AvaloniaFact]
    public void The_bars_have_their_buttons_with_their_ids_first_in_the_bar_and_a_click_asks_for_the_choice()
    {
        var panes = new ConflictPanes { Parts = TwoBlocks() };
        var choices = new List<(int Block, ConflictChoice Choice)>();
        panes.ChoiceRequested += (_, e) => choices.Add((e.Block, e.Choice));
        using var host = DiffHost.Show(panes, Width, Height, ThemeVariant.Dark);
        host.Capture();

        var expected = new[]
        {
            (ConflictPaneSide.Ours, ConflictChoice.UseOurs, "Use ours", "ConflictUseOurs"),
            (ConflictPaneSide.Ours, ConflictChoice.UseBothOursFirst, "Use both", "ConflictUseBothOursFirst"),
            (ConflictPaneSide.Theirs, ConflictChoice.UseTheirs, "Use theirs", "ConflictUseTheirs"),
            (ConflictPaneSide.Theirs, ConflictChoice.UseBothTheirsFirst, "Use both", "ConflictUseBothTheirsFirst"),
        };

        for (var block = 0; block < 2; block++)
        {
            var bar = panes.RowBounds(ConflictPaneSide.Ours, panes.BarRowOf(block))!.Value;
            foreach (var (side, choice, label, id) in expected)
            {
                var button = panes.BarButton(block, side, choice) ?? throw new InvalidOperationException($"No {id} on block {block}.");
                Assert.Equal(label, button.Content);
                Assert.Equal(id, Avalonia.Automation.AutomationProperties.GetAutomationId(button));
                Assert.Equal(label, Avalonia.Automation.AutomationProperties.GetName(button));
                Assert.Equal(22, button.Bounds.Height);
                var top = button.TranslatePoint(default, panes)!.Value;
                Assert.Equal(bar.Y + 2, top.Y, 0.5);
            }

            // The buttons start the bar: the first 6 from the text column's left, the second 4 after it.
            foreach (var (side, first, second) in new[]
            {
                (ConflictPaneSide.Ours, ConflictChoice.UseOurs, ConflictChoice.UseBothOursFirst),
                (ConflictPaneSide.Theirs, ConflictChoice.UseTheirs, ConflictChoice.UseBothTheirsFirst),
            })
            {
                var one = panes.BarButton(block, side, first)!;
                var two = panes.BarButton(block, side, second)!;
                var oneLeft = one.TranslatePoint(default, panes)!.Value.X;
                Assert.Equal(panes.TextLeft(side)!.Value + 6, oneLeft, 0.5);
                Assert.Equal(oneLeft + one.Bounds.Width + 4, two.TranslatePoint(default, panes)!.Value.X, 0.5);
            }

            // A pane has only its own side's buttons.
            Assert.Null(panes.BarButton(block, ConflictPaneSide.Ours, ConflictChoice.UseTheirs));
            Assert.Null(panes.BarButton(block, ConflictPaneSide.Theirs, ConflictChoice.UseBothOursFirst));
        }

        Assert.Null(panes.BarButton(2, ConflictPaneSide.Ours, ConflictChoice.UseOurs));

        foreach (var block in new[] { 1, 0 })
        {
            foreach (var (side, choice, _, _) in expected)
            {
                var button = panes.BarButton(block, side, choice)!;
                host.Click(button, new Point(button.Bounds.Width / 2, button.Bounds.Height / 2));
            }
        }

        Assert.Equal(
            [
                (1, ConflictChoice.UseOurs), (1, ConflictChoice.UseBothOursFirst), (1, ConflictChoice.UseTheirs), (1, ConflictChoice.UseBothTheirsFirst),
                (0, ConflictChoice.UseOurs), (0, ConflictChoice.UseBothOursFirst), (0, ConflictChoice.UseTheirs), (0, ConflictChoice.UseBothTheirsFirst),
            ],
            choices);
    }

    [AvaloniaFact]
    public void Only_bars_in_view_have_buttons_and_the_panes_scroll_together()
    {
        var common = Enumerable.Range(1, 60).Select(n => $"common line {n}").ToArray();
        var panes = new ConflictPanes
        {
            Parts =
            [
                ConflictPanesPart.Block(["ours 1"], ["theirs 1", "theirs 2"]),
                ConflictPanesPart.Common(common),
                ConflictPanesPart.Block(["ours 3", "ours 4", "ours 5"], ["theirs 3"]),
                ConflictPanesPart.Common("end"),
            ],
        };

        using var host = DiffHost.Show(panes, Width, 200, ThemeVariant.Dark);
        host.Capture();
        Assert.NotNull(panes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs));
        Assert.NotNull(panes.BarButton(0, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs));
        Assert.Null(panes.BarButton(1, ConflictPaneSide.Ours, ConflictChoice.UseOurs));
        Assert.Null(panes.BarButton(1, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs));

        // The wheel over the theirs pane scrolls both panes to the end.
        var lastBar = panes.BarRowOf(1);
        for (var i = 0; i < 20 && panes.RowBounds(ConflictPaneSide.Theirs, lastBar + 4) is null; i++)
        {
            host.Window.MouseWheel(host.ToWindow(panes, new Point(600, 100)), new Vector(0, -3));
            host.Capture();
        }

        var theirsBar = panes.RowBounds(ConflictPaneSide.Theirs, lastBar) ?? throw new InvalidOperationException("The second bar did not come into view.");
        Assert.Equal(theirsBar.Y, panes.RowBounds(ConflictPaneSide.Ours, lastBar)!.Value.Y);
        Assert.Null(panes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs));
        Assert.NotNull(panes.BarButton(1, ConflictPaneSide.Ours, ConflictChoice.UseBothOursFirst));
        Assert.NotNull(panes.BarButton(1, ConflictPaneSide.Theirs, ConflictChoice.UseBothTheirsFirst));
        TestPictures.Save(host, "conflict-panes-scrolled");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_resolved_block_s_bars_say_whether_each_side_is_used_and_its_rows_are_tinted_so(string themeName)
    {
        var colors = DiffColors.For(themeName);
        var used = themeName == "Light" ? Color.Parse("#E6F6EC") : Color.Parse("#1A2E24");
        var panes = new ConflictPanes { Parts = TwoBlocks() };
        using var host = DiffHost.Show(panes, Width, Height, colors.Variant);
        host.Capture();
        var scrolled = panes.ScrollOffset;

        panes.Bars = [new ConflictBlockBars(ConflictSideState.Used, ConflictSideState.NotUsed), new ConflictBlockBars(ConflictSideState.Used, ConflictSideState.Used)];
        var shot = host.Capture();
        TestPictures.Save(host, $"conflict-panes-used-{themeName.ToLowerInvariant()}");

        // The rows stay where they were; only the bars and backgrounds change.
        Assert.Equal(scrolled, panes.ScrollOffset);
        var ours = panes.RowsOf(ConflictPaneSide.Ours);
        var theirs = panes.RowsOf(ConflictPaneSide.Theirs);
        Assert.Equal((ConflictTint.Bar, ConflictSideState.Used, "Used in the result", 0), (ours[2].Tint, ours[2].BarState!.Value, ours[2].BarText, ours[2].Block!.Value));
        Assert.Equal((ConflictSideState.NotUsed, "Not used"), (theirs[2].BarState!.Value, theirs[2].BarText));
        Assert.Equal(ConflictTint.Used, ours[3].Tint);
        Assert.Equal(ConflictTint.Filler, ours[4].Tint);
        Assert.Equal([ConflictTint.None, ConflictTint.None, ConflictTint.None], theirs.Skip(3).Take(3).Select(row => row.Tint));
        Assert.Equal([ConflictTint.Used, ConflictTint.Used], ours.Skip(9).Take(2).Select(row => row.Tint));
        Assert.Equal(ConflictTint.Used, theirs[9].Tint);
        Assert.Null(ours[0].Block);
        Assert.Null(ours[3].BarState);

        Assert.Equal("Used in the result", panes.BarText(0, ConflictPaneSide.Ours));
        Assert.Equal("Not used", panes.BarText(0, ConflictPaneSide.Theirs));
        Assert.Null(panes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs));
        Assert.Null(panes.BarButton(1, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs));

        // The bar's text, 8 from the text column in the secondary colour, has its own id.
        var texts = panes.GetVisualDescendants().OfType<TextBlock>().Where(text => Avalonia.Automation.AutomationProperties.GetAutomationId(text) == "ConflictBarState").ToList();
        Assert.Equal(4, texts.Count);
        Assert.All(texts, text => DiffColors.AssertBrush(colors.TextSecondary, text.Foreground, "a bar's state"));
        var first = texts.Single(text => text.Text == "Used in the result" && text.TranslatePoint(default, panes)!.Value.X < Width / 2 && text.TranslatePoint(default, panes)!.Value.Y < 60);
        Assert.Equal(panes.TextLeft(ConflictPaneSide.Ours)!.Value + 8, first.TranslatePoint(default, panes)!.Value.X, 0.5);

        foreach (var (side, row, color, what) in new[]
        {
            (ConflictPaneSide.Ours, 3, used, "the first block's ours line, used"),
            (ConflictPaneSide.Theirs, 3, colors.Background, "the first block's theirs line, not used"),
            (ConflictPaneSide.Ours, 2, colors.Hunk, "the first bar"),
            (ConflictPaneSide.Theirs, 9, used, "the second block's theirs line, used"),
        })
        {
            var bounds = panes.RowBounds(side, row)!.Value;
            DiffColors.AssertColor(color, shot.PixelAt(host.ToWindow(panes, new Point(bounds.Right - 20, bounds.Center.Y))), what);
        }

        // Back to unresolved: the buttons and the sides' tints come back.
        panes.Bars = [ConflictBlockBars.Open, ConflictBlockBars.Open];
        host.Capture();
        Assert.NotNull(panes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs));
        Assert.Equal(ConflictTint.Ours, panes.RowsOf(ConflictPaneSide.Ours)[3].Tint);
    }

    [AvaloniaFact]
    public void RevealBlock_puts_a_block_s_bar_third_in_view_and_ScrollToOffset_keeps_a_place()
    {
        var common = Enumerable.Range(1, 40).Select(n => $"common line {n}").ToArray();
        var panes = new ConflictPanes
        {
            Parts =
            [
                ConflictPanesPart.Common("first"),
                ConflictPanesPart.Block(["ours 1"], ["theirs 1"]),
                ConflictPanesPart.Common(common),
                ConflictPanesPart.Block(["ours 2"], ["theirs 2"]),
                ConflictPanesPart.Common(common),
            ],
        };
        using var host = DiffHost.Show(panes, Width, 200, ThemeVariant.Dark);
        host.Capture();

        // Near the start there are fewer rows above: the first block's bar is row 1, nothing scrolls.
        panes.RevealBlock(0);
        host.Capture();
        Assert.Equal(0, panes.ScrollOffset.Y, 0.5);

        // The second block's bar (row 43) is the third row in view: rows 41 and 42 above it.
        panes.RevealBlock(1);
        host.Capture();
        var bar = panes.BarRowOf(1);
        Assert.Equal(43, bar);
        Assert.Equal(0, panes.RowBounds(ConflictPaneSide.Ours, bar - 2)!.Value.Y, 0.5);
        Assert.Equal(panes.RowBounds(ConflictPaneSide.Theirs, bar)!.Value.Y, panes.RowBounds(ConflictPaneSide.Ours, bar)!.Value.Y);
        Assert.NotNull(panes.BarButton(1, ConflictPaneSide.Ours, ConflictChoice.UseOurs));

        // New parts start at the top, unless a place to keep is asked for.
        var place = panes.ScrollOffset;
        panes.Parts = [.. panes.Parts!];
        panes.ScrollToOffset(place);
        host.Capture();
        Assert.Equal(place.Y, panes.ScrollOffset.Y, 0.5);
        panes.Parts = [.. panes.Parts!];
        host.Capture();
        Assert.Equal(0, panes.ScrollOffset.Y, 0.5);
    }

    [AvaloniaFact]
    public void The_parts_of_a_parsed_file_its_sides_a_selection_s_copy_and_the_tab_stops()
    {
        var text = VisualCommit.Core.Conflicts.ConflictText.Parse("a\n<<<<<<< HEAD\nx\n||||||| base\nb\n=======\ny\nz\n>>>>>>> t\nc\n");
        var parts = ConflictPanes.PartsOf(text);
        Assert.Equal(3, parts.Count);
        Assert.False(parts[0].IsBlock);
        Assert.Equal(["a"], parts[0].Ours);
        Assert.True(parts[1].IsBlock);
        Assert.Equal(["x"], parts[1].Ours);
        Assert.Equal(["y", "z"], parts[1].Theirs);
        Assert.Equal(["c"], parts[2].Theirs);

        var panes = new ConflictPanes { Parts = parts };
        using var host = DiffHost.Show(panes, Width, Height, ThemeVariant.Dark);
        host.Capture();
        Assert.Equal(ConflictPaneSide.Ours, panes.SideOf(panes.PaneEditor(ConflictPaneSide.Ours)));
        Assert.Equal(ConflictPaneSide.Theirs, panes.SideOf(panes.BarButton(0, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs)));
        Assert.Null(panes.SideOf(panes));
        Assert.False(panes.HasSelection(ConflictPaneSide.Ours));

        // A drag over the ours pane's text selects text, not rows.
        var row = panes.RowBounds(ConflictPaneSide.Ours, 0)!.Value;
        host.Drag(panes, new Point(panes.TextLeft(ConflictPaneSide.Ours)!.Value + 1, row.Center.Y), new Point(200, panes.RowBounds(ConflictPaneSide.Ours, 3)!.Value.Center.Y));
        Assert.True(panes.HasSelection(ConflictPaneSide.Ours));
        Assert.False(panes.HasSelection(ConflictPaneSide.Theirs));

        // The texts are no tab stops; a bar's button keeps the focus it is given.
        var button = panes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs)!;
        Assert.True(button.Focus(Avalonia.Input.NavigationMethod.Tab));
        Assert.True(button.IsFocused);
        host.PressKey(Avalonia.Input.Key.Tab);
        Assert.True(panes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseBothOursFirst)!.IsFocused);
        host.PressKey(Avalonia.Input.Key.Tab);
        Assert.True(panes.BarButton(0, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs)!.IsFocused);
    }

    [AvaloniaFact]
    public void UI_Automation_finds_the_panes_and_the_bar_buttons_by_their_ids_which_the_owner_can_set()
    {
        var panes = new ConflictPanes { Parts = TwoBlocks() };
        using var host = DiffHost.Show(panes, Width, Height, ThemeVariant.Dark);
        host.Capture();

        var ids = ControlIds(panes);
        Assert.Contains("ConflictOurs", ids);
        Assert.Contains("ConflictTheirs", ids);
        Assert.Equal(2, ids.Count(id => id == "ConflictUseOurs"));
        Assert.Equal(2, ids.Count(id => id == "ConflictUseBothOursFirst"));
        Assert.Equal(2, ids.Count(id => id == "ConflictUseTheirs"));
        Assert.Equal(2, ids.Count(id => id == "ConflictUseBothTheirsFirst"));

        panes.OursAutomationId = "LeftSide";
        panes.TheirsAutomationId = "RightSide";
        host.Capture();
        ids = ControlIds(panes);
        Assert.Contains("LeftSide", ids);
        Assert.Contains("RightSide", ids);
        Assert.DoesNotContain("ConflictOurs", ids);

        static List<string?> ControlIds(Control control)
        {
            var peers = new List<AutomationPeer>();
            Collect(ControlAutomationPeer.CreatePeerForElement(control), peers);
            return peers.Where(peer => peer.IsControlElement()).Select(peer => peer.GetAutomationId()).ToList();
        }

        static void Collect(AutomationPeer peer, List<AutomationPeer> into)
        {
            into.Add(peer);
            foreach (var child in peer.GetChildren())
            {
                Collect(child, into);
            }
        }
    }
}
