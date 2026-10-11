using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Diff;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Settings;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Headless tests of the diff view's text (<see cref="DiffTextView"/>), drawn with Skia and checked
/// against phase 2's test report ("The diff view": Text, Inline, Side by side, Hunk headers, Syntax
/// highlighting, Word-level highlights, Selecting lines). Inline at the diff body's size at
/// 1100×700 (440×514), side by side at its size at 1400×900 (740×700). Backgrounds are sampled
/// where no text is; text colours are read from the brushes the text is drawn with.
/// </summary>
public class DiffTextViewTests
{
    private const double InlineWidth = 440;
    private const double InlineHeight = 514;
    private const double SideWidth = 740;
    private const double SideHeight = 700;

    // Lines of the Calculator diff (hunk, line in hunk).
    private static readonly DiffLineRef AddLine = new(0, 1);
    private static readonly DiffLineRef OpenBrace = new(0, 2);
    private static readonly DiffLineRef Removed15 = new(0, 3);
    private static readonly DiffLineRef Added15 = new(0, 4);
    private static readonly DiffLineRef Context31 = new(1, 2);
    private static readonly DiffLineRef Added32 = new(1, 3);
    private static readonly DiffLineRef Added33 = new(1, 4);
    private static readonly DiffLineRef Added34 = new(1, 5);
    private static readonly DiffLineRef Added35 = new(1, 6);

    public static TheoryData<string> Themes => ["Dark", "Light"];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task Inline_rows_have_their_backgrounds_gutter_included(string themeName)
    {
        var colors = DiffColors.For(themeName);
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, colors.Variant);
        var shot = host.Capture();

        // Rows: 0 header, 1 context 12/12, 4 removed 15, 5 added 15, 13 added 32; the gutter
        // (2 from the left) and the far end of the text column (where no text reaches).
        foreach (var (row, color, what) in new[]
        {
            (0, colors.Hunk, "the first hunk header"),
            (1, colors.Background, "context 12/12"),
            (4, colors.Removed, "removed 15"),
            (5, colors.Added, "added 15"),
            (13, colors.Added, "added 32"),
            (17, colors.Background, "context 32/36"),
        })
        {
            var bounds = view.RowBounds(row) ?? throw new InvalidOperationException($"Row {row} is not in view.");
            DiffColors.AssertColor(color, shot.PixelAt(host.ToWindow(view, new Point(2, bounds.Center.Y))), $"{what}, in the gutter");
            if (row != 0)
            {
                DiffColors.AssertColor(color, shot.PixelAt(host.ToWindow(view, new Point(InlineWidth - 30, bounds.Center.Y))), $"{what}, right of the text");
            }
        }

