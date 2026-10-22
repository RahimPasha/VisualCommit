using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Headless tests of the conflict resolver's result (<see cref="ConflictResultEditor"/>,
/// docs/phase-4-design.md 11.3, 11.5, 11.10): typing through the headless window, undo and redo
/// with Avalonia's <c>HotkeyConfiguration</c>, a choice as one undo step, the editor's own caret,
/// the tints of an unresolved block, UI Automation's view of the text, and the keys it leaves to
/// the window. At the bottom half's size at 1100×700 (840 wide).
/// </summary>
public class ConflictResultEditorTests
{
    private const double Width = 840;
    private const double Height = 212;

    /// <summary>A file with one block (merge style) and an empty last line.</summary>
    private const string OneBlock =
        "line 1\n" +
        "<<<<<<< HEAD\n" +
        "line 2 ours\n" +
        "=======\n" +
        "line 2 theirs\n" +
        ">>>>>>> feature/theirs\n" +
        "line 3\n";

    private static readonly Color DarkOurs = Color.Parse("#15323A");
    private static readonly Color DarkTheirs = Color.Parse("#2C2140");
    private static readonly Color DarkBase = Color.Parse("#24272F");
    private static readonly Color DarkMarker = Color.Parse("#3A2E16");
    private static readonly Color DarkWarning = Color.Parse("#E0A23B");
    private static readonly Color LightOurs = Color.Parse("#DCF1F5");
    private static readonly Color LightTheirs = Color.Parse("#F0E6FA");
    private static readonly Color LightMarker = Color.Parse("#FCEFD2");
    private static readonly Color LightWarning = Color.Parse("#895A17");

    public static TheoryData<string> Themes => ["Dark", "Light"];

    [AvaloniaFact]
    public void Text_typed_through_the_window_Backspace_and_Return_edit_the_document()
    {
        var editor = Editor(OneBlock);
        using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
        host.Capture();

        // A click at the end of line 1 puts the caret there and gives the text the focus.
        host.Click(editor, editor.PointAt(1, 7)!.Value);
        Assert.True(editor.TextArea.IsFocused);
        Assert.Equal(new AvaloniaEdit.Document.TextLocation(1, 7), editor.CaretPosition);

        host.Window.KeyTextInput("abc");
        DiffHost.Settle();
        Assert.Equal("line 1abc", Line(editor, 1));
        Assert.Equal(new AvaloniaEdit.Document.TextLocation(1, 10), editor.CaretPosition);

        host.PressKey(Key.Back);
        Assert.Equal("line 1ab", Line(editor, 1));

        host.PressKey(Key.Enter);
        host.Window.KeyTextInput("new line");
        DiffHost.Settle();
        Assert.Equal("line 1ab", Line(editor, 1));
        Assert.Equal("new line", Line(editor, 2));
        Assert.Equal("<<<<<<< HEAD", Line(editor, 3));
        Assert.Equal(new AvaloniaEdit.Document.TextLocation(2, 9), editor.CaretPosition);
        Assert.True(editor.IsModified);
    }

    [AvaloniaFact]
    public void The_input_method_is_on_and_offers_its_client_and_typing_still_works()
    {
        var editor = Editor(OneBlock);
        using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
        host.Click(editor, editor.PointAt(7, 7)!.Value);

        Assert.True(editor.Options.EnableImeSupport);
        var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        editor.TextArea.RaiseEvent(request);
        Assert.NotNull(request.Client);

        host.Window.KeyTextInput("é中");
        DiffHost.Settle();
        Assert.Equal("line 3é中", Line(editor, 7));
    }

    [AvaloniaFact]
    public void Undo_and_redo_are_the_platform_gestures_of_HotkeyConfiguration()
    {
        var editor = Editor(OneBlock);
        using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
        host.Click(editor, editor.PointAt(1, 7)!.Value);
        var keys = host.Window.GetPlatformSettings()!.HotkeyConfiguration;

        host.Window.KeyTextInput("a");
        host.Window.KeyTextInput("b");
        host.Window.KeyTextInput("c");
        DiffHost.Settle();
        Assert.Equal("line 1abc", Line(editor, 1));

        // Each typed character is its own undo step in AvaloniaEdit 12.0.0.
        Press(host, keys.Undo[0]);
        Assert.Equal("line 1ab", Line(editor, 1));
        Press(host, keys.Undo[0]);
        Press(host, keys.Undo[0]);
        Assert.Equal(OneBlock, editor.Text);
        Assert.False(editor.IsModified);

        // Nothing left to undo: the key still does nothing else.
        Press(host, keys.Undo[0]);
        Assert.Equal(OneBlock, editor.Text);

        foreach (var redo in keys.Redo)
        {
            Press(host, redo);
        }

        Assert.Equal("line 1a" + (keys.Redo.Count > 1 ? "b" : string.Empty), Line(editor, 1));
        Press(host, keys.Redo[0]);
        Press(host, keys.Redo[0]);
        Assert.Equal("line 1abc", Line(editor, 1));

        // The editor's own bindings replace AvaloniaEdit's, which pick the modifier by the
        // operating system.
        Assert.DoesNotContain(editor.TextArea.DefaultInputHandler.CommandBindings, binding =>
            binding.Command == AvaloniaEdit.ApplicationCommands.Undo || binding.Command == AvaloniaEdit.ApplicationCommands.Redo);
    }

