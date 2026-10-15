using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Shell;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the graph hides and solos, in the repository's view model on the filters scenario without
/// a window (docs/phase-5-design.md 8.1 to 8.4, D159, D178): the operations writing the session
/// and the graph reloading through the refresh with the filter's key in its comparison and
/// <c>IsReloadingGraph</c> meanwhile, a filter read from the session at opening and corrected by
/// the first read, a hidden remote's branch that a fetch brings, the app's own rename, delete and
/// create, a comparison of branches, and the search and the comparison following a reloaded graph.
/// </summary>
public class RepositoryViewModelFilterTests
{
    private const string Alpha = "refs/heads/feature/alpha";
    private const string OriginAlpha = "refs/remotes/origin/feature/alpha";
    private const string AlphaOne = "7c60fc46a4178e4f2f88df0f73c4b51808be230a";

    [Fact]
    public async Task Hiding_reloads_the_graph_through_the_refresh_and_writes_the_session()
    {
        using var repo = await Scenarios.FiltersAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var repository = app.Repository;
        Assert.Null(repository.GraphFilter);
        Assert.Null(repository.LoadedFilterKey);
        var before = repository.Graph;

        var reloading = new List<bool>();
        repository.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(repository.IsReloadingGraph))
            {
                reloading.Add(repository.IsReloadingGraph);
            }
        };
        await app.Operations.HideBranchAsync(Alpha);

        Assert.NotSame(before, repository.Graph);
        Assert.Equal(12, repository.Graph.Count);
        Assert.Equal(-1, repository.Graph.IndexOf(AlphaOne));
        Assert.Equal([true, false], reloading);
        Assert.Equal(repository.GraphFilter!.Key, repository.LoadedFilterKey);
        Assert.Equal([Alpha, OriginAlpha], repository.Filters!.Hidden);
        var entry = Assert.Single(app.Services.Session.Current.GraphFilters);
        Assert.Equal(repo.Path, entry.RepositoryPath);
        Assert.Equal([Alpha, OriginAlpha], entry.Hidden);
        Assert.Contains(
            app.Calls.Snapshot(),
            call => call.CommandText.EndsWith("--exclude=feature/alpha --branches --exclude=origin/feature/alpha --exclude=origin/HEAD --remotes --tags --ignore-missing HEAD --", StringComparison.Ordinal));

        // A refresh with nothing changed loads nothing again; the menus see the filter.
        var hidden = repository.Graph;
        await repository.RefreshAsync();
        Assert.Same(hidden, repository.Graph);
        Assert.Same(repository.Filters, repository.MenuContext.Filters);

        // The checked-out branch is never hidden (8.1).
        await app.Operations.HideBranchAsync("refs/heads/main");
        Assert.DoesNotContain("refs/heads/main", repository.Filters!.Hidden);

        await app.Operations.ShowAllHiddenBranchesAsync();
        Assert.Null(repository.Filters);
        Assert.Equal(15, repository.Graph.Count);
        Assert.Empty(app.Services.Session.Current.GraphFilters);
    }

    [Fact]
    public async Task A_filter_from_the_session_loads_the_first_graph_and_names_gone_since_are_dropped()
    {
        using var repo = await Scenarios.FiltersAsync();
        var services = await OperationsHarness.OpenAsync(repo);
        var session = services.Services.Session;
        services.Dispose();

        // The session hides feature/alpha and names a branch that no longer exists, and solos only
        // a gone one: solo mode ends at the first read, and the graph loads again (8.4).
        session.Update(state => state with
        {
            GraphFilters =
            [
                new RepositoryGraphFilter(repo.Path, [Alpha, "refs/heads/gone", OriginAlpha], ["refs/heads/gone-too"], [], [new BranchUpstream(Alpha, OriginAlpha)], ["origin"], default),
            ],
        });
        using var app = await OperationsHarness.OpenAsync(repo, session);
        var repository = app.Repository;
        await WaitUntilAsync(() => repository.LoadedFilterKey == repository.GraphFilter?.Key && repository.Graph.IsComplete && !repository.IsReloadingGraph);

        Assert.Equal([Alpha, OriginAlpha], repository.Filters!.Hidden);
        Assert.Empty(repository.Filters.Soloed);
        Assert.Equal(12, repository.Graph.Count);
        Assert.Equal([Alpha, OriginAlpha], Assert.Single(session.Current.GraphFilters).Hidden);

        // The first load named the session's filter before the refs were read (8.2).
        var loads = app.Calls.Snapshot().Where(call => call.CommandText.StartsWith("git log --date-order", StringComparison.Ordinal)).ToList();
        Assert.Contains("--ignore-missing refs/heads/gone-too HEAD --", loads[0].CommandText, StringComparison.Ordinal);
        Assert.Contains("--exclude=feature/alpha --branches", loads[^1].CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hidden_remote_hides_a_branch_a_fetch_brings_and_the_apps_own_branch_changes_keep_the_filter()
    {
        using var repo = await Scenarios.FiltersAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var repository = app.Repository;

        await app.Operations.HideRemoteAsync("origin");
        Assert.Equal(["origin"], repository.Filters!.HiddenRemotes);
        Assert.Contains("refs/remotes/origin/main", repository.Filters.Hidden);

        // A branch the fetch brings is hidden too, at the next read (8.1).
        await repo.GitAsync("update-ref", "refs/remotes/origin/fresh", AlphaOne);
        await repository.RefreshAsync();
        Assert.Contains("refs/remotes/origin/fresh", repository.Filters!.Hidden);
        Assert.Contains("refs/remotes/origin/fresh", Assert.Single(app.Services.Session.Current.GraphFilters).Hidden);
        await app.Operations.ShowRemoteAsync("origin");
        Assert.Null(repository.Filters);

        // Solo mode: a branch the app creates is soloed too; a rename moves its entry, a delete removes it (8.1, 8.4).
        await app.Operations.SoloBranchAsync("refs/heads/feature/beta");
        app.Operations.FollowCreatedBranch("refs/heads/new");
        Assert.Contains("refs/heads/new", repository.Filters!.Soloed);
        app.Operations.FollowRenamedBranch("refs/heads/new", "refs/heads/newer");
        Assert.Contains("refs/heads/newer", repository.Filters!.Soloed);
        app.Operations.FollowDeletedBranch("refs/heads/newer");
        Assert.DoesNotContain("refs/heads/newer", repository.Filters!.Soloed);
        Assert.Same(repository.Filters, Assert.Single(app.Services.Session.Current.GraphFilters));
    }

    [Fact]
    public async Task A_comparison_of_branches_shows_in_the_right_panel_and_ends_on_a_selected_commit_or_the_working_row()
    {
        using var repo = await Scenarios.FiltersAsync();
        repo.WriteFile("scratch.txt", "scratch\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var repository = app.Repository;
        repository.IsWorkingRowSelected = true;

        await app.Operations.CompareBranchesAsync("refs/heads/main", Alpha);
        var comparison = Assert.IsType<CompareViewModel>(repository.MultiSelection);
        Assert.Same(comparison, repository.Comparison);
        Assert.False(repository.IsWorkingRowSelected);
        Assert.True(repository.Details.IsMultiSelection);
        Assert.Equal(("From main", "To feature/alpha"), (comparison.FromText, comparison.ToText));
        Assert.Equal("1 commit only on feature/alpha, 3 only on main", comparison.CountsText);

        // A refresh that leaves nothing selected keeps it; selecting the working row ends it.
        await repository.RefreshAsync();
        Assert.Same(comparison, repository.Comparison);
        repository.IsWorkingRowSelected = true;
        Assert.Null(repository.Comparison);
        Assert.False(repository.Details.IsMultiSelection);

        // A single selected commit ends one too.
        await app.Operations.CompareBranchesAsync("refs/heads/main", Alpha);
        app.Select(repository.Graph.CommitAt(3).Sha);
        Assert.Null(repository.Comparison);
    }

    [Fact]
    public async Task A_search_and_a_comparison_follow_a_graph_reloaded_for_a_filter()
    {
        using var repo = await Scenarios.FiltersAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var repository = app.Repository;
        repository.OpenSearch();
        var search = Assert.IsType<SearchViewModel>(repository.Search);
        search.Text = "Alpha";
        search.Enter(backwards: false);
        await search.Completion;
        Assert.Contains(repository.Graph.IndexOf(AlphaOne), search.MatchRows);

        await app.Operations.HideBranchAsync(Alpha);
        await search.Completion;
        Assert.DoesNotContain(-1, search.MatchRows);
        Assert.All(search.MatchRows, row => Assert.Contains("Alpha", repository.Graph.CommitAt(row).Subject, StringComparison.Ordinal));
        Assert.True(repository.SearchMarks.IsActive);

        search.CloseCommand.Execute(null);
        Assert.Null(repository.Search);
        Assert.False(repository.SearchMarks.IsActive);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var tries = 0; !condition(); tries++)
        {
            Assert.True(tries < 1000, "The condition did not come true.");
            await Task.Delay(10);
        }
    }
}
