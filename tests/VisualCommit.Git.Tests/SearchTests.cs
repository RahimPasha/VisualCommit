using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitRepository.SearchAsync"/> (docs/phase-5-design.md 3.2, 3.4, 0.3, D174): git is
/// given the rows' commit ids and prints the matches in their order; each mode's rule, merges and
/// stashes by their first-parent change, the user's settings kept out, the chunks and their
/// progress, and a graph older than its objects.
/// </summary>
public class SearchTests(SearchTests.Rows rows) : IClassFixture<SearchTests.Rows>
{
    /// <summary>A history with one commit for each rule a mode has, built once for the tests that only search it.</summary>
    public sealed class Rows : IAsyncLifetime
    {
        private const string App = "class App\n{\n    void Run() { }\n}\n";

        private const string FixedApp = "class App\n{\n    void Run() { FixBug(); }\n}\n";

        public TempRepo Repo { get; private set; } = null!;

        public GitRepository Repository { get; private set; } = null!;

        public string Root { get; private set; } = string.Empty;

        /// <summary>"Fix crash in parser (issue #12)", with a body; adds <c>FixBug</c> to <c>src/app.cs</c>.</summary>
        public string Fix { get; private set; } = string.Empty;

        /// <summary>Renames <c>src/app.cs</c> to <c>src/Program.cs</c>, unchanged.</summary>
        public string Rename { get; private set; } = string.Empty;

        /// <summary>Adds <c>notes.txt</c> with "foo bar".</summary>
        public string Foo { get; private set; } = string.Empty;

        /// <summary>Changes "foo bar" to "foo baz": "foo" occurs as often as before.</summary>
        public string FooEdit { get; private set; } = string.Empty;

        /// <summary>Deletes <c>docs/Old.txt</c>.</summary>
        public string Delete { get; private set; } = string.Empty;

        /// <summary>"Café menu: Straße".</summary>
        public string Cafe { get; private set; } = string.Empty;

        /// <summary>Adds a binary file holding "binary blob".</summary>
        public string Binary { get; private set; } = string.Empty;

        /// <summary>On <c>side</c>: adds "side text" to <c>f.txt</c>.</summary>
        public string Side { get; private set; } = string.Empty;

        /// <summary>Merges <c>side</c>: its first-parent change is the side's.</summary>
        public string SideMerge { get; private set; } = string.Empty;

        /// <summary>On <c>r</c>: adds <c>r.txt</c>.</summary>
        public string R { get; private set; } = string.Empty;

        /// <summary>Edits <c>f.txt</c> on main while <c>r</c> is apart.</summary>
        public string Q { get; private set; } = string.Empty;

        /// <summary>Merges <c>r</c>: it differs from its second parent in <c>f.txt</c>, but not from its first.</summary>
        public string RMerge { get; private set; } = string.Empty;

        /// <summary>On <c>e</c>: adds <c>e.txt</c>.</summary>
        public string E { get; private set; } = string.Empty;

        /// <summary>Merges <c>e</c> and adds <c>src/evil.cs</c> ("evil merge") in the merge itself.</summary>
        public string EvilMerge { get; private set; } = string.Empty;

        /// <summary>"On main: stash searching foo": adds "stash text" to <c>src/Program.cs</c>.</summary>
        public string Stash { get; private set; } = string.Empty;

        /// <summary>Every row, as the graph would give them, newest first: the stash first.</summary>
        public IReadOnlyList<string> All { get; private set; } = [];

