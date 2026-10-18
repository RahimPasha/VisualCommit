using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Keep ignored files' check inside the pull's integration (D221; docs/phase-6-design.md 9.10,
/// 9.17), run against real git after a real fetch: with
/// <see cref="PullIntegrationOptions.KeepIgnored"/> a fast-forward, a merge or a rebase that would
/// write over an ignored file returns <see cref="PullOutcome.IgnoredFilesInTheWay"/> with the files
/// and what it would have done, runs no <c>git merge</c> or <c>git rebase</c> and never calls the
/// rebase's callback; a merge it runs has <c>--no-overwrite-ignore</c>; without it git's default
/// overwrites the file. A branch only ahead of its upstream is rebased as git does: up to date when
/// linear (U2), and refused when it holds a merge, which git would replay deleting the file (U5).
/// </summary>
public class IntegratePullTests
{
    private const string Local = "LOCAL\n";

    private static readonly PullIntegrationOptions Keep = new(KeepIgnored: true);

    /// <summary>
    /// <c>main</c> (README) tracking <c>origin/main</c>, to which another clone has pushed "Upstream
    /// adds secret" (<c>secret.env</c> "UP"); <c>.git/info/exclude</c> ignores <c>secret.env</c>.
    /// </summary>
    private static readonly Lazy<Task<TempRepo>> Behind = new(BuildBehindAsync);

    /// <summary>
    /// 0.4's U5 on <c>main</c>: U0 tracks <c>secret.env</c> and is pushed; U1 follows it and is
    /// pushed (the upstream); "Stop tracking secret" (<c>git rm --cached</c>, a <c>.gitignore</c>)
    /// follows locally; <c>feat</c> from U0 adds <c>f.txt</c> and merges <c>main</c>, which is then
    /// fast-forwarded to it: only ahead of its upstream, with a local merge. <c>u2</c> stops at
    /// "Stop tracking secret", also tracking <c>origin/main</c>: only ahead, linear (U2).
    /// </summary>
    private static readonly Lazy<Task<TempRepo>> Ahead = new(BuildAheadAsync);

    /// <summary>
    /// The review's shape beside U5 on <c>main</c>: U0 tracks <c>secret.env</c> and is pushed;
    /// <c>feat</c> from U0 adds <c>f.txt</c>; U1 stops tracking and ignores it and is pushed;
    /// "Local work" adds <c>local.txt</c>, and <c>feat</c> is merged; another clone then pushes U2
    /// (<c>u2.txt</c>). Neither the upstream nor the branch has <c>secret.env</c>, but feat's
    /// original branch point does.
    /// </summary>
    private static readonly Lazy<Task<TempRepo>> MergedSide = new(BuildMergedSideAsync);

    private static async Task<TempRepo> BuildBehindAsync()
    {
        var repo = await TempRepo.CreateAsync("pull");
        await repo.CommitFileAsync("README", "base\n", "base");
        await repo.AddBareRemoteAsync();
        await repo.PushAsync("origin", setUpstream: true, "main");
        await Scenarios.PushFromElsewhereAsync(repo, "main", "secret.env", "UP\n", "Upstream adds secret");
        await File.AppendAllTextAsync(Path.Combine(repo.Path, ".git", "info", "exclude"), "secret.env\n");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => repo.Dispose();
        return repo;
    }

    private static async Task<TempRepo> BuildAheadAsync()
    {
        var repo = await TempRepo.CreateAsync("pull-ahead");
        var u0 = await repo.WriteFile("README", "base\n").WriteFile("secret.env", "OLD\n").CommitAsync("U0");
        await repo.AddBareRemoteAsync();
        await repo.PushAsync("origin", setUpstream: true, "main");
        await repo.CommitFileAsync("README", "base\nu1\n", "U1");
        await repo.PushAsync("origin", setUpstream: false, "main");
        await repo.GitAsync("rm", "--quiet", "--cached", "secret.env");
        repo.DeleteFile("secret.env").WriteFile(".gitignore", "secret.env\n");
        await repo.CommitAsync("Stop tracking secret");
        await repo.CreateBranchAsync("u2");
        await repo.GitAsync("branch", "--quiet", "--set-upstream-to=origin/main", "u2");
        await repo.GitAsync("switch", "--quiet", "--create", "feat", u0);
        await repo.CommitFileAsync("f.txt", "f\n", "Add f");
        await repo.MergeAsync("main");
        await repo.GitAsync("switch", "--quiet", "main");
        await repo.GitAsync("merge", "--quiet", "--ff-only", "feat");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => repo.Dispose();
        return repo;
    }

    private static async Task<TempRepo> BuildMergedSideAsync()
    {
        var repo = await TempRepo.CreateAsync("pull-side");
        var u0 = await repo.WriteFile("README", "base\n").WriteFile("secret.env", "OLD\n").CommitAsync("U0");
        await repo.AddBareRemoteAsync();
        await repo.PushAsync("origin", setUpstream: true, "main");
        await repo.GitAsync("switch", "--quiet", "--create", "feat", u0);
        await repo.CommitFileAsync("f.txt", "f\n", "Add f");
        await repo.GitAsync("switch", "--quiet", "main");
        await repo.GitAsync("rm", "--quiet", "--cached", "secret.env");
        repo.DeleteFile("secret.env").WriteFile(".gitignore", "secret.env\n");
        await repo.CommitAsync("U1");
        await repo.PushAsync("origin", setUpstream: false, "main");
        await repo.CommitFileAsync("local.txt", "local\n", "Local work");
        await repo.MergeAsync("feat");
        await Scenarios.PushFromElsewhereAsync(repo, "main", "u2.txt", "u2\n", "U2");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => repo.Dispose();
        return repo;
    }

    private static async Task<TempRepo> CopyAsync(Lazy<Task<TempRepo>> template, string what) =>
        await (await HangWatchdogAttribute.SharedPreparationAsync(what, template.Value)).CopyAsync();

    /// <summary>The repository opened with a call log, its current branch read, and its upstream's remote fetched, as Pull's first step does.</summary>
    private static async Task<(GitRepository Repository, GitCallLog Calls, GitRef Branch)> FetchedAsync(TempRepo repo)
    {
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var branch = (await repository.ReadRefsAsync(TestCancelled)).Refs.Single(gitRef => gitRef.IsHead);
        await repository.FetchAsync(branch.UpstreamRemote, NetworkOptions.None, cancellationToken: TestCancelled);
        return (repository, calls, branch);
    }

    /// <summary>The integration, counting the calls of its rebase's callback.</summary>
    private static async Task<(PullResult Result, int BeforeRebase, IReadOnlyList<GitCallRecord> Calls)> PullAsync(
        GitRepository repository, GitCallLog calls, GitRef branch, PullIntegration integration, PullIntegrationOptions? options)
    {
        var mark = calls.Snapshot().Count;
        var beforeRebase = 0;
        var result = await repository.IntegratePullAsync(
            branch,
            integration,
            _ =>
            {
                beforeRebase++;
                return Task.CompletedTask;
            },
            pullOptions: options,
            cancellationToken: TestCancelled);
        return (result, beforeRebase, [.. calls.Snapshot().Skip(mark)]);
    }

    /// <summary>Nothing integrated for <c>secret.env</c>: the outcome, the files and what the pull chose, field by field (the record compares its list by reference).</summary>
    private static void AssertRefused(PullResult result, bool wouldRebase)
    {
        Assert.Equal(new PullResult(PullOutcome.IgnoredFilesInTheWay, 0) { IgnoredFiles = result.IgnoredFiles, WouldRebase = wouldRebase }, result);
        Assert.Equal(["secret.env"], result.IgnoredFiles);
    }

    private static bool Ran(IReadOnlyList<GitCallRecord> calls, string command) =>
        calls.Any(call => call.CommandText == "git " + command || call.CommandText.StartsWith("git " + command + " ", StringComparison.Ordinal));

    private static string Read(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

    public static TheoryData<string, string?> Modes => new()
    {
        // The fast-forward (pull.ff=only), the merge (the branch has a commit of its own) and the
        // rebase (pull.rebase=true, a commit of its own).
        { "fast-forward", "pull.ff=only" },
        { "merge", null },
        { "rebase", "pull.rebase=true" },
    };

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task With_keep_ignored_nothing_is_integrated_over_an_ignored_file(string mode, string? setting)
    {
        using var repo = await CopyAsync(Behind, "the pull template behind");
        await PrepareAsync(repo, mode, setting);
        var (repository, calls, branch) = await FetchedAsync(repo);
        var head = await repo.HeadAsync();

        var (result, beforeRebase, made) = await PullAsync(repository, calls, branch, PullIntegration.AsConfigured, Keep);

        AssertRefused(result, wouldRebase: mode == "rebase");
        Assert.False(Ran(made, "merge"), "git merge ran.");
        Assert.False(Ran(made, "rebase"), "git rebase ran.");
        Assert.Equal(0, beforeRebase);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(Local, Read(repo, "secret.env"));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Without_keep_ignored_git_integrates_over_the_ignored_file(string mode, string? setting)
    {
        using var repo = await CopyAsync(Behind, "the pull template behind");
        await PrepareAsync(repo, mode, setting);
        var (repository, calls, branch) = await FetchedAsync(repo);

        var (result, beforeRebase, made) = await PullAsync(repository, calls, branch, PullIntegration.AsConfigured, options: null);

        Assert.Equal(mode switch { "fast-forward" => PullOutcome.FastForward, "merge" => PullOutcome.Merged, _ => PullOutcome.Rebased }, result.Outcome);
        Assert.Equal(mode == "rebase" ? 1 : 0, beforeRebase);
        Assert.DoesNotContain(made, call => call.CommandText.Contains("--no-overwrite-ignore", StringComparison.Ordinal));
        Assert.Equal("UP\n", Read(repo, "secret.env"));
    }

    [Theory]
    [InlineData("fast-forward", "pull.ff=only", "git merge --ff-only --no-overwrite-ignore FETCH_HEAD")]
    [InlineData("merge", null, "git merge --no-edit --no-overwrite-ignore FETCH_HEAD")]
    public async Task With_keep_ignored_and_nothing_in_the_way_the_merge_runs_with_gits_option(string mode, string? setting, string command)
    {
        using var repo = await CopyAsync(Behind, "the pull template behind");
        await PrepareAsync(repo, mode, setting);
        repo.DeleteFile("secret.env");
        var (repository, calls, branch) = await FetchedAsync(repo);

        var (result, _, made) = await PullAsync(repository, calls, branch, PullIntegration.AsConfigured, Keep);

        Assert.Equal(mode == "merge" ? PullOutcome.Merged : PullOutcome.FastForward, result.Outcome);
        Assert.Contains(made, call => call.CommandText == command);
        Assert.Equal("UP\n", Read(repo, "secret.env"));
    }

    [Fact]
    public async Task With_keep_ignored_the_chosen_rebase_checks_too()
    {
        // "Pull from {up} (rebase)" reads no setting and rebases a branch with a commit of its own.
        using var repo = await CopyAsync(Behind, "the pull template behind");
        await PrepareAsync(repo, "rebase", setting: null);
        var (repository, calls, branch) = await FetchedAsync(repo);

        var (result, beforeRebase, made) = await PullAsync(repository, calls, branch, PullIntegration.Rebase, Keep);

        AssertRefused(result, wouldRebase: true);
        Assert.False(Ran(made, "rebase"), "git rebase ran.");
        Assert.Equal(0, beforeRebase);
    }

    [Fact]
    public async Task An_unborn_branch_is_checked_against_every_file_of_the_upstream()
    {
        // git merge into an unborn branch reads the upstream's tree into an empty index, which
        // overwrites an ignored file with or without --no-overwrite-ignore (0.4).
        using var template = await CopyAsync(Behind, "the pull template behind");
        using var repo = await TempRepo.CreateAsync("unborn");
        await repo.GitAsync("remote", "add", "origin", template.SiblingPath("origin.git"));
        await repo.ConfigAsync("branch.main.remote", "origin");
        await repo.ConfigAsync("branch.main.merge", "refs/heads/main");
        await File.AppendAllTextAsync(Path.Combine(repo.Path, ".git", "info", "exclude"), "secret.env\n", TestCancelled);
        repo.WriteFile("secret.env", Local);
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        await repository.FetchAsync("origin", NetworkOptions.None, cancellationToken: TestCancelled);
        var branch = new GitRef(
            "refs/heads/main", "main", RefKind.LocalBranch, string.Empty, Upstream: "origin/main", IsHead: true,
            UpstreamRef: "refs/remotes/origin/main", UpstreamRemote: "origin", UpstreamRemoteRef: "refs/heads/main");

        var (refused, _, made) = await PullAsync(repository, calls, branch, PullIntegration.AsConfigured, Keep);
        Assert.Equal(PullOutcome.IgnoredFilesInTheWay, refused.Outcome);
        Assert.Equal(["secret.env"], refused.IgnoredFiles);
        Assert.Contains(made, call => call.CommandText == "git ls-tree -r -z --name-only FETCH_HEAD");
        Assert.False(Ran(made, "merge"), "git merge ran.");
        Assert.Equal(Local, Read(repo, "secret.env"));

        var (merged, _, _) = await PullAsync(repository, calls, branch, PullIntegration.AsConfigured, options: null);
        Assert.NotEqual(PullOutcome.IgnoredFilesInTheWay, merged.Outcome);
        Assert.Equal("UP\n", Read(repo, "secret.env"));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("merges")]
    public async Task A_linear_branch_only_ahead_of_its_upstream_is_up_to_date_and_keeps_the_ignored_file(string rebase)
    {
        // U2: git rebase says "Current branch u2 is up to date." and writes nothing.
        using var repo = await CopyAsync(Ahead, "the pull template ahead");
        await repo.CheckoutAsync("u2");
        await repo.ConfigAsync("pull.rebase", rebase);
        repo.WriteFile("secret.env", Local);
        var (repository, calls, branch) = await FetchedAsync(repo);
        var head = await repo.HeadAsync();

        var (result, _, made) = await PullAsync(repository, calls, branch, PullIntegration.AsConfigured, Keep);

        Assert.Equal(PullOutcome.UpToDate, result.Outcome);
        Assert.True(Ran(made, "rebase"), "git rebase did not run.");
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(Local, Read(repo, "secret.env"));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("merges")]
    public async Task A_branch_only_ahead_of_its_upstream_with_a_local_merge_is_not_rebased_over_the_ignored_file(string rebase)
    {
        // U5: git checks out the upstream, which tracks secret.env, and replays the branch, plain
        // or with --rebase-merges, deleting the ignored file (0.4).
        using var repo = await CopyAsync(Ahead, "the pull template ahead");
        await repo.ConfigAsync("pull.rebase", rebase);
        repo.WriteFile("secret.env", Local);
        using var plain = await repo.CopyAsync();
        var (repository, calls, branch) = await FetchedAsync(repo);
        var head = await repo.HeadAsync();

        var (result, beforeRebase, made) = await PullAsync(repository, calls, branch, PullIntegration.AsConfigured, Keep);

        AssertRefused(result, wouldRebase: true);
        Assert.False(Ran(made, "rebase"), "git rebase ran.");
        Assert.Equal(0, beforeRebase);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(Local, Read(repo, "secret.env"));

        // Without Keep ignored files git's rebase runs and the file is gone.
        var (plainRepository, plainCalls, plainBranch) = await FetchedAsync(plain);
        var (rebased, plainBeforeRebase, plainMade) = await PullAsync(plainRepository, plainCalls, plainBranch, PullIntegration.AsConfigured, options: null);
        Assert.True(Ran(plainMade, "rebase"), "git rebase did not run.");
        Assert.Equal(1, plainBeforeRebase);
        Assert.NotEqual(PullOutcome.IgnoredFilesInTheWay, rebased.Outcome);
        Assert.False(File.Exists(Path.Combine(plain.Path, "secret.env")), "secret.env is still there.");
    }

    [Theory]
    [InlineData("merges")]
    [InlineData("true")]
    public async Task A_branch_with_a_merged_side_branch_is_not_rebased_over_what_the_side_branchs_branch_point_tracks(string rebase)
    {
        // --rebase-merges starts feat with "reset <U0>", which writes secret.env over the ignored
        // file, and the next "reset onto" deletes it (Git 2.36 and 2.55). The plain rebase replays
        // feat's change only and keeps the file, but under rebase.rebaseMerges (Git 2.42 and
        // later) the same git rebase keeps merges, so the check refuses both (9.6).
        using var repo = await CopyAsync(MergedSide, "the pull template with a merged side branch");
        await repo.ConfigAsync("pull.rebase", rebase);
        repo.WriteFile("secret.env", Local);
        using var plain = await repo.CopyAsync();
        var (repository, calls, branch) = await FetchedAsync(repo);
        var head = await repo.HeadAsync();

        var (result, beforeRebase, made) = await PullAsync(repository, calls, branch, PullIntegration.AsConfigured, Keep);

        AssertRefused(result, wouldRebase: true);
        Assert.False(Ran(made, "rebase"), "git rebase ran.");
        Assert.Equal(0, beforeRebase);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(Local, Read(repo, "secret.env"));

        // Without Keep ignored files git's rebase runs: with --rebase-merges the file is gone, the
        // plain rebase keeps it.
        var (plainRepository, plainCalls, plainBranch) = await FetchedAsync(plain);
        var (rebased, plainBeforeRebase, plainMade) = await PullAsync(plainRepository, plainCalls, plainBranch, PullIntegration.AsConfigured, options: null);
        Assert.Equal(PullOutcome.Rebased, rebased.Outcome);
        Assert.Equal(1, plainBeforeRebase);
        var rebaseCall = Assert.Single(plainMade, call => call.CommandText.StartsWith("git rebase", StringComparison.Ordinal));
        if (rebase == "merges")
        {
            Assert.Contains("--rebase-merges", rebaseCall.CommandText, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(plain.Path, "secret.env")), "secret.env is still there.");
        }
        else
        {
            Assert.DoesNotContain("--rebase-merges", rebaseCall.CommandText, StringComparison.Ordinal);
            Assert.Equal(Local, Read(plain, "secret.env"));
        }
    }

    /// <summary>
    /// The branch's state for <paramref name="mode"/>: a commit of its own for a merge or a rebase,
    /// the pull setting, and the ignored <c>secret.env</c> on disk.
    /// </summary>
    private static async Task PrepareAsync(TempRepo repo, string mode, string? setting)
    {
        if (mode != "fast-forward")
        {
            await repo.CommitFileAsync("local.txt", "local\n", "Local work");
        }

        if (setting is not null)
        {
            var (key, value) = (setting[..setting.IndexOf('=', StringComparison.Ordinal)], setting[(setting.IndexOf('=', StringComparison.Ordinal) + 1)..]);
            await repo.ConfigAsync(key, value);
        }

        repo.WriteFile("secret.env", Local);
    }
}
