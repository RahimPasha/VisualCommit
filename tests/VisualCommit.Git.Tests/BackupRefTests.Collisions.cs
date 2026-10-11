using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The hard reset's snapshot where a path of the target meets something else in the working tree
/// (2.11): git deletes a folder's files where the target has a file, and a file where the target
/// has a folder, untracked and ignored alike, without a word. Each case runs the real reset after
/// the backup and reads every lost file back from the backup's <c>-changes</c> ref.
/// </summary>
public partial class BackupRefTests
{
    [Fact]
    public async Task A_hard_reset_snapshot_keeps_every_file_of_a_folder_where_the_target_has_a_file()
    {
        using var repo = await Scenarios.OperationsAsync();
        var target = await repo.CommitFileAsync("foo", "foo, tracked\n", "Add foo");
        await repo.GitAsync("rm", "--quiet", "foo");
        await repo.CommitAsync("Remove foo");
        await repo.CommitFileAsync(".gitignore", "build/\n", "Ignore build");
        repo.WriteFile("foo/notes.txt", "notes\n")
            .WriteFile("foo/build/x.o", "ignored\n")
            .WriteFile("foo/deep dir/über ñ.txt", "unicode\n");
        Assert.Equal(["?? foo/", "!! foo/build/"], await GitLinesAsync(repo, "status", "--porcelain", "--ignored"));
        var repository = await OpenAsync(repo);

        var changes = await HardResetWithBackupAsync(repo, repository, target);

        // Git put the file in the folder's place; the backup has every file of the folder.
        Assert.Equal("foo, tracked\n", ReadText(repo, "foo"));
        Assert.Equal("notes\n", await ShowFileAsync(repo, changes, "foo/notes.txt"));
        Assert.Equal("ignored\n", await ShowFileAsync(repo, changes, "foo/build/x.o"));
        Assert.Equal("unicode\n", await ShowFileAsync(repo, changes, "foo/deep dir/über ñ.txt"));
    }

    [Fact]
    public async Task A_hard_reset_snapshot_keeps_a_file_where_the_target_has_a_folder()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("dir/x", "x\n").WriteFile("logs/today.log", "tracked log\n").WriteFile("a/b/c", "c\n");
        var target = await repo.CommitAsync("Add folders");
        await repo.GitAsync("rm", "--quiet", "-r", "dir", "logs", "a");
        await repo.CommitAsync("Remove folders");
        File.AppendAllText(Path.Combine(repo.Path, ".git", "info", "exclude"), "logs\n");

        // An untracked file, an ignored one, and one two folders down (a/ is a folder, a/b a file).
        repo.WriteFile("dir", "untracked\n").WriteFile("logs", "ignored\n").WriteFile("a/b", "nested\n");
        var repository = await OpenAsync(repo);

        var changes = await HardResetWithBackupAsync(repo, repository, target);

        Assert.True(Directory.Exists(Path.Combine(repo.Path, "dir")));
        Assert.True(Directory.Exists(Path.Combine(repo.Path, "logs")));
        Assert.Equal("c\n", ReadText(repo, "a/b/c"));
        Assert.Equal("untracked\n", await ShowFileAsync(repo, changes, "dir"));
        Assert.Equal("ignored\n", await ShowFileAsync(repo, changes, "logs"));
        Assert.Equal("nested\n", await ShowFileAsync(repo, changes, "a/b"));
    }

    [Fact]
    public async Task A_hard_reset_snapshot_keeps_an_ignored_file_in_a_tracked_folder_that_the_target_has_as_a_file()
    {
        using var repo = await Scenarios.OperationsAsync();
        var target = await repo.CommitFileAsync("config", "config, a file\n", "Config as a file");
        await repo.GitAsync("rm", "--quiet", "config");
        repo.WriteFile("config/a.txt", "a\n").WriteFile(".gitignore", "local.env\n");
        await repo.CommitAsync("Config as a folder");
        repo.WriteFile("config/local.env", "secret\n");
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
        var repository = await OpenAsync(repo);

        var changes = await HardResetWithBackupAsync(repo, repository, target);

        Assert.Equal("config, a file\n", ReadText(repo, "config"));
        Assert.Equal("secret\n", await ShowFileAsync(repo, changes, "config/local.env"));
        Assert.Equal("a\n", await ShowFileAsync(repo, changes, "config/a.txt")); // From HEAD, as before.
    }

    [Fact]
    public async Task A_hard_reset_snapshot_keeps_the_files_of_a_folder_that_took_a_tracked_files_place()
    {
        using var repo = await Scenarios.OperationsAsync();
        var target = await repo.CommitFileAsync("f", "f, tracked\n", "Add f");
        repo.DeleteFile("f").WriteFile("f/u.txt", "in the folder\n");

        // The status shows the tracked file deleted, and not the folder's file.
        Assert.Equal([" D f"], await GitLinesAsync(repo, "status", "--porcelain"));
        var repository = await OpenAsync(repo);

        var changes = await HardResetWithBackupAsync(repo, repository, target);

        Assert.Equal("f, tracked\n", ReadText(repo, "f"));
        Assert.Equal("in the folder\n", await ShowFileAsync(repo, changes, "f/u.txt"));
    }

    [Fact]
    public async Task A_hard_reset_that_would_delete_a_nested_repository_does_not_start()
    {
        using var repo = await Scenarios.OperationsAsync();
        var target = await repo.CommitFileAsync("foo", "foo, tracked\n", "Add foo");
        await repo.GitAsync("rm", "--quiet", "foo");
        await repo.CommitAsync("Remove foo");
        Directory.CreateDirectory(Path.Combine(repo.Path, "foo", "inner"));
        await GitInAsync(repo, Path.Combine(repo.Path, "foo", "inner"), "init", "--quiet");
        repo.WriteFile("foo/inner/i.txt", "inner\n");
        var repository = await OpenAsync(repo);

        // git reset --hard would delete foo/ with the repository in it, history and all.
        var error = await Assert.ThrowsAsync<IOException>(() => repository.SnapshotForHardResetAsync(target, TestCancelled));

        Assert.Contains("'foo/inner'", error.Message, StringComparison.Ordinal);
        Assert.Equal("inner\n", ReadText(repo, "foo/inner/i.txt"));
    }

    /// <summary>
    /// The hard reset as the app runs it (2.11): the snapshot, the backup batch, the reset. Returns
    /// the backup's <c>-changes</c> ref.
    /// </summary>
    private static async Task<string> HardResetWithBackupAsync(TempRepo repo, GitRepository repository, string target)
    {
        var head = await repo.HeadAsync();
        var changes = await repository.SnapshotForHardResetAsync(target, TestCancelled);
        Assert.NotNull(changes);
        var backup = await repository.SaveBackupRefsAsync(new BackupRequest("reset", [new BackupEntry(null, head)]) { ChangesCommit = changes }, TestCancelled);
        await repository.ResetAsync(target, ResetMode.Hard, cancellationToken: TestCancelled);
        Assert.Equal(target, await repo.HeadAsync());
        return Assert.Single(backup.Refs, name => name.EndsWith("-changes", StringComparison.Ordinal));
    }
}
