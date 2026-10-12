using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.App.Views.Rebase;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// The interactive rebase's list in the driver (docs/phase-4-design.md 2.2, 7.2, 13.2; package
/// WP5): the list as a snapshot in the window's coordinates (<see cref="InteractiveRebaseView.Snapshot"/>),
/// a row's point, a click and a right-click on a row, its action button and its menu, a drag of a
/// row in steps, <c>Edit…</c>, Start and Cancel, and the message dialog's fields. A row is named
/// by its commit's subject, as the report names it ("the row Fix one").
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The list's automation id (design 7.4).</summary>
    public const string RebaseListId = "InteractiveRebaseView";

    /// <summary>
    /// How far right of the list's left edge a row's point lies: window x 600 at 1100×700 (the
    /// report's "a list row's point"), in the subject column, on no button, at every indent.
    /// </summary>
    public const double RebaseRowPointX = 340;

    /// <summary>The list as the window shows it, or null when no list is in view.</summary>
    public InteractiveRebaseView? RebaseListViewOrNull
    {
        get
        {
            Settle();
            return Window.GetVisualDescendants().OfType<InteractiveRebaseView>().FirstOrDefault(view => view.IsEffectivelyVisible);
        }
    }

    /// <summary>The list in view. Throws when none is.</summary>
    public InteractiveRebaseView RebaseListView =>
        RebaseListViewOrNull ?? throw new InvalidOperationException("No interactive rebase list is in view.");

    /// <summary>Whether an interactive rebase list is in view now.</summary>
    public bool IsRebaseListOpen => RebaseListViewOrNull is not null;

    /// <summary>Waits until a list is in view with its rows laid out (or its message shown), and returns it.</summary>
    public async Task<InteractiveRebaseView> WaitForRebaseListAsync(TimeSpan? timeout = null)
    {
        InteractiveRebaseView? found = null;
        await WaitForAsync(
            () =>
            {
                found = RebaseListViewOrNull;
                if (found is null)
                {
                    return false;
                }

                var list = found.Snapshot(Window);
                return list.Message is not null || (list.Rows.Count > 0 && list.Rows[0].Bounds is not null && list.Rows[0].HasKeyboardFocus);
            },
            "the interactive rebase list to show its rows",
            timeout);
        return found!;
    }

    /// <summary>The list as drawn now, in the window's coordinates.</summary>
    public InteractiveRebaseSnapshot RebaseListSnapshot() => RebaseListView.Snapshot(Window);

    /// <summary>
    /// A row's point (the report's convention): its vertical centre, <see cref="RebaseRowPointX"/>
    /// right of the list's left edge (window x 600 at 1100×700). Throws when the row is not drawn.
    /// </summary>
    public Point RebaseRowPoint(string subject)
    {
        var list = RebaseListSnapshot();
        var bounds = list.Row(subject).Bounds ?? throw new InvalidOperationException($"The row \"{subject}\" is not drawn: scroll it into view first.");
        return new Point(list.Bounds.X + RebaseRowPointX, bounds.Center.Y);
    }

    /// <summary>Clicks the row at its point: it is selected and takes the keyboard focus.</summary>
    public void ClickRebaseRow(string subject) => Click(RebaseRowPoint(subject));

    /// <summary>Right-clicks the row at its point: it is selected and its menu opens 2 right of and 2 below the pointer.</summary>
    public void RightClickRebaseRow(string subject) => RightClick(RebaseRowPoint(subject));

    /// <summary>Where the row's action button (<c>RebaseActionButton</c>) is, in the window.</summary>
    public Rect RebaseActionButtonBounds(string subject) =>
        RebaseListSnapshot().Row(subject).ActionButtonBounds ?? throw new InvalidOperationException($"The row \"{subject}\" is not drawn: scroll it into view first.");

    /// <summary>The bottom-left corner of the row's action button: where its menu opens from the button, Shift+F10 or the Menu key (design 14.3).</summary>
    public Point RebaseActionButtonCorner(string subject) => RebaseActionButtonBounds(subject).BottomLeft;

    /// <summary>Clicks the row's action button: the row is selected and its menu opens at the button's bottom-left corner.</summary>
    public void ClickRebaseActionButton(string subject) => Click(RebaseActionButtonBounds(subject).Center);

    /// <summary>
    /// Sets a row's action through its action button's menu: clicks the button, waits for the menu
    /// and clicks <paramref name="item"/> (<c>Pick</c>, <c>Reword…</c>, <c>Squash</c>, <c>Fixup</c>,
    /// <c>Drop</c>). <c>Reword…</c> then opens the message dialog.
    /// </summary>
    public void ChooseRebaseAction(string subject, string item)
    {
        ClickRebaseActionButton(subject);
        ClickMenuItem(item);
    }

    /// <summary>Presses the left button at the row's point without releasing it: the start of a drag that goes on with <see cref="MoveMouseHeld"/>.</summary>
    public void PressRebaseRow(string subject) => PressMouse(RebaseRowPoint(subject));

    /// <summary>
    /// Drags the row from its point to <paramref name="to"/> in <paramref name="steps"/> equal moves
    /// with the left button held, and releases there (design 2.2: a drag starts past 4 px).
    /// </summary>
    public void DragRebaseRow(string subject, Point to, int steps = 5)
    {
        PressRebaseRow(subject);
        MoveMouseHeld(to, steps);
        ReleaseMouse(to);
    }

    /// <summary>Clicks <c>Edit…</c> (<c>RebaseEditMessageButton</c>) on the second line under the row: the message dialog opens.</summary>
    public void ClickRebaseEditMessage(string subject)
    {
        var bounds = RebaseListSnapshot().Row(subject).EditButtonBounds
            ?? throw new InvalidOperationException($"The row \"{subject}\" has no second line with Edit… in view.");
        Click(bounds.Center);
    }

    /// <summary>Clicks <c>Start rebase</c>. Throws when it is hidden or disabled (a click would do nothing).</summary>
    public void ClickRebaseStart()
    {
        var list = RebaseListSnapshot();
        if (!list.StartShown || !list.CanStart)
        {
            throw new InvalidOperationException($"Start rebase cannot be clicked: shown {list.StartShown}, enabled {list.CanStart} ({list.StartToolTip ?? "no reason given"}).");
        }

        Click(list.StartBounds.Center);
    }

    /// <summary>Clicks the list's <c>Cancel</c>: it closes, asking first when the list was changed.</summary>
    public void ClickRebaseCancel() => Click(RebaseListSnapshot().CancelBounds.Center);

    /// <summary>
    /// The message dialog on top (<c>MessageDialog</c>, design 7.2): its title, summary (with where
    /// the caret is and whether it has the focus), counter, description, buttons and card. Throws
    /// when the dialog on top is another.
    /// </summary>
    public MessageDialogSnapshot MessageDialog()
    {
        var view = TopDialogView as MessageDialogView
            ?? throw new InvalidOperationException($"The dialog on top is not the message dialog: {TopDialogView?.GetType().Name ?? "none is open"}.");
        var summary = (TextBox)FindByAutomationId(view, "MessageSummary");
        var description = (TextBox)FindByAutomationId(view, "MessageDescription");
        var confirm = (Button)FindByAutomationId(view, "ConfirmButton");
        var cancel = (Button)FindByAutomationId(view, "CancelButton");
        var card = view.GetVisualDescendants().OfType<Border>().First(border => border.Name == "MessageDialogCard");
        var repository = (TextBlock)FindByAutomationId(view, "DialogRepository");
        return new MessageDialogSnapshot(
            ((TextBlock)FindByAutomationId(view, "MessageDialogTitle")).Text ?? string.Empty,
            summary.Text ?? string.Empty,
            summary.CaretIndex,
            summary.IsFocused,
            ((TextBlock)FindByAutomationId(view, "MessageSummaryCounter")).Text ?? string.Empty,
            description.Text ?? string.Empty,
            description.IsFocused,
            confirm.Content as string ?? string.Empty,
            confirm.IsEffectivelyEnabled,
            ToolTip.GetTip(confirm) as string,
            cancel.Content as string ?? string.Empty,
            repository.IsEffectivelyVisible ? repository.Text : null,
            BoundsOf(card),
            BoundsOf(summary),
            BoundsOf(description));
    }
}

