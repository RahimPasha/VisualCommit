using System.ComponentModel;

namespace VisualCommit.App.ViewModels.Workspaces;

/// <summary>
/// What a dashboard tab's toolbar acts on (docs/phase-6-design.md 4.10, D208): its Fetch and Pull
/// run Fetch all and Pull all over the workspace's rows. <c>DashboardBulkViewModel</c> implements
/// it; <see cref="ToolbarViewModel.ForDashboard"/> makes the tab's toolbar over it, whose other
/// buttons are disabled as on an empty tab. Raises <see cref="INotifyPropertyChanged.PropertyChanged"/>
/// when a state or a reason changes.
/// </summary>
public interface IDashboardToolbar : INotifyPropertyChanged
{
    /// <summary>The workspace's name, as its tab shows it: the enabled tooltips name it ("Fetch every repository of {workspace}").</summary>
    string WorkspaceName { get; }

    /// <summary>Fetch all can run now.</summary>
    bool CanFetchAll { get; }

    /// <summary>Why Fetch all cannot run, the first of 4.10's reasons that holds; null while it can or with no reason known.</summary>
    string? FetchAllDisabledReason { get; }

    /// <summary>Pull all can run now.</summary>
    bool CanPullAll { get; }

    /// <summary>Why Pull all cannot run, the first of 4.10's reasons that holds; null while it can or with no reason known.</summary>
    string? PullAllDisabledReason { get; }

    /// <summary>Runs Fetch all (4.3); completes when the run has ended.</summary>
    Task FetchAllAsync();

    /// <summary>Runs Pull all (4.3); completes when the run has ended.</summary>
    Task PullAllAsync();
}
