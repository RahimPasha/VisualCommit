namespace VisualCommit.Core.Git;

/// <summary>What git operation is in progress in the working tree (phase 3 design 5, D89).</summary>
public enum InProgressKind
{
    /// <summary>None: there may still be conflicted files (a stash that did not apply cleanly).</summary>
    None,

    /// <summary><c>MERGE_HEAD</c> exists.</summary>
    Merge,

    /// <summary><c>CHERRY_PICK_HEAD</c> exists, or a sequencer of picks stopped between commits.</summary>
    CherryPick,

    /// <summary><c>REVERT_HEAD</c> exists, or a sequencer of reverts stopped between commits.</summary>
    Revert,

    /// <summary><c>rebase-merge/</c>, or <c>rebase-apply/</c> without <c>applying</c>.</summary>
    Rebase,

    /// <summary><c>git am</c>: <c>rebase-apply/</c> with <c>applying</c>.</summary>
    ApplyMailbox,
}

/// <summary>The operation in progress, as the conflict banner shows it (phase 3 design 5).</summary>
/// <param name="Kind">Which operation.</param>
/// <param name="Commit">The commit being picked or reverted (<c>CHERRY_PICK_HEAD</c>, <c>REVERT_HEAD</c>) or merged (<c>MERGE_HEAD</c>); null when git names none.</param>
/// <param name="Subject">A merge's message (<c>MERGE_MSG</c>'s first line), or the picked or reverted commit's subject.</param>
/// <param name="Branch">A rebase's branch (<c>head-name</c> without <c>refs/heads/</c>).</param>
/// <param name="Onto">A rebase's new base (<c>onto</c>).</param>
/// <param name="Remaining">The commits a sequence (from a terminal) or a rebase has still to apply after the stopped one; 0 without one.</param>
/// <param name="Empty">A cherry-pick or revert that changes nothing: its index equals HEAD's tree and nothing conflicts (2.12).</param>
public sealed record InProgressState(
    InProgressKind Kind,
    string? Commit,
    string? Subject,
    string? Branch,
    string? Onto,
    int Remaining,
    bool Empty)
{
    /// <summary>Nothing in progress.</summary>
    public static InProgressState None { get; } = new(InProgressKind.None, null, null, null, null, 0, false);

    /// <summary>
    /// A cherry-pick or revert sequence (started in a terminal: <c>sequencer/</c> exists) whose
    /// HEAD is no longer where the sequencer left it (<c>sequencer/abort-safety</c>), as after the
    /// stopped pick was committed. Git's <c>--abort</c> then ends the sequence and leaves HEAD and
    /// the commits made so far as they are, instead of going back to where it started (D110).
    /// </summary>
    public bool AbortKeepsHead { get; init; }

    // Phase 4 (docs/phase-4-design.md section 8). Init properties with defaults, so that phase 3's
    // constructions keep compiling; the in-progress read fills them (work package WP2).

    /// <summary>
    /// A cherry-pick or revert of a list: a <c>sequencer/</c> folder exists (8). With
    /// <see cref="HasPickHead"/> false, the list is paused between commits and the stage panel
    /// fills no message.
    /// </summary>
    public bool Sequenced { get; init; }

    /// <summary>A pick head is there: <c>CHERRY_PICK_HEAD</c> or <c>REVERT_HEAD</c>, read as file or ref (8.1).</summary>
    public bool HasPickHead { get; init; }

    /// <summary>
    /// For a rebase, why it stopped (8: <see cref="RebaseStopKind.Rescheduled"/>,
    /// <see cref="RebaseStopKind.ExecFailed"/>, <see cref="RebaseStopKind.OwnAmendFailed"/>,
    /// <see cref="RebaseStopKind.Edit"/>, <see cref="RebaseStopKind.Empty"/>, else
    /// <see cref="RebaseStopKind.Pick"/>); null for any other operation.
    /// </summary>
    public RebaseStopKind? StopKind { get; init; }

    /// <summary>
    /// For a rebase, the first line of <c>git-rebase-todo</c> is <c>fixup</c>, <c>f</c>,
    /// <c>squash</c> or <c>s</c>: the stopped commit is the target of a group (5.1).
    /// </summary>
    public bool NextIsSquash { get; init; }

    /// <summary>
    /// For a rebase stopped inside a group the app wrote (its <c>exec … vc-message-&lt;n&gt;</c>
    /// line still waits in <c>git-rebase-todo</c> before the next <c>label</c>), the group's number
    /// <c>&lt;n&gt;</c> (2.5, 5.3); null otherwise.
    /// </summary>
    public int? Group { get; init; }

    /// <summary>
    /// For a rebase, the commands that apply a commit (<c>pick</c>, <c>reword</c>, <c>edit</c>,
    /// <c>squash</c>, <c>fixup</c>, <c>merge</c> and their letters) in <c>done</c>, a rescheduled
    /// pick counted as the current one: the banner's <c>{k}</c> of <c>{k} of {n}</c> (5.2).
    /// <c>label</c> and <c>exec</c> lines do not count. 0 for any other operation.
    /// </summary>
    public int Done { get; init; }

    /// <summary>For a rebase, <see cref="Done"/> plus the same commands left in <c>git-rebase-todo</c>: the banner's <c>{n}</c>. 0 for any other operation.</summary>
    public int Total { get; init; }

    /// <summary>An operation is in progress.</summary>
    public bool IsActive => Kind != InProgressKind.None;
}
