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
public sealed record RefLabel(string Text, RefLabelKind Kind);

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

    public CommitGraphData(RepoRefs refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
        Refs = refs;
        _labelsBySha = BuildLabels(refs);
    }

    /// <summary>An empty graph: no refs, no commits, complete.</summary>
    public static CommitGraphData Empty { get; } = CreateEmpty();

    /// <summary>The refs the labels come from.</summary>
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

    private static CommitGraphData CreateEmpty()
    {
        var empty = new CommitGraphData(new RepoRefs(new HeadState(null, null), [], [], []));
        empty.IsComplete = true;
        return empty;
    }

    private static Dictionary<string, List<RefLabel>> BuildLabels(RepoRefs refs)
    {
        var labels = new Dictionary<string, List<RefLabel>>(StringComparer.Ordinal);

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
            var kind = gitRef.Kind switch
            {
                RefKind.LocalBranch when gitRef.IsHead => RefLabelKind.CurrentBranch,
                RefKind.LocalBranch => RefLabelKind.LocalBranch,
                RefKind.RemoteBranch => RefLabelKind.RemoteBranch,
                _ => RefLabelKind.Tag,
            };
            Add(gitRef.TargetSha, new RefLabel(gitRef.Name, kind));
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
