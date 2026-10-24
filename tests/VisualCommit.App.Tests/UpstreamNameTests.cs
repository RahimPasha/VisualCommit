using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// How a branch's upstream is named in texts (docs/phase-6-design.md 10.4, "How an upstream is
/// named"; GD15, the lead's ruling of 2026-10-22): by the remote-tracking ref the remote's fetch
/// lines map it to, as <c>git status</c> names it, and only when no line maps it
/// <c>{remote}/{remote ref}</c>. <see cref="ToolbarViewModel.UpstreamText"/> makes every such text,
/// so the local branch's menu, Pull's tooltip (G25 step 12, whose key part WP-K's tooltips add),
/// the push's texts and a dashboard row follow. Real git reads the refs; the scenario is
/// "refspecs" (RS0), whose remotes rename their branches and fetch GitHub's pull requests.
/// </summary>
public class UpstreamNameTests
{
    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    [Fact]
    public async Task A_branch_that_tracks_a_pull_request_is_named_by_its_tracking_ref_as_git_status_names_it()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();
        await repo.GitAsync("switch", "--quiet", "--create", "pr/7", "--track", "refs/remotes/upstream/pr/7");

        var refs = await ReadRefsAsync(repo);
        var branch = refs.Refs.Single(gitRef => gitRef.FullName == "refs/heads/pr/7");

        // Git tracks the ref on the remote (0.5's P2); the app reads all three values (D101).
        Assert.Equal(("upstream", "refs/pull/7/head", "refs/remotes/upstream/pr/7"), (branch.UpstreamRemote, branch.UpstreamRemoteRef, branch.UpstreamRef));
        Assert.Equal("upstream/pr/7", ToolbarViewModel.UpstreamText(branch));
        Assert.Equal("upstream/pr/7", await StatusUpstreamAsync(repo));
        Assert.Equal("upstream/pr/7", FirstTrackingDestination(refs, branch));

        // Phase 5 named it upstream/refs/pull/7/head: what remains when no line maps it.
        Assert.Equal("upstream/refs/pull/7/head", ToolbarViewModel.UpstreamText(branch with { UpstreamRef = null }));

        // Every text that names it follows: the menus, the dashboard's row.
        var context = new RepositoryMenuContext("refspecs", repo.Path, refs, WorkingTreeStatus.Clean, [], ToolbarViewModel.StatesFor(refs, WorkingTreeStatus.Clean), IsOperationRunning: false);
        var (actions, _) = RecordingActions.Create();
        var menu = RepositoryMenusTests.Lines(RepositoryMenus.ForLocalBranch(context, "refs/heads/pr/7", actions));
        Assert.Contains("Pull from upstream/pr/7", menu);

