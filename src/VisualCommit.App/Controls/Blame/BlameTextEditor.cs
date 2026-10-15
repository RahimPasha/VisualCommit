using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using VisualCommit.App.Controls.Diff;
using VisualCommit.Core.Diff;

namespace VisualCommit.App.Controls.Blame;

/// <summary>
/// The blame page's text (docs/phase-5-design.md 4.2, 13.4: <c>BlameText</c>): the file, read-only,
/// in the code font at 12 with the font's own line height (15.84, as the diff's rows), one number
/// column (8 + 7.2 × d + 8 for the d digits of the last line's number, at least 2: 30.4 up to 99
/// lines) and the diff's syntax colours for the file's type (D64), or none for a very large file
/// (D65). No caret is drawn. Its UI Automation peer is a document whose value is the text, as the
/// conflict resolver's result's is. The gutter of runs lies left of it, a control of its own
/// (<see cref="BlameGutter"/>), which follows its lines.
/// </summary>
public sealed class BlameTextEditor : CodeTextEditor
{
    private readonly BlameNumberMargin _numbers;
    private DiffPalette? _palette;
    private DiffSyntax? _syntax;
    private string? _filePath;
    private bool _highlighting = true;

    public BlameTextEditor()
        : base(new TextArea())
    {
        IsReadOnly = true;
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
        };

        var area = TextArea;
        area.SelectionBorder = null;
        area.SelectionForeground = null;
        area.SelectionCornerRadius = 0;
        area.CaretBrush = Brushes.Transparent;
        _numbers = new BlameNumberMargin(this);
        area.LeftMargins.Add(_numbers);
        TextView.LineTransformers.Add(new BlameSyntaxColorizer(this));
        DocumentChanged += (_, _) => _numbers.UpdateWidth();
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
    }

    /// <summary>The editor's text view: the gutter of runs reads its lines' places from it.</summary>
    public TextView TextView => TextArea.TextView;

    /// <summary>
    /// Shows <paramref name="lines"/> as the file at <paramref name="path"/> (whose extension picks
    /// the grammar), with syntax colours unless <paramref name="highlighting"/> is false.
    /// </summary>
    public void Show(IReadOnlyList<string> lines, string? path, bool highlighting)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _filePath = path;
        _highlighting = highlighting;
        _syntax = highlighting ? Tokenize(lines, path) : null;
        Document = new TextDocument(string.Join('\n', lines));
        _numbers.UpdateWidth();
        TextView.Redraw();
    }

    /// <summary>The path whose grammar colours the text, as last shown.</summary>
    public string? FilePath => _filePath;

    /// <summary>Whether the text was shown with syntax colours.</summary>
    public bool IsHighlighted => _highlighting && _syntax is not null;

    /// <summary>
    /// The line (from 1) drawn at <paramref name="y"/> in this control's coordinates, or null below
    /// the last line or above the first.
    /// </summary>
    public int? LineAt(double y)
    {
        var textView = TextView;
        if (Document is not { } document || !textView.VisualLinesValid)
        {
            return null;
        }

        var top = this.TranslatePoint(new Point(0, y), textView)?.Y ?? y;
        var line = textView.GetDocumentLineByVisualTop(top + textView.VerticalOffset);
        return line is null || top < 0 || top + textView.VerticalOffset >= textView.DocumentHeight ? null : Math.Min(line.LineNumber, document.LineCount);
    }

    /// <summary>
    /// Where column <paramref name="column"/> of line <paramref name="line"/> (both from 1) starts,
    /// halfway down the line, in this control's coordinates; null when the line is not laid out in view.
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

    /// <summary>The number column's width: 30.4 for up to 99 lines at the code font's 7.2 per digit.</summary>
    public double NumberColumnWidth => _numbers.Bounds.Width;

    internal DiffPalette Palette => _palette ??= DiffPalette.Resolve(this);

    internal DiffSyntax? Syntax => _syntax;

    protected override AutomationPeer OnCreateAutomationPeer() => new BlameTextAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        OnThemeChanged();
    }

    /// <summary>
    /// The file's lines as a diff of one hunk of context lines, tokenized as the diff's new side,
    /// or null when the path has no grammar or TextMate fails (the text shows without colours).
    /// </summary>
    private static DiffSyntax? Tokenize(IReadOnlyList<string> lines, string? path)
    {
        if (!DiffHighlighter.HasGrammar(path))
        {
            return null;
        }

        var context = lines.Select((text, index) => new DiffLine(DiffLineKind.Context, text, text, index + 1, index + 1)).ToList();
        var hunk = new DiffHunk(string.Empty, string.Empty, 1, lines.Count, 1, lines.Count, context);
        try
        {
            return DiffHighlighter.Tokenize(new FileDiff([], [hunk], false, false, false, null, null, lines.Sum(line => (long)line.Length + 1)), path);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
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
        _numbers.UpdateWidth();
    }

    /// <summary>The text as UI Automation sees it: a document whose read-only value is the text, so that the real-window pass can read it.</summary>
    private sealed class BlameTextAutomationPeer(BlameTextEditor owner) : ControlAutomationPeer(owner), IValueProvider
    {
        public bool IsReadOnly => true;

        public string? Value => owner.Document?.Text;

        public void SetValue(string? value) => throw new InvalidOperationException("The blame's text is read-only.");

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>The blame text's number column: the numbers right-aligned 8 from its right edge in the secondary colour, as the resolver's result's.</summary>
internal sealed class BlameNumberMargin(BlameTextEditor editor) : AbstractMargin
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

/// <summary>Colours each line by the diff's syntax colours (D64): the tokens of its context line in the synthetic diff.</summary>
internal sealed class BlameSyntaxColorizer(BlameTextEditor editor) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        if (editor.Syntax is not { } syntax)
        {
            return;
        }

        var dark = editor.Palette.IsDark;
        foreach (var run in syntax.RunsOf(new DiffLineRef(0, line.LineNumber - 1), DiffVersion.New, dark))
        {
            var start = line.Offset + Math.Min(run.Start, line.Length);
            var end = line.Offset + Math.Min(run.Start + run.Length, line.Length);
            if (end <= start)
            {
                continue;
            }

            var style = run.Style;
            ChangeLinePart(start, end, element =>
            {
                if (style.Foreground is { } colour)
                {
                    element.TextRunProperties.SetForegroundBrush(editor.Palette.SyntaxBrush(colour));
                }

                if (style.Bold || style.Italic)
                {
                    var typeface = element.TextRunProperties.Typeface;
                    element.TextRunProperties.SetTypeface(new Typeface(
                        typeface.FontFamily,
                        style.Italic ? FontStyle.Italic : typeface.Style,
                        style.Bold ? FontWeight.Bold : typeface.Weight));
                }

                if (style.Underline || style.Strikethrough)
                {
                    element.TextRunProperties.SetTextDecorations(style.Underline ? TextDecorations.Underline : TextDecorations.Strikethrough);
                }
            });
        }
    }
}
