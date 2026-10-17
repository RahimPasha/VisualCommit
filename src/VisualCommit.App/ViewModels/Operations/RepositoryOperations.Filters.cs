// Compare, hide and solo: docs/phase-5-design.md 6.2 (a comparison of branches from their menus
// and a drop's) and section 8 (what the graph hides or solos, kept per repository in the session,
// D159). None is an operation of the queue: a comparison only reads, and a filter reloads the graph
// through the refresh (RepositoryViewModel.ChangeFiltersAsync). The branch operations of
// .Branches.cs (create, rename, delete, a remote branch's tracking branch), the undo's steps and
// the backups list's "Restore branch" (.Undo.cs) call the Follow… members inside their operations
// so that a rename, a delete or a branch created in solo mode keeps the filter right (8.1, 8.4).

using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <inheritdoc cref="IRepositoryActions.CompareBranchesAsync"/>
    public Task CompareBranchesAsync(string fromRef, string toRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(toRef);
        return IsClosed ? Task.CompletedTask : Repository.CompareAsync(fromRef, toRef);
    }

    /// <inheritdoc cref="IRepositoryActions.HideBranchAsync"/>
    public Task HideBranchAsync(string branchRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        if (string.Equals(branchRef, "refs/heads/" + Refs?.Head.BranchName, StringComparison.Ordinal))
        {
            // The menus disable it: the checked-out branch is always shown (8.1).
            Log.Info($"{Repository.Name}: {branchRef} is checked out, so it is not hidden.");
            return Task.CompletedTask;
        }

        return ChangeFiltersAsync((filter, refs) => GraphFilterChanges.Hide(filter, refs, branchRef));
    }

    /// <inheritdoc cref="IRepositoryActions.ShowBranchAsync"/>
    public Task ShowBranchAsync(string branchRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        return ChangeFiltersAsync((filter, refs) => GraphFilterChanges.Show(filter, refs, branchRef));
    }

    /// <inheritdoc cref="IRepositoryActions.SoloBranchAsync"/>
    public Task SoloBranchAsync(string branchRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        return ChangeFiltersAsync((filter, refs) => GraphFilterChanges.Solo(filter, refs, branchRef));
    }

    /// <inheritdoc cref="IRepositoryActions.StopSoloingBranchAsync"/>
    public Task StopSoloingBranchAsync(string branchRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchRef);
        return ChangeFiltersAsync((filter, refs) => GraphFilterChanges.StopSoloing(filter, refs, branchRef));
    }

    /// <inheritdoc cref="IRepositoryActions.ShowAllHiddenBranchesAsync"/>
    public Task ShowAllHiddenBranchesAsync() => ChangeFiltersAsync(GraphFilterChanges.ShowAllHidden);

    /// <inheritdoc cref="IRepositoryActions.StopSoloingAsync"/>
    public Task StopSoloingAsync() => ChangeFiltersAsync(GraphFilterChanges.StopSoloingAll);

    /// <inheritdoc cref="IRepositoryActions.HideRemoteAsync"/>
    public Task HideRemoteAsync(string remote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        return ChangeFiltersAsync((filter, refs) => GraphFilterChanges.HideRemote(filter, refs, remote));
    }

    /// <inheritdoc cref="IRepositoryActions.ShowRemoteAsync"/>
    public Task ShowRemoteAsync(string remote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        return ChangeFiltersAsync((filter, refs) => GraphFilterChanges.ShowRemote(filter, refs, remote));
    }

    /// <summary>
    /// The app renamed the branch <paramref name="oldRef"/> to <paramref name="newRef"/> (full names,
    /// 8.4): its filter entries move with it. Called inside the rename's operation, whose refresh
    /// then loads the graph.
    /// </summary>
    public void FollowRenamedBranch(string oldRef, string newRef) =>
        Repository.FollowBranchChange(filter => GraphFilterChanges.Renamed(filter, oldRef, newRef));

    /// <summary>The app deleted the branch <paramref name="branchRef"/> (8.4): its filter entries go. Called inside the delete's operation.</summary>
    public void FollowDeletedBranch(string branchRef) =>
        Repository.FollowBranchChange(filter => GraphFilterChanges.Deleted(filter, branchRef));

    /// <summary>The app created the branch <paramref name="branchRef"/> (8.1): in solo mode it is soloed too, so that it does not vanish. Called inside the create's operation.</summary>
    public void FollowCreatedBranch(string branchRef) =>
        Repository.FollowBranchChange(filter => GraphFilterChanges.Created(filter, branchRef));

    private Task ChangeFiltersAsync(Func<Core.Session.RepositoryGraphFilter?, Core.Git.RepoRefs, Core.Session.RepositoryGraphFilter?> change) =>
        IsClosed ? Task.CompletedTask : Repository.ChangeFiltersAsync(change);
}
