using VisualCommit.Core.Hosting;

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

    /// <summary>
    /// A success that the status bar shows in the warning colour (<see cref="Warning(string)"/>):
    /// something the user should know happened, though nothing failed and nothing conflicts.
    /// </summary>
    public bool IsWarning { get; init; }

    /// <summary>
    /// What the status bar offers beside a success's message (D317, docs/phase-7a-design.md 12.2:
    /// "Open in browser" after a created pull request); the queue passes it on only when the report
    /// is <see cref="OperationOutcome.Succeeded"/>.
    /// </summary>
    public OperationOffer? Offer { get; init; }

    public static OperationReport Done(string message) => new(OperationOutcome.Succeeded, message);

    /// <summary>
    /// <see cref="OperationOutcome.Succeeded"/> with a message in the warning colour
    /// (docs/phase-4-design.md 10.2: a pull whose rebase left out commits the upstream once had):
    /// logged as a success, and a success for whatever counts successes (auto-fetch, D85).
    /// </summary>
    public static OperationReport Warning(string message) => new(OperationOutcome.Succeeded, message) { IsWarning = true };

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

    /// <summary>A success whose message the status bar shows in the warning colour (<see cref="OperationReport.Warning"/>).</summary>
    public bool IsWarning { get; init; }

    /// <summary>
    /// After a failed operation: the account whose token git erased through the app's helper
    /// (<see cref="PromptScope.RefusedAccount"/>, ruling 4, docs/phase-7a-design.md 0.2), for the
    /// error dialog. Null otherwise.
    /// </summary>
    public AccountKey? RefusedAccount { get; init; }

    /// <summary>
    /// After a succeeded operation whose report carried one (<see cref="OperationReport.Offer"/>,
    /// D317): what the status bar offers beside its message. Null for every other result.
    /// </summary>
    public OperationOffer? Offer { get; init; }
}

/// <summary>
/// A button the status bar shows beside an operation's success (D317, docs/phase-7a-design.md 12.2):
/// its text and what it runs. General, so that no hosting type reaches Core or the status bar.
/// </summary>
/// <param name="Label">The button's text and name, "Open in browser".</param>
/// <param name="Run">What a click runs, on the UI thread.</param>
public sealed record OperationOffer(string Label, Func<Task> Run);

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
