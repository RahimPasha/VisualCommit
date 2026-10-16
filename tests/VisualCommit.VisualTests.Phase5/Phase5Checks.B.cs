// Part B of phase 5's scripted walk-through (docs/test-reports/phase-5.md, "Part B: the graph area's
// pages: file history, blame, "All files", the reflog and the backups"): checks B5 (a file's history
// across a rename) and B6 (the blame) here; B7 in Phase5Checks.B.AllFiles.cs, B8 in
// Phase5Checks.B.Reflog.cs, B9 in Phase5Checks.B.Backups.cs; what they share in
// Phase5Checks.B.Support.cs; their expected values in Phase5Expectations.B.cs (PartB). One test
// method per app start (phase 3's rule), named as the part's conventions name them.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.Views.Diff;
using VisualCommit.App.Views.Pages;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations.PartB;

namespace VisualCommit.VisualTests.Phase5;

/// <summary>The checks of part B: the graph area's pages and "All files".</summary>
public sealed partial class Phase5ChecksB : Phase5Checks
{
    /// <summary>
    /// Check B5, method 1: src/Reader.cs's history across its rename, from the stage panel's file,
    /// from a commit's file and from a diff's menu; the list walked with Down; Esc on a diff over
    /// the page; Show in graph; then, with the activity log open, Esc revealing the commit picked
    /// on the page. "history", dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_B5_file_history()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        var refs = await RefsAsync(repo);
        Assert.Equal(HistoryStatus, await StatusAsync(repo));

        // At the start: H0, nothing selected, the details placeholder.
        AssertGraph(app, H0, HistoryLanes, HistoryWorkingRow, showsDate: true);
        Assert.Equal((-1, false), (repository.SelectedIndex, repository.IsWorkingRowSelected));
        Assert.True(app.FindByAutomationId("DetailsPlaceholder").IsEffectivelyVisible, "The details placeholder does not show.");

        // Step 1: the working-changes row.
        app.Click(WorkingRowPointAsReported(app));
        await AssertStagePanelAsync(app, UnstagedOneFile, HistoryUnstagedRows, StagedNone, []);
        Assert.True(repository.IsWorkingRowSelected, "The working-changes row is not selected.");

        // Step 2: src/Reader.cs's menu in the stage panel.
        var pointer = app.BoundsOf(await app.FileRowAsync("UnstagedFileList", "src/Reader.cs")).Center;
        app.RightClick(pointer);
        var menu = app.Menu();
        var shot = Shot(app, "b5a-stage-file-menu");
        AssertMenuAsReported(app, menu, StageFileMenu, "Stage", StageFileMenuHeight, shot);

        // Step 3: File history, 4 rows, the first selected with its diff.
        app.ClickMenuItem(FileHistoryItem);
        await app.WaitForPageAsync<FileHistoryView>();
        await app.WaitForFileHistoryRowsAsync(4);
        await AssertHistoryDiffAsync(app, "M", "Reader.cs", SrcFolder, null, "c0bc2dd", ReadWholeLinesHunk);
        await app.WaitForAsync(() => app.FileHistorySnapshot().Selected?.HasFocus == true, "the history's first row to have the keyboard focus");
        shot = Shot(app, "b5b-history");
        await AssertNoMenuAsync(app, null);
        var page = app.FileHistorySnapshot();
        AssertPageInPlaceOfGraph(app, PageBounds, page.Bounds);
        AssertPageHeader(app, "FileHistoryHeader", page.Bounds, "IconHistory");
        AssertHistoryHeader(app, page, ReaderTitle, null);
        Assert.False(page.IsSideBySide);
        Assert.Equal((ContentTop, HistoryListBottom), (page.ListBounds.Top, page.ListBounds.Bottom));
        Assert.Equal(new Rect(260, HistoryListBottom, 440, 674 - HistoryListBottom), page.DiffBounds);
        AssertHistoryRows(app, page, ReaderHistory, selected: 0);
        AssertColour(Dark.Selection, shot, new Point(PageRowX, PageRowCentreY(0) + 8), "selected history row's background");
        await AssertStagePanelAsync(app, UnstagedOneFile, HistoryUnstagedRows, StagedNone, []);
        Assert.True(repository.IsWorkingRowSelected, "The working-changes row is no longer selected behind the page.");
        AssertNoClippedText(app);
        Assert.Equal(HistoryStatus, await StatusAsync(repo));

