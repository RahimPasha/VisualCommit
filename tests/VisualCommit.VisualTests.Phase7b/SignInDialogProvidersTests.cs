using VisualCommit.App.Tests;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The sign-in dialog per provider as a view model (docs/phase-7b-design.md 10.4, 10.8; D360, D378,
/// D385): Azure DevOps' organization box through <see cref="AzureDevOpsProvider.OrganizationOf"/>
/// alone, Sign in's and the links' enablement, each failure's text against the fakes as WP-LF left
/// them, another provider's token form refused unsent in each dialog while its own goes on, the
/// login in the server's spelling, and the links' addresses. The session is the real one over the
/// scenario "hosting7b" served by the three fakes on free ports (port 0), with a <see cref="FakeTokenStore"/>;
/// the service's URL forms are read by a session on <see cref="AzureDevOpsEndpoints.Service"/> whose
/// handler refuses every request, so that nothing leaves the machine.
/// </summary>
public sealed class SignInDialogProvidersTests
{
    private static readonly string AzureDevOpsForm = new string('a', 76) + "AZDO" + "abcd";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Azure_DevOps_s_box_takes_what_OrganizationOf_takes_and_nothing_else()
    {
        await using var setup = await Setup.StartAsync();
        var endpoints = setup.Served.AzureDevOps.Endpoints;
        var port = setup.Served.AzureDevOps.Port;
        string[] texts =
        [
            "Fernhill-Labs", "fernhill-labs", " Fernhill-Labs ", $"http://127.0.0.1:{port}/Fernhill-Labs", $"http://127.0.0.1:{port}/Fernhill-Labs/Mobile%20App",
            $"http://Fernhill-Labs@127.0.0.1:{port}/Fernhill-Labs", "https://dev.azure.com/Fernhill-Labs", "https://fernhill-labs.visualstudio.com/",
            "https://app.vssps.visualstudio.com/", "Fernhill%2DLabs", "Fernhill Labs", $"http://127.0.0.1:{port}//Fernhill-Labs", "https://example.com/Fernhill-Labs", "%",
        ];
        Assert.Equal("Fernhill-Labs", AzureDevOpsProvider.OrganizationOf($"http://127.0.0.1:{port}/Fernhill-Labs", endpoints));
        Assert.Null(AzureDevOpsProvider.OrganizationOf("Fernhill Labs", endpoints));

        foreach (var text in texts)
        {
            var organization = AzureDevOpsProvider.OrganizationOf(text, endpoints);
            var dialog = setup.Dialog(HostingProvider.AzureDevOps);
            await dialog.Opened;
            dialog.Organization = text;
            dialog.Token = AzureDevOpsToken;
            Assert.Equal(organization is not null, dialog.CanOpenTokenPage);
            Assert.Equal(organization is not null, dialog.OpenTokenPageCommand.CanExecute(null));
            Assert.True(dialog.ConfirmCommand.CanExecute(null), $"Sign in is disabled for '{text}'.");
            var mark = setup.Served.AzureDevOps.Requests.Count;
            await dialog.ConfirmCommand.ExecuteAsync(null);
            var requests = setup.Served.AzureDevOps.Requests.Skip(mark).ToList();
            if (organization is null)
            {
                Assert.Equal(PartA.NotAnOrganizationError, dialog.ErrorText);
                Assert.Empty(requests);
            }
            else
            {
                // The organization the rule gives is the one sent, as it reads (10.4).
                Assert.NotEmpty(requests);
                Assert.Equal($"/{organization}/_apis/connectionData", requests[0].Path);
            }

            if (setup.Session.AccountOf(HostingProvider.AzureDevOps) is not null)
            {
                await setup.Session.SignOutAsync(HostingProvider.AzureDevOps, Cancelled);
            }
        }
    }

    /// <summary>
    /// On the service's endpoints (<see cref="AzureDevOpsEndpoints.Service"/>, organization hosts on)
    /// the box takes both URL forms and <c>&lt;organization&gt;@</c>, and refuses
    /// <c>app.vssps.visualstudio.com</c> (a label with a dot), another host and an empty segment
    /// without a request. Nothing leaves the machine: the launcher is the fake one, and Azure DevOps'
    /// handler refuses and records every request.
    /// </summary>
    [Fact]
    public async Task On_the_service_s_endpoints_the_box_takes_both_URL_forms_and_refuses_app_vssps()
    {
        using var data = new TempDirectory("sign-in dialog service forms");
        var log = new RecordingLog();
        var launcher = new FakeFileLauncher();
        var attempts = new List<Uri?>();
        var options = HostingOptions.Off with
        {
            AzureDevOps = AzureDevOpsEndpoints.Service,
            CreateAzureDevOpsHandler = () => new RecordingRefusal(attempts),
        };
        using var session = new HostingSession(new AppPaths(data.Path), options, new FakeTokenStore(), log, action => action(), TimeProvider.System);
        await session.StartAsync(Cancelled);

        const string Page = "https://dev.azure.com/Fernhill-Labs/_usersSettings/tokens";
        (string Text, string Page)[] taken =
        [
            ("Fernhill-Labs", Page),
            ("https://dev.azure.com/Fernhill-Labs", Page),
            ("https://dev.azure.com/Fernhill-Labs/Mobile%20App/_git/mobile-app", Page),
            ("https://Fernhill-Labs@dev.azure.com/Fernhill-Labs/Mobile%20App", Page),
            ("https://fernhill-labs.visualstudio.com/", "https://dev.azure.com/fernhill-labs/_usersSettings/tokens"),
        ];
        foreach (var (text, page) in taken)
        {
            var dialog = new SignInDialogViewModel(session, HostingProvider.AzureDevOps, launcher, log);
            await dialog.Opened;
            dialog.Organization = text;
            Assert.True(dialog.CanOpenTokenPage, $"The link is disabled for '{text}'.");
            Assert.True(dialog.OpenTokenPageCommand.CanExecute(null));
            var launches = launcher.Requests.Count;
            await dialog.OpenTokenPageCommand.ExecuteAsync(null);
            Assert.Equal(page, Assert.Single(launcher.Requests.Skip(launches)).FullPath);
        }

        foreach (var text in new[] { "https://app.vssps.visualstudio.com/", "https://example.com/Fernhill-Labs", "https://dev.azure.com/", "https://dev.azure.com//Fernhill-Labs" })
        {
            var dialog = new SignInDialogViewModel(session, HostingProvider.AzureDevOps, launcher, log);
            await dialog.Opened;
            dialog.Organization = text;
            dialog.Token = AzureDevOpsToken;
            Assert.False(dialog.CanOpenTokenPage, $"The link is enabled for '{text}'.");
            Assert.False(dialog.OpenTokenPageCommand.CanExecute(null));
            Assert.True(dialog.ConfirmCommand.CanExecute(null));
            await dialog.ConfirmCommand.ExecuteAsync(null);
            Assert.Equal(PartA.NotAnOrganizationError, dialog.ErrorText);
        }

        Assert.Empty(attempts);
        Assert.Equal(taken.Length, launcher.Requests.Count);
        Assert.Null(session.AccountOf(HostingProvider.AzureDevOps));
        Assert.DoesNotContain(log.Messages, message => message.Contains(AzureDevOpsToken, StringComparison.Ordinal));
    }

    /// <summary>
    /// A token pasted into the organization box (it opens focused while empty) is no organization:
    /// a name of a provider's token form, or equal to the token box's text in any case, is refused
    /// with <c>SignInNotAnOrganization</c>; no request, no launch, no focus moved, and the error does
    /// not echo it (design 1; 10.4).
    /// </summary>
    [Fact]
    public async Task A_token_in_the_organization_box_is_no_organization_and_goes_nowhere()
    {
        await using var setup = await Setup.StartAsync();
        var port = setup.Served.AzureDevOps.Port;
        (string Organization, string Token)[] cases =
        [
            (AzureDevOpsForm, PartA.PlainValue),
            (GitLabToken, PartA.PlainValue),
            ($"http://127.0.0.1:{port}/{AzureDevOpsForm}", PartA.PlainValue),
            (PartA.PlainValue, PartA.PlainValue),
            (" " + PartA.PlainValue.ToUpperInvariant() + " ", PartA.PlainValue),
        ];
        foreach (var (organization, token) in cases)
        {
            var dialog = setup.Dialog(HostingProvider.AzureDevOps);
            await dialog.Opened;
            var changed = new List<string?>();
            var focus = new List<string>();
            dialog.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            dialog.FocusRequested += (_, id) => focus.Add(id);
            dialog.Organization = organization;
            changed.Clear();
            dialog.Token = token;
            Assert.Contains(nameof(SignInDialogViewModel.CanOpenTokenPage), changed);
            Assert.False(dialog.CanOpenTokenPage, $"The link is enabled for the organization of case '{token}'.");
            Assert.False(dialog.OpenTokenPageCommand.CanExecute(null));
            await dialog.OpenTokenPageCommand.ExecuteAsync(null);
            Assert.True(dialog.ConfirmCommand.CanExecute(null));
            await dialog.ConfirmCommand.ExecuteAsync(null);
            Assert.Equal(PartA.NotAnOrganizationError, dialog.ErrorText);
            Assert.Empty(focus);
            Assert.False(dialog.IsClosed);
        }

        // The same name with another token is an organization again.
        var plain = setup.Dialog(HostingProvider.AzureDevOps);
        await plain.Opened;
        plain.Organization = PartA.PlainValue;
        Assert.True(plain.CanOpenTokenPage);
        plain.Token = AzureDevOpsToken;
        Assert.True(plain.CanOpenTokenPage);

        Assert.Empty(setup.AllRequests());
        Assert.Empty(setup.Launcher.Requests);
        Assert.Equal(["probe"], setup.Tokens.Calls.Distinct());
    }

    [Fact]
    public async Task Sign_in_and_the_links_are_enabled_as_10_4_says()
    {
        await using var setup = await Setup.StartAsync();

        var azure = setup.Dialog(HostingProvider.AzureDevOps);
        await azure.Opened;
        Assert.True(azure.ShowsOrganization);
        Assert.False(azure.ShowsLegacyLink);
        Assert.Equal(PartA.AzureDevOpsDialogTitle, azure.Title);
        Assert.False(azure.ConfirmCommand.CanExecute(null));
        Assert.False(azure.CanOpenTokenPage);
        azure.Token = AzureDevOpsToken;
        Assert.False(azure.ConfirmCommand.CanExecute(null));
        azure.Organization = "  ";
        Assert.False(azure.ConfirmCommand.CanExecute(null));
        azure.Organization = PartA.Organization;
        Assert.True(azure.ConfirmCommand.CanExecute(null));
        Assert.True(azure.CanOpenTokenPage);

        var gitLab = setup.Dialog(HostingProvider.GitLab);
        await gitLab.Opened;
        Assert.False(gitLab.ShowsOrganization);
        Assert.True(gitLab.ShowsLegacyLink);
        Assert.True(gitLab.CanOpenTokenPage);
        Assert.Equal((PartA.GitLabDialogTitle, PartA.GitLabHelp, PartA.GitLabTokenLink, PartA.GitLabTokenLinkTip), (gitLab.Title, gitLab.HelpText, gitLab.TokenLinkText, gitLab.TokenLinkTip));
        Assert.False(gitLab.ConfirmCommand.CanExecute(null));
        gitLab.Token = " x ";
        Assert.True(gitLab.ConfirmCommand.CanExecute(null));

        var gitHub = setup.Dialog(HostingProvider.GitHub);
        await gitHub.Opened;
        Assert.False(gitHub.ShowsOrganization);
        Assert.False(gitHub.ShowsLegacyLink);
        Assert.Equal((PartA.GitHubDialogTitle, PartA.GitHubHelp, PartA.GitHubTokenLink), (gitHub.Title, gitHub.HelpText, gitHub.TokenLinkText));
        Assert.Equal(string.Empty, gitHub.Organization);
        Assert.Empty(setup.AllRequests());
    }

    [Fact]
    public async Task Each_failure_has_its_provider_s_text_and_selects_its_box()
    {
        await using var setup = await Setup.StartAsync();

        // GitLab's 401 to a glpat- token it never issued (MEASURED 2026-11-06, D373).
        var gitLab = setup.Dialog(HostingProvider.GitLab);
        await gitLab.Opened;
        var focus = new List<string>();
        gitLab.FocusRequested += (_, id) => focus.Add(id);
        gitLab.Token = PartA.UnknownGitLabToken;
        await gitLab.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal(PartA.GitLabRefused401, gitLab.ErrorText);
        Assert.Equal(["SignInToken"], focus);

        // Azure DevOps: the empty 401, the Anonymous 200 and the unknown organization's 404 (WP-LF's copies of stage 1).
        var cases = new (string Organization, string Token, string Text, string Box)[]
        {
            (PartA.Organization, PartA.UnknownAzureDevOpsToken, PartA.AzureDevOpsRefused, "SignInToken"),
            (PartA.Organization, PartA.PlainValue, PartA.AzureDevOpsAnonymous, "SignInToken"),
            (PartA.UnknownOrganization, AzureDevOpsToken, PartA.AzureDevOpsNoOrganization, "SignInOrganization"),
        };
        foreach (var (organization, token, text, box) in cases)
        {
            var azure = setup.Dialog(HostingProvider.AzureDevOps);
            await azure.Opened;
            focus.Clear();
            azure.FocusRequested += (_, id) => focus.Add(id);
            azure.Organization = organization;
            azure.Token = token;
            var mark = setup.Served.AzureDevOps.Requests.Count;
            await azure.ConfirmCommand.ExecuteAsync(null);
            Assert.Equal(text, azure.ErrorText);
            Assert.Equal([box], focus);
            Assert.False(azure.IsClosed);
            Assert.Equal((organization, token), (azure.Organization, azure.Token));
            var request = Assert.Single(setup.Served.AzureDevOps.Requests.Skip(mark));
            Assert.Equal("connection:" + organization.ToLowerInvariant(), request.Route);
        }

        Assert.Null(setup.Session.AccountOf(HostingProvider.AzureDevOps));
        Assert.Null(setup.Session.AccountOf(HostingProvider.GitLab));
        Assert.Equal(["probe"], setup.Tokens.Calls.Distinct());
    }

    [Theory]
    [InlineData(HostingProvider.GitHub)]
    [InlineData(HostingProvider.GitLab)]
    [InlineData(HostingProvider.AzureDevOps)]
    public async Task Another_provider_s_form_is_refused_unsent_and_the_dialog_s_own_goes_on(HostingProvider provider)
    {
        await using var setup = await Setup.StartAsync();
        var forms = new Dictionary<HostingProvider, string>
        {
            [HostingProvider.GitHub] = GitHubToken,
            [HostingProvider.GitLab] = GitLabToken,
            [HostingProvider.AzureDevOps] = AzureDevOpsForm,
        };
        foreach (var (other, token) in forms.Where(form => form.Key != provider))
        {
            var dialog = setup.Dialog(provider);
            await dialog.Opened;
            dialog.Organization = PartA.Organization;
            dialog.Token = token;
            await dialog.ConfirmCommand.ExecuteAsync(null);
            var organization = provider == HostingProvider.AzureDevOps ? PartA.Organization : null;
            Assert.Equal(VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts.SignInOtherProviderTokenOf(provider, other, organization), dialog.ErrorText);
            Assert.StartsWith(other == HostingProvider.AzureDevOps ? "This looks like an Azure DevOps token." : $"This looks like a {HostingProviders.DisplayName(other)} token.", dialog.ErrorText, StringComparison.Ordinal);
            Assert.Empty(setup.AllRequests());
        }

        // Its own form goes on to its fake: no dialog requires its own form, none refuses it.
        var own = setup.Dialog(provider);
        await own.Opened;
        own.Organization = PartA.Organization;
        own.Token = forms[provider];
        await own.ConfirmCommand.ExecuteAsync(null);
        Assert.NotEmpty(setup.RequestsOf(provider));
        Assert.All(HostingProviders.InAccountsOrder.Where(other => other != provider), other => Assert.Empty(setup.RequestsOf(other)));
        if (provider == HostingProvider.AzureDevOps)
        {
            // The made-up 84-character form was never issued: the fake's empty 401.
            Assert.Equal(PartA.AzureDevOpsRefused, own.ErrorText);
        }
        else
        {
            Assert.True(own.IsClosed, $"{provider}'s own token did not sign in.");
            Assert.Equal(AccountState.SignedIn, setup.Session.AccountOf(provider)?.State);
        }
    }

    [Fact]
    public async Task The_login_keeps_the_server_s_spelling_after_lower_case_is_typed()
    {
        await using var setup = await Setup.StartAsync();
        var dialog = setup.Dialog(HostingProvider.AzureDevOps);
        await dialog.Opened;
        dialog.Organization = PartA.OrganizationLower;
        dialog.Token = AzureDevOpsToken;

        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.True(dialog.IsClosed);
        var account = setup.Session.AccountOf(HostingProvider.AzureDevOps);
        Assert.Equal(PartA.Organization, account?.Account.Login);
        Assert.Equal("fernhill-labs", account?.Account.Key.Login.ToLowerInvariant());
        Assert.Equal(PartA.AzureDevOpsSignInRoutes(PartA.OrganizationLower), setup.Served.AzureDevOps.Requests.Select(request => request.Route));
        Assert.Equal(["probe", "save " + PartA.AzureDevOpsKey(setup.Served.AzureDevOps.Port)], setup.Tokens.Calls);
    }

    [Fact]
    public async Task The_links_open_each_provider_s_page_and_Azure_DevOps_needs_an_organization()
    {
        await using var setup = await Setup.StartAsync();
        var served = setup.Served;

        var gitLab = setup.Dialog(HostingProvider.GitLab);
        await gitLab.OpenTokenPageCommand.ExecuteAsync(null);
        await gitLab.OpenLegacyTokenPageCommand.ExecuteAsync(null);
        var gitHub = setup.Dialog(HostingProvider.GitHub);
        await gitHub.OpenTokenPageCommand.ExecuteAsync(null);
        var azure = setup.Dialog(HostingProvider.AzureDevOps);
        await azure.OpenTokenPageCommand.ExecuteAsync(null);
        azure.Organization = PartA.NotAnOrganization;
        await azure.OpenTokenPageCommand.ExecuteAsync(null);
        Assert.False(azure.OpenTokenPageCommand.CanExecute(null));
        azure.Organization = PartA.Organization;
        Assert.True(azure.OpenTokenPageCommand.CanExecute(null));
        await azure.OpenTokenPageCommand.ExecuteAsync(null);
        await azure.OpenLegacyTokenPageCommand.ExecuteAsync(null);

        Assert.Equal(
            [
                PartA.GitLabTokenPage(served.GitLab.Port), PartA.GitLabLegacyTokenPage(served.GitLab.Port), PartA.GitHubTokenPage(served.GitHub.Port),
                PartA.AzureDevOpsTokenPage(served.AzureDevOps.Port),
            ],
            setup.Launcher.Requests.Select(request => request.FullPath));
        Assert.Empty(setup.AllRequests());
    }

    /// <summary>A handler that records every request's address and refuses it before it is sent: nothing leaves the machine.</summary>
    private sealed class RecordingRefusal(List<Uri?> attempts) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (attempts)
            {
                attempts.Add(request.RequestUri);
            }

            throw new HttpRequestException("Refused by the test: nothing is sent.");
        }
    }

    /// <summary>A hosting session for the three providers over the scenario "hosting7b" served on free ports, its data folder, keychain, log and launcher.</summary>
    private sealed class Setup : IAsyncDisposable
    {
        private Setup(ServedHosting7b served, FakeTokenStore tokens)
        {
            Served = served;
            Tokens = tokens;
            var options = new HostingOptions(served.GitHub.Endpoints, served.GitHub.CreateHandler)
            {
                AzureDevOps = served.AzureDevOps.Endpoints,
                CreateAzureDevOpsHandler = served.AzureDevOps.CreateHandler,
                GitLab = served.GitLab.Endpoints,
                CreateGitLabHandler = served.GitLab.CreateHandler,
            };
            Session = new HostingSession(new AppPaths(Data.Path), options, tokens, Log, action => action(), TimeProvider.System);
        }

        public ServedHosting7b Served { get; }

        public FakeTokenStore Tokens { get; }

        public TempDirectory Data { get; } = new("sign-in dialog providers");

        public RecordingLog Log { get; } = new();

        public FakeFileLauncher Launcher { get; } = new();

        public HostingSession Session { get; }

        public static async Task<Setup> StartAsync()
        {
            var copy = await Scenarios.Hosting7bAsync();
            try
            {
                var served = await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
                var setup = new Setup(served, new FakeTokenStore());
                await setup.Session.StartAsync(Cancelled);
                return setup;
            }
            catch
            {
                copy.Dispose();
                throw;
            }
        }

        public SignInDialogViewModel Dialog(HostingProvider provider) => new(Session, provider, Launcher, Log);

        public IReadOnlyList<string> AllRequests() =>
            [.. Served.AzureDevOps.Requests.Select(request => request.Path), .. Served.GitLab.Requests.Select(request => request.Path), .. Served.GitHub.Requests.Select(request => request.Path)];

        public IReadOnlyList<string> RequestsOf(HostingProvider provider) => provider switch
        {
            HostingProvider.AzureDevOps => [.. Served.AzureDevOps.Requests.Select(request => request.Path)],
            HostingProvider.GitLab => [.. Served.GitLab.Requests.Select(request => request.Path)],
            _ => [.. Served.GitHub.Requests.Select(request => request.Path)],
        };

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            try
            {
                foreach (var token in new[] { AzureDevOpsToken, GitLabToken, GitLabLegacyToken, GitHubToken, AzureDevOpsForm, PartA.UnknownGitLabToken, PartA.UnknownAzureDevOpsToken, PartA.PlainValue })
                {
                    TokenTripwire.AssertAbsent(token, [Data.Path], null);
                    Assert.DoesNotContain(Log.Messages, message => message.Contains(token, StringComparison.Ordinal));
                }

                Assert.Empty(Served.AzureDevOps.Errors);
                Assert.Empty(Served.GitLab.Errors);
                Assert.Empty(Served.GitHub.Errors);
            }
            finally
            {
                await Served.DisposeAsync();
                Data.Dispose();
            }
        }
    }
}
