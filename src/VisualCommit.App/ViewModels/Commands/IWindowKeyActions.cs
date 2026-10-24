namespace VisualCommit.App.ViewModels.Commands;

/// <summary>
/// What the window does for a key or a palette entry that moves the focus (docs/phase-6-design.md
/// 6.3): <c>MainWindow</c> implements it and sets it on <see cref="MainWindowViewModel.WindowActions"/>
/// with its data context, so that no Avalonia type crosses into the view models.
/// </summary>
public interface IWindowKeyActions
{
    /// <summary>Focuses <c>CommitSummary</c> once the stage panel is laid out (the working-changes row already selected).</summary>
    void FocusCommitMessage();

    /// <summary>Focuses <c>FilterBox</c>, its text selected.</summary>
    void FocusFilter();

    /// <summary>Moves the focus to the next region, or with <paramref name="backwards"/> the previous one (F6, Shift+F6, 6.6).</summary>
    void MoveFocusToRegion(bool backwards);
}
