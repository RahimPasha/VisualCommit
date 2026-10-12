using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>How main stands against origin/main before a pull (<see cref="PullParityTemplates"/>).</summary>
public enum PullState
{
    /// <summary>origin has a commit that main lacks.</summary>
    FastForward,

    /// <summary>origin and main each have a commit, in different files.</summary>
    Diverged,

    /// <summary>main has a commit that origin lacks.</summary>
    LocalAhead,

    /// <summary>Nothing new on either side.</summary>
    Same,

    /// <summary>origin and main each changed the same line.</summary>
    Conflicting,

    /// <summary>Diverged, and main's own history holds a merge commit.</summary>
    DivergedWithMerge,
}

/// <summary>
/// The app's pull (<see cref="GitRepository.FetchAsync"/>, then
/// <see cref="GitRepository.IntegratePullAsync"/>) against <c>git pull</c> itself (phase 3 design
/// 2.18, D84), for the settings that merge: here nothing set and <c>pull.rebase=false</c>;
/// <see cref="PullFfOnlyParityTests"/> and <see cref="PullFfParityTests"/> have <c>pull.ff</c>,
/// <see cref="PullLocalUpstreamParityTests"/> a local upstream, and
/// <see cref="PullFailureParityTests"/> what fails. <see cref="PullParity"/> says how they are
/// compared; <see cref="PullRebaseTrueParityTests"/> and the classes beside it have the settings
/// that rebase, and <see cref="PullRebaseParityTests"/> (phase 4) the rebase chosen for one pull.
/// <para>
/// Each group of settings is a class of its own, because xunit runs the tests of one class (the
/// rows of a theory included) one after another and different classes side by side: as two
/// classes these were the longest of the test run. The groups share only the templates, which
/// are built once per process behind a lock and copied for each case.
/// </para>
/// </summary>
public class PullParityTests
{
    public static TheoryData<string, PullState> Cases => new()
    {
        { "none", PullState.FastForward },
        { "none", PullState.Diverged },
        { "none", PullState.LocalAhead },
        { "none", PullState.Same },
        { "none", PullState.Conflicting },
        { "pull.rebase=false", PullState.Diverged },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_apps_pull_ends_where_git_pull_ends(string settings, PullState state) =>
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(state), settings);

    [Fact]
    public async Task A_branch_without_an_upstream_cannot_be_integrated()
    {
        using var repo = await (await PullParityTemplates.SyncAsync(PullState.Same)).CopyAsync();
        var repository = await OpenAsync(repo);
        var topic = (await repository.ReadRefsAsync(TestCancelled)).Refs.Single(gitRef => gitRef.FullName == "refs/heads/topic");

        await Assert.ThrowsAsync<ArgumentException>(() => repository.IntegratePullAsync(topic, cancellationToken: TestCancelled));
    }
}

/// <summary><see cref="PullParityTests"/> for <c>pull.ff=only</c>.</summary>
public class PullFfOnlyParityTests
{
    public static TheoryData<string, PullState> Cases => new()
    {
        { "pull.ff=only", PullState.FastForward },
        { "pull.ff=only", PullState.Diverged },
        { "pull.ff=only", PullState.LocalAhead },
        { "pull.ff=only", PullState.Conflicting },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_apps_pull_ends_where_git_pull_ends(string settings, PullState state) =>
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(state), settings);
}

/// <summary><see cref="PullParityTests"/> for the other values of <c>pull.ff</c>.</summary>
public class PullFfParityTests
{
    public static TheoryData<string, PullState> Cases => new()
    {
        { "pull.ff=false", PullState.FastForward },
        { "pull.ff=false", PullState.Diverged },
        { "pull.ff=true", PullState.FastForward },
        { "pull.ff=true", PullState.Diverged },
        { "pull.ff=no", PullState.FastForward },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_apps_pull_ends_where_git_pull_ends(string settings, PullState state) =>
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(state), settings);
}

/// <summary><see cref="PullParityTests"/> for a branch whose upstream is a local branch.</summary>
public class PullLocalUpstreamParityTests
{
    public static TheoryData<string, PullState> LocalUpstreamCases => new()
    {
        { "none", PullState.FastForward },
        { "none", PullState.Diverged },
        { "branch.loc.rebase=true", PullState.Diverged },
        { "pull.ff=only", PullState.Diverged },
        { "pull.ff=false", PullState.FastForward },
    };

    [Theory]
    [MemberData(nameof(LocalUpstreamCases))]
    public async Task A_pull_from_a_local_upstream_ends_where_git_pull_ends(string settings, PullState state) =>
        await PullParity.CompareAsync(await PullParityTemplates.LocalAsync(state), settings);
}

