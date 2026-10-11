// The repository's menus: docs/phase-3-design.md 4a (opening a menu by pointer and by keyboard),
// 4b (what a right-click on each object opens; a right-click on a graph row selects it first,
// one on a left-panel row selects nothing), 4c (the double-click map) and 4g (the drop's menu).
// WP7-core built the snapshot the builders read (MenuContext, ReadMenuContextAsync), IsClosed and
// the shared OpenMenu; the Open…Menu and DoubleTap… entry points are stubs that the sub-package
// WP7-menus fills (keeping their signatures), together with the views that call them.

using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels;

public sealed partial class RepositoryViewModel
{
    private const string MenusPackage = "WP7: menus";

    private IReadOnlyList<WorktreeInfo> _otherWorktrees = [];

    /// <summary>The tab was closed (<see cref="Dispose"/>): operations start nothing and show nothing (1.8).</summary>
    public bool IsClosed => _disposed;

    /// <summary>
    /// The snapshot the menu builders read (<see cref="RepositoryMenuContext"/>), taken now: the
    /// refs, the status, the selected commits in row order with whether HEAD's history holds them
    /// (4h), the toolbar's states, whether an operation runs, and the other worktrees as last read
    /// by <see cref="ReadMenuContextAsync"/> (empty before). Before the refs are read: an unborn
    /// HEAD without refs, every toolbar button disabled.
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

    // The entry points the views call (stubs: WP7-menus fills them). Each builds its menu from a
    // fresh context and opens it through OpenMenu, and returns whether a menu opened.

    /// <summary>
    /// A right-click (or Shift+F10, the Menu key) on a commit's row outside its labels (4b): the row
    /// is selected alone first unless it is part of a multi-selection, whose menu then opens; a
    /// stash row opens the stash's menu.
    /// </summary>
    public bool OpenCommitMenu(MenuLayerViewModel layer, string sha, MenuPosition position, MenuOpenedBy openedBy) =>
        throw new NotImplementedException(MenusPackage);

    /// <summary>
    /// A right-click on a graph label (4b): the menu of what it stands for (a local or remote
    /// branch, a tag, a stash; <c>HEAD</c> and the "+N" label, <paramref name="label"/> null, open
    /// the commit's menu). The row is selected too.
    /// </summary>
    public bool OpenLabelMenu(MenuLayerViewModel layer, string sha, RefLabel? label, MenuPosition position, MenuOpenedBy openedBy) =>
        throw new NotImplementedException(MenusPackage);

    /// <summary>A right-click on the working-changes row (4b); the row is selected.</summary>
    public bool OpenWorkingChangesMenu(MenuLayerViewModel layer, MenuPosition position, MenuOpenedBy openedBy) =>
        throw new NotImplementedException(MenusPackage);

    /// <summary>A right-click below the last row of the graph, or in a graph without commits (4b).</summary>
    public bool OpenEmptyGraphAreaMenu(MenuLayerViewModel layer, MenuPosition position, MenuOpenedBy openedBy) =>
        throw new NotImplementedException(MenusPackage);

    /// <summary>
    /// A right-click on a left-panel row (4b): a section header, a remote, a local or remote
    /// branch, a tag or a stash; a folder and "Pull requests" open nothing. Nothing is selected or
    /// highlighted, and the graph's selection does not change (D114).
    /// </summary>
    public bool OpenRefRowMenu(MenuLayerViewModel layer, RefListRow row, MenuPosition position, MenuOpenedBy openedBy) =>
        throw new NotImplementedException(MenusPackage);

    /// <summary>
    /// A right-click on a file of the stage panel's lists or of a commit's changed files (4b): its
    /// menu only; the list's selection and the open diff do not change.
    /// </summary>
    public bool OpenFileMenu(MenuLayerViewModel layer, ChangedFile file, FileMenuPlace place, MenuPosition position, MenuOpenedBy openedBy) =>
        throw new NotImplementedException(MenusPackage);

    /// <summary>A right-click on the diff view's text or header (4b); <paramref name="hunk"/> is the hunk under the pointer, or null.</summary>
    public bool OpenDiffMenu(MenuLayerViewModel layer, DiffViewModel diff, int? hunk, MenuPosition position, MenuOpenedBy openedBy) =>
        throw new NotImplementedException(MenusPackage);

    /// <summary>
    /// A drop of the branch <paramref name="sourceRef"/> onto <paramref name="targetRef"/> (4g): reads
    /// what the drop needs (<see cref="DropFacts"/>, the worktrees), then opens its menu at the
    /// release point. Completes with whether a menu opened.
    /// </summary>
    public Task<bool> OpenDropMenuAsync(MenuLayerViewModel layer, string sourceRef, string targetRef, MenuPosition position) =>
        Task.FromException<bool>(new NotImplementedException(MenusPackage));

    /// <summary>
    /// A double-click on a left-panel row (4c): a local branch checks out (nothing more on the
    /// current one), a remote branch checks out into a tracking branch, a stash applies; any other
    /// row does nothing more than its click.
    /// </summary>
    public void DoubleTapRefRow(RefListRow row) => throw new NotImplementedException(MenusPackage);

    /// <summary>
    /// A double-click on a graph label (4c): a local branch checks out (nothing more on the current
    /// one), a remote branch checks out into a tracking branch; a tag, a stash or <c>HEAD</c> does
    /// nothing more than the click (D93).
    /// </summary>
    public void DoubleTapLabel(RefLabel label) => throw new NotImplementedException(MenusPackage);

    private static bool SamePath(string a, string b) => string.Equals(
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(a)),
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(b)),
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// The selected commits as the menus need them, in row order, with whether HEAD's history holds
    /// each (4h): a walk over the parents from HEAD's commit through the rows loaded so far, which
    /// stops once every selected commit is found. A commit not reached counts as not on HEAD, so
    /// its item stays enabled and git decides (2.12).
    /// </summary>
    private List<CommitMenuInfo> SelectedCommitsForMenus()
    {
        var graph = Graph;
        var rows = Selection.Shas
            .Select(sha => (Sha: sha, Row: graph.IndexOf(sha)))
            .Where(entry => entry.Row >= 0)
            .OrderBy(entry => entry.Row)
            .ToList();
        if (rows.Count == 0)
        {
            return [];
        }

        var reachable = ReachableFromHead(graph, rows.Select(entry => entry.Sha).ToHashSet(StringComparer.Ordinal));
        return [.. rows.Select(entry =>
        {
            var commit = graph.CommitAt(entry.Row);
            return new CommitMenuInfo(commit.Sha, commit.Subject, commit.Parents, commit.Kind, entry.Row, reachable.Contains(commit.Sha));
        })];
    }

    /// <summary>Which of <paramref name="wanted"/> HEAD's history holds, by a walk over the loaded rows' parents.</summary>
    private HashSet<string> ReachableFromHead(CommitGraphData graph, HashSet<string> wanted)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        if (Refs?.Head.Sha is not { } head)
        {
            return found;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal) { head };
        var pending = new Stack<string>();
        pending.Push(head);
        while (pending.Count > 0 && found.Count < wanted.Count)
        {
            var sha = pending.Pop();
            if (wanted.Contains(sha))
            {
                found.Add(sha);
            }

            var index = graph.IndexOf(sha);
            if (index < 0)
            {
                // Not loaded yet: its history is not known here.
                continue;
            }

            foreach (var parent in graph.CommitAt(index).Parents)
            {
                if (seen.Add(parent))
                {
                    pending.Push(parent);
                }
            }
        }

        return found;
    }
}
