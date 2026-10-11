using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>The texts of the stash and tag operations (design 2.24 to 2.28) that 2.33's table and <c>OperationTexts.cs</c> leave out.</summary>
public static partial class OperationTexts
{
    /// <summary>D119's reason for a conflict left without an operation (a stash that did not apply cleanly).</summary>
    public const string StashRefusedConflicts = "Resolve the conflicts first";

    /// <summary>
    /// 2.27's texts before the tag has a name: a failure while the commit for the tag dialog is
    /// read (the operation itself has <see cref="CreateTag"/>'s texts).
    /// </summary>
    public static OperationText CreateTagUnnamed { get; } =
        new("Create tag", "Creating the tag…", "Creating the tag failed", "Git could not create the tag.");

    /// <summary>
    /// Why a stash may not be made now (D119), or null when it may: "Finish or abort the
    /// &lt;kind&gt; first" while a merge, cherry-pick, revert, rebase or <c>git am</c> is in progress,
    /// "Resolve the conflicts first" while conflicted files are left without one. The toolbar's and
    /// the menus' Stash use the same texts as their disabled reason.
    /// </summary>
    public static string? StashRefusedReason(WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (status.InProgress.IsActive)
        {
            return $"Finish or abort the {InProgressName(status.InProgress.Kind)} first";
        }

        return status.Unstaged.Any(file => file.Kind == FileChangeKind.Conflicted) || status.Staged.Any(file => file.Kind == FileChangeKind.Conflicted)
            ? StashRefusedConflicts
            : null;
    }
}
