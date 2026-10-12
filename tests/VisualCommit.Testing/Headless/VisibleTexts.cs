using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// The texts a region shows, as a person reads them. Shared by the walk-through projects (phase 0's
/// <c>ShellExpectations.TextsIn</c> and phase 3's checks).
/// </summary>
public static class VisibleTexts
{
    /// <summary>The visible texts inside a region, in reading order: top to bottom, then left to right.</summary>
    public static List<string> In(Visual region)
    {
        ArgumentNullException.ThrowIfNull(region);
        var items = region.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
            .Select(text => (Text: text.Text!, Area: new Rect(text.TranslatePoint(default, region) ?? default, text.Bounds.Size)))
            .OrderBy(item => item.Area.Center.Y)
            .ToList();

        // Texts whose centres are within a few pixels of each other vertically are on one row.
        var texts = new List<string>();
        for (var start = 0; start < items.Count;)
        {
            var end = start + 1;
            while (end < items.Count && items[end].Area.Center.Y - items[end - 1].Area.Center.Y < 6)
            {
                end++;
            }

            texts.AddRange(items[start..end].OrderBy(item => item.Area.X).Select(item => item.Text));
            start = end;
        }

        return texts;
    }
}
