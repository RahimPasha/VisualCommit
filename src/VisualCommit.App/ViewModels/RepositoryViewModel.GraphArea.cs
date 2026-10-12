using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// The lead's hooks of phase 4 (docs/phase-4-design.md 2.2, 11.1, 11.2, 11.9 and section 14):
/// what puts the interactive rebase's list and the conflict resolver in the graph area, what
/// must be left before something else takes it, where the focus goes when they close, the
/// resolver fed from every status read with git's merge tool; and what the backups' clean-up
/// keeps.
/// </summary>
public sealed partial class RepositoryViewModel : IInteractiveRebaseHost
{
    private ConflictResolverViewModel? _resolver;
    private string? _mergeTool;
    private bool _closingResolverQuietly;
    private int _mergeToolReads;

    /// <summary>The interactive rebase's list shown in place of the graph (2.2), or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInteractiveRebaseOpen))]
    [NotifyPropertyChangedFor(nameof(ShowsGraph))]
    [NotifyPropertyChangedFor(nameof(ShowsInteractiveRebase))]
    public partial InteractiveRebaseViewModel? InteractiveRebase { get; private set; }

    public bool IsInteractiveRebaseOpen => InteractiveRebase is not null;

    /// <summary>
    /// The conflict resolver (11.1): one per repository, like the diff, so that its document,
    /// undo stack, caret and scroll are kept while another tab is shown. It shows only while
    /// <see cref="IsResolverOpen"/>.
    /// </summary>
    public ConflictResolverViewModel Resolver => _resolver ??= CreateResolver();

    /// <summary>The resolver is open in the graph area, which then spans the graph and right-panel columns (11.2).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsGraph))]
    [NotifyPropertyChangedFor(nameof(ShowsInteractiveRebase))]
    [NotifyPropertyChangedFor(nameof(ShowsDiff))]
    public partial bool IsResolverOpen { get; private set; }

    /// <summary>The graph area shows the graph: no diff, list or resolver is open in its place.</summary>
    public bool ShowsGraph => !IsDiffOpen && !IsInteractiveRebaseOpen && !IsResolverOpen;

    /// <summary>The graph area shows the list: it is open and no diff is open over it (2.2).</summary>
    public bool ShowsInteractiveRebase => IsInteractiveRebaseOpen && !IsDiffOpen && !IsResolverOpen;

    /// <summary>The graph area shows the open diff.</summary>
    public bool ShowsDiff => IsDiffOpen && !IsResolverOpen;

    /// <summary>
    /// Git's merge tool (<c>merge.tool</c>), read whenever the status shows a conflict (11.9), or
    /// null: the menus' <c>Open in merge tool</c> and the resolver's <c>Merge tool…</c>.
    /// </summary>
    public string? MergeTool => _mergeTool;

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

        LeaveGraphArea();
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

    /// <summary>
    /// Opens the conflict resolver on <paramref name="file"/> (11.1, D148): "Resolve conflict", a
    /// click on a conflicted file in the stage panel, <see cref="OpenDiff"/> of one. Leaves what the
    /// graph area shows first (asking when the list was changed or the result modified).
    /// </summary>
    public void OpenResolver(ChangedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _ = OpenResolverAsync(file.Path);
    }

