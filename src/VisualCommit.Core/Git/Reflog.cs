namespace VisualCommit.Core.Git;

/// <summary>
/// One reflog as the reflog page shows it (docs/phase-5-design.md 5.2, D180): its entries, newest
/// first in git's order, read through git (never <c>.git/logs</c>, which reftable lacks) with two
/// walks zipped by position, because no placeholder gives an entry's time with its index.
/// </summary>
/// <param name="Entries">The entries, newest first. Empty for a ref that has no reflog (a deleted branch, an unborn HEAD).</param>
/// <param name="Skipped">How many entries git skipped because they name no commit that exists (their numbers are missing): the footer counts them.</param>
public sealed record ReflogResult(IReadOnlyList<ReflogEntry> Entries, int Skipped)
{
    /// <summary>
    /// How many entries of a remote-tracking ref's reflog a force push's lease is looked for in
    /// (7.10): the backups list's item and the restore itself read the same depth.
    /// </summary>
    public const int ForcePushLeaseDepth = 500;

    /// <summary>No reflog.</summary>
    public static ReflogResult Empty { get; } = new([], 0);

    /// <summary>The reflog has more entries than the read's limit, which <see cref="Entries"/> stops at (5.2, docs/phase-5-design.md 15.9): the page's footer says so.</summary>
    public bool More { get; init; }

    /// <summary>
    /// The commit a force push left on the remote (7.10), the lease of its backup's restore: in
    /// this remote-tracking ref's reflog (newest first), the <c>update by push</c> entry whose
    /// older neighbour is <paramref name="backupCommit"/>, the commit the push replaced. Null when
    /// the reflog no longer has it. The backups list enables "Push {b} back to {r}…" by it, and the
    /// restore pushes with it.
    /// </summary>
    public string? ForcePushLease(string backupCommit)
    {
        ArgumentNullException.ThrowIfNull(backupCommit);
        for (var index = 0; index + 1 < Entries.Count; index++)
        {
            if (string.Equals(Entries[index].Message, "update by push", StringComparison.Ordinal)
                && string.Equals(Entries[index + 1].Sha, backupCommit, StringComparison.Ordinal))
            {
                return Entries[index].Sha;
            }
        }

        return null;
    }
}

/// <summary>One entry of a reflog.</summary>
/// <param name="Selector">Git's name for it: <c>HEAD@{3}</c>, <c>refs/heads/main@{0}</c> (<c>%gD</c>).</param>
/// <param name="Index">Its number, the <c>3</c> of <c>HEAD@{3}</c>.</param>
/// <param name="Sha">The commit the ref was set to.</param>
/// <param name="Parents">That commit's parents, first parent first.</param>
/// <param name="Message">Git's message for the entry (<c>%gs</c>): <c>checkout: moving from main to feature/alpha</c>.</param>
/// <param name="Subject">The commit's subject.</param>
/// <param name="Time">When the entry was written, in its own zone.</param>
public sealed record ReflogEntry(
    string Selector,
    int Index,
    string Sha,
    IReadOnlyList<string> Parents,
    string Message,
    string Subject,
    DateTimeOffset Time);
