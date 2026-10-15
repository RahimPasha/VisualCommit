using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.UndoTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Each recorded action of docs/phase-5-design.md 7.2 that moves HEAD, a branch or commits, made by
/// running the git command the app runs (the app's own <see cref="GitRepository"/> call), recorded
/// as the app records it, then undone and redone with <see cref="GitRepository.RunUndoStepAsync"/>
/// (7.3's sequences, 7.5's steps): the repository after the undo is as it was before the action,
/// and after the redo as the action left it (HEAD, refs, index entries, status, file bytes,
/// stashes, settings). The undone commits stay reachable through the reflogs, so each redo finds
/// them. Uncommitted work that the action did not touch is made before it and kept throughout.
/// </summary>
public class UndoStepTests
{
    [Fact]
    public async Task A_commit_is_undone_with_its_changes_staged_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        repo.WriteFile("a.txt", "a3\n").WriteFile("b.txt", "b-unstaged\n");
        await repo.GitAsync("add", "a.txt");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Commit("c4", amend: false, "main"), () => repository.CommitAsync("c4", amend: false, TestCancelled));
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.StartsWith("VisualCommit: undo commit \"c4\"", (await repo.GitAsync("log", "-g", "-1", "--format=%gs", "refs/heads/main")).StandardOutput, StringComparison.Ordinal);

        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
        Assert.StartsWith("VisualCommit: redo commit \"c4\"", (await repo.GitAsync("log", "-g", "-1", "--format=%gs", "HEAD")).StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_amend_is_undone_to_the_commit_it_amended()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        repo.WriteFile("b.txt", "b3\n");
        await repo.GitAsync("add", "b.txt");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Commit("c3 amended", amend: true, "main"), () => repository.CommitAsync("c3 amended", amend: true, TestCancelled));
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task The_initial_commit_is_undone_to_an_unborn_branch_with_its_index_kept()
    {
        using var repo = await TempRepo.CreateAsync("unborn");
        repo.WriteFile("x.txt", "x\n").WriteFile("y.txt", "y\n");
        await repo.GitAsync("add", "x.txt", "y.txt");
        var (repository, _) = await OpenDatedAsync(repo);
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Commit("first", amend: false, "main"), () => repository.CommitAsync("first", amend: false, TestCancelled));
        var done = await PictureAsync(repo);
        Assert.Null(entry.Before.Head.Sha);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Null(await TargetAsync(repo, "refs/heads/main"));

        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_soft_reset_moves_back_alone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var target = (await repo.GitAsync("rev-parse", "HEAD~2")).StandardOutput.Trim();
        repo.WriteFile("src/c.txt", "c-unstaged\n");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Reset("main", target, ResetMode.Soft), async () =>
        {
            UndoCapture.Reset(ResetMode.Soft, null);
            await repository.ResetAsync(target, ResetMode.Soft, cancellationToken: TestCancelled);
        });
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_mixed_reset_comes_back_with_its_staged_and_partly_staged_files()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var target = (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim();

        // A partly staged file, a staged new file and a staged deletion: the index snapshot keeps them.
        repo.WriteFile("a.txt", "a-staged\n");
        await repo.GitAsync("add", "a.txt");
        repo.WriteFile("a.txt", "a-unstaged\n").WriteFile("n.txt", "new\n");
        await repo.GitAsync("add", "n.txt");
        await repo.GitAsync("rm", "--quiet", "src/c.txt");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Reset("main", target, ResetMode.Mixed), async () =>
        {
            var head = await repository.ResolveAsync("HEAD", TestCancelled);
            var snapshot = await repository.SnapshotIndexAsync(TestCancelled);
            var backup = await repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, head!)]) { ChangesCommit = snapshot }, TestCancelled);
            UndoCapture.Backups(backup.Refs);
            UndoCapture.Reset(ResetMode.Mixed, snapshot);
            await repository.ResetAsync(target, ResetMode.Mixed, cancellationToken: TestCancelled);
        });
        var done = await PictureAsync(repo);
        Assert.NotNull(entry.Captured.ResetSnapshot);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_mixed_reset_of_a_clean_index_puts_the_index_back_from_the_commit()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var target = (await repo.GitAsync("rev-parse", "HEAD~2")).StandardOutput.Trim();
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Reset("main", target, ResetMode.Mixed), async () =>
        {
            var snapshot = await repository.SnapshotIndexAsync(TestCancelled);
            Assert.Null(snapshot);
            UndoCapture.Reset(ResetMode.Mixed, snapshot);
            await repository.ResetAsync(target, ResetMode.Mixed, cancellationToken: TestCancelled);
        });
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_hard_reset_comes_back_with_its_files_and_index_and_is_redone_with_a_new_snapshot()
    {
        using var repo = await UndoRepoAsync();
        var (repository, runner) = await OpenDatedAsync(repo);
        var target = (await repo.GitAsync("rev-parse", "HEAD~2")).StandardOutput.Trim();
        repo.WriteFile("a.txt", "a-staged\n");
        await repo.GitAsync("add", "a.txt");
        repo.WriteFile("a.txt", "a-unstaged\n").WriteFile("untracked.txt", "u\n").WriteFile("src/c.txt", "c-unstaged\n");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Reset("main", target, ResetMode.Hard), () => AppHardResetAsync(repository, target));
        var done = await PictureAsync(repo);
        Assert.NotNull(entry.Captured.ResetIndex);

        runner.Advance();
        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));

        // Edited between the undo and the redo: the redo's own snapshot keeps the edit.
        repo.WriteFile("src/c.txt", "c-edited after the undo\n");
        var edited = await PictureAsync(repo);
        runner.Advance();
        var redone = await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
        Assert.NotEqual(entry.Captured.ResetSnapshot, redone.Captured.ResetSnapshot);
        Assert.Contains(redone.Captured.Backups, backup => backup.EndsWith("-changes", StringComparison.Ordinal) && !entry.Captured.Backups.Contains(backup));

        await UndoAsync(repository, redone);
        Assert.Equal(edited, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_hard_reset_that_discarded_nothing_is_undone_by_the_move_alone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var target = (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim();
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Reset("main", target, ResetMode.Hard), () => AppHardResetAsync(repository, target));
        var done = await PictureAsync(repo);
        Assert.Null(entry.Captured.ResetSnapshot);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Theory]
    [InlineData(MergeMode.FastForwardOnly)]
    [InlineData(MergeMode.NoFastForward)]
    public async Task A_merge_is_undone_keeping_work_it_did_not_touch(MergeMode mode)
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);

        // The fast-forward takes behind to main (b.txt changes), the merge takes feature into
        // main (f.txt and src/c.txt): a.txt's unstaged change and an untracked file are in neither.
        var fastForward = mode == MergeMode.FastForwardOnly;
        if (fastForward)
        {
            await repo.CheckoutAsync("behind");
        }

        var source = fastForward ? "refs/heads/main" : "refs/heads/feature";
        var current = fastForward ? "behind" : "main";
        repo.WriteFile("a.txt", "a-unstaged\n").WriteFile("scratch.txt", "scratch\n");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Merge(source, current, fastForward), () => repository.MergeAsync(source, mode, cancellationToken: TestCancelled));
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_cherry_pick_of_a_list_and_a_revert_are_undone_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var f1 = (await repo.GitAsync("rev-parse", "feature~1")).StandardOutput.Trim();
        var f2 = (await repo.GitAsync("rev-parse", "feature")).StandardOutput.Trim();
        repo.WriteFile("scratch.txt", "scratch\n");
        var start = await PictureAsync(repo);

        var pick = await RecordAsync(repository, UndoScope.CherryPick([f1, f2], "main"), () => repository.CherryPickAsync([f1, f2], cancellationToken: TestCancelled));
        var picked = await PictureAsync(repo);
        pick = await UndoAsync(repository, pick);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, pick);
        Assert.Equal(picked, await PictureAsync(repo));

        var c3 = (await repo.GitAsync("rev-parse", "HEAD~2")).StandardOutput.Trim();
        var revert = await RecordAsync(repository, UndoScope.Revert([c3], "main"), () => repository.RevertAsync(c3, isMerge: false, cancellationToken: TestCancelled));
        var reverted = await PictureAsync(repo);
        revert = await UndoAsync(repository, revert);
        Assert.Equal(picked, await PictureAsync(repo));
        await RedoAsync(repository, revert);
        Assert.Equal(reverted, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_commit_and_a_cherry_pick_on_a_detached_HEAD_move_HEAD_alone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var main = (await repo.GitAsync("rev-parse", "main")).StandardOutput.Trim();
        await repo.GitAsync("checkout", "--quiet", "--detach", "HEAD~1");
        repo.WriteFile("a.txt", "a3\n").WriteFile("scratch.txt", "scratch\n");
        await repo.GitAsync("add", "a.txt");
        var start = await PictureAsync(repo);

        // The commit's undo is a soft move of the detached HEAD: its change comes back staged.
        var commit = await RecordAsync(repository, UndoScope.Commit("d1", amend: false, null), () => repository.CommitAsync("d1", amend: false, TestCancelled));
        var committed = await PictureAsync(repo);
        commit = await UndoAsync(repository, commit);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, commit);
        Assert.Equal(committed, await PictureAsync(repo));

        // The pick's is a Keep move: the files go back, scratch.txt stays.
        var f1 = (await repo.GitAsync("rev-parse", "feature~1")).StandardOutput.Trim();
        var pick = await RecordAsync(repository, UndoScope.CherryPick([f1], null), () => repository.CherryPickAsync([f1], cancellationToken: TestCancelled));
        var picked = await PictureAsync(repo);
        pick = await UndoAsync(repository, pick);
        Assert.Equal(committed, await PictureAsync(repo));
        await RedoAsync(repository, pick);
        Assert.Equal(picked, await PictureAsync(repo));
        Assert.Equal(main, await TargetAsync(repo, "refs/heads/main"));
    }

    [Fact]
    public async Task A_rebase_is_undone_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.CheckoutAsync("feature");
        repo.WriteFile("scratch.txt", "scratch\n");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Rebase("feature", "main"), () => repository.RebaseAsync("refs/heads/main", cancellationToken: TestCancelled));
        var done = await PictureAsync(repo);
        Assert.NotEqual(entry.Before.Head.Sha, entry.After.Head.Sha);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task Check_out_and_merge_moves_the_branch_back_then_switches_back_and_its_redo_does_the_mirror()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        repo.WriteFile("scratch.txt", "scratch\n");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.CheckoutAndMerge("behind", "refs/heads/feature", "main"), async () =>
        {
            await repository.CheckoutBranchAsync("behind", cancellationToken: TestCancelled);
            await repository.MergeAsync("refs/heads/feature", MergeMode.NoFastForward, cancellationToken: TestCancelled);
        });
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task Check_out_and_rebase_is_undone_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.CheckoutAndRebase("feature", "main"), async () =>
        {
            await repository.CheckoutBranchAsync("feature", cancellationToken: TestCancelled);
            await repository.RebaseAsync("refs/heads/main", cancellationToken: TestCancelled);
        });
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    /// <summary>The app's Reset with the hard mode (2.11): its snapshot and backups, then the reset; the capture as the app's operation reports it.</summary>
    internal static async Task AppHardResetAsync(GitRepository repository, string target)
    {
        var head = await repository.ResolveAsync("HEAD", TestCancelled);
        var snapshot = await repository.SnapshotForHardResetAsync(target, TestCancelled);
        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, head!)]) { ChangesCommit = snapshot }, TestCancelled);
        UndoCapture.Backups(backup.Refs);
        if (snapshot is null)
        {
            UndoCapture.Reset(ResetMode.Hard, null);
        }
        else
        {
            var files = await repository.ReadUndoSnapshotAsync(backup.Refs[^1], snapshot, TestCancelled);
            UndoCapture.Reset(ResetMode.Hard, snapshot, await repository.ResolveAsync(snapshot + "^2", TestCancelled), files);
            UndoCapture.FilesChanged(files.Paths);
        }

        await repository.ResetAsync(target, ResetMode.Hard, cancellationToken: TestCancelled);
    }
}
