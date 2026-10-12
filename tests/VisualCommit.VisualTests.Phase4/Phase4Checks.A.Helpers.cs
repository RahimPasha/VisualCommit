// Part A's own helpers (docs/test-reports/phase-4.md, part A): the left panel and the selection in
// the report's notation, a menu as "Menus" writes it with its look, drags, the details panel, the
// stage panel, the toolbar, and git's state and git's own lines read back. Only part A's writer
// edits this file; what every part shares is in Phase4Checks.Support.cs.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksA
{
    /// <summary>The active tab's repository.</summary>
    private static RepositoryViewModel Repo(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    // ---- The left panel ----

    /// <summary>
    /// The left panel in part A's notation: a section <c>▾ Title N</c> (▸ when closed), a row
    /// indented two spaces per level, a container with ▾ or ▸, the head branch with
    /// <c> (head)</c>, then <c> ↑N</c> and <c> ↓N</c>, and <c> (selected)</c> for the row with the
    /// selection background.
    /// </summary>
    private static List<string> LeftPanelLines(ShellDriver app)
    {
        app.Settle();
        return [.. app.Session.Shell.ActiveTab.LeftPanel.Rows.Select(row => row switch
        {
            RefSectionRow section => $"{(section.IsOpen ? "▾" : "▸")} {section.Text} {section.CountText}",
            RefItemRow item => new string(' ', 2 * item.Depth)
                + (item.IsContainer ? (item.IsOpen ? "▾ " : "▸ ") : string.Empty)
                + item.Text
                + (item.IsHead ? " (head)" : string.Empty)
                + (item.Ahead > 0 ? " ↑" + item.AheadText : string.Empty)
                + (item.Behind > 0 ? " ↓" + item.BehindText : string.Empty)
                + (item.IsSelected ? " (selected)" : string.Empty),
            _ => row.Text,
        })];
    }

    /// <summary>
    /// Asserts the left panel: its first lines are <paramref name="top"/> (Local branches and
    /// Remotes as the report draws them), the sections after them are Pull requests 0, Tags 0 and
    /// Stashes 0, and the head branch's name (the row marked <c> (head)</c>) is drawn in the accent
    /// colour and every other branch's in the primary colour, with the arrows the lines give.
    /// </summary>
    private static async Task AssertLeftPanelAsync(ShellDriver app, IReadOnlyList<string> top)
    {
        var lines = LeftPanelLines(app);
        Assert.True(
            lines.Take(top.Count).SequenceEqual(top),
            "The left panel is not the expected one.\nExpected first:\n  " + string.Join("\n  ", top) + "\nActual:\n  " + string.Join("\n  ", lines));
        var sections = app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefSectionRow>().Select(row => (row.Text, row.Count)).ToList();
        Assert.Equal(PartA.RebaseEmptySections, sections.Skip(2));
        foreach (var branch in app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.Kind == RefItemKind.LocalBranch).ToList())
        {
            var button = await RefRowOfAsync(app, branch);
            AssertRefRowDrawn(app, button, branch);
        }
    }

    /// <summary>
    /// Asserts the left-panel row of the local branch at <paramref name="path"/> (under "Local
    /// branches"): its ahead and behind arrows (0 for none) as the model holds and the row draws
    /// them, and whether it is the branch HEAD is on, drawn in the accent colour (else the primary).
    /// </summary>
    private static async Task AssertLocalBranchAsync(ShellDriver app, int ahead, int behind, bool head, params string[] path)
    {
        var model = (RefItemRow)app.RefRowModel(["Local branches", .. path]);
        Assert.True(
            (model.Ahead, model.Behind, model.IsHead) == (ahead, behind, head),
            $"{string.Join(" › ", path)} shows ↑{model.Ahead} ↓{model.Behind}{(model.IsHead ? " (head)" : string.Empty)}; the report says ↑{ahead} ↓{behind}{(head ? " (head)" : string.Empty)}.");
        var button = await app.RefRowAsync(["Local branches", .. path]);
        AssertRefRowDrawn(app, button, model);
    }

    private static async Task<Control> RefRowOfAsync(ShellDriver app, RefItemRow row)
    {
        // The row's path: the section, then the folders above it, then its own text.
        var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        var index = rows.IndexOf(row);
        var path = new List<string> { row.Text };
        var depth = row.Depth;
        for (var i = index - 1; i >= 0 && depth > 0; i--)
        {
            if (rows[i] is RefSectionRow section && depth == 1)
            {
                path.Insert(0, section.Text);
                depth = 0;
            }
            else if (rows[i] is RefItemRow item && item.Depth == depth - 1)
            {
                path.Insert(0, item.Text);
                depth = item.Depth;
            }
        }

        return await app.RefRowAsync([.. path]);
    }

    private static void AssertRefRowDrawn(ShellDriver app, Control button, RefItemRow model)
    {
        var palette = PaletteOf(app);
        var name = button.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "RefName");
        var colour = (name.Foreground as ISolidColorBrush)?.Color;
        Assert.True(colour == (model.IsHead ? palette.Accent : palette.TextPrimary), $"{model.Text} is drawn in {colour}; HEAD is {(model.IsHead ? string.Empty : "not ")}on it.");
        Assert.Equal(model.Ahead > 0, button.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Name == "AheadCount" && block.IsEffectivelyVisible && block.Text == model.AheadText));
        Assert.Equal(model.Behind > 0, button.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Name == "BehindCount" && block.IsEffectivelyVisible && block.Text == model.BehindText));
    }

    /// <summary>Asserts that no left-panel row has the selection background: a right-click on one opens its menu only (D114).</summary>
    private static void AssertNoRefHighlighted(ShellDriver app)
    {
        app.Settle();
        var selected = app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).Select(row => row.Key).ToList();
        Assert.True(selected.Count == 0, "Left-panel rows are highlighted: " + string.Join(", ", selected));
        Assert.DoesNotContain(
            app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<Button>(),
            button => button.IsEffectivelyVisible && button.Classes.Contains("selected"));
    }

    // ---- The graph's selection and the details ----

    /// <summary>Nothing is selected: no commit, not the working-changes row, and the details panel shows its placeholder.</summary>
    private static void AssertNothingSelected(ShellDriver app)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.True(repository.Selection.IsEmpty, $"Commits are selected: {repository.Selection}.");
        Assert.Equal(-1, repository.SelectedIndex);
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
        var placeholder = (TextBlock)app.FindByAutomationId("DetailsPlaceholder");
        Assert.True(placeholder.IsEffectivelyVisible, "The details panel does not show its placeholder.");
        Assert.Equal(PartA.NothingSelectedPlaceholder, placeholder.Text);
    }

    /// <summary>Commit row <paramref name="row"/> alone is selected (the graph's selection and the view model's).</summary>
    private static void AssertRowSelected(ShellDriver app, int row)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.Equal(row, repository.SelectedIndex);
        Assert.Equal(repository.Graph.CommitAt(row).Sha, repository.Selection.Single);
        Assert.False(repository.IsWorkingRowSelected);
        Assert.Equal(row, Graph(app).SelectedIndex);
    }

    /// <summary>Exactly the commits of <paramref name="rows"/> are selected (several commits).</summary>
    private static void AssertRowsSelected(ShellDriver app, params int[] rows)
    {
        app.Settle();
        var repository = Repo(app);
        var expected = rows.Select(row => repository.Graph.CommitAt(row).Sha).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(expected, repository.Selection.Shas.Order(StringComparer.Ordinal).ToList());
        Assert.False(repository.IsWorkingRowSelected);
    }

    /// <summary>
    /// Asserts the multi-selection view (4h) in the right panel: "N commits selected" and the
    /// commits in row order as "short id subject".
    /// </summary>
    private static async Task AssertMultiSelectionAsync(ShellDriver app, params string[] items)
    {
        await app.WaitForAsync(() => app.FindByAutomationId("MultiSelectionView").IsEffectivelyVisible, "the multi-selection view");
        var view = app.FindByAutomationId("MultiSelectionView");
        Assert.Equal($"{items.Length.ToString(CultureInfo.InvariantCulture)} commits selected", ((TextBlock)app.FindByAutomationId(view, "MultiSelectionTitle")).Text);
        var rows = view.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "MultiSelectionItem")
            .OrderBy(control => app.BoundsOf(control).Y)
            .ToList();
        Assert.Equal(items, rows.Select(row => string.Join(' ', TextsIn(row))));
    }

    /// <summary>The visible control with this automation id, or null while none is in the window.</summary>
    private static Control? Shown(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId);

    /// <summary>Waits until the details panel shows the commit <paramref name="subject"/>, the selected one.</summary>
    private static async Task WaitForDetailsAsync(ShellDriver app, string subject)
    {
        await app.WaitForAsync(
            () => Shown(app, "DetailsSubject") is SelectableTextBlock text && text.Text == subject
                && Shown(app, "DetailsSha") is SelectableTextBlock sha
                && Graph(app).Selection.Shas is [var selected] && sha.Text == selected,
            $"the details of \"{subject}\", the selected commit");
        app.Settle();
    }

    /// <summary>
    /// Asserts the details of the selected commit: its subject, author, date, no committer row,
    /// the commit date row, the SHA, the parents' short ids in order and the changed files as
    /// "letter name folder".
    /// </summary>
    private static async Task AssertDetailsAsync(ShellDriver app, string subject, string date, string commitDate, string sha, string[] parents, params string[] files)
    {
        await WaitForDetailsAsync(app, subject);
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", ((SelectableTextBlock)app.FindByAutomationId("DetailsAuthor")).Text);
        Assert.Equal(date, ((TextBlock)app.FindByAutomationId("DetailsDate")).Text);
        Assert.False(app.FindByAutomationId("DetailsCommitter").IsEffectivelyVisible, "The details show a Committer row.");
        var commitDateBlock = (TextBlock)app.FindByAutomationId("DetailsCommitDate");
        Assert.True(commitDateBlock.IsEffectivelyVisible, "The details show no Commit date row.");
        Assert.Equal(commitDate, commitDateBlock.Text);
        Assert.Equal(sha, ((SelectableTextBlock)app.FindByAutomationId("DetailsSha")).Text);
        var links = app.Window.GetVisualDescendants().OfType<Control>()
            .Where(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == "ParentLink")
            .OrderBy(link => app.BoundsOf(link).X)
            .Select(link => AutomationProperties.GetName(link))
            .ToList();
        Assert.Equal(parents, links);
        Assert.Equal($"Changed files ({files.Length.ToString(CultureInfo.InvariantCulture)})", ((TextBlock)app.FindByAutomationId("ChangedFilesTitle")).Text);
        await app.WaitForAsync(() => FileRows(app, "FileList").SequenceEqual(files), $"the details' files {string.Join(", ", files)}");
    }

    // ---- Menus ----

    /// <summary>Right-clicks commit row <paramref name="row"/> in its Message column and returns the menu it opens and the point clicked.</summary>
    private static (MenuSnapshot Menu, Point Pointer) RightClickCommit(ShellDriver app, int row)
    {
        var pointer = app.GraphRowPoint(row);
        app.RightClick(pointer);
        return (app.Menu(), pointer);
    }

    /// <summary>Right-clicks a left-panel row, found by its path, and returns the menu it opens and the point clicked.</summary>
    private static async Task<(MenuSnapshot Menu, Point Pointer)> RightClickRefAsync(ShellDriver app, params string[] path)
    {
        var row = await app.RefRowAsync(path);
        var pointer = app.BoundsOf(row).Center;
        app.RightClick(pointer);
        return (app.Menu(), pointer);
    }

    /// <summary>
    /// Asserts an open menu as the report writes it: its lines (<see cref="AssertMenu"/>), the
    /// highlighted item (D105), the height the report gives (±2) and 4a's shape
    /// (<see cref="AssertMenuShape"/>), each item's text in the primary colour, an enabled
    /// destructive one in the danger colour (<c>VcDangerBrush</c>), a disabled one in the
    /// disabled colour (<c>VcTextDisabledBrush</c>), and no label cut. With a picture taken while
    /// the menu was open, the highlighted item's background in the hover colour.
    /// </summary>
    private static void AssertMenuAsReported(ShellDriver app, MenuSnapshot menu, string[] lines, string? highlighted, double height, Screenshot? picture = null)
    {
        AssertMenu(menu, lines);
        AssertHighlighted(menu, highlighted);
        Assert.True(
            Math.Abs(menu.Bounds.Height - height) <= WindowLayout.MenuHeightTolerance,
            string.Create(CultureInfo.InvariantCulture, $"The menu \"{menu.Header}\" is {menu.Bounds.Height} high; the report says {height}."));
        AssertMenuShape(app, menu);
        var palette = PaletteOf(app);
        foreach (var item in menu.Items)
        {
            var colour = !item.IsEnabled ? palette.TextDisabled : item.IsDestructive ? palette.Danger : palette.TextPrimary;
            Assert.True(item.TextColor == colour, $"The menu item \"{item.Label}\" is drawn in {item.TextColor}, expected {colour}.");
        }

        var presenter = app.OpenMenuView ?? throw new Xunit.Sdk.XunitException("No menu is open.");
        var labels = presenter.GetVisualDescendants().OfType<TrimmedTextBlock>().ToList();
        Assert.All(labels, label => Assert.False(label.IsTrimmed, $"The menu's label \"{label.Text}\" is cut."));
        if (picture is not null && menu.Highlighted is { } item2)
        {
            var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).Select(app.BoundsOf).ToList();
            var point = new[] { new Point(item2.Bounds.Right - 4, item2.Bounds.Center.Y), new Point(item2.Bounds.Left + 4, item2.Bounds.Center.Y) }
                .FirstOrDefault(candidate => !tips.Any(tip => tip.Contains(candidate)), new Point(item2.Bounds.Right - 4, item2.Bounds.Center.Y));
            AssertColour(palette.Hover, picture, point, $"the background of the highlighted item \"{item2.Label}\"");
        }
    }

    /// <summary>The open tooltip with <paramref name="text"/> lies inside the window.</summary>
    private static void AssertToolTipInsideWindow(ShellDriver app, string text)
    {
        var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).ToList();
        var tip = Assert.Single(tips, candidate => candidate.Content as string == text || (candidate.Content is Visual visual && TextsIn(visual).Contains(text)));
        var bounds = app.BoundsOf(tip);
        var size = app.Window.ClientSize;
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height, $"The tooltip at {bounds} is not inside the {size.Width}x{size.Height} window.");
    }

    /// <summary>Presses Esc and asserts that the menu closed and nothing opened.</summary>
    private static async Task CloseMenuAsync(ShellDriver app)
    {
        var mark = app.LogMark();
        app.PressKey(Avalonia.Input.Key.Escape);
        await AssertNoMenuAsync(app, mark);
    }

    // ---- Drags ----

    /// <summary>
    /// The report's drag: the left button pressed at <paramref name="from"/>, then 5 equal moves
    /// with it held to <paramref name="to"/> (the first more than 4 px from the press, which
    /// starts the drag), then the release; then the drop's menu, read after its facts.
    /// </summary>
    private static MenuSnapshot Drop(ShellDriver app, Point from, Point to)
    {
        app.PressMouse(from);
        app.MoveMouseHeld(to, steps: PartA.DragSteps);
        Assert.True(app.BranchDrag.IsDragging, "The moves did not start a drag.");
        app.ReleaseMouse(to);
        return app.Menu();
    }

    /// <summary>The one drop fact read since <paramref name="calls"/>: <c>git merge-base --is-ancestor &lt;B&gt; &lt;A&gt;</c>, exit 1 (B is not an ancestor of A).</summary>
    private static void AssertDropFactRead(ShellDriver app, int calls)
    {
        var reads = app.GitCalls().Skip(calls).Where(call => call.CommandText.StartsWith("git merge-base --is-ancestor ", StringComparison.Ordinal)).ToList();
        var read = Assert.Single(reads);
        Assert.True(read.ExitCode == 1, $"\"{read.CommandText}\" exited {read.ExitCode}; the report says 1.");
    }

    // ---- The stage panel and the toolbar ----

    /// <summary>Waits until the stage panel shows with these titles over its two lists.</summary>
    private static Task WaitForStagePanelAsync(ShellDriver app, string unstagedTitle, string stagedTitle) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                && app.Find<TextBlock>("UnstagedTitle").Text == unstagedTitle
                && app.Find<TextBlock>("StagedTitle").Text == stagedTitle,
            $"the stage panel with \"{unstagedTitle}\" and \"{stagedTitle}\"");

    /// <summary>A file list's rows as "letter name folder" (<c>UnstagedFileList</c>, <c>StagedFileList</c>, <c>FileList</c>).</summary>
    private static List<string> FileRows(ShellDriver app, string list)
    {
        app.Settle();
        var items = (ItemsControl)app.FindByAutomationId(list);
        return [.. ((IEnumerable<ChangedFileRow>)(items.ItemsSource ?? Array.Empty<ChangedFileRow>()))
            .Where(row => row.IsFile)
            .Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part))))];
    }

    /// <summary>The text a commit box holds (<c>CommitSummary</c>, <c>CommitDescription</c>).</summary>
    private static string CommitBoxText(ShellDriver app, string automationId) => ((TextBox)app.FindByAutomationId(automationId)).Text ?? string.Empty;

    /// <summary>A toolbar button's state: enabled, or disabled with its reason as its tooltip (shown while disabled).</summary>
    private static void AssertToolbarButton(ShellDriver app, string automationId, string? disabledReason)
    {
        var button = ToolbarButton(app, automationId);
        if (disabledReason is null)
        {
            Assert.True(button.IsEffectivelyEnabled, $"{automationId} is disabled: \"{ToolTip.GetTip(button)}\".");
            return;
        }

        Assert.False(button.IsEffectivelyEnabled, $"{automationId} is enabled.");
        AssertTip(button, disabledReason);
        Assert.True(ToolTip.GetShowOnDisabled(button), $"{automationId} does not show its reason while disabled.");
    }

    // ---- The status bar and the log ----

    /// <summary>The queue's line of the operation <paramref name="title"/>'s end since <paramref name="mark"/>, with that outcome and message (one such line).</summary>
    private static string AssertOperationLine(ShellDriver app, int mark, string repo, string title, Core.Operations.OperationOutcome outcome, string message)
    {
        var pattern = AppLogFile.OperationEnded(repo, title, outcome, message);
        var lines = app.LogLinesSince(mark).Where(line => pattern.IsMatch(line)).ToList();
        Assert.True(lines.Count == 1, $"Expected one log line like /{pattern}/; found {lines.Count}. The log since the input:\n" + string.Join('\n', app.LogLinesSince(mark).Where(line => line.Contains(title, StringComparison.Ordinal))));
        return lines[0];
    }

    /// <summary>No backup was removed since <paramref name="mark"/> (every operation of part A leaves at most three, section 9).</summary>
    private static void AssertNoBackupRemoved(ShellDriver app, int mark) =>
        Assert.DoesNotContain(app.LogLinesSince(mark), line => line.Contains("Removed backup", StringComparison.Ordinal));

    // ---- Git, read back ----

    /// <summary>
    /// The lines git printed on a stream as the call log recorded it: split at every carriage
    /// return and line feed (the runner's split), <c>ESC[K</c> removed, without trailing spaces and
    /// empty lines.
    /// </summary>
    private static List<string> Printed(string output) =>
        [.. output.Replace("\u001b[K", string.Empty, StringComparison.Ordinal).Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];

    /// <summary>On Git 2.36 (the report's), asserts the lines git printed on a stream word for word; on another git, nothing (the conventions' "Git's own texts").</summary>
    private static async Task AssertGitPrintedAsync(string stream, IReadOnlyList<string> expected, string what)
    {
        if (await IsReportGitAsync())
        {
            var actual = Printed(stream);
            Assert.True(actual.SequenceEqual(expected), $"{what} printed:\n  {string.Join("\n  ", actual)}\nThe report says (Git 2.36):\n  {string.Join("\n  ", expected)}");
        }
    }

    /// <summary>Asserts the call <paramref name="command"/> made since <paramref name="mark"/>: its exit code, and returns it.</summary>
    private static GitCallRecord AssertCall(ShellDriver app, int mark, string command, int exitCode)
    {
        var call = AssertCalled(app, mark, command);
        Assert.True(call.ExitCode == exitCode, $"\"{command}\" exited {call.ExitCode}; the report says {exitCode}.\n{call.StandardError}{call.StandardOutput}");
        return call;
    }

    /// <summary>Plain git's output, line ends as line feeds, without its last line feed.</summary>
    private static async Task<string> GitTextAsync(TempRepo repo, params string[] arguments) =>
        (await repo.GitAsync(arguments)).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>Plain git's output as lines (none for an empty output).</summary>
    private static async Task<string[]> GitLinesAsync(TempRepo repo, params string[] arguments)
    {
        var text = await GitTextAsync(repo, arguments);
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary>What a revision names (<c>git rev-parse --verify</c>), or null when it names nothing.</summary>
    private static async Task<string?> RevParseAsync(TempRepo repo, string revision)
    {
        var result = await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", revision);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>HEAD's reflog, newest first: its first <paramref name="count"/> messages.</summary>
    private static Task<string[]> ReflogAsync(TempRepo repo, int count) =>
        GitLinesAsync(repo, "log", "--walk-reflogs", $"-{count.ToString(CultureInfo.InvariantCulture)}", "--format=%gs", "HEAD");

    /// <summary>How many entries HEAD's reflog has.</summary>
    private static async Task<int> ReflogCountAsync(TempRepo repo) =>
        (await GitLinesAsync(repo, "log", "--walk-reflogs", "--format=%gs", "HEAD")).Length;

    /// <summary>A file or folder in the git folder.</summary>
    private static bool InGitDir(TempRepo repo, string name)
    {
        var path = Path.Combine(repo.Path, ".git", name);
        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>A file of the git folder, as git wrote it (line ends as line feeds).</summary>
    private static string GitDirFile(TempRepo repo, string name) =>
        File.ReadAllText(Path.Combine(repo.Path, ".git", name)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>A working-tree file's text, as git wrote it (line ends as line feeds).</summary>
    private static string WorkingFile(TempRepo repo, string path) =>
        File.ReadAllText(Path.Combine(repo.Path, path)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>No rebase is in progress: no <c>rebase-merge</c> or <c>rebase-apply</c> folder.</summary>
    private static void AssertNoRebaseFolder(TempRepo repo)
    {
        Assert.False(InGitDir(repo, "rebase-merge"), "The git folder holds rebase-merge.");
        Assert.False(InGitDir(repo, "rebase-apply"), "The git folder holds rebase-apply.");
    }

    /// <summary>Every ref and what it points at, as <c>git for-each-ref</c> lists them (the backups left out).</summary>
    private static async Task<List<string>> RefsAsync(TempRepo repo) =>
        [.. (await GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)")).Where(line => !line.StartsWith(BackupRef.Prefix, StringComparison.Ordinal))];

    /// <summary>
    /// Asserts a commit as git stores it: its subject (and no body), parent, author and committer
    /// (Test Author), author date (kept) and commit date (the app's, D72).
    /// </summary>
    private static async Task AssertCommitAsync(TempRepo repo, string sha, string subject, string parent, DateTimeOffset authorDate, DateTimeOffset commitDate)
    {
        Assert.Equal(subject, await GitTextAsync(repo, "log", "-1", "--format=%s", sha));
        Assert.Equal(string.Empty, (await GitTextAsync(repo, "log", "-1", "--format=%b", sha)).Trim());
        var fields = (await GitTextAsync(repo, "log", "-1", "--format=%P%x1f%an <%ae>%x1f%cn <%ce>%x1f%aI%x1f%cI", sha)).Split('\x1f');
        Assert.Equal(parent, fields[0]);
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[1]);
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[2]);
        AssertGitDate(authorDate, fields[3]);
        AssertGitDate(commitDate, fields[4]);
    }

    /// <summary>The bare origin next to the working tree: its branches, as <c>git ls-remote --heads origin</c> prints them (name and object).</summary>
    private static async Task<List<string>> OriginHeadsAsync(TempRepo repo) =>
        [.. (await GitLinesAsync(repo, "ls-remote", "--heads", "origin")).Select(line => line.Split('\t')).Select(fields => $"{fields[1]} {fields[0]}")];
}
