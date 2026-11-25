using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Tests of what the builders do about a cleaner of the temp folder that removes every empty
/// folder, whatever its age, as Windows' Storage Sense does when the disk runs low: the blank git
/// folders and the scenarios' templates, which live for a test process, and the shared copies of
/// the 100k-commit repo, which live for the machine, lose folders that git needs. Each test strips a
/// folder of its own; none touches the process's blanks or templates, which the other tests of the
/// process copy, or the shared folder.
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
    public async Task A_copy_of_a_kept_repository_that_lost_its_empty_folders_has_them()
    {
        // A template of the test's own, kept as Scenarios keeps a template: its folders recorded, then
        // copied for each test.
        using var template = await RepositoryWithAnEmptyBareAsync();
        var empty = template.SiblingPath(EmptyBare);
        template.RecordFolders();
        Assert.Contains(Path.Combine(EmptyBare, "refs", "heads"), template.KeptFolders);

        // Stripped while the process runs, as on the laptop on 2026-11-24: the bare repository keeps
        // its HEAD and loses refs and objects, and every git folder loses its empty hooks.
        var root = Path.GetDirectoryName(template.Path)!;
        var hooks = HooksIn(root);
        Assert.Equal(2, hooks.Count);
        RemoveEmptyFolders(root);
        Assert.False(Directory.Exists(Path.Combine(empty, "refs")));
        Assert.Empty(HooksIn(root));

        using var copy = await template.CopyAsync();
        Assert.Equal("true", (await GitInAsync(copy, copy.SiblingPath(EmptyBare), "rev-parse", "--is-bare-repository")).Trim());
        Assert.Equal(hooks, HooksIn(Path.GetDirectoryName(copy.Path)!));
    }

    [Fact]
    public async Task A_copy_of_a_kept_workspace_that_lost_its_empty_folders_has_them()
    {
        // A workspace of the test's own, built as Scenarios builds the ten-repository one, with two
        // of its repositories (api with its remote, notes without) and a bare remote beside them that
        // nothing is pushed to; kept as Scenarios keeps the workspace's template.
        using var template = await SmallWorkspaceAsync();
        var empty = Path.Combine(template.Root, LeanRepositories.RemotesFolder, EmptyBare);
        template.RecordFolders();
        Assert.Contains(Path.Combine(LeanRepositories.RemotesFolder, EmptyBare, "refs", "heads"), template.KeptFolders);

        // api's working repository, its remote, notes' and the empty remote: four hooks folders.
        var hooks = HooksIn(template.Root);
        Assert.Equal(4, hooks.Count);
        RemoveEmptyFolders(template.Root);
        Assert.False(Directory.Exists(Path.Combine(empty, "refs")));
        Assert.Empty(HooksIn(template.Root));

        using var copy = await template.CopyAsync();
        var api = copy.Repo("api");
        Assert.Equal("true", (await GitInAsync(api, Path.Combine(copy.Root, LeanRepositories.RemotesFolder, EmptyBare), "rev-parse", "--is-bare-repository")).Trim());
        Assert.Equal(hooks, HooksIn(copy.Root));
    }

    [Fact]
    public async Task A_kept_repository_whose_bare_repository_lost_its_refs_before_the_record_is_refused()
    {
        // A cleaner that ran while the template was built, after its bare repository was made and
        // before its folders were read, would leave a record without that repository's refs and
        // objects, and no copy of it would be a repository: the record refuses it, as the blank git
        // folder's does. The working repository, whose refs and objects hold files, is no reason.
        using var template = await RepositoryWithAnEmptyBareAsync();
        RemoveEmptyFolders(Path.GetDirectoryName(template.Path)!);
        Assert.True(Directory.Exists(Path.Combine(template.Path, ".git", "refs")));

        var refused = Assert.Throws<InvalidOperationException>(template.RecordFolders);
        Assert.Contains(EmptyBare, refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.Combine(Path.GetFileName(template.Path), ".git"), refused.Message, StringComparison.Ordinal);
        Assert.Empty(template.KeptFolders);
    }

    [Fact]
    public async Task A_kept_workspace_whose_bare_remote_lost_its_refs_before_the_record_is_refused()
    {
        using var template = await SmallWorkspaceAsync();
        RemoveEmptyFolders(template.Root);

        var refused = Assert.Throws<InvalidOperationException>(template.RecordFolders);
        Assert.Contains(Path.Combine(LeanRepositories.RemotesFolder, EmptyBare), refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.Combine("api", ".git"), refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.Combine(LeanRepositories.RemotesFolder, "api.git"), refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.Combine("notes", ".git"), refused.Message, StringComparison.Ordinal);
        Assert.Empty(template.KeptFolders);
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

        // The copy made again is whole, and the proof takes it as it is: the next call keeps it, with
        // its marker, and does not make it again, which would take the file left in it.
        var left = Path.Combine(bare, "left-by-the-test");
        await File.WriteAllTextAsync(left, "kept\n", TestContext.Current.CancellationToken);
        var marked = File.GetLastWriteTimeUtc(bare + ".complete");
        Assert.Equal(bare, await LargeHistory.GetOrBuildBareAsync(repo.Path));
        Assert.True(File.Exists(left));
        Assert.Equal(marked, File.GetLastWriteTimeUtc(bare + ".complete"));
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
    public async Task A_whole_copy_with_its_other_refs_is_usable_as_its_own_kind()
    {
        using var repo = await SmallHistoryAsync();
        var main = await repo.HeadAsync();

        Assert.True(await LargeHistory.IsUsableAsync(repo.Path, main, bare: false, Feature, Middle));
        Assert.True(await LargeHistory.IsUsableAsync(repo.SiblingPath(SmallBare), main, bare: true, Feature, Middle));
    }

    [Fact]
    public async Task A_working_copy_that_lost_a_tracked_file_is_not_usable()
    {
        using var repo = await SmallHistoryAsync();
        var main = await repo.HeadAsync();

        File.Delete(Path.Combine(repo.Path, "a.txt"));

        Assert.False(await LargeHistory.IsUsableAsync(repo.Path, main, bare: false, Feature, Middle));
    }

    [Fact]
    public async Task A_working_copy_that_lost_its_index_is_not_usable()
    {
        using var repo = await SmallHistoryAsync();
        var main = await repo.HeadAsync();

        File.Delete(Path.Combine(repo.Path, ".git", "index"));

        Assert.False(await LargeHistory.IsUsableAsync(repo.Path, main, bare: false, Feature, Middle));
    }

    [Theory]
    [InlineData(Feature)]
    [InlineData(Middle)]
    public async Task A_working_copy_that_lost_its_feature_branch_or_its_tag_is_not_usable(string name)
    {
        using var repo = await SmallHistoryAsync();
        var main = await repo.HeadAsync();

        // The working copy's refs are files of their own, as git branch and git tag wrote them.
        var file = Path.Combine([repo.Path, ".git", .. name.Split('/')]);
        Assert.True(File.Exists(file));
        File.Delete(file);

        Assert.False(await LargeHistory.IsUsableAsync(repo.Path, main, bare: false, Feature, Middle));
    }

    [Theory]
    [InlineData(Feature)]
    [InlineData(Middle)]
    public async Task A_bare_copy_that_lost_its_feature_branch_or_its_tag_is_not_usable(string name)
    {
        using var repo = await SmallHistoryAsync();
        var bare = repo.SiblingPath(SmallBare);
        var main = await repo.HeadAsync();

        // The bare copy's refs are lines of packed-refs.
        await GitInAsync(repo, bare, "update-ref", "-d", name);

        Assert.False(await LargeHistory.IsUsableAsync(bare, main, bare: true, Feature, Middle));
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

    /// <summary>The folder of a template's bare repository that nothing is pushed to.</summary>
    private const string EmptyBare = "empty.git";

    /// <summary>
    /// A repository with one commit in a folder of the test's own and, beside it, the bare
    /// repository <see cref="EmptyBare"/> that nothing is pushed to, made as phase 7a's template makes
    /// <c>github/octo/notes.git</c>: a copy of the blank bare repository, on <c>main</c>.
    /// </summary>
    private static async Task<TempRepo> RepositoryWithAnEmptyBareAsync()
    {
        var repo = await TempRepo.CreateAsync("kept");
        try
        {
            await repo.CommitFileAsync("a.txt", "a\n", "First");
            var empty = repo.SiblingPath(EmptyBare);
            (await TempRepo.BlankBareFolderAsync()).CopyTo(empty);
            await File.WriteAllTextAsync(Path.Combine(empty, "HEAD"), "ref: refs/heads/main\n", TestContext.Current.CancellationToken);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// A workspace in a folder of the test's own, built as <c>Scenarios</c> builds the workspace's
    /// template (<see cref="LeanRepositories.BuildAsync"/> for each repository): the template's
    /// <c>api</c>, with <c>remotes/api.git</c>, and <c>notes</c>, with no remote, and beside them
    /// <c>remotes/</c><see cref="EmptyBare"/>, a copy of the blank bare repository on <c>main</c>.
    /// </summary>
    private static async Task<TempWorkspace> SmallWorkspaceAsync()
    {
        var root = new TempDirectory("workspace");
        try
        {
            var runner = new GitRunner(TempRepo.GitPath, new GitCallLog());
            var repositories = Scenarios.WorkspaceRepositories.Where(repository => repository.Name is "api" or "notes").ToList();
            foreach (var repository in repositories)
            {
                await LeanRepositories.BuildAsync(root, repository, runner);
            }

            var empty = root.Combine(LeanRepositories.RemotesFolder, EmptyBare);
            (await TempRepo.BlankBareFolderAsync()).CopyTo(empty);
            await File.WriteAllTextAsync(Path.Combine(empty, "HEAD"), "ref: refs/heads/main\n", TestContext.Current.CancellationToken);
            var clocks = repositories.ToDictionary(repository => repository.Name, repository => repository.Clock, StringComparer.Ordinal);
            return new TempWorkspace(root, clocks, new GitCallLog());
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }

    /// <summary>Every folder named <c>hooks</c> under <paramref name="folder"/>, by its path relative to it, in ordinal order: git's hooks folders.</summary>
    private static List<string> HooksIn(string folder) =>
        Directory.EnumerateDirectories(folder, "hooks", SearchOption.AllDirectories)
            .Select(hooks => Path.GetRelativePath(folder, hooks))
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>The folder of <see cref="SmallHistoryAsync"/>'s bare clone, beside the working tree.</summary>
    private const string SmallBare = "small.git";

    /// <summary>The branch beside <c>main</c> that the 100k-commit repo has, and <see cref="SmallHistoryAsync"/>'s.</summary>
    private const string Feature = "refs/heads/feature";

    /// <summary>The tag the 100k-commit repo has, and <see cref="SmallHistoryAsync"/>'s.</summary>
    private const string Middle = "refs/tags/" + LargeHistory.MiddleTag;

    /// <summary>
    /// A repository with three commits on <c>main</c>, the branch <see cref="Feature"/> and the
    /// lightweight tag <see cref="Middle"/> on the second, as the 100k-commit repo has its refs, and
    /// beside it (<see cref="SmallBare"/>) a bare clone of it as the shared bare copy is made
    /// (<c>git clone --bare --no-hardlinks</c>), whose refs are all in <c>packed-refs</c>.
    /// </summary>
    private static async Task<TempRepo> SmallHistoryAsync()
    {
        var repo = await TempRepo.CreateAsync("small");
        try
        {
            await repo.CommitFileAsync("a.txt", "a\n", "First");
            await repo.CommitFileAsync("b.txt", "b\n", "Second");
            await repo.CommitFileAsync("c.txt", "c\n", "Third");
            await repo.GitAsync("branch", "feature", "main~1");
            await repo.GitAsync("tag", LargeHistory.MiddleTag, "main~1");
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