/// <summary>The message dialog as it is drawn (design 7.2).</summary>
/// <param name="Title">The title (<c>MessageDialogTitle</c>).</param>
/// <param name="Summary">The summary box's text (<c>MessageSummary</c>).</param>
/// <param name="SummaryCaret">Where the caret is in the summary.</param>
/// <param name="SummaryHasFocus">Whether the summary has the keyboard focus.</param>
/// <param name="Counter">The counter's text (<c>MessageSummaryCounter</c>): 72 less the summary's length.</param>
/// <param name="Description">The description box's text (<c>MessageDescription</c>).</param>
/// <param name="DescriptionHasFocus">Whether the description has the keyboard focus.</param>
/// <param name="ConfirmText">The confirm button's label (<c>ConfirmButton</c>): <c>Save</c> or <c>Squash</c>.</param>
/// <param name="ConfirmEnabled">Whether the confirm button is enabled.</param>
/// <param name="ConfirmToolTip">The confirm button's tooltip (<c>Write a summary</c> while it is disabled), or null.</param>
/// <param name="CancelText">The cancel button's label (<c>CancelButton</c>).</param>
/// <param name="Repository">The "In &lt;repo&gt;" line, or null when it is not shown.</param>
/// <param name="CardBounds">The card, in the window.</param>
/// <param name="SummaryBounds">The summary box, in the window.</param>
/// <param name="DescriptionBounds">The description box, in the window.</param>
public sealed record MessageDialogSnapshot(
    string Title,
    string Summary,
    int SummaryCaret,
    bool SummaryHasFocus,
    string Counter,
    string Description,
    bool DescriptionHasFocus,
    string ConfirmText,
    bool ConfirmEnabled,
    string? ConfirmToolTip,
    string CancelText,
    string? Repository,
    Rect CardBounds,
    Rect SummaryBounds,
    Rect DescriptionBounds);
