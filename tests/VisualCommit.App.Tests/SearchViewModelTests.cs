using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The search's view model (docs/phase-5-design.md 3, D157, D174, report part F's FD1 to FD5) with
/// a fake repository and a clock the test moves: the box's delay by mode, a new text cancelling
/// the search that runs, the count in each of its forms, Enter and Shift+Enter wrapping round, the
/// Commits mode's authors and SHA prefixes matched in memory, the rows of a graph that still loads
/// searched as they come, the results kept by SHA when the graph is replaced, the progress, and a
/// search that git ends early. The tests run on the UI thread, as the app does, so that what the
/// search posts there runs in order when the test pumps it.
/// </summary>
public class SearchViewModelTests
{
    [AvaloniaFact]
    public async Task The_box_searches_after_its_delay_and_a_new_text_cancels_the_search_that_waits()
    {
        var host = new FakeHost(Graph(10));
        var clock = new ManualTimeProvider();
        using var search = new SearchViewModel(host, SearchMode.Commits, clock);
        host.Match = (_, _, sha) => sha == Sha(3);

        search.Text = "alp";
        Assert.True(search.IsPending);
        clock.Advance(TimeSpan.FromMilliseconds(299));
        Pump();
        Assert.Empty(host.Calls);

        // A new key starts the delay again.
        search.Text = "alpha";
        clock.Advance(TimeSpan.FromMilliseconds(299));
        Pump();
        Assert.Empty(host.Calls);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Pump();
        await search.Completion;
        Assert.True(search.IsSettled);
        var call = Assert.Single(host.Calls);
        Assert.Equal((SearchMode.Commits, "alpha"), (call.Mode, call.Text));
        Assert.Equal(Enumerable.Range(0, 10).Select(Sha), call.Shas);
        Assert.Equal([3], search.MatchRows);
        Assert.True(host.Marks.IsActive);
        Assert.True(host.Marks.IsMatch(Sha(3)));
        Assert.Equal("1 match", search.CountText);

        // The Files and Changes modes wait 600 ms (3.1).
        Assert.Equal(TimeSpan.FromMilliseconds(600), SearchViewModel.DelayOf(SearchMode.Files));
        Assert.Equal(TimeSpan.FromMilliseconds(600), SearchViewModel.DelayOf(SearchMode.Changes));
        search.ChooseMode(SearchMode.Files);
        clock.Advance(TimeSpan.FromMilliseconds(599));
        Pump();
        Assert.Single(host.Calls);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Pump();
        await search.Completion;
        Assert.Equal((SearchMode.Files, "alpha"), (host.Calls[1].Mode, host.Calls[1].Text));

        // An empty box clears the marks at once and runs nothing.
        search.Text = string.Empty;
        Assert.False(host.Marks.IsActive);
        Assert.Equal(string.Empty, search.CountText);
        Assert.Empty(search.MatchRows);
        clock.Advance(TimeSpan.FromSeconds(1));
        Pump();
        Assert.Equal(2, host.Calls.Count);
    }

