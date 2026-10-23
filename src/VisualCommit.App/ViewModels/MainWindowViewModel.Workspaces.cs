using System.Collections.Specialized;
using System.ComponentModel;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels;

// The workspaces' dashboards in the window (D187, D188, D205; docs/phase-6-design.md 2.1 to 2.3,
// 2.10, 3.7, 3.8): ShowWorkspace, the rows' OpenRequested, a deleted workspace's tab, and the view
// models the window waits for when it closes.
public partial class MainWindowViewModel : IWorkspaceHost
{
    /// <summary>The dashboards whose events the window handles, by the tab that shows each.</summary>
    private readonly Dictionary<RepoTabViewModel, DashboardViewModel> _watchedDashboards = [];

    /// <summary>
    /// Opens the workspace <paramref name="workspaceId"/> (2.2), the one way in (the welcome page,
    /// <c>WorkspaceCommands</c>, the palette): its open dashboard is activated; else an empty
    /// <paramref name="from"/> tab becomes it (a welcome page that opened a workspace leaves no "New
    /// tab" behind) and moves after the open dashboards; else a dashboard tab is inserted there.
    /// Then it is activated (its first read starts) and the tabs are saved. No repository of the
    /// workspace opens in a tab (D188). A workspace the store does not hold opens nothing.
    /// </summary>
    public void ShowWorkspace(string workspaceId, RepoTabViewModel? from)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        if (Tabs.FirstOrDefault(tab => tab.Dashboard?.WorkspaceId == workspaceId) is { } open)
        {
            Activate(open);
            SaveTabs();
            return;
        }

        if (_tabServices.Workspaces.Current.Find(workspaceId) is not { } workspace)
        {
            _log.Warning($"The workspace {workspaceId} is not in workspaces.json: nothing opened.");
            return;
        }

        var dashboards = DashboardCount();
        RepoTabViewModel tab;
        if (from is not null && Tabs.Contains(from) && from.IsEmpty)
        {
            tab = from;
            tab.ShowDashboard(workspace);
            Tabs.Move(Tabs.IndexOf(tab), dashboards);
        }
        else
        {
            tab = AddTab();
            tab.ShowDashboard(workspace);
            Tabs.Move(Tabs.Count - 1, dashboards);
        }

