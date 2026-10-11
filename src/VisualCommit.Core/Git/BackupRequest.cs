namespace VisualCommit.Core.Git;

/// <summary>
/// The backup refs one operation saves before it changes anything (phase 3 design 2.0, Q3,
/// D83): one timestamp, every ref in one <c>git update-ref --stdin</c> transaction, under
/// <c>refs/visualcommit/backup/&lt;kind&gt;-&lt;UTC time&gt;[-&lt;n&gt;][/&lt;name&gt;]</c>.
/// </summary>
/// <param name="Kind">
/// The kind of the design's table: <c>reset</c>, <c>detached</c>, <c>branch</c>,
/// <c>remote-branch</c>, <c>remote</c>, <c>force-push</c>, <c>tag</c>, <c>remote-tag</c>,
/// <c>stash</c> or <c>abort</c>.
/// </param>
/// <param name="Entries">The refs to write: one without a name for the kinds that have none, or one per name.</param>
public sealed record BackupRequest(string Kind, IReadOnlyList<BackupEntry> Entries)
{
    /// <summary>
    /// A snapshot commit of the working tree's changes (<see cref="IGitRepository.SnapshotForHardResetAsync"/>,
    /// <see cref="IGitRepository.SnapshotTrackedChangesAsync"/>), saved in the same batch as
    /// <c>&lt;kind&gt;-&lt;time&gt;-changes</c>; null for none.
    /// </summary>
    public string? ChangesCommit { get; init; }
}

/// <summary>One backup ref of a <see cref="BackupRequest"/>.</summary>
/// <param name="Name">What follows <c>&lt;kind&gt;-&lt;time&gt;/</c>: <c>feature/alpha</c>, <c>origin/main</c>, <c>v1.0</c>; null for a ref without a name (<c>reset-&lt;time&gt;</c>).</param>
/// <param name="Target">The object it points at: a commit, a tag object or a stash commit, as an id or as a name git resolves when it writes the ref (a tag's full ref name keeps the tag object).</param>
public sealed record BackupEntry(string? Name, string Target);

/// <summary>What a <see cref="BackupRequest"/> wrote.</summary>
/// <param name="Time">The one timestamp of the batch, with its retry suffix (<c>20261011T101500123Z-2</c>).</param>
/// <param name="Refs">The full names of the refs written, in the order of the request's entries, the changes snapshot's last.</param>
public sealed record BackupResult(string Time, IReadOnlyList<string> Refs);
