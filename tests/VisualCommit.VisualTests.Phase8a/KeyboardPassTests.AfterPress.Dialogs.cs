using System.IO;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
/// The sweep of D462 (see <c>KeyboardPassTests.AfterPress.cs</c>) in the dialogs and with an
/// account: the Remotes dialog's Remove and Save, the settings screen's Save and its Accounts
/// section's Sign in… and Sign out…, the sign-in dialog's token page link, the left panel's sign-in
/// row in "Pull requests", and the status bar's "Open in browser" after a pull request is created.
/// </summary>
public sealed partial class KeyboardPassTests
{
    // ---- The Remotes dialog ----------------------------------------------------------------------

    /// <summary>
    /// "sync" with a second remote, mirror, listed first: mirror's Remove, pressed by the keys and
    /// confirmed by a click, removes its row; origin's row, now at its place, has the focus on its
    /// first button, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task A_remotes_Remove_pressed_by_the_keys_and_confirmed_gives_the_focus_to_the_row_now_at_its_place()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        await repo.AddBareRemoteAsync("mirror");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var dialog = await OpenRemotesAsync(app);
        await app.WaitForAsync(() => dialog.Rows.Count == 2, "the two remotes");
        Assert.Equal("mirror", dialog.Rows[0].Name);
        var remove = RemoteButton(app, "mirror", "RemoteRemoveButton");

        await PressByTheKeysAsync(app, remove, Key.Enter);
        await ConfirmByClickAsync(app);
        await app.WaitForAsync(() => dialog.Rows.Count == 1, "mirror to go");
        await app.WaitForIdleAsync();

