using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitRepository.ReadBlameAsync"/> with real git (docs/phase-5-design.md 4.2, 0.1): a
/// commit's version or the working tree's, each line with the commit that last changed it, and
/// every failure of 0.1 with git's own message.
/// </summary>
public class BlameTests(BlameTests.Repository fixture) : IClassFixture<BlameTests.Repository>
{
    /// <summary>One repository with a file edited by two commits and then renamed, built once for the tests that only read it.</summary>
    public sealed class Repository : IAsyncLifetime
    {
        public TempRepo Repo { get; private set; } = null!;

        public GitRepository Git { get; private set; } = null!;

        /// <summary>Adds <c>old.txt</c> with five lines.</summary>
        public string First { get; private set; } = string.Empty;

        /// <summary>Changes its third line.</summary>
        public string Second { get; private set; } = string.Empty;

        /// <summary>Renames it to <c>docs/new.txt</c>, unchanged.</summary>
        public string Renamed { get; private set; } = string.Empty;

        public async ValueTask InitializeAsync()
        {
            Repo = await TempRepo.CreateAsync("blame");
            First = await Repo.CommitFileAsync("old.txt", "one\ntwo\nthree\nfour\nfive\n", "Add old");
            Second = await Repo.CommitFileAsync("old.txt", "one\ntwo\nTHREE\nfour\nfive\n", "Shout three");
            Repo.DeleteFile("old.txt").WriteFile("docs/new.txt", "one\ntwo\nTHREE\nfour\nfive\n");
            Renamed = await Repo.CommitAsync("Move old to docs");
            Git = await OpenAsync(Repo);
        }

        public ValueTask DisposeAsync()
        {
            Repo?.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task A_commits_version_names_each_lines_commit_across_a_rename()
    {
        var result = await fixture.Git.ReadBlameAsync(fixture.Renamed, "docs/new.txt", TestCancelled);

        Assert.Equal(["one", "two", "THREE", "four", "five"], result.Lines.Select(line => line.Text));
        Assert.Equal(
            [fixture.First, fixture.First, fixture.Second, fixture.First, fixture.First],
            result.Lines.Select(line => line.Commit.Sha));

        // Blame followed the whole-file rename without -M: each commit's own path.
        var first = result.Lines[0].Commit;
        Assert.Equal("old.txt", first.Path);
        Assert.True(first.IsBoundary);
        Assert.Null(first.PreviousSha);
        Assert.Equal("Add old", first.Summary);
        Assert.Equal(TempRepo.AuthorName, first.AuthorName);
        Assert.Equal(TempRepo.AuthorEmail, first.AuthorEmail);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), first.AuthorTime);

        var second = result.Lines[2].Commit;
        Assert.Equal(("old.txt", fixture.First, "old.txt", false), (second.Path, second.PreviousSha, second.PreviousPath, second.IsBoundary));
        Assert.Same(first, result.Lines[4].Commit);
    }

    [Fact]
    public async Task The_working_tree_marks_lines_not_committed_yet_staged_or_not()
    {
        using var repo = await TempRepo.CreateAsync("blame-work");
        var commit = await repo.CommitFileAsync("f.txt", "one\ntwo\nthree\n", "Add f");
        repo.WriteFile("f.txt", "one\nstaged\nthree\n");
        await repo.GitAsync("add", "f.txt");
        repo.WriteFile("f.txt", "one\nstaged\nthree\nunstaged\n");
        var repository = await OpenAsync(repo);

        var lines = (await repository.ReadBlameAsync(null, "f.txt", TestCancelled)).Lines;

        Assert.Equal(["one", "staged", "three", "unstaged"], lines.Select(line => line.Text));
        Assert.Equal([false, true, false, true], lines.Select(line => line.Commit.IsUncommitted));
        Assert.Equal(commit, lines[0].Commit.Sha);
        Assert.Equal("Not Committed Yet", lines[1].Commit.AuthorName);
        Assert.Same(lines[1].Commit, lines[3].Commit);
    }

