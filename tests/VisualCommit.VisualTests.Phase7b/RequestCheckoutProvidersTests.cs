using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.RequestTestData;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// A request's checkout plan for each provider (docs/phase-7b-design.md 12.5, 12.7; D361, ruling 19):
/// a head on a remote is that remote branch's checkout on every provider; GitLab's other heads come
/// through <c>refs/merge-requests/&lt;iid&gt;/head</c> under 7a's local names with a namespace path's
/// <c>/</c> made <c>-</c> and <c>mr-&lt;iid&gt;</c> without a head repository; Azure DevOps' never
/// through <c>FETCH_HEAD</c>, disabled with its reason.
/// </summary>
public sealed class RequestCheckoutProvidersTests
{
    [Fact]
    public void A_head_on_a_remote_is_that_remote_branch_s_checkout_on_both_providers()
    {
        var search = Plan(Loaded(WebApp), WebAppRefs(), MR21);
        Assert.Equal((PullRequestCheckoutKind.RemoteBranch, "Check out feature/search", null, "origin", "refs/remotes/origin/feature/search", "feature/search"), Tuple(search));
        Assert.Equal((PullRequestCheckoutKind.RemoteBranch, "Check out docs/readme", null, "origin", "refs/remotes/origin/docs/readme", "docs/readme"), Tuple(Plan(Loaded(WebApp), WebAppRefs(), MR22)));

        var signIn = Plan(Loaded(MobileApp), MobileAppRefs(), PR11);
        Assert.Equal((PullRequestCheckoutKind.RemoteBranch, "Check out feature/sign-in", null, "origin", "refs/remotes/origin/feature/sign-in", "feature/sign-in"), Tuple(signIn));
        Assert.Equal("feature/sign-in is already checked out", Plan(Loaded(MobileApp), MobileAppRefs(head: "feature/sign-in"), PR11).DisabledReason);
        Assert.Equal((PullRequestCheckoutKind.RemoteBranch, "Check out docs/guide", null, "origin", "refs/remotes/origin/docs/guide", "docs/guide"), Tuple(Plan(Loaded(MobileApp), MobileAppRefs(), PR12)));
    }

    [Fact]
    public void Gitlab_s_fork_comes_through_the_head_ref_under_7a_s_local_names()
    {
        var plan = Plan(Loaded(WebApp), WebAppRefs(), MR23);
        Assert.Equal((PullRequestCheckoutKind.FetchHead, "Check out ada:fix-typo", null, "origin", null, "fix-typo"), Tuple(plan));
        Assert.Equal("refs/merge-requests/23/head", PullRequestRefs.HeadOf(HostingProvider.GitLab, MR23.Number));

        // A taken name: {owner}-{branch}; a local branch at the head is reused.
        var taken = WebAppRefs(extra: [Local("fix-typo", '8', null)]);
        Assert.Equal("ada-fix-typo", Plan(Loaded(WebApp), taken, MR23).LocalBranch);
        var atHead = Plan(Loaded(WebApp), WebAppRefs(extra: [Local("fix-typo", '9', null)]), MR23);
        Assert.Equal((PullRequestCheckoutKind.LocalBranch, "fix-typo"), (atHead.Kind, atHead.LocalBranch));
        Assert.Equal("fix-typo is already checked out", Plan(Loaded(WebApp), WebAppRefs(head: "fix-typo", extra: [Local("fix-typo", '9', null, "fix-typo")]), MR23).DisabledReason);
    }

    [Fact]
    public void Gitlab_s_nested_owner_and_a_head_without_repository()
    {
        var team = new HostedRepository(HostingProvider.GitLab, GitLabHost, "northfield/team", "web-app");
        var nested = Request(25, "From a team fork", "ada", "fix-typo", '9', team, WebApp);
        var taken = WebAppRefs(extra: [Local("fix-typo", '8', null)]);
        var plan = Plan(Loaded(WebApp), taken, nested);
        Assert.Equal(("Check out northfield/team:fix-typo", "northfield-team-fix-typo"), (plan.Label, plan.LocalBranch));
        Assert.Equal("northfield-team-fix-typo-2", Plan(Loaded(WebApp), WebAppRefs(extra: [Local("fix-typo", '8', null), Local("northfield-team-fix-typo", '7', null)]), nested).LocalBranch);

        var unreadable = Request(23, "Fix a typo in the footer", "ada", "fix-typo", '9', null, WebApp);
        Assert.Equal((PullRequestCheckoutKind.FetchHead, "Check out fix-typo", null, "origin", null, "fix-typo"), Tuple(Plan(Loaded(WebApp), WebAppRefs(), unreadable)));
        Assert.Equal("mr-23-fix-typo", Plan(Loaded(WebApp), taken, unreadable).LocalBranch);
    }

