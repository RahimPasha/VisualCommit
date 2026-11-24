using System.Text.RegularExpressions;
using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>Tests of one open repository's view model against real git and the graph scenario (no UI).</summary>
public class RepositoryViewModelTests
{
    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    [Fact]
    public async Task Opening_the_graph_scenario_loads_its_whole_history_and_refs()
    {
        using var repo = await Scenarios.GraphAsync();
        var log = new RecordingLog();
        using var viewModel = await OpenAsync(repo, log);

        Assert.Equal(13, viewModel.Graph.Count);
        Assert.True(viewModel.Graph.IsComplete);
        Assert.Equal("On main: Work in progress on README", viewModel.Graph.CommitAt(0).Subject);
        Assert.Equal(CommitKind.Stash, viewModel.Graph.CommitAt(0).Kind);
        Assert.Equal("Add settings page", viewModel.Graph.CommitAt(2).Subject);
        Assert.Equal("main", viewModel.BranchText);
        Assert.False(viewModel.HasNoCommits);
        Assert.Null(viewModel.ErrorText);
        Assert.Equal(-1, viewModel.SelectedIndex);
        Assert.Contains(log.Messages, message => message.StartsWith("graph: loaded 13 commits in ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Nothing_is_said_about_commits_before_the_history_is_read()
    {
        using var repo = await Scenarios.LinearAsync();
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));

        // Until StartAsync has read the refs, the graph is the empty placeholder, which must not
        // read as "No commits yet".
        Assert.False(viewModel.HasNoCommits);

        await viewModel.StartAsync();
        Assert.False(viewModel.HasNoCommits);
        Assert.Equal(3, viewModel.Graph.Count);
    }

    [Fact]
    public async Task A_repository_without_commits_says_so()
    {
        using var data = new TempDirectory("fresh");
        var repository = await GitRepository.InitAsync(Runner, data.Combine("fresh"), TestContext.Current.CancellationToken);
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));

        await viewModel.StartAsync();