    [Fact]
    public async Task A_staged_rename_and_a_file_only_in_the_index_are_blamed_in_the_working_tree()
    {
        using var repo = await TempRepo.CreateAsync("blame-staged");
        var commit = await repo.CommitFileAsync("f.txt", "one\ntwo\n", "Add f");
        await repo.GitAsync("mv", "f.txt", "g.txt");
        repo.WriteFile("new.txt", "fresh\n");
        await repo.GitAsync("add", "new.txt");
        var repository = await OpenAsync(repo);

        var renamed = (await repository.ReadBlameAsync(null, "g.txt", TestCancelled)).Lines;
        Assert.All(renamed, line => Assert.Equal((commit, "f.txt"), (line.Commit.Sha, line.Commit.Path)));

        var added = (await repository.ReadBlameAsync(null, "new.txt", TestCancelled)).Lines;
        Assert.True(Assert.Single(added).Commit.IsUncommitted);
    }

    [Fact]
    public async Task A_conflicted_file_is_blamed_with_each_sides_commits()
    {
        using var repo = await TempRepo.CreateAsync("blame-conflict");
        await repo.CommitFileAsync("f.txt", "base\n", "Base");
        await repo.CreateBranchAsync("other");
        var ours = await repo.CommitFileAsync("f.txt", "ours\n", "Ours");
        await repo.CheckoutAsync("other");
        var theirs = await repo.CommitFileAsync("f.txt", "theirs\n", "Theirs");
        await repo.CheckoutAsync("main");
        Assert.False((await repo.GitAllowingFailureAsync("merge", "--no-edit", "other")).Succeeded);
        var repository = await OpenAsync(repo);

        var lines = (await repository.ReadBlameAsync(null, "f.txt", TestCancelled)).Lines;

        Assert.Equal(["<<<<<<< HEAD", "ours", "=======", "theirs", ">>>>>>> other"], lines.Select(line => line.Text));
        Assert.Equal(ours, lines[1].Commit.Sha);
        Assert.Equal(theirs, lines[3].Commit.Sha);
        Assert.True(lines[0].Commit.IsUncommitted);
    }

