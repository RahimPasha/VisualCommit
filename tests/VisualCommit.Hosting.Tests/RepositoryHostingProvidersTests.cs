using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.AzureDevOps;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// One repository's hosting for three providers (docs/phase-7b-design.md 9.7, 9.8, 9.10; D356, D366,
/// D367, ruling 5): each provider's candidate signed out without a request and listed by its own
/// account, with the Debug line of any depth; a GitLab candidate beside a GitHub upstream asking
/// neither fake while GitHub alone is signed in, and no avatars; another organization's remote sending
/// nothing and its tab's state swapped by a sign-in there; GitHub's sign-in refreshing no GitLab-listed
/// tab and its sign-out keeping GitLab's bindings; an <c>&lt;organization&gt;.visualstudio.com</c>
/// remote listed through <c>dev.azure.com</c>. The fakes are in process (ProvidersSessionSetup).
/// </summary>
public sealed class RepositoryHostingProvidersTests
{
    private const string GitHubUrl = "http://127.0.0.1:1/octo/web-app.git";
    private const string GitLabUrl = "http://127.0.0.1:3/fake-group/sub/web-app.git";
    private const string AzureUrl = "http://fake-org@127.0.0.1:2/fake-org/P/_git/r";
    private const string OtherAzureUrl = "http://other-org@127.0.0.1:2/other-org/P/_git/r";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(HostingProvider.GitHub, GitHubUrl, "octo/web-app", 7)]
    [InlineData(HostingProvider.GitLab, GitLabUrl, "fake-group/sub/web-app", 21)]
    [InlineData(HostingProvider.AzureDevOps, AzureUrl, "fake-org/P/r", 11)]
    public async Task Each_providers_candidate_is_signed_out_without_a_request_then_listed_by_its_own_account(HostingProvider provider, string url, string fullName, int number)
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(repositories: true);
        await setup.Session.StartAsync(Cancelled);
        var hosting = await OpenAsync(setup, "web-app");

        await setup.Thread.RunAsync(() => hosting.Update(RepositoryHostingTests.Refs(("origin", "https://example.com/o/r.git")), UrlRewrites.None));
        Assert.Same(RepositoryHostingState.NoHostedRemote, hosting.State);

        await setup.Thread.RunAsync(() => hosting.Update(RefsOf(("origin", url)), UrlRewrites.None));
        Assert.Equal((PullRequestsState.SignedOut, provider, fullName), (hosting.State.PullRequests, hosting.State.Repository!.Provider, hosting.State.Repository.FullName));
        Assert.Equal(0, setup.SentCount);

        // Another provider's sign-in lists nothing here.
        var other = provider == HostingProvider.GitLab ? HostingProvider.GitHub : HostingProvider.GitLab;
        await setup.SignInAsync(other);
        await setup.Thread.IdleAsync();
        Assert.Equal(PullRequestsState.SignedOut, hosting.State.PullRequests);
        Assert.Single(setup.Sent(other));
        Assert.Empty(setup.Sent(provider));

        await setup.SignInAsync(provider);
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "the list after the sign-in");
        var state = hosting.State;
        Assert.Equal(number, Assert.Single(state.List!.PullRequests).Number);
        Assert.Equal(number, state.ByRef["refs/remotes/origin/feature/sign-in"].Number);
        Assert.Equal(number, state.ByRef["refs/heads/feature/sign-in"].Number);
        Assert.Equal(["origin"], state.RequestRemotes.Keys);
        await setup.Thread.IdleAsync();
        Assert.Single(setup.Log.Lines, line => line.Level == LogLevel.Debug && line.Message == $"web-app: listed 1 open pull requests of {fullName}");

        Assert.Null(await setup.Session.SignOutAsync(provider, Cancelled));
        await setup.Thread.IdleAsync();
        Assert.Equal((PullRequestsState.SignedOut, (PullRequestList?)null), (hosting.State.PullRequests, hosting.State.List));
        Assert.Empty(hosting.State.ByRef);
        await setup.Thread.RunAsync(hosting.Dispose);
    }

    [Fact]
    public async Task A_gitlab_candidate_beside_a_github_upstream_asks_neither_fake_while_github_alone_is_signed_in()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(azureDevOps: false, repositories: true);
        await setup.SignInAsync(HostingProvider.GitHub);
        var sent = setup.SentCount;
        var hosting = await OpenAsync(setup, "web-app");

        await setup.Thread.RunAsync(() => hosting.Update(RefsOf(("origin", GitLabUrl), ("upstream", GitHubUrl)), UrlRewrites.None));
        await setup.Thread.RunAsync(() => hosting.RefreshPullRequestsAsync(Cancelled));
        var read = await setup.Thread.RunAsync(() => hosting.ReadRepositoryAsync(Cancelled));
        await setup.Thread.RunAsync(() => hosting.ShowAvatarsFor([new AuthorCommit("ada@example.com", "abc", new HostedRepository(HostingProvider.GitHub, "127.0.0.1:1", "octo", "web-app"))]));
        await setup.Thread.RunAsync(() => hosting.AvatarRun);
        await setup.Thread.IdleAsync();

        Assert.Equal((PullRequestsState.SignedOut, HostingProvider.GitLab), (hosting.State.PullRequests, hosting.State.Repository!.Provider));
        Assert.Equal((HostingFailureKind.BadCredentials, HostingProvider.GitLab), (read.Failure!.Kind, read.Failure.Provider));
        Assert.Equal(["origin"], hosting.State.RequestRemotes.Keys);
        Assert.True(hosting.WantsUrlRewrites);
        Assert.Same(AuthorAvatars.None, hosting.Avatars);
        Assert.Equal(sent, setup.SentCount);
        Assert.Equal([new CredentialBinding(GitHubUrl, setup.Session.AccountOf(HostingProvider.GitHub)!.Account.Key)], hosting.Bindings);
    }

    [Fact]
    public async Task Another_organizations_remote_sends_nothing_and_stays_unbound()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, gitLab: false, repositories: true);
        await setup.SignInAsync(HostingProvider.AzureDevOps);
        var requests = setup.AzureDevOps!.Requests.Count;
        var hosting = await OpenAsync(setup, "manifest");

        await setup.Thread.RunAsync(() => hosting.Update(RefsOf(("origin", OtherAzureUrl)), UrlRewrites.None));
        await setup.Thread.RunAsync(() => hosting.RefreshPullRequestsAsync(Cancelled));
        var read = await setup.Thread.RunAsync(() => hosting.ReadRepositoryAsync(Cancelled));
        var other = new HostedRepository(HostingProvider.AzureDevOps, "127.0.0.1:2", "other-org/P", "r");
        var created = await hosting.CreatePullRequestAsync(other, new NewPullRequest("Title", string.Empty, "feature/sign-in", null, "main", false), Cancelled);
        await setup.Thread.IdleAsync();

        var state = hosting.State;
        Assert.Equal((PullRequestsState.OtherOrganization, other), (state.PullRequests, state.Repository));
        Assert.Equal(((PullRequestList?)null, (HostingFailure?)null), (state.List, state.Failure));
        Assert.Empty(state.ByRef);
        Assert.Empty(state.RequestRemotes);
        Assert.Equal(["origin"], state.Remotes.Keys);
        Assert.Equal(("Not the signed-in account's repository.", HostingFailureKind.Validation), (read.Failure!.Message, read.Failure.Kind));
        Assert.Equal(("Not the signed-in account's repository.", HostingProvider.AzureDevOps), (created.Failure!.Message, created.Failure.Provider));
        Assert.Empty(hosting.Bindings);
        Assert.Equal(requests, setup.AzureDevOps.Requests.Count);
    }

    [Fact]
    public async Task A_sign_in_to_the_other_organization_swaps_two_tabs_states()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, gitLab: false, repositories: true);
        await setup.Session.StartAsync(Cancelled);
        var mine = await OpenAsync(setup, "mobile-app");
        var theirs = await OpenAsync(setup, "manifest");
        await setup.Thread.RunAsync(() =>
        {
            mine.Update(RefsOf(("origin", AzureUrl)), UrlRewrites.None);
            theirs.Update(RefsOf(("origin", OtherAzureUrl)), UrlRewrites.None);
        });
        Assert.Equal((PullRequestsState.SignedOut, PullRequestsState.SignedOut), (mine.State.PullRequests, theirs.State.PullRequests));

        await setup.SignInAsync(HostingProvider.AzureDevOps);
        await setup.Thread.WaitForAsync(() => mine.State.PullRequests == PullRequestsState.Loaded, "fake-org's list");
        Assert.Equal(PullRequestsState.OtherOrganization, theirs.State.PullRequests);

        await setup.SignInAsync(HostingProvider.AzureDevOps, setup.OtherOrganizationToken, ProvidersSessionSetup.OtherOrganization);
        await setup.Thread.WaitForAsync(() => theirs.State.PullRequests == PullRequestsState.Loaded, "other-org's list");
        Assert.Equal((PullRequestsState.OtherOrganization, (PullRequestList?)null), (mine.State.PullRequests, mine.State.List));
        Assert.Equal(31, Assert.Single(theirs.State.List!.PullRequests).Number);
        Assert.DoesNotContain(setup.AzureDevOps!.Requests, request => request.Path.StartsWith("/other-org/", StringComparison.Ordinal) && request.Login is { } login && !login.StartsWith("other-org", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Githubs_sign_in_refreshes_no_gitlab_listed_tab_and_its_sign_out_keeps_gitlabs_bindings()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(azureDevOps: false, repositories: true);
        var gitLab = (await setup.SignInAsync(HostingProvider.GitLab)).Account.Key;
        var hosting = await OpenAsync(setup, "web-app");
        var refs = RefsOf(("origin", GitLabUrl), ("upstream", GitHubUrl));
        await setup.Thread.RunAsync(() => hosting.Update(refs, UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "GitLab's list");
        var lists = setup.Sent(HostingProvider.GitLab).Count;
        Assert.Equal([new CredentialBinding(GitLabUrl, gitLab)], hosting.Bindings);

        var gitHub = (await setup.SignInAsync(HostingProvider.GitHub)).Account.Key;
        await setup.Thread.RunAsync(() => hosting.Update(refs, UrlRewrites.None));
        await setup.Thread.IdleAsync();

        Assert.Equal(lists, setup.Sent(HostingProvider.GitLab).Count);
        Assert.Equal(["GET /api/v3/user"], setup.Sent(HostingProvider.GitHub).Select(request => $"{request.Method} {request.Url.AbsolutePath}"));
        Assert.Equal(PullRequestsState.Loaded, hosting.State.PullRequests);
        Assert.Equal([new CredentialBinding(GitHubUrl, gitHub), new CredentialBinding(GitLabUrl, gitLab)], hosting.Bindings);

        Assert.Null(await setup.Session.SignOutAsync(HostingProvider.GitHub, Cancelled));
        await setup.Thread.IdleAsync();

        Assert.Equal([new CredentialBinding(GitLabUrl, gitLab)], hosting.Bindings);
        Assert.True(hosting.WantsUrlRewrites);
        Assert.Equal(PullRequestsState.Loaded, hosting.State.PullRequests);
        Assert.Equal(lists, setup.Sent(HostingProvider.GitLab).Count);
        Assert.Same(AuthorAvatars.None, hosting.Avatars);
    }

    [Fact]
    public async Task On_the_service_an_organization_hosts_remote_is_listed_through_dev_azure_com()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, gitLab: false, repositories: true, azureDevOpsEndpoints: AzureDevOpsEndpoints.Service);
        var account = (await setup.SignInAsync(HostingProvider.AzureDevOps)).Account;
        const string Remote = "https://fake-org.visualstudio.com/P/_git/r";
        var recognised = setup.Session.Recognise(Remote)!;
        Assert.Equal(new HostedRepository(HostingProvider.AzureDevOps, "dev.azure.com", "fake-org/P", "r"), recognised);
        Assert.True(setup.Session.ProviderOf(HostingProvider.AzureDevOps)!.Covers(recognised, account.Key));
        var hosting = await OpenAsync(setup, "mobile-app");

        await setup.Thread.RunAsync(() => hosting.Update(RefsOf(("origin", Remote)), UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "the list");

        Assert.Contains(setup.AzureDevOps!.Requests, request => request.Host == "dev.azure.com" && request.Path == "/fake-org/P/_apis/git/repositories/r/pullrequests");
        Assert.All(setup.AzureDevOps.Requests, request => Assert.Equal("dev.azure.com", request.Host));
        Assert.Equal(11, hosting.State.ByRef["refs/remotes/origin/feature/sign-in"].Number);
        Assert.Equal([new CredentialBinding(Remote, account.Key)], hosting.Bindings);
    }

    private static Task<RepositoryHosting> OpenAsync(ProvidersSessionSetup setup, string name) =>
        setup.Thread.RunAsync(() => (RepositoryHosting)setup.Session.ForRepository(Path.Combine(setup.Root.Path, name)));

    // The remotes with feature/sign-in on each, and a local feature/sign-in tracking the first.
    private static RepoRefs RefsOf(params (string Remote, string Url)[] remotes) =>
        RepositoryHostingTests.Refs(remotes) with
        {
            Refs =
            [
                new GitRef("refs/heads/feature/sign-in", "feature/sign-in", RefKind.LocalBranch, "abc", UpstreamRemote: remotes[0].Remote, UpstreamRemoteRef: "refs/heads/feature/sign-in"),
                .. remotes.Select(remote => new GitRef($"refs/remotes/{remote.Remote}/feature/sign-in", $"{remote.Remote}/feature/sign-in", RefKind.RemoteBranch, "abc", RemoteName: remote.Remote)),
            ],
        };
}
