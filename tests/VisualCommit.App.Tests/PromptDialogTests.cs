using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The prompt dialogs (docs/phase-3-design.md 3.2, 4f; checks 21 and 21b): their view models'
/// rules, the presenter that shows them in the dialog layer, the real helper run as ssh runs it
/// against the app's own broker, and a fetch of the real app against <see cref="AuthRemoteServer"/>
/// and through a fake ssh. Pictures are taken at 1100×700 in both themes; set the environment
/// variable <c>VISUALCOMMIT_TEST_PICTURES</c> to a folder to keep them there.
/// </summary>
public class PromptDialogTests
{
    private const string UsernamePrompt = "Username for 'http://127.0.0.1:47391': ";
    private const string ConfirmPrompt = "Allow use of key /home/u/.ssh/id_ed25519?\nKey fingerprint SHA256:skokdAxs76eWz/jm50ga0Qz+ewMVrjE4KE91GAFIgTE.";

    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(60);

    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_credentials_dialog_asks_for_a_user_name_and_a_password_and_returns_both()
    {
        var dialog = new CredentialsViewModel(Prompt(GitPromptKind.Username, UsernamePrompt));

        Assert.Equal("Sign in to 127.0.0.1:47391", dialog.Title);
        Assert.Equal("Git needs a user name and a password or token for http://127.0.0.1:47391.", dialog.Text);
        Assert.True(dialog.IsUserEditable);
        Assert.Equal(("Sign in", string.Empty, false), (dialog.ConfirmText, dialog.RepositoryLine, dialog.HasRepositoryLine));
        Assert.False(dialog.ConfirmCommand.CanExecute(null), "Sign in is disabled while the user name is empty.");

        dialog.UserName = "  ";
        Assert.False(dialog.ConfirmCommand.CanExecute(null));
        dialog.UserName = "alice";
        dialog.Password = "s3cret";
        Assert.True(dialog.ConfirmCommand.CanExecute(null));
        dialog.ConfirmCommand.Execute(null);

        Assert.Equal(new PromptDialogAnswer("alice", "s3cret"), await dialog.Result);
        Assert.True(dialog.IsClosed);
    }

    [Fact]
    public async Task For_gits_password_prompt_the_user_is_shown_and_only_the_password_is_typed()
    {
        var dialog = new CredentialsViewModel(Prompt(GitPromptKind.Password, "Password for 'https://alice@example.com:8443/team': "), "auth");

        Assert.Equal("Sign in to example.com:8443", dialog.Title);
        Assert.Equal("Git needs a user name and a password or token for https://example.com:8443/team.", dialog.Text);
        Assert.False(dialog.IsUserEditable);
        Assert.True(dialog.IsUserShown);
        Assert.Equal("alice", dialog.UserName);
        Assert.Equal(("In auth", true), (dialog.RepositoryLine, dialog.HasRepositoryLine));
        Assert.True(dialog.ConfirmCommand.CanExecute(null));

        dialog.Password = "s3cret";
        dialog.ConfirmCommand.Execute(null);

        Assert.Equal(new PromptDialogAnswer("s3cret"), await dialog.Result);
    }

    [Fact]
    public async Task The_passphrase_dialog_names_the_key_or_shows_ssh_s_own_text()
    {
        var named = new PassphraseViewModel(Prompt(GitPromptKind.Passphrase, FakeSsh.PassphrasePrompt));
        var unnamed = new PassphraseViewModel(Prompt(GitPromptKind.Passphrase, "Enter passphrase: "));

        Assert.Equal(("Key passphrase", "Unlock"), (named.Title, named.ConfirmText));
        Assert.Equal("Enter the passphrase for /home/u/.ssh/id_ed25519.", named.Text);
        Assert.Equal("Enter passphrase:", unnamed.Text);

        named.Passphrase = "s3cret";
        named.ConfirmCommand.Execute(null);
        Assert.Equal(new PromptDialogAnswer("s3cret"), await named.Result);
    }