/// <summary><see cref="PullParityTests"/> for what fails: a value the app does not understand, an upstream gone from the remote.</summary>
public class PullFailureParityTests
{
    [Theory]
    [InlineData("pull.rebase", "preserve", "pull.rebase is set to preserve, which this app does not understand.")]
    [InlineData("pull.rebase", "sometimes", "pull.rebase is set to sometimes, which this app does not understand.")]
    [InlineData("branch.main.rebase", "Merges", "branch.main.rebase is set to Merges, which this app does not understand.")]
    [InlineData("pull.ff", "sometimes", "pull.ff is set to sometimes, which this app does not understand.")]
    public async Task A_value_the_app_does_not_understand_fails_before_anything_changes(string key, string value, string message)
    {
        using var repo = await (await PullParityTemplates.SyncAsync(PullState.Diverged)).CopyAsync();
        await repo.ConfigAsync(key, value);
        var head = await repo.HeadAsync();
        var repository = await OpenAsync(repo);
        await repository.FetchAsync("origin", NetworkOptions.None, cancellationToken: TestCancelled);
        var branch = (await repository.ReadRefsAsync(TestCancelled)).Refs.Single(gitRef => gitRef.IsHead);

        var failure = await Assert.ThrowsAsync<GitException>(() => repository.IntegratePullAsync(branch, cancellationToken: TestCancelled));

        Assert.Equal(message, failure.StandardError);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(string.Empty, (await repo.GitAsync("status", "--porcelain")).StandardOutput);

        // git pull refuses such a value too (preserve only since Git 2.34, so it is not asked here).
        if (value != "preserve")
        {
            Assert.False((await PullParity.RunGitAsync(repo, "pull")).Succeeded);
        }
    }

    [Theory]
    [InlineData("none")]
    [InlineData("pull.rebase=true")]
    [InlineData("pull.ff=only")]
    public async Task An_upstream_deleted_on_the_remote_is_not_integrated_where_git_pull_fails(string settings)
    {
        // published tracks origin/published, which is deleted on origin; without a prune (D99) the
        // remote branch stays here, and the fetch writes only not-for-merge lines.
        using var repo = await Scenarios.RemoteSyncAsync();
        if (settings != "none")
        {
            var pair = settings.Split('=', 2);
            await repo.ConfigAsync(pair[0], pair[1]);
        }

        await repo.CheckoutAsync("published");
        await repo.CommitFileAsync("own.txt", "own\n", "Own work");
        var head = await repo.HeadAsync();
        var deleted = await NewRunner().RunAsync(new GitCommand("branch", "-D", "published") { WorkingDirectory = repo.SiblingPath("origin.git") }, TestCancelled);
        Assert.True(deleted.Succeeded, deleted.StandardError);
        var repository = await OpenAsync(repo);
        var branch = (await repository.ReadRefsAsync(TestCancelled)).Refs.Single(gitRef => gitRef.IsHead);
        await repository.FetchAsync("origin", NetworkOptions.None, cancellationToken: TestCancelled);
        Assert.DoesNotContain(
            (await File.ReadAllTextAsync(Path.Combine(repository.GitDirectory, "FETCH_HEAD"), TestCancelled)).Split('\n'),
            line => line.Split('\t') is [{ Length: > 0 }, { Length: 0 }, ..]);

        Assert.Equal(new PullResult(PullOutcome.UpstreamGone, 0), await repository.IntegratePullAsync(branch, cancellationToken: TestCancelled));

        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(string.Empty, (await repo.GitAsync("status", "--porcelain")).StandardOutput);
        var pull = await PullParity.RunGitAsync(repo, "pull");
        Assert.False(pull.Succeeded);
        Assert.Contains("no such ref was fetched", pull.StandardError, StringComparison.Ordinal);
    }
}

/// <summary>
/// <see cref="PullParityTests"/> for the settings that rebase: here <c>pull.rebase=true</c>;
/// <see cref="PullRebaseModesParityTests"/> has its other modes and <c>branch.&lt;b&gt;.rebase</c>,
/// <see cref="PullRebaseFfParityTests"/> <c>pull.ff</c> with it, and
/// <see cref="PullRebaseValuesParityTests"/> values git reads its own way. Classes of their own,
/// so that they run side by side. (Phase 3's <c>PullRebaseParityTests</c>: the name went to the
/// rebase chosen for one pull, docs/phase-4-design.md section 6.)
/// </summary>
public class PullRebaseTrueParityTests
{
    public static TheoryData<string, PullState> Cases => new()
    {
        { "pull.rebase=true", PullState.FastForward },
        { "pull.rebase=true", PullState.Diverged },
        { "pull.rebase=true", PullState.LocalAhead },
        { "pull.rebase=true", PullState.Same },
        { "pull.rebase=true", PullState.Conflicting },
        { "pull.rebase=true", PullState.DivergedWithMerge },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_apps_pull_ends_where_git_pull_ends(string settings, PullState state) =>
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(state), settings);
}

