using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Shell;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// Finding (docs/phase-5-design.md 3 and 6, report part F): the search bar's place, what opens it
/// and where its matches are marked (<see cref="ISearchHost"/>); and the comparison's place in the
/// right panel, two selected commits at once (D162) or branches from their menus, with a listed
/// file's diff and menu (<see cref="ICompareHost"/>).
/// </summary>
public sealed partial class RepositoryViewModel : ISearchHost, ICompareHost
{
    /// <summary>The mode the search bar opens in: Commits at first, then the last one chosen, for the session (3.1, FD1).</summary>
    private SearchMode _searchMode = SearchMode.Commits;

    /// <summary>The diff opened from the comparison's list, whose row keeps the selection while it is open (6.4).</summary>
    private DiffViewModel? _compareDiff;

    /// <summary>
    /// Raised when a row should be scrolled into view as little as possible, without centring it:
    /// a search's match (3.1), so that the eye can follow from one to the next.
    /// </summary>
    public event EventHandler<int>? ScrollIntoViewRequested;

    /// <summary>The open search bar's view model, or null.</summary>
    public SearchViewModel? SearchBar => Search as SearchViewModel;

    /// <summary>The comparison the right panel shows (6.1, 6.2), or null.</summary>
    public CompareViewModel? Comparison => MultiSelection as CompareViewModel;

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

    /// <summary>
    /// The comparison of <paramref name="toRef"/> with <paramref name="fromRef"/> in the right panel
    /// (6.2, D177): "Compare {b} with {cur}" and a drop's "Compare {A} with {B}", by full ref names
    /// (FD20). It leaves the working-changes row (the stage panel's draft stays in memory), so that
    /// the details panel, which holds it, shows; it replaces any other comparison. Completes when
    /// the comparison has been read.
    /// </summary>
    public Task CompareAsync(string fromRef, string toRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(toRef);
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        IsWorkingRowSelected = false;
        var comparison = new CompareViewModel(this, new CompareSide(fromRef, RefLabelOf(fromRef)), new CompareSide(toRef, RefLabelOf(toRef)), isBranches: true);
        ShowComparison(comparison);
        return comparison.LoadAsync();
    }

    /// <summary>
    /// A right-click on a file of the comparison's list (6.4, FD21): its menu only
    /// (<see cref="RepositoryMenus.ForCompareFile"/>), at To; no row is selected and no diff opens.
    /// </summary>
    public bool OpenCompareFileMenu(MenuLayerViewModel layer, CompareViewModel comparison, ChangedFile file, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(file);
        if (IsClosed || !ReferenceEquals(Comparison, comparison) || comparison.ToSha is not { } to)
        {
            return false;
        }

        var target = new FileMenuTarget(file, IsUntracked: false, ExistsInWorkingTree: ExistsInWorkingTree(file.Path), CommitSha: to);
        return OpenMenu(layer, RepositoryMenus.ForCompareFile(MenuContext, target, MenuActions), position, openedBy);
    }

    /// <summary>
    /// The right panel's slot for <paramref name="selection"/> (docs/phase-3-design.md 4h,
    /// docs/phase-5-design.md 6.1, D162): exactly two commits show their comparison, the same two
    /// keeping the one shown (a reload maps them again); three or more show phase 3's list. A
    /// branch comparison stays while nothing is selected (a reload took the selected commit away)
    /// and ends when one or more commits are selected.
    /// </summary>
    private void ShowMultiSelection(GraphSelection selection)
    {
        if (ComparedCommits(selection) is var (older, newer))
        {
            if (Comparison is { } shown && shown.Compares(older.Sha, newer.Sha))
            {
                Details.IsMultiSelection = true;
                return;
            }

            // The lower row is From, the upper To: for commits of one line, the older first (6.1).
            var comparison = new CompareViewModel(this, new CompareSide(older.Sha, Label(older)), new CompareSide(newer.Sha, Label(newer)), isBranches: false);
            ShowComparison(comparison);
            _ = comparison.LoadAsync();
            return;
        }

        if (selection.IsEmpty && Comparison is { IsBranches: true })
        {
            return;
        }

        Comparison?.Dispose();
        Details.IsMultiSelection = selection.IsMultiple;
        MultiSelection = selection.IsMultiple
            ? new MultiSelectionViewModel([.. SelectedCommits.Select(commit => new MultiSelectionItem(commit.Sha, commit.ShortSha, commit.Subject))])
            : null;

        static string Label(CommitInfo commit) => commit.ShortSha + " " + commit.Subject;
    }