        // Below the last row: the window background.
        var last = view.RowBounds(view.Rows.Count - 1)!.Value;
        DiffColors.AssertColor(colors.Background, shot.PixelAt(host.ToWindow(view, new Point(200, last.Bottom + 20))), "below the rows");
    }

    [AvaloniaFact]
    public async Task Rows_are_15_84_high_and_header_rows_26_and_the_gutter_has_two_number_columns_and_a_sign_column()
    {
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();

        Assert.Equal(26, view.RowBounds(0)!.Value.Height);
        Assert.Equal(26, view.RowBounds(9)!.Value.Height);

        // Rows 1 to 8 between the two headers: 8 × 15.84, each drawn on whole pixels.
        Assert.Equal(26 + (8 * 15.84), view.RowBounds(9)!.Value.Y, 0.5);
        Assert.InRange(view.RowBounds(1)!.Value.Height, 15, 16);

        // The gutter: two number columns of 8 + 7.2 × 2 + 8 (the largest numbers, 32 and 36,
        // have two digits) and the sign column of 16: the text starts at 76.8 (77: layout
        // rounding puts the text view on a whole pixel).
        var text = view.TextRangeBounds(AddLine, 4, 6)!.Value;
        Assert.Equal(76.8 + (4 * 7.2), text.X, 1.0);
        Assert.Equal(6 * 7.2, text.Width, 1.0);

        // The number cells' centres: the old column at 15.2, the new at 45.6.
        Assert.Equal(15.2, view.LineNumberPoint(Removed15)!.Value.X, 1.0);
        Assert.Equal(45.6, view.LineNumberPoint(Added15)!.Value.X, 1.0);
        Assert.Equal(45.6, view.LineNumberPoint(AddLine)!.Value.X, 1.0);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task The_changed_words_of_added_line_15_have_the_stronger_background(string themeName)
    {
        var colors = DiffColors.For(themeName);
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, colors.Variant);
        var shot = host.Capture();

        // "        return a + b + _offset;": " + _offset" (from column 20) is highlighted, "return" is not.
        // Sampled 1.5 below the row's top, above every glyph.
        var row = view.RowBounds(5)!.Value;
        var offset = view.TextRangeBounds(Added15, 23, 7)!.Value;
        var plusBefore = view.TextRangeBounds(Added15, 20, 1)!.Value;
        var @return = view.TextRangeBounds(Added15, 8, 6)!.Value;
        var y = row.Y + 1.5;
        DiffColors.AssertColor(colors.AddedWord, shot.PixelAt(host.ToWindow(view, new Point(offset.Center.X, y))), "behind _offset");
        DiffColors.AssertColor(colors.AddedWord, shot.PixelAt(host.ToWindow(view, new Point(plusBefore.Center.X, y))), "behind the space before + _offset");
        DiffColors.AssertColor(colors.Added, shot.PixelAt(host.ToWindow(view, new Point(@return.Center.X, y))), "behind return");

        // The removed line 15 has nothing highlighted.
        var removed = view.RowBounds(4)!.Value;
        var removedText = view.TextRangeBounds(Removed15, 8, 13)!.Value;
        for (var x = removedText.X + 2; x < removedText.Right; x += 7.2)
        {
            DiffColors.AssertColor(colors.Removed, shot.PixelAt(host.ToWindow(view, new Point(x, removed.Y + 1.5))), $"removed 15 at {x}");
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task Text_is_drawn_with_the_syntax_colours_and_without_a_colour_in_the_main_text_colour(string themeName)
    {
        var colors = DiffColors.For(themeName);
        var dark = themeName == "Dark";
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, colors.Variant);
        host.Capture();

        DiffColors.AssertBrush(Color.Parse(dark ? "#C586C0" : "#AF00DB"), view.ForegroundAt(Added15, 8), "return");
        DiffColors.AssertBrush(Color.Parse(dark ? "#569CD6" : "#0000FF"), view.ForegroundAt(AddLine, 4), "public");
        DiffColors.AssertBrush(Color.Parse(dark ? "#DCDCAA" : "#795E26"), view.ForegroundAt(AddLine, 15), "Add");
        DiffColors.AssertBrush(Color.Parse(dark ? "#9CDCFE" : "#001080"), view.ForegroundAt(Added15, 23), "_offset");
        DiffColors.AssertBrush(colors.TextPrimary, view.ForegroundAt(OpenBrace, 4), "{");
        DiffColors.AssertBrush(colors.TextPrimary, view.ForegroundAt(new DiffLineRef(1, 7), 0), "the last }");
        Assert.Equal(FontWeight.Normal, view.FontWeightAt(AddLine, 4));
    }

    [AvaloniaFact]
    public async Task Markdown_headings_are_bold_and_a_file_without_a_grammar_is_all_in_the_main_text_colour()
    {
        var diffs = await DiffScenario.DiffsAsync();
        var view = new DiffTextView { Diff = diffs.ReadmeUnstaged, FilePath = "README.md" };
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();

        DiffColors.AssertBrush(Color.Parse("#569CD6"), view.ForegroundAt(new DiffLineRef(0, 0), 2), "# Calculator");
        Assert.Equal(FontWeight.Bold, view.FontWeightAt(new DiffLineRef(0, 0), 2));
        Assert.Equal(FontWeight.Bold, view.FontWeightAt(new DiffLineRef(0, 5), 3));
        DiffColors.AssertBrush(DiffColors.Dark.TextPrimary, view.ForegroundAt(new DiffLineRef(0, 3), 2), "prose");

        view.Diff = diffs.OldNotes;
        view.FilePath = "docs/old-notes.txt";
        host.Capture();
        DiffColors.AssertBrush(DiffColors.Dark.TextPrimary, view.ForegroundAt(new DiffLineRef(0, 0), 0), "a .txt file");
    }

    [AvaloniaFact]
    public async Task Switching_the_theme_recolours_the_rows_and_the_syntax()
    {
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();
        DiffColors.AssertBrush(Color.Parse("#569CD6"), view.ForegroundAt(AddLine, 4), "public, dark");

        host.Window.RequestedThemeVariant = ThemeVariant.Light;
        var shot = host.Capture();

        DiffColors.AssertBrush(Color.Parse("#0000FF"), view.ForegroundAt(AddLine, 4), "public, light");
        DiffColors.AssertBrush(DiffColors.Light.TextPrimary, view.ForegroundAt(OpenBrace, 4), "{, light");
        var added = view.RowBounds(5)!.Value;
        DiffColors.AssertColor(DiffColors.Light.Added, shot.PixelAt(host.ToWindow(view, new Point(2, added.Center.Y))), "added 15, light");
        DiffColors.AssertColor(DiffColors.Light.Hunk, shot.PixelAt(host.ToWindow(view, new Point(2, view.RowBounds(0)!.Value.Center.Y))), "the header, light");
    }

    [AvaloniaFact]
    public async Task Hunk_headers_have_their_buttons_in_hunk_order_and_a_click_asks_for_the_action()
    {
        var view = await CalculatorAsync();
        var asked = new List<(int Hunk, HunkAction Action)>();
        view.HunkActionRequested += (_, e) => asked.Add((e.Hunk, e.Action));
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();

        var buttons = Buttons(view);
        Assert.Equal(
            ["StageHunkButton", "DiscardHunkButton", "StageHunkButton", "DiscardHunkButton"],
            buttons.Select(AutomationProperties.GetAutomationId));
        Assert.Equal(["Stage hunk", "Discard hunk", "Stage hunk", "Discard hunk"], buttons.Select(AutomationProperties.GetName));

        foreach (var button in buttons)
        {
            host.Click(button, new Point(button.Bounds.Width / 2, button.Bounds.Height / 2));
        }

        Assert.Equal([(0, HunkAction.Stage), (0, HunkAction.Discard), (1, HunkAction.Stage), (1, HunkAction.Discard)], asked);

        // A click on a header selects nothing.
        Assert.Empty(view.SelectedChanges);
    }

    [AvaloniaFact]
    public async Task Hunk_buttons_are_22_high_4_apart_6_from_the_right_edge_and_the_header_text_ends_8_before_them()
    {
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();

        var header = view.RowBounds(0)!.Value;
        var buttons = Buttons(view).Take(2).Select(button => new Rect(host.ToWindow(button, default), button.Bounds.Size)).ToList();
        var origin = host.ToWindow(view, default);
        var stage = buttons[0].Translate(-(Vector)origin);
        var discard = buttons[1].Translate(-(Vector)origin);
        Assert.Equal(22, stage.Height, 0.5);
        Assert.Equal(header.Center.Y, stage.Center.Y, 1.0);
        Assert.Equal(4, discard.X - stage.Right, 0.5);
        Assert.Equal(InlineWidth - 6, discard.Right, 1.0);

        // The header text: the code font from the start of the text column, cut with "…" 8 before the first button.
        var text = view.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text?.StartsWith("@@ -12,7", StringComparison.Ordinal) == true);
        var textBounds = new Rect(host.ToWindow(text, default), text.Bounds.Size).Translate(-(Vector)origin);
        Assert.Equal(76.8, textBounds.X, 1.0);
        Assert.Equal(stage.X - 8, textBounds.Right, 1.0);
        Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);
        Assert.Equal("@@ -12,7 +12,7 @@ public sealed class Calculator", ToolTip.GetTip(text));
    }

    [AvaloniaFact]
    public async Task A_staged_diff_has_unstage_buttons_and_a_commit_diff_none()
    {
        var view = await CalculatorAsync();
        view.HunkActions = HunkActions.Unstage;
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();
        Assert.Equal(["UnstageHunkButton", "UnstageHunkButton"], Buttons(view).Select(AutomationProperties.GetAutomationId));

        var asked = new List<(int, HunkAction)>();
        view.HunkActionRequested += (_, e) => asked.Add((e.Hunk, e.Action));
        var second = Buttons(view)[1];
        host.Click(second, new Point(second.Bounds.Width / 2, second.Bounds.Height / 2));
        Assert.Equal([(1, HunkAction.Unstage)], asked);

        view.HunkActions = HunkActions.None;
        host.Capture();
        Assert.Empty(Buttons(view));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task A_click_on_a_line_number_selects_its_row_and_shift_extends_the_selection(string themeName)
    {
        var colors = DiffColors.For(themeName);
        var view = await CalculatorAsync();
        var raised = 0;
        view.PropertyChanged += (_, e) => raised += e.Property == DiffTextView.SelectedChangesProperty ? 1 : 0;
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, colors.Variant);
        host.Capture();
        Assert.Empty(view.SelectedChanges);

        // Line number 35, the added "    public int Square(int a) => a * a;".
        host.Click(view, view.LineNumberPoint(Added35)!.Value);
        Assert.Equal([Added35], view.SelectedChanges);
        Assert.Equal(1, raised);

        // Its row has the selection background behind the text, and keeps its own in the gutter.
        var shot = host.Capture();
        var row = view.RowBounds(16)!.Value;
        DiffColors.AssertColor(colors.Selection, shot.PixelAt(host.ToWindow(view, new Point(InlineWidth - 30, row.Center.Y))), "the selected row");
        DiffColors.AssertColor(colors.Added, shot.PixelAt(host.ToWindow(view, new Point(2, row.Center.Y))), "the selected row's gutter");
        DiffColors.AssertColor(colors.Added, shot.PixelAt(host.ToWindow(view, new Point(InlineWidth - 30, view.RowBounds(15)!.Value.Center.Y))), "the row above");

        // Shift with a click on number 33 selects 33 to 35.
        host.Click(view, view.LineNumberPoint(Added33)!.Value, RawInputModifiers.Shift);
        Assert.Equal(new HashSet<DiffLineRef> { Added33, Added34, Added35 }, view.SelectedChanges.ToHashSet());

        // On to 31, a context line: context lines never count.
        host.Click(view, view.LineNumberPoint(Context31)!.Value, RawInputModifiers.Shift);
        Assert.Equal(new HashSet<DiffLineRef> { Added32, Added33, Added34, Added35 }, view.SelectedChanges.ToHashSet());

        // A click on a header row's number column selects nothing new.
        host.Click(view, new Point(15, view.RowBounds(9)!.Value.Center.Y));
        Assert.Equal(4, view.SelectedChanges.Count);

        // A click without Shift starts again; ClearSelection empties it.
        host.Click(view, view.LineNumberPoint(Removed15)!.Value);
        Assert.Equal([Removed15], view.SelectedChanges);
        view.ClearSelection();
        Assert.Empty(view.SelectedChanges);
        var cleared = host.Capture();
        DiffColors.AssertColor(colors.Removed, cleared.PixelAt(host.ToWindow(view, new Point(InlineWidth - 30, view.RowBounds(4)!.Value.Center.Y))), "the row after ClearSelection");
    }

    [AvaloniaFact]
    public async Task Dragging_over_the_text_selects_every_row_it_touches()
    {
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();

        // From the middle of removed 15 to the middle of context 16/16.
        var from = view.TextRangeBounds(Removed15, 12, 1)!.Value.Center;
        var to = view.TextRangeBounds(new DiffLineRef(0, 5), 4, 1)!.Value.Center;
        host.Drag(view, from, to);

        Assert.Equal(new HashSet<DiffLineRef> { Removed15, Added15 }, view.SelectedChanges.ToHashSet());

        // A plain click in the text selects nothing.
        host.Click(view, view.TextRangeBounds(AddLine, 6, 1)!.Value.Center);
        Assert.Empty(view.SelectedChanges);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public async Task Side_by_side_faces_removed_15_with_added_15_and_fills_the_empty_side(string themeName)
    {
        var colors = DiffColors.For(themeName);
        var view = await CalculatorAsync(DiffMode.SideBySide);
        using var host = DiffHost.Show(view, SideWidth, SideHeight, colors.Variant);
        var shot = host.Capture();

        // The removed line 15 on the left facing the added line 15 on the right, in one row.
        var left = view.LineNumberPoint(Removed15)!.Value;
        var right = view.LineNumberPoint(Added15)!.Value;
        Assert.Equal(left.Y, right.Y);
        Assert.True(left.X < SideWidth / 2 && right.X > SideWidth / 2, $"The numbers are at {left.X} and {right.X}.");
        Assert.Equal(view.TextRangeBounds(Removed15, 8, 6)!.Value.Y, view.TextRangeBounds(Added15, 8, 6)!.Value.Y);
        var row4 = view.RowBounds(4)!.Value;
        DiffColors.AssertColor(colors.Removed, shot.PixelAt(host.ToWindow(view, new Point(2, row4.Center.Y))), "removed 15's gutter");
        DiffColors.AssertColor(colors.Removed, shot.PixelAt(host.ToWindow(view, new Point((SideWidth / 2) - 20, row4.Center.Y))), "removed 15's text column");
        DiffColors.AssertColor(colors.Added, shot.PixelAt(host.ToWindow(view, new Point((SideWidth / 2) + 4, row4.Center.Y))), "added 15's gutter");
        DiffColors.AssertColor(colors.Added, shot.PixelAt(host.ToWindow(view, new Point(SideWidth - 30, row4.Center.Y))), "added 15's text column");

        // Rows 12 to 15: fillers on the left, added 32 to 35 on the right.
        for (var row = 12; row <= 15; row++)
        {
            var bounds = view.RowBounds(row)!.Value;
            DiffColors.AssertColor(colors.Filler, shot.PixelAt(host.ToWindow(view, new Point(2, bounds.Center.Y))), $"row {row}, left gutter");
            DiffColors.AssertColor(colors.Filler, shot.PixelAt(host.ToWindow(view, new Point(200, bounds.Center.Y))), $"row {row}, left text");
            DiffColors.AssertColor(colors.Added, shot.PixelAt(host.ToWindow(view, new Point(SideWidth - 30, bounds.Center.Y))), $"row {row}, right");
        }

        // The two halves are equal, with a 1-pixel line between them: 369.5 + 1 + 369.5, which
        // layout rounding puts on whole pixels one way or the other.
        var middle = (SideWidth - 1) / 2;
        var line = new[] { Math.Floor(middle), Math.Ceiling(middle) }
            .Select(x => shot.PixelAt(host.ToWindow(view, new Point(x, SideHeight - 10))))
            .ToList();
        Assert.Contains(line, pixel => Math.Abs(pixel.R - colors.Border.R) <= 3 && Math.Abs(pixel.G - colors.Border.G) <= 3 && Math.Abs(pixel.B - colors.Border.B) <= 3);
        Assert.Equal(new DiffLineRef(1, 7), view.Rows[16].Left!.Line);
        Assert.Equal(view.LineNumberPoint(new DiffLineRef(1, 7))!.Value.Y, view.RowBounds(16)!.Value.Center.Y, 1.0);

        // The header: its text on the left half, its buttons at the right end of the right half.
        var buttons = Buttons(view);
        Assert.Equal(4, buttons.Count);
        var discard = new Rect(host.ToWindow(buttons[1], default), buttons[1].Bounds.Size);
        Assert.Equal(host.ToWindow(view, new Point(SideWidth - 6, 0)).X, discard.Right, 1.0);
        var text = view.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text?.StartsWith("@@ -12,7", StringComparison.Ordinal) == true);
        Assert.True(host.ToWindow(text, new Point(text.Bounds.Width, 0)).X <= host.ToWindow(view, new Point(SideWidth / 2, 0)).X);
    }

    [AvaloniaFact]
    public async Task Side_by_side_a_selection_on_the_left_counts_removed_lines_and_one_on_the_right_added_lines()
    {
        var view = await CalculatorAsync(DiffMode.SideBySide);
        using var host = DiffHost.Show(view, SideWidth, SideHeight, ThemeVariant.Dark);
        host.Capture();

        // On the left, from 14 to 16 (context lines: LineNumberPoint gives their right side, so
        // the left numbers are clicked at the left column's x): only the removed 15 counts.
        var leftNumbers = view.LineNumberPoint(Removed15)!.Value.X;
        host.Click(view, new Point(leftNumbers, view.RowBounds(3)!.Value.Center.Y));
        host.Click(view, new Point(leftNumbers, view.RowBounds(5)!.Value.Center.Y), RawInputModifiers.Shift);
        Assert.Equal([Removed15], view.SelectedChanges);

        // On the right: the left's selection goes, the added lines count.
        host.Click(view, view.LineNumberPoint(Added15)!.Value);
        Assert.Equal([Added15], view.SelectedChanges);
        host.Click(view, view.LineNumberPoint(Added35)!.Value);
        host.Click(view, view.LineNumberPoint(Added32)!.Value, RawInputModifiers.Shift);
        Assert.Equal(new HashSet<DiffLineRef> { Added32, Added33, Added34, Added35 }, view.SelectedChanges.ToHashSet());

        // Back on the left: the right's selection goes.
        host.Click(view, view.LineNumberPoint(Removed15)!.Value);
        Assert.Equal([Removed15], view.SelectedChanges);
        var shot = host.Capture();
        var row = view.RowBounds(4)!.Value;
        DiffColors.AssertColor(DiffColors.Dark.Selection, shot.PixelAt(host.ToWindow(view, new Point((SideWidth / 2) - 20, row.Center.Y))), "the left's selected row");
        DiffColors.AssertColor(DiffColors.Dark.Added, shot.PixelAt(host.ToWindow(view, new Point(SideWidth - 30, row.Center.Y))), "the right's row, not selected");
    }

    [AvaloniaFact]
    public async Task Side_by_side_the_halves_scroll_together()
    {
        var diffs = await DiffScenario.DiffsAsync();
        var view = new DiffTextView { Diff = diffs.Large, FilePath = "data/large.txt", Highlighting = false, Mode = DiffMode.SideBySide };
        using var host = DiffHost.Show(view, SideWidth, SideHeight, ThemeVariant.Dark);
        host.Capture();

        // The wheel over the left half scrolls both.
        var position = host.ToWindow(view, new Point(200, 300));
        host.Window.MouseMove(position);
        host.Window.MouseWheel(position, new Vector(0, -10));
        DiffHost.Settle();
        host.Capture();
        Assert.Null(view.RowBounds(1));

        // A row in view: the old line on the left and the new line on the right at the same height.
        var row = FirstRowInView(view) + 5;
        var leftY = view.TextRangeBounds(view.Rows[row].Left!.Line, 0, 4)?.Y;
        var rightY = view.TextRangeBounds(view.Rows[row].Right!.Line, 0, 3)?.Y;
        Assert.NotNull(leftY);
        Assert.Equal(leftY, rightY);
        Assert.Equal(view.RowBounds(row)!.Value.Y, leftY!.Value, 1.0);
    }

    [AvaloniaFact]
    public void Scrolling_sideways_moves_the_text_but_not_the_gutter_or_the_hunk_header()
    {
        var diff = LongLines();
        var view = new DiffTextView { Diff = diff, FilePath = "notes.txt", HunkActions = HunkActions.StageAndDiscard };
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();
        var removed = new DiffLineRef(0, 1);
        var textBefore = view.TextRangeBounds(removed, 0, 1)!.Value.X;
        var numberBefore = view.LineNumberPoint(removed)!.Value;
        var buttonBefore = Edges(host, view, Buttons(view)[1]);
        var headerBefore = Edges(host, view, HeaderText(view, "@@ -1,3 +1,3 @@"));

        host.Window.MouseWheel(host.ToWindow(view, new Point(200, 100)), new Vector(-5, 0));
        host.Capture();

        var textAfter = view.TextRangeBounds(removed, 0, 1)?.X;
        Assert.True(textAfter < textBefore - 20, $"The text did not scroll sideways: from {textBefore} to {textAfter}.");
        Assert.Equal(numberBefore, view.LineNumberPoint(removed));
        Assert.Equal(buttonBefore, Edges(host, view, Buttons(view)[1]));
        Assert.Equal(headerBefore, Edges(host, view, HeaderText(view, "@@ -1,3 +1,3 @@")));
    }

    [AvaloniaFact]
    public void Side_by_side_both_halves_scroll_sideways_together()
    {
        var view = new DiffTextView { Diff = LongLines(), FilePath = "notes.txt", Mode = DiffMode.SideBySide };
        using var host = DiffHost.Show(view, SideWidth, SideHeight, ThemeVariant.Dark);
        host.Capture();
        var removed = new DiffLineRef(0, 1);
        var added = new DiffLineRef(0, 2);
        var leftBefore = view.TextRangeBounds(removed, 0, 1)!.Value.X;
        var rightBefore = view.TextRangeBounds(added, 0, 1)!.Value.X;

        host.Window.MouseWheel(host.ToWindow(view, new Point(200, 100)), new Vector(-5, 0));
        host.Capture();

        var leftMoved = leftBefore - view.TextRangeBounds(removed, 0, 1)!.Value.X;
        var rightMoved = rightBefore - view.TextRangeBounds(added, 0, 1)!.Value.X;
        Assert.True(leftMoved > 20, $"The left half moved {leftMoved}.");
        Assert.Equal(leftMoved, rightMoved, 0.5);
    }

    [AvaloniaFact]
    public async Task Ctrl_End_scrolls_to_the_end_of_the_large_diff_shown_without_highlighting()
    {
        var diffs = await DiffScenario.DiffsAsync();
        var view = new DiffTextView { Diff = diffs.Large, FilePath = "data/large.txt", Highlighting = false, HunkActions = HunkActions.StageAndDiscard };
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        view.FocusBody();
        var shot = host.Capture();

        // The first row under the header: removed old 1 "Line 00001 of the large file.", in the
        // main text colour on the plain removed background (no word-level highlights).
        Assert.Equal(60_001, view.Rows.Count);
        var first = new DiffLineRef(0, 0);
        DiffColors.AssertBrush(DiffColors.Dark.TextPrimary, view.ForegroundAt(first, 0), "Line");
        var word = view.TextRangeBounds(first, 0, 4)!.Value;
        DiffColors.AssertColor(DiffColors.Dark.Removed, shot.PixelAt(host.ToWindow(view, new Point(word.Center.X, word.Y + 1.5))), "behind Line");

        host.PressKey(Key.End, RawInputModifiers.Control);
        host.Capture();

        // The last row, added 30000 "Row 30000 of the large file.", has its bottom at the view's bottom.
        var last = view.RowBounds(view.Rows.Count - 1);
        Assert.NotNull(last);
        Assert.Equal(InlineHeight, last.Value.Bottom, 2.0);
        Assert.Null(view.RowBounds(1));
        Assert.Equal("Row 30000 of the large file.", view.Rows[^1].Left!.Text);
    }

    [AvaloniaFact]
    public async Task The_view_leaves_Escape_to_its_parent()
    {
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        bool? handled = null;
        host.Window.AddHandler(InputElement.KeyDownEvent, (_, e) => handled = e.Key == Key.Escape ? e.Handled : handled, RoutingStrategies.Bubble, handledEventsToo: true);
        view.FocusBody();
        host.Capture();
        Assert.True(view.IsKeyboardFocusWithin);

        host.Click(view, view.LineNumberPoint(Added35)!.Value);
        host.PressKey(Key.Escape);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public async Task A_new_diff_of_the_same_file_keeps_the_scroll_position_and_a_new_file_starts_at_the_top()
    {
        var diffs = await DiffScenario.DiffsAsync();
        var view = new DiffTextView { Diff = diffs.Large, FilePath = "data/large.txt", Highlighting = false };
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();
        var position = host.ToWindow(view, new Point(200, 300));
        host.Window.MouseWheel(position, new Vector(0, -20));
        host.Capture();
        var top = FirstRowInView(view);
        Assert.True(top > 10, $"The view did not scroll: row {top} is at the top.");

        // The same file's diff again, a new instance: the same rows stay in view.
        view.Diff = diffs.Large with { };
        host.Capture();
        Assert.Equal(top, FirstRowInView(view));

        // Another file, as long: the top.
        view.FilePath = "data/other.txt";
        view.Diff = diffs.Large with { };
        host.Capture();
        Assert.Equal(0, FirstRowInView(view));
    }

    [AvaloniaFact]
    public async Task Turning_highlighting_off_leaves_the_text_in_the_main_colour_without_word_highlights()
    {
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();
        DiffColors.AssertBrush(Color.Parse("#569CD6"), view.ForegroundAt(AddLine, 4), "public, highlighted");

        view.Highlighting = false;
        var shot = host.Capture();

        DiffColors.AssertBrush(DiffColors.Dark.TextPrimary, view.ForegroundAt(AddLine, 4), "public, not highlighted");
        var offset = view.TextRangeBounds(Added15, 23, 7)!.Value;
        DiffColors.AssertColor(DiffColors.Dark.Added, shot.PixelAt(host.ToWindow(view, new Point(offset.Center.X, view.RowBounds(5)!.Value.Y + 1.5))), "behind _offset");
    }

    [AvaloniaFact]
    public async Task A_diff_of_more_than_a_thousand_lines_is_coloured_once_it_is_tokenized_in_the_background()
    {
        var lines = Enumerable.Range(1, 1_500).Select(n => $"+    public int Value{n}() => {n};").ToList();
        var diff = DiffScenario.Parse(["@@ -0,0 +1,1500 @@", .. lines]);
        var view = new DiffTextView { Diff = diff, FilePath = "src/Values.cs" };
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);

        var keyword = Color.Parse("#569CD6");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while ((view.ForegroundAt(new DiffLineRef(0, 0), 4) as ISolidColorBrush)?.Color != keyword && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
            DiffHost.Settle();
        }

        DiffColors.AssertBrush(keyword, view.ForegroundAt(new DiffLineRef(0, 0), 4), "public");
        DiffColors.AssertBrush(Color.Parse("#DCDCAA"), view.ForegroundAt(new DiffLineRef(0, 1_499), 15), "Value1500");
    }

    [AvaloniaFact]
    public async Task Lines_that_end_in_CRLF_show_nothing_after_their_text()
    {
        var diffs = await DiffScenario.DiffsAsync();
        var view = new DiffTextView { Diff = diffs.CrlfNotes, FilePath = "notes.txt", HunkActions = HunkActions.StageAndDiscard };
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();

        Assert.Equal(["@@ -1,5 +1,5 @@", "@@ -15,6 +15,6 @@ Note 14"], view.Rows.Where(row => row.Kind == DiffRowKind.HunkHeader).Select(row => row.HeaderText));
        Assert.All(view.Rows.Where(row => row.Left is not null), row => Assert.DoesNotContain('\r', row.Left!.Text));

        // "Note 2, changed": every character is drawn, and nothing after the last.
        var changed = new DiffLineRef(0, 2);
        Assert.Equal("Note 2, changed", diffs.CrlfNotes.Hunks[0].Lines[2].Text);
        Assert.NotNull(view.ForegroundAt(changed, "Note 2, changed".Length - 1));
        Assert.Null(view.ForegroundAt(changed, "Note 2, changed".Length));
        Assert.Equal("Note 2, changed".Length * 7.2, view.TextRangeBounds(changed, 0, "Note 2, changed".Length)!.Value.Width, 1.0);
    }

    [AvaloniaFact]
    public async Task No_diff_shows_no_rows()
    {
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();
        host.Click(view, view.LineNumberPoint(Added35)!.Value);
        Assert.Single(view.SelectedChanges);

        view.Diff = null;
        var shot = host.Capture();

        Assert.Empty(view.Rows);
        Assert.Null(view.RowBounds(0));
        Assert.Null(view.LineNumberPoint(Added35));
        Assert.Empty(view.SelectedChanges);
        Assert.Empty(Buttons(view));
        DiffColors.AssertColor(DiffColors.Dark.Background, shot.PixelAt(host.ToWindow(view, new Point(2, 10))), "the empty view");
    }

    [AvaloniaFact]
    public async Task Focus_asked_for_before_the_view_is_shown_is_given_once_it_is()
    {
        var view = await CalculatorAsync();
        view.FocusBody();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();

        Assert.True(view.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public async Task UI_Automation_finds_the_text_and_the_hunk_buttons_by_their_ids()
    {
        var view = await CalculatorAsync(DiffMode.SideBySide);
        using var host = DiffHost.Show(view, SideWidth, SideHeight, ThemeVariant.Dark);
        host.Capture();

        var peers = new List<AutomationPeer>();
        Collect(ControlAutomationPeer.CreatePeerForElement(view), peers);
        var ids = peers.Where(peer => peer.IsControlElement()).Select(peer => peer.GetAutomationId()).ToList();

        Assert.Equal("DiffTextView", ids[0]);
        Assert.Contains("DiffTextLeft", ids);
        Assert.Contains("DiffTextRight", ids);
        Assert.Equal(2, ids.Count(id => id == "StageHunkButton"));
        Assert.Equal(2, ids.Count(id => id == "DiscardHunkButton"));
        Assert.Contains(peers, peer => peer.GetAutomationId() == "StageHunkButton" && peer.GetName() == "Stage hunk");

        static void Collect(AutomationPeer peer, List<AutomationPeer> into)
        {
            into.Add(peer);
            foreach (var child in peer.GetChildren())
            {
                Collect(child, into);
            }
        }
    }

    [AvaloniaFact]
    public async Task Changing_the_mode_keeps_the_line_at_the_top_in_view()
    {
        var diffs = await DiffScenario.DiffsAsync();
        var view = new DiffTextView { Diff = diffs.Large, FilePath = "data/large.txt", Highlighting = false };
        using var host = DiffHost.Show(view, SideWidth, SideHeight, ThemeVariant.Dark);
        host.Capture();
        host.Window.MouseWheel(host.ToWindow(view, new Point(200, 300)), new Vector(0, -20));
        host.Capture();
        var row = FirstRowInView(view);
        var line = view.Rows[row].Left!.Line;

        view.Mode = DiffMode.SideBySide;
        host.Capture();
        Assert.Equal(line, view.Rows[FirstRowInView(view)].Left?.Line);
        Assert.Equal("DiffTextLeft", AutomationProperties.GetAutomationId(Editors(view)[0]));
        Assert.Equal("DiffTextRight", AutomationProperties.GetAutomationId(Editors(view)[1]));

        view.Mode = DiffMode.Inline;
        host.Capture();
        Assert.Equal(line, view.Rows[FirstRowInView(view)].Left?.Line);
        Assert.Equal("DiffText", AutomationProperties.GetAutomationId(Editors(view)[0]));
    }

    [AvaloniaFact]
    public async Task The_view_and_its_text_have_their_automation_ids()
    {
        var view = await CalculatorAsync();
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();

        Assert.Equal("DiffTextView", AutomationProperties.GetAutomationId(view));
        var editors = Editors(view);
        Assert.Equal("DiffText", AutomationProperties.GetAutomationId(editors[0]));
        Assert.False(editors[1].IsVisible);

        view.FocusBody();
        Assert.True(view.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public async Task A_60000_line_document_without_highlighting_is_built_and_drawn_in_well_under_a_second()
    {
        var diffs = await DiffScenario.DiffsAsync();
        var view = new DiffTextView { FilePath = "data/large.txt", Highlighting = false };
        using var host = DiffHost.Show(view, InlineWidth, InlineHeight, ThemeVariant.Dark);
        host.Capture();

        var watch = Stopwatch.StartNew();
        view.Diff = diffs.Large;
        host.Capture();
        watch.Stop();

        Assert.Equal(60_001, view.Rows.Count);
        TestContext.Current.SendDiagnosticMessage($"The 60,000-line diff was built and drawn in {watch.ElapsedMilliseconds} ms.");

        // Generous: the point is to catch work that grows faster than the number of lines.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"Building and drawing took {watch.ElapsedMilliseconds} ms.");
    }

    private static async Task<DiffTextView> CalculatorAsync(DiffMode mode = DiffMode.Inline)
    {
        var diffs = await DiffScenario.DiffsAsync();
        return new DiffTextView { Diff = diffs.Calculator, FilePath = "src/Calculator.cs", HunkActions = HunkActions.StageAndDiscard, Mode = mode };
    }

    /// <summary>A diff whose changed lines are far wider than the view.</summary>
    private static FileDiff LongLines() => DiffScenario.Parse(
        "@@ -1,3 +1,3 @@",
        " first",
        "-old " + new string('x', 150),
        "+new " + new string('y', 150),
        " last");

    /// <summary>A hunk header's text block, found by the start of its text.</summary>
    private static TextBlock HeaderText(DiffTextView view, string start) =>
        view.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text?.StartsWith(start, StringComparison.Ordinal) == true);

    /// <summary>Where a control is, in the view's coordinates.</summary>
    private static Rect Edges(DiffHost host, DiffTextView view, Control control) =>
        new Rect(host.ToWindow(control, default), control.Bounds.Size).Translate(-(Vector)host.ToWindow(view, default));

    /// <summary>The hunk buttons in the order of the visual tree.</summary>
    private static List<Button> Buttons(DiffTextView view) =>
        view.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).ToList();

    private static List<Control> Editors(DiffTextView view) =>
        view.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>().Cast<Control>().ToList();

    /// <summary>The first row whose top is in view.</summary>
    private static int FirstRowInView(DiffTextView view)
    {
        for (var row = 0; row < view.Rows.Count; row++)
        {
            if (view.RowBounds(row) is { } bounds && bounds.Y >= 0)
            {
                return row;
            }
        }

        return -1;
    }
}
