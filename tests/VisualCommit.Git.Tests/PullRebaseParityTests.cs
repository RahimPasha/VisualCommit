using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// "Pull from {up} (rebase)" (<see cref="GitRepository.IntegratePullAsync(GitRef, PullIntegration, Func{string, Task}?, NetworkOptions?, CancellationToken)"/>
/// with <see cref="PullIntegration.Rebase"/>, docs/phase-4-design.md section 6, D147) against
/// <c>git pull --rebase</c> on copies of one template (<see cref="PullParity"/>): git's pull
/// settings are not read, a branch with nothing of its own is fast-forwarded, a stop is
/// conflicts, and the backup's callback runs before every rebase. Here each state of the branch
/// against its upstream with nothing set, and a stop without a conflicted file;
/// <see cref="PullRebaseSettingsParityTests"/> has git's pull settings, and
/// <see cref="PullRebaseForkPointParityTests"/> an upstream rewritten by a force push, the fork
/// point of a local upstream and a local upstream that only diverged. Classes of their own, so
/// that they run side by side. Runs on Git 2.36 and CI's git.
/// </summary>
public class PullRebaseParityTests
{
    public static TheoryData<string, PullState> Cases => new()
    {
        { "none", PullState.Diverged },
        { "none", PullState.FastForward },
        { "none", PullState.LocalAhead },
        { "none", PullState.Same },
        { "none", PullState.Conflicting },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_chosen_rebase_ends_where_git_pull_rebase_ends(string settings, PullState state) =>
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(state), settings, PullIntegration.Rebase, leftOutByForkPoint: 0);

    [Theory]
    [InlineData(PullIntegration.Rebase, "none")]
    [InlineData(PullIntegration.AsConfigured, "pull.rebase=true")]
    public async Task A_rebase_that_stops_without_a_conflicted_file_is_a_stop_as_git_pull_leaves_it(PullIntegration integration, string settings)
    {
        // An untracked file is in the way of the branch's first pick, which git reschedules (0.1);
        // phase 3 took only unmerged paths for a stop.
        using var template = await Scenarios.RemoteSyncAsync();
        await template.GitAsync("remote", "set-url", "origin", "../origin.git");
        await Scenarios.PushFromElsewhereAsync(template, "main", "src/other.txt", "other\n", "Other work");
        await template.CommitFileAsync("in.txt", "tracked\n", "Add in");
        template.DeleteFile("in.txt");
        await template.CommitAsync("Remove in");
        template.WriteFile("in.txt", "untracked\n");

        var result = await PullParity.CompareAsync(template, settings, integration, leftOutByForkPoint: 0);

        Assert.Equal(new PullResult(PullOutcome.Conflicts, 0), result);
    }

    [Fact]
    public async Task A_rebase_whose_stashed_changes_conflict_when_put_back_is_rebased_and_says_so()
    {
        // The review's findings 12 and 19: with rebase.autoStash git stashes the user's change of
        // src/shared.txt, rebases, and putting it back conflicts with the upstream's change of
        // that file; git exits 0 and keeps the stash, as git pull --rebase does.
        using var repo = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(repo, "main", "src/shared.txt", "shared elsewhere\n", "Shared elsewhere");
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");
        await repo.ConfigAsync("rebase.autoStash", "true");
        repo.WriteFile("src/shared.txt", "shared mine\n");
        using var plain = await repo.CopyAsync();
        var repository = await RepositoryTestSupport.OpenAsync(repo);
        var branch = (await repository.ReadRefsAsync(RepositoryTestSupport.TestCancelled)).Refs.Single(gitRef => gitRef.IsHead);
        await repository.FetchAsync(branch.UpstreamRemote, NetworkOptions.None, cancellationToken: RepositoryTestSupport.TestCancelled);

        var result = await repository.IntegratePullAsync(branch, PullIntegration.Rebase, beforeRebase: null, cancellationToken: RepositoryTestSupport.TestCancelled);

        var pull = await PullParity.RunGitAsync(plain, "pull", "--rebase");
        var seen = $"git pull --rebase: exit {pull.ExitCode}; stdout <{pull.StandardOutput}>; stderr <{pull.StandardError}>; status <{(await plain.GitAsync("status", "--porcelain")).StandardOutput}>; app {result}";
        Assert.True(result is { Outcome: PullOutcome.Rebased, AutostashConflicted: true }, seen);
        Assert.Equal("UU src/shared.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput.TrimEnd());
        Assert.Equal((await plain.GitAsync("status", "--porcelain")).StandardOutput, (await repo.GitAsync("status", "--porcelain")).StandardOutput);
        Assert.Single(await repo.StashListAsync());
    }
}

/// <summary><see cref="PullRebaseParityTests"/> for git's pull settings, which the chosen rebase does not read (an invalid value included), and the configured pull's callback.</summary>
public class PullRebaseSettingsParityTests
{
    public static TheoryData<string, PullState> Cases => new()
    {
        { "pull.ff=only", PullState.Diverged },
        { "pull.rebase=false", PullState.Diverged },
        { "pull.rebase=merges", PullState.DivergedWithMerge },
        { "pull.rebase=preserve", PullState.Diverged },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_chosen_rebase_ends_where_git_pull_rebase_ends(string settings, PullState state) =>
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(state), settings, PullIntegration.Rebase, leftOutByForkPoint: 0);

