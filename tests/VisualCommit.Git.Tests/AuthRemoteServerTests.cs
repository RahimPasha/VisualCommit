using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

    [Theory]
    [InlineData(1)]
    [InlineData(45)]
    [InlineData(100)]
    public async Task A_clone_held_at_a_percentage_shows_exactly_that_percentage_until_released(int atPercent)
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await ProgressRepositoryAsync(server);
        var progress = new ProgressWatch();
        var destination = repo.SiblingPath("clone");

        var held = server.HoldNextFetchPack(atPercent);
        var clone = GitAsync(repo, ["clone", "--progress", server.Url, destination], onError: progress.Add);
        await held.WaitAsync(StepTimeout, TestCancelled);
        await progress.WaitForLineAsync($"{atPercent}% (", StepTimeout);

        // Whatever reached git before the hold has come by now; nothing after it comes. The
        // counting reaches atPercent at its ceil(atPercent * objects / 100)th object.
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestCancelled);
        Assert.False(clone.IsCompleted, "The clone ended while its pack was held.");
        var counted = ((atPercent * ProgressObjects) + 99) / 100;
        Assert.Equal($"remote: Counting objects: {atPercent,3}% ({counted}/{ProgressObjects})", progress.LastPercentage?.TrimEnd());

        server.Release();
        var result = await clone.WaitAsync(StepTimeout, TestCancelled);

        Assert.True(result.Succeeded, result.StandardError);
        Assert.Equal(ProgressCommits.ToString(CultureInfo.InvariantCulture), await GitInAsync(repo, destination, "rev-list", "--count", "HEAD"));
        Assert.Equal($"remote: Counting objects: 100% ({ProgressObjects}/{ProgressObjects}), done.", progress.Lines.Single(line => line.Contains("Counting objects: 100%", StringComparison.Ordinal) && line.Contains("done", StringComparison.Ordinal)).TrimEnd());
        Assert.Single(server.Requests, request => request.Held);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_clone_held_at_0_is_held_after_the_first_progress_packet_as_before()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await ProgressRepositoryAsync(server);
        var progress = new ProgressWatch();
        var destination = repo.SiblingPath("clone");

        var held = server.HoldNextFetchPack(0);
        Assert.Same(held, server.HoldNextFetchPack(45));
        var clone = GitAsync(repo, ["clone", "--progress", server.Url, destination], onError: progress.Add);
        await held.WaitAsync(StepTimeout, TestCancelled);
        var percentage = await progress.FirstPercentage.WaitAsync(StepTimeout, TestCancelled);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestCancelled);

        Assert.Matches($@"^remote: Counting objects: +0% \(\d+/{ProgressObjects}\)$", percentage.TrimEnd());
        Assert.False(clone.IsCompleted, "The clone ended while its pack was held.");

        server.Release();
        var result = await clone.WaitAsync(StepTimeout, TestCancelled);
        Assert.True(result.Succeeded, result.StandardError);
        Assert.Single(server.Requests, request => request.Held);
        Assert.Empty(server.Errors);
    }

    [Theory]
    [InlineData(1, "Counting objects:  25% (1/4)\r", true)]
    [InlineData(25, "Counting objects:  25% (1/4)\r", true)]
    [InlineData(30, "Counting objects:  50% (2/4)\r", true)]
    [InlineData(75, "Counting objects:  75% (3/4)\r", false)]
    [InlineData(100, "Counting objects: 100% (4/4)\r", true)]
    public void A_hold_at_a_percentage_ends_right_after_the_first_whole_line_that_reaches_it(int atPercent, string line, bool splits)
    {
        var body = ProgressResponse();

        var (sent, holdAt) = AuthRemoteServer.SplitAtProgress(body, atPercent);

        // The first part ends with that line: the 50% line runs over two pkt-lines, the 75% line
        // ends with its own, the others end inside one, which is split in two there.
        Assert.Contains(PktLinesOf(sent), pktLine => pktLine.Offset == holdAt);
        Assert.EndsWith(line, BandText(sent[..holdAt], 2), StringComparison.Ordinal);
        if (splits)
        {
            Assert.Equal(body.Length + 5, sent.Length);
        }
        else
        {
            Assert.Same(body, sent);
        }

        // Every pkt-line has a length that fits, and the bands carry what they carried.
        Assert.Equal(BandText(body, 2), BandText(sent, 2));
        Assert.Equal(BandText(body, 1), BandText(sent, 1));
        Assert.Equal(PktLinesOf(body).Count + (splits ? 1 : 0), PktLinesOf(sent).Count);
    }

    [Fact]
    public void Without_a_percentage_the_hold_ends_with_the_first_progress_pkt_line_that_holds_one_as_before()
    {
        var body = ProgressResponse();

        var (hasPack, progressEnd) = AuthRemoteServer.ScanPktLines(body);

        Assert.True(hasPack);
        Assert.EndsWith("Counting objects:  25% (1/4)\rCounting objects:  5", Encoding.Latin1.GetString(body, 0, progressEnd), StringComparison.Ordinal);

        // No line reaches the percentage when git asked for no progress: then only the headers go first.
        byte[] quiet = [.. Pkt("packfile\n"), .. Band(1, "PACK...."), .. "0000"u8];
        Assert.Equal((quiet, -1), AuthRemoteServer.SplitAtProgress(quiet, 45));
        Assert.Equal((true, -1), AuthRemoteServer.ScanPktLines(quiet));
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
        Assert.Equal(47392, AuthRemoteServer.Phase1VisualChecksPort);
        Assert.Equal(47393, AuthRemoteServer.Phase6VisualChecksPort);
        Assert.Equal(47394, AuthRemoteServer.Phase6LookVisualChecksPort);
        Assert.Equal(47395, AuthRemoteServer.Phase7aVisualChecksPort);
    }

    // ---- A host (docs/phase-7a-design.md 2.2, D266) --------------------------------------------------

    [Fact]
    public async Task A_host_serves_two_levels_registered_after_the_start_with_or_without_git_in_the_path()
    {
        using var host = await HostFolderAsync();
        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, host.Options);
        Assert.True(server.ServesFolder);
        Assert.Empty(server.ServedRepositories);

        // Registered after the start; a folder that is no bare repository, or a name of one level, is refused.
        server.Serve("octo/hello");
        server.Serve("acme/tool.git");
        Assert.Equal(["acme/tool", "octo/hello"], server.ServedRepositories);
        Assert.Throws<ArgumentException>(() => server.Serve("octo/missing"));
        Assert.Throws<ArgumentException>(() => server.Serve("hello"));
        Assert.Throws<ArgumentException>(() => server.Serve("octo/he llo"));
        Assert.Equal($"http://127.0.0.1:{server.Port}/octo/hello.git", server.UrlOf("octo/hello"));

        // Token mode: any user name with a known token signs in as the token's login.
        var withGit = WithCredentials(server.UrlOf("octo/hello"), "x-access-token", OctoToken);
        var withoutGit = WithCredentials($"http://127.0.0.1:{server.Port}/octo/hello", "x-access-token", OctoToken);
        await GitOkAsync(host.Repo, "clone", "--quiet", withGit, host.Repo.SiblingPath("with-git"));
        await GitOkAsync(host.Repo, "clone", "--quiet", withoutGit, host.Repo.SiblingPath("without-git"));

        Assert.Equal(["Second", "First"], Lines(await GitInAsync(host.Repo, host.Repo.SiblingPath("with-git"), "log", "--format=%s")));
        Assert.Equal(["Second", "First"], Lines(await GitInAsync(host.Repo, host.Repo.SiblingPath("without-git"), "log", "--format=%s")));
        var signedIn = server.Requests.Where(request => request.Status == 200).ToList();
        Assert.Contains(signedIn, request => request.Path == "/octo/hello/git-upload-pack");
        Assert.Contains(signedIn, request => request.Path == "/octo/hello.git/git-upload-pack");
        Assert.All(signedIn, request => Assert.Equal(("octo", "x-access-token"), (request.UserName, request.GivenUserName)));

        // A server that is not a host's registers nothing by name.
        await using var plain = await AuthRemoteServer.StartAsync();
        Assert.Throws<InvalidOperationException>(() => plain.Serve("octo/hello"));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_claimed_path_is_answered_before_auth_and_an_unclaimed_one_goes_to_auth_then_404()
    {
        using var host = await HostFolderAsync();
        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, host.Options);
        server.Serve("octo/hello");
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });

        using var claimed = await client.GetAsync(new Uri($"http://127.0.0.1:{server.Port}/api/v3/ping?x=1"), TestCancelled);
        Assert.Equal(HttpStatusCode.OK, claimed.StatusCode);
        Assert.Equal("pong GET /api/v3/ping x=1 " + $"127.0.0.1:{server.Port}", await claimed.Content.ReadAsStringAsync(TestCancelled));
        Assert.Equal(["text/plain"], claimed.Content.Headers.GetValues("Content-Type"));
        Assert.Equal("yes", Assert.Single(claimed.Headers.GetValues("X-Fake")));

        using var posted = await client.PostAsync(new Uri($"http://127.0.0.1:{server.Port}/api/v3/ping"), new StringContent("{\"a\":1}"), TestCancelled);
        Assert.Equal("pong POST /api/v3/ping  " + $"127.0.0.1:{server.Port}" + " {\"a\":1}", await posted.Content.ReadAsStringAsync(TestCancelled));

        // The hook runs ahead of auth: a header git's auth would refuse (a token nobody has, or a
        // scheme it does not read) still reaches the claimed route.
        foreach (var header in new[] { BadBasicHeader, "Bearer " + OctoToken })
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, new Uri($"http://127.0.0.1:{server.Port}/api/v3/ping"));
            message.Headers.TryAddWithoutValidation("Authorization", header);
            using var answered = await client.SendAsync(message, TestCancelled);
            Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
            Assert.Equal("pong GET /api/v3/ping  " + $"127.0.0.1:{server.Port}", await answered.Content.ReadAsStringAsync(TestCancelled));
        }

        // A dropped answer closes the connection without a word (the client may try again first).
        await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsync(new Uri($"http://127.0.0.1:{server.Port}/api/v3/drop"), new StringContent("{}"), TestCancelled));

        // Without a header an unclaimed path in no served repository gets its 404.
        foreach (var path in new[] { "/api/v3/other", "/octo/missing.git/info/refs", "/octo", "/" })
        {
            using var unclaimed = await client.GetAsync(new Uri($"http://127.0.0.1:{server.Port}{path}"), TestCancelled);
            Assert.Equal(HttpStatusCode.NotFound, unclaimed.StatusCode);
        }

        // With a header auth cannot read, every unclaimed path gets 401 before any 404, as on the
        // plain server: a served private repository's and one in no served repository alike.
        foreach (var path in new[] { "/octo/hello.git/info/refs?service=git-upload-pack", "/octo/missing.git/info/refs?service=git-upload-pack" })
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, new Uri($"http://127.0.0.1:{server.Port}{path}"));
            message.Headers.TryAddWithoutValidation("Authorization", BadBasicHeader);
            using var refused = await client.SendAsync(message, TestCancelled);
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        var requests = server.Requests;
        Assert.Equal([200, 200, 200, 200], requests.Where(request => request.Path == "/api/v3/ping").Select(request => request.Status));
        Assert.All(requests.Where(request => request.Path == "/api/v3/drop"), request => Assert.Equal(("POST", 201), (request.Method, request.Status)));
        Assert.Contains(requests, request => request.Path == "/api/v3/drop");
        Assert.Equal(4, requests.Count(request => request.Status == 404));
        Assert.Equal(["/octo/hello.git/info/refs", "/octo/missing.git/info/refs"], requests.Where(request => request.Status == 401).Select(request => request.Path));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_public_repository_is_read_without_credentials_and_its_push_is_refused()
    {
        using var host = await HostFolderAsync();
        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, host.Options);
        server.Serve("acme/tool");
        var clone = host.Repo.SiblingPath("anonymous");

        await GitOkAsync(host.Repo, "clone", "--quiet", server.UrlOf("acme/tool"), clone);

        Assert.Equal(["Second", "First"], Lines(await GitInAsync(host.Repo, clone, "log", "--format=%s")));
        Assert.All(server.Requests, request => Assert.Equal((200, (string?)null), (request.Status, request.UserName)));

        // Without credentials a push is asked for them, and git has nobody to ask.
        var push = await GitAsync(host.Repo, ["-C", clone, "push", "origin", "HEAD:refs/heads/topic"]);
        Assert.False(push.Succeeded);
        Assert.Contains("could not read Username", push.StandardError, StringComparison.Ordinal);
        Assert.Contains(server.Requests, request => request.Query == "service=git-receive-pack" && request.Status == 401);

        // A wrong header is refused even for a public read. (Git sends the URL's credentials only
        // after a 401, so a public read through git never shows them; a client that sends them
        // first is refused.)
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        foreach (var header in new[] { "Basic " + Convert.ToBase64String("x-access-token:ghp_FAKE-nobody-9"u8), "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + AdaToken)), "Bearer " + AdaToken })
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(server.UrlOf("acme/tool") + "/info/refs?service=git-upload-pack"));
            message.Headers.TryAddWithoutValidation("Authorization", header);
            using var refused = await client.SendAsync(message, TestCancelled);
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        // An unknown token's request keeps the user name it gave; the unreadable ones give none.
        Assert.Equal(
            [(401, null, "x-access-token"), (401, null, null), (401, null, null)],
            server.Requests.TakeLast(3).Select(request => (request.Status, request.UserName, request.GivenUserName)));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_private_repository_asks_for_a_token_and_a_login_that_may_not_push_gets_403()
    {
        using var host = await HostFolderAsync();
        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, host.Options);
        server.Serve("octo/hello");

        var anonymous = await GitAsync(host.Repo, ["ls-remote", server.UrlOf("octo/hello")]);
        Assert.False(anonymous.Succeeded);
        Assert.Contains("could not read Username", anonymous.StandardError, StringComparison.Ordinal);
        using (var client = new HttpClient(new SocketsHttpHandler { UseProxy = false }))
        {
            using var response = await client.GetAsync(new Uri(server.UrlOf("octo/hello") + "/info/refs?service=git-upload-pack"), TestCancelled);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var challenge = Assert.Single(response.Headers.WwwAuthenticate);
            Assert.Equal(("Basic", "realm=\"GitHub\""), (challenge.Scheme, challenge.Parameter));
        }

        // Ada may read but not push; octo may push.
        var clone = host.Repo.SiblingPath("ada");
        await GitOkAsync(host.Repo, "clone", "--quiet", WithCredentials(server.UrlOf("octo/hello"), "x-access-token", AdaToken), clone);
        var refused = await GitAsync(host.Repo, ["-C", clone, "push", "origin", "HEAD:refs/heads/topic"]);
        Assert.False(refused.Succeeded);
        Assert.Contains(server.Requests, request => request.Status == 403 && request.UserName == "ada" && request.Query == "service=git-receive-pack");
        Assert.False((await GitAsync(host.Repo, ["-C", Path.Combine(host.Folder, "octo", "hello.git"), "rev-parse", "--verify", "--quiet", "refs/heads/topic"])).Succeeded);

        await GitOkAsync(host.Repo, "-C", clone, "push", "--quiet", WithCredentials(server.UrlOf("octo/hello"), "x-access-token", OctoToken), "HEAD:refs/heads/topic");
        Assert.Equal(
            await GitInAsync(host.Repo, clone, "rev-parse", "HEAD"),
            await GitInAsync(host.Repo, Path.Combine(host.Folder, "octo", "hello.git"), "rev-parse", "refs/heads/topic"));
        Assert.Contains(server.Requests, request => request.Status == 200 && request.UserName == "octo" && request.Path == "/octo/hello.git/git-receive-pack");
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_hosts_request_is_held_by_owner_and_name()
    {
        using var host = await HostFolderAsync();
        await using var server = await AuthRemoteServer.StartForHostAsync(host.Folder, host.Options);
        server.Serve("octo/hello");
        server.Serve("acme/tool");

        var arrived = server.HoldNextRequest("octo/hello");
        Assert.Same(arrived, server.HoldNextRequest("octo/hello.git"));
        var listing = GitAsync(host.Repo, ["ls-remote", WithCredentials(server.UrlOf("octo/hello"), "x-access-token", OctoToken)]);
        await arrived.WaitAsync(StepTimeout, TestCancelled);

        // The other repository is served meanwhile.
        var tool = await GitAsync(host.Repo, ["ls-remote", server.UrlOf("acme/tool")]).WaitAsync(StepTimeout, TestCancelled);
        Assert.True(tool.Succeeded, tool.StandardError);
        Assert.DoesNotContain(server.Requests, request => request.Path.StartsWith("/octo/", StringComparison.Ordinal));
        Assert.False(listing.IsCompleted, "octo/hello's request was answered while it was held.");

        server.ReleaseRequest("octo/hello");
        var result = await listing.WaitAsync(StepTimeout, TestCancelled);
        Assert.True(result.Succeeded, result.StandardError);
        Assert.Contains("\trefs/heads/main", result.StandardOutput, StringComparison.Ordinal);
        Assert.Empty(server.Errors);
    }

    private const string OctoToken = "github_pat_FAKE-octo-1";
    private const string AdaToken = "github_pat_FAKE-ada-3";

    /// <summary>Basic with a token the host does not know.</summary>
    private static readonly string BadBasicHeader = "Basic " + Convert.ToBase64String("x-access-token:ghp_FAKE-nobody-9"u8);

    /// <summary>
    /// A host's folder (2.2): <c>octo/hello.git</c> (private, octo pushes) and <c>acme/tool.git</c>
    /// (public, ada pushes), each a bare clone of a repository of two commits, and the options of a
    /// host that knows octo's and ada's tokens and claims <c>/api/v3/ping</c> and <c>/api/v3/drop</c>.
    /// </summary>
    private static async Task<HostFolder> HostFolderAsync()
    {
        var repo = await TempRepo.CreateAsync("host");
        try
        {
            await repo.CommitFileAsync("README.md", "first\n", "First");
            await repo.CommitFileAsync("README.md", "second\n", "Second");
            var folder = repo.SiblingPath("github");
            foreach (var name in new[] { "octo/hello", "acme/tool" })
            {
                await repo.GitAsync("clone", "--quiet", "--bare", "--no-hardlinks", repo.Path, Path.Combine(folder, name + ".git"));
            }

            var options = new AuthRemoteHostOptions(
                new PingRoutes(),
                token => token switch { OctoToken => "octo", AdaToken => "ada", _ => null },
                repository => repository == "acme/tool",
                (repository, login) => (repository, login) is ("octo/hello", "octo") or ("acme/tool", "ada"));
            return new HostFolder(repo, folder, options);
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private sealed record HostFolder(TempRepo Repo, string Folder, AuthRemoteHostOptions Options) : IDisposable
    {
        public void Dispose() => Repo.Dispose();
    }

    /// <summary>Routes that answer <c>/api/v3/ping</c> with what they were asked, and drop <c>/api/v3/drop</c>'s answer.</summary>
    private sealed class PingRoutes : IAuthRemoteRoutes
    {
        public bool Claims(string method, string path) => path is "/api/v3/ping" or "/api/v3/drop";

        public Task<AuthRemoteResponse> ServeAsync(AuthRemoteRouteRequest request, CancellationToken cancellationToken)
        {
            var body = Encoding.UTF8.GetString(request.Body);
            var text = $"pong {request.Method} {request.Path} {request.Query} {request.Host}" + (body.Length > 0 ? " " + body : string.Empty);
            return Task.FromResult(new AuthRemoteResponse(
                request.Path == "/api/v3/drop" ? 201 : 200,
                "OK",
                [KeyValuePair.Create("Content-Type", "text/plain"), KeyValuePair.Create("X-Fake", "yes")],
                Encoding.UTF8.GetBytes(text),
                Drop: request.Path == "/api/v3/drop"));
        }
    }

    // ---- A folder of repositories (docs/phase-6-design.md 17.2) ----------------------------------

    [Fact]
    public async Task A_folder_server_serves_each_repository_at_its_own_url()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var remotes = Path.Combine(copy.Root, "remotes");
        var docs = copy.Repo("docs");
        var configBefore = File.ReadAllText(Path.Combine(copy.RemoteOf("docs"), "config"));
        await using (var server = await AuthRemoteServer.StartForFolderAsync(remotes))
        {
            server.RequireAuth = false;

            Assert.True(server.ServesFolder);
            Assert.True(server.ServesInPlace);
            Assert.Equal(["api", "billing", "design-system", "docs", "infra", "mobile", "scratch", "web-app"], server.ServedRepositories);
            Assert.Equal($"http://127.0.0.1:{server.Port}/docs.git", server.UrlOf("docs"));
            Assert.Equal(server.UrlOf("docs"), server.UrlOf("docs.git"));
            Assert.Throws<ArgumentException>(() => server.UrlOf("notes"));
            Assert.Throws<InvalidOperationException>(() => server.Url);
            Assert.Throws<InvalidOperationException>(() => { _ = server.HoldNextFetchPack(); });

            // Two repositories, each at its own URL, each listing its own refs.
            foreach (var name in new[] { "docs", "mobile" })
            {
                var listing = await GitAsync(docs, ["ls-remote", server.UrlOf(name)]);
                Assert.True(listing.Succeeded, listing.StandardError);
                var refs = await GitInAsync(docs, copy.RemoteOf(name), "for-each-ref", "--format=%(objectname)%09%(refname)");
                Assert.Equal(refs, listing.StandardOutput.Trim());
            }

            Assert.All(server.Requests, request => Assert.Equal(200, request.Status));
            Assert.Contains(server.Requests, request => request.Path.StartsWith("/docs.git/", StringComparison.Ordinal));
            Assert.Contains(server.Requests, request => request.Path.StartsWith("/mobile.git/", StringComparison.Ordinal));
            Assert.Empty(server.Errors);
        }

        // Served in place: nothing in the folder changed, and nothing of it was deleted.
        Assert.Equal(configBefore, File.ReadAllText(Path.Combine(copy.RemoteOf("docs"), "config")));
        Assert.True(Directory.Exists(copy.RemoteOf("mobile")));
    }

    [Fact]
    public async Task A_folder_with_a_folder_it_cannot_serve_is_refused()
    {
        // Every folder in it is checked as a repository served in place is (17.2): a scenario
        // whose remote is broken or misnamed fails at the start, not later with a 404.
        using var copy = await Scenarios.WorkspaceAsync();
        var remotes = Path.Combine(copy.Root, "remotes");
        var docs = copy.Repo("docs");

        var plain = Path.Combine(remotes, "not-a-repository");
        Directory.CreateDirectory(plain);
        var refused = await Assert.ThrowsAsync<ArgumentException>(() => AuthRemoteServer.StartForFolderAsync(remotes));
        Assert.Contains("not-a-repository is not a bare repository", refused.Message, StringComparison.Ordinal);
        Directory.Delete(plain);

        await GitInAsync(docs, remotes, "init", "--quiet", "--bare", "two words.git");
        refused = await Assert.ThrowsAsync<ArgumentException>(() => AuthRemoteServer.StartForFolderAsync(remotes));
        Assert.Contains("'two words.git'", refused.Message, StringComparison.Ordinal);
        Directory.Delete(Path.Combine(remotes, "two words.git"), recursive: true);

        var empty = Path.Combine(copy.Root, "no-remotes");
        Directory.CreateDirectory(empty);
        await Assert.ThrowsAsync<ArgumentException>(() => AuthRemoteServer.StartForFolderAsync(empty));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => AuthRemoteServer.StartForFolderAsync(Path.Combine(copy.Root, "missing")));

        // With those gone, the eight remotes are served.
        await using var server = await AuthRemoteServer.StartForFolderAsync(remotes);
        Assert.Equal(8, server.ServedRepositories.Count);
    }

    [Fact]
    public async Task Holds_of_two_repositories_are_independent()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await using var server = await AuthRemoteServer.StartForFolderAsync(Path.Combine(copy.Root, "remotes"));
        server.RequireAuth = false;
        var docs = copy.Repo("docs");
        var design = copy.Repo("design-system");
        await docs.GitAsync("remote", "set-url", "origin", server.UrlOf("docs"));
        await design.GitAsync("remote", "set-url", "origin", server.UrlOf("design-system"));
        var docsProgress = new ProgressWatch();
        var designProgress = new ProgressWatch();

        // docs counts 4 objects, design-system 11 (0.10): held at 25 and 45, each shows exactly that.
        var docsHeld = server.HoldNextFetchPack("docs", 25);
        var designHeld = server.HoldNextFetchPack("design-system", 45);
        var docsFetch = GitAsync(docs, ["fetch", "--progress", "origin"], onError: docsProgress.Add);
        var designFetch = GitAsync(design, ["fetch", "--progress", "origin"], onError: designProgress.Add);
        await Task.WhenAll(docsHeld, designHeld).WaitAsync(StepTimeout, TestCancelled);
        await docsProgress.WaitForLineAsync("25% (", StepTimeout);
        await designProgress.WaitForLineAsync("45% (", StepTimeout);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestCancelled);
        Assert.Equal("remote: Counting objects:  25% (1/4)", docsProgress.LastPercentage?.TrimEnd());
        Assert.Equal("remote: Counting objects:  45% (5/11)", designProgress.LastPercentage?.TrimEnd());
        Assert.False(docsFetch.IsCompleted, "docs' fetch ended while its pack was held.");
        Assert.False(designFetch.IsCompleted, "design-system's fetch ended while its pack was held.");

        // Released one at a time: docs ends, design-system stays held.
        server.Release("docs");
        var docsResult = await docsFetch.WaitAsync(StepTimeout, TestCancelled);
        Assert.True(docsResult.Succeeded, docsResult.StandardError);
        Assert.Equal(await GitInAsync(docs, copy.RemoteOf("docs"), "rev-parse", "refs/heads/main"), await docs.RefTargetAsync("refs/remotes/origin/main"));
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestCancelled);
        Assert.False(designFetch.IsCompleted, "design-system's fetch ended when only docs was released.");
        Assert.Equal("remote: Counting objects:  45% (5/11)", designProgress.LastPercentage?.TrimEnd());

        server.Release("design-system");
        var designResult = await designFetch.WaitAsync(StepTimeout, TestCancelled);
        Assert.True(designResult.Succeeded, designResult.StandardError);
        Assert.Equal(await GitInAsync(design, copy.RemoteOf("design-system"), "rev-parse", "refs/heads/main"), await design.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Equal(
            ["/design-system.git/git-upload-pack", "/docs.git/git-upload-pack"],
            server.Requests.Where(request => request.Held).Select(request => request.Path).Order(StringComparer.Ordinal));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_path_outside_the_served_repositories_is_refused()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await using var server = await AuthRemoteServer.StartForFolderAsync(Path.Combine(copy.Root, "remotes"));
        server.RequireAuth = false;
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });

        foreach (var path in new[] { "/notes.git/info/refs", "/remotes/docs.git/info/refs", "/docs.git", "/missing.git/info/refs", "/" })
        {
            using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{server.Port}{path}?service=git-upload-pack"), TestCancelled);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var served = await client.GetAsync(new Uri(server.UrlOf("docs") + "/info/refs?service=git-upload-pack"), TestCancelled);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(5, server.Requests.Count(request => request.Status == 404));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_held_request_gets_its_401_only_after_release()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        await using var server = await AuthRemoteServer.StartForFolderAsync(Path.Combine(copy.Root, "remotes"));
        var repo = copy.Repo("docs");

        var arrived = server.HoldNextRequest("mobile");
        Assert.Same(arrived, server.HoldNextRequest("mobile.git"));
        var mobile = GitAsync(repo, ["ls-remote", server.UrlOf("mobile")]);
        await arrived.WaitAsync(StepTimeout, TestCancelled);

        // The other repository is served meanwhile: its 401 is recorded, mobile's is not.
        var docs = await GitAsync(repo, ["ls-remote", server.UrlOf("docs")]).WaitAsync(StepTimeout, TestCancelled);
        Assert.False(docs.Succeeded);
        Assert.Contains(server.Requests, request => request.Path == "/docs.git/info/refs" && request.Status == 401);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestCancelled);
        Assert.DoesNotContain(server.Requests, request => request.Path.StartsWith("/mobile.git/", StringComparison.Ordinal));
        Assert.False(mobile.IsCompleted, "mobile's request was answered while it was held.");

        server.ReleaseRequest("mobile");
        var result = await mobile.WaitAsync(StepTimeout, TestCancelled);
        Assert.False(result.Succeeded);
        Assert.Contains(server.Requests, request => request.Path == "/mobile.git/info/refs" && request.Status == 401);

        // The gate held that one request only; a release with no request waiting drops the gate.
        var dropped = server.HoldNextRequest("mobile");
        server.ReleaseRequest("mobile");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dropped.WaitAsync(StepTimeout, TestCancelled));
        var again = await GitAsync(repo, ["ls-remote", server.UrlOf("mobile")]).WaitAsync(StepTimeout, TestCancelled);
        Assert.False(again.Succeeded);
        Assert.Equal(2, server.Requests.Count(request => request.Path == "/mobile.git/info/refs" && request.Status == 401));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_request_held_longer_than_the_request_timeout_still_reads_its_body()
    {
        // The time to send the rest of a held request starts again when its gate opens, however
        // long it waited (17.2): a POST whose body came after the connection's own time had run
        // out lost its connection without a word in Errors.
        using var copy = await Scenarios.WorkspaceAsync();
        await using var server = await AuthRemoteServer.StartForFolderAsync(Path.Combine(copy.Root, "remotes"));
        server.RequireAuth = false;
        server.RequestTimeout = TimeSpan.FromSeconds(2);
        var arrived = server.HoldNextRequest("docs");

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port, TestCancelled);
        var stream = client.GetStream();
        var body = "0000"u8.ToArray();
        var head = "POST /docs.git/git-upload-pack HTTP/1.1\r\nHost: 127.0.0.1\r\n" +
            $"Content-Type: application/x-git-upload-pack-request\r\nContent-Length: {body.Length}\r\n\r\n";
        await stream.WriteAsync(Encoding.Latin1.GetBytes(head), TestCancelled);
        await arrived.WaitAsync(StepTimeout, TestCancelled);

        // Held past the timeout; the body comes while the request still waits, then it goes on.
        await Task.Delay(TimeSpan.FromSeconds(3), TestCancelled);
        await stream.WriteAsync(body, TestCancelled);
        server.ReleaseRequest("docs");

        using var response = new MemoryStream();
        await stream.CopyToAsync(response, TestCancelled).WaitAsync(StepTimeout, TestCancelled);
        Assert.StartsWith("HTTP/1.1 200 ", Encoding.Latin1.GetString(response.ToArray()), StringComparison.Ordinal);
        var request = Assert.Single(server.Requests);
        Assert.Equal(("POST", "/docs.git/git-upload-pack", 200), (request.Method, request.Path, request.Status));
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task Disposing_a_folder_server_lets_a_held_request_go_and_ends_its_holds()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var server = await AuthRemoteServer.StartForFolderAsync(Path.Combine(copy.Root, "remotes"));
        try
        {
            server.RequireAuth = false;
            var repo = copy.Repo("docs");
            await repo.GitAsync("remote", "set-url", "origin", server.UrlOf("docs"));
            var arrived = server.HoldNextRequest("mobile");
            var mobile = GitAsync(repo, ["ls-remote", server.UrlOf("mobile")]);
            await arrived.WaitAsync(StepTimeout, TestCancelled);
            var held = server.HoldNextFetchPack("docs");
            var fetch = GitAsync(repo, ["fetch", "--progress", "origin"]);
            await held.WaitAsync(StepTimeout, TestCancelled);

            await server.DisposeAsync().AsTask().WaitAsync(StepTimeout, TestCancelled);

            Assert.False((await fetch.WaitAsync(StepTimeout, TestCancelled)).Succeeded);
            await mobile.WaitAsync(StepTimeout, TestCancelled);
            Assert.True(Directory.Exists(copy.RemoteOf("docs")));
            Assert.Throws<ObjectDisposedException>(() => { _ = server.HoldNextRequest("docs"); });
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// <summary>The commits of <see cref="ProgressRepositoryAsync"/>.</summary>
    private const int ProgressCommits = 298;

    /// <summary>The objects a clone of <see cref="ProgressRepositoryAsync"/> counts: its commits, their one tree and its one file.</summary>
    private const int ProgressObjects = ProgressCommits + 2;

    /// <summary>
    /// A repo whose <c>main</c> the server serves: <see cref="ProgressCommits"/> commits on one
    /// tree, imported in one call (D51). A clone's counting shows every percentage from 0 to 100,
    /// a few lines to a pkt-line as upload-pack happens to read them, so a hold at a percentage
    /// mostly splits one; <see cref="ProgressResponse"/> splits one for certain.
    /// </summary>
    private static async Task<TempRepo> ProgressRepositoryAsync(AuthRemoteServer server)
    {
        var repo = await TempRepo.CreateAsync("progress");
        try
        {
            await ImportAsync(repo, HistoryStream.Line(ProgressCommits));
            await repo.GitAsync("push", "--quiet", server.RepositoryPath, "refs/heads/main:refs/heads/main");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

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

    /// <summary>
    /// A clone's response as upload-pack sends it under protocol v2, for four objects: the
    /// section's name, the progress on band 2 (several lines to a pkt-line, the 50% line over
    /// two), the pack on band 1 and a flush.
    /// </summary>
    private static byte[] ProgressResponse() =>
    [
        .. Pkt("packfile\n"),
        .. Band(2, "Enumerating objects: 4, done.\n"),
        .. Band(2, "Counting objects:   0% (0/4)\rCounting objects:  25% (1/4)\rCounting objects:  5"),
        .. Band(2, "0% (2/4)\rCounting objects:  75% (3/4)\r"),
        .. Band(2, "Counting objects: 100% (4/4)\rCounting objects: 100% (4/4), done.\n"),
        .. Band(1, "PACK...."),
        .. Band(2, "Total 4 (delta 0), reused 0 (delta 0), pack-reused 0\n"),
        .. "0000"u8,
    ];

    private static byte[] Pkt(string payload) => Pkt(Encoding.Latin1.GetBytes(payload));

    private static byte[] Pkt(byte[] payload) =>
        [.. Encoding.ASCII.GetBytes((payload.Length + 4).ToString("x4", CultureInfo.InvariantCulture)), .. payload];

    private static byte[] Band(byte band, string text) => Pkt([band, .. Encoding.Latin1.GetBytes(text)]);

    /// <summary>The pkt-lines of a body that holds nothing else, with their offsets; a flush has no payload.</summary>
    private static List<(int Offset, byte[] Payload)> PktLinesOf(byte[] body)
    {
        var lines = new List<(int Offset, byte[] Payload)>();
        var offset = 0;
        while (offset < body.Length)
        {
            var length = int.Parse(Encoding.ASCII.GetString(body, offset, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            Assert.True(length is 0 or > 4 && offset + length <= body.Length, $"A pkt-line at {offset} has the length {length}.");
            lines.Add((offset, length == 0 ? [] : body[(offset + 4)..(offset + length)]));
            offset += Math.Max(length, 4);
        }

        return lines;
    }

    /// <summary>What the pkt-lines of <paramref name="band"/> carry, joined.</summary>
    private static string BandText(byte[] body, byte band) =>
        string.Concat(PktLinesOf(body).Where(line => line.Payload.Length > 0 && line.Payload[0] == band).Select(line => Encoding.Latin1.GetString(line.Payload, 1, line.Payload.Length - 1)));

    private static string WithCredentials(string url, string user, string password) =>
        url.Replace("http://", $"http://{user}:{password}@", StringComparison.Ordinal);

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string Subject(GitResult result) => result.StandardOutput.Trim();

    /// <summary>Watches git's standard error for the first progress line with a percentage, and keeps every line.</summary>
    private sealed class ProgressWatch
    {
        private readonly TaskCompletionSource<string> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<string> _lines = new();

        public Task<string> FirstPercentage => _first.Task;

        /// <summary>Every line so far, in order.</summary>
        public IReadOnlyList<string> Lines => [.. _lines];

        /// <summary>The last line so far with a percentage; null before the first.</summary>
        public string? LastPercentage => _lines.LastOrDefault(line => line.Contains('%', StringComparison.Ordinal));

        public void Add(string line)
        {
            _lines.Enqueue(line);
            if (line.Contains('%', StringComparison.Ordinal))
            {
                _first.TrySetResult(line);
            }
        }

        /// <summary>Waits until a line holds <paramref name="text"/>; fails after <paramref name="timeout"/>.</summary>
        public async Task WaitForLineAsync(string text, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!_lines.Any(line => line.Contains(text, StringComparison.Ordinal)))
            {
                Assert.True(DateTime.UtcNow < deadline, $"No line of git's holds \"{text}\" after {timeout.TotalSeconds} s:\n{this}");
                await Task.Delay(TimeSpan.FromMilliseconds(20), TestCancelled);
            }
        }

        public override string ToString() => string.Join('\n', _lines);
    }
}
