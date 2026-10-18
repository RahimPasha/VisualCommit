using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Operations;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// A dashboard's bulk runs as its tab's status bar and toolbar show them (docs/phase-6-design.md
/// 4.3, 4.9, 4.10, D208, D210): one per dashboard (<see cref="DashboardViewModel.Bulk"/>), the
/// status bar's source (<see cref="IStatusBarOperation"/>) and the toolbar's (<see cref="IDashboardToolbar"/>).
/// <para>
/// A stub: work package WP-B builds it. Until then it says "Ready", Fetch all and Pull all are
/// disabled without a reason and run nothing, and <see cref="StopStarting"/> does nothing.
/// </para>
/// </summary>
public sealed partial class DashboardBulkViewModel : ObservableObject, IStatusBarOperation, IDashboardToolbar
{
    private readonly DashboardViewModel _dashboard;

    public DashboardBulkViewModel(DashboardViewModel dashboard)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        _dashboard = dashboard;
        CancelCommand = new RelayCommand(static () => { }, static () => false);
    }

    public string Text => OperationStatusViewModel.ReadyText;

    public OperationStatusTone Tone => OperationStatusTone.Secondary;

    public string? ToolTip => null;

    public bool IsRunning => false;

    public bool IsIndeterminate => false;

    public int? Percent => null;

    public bool CanCancel => false;

    /// <summary>Cancel all (4.5) while a run runs; disabled in the stub.</summary>
    public ICommand CancelCommand { get; }

    public string CancelToolTip => string.Empty;

    public string WaitingText => "0 waiting";

    public bool HasWaiting => false;

    public int Waiting => 0;

    public string WorkspaceName => _dashboard.Name;

    public bool CanFetchAll => false;

    public string? FetchAllDisabledReason => null;

    public bool CanPullAll => false;

    public string? PullAllDisabledReason => null;

    public Task FetchAllAsync() => Task.CompletedTask;

    public Task PullAllAsync() => Task.CompletedTask;

    /// <summary>The running run starts no more rows (4.5): its rows in line end "cancelled". Does nothing in the stub.</summary>
    public void StopStarting()
    {
    }
}
