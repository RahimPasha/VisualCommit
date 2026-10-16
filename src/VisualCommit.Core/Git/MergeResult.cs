namespace VisualCommit.Core.Git;

/// <summary>How a merge ended (phase 3 design 2.8 to 2.10).</summary>
public enum MergeResult
{
    /// <summary>Nothing to merge: the branch already contains the other one.</summary>
    UpToDate,

    /// <summary>The branch was moved forward; no merge commit.</summary>
    FastForward,

    /// <summary>A merge commit was made.</summary>
    Merged,

    /// <summary>Git stopped with conflicted files (non-zero exit and at least one conflicted file in the status).</summary>
    Conflicts,
}

/// <summary>Which merge to run (2.8 to 2.10).</summary>
public enum MergeMode
{
    /// <summary><c>git merge</c> as configured: a fast-forward when possible unless <c>merge.ff</c> says otherwise (2.8).</summary>
    Default,

    /// <summary><c>--no-ff</c>: always a merge commit (2.9).</summary>
    NoFastForward,

    /// <summary><c>--ff-only</c> (2.10).</summary>
    FastForwardOnly,
}

/// <summary>How a cherry-pick, a revert or applying a stash ended (2.12, 2.14, 2.25).</summary>
public enum ApplyResult
{
    /// <summary>Applied cleanly.</summary>
    Applied,

    /// <summary>Stopped with conflicted files.</summary>
    Conflicts,

    /// <summary>A cherry-pick or revert that changes nothing stopped with no conflicted file (2.12): an empty pick.</summary>
    Empty,
}

/// <summary>Which way a reset moves (2.11).</summary>
public enum ResetMode
{
    Soft,
    Mixed,
    Hard,
}

/// <summary>How a stash is applied (2.25, 2.31).</summary>
public enum StashApplyMode
{
    /// <summary><c>git stash apply</c>: the stash stays.</summary>
    Apply,

    /// <summary><c>git stash pop</c>: dropped when it applies cleanly.</summary>
    Pop,

    /// <summary>
    /// <c>git stash pop --index</c>, which keeps what was staged staged; when git refuses with
    /// <c>conflicts in index. Try without --index.</c> and changed nothing, a plain pop (2.31).
    /// </summary>
    PopKeepingIndex,
}

/// <summary>The paths a stash would write (<see cref="IGitRepository.ReadStashPathsAsync"/>), repository-relative with <c>/</c>, raw.</summary>
/// <param name="Changed">The paths its changes touch, against its base: both sides of a rename.</param>
/// <param name="Untracked">Its untracked files; empty for a stash made without them.</param>
public sealed record StashPaths(IReadOnlyList<string> Changed, IReadOnlyList<string> Untracked)
{
    /// <summary>The paths its index commit (its second parent) changes against its base, both sides of a rename: what an apply with <c>--index</c> stages, and what the undo history records with the rest.</summary>
    public IReadOnlyList<string> Staged { get; init; } = [];
}

/// <summary>What a pull's integration step did (2.18, D84).</summary>
public enum PullOutcome
{
    /// <summary>HEAD did not move.</summary>
    UpToDate,

    /// <summary>HEAD moved forward over <see cref="PullResult.CommitCount"/> commits.</summary>
    FastForward,

    /// <summary>A merge commit was made.</summary>
    Merged,

    /// <summary>The branch was rebased onto its upstream.</summary>
    Rebased,

    /// <summary>The merge or rebase stopped with conflicted files.</summary>
    Conflicts,

    /// <summary>The fetch found no upstream branch on the remote (deleted there): nothing was integrated.</summary>
    UpstreamGone,
}

/// <summary>How a pull's integration ended (2.18).</summary>
/// <param name="Outcome">What it did.</param>
/// <param name="CommitCount">The commits HEAD gained: <c>git rev-list --count &lt;HEAD before&gt;..HEAD</c>.</param>
public sealed record PullResult(PullOutcome Outcome, int CommitCount)
{
    /// <summary>
    /// The branch's own commits that a pull's rebase left out because its upstream once held
    /// them (git's fork point, docs/phase-4-design.md 0.1 and section 6): the commits that would
    /// have been replayed onto the upstream, less those reachable from
    /// <c>git merge-base --fork-point &lt;upstream&gt; HEAD</c>, counted before the rebase (both
    /// without merges, commits already upstream by patch left out). 0 when the pull did not
    /// rebase or git found no fork point. Set for <see cref="PullOutcome.Rebased"/> and for a
    /// rebase that stopped (<see cref="PullOutcome.Conflicts"/>).
    /// </summary>
    public int LeftOutByForkPoint { get; init; }

    /// <summary>
    /// A pull's rebase that ended rebased, whose changes git stashed at its start
    /// (<c>rebase.autoStash</c>) conflicted when put back: they are also kept in the stash
    /// (docs/phase-4-design.md 1.4, <see cref="RebaseResult.AutostashConflicted"/>).
    /// </summary>
    public bool AutostashConflicted { get; init; }
}

/// <summary>How a pull integrates its upstream (docs/phase-4-design.md section 6, D147).</summary>
public enum PullIntegration
{
    /// <summary>As git is configured (<c>pull.ff</c>, <c>branch.&lt;b&gt;.rebase</c>, <c>pull.rebase</c>): the toolbar's Pull (phase 3, D84).</summary>
    AsConfigured,

    /// <summary>
    /// "Pull from {up} (rebase)", chosen for one pull: a fast-forward when the branch has nothing
    /// of its own, otherwise <c>git rebase</c> without arguments (onto the upstream from its fork
    /// point, as <c>git pull --rebase</c>); git's pull settings are not read.
    /// </summary>
    Rebase,
}
