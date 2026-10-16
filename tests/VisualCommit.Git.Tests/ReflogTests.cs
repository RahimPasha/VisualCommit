using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitRepository.ReadReflogAsync"/> and <see cref="GitRepository.FindLostCommitsAsync"/>
/// (docs/phase-5-design.md 5.2, 0.2, D180): one reflog read through git with two walks zipped by
/// position, its gaps counted, a missing reflog empty; and which commits nothing reaches.
/// </summary>
public class ReflogTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Each entry as (selector, number, commit, message, subject, time), for comparison.</summary>
    private static List<(string Selector, int Index, string Sha, string Message, string Subject, DateTimeOffset Time)> Shape(ReflogResult result) =>
        [.. result.Entries.Select(entry => (entry.Selector, entry.Index, entry.Sha, entry.Message, entry.Subject, entry.Time))];

    [Fact]
    public async Task Heads_reflog_lists_every_move_newest_first_with_each_entrys_own_time()
    {
        using var repo = await TempRepo.CreateAsync("reflog");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        var second = await repo.CommitFileAsync("f.txt", "2\n", "Second");
        await repo.CreateBranchAsync("side");
        await repo.CheckoutAsync("side");
        var third = await repo.CommitFileAsync("f.txt", "3\n", "Third: with a colon");
        await repo.CheckoutAsync("main");
        await repo.GitAsync("reset", "--hard", first);
        var repository = await OpenAsync(repo);

        var result = await repository.ReadReflogAsync("HEAD", 500, TestCancelled);

        // TempRepo's clock gives each call the time of the next commit: the entries carry their own.
        Assert.Equal(
            [
                ("HEAD@{0}", 0, first, $"reset: moving to {first}", "First", Start.AddMinutes(3)),
                ("HEAD@{1}", 1, second, "checkout: moving from side to main", "Second", Start.AddMinutes(3)),
                ("HEAD@{2}", 2, third, "commit: Third: with a colon", "Third: with a colon", Start.AddMinutes(2)),
                ("HEAD@{3}", 3, second, "checkout: moving from main to side", "Second", Start.AddMinutes(2)),
                ("HEAD@{4}", 4, second, "commit: Second", "Second", Start.AddMinutes(1)),
                ("HEAD@{5}", 5, first, "commit (initial): First", "First", Start),
            ],
            Shape(result));
        Assert.Equal(0, result.Skipped);
        Assert.Equal([second], result.Entries[2].Parents);
        Assert.Empty(result.Entries[5].Parents);
    }

    [Fact]
    public async Task A_branchs_reflog_has_its_full_name_in_the_selectors_and_the_limit_bounds_it()
    {
        using var repo = await TempRepo.CreateAsync("reflog-branch");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        var second = await repo.CommitFileAsync("f.txt", "2\n", "Second");
        var third = await repo.CommitFileAsync("f.txt", "3\n", "Third");
        var repository = await OpenAsync(repo);

        var all = await repository.ReadReflogAsync("refs/heads/main", 500, TestCancelled);
        Assert.Equal(["refs/heads/main@{0}", "refs/heads/main@{1}", "refs/heads/main@{2}"], all.Entries.Select(entry => entry.Selector));
        Assert.Equal([third, second, first], all.Entries.Select(entry => entry.Sha));

        var two = await repository.ReadReflogAsync("refs/heads/main", 2, TestCancelled);
        Assert.Equal([third, second], two.Entries.Select(entry => entry.Sha));

        // One entry past the limit tells that there are more (15.9); exactly the limit does not.
        Assert.Equal((true, false, false), (two.More, all.More, (await repository.ReadReflogAsync("refs/heads/main", 3, TestCancelled)).More));
    }

    [Fact]
    public async Task Renaming_the_checked_out_branch_leaves_a_gap_that_is_counted()
    {
        using var repo = await TempRepo.CreateAsync("reflog-rename");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        await repo.GitAsync("branch", "--move", "main", "trunk");
        var repository = await OpenAsync(repo);

        // The rename writes two entries into HEAD's reflog; the one whose new value is null is skipped.
        var head = await repository.ReadReflogAsync("HEAD", 500, TestCancelled);
        Assert.Equal([0, 2], head.Entries.Select(entry => entry.Index));
        Assert.Equal("Branch: renamed refs/heads/main to refs/heads/trunk", head.Entries[0].Message);
        Assert.Equal(1, head.Skipped);

        // The branch's own reflog moved with it; the old name has none.
        var trunk = await repository.ReadReflogAsync("refs/heads/trunk", 500, TestCancelled);
        Assert.Equal(first, trunk.Entries[^1].Sha);
        Assert.Same(ReflogResult.Empty, await repository.ReadReflogAsync("refs/heads/main", 500, TestCancelled));
    }

    [Fact]
    public async Task An_entry_whose_commit_no_longer_exists_is_skipped_and_counted()
    {
        using var repo = await TempRepo.CreateAsync("reflog-missing");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        var gone = await repo.CommitFileAsync("f.txt", "2\n", "Gone soon");
        await repo.GitAsync("reset", "--hard", first);
        var objectFile = Path.Combine(repo.Path, ".git", "objects", gone[..2], gone[2..]);
        File.SetAttributes(objectFile, FileAttributes.Normal);
        File.Delete(objectFile);
        var repository = await OpenAsync(repo);

        var result = await repository.ReadReflogAsync("HEAD", 500, TestCancelled);

        Assert.Equal([(0, first), (2, first)], result.Entries.Select(entry => (entry.Index, entry.Sha)));
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public async Task A_ref_without_a_reflog_is_empty()
    {
        using var unborn = await TempRepo.CreateAsync("reflog-unborn");
        Assert.Same(ReflogResult.Empty, await (await OpenAsync(unborn)).ReadReflogAsync("HEAD", 500, TestCancelled));

        using var repo = await TempRepo.CreateAsync("reflog-none");
        await repo.CommitFileAsync("f.txt", "1\n", "First");
        await repo.CreateBranchAsync("deleted");
        await repo.GitAsync("branch", "--delete", "--force", "deleted");
        await repo.TagAsync("v1");
        var repository = await OpenAsync(repo);

        Assert.Same(ReflogResult.Empty, await repository.ReadReflogAsync("refs/heads/deleted", 500, TestCancelled));
        Assert.Same(ReflogResult.Empty, await repository.ReadReflogAsync("refs/tags/v1", 500, TestCancelled));
        Assert.Same(ReflogResult.Empty, await repository.ReadReflogAsync("refs/stash", 500, TestCancelled));
    }

    [Fact]
    public async Task A_remote_branchs_reflog_holds_the_push()
    {
        using var repo = await TempRepo.CreateAsync("reflog-remote");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        await repo.AddBareRemoteAsync();
        await repo.PushAsync("origin", true, "main");
        var repository = await OpenAsync(repo);

        var entry = Assert.Single((await repository.ReadReflogAsync("refs/remotes/origin/main", 500, TestCancelled)).Entries);

        Assert.Equal(("refs/remotes/origin/main@{0}", first, "update by push"), (entry.Selector, entry.Sha, entry.Message));
    }

    [Fact]
    public async Task Walks_that_disagree_are_read_once_more()
    {
        using var repo = await TempRepo.CreateAsync("reflog-race");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        var second = await repo.CommitFileAsync("f.txt", "2\n", "Second");

        // The first walk of the times sees one entry more, as if a commit came between the walks.
        var runner = new AlteringRunner(NewRunner(), output => $"HEAD@{{1767268920 +0000}}\u001f{new string('9', 40)}\0" + output, times: 1);
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestCancelled);

        var result = await repository.ReadReflogAsync("HEAD", 500, TestCancelled);

        Assert.Equal([second, first], result.Entries.Select(entry => entry.Sha));
        Assert.Equal(4, runner.Walks);
    }

    [Fact]
    public async Task Walks_that_keep_disagreeing_throw()
    {
        using var repo = await TempRepo.CreateAsync("reflog-race-again");
        await repo.CommitFileAsync("f.txt", "1\n", "First");
        // Every walk of the times names another commit than the walk of the entries.
        static string OtherCommit(string output)
        {
            var sha = output.IndexOf('\u001f', StringComparison.Ordinal) + 1;
            return output[..sha] + new string('9', 40) + output[(sha + 40)..];
        }

        var runner = new AlteringRunner(NewRunner(), OtherCommit, times: int.MaxValue);
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestCancelled);

        var ex = await Assert.ThrowsAsync<GitException>(() => repository.ReadReflogAsync("HEAD", 500, TestCancelled));

        Assert.Contains("changed while it was being read", ex.Message, StringComparison.Ordinal);
        Assert.Equal(4, runner.Walks);
    }

    [Fact]
    public void Entries_are_zipped_by_position_and_an_entry_that_cannot_be_read_is_left_out()
    {
        var a = new string('a', 40);
        var b = new string('b', 40);
        var entries = $"HEAD@{{0}}\u001f{a}\u001f{b}\u001fcommit: A\u001fA\0HEAD@{{x}}\u001f{b}\u001f\u001fcommit: B\u001fB\0HEAD@{{3}}\u001f{b}\u001f\u001fcommit (initial): B\u001fB\0";
        var times = $"HEAD@{{1767268800 -0130}}\u001f{a}\0HEAD@{{1767268800 +0000}}\u001f{b}\0HEAD@{{1767268740 +0000}}\u001f{b}\0";
        var log = new ListLog();

        var result = GitRepository.ParseReflog(entries, times, log);

        Assert.NotNull(result);
        Assert.Equal([(0, a), (3, b)], result.Entries.Select(entry => (entry.Index, entry.Sha)));
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 30, 0, new TimeSpan(-1, -30, 0)), result.Entries[0].Time);
        Assert.Equal(new TimeSpan(-1, -30, 0), result.Entries[0].Time.Offset);

        // Numbers 1 and 2 are missing: the unreadable one is counted with git's gap.
        Assert.Equal(2, result.Skipped);
        Assert.Single(log.Entries);
    }

    [Fact]
    public void Walks_with_different_commits_or_lengths_do_not_zip()
    {
        var a = new string('a', 40);
        var b = new string('b', 40);
        var entries = $"HEAD@{{0}}\u001f{a}\u001f\u001fm\u001fs\0";

        Assert.Null(GitRepository.ParseReflog(entries, $"HEAD@{{1 +0000}}\u001f{b}\0", new ListLog()));
        Assert.Null(GitRepository.ParseReflog(entries, string.Empty, new ListLog()));
        Assert.Same(ReflogResult.Empty, GitRepository.ParseReflog(string.Empty, string.Empty, new ListLog()));
    }

    [Fact]
    public async Task Lost_commits_are_those_no_branch_tag_remote_branch_or_head_reaches()
    {
        using var repo = await TempRepo.CreateAsync("lost");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        var second = await repo.CommitFileAsync("f.txt", "2\n", "Second");
        var third = await repo.CommitFileAsync("f.txt", "3\n", "Third");
        await repo.TagAsync("kept", second);
        await repo.GitAsync("reset", "--hard", first);

        // A file named HEAD would make HEAD ambiguous without the "--" after it.
        repo.WriteFile("HEAD", "a file\n");
        var repository = await OpenAsync(repo);

        var lost = await repository.FindLostCommitsAsync([first, second, third, new string('1', 40)], TestCancelled);

        Assert.Equal([third], lost);
    }

    [Fact]
    public async Task Nothing_asked_is_nothing_lost_and_a_name_is_no_commit_id()
    {
        using var repo = await TempRepo.CreateAsync("lost-arguments");
        await repo.CommitFileAsync("f.txt", "1\n", "First");
        var repository = await OpenAsync(repo);

        Assert.Empty(await repository.FindLostCommitsAsync([], TestCancelled));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.FindLostCommitsAsync(["--all"], TestCancelled));
    }

    [Fact]
    public async Task With_head_unborn_every_commit_no_ref_reaches_is_lost()
    {
        using var repo = await TempRepo.CreateAsync("lost-unborn");
        var first = await repo.CommitFileAsync("f.txt", "1\n", "First");
        await repo.GitAsync("checkout", "--quiet", "--orphan", "fresh");
        await repo.GitAsync("branch", "--delete", "--force", "main");
        var repository = await OpenAsync(repo);

        Assert.Equal([first], await repository.FindLostCommitsAsync([first], TestCancelled));
    }

    /// <summary>
    /// A runner that changes the output of the reflog's second walk (<c>--date=raw</c>) the first
    /// <c>times</c> times, as if the reflog moved between the two walks, and counts the walks.
    /// </summary>
    private sealed class AlteringRunner(IGitRunner inner, Func<string, string> alter, int times) : IGitRunner
    {
        private int _altered;
        private int _walks;

        public string ExecutablePath => inner.ExecutablePath;

        public int Walks => _walks;

        public async Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
        {
            var result = await inner.RunAsync(command, cancellationToken);
            if (!command.Arguments.Contains("--walk-reflogs"))
            {
                return result;
            }

            Interlocked.Increment(ref _walks);
            return command.Arguments.Contains("--date=raw") && Interlocked.Increment(ref _altered) <= times
                ? result with { StandardOutput = alter(result.StandardOutput) }
                : result;
        }
    }
}
