using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Workspaces;
using Xunit;
using static VisualCommit.App.Tests.DashboardTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// A dashboard tab's toolbar (docs/phase-6-design.md 4.10, 4.13; D208), through
/// <see cref="ToolbarViewModel.ForDashboard"/> only: Fetch and Pull run Fetch all and Pull all,
/// enabled with what they do and their keys, also while rows are still being read, and disabled
/// with 4.10's first reason that holds; Push, Branch, Stash, Pop, Undo, Redo and Search disabled
/// without a tooltip; the window's Theme switch enabled.
/// </summary>
public class DashboardToolbarTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public async Task On_a_dashboard_tab_only_Fetch_and_Pull_are_enabled_with_what_they_do()
    {
        using var provider = new BulkProvider();
        provider.Add("api");
        using var host = await BulkHost.StartAsync(provider, ["api"]);
        var toolbar = host.Dashboard.Toolbar;

        Assert.Same(host.Bulk, toolbar.Dashboard);
        Assert.Equal((true, FetchEvery("Web"), (string?)null), (toolbar.CanFetch, toolbar.FetchToolTip, toolbar.FetchDisabledReason));
        Assert.Equal((true, PullEvery("Web"), (string?)null), (toolbar.CanPull, toolbar.PullToolTip, toolbar.PullDisabledReason));
        Assert.True(toolbar.FetchCommand.CanExecute(null));
        Assert.True(toolbar.PullCommand.CanExecute(null));
        AssertTheRestDisabledWithoutATooltip(toolbar);
    }

    [AvaloniaFact]
    public async Task On_the_windows_dashboard_tab_the_toolbar_is_the_dashboards_and_the_theme_switch_stays_enabled()
    {
        using var provider = new BulkProvider();
        provider.Add("api");
        var services = DashboardTestSupport.Services(provider, Store(provider.PathOf("api")), new Testing.ManualTimeProvider(), new RecordingLog());
        var window = Window(services);
        window.ShowWorkspace("web", window.ActiveTab);

        Assert.Same(window.ActiveTab.Dashboard!.Toolbar, window.ActiveTab.Toolbar);
        Assert.Same(window.ActiveTab.Dashboard.Bulk, window.ActiveTab.OperationStatus);
        Assert.True(window.ToggleThemeCommand.CanExecute(null));
        AssertTheRestDisabledWithoutATooltip(window.ActiveTab.Toolbar);
        window.CloseAll();
        await SettleAsync();
    }

    [AvaloniaFact]
    public async Task Disabled_with_the_first_reason_that_holds_and_enabled_while_a_row_is_still_being_read()
    {
        using var provider = new BulkProvider();
        provider.Add("notes", MainRefs(remote: false));
        var detached = provider.Add("infra", new RepoRefs(new HeadState(null, Head), [new GitRef("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, Upstream, RemoteName: "origin")], [], ["origin"]));
        using var host = await BulkHost.StartAsync(provider, ["notes"]);
        var toolbar = host.Dashboard.Toolbar;

        // No row would be acted on: notes has no remote and cannot be pulled.
        Assert.Equal((false, "No repository of Web has a remote", "No repository of Web has a remote"), (toolbar.CanFetch, toolbar.FetchToolTip, toolbar.FetchDisabledReason));
        Assert.Equal((false, "No repository of Web can be pulled", "No repository of Web can be pulled"), (toolbar.CanPull, toolbar.PullToolTip, toolbar.PullDisabledReason));
        Assert.False(toolbar.FetchCommand.CanExecute(null));

        // A row still being read counts as one the run would act on (it waits for it).
        detached.RefsGate = new TaskCompletionSource();
        host.Services.Workspaces.Update(list => list with
        {
            Workspaces = [.. list.Workspaces.Select(workspace => workspace with { Repositories = [.. workspace.Repositories, new WorkspaceRepository(provider.PathOf("infra"))] })],
        });
        await WaitUntilAsync(() => host.Dashboard.Rows.Count == 2 && host["infra"].IsReading, "infra's read");
        Assert.Equal((true, true), (toolbar.CanFetch, toolbar.CanPull));
        Assert.Equal(PullEvery("Web"), toolbar.PullToolTip);

        // Read: detached with a remote: it can be fetched, not pulled.
        detached.RefsGate.SetResult();
        await WaitUntilAsync(() => host["infra"].HasRepository, "infra's read");
        Assert.Equal((true, false, "No repository of Web can be pulled"), (toolbar.CanFetch, toolbar.CanPull, toolbar.PullToolTip));
    }

    [AvaloniaFact]
    public async Task An_empty_workspace_says_so_and_a_running_run_holds_both_buttons()
    {
        using var provider = new BulkProvider();
        var api = provider.Add("api");
        api.FetchGate = new TaskCompletionSource();
        using var empty = await BulkHost.StartAsync(provider, []);
        Assert.Equal(("Web has no repositories", "Web has no repositories"), (empty.Dashboard.Toolbar.FetchToolTip, empty.Dashboard.Toolbar.PullToolTip));
        Assert.False(empty.Dashboard.Toolbar.CanFetch);

        using var host = await BulkHost.StartAsync(provider, ["api"]);
        var toolbar = host.Dashboard.Toolbar;

        // A click runs Fetch all; while it runs both wait for it.
        toolbar.FetchCommand.Execute(null);
        await api.FetchWaiting.Task.WaitAsync(Patience);
        Assert.Equal(BulkKind.Fetch, host.Bulk.Current?.Kind);
        Assert.Equal((false, "Wait for Fetch all to finish", "Wait for Fetch all to finish"), (toolbar.CanFetch, toolbar.FetchToolTip, toolbar.FetchDisabledReason));
        Assert.Equal((false, "Wait for Fetch all to finish"), (toolbar.CanPull, toolbar.PullToolTip));

        api.FetchGate.SetResult();
        await host.WaitForRunAsync();
        Assert.Equal((true, true), (toolbar.CanFetch, toolbar.CanPull));

        // The Pull button runs Pull all.
        api.PullGate = new TaskCompletionSource();
        toolbar.PullCommand.Execute(null);
        await api.PullIntegrating.Task.WaitAsync(Patience);
        Assert.Equal(BulkKind.Pull, host.Bulk.Current?.Kind);
        Assert.Equal(("Wait for Pull all to finish", "Wait for Pull all to finish"), (toolbar.FetchToolTip, toolbar.PullToolTip));
        api.PullGate.SetResult();
        await host.WaitForRunAsync();
        Assert.Equal("Pulled 1 repository", host.Bulk.RunText);
    }

    [Fact]
    public async Task A_button_that_fails_past_its_run_is_logged_under_the_workspaces_name()
    {
        var log = new RecordingLog();
        var toolbar = ToolbarViewModel.ForDashboard(new FailingDashboard(log));

        await toolbar.FetchCommand.ExecuteAsync(null);
        await toolbar.PullCommand.ExecuteAsync(null);

        Assert.Equal(["Web: the toolbar's Fetch failed.", "Web: the toolbar's Pull failed."], log.Messages);
    }

    [AvaloniaFact]
    public async Task The_tooltips_follow_a_rename()
    {
        using var provider = new BulkProvider();
        provider.Add("api");
        using var host = await BulkHost.StartAsync(provider, ["api"]);

        host.Services.Workspaces.Update(list => list with { Workspaces = [.. list.Workspaces.Select(workspace => workspace with { Name = "Web team" })] });
        await SettleAsync();

        Assert.Equal((FetchEvery("Web team"), PullEvery("Web team")), (host.Dashboard.Toolbar.FetchToolTip, host.Dashboard.Toolbar.PullToolTip));
    }

    /// <summary>"Fetch every repository of {workspace}" with Fetch's key (6.8): <c>(Ctrl+Shift+F)</c> once work package WP-K shows the keys.</summary>
    private static string FetchEvery(string workspace) => KeyTexts.Current.WithKey($"Fetch every repository of {workspace}", AppCommand.Fetch);

    /// <summary>"Pull every repository of {workspace}" with Pull's key (<c>Ctrl+Shift+L</c>).</summary>
    private static string PullEvery(string workspace) => KeyTexts.Current.WithKey($"Pull every repository of {workspace}", AppCommand.Pull);

    private static void AssertTheRestDisabledWithoutATooltip(ToolbarViewModel toolbar)
    {
        Assert.Equal(
            [(false, (string?)null, (string?)null), (false, null, null), (false, null, null), (false, null, null), (false, null, null), (false, null, null), (false, null, null)],
            new[]
            {
                (toolbar.CanPush, toolbar.PushToolTip, toolbar.PushDisabledReason),
                (toolbar.CanBranch, toolbar.BranchToolTip, toolbar.BranchDisabledReason),
                (toolbar.CanStash, toolbar.StashToolTip, toolbar.StashDisabledReason),
                (toolbar.CanPop, toolbar.PopToolTip, toolbar.PopDisabledReason),
                (toolbar.CanUndo, toolbar.UndoToolTip, toolbar.UndoDisabledReason),
                (toolbar.CanRedo, toolbar.RedoToolTip, toolbar.RedoDisabledReason),
                (toolbar.CanSearch, toolbar.SearchToolTip, toolbar.SearchDisabledReason),
            });
        Assert.False(toolbar.PushCommand.CanExecute(null));
        Assert.False(toolbar.SearchCommand.CanExecute(null));
    }

    /// <summary>The workspace "Web" whose Fetch all and Pull all can run and throw, which no run of the app does: what slips past it.</summary>
    private sealed class FailingDashboard(Core.Logging.IAppLog log) : IDashboardToolbar
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string WorkspaceName => "Web";

        public Core.Logging.IAppLog Log => log;

        public bool CanFetchAll => true;

        public string? FetchAllDisabledReason => null;

        public bool CanPullAll => true;

        public string? PullAllDisabledReason => null;

        public Task FetchAllAsync() => Task.FromException(new InvalidOperationException("Fetch all slipped."));

        public Task PullAllAsync() => Task.FromException(new InvalidOperationException("Pull all slipped."));
    }
}
