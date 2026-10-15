using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// Finding (docs/phase-5-design.md 3, report part F): the search bar's place, what opens it and
/// where its matches are marked (<see cref="ISearchHost"/>).
/// </summary>
public sealed partial class RepositoryViewModel : ISearchHost
{
    /// <summary>The mode the search bar opens in: Commits at first, then the last one chosen, for the session (3.1, FD1).</summary>
    private SearchMode _searchMode = SearchMode.Commits;

    /// <summary>
    /// Raised when a row should be scrolled into view as little as possible, without centring it:
    /// a search's match (3.1), so that the eye can follow from one to the next.
    /// </summary>
    public event EventHandler<int>? ScrollIntoViewRequested;

    /// <summary>The open search bar's view model, or null.</summary>
    public SearchViewModel? SearchBar => Search as SearchViewModel;

    /// <summary>
    /// The toolbar's Search and its key (3.1, 11.2): the graph shows (a diff or a page closes, they
    /// hold no work) and the search bar opens with the focus in its box; an open bar takes the
    /// focus again with its text selected. Nothing while the interactive list or the resolver is
    /// open: they hold work, and Search is disabled then.
    /// </summary>
    public void OpenSearch()
    {
        if (_disposed || IsInteractiveRebaseOpen || IsResolverOpen)
        {
            return;
        }

        ClosePage();
        CloseDiff(returnFocus: false);
        if (SearchBar is { } open)
        {
            open.RequestFocus();
            return;
        }

        var search = new SearchViewModel(this, _searchMode, Operations.Services?.Clock);
        search.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SearchViewModel.Mode))
            {
                _searchMode = search.Mode;
            }
        };
        Search = search;
        search.RequestFocus();
    }

    /// <summary>A reloaded or newly loading graph is searched where it left off (3.4).</summary>
    partial void OnGraphChanged(CommitGraphData value) => SearchBar?.OnGraphReplaced();

    partial void OnSelectedIndexChanged(int oldValue, int newValue) => SearchBar?.OnSelectionChanged();

    // The search bar's host (ISearchHost).

    int ISearchHost.SelectedRow => SelectedIndex;

    CancellationToken ISearchHost.Lifetime => _lifetime.Token;

    IAppLog ISearchHost.Log => _log;

    Task ISearchHost.SearchAsync(SearchMode mode, string text, IReadOnlyList<string> shas, Action<string> onMatch, IProgress<int>? progress, CancellationToken cancellationToken) =>
        Repository.SearchAsync(mode, text, shas, onMatch, progress, cancellationToken);

    void ISearchHost.SelectMatch(int row)
    {
        if (_disposed || row < 0 || row >= Graph.Count)
        {
            return;
        }

        // As a click selects it: the right panel shows it (3.1).
        SelectedIndex = row;
        ScrollIntoViewRequested?.Invoke(this, row);
    }

    void ISearchHost.ShowMarks(GraphSearchMarks marks) => SearchMarks = marks;

    void ISearchHost.CloseSearch()
    {
        SearchBar?.Dispose();
        Search = null;
        SearchMarks = GraphSearchMarks.None;
        GraphFocusRequested?.Invoke(this, GraphFocusRequest.Graph);
    }
}
