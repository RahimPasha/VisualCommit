using System.Net;
using System.Text.Json;
using VisualCommit.Core;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// GitHub's API client (docs/phase-7a-design.md 6.2, 6.5; D284, D285): its headers and the token
/// only to the API, redirects by hand on the API's address only, pages, kept ETags, the
/// classification of every failure, an error body's <c>errors</c> in each shape GitHub sends (D342),
/// the 30 s, cancellation, one request at a time, and log lines that hold no query, token or
/// e-mail. Against the fake of GitHub in process, and a script where the fake cannot answer as asked.
/// </summary>
public sealed class GitHubClientTests
{
    /// <summary>
    /// GitHub's answer to <c>X-GitHub-Api-Version: 2000-01-01</c> as the live run of 2026-10-30
    /// recorded it (D342): <c>message</c> "Bad Request", the explanation in <c>errors</c> as one string.
    /// </summary>
    private const string UnsupportedVersionBody = """{"message":"Bad Request","errors":"The version you specified in the \"X-GitHub-API-Version\" request header, \"2000-01-01\", is not a supported version. The following versions are currently supported: \"2026-03-10\" (most recent) and \"2022-11-28\".","documentation_url":"https://docs.github.com/rest","status":"400"}""";

    /// <summary>That answer's explanation, which the failure's message is.</summary>
    private const string UnsupportedVersionExplanation = "The version you specified in the \"X-GitHub-API-Version\" request header, \"2000-01-01\", is not a supported version. The following versions are currently supported: \"2026-03-10\" (most recent) and \"2022-11-28\".";

    /// <summary>An explanation that names the header, sent in <c>errors</c> as an array of strings.</summary>
    private const string UnsupportedVersionHeaderNamed = "Unsupported X-GitHub-API-Version.";

    /// <summary>GitHub's answer to a second create of the same head, as the live run of 2026-10-30 recorded it.</summary>
    private const string DuplicateCreateBody = """{"message":"Validation Failed","errors":[{"resource":"PullRequest","code":"custom","message":"A pull request already exists for RahimPasha:visualcommit-live/20261030-232555Z."}],"documentation_url":"https://docs.github.com/rest/pulls/pulls#create-a-pull-request","status":"422"}""";

