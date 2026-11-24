using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The sweep of D462 (see <c>KeyboardPassTests.AfterPress.cs</c>) on an empty tab's pages and the
/// dashboard: the welcome page's buttons and its clone form, the first-run screen's "Continue without
/// it", the status bar's Cancel, and a workspace's dashboard, its header's buttons and its empty
/// state's.
/// </summary>
public sealed partial class KeyboardPassTests
{
    // ---- The welcome page ------------------------------------------------------------------------

    /// <summary>Open, pressed by the keys, its folder dialog cancelled: Open has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task Open_on_the_welcome_page_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data);
        var open = app.FindByAutomationId("OpenRepoButton");

        await PressWithTheFolderDialogHeldAsync(app, open);
        await app.WaitForAsync(() => !app.Session.Shell.ActiveTab.Welcome.OpenCommand.IsRunning, "the cancelled folder dialog");

        await AssertFocusAsync(app, WelcomeOpenFact, "Enter on Open, cancelled");
    }

    /// <summary>Open, pressed by the keys, a repository chosen: the tab shows it, the welcome page gone; the graph has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task Open_on_the_welcome_page_pressed_by_the_keys_that_opens_a_repository_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data);
        app.Folders.Answer(repo.Path);

        await PressWithTheFolderDialogHeldAsync(app, app.FindByAutomationId("OpenRepoButton"));
        await app.WaitForRepositoryAsync();

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Open, a repository chosen");
    }

    /// <summary>Init, pressed by the keys, its folder dialog cancelled: Init has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task Init_on_the_welcome_page_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data);
        var init = app.FindByAutomationId("InitRepoButton");

        await PressWithTheFolderDialogHeldAsync(app, init);
        await app.WaitForAsync(() => !app.Session.Shell.ActiveTab.Welcome.InitCommand.IsRunning, "the cancelled folder dialog");

        await AssertFocusAsync(app, Framed(init, KeyRegionRole.GraphArea), "Enter on Init, cancelled");
    }

    /// <summary>A welcome page that lists "graph" as recent: its row, pressed by the keys, opens it, the welcome page gone; the graph has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task A_recent_repository_pressed_by_the_keys_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        ShellDriver.WriteSession(data.Path, new SessionState { Recent = [new RecentRepository(repo.Path, "graph", DateTimeOffset.UnixEpoch)] });
        using var app = await StartEmptyAsync(data);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.ShowsWelcome && app.Session.Shell.ActiveTab.Welcome.HasRecent, "the welcome page with a recent repository");
        var recent = app.Window.GetVisualDescendants().OfType<Button>().First(button => AutomationProperties.GetAutomationId(button) == "RecentRepository" && button.IsEffectivelyVisible);

        await PressByTheKeysAsync(app, recent, Key.Enter);
        await app.WaitForAsync(() => !app.Session.Shell.ActiveTab.ShowsWelcome && app.Session.Shell.ActiveTab.Repository is { Graph.Count: > 0 }, "the recent repository shown");
        await app.WaitForRepositoryAsync();

        await AssertFocusAsync(app, GraphFramedFact, "Enter on a recent repository");
    }

    /// <summary>New workspace…, pressed by the keys, its name dialog cancelled by Esc: New workspace… has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task New_workspace_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data);
        var button = app.FindByAutomationId("NewWorkspaceButton");

        await PressByTheKeysAsync(app, button, Key.Enter);
        var dialog = await app.WaitForDialogAsync<NameDialogViewModel>();
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => dialog.IsClosed && !app.Session.Shell.ActiveTab.Welcome.NewWorkspaceCommand.IsRunning, "the name dialog to close");

        await AssertFocusAsync(app, Framed(button, KeyRegionRole.GraphArea), "Enter on New workspace…, cancelled");
    }

    /// <summary>The clone form's Browse…, pressed by the keys, its folder dialog cancelled: Browse… has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_clone_forms_Browse_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data);
        await OpenCloneFormAsync(app);
        var browse = app.FindByAutomationId("CloneBrowseButton");

        await PressWithTheFolderDialogHeldAsync(app, browse);
        await app.WaitForAsync(() => !app.Session.Shell.ActiveTab.Welcome.BrowseCloneParentCommand.IsRunning, "the cancelled folder dialog");

        await AssertFocusAsync(app, Framed(browse, KeyRegionRole.GraphArea), "Enter on Browse…, cancelled");
    }

    /// <summary>The clone form's Clone, pressed by the keys, on a folder that is no repository: the clone fails, and Clone has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_clone_forms_Clone_pressed_by_the_keys_that_fails_gets_the_focus_back_with_the_frame()
    {
        using var data = new TempDirectory("data");
        using var nowhere = new TempDirectory("not a repository");
        using var parent = new TempDirectory("clones");
        using var app = await StartEmptyAsync(data);
        var welcome = await OpenCloneFormAsync(app);
        welcome.CloneUrl = nowhere.Path;
        welcome.CloneParent = parent.Path;
        welcome.CloneName = "copy";
        app.Settle();
        var start = app.FindByAutomationId("CloneStartButton");

        await PressByTheKeysAsync(app, start, Key.Enter);
        await app.WaitForAsync(() => !welcome.IsCloning && !welcome.StartCloneCommand.IsRunning && welcome.HasError, "the clone to fail");

        await AssertFocusAsync(app, Framed(start, KeyRegionRole.GraphArea), "Enter on Clone, failed");
    }

    /// <summary>The clone form's Clone, pressed by the keys, on a repository: the tab shows the clone, the welcome page gone; the graph has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_clone_forms_Clone_pressed_by_the_keys_that_clones_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var parent = new TempDirectory("clones");
        using var app = await StartEmptyAsync(data);
        var welcome = await OpenCloneFormAsync(app);
        welcome.CloneUrl = repo.Path;
        welcome.CloneParent = parent.Path;
        welcome.CloneName = "copy";
        app.Settle();

        await PressByTheKeysAsync(app, app.FindByAutomationId("CloneStartButton"), Key.Enter);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository is { Graph.Count: > 0 }, "the clone shown", TimeSpan.FromSeconds(60));
        await app.WaitForRepositoryAsync();

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Clone");
    }

    /// <summary>
    /// A clone held at <c>git clone</c>: the form's Cancel clone, pressed by the keys, cancels it and
    /// hides itself once the clone has stopped; Clone, which stands in its place again, has the
    /// focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_clone_forms_Cancel_clone_pressed_by_the_keys_gives_the_focus_to_Clone_with_the_frame()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var parent = new TempDirectory("clones");
        var held = new HeldGitRunner();
        using var app = await StartEmptyAsync(data, held.Probe);
        var welcome = await OpenCloneFormAsync(app);
        welcome.CloneUrl = repo.Path;
        welcome.CloneParent = parent.Path;
        welcome.CloneName = "copy";
        app.Settle();
        held.HoldNext("clone");
        welcome.StartCloneCommand.Execute(null);
        await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        var start = app.FindByAutomationId("CloneStartButton");

        await PressByTheKeysAsync(app, app.FindByAutomationId("CloneCancelButton"), Key.Enter);
        await app.WaitForAsync(() => !welcome.IsCloning && !welcome.StartCloneCommand.IsRunning, "the clone to stop");
        held.Release();

        await AssertFocusAsync(app, Framed(start, KeyRegionRole.GraphArea), "Enter on Cancel clone");
    }

    /// <summary>The clone form's Cancel, pressed by the keys, closes the form with itself; Clone on the welcome page, which opened the form, has the focus, with the frame.</summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public async Task The_clone_forms_Cancel_pressed_by_the_keys_gives_the_focus_to_the_button_that_opened_the_form(Key key)
    {
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data);
        var welcome = await OpenCloneFormAsync(app);

        await PressByTheKeysAsync(app, app.FindByAutomationId("CloneCloseButton"), key);
        await app.WaitForAsync(() => !welcome.IsCloneFormOpen, "the clone form to close");

        await AssertFocusAsync(app, Framed(app.FindByAutomationId("CloneRepoButton"), KeyRegionRole.GraphArea), $"{key} on the clone form's Cancel");
    }

    // ---- The first-run screen --------------------------------------------------------------------

    /// <summary>
    /// The first-run screen with git found and the helper for git's questions missing: "Continue
    /// without it", pressed by the keys, hides the screen with itself; the welcome page's Open, the
    /// graph area's target, has the focus, with the frame.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public async Task Continue_without_the_helper_pressed_by_the_keys_gives_the_focus_to_the_welcome_pages_Open_with_the_frame(Key key)
    {
        using var data = new TempDirectory("data");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.RealAt(Phase8aExpectations.F.WindowsGit));
        using var app = ShellDriver.Start(data.Path, gitProbe: probe, askPassHelper: Phase8aExpectations.F.MissingHelper);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        await app.WaitForGitSetupAsync(guidance: Phase8aExpectations.F.HelperMissingGuidance);

        await PressByTheKeysAsync(app, app.FindByAutomationId("GitSetupContinueButton"), key);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.ShowsWelcome && !app.Session.Shell.ActiveTab.ShowsGitSetup, "the screen to go");

        await AssertFocusAsync(app, WelcomeOpenFact, $"{key} on Continue without it");
    }

    // ---- The status bar --------------------------------------------------------------------------

    /// <summary>
    /// A fetch held by its remote: the status bar's Cancel, pressed by the keys, cancels it and hides
    /// itself; the graph has the focus, with the frame (the status bar is no region of its own).
    /// </summary>
    [AvaloniaFact]
    public async Task The_status_bars_Cancel_pressed_by_the_keys_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var hold = await HoldFetchesAsync(data, repo);
        using var app = await OpenAsync(data, repo.Path);
        var repository = Repository(app);
        repository.Toolbar.FetchCommand.Execute(null);
        await hold.WaitForHeldAsync(1);
        var cancel = app.FindByAutomationId("OperationCancelButton");
        await app.WaitForAsync(() => cancel.IsEffectivelyVisible, "the status bar's Cancel");

        await PressByTheKeysAsync(app, cancel, Key.Enter);
        await app.WaitForAsync(() => !cancel.IsEffectivelyVisible, "Cancel to go");
        await app.WaitForIdleAsync(repository, TimeSpan.FromSeconds(60));

        await AssertFocusAsync(app, GraphFramedFact, "Enter on the status bar's Cancel");
    }

    // ---- The dashboard ---------------------------------------------------------------------------

    /// <summary>A workspace of one repository: the header's Add repository…, pressed by the keys, its folder dialog cancelled, has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_dashboards_Add_repository_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenDashboardAsync(data, [repo.Path]);
        var add = app.FindByAutomationId("DashboardAddRepository");

        await PressWithTheFolderDialogHeldAsync(app, add);
        await app.WaitForAsync(() => !app.ActiveDashboard!.AddRepositoryCommand.IsRunning, "the cancelled folder dialog");

        await AssertFocusAsync(app, Framed(add, KeyRegionRole.GraphArea), "Enter on Add repository…, cancelled");
    }

    /// <summary>An empty workspace: its empty state's Add repository…, pressed by the keys, its folder dialog cancelled, has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_empty_dashboards_Add_repository_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        using var data = new TempDirectory("data");
        using var app = await OpenDashboardAsync(data, []);
        var add = app.FindByAutomationId("DashboardEmptyAddRepository");

        await PressWithTheFolderDialogHeldAsync(app, add);
        await app.WaitForAsync(() => !app.ActiveDashboard!.AddRepositoryCommand.IsRunning, "the cancelled folder dialog");

        await AssertFocusAsync(app, Framed(add, KeyRegionRole.GraphArea), "Enter on the empty state's Add repository…, cancelled");
    }

    /// <summary>
    /// An empty workspace, a repository open in a second tab: its empty state's Add open tabs,
    /// pressed by the keys, adds that repository, and the empty state goes with the button; the
    /// dashboard's list has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_empty_dashboards_Add_open_tabs_pressed_by_the_keys_gives_the_focus_to_the_dashboards_list_with_the_frame()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenDashboardAsync(data, [], extraTab: repo.Path);
        var dashboard = app.ActiveDashboard!;

        await PressByTheKeysAsync(app, app.FindByAutomationId("DashboardEmptyAddOpenTabs"), Key.Enter);
        await app.WaitForAsync(() => dashboard.HasRows, "the open tab's repository added");

        await AssertFocusInDashboardListAsync(app, "Enter on Add open tabs");
    }

    /// <summary>
    /// A workspace of one repository whose fetch its remote holds: Cancel all, pressed by the keys,
    /// cancels the run and hides itself; the dashboard's list has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Cancel_all_pressed_by_the_keys_gives_the_focus_to_the_dashboards_list_with_the_frame()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var hold = await HoldFetchesAsync(data, repo);
        using var app = await OpenDashboardAsync(data, [repo.Path]);
        var dashboard = app.ActiveDashboard!;
        await app.FetchAllAsync();
        await hold.WaitForHeldAsync(1);
        var cancel = app.FindByAutomationId("DashboardCancelAll");
        await app.WaitForAsync(() => cancel.IsEffectivelyVisible, "Cancel all");

        await PressByTheKeysAsync(app, cancel, Key.Enter);
        await app.WaitForBulkRunAsync(TimeSpan.FromSeconds(60));

        await AssertFocusInDashboardListAsync(app, "Enter on Cancel all");
    }

    /// <summary>
    /// A workspace of one repository whose fetch its remote holds: the header's Fetch all, pressed
    /// by the keys, is disabled while the run lasts; once the hold is let go and the run has ended,
    /// Fetch all has the focus back, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Fetch_all_pressed_by_the_keys_gets_the_focus_back_with_the_frame_after_the_run()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var hold = await HoldFetchesAsync(data, repo);
        using var app = await OpenDashboardAsync(data, [repo.Path]);
        var fetch = app.FindByAutomationId("DashboardFetchAll");

        await PressByTheKeysAsync(app, fetch, Key.Enter);
        await hold.WaitForHeldAsync(1);
        app.Settle();
        Assert.False(fetch.IsEffectivelyEnabled, "Fetch all is enabled while its run lasts.");
        hold.Release();
        await app.WaitForBulkRunAsync(TimeSpan.FromSeconds(60));
        await app.WaitForAsync(() => fetch.IsEffectivelyEnabled, "Fetch all enabled again");

        await AssertFocusAsync(app, Framed(fetch, KeyRegionRole.GraphArea), "Enter on Fetch all");
    }

    /// <summary>The same with the toolbar's Fetch on the dashboard's tab, which runs Fetch all: Fetch has the focus back, with the frame, after the run.</summary>
    [AvaloniaFact]
    public async Task The_toolbars_Fetch_on_a_dashboard_pressed_by_the_keys_gets_the_focus_back_with_the_frame_after_the_run()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var hold = await HoldFetchesAsync(data, repo);
        using var app = await OpenDashboardAsync(data, [repo.Path]);
        var fetch = app.FindByAutomationId("FetchButton");

        await PressByTheKeysAsync(app, fetch, Key.Enter);
        await hold.WaitForHeldAsync(1);
        app.Settle();
        Assert.False(fetch.IsEffectivelyEnabled, "Fetch is enabled while Fetch all runs.");
        hold.Release();
        await app.WaitForBulkRunAsync(TimeSpan.FromSeconds(60));
        await app.WaitForAsync(() => fetch.IsEffectivelyEnabled, "Fetch enabled again");

        await AssertFocusAsync(app, Framed(fetch, KeyRegionRole.None), "Enter on the toolbar's Fetch");
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    /// <summary>
    /// <paramref name="control"/> pressed by the keys with the folder dialog held open, as the native
    /// one stays open until the user answers: the control is disabled meanwhile, its run waiting,
    /// and the focus has left it; then the dialog answers with what was queued (cancelled when nothing).
    /// </summary>
    private static async Task PressWithTheFolderDialogHeldAsync(ShellDriver app, Control control)
    {
        var held = new TaskCompletionSource();
        app.Folders.FolderHold = held.Task;
        var asked = app.Folders.Titles.Count;
        await PressByTheKeysAsync(app, control, Key.Enter);
        await app.WaitForAsync(() => app.Folders.Titles.Count == asked + 1, "the folder dialog");
        app.Settle();
        Assert.False(control.IsEffectivelyEnabled, $"{AutomationProperties.GetAutomationId(control)} is enabled while its folder dialog is open.");
        app.Folders.FolderHold = null;
        held.SetResult();
    }

    /// <summary>
    /// Holds every fetch of "sync"'s copy <paramref name="repo"/> from its origin (<see cref="FetchHold"/>),
    /// whose script names its marker by the remote's folder as the configuration writes it: origin,
    /// next to the working tree, set as <c>../origin.git</c> first.
    /// </summary>
    private static async Task<FetchHold> HoldFetchesAsync(TempDirectory data, TempRepo repo)
    {
        await repo.GitAsync("remote", "set-url", "origin", "../origin.git");
        var hold = FetchHold.Install(data.Path);
        hold.HoldIn(repo.Path);
        return hold;
    }

    /// <summary><paramref name="control"/>, focused with the keyboard's frame, in <paramref name="region"/>, as <see cref="FocusFact"/> names it.</summary>
    private static string Framed(Control control, KeyRegionRole region) =>
        $"{control.GetType().Name} {AutomationProperties.GetAutomationId(control)} \"{AutomationProperties.GetName(control)}\" [{region}], framed";

    /// <summary>One empty tab, git looked for by <paramref name="probe"/> (the machine's search when null), once the start-up work has ended, the pointer at rest.</summary>
    private static async Task<ShellDriver> StartEmptyAsync(TempDirectory data, IGitSetupProbe? probe = null)
    {
        var app = ShellDriver.Start(data.Path, gitProbe: probe);
        try
        {
            await app.WaitUntilReadyAsync();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>The welcome page's Clone clicked: the form open, the focus in its URL box.</summary>
    private static async Task<VisualCommit.App.ViewModels.WelcomeViewModel> OpenCloneFormAsync(ShellDriver app)
    {
        app.Click(app.FindByAutomationId("CloneRepoButton"));
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneUrlBox", "the focus in the clone form's URL box");
        app.MoveToNeutral();
        return app.Session.Shell.ActiveTab.Welcome;
    }

    /// <summary>
    /// The dashboard of a workspace "Sweep" holding <paramref name="repositories"/>, its tab first and
    /// active, then a tab on <paramref name="extraTab"/> when given; read and idle, the pointer at rest.
    /// </summary>
    private static async Task<ShellDriver> OpenDashboardAsync(TempDirectory data, IReadOnlyList<string> repositories, string? extraTab = null)
    {
        var workspace = new Workspace("sweep", "Sweep", [.. repositories.Select(path => new WorkspaceRepository(path))]);
        var app = ShellDriver.StartWithWorkspace(
            data.Path,
            new WorkspaceList { Workspaces = [workspace] },
            openDashboardOf: workspace.Id,
            extraTabs: extraTab is null ? null : [new TabState(extraTab)]);
        try
        {
            await app.WaitUntilReadyAsync();
            if (extraTab is not null)
            {
                await app.WaitForTabOpenedAsync(extraTab);
            }

            if (repositories.Count > 0)
            {
                await app.WaitForDashboardReadAsync();
            }

            app.Settle();
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>Waits, a short while, for the focus to lie in the dashboard's list with the keyboard's frame, and asserts it.</summary>
    private static async Task AssertFocusInDashboardListAsync(ShellDriver app, string after)
    {
        var list = app.FindByAutomationId("DashboardList");
        try
        {
            await app.WaitForAsync(() => app.FocusIsWithin(list) && app.FocusVisible, "the focus in the dashboard's list, with the frame", FocusWait);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"After {after} the focus is on {FocusFact(app)}, not in the dashboard's list with the frame.");
        }

        app.Settle();
        Assert.True(app.FocusIsWithin(list) && app.FocusVisible, $"After {after}, once settled, the focus is on {FocusFact(app)}.");
    }
}
