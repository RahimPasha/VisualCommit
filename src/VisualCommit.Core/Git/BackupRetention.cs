using System.Globalization;

namespace VisualCommit.Core.Git;

/// <summary>
/// How long backups are kept (docs/phase-4-design.md section 9, D137, D139): every backup older
/// than <paramref name="Days"/> goes, then the oldest while more than <paramref name="Count"/>
/// remain.
/// </summary>
/// <param name="Days">The age in days past which a backup is removed (by the time in its name, UTC).</param>
/// <param name="Count">How many backups are kept at most.</param>
public sealed record BackupRetention(int Days, int Count)
{
    /// <summary>The owner's rule: 30 days, 50 backups (owner's answers 3 and 4).</summary>
    public static BackupRetention Default { get; } = new(30, 50);

    /// <summary>Why a backup went, in this rule's words: <c>older than 30 days</c>, <c>more than 50 backups</c>.</summary>
    public string Describe(BackupRemovalReason reason) => reason == BackupRemovalReason.OlderThanDays
        ? $"older than {Plural(Days, "day")}"
        : $"more than {Plural(Count, "backup")}";

    private static string Plural(int count, string noun) =>
        count == 1 ? $"1 {noun}" : string.Create(CultureInfo.InvariantCulture, $"{count} {noun}s");
}

/// <summary>Why the clean-up removed a backup (section 9): what its log line says.</summary>
public enum BackupRemovalReason
{
    /// <summary>"(older than 30 days)".</summary>
    OlderThanDays,

    /// <summary>"(more than 50 backups)".</summary>
    BeyondCount,
}

/// <summary>One backup the clean-up removed (section 9).</summary>
/// <param name="Name">The backup's name, the part after <c>refs/visualcommit/backup/</c> without its <c>-changes</c> or <c>/&lt;name&gt;</c> part: <c>&lt;kind&gt;-&lt;time&gt;[-&lt;n&gt;]</c>.</param>
/// <param name="Refs">Its refs that were deleted, full names.</param>
/// <param name="Reason">Why.</param>
public sealed record RemovedBackup(string Name, IReadOnlyList<string> Refs, BackupRemovalReason Reason)
{
    /// <summary>
    /// What the log file's line and the activity log's note say of it (docs/phase-5-design.md 2.3):
    /// <c>Removed backup reset-20260102T090000000Z (older than 30 days)</c>.
    /// </summary>
    public string Describe(BackupRetention retention)
    {
        ArgumentNullException.ThrowIfNull(retention);
        return $"Removed backup {Name} ({retention.Describe(Reason)})";
    }
}
