using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Rebase;

/// <summary>
/// What the interactive rebase's list (<c>InteractiveRebaseViewModel</c>, docs/phase-4-design.md
/// 2.2) asks of its repository: <see cref="RepositoryViewModel"/> implements it, so that the list
/// is tested with a recording fake. Every member is called on the UI thread. Start and Close name
/// the list that asks: the repository acts on them only for the list it shows, so that a list it
/// no longer shows neither starts a rebase nor closes a list opened after it.
/// </summary>
public interface IInteractiveRebaseHost
{
    /// <summary>
    /// Start rebase (2.4) of <paramref name="list"/>: runs <paramref name="plan"/> as the operation
    /// "Interactive rebase of {b}" (with the checkout first for a drop's source). Completes with
    /// true when the rebase is done or stopped, and the list is to close; false when git refused
    /// before anything started (a dirty tree, a hook), and the list stays open as it was. A list
    /// the repository no longer shows starts nothing (false).
    /// </summary>
    Task<bool> StartAsync(InteractiveRebaseViewModel list, RebasePlan plan);

    /// <summary>
    /// Shows the selected row's commit in the right panel (2.2): selects <paramref name="sha"/> in
    /// the graph behind the list by its SHA (D88), without closing the list; null clears it.
    /// </summary>
    void ShowDetails(string? sha);

    /// <summary>Asks a question on the window's dialog layer (7.2: "Discard the changes to this list?"); true when the user goes ahead.</summary>
    Task<bool> ConfirmAsync(ConfirmationRequest request);

    /// <summary>
    /// Opens the message dialog (7.2, <c>MessageDialogViewModel</c>) titled
    /// <paramref name="title"/>, filled with <paramref name="message"/>, with the confirm button
    /// <paramref name="confirmText"/> (<c>Save</c>, <c>Squash</c>): a reword's or a squash group's
    /// new message. Completes with the message as joined (5.3), or null when it was cancelled.
    /// </summary>
    Task<string?> AskMessageAsync(string title, string message, string confirmText);

    /// <summary>
    /// Closes <paramref name="list"/> (2.2): the graph comes back, with the focus and the commit
    /// last selected in it. Only the list shown closes: for a list the repository no longer shows
    /// it does nothing, so that the end of an earlier list never closes a later one.
    /// </summary>
    void Close(InteractiveRebaseViewModel list);
}
