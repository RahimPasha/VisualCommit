using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.SequenceTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The operation in progress as <see cref="GitRepository.ReadStatusAsync"/> reads it from the
/// files git leaves in the git folder (phase 3 design section 5, D89, D110; phase 4 design section
/// 8), for states made by git itself as a terminal would make them (<see cref="Scenarios.StoppedAsync"/>,
/// todos shaped as the app writes them, 2.5), and its abort (2.15): the lists' new properties, every
/// stop of a rebase (<see cref="RebaseStopKind"/>, the group, the next squash, the counts), and no
/// git call but the status when nothing is in progress. Each expected value is what git's own files
/// and commands say about that state.
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

        Assert.Equal(new InProgressState(InProgressKind.CherryPick, conflicting, "Conflicting change", null, null, 1, false) { Sequenced = true, HasPickHead = true }, status.InProgress);
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
        // The paused list names the stopped command, its todo's first line, resolved (8).
        Assert.Equal(
            new InProgressState(InProgressKind.CherryPick, await RevAsync(repo, "refs/heads/feature/conflict"), "Conflicting change", null, null, 1, false)
            {
                AbortKeepsHead = true,
                Sequenced = true,
            },
            status.InProgress);
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
        Assert.Equal(new InProgressState(InProgressKind.Revert, mainWork, "Main work", null, null, 1, false) { Sequenced = true, HasPickHead = true }, stopped.InProgress);

        // Resolved and committed in a terminal: stopped between commits.
        repo.WriteFile("src/core.txt", "core\n");
        await repo.GitAsync("add", "src/core.txt");
        await repo.GitAsync("commit", "--quiet", "--no-edit");
        Assert.False(GitFileExists(repo, "REVERT_HEAD"));
        Assert.StartsWith("revert ", ReadGitFile(repo, "sequencer/todo"), StringComparison.Ordinal);

        var between = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(
            new InProgressState(InProgressKind.Revert, mainWork, "Main work", null, null, 1, false) { AbortKeepsHead = true, Sequenced = true },
            between.InProgress);
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

        Assert.Equal(new InProgressState(InProgressKind.Revert, mainWork, "Main work", null, null, 0, false) { HasPickHead = true }, status.InProgress);
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

        var conflicting = await RevAsync(repo, "refs/heads/feature/conflict");

        var status = await repository.ReadStatusAsync(TestCancelled);

        // Stopped on a pick (8): the stopped commit from stopped-sha, the first of one.
        Assert.Equal(
            new InProgressState(InProgressKind.Rebase, conflicting, "Conflicting change", "feature/conflict", mainWork, 0, false)
            {
                StopKind = RebaseStopKind.Pick,
                Done = 1,
                Total = 1,
            },
            status.InProgress);
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
        Assert.Equal((1, 3), (status.InProgress.Done, status.InProgress.Total));
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
        var conflicting = await RevAsync(repo, "refs/heads/feature/conflict");

        // The apply backend: the commit being applied is original-commit; next and last count.
        Assert.Equal(conflicting, ReadGitFile(repo, "rebase-apply/original-commit").Trim());
        Assert.Equal(
            new InProgressState(InProgressKind.Rebase, conflicting, "Conflicting change", "feature/conflict", mainWork, 0, false)
            {
                StopKind = RebaseStopKind.Pick,
                Done = 1,
                Total = 1,
            },
            rebasing.InProgress);
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
        Assert.Equal(new InProgressState(InProgressKind.CherryPick, alphaOne, "Alpha one", null, null, 0, true) { HasPickHead = true }, status.InProgress);
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

    // ---- Phase 4: no git call when nothing is in progress (D89, 8.1) ------------------------------

    [Fact]
    public async Task With_nothing_in_progress_a_files_repository_reads_its_state_without_another_git_call()
    {
        using var repo = await Scenarios.OperationsAsync();
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var opened = calls.Snapshot().Count;

        var status = await repository.ReadStatusAsync(TestCancelled);

        // Only the status itself: no pick head is asked for through git, nor the ref format.
        Assert.Same(InProgressState.None, status.InProgress);
        Assert.Equal(["git status --porcelain=v2 -z --untracked-files=all --renames"], calls.Snapshot().Skip(opened).Select(call => call.CommandText));
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("config", "--get", "extensions.refStorage")).ExitCode);
    }

    // ---- Phase 4: the stops of a rebase (section 8) -----------------------------------------------

    [Fact]
    public async Task A_rebase_pick_resolved_to_nothing_is_empty()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Rebase);
        var repository = await OpenAsync(repo);
        await repo.GitAsync("checkout", "--ours", "--", "src/core.txt");
        await repo.GitAsync("add", "src/core.txt");

        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        // Nothing staged and nothing conflicted at a pick for stopped-sha: Continue leaves it out.
        Assert.Equal(RebaseStopKind.Empty, state.StopKind);
        Assert.False(state.HasPickHead);
        Assert.False(state.Empty); // A rebase's emptiness is its StopKind; Empty is a pick's or revert's.
        Assert.Equal(await RevAsync(repo, "refs/heads/feature/conflict"), state.Commit);
    }

    [Fact]
    public async Task A_pick_rescheduled_for_an_untracked_file_is_counted_once_also_after_another_try()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("README.md", "readme\n", "Initial commit");
        await repo.CreateBranchAsync("topic");
        var main = await repo.CommitFileAsync("main.txt", "main\n", "Main work");
        await repo.CheckoutAsync("topic");
        await repo.CommitFileAsync("x.txt", "x\n", "X first");
        var addIn = await repo.CommitFileAsync("in.txt", "tracked\n", "Add in");
        repo.DeleteFile("in.txt");
        await repo.CommitAsync("Remove in");
        repo.WriteFile("in.txt", "untracked\n");
        var repository = await OpenAsync(repo);
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("rebase", "main")).ExitCode);

        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        // X first done, Add in current (git put it back as the todo's first line), Remove in left.
        var expected = new InProgressState(InProgressKind.Rebase, addIn, "Add in", "topic", main, 1, false)
        {
            StopKind = RebaseStopKind.Rescheduled,
            Done = 2,
            Total = 3,
        };
        Assert.Equal(expected, state);
        Assert.StartsWith($"pick {addIn}", ReadGitFile(repo, "rebase-merge/git-rebase-todo"), StringComparison.Ordinal);

        // Git's next try fails the same way and appends to done again: the same state.
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("rebase", "--continue")).ExitCode);
        Assert.Equal(expected, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task A_failed_exec_from_another_todo_is_read_after_the_commit_it_followed()
    {
        var (repo, main, commits) = await TopicAsync(("a.txt", "A adds a"), ("b.txt", "B adds b"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);

        Assert.Equal(1, (await InteractiveRebaseAsync(repo, main, $"pick {commits[0]}\nexec false\npick {commits[1]}\n")).ExitCode);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        Assert.Equal(
            new InProgressState(InProgressKind.Rebase, commits[0], "A adds a", "topic", main, 1, false)
            {
                StopKind = RebaseStopKind.ExecFailed,
                Done = 1,
                Total = 2,
            },
            state);
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "rebase-merge", "stopped-sha")));
    }

    [Fact]
    public async Task The_apps_amend_refused_by_a_hook_is_its_own_failed_amend_with_the_groups_target()
    {
        var (repo, main, commits) = await TopicAsync(("t.txt", "T target"), ("p.txt", "P plain"));
        using var _repo = repo;
        WriteHook(repo, "commit-msg", RefusingWhileMarked);
        File.WriteAllText(Path.Combine(repo.Path, ".git", "refuse"), string.Empty);
        var repository = await OpenAsync(repo);

        var result = await InteractiveRebaseAsync(repo, main, Group(1, commits[0]) + $"pick {commits[1]}\n", "T reworded\n");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("the hook refuses this message", result.StandardError, StringComparison.Ordinal);
        Assert.Equal(
            new InProgressState(InProgressKind.Rebase, commits[0], "T target", "topic", main, 1, false)
            {
                StopKind = RebaseStopKind.OwnAmendFailed,
                Group = 1,
                Done = 1,
                Total = 2,
            },
            (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task An_edit_stop_from_another_todo_is_read_as_edit()
    {
        var (repo, main, commits) = await TopicAsync(("a.txt", "A adds a"), ("b.txt", "B adds b"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);

        // An edit stop exits 0 with the folder there (0.1).
        Assert.Equal(0, (await InteractiveRebaseAsync(repo, main, $"edit {commits[0]}\npick {commits[1]}\n")).ExitCode);

        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "rebase-merge", "amend")));
        Assert.Equal(
            new InProgressState(InProgressKind.Rebase, commits[0], "A adds a", "topic", main, 1, false)
            {
                StopKind = RebaseStopKind.Edit,
                Done = 1,
                Total = 2,
            },
            (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task An_interactive_rebases_commit_that_became_empty_is_read_as_empty()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
        await repo.CreateBranchAsync("topic");
        var main = await repo.CommitFileAsync("m.txt", "m\n", "Main adds m");
        await repo.CheckoutAsync("topic");
        var same = await repo.CommitFileAsync("m.txt", "m\n", "E same as main");
        var later = await repo.CommitFileAsync("b.txt", "b\n", "B adds b");
        var repository = await OpenAsync(repo);

        Assert.Equal(1, (await InteractiveRebaseAsync(repo, main, $"pick {same}\npick {later}\n")).ExitCode);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        // Git 2.36 leaves CHERRY_PICK_HEAD beside the folder (0.1); without it, the pick for
        // stopped-sha with nothing staged says the same.
        Assert.Equal(
            new InProgressState(InProgressKind.Rebase, same, "E same as main", "topic", main, 1, false)
            {
                HasPickHead = File.Exists(Path.Combine(repo.Path, ".git", "CHERRY_PICK_HEAD")),
                StopKind = RebaseStopKind.Empty,
                Done = 1,
                Total = 2,
            },
            state);
    }

    [Fact]
    public async Task A_groups_target_that_stops_is_inside_its_group_and_next_is_squash()
    {
        var (repo, main, commits) = await TopicAsync(("f.txt", "T conflicts"), ("h.txt", "F fixup"), ("p.txt", "P plain"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);

        Assert.Equal(1, (await InteractiveRebaseAsync(repo, main, Group(1, commits[0], commits[1]) + $"pick {commits[2]}\n", "Group message\n")).ExitCode);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        // label and exec lines are not counted: T is the first of three commits.
        Assert.Equal(
            new InProgressState(InProgressKind.Rebase, commits[0], "T conflicts", "topic", main, 2, false)
            {
                StopKind = RebaseStopKind.Pick,
                NextIsSquash = true,
                Group = 1,
                Done = 1,
                Total = 3,
            },
            state);
    }

    [Fact]
    public async Task A_groups_member_that_stops_is_inside_its_group_but_no_target()
    {
        var (repo, main, commits) = await TopicAsync(("t.txt", "T target"), ("f.txt", "F1 conflicts"), ("h.txt", "F2 more"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);

        Assert.Equal(1, (await InteractiveRebaseAsync(repo, main, Group(1, commits[0], commits[1], commits[2]), "Group message\n")).ExitCode);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        // The next line is a fixup too, but leaving F1 out joins nothing to another commit.
        Assert.Equal(
            new InProgressState(InProgressKind.Rebase, commits[1], "F1 conflicts", "topic", main, 1, false)
            {
                StopKind = RebaseStopKind.Pick,
                Group = 1,
                Done = 2,
                Total = 3,
            },
            state);
    }

    [Fact]
    public async Task A_pick_before_a_group_and_a_reword_are_told_apart_by_the_next_label()
    {
        var (repo, main, commits) = await TopicAsync(("f.txt", "P conflicts"), ("t.txt", "T target"), ("h.txt", "F fixup"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);

        // A plain pick, then group 2: the stop is outside any group.
        Assert.Equal(1, (await InteractiveRebaseAsync(repo, main, $"pick {commits[0]}\n" + Group(2, commits[1], commits[2]), "Unused\n", "Group two\n")).ExitCode);
        var plain = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal((RebaseStopKind.Pick, (int?)null, false, 1, 3), (plain.StopKind!.Value, plain.Group, plain.NextIsSquash, plain.Done, plain.Total));
        await repo.GitAsync("rebase", "--abort");

        // A reword: a group without members, its amend right after the target.
        Assert.Equal(1, (await InteractiveRebaseAsync(repo, main, Group(1, commits[0]) + $"pick {commits[1]}\n", "P reworded\n")).ExitCode);
        var reword = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal((RebaseStopKind.Pick, (int?)1, false, 1, 2), (reword.StopKind!.Value, reword.Group, reword.NextIsSquash, reword.Done, reword.Total));
    }

    /// <summary>
    /// main and topic from "Initial commit" (f.txt "base\n"): main changes f.txt ("Main edit"),
    /// topic makes one commit per file and subject given, each file "&lt;subject&gt;\n" (so a
    /// commit to f.txt conflicts with main); HEAD on topic. Returns main's tip and topic's commits.
    /// </summary>
    private static async Task<(TempRepo Repo, string Main, IReadOnlyList<string> Commits)> TopicAsync(params (string File, string Subject)[] commits)
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
            await repo.CreateBranchAsync("topic");
            var main = await repo.CommitFileAsync("f.txt", "main\n", "Main edit");
            await repo.CheckoutAsync("topic");
            var made = new List<string>();
            foreach (var (file, subject) in commits)
            {
                made.Add(await repo.CommitFileAsync(file, subject + "\n", subject));
            }

            return (repo, main, made);
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }
}
