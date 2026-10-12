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
/// The guarded amend that ends every group of the app's todo (docs/phase-4-design.md 2.5): it
/// gives the group's commit its message when the group's pick is clean, conflicted and
/// continued, or a member conflicts, and leaves the commit before the group alone when the
/// group's pick is skipped; it runs the user's <c>pre-commit</c> and <c>commit-msg</c> hooks,
/// and a hook that refuses stops the rebase at the failed <c>exec</c>.
/// </summary>
public class InteractiveRebaseGuardTests
{
    [Fact]
    public async Task The_guard_does_not_amend_when_the_groups_pick_is_skipped()
    {
        // The target T conflicts with main and is skipped: git applies its fixup to the commit
        // before the group, and the guarded amend must not give that commit the group's message.
        using var repo = await OntoMainAsync(memberConflicts: false);
        var (repository, list) = await OpenListAsync(repo, "main");

        var stopped = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Target("Group message\n"), Fixup), cancellationToken: TestCancelled);
        Assert.Equal(new RebaseResult(RebaseOutcome.Stopped, null, 1), stopped);

        await repo.GitAsync("rebase", "--skip");

        Assert.False(RebaseInProgress(repo));
        Assert.Equal("Add p\n", await MessageAsync(repo, "HEAD"));
        Assert.Equal(await RevAsync(repo, "main"), await RevAsync(repo, "HEAD~1"));
        Assert.True(File.Exists(Path.Combine(repo.Path, "g.txt")));
        Assert.DoesNotContain("Group message", (await repo.GitAsync("log", "--format=%B", "HEAD")).StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_guard_amends_when_the_groups_pick_conflicted_and_was_resolved_and_continued()
    {
        using var repo = await OntoMainAsync(memberConflicts: false);
        var (repository, list) = await OpenListAsync(repo, "main");

        var stopped = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Target("Group message\n\n#4 kept\n"), Fixup), cancellationToken: TestCancelled);
        Assert.Equal(RebaseOutcome.Stopped, stopped.Outcome);

        repo.WriteFile("f.txt", "MT\nb\nc\n");
        await repo.GitAsync("add", "f.txt");
        await repo.GitAsync("rebase", "--continue");

        Assert.False(RebaseInProgress(repo));
        Assert.Equal("Group message\n\n#4 kept\n", await MessageAsync(repo, "HEAD"));
        Assert.Equal(["Add p", "Group message"], await SubjectsAsync(repo, "main..HEAD"));
        Assert.Equal("MT\nb\nc\n", ReadText(repo, "f.txt"));
        Assert.True(File.Exists(Path.Combine(repo.Path, "g.txt")));
    }

    [Fact]
    public async Task A_group_of_fixups_only_keeps_the_targets_hash_lines_also_when_a_fixup_conflicts()
    {
        using var repo = await OntoMainAsync(memberConflicts: true);
        var (repository, list) = await OpenListAsync(repo, "main");
        var own = await MessageAsync(repo, list.CommitsOldestFirst[1].Sha);
        Assert.Contains("#12 issue line", own, StringComparison.Ordinal);

        var stopped = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Pick, Fixup), cancellationToken: TestCancelled);
        Assert.Equal(new RebaseResult(RebaseOutcome.Stopped, null, 1), stopped);

        repo.WriteFile("f.txt", "MF\nb\nc\n");
        await repo.GitAsync("add", "f.txt");
        await repo.GitAsync("rebase", "--continue");

        Assert.False(RebaseInProgress(repo));
        Assert.Equal(own, await MessageAsync(repo, "HEAD"));
        Assert.Equal(["Add p", "Add t"], await SubjectsAsync(repo, "main..HEAD"));
        Assert.Equal("MF\nb\nc\n", ReadText(repo, "f.txt"));
    }

    [Fact]
    public async Task The_guard_amends_with_the_groups_message_when_a_squash_member_conflicts()
    {
        using var repo = await OntoMainAsync(memberConflicts: true);
        var (repository, list) = await OpenListAsync(repo, "main");

        var stopped = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Target("T and F\n\n#2 kept\n"), Squash), cancellationToken: TestCancelled);
        Assert.Equal(RebaseOutcome.Stopped, stopped.Outcome);

        repo.WriteFile("f.txt", "MF\nb\nc\n");
        await repo.GitAsync("add", "f.txt");
        await repo.GitAsync("rebase", "--continue");

        Assert.False(RebaseInProgress(repo));
        Assert.Equal("T and F\n\n#2 kept\n", await MessageAsync(repo, "HEAD"));
        Assert.Equal(await RevAsync(repo, "main"), await RevAsync(repo, "HEAD~2"));
    }

    [Fact]
    public async Task The_amend_runs_the_users_pre_commit_and_commit_msg_hooks()
    {
        using var repo = await LineAsync();
        InstallHook(repo, "pre-commit", "echo ran >> \"$(git rev-parse --git-dir)/pre-commit-ran\"");
        InstallHook(repo, "commit-msg", "echo 'Hooked-By: test' >> \"$1\"");
        var (repository, list) = await OpenListAsync(repo);

        var result = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Reword("New two\n\n#5 kept\n"), Pick, Pick), cancellationToken: TestCancelled);

        Assert.Equal(RebaseOutcome.Rebased, result.Outcome);
        Assert.Equal("New two\n\n#5 kept\nHooked-By: test\n", await MessageAsync(repo, "HEAD~2"));
        Assert.True(GitFileExists(repo, "pre-commit-ran"));
    }

    [Fact]
    public async Task A_hook_that_refuses_the_new_message_stops_the_rebase_at_the_failed_exec()
    {
        using var repo = await LineAsync();
        InstallHook(repo, "commit-msg", "echo 'refused by the hook' >&2\nexit 1");
        var (repository, list) = await OpenListAsync(repo);

        var result = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Reword("New two\n"), Pick, Pick), cancellationToken: TestCancelled);

        Assert.Equal(new RebaseResult(RebaseOutcome.Stopped, null, 0), result);
        Assert.True(RebaseInProgress(repo));
        var done = ReadGitFile(repo, "rebase-merge/done").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("exec ", done[^1], StringComparison.Ordinal);
        Assert.Contains("vc-message-1", done[^1], StringComparison.Ordinal);
        Assert.Equal("New two\n", ReadGitFile(repo, "rebase-merge/vc-message-1"));
        Assert.Equal(TwoMessage, await MessageAsync(repo, "HEAD"));

        await repo.GitAsync("rebase", "--abort");
        Assert.Equal(list.BranchTip, await repo.HeadAsync());
    }
}
