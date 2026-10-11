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

    /// <summary>2.14 for several commits when one of them changes nothing (D117): the count, then <see cref="RevertEmpty"/>.</summary>
    public static string RevertSeveralEmpty(int done, int count, string empty, string current) =>
        $"{Progress("Reverted", done, count)} {RevertEmpty(empty, current)}";
}
