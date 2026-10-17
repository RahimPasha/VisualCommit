using System.Text;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.UndoTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// docs/phase-5-design.md 7.3's sequences for the stash and the files, each action made by the git
/// call the app makes and recorded as the app records it: a stash (undone, redone with a new
/// stash commit as the clock moves on, undone again), an apply, a pop, a pop that conflicted, a
/// drop, the discards of D62 (whole files, untracked ones included, and lines), the restore bar's
/// Restore, an ignore, and the backups list's restores of 7.10.
/// </summary>
public class UndoStepFileTests
{
    [Fact]
    public async Task A_stash_is_popped_back_and_redone_as_a_new_stash_the_entry_then_follows()
    {
        using var repo = await UndoRepoAsync();
        var (repository, runner) = await OpenDatedAsync(repo);
        repo.WriteFile("a.txt", "a-staged\n");
        await repo.GitAsync("add", "a.txt");
        repo.WriteFile("a.txt", "a-unstaged\n").WriteFile("n.txt", "new\n").WriteFile("notes/u.txt", "untracked\n");
        await repo.GitAsync("add", "n.txt");
        await repo.GitAsync("rm", "--quiet", "b.txt");
        var start = await PictureAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Stash("wip"), async () =>
        {
            var stash = await repository.StashAsync("wip", includeUntracked: true, cancellationToken: TestCancelled);
            UndoCapture.Stash(stash, includedUntracked: true, "wip");
        });
        var done = await PictureAsync(repo);

