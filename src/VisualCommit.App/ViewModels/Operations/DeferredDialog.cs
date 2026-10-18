using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// A result's dialog kept for later (docs/phase-6-design.md 4.6, D195, D211): during a bulk run no
/// dialog opens for a result, and the row keeps what the dialog would show for its Details…,
/// which opens the identical dialog (<c>RepositoryOperations.ShowDeferredAsync</c>, work package WP-B).
/// </summary>
/// <param name="Text">The operation's texts (2.33).</param>
/// <param name="Display">The failure's display as <c>ShowResultAsync</c> chose it, or null for lock files.</param>
/// <param name="Failure">The failure (git's output, the command), or null for lock files.</param>
/// <param name="StaleLockFiles">The lock files a cancel left (1.8), or empty.</param>
/// <param name="Listed">Their stamps when they were found (<c>LockFiles.Stamp</c>), so that "Remove lock files" later removes only files that are still those; null without lock files.</param>
/// <param name="OperationId">The operation's id, for the git call the lock files' dialog names.</param>
public sealed record DeferredDialog(
    OperationText Text,
    FailureDisplay? Display,
    GitFailure? Failure,
    IReadOnlyList<string> StaleLockFiles,
    IReadOnlyDictionary<string, (DateTime Time, long Length)?>? Listed,
    string? OperationId);

/// <summary>
/// One row's part in a bulk run (4.6): what the bulk entry points <c>FetchAllAsync(BulkPart)</c>
/// and <c>PullAsync(BulkPart)</c> (work package WP-B) put in their <see cref="RunOptions"/>.
/// </summary>
/// <param name="Cancellation">The run's cancellation (<see cref="RunOptions.Cancellation"/>): Cancel all.</param>
/// <param name="PromptGroup">The run's prompt group (<see cref="RunOptions.PromptGroup"/>).</param>
/// <param name="Defer">Where the row's dialog goes (<see cref="RunOptions.Defer"/>).</param>
public sealed record BulkPart(CancellationToken Cancellation, PromptGroup PromptGroup, Action<DeferredDialog> Defer);