    [AvaloniaFact]
    public void ReplaceRange_is_one_undo_step_and_puts_the_caret_at_its_start()
    {
        var editor = Editor(OneBlock);
        using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
        host.Click(editor, editor.PointAt(7, 1)!.Value);
        var keys = host.Window.GetPlatformSettings()!.HotkeyConfiguration;

        // "Use ours": the block, from its < line to its > line and that line's end, becomes its ours lines.
        var start = editor.Document.GetLineByNumber(2).Offset;
        var end = editor.Document.GetLineByNumber(6).EndOffset + 1;
        editor.ReplaceRange(start, end - start, "line 2 ours\n");
        DiffHost.Settle();
        Assert.Equal("line 1\nline 2 ours\nline 3\n", editor.Text);
        Assert.Equal(new AvaloniaEdit.Document.TextLocation(2, 1), editor.CaretPosition);
        Assert.All(Enumerable.Range(1, 4), line => Assert.Equal(ConflictTint.None, editor.TintAt(line)));
        Assert.True(editor.IsModified);

        Press(host, keys.Undo[0]);
        Assert.Equal(OneBlock, editor.Text);
        Assert.False(editor.IsModified);
        Assert.Equal(ConflictTint.Marker, editor.TintAt(2));
        Assert.Equal(ConflictTint.Ours, editor.TintAt(3));

        Press(host, keys.Redo[0]);
        Assert.Equal("line 1\nline 2 ours\nline 3\n", editor.Text);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void An_unresolved_block_has_its_tints_and_an_edit_that_removes_its_markers_takes_them_away(string themeName)
    {
        var colors = DiffColors.For(themeName);
        var light = themeName == "Light";
        var editor = Editor(OneBlock);
        using var host = DiffHost.Show(editor, Width, Height, colors.Variant);
        var shot = host.Capture();
        TestPictures.Save(host, $"conflict-result-{themeName.ToLowerInvariant()}");

        Assert.Equal(
            [ConflictTint.None, ConflictTint.Marker, ConflictTint.Ours, ConflictTint.Marker, ConflictTint.Theirs, ConflictTint.Marker, ConflictTint.None, ConflictTint.None],
            Enumerable.Range(1, 8).Select(editor.TintAt));

        // Backgrounds sampled 2 from the left (the gutter) and 30 from the right (no text).
        foreach (var (line, color, what) in new[]
        {
            (1, colors.Background, "line 1"),
            (2, light ? LightMarker : DarkMarker, "the < line"),
            (3, light ? LightOurs : DarkOurs, "the ours line"),
            (4, light ? LightMarker : DarkMarker, "the = line"),
            (5, light ? LightTheirs : DarkTheirs, "the theirs line"),
            (6, light ? LightMarker : DarkMarker, "the > line"),
            (7, colors.Background, "line 3"),
        })
        {
            var y = editor.PointAt(line, 1)!.Value.Y;
            DiffColors.AssertColor(color, shot.PixelAt(host.ToWindow(editor, new Point(2, y))), $"{what}, in the gutter");
            DiffColors.AssertColor(color, shot.PixelAt(host.ToWindow(editor, new Point(Width - 30, y))), $"{what}, right of the text");
        }

        // Marker lines are drawn in the warning colour, other lines in the main text colour.
        Assert.Equal(light ? LightWarning : DarkWarning, ForegroundOf(editor, 2));
        Assert.Equal(colors.TextPrimary, ForegroundOf(editor, 3));

        // The > line emptied with the keyboard: the block is no longer whole, nothing is tinted.
        host.Click(editor, editor.PointAt(6, 23)!.Value);
        host.PressKey(Key.Home, RawInputModifiers.Shift);
        host.PressKey(Key.Delete);
        Assert.Equal(string.Empty, Line(editor, 6));
        Assert.All(Enumerable.Range(1, 8), line => Assert.Equal(ConflictTint.None, editor.TintAt(line)));
        shot = host.Capture();
        DiffColors.AssertColor(colors.Background, shot.PixelAt(host.ToWindow(editor, new Point(Width - 30, editor.PointAt(3, 1)!.Value.Y))), "the old ours line");
        DiffColors.AssertColor(colors.Background, shot.PixelAt(host.ToWindow(editor, new Point(2, editor.PointAt(2, 1)!.Value.Y))), "the old < line, in the gutter");
        Assert.Equal(colors.TextPrimary, ForegroundOf(editor, 2));
    }

    [AvaloniaFact]
    public void A_diff3_base_section_is_tinted_and_a_marker_size_of_the_file_counts()
    {
        var editor = Editor("a\n<<<<<<< ours\nx\n||||||| base\ny\n=======\nz\n>>>>>>> theirs\n");
        using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
        var shot = host.Capture();
        Assert.Equal(
            [ConflictTint.None, ConflictTint.Marker, ConflictTint.Ours, ConflictTint.Marker, ConflictTint.Base, ConflictTint.Marker, ConflictTint.Theirs, ConflictTint.Marker],
            Enumerable.Range(1, 8).Select(editor.TintAt));
        DiffColors.AssertColor(DarkBase, shot.PixelAt(host.ToWindow(editor, new Point(Width - 30, editor.PointAt(5, 1)!.Value.Y))), "the base line");

        editor.MarkerSize = 9;
        Assert.All(Enumerable.Range(1, 8), line => Assert.Equal(ConflictTint.None, editor.TintAt(line)));
    }

    [AvaloniaFact]
    public async Task The_caret_is_drawn_only_while_the_text_has_the_focus_and_stays_still_while_blinking_is_off()
    {
        var blinks = ConflictResultEditor.CaretBlinks;
        try
        {
            ConflictResultEditor.CaretBlinks = false;
            var editor = Editor(OneBlock);
            var other = new Button { Content = "Elsewhere" };
            DockPanel.SetDock(other, Dock.Bottom);
            var root = new DockPanel { Children = { other, editor } };
            using var host = DiffHost.Show(root, Width, Height + 40, ThemeVariant.Dark);
            host.Capture();

            // The caret on the empty last line, at the text column's left edge: nothing else is drawn there.
            var caret = host.ToWindow(editor, editor.PointAt(8, 1)!.Value);
            Assert.Equal(DiffColors.Dark.Background, host.Capture().PixelAt(caret));

            host.Click(editor, editor.PointAt(8, 1)!.Value);
            Assert.Equal(new AvaloniaEdit.Document.TextLocation(8, 1), editor.CaretPosition);
            var first = host.Capture();
            TestPictures.Save(host, "conflict-result-caret");
            Assert.Equal(DiffColors.Dark.TextPrimary, first.PixelAt(caret));
            Assert.Equal(DiffColors.Dark.Background, first.PixelAt(caret + new Point(1, 0)));

            // Idle for three blink periods: the same picture, pixel for pixel.
            await WaitWhileRunningAsync(TimeSpan.FromMilliseconds(3 * ConflictResultEditor.CaretBlinkMilliseconds));
            var second = host.Capture();
            Assert.Equal(0, DifferingPixels(first, second));

            other.Focus();
            DiffHost.Settle();
            Assert.False(editor.TextArea.IsFocused);
            Assert.Equal(DiffColors.Dark.Background, host.Capture().PixelAt(caret));
        }
        finally
        {
            ConflictResultEditor.CaretBlinks = blinks;
        }
    }

    [AvaloniaFact]
    public async Task While_blinking_is_on_the_caret_is_shown_and_hidden_in_turn()
    {
        var blinks = ConflictResultEditor.CaretBlinks;
        try
        {
            ConflictResultEditor.CaretBlinks = true;
            var editor = Editor(OneBlock);
            using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
            host.Click(editor, editor.PointAt(8, 1)!.Value);
            var caret = host.ToWindow(editor, editor.PointAt(8, 1)!.Value);

            var seen = new HashSet<Color>();
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 4 * ConflictResultEditor.CaretBlinkMilliseconds && seen.Count < 2)
            {
                await WaitWhileRunningAsync(TimeSpan.FromMilliseconds(60));
                seen.Add(host.Capture().PixelAt(caret));
            }

            Assert.Contains(DiffColors.Dark.TextPrimary, seen);
            Assert.Contains(DiffColors.Dark.Background, seen);
        }
        finally
        {
            ConflictResultEditor.CaretBlinks = blinks;
        }
    }

