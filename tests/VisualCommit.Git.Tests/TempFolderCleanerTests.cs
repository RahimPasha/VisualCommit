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

    [Fact]
    public async Task A_blank_git_folder_is_not_recorded_without_the_folders_git_needs()
    {
        // A cleaner that ran between git init and the record would leave a list without refs or
        // objects, and every copy made from it would be no repository: the record refuses it.
        using var root = new TempDirectory("stripped");
        var blank = await TempRepo.MakeBlankGitFolderAsync(root, "main");
        Assert.Contains("refs", blank.Folders);
        Assert.Contains("objects", blank.Folders);

        RemoveEmptyFolders(blank.Path);

        var refused = Assert.Throws<InvalidOperationException>(() => BlankGitFolder.Record(blank.Path));
        Assert.Contains(blank.Path, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_working_copy_and_its_bare_clone_are_usable_with_mains_sha_each_as_its_own_kind()
    {
        using var repo = await SmallHistoryAsync();
        var bare = repo.SiblingPath(SmallBare);
        var main = await repo.HeadAsync();

        Assert.True(await LargeHistory.IsUsableAsync(repo.Path, main, bare: false));
        Assert.True(await LargeHistory.IsUsableAsync(bare, main, bare: true));
        Assert.False(await LargeHistory.IsUsableAsync(repo.Path, main, bare: true));
        Assert.False(await LargeHistory.IsUsableAsync(bare, main, bare: false));
    }

    [Fact]
    public async Task A_bare_copy_that_lost_its_empty_folders_is_not_usable()
    {
        using var repo = await SmallHistoryAsync();
        var bare = repo.SiblingPath(SmallBare);
        var main = await repo.HeadAsync();

        // Its refs are all in packed-refs, so refs held nothing but empty folders.
        RemoveEmptyFolders(bare);
        Assert.False(Directory.Exists(Path.Combine(bare, "refs")));

        Assert.False(await LargeHistory.IsUsableAsync(bare, main, bare: true));
    }

    [Fact]
    public async Task A_bare_copy_that_lost_its_empty_folders_is_made_again()
    {
        // The bare copy is made beside the working copy it is cloned from, here a small one of the
        // test's own, with its marker, as the shared one is.
        using var repo = await SmallHistoryAsync();
        var main = await repo.HeadAsync();
        var bare = await LargeHistory.GetOrBuildBareAsync(repo.Path);
        Assert.Equal(main, LargeHistory.MarkedSha(bare + ".complete"));
        Assert.True(await LargeHistory.IsUsableAsync(bare, main, bare: true));

        // Stripped with its marker left, as on the laptop on 2026-11-24.
        RemoveEmptyFolders(bare);
        Assert.False(Directory.Exists(Path.Combine(bare, "refs")));

        Assert.Equal(bare, await LargeHistory.GetOrBuildBareAsync(repo.Path));
        Assert.True(Directory.Exists(Path.Combine(bare, "refs")));
        Assert.True(await LargeHistory.IsUsableAsync(bare, main, bare: true));
        Assert.Equal(main, LargeHistory.MarkedSha(bare + ".complete"));
    }

    [Fact]
    public async Task A_copy_whose_marker_names_another_commit_than_main_is_not_usable()
    {
        using var repo = await SmallHistoryAsync();
        var first = (await repo.GitAsync("rev-parse", "main~2")).StandardOutput.Trim();

        Assert.False(await LargeHistory.IsUsableAsync(repo.SiblingPath(SmallBare), first, bare: true));
        Assert.False(await LargeHistory.IsUsableAsync(repo.Path, first, bare: false));
    }

    [Fact]
    public async Task A_working_copy_whose_git_folder_lost_its_refs_is_not_usable()
    {
        using var repo = await SmallHistoryAsync();
        var main = await repo.HeadAsync();

        TempRepo.DeleteDirectory(Path.Combine(repo.Path, ".git", "refs"));

        Assert.False(await LargeHistory.IsUsableAsync(repo.Path, main, bare: false));
    }

    [Fact]
    public async Task A_bare_copy_whose_commit_is_gone_is_not_usable()
    {
        using var repo = await SmallHistoryAsync();
        var bare = repo.SiblingPath(SmallBare);
        var main = await repo.HeadAsync();

        // A local clone copies the loose objects as they are: main's commit is a file of its own.
        var commit = Path.Combine(bare, "objects", main[..2], main[2..]);
        File.SetAttributes(commit, FileAttributes.Normal);
        File.Delete(commit);

        Assert.False(await LargeHistory.IsUsableAsync(bare, main, bare: true));
    }

    [Fact]
    public void A_marker_names_a_copy_only_with_a_sha()
    {
        using var folder = new TempDirectory("marker");
        var marker = folder.Combine("copy.complete");
        const string Sha = "0123456789abcdef0123456789abcdef01234567";

        Assert.Null(LargeHistory.MarkedSha(marker));
        File.WriteAllText(marker, Sha + "\n");
        Assert.Equal(Sha, LargeHistory.MarkedSha(marker));
        File.WriteAllText(marker, string.Empty);
        Assert.Null(LargeHistory.MarkedSha(marker));
        File.WriteAllText(marker, "not a sha\n");
        Assert.Null(LargeHistory.MarkedSha(marker));
    }

    /// <summary>The folder of <see cref="SmallHistoryAsync"/>'s bare clone, beside the working tree.</summary>
    private const string SmallBare = "small.git";

    /// <summary>
    /// A repository with three commits on <c>main</c> and, beside it (<see cref="SmallBare"/>), a
    /// bare clone of it as the shared bare copy is made (<c>git clone --bare --no-hardlinks</c>),
    /// whose refs are all in <c>packed-refs</c>.
    /// </summary>
    private static async Task<TempRepo> SmallHistoryAsync()
    {
        var repo = await TempRepo.CreateAsync("small");
        try
        {
            await repo.CommitFileAsync("a.txt", "a\n", "First");
            await repo.CommitFileAsync("b.txt", "b\n", "Second");
            await repo.CommitFileAsync("c.txt", "c\n", "Third");
            await repo.GitAsync("clone", "--quiet", "--bare", "--no-hardlinks", repo.Path, repo.SiblingPath(SmallBare));
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
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