    [Fact]
    public async Task Line_bytes_survive_carriage_returns_non_utf8_and_binary_content()
    {
        using var repo = await TempRepo.CreateAsync("blame-bytes");
        repo.WriteBytes("crlf.txt", Encoding.ASCII.GetBytes("a\r\nb\r\n"))
            .WriteBytes("latin1.txt", [0x63, 0x61, 0x66, 0xE9, 0x0A])
            .WriteBytes("bin.dat", [0x00, 0x01, 0x0A, 0x02, 0x00])
            .WriteFile("empty.txt", string.Empty)
            .WriteBytes("no-newline.txt", Encoding.ASCII.GetBytes("last"));
        var commit = await repo.CommitAsync("Odd files");
        var repository = await OpenAsync(repo);

        Assert.Equal(["a\r", "b\r"], (await repository.ReadBlameAsync(commit, "crlf.txt", TestCancelled)).Lines.Select(line => line.Text));
        Assert.Equal(["café"], (await repository.ReadBlameAsync(commit, "latin1.txt", TestCancelled)).Lines.Select(line => line.Text));

        var binary = await repository.ReadBlameAsync(commit, "bin.dat", TestCancelled);
        Assert.True(binary.IsBinary);
        Assert.Equal(["\0\u0001", "\u0002\0"], binary.Lines.Select(line => line.Text));

        Assert.True((await repository.ReadBlameAsync(commit, "empty.txt", TestCancelled)).IsEmpty);

        // Git adds the line feed the file lacks; the porcelain cannot tell.
        Assert.Equal(["last"], (await repository.ReadBlameAsync(commit, "no-newline.txt", TestCancelled)).Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task Names_paths_and_an_empty_message_come_back_as_they_are()
    {
        using var repo = await TempRepo.CreateAsync("blame-names");
        // Git C-quotes a non-ASCII path in the porcelain (core.quotePath); quotes and tabs, which
        // Windows does not allow in names, are BlameParserTests'.
        const string Path = "日本語/ßΩ 名前.txt";
        repo.WriteFile(Path, "line\n");
        await repo.GitAsync("add", "--all");
        await repo.GitWithEnvironmentAsync(
            new Dictionary<string, string?> { ["GIT_AUTHOR_NAME"] = "José Núñez" },
            "commit", "--quiet", "--allow-empty-message", "--message", string.Empty);
        var sha = await repo.HeadAsync();

        // A user's output encoding would change the author's bytes; the app fixes it to UTF-8.
        await repo.ConfigAsync("i18n.logOutputEncoding", "ISO-8859-1");
        var repository = await OpenAsync(repo);

        var commit = Assert.Single((await repository.ReadBlameAsync(sha, Path, TestCancelled)).Lines).Commit;

        Assert.Equal(Path, commit.Path);
        Assert.Equal("José Núñez", commit.AuthorName);
        Assert.Equal(string.Empty, commit.Summary);
    }

    [Fact]
    public async Task Textconv_is_not_applied()
    {
        using var repo = await TempRepo.CreateAsync("blame-textconv");
        repo.WriteFile(".gitattributes", "*.txt diff=upper\n").WriteFile("f.txt", "lower\n");
        var commit = await repo.CommitAsync("Add f");
        await repo.ConfigAsync("diff.upper.textconv", "tr a-z A-Z <");
        var repository = await OpenAsync(repo);

        // Git's own blame applies it; the app's does not.
        Assert.Contains("\tLOWER\n", (await repo.GitAsync("blame", "--porcelain", commit, "--", "f.txt")).StandardOutput, StringComparison.Ordinal);
        Assert.Equal(["lower"], (await repository.ReadBlameAsync(commit, "f.txt", TestCancelled)).Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task The_users_ignore_revs_file_is_honoured()
    {
        using var repo = await TempRepo.CreateAsync("blame-ignore");
        var first = await repo.CommitFileAsync("f.txt", "one\ntwo\n", "Add f");
        var reformat = await repo.CommitFileAsync("f.txt", "one\ntwo, reformatted\n", "Reformat");
        repo.WriteFile(".git-blame-ignore-revs", reformat + "\n");
        await repo.ConfigAsync("blame.ignoreRevsFile", ".git-blame-ignore-revs");
        var repository = await OpenAsync(repo);

        var lines = (await repository.ReadBlameAsync(reformat, "f.txt", TestCancelled)).Lines;

        Assert.Equal([first, first], lines.Select(line => line.Commit.Sha));
    }

    [Fact]
    public async Task A_missing_ignore_revs_file_throws_its_own_exception_naming_the_file()
    {
        using var repo = await TempRepo.CreateAsync("blame-ignore-missing");
        var commit = await repo.CommitFileAsync("f.txt", "one\n", "Add f");
        await repo.ConfigAsync("blame.ignoreRevsFile", "no such/revs file");
        var repository = await OpenAsync(repo);

        var ex = await Assert.ThrowsAsync<BlameIgnoreRevsFileMissingException>(() => repository.ReadBlameAsync(commit, "f.txt", TestCancelled));

        Assert.Equal("no such/revs file", ex.File);
        Assert.Contains("could not open object name list", ex.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gits_failures_throw_with_its_message()
    {
        using var unborn = await TempRepo.CreateAsync("blame-unborn");
        unborn.WriteFile("u.txt", "u\n");
        var unbornRepository = await OpenAsync(unborn);
        Assert.Contains("no such ref: HEAD", (await Assert.ThrowsAsync<GitException>(() => unbornRepository.ReadBlameAsync(null, "u.txt", TestCancelled))).StandardError, StringComparison.Ordinal);

        fixture.Repo.WriteFile("untracked.txt", "u\n");
        try
        {
            await AssertFailsAsync(null, "untracked.txt", "no such path 'untracked.txt' in HEAD");
            await AssertFailsAsync(null, "docs", "no such path 'docs' in HEAD");
        }
        finally
        {
            fixture.Repo.DeleteFile("untracked.txt");
        }

        // The path is deleted at the commit: its parent has it.
        await AssertFailsAsync(fixture.Renamed, "old.txt", $"no such path old.txt in {fixture.Renamed}");

        File.Move(Path.Combine(fixture.Repo.Path, "docs", "new.txt"), Path.Combine(fixture.Repo.Path, "docs", "moved-away.txt"));
        try
        {
            await AssertFailsAsync(null, "docs/new.txt", "Cannot lstat 'docs/new.txt'");
        }
        finally
        {
            File.Move(Path.Combine(fixture.Repo.Path, "docs", "moved-away.txt"), Path.Combine(fixture.Repo.Path, "docs", "new.txt"));
        }

        async Task AssertFailsAsync(string? sha, string path, string message)
        {
            var ex = await Assert.ThrowsAsync<GitException>(() => fixture.Git.ReadBlameAsync(sha, path, TestCancelled));
            Assert.Equal(128, ex.ExitCode);
            Assert.Contains(message, ex.StandardError, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_revision_that_looks_like_an_option_is_refused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Git.ReadBlameAsync("--reverse", "docs/new.txt", TestCancelled));
    }
}
