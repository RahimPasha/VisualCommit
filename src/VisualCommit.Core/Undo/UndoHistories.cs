using VisualCommit.Core.Git;

namespace VisualCommit.Core.Undo;

/// <summary>
/// The app session's undo histories (docs/phase-5-design.md 7.1), one per worktree, keyed by the
/// worktree's own git folder (<see cref="WorktreeKey"/>): HEAD, the index and the files belong to
/// a worktree, so two tabs on two worktrees of one repository never share a history, two tabs on
/// the same worktree share one, and closing a tab and opening the worktree again in the session
/// keeps it. Use it on the UI thread, as the histories themselves.
/// </summary>
public sealed class UndoHistories
{
    private readonly Dictionary<string, (UndoHistory History, string Common)> _histories = new(WorktreeKey.Comparer);

    /// <summary>The history of the worktree whose git folder is <paramref name="gitDirectory"/> in the repository whose common git folder is <paramref name="commonDirectory"/>; made the first time it is asked for.</summary>
    public UndoHistory For(string gitDirectory, string commonDirectory)
    {
        ArgumentNullException.ThrowIfNull(gitDirectory);
        ArgumentNullException.ThrowIfNull(commonDirectory);
        var key = WorktreeKey.Of(gitDirectory);
        if (!_histories.TryGetValue(key, out var kept))
        {
            kept = (new UndoHistory(), WorktreeKey.Of(commonDirectory));
            _histories.Add(key, kept);
        }

        return kept.History;
    }

    /// <summary>
    /// The backup refs the histories of every worktree of the repository whose common git folder
    /// is <paramref name="commonDirectory"/> need (7.8, D171): its worktrees share the backup refs,
    /// so the clean-up keeps the union.
    /// </summary>
    public IReadOnlySet<string> BackupsOf(string commonDirectory)
    {
        ArgumentNullException.ThrowIfNull(commonDirectory);
        var common = WorktreeKey.Of(commonDirectory);
        return _histories.Values
            .Where(kept => WorktreeKey.Comparer.Equals(kept.Common, common))
            .SelectMany(kept => kept.History.Backups)
            .ToHashSet(StringComparer.Ordinal);
    }
}
