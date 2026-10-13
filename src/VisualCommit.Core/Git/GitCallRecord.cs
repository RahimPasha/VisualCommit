namespace VisualCommit.Core.Git;

public enum GitCallOutcome
{
    /// <summary>Git ran to the end; see the exit code for success or failure.</summary>
    Completed,

    /// <summary>The call was cancelled and git was stopped.</summary>
    Cancelled,

    /// <summary>The git executable could not be started.</summary>
    FailedToStart,
}

/// <summary>
/// The record of one git call. Every call the app makes is recorded; the activity log (T4) shows
/// these records.
/// </summary>
/// <param name="StartedAt">When the call started.</param>
/// <param name="Duration">How long it ran.</param>
/// <param name="CommandText">The command as a user would type it.</param>
/// <param name="WorkingDirectory">The folder it ran in, or null for the app's current folder.</param>
/// <param name="Outcome">Whether it completed, was cancelled or never started.</param>
/// <param name="ExitCode">Git's exit code, or null when it did not complete.</param>
/// <param name="StandardOutput">The start of git's standard output, cut off at <see cref="MaxOutputLength"/> characters.</param>
/// <param name="StandardError">The start of git's standard error, cut off the same way.</param>
/// <param name="OperationId">
/// The operation of the queue the call belongs to (<c>op-&lt;n&gt;</c>, phase 3 design 1.7), as
/// <see cref="Operations.GitOperationScope"/> named it when the call was made; null for a call
/// outside an operation (reads, refreshes, clone).
/// </param>
public sealed record GitCallRecord(
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    string CommandText,
    string? WorkingDirectory,
    GitCallOutcome Outcome,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string? OperationId = null)
{
    /// <summary>The most output a record keeps per stream. Longer output is cut off with <see cref="TruncationMarker"/>.</summary>
    public const int MaxOutputLength = 16 * 1024;

    public const string TruncationMarker = "\n... (output cut off)";

    public bool Succeeded => Outcome == GitCallOutcome.Completed && ExitCode == 0;

    /// <summary>The number it took when it started (<see cref="Operations.RecordSequence"/>): the activity log orders its rows by it (docs/phase-5-design.md 2.3).</summary>
    public long Sequence { get; init; }

    /// <summary>When it started by the record clock (<see cref="AppClocks"/>): what the activity log shows.</summary>
    public DateTimeOffset DisplayedStart { get; init; }

    /// <summary>How long it ran by the record clock: what the activity log shows.</summary>
    public TimeSpan DisplayedDuration { get; init; }

    /// <summary>
    /// What it was given on standard input, for a command that records it
    /// (<see cref="GitCommand.RecordsStandardInput"/>: <c>update-ref --stdin</c>'s lines of ref names
    /// and SHAs), cut off and with credentials hidden as the output is; null otherwise.
    /// </summary>
    public string? StandardInput { get; init; }

    /// <summary>It ran with <c>GIT_INDEX_FILE</c> set: on a temporary index (a snapshot's), not the user's.</summary>
    public bool OnTemporaryIndex { get; init; }
}

/// <summary>Collects the record of every git call. Safe to use from any thread.</summary>
public interface IGitCallLog
{
    /// <summary>Raised after a call is recorded, on the thread that finished the call.</summary>
    event EventHandler<GitCallRecord>? Added;

    void Add(GitCallRecord record);

    /// <summary>The recorded calls, oldest first.</summary>
    IReadOnlyList<GitCallRecord> Snapshot();
}
