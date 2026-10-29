using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.PullRequestTestData;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The plan of a pull request's checkout (docs/phase-7a-design.md 10.5, 10.6; D305): a head on a
/// remote (the same repository, or the user's fork as <c>origin</c> while <c>upstream</c> is the
/// base, also before the remote branch is read), a fork's head through <c>FETCH_HEAD</c>, no base
/// remote, a deleted fork, and the local name: taken, conflicting as a path, reused at the head,
/// current.
/// </summary>
public sealed class PullRequestCheckoutTests
{
    [Fact]
    public void A_same_repository_head_checks_out_its_remote_branch()
    {
        var plan = PullRequestCheckout.Plan(Loaded(), HelloRefs(), One);

        Assert.Equal(new PullRequestCheckoutPlan(PullRequestCheckoutKind.RemoteBranch, "Check out feature/login", null, "origin", "refs/remotes/origin/feature/login", "feature/login"), plan);
    }

    [Fact]
    public void A_head_on_the_branch_checked_out_is_disabled()
    {
        var plan = PullRequestCheckout.Plan(Loaded(), HelloRefs(head: "feature/login"), One);

        Assert.Equal("feature/login is already checked out", plan.DisabledReason);
        Assert.Equal("Check out feature/login", plan.Label);
    }

    [Fact]
    public void A_remote_branch_not_read_yet_is_still_the_remote_s()
    {
        var pull = Pull(9, "New work", "octo", "feature/new", 'n', OctoHello);

        var plan = PullRequestCheckout.Plan(Loaded(), HelloRefs(), pull);

        Assert.Equal(new PullRequestCheckoutPlan(PullRequestCheckoutKind.RemoteBranch, "Check out feature/new", null, "origin", "refs/remotes/origin/feature/new", "feature/new"), plan);
    }

    [Fact]
    public void A_head_on_the_user_s_fork_as_origin_while_upstream_is_the_base()
    {
        var seven = Pull(7, "Fix a typo in the readme", "octo", "fix-typo", 'f', OctoTool, baseRepository: AcmeTool);

        var plan = PullRequestCheckout.Plan(ToolState(), ToolRefs(), seven);

        Assert.Equal(new PullRequestCheckoutPlan(PullRequestCheckoutKind.RemoteBranch, "Check out fix-typo", "fix-typo is already checked out", "origin", "refs/remotes/origin/fix-typo", "fix-typo"), plan);
        Assert.Null(PullRequestCheckout.Plan(ToolState(), ToolRefs(head: "main"), seven).DisabledReason);
    }

    [Fact]
    public void A_fork_s_head_is_fetched_from_the_base_s_remote()
    {
        var plan = PullRequestCheckout.Plan(Loaded(), HelloRefs(), Five);

        Assert.Equal(new PullRequestCheckoutPlan(PullRequestCheckoutKind.FetchHead, "Check out ada:fix-typo", null, "origin", null, "fix-typo"), plan);
    }

    [Fact]
    public void Without_a_remote_for_the_base_it_is_disabled()
    {
        var remotes = new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["origin"] = OctoTool };
        var state = Loaded() with { Remotes = remotes, Repository = AcmeTool };
        var pull = Pull(6, "A fix", "ada", "fix", 'x', new HostedRepository(HostingProvider.GitHub, Host, "ada", "tool"), baseRepository: AcmeTool);

        var plan = PullRequestCheckout.Plan(state, ToolRefs(head: "main"), pull);

