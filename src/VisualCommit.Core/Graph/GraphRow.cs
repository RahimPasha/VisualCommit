namespace VisualCommit.Core.Graph;

/// <summary>How a line runs within one row of the graph.</summary>
public enum GraphLineKind
{
    /// <summary>
    /// Straight through the row in one lane, from the top edge to the bottom edge:
    /// <see cref="GraphLine.FromLane"/> equals <see cref="GraphLine.ToLane"/>. A lane that waits
    /// for a commit further down.
    /// </summary>
    PassThrough,

    /// <summary>
    /// From the top edge at <see cref="GraphLine.FromLane"/> to this row's node, in the middle of
    /// the row at <see cref="GraphLine.ToLane"/> (the node's lane): a lane that waited for this commit.
    /// </summary>
    IntoNode,

    /// <summary>
    /// From this row's node, at <see cref="GraphLine.FromLane"/> (the node's lane), to the bottom
    /// edge at <see cref="GraphLine.ToLane"/>: towards one of the commit's parents.
    /// </summary>
    OutOfNode,
}

/// <summary>One line drawn in one row of the graph.</summary>
/// <param name="Kind">How it runs.</param>
/// <param name="FromLane">The lane it starts in (see <see cref="GraphLineKind"/>).</param>
/// <param name="ToLane">The lane it ends in.</param>
/// <param name="Color">The colour number of the lane the line belongs to. Draw it with palette entry <c>Color % palette size</c>.</param>
public readonly record struct GraphLine(GraphLineKind Kind, int FromLane, int ToLane, int Color);

/// <summary>What the graph column draws in the row of one commit: its node and the lines around it.</summary>
public sealed class GraphRow
{
    public GraphRow(int nodeLane, int nodeColor, IReadOnlyList<GraphLine> lines)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nodeLane);
        ArgumentNullException.ThrowIfNull(lines);
        NodeLane = nodeLane;
        NodeColor = nodeColor;
        Lines = lines;

        var highest = nodeLane;
        foreach (var line in lines)
        {
            highest = Math.Max(highest, Math.Max(line.FromLane, line.ToLane));
        }

        LaneCount = highest + 1;
    }

    /// <summary>The lane of the commit's node, counted from 0 at the left.</summary>
    public int NodeLane { get; }

    /// <summary>The colour number of the node: the colour of its lane.</summary>
    public int NodeColor { get; }

    /// <summary>Every line in the row: lanes passing through, lanes ending at the node, and lines to the parents.</summary>
    public IReadOnlyList<GraphLine> Lines { get; }

    /// <summary>The number of lanes the row uses: one more than the highest lane of its node or lines.</summary>
    public int LaneCount { get; }
}