    /// <summary>The two selected commits, lower row first, when exactly two commits (no stash) are selected and both are rows; null otherwise.</summary>
    private (CommitInfo Older, CommitInfo Newer)? ComparedCommits(GraphSelection selection)
    {
        if (selection.Count != 2)
        {
            return null;
        }

        var rows = selection.Shas.Select(sha => Graph.IndexOf(sha)).ToList();
        if (rows.Any(row => row < 0))
        {
            return null;
        }

        var commits = rows.Order().Select(row => Graph.CommitAt(row)).ToList();
        return commits.Any(commit => commit.Kind != CommitKind.Commit) ? null : (commits[1], commits[0]);
    }

    /// <summary>Shows <paramref name="comparison"/> in the right panel's slot, over the commit details, in place of what it held.</summary>
    private void ShowComparison(CompareViewModel comparison)
    {
        Comparison?.Dispose();
        MultiSelection = comparison;
        Details.IsMultiSelection = true;
    }

    /// <summary>Ends the comparison shown: the slot shows what the graph's selection shows (6.2).</summary>
    private void EndComparison()
    {
        if (Comparison is not { } comparison)
        {
            return;
        }

        comparison.Dispose();
        MultiSelection = null;
        Details.IsMultiSelection = false;
        ShowMultiSelection(Selection);
    }

    /// <summary>How a comparison names a branch, a tag or HEAD: its short name as the menus give it.</summary>
    private string RefLabelOf(string fullName) =>
        Refs?.Refs.FirstOrDefault(gitRef => string.Equals(gitRef.FullName, fullName, StringComparison.Ordinal))?.Name
        ?? ShortRefName(fullName);

    private static string ShortRefName(string fullName)
    {
        foreach (var prefix in new[] { "refs/heads/", "refs/remotes/", "refs/tags/" })
        {
            if (fullName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return fullName[prefix.Length..];
            }
        }

        return fullName;
    }

    /// <summary>A reloaded or newly loading graph is searched where it left off (3.4), and the comparison's links follow its rows.</summary>
    partial void OnGraphChanged(CommitGraphData value)
    {
        SearchBar?.OnGraphReplaced();
        Comparison?.UpdateLinks();
    }

    partial void OnSelectedIndexChanged(int oldValue, int newValue) => SearchBar?.OnSelectionChanged();

    /// <summary>Selecting the working-changes row ends a branch comparison (6.2).</summary>
    partial void OnIsWorkingRowSelectedChanged(bool oldValue, bool newValue)
    {
        if (newValue && Comparison is { IsBranches: true })
        {
            EndComparison();
        }
    }

    /// <summary>The comparison's file row loses its selection when its diff closes or another opens (6.4).</summary>
    partial void OnDiffChanged(DiffViewModel? value)
    {
        if (!ReferenceEquals(value, _compareDiff))
        {
            _compareDiff = null;
            if (Comparison is { } comparison)
            {
                comparison.SelectedFilePath = null;
            }
        }
    }

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

    // The comparison's host (ICompareHost).

    CancellationToken ICompareHost.Lifetime => _lifetime.Token;

    IAppLog ICompareHost.Log => _log;

    ISettingsStore ICompareHost.Settings => _settings;

    Task<CompareResult> ICompareHost.CompareAsync(string from, string to, bool threeDot, bool countCommits, CancellationToken cancellationToken) =>
        Repository.CompareAsync(from, to, threeDot, countCommits, cancellationToken);

    bool ICompareHost.HasRow(string sha) => Graph.IndexOf(sha) >= 0;

    void ICompareHost.Reveal(string sha) => Reveal(sha);

    void ICompareHost.OpenDiff(CompareViewModel comparison, ChangedFile file, string old, string to)
    {
        if (_disposed || !ReferenceEquals(Comparison, comparison))
        {
            return;
        }

        // From the side the list was computed from, with the old path only from its own R or C entry (6.4).
        var target = new DiffTarget(DiffSide.Commit, file.Path, file.Kind, file.OldPath, Commit: to, Parent: old);
        ShowDiff(target, $"{OperationTexts.Short(old)}..{OperationTexts.Short(to)}");
        _compareDiff = Diff;
        Changes.ShowSelection(null, staged: false);
        Details.SelectedFilePath = null;
        comparison.SelectedFilePath = file.Path;
    }

    void ICompareHost.Close(CompareViewModel comparison)
    {
        if (_disposed || !ReferenceEquals(Comparison, comparison))
        {
            return;
        }

        EndComparison();
        GraphFocusRequested?.Invoke(this, GraphFocusRequest.Graph);
    }
}
