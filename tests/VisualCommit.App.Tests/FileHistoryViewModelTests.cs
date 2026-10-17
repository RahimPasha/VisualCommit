using VisualCommit.App.ViewModels.Pages;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.App.Tests.PageFakes;

namespace VisualCommit.App.Tests;

/// <summary>
/// The file history page's view model without a UI (docs/phase-5-design.md 4.1, 11.3; report
/// part B's decisions BD2, BD13, BD24): the rows as git hands them over, the first selected
/// without touching the right panel, the user's selection showing its commit, the diff part
/// reused with each row's side label, the texts, "Blame", "Show in graph", and the messages.
/// </summary>
public class FileHistoryViewModelTests
{
    [Fact]
    public async Task The_rows_come_as_git_lists_them_and_the_first_fills_only_the_diff_part()
    {
        var repository = new PageFakeRepository();
        repository.History.AddRange(ReaderHistory());
        var host = new RecordingPageHost(repository);
        using var page = new FileHistoryViewModel(host, new FileHistoryRequest("src/Reader.cs", "HEAD"));
        var focus = 0;
        page.FocusRequested += (_, _) => focus++;

        await page.LoadAsync();

        Assert.Equal(("History of Reader.cs", "Reader.cs", "src", true), (page.Title, page.FileName, page.Folder, page.HasFolder));
        Assert.Equal(HeadSha, page.StartSha);
        Assert.Null(page.FromText);
        Assert.False(page.IsLoading);
        Assert.False(page.ShowsLoading);
        Assert.Null(page.Message);

        // BD13: the secondary text, the first that applies.
        Assert.Equal(
            [
                ("c0bc2dd Read whole lines", "M", string.Empty, "Jonas Berg", "2026-01-01 12:13"),
                ("0575c7f Rename the parser to reader", "R", "renamed from src/Parser.cs", "Mira Kovac", "2026-01-01 12:06"),
                ("365482b Handle empty input", "M", "as src/Parser.cs", "Jonas Berg", "2026-01-01 12:03"),
                ("1d5a7cf Add the parser", "A", "added", "Mira Kovac", "2026-01-01 12:01"),
            ],
            page.Rows.Select(row => (row.Name, row.StatusLetter, row.SecondaryText, row.Author, row.DateText)));

        // The first row is selected and its change shown; the right panel keeps what it showed.
        Assert.Same(page.Rows[0], page.SelectedRow);
        Assert.Empty(host.Shown);
        Assert.Equal(1, focus);
        var diff = Assert.IsType<ViewModels.Diff.DiffViewModel>(page.Diff);
        Assert.Equal(("c0bc2dd", false), (diff.SideLabel, diff.ShowsClose));
        Assert.Equal(new DiffTarget(DiffSide.Commit, "src/Reader.cs", FileChangeKind.Modified, null, Commit: HeadSha, Parent: ParentSha), Assert.Single(repository.Diffs));
        Assert.True(page.BlameCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_row_the_user_picks_shows_its_commit_and_reuses_the_diff_part_with_its_paths()
    {
        var repository = new PageFakeRepository();
        repository.History.AddRange(ReaderHistory());
        var host = new RecordingPageHost(repository);
        using var page = new FileHistoryViewModel(host, new FileHistoryRequest("src/Reader.cs", "HEAD"));
        await page.LoadAsync();
        var diff = page.Diff;

        page.SelectedRow = page.Rows[1];

        Assert.Equal([RenameSha], host.Shown);
        Assert.Same(diff, page.Diff);
        Assert.Equal("0575c7f", page.Diff!.SideLabel);
        Assert.Equal(new DiffTarget(DiffSide.Commit, "src/Reader.cs", FileChangeKind.Renamed, "src/Parser.cs", Commit: RenameSha, Parent: AddTracingSha), repository.Diffs[^1]);

        // "Blame" blames the selected row's version (4.1); a deleted file's, its parent's.
        page.BlameCommand.Execute(null);
        Assert.Equal([new BlameRequest("src/Reader.cs", RenameSha)], host.Blames);
    }

    [Fact]
    public async Task A_history_from_another_commit_says_from_and_one_from_HEADs_own_commit_does_not()
    {
        var repository = new PageFakeRepository();
        repository.History.AddRange(ReaderHistory().Skip(1));
        var host = new RecordingPageHost(repository);

        using var fromRename = new FileHistoryViewModel(host, new FileHistoryRequest("src/Reader.cs", RenameSha));
        await fromRename.LoadAsync();
        Assert.Equal(("from 0575c7f", true), (fromRename.FromText, fromRename.HasFrom));
        Assert.False(repository.Reads.ContainsKey("resolve"));

        // BD24: HEAD's own commit by its full id is no "from".
        using var fromHead = new FileHistoryViewModel(host, new FileHistoryRequest("src/Reader.cs", HeadSha));
        await fromHead.LoadAsync();
        Assert.Null(fromHead.FromText);
    }

    [Fact]
    public async Task No_row_says_no_commit_touched_the_file_and_a_failure_shows_gits_message()
    {
        var repository = new PageFakeRepository();
        var host = new RecordingPageHost(repository);
        using var empty = new FileHistoryViewModel(host, new FileHistoryRequest("docs/new.md", "HEAD"));

        await empty.LoadAsync();

        Assert.Equal(("No commit has touched new.md yet", false), (empty.Message, empty.IsError));
        Assert.False(empty.BlameCommand.CanExecute(null));
        Assert.Null(empty.Diff);

        repository.HistoryFailure = new GitException("git log", 128, "fatal: your current branch 'main' does not have any commits yet\n");
        using var failed = new FileHistoryViewModel(host, new FileHistoryRequest("a.txt", "HEAD"));
        await failed.LoadAsync();
        Assert.Equal(("fatal: your current branch 'main' does not have any commits yet", true), (failed.Message, failed.IsError));
    }

    [Fact]
    public async Task Show_in_graph_and_the_rows_menu_go_by_whether_the_graph_has_the_commit()
    {
        var repository = new PageFakeRepository();
        repository.History.AddRange(ReaderHistory());
        var host = new RecordingPageHost(repository);
        host.InGraph.Add(AddParserSha);
        using var page = new FileHistoryViewModel(host, new FileHistoryRequest("src/Reader.cs", "HEAD"));
        await page.LoadAsync();

        page.ShowInGraph(page.Rows[2]);
        page.ShowInGraph(page.Rows[3]);

        Assert.Equal([AddParserSha], host.ShownInGraph);
        Assert.Equal("Show in graph (disabled: \"This commit is not in the graph\")", RepositoryMenusTests.Lines(page.MenuFor(page.Rows[2]))[1]);
        Assert.Equal("Show in graph", RepositoryMenusTests.Lines(page.MenuFor(page.Rows[3]))[1]);

        page.Close();
        Assert.Equal([page], host.Closed);
    }

    [Fact]
    public void The_columns_follow_the_lists_width_and_the_page_lies_side_by_side_from_760()
    {
        var host = new RecordingPageHost(new PageFakeRepository());
        using var page = new FileHistoryViewModel(host, new FileHistoryRequest("src/Reader.cs", "HEAD"));

        // BD2: at 440 the author and the date are hidden, at 567 the date.
        page.ListWidth = 440;
        Assert.Equal((true, false, false), (page.ShowsSecondary, page.ShowsAuthor, page.ShowsDate));
        page.ListWidth = 567;
        Assert.Equal((true, true, false), (page.ShowsSecondary, page.ShowsAuthor, page.ShowsDate));
        page.ListWidth = 628;
        Assert.Equal((true, true, true), (page.ShowsSecondary, page.ShowsAuthor, page.ShowsDate));
        page.ListWidth = 401;
        Assert.False(page.ShowsSecondary);
        Assert.Equal(760, FileHistoryViewModel.SideBySideWidth);
    }

    [Fact]
    public async Task Closing_the_page_cancels_its_read_and_it_shows_nothing_more()
    {
        var repository = new PageFakeRepository();
        repository.History.AddRange(ReaderHistory());
        var host = new RecordingPageHost(repository);
        var page = new FileHistoryViewModel(host, new FileHistoryRequest("src/Reader.cs", "HEAD"));

        page.Dispose();
        await page.LoadAsync();

        Assert.True(page.IsClosed);
        Assert.Empty(page.Rows);
        Assert.Null(page.Diff);
    }

    /// <summary>
    /// Step 25's review: each row was one job on the UI thread and one change of the list, so a
    /// file with thousands of commits kept the window from taking input until its last row came.
    /// The rows that come while the UI thread is busy now wait for one job, which adds them all
    /// with one change, the first selected as before.
    /// </summary>
    [Fact]
    public async Task Rows_that_come_while_the_ui_thread_is_busy_reach_the_list_in_one_job_and_one_change()
    {
        var repository = new PageFakeRepository();
        repository.History.AddRange(Enumerable.Range(1, 2_000).Select(index => new FileHistoryRow(
            Commit(index.ToString("x40", System.Globalization.CultureInfo.InvariantCulture), "Jonas Berg", index % 60, ParentSha),
            FileChangeKind.Modified,
            "src/Reader.cs")));
        var host = new RecordingPageHost(repository);
        var ui = new QueuedContext();
        var previous = SynchronizationContext.Current;
        FileHistoryViewModel page;
        SynchronizationContext.SetSynchronizationContext(ui);
        try
        {
            page = new FileHistoryViewModel(host, new FileHistoryRequest("src/Reader.cs", "HEAD"));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        using (page)
        {
            // git's reader runs on a pool thread; the UI thread runs nothing until the test pumps it.
            await Task.Run(page.LoadAsync, TestContext.Current.CancellationToken);
            Assert.Empty(page.Rows);
            Assert.Equal(2, ui.Waiting);

            var changes = 0;
            page.Rows.CollectionChanged += (_, _) => changes++;
            ui.RunAll();

            Assert.Equal(2_000, page.Rows.Count);
            Assert.Equal(1, changes);
            Assert.Same(page.Rows[0], page.SelectedRow);
            Assert.Empty(host.Shown);
            Assert.False(page.IsLoading);
        }
    }

    /// <summary>A UI thread that runs what is posted to it only when the test says so, on the test's thread.</summary>
    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posted = new();

        public int Waiting => _posted.Count;

        public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

        public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

        public void RunAll()
        {
            while (_posted.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }
}
