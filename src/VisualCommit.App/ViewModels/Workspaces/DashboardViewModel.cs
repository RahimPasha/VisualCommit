using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.Core.Workspaces;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// A workspace's dashboard (D187, D205; docs/phase-6-design.md section 2): the rows of its
/// repositories, read as 2.10 says, its bulk runs and its header, inside a
/// <see cref="RepoTabViewModel"/> (<see cref="RepoTabViewModel.Dashboard"/>).
/// <para>
/// A stub with the members others call: work package WP-D builds it. Until then it has no rows,
/// reads nothing and raises nothing.
/// </para>
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    public DashboardViewModel(Workspace workspace, TabServices services)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(services);
        WorkspaceId = workspace.Id;
        Name = workspace.Name;
        Bulk = new DashboardBulkViewModel(this);
    }

    /// <summary>The workspace's id, by which the session keeps the dashboard (1.11).</summary>
    public string WorkspaceId { get; }

    /// <summary>The workspace's name, following a rename: the tab's title and the header's.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; }

    /// <summary>Its bulk runs: the tab's status bar and toolbar (4.9, 4.10).</summary>
    public DashboardBulkViewModel Bulk { get; }

    /// <summary>The selected row's view model, its own or a tab's; null with no row selected or none behind it (2.9: the activity log follows it).</summary>
    [ObservableProperty]
    public partial RepositoryViewModel? SelectedRepository { get; private set; }

#pragma warning disable CS0067 // Raised from work package WP-D on.
    /// <summary>A row asked to open its repository (a double-click, Enter, the row menu's Open, 2.3): the window opens or activates its tab.</summary>
    public event EventHandler<DashboardRowViewModel>? OpenRequested;

    /// <summary>The workspace was deleted (1.6, from <c>IWorkspaceStore.Changed</c>): the window closes the tab.</summary>
    public event EventHandler? WorkspaceDeleted;
#pragma warning restore CS0067

    /// <summary>The dashboard became the active tab (2.10's trigger 1): rows never read or read 10 s ago or more are read.</summary>
    public void OnBecameActive()
    {
    }

    /// <summary>The window was activated with the dashboard active (2.10's trigger 2, Q4).</summary>
    public void OnWindowActivated()
    {
    }

    /// <summary>The working directory of the row whose saved folder is <paramref name="savedPath"/>, when its repository is open; null otherwise (1.2's <c>WorkspaceMembership</c>).</summary>
    public string? OpenedWorkingDirectoryOf(string savedPath) => null;

    /// <summary>Closes the dashboard (its tab closed, or the window): cancels its reads and its run, releases its rows' view models (2.10).</summary>
    public void Dispose()
    {
    }

    /// <summary>F5 and the header's Refresh (2.10's trigger 3): every row without a tab is read.</summary>
    [RelayCommand]
    private Task Refresh() => Task.CompletedTask;
}
