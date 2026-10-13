using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using VisualCommit.App.Controls.Diff;
using VisualCommit.Core.Conflicts;

namespace VisualCommit.App.Controls.Conflicts;

/// <summary>
/// The conflict resolver's result (docs/phase-4-design.md 11.3, 11.5, 11.10): the file itself,
/// editable, in the code font, with one number column. An unresolved block shows git's markers as
/// text: its marker lines on <c>VcConflictMarkerBrush</c> in <c>VcWarningBrush</c>, its ours
/// section on <c>VcConflictOursBrush</c>, a <c>diff3</c> base section on <c>VcConflictBaseBrush</c>,
/// its theirs section on <c>VcConflictTheirsBrush</c>; the blocks are found again after every
/// change, as the resolver's model finds them (<see cref="ConflictText"/>). A chosen or typed line
/// has no tint (7.3, 14.3).
/// </summary>
/// <remarks>
/// <para>
/// AvaloniaEdit's caret is not drawn (its blinking cannot be stopped, and a blinking caret would
/// make pictures differ from run to run); the editor draws its own: a 1-pixel line in the main
/// text colour, one row high, only while the text has the keyboard focus, blinking every
/// <see cref="CaretBlinkMilliseconds"/> only while <see cref="CaretBlinks"/> is true.
/// </para>
/// <para>
/// Undo and redo are the gestures of Avalonia's <c>HotkeyConfiguration</c> (the platform's own,
/// and the headless driver's), taken in the tunnel phase and applied to the document; AvaloniaEdit's
/// own bindings, which pick the modifier by the operating system, are removed. Tab types a tab;
/// Ctrl+Tab is left to the window's keyboard navigation, which moves the focus out. Esc is never
/// handled here: the window's chain decides.
/// </para>
/// <para>
/// <see cref="IsModified"/> compares the text with the document's original: its text when the
/// editor first showed it, or when <see cref="MarkOriginal"/> was last called. Each document keeps
/// its own, so a document shown again (another tab's) keeps its state.
/// </para>
/// </remarks>
public sealed class ConflictResultEditor : CodeTextEditor
{
    /// <summary>The caret's blink: shown and hidden this long each.</summary>
    public const int CaretBlinkMilliseconds = 530;

    /// <summary>The environment variable that turns blinking off when it is <c>0</c> as the type is first used (the real-window pass sets it).</summary>
    public const string CaretBlinkVariable = "VISUALCOMMIT_CARET_BLINK";

    private static readonly ConditionalWeakTable<TextDocument, string> Originals = [];

    private readonly ResultGutter _gutter;
    private readonly ResultCaretLayer _caret;
    private ConflictTint[] _tints = [];
    private DiffPalette? _palette;
    private int _markerSize = ConflictText.DefaultMarkerSize;
    private (int Line, int RowsAbove)? _pendingReveal;
    private Vector? _pendingOffset;

    static ConflictResultEditor()
    {
        CaretBlinks = Environment.GetEnvironmentVariable(CaretBlinkVariable) != "0";
    }

    public ConflictResultEditor()
        : base(new ResultTextArea())
    {
        IsReadOnly = false;
        WordWrap = false;
        ShowLineNumbers = false;
        FontSize = DiffPalette.CodeFontSize;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Options = new TextEditorOptions
        {
            // The font's own line height, 15.84 at 12 pixels, as the diff's rows.
            LineHeightFactor = 1,
            EnableHyperlinks = false,
            EnableEmailHyperlinks = false,
            EnableTextDragDrop = false,
            EnableRectangularSelection = false,
            AllowScrollBelowDocument = false,
            HighlightCurrentLine = false,
            EnableImeSupport = true,
        };

        var area = TextArea;
        area.SelectionBorder = null;
        area.SelectionForeground = null;
        area.SelectionCornerRadius = 0;
        area.CaretBrush = Brushes.Transparent;
        RemoveOwnUndoBindings(area);

        _gutter = new ResultGutter(this);
        area.LeftMargins.Add(_gutter);
        TextView.BackgroundRenderers.Add(new ResultTintRenderer(this));
        TextView.LineTransformers.Add(new ResultMarkerColorizer(this));
        _caret = new ResultCaretLayer(this);
        TextView.InsertLayer(_caret, KnownLayer.Caret, LayerInsertionPosition.Above);

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        area.Caret.PositionChanged += (_, _) => _caret.Restart();
        area.GotFocus += (_, _) => _caret.Restart();
        area.LostFocus += (_, _) => _caret.Restart();
        TextView.ScrollOffsetChanged += (_, _) => _caret.InvalidateVisual();
        TextView.VisualLinesChanged += (_, _) => _caret.InvalidateVisual();
        DocumentChanged += (_, _) => OnDocumentShown();
        TextChanged += (_, _) => Reparse();
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        LayoutUpdated += (_, _) => TryRevealPending();
        OnDocumentShown();
    }

