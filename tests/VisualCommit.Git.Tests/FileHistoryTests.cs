using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitRepository.ReadFileHistoryAsync"/> (docs/phase-5-design.md 4.1, 0.1): the commits
/// that touched a file, newest first, following renames and copies, without merges, from a fixed
/// start; and <see cref="GitRepository.ReadTreeFilesAsync"/> (4.5), every file of a commit's tree.
/// </summary>
public class FileHistoryTests(FileHistoryTests.History history) : IClassFixture<FileHistoryTests.History>
{
    /// <summary>One repository with a commit of each kind the history meets, built once for the tests that only read it.</summary>
    public sealed class History : IAsyncLifetime
    {
        /// <summary>Eight lines, so that rename detection recognises the file after a move or a small edit.</summary>
        public const string LongText = "one\ntwo\nthree\nfour\nfive\nsix\nseven\neight\n";

        /// <summary>Characters without a decomposed form, so that a file system that normalises names (macOS) cannot change them.</summary>
        public const string NonAsciiPath = "日本語/ßΩ 名前.txt";

        public TempRepo Repo { get; private set; } = null!;

        public GitRepository Repository { get; private set; } = null!;

        /// <summary>The root commit: adds <c>notes/a.txt</c>, <c>other.txt</c>, <c>pages/[id].txt</c> and <c>pages/d.txt</c>.</summary>
        public string Root { get; private set; } = string.Empty;

        /// <summary>Edits <c>notes/a.txt</c>.</summary>
        public string Edit { get; private set; } = string.Empty;

        /// <summary>Renames <c>notes/a.txt</c> to <c>notes/b.txt</c>, unchanged.</summary>
        public string PureRename { get; private set; } = string.Empty;

        /// <summary>Renames <c>notes/b.txt</c> to <c>notes/c.txt</c> with one line changed.</summary>
        public string EditedRename { get; private set; } = string.Empty;

        /// <summary>On the branch <c>feature</c>: edits <c>notes/c.txt</c>.</summary>
        public string Feature { get; private set; } = string.Empty;

        /// <summary>Edits <c>pages/d.txt</c> only.</summary>
        public string Pages { get; private set; } = string.Empty;

        /// <summary>Copies <c>other.txt</c> to <c>copy.txt</c>, leaving the source alone.</summary>
        public string Copy { get; private set; } = string.Empty;

        /// <summary>Merges <c>feature</c>.</summary>
        public string Merge { get; private set; } = string.Empty;

        /// <summary>Deletes <c>notes/c.txt</c> and adds an unrelated <c>notes/d.txt</c>: a rename below 50 % similarity.</summary>
        public string Replaced { get; private set; } = string.Empty;

        /// <summary>Adds <c>again.txt</c>, which <see cref="Deleted"/> deletes and <see cref="ReAdded"/> adds again.</summary>
        public string FirstLife { get; private set; } = string.Empty;

        public string Deleted { get; private set; } = string.Empty;

        public string ReAdded { get; private set; } = string.Empty;

        /// <summary>Adds <see cref="NonAsciiPath"/>, which <see cref="NonAsciiEdit"/> edits.</summary>
        public string NonAscii { get; private set; } = string.Empty;

        public string NonAsciiEdit { get; private set; } = string.Empty;

