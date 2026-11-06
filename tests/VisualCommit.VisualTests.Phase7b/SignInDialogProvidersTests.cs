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
/// them, another provider's token form refused unsent in each dialog, bare or wrapped, while its
/// own goes on and no own token of any form is refused so, the login in the server's spelling, and
/// the links' addresses. The session is the real one over the
/// scenario "hosting7b" served by the three fakes on free ports (port 0), with a <see cref="FakeTokenStore"/>;
/// the service's URL forms are read by a session on <see cref="AzureDevOpsEndpoints.Service"/> whose
/// handler refuses every request, so that nothing leaves the machine.
/// </summary>
public sealed class SignInDialogProvidersTests
{
    private static readonly string AzureDevOpsForm = new string('a', 76) + "AZDO" + "abcd";

    /// <summary>A made-up value of Azure DevOps' legacy token shape, 52 characters of base32, which <see cref="HostingProviders.TokenFormOf"/> leaves as a form of none.</summary>
    private static readonly string LegacyAzureDevOpsShape = string.Concat(Enumerable.Repeat("mnbvcxzlkjhgfdsapoiuytrewq234567", 2))[..52];

    /// <summary>A made-up value of GitHub's classic token shape, 40 hexadecimal digits, a form of none too.</summary>
    private static readonly string ClassicGitHubShape = string.Concat(Enumerable.Repeat("a1b2c3d4e5", 4));

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
        Assert.False(log.Messages.Any(message => message.Contains(AzureDevOpsToken, StringComparison.Ordinal)), "The log holds AzureDevOpsToken.");
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

    /// <summary>
    /// A token of a shape <see cref="HostingProviders.TokenFormOf"/> leaves as a form of none, which the
    /// organization's grammar takes (Azure DevOps' legacy 52 characters of base32, GitHub's classic 40
    /// hexadecimal digits, in any case), is no organization in the box either: the link stays
    /// disabled with the token box empty, and Sign in gives <c>SignInNotAnOrganization</c> with the
    /// organization's name, a token or GitHub's form in the token box (the boxes swapped); no request,
    /// no launch, no focus moved, and neither the error, the log nor the data folder holds it (design 1; 10.4).
    /// </summary>
    [Fact]
    public async Task A_token_of_no_form_in_the_organization_box_is_no_organization_and_goes_nowhere()
    {
        await using var setup = await Setup.StartAsync();
        foreach (var shape in new[] { LegacyAzureDevOpsShape, ClassicGitHubShape, LegacyAzureDevOpsShape.ToUpperInvariant(), ClassicGitHubShape.ToUpperInvariant() })
        {
            // The grammar takes it and no form is known: the refusal is the dialog's own.
            Assert.Null(HostingProviders.TokenFormOf(shape));
            Assert.True(AzureDevOpsProvider.OrganizationOf(shape, setup.Served.AzureDevOps.Endpoints) is not null, $"The grammar refuses a token's shape of {shape.Length} characters.");

            var link = setup.Dialog(HostingProvider.AzureDevOps);
            await link.Opened;
            link.Organization = shape;
            Assert.False(link.CanOpenTokenPage, $"The link is enabled for a token's shape of {shape.Length} characters.");
            Assert.False(link.OpenTokenPageCommand.CanExecute(null));
            await link.OpenTokenPageCommand.ExecuteAsync(null);

            foreach (var token in new[] { PartA.Organization, AzureDevOpsToken, GitHubToken })
            {
                var dialog = setup.Dialog(HostingProvider.AzureDevOps);
                await dialog.Opened;
                var focus = new List<string>();
                dialog.FocusRequested += (_, id) => focus.Add(id);
                dialog.Organization = shape;
                dialog.Token = token;
                Assert.False(dialog.CanOpenTokenPage, $"The link is enabled for a token's shape of {shape.Length} characters.");
                Assert.True(dialog.ConfirmCommand.CanExecute(null));
                await dialog.ConfirmCommand.ExecuteAsync(null);
                Assert.True(dialog.ErrorText == PartA.NotAnOrganizationError, $"A token's shape of {shape.Length} characters did not give SignInNotAnOrganization.");
                Assert.Empty(focus);
                Assert.False(dialog.IsClosed);
            }
        }

        Assert.Empty(setup.AllRequests());
        Assert.Empty(setup.Launcher.Requests);
        Assert.Equal(["probe"], setup.Tokens.Calls.Distinct());
        Assert.Null(setup.Session.AccountOf(HostingProvider.AzureDevOps));
    }

