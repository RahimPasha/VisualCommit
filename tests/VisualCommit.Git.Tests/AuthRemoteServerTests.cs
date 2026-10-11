using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The credential test server (design 7.3) with real git as its client: sign-in, refusal, the
/// held fetch, parallel connections and cancelling. Credentials go in the URL or in
/// <c>http.extraHeader</c>; no credential helper or askpass program takes part.
/// </summary>
public class AuthRemoteServerTests
{
    /// <summary>How long any one step may take before the test fails instead of hanging.</summary>
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(60);

    /// <summary>alice:s3cret as an <c>Authorization</c> header.</summary>
    private static readonly string AliceHeader = "http.extraHeader=Authorization: Basic " + Convert.ToBase64String("alice:s3cret"u8);

    [Fact]
    public async Task A_clone_with_credentials_in_the_url_gets_the_whole_history()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var destination = repo.SiblingPath("clone");

        await GitOkAsync(repo, "clone", "--quiet", WithCredentials(server.Url, "alice", "s3cret"), destination);

        Assert.Equal(["Remote work", "Initial commit"], Lines(await GitInAsync(repo, destination, "log", "--format=%s")));

        // curl may ask without credentials first (a 401), and then signs in as alice.
        var requests = server.Requests;
        Assert.Contains(requests, request => request.Status == 200 && request.UserName == "alice" && request.Path == "/remote.git/git-upload-pack");
        Assert.All(requests.Where(request => request.Status != 401), request => Assert.Equal("alice", request.UserName));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_fetch_with_an_authorization_header_brings_one_commit_without_a_401()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);

        await GitOkAsync(repo, "-c", AliceHeader, "fetch", "--quiet", "origin");

        Assert.Equal("Remote work", Subject(await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")));
        Assert.Equal("1", (await repo.GitAsync("rev-list", "--count", "refs/heads/main..refs/remotes/origin/main")).StandardOutput.Trim());
        var requests = server.Requests;
        Assert.All(requests, request => Assert.Equal((200, "alice"), (request.Status, request.UserName)));

        // Protocol v2, git's default: the refs are listed first, then the pack is fetched.
        Assert.Equal("/remote.git/info/refs", requests[0].Path);
        Assert.Equal("service=git-upload-pack", requests[0].Query);
        Assert.Equal(new string?[] { "ls-refs", "fetch" }, requests.Where(request => request.Command is not null).Select(request => request.Command));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task Without_credentials_git_is_refused_with_a_basic_challenge()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);

        var fetch = await GitAsync(repo, ["fetch", "origin"]);

        Assert.False(fetch.Succeeded);
        Assert.Contains("could not read Username", fetch.StandardError, StringComparison.Ordinal);
        Assert.All(server.Requests, request => Assert.Equal((401, (string?)null), (request.Status, request.UserName)));
        Assert.Equal("Initial commit", Subject(await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")));

        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        using var response = await client.GetAsync(new Uri(server.Url + "/info/refs?service=git-upload-pack"), TestCancelled);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal(("Basic", "realm=\"VisualCommit tests\""), (challenge.Scheme, challenge.Parameter));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_wrong_password_is_refused_and_a_changed_password_takes_effect()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);

        Assert.False((await GitAsync(repo, ["ls-remote", WithCredentials(server.Url, "alice", "wrong")])).Succeeded);
        Assert.False((await GitAsync(repo, ["ls-remote", WithCredentials(server.Url, "bob", "s3cret")])).Succeeded);
        Assert.True((await GitAsync(repo, ["ls-remote", WithCredentials(server.Url, "alice", "s3cret")])).Succeeded);

        server.Credentials = new NetworkCredential("alice", "n3w");
        Assert.False((await GitAsync(repo, ["ls-remote", WithCredentials(server.Url, "alice", "s3cret")])).Succeeded);
        var listed = await GitAsync(repo, ["ls-remote", WithCredentials(server.Url, "alice", "n3w"), "refs/heads/main"]);
        Assert.True(listed.Succeeded, listed.StandardError);
        Assert.EndsWith("\trefs/heads/main", listed.StandardOutput.Trim(), StringComparison.Ordinal);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task Without_RequireAuth_fetch_and_push_need_no_credentials()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        server.RequireAuth = false;

        await GitOkAsync(repo, "fetch", "--quiet", "origin");
        await repo.GitAsync("merge", "--quiet", "--ff-only", "refs/remotes/origin/main");
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");
        await GitOkAsync(repo, "push", "--quiet", "origin", "refs/heads/main:refs/heads/main");

        Assert.Equal(local, await GitInAsync(repo, server.RepositoryPath, "rev-parse", "refs/heads/main"));
        Assert.All(server.Requests, request => Assert.Equal((200, (string?)null), (request.Status, request.UserName)));
        Assert.Contains(server.Requests, request => request.Path == "/remote.git/git-receive-pack");
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_push_larger_than_the_post_buffer_arrives_chunked()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);

        // 300 KB that do not compress, sent with the smallest post buffer git allows (64 KiB).
        var noise = new byte[300 * 1024];
        new Random(3).NextBytes(noise);
        repo.WriteBytes("data/noise.bin", noise);
        var pushed = await repo.CommitAsync("Add noise");
        await GitOkAsync(repo, "-c", AliceHeader, "-c", "http.postBuffer=65536", "push", "--quiet", "--force", "origin", "refs/heads/main:refs/heads/main");

        Assert.Equal(pushed, await GitInAsync(repo, server.RepositoryPath, "rev-parse", "refs/heads/main"));
        Assert.Contains(server.Requests, request => request.Path == "/remote.git/git-receive-pack" && request.Chunked && request.Status == 200);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_compressed_request_is_served()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);

        // Forty branches on forty commits: a clone's fetch request then holds forty "want"
        // lines, above the 1 KiB from which git compresses it.
        var history = new HistoryStream();
        var base0 = history.Commit("refs/heads/base", "Base", file: "base.txt", content: "base\n");
        for (var i = 1; i <= 40; i++)
        {
            history.Commit($"refs/heads/b{i:D2}", $"Branch {i}", from: base0);
        }

        await ImportAsync(repo, history);
        await repo.GitAsync("push", "--quiet", server.RepositoryPath, "refs/heads/b*:refs/heads/b*");
        var destination = repo.SiblingPath("clone");
        await GitOkAsync(repo, "-c", AliceHeader, "clone", "--quiet", server.Url, destination);

        Assert.Equal(42, Lines(await GitInAsync(repo, destination, "branch", "--remotes")).Count(line => !line.Contains("->", StringComparison.Ordinal)));
        Assert.Contains(server.Requests, request => request.Compressed && request.Status == 200);
        Assert.Empty(server.Errors);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public async Task A_held_fetch_shows_progress_and_waits_until_released(int protocolVersion)
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        server.RequireAuth = false;
        var progress = new ProgressWatch();

        var held = server.HoldNextFetchPack();
        var fetch = GitAsync(repo, ["-c", $"protocol.version={protocolVersion}", "fetch", "--progress", "origin"], onError: progress.Add);
        await held.WaitAsync(StepTimeout, TestCancelled);
        var percentage = await progress.FirstPercentage.WaitAsync(StepTimeout, TestCancelled);

        // git shows the percentage and waits for the rest of the pack.
        Assert.Matches(@"\d+% \(\d+/\d+\)", percentage);
        Assert.False(fetch.IsCompleted, "The fetch ended while its pack was held.");
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestCancelled);
        Assert.False(fetch.IsCompleted, "The fetch ended while its pack was held.");
        Assert.Equal("Initial commit", Subject(await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")));

        server.Release();
        var result = await fetch.WaitAsync(StepTimeout, TestCancelled);

        Assert.True(result.Succeeded, result.StandardError);
        Assert.Equal("Remote work", Subject(await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")));
        var heldRequest = Assert.Single(server.Requests, request => request.Held);
        Assert.Equal("/remote.git/git-upload-pack", heldRequest.Path);
        Assert.Equal(protocolVersion == 2 ? "fetch" : null, heldRequest.Command);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_held_fetch_without_progress_is_held_too()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        server.RequireAuth = false;

        var held = server.HoldNextFetchPack();
        Assert.Same(held, server.HoldNextFetchPack());
        var fetch = GitAsync(repo, ["fetch", "--no-progress", "origin"]);
        await held.WaitAsync(StepTimeout, TestCancelled);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestCancelled);
        Assert.False(fetch.IsCompleted, "The fetch ended while its pack was held.");

        server.Release();
        Assert.True((await fetch.WaitAsync(StepTimeout, TestCancelled)).Succeeded);
        Assert.Single(server.Requests, request => request.Held);

        // The hold was used up: the next fetch of a pack is not held.
        var elsewhere = repo.OpenSibling("elsewhere");
        await elsewhere.CommitFileAsync("src/more.txt", "more\n", "More remote work");
        await elsewhere.GitAsync("push", "--quiet", "origin", "refs/heads/main:refs/heads/main");
        await GitOkAsync(repo, "fetch", "--quiet", "origin");
        Assert.Equal("More remote work", Subject(await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")));
        Assert.Single(server.Requests, request => request.Held);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task Other_connections_are_served_while_a_fetch_is_held()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);

        var held = server.HoldNextFetchPack();
        var fetch = GitAsync(repo, ["-c", AliceHeader, "fetch", "--progress", "origin"]);
        await held.WaitAsync(StepTimeout, TestCancelled);

        var listings = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => GitAsync(repo, ["-c", AliceHeader, "ls-remote", "origin", "refs/heads/main"])))
            .WaitAsync(StepTimeout, TestCancelled);
        var remoteWork = await GitInAsync(repo, server.RepositoryPath, "rev-parse", "refs/heads/main");
        Assert.All(listings, listing => Assert.Equal($"{remoteWork}\trefs/heads/main", listing.StandardOutput.Trim()));
        Assert.False(fetch.IsCompleted, "The fetch ended while its pack was held.");

        server.Release();
        Assert.True((await fetch.WaitAsync(StepTimeout, TestCancelled)).Succeeded);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task Cancelling_a_held_fetch_stops_git_and_the_server_carries_on()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);

        var held = server.HoldNextFetchPack();
        var fetch = RunGitAsync(repo, ["-c", AliceHeader, "fetch", "--progress", "origin"], onError: null, cancel.Token);
        await held.WaitAsync(StepTimeout, TestCancelled);

        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetch.WaitAsync(StepTimeout, TestCancelled));
        server.Release();

        // The server serves the next fetch in full.
        await GitOkAsync(repo, "-c", AliceHeader, "fetch", "--quiet", "origin");
        Assert.Equal("Remote work", Subject(await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task Disposing_the_server_ends_a_held_fetch_and_a_release_before_the_pack_drops_the_hold()
    {
        var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        try
        {
            // A release before any pack: the hold is dropped and its task cancelled.
            var dropped = server.HoldNextFetchPack();
            server.Release();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dropped.WaitAsync(StepTimeout, TestCancelled));

            var held = server.HoldNextFetchPack();
            var fetch = GitAsync(repo, ["-c", AliceHeader, "fetch", "--progress", "origin"]);
            await held.WaitAsync(StepTimeout, TestCancelled);

            await server.DisposeAsync().AsTask().WaitAsync(StepTimeout, TestCancelled);
            var result = await fetch.WaitAsync(StepTimeout, TestCancelled);

            Assert.False(result.Succeeded);
            Assert.False(Directory.Exists(server.RepositoryPath));
            Assert.Throws<ObjectDisposedException>(() => { _ = server.HoldNextFetchPack(); });
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task A_taken_port_gives_way_to_a_free_one_only_when_asked()
    {
        await using var first = await AuthRemoteServer.StartAsync();
        Assert.False(first.PortWasTaken);
        Assert.Equal($"http://127.0.0.1:{first.Port}/remote.git", first.Url);

        await Assert.ThrowsAnyAsync<SocketException>(() => AuthRemoteServer.StartAsync(first.Port));
        await using var second = await AuthRemoteServer.StartAsync(first.Port, fallBackToFreePort: true);

        Assert.True(second.PortWasTaken);
        Assert.NotEqual(first.Port, second.Port);
        Assert.Equal(47391, AuthRemoteServer.VisualChecksPort);
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// <summary>
    /// Runs git in <paramref name="repo"/> with its isolation and without any askpass program or
    /// proxy that the test process may have inherited: only what the test gives reaches the server.
    /// </summary>
    private static Task<GitResult> GitAsync(TempRepo repo, string[] arguments, Action<string>? onError = null) =>
        RunGitAsync(repo, arguments, onError, TestCancelled);

    private static Task<GitResult> RunGitAsync(TempRepo repo, string[] arguments, Action<string>? onError, CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, string?>(repo.Environment);
        foreach (var variable in new[] { "GIT_ASKPASS", "SSH_ASKPASS", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy" })
        {
            environment[variable] = null;
        }

        var command = new GitCommand(arguments) { WorkingDirectory = repo.Path, Environment = environment, OnErrorLine = onError };
        return repo.Runner.RunAsync(command, cancellationToken);
    }

    private static async Task GitOkAsync(TempRepo repo, params string[] arguments)
    {
        var result = await GitAsync(repo, arguments);
        Assert.True(result.Succeeded, $"git {string.Join(' ', arguments)}: {result.StandardError}");
    }

    private static async Task<string> GitInAsync(TempRepo repo, string folder, params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = folder, Environment = repo.Environment };
        return (await repo.Runner.RunAsync(command, TestCancelled)).EnsureSuccess(command).StandardOutput.Trim();
    }

    private static string WithCredentials(string url, string user, string password) =>
        url.Replace("http://", $"http://{user}:{password}@", StringComparison.Ordinal);

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string Subject(GitResult result) => result.StandardOutput.Trim();

    /// <summary>Watches git's standard error for the first progress line with a percentage.</summary>
    private sealed class ProgressWatch
    {
        private readonly TaskCompletionSource<string> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<string> _lines = new();

        public Task<string> FirstPercentage => _first.Task;

        public void Add(string line)
        {
            _lines.Enqueue(line);
            if (line.Contains('%', StringComparison.Ordinal))
            {
                _first.TrySetResult(line);
            }
        }

        public override string ToString() => string.Join('\n', _lines);
    }
}
