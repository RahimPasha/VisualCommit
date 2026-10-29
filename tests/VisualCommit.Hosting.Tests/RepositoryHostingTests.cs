using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// One repository's hosting (docs/phase-7a-design.md 7.8, 7.13; D251, D289, D290, D293): its
/// states, signed out without a request; a fork's parent listed; a changed candidate; refreshes
/// during a run folded into one more; no <c>Changed</c> for an unchanged answer; a sign-out
/// clearing it; A4's 401 signing it out; <c>Dispose</c> ending its work; a created pull request on
/// top of the listed base's list only; its bindings, those of the session's dashboard row for the
/// same URLs and rules.
/// </summary>
public sealed class RepositoryHostingTests
{
    private const string Hello = "http://127.0.0.1:1/octo/hello.git";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_state_signed_out_without_a_request_then_loading_loaded_and_failed()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.Session.StartAsync(Cancelled);
        var hosting = await OpenAsync(setup, out var changed);

        await setup.Thread.RunAsync(() => hosting.Update(Refs(("origin", "https://example.com/octo/hello.git")), UrlRewrites.None));
        Assert.Same(RepositoryHostingState.NotGitHub, hosting.State);

        await setup.Thread.RunAsync(() => hosting.Update(HelloRefs(), UrlRewrites.None));
        Assert.Equal((PullRequestsState.SignedOut, "octo/hello", "origin"), (hosting.State.PullRequests, hosting.State.Repository!.FullName, Assert.Single(hosting.State.Remotes).Key));
        Assert.False(hosting.WantsUrlRewrites);
        Assert.Empty(setup.Fake.Requests);

