using VisualCommit.App.ViewModels.Workspaces;

namespace VisualCommit.App.ViewModels;

// The workspaces' dashboards in the window (D187, D188, D205; docs/phase-6-design.md 2.1 to 2.3,
// 2.10, 3.7, 3.8). Work package WP-D builds ShowWorkspace, the rows' OpenRequested and the view
// models the window waits for when it closes.
public partial class MainWindowViewModel : IWorkspaceHost
{
    /// <summary>
    /// Opens the workspace <paramref name="workspaceId"/> (2.2): its open dashboard activated, else
    /// <paramref name="from"/> turned into it when empty, else a dashboard tab inserted after the
    /// open ones. A stub until work package WP-D: it does nothing.
    /// </summary>
    public void ShowWorkspace(string workspaceId, RepoTabViewModel? from)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
    }

    /// <summary>The working trees of the tabs with a repository open, in tab order (1.3, 1.7).</summary>
    public IReadOnlyList<string> OpenRepositoryFolders() =>
        [.. Tabs.Where(tab => !tab.IsDashboard && tab.Repository is not null).Select(tab => tab.Repository!.Path)];

    /// <summary>
    /// The window was activated (2.10's trigger 2, Q4): the active dashboard reads its rows last
    /// read 10 s ago or more. <c>MainWindow</c>'s <c>Activated</c> handler calls it.
    /// </summary>
    public void OnWindowActivated() => ActiveTab?.Dashboard?.OnWindowActivated();

    /// <summary>3.8's term of <see cref="NamesRepositoryInDialogs"/>: a dashboard tab is open, so every dialog names its repository.</summary>
    private bool AnyDashboardOpen => Tabs.Any(tab => tab.IsDashboard);
}
