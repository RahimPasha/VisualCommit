namespace VisualCommit.Core.Git;

/// <summary>How a rebase the app started ended (docs/phase-4-design.md 1.4).</summary>
public enum RebaseOutcome
{
    /// <summary>Exit 0, no rebase folder, and git said the branch <c>is up to date</c>: nothing was written.</summary>
    UpToDate,

    /// <summary>Exit 0 and no rebase folder: the branch was rebased (also a fast-forward onto a descendant).</summary>
    Rebased,

    /// <summary>
    /// A rebase folder (<c>rebase-merge/</c> or <c>rebase-apply/</c>) is there after the call, whatever
    /// the exit code (an <c>edit</c> stop exits 0): the rebase stopped, with or without conflicted files.
    /// </summary>
    Stopped,
}

/// <summary>
/// How a rebase ended (1.4): <c>GitRepository.ClassifyRebaseAsync</c> reads it after every rebase
/// command of sections 1, 2, 3, 5 and 6. A refusal (non-zero, no rebase folder) is no result: it
/// throws <see cref="GitException"/>.
/// </summary>
/// <param name="Outcome">Up to date, rebased or stopped.</param>
/// <param name="NewTip">The new HEAD after <see cref="RebaseOutcome.Rebased"/>; null otherwise.</param>
/// <param name="ConflictedFiles">The conflicted files of the status after the call: a stop's, or those of changes put back by <c>rebase.autoStash</c>.</param>
public sealed record RebaseResult(RebaseOutcome Outcome, string? NewTip, int ConflictedFiles)
{
    /// <summary>
    /// Git rebased, but putting back the changes it stashed (<c>rebase.autoStash</c>) conflicted:
    /// stderr had <c>Applying autostash resulted in conflicts</c>, and the changes are also kept in
    /// the stash (1.4). Only with <see cref="RebaseOutcome.Rebased"/>.
    /// </summary>
    public bool AutostashConflicted { get; init; }
}

/// <summary>What an interactive rebase does with one commit of its list (2.2, 2.5).</summary>
public enum RebaseAction
{
    /// <summary>Kept as it is: <c>pick &lt;sha&gt;</c>.</summary>
    Pick,

    /// <summary>Kept with a new message (<see cref="RebaseStep.Message"/>).</summary>
    Reword,

    /// <summary>Joined to its target (the nearest Pick or Reword below, older), adding its message to the group's.</summary>
    Squash,

    /// <summary>Joined to its target, adding nothing to the group's message.</summary>
    Fixup,

    /// <summary>Left out: an explicit <c>drop &lt;sha&gt;</c> line.</summary>
    Drop,
}

/// <summary>One commit of an interactive rebase's plan, in git's order (oldest first, 2.5).</summary>
/// <param name="Sha">The commit's full id.</param>
/// <param name="Action">What the rebase does with it.</param>
/// <param name="Message">
/// A reword's new message, or on a group's target the group's message (the target's own, a
/// squash group's joined or edited one); null to keep the commit's own message. The app's todo
/// writes it to a message file that the group's guarded amend reads (2.5).
/// </param>
public sealed record RebaseStep(string Sha, RebaseAction Action, string? Message);

/// <summary>What Start of an interactive rebase (2.4) or a squash (section 3) hands the git layer.</summary>
/// <param name="Onto">The full SHA the rebase goes onto (<c>git rebase -i &lt;onto&gt;</c>): a target's tip read when the list opened, or the base commit.</param>
/// <param name="ExpectedTip">The branch's tip the list was read from: the rebase is refused when the branch has moved since (D124).</param>
/// <param name="BranchRef">The full name of the branch being rebased (<c>refs/heads/&lt;b&gt;</c>): HEAD must be on it.</param>
/// <param name="StepsOldestFirst">Every listed commit in its new order, oldest first, with its action.</param>
public sealed record RebasePlan(string Onto, string ExpectedTip, string BranchRef, IReadOnlyList<RebaseStep> StepsOldestFirst);

/// <summary>One commit of an interactive rebase's list (2.3).</summary>
/// <param name="Sha">Its full id.</param>
/// <param name="Subject">Its subject.</param>
/// <param name="AuthorName">Its author's name.</param>
/// <param name="Message">Its whole message as git keeps it (the reword dialog's and a squash group's start, 2.2).</param>
public sealed record RebaseListEntry(string Sha, string Subject, string AuthorName, string Message);

/// <summary>The commits an interactive rebase would replay, read when its list opens (2.3).</summary>
/// <param name="Onto">The target's SHA (or the base commit's for "from here"), which Start rebases onto (D124).</param>
/// <param name="BranchTip">The branch's tip when the list was read: Start refuses when it moved.</param>
/// <param name="CommitsOldestFirst">The commits git's own todo would hold, oldest first (the view shows them newest on top).</param>
/// <param name="MergeCount">The merge commits between the target and the branch (<c>git rev-list --merges --count</c>): above 0 the list cannot be made (2.1).</param>
public sealed record RebaseList(string Onto, string BranchTip, IReadOnlyList<RebaseListEntry> CommitsOldestFirst, int MergeCount);

/// <summary>
/// Why a rebase stopped (docs/phase-4-design.md 5.1, 8), read in this order from
/// <c>rebase-merge/</c> (or <c>rebase-apply/</c>): the first that applies.
/// </summary>
public enum RebaseStopKind
{
    /// <summary>Stopped on a pick or reword: with conflicts, or resolved and waiting (the default).</summary>
    Pick,

    /// <summary>An untracked file was in the way: git rescheduled the pick, which is the first line of <c>git-rebase-todo</c> and names <c>stopped-sha</c>.</summary>
    Rescheduled,

    /// <summary>An <c>exec</c> line failed (<c>done</c> ends with it, no <c>stopped-sha</c>), not the app's own amend.</summary>
    ExecFailed,

    /// <summary>The app's guarded amend (an <c>exec</c> that names <c>vc-message-</c>) failed: a hook refused the new message (2.5).</summary>
    OwnAmendFailed,

    /// <summary>An <c>edit</c> line of a todo from elsewhere (<c>amend</c> exists and <c>done</c> ends with <c>edit</c>).</summary>
    Edit,

    /// <summary>The stopped commit changes nothing: <c>CHERRY_PICK_HEAD</c> beside the folder, or a pick with nothing staged and nothing conflicted.</summary>
    Empty,
}
