using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary><see cref="GitRepository.ReadCommitDetailsAsync"/>: the message, the people and the changed files of one commit.</summary>
public class CommitDetailsTests(CommitDetailsTests.History history) : IClassFixture<CommitDetailsTests.History>
{
    /// <summary>One repository with a commit of each kind the tests read, built once for all of them.</summary>
    public sealed class History : IAsyncLifetime
    {
        /// <summary>Several lines, so that rename detection recognises the file after a move.</summary>
        private const string LongText = "one\ntwo\nthree\nfour\nfive\nsix\nseven\neight\n";

        /// <summary>Characters without a decomposed form, so that a file system that normalises names (macOS) cannot change them.</summary>
        public const string NonAsciiPath = "日本語/ßΩ 名前.txt";

        public const string SpacedPath = "folder with spaces/file name.txt";

        public TempRepo Repo { get; private set; } = null!;

        public GitRepository Repository { get; private set; } = null!;

        public string Root { get; private set; } = string.Empty;

        public string Changes { get; private set; } = string.Empty;

        public string OddNames { get; private set; } = string.Empty;

        public string MainWork { get; private set; } = string.Empty;

        public string Merge { get; private set; } = string.Empty;

        public string Stash { get; private set; } = string.Empty;

        public async ValueTask InitializeAsync()
        {
            Repo = await TempRepo.CreateAsync("details");

            Repo.WriteFile("modified.txt", "before\n").WriteFile("deleted.txt", "gone soon\n").WriteFile("old/name.txt", LongText);
            Root = await Repo.CommitAsync("Start");

            Repo.WriteFile("modified.txt", "after\n")
                .DeleteFile("deleted.txt")
                .WriteFile("added.txt", "new\n")
                .DeleteFile("old/name.txt")
                .WriteFile("new/name.txt", LongText);
            Changes = await Repo.CommitAsync("Change things");

            Repo.WriteFile(SpacedPath, "a\n").WriteFile(NonAsciiPath, "b\n");
            OddNames = await Repo.CommitAsync("Odd names\n\nFirst paragraph,\nwrapped.\n\nSecond paragraph.\n\n\n");

            await Repo.CreateBranchAsync("feature");
            await Repo.CheckoutAsync("feature");
            await Repo.CommitFileAsync("feature.txt", "feature\n", "Feature work");
            await Repo.CheckoutAsync("main");
            MainWork = await Repo.CommitFileAsync("main.txt", "main\n", "Main work");
            Merge = await Repo.MergeAsync("feature");

            Repo.WriteFile("main.txt", "changed, not committed\n");
            await Repo.GitAsync("stash", "push", "--quiet", "-m", "Work in progress");
            Stash = (await Repo.GitAsync("rev-parse", "stash@{0}")).StandardOutput.Trim();

            Repository = await OpenAsync(Repo);
        }

        public ValueTask DisposeAsync()
        {
            Repo?.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task A_root_commit_lists_its_files_as_added()
    {
        var details = await history.Repository.ReadCommitDetailsAsync(history.Root, TestCancelled);

        Assert.Equal(history.Root, details.Sha);
        Assert.Empty(details.Parents);
        Assert.Equal(TempRepo.AuthorName, details.AuthorName);
        Assert.Equal(TempRepo.AuthorEmail, details.AuthorEmail);
        Assert.Equal(TempRepo.AuthorName, details.CommitterName);
        Assert.Equal(TempRepo.AuthorEmail, details.CommitterEmail);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), details.AuthorDate);
        Assert.Equal(details.AuthorDate, details.CommitDate);
        Assert.Equal("Start", details.Subject);
        Assert.Equal(string.Empty, details.Body);
        Assert.Equal(
            [
                new ChangedFile("deleted.txt", FileChangeKind.Added),
                new ChangedFile("modified.txt", FileChangeKind.Added),
                new ChangedFile("old/name.txt", FileChangeKind.Added),
            ],
            details.Files);
    }

    [Fact]
    public async Task Lists_added_modified_deleted_and_renamed_files()
    {
        var details = await history.Repository.ReadCommitDetailsAsync(history.Changes, TestCancelled);

        Assert.Equal([history.Root], details.Parents);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero), details.CommitDate);
        Assert.Equal(
            [
                new ChangedFile("added.txt", FileChangeKind.Added),
                new ChangedFile("deleted.txt", FileChangeKind.Deleted),
                new ChangedFile("modified.txt", FileChangeKind.Modified),
                new ChangedFile("new/name.txt", FileChangeKind.Renamed, "old/name.txt"),
            ],
            details.Files.OrderBy(file => file.Path, StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_body_keeps_its_paragraphs_and_loses_trailing_blank_lines()
    {
        var details = await history.Repository.ReadCommitDetailsAsync(history.OddNames, TestCancelled);

        Assert.Equal("Odd names", details.Subject);
        Assert.Equal("First paragraph,\nwrapped.\n\nSecond paragraph.", details.Body);
    }

    [Fact]
    public async Task Paths_with_spaces_and_non_ascii_characters_come_through_unchanged()
    {
        var details = await history.Repository.ReadCommitDetailsAsync(history.OddNames, TestCancelled);

        Assert.Equal(
            [new ChangedFile(History.SpacedPath, FileChangeKind.Added), new ChangedFile(History.NonAsciiPath, FileChangeKind.Added)],
            details.Files.OrderBy(file => file.Path, StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_merge_is_compared_with_its_first_parent()
    {
        var details = await history.Repository.ReadCommitDetailsAsync(history.Merge, TestCancelled);

        Assert.Equal(2, details.Parents.Count);
        Assert.Equal(history.MainWork, details.Parents[0]);
        Assert.Equal("Merge branch 'feature'", details.Subject);
        Assert.Equal([new ChangedFile("feature.txt", FileChangeKind.Added)], details.Files);
    }

    [Fact]
    public async Task A_stash_is_compared_with_the_commit_it_was_made_on()
    {
        var details = await history.Repository.ReadCommitDetailsAsync(history.Stash, TestCancelled);

        Assert.Equal(history.Stash, details.Sha);
        Assert.Equal(history.Merge, details.Parents[0]);
        Assert.Equal("On main: Work in progress", details.Subject);
        Assert.Equal([new ChangedFile("main.txt", FileChangeKind.Modified)], details.Files);
    }

    [Fact]
    public async Task An_unknown_commit_is_reported_with_gits_message()
    {
        var exception = await Assert.ThrowsAsync<GitException>(
            () => history.Repository.ReadCommitDetailsAsync(new string('0', 40), TestCancelled));

        Assert.Contains("bad", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("M\0a.txt\0", "a.txt", FileChangeKind.Modified, null)]
    [InlineData("T\0link\0", "link", FileChangeKind.TypeChanged, null)]
    [InlineData("C075\0from.txt\0to.txt\0", "to.txt", FileChangeKind.Copied, "from.txt")]
    [InlineData("U\0conflict.txt\0", "conflict.txt", FileChangeKind.Unknown, null)]
    public void Reads_each_kind_of_name_status_entry(string output, string path, FileChangeKind kind, string? oldPath) =>
        Assert.Equal([new ChangedFile(path, kind, oldPath)], GitRepository.ParseNameStatus(output));
}
