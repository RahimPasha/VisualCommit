using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace VisualCommit.App.Controls.Diff;

/// <summary>
/// AvaloniaEdit's editor with its own look, as the app's code views use it: the diff's panes and
/// the conflict resolver's panes and result. Its search panel is removed, and it scrolls through
/// its scroll viewer.
/// </summary>
public abstract class CodeTextEditor : TextEditor
{
    private ScrollViewer? _scrollViewer;

    protected CodeTextEditor(TextArea textArea)
        : base(textArea)
    {
    }

    /// <summary>The current scroll offset, from the editor's scroll viewer.</summary>
    public Vector ScrollOffset => _scrollViewer?.Offset ?? default;

    protected override Type StyleKeyOverride => typeof(TextEditor);

    /// <summary>
    /// Where column <paramref name="column"/> of line <paramref name="line"/> (both from 1) starts,
    /// halfway down the line, in this control's coordinates; null when the line is not laid out in
    /// view. The blame's text and the resolver's result (and their tests' clicks) place by it.
    /// </summary>
    public Point? PointAt(int line, int column)
    {
        var textView = TextArea.TextView;
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
        // while it is closed, and the views' parents close them on Esc; they have no search.
        SearchPanel?.Uninstall();
    }
}