/// <summary><see cref="PullRebaseTrueParityTests"/> for <c>pull.rebase</c>'s other modes and <c>branch.&lt;b&gt;.rebase</c>.</summary>
public class PullRebaseModesParityTests
{
    public static TheoryData<string, PullState> Cases => new()
    {
        { "pull.rebase=merges", PullState.Diverged },
        { "pull.rebase=merges", PullState.DivergedWithMerge },
        { "pull.rebase=interactive", PullState.Diverged },
        { "branch.main.rebase=true", PullState.Diverged },
        { "branch.main.rebase=false pull.rebase=true", PullState.Diverged },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_apps_pull_ends_where_git_pull_ends(string settings, PullState state) =>
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(state), settings);
}

/// <summary><see cref="PullRebaseTrueParityTests"/> with <c>pull.ff</c>.</summary>
public class PullRebaseFfParityTests
{
    public static TheoryData<string, PullState> Cases => new()
    {
        { "pull.ff=only pull.rebase=true", PullState.FastForward },
        { "pull.ff=only pull.rebase=true", PullState.Diverged },
        { "pull.ff=false pull.rebase=true", PullState.FastForward },
        { "pull.ff=false pull.rebase=true", PullState.Diverged },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_apps_pull_ends_where_git_pull_ends(string settings, PullState state) =>
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(state), settings);
}

/// <summary><see cref="PullRebaseTrueParityTests"/> for values that git reads its own way.</summary>
public class PullRebaseValuesParityTests
{
    public static TheoryData<string, PullState> Cases => new()
    {
        // Values that git reads its own way: a number and "m" (on Git 2.36 the number 0 with a
        // unit, so false) as booleans, the short form of interactive, a word in another case.
        { "pull.rebase=1", PullState.Diverged },
        { "pull.rebase=m", PullState.DivergedWithMerge },
        { "branch.main.rebase=i", PullState.Diverged },
        { "pull.rebase=Yes", PullState.Diverged },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_apps_pull_ends_where_git_pull_ends(string settings, PullState state) =>
        await PullParity.CompareAsync(await PullParityTemplates.SyncAsync(state), settings);
}

/// <summary>
/// How a pull is compared: two copies of the same repository, the same settings, one pulled by
/// the app and one by git, and then the same HEAD (the same commit id, so the same trees,
/// parents, messages and dates), the same branches, the same conflicts and the same operation in
/// progress. With nothing configured the app merges (D78), which is what <c>git pull
/// --no-rebase</c> does, so that is the command compared then. The copies' <c>origin</c> is the
/// relative path <c>../origin.git</c>, so that a merge's message ("Merge branch 'main' of
/// ../origin") is the same in both. Every call runs with the test process's dates (D72), so
/// commits made on both sides get the same ids. CI runs these on its git too (design 7.7, risk 4).
/// <para>
/// The app's pull is given the callback that saves a backup before a rebase
/// (docs/phase-4-design.md section 6): it must be called once, with the old tip, when the pull
/// rebased (the result says rebased, or git left a rebase in progress), and never when it
/// fast-forwarded or merged. With <see cref="PullIntegration.Rebase"/> the command compared is
/// <c>git pull --rebase</c>, and the result's <see cref="PullResult.LeftOutByForkPoint"/> must
/// be the count the case expects.
/// </para>
/// </summary>
internal static class PullParity
{
    public static Task CompareAsync(TempRepo template, string settings) =>
        CompareAsync(template, settings, PullIntegration.AsConfigured, leftOutByForkPoint: 0);

