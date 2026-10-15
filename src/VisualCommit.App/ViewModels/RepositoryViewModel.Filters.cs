using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// What the graph hides and solos (docs/phase-5-design.md 8, D159, D178): the filter read from the
/// session when the repository opens, corrected by the refs read (a hidden remote's branches now,
/// names gone at the first read), written back when it changes, and the left panel's marks and
/// toggles. The graph loads with it (<see cref="GraphFilter"/>, <see cref="ApplyGraphFilterAsync"/>).
/// </summary>
public sealed partial class RepositoryViewModel
{
    /// <summary>The key of the filter the graph shown was loaded with (null: none), which a refresh compares (8.2).</summary>
    private string? _loadedFilterKey;

    /// <summary>The graph shown while it still loads, whose pages the left panel's marks and the comparison's links follow.</summary>
    private CommitGraphData? _loadingGraph;

    /// <summary>
    /// The key of the filter the graph shown was loaded with (<see cref="Core.Graph.GraphFilter.Key"/>),
    /// or null for a graph loaded without one: the graph is the current filter's when it equals
    /// <see cref="GraphFilter"/>'s and <see cref="IsReloadingGraph"/> is false.
    /// </summary>
    public string? LoadedFilterKey => _loadedFilterKey;

    /// <summary>The session's filters, or null in view-model tests without services.</summary>
    private GraphFilterStore? FilterStore => Operations.Services?.GraphFilters;

    /// <summary>
    /// Changes what the graph hides and solos (8.1): <paramref name="change"/> makes the new filter
    /// from the one shown and the refs as last read; it is written to the session, and the graph
    /// reloads (<see cref="ApplyGraphFilterAsync"/>). Completes when the new graph is shown. Nothing
    /// before the refs are read.
    /// </summary>
    public Task ChangeFiltersAsync(Func<RepositoryGraphFilter?, RepoRefs, RepositoryGraphFilter?> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (_disposed || Refs is not { } refs)
        {
            return Task.CompletedTask;
        }

        var next = change(Filters, refs);
        return ApplyGraphFilterAsync(FilterStore is { } store ? store.Write(Path, next) : next);
    }

    /// <summary>
    /// Follows a change of a branch's name that the app itself made (8.4): <paramref name="change"/>
    /// moves or removes its entries, which are written to the session at once. The operation's own
    /// refresh then loads the graph with the new filter.
    /// </summary>
    public void FollowBranchChange(Func<RepositoryGraphFilter?, RepositoryGraphFilter?> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (_disposed || Filters is null)
        {
            return;
        }

        var next = change(Filters);
        if (!GraphFilterChanges.Same(next, Filters))
        {
            Filters = FilterStore is { } store ? store.Write(Path, next) : next;
        }
    }

    /// <summary>
    /// The repository's filter from the session (8.4), read when it opens, so that git starts before
    /// the refs are read (8.2); opening it counts as using it, for the session's bound. Returns
    /// what the first load loads with.
    /// </summary>
    private Core.Graph.GraphFilter? LoadSessionFilter()
    {
        if (Filters is null && FilterStore is { } store && store.Find(Path) is not null)
        {
            Filters = store.Touch(Path);
        }

        return GraphFilter;
    }

    /// <summary>
    /// Corrects the filter by the refs read (8.1, 8.4): a hidden remote's branches are its branches
    /// now; at the repository's first read, names that no longer exist are dropped and solo mode
    /// ends when no soloed name exists. A change is written to the session. Returns whether what
    /// the graph loads changed.
    /// </summary>
    private bool CorrectFilters(RepoRefs refs, bool firstRead)
    {
        var corrected = GraphFilterChanges.Correct(Filters, refs, dropMissing: firstRead);
        if (ReferenceEquals(corrected, Filters))
        {
            return false;
        }

        var before = GraphFilter?.Key;
        Filters = FilterStore is { } store ? store.Write(Path, corrected) : corrected;
        return GraphFilter?.Key != before;
    }

    /// <summary>The left panel marks what the graph hides and solos, and its toggles show and stop soloing (8.3).</summary>
    private void ConnectLeftPanelFilters()
    {
        // While the graph loads a commit may still come: a hidden branch's click selects it then (8.3).
        LeftPanel.IsInGraph = sha => !Graph.IsComplete || Graph.IndexOf(sha) >= 0;
        LeftPanel.NothingActivated += (_, _) => SelectNothing();
        LeftPanel.HiddenToggleClicked += (_, row) =>
        {
            if (LeftPanelViewModel.RefNameOf(row) is { } branch)
            {
                Start(MenuActions.ShowBranchAsync(branch), "showing a hidden branch");
            }
            else if (LeftPanelViewModel.RemoteNameOf(row) is { } remote)
            {
                Start(MenuActions.ShowRemoteAsync(remote), "showing a hidden remote");
            }
        };
        LeftPanel.SoloToggleClicked += (_, row) =>
        {
            if (LeftPanelViewModel.RefNameOf(row) is { } branch)
            {
                Start(MenuActions.StopSoloingBranchAsync(branch), "ending a solo");
            }
        };
    }

    /// <summary>
    /// A click on a hidden branch whose commit is no row (8.3, FD19): as a click on a ref closes a
    /// diff and selects its commit (<see cref="Select"/>), this one selects nothing: neither a
    /// commit nor the working-changes row, and the details show their placeholder.
    /// </summary>
    private void SelectNothing()
    {
        if (_disposed)
        {
            return;
        }

        CloseDiff(returnFocus: false);
        IsWorkingRowSelected = false;
        _pendingSelection = null;
        Selection = GraphSelection.Empty;
        if (_selectedSha is not null)
        {
            // A commit the details showed without a row (11.3) goes too.
            _selectedSha = null;
            _ = Details.ShowAsync(null, null, _lifetime.Token);
        }
    }

    partial void OnFiltersChanged(RepositoryGraphFilter? value) => LeftPanel.UpdateFilters(value);

    /// <summary>
    /// A hidden branch's row says whether its commit is a row (8.3), and so does a comparison's
    /// link (docs/phase-5-design.md 6.4): both follow the graph shown, page by page while it loads.
    /// </summary>
    private void FollowLoadingGraph(CommitGraphData graph)
    {
        if (_loadingGraph is not null)
        {
            _loadingGraph.Changed -= OnLoadingGraphChanged;
        }

        _loadingGraph = graph.IsComplete ? null : graph;
        if (_loadingGraph is not null)
        {
            _loadingGraph.Changed += OnLoadingGraphChanged;
        }

        if (Filters is not null)
        {
            LeftPanel.RefreshMarks();
        }
    }

    private void OnLoadingGraphChanged(object? sender, EventArgs e)
    {
        if (sender is not CommitGraphData graph)
        {
            return;
        }

        var shown = ReferenceEquals(graph, Graph);
        if (shown)
        {
            // A page came: a link whose commit it brought reveals that commit now.
            Comparison?.UpdateLinks();
        }

        if (graph.IsComplete)
        {
            // The left panel asks once the graph is whole: until then a hidden branch's commit may still come.
            graph.Changed -= OnLoadingGraphChanged;
            _loadingGraph = null;
            if (Filters is not null && shown)
            {
                LeftPanel.RefreshMarks();
            }
        }
    }
}