        public async ValueTask InitializeAsync()
        {
            Repo = await TempRepo.CreateAsync("file-history");
            Repo.WriteFile("notes/a.txt", LongText)
                .WriteFile("other.txt", "alpha\nbeta\ngamma\ndelta\n")
                .WriteFile("pages/[id].txt", "id\n")
                .WriteFile("pages/d.txt", "d\n");
            Root = await Repo.CommitAsync("Start");
            Edit = await Repo.CommitFileAsync("notes/a.txt", LongText + "nine\n", "Edit a");
            Repo.DeleteFile("notes/a.txt").WriteFile("notes/b.txt", LongText + "nine\n");
            PureRename = await Repo.CommitAsync("Rename a to b");
            Repo.DeleteFile("notes/b.txt").WriteFile("notes/c.txt", LongText.Replace("five", "FIVE", StringComparison.Ordinal) + "nine\n");
            EditedRename = await Repo.CommitAsync("Rename b to c with an edit");

            await Repo.CreateBranchAsync("feature");
            await Repo.CheckoutAsync("feature");
            Feature = await Repo.CommitFileAsync("notes/c.txt", LongText.Replace("five", "FIVE", StringComparison.Ordinal) + "nine\nten\n", "Feature edits c");
            await Repo.CheckoutAsync("main");

            Pages = await Repo.CommitFileAsync("pages/d.txt", "d, edited\n", "Edit d");
            Copy = await Repo.CommitFileAsync("copy.txt", "alpha\nbeta\ngamma\ndelta\n", "Copy other");
            Merge = await Repo.MergeAsync("feature");
            Repo.DeleteFile("notes/c.txt").WriteFile("notes/d.txt", "something\nquite\ndifferent\n");
            Replaced = await Repo.CommitAsync("Replace c by d");

            FirstLife = await Repo.CommitFileAsync("again.txt", "first life\n", "Add again");
            Repo.DeleteFile("again.txt");
            Deleted = await Repo.CommitAsync("Delete again");
            ReAdded = await Repo.CommitFileAsync("again.txt", "second life\n", "Add again, again");

            NonAscii = await Repo.CommitFileAsync(NonAsciiPath, "名前\n", "Add a non-ASCII name");
            NonAsciiEdit = await Repo.CommitFileAsync(NonAsciiPath, "名前\n二\n", "Edit the non-ASCII name");

            Repository = await OpenAsync(Repo);
        }

        public ValueTask DisposeAsync()
        {
            Repo?.Dispose();
            return ValueTask.CompletedTask;
        }

        public async Task<string> HeadAsync() => await Repo.HeadAsync();
    }

    private static async Task<List<FileHistoryRow>> ReadAsync(GitRepository repository, string start, string path)
    {
        var rows = new List<FileHistoryRow>();
        await repository.ReadFileHistoryAsync(start, path, rows.Add, TestCancelled);
        return rows;
    }

    /// <summary>Each row as (commit, status, path, old path), for comparison.</summary>
    private static List<(string Sha, FileChangeKind Status, string Path, string? OldPath)> Shape(IEnumerable<FileHistoryRow> rows) =>
        [.. rows.Select(row => (row.Commit.Sha, row.Status, row.Path, row.OldPath))];

    [Fact]
    public async Task Follows_renames_from_a_deletion_back_to_the_first_commit_without_the_merge()
    {
        var rows = await ReadAsync(history.Repository, await history.HeadAsync(), "notes/c.txt");

        // A deleted file's history starts at its deletion; the merge that brought the feature's
        // edit is not listed, the edit itself is; R and C rows carry the old path.
        Assert.Equal(
            [
                (history.Replaced, FileChangeKind.Deleted, "notes/c.txt", null),
                (history.Feature, FileChangeKind.Modified, "notes/c.txt", null),
                (history.EditedRename, FileChangeKind.Renamed, "notes/c.txt", "notes/b.txt"),
                (history.PureRename, FileChangeKind.Renamed, "notes/b.txt", "notes/a.txt"),
                (history.Edit, FileChangeKind.Modified, "notes/a.txt", null),
                (history.Root, FileChangeKind.Added, "notes/a.txt", null),
            ],
            Shape(rows));
        Assert.DoesNotContain(rows, row => row.Commit.Sha == history.Merge);
    }

