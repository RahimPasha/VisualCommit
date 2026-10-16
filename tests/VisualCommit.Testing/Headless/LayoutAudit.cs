using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace VisualCommit.Testing.Headless;

/// <summary>Checks a window's layout for faults a screenshot can hide at a glance.</summary>
public static class LayoutAudit
{
    private const double Tolerance = 0.75;

    /// <summary>
    /// Returns a description of every visible text in the window that is not shown in full:
    /// laid out wider or taller than the space it got, shortened with an ellipsis, or partly
    /// outside the window or a clipping parent. Empty means all text is fully shown.
    /// </summary>
    public static IReadOnlyList<string> FindClippedText(TopLevel window) => FindClippedText(window, allowShortenedWithToolTip: false);

    /// <summary>
    /// As <see cref="FindClippedText(TopLevel)"/>; with <paramref name="allowShortenedWithToolTip"/>,
    /// a text that is meant to be shortened is let through: one with text trimming whose whole
    /// text is in the tooltip of the text or of an element around it (phase 1: long names and
    /// paths end in "…" and show in full on hover).
    /// </summary>
    public static IReadOnlyList<string> FindClippedText(TopLevel window, bool allowShortenedWithToolTip) =>
        FindClippedText(window, allowShortenedWithToolTip, allowScrolledOutOfView: false);

    /// <summary>
    /// As <see cref="FindClippedText(TopLevel, bool)"/>; with <paramref name="allowScrolledOutOfView"/>,
    /// a text cut only by the viewport of a scrolling list is let through: a list with more rows
    /// than room shows part of a row at its edge on purpose (phase 2's stage lists).
    /// </summary>
    public static IReadOnlyList<string> FindClippedText(TopLevel window, bool allowShortenedWithToolTip, bool allowScrolledOutOfView)
    {
        var problems = new List<string>();
        var windowArea = new Rect(window.ClientSize);

        foreach (var text in window.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!text.IsEffectivelyVisible || string.IsNullOrEmpty(text.Text))
            {
                continue;
            }

            if (allowShortenedWithToolTip && text.TextTrimming != Avalonia.Media.TextTrimming.None && HasToolTipWithText(text))
            {
                continue;
            }

            var label = $"\"{text.Text}\"";
            var layout = text.TextLayout;
            var room = text.Bounds.Size.Deflate(text.Padding);

            // A wrapped line ends in the space it broke at, which hangs past the edge and is not
            // drawn: the width a wrapped text needs is its widest line without that space.
            var needed = text.TextWrapping == Avalonia.Media.TextWrapping.NoWrap ? layout.WidthIncludingTrailingWhitespace : layout.Width;
            if (needed > room.Width + Tolerance)
            {
                problems.Add($"{label} needs a width of {needed:F1} but has {room.Width:F1}.");
            }

            if (layout.Height > room.Height + Tolerance)
            {
                problems.Add($"{label} needs a height of {layout.Height:F1} but has {room.Height:F1}.");
            }

            if (layout.TextLines.Any(line => line.HasCollapsed))
            {
                problems.Add($"{label} is shortened with an ellipsis.");
            }

            // What the text should show: all of it, or with allowScrolledOutOfView, the part inside
            // its scrolling lists' views. Only a cut of that part by anything else is a fault: a
            // row that a list at a page's bottom shows in part is cut by the page there too.
            var area = AreaInWindow(text, window);
            var shown = area;
            var visible = area.Intersect(windowArea);
            foreach (var ancestor in text.GetVisualAncestors().OfType<Control>().Where(a => a.ClipToBounds))
            {
                if (allowScrolledOutOfView && ancestor is Avalonia.Controls.Presenters.ScrollContentPresenter or ScrollViewer or ItemsControl)
                {
                    shown = shown.Intersect(AreaInWindow(ancestor, window));
                    continue;
                }

                visible = visible.Intersect(AreaInWindow(ancestor, window));
            }

            visible = visible.Intersect(shown);
            if (visible.Width < shown.Width - Tolerance || visible.Height < shown.Height - Tolerance)
            {
                problems.Add($"{label} at {area} is cut off to {visible}.");
            }
        }

        return problems;
    }

    /// <summary>Whether the text, or one of the three elements around it, has the whole text as its tooltip.</summary>
    private static bool HasToolTipWithText(TextBlock text)
    {
        Visual? element = text;
        for (var level = 0; level < 4 && element is not null; level++, element = element.GetVisualParent())
        {
            if (element is Control control && ToolTip.GetTip(control) is string tip && tip.Contains(text.Text!, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Rect AreaInWindow(Visual visual, TopLevel window)
    {
        var topLeft = visual.TranslatePoint(default, window) ?? default;
        return new Rect(topLeft, visual.Bounds.Size);
    }
}
