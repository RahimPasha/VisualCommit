namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// One workspace as the welcome page lists it (docs/phase-6-design.md 1.3, 13.1): an item of
/// <c>TabServices.WorkspaceItems</c>, which every welcome page shares as it shares the recent
/// list. Work package WP-W keeps that list: rebuilt on <c>IWorkspaceStore.Changed</c>, sorted by
/// name (ignoring case), then id.
/// </summary>
/// <param name="Id">The workspace's <c>Workspace.Id</c>.</param>
/// <param name="Name">Its name.</param>
/// <param name="CountText"><c>{N} repositories</c>, <c>1 repository</c> or <c>No repositories</c>.</param>
/// <param name="ToolTipText">Its repositories' folder names in order, joined by <c>, </c>, at most 8, then <c>, …</c>; or <c>No repositories yet</c>.</param>
public sealed record WorkspaceItem(string Id, string Name, string CountText, string ToolTipText);