    /// <summary>
    /// Whether the caret blinks. True unless <see cref="CaretBlinkVariable"/> was <c>0</c> when the
    /// type was first used; the headless driver sets it false, so that two pictures of the same
    /// state are the same.
    /// </summary>
    public static bool CaretBlinks { get; set; }

    /// <summary>The marker size of the file's blocks (git's <c>conflict-marker-size</c>, 7 unless set).</summary>
    public int MarkerSize
    {
        get => _markerSize;
        set
        {
            _markerSize = value;
            Reparse();
        }
    }

    /// <summary>Whether the text differs from the document's original (see the remarks). Not AvaloniaEdit's undo-based flag.</summary>
    public new bool IsModified => Document is { } document && !string.Equals(document.Text, OriginalOf(document), StringComparison.Ordinal);

    /// <summary>The caret's line and column, both from 1.</summary>
    public TextLocation CaretPosition => TextArea.Caret.Location;

    internal TextView TextView => TextArea.TextView;

    internal DiffPalette Palette => _palette ??= DiffPalette.Resolve(this);

    /// <summary>Makes the current text the document's original: <see cref="IsModified"/> is false until it changes.</summary>
    public void MarkOriginal()
    {
        if (Document is { } document)
        {
            Originals.AddOrUpdate(document, document.Text);
        }
    }

    /// <summary>The tint of line <paramref name="line"/> (from 1); none outside the document.</summary>
    public ConflictTint TintAt(int line) => line >= 1 && line <= _tints.Length ? _tints[line - 1] : ConflictTint.None;

    /// <summary>
    /// Where the caret goes for line <paramref name="line"/> and column <paramref name="column"/>
    /// (both from 1): the left edge of that column, halfway down the line, in this control's
    /// coordinates; null when the line is not laid out in view.
    /// </summary>
    public Point? PointAt(int line, int column)
    {
        var textView = TextView;
        if (Document is not { } document || line < 1 || line > document.LineCount || !textView.VisualLinesValid || textView.GetVisualLine(line) is null)
        {
            return null;
        }

        var point = textView.GetVisualPosition(new TextViewPosition(line, column), VisualYPosition.LineMiddle) - textView.ScrollOffset;
        if (point.X < 0 || point.Y < 0 || point.X > textView.Bounds.Width || point.Y > textView.Bounds.Height)
        {
            return null;
        }

        return textView.TranslatePoint(point, this);
    }

    /// <summary>
    /// Scrolls so that line <paramref name="line"/> (from 1) is the row
    /// <paramref name="rowsAbove"/> + 1 of the view, with fewer rows above it near the start and
    /// the text stopping at its end (11.5: the third row). Done once the text is laid out.
    /// </summary>
    public void RevealLine(int line, int rowsAbove = 2)
    {
        _pendingReveal = (line, rowsAbove);
        _pendingOffset = null;
        TryRevealPending();
    }

    /// <summary>Scrolls to <paramref name="offset"/> once the text is laid out (a resolver shown again keeps its place).</summary>
    public void ScrollToOffset(Vector offset)
    {
        _pendingOffset = offset;
        _pendingReveal = null;
        TryRevealPending();
    }

    /// <summary>Replaces characters [<paramref name="start"/>, <paramref name="start"/> + <paramref name="length"/>) with <paramref name="text"/> as one undo step, and puts the caret at its start.</summary>
    public void ReplaceRange(int start, int length, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var document = Document ?? throw new InvalidOperationException("The editor has no document.");
        using (document.RunUpdate())
        {
            document.Replace(start, length, text);
        }

        TextArea.ClearSelection();
        TextArea.Caret.Offset = start;
        TextArea.Caret.BringCaretToView();
    }

    /// <summary>
    /// Undoes the last change, if any, and brings the caret into view. On the document's undo
    /// stack itself: AvaloniaEdit's <c>TextEditor.Undo</c> goes through its own command binding,
    /// which the editor removes.
    /// </summary>
    public new bool Undo()
    {
        if (Document?.UndoStack is not { CanUndo: true } stack)
        {
            return false;
        }

        stack.Undo();
        TextArea.Caret.BringCaretToView();
        return true;
    }

