using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Hosting;
using VisualCommit.Hosting.Tokens;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part A, accounts and sign-in (docs/test-reports/phase-7a.md, checks A1 to A11; design 8.7): the
/// settings screen's Accounts section, the "Add GitHub account" dialog, sign-in with the keychain and
/// for the session only, sign-out, "needs sign-in" after the API's and git's refusals and at a start
/// whose token is gone, a restart that keeps the sign-in, and the "Use for git over HTTPS" switch.
/// One method per app start, as the report's table of part A's methods lists them.
/// </summary>
public sealed partial class Phase7aChecksA : Phase7aChecks
{
    private static readonly string[] AccountIds =
    [
        "AccountLogin", "AccountTokenPlace", "AccountNeedsSignIn", "AccountsSignOutNote", "AccountsSignOutButton",
        "AccountsBusy", "AccountsOff", "AccountUseForGit", "AccountUseForGitNote",
    ];

    /// <summary>Method 1: A1, A2 and A5 part A on one empty tab at 1100×700, dark.</summary>
    [AvaloniaFact]
    public async Task Check_A1_A2_A5_signed_out_dialog_and_signed_in()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a a1");
        var port = served.GitHub.Port;
        var app = await StartEmptyAsync(data, served);
        try
        {
            // A1 step 1: off no more, nobody signed in, nothing asked (D257).
            var session = app.Session.Services.Hosting;
            Assert.False(session.IsOff);
            Assert.Null(session.Account);
            Assert.Empty(served.GitHub.Requests);
            Assert.Empty(app.Tokens.Calls);
            Assert.False(File.Exists(AccountsFile(data)));
            var quiet = await app.QuietCallMarkAsync();

            // A1 step 2.
            var screen = await app.OpenAccountsAsync();
            Assert.Equal(SettingsSection.Accounts, screen.SelectedSection);
            Assert.True(screen.ShowsAccounts);
            AssertFrame(app, full: false);
            Assert.Equal("SettingsNavAccounts", FocusedId(app));
            AssertSignedOutSection(app, full: false);
            ShotA(app, PartA.A01SignedOut);
            var calls = await app.QuietCallsSinceAsync(quiet);
            Assert.Equal(["git config --show-scope --show-origin -z --list"], calls.Select(call => call.CommandText));
            Assert.True(SameFolder(calls[0].WorkingDirectory, data.Path), $"The screen's read ran in {calls[0].WorkingDirectory}.");
            Assert.Empty(served.GitHub.Requests);
            Assert.Empty(app.Tokens.Calls);

            // A2 step 3: the dialog over the screen.
            var dialog = await app.OpenSignInDialogAsync();
            AssertDialog(app, screen, dialog, full: false);
            Assert.Equal(["probe"], app.Tokens.Calls);
            Assert.Empty(served.GitHub.Requests);
            ShotA(app, PartA.A02DialogEmpty);

            // A2 step 4: the token masked, on screen and to UI Automation (D103, D130).
            app.TypeToken(OctoToken);
            AssertMasked(app, OctoToken, nameof(OctoToken));
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.True(CancelOf(app).IsEffectivelyEnabled);
            Assert.Empty(app.Launcher.Requests);
            Assert.Empty(served.GitHub.Requests);
            Assert.Equal(["probe"], app.Tokens.Calls);
            ShotA(app, PartA.A02TokenMasked);

            // A2 step 5: the link's tooltip, then its page (D295).
            var link = app.FindByAutomationId(app.SignInDialogView(), "SignInTokenLink");
            app.MoveMouse(link);
            Assert.Equal(PartA.TokenLinkTip, await app.WaitForToolTipAsync(link));
            app.Click(link);
            app.Settle();
            var launch = Assert.Single(app.Launcher.Requests);
            Assert.Equal(FileLaunchKind.OpenUrl, launch.Kind);
            Assert.Equal(WebBase(port) + PartA.TokenPage, launch.FullPath);
            Assert.False(dialog.IsClosed);
            AssertMasked(app, OctoToken, nameof(OctoToken));
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.Empty(served.GitHub.Requests);
            Assert.Equal(["probe"], app.Tokens.Calls);

            // A2 step 6: Esc closes the dialog only.
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => dialog.IsClosed, "the sign-in dialog to close");
            app.Settle();
            Assert.False(screen.IsClosed);
            Assert.Equal(SettingsSection.Accounts, screen.SelectedSection);
            AssertSignedOutSection(app, full: false);
            Assert.Null(session.Account);
            Assert.False(File.Exists(AccountsFile(data)));
            Assert.Empty(served.GitHub.Requests);
            Assert.Equal(["probe"], app.Tokens.Calls);

