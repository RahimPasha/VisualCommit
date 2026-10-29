using System.Runtime.CompilerServices;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Session;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels.Menus;

/// <summary>Whether a toolbar button can be used now, and why not (4d): the empty graph area's items are enabled as the toolbar's buttons.</summary>
/// <param name="IsEnabled">The button is enabled.</param>
/// <param name="Reason">Its tooltip while disabled; null while enabled, or before the refs are read.</param>
public readonly record struct ToolbarAvailability(bool IsEnabled, string? Reason);

/// <summary>The toolbar's buttons as they were when the menu opened (<see cref="ToolbarViewModel"/>, 4d).</summary>
public sealed record ToolbarMenuState(
    ToolbarAvailability Fetch,
    ToolbarAvailability Pull,
    ToolbarAvailability Push,
    ToolbarAvailability Branch,
    ToolbarAvailability Stash,
    ToolbarAvailability Pop)
{
    /// <summary>Every button disabled without a reason: a repository whose refs are not read yet.</summary>
    public static ToolbarMenuState Disabled { get; } = new(default, default, default, default, default, default);

    /// <summary>The state of <paramref name="toolbar"/>'s buttons now, with their disabled reasons, never their tooltips (docs/phase-6-design.md 6.8).</summary>
    public static ToolbarMenuState Of(ToolbarViewModel toolbar)
    {
        ArgumentNullException.ThrowIfNull(toolbar);
        return new(
            new(toolbar.CanFetch, toolbar.FetchDisabledReason),
            new(toolbar.CanPull, toolbar.PullDisabledReason),
            new(toolbar.CanPush, toolbar.PushDisabledReason),
            new(toolbar.CanBranch, toolbar.BranchDisabledReason),
            new(toolbar.CanStash, toolbar.StashDisabledReason),
            new(toolbar.CanPop, toolbar.PopDisabledReason));
    }
}

/// <summary>One selected commit as the commit menus need it (4b, 4h).</summary>
/// <param name="Sha">Its full id (a stash's commit for a stash row).</param>
/// <param name="Subject">Its subject; a stash's message.</param>
/// <param name="Parents">Its parents, first parent first.</param>
/// <param name="Kind">A commit or a stash row.</param>
/// <param name="Row">Its row in the graph when the menu opened (rows are only an order here, never what a menu acts on).</param>
/// <param name="IsReachableFromHead">
/// HEAD's history holds it (4h): "This commit is already on main" (2.12, 2.13). False while the
/// history is loading and the walk from HEAD has not reached it: the item stays enabled and git decides.
/// </param>
public sealed record CommitMenuInfo(string Sha, string Subject, IReadOnlyList<string> Parents, CommitKind Kind, int Row, bool IsReachableFromHead)
{
    /// <summary>
    /// Its distance from HEAD on HEAD's linear first-parent line (docs/phase-4-design.md 2.1,
    /// <see cref="Graph.CommitGraphData.LinearLineFromHead"/>): 0 for HEAD's own commit, 1 for its
    /// first parent, …, up to the first merge commit or the root; null when it is not on the
    /// line, or the graph has not reached it yet. "Interactive rebase of {cur} from here…" and
    /// "Squash {N} commits…" read it.
    /// </summary>
    public int? HeadLineDepth { get; init; }

    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;

    /// <summary>A merge commit: two parents or more.</summary>
    public bool IsMerge => Parents.Count > 1;

    /// <summary>A stash row of the graph.</summary>
    public bool IsStash => Kind == CommitKind.Stash;
}

