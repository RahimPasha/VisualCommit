using VisualCommit.Core.Git;

namespace VisualCommit.Core.Graph;

/// <summary>
/// Lays out the commit graph one commit at a time, in the order <c>git log --date-order</c>
/// prints them, so that the graph can draw the first page while the rest still loads (D48).
/// </summary>
/// <remarks>
/// <para>
/// The layout keeps a list of lanes. A lane is free, or waits for one commit: a parent of a
/// commit above that has not been laid out yet. Each lane keeps the colour number it was given
/// when it started; numbers are handed out 0, 1, 2, ... in the order lanes start.
/// </para>
/// <para>For each commit:</para>
/// <list type="number">
/// <item>Its node goes in the leftmost lane that waits for it, in that lane's colour. If no lane
/// waits for it (a branch tip or a stash), it starts a new lane: the leftmost free one, or a new
/// one at the right end, with the next colour.</item>
/// <item>Every lane that waited for it ends in the node, so lines from several children meet
/// there; every other lane passes straight through the row.</item>
/// <item>The first parent continues in the node's lane and colour, even when another lane already
/// waits for it: the two lines meet at the parent. Each further parent joins the leftmost lane
/// that already waits for it, or else starts a new lane, the leftmost free one or a new one at
/// the right end, with the next colour. A parent listed twice is handled once. A root commit
/// leaves its lane free.</item>
/// <item>Free lanes at the right end are dropped, so the lane list stays as wide as the graph is
/// at that row.</item>
/// </list>
/// <para>
/// The lines of a row come in this order: first the lines of its top half, lane by lane from the
/// left (lanes passing through and lanes ending in the node), then the lines to the parents,
/// first parent first.
/// </para>
/// <para>Not thread-safe: one loader feeds it, commit by commit.</para>
/// </remarks>
public sealed class GraphLayout
{
    private readonly List<Lane> _lanes = [];

    /// <summary>
    /// For each commit that some lane waits for, the leftmost such lane, so that finding a commit's
    /// lane does not scan the lanes. Lanes stop waiting for a commit only when that commit is laid
    /// out, and then all at once, so an entry is only ever lowered or removed whole.
    /// </summary>
    private readonly Dictionary<string, int> _leftmostLaneWaitingFor = new(StringComparer.Ordinal);

    /// <summary>The lines of the row being laid out; copied into an array of the exact size for the row.</summary>
    private readonly List<GraphLine> _lines = [];

    private int _nextColor;

    /// <summary>How many commits have been laid out.</summary>
    public int RowCount { get; private set; }

    /// <summary>The most lanes any row so far has used: the width the graph column needs.</summary>
    public int MaxLaneCount { get; private set; }

    /// <summary>
    /// Lays out the next commit. Commits must come in <c>git log --date-order</c> order: every
    /// commit after all its children. A parent that never comes (a shallow clone) leaves its lane
    /// running to the bottom of the graph.
    /// </summary>
    public GraphRow Add(CommitInfo commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        _lines.Clear();
        int nodeLane;
        int nodeColor;

        if (_leftmostLaneWaitingFor.Remove(commit.Sha, out var waitingLane))
        {
            nodeLane = waitingLane;
            nodeColor = _lanes[nodeLane].Color;
            DrawTopHalf(commit.Sha, nodeLane);
        }
        else
        {
            nodeLane = TakeFreeLane();
            nodeColor = _nextColor++;
            DrawTopHalf(waitedFor: null, nodeLane);
        }

        DrawParents(commit.Parents, nodeLane, nodeColor);

        while (_lanes.Count > 0 && _lanes[^1].IsFree)
        {
            _lanes.RemoveAt(_lanes.Count - 1);
        }

        var row = new GraphRow(nodeLane, nodeColor, _lines.ToArray());
        RowCount++;
        MaxLaneCount = Math.Max(MaxLaneCount, row.LaneCount);
        return row;
    }

    /// <summary>
    /// Adds a line for every lane that is taken above the node, and frees the lanes that end in it.
    /// Only lanes from <paramref name="nodeLane"/> rightwards can wait for the commit, because the
    /// node is in the leftmost of them.
    /// </summary>
    /// <param name="waitedFor">The commit's id when lanes wait for it; null when none does.</param>
    /// <param name="nodeLane">The lane of the commit's node.</param>
    private void DrawTopHalf(string? waitedFor, int nodeLane)
    {
        for (var lane = 0; lane < _lanes.Count; lane++)
        {
            var state = _lanes[lane];
            if (state.IsFree)
            {
                continue;
            }

            var endsInNode = waitedFor is not null
                && lane >= nodeLane
                && string.Equals(state.WaitsFor, waitedFor, StringComparison.Ordinal);
            if (endsInNode)
            {
                _lines.Add(new GraphLine(GraphLineKind.IntoNode, lane, nodeLane, state.Color));
                _lanes[lane] = default;
            }
            else
            {
                _lines.Add(new GraphLine(GraphLineKind.PassThrough, lane, lane, state.Color));
            }
        }
    }

    /// <summary>Points a lane at each distinct parent and adds a line from the node to it.</summary>
    private void DrawParents(IReadOnlyList<string> parents, int nodeLane, int nodeColor)
    {
        for (var index = 0; index < parents.Count; index++)
        {
            var parent = parents[index];
            if (IsListedEarlier(parents, index))
            {
                continue;
            }

            if (index == 0)
            {
                // The first parent continues the node's lane, even if another lane already waits
                // for it: the two lines then meet at the parent.
                _lanes[nodeLane] = new Lane(parent, nodeColor);
                _leftmostLaneWaitingFor[parent] = _leftmostLaneWaitingFor.TryGetValue(parent, out var other)
                    ? Math.Min(other, nodeLane)
                    : nodeLane;
                _lines.Add(new GraphLine(GraphLineKind.OutOfNode, nodeLane, nodeLane, nodeColor));
            }
            else if (_leftmostLaneWaitingFor.TryGetValue(parent, out var joined))
            {
                _lines.Add(new GraphLine(GraphLineKind.OutOfNode, nodeLane, joined, _lanes[joined].Color));
            }
            else
            {
                // The first parent has taken the node's lane by now, so a free lane is another one.
                var lane = TakeFreeLane();
                var color = _nextColor++;
                _lanes[lane] = new Lane(parent, color);
                _leftmostLaneWaitingFor[parent] = lane;
                _lines.Add(new GraphLine(GraphLineKind.OutOfNode, nodeLane, lane, color));
            }
        }
    }

    /// <summary>The leftmost free lane, or a new one at the right end. It stays free until a commit is put in it.</summary>
    private int TakeFreeLane()
    {
        for (var lane = 0; lane < _lanes.Count; lane++)
        {
            if (_lanes[lane].IsFree)
            {
                return lane;
            }
        }

        _lanes.Add(default);
        return _lanes.Count - 1;
    }

    /// <summary>Whether the parent at <paramref name="index"/> appeared earlier in the list. Commits have few parents, so a scan is cheapest.</summary>
    private static bool IsListedEarlier(IReadOnlyList<string> parents, int index)
    {
        for (var earlier = 0; earlier < index; earlier++)
        {
            if (string.Equals(parents[earlier], parents[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One lane: free when <see cref="WaitsFor"/> is null (the default value).</summary>
    /// <param name="WaitsFor">The id of the commit the lane leads down to.</param>
    /// <param name="Color">The colour number the lane was given when it started.</param>
    private readonly record struct Lane(string? WaitsFor, int Color)
    {
        public bool IsFree => WaitsFor is null;
    }
}
