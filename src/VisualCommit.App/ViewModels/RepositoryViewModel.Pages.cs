using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The lead's hooks of phase 5 (docs/phase-5-design.md 11.1 to 11.3, 3.1, 3.3, 6.2, 7.9, 8.2 to
/// 8.4): the pages that open in place of the graph and their host, a commit shown in the right
/// panel from a page or a comparison, the graph's commit shown or revealed, the places of the
/// comparison and the search with the search's marks, what the graph hides or solos with its
/// reload, and the restore bar's tie to the undo history. The pages (WP6), the comparison, the
/// search and the filters (WP7) and the undo (WP3) fill them; until then no page opens, nothing is
/// searched, hidden or soloed, the restore bar is as in phase 4, and the comparison and the search
/// throw.
/// </summary>
public sealed partial class RepositoryViewModel : IFileHistoryHost, IBlameHost
{
    private bool _revealOnPageClose;

    /// <summary>
    /// The page open in place of the graph (11.1): the file history, the blame or the reflog, or
    /// null. A diff opens over it; it and the interactive list exclude each other.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsGraph))]
    [NotifyPropertyChangedFor(nameof(ShowsPage))]
    public partial IGraphAreaPage? Page { get; private set; }

    /// <summary>The graph area shows the page: it is open and no diff or resolver covers it.</summary>
    public bool ShowsPage => Page is not null && !IsDiffOpen && !IsResolverOpen;

    /// <summary>
    /// The search bar's view model (3.1), shown above the graph's column headers while the bar is
    /// open, with the view the view locator finds for it (WP7's); null while it is closed. The lead
    /// opens it (<see cref="OpenSearch"/>).
    /// </summary>
    [ObservableProperty]
    public partial object? Search { get; private set; }

    /// <summary>
    /// What the graph marks of the search (3.3), which <c>CommitGraphControl.Search</c> binds to:
    /// <see cref="GraphSearchMarks.None"/> while nothing is searched. The lead gives it from the
    /// search's view model.
    /// </summary>
    [ObservableProperty]
    public partial GraphSearchMarks SearchMarks { get; private set; } = GraphSearchMarks.None;

    /// <summary>
    /// What the graph hides and solos (8.1, 8.4, D159), as the session keeps it for this repository,
    /// or null while nothing is. The menus read it (<see cref="RepositoryMenuContext.Filters"/>);
    /// WP7's operations change it through <see cref="ApplyGraphFilterAsync"/>.
    /// </summary>
    [ObservableProperty]
    public partial RepositoryGraphFilter? Filters { get; private set; }

    /// <summary>
    /// What the graph hides or solos (8.2, D178), which the history loads with, or null to load
    /// everything as before phase 5. The lead gives it from <see cref="Filters"/> when it joins WP7.
    /// </summary>
    public GraphFilter? GraphFilter => null;

    /// <summary>
    /// The graph reloads for a change of filter (8.2): a 2-px bar runs under the column headers, so
    /// that the click is seen to take effect on a large history. False until the lead joins WP7.
    /// </summary>
    [ObservableProperty]
    public partial bool IsReloadingGraph { get; private set; }

