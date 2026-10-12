namespace VisualCommit.Core.Operations;

/// <summary>
/// One repository's operations (phase 3 design 1, D81): one at a time, first in first out, each
/// on the synchronization context it was queued from, with the watcher paused and the refresh it
/// names afterwards. Disposing it (closing the tab) drops what waits, stops a running network
/// operation and lets a running local one finish all its steps (also one that can be cancelled,
/// <see cref="GitOperation.CanBeCancelled"/>).
/// </summary>
public interface IOperationQueue : IDisposable
{
    /// <summary>
    /// Queues <paramref name="operation"/> and completes when it has ended and its refresh is
    /// done. Never throws for the operation's own failure: exceptions become a
    /// <see cref="OperationOutcome.Failed"/> or <see cref="OperationOutcome.Cancelled"/> result.
    /// Call it on the UI thread: the operation runs on the caller's synchronization context.
    /// A queue that is disposed returns <see cref="OperationOutcome.Cancelled"/> at once.
    /// An operation with the <see cref="GitOperation.DedupeKey"/> of one that waits gets that
    /// one's task; one that runs is never merged with, so the new one waits behind it.
    /// </summary>
    Task<OperationResult> RunAsync(GitOperation operation);

    /// <summary>
    /// Starts <paramref name="operation"/> as a background operation (auto-fetch) only when
    /// nothing runs or waits; returns false otherwise. A background operation is never pre-empted:
    /// operations queued meanwhile wait for it (1.8, D85). While one of them waits, the user can
    /// cancel it (<see cref="OperationSnapshot.CanCancel"/>); closing the tab leaves it running.
    /// </summary>
    bool TryStartInBackground(GitOperation operation);

    /// <summary>Completes when nothing runs or waits: the running operation, all its steps, has ended (app exit, 1.8).</summary>
    Task WhenIdleAsync();

    /// <summary>The running operation, or null.</summary>
    OperationSnapshot? Current { get; }

    /// <summary>How many operations wait behind the running one.</summary>
    int Waiting { get; }

    /// <summary>The last result of an operation that was not in the background, or null.</summary>
    OperationResult? LastResult { get; }

    /// <summary>Cancels the running operation when <see cref="OperationSnapshot.CanCancel"/>; does nothing otherwise (a background operation only while an operation of the user waits for it).</summary>
    void CancelCurrent();

    /// <summary>
    /// Drops the waiting operations: each completes as <see cref="OperationOutcome.Cancelled"/>
    /// without running. The running one goes on. Closing the window does this at once (1.8).
    /// </summary>
    void DropWaiting();

    /// <summary>Raised on any thread when <see cref="Current"/>, <see cref="Waiting"/>, <see cref="LastResult"/> or the running operation's progress changes; listeners move to their own thread.</summary>
    event EventHandler? Changed;

    /// <summary>Raised on any thread when an operation has ended, the background ones included, before its task completes.</summary>
    event EventHandler<OperationFinishedEventArgs>? Finished;
}