    [Fact]
    public async Task The_unknown_host_dialog_shows_ssh_s_lines_without_the_trailing_space_and_answers_yes()
    {
        var dialog = new HostKeyViewModel(Prompt(GitPromptKind.HostKey, FakeSsh.HostKeyPrompt));

        Assert.Equal(("Unknown host", "Trust this host and connect?", "Trust and connect"), (dialog.Title, dialog.Question, dialog.ConfirmText));
        Assert.Equal(FakeSsh.HostKeyPrompt.TrimEnd(), dialog.Text);
        Assert.Equal(4, dialog.Text.Split('\n').Length);
        Assert.EndsWith("(yes/no/[fingerprint])?", dialog.Text, StringComparison.Ordinal);

        dialog.ConfirmCommand.Execute(null);
        Assert.Equal(new PromptDialogAnswer("yes"), await dialog.Result);
    }

    [Theory]
    [InlineData(FakeSsh.PinPrompt, "Enter PIN for ED25519-SK key /home/u/.ssh/id_ed25519_sk:", true)]
    [InlineData("Verification code: ", "Verification code:", false)]
    [InlineData("git@example.com's password: ", "git@example.com's password:", true)]
    [InlineData("Personal access token: ", "Personal access token:", true)]
    public async Task The_generic_prompt_shows_the_text_and_masks_the_box_only_for_a_secret(string text, string shown, bool masked)
    {
        var dialog = new PromptViewModel(Prompt(GitPromptKind.Other, text));

        Assert.Equal(("Git asks", "OK"), (dialog.Title, dialog.ConfirmText));
        Assert.Equal(shown, dialog.Text);
        Assert.Equal(masked, dialog.IsMasked);
        Assert.Equal(masked ? PromptDialogViewModel.MaskCharacter : '\0', dialog.PasswordChar);

        dialog.Answer = "424242";
        dialog.ConfirmCommand.Execute(null);
        Assert.Equal(new PromptDialogAnswer("424242"), await dialog.Result);
    }

    [Fact]
    public async Task Cancel_is_the_users_and_a_closed_scope_is_no_answer_at_all()
    {
        var cancelled = new PassphraseViewModel(Prompt(GitPromptKind.Passphrase, FakeSsh.PassphrasePrompt)) { Passphrase = "typed" };
        var unanswered = new PassphraseViewModel(Prompt(GitPromptKind.Passphrase, FakeSsh.PassphrasePrompt)) { Passphrase = "typed" };

        cancelled.CancelCommand.Execute(null);
        unanswered.CloseUnanswered();
        unanswered.ConfirmCommand.Execute(null);

        Assert.Equal(PromptDialogAnswer.CancelledByUser, await cancelled.Result);
        Assert.Equal(PromptDialogAnswer.None, await unanswered.Result);
    }

    [Theory]
    [InlineData(GitPromptKind.Username, typeof(CredentialsViewModel))]
    [InlineData(GitPromptKind.Password, typeof(CredentialsViewModel))]
    [InlineData(GitPromptKind.Passphrase, typeof(PassphraseViewModel))]
    [InlineData(GitPromptKind.HostKey, typeof(HostKeyViewModel))]
    [InlineData(GitPromptKind.Other, typeof(PromptViewModel))]
    public void Each_kind_of_prompt_has_its_dialog(GitPromptKind kind, Type dialog)
    {
        Assert.IsType(dialog, PromptDialogPresenter.CreateDialog(Prompt(kind, "x: ")));
        Assert.Null(PromptDialogPresenter.CreateDialog(Prompt(GitPromptKind.Confirm, "x?")));
        Assert.Null(PromptDialogPresenter.CreateDialog(Prompt(GitPromptKind.Notice, "x")));
    }

    [Fact]
    public async Task The_presenter_shows_the_dialog_on_top_of_an_open_one_and_returns_its_answer()
    {
        var dialogs = new DialogHostViewModel();
        var presenter = new PromptDialogPresenter(dialogs) { ShowsRepositoryName = () => true };
        var below = dialogs.ConfirmAsync(new ConfirmationRequest("Remotes", "-", string.Empty, "Close"));

        var shown = presenter.ShowAsync(Prompt(GitPromptKind.Username, UsernamePrompt), TestCancelled);

        // Dialogs stack (D104): the prompt goes over the dialog that was open.
        Assert.Equal(2, dialogs.Open.Count);
        var credentials = Assert.IsType<CredentialsViewModel>(dialogs.Current);
        Assert.Equal("In auth", credentials.RepositoryLine);
        credentials.UserName = "alice";
        credentials.Password = "s3cret";
        credentials.ConfirmCommand.Execute(null);

        Assert.Equal(new PromptDialogAnswer("alice", "s3cret"), await shown);
        Assert.IsType<ConfirmationViewModel>(dialogs.Current);
        Assert.False(below.IsCompleted);
        dialogs.CancelCurrent();
        Assert.False(await below);
    }