    /// <summary>
    /// What the graph hides or solos changed (8.1 to 8.4): WP7's operations call it once they have
    /// written the session (<see cref="TabServices.Session"/>). The menus see the new state at once.
    /// The graph's reload through the refresh (<see cref="GraphFilter"/>, the filter's key in the
    /// refresh's comparison, <see cref="IsReloadingGraph"/>) comes when the lead joins WP7; until
    /// then the graph stays as it is.
    /// </summary>
    public Task ApplyGraphFilterAsync(RepositoryGraphFilter? filters)
    {
        if (!_disposed)
        {
            Filters = filters;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Opens <paramref name="page"/> in place of the graph (11.1): what the graph area shows is
    /// left first (the list asks when changed; a page holds no work and goes without a word), then
    /// the page shows. Opening a page leaves the right panel as it is. When leaving was refused,
    /// the page is disposed unopened.
    /// </summary>
    public async Task OpenPageAsync(IGraphAreaPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (_disposed || !await TryLeaveGraphAreaAsync())
        {
            page.Dispose();
            return;
        }

        LeaveGraphArea();
        _revealOnPageClose = false;
        Page = page;
    }

    /// <summary>
    /// Shows the commit <paramref name="sha"/> in the right panel (11.3, D176), for the user's click
    /// or key on a page's row or a comparison's link: the working-changes row is left (the stage
    /// panel's draft stays), then the commit is selected behind the page when the graph has it,
    /// without closing a diff; otherwise the graph's selection is cleared and the details show the
    /// commit directly, kept as the one shown so that selecting a row later reloads the panel. The
    /// zero id of a line not committed yet selects the working-changes row instead (4.3).
    /// </summary>
    public void ShowCommit(string sha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        if (_disposed)
        {
            return;
        }

        if (IsNotCommitted(sha))
        {
            IsWorkingRowSelected = HasWorkingChanges;
            return;
        }

        IsWorkingRowSelected = false;
        _revealOnPageClose = Page is not null;
        if (Graph.IndexOf(sha) is >= 0 and var index)
        {
            SelectedIndex = index;
            return;
        }

        Selection = GraphSelection.Empty;
        _selectedSha = sha;
        _ = Details.ShowAsync(sha, null, _lifetime.Token);
    }

    /// <summary>
    /// "Show in graph" (11.3): closes the page, selects the commit and reveals it; nothing for a
    /// commit the graph does not have (its item is disabled). The zero id of a blame's line not
    /// committed yet selects the working-changes row at the top (4.2, 4.3).
    /// </summary>
    public void ShowInGraph(string sha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        if (_disposed || !HasRow(sha))
        {
            return;
        }

        if (IsNotCommitted(sha))
        {
            // The working-changes row takes the place of a commit picked on the page: that one is
            // not revealed.
            _revealOnPageClose = false;
            ClosePage();
            CloseDiff(returnFocus: false);
            IsWorkingRowSelected = true;
            ScrollOffset = 0;
        }
        else
        {
            ClosePage();
            Select(sha, reveal: true);
        }

        GraphFocusRequested?.Invoke(this, GraphFocusRequest.Graph);
    }

    /// <summary>Scrolls the commit's row into the middle of the view without selecting it (6.4's links); nothing for a commit the graph does not have.</summary>
    public void Reveal(string sha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        if (Graph.IndexOf(sha) is >= 0 and var index)
        {
            RevealRequested?.Invoke(this, index);
        }
    }

    /// <summary>
    /// The comparison of <paramref name="toRef"/> with <paramref name="fromRef"/> in the right panel
    /// (6.2): "Compare {b} with {cur}" and a drop's "Compare {A} with {B}". It leaves the
    /// working-changes row, so that the details panel, which holds it, shows. WP7 and the lead build it.
    /// </summary>
    public Task CompareAsync(string fromRef, string toRef) =>
        throw new NotImplementedException("The comparison's place is the lead's, with work package WP7 (docs/phase-5-design.md 6.2).");

    /// <summary>
    /// The toolbar's Search and its key (3.1, 11.2): the graph shows (a diff or a page closes, they
    /// hold no work) and the search bar opens with the focus in its box. WP7 and the lead build it.
    /// </summary>
    public void OpenSearch() =>
        throw new NotImplementedException("The search's place is the lead's, with work package WP7 (docs/phase-5-design.md 3.1).");

    /// <summary>
    /// The restore bar's tie to its discard (7.9): the Undo of the discard that saved
    /// <paramref name="snapshot"/> put its files back, so its bar goes. A bar that shows another
    /// discard's snapshot stays. WP3's undo calls it.
    /// </summary>
    public void HideRestoreBar(DiscardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_lastSnapshot is { } shown && string.Equals(shown.Ref, snapshot.Ref, StringComparison.Ordinal))
        {
            _lastSnapshot = null;
            _afterDiscard = null;
            Changes.HideRestore();
        }
    }

    /// <summary>
    /// The restore bar's tie to its discard (7.9): the Redo of the discard that saved
    /// <paramref name="snapshot"/> discarded again, so its bar shows again with the same snapshot,
    /// and what the files hold now is kept for Restore's question (D75), as after the discard
    /// itself. WP3's redo calls it inside its operation.
    /// </summary>
    public Task ShowRestoreBarAsync(DiscardSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return RememberDiscardAsync(snapshot, cancellationToken);
    }

    /// <summary>The zero id that blame gives a line not committed yet (4.2): the working-changes row stands for it.</summary>
    private static bool IsNotCommitted(string sha) => sha.All(c => c == '0');

    /// <summary>Whether the graph has a row for <paramref name="sha"/>: a commit's, or the working-changes row for the zero id while it shows.</summary>
    private bool HasRow(string sha) => IsNotCommitted(sha) ? HasWorkingChanges : Graph.IndexOf(sha) >= 0;

    /// <summary>
    /// Closes the open page without asking (it holds no work). The focus stays where the caller
    /// puts it; when the user picked a commit on the page, the graph reveals it once it shows again
    /// (11.1's "Revealing on the way back").
    /// </summary>
    private void ClosePage()
    {
        if (Page is not { } page)
        {
            return;
        }

        Page = null;
        page.Dispose();
        if (_revealOnPageClose && Selection.Single is { } sha && Graph.IndexOf(sha) is >= 0 and var index)
        {
            RevealRequested?.Invoke(this, index);
        }

        _revealOnPageClose = false;
    }

    // The pages' host (11.1, IFileHistoryHost, IBlameHost).

    CancellationToken IGraphAreaPageHost.Lifetime => _lifetime.Token;

    ISettingsStore IGraphAreaPageHost.Settings => _settings;

    IAppLog IGraphAreaPageHost.Log => _log;

    IRepositoryActions IGraphAreaPageHost.Actions => MenuActions;

    bool IGraphAreaPageHost.HasCommit(string sha) => HasRow(sha);

    void IGraphAreaPageHost.Close(IGraphAreaPage page)
    {
        // A page left for something else (another page, a diff from the stage panel, the resolver) closes nothing and takes no focus.
        if (!ReferenceEquals(Page, page))
        {
            return;
        }

        ClosePage();
        GraphFocusRequested?.Invoke(this, GraphFocusRequest.Graph);
    }

    // A page opened from a page replaces it (11.1), as one opened from a menu does.

    void IFileHistoryHost.OpenBlame(BlameRequest request) => _ = MenuActions.ShowBlameAsync(request);

    void IBlameHost.OpenFileHistory(FileHistoryRequest request) => _ = MenuActions.ShowFileHistoryAsync(request);
}
