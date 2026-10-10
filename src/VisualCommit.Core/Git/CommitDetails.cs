namespace VisualCommit.Core.Git;

/// <summary>How a file changed in a commit, from the status letters of <c>git diff-tree --name-status</c>.</summary>
public enum FileChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,

    /// <summary>The file changed type, for example from a regular file to a symbolic link.</summary>
    TypeChanged,

    /// <summary>A status letter the app does not know.</summary>
    Unknown,
}

/// <summary>A file a commit changed.</summary>
/// <param name="Path">The path relative to the repository root, with forward slashes. For a rename or copy, the new path.</param>
/// <param name="Kind">How it changed.</param>
/// <param name="OldPath">For a rename or copy, the path it came from; otherwise null.</param>
public sealed record ChangedFile(string Path, FileChangeKind Kind, string? OldPath = null);

/// <summary>Everything the details panel shows about one commit.</summary>
/// <param name="Sha">The full commit id.</param>
/// <param name="Parents">The parents' ids, first parent first.</param>
/// <param name="AuthorName">The author's name.</param>
/// <param name="AuthorEmail">The author's e-mail address.</param>
/// <param name="AuthorDate">When it was authored.</param>
/// <param name="CommitterName">The committer's name.</param>
/// <param name="CommitterEmail">The committer's e-mail address.</param>
/// <param name="CommitDate">When it was committed.</param>
/// <param name="Subject">The first line of the message.</param>
/// <param name="Body">The rest of the message after the blank line that follows the subject, without trailing blank lines. Empty when there is none.</param>
/// <param name="Files">
/// The files the commit changed, compared with its first parent (for a root commit, with an empty
/// tree), with renames detected. In the order git lists them: by path.
/// </param>
public sealed record CommitDetails(
    string Sha,
    IReadOnlyList<string> Parents,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorDate,
    string CommitterName,
    string CommitterEmail,
    DateTimeOffset CommitDate,
    string Subject,
    string Body,
    IReadOnlyList<ChangedFile> Files);
