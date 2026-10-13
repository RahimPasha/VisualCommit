using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;

namespace VisualCommit.Git;

/// <summary>
/// The undo history's reads and steps (docs/phase-5-design.md 7.3, 7.5, 7.7, 0.4, 0.5, D167, D169,
/// D170): what a recorded action's scope names, read before and after it, and every effect of
/// <see cref="UndoEffect"/> with its check, run in a step's order. Work package WP3 builds them
/// here; until then each throws.
/// </summary>
public sealed partial class GitRepository
{
    public Task<UndoState> ReadRefStatesAsync(UndoScope scope, NetworkOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("The undo history's reads are work package WP3's (docs/phase-5-design.md 7.7).");

    public Task<UndoStepResult> RunUndoStepAsync(UndoStep step, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Undo and redo steps are work package WP3's (docs/phase-5-design.md 7.5).");
}