        runner.Advance();
        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));

        // An edit between the undo and the redo would make the redo stash something else: refused.
        repo.WriteFile("scratch.txt", "scratch\n");
        Assert.Equal(UndoRefusalKind.StatusChanged, (await RefusedAsync(repository, entry, UndoDirection.Redo)).Kind);
        File.Delete(Path.Combine(repo.Path, "scratch.txt"));

        runner.Advance();
        var redone = await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
        Assert.NotEqual(entry.Captured.Stash, redone.Captured.Stash);
        Assert.Equal(redone.Captured.Stash, await TargetAsync(repo, "refs/stash"));
        Assert.Equal(redone.Captured.Stash, redone.After.Stashes[0].Sha);

        runner.Advance();
        await UndoAsync(repository, redone);
        Assert.Equal(start, await PictureAsync(repo));
    }

    [Fact]
    public async Task An_apply_puts_the_files_and_index_entries_back()
    {
        using var repo = await UndoRepoAsync();
        var (repository, stash, message) = await StashToApplyAsync(repo);
        var start = await PictureAsync(repo);

        var apply = await RecordAsync(repository, UndoScope.ApplyStash(stash, message), () => repository.ApplyStashAsync(stash, StashApplyMode.Apply, cancellationToken: TestCancelled));
        var applied = await PictureAsync(repo);
        Assert.Contains(apply.Before.Files, file => file.Path == "notes/u.txt" && !file.Exists);
        apply = await UndoAsync(repository, apply);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, apply);
        Assert.Equal(applied, await PictureAsync(repo));
        await UndoAsync(repository, apply);
        Assert.Equal(start, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_pop_puts_the_files_and_index_entries_and_its_stash_back()
    {
        using var repo = await UndoRepoAsync();
        var (repository, stash, message) = await StashToApplyAsync(repo);
        var start = await PictureAsync(repo);

        var pop = await RecordAsync(repository, UndoScope.PopStash(stash, message), () => repository.ApplyStashAsync(stash, StashApplyMode.PopKeepingIndex, cancellationToken: TestCancelled));
        var popped = await PictureAsync(repo);
        Assert.Empty(await StashShasAsync(repo));
        pop = await UndoAsync(repository, pop);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal([stash], await StashShasAsync(repo));
        await RedoAsync(repository, pop);
        Assert.Equal(popped, await PictureAsync(repo));
    }

    /// <summary>
    /// Opens <paramref name="repo"/> (the undo repository) and makes a stash of a changed
    /// <c>a.txt</c> and an untracked <c>notes/u.txt</c>, then changes <c>b.txt</c>: for an apply or
    /// a pop. Returns the repository, the stash and its message as the list shows it.
    /// </summary>
    private static async Task<(GitRepository Repository, string Stash, string Message)> StashToApplyAsync(TempRepo repo)
    {
        var (repository, _) = await OpenDatedAsync(repo);
        repo.WriteFile("a.txt", "a-stashed\n").WriteFile("notes/u.txt", "untracked\n");
        var stash = (await repository.StashAsync("saved", includeUntracked: true, cancellationToken: TestCancelled))!;
        var message = (await repo.StashListAsync())[0].Message;
        repo.WriteFile("b.txt", "b-unstaged\n");
        return (repository, stash, message);
    }

    [Fact]
    public async Task A_pop_that_conflicted_and_kept_its_stash_is_undone_with_its_conflict_as_an_apply()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        repo.WriteFile("a.txt", "a-stashed\n");
        var stash = (await repository.StashAsync("saved", includeUntracked: false, cancellationToken: TestCancelled))!;
        var message = (await repo.StashListAsync())[0].Message;
        await repo.CommitFileAsync("a.txt", "a-committed\n", "c4");
        var start = await PictureAsync(repo);

        var pop = await RecordAsync(repository, UndoScope.PopStash(stash, message), () => repository.ApplyStashAsync(stash, StashApplyMode.Pop, cancellationToken: TestCancelled));
        Assert.Contains("UU a.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput, StringComparison.Ordinal);
        var conflicted = await PictureAsync(repo);

        // The conflict is the apply's own: its undo is not refused for it (7.4).
        pop = await UndoAsync(repository, pop);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, pop);
        Assert.Equal(conflicted, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_dropped_stash_is_stored_again_with_its_message()
    {
        using var repo = await UndoRepoAsync();
        var (repository, top) = await TwoStashesAsync(repo);
        var start = await PictureAsync(repo);

        var drop = await RecordAsync(repository, UndoScope.DropStash(top.Message), () => DropAsync(repository, top));
        var dropped = await PictureAsync(repo);
        drop = await UndoAsync(repository, drop);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, drop);
        Assert.Equal(dropped, await PictureAsync(repo));
    }

    [Fact]
    public async Task The_backups_lists_restore_of_a_dropped_stash_is_undone_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, top) = await TwoStashesAsync(repo);
        var start = await PictureAsync(repo);
        await DropAsync(repository, top);
        var dropped = await PictureAsync(repo);

        // The backups list's "Restore stash": stored again with its commit's subject (7.10).
        var restore = await RecordAsync(repository, UndoScope.RestoreStash(top.Message), () => repo.GitAsync("stash", "store", "-m", top.Message, top.Sha));
        restore = await UndoAsync(repository, restore);
        Assert.Equal(dropped, await PictureAsync(repo));
        await RedoAsync(repository, restore);
        Assert.Equal(start, await PictureAsync(repo));
    }

    /// <summary>
    /// Opens <paramref name="repo"/> (the undo repository) and makes two stashes of <c>a.txt</c>,
    /// "older" and "newer". Returns the repository and the newer stash, the list's first.
    /// </summary>
    private static async Task<(GitRepository Repository, StashListEntry Top)> TwoStashesAsync(TempRepo repo)
    {
        var (repository, _) = await OpenDatedAsync(repo);
        repo.WriteFile("a.txt", "a-older\n");
        await repository.StashAsync("older", includeUntracked: false, cancellationToken: TestCancelled);
        repo.WriteFile("a.txt", "a-newer\n");
        await repository.StashAsync("newer", includeUntracked: false, cancellationToken: TestCancelled);
        return (repository, (await repo.StashListAsync())[0]);
    }

    /// <summary>Drops <paramref name="stash"/> as the app does: its backup saved first, and named to the undo capture.</summary>
    private static async Task DropAsync(GitRepository repository, StashListEntry stash)
    {
        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("stash", [new BackupEntry(null, stash.Sha)]), TestCancelled);
        UndoCapture.Backups(backup.Refs);
        await repository.DropStashAsync(stash.Sha, TestCancelled);
    }

    [Fact]
    public async Task A_discard_of_files_comes_back_from_its_snapshot_and_its_redo_writes_the_bytes_the_discard_left()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        repo.WriteBytes("a.txt", Encoding.ASCII.GetBytes("a-crlf\r\nline\r\n")).WriteFile("notes/u.txt", "untracked\n");
        var start = await PictureAsync(repo);
        IReadOnlyList<string> paths = ["a.txt", "notes/u.txt"];

        var discard = await RecordAsync(repository, UndoScope.Discard(paths), async () =>
        {
            var snapshot = await repository.SaveSnapshotAsync(paths, TestCancelled);
            UndoCapture.Snapshot(snapshot);
            await repository.DiscardAsync(paths, new HashSet<string>(StringComparer.Ordinal) { "notes/u.txt" }, TestCancelled);
        });
        var discarded = await PictureAsync(repo);

        discard = await UndoAsync(repository, discard);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, discard);
        Assert.Equal(discarded, await PictureAsync(repo));
        Assert.False(Directory.Exists(Path.Combine(repo.Path, "notes")));
    }

    /// <summary>
    /// The finding of step 25's review: the recording copied a discarded file's raw bytes into
    /// the object store before and after the discard, which under a filter (here
    /// <c>core.autocrlf</c>; Git LFS alike) are blobs of their own, kept until a gc. Now it keeps
    /// their SHA-256 only; the undo keeps the bytes the discard left, which its redo writes back.
    /// </summary>
    [Fact]
    public async Task A_discard_of_a_filtered_file_copies_no_raw_bytes_until_its_undo_and_its_redo_writes_them_back()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.ConfigAsync("core.autocrlf", "true");
        repo.WriteBytes("a.txt", Encoding.ASCII.GetBytes("a-work\r\nmore\r\n"));
        var worked = await RawBlobAsync(repo, "a.txt");
        var start = await PictureAsync(repo);

        var discard = await RecordAsync(repository, UndoScope.Discard(["a.txt"]), async () =>
        {
            UndoCapture.Snapshot(await repository.SaveSnapshotAsync(["a.txt"], TestCancelled));
            await repository.DiscardAsync(["a.txt"], new HashSet<string>(StringComparer.Ordinal), TestCancelled);
        });
        var discarded = await PictureAsync(repo);
        var left = await RawBlobAsync(repo, "a.txt");
        Assert.NotEqual(worked, left);
        Assert.All(discard.Before.Files.Concat(discard.After.Files), file => Assert.True(file.Exists && file.Blob is null, $"{file} kept its bytes"));
        Assert.False(await HasObjectAsync(repo, worked));
        Assert.False(await HasObjectAsync(repo, left));

        discard = await UndoAsync(repository, discard);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal(left, discard.After.Files.Single().Blob);
        Assert.True(await HasObjectAsync(repo, left));

        await RedoAsync(repository, discard);
        Assert.Equal(discarded, await PictureAsync(repo));
    }

    /// <summary>The id <c>git hash-object --no-filters</c> gives a working-tree file's raw bytes, without writing it.</summary>
    private static async Task<string> RawBlobAsync(TempRepo repo, string path) =>
        (await repo.GitAsync("hash-object", "--no-filters", "--", path)).StandardOutput.Trim();

    /// <summary>Whether the object store has <paramref name="id"/> (<c>git cat-file -e</c>).</summary>
    private static async Task<bool> HasObjectAsync(TempRepo repo, string id) =>
        (await repo.GitAllowingFailureAsync("cat-file", "-e", id)).ExitCode == 0;

    [Fact]
    public async Task A_discard_of_lines_keeps_the_files_other_changes_both_ways()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var lines = string.Concat(Enumerable.Range(1, 12).Select(line => $"line {line}\n"));
        await repo.CommitFileAsync("lines.txt", lines, "c4");
        repo.WriteFile("lines.txt", lines.Replace("line 1\n", "first\n", StringComparison.Ordinal).Replace("line 12\n", "last\n", StringComparison.Ordinal));
        var start = await PictureAsync(repo);

        // The first hunk alone, reversed into the working tree, as the diff view's line discard does.
        var diff = (await repo.GitAsync("diff", "--", "lines.txt")).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal);
        var second = diff.IndexOf("@@", diff.IndexOf("@@", diff.IndexOf("@@", StringComparison.Ordinal) + 2, StringComparison.Ordinal) + 2, StringComparison.Ordinal);
        var patch = diff[..second];
        var entry = await RecordAsync(repository, UndoScope.DiscardLines("lines.txt"), async () =>
        {
            UndoCapture.Snapshot(await repository.SaveSnapshotAsync(["lines.txt"], TestCancelled));
            await repository.ApplyPatchAsync(patch, toIndex: false, PatchDirection.Reverse, TestCancelled);
        });
        var done = await PictureAsync(repo);
        Assert.Contains("last\n", ReadText(repo, "lines.txt"), StringComparison.Ordinal);
        Assert.DoesNotContain("first\n", ReadText(repo, "lines.txt"), StringComparison.Ordinal);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task The_restore_bars_restore_is_undone_by_the_bytes_it_overwrote()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        repo.WriteFile("a.txt", "a-work\n");
        var snapshot = await repository.SaveSnapshotAsync(["a.txt"], TestCancelled);
        await repository.DiscardAsync(["a.txt"], new HashSet<string>(StringComparer.Ordinal), TestCancelled);
        repo.WriteFile("a.txt", "a-typed since the discard\n");
        var start = await PictureAsync(repo);

        var restore = await RecordAsync(repository, UndoScope.Restore(["a.txt"]), async () =>
        {
            UndoCapture.Snapshot(snapshot);
            await repository.RestoreSnapshotAsync(snapshot, TestCancelled);
        });
        var done = await PictureAsync(repo);
        Assert.Equal("a-work\n", ReadText(repo, "a.txt"));

        restore = await UndoAsync(repository, restore);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, restore);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_ignore_is_cut_back_off_the_gitignore_or_the_file_it_made_deleted(bool existed)
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        if (existed)
        {
            // No line feed at the end: the append starts with one.
            repo.WriteFile(".gitignore", "*.tmp");
        }

        repo.WriteFile("build.log", "log\n");
        var start = await PictureAsync(repo);
        var path = Path.Combine(repo.Path, ".gitignore");
        var length = existed ? new FileInfo(path).Length : 0;

        // The recorder reads the bytes appended itself, after the append (7.7 step 3).
        var entry = await RecordAsync(repository, UndoScope.Ignore("*.log"), () => repository.AppendToGitignoreAsync("*.log", TestCancelled));
        var bytes = File.ReadAllBytes(path);
        entry = entry with { Captured = entry.Captured with { GitignoreAppended = Encoding.Latin1.GetString(bytes, (int)length, bytes.Length - (int)length) } };
        var done = await PictureAsync(repo);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal(existed, File.Exists(path));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
    }

    [Fact]
    public async Task The_backups_lists_restore_of_a_mixed_resets_index_is_undone_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var head = (await repo.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim();

        // A mixed reset's index snapshot, put back while the index equals HEAD (7.10).
        repo.WriteFile("a.txt", "a-staged\n");
        await repo.GitAsync("add", "a.txt");
        var index = (await repository.SnapshotIndexAsync(TestCancelled))!;
        await repository.ResetAsync(head, ResetMode.Mixed, cancellationToken: TestCancelled);
        var start = await PictureAsync(repo);
        var staged = await RecordAsync(repository, UndoScope.RestoreStagedChanges(), async () =>
        {
            await repo.GitAsync("restore", "--staged", $"--source={index}", "--", ".");
            UndoCapture.IndexRestored(index);
        });
        var restoredIndex = await PictureAsync(repo);
        staged = await UndoAsync(repository, staged);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, staged);
        Assert.Equal(restoredIndex, await PictureAsync(repo));
    }

    [Fact]
    public async Task The_backups_lists_restore_of_a_hard_resets_changes_is_undone_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var head = (await repo.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim();

        // A hard reset's changes: the index from the snapshot's second parent, then its files.
        repo.WriteFile("b.txt", "b-staged\n");
        await repo.GitAsync("add", "b.txt");
        repo.WriteFile("b.txt", "b-unstaged\n").WriteFile("u.txt", "untracked\n");
        var changes = (await repository.SnapshotForHardResetAsync(head, TestCancelled))!;
        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, head)]) { ChangesCommit = changes }, TestCancelled);
        await repository.ResetAsync(head, ResetMode.Hard, cancellationToken: TestCancelled);
        File.Delete(Path.Combine(repo.Path, "u.txt"));
        var files = await repository.ReadUndoSnapshotAsync(backup.Refs[^1], changes, TestCancelled);
        var clean = await PictureAsync(repo);

        var restore = await RecordAsync(repository, UndoScope.RestoreChanges(files.Paths, discarded: false), async () =>
        {
            var second = (await repository.ResolveAsync(changes + "^2", TestCancelled))!;
            await repo.GitAsync("restore", "--staged", $"--source={second}", "--", ".");
            UndoCapture.IndexRestored(second);
            UndoCapture.Snapshot(files);
            await repository.RestoreSnapshotAsync(files, TestCancelled);
        });
        var restored = await PictureAsync(repo);
        Assert.Equal("b-unstaged\n", ReadText(repo, "b.txt"));
        Assert.Equal("b-staged\n", (await repo.GitAsync("show", ":b.txt")).StandardOutput);

        restore = await UndoAsync(repository, restore);
        Assert.Equal(clean, await PictureAsync(repo));
        await RedoAsync(repository, restore);
        Assert.Equal(restored, await PictureAsync(repo));
    }
}
