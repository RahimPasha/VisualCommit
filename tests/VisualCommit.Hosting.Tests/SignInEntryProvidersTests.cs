using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The window's sign-in entry for any provider, <c>TabServices.SignIn</c> (docs/phase-7b-design.md
/// 10.5, 10.8; ruling 22, D104, D334): each provider's dialog by its title; off answers false and
/// signed in true, without a dialog; GitLab's call while GitHub's dialog is open opens none and
/// answers for GitLab; one provider's two calls share a dialog; Azure DevOps' prefill from the held
/// account and from the active tab's remote; a block's "Sign in…" stacks the dialog over the screen;
/// a block's sign-out holds every block's buttons until it ends.
/// The app starts through <see cref="ShellDriver"/> with real git and the scenario "hosting7b" served
/// by the three fakes on free ports (port 0, D265). On the lagged list (design 0.4).
/// </summary>
public sealed class SignInEntryProvidersTests
{
    private const string Organization = "Fernhill-Labs";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public async Task Each_provider_s_dialog_has_its_title()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry providers");
        using var app = Start(data, served);
        await app.WaitUntilReadyAsync();

        foreach (var (provider, title) in new[] { (HostingProvider.GitHub, "Add GitHub account"), (HostingProvider.GitLab, "Add GitLab account"), (HostingProvider.AzureDevOps, "Add Azure DevOps account") })
        {
            var entry = EntryOf(app, provider);
            var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
            Assert.Equal((provider, title), (dialog.Provider, dialog.Title));
            dialog.CancelCommand.Execute(null);
            Assert.False(await entry.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));
        }

        Assert.DoesNotContain(app.Tokens.Calls, call => call != "probe");
        AssertNoRequests(served);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Off_answers_false_and_signed_in_true_without_a_dialog()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry providers");
        using var app = ShellDriver.Start(data.Path, gitHub: served.GitHub, azureDevOps: served.AzureDevOps);
        await app.WaitUntilReadyAsync();

        // GitLab is off: no fake of it was given (D362).
        Assert.False(await EntryOf(app, HostingProvider.GitLab));
        Assert.Null(app.Session.Services.DialogHost.Current);

        await app.SignInAsync(HostingProvider.AzureDevOps, served.AzureDevOpsToken, Organization, Cancelled);
        var requests = served.AzureDevOps.Requests.Count;
        Assert.True(await EntryOf(app, HostingProvider.AzureDevOps));
        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Equal(requests, served.AzureDevOps.Requests.Count);
        Assert.Empty(served.GitLab.Requests);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task All_off_answers_false_for_every_provider()
    {
        using var data = new TempDirectory("sign-in entry providers");
        using var app = ShellDriver.Start(data.Path);
        await app.WaitUntilReadyAsync();

        foreach (var provider in HostingProviders.InAccountsOrder)
        {
            Assert.False(await EntryOf(app, provider));
        }

        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Empty(app.Tokens.Calls);
    }

    [AvaloniaFact]
    public async Task GitLab_s_call_under_GitHub_s_open_dialog_opens_none_and_answers_for_GitLab()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry providers");
        using var app = Start(data, served);
        await app.WaitUntilReadyAsync();

        var gitHub = EntryOf(app, HostingProvider.GitHub);
        var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
        var gitLab = EntryOf(app, HostingProvider.GitLab);
        app.Settle();
        Assert.Single(app.Session.Services.DialogHost.Open);
        Assert.False(gitLab.IsCompleted);

        dialog.Token = served.GitHubToken;
        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.True(await gitHub.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));
        Assert.False(await gitLab.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));
        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.AccountOf(HostingProvider.GitHub)?.State);
        Assert.Null(app.Session.Services.Hosting.AccountOf(HostingProvider.GitLab));
        Assert.Empty(served.GitLab.Requests);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task One_provider_s_two_calls_share_a_dialog()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry providers");
        using var app = Start(data, served);
        await app.WaitUntilReadyAsync();

        var first = EntryOf(app, HostingProvider.AzureDevOps);
        var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
        var second = EntryOf(app, HostingProvider.AzureDevOps);
        app.Settle();
        Assert.Single(app.Session.Services.DialogHost.Open);
        Assert.False(second.IsCompleted);

        dialog.Organization = Organization;
        dialog.Token = served.AzureDevOpsToken;
        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));
        Assert.True(await second.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));
        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Equal(Organization, app.Session.Services.Hosting.AccountOf(HostingProvider.AzureDevOps)?.Account.Login);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Azure_DevOps_is_prefilled_from_the_active_tab_s_remote_and_empty_without_one()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry providers");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.MobileApp.Path, null], activeTab: 0, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();

        var entry = EntryOf(app, HostingProvider.AzureDevOps);
        var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
        Assert.Equal(Organization, dialog.Organization);
        dialog.CancelCommand.Execute(null);
        Assert.False(await entry.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));

        // GitLab's dialog has no organization, whatever the tab.
        entry = EntryOf(app, HostingProvider.GitLab);
        dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
        Assert.Equal(string.Empty, dialog.Organization);
        dialog.CancelCommand.Execute(null);
        Assert.False(await entry.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));

        // The empty tab: nothing to prefill.
        var tabs = app.Window.GetVisualDescendants().OfType<Button>().Where(button => button.Name == "TabButton" && button.IsEffectivelyVisible).OrderBy(button => app.BoundsOf(button).X).ToList();
        app.Click(tabs[1]);
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository is null, "the empty tab");
        entry = EntryOf(app, HostingProvider.AzureDevOps);
        dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
        Assert.Equal(string.Empty, dialog.Organization);
        dialog.CancelCommand.Execute(null);
        Assert.False(await entry.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));
        AssertNoRequests(served);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Azure_DevOps_is_prefilled_with_the_held_account_s_login_before_the_tab_s()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry providers");

        // A record kept for the last session only: the start holds it as needing sign-in and reads no store (D287).
        var host = $"127.0.0.1:{served.AzureDevOps.Port}";
        File.WriteAllText(
            Path.Combine(data.Path, "accounts.json"),
            $$"""{"version":1,"accounts":[{"provider":"azure-devops","host":"{{host}}","login":"Held-Org","id":0,"name":"Ada Lovelace","avatarUrl":null,"useForGit":true,"tokenState":"session","userId":"00000000-0000-4000-8000-000000000001"}]}""");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.MobileApp.Path], gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        Assert.Equal(AccountState.NeedsSignIn, app.Session.Services.Hosting.AccountOf(HostingProvider.AzureDevOps)?.State);

        var entry = EntryOf(app, HostingProvider.AzureDevOps);
        var dialog = await app.WaitForDialogAsync<SignInDialogViewModel>();
        Assert.Equal("Held-Org", dialog.Organization);
        dialog.CancelCommand.Execute(null);
        Assert.False(await entry.WaitAsync(TimeSpan.FromSeconds(30), Cancelled));
        Assert.DoesNotContain(app.Tokens.Calls, call => call != "probe");
        AssertNoRequests(served);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_block_s_sign_in_stacks_its_provider_s_dialog_over_the_screen()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry providers");
        using var app = Start(data, served);
        await app.WaitUntilReadyAsync();
        var settings = await app.OpenAccountsAsync();

        var dialog = await app.OpenSignInDialogAsync(HostingProvider.GitLab);

        var open = app.Session.Services.DialogHost.Open;
        Assert.Equal(2, open.Count);
        Assert.Same(settings, open[0]);
        Assert.Same(dialog, open[1]);
        Assert.Equal(HostingProvider.GitLab, dialog.Provider);
        dialog.Token = served.GitLabToken;
        await dialog.ConfirmCommand.ExecuteAsync(null);
        await app.WaitForAsync(() => !settings.Accounts.GitLab.SignInCommand.IsRunning, "the block's sign-in to end");
        Assert.True(dialog.IsClosed);
        Assert.False(settings.IsClosed);
        Assert.Same(settings, app.Session.Services.DialogHost.Current);
        Assert.Equal("ada", settings.Accounts.GitLab.Login);
        End(app, data, served);
    }

    /// <summary>
    /// While one block's sign-out waits on the keychain, no other block's button can open a dialog
    /// over the screen (7a's rule for one block, section-wide), so the sign-out's end puts the focus
    /// on its own "Sign in…" with the screen on top, never under another block's dialog.
    /// </summary>
    [AvaloniaFact]
    public async Task A_block_s_sign_out_holds_every_block_s_buttons_and_ends_with_its_sign_in_focused()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("sign-in entry providers");
        using var app = Start(data, served);
        await app.WaitUntilReadyAsync();
        await app.SignInAsync(HostingProvider.GitHub, served.GitHubToken, cancellationToken: Cancelled);
        await app.SignInAsync(HostingProvider.GitLab, served.GitLabToken, cancellationToken: Cancelled);
        var settings = await app.OpenAccountsAsync();
        var others = new[] { (HostingProvider.GitLab, "AccountsSignOutButtonGitLab"), (HostingProvider.AzureDevOps, "AccountsSignInButtonAzureDevOps") };

        using (var hold = app.Tokens.HoldNext())
        {
            app.Click(app.FindByAutomationId(app.AccountsBlock(HostingProvider.GitHub), "AccountsSignOutButton"));
            var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
            app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
            await app.WaitForAsync(() => question.IsClosed, "GitHub's sign-out question to close");
            await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
            app.Settle();

            Assert.True(settings.Accounts.GitHub.IsBusy);
            foreach (var (provider, id) in others)
            {
                var button = app.FindByAutomationId(app.AccountsBlock(provider), id);
                Assert.True(button.IsEffectivelyVisible, $"{id} does not show.");
                Assert.False(button.IsEffectivelyEnabled, $"{id} is enabled while GitHub's sign-out runs.");
                button.BringIntoView();
                app.Settle();
                app.Click(button);
                app.Settle();
                Assert.Same(settings, app.Session.Services.DialogHost.Current);
            }
        }

        await app.WaitForAsync(() => !settings.Accounts.GitHub.SignOutCommand.IsRunning, "GitHub's sign-out to end");
        app.Settle();
        Assert.Null(app.Session.Services.Hosting.AccountOf(HostingProvider.GitHub));
        Assert.Same(settings, app.Session.Services.DialogHost.Current);
        await app.WaitForAsync(() => FocusedId(app) == "AccountsSignInButton", "the focus on GitHub's Sign in…");
        foreach (var (provider, id) in others)
        {
            Assert.True(app.FindByAutomationId(app.AccountsBlock(provider), id).IsEffectivelyEnabled, $"{id} is disabled after GitHub's sign-out.");
        }

        Assert.Equal("ada", settings.Accounts.GitLab.Login);
        End(app, data, served);
    }

    private static string? FocusedId(ShellDriver app) =>
        app.FocusedElement is Control focused
            ? focused.GetSelfAndVisualAncestors().OfType<Control>().Select(Avalonia.Automation.AutomationProperties.GetAutomationId).FirstOrDefault(id => id is { Length: > 0 })
            : null;

    private static Task<bool> EntryOf(ShellDriver app, HostingProvider provider)
    {
        var signIn = app.Session.Services.SignIn ?? throw new InvalidOperationException("The window set no sign-in entry.");
        return signIn(provider);
    }

    /// <summary>The scenario "hosting7b" served by its three fakes on free ports (D265: no fixed port in this project).</summary>
    private static async Task<ServedHosting7b> ServeAsync()
    {
        var copy = await Scenarios.Hosting7bAsync();
        try
        {
            return await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    /// <summary>One empty tab with the three fakes.</summary>
    private static ShellDriver Start(TempDirectory data, ServedHosting7b served) =>
        ShellDriver.Start(data.Path, gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);

    private static void AssertNoRequests(ServedHosting7b served)
    {
        Assert.Empty(served.AzureDevOps.Requests);
        Assert.Empty(served.GitLab.Requests);
        Assert.Empty(served.GitHub.Requests);
    }

    /// <summary>Closes the app, then checks that no token reached the data folder or a git call, and that the fakes saw nothing wrong.</summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting7b served)
    {
        app.Dispose();
        foreach (var token in served.Tokens)
        {
            TokenTripwire.AssertAbsent(token, [data.Path], app.Session.GitCalls);
        }

        Assert.Equal(0, app.RefusedRequests.Count);
        Assert.Empty(served.AzureDevOps.Errors);
        Assert.Empty(served.GitLab.Errors);
        Assert.Empty(served.GitHub.Errors);
    }
}
