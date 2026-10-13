// The checks of an undo or redo step (docs/phase-5-design.md 7.3's third column, 7.4, 7.5 step 3,
// 7.6's second table): the app makes those that need no git after every refresh, and the git
// layer simulates the whole sequence inside the step's operation; both go through here. Work
// package WP3 builds them here; until then each throws.

using VisualCommit.Core.Git;

namespace VisualCommit.Core.Undo;

/// <summary>
/// Whether an undo or redo step can run against a repository's state: each effect's check made
/// against the state the effects before it in the step leave, predicted from the state read and
/// the effects' recorded values (7.5 step 3). The first check that fails gives the refusal the
/// app words (7.6); strict order is the history's (7.4).
/// </summary>
public static class UndoChecks
{
    /// <summary>
    /// The checks that need no git (7.4): HEAD, the branch tips, the tags, the stashes, the staged
    /// files, the branches the other worktrees have checked out and the remotes, against what the
    /// app last read. The app makes them after every refresh and keeps the result for the
    /// toolbar's Undo and Redo; the others (file hashes, index entries, the ignored files in the
    /// way, a remote's lease) wait for the step's operation.
    /// </summary>
    /// <param name="step">The step the toolbar would run.</param>
    /// <param name="refs">The refs as last read.</param>
    /// <param name="status">The working tree's status as last read.</param>
    /// <param name="otherWorktrees">The repository's other worktrees and their branches, as last read.</param>
    /// <returns>The first check that fails, or null when every one passes.</returns>
    public static UndoRefusal? FirstRefusalInMemory(UndoStep step, RepoRefs refs, WorkingTreeStatus status, IReadOnlyList<WorktreeInfo> otherWorktrees) =>
        throw new NotImplementedException("The undo's checks are work package WP3's (docs/phase-5-design.md 7.4).");

    /// <summary>
    /// The simulation of the whole step inside its operation (7.5 step 3): every check against
    /// <paramref name="now"/>, read with the step's scope (HEAD, the entry's refs with their
    /// worktrees, the stash list, the configuration keys, the files and the status), each against
    /// the state the effects before it will leave. A check that only git can make once an earlier
    /// effect has run (the two-way merge of a Keep move after a Switch) passes here and is made
    /// when that effect runs.
    /// </summary>
    /// <param name="step">The step to run.</param>
    /// <param name="now">What the step's operation read before it changes anything.</param>
    /// <returns>The first check that fails, or null when every one passes.</returns>
    public static UndoRefusal? FirstRefusal(UndoStep step, UndoState now) =>
        throw new NotImplementedException("The undo's checks are work package WP3's (docs/phase-5-design.md 7.5).");
}
