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
}
