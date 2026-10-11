namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The texts of the stash and tag operations (design 2.24 to 2.28) that 2.33's table and
/// <c>OperationTexts.cs</c> leave out. Why a stash is refused is the toolbar's reason
/// (<see cref="ToolbarViewModel.StashReason"/>, D119), the one function the toolbar, the menus and
/// the operation share.
/// </summary>
public static partial class OperationTexts
{
    /// <summary>
    /// 2.27's texts before the tag has a name: a failure while the commit for the tag dialog is
    /// read (the operation itself has <see cref="CreateTag"/>'s texts).
    /// </summary>
    public static OperationText CreateTagUnnamed { get; } =
        new("Create tag", "Creating the tag…", "Creating the tag failed", "Git could not create the tag.");

    /// <summary>
    /// The summary of "Applying the stash failed" when git applied the stash's tracked changes but
    /// not its untracked files, because files of those names exist (2.25); a pop adds that the
    /// stash is kept, as git keeps it.
    /// </summary>
    public static string StashUntrackedNotRestored(string stash, bool pop) =>
        $"The tracked changes of {stash} were applied, but its untracked files were not: files of those names already exist."
        + (pop ? " The stash is kept." : string.Empty);
}