        await AssertFocusAsync(app, Framed(RemoteButton(app, "origin", "RemoteEditButton"), KeyRegionRole.None), "Enter on mirror's Remove, confirmed");
    }

    /// <summary>The Remotes dialog's form, a remote "mirror" to add, git's configuration locked outside the app: Save, pressed by the keys, is refused by git; Save has the focus back, with the frame, and the form stays.</summary>
    [AvaloniaFact]
    public async Task The_remote_forms_Save_pressed_by_the_keys_that_git_refuses_gets_the_focus_back_with_the_frame()
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
        var save = app.FindByAutomationId(app.TopDialogView!, "ConfirmButton");
        var locked = Path.Combine(repo.Path, ".git", "config.lock");
        File.WriteAllText(locked, string.Empty);

        await PressByTheKeysAsync(app, save, Key.Enter);
        await app.WaitForAsync(() => !dialog.IsSaving && !string.IsNullOrEmpty(dialog.RemoteError), "git's refusal");
        await app.WaitForIdleAsync();
        File.Delete(locked);

        Assert.True(dialog.IsFormShown, "The form closed.");
        await AssertFocusAsync(app, Framed(save, KeyRegionRole.None), "Enter on Save, refused");
    }

    /// <summary>The Remotes dialog's form, a remote "mirror" to add, git's configuration locked outside the app: Enter in the URL box, which shows the frame, saves; git refuses, and the box has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task Enter_in_the_remote_forms_box_that_git_refuses_gives_the_focus_back_to_the_box()
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
        var box = app.FindByAutomationId(app.TopDialogView!, "RemoteUrlBox");
        var locked = Path.Combine(repo.Path, ".git", "config.lock");
        File.WriteAllText(locked, string.Empty);

        await PressByTheKeysAsync(app, box, Key.Enter);
        await app.WaitForAsync(() => !dialog.IsSaving && !string.IsNullOrEmpty(dialog.RemoteError), "git's refusal");
        await app.WaitForIdleAsync();
        File.Delete(locked);

        Assert.True(dialog.IsFormShown, "The form closed.");
        await AssertFocusAsync(app, Framed(box, KeyRegionRole.None), "Enter in the URL box, refused");
    }

    // ---- The create form -------------------------------------------------------------------------

    /// <summary>
    /// "hello" signed in, the create form for main into origin/topic/no-pr, the fake refusing the
    /// next create: Create, pressed by the keys, fails and the form stays; Create has the focus back,
    /// with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_create_forms_Create_pressed_by_the_keys_that_fails_gets_the_focus_back_with_the_frame()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenCreateFormAsync(data, served);
        var form = (CreatePullRequestDialogViewModel)app.Session.Services.DialogHost.Current!;
        served.GitHub.FailNext("create:octo/hello", VisualCommit.Testing.Hosting.FakeGitHubFault.ServerError);
        var create = app.FindByAutomationId(app.TopDialogView!, "ConfirmButton");

        await PressByTheKeysAsync(app, create, Key.Enter);
        await AnswerAnyDialogsByClickAsync(app, () => !form.IsCreating && !string.IsNullOrEmpty(form.Error));

        Assert.False(form.IsClosed, "The form closed.");
        await AssertFocusAsync(app, Framed(create, KeyRegionRole.None), "Enter on Create, failed");
        form.CancelCommand.Execute(null);
    }

    /// <summary>
    /// "hello" signed in, a branch sweep made outside the app and never pushed, its create form
    /// opened from its menu and Create clicked, the push it runs first held by the fake: the form's
    /// Cancel, pressed by the keys while it may still stop the create, stops it and is disabled
    /// until the create has ended; then, the form still open, Cancel has the focus back, with the
    /// frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_create_forms_Cancel_pressed_by_the_keys_while_creating_gets_the_focus_back_with_the_frame()
    {
        await using var served = await ServeAsync();
        await served.Hello.GitAsync("branch", "sweep");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path], gitHub: served.GitHub);
        await app.WaitForRepositoryAsync();
        await app.SignInAsync(served.OctoToken, Cancelled);
        await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed("hello"));
        await app.WaitForWatchersQuietAsync();
        app.MoveToNeutral();
        app.RightClick(await app.RefRowAsync("Local branches", "sweep"));
        await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "sweep's menu");
        app.ClickMenuItem("Create pull request…");
        var form = await app.WaitForDialogAsync<CreatePullRequestDialogViewModel>();
        await app.WaitForAsync(() => form.CreateCommand.CanExecute(null), "Create enabled");
        var hold = served.GitHub.HoldNext("git:octo/hello");
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => hold.Arrived.IsCompleted && form.IsCreating && form.CanCancelForm, "the push held, Cancel enabled");
        app.MoveToNeutral();
        var cancel = app.FindByAutomationId(app.TopDialogView!, "CancelButton");

        await PressByTheKeysAsync(app, cancel, Key.Enter);
        await app.WaitForAsync(() => !form.IsCreating, "the create to end", TimeSpan.FromSeconds(60));
        hold.Dispose();
        await app.WaitForIdleAsync();

        Assert.False(form.IsClosed, "The form closed.");
        await AssertFocusAsync(app, Framed(cancel, KeyRegionRole.None), "Enter on Cancel while creating");
        form.CancelCommand.Execute(null);
    }

    // ---- The settings screen ---------------------------------------------------------------------

    /// <summary>
    /// The settings screen, a user name typed in the Git section, the session's global git
    /// configuration locked outside the app: Save, pressed by the keys, fails to write it; once its
    /// error is closed, Save has the focus back, with the frame, and the screen stays.
    /// </summary>
    [AvaloniaFact]
    public async Task The_settings_Save_pressed_by_the_keys_that_fails_to_write_gets_the_focus_back_with_the_frame()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Git);
        await app.WaitForSettingsReadsAsync(dialog);
        dialog.Git.Global.UserName = "Someone Else";
        app.Settle();
        var locked = GitIsolation.SessionGlobalConfigFile(data.Path) + ".lock";
        File.WriteAllText(locked, string.Empty);
        try
        {
            var save = app.FindByAutomationId(app.TopDialogView!, "ConfirmButton");

            await PressByTheKeysAsync(app, save, Key.Enter);
            await AnswerAnyDialogsByClickAsync(app, () => (dialog.IsClosed || !dialog.ConfirmCommand.IsRunning) && app.Session.Shell.Dialogs.Open.Count <= 1);

            Assert.False(dialog.IsClosed, "The settings screen closed: the write did not fail.");
            await AssertFocusAsync(app, Framed(save, KeyRegionRole.None), "Enter on Save, failed");
        }
        finally
        {
            File.Delete(locked);
        }
    }

    /// <summary>The Accounts section, GitHub's Sign in…, pressed by the keys, its dialog cancelled by Esc: Sign in… has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_accounts_Sign_in_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Accounts);
        await app.WaitForSettingsReadsAsync(dialog);
        var signIn = app.FindByAutomationId(app.TopDialogView!, "AccountsSignInButton");

        await PressByTheKeysAsync(app, signIn, Key.Enter);
        var signing = await app.WaitForDialogAsync<SignInDialogViewModel>();
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => signing.IsClosed, "the sign-in dialog to close");

        await AssertFocusAsync(app, Framed(signIn, KeyRegionRole.None), "Enter on Sign in…, cancelled");
    }

    /// <summary>The Accounts section, signed in to GitHub: Sign out…, pressed by the keys, its question cancelled by Esc, has the focus back, with the frame.</summary>
    [AvaloniaFact]
    public async Task The_accounts_Sign_out_pressed_by_the_keys_and_cancelled_gets_the_focus_back_with_the_frame()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        await app.WaitUntilReadyAsync();
        await app.SignInAsync(served.OctoToken, Cancelled);
        app.MoveToNeutral();
        var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Accounts);
        await app.WaitForSettingsReadsAsync(dialog);
        var signOut = app.FindByAutomationId(app.TopDialogView!, "AccountsSignOutButton");

        await PressByTheKeysAsync(app, signOut, Key.Enter);
        await CancelByEscAsync(app);

        await AssertFocusAsync(app, Framed(signOut, KeyRegionRole.None), "Enter on Sign out…, cancelled");
    }

    /// <summary>
    /// The sign-in dialog: its link to the token page, pressed by the keys while the browser's
    /// answer is held, and after it: the link keeps the focus, or has it back, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_sign_in_dialogs_token_link_pressed_by_the_keys_keeps_the_focus_with_the_frame()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path, gitHub: served.GitHub);
        await app.WaitUntilReadyAsync();
        app.MoveToNeutral();
        var dialog = await app.OpenSettingsAsync(SettingsOpening.Key);
        app.ChooseSection(SettingsSection.Accounts);
        await app.WaitForSettingsReadsAsync(dialog);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "AccountsSignInButton"));
        var signing = await app.WaitForDialogAsync<SignInDialogViewModel>();
        var link = app.FindByAutomationId(app.TopDialogView!, "SignInTokenLink");
        var held = new TaskCompletionSource();
        app.Launcher.Hold = held.Task;

        await PressByTheKeysAsync(app, link, Key.Enter);
        await app.WaitForAsync(() => app.Launcher.Requests.Count == 1, "the token page's request");
        held.SetResult();
        await app.WaitForAsync(() => !signing.OpenTokenPageCommand.IsRunning, "the link's run to end");

        await AssertFocusAsync(app, Framed(link, KeyRegionRole.None), "Enter on the token page's link");
        signing.CancelCommand.Execute(null);
    }

    // ---- With an account -------------------------------------------------------------------------

    /// <summary>
    /// "hello" open, GitHub not signed in: the "Pull requests" section's sign-in row, pressed by the
    /// keys, opens the sign-in dialog; signed in there, the section is read again and the row goes;
    /// the left panel keeps the focus, with the frame.
    /// </summary>
    [AvaloniaFact]
    public async Task The_pull_requests_sign_in_row_pressed_by_the_keys_leaves_the_focus_in_the_left_panel_once_signed_in()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path], gitHub: served.GitHub);
        await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        app.MoveToNeutral();
        var rows = Repository(app).LeftPanel.Rows;
        await app.WaitForAsync(() => rows.Any(row => row is PullRequestMessageRow { CanActivate: true }), "the sign-in row");
        var row = app.Window.GetVisualDescendants().OfType<Button>().First(button => button.DataContext is PullRequestMessageRow { CanActivate: true } && button.IsEffectivelyVisible);

        await PressByTheKeysAsync(app, row, Key.Enter);
        var signing = await app.WaitForDialogAsync<SignInDialogViewModel>();
        app.Type(served.OctoToken);
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => signing.IsClosed, "the sign-in dialog to close", TimeSpan.FromSeconds(60));
        await app.WaitForAsync(() => rows.Any(each => each is PullRequestRow), "the pull requests listed", TimeSpan.FromSeconds(60));
        await app.WaitForIdleAsync();

        try
        {
            await app.WaitForAsync(() => app.FocusedRegion() == KeyRegionRole.Left && app.FocusVisible, "the focus in the left panel, with the frame", FocusWait);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"After the sign-in from the row the focus is on {FocusFact(app)}.");
        }
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    /// <summary>"hello" open and signed in, the create form opened from main's menu, its base chosen as origin/topic/no-pr.</summary>
    private static async Task<ShellDriver> OpenCreateFormAsync(TempDirectory data, ServedHosting served)
    {
        var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path], gitHub: served.GitHub);
        try
        {
            await app.WaitForRepositoryAsync();
            await app.SignInAsync(served.OctoToken, Cancelled);
            await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed("hello"));
            await app.WaitForWatchersQuietAsync();
            app.MoveToNeutral();
            app.RightClick(await app.RefRowAsync("Local branches", "main"));
            await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "main's menu");
            app.ClickMenuItem("Create pull request…");
            var form = await app.WaitForDialogAsync<CreatePullRequestDialogViewModel>();
            await app.WaitForAsync(() => form.BaseText == "origin/main", "the form's base");
            app.Click(app.FindByAutomationId("PullRequestBaseChooseButton"));
            await app.WaitForDialogAsync<PickDialogViewModel>();
            await app.PickAsync("origin/topic/no-pr");
            await app.WaitForAsync(() => form.BaseText == "origin/topic/no-pr" && form.CreateCommand.CanExecute(null), "the chosen base and Create enabled");
            app.MoveToNeutral();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    /// <summary>"Manage remotes…" from the Remotes section's menu: the Remotes dialog, open.</summary>
    private static async Task<RemotesDialogViewModel> OpenRemotesAsync(ShellDriver app)
    {
        app.RightClick(await app.RefRowAsync("Remotes"));
        app.Menu();
        app.ClickMenuItem("Manage remotes…");
        var dialog = await app.WaitForDialogAsync<RemotesDialogViewModel>();
        app.MoveToNeutral();
        return dialog;
    }

    /// <summary>The button <paramref name="automationId"/> of the remote <paramref name="name"/>'s row in the Remotes dialog.</summary>
    private static Button RemoteButton(ShellDriver app, string name, string automationId) =>
        app.TopDialogView!.GetVisualDescendants().OfType<Button>()
            .Single(button => AutomationProperties.GetAutomationId(button) == automationId && button.DataContext is RemoteRow { Name: var remote } && remote == name);
}
