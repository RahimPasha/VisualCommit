using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.Views;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The sweep of D462 (see <c>KeyboardPassTests.AfterPress.cs</c>): the guards, controls pressed by
/// the keys whose press already left the focus somewhere before the sweep (each passes before it
/// too): the graph area's closers (a page's, the search bar's, the comparison's, the rebase list's
/// Cancel), the activity log's Close, the toolbar's Search, the welcome page's Clone and a workspace's
/// item, the Remotes dialog's Add, Edit and form's Cancel, and the sign-in dialog's Sign in refused.
/// Each asserts that the focus is on the element the app has given it since before; whether with the
/// frame is the app's way there, which the sweep does not change.
/// </summary>
public sealed partial class KeyboardPassTests
{
    /// <summary>A file history's Close, pressed by the keys, closes the page; the graph has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_the_file_historys_Close_pressed_by_the_keys_gives_the_focus_to_the_graph()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenFileHistoryAsync(app, Phase8aExpectations.K.Calculator);

        await PressByTheKeysAsync(app, app.FindByAutomationId("FileHistoryClose"), Key.Enter);
        await app.WaitForAsync(() => Repository(app).Page is null, "the page to close");

        await AssertFocusOnAsync(app, "GraphRows", "Enter on the file history's Close");
    }

    /// <summary>A blame's Close, pressed by the keys, closes the page; the graph has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_the_blames_Close_pressed_by_the_keys_gives_the_focus_to_the_graph()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        await OpenReaderBlameAsync(app);

        await PressByTheKeysAsync(app, app.FindByAutomationId("BlameClose"), Key.Enter);
        await app.WaitForAsync(() => Repository(app).Page is null && Repository(app).Diff is null, "the page to close");

        await AssertFocusOnAsync(app, "GraphRows", "Enter on the blame's Close");
    }

    /// <summary>HEAD's reflog's Close, pressed by the keys, closes the page; the graph has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_the_reflogs_Close_pressed_by_the_keys_gives_the_focus_to_the_graph()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.RightClick(await app.RefRowAsync("Local branches"));
        app.Menu();
        app.ClickMenuItem("Show reflog");
        await app.WaitForAsync(() => Repository(app).Page is ReflogViewModel && app.ReflogSnapshot().ListHasFocus, "the reflog with the focus in its list");
        await app.WaitForIdleAsync();

        await PressByTheKeysAsync(app, app.FindByAutomationId("ReflogClose"), Key.Enter);
        await app.WaitForAsync(() => Repository(app).Page is null, "the page to close");

        await AssertFocusOnAsync(app, "GraphRows", "Enter on the reflog's Close");
    }

    /// <summary>The search bar's Close, pressed by the keys, closes the bar; the graph has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_the_search_bars_Close_pressed_by_the_keys_gives_the_focus_to_the_graph()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.Click(app.GraphRowPoint(0));
        app.PressKey(Key.F, RawInputModifiers.Control);
        await app.WaitForSearchAsync();

        await PressByTheKeysAsync(app, app.FindByAutomationId("SearchClose"), Key.Enter);
        await app.WaitForAsync(() => Repository(app).Search is null, "the search bar to close");

        await AssertFocusOnAsync(app, "GraphRows", "Enter on the search bar's Close");
    }

    /// <summary>The comparison of feature/conflict with main: its Close, pressed by the keys, closes it; the graph has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_the_comparisons_Close_pressed_by_the_keys_gives_the_focus_to_the_graph()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.Click(await app.RefRowAsync(Phase8aExpectations.U.LocalBranches, Phase8aExpectations.U.Feature));
        app.RightClick(await app.RefRowAsync(Phase8aExpectations.U.LocalBranches, Phase8aExpectations.U.Feature, Phase8aExpectations.U.Conflict));
        app.ClickMenuItem(Phase8aExpectations.U.CompareConflictWithMain);
        await app.WaitForCompareAsync();

        await PressByTheKeysAsync(app, app.FindByAutomationId("CompareClose"), Key.Enter);
        await app.WaitForAsync(() => Repository(app).Comparison is null, "the comparison to close");

        await AssertFocusOnAsync(app, "GraphRows", "Enter on the comparison's Close");
    }

    /// <summary>The interactive rebase's list, nothing changed: its Cancel, pressed by the keys, closes it; the graph has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_the_rebase_lists_Cancel_pressed_by_the_keys_gives_the_focus_to_the_graph()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.RightClick(app.GraphRowPoint(6));
        app.ClickMenuItem("Interactive rebase of main from here…");
        await app.WaitForRebaseListAsync();

        await PressByTheKeysAsync(app, app.FindByAutomationId("RebaseCancelButton"), Key.Enter);
        await app.WaitForAsync(() => !Repository(app).ShowsInteractiveRebase, "the list to close");

        await AssertFocusOnAsync(app, "GraphRows", "Enter on the rebase list's Cancel");
    }

    /// <summary>The activity log's Close, pressed by the keys, closes it; the graph has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_the_activity_logs_Close_pressed_by_the_keys_gives_the_focus_to_the_graph()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        app.ClickActivityLogToggle();
        await app.WaitForAsync(() => app.Session.Shell.ActivityLog.IsShown, "the activity log");

        await PressByTheKeysAsync(app, app.FindByAutomationId("ActivityLogClose"), Key.Enter);
        await app.WaitForAsync(() => !app.Session.Shell.ActivityLog.IsShown, "the activity log to close");

        await AssertFocusOnAsync(app, "GraphRows", "Enter on the activity log's Close");
    }

    /// <summary>The toolbar's Search, pressed by the keys, opens the search bar, whose box has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_the_toolbars_Search_pressed_by_the_keys_gives_the_focus_to_the_search_box()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);

        await PressByTheKeysAsync(app, app.FindByAutomationId("SearchButton"), Key.Enter);
        await app.WaitForSearchAsync();

        await AssertFocusOnAsync(app, "SearchBox", "Enter on Search");
    }

    /// <summary>The welcome page's Clone, pressed by the keys, opens the clone form, whose URL box has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_the_welcome_pages_Clone_pressed_by_the_keys_gives_the_focus_to_the_URL_box()
    {
        using var data = new TempDirectory("data");
        using var app = await StartEmptyAsync(data);

        await PressByTheKeysAsync(app, app.FindByAutomationId("CloneRepoButton"), Key.Enter);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Welcome.IsCloneFormOpen, "the clone form");

        await AssertFocusOnAsync(app, "CloneUrlBox", "Enter on Clone");
    }

    /// <summary>A workspace's item on the welcome page, pressed by the keys, opens its dashboard, whose empty state has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_a_workspaces_item_pressed_by_the_keys_gives_the_focus_to_its_dashboard()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithWorkspace(data.Path, new WorkspaceList { Workspaces = [new Workspace("first", "First", [])] });
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        var item = app.Window.GetVisualDescendants().OfType<Button>().First(button => AutomationProperties.GetAutomationId(button) == "WorkspaceItem" && button.IsEffectivelyVisible);

        await PressByTheKeysAsync(app, item, Key.Enter);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.ShowsDashboard, "the workspace's dashboard");

        await AssertFocusOnAsync(app, "DashboardEmptyAddRepository", "Enter on the workspace's item");
    }

    /// <summary>The Remotes dialog: Add remote…, pressed by the keys, shows the form, whose name box has the focus; the form's Cancel, pressed by the keys, shows the list again, the dialog's Close having the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_the_remotes_Add_and_the_forms_Cancel_pressed_by_the_keys_leave_the_focus_in_the_dialog()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var dialog = await OpenRemotesAsync(app);

        await PressByTheKeysAsync(app, app.FindByAutomationId(app.TopDialogView!, "AddRemoteButton"), Key.Enter);
        await app.WaitForAsync(() => dialog.IsFormShown, "the form");
        await AssertFocusOnAsync(app, "RemoteNameBox", "Enter on Add remote…");

        await PressByTheKeysAsync(app, app.FindByAutomationId(app.TopDialogView!, "CancelButton"), Key.Enter);
        await app.WaitForAsync(() => !dialog.IsFormShown, "the list again");
        await AssertFocusOnAsync(app, "CancelButton", "Enter on the form's Cancel");
        Assert.False(dialog.IsClosed, "The dialog closed.");
    }

    /// <summary>The Remotes dialog: origin's Edit…, pressed by the keys, shows the form, whose name box has the focus.</summary>
    [AvaloniaFact]
    public async Task Guard_a_remotes_Edit_pressed_by_the_keys_gives_the_focus_to_the_forms_name_box()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var dialog = await OpenRemotesAsync(app);

        await PressByTheKeysAsync(app, RemoteButton(app, "origin", "RemoteEditButton"), Key.Enter);
        await app.WaitForAsync(() => dialog.IsFormShown, "the form");

        await AssertFocusOnAsync(app, "RemoteNameBox", "Enter on origin's Edit…");
    }

    /// <summary>The sign-in dialog, a token the fake refuses typed and Sign in pressed by the keys: the token's box has the focus back.</summary>
    [AvaloniaFact]
    public async Task Guard_the_sign_in_dialogs_Sign_in_pressed_by_the_keys_and_refused_gives_the_focus_to_the_token()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        var settings = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(VisualCommit.App.ViewModels.SettingsSection.Accounts);
        await app.WaitForSettingsReadsAsync(settings);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "AccountsSignInButton"));
        var signing = await app.WaitForDialogAsync<SignInDialogViewModel>();
        app.Type("ghp_refused_by_the_fake");

        await PressByTheKeysAsync(app, app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"), Key.Enter);
        await app.WaitForAsync(() => !signing.IsChecking && signing.ErrorText is not null, "the refusal", TimeSpan.FromSeconds(60));

        await AssertFocusOnAsync(app, "SignInToken", "Enter on Sign in, refused");
        signing.CancelCommand.Execute(null);
    }

    /// <summary>Waits, a short while, for the focus to lie in the element whose automation id is <paramref name="automationId"/>, the nearest one above the focused element included.</summary>
    private static async Task AssertFocusOnAsync(ShellDriver app, string automationId, string after)
    {
        try
        {
            await app.WaitForAsync(() => app.FocusedAutomationId() == automationId, $"the focus on {automationId}", FocusWait);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"After {after} the focus is on {FocusOf(app)}, not {automationId}.");
        }
    }
}
