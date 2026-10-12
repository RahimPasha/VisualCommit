namespace VisualCommit.Core.Git;

/// <summary>
/// What <c>git status</c> says about the index and the working tree at one moment: the staged
/// files (HEAD → index) and the unstaged ones (index → working tree, untracked and conflicted
/// files included). Each list is in the byte order of the paths, as git sorts them.
/// </summary>
/// <param name="Staged">The files whose index entry differs from HEAD's. A rename has its old path.</param>
/// <param name="Unstaged">The files whose working copy differs from the index, the untracked files (as <see cref="FileChangeKind.Added"/>) and the conflicted ones.</param>
/// <param name="Untracked">The paths of <paramref name="Unstaged"/> that git does not track.</param>
public sealed record WorkingTreeStatus(
    IReadOnlyList<ChangedFile> Staged,
    IReadOnlyList<ChangedFile> Unstaged,
    IReadOnlySet<string> Untracked)
{
    /// <summary>A clean working tree.</summary>
    public static WorkingTreeStatus Clean { get; } = new([], [], new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// The merge, cherry-pick, revert, rebase or <c>git am</c> in progress, read from the files git
    /// leaves in the git folder (phase 3 design 5, D89); <see cref="InProgressState.None"/> when
    /// there is none.
    /// </summary>
    public InProgressState InProgress { get; init; } = InProgressState.None;

    /// <summary>
    /// The conflicted paths with their kind, modes and stage ids (docs/phase-4-design.md 0.4), in
    /// the byte order of the paths: one per <see cref="FileChangeKind.Conflicted"/> entry of
    /// <see cref="Unstaged"/>. Empty when nothing conflicts.
    /// </summary>
    public IReadOnlyList<ConflictEntry> Conflicts { get; init; } = [];

    /// <summary>Anything to show in the working-changes row (D60).</summary>
    public bool HasChanges => Staged.Count > 0 || Unstaged.Count > 0;

    /// <summary>Whether <paramref name="path"/> is untracked.</summary>
    public bool IsUntracked(string path) => Untracked.Contains(path);

    /// <summary>The conflict at <paramref name="path"/>, or null when the path is not conflicted.</summary>
    public ConflictEntry? FindConflict(string path) =>
        Conflicts.FirstOrDefault(conflict => string.Equals(conflict.Path, path, StringComparison.Ordinal));

    /// <summary>
    /// Equal when both lists hold the same files with the same kinds, in the same order, the same
    /// conflicts with the same stages, and the same operation is in progress.
    /// </summary>
    public bool SameAs(WorkingTreeStatus other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Staged.SequenceEqual(other.Staged) && Unstaged.SequenceEqual(other.Unstaged) && Untracked.SetEquals(other.Untracked)
            && Conflicts.SequenceEqual(other.Conflicts)
            && InProgress == other.InProgress;
    }
}