    [AvaloniaFact]
    public void Tab_types_a_tab_and_Ctrl_Tab_moves_the_focus_out()
    {
        var editor = Editor(OneBlock);
        var next = new Button { Content = "Next" };
        DockPanel.SetDock(next, Dock.Bottom);
        var root = new DockPanel { Children = { next, editor } };
        using var host = DiffHost.Show(root, Width, Height + 40, ThemeVariant.Dark);
        host.Click(editor, editor.PointAt(1, 1)!.Value);

        host.PressKey(Key.Tab);
        Assert.Equal("\tline 1", Line(editor, 1));
        Assert.True(editor.TextArea.IsFocused);

        host.PressKey(Key.Tab, RawInputModifiers.Control);
        Assert.Equal("\tline 1", Line(editor, 1));
        Assert.False(editor.TextArea.IsKeyboardFocusWithin);
        Assert.True(next.IsFocused);
    }

    [AvaloniaFact]
    public void Escape_is_left_to_the_window()
    {
        var editor = Editor(OneBlock);
        using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
        bool? handled = null;
        host.Window.AddHandler(InputElement.KeyDownEvent, (_, e) => handled = e.Key == Key.Escape ? e.Handled : handled, RoutingStrategies.Bubble, handledEventsToo: true);
        host.Click(editor, editor.PointAt(3, 5)!.Value);

        host.PressKey(Key.Escape);

        Assert.False(handled);
        Assert.True(editor.TextArea.IsFocused);
        Assert.Equal(OneBlock, editor.Text);
    }

