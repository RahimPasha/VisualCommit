using System.Net;
using System.Net.Http.Headers;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The fakes' shared core (docs/phase-7b-design.md 1.9, 2.3) through a fake of its own: holds, a
/// fault once, both drops, <see cref="FakeHostingService.Revoke"/> on the API and on git, each
/// <see cref="FakeAuthorizationForm"/>, git holds, <c>GitAsync</c>'s environment, the bare
/// repositories' rule and the dispose.
/// </summary>
public sealed class FakeHostingServiceTests
{
    private const string AdaToken = "test_FAKE-ada-1";
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(60);

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_held_route_waits_until_its_hold_is_disposed_and_its_key_has_no_case()
    {
        using var root = new TempDirectory("fake core");
        await using var fake = TestService.InProcess(root.Path);
        using var client = new HttpClient(fake.CreateHandler());

        var hold = fake.HoldNext(" User ");
        Assert.Equal("user", hold.Route);
        Assert.Throws<InvalidOperationException>(() => fake.HoldNext("user"));
        var sent = client.GetAsync(new Uri("http://any.example/api/user"), Cancelled);
        await hold.Arrived.WaitAsync(StepTimeout, Cancelled);
        Assert.False(sent.IsCompleted);
        Assert.Empty(fake.Requests);
        Assert.False(hold.Released.IsCompleted);

        hold.Dispose();
        using var answered = await sent.WaitAsync(StepTimeout, Cancelled);
        Assert.Equal(HttpStatusCode.Unauthorized, answered.StatusCode);
        Assert.True(hold.Released.IsCompletedSuccessfully);
        Assert.Equal(("user", 401), (fake.Requests[^1].Route, fake.Requests[^1].Status));

        // A request whose sender gives up while it is held is neither answered nor recorded.
        using var abandonedHold = fake.HoldNext("user");
        using var giveUp = CancellationTokenSource.CreateLinkedTokenSource(Cancelled);
        var abandoned = client.GetAsync(new Uri("http://any.example/api/user"), giveUp.Token);
        await abandonedHold.Arrived.WaitAsync(StepTimeout, Cancelled);
        await giveUp.CancelAsync();
        await abandonedHold.Abandoned.WaitAsync(StepTimeout, Cancelled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);
        Assert.Single(fake.Requests);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_fault_is_answered_once_and_both_drops_close_the_connection_without_an_answer()
    {
        using var root = new TempDirectory("fake core");
        await using var fake = TestService.InProcess(root.Path);
        fake.AddToken(AdaToken, "ada");
        using var client = new HttpClient(fake.CreateHandler());

        fake.FailNext("USER", TestFault.Teapot);
        Assert.Equal((HttpStatusCode)418, (await SendAsync(client, "/api/user", "Bearer " + AdaToken)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "/api/user", "Bearer " + AdaToken)).Status);
        Assert.Equal([418, 200], fake.Requests.Select(request => request.Status));
        Assert.Equal("Teapot", TestFault.Teapot.ToString());

        // Dropped: nothing is served, recorded with status 0.
        fake.FailNext("user", FakeServiceFault.Dropped);
        var dropped = await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync(client, "/api/user", "Bearer " + AdaToken));
        Assert.Equal("The fake of Test closed the connection without an answer.", dropped.Message);
        Assert.Equal((0, (string?)null), (fake.Requests[^1].Status, fake.Requests[^1].Login));

