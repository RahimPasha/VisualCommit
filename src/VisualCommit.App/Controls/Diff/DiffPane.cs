using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using VisualCommit.Core.Diff;

namespace VisualCommit.App.Controls.Diff;

/// <summary>Which part of the diff a pane shows.</summary>
internal enum PaneSide
{
    /// <summary>The only pane of the inline mode: every row's one cell.</summary>
    Inline,

    /// <summary>Side by side, the old file: each row's left cell.</summary>
    Left,

    /// <summary>Side by side, the new file: each row's right cell.</summary>
    Right,
}

/// <summary>
/// One column of the diff view: an AvaloniaEdit editor whose document holds one line per display
/// row, with the gutter as a margin, the row backgrounds, selection and word highlights as a
/// background renderer, syntax colours as a colorizer, and the hunk headers as a layer of real
/// controls over the text. The inline mode has one pane; side by side has two.
/// </summary>
internal sealed class DiffPane
{
    /// <summary>The height of a hunk header row.</summary>
    public const double HeaderHeight = 26;

    // Characters that would end a document line, and what the document shows instead.
    private const char CarriageReturn = (char)0x0D;
    private const char LineFeed = (char)0x0A;
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;
    private const char CarriageReturnSymbol = (char)0x240D;
    private const char LineFeedSymbol = (char)0x240A;
    private const char ReplacementCharacter = (char)0xFFFD;

    private readonly DiffTextView _owner;
    private bool _settingTextSelection;

    public DiffPane(DiffTextView owner)
    {
        _owner = owner;
        Editor = new DiffTextEditor
        {
            IsReadOnly = true,
            WordWrap = false,
            ShowLineNumbers = false,
            FontSize = DiffPalette.CodeFontSize,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Options = new TextEditorOptions
            {
                // AvaloniaEdit's default factor of 1.16 would make rows 18.37 high; the report
                // asks for the font's own line height, 15.84 at 12 pixels.
                LineHeightFactor = 1,
                EnableHyperlinks = false,
                EnableEmailHyperlinks = false,
                EnableTextDragDrop = false,
                EnableRectangularSelection = false,
                AllowScrollBelowDocument = false,
                HighlightCurrentLine = false,
                CutCopyWholeLine = false,
                EnableImeSupport = false,
            },
        };

        var area = Editor.TextArea;

        // AvaloniaEdit's own selection and caret are not drawn: the selected rows are (below), and
        // a blinking caret would make screenshots differ from run to run. The caret still moves,
        // so keyboard navigation scrolls.
        area.SelectionBrush = Brushes.Transparent;
        area.SelectionBorder = null;
        area.SelectionForeground = null;
        area.CaretBrush = Brushes.Transparent;

        Gutter = new DiffGutter(this);
        area.LeftMargins.Add(Gutter);
        TextView.BackgroundRenderers.Add(new DiffRowRenderer(this));
        TextView.LineTransformers.Add(new DiffColorizer(this));
        TextView.ElementGenerators.Add(new HunkHeaderGenerator(this));
        Headers = new HunkHeaderLayer(this);
        ((LayeredTextView)TextView).AddControlLayer(Headers);

        area.SelectionChanged += (_, _) => OnTextSelectionChanged();
        TextView.ScrollOffsetChanged += (_, _) => _owner.OnPaneScrolled(this);
    }

    public DiffTextView Owner => _owner;

    public DiffTextEditor Editor { get; }

    public TextView TextView => Editor.TextArea.TextView;

    public DiffGutter Gutter { get; }

    public HunkHeaderLayer Headers { get; }

    public PaneSide Side { get; private set; } = PaneSide.Inline;

    /// <summary>The rows the pane shows: the owner's, one document line each (row r is line r + 1).</summary>
    public IReadOnlyList<DiffDisplayRow> Rows { get; private set; } = [];

    /// <summary>
    /// The selected rows, from the row the selection started on (the anchor) to the row it ends
    /// on; null when nothing is selected. Header rows inside the range count for nothing.
    /// </summary>
    public (int Anchor, int Active)? Selection { get; private set; }

    /// <summary>The palette of the owner's theme.</summary>
    public DiffPalette Palette => _owner.Palette;

