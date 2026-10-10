using System.Diagnostics;
using System.Globalization;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The lane layout of the commit graph (D48), on made-up histories. A history is written top to
/// bottom as the graph shows it, one commit per line: <c>"M: P L"</c> is commit M with first
/// parent P and second parent L; <c>"A:"</c> is a root commit.
/// </summary>
public class GraphLayoutTests
{
    /// <summary>
    /// The history of the phase 1 graph scenario, in <c>--date-order</c>: a stash S on T; the tips
    /// K, X and B; the three children of the merge M (H, B and F), whose lines meet in it; and M's
    /// second parent L, whose line meets the first parent's at A2. The phase's visual check of the
    /// graph relies on this layout.
    /// </summary>
    private static readonly string[] GraphScenario =
    [
        "S: T",
        "K: F",
        "T: H",
        "X: H",
        "H: M",
        "B: M",
        "F: M",
        "M: P L",
        "P: A2",
        "L: L1",
        "L1: A2",
        "A2: A1",
        "A1:",
    ];

    [Fact]
    public void A_linear_history_stays_in_lane_0_with_colour_0()
    {
        var (layout, rows) = LayOut("E: D", "D: C", "C: B", "B: A", "A:");

        AssertRow(rows[0], "E", 0, 0, Out(0, 0, 0));
        AssertRow(rows[1], "D", 0, 0, Into(0, 0, 0), Out(0, 0, 0));
        AssertRow(rows[2], "C", 0, 0, Into(0, 0, 0), Out(0, 0, 0));
        AssertRow(rows[3], "B", 0, 0, Into(0, 0, 0), Out(0, 0, 0));
        AssertRow(rows[4], "A", 0, 0, Into(0, 0, 0));
        Assert.All(rows, row => Assert.Equal(1, row.LaneCount));
        Assert.Equal(1, layout.MaxLaneCount);
        Assert.Equal(5, layout.RowCount);
    }

    [Fact]
    public void A_merged_branch_takes_a_second_lane_that_ends_where_it_forked()
    {
        var (_, rows) = LayOut("N: M", "M: C B", "C: A", "B: A", "A:");

        AssertRow(rows[0], "N", 0, 0, Out(0, 0, 0));
        AssertRow(rows[1], "M", 0, 0, Into(0, 0, 0), Out(0, 0, 0), Out(0, 1, 1));
        AssertRow(rows[2], "C", 0, 0, Into(0, 0, 0), Through(1, 1), Out(0, 0, 0));
        AssertRow(rows[3], "B", 1, 1, Through(0, 0), Into(1, 1, 1), Out(1, 1, 1));
        AssertRow(rows[4], "A", 0, 0, Into(0, 0, 0), Into(1, 0, 1));
    }

    [Fact]
    public void An_octopus_merge_starts_a_lane_for_each_further_parent()
    {
        var (_, rows) = LayOut("O: A B C", "A: R", "B: R", "C: R", "R:");

        AssertRow(rows[0], "O", 0, 0, Out(0, 0, 0), Out(0, 1, 1), Out(0, 2, 2));
        AssertRow(rows[1], "A", 0, 0, Into(0, 0, 0), Through(1, 1), Through(2, 2), Out(0, 0, 0));
        AssertRow(rows[2], "B", 1, 1, Through(0, 0), Into(1, 1, 1), Through(2, 2), Out(1, 1, 1));
        AssertRow(rows[3], "C", 2, 2, Through(0, 0), Through(1, 1), Into(2, 2, 2), Out(2, 2, 2));
        AssertRow(rows[4], "R", 0, 0, Into(0, 0, 0), Into(1, 0, 1), Into(2, 0, 2));
    }

    [Fact]
    public void Root_commits_leave_their_lane_free()
    {
        // M merges two unrelated histories; Z is a history of one commit, with no line at all.
        var (layout, rows) = LayOut("M: A B", "A:", "B:", "Z:");

        AssertRow(rows[0], "M", 0, 0, Out(0, 0, 0), Out(0, 1, 1));
        AssertRow(rows[1], "A", 0, 0, Into(0, 0, 0), Through(1, 1));
        AssertRow(rows[2], "B", 1, 1, Into(1, 1, 1));
        AssertRow(rows[3], "Z", 0, 2);
        Assert.Equal(1, rows[3].LaneCount);
        Assert.Equal(2, layout.MaxLaneCount);
    }

