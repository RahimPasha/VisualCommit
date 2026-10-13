// The repository's menus: docs/phase-3-design.md 4a (opening a menu by pointer and by keyboard),
// 4b (what a right-click on each object opens; a right-click on a graph row selects it first,
// one on a left-panel row selects nothing, D114), 4c (the double-click map, D93) and 4g (the
// drop's menu, D120). WP7-core built the snapshot the builders read (MenuContext,
// ReadMenuContextAsync), IsClosed and the shared OpenMenu; WP7-menus the entry points the views
// call, each of which builds its menu from a fresh context and opens it through OpenMenu.

using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

public sealed partial class RepositoryViewModel
{
    private IReadOnlyList<WorktreeInfo> _otherWorktrees = [];
    private IRepositoryActions? _menuActions;

    /// <summary>The tab was closed (<see cref="Dispose"/>): operations start nothing and show nothing (1.8).</summary>
    public bool IsClosed => _disposed;

    /// <summary>
    /// What the menus' items and the double-clicks start: <see cref="Operations"/>, unless a test
    /// gave a recording fake.
    /// </summary>
    public IRepositoryActions MenuActions
    {
        get => _menuActions ?? Operations;
        set => _menuActions = value;
    }

    /// <summary>
    /// The last menu that waits for a read (a drop's, a local branch's with its fast-forward), as a
    /// task that completes with whether it opened; completed before any. Tests wait on it.
    /// </summary>
    public Task<bool> PendingMenu { get; private set; } = Task.FromResult(false);

    /// <summary>
    /// The snapshot the menu builders read (<see cref="RepositoryMenuContext"/>), taken now: the
    /// refs, the status, the selected commits in row order with whether HEAD's history holds them
    /// (4h) and where they are on HEAD's linear line (phase 4 design 2.1), the toolbar's states,
    /// whether an operation runs, the other worktrees as last read by
    /// <see cref="ReadMenuContextAsync"/> (empty before), the branch tips HEAD reaches (1.1), and
    /// what the graph hides and solos (docs/phase-5-design.md 8.3).
    /// Before the refs are read: an unborn HEAD without refs, every toolbar button disabled.
    /// </summary>
    public RepositoryMenuContext MenuContext => new(
        Name,
        Path,
        Refs ?? new RepoRefs(new HeadState(null, null), [], [], []),
        Status,
        SelectedCommitsForMenus(),
        Refs is null ? ToolbarMenuState.Disabled : ToolbarMenuState.Of(Toolbar),
        Queue.Current is not null)
    {
        OtherWorktrees = _otherWorktrees,
        TipsOnHead = BranchTipsOnHead(),
        MergeTool = _mergeTool,
        Filters = Filters,
    };

    /// <summary>
    /// Reads the repository's worktrees (<c>git worktree list --porcelain</c>, a read) and returns
    /// <see cref="MenuContext"/> with the other ones: for the menus whose items depend on them (a
    /// local branch's fast-forward, 2.7; a drop, 4g). When the read fails, the worktrees last read.
    /// </summary>
    public async Task<RepositoryMenuContext> ReadMenuContextAsync()
    {
        if (IsClosed)
        {
            return MenuContext;
        }

        try
        {
            var worktrees = await Repository.ReadWorktreesAsync(_lifetime.Token);
            _otherWorktrees = [.. worktrees.Where(worktree => !SamePath(worktree.Path, Path))];
        }
        catch (OperationCanceledException) when (IsClosed)
        {
            // The tab is closing.
        }
        catch (Exception ex)
        {
            _log.Warning($"{Name}: the worktrees could not be read for a menu.", ex);
        }

        return MenuContext;
    }

    /// <summary>
    /// Opens <paramref name="menu"/> on the window's menu layer (4a): at the pointer for
    /// <see cref="MenuOpenedBy.Pointer"/>, with its top-left at <paramref name="position"/> (the
    /// focused row's bottom-left corner) for <see cref="MenuOpenedBy.Keyboard"/>. Returns whether a
    /// menu opened, which the view uses to mark the request handled; nothing opens for null, a
    /// menu without items, or a closed tab.
    /// </summary>
    public bool OpenMenu(MenuLayerViewModel layer, MenuModel? menu, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (IsClosed || menu is null || !menu.Items.Any())
        {
            return false;
        }

        layer.Open(menu, position, openedBy);
        return true;
    }

