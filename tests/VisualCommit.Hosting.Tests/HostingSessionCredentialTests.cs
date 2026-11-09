using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The account's token for git and refusals (docs/phase-7a-design.md 7.6, 7.7, 7.13; rulings 4, 6,
/// D330, D332): git's request by key, state and switch; the API's 401 marking the account at once
/// and for the next start; git's erase marking it only when <c>GET /user</c> then answers 401,
/// checked once at a time; the bindings of a dashboard row and of a clone. For three providers
/// (docs/phase-7b-design.md 9.5, 9.6): the token by provider and key, one provider's 401 marking it
/// alone, each provider's check of its own with Azure DevOps' organization, another user ending
/// refused and a record without its user taking the check's, and the bindings' union.
/// </summary>
public sealed class HostingSessionCredentialTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gits_request_gets_the_token_only_for_the_signed_in_account_with_the_switch_on()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        var octo = HostingSessionStartTests.OctoKey(setup);
        await setup.Session.StartAsync(Cancelled);
        Assert.Null(await setup.Session.GetAsync(octo, Cancelled));

        await setup.SignInAsync();

        var credential = await setup.Session.GetAsync(octo, Cancelled);
        Assert.Equal((GitCredential.UserName, setup.OctoToken), (GitCredential.UserName, credential!.Token));
        Assert.Null(await setup.Session.GetAsync(new AccountKey(HostingProvider.GitHub, octo.Host, "ada"), Cancelled));
        Assert.Null(await setup.Session.GetAsync(new AccountKey(HostingProvider.GitHub, "github.com", "octo"), Cancelled));

        setup.Session.SetUseForGit(false);
        await setup.Session.Switching.WaitAsync(Cancelled);
        Assert.Null(await setup.Session.GetAsync(octo, Cancelled));
        Assert.False(Load(setup).UseForGit);

        setup.Session.SetUseForGit(true);
        await setup.Session.Switching.WaitAsync(Cancelled);
        Assert.NotNull(await setup.Session.GetAsync(octo, Cancelled));
        Assert.True(Load(setup).UseForGit);
    }

    [Fact]
    public async Task The_apis_401_marks_the_account_refused_and_the_next_start_reads_no_store()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        setup.Fake.Revoke(setup.OctoToken);

        var listed = await setup.Session.ListRepositoriesAsync(Cancelled);

        Assert.Equal(HostingFailureKind.BadCredentials, listed.Failure!.Kind);
        var account = setup.Session.Account!;
        Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.Refused, (TokenPlace?)null), (account.State, account.NoteKind, account.Place));
        Assert.Null(await setup.Session.GetAsync(account.Account.Key, Cancelled));
        Assert.False(setup.Session.BindsGit);
        Assert.Equal(AccountTokenStates.Refused, Load(setup).TokenState);
        Assert.Contains(setup.Warnings, warning => warning.StartsWith("GitHub account octo on 127.0.0.1:1: GitHub refused the token (the API, request ", StringComparison.Ordinal));

        // Signed out of use: no request at all (D257).
        var requests = setup.Fake.Requests.Count;
        Assert.Equal(("Not signed in.", requests), ((await setup.Session.ListRepositoriesAsync(Cancelled)).Failure!.Message, setup.Fake.Requests.Count));

        var reads = setup.FakeTokens.Calls.Count(call => call.StartsWith("read", StringComparison.Ordinal));
        var next = setup.NewSession();
        await next.StartAsync(Cancelled);
        Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.Refused), (next.Account!.State, next.Account.NoteKind));
        Assert.Equal(reads, setup.FakeTokens.Calls.Count(call => call.StartsWith("read", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Gits_erase_marks_the_account_only_when_github_then_refuses_the_token()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var octo = HostingSessionStartTests.OctoKey(setup);
        var written = File.GetLastWriteTimeUtc(setup.Paths.AccountsFile);
        var file = File.ReadAllText(setup.Paths.AccountsFile);

        // The token is valid: an http.extraHeader or a redirect made git erase it (D330). Each step
        // waits for the check its Refused started to end: by then its warning, or its mark and its
        // warning, are in, and a later Refused starts a check of its own instead of joining it.
        setup.Session.Refused(octo);
        await CheckedAsync(setup);
        Assert.Equal("GitHub account octo on 127.0.0.1:1: git was refused, yet the token is valid (an http.extraHeader, a proxy or a redirect?).", setup.Warnings[^1]);
        Assert.Equal(AccountState.SignedIn, setup.Session.Account!.State);

        // The network fails: the account stays too. The client's warning of the request (6.2) comes
        // before the session's (7.6).
        var valid = setup.Session.RefusalCheck;
        setup.NetworkDown = true;
        setup.Session.Refused(octo);
        Assert.NotSame(valid, setup.Session.RefusalCheck);
        await CheckedAsync(setup);
        setup.NetworkDown = false;
        Assert.Equal("GitHub: GET /api/v3/user failed: Network (HttpRequestException).", setup.Warnings[^2]);
        Assert.StartsWith("GitHub account octo on 127.0.0.1:1: git was refused; the token could not be checked: ", setup.Warnings[^1], StringComparison.Ordinal);
        Assert.Equal(AccountState.SignedIn, setup.Session.Account!.State);
        Assert.Equal((written, file), (File.GetLastWriteTimeUtc(setup.Paths.AccountsFile), File.ReadAllText(setup.Paths.AccountsFile)));

        // Another account's erase is ignored: no check starts, so no request.
        var requests = setup.UserRequests;
        var check = setup.Session.RefusalCheck;
        setup.Session.Refused(new AccountKey(HostingProvider.GitHub, octo.Host, "ada"));
        Assert.Same(check, setup.Session.RefusalCheck);

        // Revoked: GET /user answers 401 and the account needs signing in, its source git.
        setup.Fake.Revoke(setup.OctoToken);
        setup.Session.Refused(octo);
        Assert.NotSame(check, setup.Session.RefusalCheck);
        await CheckedAsync(setup);
        Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.Refused), (setup.Session.Account!.State, setup.Session.Account.NoteKind));
        Assert.Equal(AccountTokenStates.Refused, Load(setup).TokenState);
        Assert.Contains("GitHub account octo on 127.0.0.1:1: GitHub refused the token (git); it needs signing in again.", setup.Warnings);
        Assert.Equal(requests + 1, setup.UserRequests);
    }

    [Fact]
    public async Task An_exception_the_check_lets_through_is_logged_by_its_type_alone_and_the_account_stays()
    {
        // D382 (H4): the check's catch-all names the exception by its type and codes alone.
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var octo = HostingSessionStartTests.OctoKey(setup);
        setup.Fault = request => request.RequestUri!.AbsolutePath == "/api/v3/user" ? new InvalidOperationException(setup.ThrownText) : null;

        setup.Session.Refused(octo);
        await CheckedAsync(setup);
        setup.Fault = null;

        Assert.Equal("GitHub account octo on 127.0.0.1:1: git was refused; the token could not be checked (InvalidOperationException).", setup.Warnings[^1]);
        Assert.Equal(AccountState.SignedIn, setup.Session.Account!.State);
        setup.AssertNothingThrownLogged();
    }

    [Fact]
    public async Task Two_erases_while_the_check_is_held_make_one_request()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var octo = HostingSessionStartTests.OctoKey(setup);
        var before = setup.UserRequests;

        using (var hold = setup.Fake.HoldNext("user"))
        {
            setup.Session.Refused(octo);
            await hold.Arrived.WaitAsync(Cancelled);
            setup.Session.Refused(octo);
        }

        await WaitForWarningsAsync(setup, 1);
        Assert.Equal(before + 1, setup.UserRequests);
        Assert.Single(setup.Warnings);
    }

    [Fact]
    public async Task BindsGit_and_the_dashboard_rows_bindings_follow_the_account_and_its_switch()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        RemoteUrl[] urls =
        [
            new("origin", "http://127.0.0.1:1/octo/hello.git", false),
            new("fork", "fake:ada/hello.git", false),
            new("other", "http://127.0.0.1:2/octo/hello.git", false),
        ];
        var rewrites = new UrlRewrites([new UrlRewrite("http://127.0.0.1:1/", "fake:", ForPush: false)]);
        var off = setup.NewSession(off: HostingOptions.Off);
        Assert.Equal((false, 0), (off.BindsGit, off.BindingsFor(urls, rewrites).Count));
        Assert.Empty(off.BindingsForClone("http://127.0.0.1:1/octo/hello.git"));

        await setup.Session.StartAsync(Cancelled);
        Assert.Equal((false, 0), (setup.Session.BindsGit, setup.Session.BindingsFor(urls, rewrites).Count));

        var octo = (await setup.SignInAsync()).Account.Key;

        Assert.True(setup.Session.BindsGit);
        Assert.Equal(
            [new CredentialBinding("http://127.0.0.1:1/octo/hello.git", octo), new CredentialBinding("http://127.0.0.1:1/ada/hello.git", octo)],
            setup.Session.BindingsFor(urls, rewrites));
        Assert.Equal([new CredentialBinding("http://127.0.0.1:1/octo/new.git", octo)], setup.Session.BindingsForClone("http://127.0.0.1:1/octo/new.git"));
        Assert.Empty(setup.Session.BindingsForClone("http://alice@127.0.0.1:1/octo/new.git"));

        setup.Session.SetUseForGit(false);
        await setup.Session.Switching.WaitAsync(Cancelled);
        Assert.Equal((false, 0, 0), (setup.Session.BindsGit, setup.Session.BindingsFor(urls, rewrites).Count, setup.Session.BindingsForClone("http://127.0.0.1:1/octo/new.git").Count));

        setup.Session.SetUseForGit(true);
        await setup.Session.Switching.WaitAsync(Cancelled);
        setup.Fake.Revoke(setup.OctoToken);
        await setup.Session.ListRepositoriesAsync(Cancelled);
        Assert.Equal((false, 0), (setup.Session.BindsGit, setup.Session.BindingsFor(urls, rewrites).Count));
    }

    [Fact]
    public async Task Recognition_and_the_push_guard_go_by_the_configured_url()
    {
        await using var setup = await HostingSessionSetup.StartAsync();

        Assert.Equal("octo/hello", setup.Session.Recognise("http://127.0.0.1:1/octo/hello.git")!.FullName);
        Assert.Null(setup.Session.Recognise("http://127.0.0.1:47391/octo/hello.git"));
        Assert.False(setup.Session.SourceCanExist("http://127.0.0.1:1/octo/hello.git", "refs/pull/fix/head"));
        Assert.True(setup.Session.SourceCanExist("http://127.0.0.1:1/octo/hello.git", "refs/pull/12/head"));
        Assert.True(setup.Session.SourceCanExist("http://127.0.0.1:2/octo/hello.git", "refs/pull/fix/head"));
        Assert.Empty(setup.Fake.Requests);
    }

    // Three providers (docs/phase-7b-design.md 9.5, 9.6, 9.10): ProvidersSessionSetup's fakes in
    // process, GitHub on 127.0.0.1:1, Azure DevOps on 127.0.0.1:2, GitLab on 127.0.0.1:3.

    [Fact]
    public async Task Gits_request_gets_a_token_by_the_keys_provider_and_key_alone()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync();
        var gitHub = (await setup.SignInAsync(HostingProvider.GitHub)).Account.Key;
        var gitLab = (await setup.SignInAsync(HostingProvider.GitLab)).Account.Key;
        var azure = (await setup.SignInAsync(HostingProvider.AzureDevOps)).Account.Key;

        Assert.Equal(setup.GitHubToken, (await setup.Session.GetAsync(gitHub, Cancelled))!.Token);
        Assert.Equal(setup.GitLabToken, (await setup.Session.GetAsync(gitLab, Cancelled))!.Token);
        Assert.Equal(setup.AzureDevOpsToken, (await setup.Session.GetAsync(azure, Cancelled))!.Token);
        Assert.Null(await setup.Session.GetAsync(new AccountKey(HostingProvider.GitLab, gitHub.Host, gitHub.Login), Cancelled));
        Assert.Null(await setup.Session.GetAsync(new AccountKey(HostingProvider.AzureDevOps, azure.Host, ProvidersSessionSetup.OtherOrganization), Cancelled));
        Assert.Null(await setup.Session.GetAsync(new AccountKey(HostingProvider.GitLab, azure.Host, gitLab.Login), Cancelled));

        setup.Session.SetUseForGit(HostingProvider.GitLab, false);
        await setup.Session.SwitchingOf(HostingProvider.GitLab).WaitAsync(Cancelled);
        Assert.Null(await setup.Session.GetAsync(gitLab, Cancelled));
        Assert.NotNull(await setup.Session.GetAsync(gitHub, Cancelled));
        Assert.Equal((true, false, true), (setup.LoadFile().Accounts[0].UseForGit, setup.LoadFile().Accounts[1].UseForGit, setup.LoadFile().Accounts[2].UseForGit));
        Assert.Contains("GitLab account ada on 127.0.0.1:3: not used for git over HTTPS.", setup.Log.Lines.Select(line => line.Message));
    }

    [Fact]
    public async Task Gitlabs_401_marks_gitlab_alone_and_the_next_start_reads_no_store_for_it()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(azureDevOps: false);
        var gitHub = await setup.SignInAsync(HostingProvider.GitHub);
        await setup.SignInAsync(HostingProvider.GitLab);
        setup.GitLab!.Revoke(setup.GitLabToken);

        var listed = await setup.Session.ListRepositoriesAsync(HostingProvider.GitLab, Cancelled);

        Assert.Equal((HostingFailureKind.BadCredentials, HostingProvider.GitLab), (listed.Failure!.Kind, listed.Failure.Provider));
        Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.Refused), (setup.Session.AccountOf(HostingProvider.GitLab)!.State, setup.Session.AccountOf(HostingProvider.GitLab)!.NoteKind));
        Assert.Same(gitHub, setup.Session.AccountOf(HostingProvider.GitHub));
        Assert.True(setup.Session.BindsGit);
        Assert.Equal((AccountTokenStates.Keychain, AccountTokenStates.Refused), (setup.LoadFile().Accounts[0].TokenState, setup.LoadFile().Accounts[1].TokenState));
        Assert.Contains(setup.Warnings, warning => warning.StartsWith("GitLab account ada on 127.0.0.1:3: GitLab refused the token (the API, request ", StringComparison.Ordinal));

        var requests = setup.Sent(HostingProvider.GitLab).Count;
        var unsent = (await setup.Session.ListRepositoriesAsync(HostingProvider.GitLab, Cancelled)).Failure!;
        Assert.Equal(("Not signed in.", HostingProvider.GitLab), (unsent.Message, unsent.Provider));
        Assert.Equal(requests, setup.Sent(HostingProvider.GitLab).Count);

        var next = setup.NewSession();
        await next.StartAsync(Cancelled);
        Assert.Equal(AccountState.NeedsSignIn, next.AccountOf(HostingProvider.GitLab)!.State);
        Assert.Equal(AccountState.SignedIn, next.AccountOf(HostingProvider.GitHub)!.State);
        Assert.DoesNotContain(setup.FakeTokens.Calls, call => call.StartsWith("read gitlab", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Two_providers_checks_run_at_once_and_azure_devops_names_the_organization()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false);
        var gitLab = (await setup.SignInAsync(HostingProvider.GitLab)).Account.Key;
        var azure = (await setup.SignInAsync(HostingProvider.AzureDevOps)).Account.Key;
        var azureSent = setup.Sent(HostingProvider.AzureDevOps).Count;

        using (var hold = setup.GitLab!.HoldNext("user"))
        {
            setup.Session.Refused(gitLab);
            await hold.Arrived.WaitAsync(Cancelled);
            setup.Session.Refused(azure);
            await setup.Session.RefusalCheckOf(HostingProvider.AzureDevOps)!.WaitAsync(Cancelled);
            Assert.False(setup.Session.RefusalCheckOf(HostingProvider.GitLab)!.IsCompleted);
        }

        await setup.Session.RefusalCheckOf(HostingProvider.GitLab)!.WaitAsync(Cancelled);
        Assert.Equal(
            ["/fake-org/_apis/connectionData", $"/fake-org/_apis/resourceAreas/{AzureDevOps.AzureDevOpsApi.CoreAreaId}"],
            setup.Sent(HostingProvider.AzureDevOps).Skip(azureSent).Select(request => request.Url.AbsolutePath));
        Assert.Contains("Azure DevOps account fake-org on 127.0.0.1:2: git was refused, yet the token is valid (an http.extraHeader, a proxy or a redirect?).", setup.Warnings);
        Assert.Contains("GitLab account ada on 127.0.0.1:3: git was refused, yet the token is valid (an http.extraHeader, a proxy or a redirect?).", setup.Warnings);
        Assert.All(HostingProviders.InAccountsOrder.Skip(1), provider => Assert.Equal(AccountState.SignedIn, setup.Session.AccountOf(provider)!.State));
    }

    [Fact]
    public async Task Another_users_user_id_or_id_ends_refused_and_a_record_without_the_user_id_takes_the_checks()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false);
        await setup.SignInAsync(HostingProvider.GitLab);
        var azure = await setup.SignInAsync(HostingProvider.AzureDevOps);
        var userId = azure.Account.UserId!;
        var store = new JsonAccountStore(setup.Paths.AccountsFile, new ListLog());
        var file = store.Load();
        store.Save(file
            .With(file.For(HostingProvider.GitLab, "127.0.0.1:3")! with { Id = 9999 })
            .With(file.For(HostingProvider.AzureDevOps, "127.0.0.1:2")! with { UserId = "00000000-1111-2222-3333-444444444444" }));

        var other = setup.NewSession();
        await other.StartAsync(Cancelled);
        other.Refused(other.AccountOf(HostingProvider.GitLab)!.Account.Key);
        other.Refused(other.AccountOf(HostingProvider.AzureDevOps)!.Account.Key);
        await other.RefusalCheckOf(HostingProvider.GitLab)!.WaitAsync(Cancelled);
        await other.RefusalCheckOf(HostingProvider.AzureDevOps)!.WaitAsync(Cancelled);

        Assert.All(HostingProviders.InAccountsOrder.Skip(1), provider => Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.Refused), (other.AccountOf(provider)!.State, other.AccountOf(provider)!.NoteKind)));
        Assert.Contains("GitLab account ada on 127.0.0.1:3: GitLab refused the token (git, another user); it needs signing in again.", setup.Warnings);
        Assert.Contains("Azure DevOps account fake-org on 127.0.0.1:2: Azure DevOps refused the token (git, another user); it needs signing in again.", setup.Warnings);
        Assert.All(store.Load().Accounts, record => Assert.Equal(AccountTokenStates.Refused, record.TokenState));

        // A 7a save dropped userId: the next check takes the check's, writes it and tells the tabs.
        await setup.SignInAsync(HostingProvider.AzureDevOps, session: other);
        file = store.Load();
        store.Save(file.With(file.For(HostingProvider.AzureDevOps, "127.0.0.1:2")! with { UserId = null }));
        var third = setup.NewSession();
        await third.StartAsync(Cancelled);
        var providers = new List<HostingProvider>();
        await setup.Thread.RunAsync(() => third.AccountChanged += (_, e) => providers.Add(e.Provider));
        Assert.Null(third.AccountOf(HostingProvider.AzureDevOps)!.Account.UserId);

        third.Refused(third.AccountOf(HostingProvider.AzureDevOps)!.Account.Key);
        await third.RefusalCheckOf(HostingProvider.AzureDevOps)!.WaitAsync(Cancelled);
        await setup.Thread.IdleAsync();

        Assert.Equal((AccountState.SignedIn, userId), (third.AccountOf(HostingProvider.AzureDevOps)!.State, third.AccountOf(HostingProvider.AzureDevOps)!.Account.UserId));
        Assert.Equal(userId, store.Load().For(HostingProvider.AzureDevOps, "127.0.0.1:2")!.UserId);
        Assert.Equal([HostingProvider.AzureDevOps], providers);
    }

    [Fact]
    public async Task The_dashboard_rows_bindings_are_the_union_of_the_accounts_and_leave_another_organization_unbound()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync();
        var gitHub = (await setup.SignInAsync(HostingProvider.GitHub)).Account.Key;
        var gitLab = (await setup.SignInAsync(HostingProvider.GitLab)).Account.Key;
        var azure = (await setup.SignInAsync(HostingProvider.AzureDevOps)).Account.Key;
        RemoteUrl[] urls =
        [
            new("origin", "http://127.0.0.1:1/octo/web-app.git", false),
            new("lab", "http://127.0.0.1:3/fake-group/sub/web-app", false),
            new("azure", "http://fake-org@127.0.0.1:2/fake-org/P/_git/r", false),
            new("other", "http://other-org@127.0.0.1:2/other-org/P/_git/r", false),
        ];

        var bindings = setup.Session.BindingsFor(urls, UrlRewrites.None);

        Assert.Equal(
            [
                new CredentialBinding("http://127.0.0.1:1/octo/web-app.git", gitHub),
                new CredentialBinding("http://127.0.0.1:3/fake-group/sub/web-app", gitLab),
                new CredentialBinding("http://127.0.0.1:3/fake-group/sub/web-app.git", gitLab),
                new CredentialBinding("http://127.0.0.1:2/fake-org/P/_git/r", azure) { SharedOriginOwner = "fake-org" },
            ],
            bindings);
        Assert.Equal([null, null, null, "fake-org"], bindings.Select(binding => binding.SharedOriginOwner));
        Assert.Equal([new CredentialBinding("http://127.0.0.1:2/fake-org/P/_git/new", azure) { SharedOriginOwner = "fake-org" }], setup.Session.BindingsForClone("http://127.0.0.1:2/fake-org/P/_git/new"));
        Assert.Empty(setup.Session.BindingsForClone("http://127.0.0.1:2/other-org/P/_git/new"));

        setup.Session.SetUseForGit(HostingProvider.GitLab, false);
        await setup.Session.SwitchingOf(HostingProvider.GitLab).WaitAsync(Cancelled);
        Assert.Equal(["http://127.0.0.1:1/octo/web-app.git", "http://127.0.0.1:2/fake-org/P/_git/r"], setup.Session.BindingsFor(urls, UrlRewrites.None).Select(binding => binding.Url));
    }

    [Fact]
    public async Task On_the_service_an_organization_hosts_remote_is_bound_to_its_organizations_account()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, gitLab: false, azureDevOpsEndpoints: AzureDevOps.AzureDevOpsEndpoints.Service);
        var azure = (await setup.SignInAsync(HostingProvider.AzureDevOps)).Account.Key;
        Assert.Equal(new AccountKey(HostingProvider.AzureDevOps, "dev.azure.com", "fake-org"), azure);

        var binding = Assert.Single(setup.Session.BindingsFor([new RemoteUrl("origin", "https://fake-org.visualstudio.com/P/_git/r", false)], UrlRewrites.None));

        Assert.Equal(new CredentialBinding("https://fake-org.visualstudio.com/P/_git/r", azure), binding);
        Assert.Null(binding.SharedOriginOwner);
        Assert.Empty(setup.Session.BindingsFor([new RemoteUrl("origin", "https://other-org.visualstudio.com/P/_git/r", false)], UrlRewrites.None));
        Assert.Equal("fake-org", Assert.Single(setup.Session.BindingsFor([new RemoteUrl("origin", "https://dev.azure.com/fake-org/P/_git/r", false)], UrlRewrites.None)).SharedOriginOwner);
        Assert.All(setup.Sent(HostingProvider.AzureDevOps), request => Assert.Equal("dev.azure.com", request.Url.Host));
    }

    private static AccountRecord Load(HostingSessionSetup setup) => Assert.Single(HostingSessionSignInTests.Load(setup).Accounts);

    private static Task WaitForWarningsAsync(HostingSessionSetup setup, int count) => WaitForAsync(() => setup.Warnings.Count >= count);

    /// <summary>The check the last <c>Refused</c> started, to its end: its warning, or its mark and its warning, are in by then (D330).</summary>
    private static Task CheckedAsync(HostingSessionSetup setup) => setup.Session.RefusalCheck!.WaitAsync(Cancelled);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var limit = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < limit, "The condition did not hold within 30 s.");
            await Task.Delay(10, Cancelled);
        }
    }
}
