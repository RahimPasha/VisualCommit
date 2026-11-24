using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The window's rule for an action by the keys (D462; see <c>KeyboardPassTests.AfterPress.cs</c>),
/// what the second reading of it found: a loss that the window's own deactivation makes (another
/// program brought to the front) is no loss of an action's, and the rule gives no focus while the
/// window is inactive; a focus the user moves after the press ends the press's hold on it, also when
/// the user's own later action loses it; a text box given the focus back keeps its caret and
/// selection; the hand-over while a run lasts is the clone form's alone, so a failed Stage lines has
/// the focus back; a press of the mouse on an element ends what the keys armed on it; the rule keeps
/// no closed tab alive; and a key repeated on the framed element arms one follower, not one per
/// press.
/// </summary>
public sealed partial class KeyboardPassTests
{
    // ---- The window's deactivation ---------------------------------------------------------------

    /// <summary>
    /// The summary reached by Tab, "draft" typed and Stage all's keys pressed there (which arms the
    /// window's rule on it); the window then loses activation as Alt+Tab makes it (the platform's
    /// Deactivated, then LostFocus) and gets it back: no focus is given while it is inactive, and the
    /// summary has the focus again with the caret after "draft", nothing selected.
    /// </summary>
    [AvaloniaFact]
    public async Task A_switch_to_another_program_and_back_leaves_the_summarys_draft_unselected()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var changes = Repository(app).Changes;
        var summary = (TextBox)app.FindByAutomationId("CommitSummary");
        await FocusByTheKeysAsync(app, summary);
        app.Type("draft");
        app.PressKey(Key.A, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => !changes.HasUnstaged, "every file staged");
        await app.WaitForIdleAsync(Repository(app));
        app.Settle();
        Assert.True(
            ReferenceEquals(app.FocusedElement, summary) && summary is { SelectionStart: 5, SelectionEnd: 5 },
            $"Before the switch the focus is on {FocusFact(app)}, the selection {summary.SelectionStart}..{summary.SelectionEnd}.");

        PlatformCallback(app.Window, "Deactivated")();
        PlatformCallback(app.Window, "LostFocus")();
        Assert.False(app.Window.IsActive, "The window is still active after its platform reported it deactivated.");
        Assert.Null(app.FocusedElement);
        app.Settle();
        Assert.True(app.FocusedElement is null, $"While the window is inactive the focus was given to {FocusFact(app)}.");

