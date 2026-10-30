using System.Net;
using System.Text;
using System.Text.Json;
using VisualCommit.Core;
using VisualCommit.Hosting.GitHub;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The fake of GitHub in process (docs/phase-7a-design.md 2.4, 2.9): every route, the checks in
/// their order, GitHub's shapes and headers, each fault once and then the normal answer, the
/// holds, A5's ref and refusals, A6 and its 409, avatars and pages, a renamed repository's names
/// and redirects, and a long list's pages. On a copy of "hosting" served in process
/// (<see cref="Scenarios.ServeHostingAsync"/>).
/// </summary>
public sealed class FakeGitHubTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_checks_come_in_order_and_errors_have_githubs_shape()
    {
        await using var scenario = await ScenarioAsync();
        var fake = scenario.Served.GitHub;

        await AssertErrorAsync(fake, HttpMethod.Get, "user", 403, "Request forbidden by administrative rules. Please make sure your request has a User-Agent header.", token: null, edit: request => request.Headers.UserAgent.Clear());
        await AssertErrorAsync(fake, HttpMethod.Get, "user", 400, "Unsupported 'X-GitHub-Api-Version' header: 2022-11-28.", token: null, edit: request =>
        {
            request.Headers.Remove("X-GitHub-Api-Version");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        });
        await AssertErrorAsync(fake, HttpMethod.Get, "user", 415, "Unsupported 'Accept' header.", token: null, edit: request => request.Headers.Accept.Clear());
        await AssertErrorAsync(fake, HttpMethod.Get, "user", 401, "Requires authentication", token: null);
        await AssertErrorAsync(fake, HttpMethod.Get, "user", 401, "Requires authentication", token: null, edit: request =>
            request.Headers.TryAddWithoutValidation("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + scenario.Served.OctoToken))));
        await AssertErrorAsync(fake, HttpMethod.Get, "user", 401, "Bad credentials", token: "github_pat_FAKE-nobody-9");

        // A private repository the login cannot see, or a classic token without repo, is not found.
        await AssertErrorAsync(fake, HttpMethod.Get, "repos/octo/notes", 404, "Not Found", scenario.Served.AdaToken);
        var workflowOnly = fake.AddToken("octo", FakeTokenKind.Classic, ["workflow"]);
        await AssertErrorAsync(fake, HttpMethod.Get, "repos/octo/hello", 404, "Not Found", workflowOnly);
        await AssertErrorAsync(fake, HttpMethod.Get, "repos/octo/missing", 404, "Not Found", scenario.Served.OctoToken);
        await AssertErrorAsync(fake, HttpMethod.Get, "nothing/here", 404, "Not Found", scenario.Served.OctoToken);

        // Revoked, a token is refused at once.
        var revoked = fake.AddToken("ada");
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(fake, HttpMethod.Get, "user", revoked)).Status);
        fake.Revoke(revoked);
        await AssertErrorAsync(fake, HttpMethod.Get, "user", 401, "Bad credentials", revoked);

        // No record holds a token (every token of the fake has "_FAKE-" in it).
        Assert.All(fake.Requests, request => Assert.DoesNotContain("_FAKE-", request.ToString(), StringComparison.Ordinal));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task The_user_and_a_repository_are_answered_with_githubs_shapes_and_headers()
    {
        await using var scenario = await ScenarioAsync();
        var fake = scenario.Served.GitHub;
        var web = fake.Endpoints.WebBase.AbsoluteUri;

        var user = await SendAsync(fake, HttpMethod.Get, "user", scenario.Served.OctoToken);
        Assert.Equal(HttpStatusCode.OK, user.Status);
        Assert.Equal(("octo", 1001, "User", "Octo Cat", web + "avatars/u/1001?v=4", web + "octo"),
            (user.Text("login"), user.Json.GetProperty("id").GetInt64(), user.Text("type"), user.Text("name"), user.Text("avatar_url"), user.Text("html_url")));
        Assert.Equal("application/json; charset=utf-8", user.Header("Content-Type"));
        Assert.Matches("^W/\"[0-9a-f]{32}\"$", user.Header("ETag"));
        Assert.Matches("^FAKE-[0-9]{4}$", user.Header("X-GitHub-Request-Id"));
        Assert.Equal(("5000", "4999", "1", "core"), (user.Header("X-RateLimit-Limit"), user.Header("X-RateLimit-Remaining"), user.Header("X-RateLimit-Used"), user.Header("X-RateLimit-Resource")));
        Assert.Equal(AppClocks.HarnessTime.AddHours(1).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), user.Header("X-RateLimit-Reset"));
        Assert.Null(user.Header("X-OAuth-Scopes"));

        // A matching If-None-Match: 304, no body, not counted.
        var again = await SendAsync(fake, HttpMethod.Get, "user", scenario.Served.OctoToken, edit: request => request.Headers.TryAddWithoutValidation("If-None-Match", user.Header("ETag")));
        Assert.Equal((HttpStatusCode.NotModified, string.Empty, "1"), (again.Status, again.Body, again.Header("X-RateLimit-Used")));
        Assert.True(fake.Requests[^1].Conditional);

        // A classic token shows its scopes; a token with an expiry shows it.
        var classic = await SendAsync(fake, HttpMethod.Get, "user", scenario.Served.OctoClassicToken);
        Assert.Equal(("repo, workflow", "2"), (classic.Header("X-OAuth-Scopes"), classic.Header("X-RateLimit-Used")));
        var expiring = fake.AddToken("ada", expires: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("2026-02-01 00:00:00 UTC", (await SendAsync(fake, HttpMethod.Get, "user", expiring)).Header("GitHub-Authentication-Token-Expiration"));

        // A fork's parent and source; by id; the permissions of the login.
        var fork = await SendAsync(fake, HttpMethod.Get, "repos/ADA/Hello", scenario.Served.OctoToken);
        Assert.Equal(("ada/hello", true, "octo/hello", "octo/hello", "main", false), (fork.Text("full_name"), fork.Json.GetProperty("fork").GetBoolean(),
            fork.Json.GetProperty("parent").GetProperty("full_name").GetString(), fork.Json.GetProperty("source").GetProperty("full_name").GetString(), fork.Text("default_branch"), fork.Json.GetProperty("private").GetBoolean()));
        Assert.Equal("repo:ada/hello", fake.Requests[^1].Route);
        var byId = await SendAsync(fake, HttpMethod.Get, "repositories/3001", scenario.Served.OctoToken);
        Assert.Equal((3001, "octo/hello", true, web + "octo/hello.git", web + "octo/hello", "git@127.0.0.1:octo/hello.git"),
            (byId.Json.GetProperty("id").GetInt64(), byId.Text("full_name"), byId.Json.GetProperty("permissions").GetProperty("push").GetBoolean(), byId.Text("clone_url"), byId.Text("html_url"), byId.Text("ssh_url")));
        Assert.Equal("2026-01-02T09:00:00Z", byId.Text("updated_at"));
        var tool = await SendAsync(fake, HttpMethod.Get, "repos/acme/tool", scenario.Served.AdaToken);
        Assert.Equal((true, false, "Organization"), (tool.Json.GetProperty("permissions").GetProperty("push").GetBoolean(), tool.Json.GetProperty("permissions").GetProperty("admin").GetBoolean(), tool.Json.GetProperty("owner").GetProperty("type").GetString()));
        var website = await SendAsync(fake, HttpMethod.Get, "repos/octo/website", scenario.Served.OctoToken);
        Assert.True(website.Json.GetProperty("archived").GetBoolean());
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task The_repository_list_is_paged_sorted_and_filtered_and_type_with_visibility_is_refused()
    {
        await using var scenario = await ScenarioAsync();
        var fake = scenario.Served.GitHub;
        var token = scenario.Served.OctoToken;

        Assert.Equal(["octo/hello", "octo/notes", "octo/tool", "octo/website"], Names(await SendAsync(fake, HttpMethod.Get, "user/repos", token)));
        Assert.Equal(["octo/website", "octo/tool", "octo/notes", "octo/hello"], Names(await SendAsync(fake, HttpMethod.Get, "user/repos?direction=desc", token)));
        Assert.Equal(["octo/website", "octo/notes", "octo/tool", "octo/hello"], Names(await SendAsync(fake, HttpMethod.Get, "user/repos?sort=updated&direction=asc", token)));
        Assert.Equal(["octo/hello", "octo/notes"], Names(await SendAsync(fake, HttpMethod.Get, "user/repos?visibility=private", token)));
        Assert.Equal(["acme/tool", "ada/hello"], Names(await SendAsync(fake, HttpMethod.Get, "user/repos", scenario.Served.AdaToken)));
        Assert.Equal(["acme/tool"], Names(await SendAsync(fake, HttpMethod.Get, "user/repos?affiliation=organization_member", scenario.Served.AdaToken)));
        Assert.Equal(["ada/hello"], Names(await SendAsync(fake, HttpMethod.Get, "user/repos?type=owner", scenario.Served.AdaToken)));
        await AssertErrorAsync(fake, HttpMethod.Get, "user/repos?type=all&visibility=all", 422, "If you specify visibility or affiliation, you cannot specify type.", token);

        var first = await SendAsync(fake, HttpMethod.Get, "user/repos?sort=updated&per_page=3", token);
        var api = fake.Endpoints.ApiBase.AbsoluteUri;
        Assert.Equal(["octo/hello", "octo/tool", "octo/notes"], Names(first));
        Assert.Equal($"<{api}user/repos?sort=updated&per_page=3&page=2>; rel=\"next\", <{api}user/repos?sort=updated&per_page=3&page=2>; rel=\"last\"", first.Header("Link"));
        var second = await SendAsync(fake, HttpMethod.Get, "user/repos?sort=updated&per_page=3&page=2", token);
        Assert.Equal(["octo/website"], Names(second));
        Assert.Equal($"<{api}user/repos?sort=updated&per_page=3&page=1>; rel=\"prev\", <{api}user/repos?sort=updated&per_page=3&page=1>; rel=\"first\"", second.Header("Link"));

        // A long list: per_page is capped at MaxPerPage, and the tenth page of 100 still has a next.
        fake.AddListedRepositories("octo", 1001);
        var tenth = await SendAsync(fake, HttpMethod.Get, "user/repos?per_page=500&page=10", token);
        Assert.Equal(100, tenth.Json.GetArrayLength());
        Assert.Contains($"<{api}user/repos?per_page=500&page=11>; rel=\"next\"", tenth.Header("Link"), StringComparison.Ordinal);
        var eleventh = await SendAsync(fake, HttpMethod.Get, "user/repos?per_page=100&page=11", token);
        Assert.Equal(5, eleventh.Json.GetArrayLength());
        Assert.DoesNotContain("rel=\"next\"", eleventh.Header("Link"), StringComparison.Ordinal);
        Assert.Equal("octo/listed-0001", (await SendAsync(fake, HttpMethod.Get, "repos/octo/listed-0001", token)).Text("full_name"));
        fake.MaxPerPage = 2;
        Assert.Equal(2, (await SendAsync(fake, HttpMethod.Get, "user/repos?per_page=100", token)).Json.GetArrayLength());
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Pull_requests_are_listed_by_number_and_filtered_and_a_patch_closes_one()
    {
        await using var scenario = await ScenarioAsync();
        var fake = scenario.Served.GitHub;
        var token = scenario.Served.OctoToken;

        Assert.Equal(new[] { 5, 3, 2, 1 }, Numbers(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/pulls", token)));
        Assert.Equal(new[] { 4 }, Numbers(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/pulls?state=closed", token)));
        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, Numbers(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/pulls?state=all", token)));
        Assert.Equal(new[] { 5 }, Numbers(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/pulls?head=ada:fix-typo", token)));
        Assert.Equal(new[] { 5, 3, 2, 1 }, Numbers(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/pulls?head=fix-typo", token)));
        Assert.Empty(Numbers(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/pulls?base=docs/readme", token)));

        var list = await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/pulls?per_page=1", token);
        Assert.Contains("page=4>; rel=\"last\"", list.Header("Link"), StringComparison.Ordinal);
        var one = list.Json[0];
        Assert.Equal((4005, "open", "ada", "ada:fix-typo", "fix-typo", "octo:main", "main", true),
            (one.GetProperty("id").GetInt32(), one.GetProperty("state").GetString(), one.GetProperty("user").GetProperty("login").GetString(),
            one.GetProperty("head").GetProperty("label").GetString(), one.GetProperty("head").GetProperty("ref").GetString(),
            one.GetProperty("base").GetProperty("label").GetString(), one.GetProperty("base").GetProperty("ref").GetString(), one.GetProperty("maintainer_can_modify").GetBoolean()));
        Assert.Equal(fake.Endpoints.WebBase.AbsoluteUri + "octo/hello/pull/5", one.GetProperty("html_url").GetString());
        Assert.Equal(await RevParseAsync(scenario.Hello, "origin/main"), one.GetProperty("base").GetProperty("sha").GetString());
        Assert.Equal(await RevParseAsync(scenario.Hello, "origin/feature/login"), fake.PullRequests("octo/hello").Single(pull => pull.Number == 1).HeadSha);

        var gone = (await SendAsync(fake, HttpMethod.Get, "repos/acme/tool/pulls", token)).Json[0];
        Assert.Equal((8, JsonValueKind.Null, "gone:idea"), (gone.GetProperty("number").GetInt32(), gone.GetProperty("head").GetProperty("repo").ValueKind, gone.GetProperty("head").GetProperty("label").GetString()));
        Assert.Equal("An old idea", await SubjectAsync(scenario.Hello, gone.GetProperty("head").GetProperty("sha").GetString()!));

        var closed = await SendAsync(fake, HttpMethod.Patch, "repos/octo/hello/pulls/3", token, """{"state":"closed"}""");
        Assert.Equal((HttpStatusCode.OK, "closed"), (closed.Status, closed.Text("state")));
        Assert.Equal(("update:octo/hello", """{"state":"closed"}"""), (fake.Requests[^1].Route, fake.Requests[^1].Body));
        Assert.Equal(new[] { 5, 2, 1 }, Numbers(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/pulls", token)));
        await AssertErrorAsync(fake, HttpMethod.Patch, "repos/octo/hello/pulls/99", 404, "Not Found", token, """{"state":"closed"}""");
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_create_writes_the_pull_ref_and_its_refusals_come_in_order()
    {
        await using var scenario = await ScenarioAsync();
        var fake = scenario.Served.GitHub;
        var token = scenario.Served.OctoToken;
        var bare = fake.Repository("octo/hello").BarePath!;

        await AssertInvalidAsync(fake, token, """{"head":"topic/no-pr","base":"main"}""", """{"resource":"PullRequest","field":"title","code":"missing_field"}""");
        await AssertInvalidAsync(fake, token, """{"title":"T","head":"topic/no-pr","base":"nope"}""", """{"resource":"PullRequest","field":"base","code":"invalid"}""");
        await AssertInvalidAsync(fake, token, """{"title":"T","head":"nope","base":"main"}""", """{"resource":"PullRequest","field":"head","code":"invalid"}""");
        await AssertInvalidAsync(fake, token, """{"title":"T","head":"bob:fix-typo","base":"main"}""", """{"resource":"PullRequest","field":"head","code":"invalid"}""");
        await AssertInvalidAsync(fake, token, """{"title":"T","head":"feature/login","base":"main"}""", """{"resource":"PullRequest","code":"custom","message":"A pull request already exists for octo:feature/login."}""");
        await AssertInvalidAsync(fake, token, """{"title":"T","head":"ada:fix-typo","base":"main"}""", """{"resource":"PullRequest","code":"custom","message":"A pull request already exists for ada:fix-typo."}""");
        await AssertInvalidAsync(fake, token, """{"title":"T","head":"main","base":"main"}""", """{"resource":"PullRequest","code":"custom","message":"No commits between main and main"}""");

        // A body that breaks two rules gets the earlier one's refusal: no title before a bad base,
        // a bad head or an open one; a bad base before a bad head. (A bad head can be neither open
        // nor counted, so it meets no later rule; the open one before nothing ahead is below.)
        var title = """{"resource":"PullRequest","field":"title","code":"missing_field"}""";
        await AssertInvalidAsync(fake, token, """{"head":"topic/no-pr","base":"nope"}""", title);
        await AssertInvalidAsync(fake, token, """{"head":"nope","base":"main"}""", title);
        await AssertInvalidAsync(fake, token, """{"head":"feature/login","base":"main"}""", title);
        await AssertInvalidAsync(fake, token, """{"title":"T","head":"nope","base":"nope"}""", """{"resource":"PullRequest","field":"base","code":"invalid"}""");
        await AssertInvalidAsync(fake, token, """{"title":"T","head":"bob:fix-typo","base":"nope"}""", """{"resource":"PullRequest","field":"base","code":"invalid"}""");

        // A fine-grained token whose login cannot push to the base is refused first.
        var forbidden = await SendAsync(fake, HttpMethod.Post, "repos/acme/tool/pulls", token, """{"title":"T","head":"octo:fix-typo","base":"main"}""");
        Assert.Equal((HttpStatusCode.Forbidden, "Resource not accessible by personal access token", "pull_requests=write"), (forbidden.Status, forbidden.Text("message"), forbidden.Header("X-Accepted-GitHub-Permissions")));

        // A fork's head with nothing ahead: fetched into refs/pull/<n>/head, deleted on refusal.
        var toolBare = fake.Repository("acme/tool").BarePath!;
        await AssertInvalidAsync(fake, scenario.Served.OctoClassicToken, """{"title":"T","head":"octo:main","base":"main"}""", """{"resource":"PullRequest","code":"custom","message":"No commits between main and main"}""", "acme/tool");
        Assert.Null(await RefAsync(scenario.Hello, toolBare, "refs/pull/9/head"));

        // Made: number 6, the author the token's, the ref written, the list showing it.
        var made = await SendAsync(fake, HttpMethod.Post, "repos/octo/hello/pulls", token, """{"title":"Try it again","head":"topic/no-pr","base":"main","body":"Once more.","draft":true,"maintainer_can_modify":false}""");
        Assert.Equal((HttpStatusCode.Created, 6, "octo", true, "Once more.", false),
            (made.Status, made.Json.GetProperty("number").GetInt32(), made.Json.GetProperty("user").GetProperty("login").GetString(), made.Json.GetProperty("draft").GetBoolean(), made.Text("body"), made.Json.GetProperty("maintainer_can_modify").GetBoolean()));
        Assert.Equal(fake.Endpoints.ApiBase.AbsoluteUri + "repos/octo/hello/pulls/6", made.Header("Location"));
        Assert.Equal(await RevParseAsync(scenario.Hello, "origin/topic/no-pr"), await RefAsync(scenario.Hello, bare, "refs/pull/6/head"));
        Assert.Equal(new[] { 6, 5, 3, 2, 1 }, Numbers(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/pulls", token)));

        // An open one with nothing ahead (main into main) is refused as open, before the count.
        fake.AddPullRequest("octo", "hello", "Main into main", "octo", "main", "main", number: 7);
        await AssertInvalidAsync(fake, token, """{"title":"T","head":"main","base":"main"}""", """{"resource":"PullRequest","code":"custom","message":"A pull request already exists for octo:main."}""");
        Assert.Null(await RefAsync(scenario.Hello, bare, "refs/pull/8/head"));

        Assert.Equal("create:octo/hello", fake.Requests.Single(request => request.Status == 201).Route);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Commits_are_listed_with_their_linked_users_and_an_empty_repository_answers_409()
    {
        await using var scenario = await ScenarioAsync();
        var fake = scenario.Served.GitHub;
        var token = scenario.Served.OctoToken;

        var main = await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/commits", token);
        Assert.Equal(["Add the docs folder author@example.com -", "Add the greeting ada@example.com ada", "Initial commit octo@example.com octo"], Commits(main));
        var first = main.Json[2];
        Assert.Equal(("Octo Cat", "2026-01-01T12:00:00Z", "author@example.com", JsonValueKind.Null),
            (first.GetProperty("commit").GetProperty("author").GetProperty("name").GetString(), first.GetProperty("commit").GetProperty("author").GetProperty("date").GetString(),
            first.GetProperty("commit").GetProperty("committer").GetProperty("email").GetString(), first.GetProperty("committer").ValueKind));
        Assert.Equal(["Add the login form ada@example.com ada", "Add the docs folder author@example.com -", "Add the greeting ada@example.com ada", "Initial commit octo@example.com octo"],
            Commits(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/commits?sha=feature/login", token)));
        Assert.Equal(["Add the login form ada@example.com ada", "Add the greeting ada@example.com ada"], Commits(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/commits?sha=feature/login&author=ada", token)));
        Assert.Equal(["Initial commit octo@example.com octo"], Commits(await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/commits?author=OCTO@example.com&per_page=1", token)));
        Assert.Contains("rel=\"next\"", (await SendAsync(fake, HttpMethod.Get, "repos/octo/hello/commits?per_page=1", token)).Header("Link"), StringComparison.Ordinal);

        await AssertErrorAsync(fake, HttpMethod.Get, "repos/octo/hello/commits?sha=nope", 422, "No commit found for SHA: nope", token);
        await AssertErrorAsync(fake, HttpMethod.Get, "repos/octo/notes/commits", 409, "Git Repository is empty.", token);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Avatars_and_pages_need_no_token_and_a_token_sent_with_an_avatar_is_logged()
    {
        await using var scenario = await ScenarioAsync();
        var fake = scenario.Served.GitHub;
        using var client = new HttpClient(fake.CreateHandler());

        using var avatar = await client.GetAsync(new Uri(fake.Endpoints.WebBase, "avatars/u/1003?v=4"), Cancelled);
        Assert.Equal((HttpStatusCode.OK, "image/png"), (avatar.StatusCode, avatar.Content.Headers.ContentType?.MediaType));
        Assert.Equal(TestImages.SquareOn(64, 64, TestImages.Rgb.Parse("#1F883D"), TestImages.Rgb.Parse("#FFFFFF"), 24), await avatar.Content.ReadAsByteArrayAsync(Cancelled));
        Assert.Equal(("avatar:1003", false), (fake.Requests[^1].Route, fake.Requests[^1].TokenPresent));

        // Any host in process; a token sent along is answered and logged.
        using var withToken = new HttpRequestMessage(HttpMethod.Get, new Uri("https://avatars.example.com/avatars/u/1001"));
        withToken.Headers.TryAddWithoutValidation("Authorization", "Bearer " + scenario.Served.OctoToken);
        using var answered = await client.SendAsync(withToken, Cancelled);
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        Assert.Equal(("avatars.example.com", true, (string?)null), (fake.Requests[^1].Host, fake.Requests[^1].TokenPresent, fake.Requests[^1].Login));
        using var unknown = await client.GetAsync(new Uri(fake.Endpoints.WebBase, "avatars/u/9999"), Cancelled);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        foreach (var (path, title) in new[] { ("octo/hello/pull/1", "octo/hello #1"), ("octo/hello", "octo/hello"), ("octo/hello/pulls", "octo/hello pull requests"), ("settings/tokens", "Settings") })
        {
            using var page = await client.GetAsync(new Uri(fake.Endpoints.WebBase, path), Cancelled);
            Assert.Equal((HttpStatusCode.OK, "text/html"), (page.StatusCode, page.Content.Headers.ContentType?.MediaType));
            Assert.Equal($"<!doctype html><title>{title}</title>", await page.Content.ReadAsStringAsync(Cancelled));
        }

        Assert.Equal("page:octo/hello", fake.Requests[^2].Route);

        // In process git's paths are no route's.
        using var git = await client.GetAsync(new Uri(fake.Endpoints.WebBase, "octo/hello.git/info/refs?service=git-upload-pack"), Cancelled);
        Assert.Equal((HttpStatusCode.NotFound, (string?)null), (git.StatusCode, fake.Requests[^1].Route));
        Assert.Empty(fake.GitRequests);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Each_fault_is_answered_once_then_the_route_is_normal()
    {
        await using var scenario = await ScenarioAsync();
        var fake = scenario.Served.GitHub;
        var token = scenario.Served.OctoToken;
        var api = fake.Endpoints.ApiBase.AbsoluteUri;

        await FaultOnceAsync(fake, "user", FakeGitHubFault.BadCredentials, token, answer => Assert.Equal((HttpStatusCode.Unauthorized, "Bad credentials"), (answer.Status, answer.Text("message"))));
        await FaultOnceAsync(fake, "user", FakeGitHubFault.PrimaryRateLimit, token, answer =>
            Assert.Equal((HttpStatusCode.Forbidden, "API rate limit exceeded for user ID 1001.", "0"), (answer.Status, answer.Text("message"), answer.Header("X-RateLimit-Remaining"))));
        await FaultOnceAsync(fake, "user", FakeGitHubFault.SecondaryRateLimit, token, answer =>
            Assert.Equal((HttpStatusCode.Forbidden, "You have exceeded a secondary rate limit. Please wait a few minutes before you try again.", "60"), (answer.Status, answer.Text("message"), answer.Header("Retry-After"))));
        await FaultOnceAsync(fake, "user/repos", FakeGitHubFault.SsoRequired, token, answer =>
            Assert.Equal((HttpStatusCode.Forbidden, "Resource protected by organization SAML enforcement.", $"required; url={fake.Endpoints.WebBase.AbsoluteUri}orgs/acme/sso?authorization_request=FAKE"), (answer.Status, answer.Text("message"), answer.Header("X-GitHub-SSO"))));
        await FaultOnceAsync(fake, "user/repos", FakeGitHubFault.SsoPartialResults, token, answer =>
            Assert.Equal((HttpStatusCode.OK, 4, "partial-results; organizations=1002"), (answer.Status, answer.Json.GetArrayLength(), answer.Header("X-GitHub-SSO"))));
        foreach (var (route, path, permission) in new[] { ("repo:octo/hello", "repos/octo/hello", "metadata=read"), ("pulls:octo/hello", "repos/octo/hello/pulls", "pull_requests=read"), ("commits:octo/hello", "repos/octo/hello/commits", "contents=read") })
        {
            await FaultOnceAsync(fake, route, FakeGitHubFault.Forbidden, token, answer =>
                Assert.Equal((HttpStatusCode.Forbidden, "Resource not accessible by personal access token", permission), (answer.Status, answer.Text("message"), answer.Header("X-Accepted-GitHub-Permissions"))), path);
        }

        await FaultOnceAsync(fake, "user", FakeGitHubFault.NotFound, token, answer => Assert.Equal((HttpStatusCode.NotFound, "Not Found"), (answer.Status, answer.Text("message"))));
        await FaultOnceAsync(fake, "user", FakeGitHubFault.ApiVersionGone, token, answer => Assert.Equal(HttpStatusCode.Gone, answer.Status));
        await FaultOnceAsync(fake, "user", FakeGitHubFault.ServerError, token, answer => Assert.Equal((HttpStatusCode.InternalServerError, "Server Error"), (answer.Status, answer.Text("message"))));
        await FaultOnceAsync(fake, "user", FakeGitHubFault.BadGateway, token, answer => Assert.Equal((HttpStatusCode.BadGateway, "text/html; charset=utf-8"), (answer.Status, answer.Header("Content-Type"))));
        await FaultOnceAsync(fake, "user", FakeGitHubFault.Malformed, token, answer => Assert.Equal((HttpStatusCode.OK, "{\"login\":"), (answer.Status, answer.Body)));
        await FaultOnceAsync(fake, "repo:octo/hello", FakeGitHubFault.Moved, token, answer =>
            Assert.Equal((HttpStatusCode.MovedPermanently, api + "repositories/3001"), (answer.Status, answer.Header("Location"))), "repos/octo/hello");
        await FaultOnceAsync(fake, "commits:octo/hello", FakeGitHubFault.Moved, token, answer =>
            Assert.Equal(api + "repositories/3001/commits?per_page=1", answer.Header("Location")), "repos/octo/hello/commits?per_page=1");
        var elsewhere = new Uri("http://127.0.0.1:1/api/v3/repos/octo/renamed");
        await FaultOnceAsync(fake, "repo:octo/hello", FakeGitHubFault.MovedTo(elsewhere), token, answer => Assert.Equal(elsewhere.AbsoluteUri, answer.Header("Location")), "repos/octo/hello");

        // The lost POST: the pull request is made, the answer never comes.
        fake.FailNext("create:octo/hello", FakeGitHubFault.CreatedThenDropped);
        await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync(fake, HttpMethod.Post, "repos/octo/hello/pulls", token, """{"title":"Lost","head":"topic/no-pr","base":"main"}"""));
        Assert.Equal("Lost", fake.PullRequests("octo/hello").Single(pull => pull.Number == 6).Title);
        await AssertInvalidAsync(fake, token, """{"title":"Again","head":"topic/no-pr","base":"main"}""", """{"resource":"PullRequest","code":"custom","message":"A pull request already exists for octo:topic/no-pr."}""");

        Assert.Throws<NotSupportedException>(() => fake.FailNext("git:octo/hello", FakeGitHubFault.ServerError));
        Assert.Throws<NotSupportedException>(() => fake.FailNext("avatar:1001", FakeGitHubFault.ServerError));
        Assert.Throws<NotSupportedException>(() => fake.FailNext("page:octo/hello", FakeGitHubFault.ServerError));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_renamed_repository_answers_under_its_new_name_and_redirects_every_route_of_its_old_one()
    {
        await using var scenario = await ScenarioAsync();
        var fake = scenario.Served.GitHub;
        var token = scenario.Served.OctoToken;
        var api = fake.Endpoints.ApiBase.AbsoluteUri;
        var id = fake.Repository("octo/hello").Id;
        fake.Rename("octo/hello", "greeting");

        var renamed = await SendAsync(fake, HttpMethod.Get, "repos/octo/greeting", token);
        Assert.Equal((HttpStatusCode.OK, "greeting", "octo/greeting", fake.Endpoints.WebBase.AbsoluteUri + "octo/greeting.git"),
            (renamed.Status, renamed.Text("name"), renamed.Text("full_name"), renamed.Text("clone_url")));
        var byId = await SendAsync(fake, HttpMethod.Get, $"repositories/{id}/pulls", token);
        Assert.Equal(new[] { 5, 3, 2, 1 }, Numbers(byId));
        Assert.Equal(("octo/greeting", fake.Endpoints.WebBase.AbsoluteUri + "octo/greeting/pull/5"),
            (byId.Json[0].GetProperty("base").GetProperty("repo").GetProperty("full_name").GetString(), byId.Json[0].GetProperty("html_url").GetString()));

        // Every route of the old name goes to the id, the rest of the path and the query kept: a
        // 301, and a 307 for a request with a body (GitHub's redirect of a renamed repository).
        foreach (var (method, path, body, status, location) in new[]
        {
            (HttpMethod.Get, "repos/octo/hello", (string?)null, HttpStatusCode.MovedPermanently, $"repositories/{id}"),
            (HttpMethod.Get, "repos/octo/hello/pulls?state=open&per_page=100", null, HttpStatusCode.MovedPermanently, $"repositories/{id}/pulls?state=open&per_page=100"),
            (HttpMethod.Get, "repos/octo/hello/commits?per_page=1", null, HttpStatusCode.MovedPermanently, $"repositories/{id}/commits?per_page=1"),
            (HttpMethod.Post, "repos/octo/hello/pulls", """{"title":"T","head":"topic/no-pr","base":"main"}""", HttpStatusCode.TemporaryRedirect, $"repositories/{id}/pulls"),
            (HttpMethod.Patch, "repos/octo/hello/pulls/3", """{"state":"closed"}""", HttpStatusCode.TemporaryRedirect, $"repositories/{id}/pulls/3"),
        })
        {
            var moved = await SendAsync(fake, method, path, token, body);
            Assert.Equal((status, api + location), (moved.Status, moved.Header("Location")));
        }

        // Nothing was made or changed through the old name, and the routes' keys are the new name's.
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, fake.PullRequests("octo/greeting").Select(pull => pull.Number));
        Assert.Equal("open", fake.PullRequests("octo/hello").Single(pull => pull.Number == 3).State);
        Assert.All(fake.Requests, request => Assert.EndsWith(":octo/greeting", request.Route, StringComparison.Ordinal));
        Assert.Equal(("octo/hello", "octo/greeting"), (fake.Repository("octo/greeting").FullName, fake.Repository("octo/hello").CurrentFullName));
        Assert.Throws<ArgumentException>(() => fake.Rename("octo/tool", "greeting"));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_held_route_waits_until_the_hold_is_disposed()
    {
        await using var scenario = await ScenarioAsync();
        var fake = scenario.Served.GitHub;

        var hold = fake.HoldNext("Pulls:Octo/Hello");
        Assert.Equal("pulls:octo/hello", hold.Route);
        Assert.Throws<InvalidOperationException>(() => fake.HoldNext("pulls:octo/hello"));
        var list = SendAsync(fake, HttpMethod.Get, "repos/octo/hello/pulls", scenario.Served.OctoToken);
        await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);

        // Other routes are served meanwhile; the held one is not recorded until it goes on.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(fake, HttpMethod.Get, "user", scenario.Served.OctoToken)).Status);
        await Task.Delay(200, Cancelled);
        Assert.False(list.IsCompleted, "The held request was answered while it was held.");
        Assert.DoesNotContain(fake.Requests, request => request.Route == "pulls:octo/hello");

        hold.Dispose();
        Assert.Equal(HttpStatusCode.OK, (await list.WaitAsync(TimeSpan.FromSeconds(60), Cancelled)).Status);

        // A hold disposed before any request came is dropped; a git hold needs a server.
        var dropped = fake.HoldNext("user");
        dropped.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dropped.Arrived);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(fake, HttpMethod.Get, "user", scenario.Served.OctoToken).WaitAsync(TimeSpan.FromSeconds(60), Cancelled)).Status);
        Assert.Throws<InvalidOperationException>(() => fake.HoldNext("git:octo/hello"));
        Assert.Empty(fake.Errors);
    }

    // ---- Helpers -----------------------------------------------------------------------------------

    private static async Task<Scenario> ScenarioAsync()
    {
        var hello = await Scenarios.HostingAsync();
        try
        {
            return new Scenario(hello, await Scenarios.ServeHostingAsync(hello, inProcess: true, ct: Cancelled));
        }
        catch
        {
            hello.Dispose();
            throw;
        }
    }

    /// <summary>Sends one API request (relative to the API's address) the way GitHub's client does: a User-Agent, the media type, the pinned version and, with a token, Bearer.</summary>
    private static async Task<Answer> SendAsync(FakeGitHub fake, HttpMethod method, string relative, string? token, string? json = null, Action<HttpRequestMessage>? edit = null)
    {
        using var client = new HttpClient(fake.CreateHandler());
        using var request = new HttpRequestMessage(method, new Uri(fake.Endpoints.ApiBase, relative));
        request.Headers.UserAgent.ParseAdd("VisualCommit-tests");
        request.Headers.Accept.ParseAdd(GitHubApi.MediaType);
        request.Headers.Add("X-GitHub-Api-Version", GitHubApi.Version);
        if (token is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        }

        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        edit?.Invoke(request);
        using var response = await client.SendAsync(request, Cancelled);
        var body = await response.Content.ReadAsStringAsync(Cancelled);
        var headers = response.Headers.Concat(response.Content.Headers).ToDictionary(header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase);
        return new Answer(response.StatusCode, body, headers);
    }

    private static async Task AssertErrorAsync(FakeGitHub fake, HttpMethod method, string relative, int status, string message, string? token, string? json = null, Action<HttpRequestMessage>? edit = null)
    {
        var answer = await SendAsync(fake, method, relative, token, json, edit);
        Assert.Equal(((HttpStatusCode)status, message, "https://docs.github.com/rest", status.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            (answer.Status, answer.Text("message"), answer.Text("documentation_url"), answer.Text("status")));
    }

    private static async Task AssertInvalidAsync(FakeGitHub fake, string token, string json, string error, string repository = "octo/hello")
    {
        var answer = await SendAsync(fake, HttpMethod.Post, $"repos/{repository}/pulls", token, json);
        Assert.Equal((HttpStatusCode.UnprocessableContent, "Validation Failed"), (answer.Status, answer.Text("message")));
        Assert.Equal(error, answer.Json.GetProperty("errors")[0].GetRawText());
    }

    private static async Task FaultOnceAsync(FakeGitHub fake, string route, FakeGitHubFault fault, string token, Action<Answer> check, string? relative = null)
    {
        relative ??= route.Replace(":", "s/", StringComparison.Ordinal);
        fake.FailNext(route, fault);
        var faulted = await SendAsync(fake, HttpMethod.Get, relative, token);
        check(faulted);
        Assert.Equal(route, fake.Requests[^1].Route);
        Assert.True((await SendAsync(fake, HttpMethod.Get, relative, token)).Status is HttpStatusCode.OK, $"{route} after {fault}");
    }

    private static string[] Names(Answer answer) => [.. answer.Json.EnumerateArray().Select(repository => repository.GetProperty("full_name").GetString()!)];

    private static int[] Numbers(Answer answer) => [.. answer.Json.EnumerateArray().Select(pull => pull.GetProperty("number").GetInt32())];

    private static string[] Commits(Answer answer) =>
    [
        .. answer.Json.EnumerateArray().Select(commit =>
            $"{commit.GetProperty("commit").GetProperty("message").GetString()} {commit.GetProperty("commit").GetProperty("author").GetProperty("email").GetString()} " +
            (commit.GetProperty("author").ValueKind == JsonValueKind.Null ? "-" : commit.GetProperty("author").GetProperty("login").GetString())),
    ];

    private static async Task<string> RevParseAsync(TempRepo repo, string revision) => (await repo.GitAsync("rev-parse", revision)).StandardOutput.Trim();

    private static async Task<string> SubjectAsync(TempRepo repo, string sha) =>
        (await repo.GitAsync("--git-dir", Path.Combine(repo.SiblingPath(Scenarios.HostingFolder), "acme", "tool.git"), "log", "-1", "--format=%s", sha)).StandardOutput.Trim();

    private static async Task<string?> RefAsync(TempRepo repo, string bare, string name)
    {
        var result = await repo.GitAllowingFailureAsync("--git-dir", bare, "rev-parse", "--verify", "--quiet", name);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    private sealed record Scenario(TempRepo Hello, ServedHosting Served) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Served.DisposeAsync();
            Hello.Dispose();
        }
    }

    private sealed record Answer(HttpStatusCode Status, string Body, IReadOnlyDictionary<string, string> Headers)
    {
        public JsonElement Json => JsonDocument.Parse(Body).RootElement;

        public string? Text(string name) => Json.GetProperty(name).GetString();

        public string? Header(string name) => Headers.GetValueOrDefault(name);
    }
}
