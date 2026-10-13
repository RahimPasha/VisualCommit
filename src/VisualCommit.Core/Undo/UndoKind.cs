namespace VisualCommit.Core.Undo;

/// <summary>
/// The kinds of action the undo history records (docs/phase-5-design.md 7.2, D168): each has its
/// entry text (7.6) and its undo and redo sequences (7.3, <see cref="UndoSequence"/>). What a
/// kind's action did in detail (a checkout that created a tracking branch, a pop that conflicted
/// and kept its stash) is read from the entry's recorded states, not from more kinds.
/// </summary>
/// <remarks>
/// The backups list's restores (7.10) are recorded like any action, each as the kind whose
/// sequences undo it, with a text of its own (<see cref="UndoScope"/>'s restore scopes):
/// "Restore branch" as <see cref="CreateBranch"/>, "Restore tag" as <see cref="CreateTag"/>,
/// "Restore stash" as <see cref="RestoreStash"/>, "Restore the discarded files…" and "Restore
/// the changes…" as <see cref="Restore"/>, "Restore the staged changes…" as
/// <see cref="RestoreStagedChanges"/>, "Push {b} back to {r}…" as <see cref="Push"/> and a tag's
/// as <see cref="PushTag"/>; "Create branch here…" and "Check out this commit" as theirs.
/// </remarks>
public enum UndoKind
{
    /// <summary>The stage panel's Commit.</summary>
    Commit,

    /// <summary>The stage panel's Commit with Amend ticked.</summary>
    Amend,

    /// <summary>A checkout of every kind but the two below: a local branch, a remote branch (into a tracking branch when none exists), a commit or a tag.</summary>
    Checkout,

    /// <summary>The automatic stash's checkout of a commit the current branch reaches (9.2, D166).</summary>
    AutoStashCheckout,

    /// <summary>D91's "Stash changes and check out".</summary>
    StashAndCheckout,

    /// <summary>"Create branch", with its checkout or without.</summary>
    CreateBranch,

    RenameBranch,

    DeleteBranch,

    /// <summary>A fast-forward of a branch that is not checked out.</summary>
    FastForwardBranch,

    /// <summary>A merge of either kind, the drop's merges and fast-forward of the current branch included.</summary>
    Merge,

    /// <summary>"Check out {B} and merge {A} into it".</summary>
    CheckoutAndMerge,

    /// <summary>A reset of any mode; the mode is captured (<see cref="UndoCaptured.ResetMode"/>).</summary>
    Reset,

    /// <summary>A cherry-pick of one commit or of a list.</summary>
    CherryPick,

    /// <summary>A revert of one commit or of a list.</summary>
    Revert,

    /// <summary>"Rebase {cur} onto …".</summary>
    Rebase,

    /// <summary>"Check out {A} and rebase it onto {B}".</summary>
    CheckoutAndRebase,

    InteractiveRebase,

    Squash,

    /// <summary>A pull of any kind: fast-forward, merge or rebase.</summary>
    Pull,

    Stash,

    ApplyStash,

    PopStash,

    DropStash,

    CreateTag,

    DeleteTag,

    /// <summary>A discard of files, of a hunk, of lines or of everything (D62).</summary>
    Discard,

    /// <summary>
    /// The restore bar's Restore (D62, D75), when it is not the undo of its discard (7.9), and the
    /// backups list's restores of files (7.10), which may put the index back first.
    /// </summary>
    Restore,

    /// <summary>The backups list's "Restore stash" (7.10): a dropped or popped stash stored in the list again.</summary>
    RestoreStash,

    /// <summary>The backups list's "Restore the staged changes…" (7.10): the index put back from a mixed reset's snapshot.</summary>
    RestoreStagedChanges,

    /// <summary>"Ignore {name}" or "Ignore all *.{ext} files".</summary>
    Ignore,

    /// <summary>"Set upstream…", "Change upstream…" or "Stop tracking {up}".</summary>
    Upstream,

    AddRemote,

    RenameRemote,

    RemoveRemote,

    SetRemoteUrl,

    /// <summary>A push of a branch, new or updated, with or without setting its upstream.</summary>
    Push,

    ForcePush,

    DeleteRemoteBranch,

    PushTag,

    DeleteRemoteTag,
}

/// <summary>Which way a step of the history goes (7.1).</summary>
public enum UndoDirection
{
    /// <summary>The entry before the cursor is applied backwards.</summary>
    Undo,

    /// <summary>The entry after the cursor is applied forwards.</summary>
    Redo,
}

/// <summary>Whether a recorded action is finished (7.1, 7.2).</summary>
public enum UndoEntryState
{
    /// <summary>The action ended: the entry can be undone and redone.</summary>
    Done,

    /// <summary>
    /// The action stopped (a merge, a pick, a revert or a rebase with the banner showing): the entry
    /// holds its state before; Continue, Skip or the stage panel's Commit complete it, Abort removes it.
    /// </summary>
    Pending,
}

/// <summary>How a branch or a detached HEAD is moved back or forth (7.3).</summary>
public enum MoveMode
{
    /// <summary>The ref alone moves (<c>update-ref</c>): the index and the files stay, so a commit's changes come back staged.</summary>
    Soft,

    /// <summary>
    /// The files move too, keeping the user's work (<c>update-index --refresh</c>, then
    /// <c>read-tree -m -u</c>, then the ref's <c>update-ref</c>), refused when a file the move
    /// changes has local changes or an ignored file is in the way (D169).
    /// </summary>
    Keep,
}
