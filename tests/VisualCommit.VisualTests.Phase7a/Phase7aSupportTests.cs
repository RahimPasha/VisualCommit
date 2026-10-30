using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Avalonia.Headless.XUnit;
using VisualCommit.Hosting;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The harness of phase 7a tried once, without pictures (docs/phase-7a-design.md 1.8, 2.5, 2.8,
/// 2.9): the process's safety nets, the network tripwire with and without the fake, the start's
/// refusal of a fake and options together, the token's tripwire, and the support's port, picture
/// pattern and folder.
/// </summary>
public sealed class Phase7aSupportTests : Phase7aChecks
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>The variables 1.8 removes from every test process, named here rather than read from the list under test.</summary>
    private static readonly string[] TokenVariables =
    [
        "GH_TOKEN",
        "GITHUB_TOKEN",
        "GH_ENTERPRISE_TOKEN",
        "GITHUB_ENTERPRISE_TOKEN",
        "GH_HOST",
        "VISUALCOMMIT_LIVE_GITHUB_TOKEN",
        "VISUALCOMMIT_LIVE_GITHUB_REPO",
    ];

    [Fact]
    public void The_process_starts_with_GitHub_off_tokens_in_memory_and_no_token_variable()
    {
        Assert.Equal("none", Environment.GetEnvironmentVariable(HostingOptions.GitHubVariable));
        Assert.Equal("memory", Environment.GetEnvironmentVariable(VisualCommit.Hosting.Tokens.TokenStores.Variable));
        Assert.All(TokenVariables, variable => Assert.Contains(variable, GitIsolation.HostingTokenVariables));
        Assert.All(TokenVariables, variable => Assert.Contains(new KeyValuePair<string, string?>(variable, null), GitIsolation.MachineIndependence));
        Assert.All(TokenVariables, variable => Assert.Null(Environment.GetEnvironmentVariable(variable)));
    }

    [AvaloniaFact]
    public async Task A_start_without_a_fake_is_off_and_lets_no_request_out()
    {
        using var data = new TempDirectory("phase7a support");
        using var app = ShellDriver.Start(data.Path);

        Assert.True(app.Session.HostingStarted.IsCompletedSuccessfully);
        Assert.Null(app.Hosting.GitHub);
        Assert.Null(app.GitHub);
        Assert.Empty(app.Tokens.Calls);
        Assert.True(app.Session.Services.Hosting.IsOff);
        Assert.Equal(0, app.RefusedRequests.Count);

        using var client = new HttpClient(app.Hosting.CreateHandler());
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri("https://github.com/"), Cancelled));
        Assert.Equal(LoopbackOnlyHandler.RefusalMessage, refused.Message);
        Assert.Equal([new Uri("https://github.com/")], app.RefusedRequests.Addresses);
    }

    [AvaloniaFact]
    public async Task With_a_fake_the_handler_refuses_other_hosts_and_passes_the_fakes_api()
    {
        using var data = new TempDirectory("phase7a support");
        using var root = new TempDirectory("phase7a fake");
        await using var fake = await FakeGitHub.StartAsync(root.Path);
        fake.AddUser(OctoLogin, OctoId, OctoName);
        var token = fake.AddToken(OctoLogin);
        using var app = ShellDriver.Start(data.Path, gitHub: fake);

        Assert.Same(fake, app.GitHub);
        Assert.Equal(fake.Endpoints, app.Hosting.GitHub);
        Assert.Equal($"http://127.0.0.1:{fake.Port}/api/v3/", fake.Endpoints.ApiBase.AbsoluteUri);
        using var client = new HttpClient(app.Hosting.CreateHandler());
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri("https://example.com/"), Cancelled));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(new Uri($"http://localhost:{fake.Port}/api/v3/user"), Cancelled));
        Assert.Equal([new Uri("https://example.com/"), new Uri($"http://localhost:{fake.Port}/api/v3/user")], app.RefusedRequests.Addresses);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(fake.Endpoints.ApiBase, "user"));
        request.Headers.UserAgent.ParseAdd("VisualCommit-tests");
        request.Headers.Accept.ParseAdd(GitHubApi.MediaType);
        request.Headers.Add("X-GitHub-Api-Version", GitHubApi.Version);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var user = await client.SendAsync(request, Cancelled);
        Assert.Equal(HttpStatusCode.OK, user.StatusCode);
        Assert.Contains("\"login\":\"octo\"", await user.Content.ReadAsStringAsync(Cancelled), StringComparison.Ordinal);
        Assert.Equal(("user", "octo"), (fake.Requests[^1].Route, fake.Requests[^1].Login));
        Assert.Equal(2, app.RefusedRequests.Count);
        Assert.Empty(fake.Errors);
    }

    [AvaloniaFact]
    public async Task A_fake_and_hosting_options_together_and_an_in_process_fake_as_gitHub_are_refused()
    {
        using var data = new TempDirectory("phase7a support");
        using var root = new TempDirectory("phase7a fake");
        await using var fake = FakeGitHub.InProcess(root.Path);

        Assert.Equal("hosting", Assert.Throws<ArgumentException>(() => ShellDriver.Start(data.Path, gitHub: fake, hosting: HostingOptions.Off)).ParamName);
        Assert.Equal("hosting", Assert.Throws<ArgumentException>(() => ShellDriver.StartWithTabs(data.Path, [null], gitHub: fake, hosting: HostingOptions.Off)).ParamName);

        // A fake without a server has Local(1) for endpoints, which the default handler cannot
        // reach: it goes in the hosting options with its own handler.
        var inProcess = Assert.Throws<ArgumentException>(() => ShellDriver.Start(data.Path, gitHub: fake));
        Assert.Equal("gitHub", inProcess.ParamName);
        Assert.Contains("hosting: new HostingOptions(fake.Endpoints, fake.CreateHandler)", inProcess.Message, StringComparison.Ordinal);
        Assert.Equal("gitHub", Assert.Throws<ArgumentException>(() => ShellDriver.StartWithTabs(data.Path, [null], gitHub: fake)).ParamName);
        Assert.Empty(Directory.EnumerateFileSystemEntries(data.Path));

        fake.AddUser(OctoLogin, OctoId, OctoName);
        var token = fake.AddToken(OctoLogin);
        using var app = ShellDriver.Start(data.Path, hosting: new HostingOptions(fake.Endpoints, fake.CreateHandler));
        Assert.Equal(fake.Endpoints, app.Hosting.GitHub);
        using var client = new HttpClient(app.Hosting.CreateHandler());
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(fake.Endpoints.ApiBase, "user"));
        request.Headers.UserAgent.ParseAdd("VisualCommit-tests");
        request.Headers.Accept.ParseAdd(GitHubApi.MediaType);
        request.Headers.Add("X-GitHub-Api-Version", GitHubApi.Version);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var user = await client.SendAsync(request, Cancelled);
        Assert.Equal(HttpStatusCode.OK, user.StatusCode);
        Assert.Equal(("user", "octo"), (fake.Requests[^1].Route, fake.Requests[^1].Login));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task The_token_tripwire_finds_each_form_in_a_file_and_a_call_and_passes_clean_ones()
    {
        const string Token = "ghp_FAKE-octo/9+x";
        using var repo = await TempRepo.CreateAsync("tripwire");
        var forms = new Dictionary<string, byte[]>
        {
            ["UTF-8"] = Encoding.UTF8.GetBytes(Token),
            ["UTF-16LE"] = Encoding.Unicode.GetBytes(Token),
            ["URL-escaped"] = Encoding.UTF8.GetBytes(Uri.EscapeDataString(Token)),
            ["base64 of x-access-token:<token>"] = Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + Token))),
            ["base64 of the token"] = Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(Token))),
        };

        // A clean folder and a clean call log pass, the git folder included.
        await repo.GitAsync("status", "--porcelain");
        TokenTripwire.AssertAbsent(Token, [repo.Path], repo.Calls);

        foreach (var (form, bytes) in forms)
        {
            var file = Path.Combine(repo.Path, ".git", "leak.bin");
            File.WriteAllBytes(file, [.. "before "u8, .. bytes, .. " after"u8]);
            var found = Assert.Throws<TokenTripwireException>(() => TokenTripwire.AssertAbsent(Token, [repo.Path], null));
            Assert.Contains($"leak.bin ({form})", found.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Token, found.Message, StringComparison.Ordinal);
            File.Delete(file);
        }

        // A file another process holds open for writing is read too.
        var written = Path.Combine(repo.Path, ".git", "held.log");
        using (var writer = new FileStream(written, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            writer.Write([.. "before "u8, .. Encoding.UTF8.GetBytes(Token)]);
            writer.Flush();
            var held = Assert.Throws<TokenTripwireException>(() => TokenTripwire.AssertAbsent(Token, [repo.Path], null));
            Assert.Contains("held.log (UTF-8)", held.Message, StringComparison.Ordinal);
        }

        File.Delete(written);

        // One that nothing may read fails as not read, named, and never passes as clean.
        var locked = Path.Combine(repo.Path, ".git", "locked.bin");
        File.WriteAllText(locked, "clean");
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var unread = Assert.Throws<TokenTripwireException>(() => TokenTripwire.AssertAbsent(Token, [repo.Path], null));
            Assert.Contains("Files not read", unread.Message, StringComparison.Ordinal);
            Assert.Contains("locked.bin (IOException: ", unread.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("was found", unread.Message, StringComparison.Ordinal);
        }

        File.Delete(locked);
        TokenTripwire.AssertAbsent(Token, [repo.Path], null);

        // A call whose command line holds the token.
        await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", Token);
        var inCall = Assert.Throws<TokenTripwireException>(() => TokenTripwire.AssertAbsent(Token, [repo.Path], repo.Calls));
        Assert.Contains("the command of the git call", inCall.Message, StringComparison.Ordinal);
        Assert.Contains("(UTF-8)", inCall.Message, StringComparison.Ordinal);
        Assert.Contains("git rev-parse --verify --quiet …", inCall.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, inCall.Message, StringComparison.Ordinal);
        TokenTripwire.AssertAbsent("github_pat_FAKE-ada-3", [repo.Path], repo.Calls);

        // A call whose recorded standard input holds the token (update-ref --stdin's record keeps it).
        var log = new VisualCommit.Git.GitCallLog();
        log.Add(new VisualCommit.Core.Git.GitCallRecord(DateTimeOffset.UnixEpoch, TimeSpan.Zero, "git update-ref --stdin", repo.Path, VisualCommit.Core.Git.GitCallOutcome.Completed, 0, string.Empty, string.Empty) { StandardInput = "update refs/heads/x " + Token + "\n" });
        var inInput = Assert.Throws<TokenTripwireException>(() => TokenTripwire.AssertAbsent(Token, [repo.Path], log));
        Assert.Contains("the input of the git call git update-ref --stdin (UTF-8)", inInput.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, inInput.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_port_the_picture_names_and_the_folder_are_7as()
    {
        Assert.Equal("7a", Phase);
        Assert.Equal(47395, ServerPort);
        Assert.Equal("http://127.0.0.1:50123/octo/hello.git", WithPort("http://127.0.0.1:47395/octo/hello.git", 50123));
        Assert.Equal("http://127.0.0.1:47395/", WebBase());
        Assert.EndsWith(Path.Combine("artifacts", "visual", "phase-7a", "scripted"), VisualCommit.Testing.Headless.VisualRun.FolderFor(Phase), StringComparison.Ordinal);

        foreach (var name in new[] { "a1a-accounts-signed-out", "b9-browser-listed", "p12c-section-menu", "g20a-badge", "n24b-form-draft", "1a-start" })
        {
            Assert.Matches(PictureName(), name);
        }

        foreach (var name in new[] { "c1a-commit", "r3-real", "t2-x", "w4a-fetch-held", "a1a_bad", "a1abcd-too-long-step", "A1a-upper" })
        {
            Assert.DoesNotMatch(PictureName(), name);
        }

        Assert.Throws<ArgumentException>(() => ClaimPictureName("w4a-fetch-held"));
        Assert.Equal([5, 3, 2, 1], HelloPullRequests.Select(pull => pull.Number));
        Assert.Equal([8, 7], ToolPullRequests.Select(pull => pull.Number));
    }
}