    [AvaloniaFact]
    public void UI_Automation_sees_a_document_with_its_owner_s_id_whose_read_only_value_is_the_text()
    {
        var editor = Editor(OneBlock);
        AutomationProperties.SetAutomationId(editor, "ConflictResult");
        using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
        host.Click(editor, editor.PointAt(1, 7)!.Value);
        host.Window.KeyTextInput("!");
        DiffHost.Settle();

        var peer = ControlAutomationPeer.CreatePeerForElement(editor);
        Assert.Equal("ConflictResult", peer.GetAutomationId());
        Assert.Equal(AutomationControlType.Document, peer.GetAutomationControlType());
        Assert.True(peer.IsControlElement());
        var value = peer.GetProvider<IValueProvider>() ?? throw new InvalidOperationException("The result has no Value pattern.");
        Assert.True(value.IsReadOnly);
        Assert.Equal(OneBlock.Replace("line 1\n", "line 1!\n", StringComparison.Ordinal), value.Value);
    }

    [AvaloniaFact]
    public void Modified_compares_the_text_with_the_document_s_original_which_MarkOriginal_moves()
    {
        var editor = Editor(OneBlock);
        using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
        host.Click(editor, editor.PointAt(1, 7)!.Value);
        Assert.False(editor.IsModified);

        host.Window.KeyTextInput("x");
        DiffHost.Settle();
        Assert.True(editor.IsModified);
        host.PressKey(Key.Back);
        Assert.False(editor.IsModified);

        host.Window.KeyTextInput("y");
        DiffHost.Settle();
        editor.MarkOriginal();
        Assert.False(editor.IsModified);

        // Another document keeps its own original; the first, shown again, its own.
        var first = editor.Document;
        editor.Document = new AvaloniaEdit.Document.TextDocument("other\n");
        DiffHost.Settle();
        Assert.False(editor.IsModified);
        editor.Document = first;
        DiffHost.Settle();
        Assert.False(editor.IsModified);
        Assert.Equal("line 1y", Line(editor, 1));
    }

    [Fact]
    public void The_tints_are_ConflictText_s_blocks_on_the_editor_s_own_lines()
    {
        Assert.Equal(
            [ConflictTint.None, ConflictTint.Marker, ConflictTint.Ours, ConflictTint.Marker, ConflictTint.Theirs, ConflictTint.Marker, ConflictTint.None, ConflictTint.None],
            ConflictResultEditor.TintsOf(OneBlock.Split('\n')));

        // A block that is not whole, a marker of another size, and a second = line are text (git's rerere reading).
        Assert.All(ConflictResultEditor.TintsOf(["<<<<<<< HEAD", "x", "=======", "y"]), tint => Assert.Equal(ConflictTint.None, tint));
        Assert.All(ConflictResultEditor.TintsOf(["<<<<<<<< HEAD", "x", "========", "y", ">>>>>>>> t"]), tint => Assert.Equal(ConflictTint.None, tint));
        Assert.All(ConflictResultEditor.TintsOf(["<<<<<<< HEAD", "x", "=======", "y", "=======", "z", ">>>>>>> t"]), tint => Assert.Equal(ConflictTint.None, tint));
        Assert.Equal(ConflictTint.Marker, ConflictResultEditor.TintsOf(["<<<<<<<< HEAD", "x", "========", "y", ">>>>>>>> t"], 8)[0]);

        // Two blocks, a diff3 base section in the second; line ends of either kind.
        Assert.Equal(
            [ConflictTint.Marker, ConflictTint.Ours, ConflictTint.Marker, ConflictTint.Theirs, ConflictTint.Marker, ConflictTint.None, ConflictTint.Marker, ConflictTint.Ours, ConflictTint.Marker, ConflictTint.Base, ConflictTint.Marker, ConflictTint.Theirs, ConflictTint.Marker],
            ConflictResultEditor.TintsOf(["<<<<<<< a", "1", "=======", "2", ">>>>>>> b", "mid", "<<<<<<< a", "3", "||||||| base", "4", "=======", "5", ">>>>>>> b"]));
    }

