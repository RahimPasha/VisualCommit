namespace VisualCommit.Core.Git;

/// <summary>
/// One backup the backups list shows (docs/phase-5-design.md 5.3, 7.10): one operation's refs
/// under <c>refs/visualcommit/backup/</c>, grouped by kind, time and number as the clean-up groups
/// them (phase 4's <c>BackupNamePattern</c>), with what each ref holds.
/// </summary>
/// <param name="Name">The backup's name: <c>&lt;kind&gt;-&lt;time&gt;[-&lt;n&gt;]</c>.</param>
/// <param name="Kind">Its kind: <c>reset</c>, <c>branch</c>, <c>discard</c>, <c>tag-undone</c>, ….</param>
/// <param name="Time">The time in its name, UTC.</param>
/// <param name="Number">The retry suffix of its name, or null.</param>
/// <param name="Refs">Its refs in git's order of their names: the main one or the named ones, then <c>-changes</c>.</param>
public sealed record BackupInfo(string Name, string Kind, DateTimeOffset Time, int? Number, IReadOnlyList<BackupRefInfo> Refs)
{
    /// <summary>The commit the backup keeps: its first ref's that is not a <c>-changes</c> snapshot, or the snapshot's when it has only that.</summary>
    public BackupRefInfo? Main => Refs.FirstOrDefault(entry => !entry.IsChanges) ?? Refs.FirstOrDefault();
}

/// <summary>One ref of a backup, with the object it points at and that object's message.</summary>
/// <param name="FullName">The full ref name.</param>
/// <param name="Target">The object it points at: a commit, a tag object or a stash commit.</param>
/// <param name="Name">What follows <c>&lt;kind&gt;-&lt;time&gt;[-&lt;n&gt;]/</c> (<c>feature/alpha</c>, <c>origin/main</c>), or null.</param>
/// <param name="IsChanges">The <c>-changes</c> snapshot of the backup.</param>
/// <param name="Subject">The object's subject (a tag object's message), which names a snapshot's kind (<c>VisualCommit: index before a mixed reset</c>).</param>
/// <param name="Body">The rest of its message: a snapshot's paths, one per line.</param>
public sealed record BackupRefInfo(string FullName, string Target, string? Name, bool IsChanges, string Subject, string Body);
