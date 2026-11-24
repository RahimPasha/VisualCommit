using System.IO;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The sweep of D462 (see <c>KeyboardPassTests.AfterPress.cs</c>) in the graph area's other
/// contents and the left panel: the conflict banner, the conflict resolver, the interactive rebase's
/// list, the file history's and the blame's pages, a branch row and a workspace item removed by an
/// action of their menus opened by the keys.
/// </summary>
public sealed partial class KeyboardPassTests
{
    // ---- The conflict banner ---------------------------------------------------------------------

    /// <summary>A merge stopped on src/core.txt: the banner's Abort, pressed by the keys, its question cancelled by Esc, has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_banners_Abort_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var abort = app.FindByAutomationId("BannerAbortButton");

        await PressByTheKeysAsync(app, abort, Key.Enter);
        await CancelByEscAsync(app);

        await AssertFocusAsync(app, Framed(abort, KeyRegionRole.GraphArea), "Enter on Abort, cancelled");
        Assert.True(Repository(app).Banner.IsVisible, "The banner went.");
    }

    /// <summary>The same Abort confirmed by a click: the merge is aborted and the banner goes with the button; the graph has the focus, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_banners_Abort_pressed_by_the_keys_and_confirmed_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);

        await PressByTheKeysAsync(app, app.FindByAutomationId("BannerAbortButton"), Key.Enter);
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => !Repository(app).Banner.IsVisible, "the banner to go");

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Abort, confirmed");
    }

    /// <summary>A cherry-pick stopped on its second commit: the banner's Skip, pressed by the keys, its question cancelled by Esc, has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_banners_Skip_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var skip = app.FindByAutomationId("BannerSkipButton");

        await PressByTheKeysAsync(app, skip, Key.Enter);
        await CancelByEscAsync(app);

        await AssertFocusAsync(app, Framed(skip, KeyRegionRole.GraphArea), "Enter on Skip, cancelled");
    }

    /// <summary>
    /// A merge stopped on src/core.txt, resolved and staged outside the app: the banner's Continue,
    /// pressed by the keys, ends the merge, and the banner goes with the button; the graph has the
    /// focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_banners_Continue_pressed_by_the_keys_that_ends_the_merge_gives_the_focus_to_the_graph_with_the_frame()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        repo.WriteFile("src/core.txt", "resolved\n");
        await repo.GitAsync("add", "src/core.txt");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var cont = app.FindByAutomationId("BannerContinueButton");

        await PressByTheKeysAsync(app, cont, Key.Enter);
        await AnswerAnyDialogsByClickAsync(app, () => !Repository(app).Banner.IsVisible);

        await AssertFocusAsync(app, GraphFramedFact, "Enter on Continue");
    }

    // ---- The conflict resolver -------------------------------------------------------------------

    /// <summary>
    /// notes.txt in the resolver, its result changed: Close, pressed by the keys, asks first; Esc
    /// keeps editing, and Close has the focus back, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_resolvers_Close_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenResolverOnAsync(app, "notes.txt");
        await ChangeTheResultAsync(app);
        var close = app.FindByAutomationId("ResolverCloseButton");

        await PressByTheKeysAsync(app, close, Key.Enter);
        await CancelByEscAsync(app);

        await AssertFocusAsync(app, Framed(close, KeyRegionRole.GraphArea), "Enter on the resolver's Close, cancelled");
    }

    /// <summary>notes.txt in the resolver, its markers still in the result: Mark resolved, pressed by the keys, asks about them; Esc, and Mark resolved has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task Mark_resolved_pressed_by_the_keys_and_cancelled_in_its_markers_question_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenResolverOnAsync(app, "notes.txt");
        var mark = app.FindByAutomationId("ResolverMarkResolved");

        await PressByTheKeysAsync(app, mark, Key.Enter);
        await CancelByEscAsync(app);

        await AssertFocusAsync(app, Framed(mark, KeyRegionRole.GraphArea), "Enter on Mark resolved, cancelled");
    }

    /// <summary>
    /// The merge of three kinds, new.txt (added by both, the second of three) in the resolver, its
    /// result changed: Previous file or Next file, pressed by the keys, asks first; Esc keeps editing,
    /// and the button has the focus back, with the frame.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("ResolverPreviousFile")]
    [InlineData("ResolverNextFile")]
    public async Task The_resolvers_Previous_or_Next_file_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame(string id)
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.MergeKinds);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenResolverOnAsync(app, "new.txt");
        await ChangeTheResultAsync(app);
        var button = app.FindByAutomationId(id);

        await PressByTheKeysAsync(app, button, Key.Enter);
        await CancelByEscAsync(app);

        await AssertFocusAsync(app, Framed(button, KeyRegionRole.GraphArea), $"Enter on {id}, cancelled");
    }

    /// <summary>notes.txt in the resolver with a merge tool set up, its result changed: Merge tool, pressed by the keys, asks first; Esc keeps editing, and Merge tool has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_resolvers_Merge_tool_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge, mergeTool: true);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenResolverOnAsync(app, "notes.txt");
        await ChangeTheResultAsync(app);
        var tool = app.FindByAutomationId("ResolverMergeTool");

        await PressByTheKeysAsync(app, tool, Key.Enter);
        await CancelByEscAsync(app);

        await AssertFocusAsync(app, Framed(tool, KeyRegionRole.GraphArea), "Enter on Merge tool, cancelled");
    }

    /// <summary>
    /// The merge of three kinds, other.txt (deleted by them) in the resolver, git's index locked
    /// outside the app: the card's Keep the file, pressed by the keys, fails; once its error is
    /// closed, Keep the file has the focus back, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task A_cards_button_pressed_by_the_keys_that_fails_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.MergeKinds);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenResolverOnAsync(app, "other.txt");
        var keep = app.FindByAutomationId("ConflictKeepFile");
        File.WriteAllText(Path.Combine(repo.Path, ".git", "index.lock"), string.Empty);

        await PressByTheKeysAsync(app, keep, Key.Enter);
        await AnswerAnyDialogsByClickAsync(app, () => !app.ResolverModel.IsBusy && app.Window.GetVisualDescendants().OfType<Control>().Any(control => ReferenceEquals(control, keep) && control.IsEffectivelyEnabled));
        File.Delete(Path.Combine(repo.Path, ".git", "index.lock"));

        await AssertFocusAsync(app, Framed(keep, KeyRegionRole.GraphArea), "Enter on Keep the file, failed");
    }

    /// <summary>notes.txt in the resolver: Take ours, pressed by the keys, gives the result's text the focus, where the side was put (a guard: this held before).</summary>
    [AvaloniaFact]
    public async Task Take_ours_pressed_by_the_keys_gives_the_focus_to_the_result()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenResolverOnAsync(app, "notes.txt");
        app.FindByAutomationId("ResolverMarkResolved").Focus(NavigationMethod.Tab);

        await PressByTheKeysAsync(app, app.FindByAutomationId("ResolverTakeOurs"), Key.Enter);
        await app.WaitForAsync(() => !app.ResolverModel.IsBusy, "Take ours to end");

        await app.WaitForAsync(() => app.FocusedAutomationId() == "ConflictResult", $"the focus in the result (it is on {app.DescribeFocus()})", FocusWait);
    }

    // ---- The interactive rebase ------------------------------------------------------------------

    /// <summary>
    /// The interactive rebase of main from Base, its second row moved down by Alt+Down, git's index
    /// locked outside the app: Start, pressed by the keys, is refused by git before anything starts;
    /// once its error is closed, Start has the focus back, with the frame, and the list stays.
    /// </summary>
    [AvaloniaFact]
    public async Task Start_rebase_pressed_by_the_keys_that_git_refuses_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.RightClick(app.GraphRowPoint(6));
        app.ClickMenuItem("Interactive rebase of main from here…");
        await app.WaitForRebaseListAsync();
        app.ClickRebaseRow(app.RebaseListSnapshot().Rows[1].Subject);
        app.PressKey(Key.Down, RawInputModifiers.Alt);
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        var start = app.FindByAutomationId("RebaseStartButton");
        var locked = Path.Combine(repo.Path, ".git", "index.lock");
        File.WriteAllText(locked, string.Empty);

        await PressByTheKeysAsync(app, start, Key.Enter);
        await AnswerAnyDialogsByClickAsync(app, () => Repository(app).InteractiveRebase is { IsBusy: false } && !app.Session.Shell.Dialogs.IsOpen);
        File.Delete(locked);

        Assert.True(Repository(app).ShowsInteractiveRebase, "The list closed.");
        await AssertFocusAsync(app, Framed(start, KeyRegionRole.GraphArea), "Enter on Start, refused");
    }

    // ---- The pages -------------------------------------------------------------------------------

    /// <summary>
    /// The file history of data/blob.bin, a binary file: its Blame, pressed by the keys, opens the
    /// blame in the page's place, which shows a message and no text; the page's first stop has the
    /// focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_file_historys_Blame_pressed_by_the_keys_on_a_binary_file_leaves_the_focus_on_the_page_with_the_frame()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenFileHistoryAsync(app, "data/blob.bin");

        await PressByTheKeysAsync(app, app.FindByAutomationId("FileHistoryBlame"), Key.Enter);
        await app.WaitForAsync(() => Repository(app).Page is BlameViewModel { ShowsLoading: false }, "the blame's message");

        await AssertFocusInPageAsync(app, "Enter on the file history's Blame");
    }

    /// <summary>The file history of src/Calculator.cs: its Blame, pressed by the keys, opens the blame, whose text takes the focus (a guard: this held before).</summary>
    [AvaloniaFact]
    public async Task The_file_historys_Blame_pressed_by_the_keys_gives_the_focus_to_the_blames_text()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenFileHistoryAsync(app, Phase8aExpectations.K.Calculator);

        await PressByTheKeysAsync(app, app.FindByAutomationId("FileHistoryBlame"), Key.Enter);
        await app.WaitForAsync(() => Repository(app).Page is BlameViewModel { ShowsText: true }, "the blame's text");

        await app.WaitForAsync(() => app.FocusedAutomationId() == "BlameText", $"the focus in the blame's text (it is on {app.DescribeFocus()})", FocusWait);
    }

    /// <summary>src/Reader.cs's blame: its History, pressed by the keys, opens the file history, whose list takes the focus (a guard: this held before).</summary>
    [AvaloniaFact]
    public async Task The_blames_History_pressed_by_the_keys_gives_the_focus_to_the_file_historys_list()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenReaderBlameAsync(app);

        await PressByTheKeysAsync(app, app.FindByAutomationId("BlameHistory"), Key.Enter);
        await app.WaitForAsync(() => Repository(app).Page is FileHistoryViewModel, "the file history");

        await app.WaitForAsync(() => app.FocusIsWithin(app.FindByAutomationId("FileHistoryList")), $"the focus in the file history's list (it is on {app.DescribeFocus()})", FocusWait);
    }

    /// <summary>
    /// The file history of data/large.txt, its first commit's diff too large to show at once: Show
    /// the diff, pressed by the keys, shows it and hides itself with its message; the diff's text,
    /// in its place, has the focus, without the frame, as the text has it everywhere.
    /// </summary>
    [AvaloniaFact]
    public async Task Show_the_diff_in_the_file_history_pressed_by_the_keys_gives_the_focus_to_the_diffs_text()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenFileHistoryAsync(app, "data/large.txt");
        var history = (FileHistoryViewModel)Repository(app).Page!;
        await app.WaitForAsync(() => history.Diff is { ShowsVeryLarge: true }, "the diff's message");
        var show = app.Window.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "ShowLargeDiffButton" && button.IsEffectivelyVisible);

        await PressByTheKeysAsync(app, show, Key.Enter);
        await app.WaitForAsync(() => history.Diff is { ShowsText: true }, "the diff's text");

        await app.WaitForAsync(() => app.FocusedAutomationId() == "DiffText" && !app.FocusVisible, $"the focus in the diff's text, without the frame (it is on {FocusOf(app)})", FocusWait);
    }

    // ---- Rows removed by their menus' actions ----------------------------------------------------

    /// <summary>
    /// "ops" with a branch a-sweep made outside the app, first under Local branches: its row, reached
    /// by Tab, its menu opened by Shift+F10 and "Delete a-sweep…" chosen by the keys, confirmed by a
    /// click; once the row has left, the row now at its place has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task A_branch_deleted_from_its_menu_opened_by_the_keys_gives_the_focus_to_the_row_now_at_its_place()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("branch", "a-sweep");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var rows = Repository(app).LeftPanel.Rows;
        var index = rows.ToList().FindIndex(row => row.Key == "ref:refs/heads/a-sweep");
        Assert.True(index > 0, "a-sweep has no row.");
        app.Click(app.GraphRowPoint(0));
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        await FocusLeftSectionAsync(app);
        for (var tab = 0; app.FocusedElement is not Button { DataContext: RefItemRow { Key: "ref:refs/heads/a-sweep" } }; tab++)
        {
            Assert.True(tab < 10, $"Ten Tabs did not reach a-sweep's row: the focus is on {app.DescribeFocus()}.");
            app.PressKey(Key.Tab);
        }

        Assert.True(app.FocusVisible, "a-sweep's row shows no keyboard frame.");

        app.PressKey(Key.F10, RawInputModifiers.Shift);
        await ChooseByTheKeysAsync(app, "Delete a-sweep…");
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => rows.All(row => row.Key != "ref:refs/heads/a-sweep"), "a-sweep's row to go");
        await app.WaitForIdleAsync();

        var now = (RefItemRow)rows[index];
        await AssertFocusAsync(app, $"Button RefItem \"{now.Text}\" [Left], framed", "a-sweep deleted from its menu");
    }

    /// <summary>
    /// Two workspaces on the welcome page: the first one's item, reached by Tab, its menu opened by
    /// Shift+F10 and "Delete workspace…" chosen by the keys, confirmed by a click; once the item has
    /// left, the item now at its place, the other workspace's, has the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task A_workspace_deleted_from_its_menu_opened_by_the_keys_gives_the_focus_to_the_item_now_at_its_place()
    {
        using var data = new TempDirectory("data");
        var list = new WorkspaceList { Workspaces = [new Workspace("first", "First", []), new Workspace("second", "Second", [])] };
        using var app = ShellDriver.StartWithWorkspace(data.Path, list);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        var welcome = app.Session.Shell.ActiveTab.Welcome;
        await app.WaitForAsync(() => welcome.Workspaces.Count == 2, "the two workspaces");
        var first = app.Window.GetVisualDescendants().OfType<Button>().First(button => AutomationProperties.GetAutomationId(button) == "WorkspaceItem" && AutomationProperties.GetName(button) == "First");
        Assert.True(first.Focus(NavigationMethod.Tab));
        app.Settle();

        app.PressKey(Key.F10, RawInputModifiers.Shift);
        await ChooseByTheKeysAsync(app, "Delete workspace…");
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => welcome.Workspaces.Count == 1, "the first workspace to go");

        var second = app.Window.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "WorkspaceItem" && button.IsEffectivelyVisible);
        await AssertFocusAsync(app, Framed(second, KeyRegionRole.GraphArea), "the first workspace deleted from its menu");
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    /// <summary>The question open now cancelled by Esc, closed, and the app idle.</summary>
    private static async Task CancelByEscAsync(ShellDriver app)
    {
        var question = await app.WaitForDialogAsync<DialogViewModel>();
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => question.IsClosed, "the question to close by Esc");
        if (app.Session.Shell.ActiveTab.Repository is { } repository)
        {
            await app.WaitForIdleAsync(repository);
        }
    }

    /// <summary>
    /// Until <paramref name="done"/>: every dialog that opens meanwhile (a message to confirm, an
    /// error to close) is answered by a click on its confirm button. Then the app is idle.
    /// </summary>
    private static async Task AnswerAnyDialogsByClickAsync(ShellDriver app, Func<bool> done)
    {
        await app.WaitForAsync(
            () =>
            {
                if (done())
                {
                    return true;
                }

                if (app.Session.Shell.Dialogs.IsOpen && app.TopDialogView is { } view
                    && view.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "ConfirmButton" && button.IsEffectivelyVisible && button.IsEffectivelyEnabled) is { } confirm)
                {
                    app.Click(confirm);
                }

                return false;
            },
            "the press's run to end, its dialogs answered",
            TimeSpan.FromSeconds(60));
        if (app.Session.Shell.ActiveTab.Repository is { } repository)
        {
            await app.WaitForIdleAsync(repository);
        }

        app.MoveToNeutral();
    }

    /// <summary>The working-changes row clicked and <paramref name="path"/>'s row in the unstaged list clicked: the resolver shows it.</summary>
    private static async Task OpenResolverOnAsync(ShellDriver app, string path)
    {
        await ShowStagePanelAsync(app);
        app.Click(await app.FileRowAsync("UnstagedFileList", path));
        await app.WaitForResolverAsync(Path.GetFileName(path));
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
    }

    /// <summary>A line typed at the start of the resolver's result, which has the focus.</summary>
    private static async Task ChangeTheResultAsync(ShellDriver app)
    {
        await app.WaitForResultParsedAsync();
        app.ResultEditor.TextArea.Focus(NavigationMethod.Unspecified);
        app.ResultEditor.TextArea.Caret.Offset = 0;
        app.Type("changed\n");
        await app.WaitForAsync(() => app.ResolverModel.IsModified, "the result changed");
    }

    /// <summary>The menu open now: Down until <paramref name="label"/> is highlighted, then Enter; the menu closes.</summary>
    private static async Task ChooseByTheKeysAsync(ShellDriver app, string label)
    {
        var menu = app.Menu();
        for (var down = 0; app.Menu().Highlighted?.Label != label; down++)
        {
            Assert.True(down < 20, $"Twenty Down did not highlight {label} in {menu.Header}'s menu.");
            app.PressKey(Key.Down);
        }

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsMenuOpen, "the menu to close");
    }

    /// <summary><paramref name="path"/>'s file history opened by its row's menu in the details of HEAD's commit, its list shown.</summary>
    private static async Task OpenFileHistoryAsync(ShellDriver app, string path)
    {
        app.Click(app.GraphRowPoint(0));
        app.WaitForCommitDetails();
        await app.WaitForIdleAsync();
        app.RightClick(await app.FileRowAsync("FileList", path));
        app.ClickMenuItem("File history");
        await app.WaitForAsync(() => Repository(app).Page is FileHistoryViewModel { ShowsLoading: false } history && history.Rows.Count > 0, $"{path}'s file history");
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
    }

    /// <summary>Waits, a short while, for the focus to lie in the graph area's page with the keyboard's frame, and asserts it.</summary>
    private static async Task AssertFocusInPageAsync(ShellDriver app, string after)
    {
        var page = app.Find<ContentControl>("PageSlot");
        try
        {
            await app.WaitForAsync(() => app.FocusIsWithin(page) && app.FocusVisible, "the focus in the page, with the frame", FocusWait);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"After {after} the focus is on {FocusOf(app)}, not in the page with the frame.");
        }

        await app.WaitForIdleAsync();
        app.Settle();
        Assert.True(app.FocusIsWithin(page) && app.FocusVisible, $"After {after}, once the app was idle, the focus is on {FocusOf(app)}.");
    }
}