    /// <summary>
    /// A right-click (or Shift+F10, the Menu key) on a commit's row outside its labels (4b): the row
    /// is selected alone first unless it is part of a multi-selection, whose menu then opens; a
    /// stash row opens the stash's menu.
    /// </summary>
    public bool OpenCommitMenu(MenuLayerViewModel layer, string sha, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(sha);
        if (IsClosed || !SelectForMenu(sha))
        {
            return false;
        }

        var context = MenuContext;
        var menu = context.HasMultiSelection && context.FindSelected(sha) is not null
            ? RepositoryMenus.ForCommits(context, MenuActions)
            : RepositoryMenus.ForCommit(context, sha, MenuActions);
        return OpenMenu(layer, menu, position, openedBy);
    }

    /// <summary>
    /// A right-click on a graph label (4b): the menu of what it stands for (a local or remote
    /// branch, a tag, a stash; <c>HEAD</c> and the "+N" label, <paramref name="label"/> null, open
    /// the commit's menu). The row is selected too.
    /// </summary>
    public bool OpenLabelMenu(MenuLayerViewModel layer, string sha, RefLabel? label, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(sha);
        switch (label)
        {
            case null or { Kind: RefLabelKind.DetachedHead }:
                return OpenCommitMenu(layer, sha, position, openedBy);

            case { Kind: RefLabelKind.Stash }:
                return !IsClosed && SelectForMenu(sha) && OpenMenu(layer, RepositoryMenus.ForStash(MenuContext, sha, MenuActions), position, openedBy);

            case { RefName: { } refName }:
                if (IsClosed || !SelectForMenu(sha))
                {
                    return false;
                }

                return label.Kind switch
                {
                    RefLabelKind.CurrentBranch or RefLabelKind.LocalBranch => OpenLocalBranchMenu(layer, refName, position, openedBy),
                    RefLabelKind.RemoteBranch => OpenMenu(layer, RepositoryMenus.ForRemoteBranch(MenuContext, refName, MenuActions), position, openedBy),
                    RefLabelKind.Tag => OpenMenu(layer, RepositoryMenus.ForTag(MenuContext, refName, MenuActions), position, openedBy),
                    _ => false,
                };

            default:
                return false;
        }
    }

    /// <summary>A right-click on the working-changes row (4b); the row is selected.</summary>
    public bool OpenWorkingChangesMenu(MenuLayerViewModel layer, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (IsClosed || !HasWorkingChanges)
        {
            return false;
        }

        if (!IsWorkingRowSelected)
        {
            IsWorkingRowSelected = true;
        }

        return OpenMenu(layer, RepositoryMenus.ForWorkingChanges(MenuContext, MenuActions), position, openedBy);
    }

    /// <summary>
    /// A right-click below the last row of the graph, or in a graph without commits (4b). The
    /// graph's selection does not change: asking for a menu changes nothing (check 3, as D114 for
    /// the left panel).
    /// </summary>
    public bool OpenEmptyGraphAreaMenu(MenuLayerViewModel layer, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return !IsClosed && Refs is not null && OpenMenu(layer, RepositoryMenus.ForEmptyGraphArea(MenuContext, MenuActions), position, openedBy);
    }

    /// <summary>
    /// A right-click on a left-panel row (4b): a section header, a remote, a local or remote
    /// branch, a tag or a stash; a folder and "Pull requests" open nothing. Nothing is selected or
    /// highlighted, and the graph's selection does not change (D114).
    /// </summary>
    public bool OpenRefRowMenu(MenuLayerViewModel layer, RefListRow row, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(row);
        if (IsClosed || Refs is null)
        {
            return false;
        }

        var actions = MenuActions;
        return LeftPanelViewModel.TargetOf(row) switch
        {
            RefRowTarget.Section when row is RefSectionRow section =>
                OpenMenu(layer, RepositoryMenus.ForSection(MenuContext, section.Section, actions), position, openedBy),
            RefRowTarget.Remote =>
                OpenMenu(layer, RepositoryMenus.ForRemote(MenuContext, LeftPanelViewModel.RemoteNameOf(row)!, actions), position, openedBy),
            RefRowTarget.LocalBranch =>
                OpenLocalBranchMenu(layer, LeftPanelViewModel.RefNameOf(row)!, position, openedBy),
            RefRowTarget.RemoteBranch =>
                OpenMenu(layer, RepositoryMenus.ForRemoteBranch(MenuContext, LeftPanelViewModel.RefNameOf(row)!, actions), position, openedBy),
            RefRowTarget.Tag =>
                OpenMenu(layer, RepositoryMenus.ForTag(MenuContext, LeftPanelViewModel.RefNameOf(row)!, actions), position, openedBy),
            RefRowTarget.Stash when row is RefItemRow { TargetSha: { } stash } =>
                OpenMenu(layer, RepositoryMenus.ForStash(MenuContext, stash, actions), position, openedBy),
            _ => false,
        };
    }

