using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// How a rebase command ended (<see cref="GitRepository.ClassifyRebaseAsync"/>,
/// docs/phase-4-design.md 1.4), on rebases run by real git as a terminal would run them. The
/// assertions name git's classifying lines only, so that they hold on Git 2.36 and CI's newer git.
/// </summary>
public class RebaseClassificationTests
{
    [Fact]
    public async Task A_rebase_that_replays_the_branch_is_rebased_with_the_new_tip()
    {
        using var repo = await DivergedAsync(conflicting: false);
        var repository = await OpenAsync(repo);

        var result = await RebaseAsync(repo, repository, "rebase", "main");

        Assert.Equal(new RebaseResult(RebaseOutcome.Rebased, await RevAsync(repo, "HEAD"), 0), result);
        Assert.Equal(await RevAsync(repo, "main"), await RevAsync(repo, "HEAD~1"));
        Assert.False(repository.RebaseFolderExists());
    }

    [Fact]
    public async Task A_branch_that_already_contains_the_target_is_up_to_date()
    {
        using var repo = await DivergedAsync(conflicting: false);
        await repo.GitAsync("rebase", "main");
        var tip = await RevAsync(repo, "HEAD");
        var repository = await OpenAsync(repo);

        var result = await RebaseAsync(repo, repository, "rebase", "main");

        Assert.Equal(new RebaseResult(RebaseOutcome.UpToDate, null, 0), result);
        Assert.Equal(tip, await RevAsync(repo, "HEAD"));
    }

    [Theory]
    [InlineData("apply")]
    [InlineData("merge")]
    public async Task A_rebase_that_prints_a_subject_saying_is_up_to_date_is_still_rebased(string backend)
    {
        // The review's finding 11: the apply backend prints "Applying: <subject>", the merge
        // backend "dropping <sha> <subject> -- patch contents already upstream" for a commit that
        // becomes empty; neither is git's own "is up to date" line.
        using var repo = await TempRepo.CreateAsync();
        repo.WriteFile("README.md", "readme\n").WriteFile("f.txt", "base\n");
        await repo.CommitAsync("Initial commit");
        await repo.CreateBranchAsync("topic");
        repo.WriteFile("f.txt", "cached\n").WriteFile("main.txt", "main\n");
        await repo.CommitAsync("Main caches and works");
        await repo.CheckoutAsync("topic");
        await repo.CommitFileAsync("f.txt", "cached\n", "Make sure the cache is up to date");
        await repo.CommitFileAsync("topic.txt", "topic\n", "Check the index is up to date");
        var oldTip = await RevAsync(repo, "HEAD");
        var repository = await OpenAsync(repo);

        var result = await RebaseAsync(repo, repository, "-c", $"rebase.backend={backend}", "rebase", "main");

        Assert.NotEqual(oldTip, await RevAsync(repo, "HEAD"));
        Assert.Equal(new RebaseResult(RebaseOutcome.Rebased, await RevAsync(repo, "HEAD"), 0), result);
        Assert.Equal(await RevAsync(repo, "main"), await RevAsync(repo, "HEAD~1"));
    }

    [Fact]
    public async Task A_detached_head_that_already_contains_the_target_is_up_to_date()
    {
        using var repo = await DivergedAsync(conflicting: false);
        await repo.GitAsync("rebase", "main");
        await repo.GitAsync("checkout", "--detach");
        var repository = await OpenAsync(repo);

        Assert.Equal(new RebaseResult(RebaseOutcome.UpToDate, null, 0), await RebaseAsync(repo, repository, "rebase", "main"));
    }

    [Fact]
    public async Task A_conflict_stops_the_rebase_with_its_conflicted_files_and_continue_finishes_it()
    {
        using var repo = await DivergedAsync(conflicting: true);
        var repository = await OpenAsync(repo);

        var stopped = await RebaseAsync(repo, repository, "rebase", "main");

        Assert.Equal(new RebaseResult(RebaseOutcome.Stopped, null, 1), stopped);
        Assert.True(repository.RebaseFolderExists());

        // Continue with the file still unresolved: git refuses ("needs merge") and nothing changes.
        var refused = await Assert.ThrowsAsync<GitException>(() => RebaseAsync(repo, repository, "rebase", "--continue"));
        Assert.Contains("You must edit all merge conflicts", refused.StandardOutput + refused.StandardError, StringComparison.Ordinal);
        Assert.True(repository.RebaseFolderExists());

        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");
        var continued = await RebaseAsync(repo, repository, "rebase", "--continue");

        Assert.Equal(new RebaseResult(RebaseOutcome.Rebased, await RevAsync(repo, "HEAD"), 0), continued);
        Assert.Equal("resolved\n", ReadText(repo, "f.txt"));
    }

