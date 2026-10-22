using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// One tab (D45): a repository, or the welcome page when it has none. The window's regions show
/// the active tab's left panel, graph area, details and branch.
/// </summary>
public sealed partial class RepoTabViewModel : ObservableObject, IDisposable
{
    public const string EmptyTitle = "New tab";

    private readonly TabServices _services;
    private readonly LeftPanelViewModel _emptyLeftPanel = new();
    private readonly CommitDetailsViewModel _emptyDetails;
    private readonly ToolbarViewModel _emptyToolbar = new(null);
    private readonly OperationStatusViewModel _emptyStatus;
    private bool _disposed;

    public RepoTabViewModel(TabServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _emptyDetails = new CommitDetailsViewModel(null, services.Settings, services.Dates, services.Log);

        // ssh's notices show here too: a clone on the welcome page can get one (3.2, 3.3).
        _emptyStatus = new OperationStatusViewModel(null, services.DialogHost.Notices);
        Welcome = new WelcomeViewModel(this, services);
    }

    /// <summary>The repository's name, or "New tab".</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = EmptyTitle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CloseToolTip), nameof(CloseKeyText))]
    public partial bool IsActive { get; set; }

    /// <summary>
    /// The close button's tooltip and help text (docs/phase-6-design.md 6.8): with Mod+W's key on
    /// the active tab, without it on the others, since Mod+W closes the active tab only (6.5).
    /// </summary>
    public string CloseToolTip => IsActive ? ShellToolTips.CloseTab : ShellToolTips.CloseOtherTab;

    /// <summary>The close button's <c>AcceleratorKey</c>: Mod+W's text on the active tab, null on the others (6.8).</summary>
    public string? CloseKeyText => IsActive ? KeyTexts.Current.Of(AppCommand.CloseTab) : null;

    /// <summary>The tab shows a close button: every tab but a lone empty one.</summary>
    [ObservableProperty]
    public partial bool CanClose { get; set; }

    /// <summary>The repository open in this tab, or null for the welcome page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRepository), nameof(ShowsWelcome), nameof(LeftPanel), nameof(Details), nameof(BranchText), nameof(Changes), nameof(ShowsWorkingChanges))]
    [NotifyPropertyChangedFor(nameof(Toolbar), nameof(OperationStatus))]
    [NotifyPropertyChangedFor(nameof(ShowsRightPanel), nameof(ShowsDetailsPanel), nameof(ShowsStagePanel), nameof(GraphColumnSpan))]
    public partial RepositoryViewModel? Repository { get; private set; }

    /// <summary>
    /// The toolbar's buttons (docs/phase-3-design.md 4d): a dashboard's (Fetch and Pull run Fetch all
    /// and Pull all, docs/phase-6-design.md 2.8, 4.10), the repository's, or a toolbar whose commands
    /// are all disabled.
    /// </summary>
    public ToolbarViewModel Toolbar => Dashboard?.Toolbar ?? Repository?.Toolbar ?? _emptyToolbar;

    /// <summary>
    /// What the status bar shows of the operations (4e): a dashboard's bulk runs
    /// (docs/phase-6-design.md 4.9), the repository's, or "Ready".
    /// </summary>
    public IStatusBarOperation OperationStatus => (IStatusBarOperation?)Dashboard?.Bulk ?? Repository?.OperationStatus ?? _emptyStatus;

    /// <summary>
    /// The workspace's dashboard this tab shows (D205, docs/phase-6-design.md 2.1), set once by
    /// <see cref="ShowDashboard"/>; null for a repository's or an empty tab. It spans the main area's
    /// three columns, where neither side panel shows (2.4).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboard), nameof(ShowsDashboard), nameof(ShowsLeftPanel), nameof(GraphColumn), nameof(TabAutomationId), nameof(OperationStatus))]
    [NotifyPropertyChangedFor(nameof(Toolbar), nameof(BranchText), nameof(ShowsWelcome), nameof(ShowsRightPanel), nameof(ShowsDetailsPanel), nameof(ShowsStagePanel), nameof(GraphColumnSpan))]
    public partial DashboardViewModel? Dashboard { get; private set; }