    /// <summary>Header rows whose visual lines have not been built yet: they are built once the pane has a size, so that the scroll extent knows their height.</summary>
    public bool HeadersPending { get; set; }

    /// <summary>The cell of a row this pane shows.</summary>
    public DiffCell? CellOf(DiffDisplayRow row) => Side == PaneSide.Right ? row.Right : row.Left;

    /// <summary>
    /// Which side's tokens colour a cell: the left pane the old side, the right the new; inline, a
    /// removed line the old side and any other line the new.
    /// </summary>
    public DiffVersion VersionOf(DiffCell cell) => Side switch
    {
        PaneSide.Left => DiffVersion.Old,
        PaneSide.Right => DiffVersion.New,
        _ => cell.Kind == DiffLineKind.Removed ? DiffVersion.Old : DiffVersion.New,
    };

    /// <summary>The row of a 1-based document line number, or null outside the rows.</summary>
    public DiffDisplayRow? RowAtLine(int lineNumber) => lineNumber >= 1 && lineNumber <= Rows.Count ? Rows[lineNumber - 1] : null;

    public bool IsSelected(int row) =>
        Selection is var (anchor, active) && row >= Math.Min(anchor, active) && row <= Math.Max(anchor, active);

    /// <summary>Shows <paramref name="rows"/> as <paramref name="side"/>: a new document, the gutter's columns for its numbers, no selection.</summary>
    public void Show(PaneSide side, IReadOnlyList<DiffDisplayRow> rows)
    {
        Side = side;
        Rows = rows;
        Selection = null;

        var document = new TextDocument(DocumentText(rows));
        document.UndoStack.SizeLimit = 0;
        _settingTextSelection = true;
        try
        {
            Editor.Document = document;
        }
        finally
        {
            _settingTextSelection = false;
        }

        HeadersPending = rows.Any(row => row.Kind == DiffRowKind.HunkHeader);
        Redraw();
    }

    /// <summary>Draws everything again: the colours, the gutter (measured again for the code font) and the headers.</summary>
    public void Redraw()
    {
        Editor.Background = Palette.Background;
        Editor.Foreground = Palette.TextPrimary;
        Editor.FontFamily = Palette.CodeFont;
        TextView.Redraw();
        Gutter.UpdateColumns();
        Headers.Rebuild();
    }

    /// <summary>Redraws the row backgrounds, after the selection changed.</summary>
    public void RedrawBackgrounds()
    {
        TextView.InvalidateLayer(KnownLayer.Background);
        Gutter.InvalidateVisual();
    }

    /// <summary>
    /// Builds the visual line of every header row, so that the scroll extent counts each header
    /// as 26 high and not as a line of text: AvaloniaEdit learns a line's height only when it
    /// builds its visual line. A diff with very many hunks is left to learn them as it scrolls.
    /// </summary>
    public void BuildHeaderLines()
    {
        HeadersPending = false;
        var document = Editor.Document;
        if (document is null)
        {
            return;
        }

        var headers = 0;
        for (var r = 0; r < Rows.Count && headers < 2_000; r++)
        {
            if (Rows[r].Kind == DiffRowKind.HunkHeader)
            {
                headers++;
                TextView.GetOrConstructVisualLine(document.GetLineByNumber(r + 1));
            }
        }
    }

    /// <summary>Selects one row: a click on its number.</summary>
    public void SelectRow(int row)
    {
        SetSelection((row, row));
        MoveCaretTo(row);
    }

    /// <summary>Extends the selection from its anchor to <paramref name="row"/>: a shift-click on a number, or a drag over the numbers.</summary>
    public void ExtendSelection(int row)
    {
        var anchor = Selection?.Anchor ?? row;
        SetSelection((anchor, row));
        MoveCaretTo(row);
    }

    /// <summary>Selects nothing, without telling the owner.</summary>
    public void ClearSelectionQuietly()
    {
        Selection = null;
        ClearTextSelection();
        RedrawBackgrounds();
    }

    /// <summary>The visual line of a row when it is laid out in view; null otherwise.</summary>
    public VisualLine? VisualLineOf(int row)
    {
        if (row < 0 || row >= Rows.Count || !TextView.VisualLinesValid)
        {
            return null;
        }

        return TextView.GetVisualLine(row + 1);
    }

