using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The texts of the branch and history operations (docs/phase-3-design.md 2.1 to 2.15, 2.31) that
/// 2.33's table leaves to the operations themselves: the refusals of an operation started while
/// another is in progress or on the branch HEAD is on, and the failures of a step that a sequence
/// of several calls reaches. Built by WP7-branches beside the lead's <c>OperationTexts.cs</c>.
/// </summary>
public static partial class OperationTexts
{
    /// <summary>
    /// The summary of the error dialog of a merge, cherry-pick or revert started while
    /// <paramref name="kind"/> is in progress (design 5): the menus' and the toolbar's disabled
    /// reason, word for word ("Finish or abort the merge first").
    /// </summary>
    public static string FinishOrAbortFirst(InProgressKind kind) => $"Finish or abort the {InProgressName(kind)} first";

    /// <summary>The summary of "Deleting the branch failed" for the branch HEAD is on: the menu's disabled reason (2.6).</summary>
    public const string CannotDeleteCurrentBranch = "You cannot delete the branch you are on";

    /// <summary>
    /// 2.31 when the checkout failed and the stash could not be popped back either: the changes
    /// are named where they are, as in every outcome of 2.31.
    /// </summary>
    public static string CheckoutFailedChangesKept(string target) =>
        $"Git could not check out {target}, and your changes could not be re-applied. They are kept in the stash \"{StashBeforeCheckout(target)}\".";

    /// <summary>
    /// The status bar's message, in the warning colour, when a revert that changes nothing stopped
    /// with <c>REVERT_HEAD</c> (D117's other case, check 12 part E): "Revert stopped: a1b2c3d changes
    /// nothing on main: it is already undone." No dialog opens; the banner offers Abort.
    /// </summary>
    public static string RevertStoppedEmpty(string sha, string current) => $"Revert stopped: {RevertEmpty(sha, current)}";

    /// <summary>
    /// The summary of a checkout refused inside its operation (2.1, D83): while it waited, HEAD
    /// came to be detached on commits that nothing else reaches, which the user was never asked
    /// to leave behind. "HEAD moved while this waited: it is now detached on 3 commits that are
    /// on no branch. Nothing was checked out, so they are not left behind."
    /// </summary>
    public static string DetachedCommitsSinceChosen(int count) =>
        $"HEAD moved while this waited: it is now detached on {Commits(count)} that {(count == 1 ? "is" : "are")} on no branch. "
        + $"Nothing was checked out, so {(count == 1 ? "it is" : "they are")} not left behind.";

    /// <summary>
    /// The summary of an operation on HEAD refused inside its operation because HEAD is no longer
    /// on the branch the user chose it for: "HEAD moved while this waited: it was on main and is
    /// on other now. Nothing was reset." <paramref name="nothing"/> is the last sentence.
    /// </summary>
    public static string HeadMoved(HeadState then, HeadState now, string nothing)
    {
        ArgumentNullException.ThrowIfNull(then);
        ArgumentNullException.ThrowIfNull(now);
        return $"HEAD moved while this waited: it was {Where(then)} and is {Where(now)} now. {nothing}";

        static string Where(HeadState head) => head.BranchName is { } branch ? $"on {branch}" : $"detached at {Short(head.Sha ?? string.Empty)}";
    }

    /// <summary>
    /// The summary of a reset refused because the branch (or the detached HEAD) it was chosen for
    /// moved while it waited: "main moved while this waited: it was at d0bc350 and is at 4a5b6c7
    /// now. Nothing was reset."
    /// </summary>
    public static string HeadCommitMoved(string current, string then, string now, string nothing) =>
        $"{current} moved while this waited: it was at {Short(then)} and is at {Short(now)} now. {nothing}";

    /// <summary>
    /// The summary of a pull refused because the branch's upstream changed while it waited (2.18):
    /// "main's upstream changed while this waited: it is origin/dev now, not origin/main. Nothing
    /// was pulled."; <paramref name="now"/> null: "it has none now".
    /// </summary>
    public static string UpstreamChanged(string branch, string then, string? now) =>
        $"{branch}'s upstream changed while this waited: {(now is null ? "it has none now" : $"it is {now} now, not {then}")}. {NothingPulled}";

    public const string NothingReset = "Nothing was reset.";

    public const string NothingMerged = "Nothing was merged.";

    public const string NothingPulled = "Nothing was pulled.";

    public const string NothingCherryPicked = "Nothing was cherry-picked.";

    public const string NothingReverted = "Nothing was reverted.";

    /// <summary>The last sentence of an amend refused because HEAD moved after "Amend previous commit" was ticked on it (D124).</summary>
    public const string NothingAmended = "Nothing was amended.";

    private static string Commits(int count) => count == 1 ? "1 commit" : $"{count} commits";
}
