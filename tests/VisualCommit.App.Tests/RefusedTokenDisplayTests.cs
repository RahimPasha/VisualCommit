using System.Net;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.AskPass;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The error dialog of an operation whose credentials GitHub refused through the app's helper
/// (docs/phase-7a-design.md 4.7, ruling 4, D276, D330): <see cref="OperationTexts.GitCredentialsRefused"/>
/// word for word with the login's own case, never <see cref="OperationTexts.TokenRefused"/>, and
/// "Open settings…" on Accounts; no credentials dialog; a bulk row's kept dialog on a row without
/// a tab the same.
/// </summary>
public class RefusedTokenDisplayTests
{
    [Fact]
    public void The_two_refusals_have_the_designs_sentences()
    {
        Assert.Equal(
            "GitHub refused git's credentials for octocat. If the token has expired or was revoked, sign in to GitHub again in Settings, Accounts.",
            OperationTexts.GitCredentialsRefused("octocat"));
        Assert.Equal("GitHub refused the token of octocat. Sign in to GitHub in Settings, Accounts.", OperationTexts.TokenRefused("octocat"));
    }

    [Fact]
    public async Task A_refused_fetch_says_git_credentials_were_refused_with_the_logins_case_and_opens_accounts()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.Credentials = new NetworkCredential(AskPassProtocol.CredentialUserName, CredentialTestSupport.Token);
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var stub = CredentialTestSupport.Stub(server, login: "OctoCat", token: "github_pat_FAKE-octocat-0");
        stub.RepositoryBindings = [new CredentialBinding(server.Url, stub.Key)];
        var handler = new ScriptedPromptHandler();
        using var app = await OperationsHarness.OpenAsync(repo, _ => handler, hosting: stub);
        SettingsSection? opened = null;
        app.Services.OpenSettings = section =>
        {
            opened = section;
            return Task.CompletedTask;
        };
        app.Dialogs.CloseError(clickExtra: true);

        await app.Operations.FetchAllAsync();
        await app.WaitForIdleAsync();

        // The stub keeps the account signed in, as 7.6's check of the token does for a valid one (D330).
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(
            ("Fetch failed", "GitHub refused git's credentials for OctoCat. If the token has expired or was revoked, sign in to GitHub again in Settings, Accounts.", true),
            (error.Title, error.Summary, error.HasExtraAction));
        Assert.Equal(SettingsSection.Accounts, opened);
        Assert.Empty(handler.Prompts);
        Assert.Equal([stub.Key], stub.Credentials.Refusals);
        Assert.Equal(AccountState.SignedIn, stub.Account!.State);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Without_a_signed_in_status_the_refused_keys_login_is_named()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.Credentials = new NetworkCredential(AskPassProtocol.CredentialUserName, CredentialTestSupport.Token);
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var stub = CredentialTestSupport.Stub(server, login: "OctoCat", token: "github_pat_FAKE-octocat-0");
        stub.RepositoryBindings = [new CredentialBinding(server.Url, stub.Key)];
        stub.Account = null;
        using var app = await OperationsHarness.OpenAsync(repo, _ => new ScriptedPromptHandler(), hosting: stub);
        app.Dialogs.CloseError();

        await app.Operations.FetchAllAsync();
        await app.WaitForIdleAsync();

        Assert.Equal(OperationTexts.GitCredentialsRefused("octocat"), Assert.Single(app.Dialogs.Errors).Summary);
        app.AssertScriptFollowed();
    }

    [AvaloniaFact]
    public async Task A_bulk_rows_kept_dialog_on_a_row_without_a_tab_says_the_same()
    {
        // Fetch all on rows without a tab (D332), with a token the server no longer takes: each
        // row's dialog is kept for later (D195) and is the same refusal, with "Open settings…".
        await using var setup = await CredentialDashboard.StartAsync(bindsGit: true, token: "github_pat_FAKE-octocat-0");
        var opened = new List<SettingsSection>();
        setup.Services.OpenSettings = section =>
        {
            opened.Add(section);
            return Task.CompletedTask;
        };
        var dashboard = setup.Dashboard;
        dashboard.OnBecameActive();

        await dashboard.Bulk.FetchAllAsync().WaitAsync(TimeSpan.FromSeconds(90));

        Assert.Equal(2, dashboard.Bulk.Current!.Failed);
        foreach (var row in dashboard.Rows)
        {
            Assert.True(row.Repository is { IsSummary: true });
            var display = row.KeptDialog?.Display;
            Assert.Equal(("Fetch failed", OperationTexts.GitCredentialsRefused("octocat"), OperationTexts.OpenSettingsButton), (display?.Title, display?.Summary, display?.Extra?.Label));
            await display!.Extra!.Run();
        }

        Assert.Equal([SettingsSection.Accounts, SettingsSection.Accounts], opened);
        Assert.Equal([setup.Stub.Key, setup.Stub.Key], setup.Stub.Credentials.Refusals);
        Assert.Empty(setup.Handler.Prompts);
        Assert.Null(setup.Services.DialogHost.Current);
    }
}
