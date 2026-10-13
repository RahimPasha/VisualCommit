using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.InteractiveRebaseScenarios;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RebaseTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// What an interactive rebase refuses (docs/phase-4-design.md 2.4, 2.5): Start when HEAD is no
/// longer on the listed branch at its listed tip, before anything runs; a todo of the app's that
/// git refuses (an invalid line, <c>rebase.missingCommitsCheck</c>), which is aborted, logged and
/// thrown. And what it leaves alone: other branches, also with <c>rebase.updateRefs</c>.
/// </summary>
public class InteractiveRebaseRefusalTests
{
    [Fact]
    public async Task Start_is_refused_when_the_branch_moved_since_the_list_was_read()
    {
        using var repo = await LineAsync();
        var (repository, list) = await OpenListAsync(repo);
        var plan = Plan(list, Topic, Pick, Drop, Pick, Pick);
        var moved = await repo.CommitFileAsync("five.txt", "five\n", "Add five");

        var refused = await Assert.ThrowsAsync<GitException>(() => repository.InteractiveRebaseAsync(plan, cancellationToken: TestCancelled));

        Assert.Equal(GitRepository.RebaseHeadMovedMessage, refused.StandardError);
        Assert.Equal(moved, await repo.HeadAsync());
        Assert.False(RebaseInProgress(repo));
    }

    [Fact]
    public async Task Start_is_refused_when_HEAD_is_on_another_branch()
    {
        using var repo = await LineAsync();
        var (repository, list) = await OpenListAsync(repo);
        await repo.CheckoutAsync("other");
        var other = await repo.HeadAsync();

        var refused = await Assert.ThrowsAsync<GitException>(() => repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Drop, Pick, Pick), cancellationToken: TestCancelled));

        Assert.Equal(GitRepository.RebaseHeadMovedMessage, refused.StandardError);
        Assert.Equal(other, await repo.HeadAsync());
        Assert.Equal(list.BranchTip, await RevAsync(repo, "topic"));
    }

    [Fact]
    public async Task A_todo_git_finds_invalid_is_aborted_logged_and_thrown()
    {
        using var repo = await LineAsync();
        var log = new ListLog();
        var repository = await GitRepository.OpenAsync(NewRunner(log: log), repo.Path, TestCancelled, log);
        var list = await repository.ReadRebaseListAsync(await RevAsync(repo, "main"), Topic, fromCommit: true, cancellationToken: TestCancelled);
        var plan = Plan(list, Topic, Pick, Drop, Pick, Pick);
        plan = plan with
        {
            StepsOldestFirst = [.. plan.StepsOldestFirst.Select((step, index) => index == 2 ? step with { Sha = "0123456789012345678901234567890123456789" } : step)],
        };

        var refused = await Assert.ThrowsAsync<GitException>(() => repository.InteractiveRebaseAsync(plan, cancellationToken: TestCancelled));

        Assert.Contains("error: invalid line", refused.StandardError, StringComparison.Ordinal);
        Assert.Equal(1, refused.ExitCode);
        Assert.False(RebaseInProgress(repo));
        Assert.Equal(Topic, await HeadBranchAsync(repo));
        Assert.Equal(list.BranchTip, await repo.HeadAsync());
        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("own todo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_todo_that_rebase_missingCommitsCheck_refuses_is_aborted_and_thrown()
    {
        // The app's todo names every listed commit (dropped ones with drop lines); a plan that
        // leaves one out is the app's fault, and git's check catches it.
        using var repo = await LineAsync();
        await repo.ConfigAsync("rebase.missingCommitsCheck", "error");
        var (repository, list) = await OpenListAsync(repo);
        var plan = Plan(list, Topic, Pick, Pick, Pick, Pick);
        plan = plan with { StepsOldestFirst = [.. plan.StepsOldestFirst.Where((_, index) => index != 2)] };

        var refused = await Assert.ThrowsAsync<GitException>(() => repository.InteractiveRebaseAsync(plan, cancellationToken: TestCancelled));

        Assert.Contains("may have been dropped accidentally", refused.StandardError, StringComparison.Ordinal);
        Assert.False(RebaseInProgress(repo));
        Assert.Equal(Topic, await HeadBranchAsync(repo));
        Assert.Equal(list.BranchTip, await repo.HeadAsync());
    }

    [Fact]
    public async Task Explicit_drop_lines_pass_rebase_missingCommitsCheck()
    {
        using var repo = await LineAsync();
        await repo.ConfigAsync("rebase.missingCommitsCheck", "error");
        var (repository, list) = await OpenListAsync(repo);

        var result = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Pick, Drop, Pick), cancellationToken: TestCancelled);

        Assert.Equal(RebaseOutcome.Rebased, result.Outcome);
        Assert.Equal(["Add one", "Add two", "Add four"], await SubjectsAsync(repo, $"{list.Onto}..HEAD"));
    }

    [Fact]
    public async Task No_other_branch_moves_also_with_rebase_updateRefs_set()
    {
        // other points at "Add two", inside the rebased range. Git 2.38 and later would move it
        // with rebase.updateRefs; the app's todo has no update-ref line (2.5).
        using var repo = await LineAsync();
        await repo.ConfigAsync("rebase.updateRefs", "true");
        var (repository, list) = await OpenListAsync(repo);
        var branchesBefore = await OtherBranchesAsync(repo);

        var result = await repository.InteractiveRebaseAsync(Plan(list, Topic, Reword("One reworded\n"), Pick, Pick, Pick), cancellationToken: TestCancelled);

        Assert.Equal(RebaseOutcome.Rebased, result.Outcome);
        Assert.Equal(branchesBefore, await OtherBranchesAsync(repo));
        Assert.NotEqual(list.BranchTip, await RevAsync(repo, "topic"));
    }
}
