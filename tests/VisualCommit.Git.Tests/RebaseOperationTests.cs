using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RebaseTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// "Rebase {cur} onto …" in the git layer (<see cref="GitRepository.RebaseAsync"/>,
/// docs/phase-4-design.md 1.3, 1.4, D142) with real git: onto a branch's tip and onto a commit,
/// up to date, the stops with and without a conflicted file, git's refusal of a dirty tree, an
/// autostash whose changes conflict when put back, and why the app never passes git a branch.
/// Git's lines are asserted by their classifying part only, the same on Git 2.36 and CI's git.
/// </summary>
public class RebaseOperationTests
{
    [Fact]
    public async Task Onto_a_branch_the_branch_is_replayed_on_its_tip_and_stays_checked_out()
    {
        using var repo = await DivergedAsync(conflicting: false);
        var oldTip = await repo.HeadAsync();
        var main = await RevAsync(repo, "main");
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);

        var result = await repository.RebaseAsync(main, cancellationToken: TestCancelled);

        Assert.Equal(new RebaseResult(RebaseOutcome.Rebased, await repo.HeadAsync(), 0), result);
        Assert.Equal("refs/heads/topic", await HeadBranchAsync(repo));
        Assert.Equal(main, await RevAsync(repo, "HEAD~1"));
        Assert.Equal(["Topic work"], await SubjectsAsync(repo, "main..HEAD"));
        Assert.Equal(main, await RevAsync(repo, "main"));
        Assert.Equal(oldTip, await RevAsync(repo, "ORIG_HEAD"));
        Assert.False(RebaseInProgress(repo));

