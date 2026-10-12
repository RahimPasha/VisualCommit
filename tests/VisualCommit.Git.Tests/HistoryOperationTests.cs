using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.SequenceTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The history operations against real git on the scenario repos: phase 3's reset in each mode
/// with its backup refs, cherry-pick and revert of one commit (design 2.11 to 2.15; the
/// single-commit calls stay for one commit, and the tests that call them once per commit show
/// what they do), phase 4's lists with one git call and git's sequencer, one test per row of
/// docs/phase-4-design.md section 4's table (D136, D145), <c>--quit</c>, and the abort of every
/// state. Expected states are read back with plain git.
/// </summary>
public class HistoryOperationTests
{
    // ---- Reset ------------------------------------------------------------------------------------

    [Theory]
    [InlineData(ResetMode.Soft, "M  src/core.txt")]
    [InlineData(ResetMode.Mixed, " M src/core.txt")]
    public async Task A_soft_or_mixed_reset_moves_the_branch_keeps_the_changes_and_backs_up_HEAD(ResetMode mode, string status)
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var addCore = await RevAsync(repo, "refs/heads/main~1");

        // As the reset dialog runs it (2.11): the backup batch, then the reset.
        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, mainWork)]), TestCancelled);
        await repository.ResetAsync(addCore, mode, cancellationToken: TestCancelled);

        Assert.Equal(addCore, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(["## main...origin/main [behind 1]", status], await GitLinesAsync(repo, "status", "--short", "--branch"));
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, ReadText(repo, "src/core.txt"));
        Assert.Equal([new RefEntry($"refs/visualcommit/backup/reset-{backup.Time}", mainWork)], await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task A_mixed_reset_prints_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmore\n");
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var addCore = await RevAsync(repo, "refs/heads/main~1");

        await repository.ResetAsync(addCore, ResetMode.Mixed, cancellationToken: TestCancelled);

        var reset = calls.Snapshot().Single(call => call.CommandText.StartsWith("git reset", StringComparison.Ordinal));
        Assert.Equal($"git reset --mixed --quiet {addCore} --", reset.CommandText);
        Assert.Equal(0, reset.ExitCode);
        Assert.Equal(string.Empty, reset.StandardOutput); // Without --quiet git lists the unstaged files.
        Assert.Equal([" M src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task A_hard_reset_saves_the_changes_it_discards_in_the_same_batch()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var addCore = await RevAsync(repo, "refs/heads/main~1");

        // Check 10 part C's files: staged and unstaged edits of one file, an unstaged edit, a staged
        // new file, and an untracked file that Add core does not track.
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\n");
        await repo.GitAsync("add", "src/core.txt");
        repo.WriteFile("src/core.txt", "core\nmain work\nstaged line\nunstaged line\n");
        repo.WriteFile("README.md", "# Operations\n\nLocal edit.\n");
        repo.WriteFile("scratch.txt", "scratch\n");
        await repo.GitAsync("add", "scratch.txt");
        repo.WriteFile("todo.txt", "todo\n");

        var changes = await repository.SnapshotForHardResetAsync(addCore, TestCancelled);
        Assert.NotNull(changes);
        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, mainWork)]) { ChangesCommit = changes }, TestCancelled);
        await repository.ResetAsync(addCore, ResetMode.Hard, cancellationToken: TestCancelled);

        // The reset as git does it.
        Assert.Equal(["## main...origin/main [behind 1]", "?? todo.txt"], await GitLinesAsync(repo, "status", "--short", "--branch"));
        Assert.Equal("# Operations\n", ReadText(repo, "README.md"));
        Assert.Equal("core\n", ReadText(repo, "src/core.txt"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "scratch.txt")));
        Assert.Equal("todo\n", ReadText(repo, "todo.txt"));

        // Both refs, one timestamp, the snapshot paired by name.
        var stem = $"refs/visualcommit/backup/reset-{backup.Time}";
        Assert.Equal([stem, stem + "-changes"], backup.Refs);
        Assert.Equal([new RefEntry(stem, mainWork), new RefEntry(stem + "-changes", changes)], await repo.BackupRefsAsync());

        // The snapshot: HEAD then the index as parents; the working tree's versions in its tree.
        var parents = (await GitTextAsync(repo, "log", "-1", "--format=%P", changes)).Split(' ');
        Assert.Equal(2, parents.Length);
        Assert.Equal(mainWork, parents[0]);
        Assert.Equal(["README.md", "scratch.txt", "src/core.txt"], await TreeFilesAsync(repo, changes));
        Assert.Equal("# Operations\n\nLocal edit.\n", await ShowFileAsync(repo, changes, "README.md"));
        Assert.Equal("scratch\n", await ShowFileAsync(repo, changes, "scratch.txt"));
        Assert.Equal("core\nmain work\nstaged line\nunstaged line\n", await ShowFileAsync(repo, changes, "src/core.txt"));
        Assert.Equal(mainWork, await GitTextAsync(repo, "log", "-1", "--format=%P", parents[1]));
        Assert.Equal(["README.md", "scratch.txt", "src/core.txt"], await TreeFilesAsync(repo, parents[1]));
        Assert.Equal("# Operations\n", await ShowFileAsync(repo, parents[1], "README.md"));
        Assert.Equal("core\nmain work\nstaged line\n", await ShowFileAsync(repo, parents[1], "src/core.txt"));
    }

    [Fact]
    public async Task A_hard_reset_with_nothing_to_lose_saves_HEAD_alone()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("todo.txt", "todo\n");
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var addCore = await RevAsync(repo, "refs/heads/main~1");

        var changes = await repository.SnapshotForHardResetAsync(addCore, TestCancelled);
        Assert.Null(changes); // todo.txt is untracked there too: the reset leaves it alone.

        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, mainWork)]) { ChangesCommit = changes }, TestCancelled);
        await repository.ResetAsync(addCore, ResetMode.Hard, cancellationToken: TestCancelled);

        Assert.Equal([$"refs/visualcommit/backup/reset-{backup.Time}"], backup.Refs);
        Assert.Single(await repo.BackupRefsAsync());
        Assert.Equal(addCore, await repo.HeadAsync());
        Assert.Equal("todo\n", ReadText(repo, "todo.txt"));
    }

    [Fact]
    public async Task A_detached_HEAD_is_reset_where_it_is()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        var repository = await OpenAsync(repo);
        var initial = await RevAsync(repo, "refs/heads/side");

        await repository.ResetAsync(initial, ResetMode.Hard, cancellationToken: TestCancelled);

        Assert.Equal(initial, await repo.HeadAsync());
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD")).ExitCode);
        Assert.Equal(await RevAsync(repo, "refs/remotes/origin/main"), await repo.RefTargetAsync("refs/heads/main"));
    }

    // ---- Cherry-pick ------------------------------------------------------------------------------

    [Fact]
    public async Task Cherry_picking_one_commit_applies_it_on_HEAD_with_its_author_date()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var alphaOne = await RevAsync(repo, "refs/heads/feature/alpha~1");

        Assert.Equal(ApplyResult.Applied, await repository.CherryPickAsync(alphaOne, cancellationToken: TestCancelled));

        Assert.Equal($"Alpha one\n{mainWork}", await GitTextAsync(repo, "log", "-1", "--format=%B%P"));
        (DateTimeOffset, TimeSpan)[] authorThenCommitter =
        [
            (new DateTimeOffset(2026, 1, 1, 12, 2, 0, TimeSpan.Zero), TimeSpan.Zero),
            (GitIsolation.AppCommitDate, TimeSpan.Zero),
        ];
        Assert.Equal(
            authorThenCommitter,
            GitIsoDates.ParseLines(await GitTextAsync(repo, "log", "-1", "--format=%aI%n%cI")).Select(date => (date, date.Offset)));
        Assert.Equal(Scenarios.OperationsFiles.AlphaOne, ReadText(repo, "src/alpha.txt"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await GitLinesAsync(repo, "status", "--short", "--branch"));
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.Empty(await repo.BackupRefsAsync());
    }

    [Fact]
    public async Task Several_commits_are_picked_one_at_a_time_and_stop_at_the_first_conflict()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        string[] picks =
        [
            await RevAsync(repo, "refs/heads/feature/alpha~1"),
            await RevAsync(repo, "refs/heads/feature/conflict"),
            await RevAsync(repo, "refs/heads/feature/alpha"),
        ];

        // As the operation of 2.13 runs them, oldest first, stopping at the first that does not
        // apply cleanly.
        var results = new List<ApplyResult>();
        foreach (var pick in picks)
        {
            var result = await repository.CherryPickAsync(pick, cancellationToken: TestCancelled);
            results.Add(result);
            if (result != ApplyResult.Applied)
            {
                break;
            }
        }

        Assert.Equal([ApplyResult.Applied, ApplyResult.Conflicts], results);
        var picked = await repo.HeadAsync();
        Assert.Equal(["Alpha one", "Main work"], await GitLinesAsync(repo, "log", "--format=%s", "-2"));
        Assert.Equal(mainWork, await RevAsync(repo, "HEAD~1"));

        // A plain single pick: no sequencer, which committing in the stage panel would conclude
        // and whose abort undoes it alone.
        Assert.Equal(picks[1], ReadGitFile(repo, "CHERRY_PICK_HEAD").Trim());
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.Equal("Conflicting change\n\n# Conflicts:\n#\tsrc/core.txt\n", ReadGitFile(repo, "MERGE_MSG"));
        Assert.Equal(["UU src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        var status = await repository.ReadStatusAsync(TestCancelled);
        Assert.Equal(new InProgressState(InProgressKind.CherryPick, picks[1], "Conflicting change", null, null, 0, false) { HasPickHead = true }, status.InProgress);
        Assert.False(status.InProgress.AbortKeepsHead);

        // The abort keeps the pick already made.
        var changes = await repository.SnapshotTrackedChangesAsync(TestCancelled);
        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("abort", [new BackupEntry(null, picked)]) { ChangesCommit = changes }, TestCancelled);
        await repository.AbortAsync(InProgressKind.CherryPick, cancellationToken: TestCancelled);

        Assert.Equal(picked, await repo.HeadAsync());
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, ReadText(repo, "src/core.txt"));
        Assert.Equal(Scenarios.OperationsFiles.AlphaOne, ReadText(repo, "src/alpha.txt"));
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.Equal([$"refs/visualcommit/backup/abort-{backup.Time}", $"refs/visualcommit/backup/abort-{backup.Time}-changes"], backup.Refs);
        Assert.Equal(picked, await GitTextAsync(repo, "log", "-1", "--format=%P", changes!));
        Assert.Equal(
            $"core\n<<<<<<< HEAD\nmain work\n=======\nconflict\n>>>>>>> {picks[1][..7]} (Conflicting change)\n",
            await ShowFileAsync(repo, changes!, "src/core.txt"));
    }

    [Fact]
    public async Task A_cherry_pick_that_changes_nothing_stops_empty_and_its_abort_keeps_HEAD()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var alphaOne = await RevAsync(repo, "refs/heads/feature/alpha~1");
        Assert.Equal(ApplyResult.Applied, await repository.CherryPickAsync(alphaOne, cancellationToken: TestCancelled));
        var picked = await repo.HeadAsync();

        Assert.Equal(ApplyResult.Empty, await repository.CherryPickAsync(alphaOne, cancellationToken: TestCancelled));

        Assert.Equal(alphaOne, ReadGitFile(repo, "CHERRY_PICK_HEAD").Trim());
        Assert.Equal(picked, await repo.HeadAsync());
        Assert.True((await repository.ReadStatusAsync(TestCancelled)).InProgress.Empty);

        Assert.Null(await repository.SnapshotTrackedChangesAsync(TestCancelled));
        await repository.AbortAsync(InProgressKind.CherryPick, cancellationToken: TestCancelled);
        Assert.Equal(picked, await repo.HeadAsync());
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
    }

    [Fact]
    public async Task A_cherry_pick_refused_by_local_changes_throws_with_gits_text()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nlocal\n");
        var repository = await OpenAsync(repo);
        var head = await repo.HeadAsync();
        var conflicting = await RevAsync(repo, "refs/heads/feature/conflict");

        var error = await Assert.ThrowsAsync<GitException>(() => repository.CherryPickAsync(conflicting, cancellationToken: TestCancelled));

        Assert.Contains("would be overwritten", error.StandardError, StringComparison.Ordinal);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.Equal([" M src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task With_conflicts_already_there_a_pick_a_revert_and_a_merge_are_refused_not_stopped()
    {
        // Conflicts from a stash, with no operation in progress: git refuses each call.
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.None);
        var repository = await OpenAsync(repo);
        var head = await repo.HeadAsync();
        var alphaOne = await RevAsync(repo, "refs/heads/feature/alpha~1");

        await Assert.ThrowsAsync<GitException>(() => repository.CherryPickAsync(alphaOne, cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<GitException>(() => repository.RevertAsync(head, isMerge: false, cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<GitException>(() => repository.MergeAsync("refs/heads/feature/alpha", MergeMode.Default, cancellationToken: TestCancelled));

        Assert.Equal(head, await repo.HeadAsync());
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.False(GitFileExists(repo, "REVERT_HEAD"));
        Assert.False(GitFileExists(repo, "MERGE_HEAD"));
        Assert.Equal(["UU src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
    }

    // ---- Revert -----------------------------------------------------------------------------------

    [Fact]
    public async Task Reverting_a_commit_makes_gits_revert_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var reference = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");

        Assert.Equal(ApplyResult.Applied, await repository.RevertAsync(mainWork, isMerge: false, cancellationToken: TestCancelled));
        await reference.GitAsync("revert", "--no-edit", mainWork);

        Assert.Equal(await GitTextAsync(reference, "log", "-1", "--format=%B"), await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal($"Revert \"Main work\"\n\nThis reverts commit {mainWork}.", await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal(mainWork, await RevAsync(repo, "HEAD~1"));
        Assert.Equal(Scenarios.OperationsFiles.Core, ReadText(repo, "src/core.txt"));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task Reverting_a_merge_commit_undoes_what_its_second_parent_brought_in()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        Assert.Equal(MergeResult.Merged, await repository.MergeAsync("refs/heads/feature/alpha", MergeMode.NoFastForward, cancellationToken: TestCancelled));
        var merge = await repo.HeadAsync();

        Assert.Equal(ApplyResult.Applied, await repository.RevertAsync(merge, isMerge: true, cancellationToken: TestCancelled));

        Assert.Equal(
            $"Revert \"Merge branch 'feature/alpha'\"\n\nThis reverts commit {merge}, reversing\nchanges made to {mainWork}.",
            await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal(merge, await RevAsync(repo, "HEAD~1"));
        Assert.Equal(["D\tsrc/alpha.txt"], await GitLinesAsync(repo, "diff", "--name-status", "HEAD^", "HEAD"));

        // Without -m, git refuses a merge commit.
        var error = await Assert.ThrowsAsync<GitException>(() => repository.RevertAsync(merge, isMerge: false, cancellationToken: TestCancelled));
        Assert.Contains("is a merge but no -m option was given", error.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Several_commits_are_reverted_newest_first_one_at_a_time()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);

        foreach (var commit in new[] { await RevAsync(repo, "refs/heads/main"), await RevAsync(repo, "refs/heads/main~1") })
        {
            Assert.Equal(ApplyResult.Applied, await repository.RevertAsync(commit, isMerge: false, cancellationToken: TestCancelled));
        }

        Assert.Equal(["Revert \"Add core\"", "Revert \"Main work\"", "Main work"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "src", "core.txt")));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.False(GitFileExists(repo, "sequencer"));
    }

    [Fact]
    public async Task A_revert_that_conflicts_stops_with_REVERT_HEAD_and_its_abort_returns()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var coreAgain = await repo.CommitFileAsync("src/core.txt", "core\nmain work again\n", "Core again");

        Assert.Equal(ApplyResult.Conflicts, await repository.RevertAsync(mainWork, isMerge: false, cancellationToken: TestCancelled));

        Assert.Equal(mainWork, ReadGitFile(repo, "REVERT_HEAD").Trim());
        Assert.Equal($"Revert \"Main work\"\n\nThis reverts commit {mainWork}.\n\n# Conflicts:\n#\tsrc/core.txt\n", ReadGitFile(repo, "MERGE_MSG"));
        Assert.Equal($"core\n<<<<<<< HEAD\nmain work again\n=======\n>>>>>>> parent of {mainWork[..7]} (Main work)\n", ReadText(repo, "src/core.txt"));
        Assert.Equal(
            new InProgressState(InProgressKind.Revert, mainWork, "Main work", null, null, 0, false) { HasPickHead = true },
            (await repository.ReadStatusAsync(TestCancelled)).InProgress);

        var changes = await repository.SnapshotTrackedChangesAsync(TestCancelled);
        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("abort", [new BackupEntry(null, coreAgain)]) { ChangesCommit = changes }, TestCancelled);
        await repository.AbortAsync(InProgressKind.Revert, cancellationToken: TestCancelled);

        Assert.Equal(coreAgain, await repo.HeadAsync());
        Assert.Equal("core\nmain work again\n", ReadText(repo, "src/core.txt"));
        Assert.False(GitFileExists(repo, "REVERT_HEAD"));
        Assert.Equal(2, backup.Refs.Count);
        Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task A_revert_that_changes_nothing_is_empty_only_while_git_keeps_it_in_progress()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        Assert.Equal(ApplyResult.Applied, await repository.RevertAsync(mainWork, isMerge: false, cancellationToken: TestCancelled));
        var reverted = await repo.HeadAsync();

        // Git 2.36 fails with "nothing to commit" and leaves no REVERT_HEAD: nothing is in progress
        // and nothing to abort, so it is a failure with git's text. Should a git version keep the
        // revert in progress, as cherry-pick does, it is an empty revert to abort.
        ApplyResult? result = null;
        GitException? failure = null;
        try
        {
            result = await repository.RevertAsync(mainWork, isMerge: false, cancellationToken: TestCancelled);
        }
        catch (GitException ex)
        {
            failure = ex;
        }

        Assert.Equal(reverted, await repo.HeadAsync());
        if (GitFileExists(repo, "REVERT_HEAD"))
        {
            Assert.Equal(ApplyResult.Empty, result);
            Assert.True((await repository.ReadStatusAsync(TestCancelled)).InProgress.Empty);
        }
        else
        {
            Assert.NotNull(failure);
            Assert.Contains("nothing to commit", failure.StandardOutput + failure.StandardError, StringComparison.Ordinal);
            Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
        }
    }

    // ---- Lists with git's sequencer (phase 4 design section 4) -------------------------------------

    [Fact]
    public async Task A_list_of_picks_is_one_git_call_that_applies_every_commit_oldest_first()
    {
        using var repo = await Scenarios.OperationsAsync();
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var alphaOne = await RevAsync(repo, "refs/heads/feature/alpha~1");
        var alphaTwo = await RevAsync(repo, "refs/heads/feature/alpha");

        var result = await repository.CherryPickAsync([alphaOne, alphaTwo], cancellationToken: TestCancelled);

        // Exit 0: None, every commit applied.
        Assert.Equal(new SequenceResult(SequenceStop.None, 2, 2), result);
        Assert.Equal([$"git cherry-pick {alphaOne} {alphaTwo}"], calls.Snapshot().Select(call => call.CommandText).Where(text => text.StartsWith("git cherry-pick", StringComparison.Ordinal)));
        Assert.Equal(["Alpha two", "Alpha one", "Main work"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
        Assert.Equal(mainWork, await RevAsync(repo, "HEAD~2"));
        Assert.Equal(Scenarios.OperationsFiles.AlphaTwo, ReadText(repo, "src/alpha.txt"));
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task A_list_of_reverts_is_one_git_call_newest_first()
    {
        using var repo = await Scenarios.OperationsAsync();
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var addCore = await RevAsync(repo, "refs/heads/main~1");

        var result = await repository.RevertAsync([mainWork, addCore], cancellationToken: TestCancelled);

        Assert.Equal(new SequenceResult(SequenceStop.None, 2, 2), result);
        Assert.Equal([$"git revert --no-edit {mainWork} {addCore}"], calls.Snapshot().Select(call => call.CommandText).Where(text => text.StartsWith("git revert", StringComparison.Ordinal)));
        Assert.Equal(["Revert \"Add core\"", "Revert \"Main work\"", "Main work"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "src", "core.txt")));
        Assert.False(GitFileExists(repo, "sequencer"));
    }

    [Fact]
    public async Task A_list_that_conflicts_stops_with_its_pick_head_and_gits_list()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var alphaOne = await RevAsync(repo, "refs/heads/feature/alpha~1");
        var conflicting = await RevAsync(repo, "refs/heads/feature/conflict");
        var alphaTwo = await RevAsync(repo, "refs/heads/feature/alpha");

        var result = await repository.CherryPickAsync([alphaOne, conflicting, alphaTwo], cancellationToken: TestCancelled);

        // A pick head and a conflicted file: Conflicts. Alpha one is applied; the stopped pick is
        // still the todo's first line, so 3 less 2 lines.
        Assert.Equal(new SequenceResult(SequenceStop.Conflicts, 1, 3), result);
        Assert.Equal(conflicting, ReadGitFile(repo, "CHERRY_PICK_HEAD").Trim());
        Assert.Equal(2, ReadGitFile(repo, "sequencer/todo").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(["Alpha one", "Main work"], await GitLinesAsync(repo, "log", "--format=%s", "-2"));
        Assert.Equal(["UU src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(
            new InProgressState(InProgressKind.CherryPick, conflicting, "Conflicting change", null, null, 1, false) { Sequenced = true, HasPickHead = true, Done = 1, Total = 3 },
            (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task A_list_of_reverts_that_conflicts_stops_with_REVERT_HEAD()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var addCore = await RevAsync(repo, "refs/heads/main~1");
        await repo.CommitFileAsync("src/core.txt", Scenarios.OperationsFiles.CoreAgain, "Core again");

        var result = await repository.RevertAsync([mainWork, addCore], cancellationToken: TestCancelled);

        Assert.Equal(new SequenceResult(SequenceStop.Conflicts, 0, 2), result);
        Assert.Equal(mainWork, ReadGitFile(repo, "REVERT_HEAD").Trim());
        Assert.StartsWith("revert ", ReadGitFile(repo, "sequencer/todo"), StringComparison.Ordinal);
        Assert.Equal(
            new InProgressState(InProgressKind.Revert, mainWork, "Main work", null, null, 1, false) { Sequenced = true, HasPickHead = true, Done = 0, Total = 2 },
            (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task A_pick_in_a_list_that_changes_nothing_stops_empty()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var alphaOne = await RevAsync(repo, "refs/heads/feature/alpha~1");
        var alphaTwo = await RevAsync(repo, "refs/heads/feature/alpha");
        await repo.GitAsync("cherry-pick", alphaOne);
        var head = await repo.HeadAsync();

        var result = await repository.CherryPickAsync([alphaOne, alphaTwo], cancellationToken: TestCancelled);

        // A pick head, nothing conflicted, the index equal to HEAD: Empty.
        Assert.Equal(new SequenceResult(SequenceStop.Empty, 0, 2), result);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(alphaOne, ReadGitFile(repo, "CHERRY_PICK_HEAD").Trim());
        Assert.True(GitFileExists(repo, "sequencer"));
        Assert.Equal(
            new InProgressState(InProgressKind.CherryPick, alphaOne, "Alpha one", null, null, 1, true) { Sequenced = true, HasPickHead = true, Done = 0, Total = 2 },
            (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task A_pick_in_a_list_that_rerere_resolved_stops_with_no_conflicted_file()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "base\n", "Base");
        await repo.CreateBranchAsync("side");
        var mainTip = await repo.CommitFileAsync("a.txt", "main\n", "Main change");
        await repo.CheckoutAsync("side");
        var sideChange = await repo.CommitFileAsync("a.txt", "side\n", "Side change");
        var sideMore = await repo.CommitFileAsync("b.txt", "more\n", "Side more");
        await repo.CheckoutAsync("main");
        await repo.ConfigAsync("rerere.enabled", "true");
        await repo.ConfigAsync("rerere.autoUpdate", "true");

        // The resolution recorded once, then forgotten.
        await repo.GitAllowingFailureAsync("cherry-pick", sideChange);
        repo.WriteFile("a.txt", "resolved\n");
        await repo.GitAsync("add", "a.txt");
        await repo.GitAsync("commit", "--quiet", "--no-edit");
        await repo.GitAsync("reset", "--quiet", "--hard", mainTip);
        var repository = await OpenAsync(repo);

        var result = await repository.CherryPickAsync([sideChange, sideMore], cancellationToken: TestCancelled);

        // A pick head, nothing conflicted, the index not equal to HEAD: Conflicts with 0 files (D128).
        Assert.Equal(new SequenceResult(SequenceStop.Conflicts, 0, 2), result);
        Assert.Equal(["M  a.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(sideChange, ReadGitFile(repo, "CHERRY_PICK_HEAD").Trim());
        Assert.True(GitFileExists(repo, "sequencer"));
    }

    [Fact]
    public async Task An_empty_revert_in_a_list_stops_empty_with_or_without_REVERT_HEAD()
    {
        var (repo, commits) = await ThreeCommitsAsync();
        using var _repo = repo;
        var (a, b, c) = commits;
        await repo.GitAsync("revert", "--no-edit", b);
        var repository = await OpenAsync(repo);

        var result = await repository.RevertAsync([c, b, a], cancellationToken: TestCancelled);

        // C reverted; B's revert changes nothing: Empty. Git 2.36 keeps the list and MERGE_MSG but
        // writes no REVERT_HEAD (0.3, D117); a git that keeps REVERT_HEAD stops as an empty pick does.
        Assert.Equal(new SequenceResult(SequenceStop.Empty, 1, 3), result);
        var pickHead = GitFileExists(repo, "REVERT_HEAD");
        if (await GitVersionAsync(repo) == new GitVersion(2, 36, 0))
        {
            Assert.False(pickHead);
        }

        Assert.StartsWith("Revert \"B adds b\"", ReadGitFile(repo, "MERGE_MSG"), StringComparison.Ordinal);
        Assert.StartsWith($"revert {b[..7]}", ReadGitFile(repo, "sequencer/todo"), StringComparison.Ordinal);
        Assert.Equal(["Revert \"C adds c\"", "Revert \"B adds b\""], await GitLinesAsync(repo, "log", "--format=%s", "-2"));
        Assert.Equal(
            new InProgressState(InProgressKind.Revert, b, "B adds b", null, null, 1, true) { Sequenced = true, HasPickHead = pickHead, Done = 1, Total = 3 },
            (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task An_empty_revert_first_in_a_list_stops_where_the_list_started()
    {
        var (repo, commits) = await ThreeCommitsAsync();
        using var _repo = repo;
        var (_, b, c) = commits;
        await repo.GitAsync("revert", "--no-edit", b);
        var head = await repo.HeadAsync();
        var repository = await OpenAsync(repo);

        var result = await repository.RevertAsync([b, c], cancellationToken: TestCancelled);

        // HEAD is still sequencer/head, as at a refused start: MERGE_MSG tells them apart.
        Assert.Equal(new SequenceResult(SequenceStop.Empty, 0, 2), result);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(head, ReadGitFile(repo, "sequencer/head").Trim());
        Assert.Equal(head, ReadGitFile(repo, "sequencer/abort-safety").Trim());
        Assert.True(GitFileExists(repo, "MERGE_MSG"));
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal(
            new InProgressState(InProgressKind.Revert, b, "B adds b", null, null, 1, true) { Sequenced = true, HasPickHead = GitFileExists(repo, "REVERT_HEAD"), Done = 0, Total = 2 },
            state);
    }

    [Fact]
    public async Task A_list_refused_at_its_start_is_quit_and_keeps_the_staged_change()
    {
        using var repo = await Scenarios.OperationsAsync();
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var head = await repo.HeadAsync();
        var alphaOne = await RevAsync(repo, "refs/heads/feature/alpha~1");
        var alphaTwo = await RevAsync(repo, "refs/heads/feature/alpha");
        repo.WriteFile("scratch.txt", "staged\n");
        await repo.GitAsync("add", "scratch.txt");

        var error = await Assert.ThrowsAsync<GitException>(() => repository.CherryPickAsync([alphaOne, alphaTwo], cancellationToken: TestCancelled));

        // Git's refusal is reported; the list git wrote anyway is quit, not aborted (D145).
        Assert.Equal($"git cherry-pick {alphaOne} {alphaTwo}", error.CommandText);
        Assert.Contains("fatal: cherry-pick failed", error.StandardError, StringComparison.Ordinal);
        Assert.Contains("git cherry-pick --quit", calls.Snapshot().Select(call => call.CommandText));
        Assert.DoesNotContain("git cherry-pick --abort", calls.Snapshot().Select(call => call.CommandText));
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(["A  scratch.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task A_list_of_reverts_refused_by_a_local_change_is_quit_and_keeps_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var head = await repo.HeadAsync();
        var mainWork = await RevAsync(repo, "refs/heads/main");
        var addCore = await RevAsync(repo, "refs/heads/main~1");
        repo.WriteFile("src/core.txt", "core\nmain work\nlocal\n");

        var error = await Assert.ThrowsAsync<GitException>(() => repository.RevertAsync([mainWork, addCore], cancellationToken: TestCancelled));

        Assert.Contains("would be overwritten", error.StandardError, StringComparison.Ordinal);
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.False(GitFileExists(repo, "REVERT_HEAD"));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal([" M src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal("core\nmain work\nlocal\n", ReadText(repo, "src/core.txt"));
    }

    [Fact]
    public async Task A_later_commit_git_cannot_apply_stops_the_list_as_refused()
    {
        var (repo, untracked) = await UntrackedInTheWayAsync();
        using var _repo = repo;
        var (a, b, c) = untracked;
        var repository = await OpenAsync(repo);
        var start = await repo.HeadAsync();

        var result = await repository.CherryPickAsync([a, b, c], cancellationToken: TestCancelled);

        // No pick head, no MERGE_MSG, the list kept with B first, HEAD moved by A: Refused.
        Assert.Equal(new SequenceResult(SequenceStop.Refused, 1, 3), result);
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.False(GitFileExists(repo, "MERGE_MSG"));
        Assert.StartsWith($"pick {b[..7]}", ReadGitFile(repo, "sequencer/todo"), StringComparison.Ordinal);
        Assert.Equal(start, ReadGitFile(repo, "sequencer/head").Trim());
        Assert.Equal(await repo.HeadAsync(), ReadGitFile(repo, "sequencer/abort-safety").Trim());
        Assert.Equal("untracked\n", ReadText(repo, "u.txt"));
        Assert.Equal(
            new InProgressState(InProgressKind.CherryPick, b, "B adds u", null, null, 1, false) { Sequenced = true, Done = 1, Total = 3 },
            (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task A_list_over_conflicts_already_there_is_refused_and_leaves_no_list()
    {
        // Conflicts from a stash, with no operation in progress.
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.None);
        var repository = await OpenAsync(repo);
        var head = await repo.HeadAsync();
        var alphaOne = await RevAsync(repo, "refs/heads/feature/alpha~1");
        var alphaTwo = await RevAsync(repo, "refs/heads/feature/alpha");

        await Assert.ThrowsAsync<GitException>(() => repository.CherryPickAsync([alphaOne, alphaTwo], cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<GitException>(() => repository.RevertAsync([head, alphaOne], cancellationToken: TestCancelled));

        Assert.Equal(head, await repo.HeadAsync());
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.Equal(["UU src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task A_list_while_another_list_is_stopped_is_refused_and_leaves_that_list_alone()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var todo = ReadGitFile(repo, "sequencer/todo");
        var pickHead = ReadGitFile(repo, "CHERRY_PICK_HEAD");
        var head = await repo.HeadAsync();
        var mainWork = await RevAsync(repo, "refs/heads/main~1");

        var error = await Assert.ThrowsAsync<GitException>(() => repository.RevertAsync([head, mainWork], cancellationToken: TestCancelled));

        Assert.Equal(128, error.ExitCode);
        Assert.DoesNotContain(calls.Snapshot(), call => call.CommandText.EndsWith("--quit", StringComparison.Ordinal));
        Assert.Equal(todo, ReadGitFile(repo, "sequencer/todo"));
        Assert.Equal(pickHead, ReadGitFile(repo, "CHERRY_PICK_HEAD"));
        Assert.Equal(head, await repo.HeadAsync());
    }

    [Fact]
    public async Task A_list_of_one_commit_that_stops_has_no_list_and_none_applied()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var conflicting = await RevAsync(repo, "refs/heads/feature/conflict");

        var result = await repository.CherryPickAsync([conflicting], cancellationToken: TestCancelled);

        Assert.Equal(new SequenceResult(SequenceStop.Conflicts, 0, 1), result);
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.Equal(conflicting, ReadGitFile(repo, "CHERRY_PICK_HEAD").Trim());
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CherryPickAsync([], cancellationToken: TestCancelled));
    }

    [Fact]
    public async Task Quit_ends_a_list_and_keeps_HEAD_and_the_files()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        var repository = await OpenAsync(repo);
        var head = await repo.HeadAsync();
        var conflicted = ReadText(repo, "src/core.txt");

        await repository.QuitSequenceAsync(InProgressKind.CherryPick, cancellationToken: TestCancelled);

        // Git forgets the list and the stopped pick; HEAD and the conflicted file stay.
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(conflicted, ReadText(repo, "src/core.txt"));
        Assert.Equal(["UU src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.QuitSequenceAsync(InProgressKind.Merge, cancellationToken: TestCancelled));
    }

    /// <summary>Initial commit, then "A adds a", "B adds b", "C adds c" on main; returns the three commits.</summary>
    private static async Task<(TempRepo Repo, (string A, string B, string C) Commits)> ThreeCommitsAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
            var a = await repo.CommitFileAsync("a.txt", "a\n", "A adds a");
            var b = await repo.CommitFileAsync("b.txt", "b\n", "B adds b");
            var c = await repo.CommitFileAsync("c.txt", "c\n", "C adds c");
            return (repo, (a, b, c));
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// main with an untracked u.txt, and side with "A adds a", "B adds u" (u.txt, in the way of
    /// the untracked one) and "C adds c"; HEAD on main.
    /// </summary>
    private static async Task<(TempRepo Repo, (string A, string B, string C) Commits)> UntrackedInTheWayAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
            await repo.CreateBranchAsync("side");
            await repo.CheckoutAsync("side");
            var a = await repo.CommitFileAsync("a.txt", "a\n", "A adds a");
            var b = await repo.CommitFileAsync("u.txt", "tracked\n", "B adds u");
            var c = await repo.CommitFileAsync("c.txt", "c\n", "C adds c");
            await repo.CheckoutAsync("main");
            repo.WriteFile("u.txt", "untracked\n");
            return (repo, (a, b, c));
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    // ---- Abort ------------------------------------------------------------------------------------

    [Fact]
    public async Task Aborting_a_merge_from_a_terminal_returns_to_HEAD_and_its_backup_keeps_the_conflicted_file()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        var repository = await OpenAsync(repo);
        var mainWork = await repo.HeadAsync();
        var conflicted = ReadText(repo, "src/core.txt");

        var changes = await repository.SnapshotTrackedChangesAsync(TestCancelled);
        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("abort", [new BackupEntry(null, mainWork)]) { ChangesCommit = changes }, TestCancelled);
        await repository.AbortAsync(InProgressKind.Merge, cancellationToken: TestCancelled);

        Assert.Equal(mainWork, await repo.HeadAsync());
        Assert.False(GitFileExists(repo, "MERGE_HEAD"));
        Assert.Equal(["## main...origin/main"], await GitLinesAsync(repo, "status", "--short", "--branch"));
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, ReadText(repo, "src/core.txt"));

        var stem = $"refs/visualcommit/backup/abort-{backup.Time}";
        Assert.Equal([new RefEntry(stem, mainWork), new RefEntry(stem + "-changes", changes!)], await repo.BackupRefsAsync());
        Assert.Equal(mainWork, await GitTextAsync(repo, "log", "-1", "--format=%P", changes!));
        Assert.Equal(["README.md", "src/core.txt"], await TreeFilesAsync(repo, changes!));
        Assert.Equal(conflicted, await ShowFileAsync(repo, changes!, "src/core.txt"));
    }

    [Fact]
    public async Task Aborting_a_cherry_pick_sequence_from_a_terminal_drops_the_picks_it_made()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        var repository = await OpenAsync(repo);
        var alphaOneCopy = await repo.HeadAsync();
        var mainWork = await RevAsync(repo, "HEAD~1");

        var changes = await repository.SnapshotTrackedChangesAsync(TestCancelled);
        await repository.SaveBackupRefsAsync(new BackupRequest("abort", [new BackupEntry(null, alphaOneCopy)]) { ChangesCommit = changes }, TestCancelled);
        await repository.AbortAsync(InProgressKind.CherryPick, cancellationToken: TestCancelled);

        // Git rewinds to where the sequence started; the backup keeps the dropped pick.
        Assert.Equal(mainWork, await repo.HeadAsync());
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.Contains(await repo.BackupRefsAsync(), entry => entry.Sha == alphaOneCopy);
    }

    [Fact]
    public async Task Aborting_a_rebase_from_a_terminal_returns_the_branch_to_where_it_was()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Rebase);
        var repository = await OpenAsync(repo);
        var conflicting = await RevAsync(repo, "refs/heads/feature/conflict");

        await repository.AbortAsync(InProgressKind.Rebase, cancellationToken: TestCancelled);

        Assert.Equal("feature/conflict", await repo.CurrentBranchAsync());
        Assert.Equal(conflicting, await repo.HeadAsync());
        Assert.False(GitFileExists(repo, "rebase-merge"));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task Aborting_with_nothing_in_progress_throws_with_gits_text()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);

        await Assert.ThrowsAsync<GitException>(() => repository.AbortAsync(InProgressKind.Merge, cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.AbortAsync(InProgressKind.None, cancellationToken: TestCancelled));
    }
}