    [Fact]
    public void A_first_parent_that_another_lane_waits_for_gets_its_own_line_and_the_lines_meet_there()
    {
        var (_, rows) = LayOut("B: A", "C: A", "A:");

        AssertRow(rows[0], "B", 0, 0, Out(0, 0, 0));
        AssertRow(rows[1], "C", 1, 1, Through(0, 0), Out(1, 1, 1));
        AssertRow(rows[2], "A", 0, 0, Into(0, 0, 0), Into(1, 0, 1));
    }

    [Fact]
    public void A_further_parent_that_a_lane_waits_for_joins_that_lane_instead_of_starting_one()
    {
        // T's lane already waits for A when M names A as its second parent.
        var (layout, rows) = LayOut("T: A", "M: X A", "X: A", "A:", "Z:");

        AssertRow(rows[0], "T", 0, 0, Out(0, 0, 0));
        AssertRow(rows[1], "M", 1, 1, Through(0, 0), Out(1, 1, 1), Out(1, 0, 0));
        AssertRow(rows[2], "X", 1, 1, Through(0, 0), Into(1, 1, 1), Out(1, 1, 1));
        AssertRow(rows[3], "A", 0, 0, Into(0, 0, 0), Into(1, 0, 1));

        // No lane started for A, so the next lane to start gets colour 2.
        AssertRow(rows[4], "Z", 0, 2);
        Assert.Equal(2, layout.MaxLaneCount);
    }

    [Fact]
    public void A_lane_that_ends_is_reused_by_the_next_new_lane_with_a_new_colour()
    {
        // B's lane (1) ends at A while V's lane (2) still runs; W, a new tip, takes lane 1.
        var (_, rows) = LayOut("T: A", "U: B", "V: D", "B: A", "A: D", "W: D", "D:");

        AssertRow(rows[0], "T", 0, 0, Out(0, 0, 0));
        AssertRow(rows[1], "U", 1, 1, Through(0, 0), Out(1, 1, 1));
        AssertRow(rows[2], "V", 2, 2, Through(0, 0), Through(1, 1), Out(2, 2, 2));
        AssertRow(rows[3], "B", 1, 1, Through(0, 0), Into(1, 1, 1), Through(2, 2), Out(1, 1, 1));
        AssertRow(rows[4], "A", 0, 0, Into(0, 0, 0), Into(1, 0, 1), Through(2, 2), Out(0, 0, 0));
        AssertRow(rows[5], "W", 1, 3, Through(0, 0), Through(2, 2), Out(1, 1, 3));
        AssertRow(rows[6], "D", 0, 0, Into(0, 0, 0), Into(1, 0, 3), Into(2, 0, 2));
    }

    [Fact]
    public void Each_lane_keeps_its_colour_from_where_it_starts_to_where_it_ends()
    {
        // The main line runs in lane 0 with colour 0, the merged side line in lane 1 with colour 1,
        // their commits taking turns.
        var (_, rows) = LayOut("M: C F", "F: E", "C: B", "E: D", "B: A", "D: A", "A:");

        Assert.Equal([0, 1, 0, 1, 0, 1, 0], rows.Select(row => row.NodeColor));
        Assert.Equal([0, 1, 0, 1, 0, 1, 0], rows.Select(row => row.NodeLane));
        foreach (var row in rows)
        {
            foreach (var line in row.Lines)
            {
                // The lane a line belongs to: where it comes from above, or where it goes below.
                var lane = line.Kind == GraphLineKind.OutOfNode ? line.ToLane : line.FromLane;
                Assert.Equal(lane, line.Color);
            }
        }
    }

    [Fact]
    public void A_parent_listed_twice_is_handled_once()
    {
        var (_, rows) = LayOut("M: A B B A", "B: A", "A:");

        AssertRow(rows[0], "M", 0, 0, Out(0, 0, 0), Out(0, 1, 1));
        AssertRow(rows[1], "B", 1, 1, Through(0, 0), Into(1, 1, 1), Out(1, 1, 1));
        AssertRow(rows[2], "A", 0, 0, Into(0, 0, 0), Into(1, 0, 1));
    }