        // ServedThenDropped: served in full (signed in, recorded with its status), then dropped.
        fake.FailNext("things:x", FakeServiceFault.ServedThenDropped);
        await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync(client, "/api/Things/X", "Bearer " + AdaToken));
        Assert.Equal(("things:x", 200, "ada"), (fake.Requests[^1].Route, fake.Requests[^1].Status, fake.Requests[^1].Login));
        Assert.Equal(2, fake.Served);

        // Git's requests are the server's; a path the fake does not claim is 404 in process.
        Assert.Throws<NotSupportedException>(() => fake.FailNext("git:o/r", TestFault.Teapot));
        Assert.Throws<InvalidOperationException>(() => fake.HoldNext("git:o/r"));
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, "/o/r.git/info/refs", null)).Status);
        Assert.Null(fake.Requests[^1].Route);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Each_credential_form_is_read_and_no_record_holds_the_token()
    {
        using var root = new TempDirectory("fake core");
        await using var fake = TestService.InProcess(root.Path);
        fake.AddToken(AdaToken, "ada");
        using var client = new HttpClient(fake.CreateHandler());

        var cases = new (string Path, string? Authorization, string? PrivateToken, FakeAuthorizationForm Form, string? Login)[]
        {
            ("/api/user", null, null, FakeAuthorizationForm.None, null),
            ("/api/user", "Bearer " + AdaToken, null, FakeAuthorizationForm.Bearer, "ada"),
            ("/api/user", Basic("x-access-token:" + AdaToken), null, FakeAuthorizationForm.Basic, "ada"),
            ("/api/user", Basic(":" + AdaToken), null, FakeAuthorizationForm.BasicEmptyUser, "ada"),
            ("/api/user", null, AdaToken, FakeAuthorizationForm.PrivateToken, "ada"),
            ("/api/user?private_token=" + AdaToken + "&x=1", null, null, FakeAuthorizationForm.Query, "ada"),
            ("/api/user", "Token " + AdaToken, null, FakeAuthorizationForm.Other, null),
            ("/api/user", "Basic !!", null, FakeAuthorizationForm.Other, null),
            ("/api/user", "Bearer " + AdaToken, AdaToken, FakeAuthorizationForm.Other, null),
        };
        foreach (var (path, authorization, privateToken, form, login) in cases)
        {
            await SendAsync(client, path, authorization, privateToken);
            Assert.Equal((form, login), (fake.Requests[^1].Authorization, fake.Requests[^1].Login));
            Assert.Equal(form.ToString(), fake.LastForm);
        }

        Assert.Equal("private_token=***&x=1", fake.Requests[5].Query);
        Assert.All(fake.Requests, request => Assert.DoesNotContain(AdaToken, request.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(AdaToken, fake.LastCall, StringComparison.Ordinal);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Two_Authorization_headers_are_Other_in_process_and_over_TCP()
    {
        using var root = new TempDirectory("fake core");
        await using var fake = await TestService.StartAsync(root.Path);
        fake.AddToken(AdaToken, "ada");

        // In process the handler joins the two values with ", ", as the server does two lines.
        using (var client = new HttpClient(fake.CreateHandler()))
        using (var message = new HttpRequestMessage(HttpMethod.Get, new Uri("http://any.example/api/user")))
        {
            message.Headers.TryAddWithoutValidation("Authorization", ["Bearer " + AdaToken, "Bearer " + AdaToken]);
            using var response = await client.SendAsync(message, Cancelled);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal((FakeAuthorizationForm.Other, (string?)null), (fake.Requests[^1].Authorization, fake.Requests[^1].Login));
        }

        using (var tcp = new System.Net.Sockets.TcpClient())
        {
            await tcp.ConnectAsync(IPAddress.Loopback, fake.Port, Cancelled);
            var stream = tcp.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                $"GET /api/user HTTP/1.1\r\nHost: 127.0.0.1:{fake.Port}\r\nAuthorization: Bearer {AdaToken}\r\nAuthorization: Bearer {AdaToken}\r\nConnection: close\r\n\r\n"), Cancelled);
            using var reader = new StreamReader(stream, Encoding.ASCII);
            Assert.StartsWith("HTTP/1.1 401 ", await reader.ReadLineAsync(Cancelled), StringComparison.Ordinal);
            await reader.ReadToEndAsync(Cancelled);
        }

        Assert.Equal((FakeAuthorizationForm.Other, (string?)null), (fake.Requests[^1].Authorization, fake.Requests[^1].Login));
        Assert.Equal(2, fake.Requests.Count);
        Assert.Equal(0, fake.Served);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Revoke_refuses_the_token_on_the_API_and_on_git()
    {
        using var source = await TempRepo.CreateAsync("fake core source");
        await source.CommitFileAsync("README.md", "first\n", "First");
        await using var fake = await TestService.StartAsync(source.SiblingPath("served"));
        fake.AddToken(AdaToken, "ada");
        var bare = await fake.AddRepositoryAsync("Team/My Sub/Repo", gitSuffix: true, source.Path);
        Assert.Equal(Path.Combine(fake.Root, "Team", "My Sub", "Repo.git"), bare);
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        var url = $"http://x-access-token:{AdaToken}@127.0.0.1:{fake.Port}/Team/My%20Sub/Repo.git";

        Assert.True((await LsRemoteAsync(source, url)).Succeeded);
        Assert.Contains(fake.GitRequests, request => request is { Status: 200, UserName: "ada", GivenUserName: "x-access-token" });
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, $"http://127.0.0.1:{fake.Port}/api/user", "Bearer " + AdaToken)).Status);
        Assert.All(fake.GitRequests, request => Assert.False(request.Path.StartsWith("/api/", StringComparison.Ordinal)));

        fake.Revoke(AdaToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, $"http://127.0.0.1:{fake.Port}/api/user", "Bearer " + AdaToken)).Status);
        var refused = await LsRemoteAsync(source, url);
        Assert.False(refused.Succeeded);
        Assert.Contains(fake.GitRequests, request => request is { Status: 401, GivenUserName: "x-access-token" });

        // The git 401's realm is the fake's.
        using var challenge = await client.GetAsync(new Uri($"http://127.0.0.1:{fake.Port}/Team/My%20Sub/Repo.git/info/refs?service=git-upload-pack"), Cancelled);
        Assert.Equal(("Basic", "realm=\"Test\""), (challenge.Headers.WwwAuthenticate.Single().Scheme, challenge.Headers.WwwAuthenticate.Single().Parameter));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_git_hold_names_the_folder_in_its_case_and_holds_its_next_git_request()
    {
        using var source = await TempRepo.CreateAsync("fake core source");
        await source.CommitFileAsync("README.md", "first\n", "First");
        await using var fake = await TestService.StartAsync(source.SiblingPath("served"));
        fake.AddToken(AdaToken, "ada");
        await fake.AddRepositoryAsync("Team/Sub/Repo", gitSuffix: true, source.Path);
        Assert.Throws<ArgumentException>(() => fake.HoldNext("git:team/other"));

        var hold = fake.HoldNext("GIT:team/sub/repo.git");
        Assert.Equal("git:Team/Sub/Repo", hold.Route);
        // A second hold would share the first's gate, so it is refused until the first is disposed,
        // also once a request waits at it.
        Assert.Throws<InvalidOperationException>(() => fake.HoldNext("git:Team/Sub/Repo"));
        var listing = LsRemoteAsync(source, $"http://x-access-token:{AdaToken}@127.0.0.1:{fake.Port}/team/sub/repo");
        await hold.Arrived.WaitAsync(StepTimeout, Cancelled);
        Assert.False(listing.IsCompleted, "The git request was answered while it was held.");
        Assert.Empty(fake.GitRequests);
        Assert.Throws<InvalidOperationException>(() => fake.HoldNext("git:team/sub/repo"));

        hold.Dispose();
        Assert.True((await listing.WaitAsync(StepTimeout, Cancelled)).Succeeded);
        Assert.True(hold.Released.IsCompletedSuccessfully);
        Assert.Contains(fake.BeforeGit, name => name == "Team/Sub/Repo");

        // Disposed, the route may be held again; disposing the old hold twice changes nothing.
        using var again = fake.HoldNext("git:Team/Sub/Repo");
        hold.Dispose();
        Assert.False(again.Arrived.IsCompleted);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task GitAsync_runs_isolated_with_the_environment_given_and_the_bare_rule_is_in_place_clone_or_init()
    {
        using var source = await TempRepo.CreateAsync("fake core source");
        await source.CommitFileAsync("README.md", "first\n", "First");
        using var root = new TempDirectory("fake core");
        await using var fake = TestService.InProcess(root.Path);

        var ident = await fake.RunGitAsync(fake.Root, new Dictionary<string, string?>
        {
            ["GIT_AUTHOR_NAME"] = "Ada Lovelace",
            ["GIT_AUTHOR_EMAIL"] = "ada@example.com",
            ["GIT_AUTHOR_DATE"] = "2026-01-01T12:00:00+00:00",
        }, "var", "GIT_AUTHOR_IDENT");
        Assert.True(ident.Succeeded, ident.StandardError);
        Assert.Equal("Ada Lovelace <ada@example.com> 1767268800 +0000", ident.StandardOutput.Trim());
        var plain = await fake.RunGitAsync(fake.Root, null, "var", "GIT_AUTHOR_IDENT");
        Assert.DoesNotContain("Ada Lovelace", plain.StandardOutput, StringComparison.Ordinal);

        // A new empty one on main, a clone of a source, and an existing one in place (its source ignored).
        var empty = await fake.AddRepositoryAsync("g/empty", gitSuffix: true, null);
        Assert.Equal("ref: refs/heads/main", File.ReadAllText(Path.Combine(empty, "HEAD")).Trim());
        var cloned = await fake.AddRepositoryAsync("Org/Project/_git/cloned", gitSuffix: false, source.Path);
        Assert.Equal(Path.Combine(fake.Root, "Org", "Project", "_git", "cloned"), cloned);
        Assert.True((await fake.RunGitAsync(cloned, null, "rev-parse", "--verify", "--quiet", "refs/heads/main")).Succeeded);
        Assert.Equal(cloned, await fake.AddRepositoryAsync("Org/Project/_git/cloned", gitSuffix: false, "no such source"));
        await Assert.ThrowsAsync<ArgumentException>(() => fake.AddRepositoryAsync("g/../x", gitSuffix: true, null));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Disposing_lets_a_held_request_go_on_in_process()
    {
        using var root = new TempDirectory("fake core");
        await using var fake = TestService.InProcess(root.Path);
        using var client = new HttpClient(fake.CreateHandler());
        var hold = fake.HoldNext("user");
        var sent = SendAsync(client, "/api/user", null);
        await hold.Arrived.WaitAsync(StepTimeout, Cancelled);

        // Well within the hold's 120 s: the dispose released the request, which is answered.
        await fake.DisposeAsync();
        Assert.True(hold.Released.IsCompletedSuccessfully);
        var answered = await sent.WaitAsync(TimeSpan.FromSeconds(10), Cancelled);
        Assert.Equal(HttpStatusCode.Unauthorized, answered.Status);
        Assert.False(hold.Abandoned.IsCompleted);
        Assert.Equal(("user", 401), (fake.Requests.Single().Route, fake.Requests.Single().Status));
        Assert.Empty(fake.Errors);
        hold.Dispose();
    }

    [Fact]
    public async Task Disposing_lets_a_held_request_go_on_stops_the_server_and_refuses_new_holds()
    {
        using var root = new TempDirectory("fake core");
        var fake = await TestService.StartAsync(root.Path);
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        var port = fake.Port;
        Assert.NotEqual(TestService.InProcessPort, port);
        Assert.False(fake.PortWasTaken);

        using var pending = fake.HoldNext("things:y");
        var hold = fake.HoldNext("user");
        var sent = SendAsync(client, $"http://127.0.0.1:{port}/api/user", null);
        await hold.Arrived.WaitAsync(StepTimeout, Cancelled);

        await fake.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.Arrived);
        Assert.Throws<ObjectDisposedException>(() => fake.HoldNext("other"));
        // The held request was released and goes on into a server that is stopping: answered or cut
        // off, never left waiting.
        Assert.True(hold.Released.IsCompletedSuccessfully);
        await Task.WhenAny(sent).WaitAsync(StepTimeout, Cancelled);
        Assert.True(sent.IsCompleted);
        await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync(client, $"http://127.0.0.1:{port}/api/user", null));
        await fake.DisposeAsync();
        Assert.True(Directory.Exists(root.Path));
    }

    private static string Basic(string userAndPassword) => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userAndPassword));

    private static async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpClient client, string pathOrUrl, string? authorization, string? privateToken = null)
    {
        var uri = pathOrUrl.StartsWith('/') ? new Uri("http://any.example" + pathOrUrl) : new Uri(pathOrUrl);
        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        if (authorization is not null)
        {
            message.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        if (privateToken is not null)
        {
            message.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", privateToken);
        }

        using var response = await client.SendAsync(message, Cancelled);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(Cancelled));
    }

    private static Task<GitResult> LsRemoteAsync(TempRepo repo, string url)
    {
        var environment = new Dictionary<string, string?>(repo.Environment) { ["GIT_TERMINAL_PROMPT"] = "0" };
        return repo.Runner.RunAsync(new GitCommand("ls-remote", url) { WorkingDirectory = repo.Path, Environment = environment }, Cancelled);
    }

    /// <summary>The test's own fault.</summary>
    private sealed class TestFault : FakeServiceFault
    {
        private TestFault(string name)
            : base(name)
        {
        }

        public static TestFault Teapot { get; } = new(nameof(Teapot));
    }

    /// <summary>
    /// A fake of the core's own: <c>GET /api/user</c> (<c>user</c>) and <c>GET /api/things/&lt;x&gt;</c>
    /// (<c>things:&lt;x&gt;</c>) answer the token's login, 401 without a known token, 418 for
    /// <see cref="TestFault.Teapot"/>; every repository is private and nobody pushes.
    /// </summary>
    private sealed class TestService : FakeHostingService
    {
        public const int InProcessPort = 9;
        private readonly List<string> _beforeGit = [];
        private int _served;
        private string _lastForm = string.Empty;
        private string _lastCall = string.Empty;

        private TestService(string root)
            : base(root, "Test", InProcessPort)
        {
        }

        public int Served => Volatile.Read(ref _served);

        public string LastForm
        {
            get
            {
                lock (Gate)
                {
                    return _lastForm;
                }
            }
        }

        public string LastCall
        {
            get
            {
                lock (Gate)
                {
                    return _lastCall;
                }
            }
        }

        public IReadOnlyList<string> BeforeGit
        {
            get
            {
                lock (Gate)
                {
                    return [.. _beforeGit];
                }
            }
        }

        protected override string Realm => "Test";

        public static TestService InProcess(string root) => new(root);

        public static async Task<TestService> StartAsync(string root)
        {
            var fake = new TestService(root);
            await fake.StartServerAsync(0, fallBackToFreePort: false);
            return fake;
        }

        public void AddToken(string token, string login)
        {
            lock (Gate)
            {
                AddTokenLocked(token, login);
            }
        }

        public Task<string> AddRepositoryAsync(string urlPath, bool gitSuffix, string? source) =>
            BareRepositoryAsync(urlPath, gitSuffix, source, TestContext.Current.CancellationToken);

        public Task<GitResult> RunGitAsync(string folder, IReadOnlyDictionary<string, string?>? environment, params string[] arguments) =>
            GitAsync(folder, null, environment, TestContext.Current.CancellationToken, arguments);

        protected override bool Claims(string method, string path) => path.StartsWith("/api/", StringComparison.Ordinal);

        protected override string? RouteOf(string method, string path) => path.Split('/') switch
        {
            ["", "api", "user"] when method == "GET" => "user",
            ["", "api", "Things" or "things", var name] when method == "GET" => "things:" + name,
            _ => null,
        };

        protected override Task<AuthRemoteResponse> AnswerAsync(FakeServiceCall call, CancellationToken ct)
        {
            lock (Gate)
            {
                _lastForm = call.Authorization.ToString();
                _lastCall = call.ToString();
            }

            if (call.Fault == TestFault.Teapot)
            {
                return Text(418, "I'm a teapot");
            }

            string? login;
            lock (Gate)
            {
                login = call.Token is null ? null : TokenLoginLocked(call.Token);
            }

            if (login is null)
            {
                return Text(401, "Unauthorized");
            }

            call.Login = login;
            Interlocked.Increment(ref _served);
            return Text(200, login);
        }

        protected override bool IsPublic(string repository) => false;

        protected override bool MayPush(string repository, string login) => false;

        protected override Task BeforeGitAsync(string repository, CancellationToken ct)
        {
            lock (Gate)
            {
                _beforeGit.Add(repository);
            }

            return Task.CompletedTask;
        }

        private static Task<AuthRemoteResponse> Text(int status, string text) =>
            Task.FromResult(new AuthRemoteResponse(status, "Test", [KeyValuePair.Create("Content-Type", "text/plain")], Encoding.UTF8.GetBytes(text)));
    }
}
