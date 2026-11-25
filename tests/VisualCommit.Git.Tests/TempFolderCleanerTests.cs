using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Tests of what the builders do about a cleaner of the temp folder that removes every empty
/// folder, whatever its age, as Windows' Storage Sense does when the disk runs low: the blank git
/// folders, which live for a test process, and the shared copies of the 100k-commit repo, which
/// live for the machine, lose folders that git needs. Each test strips a folder of its own; none
/// touches the process's blanks, which the other tests of the process copy, or the shared folder.
/// </summary>
public class TempFolderCleanerTests
{
    [Fact]
    public async Task A_copy_of_a_blank_git_folder_that_lost_its_empty_folders_is_a_repository()
    {
        using var root = new TempDirectory("stripped");
        var blank = await TempRepo.MakeBlankGitFolderAsync(root, "main");
        RemoveEmptyFolders(blank.Path);
        Assert.False(Directory.Exists(Path.Combine(blank.Path, "refs")));

        var path = root.Combine("repo");
        blank.CopyTo(Path.Combine(path, ".git"));
        using var repo = TempRepo.Over(root, path, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var commit = await repo.CommitFileAsync("a.txt", "a\n", "First");

        Assert.Equal("false", (await repo.GitAsync("rev-parse", "--is-bare-repository")).StandardOutput.Trim());
        Assert.Equal(commit, await repo.RefTargetAsync("refs/heads/main"));
    }

    [Fact]
    public async Task A_copy_of_a_blank_bare_folder_that_lost_its_empty_folders_is_a_bare_repository()
    {
        using var root = new TempDirectory("stripped");
        var blank = await TempRepo.MakeBlankGitFolderAsync(root, TempRepo.BareKey);
        RemoveEmptyFolders(blank.Path);
        Assert.False(Directory.Exists(Path.Combine(blank.Path, "refs")));

        var bare = root.Combine("remote.git");
        blank.CopyTo(bare);
        using var repo = await TempRepo.CreateAsync();
        var commit = await repo.CommitFileAsync("a.txt", "a\n", "First");

        Assert.Equal("true", (await GitInAsync(repo, bare, "rev-parse", "--is-bare-repository")).Trim());
        await repo.GitAsync("push", "--quiet", bare, "main");
        Assert.Equal(commit, (await GitInAsync(repo, bare, "rev-parse", "refs/heads/main")).Trim());
    }

    /// <summary>
    /// Removes every folder under <paramref name="folder"/> that holds nothing, from the bottom up
    /// and again until none is left, as the cleaner does: a folder that held only empty folders
    /// goes too, as <c>refs</c> does when <c>refs/heads</c> and <c>refs/tags</c> are empty.
    /// </summary>
    private static void RemoveEmptyFolders(string folder)
    {
        while (true)
        {
            var empty = Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories)
                .Where(directory => !Directory.EnumerateFileSystemEntries(directory).Any())
                .ToList();
            if (empty.Count == 0)
            {
                return;
            }

            foreach (var directory in empty)
            {
                Directory.Delete(directory);
            }
        }
    }

    /// <summary>Runs git in <paramref name="folder"/> with <paramref name="repo"/>'s isolation and returns what it printed; throws when it fails.</summary>
    private static async Task<string> GitInAsync(TempRepo repo, string folder, params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = folder, Environment = repo.Environment };
        return (await repo.Runner.RunAsync(command, TestContext.Current.CancellationToken)).EnsureSuccess(command).StandardOutput;
    }
}
