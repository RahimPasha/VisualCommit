using Avalonia;
using Avalonia.Controls;

namespace VisualCommit.App.Controls;

/// <summary>
/// A text block that shows its whole text as its tooltip while its <see cref="TextBlock.TextTrimming"/>
/// cuts it with "…", and has no tooltip while the text fits: a context menu item's label longer
/// than the widest menu (docs/phase-3-design.md 4a). Styled as a plain <see cref="TextBlock"/>.
/// </summary>
public sealed class TrimmedTextBlock : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <summary>Whether the text was cut at the last arrange.</summary>
    public bool IsTrimmed { get; private set; }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // The arrange made a new layout for the final width: a collapsed line is a cut one.
        var trimmed = TextTrimming != Avalonia.Media.TextTrimming.None && TextLayout.TextLines.Any(line => line.HasCollapsed);
        if (trimmed != IsTrimmed)
        {
            IsTrimmed = trimmed;
            ToolTip.SetTip(this, trimmed ? Text : null);
        }

        return size;
    }
}
