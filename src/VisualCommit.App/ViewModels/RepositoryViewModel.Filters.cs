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

    /// <summary>The graph the left panel's marks follow until it is complete.</summary>
    private CommitGraphData? _markedGraph;

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

    partial void OnFiltersChanged(RepositoryGraphFilter? value) => LeftPanel.UpdateFilters(value);

    /// <summary>A hidden branch's row says whether its commit is a row (8.3): the marks follow the graph shown until it is complete.</summary>
    private void FollowGraphForLeftPanel(CommitGraphData graph)
    {
        if (_markedGraph is not null)
        {
            _markedGraph.Changed -= OnMarkedGraphChanged;
        }

        _markedGraph = graph.IsComplete ? null : graph;
        if (_markedGraph is not null)
        {
            _markedGraph.Changed += OnMarkedGraphChanged;
        }

        if (Filters is not null)
        {
            LeftPanel.RefreshMarks();
        }
    }

    private void OnMarkedGraphChanged(object? sender, EventArgs e)
    {
        if (sender is CommitGraphData { IsComplete: true } graph)
        {
            graph.Changed -= OnMarkedGraphChanged;
            _markedGraph = null;
            if (Filters is not null && ReferenceEquals(graph, Graph))
            {
                LeftPanel.RefreshMarks();
            }
        }
    }
}
