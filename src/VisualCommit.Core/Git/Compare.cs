namespace VisualCommit.Core.Git;

/// <summary>How a comparison chose the side it compared from (docs/phase-5-design.md 6.3, D177).</summary>
public enum CompareBase
{
    /// <summary>The two trees, <c>From</c> against <c>To</c> (two-dot): two commits, or "All differences".</summary>
    Trees,

    /// <summary>The changes on <c>To</c> since it left <c>From</c> (three-dot): the merge base against <c>To</c>.</summary>
    MergeBase,

    /// <summary>Three-dot was asked, and the two have no common history: all differences instead.</summary>
    NoCommonHistory,

    /// <summary>Three-dot over several merge bases (a criss-cross): the one git prints.</summary>
    SeveralMergeBases,
}

/// <summary>
/// What two commits or branches differ in (6.3, 6.4): the names resolved to commits once, so that
/// refs moving meanwhile change nothing, and the changed files from the side compared from.
/// </summary>
/// <param name="From">The commit <c>From</c> named.</param>
/// <param name="To">The commit <c>To</c> named.</param>
/// <param name="Base">How the side compared from was chosen.</param>
/// <param name="Old">The side the files are compared from: the merge base in three-dot, <paramref name="From"/> otherwise. A file's diff uses it as its parent.</param>
/// <param name="Files">The changed files (<c>diff-tree -r -z --name-status -M</c>), by path; a rename's old path only from its own entry.</param>
/// <param name="RenamesSkipped">Git skipped rename detection (<c>diff.renameLimit</c>): renames show as removed and added files.</param>
/// <param name="Counts">The commits only on each side, when counted (branches); null otherwise.</param>
public sealed record CompareResult(
    string From,
    string To,
    CompareBase Base,
    string Old,
    IReadOnlyList<ChangedFile> Files,
    bool RenamesSkipped,
    CompareCounts? Counts)
{
    /// <summary>Both sides are the same commit: nothing to compare, without git.</summary>
    public bool IsSameCommit => string.Equals(From, To, StringComparison.Ordinal);
}

/// <summary>The commits only on each side of a comparison: <c>rev-list --left-right --count &lt;from&gt;...&lt;to&gt;</c>.</summary>
/// <param name="OnlyOnFrom">Commits <c>From</c> has that <c>To</c> lacks.</param>
/// <param name="OnlyOnTo">Commits <c>To</c> has that <c>From</c> lacks.</param>
public sealed record CompareCounts(int OnlyOnFrom, int OnlyOnTo);
