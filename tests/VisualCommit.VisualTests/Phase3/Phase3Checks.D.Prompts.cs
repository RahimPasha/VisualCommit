// Part D's checks of git's and ssh's prompts (docs/test-reports/phase-3.md, checks 21 and 21b):
// the credentials dialog against the auth server, and the passphrase, unknown-host and generic
// prompt dialogs through a real fetch over FakeSsh, then the real askpass helper in a prompt scope
// of the app's own broker.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksD
{
    /// <summary>
    /// Check 21: the toolbar's Fetch asks for a sign-in; signing in fetches; a second fetch asks
    /// again and is cancelled in the dialog; a wrong password fails the fetch of origin.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_21_credentials_dialog()
    {
        await using var scenario = await AuthAtServerAsync(requireAuth: true);
        var repo = scenario.Repo;
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the toolbar's Fetch; git asks for a user name.
        app.Click(ToolbarButton(app, "FetchButton"));
        await app.WaitForDialogAsync<CredentialsViewModel>(NetworkTimeout);
        Shot(app, "21a-credentials");
        AssertCard(app, "CredentialsCard", 400);
        Assert.Equal(scenario.WithPort(PartD.CredentialsTitle), DialogText(app, "DialogTitle"));
        Assert.Equal(scenario.WithPort(PartD.CredentialsText), DialogText(app, "CredentialsText"));
        Assert.False(app.FindByAutomationId(app.TopDialogView!, "DialogRepository").IsEffectivelyVisible, "The dialog names its repository; the window has one tab.");
        AssertLabelAbove(app, "User name", "CredentialUsername");
        AssertLabelAbove(app, "Password", "CredentialPassword");
        Assert.Equal(string.Empty, DialogText(app, "CredentialUsername"));
        Assert.Equal(string.Empty, DialogText(app, "CredentialPassword"));
        await AssertFocusedAsync(app, "CredentialUsername");
        Assert.Equal(["Cancel", "Sign in"], DialogButtons(app));
        Assert.False(DialogButton(app, "ConfirmButton").IsEffectivelyEnabled, "\"Sign in\" is enabled while the user name is empty.");
        AssertRunningWithoutPercent(app, PartD.FetchingFromAll);
        var unauthorized = Assert.Single(scenario.Server.Requests);
        AssertRequest(unauthorized, "GET", "/remote.git/info/refs", "service=git-upload-pack", null, null, 401);
        AssertNoClippedText(app);

        app.TypeInto("CredentialUsername", "alice");
        app.TypeInto("CredentialPassword", "s3cret");
        Assert.True(DialogButton(app, "ConfirmButton").IsEffectivelyEnabled, "\"Sign in\" is disabled with both boxes typed in.");
        var password = (TextBox)app.FindByAutomationId(app.TopDialogView!, "CredentialPassword");
        Assert.Equal(PromptDialogViewModel.MaskCharacter, password.PasswordChar);
        Assert.Equal(6, password.Text?.Length);

        // Step 2: "Sign in"; git's password prompt is answered from the kept password, with no second dialog.
        ClickId(app, "ConfirmButton");
        await WaitForResultAsync(app, PartD.FetchedFromAll);
        Shot(app, "21b-signed-in");
        await AssertNoDialogAsync(app);
        AssertIdleStatus(app, PartD.FetchedFromAll);
        AssertGraph(app, PartD.AuthFetchedGraph, lanes: 1);
        AssertAuthLeftPanel(app, behind: 1);
        var requests = scenario.Server.Requests;
        Assert.Equal(4, requests.Count);
        AssertRequest(requests[0], "GET", "/remote.git/info/refs", "service=git-upload-pack", null, null, 401);
        AssertRequest(requests[1], "GET", "/remote.git/info/refs", "service=git-upload-pack", null, "alice", 200);
        AssertRequest(requests[2], "POST", "/remote.git/git-upload-pack", string.Empty, "ls-refs", "alice", 200);
        AssertRequest(requests[3], "POST", "/remote.git/git-upload-pack", string.Empty, "fetch", "alice", 200);

        // Step 3: the dialog comes again (nothing was stored); Cancel ends the fetch as cancelled.
        var refs = await RefsAsync(repo);
        app.Click(ToolbarButton(app, "FetchButton"));
        await app.WaitForDialogAsync<CredentialsViewModel>(NetworkTimeout);
        ClickId(app, "CancelButton");
        await WaitForResultAsync(app, PartD.FetchCancelled);
        Shot(app, "21c-cancelled");
        await AssertNoDialogAsync(app);
        AssertIdleStatus(app, PartD.FetchCancelled);
        Assert.Equal(refs, await RefsAsync(repo));

        // Step 4: a wrong password for "Fetch origin".
        var mark = app.LogMark();
        await RightClickRowAsync(app, "Remotes", "origin");
        app.ClickMenuItem("Fetch origin");
        await app.WaitForDialogAsync<CredentialsViewModel>(NetworkTimeout);
        app.TypeInto("CredentialUsername", "alice");
        app.TypeInto("CredentialPassword", "wrong");
        ClickId(app, "ConfirmButton");
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        Shot(app, "21d-wrong-password");
        await AssertErrorDialogAsync(app, Expected(PartD.WrongPasswordDialog, scenario.Port));
        AssertStatus(app, PartD.FetchOriginFailed, OperationStatusTone.Danger);

        // The helper was asked twice, for the user name and then for the password (from the kept value), and nothing more.
        Assert.Equal(
            ["Username", "Password"],
            app.LogLinesSince(mark)
                .Select(line => System.Text.RegularExpressions.Regex.Match(line, @"\] auth: Fetch origin: git asks \((\w+)\)\.$"))
                .Where(match => match.Success)
                .Select(match => match.Groups[1].Value));

        // Throughout: neither password is written down.
        AssertNotWritten(app, "s3cret");
        AssertNotWritten(app, "wrong");
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check 21b: a real fetch over FakeSsh asks the passphrase, the unknown host and a PIN in the
    /// app's dialogs; then the real helper in a prompt scope of the app's broker answers a
    /// verification code with Enter, a passphrase with Esc, and ssh's confirmation with "Yes".
    /// </summary>
    [AvaloniaFact]
    public async Task Check_21b_ssh_prompt_dialogs()
    {
        await using var scenario = await AuthAtServerAsync();
        var repo = scenario.Repo;
        var fake = await FakeSsh.AddRemoteAsync(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Before step 1: both remotes open, fake-ssh with nothing under it.
        Assert.Equal([("fake-ssh", true), ("origin", true)], Remotes(app));
        Assert.Empty(RemoteChildren(app, "fake-ssh"));
        Assert.Equal(["main"], RemoteChildren(app, "origin"));

        // Step 1: "Fetch fake-ssh"; ssh asks the key's passphrase.
        await RightClickRowAsync(app, "Remotes", FakeSsh.RemoteName);
        app.ClickMenuItem("Fetch fake-ssh");
        await app.WaitForDialogAsync<PassphraseViewModel>(NetworkTimeout);
        Shot(app, "21b1-passphrase");
        AssertCard(app, "PassphraseCard", 400);
        Assert.Equal(PartD.PassphraseTitle, DialogText(app, "DialogTitle"));
        Assert.Equal(PartD.PassphraseText, DialogText(app, "PassphraseText"));
        Assert.Equal(string.Empty, DialogText(app, "PassphraseBox"));
        await AssertFocusedAsync(app, "PassphraseBox");
        Assert.Equal(PromptDialogViewModel.MaskCharacter, ((TextBox)app.FindByAutomationId(app.TopDialogView!, "PassphraseBox")).PasswordChar);
        Assert.Equal(["Cancel", "Unlock"], DialogButtons(app));
        AssertRunningWithoutPercent(app, PartD.FetchingFromFakeSsh);
        AssertNoClippedText(app);
        app.TypeInto("PassphraseBox", "s3cret");
        ClickId(app, "ConfirmButton");

        // Step 2: the unknown host.
        await app.WaitForDialogAsync<HostKeyViewModel>(NetworkTimeout);
        Shot(app, "21b2-unknown-host");
        AssertCard(app, "HostKeyCard", 400);
        Assert.Equal(PartD.HostKeyTitle, DialogText(app, "DialogTitle"));
        var hostKey = (TextBlock)app.FindByAutomationId(app.TopDialogView!, "HostKeyText");
        Assert.Equal(PartD.HostKeyText, hostKey.Text);
        Assert.Equal(CodeFont(app), hostKey.FontFamily);
        Assert.Equal(PartD.HostKeyQuestion, DialogText(app, "HostKeyQuestion"));
        Assert.True(app.BoundsOf(app.FindByAutomationId(app.TopDialogView!, "HostKeyQuestion")).Top >= app.BoundsOf(hostKey).Bottom, "The question is not under ssh's lines.");
        Assert.Equal(["Cancel", "Trust and connect"], DialogButtons(app));
        AssertRunningWithoutPercent(app, PartD.FetchingFromFakeSsh);
        AssertNoClippedText(app);
        ClickId(app, "ConfirmButton");

        // Step 3: a prompt no dialog of its own knows, masked because it asks for a PIN.
        await app.WaitForDialogAsync<PromptViewModel>(NetworkTimeout);
        Shot(app, "21b3-prompt");
        AssertCard(app, "PromptCard", 400);
        Assert.Equal(PartD.PromptTitle, DialogText(app, "DialogTitle"));
        Assert.Equal(PartD.PinText, DialogText(app, "PromptText"));
        await AssertFocusedAsync(app, "PromptBox");
        Assert.Equal(PromptDialogViewModel.MaskCharacter, ((TextBox)app.FindByAutomationId(app.TopDialogView!, "PromptBox")).PasswordChar);
        Assert.Equal(["Cancel", "OK"], DialogButtons(app));
        AssertNoClippedText(app);
        app.TypeInto("PromptBox", "123456");
        ClickId(app, "ConfirmButton");
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        Shot(app, "21b4-fetch-failed");
        await AssertErrorDialogAsync(app, Expected(PartD.FakeSshFailedDialog, scenario.Port));
        AssertStatus(app, PartD.FetchFakeSshFailed, OperationStatusTone.Danger);
        Assert.Equal(["s3cret", "yes", "123456"], fake.Answers);
        ClickId(app, "ConfirmButton");
        await AssertNoDialogAsync(app);

        // Steps 4 to 6: the real helper, run as OpenSSH runs it, in a prompt scope of the app's broker.
        using (var scope = app.Session.Services.Prompts.Begin("Fetch fake-ssh", "auth", PromptMode.Ask))
        {
            // Step 4: a verification code, not masked; Enter answers.
            var run = AskPassRunner.RunAsync(scope, PartD.VerificationPrompt);
            await app.WaitForDialogAsync<PromptViewModel>(NetworkTimeout);
            Assert.Equal(PartD.VerificationText, DialogText(app, "PromptText"));
            var box = (TextBox)app.FindByAutomationId(app.TopDialogView!, "PromptBox");
            Assert.Equal('\0', box.PasswordChar);
            app.TypeInto("PromptBox", "424242");
            Assert.Equal("424242", box.Text);
            app.PressKey(Key.Enter);
            var result = await CompletedAsync(app, run);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("424242\n", result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
            await AssertNoDialogAsync(app);

            // Step 5: the passphrase again, closed with Esc: no answer.
            run = AskPassRunner.RunAsync(scope, FakeSsh.PassphrasePrompt);
            await app.WaitForDialogAsync<PassphraseViewModel>(NetworkTimeout);
            app.PressKey(Key.Escape);
            result = await CompletedAsync(app, run);
            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.True(scope.UserCancelled, "The scope does not record the user's cancel.");
            await AssertNoDialogAsync(app);

            // Step 6: ssh's confirmation, answered "Yes".
            run = AskPassRunner.RunAsync(scope, PartD.ConfirmPromptText, "confirm");
            await AssertConfirmationAsync(app, PartD.SshAsksTitle, PartD.ConfirmPromptText, string.Empty, "Yes", destructive: false);
            ClickId(app, "ConfirmButton");
            result = await CompletedAsync(app, run);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("yes\n", result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
        }

        // Throughout: no answer is written down.
        AssertNotWritten(app, "s3cret");
        AssertNotWritten(app, "123456");
        AssertNotWritten(app, "424242");
    }

    /// <summary>Asserts one request the server served: method, path, query, protocol v2 command, user and status.</summary>
    private static void AssertRequest(AuthRemoteRequest request, string method, string path, string query, string? command, string? user, int status) =>
        Assert.Equal((method, path, query, command, user, status), (request.Method, request.Path, request.Query, request.Command, request.UserName, request.Status));

    /// <summary>Asserts that the label <paramref name="label"/> is shown right above the box <paramref name="automationId"/> of the dialog on top.</summary>
    private static void AssertLabelAbove(ShellDriver app, string label, string automationId)
    {
        var view = app.TopDialogView!;
        var box = app.BoundsOf(app.FindByAutomationId(view, automationId));
        var text = view.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == label && block.IsEffectivelyVisible);
        var bounds = app.BoundsOf(text);
        Assert.True(bounds.Bottom <= box.Top + 0.5 && box.Top - bounds.Bottom < 12, $"The label \"{label}\" at {bounds} is not right above {automationId} at {box}.");
    }

    /// <summary>The app's code font (<c>VcCodeFontFamily</c>), as the error dialog's output uses it.</summary>
    private static FontFamily CodeFont(ShellDriver app) =>
        app.Window.TryFindResource("VcCodeFontFamily", app.Window.ActualThemeVariant, out var font) && font is FontFamily family
            ? family
            : throw new Xunit.Sdk.XunitException("The app has no VcCodeFontFamily.");

    /// <summary>Waits, letting the UI thread run, until the helper's run has ended, and returns how.</summary>
    private static async Task<AskPassResult> CompletedAsync(ShellDriver app, Task<AskPassResult> run)
    {
        await app.WaitForAsync(() => run.IsCompleted, "the askpass helper to end", NetworkTimeout);
        return await run;
    }
}
