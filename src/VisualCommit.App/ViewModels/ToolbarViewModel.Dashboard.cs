using System.ComponentModel;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;

namespace VisualCommit.App.ViewModels;

public sealed partial class ToolbarViewModel
{
    /// <summary>
    /// A dashboard tab's toolbar (docs/phase-6-design.md 4.10, D208): Fetch and Pull run Fetch all
    /// and Pull all over <paramref name="dashboard"/>'s rows (and so do their keys and the
    /// palette's toolbar entries), enabled with "Fetch every repository of {workspace}" and "Pull
    /// every repository of {workspace}" and their keys, disabled with the dashboard's first reason;
    /// Push, Branch, Stash, Pop, Undo, Redo and Search are disabled without a tooltip, as on an empty
    /// tab. A static factory over a private constructor, so that <c>new ToolbarViewModel(null)</c>
    /// stays unambiguous.
    /// </summary>
    public static ToolbarViewModel ForDashboard(IDashboardToolbar dashboard)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        return new ToolbarViewModel(dashboard);
    }

    /// <summary>A dashboard's toolbar: an empty tab's, whose Fetch and Pull follow <paramref name="dashboard"/>.</summary>
    private ToolbarViewModel(IDashboardToolbar dashboard)
        : this((RepositoryViewModel?)null)
    {
        Dashboard = dashboard;
        Actions = new DashboardActions(dashboard);
        dashboard.PropertyChanged += OnDashboardChanged;
        UpdateDashboardStates();
    }

    /// <summary>The dashboard whose Fetch all and Pull all Fetch and Pull run, or null for a repository's or an empty tab's toolbar.</summary>
    public IDashboardToolbar? Dashboard { get; }

    private void OnDashboardChanged(object? sender, PropertyChangedEventArgs e) => UpdateDashboardStates();

    /// <summary>Fetch and Pull as the dashboard says (4.10): enabled with what they do, or disabled with the first reason.</summary>
    private void UpdateDashboardStates()
    {
        if (Dashboard is not { } dashboard)
        {
            return;
        }

        var (canFetch, fetchReason) = (dashboard.CanFetchAll, dashboard.FetchAllDisabledReason);
        (FetchDisabledReason, FetchToolTip, CanFetch) = (canFetch ? null : fetchReason, canFetch ? OperationTexts.FetchEveryRepository(dashboard.WorkspaceName, _keys) : fetchReason, canFetch);
        var (canPull, pullReason) = (dashboard.CanPullAll, dashboard.PullAllDisabledReason);
        (PullDisabledReason, PullToolTip, CanPull) = (canPull ? null : pullReason, canPull ? OperationTexts.PullEveryRepository(dashboard.WorkspaceName, _keys) : pullReason, canPull);
    }

    /// <summary>A dashboard's toolbar's actions: Fetch and Pull run Fetch all and Pull all; nothing else is enabled.</summary>
    private sealed class DashboardActions(IDashboardToolbar dashboard) : IToolbarActions
    {
        public Task FetchAllAsync() => dashboard.FetchAllAsync();

        public Task PullAsync() => dashboard.PullAllAsync();

        public Task PushAsync() => Task.CompletedTask;

        public Task CreateBranchAtHeadAsync() => Task.CompletedTask;

        public Task StashAsync() => Task.CompletedTask;

        public Task PopAsync() => Task.CompletedTask;

        public Task UndoAsync() => Task.CompletedTask;

        public Task RedoAsync() => Task.CompletedTask;
    }
}
