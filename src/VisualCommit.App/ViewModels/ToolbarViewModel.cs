using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// What the toolbar's buttons do (docs/phase-3-design.md 4d, D90): each starts one operation of
/// section 2 on the tab's repository, opening its dialog first where it has one. The toolbar
/// finds it as the repository's <see cref="RepositoryViewModel.Operations"/> when a button is
/// clicked (work package WP7's <c>RepositoryOperations</c> implements it); tests give a fake
/// through <see cref="ToolbarViewModel.Actions"/>. Each task completes when the operation has
/// ended or the dialog was cancelled; the toolbar does not wait for it, so a button stays
/// enabled while its operation runs and a second click queues a second operation (or none, by
/// the operation's dedupe key, 1.2).
/// </summary>
public interface IToolbarActions
{
    /// <summary>Fetch: fetches every remote (2.16).</summary>
    Task FetchAllAsync();

    /// <summary>Pull: pulls the current branch from its upstream as git is configured (2.18, D84).</summary>
    Task PullAsync();

    /// <summary>Push: pushes the current branch to its upstream, or opens the push dialog when it has none (2.19).</summary>
    Task PushAsync();

    /// <summary>Branch: opens the name dialog to create a branch at HEAD, "Check out the new branch" ticked (2.4).</summary>
    Task CreateBranchAtHeadAsync();

    /// <summary>Stash: opens the stash dialog (2.24).</summary>
    Task StashAsync();

    /// <summary>Pop: pops the newest stash, <c>stash@{0}</c> (2.25).</summary>
    Task PopAsync();

    /// <summary>Undo: the entry before the undo history's cursor, applied backwards as one operation (docs/phase-5-design.md 7.5).</summary>
    Task UndoAsync();

    /// <summary>Redo: the entry after the cursor, applied forwards as one operation.</summary>
    Task RedoAsync();
}

/// <summary>
/// The toolbar's buttons for one tab (docs/phase-3-design.md 4d, D90): Fetch, Pull, Push, Branch,
/// Stash and Pop act on the tab's repository; a tab without one has a toolbar whose commands are
/// all disabled and have no tooltips. A disabled button's tooltip says why
/// (<c>ToolTip.ShowOnDisabled</c>): the first reason that holds, in 4d's order. An enabled button
/// has no tooltip. The states follow the repository's refs (<see cref="RepositoryViewModel.Refs"/>)
/// and its working-tree status; until the refs have been read every button is disabled, without a
/// reason.
/// <para>
/// Phase 5's Undo, Redo and Search (docs/phase-5-design.md 1.1) have a tooltip while enabled too,
/// which is also their automation help text: what Undo and Redo would act on is seen nowhere else,
/// and Search's key is worth showing. Undo and Redo take their states from the repository's
/// operations (<see cref="RepositoryOperations.ReadUndoAvailability"/>, 7.4), read with the others,
/// again on <see cref="UpdateUndoStates"/> and on every change of the repository's queue, so that
/// the state after an action shows as soon as the queue has finished it; while those give no
/// tooltip they are disabled with <see cref="NothingToUndoText"/> and <see cref="NothingToRedoText"/>.
/// Search is enabled whenever the tab has a repository and neither the interactive rebase's list
/// nor the conflict resolver is open (11.2, 12.4).
/// </para>
/// </summary>
public sealed partial class ToolbarViewModel : ObservableObject
{
    /// <summary>Fetch without remotes.</summary>
    public const string NoRemotesText = "This repository has no remotes";

    /// <summary>Push without remotes.</summary>
    public const string AddRemoteText = "Add a remote first";

    /// <summary>Push, Branch and Stash before the first commit.</summary>
    public const string UnbornText = "Make the first commit first";

    public const string PullDetachedText = "HEAD is detached: check out a branch to pull";

    public const string PushDetachedText = "HEAD is detached: check out a branch to push";

    public const string NothingToStashText = "There are no changes to stash";

    public const string NoStashesText = "There are no stashes";

    /// <summary>Undo with an empty undo history (docs/phase-5-design.md 7.4's reason 4).</summary>
    public const string NothingToUndoText = OperationTexts.NothingToUndo;

    /// <summary>Redo with nothing undone to redo (7.4's reason 4).</summary>
    public const string NothingToRedoText = OperationTexts.NothingToRedo;

