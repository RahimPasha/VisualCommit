using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace VisualCommit.App.Controls.Diff;

/// <summary>
/// One number column of a code editor: 8 + 7.2 × d + 8 wide for the d digits of the last line's
/// number (at least 2), the numbers right-aligned 8 from its right edge in the secondary colour,
/// each on its line's tint where <c>lineTint</c> gives one (the conflict resolver's result); the
/// blame's text has none. One rule for both, so that the column cannot drift between them.
/// </summary>
/// <param name="editor">The editor whose lines are numbered.</param>
/// <param name="palette">The editor's colours and code font.</param>
/// <param name="lineTint">The brush behind line n's number (from 1), or null for none.</param>
internal sealed class CodeNumberMargin(TextEditor editor, Func<DiffPalette> palette, Func<int, IBrush?>? lineTint = null) : AbstractMargin
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

        var colours = palette();
        var typeface = new Typeface(colours.CodeFont);
        foreach (var line in textView.VisualLines)
        {
            var number = line.FirstDocumentLine.LineNumber;
            if (lineTint?.Invoke(number) is { } tint)
            {
                var top = Math.Round(line.VisualTop - textView.VerticalOffset);
                var bottom = Math.Round(line.VisualTop + line.Height - textView.VerticalOffset);
                context.FillRectangle(tint, new Rect(0, top, Bounds.Width, bottom - top));
            }

            var text = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, DiffPalette.CodeFontSize, colours.TextSecondary);
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
        new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(palette().CodeFont), DiffPalette.CodeFontSize, Brushes.Transparent).WidthIncludingTrailingWhitespace;
}
