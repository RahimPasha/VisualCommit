using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;

namespace VisualCommit.App.ViewModels.Graph;

/// <summary>What a ref label in the graph's Branch / Tag column stands for. The order is the order labels are drawn in.</summary>
public enum RefLabelKind
{
    /// <summary>A detached HEAD: the label "HEAD".</summary>
    DetachedHead,

    /// <summary>The local branch HEAD is on: drawn with a check mark.</summary>
    CurrentBranch,

    LocalBranch,
    RemoteBranch,
    Tag,
    Stash,
}

/// <summary>One label in the Branch / Tag column.</summary>
/// <param name="Text">What the label says: a branch, remote branch or tag name, <c>stash@{0}</c>, or <c>HEAD</c>.</param>
/// <param name="Kind">What it stands for, which decides how it is drawn.</param>
/// <param name="RefName">
/// The full ref name of a branch, remote branch or tag (<c>refs/heads/main</c>), which tells a
/// local branch <c>origin/main</c> from the remote branch of that name; null for HEAD and a stash.
/// </param>
public sealed record RefLabel(string Text, RefLabelKind Kind, string? RefName = null);

/// <summary>
/// The commits of one repository with their graph rows and ref labels, as the graph control
/// draws them. It is filled page by page while the history loads (D47), always on the UI
/// thread, and only grows: row <c>i</c> never changes once it has been added. A reload builds a
/// new instance and swaps it in whole (D52).
/// </summary>
public sealed class CommitGraphData
{
    private readonly List<CommitInfo> _commits = [];
    private readonly List<GraphRow> _rows = [];
    private readonly Dictionary<string, int> _indexBySha = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RefLabel>> _labelsBySha;

    /// <param name="refs">The refs the graph was loaded with: the labels and the HEAD walks come from them.</param>
    /// <param name="filter">
    /// What the graph was loaded hiding or soloing (docs/phase-5-design.md 8.2), whose refs get no
    /// label; null when nothing is hidden or soloed.
    /// </param>
    public CommitGraphData(RepoRefs refs, GraphFilter? filter = null)
    {
        ArgumentNullException.ThrowIfNull(refs);
        Refs = refs;
        _labelsBySha = BuildLabels(refs, filter);
    }

    /// <summary>An empty graph: no refs, no commits, complete.</summary>
    public static CommitGraphData Empty { get; } = CreateEmpty();

    /// <summary>
    /// The refs the labels come from, all of them, also those a filter hides: the details panel
    /// and the HEAD walks use them.
    /// </summary>
    public RepoRefs Refs { get; }

    /// <summary>The number of rows loaded so far.</summary>
    public int Count => _commits.Count;

    /// <summary>The most lanes any loaded row uses: the graph column is sized for it.</summary>
    public int MaxLaneCount { get; private set; }

    /// <summary>All of the history has been loaded.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>Raised on the UI thread after rows were added or loading completed.</summary>
    public event EventHandler? Changed;

    public CommitInfo CommitAt(int index) => _commits[index];

    public GraphRow RowAt(int index) => _rows[index];

    /// <summary>The labels of the commit in row <paramref name="index"/>, in drawing order. Empty for most rows.</summary>
    public IReadOnlyList<RefLabel> LabelsAt(int index) =>
        _labelsBySha.TryGetValue(_commits[index].Sha, out var labels) ? labels : [];

    /// <summary>The row of a commit, or -1 when it is not (yet) loaded.</summary>
    public int IndexOf(string sha) => _indexBySha.TryGetValue(sha, out var index) ? index : -1;