    /// <summary>Redoes the last undone change, if any, and brings the caret into view.</summary>
    public new bool Redo()
    {
        if (Document?.UndoStack is not { CanRedo: true } stack)
        {
            return false;
        }

        stack.Redo();
        TextArea.Caret.BringCaretToView();
        return true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ResultAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        OnThemeChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _caret.Stop();
    }

    /// <summary>
    /// AvaloniaEdit binds undo and redo to Ctrl (Cmd on macOS) by the operating system, which the
    /// headless driver's <c>HotkeyConfiguration</c> need not match; the editor's own gestures
    /// replace them (<see cref="OnPreviewKeyDown"/>).
    /// </summary>
    private static void RemoveOwnUndoBindings(TextArea area)
    {
        var bindings = area.DefaultInputHandler.CommandBindings;
        foreach (var binding in bindings.Where(binding => binding.Command == ApplicationCommands.Undo || binding.Command == ApplicationCommands.Redo).ToList())
        {
            bindings.Remove(binding);
        }
    }

    private static string OriginalOf(TextDocument document) => Originals.GetValue(document, shown => shown.Text);

    /// <summary>Undo and redo with the platform's gestures, before the text area sees the keys.</summary>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var keys = this.GetPlatformSettings()?.HotkeyConfiguration;
        if (keys is null)
        {
            return;
        }

        if (keys.Undo.Any(gesture => gesture.Matches(e)))
        {
            Undo();
            e.Handled = true;
        }
        else if (keys.Redo.Any(gesture => gesture.Matches(e)))
        {
            Redo();
            e.Handled = true;
        }
    }

    /// <summary>A document is shown: its original is known from now on, and its blocks are found.</summary>
    private void OnDocumentShown()
    {
        if (Document is { } document)
        {
            _ = OriginalOf(document);
        }

        Reparse();
    }

    /// <summary>
    /// The tint of each of the document's lines (from 0): an unresolved block's marker lines, ours,
    /// base and theirs sections; every other line none. The lines are the editor's own (a lone
    /// carriage return ends one here), parsed by <see cref="ConflictText"/>.
    /// </summary>
    public static ConflictTint[] TintsOf(IReadOnlyList<string> lines, int markerSize = ConflictText.DefaultMarkerSize)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var tints = new ConflictTint[lines.Count];
        var text = ConflictText.Parse(string.Join('\n', lines), markerSize);
        foreach (var block in text.Blocks)
        {
            var oursEnd = block.BaseLine ?? block.SeparatorLine;
            Fill(block.StartLine + 1, oursEnd, ConflictTint.Ours);
            if (block.BaseLine is { } baseLine)
            {
                Fill(baseLine + 1, block.SeparatorLine, ConflictTint.Base);
            }

            Fill(block.SeparatorLine + 1, block.EndLine, ConflictTint.Theirs);
            foreach (var marker in new[] { block.StartLine, block.BaseLine, block.SeparatorLine, block.EndLine })
            {
                if (marker is { } line && line < tints.Length)
                {
                    tints[line] = ConflictTint.Marker;
                }
            }
        }

        return tints;

        void Fill(int from, int to, ConflictTint tint)
        {
            for (var line = from; line < to && line < tints.Length; line++)
            {
                tints[line] = tint;
            }
        }
    }

    /// <summary>Scrolls as <see cref="RevealLine"/> or <see cref="ScrollToOffset"/> asked, once the text is laid out.</summary>
    private void TryRevealPending()
    {
        if ((_pendingReveal is null && _pendingOffset is null) || Document is not { } document || !(TextView.Bounds.Height > 0) || !TextView.VisualLinesValid)
        {
            return;
        }

        if (_pendingReveal is var (line, rowsAbove))
        {
            var first = Math.Clamp(line - rowsAbove, 1, Math.Max(1, document.LineCount));
            ScrollTo(0, TextView.GetVisualTopByDocumentLine(first));
        }
        else if (_pendingOffset is { } offset)
        {
            ScrollTo(offset.X, offset.Y);
        }

        _pendingReveal = null;
        _pendingOffset = null;
    }

    /// <summary>Finds the blocks again and redraws what their tints changed.</summary>
    private void Reparse()
    {
        var document = Document;
        var lines = document is null ? [] : document.Lines.Select(document.GetText).ToList();
        var tints = TintsOf(lines, _markerSize);
        var markersMoved = tints.Length != _tints.Length
            || tints.Where((tint, line) => (tint == ConflictTint.Marker) != (_tints[line] == ConflictTint.Marker)).Any();
        _tints = tints;

        // The colorizer's marker text needs the lines built again; the tints only a new background.
        if (markersMoved)
        {
            TextView.Redraw();
        }
        else
        {
            TextView.InvalidateLayer(KnownLayer.Background);
        }

        _gutter.UpdateWidth();
    }

    private void OnThemeChanged()
    {
        _palette = null;
        var palette = Palette;
        Background = palette.Background;
        Foreground = palette.TextPrimary;
        FontFamily = palette.CodeFont;
        TextArea.SelectionBrush = palette.Selection;
        TextView.Redraw();
        _gutter.UpdateWidth();
        _caret.InvalidateVisual();
    }

    /// <summary>The result as UI Automation sees it: a document whose read-only value is the text, so that the real-window pass can read it.</summary>
    private sealed class ResultAutomationPeer(ConflictResultEditor owner) : ControlAutomationPeer(owner), IValueProvider
    {
        public bool IsReadOnly => true;

        public string? Value => owner.Document?.Text;

        public void SetValue(string? value) => throw new InvalidOperationException("The result's text is read-only to UI Automation.");

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// AvaloniaEdit's text area, with its look, that leaves Ctrl+Tab to the window: AvaloniaEdit takes
/// Tab with any modifier but Shift as a tab to type.
/// </summary>
internal sealed class ResultTextArea : TextArea
{
    public ResultTextArea()
        : base(new TextView())
    {
    }

    protected override Type StyleKeyOverride => typeof(TextArea);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Tab && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            // Left unhandled, the window's keyboard navigation moves the focus out.
            return;
        }

        base.OnKeyDown(e);
    }
}

