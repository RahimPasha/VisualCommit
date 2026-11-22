using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.Views;

namespace VisualCommit.App.Controls;

/// <summary>
/// A long text shown whole and read-only (D432): the license and the third-party notices. AvaloniaEdit
/// lays out only the lines in view, so some 200 KB open at once, where a text block would stall the
/// window. The text is wrapped, in the code font at 11.5, with no caret drawn; Tab and Shift+Tab move
/// the focus on (D426). The keys scroll it as a reader expects: Up and Down a line, PageUp and PageDown
/// the view less a line, Home and End (also with the platform's command key, Ctrl or Cmd) to its start
/// and its end, End showing the true last line once the wrapped lines below are measured. The
/// platform's copy key copies a selection, and nothing without one; its select-all selects the whole
/// text. With no caret shown, the text takes no key that would move, extend or use one, or type: such a
/// key is taken and does nothing (no scroll, no change of the text or the selection). Enter, with or
/// without modifiers, is not the text's: it neither scrolls nor is taken, so a plain Enter is the
/// dialog's (its Close). Its UI Automation peer is a document whose value is the text, named by
/// <c>AutomationProperties.Name</c>.
/// </summary>
public sealed class DocumentTextView : CodeTextEditor
{
    /// <summary>
    /// The editor's commands whose own keys stay the editor's: copy and select-all (which the view
    /// also runs on the platform's keys), and cut, paste, undo and redo, which do nothing in a
    /// read-only text.
    /// </summary>
    private static readonly RoutedCommand[] KeptCommands =
    [
        ApplicationCommands.Copy,
        ApplicationCommands.SelectAll,
        ApplicationCommands.Cut,
        ApplicationCommands.Paste,
        ApplicationCommands.Undo,
        ApplicationCommands.Redo,
    ];

    /// <summary>The size of the code font in the view.</summary>
    public const double TextFontSize = 11.5;

    private ScrollViewer? _scrollViewer;

    /// <summary>End was pressed: the view keeps to the text's end while the measured lines change the extent.</summary>
    private bool _keepAtEnd;

    public DocumentTextView()
        : base(new TextArea())
    {
        IsReadOnly = true;
        WordWrap = true;
        ShowLineNumbers = false;
        FontSize = TextFontSize;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Options = new TextEditorOptions
        {
            EnableHyperlinks = false,
            EnableEmailHyperlinks = false,
            EnableTextDragDrop = false,
            EnableRectangularSelection = false,
            AllowScrollBelowDocument = false,
            HighlightCurrentLine = false,

            // Ctrl+C copies a selection only: without one, AvaloniaEdit would copy the hidden caret's line.
            CutCopyWholeLine = false,

            // Nothing is typed into a read-only text.
            EnableImeSupport = false,

            // Read-only: Tab and Shift+Tab move the focus on, as in any other view (D426).
            AcceptsTab = false,
        };

        var area = TextArea;
        area.SelectionBorder = null;
        area.SelectionForeground = null;
        area.SelectionCornerRadius = 0;
        area.CaretBrush = Brushes.Transparent;
        RemoveEnterBindings(area.DefaultInputHandler);
        AddHandler(KeyDownEvent, OnKeyDownInText, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInputInText, RoutingStrategies.Tunnel);
        ActualThemeVariantChanged += (_, _) => UseSelectionBrush();
    }

    /// <summary>The scroll viewer's extent; empty before the template is applied.</summary>
    public Size ScrollExtent => _scrollViewer?.Extent ?? default;

    /// <summary>The scroll viewer's viewport; empty before the template is applied.</summary>
    public Size ScrollViewport => _scrollViewer?.Viewport ?? default;

    /// <summary>The height of one line of the code font in the view.</summary>
    public double LineHeight => TextArea.TextView.DefaultLineHeight;

    /// <summary>Shows <paramref name="text"/> from its top, with nothing selected.</summary>
    public void Show(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _keepAtEnd = false;
        Document = new TextDocument(text);
        TextArea.Caret.Offset = 0;
        ScrollTo(0, 0);
    }