        // Step 4: the rename's row.
        app.Click(HistoryRowPointAsReported(app, 1));
        await AssertHistoryDiffAsync(app, "R", "Reader.cs", SrcFolder, "renamed from src/Parser.cs", "0575c7f", RenameHunk);
        await AssertDetailsAsync(app, Rename, RenameRow);
        shot = Shot(app, "b5c-rename-row");
        page = app.FileHistorySnapshot();
        AssertHistoryRows(app, page, ReaderHistory, selected: 1);
        Assert.Equal(RenameChanges, Changes(page.Diff!.TextDiff!));
        AssertNoClippedText(app);

        // Step 5: the details' src/Reader.cs opens its history from 0575c7f.
        pointer = app.BoundsOf(await app.FileRowAsync("FileList", "src/Reader.cs")).Center;
        app.RightClick(pointer);
        AssertMenuAsReported(app, app.Menu(), CommitFileMenu("Reader.cs"), FileHistoryItem, FileMenuHeight);
        app.ClickMenuItem(FileHistoryItem);
        await app.WaitForAsync(() => app.ShownPage is FileHistoryView && app.FileHistorySnapshot().From == From("0575c7f"), "the history from 0575c7f");
        await app.WaitForFileHistoryRowsAsync(3);
        await AssertHistoryDiffAsync(app, "R", "Reader.cs", SrcFolder, "renamed from src/Parser.cs", "0575c7f", RenameHunk);
        await app.WaitForAsync(() => app.FileHistorySnapshot().Selected?.HasFocus == true, "the history's first row to have the keyboard focus");
        Shot(app, "b5d-from-commit");
        page = app.FileHistorySnapshot();
        AssertHistoryHeader(app, page, ReaderTitle, From("0575c7f"));
        AssertHistoryRows(app, page, ReaderHistoryFromRename, selected: 0);
        await AssertDetailsAsync(app, Rename, RenameRow);
        AssertNoClippedText(app);

        // Step 6: Down, Down, then Up.
        app.PressKey(Key.Down);
        await AssertHistoryDiffAsync(app, "M", "Parser.cs", SrcFolder, null, "365482b", HandleEmptyInputHunk);
        await AssertDetailsAsync(app, HandleEmptyInput, HandleEmptyInputRow);
        app.PressKey(Key.Down);
        await AssertHistoryDiffAsync(app, "A", "Parser.cs", SrcFolder, null, "1d5a7cf", AddTheParserHunk);
        await AssertDetailsAsync(app, AddTheParser, AddTheParserRow);
        Shot(app, "b5e-walked");
        AssertNoClippedText(app);
        page = app.FileHistorySnapshot();
        AssertHistoryRows(app, page, ReaderHistoryFromRename, selected: 2);
        var added = Assert.Single(page.Diff!.TextDiff!.Hunks).Lines;
        Assert.Equal(AddTheParserRows, added.Count);
        Assert.All(added, line => Assert.Equal(DiffLineKind.Added, line.Kind));
        Assert.Equal(FirstLine, added[0].Text);
        app.PressKey(Key.Up);
        await AssertHistoryDiffAsync(app, "M", "Parser.cs", SrcFolder, null, "365482b", HandleEmptyInputHunk);
        await AssertDetailsAsync(app, HandleEmptyInput, HandleEmptyInputRow);
        AssertHistoryRows(app, app.FileHistorySnapshot(), ReaderHistoryFromRename, selected: 1);
        Assert.Equal(HandleEmptyInputChanges, Changes(app.FileHistorySnapshot().Diff!.TextDiff!));

