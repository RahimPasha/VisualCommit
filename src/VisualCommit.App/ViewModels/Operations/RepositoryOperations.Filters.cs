// Compare, hide and solo: docs/phase-5-design.md 6.2 (a comparison of branches from their menus
// and a drop's) and section 8 (what the graph hides or solos, kept per repository in the session,
// D159). Work package WP7 builds them here; until then each throws. None is an operation of the
// queue: a comparison only reads, and a filter reloads the graph through the refresh.

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <inheritdoc cref="IRepositoryActions.CompareBranchesAsync"/>
    public Task CompareBranchesAsync(string fromRef, string toRef) =>
        throw new NotImplementedException("Comparing branches is work package WP7's (docs/phase-5-design.md 6.2).");

    /// <inheritdoc cref="IRepositoryActions.HideBranchAsync"/>
    public Task HideBranchAsync(string branchRef) =>
        throw new NotImplementedException("Hiding a branch is work package WP7's (docs/phase-5-design.md 8.1).");

    /// <inheritdoc cref="IRepositoryActions.ShowBranchAsync"/>
    public Task ShowBranchAsync(string branchRef) =>
        throw new NotImplementedException("Showing a hidden branch is work package WP7's (docs/phase-5-design.md 8.1).");

    /// <inheritdoc cref="IRepositoryActions.SoloBranchAsync"/>
    public Task SoloBranchAsync(string branchRef) =>
        throw new NotImplementedException("Soloing a branch is work package WP7's (docs/phase-5-design.md 8.1).");

    /// <inheritdoc cref="IRepositoryActions.StopSoloingBranchAsync"/>
    public Task StopSoloingBranchAsync(string branchRef) =>
        throw new NotImplementedException("Soloing a branch is work package WP7's (docs/phase-5-design.md 8.1).");

    /// <inheritdoc cref="IRepositoryActions.ShowAllHiddenBranchesAsync"/>
    public Task ShowAllHiddenBranchesAsync() =>
        throw new NotImplementedException("Showing hidden branches is work package WP7's (docs/phase-5-design.md 8.3).");

    /// <inheritdoc cref="IRepositoryActions.StopSoloingAsync"/>
    public Task StopSoloingAsync() =>
        throw new NotImplementedException("Soloing is work package WP7's (docs/phase-5-design.md 8.3).");

    /// <inheritdoc cref="IRepositoryActions.HideRemoteAsync"/>
    public Task HideRemoteAsync(string remote) =>
        throw new NotImplementedException("Hiding a remote's branches is work package WP7's (docs/phase-5-design.md 8.1).");

    /// <inheritdoc cref="IRepositoryActions.ShowRemoteAsync"/>
    public Task ShowRemoteAsync(string remote) =>
        throw new NotImplementedException("Hiding a remote's branches is work package WP7's (docs/phase-5-design.md 8.1).");
}
