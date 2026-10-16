// Part P's own helpers (docs/test-reports/phase-5.md, "Conventions of part P"): the left panel and
// the selection as the report writes them, the menus part P opens, the error dialog's end, the
// stage panel, the toolbar, and git's state read back with plain git. Only part P's writer edits
// this file; what every part shares is in Phase5Checks.Support.cs.
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksP
{
    /// <summary>The active tab's repository.</summary>
    private static RepositoryViewModel Repo(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    // ---- The left panel ----

    /// <summary>
    /// Asserts the left panel as part P's graphs write it: the sections with their counts (Local
    /// branches 2, Remotes 0, Pull requests 0, Tags 1, Stashes <paramref name="stashes"/>), the
    /// local branches <c>main</c> and <c>other</c> in that order, the one HEAD is on
    /// (<paramref name="head"/>, null while HEAD is detached) drawn in the accent colour and the
    /// other in the primary colour, and the sections Tags and Stashes open (as every step leaves
    /// them) with the tag <c>v1.1</c> and the stashes <paramref name="stashRows"/>.
    /// </summary>
    private static async Task AssertLeftPanelAsync(ShellDriver app, int stashes, string? head, params string[] stashRows)
    {
        app.Settle();
        var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
        Assert.Equal(PartP.Sections(stashes), rows.OfType<RefSectionRow>().Select(row => (row.Text, row.Count)).ToArray());
        Assert.Equal(PartP.LocalBranches, ItemsOf(rows, RefItemKind.LocalBranch).Select(row => row.Text));
        Assert.Equal(
            PartP.LocalBranches.Select(branch => branch == head).ToList(),
            ItemsOf(rows, RefItemKind.LocalBranch).Select(row => row.IsHead).ToList());
        var palette = PaletteOf(app);
        foreach (var branch in PartP.LocalBranches)
        {
            var button = await app.RefRowAsync("Local branches", branch);
            var name = button.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "RefName");
            var colour = (name.Foreground as ISolidColorBrush)?.Color;
            var expected = branch == head ? palette.Accent : palette.TextPrimary;
            Assert.True(colour == expected, $"Local branches › {branch} is drawn in {colour}, expected {expected}.");
        }

        Assert.True(SectionOf(rows, "Tags").IsOpen, "The left panel's section Tags is closed.");
        Assert.Equal(PartP.Tags, ItemsOf(rows, RefItemKind.Tag).Select(row => row.Text));
        Assert.True(SectionOf(rows, "Stashes").IsOpen, "The left panel's section Stashes is closed.");
        Assert.Equal(stashRows, ItemsOf(rows, RefItemKind.Stash).Select(row => row.Text));
    }

    private static IEnumerable<RefItemRow> ItemsOf(IEnumerable<RefListRow> rows, RefItemKind kind) =>
        rows.OfType<RefItemRow>().Where(row => row.Kind == kind);

    private static RefSectionRow SectionOf(IEnumerable<RefListRow> rows, string title) =>
        rows.OfType<RefSectionRow>().Single(row => row.Text == title);

    /// <summary>Local branches › <paramref name="branch"/> is the left panel's selected row: a double-click's first click selects it (phase 3's 4c).</summary>
    private static void AssertBranchRowSelected(ShellDriver app, string branch)
    {
        var row = app.RefRowModel("Local branches", branch);
        Assert.True(row is RefItemRow { IsSelected: true }, $"Local branches › {branch} is not selected in the left panel.");
        Assert.Equal([row.Key], SelectedRefRows(app));
    }

    /// <summary>The keys of the left panel's selected rows.</summary>
    private static List<string> SelectedRefRows(ShellDriver app)
    {
        app.Settle();
        return [.. app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefItemRow>().Where(row => row.IsSelected).Select(row => row.Key)];
    }

    // ---- The graph's selection and the details ----

    /// <summary>Nothing is selected: no commit, not the working-changes row, and the details panel shows its placeholder (phase 3's).</summary>
    private static void AssertNothingSelected(ShellDriver app)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.True(repository.Selection.IsEmpty, $"Commits are selected: {repository.Selection}.");
        Assert.Equal(-1, repository.SelectedIndex);
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
        var placeholder = (TextBlock)app.FindByAutomationId("DetailsPlaceholder");
        Assert.True(placeholder.IsEffectivelyVisible, "The details panel does not show its placeholder.");
        Assert.Equal(PartP.NothingSelectedPlaceholder, placeholder.Text);
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
            () => app.FindByAutomationId("DetailsSubject") is SelectableTextBlock text && text.IsEffectivelyVisible && text.Text == subject,
            $"the details of \"{subject}\", the selected commit");
    }

    /// <summary>The working-changes row is selected, and nothing else.</summary>
    private static void AssertWorkingRowSelected(ShellDriver app)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.True(repository.IsWorkingRowSelected, "The working-changes row is not selected.");
        Assert.True(repository.Selection.IsEmpty, $"Commits are selected: {repository.Selection}.");
    }

    // ---- Menus ----

    /// <summary>
    /// Right-clicks commit row <paramref name="row"/> in its Message column and returns the menu it
    /// opens. The point the bounds give is first asserted against the one the report states (±2,
    /// the conventions' rule): <c>(468, 129 + 26 r)</c>, or <c>(468, 155 + 26 r)</c> while the
    /// working-changes row shows (<paramref name="belowWorkingRow"/>).
    /// </summary>
    private static MenuSnapshot RightClickCommit(ShellDriver app, int row, bool belowWorkingRow)
    {
        var point = app.GraphRowPoint(row);
        AssertNear(PartP.RowPoint(row, belowWorkingRow), point, string.Create(CultureInfo.InvariantCulture, $"Commit row {row}'s Message column"));
        app.RightClick(point);
        return app.Menu();
    }

    /// <summary>Clicks the working-changes row in its Message column, at the point the report states, (468, 129), ±2.</summary>
    private static void ClickWorkingRow(ShellDriver app)
    {
        var point = app.WorkingRowPoint();
        AssertNear(PartP.WorkingRowPoint, point, "The working-changes row's Message column");
        app.Click(point);
    }

    /// <summary>A point the bounds give against the one the report states, ±2 (the conventions' rule).</summary>
    private static void AssertNear(Point reported, Point actual, string what) =>
        Assert.True(
            Math.Abs(reported.X - actual.X) <= 2 && Math.Abs(reported.Y - actual.Y) <= 2,
            string.Create(CultureInfo.InvariantCulture, $"{what} is at {actual}; the report says {reported} (±2)."));

    /// <summary>Right-clicks a left-panel row, found by its path, and returns the menu it opens.</summary>
    private static async Task<MenuSnapshot> RightClickRefAsync(ShellDriver app, params string[] path)
    {
        app.RightClick(await app.RefRowAsync(path));
        return app.Menu();
    }

    /// <summary>Asserts the menu's header and that <paramref name="item"/> is in it, enabled and without a reason (part P asserts only the item it clicks); first when <paramref name="first"/>.</summary>
    private static void AssertItemEnabled(MenuSnapshot menu, string header, string item, bool first = false)
    {
        Assert.Equal(header, menu.Header);
        var found = menu.Item(item);
        Assert.True(found.IsEnabled, $"The menu item \"{item}\" is disabled.");
        Assert.Null(found.ToolTip);
        if (first)
        {
            Assert.Equal(item, menu.Items[0].Label);
        }
    }

    // ---- Operations and the error dialog ----

    /// <summary>
    /// Waits until the operation <paramref name="title"/> started since <paramref name="mark"/>
    /// failed (the queue's log line with "Failed", then the refresh) and its error dialog is open.
    /// </summary>
    private static async Task WaitForFailureAsync(ShellDriver app, TempRepo repo, string title, int mark)
    {
        await WaitForOperationAsync(app, repo, title, mark);
        AssertOperationLine(app, mark, title, OperationOutcome.Failed);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
    }

    /// <summary>
    /// Waits until the operation <paramref name="title"/> started since <paramref name="mark"/>
    /// succeeded (the queue's log line with "Succeeded", then the refresh), and no dialog is open.
    /// Returns the log line.
    /// </summary>
    private static async Task<string> WaitForSuccessAsync(ShellDriver app, TempRepo repo, string title, int mark, string? message = null)
    {
        await WaitForOperationAsync(app, repo, title, mark);
        var line = AssertOperationLine(app, mark, title, OperationOutcome.Succeeded, message);
        await AssertNoDialogAsync(app);
        return line;
    }

    /// <summary>The queue's one line of the operation <paramref name="title"/>'s end since <paramref name="mark"/>, with that outcome (and message, when given).</summary>
    private static string AssertOperationLine(ShellDriver app, int mark, string title, OperationOutcome outcome, string? message = null)
    {
        var pattern = AppLogFile.OperationEnded(PartP.Repo, title, outcome, message);
        var lines = app.LogLinesSince(mark).Where(line => pattern.IsMatch(line)).ToList();
        Assert.True(lines.Count == 1, $"Expected one log line like /{pattern}/; found {lines.Count}. The log since the input:\n" + string.Join('\n', app.LogLinesSince(mark).Where(line => line.Contains(title, StringComparison.Ordinal))));
        return lines[0];
    }

    /// <summary>Clicks the error dialog's "Close" and waits until no dialog is open.</summary>
    private static async Task CloseDialogAsync(ShellDriver app)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var close = view.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && button.Content as string == "Close");
        app.Click(close);
        await AssertNoDialogAsync(app);
    }

    /// <summary>The error dialog's extra action (<c>ErrorExtraButton</c>) is shown with this label and enabled.</summary>
    private static void AssertExtraButtonEnabled(ShellDriver app, string label)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        var extra = (Button)app.FindByAutomationId(view, "ErrorExtraButton");
        Assert.True(extra.IsEffectivelyVisible, "The error dialog shows no extra action.");
        Assert.Equal(label, extra.Content);
        Assert.True(extra.IsEffectivelyEnabled, $"\"{label}\" is disabled.");
    }

    /// <summary>The status bar's operation text is drawn whole, not cut with "…", within its 520, and its tooltip holds the same text (PD14).</summary>
    private static void AssertStatusWhole(ShellDriver app, string text)
    {
        var layout = app.OperationStatusLayout();
        Assert.Equal(text, layout.Text);
        Assert.True(layout.IsWhole, $"The status text \"{text}\" is cut ({layout.Width} wide).");
        Assert.True(layout.Width <= PartP.StatusTextMaxWidth, $"The status text is {layout.Width} wide, more than {PartP.StatusTextMaxWidth}.");
        Assert.Equal(text, layout.ToolTip);
    }

    // ---- The stage panel ----

    /// <summary>Waits until the stage panel shows with these titles over its two lists.</summary>
    private static Task WaitForStagePanelAsync(ShellDriver app, string unstagedTitle, string stagedTitle) =>
        app.WaitForAsync(
            () => app.FindByAutomationId("StagePanel").IsEffectivelyVisible
                && app.Find<TextBlock>("UnstagedTitle").Text == unstagedTitle
                && app.Find<TextBlock>("StagedTitle").Text == stagedTitle
                && app.StagePanelSettled,
            $"the stage panel with \"{unstagedTitle}\" and \"{stagedTitle}\"");

    /// <summary>A file list's rows as "letter name folder" (<c>UnstagedFileList</c>, <c>StagedFileList</c>).</summary>
    private static List<string> FileRows(ShellDriver app, string list)
    {
        app.Settle();
        var items = (ItemsControl)app.FindByAutomationId(list);
        return [.. ((IEnumerable<ChangedFileRow>)(items.ItemsSource ?? Array.Empty<ChangedFileRow>()))
            .Where(row => row.IsFile)
            .Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part))))];
    }

    // ---- The toolbar ----

    /// <summary>The toolbar's Undo and Redo: each enabled or not with its tooltip, a disabled one showing it while disabled.</summary>
    private static void AssertUndoRedo(ShellDriver app, UndoRedoButtons expected)
    {
        Assert.Equal(expected, app.UndoRedoState());
        foreach (var id in new[] { ShellDriver.UndoButtonId, ShellDriver.RedoButtonId })
        {
            var button = (Control)app.FindByAutomationId(id);
            if (!button.IsEffectivelyEnabled)
            {
                Assert.True(ToolTip.GetShowOnDisabled(button), $"The disabled {id} does not show its reason.");
            }
        }
    }

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

    // ---- The git calls ----

    /// <summary>Asserts the call <paramref name="command"/> made since <paramref name="mark"/>: its exit code, and returns it.</summary>
    private static GitCallRecord AssertCall(ShellDriver app, int mark, string command, int exitCode)
    {
        var call = AssertCalled(app, mark, command);
        Assert.True(call.ExitCode == exitCode, $"\"{command}\" exited {call.ExitCode}; the report says {exitCode}.\n{call.StandardError}{call.StandardOutput}");
        return call;
    }

    /// <summary>No <c>git stash push</c>, <c>pop</c> or <c>apply</c> since <paramref name="mark"/> (the conventions' "no <c>git stash</c> call").</summary>
    private static void AssertNoStashCall(ShellDriver app, int mark)
    {
        var calls = app.StashCallsSince(mark);
        Assert.True(calls.Count == 0, "The app ran stash calls:\n" + string.Join('\n', calls.Select(call => call.CommandText)));
    }

    /// <summary>
    /// The lines git printed on a stream as the call log recorded it: split at every carriage
    /// return and line feed, without trailing spaces and empty lines.
    /// </summary>
    private static List<string> Printed(string output) =>
        [.. output.Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];

    /// <summary>On Git 2.36 (the report's), asserts the lines git printed on a stream word for word; on another git, nothing (the conventions' "Git's own texts").</summary>
    private static async Task AssertGitPrintedAsync(string stream, IReadOnlyList<string> expected, string what)
    {
        if (await IsReportGitAsync())
        {
            var actual = Printed(stream);
            Assert.True(actual.SequenceEqual(expected), $"{what} printed:\n  {string.Join("\n  ", actual)}\nThe report says (Git 2.36):\n  {string.Join("\n  ", expected)}");
        }
    }

    // ---- Git, read back ----

    /// <summary>Plain git's output as lines, line ends as line feeds (none for an empty output).</summary>
    private static async Task<string[]> GitLinesAsync(TempRepo repo, params string[] arguments)
    {
        var text = (await repo.GitAsync(arguments)).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary><c>git status --porcelain=v1 --untracked-files=all --ignored</c>, as lines.</summary>
    private static Task<string[]> StatusAsync(TempRepo repo) =>
        GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all", "--ignored");

    /// <summary><c>git status --porcelain</c>, as lines.</summary>
    private static Task<string[]> PorcelainAsync(TempRepo repo) => GitLinesAsync(repo, "status", "--porcelain");

    /// <summary><c>git stash list</c>, as lines.</summary>
    private static Task<string[]> StashListAsync(TempRepo repo) => GitLinesAsync(repo, "stash", "list");

    /// <summary>HEAD's reflog, newest first, as <c>git log -g --format=%gs HEAD</c> lists it.</summary>
    private static Task<string[]> ReflogAsync(TempRepo repo) => GitLinesAsync(repo, "log", "-g", "--format=%gs", "HEAD");

    /// <summary>Every ref and what it points at, as <c>git for-each-ref</c> lists them (the backups included).</summary>
    private static Task<string[]> RefsAsync(TempRepo repo) => GitLinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)");

    /// <summary>The branch HEAD is on (<c>git symbolic-ref -q HEAD</c>), or null while it is detached.</summary>
    private static async Task<string?> SymbolicHeadAsync(TempRepo repo)
    {
        var result = await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD");
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>What a revision names (<c>git rev-parse --verify</c>), or null when it names nothing.</summary>
    private static async Task<string?> RevParseAsync(TempRepo repo, string revision)
    {
        var result = await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", revision);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>A working-tree file's text, as git wrote it (line ends as line feeds).</summary>
    private static string WorkingFile(TempRepo repo, string path) =>
        File.ReadAllText(Path.Combine(repo.Path, path)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Whether a file or folder is in the working tree.</summary>
    private static bool InWorkingTree(TempRepo repo, string path) => Path.Exists(Path.Combine(repo.Path, path));

    /// <summary>No operation is in progress: none of <c>MERGE_HEAD</c>, <c>CHERRY_PICK_HEAD</c>, <c>REVERT_HEAD</c> and <c>sequencer</c> in the git folder.</summary>
    private static void AssertNothingInProgress(TempRepo repo)
    {
        foreach (var name in new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "sequencer" })
        {
            Assert.False(Path.Exists(Path.Combine(repo.Path, ".git", name)), $"The git folder holds {name}.");
        }
    }

    /// <summary>HEAD is on <c>main</c> at Main work.</summary>
    private static async Task AssertOnMainAsync(TempRepo repo)
    {
        Assert.Equal("refs/heads/main", await SymbolicHeadAsync(repo));
        Assert.Equal(PartP.MainWorkSha, await repo.HeadAsync());
    }

    /// <summary>HEAD is detached at <paramref name="sha"/>.</summary>
    private static async Task AssertDetachedAtAsync(TempRepo repo, string sha)
    {
        Assert.Null(await SymbolicHeadAsync(repo));
        Assert.Equal(sha, await repo.HeadAsync());
    }

    /// <summary>No backup ref (<c>git for-each-ref refs/visualcommit/backup</c> prints nothing).</summary>
    private static async Task AssertNoBackupAsync(TempRepo repo) =>
        Assert.Empty(await GitLinesAsync(repo, "for-each-ref", BackupRef.Prefix.TrimEnd('/')));

    /// <summary>
    /// Asserts a stash commit as git stores it: its parents, its message (<c>git log --format=%s</c>),
    /// author and committer "Test Author" and both dates the app's (D72).
    /// </summary>
    private static async Task AssertStashCommitAsync(TempRepo repo, string sha, string listed, params string[] parents)
    {
        var fields = (await GitLinesAsync(repo, "log", "-1", "--format=%P%x1f%s%x1f%an <%ae>%x1f%cn <%ce>%x1f%aI%x1f%cI", sha)).Single().Split('\x1f');
        Assert.Equal(string.Join(' ', parents), fields[0]);
        Assert.Equal(listed, fields[1]);
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[2]);
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[3]);
        AssertGitDate(AppCommitTime, fields[4]);
        AssertGitDate(AppCommitTime, fields[5]);
    }

    /// <summary>
    /// The labels the graph draws in the given rows (<c>LabelsInRow</c>): their drawn texts in
    /// order, none cut and none a "+N", each from and to the x the report gives, ±2 (the
    /// conventions' rule).
    /// </summary>
    private static void AssertDrawnLabels(ShellDriver app, params (int Row, PartP.DrawnLabel[] Labels)[] rows)
    {
        var graph = Graph(app);
        foreach (var (row, labels) in rows)
        {
            var drawn = graph.LabelsInRow(row);
            Assert.Equal(labels.Select(label => label.Text), drawn.Select(label => label.Text));
            Assert.All(drawn, label => Assert.False(label.IsOverflow, $"Row {row} draws \"{label.Text}\" for labels it has no room for."));
            foreach (var expected in labels)
            {
                var bounds = app.GraphLabelBounds(row, expected.Text);
                Assert.True(
                    Math.Abs(bounds.Left - expected.Left) <= 2 && Math.Abs(bounds.Right - expected.Right) <= 2,
                    string.Create(CultureInfo.InvariantCulture, $"Row {row}'s label \"{expected.Text}\" is drawn from x {bounds.Left} to {bounds.Right}; the report gives {expected.Left} to {expected.Right} (±2)."));
            }
        }
    }

    /// <summary>A label's bounds against the report's numbers, ±2 (the conventions' rule).</summary>
    private static void AssertBounds(Rect actual, (double Left, double Top, double Right, double Bottom) expected, string what) =>
        Assert.True(
            Math.Abs(actual.Left - expected.Left) <= 2 && Math.Abs(actual.Top - expected.Top) <= 2 && Math.Abs(actual.Right - expected.Right) <= 2 && Math.Abs(actual.Bottom - expected.Bottom) <= 2,
            string.Create(CultureInfo.InvariantCulture, $"{what} lies at {actual}; the report gives x {expected.Left} to {expected.Right}, y {expected.Top} to {expected.Bottom} (±2)."));

    /// <summary>A copy of "protect" with P25's changes, made as a terminal makes them: <c>src/lib.txt</c> staged, then changed further, <c>src/app.txt</c> changed and <c>scratch.txt</c> untracked.</summary>
    private static async Task MakeP25ChangesAsync(TempRepo repo)
    {
        repo.WriteFile("src/lib.txt", PartP.LibStaged);
        await repo.GitAsync("add", "--", "src/lib.txt");
        repo.WriteFile("src/lib.txt", PartP.LibStagedAndMore)
            .WriteFile("src/app.txt", PartP.AppUnstaged)
            .WriteFile("scratch.txt", PartP.Scratch);
    }

    /// <summary>The files P25's changes leave, with their contents, and the ignored settings untouched.</summary>
    private static void AssertP25Files(TempRepo repo, bool scratch = true)
    {
        Assert.Equal(PartP.AppUnstaged, WorkingFile(repo, "src/app.txt"));
        Assert.Equal(PartP.LibStagedAndMore, WorkingFile(repo, "src/lib.txt"));
        Assert.Equal(scratch, InWorkingTree(repo, "scratch.txt"));
        if (scratch)
        {
            Assert.Equal(PartP.Scratch, WorkingFile(repo, "scratch.txt"));
        }

        Assert.Equal(PartP.SettingsLocal, WorkingFile(repo, "settings.local.json"));
    }
}