    private static readonly Uri Script = new("https://api.github.com/");

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_request_carries_githubs_headers_and_the_token_goes_only_to_the_api()
    {
        await using var setup = Setup.Start();

        var user = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);
        var repository = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("repos/octo/listed-0001"), null, GitHubJsonContext.Default.GitHubRepositoryJson, Cancelled);
        var list = await setup.Client.GetPagesAsync(setup.Octo, setup.Api("user/repos?per_page=100&sort=updated"), GitHubJsonContext.Default.ListGitHubRepositoryJson, 10, Cancelled);

        Assert.Equal(("octo", 1001L), (user.Value!.Login, user.Value.Id));
        Assert.Equal("listed-0001", repository.Value!.Name);
        Assert.Equal(25, list.Value!.Count);
        Assert.Equal("VisualCommit/0.1.0", GitHubClient.UserAgent);
        Assert.Equal(3, setup.Recorder.Sent.Count);
        Assert.All(setup.Recorder.Sent, sent =>
        {
            Assert.Equal(GitHubApi.MediaType, sent.Header("Accept"));
            Assert.Equal(GitHubApi.Version, sent.Header("X-GitHub-Api-Version"));
            Assert.Equal("VisualCommit/0.1.0", sent.Header("User-Agent"));
            Assert.Equal("Bearer " + setup.Octo, sent.Header("Authorization"));
        });

        // Another host, or another port or scheme of the API's host, gets no token.
        foreach (var elsewhere in new[] { "http://127.0.0.2:1/api/v3/user", "http://127.0.0.1:2/api/v3/user", "https://127.0.0.1:1/api/v3/user" })
        {
            var refused = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, new Uri(elsewhere), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);
            Assert.Equal((HostingFailureKind.BadCredentials, "Requires authentication"), (refused.Failure!.Kind, refused.Failure.Message));
            Assert.Null(setup.Recorder.Sent[^1].Header("Authorization"));
        }
    }

    [Fact]
    public async Task A_body_is_json_in_utf_8_and_a_redirected_post_is_repeated_with_its_method_body_and_token()
    {
        var recorder = new RecordingHttpHandler(request => request.RequestUri!.AbsolutePath == "/repos/octo/hello/pulls"
            ? RecordingHttpHandler.Json(HttpStatusCode.TemporaryRedirect, "{}", ("Location", "/repositories/3001/pulls"))
            : RecordingHttpHandler.Json(HttpStatusCode.Created, "{\"login\":\"octo\",\"id\":1001}"));
        using var client = new GitHubClient(GitHubEndpoints.GitHubCom, () => recorder, new ListLog(), new ManualTimeProvider());
        var body = new GitHubNewPullRequestJson { Title = "Add the login form", Body = "Ready.", Head = "feature/login", Base = "main" };

        var reply = await client.SendAsync(HttpMethod.Post, "github_pat_FAKE-octo-1", new Uri(Script, "repos/octo/hello/pulls"), body, GitHubJsonContext.Default.GitHubUserJson, Cancelled);

        Assert.True(reply.Succeeded);
        Assert.Equal(201, reply.Status);
        Assert.Equal(
            new[] { "POST https://api.github.com/repos/octo/hello/pulls", "POST https://api.github.com/repositories/3001/pulls" },
            recorder.Sent.Select(sent => $"{sent.Method} {sent.Url.AbsoluteUri}"));
        Assert.All(recorder.Sent, sent =>
        {
            Assert.Equal("application/json; charset=utf-8", sent.Header("Content-Type"));
            Assert.Equal("{\"title\":\"Add the login form\",\"body\":\"Ready.\",\"head\":\"feature/login\",\"base\":\"main\",\"draft\":false}", sent.Body);
            Assert.Equal("Bearer github_pat_FAKE-octo-1", sent.Header("Authorization"));
        });
    }

    [Fact]
    public async Task A_redirect_to_another_scheme_host_or_port_is_malformed_and_nothing_is_sent_there()
    {
        await using var setup = Setup.Start();
        string[] elsewhere = ["https://elsewhere.example/api/v3/user", "https://127.0.0.1:1/api/v3/user", "http://127.0.0.1:2/api/v3/user"];

        foreach (var target in elsewhere)
        {
            setup.Fake.FailNext("user", FakeGitHubFault.MovedTo(new Uri(target)));
            var get = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);
            Assert.Equal((HostingFailureKind.Malformed, "GitHub's answer could not be read."), (get.Failure!.Kind, get.Failure.Message));
        }

        setup.Fake.FailNext("create:octo/listed-0001", FakeGitHubFault.MovedTo(new Uri("https://elsewhere.example/api/v3/repos/octo/listed-0001/pulls")));
        var body = new GitHubNewPullRequestJson { Title = "A title", Body = "A body", Head = "topic", Base = "main" };
        var post = await setup.Client.SendAsync(HttpMethod.Post, setup.Octo, setup.Api("repos/octo/listed-0001/pulls"), body, GitHubJsonContext.Default.GitHubPullRequestJson, Cancelled);
        Assert.Equal(HostingFailureKind.Malformed, post.Failure!.Kind);

        Assert.All(setup.Recorder.Sent, sent => Assert.Equal("127.0.0.1:1", sent.Url.Authority));
        Assert.Equal(4, setup.Recorder.Sent.Count);
        Assert.Equal(
            new[]
            {
                "GitHub: GET /api/v3/user failed: redirected to https://elsewhere.example:443",
                "GitHub: GET /api/v3/user failed: redirected to https://127.0.0.1:1",
                "GitHub: GET /api/v3/user failed: redirected to http://127.0.0.1:2",
                "GitHub: POST /api/v3/repos/octo/listed-0001/pulls failed: redirected to https://elsewhere.example:443",
            },
            setup.Log.Lines.Select(line => line.Message[..line.Message.IndexOf(" (request ", StringComparison.Ordinal)]));
        Assert.All(setup.Log.Lines, line => Assert.Equal(LogLevel.Warning, line.Level));
    }

    [Fact]
    public async Task A_moved_repository_is_followed_with_the_token_on_the_api()
    {
        await using var setup = Setup.Start();
        setup.Fake.FailNext("repo:octo/listed-0001", FakeGitHubFault.Moved);

        var reply = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("repos/octo/listed-0001"), null, GitHubJsonContext.Default.GitHubRepositoryJson, Cancelled);

        Assert.Equal(("listed-0001", "octo", 200), (reply.Value!.Name, reply.Value.Owner.Login, reply.Status));
        Assert.Equal(
            new[] { "/api/v3/repos/octo/listed-0001 301", "/api/v3/repositories/3001 200" },
            setup.Fake.Requests.Select(request => $"{request.Path} {request.Status}"));
        Assert.All(setup.Fake.Requests, request => Assert.True(request.TokenPresent));
        Assert.Empty(setup.Log.Lines);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task Three_redirects_are_followed_and_a_fourth_is_malformed(int redirects, bool succeeds)
    {
        var recorder = new RecordingHttpHandler(request =>
        {
            var hop = int.Parse(request.RequestUri!.Segments[^1], System.Globalization.CultureInfo.InvariantCulture);
            return hop < redirects
                ? RecordingHttpHandler.Json(HttpStatusCode.MovedPermanently, "{}", ("Location", FormattableString.Invariant($"/hop/{hop + 1}")))
                : RecordingHttpHandler.Json(HttpStatusCode.OK, "{\"login\":\"octo\",\"id\":1001}");
        });
        var log = new ListLog();
        using var client = new GitHubClient(GitHubEndpoints.GitHubCom, () => recorder, log, new ManualTimeProvider());

        var reply = await client.SendAsync(HttpMethod.Get, "github_pat_FAKE-octo-1", new Uri(Script, "hop/0"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);

        Assert.Equal(succeeds, reply.Succeeded);
        Assert.Equal(4, recorder.Sent.Count);
        if (!succeeds)
        {
            Assert.Equal(HostingFailureKind.Malformed, reply.Failure!.Kind);
            Assert.Equal("GitHub: GET /hop/3 failed: 301, a fourth redirect.", Assert.Single(log.Lines).Message);
        }
    }

    [Fact]
    public async Task A_redirect_without_a_location_is_malformed()
    {
        var recorder = new RecordingHttpHandler(_ => RecordingHttpHandler.Json(HttpStatusCode.Found, "{}"));
        var log = new ListLog();
        using var client = new GitHubClient(GitHubEndpoints.GitHubCom, () => recorder, log, new ManualTimeProvider());

        var reply = await client.SendAsync(HttpMethod.Get, "github_pat_FAKE-octo-1", new Uri(Script, "user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);

        Assert.Equal(HostingFailureKind.Malformed, reply.Failure!.Kind);
        Assert.Equal("GitHub: GET /user failed: 302 without Location.", Assert.Single(log.Lines).Message);
    }

    [Theory]
    [InlineData(10, 20, true)]
    [InlineData(3, 6, true)]
    [InlineData(13, 25, false)]
    public async Task Pages_follow_next_up_to_the_cap(int maxPages, int count, bool moreExist)
    {
        await using var setup = Setup.Start();
        setup.Fake.MaxPerPage = 2;

        var reply = await setup.Client.GetPagesAsync(setup.Octo, setup.Api("user/repos?per_page=100&sort=updated"), GitHubJsonContext.Default.ListGitHubRepositoryJson, maxPages, Cancelled);

        Assert.True(reply.Succeeded);
        Assert.Equal((count, moreExist), (reply.Value!.Count, reply.MoreExist));
        Assert.Equal(Enumerable.Range(1, count).Select(n => FormattableString.Invariant($"listed-{n:D4}")), reply.Value.Select(repository => repository.Name));
        Assert.Equal(Math.Min(maxPages, 13), setup.Fake.Requests.Count);
        Assert.Equal("per_page=100&sort=updated&page=2", setup.Fake.Requests[1].Query);
    }

    [Theory]
    [InlineData("http://elsewhere.example/api/v3/user/repos?page=2", "http://elsewhere.example:80")]
    [InlineData("https://api.github.com:8443/user/repos?page=2", "https://api.github.com:8443")]
    public async Task A_next_page_on_another_host_is_malformed_and_not_asked_for(string next, string origin)
    {
        var recorder = new RecordingHttpHandler(_ => RecordingHttpHandler.Json(HttpStatusCode.OK, "[{\"name\":\"hello\",\"owner\":{\"login\":\"octo\"}}]", ("Link", $"<{next}>; rel=\"next\", <{next}>; rel=\"last\"")));
        var log = new ListLog();
        using var client = new GitHubClient(GitHubEndpoints.GitHubCom, () => recorder, log, new ManualTimeProvider());

        var reply = await client.GetPagesAsync("github_pat_FAKE-octo-1", new Uri(Script, "user/repos?per_page=100"), GitHubJsonContext.Default.ListGitHubRepositoryJson, 10, Cancelled);

        Assert.Equal(HostingFailureKind.Malformed, reply.Failure!.Kind);
        Assert.Single(recorder.Sent);
        Assert.Equal($"GitHub: GET /user/repos failed: Link to {origin}.", Assert.Single(log.Lines).Message);
    }

    [Fact]
    public async Task A_next_page_outside_the_apis_path_is_malformed()
    {
        await using var setup = Setup.Start();
        var recorder = new RecordingHttpHandler(_ => RecordingHttpHandler.Json(HttpStatusCode.OK, "[]", ("Link", "<http://127.0.0.1:1/other/user/repos?page=2>; rel=\"next\"")));
        using var client = new GitHubClient(setup.Fake.Endpoints, () => recorder, setup.Log, new ManualTimeProvider());

        var reply = await client.GetPagesAsync(setup.Octo, setup.Api("user/repos"), GitHubJsonContext.Default.ListGitHubRepositoryJson, 10, Cancelled);

        Assert.Equal(HostingFailureKind.Malformed, reply.Failure!.Kind);
        Assert.Single(recorder.Sent);
    }

    [Fact]
    public async Task A_second_identical_call_is_conditional_and_gets_the_kept_value_and_another_tokens_is_not()
    {
        await using var setup = Setup.Start();
        var url = setup.Api("repos/octo/listed-0002");

        var first = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, url, null, GitHubJsonContext.Default.GitHubRepositoryJson, Cancelled);
        var second = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, url, null, GitHubJsonContext.Default.GitHubRepositoryJson, Cancelled);
        var others = await setup.Client.SendAsync(HttpMethod.Get, setup.Ada, url, null, GitHubJsonContext.Default.GitHubRepositoryJson, Cancelled);

        Assert.Equal((200, 304, 200), (first.Status, second.Status, others.Status));
        Assert.Equal((first.Value!.Name, first.Value.UpdatedAt), (second.Value!.Name, second.Value.UpdatedAt));
        Assert.Equal(new[] { false, true, false }, setup.Fake.Requests.Select(request => request.Conditional));
        Assert.Equal(new[] { 200, 304, 200 }, setup.Fake.Requests.Select(request => request.Status));
        Assert.Equal(new[] { false, true, false }, setup.Recorder.Sent.Select(sent => sent.Header("If-None-Match") is not null));
    }

    [Fact]
    public async Task A_repeated_list_is_conditional_on_every_page_and_keeps_its_pages_more_and_single_sign_on()
    {
        await using var setup = Setup.Start();
        setup.Fake.MaxPerPage = 2;
        setup.Fake.FailNext("user/repos", FakeGitHubFault.SsoPartialResults);
        var url = setup.Api("user/repos?per_page=100&sort=updated");

        var first = await setup.Client.GetPagesAsync(setup.Octo, url, GitHubJsonContext.Default.ListGitHubRepositoryJson, 10, Cancelled);
        var second = await setup.Client.GetPagesAsync(setup.Octo, url, GitHubJsonContext.Default.ListGitHubRepositoryJson, 10, Cancelled);

        // The fake's 304 carries neither Link nor X-GitHub-SSO: the kept ones page on and say so.
        Assert.Equal((20, true, 200), (first.Value!.Count, first.MoreExist, first.Status));
        Assert.Equal(first.Value.Select(repository => repository.Name), second.Value!.Select(repository => repository.Name));
        Assert.Equal((true, 304, first.OAuthScopes), (second.MoreExist, second.Status, second.OAuthScopes));
        Assert.StartsWith("partial-results", first.Sso, StringComparison.Ordinal);
        Assert.Equal(first.Sso, second.Sso);
        var requests = setup.Fake.Requests;
        Assert.Equal(20, requests.Count);
        Assert.All(requests.Take(10), request => Assert.Equal((false, 200), (request.Conditional, request.Status)));
        Assert.All(requests.Skip(10), request => Assert.Equal((true, 304), (request.Conditional, request.Status)));
        Assert.Equal(requests.Take(10).Select(request => request.Query), requests.Skip(10).Select(request => request.Query));
        Assert.Empty(setup.Log.Lines);
    }

    [Fact]
    public async Task Kept_answers_are_bounded_by_their_bytes_and_the_oldest_stored_goes_first()
    {
        // Bodies of 1,000 bytes (d's 3,000) under a budget of 2,500: two are kept. The script
        // answers 304 to a URL's current ETag, which a new version changes.
        var versions = new Dictionary<string, int>(StringComparer.Ordinal) { ["/a"] = 1, ["/b"] = 1, ["/c"] = 1, ["/d"] = 1 };
        var recorder = new RecordingHttpHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var etag = $"\"{path[1..]}{versions[path]}\"";
            if (request.Headers.TryGetValues("If-None-Match", out var given) && given.Contains(etag))
            {
                return RecordingHttpHandler.Json(HttpStatusCode.NotModified, string.Empty, ("ETag", etag));
            }

            const string Start = "{\"login\":\"octo\",\"id\":1001,\"pad\":\"";
            var size = path == "/d" ? 3000 : 1000;
            return RecordingHttpHandler.Json(HttpStatusCode.OK, Start + new string('x', size - Start.Length - 2) + "\"}", ("ETag", etag));
        });
        using var client = new GitHubClient(GitHubEndpoints.GitHubCom, () => recorder, new ListLog(), new ManualTimeProvider(), keptBytes: 2500);
        async Task<int> GetAsync(string path) =>
            (await client.SendAsync(HttpMethod.Get, "github_pat_FAKE-octo-1", new Uri(Script, path), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled)).Status;

        // a and b kept; a's new version makes it the newest; c then drops b, the oldest stored.
        Assert.Equal(200, await GetAsync("a"));
        Assert.Equal(200, await GetAsync("b"));
        versions["/a"] = 2;
        Assert.Equal(200, await GetAsync("a"));
        Assert.Equal(200, await GetAsync("c"));
        Assert.Equal(304, await GetAsync("a"));
        Assert.Equal(200, await GetAsync("b"));

        // A body over the whole budget is not kept, and drops nothing.
        Assert.Equal(200, await GetAsync("d"));
        Assert.Equal(200, await GetAsync("d"));
        Assert.Equal(304, await GetAsync("b"));

        Assert.Equal(
            new[] { "/a -", "/b -", "/a \"a1\"", "/c -", "/a \"a2\"", "/b -", "/d -", "/d -", "/b \"b1\"" },
            recorder.Sent.Select(sent => $"{sent.Url.AbsolutePath} {sent.Header("If-None-Match") ?? "-"}"));
    }

    [Fact]
    public async Task Each_fault_is_classified_with_its_message_detail_retry_time_and_request()
    {
        await using var setup = Setup.Start();
        var now = AppClocks.HarnessTime;
        (FakeGitHubFault Fault, HostingFailureKind Kind, string Message, string? Detail, DateTimeOffset? RetryAt)[] cases =
        [
            (FakeGitHubFault.BadCredentials, HostingFailureKind.BadCredentials, "Bad credentials", null, null),
            (FakeGitHubFault.PrimaryRateLimit, HostingFailureKind.RateLimited, "API rate limit exceeded for user ID 1001.", null, now.AddHours(1)),
            (FakeGitHubFault.SecondaryRateLimit, HostingFailureKind.RateLimited, "You have exceeded a secondary rate limit. Please wait a few minutes before you try again.", null, now.AddSeconds(60)),
            (FakeGitHubFault.SsoRequired, HostingFailureKind.SsoRequired, "Resource protected by organization SAML enforcement.", "http://127.0.0.1:1/orgs/acme/sso?authorization_request=FAKE", null),
            (FakeGitHubFault.Forbidden, HostingFailureKind.Forbidden, "Resource not accessible by personal access token", "metadata=read", null),
            (FakeGitHubFault.NotFound, HostingFailureKind.NotFound, "Not Found", null, null),
            (FakeGitHubFault.ApiVersionGone, HostingFailureKind.ApiVersion, "API version 2026-03-10 is no longer supported.", null, null),
            (FakeGitHubFault.ServerError, HostingFailureKind.ServerError, "Server Error", null, null),
            (FakeGitHubFault.BadGateway, HostingFailureKind.ServerError, "GitHub had a problem: HTTP 502.", null, null),
            (FakeGitHubFault.Malformed, HostingFailureKind.Malformed, "GitHub's answer could not be read.", null, null),
            (FakeGitHubFault.CreatedThenDropped, HostingFailureKind.Network, "Could not reach GitHub.", null, null),
        ];

        foreach (var (fault, kind, message, detail, retryAt) in cases)
        {
            setup.Fake.FailNext("user", fault);
            var reply = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("user?x=1"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);

            var failure = reply.Failure;
            Assert.NotNull(failure);
            Assert.Equal((kind, message, detail, retryAt), (failure.Kind, failure.Message, failure.Detail, failure.RetryAt));
            Assert.Equal("http://127.0.0.1:1/api/v3/user", failure.Url!.AbsoluteUri);
            if (kind == HostingFailureKind.Network)
            {
                Assert.Null(failure.RequestId);
            }
            else
            {
                Assert.Matches("^FAKE-[0-9]{4}$", failure.RequestId);
            }

            Assert.True((await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled)).Succeeded, $"user after {fault}");
        }

        Assert.Equal(cases.Length, setup.Log.Lines.Count);
        Assert.Equal("GitHub: GET /api/v3/user failed: Network.", setup.Log.Lines[^1].Message);
    }

    [Theory]
    [InlineData(429, "30", "{\"message\":\"Too many\"}", HostingFailureKind.RateLimited, 30, "Too many")]
    [InlineData(429, null, "{\"message\":\"Too many\"}", HostingFailureKind.Malformed, null, GitHubClient.MalformedText)]
    [InlineData(403, "120", "{\"message\":\"Slow down\"}", HostingFailureKind.RateLimited, 120, "Slow down")]
    [InlineData(403, null, "{\"message\":\"You have exceeded a Secondary Rate Limit.\"}", HostingFailureKind.RateLimited, 60, "You have exceeded a Secondary Rate Limit.")]
    [InlineData(400, null, "{\"message\":\"Unsupported 'X-GitHub-Api-Version' header: x.\"}", HostingFailureKind.ApiVersion, null, "Unsupported 'X-GitHub-Api-Version' header: x.")]
    [InlineData(400, null, UnsupportedVersionBody, HostingFailureKind.ApiVersion, null, UnsupportedVersionExplanation)]
    [InlineData(400, null, "{\"message\":\"Bad Request\",\"errors\":[\"" + UnsupportedVersionHeaderNamed + "\"]}", HostingFailureKind.ApiVersion, null, UnsupportedVersionHeaderNamed)]
    [InlineData(400, null, "{\"message\":\"Bad Request\",\"errors\":\"Something else\"}", HostingFailureKind.Malformed, null, GitHubClient.MalformedText)]
    [InlineData(400, null, "{\"message\":\"Bad Request\",\"errors\":42}", HostingFailureKind.Malformed, null, GitHubClient.MalformedText)]
    [InlineData(400, null, "{\"message\":\"Problems parsing JSON\"}", HostingFailureKind.Malformed, null, GitHubClient.MalformedText)]
    [InlineData(403, null, "{\"message\":\"Resource not accessible by personal access token\",\"errors\":\"x\"}", HostingFailureKind.Forbidden, null, "Resource not accessible by personal access token")]
    [InlineData(403, null, "{\"message\":\"Resource not accessible by personal access token\",\"errors\":42}", HostingFailureKind.Forbidden, null, "Resource not accessible by personal access token")]
    [InlineData(404, null, "{\"message\":\"Not Found\",\"errors\":{\"field\":\"sha\"},\"documentation_url\":\"https://docs.github.com/rest/commits/commits#list-commits\",\"status\":\"404\"}", HostingFailureKind.NotFound, null, "Not Found")]
    [InlineData(415, null, "{\"message\":\"Unsupported 'Accept' header.\"}", HostingFailureKind.Malformed, null, GitHubClient.MalformedText)]
    [InlineData(418, null, "{}", HostingFailureKind.Malformed, null, GitHubClient.MalformedText)]
    [InlineData(500, null, "", HostingFailureKind.ServerError, null, "GitHub had a problem: HTTP 500.")]
    [InlineData(503, null, "<html>busy</html>", HostingFailureKind.ServerError, null, "GitHub had a problem: HTTP 503.")]
    [InlineData(401, null, "<html>no</html>", HostingFailureKind.BadCredentials, null, "Unauthorized")]
    public async Task Other_answers_are_classified_by_the_table(int status, string? retryAfter, string body, HostingFailureKind kind, int? retrySeconds, string message)
    {
        var clock = new ManualTimeProvider(AppClocks.HarnessTime);
        var recorder = new RecordingHttpHandler(_ => retryAfter is null
            ? RecordingHttpHandler.Json((HttpStatusCode)status, body)
            : RecordingHttpHandler.Json((HttpStatusCode)status, body, ("Retry-After", retryAfter)));
        using var client = new GitHubClient(GitHubEndpoints.GitHubCom, () => recorder, new ListLog(), clock);

        var reply = await client.SendAsync(HttpMethod.Get, "github_pat_FAKE-octo-1", new Uri(Script, "user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);

        Assert.Equal((kind, message), (reply.Failure!.Kind, reply.Failure.Message));
        Assert.Equal(retrySeconds is null ? null : AppClocks.HarnessTime.AddSeconds(retrySeconds.Value), reply.Failure.RetryAt);
    }

    [Theory]
    [InlineData("{\"resource\":\"PullRequest\",\"field\":\"title\",\"code\":\"missing_field\"}", "title is missing")]
    [InlineData("{\"resource\":\"PullRequest\",\"field\":\"title\",\"code\":\"missing\"}", "title is missing")]
    [InlineData("{\"resource\":\"PullRequest\",\"field\":\"base\",\"code\":\"invalid\"}", "base is invalid")]
    [InlineData("{\"resource\":\"PullRequest\",\"field\":\"head\",\"code\":\"already_exists\"}", "head already exists")]
    [InlineData("{\"resource\":\"PullRequest\",\"code\":\"custom\",\"message\":\"No commits between main and topic\"}", "No commits between main and topic")]
    [InlineData("{\"resource\":\"PullRequest\",\"field\":\"head\",\"code\":\"unprocessable\"}", "Validation Failed")]
    [InlineData(null, "Validation Failed")]
    public async Task A_422s_detail_is_the_first_errors_message_or_its_field_and_code(string? error, string detail)
    {
        var body = error is null ? "{\"message\":\"Validation Failed\"}" : $"{{\"message\":\"Validation Failed\",\"errors\":[{error}]}}";
        Assert.Equal((HostingFailureKind.Validation, "Validation Failed", detail), await RefusedCreateAsync(body));
    }

    [Theory]
    [InlineData(DuplicateCreateBody, "A pull request already exists for RahimPasha:visualcommit-live/20261030-232555Z.")]
    [InlineData("{\"message\":\"Validation Failed\",\"errors\":[\"head sha can't be blank\"]}", "head sha can't be blank")]
    [InlineData("{\"message\":\"Validation Failed\",\"errors\":\"head sha can't be blank\"}", "head sha can't be blank")]
    [InlineData("{\"message\":\"Validation Failed\",\"errors\":[{\"resource\":\"PullRequest\",\"field\":\"head\",\"code\":\"invalid\",\"message\":7}]}", "head is invalid")]
    [InlineData("{\"message\":\"Validation Failed\",\"errors\":42}", "Validation Failed")]
    public async Task A_422s_errors_are_read_as_an_array_of_objects_or_of_strings_or_one_string(string body, string detail)
    {
        Assert.Equal((HostingFailureKind.Validation, "Validation Failed", detail), await RefusedCreateAsync(body));
    }

    [Theory]
    [InlineData(UnsupportedVersionBody, "Bad Request", "-|-|" + UnsupportedVersionExplanation)]
    [InlineData(DuplicateCreateBody, "Validation Failed", "-|custom|A pull request already exists for RahimPasha:visualcommit-live/20261030-232555Z.")]
    [InlineData("{\"message\":\"m\",\"errors\":[{\"field\":\"head\",\"code\":\"invalid\",\"value\":{\"a\":[1,{\"message\":\"no\"}]},\"message\":null},\"one\",{\"field\":3,\"code\":[\"x\"],\"message\":\"two\"},7,[\"three\"],null]}", "m", "head|invalid|- / -|-|one / -|-|two")]
    [InlineData("{\"errors\":\"e\",\"message\":\"m\"}", "m", "-|-|e")]
    [InlineData("{\"errors\":[],\"message\":\"m\"}", "m", "")]
    [InlineData("{\"message\":\"m\",\"errors\":{\"message\":\"no\"},\"status\":\"422\"}", "m", null)]
    [InlineData("{\"message\":\"m\",\"errors\":true}", "m", null)]
    [InlineData("{\"message\":\"m\",\"errors\":null}", "m", null)]
    [InlineData("{\"message\":\"m\"}", "m", null)]
    public void GitHubs_errors_are_read_in_every_shape_through_its_json_context(string body, string message, string? items)
    {
        var error = JsonSerializer.Deserialize(body, GitHubJsonContext.Default.GitHubErrorJson);

        Assert.Equal(message, error!.Message);
        Assert.Equal(items, error.Errors is null ? null : string.Join(" / ", error.Errors.Select(item => $"{item.Field ?? "-"}|{item.Code ?? "-"}|{item.Message ?? "-"}")));
    }

    [Fact]
    public void GitHubs_errors_are_never_written()
    {
        var error = new GitHubErrorJson { Message = "m", Errors = [new GitHubErrorItemJson { Message = "e" }] };

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(error, GitHubJsonContext.Default.GitHubErrorJson));
    }

    [Fact]
    public async Task A_missing_version_header_is_an_api_version_failure()
    {
        await using var setup = Setup.Start(edit: request => request.Headers.Remove("X-GitHub-Api-Version"));

        var reply = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);

        Assert.Equal(
            (HostingFailureKind.ApiVersion, "The fake of GitHub accepts only \"2026-03-10\" in the \"X-GitHub-API-Version\" request header (D266); the request sent none."),
            (reply.Failure!.Kind, reply.Failure.Message));
    }

    [Fact]
    public async Task A_version_github_does_not_support_is_an_api_version_failure_in_githubs_words()
    {
        await using var setup = Setup.Start(edit: request =>
        {
            request.Headers.Remove("X-GitHub-Api-Version");
            request.Headers.Add("X-GitHub-Api-Version", "2000-01-01");
        });

        var reply = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);

        Assert.Equal((HostingFailureKind.ApiVersion, UnsupportedVersionExplanation), (reply.Failure!.Kind, reply.Failure.Message));
        Assert.Matches("^FAKE-[0-9]{4}$", reply.Failure.RequestId);
        Assert.Matches(@"^GitHub: GET /api/v3/user failed: 400 \(request FAKE-[0-9]{4}\)\.$", Assert.Single(setup.Log.Lines).Message);
    }

    [Fact]
    public async Task No_answer_is_a_network_failure_with_the_exception_in_the_log_only()
    {
        var recorder = new RecordingHttpHandler(_ => throw new HttpRequestException("No such host is known. (api.github.com:443)"));
        var log = new ListLog();
        using var client = new GitHubClient(GitHubEndpoints.GitHubCom, () => recorder, log, new ManualTimeProvider());

        var reply = await client.SendAsync(HttpMethod.Get, "github_pat_FAKE-octo-1", new Uri(Script, "user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);

        Assert.Equal((HostingFailureKind.Network, "Could not reach GitHub.", 0), (reply.Failure!.Kind, reply.Failure.Message, reply.Status));
        Assert.Equal("GitHub: GET /user failed: Network.", Assert.Single(log.Lines).Message);
    }

    [Fact]
    public async Task A_cancellation_neither_the_callers_nor_the_time_limits_is_no_answer_with_its_exception_in_the_log()
    {
        // As the handler's 10 s connect time-out throws it: a TaskCanceledException around a TimeoutException.
        var recorder = new RecordingHttpHandler(_ => throw new TaskCanceledException("The operation was canceled.", new TimeoutException("A connection could not be made within 10 seconds.")));
        var log = new ExceptionLog();
        using var client = new GitHubClient(GitHubEndpoints.GitHubCom, () => recorder, log, new ManualTimeProvider());

        var reply = await client.SendAsync(HttpMethod.Get, "github_pat_FAKE-octo-1", new Uri(Script, "user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);

        Assert.Equal((HostingFailureKind.Network, "Could not reach GitHub.", 0), (reply.Failure!.Kind, reply.Failure.Message, reply.Status));
        var (message, exception) = Assert.Single(log.Lines);
        Assert.Equal("GitHub: GET /user failed: Network.", message);
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Fact]
    public async Task An_answer_over_10_mb_is_malformed()
    {
        var recorder = new RecordingHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[GitHubClient.MaxBodyBytes + 1]) });
        var log = new ListLog();
        using var client = new GitHubClient(GitHubEndpoints.GitHubCom, () => recorder, log, new ManualTimeProvider());

        var reply = await client.SendAsync(HttpMethod.Get, "github_pat_FAKE-octo-1", new Uri(Script, "user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);

        Assert.Equal(HostingFailureKind.Malformed, reply.Failure!.Kind);
        Assert.Equal("GitHub: GET /user failed: an answer over 10 MB.", Assert.Single(log.Lines).Message);
    }

    [Fact]
    public async Task A_409_is_passed_as_its_status_without_a_failure()
    {
        await using var setup = Setup.Start();

        var reply = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("repos/octo/listed-0001/commits?per_page=100"), null, GitHubJsonContext.Default.ListGitHubCommitItemJson, Cancelled);

        Assert.Equal((true, 409), (reply.Succeeded, reply.Status));
        Assert.Null(reply.Value);
        Assert.Empty(setup.Log.Lines);
    }

    [Fact]
    public async Task A_held_request_runs_out_of_its_time_with_the_fixed_text()
    {
        await using var setup = Setup.Start(timeout: TimeSpan.FromSeconds(1));
        using var hold = setup.Fake.HoldNext("user");

        var reply = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled).WaitAsync(TimeSpan.FromSeconds(60), Cancelled);

        Assert.Equal((HostingFailureKind.Network, "GitHub did not answer within 30 seconds."), (reply.Failure!.Kind, reply.Failure.Message));
        Assert.Equal("GitHub: GET /api/v3/user failed: time-out.", Assert.Single(setup.Log.Lines).Message);
    }

    [Fact]
    public async Task Cancelling_a_held_call_throws()
    {
        await using var setup = Setup.Start();
        using var hold = setup.Fake.HoldNext("user");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancelled);

        var call = setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("user"), null, GitHubJsonContext.Default.GitHubUserJson, cancel.Token);
        await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(TimeSpan.FromSeconds(60), Cancelled));
        Assert.Empty(setup.Log.Lines);
    }

    [Fact]
    public async Task A_second_call_waits_while_the_first_is_held_and_its_own_cancellation_ends_the_wait()
    {
        await using var setup = Setup.Start();
        var hold = setup.Fake.HoldNext("user");

        var first = setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("user"), null, GitHubJsonContext.Default.GitHubUserJson, Cancelled);
        await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        var second = setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("repos/octo/listed-0001"), null, GitHubJsonContext.Default.GitHubRepositoryJson, Cancelled);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancelled);
        var third = setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("repos/octo/listed-0002"), null, GitHubJsonContext.Default.GitHubRepositoryJson, cancel.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(300), Cancelled);

        Assert.False(second.IsCompleted);
        Assert.Single(setup.Recorder.Sent);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => third.WaitAsync(TimeSpan.FromSeconds(60), Cancelled));
        Assert.Single(setup.Recorder.Sent);

        hold.Dispose();
        Assert.True((await first.WaitAsync(TimeSpan.FromSeconds(60), Cancelled)).Succeeded);
        Assert.True((await second.WaitAsync(TimeSpan.FromSeconds(60), Cancelled)).Succeeded);
        Assert.Equal(new[] { "/api/v3/user", "/api/v3/repos/octo/listed-0001" }, setup.Recorder.Sent.Select(sent => sent.Url.AbsolutePath));
    }

    [Fact]
    public async Task A_log_line_holds_no_query_token_or_e_mail()
    {
        await using var setup = Setup.Start();
        setup.Fake.FailNext("commits:octo/listed-0001", FakeGitHubFault.NotFound);

        var reply = await setup.Client.SendAsync(HttpMethod.Get, setup.Octo, setup.Api("repos/octo/listed-0001/commits?author=ada%40example.com&per_page=1"), null, GitHubJsonContext.Default.ListGitHubCommitItemJson, Cancelled);

        Assert.Equal(HostingFailureKind.NotFound, reply.Failure!.Kind);
        Assert.Equal("http://127.0.0.1:1/api/v3/repos/octo/listed-0001/commits", reply.Failure.Url!.AbsoluteUri);
        var line = Assert.Single(setup.Log.Lines);
        Assert.Matches(@"^GitHub: GET /api/v3/repos/octo/listed-0001/commits failed: 404 \(request FAKE-[0-9]{4}\)\.$", line.Message);
        foreach (var secret in new[] { setup.Octo, "ada", "example.com", "%40", "?", "author", "per_page" })
        {
            Assert.DoesNotContain(secret, line.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>A create GitHub refuses with 422 and <paramref name="body"/>: the failure's kind, message and detail.</summary>
    private static async Task<(HostingFailureKind Kind, string Message, string? Detail)> RefusedCreateAsync(string body)
    {
        var recorder = new RecordingHttpHandler(_ => RecordingHttpHandler.Json(HttpStatusCode.UnprocessableContent, body));
        using var client = new GitHubClient(GitHubEndpoints.GitHubCom, () => recorder, new ListLog(), new ManualTimeProvider());

        var reply = await client.SendAsync(HttpMethod.Post, "github_pat_FAKE-octo-1", new Uri(Script, "repos/octo/hello/pulls"), new GitHubNewPullRequestJson { Title = "t", Body = "b", Head = "h", Base = "main" }, GitHubJsonContext.Default.GitHubPullRequestJson, Cancelled);

        return (reply.Failure!.Kind, reply.Failure.Message, reply.Failure.Detail);
    }

    /// <summary>A log that keeps each line's message with its exception.</summary>
    private sealed class ExceptionLog : IAppLog
    {
        private readonly List<(string Message, Exception? Exception)> _lines = [];

        public IReadOnlyList<(string Message, Exception? Exception)> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        public void Write(LogLevel level, string message, Exception? exception = null)
        {
            lock (_lines)
            {
                _lines.Add((message, exception));
            }
        }
    }

    /// <summary>The fake in process with octo (1001) and ada (1003), a token each, and 25 listed repositories of octo; a client over a recording handler.</summary>
    private sealed class Setup : IAsyncDisposable
    {
        private readonly TempDirectory _folder;

        private Setup(TempDirectory folder, FakeGitHub fake, RecordingHttpHandler recorder, ListLog log, GitHubClient client, string octo, string ada)
        {
            _folder = folder;
            Fake = fake;
            Recorder = recorder;
            Log = log;
            Client = client;
            Octo = octo;
            Ada = ada;
        }

        public FakeGitHub Fake { get; }

        public RecordingHttpHandler Recorder { get; }

        public ListLog Log { get; }

        public GitHubClient Client { get; }

        public string Octo { get; }

        public string Ada { get; }

        public static Setup Start(TimeSpan? timeout = null, Action<HttpRequestMessage>? edit = null)
        {
            var folder = new TempDirectory("github-client");
            var fake = FakeGitHub.InProcess(folder.Combine("github"));
            fake.AddUser("octo", 1001, "Octo Cat");
            fake.AddUser("ada", 1003, "Ada Lovelace");
            var octo = fake.AddToken("octo");
            var ada = fake.AddToken("ada");
            fake.AddListedRepositories("octo", 25);
            var recorder = new RecordingHttpHandler(fake.CreateHandler(), edit);
            var log = new ListLog();
            var client = new GitHubClient(fake.Endpoints, () => recorder, log, new ManualTimeProvider(AppClocks.HarnessTime), timeout);
            return new Setup(folder, fake, recorder, log, client, octo, ada);
        }

        public Uri Api(string relative) => new(Fake.Endpoints.ApiBase, relative);

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            Assert.Empty(Fake.Errors);
            await Fake.DisposeAsync();
            _folder.Dispose();
        }
    }
}