            // A5 step 7: signed in with the keychain.
            var requests = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;
            dialog = await app.OpenSignInDialogAsync();
            app.TypeToken(OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            Assert.True(dialog.IsClosed, "The dialog did not close after the sign-in.");
            AssertOneUserRequest(RequestsSince(served, requests), 200);
            Assert.Equal([$"save {PartA.OctoKey(port)}"], StoreCallsSince(app, stored));
            Assert.True(app.Tokens.Holds(OctoKey(served)));
            AssertSignedIn(session, TokenPlace.Keychain, AccountNoteKind.None);
            AssertRecord(data, served, useForGit: true, "keychain");
            await app.WaitForAsync(() => FocusedId(app) == "AccountsSignOutButton", "the focus on Sign out…");
            AssertSignedInSection(app, PartA.InKeychain, full: false);
            ShotA(app, PartA.A05SignedIn);
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served, launches: 1);
    }

    /// <summary>Method 2: A5 part B, signed in through the dialog at 1920×1080, dark, then light.</summary>
    [AvaloniaFact]
    public async Task Check_A5_signed_in_at_1920x1080()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a a5");
        var app = await StartEmptyAsync(data, served, width: 1920, height: 1080);
        try
        {
            // Step 8.
            var screen = await app.OpenAccountsAsync();
            AssertFrame(app, full: true);
            AssertSignedOutSection(app, full: true);
            AssertSaveButtonsFull(app);
            var dialog = await app.OpenSignInDialogAsync();
            AssertDialog(app, screen, dialog, full: true);
            Assert.Equal(["probe"], app.Tokens.Calls);
            Assert.Empty(served.GitHub.Requests);

            // Step 9.
            app.TypeToken(OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            Assert.True(dialog.IsClosed, "The dialog did not close after the sign-in.");
            AssertOneUserRequest(served.GitHub.Requests, 200);
            Assert.Equal(["probe", $"save {PartA.OctoKey(served.GitHub.Port)}"], app.Tokens.Calls);
            AssertSignedIn(app.Session.Services.Hosting, TokenPlace.Keychain, AccountNoteKind.None);
            await app.WaitForAsync(() => FocusedId(app) == "AccountsSignOutButton", "the focus on Sign out…");
            AssertFrame(app, full: true);
            AssertSignedInSection(app, PartA.InKeychain, full: true);
            AssertSaveButtonsFull(app);
            ShotA(app, PartA.A05SignedInFull);

            // Step 10: the light theme by the toolbar's switch; still signed in (D256).
            var requests = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            app.Click(app.FindByAutomationId("ThemeSwitch"));
            await app.WaitForAsync(() => app.Window.ActualThemeVariant == ThemeVariant.Light, "the light theme");
            await app.OpenAccountsAsync();
            Assert.Equal("SettingsNavAccounts", FocusedId(app));
            AssertSignedIn(app.Session.Services.Hosting, TokenPlace.Keychain, AccountNoteKind.None);
            Assert.Equal(requests, served.GitHub.Requests.Count);
            Assert.Equal(stored, app.Tokens.Calls.Count);
            Assert.Equal(Light.Panel, (CardNamed(app, "SettingsCard").Background as ISolidColorBrush)?.Color);
            Assert.Equal(Light.TextSecondary, ColourOf((TextBlock)app.FindByAutomationId("AccountsIntro")));
            var useForGit = (CheckBox)app.FindByAutomationId("AccountUseForGit");
            Assert.True(useForGit.IsChecked);
            var tick = useForGit.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Box");
            Assert.Equal(Light.Accent, (tick.Background as ISolidColorBrush)?.Color);
            AssertFrame(app, full: true);
            AssertSignedInSection(app, PartA.InKeychain, full: true);
            ShotA(app, PartA.A05SignedInFullLight);
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served);
    }

    /// <summary>Method 3: A3, A4 and A7 on one empty tab: the check held and cancelled, refusals, a failed save.</summary>
    [AvaloniaFact]
    public async Task Check_A3_A4_A7_checking_refused_and_save_failed()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a a3");
        var port = served.GitHub.Port;
        var app = await StartEmptyAsync(data, served);
        FakeGitHubHold? hold = null;
        try
        {
            // A3 step 1.
            var screen = await app.OpenAccountsAsync();
            var dialog = await app.OpenSignInDialogAsync();

            // A3 step 2: the check held at the fake.
            var mark = served.GitHub.Requests.Count;
            hold = served.GitHub.HoldNext("user");
            app.TypeToken(OctoToken);
            app.Click(ConfirmOf(app));
            await hold.Arrived.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
            app.Settle();
            var view = app.SignInDialogView();
            var checking = (TextBlock)app.FindByAutomationId(view, "SignInChecking");
            Assert.Equal(PartA.Checking, checking.Text);
            Assert.Equal(Dark.TextSecondary, ColourOf(checking));
            Assert.False(BoxOf(app).IsEffectivelyEnabled);
            AssertMasked(app, OctoToken, nameof(OctoToken));
            Assert.False(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.True(CancelOf(app).IsEffectivelyEnabled);
            Assert.Empty(RequestsSince(served, mark));
            Assert.Null(app.Session.Services.Hosting.Account);
            Assert.Equal(["probe"], app.Tokens.Calls);
            ShotA(app, PartA.A03Checking);

            // A3 step 3: Cancel closes the dialog at once; the released request is recorded later.
            app.Click(CancelOf(app));
            app.Settle();
            Assert.True(dialog.IsClosed, "Cancel did not close the dialog before the hold's release.");
            AssertSignedOutSection(app, full: false);
            hold.Dispose();
            hold = null;
            await app.WaitForAsync(() => RequestsSince(served, mark).Any(request => request.Route == "user"), "the released request to be recorded");
            AssertOneUserRequest(RequestsSince(served, mark), 200);
            Assert.Null(app.Session.Services.Hosting.Account);
            AssertSignedOutSection(app, full: false);
            Assert.False(File.Exists(AccountsFile(data)));
            Assert.Equal(["probe"], app.Tokens.Calls);

            // A4 step 4: a token with a space, refused before any request (D298).
            mark = served.GitHub.Requests.Count;
            dialog = await app.OpenSignInDialogAsync();
            app.TypeToken(PartA.TokenWithSpace);
            app.PressKey(Key.Enter);
            app.Settle();
            var error = (TextBlock)app.FindByAutomationId(app.SignInDialogView(), "SignInError");
            Assert.Equal(PartA.NotAToken, error.Text);
            Assert.Equal(Dark.Danger, ColourOf(error));
            Assert.Empty(RequestsSince(served, mark));
            Assert.True(BoxOf(app).IsEffectivelyEnabled);
            Assert.True(BoxOf(app).Text == PartA.TokenWithSpace, "The box does not hold what was typed.");
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.Null(app.Session.Services.Hosting.Account);
            Assert.Equal(["probe"], app.Tokens.Calls);
            ShotA(app, PartA.A04NotAToken);

            // A4 step 5: an unknown token, refused by GitHub.
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.Type(PartA.UnknownToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            await app.WaitForAsync(() => FocusedId(app) == "SignInToken", "the focus back in the token box");
            var refused = Assert.Single(RequestsSince(served, mark));
            AssertUserRequest(refused, 401);
            Assert.Equal(PartA.BadCredentials, ((TextBlock)app.FindByAutomationId(app.SignInDialogView(), "SignInError")).Text);
            var box = BoxOf(app);
            Assert.True(box.IsEffectivelyEnabled);
            Assert.True(box.Text == PartA.UnknownToken, "The box did not keep the refused token.");
            Assert.True(box.SelectionStart == 0 && box.SelectionEnd == box.Text!.Length, "The refused token is not all selected.");
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.True(CancelOf(app).IsEffectivelyEnabled);
            Assert.Null(app.Session.Services.Hosting.Account);
            Assert.False(File.Exists(AccountsFile(data)));
            Assert.Equal(["probe"], app.Tokens.Calls);
            ShotA(app, PartA.A04Refused);

            // A7 step 6: the selection replaced by octo's token; the save fails (ruling 19).
            mark = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;
            app.Tokens.FailNextSave(TokenStoreReasons.Locked);
            app.Type(OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            AssertOneUserRequest(RequestsSince(served, mark), 200);
            view = app.SignInDialogView();
            var saveFailed = (TextBlock)app.FindByAutomationId(view, "SignInSaveFailed");
            Assert.Equal(PartA.SaveFailedDialogOcto, saveFailed.Text);
            Assert.Equal(Dark.Warning, ColourOf(saveFailed));
            Assert.False(BoxOf(app).IsEffectivelyEnabled);
            Assert.True(string.IsNullOrEmpty(BoxOf(app).Text), "The box was not cleared after the failed save.");
            Assert.Equal([PartA.Close], ButtonsOf(app, view));
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.DoesNotContain(TextsIn(app.Window), text => text.Contains("Try again", StringComparison.Ordinal));
            var account = AssertSignedIn(app.Session.Services.Hosting, TokenPlace.Session, AccountNoteKind.SaveFailed);
            Assert.Equal(PartA.Locked, account.Note);
            AssertRecord(data, served, useForGit: true, "session");
            Assert.Equal([$"save {PartA.OctoKey(port)}"], StoreCallsSince(app, stored));
            Assert.False(app.Tokens.Holds(OctoKey(served)));
            ShotA(app, PartA.A07SaveFailedDialog);

            // A7 step 7: Close; the section's note.
            app.Click(ConfirmOf(app));
            await app.WaitForAsync(() => dialog.IsClosed, "the dialog to close");
            await app.WaitForAsync(() => FocusedId(app) == "AccountsSignOutButton", "the focus on Sign out…");
            AssertSignedInSection(app, PartA.SaveFailedLocked, full: false);
            Assert.DoesNotContain(TextsIn(app.Window), text => text.Contains("Try again", StringComparison.Ordinal));
            ShotA(app, PartA.A07SaveFailedSection);

            // A7 step 8.
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            AssertSignedIn(app.Session.Services.Hosting, TokenPlace.Session, AccountNoteKind.SaveFailed);
        }
        finally
        {
            hold?.Dispose();
            app.Dispose();
        }

        AssertCleanEnd([app], data, served);
    }

    /// <summary>Method 4: A6 and A10 part A, two starts on one data folder with a store that has no keychain.</summary>
    [Fact]
    public async Task Check_A6_A10_session_only_and_its_restart()
    {
        var assembly = typeof(Phase7aChecksA).Assembly;
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a a6");
        var tokens = new FakeTokenStore { Available = false };
        var apps = new List<ShellDriver>();

        // A6, the first start.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                var app = await StartEmptyAsync(data, served, tokens);
                apps.Add(app);
                try
                {
                    // Step 1: the warning before anything is typed (D253).
                    var screen = await app.OpenAccountsAsync();
                    var dialog = await app.OpenSignInDialogAsync();
                    AssertDialog(app, screen, dialog, full: false, noKeychain: true);
                    var warning = (TextBlock)app.FindByAutomationId(app.SignInDialogView(), "SignInNoKeychain");
                    Assert.Equal(PartA.NoKeychain, warning.Text);
                    Assert.Equal(Dark.Warning, ColourOf(warning));
                    Assert.Equal(["probe"], tokens.Calls);
                    Assert.Empty(served.GitHub.Requests);
                    ShotA(app, PartA.A06NoKeychainDialog);

                    // Step 2: kept for the session; never saved.
                    app.TypeToken(OctoToken);
                    app.PressKey(Key.Enter);
                    await app.WaitForSignInCheckAsync(dialog);
                    Assert.True(dialog.IsClosed, "The dialog did not close: a sign-in without a keychain is no failed save.");
                    AssertOneUserRequest(served.GitHub.Requests, 200);
                    AssertSignedIn(app.Session.Services.Hosting, TokenPlace.Session, AccountNoteKind.NoKeychain);
                    await app.WaitForAsync(() => FocusedId(app) == "AccountsSignOutButton", "the focus on Sign out…");
                    AssertSignedInSection(app, PartA.NoKeychain, full: false);
                    AssertRecord(data, served, useForGit: true, "session");
                    Assert.Equal(["probe"], tokens.Calls);
                    Assert.False(tokens.Holds(OctoKey(served)));
                    ShotA(app, PartA.A06SessionOnly);
                }
                finally
                {
                    app.Dispose();
                }

                return true;
            },
            Cancelled);

        // A10 part A, the second start: a session record reads no store (D287).
        var requests = served.GitHub.Requests.Count;
        var stored = tokens.Calls.Count;
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                var app = await StartEmptyAsync(data, served, tokens);
                apps.Add(app);
                try
                {
                    Assert.Empty(StoreCallsSince(app, stored));
                    Assert.Empty(RequestsSince(served, requests));
                    AssertNeedsSignIn(app.Session.Services.Hosting, AccountNoteKind.SessionEnded);
                    await app.OpenAccountsAsync();
                    AssertNeedsSignInSection(app, PartA.SessionEnded);
                    AssertRecord(data, served, useForGit: true, "session");
                    ShotA(app, PartA.A10SessionEnded);
                    Assert.Empty(StoreCallsSince(app, stored));
                    Assert.Empty(RequestsSince(served, requests));
                }
                finally
                {
                    app.Dispose();
                }

                return true;
            },
            Cancelled);

        AssertCleanEnd(apps, data, served);
    }

    /// <summary>Method 5: A8, sign out with its confirmation, held and failed.</summary>
    [AvaloniaFact]
    public async Task Check_A8_sign_out()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a a8");
        var port = served.GitHub.Port;
        var app = await StartEmptyAsync(data, served);
        FakeTokenStoreHold? hold = null;
        try
        {
            // Step 1.
            await app.SignInAsync(OctoToken, Cancelled);
            var screen = await app.OpenAccountsAsync();
            var requests = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;

            // Step 2: the question, with the login and the primary "Sign out" (D291, D296).
            app.Click(app.FindByAutomationId("AccountsSignOutButton"));
            var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
            app.Settle();
            var view = app.TopDialogView!;
            Assert.Equal(PartA.SignOutTitle, ((TextBlock)app.FindByAutomationId(view, "ConfirmTitle")).Text);
            Assert.Equal(PartA.SignOutQuestionOcto, ((TextBlock)app.FindByAutomationId(view, "ConfirmQuestion")).Text);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Control>(), control => AutomationProperties.GetAutomationId(control) is "ConfirmDetail" or "DialogRepository" && control.IsEffectivelyVisible);
            Assert.Equal([PartA.Cancel, PartA.SignOutConfirm], ButtonsOf(app, view));
            Assert.False(question.IsDestructive);
            AssertPrimaryLook((Button)app.FindByAutomationId(view, "ConfirmButton"));
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);
            Assert.Empty(StoreCallsSince(app, stored));
            Assert.Empty(RequestsSince(served, requests));
            ShotA(app, PartA.A08Question);

            // Step 3: Cancel changes nothing.
            app.Click(app.FindByAutomationId(view, "CancelButton"));
            await app.WaitForAsync(() => question.IsClosed, "the question to close");
            AssertSignedInSection(app, PartA.InKeychain, full: false);
            AssertRecord(data, served, useForGit: true, "keychain");
            Assert.True(app.Tokens.Holds(OctoKey(served)));
            Assert.Empty(StoreCallsSince(app, stored));

            // Step 4: "Signing out…" while the keychain's delete is held.
            hold = app.Tokens.HoldNext();
            app.Click(app.FindByAutomationId("AccountsSignOutButton"));
            question = await app.WaitForDialogAsync<ConfirmationViewModel>();
            app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
            await hold.Arrived.WaitAsync(ShellDriver.DefaultWaitTimeout, Cancelled);
            app.Settle();
            var snapshot = app.AccountsSnapshot();
            Assert.Equal(PartA.Busy, snapshot.Field("AccountsBusy")?.Text);
            Assert.False(snapshot.Field("AccountsSignOutButton")?.IsEnabled);
            Assert.False(snapshot.Shows("AccountsSignInButton"));
            Assert.Equal([$"delete {PartA.OctoKey(port)}"], StoreCallsSince(app, stored));
            ShotA(app, PartA.A08SigningOut);

            // Step 5: signed out; the store and the file empty of the account; no avatars/ (D291).
            hold.Dispose();
            hold = null;
            await app.WaitForAccountAsync(account => account is null);
            await app.WaitForAsync(() => !screen.Accounts.SignOutCommand.IsRunning, "the sign-out to end");
            await app.WaitForAsync(() => FocusedId(app) == "AccountsSignInButton", "the focus on Sign in…");
            AssertSignedOutSection(app, full: false);
            Assert.Null(app.Session.Services.Hosting.Account);
            AssertNoRecord(data);
            Assert.False(Directory.Exists(Path.Combine(data.Path, "avatars")));
            Assert.False(app.Tokens.Holds(OctoKey(served)));
            Assert.Equal([$"delete {PartA.OctoKey(port)}"], StoreCallsSince(app, stored));
            Assert.Empty(RequestsSince(served, requests));
            ShotA(app, PartA.A08SignedOut);

            // Step 6: signed in again; a delete the keychain refuses (the report's ruling 1).
            requests = served.GitHub.Requests.Count;
            stored = app.Tokens.Calls.Count;
            var dialog = await app.OpenSignInDialogAsync();
            app.TypeToken(OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            await app.WaitForAccountAsync(account => account is { State: AccountState.SignedIn });
            app.Tokens.FailNextDelete(TokenStoreReasons.Locked);
            await app.SignOutAsync(confirm: true);
            await app.WaitForAsync(() => FocusedId(app) == "AccountsSignInButton", "the focus on Sign in…");
            AssertOneUserRequest(RequestsSince(served, requests), 200, againInThisSession: true);
            Assert.Equal([$"save {PartA.OctoKey(port)}", $"delete {PartA.OctoKey(port)}"], StoreCallsSince(app, stored));
            snapshot = app.AccountsSnapshot();
            Assert.Equal(PartA.SignedOut, snapshot.Field("AccountsSignedOut")?.Text);
            Assert.Equal(PartA.SignOutNoteLocked, snapshot.Field("AccountsSignOutNote")?.Text);
            Assert.Equal(Dark.Warning, ColourOf((TextBlock)app.FindByAutomationId("AccountsSignOutNote")));
            Assert.True(snapshot.Field("AccountsSignInButton")?.IsEnabled);
            Assert.True(
                app.BoundsOf(app.FindByAutomationId("AccountsSignOutNote")).Top > app.BoundsOf(app.FindByAutomationId("AccountsSignedOut")).Top
                && app.BoundsOf(app.FindByAutomationId("AccountsSignInButton")).Top > app.BoundsOf(app.FindByAutomationId("AccountsSignOutNote")).Top,
                "The note is not between \"Not signed in.\" and \"Sign in…\".");
            Assert.Null(app.Session.Services.Hosting.Account);
            AssertNoRecord(data);
            Assert.True(app.Tokens.Holds(OctoKey(served)));
            ShotA(app, PartA.A08SignOutNote);

            // Step 7: the note goes with the screen.
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            await app.OpenAccountsAsync();
            AssertSignedOutSection(app, full: false);
        }
        finally
        {
            hold?.Dispose();
            app.Dispose();
        }

        AssertCleanEnd([app], data, served);
    }

    /// <summary>Method 6: A9, needs sign-in after the API's refusal and after git's (D330), with WP-C's dialog.</summary>
    [AvaloniaFact]
    public async Task Check_A9_needs_sign_in_after_a_refused_token()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a a9");
        var port = served.GitHub.Port;
        var app = await StartEmptyAsync(data, served);
        try
        {
            // Step 1.
            await app.SignInAsync(OctoToken, Cancelled);

            // Step 2: the token revoked; hello's pull-request read gets 401.
            served.GitHub.Revoke(OctoToken);
            var requests = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;
            app.Folders.Answer(served.Hello.Path);
            app.Click(app.FindByAutomationId("OpenRepoButton"));
            await app.WaitForRepositoryAsync();
            await app.WaitForAccountAsync(account => account?.State == AccountState.NeedsSignIn);
            await WaitForRecordAsync(app, data, useForGit: true, "refused");
            await app.WaitForWatchersQuietAsync();
            var hello = app.Session.Shell.ActiveTab.Repository ?? throw new Xunit.Sdk.XunitException("hello's tab is not open.");
            var refusedReads = RequestsSince(served, requests);
            Assert.NotEmpty(refusedReads);
            Assert.All(refusedReads, request => Assert.True(request.TokenPresent && request.Status == 401, $"{request.Method} {request.Path}: {request.Status}, token {request.TokenPresent}"));
            Assert.Equal(("GET", "/api/v3/repos/octo/hello", "repo:octo/hello"), (refusedReads[0].Method, refusedReads[0].Path, refusedReads[0].Route));

            // None made after the account was marked: the refresh stops at the repository read's 401
            // (7.8 step 1), so it never goes on to the list, and nothing more arrives through steps 3
            // and 4 up to the next mark (no request without the account's token, 7.6).
            Assert.DoesNotContain(refusedReads, request => request.Path == "/api/v3/repos/octo/hello/pulls");
            var afterMarked = served.GitHub.Requests.Count;
            AssertNeedsSignIn(app.Session.Services.Hosting, AccountNoteKind.Refused);
            AssertRecord(data, served, useForGit: true, "refused");
            Assert.Empty(StoreCallsSince(app, stored));
            Assert.True(app.Tokens.Holds(OctoKey(served)));
            Assert.Equal(PullRequestsState.SignedOut, hello.Hosting.State.PullRequests);
            Assert.Empty(hello.Hosting.Bindings);
            Assert.Null(app.Session.Services.DialogHost.Current);

            // Step 3.
            var screen = await app.OpenAccountsAsync();
            AssertNeedsSignInSection(app, PartA.Refused);
            ShotA(app, PartA.A09NeedsSignInApi);
            Assert.Equal(afterMarked, served.GitHub.Requests.Count);

            // Step 4: signed in again with the classic token.
            var logMark = app.LogMark();
            requests = served.GitHub.Requests.Count;
            Assert.Equal(afterMarked, requests);
            stored = app.Tokens.Calls.Count;
            var dialog = await app.OpenSignInDialogAsync();
            app.TypeToken(OctoClassicToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            await app.WaitForAccountAsync(account => account is { State: AccountState.SignedIn });
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            await app.WaitForAsync(() => app.LogLinesSince(logMark).Any(line => line.Contains(PartA.HelloListed, StringComparison.Ordinal)), "hello's pull requests listed");
            await app.WaitForLogLineAsync(AppLogFile.AvatarsShown(Hello), mark: logMark);
            await app.WaitForWatchersQuietAsync();
            var signedIn = RequestsSince(served, requests);
            AssertUserRequest(signedIn[0], 200);
            Assert.Contains(signedIn, request => request.Path == "/api/v3/repos/octo/hello" && request.Status == 200);
            Assert.Contains(signedIn, request => request.Path == "/api/v3/repos/octo/hello/pulls" && request.Status == 200);
            Assert.DoesNotContain(signedIn, request => request.Status == 401);
            Assert.Equal([$"save {PartA.OctoKey(port)}"], StoreCallsSince(app, stored));
            AssertSignedIn(app.Session.Services.Hosting, TokenPlace.Keychain, AccountNoteKind.None);
            AssertRecord(data, served, useForGit: true, "keychain");
            AssertHelloBinding(hello, port);

            // Step 5: that token revoked; the fetch through the app's helper fails with WP-C's dialog.
            var remotesBefore = (await served.Hello.GitAsync("for-each-ref", "refs/remotes")).StandardOutput;
            served.GitHub.Revoke(OctoClassicToken);
            requests = served.GitHub.Requests.Count;
            var gitRequests = served.GitHub.GitRequests.Count;
            stored = app.Tokens.Calls.Count;
            app.Click(app.FindByAutomationId("FetchButton"));
            var errorDialog = await app.WaitForDialogAsync<ErrorDialogViewModel>();
            await app.WaitForAsync(() => RequestsSince(served, requests).Any(request => request.Path == "/api/v3/user" && request.Status == 401), "the session's check of git's erase");
            await app.WaitForAccountAsync(account => account?.State == AccountState.NeedsSignIn);
            await WaitForRecordAsync(app, data, useForGit: true, "refused");
            await app.WaitForIdleAsync();
            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, open => open is CredentialsViewModel or PromptDialogViewModel);
            AssertErrorDialog(
                app,
                errorDialog,
                PartA.FetchFailedTitle,
                PartA.GitRefusedOcto,
                Hello,
                PartA.AuthenticationFailed(port),
                PartA.CouldNotFetchOrigin,
                PartA.FetchCommand,
                ["Copy output", PartA.OpenSettingsButton, PartA.Close]);
            var gitSince = served.GitHub.GitRequests.Skip(gitRequests).Where(request => request.Path.Contains("/octo/hello", StringComparison.Ordinal)).ToList();
            Assert.NotEmpty(gitSince);
            Assert.All(gitSince, request => Assert.Equal(401, request.Status));
            Assert.Contains(gitSince, request => request.GivenUserName == "x-access-token");
            Assert.All(gitSince, request => Assert.True(request.GivenUserName is null or "x-access-token", $"git gave the user name {request.GivenUserName}."));
            AssertOneUserRequest(RequestsSince(served, requests), 401);
            AssertNeedsSignIn(app.Session.Services.Hosting, AccountNoteKind.Refused);
            AssertRecord(data, served, useForGit: true, "refused");
            Assert.Empty(StoreCallsSince(app, stored));
            Assert.Equal(remotesBefore, (await served.Hello.GitAsync("for-each-ref", "refs/remotes")).StandardOutput);
            ShotA(app, PartA.A09GitRefused);

            // Step 6: "Open settings…" opens Accounts.
            app.Click(app.FindByAutomationId(app.TopDialogView!, "ErrorExtraButton"));
            var reopened = await app.WaitForDialogAsync<SettingsDialogViewModel>();
            await app.WaitForSettingsReadsAsync(reopened);
            Assert.True(errorDialog.IsClosed);
            Assert.Equal(SettingsSection.Accounts, reopened.SelectedSection);
            Assert.Contains("selected", app.FindByAutomationId("SettingsNavAccounts").Classes);
            AssertNeedsSignInSection(app, PartA.Refused);
            ShotA(app, PartA.A09NeedsSignInGit);
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served);
    }

    /// <summary>Method 7: A10 part B, three starts on one data folder, one store and one fake.</summary>
    [Fact]
    public async Task Check_A10_restart_keeps_the_sign_in_and_finds_the_token_gone()
    {
        var assembly = typeof(Phase7aChecksA).Assembly;
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a a10");
        var tokens = new FakeTokenStore();
        var apps = new List<ShellDriver>();
        var key = PartA.OctoKey(served.GitHub.Port);

        // Step 5: the first start signs in.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                var app = await StartEmptyAsync(data, served, tokens);
                apps.Add(app);
                try
                {
                    await app.SignInAsync(OctoToken, Cancelled);
                }
                finally
                {
                    app.Dispose();
                }

                return true;
            },
            Cancelled);
        AssertRecord(data, served, useForGit: true, "keychain");
        Assert.True(tokens.Holds(OctoKey(served)));
        var file = await File.ReadAllBytesAsync(AccountsFile(data), Cancelled);

        // Step 6: the second start is signed in with one store read and no request (ruling 6, D287).
        var requests = served.GitHub.Requests.Count;
        var stored = tokens.Calls.Count;
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                var app = await StartEmptyAsync(data, served, tokens);
                apps.Add(app);
                try
                {
                    Assert.Equal(["probe", $"read {key}"], StoreCallsSince(app, stored));
                    Assert.Empty(RequestsSince(served, requests));
                    AssertSignedIn(app.Session.Services.Hosting, TokenPlace.Keychain, AccountNoteKind.None);
                    await app.OpenAccountsAsync();
                    Assert.Equal("SettingsNavAccounts", FocusedId(app));
                    AssertSignedInSection(app, PartA.InKeychain, full: false);
                    ShotA(app, PartA.A10StillSignedIn);
                    Assert.Empty(RequestsSince(served, requests));
                }
                finally
                {
                    app.Dispose();
                }

                return true;
            },
            Cancelled);
        Assert.Equal(file, await File.ReadAllBytesAsync(AccountsFile(data), Cancelled));

        // Step 7: the token taken away behind the app's back.
        tokens.Remove(OctoKey(served));
        requests = served.GitHub.Requests.Count;
        stored = tokens.Calls.Count;
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                var app = await StartEmptyAsync(data, served, tokens);
                apps.Add(app);
                try
                {
                    Assert.Equal(["probe", $"read {key}"], StoreCallsSince(app, stored));
                    Assert.Empty(RequestsSince(served, requests));
                    AssertNeedsSignIn(app.Session.Services.Hosting, AccountNoteKind.TokenMissing);
                    await app.OpenAccountsAsync();
                    AssertNeedsSignInSection(app, PartA.TokenMissing);
                    ShotA(app, PartA.A10TokenGone);
                    Assert.Empty(RequestsSince(served, requests));
                }
                finally
                {
                    app.Dispose();
                }

                return true;
            },
            Cancelled);
        Assert.Equal(file, await File.ReadAllBytesAsync(AccountsFile(data), Cancelled));

        AssertCleanEnd(apps, data, served);
    }

    /// <summary>Method 8: A11, the "Use for git over HTTPS" switch at Save, Cancel and Esc (D216, D255).</summary>
    [AvaloniaFact]
    public async Task Check_A11_use_for_git_switch()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7a a11");
        var port = served.GitHub.Port;
        var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path, null], activeTab: 0, gitHub: served.GitHub);
        try
        {
            // Step 1.
            var hello = await app.WaitForRepositoryAsync();
            var logMark = app.LogMark();
            await app.SignInAsync(OctoToken, Cancelled);
            await app.WaitForAsync(() => app.LogLinesSince(logMark).Any(line => line.Contains(PartA.HelloListed, StringComparison.Ordinal)), "hello's pull requests listed");
            await app.WaitForWatchersQuietAsync();
            var session = app.Session.Services.Hosting;
            await app.WaitForAsync(() => hello.Hosting.Bindings.Count == 1, "hello's binding");
            AssertHelloBinding(hello, port);
            Assert.True(session.BindsGit);
            AssertRecord(data, served, useForGit: true, "keychain");
            var stored = app.Tokens.Calls.Count;

            // Step 2: off and Save.
            var tabs = app.Window.GetVisualDescendants().OfType<Button>().Where(button => button.Name == "TabButton" && button.IsEffectivelyVisible).OrderBy(button => app.BoundsOf(button).X).ToList();
            app.Click(tabs[1]);
            await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository is null, "the empty tab");
            var screen = await app.OpenAccountsAsync();
            app.Click(app.FindByAutomationId("AccountUseForGit"));
            app.Settle();
            Assert.False(app.AccountsSnapshot().Field("AccountUseForGit")?.IsChecked);
            AssertRecord(data, served, useForGit: true, "keychain");
            AssertHelloBinding(hello, port);
            Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
            await WaitForRecordAsync(app, data, useForGit: false, "keychain");
            await app.WaitForAsync(() => hello.Hosting.Bindings.Count == 0, "hello's bindings to go");
            AssertRecord(data, served, useForGit: false, "keychain");
            Assert.Equal(AccountState.SignedIn, session.Account?.State);
            Assert.False(session.Account?.Account.UseForGit);
            Assert.False(session.BindsGit);
            Assert.Empty(StoreCallsSince(app, stored));

            // Step 3.
            screen = await app.OpenAccountsAsync();
            var snapshot = app.AccountsSnapshot();
            Assert.Equal(OctoLogin, snapshot.Field("AccountLogin")?.Text);
            Assert.Equal(PartA.InKeychain, snapshot.Field("AccountTokenPlace")?.Text);
            Assert.True(snapshot.Shows("AccountsSignOutButton"));
            Assert.Equal(PartA.UseForGit, snapshot.Field("AccountUseForGit")?.Text);
            Assert.False(snapshot.Field("AccountUseForGit")?.IsChecked);
            Assert.True(snapshot.Field("AccountUseForGit")?.IsEnabled);
            Assert.Equal(PartA.UseForGitNote, snapshot.Field("AccountUseForGitNote")?.Text);
            Assert.Equal([Hello, "New tab"], app.TabStrip().Tabs.Select(tab => tab.Title));
            ShotA(app, PartA.A11SwitchOff);

            // Step 4: Cancel and Esc after a toggle change nothing.
            app.Click(app.FindByAutomationId("AccountUseForGit"));
            app.CancelSettings();
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            AssertRecord(data, served, useForGit: false, "keychain");
            Assert.Empty(hello.Hosting.Bindings);
            screen = await app.OpenAccountsAsync();
            app.Click(app.FindByAutomationId("AccountUseForGit"));
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            AssertRecord(data, served, useForGit: false, "keychain");
            Assert.Empty(hello.Hosting.Bindings);
            screen = await app.OpenAccountsAsync();
            Assert.False(app.AccountsSnapshot().Field("AccountUseForGit")?.IsChecked);

            // Step 5: on and Save binds again.
            app.Click(app.FindByAutomationId("AccountUseForGit"));
            Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
            await WaitForRecordAsync(app, data, useForGit: true, "keychain");
            await app.WaitForAsync(() => hello.Hosting.Bindings.Count == 1, "hello's binding again");
            AssertRecord(data, served, useForGit: true, "keychain");
            AssertHelloBinding(hello, port);
            Assert.True(session.BindsGit);
            Assert.Empty(StoreCallsSince(app, stored));
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served);
    }

    // ---- Part A's assertions of a state ------------------------------------------------------------

    private static SecretTextBox BoxOf(ShellDriver app) => (SecretTextBox)app.FindByAutomationId(app.SignInDialogView(), "SignInToken");

    private static Button ConfirmOf(ShellDriver app) => (Button)app.FindByAutomationId(app.SignInDialogView(), "ConfirmButton");

    private static Button CancelOf(ShellDriver app) => (Button)app.FindByAutomationId(app.SignInDialogView(), "CancelButton");

    /// <summary>The box masked: one mask character per character of the token, the automation value empty, the name "Token", no text holding the token.</summary>
    private static void AssertMasked(ShellDriver app, string token, string tokenName)
    {
        var box = BoxOf(app);
        Assert.Equal(PartA.Mask, box.PasswordChar);
        Assert.True(box.Text == token, $"The box does not hold {tokenName}.");
        Assert.Equal(string.Empty, ValueOf(box) ?? string.Empty);
        Assert.Equal(PartA.TokenLabel, AutomationProperties.GetName(box));
        AssertNoTokenOnScreen(app);
    }

    private static AccountStatus AssertSignedIn(IHostingSession session, TokenPlace place, AccountNoteKind kind)
    {
        var account = session.Account ?? throw new Xunit.Sdk.XunitException("No account is signed in.");
        Assert.Equal((AccountState.SignedIn, place, kind), (account.State, account.Place, account.NoteKind));
        Assert.Equal(OctoLogin, account.Account.Login);
        Assert.Equal(OctoId, account.Account.Id);
        Assert.True(account.Account.UseForGit);
        return account;
    }

    private static void AssertNeedsSignIn(IHostingSession session, AccountNoteKind kind)
    {
        var account = session.Account ?? throw new Xunit.Sdk.XunitException("There is no account.");
        Assert.Equal((AccountState.NeedsSignIn, kind), (account.State, account.NoteKind));
        Assert.Equal(OctoLogin, account.Account.Login);
    }

    /// <summary>The section signed out: its introduction, "Not signed in." and "Sign in…" at their places; nothing of an account.</summary>
    private static void AssertSignedOutSection(ShellDriver app, bool full)
    {
        var snapshot = app.AccountsSnapshot();
        AssertShows(snapshot, ["AccountsIntro", "AccountsSignedOut", "AccountsSignInButton"], AccountIds);
        Assert.Equal(PartA.Intro, snapshot.Field("AccountsIntro")?.Text);
        Assert.Equal(PartA.SignedOut, snapshot.Field("AccountsSignedOut")?.Text);
        Assert.Equal(PartA.SignInButton, snapshot.Field("AccountsSignInButton")?.Text);
        Assert.True(snapshot.Field("AccountsSignInButton")?.IsEnabled);
        AssertSectionPlaces(app, full, "AccountsSignedOut");
        var signedOut = app.BoundsOf(app.FindByAutomationId("AccountsSignedOut"));
        var signIn = app.BoundsOf(app.FindByAutomationId("AccountsSignInButton"));
        Assert.Equal(PartA.ButtonsGap, signIn.Top - signedOut.Bottom, 2.0);
        Assert.Equal(full ? PartA.ControlsXFull : PartA.ControlsX, signIn.X, 2.0);
    }

    /// <summary>The section signed in: the login, where the token is, "Sign out…", and the switch on with its note.</summary>
    private static void AssertSignedInSection(ShellDriver app, string tokenPlace, bool full)
    {
        var snapshot = app.AccountsSnapshot();
        AssertShows(
            snapshot,
            ["AccountsIntro", "AccountLogin", "AccountTokenPlace", "AccountsSignOutButton", "AccountUseForGit", "AccountUseForGitNote"],
            ["AccountsSignedOut", "AccountNeedsSignIn", "AccountsSignOutNote", "AccountsSignInButton", "AccountsBusy", "AccountsOff"]);
        Assert.Equal(OctoLogin, snapshot.Field("AccountLogin")?.Text);
        Assert.Equal(FontWeight.SemiBold, ((TextBlock)app.FindByAutomationId("AccountLogin")).FontWeight);
        Assert.Equal(tokenPlace, snapshot.Field("AccountTokenPlace")?.Text);
        Assert.Contains("settingsNote", app.FindByAutomationId("AccountTokenPlace").Classes);
        Assert.Equal(PartA.SignOutButton, snapshot.Field("AccountsSignOutButton")?.Text);
        Assert.True(snapshot.Field("AccountsSignOutButton")?.IsEnabled);
        Assert.Equal(PartA.UseForGit, snapshot.Field("AccountUseForGit")?.Text);
        Assert.True(snapshot.Field("AccountUseForGit")?.IsChecked);
        Assert.Equal(PartA.UseForGitNote, snapshot.Field("AccountUseForGitNote")?.Text);
        AssertSectionPlaces(app, full, "AccountLogin");
        AssertGitRowLabel(app);
    }

    /// <summary>The section needing sign-in: the login, why, both buttons enabled, the switch on.</summary>
    private static void AssertNeedsSignInSection(ShellDriver app, string why)
    {
        var snapshot = app.AccountsSnapshot();
        AssertShows(
            snapshot,
            ["AccountLogin", "AccountNeedsSignIn", "AccountsSignInButton", "AccountsSignOutButton", "AccountUseForGit", "AccountUseForGitNote"],
            ["AccountsSignedOut", "AccountTokenPlace", "AccountsSignOutNote", "AccountsBusy", "AccountsOff"]);
        Assert.Equal(OctoLogin, snapshot.Field("AccountLogin")?.Text);
        Assert.Equal(why, snapshot.Field("AccountNeedsSignIn")?.Text);
        Assert.Equal(PaletteOf(app).Warning, ColourOf((TextBlock)app.FindByAutomationId("AccountNeedsSignIn")));
        Assert.True(snapshot.Field("AccountsSignInButton")?.IsEnabled);
        Assert.True(snapshot.Field("AccountsSignOutButton")?.IsEnabled);
        Assert.True(snapshot.Field("AccountUseForGit")?.IsChecked);
        Assert.Equal(PartA.UseForGitNote, snapshot.Field("AccountUseForGitNote")?.Text);
        AssertSectionPlaces(app, full: false, "AccountLogin");
    }

    /// <summary>
    /// The section's places (8.3): the introduction from the section's x, secondary; 18 below it the
    /// "GitHub" label in the label column and the row's first text from the controls' x; every
    /// shown control of the section within the controls' right edge; no scroll bar.
    /// </summary>
    private static void AssertSectionPlaces(ShellDriver app, bool full, string firstOfRow)
    {
        var intro = (TextBlock)app.FindByAutomationId("AccountsIntro");
        var introBounds = app.BoundsOf(intro);
        Assert.Equal(full ? PartA.SectionXFull : PartA.SectionX, introBounds.X, 2.0);
        Assert.Equal(PaletteOf(app).TextSecondary, ColourOf(intro));
        var section = app.FindByAutomationId("SettingsAccounts");
        var label = section.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == PartA.GitHubLabel && block.IsEffectivelyVisible);
        var labelBounds = app.BoundsOf(label);
        Assert.Equal(introBounds.X, labelBounds.X, 2.0);
        Assert.True(labelBounds.Right <= introBounds.X + (PartA.LabelRight - PartA.SectionX) + 2, $"The label \"GitHub\" ends at {labelBounds.Right}.");
        Assert.Equal(PartA.RowSpacing, labelBounds.Top - introBounds.Bottom, 2.0);
        var first = app.BoundsOf(app.FindByAutomationId(firstOfRow));
        Assert.Equal(full ? PartA.ControlsXFull : PartA.ControlsX, first.X, 2.0);
        if (!full)
        {
            foreach (var control in section.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) is { Length: > 0 } id && id.StartsWith("Account", StringComparison.Ordinal)))
            {
                var bounds = app.BoundsOf(control);
                Assert.True(bounds.Right <= PartA.ControlsRight + 2, $"{AutomationProperties.GetAutomationId(control)} ends at {bounds.Right}, past {PartA.ControlsRight}.");
            }
        }

        var scroller = (ScrollViewer)app.FindByAutomationId("SettingsSection");
        Assert.True(scroller.Extent.Height <= scroller.Viewport.Height + 0.5, "The section scrolls.");
    }

    /// <summary>The second row's label "Git", in the label column.</summary>
    private static void AssertGitRowLabel(ShellDriver app)
    {
        var section = app.FindByAutomationId("SettingsAccounts");
        var label = section.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(block => block.Text == PartA.GitLabel && block.IsEffectivelyVisible);
        Assert.NotNull(label);
        Assert.Equal(app.BoundsOf(app.FindByAutomationId("AccountsIntro")).X, app.BoundsOf(label).X, 2.0);
    }

    /// <summary>Cancel and Save at 1920×1080: y 827 to 859.</summary>
    private static void AssertSaveButtonsFull(ShellDriver app)
    {
        var card = CardNamed(app, "SettingsCard");
        foreach (var id in new[] { "CancelButton", "ConfirmButton" })
        {
            var bounds = app.BoundsOf(app.FindByAutomationId(card, id));
            Assert.Equal(PartA.ButtonsFull.Top, bounds.Top, 2.0);
            Assert.Equal(PartA.ButtonsFull.Bottom, bounds.Bottom, 2.0);
        }
    }

    /// <summary>
    /// "Add GitHub account" as it opens (A2 step 3): stacked over the screen on Accounts, its card 400
    /// wide and centred vertically, its parts top to bottom, the box empty with the focus, Sign in
    /// disabled, Cancel enabled, no "In …" line; the no-keychain warning only when asked for.
    /// </summary>
    private static void AssertDialog(ShellDriver app, SettingsDialogViewModel screen, SignInDialogViewModel dialog, bool full, bool noKeychain = false)
    {
        var host = app.Session.Services.DialogHost;
        Assert.Equal([screen, dialog], host.Open.Cast<DialogViewModel>());
        Assert.Same(dialog, host.Current);
        Assert.Equal(SettingsSection.Accounts, screen.SelectedSection);
        var view = app.SignInDialogView();
        Assert.Equal(PartA.DialogTitle, AutomationProperties.GetName(view));
        var card = app.BoundsOf(CardNamed(app, "SignInCard"));
        var (left, right) = full ? PartA.DialogCardFull : PartA.DialogCard;
        Assert.Equal(left, card.Left, 2.0);
        Assert.Equal(right, card.Right, 2.0);
        Assert.Equal(app.Window.ClientSize.Height / 2, card.Center.Y, 2.0);
        Assert.Equal(PartA.DialogTitle, ((TextBlock)app.FindByAutomationId(view, "DialogTitle")).Text);
        var help = (TextBlock)app.FindByAutomationId(view, "SignInHelp");
        Assert.Equal(PartA.Help, help.Text);
        var link = (Button)app.FindByAutomationId(view, "SignInTokenLink");
        Assert.Equal(PartA.TokenLink, link.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.Equal(PaletteOf(app).Accent, ColourOf(link.GetVisualDescendants().OfType<TextBlock>().Single()));
        Assert.Equal(PartA.LinkGap, app.BoundsOf(link).Top - app.BoundsOf(help).Bottom, 2.0);
        var label = view.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == PartA.TokenLabel);
        var box = BoxOf(app);
        Assert.True(app.BoundsOf(label).Top > app.BoundsOf(link).Bottom && app.BoundsOf(box).Top > app.BoundsOf(label).Bottom, "The label and the box are not below the link in order.");
        Assert.True(string.IsNullOrEmpty(box.Text), "The box is not empty.");
        Assert.Equal("SignInToken", FocusedId(app));
        var shown = view.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible).Select(AutomationProperties.GetAutomationId).ToHashSet();
        Assert.Equal(noKeychain, shown.Contains("SignInNoKeychain"));
        Assert.DoesNotContain("SignInChecking", shown);
        Assert.DoesNotContain("SignInError", shown);
        Assert.DoesNotContain("SignInSaveFailed", shown);
        Assert.DoesNotContain("DialogRepository", shown);
        Assert.Equal([PartA.Cancel, PartA.SignIn], ButtonsOf(app, view));
        Assert.True(CancelOf(app).IsEffectivelyEnabled);
        Assert.False(ConfirmOf(app).IsEffectivelyEnabled);
        AssertPrimaryLook(ConfirmOf(app));
        Assert.True(app.BoundsOf(ConfirmOf(app)).Right > app.BoundsOf(CancelOf(app)).Right && app.BoundsOf(ConfirmOf(app)).Right >= card.Right - 22, "The buttons are not right-aligned.");
    }

    /// <summary>A button drawn in the primary look (<c>VcPrimaryButton</c>), not the danger one (D296).</summary>
    private static void AssertPrimaryLook(Button button)
    {
        Assert.Same(Application.Current!.FindResource("VcPrimaryButton"), button.Theme);
        Assert.DoesNotContain("danger", button.Classes);
    }

    /// <summary>The scenario facts' one binding of hello: its URL and octo's account (D278).</summary>
    private static void AssertHelloBinding(RepositoryViewModel hello, int port)
    {
        var binding = Assert.Single(hello.Hosting.Bindings);
        Assert.Equal(PartA.HelloUrl(port), binding.Url);
        Assert.Equal(PartA.OctoKey(port), binding.Account.Id);
    }

    /// <summary>Whether two folder paths name one folder (the call log's working directory as git was given it).</summary>
    private static bool SameFolder(string? left, string right) =>
        left is not null && string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
