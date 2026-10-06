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
    public static IReadOnlyList<string> FindClippedText(TopLevel window)
    {
        var problems = new List<string>();
        var windowArea = new Rect(window.ClientSize);

        foreach (var text in window.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!text.IsEffectivelyVisible || string.IsNullOrEmpty(text.Text))
            {
                continue;
            }

            var label = $"\"{text.Text}\"";
            var layout = text.TextLayout;
            var room = text.Bounds.Size.Deflate(text.Padding);

            if (layout.WidthIncludingTrailingWhitespace > room.Width + Tolerance)
            {
                problems.Add($"{label} needs a width of {layout.WidthIncludingTrailingWhitespace:F1} but has {room.Width:F1}.");
            }

            if (layout.Height > room.Height + Tolerance)
            {
                problems.Add($"{label} needs a height of {layout.Height:F1} but has {room.Height:F1}.");
            }

            if (layout.TextLines.Any(line => line.HasCollapsed))
            {
                problems.Add($"{label} is shortened with an ellipsis.");
            }

            var area = AreaInWindow(text, window);
            var visible = area.Intersect(windowArea);
            foreach (var ancestor in text.GetVisualAncestors().OfType<Control>().Where(a => a.ClipToBounds))
            {
                visible = visible.Intersect(AreaInWindow(ancestor, window));
            }

            if (visible.Width < area.Width - Tolerance || visible.Height < area.Height - Tolerance)
            {
                problems.Add($"{label} at {area} is cut off to {visible}.");
            }
        }

        return problems;
    }

    private static Rect AreaInWindow(Visual visual, TopLevel window)
    {
        var topLeft = visual.TranslatePoint(default, window) ?? default;
        return new Rect(topLeft, visual.Bounds.Size);
    }
}
