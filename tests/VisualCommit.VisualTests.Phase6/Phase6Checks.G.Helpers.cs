// Part G's own helpers (docs/test-reports/phase-6.md, "Conventions of part G"): the left panel as
// the report writes it, the selection, the menus part G opens and their keys, the end of an
// operation and its error dialog, the call log read by operation ("in this order", "as a set",
// "no write"), and git's state read back with plain git. Only part G's writer edits this file;
// what every part shares is in Phase6Checks.Support.cs.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksG
{
    /// <summary>The active tab's repository.</summary>
    private static RepositoryViewModel Repo(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    // ---- The left panel ----

    /// <summary>
    /// The left panel's rows as the report writes them (<see cref="PartG.Ig0Panel"/>): a section as
    /// "&lt;title&gt; &lt;count&gt;" (the Remotes section counts remote branches, GD16); below it each
    /// row indented two spaces per level, a remote or folder with " (open)" or " (closed)", the
    /// local branch HEAD is on with " (accented)" (its name drawn in the accent colour, which this
    /// asserts), and a local branch's ahead and behind counts as " ↑a" and " ↓b" above 0.
    /// </summary>
    private static List<string> LeftPanelLines(ShellDriver app)
    {
        app.Settle();
        var palette = PaletteOf(app);
        var list = app.Find<ItemsControl>("RefList");
        var lines = new List<string>();
        foreach (var row in app.Session.Shell.ActiveTab.LeftPanel.Rows)
        {
            switch (row)
            {
                case RefSectionRow section:
                    lines.Add(string.Create(CultureInfo.InvariantCulture, $"{section.Text} {section.Count}"));
                    break;
                case RefItemRow item:
                    var text = item.IsContainer
                        ? item.Text + (item.IsOpen ? " (open)" : " (closed)")
                        : item.Kind == RefItemKind.LocalBranch ? PartG.PanelBranch(item.Text, item.IsHead, item.Ahead, item.Behind)
                        : item.Text;
                    lines.Add(new string(' ', 2 * item.Depth) + text);
                    if (item.IsHead)
                    {
                        var button = list.GetVisualDescendants().OfType<Button>().FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, item))
                            ?? throw new Xunit.Sdk.XunitException($"The left panel's row of {item.Text}, HEAD's branch, is not laid out.");
                        var name = button.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "RefName");
                        var colour = (name.Foreground as ISolidColorBrush)?.Color;
                        Assert.True(colour == palette.Accent, $"The left panel's {item.Text}, HEAD's branch, is drawn in {colour}, not the accent colour {palette.Accent}.");
                    }

                    break;
            }
        }

        return lines;
    }

    /// <summary>Asserts the left panel as the report writes it (<see cref="LeftPanelLines"/>).</summary>
    private static void AssertLeftPanel(ShellDriver app, IReadOnlyList<string> expected)
    {
        var actual = LeftPanelLines(app);
        Assert.True(
            actual.SequenceEqual(expected),
            "The left panel is not the expected one.\nExpected:\n  " + string.Join("\n  ", expected) + "\nActual:\n  " + string.Join("\n  ", actual));
    }

    /// <summary>Opens a closed folder of the left panel by a click on its row (the conventions' "Left panel"), and waits until it shows open.</summary>
    private static async Task OpenFolderAsync(ShellDriver app, params string[] path)
    {
        var row = app.RefRowModel(path);
        Assert.True(row is RefItemRow { IsContainer: true, IsOpen: false }, $"{string.Join(" › ", path)} is no closed folder.");
        app.Click(await app.RefRowAsync(path));
        await app.WaitForAsync(() => app.RefRowModel(path).IsOpen, $"the folder {string.Join(" › ", path)} to open");
    }

    /// <summary>The keys of the left panel's selected rows.</summary>
    private static List<string> SelectedRefRows(ShellDriver app)
    {
        app.Settle();
        return [.. app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).Select(row => row.Key)];
    }

    // ---- The graph's selection and labels ----

    /// <summary>Nothing is selected in the graph: no commit, not the working-changes row, and the details panel shows its placeholder.</summary>
    private static void AssertNothingSelected(ShellDriver app)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.True(repository.Selection.IsEmpty, $"Commits are selected: {repository.Selection}.");
        Assert.Equal(-1, repository.SelectedIndex);
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
        var placeholder = (TextBlock)app.FindByAutomationId("DetailsPlaceholder");
        Assert.True(placeholder.IsEffectivelyVisible, "The details panel does not show its placeholder.");
    }

    /// <summary>Commit row <paramref name="row"/> alone is selected (the graph's selection and the view model's), and the details panel shows it.</summary>
    private static async Task AssertRowSelectedAsync(ShellDriver app, int row, string subject)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.Equal(row, repository.SelectedIndex);
        Assert.Equal(repository.Graph.CommitAt(row).Sha, repository.Selection.Single);
        Assert.Equal(subject, repository.Graph.CommitAt(row).Subject);
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
        Assert.Equal(row, Graph(app).SelectedIndex);
        await app.WaitForAsync(
            () => DetailsSubject(app) is { } text && text.IsEffectivelyVisible && text.Text == subject,
            $"the details of \"{subject}\", the selected commit");
    }

    /// <summary>The details panel's subject (<c>DetailsSubject</c>), or null while the panel has none in the window (it is still reading the commit).</summary>
    private static SelectableTextBlock? DetailsSubject(ShellDriver app)
    {
        try
        {
            return app.FindByAutomationId("DetailsSubject") as SelectableTextBlock;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The labels the graph draws in the given rows (<c>LabelsInRow</c>), as their drawn texts: a name, or a "+N".</summary>
    private static void AssertDrawnLabels(ShellDriver app, params (int Row, string[] Drawn)[] rows)
    {
        var graph = Graph(app);
        foreach (var (row, drawn) in rows)
        {
            Assert.Equal(drawn, graph.LabelsInRow(row).Select(label => label.Text));
        }
    }

    // ---- Menus ----

    /// <summary>
    /// Right-clicks commit row <paramref name="row"/> in its Message column, at the point the bounds
    /// give, first held against the one the report states (±2, D122's rule), and returns the menu.
    /// </summary>
    private static MenuSnapshot RightClickCommit(ShellDriver app, int row, Point reported)
    {
        var point = app.GraphRowPoint(row);
        Assert.True(
            Math.Abs(reported.X - point.X) <= 2 && Math.Abs(reported.Y - point.Y) <= 2,
            string.Create(CultureInfo.InvariantCulture, $"Commit row {row}'s Message column is at {point}; the report says {reported} (±2)."));
        app.RightClick(point);
        return app.Menu();
    }

    /// <summary>Right-clicks a left-panel row, found by its path, and returns the menu it opens and where the pointer was.</summary>
    private static async Task<(MenuSnapshot Menu, Point Pointer)> RightClickRefAsync(ShellDriver app, params string[] path)
    {
        var pointer = app.BoundsOf(await app.RefRowAsync(path)).Center;
        app.RightClick(pointer);
        return (app.Menu(), pointer);
    }

    /// <summary>Asserts the menu's header and that <paramref name="item"/> is in it, enabled and without a reason (part G asserts only the item it clicks, its conventions' "Menus").</summary>
    private static void AssertItemEnabled(MenuSnapshot menu, string header, string item)
    {
        Assert.Equal(header, menu.Header);
        var found = menu.Item(item);
        Assert.True(found.IsEnabled, $"The menu item \"{item}\" is disabled: \"{found.ToolTip}\".");
        Assert.Null(found.ToolTip);
    }

    /// <summary>
    /// A menu compared whole (the conventions' "Menus", <c>MenuSnapshot</c>): its lines, the
    /// highlighted item (the first enabled one, D105), its height by 4a's formula (±2) and the
    /// report's number, at least 4 px inside the window, and placed by the pointer that opened it
    /// (<see cref="AssertMenuPlaced"/>).
    /// </summary>
    private static void AssertWholeMenu(ShellDriver app, MenuSnapshot menu, IReadOnlyList<string> lines, string highlighted, double height, Point pointer)
    {
        AssertMenu(menu, [.. lines]);
        AssertHighlighted(menu, highlighted);
        var (items, separators) = PartG.MenuCounts(lines);
        Assert.Equal(height, MenuHeight(items, separators));
        AssertMenuShape(app, menu);
        AssertMenuPlaced(app, menu, pointer);
    }

    /// <summary>
    /// Phase 3's 4a (the conventions' "Menus"): a menu opened at <paramref name="pointer"/> has its
    /// top-left 2 right of and 2 below it, flipped to the pointer's other side where it would pass
    /// the window's right or bottom edge, and moved to stay at least 4 px inside the window (±2).
    /// </summary>
    private static void AssertMenuPlaced(ShellDriver app, MenuSnapshot menu, Point pointer)
    {
        var window = app.Window.ClientSize;
        var size = menu.Bounds.Size;
        var gap = WindowLayout.MenuPointerGap;
        var inset = WindowLayout.MenuEdgeInset;
        var x = pointer.X + gap + size.Width > window.Width ? pointer.X - gap - size.Width : pointer.X + gap;
        var y = pointer.Y + gap + size.Height > window.Height ? pointer.Y - gap - size.Height : pointer.Y + gap;
        x = Math.Max(Math.Min(x, window.Width - inset - size.Width), inset);
        y = Math.Max(Math.Min(y, window.Height - inset - size.Height), inset);
        Assert.True(
            Math.Abs(menu.Bounds.X - x) <= 2 && Math.Abs(menu.Bounds.Y - y) <= 2,
            string.Create(CultureInfo.InvariantCulture, $"The menu's top-left is {menu.Bounds.TopLeft}; 4a places a {size} menu opened at {pointer} at ({x}, {y})."));
    }

    /// <summary>
    /// The open menu's item <paramref name="label"/> shows its key at its right (design 6.7): its
    /// <c>AutomationProperties.AcceleratorKey</c> is <paramref name="key"/>, and its
    /// <c>ContextMenuItemKey</c> text block shows it in the secondary colour.
    /// </summary>
    private static void AssertMenuKey(ShellDriver app, string label, string key)
    {
        var item = app.MenuItemControl(label);
        Assert.True(
            AutomationProperties.GetAcceleratorKey(item) == key,
            $"The menu item \"{label}\" has the accelerator key \"{AutomationProperties.GetAcceleratorKey(item)}\", not \"{key}\" (design 6.7).");
        var drawn = item.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(block => AutomationProperties.GetAutomationId(block) == "ContextMenuItemKey" && block.IsEffectivelyVisible);
        Assert.True(drawn is not null, $"The menu item \"{label}\" draws no key at its right (ContextMenuItemKey, design 6.7).");
        Assert.Equal(key, drawn!.Text);
        Assert.Equal(PaletteOf(app).TextSecondary, (drawn.Foreground as ISolidColorBrush)?.Color);
    }

    /// <summary>A toolbar button's tooltip and help text (<c>ToolTip.GetTip</c>, no hover), and that it is enabled.</summary>
    private static void AssertEnabledToolTip(ShellDriver app, string automationId, string text)
    {
        var button = ToolbarButton(app, automationId);
        Assert.True(button.IsEffectivelyEnabled, $"{automationId} is disabled: \"{ToolTip.GetTip(button)}\".");
        Assert.Equal(text, ToolTip.GetTip(button) as string);
        Assert.Equal(text, AutomationProperties.GetHelpText(button));
    }

    // ---- Drags ----

    /// <summary>
    /// Phase 3's left-panel drag (check 7, step 5; the conventions' "Drags"): press at the centre of
    /// the row <paramref name="from"/>, move to the centre of <paramref name="onto"/> in 5 equal steps
    /// with the button held, the first step more than 4 px from the press, release. Returns the
    /// release point. The press selects nothing in the graph.
    /// </summary>
    private static async Task<Point> DragRowAsync(ShellDriver app, string[] from, string[] onto)
    {
        var start = app.BoundsOf(await app.RefRowAsync(from)).Center;
        var end = app.BoundsOf(await app.RefRowAsync(onto)).Center;
        Assert.True((new Vector(end.X - start.X, end.Y - start.Y) / 5).Length > 4, "The drag's first step is not more than 4 px from the press.");
        app.PressMouse(start);
        app.MoveMouseHeld(end, steps: 5);
        app.ReleaseMouse(end);
        return end;
    }

    /// <summary>After a drop: no drag runs, and the drag layer draws no pill and no outline.</summary>
    private static void AssertNoDragDrawn(ShellDriver app)
    {
        app.Settle();
        Assert.False(app.BranchDrag.IsDragging, "A drag still runs after the release.");
        var layer = app.Find<DragLayer>("DragLayer");
        Assert.Null(layer.PillBounds);
        Assert.Null(layer.OutlineBounds);
    }

    // ---- Operations and the error dialog ----

    /// <summary>
    /// Waits until the operation <paramref name="title"/> started since <paramref name="mark"/>
    /// failed (the queue's log line with "Failed", then the refresh) and its error dialog is open.
    /// </summary>
    private static async Task WaitForFailureAsync(ShellDriver app, TempRepo repo, string title, int mark, TimeSpan? timeout = null)
    {
        await WaitForOperationAsync(app, repo, title, mark, timeout);
        AssertOperationLine(app, repo, mark, title, OperationOutcome.Failed);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
    }

    /// <summary>
    /// Waits until the operation <paramref name="title"/> started since <paramref name="mark"/>
    /// succeeded (the queue's log line with "Succeeded", then the refresh), and no dialog is open.
    /// </summary>
    private static async Task WaitForSuccessAsync(ShellDriver app, TempRepo repo, string title, int mark, TimeSpan? timeout = null)
    {
        await WaitForOperationAsync(app, repo, title, mark, timeout);
        AssertOperationLine(app, repo, mark, title, OperationOutcome.Succeeded);
        await AssertNoDialogAsync(app);
    }

    /// <summary>The queue's one line of the operation <paramref name="title"/>'s end since <paramref name="mark"/>, with that outcome.</summary>
    private static void AssertOperationLine(ShellDriver app, TempRepo repo, int mark, string title, OperationOutcome outcome)
    {
        var pattern = AppLogFile.OperationEnded(NameOf(repo), title, outcome);
        var lines = app.LogLinesSince(mark).Where(line => pattern.IsMatch(line)).ToList();
        Assert.True(lines.Count == 1, $"Expected one log line like /{pattern}/; found {lines.Count}. The log since the input:\n" + string.Join('\n', app.LogLinesSince(mark).Where(line => line.Contains(title, StringComparison.Ordinal))));
    }

    /// <summary>Clicks the dialog's "Close" and waits until no dialog is open.</summary>
    private static async Task CloseDialogAsync(ShellDriver app)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var close = view.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && button.Content as string == "Close");
        app.Click(close);
        await AssertNoDialogAsync(app);
    }

    /// <summary>The toolbar's Undo and Redo while nothing is recorded: <c>Nothing to undo</c> (phase 5 design 7.2: a refused or failed operation records nothing).</summary>
    private static void AssertNothingToUndo(ShellDriver app) => Assert.Equal(UndoRedoButtons.Nothing, app.UndoRedoState());

    /// <summary>The toolbar's Undo is enabled (an operation was recorded).</summary>
    private static void AssertUndoEnabled(ShellDriver app)
    {
        var state = app.UndoRedoState();
        Assert.True(state.UndoEnabled, $"Undo is disabled: \"{state.UndoToolTip}\".");
    }

    // ---- The call log ----

    /// <summary>The git calls the app made since <paramref name="mark"/> (a count of <see cref="ShellDriver.GitCalls"/> read before the input), oldest first.</summary>
    private static List<GitCallRecord> Since(ShellDriver app, int mark) => [.. app.GitCalls().Skip(mark)];

    /// <summary>A read as the report gives it, against the call log's record: its exit code and its output (<see cref="PartG.Normalised"/>), the same on every git.</summary>
    private static void AssertRead(GitCallRecord call, ExpectedRead expected)
    {
        Assert.True(
            call.ExitCode == expected.ExitCode,
            $"\"{call.CommandText}\" exited {call.ExitCode}; the report says {expected.ExitCode}.\n{call.StandardError}");
        Assert.True(
            PartG.Normalised(call.StandardOutput) == expected.Output,
            $"\"{call.CommandText}\" printed {Shown(call.StandardOutput)}; the report says {Shown(expected.Output)}.");
    }

    /// <summary>An output with its NULs and control characters shown, for a failure's message.</summary>
    private static string Shown(string output) =>
        "\"" + string.Concat(output.Select(character => character switch
        {
            '\0' => "\\0",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            < ' ' => string.Create(CultureInfo.InvariantCulture, $"\\x{(int)character:x2}"),
            _ => character.ToString(),
        })) + "\"";

    /// <summary>
    /// The calls of one operation (<c>GitCallRecord.OperationId</c>) since <paramref name="mark"/>,
    /// oldest first: that of the first call since the mark whose command is
    /// <paramref name="firstCommand"/> and that belongs to an operation (the repository watcher's
    /// reads belong to none, the conventions' "The call log").
    /// </summary>
    private static List<GitCallRecord> OperationCalls(ShellDriver app, int mark, string firstCommand)
    {
        var calls = Since(app, mark);
        var first = calls.FirstOrDefault(call => call.CommandText == firstCommand && call.OperationId is not null)
            ?? throw new Xunit.Sdk.XunitException($"No operation ran \"{firstCommand}\" since the step began. The calls:\n" + string.Join('\n', calls.Select(Described)));
        return [.. calls.Where(call => call.OperationId == first.OperationId)];
    }

    /// <summary>A call as a failure's message shows it: its command, exit code and operation.</summary>
    private static string Described(GitCallRecord call) =>
        string.Create(CultureInfo.InvariantCulture, $"{call.CommandText} (exit {call.ExitCode}, operation {call.OperationId ?? "none"})");

    /// <summary>
    /// Asserts that <paramref name="calls"/> hold <paramref name="reads"/> in this order (others
    /// between them allowed), each with its exit code and output, after index <paramref name="after"/>;
    /// returns the index of the last.
    /// </summary>
    private static int AssertReadsInOrder(IReadOnlyList<GitCallRecord> calls, int after, params ExpectedRead[] reads)
    {
        var at = after;
        foreach (var read in reads)
        {
            var next = -1;
            for (var index = at + 1; index < calls.Count; index++)
            {
                if (calls[index].CommandText == read.Command)
                {
                    next = index;
                    break;
                }
            }

            Assert.True(next > at, $"The app did not run \"{read.Command}\" after the calls before it. It ran:\n" + string.Join('\n', calls.Select(Described)));
            AssertRead(calls[next], read);
            at = next;
        }

        return at;
    }

    /// <summary>
    /// Asserts that <paramref name="calls"/> hold each of <paramref name="reads"/> once after index
    /// <paramref name="after"/>, in any order (reads the app runs side by side, GD18), each with its
    /// exit code and output; returns the index of the last of them.
    /// </summary>
    private static int AssertReadsAsSet(IReadOnlyList<GitCallRecord> calls, int after, params ExpectedRead[] reads)
    {
        var last = after;
        foreach (var read in reads)
        {
            var found = Enumerable.Range(after + 1, Math.Max(0, calls.Count - after - 1)).Where(index => calls[index].CommandText == read.Command).ToList();
            Assert.True(found.Count == 1, $"The app ran \"{read.Command}\" {found.Count} times after the calls before it, not once. It ran:\n" + string.Join('\n', calls.Select(Described)));
            AssertRead(calls[found[0]], read);
            last = Math.Max(last, found[0]);
        }

        return last;
    }

    /// <summary>The index of the one call of <paramref name="calls"/> with this command, after <paramref name="after"/>.</summary>
    private static int IndexOf(IReadOnlyList<GitCallRecord> calls, string command, int after = -1)
    {
        var index = -1;
        for (var at = after + 1; at < calls.Count; at++)
        {
            if (calls[at].CommandText == command)
            {
                index = at;
                break;
            }
        }

        Assert.True(index >= 0, $"The app did not run \"{command}\". It ran:\n" + string.Join('\n', calls.Select(Described)));
        return index;
    }

    /// <summary>
    /// "No write" (part G's "The call log"): no call since <paramref name="mark"/> whose command
    /// starts with one of <see cref="PartG.WritePrefixes"/>, and the backup refs as they were.
    /// </summary>
    private static async Task AssertNoWriteAsync(ShellDriver app, TempRepo repo, int mark, IReadOnlyList<RefEntry> backups)
    {
        var writes = Since(app, mark).Where(call => PartG.WritePrefixes.Any(prefix => call.CommandText.StartsWith(prefix, StringComparison.Ordinal))).ToList();
        Assert.True(writes.Count == 0, "The app wrote:\n" + string.Join('\n', writes.Select(Described)));
        Assert.Equal(backups, await repo.BackupRefsAsync());
    }

    /// <summary>No <c>git stash push</c>, <c>pop</c> or <c>apply</c> since <paramref name="mark"/>.</summary>
    private static void AssertNoStashCall(ShellDriver app, int mark)
    {
        var calls = app.StashCallsSince(mark);
        Assert.True(calls.Count == 0, "The app ran stash calls:\n" + string.Join('\n', calls.Select(call => call.CommandText)));
    }

    /// <summary>The lines git printed on a stream as the call log recorded it: split at every carriage return and line feed, without trailing spaces and empty lines.</summary>
    private static List<string> Printed(string output) =>
        [.. output.Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];

    /// <summary>
    /// A stream of git's own text (TG1 to TG12): on Git 2.36 (the report's), its lines word for
    /// word; on every git, <paramref name="key"/> among them when given (phase 3's rule).
    /// </summary>
    private static async Task AssertGitPrintedAsync(string stream, IReadOnlyList<string> expected, string? key, string what)
    {
        var actual = Printed(stream);
        if (key is not null)
        {
            Assert.True(actual.Contains(key), $"{what} lacks the key line \"{key}\". It printed:\n  {string.Join("\n  ", actual)}");
        }

        if (await IsReportGitAsync())
        {
            Assert.True(actual.SequenceEqual(expected), $"{what} printed:\n  {string.Join("\n  ", actual)}\nThe report says (Git 2.36):\n  {string.Join("\n  ", expected)}");
        }
    }

    /// <summary>
    /// A checkout git refused for the ignored file in its way (G22): since <paramref name="mark"/>
    /// the call <paramref name="command"/> once, exit 1, its standard output empty and its standard
    /// error <paramref name="refusal"/> (on 2.36; its key line on every git); then, in its
    /// operation, <c>git check-ignore --stdin -z</c> exit 0 with <paramref name="ignored"/>; and no
    /// stash call (a branch, or a commit the branch does not reach, is no automatic stash's target).
    /// </summary>
    private static async Task AssertRefusedCheckoutCallsAsync(ShellDriver app, int mark, string command, IReadOnlyList<string> refusal, string key, string ignored)
    {
        var calls = Since(app, mark);
        var switches = calls.Where(call => call.CommandText == command).ToList();
        Assert.True(switches.Count == 1, $"The app ran \"{command}\" {switches.Count} times, not once. It ran:\n" + string.Join('\n', calls.Select(Described)));
        var refused = switches[0];
        Assert.True(refused.ExitCode == 1, $"\"{command}\" exited {refused.ExitCode}; the report says 1.\n{refused.StandardError}");
        await AssertGitPrintedAsync(refused.StandardOutput, [], null, $"\"{command}\"'s standard output");
        await AssertGitPrintedAsync(refused.StandardError, refusal, key, $"\"{command}\"'s standard error");
        var operation = calls.Where(call => call.OperationId == refused.OperationId && refused.OperationId is not null).ToList();
        var at = operation.IndexOf(refused);
        AssertReadsInOrder(operation, at, new ExpectedRead(PartG.CheckIgnoreCommand, 0, ignored));
        AssertNoStashCall(app, mark);
    }

    /// <summary>
    /// No call of <c>git check-ignore</c> in the operation of the call <paramref name="command"/>
    /// since <paramref name="mark"/> (its <c>OperationId</c>), nor between the mark and that call
    /// (part G's "No <c>git check-ignore</c>": the watcher's own reads, outside every operation,
    /// may come after it).
    /// </summary>
    private static void AssertNoCheckIgnore(ShellDriver app, int mark, string command)
    {
        var calls = Since(app, mark);
        var at = IndexOf(calls, command);
        var operation = calls[at].OperationId;
        var asked = calls.Where((call, index) => call.CommandText == PartG.CheckIgnoreCommand && (index < at || (operation is not null && call.OperationId == operation))).ToList();
        Assert.True(asked.Count == 0, $"The app asked git check-ignore before or in \"{command}\":\n" + string.Join('\n', asked.Select(Described)));
    }

    // ---- Changes made outside the app ----

    /// <summary>
    /// A write of <c>.git/config</c> outside the app while it runs (part G's "Outside the app", GD22):
    /// the call log marked, <c>git config</c> run through the copy as a terminal runs it, then a wait
    /// (<see cref="Phase6Checks.OutsideChangeTimeout"/>) for the app's next refs read (the call
    /// <c>git config --null --get-regexp ^remote\.</c> after the mark) and until it is idle.
    /// </summary>
    private static async Task ConfigOutsideAsync(ShellDriver app, TempRepo repo, params string[] arguments)
    {
        var calls = app.GitCalls().Count;
        await repo.GitAsync(["config", .. arguments]);
        await app.WaitForAsync(
            () => app.GitCalls().Skip(calls).Any(call => call.CommandText == PartG.RemotesReadCommand),
            "the app's refs read after the outside write of .git/config",
            OutsideChangeTimeout);
        await app.WaitForIdleAsync(timeout: OutsideChangeTimeout);
    }

    // ---- Git, read back ----

    /// <summary>Plain git's output as lines, line ends as line feeds (none for an empty output).</summary>
    private static async Task<string[]> GitLinesAsync(TempRepo repo, params string[] arguments)
    {
        var text = PartG.Normalised((await repo.GitAsync(arguments)).StandardOutput);
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary><c>git status --porcelain=v1 --untracked-files=all --ignored</c>, as lines.</summary>
    private static Task<string[]> IgnoredStatusAsync(TempRepo repo) =>
        GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all", "--ignored");

    /// <summary>HEAD's reflog, newest first, as <c>git log -g --format=%gs HEAD</c> lists it.</summary>
    private static Task<string[]> ReflogAsync(TempRepo repo) => GitLinesAsync(repo, "log", "-g", "--format=%gs", "HEAD");

    /// <summary>Every ref and what it points at, as <c>git for-each-ref</c> lists them (the backups included).</summary>
    private static Task<string[]> RefsAsync(TempRepo repo) => GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)");

    /// <summary>A remote repository's refs (<c>git -C ../&lt;name&gt;.git for-each-ref</c>).</summary>
    private static Task<string[]> RemoteRefsAsync(TempRepo repo, string remote) =>
        GitLinesAsync(repo, "-C", repo.SiblingPath(remote + ".git"), "for-each-ref", "--format=%(refname) %(objectname)");

    /// <summary>The branch keys (<c>git config --get-regexp ^branch\.</c>), none when git finds none (exit 1).</summary>
    private static async Task<string[]> BranchKeysAsync(TempRepo repo, string pattern = @"^branch\.")
    {
        var result = await repo.GitAllowingFailureAsync("config", "--get-regexp", pattern);
        Assert.True(result.ExitCode is 0 or 1, $"git config --get-regexp {pattern} exited {result.ExitCode}.\n{result.StandardError}");
        var text = PartG.Normalised(result.StandardOutput);
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary>The branch HEAD is on (<c>git symbolic-ref -q HEAD</c>), or null while it is detached.</summary>
    private static async Task<string?> SymbolicHeadAsync(TempRepo repo)
    {
        var result = await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD");
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>What a revision names (<c>git rev-parse --verify --quiet</c>), or null when it names nothing.</summary>
    private static async Task<string?> RevParseAsync(TempRepo repo, string revision)
    {
        var result = await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", revision);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>The header lines of <c>git status --porcelain=v2 --branch</c>.</summary>
    private static async Task<string[]> StatusBranchLinesAsync(TempRepo repo) =>
        [.. (await GitLinesAsync(repo, "status", "--porcelain=v2", "--branch")).Where(line => line.StartsWith("# ", StringComparison.Ordinal))];

    /// <summary>A working-tree file's text, as git wrote it (line ends as line feeds).</summary>
    private static string WorkingFile(TempRepo repo, string path) =>
        File.ReadAllText(Path.Combine(repo.Path, path)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Whether a file or folder is in the working tree.</summary>
    private static bool InWorkingTree(TempRepo repo, string path) => Path.Exists(Path.Combine(repo.Path, path));

    /// <summary>No operation is in progress: none of <c>MERGE_HEAD</c>, <c>CHERRY_PICK_HEAD</c>, <c>REVERT_HEAD</c>, <c>sequencer/</c>, <c>rebase-merge/</c> and <c>rebase-apply/</c> in the git folder.</summary>
    private static void AssertNothingInProgress(TempRepo repo)
    {
        foreach (var name in new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "sequencer", "rebase-merge", "rebase-apply" })
        {
            Assert.False(Path.Exists(Path.Combine(repo.Path, ".git", name)), $"The git folder holds {name}.");
        }
    }

    /// <summary>HEAD is on <paramref name="branch"/> at <paramref name="sha"/>.</summary>
    private static async Task AssertOnBranchAsync(TempRepo repo, string branch, string sha)
    {
        Assert.Equal("refs/heads/" + branch, await SymbolicHeadAsync(repo));
        Assert.Equal(sha, await repo.HeadAsync());
    }

    /// <summary>No backup ref (<c>git for-each-ref refs/visualcommit/backup</c> prints nothing).</summary>
    private static async Task AssertNoBackupAsync(TempRepo repo) => Assert.Empty(await repo.BackupRefsAsync());
}
