using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The push guard on remotes GitLab and Azure DevOps recognise (ruling 14, D279,
/// docs/phase-7b-design.md 8.4; r7 9): only the rows that push, against bare remotes. A request ref
/// that cannot exist on the provider counts as no source, so a branch whose tracking ref a request
/// line could also fill is pushed, and the next fetch still succeeds (r7 9's F3). The refusals and
/// the ref rules themselves are <c>PullRequestRefsTests</c>' on strings; 7a's GitHub rows are
/// <see cref="PushGuardGitHubTests"/>'.
/// </summary>
public sealed class PushGuardProvidersTests
{
    private static readonly AccountKey MyOrg = new(HostingProvider.AzureDevOps, "dev.azure.com", "myorg");

    [Fact]
    public async Task On_GitLab_a_branch_named_merge_requests_slash_a_word_is_pushed_and_the_next_fetch_succeeds()
    {
        using var repo = await RemoteWithLineAsync("+refs/merge-requests/*/head:refs/remotes/origin/merge-requests/*", "merge-requests/foo", "refs/merge-requests/5/head");
        var stub = new HostingSessionStub(
            new GitCoverage(new AccountKey(HostingProvider.GitLab, "gitlab.com", "ada"), new Uri("https://gitlab.com/")) { GitTwin = true, HelperUserIsAccounts = true },
            null);
        stub.RecognisedUrls.Add(await UrlOfAsync(repo, "origin"), HostingProvider.GitLab);

        await PushesAsync(repo, stub, "origin", "merge-requests/foo");

        var fetch = await repo.GitAllowingFailureAsync("fetch", "--quiet", "origin");
        Assert.True(fetch.Succeeded, fetch.StandardError);
        Assert.NotNull(await TrackingAsync(repo, "refs/remotes/origin/merge-requests/5"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task On_Azure_DevOps_a_branch_named_pr_slash_a_word_beside_the_merge_line_is_pushed_on_the_accounts_remote_and_another_organizations(bool accounts)
    {
        // Recognising the remote makes no request and needs no account (D279): another
        // organization's remote, which no binding covers, has the same rule.
        using var repo = await RemoteWithLineAsync("+refs/pull/*/merge:refs/remotes/origin/pr/*", "pr/fix", "refs/pull/7/merge");
        var stub = new HostingSessionStub(new GitCoverage(MyOrg, new Uri("https://dev.azure.com/")) { Organization = "myorg", OrganizationHost = true, HelperUserIsAccounts = true }, null);
        if (accounts)
        {
            stub.RepositoryBindings = [new CredentialBinding("https://dev.azure.com/myorg/p/_git/r", MyOrg) { SharedOriginOwner = "myorg" }];
        }

        stub.RecognisedUrls.Add(await UrlOfAsync(repo, "origin"), HostingProvider.AzureDevOps);

        await PushesAsync(repo, stub, "origin", "pr/fix");

        var fetch = await repo.GitAllowingFailureAsync("fetch", "--quiet", "origin");
        Assert.True(fetch.Succeeded, fetch.StandardError);
        Assert.NotNull(await TrackingAsync(repo, "refs/remotes/origin/pr/7"));
    }

    [Fact]
    public async Task On_Azure_DevOps_a_branch_named_pr_slash_a_number_beside_a_head_line_is_pushed()
    {
        // Azure DevOps has no refs/pull/<n>/head (r5 0.4, MEASURED), so GitHub's line fills nothing there.
        using var repo = await RemoteWithLineAsync("+refs/pull/*/head:refs/remotes/origin/pr/*", "pr/7");
        var stub = new HostingSessionStub(new GitCoverage(MyOrg, new Uri("https://dev.azure.com/")) { Organization = "myorg", OrganizationHost = true, HelperUserIsAccounts = true }, null);
        stub.RecognisedUrls.Add(await UrlOfAsync(repo, "origin"), HostingProvider.AzureDevOps);

        await PushesAsync(repo, stub, "origin", "pr/7");
    }

    /// <summary>Pushes <paramref name="branch"/> to its upstream through the app: no dialog, and the remote's branch is the local head.</summary>
    private static async Task PushesAsync(TempRepo repo, HostingSessionStub stub, string remote, string branch)
    {
        using var app = await OperationsHarness.OpenAsync(repo, prompts: null, hosting: stub);
        var head = await repo.HeadAsync();

        await app.Operations.PushBranchAsync("refs/heads/" + branch);
        await app.WaitForIdleAsync();

        Assert.Empty(app.Dialogs.Errors);
        var listed = (await repo.GitAsync("ls-remote", remote, "refs/heads/" + branch)).StandardOutput.Trim();
        Assert.Equal(head, listed.Split('\t')[0]);
        app.AssertScriptFollowed();
    }

    /// <summary>
    /// A repository whose bare <c>origin</c> has <paramref name="serverRefs"/> and the request
    /// <paramref name="line"/> beside the default one, fetched, with <paramref name="branch"/>
    /// published there and tracked, and one more local commit on it to push.
    /// </summary>
    private static async Task<TempRepo> RemoteWithLineAsync(string line, string branch, params string[] serverRefs)
    {
        var repo = await TempRepo.CreateAsync("guard");
        try
        {
            await repo.CommitFileAsync("README.md", "# guard\n", "Initial commit");
            await repo.AddBareRemoteAsync("origin", relative: true);
            await repo.PushAsync("origin", setUpstream: true, "main");
            if (serverRefs.Length > 0)
            {
                await repo.PushAsync("origin", setUpstream: false, [.. serverRefs.Select(serverRef => "main:" + serverRef)]);
            }

            await repo.GitAsync("config", "--add", "remote.origin.fetch", line);
            await repo.GitAsync("fetch", "--quiet", "origin");
            await repo.GitAsync("switch", "--quiet", "--create", branch);
            await repo.GitAsync("push", "--quiet", "--set-upstream", "origin", $"refs/heads/{branch}:refs/heads/{branch}");
            await repo.CommitFileAsync("change.txt", "changed\n", "Change on " + branch);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<string> UrlOfAsync(TempRepo repo, string remote) =>
        (await repo.GitAsync("config", "--get", $"remote.{remote}.url")).StandardOutput.Trim();

    private static async Task<string?> TrackingAsync(TempRepo repo, string trackingRef)
    {
        var result = await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", trackingRef);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }
}