    [Fact]
    public void LaneCount_is_the_width_of_each_row_and_MaxLaneCount_the_widest_so_far()
    {
        var layout = new GraphLayout();
        Assert.Equal(0, layout.RowCount);
        Assert.Equal(0, layout.MaxLaneCount);

        var laneCounts = new List<int>();
        var maxLaneCounts = new List<int>();
        var rowCounts = new List<int>();
        foreach (var commit in History(GraphScenario))
        {
            laneCounts.Add(layout.Add(commit).LaneCount);
            maxLaneCounts.Add(layout.MaxLaneCount);
            rowCounts.Add(layout.RowCount);
        }

        Assert.Equal([1, 2, 2, 3, 3, 3, 3, 3, 2, 2, 2, 2, 1], laneCounts);
        Assert.Equal([1, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3], maxLaneCounts);
        Assert.Equal(Enumerable.Range(1, 13), rowCounts);
    }

    [Fact]
    public void Add_refuses_a_missing_commit()
    {
        Assert.Throws<ArgumentNullException>(() => new GraphLayout().Add(null!));
    }

    [Fact]
    public void The_graph_scenario_has_the_layout_its_visual_check_expects()
    {
        var (layout, rows) = LayOut(GraphScenario);

        Assert.Equal(13, rows.Length);
        AssertRow(rows[0], "S", 0, 0, Out(0, 0, 0));
        AssertRow(rows[1], "K", 1, 1, Through(0, 0), Out(1, 1, 1));
        AssertRow(rows[2], "T", 0, 0, Into(0, 0, 0), Through(1, 1), Out(0, 0, 0));
        AssertRow(rows[3], "X", 2, 2, Through(0, 0), Through(1, 1), Out(2, 2, 2));
        AssertRow(rows[4], "H", 0, 0, Into(0, 0, 0), Through(1, 1), Into(2, 0, 2), Out(0, 0, 0));
        AssertRow(rows[5], "B", 2, 3, Through(0, 0), Through(1, 1), Out(2, 2, 3));
        AssertRow(rows[6], "F", 1, 1, Through(0, 0), Into(1, 1, 1), Through(2, 3), Out(1, 1, 1));
        AssertRow(rows[7], "M", 0, 0, Into(0, 0, 0), Into(1, 0, 1), Into(2, 0, 3), Out(0, 0, 0), Out(0, 1, 4));
        AssertRow(rows[8], "P", 0, 0, Into(0, 0, 0), Through(1, 4), Out(0, 0, 0));
        AssertRow(rows[9], "L", 1, 4, Through(0, 0), Into(1, 1, 4), Out(1, 1, 4));
        AssertRow(rows[10], "L1", 1, 4, Through(0, 0), Into(1, 1, 4), Out(1, 1, 4));
        AssertRow(rows[11], "A2", 0, 0, Into(0, 0, 0), Into(1, 0, 4), Out(0, 0, 0));
        AssertRow(rows[12], "A1", 0, 0, Into(0, 0, 0));
        Assert.Equal(3, layout.MaxLaneCount);
    }

    [Fact]
    public void Random_histories_keep_every_line_connected_and_every_lane_to_one_commit()
    {
        var octopusMerges = 0;
        var roots = 0;
        var tips = 0;
        for (var seed = 1; seed <= 200; seed++)
        {
            var history = RandomHistory(seed, commitCount: 300);
            CheckLayout(history, $"seed {seed}");

            octopusMerges += history.Count(commit => commit.Parents.Distinct().Count() > 2);
            roots += history.Count(commit => commit.Parents.Count == 0);
            var parents = history.SelectMany(commit => commit.Parents).ToHashSet();
            tips += history.Count(commit => !parents.Contains(commit.Sha));
        }

        // The generator must keep producing the shapes the check is for.
        Assert.True(octopusMerges > 200, $"only {octopusMerges} octopus merges");
        Assert.True(roots > 400, $"only {roots} root commits");
        Assert.True(tips > 400, $"only {tips} tips");
    }