        Assert.Equal("Check out ada:fix", plan.Label);
        Assert.Equal("Add a remote for acme/tool to check out this pull request", plan.DisabledReason);
    }

    [Fact]
    public void A_deleted_fork_s_head_is_its_branch_alone_from_upstream()
    {
        var eight = Pull(8, "An old idea", "ada", "idea", 'i', null, baseRepository: AcmeTool);

        var plan = PullRequestCheckout.Plan(ToolState(), ToolRefs(head: "main"), eight);

        Assert.Equal(new PullRequestCheckoutPlan(PullRequestCheckoutKind.FetchHead, "Check out idea", null, "upstream", null, "idea"), plan);

        // Its owner, pr-8, names the next candidate.
        var taken = PullRequestCheckout.Plan(ToolState(), ToolRefs(head: "main", extra: [Local("idea", 'z', null)]), eight);
        Assert.Equal("pr-8-idea", taken.LocalBranch);
    }

    [Fact]
    public void Taken_names_go_to_the_owner_s_and_then_numbered_ones()
    {
        var refs = HelloRefs() with { Refs = [.. HelloRefs().Refs, Local("fix-typo", 'z', null), Local("ada-fix-typo", 'y', null)] };

        var plan = PullRequestCheckout.Plan(Loaded(), refs, Five);

        Assert.Equal(PullRequestCheckoutKind.FetchHead, plan.Kind);
        Assert.Equal("ada-fix-typo-2", plan.LocalBranch);
        Assert.Equal("Check out ada:fix-typo", plan.Label);
    }

    [Fact]
    public void A_name_that_conflicts_as_a_path_is_skipped()
    {
        var folder = HelloRefs() with { Refs = [.. HelloRefs().Refs, Local("fix-typo/old", 'z', null)] };
        Assert.Equal("ada-fix-typo", PullRequestCheckout.Plan(Loaded(), folder, Five).LocalBranch);

        var nested = Pull(5, "Fix a typo in the greeting", "ada", "local-work/typo", 'f', AdaHello);
        Assert.Equal("ada-local-work/typo", PullRequestCheckout.Plan(Loaded(), HelloRefs(), nested).LocalBranch);

        var otherCase = HelloRefs() with { Refs = [.. HelloRefs().Refs, Local("Fix-Typo", 'z', null)] };
        Assert.Equal("ada-fix-typo", PullRequestCheckout.Plan(Loaded(), otherCase, Five).LocalBranch);
    }

    [Fact]
    public void A_local_branch_at_the_head_is_reused_and_when_current_disabled()
    {
        var refs = HelloRefs() with { Refs = [.. HelloRefs().Refs, Local("fix-typo", 'f', null)] };

        var plan = PullRequestCheckout.Plan(Loaded(), refs, Five);

        Assert.Equal(new PullRequestCheckoutPlan(PullRequestCheckoutKind.LocalBranch, "Check out ada:fix-typo", null, "origin", null, "fix-typo"), plan);

        var current = refs with { Head = new HeadState("fix-typo", Sha('f')) };
        Assert.Equal("fix-typo is already checked out", PullRequestCheckout.Plan(Loaded(), current, Five).DisabledReason);

        // Taken at another commit, the reused one comes later in the order.
        var later = HelloRefs() with { Refs = [.. HelloRefs().Refs, Local("fix-typo", 'z', null), Local("ada-fix-typo", 'f', null)] };
        Assert.Equal(("ada-fix-typo", PullRequestCheckoutKind.LocalBranch), (PullRequestCheckout.Plan(Loaded(), later, Five).LocalBranch, PullRequestCheckout.Plan(Loaded(), later, Five).Kind));
    }

    /// <summary><c>tool</c>: <c>origin</c> octo/tool (a fork of acme/tool), <c>upstream</c> acme/tool; <c>main</c>, <c>fix-typo</c> tracking <c>origin/fix-typo</c>.</summary>
    private static RepoRefs ToolRefs(string head = "fix-typo", IReadOnlyList<GitRef>? extra = null) => new(
        new HeadState(head, Sha(head == "main" ? 'a' : 'f')),
        [
            Local("fix-typo", 'f', "origin/fix-typo", head),
            Local("main", 'a', "upstream/main", head),
            Remote("origin/fix-typo", 'f'),
            Remote("origin/main", 'a'),
            Remote("upstream/main", 'a'),
            .. extra ?? [],
        ],
        [],
        ["origin", "upstream"]);

    private static RepositoryHostingState ToolState() => new(
        PullRequestsState.Loaded,
        new Dictionary<string, HostedRepository>(StringComparer.Ordinal) { ["origin"] = OctoTool, ["upstream"] = AcmeTool },
        AcmeTool,
        new PullRequestList([], false),
        null,
        new Dictionary<string, PullRequest>(StringComparer.Ordinal));
}
