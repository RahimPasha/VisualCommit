using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// One repository's hosting for three providers (docs/phase-7b-design.md 9.7, 9.8, 9.10; D356, D366,
/// D367, ruling 5): each provider's candidate signed out without a request and listed by its own
/// account, with the Debug line of any depth; a new provider's list loading while its request waits
/// and failed with its provider and one warning when the service fails; a GitLab candidate beside a
/// GitHub upstream asking neither fake while GitHub alone is signed in, and no avatars; GitHub's
/// avatars taking GitHub remotes' authors alone, kept through GitLab's sign-out and gone once
/// GitLab's repository is listed; another organization's remote sending
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

    [Theory]
    [InlineData(HostingProvider.GitLab, GitLabUrl, "GitLab", "fake-group/sub/web-app", "mrs:fake-group/sub/web-app")]
    [InlineData(HostingProvider.AzureDevOps, AzureUrl, "Azure DevOps", "fake-org/P/r", "pulls:fake-org/p/r")]
    public async Task A_new_providers_list_is_loading_while_its_request_waits_and_failed_with_its_provider_when_the_service_fails(HostingProvider provider, string url, string name, string fullName, string route)
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, repositories: true);
        FakeHostingService fake = provider == HostingProvider.GitLab ? setup.GitLab! : setup.AzureDevOps!;
        await setup.SignInAsync(provider);
        var hosting = await OpenAsync(setup, "web-app");

        // The list's request held: the section is loading, its repository the candidate.
        using (var hold = fake.HoldNext(route))
        {
            await setup.Thread.RunAsync(() => hosting.Update(RefsOf(("origin", url)), UrlRewrites.None));
            await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
            await setup.Thread.IdleAsync();
            Assert.Equal((PullRequestsState.Loading, provider, fullName), (hosting.State.PullRequests, hosting.State.Repository!.Provider, hosting.State.Repository.FullName));
            Assert.Equal(((PullRequestList?)null, (HostingFailure?)null), (hosting.State.List, hosting.State.Failure));
        }

        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "the list once released");

        // The next list answered with a server fault: failed, the failure the provider's, one warning naming it.
        fake.FailNext(route, provider == HostingProvider.GitLab ? FakeGitLabFault.ServerError : FakeAzureDevOpsFault.ServerError);
        var warnings = setup.Warnings.Count;
        await setup.Thread.RunAsync(() => hosting.RefreshPullRequestsAsync(Cancelled));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Failed, "the failed list");

        var state = hosting.State;
        Assert.Equal((provider, fullName), (state.Failure!.Provider, state.Repository!.FullName));
        Assert.Equal(((PullRequestList?)null, 0), (state.List, state.ByRef.Count));
        Assert.Single(setup.Warnings.Skip(warnings), warning => warning.StartsWith($"{name}: listing the pull requests of {fullName} failed: ", StringComparison.Ordinal));
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
        Assert.Equal(sent, setup.SentCount);
        Assert.Equal([new CredentialBinding(GitHubUrl, setup.Session.AccountOf(HostingProvider.GitHub)!.Account.Key)], hosting.Bindings);
    }

    [Fact]
    public async Task Githubs_avatars_take_github_authors_alone_stay_through_gitlabs_sign_out_and_go_when_gitlab_is_listed()
    {
        await using var setup = await ProvidersSessionSetup.StartAsync(azureDevOps: false, repositories: true);
        setup.GitHub!.LinkEmail(TempRepo.AuthorEmail, "octo");
        await setup.SignInAsync(HostingProvider.GitHub);
        await setup.SignInAsync(HostingProvider.GitLab);
        var hosting = await OpenAsync(setup, "web-app");
        await setup.Thread.RunAsync(() => hosting.Update(RefsOf(("origin", GitHubUrl), ("upstream", GitLabUrl)), UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "GitHub's list");

        // One author of a GitHub remote, found on main's page, and one of the GitLab remote, whom
        // GitHub is never asked about: no lookup names it, and only the first gets an avatar.
        const string GitLabOnly = "gitlab-only@example.com";
        AuthorCommit[] authors =
        [
            new(TempRepo.AuthorEmail, new string('a', 40), new HostedRepository(HostingProvider.GitHub, "127.0.0.1:1", "octo", "web-app")),
            new(GitLabOnly, new string('b', 40), new HostedRepository(HostingProvider.GitLab, "127.0.0.1:3", "fake-group/sub", "web-app")),
        ];
        var run = await setup.Thread.RunAsync<Task>(() =>
        {
            hosting.ShowAvatarsFor(authors);
            return hosting.AvatarRun;
        });
        await run.WaitAsync(Cancelled);
        await setup.Thread.IdleAsync();
        await setup.Thread.IdleAsync();

        Assert.Equal(["sha=main&per_page=100"], setup.GitHub.Requests.Where(request => request.Route == "commits:octo/web-app").Select(request => request.Query));
        Assert.DoesNotContain(setup.GitHub.Requests, request => request.Query.Contains("gitlab-only", StringComparison.OrdinalIgnoreCase));
        var avatars = hosting.Avatars;
        Assert.True(File.Exists(avatars.FileOf(TempRepo.AuthorEmail)));
        Assert.Null(avatars.FileOf(GitLabOnly));
        Assert.Single(avatars.FileByEmail);

        // GitLab's sign-out keeps GitHub's avatars.
        Assert.Null(await setup.Session.SignOutAsync(HostingProvider.GitLab, Cancelled));
        await setup.Thread.IdleAsync();
        Assert.Same(avatars, hosting.Avatars);

        // GitLab's repository listed: the avatars go, and GitHub is asked nothing more.
        var gitHubSent = setup.Sent(HostingProvider.GitHub).Count;
        await setup.Thread.RunAsync(() => hosting.Update(RefsOf(("origin", GitLabUrl), ("upstream", GitHubUrl)), UrlRewrites.None));
        await setup.Thread.IdleAsync();
        Assert.Equal(HostingProvider.GitLab, hosting.State.Repository!.Provider);
        Assert.Same(AuthorAvatars.None, hosting.Avatars);
        Assert.Equal(gitHubSent, setup.Sent(HostingProvider.GitHub).Count);
        await setup.Thread.RunAsync(hosting.Dispose);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Another_organizations_tab_raises_changed_when_azure_devops_account_becomes_a_third_organizations(bool refusedFirst)
    {
        // The tab's state is OtherOrganization for fake-org's account and for other-org's alike, yet
        // the tab shows which account is held (D356, 9.9): swapping one for the other is a change.
        // Refused first, with GitLab signed in so that the rewrites stay wanted, it is too (D366).
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, gitLab: refusedFirst);
        await setup.Session.StartAsync(Cancelled);
        if (refusedFirst)
        {
            await setup.SignInAsync(HostingProvider.GitLab);
        }

        var hosting = await OpenAsync(setup, "unlisted");
        var logins = new List<string?>();
        await setup.Thread.RunAsync(() =>
        {
            hosting.Changed += (_, _) => logins.Add(setup.Session.AccountOf(HostingProvider.AzureDevOps)?.Account.Key.Login);
            hosting.Update(RefsOf(("origin", "http://unlisted-org@127.0.0.1:2/unlisted-org/P/_git/r")), UrlRewrites.None);
        });
        await setup.SignInAsync(HostingProvider.AzureDevOps);
        await setup.Thread.IdleAsync();
        Assert.Equal(PullRequestsState.OtherOrganization, hosting.State.PullRequests);
        Assert.Equal("fake-org", logins[^1]);
        if (refusedFirst)
        {
            setup.AzureDevOps!.Revoke(setup.AzureDevOpsToken);
            var listed = await setup.Session.ListRepositoriesAsync(HostingProvider.AzureDevOps, Cancelled);
            Assert.Equal(HostingFailureKind.BadCredentials, listed.Failure!.Kind);
            await setup.Thread.IdleAsync();
            Assert.Equal((AccountState.NeedsSignIn, PullRequestsState.OtherOrganization), (setup.Session.AccountOf(HostingProvider.AzureDevOps)!.State, hosting.State.PullRequests));
        }

        var changes = logins.Count;
        await setup.SignInAsync(HostingProvider.AzureDevOps, setup.OtherOrganizationToken, ProvidersSessionSetup.OtherOrganization);
        await setup.Thread.IdleAsync();
        await setup.Thread.IdleAsync();

        Assert.Equal(["other-org"], logins.Skip(changes));
        Assert.Equal(PullRequestsState.OtherOrganization, hosting.State.PullRequests);
        Assert.DoesNotContain(setup.AzureDevOps!.Requests, request => request.Path.Contains("unlisted", StringComparison.OrdinalIgnoreCase));
        await setup.Thread.RunAsync(hosting.Dispose);
    }

    [Fact]
    public async Task A3s_answer_is_kept_for_its_account_alone_so_another_user_never_lists_the_first_ones_private_parent()
    {
        // ada may see the fork's private parent, bob may not (D348: one GitLab user swapped for
        // another). Under bob the fork is read again and listed itself, as for bob alone.
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, azureDevOps: false);
        var gitLab = setup.GitLab!;
        gitLab.AddUser("bob", 2002, "Bob Example");
        var bobToken = gitLab.AddToken("bob");
        using var source = await TempRepo.CreateAsync("fork-source");
        await source.CommitFileAsync("README.md", "fork\n", "Initial commit");
        var parent = await gitLab.AddProjectAsync("secret/parent", source.Path, ct: Cancelled);
        await gitLab.AddProjectAsync("g/fork", source.Path, forkOf: parent, ct: Cancelled);
        gitLab.Grant("secret", "ada", 40);
        gitLab.Grant("g", "ada", 40);
        gitLab.Grant("g", "bob", 40);
        await setup.SignInAsync(HostingProvider.GitLab);
        var hosting = await OpenAsync(setup, "fork");
        await setup.Thread.RunAsync(() => hosting.Update(RefsOf(("origin", "http://127.0.0.1:3/g/fork.git")), UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "ada's list");
        Assert.Equal("secret/parent", hosting.State.Repository!.FullName);

        Assert.Null(await setup.Session.SignOutAsync(HostingProvider.GitLab, Cancelled));
        await setup.Thread.IdleAsync();
        var mark = setup.Sent(HostingProvider.GitLab).Count;
        var signedIn = await setup.Session.SignInAsync(new SignInRequest(HostingProvider.GitLab, bobToken), Cancelled);
        Assert.Null(signedIn.Failure);
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests is PullRequestsState.Loaded or PullRequestsState.Failed, "bob's list");
        var read = await setup.Thread.RunAsync(() => hosting.ReadRepositoryAsync(Cancelled));

        Assert.Equal((PullRequestsState.Loaded, "g/fork"), (hosting.State.PullRequests, hosting.State.Repository!.FullName));
        Assert.Equal((false, (HostedRepository?)null), (read.Value!.IsFork, read.Value.Parent));
        Assert.DoesNotContain(setup.Sent(HostingProvider.GitLab).Skip(mark), request => request.Url.AbsoluteUri.Contains("secret", StringComparison.OrdinalIgnoreCase));
        TokenTripwire.AssertAbsent(bobToken, [setup.Data.Path], null);
        await setup.Thread.RunAsync(hosting.Dispose);
    }

    [Fact]
    public async Task The_rewrites_are_wanted_as_last_raised_so_a_refusal_raced_by_another_sign_in_still_reads_the_rules()
    {
        // Azure DevOps alone signed in, a GitLab tab whose rules were read. Its 401 changes the
        // account on the UI thread, and GitLab's sign-in sets its slot before the Changed that change
        // posted runs. A handler by 7.10's rule (forget the rules when they are not wanted, read them
        // when they are and were forgotten) sees the first Changed unwanted and the second wanted, so
        // the rules are read again and GitLab's remote is bound.
        await using var setup = await ProvidersSessionSetup.StartAsync(gitHub: false, repositories: true);
        await setup.SignInAsync(HostingProvider.AzureDevOps);
        var hosting = await OpenAsync(setup, "web-app");
        var refs = RefsOf(("origin", GitLabUrl));
        var rulesRead = true;
        var seen = new List<bool>();
        await setup.Thread.RunAsync(() =>
        {
            hosting.Update(refs, UrlRewrites.None);
            hosting.Changed += (_, _) =>
            {
                seen.Add(hosting.WantsUrlRewrites);
                if (!hosting.WantsUrlRewrites)
                {
                    rulesRead = false;
                }
                else if (!rulesRead)
                {
                    rulesRead = true;
                    setup.Thread.Post(() =>
                    {
                        if (hosting.WantsUrlRewrites)
                        {
                            hosting.Update(refs, UrlRewrites.None);
                        }
                    });
                }
            };
        });
        await setup.Thread.IdleAsync();
        Assert.True(hosting.WantsUrlRewrites);
        Assert.Empty(hosting.Bindings);
        await setup.Thread.RunAsync(seen.Clear);

        // The UI thread held while the 401 posts its change, then held again behind it.
        using var first = new ManualResetEventSlim();
        using var second = new ManualResetEventSlim();
        var secondArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Thread.Post(() => first.Wait(Cancelled));
        setup.AzureDevOps!.Revoke(setup.AzureDevOpsToken);
        Assert.Equal(HostingFailureKind.BadCredentials, (await setup.Session.ListRepositoriesAsync(HostingProvider.AzureDevOps, Cancelled)).Failure!.Kind);
        setup.Thread.Post(() =>
        {
            secondArrived.SetResult();
            second.Wait(Cancelled);
        });
        first.Set();
        await secondArrived.Task.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);

        // Azure DevOps' change has run; GitLab's slot is set before its Changed runs.
        var gitLab = (await setup.Session.SignInAsync(new SignInRequest(HostingProvider.GitLab, setup.GitLabToken), Cancelled)).Status!.Account.Key;
        second.Set();
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "GitLab's list");
        await setup.Thread.IdleAsync();
        await setup.Thread.IdleAsync();

        Assert.Equal([false, true], seen.Take(2));
        Assert.Equal([new CredentialBinding(GitLabUrl, gitLab)], hosting.Bindings);
        await setup.Thread.RunAsync(hosting.Dispose);
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
