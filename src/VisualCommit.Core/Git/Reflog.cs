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
    /// <summary>No reflog.</summary>
    public static ReflogResult Empty { get; } = new([], 0);
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