        Activate(tab);
        UpdateCanClose();
        SaveTabs();
    }

    /// <summary>The working trees of the tabs with a repository open, in tab order (1.3, 1.7).</summary>
    public IReadOnlyList<string> OpenRepositoryFolders() =>
        [.. Tabs.Where(tab => !tab.IsDashboard && tab.Repository is not null).Select(tab => tab.Repository!.Path)];

    /// <summary>
    /// Why the workspace's saved folder <paramref name="savedPath"/> has no repository of its own to
    /// open, as its open dashboard's last read found it (its row's State tooltip); null without an
    /// open dashboard, a row of that folder or a reason. The palette disables its <c>Open
    /// repository</c> item with it (5.5, D204).
    /// </summary>
    public string? FolderReasonOf(string workspaceId, string savedPath) =>
        FindOpenDashboard(workspaceId)?.FolderReasonOf(savedPath);

    /// <summary>
    /// The window was activated (2.10's trigger 2, Q4): the active dashboard reads its rows last
    /// read 10 s ago or more. <c>MainWindow</c>'s <c>Activated</c> handler calls it.
    /// </summary>
    public void OnWindowActivated() => ActiveTab?.Dashboard?.OnWindowActivated();

    /// <summary>3.8's term of <see cref="NamesRepositoryInDialogs"/>: a dashboard tab is open, so every dialog names its repository.</summary>
    private bool AnyDashboardOpen => Tabs.Any(tab => tab.IsDashboard);

    /// <summary>
    /// The window waits for these view models when it closes (3.7): the tabs' repositories, closed
    /// tabs' that still run, the dashboards' summary-mode ones and those released while running,
    /// each once.
    /// </summary>
    private IEnumerable<RepositoryViewModel> RepositoriesToFinish() =>
        Tabs.Select(tab => tab.Repository)
            .OfType<RepositoryViewModel>()
            .Concat(_closedButRunning)
            .Concat(_tabServices.OpenRepositories.Summaries)
            .Concat(_tabServices.OpenRepositories.Closing)
            .Distinct();

    /// <summary>Every dashboard's running bulk run starts no more rows (4.12), before the queues drop what waits.</summary>
    private void StopBulkRunsStarting()
    {
        foreach (var dashboard in Tabs.Select(tab => tab.Dashboard).OfType<DashboardViewModel>())
        {
            dashboard.Bulk.StopStarting();
        }
    }

    /// <summary>The first read of the dashboard that is active at start (2.10): <c>InitializeAsync</c> awaits it beside the restored tabs' openings.</summary>
    private Task ReadActiveDashboardAtStartAsync() => ActiveTab?.Dashboard?.ReadAtStartAsync() ?? Task.CompletedTask;

    /// <summary>
    /// Watches the tabs for dashboards (made by <see cref="ShowWorkspace"/> or by the session's
    /// restore): the window opens their rows' repositories and closes the tab of a deleted workspace.
    /// Called once, from the constructor, before the tabs are restored.
    /// </summary>
    private void WatchDashboards()
    {
        Tabs.CollectionChanged += OnTabsChanged;
    }

    private int DashboardCount() => Tabs.TakeWhile(tab => tab.IsDashboard).Count();

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            return;
        }

        foreach (var tab in e.OldItems?.OfType<RepoTabViewModel>() ?? [])
        {
            if (!Tabs.Contains(tab))
            {
                tab.PropertyChanged -= OnTabPropertyChanged;
                Unwatch(tab);
            }
        }

        foreach (var tab in e.NewItems?.OfType<RepoTabViewModel>() ?? [])
        {
            tab.PropertyChanged -= OnTabPropertyChanged;
            tab.PropertyChanged += OnTabPropertyChanged;
            Watch(tab);
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var tab in _watchedDashboards.Keys.Where(tab => !Tabs.Contains(tab)).ToList())
            {
                Unwatch(tab);
            }
        }
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepoTabViewModel.Dashboard) && sender is RepoTabViewModel tab)
        {
            Watch(tab);
        }
    }

    private void Watch(RepoTabViewModel tab)
    {
        if (tab.Dashboard is not { } dashboard || _watchedDashboards.ContainsKey(tab))
        {
            return;
        }

        _watchedDashboards[tab] = dashboard;
        dashboard.OpenRequested += OnOpenRequested;
        dashboard.WorkspaceDeleted += OnWorkspaceDeleted;
    }

    private void Unwatch(RepoTabViewModel tab)
    {
        if (_watchedDashboards.Remove(tab, out var dashboard))
        {
            dashboard.OpenRequested -= OnOpenRequested;
            dashboard.WorkspaceDeleted -= OnWorkspaceDeleted;
        }
    }

    /// <summary>
    /// A row asked to open its repository (2.3): the leftmost tab that shows its worktree is
    /// activated (its view model is the row's or is registered for the row's worktree, or its
    /// pending folder is the row's); otherwise a new tab is inserted after the dashboards and the
    /// tabs this dashboard opened that are still open, activated, and opens the folder, adopting the
    /// row's summary-mode view model (3.3).
    /// </summary>
    private void OnOpenRequested(object? sender, DashboardRowViewModel row)
    {
        if (sender is not DashboardViewModel dashboard || dashboard.IsClosed)
        {
            return;
        }

        var key = row.Key;
        var shown = Tabs.FirstOrDefault(tab =>
            !tab.IsDashboard
            && ((tab.Repository is { } repository
                    && (ReferenceEquals(repository, row.Repository)
                        || (key is not null && WorktreeKey.Comparer.Equals(OpenRepositories.KeyOf(repository), key))))
                || (tab.Repository is null && tab.PendingPath is { } pending && WorkspacePaths.Same(pending, row.Path))));
        if (shown is not null)
        {
            Activate(shown);
            return;
        }

        dashboard.OpenedTabs.RemoveAll(opened => !Tabs.Contains(opened));
        var highest = dashboard.OpenedTabs.Count == 0 ? -1 : dashboard.OpenedTabs.Max(Tabs.IndexOf);
        var index = 1 + Math.Max(DashboardCount() - 1, highest);
        var tab = AddTab();
        Tabs.Move(Tabs.Count - 1, Math.Min(index, Tabs.Count - 1));
        dashboard.OpenedTabs.Add(tab);
        Activate(tab);
        UpdateCanClose();
        _ = tab.OpenAsync(row.Path, remember: true);
    }

    /// <summary>"Delete workspace…" (1.6): the workspace's dashboard closes as by its close button (2.1); the tabs opened from it stay.</summary>
    private void OnWorkspaceDeleted(object? sender, EventArgs e)
    {
        if (_watchedDashboards.FirstOrDefault(entry => ReferenceEquals(entry.Value, sender)).Key is { } tab)
        {
            CloseTab(tab);
        }
    }
}
