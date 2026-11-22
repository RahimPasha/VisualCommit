using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.App.Views;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The text dialog's text view (D432), in a window of its own with no session, showing the
/// third-party notices as the app embeds them: read-only, wrapped, no tab kept; Down scrolls a
/// line, PageDown the view less a line, End to the true last line of the wrapped text, Home to the
/// top (also with the platform's command key), and a wheel up after End stays up; Enter, with or
/// without modifiers, neither scrolls nor is taken; a key that would move, extend or use the hidden
/// caret, or type, does nothing; the platform's copy key copies a selection and nothing without
/// one, its select-all selects the whole text, and its cut, paste, undo and redo change nothing (the
/// headless platform's clipboard, in the test process); Tab leaves it; its peer is a document whose
/// value is the text. Every key with the command modifier is the platform's gesture
/// (<c>HotkeyConfiguration</c>), never a fixed Ctrl: the headless platform's is Ctrl on every system.
/// </summary>
public sealed class DocumentTextViewTests
{
    private const string Title = "Third-party notices";

    /// <summary>Every mix of Shift, Ctrl, Alt and Meta, none first.</summary>
    private static readonly KeyModifiers[] Mixes = Enumerable.Range(0, 16)
        .Select(bits => new[] { KeyModifiers.Alt, KeyModifiers.Control, KeyModifiers.Shift, KeyModifiers.Meta }
            .Where((_, index) => (bits & (1 << index)) != 0)
            .Aggregate(KeyModifiers.None, (all, one) => all | one))
        .ToArray();

    /// <summary>Text typed into the view: letters, a digit, Space, others, a tab and line ends.</summary>
    private static readonly string[] Typed = ["a", "Z", "1", " ", "é中", "\t", "\r", "\n"];

    private static (DiffHost Host, DocumentTextView View, Button After) Show(string text)
    {
        var view = new DocumentTextView { Height = 400 };
        AutomationProperties.SetName(view, Title);
        var after = new Button { Content = "After" };
        var panel = new StackPanel { Children = { view, after } };
        var host = DiffHost.Show(panel, 720, 520, ThemeVariant.Dark);
        view.Show(text);
        DiffHost.Settle();
        Assert.True(view.FocusText());
        DiffHost.Settle();
        return (host, view, after);
    }

    [AvaloniaFact]
    public void The_view_is_read_only_wrapped_and_keeps_no_tab()
    {
        var (host, view, _) = Show(EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices));
        using var window = host;

