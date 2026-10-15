using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Shell;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// What the graph hides and solos, in the repository's view model on the filters scenario without
/// a window (docs/phase-5-design.md 8.1 to 8.4, D159, D178): the operations writing the session
/// and the graph reloading through the refresh with the filter's key in its comparison and
/// <c>IsReloadingGraph</c> meanwhile, a filter read from the session at opening and corrected by
/// the first read, a hidden remote's branch that a fetch brings, the app's own rename, delete and
/// create; a comparison of branches (6.2, 6.4), which a reload under several selected commits
/// keeps and whose links follow a graph that still loads; and the search and the comparison
/// following a reloaded graph, in the app's window since the search runs on the UI thread.
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
    public async Task A_comparison_of_branches_stays_while_a_reload_maps_two_or_more_selected_commits_again()
    {
        using var repo = await Scenarios.FiltersAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var repository = app.Repository;
        var commits = Enumerable.Range(0, repository.Graph.Count)
            .Select(repository.Graph.CommitAt)
            .Where(commit => commit.Kind == CommitKind.Commit)
            .Select(commit => commit.Sha)
            .Take(3)
            .ToList();

        // Two selected commits compare; a branch comparison then takes their place (6.2).
        app.Select(commits[0], commits[1]);
        Assert.False(repository.Comparison!.IsBranches);
        await app.Operations.CompareBranchesAsync("refs/heads/main", Alpha);
        var branches = repository.Comparison!;
        Assert.True(branches.IsBranches);

        // A commit made outside reloads the graph: the same two on other rows are no selection of
        // the user's, so the branch comparison stays.
        await repo.CommitFileAsync("outside.txt", "outside\n", "Outside work");
        await repository.RefreshAsync();
        Assert.Equal("Outside work", repository.Graph.CommitAt(0).Subject);
        Assert.Equal([commits[0], commits[1]], repository.Selection.Shas);
        Assert.Same(branches, repository.Comparison);
        Assert.True(repository.Details.IsMultiSelection);

        // So with three, whose list it replaced.
        app.Select([.. commits]);
        Assert.IsType<MultiSelectionViewModel>(repository.MultiSelection);
        await app.Operations.CompareBranchesAsync("refs/heads/main", Alpha);
        branches = repository.Comparison!;
        await repo.CommitFileAsync("outside.txt", "again\n", "Outside again");
        await repository.RefreshAsync();
        Assert.Equal("Outside again", repository.Graph.CommitAt(0).Subject);
        Assert.Equal(3, repository.Selection.Count);
        Assert.Same(branches, repository.Comparison);

        // The user's own selection of two ends it.
        app.Select(commits[0], commits[2]);
        Assert.False(repository.Comparison!.IsBranches);
    }

    [Fact]
    public async Task A_comparisons_link_follows_the_pages_of_a_graph_that_still_loads()
    {
        var repository = new PagedRepository();
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        var starting = viewModel.StartAsync();
        repository.Send();
        await repository.Sent.WaitAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => viewModel.Graph.Count == 1 && viewModel.Refs is not null);

        // "Compare old with main" while the first page is all there is: old's commit is no row yet (6.4).
        await viewModel.CompareAsync("refs/heads/main", "refs/heads/old");
        var comparison = viewModel.Comparison!;
        Assert.Equal(("From main", "To old"), (comparison.FromText, comparison.ToText));
        Assert.Equal(PagedRepository.Old.Sha, comparison.ToSha);
        Assert.True(comparison.CanRevealFrom);
        Assert.False(comparison.CanRevealTo);
        Assert.Equal(CompareViewModel.NotInGraphToolTip, comparison.ToLinkToolTip);

        // The next page brings it: the link reveals it, before the graph is complete.
        repository.Send();
        await repository.Sent.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, viewModel.Graph.Count);
        Assert.False(viewModel.Graph.IsComplete);
        Assert.True(comparison.CanRevealTo);
        Assert.Null(comparison.ToLinkToolTip);

        repository.Send();
        await starting;
        Assert.True(viewModel.Graph.IsComplete);
        Assert.True(comparison.CanRevealTo);
    }

    [AvaloniaFact]
    public async Task A_search_and_a_comparison_follow_a_graph_reloaded_for_a_filter()
    {
        // On the UI thread, as the app runs the search: its batches and git's turns take turns there.
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.FiltersAsync();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        repository.OpenSearch();
        var search = Assert.IsType<SearchViewModel>(repository.Search);
        search.Text = "Alpha";
        search.Enter(backwards: false);
        await app.WaitForSearchAsync();
        Assert.Contains(repository.Graph.IndexOf(AlphaOne), search.MatchRows);
        Assert.Contains(repository.SelectedIndex, search.MatchRows);

        // A comparison of feature/alpha, whose commit the hide takes out of the graph.
        await repository.CompareAsync("refs/heads/main", Alpha);
        var comparison = repository.Comparison!;
        Assert.True(comparison.CanRevealTo);

        await repository.Operations.HideBranchAsync(Alpha);
        await app.WaitForGraphReloadAsync();
        await app.WaitForSearchAsync();
        Assert.Equal(-1, repository.Graph.IndexOf(AlphaOne));
        Assert.DoesNotContain(-1, search.MatchRows);
        Assert.All(search.MatchRows, row => Assert.Contains("Alpha", repository.Graph.CommitAt(row).Subject, StringComparison.Ordinal));
        Assert.True(repository.SearchMarks.IsActive);
        Assert.Same(comparison, repository.Comparison);
        Assert.False(comparison.CanRevealTo);
        Assert.Equal(CompareViewModel.NotInGraphToolTip, comparison.ToLinkToolTip);

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

    /// <summary>
    /// Two commits, <c>main</c>'s and below it <c>old</c>'s, loaded one page at a time when the
    /// test sends it, the load ending at the third send; a comparison resolves the two names.
    /// </summary>
    private sealed class PagedRepository : FakeRepositoryBase
    {
        public static readonly CommitInfo Main = Commit(2, [new string('1', 40)]);

        public static readonly CommitInfo Old = Commit(1, []);

        private readonly SemaphoreSlim _send = new(0);

        /// <summary>Released after each page is handed over.</summary>
        public SemaphoreSlim Sent { get; } = new(0);

        public override string WorkingDirectory => Path.Combine(Path.GetTempPath(), "fake");

        public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

        public override string CommonDirectory => GitDirectory;

        public override string Name => "fake";

        /// <summary>Lets the load go on: the next page, or after the last one the end.</summary>
        public void Send() => _send.Release();

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepoRefs(
                new HeadState("main", Main.Sha),
                [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Main.Sha, IsHead: true), new GitRef("refs/heads/old", "old", RefKind.LocalBranch, Old.Sha)],
                [],
                []));

        public override async Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default)
        {
            foreach (var commit in new[] { Main, Old })
            {
                await _send.WaitAsync(cancellationToken);
                onPage([commit]);
                Sent.Release();
            }

            await _send.WaitAsync(cancellationToken);
        }

        public override Task<CompareResult> CompareAsync(string from, string to, bool threeDot, bool countCommits, CancellationToken cancellationToken = default)
        {
            var (fromSha, toSha) = (Resolve(from), Resolve(to));
            return Task.FromResult(new CompareResult(fromSha, toSha, threeDot ? CompareBase.MergeBase : CompareBase.Trees, threeDot ? toSha : fromSha, [], RenamesSkipped: false, countCommits ? new CompareCounts(1, 0) : null));
        }

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default)
        {
            var commit = sha == Main.Sha ? Main : Old;
            return Task.FromResult(new CommitDetails(commit.Sha, commit.Parents, commit.AuthorName, commit.AuthorEmail, commit.AuthorDate, commit.AuthorName, commit.AuthorEmail, commit.CommitDate, commit.Subject, string.Empty, []));
        }

        public override IRepositoryWatcher CreateWatcher() => new QuietWatcher();

        private static string Resolve(string name) => name == "refs/heads/old" ? Old.Sha : Main.Sha;

        private static CommitInfo Commit(int number, string[] parents)
        {
            var date = new DateTimeOffset(2026, 1, 1, 12, number, 0, TimeSpan.Zero);
            return new CommitInfo(new string((char)('0' + number), 40), parents, "Test Author", "author@example.com", date, date, $"Commit {number}");
        }

        private sealed class QuietWatcher : IRepositoryWatcher
        {
            public event EventHandler<RepositoryChangedEventArgs>? Changed
            {
                add { }
                remove { }
            }

            public void Start()
            {
            }

            public IDisposable Pause() => this;

            public void Dispose()
            {
            }
        }
    }
}
