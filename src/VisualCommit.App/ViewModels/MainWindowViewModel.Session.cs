using System.Collections.Specialized;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels;

// The session's tabs (D46, docs/phase-6-design.md 1.11): what is restored at start and what is
// saved, the dashboards among them; the active tab's index, which the tab strip's panel keeps in
// view (13.3); and what the workspace commands ask the window about its open dashboards (1.2, 1.6).
public partial class MainWindowViewModel
{
    private bool _watchingTabs;

    /// <summary>The active tab's place in <see cref="Tabs"/> (13.3): <c>RepoTabStripPanel</c> brings it into view.</summary>
    public int ActiveTabIndex => ActiveTab is null ? -1 : Tabs.IndexOf(ActiveTab);

    /// <summary>
    /// Whether the workspace <paramref name="workspaceId"/>'s dashboard is open in a tab
    /// (<see cref="IWorkspaceHost.HasOpenDashboard"/>: the deletion's "Its dashboard tab closes.", 1.6).
    /// </summary>
    public bool HasOpenDashboard(string workspaceId) => FindOpenDashboard(workspaceId) is not null;

    /// <summary>
    /// The working directory of the open dashboard's row whose saved folder is
    /// <paramref name="savedPath"/>, while its repository is open (<see cref="IWorkspaceHost.OpenedWorkingDirectoryOf"/>,
    /// 1.2's <c>WorkspaceMembership</c>); null without such a dashboard or row.
    /// </summary>
    public string? OpenedWorkingDirectoryOf(string workspaceId, string savedPath) =>
        FindOpenDashboard(workspaceId)?.OpenedWorkingDirectoryOf(savedPath);

    /// <summary>
    /// The tabs of <paramref name="saved"/>, there from the first frame (<c>InitializeAsync</c>
    /// opens the repositories once git has been found), and the active one (1.11):
    /// <list type="bullet">
    /// <item>a tab whose workspace id the store lacks (deleted, or <c>workspaces.json</c> set aside
    /// or unreadable) is dropped, and so is a second dashboard of one workspace; a dashboard's
    /// saved path is ignored;</item>
    /// <item>the dashboards come first in their saved order, the other tabs after in theirs (a
    /// stable partition: a hand-edited file cannot put a dashboard in the middle);</item>
    /// <item>the saved active tab is mapped to its tab: the first tab when it was dropped, one empty
    /// tab when all were (or none was saved).</item>
    /// </list>
    /// A restored dashboard is made by <see cref="RepoTabViewModel.ShowDashboard"/> and has no
    /// pending folder, so <c>InitializeAsync</c> does not open it as a repository; the other tabs
    /// show their folders' names until they open.
    /// </summary>
    private void RestoreTabs(SessionState saved)
    {
        WatchTabs();
        var workspaces = _tabServices.Workspaces.Current;
        var dashboards = new List<(int Saved, Workspace Workspace)>();
        var others = new List<(int Saved, string? Path)>();
        for (var i = 0; i < saved.Tabs.Count; i++)
        {
            var tab = saved.Tabs[i];
            if (tab.WorkspaceId is { } id)
            {
                if (workspaces.Find(id) is { } workspace && !dashboards.Any(dashboard => dashboard.Workspace.Id == id))
                {
                    dashboards.Add((i, workspace));
                }
            }
            else
            {
                others.Add((i, string.IsNullOrWhiteSpace(tab.RepositoryPath) ? null : tab.RepositoryPath));
            }
        }

        RepoTabViewModel? active = null;
        foreach (var (index, workspace) in dashboards)
        {
            var tab = AddTab();
            tab.ShowDashboard(workspace);
            if (index == saved.ActiveTab)
            {
                active = tab;
            }
        }

        foreach (var (index, path) in others)
        {
            var tab = AddTab();
            if (path is not null)
            {
                tab.ShowPending(path);
            }

            if (index == saved.ActiveTab)
            {
                active = tab;
            }
        }

        if (Tabs.Count == 0)
        {
            AddTab();
        }

        _restoring = true;
        Activate(active ?? Tabs[0]);
        _restoring = false;
    }

    /// <summary>What the session keeps of each tab, in tab order (1.11): a dashboard by its workspace's id, any other tab by its folder.</summary>
    private List<TabState> SavedTabStates() => Tabs.Select(tab => new TabState(tab.SavedPath, tab.SavedWorkspaceId)).ToList();

    /// <summary>The open dashboard of the workspace <paramref name="workspaceId"/>, or null.</summary>
    private DashboardViewModel? FindOpenDashboard(string workspaceId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        return Tabs.Select(tab => tab.Dashboard).FirstOrDefault(dashboard => dashboard is not null && string.Equals(dashboard.WorkspaceId, workspaceId, StringComparison.Ordinal));
    }

    /// <summary><see cref="ActiveTabIndex"/> follows the tabs' order and count as well as the active tab.</summary>
    private void WatchTabs()
    {
        if (_watchingTabs)
        {
            return;
        }

        _watchingTabs = true;
        Tabs.CollectionChanged += OnTabsChanged;
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(ActiveTabIndex));

    partial void OnActiveTabChanged(RepoTabViewModel value) => OnPropertyChanged(nameof(ActiveTabIndex));
}
