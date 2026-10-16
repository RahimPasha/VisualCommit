// What checks U16 to U19 of part U share (docs/test-reports/phase-5.md, "Part U: undo and redo"
// and its "Conventions of part U"): a step of the toolbar's Undo or Redo with what "Undo ran"
// asserts, the toolbar's states T0, T+ and T−, the calls a step leaves in the call log, the graph's
// selection, the stage panel, the left panel's arrows, and git's state read back as the
// conventions read it. Only the writer of U16 to U19 edits this file; the checks are in
// Phase5Checks.U16.cs to Phase5Checks.U19.cs, their expected results in Phase5Expectations.U.cs
// (PartU, the region of U16 to U19).
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase5;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5Undo;

/// <summary>The checks of part U: undo and redo (U16 to U23), one action with its undo and redo per method.</summary>
public sealed partial class Phase5ChecksU : Phase5Checks
{
    /// <summary>The active tab's repository.</summary>
    private static RepositoryViewModel Repo(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    // ---- The toolbar's Undo and Redo, and a step ----

    /// <summary>Asserts the toolbar's Undo and Redo as drawn (the conventions' T0, T+ and T−, or a state written out in full).</summary>
    private static void AssertButtons(ShellDriver app, UndoRedoButtons expected)
    {
        var actual = app.UndoRedoState();
        Assert.True(actual == expected, $"Undo and Redo are {actual}; the report says {expected}.");
    }

    /// <summary>
    /// Clicks the toolbar's Undo and asserts what the conventions call "Undo ran": one operation of
    /// the queue titled <c>Undo {text}</c> whose running text <c>Undoing {text}…</c> the status
    /// bar showed, no dialog after it, the queue's log line <c>Undo {text}: Succeeded after N ms:
    /// Undid {text}</c>, and the status bar <c>Undid {text}</c> in the secondary colour. Returns the
    /// number of git calls before the click, a mark for <see cref="CallsSince"/>.
    /// </summary>
    private static Task<int> UndoAsync(ShellDriver app, TempRepo repo, string text) =>
        StepAsync(app, repo, PartU.Undo, text, () => app.ClickUndoAsync(OperationChainTimeout));

    /// <summary>Clicks the toolbar's Redo and asserts "Redo ran", as <see cref="UndoAsync"/> asserts "Undo ran".</summary>
    private static Task<int> RedoAsync(ShellDriver app, TempRepo repo, string text) =>
        StepAsync(app, repo, PartU.Redo, text, () => app.ClickRedoAsync(OperationChainTimeout));

    /// <summary>
    /// Asserts that <paramref name="click"/> ran one step of <paramref name="step"/>'s direction
    /// for the entry <paramref name="text"/> ("Undo ran", "Redo ran"); the restore bar's Restore
    /// runs as the discard's Undo this way (design 7.9).
    /// </summary>
    private static async Task<int> StepAsync(ShellDriver app, TempRepo repo, PartU.StepTexts step, string text, Func<Task> click)
    {
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        var operations = ForegroundOperations(app);
        List<string> running;
        using (var recorder = app.RecordStatusTexts())
        {
            await click();
            await WaitForOperationAsync(app, repo, step.Title(text), mark);
            running = [.. recorder.Texts];
        }

        await AssertNoDialogAsync(app);
        var ran = ForegroundOperations(app).Skip(operations.Count).Select(record => record.Title).ToList();
        Assert.True(ran.SequenceEqual([step.Title(text)]), $"The click ran {string.Join(", ", ran.Select(title => $"\"{title}\""))}; the report says one operation \"{step.Title(text)}\".");
        Assert.True(running.Contains(step.RunningText(text)), $"The status bar did not show \"{step.RunningText(text)}\" while the step ran: {string.Join(" | ", running)}");
        AssertOperationLine(app, mark, NameOf(repo), step.Title(text), OperationOutcome.Succeeded, step.DoneText(text));
        AssertStatus(app, step.DoneText(text));
        return calls;
    }

    /// <summary>
    /// The open tooltip with <paramref name="text"/> lies inside the window and under the toolbar
    /// button <paramref name="owner"/>: below its icon and label (the lowest text it draws), its
    /// left edge within the button.
    /// </summary>
    private static void AssertToolTipUnder(ShellDriver app, Control owner, string text)
    {
        var tips = app.Window.GetVisualDescendants().OfType<ToolTip>().Where(tip => tip.IsEffectivelyVisible && tip.Bounds.Width > 0).ToList();
        var tip = Assert.Single(tips, candidate => candidate.Content as string == text || (candidate.Content is Visual visual && TextsIn(visual).Contains(text)));
        var bounds = app.BoundsOf(tip);
        var size = app.Window.ClientSize;
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= size.Width && bounds.Bottom <= size.Height, $"The tooltip at {bounds} is not inside the {size.Width}x{size.Height} window.");
        var button = app.BoundsOf(owner);
        var label = owner.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(app.BoundsOf).Max(block => block.Bottom);
        Assert.True(
            bounds.Top >= label - 0.5 && bounds.Left >= button.Left - 0.5 && bounds.Left <= button.Right,
            $"The tooltip at {bounds} is not under {owner.Name} at {button} (its label ends at y {label}).");
    }

    /// <summary>Waits until the graph shows HEAD at <paramref name="sha"/> with its history complete, and the tab is idle: a write of the stage panel has ended.</summary>
    private static async Task WaitForHeadAsync(ShellDriver app, string sha)
    {
        await app.WaitForAsync(
            () => Repo(app) is { Graph: { IsComplete: true } graph } && graph.Count > 0 && graph.Refs.Head.Sha == sha,
            $"HEAD at {sha[..7]} in the graph");
        await app.WaitForIdleAsync();
    }

    /// <summary>
    /// Runs an action of an earlier phase: <paramref name="act"/> (the click that starts it), then
    /// a wait until one more operation the user started has ended and the tab is idle, its refresh
    /// included. Returns the number of git calls before the click, a mark for <see cref="CallsSince"/>.
    /// </summary>
    private static async Task<int> ActAsync(ShellDriver app, Action act, TimeSpan? timeout = null)
    {
        var calls = app.GitCalls().Count;
        var before = ForegroundOperations(app).Count;
        act();
        await app.WaitForAsync(() => ForegroundOperations(app).Count > before && app.Session.Shell.ActiveTab.Repository?.Queue.Current is null, "the action's operation to end", timeout ?? OperationChainTimeout);
        await app.WaitForIdleAsync(timeout: timeout ?? OperationChainTimeout);
        await AssertNoDialogAsync(app);
        return calls;
    }

    /// <summary>The operations the active tab's queue ran that the user started (no background ones), oldest first.</summary>
    private static List<OperationRecord> ForegroundOperations(ShellDriver app) =>
        [.. app.Session.Operations.Snapshot().Where(record => !record.IsBackground)];

    /// <summary>The queue's line of the operation <paramref name="title"/>'s end since <paramref name="mark"/>, with that outcome and message (one such line).</summary>
    private static string AssertOperationLine(ShellDriver app, int mark, string repo, string title, OperationOutcome outcome, string message)
    {
        var pattern = AppLogFile.OperationEnded(repo, title, outcome, message);
        var lines = app.LogLinesSince(mark).Where(line => pattern.IsMatch(line)).ToList();
        Assert.True(lines.Count == 1, $"Expected one log line like /{pattern}/; found {lines.Count}. The log since the input:\n" + string.Join('\n', app.LogLinesSince(mark).Where(line => line.Contains(title, StringComparison.Ordinal))));
        return lines[0];
    }

    // ---- The call log ----

    /// <summary>
    /// Asserts the step's <c>update-ref -m &lt;message&gt; --stdin</c> transaction since
    /// <paramref name="calls"/>: one such call with that message (and <c>--create-reflog</c> when
    /// asked), which succeeded, with exactly these lines as its recorded input.
    /// </summary>
    private static void AssertTransaction(ShellDriver app, int calls, string message, string[] lines, bool createReflog = false)
    {
        var command = PartU.Transaction(message, createReflog);
        var call = AssertCalled(app, calls, command);
        Assert.True(call.Succeeded, $"\"{command}\" failed: {call.StandardError}");
        Assert.Equal(string.Concat(lines.Select(line => line + "\n")), call.StandardInput);
        Assert.Single(CallsSince(app, calls), text => text == command);
    }

    /// <summary>
    /// Asserts that no write since <paramref name="calls"/> starts with <paramref name="prefix"/>
    /// (<c>git read-tree</c>, <c>git restore</c>, <c>git config</c>, <c>git stash</c>): the
    /// history's reads before and after a step (7.7), which no check counts, are left out
    /// (<see cref="IsRead"/>).
    /// </summary>
    private static void AssertNoCall(ShellDriver app, int calls, string prefix)
    {
        var found = CallsSince(app, calls).Where(call => call.StartsWith(prefix, StringComparison.Ordinal) && !IsRead(call)).ToList();
        Assert.True(found.Count == 0, $"The step ran \"{prefix}…\":\n" + string.Join('\n', found));
    }

    /// <summary>
    /// Asserts the writes since <paramref name="calls"/> that start with one of
    /// <paramref name="prefixes"/>: exactly <paramref name="expected"/>, in order. Reads of the
    /// same commands (<see cref="IsRead"/>) are left out.
    /// </summary>
    private static void AssertWrites(ShellDriver app, int calls, string[] prefixes, params string[] expected)
    {
        var writes = CallsSince(app, calls).Where(call => prefixes.Any(prefix => call.StartsWith(prefix, StringComparison.Ordinal)) && !IsRead(call)).ToList();
        Assert.True(
            writes.SequenceEqual(expected),
            "The step's writes are not the report's.\nExpected:\n  " + string.Join("\n  ", expected) + "\nActual:\n  " + string.Join("\n  ", writes));
    }

    /// <summary>
    /// Whether a call is one of the reads the undo history makes before and after a step (7.7:
    /// the configuration's keys, the stash list) rather than a write of the same command.
    /// </summary>
    private static bool IsRead(string call) =>
        call.StartsWith("git stash list", StringComparison.Ordinal)
        || (call.StartsWith("git config", StringComparison.Ordinal) && (call.Contains(" --list", StringComparison.Ordinal) || call.Contains(" --get", StringComparison.Ordinal)));

    // ---- The graph's selection ----

    /// <summary>Nothing is selected: no commit, not the working-changes row, and the details panel shows its placeholder.</summary>
    private static void AssertNothingSelected(ShellDriver app)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.True(repository.Selection.IsEmpty, $"Commits are selected: {string.Join(", ", repository.Selection.Shas)}.");
        Assert.Equal(-1, repository.SelectedIndex);
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
        var placeholder = (TextBlock)app.FindByAutomationId("DetailsPlaceholder");
        Assert.True(placeholder.IsEffectivelyVisible, "The details panel does not show its placeholder.");
        Assert.Equal(PartU.NothingSelectedPlaceholder, placeholder.Text);
    }

    /// <summary>Commit row <paramref name="row"/> alone is selected, and it is the commit <paramref name="sha"/> (the graph's selection and the view model's).</summary>
    private static void AssertRowSelected(ShellDriver app, int row, string sha)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.Equal(row, repository.SelectedIndex);
        Assert.Equal(sha, repository.Selection.Single);
        Assert.Equal(sha, repository.Graph.CommitAt(row).Sha);
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
        Assert.Equal(row, Graph(app).SelectedIndex);
    }

    /// <summary>Exactly the commits <paramref name="shas"/> are selected (several commits), at the rows <paramref name="rows"/>.</summary>
    private static void AssertRowsSelected(ShellDriver app, int[] rows, params string[] shas)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.Equal(shas.Order(StringComparer.Ordinal), repository.Selection.Shas.Order(StringComparer.Ordinal));
        Assert.Equal(shas.Order(StringComparer.Ordinal), rows.Select(row => repository.Graph.CommitAt(row).Sha).Order(StringComparer.Ordinal));
        Assert.False(repository.IsWorkingRowSelected, "The working-changes row is selected.");
    }

    /// <summary>The working-changes row is selected (the stage panel shows).</summary>
    private static void AssertWorkingRowSelected(ShellDriver app)
    {
        app.Settle();
        var repository = Repo(app);
        Assert.True(repository.IsWorkingRowSelected, "The working-changes row is not selected.");
        Assert.True(repository.Selection.IsEmpty, $"Commits are selected: {string.Join(", ", repository.Selection.Shas)}.");
    }

    /// <summary>Waits until the details panel shows the selected commit <paramref name="subject"/>.</summary>
    private static async Task WaitForDetailsAsync(ShellDriver app, string subject)
    {
        await app.WaitForAsync(
            () => Shown(app, "DetailsSubject") is SelectableTextBlock text && text.Text == subject
                && Shown(app, "DetailsSha") is SelectableTextBlock sha
                && Graph(app).Selection.Shas is [var selected] && sha.Text == selected,
            $"the details of \"{subject}\", the selected commit");
        app.Settle();
    }

    /// <summary>The visible control with this automation id, or null while none is in the window.</summary>
    private static Control? Shown(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(control) == automationId);

    /// <summary>Clicks commit row <paramref name="row"/> in its Message column (the point asserted against the report's, ±2, when it states one).</summary>
    private static void ClickRow(ShellDriver app, int row, Point? reported = null)
    {
        var point = app.GraphRowPoint(row);
        AssertNear(reported, point, $"row {row.ToString(CultureInfo.InvariantCulture)}'s Message column");
        app.Click(point);
    }

    /// <summary>Right-clicks commit row <paramref name="row"/> in its Message column (asserted against the report's point, ±2), waits for its menu and clicks <paramref name="item"/>.</summary>
    private static void RightClickRowAndChoose(ShellDriver app, int row, Point? reported, string item)
    {
        var point = app.GraphRowPoint(row);
        AssertNear(reported, point, $"row {row.ToString(CultureInfo.InvariantCulture)}'s Message column");
        app.RightClick(point);
        app.Menu();
        app.ClickMenuItem(item);
    }

    /// <summary>Right-clicks the label <paramref name="label"/> of commit row <paramref name="row"/> at its centre, waits for its menu and clicks <paramref name="item"/>.</summary>
    private static void RightClickLabelAndChoose(ShellDriver app, int row, string label, string item)
    {
        app.RightClick(app.GraphLabelBounds(row, label).Center);
        app.Menu();
        app.ClickMenuItem(item);
    }

    /// <summary>Clicks the working-changes row in its Message column (against the report's point, ±2, where it states one) and waits for the stage panel.</summary>
    private static async Task ClickWorkingRowAsync(ShellDriver app, Point? reported = null)
    {
        var point = app.WorkingRowPoint();
        AssertNear(reported, point, "the working-changes row's Message column");
        app.Click(point);
        await app.WaitForAsync(() => Shown(app, "StagePanel") is not null && app.StagePanelSettled, "the stage panel");
        await app.WaitForIdleAsync();
    }

    /// <summary>A point the app's bounds give, against the one the report states (±2), when it states one.</summary>
    private static void AssertNear(Point? reported, Point actual, string what)
    {
        if (reported is { } expected)
        {
            Assert.True(
                Math.Abs(expected.X - actual.X) <= 2 && Math.Abs(expected.Y - actual.Y) <= 2,
                $"{what} is at {actual}; the report says {expected} (±2).");
        }
    }

    // ---- The stage panel ----

    /// <summary>Waits until the stage panel shows with these titles over its two lists, its message read.</summary>
    private static Task WaitForStagePanelAsync(ShellDriver app, string unstagedTitle, string stagedTitle) =>
        app.WaitForAsync(
            () => Shown(app, "StagePanel") is not null
                && app.Find<TextBlock>("UnstagedTitle").Text == unstagedTitle
                && app.Find<TextBlock>("StagedTitle").Text == stagedTitle
                && app.StagePanelSettled,
            $"the stage panel with \"{unstagedTitle}\" and \"{stagedTitle}\"");

    /// <summary>
    /// Asserts the stage panel's two lists as "letter name folder" rows and their titles, and
    /// "No unstaged changes" when the first is empty.
    /// </summary>
    private static async Task AssertStagePanelAsync(ShellDriver app, string[] unstaged, string[] staged)
    {
        var unstagedTitle = string.Create(CultureInfo.InvariantCulture, $"Unstaged files ({unstaged.Length})");
        var stagedTitle = string.Create(CultureInfo.InvariantCulture, $"Staged files ({staged.Length})");
        await WaitForStagePanelAsync(app, unstagedTitle, stagedTitle);
        Assert.Equal(unstaged, FileRows(app, "UnstagedFileList"));
        Assert.Equal(staged, FileRows(app, "StagedFileList"));
        var none = app.Find<TextBlock>("NoUnstaged");
        Assert.Equal(unstaged.Length == 0, none.IsEffectivelyVisible);
        if (unstaged.Length == 0)
        {
            Assert.Equal(PartU.NoUnstagedChanges, none.Text);
        }
    }

    /// <summary>A file list's rows as "letter name folder" (<c>UnstagedFileList</c>, <c>StagedFileList</c>).</summary>
    private static List<string> FileRows(ShellDriver app, string list)
    {
        app.Settle();
        var items = (ItemsControl)app.FindByAutomationId(list);
        return [.. ((IEnumerable<ChangedFileRow>)(items.ItemsSource ?? Array.Empty<ChangedFileRow>()))
            .Where(row => row.IsFile)
            .Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part))))];
    }

    /// <summary>The text a commit box holds (<c>CommitSummary</c>, <c>CommitDescription</c>).</summary>
    private static string BoxText(ShellDriver app, string automationId) => ((TextBox)app.FindByAutomationId(automationId)).Text ?? string.Empty;

    /// <summary>
    /// The restore bar (D62, design 7.9): shown with <paramref name="text"/>, or closed when
    /// <paramref name="text"/> is null. The stage panel keeps it while a commit is selected; it is
    /// drawn only while the stage panel shows, and then the drawn bar is asserted too.
    /// </summary>
    private static void AssertRestoreBar(ShellDriver app, string? text)
    {
        app.Settle();
        Assert.Equal(text, Repo(app).Changes.RestoreText);
        if (Shown(app, "StagePanel") is null)
        {
            return;
        }

        var bar = app.Find<Border>("RestoreBar");
        Assert.True(bar.IsEffectivelyVisible == (text is not null), text is null ? "The restore bar shows." : "The restore bar does not show.");
        if (text is not null)
        {
            Assert.Equal(text, app.Find<TextBlock>("RestoreBarText").Text);
        }
    }

    /// <summary>Rests the pointer on a stage list's file row, then clicks one of its own buttons (<c>RowDiscardButton</c>).</summary>
    private static async Task ClickRowButtonAsync(ShellDriver app, string list, string path, string buttonId)
    {
        var row = await app.FileRowAsync(list, path);
        app.MoveMouse(row);
        var button = row.GetVisualParent()!.GetVisualDescendants().OfType<Button>()
            .Single(candidate => Avalonia.Automation.AutomationProperties.GetAutomationId(candidate) == buttonId);
        Assert.True(button.IsEffectivelyVisible, $"{buttonId} does not show on the row of {path}.");
        app.Click(button);
    }

    // ---- The left panel ----

    /// <summary>Asserts a left-panel branch's ahead and behind arrows (0 for none).</summary>
    private static void AssertArrows(ShellDriver app, int ahead, int behind, params string[] path)
    {
        var model = (RefItemRow)app.RefRowModel(path);
        Assert.True(
            (model.Ahead, model.Behind) == (ahead, behind),
            $"{string.Join(" › ", path)} shows ↑{model.Ahead} ↓{model.Behind}; the report says ↑{ahead} ↓{behind}.");
    }

    /// <summary>How many rows the left panel's section <paramref name="title"/> counts.</summary>
    private static int SectionCount(ShellDriver app, string title)
    {
        app.Settle();
        return app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<RefSectionRow>().Single(row => row.Text == title).Count;
    }

    // ---- Git, read back ----

    /// <summary>Plain git's output, line ends as line feeds, without its last line feed.</summary>
    private static async Task<string> GitTextAsync(TempRepo repo, params string[] arguments) =>
        (await repo.GitAsync(arguments)).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>Plain git's output as lines (none for an empty output).</summary>
    private static async Task<string[]> GitLinesAsync(TempRepo repo, params string[] arguments)
    {
        var text = await GitTextAsync(repo, arguments);
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary>The conventions' status: <c>git status --porcelain=v1 -b --untracked-files=all</c>, as lines.</summary>
    private static Task<string[]> StatusAsync(TempRepo repo) => GitLinesAsync(repo, "status", "--porcelain=v1", "-b", "--untracked-files=all");

    /// <summary>What a revision names (<c>git rev-parse --verify</c>), or null when it names nothing.</summary>
    private static async Task<string?> RevParseAsync(TempRepo repo, string revision)
    {
        var result = await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", revision);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    /// <summary>The newest <paramref name="count"/> messages of a ref's reflog (<c>git log -g -n k --format=%gs</c>), newest first.</summary>
    private static Task<string[]> ReflogAsync(TempRepo repo, string reference, int count) =>
        GitLinesAsync(repo, "log", "-g", "-n", count.ToString(CultureInfo.InvariantCulture), "--format=%gs", reference);

    /// <summary>Whether a ref has a reflog (<c>git reflog exists</c>).</summary>
    private static async Task<bool> ReflogExistsAsync(TempRepo repo, string reference) =>
        (await repo.GitAllowingFailureAsync("reflog", "exists", reference)).Succeeded;

    /// <summary>The blob a working-tree file's bytes make (<c>git hash-object</c>), or null when the file is absent.</summary>
    private static async Task<string?> WorkingBlobAsync(TempRepo repo, string path) =>
        File.Exists(Path.Combine(repo.Path, path)) ? (await GitTextAsync(repo, "hash-object", "--", path)).Trim() : null;

    /// <summary>The blob the index holds for a path (<c>git rev-parse :path</c>), or null when it holds none.</summary>
    private static Task<string?> IndexBlobAsync(TempRepo repo, string path) => RevParseAsync(repo, ":" + path);

    /// <summary>Asserts that an object id starts with the short id the report gives.</summary>
    private static void AssertShort(string expectedShort, string? actual, string what)
    {
        Assert.True(actual is not null && actual.StartsWith(expectedShort, StringComparison.Ordinal), $"{what} is {actual ?? "absent"}; the report says {expectedShort}.");
    }

    /// <summary>The local configuration's keys that match <paramref name="pattern"/> (<c>git config --local --get-regexp</c>), as "key value" lines; none when nothing matches.</summary>
    private static async Task<string[]> ConfigAsync(TempRepo repo, string pattern)
    {
        var result = await repo.GitAllowingFailureAsync("config", "--local", "--get-regexp", pattern);
        return result.ExitCode == 1 ? [] : result.EnsureSuccess(new GitCommand("config")).StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
    }

    /// <summary>The backup refs as "name target" lines, in git's order of their names.</summary>
    private static async Task<List<string>> BackupLinesAsync(TempRepo repo) =>
        [.. (await BackupsAsync(repo)).Select(backup => $"{backup.Name} {backup.Sha}")];

    /// <summary>
    /// Asserts a commit as git stores it: its message (subject, and body after a blank line when
    /// given), parents, author and committer, and the author and commit dates.
    /// </summary>
    private static async Task AssertCommitAsync(TempRepo repo, string sha, string message, string[] parents, string author, DateTimeOffset authorDate, DateTimeOffset commitDate)
    {
        var fields = (await GitTextAsync(repo, "log", "-1", "--format=%P%x1f%an <%ae>%x1f%cn <%ce>%x1f%aI%x1f%cI%x1f%B", sha)).Split('\x1f');
        Assert.Equal(string.Join(' ', parents), fields[0]);
        Assert.Equal(author, fields[1]);
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[2]);
        AssertGitDate(authorDate, fields[3]);
        AssertGitDate(commitDate, fields[4]);
        Assert.Equal(message, fields[5].TrimEnd('\n'));
    }

    /// <summary>A working-tree file's text, as git wrote it (line ends as line feeds), or null when it is absent.</summary>
    private static string? WorkingFile(TempRepo repo, string path)
    {
        var full = Path.Combine(repo.Path, path);
        return File.Exists(full) ? File.ReadAllText(full).Replace("\r\n", "\n", StringComparison.Ordinal) : null;
    }
}
