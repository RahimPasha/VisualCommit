using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Phase 3's history operations against real git on the scenario repos (design 2.11 to 2.15):
/// reset in each mode with its backup refs, cherry-pick and revert one commit at a time (several
/// as the app runs them: one call per commit, stopping at the first that does not apply cleanly,
/// 2.13), and the abort of every state. Expected states are read back with plain git.
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