        Assert.Equal(0, viewModel.Graph.Count);
        Assert.True(viewModel.HasNoCommits);
        Assert.NotEqual(string.Empty, viewModel.BranchText);
    }

    [Fact]
    public async Task Selecting_a_ref_selects_its_row_and_asks_for_it_to_be_revealed()
    {
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        var revealed = new List<int>();
        viewModel.RevealRequested += (_, index) => revealed.Add(index);
        var v01 = (await repo.GitAsync("rev-parse", "v0.1^{commit}")).StandardOutput.Trim();

        viewModel.Select(v01, reveal: true);

        Assert.Equal(11, viewModel.SelectedIndex);
        Assert.Equal([11], revealed);
    }

    [Fact]
    public async Task A_commit_made_outside_the_app_appears_after_a_refresh_and_the_selection_stays()
    {
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        viewModel.SelectedIndex = 2;
        var selected = viewModel.Graph.CommitAt(2).Sha;
        var before = viewModel.Graph;

        await repo.CommitFileAsync("terminal.txt", "from a terminal\n", "Commit from a terminal");
        await viewModel.RefreshAsync();

        Assert.NotSame(before, viewModel.Graph);
        Assert.Equal(14, viewModel.Graph.Count);
        Assert.Equal("Commit from a terminal", viewModel.Graph.CommitAt(0).Subject);
        Assert.Equal(3, viewModel.SelectedIndex);
        Assert.Equal(selected, viewModel.Graph.CommitAt(3).Sha);
    }

    [Fact]
    public async Task A_refresh_without_changes_keeps_the_graph()
    {
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        var before = viewModel.Graph;

        await viewModel.RefreshAsync();

        Assert.Same(before, viewModel.Graph);
    }

    [Fact]
    public async Task An_outside_commit_logs_one_reload_line_and_a_heap_line_after_each_load()
    {
        // D438: the reload line only at a swap whose refs changed, the heap line at each load's end.
        using var repo = await Scenarios.GraphAsync();
        var log = new RecordingLog();
        using var viewModel = await OpenAsync(repo, log);
        Assert.Equal([HeapLine(13)], HeapLines(log));

        await repo.CommitFileAsync("terminal.txt", "from a terminal\n", "Commit from a terminal");
        await viewModel.RefreshAsync();

        Assert.Equal(14, viewModel.Graph.Count);
        var reload = Assert.Single(log.Entries, entry => entry.Message.Contains(" reloaded ", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Info, reload.Level);
        Assert.Matches(@"^graph: graph reloaded after a change of the refs, 14 commits in \d+ ms\.$", reload.Message);
        Assert.Equal([HeapLine(13), HeapLine(14)], HeapLines(log));
        Assert.All(log.Entries.Where(entry => entry.Message.Contains(": managed heap ", StringComparison.Ordinal)), entry => Assert.Equal(LogLevel.Info, entry.Level));
    }

    [Fact]
    public async Task A_refresh_without_changes_logs_no_reload_line_and_no_heap_line()
    {
        using var repo = await Scenarios.GraphAsync();
        var log = new RecordingLog();
        using var viewModel = await OpenAsync(repo, log);

        await viewModel.RefreshAsync();

        Assert.DoesNotContain(log.Messages, message => message.Contains(" reloaded ", StringComparison.Ordinal));
        Assert.Equal([HeapLine(13)], HeapLines(log));
    }

    [Fact]
    public async Task Commits_that_come_above_the_top_row_in_view_leave_that_row_at_the_top()
    {
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        Assert.False(viewModel.HasWorkingChanges);
        viewModel.ScrollOffset = (5 * RowHeight) + 10;
        var top = viewModel.Graph.CommitAt(5).Sha;

        await repo.CommitFileAsync("one.txt", "one\n", "First from a terminal");
        await repo.CommitFileAsync("two.txt", "two\n", "Second from a terminal");
        await viewModel.RefreshAsync();

        // Two rows more above it: the offset follows by two rows, its part of a row kept.
        Assert.Equal(15, viewModel.Graph.Count);
        Assert.Equal(7, viewModel.Graph.IndexOf(top));
        Assert.Equal((7 * RowHeight) + 10, viewModel.ScrollOffset);
        Assert.Equal(top, TopCommit(viewModel));
    }

    [Fact]
    public async Task Commits_that_go_above_the_top_row_in_view_leave_that_row_at_the_top()
    {
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        await repo.CommitFileAsync("one.txt", "one\n", "First from a terminal");
        await viewModel.RefreshAsync();
        viewModel.ScrollOffset = (6 * RowHeight) + 3;
        var top = TopCommit(viewModel);

        await repo.GitAsync("reset", "--hard", "--quiet", "HEAD~1");
        await viewModel.RefreshAsync();

        Assert.Equal(13, viewModel.Graph.Count);
        Assert.Equal((5 * RowHeight) + 3, viewModel.ScrollOffset);
        Assert.Equal(top, TopCommit(viewModel));
    }

    [Fact]
    public async Task When_the_top_row_in_view_leaves_the_graph_the_offset_stays()
    {
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        await repo.CommitFileAsync("one.txt", "one\n", "First from a terminal");
        await repo.CommitFileAsync("two.txt", "two\n", "Second from a terminal");
        await viewModel.RefreshAsync();
        var gone = (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim();
        viewModel.ScrollOffset = (viewModel.Graph.IndexOf(gone) * RowHeight) + 5;

        await repo.GitAsync("reset", "--hard", "--quiet", "HEAD~2");
        await viewModel.RefreshAsync();

        Assert.Equal(-1, viewModel.Graph.IndexOf(gone));
        Assert.Equal((1 * RowHeight) + 5, viewModel.ScrollOffset);
    }

    [Fact]
    public async Task At_the_top_of_the_graph_new_commits_show_at_the_top()
    {
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        Assert.Equal(0, viewModel.ScrollOffset);

        await repo.CommitFileAsync("one.txt", "one\n", "First from a terminal");
        await viewModel.RefreshAsync();

        Assert.Equal(0, viewModel.ScrollOffset);
        Assert.Equal("First from a terminal", viewModel.Graph.CommitAt(0).Subject);
    }

    [Fact]
    public async Task A_commit_of_every_change_from_a_terminal_while_scrolled_leaves_the_top_row_in_view_at_the_top()
    {
        // The working-changes row goes as the commit comes: the swap follows the commit, the status
        // read takes the row back (D60), and the same commit stays at the top.
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        repo.WriteFile("notes.txt", "A note\n");
        await viewModel.RefreshStatusAsync();
        Assert.True(viewModel.HasWorkingChanges);
        viewModel.ScrollOffset = (6 * RowHeight) + 10;
        var top = TopCommit(viewModel);

        await repo.GitAsync("add", "--all");
        await repo.GitAsync("commit", "--quiet", "-m", "Everything from a terminal");
        await viewModel.RefreshAsync();

        Assert.False(viewModel.HasWorkingChanges);
        Assert.Equal(14, viewModel.Graph.Count);
        Assert.Equal(top, TopCommit(viewModel));
    }

    [Fact]
    public async Task A_commit_made_in_the_app_while_scrolled_leaves_the_top_row_in_view_at_the_top()
    {
        // Part of the changes committed: the working-changes row stays, and so does the top row.
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        repo.WriteFile("staged.txt", "staged\n");
        repo.WriteFile("unstaged.txt", "unstaged\n");
        await repo.GitAsync("add", "staged.txt");
        await viewModel.RefreshStatusAsync();
        viewModel.ScrollOffset = (6 * RowHeight) + 10;
        var top = TopCommit(viewModel);

        Assert.Null(await viewModel.CommitAsync("Staged in the app", amend: false));
        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasWorkingChanges);
        Assert.Equal("Staged in the app", viewModel.Graph.CommitAt(0).Subject);
        Assert.Equal((7 * RowHeight) + 10, viewModel.ScrollOffset);
        Assert.Equal(top, TopCommit(viewModel));
    }

    [Fact]
    public async Task The_anchor_is_the_row_drawn_at_the_top_when_the_offset_rounds_up_to_it()
    {
        // The control draws at the offset rounded to a whole pixel: 0.4 short of a row's top edge
        // draws that row at the top, and the row above it wholly out of view. That row above is a
        // commit only one branch holds; the branch is deleted from a terminal.
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        Assert.False(viewModel.HasWorkingChanges);
        var gone = viewModel.Graph.IndexOf((await repo.GitAsync("rev-parse", "bugfix/crash-on-start")).StandardOutput.Trim());
        Assert.InRange(gone, 0, viewModel.Graph.Count - 2);
        var offset = ((gone + 1) * RowHeight) - 0.4;
        viewModel.ScrollOffset = offset;
        var top = TopCommit(viewModel);
        Assert.Equal(viewModel.Graph.CommitAt(gone + 1).Sha, top);

        await repo.GitAsync("branch", "--quiet", "-D", "bugfix/crash-on-start");
        await viewModel.RefreshAsync();

        Assert.Equal(12, viewModel.Graph.Count);
        Assert.Equal(gone, viewModel.Graph.IndexOf(top));
        Assert.Equal(top, TopCommit(viewModel));
        Assert.Equal(offset - RowHeight, viewModel.ScrollOffset);
    }

    [Fact]
    public async Task An_offset_drawn_as_the_top_of_the_graph_shows_new_commits_at_the_top()
    {
        // Less than half a pixel down is drawn at offset 0: the view is at the top, as at offset 0.
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);
        Assert.False(viewModel.HasWorkingChanges);
        viewModel.ScrollOffset = 0.4;

        await repo.CommitFileAsync("one.txt", "one\n", "First from a terminal");
        await viewModel.RefreshAsync();

        Assert.Equal(0.4, viewModel.ScrollOffset);
        Assert.Equal("First from a terminal", viewModel.Graph.CommitAt(0).Subject);
        Assert.Equal(viewModel.Graph.CommitAt(0).Sha, TopCommit(viewModel));
    }

    [Fact]
    public async Task A_detached_head_shows_in_the_branch_text()
    {
        using var repo = await Scenarios.GraphAsync();
        using var viewModel = await OpenAsync(repo);

        await repo.DetachAsync("v0.2");
        await viewModel.RefreshAsync();

        Assert.Equal("Detached at 2775228", viewModel.BranchText);
    }

    [Fact]
    public async Task The_first_rows_are_logged_once_and_frame_times_when_the_tab_closes()
    {
        using var repo = await Scenarios.GraphAsync();
        var log = new RecordingLog();
        var viewModel = await OpenAsync(repo, log);

        viewModel.OnRowsDrawn();
        viewModel.OnRowsDrawn();
        for (var i = 1; i <= 20; i++)
        {
            viewModel.OnFrameDrawn(TimeSpan.FromMilliseconds(i));
        }

        viewModel.Dispose();

        Assert.Single(log.Messages, message => message.StartsWith("graph: first graph rows drawn after ", StringComparison.Ordinal));
        Assert.Contains("graph: graph frames: 20 drawn, 95th percentile 19.0 ms, longest 20.0 ms", log.Messages);
    }

    /// <summary>The graph's row height, which <see cref="RepositoryViewModel.ScrollOffset"/> counts in.</summary>
    private const double RowHeight = VisualCommit.App.Controls.CommitGraphControl.RowHeight;

    /// <summary>
    /// The commit at the top of the view, as the graph control draws it (its <c>FirstVisibleRow</c>):
    /// the row at the offset rounded to a whole pixel, less the working-changes row when it is shown.
    /// </summary>
    private static string TopCommit(RepositoryViewModel viewModel) =>
        viewModel.Graph.CommitAt((int)Math.Floor(Math.Round(viewModel.ScrollOffset) / RowHeight) - (viewModel.HasWorkingChanges ? 1 : 0)).Sha;

    /// <summary>The graph scenario's heap line after a load of <paramref name="commits"/> commits, as a pattern; its megabytes any whole number.</summary>
    private static string HeapLine(int commits) => $"graph: managed heap N MB after loading {commits} commits.";

    /// <summary>The heap lines in the log, in order, their megabytes written N (<see cref="HeapLine"/>).</summary>
    private static List<string> HeapLines(RecordingLog log) =>
        [.. log.Messages
            .Where(message => message.Contains(": managed heap ", StringComparison.Ordinal))
            .Select(message => Regex.Replace(message, @"^graph: managed heap \d+ MB ", "graph: managed heap N MB "))];

    private static async Task<RepositoryViewModel> OpenAsync(TempRepo repo, RecordingLog? log = null)
    {
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc), log);
        await viewModel.StartAsync();
        return viewModel;
    }
}