    /// <summary>
    /// While the tab waits before it opens a worktree whose closed tab's or released summary's
    /// queue still runs there (docs/phase-6-design.md 3.3): <c>Waiting for {title} to finish…</c>,
    /// shown centred in the graph area (<c>TabWaitingText</c>); null otherwise.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWaitingForClosedQueue))]
    public partial string? WaitingText { get; private set; }

    /// <summary><see cref="WaitingText"/> shows.</summary>
    public bool IsWaitingForClosedQueue => WaitingText is not null;

    /// <summary>
    /// The tab can become a dashboard in place (docs/phase-6-design.md 2.2): no repository, not
    /// opening, no pending folder, not a dashboard, no clone running on its welcome page; a tab whose
    /// restore failed counts (its folder is dropped then).
    /// </summary>
    public bool IsEmpty => Repository is null && !IsOpening && PendingPath is null && !IsDashboard && Welcome.RunningClone is null;

    /// <summary>The tab is a workspace's dashboard.</summary>
    public bool IsDashboard => Dashboard is not null;

    /// <summary>The graph area shows the dashboard (2.4).</summary>
    public bool ShowsDashboard => IsDashboard;

    /// <summary>The left panel and its splitter are shown: not on a dashboard (2.4).</summary>
    public bool ShowsLeftPanel => !IsDashboard;

    /// <summary>The main area's column the graph area starts in: 0 on a dashboard, which spans the three columns (2.4), else 1.</summary>
    public int GraphColumn => IsDashboard ? 0 : 1;

    /// <summary>The workspace id the session keeps for a dashboard (1.11), or null for every other tab.</summary>
    public string? SavedWorkspaceId { get; private set; }

    /// <summary>The tab button's automation id (13.4): <c>DashboardTab</c> on a dashboard, <c>RepoTab</c> otherwise.</summary>
    public string TabAutomationId => IsDashboard ? "DashboardTab" : "RepoTab";

    public bool HasRepository => Repository is not null;

    /// <summary>A repository is being opened: the graph area shows neither the welcome page nor a graph yet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWelcome))]
    public partial bool IsOpening { get; private set; }

    /// <summary>The graph area shows the welcome page: not on a dashboard (2.1).</summary>
    public bool ShowsWelcome => Repository is null && !IsOpening && !IsDashboard;

    public WelcomeViewModel Welcome { get; }

    public LeftPanelViewModel LeftPanel => Repository?.LeftPanel ?? _emptyLeftPanel;

    public CommitDetailsViewModel Details => Repository?.Details ?? _emptyDetails;

    /// <summary>The repository's stage panel, or null without a repository.</summary>
    public WorkingChangesViewModel? Changes => Repository?.Changes;

    /// <summary>The right panel shows the stage panel: the working-changes row is selected.</summary>
    public bool ShowsWorkingChanges => Repository?.IsWorkingRowSelected == true;

    /// <summary>
    /// The right panel and its splitter are shown: not while the conflict resolver spans the graph
    /// and right-panel columns (docs/phase-4-design.md 11.2), nor on a dashboard (docs/phase-6-design.md 2.4).
    /// </summary>
    public bool ShowsRightPanel => !IsDashboard && Repository?.IsResolverOpen != true;

    /// <summary>The right panel shows the commit's details.</summary>
    public bool ShowsDetailsPanel => ShowsRightPanel && !ShowsWorkingChanges;

    /// <summary>The right panel shows the stage panel.</summary>
    public bool ShowsStagePanel => ShowsRightPanel && ShowsWorkingChanges;

    /// <summary>
    /// The columns the graph area spans: the right panel's too while the resolver is open (11.2);
    /// on a dashboard all three (docs/phase-6-design.md 2.4).
    /// </summary>
    public int GraphColumnSpan => IsDashboard ? 3 : ShowsRightPanel ? 1 : 2;

    /// <summary>What the status bar shows for this tab: on a dashboard the workspace's name, following a rename (2.1, 2.8).</summary>
    public string BranchText => Dashboard?.Name ?? Repository?.BranchText ?? "No repository";