        // Its Push and Force push are disabled (R4: refs/pull/7/head is no branch), naming it so too.
        Assert.Contains("Push pr/7 to upstream/pr/7 (disabled: \"upstream/pr/7 is refs/pull/7/head on upstream, not a branch\")", menu);
        Assert.Contains("Stop tracking upstream/pr/7", menu);
        Assert.Equal("pr/7 is level with upstream/pr/7", DashboardTexts.AheadBehind(refs, WorkingTreeStatus.Clean).ToolTip);
    }

    [Fact]
    public async Task With_the_default_fetch_line_every_upstream_reads_as_before()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();

        var refs = await ReadRefsAsync(repo);
        var main = refs.Refs.Single(gitRef => gitRef.FullName == "refs/heads/main");

        Assert.Equal(("origin", "refs/heads/main", "refs/remotes/origin/main"), (main.UpstreamRemote, main.UpstreamRemoteRef, main.UpstreamRef));
        Assert.Equal("origin/main", ToolbarViewModel.UpstreamText(main));
        Assert.Equal("origin/main", ToolbarViewModel.UpstreamText(main with { UpstreamRef = null }));
        Assert.Equal("origin/main", await StatusUpstreamAsync(repo));
        Assert.Equal("origin/main", FirstTrackingDestination(refs, main));
    }

    [Fact]
    public async Task A_branch_that_tracks_a_renamed_remote_branch_is_named_by_its_tracking_ref()
    {
        // fork fetches its branches into fork/up/* (0.5's B1): git tracks fork's main.
        using var repo = await Scenarios.RenamingRefspecAsync();
        await repo.GitAsync("switch", "--quiet", "--create", "forked", "--track", "refs/remotes/fork/up/main");

        var refs = await ReadRefsAsync(repo);
        var branch = refs.Refs.Single(gitRef => gitRef.FullName == "refs/heads/forked");

        Assert.Equal(("fork", "refs/heads/main", "refs/remotes/fork/up/main"), (branch.UpstreamRemote, branch.UpstreamRemoteRef, branch.UpstreamRef));
        Assert.Equal("fork/up/main", ToolbarViewModel.UpstreamText(branch));
        Assert.Equal("fork/up/main", await StatusUpstreamAsync(repo));
        Assert.Equal("fork/up/main", FirstTrackingDestination(refs, branch));
    }

    [Fact]
    public async Task Of_two_lines_that_fetch_a_branch_the_first_names_its_upstream()
    {
        // 0.5's C2 without the server's trunk: git fetches main into origin/main and origin/trunk,
        // and names the upstream by the first line's tracking ref, as DestinationsOf lists them.
        using var repo = await Scenarios.RenamingRefspecAsync();
        await repo.GitAsync("config", "--add", "remote.origin.fetch", "refs/heads/main:refs/remotes/origin/trunk");
        await repo.GitAsync("fetch", "--quiet", "origin");

        var refs = await ReadRefsAsync(repo);
        var main = refs.Refs.Single(gitRef => gitRef.FullName == "refs/heads/main");

        Assert.Equal(["refs/remotes/origin/main", "refs/remotes/origin/trunk"], refs.Refspecs!.DestinationsOf("origin", "refs/heads/main"));
        Assert.Equal("origin/main", ToolbarViewModel.UpstreamText(main));
        Assert.Equal("origin/main", await StatusUpstreamAsync(repo));
    }

    [Fact]
    public async Task Under_a_mirror_line_first_git_maps_the_upstream_to_a_local_branch_and_the_text_falls_back_to_its_remote_and_name()
    {
        // 0.5's F on origin itself, the mirror line first and a renaming line after it: git's first
        // matching line maps refs/heads/main to the local refs/heads/main, no tracking ref, so the
        // text is origin/main; the design's first destination under refs/remotes/ would be
        // origin/up/main, and git's status says main. Nothing is fetched: the upstream is configuration.
        using var repo = await Scenarios.RenamingRefspecAsync();
        await repo.GitAsync("config", "--unset-all", "remote.origin.fetch");
        await repo.GitAsync("config", "--add", "remote.origin.fetch", "+refs/*:refs/*");
        await repo.GitAsync("config", "--add", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/up/*");

        var refs = await ReadRefsAsync(repo);
        var main = refs.Refs.Single(gitRef => gitRef.FullName == "refs/heads/main");

        Assert.Equal(["refs/heads/main", "refs/remotes/origin/up/main"], refs.Refspecs!.DestinationsOf("origin", "refs/heads/main"));
        Assert.Equal(("origin", "refs/heads/main", "refs/heads/main"), (main.UpstreamRemote, main.UpstreamRemoteRef, main.UpstreamRef));
        Assert.Equal("origin/main", ToolbarViewModel.UpstreamText(main));
        Assert.Equal("main", await StatusUpstreamAsync(repo));
        Assert.Equal("origin/up/main", FirstTrackingDestination(refs, main));
    }

    [Fact]
    public void Without_a_tracking_ref_the_upstream_is_named_by_its_remote_and_its_name_there()
    {
        // A snapshot built by hand, or a line that maps the ref to no tracking ref (a mirror line's
        // refs/heads/<y>, 10.1): the remote and the ref's name there.
        var branch = new GitRef("refs/heads/pr/7", "pr/7", RefKind.LocalBranch, new string('a', 40), UpstreamRemote: "upstream", UpstreamRemoteRef: "refs/pull/7/head");
        Assert.Equal("upstream/refs/pull/7/head", ToolbarViewModel.UpstreamText(branch));
        Assert.Equal("upstream/refs/pull/7/head", ToolbarViewModel.UpstreamText(branch with { UpstreamRef = "refs/heads/pr/7" }));
        Assert.Equal("upstream/refs/pull/7/head", ToolbarViewModel.UpstreamText(branch with { UpstreamRef = "refs/remotes/" }));

        // A local upstream keeps its branch's name alone (D114).
        var local = new GitRef("refs/heads/loc", "loc", RefKind.LocalBranch, new string('a', 40), Upstream: "main", UpstreamRef: "refs/heads/main", UpstreamRemote: ".", UpstreamRemoteRef: "refs/heads/main");
        Assert.Equal("main", ToolbarViewModel.UpstreamText(local));
        Assert.Null(ToolbarViewModel.UpstreamText(new GitRef("refs/heads/x", "x", RefKind.LocalBranch, new string('a', 40))));
    }

    private static async Task<RepoRefs> ReadRefsAsync(TempRepo repo)
    {
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        return await repository.ReadRefsAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>How <c>git status</c> names the checked-out branch's upstream (<c># branch.upstream</c>), or null.</summary>
    private static async Task<string?> StatusUpstreamAsync(TempRepo repo)
    {
        const string Prefix = "# branch.upstream ";
        var output = (await repo.GitAsync("status", "--porcelain=v2", "--branch")).StandardOutput;
        return output.Split('\n').Select(line => line.TrimEnd('\r')).FirstOrDefault(line => line.StartsWith(Prefix, StringComparison.Ordinal))?[Prefix.Length..];
    }

    /// <summary>10.4's rule as the design states it: the first of the remote's destinations of the ref under <c>refs/remotes/</c>, without it.</summary>
    private static string? FirstTrackingDestination(RepoRefs refs, GitRef branch) =>
        refs.Refspecs!.DestinationsOf(branch.UpstreamRemote!, branch.UpstreamRemoteRef!)
            .FirstOrDefault(destination => destination.StartsWith("refs/remotes/", StringComparison.Ordinal))?["refs/remotes/".Length..];
}
