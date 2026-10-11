namespace VisualCommit.Core.Diff;

/// <summary>Which two versions of a file a diff compares.</summary>
public enum DiffSide
{
    /// <summary>The index against the working tree: what is not staged yet.</summary>
    Unstaged,

    /// <summary>HEAD against the index: what is staged.</summary>
    Staged,

    /// <summary>A commit's first parent (nothing, for a root commit) against the commit.</summary>
    Commit,
}

/// <summary>One file's diff to read.</summary>
/// <param name="Side">Which versions are compared.</param>
/// <param name="Path">The file's path relative to the repository's top folder, with forward slashes; for a rename, the new path.</param>
/// <param name="Kind">How the file changed, as the status or the commit's file list says.</param>
/// <param name="OldPath">For a rename or copy, where it came from.</param>
/// <param name="IsUntracked">An untracked file (<see cref="DiffSide.Unstaged"/> only): compared with nothing.</param>
/// <param name="Commit">For <see cref="DiffSide.Commit"/>, the commit's id.</param>
/// <param name="Parent">For <see cref="DiffSide.Commit"/>, its first parent, or null for a root commit.</param>
public sealed record DiffTarget(
    DiffSide Side,
    string Path,
    Git.FileChangeKind Kind,
    string? OldPath = null,
    bool IsUntracked = false,
    string? Commit = null,
    string? Parent = null)
{
    /// <summary>Where the version before the change is, or null when there is none (an added file).</summary>
    public FileVersion? Before => Kind is Git.FileChangeKind.Added || IsUntracked
        ? null
        : Side switch
        {
            DiffSide.Unstaged => FileVersion.InIndex(Path),
            DiffSide.Staged => FileVersion.InCommit("HEAD", OldPath ?? Path),
            _ => Parent is null ? null : FileVersion.InCommit(Parent, OldPath ?? Path),
        };

    /// <summary>Where the version after the change is, or null when there is none (a deleted file).</summary>
    public FileVersion? After => Kind is Git.FileChangeKind.Deleted
        ? null
        : Side switch
        {
            DiffSide.Unstaged => FileVersion.InWorkingTree(Path),
            DiffSide.Staged => FileVersion.InIndex(Path),
            _ => FileVersion.InCommit(Commit!, Path),
        };
}

/// <summary>Where a version of a file is kept.</summary>
public enum FileVersionSource
{
    WorkingTree,
    Index,
    Commit,
}

/// <summary>One version of a file: in the working tree, in the index, or in a commit.</summary>
/// <param name="Source">Where it is.</param>
/// <param name="Path">The path relative to the repository's top folder, with forward slashes.</param>
/// <param name="Commit">For <see cref="FileVersionSource.Commit"/>, the commit (an id or a name such as HEAD).</param>
public sealed record FileVersion(FileVersionSource Source, string Path, string? Commit = null)
{
    public static FileVersion InWorkingTree(string path) => new(FileVersionSource.WorkingTree, path);

    public static FileVersion InIndex(string path) => new(FileVersionSource.Index, path);

    public static FileVersion InCommit(string commit, string path) => new(FileVersionSource.Commit, path, commit);
}
