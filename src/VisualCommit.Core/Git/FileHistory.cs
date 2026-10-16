namespace VisualCommit.Core.Git;

/// <summary>
/// One commit of a file's history (docs/phase-5-design.md 4.1, 0.1): <c>git log --follow</c> from
/// a fixed start, without merges, newest first, following renames of 50 % similarity or more.
/// </summary>
/// <param name="Commit">The commit, as the graph reads it (its subject, author and dates).</param>
/// <param name="Status">How the file changed in it: added, modified, deleted, renamed, copied or type-changed (the changed-file list's letters).</param>
/// <param name="Path">The file's path at that commit.</param>
/// <param name="OldPath">For a rename or copy, the path it came from; otherwise null.</param>
public sealed record FileHistoryRow(CommitInfo Commit, FileChangeKind Status, string Path, string? OldPath = null);

/// <summary>
/// A file's blame (4.2, 0.1): <c>git blame --porcelain --no-textconv</c> of a commit's version or
/// of the working tree's, line by line. Lines are read as one character per byte (D66), as diff
/// lines are; a line with a NUL makes the file binary (<see cref="IsBinary"/>).
/// </summary>
/// <param name="Lines">The file's lines in order, each with the commit that last changed it.</param>
public sealed record BlameResult(IReadOnlyList<BlameLine> Lines)
{
    /// <summary>The file has no lines (git printed nothing).</summary>
    public bool IsEmpty => Lines.Count == 0;

    /// <summary>A line holds a NUL byte: the page says the file is binary instead of showing it.</summary>
    public bool IsBinary => Lines.Any(line => line.Text.Contains('\0', StringComparison.Ordinal));

    /// <summary>
    /// The path is a submodule (a gitlink, mode 160000), which git cannot blame
    /// (<c>fatal: unsupported file type</c>, <c>no such path … in …</c>): the page says so, as for a
    /// binary file (docs/phase-5-design.md 15.9). No lines then.
    /// </summary>
    public bool IsSubmodule { get; init; }
}

/// <summary>One line of a blame.</summary>
/// <param name="Text">The line's bytes, one character per byte (D66), without its line feed (a CRLF file keeps its <c>\r</c>).</param>
/// <param name="Commit">The commit that last changed it.</param>
public sealed record BlameLine(string Text, BlameCommit Commit);

/// <summary>
/// The commit a blame names for a line (0.1's porcelain): the zero id for a line not committed
/// yet (<see cref="IsUncommitted"/>, author <c>Not Committed Yet</c>).
/// </summary>
/// <param name="Sha">The full commit id.</param>
/// <param name="AuthorName">The author's name, decoded as UTF-8.</param>
/// <param name="AuthorEmail">The author's e-mail address, without the angle brackets.</param>
/// <param name="AuthorTime">When it was authored, in the author's zone.</param>
/// <param name="Summary">The commit's subject, decoded as UTF-8; empty for git's <c>summary (&lt;sha&gt;)</c> of an empty message.</param>
/// <param name="Path">The file's path at that commit (<c>filename</c>, C-unquoted).</param>
/// <param name="PreviousSha">The commit before it that had the line's file (<c>previous</c>), or null at the file's first commit.</param>
/// <param name="PreviousPath">The file's path at <paramref name="PreviousSha"/>, C-unquoted, or null.</param>
/// <param name="IsBoundary">A root commit (<c>boundary</c>).</param>
public sealed record BlameCommit(
    string Sha,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorTime,
    string Summary,
    string Path,
    string? PreviousSha,
    string? PreviousPath,
    bool IsBoundary)
{
    /// <summary>The line is not committed yet: git's zero id.</summary>
    public bool IsUncommitted => Sha.All(c => c == '0');
}

/// <summary>
/// A blame that git stopped because the user's <c>blame.ignoreRevsFile</c> setting names a file
/// that does not exist, which no option on the command line avoids (0.1): the page says so in
/// words of its own.
/// </summary>
/// <param name="file">The file the setting names, as git printed it.</param>
/// <param name="standardError">Git's standard error.</param>
public sealed class BlameIgnoreRevsFileMissingException(string file, string standardError)
    : Exception($"The git setting blame.ignoreRevsFile names {file}, which does not exist.")
{
    /// <summary>The file the setting names.</summary>
    public string File { get; } = file;

    /// <summary>Git's standard error.</summary>
    public string StandardError { get; } = standardError;
}