    /// <summary>Gives the keyboard focus to the text, without the keyboard's frame (it has no caret to show either).</summary>
    public bool FocusText() => TextArea.Focus(NavigationMethod.Unspecified);

    protected override AutomationPeer OnCreateAutomationPeer() => new DocumentTextAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UseSelectionBrush();
    }

    /// <summary>A selection in the theme's selection colour, the text keeping its own colour (the editor's default is the accent).</summary>
    private void UseSelectionBrush()
    {
        if (this.TryFindResource("VcSelectionBrush", ActualThemeVariant, out var brush) && brush is IBrush selection)
        {
            TextArea.SelectionBrush = selection;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        }

        base.OnApplyTemplate(e);
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }
    }

    /// <summary>
    /// The text's keys, taken on the way down, before the editor's own bindings see them. The reading
    /// keys scroll, so that the hidden caret does not move a line at a time: Up, Down, PageUp and
    /// PageDown alone; Home and End alone or with the platform's command key go to the start or the
    /// end. The platform's copy and select-all (Avalonia's <c>HotkeyConfiguration</c>, where
    /// AvaloniaEdit picks its own keys by the operating system) run the editor's commands. A key that
    /// would move, extend or use the hidden caret, or edit, is taken and does nothing
    /// (<see cref="MovesOrUsesTheCaret"/>). Every other key is the editor's (cut, paste, undo and redo,
    /// which do nothing in a read-only text) or goes on (Tab, Esc, Enter).
    /// </summary>
    private void OnKeyDownInText(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _scrollViewer is not { } viewer)
        {
            return;
        }

        var hotkeys = this.GetPlatformSettings()?.HotkeyConfiguration;
        var command = ShortcutKeys.CommandModifierOf(hotkeys);
        var modifiers = e.KeyModifiers;
        var line = LineHeight;
        var page = Math.Max(line, viewer.Viewport.Height - line);
        switch (e.Key)
        {
            case Key.Up when modifiers == KeyModifiers.None:
                ScrollBy(-line);
                break;
            case Key.Down when modifiers == KeyModifiers.None:
                ScrollBy(line);
                break;
            case Key.PageUp when modifiers == KeyModifiers.None:
                ScrollBy(-page);
                break;
            case Key.PageDown when modifiers == KeyModifiers.None:
                ScrollBy(page);
                break;
            case Key.Home when modifiers == KeyModifiers.None || modifiers == command:
                _keepAtEnd = false;
                TextArea.ClearSelection();
                TextArea.Caret.Offset = 0;
                ScrollTo(0, 0);
                break;
            case Key.End when modifiers == KeyModifiers.None || modifiers == command:
                TextArea.ClearSelection();
                TextArea.Caret.Offset = Document?.TextLength ?? 0;
                _keepAtEnd = true;
                ScrollToTextEnd();
                break;
            default:
                if (hotkeys?.Copy.Any(gesture => gesture.Matches(e)) == true)
                {
                    // A selection only (CutCopyWholeLine is off): without one, nothing.
                    ApplicationCommands.Copy.Execute(null, TextArea);
                }
                else if (hotkeys?.SelectAll.Any(gesture => gesture.Matches(e)) == true)
                {
                    ApplicationCommands.SelectAll.Execute(null, TextArea);
                }
                else if (!MovesOrUsesTheCaret(e))
                {
                    return;
                }

                break;
        }

        e.Handled = true;
    }

    /// <summary>
    /// A key that would move, extend or use the hidden caret, or edit: Left, Right, Backspace and
    /// Delete with any modifiers; Up, Down, Home, End, PageUp and PageDown with Shift and any others;
    /// and every other key the editor's text area binds, its own key bindings and its commands' keys,
    /// but those of the commands it keeps. Each of these brought the caret into view, a jump of the
    /// text, or moved, extended or replaced the selection; Avalonia's scroll viewer paged the view by
    /// its whole height on PageUp and PageDown with Shift and another modifier.
    /// </summary>
    private bool MovesOrUsesTheCaret(KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Back or Key.Delete)
        {
            return true;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key is Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        {
            return true;
        }

        return KeyBindingsOf(TextArea.DefaultInputHandler).Any(binding => binding.Gesture?.Matches(e) == true)
            || TextArea.CommandBindings.Any(binding => binding.Command.Gesture?.Matches(e) == true && !KeptCommands.Contains(binding.Command));
    }

    /// <summary>The key bindings of an input handler and of the handlers nested in it.</summary>
    private static IEnumerable<KeyBinding> KeyBindingsOf(ITextAreaInputHandler handler) =>
        handler is TextAreaInputHandler input
            ? input.KeyBindings.Concat(input.NestedInputHandlers.SelectMany(KeyBindingsOf))
            : [];

    /// <summary>
    /// Typed text, Space among it, is taken and dropped: the text area would type nothing into the
    /// read-only text, but bring the hidden caret into view, a jump of the text. What the text area
    /// leaves (no text, Esc, Backspace and Delete as characters) goes on as before.
    /// </summary>
    private static void OnTextInputInText(object? sender, TextInputEventArgs e)
    {
        if (!e.Handled && !string.IsNullOrEmpty(e.Text) && e.Text is not ("\u001b" or "\b" or "\u007f"))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// The editor takes no Enter, with or without modifiers. Read-only, it types nothing, but its
    /// paragraph and line breaks (Enter, Shift+Enter) still bring the hidden caret into view, a jump
    /// back to the top or the end, and they take the key: a plain Enter goes on to the dialog, whose
    /// own Enter is its Close. The bindings are this text area's own copies; every other editor keeps
    /// AvaloniaEdit's.
    /// </summary>
    private static void RemoveEnterBindings(TextAreaInputHandler handler)
    {
        foreach (var binding in handler.KeyBindings.Where(binding => binding.Gesture?.Key == Key.Enter).ToList())
        {
            handler.KeyBindings.Remove(binding);
        }

        foreach (var nested in handler.NestedInputHandlers.OfType<TextAreaInputHandler>())
        {
            RemoveEnterBindings(nested);
        }
    }

    private void ScrollBy(double delta)
    {
        _keepAtEnd = false;
        ScrollTo(null, Math.Max(0, ScrollOffset.Y + delta));
    }

    /// <summary>The scroll viewer's end: it clamps an offset past the extent to the last place.</summary>
    private void ScrollToTextEnd()
    {
        if (_scrollViewer is { } viewer)
        {
            ScrollTo(null, Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height));
        }
    }

    /// <summary>
    /// After End, the wrapped lines that come into view are measured and the extent grows or shrinks:
    /// the view goes on to the new end, until the reader scrolls away from it.
    /// </summary>
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!_keepAtEnd || _scrollViewer is not { } viewer)
        {
            return;
        }

        // The reader moved up (the wheel, the scroll bar's thumb). A long move measures the lines it
        // brings into view, so the extent may grow in the same pass; the view's own moves to its end
        // go up only when the extent shrank or the viewport changed.
        if (e.OffsetDelta.Y < 0 && e.ExtentDelta.Y >= 0 && e.ViewportDelta.Y == 0)
        {
            _keepAtEnd = false;
            return;
        }

        var end = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
        if (e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0)
        {
            ScrollToTextEnd();
        }
        else if (viewer.Offset.Y < end - 1)
        {
            _keepAtEnd = false;
        }
    }

    /// <summary>The text as UI Automation sees it: a document whose read-only value is the text.</summary>
    private sealed class DocumentTextAutomationPeer(DocumentTextView owner) : ControlAutomationPeer(owner), IValueProvider
    {
        public bool IsReadOnly => true;

        public string? Value => owner.Document?.Text;

        public void SetValue(string? value) => throw new InvalidOperationException("The text is read-only.");

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
