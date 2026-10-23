// Part B's real-window steps (docs/test-reports/phase-5.md, part B, the "Real window" column of B5
// to B9): B5 method 1 steps 1 to 4; B6 steps 1, 4, 6, 7, 9, 10 and 11 (a real double-click on a
// run); B7's first method, steps 1 to 3 and 5 to 8; B8 steps 1, 2, 4 to 8 and 11; B9's first
// method, step 1 (the list). Each launch on a fresh copy at 1100×700, dark. Pages are found by
// their automation ids and names (FileHistory, Blame, Reflog), their rows by FileHistoryRow,
// ReflogRow and BackupRow named as design 13.4 names them and clicked at window x 420 (BD1); the
// blame's lines at the points BlameGutter's and BlameText's bounding rectangles give; the blame's
// text through its Value pattern. B5's method 2 (1920×1080) and B7's restart are scripted only.
using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations.PartB;

namespace VisualCommit.RealWindowTests;

public partial class Phase5RealWindowPass
{
    /// <summary>
    /// Check B5, method 1, steps 1 to 4, with the real mouse: the working-changes row, the stage
    /// panel's menu of src/Reader.cs and its "File history" (the page with its 4 rows and its hosted
    /// diff, the first row focused, the stage panel kept: <c>b5b-history</c>), then a real click on
    /// row 1, the rename (its diff and its commit in the right panel: <c>b5c-rename-row</c>).
    /// </summary>
    [Fact]
    public async Task Check_B5_file_history()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var app = Launch(repo);
        var refs = await RefsAsync(repo);
        WaitForNothingSelected(app);

        // Step 1: the working-changes row.
        ClickWorkingRow(app, HistoryLanes, UnstagedOneFile, StagedNone);

        // Step 2: src/Reader.cs's menu in the stage panel (pictured by the scripted run).
        app.RightClickWithMouse(app.StageFileRow("UnstagedFileList", "src/Reader.cs", StepTimeout));
        AssertMenu(app, StageFileMenu, StageFileMenuHeight);
        AssertFocusedItem(app, "Stage");

        // Step 3: File history: 4 rows, the first selected with its diff, the stage panel kept.
        app.ClickMenuItem(FileHistoryItem);
        app.WaitForPage("FileHistory", ReaderTitle, StepTimeout);
        app.WaitForPageRows("FileHistory", "FileHistoryRow", [.. ReaderHistory.Select(row => row.Name)], StepTimeout);
        app.WaitForPageText("FileHistory", "DiffSideLabel", "c0bc2dd", StepTimeout);
        app.WaitForPageRowFocus("FileHistory", "FileHistoryRow", ReaderHistory[0].Name, StepTimeout);
        AssertHistoryHeader(app, ReaderTitle, from: null);
        AssertPageInPlaceOfGraph(app, "FileHistory");
        app.WaitForStageTitles(UnstagedOneFile, StagedNone, StepTimeout);
        Compare(app, "b5b-history");

        // Step 4: a real click on the rename's row: its diff, its commit in the right panel.
        app.ClickPageRow("FileHistory", "FileHistoryRow", ReaderHistory[1].Name);
        app.WaitForPageText("FileHistory", "DiffSideLabel", "0575c7f", StepTimeout);
        WaitForDetails(app, Rename.Subject, Rename.Sha);
        app.WaitForPageRowFocus("FileHistory", "FileHistoryRow", ReaderHistory[1].Name, StepTimeout);
        Compare(app, "b5c-rename-row");

