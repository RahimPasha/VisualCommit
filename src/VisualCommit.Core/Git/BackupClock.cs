namespace VisualCommit.Core.Git;

/// <summary>
/// The clock of the backups (D67, D139, docs/phase-5-design.md 2.3, D173): the time each new
/// backup ref (and discard snapshot) is named with, and the clean-up's "now". The app's is the
/// system clock (<see cref="System"/>); the test harnesses give <see cref="Fixed"/>, so that the
/// backups the checks make get the same names in every run and both passes, still in order, and a
/// scenario's backups are judged against a fixed day.
/// </summary>
public class BackupClock
{
    private readonly TimeProvider _time;

    /// <param name="time">What both times are read from.</param>
    public BackupClock(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>The system clock: a backup is named with the time it is made.</summary>
    public static BackupClock System { get; } = new(TimeProvider.System);

    /// <summary>The clean-up's "now", which backups' ages are measured from.</summary>
    public virtual DateTimeOffset Now => _time.GetUtcNow();

    /// <summary>
    /// A clock that says it is <paramref name="now"/> for the clean-up and names each backup one
    /// second after the one it named before, the first one second after <paramref name="now"/>.
    /// One clock serves every repository of a session, so their backups are ordered as they were made.
    /// </summary>
    public static BackupClock Fixed(DateTimeOffset now) => new FixedBackupClock(now);

    /// <summary>The time the next backup is named with, UTC.</summary>
    public virtual DateTimeOffset NextName() => _time.GetUtcNow();

    private sealed class FixedBackupClock(DateTimeOffset now) : BackupClock(TimeProvider.System)
    {
        private long _named;

        public override DateTimeOffset Now => now;

        public override DateTimeOffset NextName() => now.AddSeconds(Interlocked.Increment(ref _named));
    }
}
