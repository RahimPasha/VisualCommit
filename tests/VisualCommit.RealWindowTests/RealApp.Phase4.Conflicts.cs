using System.Drawing;
using FlaUI.Core.AutomationElements;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The conflict resolver in the real window (docs/phase-4-design.md 13.2, package WP6), through UI
/// Automation and the automation ids of 7.4: waiting for it to show a file and to close, its texts
/// (each a text element named by its text), the result's text through its read-only Value pattern,
/// a bar's button among those in view (block 1's being the upper one), a click in the result at a
/// line and column computed as the report's part C says, and the card's buttons, all clicked with
/// the real mouse.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>The resolver's root (a control element of its own).</summary>
    public const string ResolverId = "ConflictResolver";

    /// <summary>The result editor: a Document with a read-only Value pattern holding the text.</summary>
    public const string ResultId = "ConflictResult";

    /// <summary>The row height of the result and the panes: the code font's line height at 12 (D64).</summary>
    public const float ResultRowHeight = 15.84f;

    /// <summary>The result's number column for up to 99 lines (8 + 7.2 × 2 + 8) and the 2 px into the text the report adds.</summary>
    public const float ResultTextLeft = 30.4f + 2;

    /// <summary>The advance of one character of the code font at 12.</summary>
    public const float CodeCharacterWidth = 7.2f;

    /// <summary>Whether the resolver is in the window and on screen.</summary>
    public bool IsResolverShown => FindAll(ResolverId).Any(element => !element.IsOffscreen);

    /// <summary>The resolver's root element; throws when it is not in the window.</summary>
    public AutomationElement Resolver() => Find(ResolverId);

    /// <summary>Waits until the resolver shows <paramref name="fileName"/> (<c>ResolverFileName</c>), or any file when null.</summary>
    public void WaitForResolver(string? fileName = null, TimeSpan? timeout = null) =>
        WaitFor(
            () => IsResolverShown && (fileName is null || FindAll("ResolverFileName").Any(element => !element.IsOffscreen && element.Name == fileName)),
            fileName is null ? "the conflict resolver" : $"the conflict resolver on {fileName}",
            timeout ?? DefaultWaitTimeout);

    /// <summary>Waits until the resolver is no longer shown.</summary>
    public void WaitForResolverClosed(TimeSpan? timeout = null) =>
        WaitFor(() => !IsResolverShown, "the conflict resolver to close", timeout ?? DefaultWaitTimeout);

    /// <summary>
    /// The text of one of the resolver's texts by its automation id (<c>ResolverFileName</c>,
    /// <c>ResolverConflictPosition</c>, <c>ConflictResultCount</c>, <c>ConflictCardText</c>, …).
    /// </summary>
    public string ResolverText(string automationId) => Find(automationId).Name;

    /// <summary>The result's text, read through its Value pattern.</summary>
    public string ResultText()
    {
        var result = Find(ResultId);
        return result.Patterns.Value.PatternOrDefault?.Value.Value
            ?? throw new InvalidOperationException("The result has no Value pattern.");
    }

    /// <summary>
    /// The button with <paramref name="automationId"/> (<c>ConflictUseOurs</c>, …) of a block's
    /// bar in view, the upper one first: <paramref name="index"/> 0 is the uppermost bar in view.
    /// </summary>
    public AutomationElement BarButton(string automationId, int index = 0)
    {
        var buttons = FindAll(automationId)
            .Where(element => !element.IsOffscreen)
            .OrderBy(element => element.BoundingRectangle.Top)
            .ToList();
        return index < buttons.Count
            ? buttons[index]
            : throw new InvalidOperationException($"Only {buttons.Count} {automationId} in view; wanted the bar {index + 1} from the top.");
    }

    /// <summary>Clicks a bar's button with the real mouse (<see cref="BarButton"/>).</summary>
    public void ClickBarButton(string automationId, int index = 0) => ClickWithMouse(BarButton(automationId, index));

    /// <summary>
    /// The point of the result's line <paramref name="line"/> and column <paramref name="column"/>
    /// (from 1) in the client area, as the report's part C computes it from the result's element:
    /// x = its left + 30.4 + 2 + 7.2 × (column − 1), y = its top + (line − 0.5) × 15.84, the result
    /// being at its top (opening on block 1 scrolls nothing).
    /// </summary>
    public PointF ResultPoint(int line, int column)
    {
        var bounds = BoundsOf(Find(ResultId));
        return new PointF(bounds.Left + ResultTextLeft + (CodeCharacterWidth * (column - 1)), bounds.Top + ((line - 0.5f) * ResultRowHeight));
    }

    /// <summary>Clicks in the result at a line and column with the real mouse (<see cref="ResultPoint"/>).</summary>
    public void ClickResultAt(int line, int column)
    {
        var point = ResultPoint(line, column);
        ClickAt(point.X, point.Y);
    }

    /// <summary>Clicks one of the resolver's buttons (header, toolbar, card) by its automation id with the real mouse; throws when it is off screen or disabled.</summary>
    public void ClickResolverButton(string automationId)
    {
        var button = FindAll(automationId).FirstOrDefault(element => !element.IsOffscreen)
            ?? throw new InvalidOperationException($"The resolver's {automationId} is not on screen.");
        if (!button.IsEnabled)
        {
            throw new InvalidOperationException($"The resolver's {automationId} is disabled.");
        }

        ClickWithMouse(button);
    }

    /// <summary>Whether a button of the resolver is on screen and enabled.</summary>
    public bool IsResolverButtonEnabled(string automationId) =>
        FindAll(automationId).FirstOrDefault(element => !element.IsOffscreen) is { IsEnabled: true };
}
