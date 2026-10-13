using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The lead's hooks of phase 5 (docs/phase-5-design.md 11.1 to 11.3, 3.1, 6.2, 8.2): the pages that
/// open in place of the graph and their host, a commit shown in the right panel from a page or a
/// comparison, the graph's commit shown or revealed, and the places of the comparison, the search
/// and the graph's filter. The pages (WP6), the comparison and the search (WP7) and the filter's
/// session (WP7) fill them; until then no page opens, the filter is none, and the comparison and the
/// search throw.
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
    /// What the graph hides or solos (8.2, D178), which the history loads with, or null to load
    /// everything as before phase 5. WP7 and the lead give it from the session.
    /// </summary>
    public GraphFilter? GraphFilter => null;

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

        if (sha.All(c => c == '0'))
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

    /// <summary>"Show in graph" (11.3): closes the page, selects the commit and reveals it; nothing for a commit the graph does not have (its item is disabled).</summary>
    public void ShowInGraph(string sha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        if (_disposed || Graph.IndexOf(sha) < 0)
        {
            return;
        }

        ClosePage();
        Select(sha, reveal: true);
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

    bool IGraphAreaPageHost.HasCommit(string sha) => Graph.IndexOf(sha) >= 0;

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

    void IFileHistoryHost.OpenBlame(BlameRequest request) =>
        throw new NotImplementedException("The blame page is work package WP6's (docs/phase-5-design.md 4.2).");

    void IBlameHost.OpenFileHistory(FileHistoryRequest request) =>
        throw new NotImplementedException("The file history page is work package WP6's (docs/phase-5-design.md 4.1).");
}