        // The one write: git rebase <sha>, without a branch argument or any option.
        Assert.Equal([$"git rebase {main}"], calls.Snapshot().Select(record => record.CommandText).Where(text => text.StartsWith("git rebase", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Onto_a_commit_that_is_no_branch_tip_the_branch_is_replayed_on_that_commit()
    {
        using var repo = await DivergedAsync(conflicting: false);
        await repo.CheckoutAsync("main");
        await repo.CommitFileAsync("later.txt", "later\n", "Later main work");
        await repo.CheckoutAsync("topic");
        var target = await RevAsync(repo, "main~1");
        var repository = await OpenAsync(repo);

        var result = await repository.RebaseAsync(target, cancellationToken: TestCancelled);

        Assert.Equal(RebaseOutcome.Rebased, result.Outcome);
        Assert.Equal(target, await RevAsync(repo, "HEAD~1"));
        Assert.Equal("refs/heads/topic", await HeadBranchAsync(repo));
        Assert.False(File.Exists(Path.Combine(repo.Path, "later.txt")));
    }

    [Fact]
    public async Task A_branch_that_already_contains_the_target_is_up_to_date_and_nothing_is_written()
    {
        using var repo = await DivergedAsync(conflicting: false);
        var tip = await repo.HeadAsync();
        var initial = await RevAsync(repo, "HEAD~1");
        var repository = await OpenAsync(repo);

        var result = await repository.RebaseAsync(initial, cancellationToken: TestCancelled);

        Assert.Equal(new RebaseResult(RebaseOutcome.UpToDate, null, 0), result);
        Assert.Equal(tip, await repo.HeadAsync());
        Assert.Equal("refs/heads/topic", await HeadBranchAsync(repo));
    }

    [Fact]
    public async Task A_conflict_stops_the_rebase_with_its_conflicted_file()
    {
        using var repo = await DivergedAsync(conflicting: true);
        var tip = await repo.HeadAsync();
        var repository = await OpenAsync(repo);

        var result = await repository.RebaseAsync(await RevAsync(repo, "main"), cancellationToken: TestCancelled);

        Assert.Equal(new RebaseResult(RebaseOutcome.Stopped, null, 1), result);
        Assert.True(RebaseInProgress(repo));
        Assert.Null(await HeadBranchAsync(repo));
        Assert.Equal(["UU f.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal("refs/heads/topic", ReadGitFile(repo, "rebase-merge/head-name").Trim());

        await repo.GitAsync("rebase", "--abort");
        Assert.Equal(tip, await repo.HeadAsync());
        Assert.Equal("refs/heads/topic", await HeadBranchAsync(repo));
    }

    [Fact]
    public async Task An_untracked_file_in_the_way_of_a_later_pick_stops_the_rebase_without_a_conflicted_file()
    {
        // The topic adds in.txt and removes it again, so the checkout of the target is not in the
        // way of the untracked in.txt; the first pick is, and git reschedules it (0.1).
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

        var result = await repository.RebaseAsync(await RevAsync(repo, "main"), cancellationToken: TestCancelled);

        Assert.Equal(new RebaseResult(RebaseOutcome.Stopped, null, 0), result);
        Assert.True(RebaseInProgress(repo));
        Assert.Equal("untracked\n", ReadText(repo, "in.txt"));
        Assert.StartsWith("pick ", ReadGitFile(repo, "rebase-merge/git-rebase-todo"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Git_refuses_a_tree_with_changes_to_tracked_files_and_nothing_changes(bool staged)
    {
        using var repo = await DivergedAsync(conflicting: false);
        var tip = await repo.HeadAsync();
        repo.WriteFile("README.md", "changed\n");
        if (staged)
        {
            await repo.GitAsync("add", "README.md");
        }

        var repository = await OpenAsync(repo);
        var main = await RevAsync(repo, "main");

        var refused = await Assert.ThrowsAsync<GitException>(() => repository.RebaseAsync(main, cancellationToken: TestCancelled));

        Assert.Contains("error: cannot rebase:", refused.StandardError, StringComparison.Ordinal);
        Assert.Equal(1, refused.ExitCode);
        Assert.False(RebaseInProgress(repo));
        Assert.Equal(tip, await repo.HeadAsync());
        Assert.Equal("changed\n", ReadText(repo, "README.md"));
        if (await GitIs236Async(repo))
        {
            Assert.Contains(
                staged ? "error: cannot rebase: Your index contains uncommitted changes." : "error: cannot rebase: You have unstaged changes.",
                refused.StandardError,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task An_autostash_whose_changes_conflict_when_put_back_is_rebased_with_the_conflicts_and_the_stash_kept()
    {
        // main changes f.txt; the topic does not; the user's unstaged change of f.txt is stashed by
        // git (rebase.autoStash), the rebase succeeds, and putting the change back conflicts (1.4).
        using var repo = await DivergedAsync(conflicting: false);
        await repo.CheckoutAsync("main");
        await repo.CommitFileAsync("f.txt", "main line\n", "Main changes f");
        await repo.CheckoutAsync("topic");
        await repo.ConfigAsync("rebase.autoStash", "true");
        repo.WriteFile("f.txt", "my line\n");
        var repository = await OpenAsync(repo);

        var result = await repository.RebaseAsync(await RevAsync(repo, "main"), cancellationToken: TestCancelled);

        Assert.Equal(new RebaseResult(RebaseOutcome.Rebased, await repo.HeadAsync(), 1) { AutostashConflicted = true }, result);
        Assert.Equal(await RevAsync(repo, "main"), await RevAsync(repo, "HEAD~1"));
        Assert.False(RebaseInProgress(repo));
        Assert.Equal(["UU f.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Single(await repo.StashListAsync());
    }

    [Fact]
    public async Task Git_detaches_HEAD_for_a_branch_given_as_a_full_ref_name_which_is_why_the_app_never_passes_one()
    {
        // Pins git's behaviour (0.1): `git rebase main refs/heads/topic` rebases a detached HEAD
        // and leaves topic where it was. RebaseAsync passes only the target (the first test).
        using var repo = await DivergedAsync(conflicting: false);
        var topic = await RevAsync(repo, "topic");

        var result = await repo.GitAsync("rebase", "main", "refs/heads/topic");

        Assert.Contains("detached HEAD", result.StandardOutput + result.StandardError, StringComparison.Ordinal);
        Assert.Null(await HeadBranchAsync(repo));
        Assert.Equal(topic, await RevAsync(repo, "topic"));
        Assert.Equal(await RevAsync(repo, "main"), await RevAsync(repo, "HEAD~1"));
    }

    [Fact]
    public async Task A_rebase_while_one_is_stopped_is_refused_by_git()
    {
        using var repo = await DivergedAsync(conflicting: true);
        var repository = await OpenAsync(repo);
        var main = await RevAsync(repo, "main");
        await repository.RebaseAsync(main, cancellationToken: TestCancelled);

        var refused = await Assert.ThrowsAsync<GitException>(() => repository.RebaseAsync(main, cancellationToken: TestCancelled));

        Assert.Equal(128, refused.ExitCode);
        Assert.True(RebaseInProgress(repo));
    }

    [Fact]
    public void The_terminals_erase_line_is_removed_from_both_streams()
    {
        var result = new GitResult(1, "out\u001b[K", "Rebasing (1/1)\r\r\u001b[KSuccessfully rebased and updated refs/heads/topic.\n", TimeSpan.Zero);

        var cleaned = GitRepository.RebaseText.WithoutEraseLine(result);

        Assert.Equal(result with { StandardOutput = "out", StandardError = "Rebasing (1/1)\r\rSuccessfully rebased and updated refs/heads/topic.\n" }, cleaned);
        Assert.Same(cleaned, GitRepository.RebaseText.WithoutEraseLine(cleaned));
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
}