    /// <summary>
    /// A right-click on a file of the stage panel's lists or of a commit's changed files (4b): its
    /// menu only; the list's selection and the open diff do not change.
    /// </summary>
    public bool OpenFileMenu(MenuLayerViewModel layer, ChangedFile file, FileMenuPlace place, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(file);
        if (IsClosed)
        {
            return false;
        }

        var context = MenuContext;
        var target = new FileMenuTarget(
            file,
            IsUntracked: place == FileMenuPlace.Unstaged && Status.IsUntracked(file.Path),
            ExistsInWorkingTree: ExistsInWorkingTree(file.Path),
            CommitSha: place == FileMenuPlace.CommitFiles ? Selection.Single : null);
        var menu = place switch
        {
            FileMenuPlace.Unstaged => RepositoryMenus.ForUnstagedFile(context, target, MenuActions),
            FileMenuPlace.Staged => RepositoryMenus.ForStagedFile(context, target, MenuActions),
            _ => RepositoryMenus.ForCommitFile(context, target, MenuActions),
        };
        return OpenMenu(layer, menu, position, openedBy);
    }

    /// <summary>A right-click on the diff view's text or header (4b); <paramref name="hunk"/> is the hunk under the pointer, or null.</summary>
    public bool OpenDiffMenu(MenuLayerViewModel layer, DiffViewModel diff, int? hunk, MenuPosition position, MenuOpenedBy openedBy)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(diff);
        if (IsClosed)
        {
            return false;
        }