    /// <summary>The top of a visual line in the text view's coordinates, on a whole pixel, as rows are drawn.</summary>
    public double TopOf(VisualLine line) => Math.Round(line.VisualTop - TextView.VerticalOffset);

    /// <summary>The bottom of a visual line in the text view's coordinates, on a whole pixel.</summary>
    public double BottomOf(VisualLine line) => Math.Round(line.VisualTop + line.Height - TextView.VerticalOffset);

    /// <summary>The row shown at a height in the text view, or -1.</summary>
    public int RowAtY(double y)
    {
        if (Editor.Document is null || !TextView.VisualLinesValid)
        {
            return -1;
        }

        foreach (var line in TextView.VisualLines)
        {
            if (y >= TopOf(line) && y < BottomOf(line))
            {
                return line.FirstDocumentLine.LineNumber - 1;
            }
        }

        return -1;
    }

    private void SetSelection((int Anchor, int Active)? selection)
    {
        Selection = selection;
        ClearTextSelection();
        RedrawBackgrounds();
        _owner.OnPaneSelectionChanged(this);
    }

    private void ClearTextSelection()
    {
        if (Editor.TextArea.Selection.IsEmpty)
        {
            return;
        }

        _settingTextSelection = true;
        try
        {
            Editor.TextArea.ClearSelection();
        }
        finally
        {
            _settingTextSelection = false;
        }
    }

    /// <summary>Puts the caret at the start of a row, so that a shift-click in the text or Shift with an arrow key goes on from there.</summary>
    private void MoveCaretTo(int row)
    {
        var document = Editor.Document;
        if (document is null || row < 0 || row >= document.LineCount)
        {
            return;
        }

        _settingTextSelection = true;
        try
        {
            Editor.TextArea.Caret.Offset = document.GetLineByNumber(row + 1).Offset;
        }
        finally
        {
            _settingTextSelection = false;
        }
    }

    /// <summary>
    /// A selection made in the text (dragging, shift-click, Shift with the keys) selects every row it
    /// touches, except a last row it only reaches the start of. A click that selects no text
    /// selects nothing.
    /// </summary>
    private void OnTextSelectionChanged()
    {
        if (_settingTextSelection)
        {
            return;
        }

        var document = Editor.Document;
        var selection = Editor.TextArea.Selection;
        if (document is null || selection.IsEmpty || selection.SurroundingSegment is not { } segment)
        {
            if (Selection is not null)
            {
                Selection = null;
                RedrawBackgrounds();
                _owner.OnPaneSelectionChanged(this);
            }

            return;
        }

        var first = document.GetLineByOffset(segment.Offset).LineNumber;
        var lastLine = document.GetLineByOffset(segment.EndOffset);
        var last = lastLine.LineNumber;
        if (last > first && segment.EndOffset == lastLine.Offset)
        {
            last--;
        }

        // The anchor is where the selection started: the end the caret is not at.
        var caretAtStart = Editor.TextArea.Caret.Offset <= segment.Offset;
        Selection = caretAtStart ? (last - 1, first - 1) : (first - 1, last - 1);
        RedrawBackgrounds();
        _owner.OnPaneSelectionChanged(this);
    }

    /// <summary>
    /// The document: one line per row. A header row holds git's header line (drawn by the header
    /// layer, not as text), a filler an empty line. Characters that AvaloniaEdit or the text
    /// formatter would take as the end of a line are replaced one for one, so that every row
    /// stays one line and columns stay where the diff has them.
    /// </summary>
    private string DocumentText(IReadOnlyList<DiffDisplayRow> rows)
    {
        var text = new StringBuilder();
        for (var r = 0; r < rows.Count; r++)
        {
            if (r > 0)
            {
                text.Append('\n');
            }

            var row = rows[r];
            var line = row.Kind == DiffRowKind.HunkHeader ? row.HeaderText ?? string.Empty : CellOf(row)?.Text ?? string.Empty;
            foreach (var c in line)
            {
                text.Append(c switch
                {
                    CarriageReturn => CarriageReturnSymbol,
                    LineFeed => LineFeedSymbol,
                    LineSeparator or ParagraphSeparator => ReplacementCharacter,
                    _ => c,
                });
            }
        }

        return text.ToString();
    }
}

