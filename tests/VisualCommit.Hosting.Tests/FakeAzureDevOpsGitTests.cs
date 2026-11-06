using System.Net.Http.Headers;
using System.Text;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The fake of Azure DevOps serving git on port 0 with real git (docs/phase-7b-design.md 3.5, 3.6),
/// on a served copy of "hosting7b" (<see cref="Scenarios.ServeHosting7bAsync"/>) with no credential
/// helper or askpass program: the token goes in the URL. Clones with <c>x-access-token</c> and with
/// the organization as the user, at <c>{o}/_git/{r}</c> and through a space's <c>%20</c>; no final
/// <c>.git</c> stripped; the merge refs and no <c>/head</c>; revoked, expired and another
/// organization's tokens; a non-contributor's push; a public repository; and the scenario's half.
/// </summary>
public sealed class FakeAzureDevOpsGitTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_scenarios_half_is_seeded_and_its_merge_refs_are_written_at_the_first_listing()
    {
        var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
        var fake = served.AzureDevOps;
        Assert.Equal("azdo_FAKE-Fernhill-Labs-1", served.AzureDevOpsToken);

        var mobile = fake.Repository("fernhill-labs", "mobile app", "MOBILE-APP");
        Assert.Equal("Fernhill-Labs/Mobile App/_git/mobile-app", mobile.FolderName);
        Assert.Equal(Path.Combine(copy.SiblingPath(Scenarios.AzureDevOpsFolder), "Fernhill-Labs", "Mobile App", "_git", "mobile-app"), mobile.BarePath);
        var fork = fake.Repository("Fernhill-Labs", "Mobile App", "mobile-app-fork");
        Assert.Same(mobile, fork.ForkOf);
        var manifest = fake.Repository("harbor-co", "Docks", "manifest");
        Assert.Equal(("Docks", false), (manifest.Project.Name, manifest.Project.IsPublic));

        var pulls = fake.PullRequests(mobile);
        Assert.Equal(
            [(11, "Add the sign-in page", "Ada Lovelace", "feature/sign-in", false, (FakeAzureRepository?)null),
             (12, "Write the guide", "Octo Cat", "docs/guide", true, null),
             (13, "Fix a typo in the greeting", "Ada Lovelace", "fix-typo", false, fork)],
            pulls.Select(pull => (pull.Id, pull.Title, pull.Creator.DisplayName, pull.SourceBranch, pull.IsDraft, pull.Fork)));
        Assert.All(pulls, pull => Assert.Equal(("main", FakeAzurePullRequestStatus.Active, "queued"), (pull.TargetBranch, pull.Status, pull.MergeStatus)));
        var five = Assert.Single(fake.PullRequests(manifest));
        Assert.Equal((5, Scenarios.Hosting7bManifestRequestTitle, "Test Author", "harbor-co"), (five.Id, five.Title, five.Creator.DisplayName, five.Creator.Organization.Name));
        Assert.Equal("ada.lovelace@example.com", pulls[0].Creator.UniqueName);

        // The API, over TCP, lists Fernhill-Labs' repositories in their order and signs Ada in.
        using var client = Loopback();
        var listed = await GetAsync(client, fake, "Fernhill-Labs/_apis/git/repositories?api-version=7.1", served.AzureDevOpsToken);
        Assert.Contains("\"name\":\"mobile-app\"", listed, StringComparison.Ordinal);
        Assert.True(
            listed.IndexOf("\"name\":\"mobile-app\"", StringComparison.Ordinal) < listed.IndexOf("\"name\":\"mobile-app-fork\"", StringComparison.Ordinal)
            && listed.IndexOf("\"name\":\"mobile-app-fork\"", StringComparison.Ordinal) < listed.IndexOf("\"name\":\"sandbox\"", StringComparison.Ordinal)
            && listed.IndexOf("\"name\":\"sandbox\"", StringComparison.Ordinal) < listed.IndexOf($"\"name\":\"{Scenarios.Hosting7bLongRepository}\"", StringComparison.Ordinal),
            listed);
        Assert.DoesNotContain("manifest", listed, StringComparison.Ordinal);
        Assert.Contains($"\"remoteUrl\":\"http://Fernhill-Labs@127.0.0.1:{fake.Port}/Fernhill-Labs/Mobile%20App/_git/mobile-app\"", listed, StringComparison.Ordinal);
        Assert.Contains("\"providerDisplayName\":\"Ada Lovelace\"", await GetAsync(client, fake, "Fernhill-Labs/_apis/connectionData?api-version=7.1-preview.1", served.AzureDevOpsToken), StringComparison.Ordinal);

        // git's first listing writes the three merge refs, !13's from the fork's branch.
        var listing = await copy.GitAllowingFailureAsync("ls-remote", Url(fake, "x-access-token", served.AzureDevOpsToken, "Fernhill-Labs", "Mobile App", "mobile-app"));
        Assert.True(listing.Succeeded, listing.StandardError);
        Assert.Equal(["refs/pull/11/merge", "refs/pull/12/merge", "refs/pull/13/merge"], RefNames(listing.StandardOutput).Where(name => name.StartsWith("refs/pull/", StringComparison.Ordinal)));
        Assert.Equal(await RevAsync(copy, mobile.BarePath, "refs/heads/main"), await RevAsync(copy, mobile.BarePath, "refs/pull/13/merge^1"));
        Assert.Equal(await RevAsync(copy, fork.BarePath, "refs/heads/fix-typo"), await RevAsync(copy, mobile.BarePath, "refs/pull/13/merge^2"));
        Assert.All(fake.PullRequests(mobile), pull => Assert.Equal("succeeded", pull.MergeStatus));
        Assert.Null(await RevAsync(copy, mobile.BarePath, "refs/fake/source/13"));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_private_repository_is_cloned_with_x_access_token_or_the_organization_as_user_at_both_paths_but_not_with_git()
    {
        var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
        var fake = served.AzureDevOps;
        var token = served.AzureDevOpsToken;
        var mobile = fake.Repository("Fernhill-Labs", "Mobile App", "mobile-app");
        var adaLogin = $"Fernhill-Labs/{fake.PullRequests(mobile)[0].Creator.Id}";

        var clone = copy.SiblingPath("clone");
        var cloned = await copy.GitAllowingFailureAsync("clone", "--quiet", Url(fake, "x-access-token", token, "Fernhill-Labs", "Mobile App", "mobile-app"), clone);
        Assert.True(cloned.Succeeded, cloned.StandardError);
        Assert.Equal("Add the docs folder", await GitInAsync(copy, clone, "log", "-1", "--format=%s"));
        var signedIn = fake.GitRequests.Where(request => request.Status == 200).ToList();
        Assert.NotEmpty(signedIn);
        Assert.All(signedIn, request => Assert.Equal((adaLogin, "x-access-token"), (request.UserName, request.GivenUserName)));
        Assert.Contains(fake.GitRequests, request => request.Status == 401 && request.UserName is null);
        Assert.All(fake.GitRequests, request => Assert.StartsWith("/Fernhill-Labs/Mobile%20App/_git/mobile-app/", request.Path, StringComparison.Ordinal));

        // The organization as the user (the service's own remote URL), the names in another case: a
        // whole clone, its pack fetched as Ada.
        await AssertClonedAsync(copy, fake, $"http://Fernhill-Labs:{token}@127.0.0.1:{fake.Port}/fernhill-labs/mobile%20app/_git/MOBILE-APP", "/fernhill-labs/mobile%20app/_git/MOBILE-APP/", "as-organization", adaLogin, "Fernhill-Labs");

        // A repository named as its project is served at {o}/_git/{r} too (MEASURED r5 6.1), cloned
        // whole through that path; and listed at its own.
        await fake.AddRepositoryAsync("Fernhill-Labs", "Mobile App", "Mobile App", sourceRepository: mobile.BarePath, ct: Cancelled);
        await AssertClonedAsync(copy, fake, $"http://x-access-token:{token}@127.0.0.1:{fake.Port}/Fernhill-Labs/_git/Mobile%20App", "/Fernhill-Labs/_git/Mobile%20App/", "short-path", adaLogin, "x-access-token");
        var listed = await copy.GitAllowingFailureAsync("ls-remote", Url(fake, "x-access-token", token, "Fernhill-Labs", "Mobile App", "Mobile App"), "refs/heads/main");
        Assert.True(listed.Succeeded, listed.StandardError);
        Assert.StartsWith(await RevAsync(copy, mobile.BarePath, "refs/heads/main"), listed.StandardOutput, StringComparison.Ordinal);

        // Azure DevOps strips no final .git (MEASURED r5 6.1).
        var suffixed = await copy.GitAllowingFailureAsync("ls-remote", Url(fake, "x-access-token", token, "Fernhill-Labs", "Mobile App", "mobile-app.git"));
        Assert.False(suffixed.Succeeded);
        Assert.Equal(404, fake.GitRequests[^1].Status);
        Assert.Empty(fake.Requests);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Git_sees_the_merge_ref_and_no_head_and_the_merge_ref_goes_once_its_pull_request_is_abandoned()
    {
        var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
        var fake = served.AzureDevOps;
        var url = Url(fake, "x-access-token", served.AzureDevOpsToken, "Fernhill-Labs", "Mobile App", "mobile-app");
        var bare = fake.Repository("Fernhill-Labs", "Mobile App", "mobile-app").BarePath;

        var listed = await copy.GitAllowingFailureAsync("ls-remote", url);
        Assert.True(listed.Succeeded, listed.StandardError);
        Assert.Contains("refs/pull/11/merge", RefNames(listed.StandardOutput));
        Assert.DoesNotContain(RefNames(listed.StandardOutput), name => name.EndsWith("/head", StringComparison.Ordinal) && name.StartsWith("refs/pull/", StringComparison.Ordinal));
        var head = await copy.GitAllowingFailureAsync("fetch", "--no-tags", url, "refs/pull/11/head");
        Assert.False(head.Succeeded);
        Assert.Contains("couldn't find remote ref", head.StandardError, StringComparison.Ordinal);

        // The merge commit: target then source as parents, the service's message, the creator at the harness's time.
        Assert.Equal(await RevAsync(copy, bare, "refs/heads/main"), await RevAsync(copy, bare, "refs/pull/11/merge^1"));
        Assert.Equal(await RevAsync(copy, bare, "refs/heads/feature/sign-in"), await RevAsync(copy, bare, "refs/pull/11/merge^2"));
        Assert.Equal(await RevAsync(copy, bare, "refs/heads/feature/sign-in^{tree}"), await RevAsync(copy, bare, "refs/pull/11/merge^{tree}"));
        Assert.Equal(
            $"Merge pull request 11 from feature/sign-in into main|Ada Lovelace <ada.lovelace@example.com>|Ada Lovelace|{AppClocks.HarnessTime.ToUnixTimeSeconds()}|{AppClocks.HarnessTime.ToUnixTimeSeconds()}",
            await GitInAsync(copy, bare, "log", "-1", "--format=%s|%an <%ae>|%cn|%at|%ct", "refs/pull/11/merge"));

        // Abandoned through the API (over TCP), the merge ref goes at git's next listing; the others stay.
        using var client = Loopback();
        using var patch = new HttpRequestMessage(HttpMethod.Patch, new Uri(fake.Endpoints.WebBase, "Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests/11?api-version=7.1"))
        {
            Content = new StringContent("{\"status\":\"abandoned\"}", Encoding.UTF8, "application/json"),
        };
        patch.Headers.Authorization = Basic(served.AzureDevOpsToken);
        using (var abandoned = await client.SendAsync(patch, Cancelled))
        {
            Assert.Equal(200, (int)abandoned.StatusCode);
        }

        var after = await copy.GitAllowingFailureAsync("ls-remote", url);
        Assert.True(after.Succeeded, after.StandardError);
        Assert.Equal(["refs/pull/12/merge", "refs/pull/13/merge"], RefNames(after.StandardOutput).Where(name => name.StartsWith("refs/pull/", StringComparison.Ordinal)));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task Revoked_expired_and_another_organizations_tokens_get_401()
    {
        var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
        var fake = served.AzureDevOps;
        var ada = fake.User("fernhill-labs", "ADA LOVELACE");
        Assert.Same(fake.PullRequests(fake.Repository("Fernhill-Labs", "Mobile App", "mobile-app"))[0].Creator, ada);

        // harbor-co's token reads harbor-co's manifest, not Fernhill-Labs' mobile-app.
        var harborToken = fake.AddToken("harbor-co", fake.AddUser("harbor-co", "Dock Hand"));
        var own = await copy.GitAllowingFailureAsync("ls-remote", Url(fake, "x-access-token", harborToken, "harbor-co", "Docks", "manifest"));
        Assert.True(own.Succeeded, own.StandardError);
        await AssertRefusedAsync(copy, fake, Url(fake, "x-access-token", harborToken, "Fernhill-Labs", "Mobile App", "mobile-app"));
        await AssertRefusedAsync(copy, fake, Url(fake, "harbor-co", served.AzureDevOpsToken, "harbor-co", "Docks", "manifest"), "harbor-co");

        var expiring = fake.AddToken("Fernhill-Labs", ada);
        Assert.True((await copy.GitAllowingFailureAsync("ls-remote", Url(fake, "x-access-token", expiring, "Fernhill-Labs", "Mobile App", "mobile-app"))).Succeeded);
        fake.Expire(expiring);
        await AssertRefusedAsync(copy, fake, Url(fake, "x-access-token", expiring, "Fernhill-Labs", "Mobile App", "mobile-app"));

        fake.Revoke(served.AzureDevOpsToken);
        await AssertRefusedAsync(copy, fake, Url(fake, "x-access-token", served.AzureDevOpsToken, "Fernhill-Labs", "Mobile App", "mobile-app"));

        // git's first request, with no credential: 401 with the realm's form with .app. (both MEASURED
        // g1 G4.1 for git's agent). The service sends three challenges (Bearer, Basic, TFS-Federated) and
        // a text/html body with a BOM, and a refused credential's realm without .app. (MEASURED
        // 2026-11-05); the one Basic challenge, its one realm and the text/plain body are the kept
        // difference: AuthRemoteServer's shared 401, and the helper reads no wwwauth[] (D370).
        using var client = Loopback();
        using var refusal = await client.GetAsync(new Uri($"http://127.0.0.1:{fake.Port}/Fernhill-Labs/Mobile%20App/_git/mobile-app/info/refs?service=git-upload-pack"), Cancelled);
        Assert.Equal(401, (int)refusal.StatusCode);
        Assert.Equal(["Basic realm=\"https://tfsprodfake.app.visualstudio.com/\""], refusal.Headers.NonValidated["WWW-Authenticate"]);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_non_contributors_push_gets_403_and_a_public_repository_is_cloned_anonymously_but_asks_for_credentials_to_push()
    {
        var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
        var fake = served.AzureDevOps;
        var mobile = fake.Repository("Fernhill-Labs", "Mobile App", "mobile-app");
        var pulls = fake.PullRequests(mobile);
        var (ada, octo) = (pulls[0].Creator, pulls[1].Creator);

        mobile.Contributors = [octo];
        var push = await copy.GitAllowingFailureAsync("push", Url(fake, "x-access-token", served.AzureDevOpsToken, "Fernhill-Labs", "Mobile App", "mobile-app"), "local-work:refs/heads/local-work");
        Assert.False(push.Succeeded);
        Assert.Contains(fake.GitRequests, request => request.Status == 403 && request.UserName == $"Fernhill-Labs/{ada.Id}");
        Assert.Null(await RevAsync(copy, mobile.BarePath, "refs/heads/local-work"));

        // A read-only token's push is refused too; a contributor's write token's goes through.
        mobile.Contributors = [];
        var readOnly = fake.AddToken("Fernhill-Labs", ada, codeWrite: false);
        var readOnlyPush = await copy.GitAllowingFailureAsync("push", Url(fake, "x-access-token", readOnly, "Fernhill-Labs", "Mobile App", "mobile-app"), "local-work:refs/heads/local-work");
        Assert.False(readOnlyPush.Succeeded);
        Assert.Contains(fake.GitRequests, request => request.Status == 403 && request.UserName == $"Fernhill-Labs/{ada.Id}#read");
        var allowed = await copy.GitAllowingFailureAsync("push", "--quiet", Url(fake, "x-access-token", served.AzureDevOpsToken, "Fernhill-Labs", "Mobile App", "mobile-app"), "local-work:refs/heads/local-work");
        Assert.True(allowed.Succeeded, allowed.StandardError);

        // A public project's repository: cloned with no credential; a push asks for one (401).
        fake.AddProject("Fernhill-Labs", "Open Source", isPublic: true);
        await fake.AddRepositoryAsync("Fernhill-Labs", "Open Source", "tools", sourceRepository: mobile.BarePath, ct: Cancelled);
        var anonymousUrl = $"http://127.0.0.1:{fake.Port}/Fernhill-Labs/Open%20Source/_git/tools";
        var clone = copy.SiblingPath("anonymous");
        var cloned = await copy.GitAllowingFailureAsync("clone", "--quiet", anonymousUrl, clone);
        Assert.True(cloned.Succeeded, cloned.StandardError);
        Assert.All(fake.GitRequests.Where(request => request.Path.Contains("/Open%20Source/", StringComparison.Ordinal)), request => Assert.Equal((200, (string?)null), (request.Status, request.UserName)));
        var anonymousPush = await copy.GitAllowingFailureAsync("push", anonymousUrl, "local-work:refs/heads/local-work");
        Assert.False(anonymousPush.Succeeded);
        Assert.Contains(fake.GitRequests, request => request.Path.Contains("/Open%20Source/", StringComparison.Ordinal) && request.Query.Contains("service=git-receive-pack", StringComparison.Ordinal) && request.Status == 401);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_repository_named_with_a_final_dot_git_is_public_pushed_to_and_given_its_merge_ref()
    {
        var copy = await Scenarios.Hosting7bAsync();
        await using var served = await Scenarios.ServeHosting7bAsync(copy, 0, 0, 0, ownsCopy: true, ct: Cancelled);
        var fake = served.AzureDevOps;
        var mobile = fake.Repository("Fernhill-Labs", "Mobile App", "mobile-app");
        var ada = fake.User("Fernhill-Labs", "Ada Lovelace");

        // Azure DevOps takes a final .git in a name (r5 6.1); git's server names it without (AuthRemoteServer).
        fake.AddProject("Fernhill-Labs", "Open Source", isPublic: true);
        var tools = await fake.AddRepositoryAsync("Fernhill-Labs", "Open Source", "tools.git", sourceRepository: mobile.BarePath, ct: Cancelled);
        var pull = fake.AddPullRequest(tools, "Add the sign-in page", ada, "feature/sign-in", "main");
        await Assert.ThrowsAsync<ArgumentException>(() => fake.AddRepositoryAsync("Fernhill-Labs", "Open Source", "TOOLS", ct: Cancelled));

        // Cloned with no credential, as a public project's.
        var anonymousUrl = $"http://127.0.0.1:{fake.Port}/Fernhill-Labs/Open%20Source/_git/tools.git";
        var cloned = await copy.GitAllowingFailureAsync("clone", "--quiet", anonymousUrl, copy.SiblingPath("tools-anonymous"));
        Assert.True(cloned.Succeeded, cloned.StandardError);

        // Listed signed in with its merge ref, which the sync wrote.
        var url = Url(fake, "x-access-token", served.AzureDevOpsToken, "Fernhill-Labs", "Open Source", "tools.git");
        var listing = await copy.GitAllowingFailureAsync("ls-remote", url);
        Assert.True(listing.Succeeded, listing.StandardError);
        Assert.Contains($"refs/pull/{pull.Id}/merge", RefNames(listing.StandardOutput));
        Assert.Equal(await RevAsync(copy, tools.BarePath, "refs/heads/feature/sign-in"), await RevAsync(copy, tools.BarePath, $"refs/pull/{pull.Id}/merge^2"));

        // A contributor's write token pushes.
        var push = await copy.GitAllowingFailureAsync("push", "--quiet", url, "local-work:refs/heads/local-work");
        Assert.True(push.Succeeded, push.StandardError);
        Assert.NotNull(await RevAsync(copy, tools.BarePath, "refs/heads/local-work"));
        Assert.Empty(fake.Errors);
    }

    /// <summary>Clones <paramref name="url"/> into a new folder: its head is main's last commit, a pack was fetched, and every 200 git request on <paramref name="path"/> signed in as <paramref name="login"/> with <paramref name="givenUser"/>.</summary>
    private static async Task AssertClonedAsync(TempRepo copy, FakeAzureDevOps fake, string url, string path, string folder, string login, string givenUser)
    {
        var clone = copy.SiblingPath(folder);
        var cloned = await copy.GitAllowingFailureAsync("clone", "--quiet", url, clone);
        Assert.True(cloned.Succeeded, cloned.StandardError);
        Assert.Equal("Add the docs folder", await GitInAsync(copy, clone, "log", "-1", "--format=%s"));
        var signedIn = fake.GitRequests.Where(request => request.Status == 200 && request.Path.StartsWith(path, StringComparison.Ordinal)).ToList();
        Assert.Contains(signedIn, request => request.Method == "POST" && request.Path.EndsWith("/git-upload-pack", StringComparison.Ordinal));
        Assert.All(signedIn, request => Assert.Equal((login, givenUser), (request.UserName, request.GivenUserName)));
    }

    private static async Task AssertRefusedAsync(TempRepo copy, FakeAzureDevOps fake, string url, string givenUser = "x-access-token")
    {
        var refused = await copy.GitAllowingFailureAsync("ls-remote", url);
        Assert.False(refused.Succeeded);
        Assert.Equal((401, (string?)null, givenUser), (fake.GitRequests[^1].Status, fake.GitRequests[^1].UserName, fake.GitRequests[^1].GivenUserName));
    }

    /// <summary><c>http://{user}:{token}@127.0.0.1:&lt;port&gt;/{o}/{p}/_git/{r}</c>, the project and repository escaped.</summary>
    private static string Url(FakeAzureDevOps fake, string user, string token, string organization, string project, string repository) =>
        $"http://{user}:{token}@127.0.0.1:{fake.Port}/{organization}/{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(repository)}";

    /// <summary>A client of the fake's TCP port alone: no proxy, no redirect followed.</summary>
    private static HttpClient Loopback() => new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false });

    private static AuthenticationHeaderValue Basic(string token) => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + token)));

    private static async Task<string> GetAsync(HttpClient client, FakeAzureDevOps fake, string pathAndQuery, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(fake.Endpoints.WebBase, pathAndQuery));
        request.Headers.Authorization = Basic(token);
        using var response = await client.SendAsync(request, Cancelled);
        Assert.Equal(200, (int)response.StatusCode);
        return await response.Content.ReadAsStringAsync(Cancelled);
    }

    private static List<string> RefNames(string listing) =>
        [.. listing.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r').Split('\t')[^1])];

    private static async Task<string> GitInAsync(TempRepo repo, string folder, params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = folder, Environment = repo.Environment };
        return (await repo.Runner.RunAsync(command, Cancelled)).EnsureSuccess(command).StandardOutput.Trim();
    }

    private static async Task<string?> RevAsync(TempRepo repo, string bare, string name)
    {
        var result = await repo.GitAllowingFailureAsync("--git-dir", bare, "rev-parse", "--verify", "--quiet", name);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }
}
