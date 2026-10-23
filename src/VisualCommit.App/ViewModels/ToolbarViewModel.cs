using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Commands;
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
/// says what it does with its key, <c>{what it does} ({key})</c> (docs/phase-6-design.md 6.8,
/// D215; phase 3's "an enabled button has no tooltip" goes): <c>Fetch all remotes (Ctrl+Shift+F)</c>,
/// <c>Pull from origin/main (Ctrl+Shift+L)</c>. The reasons alone are the <c>XDisabledReason</c>
/// properties, which the menus read (<see cref="ToolbarMenuState.Of"/>). The states follow the
/// repository's refs (<see cref="RepositoryViewModel.Refs"/>) and its working-tree status; until
/// the refs have been read every button is disabled, without a reason.
/// <para>
/// Phase 5's Undo, Redo and Search (docs/phase-5-design.md 1.1): what Undo and Redo would act on,
/// with their keys while enabled. Undo and Redo take their states from the repository's
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
    private KeyTexts _keys = KeyTexts.Current;

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

    /// <summary>
    /// Whose words the tooltips' keys use (docs/phase-6-design.md 6.4): <see cref="KeyTexts.Current"/>,
    /// unless a test of another platform's texts sets its own; setting it writes the tooltips again.
    /// </summary>
    public KeyTexts Keys
    {
        get => _keys;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _keys = value;
            if (Dashboard is null)
            {
                Update();
            }
            else
            {
                UpdateDashboardStates();
            }
        }
    }

    /// <summary>Fetch's tooltip and help text: <c>Fetch all remotes ({key})</c> while enabled, its reason while disabled (6.8).</summary>
    [ObservableProperty]
    public partial string? FetchToolTip { get; private set; }

    /// <summary>Pull's: <c>Pull from {up} ({key})</c>, or its reason.</summary>
    [ObservableProperty]
    public partial string? PullToolTip { get; private set; }

    /// <summary>Push's: <c>Push {b} to {up} ({key})</c>, <c>Push {b} to a remote ({key})</c> without a remote upstream, or its reason.</summary>
    [ObservableProperty]
    public partial string? PushToolTip { get; private set; }

    /// <summary>Branch's: <c>Create a branch at HEAD ({key})</c>, or its reason.</summary>
    [ObservableProperty]
    public partial string? BranchToolTip { get; private set; }

    /// <summary>Stash's: <c>Stash your changes ({key})</c>, or its reason.</summary>
    [ObservableProperty]
    public partial string? StashToolTip { get; private set; }

    /// <summary>Pop's: <c>Pop the newest stash ({key})</c>, or its reason.</summary>
    [ObservableProperty]
    public partial string? PopToolTip { get; private set; }

    /// <summary>
    /// Why Fetch is disabled, or null while it is enabled or no reason is known (docs/phase-6-design.md
    /// 6.8): what <see cref="ToolbarMenuState.Of"/> and the palette read as its reason, so that a
    /// tooltip that speaks while enabled never becomes a menu item's reason.
    /// </summary>
    [ObservableProperty]
    public partial string? FetchDisabledReason { get; private set; }

    /// <summary>Why Pull is disabled, or null (6.8).</summary>
    [ObservableProperty]
    public partial string? PullDisabledReason { get; private set; }

    /// <summary>Why Push is disabled, or null (6.8).</summary>
    [ObservableProperty]
    public partial string? PushDisabledReason { get; private set; }

    /// <summary>Why Branch is disabled, or null (6.8).</summary>
    [ObservableProperty]
    public partial string? BranchDisabledReason { get; private set; }

    /// <summary>Why Stash is disabled, or null (6.8).</summary>
    [ObservableProperty]
    public partial string? StashDisabledReason { get; private set; }

    /// <summary>Why Pop is disabled, or null (6.8).</summary>
    [ObservableProperty]
    public partial string? PopDisabledReason { get; private set; }

    /// <summary>Why Undo is disabled (7.4's reasons), or null while it is enabled or its state is not given yet (6.8).</summary>
    [ObservableProperty]
    public partial string? UndoDisabledReason { get; private set; }

    /// <summary>Why Redo is disabled, or null (6.8).</summary>
    [ObservableProperty]
    public partial string? RedoDisabledReason { get; private set; }

    /// <summary>Why Search is disabled (11.2's reasons), or null while it is enabled or the tab has no repository (6.8).</summary>
    [ObservableProperty]
    public partial string? SearchDisabledReason { get; private set; }

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
    /// Undo's tooltip (1.1): <c>Undo {text} ({key})</c> while it is enabled (docs/phase-6-design.md
    /// 6.8), otherwise its first reason (7.4); null while its state is not given yet.
    /// </summary>
    [ObservableProperty]
    public partial string? UndoToolTip { get; private set; }

    /// <summary>Redo's tooltip (1.1): <c>Redo {text} ({key})</c>, or its first reason; null while its state is not given yet.</summary>
    [ObservableProperty]
    public partial string? RedoToolTip { get; private set; }

    /// <summary>
    /// What Undo would act on while it is enabled, without its key (<c>Undo commit "Fix the
    /// parser"</c>): the palette's label (docs/phase-6-design.md 5.4); null while it is disabled.
    /// </summary>
    [ObservableProperty]
    public partial string? UndoText { get; private set; }

    /// <summary>What Redo would act on while it is enabled, without its key; null while it is disabled.</summary>
    [ObservableProperty]
    public partial string? RedoText { get; private set; }

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
    /// How a branch's upstream is named in texts (design, "Upstreams"; docs/phase-6-design.md 10.4,
    /// GD15): git's <c>%(upstream)</c> (<see cref="GitRef.UpstreamRef"/>) without <c>refs/remotes/</c>
    /// when it lies there, the name <c>git status</c> shortens (<c>upstream/pr/7</c> for a branch
    /// that tracks <c>refs/pull/7/head</c> on <c>upstream</c>); otherwise <c>&lt;r&gt;/&lt;u&gt;</c> from
    /// <see cref="GitRef.UpstreamRemote"/> and <see cref="GitRef.UpstreamRemoteRef"/>; the local
    /// branch's name alone for a local upstream (D114); null without an upstream. Git maps the ref
    /// on the remote forward by the remote's first fetch line that matches it, in configuration
    /// order, as a push updates the tracking ref; a negative line takes that mapping away only
    /// from an exact line (with it, git shows no upstream at all). With the default lines every
    /// upstream reads <c>&lt;r&gt;/&lt;u&gt;</c>, as before. A mirror line (<c>+refs/*:refs/*</c>)
    /// before the remote's other lines makes <c>%(upstream)</c> the local branch of the same name,
    /// so the text falls back to <c>&lt;r&gt;/&lt;u&gt;</c> (git's status says just the branch's
    /// name). Display only: what an operation acts on is still the three values (D101).
    /// </summary>
    public static string? UpstreamText(GitRef branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        const string Heads = "refs/heads/";
        const string Remotes = "refs/remotes/";
        var remoteRef = branch.UpstreamRemoteRef is { } full && full.StartsWith(Heads, StringComparison.Ordinal)
            ? full[Heads.Length..]
            : branch.UpstreamRemoteRef;
        if (branch.HasLocalUpstream)
        {
            return remoteRef ?? branch.Upstream;
        }

        if (branch.UpstreamRef is { } tracking && tracking.Length > Remotes.Length && tracking.StartsWith(Remotes, StringComparison.Ordinal))
        {
            return tracking[Remotes.Length..];
        }

        return branch.UpstreamRemote is { } remote && remoteRef is not null ? $"{remote}/{remoteRef}" : branch.Upstream;
    }

    /// <summary>
    /// Why a branch's Push and Force push are disabled when its upstream is no branch on its remote
    /// (docs/phase-6-design.md 10.5's R4, <see cref="OperationTexts.MenuTexts.NotABranch"/>): its
    /// <see cref="GitRef.UpstreamRemoteRef"/> is not under <c>refs/heads/</c>, as a branch checked out
    /// from a ref GitHub's pull-request line alone fetches tracks <c>refs/pull/1/head</c> (0.5's P2),
    /// or a short name a fetch line without <c>refs/heads/</c> gave (O2). The upstream is named as
    /// <see cref="UpstreamText"/> names it. Null for a branch without a remote upstream and for one
    /// whose upstream is a branch.
    /// </summary>
    public static string? UpstreamNotABranchReason(GitRef branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        return branch is { HasLocalUpstream: false, UpstreamRemote: { } remote, UpstreamRemoteRef: { } remoteRef }
            && !remoteRef.StartsWith("refs/heads/", StringComparison.Ordinal)
                ? OperationTexts.MenuTexts.NotABranch(UpstreamText(branch) ?? $"{remote}/{remoteRef}", remoteRef, remote)
                : null;
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
    /// the key the window takes for it (1.3), in <see cref="KeyTexts.Current"/>'s words.
    /// </summary>
    public static string SearchText => SearchTextFor(KeyTexts.Current);

    /// <summary>
    /// The search's key in words (docs/phase-6-design.md 6.4): the platform's command modifier with
    /// F, <c>Ctrl+F</c>, or <c>Cmd+F</c> where the key texts are macOS's (<see cref="KeyTexts.Current"/>,
    /// set from Avalonia's hotkey configuration, never from a test of the operating system).
    /// </summary>
    public static string SearchGestureText => KeyTexts.Current.Of(AppCommand.Search) ?? string.Empty;

    /// <summary><see cref="SearchText"/> in <paramref name="keys"/>' words.</summary>
    public static string SearchTextFor(KeyTexts keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return keys.WithKey("Search commits", AppCommand.Search);
    }

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
            (UndoToolTip, UndoText, CanUndo, UndoDisabledReason) = (null, null, false, null);
            (RedoToolTip, RedoText, CanRedo, RedoDisabledReason) = (null, null, false, null);
            return;
        }

        var undo = UndoStates?.Invoke()
            ?? (repository.IsClosed ? UndoAvailability.Disabled : repository.Operations.ReadUndoAvailability());
        var (undoText, canUndo) = (undo.UndoToolTip ?? NothingToUndoText, undo.CanUndo && undo.UndoToolTip is not null);
        var (redoText, canRedo) = (undo.RedoToolTip ?? NothingToRedoText, undo.CanRedo && undo.RedoToolTip is not null);

        // UndoAvailability keeps "Undo {text}"; the toolbar appends the key while enabled (6.8).
        UndoDisabledReason = canUndo ? null : undoText;
        RedoDisabledReason = canRedo ? null : redoText;
        UndoText = canUndo ? undoText : null;
        RedoText = canRedo ? redoText : null;
        UndoToolTip = canUndo ? _keys.WithKey(undoText, AppCommand.Undo) : undoText;
        RedoToolTip = canRedo ? _keys.WithKey(redoText, AppCommand.Redo) : redoText;
        (CanUndo, CanRedo) = (canUndo, canRedo);
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
        SearchDisabledReason = Repository is null ? null : reason;
        SearchToolTip = Repository is null ? null : reason ?? SearchTextFor(_keys);
    }

    private void Update()
    {
        UpdateUndoStates();
        UpdateSearchState();
        if (Repository?.Refs is not { } refs)
        {
            // No repository, or its refs are not read yet.
            SetStates(fetch: null, pull: null, push: null, branch: null, stash: null, pop: null, enabled: false, refs: null);
            return;
        }

        var (fetch, pull, push, branch, stash, pop) = Reasons(refs, Repository.Status);
        SetStates(fetch, pull, push, branch, stash, pop, enabled: true, refs);
    }

    /// <summary>
    /// What Pull and Push say while enabled (6.8), without the key: <c>Pull from {up}</c>, and
    /// <c>Push {b} to {up}</c> with a remote upstream or <c>Push {b} to a remote</c> without one;
    /// the upstream named by <see cref="UpstreamText"/>.
    /// </summary>
    private static (string Pull, string Push) EnabledTexts(RepoRefs? refs)
    {
        var name = refs?.Head.BranchName ?? "HEAD";
        var current = refs?.Head.BranchName is { } branchName
            ? refs.Refs.FirstOrDefault(r => r.Kind == RefKind.LocalBranch && r.FullName == "refs/heads/" + branchName)
            : null;
        var upstream = current is null ? null : UpstreamText(current);
        var pull = $"Pull from {upstream}";
        var push = upstream is not null && current is { HasLocalUpstream: false } ? $"Push {name} to {upstream}" : $"Push {name} to a remote";
        return (pull, push);
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
            : current is not null ? UpstreamNotABranchReason(current)
            : null;
        var branch = head.IsUnborn ? UnbornText : null;
        var stash = StashReason(head, status);
        var pop = refs.Stashes.Count == 0 ? NoStashesText : null;
        return (fetch, pull, push, branch, stash, pop);
    }

    /// <summary>
    /// Sets each button's state, its reason and its tooltip (6.8): <c>XDisabledReason ?? (CanX ?
    /// {what it does} ({key}) : null)</c>, so that a disabled button keeps its reason alone and one
    /// without a reason (refs not read) has none.
    /// </summary>
    private void SetStates(string? fetch, string? pull, string? push, string? branch, string? stash, string? pop, bool enabled, RepoRefs? refs)
    {
        var (pullText, pushText) = EnabledTexts(refs);
        (FetchDisabledReason, CanFetch) = (fetch, enabled && fetch is null);
        (PullDisabledReason, CanPull) = (pull, enabled && pull is null);
        (PushDisabledReason, CanPush) = (push, enabled && push is null);
        (BranchDisabledReason, CanBranch) = (branch, enabled && branch is null);
        (StashDisabledReason, CanStash) = (stash, enabled && stash is null);
        (PopDisabledReason, CanPop) = (pop, enabled && pop is null);
        FetchToolTip = fetch ?? (CanFetch ? _keys.WithKey("Fetch all remotes", AppCommand.Fetch) : null);
        PullToolTip = pull ?? (CanPull ? _keys.WithKey(pullText, AppCommand.Pull) : null);
        PushToolTip = push ?? (CanPush ? _keys.WithKey(pushText, AppCommand.Push) : null);
        BranchToolTip = branch ?? (CanBranch ? _keys.WithKey("Create a branch at HEAD", AppCommand.CreateBranch) : null);
        StashToolTip = stash ?? (CanStash ? _keys.WithKey("Stash your changes", AppCommand.Stash) : null);
        PopToolTip = pop ?? (CanPop ? _keys.WithKey("Pop the newest stash", AppCommand.Pop) : null);
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
