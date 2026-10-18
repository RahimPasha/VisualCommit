namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// The window as the workspace code sees it (docs/phase-6-design.md 1.3, 2.2, D205):
/// <c>MainWindowViewModel</c> implements it and sets itself as <c>TabServices.WorkspaceHost</c>. The
/// welcome page, <see cref="WorkspaceCommands"/> and the palette open a workspace through it, never
/// through an event.
/// </summary>
public interface IWorkspaceHost
{
    /// <summary>
    /// The one way a workspace opens (2.2): activates its open dashboard, else turns an empty
    /// <paramref name="from"/> tab into it, else inserts a dashboard tab after the open ones; then
    /// activates it and saves the tabs. No repository of the workspace opens in a tab (D188).
    /// </summary>
    /// <param name="workspaceId">The workspace's <c>Workspace.Id</c>.</param>
    /// <param name="from">The tab it was opened from (a welcome page's tab, the palette's active tab), or null.</param>
    void ShowWorkspace(string workspaceId, RepoTabViewModel? from);

    /// <summary>The working trees of the tabs with a repository open, in tab order (dashboards, empty tabs and tabs still opening left out): "Add open tabs" (1.7).</summary>
    IReadOnlyList<string> OpenRepositoryFolders();
}
