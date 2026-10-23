using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Closing the window with dashboards (docs/phase-6-design.md 3.7, 4.12, D209) and the rule of
/// naming the repository in dialogs (3.8): the window waits for a dashboard's running pull as for a
/// tab's, closed dashboards' included; closing a dashboard cancels a row's fetch; CloseAll leaves
/// no summary and makes no git call afterwards.
/// </summary>
public class MainWindowCloseTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public async Task The_window_waits_for_a_summarys_running_pull_and_drops_what_waits()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var (window, dashboard) = await OpenAsync(provider, api.WorkingDirectory);
        var summary = dashboard.Rows[0].Repository!;
        var (running, finish) = (new TaskCompletionSource(), new TaskCompletionSource());
        var pull = summary.Queue.RunAsync(Local("Pull", running, finish));
        await running.Task.WaitAsync(Patience);
        var waiting = summary.Queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.None, _ => Task.FromResult(OperationReport.Done("Fetched from all remotes"))));
        var closed = 0;

        Assert.False(window.TryClose(() => closed++));

        var finishing = Assert.IsType<FinishingDialogViewModel>(Assert.Single(window.Dialogs.Open));
        Assert.Equal(["Pull — api"], finishing.Items.Select(item => item.Text));
        Assert.Equal(OperationOutcome.Cancelled, (await waiting.WaitAsync(Patience)).Outcome);

        finish.SetResult();
        await pull.WaitAsync(Patience);
        await DashboardTestSupport.WaitUntilAsync(() => closed == 1, "the window to close");
    }

    [AvaloniaFact]
    public async Task The_window_waits_for_a_closed_dashboards_pull()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var (window, dashboard) = await OpenAsync(provider, api.WorkingDirectory);
        window.NewTabCommand.Execute(null);
        var summary = dashboard.Rows[0].Repository!;
        var (running, finish) = (new TaskCompletionSource(), new TaskCompletionSource());
        var pull = summary.Queue.RunAsync(Local("Pull", running, finish));
        await running.Task.WaitAsync(Patience);

        // The dashboard closes during its pull past the fetch: the summary is released while running.
        window.CloseTabCommand.Execute(window.Tabs[0]);
        Assert.True(dashboard.IsClosed);
        Assert.True(summary.IsClosed);
        var closed = 0;

        Assert.False(window.TryClose(() => closed++));
        var finishing = Assert.IsType<FinishingDialogViewModel>(Assert.Single(window.Dialogs.Open));
        Assert.Equal(["Pull — api"], finishing.Items.Select(item => item.Text));

        finish.SetResult();
        await pull.WaitAsync(Patience);
        await DashboardTestSupport.WaitUntilAsync(() => closed == 1, "the window to close");
    }

    [AvaloniaFact]
    public async Task Closing_the_window_during_Fetch_all_ends_its_rows_in_line_and_waits_for_the_four_running()
    {
        // 3.7, 4.12: the run starts no more rows before the queues drop what waits; "Finishing
        // operations" lists the four running fetches only; on "Keep open" the run ends with them,
        // counting the rest cancelled, no fifth fetch starts, and a later Fetch all works.
        using var provider = new BulkProvider();
        var gate = new TaskCompletionSource();
        string[] names = ["a", "b", "c", "d", "e", "f"];
        foreach (var name in names)
        {
            provider.Add(name).FetchGate = gate;
        }

        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store([.. names.Select(provider.PathOf)]));
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var dashboard = window.ActiveTab.Dashboard!;
        await DashboardTestSupport.WaitUntilAsync(() => !dashboard.IsPassRunning && dashboard.Rows.All(row => row.HasRepository), "the dashboard's first read");
        var run = dashboard.Bulk.FetchAllAsync();
        var current = dashboard.Bulk.Current!;
        await DashboardTestSupport.WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");
        Assert.Equal((4, 2), (current.RunningRows, current.InLine));
        var closed = 0;

        Assert.False(window.TryClose(() => closed++));

        Assert.Equal([BulkRowState.CancelledInLine, BulkRowState.CancelledInLine], current.Rows.Skip(4).Select(row => row.State));
        Assert.Equal(["Fetch cancelled", "Fetch cancelled"], dashboard.Rows.Skip(4).Select(row => row.Operation.Text));
        var finishing = Assert.IsType<FinishingDialogViewModel>(Assert.Single(window.Dialogs.Open));
        Assert.Equal(["Fetch — a", "Fetch — b", "Fetch — c", "Fetch — d"], finishing.Items.Select(item => item.Text).Order(StringComparer.Ordinal));

        // Keep open: the window stays; the four end.
        finishing.CancelCommand.Execute(null);
        await DashboardTestSupport.WaitUntilAsync(() => finishing.IsClosed, "the dialog to close");
        gate.SetResult();
        await run.WaitAsync(Patience);
        Assert.Equal(0, closed);
        Assert.Equal(("Fetched 4 repositories; 2 cancelled", 4, 2), (dashboard.Bulk.RunText, current.Succeeded, current.Cancelled));
        Assert.Equal((0, 0, 4), (provider["e"].Fetches, provider["f"].Fetches, provider.MaxFetchesAtOnce));
        Assert.Equal(0, services.BulkSlots.Held);

        await dashboard.Bulk.FetchAllAsync().WaitAsync(Patience);
        Assert.Equal("Fetched 6 repositories", dashboard.Bulk.RunText);
        Assert.Equal((1, 1), (provider["e"].Fetches, provider["f"].Fetches));
        window.CloseAll();
    }

    [AvaloniaFact]
    public async Task A_dashboards_idle_rows_do_not_hold_the_window()
    {
        using var provider = new FakeProvider();
        var (window, _) = await OpenAsync(provider, provider.Add("api").WorkingDirectory, provider.PathOf("old-site"));

        Assert.True(window.TryClose(() => { }));
    }

    [AvaloniaFact]
    public async Task Closing_a_dashboard_tab_cancels_a_rows_fetch()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var (window, dashboard) = await OpenAsync(provider, api.WorkingDirectory);
        var summary = dashboard.Rows[0].Repository!;
        var started = new TaskCompletionSource();
        var fetch = summary.Queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.Refs, async context =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Fetched from all remotes");
        }));
        await started.Task.WaitAsync(Patience);

        window.CloseTabCommand.Execute(window.ActiveTab);

        var result = await fetch.WaitAsync(Patience);
        Assert.Equal((OperationOutcome.Cancelled, "Fetch cancelled"), (result.Outcome, result.Message));
        Assert.True(summary.IsClosed);
    }

    [AvaloniaFact]
    public async Task Close_all_disposes_the_summaries()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(provider.Add("api").WorkingDirectory, provider.Add("docs").WorkingDirectory));
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var dashboard = window.ActiveTab.Dashboard!;
        await DashboardTestSupport.WaitUntilAsync(() => dashboard.Rows.All(row => row.HasRepository), "the rows' reads");
        var summaries = services.OpenRepositories.Summaries;
        Assert.Equal(2, summaries.Count);

        window.CloseAll();

        Assert.All(summaries, summary => Assert.True(summary.IsClosed));
        Assert.Empty(services.OpenRepositories.Summaries);
        Assert.True(dashboard.IsClosed);
    }

    [AvaloniaFact]
    public async Task Close_all_during_a_held_read_leaves_no_view_model_and_no_git_call()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");

        // A gate that ignores the reads' cancellation, so that both probes return after CloseAll
        // and reach what refuses them: the closed row's token, the registry's DisposeAll.
        provider.Gate = new TaskCompletionSource();
        provider.GateIgnoresCancellation = true;
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(api.WorkingDirectory));
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var row = window.ActiveTab.Dashboard!.Rows[0];
        await DashboardTestSupport.WaitUntilAsync(() => provider.Opens == 1, "the row's probe");

        // The positive control: a dashboard of the workspace outside the window, on the same
        // services, its probe held at the same gate, which CloseAll leaves alone.
        var control = new DashboardViewModel(services.Workspaces.Current.Find("web")!, services, () => true);
        control.OnBecameActive();
        await DashboardTestSupport.WaitUntilAsync(() => provider.Opens == 2, "the control's probe");
        window.CloseAll();
        provider.Gate.SetResult();

        // The closed row's read ends, and the control's read, refused a summary after DisposeAll,
        // ends with nothing either.
        await DashboardTestSupport.WaitUntilAsync(() => !row.IsReading && !control.Rows[0].IsReading && !control.IsPassRunning, "both reads to end");
        Assert.NotNull(control.Rows[0].Key);
        Assert.Null(row.Key);
        Assert.Empty(services.OpenRepositories.Summaries);
        Assert.Equal((null, null), (row.Repository, control.Rows[0].Repository));
        Assert.Equal((0, 0), (api.RefsReads, api.StatusReads));
        control.Dispose();
    }

    [AvaloniaFact]
    public async Task Dialogs_name_their_repository_while_a_dashboard_is_open()
    {
        using var provider = new FakeProvider();
        var api = provider.Add("api");
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store());
        var window = DashboardTestSupport.Window(services);

        // Without a dashboard: as before, only with more than one tab with a repository.
        Assert.True(await window.ActiveTab.OpenAsync(api.WorkingDirectory));
        Assert.False(window.NamesRepositoryInDialogs);
        window.NewTabCommand.Execute(null);
        Assert.False(window.NamesRepositoryInDialogs);

        // A dashboard beside one repository tab.
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        Assert.True(window.NamesRepositoryInDialogs);
        Assert.True(services.NamesRepositoryInDialogs());

        // A dashboard alone.
        window.CloseTabCommand.Execute(window.Tabs[1]);
        Assert.Single(window.Tabs);
        Assert.True(window.NamesRepositoryInDialogs);
    }

    private static GitOperation Local(string title, TaskCompletionSource running, TaskCompletionSource finish) =>
        new(title, "Pulling origin/main into main…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            running.SetResult();
            await finish.Task;
            return OperationReport.Done("Pulled 1 commit into main (fast-forward)");
        });

    private static async Task<(MainWindowViewModel Window, DashboardViewModel Dashboard)> OpenAsync(FakeProvider provider, params string[] folders)
    {
        var services = DashboardTestSupport.Services(provider, DashboardTestSupport.Store(folders));
        var window = DashboardTestSupport.Window(services);
        services.WorkspaceHost!.ShowWorkspace("web", window.ActiveTab);
        var dashboard = window.ActiveTab.Dashboard!;
        await DashboardTestSupport.WaitUntilAsync(() => !dashboard.IsPassRunning && dashboard.Rows.All(row => !row.IsReading && !row.IsNotReadYet), "the dashboard's first read");
        return (window, dashboard);
    }
}