/// <summary>
/// The result's gutter: one number column 8 + 7.2 × d + 8 wide for the d digits of the last line's
/// number (at least 2), the numbers right-aligned 8 from its right edge in the secondary colour, on
/// each line's tint.
/// </summary>
internal sealed class ResultGutter(ConflictResultEditor editor) : AbstractMargin
{
    private double _width;

    /// <summary>Sizes the column for the document's last line number.</summary>
    public void UpdateWidth()
    {
        var digits = Math.Max(2, (editor.Document?.LineCount ?? 1).ToString(CultureInfo.InvariantCulture).Length);
        var width = (2 * DiffGutter.NumberPadding) + (DigitWidth() * digits);
        if (width != _width)
        {
            _width = width;
            InvalidateMeasure();
        }

        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(_width, 0);

    public override void Render(DrawingContext context)
    {
        var textView = TextView;
        if (textView is null || !textView.VisualLinesValid)
        {
            return;
        }

        var palette = editor.Palette;
        var typeface = new Typeface(palette.CodeFont);
        foreach (var line in textView.VisualLines)
        {
            var number = line.FirstDocumentLine.LineNumber;
            var top = Math.Round(line.VisualTop - textView.VerticalOffset);
            var bottom = Math.Round(line.VisualTop + line.Height - textView.VerticalOffset);
            if (ResultTintRenderer.BrushOf(editor.TintAt(number), palette) is { } tint)
            {
                context.FillRectangle(tint, new Rect(0, top, Bounds.Width, bottom - top));
            }

            var text = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, DiffPalette.CodeFontSize, palette.TextSecondary);
            context.DrawText(text, new Point(Bounds.Width - DiffGutter.NumberPadding - text.WidthIncludingTrailingWhitespace, line.VisualTop - textView.VerticalOffset));
        }
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView is not null)
        {
            oldTextView.ScrollOffsetChanged -= OnTextViewScrolled;
        }

        base.OnTextViewChanged(oldTextView!, newTextView);
        if (newTextView is not null)
        {
            newTextView.ScrollOffsetChanged += OnTextViewScrolled;
        }
    }

    protected override void OnTextViewVisualLinesChanged()
    {
        base.OnTextViewVisualLinesChanged();
        InvalidateVisual();
    }

    private void OnTextViewScrolled(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>The advance of a digit in the code font: 7.2 at 12 pixels in JetBrains Mono.</summary>
    private double DigitWidth() =>
        new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(editor.Palette.CodeFont), DiffPalette.CodeFontSize, Brushes.Transparent).WidthIncludingTrailingWhitespace;
}

