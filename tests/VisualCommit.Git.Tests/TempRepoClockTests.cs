using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="TempRepo.MoveClockTo"/> (D111): a step made through <see cref="TempRepo"/> after an
/// operation of the app under test, whose ref updates are dated
/// <see cref="GitIsolation.AppCommitDate"/>, can be made to look newer in the reflog, which git's
/// <c>--force-if-includes</c> depends on.
/// </summary>
public class TempRepoClockTests
{
    [Fact]
    public async Task The_clock_moves_forward_for_the_repository_and_the_ones_next_to_it()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var other = await sync.CloneSiblingAsync("other", sync.SiblingPath("origin.git"));
        var later = GitIsolation.AppCommitDate.AddMinutes(1);

        Assert.Same(sync, sync.MoveClockTo(later));

        Assert.Equal(later, sync.ClockTime);
        Assert.Equal(later, other.ClockTime);
        await other.GitAsync("checkout", "--quiet", "-B", "main", "refs/remotes/origin/main");
        await other.CommitFileAsync("src/other.txt", "other\n", "Other work");
        (DateTimeOffset, TimeSpan)[] authorAndCommitter = [(later, TimeSpan.Zero), (later, TimeSpan.Zero)];
        Assert.Equal(
            authorAndCommitter,
            GitIsoDates.ParseLines((await other.GitAsync("log", "-1", "--format=%aI%n%cI")).StandardOutput).Select(date => (date, date.Offset)));

        // A minute per commit from there, for both.
        Assert.Equal(later.AddMinutes(1), sync.ClockTime);
    }

    [Fact]
    public async Task The_clock_never_goes_back()
    {
        using var repo = await TempRepo.CreateAsync();
        var start = repo.ClockTime;

        Assert.Throws<ArgumentOutOfRangeException>(() => repo.MoveClockTo(start.AddSeconds(-1)));
        Assert.Equal(start, repo.ClockTime);

        // Staying where it is is allowed.
        repo.MoveClockTo(start);
        Assert.Equal(start, repo.ClockTime);
    }

    // A test per case, each on a scenario of its own (D237): the two in one test took 25 s in the
    // project's own run.
    [Fact]
    public async Task A_step_after_an_app_operation_counts_for_force_if_includes_once_the_clock_has_moved_past_it()
    {
        Assert.True(await ForceIfIncludesAllowsAsync(moveClock: true), "The force push was refused although the reset came after the fetch.");
    }

    [Fact]
    public async Task A_step_after_an_app_operation_does_not_count_for_force_if_includes_before_the_clock_has_moved_past_it()
    {
        Assert.False(await ForceIfIncludesAllowsAsync(moveClock: false), "The force push went through although git saw the reset as older than the fetch.");
    }

    /// <summary>
    /// The order D111 is about: someone else pushes to origin; the app fetches it (its ref update
    /// dated 2026-01-02 09:00); the user, through a terminal, looks at origin's work (resets main
    /// to it) and goes back to their own commit; then force-pushes with a lease and
    /// <c>--force-if-includes</c>. Git allows it only if the reset is newer in the reflog than the
    /// fetch, which the test clock gives only after <see cref="TempRepo.MoveClockTo"/>.
    /// </summary>
    private static async Task<bool> ForceIfIncludesAllowsAsync(bool moveClock)
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(sync, "main", "src/elsewhere.txt", "elsewhere\n", "Work from elsewhere");
        var local = await sync.CommitFileAsync("src/local.txt", "local\n", "Local work");

        await AppFetchAsync(sync);

        if (moveClock)
        {
            sync.MoveClockTo(GitIsolation.AppCommitDate.AddMinutes(1));
        }

        await sync.GitAsync("reset", "--quiet", "--hard", "refs/remotes/origin/main");
        await sync.GitAsync("reset", "--quiet", "--hard", local);
        var push = await sync.GitAllowingFailureAsync("push", "--porcelain", "--force-with-lease", "--force-if-includes", "origin", "refs/heads/main:refs/heads/main");

        var originMain = (await sync.GitAsync("--git-dir", sync.SiblingPath("origin.git"), "rev-parse", "refs/heads/main")).StandardOutput.Trim();
        Assert.Equal(push.Succeeded, originMain == local);
        return push.Succeeded;
    }

    /// <summary>A fetch as the app under test makes it: its ref updates dated <see cref="GitIsolation.AppCommitDate"/> (D72).</summary>
    private static async Task AppFetchAsync(TempRepo repo)
    {
        var date = GitIsolation.AppCommitDate.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        var environment = new Dictionary<string, string?>(repo.Environment)
        {
            ["GIT_AUTHOR_DATE"] = date,
            ["GIT_COMMITTER_DATE"] = date,
        };
        var command = new GitCommand("fetch", "--quiet", "origin") { WorkingDirectory = repo.Path, Environment = environment };
        (await repo.Runner.RunAsync(command, TestCancelled)).EnsureSuccess(command);
    }
}
