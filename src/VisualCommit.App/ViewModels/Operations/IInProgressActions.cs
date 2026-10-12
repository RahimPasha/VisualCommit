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

    /// <summary>
    /// Continue (docs/phase-4-design.md 5.1, 5.4, D144) for <paramref name="state"/>, from the
    /// banner (<paramref name="message"/> null) or the stage panel (its message): refuses when the
    /// state changed since the click (a fresh status read), asks when markers are staged (7.2),
    /// then runs <see cref="IGitRepository.ContinueAsync"/> as the local operation "Continue the
    /// {kind}". Ends done, at the next stop (the banner), or failed ("Continue failed"). Completes
    /// when the operation has ended; never throws for git's failure.
    /// </summary>
    Task ContinueAsync(InProgressState state, string? message);

    /// <summary>
    /// Skip (5.1, 5.4) for <paramref name="state"/>, after the user confirmed the banner's
    /// question (asked by <see cref="RepositoryViewModel"/>, as Abort's is): saves the backup
    /// <c>skip-&lt;time&gt;</c> (HEAD) with <c>skip-&lt;time&gt;-changes</c>, then runs
    /// <see cref="IGitRepository.SkipAsync"/> as the local operation "Skip {short}". Completes
    /// when the operation has ended; never throws for git's failure.
    /// </summary>
    Task SkipAsync(InProgressState state);
}