        PlatformCallback(app.Window, "Activated")();
        app.Settle();
        await app.WaitForIdleAsync(Repository(app));
        app.Settle();
        Assert.True(ReferenceEquals(app.FocusedElement, summary), $"After the switch back the focus is on {FocusFact(app)}.");
        Assert.True(summary is { SelectionStart: 5, SelectionEnd: 5 }, $"After the switch back the selection is {summary.SelectionStart}..{summary.SelectionEnd}.");
        app.Type("!");
        Assert.Equal("draft!", summary.Text);
    }

    /// <summary>
    /// A workspace of one repository whose fetch its remote holds: the toolbar's Fetch, pressed by the
    /// keys, is disabled while Fetch all runs; the user clicks the dashboard's first row and then
    /// brings another program to the front; the run ends while the window is inactive. Once the
    /// window is active again, the focus is on the row the user clicked, not on Fetch.
    /// </summary>
    [AvaloniaFact]
    public async Task A_keyboard_Fetch_ending_while_the_window_is_inactive_leaves_the_row_the_user_clicked()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var hold = await HoldFetchesAsync(data, repo);
        using var app = await OpenDashboardAsync(data, [repo.Path]);
        var fetch = app.FindByAutomationId("FetchButton");
        var list = app.Find<ListBox>("DashboardList");

        await PressByTheKeysAsync(app, fetch, Key.Enter);
        await hold.WaitForHeldAsync(1);
        app.Settle();
        Assert.False(fetch.IsEffectivelyEnabled, "Fetch is enabled while Fetch all runs.");
        await app.WaitForAsync(() => list.ContainerFromIndex(0) is ListBoxItem, "the first row's control");
        app.Click(list.ContainerFromIndex(0)!);
        app.MoveToNeutral();
        Assert.True(app.FocusIsWithin(list), $"The click on the dashboard's first row left the focus on {FocusFact(app)}.");

        PlatformCallback(app.Window, "Deactivated")();
        PlatformCallback(app.Window, "LostFocus")();
        hold.Release();
        await app.WaitForBulkRunAsync(TimeSpan.FromSeconds(60));
        await app.WaitForAsync(() => fetch.IsEffectivelyEnabled, "Fetch enabled again");
        app.Settle();

        PlatformCallback(app.Window, "Activated")();
        app.Settle();
        Assert.True(app.FocusIsWithin(list), $"After the switch back the focus is on {FocusFact(app)}, not on the row the user clicked.");
    }

    // ---- A focus the user moved during a run -----------------------------------------------------

    /// <summary>
    /// "sync" with a new file, its fetch held by its remote: the toolbar's Fetch, pressed by the
    /// keys, keeps the focus while it runs; notes.txt's Stage is clicked with the mouse, takes the
    /// focus by the pointer and, its run waiting behind the fetch, is disabled, the focus going to
    /// nothing. Once the fetch is let go and the file is staged, the focus is not on Fetch: the user
    /// moved it, and a click arms nothing.
    /// </summary>
    [AvaloniaFact]
    public async Task A_mouse_Stage_during_a_keyboard_Fetch_leaves_the_focus_off_Fetch()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        repo.WriteFile("notes.txt", "notes\n");
        using var data = new TempDirectory("data");
        using var hold = await HoldFetchesAsync(data, repo);
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var repository = Repository(app);
        var changes = repository.Changes;
        await app.WaitForAsync(() => changes.Unstaged.Rows.Any(row => row.Key == "notes.txt"), "notes.txt unstaged");
        var fetch = app.FindByAutomationId("FetchButton");

        await PressByTheKeysAsync(app, fetch, Key.Enter);
        await hold.WaitForHeldAsync(1);
        app.Settle();
        Assert.True(fetch.IsFocused, $"Fetch lost the focus as its run began: {FocusFact(app)}.");

        // The row's buttons show while the pointer is over it.
        app.MoveMouse(StageRowButton(app, "UnstagedFileList", "notes.txt"));
        app.Click(StageButtonOf(app, "UnstagedFileList", "notes.txt"));
        await app.WaitForAsync(() => changes.StageFileCommand.IsRunning, "the Stage waiting behind the fetch");
        app.MoveToNeutral();
        Assert.Equal(NothingFact, FocusFact(app));

        hold.Release();
        await app.WaitForAsync(() => changes.Staged.Rows.Any(row => row.Key == "notes.txt"), "notes.txt staged", TimeSpan.FromSeconds(60));
        await app.WaitForIdleAsync(repository, TimeSpan.FromSeconds(60));
        app.Settle();

        Assert.False(fetch.IsFocused, $"The keyboard Fetch's look took the focus the mouse's Stage had left: {FocusFact(app)}.");
    }

    /// <summary>
    /// The same, notes.txt's Stage reached by Tab and pressed by Enter while the keyboard Fetch is
    /// held: once the fetch is let go and the file is staged, the row now at its place, notes.txt's
    /// in the staged list, has the focus, with the frame (the stage row's own return), not Fetch.
    /// </summary>
    [AvaloniaFact]
    public async Task A_keyboard_Stage_during_a_keyboard_Fetch_gives_the_focus_to_the_row_now_at_its_place()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        repo.WriteFile("notes.txt", "notes\n");
        using var data = new TempDirectory("data");
        using var hold = await HoldFetchesAsync(data, repo);
        using var app = await OpenAsync(data, repo.Path);
        await ShowStagePanelAsync(app);
        var repository = Repository(app);
        var changes = repository.Changes;
        await app.WaitForAsync(() => changes.Unstaged.Rows.Any(row => row.Key == "notes.txt"), "notes.txt unstaged");
        var fetch = app.FindByAutomationId("FetchButton");

        await PressByTheKeysAsync(app, fetch, Key.Enter);
        await hold.WaitForHeldAsync(1);
        await FocusByTheKeysAsync(app, StageRowButton(app, "UnstagedFileList", "notes.txt"));
        await app.TabToAsync(focused => focused is Control control && AutomationProperties.GetAutomationId(control) == "RowStageButton", "notes.txt's Stage");
        Assert.True(app.FocusVisible, "notes.txt's Stage shows no keyboard frame.");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => changes.StageFileCommand.IsRunning, "the Stage waiting behind the fetch");
        hold.Release();
        await app.WaitForAsync(() => !changes.HasUnstaged && !changes.StageFileCommand.IsRunning, "notes.txt staged", TimeSpan.FromSeconds(60));
        await app.WaitForIdleAsync(repository, TimeSpan.FromSeconds(60));

        await AssertFocusAsync(app, RowFact("notes.txt", "StagedFileList"), "Enter on notes.txt's Stage during a keyboard Fetch");
    }

    // ---- A text box given the focus back ---------------------------------------------------------

    /// <summary>
    /// The Remotes dialog's form, a remote "mirror" to add, git's configuration locked outside the
    /// app: the URL box, entered by the keys, its caret moved to the third character; Enter saves, git
    /// refuses, and the box has the focus back, with the frame, its caret where it was and nothing
    /// selected.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_in_the_remote_forms_box_that_git_refuses_keeps_the_boxs_caret_and_selects_nothing()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var dialog = await OpenRemotesAsync(app);
        app.Click(app.FindByAutomationId("AddRemoteButton"));
        await app.WaitForAsync(() => dialog.IsFormShown, "the form");
        dialog.RemoteName = "mirror";
        dialog.RemoteUrl = repo.SiblingPath("origin.git");
        app.Settle();
        var box = (TextBox)app.FindByAutomationId(app.TopDialogView!, "RemoteUrlBox");
        var locked = Path.Combine(repo.Path, ".git", "config.lock");
        File.WriteAllText(locked, string.Empty);
        try
        {
            await FocusByTheKeysAsync(app, box);
            MoveCaretToTheThirdCharacter(app, box);

            app.PressKey(Key.Enter);
            await app.WaitForAsync(() => !dialog.IsSaving && !string.IsNullOrEmpty(dialog.RemoteError), "git's refusal");
            await app.WaitForIdleAsync();
        }
        finally
        {
            File.Delete(locked);
        }

        Assert.True(dialog.IsFormShown, "The form closed.");
        await AssertFocusAsync(app, Framed(box, KeyRegionRole.None), "Enter in the URL box, its caret moved, refused");
        Assert.Equal((3, 3, 3), (box.CaretIndex, box.SelectionStart, box.SelectionEnd));
    }

    /// <summary>
    /// "hello" signed in, the create form for main into origin/topic/no-pr, the fake refusing the
    /// next create: the title, entered by the keys, its caret moved to the third character; Enter
    /// creates, the create fails and the form stays; the title has the focus back, with the frame,
    /// its caret where it was and nothing selected.
    /// </summary>
    [AvaloniaFact]
    public async Task Enter_in_the_create_forms_title_that_fails_keeps_the_titles_caret_and_selects_nothing()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenCreateFormAsync(data, served);
        var form = (CreatePullRequestDialogViewModel)app.Session.Services.DialogHost.Current!;
        served.GitHub.FailNext("create:octo/hello", VisualCommit.Testing.Hosting.FakeGitHubFault.ServerError);
        var title = (TextBox)app.FindByAutomationId(app.TopDialogView!, "PullRequestTitle");
        await FocusByTheKeysAsync(app, title);
        MoveCaretToTheThirdCharacter(app, title);

        app.PressKey(Key.Enter);
        await AnswerAnyDialogsByClickAsync(app, () => !form.IsCreating && !string.IsNullOrEmpty(form.Error));

        Assert.False(form.IsClosed, "The form closed.");
        await AssertFocusAsync(app, Framed(title, KeyRegionRole.None), "Enter in the title, its caret moved, failed");
        Assert.Equal((3, 3, 3), (title.CaretIndex, title.SelectionStart, title.SelectionEnd));
        form.CancelCommand.Execute(null);
    }

    // ---- A failed Stage lines --------------------------------------------------------------------

    /// <summary>
    /// Two lines of src/Calculator.cs's second hunk chosen by the arrows, git's index locked outside
    /// the app: Stage lines, pressed by the keys, fails, its error in the stage panel and the lines
    /// still chosen; Stage lines has the focus back, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task Stage_lines_pressed_by_the_keys_that_fails_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenCalculatorDiffAsync(app);
        var diff = await ChooseTwoCalculatorLinesAsync(app);
        var stage = app.FindByAutomationId("StageLinesButton");
        var locked = Path.Combine(repo.Path, ".git", "index.lock");
        File.WriteAllText(locked, string.Empty);
        try
        {
            await app.WaitForWatchersQuietAsync();
            Assert.Equal(2, diff.SelectedChanges.Count);
            await PressByTheKeysAsync(app, stage, Key.Enter);
            await app.WaitForAsync(() => !diff.StageLinesCommand.IsRunning && Repository(app).Changes.ErrorText is not null, "Stage lines to fail");
        }
        finally
        {
            File.Delete(locked);
        }

        Assert.True(diff.SelectedChanges.Count == 2 && stage.IsEffectivelyVisible, "The failed Stage lines did not leave its lines and its button.");
        await AssertFocusAsync(app, Framed(stage, KeyRegionRole.GraphArea), "Enter on Stage lines, failed");
    }

    /// <summary>README.md's staged diff, two of its added lines chosen, git's index locked outside the app: Unstage lines, pressed by the keys, fails; Unstage lines has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task Unstage_lines_pressed_by_the_keys_that_fails_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenStagedDiffAsync(app, Phase8aExpectations.K.Readme);
        var diff = Repository(app).Diff!;
        app.PressKey(Key.Home, RawInputModifiers.Control);
        await ChooseChangedLinesAsync(app, diff, 2);
        var unstage = app.FindByAutomationId("UnstageLinesButton");
        var locked = Path.Combine(repo.Path, ".git", "index.lock");
        File.WriteAllText(locked, string.Empty);
        try
        {
            await app.WaitForWatchersQuietAsync();
            Assert.Equal(2, diff.SelectedChanges.Count);
            await PressByTheKeysAsync(app, unstage, Key.Enter);
            await app.WaitForAsync(() => !diff.UnstageLinesCommand.IsRunning && Repository(app).Changes.ErrorText is not null, "Unstage lines to fail");
        }
        finally
        {
            File.Delete(locked);
        }

        Assert.True(diff.SelectedChanges.Count == 2 && unstage.IsEffectivelyVisible, "The failed Unstage lines did not leave its lines and its button.");
        await AssertFocusAsync(app, Framed(unstage, KeyRegionRole.GraphArea), "Enter on Unstage lines, failed");
    }

    // ---- A mouse action after a menu of the keys -------------------------------------------------

    /// <summary>
    /// "ops" with a-sweep: its row reached by Tab, its menu opened by Shift+F10 and "Rename a-sweep…"
    /// chosen by the keys, cancelled by Esc (D104 gives the row back without the frame); then its menu
    /// opened by a right-click, "Delete a-sweep…" clicked and confirmed by a click. An action by the
    /// mouse alone arms nothing: once the row has left, no element shows the keyboard's frame.
    /// </summary>
    [AvaloniaFact]
    public async Task A_branch_deleted_by_the_mouse_after_its_menu_by_the_keys_was_cancelled_leaves_no_frame()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("branch", "a-sweep");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var rows = Repository(app).LeftPanel.Rows;
        app.Click(app.GraphRowPoint(0));
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        await FocusLeftSectionAsync(app);
        for (var tab = 0; app.FocusedElement is not Button { DataContext: RefItemRow { Key: "ref:refs/heads/a-sweep" } }; tab++)
        {
            Assert.True(tab < 10, $"Ten Tabs did not reach a-sweep's row: the focus is on {app.DescribeFocus()}.");
            app.PressKey(Key.Tab);
        }

        var row = (Button)app.FocusedElement!;
        Assert.True(app.FocusVisible, "a-sweep's row shows no keyboard frame.");
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        await ChooseByTheKeysAsync(app, "Rename a-sweep…");
        await CancelByEscAsync(app);
        await app.WaitForAsync(() => ReferenceEquals(app.FocusedElement, row) && !app.FocusVisible, "a-sweep's row given back without the frame (D104)", FocusWait);

        app.RightClick(await app.RefRowAsync("Local branches", "a-sweep"));
        app.ClickMenuItem("Delete a-sweep…");
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => rows.All(each => each.Key != "ref:refs/heads/a-sweep"), "a-sweep's row to go");
        await app.WaitForIdleAsync();
        app.Settle();

        Assert.False(app.FocusVisible, $"After a delete by the mouse alone the focus is on {FocusFact(app)}, with the keyboard's frame.");
    }

    // ---- Closed tabs let go ----------------------------------------------------------------------

    /// <summary>
    /// Two "changes" tabs, the first active: its Stage all, pressed by the keys (or clicked, the
    /// control), stages every file; the first tab is then closed. Its repository's view model is let
    /// go: the window's rule holds no closed tab (docs/phase-6-design.md 8.11).
    /// </summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_tab_closed_after_Stage_all_pressed_on_it_is_let_go(bool byTheKeys)
    {
        using var first = await Scenarios.ChangesAsync();
        using var second = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenTabsAsync(data, [first.Path, second.Path], activeTab: 0);
        await StageAllOnTheActiveTabAsync(app, byTheKeys);

        var closed = CloseFirstTab(app.Session.Shell);

        await AssertLetGoAsync(app, closed);
    }

    /// <summary>
    /// Two "changes" tabs, the first active, its graph focused by the keys (or by a click, the
    /// control): Ctrl+W closes the first tab, the graph keeps the focus showing the second's, and the
    /// first tab's repository's view model is let go.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_tab_closed_by_Ctrl_W_from_the_graph_is_let_go(bool framed)
    {
        using var first = await Scenarios.ChangesAsync();
        using var second = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenTabsAsync(data, [first.Path, second.Path], activeTab: 0);
        var graph = app.FindByAutomationId("GraphRows");
        if (framed)
        {
            await FocusByTheKeysAsync(app, graph);
        }
        else
        {
            app.Click(app.GraphRowPoint(0));
            await app.WaitForIdleAsync();
            app.MoveToNeutral();
            Assert.True(ReferenceEquals(app.FocusedElement, graph) && !app.FocusVisible, $"The click left the focus on {FocusFact(app)}.");
        }

        var closed = FirstTabsRepository(app.Session.Shell);
        app.PressKey(Key.W, RawInputModifiers.Control);
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 1, "the first tab to close");
        app.Settle();
        Assert.True(ReferenceEquals(app.FocusedElement, graph), $"After Ctrl+W the focus is on {FocusFact(app)}, not the graph.");

        await AssertLetGoAsync(app, closed);
    }

    /// <summary>
    /// Two "changes" tabs, the first active, its graph focused by the keys: its menu opened by
    /// Shift+F10 and closed by Esc (which follows the graph as a menu's opener); the first tab is
    /// then closed by its command, the graph keeping the focus. The first tab's repository's view
    /// model is let go.
    /// </summary>
    [AvaloniaFact]
    public async Task A_tab_closed_after_a_menu_the_keys_opened_in_its_graph_is_let_go()
    {
        using var first = await Scenarios.ChangesAsync();
        using var second = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenTabsAsync(data, [first.Path, second.Path], activeTab: 0);
        var graph = app.FindByAutomationId("GraphRows");
        app.Click(app.GraphRowPoint(0));
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        await FocusByTheKeysAsync(app, graph);
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.IsMenuOpen, "the graph's menu");
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsMenuOpen && ReferenceEquals(app.FocusedElement, graph), "the menu to close, the graph focused");
        app.Settle();

        var closed = CloseFirstTab(app.Session.Shell);
        await app.WaitForAsync(() => app.Session.Shell.Tabs.Count == 1, "the first tab to close");
        app.Settle();
        Assert.True(ReferenceEquals(app.FocusedElement, graph), $"After the close the focus is on {FocusFact(app)}, not the graph.");

        await AssertLetGoAsync(app, closed);
    }

    // ---- A key repeated --------------------------------------------------------------------------

    /// <summary>
    /// "graph": the graph focused with the frame, as F6 gives it; Refresh by F5 twenty times (a key
    /// held or repeated) leaves the graph focused and arms one follower of its next loss, not one per
    /// press.
    /// </summary>
    [AvaloniaFact]
    public async Task Refresh_by_its_key_twenty_times_on_the_framed_graph_arms_one_follower_of_the_graph()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var repository = Repository(app);
        var graph = app.FindByAutomationId("GraphRows");
        await FocusByTheKeysAsync(app, graph);

        for (var press = 0; press < 20; press++)
        {
            app.PressKey(Key.F5);
            await app.WaitForIdleAsync(repository);
        }

        app.Settle();
        Assert.True(ReferenceEquals(app.FocusedElement, graph) && app.FocusVisible, $"After F5 the focus is on {FocusFact(app)}, not the framed graph.");
        Assert.Equal(1, FollowersOfLoss(graph));
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    /// <summary>
    /// The headless window's platform callback <paramref name="name"/> (Activated, Deactivated,
    /// LostFocus), read by reflection as <c>MainWindowBringForwardTests</c> reads Deactivated:
    /// Avalonia does not let code outside it reach the callbacks.
    /// </summary>
    private static Action PlatformCallback(Window window, string name) =>
        window.PlatformImpl is { } impl && impl.GetType().FullName == "Avalonia.Headless.HeadlessWindowImpl"
            && impl.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(impl) is Action callback
            ? callback
            : throw new InvalidOperationException($"The window has no public {name} callback: not a headless window, or Avalonia.Headless changed.");

    /// <summary>The Stage (or Unstage) button of <paramref name="path"/>'s row in the stage panel's list <paramref name="list"/>.</summary>
    private static Button StageButtonOf(ShellDriver app, string list, string path) =>
        app.Window.GetVisualDescendants().OfType<Button>().First(button =>
            AutomationProperties.GetAutomationId(button) is "RowStageButton" or "RowUnstageButton" && button.IsEffectivelyVisible
            && ShellDriver.StageRowOf(button)?.DataContext is ChangedFileRow { Key: var key } && key == path
            && button.GetVisualAncestors().OfType<ItemsControl>().Any(items => items.Name == list));

    /// <summary>In <paramref name="box"/>, which has the focus: Home, then Right three times; the caret after the third character, nothing selected.</summary>
    private static void MoveCaretToTheThirdCharacter(ShellDriver app, TextBox box)
    {
        app.PressKey(Key.Home);
        app.PressKey(Key.Right);
        app.PressKey(Key.Right);
        app.PressKey(Key.Right);
        app.Settle();
        Assert.True(box.Text?.Length > 3, $"The box holds too little to move in: \"{box.Text}\".");
        Assert.True(ReferenceEquals(app.FocusedElement, box) && app.FocusVisible, $"The box lost the focus or its frame: {FocusFact(app)}.");
        Assert.Equal((3, 3, 3), (box.CaretIndex, box.SelectionStart, box.SelectionEnd));
    }

    /// <summary>The active tab's stage panel shown and its Stage all pressed by the keys (or clicked): every file staged, the app idle.</summary>
    private static async Task StageAllOnTheActiveTabAsync(ShellDriver app, bool byTheKeys)
    {
        await ShowStagePanelAsync(app);
        var changes = Repository(app).Changes;
        var stageAll = app.FindByAutomationId("StageAllButton");
        if (byTheKeys)
        {
            await PressByTheKeysAsync(app, stageAll, Key.Enter);
        }
        else
        {
            app.Click(stageAll);
            app.MoveToNeutral();
        }

        await app.WaitForAsync(() => !changes.HasUnstaged, "every file staged");
        await app.WaitForIdleAsync(Repository(app));
        app.Settle();
    }

    /// <summary>Closes the window's first tab by its command and returns a weak reference to its repository's view model, holding none itself.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CloseFirstTab(MainWindowViewModel shell)
    {
        var tab = shell.Tabs[0];
        var weak = new WeakReference(tab.Repository ?? throw new InvalidOperationException("The first tab has no repository."));
        shell.CloseTabCommand.Execute(tab);
        return weak;
    }

    /// <summary>A weak reference to the first tab's repository's view model, holding none itself.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference FirstTabsRepository(MainWindowViewModel shell) =>
        new(shell.Tabs[0].Repository ?? throw new InvalidOperationException("The first tab has no repository."));

    /// <summary>The tab now active read and idle; then, after up to five collections, the closed tab's repository's view model is gone.</summary>
    private static async Task AssertLetGoAsync(ShellDriver app, WeakReference closed)
    {
        await app.WaitForRepositoryAsync();
        await app.WaitForIdleAsync();
        app.Settle();
        for (var round = 0; round < 5 && closed.IsAlive; round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            app.Settle();
            await Task.Delay(100, Cancelled);
            app.Settle();
        }

        Assert.False(closed.IsAlive, "The closed tab's repository's view model is still held.");
    }

    /// <summary>
    /// The handlers of <paramref name="element"/>'s LostFocus that the window's rule for an action by
    /// the keys added (its private ActionByKeys), read from Avalonia's Interactive by reflection: the
    /// app shows no internals to the tests.
    /// </summary>
    private static int FollowersOfLoss(InputElement element)
    {
        var field = typeof(Interactive).GetField("_eventHandlers", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Avalonia's Interactive keeps its handlers elsewhere now: read them another way.");
        if (field.GetValue(element) is not IDictionary handlers || !handlers.Contains(InputElement.LostFocusEvent))
        {
            return 0;
        }

        return ((IEnumerable)handlers[InputElement.LostFocusEvent]!).Cast<object>()
            .Select(subscription => subscription.GetType().GetProperty("Handler")?.GetValue(subscription) as Delegate)
            .Count(handler => handler?.Target?.GetType().Name == "ActionByKeys");
    }
}
