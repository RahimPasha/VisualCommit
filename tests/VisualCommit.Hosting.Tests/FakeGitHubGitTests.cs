using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The fake of GitHub serving git on port 0 (docs/phase-7a-design.md 2.2, 2.3, 2.9), with real git
/// and no credential helper or askpass program: the token goes in the URL, the fake's own test of
/// what the app's helper later gives git. A revoked token, a push by someone who may not push, an
/// anonymous clone of a public repository, and a pull request's head fetched into
/// <c>FETCH_HEAD</c> only.
/// </summary>
public sealed class FakeGitHubGitTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_private_repository_is_cloned_with_a_token_and_refused_once_it_is_revoked()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, port: 0, ct: Cancelled);
        var fake = served.GitHub;
        var clone = hello.SiblingPath("clone");

        var cloned = await hello.GitAllowingFailureAsync("clone", "--quiet", WithToken(fake.Port, "octo/hello", served.OctoToken), clone);

        Assert.True(cloned.Succeeded, cloned.StandardError);
        Assert.Equal("Add the docs folder", await GitInAsync(hello, clone, "log", "-1", "--format=%s"));
        var signedIn = fake.GitRequests.Where(request => request.Status == 200).ToList();
        Assert.NotEmpty(signedIn);
        Assert.All(signedIn, request => Assert.Equal(("octo", "x-access-token"), (request.UserName, request.GivenUserName)));
        Assert.Contains(fake.GitRequests, request => request.Status == 401 && request.UserName is null);
        Assert.Empty(fake.Requests);

        fake.Revoke(served.OctoToken);
        var refused = await hello.GitAllowingFailureAsync("ls-remote", WithToken(fake.Port, "octo/hello", served.OctoToken));
        Assert.False(refused.Succeeded);
        Assert.Equal(401, fake.GitRequests[^1].Status);
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_login_that_may_not_push_gets_403_and_a_public_repository_is_cloned_anonymously()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, port: 0, ct: Cancelled);
        var fake = served.GitHub;

        // Ada reads octo/hello but may not push to it.
        var push = await hello.GitAllowingFailureAsync("push", WithToken(fake.Port, "octo/hello", served.AdaToken), "local-work:refs/heads/local-work");
        Assert.False(push.Succeeded);
        Assert.Contains(fake.GitRequests, request => request.Status == 403 && request.UserName == "ada");
        Assert.Null(await RefAsync(hello, fake.Repository("octo/hello").BarePath!, "refs/heads/local-work"));

        // acme/tool is public: cloned without any credentials, as GitHub serves it.
        var clone = hello.SiblingPath("anonymous");
        var cloned = await hello.GitAllowingFailureAsync("clone", "--quiet", Scenarios.HostingUrl("acme/tool", fake.Port), clone);
        Assert.True(cloned.Succeeded, cloned.StandardError);
        Assert.Equal("Initial commit", await GitInAsync(hello, clone, "log", "-1", "--format=%s"));
        Assert.All(fake.GitRequests.Where(request => request.Path.StartsWith("/acme/", StringComparison.Ordinal)), request => Assert.Equal((200, (string?)null), (request.Status, request.UserName)));
        Assert.Empty(fake.Errors);
    }

    [Fact]
    public async Task A_pull_requests_head_is_fetched_into_FETCH_HEAD_only()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, port: 0, ct: Cancelled);
        var tool = served.Tool;
        var before = await GitInAsync(hello, tool.Path, "for-each-ref", "--format=%(objectname) %(refname)");
        var configBefore = File.ReadAllText(Path.Combine(tool.Path, ".git", "config"));

        var fetched = await tool.GitAllowingFailureAsync("fetch", "--quiet", "--no-tags", "upstream", "refs/pull/7/head");

        Assert.True(fetched.Succeeded, fetched.StandardError);
        Assert.Equal(before, await GitInAsync(hello, tool.Path, "for-each-ref", "--format=%(objectname) %(refname)"));
        Assert.Equal(configBefore, File.ReadAllText(Path.Combine(tool.Path, ".git", "config")));
        var head = File.ReadAllText(Path.Combine(tool.Path, ".git", "FETCH_HEAD"));
        Assert.StartsWith(await GitInAsync(hello, tool.Path, "rev-parse", "origin/fix-typo"), head, StringComparison.Ordinal);
        Assert.Contains("'refs/pull/7/head'", head, StringComparison.Ordinal);
        Assert.Empty(served.GitHub.Errors);
    }

    [Fact]
    public async Task A_push_to_a_pull_requests_head_branch_moves_its_pull_ref_at_the_next_listing()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, port: 0, ct: Cancelled);
        var fake = served.GitHub;
        var bare = fake.Repository("octo/hello").BarePath!;

        // octo pushes a new commit onto feature/login, #1's head; git's next ref listing syncs refs/pull/1/head.
        await hello.CheckoutAsync("feature/login");
        var moved = await hello.CommitFileAsync("src/login.txt", "login form\nremember me\n", "Remember the user");
        var push = await hello.GitAllowingFailureAsync("push", "--quiet", WithToken(fake.Port, "octo/hello", served.OctoToken), "feature/login");
        Assert.True(push.Succeeded, push.StandardError);
        var listed = await hello.GitAllowingFailureAsync("ls-remote", WithToken(fake.Port, "octo/hello", served.OctoToken), "refs/pull/1/head");

        Assert.True(listed.Succeeded, listed.StandardError);
        Assert.StartsWith(moved, listed.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(moved, await RefAsync(hello, bare, "refs/pull/1/head"));
        Assert.Equal(moved, fake.PullRequests("octo/hello").Single(pull => pull.Number == 1).HeadSha);
        Assert.Empty(fake.Errors);
    }

    private static string WithToken(int port, string ownerAndName, string token) =>
        Scenarios.HostingUrl(ownerAndName, port).Replace("http://", $"http://x-access-token:{token}@", StringComparison.Ordinal);

    private static async Task<string> GitInAsync(TempRepo repo, string folder, params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = folder, Environment = repo.Environment };
        return (await repo.Runner.RunAsync(command, Cancelled)).EnsureSuccess(command).StandardOutput.Trim();
    }

    private static async Task<string?> RefAsync(TempRepo repo, string bare, string name)
    {
        var result = await repo.GitAllowingFailureAsync("--git-dir", bare, "rev-parse", "--verify", "--quiet", name);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }
}