    [Fact]
    public void Gitlab_without_the_base_s_remote_is_disabled_in_its_words()
    {
        var other = new HostedRepository(HostingProvider.GitLab, GitLabHost, "elsewhere", "web-app");
        var plan = Plan(Loaded(WebApp), WebAppRefs(), Request(30, "Elsewhere", "ada", "fix", '7', AdaWebApp, other));
        Assert.Equal("Add a remote for elsewhere/web-app to check out this merge request", plan.DisabledReason);
    }

    [Fact]
    public void Azure_devops_never_fetches_a_head_and_says_why()
    {
        var fork = Plan(Loaded(MobileApp), MobileAppRefs(), PR13);
        Assert.Equal((PullRequestCheckoutKind.FetchHead, "Check out mobile-app-fork:fix-typo", "Add a remote for Fernhill-Labs/Mobile App/mobile-app-fork to check out this pull request", null, null, null), Tuple(fork));
        Assert.Null(PullRequestRefs.HeadOf(HostingProvider.AzureDevOps, PR13.Number));

        var unnamed = Plan(Loaded(MobileApp), MobileAppRefs(), Request(14, "Unnamed", "Ada Lovelace", "topic/x", '7', null, MobileApp));
        Assert.Equal(("Check out topic/x", "Azure DevOps names no repository for this pull request's branch"), (unnamed.Label, unnamed.DisabledReason));

        var narrow = MobileAppRefs() with { Refspecs = FetchRefspecs.Parse([("origin", "+refs/heads/main:refs/remotes/origin/main")]) };
        var notFetched = Plan(Loaded(MobileApp), narrow, PR11);
        Assert.Equal(("Check out feature/sign-in", "origin does not fetch feature/sign-in"), (notFetched.Label, notFetched.DisabledReason));

        // The fork made a remote here: its remote branch's checkout, never FETCH_HEAD.
        var remotes = new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["fork"] = MobileAppFork, ["origin"] = MobileApp };
        var withFork = new RepositoryHostingState(PullRequestsState.Loaded, remotes, MobileApp, MobileAppList, null, MobileAppByRef);
        var forkRefs = WithRefspecs(MobileAppRefs() with { Refs = [.. MobileAppRefs().Refs, Remote("fork/fix-typo", '9')], Remotes = ["fork", "origin"] });
        var viaFork = Plan(withFork, forkRefs, PR13);
        Assert.Equal((PullRequestCheckoutKind.RemoteBranch, "Check out fix-typo", null, "fork", "refs/remotes/fork/fix-typo", "fix-typo"), Tuple(viaFork));

        foreach (var request in MobileAppList.PullRequests)
        {
            var each = Plan(Loaded(MobileApp), MobileAppRefs(), request);
            Assert.True(each.Kind != PullRequestCheckoutKind.FetchHead || each.DisabledReason is not null, $"!{request.Number}");
        }
    }

    [Fact]
    public void Github_s_upstream_beside_a_listed_gitlab_origin_is_no_request_remote()
    {
        // A request whose head is GitHub's octo/web-app: upstream is no key of RequestRemotes, so
        // step 1 does not take it and the head ref comes from GitLab's origin.
        var fromUpstream = Request(26, "From upstream", "octo", "main", '2', OctoWebApp, WebApp);
        var plan = Plan(Loaded(WebApp), WebAppRefs(), fromUpstream);
        Assert.Equal((PullRequestCheckoutKind.LocalBranch, "Check out octo:main", "main is already checked out", "origin"), (plan.Kind, plan.Label, plan.DisabledReason, plan.Remote));
    }

    private static PullRequestCheckoutPlan Plan(RepositoryHostingState state, RepoRefs refs, PullRequest request) => PullRequestCheckout.Plan(state, refs, request);

    private static (PullRequestCheckoutKind, string, string?, string?, string?, string?) Tuple(PullRequestCheckoutPlan plan) =>
        (plan.Kind, plan.Label, plan.DisabledReason, plan.Remote, plan.RemoteRef, plan.LocalBranch);
}
