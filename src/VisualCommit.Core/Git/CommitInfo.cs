namespace VisualCommit.Core.Git;

/// <summary>What a row of the commit graph stands for.</summary>
public enum CommitKind
{
    /// <summary>A commit reachable from a branch, tag, remote branch or HEAD.</summary>
    Commit,

    /// <summary>
    /// A stash entry. Its only parent is the commit the stash was made on; the stash's own index
    /// and untracked-files commits are not part of the graph (D47).
    /// </summary>
    Stash,
}

/// <summary>
/// One commit as the graph shows it: what <c>git log</c> prints for it. The full message, the
/// committer and the changed files are in <see cref="CommitDetails"/>, read when a commit is selected.
/// </summary>
/// <param name="Sha">The full commit id.</param>
/// <param name="Parents">The parents' ids, first parent first. Empty for a root commit. A stash has one: the commit it was made on.</param>
/// <param name="AuthorName">The author's name.</param>
/// <param name="AuthorEmail">The author's e-mail address.</param>
/// <param name="AuthorDate">When the commit was authored, with the author's own offset. The Date column shows this (D43).</param>
/// <param name="CommitDate">When it was committed. <c>git log --date-order</c> orders by this.</param>
/// <param name="Subject">The first line of the message; for a stash, the stash's message.</param>
/// <param name="Kind">A commit or a stash.</param>
public sealed record CommitInfo(
    string Sha,
    IReadOnlyList<string> Parents,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorDate,
    DateTimeOffset CommitDate,
    string Subject,
    CommitKind Kind = CommitKind.Commit)
{
    /// <summary>The first 7 characters of the id, as the SHA column shows it.</summary>
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;
}