    [Fact]
    public async Task Each_row_carries_the_commit_as_the_graph_reads_it()
    {
        var rows = await ReadAsync(history.Repository, await history.HeadAsync(), "notes/c.txt");

        var feature = rows.Single(row => row.Commit.Sha == history.Feature).Commit;
        Assert.Equal([history.EditedRename], feature.Parents);
        Assert.Equal("Feature edits c", feature.Subject);
        Assert.Equal(TempRepo.AuthorName, feature.AuthorName);
        Assert.Equal(TempRepo.AuthorEmail, feature.AuthorEmail);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 4, 0, TimeSpan.Zero), feature.AuthorDate);
        Assert.Empty(rows.Single(row => row.Commit.Sha == history.Root).Commit.Parents);
    }

    [Fact]
    public async Task Starts_at_the_commit_it_is_given_with_the_path_there()
    {
        var rows = await ReadAsync(history.Repository, history.PureRename, "notes/b.txt");

        Assert.Equal(
            [
                (history.PureRename, FileChangeKind.Renamed, "notes/b.txt", "notes/a.txt"),
                (history.Edit, FileChangeKind.Modified, "notes/a.txt", null),
                (history.Root, FileChangeKind.Added, "notes/a.txt", null),
            ],
            Shape(rows));
    }

    [Fact]
    public async Task A_rename_below_half_similarity_is_an_addition_and_the_older_history_is_not_reached()
    {
        var rows = await ReadAsync(history.Repository, await history.HeadAsync(), "notes/d.txt");

        Assert.Equal([(history.Replaced, FileChangeKind.Added, "notes/d.txt", (string?)null)], Shape(rows));
    }

    [Fact]
    public async Task A_copy_goes_on_with_the_history_of_its_source()
    {
        var rows = await ReadAsync(history.Repository, await history.HeadAsync(), "copy.txt");

        Assert.Equal(
            [
                (history.Copy, FileChangeKind.Copied, "copy.txt", "other.txt"),
                (history.Root, FileChangeKind.Added, "other.txt", null),
            ],
            Shape(rows));
    }

    [Fact]
    public async Task A_file_deleted_and_added_again_lists_both_lives()
    {
        var rows = await ReadAsync(history.Repository, await history.HeadAsync(), "again.txt");

        Assert.Equal(
            [
                (history.ReAdded, FileChangeKind.Added, "again.txt", null),
                (history.Deleted, FileChangeKind.Deleted, "again.txt", null),
                (history.FirstLife, FileChangeKind.Added, "again.txt", (string?)null),
            ],
            Shape(rows));
    }

    [Fact]
    public async Task A_path_with_brackets_is_taken_literally()
    {
        // Without GIT_LITERAL_PATHSPECS (D74) the brackets would match pages/d.txt, and its edit would be listed.
        var rows = await ReadAsync(history.Repository, await history.HeadAsync(), "pages/[id].txt");

        Assert.Equal([(history.Root, FileChangeKind.Added, "pages/[id].txt", (string?)null)], Shape(rows));
    }

    [Fact]
    public async Task A_non_ascii_path_comes_back_as_it_is()
    {
        var rows = await ReadAsync(history.Repository, await history.HeadAsync(), History.NonAsciiPath);

        Assert.Equal(
            [
                (history.NonAsciiEdit, FileChangeKind.Modified, History.NonAsciiPath, null),
                (history.NonAscii, FileChangeKind.Added, History.NonAsciiPath, (string?)null),
            ],
            Shape(rows));
    }

    [Theory]
    [InlineData("untracked.txt")]
    [InlineData("never/was/here.txt")]
    public async Task A_path_no_commit_has_touched_gives_no_row(string path)
    {
        history.Repo.WriteFile("untracked.txt", "not committed\n");
        try
        {
            Assert.Empty(await ReadAsync(history.Repository, await history.HeadAsync(), path));
        }
        finally
        {
            history.Repo.DeleteFile("untracked.txt");
        }
    }

    [Fact]
    public async Task A_start_that_names_no_commit_throws_with_gits_message()
    {
        var ex = await Assert.ThrowsAsync<GitException>(() => ReadAsync(history.Repository, new string('1', 40), "other.txt"));

        Assert.Equal(128, ex.ExitCode);
        Assert.Contains("1111111111111111111111111111111111111111", ex.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_staged_rename_has_no_history_under_its_new_name_and_an_unborn_head_throws()
    {
        // The menus open a staged rename's history under its old path (4.4).
        using var repo = await TempRepo.CreateAsync("history-staged");
        var commit = await repo.CommitFileAsync("old.txt", "one\n", "Add old");
        await repo.GitAsync("mv", "old.txt", "new.txt");
        var repository = await OpenAsync(repo);

        Assert.Empty(await ReadAsync(repository, commit, "new.txt"));
        Assert.Equal([(commit, FileChangeKind.Added, "old.txt", (string?)null)], Shape(await ReadAsync(repository, commit, "old.txt")));

        using var unborn = await TempRepo.CreateAsync("history-unborn");
        var unbornRepository = await OpenAsync(unborn);
        var ex = await Assert.ThrowsAsync<GitException>(() => ReadAsync(unbornRepository, "HEAD", "f.txt"));
        Assert.Equal(128, ex.ExitCode);
    }

    [Fact]
    public async Task A_start_that_looks_like_an_option_is_refused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ReadAsync(history.Repository, "--all", "other.txt"));
    }

    [Fact]
    public async Task Cancelling_stops_the_read_and_hands_over_no_more_rows()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);
        var rows = new List<FileHistoryRow>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => history.Repository.ReadFileHistoryAsync(
            history.ReAdded,
            "notes/a.txt",
            row =>
            {
                rows.Add(row);
                cancel.Cancel();
            },
            cancel.Token));

        // The first row cancelled; nothing came after it.
        Assert.Single(rows);
    }

    [Fact]
    public async Task The_root_commit_is_listed_when_log_show_root_is_off()
    {
        using var repo = await TempRepo.CreateAsync("history-root");
        var root = await repo.CommitFileAsync("f.txt", "one\n", "Add f");
        var edit = await repo.CommitFileAsync("f.txt", "two\n", "Edit f");
        await repo.ConfigAsync("log.showRoot", "false");
        var repository = await OpenAsync(repo);

        Assert.Equal(
            [(edit, FileChangeKind.Modified, "f.txt", null), (root, FileChangeKind.Added, "f.txt", (string?)null)],
            Shape(await ReadAsync(repository, edit, "f.txt")));
    }

    [Fact]
    public async Task Paths_with_a_line_feed_or_the_look_of_a_header_are_read_whole()
    {
        // Neither name fits a Windows folder, so the history is imported without a working tree.
        // The runner splits git's output at line feeds; the reader joins the pieces again, and
        // consumes paths by count, so a path that starts like a commit header stays a path.
        using var repo = await TempRepo.CreateAsync("history-names");

        // Newer Git for Windows refuses to write such names into a tree unless told they need not fit Windows.
        await repo.ConfigAsync("core.protectNTFS", "false");
        var headerLike = new string('a', 40) + "\u001f" + "x.txt";
        var stream = new HistoryStream();
        var first = stream.Commit("refs/heads/main", "Add both", file: "\"line\\nbreak.txt\"", content: "1\n");
        var second = stream.Commit("refs/heads/main", "Edit the line break", file: "\"line\\nbreak.txt\"", content: "2\n");
        var third = stream.Commit("refs/heads/main", "Add the odd one", file: "\"" + new string('a', 40) + "\\037x.txt\"", content: "3\n");
        var fourth = stream.Commit("refs/heads/main", "Edit the odd one", file: "\"" + new string('a', 40) + "\\037x.txt\"", content: "4\n");
        var marks = await ImportAsync(repo, stream);
        var repository = await OpenAsync(repo);

        Assert.Equal(
            [(marks[second], FileChangeKind.Modified, "line\nbreak.txt", null), (marks[first], FileChangeKind.Added, "line\nbreak.txt", (string?)null)],
            Shape(await ReadAsync(repository, marks[fourth], "line\nbreak.txt")));
        Assert.Equal(
            [(marks[fourth], FileChangeKind.Modified, headerLike, null), (marks[third], FileChangeKind.Added, headerLike, (string?)null)],
            Shape(await ReadAsync(repository, marks[fourth], headerLike)));
    }

    [Fact]
    public void The_reader_joins_the_runners_lines_and_keeps_one_entry_per_commit()
    {
        var a = new string('a', 40);
        var b = new string('b', 40);
        var c = new string('c', 40);
        string Header(string sha, string parent, string subject) =>
            $"{sha}\u001f{parent}\u001fA U Thor\u001fa@example.com\u001f2026-01-01T12:00:00+00:00\u001f2026-01-01T12:00:00Z\u001f{subject}\0";

        // What git prints, as the runner hands it over: split at every line feed, which also
        // splits the path "x\ny" and the line before each commit's entries.
        var stream = Header(a, b, "Third") + "\nR100\0old\0x\ny\0" + "M\0another\0"
            + Header(b, c, "Second") + "\nM\0old\0"
            + Header(c, string.Empty, "First") + "\nA\0old\0";
        var rows = new List<FileHistoryRow>();
        var reader = new GitRepository.FileHistoryReader(text => text, rows.Add);
        foreach (var line in stream.Split('\n'))
        {
            reader.Add(line);
        }

        reader.Finish();

        Assert.Equal(0, reader.Skipped);
        Assert.Equal(
            [(a, FileChangeKind.Renamed, "x\ny", "old"), (b, FileChangeKind.Modified, "old", null), (c, FileChangeKind.Added, "old", (string?)null)],
            Shape(rows));
        Assert.Equal("Third", rows[0].Commit.Subject);
        Assert.Equal([b], rows[0].Commit.Parents);
    }

    [Fact]
    public void The_reader_skips_a_header_it_cannot_read_with_its_entries_and_counts_it()
    {
        var a = new string('a', 40);
        var rows = new List<FileHistoryRow>();
        var reader = new GitRepository.FileHistoryReader(text => text, rows.Add);

        // A header whose parent is no commit id, then a good one.
        reader.Add($"{a}\u001fnot-a-sha\u001fA\u001fa@x\u001f\u001f\u001fBad\0");
        reader.Add($"M\0f.txt\0{a}\u001f\u001fA\u001fa@x\u001f2026-01-01T12:00:00Z\u001f2026-01-01T12:00:00Z\u001fGood\0");
        reader.Add("A\0f.txt\0");
        reader.Finish();

        Assert.Equal(1, reader.Skipped);
        Assert.Equal([(a, FileChangeKind.Added, "f.txt", (string?)null)], Shape(rows));
    }

    [Fact]
    public async Task All_files_lists_every_file_of_the_commits_tree()
    {
        var files = await history.Repository.ReadTreeFilesAsync(history.Copy, TestCancelled);

        Assert.Equal(
            ["copy.txt", "notes/c.txt", "other.txt", "pages/[id].txt", "pages/d.txt"],
            files.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task All_files_keeps_unusual_names_as_they_are_and_reads_a_stash()
    {
        var files = await history.Repository.ReadTreeFilesAsync(history.NonAsciiEdit, TestCancelled);
        Assert.Contains(History.NonAsciiPath, files);

        using var repo = await TempRepo.CreateAsync("tree-stash");
        await repo.CommitFileAsync("f.txt", "one\n", "Add f");
        repo.WriteFile("f.txt", "two\n");
        var stash = await repo.StashAsync("Work");
        var repository = await OpenAsync(repo);

        Assert.Equal(["f.txt"], await repository.ReadTreeFilesAsync(stash, TestCancelled));
    }

    [Fact]
    public async Task All_files_of_a_commit_that_does_not_exist_throws_with_gits_message()
    {
        var ex = await Assert.ThrowsAsync<GitException>(() => history.Repository.ReadTreeFilesAsync(new string('1', 40), TestCancelled));

        Assert.Equal(128, ex.ExitCode);
    }
}
