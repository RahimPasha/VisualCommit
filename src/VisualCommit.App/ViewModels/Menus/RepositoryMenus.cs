// The menu builders: docs/phase-3-design.md 4a (the model and its rules), 4b (the menu of every
// object, its items, labels, order, enabled state and disabled reasons), 4g (the drop's menu),
// 4h (several commits), D77 and D92 (what is left out), D114 (separators, a local upstream's
// name, a gone upstream, "Check out <r>/<b>" while on <b>). WP7-core left the API: one pure
// builder per object, each returning an empty menu, and the shared helpers Build and Item, which
// are real. The sub-package WP7-menus fills every builder and keeps the signatures; its tests
// are tests/VisualCommit.App.Tests/RepositoryMenusTests.cs (pure: a RepositoryMenuContext made
// in the test, and RecordingActions to see what an item starts).

using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.ViewModels.Menus;

/// <summary>
/// Builds the context menu of every object of a repository (docs/phase-3-design.md 4b) as data
/// (<see cref="MenuModel"/>, D86): pure functions of a <see cref="RepositoryMenuContext"/>, whose
/// items start <see cref="IRepositoryActions"/> on the objects the menu captured when it opened
/// (full ref names and SHAs, never rows). Items for later phases are left out, not disabled
/// (D77, D92). Every menu starts with its header; groups are separated by one separator, and a
/// group whose items are all left out leaves none (<see cref="Build"/>, D114).
/// </summary>
public static class RepositoryMenus
{
    /// <summary>
    /// One selected commit, right-clicked on its row outside its labels, or its <c>HEAD</c> or "+N"
    /// label (4b): header <c>a1b2c3d Add core</c>; check out, create branch, create tag — cherry-pick,
    /// revert, reset — copy SHA. <paramref name="sha"/> is in <see cref="RepositoryMenuContext.SelectedCommits"/>.
    /// A stash row has the stash's menu (<see cref="ForStash"/>).
    /// </summary>
    public static MenuModel ForCommit(RepositoryMenuContext context, string sha, IRepositoryActions actions) => Empty;

    /// <summary>
    /// Two or more selected commits (4b, 4h): header <c>3 commits</c>; cherry-pick N commits, revert N
    /// commits — copy SHAs, on <see cref="RepositoryMenuContext.SelectedCommits"/>.
    /// </summary>
    public static MenuModel ForCommits(RepositoryMenuContext context, IRepositoryActions actions) => Empty;

    /// <summary>
    /// A stash: its graph row, its left-panel row or its <c>stash@{n}</c> label (4b): header
    /// <c>stash@{0} On main: Experiment</c>; apply, pop, drop… — copy SHA. <paramref name="stashSha"/> is the stash's commit.
    /// </summary>
    public static MenuModel ForStash(RepositoryMenuContext context, string stashSha, IRepositoryActions actions) => Empty;

    /// <summary>
    /// The working-changes row (4b): header "Working changes"; stage all, unstage all, discard all
    /// changes… — stash changes…, create branch here… — show in folder.
    /// </summary>
    public static MenuModel ForWorkingChanges(RepositoryMenuContext context, IRepositoryActions actions) => Empty;

    /// <summary>
    /// A local branch, its left-panel row or its graph label (4b): header <c>&lt;b&gt;</c>; check out,
    /// merge (both), pull or fast-forward — push, force push… — create branch here…, create tag
    /// here…, rename…, delete… — set or change upstream…, stop tracking — copy branch name.
    /// <paramref name="branchRef"/> is <c>refs/heads/&lt;b&gt;</c>.
    /// </summary>
    public static MenuModel ForLocalBranch(RepositoryMenuContext context, string branchRef, IRepositoryActions actions) => Empty;

    /// <summary>
    /// A remote branch, its row or its label (4b): header <c>&lt;r&gt;/&lt;b&gt;</c>; check out,
    /// merge (both) — create branch here…, create tag here… — delete on the remote… — copy branch
    /// name. <paramref name="remoteBranchRef"/> is <c>refs/remotes/&lt;r&gt;/&lt;b&gt;</c>.
    /// </summary>
    public static MenuModel ForRemoteBranch(RepositoryMenuContext context, string remoteBranchRef, IRepositoryActions actions) => Empty;

    /// <summary>
    /// A remote's row in the left panel (4b): header <c>&lt;r&gt;</c>; fetch — change URL…, rename…,
    /// remove… — copy URL, manage remotes….
    /// </summary>
    public static MenuModel ForRemote(RepositoryMenuContext context, string remote, IRepositoryActions actions) => Empty;

    /// <summary>
    /// A tag, its row or its label (4b): header <c>&lt;t&gt;</c>; check out, create branch here… —
    /// push to a remote, delete…, delete on a remote… — copy tag name. <paramref name="tagRef"/> is <c>refs/tags/&lt;t&gt;</c>.
    /// </summary>
    public static MenuModel ForTag(RepositoryMenuContext context, string tagRef, IRepositoryActions actions) => Empty;