    [Fact]
    public void A_100k_commit_history_with_a_merge_every_50_commits_lays_out_in_under_2_seconds()
    {
        var history = LongHistory(100_000);

        // A first, short run, so that compiling the code is not part of the time.
        var warmUp = new GraphLayout();
        foreach (var commit in LongHistory(1_000))
        {
            warmUp.Add(commit);
        }

        var layout = new GraphLayout();
        var rows = new GraphRow[history.Count];
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < history.Count; i++)
        {
            rows[i] = layout.Add(history[i]);
        }

        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"took {stopwatch.ElapsedMilliseconds} ms");
        Assert.Equal(100_000, layout.RowCount);
        Assert.Equal(2, layout.MaxLaneCount);
        Assert.Equal(1_999, rows.Count(row => row.Lines.Count(line => line.Kind == GraphLineKind.OutOfNode) == 2));
    }

    // ---- Building histories and layouts --------------------------------------------------------

    /// <summary>Commits from lines such as <c>"M: P L"</c> (commit M, parents P then L) and <c>"A:"</c> (a root).</summary>
    private static List<CommitInfo> History(params string[] lines) =>
        [.. lines.Select(line =>
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            var parents = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return Commit(line[..colon].Trim(), parents);
        })];

    private static CommitInfo Commit(string sha, IReadOnlyList<string> parents)
    {
        var date = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        return new CommitInfo(sha, parents, "Test Author", "author@example.com", date, date, sha);
    }

    private static (GraphLayout Layout, GraphRow[] Rows) LayOut(params string[] lines)
    {
        var layout = new GraphLayout();
        var rows = History(lines).Select(layout.Add).ToArray();
        return (layout, rows);
    }

    private static GraphLine Through(int lane, int color) => new(GraphLineKind.PassThrough, lane, lane, color);

    private static GraphLine Into(int fromLane, int nodeLane, int color) => new(GraphLineKind.IntoNode, fromLane, nodeLane, color);

    private static GraphLine Out(int nodeLane, int toLane, int color) => new(GraphLineKind.OutOfNode, nodeLane, toLane, color);

    /// <summary>Compares the row as text, so that a failure names the commit and shows the whole row.</summary>
    private static void AssertRow(GraphRow row, string commit, int nodeLane, int nodeColor, params GraphLine[] lines) =>
        Assert.Equal(Describe(commit, nodeLane, nodeColor, lines), Describe(commit, row.NodeLane, row.NodeColor, row.Lines));

    private static string Describe(string commit, int nodeLane, int nodeColor, IEnumerable<GraphLine> lines)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{commit}: node in lane {nodeLane}, colour {nodeColor}; lines:");
        foreach (var line in lines)
        {
            text.Append(CultureInfo.InvariantCulture, $" {line.Kind} {line.FromLane}>{line.ToLane} colour {line.Color},");
        }

        return text.ToString();
    }

    /// <summary>
    /// A history of <paramref name="commitCount"/> commits with branches, merges, octopus merges,
    /// several roots and tips, in an order in which every commit comes after all its children.
    /// </summary>
    private static List<CommitInfo> RandomHistory(int seed, int commitCount)
    {
        var random = new Random(seed);

        // Made oldest first: every parent has a lower number than its children.
        var parentsOf = new List<int[]>(commitCount);
        var heads = new List<int>();
        for (var i = 0; i < commitCount; i++)
        {
            var roll = random.NextDouble();
            if (i == 0 || roll < 0.03)
            {
                parentsOf.Add([]);
                heads.Add(i);
            }
            else if (roll < 0.18 && heads.Count >= 2)
            {
                var count = random.NextDouble() < 0.3 ? random.Next(3, Math.Min(heads.Count, 5) + 1) : 2;
                var merged = heads.OrderBy(_ => random.Next()).Take(count).ToList();
                if (random.NextDouble() < 0.05)
                {
                    merged.Add(merged[random.Next(merged.Count)]);
                }

                parentsOf.Add([.. merged]);
                heads[heads.IndexOf(merged[0])] = i;
                foreach (var head in merged.Skip(1).Distinct())
                {
                    // Most merged branches are deleted; the others go on from where they were.
                    if (random.NextDouble() < 0.8)
                    {
                        heads.Remove(head);
                    }
                }
            }
            else if (roll < 0.30)
            {
                parentsOf.Add([i - 1 - random.Next(Math.Min(i, 30))]);
                heads.Add(i);
            }
            else
            {
                var branch = random.Next(heads.Count);
                parentsOf.Add([heads[branch]]);
                heads[branch] = i;
            }
        }

        // Shown newest first, as date order would, but now and then a commit from further back
        // that is ready (all its children shown), as when clocks were wrong.
        var childrenLeft = new int[commitCount];
        foreach (var parent in parentsOf.SelectMany(parents => parents.Distinct()))
        {
            childrenLeft[parent]++;
        }

        var ready = Enumerable.Range(0, commitCount).Where(i => childrenLeft[i] == 0).ToList();
        var order = new List<int>(commitCount);
        while (ready.Count > 0)
        {
            var pick = random.NextDouble() < 0.75 ? ready.IndexOf(ready.Max()) : random.Next(ready.Count);
            var next = ready[pick];
            ready.RemoveAt(pick);
            order.Add(next);
            foreach (var parent in parentsOf[next].Distinct())
            {
                if (--childrenLeft[parent] == 0)
                {
                    ready.Add(parent);
                }
            }
        }

        Assert.Equal(commitCount, order.Count);
        return [.. order.Select(i => Commit($"c{i}", [.. parentsOf[i].Select(parent => $"c{parent}")]))];
    }

    /// <summary>
    /// Lays the history out and checks every row against what the rows above it promised: the
    /// lanes and colours leaving the bottom of one row enter the top of the next, the lines into a
    /// node come from exactly the lanes that waited for it, every distinct parent is reached by one
    /// line, and a lane never waits for two commits at once. Plain loops rather than LINQ keep the
    /// tens of thousands of rows fast.
    /// </summary>
    private static void CheckLayout(IReadOnlyList<CommitInfo> history, string context)
    {
        var layout = new GraphLayout();

        // The model: what the rows so far say each lane waits for at their bottom edge (null for a
        // free lane), and the lane's colour there.
        var waitsFor = new List<string?>();
        var colorOf = new List<int>();
        var lanesStarted = 0;
        var widest = 0;

        string? WaitingIn(int lane) => lane < waitsFor.Count ? waitsFor[lane] : null;

        int LeftmostFree()
        {
            var free = waitsFor.IndexOf(null);
            return free >= 0 ? free : waitsFor.Count;
        }

        for (var i = 0; i < history.Count; i++)
        {
            var commit = history[i];
            var row = layout.Add(commit);

            // Messages are only built when a check fails: building them for every row takes seconds.
            void Fail(string what) =>
                Assert.Fail($"{context}, row {i}: {what}. {Describe(commit.Sha, row.NodeLane, row.NodeColor, row.Lines)}");

            // The top edge: one line from each lane that waits, in its colour, lane by lane from the
            // left, into the node exactly when the lane waited for this commit.
            var entering = 0;
            var previousLane = -1;
            foreach (var line in row.Lines)
            {
                if (line.Kind == GraphLineKind.OutOfNode)
                {
                    if (line.FromLane != row.NodeLane)
                    {
                        Fail($"{line} does not start at the node");
                    }

                    continue;
                }

                var waits = WaitingIn(line.FromLane);
                if (waits is null || colorOf[line.FromLane] != line.Color || line.FromLane <= previousLane)
                {
                    Fail($"{line} does not continue exactly one line of the row above");
                }

                var endsHere = line.Kind == GraphLineKind.IntoNode;
                if (endsHere != (waits == commit.Sha) || line.ToLane != (endsHere ? row.NodeLane : line.FromLane))
                {
                    Fail($"{line} goes to the wrong place");
                }

                previousLane = line.FromLane;
                entering++;
            }

            if (entering != waitsFor.Count - waitsFor.FindAll(lane => lane is null).Count)
            {
                Fail("a lane of the row above does not go on into this row");
            }

            // The node: in the leftmost lane that waited for the commit, in its colour, or else in
            // the leftmost free lane with the next colour.
            var waitedIn = waitsFor.IndexOf(commit.Sha);
            if (waitedIn >= 0 && (row.NodeLane != waitedIn || row.NodeColor != colorOf[waitedIn]))
            {
                Fail("the node is not in the leftmost lane that waited for it");
            }

            if (waitedIn < 0)
            {
                if (row.NodeLane != LeftmostFree() || row.NodeColor != lanesStarted)
                {
                    Fail("the node's new lane is not the leftmost free one with the next colour");
                }

                lanesStarted++;
            }

            for (var lane = 0; lane < waitsFor.Count; lane++)
            {
                if (waitsFor[lane] == commit.Sha)
                {
                    waitsFor[lane] = null;
                }
            }

            // One line to each distinct parent, first parent first: straight down in the node's
            // colour, the others to the leftmost lane that waits for them or to a new lane.
            var parents = commit.Parents.Distinct().ToList();
            var reached = 0;
            foreach (var line in row.Lines)
            {
                if (line.Kind != GraphLineKind.OutOfNode)
                {
                    continue;
                }

                if (reached == parents.Count)
                {
                    Fail("more lines to parents than parents");
                }

                var parent = parents[reached];
                var joined = waitsFor.IndexOf(parent);
                if (reached == 0)
                {
                    if (line.ToLane != row.NodeLane || line.Color != row.NodeColor)
                    {
                        Fail("the first parent does not go on straight down in the node's colour");
                    }
                }
                else if (joined >= 0)
                {
                    if (line.ToLane != joined)
                    {
                        Fail($"the line to {parent} does not join lane {joined}, which waits for it");
                    }
                }
                else
                {
                    if (line.ToLane != LeftmostFree() || line.Color != lanesStarted)
                    {
                        Fail($"the lane for {parent} is not the leftmost free one with the next colour");
                    }

                    lanesStarted++;
                }

                var already = WaitingIn(line.ToLane);
                if (already is not null && (already != parent || colorOf[line.ToLane] != line.Color))
                {
                    Fail($"lane {line.ToLane} waits for {already} in colour {colorOf[line.ToLane]}, not for {parent} in colour {line.Color}");
                }

                while (waitsFor.Count <= line.ToLane)
                {
                    waitsFor.Add(null);
                    colorOf.Add(-1);
                }

                waitsFor[line.ToLane] = parent;
                colorOf[line.ToLane] = line.Color;
                reached++;
            }

            if (reached != parents.Count)
            {
                Fail($"{reached} lines to {parents.Count} parents");
            }

            var highest = row.NodeLane;
            foreach (var line in row.Lines)
            {
                highest = Math.Max(highest, Math.Max(line.FromLane, line.ToLane));
            }

            widest = Math.Max(widest, highest + 1);
            if (row.LaneCount != highest + 1 || layout.MaxLaneCount != widest || layout.RowCount != i + 1)
            {
                Fail($"LaneCount {row.LaneCount}, MaxLaneCount {layout.MaxLaneCount}, RowCount {layout.RowCount}");
            }
        }

        // Every commit's parents are in the history, so every lane has ended.
        Assert.True(waitsFor.TrueForAll(lane => lane is null), $"lanes still wait at the end of {context}");
    }

    /// <summary>
    /// Two lines of development that a merge joins every 50 commits, after which both go on from
    /// the merge; newest first. Ids look like git's, 40 hex digits, and each parent id is a string
    /// of its own, as when commits and parents are read from git's output.
    /// </summary>
    private static List<CommitInfo> LongHistory(int commitCount)
    {
        var random = new Random(50);
        var bytes = new byte[20];
        var ids = new string[commitCount];
        for (var i = 0; i < commitCount; i++)
        {
            random.NextBytes(bytes);
            ids[i] = Convert.ToHexStringLower(bytes);
        }

        string ParentId(int number) => new(ids[number].AsSpan());

        var commits = new List<CommitInfo>(commitCount) { Commit(ids[0], []) };
        int main = 0, side = 0;
        for (var i = 1; i < commitCount; i++)
        {
            int[] parents;
            if (i % 50 == 0)
            {
                parents = [main, side];
                main = side = i;
            }
            else if (i % 2 == 0)
            {
                parents = [main];
                main = i;
            }
            else
            {
                parents = [side];
                side = i;
            }

            commits.Add(Commit(ids[i], [.. parents.Select(ParentId)]));
        }

        commits.Reverse();
        return commits;
    }
}