    /// <summary>
    /// Whether what the graph area shows may be replaced (11.1): asks the list's question when it
    /// was changed (2.2, 7.2), and the resolver's when its result is modified. False on "Keep
    /// editing". It closes nothing itself.
    /// </summary>
    public async Task<bool> TryLeaveGraphAreaAsync()
    {
        if (InteractiveRebase is { } list && !await list.TryLeaveAsync())
        {
            return false;
        }

        if (IsResolverOpen && _resolver is { } resolver && !await resolver.TryLeaveAsync())
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

    /// <summary>
    /// Closes what the graph area shows instead of the graph, without asking (after
    /// <see cref="TryLeaveGraphAreaAsync"/>): the diff, the list and the resolver. The focus stays
    /// where the caller puts it.
    /// </summary>
    private void LeaveGraphArea()
    {
        CloseDiff(returnFocus: false);
        InteractiveRebase = null;
        if (IsResolverOpen && _resolver is { } resolver)
        {
            _closingResolverQuietly = true;
            try
            {
                resolver.Close();
            }
            finally
            {
                _closingResolverQuietly = false;
            }
        }
    }

    private async Task OpenResolverAsync(string path)
    {
        try
        {
            // Another file of the open resolver asks as leaving it does (11.1).
            if (_disposed || !await TryLeaveGraphAreaAsync())
            {
                return;
            }

            var resolver = Resolver;
            CloseDiff(returnFocus: false);
            InteractiveRebase = null;
            resolver.Update(Status, CurrentName(), OntoNameOf(Status.InProgress));
            await resolver.OpenAsync(path);
            IsResolverOpen = resolver.IsOpen;
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: opening the conflict resolver on {path} failed.", ex);
        }
    }

    /// <summary>A working-tree file's diff from the stage panel while the list or the resolver is open: leaves them first (11.1).</summary>
    private async Task OpenDiffAfterLeavingAsync(ChangedFile file, bool staged)
    {
        try
        {
            if (_disposed || !await TryLeaveGraphAreaAsync())
            {
                return;
            }

            LeaveGraphArea();
            ShowWorkingTreeDiff(file, staged);
        }
        catch (Exception ex)
        {
            _log.Error($"{Name}: opening the diff of {file.Path} failed.", ex);
        }
    }

    private ConflictResolverViewModel CreateResolver()
    {
        var resolver = new ConflictResolverViewModel(Operations, _log)
        {
            MergeTool = _mergeTool,
            FileMenu = file => RepositoryMenus.ForUnstagedFile(
                MenuContext,
                new FileMenuTarget(file, IsUntracked: false, ExistsInWorkingTree: ExistsInWorkingTree(file.Path), CommitSha: null),
                MenuActions),
        };
        resolver.Closed += OnResolverClosed;
        return resolver;
    }

    private void OnResolverClosed(object? sender, EventArgs e)
    {
        IsResolverOpen = false;
        if (!_closingResolverQuietly && !_disposed)
        {
            // The next step after the last file is resolved is Continue (11.1).
            GraphFocusRequested?.Invoke(this, GraphFocusRequest.ContinueFirst);
        }
    }

    /// <summary>
    /// After every status read (11.9, 11.11): the resolver learns the conflicts and what is in
    /// progress, and git's merge tool is read again while files conflict.
    /// </summary>
    private void FollowStatusInGraphArea(WorkingTreeStatus status)
    {
        _resolver?.Update(status, CurrentName(), OntoNameOf(status.InProgress));
        if (status.Conflicts.Count > 0)
        {
            _ = ReadMergeToolAsync();
        }
    }

    private async Task ReadMergeToolAsync()
    {
        var read = ++_mergeToolReads;
        try
        {
            var tool = await Repository.ReadMergeToolAsync(cancellationToken: _lifetime.Token);
            if (read != _mergeToolReads || _disposed)
            {
                return;
            }

            _mergeTool = string.IsNullOrWhiteSpace(tool) ? null : tool;
            if (_resolver is { } resolver)
            {
                resolver.MergeTool = _mergeTool;
            }
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (Exception ex)
        {
            _log.Warning($"{Name}: git's merge tool could not be read.", ex);
        }
    }

    private string CurrentName() => Refs?.Head.BranchName ?? "HEAD";

    /// <summary>
    /// The repository has opened and its first load is done (section 9 (b), D139): the backups'
    /// clean-up runs as a background operation of the queue, <c>Clean up backups</c>, which shows
    /// nothing and never fails; when the queue is busy now, the next backup or opening does it.
    /// </summary>
    private void OnFirstLoadCompleted()
    {
        if (_disposed)
        {
            return;
        }

        var cleanUp = new GitOperation(
            CleanUpBackupsTitle,
            string.Empty,
            OperationKind.Local,
            RefreshScope.None,
            async _ =>
            {
                // CleanUpBackupsAsync logs its own failure at Warning and never throws.
                await Operations.CleanUpBackupsAsync();
                return OperationReport.Done(string.Empty);
            })
        {
            IsBackground = true,
            Prompts = PromptMode.None,
        };
        if (!_queue.TryStartInBackground(cleanUp))
        {
            _log.Debug($"{Name}: the backups' clean-up waits for the next backup or opening: the queue is busy.");
        }
    }

    /// <summary>The background operation's title (10.1): never shown, only logged.</summary>
    public const string CleanUpBackupsTitle = "Clean up backups";

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