        var target = diff.Target;
        var file = new FileMenuTarget(
            new ChangedFile(target.Path, target.Kind, target.OldPath),
            target.IsUntracked,
            ExistsInWorkingTree(target.Path),
            target.Side == DiffSide.Commit ? target.Commit : null);
        var hunkInDiff = hunk is { } index && diff.Diff is { } read && index >= 0 && index < read.Hunks.Count ? hunk : null;
        var menu = RepositoryMenus.ForDiff(
            MenuContext,
            new DiffMenuTarget(file, target.Side, diff.HasSelectedLines, hunkInDiff, diff.ActsOnLines),
            MenuActions,
            new DiffMenuActions(diff));
        return OpenMenu(layer, menu, position, openedBy);
    }

    /// <summary>
    /// A drop of the branch <paramref name="sourceRef"/> onto <paramref name="targetRef"/> (4g): reads
    /// what the drop needs (<see cref="DropFacts"/>, the worktrees), then opens its menu at the
    /// release point, unless Esc, another menu, a dialog or another tab came first (4a). Completes
    /// with whether a menu opened.
    /// </summary>
    public async Task<bool> OpenDropMenuAsync(MenuLayerViewModel layer, string sourceRef, string targetRef, MenuPosition position)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(sourceRef);
        ArgumentNullException.ThrowIfNull(targetRef);
        if (IsClosed)
        {
            return false;
        }

        var request = layer.Request(this);
        var context = await ReadMenuContextAsync();
        if (await ReadDropFactsAsync(sourceRef, targetRef) is not { } drop)
        {
            return false;
        }

        context = context with { Drop = drop };
        return OpenRequested(layer, request, RepositoryMenus.ForDrop(context, sourceRef, targetRef, MenuActions), position, MenuOpenedBy.Pointer);
    }

    /// <summary>
    /// What a drop of <paramref name="sourceRef"/> onto <paramref name="targetRef"/> reads before
    /// its menu opens (4g, phase 4 design 1.1), with one <c>git merge-base --is-ancestor
    /// &lt;target&gt; &lt;source&gt;</c>: for a local or remote target whether the source
    /// contains it (<see cref="DropFacts.SourceContainsTarget"/>, null when the read failed), and
    /// for a local target whether it can be fast-forwarded to the source
    /// (<see cref="DropFacts.TargetIsAncestorOfSource"/>, true when the read failed, so that git
    /// decides). Null when the tab closed meanwhile.
    /// </summary>
    /// <remarks>
    /// <see cref="DropFacts.RemoteMapsBranchesDifferently"/> is not read: the Git layer has no read
    /// of a remote's fetch refspec yet, so a drop onto a remote branch always offers the push.
    /// </remarks>
    public async Task<DropFacts?> ReadDropFactsAsync(string sourceRef, string targetRef)
    {
        ArgumentNullException.ThrowIfNull(sourceRef);
        ArgumentNullException.ThrowIfNull(targetRef);
        var ancestor = true;
        bool? sourceContainsTarget = null;
        var localTarget = targetRef.StartsWith("refs/heads/", StringComparison.Ordinal);
        if (localTarget || targetRef.StartsWith("refs/remotes/", StringComparison.Ordinal))
        {
            try
            {
                // B is an ancestor of A: A contains B (phase 4's rebase items, 1.1), and a local
                // B can be fast-forwarded to A (2.7, 2.10).
                sourceContainsTarget = await Repository.IsAncestorAsync(targetRef, sourceRef, _lifetime.Token);
                if (localTarget)
                {
                    ancestor = sourceContainsTarget.Value;
                }
            }
            catch (OperationCanceledException) when (IsClosed)
            {
                return null;
            }
            catch (Exception ex)
            {
                // The fast-forward and the rebase items stay enabled and git decides (2.7 refuses a non-fast-forward).
                _log.Warning($"{Name}: whether {targetRef} is an ancestor of {sourceRef} could not be read for the drop's menu.", ex);
            }
        }

        return new DropFacts(ancestor) { SourceContainsTarget = sourceContainsTarget };
    }

    /// <summary>
    /// The menu of a request whose read has completed (<see cref="MenuLayerViewModel.Request"/>):
    /// it opens only while nothing has dropped the request since (Esc, another menu or request, a
    /// dialog, another tab; 4a) and the tab is still open.
    /// </summary>
    private bool OpenRequested(MenuLayerViewModel layer, MenuRequest request, MenuModel? menu, MenuPosition position, MenuOpenedBy openedBy) =>
        !IsClosed && menu is not null && menu.Items.Any() && layer.OpenRequested(request, menu, position, openedBy);

    /// <summary>
    /// What a drop of a branch onto a branch opens (WP8's <see cref="BranchDropMenu"/> hook): the
    /// drop's menu at the release point on <paramref name="layer"/>, once its facts are read (4g).
    /// The function returns null, because the menu opens when the read completes, not at once.
    /// </summary>
    public Func<BranchDrop, MenuModel?> DropMenuOpener(MenuLayerViewModel layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return drop =>
        {
            PendingMenu = OpenDropMenuAsync(layer, drop.SourceRef, drop.TargetRef, new MenuPosition(drop.X, drop.Y));
            Start(PendingMenu, "the drop's menu");
            return null;
        };
    }

    /// <summary>
    /// A double-click on a left-panel row (4c): a local branch checks out (nothing more on the
    /// current one), a remote branch checks out into a tracking branch (nothing while HEAD is on
    /// the local branch it would check out), a stash applies; any other row does nothing more than
    /// its click.
    /// </summary>
    public void DoubleTapRefRow(RefListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (IsClosed || Refs is null)
        {
            return;
        }

        switch (LeftPanelViewModel.TargetOf(row))
        {
            case RefRowTarget.LocalBranch or RefRowTarget.RemoteBranch:
                CheckOutByDoubleClick(LeftPanelViewModel.RefNameOf(row)!);
                break;
            case RefRowTarget.Stash when row is RefItemRow { TargetSha: { } stash }:
                Start(MenuActions.ApplyStashAsync(stash), "applying the stash");
                break;
        }
    }

    /// <summary>
    /// A double-click on a graph label (4c): a local branch checks out (nothing more on the current
    /// one), a remote branch checks out into a tracking branch; a tag, a stash or <c>HEAD</c> does
    /// nothing more than the click (D93).
    /// </summary>
    public void DoubleTapLabel(RefLabel label)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (IsClosed || Refs is null)
        {
            return;
        }

        if (label is { Kind: RefLabelKind.LocalBranch or RefLabelKind.RemoteBranch, RefName: { } refName })
        {
            CheckOutByDoubleClick(refName);
        }
    }

    /// <summary>Checks out a branch by a double-click, unless HEAD is on it already (as the menus leave out or disable the item).</summary>
    private void CheckOutByDoubleClick(string refName)
    {
        var context = MenuContext;
        if (context.FindRef(refName) is not { } branch)
        {
            return;
        }

        var local = branch.Kind == RefKind.RemoteBranch ? RepositoryMenuContext.LocalNameOf(branch) : branch.Name;
        if (branch.IsHead || string.Equals(context.Head.BranchName, local, StringComparison.Ordinal))
        {
            return;
        }

        Start(MenuActions.CheckoutAsync(refName), "the checkout");
    }

    /// <summary>
    /// A local branch's menu (4b). Its fast-forward item depends on the other worktrees (2.7): when
    /// that item would otherwise be enabled, they are read first and the menu opens when the read
    /// completes, unless Esc, another menu, a dialog or another tab came first (4a); the request
    /// counts as handled at once.
    /// </summary>
    private bool OpenLocalBranchMenu(MenuLayerViewModel layer, string branchRef, MenuPosition position, MenuOpenedBy openedBy)
    {
        var context = MenuContext;
        if (context.FindRef(branchRef) is not { } branch)
        {
            return false;
        }

        var fastForwardable = !branch.IsHead && !branch.UpstreamGone && branch.UpstreamRef is not null && branch.Behind > 0 && branch.Ahead == 0;
        if (!fastForwardable)
        {
            return OpenMenu(layer, RepositoryMenus.ForLocalBranch(context, branchRef, MenuActions), position, openedBy);
        }

        PendingMenu = OpenAfterReadingWorktreesAsync(layer, layer.Request(this), branchRef, position, openedBy);
        Start(PendingMenu, "a branch's menu");
        return true;
    }

    private async Task<bool> OpenAfterReadingWorktreesAsync(MenuLayerViewModel layer, MenuRequest request, string branchRef, MenuPosition position, MenuOpenedBy openedBy)
    {
        var context = await ReadMenuContextAsync();
        return OpenRequested(layer, request, RepositoryMenus.ForLocalBranch(context, branchRef, MenuActions), position, openedBy);
    }

    /// <summary>
    /// Selects <paramref name="sha"/>'s row for its menu (4b), alone unless it is part of a
    /// multi-selection. False when the graph does not have it.
    /// </summary>
    private bool SelectForMenu(string sha)
    {
        var index = Graph.IndexOf(sha);
        if (index < 0)
        {
            return false;
        }

        if (!(Selection.IsMultiple && Selection.Contains(sha)) && SelectedIndex != index)
        {
            SelectedIndex = index;
        }

        return true;
    }

    private bool ExistsInWorkingTree(string path)
    {
        var full = System.IO.Path.Combine(Path, path.TrimEnd('/'));
        return File.Exists(full) || Directory.Exists(full);
    }

    /// <summary>Lets a task run without waiting for it, logging a failure that got past its own handling.</summary>
    private void Start(Task task, string what) =>
        _ = task.ContinueWith(
            failed => _log.Error($"{Name}: {what} failed.", failed.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private static bool SamePath(string a, string b) => string.Equals(
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(a)),
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(b)),
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// The selected commits as the menus need them, in row order, with whether HEAD's history holds
    /// each (4h): <see cref="CommitGraphData.IsReachableFromHead"/>, the graph's walk over the
    /// parents from HEAD's commit through the rows loaded so far, cached per graph. A commit the
    /// walk has not reached counts as not on HEAD, so its item stays enabled and git decides (2.12).
    /// Each also gets its place on HEAD's linear first-parent line (phase 4 design 2.1,
    /// <see cref="CommitGraphData.DepthOnHeadLine"/>), likewise cached.
    /// </summary>
    private List<CommitMenuInfo> SelectedCommitsForMenus()
    {
        var graph = Graph;
        return [.. Selection.Shas
            .Select(sha => (Sha: sha, Row: graph.IndexOf(sha)))
            .Where(entry => entry.Row >= 0)
            .OrderBy(entry => entry.Row)
            .Select(entry =>
            {
                var commit = graph.CommitAt(entry.Row);
                var isCommit = commit.Kind != CommitKind.Stash;
                var reachable = isCommit && graph.IsReachableFromHead(commit.Sha);
                return new CommitMenuInfo(commit.Sha, commit.Subject, commit.Parents, commit.Kind, entry.Row, reachable)
                {
                    HeadLineDepth = isCommit ? graph.DepthOnHeadLine(commit.Sha) : null,
                };
            })];
    }

    /// <summary>
    /// The SHAs of the local and remote branch tips that HEAD reaches in the loaded graph (phase 4
    /// design 1.1), by <see cref="CommitGraphData.IsReachableFromHead"/>: a tip the walk has not
    /// reached counts as not on HEAD. Empty before the refs are read.
    /// </summary>
    private HashSet<string> BranchTipsOnHead()
    {
        var tips = new HashSet<string>(StringComparer.Ordinal);
        if (Refs is not { } refs)
        {
            return tips;
        }

        var graph = Graph;
        foreach (var gitRef in refs.Refs)
        {
            if (gitRef.Kind is RefKind.LocalBranch or RefKind.RemoteBranch
                && !tips.Contains(gitRef.TargetSha)
                && graph.IsReachableFromHead(gitRef.TargetSha))
            {
                tips.Add(gitRef.TargetSha);
            }
        }

        return tips;
    }
}