/// <summary>What a drop of a branch onto a branch found when it read the repository (4g), before its menu is built.</summary>
/// <param name="TargetIsAncestorOfSource">
/// <c>git merge-base --is-ancestor &lt;B&gt; &lt;A&gt;</c> succeeded: B can be fast-forwarded to A
/// (2.7, 2.10); otherwise "&lt;B&gt; has commits that &lt;A&gt; does not have". How a remote
/// branch as the target is fetched is read from the refs' snapshot, not here
/// (<see cref="RepoRefs.SourceOf(GitRef)"/>, docs/phase-6-design.md 10.7).
/// </param>
public sealed record DropFacts(bool TargetIsAncestorOfSource)
{
    /// <summary>
    /// The source A already contains the target B (<c>git merge-base --is-ancestor &lt;B&gt;
    /// &lt;A&gt;</c>, for a local or remote B; docs/phase-4-design.md 1.1): the drop's rebase
    /// items say "{A} is up to date with {B}". Null when it was not read or the read failed: the
    /// items stay enabled and git decides.
    /// </summary>
    public bool? SourceContainsTarget { get; init; }
}

/// <summary>
/// Everything the menu builders (<see cref="RepositoryMenus"/>) read, as one immutable snapshot
/// taken when a menu opens (docs/phase-3-design.md 4a, 4b, 4g, 4h): the refs (HEAD, branches with
/// their upstreams, remote branches, tags, stashes, remotes), the working tree's status with what
/// is in progress, the selected commits, the other worktrees, the toolbar's states and whether an
/// operation runs. The builders are pure functions of it, so they are tested without a UI or git.
/// <see cref="RepositoryViewModel.MenuContext"/> builds it from the repository's view model.
/// </summary>
/// <param name="RepositoryName">The repository's name: the empty graph area's header.</param>
/// <param name="WorkingDirectory">The working tree's folder.</param>
/// <param name="Refs">The refs as last read.</param>
/// <param name="Status">The working tree's status as last read, with <see cref="WorkingTreeStatus.InProgress"/>.</param>
/// <param name="SelectedCommits">The commits selected in the graph, in row order (top first); stash rows included.</param>
/// <param name="Toolbar">The toolbar's buttons now.</param>
/// <param name="IsOperationRunning">An operation of this repository runs (an auto-fetch included).</param>
public sealed record RepositoryMenuContext(
    string RepositoryName,
    string WorkingDirectory,
    RepoRefs Refs,
    WorkingTreeStatus Status,
    IReadOnlyList<CommitMenuInfo> SelectedCommits,
    ToolbarMenuState Toolbar,
    bool IsOperationRunning)
{
    private const string HeadsPrefix = "refs/heads/";

    private static readonly IReadOnlySet<string> NoTips = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// The repository's other worktrees (linked or main, not this one) and the branch each has
    /// checked out: "&lt;b&gt; is checked out in another worktree (&lt;path&gt;)" (2.7). Empty when
    /// they were not read (<see cref="RepositoryViewModel.ReadMenuContextAsync"/> reads them).
    /// </summary>
    public IReadOnlyList<WorktreeInfo> OtherWorktrees { get; init; } = [];

    /// <summary>What a drop read (4g), for <see cref="RepositoryMenus.ForDrop"/>; null otherwise.</summary>
    public DropFacts? Drop { get; init; }

    /// <summary>
    /// The SHAs of the local and remote branch tips that HEAD reaches in the loaded graph
    /// (docs/phase-4-design.md 1.1): a rebase onto such a branch is disabled with "{b} is up to
    /// date with {target}". A tip the graph has not reached yet counts as not on HEAD, and git
    /// decides. Empty while detached and unborn, or when not computed.
    /// </summary>
    public IReadOnlySet<string> TipsOnHead { get; init; } = NoTips;

    /// <summary>
    /// The merge tool git's configuration names (<c>merge.tool</c>, 11.9), read whenever the
    /// status shows a conflict; null when none is set or it was not read. Without one, "Open in
    /// merge tool" is disabled.
    /// </summary>
    public string? MergeTool { get; init; }

    /// <summary>
    /// What the graph hides and solos (docs/phase-5-design.md 8.1, 8.3): the hidden and soloed
    /// branches, the hidden remotes, by full names, for the labels and disabled reasons of Hide or
    /// Show, Solo or Stop soloing, and the sections' and remotes' items. Null while nothing is
    /// hidden or soloed.
    /// </summary>
    public RepositoryGraphFilter? Filters { get; init; }

    /// <summary>
    /// An editor is chosen (docs/phase-6-design.md 12.1): the file menus have "Open in editor"
    /// after "Open file", whichever editor it is (its label is fixed); false while none is chosen.
    /// Nothing touches the disk for it.
    /// </summary>
    public bool HasEditor { get; init; }

    /// <summary>
    /// The repository's hosting (docs/phase-7a-design.md 1.7, 10): its pull requests and which
    /// branch has one, for the pull-request items of the menus; null for a repository without a
    /// GitHub remote (<see cref="PullRequestsState.NotGitHub"/>).
    /// </summary>
    public RepositoryHostingState? Hosting { get; init; }

    public HeadState Head => Refs.Head;

    public bool IsDetached => Head.IsDetached;

    public bool IsUnborn => Head.IsUnborn;

    /// <summary>What is in progress (section 5).</summary>
    public InProgressState InProgress => Status.InProgress;

    /// <summary>The local branch HEAD is on, or null.</summary>
    public GitRef? CurrentBranch => Head.BranchName is { } branch ? FindRef(HeadsPrefix + branch) : null;

    /// <summary>How the menus name what HEAD is on: the current branch, or "HEAD" while detached (4b).</summary>
    public string CurrentName => Head.BranchName ?? "HEAD";

    public bool HasRemotes => Refs.Remotes.Count > 0;

    public IReadOnlyList<string> Remotes => Refs.Remotes;

    public IEnumerable<GitRef> LocalBranches => Refs.Refs.Where(gitRef => gitRef.Kind == RefKind.LocalBranch);

    public IEnumerable<GitRef> RemoteBranches => Refs.Refs.Where(gitRef => gitRef.Kind == RefKind.RemoteBranch);

    public IEnumerable<GitRef> Tags => Refs.Refs.Where(gitRef => gitRef.Kind == RefKind.Tag);

    /// <summary>Two or more commits are selected: their menu is the one for several commits (4h).</summary>
    public bool HasMultiSelection => SelectedCommits.Count > 1;

    /// <summary>
    /// The branch, remote branch or tag with this full name, or null: the first of that name in
    /// <see cref="RepoRefs.Refs"/>, found in an index by full name (docs/phase-6-design.md 5.5),
    /// built at the first call and kept with the list it was built from, so that a copy whose
    /// <see cref="Refs"/> holds another list builds its own.
    /// </summary>
    public GitRef? FindRef(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        return RefIndexes.GetValue(Refs.Refs, BuildRefIndex).GetValueOrDefault(fullName);
    }

    /// <summary>The stash whose commit is <paramref name="sha"/>, or null.</summary>
    public StashEntry? FindStash(string sha) =>
        Refs.Stashes.FirstOrDefault(stash => string.Equals(stash.Sha, sha, StringComparison.Ordinal));

    /// <summary>The selected commit <paramref name="sha"/>, or null when it is not selected.</summary>
    public CommitMenuInfo? FindSelected(string sha) =>
        SelectedCommits.FirstOrDefault(commit => string.Equals(commit.Sha, sha, StringComparison.Ordinal));

    /// <summary>How the menus name a branch's upstream: <c>origin/main</c>, or a local upstream's branch alone (D114); null without one.</summary>
    public static string? UpstreamText(GitRef branch) => ToolbarViewModel.UpstreamText(branch);

    // The local branch a remote branch checks out into is RepoRefs.LocalNameOf (docs/phase-6-design.md 10.6).

    /// <summary>Each refs list's index by full name, the first ref of each name (<see cref="FindRef"/>); kept as long as its list lives.</summary>
    private static readonly ConditionalWeakTable<IReadOnlyList<GitRef>, Dictionary<string, GitRef>> RefIndexes = new();

    private static Dictionary<string, GitRef> BuildRefIndex(IReadOnlyList<GitRef> refs)
    {
        var index = new Dictionary<string, GitRef>(refs.Count, StringComparer.Ordinal);
        foreach (var gitRef in refs)
        {
            // The first of a name wins, as FirstOrDefault found it.
            index.TryAdd(gitRef.FullName, gitRef);
        }

        return index;
    }

    /// <summary>The other worktree where the local branch <paramref name="branchRef"/> is checked out, or null (2.7).</summary>
    public string? OtherWorktreeOf(string branchRef) =>
        OtherWorktrees.FirstOrDefault(worktree => string.Equals(worktree.Branch, branchRef, StringComparison.Ordinal))?.Path;
}