/// <summary>Draws each line's tint behind its text, across the text column's whole width.</summary>
internal sealed class ResultTintRenderer(ConflictResultEditor editor) : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Background;

    /// <summary>The brush of a tint, or null for none.</summary>
    public static IBrush? BrushOf(ConflictTint tint, DiffPalette palette) => tint switch
    {
        ConflictTint.Ours => palette.ConflictOurs,
        ConflictTint.Theirs => palette.ConflictTheirs,
        ConflictTint.Base => palette.ConflictBase,
        ConflictTint.Marker => palette.ConflictMarker,
        ConflictTint.Used => palette.ConflictUsed,
        _ => null,
    };

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
        {
            return;
        }

        var palette = editor.Palette;
        foreach (var line in textView.VisualLines)
        {
            if (BrushOf(editor.TintAt(line.FirstDocumentLine.LineNumber), palette) is not { } brush)
            {
                continue;
            }

            var top = Math.Round(line.VisualTop - textView.VerticalOffset);
            var bottom = Math.Round(line.VisualTop + line.Height - textView.VerticalOffset);
            drawingContext.FillRectangle(brush, new Rect(0, top, textView.Bounds.Width, bottom - top));
        }
    }
}

/// <summary>Draws the text of an unresolved block's marker lines in the warning colour.</summary>
internal sealed class ResultMarkerColorizer(ConflictResultEditor editor) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.Length > 0 && editor.TintAt(line.LineNumber) == ConflictTint.Marker)
        {
            var warning = editor.Palette.Warning;
            ChangeLinePart(line.Offset, line.EndOffset, element => element.TextRunProperties.SetForegroundBrush(warning));
        }
    }
}

/// <summary>
/// The result's caret, drawn over the text: a 1-pixel line in the main text colour, one row high,
/// at the caret's position, only while the text area has the keyboard focus. While
/// <see cref="ConflictResultEditor.CaretBlinks"/> is true it is shown and hidden every
/// <see cref="ConflictResultEditor.CaretBlinkMilliseconds"/>, shown again at once whenever the
/// caret moves.
/// </summary>
internal sealed class ResultCaretLayer : Control
{
    private readonly ConflictResultEditor _editor;
    private readonly DispatcherTimer _timer;
    private bool _blinkShown = true;

    public ResultCaretLayer(ConflictResultEditor editor)
    {
        _editor = editor;
        IsHitTestVisible = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ConflictResultEditor.CaretBlinkMilliseconds) };
        _timer.Tick += (_, _) =>
        {
            _blinkShown = !_blinkShown;
            InvalidateVisual();
        };
    }

    /// <summary>Whether the caret is drawn now.</summary>
    public bool IsShown => _editor.TextArea.IsFocused && _blinkShown;

    /// <summary>Shows the caret at once and starts its blink again (or stops it: no focus, or blinking off).</summary>
    public void Restart()
    {
        _blinkShown = true;
        _timer.Stop();
        if (_editor.TextArea.IsFocused && ConflictResultEditor.CaretBlinks && TopLevel.GetTopLevel(_editor) is not null)
        {
            _timer.Start();
        }

        InvalidateVisual();
    }

    /// <summary>Stops the blink: the editor left the window.</summary>
    public void Stop()
    {
        _timer.Stop();
        _blinkShown = true;
    }

    /// <summary>The caret's line in the text view's coordinates, on whole pixels; null when its line is not laid out in view.</summary>
    public Rect? CaretBounds()
    {
        var textView = _editor.TextView;
        var caret = _editor.TextArea.Caret;
        if (_editor.Document is null || !textView.VisualLinesValid || textView.GetVisualLine(caret.Line) is null)
        {
            return null;
        }

        var top = textView.GetVisualPosition(caret.Position, VisualYPosition.LineTop) - textView.ScrollOffset;
        var bottom = textView.GetVisualPosition(caret.Position, VisualYPosition.LineBottom) - textView.ScrollOffset;
        var x = Math.Round(top.X);
        return new Rect(x, Math.Round(top.Y), 1, Math.Round(bottom.Y) - Math.Round(top.Y));
    }

    public override void Render(DrawingContext context)
    {
        if (IsShown && CaretBounds() is { } bounds)
        {
            context.FillRectangle(_editor.Palette.TextPrimary, bounds);
        }
    }
}
