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
public sealed record GitCallRecord(
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    string CommandText,
    string? WorkingDirectory,
    GitCallOutcome Outcome,
    int? ExitCode,
    string StandardOutput,
    string StandardError)
{
    /// <summary>The most output a record keeps per stream. Longer output is cut off with <see cref="TruncationMarker"/>.</summary>
    public const int MaxOutputLength = 16 * 1024;

    public const string TruncationMarker = "\n... (output cut off)";

    public bool Succeeded => Outcome == GitCallOutcome.Completed && ExitCode == 0;
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