        // Step 7: the details' src/Parser.cs opens phase 2's diff over the page; Esc closes only it.
        app.Click(await app.FileRowAsync("FileList", "src/Parser.cs"));
        await app.WaitForAsync(() => repository.Diff?.ShowsText == true && app.ShownPage is null, "phase 2's diff over the page");
        var mainDiff = (DiffView)app.FindByAutomationId("DiffView");
        Assert.Equal(("M", "Parser.cs", SrcFolder, "365482b"), (repository.Diff!.StatusLetter, repository.Diff.FileName, repository.Diff.Folder, repository.Diff.SideLabel));
        Assert.Equal("365482b", app.Find<TextBlock>("DiffSideLabel").Text);
        Assert.True(app.FindByAutomationId(mainDiff, "CloseDiffButton").IsEffectivelyVisible, "Phase 2's diff has no close button.");
        Assert.Equal(HandleEmptyInputHunk.Header, Assert.Single(repository.Diff.TextDiff!.Hunks).Header);
        Assert.True(app.DetailsFileRows().Single(row => row.Name == "Parser.cs").IsSelected, "The file row is not selected.");
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is FileHistoryView && app.FileHistorySnapshot().ListHasFocus, "the page back with the focus in its list");
        Assert.False(repository.IsDiffOpen);
        page = app.FileHistorySnapshot();
        AssertHistoryRows(app, page, ReaderHistoryFromRename, selected: 1);
        app.Click(await app.FileRowAsync("FileList", "src/Parser.cs"));
        await app.WaitForAsync(() => repository.Diff?.ShowsText == true && app.ShownPage is null, "phase 2's diff over the page again");
        var line10 = app.DiffRowOfLine(10);
        Assert.True(line10 >= 0, "The diff has no row of new line 10.");
        var cell = app.Find<DiffTextView>("DiffText").Rows[line10].Left!;
        Assert.Equal((DiffLineKind.Added, (int?)null, (int?)10, ParserNewLine10), (cell.Kind, cell.OldNumber, cell.NewNumber, cell.Text));
        pointer = app.DiffTextPoint(line10);
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "b5f-diff-menu");
        AssertMenuAsReported(app, menu, ParserDiffMenu, FileHistoryItem, FileMenuHeight, shot);

        // Step 8: File history from the diff's menu: the diff closes, the history from 365482b.
        app.ClickMenuItem(FileHistoryItem);
        await app.WaitForAsync(() => app.ShownPage is FileHistoryView && app.FileHistorySnapshot().Title == ParserTitle, "the history of Parser.cs");
        await app.WaitForFileHistoryRowsAsync(2);
        await AssertHistoryDiffAsync(app, "M", "Parser.cs", SrcFolder, null, "365482b", HandleEmptyInputHunk);
        await app.WaitForAsync(() => app.FileHistorySnapshot().Selected?.HasFocus == true, "the history's first row to have the keyboard focus");
        Shot(app, "b5g-from-diff");
        AssertNoClippedText(app);
        Assert.False(repository.IsDiffOpen, "The diff is still open.");
        page = app.FileHistorySnapshot();
        AssertHistoryHeader(app, page, ParserTitle, From("365482b"));
        AssertHistoryRows(app, page, ParserHistory, selected: 0);
        await AssertDetailsAsync(app, HandleEmptyInput, HandleEmptyInputRow);

        // Step 9: the row menu of Add the parser, which the right-click selects first (BD9).
        app.RightClick(HistoryRowPointAsReported(app, 1));
        menu = app.Menu();
        await AssertHistoryDiffAsync(app, "A", "Parser.cs", SrcFolder, null, "1d5a7cf", AddTheParserHunk);
        await AssertDetailsAsync(app, AddTheParser, AddTheParserRow);
        shot = Shot(app, "b5h-row-menu");
        AssertMenuAsReported(app, menu, HistoryRowMenu("1d5a7cf", "Add the parser"), ShowInGraph, HistoryRowMenuHeight, shot);
        Assert.Equal(new Point(422, 165), menu.Bounds.TopLeft, PointWithin(2));
        Assert.True(app.FileHistorySnapshot().Rows[1].IsSelected, "The right-click did not select the row.");

        // Step 10: Show in graph.
        app.ClickMenuItem(ShowInGraph);
        await app.WaitForAsync(() => app.ShownPage is null && repository.ShowsGraph, "the page to close");
        await app.WaitForAsync(() => Graph(app).IsFocused, "the graph to take the focus");
        Shot(app, "b5i-shown-in-graph");
        AssertNoClippedText(app);
        await AssertNoMenuAsync(app, null);
        Assert.Null(repository.Page);
        AssertGraph(app, H0, HistoryLanes, HistoryWorkingRow, showsDate: true);
        await AssertDetailsAsync(app, AddTheParser, AddTheParserRow);
        AssertRowWhollyInView(app, AddTheParserRow, 467);

        // Step 11: with the activity log open, the commit picked on the page is revealed on the way back.
        app.ClickActivityLogToggle();
        await app.WaitForAsync(() => app.ActivityLogView.IsEffectivelyVisible && app.BoundsOf(app.ActivityLogView).Height > 0, "the activity log to open");
        Assert.Equal(new Rect(260, 474, 440, 200), app.BoundsOf(app.ActivityLogView));
        Assert.Equal((469, 474), (app.BoundsOf(app.FindByAutomationId("ActivityLogSplitter")).Top, app.BoundsOf(app.FindByAutomationId("ActivityLogSplitter")).Bottom));
        Assert.Empty(app.ActivityLog().Rows);
        var graphBounds = app.BoundsOf(Graph(app));
        Assert.Equal((116, 469), (graphBounds.Top, graphBounds.Bottom));
        Assert.Equal(new Rect(260, 454, 440, 26), RowInWindow(app, AddTheParserRow));
        Assert.Equal(480 - 469, RowInWindow(app, AddTheParserRow).Bottom - graphBounds.Bottom);
        app.Click(WorkingRowPointAsReported(app));
        app.RightClick(await app.FileRowAsync("UnstagedFileList", "src/Reader.cs"));
        app.ClickMenuItem(FileHistoryItem);
        await app.WaitForAsync(() => app.ShownPage is FileHistoryView && app.FileHistorySnapshot().From is null && app.FileHistorySnapshot().Title == ReaderTitle, "the history of Reader.cs from HEAD");
        await app.WaitForFileHistoryRowsAsync(4);
        await AssertHistoryDiffAsync(app, "M", "Reader.cs", SrcFolder, null, "c0bc2dd", ReadWholeLinesHunk);
        page = app.FileHistorySnapshot();
        Assert.Equal(new Rect(260, 88, 440, 469 - 88), page.Bounds);
        Assert.Equal(new Rect(260, 88, 440, HeaderHeight), page.HeaderBounds);
        Assert.Equal((ContentTop, 262), (page.ListBounds.Top, page.ListBounds.Bottom));
        Assert.Equal((262, 469), (page.DiffBounds.Top, page.DiffBounds.Bottom));
        AssertHistoryRows(app, page, ReaderHistory, selected: 0);
        app.Click(HistoryRowPointAsReported(app, 3));
        await AssertDetailsAsync(app, AddTheParser, AddTheParserRow);
        AssertHistoryRows(app, app.FileHistorySnapshot(), ReaderHistory, selected: 3);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null && repository.ShowsGraph, "the page to close");
        await app.WaitForAsync(() => Graph(app).IsFocused, "the graph to take the focus");
        shot = Shot(app, "b5j-back-to-graph");
        AssertNoClippedText(app);
        Assert.True(app.ActivityLogView.IsEffectivelyVisible, "The activity log closed with the page.");
        Assert.Equal(37, Graph(app).ScrollOffset, 0.5);
        Assert.Equal(new Rect(260, 417, 440, 26), RowInWindow(app, AddTheParserRow));
        Assert.Equal(new Rect(260, 443, 440, 26), RowInWindow(app, InitialCommitRow));
        AssertColour(Dark.Selection, shot, new Point(PageRowX, 430), "revealed row's background");
        await AssertDetailsAsync(app, AddTheParser, AddTheParserRow);

        // Git unchanged throughout; no operation ran.
        await AssertGitUnchangedAsync(repo, refs, HistoryStatus);
        Assert.DoesNotContain(refs, line => line.StartsWith("refs/visualcommit/", StringComparison.Ordinal));
        AssertNoOperationRan(app, repo);
    }

    /// <summary>Check B5, method 2: the history beside its diff at 1920×1080 in the light theme (BD2, BD24).</summary>
    [AvaloniaFact]
    public async Task Check_B5_side_by_side_light()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, FullSize.Width, FullSize.Height, AppTheme.Light);
        var palette = Light;

        // Step 12: Read whole lines' src/Reader.cs, its history from HEAD's own commit, the rename's row.
        app.Click(GraphRowPointAsReported(app, ReadWholeLinesRow, 155));
        await AssertDetailsAsync(app, ReadWholeLines, ReadWholeLinesRow);
        app.RightClick(await app.FileRowAsync("FileList", "src/Reader.cs"));
        app.ClickMenuItem(FileHistoryItem);
        await app.WaitForPageAsync<FileHistoryView>();
        await app.WaitForFileHistoryRowsAsync(4);
        await AssertHistoryDiffAsync(app, "M", "Reader.cs", SrcFolder, null, "c0bc2dd", ReadWholeLinesHunk);
        app.Click(HistoryRowPointAsReported(app, 1));
        await AssertHistoryDiffAsync(app, "R", "Reader.cs", SrcFolder, "renamed from src/Parser.cs", "0575c7f", RenameHunk);
        await AssertDetailsAsync(app, Rename, RenameRow);
        var shot = Shot(app, "b5k-side-by-side-light");

        var page = app.FileHistorySnapshot();
        AssertPageInPlaceOfGraph(app, FullPageBounds, page.Bounds);
        AssertPageHeader(app, "FileHistoryHeader", page.Bounds, "IconHistory");
        AssertHistoryHeader(app, page, ReaderTitle, null);
        Assert.True(page.IsSideBySide, "The list and the diff are not side by side.");
        Assert.Equal((260, SideBySideListRight, ContentTop), (page.ListBounds.Left, page.ListBounds.Right, page.ListBounds.Top));
        Assert.Equal((SideBySideListRight, 1520, ContentTop), (page.DiffBounds.Left, page.DiffBounds.Right, page.DiffBounds.Top));
        AssertHistoryRows(app, page, ReaderHistory, selected: 1, authorShown: true, dateShown: false);
        Assert.Equal(palette.Accent, page.Rows[1].LetterColor);
        Assert.Equal(palette.Selection, page.Rows[1].Background);
        AssertColour(palette.Selection, shot, new Point(PageRowX, PageRowCentreY(1) + 8), "selected history row's background");
        Assert.Equal("Mira Kovac", TextsIn(app.Find<ListBox>("FileHistoryList").ContainerFromIndex(1)!).Last());
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check B6: the blame of src/Reader.cs in the working tree, its runs, a run's tooltip, a click
    /// on a run in the gutter and on its text, Blame before this commit across the rename and its
    /// disabled reason on the first commit, File history and the history's Blame, a double-click on
    /// a run. "history", dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_B6_blame()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        var refs = await RefsAsync(repo);
        var palette = Dark;

        // Step 1: the working tree's blame from the stage panel's menu.
        app.Click(WorkingRowPointAsReported(app));
        await AssertStagePanelAsync(app, UnstagedOneFile, HistoryUnstagedRows, StagedNone, []);
        app.RightClick(await app.FileRowAsync("UnstagedFileList", "src/Reader.cs"));
        app.ClickMenuItem(BlameItem);
        var blame = await WaitForBlameAsync(app, ReaderBlameTitle, InTheWorkingTree, 24);
        await app.WaitForAsync(() => app.BlameSnapshot().TextHasFocus, "the blame's text to take the keyboard focus");
        var shot = Shot(app, "b6a-blame-working-tree");
        blame = app.BlameSnapshot();
        AssertPageInPlaceOfGraph(app, PageBounds, blame.Bounds);
        AssertPageHeader(app, "BlameHeader", blame.Bounds, "IconBlame");
        AssertBlameLayout(app, blame, highlighted: true);
        Assert.Equal(ReaderWorkingTree, blame.Text.TrimEnd('\n'));
        AssertRuns(blame, ReaderWorkingTreeRuns);
        AssertGutterColours(shot, blame, palette);
        AssertLineNumbers(shot, blame, 24);
        await AssertStagePanelAsync(app, UnstagedOneFile, HistoryUnstagedRows, StagedNone, []);
        Assert.True(repository.IsWorkingRowSelected, "The working-changes row is not selected behind the page.");
        AssertNoClippedText(app);

        // Step 2: the tooltip of line 1's run (BD11).
        var line1 = app.BlameGutterPoint(1);
        Assert.Equal(new Point(GutterPointX, BlameLineCentreY(1)), line1, PointWithin(0.5));
        Assert.Equal(AddTheParserRunToolTip, await app.RestPointerAtAsync(line1));
        Shot(app, "b6b-run-tooltip", keepPointer: true);
        AssertToolTipShown(app, AddTheParserRunToolTip);

        // Step 3: line 22's tooltip; the menu of line 3, a run not committed yet.
        Assert.Equal(NotCommittedYet, await app.RestPointerAtAsync(GutterPointAsReported(app, 22)));
        app.RightClick(GutterPointAsReported(app, 3));
        AssertMenuAsReported(app, app.Menu(), UncommittedRunMenu, ShowInGraph, UncommittedRunMenuHeight);
        Assert.True(repository.IsWorkingRowSelected, "The right-click on a run not committed yet did not select the working-changes row.");
        await AssertStagePanelAsync(app, UnstagedOneFile, HistoryUnstagedRows, StagedNone, []);
        await CloseMenuAsync(app);

        // Step 4: a click on line 13 in the gutter.
        app.Click(GutterPointAsReported(app, 13));
        await AssertDetailsAsync(app, ReadWholeLines, ReadWholeLinesRow);
        shot = Shot(app, "b6c-run-clicked");
        AssertNoClippedText(app);
        AssertRuns(app.BlameSnapshot(), ReaderWorkingTreeRuns, selected: 8);
        AssertColour(palette.Selection, shot, new Point(GutterBounds.Right - 2, BlameLineCentreY(13)), "line 13's gutter");
        AssertColour(palette.Panel, shot, new Point(GutterBounds.Right - 2, BlameLineCentreY(1)), "line 1's gutter");

        // Step 5: a click on line 10, column 9 of the text.
        var text10 = app.BlameTextPoint(10, 9);
        Assert.Equal(new Point(BlameColumnX(9), BlameLineCentreY(10)), text10, PointWithin(2));
        app.Click(text10);
        await AssertDetailsAsync(app, HandleEmptyInput, HandleEmptyInputRow);
        AssertRuns(app.BlameSnapshot(), ReaderWorkingTreeRuns, selected: 6);

        // Step 6: the menu of line 4's run, which the right-click selects first (BD9).
        app.RightClick(GutterPointAsReported(app, 4));
        var menu = app.Menu();
        await AssertDetailsAsync(app, Rename, RenameRow);
        shot = Shot(app, "b6d-run-menu");
        AssertMenuAsReported(app, menu, RunMenu("0575c7f", "Rename the parser to reader"), ShowInGraph, RunMenuHeight, shot);
        Assert.Equal(new Point(302, 181.4), menu.Bounds.TopLeft, PointWithin(2));
        AssertRuns(app.BlameSnapshot(), ReaderWorkingTreeRuns, selected: 2);

        // Step 7: Blame before this commit: src/Parser.cs at 14a5c6c, across the rename.
        app.ClickMenuItem(BlameBeforeThisCommit);
        blame = await WaitForBlameAsync(app, ParserBlameTitle, At("14a5c6c", "Add tracing"), ParserLines);
        Shot(app, "b6e-blame-before");
        AssertBlameLayout(app, blame);
        AssertParserBlame(blame);
        await AssertDetailsAsync(app, Rename, RenameRow);
        AssertNoClippedText(app);

        // Step 8: line 1's run is the file's first commit: Blame before this commit is disabled.
        app.RightClick(GutterPointAsReported(app, 1));
        menu = app.Menu();
        await AssertDetailsAsync(app, AddTheParser, AddTheParserRow);
        Assert.Equal(FilesFirstCommit, await app.RestPointerOnMenuItemAsync(BlameBeforeThisCommit));
        shot = Shot(app, "b6f-first-commit", keepPointer: true);
        AssertMenuAsReported(app, app.Menu(), RunMenu("1d5a7cf", "Add the parser", FilesFirstCommit), ShowInGraph, RunMenuHeight);
        AssertToolTipShown(app, FilesFirstCommit);
        await CloseMenuAsync(app);

        // Step 9: File history from the blame: src/Parser.cs from 14a5c6c.
        app.Click(app.FindByAutomationId("BlameHistory"));
        await app.WaitForAsync(() => app.ShownPage is FileHistoryView && app.FileHistorySnapshot().Title == ParserTitle, "the history of Parser.cs");
        await app.WaitForFileHistoryRowsAsync(2);
        await AssertHistoryDiffAsync(app, "M", "Parser.cs", SrcFolder, null, "365482b", HandleEmptyInputHunk);
        await app.WaitForAsync(() => app.FileHistorySnapshot().Selected?.HasFocus == true, "the history's first row to have the keyboard focus");
        Shot(app, "b6g-file-history");
        AssertNoClippedText(app);
        var page = app.FileHistorySnapshot();
        AssertHistoryHeader(app, page, ParserTitle, From("14a5c6c"));
        AssertHistoryRows(app, page, ParserHistory, selected: 0);
        await AssertDetailsAsync(app, AddTheParser, AddTheParserRow);

        // Step 10: the history's Blame: src/Parser.cs at 365482b.
        app.Click(app.FindByAutomationId("FileHistoryBlame"));
        blame = await WaitForBlameAsync(app, ParserBlameTitle, At("365482b", "Handle empty input"), ParserLines);
        AssertParserBlame(blame);

        // Step 11: a double-click on line 20's run shows its commit in the graph.
        app.DoubleClick(GutterPointAsReported(app, 20));
        await app.WaitForAsync(() => app.ShownPage is null && repository.ShowsGraph, "the page to close");
        await app.WaitForAsync(() => Graph(app).IsFocused, "the graph to take the focus");
        Shot(app, "b6h-shown-in-graph");
        AssertNoClippedText(app);
        Assert.Null(repository.Page);
        AssertGraph(app, H0, HistoryLanes, HistoryWorkingRow, showsDate: true);
        await AssertDetailsAsync(app, AddTheParser, AddTheParserRow);
        AssertRowWhollyInView(app, AddTheParserRow, 467);

        // Git unchanged throughout; no operation ran.
        await AssertGitUnchangedAsync(repo, refs, HistoryStatus);
        AssertNoOperationRan(app, repo);
    }

    /// <summary>Asserts src/Parser.cs's blame at 14a5c6c or 365482b: 22 lines, the lines the report names, the 5 runs.</summary>
    private static void AssertParserBlame(BlameSnapshot blame)
    {
        var lines = blame.Text.TrimEnd('\n').Split('\n');
        Assert.Equal(ParserLines, lines.Length);
        foreach (var (line, text) in ParserNamedLines)
        {
            Assert.Equal(text, lines[line - 1]);
        }

        AssertRuns(blame, ParserRuns);
    }

    /// <summary>
    /// Asserts in the picture what the gutter draws (BD12): each run's background at its first
    /// line's right end, the short SHA in the primary text colour, the date in the secondary one,
    /// <c>Not committed yet</c> in the warning colour; and on each of the run's other lines
    /// nothing but its background (line 23, the second of run 13's, too).
    /// </summary>
    private static void AssertGutterColours(Screenshot picture, BlameSnapshot blame, Palette palette)
    {
        foreach (var run in blame.Runs)
        {
            var top = blame.GutterBounds.Top + ((run.FirstLine - 1) * BlameLineHeight);
            var background = run.Background switch
            {
                "VcSelectionBrush" => palette.Selection,
                "VcPanelBackgroundBrush" => palette.Panel,
                _ => palette.Window,
            };
            AssertColour(background, picture, new Point(blame.GutterBounds.Right - 2, top + (BlameLineHeight / 2)), $"run {run.Index}'s background");
            foreach (var part in run.Texts)
            {
                var colour = part.Text == NotCommittedYet ? palette.Warning : part.Text == GutterDate ? palette.TextSecondary : palette.TextPrimary;
                AssertDrawnIn(picture, new Rect(part.X, top, part.Width, BlameLineHeight), colour, $"run {run.Index}'s \"{part.Text}\"");
            }

            for (var line = run.FirstLine + 1; line <= run.LastLine; line++)
            {
                var lineTop = blame.GutterBounds.Top + ((line - 1) * BlameLineHeight);
                AssertBlank(picture, new Rect(blame.GutterBounds.Left + 1, lineTop + 2, blame.GutterBounds.Width - 2, BlameLineHeight - 4), background, $"the gutter of line {line}, run {run.Index}'s");
            }
        }
    }
}