    /// <summary>
    /// A left-panel section header (4b), headed by the section's title: "Local branches" create
    /// branch…; "Remotes" add remote…, fetch all remotes, manage remotes…; "Tags" create tag at
    /// HEAD…; "Stashes" stash changes…. Null for "Pull requests", which has no menu (D92).
    /// </summary>
    public static MenuModel? ForSection(RepositoryMenuContext context, RefSection section, IRepositoryActions actions) => null;

    /// <summary>
    /// A file in the stage panel's unstaged list (4b): header the file name; stage (or mark as
    /// resolved), discard changes… — ignore the file, ignore its extension — open file, show in
    /// folder, copy path.
    /// </summary>
    public static MenuModel ForUnstagedFile(RepositoryMenuContext context, FileMenuTarget file, IRepositoryActions actions) => Empty;

    /// <summary>A file in the staged list (4b): header the file name; unstage — open file, show in folder, copy path.</summary>
    public static MenuModel ForStagedFile(RepositoryMenuContext context, FileMenuTarget file, IRepositoryActions actions) => Empty;

    /// <summary>A file in a commit's changed files (4b): header the file name; open file, show in folder, copy path.</summary>
    public static MenuModel ForCommitFile(RepositoryMenuContext context, FileMenuTarget file, IRepositoryActions actions) => Empty;

    /// <summary>
    /// A right-click on the diff view's text or header (4b): header the file name; the selected
    /// lines' and the hunk's actions (<paramref name="lines"/>), the file's own, the two ignore
    /// items for an untracked file, then open file, show in folder, copy path. A commit's diff has
    /// the last three only.
    /// </summary>
    public static MenuModel ForDiff(RepositoryMenuContext context, DiffMenuTarget diff, IRepositoryActions actions, IDiffMenuActions lines) => Empty;

    /// <summary>
    /// The empty graph area, below the last row or a graph without commits (4b): header the
    /// repository's name; fetch all remotes, pull, push — create branch…, stash changes… — manage
    /// remotes…, enabled as the toolbar's buttons (<see cref="RepositoryMenuContext.Toolbar"/>).
    /// </summary>
    public static MenuModel ForEmptyGraphArea(RepositoryMenuContext context, IRepositoryActions actions) => Empty;

    /// <summary>
    /// The menu of a drop of the branch <paramref name="sourceRef"/> onto <paramref name="targetRef"/>
    /// (4g): header <c>&lt;A&gt; → &lt;B&gt;</c>; onto the current branch merge (both) and
    /// fast-forward; onto another local branch fast-forward and check out and merge; onto a remote
    /// branch push and push and track. <see cref="RepositoryMenuContext.Drop"/> holds what the drop read.
    /// </summary>
    public static MenuModel ForDrop(RepositoryMenuContext context, string sourceRef, string targetRef, IRepositoryActions actions) => Empty;

    /// <summary>
    /// A menu from its header and groups of items (4a, D114): the header first; the items of each
    /// group in order, null items (left out) dropped; one separator between two groups that keep
    /// items, so that no separator follows the header, ends the menu, or follows another.
    /// </summary>
    public static MenuModel Build(MenuHeader header, params IEnumerable<MenuItem?>[] groups)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(groups);
        var entries = new List<MenuEntry> { header };
        foreach (var group in groups)
        {
            var items = group.OfType<MenuItem>().ToList();
            if (items.Count == 0)
            {
                continue;
            }

            if (entries.Count > 1)
            {
                entries.Add(new MenuSeparator());
            }

            entries.AddRange(items);
        }

        return new MenuModel(entries);
    }

    /// <summary>
    /// An item that starts <paramref name="action"/> when invoked: enabled when
    /// <paramref name="disabledReason"/> is null, otherwise disabled with that reason as its
    /// tooltip. The action's task is not awaited (the menu has closed); a failure that gets past
    /// the operation's own handling is logged to <see cref="IRepositoryActions.Log"/>.
    /// </summary>
    public static MenuItem Item(IRepositoryActions actions, string label, Func<Task> action, string? disabledReason = null, bool isDestructive = false)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(action);
        return new MenuItem(label, () => Start(actions, label, action), disabledReason is null, disabledReason, isDestructive);
    }

    private static MenuModel Empty => new([]);

    private static void Start(IRepositoryActions actions, string label, Func<Task> action)
    {
        var log = actions.Log ?? NullAppLog.Instance;
        Task task;
        try
        {
            task = action();
        }
        catch (Exception ex)
        {
            log.Error($"The menu item \"{label}\" failed.", ex);
            return;
        }

        _ = task.ContinueWith(
            failed => log.Error($"The menu item \"{label}\" failed.", failed.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
