namespace VisualCommit.App.ViewModels.Graph;

/// <summary>
/// A branch dropped onto a branch (docs/phase-3-design.md 4g, D87): what the drop menu is built
/// for. The refs are full names, so a local branch <c>origin/main</c> is never taken for the
/// remote branch of that name.
/// </summary>
/// <param name="SourceRef">The dragged branch: <c>refs/heads/feature/alpha</c> or <c>refs/remotes/origin/main</c>.</param>
/// <param name="TargetRef">The branch it was dropped on, local or (for a local source) remote.</param>
/// <param name="X">Where it was dropped, in the window's coordinates: where the menu opens.</param>
/// <param name="Y">See <paramref name="X"/>.</param>
public sealed record BranchDrop(string SourceRef, string TargetRef, double X, double Y);