    [Fact]
    public async Task An_untracked_file_in_the_way_of_a_later_pick_stops_the_rebase_without_a_conflict()
    {
        // The topic adds in.txt and deletes it again, so its tip has no in.txt: an untracked
        // in.txt is not in the way of the checkout of main, only of the first pick (0.1).
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("README.md", "readme\n", "Initial commit");
        await repo.CreateBranchAsync("topic");
        await repo.CommitFileAsync("main.txt", "main\n", "Main work");
        await repo.CheckoutAsync("topic");
        await repo.CommitFileAsync("in.txt", "tracked\n", "Add in");
        repo.DeleteFile("in.txt");
        await repo.CommitAsync("Remove in");
        repo.WriteFile("in.txt", "untracked\n");
        var repository = await OpenAsync(repo);

        var result = await RebaseAsync(repo, repository, "rebase", "main");

        Assert.Equal(new RebaseResult(RebaseOutcome.Stopped, null, 0), result);
        Assert.True(repository.RebaseFolderExists());
        Assert.Equal("untracked\n", ReadText(repo, "in.txt"));
    }

    [Fact]
    public async Task A_rebase_git_refuses_for_a_changed_tracked_file_throws_and_changes_nothing()
    {
        using var repo = await DivergedAsync(conflicting: false);
        var tip = await RevAsync(repo, "HEAD");
        repo.WriteFile("README.md", "changed\n");
        var repository = await OpenAsync(repo);

        var refused = await Assert.ThrowsAsync<GitException>(() => RebaseAsync(repo, repository, "rebase", "main"));

        Assert.Contains("cannot rebase", refused.StandardError, StringComparison.Ordinal);
        Assert.Equal(1, refused.ExitCode);
        Assert.False(repository.RebaseFolderExists());
        Assert.Equal(tip, await RevAsync(repo, "HEAD"));
        Assert.Equal("changed\n", ReadText(repo, "README.md"));
    }

    [Fact]
    public async Task A_second_rebase_while_one_is_stopped_is_a_refusal_not_a_stop()
    {
        using var repo = await DivergedAsync(conflicting: true);
        var repository = await OpenAsync(repo);
        await RebaseAsync(repo, repository, "rebase", "main");

        // The folder was there before the call: git's fatal refusal is not this call's stop.
        var refused = await Assert.ThrowsAsync<GitException>(() => RebaseAsync(repo, repository, "rebase", "main"));

        Assert.Equal(128, refused.ExitCode);
        Assert.True(repository.RebaseFolderExists());
    }

    /// <summary>
    /// <c>main</c> and <c>topic</c> diverged from an initial commit, HEAD on <c>topic</c>: each
    /// changes <c>f.txt</c> (so that rebasing <c>topic</c> conflicts) or a file of its own.
    /// </summary>
    private static async Task<TempRepo> DivergedAsync(bool conflicting)
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            repo.WriteFile("README.md", "readme\n").WriteFile("f.txt", "base\n");
            await repo.CommitAsync("Initial commit");
            await repo.CreateBranchAsync("topic");
            await repo.CommitFileAsync(conflicting ? "f.txt" : "main.txt", "main\n", "Main work");
            await repo.CheckoutAsync("topic");
            await repo.CommitFileAsync(conflicting ? "f.txt" : "topic.txt", "topic\n", "Topic work");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>Runs a rebase command as a terminal would and classifies it, with the rebase folder read before the call.</summary>
    private static async Task<RebaseResult> RebaseAsync(TempRepo repo, GitRepository repository, params string[] arguments)
    {
        var before = repository.RebaseFolderExists();
        var result = await repo.GitAllowingFailureAsync(arguments);
        return await repository.ClassifyRebaseAsync(new GitCommand(arguments), result, before, TestCancelled);
    }
}