        public async ValueTask InitializeAsync()
        {
            Repo = await TempRepo.CreateAsync("search");
            Repo.WriteFile("src/app.cs", App)
                .WriteFile("docs/Old.txt", "old\n")
                .WriteFile("README.md", "readme\n")
                .WriteFile("src/[id].tsx", "id\n")
                .WriteFile("src/d.tsx", "d\n")
                .WriteFile("f.txt", "base\n");
            Root = await Repo.CommitAsync("Initial commit");
            Fix = await Repo.CommitFileAsync("src/app.cs", FixedApp, "Fix crash in parser (issue #12)\n\nThe Widget had a.b*c in it.");
            Repo.DeleteFile("src/app.cs").WriteFile("src/Program.cs", FixedApp);
            Rename = await Repo.CommitAsync("Rename app to Program");
            Foo = await Repo.CommitFileAsync("notes.txt", "foo bar\n", "Add notes");
            FooEdit = await Repo.CommitFileAsync("notes.txt", "foo baz\n", "Edit notes");
            Repo.DeleteFile("docs/Old.txt");
            Delete = await Repo.CommitAsync("Remove Old.txt");
            Cafe = await Repo.CommitFileAsync("menu.txt", "menu\n", "Café menu: Straße");
            Repo.WriteBytes("img.bin", [0, 1, 2, .. "binary blob"u8.ToArray(), 0, 3]);
            Binary = await Repo.CommitAsync("Add an image");

            await Repo.CreateBranchAsync("side");
            await Repo.CheckoutAsync("side");
            Side = await Repo.CommitFileAsync("f.txt", "base\nside text\n", "Side edits f");
            await Repo.CheckoutAsync("main");
            SideMerge = await Repo.MergeAsync("side");

            await Repo.CreateBranchAsync("r");
            await Repo.CheckoutAsync("r");
            R = await Repo.CommitFileAsync("r.txt", "r\n", "R adds r");
            await Repo.CheckoutAsync("main");
            Q = await Repo.CommitFileAsync("f.txt", "base\nside text\nq\n", "Q edits f");
            RMerge = await Repo.MergeAsync("r");

            await Repo.CreateBranchAsync("e");
            await Repo.CheckoutAsync("e");
            E = await Repo.CommitFileAsync("e.txt", "e\n", "E adds e");
            await Repo.CheckoutAsync("main");
            await Repo.GitAsync("merge", "--quiet", "--no-ff", "--no-commit", "e");
            EvilMerge = await Repo.CommitFileAsync("src/evil.cs", "evil merge\n", "Merge e, with something of its own");

            Repo.WriteFile("src/Program.cs", FixedApp + "stash text\n");
            Stash = await Repo.StashAsync("stash searching foo");

            // A user's pattern type would make "(issue" an error without -F.
            await Repo.ConfigAsync("grep.patternType", "extended");

            All = [Stash, EvilMerge, E, RMerge, Q, R, SideMerge, Side, Binary, Cafe, Delete, FooEdit, Foo, Rename, Fix, Root];
            Repository = await OpenAsync(Repo);
        }