    public static async Task CompareAsync(TempRepo template, string settings, PullIntegration integration, int leftOutByForkPoint)
    {
        using var appCopy = await template.CopyAsync();
        using var gitCopy = await template.CopyAsync();
        var pairs = settings == "none"
            ? []
            : settings.Split(' ').Select(setting => setting.Split('=', 2)).Select(pair => (Key: pair[0], Value: pair[1])).ToList();
        foreach (var (key, value) in pairs)
        {
            await appCopy.ConfigAsync(key, value);
            await gitCopy.ConfigAsync(key, value);
        }

        // The app: the fetch of the upstream's remote (none for a local upstream), then the integration.
        var repository = await OpenAsync(appCopy);
        var refs = await repository.ReadRefsAsync(TestCancelled);
        var before = refs.Head.Sha!;
        var branch = refs.Refs.Single(gitRef => gitRef.IsHead);
        if (!branch.HasLocalUpstream)
        {
            await repository.FetchAsync(branch.UpstreamRemote, NetworkOptions.None, cancellationToken: TestCancelled);
        }

        PullResult? result = null;
        GitException? failure = null;
        var backups = new List<string>();
        try
        {
            result = await repository.IntegratePullAsync(
                branch,
                integration,
                async oldTip =>
                {
                    // The callback runs before the rebase: HEAD is still the old tip.
                    Assert.Equal(oldTip, await appCopy.HeadAsync());
                    backups.Add(oldTip);
                },
                cancellationToken: TestCancelled);
        }
        catch (GitException exception)
        {
            failure = exception;
        }

        // git pull, with --no-rebase when nothing is configured (D78); git pull --rebase for the chosen rebase.
        string[] gitPull = integration == PullIntegration.Rebase ? ["pull", "--rebase"] : pairs.Count == 0 ? ["pull", "--no-rebase"] : ["pull"];
        var pull = await RunGitAsync(gitCopy, gitPull);

        var gitState = await DescribeAsync(gitCopy);
        Assert.Equal(gitState.Text, (await DescribeAsync(appCopy)).Text);

        if (!pull.Succeeded && !gitState.Conflicts && !gitState.Rebasing)
        {
            Assert.True(failure is not null, $"git pull failed ({pull.StandardError.Trim()}), the app's pull did not.");
            Assert.Equal(
                GitFailureClassifier.Classify(pull.StandardError, pull.StandardOutput, pull.ExitCode),
                GitFailureClassifier.Classify(failure.StandardError, failure.StandardOutput, failure.ExitCode));
            return;
        }

        Assert.True(failure is null, $"The app's pull failed: {failure?.Message}");
        var expected = await ExpectedResultAsync(gitCopy, before, gitState);
        if (expected.Outcome == PullOutcome.Rebased || gitState.Rebasing)
        {
            expected = expected with { LeftOutByForkPoint = leftOutByForkPoint };
            Assert.Equal([before], backups);
        }
        else if (expected.Outcome is PullOutcome.FastForward or PullOutcome.Merged || gitState.Conflicts)
        {
            Assert.Empty(backups);
        }

        Assert.Equal(expected, result);
    }

    /// <summary>Runs git in <paramref name="repo"/> as the app's calls run: the test process's environment and the runner's defaults.</summary>
    public static Task<GitResult> RunGitAsync(TempRepo repo, params string[] arguments) =>
        NewRunner().RunAsync(new GitCommand(arguments) { WorkingDirectory = repo.Path }, TestCancelled);

    /// <summary>What the app should report, read from where git pull left <paramref name="repo"/>.</summary>
    private static async Task<PullResult> ExpectedResultAsync(TempRepo repo, string before, (string Text, string Head, bool Conflicts, bool Rebasing) state)
    {
        if (state.Conflicts || state.Rebasing)
        {
            return new PullResult(PullOutcome.Conflicts, 0);
        }

        if (state.Head == before)
        {
            return new PullResult(PullOutcome.UpToDate, 0);
        }

        // The commits from before to HEAD with their parents, newest first: their number, and
        // HEAD's parents.
        var commits = (await repo.GitAsync("rev-list", "--parents", $"{before}..{state.Head}")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var parents = commits[0].Trim().Split(' ');
        if (parents.Length > 2 && parents[1] == before)
        {
            return new PullResult(PullOutcome.Merged, commits.Length);
        }

        var fastForward = (await repo.GitAllowingFailureAsync("merge-base", "--is-ancestor", before, state.Head)).Succeeded;
        return new PullResult(fastForward ? PullOutcome.FastForward : PullOutcome.Rebased, commits.Length);
    }

    /// <summary>
    /// Where a pull left a repository: HEAD's commit and branch and the status with its conflicts
    /// (<c>git status --porcelain=v2 --branch</c>), every branch's commit, the working tree's
    /// differences, and what is in progress.
    /// </summary>
    private static async Task<(string Text, string Head, bool Conflicts, bool Rebasing)> DescribeAsync(TempRepo repo)
    {
        var status = (await repo.GitAsync("status", "--porcelain=v2", "--branch")).StandardOutput;
        var branches = (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)", "refs/heads", "refs/remotes")).StandardOutput;
        var diff = (await repo.GitAsync("diff")).StandardOutput;
        var gitFolder = Path.Combine(repo.Path, ".git");
        var inProgress = string.Join(' ', new[] { "MERGE_HEAD", "MERGE_MSG", "rebase-merge", "rebase-apply" }
            .Where(name => File.Exists(Path.Combine(gitFolder, name)) || Directory.Exists(Path.Combine(gitFolder, name))));
        var mergeMessage = File.Exists(Path.Combine(gitFolder, "MERGE_MSG")) ? await File.ReadAllTextAsync(Path.Combine(gitFolder, "MERGE_MSG"), TestCancelled) : string.Empty;

        var lines = status.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        const string OidPrefix = "# branch.oid ";
        var head = lines.Single(line => line.StartsWith(OidPrefix, StringComparison.Ordinal))[OidPrefix.Length..].Trim();
        var conflicts = lines.Any(line => line.StartsWith("u ", StringComparison.Ordinal));
        var rebasing = Directory.Exists(Path.Combine(gitFolder, "rebase-merge")) || Directory.Exists(Path.Combine(gitFolder, "rebase-apply"));
        return ($"status:\n{status}\nbranches:\n{branches}\ndiff:\n{diff}\nin progress: {inProgress}\n{mergeMessage}", head, conflicts, rebasing);
    }
}

/// <summary>
/// The repositories the pull parity tests copy twice per case: each built the first time a test
/// asks for it, once per test process, and deleted when the process ends.
/// </summary>
internal static class PullParityTemplates
{
    private static readonly Dictionary<PullState, Lazy<Task<TempRepo>>> Sync =
        Enum.GetValues<PullState>().ToDictionary(state => state, state => new Lazy<Task<TempRepo>>(() => KeepUntilExit(BuildSyncAsync(state))));

