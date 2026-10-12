using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// Phase 4's operations (docs/phase-4-design.md) as stubs, so that the menus, the banner and the
/// stage panel build against <see cref="IRepositoryActions"/> and <see cref="IInProgressActions"/>
/// before they exist. Nothing calls them yet. Work package WP4 replaces this file with
/// <c>RepositoryOperations.Rebase.cs</c> and <c>.Conflicts.cs</c> and its parts of
/// <c>.Branches.cs</c> and <c>.Remotes.cs</c>.
/// </summary>
public sealed partial class RepositoryOperations
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
}
