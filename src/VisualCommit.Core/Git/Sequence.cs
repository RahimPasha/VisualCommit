namespace VisualCommit.Core.Git;

/// <summary>
/// How a cherry-pick or revert of a list of commits ended (docs/phase-4-design.md section 4,
/// D136), read from git's files right after the call.
/// </summary>
public enum SequenceStop
{
    /// <summary>Exit 0: every commit was applied.</summary>
    None,

    /// <summary>Stopped on a commit with conflicted files, or with changes staged and nothing conflicted (rerere's <c>autoUpdate</c>, a refusing hook, D128).</summary>
    Conflicts,

    /// <summary>Stopped on a commit that changes nothing (a pick head with the index equal to HEAD, or an empty revert in a list): Skip goes on.</summary>
    Empty,

    /// <summary>
    /// Stopped on a later commit git could not apply without a conflict (an untracked file in the
    /// way: <c>sequencer/</c> with no pick head and no <c>MERGE_MSG</c>, HEAD not at
    /// <c>sequencer/head</c>): Skip and Abort, not Continue (5.1).
    /// </summary>
    Refused,
}

/// <summary>How a list of picks or reverts ended (section 4).</summary>
/// <param name="Stop">Where it stopped, or <see cref="SequenceStop.None"/>.</param>
/// <param name="Applied">The commits applied: the list's length less the commands left in <c>sequencer/todo</c> (the stopped one is still its first line).</param>
/// <param name="Total">The list's length.</param>
public sealed record SequenceResult(SequenceStop Stop, int Applied, int Total);

/// <summary>How Continue or Skip ended (5.4).</summary>
public enum ContinueOutcome
{
    /// <summary>The operation is finished: nothing is in progress any more.</summary>
    Done,

    /// <summary>It went on and stopped again (the next conflict, an empty commit, another stop of 5.1).</summary>
    Stopped,
}

/// <summary>What Continue or Skip did (5.4); a refusal throws <see cref="GitException"/>.</summary>
/// <param name="Outcome">Done, or stopped again.</param>
/// <param name="NewTip">HEAD after it, or null when it could not be read (unborn).</param>
public sealed record ContinueResult(ContinueOutcome Outcome, string? NewTip);