    private static readonly Dictionary<PullState, Lazy<Task<TempRepo>>> Local =
        new[] { PullState.FastForward, PullState.Diverged }.ToDictionary(state => state, state => new Lazy<Task<TempRepo>>(() => KeepUntilExit(BuildLocalAsync(state))));

    /// <summary>The remote sync scenario on main, origin at <c>../origin.git</c>, in <paramref name="state"/>.</summary>
    /// <remarks>Built once per test process; the wait for it does not count against the test in the hang watchdog.</remarks>
    public static Task<TempRepo> SyncAsync(PullState state) =>
        HangWatchdogAttribute.SharedPreparationAsync($"the pull parity template sync {state}", Sync[state].Value);

    /// <summary>The edge cases scenario on <c>loc</c>, whose upstream is the local main, in <paramref name="state"/> (fast-forward or diverged).</summary>
    /// <remarks>Built once per test process; the wait for it does not count against the test in the hang watchdog.</remarks>
    public static Task<TempRepo> LocalAsync(PullState state) =>
        HangWatchdogAttribute.SharedPreparationAsync($"the pull parity template local {state}", Local[state].Value);

    private static async Task<TempRepo> KeepUntilExit(Task<TempRepo> building)
    {
        var repo = await building;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => repo.Dispose();
        return repo;
    }

    private static async Task<TempRepo> BuildSyncAsync(PullState state)
    {
        var repo = await Scenarios.RemoteSyncAsync();
        try
        {
            await repo.GitAsync("remote", "set-url", "origin", "../origin.git");
            switch (state)
            {
                case PullState.FastForward:
                    await Scenarios.PushFromElsewhereAsync(repo, "main", "src/other.txt", "other\n", "Other work");
                    break;
                case PullState.Diverged:
                    await Scenarios.PushFromElsewhereAsync(repo, "main", "src/other.txt", "other\n", "Other work");
                    await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");
                    break;
                case PullState.LocalAhead:
                    await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");
                    break;
                case PullState.Same:
                    break;
                case PullState.Conflicting:
                    await Scenarios.PushFromElsewhereAsync(repo, "main", "src/shared.txt", "shared\nother\n", "Other change");
                    await repo.CommitFileAsync("src/shared.txt", "shared\nlocal\n", "Local change");
                    break;
                case PullState.DivergedWithMerge:
                    await Scenarios.PushFromElsewhereAsync(repo, "main", "src/other.txt", "other\n", "Other work");
                    await repo.MergeAsync("topic");
                    await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");
                    break;
            }

            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    private static async Task<TempRepo> BuildLocalAsync(PullState state)
    {
        var repo = await Scenarios.EdgeCasesAsync();
        try
        {
            // loc is at Initial commit, its upstream main one commit further.
            await repo.GitAsync("checkout", "--quiet", "loc");
            if (state == PullState.Diverged)
            {
                await repo.CommitFileAsync("src/loc.txt", "loc\n", "Loc work");
            }

            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }
}
