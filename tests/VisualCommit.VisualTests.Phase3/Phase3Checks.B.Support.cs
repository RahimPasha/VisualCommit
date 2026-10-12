// What part B's checks share (Phase3ChecksB, docs/test-reports/phase-3.md part B): the graph
// tables G0 to G12, E0 and E1 asserted row by row (labels, lanes, colours, node shapes, lines,
// dates, columns) and by pixel for the nodes; the selection; the details and the multi-selection
// view; the left panel's main; the stage panel; running an operation through its log line; and
// reading git's state with plain git. Only part B's writer edits this file.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations.PartB;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksB
{
    /// <summary>
    /// Asserts a graph of the report against the graph control: its rows (message, short id, the
    /// Date column's text, node lane and colour number, a merge's and a stash's node), every label
    /// with its kind in drawing order and how the 130-wide Branch / Tag column draws them (the
    /// leading labels whole or cut with "…", then one "+N" for the rest), the lines of every row
    /// exactly (<see cref="ExpectedLines"/>), the lane count and the Graph column's width, and,
    /// with a picture, each node's colour by pixel (the centre of a filled node, the ring of a
    /// stash). The working-changes row, when one shows, is not part of the table.
    /// </summary>
    private static void AssertGraph(ShellDriver app, GraphTable table, Screenshot? picture = null)
    {
        var graph = Graph(app);
        var data = graph.Data!;
        Assert.True(data.IsComplete, $"{table.Name}: the history is not loaded.");
        Assert.True(
            table.Rows.Count == data.Count,
            $"{table.Name} has {table.Rows.Count} rows; the graph has {data.Count}: {string.Join(" | ", Enumerable.Range(0, data.Count).Select(row => data.CommitAt(row).Subject))}.");
        Assert.Equal(table.Lanes, data.MaxLaneCount);
        Assert.Equal(WindowLayout.GraphColumnWidth(table.Lanes), graph.Columns.Graph.Width);
        Assert.Equal(WindowLayout.BranchTagColumnWidth, graph.Columns.Refs.Width);

        var expectedLines = ExpectedLines(table);
        var palette = PaletteOf(app);
        var origin = app.BoundsOf(graph).TopLeft;
        for (var row = 0; row < table.Rows.Count; row++)
        {
            var expected = table.Rows[row];
            var commit = data.CommitAt(row);
            var layout = data.RowAt(row);
            var where = $"{table.Name} row {row} (\"{expected.Message}\")";
            Assert.True(expected.Message == commit.Subject, $"{where}: the graph shows \"{commit.Subject}\".");
            Assert.True(commit.Sha.StartsWith(expected.ShortSha, StringComparison.Ordinal), $"{where}: the commit is {commit.Sha}, not {expected.ShortSha}.");
            Assert.True(expected.Date == graph.Dates!.Format(commit.AuthorDate), $"{where}: the date is {graph.Dates!.Format(commit.AuthorDate)}, not {expected.Date}.");
            Assert.True(
                (expected.Lane, expected.Colour) == (layout.NodeLane, layout.NodeColor),
                $"{where}: the node is in lane {layout.NodeLane}, colour {layout.NodeColor}; the report gives lane {expected.Lane}, colour {expected.Colour}.");
            Assert.True(expected.Node == Node.Merge == (commit.Parents.Count > 1), $"{where}: merge node {expected.Node == Node.Merge}, but the commit has {commit.Parents.Count} parents.");
            Assert.True(expected.Node == Node.Stash == (commit.Kind == CommitKind.Stash), $"{where}: stash {expected.Node == Node.Stash}, but the commit is a {commit.Kind}.");

            var labels = data.LabelsAt(row).Select(label => new GraphLabel(label.Text, KindOf(label))).ToList();
            Assert.True(
                labels.SequenceEqual(expected.Labels),
                $"{where}: the labels are [{string.Join(", ", labels)}], the report gives [{string.Join(", ", expected.Labels)}].");
            AssertDrawnLabels(graph, row, expected, where);

            var lines = layout.Lines.Select(line => line.ToString()).Order(StringComparer.Ordinal).ToList();
            var wanted = expectedLines[row].Select(line => line.ToString()).Order(StringComparer.Ordinal).ToList();
            Assert.True(
                lines.SequenceEqual(wanted),
                $"{where}: the lines are\n  {string.Join("\n  ", lines)}\nthe report's lines give\n  {string.Join("\n  ", wanted)}");

            if (picture is null)
            {
                continue;
            }

            var centre = new Point(origin.X + graph.LaneCenterX(expected.Lane), origin.Y + graph.RowBounds(row).Center.Y);
            if (centre.Y < origin.Y + 6 || centre.Y > origin.Y + graph.ViewportHeight - 6)
            {
                continue;
            }

            var colour = palette.Lane(expected.Colour);
            if (expected.Node == Node.Stash)
            {
                var ring = Enumerable.Range(3, 3).Select(offset => picture.PixelAt(centre + new Point(offset, 0))).ToList();
                Assert.True(ring.Contains(colour), $"{where}: the stash's ring is not {colour} 3 to 5 px right of its centre: {string.Join(", ", ring)}.");
                Assert.NotEqual(colour, picture.PixelAt(centre));
            }
            else
            {
                AssertColour(colour, picture, centre, $"node of {where}");

                // A merge commit's node is 7 across, a commit's 10: the pixel 3 right of the
                // centre is wholly inside a commit's node and partly outside a merge's.
                var edge = picture.PixelAt(centre + new Point(3, 0));
                Assert.True(expected.Node == Node.Merge ? edge != colour : edge == colour, $"{where}: the node's size does not fit a {expected.Node} (the pixel 3 right of its centre is {edge}).");
            }
        }
    }

    /// <summary>What each row's lines must be, row by row, from the report's segments (<see cref="Segment"/>).</summary>
    private static List<GraphLine>[] ExpectedLines(GraphTable table)
    {
        var rows = table.Rows.Select(_ => new List<GraphLine>()).ToArray();
        foreach (var segment in table.Lines)
        {
            var start = segment.StartLane ?? segment.Lane;
            Assert.Equal(start, table.Rows[segment.FromRow].Lane);
            rows[segment.FromRow].Add(new GraphLine(GraphLineKind.OutOfNode, start, segment.Lane, segment.Colour));
            for (var row = segment.FromRow + 1; row < segment.ToRow; row++)
            {
                if (table.Rows[row].Lane == segment.Lane)
                {
                    // A node of the lane's own on the way: the line runs into it and on out of it.
                    rows[row].Add(new GraphLine(GraphLineKind.IntoNode, segment.Lane, segment.Lane, segment.Colour));
                    rows[row].Add(new GraphLine(GraphLineKind.OutOfNode, segment.Lane, segment.Lane, segment.Colour));
                }
                else
                {
                    rows[row].Add(new GraphLine(GraphLineKind.PassThrough, segment.Lane, segment.Lane, segment.Colour));
                }
            }

            rows[segment.ToRow].Add(new GraphLine(GraphLineKind.IntoNode, segment.Lane, table.Rows[segment.ToRow].Lane, segment.Colour));
        }

        return rows;
    }

    /// <summary>
    /// How the Branch / Tag column draws a row's labels: the leading labels as they fit (whole, or
    /// the last of them cut with "…"), then, when some did not fit, one "+N" label whose tooltip
    /// text lists them; nothing is drawn past the column's right edge.
    /// </summary>
    private static void AssertDrawnLabels(CommitGraphControl graph, int row, Row expected, string where)
    {
        var drawn = graph.LabelsInRow(row);
        if (expected.Labels.Count == 0)
        {
            Assert.Empty(drawn);
            return;
        }

        var shown = drawn.Where(label => !label.IsOverflow).ToList();
        for (var i = 0; i < shown.Count; i++)
        {
            var name = expected.Labels[i].Text;
            Assert.Equal(name, shown[i].FullText);
            Assert.True(
                shown[i].Text == name || (shown[i].Text.EndsWith('…') && name.StartsWith(shown[i].Text[..^1], StringComparison.Ordinal)),
                $"{where}: the label {name} is drawn as \"{shown[i].Text}\".");
        }

        var hidden = expected.Labels.Count - shown.Count;
        var overflow = drawn.Where(label => label.IsOverflow).ToList();
        if (hidden == 0)
        {
            Assert.Empty(overflow);
        }
        else
        {
            var plus = Assert.Single(overflow);
            Assert.Equal("+" + hidden.ToString(CultureInfo.InvariantCulture), plus.Text);
            Assert.Equal(expected.Labels.Skip(shown.Count).Select(label => label.Text), plus.FullText.Split('\n'));
        }

        Assert.All(drawn, label => Assert.True(label.Bounds.Right <= graph.Columns.Refs.Right + 0.5, $"{where}: the label \"{label.Text}\" reaches past the Branch / Tag column."));
    }

    private static LabelKind KindOf(RefLabel label) => label.Kind switch
    {
        RefLabelKind.DetachedHead => LabelKind.Head,
        RefLabelKind.CurrentBranch => LabelKind.Current,
        RefLabelKind.LocalBranch => LabelKind.Local,
        RefLabelKind.RemoteBranch => LabelKind.Remote,
        RefLabelKind.Tag => LabelKind.Tag,
        _ => LabelKind.Stash,
    };

    /// <summary>
    /// Asserts which columns the graph shows: all six at 1400×900 (Author "Test Author", SHA the
    /// short ids), Branch / Tag, Graph, Message and Date at 1100×700 with 3 lanes or fewer, and
    /// without Date with 4 or more ("Window sizes").
    /// </summary>
    private static void AssertColumns(ShellDriver app, bool author, bool date, bool sha)
    {
        var graph = Graph(app);
        Assert.Equal((author, date, sha), (graph.Columns.ShowsAuthor, graph.Columns.ShowsDate, graph.Columns.ShowsSha));
        var expected = new List<string> { "Branch / Tag", "Graph", "Message" };
        if (author)
        {
            expected.Add("Author");
        }

        if (date)
        {
            expected.Add("Date");
        }

        if (sha)
        {
            expected.Add("SHA");
        }

        Assert.Equal(expected, TextsIn(app.Find<Border>("ColumnHeaders")));
    }

    /// <summary>
    /// Asserts the graph's selection: exactly the commits of <paramref name="rows"/> (by SHA, 4h),
    /// none for an empty list; for one row its index; and, with a picture, the selection
    /// background on each selected row (sampled at the top right of its Message cell, above the
    /// text) and none on the others.
    /// </summary>
    private static void AssertSelected(ShellDriver app, Screenshot? picture, params int[] rows)
    {
        var graph = Graph(app);
        var data = graph.Data!;
        var expected = rows.Select(row => data.CommitAt(row).Sha).Order(StringComparer.Ordinal).ToList();
        var actual = graph.Selection.Shas.Order(StringComparer.Ordinal).ToList();
        Assert.True(
            expected.SequenceEqual(actual),
            $"The selection is [{string.Join(", ", actual.Select(sha => $"{data.IndexOf(sha)} {sha[..7]}"))}], expected rows [{string.Join(", ", rows)}].");
        Assert.False(graph.IsWorkingRowSelected, "The working-changes row is selected.");
        if (rows.Length == 1)
        {
            Assert.Equal(rows[0], graph.SelectedIndex);
        }
        else if (rows.Length == 0)
        {
            Assert.Equal(-1, graph.SelectedIndex);
        }

        if (picture is null)
        {
            return;
        }

        var palette = PaletteOf(app);
        var origin = app.BoundsOf(graph).TopLeft;

        // What lies over the rows in the picture: an open menu, an open tooltip.
        var covers = app.Window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && (control is ToolTip || AutomationProperties.GetAutomationId(control) == "ContextMenu"))
            .Select(control => app.BoundsOf(control))
            .ToList();
        for (var row = 0; row < data.Count; row++)
        {
            var bounds = graph.RowBounds(row);
            if (bounds.Top < 0 || bounds.Bottom > graph.ViewportHeight)
            {
                continue;
            }

            var point = new Point(origin.X + graph.Columns.Message.Right - 4, origin.Y + bounds.Top + 2);
            if (covers.Any(cover => cover.Inflate(24).Contains(point)))
            {
                continue;
            }

            var colour = picture.PixelAt(point);
            if (rows.Contains(row))
            {
                Assert.True(colour == palette.Selection, $"Row {row} is selected but its background at {point} is {colour}, not {palette.Selection}.");
            }
            else
            {
                Assert.True(colour != palette.Selection, $"Row {row} is not selected but has the selection background at {point}.");
            }
        }
    }

    /// <summary>Asserts that the working-changes row shows with this text, its ring in lane 0 and colour 0 (by pixel when a picture is given), and its dashed line down from the ring.</summary>
    private static void AssertWorkingRow(ShellDriver app, string text, Screenshot? picture = null)
    {
        var graph = Graph(app);
        Assert.True(graph.ShowsWorkingRow, "The working-changes row does not show.");
        Assert.Equal(text, graph.WorkingRowText);
        Assert.Equal(CommitGraphControl.RowHeight, graph.RowBounds(0).Top - graph.WorkingRowBounds.Top);
        if (picture is null)
        {
            return;
        }

        var palette = PaletteOf(app);
        var origin = app.BoundsOf(graph).TopLeft;
        var centre = new Point(origin.X + graph.LaneCenterX(0), origin.Y + graph.WorkingRowBounds.Center.Y);
        var ring = Enumerable.Range(3, 3).Select(offset => picture.PixelAt(centre + new Point(offset, 0))).ToList();
        Assert.True(ring.Contains(palette.Lane(0)), $"The working-changes row's ring is not {palette.Lane(0)} 3 to 5 px right of its centre: {string.Join(", ", ring)}.");
        Assert.NotEqual(palette.Lane(0), picture.PixelAt(centre));

        // The dashed line from the ring's bottom: dashes of 3, gaps of 3 (phase 2, D60).
        var x = centre.X - 1;
        var ringBottom = centre.Y + 5;
        Assert.Equal(palette.Lane(0), picture.PixelAt(new Point(x, ringBottom + 1)));
        Assert.NotEqual(palette.Lane(0), picture.PixelAt(new Point(x, ringBottom + 4)));
        Assert.Equal(palette.Lane(0), picture.PixelAt(new Point(x, ringBottom + 7)));
    }

    /// <summary>
    /// Rests the pointer on <paramref name="target"/> until its tooltip is open
    /// (<see cref="ShellDriver.RestPointerOnAsync(Visual, TimeSpan?)"/>) and then until the open
    /// tooltip has faded in completely (<see cref="WaitForToolTipShownAsync"/>). Returns its text.
    /// </summary>
    private static async Task<string> RestOnAsync(ShellDriver app, Visual target)
    {
        var text = await app.RestPointerOnAsync(target);
        await WaitForToolTipShownAsync(app);
        return text;
    }

    /// <summary>As <see cref="RestOnAsync"/> for the open menu's item labelled <paramref name="label"/> (<see cref="ShellDriver.RestPointerOnMenuItemAsync"/>).</summary>
    private static async Task<string> RestOnMenuItemAsync(ShellDriver app, string label)
    {
        var text = await app.RestPointerOnMenuItemAsync(label);
        await WaitForToolTipShownAsync(app);
        return text;
    }

    /// <summary>
    /// Waits until every tooltip in the window is fully drawn: the theme fades a tooltip in after
    /// it opens (an opacity transition), and a picture taken as it opens shows it half
    /// transparent, the text below showing through. A condition, not a fixed time.
    /// </summary>
    private static async Task WaitForToolTipShownAsync(ShellDriver app)
    {
        await app.WaitForAsync(
            () => app.Window.GetVisualDescendants().OfType<ToolTip>().Any(tip => tip.IsEffectivelyVisible)
                && app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible).All(tip => tip.Opacity >= 1),
            "the open tooltip to fade in completely");
        app.Settle();
    }

    /// <summary>Asserts that no working-changes row shows.</summary>
    private static void AssertNoWorkingRow(ShellDriver app) =>
        Assert.False(Graph(app).ShowsWorkingRow, "A working-changes row shows.");

    /// <summary>Asserts that no in-progress banner shows.</summary>
    private static void AssertNoBanner(ShellDriver app) =>
        Assert.True(app.BannerBounds is null, "A banner shows: " + (app.BannerBounds is null ? string.Empty : ((TextBlock)app.FindByAutomationId("BannerText")).Text));

    /// <summary>
    /// Asserts the in-progress banner as check 9 step 2 describes it, beyond its text and button
    /// (<see cref="Phase3Checks.AssertBanner"/>): above the column headers and as wide as the graph
    /// area; the warning icon; its background and the 1-pixel warning line along its bottom by
    /// pixel; the rows start below it plus the column headers (D122).
    /// </summary>
    private static void AssertBannerLook(ShellDriver app, Rect banner, Screenshot picture)
    {
        var palette = PaletteOf(app);
        var graph = Graph(app);
        var graphArea = app.BoundsOf(app.FindByAutomationId("CommitGraph"));
        Assert.Equal(graphArea.Left, banner.Left, 0.5);
        Assert.Equal(graphArea.Width, banner.Width, 0.5);
        Assert.Equal(WindowLayout.GraphAreaTop, banner.Top, 0.5);
        Assert.Equal(WindowLayout.RowsTopBelow(banner.Bottom), app.BoundsOf(graph).Top, 0.5);
        Assert.True(app.Find<Control>("BannerIcon").IsEffectivelyVisible, "The banner has no warning icon.");
        AssertColour(palette.BannerBackground, picture, new Point(banner.Center.X, banner.Top + 2), "the banner's background");
        AssertColour(palette.Warning, picture, new Point(banner.Center.X, banner.Bottom - 0.5), "the line along the banner's bottom");
    }

    /// <summary>
    /// Waits until the details panel shows the commit with this subject, and the one the graph has
    /// selected (by its SHA: the original "Alpha one" and its pick have one subject).
    /// </summary>
    private static async Task WaitForDetailsAsync(ShellDriver app, string subject)
    {
        await app.WaitForAsync(
            () => Shown(app, "DetailsSubject") is SelectableTextBlock text && text.Text == subject
                && Shown(app, "DetailsSha") is SelectableTextBlock sha
                && Graph(app).Selection.Shas is [var selected] && sha.Text == selected,
            $"the details of \"{subject}\", the selected commit");
        app.Settle();
    }

    /// <summary>The visible control with this automation id, or null while none is in the window (the details are made when a commit is first selected).</summary>
    private static Control? Shown(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId);

    /// <summary>A commit's author and committer dates, read with plain git as UTC times.</summary>
    private static async Task<(DateTimeOffset Author, DateTimeOffset Committer)> DatesAsync(TempRepo repo, string revision)
    {
        var parts = (await GitOutAsync(repo, "log", "-1", "--format=%at %ct", revision)).Split(' ');
        return (
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[0], CultureInfo.InvariantCulture)),
            DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[1], CultureInfo.InvariantCulture)));
    }

    /// <summary>A commit's author and committer as "name &lt;email&gt;", read with plain git.</summary>
    private static Task<string> PeopleAsync(TempRepo repo, string revision) => GitOutAsync(repo, "log", "-1", "--format=%an <%ae> / %cn <%ce>", revision);

    /// <summary>
    /// Asserts the details of a commit (phase 1's panel with phase 3's "Commit date" row): its
    /// subject, author, date, committer row (none here: the harness commits as Test Author), the
    /// commit date row when given, the parents' short ids in order and the changed files as
    /// "letter name folder".
    /// </summary>
    private static async Task AssertDetailsAsync(ShellDriver app, string subject, string date, string? commitDate, string[] parents, params string[] files)
    {
        await WaitForDetailsAsync(app, subject);
        Assert.Equal("Test Author <author@example.com>", ((SelectableTextBlock)app.FindByAutomationId("DetailsAuthor")).Text);
        Assert.Equal(date, ((TextBlock)app.FindByAutomationId("DetailsDate")).Text);
        Assert.False(app.FindByAutomationId("DetailsCommitter").IsEffectivelyVisible, "The details show a Committer row.");
        var commitDateBlock = (TextBlock)app.FindByAutomationId("DetailsCommitDate");
        Assert.Equal(commitDate is not null, commitDateBlock.IsEffectivelyVisible);
        if (commitDate is not null)
        {
            Assert.Equal(commitDate, commitDateBlock.Text);
        }

        var links = app.Window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "ParentLink")
            .OrderBy(link => app.BoundsOf(link).X)
            .Select(link => AutomationProperties.GetName(link))
            .ToList();
        Assert.Equal(parents, links);
        Assert.Equal($"Changed files ({files.Length.ToString(CultureInfo.InvariantCulture)})", ((TextBlock)app.FindByAutomationId("ChangedFilesTitle")).Text);
        await app.WaitForAsync(() => DetailsFileRows(app).SequenceEqual(files), $"the details' files {string.Join(", ", files)}");
    }

    /// <summary>The details panel's changed files as "letter name folder", top to bottom.</summary>
    private static List<string> DetailsFileRows(ShellDriver app)
    {
        var list = (ItemsControl)app.FindByAutomationId("FileList");
        return [.. ((IEnumerable<ChangedFileRow>)(list.ItemsSource ?? Array.Empty<ChangedFileRow>())).Where(row => row.IsFile).Select(FileRowText)];
    }

    private static string FileRowText(ChangedFileRow row) =>
        string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => part.Length > 0));

    /// <summary>A stage panel list's files as "letter name folder" (<c>UnstagedFileList</c>, <c>StagedFileList</c>).</summary>
    private static List<string> StageRows(ShellDriver app, string list)
    {
        var items = app.Find<ItemsControl>(list);
        return [.. ((IEnumerable<ChangedFileRow>)(items.ItemsSource ?? Array.Empty<ChangedFileRow>())).Where(row => row.IsFile).Select(FileRowText)];
    }

    /// <summary>Waits until the stage panel shows with these titles over its two lists.</summary>
    private static Task WaitForStagePanelAsync(ShellDriver app, string unstagedTitle, string stagedTitle) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                && app.Find<TextBlock>("UnstagedTitle").Text == unstagedTitle
                && app.Find<TextBlock>("StagedTitle").Text == stagedTitle,
            $"the stage panel with \"{unstagedTitle}\" and \"{stagedTitle}\"");

    /// <summary>
    /// Asserts the multi-selection view (4h) in the right panel: "N commits selected", the commits
    /// in row order as "short id subject" (short ids in the code font, secondary colour), the
    /// hint, and no file list.
    /// </summary>
    private static void AssertMultiSelection(ShellDriver app, params string[] items)
    {
        var view = app.FindByAutomationId("MultiSelectionView");
        Assert.True(view.IsEffectivelyVisible, "The multi-selection view does not show.");
        Assert.Equal($"{items.Length.ToString(CultureInfo.InvariantCulture)} commits selected", ((TextBlock)app.FindByAutomationId(view, "MultiSelectionTitle")).Text);
        var rows = view.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "MultiSelectionItem")
            .OrderBy(control => app.BoundsOf(control).Y)
            .ToList();
        Assert.Equal(items, rows.Select(row => string.Join(' ', TextsIn(row))));
        var palette = PaletteOf(app);
        foreach (var row in rows)
        {
            var sha = row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "MultiSelectionSha");
            Assert.Equal(palette.TextSecondary, (sha.Foreground as ISolidColorBrush)?.Color);
            Assert.Contains("Mono", sha.FontFamily.Name, StringComparison.OrdinalIgnoreCase);
        }

        var hint = (TextBlock)app.FindByAutomationId(view, "MultiSelectionHint");
        Assert.Equal(MultiSelectionHint, hint.Text);
        Assert.Equal(palette.TextSecondary, (hint.Foreground as ISolidColorBrush)?.Color);
        Assert.False(app.FindByAutomationId("FileList").IsEffectivelyVisible, "The multi-selection view shows a file list.");
    }

    /// <summary>
    /// Asserts the left panel's row of the local branch <paramref name="name"/>: its ahead and
    /// behind marks, and whether it is the branch HEAD is on (the accent colour; the panel draws
    /// no check mark).
    /// </summary>
    private static async Task AssertLocalBranchAsync(ShellDriver app, string name, int ahead, int behind, bool head)
    {
        var model = (RefItemRow)app.RefRowModel("Local branches", name);
        Assert.Equal((ahead, behind, head), (model.Ahead, model.Behind, model.IsHead));
        var row = await app.RefRowAsync("Local branches", name);
        var text = row.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "RefName");
        var palette = PaletteOf(app);
        var colour = (text.Foreground as ISolidColorBrush)?.Color;
        Assert.True(head == (colour == palette.Accent), $"{name} is drawn in {colour}; HEAD is {(head ? string.Empty : "not ")}on it.");
        Assert.DoesNotContain(TextsIn(row), part => part.Contains('✓', StringComparison.Ordinal));
        Assert.Equal(ahead > 0, row.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Name == "AheadCount" && block.IsEffectivelyVisible && block.Text == ahead.ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(behind > 0, row.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Name == "BehindCount" && block.IsEffectivelyVisible && block.Text == behind.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Opens the context menu of a point with a right-click and returns it once it is placed,
    /// asserting the report's notation, the highlighted item, the height of 4a's formula and that
    /// it lies inside the window, and the colours of its items' labels (D114: a disabled item in
    /// the disabled colour, an enabled destructive one in the danger colour, the others primary).
    /// </summary>
    private static MenuSnapshot OpenMenu(ShellDriver app, Point at, string[] lines, string? highlighted)
    {
        app.RightClick(at);
        var menu = app.Menu();
        AssertMenuLook(app, menu, lines, highlighted);
        return menu;
    }

    /// <summary>The assertions of <see cref="OpenMenu"/> on a menu already open.</summary>
    private static void AssertMenuLook(ShellDriver app, MenuSnapshot menu, string[] lines, string? highlighted)
    {
        AssertMenu(menu, lines);
        AssertHighlighted(menu, highlighted);
        AssertMenuShape(app, menu);
        var palette = PaletteOf(app);
        foreach (var item in menu.Items)
        {
            var expected = !item.IsEnabled ? palette.TextDisabled : item.IsDestructive ? palette.Danger : palette.TextPrimary;
            Assert.True(item.TextColor == expected, $"The menu item \"{item.Label}\" is drawn in {item.TextColor}, expected {expected}.");
        }
    }

    /// <summary>
    /// Runs an input that starts the operation <paramref name="title"/> of the ops repository and
    /// waits until it has ended (its log line, design 1.7) and the queue is idle with its refresh
    /// done ("After every operation the check waits until the queue is idle"). Returns the line.
    /// </summary>
    private static async Task<string> RunOperationAsync(ShellDriver app, string title, Action input, string repository = Ops)
    {
        var mark = app.LogMark();
        input();
        var line = await app.WaitForLogLineAsync(AppLogFile.OperationEnded(repository, title), mark: mark, timeout: NetworkTimeout);
        await app.WaitForIdleAsync();
        return line;
    }

    /// <summary>Waits until the active repository has reloaded its history after an operation: the graph control shows data with this many rows and its top row is <paramref name="topSha"/>.</summary>
    private static Task WaitForGraphAsync(ShellDriver app, int rows, string topSha) =>
        app.WaitForAsync(
            () => Graph(app).Data is { IsComplete: true } data && data.Count == rows && data.CommitAt(0).Sha.StartsWith(topSha, StringComparison.Ordinal),
            $"the graph of {rows} rows with {topSha} on top");

    // ---------------------------------------------------------------------------------------
    // Git's state, read back with plain git.

    /// <summary>A git call's standard output with its trailing line end removed.</summary>
    private static async Task<string> GitOutAsync(TempRepo repo, params string[] arguments) =>
        (await repo.GitAsync(arguments)).StandardOutput.TrimEnd('\n');

    /// <summary>The lines of <c>git status --short --branch</c>.</summary>
    private static async Task<string[]> StatusAsync(TempRepo repo) =>
        (await GitOutAsync(repo, "status", "--short", "--branch")).Split('\n');

    /// <summary>The full id a revision names.</summary>
    private static Task<string> RevAsync(TempRepo repo, string revision) => GitOutAsync(repo, "rev-parse", "--verify", revision);

    /// <summary>A commit's parents' full ids, in order.</summary>
    private static async Task<string[]> ParentsAsync(TempRepo repo, string revision) =>
        (await GitOutAsync(repo, "log", "-1", "--format=%P", revision)).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>A commit's whole message (subject, a blank line, body) as git stores it, without its last line end.</summary>
    private static Task<string> MessageAsync(TempRepo repo, string revision) => GitOutAsync(repo, "log", "-1", "--format=%B", revision);

    /// <summary>A file of the working tree, read as it is.</summary>
    private static string WorkingFile(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

    /// <summary>A file of git's folder (MERGE_HEAD, MERGE_MSG, …), or null when it is not there.</summary>
    private static string? GitDirFile(TempRepo repo, string name)
    {
        var path = Path.Combine(repo.Path, ".git", name);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>Whether git's folder has the sequencer folder (a cherry-pick or revert of several commits).</summary>
    private static bool HasSequencer(TempRepo repo) => Directory.Exists(Path.Combine(repo.Path, ".git", "sequencer"));

    /// <summary>A file in a commit, as git stores it.</summary>
    private static async Task<string> ShowAsync(TempRepo repo, string revision, string path) =>
        (await repo.GitAsync("show", $"{revision}:{path}")).StandardOutput;

    /// <summary>The backup refs, taken apart (<see cref="BackupRef"/>), with what each points at.</summary>
    private static async Task<List<(BackupRef Ref, string Name, string Sha)>> BackupsAsync(TempRepo repo) =>
        [.. (await repo.BackupRefsAsync()).Select(entry => (BackupRef.Parse(entry.Name), entry.Name, entry.Sha))];

    /// <summary>
    /// Asserts the backups an Abort (or a hard reset) makes (D83): <c>&lt;kind&gt;-&lt;time&gt;</c>
    /// pointing at <paramref name="target"/> and, when <paramref name="changes"/>, a
    /// <c>-changes</c> ref of the same time. Returns the snapshot's ref name (null without one).
    /// </summary>
    private static async Task<string?> AssertBackupsAsync(TempRepo repo, string kind, string target, bool changes)
    {
        var backups = await BackupsAsync(repo);
        Assert.Equal(changes ? 2 : 1, backups.Count);
        var main = Assert.Single(backups, backup => !backup.Ref.IsChanges);
        Assert.Equal(kind, main.Ref.Kind);
        Assert.Null(main.Ref.Name);
        Assert.Equal(target, main.Sha);
        if (!changes)
        {
            return null;
        }

        var snapshot = Assert.Single(backups, backup => backup.Ref.IsChanges);
        Assert.Equal(kind, snapshot.Ref.Kind);
        Assert.Equal(main.Ref.Time, snapshot.Ref.Time);
        Assert.Equal(main.Name + "-changes", snapshot.Name);
        return snapshot.Name;
    }

    /// <summary>Asserts that no ref under <c>refs/visualcommit/backup</c> exists.</summary>
    private static async Task AssertNoBackupsAsync(TempRepo repo) => Assert.Empty(await repo.BackupRefsAsync());

    /// <summary>Asserts that no backup ref shows in the graph or the left panel (D83).</summary>
    private static void AssertBackupsHidden(ShellDriver app)
    {
        var data = Graph(app).Data!;
        for (var row = 0; row < data.Count; row++)
        {
            Assert.DoesNotContain(data.LabelsAt(row), label => label.RefName?.StartsWith("refs/visualcommit/", StringComparison.Ordinal) == true);
        }

        Assert.DoesNotContain(TextsIn(app.FindByAutomationId("LeftPanel")), text => text.Contains("visualcommit", StringComparison.OrdinalIgnoreCase) || text.Contains("backup", StringComparison.OrdinalIgnoreCase));
    }
}