        public ValueTask DisposeAsync()
        {
            Repo?.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<List<string>> SearchAsync(GitRepository repository, SearchMode mode, string text, IReadOnlyList<string> shas, IProgress<int>? progress = null)
    {
        var matches = new List<string>();
        await repository.SearchAsync(mode, text, shas, matches.Add, progress, TestCancelled);
        return matches;
    }

    private Task<List<string>> SearchAsync(SearchMode mode, string text) => SearchAsync(rows.Repository, mode, text, rows.All);

    [Fact]
    public async Task Commits_match_the_whole_message_as_fixed_text_with_case_ignored()
    {
        Assert.Equal([rows.Fix], await SearchAsync(SearchMode.Commits, "CRASH"));
        Assert.Equal([rows.Fix], await SearchAsync(SearchMode.Commits, "widget"));
        Assert.Equal([rows.Fix], await SearchAsync(SearchMode.Commits, "(issue"));
        Assert.Equal([rows.Fix], await SearchAsync(SearchMode.Commits, "a.b*c"));
        Assert.Equal([rows.Cafe], await SearchAsync(SearchMode.Commits, "CAFÉ"));
        Assert.Equal([rows.Stash], await SearchAsync(SearchMode.Commits, "searching"));

        // In the rows' order, merges among them.
        Assert.Equal([rows.EvilMerge, rows.RMerge, rows.SideMerge], await SearchAsync(SearchMode.Commits, "merge"));
    }

    [Fact]
    public async Task Files_match_a_path_containing_the_text_in_the_commits_own_change()
    {
        // The stash by its working-tree change; the rename by both its names.
        Assert.Equal([rows.Stash, rows.Rename], await SearchAsync(SearchMode.Files, "program"));
        Assert.Equal([rows.Rename, rows.Fix, rows.Root], await SearchAsync(SearchMode.Files, "app.cs"));

        // A deleted file's history is found.
        Assert.Equal([rows.Delete, rows.Root], await SearchAsync(SearchMode.Files, "Old.txt"));

        // Brackets are literal; a backslash is a folder separator; a folder's name matches the files under it.
        Assert.Equal([rows.Root], await SearchAsync(SearchMode.Files, "[id]"));
        Assert.Equal([rows.Stash, rows.Rename], await SearchAsync(SearchMode.Files, "SRC\\program.CS"));
        Assert.Equal([rows.Stash, rows.EvilMerge, rows.Rename, rows.Fix, rows.Root], await SearchAsync(SearchMode.Files, "src"));
        Assert.Empty(await SearchAsync(SearchMode.Files, "*.nothing"));
    }

    [Fact]
    public async Task Git_2_55_prints_every_commit_given_to_no_walk_whatever_the_path_so_files_are_matched_by_the_paths_listed()
    {
        // The design's Files command relied on a path filter that Git 2.36 applies to the commits
        // given on standard input and Git 2.55 does not: there it printed all of them. The search
        // asks for the paths of each commit's change instead (--name-only), which both print alike.
        var output = (await rows.Repo.GitWithInputAsync(string.Concat(rows.All.Select(sha => sha + "\n")), "log", "--no-walk=unsorted", "--stdin", "--format=%H", "--", "notes.txt"))
            .StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Seen on these two; a git between them may do either.
        var version = await SequenceTestSupport.GitVersionAsync(rows.Repo);
        if (version is { Major: 2, Minor: 36 })
        {
            Assert.Equal([rows.FooEdit, rows.Foo], output);
        }
        else if (version >= new GitVersion(2, 55, 0))
        {
            Assert.Equal(rows.All, output);
        }

        Assert.Equal([rows.FooEdit, rows.Foo], await SearchAsync(SearchMode.Files, "notes.txt"));
    }

    [Fact]
    public async Task Files_match_a_merge_by_its_change_against_its_first_parent()
    {
        // The side's merge brought f.txt's change into main; r's merge differs from main's line
        // only in r.txt, though from its second parent in f.txt as well.
        Assert.Equal([rows.Q, rows.SideMerge, rows.Side, rows.Root], await SearchAsync(SearchMode.Files, "f.txt"));
        Assert.Equal([rows.EvilMerge], await SearchAsync(SearchMode.Files, "evil"));
    }

    [Fact]
    public async Task Changes_match_a_commit_that_adds_or_removes_the_text()
    {
        Assert.Equal([rows.Fix], await SearchAsync(SearchMode.Changes, "fixbug"));

        // "foo bar" to "foo baz" leaves "foo" as often as before.
        Assert.Equal([rows.Foo], await SearchAsync(SearchMode.Changes, "foo"));
        Assert.Equal([rows.FooEdit], await SearchAsync(SearchMode.Changes, "BAZ"));

        // A merge by its first-parent change: the side's text came into main with it.
        Assert.Equal([rows.SideMerge, rows.Side], await SearchAsync(SearchMode.Changes, "side text"));
        Assert.Equal([rows.EvilMerge], await SearchAsync(SearchMode.Changes, "evil merge"));

        // Binary files are searched; a stash by its working-tree change.
        Assert.Equal([rows.Binary], await SearchAsync(SearchMode.Changes, "binary blob"));
        Assert.Equal([rows.Stash], await SearchAsync(SearchMode.Changes, "stash text"));
    }

    [Fact]
    public async Task Changes_keep_the_users_root_rename_and_textconv_settings_out()
    {
        using var repo = await TempRepo.CreateAsync("search-settings");
        repo.WriteFile(".gitattributes", "*.cs diff=other\n").WriteFile("a.cs", "rooted\nlower\n");
        var root = await repo.CommitAsync("Root");
        repo.DeleteFile("a.cs").WriteFile("b.cs", "rooted\nlower\n");
        var rename = await repo.CommitAsync("Rename");
        await repo.ConfigAsync("log.showRoot", "false");
        await repo.ConfigAsync("diff.renames", "false");
        await repo.ConfigAsync("diff.other.textconv", "sed s/lower/other/");
        var repository = await OpenAsync(repo);

        // Git's own pickaxe reads the root through the textconv, which turns "lower" into "other".
        Assert.Empty((await repo.GitAsync("log", "--format=%H", "--root", "-Slower", root)).StandardOutput);

        // The root is searched; the rename is a rename, not a removal and an addition; the bytes are the file's.
        Assert.Equal([root], await SearchAsync(repository, SearchMode.Changes, "rooted", [rename, root]));
        Assert.Equal([root], await SearchAsync(repository, SearchMode.Changes, "lower", [rename, root]));
        Assert.Equal([root], await SearchAsync(repository, SearchMode.Files, "a.cs", [root]));
    }

    [Fact]
    public async Task Rows_go_to_git_in_chunks_with_the_progress_after_each()
    {
        using var repo = await TempRepo.CreateAsync("search-chunks");
        await ImportAsync(repo, HistoryStream.Line(10));
        var shas = (await repo.GitAsync("log", "--format=%H", "main")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        repository.FirstSearchChunk = 2;
        repository.SearchChunk = 3;
        var progress = new List<int>();
        var before = calls.Snapshot().Count;

        var matches = await SearchAsync(repository, SearchMode.Commits, "commit 1", shas, new SynchronousProgress(progress.Add));

        // "Commit 10" is the newest row, "Commit 1" the oldest.
        Assert.Equal([shas[0], shas[9]], matches);
        Assert.Equal([2, 5, 8, 10], progress);
        Assert.Equal(4, calls.Snapshot().Count - before);
    }

    [Fact]
    public async Task A_commit_git_no_longer_has_ends_the_search_quietly()
    {
        using var repo = await TempRepo.CreateAsync("search-pruned");
        await ImportAsync(repo, HistoryStream.Line(6));
        var shas = (await repo.GitAsync("log", "--format=%H", "main")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        shas.Insert(3, new string('1', 40));
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        repository.FirstSearchChunk = 2;
        repository.SearchChunk = 2;
        var progress = new List<int>();
        var before = calls.Snapshot().Count;

        var matches = await SearchAsync(repository, SearchMode.Commits, "commit", shas, new SynchronousProgress(progress.Add));

        // The first chunk's matches came; the second chunk failed; no third ran.
        Assert.Equal([shas[0], shas[1]], matches);
        Assert.Equal([2], progress);
        Assert.Equal(2, calls.Snapshot().Count - before);
    }

    [Fact]
    public async Task An_empty_text_or_no_rows_runs_no_git_and_a_name_is_no_commit_id()
    {
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), rows.Repo.Path, TestCancelled);
        var before = calls.Snapshot().Count;

        Assert.Empty(await SearchAsync(repository, SearchMode.Changes, string.Empty, rows.All));
        Assert.Empty(await SearchAsync(repository, SearchMode.Commits, "commit", []));
        Assert.Equal(before, calls.Snapshot().Count);

        await Assert.ThrowsAsync<ArgumentException>(() => SearchAsync(repository, SearchMode.Commits, "x", ["--all"]));
    }

    [Fact]
    public async Task A_git_without_diff_merges_searches_changes_with_first_parent_alone()
    {
        var runner = new NoDiffMergesRunner(NewRunner());
        var repository = await GitRepository.OpenAsync(runner, rows.Repo.Path, TestCancelled);
        repository.FirstSearchChunk = 4;

        Assert.Equal([rows.SideMerge, rows.Side], await SearchAsync(repository, SearchMode.Changes, "side text", rows.All));

        // Refused once; every later call leaves the option out.
        Assert.Equal(1, runner.Refused);
        Assert.Equal(2, runner.Searches);
    }

    [Fact]
    public async Task Cancelling_stops_the_search_and_starts_no_other_call()
    {
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), rows.Repo.Path, TestCancelled);
        repository.FirstSearchChunk = 2;
        repository.SearchChunk = 2;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);
        var matches = new List<string>();
        var before = calls.Snapshot().Count;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SearchAsync(
            SearchMode.Commits,
            "e",
            rows.All,
            sha =>
            {
                matches.Add(sha);
                cancel.Cancel();
            },
            null,
            cancel.Token));

        Assert.Single(matches);
        Assert.Equal(1, calls.Snapshot().Count - before);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a*b?c[d]e\\f", "a\\*b\\?c\\[d]e\\\\f")]
    public void A_glob_takes_the_text_literally(string text, string expected)
    {
        Assert.Equal(expected, GitRepository.EscapeGlob(text));
    }

    /// <summary>A progress that reports on the caller's thread, so that the reports are all in when the search returns.</summary>
    private sealed class SynchronousProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }

    /// <summary>A runner that answers the first call with <c>--diff-merges</c> as Git 2.30 does, and counts the searches.</summary>
    private sealed class NoDiffMergesRunner(IGitRunner inner) : IGitRunner
    {
        private int _refused;
        private int _searches;

        public string ExecutablePath => inner.ExecutablePath;

        public int Refused => _refused;

        public int Searches => _searches;

        public Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
        {
            if (command.Arguments.Contains("--diff-merges=first-parent"))
            {
                Interlocked.Increment(ref _refused);
                return Task.FromResult(new GitResult(128, string.Empty, "fatal: unrecognized argument: --diff-merges=first-parent\n", TimeSpan.Zero));
            }

            if (command.Arguments.Contains("--no-walk=unsorted"))
            {
                Interlocked.Increment(ref _searches);
            }

            return inner.RunAsync(command, cancellationToken);
        }
    }
}