/// <summary>
/// The editor of a pane: AvaloniaEdit's, with its look, and visible to UI Automation as one
/// element that the real-window pass finds by its automation id.
/// </summary>
internal sealed class DiffTextEditor : TextEditor
{
    private ScrollViewer? _scrollViewer;

    public DiffTextEditor()
        : base(new LayeredTextArea())
    {
    }

    /// <summary>The current scroll offset, from the editor's scroll viewer.</summary>
    public Vector ScrollOffset => _scrollViewer?.Offset ?? default;

    protected override Type StyleKeyOverride => typeof(TextEditor);

    /// <summary>
    /// Scrolls to an offset, either part left as it is when null. Done on the template's scroll
    /// viewer, because <see cref="TextEditor.ScrollToVerticalOffset"/> does not scroll in this
    /// version of AvaloniaEdit. The scroll viewer clamps the offset to the extent.
    /// </summary>
    public void ScrollTo(double? horizontal, double? vertical)
    {
        ApplyTemplate();
        if (_scrollViewer is { } viewer)
        {
            viewer.Offset = new Vector(horizontal ?? viewer.Offset.X, vertical ?? viewer.Offset.Y);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");

        // The editor installs its search panel with its template. The panel handles Esc even
        // while it is closed, and the diff view's parent closes the view on Esc; the diff view
        // has no search.
        SearchPanel?.Uninstall();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DiffTextAutomationPeer(this);

    private sealed class DiffTextAutomationPeer(DiffTextEditor owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>AvaloniaEdit's text area, with its look, over a <see cref="LayeredTextView"/>.</summary>
internal sealed class LayeredTextArea : AvaloniaEdit.Editing.TextArea
{
    public LayeredTextArea()
        : base(new LayeredTextView())
    {
    }

    protected override Type StyleKeyOverride => typeof(AvaloniaEdit.Editing.TextArea);
}

/// <summary>
/// AvaloniaEdit's text view, with a way to add a layer of controls that are styled like any
/// other: the text view adds a layer as a visual child only, and a control outside the logical
/// tree gets no styles or templates, so a button in it would draw nothing.
/// </summary>
internal sealed class LayeredTextView : TextView
{
    protected override Type StyleKeyOverride => typeof(TextView);

    /// <summary>Adds <paramref name="layer"/> over the text and the caret, as a visual and a logical child.</summary>
    public void AddControlLayer(Control layer)
    {
        InsertLayer(layer, KnownLayer.Caret, LayerInsertionPosition.Above);
        LogicalChildren.Add(layer);
    }
}

/// <summary>
/// A pane's gutter (a margin left of the text that does not scroll sideways): number columns, each
/// 8 + 7.2 × d + 8 wide for the d digits of its largest number (at least 2), the numbers
/// right-aligned 8 from the column's right edge; then a sign column 16 wide with "+" or "-"
/// centred. Inline has an old and a new number column, each side by side pane one. Clicking a
/// row's number selects the row; Shift extends the selection; dragging over the numbers too.
/// </summary>
internal sealed class DiffGutter : AvaloniaEdit.Editing.AbstractMargin
{
    /// <summary>Padding on each side of a number column's digits.</summary>
    public const double NumberPadding = 8;

    /// <summary>The width of the sign column.</summary>
    public const double SignWidth = 16;

    private readonly DiffPane _pane;
    private double[] _columnWidths = [];
    private bool _dragging;

    public DiffGutter(DiffPane pane)
    {
        _pane = pane;
        ClipToBounds = true;
    }

    /// <summary>The widths of the number columns, left to right.</summary>
    public IReadOnlyList<double> ColumnWidths => _columnWidths;

    /// <summary>The gutter's width: its number columns and the sign column.</summary>
    public double TotalWidth => _columnWidths.Sum() + SignWidth;

    /// <summary>Sizes the number columns for the largest number each shows.</summary>
    public void UpdateColumns()
    {
        var digitWidth = DigitWidth();
        var rows = _pane.Rows;
        if (_pane.Side == PaneSide.Inline)
        {
            var maxOld = rows.Select(row => row.Left?.OldNumber ?? 0).DefaultIfEmpty(0).Max();
            var maxNew = rows.Select(row => row.Left?.NewNumber ?? 0).DefaultIfEmpty(0).Max();
            _columnWidths = [Width(maxOld), Width(maxNew)];
        }
        else
        {
            var max = rows.Select(row => _pane.CellOf(row) is { } cell ? cell.OldNumber ?? cell.NewNumber ?? 0 : 0).DefaultIfEmpty(0).Max();
            _columnWidths = [Width(max)];
        }

        InvalidateMeasure();
        InvalidateVisual();

        double Width(int max) => (2 * NumberPadding) + (digitWidth * Math.Max(2, max.ToString(CultureInfo.InvariantCulture).Length));
    }

    /// <summary>The x of the centre of number column <paramref name="column"/>, in the gutter's coordinates.</summary>
    public double ColumnCenter(int column)
    {
        var left = 0.0;
        for (var i = 0; i < column && i < _columnWidths.Length; i++)
        {
            left += _columnWidths[i];
        }

        return left + ((column < _columnWidths.Length ? _columnWidths[column] : SignWidth) / 2);
    }

    protected override Size MeasureOverride(Size availableSize) => new(TotalWidth, 0);

    public override void Render(DrawingContext context)
    {
        var textView = TextView;
        if (textView is null || !textView.VisualLinesValid)
        {
            return;
        }

        var palette = _pane.Palette;
        var typeface = new Typeface(palette.CodeFont);
        var width = Bounds.Width;
        foreach (var line in textView.VisualLines)
        {
            if (_pane.RowAtLine(line.FirstDocumentLine.LineNumber) is not { } row)
            {
                continue;
            }

            var cell = _pane.CellOf(row);
            var top = _pane.TopOf(line);
            var bottom = _pane.BottomOf(line);
            context.FillRectangle(palette.RowBackground(row, cell), new Rect(0, top, width, bottom - top));
            if (row.Kind != DiffRowKind.Line || cell is null)
            {
                continue;
            }

            // The numbers and the sign sit on the text's baseline: same font, same size, same top.
            var textTop = line.VisualTop - textView.VerticalOffset;
            var numbers = _pane.Side switch
            {
                PaneSide.Inline => new[] { cell.OldNumber, cell.NewNumber },
                PaneSide.Left => [cell.OldNumber],
                _ => [cell.NewNumber],
            };

            var right = 0.0;
            for (var c = 0; c < numbers.Length && c < _columnWidths.Length; c++)
            {
                right += _columnWidths[c];
                if (numbers[c] is { } number)
                {
                    var text = Format(number.ToString(CultureInfo.InvariantCulture), typeface, palette.TextSecondary);
                    context.DrawText(text, new Point(right - NumberPadding - text.WidthIncludingTrailingWhitespace, textTop));
                }
            }

            var sign = cell.Kind switch
            {
                DiffLineKind.Added => "+",
                DiffLineKind.Removed => "-",
                _ => null,
            };

            if (sign is not null)
            {
                var text = Format(sign, typeface, palette.TextSecondary);
                context.DrawText(text, new Point(right + ((SignWidth - text.WidthIncludingTrailingWhitespace) / 2), textTop));
            }
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

    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;
        TextArea?.Focus();
        var row = _pane.RowAtY(point.Position.Y);
        if (row < 0 || _pane.Rows[row].Kind == DiffRowKind.HunkHeader)
        {
            return;
        }

        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Shift) != 0 && _pane.Selection is not null)
        {
            _pane.ExtendSelection(row);
        }
        else
        {
            _pane.SelectRow(row);
        }

        _dragging = true;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
        {
            return;
        }

        var row = _pane.RowAtY(e.GetPosition(this).Y);
        if (row >= 0 && _pane.Rows[row].Kind != DiffRowKind.HunkHeader && _pane.Selection is { } selection && selection.Active != row)
        {
            _pane.ExtendSelection(row);
        }
    }

    protected override void OnPointerReleased(Avalonia.Input.PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(Avalonia.Input.PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    private void OnTextViewScrolled(object? sender, EventArgs e) => InvalidateVisual();

    private FormattedText Format(string text, Typeface typeface, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, DiffPalette.CodeFontSize, brush);

    /// <summary>The advance of a digit in the code font: 7.2 at 12 pixels in JetBrains Mono.</summary>
    private double DigitWidth() =>
        Format("0", new Typeface(_pane.Palette.CodeFont), Brushes.Transparent).WidthIncludingTrailingWhitespace;
}

/// <summary>
/// Draws, behind each row's text and across the text column's whole width: the row's background,
/// the selection background on a selected row, and the word-level highlights of a changed line.
/// </summary>
internal sealed class DiffRowRenderer(DiffPane pane) : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
        {
            return;
        }

        var palette = pane.Palette;
        var width = textView.Bounds.Width;
        var words = pane.Owner.Words;
        foreach (var line in textView.VisualLines)
        {
            var index = line.FirstDocumentLine.LineNumber - 1;
            if (pane.RowAtLine(index + 1) is not { } row)
            {
                continue;
            }

            var cell = pane.CellOf(row);
            var top = pane.TopOf(line);
            var bounds = new Rect(0, top, width, pane.BottomOf(line) - top);
            drawingContext.FillRectangle(palette.RowBackground(row, cell), bounds);
            if (row.Kind == DiffRowKind.HunkHeader)
            {
                continue;
            }

            if (pane.IsSelected(index))
            {
                drawingContext.FillRectangle(palette.Selection, bounds);
            }

            if (row.Kind != DiffRowKind.Line || cell is null || !words.TryGetValue(cell.Line, out var ranges))
            {
                continue;
            }

            var brush = cell.Kind == DiffLineKind.Removed ? palette.RemovedWord : palette.AddedWord;
            foreach (var range in ranges)
            {
                var start = line.GetVisualColumn(range.Start);
                var end = line.GetVisualColumn(range.End);
                foreach (var rect in BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, line, start, end))
                {
                    drawingContext.FillRectangle(brush, new Rect(rect.X, bounds.Y, rect.Width, bounds.Height));
                }
            }
        }
    }
}

/// <summary>Colours each line's text: syntax colours and styles from its side's tokens, and the no-newline marker in the secondary colour.</summary>
internal sealed class DiffColorizer(DiffPane pane) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        if (pane.RowAtLine(line.LineNumber) is not { } row || pane.CellOf(row) is not { } cell || line.Length == 0)
        {
            return;
        }

        var palette = pane.Palette;
        if (row.Kind == DiffRowKind.NoNewline)
        {
            ChangeLinePart(line.Offset, line.EndOffset, element => element.TextRunProperties.SetForegroundBrush(palette.TextSecondary));
            return;
        }

        if (row.Kind != DiffRowKind.Line || pane.Owner.Syntax is not { } syntax)
        {
            return;
        }

        foreach (var run in syntax.RunsOf(cell.Line, pane.VersionOf(cell), palette.IsDark))
        {
            var start = line.Offset + run.Start;
            var end = Math.Min(start + run.Length, line.EndOffset);
            if (end <= start)
            {
                continue;
            }

            var style = run.Style;
            ChangeLinePart(start, end, element =>
            {
                var properties = element.TextRunProperties;
                if (style.Foreground is { } color)
                {
                    properties.SetForegroundBrush(palette.SyntaxBrush(color));
                }

                if (style.Bold || style.Italic)
                {
                    properties.SetTypeface(new Typeface(
                        properties.Typeface.FontFamily,
                        style.Italic ? FontStyle.Italic : FontStyle.Normal,
                        style.Bold ? FontWeight.Bold : FontWeight.Normal));
                }

                if (style.Underline || style.Strikethrough)
                {
                    var decorations = new TextDecorationCollection();
                    if (style.Underline)
                    {
                        decorations.AddRange(TextDecorations.Underline);
                    }

                    if (style.Strikethrough)
                    {
                        decorations.AddRange(TextDecorations.Strikethrough);
                    }

                    properties.SetTextDecorations(decorations);
                }
            });
        }
    }
}
