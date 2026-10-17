namespace VisualCommit.Core.Operations;

/// <summary>What kind of failure git reported, read from its English text (phase 3 design 1.6, D30).</summary>
public enum GitFailureKind
{
    /// <summary>Anything else.</summary>
    Other,

    /// <summary>A checkout refused because tracked files would be overwritten, and no untracked file is in the way: "Stash changes and check out" is offered (2.31).</summary>
    CheckoutBlockedByLocalChanges,

    /// <summary>A checkout refused because untracked files are in the way (alone or with tracked ones).</summary>
    CheckoutBlockedByUntrackedFiles,

    /// <summary>
    /// A checkout refused because it would delete a folder that holds untracked files (<c>Updating
    /// the following directories would lose untracked files in them</c>, docs/phase-5-design.md 9.3).
    /// </summary>
    CheckoutBlockedByUntrackedFolder,

    /// <summary>A push rejected because the remote has commits the branch lacks (<c>fetch first</c>, <c>non-fast-forward</c>).</summary>
    PushRejected,

    /// <summary>A push refused by its lease (<c>stale info</c>, <c>remote ref updated since checkout</c>).</summary>
    PushRejectedStale,

    /// <summary>Signing in to the remote failed.</summary>
    AuthenticationFailed,

    /// <summary><c>Not possible to fast-forward</c>.</summary>
    NotFastForward,

    /// <summary>Another git holds the index lock.</summary>
    IndexLocked,

    /// <summary>
    /// A merge, cherry-pick, revert or pull refused because changes are in the way of the files it
    /// writes: git's <c>… would be overwritten by merge:</c> blocks, which name them
    /// (<see cref="GitFailure.BlockedPaths"/>; docs/phase-5-design.md 9.3).
    /// </summary>
    MergeBlockedByLocalChanges,

    /// <summary>
    /// A merge, cherry-pick or revert refused because something is staged, naming no conflict:
    /// ort's <c>Your local changes to the following files would be overwritten by merge:</c> with
    /// the names indented by two spaces, or <c>your local changes would be overwritten by
    /// cherry-pick.</c> (<c>by revert.</c>) (docs/phase-5-design.md 9.3, the report's PD10).
    /// </summary>
    MergeBlockedByStagedChanges,
}

/// <summary>
/// A failed operation, as the error dialog shows it (C11): the operation's own sentence, git's
/// output cleaned of progress and credentials, and the command that failed.
/// </summary>
/// <param name="Kind">What kind of failure it was.</param>
/// <param name="Summary">The operation's one-line sentence ("Git refused to push main to origin."); the queue gives "&lt;Title&gt; failed." and the operation replaces it.</param>
/// <param name="GitOutput">Git's standard error without progress lines, or its standard output when that is empty; credentials hidden; never shortened.</param>
/// <param name="CommandText">The failing call as a user would type it, credentials hidden. Empty when no git call failed.</param>
/// <param name="ExitCode">Git's exit code, or null when git did not run to the end.</param>
public sealed record GitFailure(GitFailureKind Kind, string Summary, string GitOutput, string CommandText, int? ExitCode)
{
    /// <summary>
    /// The paths git named under the headers of a refusal for local changes or files in the way
    /// (docs/phase-5-design.md 9.3), each once, raw, in git's order: what the error dialog counts.
    /// Read from <see cref="GitOutput"/> by <see cref="GitFailureClassifier.BlockedPaths"/>, so
    /// every failure has them however it was made; empty for any other failure.
    /// </summary>
    public IReadOnlyList<string> BlockedPaths { get; init; } = GitFailureClassifier.BlockedPaths(GitOutput);
}