    [AvaloniaFact]
    public async Task The_count_says_how_far_the_search_is_and_where_the_selection_is_among_the_matches()
    {
        var host = new FakeHost(Graph(8));
        var clock = new ManualTimeProvider();
        using var search = new SearchViewModel(host, SearchMode.Commits, clock);
        Assert.Equal(string.Empty, search.CountText);

        // While git runs: "Searching…", then "{n} so far…" as matches are applied.
        var release = new TaskCompletionSource();
        host.Match = (_, _, sha) => sha == Sha(2) || sha == Sha(5);
        host.Blocking = release.Task;
        search.Text = "zzz";
        clock.Advance(TimeSpan.FromMilliseconds(300));
        Pump();
        Assert.True(search.IsSearching);
        Assert.Equal(SearchViewModel.SearchingText, search.CountText);
        clock.Advance(SearchViewModel.BatchInterval);
        Pump();
        await WaitUntilAsync(() => search.CountText == "2 so far…");
        release.SetResult();
        await search.Completion;

        Assert.Equal("2 matches", search.CountText);
        host.SelectedRow = 5;
        search.OnSelectionChanged();
        Assert.Equal("2 of 2", search.CountText);
        host.SelectedRow = 4;
        search.OnSelectionChanged();
        Assert.Equal("2 matches", search.CountText);

        host.Match = (_, _, _) => false;
        host.Blocking = null;
        search.Text = "nothing";
        clock.Advance(TimeSpan.FromMilliseconds(300));
        Pump();
        await search.Completion;
        Assert.Equal(SearchViewModel.NoMatchesText, search.CountText);
        Assert.False(search.NextCommand.CanExecute(null));
        Assert.False(search.PreviousCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Enter_goes_on_from_the_selected_row_and_wraps_round_and_Shift_Enter_goes_back()
    {
        var host = new FakeHost(Graph(10));
        var clock = new ManualTimeProvider();
        using var search = new SearchViewModel(host, SearchMode.Commits, clock);
        host.Search = search;
        host.Match = (_, _, sha) => sha == Sha(2) || sha == Sha(6) || sha == Sha(9);
        search.Text = "zzz";

        // Enter before the delay has passed searches at once (3.1).
        search.Enter(backwards: false);
        await search.Completion;
        Assert.Single(host.Calls);
        Assert.Equal(2, host.SelectedRow);
        Assert.Equal("1 of 3", search.CountText);

        search.Enter(backwards: false);
        Assert.Equal(6, host.SelectedRow);
        search.Enter(backwards: false);
        Assert.Equal(9, host.SelectedRow);
        search.Enter(backwards: false);
        Assert.Equal(2, host.SelectedRow);
        search.Enter(backwards: true);
        Assert.Equal(9, host.SelectedRow);

        // From a row between two matches; from the top, backwards goes to the last.
        host.SelectedRow = 4;
        search.Enter(backwards: true);
        Assert.Equal(2, host.SelectedRow);
        host.SelectedRow = -1;
        search.Enter(backwards: true);
        Assert.Equal(9, host.SelectedRow);
        Assert.Single(host.Calls);
    }

    [AvaloniaFact]
    public async Task The_commits_mode_matches_authors_and_sha_prefixes_itself_and_the_others_do_not()
    {
        var commits = Commits(4).ToList();
        commits[1] = commits[1] with { AuthorName = "Jonas Berg", AuthorEmail = "jonas@example.com" };
        commits[2] = commits[2] with { Sha = "0575c7f6631b3ba6c9d623f53b28489405230cfd" };
        var host = new FakeHost(Graph(commits));
        var clock = new ManualTimeProvider();
        using var search = new SearchViewModel(host, SearchMode.Commits, clock);

        search.Text = "jONAS";
        clock.Advance(TimeSpan.FromMilliseconds(300));
        Pump();
        await search.Completion;
        Assert.Equal([1], search.MatchRows);

        search.Text = "EXAMPLE.COM";
        clock.Advance(TimeSpan.FromMilliseconds(300));
        Pump();
        await search.Completion;
        Assert.Equal([0, 1, 2, 3], search.MatchRows);

        // A SHA prefix of 4 to 40 hex digits, its case ignored; 3 digits are too few.
        search.Text = "0575C";
        clock.Advance(TimeSpan.FromMilliseconds(300));
        Pump();
        await search.Completion;
        Assert.Equal([2], search.MatchRows);
        search.Text = "057";
        clock.Advance(TimeSpan.FromMilliseconds(300));
        Pump();
        await search.Completion;
        Assert.Empty(search.MatchRows);

        // Git's part runs too, and the matches are the union.
        host.Match = (_, _, sha) => sha == commits[3].Sha;
        search.Text = "Jonas";
        clock.Advance(TimeSpan.FromMilliseconds(300));
        Pump();
        await search.Completion;
        Assert.Equal([1, 3], search.MatchRows);

        // The Files mode leaves authors to git.
        host.Match = (_, _, _) => false;
        search.ChooseMode(SearchMode.Files);
        clock.Advance(TimeSpan.FromMilliseconds(600));
        Pump();
        await search.Completion;
        Assert.Empty(search.MatchRows);
        Assert.Equal("File name or path (case ignored for A–Z)", search.Placeholder);
        Assert.Equal("Files", search.ModeLabel);
    }

    [AvaloniaFact]
    public async Task A_replaced_graph_gives_git_only_its_rows_not_searched_yet_and_the_marks_stay()
    {
        var commits = Commits(6).ToList();
        var host = new FakeHost(Graph(commits[1..]));
        var clock = new ManualTimeProvider();
        using var search = new SearchViewModel(host, SearchMode.Changes, clock);
        host.Match = (_, _, sha) => sha == commits[2].Sha || sha == commits[0].Sha;
        search.Text = "zzz";
        clock.Advance(TimeSpan.FromMilliseconds(600));
        Pump();
        await search.Completion;
        Assert.Equal([1], search.MatchRows);

        // A refresh brought a new commit at the top (3.4): the old match keeps its mark on its new row
        // while git searches the new row alone.
        var release = new TaskCompletionSource();
        host.Blocking = release.Task;
        host.Graph = Graph(commits);
        search.OnGraphReplaced();
        Assert.Equal([2], search.MatchRows);
        Assert.Equal("1 so far…", search.CountText);
        release.SetResult();
        await search.Completion;
        Assert.Equal(2, host.Calls.Count);
        Assert.Equal([commits[0].Sha], host.Calls[1].Shas);
        Assert.Equal([0, 2], search.MatchRows);
        Assert.Equal("2 matches", search.CountText);

        // A new text starts over: every row is given again.
        host.Blocking = null;
        search.Text = "y";
        clock.Advance(TimeSpan.FromMilliseconds(600));
        Pump();
        await search.Completion;
        Assert.Equal(6, host.Calls[2].Shas.Count);
    }

    [AvaloniaFact]
    public async Task A_graph_that_still_loads_is_searched_as_its_rows_come()
    {
        var commits = Commits(5).ToList();
        var graph = new CommitGraphData(new RepoRefs(new HeadState("main", commits[0].Sha), [], [], []));
        var layout = new GraphLayout();
        graph.Append(commits[..2], [.. commits[..2].Select(layout.Add)]);
        var host = new FakeHost(graph);
        var clock = new ManualTimeProvider();
        using var search = new SearchViewModel(host, SearchMode.Commits, clock);
        host.Match = (_, _, sha) => sha == commits[3].Sha;
        search.Text = "zzz";
        clock.Advance(TimeSpan.FromMilliseconds(300));
        Pump();
        await WaitUntilAsync(() => host.Calls.Count == 1);
        Assert.True(search.IsSearching);
        Assert.False(search.IsSettled);

        graph.Append(commits[2..], [.. commits[2..].Select(layout.Add)]);
        graph.Complete();
        await search.Completion;
        Assert.Equal(2, host.Calls.Count);
        Assert.Equal(commits[2..].Select(commit => commit.Sha), host.Calls[1].Shas);
        Assert.Equal([3], search.MatchRows);
        Assert.True(search.IsSettled);
    }

    [AvaloniaFact]
    public async Task A_changes_search_shows_its_progress_and_one_that_git_ends_early_stops_quietly()
    {
        var host = new FakeHost(Graph(4));
        var clock = new ManualTimeProvider();
        using var search = new SearchViewModel(host, SearchMode.Changes, clock);
        var release = new TaskCompletionSource();
        host.Blocking = release.Task;
        host.ReportFirst = 2;
        search.Text = "zzz";
        clock.Advance(TimeSpan.FromMilliseconds(600));
        Pump();
        Assert.True(search.ShowsProgress);
        await WaitUntilAsync(() => search.ProgressValue == 2);
        Assert.Equal("Searched 2 of 4 commits", search.ProgressText);
        release.SetResult();
        await search.Completion;
        Assert.False(search.ShowsProgress);

        // Git reported 2 of 4 searched and ended: no further call, the count final.
        Assert.Single(host.Calls);
        Assert.False(search.IsSearching);
        Assert.Equal(SearchViewModel.NoMatchesText, search.CountText);
    }

    [AvaloniaFact]
    public void The_mode_menu_has_its_header_and_three_modes_always_enabled_and_choosing_one_asks_for_the_focus()
    {
        var host = new FakeHost(Graph(1));
        using var search = new SearchViewModel(host, SearchMode.Changes, new ManualTimeProvider());
        Assert.Equal("Changes", search.ModeLabel);
        Assert.Equal("Text added or removed (case ignored for A–Z)", search.Placeholder);

        var menu = search.ModeMenu();
        Assert.Equal("Search", menu.HeaderText);
        Assert.Equal(["Commits: message, author or SHA", "Files: a path or file name", "Changes: text added or removed"], menu.Items.Select(item => item.Label));
        Assert.All(menu.Items, item => Assert.True(item.IsEnabled));

        var focused = 0;
        search.FocusRequested += (_, _) => focused++;
        menu.Items.First().Invoke();
        Assert.Equal(SearchMode.Commits, search.Mode);
        Assert.Equal("Message, author or SHA", search.Placeholder);
        Assert.Equal(1, focused);

        search.CloseCommand.Execute(null);
        Assert.True(host.Closed);
    }

    /// <summary>Runs what was posted to the UI thread, and what that posts in turn.</summary>
    private static void Pump()
    {
        for (var round = 0; round < 5; round++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var tries = 0; !condition(); tries++)
        {
            Assert.True(tries < 500, "The condition did not come true.");
            Pump();
            await Task.Delay(10);
        }
    }

    private static string Sha(int row) => (row + 1).ToString("x40", System.Globalization.CultureInfo.InvariantCulture);

    private static IEnumerable<CommitInfo> Commits(int count)
    {
        var date = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        for (var row = 0; row < count; row++)
        {
            yield return new CommitInfo(Sha(row), row + 1 < count ? [Sha(row + 1)] : [], "Test Author", "author@example.com", date.AddMinutes(-row), date.AddMinutes(-row), $"Commit {row}");
        }
    }

    private static CommitGraphData Graph(int count) => Graph(Commits(count).ToList());

    private static CommitGraphData Graph(IReadOnlyList<CommitInfo> commits)
    {
        var graph = new CommitGraphData(new RepoRefs(new HeadState("main", commits.Count > 0 ? commits[0].Sha : null), [], [], []));
        var layout = new GraphLayout();
        graph.Append(commits, [.. commits.Select(layout.Add)]);
        graph.Complete();
        return graph;
    }

    private sealed class FakeHost(CommitGraphData graph) : ISearchHost
    {
        public CommitGraphData Graph { get; set; } = graph;

        public int SelectedRow { get; set; } = -1;

        public CancellationToken Lifetime => CancellationToken.None;

        public IAppLog Log { get; } = new RecordingLog();

        /// <summary>Whether git matches a SHA for a mode and text.</summary>
        public Func<SearchMode, string, string, bool> Match { get; set; } = (_, _, _) => false;

        /// <summary>Holds each search after it has handed over its matches, until it completes.</summary>
        public Task? Blocking { get; set; }

        /// <summary>How many rows the first search reports searched before it is released and ends early; null reports all.</summary>
        public int? ReportFirst { get; set; }

        public List<(SearchMode Mode, string Text, IReadOnlyList<string> Shas)> Calls { get; } = [];

        public GraphSearchMarks Marks { get; private set; } = GraphSearchMarks.None;

        public bool Closed { get; private set; }

        public async Task SearchAsync(SearchMode mode, string text, IReadOnlyList<string> shas, Action<string> onMatch, IProgress<int>? progress, CancellationToken cancellationToken)
        {
            Calls.Add((mode, text, [.. shas]));
            foreach (var sha in shas.Where(sha => Match(mode, text, sha)))
            {
                onMatch(sha);
            }

            if (ReportFirst is { } first)
            {
                progress?.Report(first);
            }

            if (Blocking is { } blocking)
            {
                await blocking.WaitAsync(cancellationToken);
            }

            if (ReportFirst is null)
            {
                progress?.Report(shas.Count);
            }
        }

        /// <summary>The search, which the repository tells of a change of selection.</summary>
        public SearchViewModel? Search { get; set; }

        public void SelectMatch(int row)
        {
            SelectedRow = row;
            Search?.OnSelectionChanged();
        }

        public void ShowMarks(GraphSearchMarks marks) => Marks = marks;

        public void CloseSearch() => Closed = true;
    }
}