    /// <summary>The working tree of the open repository, or null.</summary>
    public string? RepositoryPath => Repository?.Path;

    /// <summary>The folder of a tab restored from the session that has not been opened yet, or null.</summary>
    public string? PendingPath { get; private set; }

    /// <summary>
    /// The folder of a tab restored from the session that could not be opened (a drive not
    /// mounted yet, git missing), or null. The session keeps it, so the tab comes back on the next
    /// start, until the user opens something else in the tab or closes it.
    /// </summary>
    public string? FailedPath { get; private set; }

    /// <summary>The folder the session keeps for this tab, or null for an empty tab.</summary>
    public string? SavedPath => RepositoryPath ?? PendingPath ?? FailedPath;

    /// <summary>Raised after a repository was opened, initialised or cloned in this tab.</summary>
    public event EventHandler? RepositoryOpened;

    /// <summary>
    /// Makes this tab <paramref name="workspace"/>'s dashboard (2.1): the restore of a dashboard
    /// tab (1.11) and <c>IWorkspaceHost.ShowWorkspace</c> (2.2) call it. Sets <see cref="Dashboard"/>,
    /// <see cref="IsDashboard"/>, <see cref="Title"/> (which follows a rename) and
    /// <see cref="SavedWorkspaceId"/>, and gives the tab the dashboard's toolbar (2.8, 4.10). A
    /// folder a failed restore kept is dropped. A tab is made a dashboard once.
    /// </summary>
    public void ShowDashboard(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (Dashboard is not null)
        {
            return;
        }

        FailedPath = null;
        SavedWorkspaceId = workspace.Id;
        Title = workspace.Name;

        // "Active" is the window's active tab (2.10): a closed tab is never it.
        var dashboard = new DashboardViewModel(workspace, _services, () => IsActive && !_disposed);
        dashboard.PropertyChanged += OnDashboardPropertyChanged;
        Dashboard = dashboard;
    }

    /// <summary>
    /// Shows a tab restored from the session under its folder's name, before git has been found
    /// and the repository can be opened. <see cref="OpenAsync"/> with <see cref="PendingPath"/> opens it.
    /// </summary>
    public void ShowPending(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        PendingPath = path;
        IsOpening = true;
        Title = FolderName(path);
    }