        Assert.Equal(0, app.Close());
        await AssertGitUnchangedAsync(repo, refs);
    }

    /// <summary>
    /// Check B6, steps 1, 4, 6, 7, 9, 10 and 11, with the real mouse: the working tree's blame from
    /// the stage panel's menu (<c>b6a-blame-working-tree</c>), a real click on line 13 in the gutter
    /// (<c>b6c-run-clicked</c>), the run menu of line 4 and "Blame before this commit", the blame's
    /// File history and the history's Blame (by id), then a real double-click on line 20 in the
    /// gutter: Show in graph once, the page closed (<c>b6h-shown-in-graph</c>).
    /// </summary>
    [Fact]
    public async Task Check_B6_blame()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var app = Launch(repo);
        var refs = await RefsAsync(repo);

        // Step 1: the working tree's blame from the stage panel's menu.
        ClickWorkingRow(app, HistoryLanes, UnstagedOneFile, StagedNone);
        app.RightClickWithMouse(app.StageFileRow("UnstagedFileList", "src/Reader.cs", StepTimeout));
        app.ClickMenuItem(BlameItem);
        WaitForBlame(app, ReaderBlameTitle, InTheWorkingTree, ReaderWorkingTree);
        AssertPageInPlaceOfGraph(app, "Blame");
        AssertBlameLayout(app);
        app.WaitForStageTitles(UnstagedOneFile, StagedNone, StepTimeout);
        Compare(app, "b6a-blame-working-tree");

        // Step 4: a real click on line 13 in the gutter: Read whole lines in the right panel.
        var line13 = app.BlameGutterPoint(13);
        AssertNear(new PointF((float)GutterPointX, (float)BlameLineCentreY(13)), line13, "Line 13's point in the gutter");
        app.ClickAt(line13.X, line13.Y);
        WaitForDetails(app, ReadWholeLines.Subject, ReadWholeLines.Sha);
        Compare(app, "b6c-run-clicked");

        // Step 6: the menu of line 4's run, which the right-click selects first (BD9).
        var line4 = app.BlameGutterPoint(4);
        app.RightClickAt(line4.X, line4.Y);
        AssertMenu(app, RunMenu("0575c7f", "Rename the parser to reader"), RunMenuHeight);
        AssertFocusedItem(app, ShowInGraph);
        WaitForDetails(app, Rename.Subject, Rename.Sha);

        // Step 7: Blame before this commit: src/Parser.cs at 14a5c6c, across the rename.
        app.ClickMenuItem(BlameBeforeThisCommit);
        WaitForBlameLines(app, ParserBlameTitle, At("14a5c6c", "Add tracing"), ParserLines);
        AssertParserNamedLines(app);

        // Step 9: the blame's File history (by id): src/Parser.cs from 14a5c6c.
        app.ClickWithMouse(app.Find("BlameHistory"));
        app.WaitForPage("FileHistory", ParserTitle, StepTimeout);
        app.WaitForPageRows("FileHistory", "FileHistoryRow", [.. ParserHistory.Select(row => row.Name)], StepTimeout);
        AssertHistoryHeader(app, ParserTitle, From("14a5c6c"));

        // Step 10: the history's Blame (by id): src/Parser.cs at 365482b.
        app.ClickWithMouse(app.Find("FileHistoryBlame"));
        WaitForBlameLines(app, ParserBlameTitle, At("365482b", "Handle empty input"), ParserLines);
        AssertParserNamedLines(app);

        // Step 11: a real double-click on line 20's run: Show in graph once, the page closed.
        var line20 = app.BlameGutterPoint(20);
        app.DoubleClickAt(line20.X, line20.Y);
        app.WaitFor(() => !app.IsAnyPageShown, "the page to close", StepTimeout);
        app.WaitFor(app.GraphHasKeyboardFocus, "the graph to take the keyboard focus", StepTimeout);
        WaitForDetails(app, AddTheParser.Subject, AddTheParser.Sha);
        Compare(app, "b6h-shown-in-graph");

        Assert.Equal(0, app.Close());
        await AssertGitUnchangedAsync(repo, refs);
    }

    /// <summary>
    /// Check B7's first method, steps 1 to 3 and 5 to 8, with the real mouse: "All files" clicked by
    /// id (<c>b7a-all-files</c>), LICENSE's row clicked by its name in the details' list (its blame
    /// in one click: <c>b7b-license-blame</c>), Esc and Read whole lines again (the setting kept),
    /// README.md's menu and Blame (<c>b7e-readme-blame</c>), then a real click on line 5, column 5
    /// of the text (the side branch's commit: <c>b7f-traced</c>). Step 4 and the restart (the
    /// setting is phase 1's settings.json) are the scripted run's.
    /// </summary>
    [Fact]
    public async Task Check_B7_all_files()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var app = Launch(repo);
        var refs = await RefsAsync(repo);

        // Step 1: Read whole lines (display row 1, below the working-changes row).
        var readWholeLines = RowPoint(app, ReadWholeLinesRow + 1, HistoryLanes);
        app.ClickAt(readWholeLines.X, readWholeLines.Y);
        WaitForDetails(app, ReadWholeLines.Subject, ReadWholeLines.Sha);
        app.WaitForText("ChangedFilesTitle", ReadWholeLines.FilesTitle, StepTimeout);

        // Step 2: All files.
        app.ClickWithMouse(app.Find("AllFilesButton"));
        app.WaitForText("ChangedFilesTitle", ReadWholeLinesAllFiles.FilesTitle, StepTimeout);
        WaitForShowAllFilesSetting(app, true);
        Compare(app, "b7a-all-files");

        // Step 3: LICENSE, an unchanged file: its blame at the commit in one click.
        app.ClickWithMouse(app.DetailsFileRow("LICENSE", StepTimeout));
        WaitForBlame(app, LicenseBlameTitle, At("c0bc2dd", "Read whole lines"), License);
        WaitForDetails(app, ReadWholeLines.Subject, ReadWholeLines.Sha);
        Assert.Empty(app.ShownElements("DiffView"));
        Compare(app, "b7b-license-blame");

        // Step 5: Esc; Read whole lines again keeps "All files".
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => !app.IsAnyPageShown, "the page to close", StepTimeout);
        readWholeLines = RowPoint(app, ReadWholeLinesRow + 1, HistoryLanes);
        app.ClickAt(readWholeLines.X, readWholeLines.Y);
        WaitForDetails(app, ReadWholeLines.Subject, ReadWholeLines.Sha);
        app.WaitForText("ChangedFilesTitle", ReadWholeLinesAllFiles.FilesTitle, StepTimeout);

        // Step 6: README.md's menu (every row's is ForCommitFile, 4.5).
        app.RightClickWithMouse(app.DetailsFileRow("README.md", StepTimeout));
        AssertMenu(app, CommitFileMenu("README.md"), FileMenuHeight);
        AssertFocusedItem(app, FileHistoryItem);

        // Step 7: Blame.
        app.ClickMenuItem(BlameItem);
        WaitForBlame(app, ReadmeBlameTitle, At("c0bc2dd", "Read whole lines"), Readme);
        Compare(app, "b7e-readme-blame");

        // Step 8: a real click on line 5, column 5 of the text: the side branch's commit.
        var point = app.BlameTextPoint(5, 5);
        AssertNear(new PointF((float)BlameColumnX(5), (float)BlameLineCentreY(5)), point, "Line 5, column 5 of the blame's text");
        app.ClickAt(point.X, point.Y);
        WaitForDetails(app, ExplainTheReaderAllFiles.Subject, ExplainTheReaderAllFiles.Sha);
        app.WaitForText("ChangedFilesTitle", ExplainTheReaderAllFiles.FilesTitle, StepTimeout);
        Compare(app, "b7f-traced");

        Assert.Equal(0, app.Close());
        await AssertGitUnchangedAsync(repo, refs);
    }

    /// <summary>
    /// Check B8, steps 1, 2, 4 to 8 and 11, with the real mouse and keyboard on a fresh "reflog" copy,
    /// after the clean-up's record: the Local branches section's menu, HEAD's reflog
    /// (<c>b8b-head-reflog</c>), a real click on <c>HEAD@{9}</c> (the lost d07c677 in the right panel),
    /// its menu with the disabled "Show in graph"'s tooltip drawn inside the window
    /// (<c>b8d-entry-menu</c>), "Create branch here…" with the name typed on the real keyboard and its
    /// box unticked, the row clicked again, and Esc: the graph with the new branch's row selected
    /// (<c>b8j-back-to-graph</c>). Steps 3, 9 and 10 are the scripted run's.
    /// </summary>
    [Fact]
    public async Task Check_B8_reflog_and_a_branch_from_a_lost_commit()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = Launch(repo);
        var name = NameOf(repo);
        WaitForCleanUp(app, name, CleanUpMessage);
        var refs = await RefsAsync(repo);
        var headLog = File.ReadAllLines(Path.Combine(repo.Path, ".git", "logs", "HEAD"));
        Assert.Equal(BackupsLeft, (await repo.BackupRefsAsync()).Select(entry => BackupRef.Parse(entry.Name).Stem).Distinct().Count());
        WaitForNothingSelected(app);

        // Step 1: the Local branches section's menu.
        app.RightClickWithMouse(app.RefRow("Local branches"));
        AssertMenu(app, LocalBranchesMenu, LocalBranchesMenuHeight);
        AssertFocusedItem(app, "Create branch…");

        // Step 2: Show reflog: HEAD's 36 entries newest first, the footer; the right panel unchanged.
        app.ClickMenuItem("Show reflog");
        app.WaitForPage("Reflog", ReflogOf("HEAD"), StepTimeout);
        app.WaitFor(
            () => app.PageRowNames("Reflog", "ReflogRow").Take(20).SequenceEqual(HeadReflog.Take(20).Select(row => row.Selector)),
            "HEAD's reflog's first 20 rows, HEAD@{0} to HEAD@{20}",
            StepTimeout);
        Assert.Equal(["HEAD"], app.NamesOnScreen("ReflogSource"));
        app.WaitFor(() => app.Texts().Contains(HeadFooter), "the reflog's footer", StepTimeout);
        AssertPageInPlaceOfGraph(app, "Reflog");
        WaitForNothingSelected(app);
        Assert.Equal(refs, await RefsAsync(repo));
        Compare(app, "b8b-head-reflog");

        // Step 4: a real click on HEAD@{9}, d07c677, a lost commit: the right panel shows it.
        AssertNear(new PointF((float)PageRowX, (float)PageRowCentreY(8)), app.PageRowPoint("Reflog", app.PageRow("Reflog", "ReflogRow", "HEAD@{9}")), "HEAD@{9}'s point");
        app.ClickPageRow("Reflog", "ReflogRow", "HEAD@{9}");
        WaitForDetails(app, TryAShortcut.Subject, TryAShortcut.Sha);

        // Step 5: its menu; the pointer rests on the disabled Show in graph until its tooltip shows.
        app.RightClickPageRow("Reflog", "ReflogRow", "HEAD@{9}");
        AssertMenu(app, ShortcutEntryMenu, EntryMenuHeight);
        AssertFocusedItem(app, CreateBranchHere);
        var tip = app.RestPointerOnMenuItem(ShowInGraph, NotInTheGraph);
        AssertToolTipInside(app, tip, NotInTheGraph);
        AssertFocusedItem(app, CreateBranchHere);
        Compare(app, "b8d-entry-menu", keepPointer: true);

        // Step 6: Create branch here…: phase 3's name dialog.
        app.ClickMenuItem(CreateBranchHere);
        app.WaitForText("NameDialogTitle", CreateBranchTitle, StepTimeout);
        Assert.Equal([ShortcutContext], app.NamesOnScreen("NameDialogContext"));
        Assert.True(app.Find("NameDialogOption").AsCheckBox().IsChecked == true, "\"Check out the new branch\" is not ticked.");
        Assert.False(app.Find("ConfirmButton").IsEnabled, "\"Create branch\" is enabled with no name.");

        // Step 7: shortcut typed on the real keyboard, not checked out.
        app.ReplaceText("NameBox", ShortcutName);
        app.ClickWithMouse(app.Find("NameDialogOption"));
        app.WaitFor(() => app.Find("NameDialogOption").AsCheckBox().IsChecked == false, "\"Check out the new branch\" to be unticked", StepTimeout);
        var create = ExpectOperation(app, name, CreateShortcut.Title);
        app.ClickWithMouse(app.Find("ConfirmButton"));
        create.Wait(OperationOutcome.Succeeded, CreateShortcut.Status);
        app.WaitUntilGone("NameDialog", StepTimeout);
        WaitForStatus(app, CreateShortcut.Status);
        AssertUndoRedo(app, true, PartU.Undo.ToolTip(CreateShortcut.UndoText), false, "Nothing to redo");
        Assert.Equal(ShortcutSha, await repo.RefTargetAsync("refs/heads/shortcut"));
        Assert.Equal([ShortcutReflog], await GitLinesAsync(repo, "log", "--walk-reflogs", "--format=%gs", "refs/heads/shortcut"));
        Assert.Equal(["## main...origin/main [ahead 2]"], await GitLinesAsync(repo, "status", "--porcelain", "--branch"));
        Assert.Equal(headLog, File.ReadAllLines(Path.Combine(repo.Path, ".git", "logs", "HEAD")));
        app.WaitForPage("Reflog", ReflogOf("HEAD"), StepTimeout);

        // Step 8: a real click on HEAD@{9} again: the graph has d07c677 now, selected behind the page.
        app.ClickPageRow("Reflog", "ReflogRow", "HEAD@{9}");
        WaitForDetails(app, TryAShortcut.Subject, TryAShortcut.Sha);

        // Step 11: Esc: the graph with shortcut's row selected and in view, the focus on the graph.
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => !app.IsAnyPageShown, "the page to close", StepTimeout);
        app.WaitFor(app.GraphHasKeyboardFocus, "the graph to take the keyboard focus", StepTimeout);
        WaitForDetails(app, TryAShortcut.Subject, TryAShortcut.Sha);
        Compare(app, "b8j-back-to-graph");

        Assert.Equal(0, app.Close());
        Assert.Equal(
            refs.Append("refs/heads/shortcut " + ShortcutSha).Order(StringComparer.Ordinal),
            (await RefsAsync(repo)).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Check B9's first method, step 1, with the real mouse after the clean-up's record: the Local
    /// branches section's header by RefRow, "Show backups" by name, the 15 rows by their BackupRow
    /// names in order, all in view (<c>b9a-backups</c>). The restores are the scripted run's.
    /// </summary>
    [Fact]
    public async Task Check_B9_backups_list()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var app = Launch(repo);
        var name = NameOf(repo);
        WaitForCleanUp(app, name, CleanUpMessage);
        var refs = await RefsAsync(repo);

        // Step 1: the backups list, its 15 rows newest first, all in view; the right panel unchanged.
        app.RightClickWithMouse(app.RefRow("Local branches"));
        app.ClickMenuItem("Show backups");
        app.WaitForPage("Reflog", BackupsTitle, StepTimeout);
        app.WaitForPageRows("Reflog", "BackupRow", [.. Backups.Select(backup => backup.Name)], StepTimeout);
        Assert.Equal([BackupsTitle], app.NamesOnScreen("ReflogSource"));
        Assert.Empty(app.PageRows("Reflog", "ReflogRow"));
        var last = app.BoundsOf(app.PageRows("Reflog", "BackupRow")[^1]);
        Assert.True(Math.Abs(last.Top + (last.Height / 2) - PageRowCentreY(AbortBackupRow)) <= 2, $"The last backup's row is at {last}; the report puts its centre at y {PageRowCentreY(AbortBackupRow)}.");
        AssertPageInPlaceOfGraph(app, "Reflog");
        WaitForNothingSelected(app);
        Compare(app, "b9a-backups");

        Assert.Equal(0, app.Close());
        Assert.Equal(refs, await RefsAsync(repo));
    }

    // ---- Part B's helpers ----

    /// <summary>A point within 2 logical pixels of the one the report states (the conventions' ±2).</summary>
    private static void AssertNear(PointF expected, PointF actual, string what) =>
        Assert.True(
            Math.Abs(expected.X - actual.X) <= 2 && Math.Abs(expected.Y - actual.Y) <= 2,
            $"{what} is at {actual}; the report says {expected} (±2).");

    /// <summary>The page <paramref name="pageId"/> in the graph area's place (BD1): x 260 to 700, y 88 to 674, ±2.</summary>
    private static void AssertPageInPlaceOfGraph(RealApp app, string pageId)
    {
        var page = app.BoundsOf(app.ShownElements(pageId).Single());
        Assert.True(
            Math.Abs(page.Left - PageBounds.Left) <= 2 && Math.Abs(page.Top - PageBounds.Top) <= 2
                && Math.Abs(page.Right - PageBounds.Right) <= 2 && Math.Abs(page.Bottom - PageBounds.Bottom) <= 2,
            $"The page {pageId} is at {page}; the report says {PageBounds} (±2).");
    }

    /// <summary>The history's header (4.1, BD24): the title, the folder <c>src</c>, and the <c>from {short}</c> text or none.</summary>
    private static void AssertHistoryHeader(RealApp app, string title, string? from)
    {
        app.WaitForPageText("FileHistory", "FileHistoryTitle", title, StepTimeout);
        Assert.Equal(SrcFolder, app.PageText("FileHistory", "FileHistoryFolder"));
        if (from is null)
        {
            Assert.Null(app.PageText("FileHistory", "FileHistoryFrom"));
        }
        else
        {
            app.WaitForPageText("FileHistory", "FileHistoryFrom", from, StepTimeout);
        }
    }

    /// <summary>
    /// Waits for the blame page <paramref name="title"/> with its caption and its text (the
    /// <c>BlameText</c> Document's Value, without its last line break) as given.
    /// </summary>
    private static void WaitForBlame(RealApp app, string title, string caption, string text)
    {
        app.WaitForPage("Blame", title, StepTimeout);
        app.WaitForPageText("Blame", "BlameCaption", caption, StepTimeout);
        app.WaitForPageText("Blame", "BlameTitle", title, StepTimeout);
        app.WaitFor(() => app.BlameTextValue().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') == text, $"the blame's text of {title}", StepTimeout);
    }

    /// <summary>Waits for the blame page <paramref name="title"/> with its caption and <paramref name="lines"/> lines of text.</summary>
    private static void WaitForBlameLines(RealApp app, string title, string caption, int lines)
    {
        app.WaitForPage("Blame", title, StepTimeout);
        app.WaitForPageText("Blame", "BlameCaption", caption, StepTimeout);
        app.WaitFor(() => app.BlameTextValue().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n').Length == lines, $"the blame of {title} {caption} with {lines} lines", StepTimeout);
    }

    /// <summary>src/Parser.cs's blame (B6 steps 7 and 10): the lines the report names.</summary>
    private static void AssertParserNamedLines(RealApp app)
    {
        var lines = app.BlameTextValue().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        foreach (var (line, text) in ParserNamedLines)
        {
            Assert.Equal(text, lines[line - 1]);
        }
    }

    /// <summary>The blame's layout at 1100×700 (4.2): the gutter x 260 to 390 and the text x 390 to 700, both from y 124 (±2).</summary>
    private static void AssertBlameLayout(RealApp app)
    {
        var gutter = app.BoundsOf(app.Find("BlameGutter"));
        var text = app.BoundsOf(app.Find("BlameText"));
        Assert.True(
            Math.Abs(gutter.Left - GutterBounds.Left) <= 2 && Math.Abs(gutter.Right - GutterBounds.Right) <= 2 && Math.Abs(gutter.Top - GutterBounds.Top) <= 2,
            $"The gutter is at {gutter}; the report says {GutterBounds} (±2).");
        Assert.True(
            Math.Abs(text.Left - BlameTextBounds.Left) <= 2 && Math.Abs(text.Right - BlameTextBounds.Right) <= 2 && Math.Abs(text.Top - BlameTextBounds.Top) <= 2,
            $"The blame's text is at {text}; the report says {BlameTextBounds} (±2).");
    }

    /// <summary>Waits until the data folder's settings.json says <c>"showAllFiles": value</c> (the store writes camelCase).</summary>
    private static void WaitForShowAllFilesSetting(RealApp app, bool value)
    {
        var file = Path.Combine(app.DataDirectory, "settings.json");
        var expected = $"\"showAllFiles\": {(value ? "true" : "false")}";
        app.WaitFor(
            () =>
            {
                try
                {
                    return File.Exists(file) && File.ReadAllText(file).Contains(expected, StringComparison.Ordinal);
                }
                catch (IOException)
                {
                    return false;
                }
            },
            $"settings.json to hold {expected}",
            StepTimeout);
    }

    /// <summary>Git unchanged by part B's history checks: every ref as before and the working tree's one change.</summary>
    private static async Task AssertGitUnchangedAsync(TempRepo repo, IReadOnlyList<string> refs)
    {
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(HistoryStatus, await GitLinesAsync(repo, "status", "--porcelain"));
    }
}