    [Fact]
    public async Task When_the_scope_ends_the_dialog_closes_without_an_answer_and_without_a_cancel()
    {
        var dialogs = new DialogHostViewModel();
        var presenter = new PromptDialogPresenter(dialogs);
        using var scope = new CancellationTokenSource();

        var shown = presenter.ShowAsync(Prompt(GitPromptKind.HostKey, FakeSsh.HostKeyPrompt), scope.Token);
        Assert.IsType<HostKeyViewModel>(dialogs.Current);
        await scope.CancelAsync();

        Assert.Equal(PromptDialogAnswer.None, await shown);
        Assert.Empty(dialogs.Open);
        Assert.Equal(PromptDialogAnswer.None, await presenter.ShowAsync(Prompt(GitPromptKind.Other, "x: "), scope.Token));
        Assert.Empty(dialogs.Open);
    }

    [Fact]
    public async Task Esc_on_a_prompt_dialog_is_a_cancel_of_the_user()
    {
        var dialogs = new DialogHostViewModel();
        var presenter = new PromptDialogPresenter(dialogs);

        var shown = presenter.ShowAsync(Prompt(GitPromptKind.Passphrase, FakeSsh.PassphrasePrompt), TestCancelled);
        Assert.True(dialogs.CancelCurrent());

        Assert.Equal(PromptDialogAnswer.CancelledByUser, await shown);
        Assert.Empty(dialogs.Open);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ssh_s_confirm_prompt_is_the_confirmation_titled_ssh_asks_with_yes(bool yes)
    {
        var dialogs = new DialogHostViewModel();
        var presenter = new PromptDialogPresenter(dialogs);

        var shown = presenter.ShowAsync(Prompt(GitPromptKind.Confirm, ConfirmPrompt), TestCancelled);

        var confirmation = Assert.IsType<ConfirmationViewModel>(dialogs.Current);
        Assert.Equal(("ssh asks", ConfirmPrompt, string.Empty, false, "Yes"), (confirmation.Title, confirmation.Question, confirmation.Detail, confirmation.HasDetail, confirmation.ConfirmText));
        Assert.False(confirmation.IsDestructive);
        if (yes)
        {
            confirmation.ConfirmCommand.Execute(null);
        }
        else
        {
            confirmation.CancelCommand.Execute(null);
        }

        Assert.Equal(yes ? new PromptDialogAnswer("yes") : PromptDialogAnswer.CancelledByUser, await shown);
    }

    [Fact]
    public async Task A_notice_gets_no_dialog_and_stays_in_the_notices_until_its_scope_ends()
    {
        var dialogs = new DialogHostViewModel();
        var handler = new PromptDialogs(dialogs);
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();

        var touch = handler.AnswerAsync(Prompt(GitPromptKind.Notice, "Confirm user presence for key ED25519-SK SHA256:abc\n"), first.Token);
        Assert.Equal("Confirm user presence for key ED25519-SK SHA256:abc", dialogs.Notices.Text);
        var other = handler.AnswerAsync(Prompt(GitPromptKind.Notice, "Touch your key"), second.Token);

        Assert.Empty(dialogs.Open);
        Assert.Equal(("Touch your key", true), (dialogs.Notices.Text, dialogs.Notices.IsShown));
        Assert.False(touch.IsCompleted);

        await second.CancelAsync();
        Assert.Equal(PromptAnswer.Refused, await other);
        Assert.Equal("Confirm user presence for key ED25519-SK SHA256:abc", dialogs.Notices.Text);
        await first.CancelAsync();
        Assert.Equal(PromptAnswer.Refused, await touch);
        Assert.Null(dialogs.Notices.Text);
        Assert.False(dialogs.Notices.IsShown);
    }

    [AvaloniaFact]
    public async Task A_prompt_from_another_thread_is_shown_on_the_UI_thread()
    {
        var dialogs = new DialogHostViewModel();
        var presenter = new PromptDialogPresenter(dialogs);
        var changedOnUiThread = new List<bool>();
        dialogs.Open.CollectionChanged += (_, _) => changedOnUiThread.Add(Dispatcher.UIThread.CheckAccess());

        var shown = Task.Run(() => presenter.ShowAsync(Prompt(GitPromptKind.Other, "Verification code: "), TestCancelled), TestCancelled);
        await WaitAsync(() => dialogs.Current is PromptViewModel, "the prompt dialog");
        var dialog = (PromptViewModel)dialogs.Current!;
        dialog.Answer = "424242";
        dialog.ConfirmCommand.Execute(null);
        await WaitAsync(() => shown.IsCompleted, "the answer");

        Assert.Equal(new PromptDialogAnswer("424242"), await shown);
        Assert.Equal([true, true], changedOnUiThread);
    }

    [AvaloniaTheory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task The_dialogs_drawn_at_1100_by_700(string theme)
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        app.Window.RequestedThemeVariant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var presenter = new PromptDialogPresenter(app.Session.Services.DialogHost);
        var panel = theme == "dark" ? Color.Parse("#1A1D24") : Color.Parse("#F5F6F8");

        var cases = new (GitPrompt Prompt, string Root, string Card, string Focused, string[] Texts)[]
        {
            (Prompt(GitPromptKind.Username, UsernamePrompt), "CredentialsDialog", "CredentialsCard", "CredentialUsername",
                ["Sign in to 127.0.0.1:47391", "Git needs a user name and a password or token for http://127.0.0.1:47391.", "Cancel", "Sign in"]),
            (Prompt(GitPromptKind.Password, "Password for 'http://alice@127.0.0.1:47391': "), "CredentialsDialog", "CredentialsCard", "CredentialPassword",
                ["Sign in to 127.0.0.1:47391", "alice"]),
            (Prompt(GitPromptKind.Passphrase, FakeSsh.PassphrasePrompt), "PassphraseDialog", "PassphraseCard", "PassphraseBox",
                ["Key passphrase", "Enter the passphrase for /home/u/.ssh/id_ed25519.", "Unlock"]),
            (Prompt(GitPromptKind.HostKey, FakeSsh.HostKeyPrompt), "HostKeyDialog", "HostKeyCard", "CancelButton",
                ["Unknown host", FakeSsh.HostKeyPrompt.TrimEnd(), "Trust this host and connect?", "Trust and connect"]),
            (Prompt(GitPromptKind.Other, FakeSsh.PinPrompt), "PromptDialog", "PromptCard", "PromptBox",
                ["Git asks", "Enter PIN for ED25519-SK key /home/u/.ssh/id_ed25519_sk:", "OK"]),
            (Prompt(GitPromptKind.Confirm, ConfirmPrompt), "ConfirmationDialog", "ConfirmationCard", "CancelButton",
                ["ssh asks", ConfirmPrompt, "Yes"]),
        };

        foreach (var (prompt, root, cardName, focused, texts) in cases)
        {
            using var scope = new CancellationTokenSource();
            var shown = presenter.ShowAsync(prompt, scope.Token);
            app.Settle();
            var shot = app.Capture();
            Keep(shot, $"{theme}-{root}-{prompt.Kind}");

            var view = app.FindByAutomationId(root);
            var card = app.Find<Border>(cardName);
            var bounds = app.BoundsOf(card);
            Assert.Equal(400, bounds.Width, 0.5);
            Assert.InRange(bounds.Center.X, 549, 551);
            Assert.Equal(panel, shot.PixelAt((int)bounds.X + 6, (int)bounds.Y + 6));
            Assert.Equal(focused, Avalonia.Automation.AutomationProperties.GetAutomationId((Control)app.Window.FocusManager!.GetFocusedElement()!));
            var shownTexts = view.GetVisualDescendantsText();
            Assert.All(texts, text => Assert.Contains(text, shownTexts));
            Assert.Empty(LayoutAudit.FindClippedText(app.Window));

            await scope.CancelAsync();
            await WaitAsync(() => shown.IsCompleted, "the dialog to close");
            app.Settle();
            Assert.False(app.Session.Services.DialogHost.IsOpen);
        }
    }

    [AvaloniaFact]
    public async Task The_masked_boxes_show_a_mask_for_each_character_and_enter_answers()
    {
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        var presenter = new PromptDialogPresenter(app.Session.Services.DialogHost);

        var shown = presenter.ShowAsync(Prompt(GitPromptKind.Username, UsernamePrompt), TestCancelled);
        app.Settle();
        var signIn = (Button)app.FindByAutomationId("ConfirmButton");
        Assert.False(signIn.IsEffectivelyEnabled, "Sign in is disabled while the user name is empty.");
        app.TypeInto("CredentialUsername", "alice");
        app.TypeInto("CredentialPassword", "s3cret");
        Assert.True(signIn.IsEffectivelyEnabled);
        var password = (TextBox)app.FindByAutomationId("CredentialPassword");
        Assert.Equal(PromptDialogViewModel.MaskCharacter, password.PasswordChar);
        Assert.Equal("s3cret", password.Text);
        Keep(app.Capture(), "dark-CredentialsDialog-typed");

        app.PressKey(Key.Enter);
        await WaitAsync(() => shown.IsCompleted, "the answer");
        Assert.Equal(new PromptDialogAnswer("alice", "s3cret"), await shown);

        // The generic prompt, not masked: what is typed shows.
        shown = presenter.ShowAsync(Prompt(GitPromptKind.Other, "Verification code: "), TestCancelled);
        app.Settle();
        app.TypeInto("PromptBox", "424242");
        Assert.Equal('\0', ((TextBox)app.FindByAutomationId("PromptBox")).PasswordChar);
        Keep(app.Capture(), "dark-PromptDialog-verification-code");
        app.PressKey(Key.Enter);
        await WaitAsync(() => shown.IsCompleted, "the answer");
        Assert.Equal(new PromptDialogAnswer("424242"), await shown);
    }

    [AvaloniaFact]
    public async Task The_real_helper_run_as_ssh_runs_it_gets_the_answers_of_the_apps_dialogs()
    {
        // Check 21b steps 4 to 6, and the notice (design 3.5): the session's broker and dialogs,
        // the real helper with an open scope's environment, ssh's way of running it.
        using var data = new TempDirectory("data");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();
        using var scope = app.Session.Services.Prompts.Begin("Fetch", "auth", PromptMode.Ask);

        // Step 4: a prompt no dialog of its own knows; Enter acts as OK.
        var run = RunHelper(scope, "Verification code: ");
        var prompt = await app.WaitForDialogAsync<PromptViewModel>(StepTimeout);
        Assert.Equal(("Verification code:", false), (prompt.Text, prompt.IsMasked));
        app.TypeInto("PromptBox", "424242");
        app.PressKey(Key.Enter);
        var result = await WaitForAsync(app, run);
        Assert.Equal((0, "424242\n"), (result.ExitCode, result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal)));

        // Step 5: the passphrase dialog, closed with Esc: no output, exit 1, the user cancelled.
        run = RunHelper(scope, FakeSsh.PassphrasePrompt);
        await app.WaitForDialogAsync<PassphraseViewModel>(StepTimeout);
        Assert.True(((TextBox)app.FindByAutomationId("PassphraseBox")).IsFocused);
        app.PressKey(Key.Escape);
        result = await WaitForAsync(app, run);
        Assert.Equal((1, string.Empty), (result.ExitCode, result.StandardOutput));
        Assert.True(scope.UserCancelled);
        Assert.False(app.Session.Services.DialogHost.IsOpen);

        // Step 6: ssh's confirm prompt: "ssh asks", Cancel focused, "Yes" prints yes.
        run = RunHelper(scope, ConfirmPrompt, "confirm");
        var confirmation = await app.WaitForDialogAsync<ConfirmationViewModel>(StepTimeout);
        Assert.Equal(("ssh asks", ConfirmPrompt, false), (confirmation.Title, confirmation.Question, confirmation.HasDetail));
        app.Settle();
        Assert.True(((Button)app.FindByAutomationId("CancelButton")).IsFocused);
        app.Click(app.FindByAutomationId("ConfirmButton"));
        result = await WaitForAsync(app, run);
        Assert.Equal((0, "yes\n"), (result.ExitCode, result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal)));

