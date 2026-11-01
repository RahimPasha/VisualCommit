using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The fake of GitLab serving git on port 0 (docs/phase-7b-design.md 4.5, 4.6), with real git and no
/// credential helper or askpass program: the token goes in the URL, the fake's own test of what the
/// app's helper later gives git. A nested project cloned with <c>x-access-token</c>, the username and
/// through the <c>.git</c>-less 301; merge requests' head refs, a fork's included, and no
/// <c>/merge</c>; unknown, revoked and expired tokens; a token that may not fetch or push; an anonymous
/// clone of a public project; a moved project at its old path; a path in another case; the
/// scenario's half. Nothing here depends on git's version (CI's 2.43 containers run it too).
/// </summary>
public sealed class FakeGitLabGitTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_nested_project_is_cloned_with_x_access_token_the_username_through_the_301_and_in_another_case()
    {
        await using var lab = await GitLab.CreateAsync();
        var fake = lab.Fake;

        var byHelperUser = await lab.Source.GitAllowingFailureAsync("clone", "--quiet", lab.Url("g/s/p.git", "x-access-token", lab.Ada), lab.Source.SiblingPath("helper-user"));
        Assert.True(byHelperUser.Succeeded, byHelperUser.StandardError);
        var signedIn = fake.GitRequests.Where(request => request.Status == 200).ToList();
        Assert.NotEmpty(signedIn);
        Assert.All(signedIn, request => Assert.Equal(("ada", "x-access-token"), (request.UserName, request.GivenUserName)));
        Assert.Contains(fake.GitRequests, request => request.Status == 401 && request.UserName is null);

        var byUsername = await lab.Source.GitAllowingFailureAsync("clone", "--quiet", lab.Url("g/s/p.git", "ada", lab.Ada), lab.Source.SiblingPath("username"));
        Assert.True(byUsername.Succeeded, byUsername.StandardError);
        Assert.Equal(("ada", "ada"), (fake.GitRequests[^1].UserName, fake.GitRequests[^1].GivenUserName));

        // Without .git: GitLab's 301 to <path>.git/info/refs, which git follows (MEASURED r6 6.1, r7 6).
        var redirected = await lab.Source.GitAllowingFailureAsync("clone", "--quiet", lab.Url("g/s/p", "x-access-token", lab.Ada), lab.Source.SiblingPath("redirected"));
        Assert.True(redirected.Succeeded, redirected.StandardError);
        var redirect = Assert.Single(fake.Requests, request => request.Path == "/g/s/p/info/refs");
        Assert.Equal((301, "page", "service=git-upload-pack"), (redirect.Status, redirect.Route, redirect.Query));
        Assert.Contains(fake.GitRequests, request => request.Path == "/g/s/p.git/git-upload-pack" && request.Status == 200);
        Assert.Equal("Work on fix", await GitInAsync(lab.Source, lab.Source.SiblingPath("redirected"), "log", "-1", "--format=%s", "origin/fix"));

        // Paths match without case, as gitlab.com's do (MEASURED r6 6.1).
        var otherCase = await lab.Source.GitAllowingFailureAsync("clone", "--quiet", lab.Url("G/S/P.git", "x-access-token", lab.Ada), lab.Source.SiblingPath("other-case"));
        Assert.True(otherCase.Succeeded, otherCase.StandardError);
        Assert.Empty(fake.Errors);
        Assert.All(fake.Requests, request => Assert.DoesNotContain("glpat-", request.Path + request.Query, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ls_remote_shows_a_same_project_and_a_fork_merge_requests_head_and_no_merge_ref_and_its_fetch_writes_only_FETCH_HEAD()
    {
        await using var lab = await GitLab.CreateAsync();
        var url = lab.Url("g/s/p.git", "x-access-token", lab.Ada);

        var listed = await lab.Source.GitAllowingFailureAsync("ls-remote", url);
        Assert.True(listed.Succeeded, listed.StandardError);
        var refs = listed.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r').Split('\t')).ToDictionary(fields => fields[1], fields => fields[0]);
        Assert.Equal(await lab.TipAsync("feature/one"), refs["refs/merge-requests/1/head"]);
        Assert.Equal(await lab.ForkTipAsync("fork-fix"), refs["refs/merge-requests/2/head"]);
        Assert.DoesNotContain(refs.Keys, name => name.EndsWith("/merge", StringComparison.Ordinal));
        Assert.Equal(await lab.ForkTipAsync("fork-fix"), lab.Fake.MergeRequests(lab.Project)[1].HeadSha);

        var clone = await lab.CloneAsync("checkout", lab.Ada);
        var before = await GitInAsync(lab.Source, clone, "for-each-ref", "--format=%(objectname) %(refname)");
        var configBefore = File.ReadAllText(Path.Combine(clone, ".git", "config"));
        var fetched = await GitResultInAsync(lab.Source, clone, "fetch", "--quiet", "--no-tags", "origin", "refs/merge-requests/2/head");

        Assert.True(fetched.Succeeded, fetched.StandardError);
        Assert.Equal(before, await GitInAsync(lab.Source, clone, "for-each-ref", "--format=%(objectname) %(refname)"));
        Assert.Equal(configBefore, File.ReadAllText(Path.Combine(clone, ".git", "config")));
        var head = File.ReadAllText(Path.Combine(clone, ".git", "FETCH_HEAD"));
        Assert.StartsWith(await lab.ForkTipAsync("fork-fix"), head, StringComparison.Ordinal);
        Assert.Contains("'refs/merge-requests/2/head'", head, StringComparison.Ordinal);
        Assert.Empty(lab.Fake.Errors);
    }

    [Fact]
    public async Task Unknown_revoked_and_expired_tokens_are_401_and_a_token_that_may_not_fetch_or_push_is_403()
    {
        await using var lab = await GitLab.CreateAsync();
        var fake = lab.Fake;

        foreach (var refused in new[] { "glpat-FAKE-nobody-1", lab.Octo, lab.AdaExpiring })
        {
            if (refused == lab.Octo)
            {
                fake.Revoke(refused);
            }
            else if (refused == lab.AdaExpiring)
            {
                fake.Expire(refused);
            }

            var listed = await lab.Source.GitAllowingFailureAsync("ls-remote", lab.Url("g/s/p.git", "x-access-token", refused));
            Assert.False(listed.Succeeded);
            Assert.Equal((401, (string?)null, "x-access-token"), (fake.GitRequests[^1].Status, fake.GitRequests[^1].UserName, fake.GitRequests[^1].GivenUserName));
        }

        // A legacy read_user token cannot download code; a fine-grained Code: Download one cannot push (READ g2 G3).
        var readUser = await lab.Source.GitAllowingFailureAsync("ls-remote", lab.Url("g/s/p.git", "x-access-token", lab.ReadUser));
        Assert.False(readUser.Succeeded);
        Assert.Equal((403, "ada"), (fake.GitRequests[^1].Status, fake.GitRequests[^1].UserName));
        Assert.Contains("You are not allowed to download code.", readUser.StandardError, StringComparison.Ordinal);

        var clone = await lab.CloneAsync("download-only", lab.DownloadOnly);
        await GitInAsync(lab.Source, clone, "checkout", "--quiet", "-b", "try-push");
        await File.WriteAllTextAsync(Path.Combine(clone, "push.txt"), "push\n", Cancelled);
        await GitInAsync(lab.Source, clone, "add", "push.txt");
        await GitInAsync(lab.Source, clone, "commit", "--quiet", "-m", "Try a push");
        var push = await GitResultInAsync(lab.Source, clone, "push", "--quiet", "origin", "try-push");
        Assert.False(push.Succeeded);
        Assert.Contains(fake.GitRequests, request => request.Status == 403 && request.UserName == "ada" && request.Query.Contains("git-receive-pack", StringComparison.Ordinal));
        Assert.Contains(Granular("Code: Push"), push.StandardError, StringComparison.Ordinal);
        Assert.Null(await lab.RefAsync(lab.Project, "refs/heads/try-push"));

        // A fine-grained Code: Push one alone cannot fetch (READ g2 G3).
        var pushOnly = await lab.Source.GitAllowingFailureAsync("ls-remote", lab.Url("g/s/p.git", "x-access-token", lab.PushOnly));
        Assert.False(pushOnly.Succeeded);
        Assert.Equal((403, "ada"), (fake.GitRequests[^1].Status, fake.GitRequests[^1].UserName));
        Assert.Contains(Granular("Code: Download"), pushOnly.StandardError, StringComparison.Ordinal);

        // The same push with the full fine-grained token goes through.
        await GitInAsync(lab.Source, clone, "remote", "set-url", "origin", lab.Url("g/s/p.git", "x-access-token", lab.Ada));
        var allowed = await GitResultInAsync(lab.Source, clone, "push", "--quiet", "origin", "try-push");
        Assert.True(allowed.Succeeded, allowed.StandardError);
        Assert.NotNull(await lab.RefAsync(lab.Project, "refs/heads/try-push"));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_project_hidden_from_the_user_is_refused_in_GitAccess_order_a_legacy_tokens_missing_ability_first()
    {
        await using var lab = await GitLab.CreateAsync();
        var fake = lab.Fake;
        const string NotFound = "The project you were looking for could not be found or you don't have permission to view it.";
        string Secret(string token) => lab.Url("octo/secret.git", "x-access-token", token);

        // lib/gitlab/git_access.rb:77-98: check_authentication_abilities! before the project's (READ g2 G3).
        var readUser = await lab.Source.GitAllowingFailureAsync("ls-remote", Secret(lab.ReadUser));
        Assert.False(readUser.Succeeded);
        Assert.Contains("You are not allowed to download code.", readUser.StandardError, StringComparison.Ordinal);
        foreach (var token in new[] { lab.Ada, lab.AdaLegacyApi, lab.DownloadOnly })
        {
            var fetch = await lab.Source.GitAllowingFailureAsync("ls-remote", Secret(token));
            Assert.False(fetch.Succeeded);
            Assert.Equal((403, "ada"), (fake.GitRequests[^1].Status, fake.GitRequests[^1].UserName));
            Assert.Contains(NotFound, fetch.StandardError, StringComparison.Ordinal);
        }

        // A push: a legacy token without push ability says so first; any other token meets the hidden project.
        var readUserPush = await lab.Source.GitAllowingFailureAsync("push", "--quiet", Secret(lab.ReadUser), "main:refs/heads/hidden");
        Assert.False(readUserPush.Succeeded);
        Assert.Contains("You are not allowed to upload code.", readUserPush.StandardError, StringComparison.Ordinal);
        foreach (var token in new[] { lab.Ada, lab.AdaLegacyApi, lab.DownloadOnly })
        {
            var push = await lab.Source.GitAllowingFailureAsync("push", "--quiet", Secret(token), "main:refs/heads/hidden");
            Assert.False(push.Succeeded);
            Assert.Equal((403, "ada"), (fake.GitRequests[^1].Status, fake.GitRequests[^1].UserName));
            Assert.Contains(NotFound, push.StandardError, StringComparison.Ordinal);
            Assert.DoesNotContain("upload code", push.StandardError, StringComparison.Ordinal);
        }

        Assert.Null(await lab.RefAsync(lab.Secret, "refs/heads/hidden"));
        Assert.True((await lab.Source.GitAllowingFailureAsync("ls-remote", lab.Url("octo/secret.git", "x-access-token", lab.Octo))).Succeeded);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_public_project_is_cloned_anonymously_and_its_push_asks_for_credentials()
    {
        await using var lab = await GitLab.CreateAsync();
        var anonymous = lab.Source.SiblingPath("anonymous");

        var cloned = await lab.Source.GitAllowingFailureAsync("clone", "--quiet", $"http://127.0.0.1:{lab.Fake.Port}/pub/open.git", anonymous);
        Assert.True(cloned.Succeeded, cloned.StandardError);
        Assert.All(lab.Fake.GitRequests, request => Assert.Equal((200, (string?)null), (request.Status, request.UserName)));

        var push = await GitResultInAsync(lab.Source, anonymous, "push", "--quiet", "origin", "main:refs/heads/anonymous");
        Assert.False(push.Succeeded);
        Assert.Contains(lab.Fake.GitRequests, request => request.Status == 401 && request.UserName is null && request.Query.Contains("git-receive-pack", StringComparison.Ordinal));
        Assert.Null(await lab.RefAsync(lab.Public, "refs/heads/anonymous"));
        Assert.Empty(lab.Fake.Errors);
    }

    [Fact]
    public async Task A_moved_project_is_cloned_fetched_and_pushed_at_its_old_path_with_the_token_and_no_redirect()
    {
        await using var lab = await GitLab.CreateAsync();
        lab.Fake.Move("g/s/p", "g/s/renamed");

        var clone = await lab.CloneAsync("old-path", lab.Ada);
        Assert.Equal("Work on feature/one", await GitInAsync(lab.Source, clone, "log", "-1", "--format=%s", "origin/feature/one"));
        var fetched = await GitResultInAsync(lab.Source, clone, "fetch", "--quiet", "origin");
        Assert.True(fetched.Succeeded, fetched.StandardError);
        var push = await GitResultInAsync(lab.Source, clone, "push", "--quiet", "origin", "main:refs/heads/after-move");
        Assert.True(push.Succeeded, push.StandardError);

        Assert.Equal(await lab.TipAsync("main"), await lab.RefAsync(lab.Project, "refs/heads/after-move"));
        Assert.DoesNotContain(lab.Fake.GitRequests, request => request.Status is 301 or 302 or 307 or 308);
        Assert.DoesNotContain(lab.Fake.Requests, request => request.Status == 301);
        Assert.All(lab.Fake.GitRequests.Where(request => request.Status == 200), request => Assert.StartsWith("/g/s/p.git/", request.Path, StringComparison.Ordinal));

        // The new path serves the same repository.
        var listed = await lab.Source.GitAllowingFailureAsync("ls-remote", lab.Url("g/s/renamed.git", "x-access-token", lab.Ada), "refs/heads/after-move");
        Assert.True(listed.Succeeded, listed.StandardError);
        Assert.StartsWith(await lab.TipAsync("main"), listed.StandardOutput, StringComparison.Ordinal);
        Assert.Empty(lab.Fake.Errors);
    }

    [Fact]
    public async Task The_scenarios_half_is_served_on_port_0()
    {
        using var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, azureDevOpsPort: 0, gitLabPort: 0, gitHubPort: 0, ct: Cancelled);
        var fake = served.GitLab;

        Assert.Equal(("glpat-FAKE-ada-1", "glpat-FAKE-ada-2"), (served.GitLabToken, served.GitLabLegacyToken));
        var webApp = fake.Project(Scenarios.Hosting7bWebApp);
        var fork = fake.Project(Scenarios.Hosting7bWebAppFork);
        var long_ = fake.Project(Scenarios.Hosting7bLongProject);
        Assert.Equal((5001L, 5002L, 5003L), (webApp.Id, fork.Id, long_.Id));
        Assert.Equal((webApp, "private", "private"), (fork.ForkOf, webApp.Visibility, fork.Visibility));
        Assert.Equal(
            [(21, "Add the search box", "ada", "feature/search", false, webApp), (22, "Draft: Update the readme", "octo", "docs/readme", true, webApp), (23, "Fix a typo in the footer", "ada", "fix-typo", false, fork)],
            fake.MergeRequests(webApp).Select(request => (request.Iid, request.Title, request.Author, request.SourceBranch, request.Draft, request.Source)));
        Assert.All(fake.MergeRequests(webApp), request => Assert.Equal("main", request.TargetBranch));

        // Its users, ada 2001 and octo 2002, both Developers (30) of northfield (4.5), as the API shows them.
        using var api = new HttpClient(fake.CreateHandler());
        var octoToken = fake.AddToken("octo");
        foreach (var (token, id, username, name) in new[] { (served.GitLabToken, 2001L, "ada", "Ada Lovelace"), (octoToken, 2002L, "octo", "Octo Cat") })
        {
            var user = await GetJsonAsync(api, fake, "api/v4/user", token);
            Assert.Equal((id, username, name), ((long)user["id"]!, (string)user["username"]!, (string)user["name"]!));
            foreach (var path in new[] { Scenarios.Hosting7bWebApp, Scenarios.Hosting7bLongProject })
            {
                var project = await GetJsonAsync(api, fake, "api/v4/projects/" + Uri.EscapeDataString(path), token);
                Assert.Equal(30, (int)project["permissions"]!["group_access"]!["access_level"]!);
            }
        }

        Assert.Equal(Scenarios.Hosting7bGitLabGroup, Scenarios.Hosting7bWebApp.Split('/')[0]);

        var url = $"http://x-access-token:{served.GitLabToken}@127.0.0.1:{fake.Port}/{Scenarios.Hosting7bWebApp}.git";
        Assert.Equal(url.Replace($"x-access-token:{served.GitLabToken}@", string.Empty, StringComparison.Ordinal), (await served.WebApp.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim());
        var listed = await served.WebApp.GitAllowingFailureAsync("ls-remote", url, "refs/merge-requests/*");
        Assert.True(listed.Succeeded, listed.StandardError);
        var refs = listed.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r').Split('\t')).ToDictionary(fields => fields[1], fields => fields[0]);
        Assert.Equal(["refs/merge-requests/21/head", "refs/merge-requests/22/head", "refs/merge-requests/23/head"], refs.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(await BareTipAsync(served.WebApp, webApp, "feature/search"), refs["refs/merge-requests/21/head"]);
        Assert.Equal(await BareTipAsync(served.WebApp, webApp, "docs/readme"), refs["refs/merge-requests/22/head"]);
        Assert.Equal(await BareTipAsync(served.WebApp, fork, "fix-typo"), refs["refs/merge-requests/23/head"]);
        Assert.All(fake.GitRequests.Where(request => request.Status == 200), request => Assert.Equal("ada", request.UserName));

        // ada's legacy token (api) reads git too; the long project is served, empty.
        var legacy = await served.WebApp.GitAllowingFailureAsync("ls-remote", url.Replace(served.GitLabToken, served.GitLabLegacyToken, StringComparison.Ordinal), "refs/heads/main");
        Assert.True(legacy.Succeeded, legacy.StandardError);
        var empty = await served.WebApp.GitAllowingFailureAsync("ls-remote", $"http://x-access-token:{served.GitLabToken}@127.0.0.1:{fake.Port}/{Scenarios.Hosting7bLongProject}.git");
        Assert.True(empty.Succeeded, empty.StandardError);
        Assert.Equal(string.Empty, empty.StandardOutput.Trim());
        Assert.Empty(fake.Errors);
    }

    /// <summary>A GET through the fake's handler with a Bearer token; its 200 JSON.</summary>
    private static async Task<System.Text.Json.Nodes.JsonNode> GetJsonAsync(HttpClient api, FakeGitLab fake, string path, string token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(fake.Endpoints.WebBase.AbsoluteUri + path));
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await api.SendAsync(message, Cancelled);
        var body = await response.Content.ReadAsStringAsync(Cancelled);
        Assert.True(response.IsSuccessStatusCode, body);
        return System.Text.Json.Nodes.JsonNode.Parse(body)!;
    }

    /// <summary>GitLab's fine-grained refusal naming one project permission (READ g2 G3).</summary>
    private static string Granular(string label) =>
        $"Access denied: This operation requires a fine-grained personal access token with the following project permissions: [{label}].";

    private static async Task<string> BareTipAsync(TempRepo repo, FakeGitLabProject project, string branch) =>
        (await repo.GitAsync("--git-dir", project.BarePath!, "rev-parse", "refs/heads/" + branch)).StandardOutput.Trim();

    private static async Task<string> GitInAsync(TempRepo repo, string folder, params string[] arguments) =>
        (await GitResultInAsync(repo, folder, arguments)).EnsureSuccess(new GitCommand(arguments)).StandardOutput.Trim();

    private static Task<GitResult> GitResultInAsync(TempRepo repo, string folder, params string[] arguments) =>
        repo.Runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = folder, Environment = repo.Environment }, Cancelled);

    /// <summary>
    /// The fake on port 0 with a source repository (main; feature/one and fix a commit each), ada
    /// Developer (30) of the group g, octo too; g/s/p cloned from the source, ada's fork ada/p with a
    /// fork-fix branch, the public pub/open, octo's private octo/secret that ada cannot see; g/s/p's !1
    /// from feature/one and !2 from the fork's fork-fix. Tokens: ada's fine-grained (default), a legacy
    /// <c>read_user</c> one, a legacy <c>api</c> one, a fine-grained Code: Download one, a fine-grained
    /// Code: Push one, one to expire; octo's.
    /// </summary>
    private sealed class GitLab : IAsyncDisposable
    {
        private readonly TempDirectory _root;

        private GitLab(TempRepo source, TempDirectory root, FakeGitLab fake)
        {
            Source = source;
            _root = root;
            Fake = fake;
        }

        public TempRepo Source { get; }

        public FakeGitLab Fake { get; }

        public FakeGitLabProject Project { get; private set; } = null!;

        public FakeGitLabProject Fork { get; private set; } = null!;

        public FakeGitLabProject Public { get; private set; } = null!;

        public FakeGitLabProject Secret { get; private set; } = null!;

        public string Ada { get; private set; } = string.Empty;

        public string AdaLegacyApi { get; private set; } = string.Empty;

        public string PushOnly { get; private set; } = string.Empty;

        public string AdaExpiring { get; private set; } = string.Empty;

        public string ReadUser { get; private set; } = string.Empty;

        public string DownloadOnly { get; private set; } = string.Empty;

        public string Octo { get; private set; } = string.Empty;

        public static async Task<GitLab> CreateAsync()
        {
            var source = await TempRepo.CreateAsync("gitlab-git-source");
            await source.CommitFileAsync("README.md", "web\n", "Initial commit");
            foreach (var branch in new[] { "feature/one", "fix" })
            {
                await source.CreateBranchAsync(branch, "main");
                await source.CheckoutAsync(branch);
                await source.CommitFileAsync(branch.Replace('/', '-') + ".txt", branch + "\n", "Work on " + branch);
            }

            await source.CheckoutAsync("main");
            var root = new TempDirectory("fake gitlab git");
            var lab = new GitLab(source, root, await FakeGitLab.StartAsync(root.Path, ct: Cancelled));
            var fake = lab.Fake;
            fake.AddUser("ada", 2001, Scenarios.AdaName);
            fake.AddUser("octo", 2002, Scenarios.OctoName);
            lab.Ada = fake.AddToken("ada");
            lab.ReadUser = fake.AddToken("ada", FakeGitLabTokenKind.Legacy, [FakeGitLabPermissions.ReadUser]);
            lab.DownloadOnly = fake.AddToken("ada", permissions: [FakeGitLabPermissions.CodeDownload]);
            lab.AdaExpiring = fake.AddToken("ada");
            lab.AdaLegacyApi = fake.AddToken("ada", FakeGitLabTokenKind.Legacy);
            lab.PushOnly = fake.AddToken("ada", permissions: [FakeGitLabPermissions.CodePush]);
            lab.Octo = fake.AddToken("octo");
            fake.Grant("g", "ada", 30);
            fake.Grant("g", "octo", 30);
            lab.Project = await fake.AddProjectAsync("g/s/p", source.Path, ct: Cancelled);
            lab.Fork = await fake.AddProjectAsync("ada/p", source.Path, forkOf: lab.Project, ct: Cancelled);
            lab.Public = await fake.AddProjectAsync("pub/open", source.Path, "public", ct: Cancelled);
            lab.Secret = await fake.AddProjectAsync("octo/secret", source.Path, ct: Cancelled);
            await source.GitAsync("push", "--quiet", lab.Fork.BarePath!, "fix:refs/heads/fork-fix");
            fake.AddMergeRequest(lab.Project, "Add feature one", "ada", "feature/one", "main");
            fake.AddMergeRequest(lab.Project, "Fix it from the fork", "ada", "fork-fix", "main", source: lab.Fork);
            return lab;
        }

        /// <summary>The project at <paramref name="path"/> on the fake with <paramref name="user"/> and <paramref name="token"/> in the URL.</summary>
        public string Url(string path, string user, string token) => $"http://{user}:{token}@127.0.0.1:{Fake.Port}/{path}";

        /// <summary>A clone of g/s/p at its first path with <paramref name="token"/> in its origin's URL.</summary>
        public async Task<string> CloneAsync(string name, string token)
        {
            var folder = Source.SiblingPath(name);
            var cloned = await Source.GitAllowingFailureAsync("clone", "--quiet", Url("g/s/p.git", "x-access-token", token), folder);
            Assert.True(cloned.Succeeded, cloned.StandardError);
            await GitInAsync(Source, folder, "config", "user.name", "Test Author");
            await GitInAsync(Source, folder, "config", "user.email", "test@example.com");
            return folder;
        }

        public async Task<string> TipAsync(string branch) => (await Source.GitAsync("rev-parse", "refs/heads/" + branch)).StandardOutput.Trim();

        public async Task<string> ForkTipAsync(string branch) => (await Source.GitAsync("--git-dir", Fork.BarePath!, "rev-parse", "refs/heads/" + branch)).StandardOutput.Trim();

        public async Task<string?> RefAsync(FakeGitLabProject project, string name)
        {
            var result = await Source.GitAllowingFailureAsync("--git-dir", project.BarePath!, "rev-parse", "--verify", "--quiet", name);
            return result.Succeeded ? result.StandardOutput.Trim() : null;
        }

        public async ValueTask DisposeAsync()
        {
            await Fake.DisposeAsync();
            _root.Dispose();
            Source.Dispose();
        }
    }
}