    /// <summary>
    /// Cancel (Esc too, and the session's end through <c>CancelAll</c>, which run the same
    /// <c>OnCancel</c>) during GitLab's check or either of Azure DevOps' two reads: the dialog closes
    /// at once with no result, the held request ends with the dialog's cancellation before its hold is
    /// released (in process: abandoned, never answered), no read follows it, and nothing is signed in
    /// or kept: no account, the store asked only its probe, no <c>accounts.json</c> (7a's A3 per provider, 10.4).
    /// </summary>
    [Theory]
    [InlineData(HostingProvider.GitLab, "user")]
    [InlineData(HostingProvider.AzureDevOps, "connection:fernhill-labs")]
    [InlineData(HostingProvider.AzureDevOps, "areas")]
    public async Task Cancel_during_the_check_cancels_its_request_and_nothing_changes(HostingProvider provider, string route)
    {
        await using var setup = await Setup.StartAsync();
        var dialog = setup.Dialog(provider);
        await dialog.Opened;
        FakeHostingService fake = provider == HostingProvider.GitLab ? setup.Served.GitLab : setup.Served.AzureDevOps;
        using var hold = fake.HoldNext(route);

        dialog.Organization = PartA.Organization;
        dialog.Token = provider == HostingProvider.GitLab ? GitLabToken : AzureDevOpsToken;
        var checking = dialog.ConfirmCommand.ExecuteAsync(null);
        await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        Assert.True(dialog.IsChecking);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));

        dialog.CancelCommand.Execute(null);

        Assert.True(dialog.IsClosed);
        Assert.Null(await dialog.Result);

        // The held request ends with the dialog's cancellation, before the hold is released.
        await checking.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        await hold.Abandoned.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        Assert.False(dialog.IsChecking);
        Assert.Null(setup.Session.AccountOf(provider));
        Assert.False(File.Exists(Path.Combine(setup.Data.Path, "accounts.json")));
        Assert.Equal(["probe"], setup.Tokens.Calls);

        // Only what comes before the held read was answered: Azure DevOps' connectionData, 200, when
        // the resource area was held; nothing after it, and nothing at the other fakes.
        if (route == "areas")
        {
            var connection = Assert.Single(fake.Requests);
            Assert.Equal(("connection:fernhill-labs", 200), (connection.Route, connection.Status));
        }
        else
        {
            Assert.Empty(fake.Requests);
        }

        Assert.All(HostingProviders.InAccountsOrder.Where(other => other != provider), other => Assert.Empty(setup.RequestsOf(other)));
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
            Assert.Equal(organization, azure.Organization);
            Assert.True(string.Equals(token, azure.Token, StringComparison.Ordinal), $"The token box does not keep the token typed ({azure.Token.Length} characters, {token.Length} typed).");
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

    /// <summary>
    /// Another provider's token wrapped (in quotes or angle brackets, a <c>.env</c> line, a clone URL,
    /// JSON) is refused unsent as a bare one (D378; the lead's ruling of 2026-11-06, Hunt 3): the
    /// dialog shows <c>SignInOtherProviderTokenOf</c>, no fake is asked, and neither the error nor the
    /// log holds the value (the setup's end checks the log and the data folder for each form).
    /// </summary>
    [Theory]
    [InlineData(HostingProvider.GitHub)]
    [InlineData(HostingProvider.GitLab)]
    [InlineData(HostingProvider.AzureDevOps)]
    public async Task Another_provider_s_form_wrapped_is_refused_unsent_as_a_bare_one(HostingProvider provider)
    {
        await using var setup = await Setup.StartAsync();
        var forms = new Dictionary<HostingProvider, string>
        {
            [HostingProvider.GitHub] = GitHubToken,
            [HostingProvider.GitLab] = GitLabToken,
            [HostingProvider.AzureDevOps] = AzureDevOpsForm,
        };
        var organization = provider == HostingProvider.AzureDevOps ? PartA.Organization : null;
        foreach (var (other, token) in forms.Where(form => form.Key != provider))
        {
            for (var which = 0; which < Wrappers.Length; which++)
            {
                var value = Wrappers[which](token);
                var dialog = setup.Dialog(provider);
                await dialog.Opened;
                dialog.Organization = PartA.Organization;
                dialog.Token = value;
                Assert.True(dialog.ConfirmCommand.CanExecute(null));
                await dialog.ConfirmCommand.ExecuteAsync(null);
                Assert.True(
                    dialog.ErrorText == VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts.SignInOtherProviderTokenOf(provider, other, organization),
                    $"{provider}'s dialog given {other}'s form in wrapper {which} did not refuse it as another provider's token.");
                Assert.False(dialog.ErrorText!.Contains(token, StringComparison.Ordinal), "The error echoes the token.");
                Assert.False(dialog.IsClosed);
                Assert.True(string.Equals(value, dialog.Token, StringComparison.Ordinal), $"Wrapper {which}: the box does not keep what was typed.");
                Assert.Empty(setup.AllRequests());
            }
        }

        Assert.Null(setup.Session.AccountOf(provider));
        Assert.Equal(["probe"], setup.Tokens.Calls.Distinct());
    }

    /// <summary>
    /// No token of a dialog's own provider is refused as holding another's form: random values of
    /// each of its forms (GitHub's prefixed and classic ones, GitLab's fine-grained, newer and legacy
    /// ones, Azure DevOps' 84-character and legacy ones) and edge values that hold another provider's
    /// prefix or Azure DevOps' mark inside, after <c>_</c>, <c>-</c> or <c>.</c>, are each one run of
    /// token characters whose form is the provider's own or none (<see cref="SignInDialogViewModel.OtherProviderFormIn"/>).
    /// </summary>
    [Fact]
    public void No_token_of_the_dialog_s_own_provider_is_refused_as_another_s()
    {
        var random = new Random(378);
        foreach (var provider in HostingProviders.InAccountsOrder)
        {
            var own = OwnTokens(provider, random).ToList();
            Assert.True(own.Count > 1000);
            for (var i = 0; i < own.Count; i++)
            {
                Assert.True(
                    SignInDialogViewModel.OtherProviderFormIn(own[i], provider) is null,
                    $"{provider}'s own token {i} ({own[i].Length} characters) is refused as another provider's.");
            }
        }
    }

    /// <summary>
    /// A value with no character outside the token characters is one run, and only the whole value's
    /// form counts (D378's rule as it was): random values of letters, digits, <c>_</c>, <c>-</c> and
    /// <c>.</c> with the providers' prefixes and Azure DevOps' mark spliced in at random places.
    /// </summary>
    [Fact]
    public void Within_one_run_of_token_characters_only_the_whole_value_s_form_counts()
    {
        var random = new Random(2026);
        string[] pieces = ["ghp_", "gho_", "ghu_", "ghs_", "ghr_", "github_pat_", "glpat-", "AZDO", "_", "-", ".", "x", "7"];
        foreach (var provider in HostingProviders.InAccountsOrder)
        {
            for (var i = 0; i < 4000; i++)
            {
                var value = new System.Text.StringBuilder();
                var length = random.Next(1, 130);
                while (value.Length < length)
                {
                    value.Append(random.Next(3) == 0 ? pieces[random.Next(pieces.Length)] : TokenCharacters[random.Next(TokenCharacters.Length)].ToString());
                }

                // Every fourth value 84 long with the mark at 76, the length of Azure DevOps' form.
                if (i % 4 == 0)
                {
                    while (value.Length < 84)
                    {
                        value.Append(TokenCharacters[random.Next(TokenCharacters.Length)]);
                    }

                    value.Length = 84;
                    value.Remove(76, 4).Insert(76, "AZDO");
                }

                var text = value.ToString();
                var whole = HostingProviders.TokenFormOf(text) is { } form && form != provider ? form : (HostingProvider?)null;
                Assert.True(
                    SignInDialogViewModel.OtherProviderFormIn(text, provider) == whole,
                    $"{provider}'s dialog: value {i} ({text.Length} characters) is judged otherwise than its whole value.");
            }
        }
    }

    /// <summary>
    /// Another provider's form between other characters is found wherever it lies, the first one when
    /// there are two; the dialog's own form so wrapped is not refused. Random values of each form,
    /// random separators and runs of digits around them.
    /// </summary>
    [Fact]
    public void Another_provider_s_form_between_other_characters_is_found_anywhere()
    {
        var random = new Random(1378);
        const string Separators = "\"'<>()[]{}=:@/,;!#$%&*+?^|~`\\";
        foreach (var provider in HostingProviders.InAccountsOrder)
        {
            foreach (var other in HostingProviders.InAccountsOrder)
            {
                for (var i = 0; i < 500; i++)
                {
                    var token = FormOf(other, random);
                    var left = random.Next(3) switch
                    {
                        0 => string.Empty,
                        1 => Separators[random.Next(Separators.Length)].ToString(),
                        _ => Digits(random, random.Next(1, 30)) + Separators[random.Next(Separators.Length)],
                    };
                    var right = random.Next(3) switch
                    {
                        0 => string.Empty,
                        1 => Separators[random.Next(Separators.Length)].ToString(),
                        _ => Separators[random.Next(Separators.Length)] + Digits(random, random.Next(1, 30)),
                    };
                    var expected = other == provider ? (HostingProvider?)null : other;
                    Assert.True(
                        SignInDialogViewModel.OtherProviderFormIn(left + token + right, provider) == expected,
                        $"{provider}'s dialog: {other}'s form {i} between other characters is judged otherwise ({left.Length} before, {right.Length} after).");
                }
            }

            // Two others' forms: the first is named.
            var others = HostingProviders.InAccountsOrder.Where(other => other != provider).ToList();
            var both = $"A={FormOf(others[0], random)};B=<{FormOf(others[1], random)}>";
            Assert.Equal(others[0], SignInDialogViewModel.OtherProviderFormIn(both, provider));
            var swapped = $"B=<{FormOf(others[1], random)}>;A={FormOf(others[0], random)}";
            Assert.Equal(others[1], SignInDialogViewModel.OtherProviderFormIn(swapped, provider));
        }
    }

    /// <summary>The ways a token is pasted wrapped (Hunt 3): quotes, angle brackets, a <c>.env</c> line, clone URLs, JSON, a header without its space.</summary>
    private static readonly Func<string, string>[] Wrappers =
    [
        token => $"\"{token}\"",
        token => $"'{token}'",
        token => $"<{token}>",
        token => $"`{token}`",
        token => $"({token})",
        token => $"GITLAB_TOKEN={token}",
        token => $"GITHUB_TOKEN=\"{token}\"",
        token => $"AZURE_DEVOPS_EXT_PAT={token};",
        token => $"https://oauth2:{token}@127.0.0.1:47398/northfield/platform/web-app.git",
        token => $"https://x-access-token:{token}@127.0.0.1:47399/octo/web-app.git",
        token => $"https://{token}@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app",
        token => $"{{\"token\":\"{token}\"}}",
        token => $"Authorization:Bearer:{token}",
        token => $"{token},",
    ];

    /// <summary>The characters of the providers' tokens: letters, digits, <c>_</c>, <c>-</c> and <c>.</c>.</summary>
    private const string TokenCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.";

    private const string Alphanumerics = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private static string Of(Random random, string alphabet, int length)
    {
        var characters = new char[length];
        for (var i = 0; i < length; i++)
        {
            characters[i] = alphabet[random.Next(alphabet.Length)];
        }

        return new string(characters);
    }

    private static string Digits(Random random, int length) => Of(random, "0123456789", length);

    /// <summary>A random made-up value of <paramref name="provider"/>'s form by <see cref="HostingProviders.TokenFormOf"/>.</summary>
    private static string FormOf(HostingProvider provider, Random random) => provider switch
    {
        HostingProvider.GitHub => random.Next(2) == 0 ? "ghp_" + Of(random, Alphanumerics, 36) : "github_pat_" + Of(random, Alphanumerics, 22) + "_" + Of(random, Alphanumerics, 59),
        HostingProvider.GitLab => "glpat-" + Of(random, Alphanumerics + "_-", 20),
        _ => Of(random, Alphanumerics, 76) + "AZDO" + Of(random, Alphanumerics, 4),
    };

    /// <summary>Made-up tokens of <paramref name="provider"/>'s own forms, random and at the edges.</summary>
    private static IEnumerable<string> OwnTokens(HostingProvider provider, Random random)
    {
        const string Base64Url = Alphanumerics + "_-";
        const string Base32 = "abcdefghijklmnopqrstuvwxyz234567";
        for (var i = 0; i < 400; i++)
        {
            switch (provider)
            {
                case HostingProvider.GitHub:
                    foreach (var prefix in new[] { "ghp_", "gho_", "ghu_", "ghs_", "ghr_" })
                    {
                        yield return prefix + Of(random, Alphanumerics, 36);
                    }

                    yield return "github_pat_" + Of(random, Alphanumerics, 22) + "_" + Of(random, Alphanumerics, 59);
                    yield return Of(random, "0123456789abcdef", 40);
                    break;
                case HostingProvider.GitLab:
                    yield return "glpat-" + Of(random, Base64Url, 20);
                    yield return "glpat-" + Of(random, Base64Url, 27) + ".01." + Of(random, "0123456789abcdefghijklmnopqrstuvwxyz", 9);
                    yield return "glpat-" + Of(random, Base64Url, random.Next(20, 120));
                    yield return Of(random, Alphanumerics, 20);
                    break;
                default:
                    yield return Of(random, Alphanumerics, 76) + "AZDO" + Of(random, Alphanumerics, 4);
                    yield return Of(random, Base32, 52);
                    yield return Of(random, Base32.ToUpperInvariant(), 52);
                    break;
            }
        }

        // The edges: another provider's prefix or Azure DevOps' mark inside the token, after the
        // token characters a naive search would take as a boundary.
        var mark = Of(random, Alphanumerics, 76) + "AZDO" + Of(random, Alphanumerics, 4);
        switch (provider)
        {
            case HostingProvider.GitHub:
                yield return "ghp_" + Of(random, Alphanumerics, 72) + "AZDO" + Of(random, Alphanumerics, 4);
                yield return "github_pat_" + Of(random, Alphanumerics, 10) + "_ghp_" + Of(random, Alphanumerics, 36);
                yield return "github_pat_" + Of(random, Alphanumerics, 22) + "_" + mark;
                yield return "ghp_glpat" + Of(random, Alphanumerics, 31);
                yield return "ghp_ghp_" + Of(random, Alphanumerics, 32);
                break;
            case HostingProvider.GitLab:
                yield return "glpat-x_ghp_" + Of(random, Alphanumerics, 36);
                yield return "glpat--ghp_" + Of(random, Alphanumerics, 36);
                yield return "glpat-.github_pat_" + Of(random, Alphanumerics, 22) + "_" + Of(random, Alphanumerics, 59);
                yield return "glpat-" + Of(random, Base64Url, 10) + "." + mark;
                yield return "glpat-" + Of(random, Base64Url, 10) + "-" + mark;
                yield return "glpat-" + Of(random, Base64Url, 10) + "_" + mark + "." + Of(random, Base64Url, 5);
                yield return "glpat-" + Of(random, Alphanumerics, 70) + "AZDO" + Of(random, Alphanumerics, 4);
                yield return "glpat-" + Of(random, Base64Url, 5) + ".glpat-" + Of(random, Base64Url, 20);
                break;
            default:
                yield return "ghpglpatgithubpat" + Of(random, Alphanumerics, 59) + "AZDO" + Of(random, Alphanumerics, 4);
                yield return "AZDO" + Of(random, Alphanumerics, 72) + "AZDO" + "AZDO";
                yield return Of(random, Base32, 52).Replace('a', 'g');
                break;
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
                foreach (var token in new[] { AzureDevOpsToken, GitLabToken, GitLabLegacyToken, GitHubToken, AzureDevOpsForm, PartA.UnknownGitLabToken, PartA.UnknownAzureDevOpsToken, PartA.PlainValue, LegacyAzureDevOpsShape, ClassicGitHubShape })
                {
                    TokenTripwire.AssertAbsent(token, [Data.Path], null);
                    Assert.False(Log.Messages.Any(message => message.Contains(token, StringComparison.Ordinal)), $"The log holds a made-up token ({token.Length} characters).");
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