    /// <summary>Search, and Undo and Redo, while the interactive rebase's list is open (11.2, 12.4; 7.4's reason 3).</summary>
    public const string CloseInteractiveRebaseText = OperationTexts.CloseInteractiveRebaseFirst;

    /// <summary>Search while the conflict resolver is open (11.2, 12.4).</summary>
    public const string CloseConflictResolverText = "Close the conflict resolver first";

    private readonly SynchronizationContext? _ui;
    private IToolbarActions? _actions;

    /// <param name="repository">The tab's repository, or null for a tab without one.</param>
    public ToolbarViewModel(RepositoryViewModel? repository)
    {
        Repository = repository;
        _ui = SynchronizationContext.Current;
        if (repository is not null)
        {
            repository.PropertyChanged += OnRepositoryChanged;

            // An operation that starts, waits or ends changes Undo's and Redo's states without a
            // change of the refs or the status (7.4's reason 1, the entry an action just added).
            repository.Queue.Changed += OnQueueChanged;
        }

        Update();
    }

    /// <summary>The repository the buttons act on, or null.</summary>
    public RepositoryViewModel? Repository { get; }

    /// <summary>
    /// What the buttons call. Unless set (tests), the repository's
    /// <see cref="RepositoryViewModel.Operations"/>, looked up when a button is clicked.
    /// </summary>
    public IToolbarActions? Actions
    {
        get => _actions ?? (object?)Repository?.Operations as IToolbarActions;
        set => _actions = value;
    }

    /// <summary>
    /// Where Undo's and Redo's states come from (7.4). Unless set (tests), the repository's
    /// <see cref="RepositoryOperations.ReadUndoAvailability"/>, read on <see cref="UpdateUndoStates"/>.
    /// </summary>
    public Func<UndoAvailability>? UndoStates { get; set; }

    /// <summary>What Search does (3.1). Unless set (tests), the repository's <see cref="RepositoryViewModel.OpenSearch"/>.</summary>
    public Action? SearchAction { get; set; }

    /// <summary>Why Fetch is disabled, or null while it is enabled (its tooltip).</summary>
    [ObservableProperty]
    public partial string? FetchToolTip { get; private set; }

    /// <summary>Why Pull is disabled, or null while it is enabled.</summary>
    [ObservableProperty]
    public partial string? PullToolTip { get; private set; }

    /// <summary>Why Push is disabled, or null while it is enabled.</summary>
    [ObservableProperty]
    public partial string? PushToolTip { get; private set; }

    /// <summary>Why Branch is disabled, or null while it is enabled.</summary>
    [ObservableProperty]
    public partial string? BranchToolTip { get; private set; }

    /// <summary>Why Stash is disabled, or null while it is enabled.</summary>
    [ObservableProperty]
    public partial string? StashToolTip { get; private set; }

