using System.Text.Json;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.Tokens;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The session for three providers (docs/phase-7b-design.md 9.2 to 9.4, 9.6, 9.10; D348, D353, D357,
/// D382): its construction lines and what <see cref="HostingOptions.FromEnvironment"/> turns on, two
/// providers refused on one origin; the start's one read per record in D353's order, an off
/// provider's record kept unread; each provider's sign-in with one A1 to its fake and its own record,
/// the others untouched, no address and no echoed token kept; sign-outs, GitHub's alone taking
/// <c>avatars/</c>; and the push guard by the recognising provider.
/// </summary>
public sealed class HostingSessionProvidersTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_environments_defaults_turn_three_providers_on_and_recognise_each_hosts_remotes_without_a_request()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, azureDevOps: false, gitLab: false);
        var made = new List<RecordingHttpHandler>();
        Func<HttpMessageHandler> refusing = () =>
        {
            var handler = new RecordingHttpHandler(_ => throw new HttpRequestException("No request may be sent here."));
            lock (made)
            {
                made.Add(handler);
            }

            return handler;
        };
        var log = new ListLog();
        var options = HostingOptions.FromEnvironment(_ => null, log) with { CreateHandler = refusing, CreateAzureDevOpsHandler = refusing, CreateGitLabHandler = refusing };

        using var session = setup.NewSession(options: options, log: log);

        Assert.Equal(["GitHub: github.com.", "GitLab: gitlab.com.", "Azure DevOps: dev.azure.com."], log.Lines.Select(line => line.Message));
        Assert.Equal([HostingProvider.GitHub, HostingProvider.GitLab, HostingProvider.AzureDevOps], session.Providers);
        Assert.False(session.IsOff);
        Assert.Equal(("github.com", "octo/web-app"), Named(session.Recognise("https://github.com/octo/web-app.git")));
        Assert.Equal(("gitlab.com", "fake-group/sub/web-app"), Named(session.Recognise("https://gitlab.com/fake-group/sub/web-app.git")));
        Assert.Equal(("dev.azure.com", "fake-org/P/r"), Named(session.Recognise("https://dev.azure.com/fake-org/P/_git/r")));
        Assert.Equal(("dev.azure.com", "fake-org/P/r"), Named(session.Recognise("https://fake-org.visualstudio.com/P/_git/r")));
        Assert.Equal(HostingProvider.GitLab, session.Recognise("git@gitlab.com:fake-group/sub/web-app.git")!.Provider);
        Assert.Null(session.Recognise("https://example.com/octo/web-app.git"));
        Assert.All(made, handler => Assert.Empty(handler.Sent));

        static (string Host, string FullName) Named(HostedRepository? repository) => (repository!.Host, repository.FullName);
    }

    [Fact]
    public async Task None_turns_three_off_and_two_providers_on_one_origin_throw()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, azureDevOps: false, gitLab: false);
        var log = new ListLog();

        using var off = setup.NewSession(options: HostingOptions.FromEnvironment(_ => "none", log), log: log);

        Assert.Equal(["GitHub: off.", "GitLab: off.", "Azure DevOps: off."], log.Lines.Select(line => line.Message));
        Assert.True(off.IsOff);
        Assert.Empty(off.Providers);
        Assert.Same(NullRepositoryHosting.Instance, off.ForRepository(setup.Data.Path));
        Assert.Null(off.Recognise("https://gitlab.com/g/p.git"));

        var shared = new HostingOptions(GitHubEndpoints.Local(47001), HostingOptions.Off.CreateHandler) with { GitLab = GitLab.GitLabEndpoints.Local(47001) };
        Assert.Throws<ArgumentException>(() => setup.NewSession(options: shared));
        var sharedAzure = HostingOptions.Off with { GitLab = GitLab.GitLabEndpoints.Local(47002), AzureDevOps = AzureDevOps.AzureDevOpsEndpoints.Local(47002) };
        Assert.Throws<ArgumentException>(() => setup.NewSession(options: sharedAzure));
    }

    [Fact]
    public async Task A_start_without_a_record_touches_no_store_and_sends_nothing()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync();

        await setup.Session.StartAsync(Cancelled);

        Assert.Empty(setup.FakeTokens.Calls);
        Assert.Equal(0, setup.SentCount);
        Assert.Equal(
            ["GitHub: no account on 127.0.0.1:1.", "GitLab: no account on 127.0.0.1:3.", "Azure DevOps: no account on 127.0.0.1:2."],
            setup.Log.Lines.Where(line => line.Message.Contains(": no account on ", StringComparison.Ordinal)).Select(line => line.Message));
        Assert.All(HostingProviders.InAccountsOrder, provider => Assert.Null(setup.Session.AccountOf(provider)));
    }

    [Fact]
    public async Task Three_records_are_read_with_three_reads_one_after_another_and_their_events_come_in_the_accounts_order()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync();
        await setup.SignInAsync(HostingProvider.AzureDevOps);
        await setup.SignInAsync(HostingProvider.GitLab);
        await setup.SignInAsync(HostingProvider.GitHub);
        var calls = setup.FakeTokens.Calls.Count;

        var next = setup.NewSession();
        var providers = new List<HostingProvider>();
        await setup.Thread.RunAsync(() => next.AccountChanged += (_, e) => providers.Add(e.Provider));

        // The first read held (after the keychain's probe, which every read waits for): the next
        // read starts only once it has ended.
        var hold = setup.FakeTokens.HoldNext();
        var start = next.StartAsync(Cancelled);
        await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        if (setup.FakeTokens.Calls[^1] == "probe")
        {
            var probe = hold;
            hold = setup.FakeTokens.HoldNext();
            probe.Dispose();
            await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        }

        Assert.Equal("read github:127.0.0.1:1:octo", setup.FakeTokens.Calls[^1]);
        await Task.Delay(200, Cancelled);
        Assert.DoesNotContain(setup.FakeTokens.Calls.Skip(calls), call => call.StartsWith("read gitlab", StringComparison.Ordinal) || call.StartsWith("read azure-devops", StringComparison.Ordinal));
        Assert.False(start.IsCompleted);
        hold.Dispose();
        await start.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        await setup.Thread.IdleAsync();

        Assert.Equal(
            ["read github:127.0.0.1:1:octo", "read gitlab:127.0.0.1:3:ada", "read azure-devops:127.0.0.1:2:fake-org"],
            setup.FakeTokens.Calls.Skip(calls).Where(call => call != "probe"));
        Assert.Equal([HostingProvider.GitHub, HostingProvider.GitLab, HostingProvider.AzureDevOps], providers);
        Assert.All(HostingProviders.InAccountsOrder, provider => Assert.Equal(AccountState.SignedIn, next.AccountOf(provider)!.State));
        var azure = next.AccountOf(HostingProvider.AzureDevOps)!.Account;
        Assert.Equal((ProvidersSessionSetup.Organization, 0L, setup.Ada!.Id.ToString("D")), (azure.Login, azure.Id, azure.UserId));
        Assert.Null(next.AccountOf(HostingProvider.GitLab)!.Account.AvatarUrl);
    }

    [Fact]
    public async Task An_off_providers_record_is_kept_unread_through_another_providers_sign_in()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(azureDevOps: false, gitLab: false);
        Directory.CreateDirectory(setup.Data.Path);
        File.WriteAllText(setup.Paths.AccountsFile, """
            { "version": 1, "accounts": [ { "provider": "gitlab", "host": "gitlab.com", "login": "ada", "id": 2001, "tokenState": "keychain" } ] }
            """);

        await setup.SignInAsync(HostingProvider.GitHub);

        Assert.DoesNotContain(setup.FakeTokens.Calls, call => call.StartsWith("read", StringComparison.Ordinal));
        Assert.Null(setup.Session.AccountOf(HostingProvider.GitLab));
        var file = setup.LoadFile();
        Assert.Equal(["gitlab", "github"], file.Accounts.Select(record => record.Provider));
        Assert.Equal((2001L, "ada"), (file.Accounts[0].Id, file.Accounts[0].Login));
    }

    [Fact]
    public async Task Each_providers_sign_in_sends_one_a1_to_its_fake_and_writes_its_own_record_leaving_the_others()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync();
        var gitHub = await setup.SignInAsync(HostingProvider.GitHub);
        Assert.Equal(["GET /api/v3/user"], Paths(setup, HostingProvider.GitHub));

        var gitLab = await setup.SignInAsync(HostingProvider.GitLab);
        Assert.Equal(["GET /api/v4/user"], Paths(setup, HostingProvider.GitLab));
        Assert.Same(gitHub, setup.Session.AccountOf(HostingProvider.GitHub));

        var azure = await setup.SignInAsync(HostingProvider.AzureDevOps, organization: "Fake-Org");
        Assert.Equal(["GET /Fake-Org/_apis/connectionData", $"GET /Fake-Org/_apis/resourceAreas/{AzureDevOps.AzureDevOpsApi.CoreAreaId}"], Paths(setup, HostingProvider.AzureDevOps));
        Assert.Same(gitLab, setup.Session.AccountOf(HostingProvider.GitLab));
        Assert.Single(setup.Sent(HostingProvider.GitHub));

        Assert.Equal(new AccountKey(HostingProvider.AzureDevOps, "127.0.0.1:2", "fake-org"), azure.Account.Key);
        Assert.Equal(new AccountKey(HostingProvider.GitLab, "127.0.0.1:3", "ada"), gitLab.Account.Key);
        using var json = JsonDocument.Parse(File.ReadAllText(setup.Paths.AccountsFile));
        var records = json.RootElement.GetProperty("accounts").EnumerateArray().ToList();
        Assert.Equal(["github", "gitlab", "azure-devops"], records.Select(record => record.GetProperty("provider").GetString()));
        Assert.False(records[0].TryGetProperty("userId", out _));
        Assert.False(records[1].TryGetProperty("userId", out _));
        Assert.Equal((2001L, JsonValueKind.Null), (records[1].GetProperty("id").GetInt64(), records[1].GetProperty("avatarUrl").ValueKind));
        Assert.Equal(
            ("127.0.0.1:2", "fake-org", JsonValueKind.Number, 0L, setup.Ada!.Id.ToString("D"), JsonValueKind.Null, AccountTokenStates.Keychain),
            (records[2].GetProperty("host").GetString(), records[2].GetProperty("login").GetString(), records[2].GetProperty("id").ValueKind, records[2].GetProperty("id").GetInt64(),
             records[2].GetProperty("userId").GetString(), records[2].GetProperty("avatarUrl").ValueKind, records[2].GetProperty("tokenState").GetString()));
        Assert.Contains("Azure DevOps account fake-org on 127.0.0.1:2: signed in; the token is kept in the keychain.", setup.Log.Lines.Select(line => line.Message));
        Assert.Contains("GitLab account ada on 127.0.0.1:3: signed in; the token is kept in the keychain.", setup.Log.Lines.Select(line => line.Message));
    }

    [Fact]
    public async Task A_users_name_that_is_an_address_is_kept_nowhere_in_the_accounts_file()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false);
        var grace = setup.AzureDevOps!.AddUser(ProvidersSessionSetup.Organization, "grace.hopper@example.com");
        setup.GitLab!.AddUser("grace", 2005, "grace.hopper@example.com");

        var azure = await setup.SignInAsync(HostingProvider.AzureDevOps, setup.AzureDevOps.AddToken(ProvidersSessionSetup.Organization, grace));
        var gitLab = await setup.SignInAsync(HostingProvider.GitLab, setup.GitLab.AddToken("grace"));

        Assert.Equal(((string?)null, (string?)null), (azure.Account.Name, gitLab.Account.Name));
        Assert.DoesNotContain("@", File.ReadAllText(setup.Paths.AccountsFile), StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", setup.Log.Written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_gitlab_user_forgets_the_firsts_entry_and_keeps_other_providers()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(azureDevOps: false);
        await setup.SignInAsync(HostingProvider.GitHub);
        await setup.SignInAsync(HostingProvider.GitLab);
        setup.GitLab!.AddUser("bob", 2003, "Bob Example");

        var bob = await setup.SignInAsync(HostingProvider.GitLab, setup.GitLab.AddToken("bob"));

        Assert.Equal("bob", bob.Account.Login);
        Assert.False(setup.FakeTokens.Holds(TokenKey.Of(new AccountKey(HostingProvider.GitLab, "127.0.0.1:3", "ada"))));
        Assert.True(setup.FakeTokens.Holds(TokenKey.Of(new AccountKey(HostingProvider.GitLab, "127.0.0.1:3", "bob"))));
        Assert.True(setup.FakeTokens.Holds(TokenKey.Of(new AccountKey(HostingProvider.GitHub, "127.0.0.1:1", "octo"))));
        Assert.Equal(["github", "gitlab"], setup.LoadFile().Accounts.Select(record => record.Provider));
        Assert.Equal("bob", setup.LoadFile().For(HostingProvider.GitLab, "127.0.0.1:3")!.Login);
    }

    [Fact]
    public async Task An_off_providers_sign_in_fails_without_a_request_and_two_at_once_keep_both_records()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(azureDevOps: false);
        await setup.Session.StartAsync(Cancelled);

        var off = await setup.Session.SignInAsync(new SignInRequest(HostingProvider.AzureDevOps, "azdo_FAKE-made-up-1", "fake-org"), Cancelled);

        Assert.Equal((HostingFailureKind.Validation, "Azure DevOps is turned off.", HostingProvider.AzureDevOps), (off.Failure!.Kind, off.Failure.Message, off.Failure.Provider));
        Assert.Equal(0, setup.SentCount);

        var both = await Task.WhenAll(
            setup.Session.SignInAsync(new SignInRequest(HostingProvider.GitHub, setup.GitHubToken), Cancelled),
            setup.Session.SignInAsync(new SignInRequest(HostingProvider.GitLab, setup.GitLabToken), Cancelled));

        Assert.All(both, result => Assert.Null(result.Failure));
        Assert.Equal(["github", "gitlab"], setup.LoadFile().Accounts.Select(record => record.Provider).Order(StringComparer.Ordinal));
        Assert.True(setup.Session.BindsGit);
    }

    [Fact]
    public async Task An_answer_that_names_the_token_is_malformed_and_nothing_is_kept_or_logged()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, azureDevOps: false);
        setup.GitLab!.AddUser("eve", 2004, "Eve GLPAT-FAKE-EVE-1");
        var token = setup.GitLab.AddToken("eve");
        Assert.Equal("glpat-FAKE-eve-1", token);
        await setup.Session.StartAsync(Cancelled);

        var result = await setup.Session.SignInAsync(new SignInRequest(HostingProvider.GitLab, token), Cancelled);

        Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", HostingProvider.GitLab), (result.Failure!.Kind, result.Failure.Message, result.Failure.Provider));
        Assert.Null(setup.Session.AccountOf(HostingProvider.GitLab));
        Assert.False(File.Exists(setup.Paths.AccountsFile));
        Assert.DoesNotContain(setup.FakeTokens.Calls, call => call.StartsWith("save", StringComparison.Ordinal));
        Assert.DoesNotContain("EVE-1", setup.Log.Written, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gitlabs_sign_out_keeps_the_avatars_and_githubs_slot_and_githubs_deletes_them_keeping_gitlabs()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(azureDevOps: false);
        var gitHub = await setup.SignInAsync(HostingProvider.GitHub);
        await setup.SignInAsync(HostingProvider.GitLab);
        Directory.CreateDirectory(setup.Paths.AvatarsDirectory);
        var avatar = Path.Combine(setup.Paths.AvatarsDirectory, "made-up.png");
        File.WriteAllBytes(avatar, [1, 2, 3]);

        Assert.Null(await setup.Session.SignOutAsync(HostingProvider.GitLab, Cancelled));

        Assert.True(File.Exists(avatar));
        Assert.Same(gitHub, setup.Session.AccountOf(HostingProvider.GitHub));
        Assert.Null(setup.Session.AccountOf(HostingProvider.GitLab));
        Assert.Equal(["github"], setup.LoadFile().Accounts.Select(record => record.Provider));
        Assert.Contains("GitLab account ada on 127.0.0.1:3: signed out.", setup.Log.Lines.Select(line => line.Message));

        var gitLab = await setup.SignInAsync(HostingProvider.GitLab);
        Assert.Null(await setup.Session.SignOutAsync(HostingProvider.GitHub, Cancelled));

        Assert.False(Directory.Exists(setup.Paths.AvatarsDirectory));
        Assert.Same(gitLab, setup.Session.AccountOf(HostingProvider.GitLab));
        Assert.Equal(["gitlab"], setup.LoadFile().Accounts.Select(record => record.Provider));
        Assert.Null(await setup.Session.SignOutAsync(HostingProvider.AzureDevOps, Cancelled));
    }

    [Fact]
    public async Task The_push_guard_asks_the_provider_that_recognises_the_first_url()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync();
        var session = setup.Session;
        const string GitLabRemote = "http://127.0.0.1:3/fake-group/sub/web-app.git";
        const string GitHubRemote = "http://127.0.0.1:1/octo/web-app.git";
        const string AzureRemote = "http://127.0.0.1:2/fake-org/P/_git/r";

        Assert.False(session.SourceCanExist(GitLabRemote, "refs/merge-requests/x/head"));
        Assert.True(session.SourceCanExist(GitLabRemote, "refs/merge-requests/5/head"));
        Assert.True(session.SourceCanExist(GitHubRemote, "refs/merge-requests/x/head"));
        Assert.False(session.SourceCanExist(AzureRemote, "refs/pull/x/merge"));
        Assert.False(session.SourceCanExist(AzureRemote, "refs/pull/7/head"));
        Assert.True(session.SourceCanExist(AzureRemote, "refs/pull/7/merge"));
        Assert.False(session.SourceCanExist("http://127.0.0.1:2/other-org/P/_git/r", "refs/pull/x/merge"));
        Assert.True(session.SourceCanExist("https://example.com/o/r.git", "refs/pull/x/merge"));
        Assert.Equal(0, setup.SentCount);
    }

    private static IReadOnlyList<string> Paths(ProvidersSessionSetup setup, HostingProvider provider) =>
        [.. setup.Sent(provider).Select(request => $"{request.Method} {request.Url.AbsolutePath}")];
}