/// <summary>Where a file's menu was asked for (4b): the stage panel's two lists, the diff view, a commit's changed files.</summary>
public enum FileMenuPlace
{
    Unstaged,
    Staged,
    Diff,
    CommitFiles,
}

/// <summary>A file a menu acts on (4b): the file as its list shows it, and what the menu needs to know about it.</summary>
/// <param name="File">The file: its path (repository-relative, <c>/</c>), its kind, its old path.</param>
/// <param name="IsUntracked">Git does not track it: it can be ignored (2.30).</param>
/// <param name="ExistsInWorkingTree">It is in the working tree: "Open file" is enabled (2.32).</param>
/// <param name="CommitSha">For a commit's changed files, the commit; null otherwise.</param>
public sealed record FileMenuTarget(ChangedFile File, bool IsUntracked, bool ExistsInWorkingTree, string? CommitSha = null)
{
    public string Path => File.Path;

    /// <summary>The file part of the path ("Ignore run.log"); a folder's without its slash.</summary>
    public string FileName => WorkingChangesViewModel.FileName(Path.TrimEnd('/'));

    /// <summary>The extension without its dot ("log"), or null when the name has none (2.30): "Ignore all *.log files" is left out then.</summary>
    public string? Extension
    {
        get
        {
            var name = FileName;
            var dot = name.LastIndexOf('.');
            return dot > 0 && dot < name.Length - 1 ? name[(dot + 1)..] : null;
        }
    }