    /// <summary>Adds a page of commits with their rows, laid out in the same order. Call on the UI thread.</summary>
    public void Append(IReadOnlyList<CommitInfo> commits, IReadOnlyList<GraphRow> rows)
    {
        ArgumentNullException.ThrowIfNull(commits);
        ArgumentNullException.ThrowIfNull(rows);
        if (commits.Count != rows.Count)
        {
            throw new ArgumentException("Every commit needs its row.", nameof(rows));
        }

        if (IsComplete)
        {
            throw new InvalidOperationException("The graph is complete; nothing can be added.");
        }

        for (var i = 0; i < commits.Count; i++)
        {
            _indexBySha.TryAdd(commits[i].Sha, _commits.Count);
            _commits.Add(commits[i]);
            _rows.Add(rows[i]);
            MaxLaneCount = Math.Max(MaxLaneCount, rows[i].LaneCount);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Marks the history as fully loaded. Call on the UI thread.</summary>
    public void Complete()
    {
        IsComplete = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Whether <paramref name="sha"/> is known to be reachable from HEAD's commit over the rows
    /// loaded so far (docs/phase-3-design.md 4h; 2.12 and 2.13 disable a pick of a commit that is
    /// already on the current branch). The walk over the parents runs when a menu first asks and
    /// is cached per graph; rows that arrive later extend it. False also while the history is
    /// still loading and the walk has parents left that are not loaded yet: then the menu item is
    /// enabled and git decides. False for every commit when HEAD is unborn. Call on the UI thread.
    /// </summary>
    public bool IsReachableFromHead(string sha)
    {
        ArgumentNullException.ThrowIfNull(sha);
        var walk = _reachable ??= new ReachabilityWalk(Refs.Head.Sha);
        if (walk.Contains(sha))
        {
            return true;
        }

        walk.Extend(this);
        return walk.Contains(sha);
    }

    private ReachabilityWalk? _reachable;

    /// <summary>
    /// HEAD's linear first-parent line over the rows loaded so far (docs/phase-4-design.md 2.1):
    /// HEAD's commit, its first parent, and so on until the first merge commit, which is
    /// included, or the root. "Interactive rebase of {cur} from here…" is offered for a commit on
    /// it, and a squash takes commits of it (section 3). The walk runs when it is first asked for
    /// and is cached per graph, as <see cref="IsReachableFromHead"/>'s is; rows that arrive later
    /// extend it. Empty when HEAD is unborn or its row is not loaded yet; on a complete graph, a
    /// parent that is never loaded (a shallow history) ends it. Call on the UI thread.
    /// </summary>
    /// <returns>The line's SHAs, HEAD's first.</returns>
    public IReadOnlyList<string> LinearLineFromHead()
    {
        var line = _headLine ??= new HeadLine(Refs.Head.Sha);
        line.Extend(this);
        return line.Shas;
    }

    /// <summary>
    /// Where <paramref name="sha"/> is on <see cref="LinearLineFromHead"/>: 0 for HEAD's commit,
    /// 1 for its first parent, …; null when it is not on the line as far as the rows are loaded.
    /// Call on the UI thread.
    /// </summary>
    public int? DepthOnHeadLine(string sha)
    {
        ArgumentNullException.ThrowIfNull(sha);
        LinearLineFromHead();
        return _headLine!.DepthOf(sha);
    }

    private HeadLine? _headLine;

    /// <summary>HEAD's linear first-parent line as far as it is loaded, and the commit it waits for.</summary>
    private sealed class HeadLine(string? head)
    {
        private readonly List<string> _shas = [];
        private readonly Dictionary<string, int> _depths = new(StringComparer.Ordinal);

        /// <summary>The next commit of the line, whose row is not loaded yet; null once the line has ended.</summary>
        private string? _next = head;

        public IReadOnlyList<string> Shas => _shas;

        public int? DepthOf(string sha) => _depths.TryGetValue(sha, out var depth) ? depth : null;

        /// <summary>Follows the first parents from where the last walk stopped, while their rows are loaded.</summary>
        public void Extend(CommitGraphData data)
        {
            while (_next is { } sha)
            {
                var index = data.IndexOf(sha);
                if (index < 0)
                {
                    // Not loaded yet; on a complete graph it never will be (a shallow history).
                    if (data.IsComplete)
                    {
                        _next = null;
                    }

                    return;
                }

                _depths[sha] = _shas.Count;
                _shas.Add(sha);

                // A merge commit ends the line, and so does the root.
                var parents = data.CommitAt(index).Parents;
                _next = parents.Count == 1 ? parents[0] : null;
            }
        }
    }

    /// <summary>The commits reached from HEAD so far, and the parents the walk waits for because their rows are not loaded yet.</summary>
    private sealed class ReachabilityWalk
    {
        private readonly HashSet<string> _reached = new(StringComparer.Ordinal);
        private List<string> _waiting = [];
        private int _seenCount = -1;

        public ReachabilityWalk(string? head)
        {
            if (head is not null)
            {
                _waiting.Add(head);
            }
        }

        public bool Contains(string sha) => _reached.Contains(sha);

        /// <summary>Follows every waiting parent whose row has been loaded since the last walk.</summary>
        public void Extend(CommitGraphData data)
        {
            if (data.Count == _seenCount || _waiting.Count == 0)
            {
                return;
            }

            _seenCount = data.Count;
            var stack = new Stack<string>(_waiting);
            var stillWaiting = new List<string>();
            while (stack.TryPop(out var sha))
            {
                if (_reached.Contains(sha))
                {
                    continue;
                }

                var index = data.IndexOf(sha);
                if (index < 0)
                {
                    // Not loaded yet; on a complete graph it never will be (a shallow history).
                    if (!data.IsComplete)
                    {
                        stillWaiting.Add(sha);
                    }

                    continue;
                }

                _reached.Add(sha);
                foreach (var parent in data.CommitAt(index).Parents)
                {
                    if (!_reached.Contains(parent))
                    {
                        stack.Push(parent);
                    }
                }
            }

            _waiting = stillWaiting;
        }
    }

    private static CommitGraphData CreateEmpty()
    {
        var empty = new CommitGraphData(new RepoRefs(new HeadState(null, null), [], [], []));
        empty.IsComplete = true;
        return empty;
    }

    /// <summary>
    /// The labels of every row, by commit: HEAD's when detached, each ref's, each stash's. Under a
    /// filter (docs/phase-5-design.md 8.2) a hidden branch has none, and in solo mode only the
    /// soloed branches have theirs; the checked-out branch always keeps its label, and tags keep
    /// theirs on the rows shown.
    /// </summary>
    private static Dictionary<string, List<RefLabel>> BuildLabels(RepoRefs refs, GraphFilter? filter)
    {
        var labels = new Dictionary<string, List<RefLabel>>(StringComparer.Ordinal);
        // The refs the filter names: those it solos in solo mode, else those it hides.
        var solo = filter is { IsSolo: true };
        var named = filter is null ? null : new HashSet<string>(solo ? filter.Soloed : filter.Hidden, StringComparer.Ordinal);
        bool HasLabel(GitRef gitRef) =>
            named is null || gitRef.Kind == RefKind.Tag || gitRef.IsHead || named.Contains(gitRef.FullName) == solo;

        void Add(string sha, RefLabel label)
        {
            if (!labels.TryGetValue(sha, out var list))
            {
                list = [];
                labels[sha] = list;
            }

            list.Add(label);
        }

        if (refs.Head.IsDetached)
        {
            Add(refs.Head.Sha!, new RefLabel("HEAD", RefLabelKind.DetachedHead));
        }

        foreach (var gitRef in refs.Refs)
        {
            if (!HasLabel(gitRef))
            {
                continue;
            }

            var kind = gitRef.Kind switch
            {
                RefKind.LocalBranch when gitRef.IsHead => RefLabelKind.CurrentBranch,
                RefKind.LocalBranch => RefLabelKind.LocalBranch,
                RefKind.RemoteBranch => RefLabelKind.RemoteBranch,
                _ => RefLabelKind.Tag,
            };
            Add(gitRef.TargetSha, new RefLabel(gitRef.Name, kind, gitRef.FullName));
        }

        foreach (var stash in refs.Stashes)
        {
            Add(stash.Sha, new RefLabel(stash.Name, RefLabelKind.Stash));
        }

        // Drawing order: by kind, then by name; refs come sorted by full name already.
        foreach (var list in labels.Values)
        {
            var sorted = list.OrderBy(label => label.Kind).ThenBy(label => label.Text, StringComparer.Ordinal).ToList();
            list.Clear();
            list.AddRange(sorted);
        }

        return labels;
    }
}