    [Fact]
    public async Task The_configured_pull_calls_the_callback_before_its_rebase_and_not_before_a_fast_forward()
    {
        // PullParity checks the callback for every case; these two are the configured pull's
        // rebase and fast-forward with pull.rebase=true.
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(PullState.Diverged), "pull.rebase=true", PullIntegration.AsConfigured, leftOutByForkPoint: 0);
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(PullState.FastForward), "pull.rebase=true", PullIntegration.AsConfigured, leftOutByForkPoint: 0);
    }
}

/// <summary>
/// <see cref="PullRebaseParityTests"/> where git's fork point decides (0.1, section 6): the
/// upstream's reflog holds a commit the branch has, which the upstream no longer has, so
/// <c>git pull --rebase</c> and the app's <c>git rebase</c> leave it out and the result counts
/// it (<see cref="PullResult.LeftOutByForkPoint"/>).
/// </summary>
public class PullRebaseForkPointParityTests
{
    [Fact]
    public async Task An_upstream_rewritten_by_a_force_push_leaves_its_old_commit_out_and_counts_it() =>
        await PullParity.CompareAsync(await ForkPointTemplates.ForcePushedAsync(), "none", PullIntegration.Rebase, leftOutByForkPoint: 1);

    [Fact]
    public async Task The_configured_rebase_pull_counts_what_the_force_push_left_out_too() =>
        await PullParity.CompareAsync(await ForkPointTemplates.ForcePushedAsync(), "pull.rebase=true", PullIntegration.AsConfigured, leftOutByForkPoint: 1);

    [Fact]
    public async Task A_commit_the_local_upstream_once_held_is_left_out_and_counted() =>
        await PullParity.CompareAsync(await ForkPointTemplates.LocalUpstreamResetAsync(), "none", PullIntegration.Rebase, leftOutByForkPoint: 1);

    [Fact]
    public async Task A_local_upstream_that_only_diverged_leaves_nothing_out() =>
        await PullParity.CompareAsync(await PullParityTemplates.LocalAsync(PullState.Diverged), "none", PullIntegration.Rebase, leftOutByForkPoint: 0);

    [Fact]
    public async Task A_commit_that_becomes_empty_is_not_counted_as_left_out_by_the_fork_point()
    {
        // The upstream gained a commit that holds the branch's own change and more (not the same
        // patch): the rebase drops the branch's commit because it became empty, which is not the
        // fork point's doing. HEAD gains only the upstream's commit.
        var result = await PullParity.CompareAsync(await ForkPointTemplates.UpstreamHoldsTheChangeAsync(), "none", PullIntegration.Rebase, leftOutByForkPoint: 0);

        Assert.Equal(new PullResult(PullOutcome.Rebased, 1), result);
    }
}

/// <summary>The templates of <see cref="PullRebaseForkPointParityTests"/>, each built once per test process and copied for every case.</summary>
internal static class ForkPointTemplates
{
    private static readonly Lazy<Task<TempRepo>> ForcePushed = new(() => KeepUntilExit(BuildForcePushedAsync()));
    private static readonly Lazy<Task<TempRepo>> LocalUpstreamReset = new(() => KeepUntilExit(BuildLocalUpstreamResetAsync()));
    private static readonly Lazy<Task<TempRepo>> UpstreamHoldsTheChange = new(() => KeepUntilExit(BuildUpstreamHoldsTheChangeAsync()));

