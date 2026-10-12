using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The lead's hooks of phase 4 (docs/phase-4-design.md 2.2, 11.1 and section 14): what puts the
/// interactive rebase's list and the conflict resolver in the graph area, what must be left
/// before something else takes it, where the focus goes when they close; and what the backups'
/// clean-up keeps.
/// </summary>
public sealed partial class RepositoryViewModel : IInteractiveRebaseHost
{
    /// <summary>The interactive rebase's list shown in place of the graph (2.2), or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInteractiveRebaseOpen))]
    [NotifyPropertyChangedFor(nameof(ShowsGraph))]
    [NotifyPropertyChangedFor(nameof(ShowsInteractiveRebase))]
    public partial InteractiveRebaseViewModel? InteractiveRebase { get; private set; }

    public bool IsInteractiveRebaseOpen => InteractiveRebase is not null;

    /// <summary>The graph area shows the graph: no diff, list or resolver is open in its place.</summary>
    public bool ShowsGraph => !IsDiffOpen && !IsInteractiveRebaseOpen;

    /// <summary>The graph area shows the list: it is open and no diff is open over it (2.2).</summary>
    public bool ShowsInteractiveRebase => IsInteractiveRebaseOpen && !IsDiffOpen;

    /// <summary>
    /// Raised when the list or the resolver has closed (2.2, 11.1): the graph area's view gives
    /// the keyboard focus to the graph, or, with <see cref="GraphFocusRequest.ContinueFirst"/>, to
    /// the banner's Continue button when it is shown and enabled.
    /// </summary>
    public event EventHandler<GraphFocusRequest>? GraphFocusRequested;

    /// <summary>
    /// Opens the interactive rebase's list (2.2) for <paramref name="request"/>, holding the
    /// commits <paramref name="list"/> read (2.3), after leaving what the graph area shows
    /// (11.1). Completes when the list is open, or when leaving was cancelled.
    /// </summary>
    public async Task ShowInteractiveRebaseAsync(InteractiveRebaseRequest request, RebaseList list)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(list);
        if (_disposed || !await TryLeaveGraphAreaAsync())
        {
            return;
        }

        CloseDiff();
        InteractiveRebase = null;
        IsWorkingRowSelected = false;
        var index = Graph.IndexOf(list.Onto);
        var baseSubject = index >= 0 ? Graph.CommitAt(index).Subject : string.Empty;
        var opened = new InteractiveRebaseViewModel(request, list, baseSubject, this);
        if (Refs is { } refs)
        {
            opened.ObserveRefs(refs);
        }

        InteractiveRebase = opened;
    }

    /// <summary>Opens the conflict resolver on <paramref name="file"/> (11.1, D148), after leaving what the graph area shows.</summary>
    public void OpenResolver(ChangedFile file) =>
        throw new NotImplementedException("The lead's hook: docs/phase-4-design.md 11.1");

    /// <summary>
    /// Whether what the graph area shows may be replaced (11.1): asks the list's question when it
    /// was changed (2.2, 7.2). False on "Keep editing". It closes nothing itself.
    /// </summary>
    public async Task<bool> TryLeaveGraphAreaAsync()
    {
        if (InteractiveRebase is { } list && !await list.TryLeaveAsync())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// The full ref names the backups' clean-up keeps (section 9, D139): the restore bar's
    /// snapshot while the bar offers it.
    /// </summary>
    public IReadOnlySet<string> BackupsToKeep =>
        _lastSnapshot is { } snapshot ? new HashSet<string>(StringComparer.Ordinal) { snapshot.Ref } : new HashSet<string>(StringComparer.Ordinal);

    // The list's host (2.2, IInteractiveRebaseHost).

    Task<bool> IInteractiveRebaseHost.StartAsync(RebasePlan plan) =>
        InteractiveRebase is { } list ? Operations.RunInteractiveRebaseAsync(plan, list.Request) : Task.FromResult(false);

    void IInteractiveRebaseHost.ShowDetails(string? sha)
    {
        // The graph's selection behind the list, by SHA (D88); Select would close a diff over it.
        if (sha is null)
        {
            Selection = GraphSelection.Empty;
            return;
        }

        if (Graph.IndexOf(sha) is >= 0 and var index)
        {
            SelectedIndex = index;
        }
        else
        {
            _pendingSelection = Graph.IsComplete ? null : sha;
        }
    }

    Task<bool> IInteractiveRebaseHost.ConfirmAsync(ConfirmationRequest request) => Operations.ConfirmAsync(request);

    Task<string?> IInteractiveRebaseHost.AskMessageAsync(string title, string message, string confirmText) =>
        Operations.ShowDialogAsync(new MessageDialogViewModel(title, message, confirmText));

    void IInteractiveRebaseHost.Close()
    {
        InteractiveRebase = null;
        GraphFocusRequested?.Invoke(this, GraphFocusRequest.Graph);
    }
}

/// <summary>Where the keyboard focus goes when the list or the resolver closes (2.2, 11.1).</summary>
public enum GraphFocusRequest
{
    /// <summary>The graph.</summary>
    Graph,

    /// <summary>The banner's Continue button when it is shown and enabled, otherwise the graph.</summary>
    ContinueFirst,
}
