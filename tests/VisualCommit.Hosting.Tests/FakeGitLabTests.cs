using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using VisualCommit.Core;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The fake of GitLab in process (docs/phase-7b-design.md 4.6): every route, check, 4.4 item and
/// fault once, a fault then the normal answer; <c>PRIVATE-TOKEN</c> and <c>?private_token=</c> 401;
/// membership through a group three levels up; 1,001 listed projects to their last page; a project's
/// <c>:id</c> six ways; "404 Not found" with its lower-case <c>f</c>; create's refusals in order, three
/// draft prefixes, a fork's create; DELETE 204, 403, then 404; a counted 304; no request record holds a token.
/// </summary>
public sealed class FakeGitLabTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task User_and_self_answer_the_tokens_user_and_token_with_GitLabs_headers()
    {
        await using var lab = await Lab.CreateAsync();

        var user = await lab.SendAsync(HttpMethod.Get, "api/v4/user", lab.Ada);
        Assert.Equal(200, user.Status);
        Assert.Equal("application/json", user.Header("Content-Type"));
        Assert.Equal(("ada", 2001L, "Ada Lovelace", "ada@example.com"), ((string)user.Json["username"]!, (long)user.Json["id"]!, (string)user.Json["name"]!, (string)user.Json["email"]!));
        Assert.Equal(lab.Fake.Endpoints.WebBase.AbsoluteUri + "ada", (string)user.Json["web_url"]!);
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$", (string)user.Json["created_at"]!);
        Assert.Equal(("burst_traffic_per_user_free_plan", "100"), (user.Header("ratelimit-name"), user.Header("ratelimit-limit")));

        // GET user's 41 members in gitlab.com's order (MEASURED 2026-11-06).
        Assert.Equal(
            ["id", "username", "public_email", "name", "state", "locked", "avatar_url", "web_url", "created_at", "bio", "location", "linkedin", "twitter", "discord", "website_url", "github", "job_title", "pronouns", "organization", "bot", "work_information", "local_time", "last_sign_in_at", "confirmed_at", "last_activity_on", "email", "theme_id", "color_scheme_id", "projects_limit", "current_sign_in_at", "identities", "can_create_group", "can_create_project", "two_factor_enabled", "external", "private_profile", "commit_email", "preferred_language", "shared_runners_minutes_limit", "extra_shared_runners_minutes_limit", "scim_identities"],
            user.Json.AsObject().Select(property => property.Key));
        Assert.StartsWith("FAKE-GL-", user.Header("x-request-id"), StringComparison.Ordinal);
        Assert.Contains(user.Header("x-request-id")!, user.Header("x-gitlab-meta"), StringComparison.Ordinal);
        Assert.StartsWith("W/\"", user.Header("ETag"), StringComparison.Ordinal);
        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/user", lab.AdaLegacy)).Status);

        var legacySelf = await lab.SendAsync(HttpMethod.Get, "api/v4/personal_access_tokens/self", lab.AdaLegacy);
        Assert.Equal(200, legacySelf.Status);
        Assert.Equal("[\"api\"]", legacySelf.Json["scopes"]!.ToJsonString());
        Assert.Equal((false, true, 2001L), ((bool)legacySelf.Json["granular"]!, (bool)legacySelf.Json["active"]!, (long)legacySelf.Json["user_id"]!));

        // D364's token has no Personal Access Token: Read: self is check 5's 403 (4.7).
        var granularSelf = await lab.SendAsync(HttpMethod.Get, "api/v4/personal_access_tokens/self", lab.Ada);
        Assert.Equal(403, granularSelf.Status);
        Assert.Equal("{\"error\":\"insufficient_granular_scope\",\"error_description\":\"Access denied: This operation requires a fine-grained personal access token with the following user permissions: [Personal Access Token: Read].\"}", granularSelf.Body);
        Assert.Null(granularSelf.Header("WWW-Authenticate"));

        var reader = lab.Fake.AddToken("ada", permissions: [FakeGitLabPermissions.PersonalAccessTokenRead, FakeGitLabPermissions.ProjectRead], expires: new DateOnly(2026, 2, 1));
        var self = await lab.SendAsync(HttpMethod.Get, "api/v4/personal_access_tokens/self", reader);
        Assert.Equal(200, self.Status);
        Assert.Equal(("[\"granular\"]", true, "2026-02-01"), (self.Json["scopes"]!.ToJsonString(), (bool)self.Json["granular"]!, (string)self.Json["expires_at"]!));
        Assert.Equal("[{\"access\":\"user\",\"permissions\":[\"read_personal_access_token\"],\"project_id\":null,\"group_id\":null},{\"access\":\"all_memberships\",\"permissions\":[\"read_project\"],\"project_id\":null,\"group_id\":null}]", self.Json["granular_scopes"]!.ToJsonString());
        lab.AssertNoToken();
    }

    [Fact]
    public async Task No_token_PRIVATE_TOKEN_private_token_and_Basic_are_401_but_the_list_and_a_projects_reads_go_on_to_200_or_404()
    {
        await using var lab = await Lab.CreateAsync();
        const string Unauthorized = "{\"message\":\"401 Unauthorized\"}";

        var none = await lab.SendAsync(HttpMethod.Get, "api/v4/user");
        Assert.Equal((401, Unauthorized), (none.Status, none.Body));
        Assert.Null(none.Header("WWW-Authenticate"));
        Assert.Equal(("throttle_unauthenticated_api", "500", "no-store, private, max-age=0"), (none.Header("ratelimit-name"), none.Header("ratelimit-limit"), none.Header("Cache-Control")));

        // D370: gitlab.com takes PRIVATE-TOKEN and private_token; the fake takes Bearer alone.
        var privateToken = await lab.SendAsync(HttpMethod.Get, "api/v4/user", headers: [("PRIVATE-TOKEN", lab.Ada)]);
        var query = await lab.SendAsync(HttpMethod.Get, "api/v4/user?private_token=" + lab.Ada);
        var basic = await lab.SendAsync(HttpMethod.Get, "api/v4/user", headers: [("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("ada:" + lab.Ada)))]);
        Assert.All(new[] { privateToken, query, basic }, answer => Assert.Equal((401, Unauthorized), (answer.Status, answer.Body)));
        Assert.Equal(
            [FakeAuthorizationForm.None, FakeAuthorizationForm.PrivateToken, FakeAuthorizationForm.Query, FakeAuthorizationForm.Basic],
            lab.Fake.Requests.Select(request => request.Authorization));
        Assert.Equal("private_token=***", lab.Fake.Requests[2].Query);

        var membership = await lab.SendAsync(HttpMethod.Get, "api/v4/projects?membership=true");
        Assert.Equal((200, "[]"), (membership.Status, membership.Body));
        var listed = await lab.SendAsync(HttpMethod.Get, "api/v4/projects");
        Assert.Equal(["pub/open"], listed.Json.AsArray().Select(row => (string)row!["path_with_namespace"]!));
        Assert.Equal(20, listed.Json[0]!.AsObject().Count);
        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/projects/pub%2Fopen")).Status);
        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/projects/pub%2Fopen/merge_requests")).Status);
        // A project's reads go on without a token: a private, unknown, .git or double-escaped one is 404
        // "404 Project Not Found", as gitlab.com answered anonymously (MEASURED r6 3.2); writes are 401.
        const string ProjectNotFound = "{\"message\":\"404 Project Not Found\"}";
        foreach (var id in new[] { "g%2Fs%2Fp", "g%2Fs%2Fp/merge_requests", "g%2Fs%2Fp/merge_requests/1", "g%2Fs%2Fp/repository/branches", "99999", "pub%2Fopen.git", "pub%252Fopen" })
        {
            Assert.Equal((404, ProjectNotFound), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects/" + id));
        }

        Assert.Equal((401, Unauthorized), await lab.StatusAndBodyAsync(HttpMethod.Post, "api/v4/projects/pub%2Fopen/merge_requests"));
        Assert.Equal((401, Unauthorized), await lab.StatusAndBodyAsync(HttpMethod.Put, "api/v4/projects/pub%2Fopen/merge_requests/1"));
        Assert.Equal((401, Unauthorized), await lab.StatusAndBodyAsync(HttpMethod.Delete, "api/v4/projects/pub%2Fopen/repository/branches/fix"));
        Assert.Equal((401, Unauthorized), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/personal_access_tokens/self"));
        lab.AssertNoToken();
    }

    [Fact]
    public async Task Unknown_revoked_and_expired_tokens_get_GitLabs_401s()
    {
        await using var lab = await Lab.CreateAsync();
        const string Expired = "Token is expired. You can either do re-authorization or token refresh.";
        const string Revoked = "Token was revoked. You have to re-authorize from the user.";

        Assert.Equal((401, "{\"message\":\"401 Unauthorized\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/user", "glpat-FAKE-nobody-1"));

        lab.Fake.Revoke(lab.Octo);
        var revoked = await lab.SendAsync(HttpMethod.Get, "api/v4/user", lab.Octo);
        Assert.Equal((401, $"{{\"error\":\"invalid_token\",\"error_description\":\"{Revoked}\"}}"), (revoked.Status, revoked.Body));
        Assert.Equal($"Bearer realm=\"Protected by OAuth 2.0\", error=\"invalid_token\", error_description=\"{Revoked}\"", revoked.Header("WWW-Authenticate"));

        lab.Fake.Expire(lab.Bob);
        var expired = await lab.SendAsync(HttpMethod.Get, "api/v4/projects", lab.Bob);
        Assert.Equal((401, $"{{\"error\":\"invalid_token\",\"error_description\":\"{Expired}\"}}"), (expired.Status, expired.Body));
        Assert.Equal($"Bearer realm=\"Protected by OAuth 2.0\", error=\"invalid_token\", error_description=\"{Expired}\"", expired.Header("WWW-Authenticate"));

        // A token is unusable from its expiry date (ASSUMED r6 2.4), read on the harness's clock.
        var dated = lab.Fake.AddToken("ada", expires: DateOnly.FromDateTime(AppClocks.HarnessTime.UtcDateTime));
        Assert.Equal(401, (await lab.SendAsync(HttpMethod.Get, "api/v4/user", dated)).Status);
        var later = lab.Fake.AddToken("ada", expires: DateOnly.FromDateTime(AppClocks.HarnessTime.UtcDateTime).AddDays(1));
        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/user", later)).Status);
        Assert.All(lab.Fake.Requests.Where(request => request.Status == 401), request => Assert.Null(request.Login));
        lab.AssertNoToken();
    }

    [Fact]
    public async Task A_legacy_token_without_the_routes_scope_is_403_insufficient_scope_in_body_and_header()
    {
        await using var lab = await Lab.CreateAsync();
        const string Description = "The request requires higher privileges than provided by the access token.";
        var readUser = lab.Fake.AddToken("ada", FakeGitLabTokenKind.Legacy, [FakeGitLabPermissions.ReadUser]);
        var readApi = lab.Fake.AddToken("ada", FakeGitLabTokenKind.Legacy, [FakeGitLabPermissions.ReadApi]);

        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/user", readUser)).Status);
        var list = await lab.SendAsync(HttpMethod.Get, "api/v4/projects?membership=true", readUser);
        Assert.Equal((403, $"{{\"error\":\"insufficient_scope\",\"error_description\":\"{Description}\",\"scope\":\"api read_api\"}}"), (list.Status, list.Body));
        Assert.Equal($"Bearer realm=\"Protected by OAuth 2.0\", error=\"insufficient_scope\", error_description=\"{Description}\", scope=\"api read_api\"", list.Header("WWW-Authenticate"));

        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests", readApi)).Status);
        var create = await lab.SendAsync(HttpMethod.Post, "api/v4/projects/g%2Fs%2Fp/merge_requests", readApi, form: [("source_branch", "feature/two"), ("target_branch", "main"), ("title", "Two")]);
        Assert.Equal(403, create.Status);
        Assert.Contains("api", ((string)create.Json["scope"]!).Split(' '));
        Assert.Contains("scope=\"" + (string)create.Json["scope"]! + "\"", create.Header("WWW-Authenticate"), StringComparison.Ordinal);
        Assert.DoesNotContain(lab.Fake.MergeRequests(lab.Project), request => request.SourceBranch == "feature/two");
        lab.AssertNoToken();
    }

    [Fact]
    public async Task A_fine_grained_token_without_the_routes_permission_is_403_but_a_public_projects_reads_pass_and_a_hidden_project_404s()
    {
        await using var lab = await Lab.CreateAsync();
        var noUser = lab.Fake.AddToken("ada", permissions: [FakeGitLabPermissions.ProjectRead]);
        var noProject = lab.Fake.AddToken("ada", permissions: [FakeGitLabPermissions.UserRead]);
        var bobNoProject = lab.Fake.AddToken("bob", permissions: [FakeGitLabPermissions.UserRead]);
        const string Prefix = "Access denied: This operation requires a fine-grained personal access token with the following ";

        var user = await lab.SendAsync(HttpMethod.Get, "api/v4/user", noUser);
        Assert.Equal((403, "insufficient_granular_scope", Prefix + "user permissions: [User: Read]."), (user.Status, (string)user.Json["error"]!, (string)user.Json["error_description"]!));
        Assert.Null(user.Header("WWW-Authenticate"));
        Assert.Equal(Prefix + "user permissions: [Project: Read].", (string)(await lab.SendAsync(HttpMethod.Get, "api/v4/projects", noProject)).Json["error_description"]!);

        // D385: Project: Read among the group and project permissions alone does not list the
        // projects: 403 naming the user permission (MEASURED 2026-11-05), while a project's read
        // passes; among the user permissions alone it lists them, and a project's read is the project
        // permission's 403 (ASSUMED).
        var projectSection = lab.Fake.AddToken("ada", permissions: [FakeGitLabPermissions.UserRead, FakeGitLabPermissions.ProjectRead]);
        var listed = await lab.SendAsync(HttpMethod.Get, "api/v4/projects?membership=true", projectSection);
        Assert.Equal((403, "{\"error\":\"insufficient_granular_scope\",\"error_description\":\"Access denied: This operation requires a fine-grained personal access token with the following user permissions: [Project: Read].\"}"), (listed.Status, listed.Body));
        Assert.Equal(("application/json", null), (listed.Header("Content-Type"), listed.Header("WWW-Authenticate")));
        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp", projectSection)).Status);
        var userSection = lab.Fake.AddToken("ada", permissions: [FakeGitLabPermissions.UserRead, FakeGitLabPermissions.UserProjectRead]);
        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/projects?membership=true", userSection)).Status);
        var project = await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp", userSection);
        Assert.Equal((403, Prefix + "project permissions: [Project: Read]."), (project.Status, (string)project.Json["error_description"]!));

        var requests = await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests", noProject);
        Assert.Equal((403, Prefix + "project permissions: [Merge Request: Read]."), (requests.Status, (string)requests.Json["error_description"]!));
        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/projects/pub%2Fopen/merge_requests", noProject)).Status);
        Assert.Equal(403, (await lab.SendAsync(HttpMethod.Post, "api/v4/projects/pub%2Fopen/merge_requests", noProject, form: [("source_branch", "fix"), ("target_branch", "main"), ("title", "Fix")])).Status);

        // octo/secret is hidden from bob: 404 "404 Not Found", not the permission's 403.
        Assert.Equal((404, "{\"message\":\"404 Not Found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects/octo%2Fsecret", bobNoProject));
        Assert.Equal((404, "{\"message\":\"404 Project Not Found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects/octo%2Fsecret", lab.Bob));
        lab.AssertNoToken();
    }

    [Fact]
    public async Task Membership_reaches_a_project_through_a_group_three_levels_up_and_the_list_filters_and_sorts()
    {
        await using var lab = await Lab.CreateAsync();
        lab.Fork.IsArchived = true;
        lab.Project.LastActivityAt = AppClocks.HarnessTime;

        var mine = await lab.SendAsync(HttpMethod.Get, "api/v4/projects?membership=true&order_by=last_activity_at&sort=desc", lab.Ada);
        Assert.Equal(["g/s/p", "g/s/t/deep", "ada/p"], Paths(mine));
        var full = mine.Json[0]!.AsObject();
        Assert.Equal("{\"project_access\":null,\"group_access\":{\"access_level\":30,\"notification_level\":3}}", full["permissions"]!.ToJsonString());
        Assert.Equal((false, false, true, "main"), ((bool)full["archived"]!, (bool)full["empty_repo"]!, (bool)full["merge_requests_enabled"]!, (string)full["default_branch"]!));
        Assert.Equal(("group", "g/s", "g / s / p"), ((string)full["namespace"]!["kind"]!, (string)full["namespace"]!["full_path"]!, (string)full["name_with_namespace"]!));
        Assert.Equal((lab.Fake.Endpoints.WebBase.AbsoluteUri + "g/s/p.git", $"git@127.0.0.1:g/s/p.git"), ((string)full["http_url_to_repo"]!, (string)full["ssh_url_to_repo"]!));
        var deep = mine.Json[1]!.AsObject();
        Assert.Equal((true, "main"), ((bool)deep["empty_repo"]!, (string)deep["default_branch"]!));
        var fork = mine.Json[2]!.AsObject();
        Assert.Equal(("g/s/p", true, "user"), ((string)fork["forked_from_project"]!["path_with_namespace"]!, (bool)fork["archived"]!, (string)fork["namespace"]!["kind"]!));
        Assert.Equal("{\"project_access\":{\"access_level\":50,\"notification_level\":3},\"group_access\":null}", fork["permissions"]!.ToJsonString());
        Assert.Equal("ada", (string)fork["owner"]!["username"]!);

        Assert.Equal(["g/s/p", "g/s/t/deep"], Paths(await lab.SendAsync(HttpMethod.Get, "api/v4/projects?membership=true&archived=false&order_by=last_activity_at", lab.Ada)));
        Assert.Equal(["g/s/t/deep", "pub/open", "g/s/p", "ada/p"], Paths(await lab.SendAsync(HttpMethod.Get, "api/v4/projects?order_by=path&sort=asc", lab.Ada)));
        var simple = await lab.SendAsync(HttpMethod.Get, "api/v4/projects?membership=true&simple=true", lab.Ada);
        Assert.Equal(
            ["id", "description", "name", "name_with_namespace", "path", "path_with_namespace", "created_at", "default_branch", "tag_list", "topics", "ssh_url_to_repo", "http_url_to_repo", "web_url", "readme_url", "forks_count", "avatar_url", "star_count", "last_activity_at", "visibility", "namespace"],
            simple.Json[0]!.AsObject().Select(member => member.Key));
        Assert.Equal((400, "{\"error\":\"order_by does not have a valid value\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects?order_by=size", lab.Ada));

        // octo, Owner of g/s/p by a grant on it, and internal projects for any signed-in user.
        lab.Fake.Project("octo/secret").Visibility = "internal";
        Assert.Contains("octo/secret", Paths(await lab.SendAsync(HttpMethod.Get, "api/v4/projects", lab.Bob)));
        Assert.DoesNotContain("octo/secret", Paths(await lab.SendAsync(HttpMethod.Get, "api/v4/projects")));
        lab.AssertNoToken();
    }

    [Fact]
    public async Task Listed_projects_page_to_the_last_with_GitLabs_headers()
    {
        await using var lab = await Lab.CreateAsync();
        lab.Fake.AddListedProjects("g/many", 1001);
        Assert.Equal("g/many/listed-1001", lab.Fake.Project("g/many/listed-1001").Path);

        var first = await lab.SendAsync(HttpMethod.Get, "api/v4/projects?membership=true", lab.Ada);
        Assert.Equal(("20", "1", "2", string.Empty, "1004", "51"), (first.Header("x-per-page"), first.Header("x-page"), first.Header("x-next-page"), first.Header("x-prev-page"), first.Header("x-total"), first.Header("x-total-pages")));
        Assert.Equal(20, first.Json.AsArray().Count);

        var rows = 0;
        var pages = 0;
        var next = "api/v4/projects?membership=true&per_page=500";
        Answer page;
        do
        {
            page = await lab.SendAsync(HttpMethod.Get, next, lab.Ada);
            Assert.Equal("100", page.Header("x-per-page"));
            rows += page.Json.AsArray().Count;
            pages++;
            next = NextOf(page.Header("Link")!) is { } link ? link[lab.Fake.Endpoints.WebBase.AbsoluteUri.Length..] : string.Empty;
        }
        while (next.Length > 0);

        Assert.Equal((1004, 11), (rows, pages));
        Assert.Equal((string.Empty, "10", "11"), (page.Header("x-next-page"), page.Header("x-prev-page"), page.Header("x-page")));
        Assert.Contains("rel=\"last\"", page.Header("Link"), StringComparison.Ordinal);
        Assert.Equal(4, page.Json.AsArray().Count);

        var past = await lab.SendAsync(HttpMethod.Get, "api/v4/projects?membership=true&per_page=100&page=12", lab.Ada);
        Assert.Equal((200, "[]", "12"), (past.Status, past.Body, past.Header("x-page")));

        lab.Fake.MaxPerPage = 2;
        Assert.Equal("2", (await lab.SendAsync(HttpMethod.Get, "api/v4/projects?membership=true&per_page=100", lab.Ada)).Header("x-per-page"));
        lab.AssertNoToken();
    }

    [Fact]
    public async Task A_projects_id_is_read_six_ways()
    {
        await using var lab = await Lab.CreateAsync();
        var id = lab.Project.Id.ToString(CultureInfo.InvariantCulture);

        foreach (var form in new[] { "g%2Fs%2Fp", "g%2fs%2fp", "G%2FS%2FP", id, "g%2Fs%2Fp/" })
        {
            var answer = await lab.SendAsync(HttpMethod.Get, "api/v4/projects/" + form, lab.Ada);
            Assert.Equal((200, "g/s/p"), (answer.Status, (string)answer.Json["path_with_namespace"]!));
        }

        Assert.Equal((404, "{\"message\":\"404 Project Not Found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp.git", lab.Ada));
        Assert.Equal((404, "{\"message\":\"404 Project Not Found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects/g%252Fs%252Fp", lab.Ada));
        Assert.Equal((404, "{\"error\":\"404 Not Found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects/g/s/p", lab.Ada));
        Assert.Equal((404, "{\"error\":\"404 Not Found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/nothing", lab.Ada));
        Assert.Equal((404, "{\"message\":\"404 Project Not Found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects/99999", lab.Ada));
        Assert.Equal(["project:g/s/p", "project:g/s/p", "project:g/s/p", "project:g/s/p", "project:g/s/p", "project:g/s/p.git", "project:g%2fs%2fp", null, null, "project:99999"], lab.Fake.Requests.Select(request => request.Route));
        Assert.Contains(lab.Fake.Requests, request => request.Path == "/api/v4/projects/g%2Fs%2Fp");
        lab.AssertNoToken();
    }

    [Fact]
    public async Task Merge_requests_are_listed_by_state_and_branch_newest_first_in_GitLabs_shape()
    {
        await using var lab = await Lab.CreateAsync();
        var closed = lab.Fake.AddMergeRequest(lab.Project, "Work on two", "octo", "feature/two", "main", description: "Closed already.");
        closed.State = "closed";
        lab.Fake.AddMergeRequest(lab.Project, "Fix it", "ada", "fix", "main", source: lab.Fork);

        var all = await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests", lab.Ada);
        Assert.Equal([3, 2, 1], all.Json.AsArray().Select(row => (int)row!["iid"]!));
        Assert.Equal([2], (await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests?state=closed", lab.Ada)).Json.AsArray().Select(row => (int)row!["iid"]!));
        Assert.Equal([3, 1], (await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests?state=opened&per_page=100", lab.Ada)).Json.AsArray().Select(row => (int)row!["iid"]!));
        Assert.Equal([3], (await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests?source_branch=fix&target_branch=main", lab.Ada)).Json.AsArray().Select(row => (int)row!["iid"]!));
        Assert.Equal((400, "{\"error\":\"state does not have a valid value\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests?state=open", lab.Ada));

        var one = all.Json[2]!.AsObject();
        Assert.Equal(await lab.TipAsync("feature/one"), (string)one["sha"]!);
        Assert.Equal(("!1", "g/s/p!1", lab.Fake.Endpoints.WebBase.AbsoluteUri + "g/s/p/-/merge_requests/1"), ((string)one["references"]!["short"]!, (string)one["references"]!["full"]!, (string)one["web_url"]!));
        Assert.Equal(("ada", "ada@example.com", false, false), ((string)one["author"]!["username"]!, (string)one["author"]!["public_email"]!, (bool)one["draft"]!, (bool)one["work_in_progress"]!));
        Assert.Equal((lab.Project.Id, lab.Project.Id, lab.Project.Id), ((long)one["project_id"]!, (long)one["source_project_id"]!, (long)one["target_project_id"]!));
        Assert.False(one.ContainsKey("allow_collaboration"));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$", (string)one["created_at"]!);
        var forked = all.Json[0]!.AsObject();
        Assert.Equal((lab.Fork.Id, lab.Project.Id, false), ((long)forked["source_project_id"]!, (long)forked["target_project_id"]!, (bool)forked["allow_collaboration"]!));
        Assert.DoesNotContain(forked, member => member.Key.Contains("source_project_path", StringComparison.Ordinal) || member.Key == "source_project");
        Assert.Equal(await lab.TipAsync("fix"), (string)forked["sha"]!);

        var single = await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests/1", lab.Ada);
        Assert.Equal((200, await lab.TipAsync("feature/one")), (single.Status, (string)single.Json["diff_refs"]!["head_sha"]!));
        Assert.Equal((404, "{\"message\":\"404 Not found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests/999", lab.Ada));
        Assert.Equal((400, "{\"error\":\"merge_request_iid is invalid\"}"), await lab.StatusAndBodyAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests/abc", lab.Ada));
        Assert.Contains(lab.Fake.Requests, request => request.Route == "mr:g/s/p/999");
        lab.AssertNoToken();
    }

    [Fact]
    public async Task Create_refuses_in_order_then_makes_merge_requests_with_draft_titles_from_a_fork_too_and_writes_their_head_refs()
    {
        await using var lab = await Lab.CreateAsync();
        const string Path = "api/v4/projects/g%2Fs%2Fp/merge_requests";
        var forkPath = $"api/v4/projects/{lab.Fork.Id}/merge_requests";

        const string Forbidden = "{\"message\":\"403 Forbidden\"}";
        const string SameBranch = "{\"message\":[\"You can't use same project/branch for source and target\"]}";

        // Grape names the missing parameters in the order the route declares them (lib/api/merge_requests.rb:423-425).
        Assert.Equal((400, "{\"error\":\"title is missing, source_branch is missing, target_branch is missing\"}"), (await lab.CreateAsync(Path, lab.Ada)).StatusAndBody);
        Assert.Equal((400, "{\"error\":\"title is missing, target_branch is missing\"}"), (await lab.CreateAsync(Path, lab.Ada, ("source_branch", "feature/two"))).StatusAndBody);
        Assert.Equal((400, "{\"error\":\"title is missing\"}"), (await lab.CreateAsync(Path, lab.Ada, ("source_branch", "feature/two"), ("target_branch", "main"))).StatusAndBody);

        // Where two refusals apply, the earlier one answers: (a) before (b), bob a Reporter with no title;
        // (b) before (c), bob with an unknown target.
        Assert.Equal((400, "{\"error\":\"title is missing\"}"), (await lab.CreateAsync(Path, lab.Bob, ("source_branch", "feature/two"), ("target_branch", "main"))).StatusAndBody);
        Assert.Equal((403, Forbidden), (await lab.CreateAsync(Path, lab.Bob, ("source_branch", "feature/two"), ("target_branch", "main"), ("title", "Two"))).StatusAndBody);
        Assert.Equal((403, Forbidden), (await lab.CreateAsync(Path, lab.Bob, ("source_branch", "feature/two"), ("target_branch", "main"), ("title", "Two"), ("target_project_id", "99999"))).StatusAndBody);
        lab.Project.MergeRequestsEnabled = false;
        Assert.Equal(403, (await lab.CreateAsync(Path, lab.Ada, ("source_branch", "feature/two"), ("target_branch", "main"), ("title", "Two"))).Status);
        lab.Project.MergeRequestsEnabled = true;
        Assert.Equal((404, "{\"message\":\"404 Not found\"}"), (await lab.CreateAsync(forkPath, lab.Ada, ("source_branch", "fix"), ("target_branch", "main"), ("title", "Fix"), ("target_project_id", "99999"))).StatusAndBody);
        Assert.Equal(403, (await lab.CreateAsync(forkPath, lab.Ada, ("source_branch", "fix"), ("target_branch", "main"), ("title", "Fix"), ("target_project_id", Id(lab.Secret)))).Status);
        Assert.Equal((422, SameBranch), (await lab.CreateAsync(Path, lab.Ada, ("source_branch", "main"), ("target_branch", "main"), ("title", "Same"))).StatusAndBody);

        // (e) before (g): a branch the project lacks, into itself.
        Assert.Equal((422, SameBranch), (await lab.CreateAsync(Path, lab.Ada, ("source_branch", "gone"), ("target_branch", "gone"), ("title", "Gone"))).StatusAndBody);
        Assert.Equal((422, "{\"message\":[\"Source project is not a fork of the target project\"]}"), (await lab.CreateAsync(forkPath, lab.Ada, ("source_branch", "fix"), ("target_branch", "main"), ("title", "Fix"), ("target_project_id", Id(lab.Public)))).StatusAndBody);
        Assert.Equal((409, "{\"message\":[\"Another open merge request already exists for this source branch: !1\"]}"), (await lab.CreateAsync(Path, lab.Ada, ("source_branch", "feature/one"), ("target_branch", "main"), ("title", "Again"))).StatusAndBody);
        Assert.Equal((400, "{\"message\":{\"source_branch\":[\"does not exist\"]}}"), (await lab.CreateAsync(Path, lab.Ada, ("source_branch", "gone"), ("target_branch", "main"), ("title", "Gone"))).StatusAndBody);
        Assert.Equal((400, "{\"message\":{\"source_branch\":[\"does not exist\"],\"target_branch\":[\"does not exist\"]}}"), (await lab.CreateAsync(Path, lab.Ada, ("source_branch", "gone"), ("target_branch", "away"), ("title", "Gone"))).StatusAndBody);
        Assert.Single(lab.Fake.MergeRequests(lab.Project));

        // Three draft prefixes, one in JSON; "level" has nothing ahead of main (READ g2 G1.4).
        var two = await lab.CreateAsync(Path, lab.Ada, ("source_branch", "feature/two"), ("target_branch", "main"), ("title", "Draft: Work on two"), ("description", "/close\nAs typed."));
        Assert.Equal((201, 2, true, true), (two.Status, (int)two.Json["iid"]!, (bool)two.Json["draft"]!, (bool)two.Json["work_in_progress"]!));
        Assert.Equal(("Draft: Work on two", "/close\nAs typed.", "opened"), ((string)two.Json["title"]!, (string)two.Json["description"]!, (string)two.Json["state"]!));
        var three = await lab.SendAsync(HttpMethod.Post, Path, lab.Ada, json: "{\"source_branch\":\"feature/three\",\"target_branch\":\"main\",\"title\":\"[Draft] Work on three\"}");
        Assert.Equal((201, 3, true), (three.Status, (int)three.Json["iid"]!, (bool)three.Json["draft"]!));
        var level = await lab.CreateAsync(Path, lab.Ada, ("source_branch", "level"), ("target_branch", "main"), ("title", "(draft) Nothing ahead"));
        Assert.Equal((201, 4, true, await lab.TipAsync("main")), (level.Status, (int)level.Json["iid"]!, (bool)level.Json["draft"]!, (string)level.Json["sha"]!));

        // A single one's members in gitlab.com's order (MEASURED 2026-11-06); the create's answer leaves
        // out first_contribution (MEASURED 2026-11-06 for nothing ahead).
        string[] read =
        [
            "id", "iid", "project_id", "title", "description", "state", "created_at", "updated_at", "merged_by", "merge_user", "merged_at", "closed_by", "closed_at", "target_branch", "source_branch",
            "user_notes_count", "upvotes", "downvotes", "author", "assignees", "assignee", "reviewers", "source_project_id", "target_project_id", "labels", "draft", "imported", "imported_from",
            "work_in_progress", "milestone", "merge_when_pipeline_succeeds", "merge_status", "detailed_merge_status", "merge_after", "sha", "merge_commit_sha", "squash_commit_sha",
            "discussion_locked", "should_remove_source_branch", "force_remove_source_branch", "prepared_at", "reference", "references", "web_url", "time_stats", "squash", "squash_on_merge",
            "task_completion_status", "has_conflicts", "blocking_discussions_resolved", "approvals_before_merge", "subscribed", "changes_count", "latest_build_started_at",
            "latest_build_finished_at", "first_deployed_to_production_at", "pipeline", "head_pipeline", "diff_refs", "merge_error", "first_contribution", "user",
        ];
        Assert.Equal(read.Where(name => name != "first_contribution"), level.Json.AsObject().Select(member => member.Key));
        Assert.Equal(read, (await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests/4", lab.Ada)).Json.AsObject().Select(member => member.Key));

        var fork = await lab.CreateAsync(forkPath, lab.Ada, ("source_branch", "fix"), ("target_branch", "main"), ("title", "Fix the typo"), ("target_project_id", Id(lab.Project)), ("allow_collaboration", "true"));
        Assert.Equal((201, 5, false), (fork.Status, (int)fork.Json["iid"]!, (bool)fork.Json["draft"]!));
        Assert.Equal((lab.Project.Id, lab.Fork.Id, lab.Project.Id, true), ((long)fork.Json["project_id"]!, (long)fork.Json["source_project_id"]!, (long)fork.Json["target_project_id"]!, (bool)fork.Json["allow_collaboration"]!));
        Assert.Equal("ada", (string)fork.Json["author"]!["username"]!);

        // A fork's duplicate names the open one !<iid> alone, as a project's own does (READ: to_reference
        // with no from, app/models/merge_request.rb:1441-1446).
        Assert.Equal((409, "{\"message\":[\"Another open merge request already exists for this source branch: !5\"]}"), (await lab.CreateAsync(forkPath, lab.Ada, ("source_branch", "fix"), ("target_branch", "main"), ("title", "Fix it again"), ("target_project_id", Id(lab.Project)))).StatusAndBody);

        // The parent into its fork is in one fork network (READ: validate_fork, app/models/merge_request.rb:1481-1487, :1508-1513).
        var intoFork = await lab.CreateAsync(Path, lab.Ada, ("source_branch", "feature/two"), ("target_branch", "main"), ("title", "Two in the fork"), ("target_project_id", Id(lab.Fork)));
        Assert.Equal((201, 1), (intoFork.Status, (int)intoFork.Json["iid"]!));
        Assert.Equal((lab.Fork.Id, lab.Project.Id, lab.Fork.Id), ((long)intoFork.Json["project_id"]!, (long)intoFork.Json["source_project_id"]!, (long)intoFork.Json["target_project_id"]!));
        Assert.Equal(await lab.TipAsync("feature/two"), await lab.RefAsync(lab.Fork, "refs/merge-requests/1/head"));

        Assert.Equal(await lab.TipAsync("feature/two"), await lab.RefAsync(lab.Project, "refs/merge-requests/2/head"));
        Assert.Equal(await lab.TipAsync("fix"), await lab.RefAsync(lab.Project, "refs/merge-requests/5/head"));
        Assert.Null(await lab.RefAsync(lab.Project, "refs/merge-requests/5/merge"));
        Assert.Contains(lab.Fake.Requests, request => request.Route == "create:g/s/p" && request.Status == 201 && request.Body!.Contains("source_branch=feature%2Ftwo", StringComparison.Ordinal));

        // (f) before (g): !1's branch gone from the project, its open request still answers 409.
        await lab.DeleteRefAsync(lab.Project, "refs/heads/feature/one");
        Assert.Equal((409, "{\"message\":[\"Another open merge request already exists for this source branch: !1\"]}"), (await lab.CreateAsync(Path, lab.Ada, ("source_branch", "feature/one"), ("target_branch", "main"), ("title", "Again"))).StatusAndBody);

        // (e) before (f): an open request from a branch into itself (the model takes one) still meets the 422.
        lab.Fake.AddMergeRequest(lab.Project, "Level with itself", "ada", "level", "level");
        Assert.Equal((422, SameBranch), (await lab.CreateAsync(Path, lab.Ada, ("source_branch", "level"), ("target_branch", "level"), ("title", "Level"))).StatusAndBody);
        Assert.Empty(lab.Fake.Errors);
        lab.AssertNoToken();
    }

    [Fact]
    public async Task A_fork_request_reopened_while_the_ref_sync_reads_another_fork_waits_for_the_next_sync()
    {
        await using var lab = await Lab.CreateAsync();

        // octo's fork's request comes before ada's fork's: a sync that finds it closed reads ada/p's heads
        // alone, while PUTs close and reopen it without the target's lock (they take the fake's alone).
        var octoFork = await lab.AddForkAsync("octo/p");
        lab.Fake.AddMergeRequest(lab.Project, "From octo's fork", "octo", "fix", "main", source: octoFork);
        lab.Fake.AddMergeRequest(lab.Project, "From ada's fork", "ada", "fix", "main", source: lab.Fork);
        const string Two = "api/v4/projects/g%2Fs%2Fp/merge_requests/2";
        Assert.Equal("closed", (string)(await lab.SendAsync(HttpMethod.Put, Two, lab.Octo, form: [("state_event", "close")])).Json["state"]!);

        using var stop = new CancellationTokenSource();
        var toggles = new List<int>();
        var toggling = Task.Run(
            async () =>
            {
                for (var reopen = true; !stop.IsCancellationRequested; reopen = !reopen)
                {
                    toggles.Add((await lab.SendAsync(HttpMethod.Put, Two, lab.Octo, form: [("state_event", reopen ? "reopen" : "close")])).Status);
                }
            },
            Cancelled);
        var lists = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            lists.Add((await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests", lab.Ada)).Status);
        }

        await stop.CancelAsync();
        await toggling;
        Assert.All(lists, status => Assert.Equal(200, status));
        Assert.True(toggles.Count > lists.Count, $"{toggles.Count} toggles");
        Assert.All(toggles, status => Assert.Equal(200, status));
        Assert.Empty(lab.Fake.Errors);

        // Left open, the next sync writes its head from octo's fork.
        Assert.Equal("opened", (string)(await lab.SendAsync(HttpMethod.Put, Two, lab.Octo, form: [("state_event", "reopen")])).Json["state"]!);
        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, Two, lab.Ada)).Status);
        Assert.Equal(await lab.TipAsync("fix"), await lab.RefAsync(lab.Project, "refs/merge-requests/2/head"));
        lab.AssertNoToken();
    }

    [Fact]
    public async Task Update_closes_and_reopens_and_delete_is_the_owners_alone()
    {
        await using var lab = await Lab.CreateAsync();
        const string One = "api/v4/projects/g%2Fs%2Fp/merge_requests/1";
        Assert.Equal(await lab.TipAsync("feature/one"), (string)(await lab.SendAsync(HttpMethod.Get, One, lab.Ada)).Json["sha"]!);

        var close = await lab.SendAsync(HttpMethod.Put, One, lab.Ada, form: [("state_event", "close")]);
        Assert.Equal((200, "closed"), (close.Status, (string)close.Json["state"]!));
        Assert.Equal("opened", (string)(await lab.SendAsync(HttpMethod.Put, One, lab.Ada, json: "{\"state_event\":\"reopen\"}")).Json["state"]!);
        Assert.Equal((403, "{\"message\":\"403 Forbidden\"}"), await lab.StatusAndBodyAsync(HttpMethod.Put, One, lab.Bob));
        Assert.Equal((404, "{\"message\":\"404 Not found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Put, "api/v4/projects/g%2Fs%2Fp/merge_requests/9", lab.Ada));

        Assert.Equal((403, "{\"message\":\"403 Forbidden\"}"), await lab.StatusAndBodyAsync(HttpMethod.Delete, One, lab.Ada));
        var deleted = await lab.SendAsync(HttpMethod.Delete, One, lab.Octo);
        Assert.Equal((204, string.Empty), (deleted.Status, deleted.Body));
        Assert.Equal((404, "{\"message\":\"404 Not found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Delete, One, lab.Octo));
        Assert.Empty(lab.Fake.MergeRequests(lab.Project));
        await lab.Fake.SyncRefsAsync(lab.Project, Cancelled);
        Assert.Null(await lab.RefAsync(lab.Project, "refs/merge-requests/1/head"));
        Assert.Equal(["mr:g/s/p/1", "update:g/s/p/1", "update:g/s/p/1", "update:g/s/p/1", "update:g/s/p/9", "delete:g/s/p/1", "delete:g/s/p/1", "delete:g/s/p/1"], lab.Fake.Requests.Select(request => request.Route));
        lab.AssertNoToken();
    }

    [Fact]
    public async Task Branches_are_listed_by_search_and_deleted_by_a_pusher()
    {
        await using var lab = await Lab.CreateAsync();
        const string Branches = "api/v4/projects/g%2Fs%2Fp/repository/branches";

        var listed = await lab.SendAsync(HttpMethod.Get, Branches + "?search=%5Efeature", lab.Ada);
        Assert.Equal(["feature/one", "feature/three", "feature/two"], listed.Json.AsArray().Select(row => (string)row!["name"]!));
        Assert.Equal((await lab.TipAsync("feature/one"), false, true), ((string)listed.Json[0]!["commit"]!["id"]!, (bool)listed.Json[0]!["default"]!, (bool)listed.Json[0]!["can_push"]!));
        Assert.Equal(["fix"], (await lab.SendAsync(HttpMethod.Get, Branches + "?search=ix%24", lab.Ada)).Json.AsArray().Select(row => (string)row!["name"]!));
        Assert.True((bool)(await lab.SendAsync(HttpMethod.Get, Branches + "?search=main", lab.Bob)).Json[0]!["default"]!);

        Assert.Equal((403, "{\"message\":\"403 Forbidden\"}"), await lab.StatusAndBodyAsync(HttpMethod.Delete, Branches + "/feature%2Fthree", lab.Bob));
        Assert.Equal(204, (await lab.SendAsync(HttpMethod.Delete, Branches + "/feature%2Fthree", lab.Ada)).Status);
        Assert.Null(await lab.RefAsync(lab.Project, "refs/heads/feature/three"));
        Assert.Equal((404, "{\"message\":\"404 Branch Not Found\"}"), await lab.StatusAndBodyAsync(HttpMethod.Delete, Branches + "/feature%2Fthree", lab.Ada));
        Assert.All(lab.Fake.Requests, request => Assert.Equal("branches:g/s/p", request.Route));
        lab.AssertNoToken();
    }

    [Fact]
    public async Task A_moved_projects_GET_is_301_to_its_id_with_the_query_any_other_method_405_and_its_pages_301()
    {
        await using var lab = await Lab.CreateAsync();
        lab.Fake.Move("g/s/p", "g/s/renamed");
        var id = Id(lab.Project);

        var moved = await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp/merge_requests?state=opened&per_page=1", lab.Ada);
        var location = $"{lab.Fake.Endpoints.ApiBase.AbsoluteUri}projects/{id}/merge_requests?per_page=1&state=opened";
        Assert.Equal((301, "text/plain; charset=utf-8", location, "This resource has been moved permanently to " + location), (moved.Status, moved.Header("Content-Type"), moved.Header("Location"), moved.Body));
        Assert.Equal($"{lab.Fake.Endpoints.ApiBase.AbsoluteUri}projects/{id}", (await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Fp", lab.Ada)).Header("Location"));
        Assert.Equal((405, "{\"message\":\"Non GET methods are not allowed for moved projects\"}"), (await lab.CreateAsync("api/v4/projects/g%2Fs%2Fp/merge_requests", lab.Ada, ("source_branch", "feature/two"), ("target_branch", "main"), ("title", "Two"))).StatusAndBody);

        var current = await lab.SendAsync(HttpMethod.Get, $"api/v4/projects/{id}/merge_requests?per_page=1&state=opened", lab.Ada);
        Assert.Equal([1], current.Json.AsArray().Select(row => (int)row!["iid"]!));
        Assert.Equal("g/s/renamed", (string)(await lab.SendAsync(HttpMethod.Get, "api/v4/projects/g%2Fs%2Frenamed", lab.Ada)).Json["path_with_namespace"]!);
        Assert.Equal(lab.Fake.Endpoints.WebBase.AbsoluteUri + "g/s/renamed/-/merge_requests/1", (string)current.Json[0]!["web_url"]!);

        var page = await lab.SendAsync(HttpMethod.Get, "g/s/p/-/merge_requests");
        Assert.Equal((301, lab.Fake.Endpoints.WebBase.AbsoluteUri + "g/s/renamed/-/merge_requests"), (page.Status, page.Header("Location")));
        Assert.Equal(lab.Fake.Endpoints.WebBase.AbsoluteUri + "g/s/renamed", (await lab.SendAsync(HttpMethod.Get, "G/S/P")).Header("Location"));
        lab.AssertNoToken();
    }

    [Fact]
    public async Task Every_fault_is_answered_once_then_the_normal_answer()
    {
        await using var lab = await Lab.CreateAsync();
        var faults = new (FakeGitLabFault Fault, int Status, string? Body)[]
        {
            (FakeGitLabFault.Unauthorized, 401, "{\"message\":\"401 Unauthorized\"}"),
            (FakeGitLabFault.Expired, 401, "{\"error\":\"invalid_token\",\"error_description\":\"Token is expired. You can either do re-authorization or token refresh.\"}"),
            (FakeGitLabFault.Revoked, 401, "{\"error\":\"invalid_token\",\"error_description\":\"Token was revoked. You have to re-authorize from the user.\"}"),
            (FakeGitLabFault.InsufficientScope, 403, "{\"error\":\"insufficient_scope\",\"error_description\":\"The request requires higher privileges than provided by the access token.\",\"scope\":\"read_user ai_workflows api read_api\"}"),
            (FakeGitLabFault.InsufficientGranular, 403, "{\"error\":\"insufficient_granular_scope\",\"error_description\":\"Access denied: This operation requires a fine-grained personal access token with the following user permissions: [User: Read].\"}"),
            (FakeGitLabFault.Forbidden, 403, "{\"message\":\"403 Forbidden\"}"),
            (FakeGitLabFault.NotFound, 404, "{\"message\":\"404 Not Found\"}"),
            (FakeGitLabFault.RateLimited, 429, "Retry later\n"),
            (FakeGitLabFault.EndpointRateLimited, 429, "{\"message\":{\"error\":\"This endpoint has been requested too many times. Try again later.\"}}"),
            (FakeGitLabFault.Timeout, 408, "{\"message\":{\"error\":\"Request timed out\"}}"),
            (FakeGitLabFault.ServerError, 500, "{\"message\":\"500 Internal Server Error\"}"),
            (FakeGitLabFault.BadGateway, 502, null),
            (FakeGitLabFault.Malformed, 200, "{\"id\":"),
            (FakeGitLabFault.MovedTo(new Uri("http://127.0.0.1:3/api/v4/user?moved=1")), 301, "This resource has been moved permanently to http://127.0.0.1:3/api/v4/user?moved=1"),
        };
        foreach (var (fault, status, body) in faults)
        {
            lab.Fake.FailNext("User", fault);
            var failed = await lab.SendAsync(HttpMethod.Get, "api/v4/user", lab.Ada);
            Assert.True(status == failed.Status, $"{fault}: {failed.Status}");
            if (body is not null)
            {
                Assert.Equal(body, failed.Body);
            }

            if (fault == FakeGitLabFault.RateLimited)
            {
                Assert.Equal(("60", "text/plain", "0"), (failed.Header("Retry-After"), failed.Header("Content-Type"), failed.Header("ratelimit-remaining")));
                Assert.Equal(AppClocks.HarnessTime.AddMinutes(1).ToString("r", CultureInfo.InvariantCulture), failed.Header("RateLimit-ResetTime"));
            }
            else if (fault == FakeGitLabFault.EndpointRateLimited)
            {
                Assert.Equal(("600", "projects_api"), (failed.Header("Retry-After"), failed.Header("RateLimit-Name")));
                Assert.Null(failed.Header("ratelimit-remaining"));
            }
            else if (fault == FakeGitLabFault.BadGateway)
            {
                Assert.StartsWith("text/html", failed.Header("Content-Type"), StringComparison.Ordinal);
            }
            else if (fault == FakeGitLabFault.Expired || fault == FakeGitLabFault.InsufficientScope)
            {
                Assert.StartsWith("Bearer realm=\"Protected by OAuth 2.0\"", failed.Header("WWW-Authenticate"), StringComparison.Ordinal);
            }
            else if (fault.Location is { } location)
            {
                Assert.Equal(location.AbsoluteUri, failed.Header("Location"));
            }

            Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/user", lab.Ada)).Status);
        }

        // A create made in full, then no answer (7a's CreatedThenDropped), and the core's drops.
        lab.Fake.FailNext("create:g/s/p", FakeGitLabFault.CreatedThenDropped);
        await Assert.ThrowsAsync<HttpRequestException>(() => lab.CreateAsync("api/v4/projects/g%2Fs%2Fp/merge_requests", lab.Ada, ("source_branch", "feature/two"), ("target_branch", "main"), ("title", "Two")));
        Assert.Contains(lab.Fake.MergeRequests(lab.Project), request => request.SourceBranch == "feature/two");
        lab.Fake.FailNext("projects", FakeServiceFault.Dropped);
        await Assert.ThrowsAsync<HttpRequestException>(() => lab.SendAsync(HttpMethod.Get, "api/v4/projects", lab.Ada));
        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, "api/v4/projects", lab.Ada)).Status);
        Assert.Throws<NotSupportedException>(() => lab.Fake.FailNext("git:g/s/p", FakeGitLabFault.NotFound));
        Assert.Empty(lab.Fake.Errors);
        lab.AssertNoToken();
    }

    [Fact]
    public async Task A_200_GETs_weak_ETag_gets_a_counted_304_and_pages_and_gits_redirect_answer_without_a_token()
    {
        await using var lab = await Lab.CreateAsync();
        const string Project = "api/v4/projects/g%2Fs%2Fp";

        var first = await lab.SendAsync(HttpMethod.Get, Project, lab.Ada);
        var etag = first.Header("ETag")!;
        Assert.Matches("^W/\"[0-9a-f]{32}\"$", etag);
        Assert.Equal("max-age=0, private, must-revalidate", first.Header("Cache-Control"));
        var unchanged = await lab.SendAsync(HttpMethod.Get, Project, lab.Ada, headers: [("If-None-Match", etag)]);
        Assert.Equal((304, string.Empty, etag), (unchanged.Status, unchanged.Body, unchanged.Header("ETag")));
        Assert.Equal(int.Parse(first.Header("ratelimit-remaining")!, CultureInfo.InvariantCulture) - 1, int.Parse(unchanged.Header("ratelimit-remaining")!, CultureInfo.InvariantCulture));
        Assert.Equal(200, (await lab.SendAsync(HttpMethod.Get, Project, lab.Ada, headers: [("If-None-Match", "W/\"other\"")])).Status);

        var page = await lab.SendAsync(HttpMethod.Get, "g/s/p/-/merge_requests/1");
        Assert.Equal((200, "text/html; charset=utf-8"), (page.Status, page.Header("Content-Type")));
        var redirect = await lab.SendAsync(HttpMethod.Get, "g/s/p/info/refs?service=git-upload-pack");
        Assert.Equal((301, "http://127.0.0.1:3/g/s/p.git/info/refs?service=git-upload-pack"), (redirect.Status, redirect.Header("Location")));

        // In process git's paths are not the fake's: the core's 404.
        Assert.Equal((404, "Not found.\n"), await lab.StatusAndBodyAsync(HttpMethod.Get, "g/s/p.git/info/refs?service=git-upload-pack"));
        Assert.Equal(["project:g/s/p", "project:g/s/p", "project:g/s/p", "page", "page", null], lab.Fake.Requests.Select(request => request.Route));
        lab.AssertNoToken();
    }

    private static string Id(FakeGitLabProject project) => project.Id.ToString(CultureInfo.InvariantCulture);

    private static List<string> Paths(Answer answer) => [.. answer.Json.AsArray().Select(row => (string)row!["path_with_namespace"]!)];

    /// <summary>A <c>Link</c> header's <c>rel="next"</c> address; null without one.</summary>
    private static string? NextOf(string link) =>
        link.Split(", ").Where(part => part.EndsWith("rel=\"next\"", StringComparison.Ordinal)).Select(part => part[1..part.IndexOf('>', StringComparison.Ordinal)]).FirstOrDefault();

    /// <summary>An answer as the test reads it: status, body, and every header by name without case.</summary>
    private sealed record Answer(int Status, string Body, IReadOnlyDictionary<string, string> Headers)
    {
        public JsonNode Json => JsonNode.Parse(Body)!;

        public (int, string) StatusAndBody => (Status, Body);

        public string? Header(string name) => Headers.GetValueOrDefault(name);
    }

    /// <summary>
    /// The fake in process with a source repository (main; feature/one, feature/two, feature/three and
    /// fix a commit each on it; level at main): ada (2001), octo (2002), bob (2003) and a token each
    /// (ada's legacy one too); ada Developer of the group g, octo Owner and bob Reporter of g/s/p;
    /// projects g/s/p, its fork ada/p and the public pub/open cloned from the source, g/s/t/deep and
    /// octo/secret empty; g/s/p's !1 "Add feature one" (ada, feature/one).
    /// </summary>
    private sealed class Lab : IAsyncDisposable
    {
        private readonly TempRepo _source;
        private readonly TempDirectory _root;
        private readonly HttpClient _client;

        private Lab(TempRepo source, TempDirectory root, FakeGitLab fake)
        {
            _source = source;
            _root = root;
            Fake = fake;
            _client = new HttpClient(fake.CreateHandler());
        }

        public FakeGitLab Fake { get; }

        public FakeGitLabProject Project { get; private set; } = null!;

        public FakeGitLabProject Fork { get; private set; } = null!;

        public FakeGitLabProject Public { get; private set; } = null!;

        public FakeGitLabProject Secret { get; private set; } = null!;

        public string Ada { get; private set; } = string.Empty;

        public string AdaLegacy { get; private set; } = string.Empty;

        public string Octo { get; private set; } = string.Empty;

        public string Bob { get; private set; } = string.Empty;

        public static async Task<Lab> CreateAsync()
        {
            var source = await TempRepo.CreateAsync("gitlab-source");
            await source.CommitFileAsync("README.md", "web\n", "Initial commit");
            foreach (var branch in new[] { "feature/one", "feature/two", "feature/three", "fix" })
            {
                await source.CreateBranchAsync(branch, "main");
                await source.CheckoutAsync(branch);
                await source.CommitFileAsync(branch.Replace('/', '-') + ".txt", branch + "\n", "Work on " + branch);
            }

            await source.CreateBranchAsync("level", "main");
            await source.CheckoutAsync("main");
            var root = new TempDirectory("fake gitlab");
            var lab = new Lab(source, root, FakeGitLab.InProcess(root.Path));
            var fake = lab.Fake;
            fake.AddUser("ada", 2001, Scenarios.AdaName);
            fake.AddUser("octo", 2002, Scenarios.OctoName);
            fake.AddUser("bob", 2003, "Bob Example");
            lab.Ada = fake.AddToken("ada");
            lab.AdaLegacy = fake.AddToken("ada", FakeGitLabTokenKind.Legacy);
            lab.Octo = fake.AddToken("octo");
            lab.Bob = fake.AddToken("bob");
            Assert.Equal(("glpat-FAKE-ada-1", "glpat-FAKE-ada-2", "glpat-FAKE-octo-1"), (lab.Ada, lab.AdaLegacy, lab.Octo));
            fake.Grant("g", "ada", 30);
            fake.Grant("g/s/p", "octo", 50);
            fake.Grant("g/s/p", "bob", 20);
            lab.Project = await fake.AddProjectAsync("g/s/p", source.Path, ct: Cancelled);
            lab.Fork = await fake.AddProjectAsync("ada/p", source.Path, forkOf: lab.Project, ct: Cancelled);
            lab.Public = await fake.AddProjectAsync("pub/open", source.Path, "public", ct: Cancelled);
            await fake.AddProjectAsync("g/s/t/deep", ct: Cancelled);
            lab.Secret = await fake.AddProjectAsync("octo/secret", ct: Cancelled);
            fake.AddMergeRequest(lab.Project, "Add feature one", "ada", "feature/one", "main");
            Assert.Equal((5001L, 7001L), (lab.Project.Id, fake.MergeRequests(lab.Project)[0].Id));
            return lab;
        }

        public async Task<Answer> SendAsync(HttpMethod method, string path, string? token = null, (string Name, string Value)[]? form = null, string? json = null, (string Name, string Value)[]? headers = null)
        {
            using var message = new HttpRequestMessage(method, new Uri(Fake.Endpoints.WebBase.AbsoluteUri + path));
            if (token is not null)
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            foreach (var (name, value) in headers ?? [])
            {
                message.Headers.TryAddWithoutValidation(name, value);
            }

            if (form is not null)
            {
                message.Content = new FormUrlEncodedContent(form.Select(pair => KeyValuePair.Create(pair.Name, pair.Value)));
            }
            else if (json is not null)
            {
                message.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using var response = await _client.SendAsync(message, Cancelled);
            var received = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, values) in response.Headers.NonValidated.Concat(response.Content.Headers.NonValidated))
            {
                received[name] = string.Join(", ", values);
            }

            return new Answer((int)response.StatusCode, await response.Content.ReadAsStringAsync(Cancelled), received);
        }

        public Task<Answer> CreateAsync(string path, string token, params (string Name, string Value)[] form) => SendAsync(HttpMethod.Post, path, token, form: form);

        public async Task<(int, string)> StatusAndBodyAsync(HttpMethod method, string path, string? token = null) => (await SendAsync(method, path, token)).StatusAndBody;

        /// <summary>A branch's tip in the source repository.</summary>
        public async Task<string> TipAsync(string branch) => (await _source.GitAsync("rev-parse", "refs/heads/" + branch)).StandardOutput.Trim();

        /// <summary>A ref in a project's bare repository; null when it has none.</summary>
        public async Task<string?> RefAsync(FakeGitLabProject project, string name)
        {
            var result = await _source.GitAllowingFailureAsync("--git-dir", project.BarePath!, "rev-parse", "--verify", "--quiet", name);
            return result.Succeeded ? result.StandardOutput.Trim() : null;
        }

        /// <summary>A fork of g/s/p at <paramref name="path"/>, cloned from the source repository.</summary>
        public Task<FakeGitLabProject> AddForkAsync(string path) => Fake.AddProjectAsync(path, _source.Path, forkOf: Project, ct: Cancelled);

        /// <summary>Deletes a ref in a project's bare repository, behind the fake's back.</summary>
        public async Task DeleteRefAsync(FakeGitLabProject project, string name) =>
            await _source.GitAsync("--git-dir", project.BarePath!, "update-ref", "-d", name);

        /// <summary>No request record holds a token: not its path, query, body or login.</summary>
        public void AssertNoToken() =>
            Assert.All(Fake.Requests, request => Assert.DoesNotContain("glpat-", request.Path + request.Query + request.Body + request.Login, StringComparison.Ordinal));

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await Fake.DisposeAsync();
            _root.Dispose();
            _source.Dispose();
        }
    }
}