    /// <summary>
    /// Opens the repository at <paramref name="path"/> in this tab. Returns false and shows the
    /// reason on the welcome page when it cannot be opened. A tab restored from the session
    /// passes <paramref name="remember"/> false, so restoring does not reorder the recent list.
    /// </summary>
    public async Task<bool> OpenAsync(string path, bool remember = true)
    {
        var sinceOpening = Stopwatch.StartNew();
        var previousTitle = Title;
        IsOpening = true;
        Title = FolderName(path);
        var restoring = PendingPath is not null;
        try
        {
            var repository = await _services.Repositories.OpenAsync(path);
            if (_disposed)
            {
                // The tab was closed while the repository was being opened.
                return false;
            }

            PendingPath = null;

            // IsOpening stays true until ShowAsync shows the repository: it may first wait for a
            // closed queue on the worktree, and opens nothing when the tab closes meanwhile
            // (docs/phase-6-design.md 3.3).
            await ShowAsync(repository, sinceOpening, remember);
            return Repository is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_disposed)
            {
                return false;
            }

            if (restoring)
            {
                FailedPath = path;
            }

            PendingPath = null;
            IsOpening = false;
            Title = Repository is null ? EmptyTitle : previousTitle;
            Welcome.ShowError(ex is GitException git ? git.StandardError.Trim() : ex.Message);
            if (ex is not NotARepositoryException)
            {
                _services.Log.Error($"Opening {path} failed.", ex);
            }

            return false;
        }
    }

    /// <summary>
    /// Shows a repository that was just opened, initialised or cloned, and loads it. While the
    /// queue of a closed tab or of a released summary still runs on its worktree, it first waits for
    /// it, opening (<see cref="IsOpening"/>) and saying so (<see cref="WaitingText"/>), and opens
    /// nothing if the tab closes meanwhile (docs/phase-6-design.md 3.3). Then it adopts the
    /// worktree's summary-mode view model a dashboard holds, when there is one, so that a row and the
    /// tab share one queue (D209), and registers the view model (3.4).
    /// </summary>
    public async Task ShowAsync(IGitRepository repository, Stopwatch? sinceOpening = null, bool remember = true)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (_disposed)
        {
            // Closed while the repository was being opened, initialised or cloned.
            return;
        }

        var registry = _services.OpenRepositories;
        var key = WorktreeKey.Of(repository.GitDirectory);
        var idle = registry.WhenIdleAsync(key);
        if (!idle.IsCompleted)
        {
            IsOpening = true;
            while (!idle.IsCompleted && !_disposed)
            {
                WaitingText = registry.ClosedRunningOn(key)?.Queue.Current is { } running
                    ? DashboardTexts.WaitingToFinish(running.Title)
                    : WaitingText;
                await idle;
                idle = registry.WhenIdleAsync(key);
            }

            WaitingText = null;
            if (_disposed)
            {
                return;
            }
        }

        IsOpening = false;
        FailedPath = null;
        var previous = Repository;
        var opened = registry.TryAdopt(key) is { } adopted
            ? Adopt(adopted, sinceOpening)
            : new RepositoryViewModel(repository, _services.Settings, _services.Dates, _services.Log, sinceOpening, _services.Dialogs, _services);
        opened.PropertyChanged += OnRepositoryPropertyChanged;
        registry.RegisterTab(opened);
        Repository = opened;
        Title = opened.Name;
        if (previous is not null)
        {
            previous.PropertyChanged -= OnRepositoryPropertyChanged;
            registry.UnregisterTab(previous);
            previous.Dispose();
        }

        if (remember)
        {
            _services.Recent.Add(repository.WorkingDirectory, repository.Name);
        }

        RepositoryOpened?.Invoke(this, EventArgs.Empty);
        await opened.StartAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Welcome.Dispose();
        _emptyStatus.Dispose();
        if (Dashboard is { } dashboard)
        {
            dashboard.PropertyChanged -= OnDashboardPropertyChanged;
            dashboard.Dispose();
        }

        if (Repository is { } repository)
        {
            repository.PropertyChanged -= OnRepositoryPropertyChanged;

            // The worktree's registry keeps its queue's idle task (3.4), so no tab and no summary
            // is made there while a local operation of this one still runs.
            _services.OpenRepositories.UnregisterTab(repository);
            repository.Dispose();
        }
    }

    /// <summary>
    /// The tab takes a dashboard's summary-mode view model (docs/phase-6-design.md 3.3): the freshly
    /// opened repository is dropped (the adopted one points at the same folders) and the view model
    /// is promoted with the tab's stopwatch, so that its full start counts from the user's open.
    /// </summary>
    private static RepositoryViewModel Adopt(RepositoryViewModel adopted, Stopwatch? sinceOpening)
    {
        adopted.Promote(sinceOpening ?? Stopwatch.StartNew());
        return adopted;
    }

    private void OnDashboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.Name) && Dashboard is { } dashboard)
        {
            // A rename follows (2.1).
            Title = dashboard.Name;
            OnPropertyChanged(nameof(BranchText));
        }
    }

    private static string FolderName(string path) =>
        System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path));

    private void OnRepositoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepositoryViewModel.BranchText))
        {
            OnPropertyChanged(nameof(BranchText));
        }
        else if (e.PropertyName == nameof(RepositoryViewModel.IsWorkingRowSelected))
        {
            OnPropertyChanged(nameof(ShowsWorkingChanges));
            OnPropertyChanged(nameof(ShowsDetailsPanel));
            OnPropertyChanged(nameof(ShowsStagePanel));
        }
        else if (e.PropertyName == nameof(RepositoryViewModel.IsResolverOpen))
        {
            OnPropertyChanged(nameof(ShowsRightPanel));
            OnPropertyChanged(nameof(ShowsDetailsPanel));
            OnPropertyChanged(nameof(ShowsStagePanel));
            OnPropertyChanged(nameof(GraphColumnSpan));
        }
    }
}
