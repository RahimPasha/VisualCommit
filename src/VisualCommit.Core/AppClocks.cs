using VisualCommit.Core.Git;

namespace VisualCommit.Core;

/// <summary>
/// The clocks phase 5's records and backups read (docs/phase-5-design.md 2.3, D173): the record
/// clock gives the times and durations the activity log shows (<c>OperationRecord.DisplayedStart</c>,
/// <c>GitCallRecord.DisplayedStart</c> and their durations), the backup clock names backups and is
/// the clean-up's "now" (<see cref="BackupClock"/>). The app reads the system clock for both. The
/// test harnesses fix them so that pictures repeat: the headless one gives them to
/// <c>AppSession.Start</c>, the real-window pass sets <see cref="RecordClockVariable"/> and
/// <see cref="BackupClockVariable"/> for the app it starts.
/// </summary>
public static class AppClocks
{
    /// <summary>Set to <c>frozen</c>, the record clock stands at <see cref="HarnessTime"/>: every time reads 09:00:00 and every duration 0 ms.</summary>
    public const string RecordClockVariable = "VISUALCOMMIT_RECORD_CLOCK";

    /// <summary>Set to <c>fixed</c>, the backup clock is <see cref="BackupClock.Fixed"/> at <see cref="HarnessTime"/>.</summary>
    public const string BackupClockVariable = "VISUALCOMMIT_BACKUP_CLOCK";

    /// <summary>The time the harnesses' clocks stand at: 2026-01-02 09:00:00 UTC, the time of the commits the app makes in the tests (D72).</summary>
    public static DateTimeOffset HarnessTime { get; } = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The record clock this process's environment asks for: frozen at <see cref="HarnessTime"/>, or the system clock.</summary>
    public static TimeProvider RecordClockFromEnvironment() =>
        string.Equals(Environment.GetEnvironmentVariable(RecordClockVariable), "frozen", StringComparison.Ordinal)
            ? new FrozenClock(HarnessTime)
            : TimeProvider.System;

    /// <summary>The backup clock this process's environment asks for: fixed at <see cref="HarnessTime"/>, or the system clock.</summary>
    public static BackupClock BackupClockFromEnvironment() =>
        string.Equals(Environment.GetEnvironmentVariable(BackupClockVariable), "fixed", StringComparison.Ordinal)
            ? BackupClock.Fixed(HarnessTime)
            : BackupClock.System;
}

/// <summary>A clock that stands still: it always says <paramref name="now"/>, and no time passes between two of its timestamps.</summary>
/// <param name="now">What it says.</param>
public sealed class FrozenClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override long GetTimestamp() => 0;
}
