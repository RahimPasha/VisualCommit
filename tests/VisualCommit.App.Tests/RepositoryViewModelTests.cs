using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
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

    private static async Task<RepositoryViewModel> OpenAsync(TempRepo repo, RecordingLog? log = null)
    {
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc), log);
        await viewModel.StartAsync();
        return viewModel;
    }
}