    /// <summary>
    /// The remote sync scenario on main (origin at <c>../origin.git</c>): main has origin's
    /// "Other work" (fetched, so origin/main's reflog holds it) and its own "Local work"; origin's
    /// main was then rewritten from the clone <c>other</c> ("Other work, rewritten" in place of
    /// "Other work", the same file with other content) and force-pushed. The pull's fetch brings it.
    /// </summary>
    public static Task<TempRepo> ForcePushedAsync() =>
        HangWatchdogAttribute.SharedPreparationAsync("the pull rebase template force pushed", ForcePushed.Value);

    /// <summary>
    /// topic's upstream is the local main (remote "."): main got "Kept for a while" (x.txt),
    /// topic branched from it and added "Topic work"; main was then reset back over it and got
    /// "Main later work". main's reflog holds the commit topic still has. HEAD on topic.
    /// </summary>
    public static Task<TempRepo> LocalUpstreamResetAsync() =>
        HangWatchdogAttribute.SharedPreparationAsync("the pull rebase template local upstream reset", LocalUpstreamReset.Value);

    /// <summary>
    /// The remote sync scenario on main: main's own "Local work" adds src/local.txt; origin's main
    /// then got, from the clone <c>other</c>, "Local work and more", which adds the same
    /// src/local.txt and src/more.txt. The pull's fetch brings it.
    /// </summary>
    public static Task<TempRepo> UpstreamHoldsTheChangeAsync() =>
        HangWatchdogAttribute.SharedPreparationAsync("the pull rebase template upstream holds the change", UpstreamHoldsTheChange.Value);

    private static async Task<TempRepo> KeepUntilExit(Task<TempRepo> building)
    {
        var repo = await building;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => repo.Dispose();
        return repo;
    }

    private static async Task<TempRepo> BuildForcePushedAsync()
    {
        var repo = await Scenarios.RemoteSyncAsync();
        try
        {
            await repo.GitAsync("remote", "set-url", "origin", "../origin.git");
            await Scenarios.PushFromElsewhereAsync(repo, "main", "src/other.txt", "other\n", "Other work");
            await repo.GitAsync("fetch", "--quiet", "origin");
            await repo.GitAsync("merge", "--quiet", "--ff-only", "refs/remotes/origin/main");
            await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");

            var other = repo.OpenSibling("other");
            await other.GitAsync("reset", "--quiet", "--hard", "HEAD~1");
            await other.CommitFileAsync("src/other.txt", "other, rewritten\n", "Other work, rewritten");
            await other.GitAsync("push", "--quiet", "--force", "origin", "refs/heads/main:refs/heads/main");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildLocalUpstreamResetAsync()
    {
        var repo = await TempRepo.CreateAsync("forkpoint");
        try
        {
            await repo.CommitFileAsync("README.md", "# Fork point\n", "Initial commit");
            await repo.CommitFileAsync("x.txt", "x\n", "Kept for a while");
            await repo.CreateBranchAsync("topic");
            await repo.ConfigAsync("branch.topic.remote", ".");
            await repo.ConfigAsync("branch.topic.merge", "refs/heads/main");
            await repo.CheckoutAsync("topic");
            await repo.CommitFileAsync("topic.txt", "topic\n", "Topic work");
            await repo.CheckoutAsync("main");
            await repo.GitAsync("reset", "--quiet", "--hard", "HEAD~1");
            await repo.CommitFileAsync("later.txt", "later\n", "Main later work");
            await repo.CheckoutAsync("topic");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildUpstreamHoldsTheChangeAsync()
    {
        var repo = await Scenarios.RemoteSyncAsync();
        try
        {
            await repo.GitAsync("remote", "set-url", "origin", "../origin.git");
            await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");
            var other = await repo.CloneSiblingAsync("other", repo.SiblingPath("origin.git"));

            // The bare origin's HEAD names no branch of it, so the clone checks nothing out.
            await other.GitAsync("checkout", "--quiet", "-B", "main", "refs/remotes/origin/main");
            other.WriteFile("src/local.txt", "local\n").WriteFile("src/more.txt", "more\n");
            await other.CommitAsync("Local work and more");
            await other.GitAsync("push", "--quiet", "origin", "refs/heads/main:refs/heads/main");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }
}
