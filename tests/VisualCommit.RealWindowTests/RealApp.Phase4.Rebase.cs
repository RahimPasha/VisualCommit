using System.Drawing;
using FlaUI.Core.AutomationElements;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// The interactive rebase's list in the real-window driver (docs/phase-4-design.md 2.2, 7.2, 7.4,
/// 13.2; package WP5), through UI Automation and the list's automation ids:
/// <c>InteractiveRebaseView</c>; <c>RebaseRow</c> list items named <c>{short} {subject}</c>, top to
/// bottom, each holding its <c>RebaseActionButton</c>; <c>RebaseRowSecondLine</c> and
/// <c>RebaseEditMessageButton</c> right under a row whose message will change (they are not inside
/// the <c>RebaseRow</c>, which is the row's 30 px); <c>RebaseBaseRow</c>; <c>RebaseListHeader</c>,
/// <c>RebaseListCount</c>, <c>RebaseListMessage</c>, <c>RebaseCancelButton</c>,
/// <c>RebaseStartButton</c>; and the message dialog's <c>MessageSummary</c>,
/// <c>MessageSummaryCounter</c>, <c>MessageDescription</c>. A row is named here by its UI
/// Automation name (<c>938844a Two</c>). Only the rows in view are in UI Automation: the list
/// virtualises its rows.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>The list's automation id.</summary>
    public const string RebaseListId = "InteractiveRebaseView";

    /// <summary>How far right of the list's left edge a row's point lies (window x 600 at 1100×700, the report's "a list row's point").</summary>
    public const float RebaseRowPointX = 340;

    /// <summary>The list, as UI Automation finds it. Throws when it is not in the window.</summary>
    public AutomationElement RebaseListView() => Find(RebaseListId);

    /// <summary>Waits until the list is in the window with its first row (or its message) shown, and returns it.</summary>
    public AutomationElement WaitForRebaseList(TimeSpan? timeout = null)
    {
        AutomationElement? list = null;
        WaitFor(
            () =>
            {
                list = FindAll(RebaseListId).FirstOrDefault(element => !element.IsOffscreen);
                return list is not null && (FindAll("RebaseRow").Count > 0 || FindAll("RebaseListMessage").Any(message => !message.IsOffscreen));
            },
            "the interactive rebase list to show its rows",
            timeout ?? DefaultWaitTimeout);
        return list!;
    }

    /// <summary>The rows in view (<c>RebaseRow</c>), top to bottom.</summary>
    public IReadOnlyList<AutomationElement> RebaseRows() =>
        [.. FindAll("RebaseRow").Where(row => !row.IsOffscreen).OrderBy(row => row.BoundingRectangle.Top)];

    /// <summary>The names of the rows in view (<c>{short} {subject}</c>), top to bottom.</summary>
    public IReadOnlyList<string> RebaseRowNames() => [.. RebaseRows().Select(row => row.Name)];

    /// <summary>The row named <paramref name="name"/> (<c>{short} {subject}</c>). Throws when there is none in view, or more than one.</summary>
    public AutomationElement RebaseRow(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return RebaseRows().Where(row => row.Name == name).ToList() switch
        {
            [var row] => row,
            [] => throw new InvalidOperationException($"The list has no row \"{name}\" in view; it has: {string.Join(" | ", RebaseRowNames())}."),
            _ => throw new InvalidOperationException($"The list has more than one row \"{name}\"."),
        };
    }

    /// <summary>A row's point: its vertical centre, <see cref="RebaseRowPointX"/> right of the list's left edge (window x 600 at 1100×700).</summary>
    public PointF RebaseRowPoint(string name)
    {
        var row = BoundsOf(RebaseRow(name));
        return new PointF(BoundsOf(RebaseListView()).X + RebaseRowPointX, row.Y + (row.Height / 2));
    }

    /// <summary>Clicks the row at its point with the real mouse: it is selected and takes the keyboard focus.</summary>
    public void ClickRebaseRow(string name)
    {
        var point = RebaseRowPoint(name);
        ClickAt(point.X, point.Y);
    }

    /// <summary>Right-clicks the row at its point with the real mouse: it is selected and its menu opens 2 right of and 2 below the pointer.</summary>
    public void RightClickRebaseRow(string name)
    {
        var point = RebaseRowPoint(name);
        RightClickAt(point.X, point.Y);
    }

    /// <summary>The row's action button (<c>RebaseActionButton</c>, named by the action: <c>Pick</c>, <c>Reword</c>, …).</summary>
    public AutomationElement RebaseActionButton(string name) =>
        RebaseRow(name).FindFirstDescendant(condition => condition.ByAutomationId("RebaseActionButton"))
        ?? throw new InvalidOperationException($"The row \"{name}\" has no action button.");

    /// <summary>The bottom-left corner of the row's action button, in the client area: where its menu opens (design 14.3).</summary>
    public PointF RebaseActionButtonCorner(string name)
    {
        var button = BoundsOf(RebaseActionButton(name));
        return new PointF(button.X, button.Bottom);
    }

    /// <summary>Clicks the row's action button with the real mouse: the row is selected and its menu opens at the button's bottom-left corner.</summary>
    public void ClickRebaseActionButton(string name) => ClickWithMouse(RebaseActionButton(name));

    /// <summary>Sets a row's action through its action button's menu: the button, then the item (<c>Pick</c>, <c>Reword…</c>, <c>Squash</c>, <c>Fixup</c>, <c>Drop</c>).</summary>
    public void ChooseRebaseAction(string name, string item)
    {
        ClickRebaseActionButton(name);
        ClickMenuItem(item);
    }

    /// <summary>The second line right under the row (<c>RebaseRowSecondLine</c>, its text as its name), or null when the row has none.</summary>
    public AutomationElement? RebaseSecondLine(string name) => UnderRow(name, "RebaseRowSecondLine");

    /// <summary>The <c>Edit…</c> button on the second line under the row. Throws when the row has no second line.</summary>
    public AutomationElement RebaseEditMessageButton(string name) =>
        UnderRow(name, "RebaseEditMessageButton") ?? throw new InvalidOperationException($"The row \"{name}\" has no second line with Edit….");

    /// <summary>Clicks <c>Edit…</c> under the row with the real mouse: the message dialog opens.</summary>
    public void ClickRebaseEditMessage(string name) => ClickWithMouse(RebaseEditMessageButton(name));

    /// <summary>The header's text (<c>RebaseListHeader</c>).</summary>
    public string RebaseListHeaderText() => Find("RebaseListHeader").Name;

    /// <summary>The count's text (<c>RebaseListCount</c>).</summary>
    public string RebaseListCountText() => Find("RebaseListCount").Name;

    /// <summary>The base row (<c>RebaseBaseRow</c>, named <c>Base {short} {subject}</c>).</summary>
    public AutomationElement RebaseBaseRow() => Find("RebaseBaseRow");

    /// <summary>The list's <c>Start rebase</c> button.</summary>
    public AutomationElement RebaseStartButton() => Find("RebaseStartButton");

    /// <summary>The list's <c>Cancel</c> button.</summary>
    public AutomationElement RebaseCancelButton() => Find("RebaseCancelButton");

    /// <summary>Clicks <c>Start rebase</c> with the real mouse. Throws when it is off screen or disabled.</summary>
    public void ClickRebaseStart()
    {
        var start = RebaseStartButton();
        if (start.IsOffscreen || !start.IsEnabled)
        {
            throw new InvalidOperationException($"Start rebase cannot be clicked: off screen {start.IsOffscreen}, enabled {start.IsEnabled}.");
        }

        ClickWithMouse(start);
    }

    /// <summary>Clicks the list's <c>Cancel</c> with the real mouse: the list closes, asking first when it was changed.</summary>
    public void ClickRebaseCancel() => ClickWithMouse(RebaseCancelButton());

    /// <summary>
    /// Drags the row from its point to (<paramref name="toX"/>, <paramref name="toY"/>) with the
    /// real mouse in <paramref name="steps"/> moves, the button held, and releases it there; the
    /// button is released even when a move fails. To capture in the middle, use
    /// <see cref="MouseDownAt"/>, <see cref="MoveHeldTo"/> and <see cref="MouseUpAt"/> (in a
    /// <c>finally</c>) at <see cref="RebaseRowPoint"/>.
    /// </summary>
    public void DragRebaseRow(string name, float toX, float toY, int steps = 5)
    {
        var from = RebaseRowPoint(name);
        MouseDownAt(from.X, from.Y);
        try
        {
            MoveHeldTo(toX, toY, steps);
        }
        finally
        {
            MouseUpAt(toX, toY);
        }
    }

    /// <summary>The message dialog's title (<c>MessageDialogTitle</c>).</summary>
    public string MessageDialogTitle() => Find("MessageDialogTitle").Name;

    /// <summary>The message dialog's summary (<c>MessageSummary</c>), through its Value pattern.</summary>
    public string MessageSummaryText() => Find("MessageSummary").AsTextBox().Text;

    /// <summary>The message dialog's counter (<c>MessageSummaryCounter</c>).</summary>
    public string MessageSummaryCounterText() => Find("MessageSummaryCounter").Name;

    /// <summary>The message dialog's description (<c>MessageDescription</c>), through its Value pattern.</summary>
    public string MessageDescriptionText() => Find("MessageDescription").AsTextBox().Text;

    /// <summary>The element with <paramref name="automationId"/> whose top edge is the bottom edge of the row named <paramref name="name"/> (±1), or null.</summary>
    private AutomationElement? UnderRow(string name, string automationId)
    {
        var row = BoundsOf(RebaseRow(name));
        return FindAll(automationId)
            .Where(element => !element.IsOffscreen)
            .Select(element => (Element: element, Bounds: BoundsOf(element)))
            .Where(candidate => candidate.Bounds.Top >= row.Bottom - 1 && candidate.Bounds.Top <= row.Bottom + 22)
            .OrderBy(candidate => candidate.Bounds.Top)
            .Select(candidate => candidate.Element)
            .FirstOrDefault();
    }
}
