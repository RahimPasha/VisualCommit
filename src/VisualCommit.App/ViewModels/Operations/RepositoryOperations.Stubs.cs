using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// Phase 4's operations (docs/phase-4-design.md) as stubs, so that the menus, the banner and the
/// stage panel build against <see cref="IRepositoryActions"/> and <see cref="IInProgressActions"/>
/// before they exist. Nothing calls them yet. Work package WP4 replaces this file with
/// <c>RepositoryOperations.Rebase.cs</c> and <c>.Conflicts.cs</c> and its parts of
/// <c>.Branches.cs</c> and <c>.Remotes.cs</c>. The conflict resolver's <see cref="IConflictActions"/>
/// are the repository's operations too (section 14): <c>.Conflicts.cs</c> implements them.
/// </summary>
public sealed partial class RepositoryOperations : IConflictActions
{
    public Task RebaseAsync(RebaseTarget onto) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 1.3");

    public Task CheckoutAndRebaseAsync(string branchRef, RebaseTarget onto) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 1.3");

    public Task OpenInteractiveRebaseAsync(InteractiveRebaseRequest request) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 2.1");

    public Task SquashAsync(IReadOnlyList<string> commits) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md section 3");

    public Task PullRebaseAsync() =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md section 6");

    public void ResolveConflict(ChangedFile file) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 11.1");

    public Task OpenMergeToolAsync(string path) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 11.9");

    public Task ContinueAsync(InProgressState state, string? message) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 5.4");

    public Task SkipAsync(InProgressState state) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 5.4");

    /// <summary>
    /// The list's Start (2.4), for <see cref="IInteractiveRebaseHost.StartAsync"/>: runs
    /// <paramref name="plan"/> as the operation "Interactive rebase of {b}", with the checkout
    /// first when <paramref name="request"/> asks for it. True when the rebase is done or stopped
    /// (the list closes), false when git refused before anything started (the list stays open).
    /// </summary>
    public Task<bool> RunInteractiveRebaseAsync(RebasePlan plan, InteractiveRebaseRequest request) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 2.4");

    Task<bool> IConflictActions.MarkResolvedAsync(string path, byte[] content) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 11.7");

    Task<bool> IConflictActions.TakeSideAsync(string path, ConflictSide side) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 11.8");

    Task<bool> IConflictActions.RemoveAsync(string path) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 11.8");

    Task<bool> IConflictActions.RunMergeToolAsync(string path) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 11.9");

    Task<byte[]?> IConflictActions.ReadSideAsync(ConflictEntry entry, ConflictSide side) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 11.6");

    Task<ConflictContent> IConflictActions.ReadAsync(ConflictEntry entry) =>
        throw new NotImplementedException("WP4: docs/phase-4-design.md 11.3");
}