    /// <summary>A conflicted file: "Mark as resolved", and no discard (4b).</summary>
    public bool IsConflicted => File.Kind == FileChangeKind.Conflicted;

    /// <summary>An untracked folder git lists with a slash: a repository of its own, never discarded (D75).</summary>
    public bool IsNestedRepository => Path.EndsWith('/');

    /// <summary>
    /// The conflict resolver's header menu (docs/phase-5-design.md 4.4): it leaves out File history
    /// and Blame, since opening a page would close the resolver.
    /// </summary>
    public bool InConflictResolver { get; init; }

    /// <summary>
    /// For a commit's file, the commit's first parent (a stash's: the commit it was made on), or
    /// null: the blame of a file the commit deleted is its parent's version (docs/phase-5-design.md 4.4).
    /// </summary>
    public string? CommitParent { get; init; }
}

/// <summary>A right-click in the diff view (4b): which file and side, and what is selected or under the pointer.</summary>
/// <param name="File">The file.</param>
/// <param name="Side">The diff's side: unstaged, staged or a commit's.</param>
/// <param name="HasSelectedLines">Lines are selected: "Stage selected lines", "Discard selected lines…", "Unstage selected lines".</param>
/// <param name="Hunk">The hunk the right-click was in, or null outside every hunk.</param>
/// <param name="ActsOnLines">Lines and hunks can be acted on (not a type change, D76).</param>
public sealed record DiffMenuTarget(FileMenuTarget File, DiffSide Side, bool HasSelectedLines, int? Hunk, bool ActsOnLines);

/// <summary>
/// The diff view's own line and hunk actions (phase 2, D70, D76), which the diff's menu offers
/// besides the file's: the menus' sub-package adapts the open <c>DiffViewModel</c> to it.
/// </summary>
public interface IDiffMenuActions
{
    /// <summary>"Stage selected lines".</summary>
    Task StageLinesAsync();

    /// <summary>"Unstage selected lines".</summary>
    Task UnstageLinesAsync();

    /// <summary>"Discard selected lines…" (asks first).</summary>
    Task DiscardLinesAsync();

    /// <summary>"Stage hunk", "Unstage hunk", "Discard hunk…" on <paramref name="hunk"/>.</summary>
    Task RunHunkActionAsync(int hunk, Diff.LineAction action);
}
