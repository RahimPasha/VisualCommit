// What part C's checks share beyond Phase3Checks.Support.cs: the graph as the report's tables write
// it (Phase3Expectations.PartC.Row), the left panel's rows, the stage panel, the name dialog, the
// menu's colours, the toolbar's Stash and Pop, and git's state read back with plain git. Only part
// C's writer edits this file.
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksC
{
    /// <summary>
    /// Asserts a graph as the report's tables write it: the rows' messages, node lanes and colours,
    /// rings for stashes, labels in phase 1's order (the check mark on the branch HEAD is on, the
    /// filled "HEAD"), dates where stated, the lanes that end in each node, the number of lanes
    /// with the Graph column's width, and the working-changes row (its text and its ring's lane and
    /// colour) or its absence. Shapes are asserted by pixel on <paramref name="screenshot"/>: each
    /// node's colour (a ring's outside of its centre), the working-changes ring.
    /// </summary>
    private static void AssertGraph(ShellDriver app, Screenshot screenshot, IReadOnlyList<PartC.Row> rows, int lanes, PartC.WorkingRow? working = null)
    {
        var graph = Graph(app);
        var data = graph.Data!;
        var palette = PaletteOf(app);
        Assert.True(data.IsComplete, "The graph's history is not complete.");
        Assert.Equal(rows.Select(row => row.Message), Enumerable.Range(0, data.Count).Select(index => data.CommitAt(index).Subject));
        Assert.Equal(lanes, data.MaxLaneCount);
        Assert.Equal(WindowLayout.GraphColumnWidth(lanes), graph.Columns.Graph.Width);
        var origin = app.BoundsOf(graph).TopLeft;
        for (var index = 0; index < rows.Count; index++)
        {
            var expected = rows[index];
            var commit = data.CommitAt(index);
            var layout = data.RowAt(index);
            var where = $"Row {index} (\"{expected.Message}\")";
            Assert.True(expected.Lane == layout.NodeLane, $"{where} has its node in lane {layout.NodeLane}, expected {expected.Lane}.");
            Assert.True(expected.Color == layout.NodeColor, $"{where} has colour {layout.NodeColor}, expected {expected.Color}.");
            Assert.True(expected.Stash == (commit.Kind == VisualCommit.Core.Git.CommitKind.Stash), $"{where} is{(expected.Stash ? " not" : string.Empty)} a stash.");
            if (expected.Date is { } date)
            {
                Assert.Equal(date, graph.Dates!.Format(commit.AuthorDate));
            }

            var ends = layout.Lines
                .Where(line => line.Kind == GraphLineKind.IntoNode && line.FromLane != layout.NodeLane)
                .Select(line => line.FromLane)
                .Distinct()
                .Order()
                .ToList();
            Assert.True(expected.EndingLanes.Order().SequenceEqual(ends), $"{where}: lanes [{string.Join(", ", ends)}] end in its node, expected [{string.Join(", ", expected.EndingLanes)}].");

            AssertLabels(graph, index, expected);

            var centre = new Point(origin.X + graph.LaneCenterX(expected.Lane), origin.Y + graph.RowBounds(index).Center.Y);
            if (expected.Stash)
            {
                AssertRing(screenshot, centre, palette.Lane(expected.Color), $"{where}'s stash ring");
            }
            else
            {
                AssertColour(palette.Lane(expected.Color), screenshot, centre, $"node of {where}");
            }
        }

        Assert.Equal(working is not null, graph.ShowsWorkingRow);
        if (working is not null)
        {
            Assert.Equal(working.Text, graph.WorkingRowText ?? PartC.WorkingChanges);
            var ring = new Point(origin.X + graph.LaneCenterX(working.Lane), origin.Y + graph.WorkingRowBounds.Center.Y);
            AssertRing(screenshot, ring, palette.Lane(working.Color), "the working-changes ring");
        }
    }

    /// <summary>A row's labels: all of them in phase 1's order from the data, and as drawn: a prefix of them, then "+N" for the rest.</summary>
    private static void AssertLabels(CommitGraphControl graph, int index, PartC.Row expected)
    {
        var data = graph.Data!;
        var labels = data.LabelsAt(index);
        var written = labels.Select(label => label.Kind switch
        {
            RefLabelKind.CurrentBranch => label.Text + PartC.Check,
            _ => label.Text,
        }).ToList();
        Assert.True(expected.Labels.SequenceEqual(written), $"Row {index} (\"{expected.Message}\") has the labels [{string.Join(", ", written)}], expected [{string.Join(", ", expected.Labels)}].");
        foreach (var label in labels.Where(label => label.Text == "HEAD"))
        {
            Assert.Equal(RefLabelKind.DetachedHead, label.Kind);
        }

        var drawn = graph.LabelsInRow(index);
        var shown = drawn.Where(label => !label.IsOverflow).ToList();
        Assert.True(
            shown.Select(label => label.FullText).SequenceEqual(labels.Take(shown.Count).Select(label => label.Text)),
            $"Row {index} draws [{string.Join(", ", drawn.Select(label => label.Text))}], not a prefix of its labels.");
        var rest = labels.Count - shown.Count;
        if (rest > 0)
        {
            var overflow = Assert.Single(drawn, label => label.IsOverflow);
            Assert.Equal("+" + rest, overflow.Text);
        }
        else
        {
            Assert.DoesNotContain(drawn, label => label.IsOverflow);
        }
    }

    /// <summary>A ring 10 across and 2 wide: between 3 and 5 pixels right of its centre some pixel has the colour, and its centre does not.</summary>
    private static void AssertRing(Screenshot screenshot, Point centre, Color colour, string what)
    {
        var samples = Enumerable.Range(3, 3).Select(offset => screenshot.PixelAt(centre + new Point(offset, 0))).ToList();
        Assert.True(samples.Contains(colour), $"The {what} at {centre} is not {colour} anywhere from 3 to 5 pixels right of its centre: {string.Join(", ", samples)}.");
        Assert.NotEqual(colour, screenshot.PixelAt(centre));
    }

    /// <summary>Asserts which commit rows are selected (none for an empty list), the working-changes row's selection, and the selection background of a selected row, by pixel.</summary>
    private static void AssertSelection(ShellDriver app, Screenshot? screenshot, int? row, bool workingRow = false)
    {
        var graph = Graph(app);
        Assert.Equal(row ?? -1, graph.SelectedIndex);
        Assert.Equal(workingRow, graph.IsWorkingRowSelected);
        if (screenshot is not null && (row is not null || workingRow))
        {
            var origin = app.BoundsOf(graph).TopLeft;
            var bounds = row is { } index ? graph.RowBounds(index) : graph.WorkingRowBounds;
            var point = new Point(origin.X + graph.Columns.Message.Right - 4, origin.Y + bounds.Top + 4);
            AssertColour(PaletteOf(app).Selection, screenshot, point, "the selected row's background");
        }
    }

    /// <summary>Waits until the details panel shows the commit <paramref name="subject"/>.</summary>
    private static Task WaitForDetailsAsync(ShellDriver app, string subject) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("RightPanel").GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.IsEffectivelyVisible && text.Text == subject && Avalonia.Automation.AutomationProperties.GetAutomationId(text) == "DetailsSubject"),
            $"the details of \"{subject}\"");

    /// <summary>The text of a visible control with this automation id inside the right panel.</summary>
    private static string? RightPanelText(ShellDriver app, string automationId) =>
        app.FindByAutomationId("RightPanel").GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(text => text.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(text) == automationId)?.Text;

    // ---- The left panel ----

    private static RefSectionRow SectionRow(ShellDriver app, string section) => (RefSectionRow)app.RefRowModel(section);

    private static RefItemRow ItemRow(ShellDriver app, params string[] path) => (RefItemRow)app.RefRowModel(path);

    /// <summary>The rows of a section, its own rows and theirs, as "  name" indented two spaces per level below the section.</summary>
    private static List<string> SectionLines(ShellDriver app, string section)
    {
        app.Settle();
        var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        var start = rows.FindIndex(row => row is RefSectionRow && row.Text == section);
        Assert.True(start >= 0, $"The left panel has no section {section}.");
        return [.. rows.Skip(start + 1).TakeWhile(row => row is RefItemRow).Select(row => new string(' ', 2 * (row.Depth - 1)) + row.Text)];
    }

    /// <summary>
    /// Asserts a local branch's row: whether HEAD is on it, drawn in the accent colour (else the
    /// primary text colour), and its arrows (ahead, behind). The row's button is laid out first.
    /// </summary>
    private static async Task AssertBranchRowAsync(ShellDriver app, string[] path, bool head, int ahead = 0, int behind = 0)
    {
        var model = ItemRow(app, path);
        Assert.Equal(head, model.IsHead);
        Assert.Equal(ahead, model.Ahead);
        Assert.Equal(behind, model.Behind);
        var button = await app.RefRowAsync(path);
        var name = button.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == path[^1] && text.IsEffectivelyVisible);
        var palette = PaletteOf(app);
        Assert.Equal(head ? palette.Accent : palette.TextPrimary, (name.Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>No row of the left panel is selected (D114: a right-click there selects nothing).</summary>
    private static void AssertNoLeftPanelSelection(ShellDriver app)
    {
        app.Settle();
        var selected = app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).Select(row => row.Text).ToList();
        Assert.True(selected.Count == 0, "Selected in the left panel: " + string.Join(", ", selected));
    }

    // ---- The toolbar ----

    /// <summary>A toolbar button's state: enabled or not, and its tooltip (a disabled button's reason; null for none).</summary>
    private static void AssertToolbar(ShellDriver app, string automationId, bool enabled, string? toolTip = null)
    {
        var button = ToolbarButton(app, automationId);
        Assert.True(enabled == button.IsEffectivelyEnabled, $"{automationId} is {(button.IsEffectivelyEnabled ? "enabled" : "disabled")}.");
        if (!enabled || toolTip is not null)
        {
            AssertTip(button, toolTip);
        }
    }

    // ---- The stage panel ----

    /// <summary>Waits until the stage panel shows with these titles of its two lists.</summary>
    private static Task WaitForStagePanelAsync(ShellDriver app, string unstagedTitle, string stagedTitle) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                && app.Find<TextBlock>("UnstagedTitle").Text == unstagedTitle
                && app.Find<TextBlock>("StagedTitle").Text == stagedTitle
                && app.StagePanelSettled,
            $"the stage panel with \"{unstagedTitle}\" and \"{stagedTitle}\"");

    /// <summary>A stage list's file rows as "&lt;letter&gt; &lt;name&gt; &lt;folder&gt;" (the folder left out when the file has none).</summary>
    private static List<string> FileRows(ShellDriver app, string list)
    {
        var rows = (IEnumerable<ChangedFileRow>)app.Find<ItemsControl>(list).ItemsSource!;
        return [.. rows.Where(row => row.IsFile).Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part))))];
    }

    /// <summary>A conflicted file's row: its "U" in the danger colour and the conflict tooltip.</summary>
    private static async Task AssertConflictRowAsync(ShellDriver app, string path)
    {
        var row = await app.FileRowAsync("UnstagedFileList", path);
        AssertTip(row, PartC.ConflictRowTip);
        var letter = row.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "U");
        Assert.Equal(PaletteOf(app).Danger, (letter.Foreground as ISolidColorBrush)?.Color);
    }

    private static string CommitBoxText(ShellDriver app, string automationId) => ((TextBox)app.FindByAutomationId(automationId)).Text ?? string.Empty;

    // ---- The banner ----

    private static string? BannerToolTip(ShellDriver app) => ToolTip.GetTip(app.Find<Border>("BannerInfo")) as string;

    // ---- Dialogs ----

    /// <summary>
    /// Asserts the name dialog on top (4f): its title, context (null: hidden), the name box's text,
    /// placeholder and focus, the error (empty: hidden), the option (null: hidden) and its tick,
    /// "Cancel", and the confirm button's label, primary style and whether it is enabled.
    /// </summary>
    private static async Task AssertNameDialogAsync(
        ShellDriver app,
        string title,
        string? context,
        string name,
        string? placeholder,
        string error,
        string? option,
        bool optionChecked,
        string confirm,
        bool confirmEnabled)
    {
        var model = await app.WaitForDialogAsync<NameDialogViewModel>();
        var view = app.TopDialogView!;
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        Assert.Equal(title, model.Title);
        var contextBlock = (TextBlock)app.FindByAutomationId(view, "NameDialogContext");
        Assert.Equal(context is not null, contextBlock.IsEffectivelyVisible);
        if (context is not null)
        {
            Assert.Equal(context, contextBlock.Text);
        }

        Assert.False(app.FindByAutomationId(view, "DialogRepository").IsEffectivelyVisible, "The dialog shows \"In <repo>\" with one tab.");
        var box = (TextBox)app.FindByAutomationId(view, "NameBox");
        Assert.True(box.IsEffectivelyVisible);
        Assert.Equal(name, box.Text ?? string.Empty);
        if (placeholder is not null)
        {
            Assert.Equal(placeholder, box.PlaceholderText);
        }

        await app.WaitForAsync(() => box.IsFocused, "the name box to have the keyboard focus");
        AssertNameError(app, error);
        var optionBox = (CheckBox)app.FindByAutomationId(view, "NameDialogOption");
        Assert.Equal(option is not null, optionBox.IsEffectivelyVisible);
        if (option is not null)
        {
            Assert.Equal(option, optionBox.Content);
            Assert.Equal(optionChecked, optionBox.IsChecked);
        }

        AssertDialogButtons(app, confirm, confirmEnabled);
    }

    /// <summary><c>NameError</c>: its text in the danger colour, or hidden when <paramref name="error"/> is empty.</summary>
    private static void AssertNameError(ShellDriver app, string error)
    {
        app.Settle();
        var block = (TextBlock)app.FindByAutomationId(app.TopDialogView!, "NameError");
        Assert.True(error.Length > 0 == block.IsEffectivelyVisible, $"NameError is {(block.IsEffectivelyVisible ? "shown: " + block.Text : "hidden")}; expected \"{error}\".");
        if (error.Length > 0)
        {
            Assert.Equal(error, block.Text);
            Assert.Equal(PaletteOf(app).Danger, (block.Foreground as ISolidColorBrush)?.Color);
        }
    }

    /// <summary>The dialog's "Cancel" and its confirm button (primary style, enabled or not).</summary>
    private static void AssertDialogButtons(ShellDriver app, string confirm, bool confirmEnabled)
    {
        var view = app.TopDialogView!;
        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        Assert.Equal("Cancel", cancel.Content);
        var button = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(confirm, button.Content);
        Assert.True(confirmEnabled == button.IsEffectivelyEnabled, $"\"{confirm}\" is {(button.IsEffectivelyEnabled ? "enabled" : "disabled")}.");
        Assert.Same(app.Window.FindResource("VcPrimaryButton"), button.Theme);
        Assert.True(app.BoundsOf(cancel).X < app.BoundsOf(button).X, "Cancel is not left of the confirm button.");
    }

    /// <summary>Clicks a button of the dialog on top by its automation id.</summary>
    private static void ClickDialogButton(ShellDriver app, string automationId) =>
        app.Click(app.FindByAutomationId(app.TopDialogView ?? throw new InvalidOperationException("No dialog is open."), automationId));

    // ---- Menus ----

    /// <summary>
    /// Asserts a menu as every check of this part does: its lines in the report's notation, the
    /// highlighted item, its height by 4a's formula and place inside the window, its top-left at
    /// the pointer + (2, 2) or where 4a's rule moves a menu that does not fit there
    /// (<see cref="Phase3Checks.AssertMenuPlaced"/>), the items' text colours by brush and the
    /// highlight's background by pixel on <paramref name="screenshot"/>.
    /// </summary>
    private static void AssertMenuAt(ShellDriver app, MenuSnapshot menu, Screenshot screenshot, Point pointer, string? highlighted, params string[] lines)
    {
        AssertMenu(menu, lines);
        AssertHighlighted(menu, highlighted);
        AssertMenuShape(app, menu);

        // At the pointer when it fits below it; phase 4's taller menus may open above it (4a's rule).
        AssertMenuPlaced(app, menu, pointer);
        var palette = PaletteOf(app);
        foreach (var item in menu.Items)
        {
            var colour = !item.IsEnabled ? palette.TextDisabled : item.IsDestructive ? palette.Danger : palette.TextPrimary;
            Assert.True(item.TextColor == colour, $"The menu item \"{item.Label}\" is drawn in {item.TextColor}, expected {colour}.");
        }

        if (menu.Highlighted is { } item2)
        {
            AssertColour(palette.Hover, screenshot, new Point(item2.Bounds.Right - 4, item2.Bounds.Center.Y), $"the background of the highlighted item \"{item2.Label}\"");
        }
    }

    /// <summary>The open tooltip's popup lies inside the window ("drawn inside the window", D105).</summary>
    private static void AssertToolTipInsideWindow(ShellDriver app, string text)
    {
        var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).ToList();
        var tip = Assert.Single(tips, candidate => ToolTipText(candidate) == text);
        var bounds = app.BoundsOf(tip);
        var size = app.Window.ClientSize;
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height, $"The tooltip at {bounds} is not inside the {size.Width}x{size.Height} window.");
    }

    private static string ToolTipText(ToolTip tip) => tip.Content switch
    {
        string text => text,
        Visual visual => string.Join('\n', visual.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text)),
        _ => string.Empty,
    };

    // ---- Git, read back with plain git ----

    /// <summary>The lines git prints for these arguments, without empty ones.</summary>
    private static async Task<List<string>> GitLinesAsync(TempRepo repo, params string[] arguments) =>
        [.. (await repo.GitAsync(arguments)).StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];

    /// <summary><c>git status --porcelain</c>'s lines.</summary>
    private static Task<List<string>> StatusAsync(TempRepo repo) => GitLinesAsync(repo, "status", "--porcelain");

    /// <summary><c>git branch</c>'s lines, trimmed of the leading spaces ("* hotfix" for HEAD's).</summary>
    private static async Task<List<string>> BranchesAsync(TempRepo repo) =>
        [.. (await GitLinesAsync(repo, "branch")).Select(line => line.Trim())];

    /// <summary><c>git stash list</c>'s lines ("stash@{0}: On main: Experiment").</summary>
    private static Task<List<string>> StashListAsync(TempRepo repo) => GitLinesAsync(repo, "stash", "list");

    /// <summary>The commit (or object) a revision names, in full.</summary>
    private static async Task<string> RevParseAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("rev-parse", "--verify", revision)).StandardOutput.Trim();

    /// <summary>Whether a file exists in the git folder (<c>MERGE_HEAD</c>, <c>sequencer</c>, …).</summary>
    private static bool InGitDir(TempRepo repo, string name)
    {
        var path = Path.Combine(repo.Path, ".git", name);
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>No operation is in progress in the git folder.</summary>
    private static void AssertNoOperationFiles(TempRepo repo)
    {
        foreach (var name in new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply", "sequencer" })
        {
            Assert.False(InGitDir(repo, name), $"The git folder holds {name}.");
        }
    }

    /// <summary>A file's lines in the working tree, read as git wrote them.</summary>
    private static string[] FileLines(TempRepo repo, string path) =>
        File.ReadAllText(Path.Combine(repo.Path, path)).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    /// <summary>The git calls the app made since <paramref name="mark"/> (a count of <see cref="ShellDriver.GitCalls"/> read before the input), as their command lines.</summary>
    private static List<string> CallsSince(ShellDriver app, int mark) => [.. app.GitCalls().Skip(mark).Select(call => call.CommandText)];

    /// <summary>Asserts that the app made the call <paramref name="command"/> since <paramref name="mark"/>, and returns its record.</summary>
    private static VisualCommit.Core.Git.GitCallRecord AssertCalled(ShellDriver app, int mark, string command)
    {
        var call = app.GitCalls().Skip(mark).LastOrDefault(record => record.CommandText == command);
        Assert.True(call is not null, $"The app did not run \"{command}\". It ran:\n" + string.Join('\n', CallsSince(app, mark)));
        return call!;
    }

    /// <summary>Asserts the order of calls the app made since <paramref name="mark"/>: these commands, each after the one before.</summary>
    private static void AssertCalledInOrder(ShellDriver app, int mark, params string[] commands)
    {
        var calls = CallsSince(app, mark);
        var at = -1;
        foreach (var command in commands)
        {
            var next = calls.FindIndex(at + 1, call => call == command);
            Assert.True(next > at, $"The app did not run \"{command}\" after the calls before it. It ran:\n" + string.Join('\n', calls));
            at = next;
        }
    }

    /// <summary>The backup refs, parsed (<see cref="BackupRef"/>), with what each points at.</summary>
    private static async Task<List<(BackupRef Ref, string Name, string Sha)>> BackupsAsync(TempRepo repo) =>
        [.. (await repo.BackupRefsAsync()).Select(entry => (BackupRef.Parse(entry.Name), entry.Name, entry.Sha))];

    /// <summary>The working tree's folder name: the tab's and the repository's name.</summary>
    private static string NameOf(TempRepo repo) => Path.GetFileName(repo.Path);

    /// <summary>Waits until the queue logged the end of <paramref name="title"/> since <paramref name="mark"/>, and the tab is idle.</summary>
    private static async Task WaitForOperationAsync(ShellDriver app, TempRepo repo, string title, int mark)
    {
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded(NameOf(repo), title), mark: mark, timeout: TimeSpan.FromSeconds(30));
        await app.WaitForIdleAsync();
    }

    [GeneratedRegex(@"^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex FullSha();
}
