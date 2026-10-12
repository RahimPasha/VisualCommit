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
public sealed record GitFailure(GitFailureKind Kind, string Summary, string GitOutput, string CommandText, int? ExitCode);