        Assert.True(view.IsReadOnly);
        Assert.True(view.WordWrap);
        Assert.False(view.Options.AcceptsTab);
        Assert.Equal(11.5, view.FontSize);
        Assert.Equal(EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices), view.Document.Text);
        Assert.Equal(0, view.ScrollOffset.Y);
        Assert.True(view.TextArea.IsFocused);
    }

    [AvaloniaFact]
    public void Down_scrolls_a_line_and_PageDown_the_view_less_a_line()
    {
        var (host, view, _) = Show(EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices));
        using var window = host;
        var line = view.LineHeight;
        Assert.True(line > 10, $"A line is {line} high.");

        host.PressKey(Key.Down);
        Assert.Equal(line, view.ScrollOffset.Y, 0.5);
        host.PressKey(Key.Up);
        Assert.Equal(0, view.ScrollOffset.Y, 0.5);

        host.PressKey(Key.PageDown);
        Assert.Equal(view.ScrollViewport.Height - line, view.ScrollOffset.Y, 0.5);

        // The hidden caret stays at the start: Up and Down scroll, they do not move it.
        Assert.Equal(0, view.TextArea.Caret.Offset);
    }

    [AvaloniaFact]
    public void End_shows_the_last_line_and_Home_the_first()
    {
        var text = EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices);
        var (host, view, _) = Show(text);
        using var window = host;

        host.PressKey(Key.End);
        DiffHost.Settle();
        var end = view.ScrollExtent.Height - view.ScrollViewport.Height;
        Assert.True(end > 0, "The notices are longer than the view.");
        Assert.Equal(end, view.ScrollOffset.Y, 1.0);

        // The document's last line (empty, after the file's final line end) and the file's last
        // line are drawn whole at the view's bottom.
        var textView = view.TextArea.TextView;
        var document = view.Document;
        Assert.Equal(string.Empty, document.GetText(document.Lines[^1]));
        var lastWritten = document.Lines.Last(line => document.GetText(line).Length > 0);
        Assert.Equal(text.TrimEnd('\n').Split('\n')[^1], document.GetText(lastWritten));
        foreach (var number in new[] { lastWritten.LineNumber, document.LineCount })
        {
            var visual = textView.GetVisualLine(number);
            Assert.NotNull(visual);
            Assert.True(visual!.VisualTop + visual.Height <= textView.VerticalOffset + textView.Bounds.Height + 0.5, $"Line {number} ends below the view.");
            Assert.True(visual.VisualTop >= textView.VerticalOffset - 0.5, $"Line {number} starts above the view.");
        }

        Assert.Equal(document.TextLength, view.TextArea.Caret.Offset);

        host.PressKey(Key.Home);
        Assert.Equal(0, view.ScrollOffset.Y, 0.5);
        Assert.Equal(0, view.TextArea.Caret.Offset);

        // With the platform's command key (Ctrl; Cmd on macOS).
        var command = ShortcutKeys.CommandModifierOf(Hotkeys(host));
        Press(host, new KeyGesture(Key.End, command));
        DiffHost.Settle();
        Assert.Equal(view.ScrollExtent.Height - view.ScrollViewport.Height, view.ScrollOffset.Y, 1.0);
        Press(host, new KeyGesture(Key.Home, command));
        Assert.Equal(0, view.ScrollOffset.Y, 0.5);
    }

    [AvaloniaFact]
    public void Enter_with_or_without_modifiers_leaves_the_view_where_it_is_and_plain_Enter_goes_on_unhandled()
    {
        var text = EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices);
        var (host, view, _) = Show(text);
        using var window = host;

        // What reaches the view's parent: the dialog's own Enter (DialogView) is a handler there.
        var handled = new List<bool>();
        ((Control)view.Parent!).AddHandler(
            InputElement.KeyDownEvent,
            (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    handled.Add(e.Handled);
                }
            },
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        // No modifier, then Shift, Ctrl, Alt and Meta, alone and mixed: none takes the key in the text.
        RawInputModifiers[] keys = [RawInputModifiers.Shift, RawInputModifiers.Control, RawInputModifiers.Alt, RawInputModifiers.Meta];
        var mixes = Enumerable.Range(0, 1 << keys.Length)
            .Select(bits => keys.Where((_, index) => (bits & (1 << index)) != 0).Aggregate(RawInputModifiers.None, (all, one) => all | one))
            .ToArray();

        // The hidden caret at the top (at opening), the view three pages down; then the caret at
        // the end (End), the view three pages up: Enter brought the caret into view, a jump back.
        foreach (var (start, away) in new[] { (Key.Home, Key.PageDown), (Key.End, Key.PageUp) })
        {
            host.PressKey(start);
            DiffHost.Settle();
            for (var page = 0; page < 3; page++)
            {
                host.PressKey(away);
            }

            DiffHost.Settle();
            var offset = view.ScrollOffset.Y;
            var caret = view.TextArea.Caret.Offset;
            Assert.True(view.TextArea.TextView.GetVisualLine(view.Document.GetLineByOffset(caret).LineNumber) is null, "The caret's line is still in view: the case is not made.");
            foreach (var modifiers in mixes)
            {
                handled.Clear();
                host.PressKey(Key.Enter, modifiers);
                DiffHost.Settle();

                Assert.True(Math.Abs(view.ScrollOffset.Y - offset) <= 0.5, $"Enter with {modifiers} after {start} and three {away}s moved the view from {offset} to {view.ScrollOffset.Y}.");
                Assert.Equal(caret, view.TextArea.Caret.Offset);
                Assert.Equal(text, view.Document.Text);
                Assert.True(view.TextArea.IsFocused);
                Assert.False(Assert.Single(handled), $"Enter with {modifiers} was taken in the text.");
            }
        }
    }

    [AvaloniaFact]
    public void Another_editor_still_takes_its_keys_line_breaks_typing_deleting_and_moving_its_caret()
    {
        // The text's keys go from its own text area alone: the app's other editors (the diff, the
        // blame, the conflict panes and result) are AvaloniaEdit's with its key bindings. A text view
        // is made and pressed first, so that nothing it does to its keys can stay behind.
        var (documentHost, _, _) = Show(EmbeddedTexts.Read(EmbeddedTexts.License));
        documentHost.Window.KeyTextInput("a");
        documentHost.PressKey(Key.Back);
        documentHost.PressKey(Key.Right, RawInputModifiers.Shift);
        documentHost.Dispose();

        var editor = new AvaloniaEdit.TextEditor { Document = new AvaloniaEdit.Document.TextDocument("ab"), Height = 200 };
        using var host = DiffHost.Show(editor, 400, 300, ThemeVariant.Dark);
        Assert.True(editor.TextArea.Focus());
        editor.TextArea.Caret.Offset = 1;
        DiffHost.Settle();

        host.PressKey(Key.Enter);
        host.PressKey(Key.Enter, RawInputModifiers.Shift);

        Assert.Equal(3, editor.Document.LineCount);
        Assert.Equal(["a", string.Empty, "b"], editor.Document.Lines.Select(line => editor.Document.GetText(line)));

        // Typed text, Backspace and Delete edit; Left moves the caret; Shift+Right selects. (The
        // line breaks are the system's, as the editor types them into a text that has none.)
        host.Window.KeyTextInput("x");
        DiffHost.Settle();
        Assert.Equal(["a", string.Empty, "xb"], editor.Document.Lines.Select(line => editor.Document.GetText(line)));
        host.PressKey(Key.Back);
        Assert.Equal(["a", string.Empty, "b"], editor.Document.Lines.Select(line => editor.Document.GetText(line)));
        host.PressKey(Key.Delete);
        Assert.Equal(["a", string.Empty, string.Empty], editor.Document.Lines.Select(line => editor.Document.GetText(line)));
        Assert.Equal((3, 1), (editor.TextArea.Caret.Line, editor.TextArea.Caret.Column));
        host.PressKey(Key.Left);
        Assert.Equal((2, 1), (editor.TextArea.Caret.Line, editor.TextArea.Caret.Column));
        var lineBreak = editor.Document.GetLineByNumber(2).DelimiterLength;
        host.PressKey(Key.Right, RawInputModifiers.Shift);
        Assert.Equal((editor.Document.GetLineByNumber(2).Offset, lineBreak), (editor.SelectionStart, editor.SelectionLength));
    }

    [AvaloniaFact]
    public void A_wheel_up_after_End_leaves_the_end_though_the_lines_it_brings_in_wrap()
    {
        // Lines that wrap to several rows each: those well above the end are measured only when a
        // long move up brings them into view (a fast flick of the wheel, a drag of the scroll bar's
        // thumb), which grows the extent in the same layout pass as the move.
        var text = string.Join('\n', Enumerable.Range(1, 200).Select(number => $"Line {number}: " + string.Join(' ', Enumerable.Repeat("wrapped words", 30)))) + "\n";
        var (host, view, _) = Show(text);
        using var window = host;
        host.PressKey(Key.End);
        DiffHost.Settle();
        Assert.Equal(view.ScrollExtent.Height - view.ScrollViewport.Height, view.ScrollOffset.Y, 1.0);
        var extentAtEnd = view.ScrollExtent.Height;

        host.Window.MouseWheel(host.ToWindow(view, new Point(100, 100)), new Vector(0, 40));
        DiffHost.Settle();
        Assert.True(view.ScrollExtent.Height > extentAtEnd, "The move up measured no new line: the case is not made.");

        var end = view.ScrollExtent.Height - view.ScrollViewport.Height;
        Assert.True(
            view.ScrollOffset.Y < end - 1,
            $"The wheel's move up was undone: offset {view.ScrollOffset.Y}, end {end}; the extent {view.ScrollExtent.Height}, {extentAtEnd} at the end.");
    }

    [AvaloniaFact]
    public async Task The_copy_key_copies_a_selection_and_nothing_without_one()
    {
        var (host, view, _) = Show(EmbeddedTexts.Read(EmbeddedTexts.License));
        using var window = host;

        // The headless platform's clipboard, which lives in the test process.
        var clipboard = ClipboardOf(view);
        const string Before = "What the clipboard held before the copy key.";

        // Every gesture of the platform's copy (HotkeyConfiguration.Copy): the command key with C
        // (Ctrl; Cmd on macOS), and Ctrl+Insert.
        var gestures = Hotkeys(host).Copy;
        Assert.NotEmpty(gestures);
        foreach (var gesture in gestures)
        {
            view.TextArea.ClearSelection();
            await clipboard.SetTextAsync(Before);
            Press(host, gesture);
            Assert.True(Before == await clipboard.TryGetTextAsync(), $"{gesture} with nothing selected changed the clipboard.");

            view.Select(0, "MIT License".Length);
            Press(host, gesture);
            var copied = await clipboard.TryGetTextAsync();
            Assert.True("MIT License" == copied, $"{gesture} with \"MIT License\" selected left \"{copied}\" on the clipboard.");
        }
    }

    [AvaloniaFact]
    public async Task The_select_all_key_selects_the_whole_text_and_leaves_the_view_where_it_is()
    {
        var text = EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices);
        var (host, view, _) = Show(text);
        using var window = host;
        var clipboard = ClipboardOf(view);

        var gestures = Hotkeys(host).SelectAll;
        Assert.NotEmpty(gestures);
        foreach (var gesture in gestures)
        {
            host.PressKey(Key.Home);
            for (var page = 0; page < 3; page++)
            {
                host.PressKey(Key.PageDown);
            }

            var offset = view.ScrollOffset.Y;
            Press(host, gesture);

            Assert.True(Math.Abs(view.ScrollOffset.Y - offset) <= 0.5, $"{gesture} moved the view from {offset} to {view.ScrollOffset.Y}.");
            Assert.Equal((0, text.Length), (view.SelectionStart, view.SelectionLength));
            Assert.True(view.TextArea.IsFocused);

            // The copy key copies it whole, with the system's line ends as AvaloniaEdit writes them.
            await clipboard.SetTextAsync(string.Empty);
            Press(host, Hotkeys(host).Copy[0]);
            Assert.Equal(WithSystemLineEnds(text), await clipboard.TryGetTextAsync());
        }
    }

    [AvaloniaFact]
    public async Task Keys_that_would_move_extend_or_use_the_hidden_caret_or_type_do_nothing()
    {
        // The ruling: the text shows no caret, so it takes no key that would move, use or change one.
        // Typed text; Backspace, Delete, Left and Right with any modifiers; Shift, with any others,
        // with Up, Down, Home, End, PageUp and PageDown; and every other key the editor binds, but its
        // reading keys and the platform's copy, select-all, cut, paste, undo and redo. Each does
        // nothing (the view, the hidden caret, the selection, the text and the focus stay), and is
        // taken, so that nothing past the text acts on it. Each is held for three repeats.
        var text = EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices);
        var (host, view, _) = Show(text);
        using var window = host;
        var clipboard = ClipboardOf(view);
        var hotkeys = Hotkeys(host);
        var gestures = NothingGestures(view, hotkeys);

        var taken = new List<bool>();
        var parent = (Control)view.Parent!;
        parent.AddHandler(InputElement.KeyDownEvent, (_, e) => taken.Add(e.Handled), RoutingStrategies.Bubble, handledEventsToo: true);
        parent.AddHandler(InputElement.TextInputEvent, (_, e) => taken.Add(e.Handled), RoutingStrategies.Bubble, handledEventsToo: true);

        var failures = new List<string>();
        foreach (var mouseSelection in new[] { false, true })
        {
            var state = mouseSelection ? "a selection made with the mouse and three PageDowns" : "three PageDowns from the top";
            AwayFromTheCaret(host, view, mouseSelection);
            var selected = mouseSelection ? view.SelectedText : string.Empty;
            var presses = gestures.Select(gesture => (Name: gesture.ToString(), Press: (Action)(() => Hold(host, gesture))))
                .Concat(Typed.Select(typed => (Name: $"typing \"{Escaped(typed)}\"", Press: (Action)(() => TypeHeld(host, typed)))));
            foreach (var (name, press) in presses)
            {
                var (offset, caret, start, length) = (view.ScrollOffset.Y, view.TextArea.Caret.Offset, view.SelectionStart, view.SelectionLength);
                taken.Clear();
                press();

                var wrong = new List<string>();
                if (Math.Abs(view.ScrollOffset.Y - offset) > 0.5)
                {
                    wrong.Add($"moved the view from {offset:0.##} to {view.ScrollOffset.Y:0.##}");
                }

                if (view.TextArea.Caret.Offset != caret)
                {
                    wrong.Add($"moved the hidden caret from {caret} to {view.TextArea.Caret.Offset}");
                }

                if ((view.SelectionStart, view.SelectionLength) != (start, length))
                {
                    wrong.Add($"changed the selection from ({start}, {length}) to ({view.SelectionStart}, {view.SelectionLength})");
                }

                if (view.Document.Text != text)
                {
                    wrong.Add("changed the text");
                }

                if (!view.TextArea.IsFocused)
                {
                    wrong.Add("moved the focus out of the text");
                }

                if (taken.Count == 0 || taken.Contains(false))
                {
                    wrong.Add("was not taken in the text");
                }

                if (wrong.Count > 0)
                {
                    failures.Add($"{name} after {state}: {string.Join("; ", wrong)}.");
                    AwayFromTheCaret(host, view, mouseSelection);
                }
            }

            if (mouseSelection)
            {
                // The selection made with the mouse stays, and the copy key copies it.
                Assert.Equal(10, selected.Length);
                await clipboard.SetTextAsync(string.Empty);
                Press(host, hotkeys.Copy[0]);
                Assert.Equal(selected, await clipboard.TryGetTextAsync());
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} keys did something:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [AvaloniaFact]
    public async Task Cut_paste_undo_and_redo_change_nothing_and_leave_the_view_where_it_is()
    {
        // Read-only, the editor's cut, paste, undo and redo already do nothing: the platform's
        // gestures (HotkeyConfiguration) and AvaloniaEdit's own (which it picks by the operating
        // system, Cmd on macOS) change neither the text, the clipboard, the selection, the hidden
        // caret nor the view.
        var text = EmbeddedTexts.Read(EmbeddedTexts.ThirdPartyNotices);
        var (host, view, _) = Show(text);
        using var window = host;
        var clipboard = ClipboardOf(view);
        const string Held = "What the clipboard holds.";
        await clipboard.SetTextAsync(Held);

        var hotkeys = Hotkeys(host);
        var gestures = hotkeys.Cut.Concat(hotkeys.Paste).Concat(hotkeys.Undo).Concat(hotkeys.Redo)
            .Concat(new[] { AvaloniaEdit.ApplicationCommands.Cut, AvaloniaEdit.ApplicationCommands.Paste, AvaloniaEdit.ApplicationCommands.Undo, AvaloniaEdit.ApplicationCommands.Redo }.Select(command => command.Gesture))
            .DistinctBy(gesture => (gesture.Key, gesture.KeyModifiers))
            .ToList();
        foreach (var mouseSelection in new[] { false, true })
        {
            AwayFromTheCaret(host, view, mouseSelection);
            foreach (var gesture in gestures)
            {
                var (offset, caret, start, length) = (view.ScrollOffset.Y, view.TextArea.Caret.Offset, view.SelectionStart, view.SelectionLength);
                Press(host, gesture);

                Assert.True(Math.Abs(view.ScrollOffset.Y - offset) <= 0.5, $"{gesture} moved the view from {offset} to {view.ScrollOffset.Y}.");
                Assert.True(view.TextArea.Caret.Offset == caret, $"{gesture} moved the hidden caret.");
                Assert.True((view.SelectionStart, view.SelectionLength) == (start, length), $"{gesture} changed the selection.");
                Assert.True(view.Document.Text == text, $"{gesture} changed the text.");
                Assert.Equal(Held, await clipboard.TryGetTextAsync());
                Assert.True(view.TextArea.IsFocused);
            }
        }
    }

    [AvaloniaFact]
    public void Tab_leaves_the_text()
    {
        var (host, view, after) = Show(EmbeddedTexts.Read(EmbeddedTexts.License));
        using var window = host;
        var before = view.Document.Text;

        host.PressKey(Key.Tab);

        Assert.True(after.IsFocused, "Tab did not move the focus out of the text.");
        Assert.Equal(before, view.Document.Text);
    }

    [AvaloniaFact]
    public void The_peer_is_a_document_whose_value_is_the_text_and_whose_name_is_the_title()
    {
        var license = EmbeddedTexts.Read(EmbeddedTexts.License);
        var (host, view, _) = Show(license);
        using var window = host;

        var peer = ControlAutomationPeer.CreatePeerForElement(view);
        Assert.Equal(AutomationControlType.Document, peer.GetAutomationControlType());
        Assert.Equal(Title, peer.GetName());
        var value = Assert.IsAssignableFrom<IValueProvider>(peer);
        Assert.True(value.IsReadOnly);
        Assert.Equal(license, value.Value);
    }

    /// <summary>
    /// The keys that would move, extend or use the hidden caret, or edit (the ruling): Backspace,
    /// Delete, Left and Right with every mix of modifiers; Up, Down, Home, End, PageUp and PageDown
    /// with every mix that holds Shift; then every other gesture the editor's text area binds, its
    /// keys and its commands' own, but its reading keys and the commands kept (copy, select-all,
    /// cut, paste, undo and redo).
    /// </summary>
    private static List<KeyGesture> NothingGestures(DocumentTextView view, PlatformHotkeyConfiguration hotkeys)
    {
        var gestures = new List<KeyGesture>();
        foreach (var key in new[] { Key.Back, Key.Delete, Key.Left, Key.Right })
        {
            gestures.AddRange(Mixes.Select(mix => new KeyGesture(key, mix)));
        }

        foreach (var key in new[] { Key.Up, Key.Down, Key.Home, Key.End, Key.PageUp, Key.PageDown })
        {
            gestures.AddRange(Mixes.Where(mix => mix.HasFlag(KeyModifiers.Shift)).Select(mix => new KeyGesture(key, mix)));
        }

        var command = ShortcutKeys.CommandModifierOf(hotkeys);
        AvaloniaEdit.RoutedCommand[] kept =
        [
            AvaloniaEdit.ApplicationCommands.Copy, AvaloniaEdit.ApplicationCommands.SelectAll, AvaloniaEdit.ApplicationCommands.Cut,
            AvaloniaEdit.ApplicationCommands.Paste, AvaloniaEdit.ApplicationCommands.Undo, AvaloniaEdit.ApplicationCommands.Redo,
        ];
        gestures.AddRange(
            KeyBindingsOf(view.TextArea.DefaultInputHandler).Select(binding => binding.Gesture)
                .Concat(view.TextArea.CommandBindings.Where(binding => !kept.Contains(binding.Command)).Select(binding => binding.Command.Gesture))
                .OfType<KeyGesture>()
                .Where(gesture => !IsReadingKey(gesture, command)));
        return [.. gestures.DistinctBy(gesture => (gesture.Key, gesture.KeyModifiers))];
    }

    /// <summary>Up, Down, PageUp and PageDown alone, and Home and End alone or with the command key: the keys that scroll (8.3.2).</summary>
    private static bool IsReadingKey(KeyGesture gesture, KeyModifiers command) => gesture.Key switch
    {
        Key.Up or Key.Down or Key.PageUp or Key.PageDown => gesture.KeyModifiers == KeyModifiers.None,
        Key.Home or Key.End => gesture.KeyModifiers == KeyModifiers.None || gesture.KeyModifiers == command,
        _ => false,
    };

    /// <summary>The key bindings of an input handler and the handlers nested in it.</summary>
    private static IEnumerable<KeyBinding> KeyBindingsOf(ITextAreaInputHandler handler) =>
        handler is TextAreaInputHandler input
            ? input.KeyBindings.Concat(input.NestedInputHandlers.SelectMany(KeyBindingsOf))
            : [];

    /// <summary>
    /// Leaves the hidden caret out of view: at the top, where the text opens with it, or at the end
    /// of a selection the mouse makes on the first line in view that is long enough (its columns 6
    /// to 16); then three PageDowns, so that a key that brought the caret into view would move the
    /// view back.
    /// </summary>
    private static void AwayFromTheCaret(DiffHost host, DocumentTextView view, bool mouseSelection)
    {
        if (!view.TextArea.IsFocused)
        {
            Assert.True(view.FocusText());
        }

        host.PressKey(Key.Home);
        if (mouseSelection)
        {
            var lines = view.TextArea.TextView.VisualLines.Select(visual => visual.FirstDocumentLine)
                .Where(line => line.Length >= 30 && view.PointAt(line.LineNumber, 20) is not null)
                .ToList();
            var line = lines[0];

            // A click on another line first: a drag made again at once at the same place would be a
            // double click, which selects a word.
            host.Click(view, view.PointAt(lines[^1].LineNumber, 20)!.Value);
            host.Drag(view, view.PointAt(line.LineNumber, 6)!.Value, view.PointAt(line.LineNumber, 16)!.Value);
            Assert.Equal((line.Offset + 5, 10), (view.SelectionStart, view.SelectionLength));
            Assert.True(view.TextArea.IsFocused);
        }

        for (var page = 0; page < 3; page++)
        {
            host.PressKey(Key.PageDown);
        }

        DiffHost.Settle();
        var caretLine = view.Document.GetLineByOffset(view.TextArea.Caret.Offset).LineNumber;
        Assert.True(view.TextArea.TextView.GetVisualLine(caretLine) is null, "The caret's line is still in view: the case is not made.");
    }

    /// <summary>The platform's hotkeys, as the app reads them (never from a test of the operating system).</summary>
    private static PlatformHotkeyConfiguration Hotkeys(DiffHost host) => host.Window.GetPlatformSettings()!.HotkeyConfiguration;

    /// <summary>The headless platform's clipboard, which lives in the test process.</summary>
    private static IClipboard ClipboardOf(Visual view) =>
        TopLevel.GetTopLevel(view)?.Clipboard ?? throw new InvalidOperationException("The window has no clipboard.");

    /// <summary>Presses a gesture's key with its modifiers.</summary>
    private static void Press(DiffHost host, KeyGesture gesture) => host.PressKey(gesture.Key, Raw(gesture.KeyModifiers));

    /// <summary>A gesture held for three repeats: three key downs, as the system's auto-repeat sends them, and one key up.</summary>
    private static void Hold(DiffHost host, KeyGesture gesture)
    {
        var modifiers = Raw(gesture.KeyModifiers);
        for (var repeat = 0; repeat < 3; repeat++)
        {
            host.Window.KeyPress(gesture.Key, modifiers, PhysicalKey.None, null);
        }

        host.Window.KeyRelease(gesture.Key, modifiers, PhysicalKey.None, null);
        DiffHost.Settle();
    }

    /// <summary>Text typed with its key held for three repeats.</summary>
    private static void TypeHeld(DiffHost host, string typed)
    {
        for (var repeat = 0; repeat < 3; repeat++)
        {
            host.Window.KeyTextInput(typed);
        }

        DiffHost.Settle();
    }

    private static RawInputModifiers Raw(KeyModifiers modifiers)
    {
        var raw = RawInputModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            raw |= RawInputModifiers.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            raw |= RawInputModifiers.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            raw |= RawInputModifiers.Alt;
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            raw |= RawInputModifiers.Meta;
        }

        return raw;
    }

    /// <summary>The text as AvaloniaEdit copies it: every line end the system's.</summary>
    private static string WithSystemLineEnds(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\n", Environment.NewLine, StringComparison.Ordinal);

    private static string Escaped(string text) => string.Concat(text.Select(c => c < ' ' ? $"\\u{(int)c:x4}" : c.ToString()));
}
