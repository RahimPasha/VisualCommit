using System.IO;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.Views;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The sweep of D462 (see <c>KeyboardPassTests.AfterPress.cs</c>), what its reading found missing:
/// a press whose run switches to the tab that already shows its folder; a press whose run hides it
/// with what stands in its place shown meanwhile (the clone form's Clone and Cancel clone); a run that
/// ends while a dialog stands over the window; Enter in a settings section's box whose Save shows
/// another section; the window's keys that take the focused element away (Stage all, Unstage all,
/// Close tab, Undo); and, as guards, the status bar's Cancel on a dashboard, a staged diff's Unstage
/// hunk and an unstaged one's Discard hunk on the file's last hunk, the dashboard's "…" menu's Delete
/// workspace…, the comparison's All differences hidden by its read, and the rows' menus opened by
/// the keys in the reflog, the file history and the dashboard.
/// </summary>
public sealed partial class KeyboardPassTests
{
    // ---- A run that switches tabs ----------------------------------------------------------------

    /// <summary>
    /// "graph" open in a tab, a new tab's welcome page: Open, pressed by the keys, its folder dialog
    /// held and answered with "graph"'s folder, activates the tab that shows it; that tab's graph has
    /// the focus, with the frame, and the new tab stays.
    /// </summary>
    [AvaloniaFact]
    public async Task Open_pressed_by_the_keys_choosing_a_folder_another_tab_shows_gives_the_focus_to_that_tabs_graph()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var shows = app.Session.Shell.ActiveTab;
        app.PressKey(Key.T, RawInputModifiers.Control);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab is { ShowsWelcome: true } tab && !ReferenceEquals(tab, shows), "the new tab's welcome page");
        var welcome = app.Session.Shell.ActiveTab;
        app.Settle();
        app.Folders.Answer(repo.Path);

        await PressWithTheFolderDialogHeldAsync(app, app.FindByAutomationId("OpenRepoButton"));
        await app.WaitForAsync(() => ReferenceEquals(app.Session.Shell.ActiveTab, shows) && !welcome.Welcome.OpenCommand.IsRunning, "the tab that shows the folder");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Open, a folder another tab shows chosen");
        Assert.Contains(welcome, app.Session.Shell.Tabs);
    }

    // ---- A run that hides the pressed button -----------------------------------------------------

    /// <summary>
    /// The clone form's Clone, pressed by the keys, its <c>git clone</c> held: while the clone runs,
    /// Clone is disabled and Cancel clone, which stands beside it, has the focus, with the frame;
    /// Enter there cancels the clone, and Clone has the focus back, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_clone_forms_Clone_pressed_by_the_keys_gives_the_focus_to_Cancel_clone_while_the_clone_runs()
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
        var start = app.FindByAutomationId("CloneStartButton");
        held.HoldNext("clone");

        await PressByTheKeysAsync(app, start, Key.Enter);
        await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        Assert.False(start.IsEffectivelyEnabled, "Clone is enabled while the clone runs.");

        await AssertFocusAsync(app, Framed(app.FindByAutomationId("CloneCancelButton"), KeyRegionRole.GraphArea), "Enter on Clone, the clone running");

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !welcome.IsCloning && !welcome.StartCloneCommand.IsRunning, "the clone to stop");
        held.Release();
        await AssertFocusAsync(app, Framed(start, KeyRegionRole.GraphArea), "Enter on Cancel clone");
    }

    // ---- A run that ends under a dialog ----------------------------------------------------------

    /// <summary>
    /// A workspace of one repository whose fetch its remote holds: Fetch all, pressed by the keys, is
    /// disabled while its run lasts; the settings screen is opened by its keys meanwhile and the run
    /// ends under it, the screen keeping the focus; once the screen is closed by Esc, Fetch all has
    /// the focus back, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Fetch_all_pressed_by_the_keys_whose_run_ends_under_the_settings_gets_the_focus_back_once_they_close()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var hold = await HoldFetchesAsync(data, repo);
        using var app = await OpenDashboardAsync(data, [repo.Path]);
        var fetch = app.FindByAutomationId("DashboardFetchAll");

        await PressByTheKeysAsync(app, fetch, Key.Enter);
        await hold.WaitForHeldAsync(1);
        app.Settle();
        var settings = await app.OpenSettingsAsync(SettingsOpening.Key);
        hold.Release();
        await app.WaitForBulkRunAsync(TimeSpan.FromSeconds(60));
        await app.WaitForAsync(() => fetch.IsEffectivelyEnabled, "Fetch all enabled again");
        app.Settle();
        Assert.True(app.FocusIsWithin(app.TopDialogView!), $"With the settings screen open the focus is on {FocusFact(app)}.");

        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => settings.IsClosed, "the settings screen to close");

        await AssertFocusAsync(app, Framed(fetch, KeyRegionRole.GraphArea), "Enter on Fetch all, its run ended under the settings screen");
    }

    /// <summary>
    /// The same, the settings screen's own focus on nothing for a moment as the run ends (as while a
    /// box of its own is disabled by its Save): Fetch all does not take the focus behind the screen;
    /// once the screen is closed, it has the focus back, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Fetch_all_pressed_by_the_keys_takes_no_focus_behind_a_dialog_whose_focus_is_on_nothing()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var hold = await HoldFetchesAsync(data, repo);
        using var app = await OpenDashboardAsync(data, [repo.Path]);
        var fetch = app.FindByAutomationId("DashboardFetchAll");

        await PressByTheKeysAsync(app, fetch, Key.Enter);
        await hold.WaitForHeldAsync(1);
        app.Settle();
        var settings = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.Window.FocusManager!.Focus(null);
        app.Settle();
        Assert.Equal(NothingFact, FocusFact(app));
        hold.Release();
        await app.WaitForBulkRunAsync(TimeSpan.FromSeconds(60));
        await app.WaitForAsync(() => fetch.IsEffectivelyEnabled, "Fetch all enabled again");

        var behind = false;
        try
        {
            await app.WaitForAsync(() => fetch.IsFocused, "Fetch all focused", FocusWait);
            behind = true;
        }
        catch (TimeoutException)
        {
        }

        Assert.False(behind, "Fetch all took the focus behind the settings screen.");

        settings.CancelCommand.Execute(null);
        await app.WaitForAsync(() => settings.IsClosed, "the settings screen to close");
        await AssertFocusAsync(app, Framed(fetch, KeyRegionRole.GraphArea), "the settings screen closed");
    }

    // ---- A settings box whose Save shows another section -----------------------------------------

    /// <summary>
    /// The settings screen, a user name typed in the Git section, the session's global git
    /// configuration locked outside the app: Enter in General's "Also ask for" box, which shows the
    /// frame, saves; the write fails, its error is closed, and the screen shows the Git section,
    /// which hides the box; the Git section's button, the screen's own first stop for that section,
    /// has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_in_a_General_box_whose_Save_fails_to_write_gives_the_focus_to_the_Git_sections_button()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        await app.WaitForSettingsReadsAsync(dialog);
        dialog.Git.Global.UserName = "Someone Else";
        app.ChooseSection(SettingsSection.General);
        app.MoveToNeutral();
        var locked = GitIsolation.SessionGlobalConfigFile(data.Path) + ".lock";
        File.WriteAllText(locked, string.Empty);
        try
        {
            await PressByTheKeysAsync(app, app.FindByAutomationId(app.TopDialogView!, "RunnableExtraBox"), Key.Enter);
            await AnswerAnyDialogsByClickAsync(app, () => (dialog.IsClosed || !dialog.ConfirmCommand.IsRunning) && app.Session.Shell.Dialogs.Open.Count <= 1);

            Assert.False(dialog.IsClosed, "The settings screen closed: the write did not fail.");
            Assert.Equal(SettingsSection.Git, dialog.SelectedSection);
            await AssertFocusAsync(app, Framed(app.FindByAutomationId(app.TopDialogView!, "SettingsNavGit"), KeyRegionRole.None), "Enter in General's box, the write failed");
        }
        finally
        {
            File.Delete(locked);
        }
    }

    // ---- The window's keys -----------------------------------------------------------------------

    /// <summary>README.md's row focused by F6, with the frame: Stage all's keys stage every file, and the row leaves; the staged list's first row has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task Stage_all_by_its_keys_on_an_unstaged_row_gives_the_focus_to_the_staged_lists_first_row()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var changes = Repository(app).Changes;
        await FocusReadmesRowAsync(app);
        Assert.Equal(ReadmesRowFact, FocusFact(app));

        app.PressKey(Key.A, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => !changes.HasUnstaged, "every file staged");

        await AssertFocusAsync(app, RowFact(changes.Staged.Rows[0].Key, "StagedFileList"), "Ctrl+Shift+A on README.md's row");
    }

    /// <summary>config/settings.json's staged row focused as Tab gives it: Unstage all's keys unstage every file, and the row leaves; the unstaged list's first row has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task Unstage_all_by_its_keys_on_a_staged_row_gives_the_focus_to_the_unstaged_lists_first_row()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var changes = Repository(app).Changes;
        await FocusByTheKeysAsync(app, StageRowButton(app, "StagedFileList", "config/settings.json"));

        app.PressKey(Key.X, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => !changes.HasStaged, "every file unstaged");

        await AssertFocusAsync(app, RowFact(changes.Unstaged.Rows[0].Key, "UnstagedFileList"), "Ctrl+Shift+X on config/settings.json's staged row");
    }

    /// <summary>Two tabs, the second active, its own button in the strip focused as Tab gives it: Close tab's keys close it; the tab now at its place has the focus on its own button, with the frame.</summary>
    [AvaloniaFact]
    public async Task Close_tab_by_its_keys_on_the_active_tabs_button_gives_the_focus_to_the_tab_now_at_its_place()
    {
        using var first = await Scenarios.ChangesAsync();
        using var second = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenTabsAsync(data, [first.Path, second.Path], activeTab: 1);
        var tabs = app.Session.Shell.Tabs;
        var remaining = tabs[0];
        await FocusByTheKeysAsync(app, TabButtonOf(app, tabs[1]));

        app.PressKey(Key.W, RawInputModifiers.Control);
        await app.WaitForAsync(() => tabs.Count == 1, "the second tab to close");

        await AssertFocusAsync(app, TabFact(remaining), "Ctrl+W on the active tab's button");
    }

    /// <summary>
    /// "graph" with an untracked file, its stash popped: README.md's row focused as Tab gives it, Undo's
    /// keys undo the pop, and README.md's row leaves; the row now at its place, the untracked file's,
    /// has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Undo_by_its_keys_on_a_row_the_undo_removes_gives_the_focus_to_the_row_now_at_its_place()
    {
        using var repo = await Scenarios.GraphAsync();
        repo.WriteFile("aside.txt", "aside\n");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var repository = Repository(app);
        await repository.Toolbar.PopCommand.ExecuteAsync(null);
        await app.WaitForIdleAsync(repository);
        await ShowStagePanelAsync(app);
        var changes = repository.Changes;
        await app.WaitForAsync(() => changes.Unstaged.Rows.Any(row => row.Key == "README.md") && changes.Unstaged.Rows.Any(row => row.Key == "aside.txt"), "README.md and aside.txt unstaged");
        var readme = changes.Unstaged.Rows.ToList().FindIndex(row => row.Key == "README.md");
        await FocusByTheKeysAsync(app, StageRowButton(app, "UnstagedFileList", "README.md"));

        app.PressKey(Key.Z, RawInputModifiers.Control);
        await app.WaitForAsync(() => changes.Unstaged.Rows.All(row => row.Key != "README.md"), "README.md's row to go");
        await app.WaitForIdleAsync(repository);

        var now = changes.Unstaged.Rows[Math.Min(readme, changes.Unstaged.Rows.Count - 1)].Key;
        await AssertFocusAsync(app, RowFact(now, "UnstagedFileList"), "Ctrl+Z on README.md's row");
    }

    // ---- Guards ----------------------------------------------------------------------------------

    /// <summary>
    /// A workspace of one repository whose fetch its remote holds, Fetch all clicked: the status
    /// bar's Cancel, pressed by the keys, cancels the run and hides itself; the dashboard's list has
    /// the focus, with the frame (a guard: the window's rule of the sweep gives it).
    /// </summary>
    [AvaloniaFact]
    public async Task Guard_the_status_bars_Cancel_on_a_dashboard_pressed_by_the_keys_gives_the_focus_to_the_dashboards_list()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var hold = await HoldFetchesAsync(data, repo);
        using var app = await OpenDashboardAsync(data, [repo.Path]);
        await app.FetchAllAsync();
        await hold.WaitForHeldAsync(1);
        var cancel = app.FindByAutomationId("OperationCancelButton");
        await app.WaitForAsync(() => cancel.IsEffectivelyVisible, "the status bar's Cancel");
        app.MoveToNeutral();

        await PressByTheKeysAsync(app, cancel, Key.Enter);
        await app.WaitForBulkRunAsync(TimeSpan.FromSeconds(60));

        await AssertFocusInDashboardListAsync(app, "Enter on the status bar's Cancel on a dashboard");
    }

    /// <summary>README.md's staged diff, one hunk: its Unstage hunk, reached by Tab and pressed by Enter, unstages it, and the diff closes; the graph has the focus, with the frame (a guard).</summary>
    [AvaloniaFact]
    public async Task Guard_Unstage_hunk_pressed_by_the_keys_on_the_files_last_hunk_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenStagedDiffAsync(app, Phase8aExpectations.K.Readme);
        Assert.Single(Repository(app).Diff!.Diff!.Hunks);

        await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "UnstageHunkButton", "Unstage hunk");
        Assert.True(app.FocusVisible, "Unstage hunk shows no keyboard frame.");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => Repository(app).Diff is null, "the diff to close");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Unstage hunk");
    }

    /// <summary>README.md's unstaged diff, one hunk: its Discard hunk, reached by Tab and pressed by Enter, confirmed by a click, discards it, and the diff closes; the graph has the focus, with the frame (a guard).</summary>
    [AvaloniaFact]
    public async Task Guard_Discard_hunk_pressed_by_the_keys_and_confirmed_on_the_files_last_hunk_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenUnstagedDiffAsync(app, Phase8aExpectations.K.Readme);
        Assert.Single(Repository(app).Diff!.Diff!.Hunks);

        await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "StageHunkButton", "Stage hunk");
        await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "DiscardHunkButton", "Discard hunk");
        Assert.True(app.FocusVisible, "Discard hunk shows no keyboard frame.");
        app.PressKey(Key.Enter);
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => Repository(app).Diff is null, "the diff to close");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Discard hunk, confirmed");
    }

    /// <summary>
    /// A workspace's dashboard, a tab on "graph" after it: the header's "…", pressed by the keys,
    /// opens its menu; "Delete workspace…" chosen by the keys and confirmed by a click closes the
    /// dashboard's tab with the button; "graph"'s tab shows, and its graph has the focus (a guard). It
    /// comes without the frame: the question's close gives "…" the focus back without it (D104), and
    /// the switch of tabs then frames as "…" was; the fact asserts the place alone.
    /// </summary>
    [AvaloniaFact]
    public async Task Guard_Delete_workspace_from_the_dashboards_menu_opened_by_the_keys_gives_the_focus_to_the_next_tabs_graph()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var other = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenDashboardAsync(data, [repo.Path], extraTab: other.Path);

        await PressByTheKeysAsync(app, app.FindByAutomationId("DashboardMore"), Key.Enter);
        await app.WaitForAsync(() => app.IsMenuOpen, "the dashboard's menu");
        await ChooseByTheKeysAsync(app, VisualCommit.App.ViewModels.Workspaces.WorkspaceTexts.DeleteWorkspace);
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 1 && app.Session.Shell.ActiveTab.Repository is not null, "the dashboard's tab to close");
        await app.WaitForRepositoryAsync();

        try
        {
            await app.WaitForAsync(() => app.FocusedAutomationId() == "GraphRows", "the focus on the graph", FocusWait);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"After Delete workspace… from the dashboard's menu the focus is on {FocusFact(app)}, not the graph.");
        }
    }

    /// <summary>
    /// "graph" with a branch lonely of a history of its own: the comparison of lonely with main, its
    /// first read held at <c>merge-base</c>; All differences, focused as Tab gives it, pressed by Space
    /// twice: the second read finds no common history and hides the switch; the focus stays in the
    /// comparison's panel, with the frame (a guard).
    /// </summary>
    [AvaloniaFact]
    public async Task Guard_All_differences_pressed_by_the_keys_and_hidden_by_its_read_leaves_the_focus_in_the_right_panel()
    {
        using var repo = await Scenarios.GraphAsync();
        await repo.GitAsync("switch", "--quiet", "--orphan", "lonely");
        await repo.CommitFileAsync("alone.txt", "alone\n", "Alone");
        await repo.GitAsync("switch", "--quiet", "main");
        using var data = new TempDirectory("data");
        var held = new HeldGitRunner();
        using var app = await OpenAsync(data, repo.Path, held.Probe);
        app.RightClick(await app.RefRowAsync("Local branches", "lonely"));
        held.HoldNext("merge-base");
        app.ClickMenuItem("Compare lonely with main");
        await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
        var comparison = Repository(app).Comparison!;
        var all = app.FindByAutomationId("CompareAllDifferences");
        await FocusByTheKeysAsync(app, all);

        app.PressKey(Key.Space);
        await app.WaitForAsync(() => comparison is { AllDifferences: true, IsLoading: false }, "all differences shown");
        app.PressKey(Key.Space);
        await app.WaitForAsync(() => !all.IsEffectivelyVisible && !comparison.IsLoading, "the switch to go with no common history");
        held.Release();
        await app.WaitForIdleAsync();

        try
        {
            await app.WaitForAsync(() => app.FocusedRegion() == KeyRegionRole.Right && app.FocusVisible, "the focus in the right panel, with the frame", FocusWait);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"After the switch went the focus is on {FocusFact(app)}, not in the right panel with the frame.");
        }
    }

    /// <summary>
    /// A workspace of two repositories: the first row, focused as Tab gives it, its menu opened by
    /// Shift+F10 and "Remove from workspace" chosen by the keys; once the row has left, the focus
    /// lies in the dashboard's list, where the dashboard puts it (a guard).
    /// </summary>
    [AvaloniaFact]
    public async Task Guard_a_dashboard_row_removed_from_its_menu_opened_by_the_keys_leaves_the_focus_in_the_list()
    {
        using var first = await Scenarios.RemoteSyncAsync();
        using var second = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenDashboardAsync(data, [first.Path, second.Path]);
        var dashboard = app.ActiveDashboard!;
        var list = app.Find<ListBox>("DashboardList");
        await app.WaitForAsync(() => list.ContainerFromIndex(0) is ListBoxItem, "the first row's control");
        var row = (ListBoxItem)list.ContainerFromIndex(0)!;
        await FocusByTheKeysAsync(app, row);

        app.PressKey(Key.F10, RawInputModifiers.Shift);
        await ChooseByTheKeysAsync(app, VisualCommit.App.ViewModels.Workspaces.WorkspaceTexts.RemoveFromWorkspace);
        await AnswerAnyDialogsByClickAsync(app, () => dashboard.Rows.Count == 1);

        try
        {
            await app.WaitForAsync(() => app.FocusIsWithin(list), "the focus in the dashboard's list", FocusWait);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"After the row was removed from its menu the focus is on {FocusFact(app)}, not in the dashboard's list.");
        }
    }

    /// <summary>
    /// HEAD's reflog, its list focused, its second row chosen by Down: its menu opened by Shift+F10 and
    /// "Reset main to this commit…" chosen by the keys, the reset confirmed by a click; once the page
    /// has read the reflog again, the focus lies in its list (a guard).
    /// </summary>
    [AvaloniaFact]
    public async Task Guard_a_reset_from_a_reflog_rows_menu_opened_by_the_keys_leaves_the_focus_in_the_reflogs_list()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.RightClick(await app.RefRowAsync("Local branches"));
        app.Menu();
        app.ClickMenuItem("Show reflog");
        await app.WaitForAsync(() => Repository(app).Page is ReflogViewModel && app.ReflogSnapshot().ListHasFocus, "the reflog with the focus in its list");
        await app.WaitForAsync(() => app.ReflogSnapshot() is { IsLoading: false, Rows.Count: > 2 }, "the reflog page to show its rows");
        await app.WaitForIdleAsync();
        var page = (ReflogViewModel)Repository(app).Page!;
        var entries = page.Rows.Count;
        var list = app.FindByAutomationId("ReflogList");
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        await app.WaitForAsync(() => page.SelectedRow is not null && app.FocusIsWithin(list), $"a row chosen, the focus in the list (it is on {app.DescribeFocus()})");

        app.PressKey(Key.F10, RawInputModifiers.Shift);
        await ChooseByTheKeysAsync(app, "Reset main to this commit…");
        await AnswerAnyDialogsByClickAsync(app, () => !app.Session.Shell.Dialogs.IsOpen && page.Rows.Count > entries && !page.IsLoading && Repository(app).Queue.Current is null);

        try
        {
            await app.WaitForAsync(() => app.FocusIsWithin(list), "the focus in the reflog's list", FocusWait);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"After the reset from the reflog's row the focus is on {FocusFact(app)}, not in the reflog's list.");
        }
    }

    /// <summary>
    /// src/Calculator.cs's file history, its list focused, a row chosen by Down: its menu opened by
    /// Shift+F10 and "Show in graph" chosen by the keys closes the page; the graph has the focus (a
    /// guard).
    /// </summary>
    [AvaloniaFact]
    public async Task Guard_Show_in_graph_from_a_file_history_rows_menu_opened_by_the_keys_gives_the_focus_to_the_graph()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenFileHistoryAsync(app, Phase8aExpectations.K.Calculator);
        var history = (FileHistoryViewModel)Repository(app).Page!;
        var list = app.FindByAutomationId("FileHistoryList");
        await app.WaitForAsync(() => app.FocusIsWithin(list), $"the focus in the file history's list (it is on {app.DescribeFocus()})");
        app.PressKey(Key.Down);
        await app.WaitForAsync(() => history.SelectedRow is not null && app.FocusIsWithin(list), $"a row chosen, the focus in the list (it is on {app.DescribeFocus()})");

        app.PressKey(Key.F10, RawInputModifiers.Shift);
        await ChooseByTheKeysAsync(app, "Show in graph");
        await app.WaitForAsync(() => Repository(app).Page is null, "the page to close");

        await AssertFocusOnAsync(app, "GraphRows", "Show in graph from the file history's row");
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    /// <summary>The own button of <paramref name="path"/>'s row in the stage panel's list <paramref name="list"/>.</summary>
    private static Button StageRowButton(ShellDriver app, string list, string path) =>
        app.Window.GetVisualDescendants().OfType<Button>().First(button =>
            AutomationProperties.GetAutomationId(button) == "StageFileRow" && button.IsEffectivelyVisible
            && ShellDriver.StageRowOf(button)?.DataContext is VisualCommit.App.ViewModels.Panels.ChangedFileRow { Key: var key } && key == path
            && button.GetVisualAncestors().OfType<ItemsControl>().Any(items => items.Name == list));

    /// <summary>The own button of <paramref name="tab"/> in the tab strip.</summary>
    private static Button TabButtonOf(ShellDriver app, RepoTabViewModel tab)
    {
        var list = app.Find<ItemsControl>("TabList");
        var container = list.ContainerFromItem(tab) ?? throw new InvalidOperationException($"The tab {tab.Title} has no control in the strip.");
        return container.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == tab.TabAutomationId);
    }
}
