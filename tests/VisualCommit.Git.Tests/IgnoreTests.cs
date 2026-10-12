using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Ignore a file (phase 3 design 2.30): the line <see cref="GitignorePattern"/> builds, appended
/// by <see cref="GitRepository.AppendToGitignoreAsync"/>, and git's own view of the result
/// (<c>git status</c>, <c>git check-ignore</c>).
/// </summary>
public class IgnoreTests
{
    [Fact]
    public async Task Ignoring_an_untracked_file_creates_the_gitignore_and_the_file_leaves_the_status()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("logs/run.log", "run\n").WriteFile("logs/other.log", "other\n");
        var repository = await OpenAsync(repo);

        var pattern = GitignorePattern.ForPath("logs/run.log");
        await repository.AppendToGitignoreAsync(pattern, TestCancelled);

        Assert.Equal("/logs/run.log", pattern);
        Assert.Equal("/logs/run.log\n", GitignoreText(repo));
        Assert.Equal(["?? .gitignore", "?? logs/other.log"], await PorcelainAsync(repo));
    }

    [Fact]
    public async Task A_gitignore_that_is_a_symbolic_link_is_not_written_through()
    {
        using var repo = await Scenarios.OperationsAsync();
        var outside = Sibling(repo, "outside.txt");
        File.WriteAllText(outside, "# outside the working tree\n");

        // A cloned repository can hold .gitignore as a link to any file, such as ~/.bashrc.
        File.CreateSymbolicLink(Path.Combine(repo.Path, ".gitignore"), outside);
        var repository = await OpenAsync(repo);

        var error = await Assert.ThrowsAsync<IOException>(() => repository.AppendToGitignoreAsync("/build.log", TestCancelled));

        Assert.Contains("symbolic link", error.Message, StringComparison.Ordinal);
        Assert.Equal("# outside the working tree\n", File.ReadAllText(outside));
    }

    [Fact]
    public async Task A_folder_is_ignored_whole()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("build/out/app.bin", "app\n").WriteFile("build/log.txt", "log\n").WriteFile("buildnotes.txt", "notes\n");
        var repository = await OpenAsync(repo);

        await repository.AppendToGitignoreAsync(GitignorePattern.ForPath("build"), TestCancelled);

        Assert.Equal("/build\n", GitignoreText(repo));
        Assert.Equal(["?? .gitignore", "?? buildnotes.txt"], await PorcelainAsync(repo));
    }

    [Fact]
    public async Task A_path_with_brackets_ignores_that_file_and_not_the_names_its_pattern_would_match()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("drafts/[wip].txt", "wip\n").WriteFile("drafts/w.txt", "w\n").WriteFile("drafts/i.txt", "i\n");
        var repository = await OpenAsync(repo);

        var pattern = GitignorePattern.ForPath("drafts/[wip].txt");
        await repository.AppendToGitignoreAsync(pattern, TestCancelled);

        Assert.Equal(@"/drafts/\[wip].txt", pattern);
        Assert.Equal(["?? .gitignore", "?? drafts/i.txt", "?? drafts/w.txt"], await PorcelainAsync(repo));
        Assert.True(await IsIgnoredAsync(repo, "drafts/[wip].txt"));
    }

    [Fact]
    public async Task All_files_of_an_extension_are_ignored_anywhere()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("a.log", "a\n").WriteFile("deep/down/b.log", "b\n").WriteFile("c.log.txt", "c\n");
        var repository = await OpenAsync(repo);

        var pattern = GitignorePattern.ForExtension("log");
        await repository.AppendToGitignoreAsync(pattern, TestCancelled);

        Assert.Equal("*.log", pattern);
        Assert.Equal(["?? .gitignore", "?? c.log.txt"], await PorcelainAsync(repo));
    }

    [Fact]
    public async Task A_line_is_added_after_a_last_line_without_a_line_feed()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile(".gitignore", "bin").WriteFile("notes.txt", "notes\n");
        var repository = await OpenAsync(repo);

        await repository.AppendToGitignoreAsync(GitignorePattern.ForPath("notes.txt"), TestCancelled);

        Assert.Equal("bin\n/notes.txt\n", GitignoreText(repo));
        Assert.True(await IsIgnoredAsync(repo, "notes.txt"));
        Assert.True(await IsIgnoredAsync(repo, "bin"));
    }

    [Theory]
    [InlineData("bin\r\nobj\r\n")]
    [InlineData("bin\r\nobj")]
    public async Task A_file_with_crlf_line_endings_gets_a_crlf_line(string existing)
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile(".gitignore", existing).WriteFile("notes.txt", "notes\n");
        var repository = await OpenAsync(repo);

        await repository.AppendToGitignoreAsync(GitignorePattern.ForPath("notes.txt"), TestCancelled);

        Assert.Equal("bin\r\nobj\r\n/notes.txt\r\n", GitignoreText(repo));
        Assert.True(await IsIgnoredAsync(repo, "notes.txt"));
        Assert.True(await IsIgnoredAsync(repo, "obj"));
    }

    [Fact]
    public async Task The_rest_of_the_file_is_kept_byte_for_byte()
    {
        using var repo = await Scenarios.OperationsAsync();
        byte[] existing = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("# Généré\nbin/\n")];
        repo.WriteBytes(".gitignore", existing).WriteFile("notes.txt", "notes\n");
        var repository = await OpenAsync(repo);

        await repository.AppendToGitignoreAsync(GitignorePattern.ForPath("notes.txt"), TestCancelled);

        Assert.Equal([.. existing, .. "/notes.txt\n"u8.ToArray()], File.ReadAllBytes(Path.Combine(repo.Path, ".gitignore")));
    }

    [Fact]
    public async Task A_line_the_file_already_has_is_not_added_twice_unless_a_negation_follows_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile(".gitignore", "/notes.txt\n*.tmp\n").WriteFile("notes.txt", "notes\n").WriteFile("keep.tmp", "keep\n");
        var repository = await OpenAsync(repo);

        await repository.AppendToGitignoreAsync("/notes.txt", TestCancelled);
        Assert.Equal("/notes.txt\n*.tmp\n", GitignoreText(repo));

        // A later "!" line could take the pattern back: the line is added again, after it.
        repo.WriteFile(".gitignore", "*.tmp\n!keep.tmp\n");
        Assert.False(await IsIgnoredAsync(repo, "keep.tmp"));
        await repository.AppendToGitignoreAsync("*.tmp", TestCancelled);

        Assert.Equal("*.tmp\n!keep.tmp\n*.tmp\n", GitignoreText(repo));
        Assert.True(await IsIgnoredAsync(repo, "keep.tmp"));
    }

    [Theory]
    [InlineData("plain.txt", "/plain.txt")]
    [InlineData("src/a b.txt", "/src/a b.txt")]
    [InlineData("star*name?.txt", @"/star\*name\?.txt")]
    [InlineData(@"back\slash.txt", @"/back\\slash.txt")]
    [InlineData("#notes.txt", "/#notes.txt")]
    [InlineData("!important.txt", "/!important.txt")]
    [InlineData("trailing  ", @"/trailing\ \ ")]
    [InlineData("sp ace", "/sp ace")]
    [InlineData("/already/anchored.txt", "/already/anchored.txt")]
    public void Paths_are_anchored_and_escaped(string path, string expected) =>
        Assert.Equal(expected, GitignorePattern.ForPath(path));

    [Theory]
    [InlineData("log", "*.log")]
    [InlineData(".log", "*.log")]
    [InlineData("t[x]t", @"*.t\[x]t")]
    [InlineData("a*", @"*.a\*")]
    public void Extensions_are_escaped(string extension, string expected) =>
        Assert.Equal(expected, GitignorePattern.ForExtension(extension));

    [Theory]
    [InlineData("#comment", @"\#comment")]
    [InlineData("!negation", @"\!negation")]
    [InlineData("mid#dle!", "mid#dle!")]
    public void A_leading_hash_or_exclamation_mark_is_escaped(string text, string expected) =>
        Assert.Equal(expected, GitignorePattern.Escape(text));

    [Fact]
    public async Task Escaped_lines_mean_the_literal_names_to_git()
    {
        // Names that Windows allows, written as escaped lines and checked by git itself.
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("#hash.txt", "h\n").WriteFile("!bang.txt", "b\n").WriteFile("hash.txt", "x\n");
        var repository = await OpenAsync(repo);

        await repository.AppendToGitignoreAsync(GitignorePattern.Escape("#hash.txt"), TestCancelled);
        await repository.AppendToGitignoreAsync(GitignorePattern.Escape("!bang.txt"), TestCancelled);

        Assert.Equal(["?? .gitignore", "?? hash.txt"], await PorcelainAsync(repo));
    }

    [Theory]
    [InlineData("two\nlines")]
    [InlineData("carriage\rreturn")]
    public async Task A_pattern_is_one_line(string pattern)
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AppendToGitignoreAsync(pattern, TestCancelled));
        Assert.Throws<ArgumentException>(() => GitignorePattern.ForPath(pattern));
        Assert.False(File.Exists(Path.Combine(repo.Path, ".gitignore")));
    }

    private static string GitignoreText(TempRepo repo) => File.ReadAllText(Path.Combine(repo.Path, ".gitignore"));

    private static async Task<IReadOnlyList<string>> PorcelainAsync(TempRepo repo) =>
        (await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Whether git ignores <paramref name="path"/>: <c>git check-ignore</c> exits with 0 for an ignored path and 1 for one it does not ignore.</summary>
    private static async Task<bool> IsIgnoredAsync(TempRepo repo, string path)
    {
        // check-ignore refuses literal pathspecs, which the runner sets for every call.
        var command = new GitCommand("check-ignore", "--quiet", "--no-index", "--", path)
        {
            WorkingDirectory = repo.Path,
            Environment = new Dictionary<string, string?>(repo.Environment) { ["GIT_LITERAL_PATHSPECS"] = null },
        };
        var result = await repo.Runner.RunAsync(command, TestCancelled);
        Assert.True(result.ExitCode is 0 or 1, $"git check-ignore failed: {result.StandardError}");
        return result.ExitCode == 0;
    }
}