    [AvaloniaFact]
    public void RevealLine_puts_a_line_third_in_view_and_ScrollToOffset_keeps_a_place()
    {
        var text = string.Concat(Enumerable.Range(1, 60).Select(n => $"line {n}\n"));
        var editor = Editor(text);
        using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
        host.Capture();

        editor.RevealLine(2);
        host.Capture();
        Assert.Equal(0, editor.ScrollOffset.Y, 0.5);

        editor.RevealLine(30);
        host.Capture();
        Assert.Equal(27 * 15.84, editor.ScrollOffset.Y, 1.0);
        Assert.Equal(2.5 * 15.84, editor.PointAt(30, 1)!.Value.Y, 1.5);

        // The text stops at its end.
        editor.RevealLine(60);
        host.Capture();
        Assert.Equal((61 * 15.84) - Height, editor.ScrollOffset.Y, 2.0);

        editor.ScrollToOffset(new Vector(0, 100));
        host.Capture();
        Assert.Equal(100, editor.ScrollOffset.Y, 0.5);

        // Asked before the text is laid out, it waits for the layout.
        var later = Editor(text);
        later.RevealLine(40);
        using var other = DiffHost.Show(later, Width, Height, ThemeVariant.Dark);
        other.Capture();
        Assert.Equal(37 * 15.84, later.ScrollOffset.Y, 1.0);
    }

    [AvaloniaFact]
    public void A_read_only_result_takes_no_typing()
    {
        var editor = Editor(OneBlock);
        using var host = DiffHost.Show(editor, Width, Height, ThemeVariant.Dark);
        host.Click(editor, editor.PointAt(1, 7)!.Value);

        editor.IsReadOnly = true;
        host.Window.KeyTextInput("abc");
        DiffHost.Settle();

        Assert.Equal(OneBlock, editor.Text);
    }

    /// <summary>An editor showing <paramref name="text"/> as its document's original.</summary>
    private static ConflictResultEditor Editor(string text) => new() { Document = new AvaloniaEdit.Document.TextDocument(text) };

    private static string Line(ConflictResultEditor editor, int line) => editor.Document.GetText(editor.Document.GetLineByNumber(line));

    /// <summary>The colour the first character of a line is drawn in.</summary>
    private static Color ForegroundOf(ConflictResultEditor editor, int line)
    {
        var textView = editor.TextArea.TextView;
        var visual = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(line));
        var brush = visual.Elements[0].TextRunProperties.ForegroundBrush;
        return Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
    }

    /// <summary>Presses a gesture's key with its modifiers.</summary>
    private static void Press(DiffHost host, KeyGesture gesture)
    {
        var modifiers = RawInputModifiers.None;
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            modifiers |= RawInputModifiers.Control;
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            modifiers |= RawInputModifiers.Shift;
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            modifiers |= RawInputModifiers.Alt;
        }

        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            modifiers |= RawInputModifiers.Meta;
        }

        host.PressKey(gesture.Key, modifiers);
    }

    /// <summary>
    /// Lets the UI thread run for <paramref name="time"/>, its timers included: awaiting hands the
    /// thread back to the headless session's dispatcher loop (running its jobs alone fires no timer).
    /// </summary>
    private static async Task WaitWhileRunningAsync(TimeSpan time)
    {
        await Task.Delay(time);
        DiffHost.Settle();
    }

    private static int DifferingPixels(Screenshot first, Screenshot second)
    {
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
        var differing = 0;
        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                if (first.PixelAt(x, y) != second.PixelAt(x, y))
                {
                    differing++;
                }
            }
        }

        return differing;
    }
}
