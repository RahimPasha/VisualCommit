using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// What the conflict banner asks its repository to do (docs/phase-3-design.md section 5, 2.15).
/// <see cref="RepositoryOperations"/> (work package WP7) implements it, as it implements
/// <see cref="IToolbarActions"/>; tests give a fake through
/// <see cref="RepositoryViewModel.InProgressActions"/>.
/// </summary>
public interface IInProgressActions
{
    /// <summary>
    /// Aborts <paramref name="state"/>'s operation, after the user confirmed the banner's question
    /// (<see cref="ConflictBannerViewModel.AbortConfirmation"/>): saves the backups of 2.0 and 2.15
    /// (<c>abort-&lt;time&gt;</c> and, with changed tracked files, <c>abort-&lt;time&gt;-changes</c>,
    /// one batch), runs <see cref="IGitRepository.AbortAsync"/> for <see cref="InProgressState.Kind"/>
    /// as the operation "Abort the &lt;kind&gt;" of the queue (2.33), and shows its result: "Aborted
    /// the &lt;kind&gt;" in the status bar, or the error dialog "Abort failed". Completes when the
    /// operation has ended; never throws for git's failure.
    /// </summary>
    Task AbortAsync(InProgressState state);
}