        // A notice (SSH_ASKPASS_PROMPT=none): no dialog; shown until the helper goes, which it
        // does when the scope ends.
        run = RunHelper(scope, "Confirm user presence for key ED25519-SK SHA256:skokdAxs76eWz", "none");
        var notices = app.Session.Services.DialogHost.Notices;
        await app.WaitForAsync(() => notices.IsShown, "the notice", StepTimeout);
        Assert.Equal("Confirm user presence for key ED25519-SK SHA256:skokdAxs76eWz", notices.Text);
        Assert.False(app.Session.Services.DialogHost.IsOpen);
        scope.Dispose();
        result = await WaitForAsync(app, run);
        Assert.Equal((1, string.Empty), (result.ExitCode, result.StandardOutput));
        await app.WaitForAsync(() => !notices.IsShown, "the notice to go");
    }

    [AvaloniaFact]
    public async Task A_fetch_of_the_real_app_signs_in_through_the_credentials_dialog_and_a_cancel_ends_it_cancelled()
    {
        // Check 21 headless, the fetch started through the repository's queue as the toolbar's
        // Fetch starts it (the button itself is work package WP6's).
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync();
        var opened = new List<DialogViewModel>();
        app.Session.Services.DialogHost.Open.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                opened.AddRange(e.NewItems!.OfType<DialogViewModel>());
            }
        };

        // Step 1: the dialog asks; nothing came through the server but the 401.
        var fetch = repository.Queue.RunAsync(Fetch(repository.Repository));
        var credentials = await app.WaitForDialogAsync<CredentialsViewModel>(StepTimeout);
        app.Settle();
        Keep(app.Capture(), "dark-21a-credentials");
        Assert.Equal($"Sign in to 127.0.0.1:{server.Port}", credentials.Title);
        Assert.Equal($"Git needs a user name and a password or token for http://127.0.0.1:{server.Port}.", credentials.Text);
        Assert.False(credentials.HasRepositoryLine);
        Assert.True(((TextBox)app.FindByAutomationId("CredentialUsername")).IsFocused);
        Assert.False(((Button)app.FindByAutomationId("ConfirmButton")).IsEffectivelyEnabled);
        var first = Assert.Single(server.Requests);
        Assert.Equal((401, null), (first.Status, first.UserName));

        // Step 2: one dialog only; git's password prompt is answered with the kept password.
        app.TypeInto("CredentialUsername", "alice");
        app.TypeInto("CredentialPassword", "s3cret");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForIdleAsync(repository, StepTimeout);
        var signedIn = await fetch;
        Keep(app.Capture(), "dark-21b-signed-in");
        Assert.Equal((OperationOutcome.Succeeded, "Fetched from all remotes"), (signedIn.Outcome, signedIn.Message));
        Assert.Single(opened);
        Assert.Equal("Remote work", (await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")).StandardOutput.Trim());
        Assert.Contains(server.Requests, request => request.Status == 200 && request.UserName == "alice" && request.Command == "fetch");

        // Step 3: nothing was stored, so the dialog comes again; Cancel ends the fetch as cancelled.
        var before = await repo.RefTargetAsync("refs/remotes/origin/main");
        fetch = repository.Queue.RunAsync(Fetch(repository.Repository));
        await app.WaitForDialogAsync<CredentialsViewModel>(StepTimeout);
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForIdleAsync(repository, StepTimeout);
        var cancelled = await fetch;
        Keep(app.Capture(), "dark-21c-cancelled");
        Assert.Equal((OperationOutcome.Cancelled, "Fetch cancelled"), (cancelled.Outcome, cancelled.Message));
        Assert.Equal(2, opened.Count);
        Assert.False(app.Session.Services.DialogHost.IsOpen);
        Assert.Equal(before, await repo.RefTargetAsync("refs/remotes/origin/main"));

        // Throughout: the password is in no call record and no log line.
        Assert.All(app.Session.GitCalls.Snapshot(), call =>
        {
            Assert.DoesNotContain("s3cret", call.CommandText, StringComparison.Ordinal);
            Assert.DoesNotContain("s3cret", call.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("s3cret", call.StandardError, StringComparison.Ordinal);
        });
        Assert.All(
            Directory.EnumerateFiles(app.Session.Paths.LogDirectory),
            file => Assert.DoesNotContain("s3cret", ReadShared(file), StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task A_fetch_over_a_fake_ssh_shows_the_passphrase_host_key_and_prompt_dialogs_in_turn()
    {
        // Check 21b steps 1 to 3 headless (D115): FakeSsh runs the app's helper with the recorded texts.
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var fakeSsh = await FakeSsh.AddRemoteAsync(repo);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync();

        var fetch = repository.Queue.RunAsync(Fetch(repository.Repository, FakeSsh.RemoteName));
        var passphrase = await app.WaitForDialogAsync<PassphraseViewModel>(StepTimeout);
        app.Settle();
        Keep(app.Capture(), "dark-21b1-passphrase");
        Assert.Equal("Enter the passphrase for /home/u/.ssh/id_ed25519.", passphrase.Text);
        app.TypeInto("PassphraseBox", "s3cret");
        app.Click(app.FindByAutomationId("ConfirmButton"));

        var hostKey = await app.WaitForDialogAsync<HostKeyViewModel>(StepTimeout);
        app.Settle();
        Keep(app.Capture(), "dark-21b2-unknown-host");
        Assert.Equal(FakeSsh.HostKeyPrompt.TrimEnd(), hostKey.Text);
        app.Click(app.FindByAutomationId("ConfirmButton"));

        var pin = await app.WaitForDialogAsync<PromptViewModel>(StepTimeout);
        app.Settle();
        Keep(app.Capture(), "dark-21b3-prompt");
        Assert.True(pin.IsMasked);
        app.TypeInto("PromptBox", "123456");
        app.Click(app.FindByAutomationId("ConfirmButton"));

        await app.WaitForIdleAsync(repository, StepTimeout);
        var failed = await fetch;
        Assert.Equal((OperationOutcome.Failed, "Fetch fake-ssh failed"), (failed.Outcome, failed.Message));
        Assert.Contains(FakeSsh.Refusal, failed.Failure!.GitOutput, StringComparison.Ordinal);
        Assert.Equal(["s3cret", "yes", "123456"], fakeSsh.Answers);
    }

    private static GitPrompt Prompt(GitPromptKind kind, string text)
    {
        var classified = GitPromptClassifier.Classify(text, null);
        return new GitPrompt(kind, text, "op-1", "Fetch", "auth", classified.Url, classified.User, classified.KeyPath);
    }

    /// <summary>The fetch as the toolbar and the remote's menu start it (2.16, 2.33).</summary>
    private static GitOperation Fetch(IGitRepository repository, string? remote = null) => new(
        remote is null ? "Fetch" : $"Fetch {remote}",
        remote is null ? "Fetching from all remotes…" : $"Fetching from {remote}…",
        OperationKind.Network,
        RefreshScope.Refs,
        async context =>
        {
            await repository.FetchAsync(remote, NetworkOptions.From(context), cancellationToken: context.CancellationToken);
            return OperationReport.Done(remote is null ? "Fetched from all remotes" : $"Fetched from {remote}");
        });

    private static Task<AskPassResult> RunHelper(PromptScope scope, string prompt, string? sshPromptKind = null)
    {
        var environment = scope.Environment;
        return Task.Run(() => AskPassRunner.RunAsync(environment, prompt, sshPromptKind, timeout: StepTimeout, cancellationToken: TestCancelled), TestCancelled);
    }

    private static async Task<AskPassResult> WaitForAsync(ShellDriver app, Task<AskPassResult> run)
    {
        await app.WaitForAsync(() => run.IsCompleted, "the helper to end", StepTimeout);
        return await run;
    }

    /// <summary>Waits on the UI thread, letting it run, until <paramref name="condition"/> holds.</summary>
    private static async Task WaitAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + StepTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestCancelled);
        }
    }

    private static string ReadShared(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Saves <paramref name="shot"/> under <c>VISUALCOMMIT_TEST_PICTURES</c> when that names a folder.</summary>
    private static void Keep(Screenshot shot, string name)
    {
        var folder = Environment.GetEnvironmentVariable("VISUALCOMMIT_TEST_PICTURES");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
            shot.Save(Path.Combine(folder, $"prompt-{name}.png"));
        }
    }
}

/// <summary>The texts a view shows, for asserting what is on a dialog.</summary>
internal static class VisualTextExtensions
{
    public static IReadOnlyList<string> GetVisualDescendantsText(this Control control) =>
        [.. Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(control).OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible && !string.IsNullOrEmpty(block.Text))
            .Select(block => block.Text!)];
}
