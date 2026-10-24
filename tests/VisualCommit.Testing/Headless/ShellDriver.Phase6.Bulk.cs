using Avalonia.Controls;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Workspaces;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 6's bulk runs as the checks drive them (docs/phase-6-design.md 17.2; the report's part W,
/// W4 to W7): the header's Fetch all, Pull all and Cancel all clicked, a row's percentage waited
/// for, the run's end with every row's queue idle, and a row's Details… and Cancel clicked.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>Clicks the active dashboard's <c>DashboardFetchAll</c> and lets the UI thread run: the run starts (4.3).</summary>
    public Task FetchAllAsync() => ClickHeaderAsync("DashboardFetchAll");

    /// <summary>Clicks the active dashboard's <c>DashboardPullAll</c>.</summary>
    public Task PullAllAsync() => ClickHeaderAsync("DashboardPullAll");

    /// <summary>Clicks the active dashboard's <c>DashboardCancelAll</c> (4.5).</summary>
    public Task CancelAllAsync() => ClickHeaderAsync("DashboardCancelAll");

    /// <summary>
    /// Waits until the active dashboard's row <paramref name="name"/> shows its running fetch's
    /// percentage <paramref name="percent"/> (<c>Fetching from all remotes… 25%</c>, its
    /// <c>CompactText</c>, 2.6), and returns the row.
    /// </summary>
    public Task<DashboardRowSnapshot> WaitForRowPercentAsync(string name, int percent, TimeSpan? timeout = null) =>
        WaitForRowAsync(name, row => row.Operation.Text.EndsWith(FormattableString.Invariant($" {percent}%"), StringComparison.Ordinal), timeout);

    /// <summary>
    /// Waits for the active dashboard's run to end (4.3): its result in the header, and every
    /// row's queue idle (<see cref="WaitForIdleAsync"/>), so that the rows show what their
    /// refreshes read. Returns the run.
    /// </summary>
    /// <exception cref="InvalidOperationException">The active tab is no dashboard, or it has had no run.</exception>
    public async Task<BulkRun> WaitForBulkRunAsync(TimeSpan? timeout = null)
    {
        var dashboard = ActiveDashboard ?? throw new InvalidOperationException("The active tab is no dashboard.");
        var run = dashboard.Bulk.Current ?? throw new InvalidOperationException($"{dashboard.Name} has had no run.");
        await WaitForAsync(() => !run.IsRunning, $"{dashboard.Name}'s {(run.Kind == BulkKind.Fetch ? "Fetch all" : "Pull all")} to end", timeout);
        foreach (var repository in dashboard.Rows.Select(row => row.Repository).OfType<App.ViewModels.RepositoryViewModel>().Distinct())
        {
            await WaitForIdleAsync(repository, timeout);
        }

        Settle();
        return run;
    }

    /// <summary>Clicks the <c>DashboardRowDetails</c> of the row <paramref name="name"/> (4.6): its kept dialog opens.</summary>
    public Task ClickRowDetailsAsync(string name)
    {
        Click(RowButton(name, "DashboardRowDetails"));
        Settle();
        return Task.CompletedTask;
    }

    /// <summary>Clicks the <c>DashboardRowCancel</c> of the row <paramref name="name"/> (4.5): its queue's running operation is cancelled.</summary>
    public void ClickRowCancel(string name)
    {
        Click(RowButton(name, "DashboardRowCancel"));
        Settle();
    }

    private Task ClickHeaderAsync(string automationId)
    {
        Settle();
        var button = Window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(control => Avalonia.Automation.AutomationProperties.GetAutomationId(control) == automationId && control.IsEffectivelyVisible)
            ?? throw new InvalidOperationException($"No {automationId} is shown.");
        Click(button);
        Settle();
        return Task.CompletedTask;
    }

    private Button RowButton(string name, string automationId)
    {
        var row = DashboardRowControl(name);
        return row.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == automationId && button.IsEffectivelyVisible)
            ?? throw new InvalidOperationException($"The row {name} shows no {automationId}.");
    }
}
