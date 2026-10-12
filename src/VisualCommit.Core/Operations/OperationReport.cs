namespace VisualCommit.Core.Operations;

/// <summary>How an operation ended (phase 3 design 1.2, D95).</summary>
public enum OperationOutcome
{
    Succeeded,

    /// <summary>It stopped with conflicts: not a failure (D95); the banner shows them.</summary>
    Conflicts,

    Failed,

    Cancelled,
}

/// <summary>What an operation's <see cref="GitOperation.Run"/> returns.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="Message">What the status bar says: the success or conflict message of section 2.</param>
public sealed record OperationReport(OperationOutcome Outcome, string Message)
{
    /// <summary>
    /// For an operation that ends <see cref="OperationOutcome.Failed"/> without an exception (an
    /// empty cherry-pick, 2.12; changes not re-applied, 2.31): what the error dialog shows.
    /// </summary>
    public GitFailure? Failure { get; init; }

    public static OperationReport Done(string message) => new(OperationOutcome.Succeeded, message);

    public static OperationReport Conflicts(string message) => new(OperationOutcome.Conflicts, message);

    public static OperationReport Failed(string message, GitFailure failure) =>
        new(OperationOutcome.Failed, message) { Failure = failure };
}

/// <summary>
/// How an operation ended, as the queue reports it: the report, or the failure or cancellation
/// it turned an exception into.
/// </summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="Message">The status bar's text: the report's message, "&lt;Title&gt; failed" or "&lt;Title&gt; cancelled".</param>
/// <param name="Failure">For <see cref="OperationOutcome.Failed"/>: git's output and the command (C11).</param>
public sealed record OperationResult(OperationOutcome Outcome, string Message, GitFailure? Failure)
{
    /// <summary>
    /// After a network operation that was cancelled: the lock files that git, stopped while it
    /// updated refs, left behind (1.8). Empty otherwise.
    /// </summary>
    public IReadOnlyList<string> StaleLockFiles { get; init; } = [];
}

/// <summary>What the status bar shows of the running operation.</summary>
/// <param name="Title">The operation's title.</param>
/// <param name="RunningText">"Fetching from origin…".</param>
/// <param name="Kind">Local or network.</param>
/// <param name="IsBackground">Auto-fetch: nothing of it is shown.</param>
/// <param name="Errors">Where its failure would be shown; a stage-panel operation leaves no message in the status bar (1.9).</param>
/// <param name="Progress">Git's last progress line, or null before one came.</param>
/// <param name="CanCancel">Cancel is offered: a network operation (or a local one that can be cancelled, <see cref="GitOperation.CanBeCancelled"/>) that is not in the background, before <see cref="OperationContext.EndCancelWindow"/>, and not cancelled yet.</param>
/// <param name="HasSeveralSteps">The operation's <see cref="GitOperation.HasSeveralSteps"/>, for the "Finishing operations" dialog (1.8).</param>
public sealed record OperationSnapshot(
    string Title,
    string RunningText,
    OperationKind Kind,
    bool IsBackground,
    ErrorDisplay Errors,
    OperationProgress? Progress,
    bool CanCancel,
    bool HasSeveralSteps = false);

/// <summary>One operation has ended: what it was and how it ended.</summary>
public sealed class OperationFinishedEventArgs(GitOperation operation, OperationResult result) : EventArgs
{
    public GitOperation Operation { get; } = operation;

    public OperationResult Result { get; } = result;
}