        await setup.SignInAsync();
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "the list after the sign-in");

        var state = hosting.State;
        Assert.Equal([5, 3, 2, 1], state.List!.PullRequests.Select(pull => pull.Number));
        Assert.Equal(1, state.ByRef["refs/heads/feature/login"].Number);
        Assert.Equal(1, state.ByRef["refs/remotes/origin/feature/login"].Number);
        Assert.Equal(2, state.ByRef["refs/remotes/origin/docs/readme"].Number);
        Assert.False(state.ByRef.ContainsKey("refs/heads/main"));
        Assert.True(hosting.WantsUrlRewrites);
        await setup.Thread.IdleAsync();
        Assert.Single(setup.Log.Lines, line => line.Message == "hello: listed 4 open pull requests of octo/hello");

        setup.Fake.FailNext("pulls:octo/hello", FakeGitHubFault.ServerError);
        await setup.Thread.RunAsync(() => hosting.RefreshPullRequestsAsync(Cancelled));
        Assert.Equal((PullRequestsState.Failed, HostingFailureKind.ServerError, (PullRequestList?)null), (hosting.State.PullRequests, hosting.State.Failure!.Kind, hosting.State.List));

        Task refresh;
        using (var hold = setup.Fake.HoldNext("pulls:octo/hello"))
        {
            refresh = setup.Thread.RunAsync(() => hosting.RefreshPullRequestsAsync(Cancelled));
            await hold.Arrived.WaitAsync(Cancelled);
            Assert.Equal(PullRequestsState.Loading, (await setup.Thread.RunAsync(() => hosting.State)).PullRequests);
        }

        await refresh.WaitAsync(Cancelled);
        Assert.Equal(PullRequestsState.Loaded, hosting.State.PullRequests);
        Assert.True(changed.Count >= 4);
    }

    [Fact]
    public async Task A_forks_parent_is_listed_and_a_changed_candidate_drops_the_list()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenAsync(setup, out _);

        await setup.Thread.RunAsync(() => hosting.Update(HelloRefs(), UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "octo/hello's list");

        var tool = Refs(("origin", "http://127.0.0.1:1/octo/tool.git"), ("upstream", "http://127.0.0.1:1/acme/tool.git")) with
        {
            Refs =
            [
                new GitRef("refs/heads/fix-typo", "fix-typo", RefKind.LocalBranch, "abc", UpstreamRemote: "origin", UpstreamRemoteRef: "refs/heads/fix-typo"),
                new GitRef("refs/remotes/origin/fix-typo", "origin/fix-typo", RefKind.RemoteBranch, "abc", RemoteName: "origin"),
            ],
        };
        var loading = await setup.Thread.RunAsync(() =>
        {
            hosting.Update(tool, UrlRewrites.None);
            return hosting.State;
        });
        Assert.Equal((PullRequestsState.Loading, "octo/tool"), (loading.PullRequests, loading.Repository!.FullName));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "acme/tool's list");

        Assert.Equal("acme/tool", hosting.State.Repository!.FullName);
        Assert.Equal([8, 7], hosting.State.List!.PullRequests.Select(pull => pull.Number));
        Assert.Equal(7, hosting.State.ByRef["refs/heads/fix-typo"].Number);
        Assert.Equal(7, hosting.State.ByRef["refs/remotes/origin/fix-typo"].Number);
        var info = await setup.Thread.RunAsync(() => hosting.ReadRepositoryAsync(Cancelled));
        Assert.Equal(("octo/tool", "acme/tool"), (info.Value!.Repository.FullName, info.Value.Parent!.FullName));
        Assert.Equal(1, setup.Fake.Requests.Count(request => request.Route == "repo:octo/tool"));
        Assert.Contains(setup.Fake.Requests, request => request.Route == "pulls:acme/tool");
    }

    [Fact]
    public async Task Refreshes_during_a_run_fold_into_one_more_and_an_unchanged_answer_raises_nothing()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenAsync(setup, out var changed);
        await setup.Thread.RunAsync(() => hosting.Update(HelloRefs(), UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "the first list");
        await setup.Thread.IdleAsync();
        var raised = changed.Count;
        var lists = Lists(setup);

        Task[] refreshes;
        using (var hold = setup.Fake.HoldNext("pulls:octo/hello"))
        {
            var first = setup.Thread.RunAsync(() => hosting.RefreshPullRequestsAsync(Cancelled));
            await hold.Arrived.WaitAsync(Cancelled);
            refreshes = [first, setup.Thread.RunAsync(() => hosting.RefreshPullRequestsAsync(Cancelled)), setup.Thread.RunAsync(() => hosting.RefreshPullRequestsAsync(Cancelled))];
        }

        await Task.WhenAll(refreshes).WaitAsync(Cancelled);
        await setup.Thread.IdleAsync();

        Assert.Equal(lists + 2, Lists(setup));
        Assert.Equal(PullRequestsState.Loaded, hosting.State.PullRequests);
        Assert.Equal(raised, changed.Count);
    }

    [Fact]
    public async Task A_sign_out_clears_the_list_the_badges_and_the_bindings()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenAsync(setup, out _);
        await setup.Thread.RunAsync(() => hosting.Update(HelloRefs(), UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "the list");
        Assert.Single(hosting.Bindings);

        await setup.Session.SignOutAsync(Cancelled);
        await setup.Thread.IdleAsync();

        Assert.Equal((PullRequestsState.SignedOut, (PullRequestList?)null, 0, 0), (hosting.State.PullRequests, hosting.State.List, hosting.State.ByRef.Count, hosting.Bindings.Count));
        Assert.False(hosting.WantsUrlRewrites);
        Assert.Same(AuthorAvatars.None, hosting.Avatars);
    }

    [Fact]
    public async Task Dispose_ends_a_run_and_nothing_is_applied_or_raised_after_it()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenAsync(setup, out var changed);
        Task refresh;
        using (var hold = setup.Fake.HoldNext("pulls:octo/hello"))
        {
            await setup.Thread.RunAsync(() => hosting.Update(HelloRefs(), UrlRewrites.None));
            refresh = setup.Thread.RunAsync(() => hosting.RefreshPullRequestsAsync(Cancelled));
            await hold.Arrived.WaitAsync(Cancelled);
            await setup.Thread.IdleAsync();
            var raised = changed.Count;
            Assert.Equal(1, setup.Session.HostingCount);

            await setup.Thread.RunAsync(hosting.Dispose);
            await refresh.WaitAsync(Cancelled);
            await setup.Thread.IdleAsync();

            Assert.Equal(raised, changed.Count);
            Assert.Equal(PullRequestsState.Loading, hosting.State.PullRequests);
        }

        // Told of the account no more: the session holds it no more.
        Assert.Equal(0, setup.Session.HostingCount);
    }

    [Fact]
    public async Task A4s_401_signs_the_tab_out_without_a_failure_and_marks_the_account_refused()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenAsync(setup, out _);
        await setup.Thread.RunAsync(() => hosting.Update(HelloRefs(), UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "the list");
        Assert.Single(hosting.Bindings);

        setup.Fake.Revoke(setup.OctoToken);
        await setup.Thread.RunAsync(() => hosting.RefreshPullRequestsAsync(Cancelled));
        await setup.Thread.IdleAsync();

        var state = hosting.State;
        Assert.Equal((PullRequestsState.SignedOut, (PullRequestList?)null, (HostingFailure?)null, 0, 0), (state.PullRequests, state.List, state.Failure, state.ByRef.Count, hosting.Bindings.Count));
        Assert.False(hosting.WantsUrlRewrites);
        var account = setup.Session.Account!;
        Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.Refused), (account.State, account.NoteKind));
        Assert.Equal(AccountTokenStates.Refused, Assert.Single(HostingSessionSignInTests.Load(setup).Accounts).TokenState);
        Assert.Equal([200, 401], setup.Fake.Requests.Where(request => request.Route == "pulls:octo/hello").Select(request => request.Status));
    }

    [Fact]
    public async Task A_created_pull_request_goes_on_top_only_of_the_listed_bases_list()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await OpenAsync(setup, out _);
        await setup.Thread.RunAsync(() => hosting.Update(HelloRefs(), UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => hosting.State.PullRequests == PullRequestsState.Loaded, "the list");
        var hello = hosting.State.Repository!;

        var created = await setup.Thread.RunAsync(() => hosting.CreatePullRequestAsync(hello, new NewPullRequest("Try an idea again", "", "topic/no-pr", null, "main", Draft: false), Cancelled));
        await setup.Thread.IdleAsync();

        Assert.Equal(created.Value!.Number, hosting.State.List!.PullRequests[0].Number);
        Assert.Equal(5, hosting.State.List.PullRequests.Count);

        // In a tab whose listed repository is acme/tool (octo/tool's parent), a pull request created
        // on octo/tool itself leaves the list as it is.
        var tool = await OpenAsync(setup, out _);
        await setup.Thread.RunAsync(() => tool.Update(Refs(("origin", "http://127.0.0.1:1/octo/tool.git")), UrlRewrites.None));
        await setup.Thread.WaitForAsync(() => tool.State.PullRequests == PullRequestsState.Loaded, "acme/tool's list");
        var elsewhere = await setup.Thread.RunAsync(() => tool.CreatePullRequestAsync(tool.State.Remotes["origin"], new NewPullRequest("Elsewhere", "", "fix-typo", null, "main", Draft: true), Cancelled));
        await setup.Thread.IdleAsync();

        Assert.True(elsewhere.Succeeded, elsewhere.Failure?.Message);
        Assert.Equal([8, 7], tool.State.List!.PullRequests.Select(pull => pull.Number));
        Assert.Equal("Elsewhere", setup.Fake.PullRequests("octo/tool").Single().Title);
    }

    [Fact]
    public async Task Bindings_wait_for_the_rewrites_and_equal_the_dashboard_rows_for_the_same_urls_and_rules()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.Session.StartAsync(Cancelled);
        var hosting = await OpenAsync(setup, out _);
        var refs = Refs(("origin", "fake:octo/hello.git"), ("ada", "http://alice@127.0.0.1:1/ada/hello.git"), ("pages", Hello));
        var rewrites = new UrlRewrites([new UrlRewrite("http://127.0.0.1:1/", "fake:", ForPush: false)]);
        await setup.Thread.RunAsync(() => hosting.Update(refs, UrlRewrites.None));

        await setup.SignInAsync();
        await setup.Thread.IdleAsync();
        Assert.Empty(hosting.Bindings);

        await setup.Thread.RunAsync(() => hosting.Update(refs, rewrites));

        Assert.Equal([new CredentialBinding(Hello, HostingSessionStartTests.OctoKey(setup))], hosting.Bindings);
        Assert.Equal(setup.Session.BindingsFor(refs.RemoteUrls, rewrites), hosting.Bindings);

        setup.Session.SetUseForGit(false);
        await setup.Session.Switching.WaitAsync(Cancelled);
        await setup.Thread.IdleAsync();
        Assert.Empty(hosting.Bindings);
        Assert.True(hosting.WantsUrlRewrites);
    }

    internal static RepoRefs Refs(params (string Remote, string Url)[] remotes) =>
        new(new HeadState("main", "abc"), [], [], [.. remotes.Select(remote => remote.Remote).Distinct().Order(StringComparer.Ordinal)])
        {
            RemoteUrls = [.. remotes.Select(remote => new RemoteUrl(remote.Remote, remote.Url, IsPushUrl: false))],
        };

    internal static RepoRefs HelloRefs() => Refs(("origin", Hello)) with
    {
        Refs =
        [
            new GitRef("refs/heads/feature/login", "feature/login", RefKind.LocalBranch, "abc", UpstreamRemote: "origin", UpstreamRemoteRef: "refs/heads/feature/login"),
            new GitRef("refs/heads/main", "main", RefKind.LocalBranch, "abc", UpstreamRemote: "origin", UpstreamRemoteRef: "refs/heads/main", IsHead: true),
            new GitRef("refs/remotes/origin/docs/readme", "origin/docs/readme", RefKind.RemoteBranch, "abc", RemoteName: "origin"),
            new GitRef("refs/remotes/origin/feature/login", "origin/feature/login", RefKind.RemoteBranch, "abc", RemoteName: "origin"),
            new GitRef("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, "abc", RemoteName: "origin"),
        ],
    };

    internal static Task<RepositoryHosting> OpenAsync(HostingSessionSetup setup, out List<bool> changed)
    {
        var raised = new List<bool>();
        changed = raised;
        return setup.Thread.RunAsync(() =>
        {
            var hosting = (RepositoryHosting)setup.Session.ForRepository(setup.Served.Hello.Path);
            hosting.Changed += (_, _) => raised.Add(setup.Thread.IsCurrent);
            return hosting;
        });
    }

    private static int Lists(HostingSessionSetup setup) => setup.Fake.Requests.Count(request => request.Route == "pulls:octo/hello");
}
