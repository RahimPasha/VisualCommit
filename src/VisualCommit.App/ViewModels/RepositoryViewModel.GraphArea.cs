using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The lead's hooks of phase 4 (docs/phase-4-design.md 2.2, 11.1 and section 14): what puts the
/// interactive rebase's list and the conflict resolver in the graph area. Stubs until work
/// packages WP5 and WP6 are merged, so that WP4's operations build against them.
/// </summary>
public sealed partial class RepositoryViewModel
{
    /// <summary>
    /// Opens the interactive rebase's list (2.2) for <paramref name="request"/>, holding the
    /// commits <paramref name="list"/> read (2.3), after leaving what the graph area shows
    /// (11.1). Completes when the list is open, or when leaving was cancelled.
    /// </summary>
    public Task ShowInteractiveRebaseAsync(InteractiveRebaseRequest request, RebaseList list) =>
        throw new NotImplementedException("The lead's hook: docs/phase-4-design.md 2.2");

    /// <summary>Opens the conflict resolver on <paramref name="file"/> (11.1, D148), after leaving what the graph area shows.</summary>
    public void OpenResolver(ChangedFile file) =>
        throw new NotImplementedException("The lead's hook: docs/phase-4-design.md 11.1");
}
