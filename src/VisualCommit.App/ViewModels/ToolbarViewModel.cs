using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Menus;
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
}

/// <summary>
/// The toolbar's buttons for one tab (docs/phase-3-design.md 4d, D90): Fetch, Pull, Push, Branch,
/// Stash and Pop act on the tab's repository; a tab without one has a toolbar whose commands are
/// all disabled and have no tooltips. A disabled button's tooltip says why
/// (<c>ToolTip.ShowOnDisabled</c>): the first reason that holds, in 4d's order. An enabled button
/// has no tooltip. The states follow the repository's refs (<see cref="RepositoryViewModel.Refs"/>)
/// and its working-tree status; until the refs have been read every button is disabled, without a
/// reason. Undo, Redo and Search are placeholders until phase 5 and are not here.
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

    private IToolbarActions? _actions;

    /// <param name="repository">The tab's repository, or null for a tab without one.</param>
    public ToolbarViewModel(RepositoryViewModel? repository)
    {
        Repository = repository;
        if (repository is not null)
        {
            repository.PropertyChanged += OnRepositoryChanged;
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
    /// Why Stash cannot be used (2.24, 4d, D119), or null when it can: HEAD unborn; nothing to
    /// stash; an operation in progress ("Finish or abort the merge first"), or conflicts that a
    /// stash left without one ("Resolve the conflicts first"), because a stash during a merge drops
    /// the merge without a word. Several reasons give the first, in this order.
    /// </summary>
    public static string? StashReason(HeadState head, WorkingTreeStatus status)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(status);
        return head.IsUnborn ? UnbornText
            : !status.HasChanges ? NothingToStashText
            : status.InProgress.IsActive ? $"Finish or abort the {InProgressName(status.InProgress.Kind)} first"
            : HasConflicts(status) ? ResolveConflictsText
            : null;
    }

    /// <summary>Stash with conflicts left by a stash, without an operation in progress (D119).</summary>
    public const string ResolveConflictsText = "Resolve the conflicts first";

    private static bool HasConflicts(WorkingTreeStatus status) =>
        status.Unstaged.Any(file => file.Kind == FileChangeKind.Conflicted) || status.Staged.Any(file => file.Kind == FileChangeKind.Conflicted);

    private void Update()
    {
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
            pull = $"Finish or abort the {InProgressName(status.InProgress.Kind)} first";
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