    /// <summary>Why Pop is disabled, or null while it is enabled.</summary>
    [ObservableProperty]
    public partial string? PopToolTip { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchCommand))]
    public partial bool CanFetch { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PullCommand))]
    public partial bool CanPull { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PushCommand))]
    public partial bool CanPush { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BranchCommand))]
    public partial bool CanBranch { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StashCommand))]
    public partial bool CanStash { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PopCommand))]
    public partial bool CanPop { get; private set; }

    /// <summary>
    /// Undo's tooltip (1.1): <c>Undo {text}</c> while it is enabled, otherwise its first reason
    /// (7.4); null while its state is not given yet.
    /// </summary>
    [ObservableProperty]
    public partial string? UndoToolTip { get; private set; }

    /// <summary>Redo's tooltip (1.1): <c>Redo {text}</c>, or its first reason; null while its state is not given yet.</summary>
    [ObservableProperty]
    public partial string? RedoToolTip { get; private set; }

    /// <summary>Search's tooltip (1.1): <c>Search commits (Ctrl+F)</c>, or why it is disabled (11.2); null in a tab without a repository.</summary>
    [ObservableProperty]
    public partial string? SearchToolTip { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    public partial bool CanUndo { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RedoCommand))]
    public partial bool CanRedo { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial bool CanSearch { get; private set; }

    /// <summary>
    /// The name of what is in progress as the reason "Finish or abort the &lt;kind&gt; first"
    /// names it (design 2.8, 5): "merge", "cherry-pick", "revert", "rebase", "git am".
    /// </summary>
    public static string InProgressName(InProgressKind kind) => kind switch
    {
        InProgressKind.Merge => "merge",
        InProgressKind.CherryPick => "cherry-pick",
        InProgressKind.Revert => "revert",
        InProgressKind.Rebase => "rebase",
        InProgressKind.ApplyMailbox => "git am",
        _ => kind.ToString().ToLowerInvariant(),
    };

    /// <summary>
    /// How a branch's upstream is named in texts (design, "Upstreams"): <c>&lt;r&gt;/&lt;u&gt;</c>
    /// from <see cref="GitRef.UpstreamRemote"/> and <see cref="GitRef.UpstreamRemoteRef"/>, or the
    /// local branch's name alone for a local upstream (D114); null without an upstream.
    /// </summary>
    public static string? UpstreamText(GitRef branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        const string Heads = "refs/heads/";
        var remoteRef = branch.UpstreamRemoteRef is { } full && full.StartsWith(Heads, StringComparison.Ordinal)
            ? full[Heads.Length..]
            : branch.UpstreamRemoteRef;
        if (branch.HasLocalUpstream)
        {
            return remoteRef ?? branch.Upstream;
        }

        return branch.UpstreamRemote is { } remote && remoteRef is not null ? $"{remote}/{remoteRef}" : branch.Upstream;
    }

    [RelayCommand(CanExecute = nameof(CanFetch), AllowConcurrentExecutions = true)]
    private Task Fetch() => RunAsync("Fetch", actions => actions.FetchAllAsync());

    [RelayCommand(CanExecute = nameof(CanPull), AllowConcurrentExecutions = true)]
    private Task Pull() => RunAsync("Pull", actions => actions.PullAsync());

    [RelayCommand(CanExecute = nameof(CanPush), AllowConcurrentExecutions = true)]
    private Task Push() => RunAsync("Push", actions => actions.PushAsync());

    [RelayCommand(CanExecute = nameof(CanBranch), AllowConcurrentExecutions = true)]
    private Task Branch() => RunAsync("Branch", actions => actions.CreateBranchAtHeadAsync());

    [RelayCommand(CanExecute = nameof(CanStash), AllowConcurrentExecutions = true)]
    private Task Stash() => RunAsync("Stash", actions => actions.StashAsync());

    [RelayCommand(CanExecute = nameof(CanPop), AllowConcurrentExecutions = true)]
    private Task Pop() => RunAsync("Pop", actions => actions.PopAsync());

    [RelayCommand(CanExecute = nameof(CanUndo), AllowConcurrentExecutions = true)]
    private Task Undo() => RunAsync("Undo", actions => actions.UndoAsync());

    [RelayCommand(CanExecute = nameof(CanRedo), AllowConcurrentExecutions = true)]
    private Task Redo() => RunAsync("Redo", actions => actions.RedoAsync());

    /// <summary>
    /// What Search's tooltip says while it is enabled (1.1): <c>Search commits (Ctrl+F)</c>, with
    /// the key the window takes for it (1.3).
    /// </summary>
    public static string SearchText => $"Search commits ({SearchGestureText})";

    /// <summary>
    /// The search's key in words: the platform's command modifier with F, <c>Ctrl+F</c>, or
    /// <c>Cmd+F</c> where Avalonia's hotkey configuration gives Cmd (macOS), never from a test of
    /// the operating system; Ctrl where it names none.
    /// </summary>
    public static string SearchGestureText =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers is { } modifiers && modifiers.HasFlag(KeyModifiers.Meta)
            ? "Cmd+F"
            : "Ctrl+F";

    /// <summary>
    /// Search, and its key (1.3), opens the search bar (3.1): not an operation, the repository's
    /// graph area opens it at once. What fails in it is logged; the toolbar goes on.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSearch))]
    private void Search()
    {
        try
        {
            if (SearchAction is { } search)
            {
                search();
            }
            else
            {
                Repository?.OpenSearch();
            }
        }
        catch (Exception ex)
        {
            (Repository?.Operations.Log ?? NullAppLog.Instance).Error($"{Repository?.Name}: the toolbar's Search failed.", ex);
        }
    }

    private async Task RunAsync(string button, Func<IToolbarActions, Task> action)
    {
        var log = Repository?.Operations.Log ?? NullAppLog.Instance;
        if (Actions is not { } actions)
        {
            log.Warning($"{Repository?.Name}: the toolbar's {button} has nothing to run.");
            return;
        }

        try
        {
            await action(actions);
        }
        catch (Exception ex)
        {
            // An operation reports its own failures; this is what slipped past it.
            log.Error($"{Repository?.Name}: the toolbar's {button} failed.", ex);
        }
    }

    private void OnRepositoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RepositoryViewModel.Refs) or nameof(RepositoryViewModel.Status))
        {
            Update();
        }
        else if (e.PropertyName is nameof(RepositoryViewModel.IsInteractiveRebaseOpen) or nameof(RepositoryViewModel.IsResolverOpen))
        {
            UpdateSearchState();
            UpdateUndoStates();
        }
    }

    /// <summary>The queue changed, on whatever thread it acted: Undo's and Redo's states are read again on the toolbar's own.</summary>
    private void OnQueueChanged(object? sender, EventArgs e)
    {
        if (_ui is null || SynchronizationContext.Current == _ui)
        {
            UpdateUndoStates();
        }
        else
        {
            _ui.Post(_ => UpdateUndoStates(), null);
        }
    }

    /// <summary>
    /// The buttons' states for a repository whose refs are <paramref name="refs"/> and whose
    /// working tree is <paramref name="status"/> (4d): each enabled, or disabled with the first of
    /// its reasons. The toolbar shows them; the menus that offer the same actions (the empty graph
    /// area, "Stash changes…" everywhere, D119) take them from here.
    /// </summary>
    public static ToolbarMenuState StatesFor(RepoRefs refs, WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(status);
        var (fetch, pull, push, branch, stash, pop) = Reasons(refs, status);
        return new(Of(fetch), Of(pull), Of(push), Of(branch), Of(stash), Of(pop));

        static ToolbarAvailability Of(string? reason) => new(reason is null, reason);
    }

    /// <summary>
    /// Why a stash cannot be made now (2.24, 4d, D119), or null when it can: the one reason that
    /// the toolbar's Stash, every "Stash changes…" item (4b) and the stash operation's guard
    /// (<c>RepositoryOperations.StashAsync</c>) give. Several reasons give the first, in 2.24's
    /// order: HEAD unborn ("Make the first commit first"); an operation in progress ("Finish or
    /// abort the merge first", "… the git am first"), because a stash during a merge without
    /// conflicts drops the merge without a word; conflicts without one, which a stash that did not
    /// apply cleanly leaves ("Resolve the conflicts first"); nothing to stash ("There are no changes
    /// to stash").
    /// </summary>
    public static string? StashReason(HeadState head, WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(status);
        return head.IsUnborn ? UnbornText
            : status.InProgress.IsActive ? OperationTexts.FinishOrAbortFirst(status.InProgress.Kind)
            : HasConflicts(status) ? ResolveConflictsText
            : !status.HasChanges ? NothingToStashText
            : null;
    }

    /// <summary>Stash with conflicts left by a stash, without an operation in progress (D119).</summary>
    public const string ResolveConflictsText = "Resolve the conflicts first";

    private static bool HasConflicts(WorkingTreeStatus status) =>
        status.Unstaged.Any(file => file.Kind == FileChangeKind.Conflicted) || status.Staged.Any(file => file.Kind == FileChangeKind.Conflicted);

    /// <summary>
    /// Reads Undo's and Redo's states again (7.4): when the undo history, the queue or the checks
    /// made after a refresh changed them without a change of the refs or the status. With a
    /// repository each has a tooltip, what it would do while enabled and its reason otherwise:
    /// <see cref="NothingToUndoText"/> (<see cref="NothingToRedoText"/>) while the repository's
    /// operations give none. In a tab without a repository both are disabled without a tooltip.
    /// </summary>
    public void UpdateUndoStates()
    {
        if (Repository is not { } repository)
        {
            (UndoToolTip, CanUndo) = (null, false);
            (RedoToolTip, CanRedo) = (null, false);
            return;
        }

        var undo = UndoStates?.Invoke()
            ?? (repository.IsClosed ? UndoAvailability.Disabled : repository.Operations.ReadUndoAvailability());
        (UndoToolTip, CanUndo) = (undo.UndoToolTip ?? NothingToUndoText, undo.CanUndo && undo.UndoToolTip is not null);
        (RedoToolTip, CanRedo) = (undo.RedoToolTip ?? NothingToRedoText, undo.CanRedo && undo.RedoToolTip is not null);
    }

    /// <summary>
    /// Search's state (1.1, 11.2): enabled with <see cref="SearchText"/> while the tab has a
    /// repository and neither the conflict resolver nor the interactive rebase's list is open (they
    /// hold work, and leaving them asks), otherwise disabled with the reason (12.4); in a tab
    /// without a repository disabled without a tooltip.
    /// </summary>
    private void UpdateSearchState()
    {
        var reason = Repository switch
        {
            { IsResolverOpen: true } => CloseConflictResolverText,
            { IsInteractiveRebaseOpen: true } => CloseInteractiveRebaseText,
            _ => null,
        };
        CanSearch = Repository is not null && reason is null;
        SearchToolTip = Repository is null ? null : reason ?? SearchText;
    }

    private void Update()
    {
        UpdateUndoStates();
        UpdateSearchState();
        if (Repository?.Refs is not { } refs)
        {
            // No repository, or its refs are not read yet.
            SetStates(fetch: null, pull: null, push: null, branch: null, stash: null, pop: null, enabled: false);
            return;
        }

        var (fetch, pull, push, branch, stash, pop) = Reasons(refs, Repository.Status);
        SetStates(fetch, pull, push, branch, stash, pop, enabled: true);
    }

    private static (string? Fetch, string? Pull, string? Push, string? Branch, string? Stash, string? Pop) Reasons(RepoRefs refs, WorkingTreeStatus status)
    {
        var head = refs.Head;
        var name = head.BranchName ?? "HEAD";
        var current = head.BranchName is { } branchName
            ? refs.Refs.FirstOrDefault(r => r.Kind == RefKind.LocalBranch && r.FullName == "refs/heads/" + branchName)
            : null;
        var hasRemotes = refs.Remotes.Count > 0;

        var fetch = hasRemotes ? null : NoRemotesText;

        string? pull;
        if (head.IsDetached)
        {
            pull = PullDetachedText;
        }
        else if (current?.UpstreamRef is null && current?.Upstream is null)
        {
            pull = $"{name} has no upstream: set one first";
        }
        else if (current!.UpstreamGone)
        {
            pull = $"{UpstreamText(current)} no longer exists";
        }
        else if (status.InProgress.IsActive)
        {
            pull = OperationTexts.FinishOrAbortFirst(status.InProgress.Kind);
        }
        else if (!hasRemotes && !current.HasLocalUpstream)
        {
            pull = NoRemotesText;
        }
        else
        {
            pull = null;
        }

        var push = head.IsDetached ? PushDetachedText
            : head.IsUnborn ? UnbornText
            : !hasRemotes ? AddRemoteText
            : current?.HasLocalUpstream == true ? $"{name} tracks a local branch"
            : null;
        var branch = head.IsUnborn ? UnbornText : null;
        var stash = StashReason(head, status);
        var pop = refs.Stashes.Count == 0 ? NoStashesText : null;
        return (fetch, pull, push, branch, stash, pop);
    }

    private void SetStates(string? fetch, string? pull, string? push, string? branch, string? stash, string? pop, bool enabled)
    {
        (FetchToolTip, CanFetch) = (fetch, enabled && fetch is null);
        (PullToolTip, CanPull) = (pull, enabled && pull is null);
        (PushToolTip, CanPush) = (push, enabled && push is null);
        (BranchToolTip, CanBranch) = (branch, enabled && branch is null);
        (StashToolTip, CanStash) = (stash, enabled && stash is null);
        (PopToolTip, CanPop) = (pop, enabled && pop is null);
    }
}

/// <summary>
/// The toolbar's Undo and Redo now (docs/phase-5-design.md 1.1, 7.4): each enabled or not, with
/// its tooltip, <c>Undo {text}</c> while enabled or the first reason it is disabled.
/// </summary>
/// <param name="CanUndo">Undo is enabled.</param>
/// <param name="UndoToolTip">Its tooltip; null while its state is not known.</param>
/// <param name="CanRedo">Redo is enabled.</param>
/// <param name="RedoToolTip">Its tooltip; null while its state is not known.</param>
public sealed record UndoAvailability(bool CanUndo, string? UndoToolTip, bool CanRedo, string? RedoToolTip)
{
    /// <summary>Both disabled, without a tooltip: a tab without a repository, or a state not known yet.</summary>
    public static UndoAvailability Disabled { get; } = new(false, null, false, null);
}
