using System.Globalization;
using System.Net;
using System.Text;
using VisualCommit.Core;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.GitLab;
using VisualCommit.Hosting.Http;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// GitLab's profile on the shared HTTP core (docs/phase-7b-design.md 7.3, 7.6), against the fake in
/// process and scripted answers: <c>Bearer</c> alone and GitLab's headers on every hop; <c>%2F</c> to
/// the request line; a moved project's GET followed with the token and a POST there refused; another
/// origin, a fourth hop and a <c>Link</c> elsewhere refused with nothing sent there; pages; kept
/// answers per token; 7.3's kinds and words, from the status and shape F's code alone; a refusal's
/// reason on the create's POST alone; the four sources of a rate limit's end; warnings that name
/// GitLab and no query, token or address.
/// </summary>
public sealed class GitLabClientTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Bearer_alone_and_GitLabs_headers_go_on_every_hop_of_a_moved_projects_GET_and_its_POST_is_refused()
    {
        await using var lab = await GitLabLab.CreateAsync();
        using var client = lab.Client();
        lab.Fake.Move("g/s/p", "g/s/q");

        var moved = await client.SendAsync(HttpMethod.Get, lab.Ada, Url(lab, "projects/g%2Fs%2Fp"), null, GitLabJsonContext.Default.GitLabProjectJson, Cancelled);

        Assert.True(moved.Succeeded);
        Assert.Equal(("g/s/q", 5001L), (moved.Value!.PathWithNamespace, moved.Value.Id));
        Assert.Equal(
            [("/api/v4/projects/g%2Fs%2Fp", 301), ("/api/v4/projects/5001", 200)],
            lab.Fake.Requests.Select(request => (request.Path, request.Status)));
        Assert.All(lab.Fake.Requests, request => Assert.Equal((FakeAuthorizationForm.Bearer, "ada"), (request.Authorization, request.Login)));
        Assert.All(lab.Recorder!.Sent, sent =>
        {
            Assert.Equal("Bearer " + lab.Ada, sent.Header("Authorization"));
            Assert.Equal("application/json", sent.Header("Accept"));
            Assert.Equal(HostingHttpClient.UserAgent, sent.Header("User-Agent"));
            Assert.Null(sent.Header("PRIVATE-TOKEN"));
            Assert.Null(sent.Header("X-GitHub-Api-Version"));
        });

        var body = new GitLabNewMergeRequestJson { SourceBranch = "feature/one", TargetBranch = "main", Title = "T", Description = "" };
        var post = await client.SendAsync(HttpMethod.Post, lab.Ada, Url(lab, "projects/g%2Fs%2Fp/merge_requests"), body, GitLabJsonContext.Default.GitLabMergeRequestJson, Cancelled);

        Assert.Equal((HostingFailureKind.Validation, "Non GET methods are not allowed for moved projects", 405, PullRequestRefusal.None),
            (post.Failure!.Kind, post.Failure.Message, post.Status, post.Failure.Refusal));
        Assert.Equal(3, lab.Fake.Requests.Count);
        Assert.Equal("application/json; charset=utf-8", lab.Recorder.Sent[^1].Header("Content-Type"));
        Assert.Equal("{\"source_branch\":\"feature/one\",\"target_branch\":\"main\",\"title\":\"T\",\"description\":\"\"}", lab.Recorder.Sent[^1].Body);
        lab.AssertNoSecret();
    }

    [Fact]
    public async Task A_projects_id_is_its_path_escaped_whole_and_its_slashes_reach_the_request_line_escaped()
    {
        await using var lab = await GitLabLab.CreateAsync();
        using var client = lab.Client();

        Assert.Equal("g%2Fs%2Fp", GitLabProfile.ProjectId("g/s/p"));
        Assert.Equal("northfield%2Fplatform%2Fproj.name_1-x", GitLabProfile.ProjectId("northfield/platform/proj.name_1-x"));
        var profile = new GitLabProfile(lab.Fake.Endpoints, lab.Clock);
        var url = profile.Url("projects/" + GitLabProfile.ProjectId("g/s/p") + "/merge_requests?state=opened");
        Assert.Equal("http://127.0.0.1:3/api/v4/projects/g%2Fs%2Fp/merge_requests?state=opened", url.AbsoluteUri);
        Assert.Equal("/api/v4/projects/g%2Fs%2Fp/merge_requests", url.AbsolutePath);

        var reply = await client.SendAsync(HttpMethod.Get, lab.Ada, url, null, GitLabJsonContext.Default.ListGitLabMergeRequestJson, Cancelled);

        Assert.True(reply.Succeeded);
        Assert.Equal(("/api/v4/projects/g%2Fs%2Fp/merge_requests", "state=opened", "mrs:g/s/p"), (lab.Fake.Requests[0].Path, lab.Fake.Requests[0].Query, lab.Fake.Requests[0].Route));
        Assert.Equal("/api/v4/projects/g%2Fs%2Fp/merge_requests", lab.Recorder!.Sent[0].Url.AbsolutePath);
    }

    [Fact]
    public async Task Another_origin_a_fourth_hop_and_a_Link_elsewhere_are_Malformed_and_nothing_is_sent_there()
    {
        var log = new ListLog();
        var hops = 0;
        var recorder = new RecordingHttpHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/v4/user" => Redirect("https://evil.example/api/v4/user"),
            "/api/v4/loop" or "/api/v4/loop2" => Redirect(++hops % 2 == 1 ? "https://gitlab.com/api/v4/loop2" : "https://gitlab.com/api/v4/loop"),
            "/api/v4/projects" when request.RequestUri.Query.Contains("off", StringComparison.Ordinal) =>
                RecordingHttpHandler.Json(HttpStatusCode.OK, "[]", ("Link", "<https://evil.example/api/v4/projects?page=2>; rel=\"next\"")),
            "/api/v4/projects" => RecordingHttpHandler.Json(HttpStatusCode.OK, "[]", ("Link", "<https://gitlab.com/projects?page=2>; rel=\"next\"")),
            _ => RecordingHttpHandler.Json(HttpStatusCode.NotFound, "{\"error\":\"404 Not Found\"}"),
        });
        using var client = GitLabClient.Create(GitLabEndpoints.GitLabCom, () => recorder, log, new ManualTimeProvider());
        var api = GitLabEndpoints.GitLabCom.ApiBase.AbsoluteUri;

        var elsewhere = await client.SendAsync(HttpMethod.Get, "glpat-FAKE-ada-1", new Uri(api + "user"), null, GitLabJsonContext.Default.GitLabUserJson, Cancelled);
        var fourth = await client.SendAsync(HttpMethod.Get, "glpat-FAKE-ada-1", new Uri(api + "loop"), null, GitLabJsonContext.Default.GitLabUserJson, Cancelled);
        var offOrigin = await client.GetPagesAsync("glpat-FAKE-ada-1", new Uri(api + "projects?off=1"), GitLabJsonContext.Default.ListGitLabProjectJson, 10, Cancelled);
        var offPath = await client.GetPagesAsync("glpat-FAKE-ada-1", new Uri(api + "projects"), GitLabJsonContext.Default.ListGitLabProjectJson, 10, Cancelled);

        Assert.All(new[] { elsewhere.Failure, fourth.Failure, offOrigin.Failure, offPath.Failure }, failure =>
            Assert.Equal((HostingFailureKind.Malformed, "GitLab's answer could not be read.", HostingProvider.GitLab), (failure!.Kind, failure.Message, failure.Provider)));
        Assert.Equal(1 + 4 + 1 + 1, recorder.Sent.Count);
        Assert.All(recorder.Sent, sent => Assert.Equal("gitlab.com", sent.Url.Host));
        Assert.Equal(
            [
                "GitLab: GET /api/v4/user failed: redirected to https://evil.example:443.",
                "GitLab: GET /api/v4/loop2 failed: 301, a fourth redirect.",
                "GitLab: GET /api/v4/projects failed: Link to https://evil.example:443.",
                "GitLab: GET /api/v4/projects failed: Link to https://gitlab.com:443.",
            ],
            log.Lines.Select(line => line.Message));

        static HttpResponseMessage Redirect(string location)
        {
            var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Content = new StringContent("moved", Encoding.UTF8, "text/plain") };
            response.Headers.Location = new Uri(location);
            return response;
        }
    }

    [Fact]
    public async Task A_list_follows_Link_page_by_page_at_MaxPerPage_2_and_stops_at_the_cap_with_the_next_page_left()
    {
        await using var lab = await GitLabLab.CreateAsync();
        lab.Fake.AddListedProjects("g/listed", 5);
        using var client = lab.Client();
        var first = Url(lab, "projects?membership=true&per_page=100");
        var whole = await client.GetPagesAsync(lab.Ada, first, GitLabJsonContext.Default.ListGitLabProjectJson, 10, Cancelled);
        var all = whole.Value!.Select(project => project.PathWithNamespace).ToList();
        Assert.True(all.Count >= 8);
        lab.Fake.MaxPerPage = 2;
        var before = lab.Fake.Requests.Count;

        var paged = await client.GetPagesAsync(lab.Ada, first, GitLabJsonContext.Default.ListGitLabProjectJson, 10, Cancelled);
        var capped = await client.GetPagesAsync(lab.Ada, first, GitLabJsonContext.Default.ListGitLabProjectJson, 3, Cancelled);

        Assert.Equal(all, paged.Value!.Select(project => project.PathWithNamespace));
        Assert.Null(paged.Next);
        var pages = (all.Count + 1) / 2;
        Assert.Equal(
            Enumerable.Range(1, pages).Select(page => page == 1 ? "membership=true&per_page=100" : $"membership=true&page={page}&per_page=2"),
            lab.Fake.Requests.Skip(before).Take(pages).Select(request => request.Query).Select(Sorted));
        Assert.Equal(all.Take(6), capped.Value!.Select(project => project.PathWithNamespace));
        Assert.Equal("page=4", capped.Next!.Query.TrimStart('?').Split('&').Single(part => part.StartsWith("page=", StringComparison.Ordinal)));
        Assert.Equal(before + pages + 3, lab.Fake.Requests.Count);
    }

    [Fact]
    public async Task A_repeated_GET_is_a_counted_304_with_the_same_value_and_Link_while_another_tokens_is_not_conditional()
    {
        await using var lab = await GitLabLab.CreateAsync();
        using var client = lab.Client();
        var url = Url(lab, "projects/g%2Fs%2Fp");

        var first = await client.SendAsync(HttpMethod.Get, lab.Ada, url, null, GitLabJsonContext.Default.GitLabProjectJson, Cancelled);
        var again = await client.SendAsync(HttpMethod.Get, lab.Ada, url, null, GitLabJsonContext.Default.GitLabProjectJson, Cancelled);
        var octo = await client.SendAsync(HttpMethod.Get, lab.Octo, url, null, GitLabJsonContext.Default.GitLabProjectJson, Cancelled);

        Assert.Equal((200, 304, 200), (first.Status, again.Status, octo.Status));
        Assert.Equal(first.Value!.PathWithNamespace, again.Value!.PathWithNamespace);
        Assert.Equal([200, 304, 200], lab.Fake.Requests.Select(request => request.Status));
        Assert.Null(lab.Recorder!.Sent[0].Header("If-None-Match"));
        Assert.StartsWith("W/\"", lab.Recorder.Sent[1].Header("If-None-Match"), StringComparison.Ordinal);
        Assert.Null(lab.Recorder.Sent[2].Header("If-None-Match"));
        Assert.Equal((30, 50), (first.Value.Permissions!.GroupAccess!.AccessLevel, octo.Value!.Permissions!.ProjectAccess!.AccessLevel));

        // A list's pages come back as 304s and still page: the kept answer keeps its Link (r6 8).
        lab.Fake.MaxPerPage = 1;
        var list = Url(lab, "projects?membership=true");
        var listed = await client.GetPagesAsync(lab.Ada, list, GitLabJsonContext.Default.ListGitLabProjectJson, 10, Cancelled);
        var count = lab.Fake.Requests.Count;
        var relisted = await client.GetPagesAsync(lab.Ada, list, GitLabJsonContext.Default.ListGitLabProjectJson, 10, Cancelled);

        Assert.Equal(listed.Value!.Select(project => project.Id), relisted.Value!.Select(project => project.Id));
        Assert.True(listed.Value!.Count > 1);
        Assert.All(lab.Fake.Requests.Skip(count), request => Assert.Equal(304, request.Status));
        Assert.Equal(listed.Value.Count, lab.Fake.Requests.Count - count);
    }

    [Theory]
    [InlineData(401, "application/json", "{\"message\":\"401 Unauthorized\"}", HostingFailureKind.BadCredentials, "401 Unauthorized", null)]
    [InlineData(401, "application/json", "{\"error\":\"invalid_token\",\"error_description\":\"Token is expired. You can either do re-authorization or token refresh.\"}", HostingFailureKind.BadCredentials, "Token is expired. You can either do re-authorization or token refresh.", null)]
    [InlineData(401, "text/plain", "", HostingFailureKind.BadCredentials, "401 Unauthorized", null)]
    [InlineData(403, "application/json", "{\"error\":\"insufficient_scope\",\"error_description\":\"The request requires higher privileges than provided by the access token.\",\"scope\":\"read_user ai_workflows api read_api\"}", HostingFailureKind.Forbidden, "The request requires higher privileges than provided by the access token.", "insufficient_scope")]
    [InlineData(403, "application/json", "{\"error\":\"insufficient_granular_scope\",\"error_description\":\"Access denied: This operation requires a fine-grained personal access token with the following user permissions: [User: Read].\"}", HostingFailureKind.Forbidden, "Access denied: This operation requires a fine-grained personal access token with the following user permissions: [User: Read].", "insufficient_granular_scope")]
    [InlineData(403, "application/json", "{\"message\":\"403 Forbidden\"}", HostingFailureKind.Forbidden, "403 Forbidden", null)]
    [InlineData(404, "application/json", "{\"message\":\"404 Project Not Found\"}", HostingFailureKind.NotFound, "404 Project Not Found", null)]
    [InlineData(404, "application/json", "{\"message\":\"404 Not found\"}", HostingFailureKind.NotFound, "404 Not found", null)]
    [InlineData(404, "application/json", "{\"error\":\"404 Not Found\"}", HostingFailureKind.NotFound, "404 Not Found", null)]
    [InlineData(409, "application/json", "{\"message\":[\"Another open merge request already exists for this source branch: !21\"]}", HostingFailureKind.Validation, "Another open merge request already exists for this source branch: !21", null)]
    [InlineData(400, "application/json", "{\"message\":{\"source_branch\":[\"does not exist\"]}}", HostingFailureKind.Validation, "source_branch does not exist", null)]
    [InlineData(400, "application/json", "{\"message\":{\"source_branch\":[\"does not exist\",\"is invalid\"],\"target_branch\":[\"does not exist\"]}}", HostingFailureKind.Validation, "source_branch does not exist; target_branch does not exist", null)]
    [InlineData(400, "application/json", "{\"error\":\"state does not have a valid value\"}", HostingFailureKind.Validation, "state does not have a valid value", null)]
    [InlineData(400, "application/json", "{\"error\":\"title is missing, source_branch is missing\"}", HostingFailureKind.Validation, "title is missing, source_branch is missing", null)]
    [InlineData(405, "application/json", "{\"message\":\"Non GET methods are not allowed for moved projects\"}", HostingFailureKind.Validation, "Non GET methods are not allowed for moved projects", null)]
    [InlineData(422, "application/json", "{\"message\":[\"You can't use same project/branch for source and target\"]}", HostingFailureKind.Validation, "You can't use same project/branch for source and target", null)]
    [InlineData(429, "text/plain", "Retry later\n", HostingFailureKind.RateLimited, "429 Too Many Requests", null)]
    [InlineData(429, "application/json", "{\"message\":{\"error\":\"This endpoint has been requested too many times. Try again later.\"}}", HostingFailureKind.RateLimited, "This endpoint has been requested too many times. Try again later.", null)]
    [InlineData(408, "application/json", "{\"message\":{\"error\":\"Request timed out\"}}", HostingFailureKind.ServerError, "Request timed out", null)]
    [InlineData(500, "application/json", "{\"message\":\"500 Internal Server Error\"}", HostingFailureKind.ServerError, "500 Internal Server Error", null)]
    [InlineData(502, "text/html", "<html><body><h1>502 Bad Gateway</h1></body></html>", HostingFailureKind.ServerError, "GitLab had a problem: HTTP 502.", null)]
    [InlineData(503, "application/json", "{\"message\":[]}", HostingFailureKind.ServerError, "GitLab had a problem: HTTP 503.", null)]
    [InlineData(418, "application/json", "{\"message\":\"I'm a teapot\"}", HostingFailureKind.Malformed, "GitLab's answer could not be read.", null)]
    [InlineData(404, "text/html", "<html>Not here</html>", HostingFailureKind.NotFound, "404 Not Found", null)]
    [InlineData(400, "application/json", "[\"not an object\"]", HostingFailureKind.Validation, "400 Bad Request", null)]
    [InlineData(400, "application/json", "{\"message\":42,\"error\":7}", HostingFailureKind.Validation, "400 Bad Request", null)]
    [InlineData(400, "application/json", "{\"message\":", HostingFailureKind.Validation, "400 Bad Request", null)]
    public async Task GitLabs_answers_give_its_kinds_and_its_words(int status, string contentType, string body, HostingFailureKind kind, string message, string? detail)
    {
        var log = new ListLog();
        var recorder = new RecordingHttpHandler(_ => Answer((HttpStatusCode)status, contentType, body, ("x-request-id", "01JFAKEREQUEST0000000000")));
        using var client = GitLabClient.Create(GitLabEndpoints.GitLabCom, () => recorder, log, new ManualTimeProvider());

        var reply = await client.SendAsync(HttpMethod.Get, "glpat-FAKE-ada-1", new Uri("https://gitlab.com/api/v4/user?probe=1"), null, GitLabJsonContext.Default.GitLabUserJson, Cancelled);

        var failure = reply.Failure!;
        Assert.Equal((kind, message, detail, PullRequestRefusal.None), (failure.Kind, failure.Message, failure.Detail, failure.Refusal));
        Assert.Equal((HostingProvider.GitLab, "https://gitlab.com/api/v4/user", "01JFAKEREQUEST0000000000"), (failure.Provider, failure.Url!.AbsoluteUri, failure.RequestId));
        Assert.Equal(kind == HostingFailureKind.RateLimited, failure.RetryAt is not null);
        Assert.Equal(
            kind == HostingFailureKind.Malformed ? "GitLab: GET /api/v4/user failed: 418 (request 01JFAKEREQUEST0000000000)." : $"GitLab: GET /api/v4/user failed: {status} (request 01JFAKEREQUEST0000000000).",
            Assert.Single(log.Lines).Message);
    }

    [Theory]
    [InlineData("Unauthorized", HostingFailureKind.BadCredentials, "401 Unauthorized", null, null)]
    [InlineData("Expired", HostingFailureKind.BadCredentials, "Token is expired. You can either do re-authorization or token refresh.", null, null)]
    [InlineData("Revoked", HostingFailureKind.BadCredentials, "Token was revoked. You have to re-authorize from the user.", null, null)]
    [InlineData("InsufficientScope", HostingFailureKind.Forbidden, "The request requires higher privileges than provided by the access token.", "insufficient_scope", null)]
    [InlineData("InsufficientGranular", HostingFailureKind.Forbidden, "Access denied: This operation requires a fine-grained personal access token with the following user permissions: [User: Read].", "insufficient_granular_scope", null)]
    [InlineData("Forbidden", HostingFailureKind.Forbidden, "403 Forbidden", null, null)]
    [InlineData("NotFound", HostingFailureKind.NotFound, "404 Not Found", null, null)]
    [InlineData("RateLimited", HostingFailureKind.RateLimited, "429 Too Many Requests", null, 60)]
    [InlineData("EndpointRateLimited", HostingFailureKind.RateLimited, "This endpoint has been requested too many times. Try again later.", null, 600)]
    [InlineData("Timeout", HostingFailureKind.ServerError, "Request timed out", null, null)]
    [InlineData("ServerError", HostingFailureKind.ServerError, "500 Internal Server Error", null, null)]
    [InlineData("BadGateway", HostingFailureKind.ServerError, "GitLab had a problem: HTTP 502.", null, null)]
    [InlineData("Malformed", HostingFailureKind.Malformed, "GitLab's answer could not be read.", null, null)]
    public async Task The_fakes_faults_give_their_kinds_naming_GitLab_its_address_and_its_request_id(string fault, HostingFailureKind kind, string message, string? detail, int? retrySeconds)
    {
        await using var lab = await GitLabLab.CreateAsync(withGit: false);
        using var client = lab.Client();
        lab.Fake.FailNext("user", Fault(fault));

        var reply = await client.SendAsync(HttpMethod.Get, lab.Ada, Url(lab, "user?probe=1"), null, GitLabJsonContext.Default.GitLabUserJson, Cancelled);
        var after = await client.SendAsync(HttpMethod.Get, lab.Ada, Url(lab, "user"), null, GitLabJsonContext.Default.GitLabUserJson, Cancelled);

        var failure = reply.Failure!;
        Assert.Equal((kind, message, detail), (failure.Kind, failure.Message, failure.Detail));
        Assert.Equal((HostingProvider.GitLab, "http://127.0.0.1:3/api/v4/user"), (failure.Provider, failure.Url!.AbsoluteUri));
        Assert.StartsWith("FAKE-GL-", failure.RequestId, StringComparison.Ordinal);
        Assert.Equal(retrySeconds is null ? null : lab.Clock.GetUtcNow().AddSeconds(retrySeconds.Value), failure.RetryAt);
        Assert.Equal("ada", after.Value!.Username);
        lab.AssertNoSecret();
    }

    [Theory]
    [InlineData("POST", "projects/5001/merge_requests", 409, "{\"message\":[\"Another open merge request already exists for this source branch: !21\"]}", PullRequestRefusal.AlreadyExists)]
    [InlineData("POST", "projects/5001/merge_requests", 400, "{\"message\":{\"source_branch\":[\"does not exist\"]}}", PullRequestRefusal.HeadMissing)]
    [InlineData("POST", "projects/5001/merge_requests", 400, "{\"message\":{\"target_branch\":[\"does not exist\"]}}", PullRequestRefusal.BaseMissing)]
    [InlineData("POST", "projects/5001/merge_requests", 400, "{\"message\":{\"source_branch\":[\"does not exist\"],\"target_branch\":[\"does not exist\"]}}", PullRequestRefusal.BranchesMissing)]
    [InlineData("POST", "projects/5001/merge_requests", 400, "{\"message\":{\"title\":[\"is too long\"]}}", PullRequestRefusal.None)]
    [InlineData("POST", "projects/5001/merge_requests", 400, "{\"error\":\"source_branch is missing\"}", PullRequestRefusal.None)]
    [InlineData("POST", "projects/5001/merge_requests", 422, "{\"message\":[\"Source project is not a fork of the target project\"]}", PullRequestRefusal.None)]
    [InlineData("POST", "projects/5001/merge_requests", 403, "{\"message\":\"403 Forbidden\"}", PullRequestRefusal.None)]
    [InlineData("GET", "projects/5001/merge_requests", 409, "{\"message\":[\"Another open merge request already exists for this source branch: !21\"]}", PullRequestRefusal.None)]
    [InlineData("GET", "projects/5001/merge_requests", 400, "{\"message\":{\"source_branch\":[\"does not exist\"]}}", PullRequestRefusal.None)]
    [InlineData("PUT", "projects/5001/merge_requests/21", 409, "{\"message\":[\"Another open merge request already exists for this source branch: !21\"]}", PullRequestRefusal.None)]
    [InlineData("POST", "projects/5001/repository/branches", 400, "{\"message\":{\"source_branch\":[\"does not exist\"]}}", PullRequestRefusal.None)]
    public async Task A_refusals_reason_is_set_on_the_creates_POST_alone(string method, string path, int status, string body, PullRequestRefusal refusal)
    {
        var recorder = new RecordingHttpHandler(_ => Answer((HttpStatusCode)status, "application/json", body));
        using var client = GitLabClient.Create(GitLabEndpoints.GitLabCom, () => recorder, new ListLog(), new ManualTimeProvider());
        var payload = method == "GET" ? null : new GitLabNewMergeRequestJson { SourceBranch = "a", TargetBranch = "b", Title = "c", Description = "" };

        var reply = await client.SendAsync(new HttpMethod(method), "glpat-FAKE-ada-1", new Uri("https://gitlab.com/api/v4/" + path), payload, GitLabJsonContext.Default.GitLabMergeRequestJson, Cancelled);

        Assert.Equal(refusal, reply.Failure!.Refusal);
    }

    [Fact]
    public async Task Made_up_words_are_classified_alike_and_a_403_naming_a_rate_limit_or_api_stays_Forbidden()
    {
        var answers = new Queue<HttpResponseMessage>(
        [
            Answer(HttpStatusCode.Conflict, "application/json", "{\"message\":[\"Zut alors, encore une fois\"]}"),
            Answer(HttpStatusCode.BadRequest, "application/json", "{\"message\":{\"source_branch\":[\"n'existe pas\"],\"target_branch\":[\"fehlt\"]}}"),
            Answer(HttpStatusCode.BadRequest, "application/json", "{\"message\":{\"target_branch\":[]}}"),
            Answer(HttpStatusCode.Forbidden, "application/json", "{\"message\":\"403 Forbidden - Rate limit exceeded, retry later\"}", ("ratelimit-remaining", "0"), ("Retry-After", "60")),
            Answer(HttpStatusCode.Forbidden, "application/json", "{\"error\":\"insufficient_scope\",\"error_description\":\"Il faut plus de droits.\",\"scope\":\"read_api\"}"),
            Answer(HttpStatusCode.Forbidden, "application/json", "{\"message\":\"insufficient_scope: the token needs api\"}"),
            Answer(HttpStatusCode.Forbidden, "application/json", "{\"error\":\"insufficient_granular_scope\"}"),
            Answer(HttpStatusCode.Unauthorized, "application/json", "{\"error\":\"insufficient_granular_scope\",\"error_description\":\"Not this one.\"}"),
        ]);
        var recorder = new RecordingHttpHandler(_ => answers.Dequeue());
        using var client = GitLabClient.Create(GitLabEndpoints.GitLabCom, () => recorder, new ListLog(), new ManualTimeProvider());
        var create = new Uri("https://gitlab.com/api/v4/projects/5001/merge_requests");
        var body = new GitLabNewMergeRequestJson { SourceBranch = "a", TargetBranch = "b", Title = "c", Description = "" };
        var failures = new List<HostingFailure>();
        for (var i = 0; i < 8; i++)
        {
            var reply = await client.SendAsync(HttpMethod.Post, "glpat-FAKE-ada-1", create, body, GitLabJsonContext.Default.GitLabMergeRequestJson, Cancelled);
            failures.Add(reply.Failure!);
        }

        Assert.Equal(
            [
                (HostingFailureKind.Validation, PullRequestRefusal.AlreadyExists, "Zut alors, encore une fois", (string?)null, (DateTimeOffset?)null),
                (HostingFailureKind.Validation, PullRequestRefusal.BranchesMissing, "source_branch n'existe pas; target_branch fehlt", null, null),
                (HostingFailureKind.Validation, PullRequestRefusal.BaseMissing, "target_branch", null, null),
                (HostingFailureKind.Forbidden, PullRequestRefusal.None, "403 Forbidden - Rate limit exceeded, retry later", null, null),
                (HostingFailureKind.Forbidden, PullRequestRefusal.None, "Il faut plus de droits.", "insufficient_scope", null),
                (HostingFailureKind.Forbidden, PullRequestRefusal.None, "insufficient_scope: the token needs api", null, null),
                (HostingFailureKind.Forbidden, PullRequestRefusal.None, "insufficient_granular_scope", null, null),
                (HostingFailureKind.BadCredentials, PullRequestRefusal.None, "Not this one.", null, null),
            ],
            failures.Select(failure => (failure.Kind, failure.Refusal, failure.Message, failure.Detail, failure.RetryAt)));
    }

    [Fact]
    public async Task A_rate_limits_end_comes_from_Retry_After_then_ratelimit_reset_then_ratelimit_resettime_then_60_seconds()
    {
        var now = AppClocks.HarnessTime;
        var reset = now.AddMinutes(7).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var resetTime = now.AddMinutes(9).ToString("r", CultureInfo.InvariantCulture);
        var cases = new (string Name, (string, string)[] Headers, DateTimeOffset? RetryAt, HostingFailureKind Kind)[]
        {
            ("seconds", [("Retry-After", "120"), ("ratelimit-reset", reset), ("RateLimit-ResetTime", resetTime)], now.AddSeconds(120), HostingFailureKind.RateLimited),
            ("a day at most", [("Retry-After", "999999")], now.AddDays(1), HostingFailureKind.RateLimited),
            ("a date", [("Retry-After", now.AddMinutes(3).ToString("r", CultureInfo.InvariantCulture))], now.AddMinutes(3), HostingFailureKind.RateLimited),
            ("unreadable, then the reset", [("Retry-After", "soon"), ("ratelimit-reset", reset)], DateTimeOffset.FromUnixTimeSeconds(long.Parse(reset, CultureInfo.InvariantCulture)), HostingFailureKind.RateLimited),
            ("the reset", [("ratelimit-reset", reset), ("RateLimit-ResetTime", resetTime)], DateTimeOffset.FromUnixTimeSeconds(long.Parse(reset, CultureInfo.InvariantCulture)), HostingFailureKind.RateLimited),
            ("the reset time", [("RateLimit-ResetTime", resetTime)], now.AddMinutes(9), HostingFailureKind.RateLimited),
            ("none", [], now.AddSeconds(60), HostingFailureKind.RateLimited),
            ("a reset past year 9999", [("ratelimit-reset", "253402300800")], null, HostingFailureKind.Malformed),
        };
        foreach (var (name, headers, retryAt, kind) in cases)
        {
            var recorder = new RecordingHttpHandler(_ => Answer(HttpStatusCode.TooManyRequests, "text/plain", "Retry later\n", headers));
            var log = new ListLog();
            using var client = GitLabClient.Create(GitLabEndpoints.GitLabCom, () => recorder, log, new ManualTimeProvider(now));

            var reply = await client.SendAsync(HttpMethod.Get, "glpat-FAKE-ada-1", new Uri("https://gitlab.com/api/v4/projects"), null, GitLabJsonContext.Default.ListGitLabProjectJson, Cancelled);

            Assert.True((kind, retryAt) == (reply.Failure!.Kind, reply.Failure.RetryAt), $"{name}: {reply.Failure.Kind} {reply.Failure.RetryAt:O}");
            if (kind == HostingFailureKind.Malformed)
            {
                Assert.Equal("GitLab: GET /api/v4/projects failed: Malformed (OverflowException).", Assert.Single(log.Lines).Message);
            }
        }
    }

    [Fact]
    public async Task A_warning_names_GitLab_and_no_query_token_or_address_and_a_failures_words_are_screened()
    {
        await using var lab = await GitLabLab.CreateAsync(withGit: false);
        using var client = lab.Client();

        var missing = await client.SendAsync(HttpMethod.Get, lab.Ada, Url(lab, $"projects/nobody%2Fnothing?private_token={lab.Ada}&search=ada@example.com"), null, GitLabJsonContext.Default.GitLabProjectJson, Cancelled);
        var addressed = await client.SendAsync(HttpMethod.Get, lab.Ada, Url(lab, "projects/ada%40example.com%2Fp"), null, GitLabJsonContext.Default.GitLabProjectJson, Cancelled);

        Assert.Equal((HostingFailureKind.NotFound, "404 Project Not Found"), (missing.Failure!.Kind, missing.Failure.Message));
        Assert.Equal("http://127.0.0.1:3/api/v4/projects/nobody%2Fnothing", missing.Failure.Url!.AbsoluteUri);
        Assert.Equal("http://127.0.0.1:3/api/v4/projects/%3Ce-mail%3E", addressed.Failure!.Url!.AbsoluteUri);
        Assert.Equal(
            [
                $"GitLab: GET /api/v4/projects/nobody%2Fnothing failed: 404 (request {missing.Failure.RequestId}).",
                $"GitLab: GET /api/v4/projects/<e-mail> failed: 404 (request {addressed.Failure.RequestId}).",
            ],
            lab.Log.Lines.Select(line => line.Message));

        var token = "glpat-FAKE-ada-1";
        var recorder = new RecordingHttpHandler(_ => Answer(HttpStatusCode.BadRequest, "application/json", $"{{\"message\":{{\"description\":[\"names ada@example.com and {token}\"]}}}}"));
        var log = new ListLog();
        using var scripted = GitLabClient.Create(GitLabEndpoints.GitLabCom, () => recorder, log, new ManualTimeProvider());
        var echoed = await scripted.SendAsync(HttpMethod.Get, token, new Uri("https://gitlab.com/api/v4/projects?search=" + token), null, GitLabJsonContext.Default.ListGitLabProjectJson, Cancelled);

        Assert.Equal("description names <e-mail> and <token>", echoed.Failure!.Message);
        Assert.Equal("GitLab: GET /api/v4/projects failed: 400.", Assert.Single(log.Lines).Message);
        foreach (var written in new[] { lab.Log.Written, log.Written })
        {
            Assert.DoesNotContain("glpat-", written, StringComparison.Ordinal);
            Assert.DoesNotContain("@example.com", written, StringComparison.Ordinal);
            Assert.DoesNotContain("search", written, StringComparison.Ordinal);
            Assert.DoesNotContain("private_token", written, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task No_answer_is_a_Network_failure_naming_GitLab()
    {
        var log = new ListLog();
        var recorder = new RecordingHttpHandler(_ => throw new HttpRequestException(HttpRequestError.ConnectionError, "refused by glpat-FAKE-ada-1"));
        using var client = GitLabClient.Create(GitLabEndpoints.GitLabCom, () => recorder, log, new ManualTimeProvider());

        var reply = await client.SendAsync(HttpMethod.Get, "glpat-FAKE-ada-1", new Uri("https://gitlab.com/api/v4/user"), null, GitLabJsonContext.Default.GitLabUserJson, Cancelled);

        Assert.Equal((HostingFailureKind.Network, "Could not reach GitLab.", HostingProvider.GitLab), (reply.Failure!.Kind, reply.Failure.Message, reply.Failure.Provider));
        Assert.Equal("GitLab: GET /api/v4/user failed: Network (HttpRequestException ConnectionError).", Assert.Single(log.Lines).Message);
    }

    private static Uri Url(GitLabLab lab, string relative) => new(lab.Fake.Endpoints.ApiBase.AbsoluteUri + relative);

    private static string Sorted(string query) => string.Join('&', query.Split('&').Order(StringComparer.Ordinal));

    private static FakeGitLabFault Fault(string name) => name switch
    {
        "Unauthorized" => FakeGitLabFault.Unauthorized,
        "Expired" => FakeGitLabFault.Expired,
        "Revoked" => FakeGitLabFault.Revoked,
        "InsufficientScope" => FakeGitLabFault.InsufficientScope,
        "InsufficientGranular" => FakeGitLabFault.InsufficientGranular,
        "Forbidden" => FakeGitLabFault.Forbidden,
        "NotFound" => FakeGitLabFault.NotFound,
        "RateLimited" => FakeGitLabFault.RateLimited,
        "EndpointRateLimited" => FakeGitLabFault.EndpointRateLimited,
        "Timeout" => FakeGitLabFault.Timeout,
        "ServerError" => FakeGitLabFault.ServerError,
        "BadGateway" => FakeGitLabFault.BadGateway,
        "Malformed" => FakeGitLabFault.Malformed,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    internal static HttpResponseMessage Answer(HttpStatusCode status, string contentType, string body, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)) };
        response.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        foreach (var (name, value) in headers)
        {
            if (!response.Headers.TryAddWithoutValidation(name, value))
            {
                response.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return response;
    }
}

/// <summary>
/// The fake of GitLab in process for GitLab's client and provider tests (docs/phase-7b-design.md
/// 7.5): users ada (2001), octo (2002) and bob (2003) with fine-grained tokens and ada's legacy one;
/// ada a Developer of group <c>g</c>, octo an Owner and bob a Reporter of <c>g/s/p</c>; projects
/// <c>g/s/p</c> (with git, branches <c>main</c>, <c>feature/one</c>, <c>feature/two</c>, <c>fix</c>),
/// <c>ada/p</c> its fork, <c>pub/open</c> public, <c>g/s/t/deep</c> and octo's private <c>octo/secret</c>;
/// a recording handler over the fake's own, and a log and a clock at the harness's time.
/// </summary>
internal sealed class GitLabLab : IAsyncDisposable
{
    private readonly TempRepo? _source;
    private readonly TempDirectory _root;

    private GitLabLab(TempRepo? source, TempDirectory root)
    {
        _source = source;
        _root = root;
        Fake = FakeGitLab.InProcess(root.Path);
    }

    public FakeGitLab Fake { get; }

    public ListLog Log { get; } = new();

    public ManualTimeProvider Clock { get; } = new(AppClocks.HarnessTime);

    /// <summary>The handler the last client or provider was given.</summary>
    public RecordingHttpHandler? Recorder { get; private set; }

    public FakeGitLabProject Project { get; private set; } = null!;

    public FakeGitLabProject Fork { get; private set; } = null!;

    public FakeGitLabProject Public { get; private set; } = null!;

    public FakeGitLabProject Secret { get; private set; } = null!;

    public string Ada { get; private set; } = string.Empty;

    public string AdaLegacy { get; private set; } = string.Empty;

    public string Octo { get; private set; } = string.Empty;

    public string Bob { get; private set; } = string.Empty;

    /// <summary>The source repository's path: <c>g/s/p</c>'s and its fork's history; null without git.</summary>
    public string? SourcePath => _source?.Path;

    /// <summary>The lab; <paramref name="withGit"/> false gives projects without a source (empty bare repositories), for tests of the API alone.</summary>
    public static async Task<GitLabLab> CreateAsync(bool withGit = true)
    {
        var ct = TestContext.Current.CancellationToken;
        TempRepo? source = null;
        if (withGit)
        {
            source = await TempRepo.CreateAsync("gitlab-client-source");
            await source.CommitFileAsync("README.md", "web\n", "Initial commit");
            foreach (var branch in new[] { "feature/one", "feature/two", "fix" })
            {
                await source.CreateBranchAsync(branch, "main");
                await source.CheckoutAsync(branch);
                await source.CommitFileAsync(branch.Replace('/', '-') + ".txt", branch + "\n", "Work on " + branch);
            }

            await source.CheckoutAsync("main");
        }

        var lab = new GitLabLab(source, new TempDirectory("fake gitlab client"));
        var fake = lab.Fake;
        fake.AddUser("ada", 2001, Scenarios.AdaName);
        fake.AddUser("octo", 2002, Scenarios.OctoName);
        fake.AddUser("bob", 2003, "Bob Example");
        lab.Ada = fake.AddToken("ada");
        lab.AdaLegacy = fake.AddToken("ada", FakeGitLabTokenKind.Legacy);
        lab.Octo = fake.AddToken("octo");
        lab.Bob = fake.AddToken("bob");
        Assert.Equal(("glpat-FAKE-ada-1", "glpat-FAKE-ada-2", "glpat-FAKE-octo-1", "glpat-FAKE-bob-1"), (lab.Ada, lab.AdaLegacy, lab.Octo, lab.Bob));
        fake.Grant("g", "ada", 30);
        fake.Grant("g/s/p", "octo", 50);
        fake.Grant("g/s/p", "bob", 20);
        lab.Project = await fake.AddProjectAsync("g/s/p", source?.Path, ct: ct);
        lab.Fork = await fake.AddProjectAsync("ada/p", source?.Path, forkOf: lab.Project, ct: ct);
        lab.Public = await fake.AddProjectAsync("pub/open", source?.Path, "public", ct: ct);
        await fake.AddProjectAsync("g/s/t/deep", ct: ct);
        lab.Secret = await fake.AddProjectAsync("octo/secret", ct: ct);
        Assert.Equal((5001L, 5002L, 5003L, 5005L), (lab.Project.Id, lab.Fork.Id, lab.Public.Id, lab.Secret.Id));
        return lab;
    }

    /// <summary>A handler factory that records each handler it makes in <see cref="Recorder"/>.</summary>
    public Func<HttpMessageHandler> Handler => () => Recorder = new RecordingHttpHandler(Fake.CreateHandler());

    public HostingHttpClient Client() => GitLabClient.Create(Fake.Endpoints, Handler, Log, Clock);

    public GitLabProvider Provider() => new(Fake.Endpoints, Handler, Log, Clock);

    /// <summary>A fork of <c>g/s/p</c> at <paramref name="path"/>, cloned from the source repository.</summary>
    public Task<FakeGitLabProject> AddForkAsync(string path, string visibility = "private") =>
        Fake.AddProjectAsync(path, _source?.Path, visibility, forkOf: Project, ct: TestContext.Current.CancellationToken);

    /// <summary>No token in the fake's records and no token or address in the log.</summary>
    public void AssertNoSecret()
    {
        Assert.All(Fake.Requests, request => Assert.DoesNotContain("glpat-", request.Path + request.Query + request.Body + request.Login, StringComparison.Ordinal));
        Assert.DoesNotContain("glpat-", Log.Written, StringComparison.Ordinal);
        Assert.DoesNotContain("@example.com", Log.Written, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        await Fake.DisposeAsync();
        _root.Dispose();
        _source?.Dispose();
    }
}
