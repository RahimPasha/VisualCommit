using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The operation in progress as <see cref="GitRepository.ReadStatusAsync"/> reads it from the
/// files git leaves in the git folder (design section 5, D89, D110), for states made by git
/// itself as a terminal would make them (<see cref="Scenarios.StoppedAsync"/>), and its abort
/// (2.15). Each expected value is what git's own files and commands say about that state.
/// </summary>
public class InProgressTests
{
    [Fact]
    public async Task A_clean_repository_has_nothing_in_progress()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);

        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.Same(InProgressState.None, status.InProgress);
        Assert.False(status.InProgress.IsActive);
        Assert.True(status.SameAs(WorkingTreeStatus.Clean));
    }

    [Fact]
    public async Task A_stopped_merge_is_read_with_its_commit_and_message()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        var repository = await OpenAsync(repo);

        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.Equal(
            new InProgressState(InProgressKind.Merge, await RevAsync(repo, "MERGE_HEAD"), "Merge branch 'feature/conflict'", null, null, 0, false),
            status.InProgress);
        Assert.Equal(await RevAsync(repo, "refs/heads/feature/conflict"), status.InProgress.Commit);
        Assert.Equal([new ChangedFile("src/core.txt", FileChangeKind.Conflicted)], status.Unstaged);

        // The merge resolved: the state stays until it is committed.
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        status = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(InProgressKind.Merge, status.InProgress.Kind);
        Assert.Equal([new ChangedFile("src/core.txt", FileChangeKind.Modified)], status.Staged);
        Assert.False(status.InProgress.Empty);
    }

    [Fact]
    public async Task A_cherry_pick_sequence_from_a_terminal_shows_the_stopped_pick_and_the_commits_left()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        var repository = await OpenAsync(repo);
        var conflicting = await RevAsync(repo, "refs/heads/feature/conflict");

        // Git's own files: the stopped pick is the todo's first line, Alpha two waits, and HEAD is
        // where the sequencer left it.
        Assert.Equal(conflicting, ReadGitFile(repo, "CHERRY_PICK_HEAD").Trim());
        Assert.Equal(2, ReadGitFile(repo, "sequencer/todo").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(await repo.HeadAsync(), ReadGitFile(repo, "sequencer/abort-safety").Trim());

        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.Equal(new InProgressState(InProgressKind.CherryPick, conflicting, "Conflicting change", null, null, 1, false), status.InProgress);
        Assert.False(status.InProgress.AbortKeepsHead);
    }

    [Fact]
    public async Task After_the_stopped_pick_is_committed_one_commit_is_left_and_abort_keeps_HEAD()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        var repository = await OpenAsync(repo);
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        await repo.GitAsync("commit", "--quiet", "--no-edit");
        var committed = await repo.HeadAsync();

        // Git keeps both lines in the todo and no CHERRY_PICK_HEAD; --continue would pick only
        // Alpha two, and --abort would not rewind (D110).
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.Equal(2, ReadGitFile(repo, "sequencer/todo").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.NotEqual(committed, ReadGitFile(repo, "sequencer/abort-safety").Trim());

        var status = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(new InProgressState(InProgressKind.CherryPick, null, null, null, null, 1, false) { AbortKeepsHead = true }, status.InProgress);
        Assert.Empty(status.Unstaged);

        var snapshot = await repository.SnapshotTrackedChangesAsync(TestCancelled);
        Assert.Null(snapshot); // Nothing changed: the abort's backup is HEAD alone.
        await repository.SaveBackupRefsAsync(new BackupRequest("abort", [new BackupEntry(null, committed)]), TestCancelled);
        await repository.AbortAsync(InProgressKind.CherryPick, cancellationToken: TestCancelled);

        Assert.Equal(committed, await repo.HeadAsync());
        Assert.Equal(["Conflicting change", "Alpha one", "Main work"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task A_sequence_of_reverts_stopped_between_commits_is_read_by_its_todo()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var addCore = await RevAsync(repo, "refs/heads/main~1");
        await repo.CommitFileAsync("src/core.txt", Scenarios.OperationsFiles.CoreAgain, "Core again");

        // Main work's revert conflicts with Core again; Add core waits.
        Assert.False((await repo.GitAllowingFailureAsync("revert", "--no-edit", mainWork, addCore)).Succeeded);
        var stopped = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(new InProgressState(InProgressKind.Revert, mainWork, "Main work", null, null, 1, false), stopped.InProgress);

        // Resolved and committed in a terminal: stopped between commits.
        repo.WriteFile("src/core.txt", "core\n");
        await repo.GitAsync("add", "src/core.txt");
        await repo.GitAsync("commit", "--quiet", "--no-edit");
        Assert.False(GitFileExists(repo, "REVERT_HEAD"));
        Assert.StartsWith("revert ", ReadGitFile(repo, "sequencer/todo"), StringComparison.Ordinal);

        var between = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(new InProgressState(InProgressKind.Revert, null, null, null, null, 1, false) { AbortKeepsHead = true }, between.InProgress);
    }

    [Fact]
    public async Task A_stopped_revert_is_read_with_the_reverted_commit()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Revert);
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main~1");
        Assert.Equal(mainWork, ReadGitFile(repo, "REVERT_HEAD").Trim());
        Assert.False(GitFileExists(repo, "sequencer"));

        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.Equal(new InProgressState(InProgressKind.Revert, mainWork, "Main work", null, null, 0, false), status.InProgress);
        Assert.False(status.InProgress.AbortKeepsHead);
    }

    [Fact]
    public async Task A_stopped_rebase_is_read_with_its_branch_and_base()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Rebase);
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");

        // Git leaves MERGE_MSG too, but no MERGE_HEAD: the rebase comes first.
        Assert.True(GitFileExists(repo, "rebase-merge"));
        Assert.Equal("refs/heads/feature/conflict", ReadGitFile(repo, "rebase-merge/head-name").Trim());

        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.Equal(new InProgressState(InProgressKind.Rebase, null, null, "feature/conflict", mainWork, 0, false), status.InProgress);
        Assert.Equal([new ChangedFile("src/core.txt", FileChangeKind.Conflicted)], status.Unstaged);
    }

    [Fact]
    public async Task A_rebase_of_several_commits_counts_the_ones_left_in_its_todo()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.CheckoutAsync("feature/conflict");
        await repo.CommitFileAsync("src/later.txt", "later\n", "Later one");
        await repo.CommitFileAsync("src/later.txt", "later\nlater two\n", "Later two");
        var repository = await OpenAsync(repo);

        Assert.False((await repo.GitAllowingFailureAsync("rebase", "--merge", "main")).Succeeded);
        var todo = ReadGitFile(repo, "rebase-merge/git-rebase-todo").Split('\n').Count(line => line.Length > 0 && !line.StartsWith('#'));
        Assert.Equal(2, todo);

        var status = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(InProgressKind.Rebase, status.InProgress.Kind);
        Assert.Equal(2, status.InProgress.Remaining);
        Assert.Equal("feature/conflict", status.InProgress.Branch);
    }

    [Fact]
    public async Task Git_am_and_the_apply_backend_of_rebase_are_told_apart_by_their_applying_file()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var patch = repo.SiblingPath("conflicting.patch");
        File.WriteAllText(patch, (await repo.GitAsync("format-patch", "-1", "--stdout", "refs/heads/feature/conflict")).StandardOutput);

        Assert.False((await repo.GitAllowingFailureAsync("am", "--3way", patch)).Succeeded);
        Assert.True(GitFileExists(repo, "rebase-apply/applying"));
        var applying = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, 0, false), applying.InProgress);

        await repository.AbortAsync(InProgressKind.ApplyMailbox, cancellationToken: TestCancelled);
        Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
        Assert.Equal(mainWork, await repo.HeadAsync());
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));

        await repo.CheckoutAsync("feature/conflict");
        Assert.False((await repo.GitAllowingFailureAsync("rebase", "--apply", "main")).Succeeded);
        Assert.False(GitFileExists(repo, "rebase-apply/applying"));
        var rebasing = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(new InProgressState(InProgressKind.Rebase, null, null, "feature/conflict", mainWork, 0, false), rebasing.InProgress);
    }

    [Fact]
    public async Task Conflicts_from_a_stash_have_no_operation_in_progress()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.None);
        var repository = await OpenAsync(repo);

        var status = await repository.ReadStatusAsync(TestCancelled);

        Assert.Same(InProgressState.None, status.InProgress);
        Assert.Equal([new ChangedFile("src/core.txt", FileChangeKind.Conflicted)], status.Unstaged);
    }

    [Fact]
    public async Task A_cherry_pick_that_changes_nothing_is_empty()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var alphaOne = await RevAsync(repo, "refs/heads/feature/alpha~1");
        await repo.GitAsync("cherry-pick", alphaOne);

        // Picked again: git stops with CHERRY_PICK_HEAD, nothing staged and nothing conflicted.
        Assert.False((await repo.GitAllowingFailureAsync("cherry-pick", alphaOne)).Succeeded);
        Assert.True(GitFileExists(repo, "CHERRY_PICK_HEAD"));

        var status = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(new InProgressState(InProgressKind.CherryPick, alphaOne, "Alpha one", null, null, 0, true), status.InProgress);
        Assert.False(status.HasChanges);

        // An unstaged change elsewhere keeps the index equal to HEAD's tree: still empty. Once it is
        // staged, committing would record it, so the pick is no longer empty.
        repo.WriteFile("README.md", "# Operations\n\nEdited.\n");
        Assert.True((await repository.ReadStatusAsync(TestCancelled)).InProgress.Empty);
        await repo.GitAsync("add", "README.md");
        Assert.False((await repository.ReadStatusAsync(TestCancelled)).InProgress.Empty);
    }

    [Fact]
    public async Task The_same_files_with_another_operation_are_not_the_same_status()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        var repository = await OpenAsync(repo);
        var merging = await repository.ReadStatusAsync(TestCancelled);

        var withoutOperation = merging with { InProgress = InProgressState.None };
        var keepsHead = merging with { InProgress = merging.InProgress with { AbortKeepsHead = true } };

        Assert.True(merging.SameAs(await repository.ReadStatusAsync(TestCancelled)));
        Assert.False(merging.SameAs(withoutOperation));
        Assert.False(merging.SameAs(keepsHead));
    }

    [Fact]
    public async Task The_state_of_a_linked_worktree_is_its_own()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        var worktree = repo.SiblingPath(Scenarios.EdgeCasesWorktree);

        // side (at Initial commit) adds src/main.txt with other content than main's, so a merge of
        // main stops in the linked worktree with an add/add conflict.
        Directory.CreateDirectory(Path.Combine(worktree, "src"));
        File.WriteAllText(Path.Combine(worktree, "src", "main.txt"), "side\n");
        await GitInAsync(repo, worktree, "add", "src/main.txt");
        await GitInAsync(repo, worktree, "commit", "--quiet", "--message", "Side work");
        var merge = new GitCommand("merge", "--no-edit", "refs/heads/main") { WorkingDirectory = worktree, Environment = repo.Environment };
        Assert.False((await repo.Runner.RunAsync(merge, TestCancelled)).Succeeded);

        var linked = await GitRepository.OpenAsync(NewRunner(), worktree, TestCancelled);
        var main = await OpenAsync(repo);
        var linkedStatus = await linked.ReadStatusAsync(TestCancelled);

        Assert.Equal(InProgressKind.Merge, linkedStatus.InProgress.Kind);
        Assert.Equal(await RevAsync(repo, "refs/heads/main"), linkedStatus.InProgress.Commit);
        Assert.Equal([new ChangedFile("src/main.txt", FileChangeKind.Conflicted)], linkedStatus.Unstaged);
        Assert.Same(InProgressState.None, (await main.ReadStatusAsync(TestCancelled)).InProgress);
        Assert.False(GitFileExists(repo, "MERGE_HEAD"));

        // Its abort ends it there.
        await linked.AbortAsync(InProgressKind.Merge, cancellationToken: TestCancelled);
        Assert.Same(InProgressState.None, (await linked.ReadStatusAsync(TestCancelled)).InProgress);
        Assert.Equal("Side work", await GitInAsync(repo, worktree, "log", "-1", "--format=%s"));
    }
}
