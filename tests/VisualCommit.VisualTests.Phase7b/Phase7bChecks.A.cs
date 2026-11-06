using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part A, accounts and sign-in (docs/test-reports/phase-7b.md, checks A1 to A14; design 10.7): the
/// Accounts section's three blocks, each provider's dialog in each state, the window's entry with
/// Azure DevOps' prefill, session only and its restart, one sign-out per provider and each switch
/// saved alone. One method per app start (the report's part A methods table); every method ends
/// with the tripwire and the fakes' clean end.
/// </summary>
public sealed partial class Phase7bChecksA : Phase7bChecks
{
    private static readonly HostingProvider[] Three = [HostingProvider.GitHub, HostingProvider.GitLab, HostingProvider.AzureDevOps];

    /// <summary>Method 1: A1 and A2 on one empty tab with the three fakes.</summary>
    [AvaloniaFact]
    public async Task Check_A1_A2_signed_out_to_three_signed_in()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b a1");
        var app = await StartEmptyAsync(data, served);
        try
        {
            // A1 step 1: the three on, nobody signed in, nothing asked (D257, D350).
            var session = app.Session.Services.Hosting;
            Assert.False(session.IsOff);
            Assert.Equal(Three, session.Providers);
            Assert.All(Three, provider => Assert.True(session.IsOn(provider)));
            Assert.All(Three, provider => Assert.Null(session.AccountOf(provider)));
            AssertStartLines(app, served, PartA.StartLines);
            AssertNoRequests(served, default);
            Assert.Empty(app.Tokens.Calls);
            Assert.False(File.Exists(AccountsFile(data)));
            var quiet = await app.QuietCallMarkAsync();

            // A1 step 2: the section, its intro measured first (10.9).
            var screen = await app.OpenAccountsAsync();
            Assert.Equal(SettingsSection.Accounts, screen.SelectedSection);
            AssertFrame(app);
            Assert.Equal("SettingsNavAccounts", FocusedId(app));
            AssertIntro(app);
            foreach (var provider in Three)
            {
                AssertSignedOutBlock(app, provider);
            }

            AssertBlockGaps(app, Three);
            AssertFits(app);
            ShotA(app, PartA.A01ThreeSignedOut);
            var calls = await app.QuietCallsSinceAsync(quiet);
            Assert.Equal(["git config --show-scope --show-origin -z --list"], calls.Select(call => call.CommandText));
            Assert.True(SameFolder(calls[0].WorkingDirectory, data.Path), $"The screen's read ran in {calls[0].WorkingDirectory}.");
            AssertNoRequests(served, default);
            Assert.Empty(app.Tokens.Calls);

            // A2 step 3: GitHub's 7a's dialog and block.
            var mark = Mark(served);
            var dialog = await app.OpenSignInDialogAsync(HostingProvider.GitHub);
            AssertDialog(app, HostingProvider.GitHub, string.Empty);
            Assert.Equal("SignInToken", FocusedId(app));
            app.TypeToken(GitHubToken);
            await app.ConfirmSignInAsync();
            Assert.True(dialog.IsClosed, "GitHub's dialog did not close after the sign-in.");
            Assert.Equal(["probe", "save " + PartA.GitHubKey(served.GitHub.Port)], app.Tokens.Calls);
            AssertGitHubUser(GitHubSince(served, mark), 200, OctoLogin);
            Assert.Empty(GitLabSince(served, mark));
            Assert.Empty(AzureSince(served, mark));
            AssertAccount(app, served, HostingProvider.GitHub, TokenPlace.Keychain, AccountNoteKind.None);
            Assert.Null(session.AccountOf(HostingProvider.GitLab));
            Assert.Null(session.AccountOf(HostingProvider.AzureDevOps));
            await WaitForFocusAsync(app, "AccountsSignOutButton");
            AssertSignedInBlock(app, HostingProvider.GitHub, PartA.InKeychain);
            AssertSignedOutBlock(app, HostingProvider.GitLab);
            AssertSignedOutBlock(app, HostingProvider.AzureDevOps);
            AssertBlockGaps(app, Three);
            AssertFits(app);
            ShotA(app, PartA.A02GitHubSignedIn);

            // A2 step 4: GitLab's.
            mark = Mark(served);
            var calledMark = app.Tokens.Calls.Count;
            dialog = await app.OpenSignInDialogAsync(HostingProvider.GitLab);
            Assert.Equal(PartA.GitLabDialogTitle, dialog.Title);
            Assert.Equal("SignInToken", FocusedId(app));
            app.TypeToken(GitLabToken);
            await app.ConfirmSignInAsync();
            Assert.True(dialog.IsClosed, "GitLab's dialog did not close after the sign-in.");
            Assert.Equal(["save " + PartA.GitLabKey(served.GitLab.Port)], CallsSince(app.Tokens, calledMark));
            AssertGitLabUser(GitLabSince(served, mark), 200, AdaLogin);
            Assert.Empty(GitHubSince(served, mark));
            Assert.Empty(AzureSince(served, mark));
            AssertAccount(app, served, HostingProvider.GitLab, TokenPlace.Keychain, AccountNoteKind.None);
            await WaitForFocusAsync(app, "AccountsSignOutButtonGitLab");
            AssertSignedInBlock(app, HostingProvider.GitHub, PartA.InKeychain);
            AssertSignedInBlock(app, HostingProvider.GitLab, PartA.InKeychain);
            AssertSignedOutBlock(app, HostingProvider.AzureDevOps);
            AssertBlockGaps(app, Three);
            AssertFits(app);
            ShotA(app, PartA.A02GitLabSignedIn);

            // A2 step 5: Azure DevOps', the organization typed in lower case.
            mark = Mark(served);
            calledMark = app.Tokens.Calls.Count;
            dialog = await app.OpenSignInDialogAsync(HostingProvider.AzureDevOps);
            Assert.Equal(PartA.AzureDevOpsDialogTitle, dialog.Title);
            Assert.Equal(string.Empty, dialog.Organization);
            Assert.Equal(PartA.FocusOpenedEmpty, FocusedId(app));
            app.TypeOrganization(PartA.OrganizationLower);
            app.Click(Find(app, "SignInToken"));
            app.TypeToken(AzureDevOpsToken);
            await app.ConfirmSignInAsync();
            Assert.True(dialog.IsClosed, "Azure DevOps' dialog did not close after the sign-in.");
            Assert.Equal(["save " + PartA.AzureDevOpsKey(served.AzureDevOps.Port)], CallsSince(app.Tokens, calledMark));
            AssertAzureSignIn(served, AzureSince(served, mark), PartA.OrganizationLower);
            Assert.Empty(GitHubSince(served, mark));
            Assert.Empty(GitLabSince(served, mark));
            AssertAccount(app, served, HostingProvider.AzureDevOps, TokenPlace.Keychain, AccountNoteKind.None);
            await WaitForFocusAsync(app, "AccountsSignOutButtonAzureDevOps");
            Assert.True(WhollyInView(app, InBlock(app, HostingProvider.AzureDevOps, "AccountsSignOutButton")), "Azure DevOps' Sign out… is not in view.");
            var signedIn = Mark(served);
            var signedInCalls = app.Tokens.Calls.Count;

            // A2 step 6: the top; the section scrolls with three.
            WheelUntilStill(app, up: true);
            var scroller = Scroller(app);
            Assert.Equal(0, scroller.Offset.Y, 0.5);
            Assert.True(scroller.Extent.Height > scroller.Viewport.Height, "Three signed-in blocks do not scroll.");
            foreach (var provider in Three)
            {
                AssertSignedInBlock(app, provider, PartA.InKeychain);
            }

            ShotA(app, PartA.A02ThreeTop);

            // A2 step 7: the end.
            WheelUntilStill(app, up: false);
            AssertAtEnd(app);
            Assert.True(WhollyInView(app, InBlock(app, HostingProvider.AzureDevOps, "AccountUseForGitNote")), "Azure DevOps' note is not in view at the end.");
            Assert.True(WhollyInView(app, app.AccountsBlock(HostingProvider.AzureDevOps)), "Azure DevOps' block is not wholly in view at the end.");
            Assert.False(WhollyInView(app, Find(app, "AccountsIntro")), "The intro is still in view at the end.");
            Assert.True(TopScrolledOut(app, Find(app, "AccountsIntro")), "The intro's top is not scrolled out of view at the end.");
            Assert.True(TopScrolledOut(app, app.AccountsBlock(HostingProvider.GitHub)), "The top of GitHub's block is not scrolled out of view at the end.");
            Assert.Equal("AccountsSignOutButtonAzureDevOps", FocusedId(app));
            ShotA(app, PartA.A02ThreeEnd);
            AssertNoRequests(served, signedIn);
            Assert.Empty(CallsSince(app.Tokens, signedInCalls));

            // A2 step 8: Esc; the three stay (D256).
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            Assert.All(Three, provider => Assert.Equal(AccountState.SignedIn, session.AccountOf(provider)?.State));
            await WaitForFileAsync(app, data, file => RecordsOf(file).Count == 3, "accounts.json's three records");
            var accounts = ReadAccounts(data);
            Assert.Equal(1, accounts!.Value.GetProperty("version").GetInt32());
            foreach (var provider in Three)
            {
                AssertRecord(accounts, served, provider, useForGit: true, "keychain");
            }

            AssertNoAddress(data);
            Assert.True(app.Tokens.Holds(new("github", PartA.Host(served.GitHub.Port), OctoLogin)));
            Assert.True(app.Tokens.Holds(new("gitlab", PartA.Host(served.GitLab.Port), AdaLogin)));
            Assert.True(app.Tokens.Holds(new("azure-devops", PartA.Host(served.AzureDevOps.Port), PartA.OrganizationLower)));
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served);
    }

    /// <summary>Method 2: A3 with GitLab off (no fake of it given, D362).</summary>
    [AvaloniaFact]
    public async Task Check_A3_gitlab_off()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b a3a");
        var app = ShellDriver.Start(data.Path, gitHub: served.GitHub, azureDevOps: served.AzureDevOps);
        try
        {
            await app.WaitUntilReadyAsync();
            app.MoveToNeutral();
            var session = app.Session.Services.Hosting;
            Assert.Equal([HostingProvider.GitHub, HostingProvider.AzureDevOps], session.Providers);
            Assert.False(session.IsOn(HostingProvider.GitLab));
            Assert.Null(session.ProviderOf(HostingProvider.GitLab));
            AssertStartLines(app, served, PartA.StartLinesGitLabOff);
            AssertNoRequests(served, default);
            Assert.Empty(app.Tokens.Calls);

            await app.OpenAccountsAsync();
            AssertIntro(app);
            AssertSignedOutBlock(app, HostingProvider.GitHub);
            AssertSignedOutBlock(app, HostingProvider.AzureDevOps);
            Assert.False(app.AccountsBlock(HostingProvider.GitLab).IsEffectivelyVisible);
            Assert.Equal(0, app.BoundsOf(app.AccountsBlock(HostingProvider.GitLab)).Height, 0.5);
            Assert.DoesNotContain(
                app.Window.GetVisualDescendants().OfType<Control>(),
                control => AutomationProperties.GetAutomationId(control) is { } id && id.EndsWith("GitLab", StringComparison.Ordinal) && control.IsEffectivelyVisible);
            AssertBlockGaps(app, HostingProvider.GitHub, HostingProvider.AzureDevOps);
            AssertFits(app);
            ShotA(app, PartA.A03GitLabOff);
            AssertNoRequests(served, default);
            Assert.Empty(app.Tokens.Calls);
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served);
    }

    /// <summary>Method 3: A3 with the three off: GitHub's block alone with 7a's off text.</summary>
    [AvaloniaFact]
    public async Task Check_A3_three_off()
    {
        using var data = new TempDirectory("phase7b a3b");
        var app = ShellDriver.Start(data.Path);
        try
        {
            await app.WaitUntilReadyAsync();
            app.MoveToNeutral();
            var session = app.Session.Services.Hosting;
            Assert.True(session.IsOff);
            Assert.Empty(session.Providers);

            await app.OpenAccountsAsync();
            AssertIntro(app);
            AssertBlockShows(app, HostingProvider.GitHub, "AccountsOff");
            var off = InBlock(app, HostingProvider.GitHub, "AccountsOff");
            Assert.Equal(PartA.Off, ((TextBlock)off).Text);
            Assert.Equal(PartA.ControlsX, app.BoundsOf(off).X, PartA.Tolerance);
            Assert.False(app.AccountsBlock(HostingProvider.GitLab).IsEffectivelyVisible);
            Assert.False(app.AccountsBlock(HostingProvider.AzureDevOps).IsEffectivelyVisible);
            ShotA(app, PartA.A03ThreeOff);
            Assert.Empty(app.Tokens.Calls);
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, null);
    }

    /// <summary>Method 4: A5, A11 (GitLab's and GitHub's dialogs) and A8 (GitLab's) on one empty tab.</summary>
    [AvaloniaFact]
    public async Task Check_A5_A8_A11_gitlab_dialog_links_and_refusals()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b a5");
        var app = await StartEmptyAsync(data, served);
        try
        {
            var session = app.Session.Services.Hosting;
            var screen = await app.OpenAccountsAsync();
            var mark = Mark(served);

            // A5 step 2: the dialog empty.
            var dialog = await app.OpenSignInDialogAsync(HostingProvider.GitLab);
            Assert.False(screen.IsClosed);
            Assert.Equal(SettingsSection.Accounts, screen.SelectedSection);
            AssertDialog(app, HostingProvider.GitLab, string.Empty);
            Assert.Equal("SignInToken", FocusedId(app));
            Assert.False(ConfirmOf(app).IsEffectivelyEnabled);
            foreach (var provider in Three)
            {
                // Behind the dialog's backdrop, the three blocks still signed out.
                AssertSignedOutBlock(app, provider, behindDialog: true);
            }

            Assert.Equal(["probe"], app.Tokens.Calls);
            AssertNoRequests(served, mark);
            ShotA(app, PartA.A05GitLabDialog);

            // A5 step 3: the token masked (R).
            app.TypeToken(GitLabToken);
            AssertMasked(app, GitLabToken);
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.Empty(app.Launcher.Requests);
            ShotA(app, PartA.A05GitLabTokenMasked);

            // A5 steps 4 and 5: both links' tooltips and launches.
            var link = Find(app, "SignInTokenLink");
            Assert.Equal(PartA.GitLabTokenLinkTip, await app.RestPointerOnAsync(link));
            app.Click(link);
            await app.WaitForAsync(() => app.Launcher.Requests.Count == 1, "the fine-grained form's launch");
            Assert.Equal((FileLaunchKind.OpenUrl, PartA.GitLabTokenPage(served.GitLab.Port)), (app.Launcher.Requests[0].Kind, app.Launcher.Requests[0].FullPath));
            Assert.False(dialog.IsClosed);
            AssertMasked(app, GitLabToken);
            var legacy = Find(app, "SignInLegacyTokenLink");
            Assert.Equal(PartA.GitLabLegacyLinkTip, await app.RestPointerOnAsync(legacy));
            app.Click(legacy);
            await app.WaitForAsync(() => app.Launcher.Requests.Count == 2, "the legacy form's launch");
            Assert.Equal((FileLaunchKind.OpenUrl, PartA.GitLabLegacyTokenPage(served.GitLab.Port)), (app.Launcher.Requests[1].Kind, app.Launcher.Requests[1].FullPath));
            Assert.False(dialog.IsClosed);
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            AssertNoRequests(served, mark);
            Assert.Equal(["probe"], app.Tokens.Calls);

            // A11 step 6: Azure DevOps' form in GitLab's dialog, refused unsent (D378).
            Replace(app, "SignInToken", PartA.AzureDevOpsForm);
            app.PressKey(Key.Enter);
            app.Settle();
            AssertError(app, PartA.GitLabGivenAzureDevOpsToken);
            AssertNoRequests(served, mark);
            Assert.Equal(PartA.AzureDevOpsForm, dialog.Token);
            Assert.True(Find(app, "SignInToken").IsEffectivelyEnabled);
            Assert.Equal("SignInToken", FocusedId(app));
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.Null(session.AccountOf(HostingProvider.GitLab));
            ShotA(app, PartA.A11GitLabAzureToken);

            // A8 step 7: a glpat- token GitLab never issued.
            app.PressKey(Key.A, RawInputModifiers.Control);
            app.Type(PartA.UnknownGitLabToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            AssertGitLabUser(GitLabSince(served, mark), 401, null);
            AssertError(app, PartA.GitLabRefused401);
            Assert.Equal(PartA.Danger, ColourOf(Find(app, "SignInError")));
            AssertSelected(app, "SignInToken", PartA.UnknownGitLabToken);
            AssertDialogButtonsEnabled(app);
            Assert.Null(session.AccountOf(HostingProvider.GitLab));
            Assert.False(File.Exists(AccountsFile(data)));
            Assert.Equal(["probe"], app.Tokens.Calls);
            ShotA(app, PartA.A08GitLabRefused);

            // A8 step 8: the legacy token signs in (D345).
            mark = Mark(served);
            app.Type(GitLabLegacyToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            Assert.True(dialog.IsClosed, "GitLab's dialog did not close after the legacy token's sign-in.");
            AssertGitLabUser(GitLabSince(served, mark), 200, AdaLogin);
            Assert.Equal(["probe", "save " + PartA.GitLabKey(served.GitLab.Port)], app.Tokens.Calls);
            AssertAccount(app, served, HostingProvider.GitLab, TokenPlace.Keychain, AccountNoteKind.None);
            await WaitForFocusAsync(app, "AccountsSignOutButtonGitLab");
            AssertSignedInBlock(app, HostingProvider.GitLab, PartA.InKeychain);
            await WaitForFileAsync(app, data, file => RecordsOf(file).Count == 1, "GitLab's record");
            AssertRecord(ReadAccounts(data), served, HostingProvider.GitLab, useForGit: true, "keychain");

            // A11 step 9: GitLab's form in GitHub's dialog.
            mark = Mark(served);
            var gitHub = await app.OpenSignInDialogAsync(HostingProvider.GitHub);
            app.TypeToken(GitLabToken);
            app.PressKey(Key.Enter);
            app.Settle();
            Assert.Equal(PartA.GitHubDialogTitle, gitHub.Title);
            AssertError(app, PartA.GitHubGivenGitLabToken);
            AssertNoRequests(served, mark);
            Assert.Null(session.AccountOf(HostingProvider.GitHub));

            // A11 step 10: Esc closes the dialog only.
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => gitHub.IsClosed, "GitHub's dialog to close");
            Assert.False(screen.IsClosed);
            AssertSignedOutBlock(app, HostingProvider.GitHub);
            AssertSignedInBlock(app, HostingProvider.GitLab, PartA.InKeychain);
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served, launches: 2);
    }

    /// <summary>Method 5: A6 (empty, typed), A11, A8, A9, A10, A7 and A4 in Azure DevOps' dialog.</summary>
    [AvaloniaFact]
    public async Task Check_A6_to_A11_and_A4_azure_dialog()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b a6");
        var app = await StartEmptyAsync(data, served);
        try
        {
            var session = app.Session.Services.Hosting;
            var screen = await app.OpenAccountsAsync();
            var mark = Mark(served);

            // A6 step 2: empty, the organization box focused, the link disabled.
            var dialog = await app.OpenSignInDialogAsync(HostingProvider.AzureDevOps);
            Assert.False(screen.IsClosed);
            AssertDialog(app, HostingProvider.AzureDevOps, string.Empty);
            Assert.True(app.BoundsOf(CardNamed(app, "SignInCard")).Height <= PartA.AzureDevOpsCardMaxHeight, "Azure DevOps' card is over 345 high.");
            Assert.Equal(PartA.FocusOpenedEmpty, FocusedId(app));
            Assert.False(Find(app, "SignInTokenLink").IsEffectivelyEnabled);
            Assert.False(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.Equal(["probe"], app.Tokens.Calls);
            AssertNoRequests(served, mark);
            ShotA(app, PartA.A06AzureDialogEmpty);

            // A6 step 3: the organization typed and the token masked (R).
            app.TypeOrganization(PartA.Organization);
            app.Click(Find(app, "SignInToken"));
            app.TypeToken(AzureDevOpsToken);
            Assert.Equal(PartA.Organization, ((TextBox)Find(app, "SignInOrganization")).Text);
            Assert.True(Find(app, "SignInTokenLink").IsEffectivelyEnabled);
            AssertMasked(app, AzureDevOpsToken);
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.Empty(app.Launcher.Requests);
            Assert.True(app.BoundsOf(CardNamed(app, "SignInCard")).Height <= PartA.AzureDevOpsCardMaxHeight, "Azure DevOps' card is over 345 high.");
            ShotA(app, PartA.A06AzureTokenMasked);

            // A6 step 4: the organization's page for tokens.
            var link = Find(app, "SignInTokenLink");
            Assert.Equal(PartA.AzureDevOpsTokenLinkTip, await app.RestPointerOnAsync(link));
            app.Click(link);
            await app.WaitForAsync(() => app.Launcher.Requests.Count == 1, "the token page's launch");
            Assert.Equal((FileLaunchKind.OpenUrl, PartA.AzureDevOpsTokenPage(served.AzureDevOps.Port)), (app.Launcher.Requests[0].Kind, app.Launcher.Requests[0].FullPath));
            Assert.False(dialog.IsClosed);
            Assert.Equal(PartA.Organization, ((TextBox)Find(app, "SignInOrganization")).Text);
            AssertMasked(app, AzureDevOpsToken);
            AssertNoRequests(served, mark);

            // A11 step 5: an organization outside the grammar, refused unsent.
            Replace(app, "SignInOrganization", PartA.NotAnOrganization);
            app.Click(ConfirmOf(app));
            app.Settle();
            AssertError(app, PartA.NotAnOrganizationError);
            AssertNoRequests(served, mark);
            Assert.Equal(PartA.NotAnOrganization, ((TextBox)Find(app, "SignInOrganization")).Text);
            Assert.False(Find(app, "SignInTokenLink").IsEffectivelyEnabled);
            AssertMasked(app, AzureDevOpsToken);
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            ShotA(app, PartA.A11AzureNotAnOrganization);

            // A11 step 6: GitHub's form in Azure DevOps' dialog.
            Replace(app, "SignInOrganization", PartA.Organization);
            Replace(app, "SignInToken", GitHubToken);
            app.PressKey(Key.Enter);
            app.Settle();
            AssertError(app, PartA.AzureDevOpsGivenGitHubToken);
            AssertNoRequests(served, mark);
            Assert.True(Find(app, "SignInTokenLink").IsEffectivelyEnabled);
            Assert.Equal(GitHubToken, dialog.Token);
            AssertMasked(app, GitHubToken);
            Assert.True(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.Equal("SignInToken", FocusedId(app));
            Assert.Null(session.AccountOf(HostingProvider.AzureDevOps));
            ShotA(app, PartA.A11AzureGitHubToken);

            // A8 step 7: a PAT-shaped value the fake never issued: the empty 401.
            Replace(app, "SignInToken", PartA.UnknownAzureDevOpsToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            var requests = AzureSince(served, mark);
            AssertConnection(Assert.Single(requests), PartA.Organization, 401, null);
            Assert.Empty(GitLabSince(served, mark));
            Assert.Empty(GitHubSince(served, mark));
            AssertError(app, PartA.AzureDevOpsRefused);
            Assert.Equal(PartA.Organization, ((TextBox)Find(app, "SignInOrganization")).Text);
            Assert.True(Find(app, "SignInOrganization").IsEffectivelyEnabled);
            AssertSelected(app, "SignInToken", PartA.UnknownAzureDevOpsToken);
            AssertDialogButtonsEnabled(app);
            Assert.Null(session.AccountOf(HostingProvider.AzureDevOps));
            Assert.False(File.Exists(AccountsFile(data)));
            Assert.Equal(["probe"], app.Tokens.Calls);
            ShotA(app, PartA.A08AzureRefused);

            // A9 step 8: a value in none of the PAT forms: the Anonymous 200 (ASSUMED in a private organization, open point 8).
            mark = Mark(served);
            app.Type(PartA.PlainValue);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            AssertConnection(Assert.Single(AzureSince(served, mark)), PartA.Organization, 200, null);
            AssertError(app, PartA.AzureDevOpsAnonymous);
            Assert.Equal(PartA.Organization, ((TextBox)Find(app, "SignInOrganization")).Text);
            AssertSelected(app, "SignInToken", PartA.PlainValue);
            AssertDialogButtonsEnabled(app);
            Assert.Null(session.AccountOf(HostingProvider.AzureDevOps));
            Assert.False(File.Exists(AccountsFile(data)));
            Assert.Equal(["probe"], app.Tokens.Calls);
            ShotA(app, PartA.A09AzureAnonymous);

            // A10 step 9: an organization unknown to the fake: connectionData's 404.
            mark = Mark(served);
            Replace(app, "SignInOrganization", PartA.UnknownOrganization);
            Replace(app, "SignInToken", AzureDevOpsToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            AssertConnection(Assert.Single(AzureSince(served, mark)), PartA.UnknownOrganization, 404, null);
            AssertError(app, PartA.AzureDevOpsNoOrganization);
            AssertSelected(app, "SignInOrganization", PartA.UnknownOrganization);
            AssertMasked(app, AzureDevOpsToken);
            Assert.True(Find(app, "SignInTokenLink").IsEffectivelyEnabled);
            AssertDialogButtonsEnabled(app);
            Assert.Null(session.AccountOf(HostingProvider.AzureDevOps));
            Assert.False(File.Exists(AccountsFile(data)));
            Assert.Equal(["probe"], app.Tokens.Calls);
            ShotA(app, PartA.A10AzureNoOrganization);

            // A7 step 10: the check running, connectionData held.
            app.Type(PartA.Organization);
            mark = Mark(served);
            var stored = app.Tokens.Calls.Count;
            var hold = served.AzureDevOps.HoldNext("connection:fernhill-labs");
            try
            {
                app.Click(ConfirmOf(app));
                await app.WaitForAsync(() => hold.Arrived.IsCompleted, "connectionData to arrive at the hold");
                app.Settle();
                var checking = (TextBlock)Find(app, "SignInChecking");
                Assert.True(checking.IsEffectivelyVisible);
                Assert.Equal(PartA.Checking, checking.Text);
                Assert.Contains("secondary", checking.Classes);
                Assert.Equal(PartA.Organization, ((TextBox)Find(app, "SignInOrganization")).Text);
                Assert.False(Find(app, "SignInOrganization").IsEffectivelyEnabled);
                Assert.False(Find(app, "SignInToken").IsEffectivelyEnabled);
                Assert.False(ConfirmOf(app).IsEffectivelyEnabled);
                Assert.True(Find(app, "CancelButton").IsEffectivelyEnabled);
                Assert.False(Find(app, "SignInError").IsEffectivelyVisible);
                AssertNoRequests(served, mark);
                Assert.Null(session.AccountOf(HostingProvider.AzureDevOps));
                Assert.Empty(CallsSince(app.Tokens, stored));
                Assert.False(File.Exists(AccountsFile(data)));
                ShotA(app, PartA.A07AzureChecking);
            }
            finally
            {
                hold.Dispose();
            }

            // A4 step 11: signed in, Fernhill-Labs bold over Ada's name.
            await app.WaitForSignInCheckAsync(dialog);
            Assert.True(dialog.IsClosed, "Azure DevOps' dialog did not close after the sign-in.");
            await app.WaitForAccountAsync(HostingProvider.AzureDevOps, account => account?.State == AccountState.SignedIn);
            AssertAzureSignIn(served, AzureSince(served, mark), PartA.Organization);
            Assert.Equal(["save " + PartA.AzureDevOpsKey(served.AzureDevOps.Port)], CallsSince(app.Tokens, stored));
            AssertAccount(app, served, HostingProvider.AzureDevOps, TokenPlace.Keychain, AccountNoteKind.None);
            Assert.Null(session.AccountOf(HostingProvider.GitHub));
            Assert.Null(session.AccountOf(HostingProvider.GitLab));
            await WaitForFocusAsync(app, "AccountsSignOutButtonAzureDevOps");
            AssertFits(app);
            AssertSignedOutBlock(app, HostingProvider.GitHub);
            AssertSignedOutBlock(app, HostingProvider.GitLab);
            AssertSignedInBlock(app, HostingProvider.AzureDevOps, PartA.InKeychain);
            await WaitForFileAsync(app, data, file => RecordsOf(file).Count == 1, "Azure DevOps' record");
            AssertRecord(ReadAccounts(data), served, HostingProvider.AzureDevOps, useForGit: true, "keychain");
            AssertNoAddress(data);
            Assert.True(app.Tokens.Holds(new("azure-devops", PartA.Host(served.AzureDevOps.Port), PartA.OrganizationLower)));
            ShotA(app, PartA.A04AzureSignedIn);

            // A4 step 12.
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.AzureDevOps)?.State);
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served, launches: 1);
    }

    /// <summary>Method 6: A6, Azure DevOps' dialog prefilled from mobile-app's tab (10.5).</summary>
    [AvaloniaFact]
    public async Task Check_A6_azure_dialog_prefilled_from_the_tab()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b a6c");
        var app = await OpenAsync(data, served, signIn: []);
        try
        {
            var session = app.Session.Services.Hosting;
            Assert.NotNull(app.Session.Shell.ActiveTab.Repository);
            Assert.Null(session.AccountOf(HostingProvider.AzureDevOps));
            AssertNoRequests(served, default);
            var mark = Mark(served);

            // Step 2: prefilled with the tab's organization, the focus in the token box.
            var screen = await app.OpenAccountsAsync();
            var dialog = await app.OpenSignInDialogAsync(HostingProvider.AzureDevOps);
            AssertDialog(app, HostingProvider.AzureDevOps, PartA.Organization);
            Assert.Equal(PartA.Organization, dialog.Organization);
            Assert.Equal(PartA.FocusOpened, FocusedId(app));
            Assert.True(Find(app, "SignInTokenLink").IsEffectivelyEnabled);
            Assert.False(ConfirmOf(app).IsEffectivelyEnabled);
            Assert.Equal(["probe"], app.Tokens.Calls);
            AssertNoRequests(served, mark);
            ShotA(app, PartA.A06AzurePrefilled);

            // Step 3: the prefilled organization sent as it reads.
            var logMark = app.LogMark();
            app.TypeToken(AzureDevOpsToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            Assert.True(dialog.IsClosed, "Azure DevOps' dialog did not close after the sign-in.");
            await app.WaitForAsync(() => app.LogLinesSince(logMark).Any(line => line.Contains(PartA.MobileAppListed, StringComparison.Ordinal)), "mobile-app's pull requests listed");
            await app.WaitForWatchersQuietAsync();
            var requests = AzureSince(served, mark);
            Assert.True(requests.Count >= 2, "Azure DevOps' sign-in made fewer than two requests.");
            AssertAzureSignIn(served, requests[..2], PartA.Organization);
            Assert.Equal(["probe", "save " + PartA.AzureDevOpsKey(served.AzureDevOps.Port)], app.Tokens.Calls);
            Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.AzureDevOps)?.State);
            Assert.Equal(PartA.Organization, session.AccountOf(HostingProvider.AzureDevOps)?.Account.Login);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served);
    }

    /// <summary>Method 7: A12, session only where no keychain can be reached, and its restart on the same data folder (D253, D287).</summary>
    [Fact]
    public async Task Check_A12_session_only_and_its_restart()
    {
        var assembly = typeof(Phase7bChecksA).Assembly;
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b a12");
        var tokens = new FakeTokenStore { Available = false };
        var apps = new List<ShellDriver>();
        string? fileAfterFirst = null;
        double? belowAtA12b = null;

        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                var app = await StartEmptyAsync(data, served, tokens);
                apps.Add(app);
                try
                {
                    await app.OpenAccountsAsync();
                    var mark = Mark(served);

                    // Step 2: GitLab's dialog warns before anything is typed.
                    var dialog = await app.OpenSignInDialogAsync(HostingProvider.GitLab);
                    AssertDialog(app, HostingProvider.GitLab, string.Empty, noKeychain: true);
                    var warning = (TextBlock)Find(app, "SignInNoKeychain");
                    Assert.Equal(PartA.NoKeychain, warning.Text);
                    Assert.Equal(PartA.Warning, ColourOf(warning));
                    Assert.Equal(["probe"], tokens.Calls);
                    AssertNoRequests(served, mark);
                    ShotA(app, PartA.A12GitLabNoKeychainDialog);

                    // Step 3: kept for the session.
                    app.TypeToken(GitLabToken);
                    app.PressKey(Key.Enter);
                    await app.WaitForSignInCheckAsync(dialog);
                    Assert.True(dialog.IsClosed, "GitLab's dialog did not close: a sign-in without a keychain is no failed save.");
                    AssertGitLabUser(GitLabSince(served, mark), 200, AdaLogin);
                    AssertAccount(app, served, HostingProvider.GitLab, TokenPlace.Session, AccountNoteKind.NoKeychain);

                    // Step 4: Azure DevOps' too.
                    mark = Mark(served);
                    dialog = await app.OpenSignInDialogAsync(HostingProvider.AzureDevOps);
                    Assert.True(Find(app, "SignInNoKeychain").IsEffectivelyVisible);
                    Assert.Equal(PartA.FocusOpenedEmpty, FocusedId(app));
                    app.TypeOrganization(PartA.Organization);
                    app.Click(Find(app, "SignInToken"));
                    app.TypeToken(AzureDevOpsToken);
                    app.PressKey(Key.Enter);
                    await app.WaitForSignInCheckAsync(dialog);
                    Assert.True(dialog.IsClosed, "Azure DevOps' dialog did not close.");
                    AssertAzureSignIn(served, AzureSince(served, mark), PartA.Organization);
                    AssertAccount(app, served, HostingProvider.AzureDevOps, TokenPlace.Session, AccountNoteKind.NoKeychain);
                    await WaitForFocusAsync(app, "AccountsSignOutButtonAzureDevOps");
                    WheelUntilStill(app, up: true);
                    Assert.Equal(0, Scroller(app).Offset.Y, 0.5);
                    AssertSignedOutBlock(app, HostingProvider.GitHub);
                    AssertSignedInBlock(app, HostingProvider.GitLab, PartA.NoKeychain);
                    AssertSignedInBlock(app, HostingProvider.AzureDevOps, PartA.NoKeychain);
                    await WaitForFileAsync(app, data, file => RecordsOf(file).Count == 2, "the two session records");
                    var file = ReadAccounts(data);
                    AssertRecord(file, served, HostingProvider.GitLab, useForGit: true, "session");
                    AssertRecord(file, served, HostingProvider.AzureDevOps, useForGit: true, "session");
                    Assert.Equal(["probe"], tokens.Calls);
                    Assert.False(tokens.Holds(new("gitlab", PartA.Host(served.GitLab.Port), AdaLogin)));
                    Assert.False(tokens.Holds(new("azure-devops", PartA.Host(served.AzureDevOps.Port), PartA.OrganizationLower)));

                    // Open point 2: at offset 0 at most the last few pixels of Azure DevOps' Git row
                    // below the viewport. Measured here and asserted after the method's last step, so
                    // that a failure of that bound alone still lets every other step run.
                    belowAtA12b = BelowView(app, InBlock(app, HostingProvider.AzureDevOps, "AccountUseForGitNote"));
                    ShotA(app, PartA.A12SessionOnly);
                    fileAfterFirst = File.ReadAllText(AccountsFile(data));
                }
                finally
                {
                    app.Dispose();
                }

                return true;
            },
            Cancelled);

        // Steps 6 and 7, the second start: both need sign-in; nothing read, nothing sent.
        var requests = Mark(served);
        var stored = tokens.Calls.Count;
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                var app = await StartEmptyAsync(data, served, tokens);
                apps.Add(app);
                try
                {
                    await app.OpenAccountsAsync();
                    Assert.Empty(CallsSince(tokens, stored));
                    AssertNoRequests(served, requests);
                    var session = app.Session.Services.Hosting;
                    AssertAccount(app, served, HostingProvider.GitLab, TokenPlace.Session, AccountNoteKind.SessionEnded, state: AccountState.NeedsSignIn);
                    AssertAccount(app, served, HostingProvider.AzureDevOps, TokenPlace.Session, AccountNoteKind.SessionEnded, state: AccountState.NeedsSignIn);
                    Assert.Null(session.AccountOf(HostingProvider.GitHub));
                    AssertFits(app);
                    AssertSignedOutBlock(app, HostingProvider.GitHub);
                    AssertSessionEndedBlock(app, HostingProvider.GitLab);
                    AssertSessionEndedBlock(app, HostingProvider.AzureDevOps);
                    Assert.Equal(fileAfterFirst, File.ReadAllText(AccountsFile(data)));
                    ShotA(app, PartA.A12SessionEnded);

                    // Step 7: the held account's organization prefills the dialog, stacked over the screen.
                    var screen = app.SettingsDialog;
                    var dialog = await app.OpenSignInDialogAsync(HostingProvider.AzureDevOps);
                    Assert.Equal(PartA.AzureDevOpsDialogTitle, dialog.Title);
                    var open = app.Session.Services.DialogHost.Open;
                    Assert.Equal(2, open.Count);
                    Assert.Same(screen, open[0]);
                    Assert.Same(dialog, open[1]);
                    AssertDialog(app, HostingProvider.AzureDevOps, PartA.Organization, noKeychain: true);
                    Assert.Equal(PartA.Organization, dialog.Organization);
                    Assert.Equal(PartA.FocusOpened, FocusedId(app));
                    Assert.True(Find(app, "SignInNoKeychain").IsEffectivelyVisible);
                    Assert.True(Find(app, "SignInTokenLink").IsEffectivelyEnabled);
                    Assert.False(ConfirmOf(app).IsEffectivelyEnabled);
                    Assert.Equal(["probe"], CallsSince(tokens, stored));
                    AssertNoRequests(served, requests);
                    app.PressKey(Key.Escape);
                    await app.WaitForAsync(() => dialog.IsClosed, "the dialog to close");
                    Assert.NotNull(app.SettingsDialog);
                    Assert.Equal(AccountState.NeedsSignIn, session.AccountOf(HostingProvider.GitLab)?.State);
                    Assert.Equal(AccountState.NeedsSignIn, session.AccountOf(HostingProvider.AzureDevOps)?.State);
                }
                finally
                {
                    app.Dispose();
                }

                return true;
            },
            Cancelled);

        AssertCleanEnd(apps, data, served);
        Assert.NotNull(belowAtA12b);
        Assert.True(
            belowAtA12b <= PartA.A12FewPixels,
            $"a12b: at offset 0, {belowAtA12b:0.#} px of Azure DevOps' Git row lie below the viewport, more than the few pixels (PartA.A12FewPixels, {PartA.A12FewPixels}) A12's step 4 allows (open point 2).");
    }

    /// <summary>Method 8: A13, one sign-out leaves the other blocks and records (D348, D291, D296).</summary>
    [AvaloniaFact]
    public async Task Check_A13_one_sign_out_keeps_the_others()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b a13");
        var app = await StartEmptyAsync(data, served);
        try
        {
            var session = app.Session.Services.Hosting;
            await app.SignInAsync(HostingProvider.GitHub, GitHubToken, cancellationToken: Cancelled);
            await app.SignInAsync(HostingProvider.GitLab, GitLabToken, cancellationToken: Cancelled);
            await app.SignInAsync(HostingProvider.AzureDevOps, AzureDevOpsToken, PartA.Organization, Cancelled);
            var screen = await app.OpenAccountsAsync();
            var mark = Mark(served);
            var stored = app.Tokens.Calls.Count;
            await WaitForFileAsync(app, data, file => RecordsOf(file).Count == 3, "the three records");
            var before = ReadAccounts(data);
            Assert.All(Three, provider => Assert.Equal((AccountState.SignedIn, TokenPlace.Keychain), (session.AccountOf(provider)?.State, session.AccountOf(provider)?.Place)));
            Assert.Equal(3, RecordsOf(before).Count);
            Assert.True(Scroller(app).Extent.Height > Scroller(app).Viewport.Height, "Three signed-in blocks do not scroll.");
            Assert.Equal(0, Scroller(app).Offset.Y, 0.5);
            Assert.True(WhollyInView(app, InBlock(app, HostingProvider.GitLab, "AccountsSignOutButton")));

            // Step 2: GitLab's question.
            app.Click(InBlock(app, HostingProvider.GitLab, "AccountsSignOutButton"));
            var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
            AssertQuestion(app, PartA.GitLabSignOutTitle, PartA.GitLabSignOutQuestionAda);
            Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.GitLab)?.State);
            Assert.Empty(CallsSince(app.Tokens, stored));
            AssertNoRequests(served, mark);
            ShotA(app, PartA.A13GitLabQuestion);

            // Step 3: signed out of GitLab alone.
            app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
            await app.WaitForAsync(() => question.IsClosed, "the question to close");
            await app.WaitForAccountAsync(HostingProvider.GitLab, account => account is null);
            await app.WaitForAsync(() => !screen.Accounts.GitLab.SignOutCommand.IsRunning, "GitLab's sign-out to end");
            Assert.Equal(["delete " + PartA.GitLabKey(served.GitLab.Port)], CallsSince(app.Tokens, stored));
            Assert.False(app.Tokens.Holds(new("gitlab", PartA.Host(served.GitLab.Port), AdaLogin)));
            Assert.True(app.Tokens.Holds(new("github", PartA.Host(served.GitHub.Port), OctoLogin)));
            Assert.True(app.Tokens.Holds(new("azure-devops", PartA.Host(served.AzureDevOps.Port), PartA.OrganizationLower)));
            AssertNoRequests(served, mark);
            Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.GitHub)?.State);
            Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.AzureDevOps)?.State);
            await WaitForFileAsync(app, data, file => RecordsOf(file).Count == 2, "the two records left");
            var after = ReadAccounts(data);
            Assert.Equal(1, after!.Value.GetProperty("version").GetInt32());
            Assert.Null(RecordOf(after, "gitlab", PartA.Host(served.GitLab.Port)));
            Assert.Equal(
                RecordOf(before, "github", PartA.Host(served.GitHub.Port))!.Value.GetRawText(),
                RecordOf(after, "github", PartA.Host(served.GitHub.Port))!.Value.GetRawText());
            Assert.Equal(
                RecordOf(before, "azure-devops", PartA.Host(served.AzureDevOps.Port))!.Value.GetRawText(),
                RecordOf(after, "azure-devops", PartA.Host(served.AzureDevOps.Port))!.Value.GetRawText());
            await WaitForFocusAsync(app, "AccountsSignInButtonGitLab");
            AssertFits(app);
            AssertSignedInBlock(app, HostingProvider.GitHub, PartA.InKeychain);
            AssertSignedOutBlock(app, HostingProvider.GitLab);
            AssertSignedInBlock(app, HostingProvider.AzureDevOps, PartA.InKeychain);
            ShotA(app, PartA.A13GitLabSignedOut);

            // Step 4: Azure DevOps' question names the organization.
            stored = app.Tokens.Calls.Count;
            app.Click(InBlock(app, HostingProvider.AzureDevOps, "AccountsSignOutButton"));
            question = await app.WaitForDialogAsync<ConfirmationViewModel>();
            AssertQuestion(app, PartA.AzureDevOpsSignOutTitle, PartA.AzureDevOpsSignOutQuestion);
            Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.AzureDevOps)?.State);
            ShotA(app, PartA.A13AzureQuestion);

            // Step 5: Cancel.
            app.Click(app.FindByAutomationId(app.TopDialogView!, "CancelButton"));
            await app.WaitForAsync(() => question.IsClosed, "the question to close");
            Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.AzureDevOps)?.State);
            AssertSignedOutBlock(app, HostingProvider.GitLab);
            AssertSignedInBlock(app, HostingProvider.AzureDevOps, PartA.InKeychain);
            Assert.Empty(CallsSince(app.Tokens, stored));
            Assert.Equal(after!.Value.GetRawText(), ReadAccounts(data)!.Value.GetRawText());
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served);
    }

    /// <summary>Method 9: A14, each switch saved alone (D216, D255, D350), on the empty tab beside web-app's.</summary>
    [AvaloniaFact]
    public async Task Check_A14_each_switch_saved_alone()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("phase7b a14");
        var app = ShellDriver.StartWithTabs(data.Path, [served.WebApp.Path, null], activeTab: 0, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        try
        {
            await app.WaitForRepositoryAsync();
            var webApp = app.Session.Shell.ActiveTab.Repository!;
            var logMark = app.LogMark();
            await app.SignInAsync(HostingProvider.GitHub, GitHubToken, cancellationToken: Cancelled);
            await app.SignInAsync(HostingProvider.GitLab, GitLabToken, cancellationToken: Cancelled);
            await app.SignInAsync(HostingProvider.AzureDevOps, AzureDevOpsToken, PartA.Organization, Cancelled);
            await app.WaitForAsync(() => app.LogLinesSince(logMark).Any(line => line.Contains(PartA.WebAppListed, StringComparison.Ordinal)), "web-app's merge requests listed");
            await app.WaitForWatchersQuietAsync();
            app.MoveToNeutral();
            var session = app.Session.Services.Hosting;
            var stored = app.Tokens.Calls.Count;
            await WaitForFileAsync(app, data, file => RecordsOf(file).Count == 3, "the three records");
            var before = ReadAccounts(data);

            // Step 1: web-app's bindings, GitLab's for its origin and GitHub's for its upstream.
            var gitLabKey = PartA.GitLabKey(served.GitLab.Port);
            var gitHubKey = PartA.GitHubKey(served.GitHub.Port);
            await app.WaitForAsync(() => webApp.Hosting.Bindings.Any(binding => binding.Account.Id == gitLabKey) && webApp.Hosting.Bindings.Any(binding => binding.Account.Id == gitHubKey), "web-app's bindings");
            AssertWebAppBindings(webApp, served, gitLab: true);
            Assert.True(session.BindsGit);
            Assert.Equal(3, RecordsOf(before).Count);
            foreach (var provider in Three)
            {
                AssertRecord(before, served, provider, useForGit: true, "keychain");
            }

            // Step 2: GitLab's switch off and Save, on the empty tab.
            var tabs = app.Window.GetVisualDescendants().OfType<Button>().Where(button => button.Name == "TabButton" && button.IsEffectivelyVisible).OrderBy(button => app.BoundsOf(button).X).ToList();
            app.Click(tabs[1]);
            await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository is null, "the empty tab");
            await app.OpenAccountsAsync();
            app.Click(InBlock(app, HostingProvider.GitLab, "AccountUseForGit"));
            app.Settle();
            Assert.Equal(false, ((CheckBox)InBlock(app, HostingProvider.GitLab, "AccountUseForGit")).IsChecked);
            Assert.Equal(before!.Value.GetRawText(), ReadAccounts(data)!.Value.GetRawText());
            AssertWebAppBindings(webApp, served, gitLab: true);
            Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
            await WaitForFileAsync(app, data, file => RecordOf(file, "gitlab", PartA.Host(served.GitLab.Port)) is { } record && !record.GetProperty("useForGit").GetBoolean(), "GitLab's record with the switch off");
            await app.WaitForAsync(() => webApp.Hosting.Bindings.All(binding => binding.Account.Id != gitLabKey), "web-app's GitLab binding to go");
            var afterGitLab = ReadAccounts(data);
            AssertRecord(afterGitLab, served, HostingProvider.GitLab, useForGit: false, "keychain");
            Assert.Equal(RecordOf(before, "github", PartA.Host(served.GitHub.Port))!.Value.GetRawText(), RecordOf(afterGitLab, "github", PartA.Host(served.GitHub.Port))!.Value.GetRawText());
            Assert.Equal(RecordOf(before, "azure-devops", PartA.Host(served.AzureDevOps.Port))!.Value.GetRawText(), RecordOf(afterGitLab, "azure-devops", PartA.Host(served.AzureDevOps.Port))!.Value.GetRawText());
            Assert.Equal(AccountState.SignedIn, session.AccountOf(HostingProvider.GitLab)?.State);
            Assert.False(session.AccountOf(HostingProvider.GitLab)?.Account.UseForGit);
            Assert.True(session.BindsGit);
            AssertWebAppBindings(webApp, served, gitLab: false);
            Assert.Empty(CallsSince(app.Tokens, stored));

            // Step 3: the reopened screen at its top.
            await app.OpenAccountsAsync();
            Assert.Equal(0, Scroller(app).Offset.Y, 0.5);
            AssertSignedInBlock(app, HostingProvider.GitHub, PartA.InKeychain);
            AssertSignedInBlock(app, HostingProvider.GitLab, PartA.InKeychain, useForGit: false);
            Assert.True(InBlock(app, HostingProvider.GitLab, "AccountUseForGit").IsEffectivelyEnabled);
            Assert.False(WhollyInView(app, InBlock(app, HostingProvider.AzureDevOps, "AccountUseForGit")), "Azure DevOps' Git row is wholly in view at the top.");
            Assert.Null(app.Session.Shell.ActiveTab.Repository);
            var strip = app.TabStrip();
            Assert.Equal([WebApp, PartA.NewTab], strip.Tabs.Select(tab => tab.Title));
            Assert.Equal(1, strip.ActiveIndex);
            Assert.True(strip.Tabs[1].IsActive, "The empty tab is not the active one.");
            ShotA(app, PartA.A14GitLabSwitchOff);

            // Step 4: Azure DevOps' switch off and Save.
            WheelUntilStill(app, up: false);
            app.Click(InBlock(app, HostingProvider.AzureDevOps, "AccountUseForGit"));
            app.Settle();
            Assert.True(await app.SaveSettingsAsync(), "Save did not close the screen.");
            await WaitForFileAsync(app, data, file => RecordOf(file, "azure-devops", PartA.Host(served.AzureDevOps.Port)) is { } record && !record.GetProperty("useForGit").GetBoolean(), "Azure DevOps' record with the switch off");
            var afterAzure = ReadAccounts(data);
            AssertRecord(afterAzure, served, HostingProvider.AzureDevOps, useForGit: false, "keychain");
            AssertRecord(afterAzure, served, HostingProvider.GitLab, useForGit: false, "keychain");
            Assert.Equal(RecordOf(before, "github", PartA.Host(served.GitHub.Port))!.Value.GetRawText(), RecordOf(afterAzure, "github", PartA.Host(served.GitHub.Port))!.Value.GetRawText());
            Assert.False(session.AccountOf(HostingProvider.AzureDevOps)?.Account.UseForGit);
            Assert.True(session.BindsGit);
            AssertWebAppBindings(webApp, served, gitLab: false);
            Assert.Empty(CallsSince(app.Tokens, stored));

            // Step 5: the end of the reopened screen.
            var screen = await app.OpenAccountsAsync();
            WheelUntilStill(app, up: false);
            AssertAtEnd(app);
            Assert.Equal(true, ((CheckBox)InBlock(app, HostingProvider.GitHub, "AccountUseForGit")).IsChecked);
            Assert.Equal(false, ((CheckBox)InBlock(app, HostingProvider.GitLab, "AccountUseForGit")).IsChecked);
            Assert.True(WhollyInView(app, InBlock(app, HostingProvider.GitHub, "AccountUseForGit")), "GitHub's switch is not in view at the end.");
            Assert.True(WhollyInView(app, InBlock(app, HostingProvider.GitLab, "AccountUseForGit")), "GitLab's switch is not in view at the end.");
            Assert.True(WhollyInView(app, app.AccountsBlock(HostingProvider.AzureDevOps)));
            Assert.Equal(false, ((CheckBox)InBlock(app, HostingProvider.AzureDevOps, "AccountUseForGit")).IsChecked);
            Assert.Equal(PartA.AzureDevOpsUseForGitNote, TextOf(app, HostingProvider.AzureDevOps, "AccountUseForGitNote"));
            ShotA(app, PartA.A14AzureSwitchOffEnd);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            Assert.Equal(afterAzure!.Value.GetRawText(), ReadAccounts(data)!.Value.GetRawText());
        }
        finally
        {
            app.Dispose();
        }

        AssertCleanEnd([app], data, served);
    }

    /// <summary>The confirmation stacked over the screen: its title and question, no detail, Cancel and Sign out in the primary look (D296).</summary>
    private static void AssertQuestion(ShellDriver app, string title, string question)
    {
        var view = app.TopDialogView!;
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId(view, "ConfirmTitle")).Text);
        Assert.Equal(question, ((TextBlock)app.FindByAutomationId(view, "ConfirmQuestion")).Text);
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<Control>(), control => AutomationProperties.GetAutomationId(control) is "ConfirmDetail" or "DialogRepository" && control.IsEffectivelyVisible);
        Assert.Equal([PartA.Cancel, PartA.SignOutConfirm], ButtonsOf(app, view));
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Same(Application.Current!.FindResource("VcPrimaryButton"), confirm.Theme);
        Assert.DoesNotContain("danger", confirm.Classes);
    }

    /// <summary>web-app's bindings: GitHub's for the upstream always, GitLab's for the origin when <paramref name="gitLab"/>; none of Azure DevOps' (twins are WP-C's, not counted).</summary>
    private static void AssertWebAppBindings(RepositoryViewModel webApp, ServedHosting7b served, bool gitLab)
    {
        var bindings = webApp.Hosting.Bindings;
        var gitLabKey = PartA.GitLabKey(served.GitLab.Port);
        var gitHubKey = PartA.GitHubKey(served.GitHub.Port);
        Assert.Contains(bindings, binding => binding.Account.Id == gitHubKey && binding.Url == WithPorts(WebAppUpstreamUrl, served));
        Assert.Equal(gitLab, bindings.Any(binding => binding.Account.Id == gitLabKey && binding.Url == WithPorts(WebAppUrl, served)));
        Assert.Equal(gitLab, bindings.Any(binding => binding.Account.Id == gitLabKey));
        Assert.DoesNotContain(bindings, binding => binding.Account.Provider == HostingProvider.AzureDevOps);
    }
}
