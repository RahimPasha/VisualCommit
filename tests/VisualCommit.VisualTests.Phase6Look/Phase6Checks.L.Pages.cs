// Checks L28 and L29 of part L (docs/test-reports/phase-6.md): the sweep's changes, pages and the
// graph's views on "history" with docs/todo.md (the diff in C#'s colours, the restore bar and its
// Restore, the blame, a comparison, All files, a branch hidden and another soloed), and the reflog
// and backups pages on "reflog", each in both themes by one pair. The part's map is in
// Phase6Checks.L.cs.
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Blame;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Pages;
using VisualCommit.Core;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6Look;

public sealed partial class Phase6ChecksL
{
    /// <summary>
    /// Check L28: "history" with docs/todo.md untracked (H0t, LD7): src/Reader.cs's working change
    /// (its stage-panel row and inline diff in C#'s colours); the restore bar after its discard,
    /// then Restore as the discard's Undo; its blame; two commits compared; All files; feature/compare
    /// hidden and main soloed; each in both themes (11.10, 11.13, 11.14; LD7 to LD10). System with the
    /// fake dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L28_changes_pages_and_views_in_both_themes()
    {
        using var repo = await Scenarios.HistoryAsync();
        repo.WriteFile(PartL.TodoPath, PartL.Todo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, PartL.Size.Width, PartL.Size.Height, AppTheme.System, systemDark: true);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var refs = await RefsAsync(repo);
        var config = File.ReadAllText(Path.Combine(repo.Path, ".git", "config"));

        // At the start: H0t; Undo "Nothing to undo".
        AssertSystemDarkStart(app);
        AssertGraph(app, PartL.H0, 2, PartL.WorkingRow);
        Assert.Equal(PartL.H0tStatus, await StatusAsync(repo));
        Assert.Equal(UndoRedoButtons.Nothing, app.UndoRedoState());

        // Step 1: the working-changes row; src/Reader.cs's diff.
        app.Click(Near(app.WorkingRowPoint(), PartL.HistoryWorkingRowPoint, "the working-changes row"));
        await WaitForStagePanelAsync(app, 2, 0);
        app.Click(await app.FileRowAsync("UnstagedFileList", PartL.ReaderPath));
        await app.WaitForAsync(() => repository.IsDiffOpen && ShownDiffText(app) is { } view && view.Rows.Count(row => row.Kind == DiffRowKind.HunkHeader) == 2 && view.RowBounds(0) is not null, "src/Reader.cs's diff with its two hunks");
        await app.WaitForIdleAsync();
        var readerRow = await app.FileRowAsync("UnstagedFileList", PartL.ReaderPath);
        var todoRow = await app.FileRowAsync("UnstagedFileList", PartL.TodoPath);
        await ShotInBothThemesAsync(app, "l28a-diff", assertEach: (picture, palette) =>
        {
            Assert.Equal(PartL.H0tUnstaged, FileRows(app, "UnstagedFileList"));
            Assert.Equal(("Unstaged files (2)", "Staged files (0)"), (app.Find<TextBlock>("UnstagedTitle").Text, app.Find<TextBlock>("StagedTitle").Text));
            Assert.Empty(FileRows(app, "StagedFileList"));
            var readerBackground = picture.PixelAt(new Point(app.BoundsOf(readerRow).Right - 4, app.BoundsOf(readerRow).Top + 2));
            Assert.Equal(palette.Selection, readerBackground);
            AssertInk(picture, app.BoundsOf(LetterOf(readerRow)), palette.Warning, palette.Selection, "Reader.cs's M (VcWarningBrush on the selected row, 11.10)");
            var todoBackground = picture.PixelAt(new Point(app.BoundsOf(todoRow).Right - 4, app.BoundsOf(todoRow).Top + 2));
            AssertInk(picture, app.BoundsOf(LetterOf(todoRow)), palette.Success, todoBackground, "todo.md's A (VcSuccessBrush, 11.10)");
            AssertDiffView(app, picture, palette);
        });

        // Step 2: Discard file, its question, Discard: H0d with the restore bar.
        var mark = app.LogMark();
        app.Click(app.FindByAutomationId("DiscardFileButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal(PartL.DiscardTitle, DialogText(app, "ConfirmTitle"));
        Assert.Equal(PartL.DiscardReaderQuestion, DialogText(app, "ConfirmQuestion"));
        app.Click(InDialog<Button>(app, "ConfirmButton"));
        await app.WaitForLogLineAsync(new Regex(@"\] history: [^:]+: Succeeded after \d+ ms", RegexOptions.CultureInvariant), mark: mark);
        await app.WaitForAsync(() => repository.Changes.RestoreText == PartL.RestoreBarReader && !repository.IsDiffOpen, "the restore bar and the diff closed");
        await app.WaitForIdleAsync();
        await WaitForStagePanelAsync(app, 1, 0);
        Assert.Equal(PartL.H0dStatus, await StatusAsync(repo));
        Assert.Equal(PartL.ReaderCommittedBlob, await WorkingBlobAsync(repo, PartL.ReaderPath));
        Assert.Equal([(PartL.DiscardBackup, PartL.DiscardSnapshot)], (await repo.BackupRefsAsync()).Select(entry => (entry.Name, entry.Sha)));
        await ShotInBothThemesAsync(app, "l28b-restore-bar", assertEach: (picture, palette) =>
        {
            Assert.False(repository.IsDiffOpen, "The diff is still open.");
            AssertGraph(app, PartL.H0, 2, PartL.WorkingRow);
            var bar = app.Find<Border>("RestoreBar");
            Assert.True(bar.IsEffectivelyVisible, "The restore bar does not show.");
            Assert.Equal(40, app.BoundsOf(bar).Height, 0.5);
            Assert.Equal(palette.Control, (bar.Background as ISolidColorBrush)?.Color);
            AssertColour(palette.Control, picture, new Point(app.BoundsOf(bar).Right - 60, app.BoundsOf(bar).Top + 3), "restore bar's background (VcControlBackgroundBrush)");
            Assert.Equal(PartL.RestoreBarReader, app.Find<TextBlock>("RestoreBarText").Text);
            Assert.True(app.FindByAutomationId("RestoreDiscardButton").IsEffectivelyVisible && app.FindByAutomationId("DismissRestoreButton").IsEffectivelyVisible, "The bar's Restore or close button does not show.");
            Assert.Equal(PartL.H0dUnstaged, FileRows(app, "UnstagedFileList"));
            Assert.Equal(UndoRedoButtons.CanUndo(PartL.DiscardReaderText), app.UndoRedoState());
            AssertColour(palette.Lane(1), picture, PartL.HistoryNode4, "row 4's node (lane colour 1)");
            AssertColour(palette.Lane(2), picture, PartL.HistoryNode6, "row 6's node (lane colour 2)");
        });

        // Restore: the discard's own Undo, with no question.
        mark = app.LogMark();
        app.Click(app.FindByAutomationId("RestoreDiscardButton"));
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded("history", PartL.UndoDiscardTitle, OperationOutcome.Succeeded, PartL.UndidDiscard), mark: mark);
        await app.WaitForIdleAsync();
        await AssertNoDialogAsync(app);
        AssertStatus(app, PartL.UndidDiscard);
        Assert.Equal(PartL.H0tStatus, await StatusAsync(repo));
        Assert.Equal(PartL.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartL.ReaderPath));
        Assert.Equal([PartL.DiscardBackup], (await repo.BackupRefsAsync()).Select(entry => entry.Name));
        Assert.Null(repository.Changes.RestoreText);
        await WaitForStagePanelAsync(app, 2, 0);
        Assert.Equal(PartL.H0tUnstaged, FileRows(app, "UnstagedFileList"));
        Assert.Equal(UndoRedoButtons.CanRedo(PartL.DiscardReaderText), app.UndoRedoState());

        // Step 3: Reader.cs's file menu, Blame: the page with 24 lines.
        app.RightClick(await app.FileRowAsync("UnstagedFileList", PartL.ReaderPath));
        var fileMenu = app.Menu();
        Assert.Contains("File history", fileMenu.Items.Select(item => item.Label));
        Assert.Contains("Blame", fileMenu.Items.Select(item => item.Label));
        app.ClickMenuItem("Blame");
        await app.WaitForPageAsync<BlameView>();
        await app.WaitForAsync(() => app.BlameSnapshot() is { IsLoading: false } blame && blame.Text.Split('\n').Length >= PartL.BlameLines && blame.Runs.Count == PartL.BlameRuns, "the blame's 24 lines and 15 runs");
        await ShotInBothThemesAsync(app, "l28c-blame", assertEach: (picture, palette) => AssertBlame(app, picture, palette));
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null, "the blame page to close");
        AssertGraph(app, PartL.H0, 2, PartL.WorkingRow);

        // Step 4: row 7, then a command-click on row 0: the comparison.
        app.Click(Near(app.GraphRowPoint(7), PartL.HistoryRowPoint(7), "row 7"));
        await app.WaitForAsync(() => repository.SelectedIndex == 7, "row 7 to be selected");
        app.Click(Near(app.GraphRowPoint(0), PartL.HistoryRowPoint(0), "row 0"), app.CommandModifier);
        await app.WaitForAsync(() => app.Compare() is { IsLoading: false, FilesTitle: not null }, "the comparison of rows 7 and 0");
        await app.WaitForIdleAsync();
        await ShotInBothThemesAsync(app, "l28d-compare", assertEach: (picture, palette) =>
        {
            Assert.Equal(new[] { 0, 7 }.Select(row => repository.Graph.CommitAt(row).Sha).Order(StringComparer.Ordinal), repository.Selection.Shas.Order(StringComparer.Ordinal));
            AssertColour(palette.Selection, picture, HistoryRowBackground(0), "row 0's background (VcSelectionBrush)");
            AssertColour(palette.Selection, picture, HistoryRowBackground(7), "row 7's background (VcSelectionBrush)");
            var compare = app.Compare() ?? throw new Xunit.Sdk.XunitException("No comparison shows.");
            Assert.Equal("Compare", compare.Title);
            Assert.Equal((PartL.CompareFrom, true, PartL.CompareTo, true), (compare.From, compare.FromEnabled, compare.To, compare.ToEnabled));
            Assert.True(compare.SwapShown, "The swap button does not show.");
            Assert.Equal(PartL.ChangedFiles4, compare.FilesTitle);
            Assert.Equal(PartL.ComparedFiles, compare.Files.Select(DescribeFile));
            AssertFileLetters(app, picture, palette, "CompareFileList");
            AssertColour(palette.Lane(1), picture, PartL.HistoryNode4, "row 4's node (lane colour 1)");
            AssertColour(palette.Lane(2), picture, PartL.HistoryNode6, "row 6's node (lane colour 2)");
        });

        // Step 5: row 0 alone; its details; All files.
        app.Click(Near(app.GraphRowPoint(0), PartL.HistoryRowPoint(0), "row 0"));
        await app.WaitForAsync(() => repository.SelectedIndex == 0 && repository.Selection.Count == 1, "row 0 alone to be selected");
        app.WaitForCommitDetails();
        app.Click(app.FindByAutomationId("AllFilesButton"));
        await app.WaitForAsync(() => app.DetailsFileRows().Count == PartL.AllFilesOfReadWholeLines.Count, "All files' six rows");
        await ShotInBothThemesAsync(app, "l28e-all-files", assertEach: (picture, palette) =>
        {
            Assert.Equal(0, repository.SelectedIndex);
            Assert.Equal(1, repository.Selection.Count);
            Assert.StartsWith("c0bc2dd", app.Find<SelectableTextBlock>("DetailsSha").Text, StringComparison.Ordinal);
            Assert.True(app.FindByAutomationId("AllFilesButton").Classes.Contains("selected"), "All files is not selected.");
            Assert.Contains(PartL.AllFiles6, TextsIn(app.FindByAutomationId("RightPanel")));
            Assert.Equal(PartL.AllFilesOfReadWholeLines, app.DetailsFileRows().Select(row => string.Join(' ', new[] { row.Letter, row.Name, row.Folder }.Where(part => part.Length > 0))));
        });
        Assert.True(SettingsJson(data).TryGetProperty("showAllFiles", out var showAll) && showAll.GetBoolean(), "settings.json does not hold \"showAllFiles\": true.");

        // Step 6: both feature folders opened; feature/compare hidden, then main soloed.
        app.Click(await app.RefRowAsync("Local branches", "feature"));
        await app.WaitForAsync(() => app.RefRowModel("Local branches", "feature") is RefItemRow { IsOpen: true }, "Local branches › feature to open");
        app.Click(await app.RefRowAsync("Remotes", "origin", "feature"));
        await app.WaitForAsync(() => app.RefRowModel("Remotes", "origin", "feature") is RefItemRow { IsOpen: true }, "Remotes › origin › feature to open");
        var calls = app.GitCalls().Count;
        app.RightClick(await app.RefRowAsync("Local branches", "feature", "compare"));
        app.Menu();
        app.ClickMenuItem(PartL.HideCompare);
        await app.WaitForGraphReloadAsync();
        app.RightClick(await app.RefRowAsync("Local branches", "main"));
        app.Menu();
        app.ClickMenuItem(PartL.SoloMain);
        await app.WaitForGraphReloadAsync();
        await app.WaitForIdleAsync();
        AssertCalledInOrder(app, calls, PartL.HideLog, PartL.SoloLog);
        Assert.All(app.GitCalls().Skip(calls).Where(call => call.CommandText is PartL.HideLog or PartL.SoloLog), call => Assert.Equal(0, call.ExitCode));
        await ShotInBothThemesAsync(app, "l28f-hidden", assertEach: (picture, palette) =>
        {
            AssertGraph(app, PartL.Hs1, 2, PartL.WorkingRow);
            Assert.False(app.Find<ProgressBar>("GraphReloading").IsEffectivelyVisible, "GraphReloading shows.");
            foreach (var path in new[] { new[] { "Local branches", "feature", "compare" }, ["Remotes", "origin", "feature", "compare"] })
            {
                var marks = app.RefRowMarks(path);
                Assert.Equal(palette.TextDisabled, marks.NameColor);
                Assert.True(marks.ShowsHiddenToggle && !marks.ShowsSoloToggle, $"{string.Join(" › ", path)} does not show its eye-off toggle alone.");
                Assert.Equal((18d, 18d), (marks.ToggleBounds!.Value.Width, marks.ToggleBounds!.Value.Height));
                var eye = app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<Button>()
                    .First(button => button.Name == "RefHiddenToggle" && button.IsEffectivelyVisible && app.BoundsOf(button) == marks.ToggleBounds!.Value);
                Assert.True(ReferenceEquals(Application.Current!.FindResource("IconEyeOff"), eye.GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>().Single().Data), "The hidden toggle does not show IconEyeOff.");
                AssertInk(picture, NameBounds(app, path), palette.TextDisabled, palette.Panel, $"{string.Join(" › ", path)}'s name (VcTextDisabledBrush)");
            }

            foreach (var path in new[] { new[] { "Local branches", "main" }, ["Remotes", "origin", "main"] })
            {
                var marks = app.RefRowMarks(path);
                Assert.True(marks.ShowsSoloToggle && !marks.ShowsHiddenToggle, $"{string.Join(" › ", path)} does not show its solo toggle alone.");
                Assert.Equal(palette.Accent, marks.ToggleColor);
                var toggle = app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<Button>()
                    .First(button => button.Name == "RefSoloToggle" && button.IsEffectivelyVisible && app.BoundsOf(button) == marks.ToggleBounds!.Value);
                Assert.True(ReferenceEquals(Application.Current!.FindResource("IconSolo"), toggle.GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>().Single().Data), "The solo toggle does not show IconSolo.");
                AssertIcon(app, picture, toggle, palette.Accent, $"{string.Join(" › ", path)}'s IconSolo (VcAccentBrush)");
            }

            var local = app.RefRowMarks("Local branches", "main");
            Assert.Equal(palette.Accent, local.NameColor);
            AssertInk(picture, NameBounds(app, ["Local branches", "main"]), palette.Accent, palette.Panel, "Local branches › main's name (VcAccentBrush)");
            Assert.StartsWith("c0bc2dd", app.Find<SelectableTextBlock>("DetailsSha").Text, StringComparison.Ordinal);
            Assert.Contains(PartL.AllFiles6, TextsIn(app.FindByAutomationId("RightPanel")));
            AssertColour(palette.Lane(1), picture, PartL.HistoryNode4, "row 4's node (lane colour 1)");
        });
        var filter = Assert.Single(new JsonSessionStore(new AppPaths(data.Path).SessionFile).Current.GraphFilters);
        Assert.Equal(PartL.Hidden, filter.Hidden);
        Assert.Equal(PartL.Soloed, filter.Soloed);
        Assert.Equal(
            [new BranchUpstream("refs/heads/feature/compare", "refs/remotes/origin/feature/compare"), new BranchUpstream("refs/heads/main", "refs/remotes/origin/main")],
            filter.Upstreams);
        Assert.Empty(filter.HiddenRemotes);

        // At the end: refs and .git/config as at the start, H0r's status and the one backup ref.
        Assert.Equal([.. refs, $"{PartL.DiscardBackup} {PartL.DiscardSnapshot}"], (await RefsAsync(repo)).Order(StringComparer.Ordinal));
        Assert.Equal(config, File.ReadAllText(Path.Combine(repo.Path, ".git", "config")));
        Assert.Equal(PartL.H0tStatus, await StatusAsync(repo));
    }

    /// <summary>
    /// Check L29: "reflog" (RL0): HEAD's reflog with its lost marks and footer, then the backups
    /// list, each in both themes (11.10, 11.14; LD11). Nothing changes git after the clean-up at
    /// opening. System with the fake dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L29_reflog_and_backups_in_both_themes()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, PartL.Size.Width, PartL.Size.Height, AppTheme.System, systemDark: true);
        AssertSystemDarkStart(app);

        // Step 1: the clean-up's record, the baseline read, HEAD's reflog from Local branches' menu.
        var record = await app.WaitForOperationRecordAsync(PartL.CleanUpTitle);
        await app.WaitForIdleAsync();
        Assert.Equal((OperationOutcome.Succeeded, PartL.CleanUpMessage), (record.Outcome, record.Message));
        Assert.Equal(PartL.BackupRows.Count, (await repo.BackupRefsAsync()).Select(entry => BackupRef.Parse(entry.Name).Stem).Distinct().Count());
        var exact = await repo.ExactStateAsync();
        Assert.Equal(-1, app.Session.Shell.ActiveTab.Repository!.SelectedIndex);
        app.RightClick(await app.RefRowAsync("Local branches"));
        app.Menu();
        app.ClickMenuItem(PartL.ShowReflog);
        await app.WaitForPageAsync<ReflogView>();
        await app.WaitForAsync(() => app.ReflogSnapshot() is { IsLoading: false } page && page.Title == PartL.ReflogOfHead && page.Rows.Count == PartL.HeadSelectors.Count, "HEAD's 36 entries");
        await app.WaitForReflogReadAsync(0);
        await AssertNoMenuAsync(app, logMark: null);
        await ShotInBothThemesAsync(app, "l29a-reflog", assertEach: (picture, palette) =>
        {
            var page = app.ReflogSnapshot();
            Assert.Equal((PartL.ReflogOfHead, "HEAD"), (page.Title, page.SourceLabel));
            var header = (Border)app.FindByAutomationId("ReflogHeader");
            Assert.Equal(palette.Chrome, (header.Background as ISolidColorBrush)?.Color);
            Assert.True(app.FindByAutomationId("ReflogClose").IsEffectivelyVisible, "ReflogClose does not show.");
            Assert.Equal((PartL.ReflogListTop, PartL.ReflogListBottom), (page.ListBounds.Top, page.ListBounds.Bottom));
            Assert.Equal(PartL.HeadSelectors, page.Rows.Select(row => row.Selector));
            Assert.Equal(PartL.HeadLost, page.Rows.Where(row => row.IsLost).Select(row => row.Selector));
            Assert.Equal(20, page.Rows.Count(row => row.Bounds is { } bounds && bounds.Top >= page.ListBounds.Top - 0.5 && bounds.Bottom <= page.ListBounds.Bottom + 0.5));
            Assert.All(page.Rows.Where(row => row.Bounds is not null), row => Assert.Null(row.TimeBounds));
            var inView = page.Rows.Where(row => row.LostMarkBounds is { } mark && mark.Bottom <= page.ListBounds.Bottom + 0.5).Select(row => row.Selector).ToList();
            Assert.Equal(PartL.LostMarksInView.Select(mark => mark.Selector), inView);
            foreach (var (selector, y) in PartL.LostMarksInView)
            {
                var mark = app.LostMarkBounds(selector);
                Assert.Equal(y, mark.Center.Y, 0.5);
                var icon = app.FindByAutomationId("ReflogList").GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>()
                    .First(candidate => candidate.IsEffectivelyVisible && mark.Contains(app.BoundsOf(candidate).Center));
                Assert.True(ReferenceEquals(Application.Current!.FindResource("IconAlert"), icon.Data), $"{selector}'s lost mark is not IconAlert.");
                AssertIconColour(app, picture, icon, palette.Warning, $"{selector}'s lost mark (IconAlert in VcWarningBrush, 11.10)");
            }

            Assert.Equal(PartL.HeadFooter, page.Footer);
            Assert.Equal((PartL.ReflogListBottom, PartL.ReflogFooterBottom), (page.FooterBounds!.Value.Top, page.FooterBounds.Value.Bottom));
            Assert.Null(page.Selected);
            Assert.True(app.FindByAutomationId("DetailsPlaceholder").IsEffectivelyVisible, "The right panel does not show its placeholder.");
        });

        // Step 2: Esc; Local branches' menu, Show backups: 15 rows.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null, "the reflog page to close");
        app.RightClick(await app.RefRowAsync("Local branches"));
        app.Menu();
        app.ClickMenuItem(PartL.ShowBackups);
        await app.WaitForPageAsync<ReflogView>();
        await app.WaitForAsync(() => app.ReflogSnapshot() is { IsLoading: false } page && page.SourceLabel == PartL.Backups && page.Backups.Count == PartL.BackupRows.Count, "the 15 backups");
        await ShotInBothThemesAsync(app, "l29b-backups", assertEach: (picture, palette) =>
        {
            var page = app.ReflogSnapshot();
            Assert.Equal((PartL.Backups, PartL.Backups), (page.Title, page.SourceLabel));
            Assert.Equal(PartL.BackupRows, page.Backups.Select(row => (row.Name, row.Words)));
            Assert.All(page.Backups, row => Assert.True(row.Bounds is { } bounds && bounds.Bottom <= page.ListBounds.Bottom + 0.5, $"The backup {row.Name} is not in view."));
            Assert.Equal(PartL.LastBackupRowY, page.Backups[^1].Bounds!.Value.Center.Y, 0.5);
            Assert.Null(page.FooterBounds);
            Assert.Empty(page.Rows);
            AssertColour(palette.Window, picture, new Point(page.ListBounds.Right - 4, page.ListBounds.Bottom - 4), "the list's background (VcWindowBackgroundBrush)");
            var header = (Border)app.FindByAutomationId("ReflogHeader");
            AssertColour(palette.Chrome, picture, new Point(app.BoundsOf(header).Right - 40, app.BoundsOf(header).Top + 3), "the header's background (VcChromeBackgroundBrush)");
            foreach (var row in page.Backups)
            {
                AssertInk(picture, row.WordsBounds!.Value, palette.TextPrimary, palette.Window, $"{row.Name}'s words (VcTextPrimaryBrush)");
            }
        });
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null, "the backups page to close");
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    // ---- L28's views --------------------------------------------------------------------------

    /// <summary>
    /// L28 step 1's diff view in the variant shown (phase 2's view, phase 5's scenario facts 2.2):
    /// its header (M on the chrome, Reader.cs, src, Unstaged, the close button), its toolbar, its two
    /// hunks with their buttons and lines, the rows' and the changed words' backgrounds, and C#'s
    /// colours on line 23.
    /// </summary>
    private static void AssertDiffView(ShellDriver app, Screenshot picture, Palette palette)
    {
        var dark = IsDark(palette);
        var status = app.Find<TextBlock>("DiffStatus");
        Assert.Equal("M", status.Text);
        AssertInk(picture, app.BoundsOf(status), palette.Warning, palette.Chrome, "the diff header's M (VcWarningBrush on the chrome)");
        Assert.Equal(("Reader.cs", "src", "Unstaged"), (app.Find<TextBlock>("DiffFileName").Text, app.Find<TextBlock>("DiffFolder").Text, app.Find<TextBlock>("DiffSideLabel").Text));
        Assert.True(app.Find<Button>("CloseDiffButton").IsEffectivelyVisible, "The diff's close button does not show.");
        Assert.True(app.Find<Button>("InlineDiffButton").Classes.Contains("selected"), "Inline is not selected.");
        Assert.True(app.Find<Button>("SideBySideDiffButton").IsEffectivelyVisible, "Side by side does not show.");
        Assert.Equal(
            ["StageFileButton", "DiscardFileButton"],
            new[] { "StageLinesButton", "DiscardLinesButton", "UnstageLinesButton", "StageFileButton", "DiscardFileButton", "UnstageFileButton" }.Where(id => app.Find<Button>(id).IsEffectivelyVisible));
        var view = DiffText(app);
        var rows = view.Rows;
        Assert.Equal(PartL.HunkHeaders, rows.Where(row => row.Kind == DiffRowKind.HunkHeader).Select(row => row.HeaderText));
        var hunkButtons = view.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).Select(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button)).ToList();
        Assert.Equal(2, hunkButtons.Count(id => id == "StageHunkButton"));
        Assert.Equal(2, hunkButtons.Count(id => id == "DiscardHunkButton"));
        var lines = rows.Where(row => row.Kind == DiffRowKind.Line).Select(row => row.Left!).ToList();
        Assert.Equal(
            [DiffLineKind.Context, DiffLineKind.Context, DiffLineKind.Removed, DiffLineKind.Added, DiffLineKind.Context, DiffLineKind.Context, DiffLineKind.Context,
             DiffLineKind.Context, DiffLineKind.Context, DiffLineKind.Context, DiffLineKind.Added, DiffLineKind.Added, DiffLineKind.Context],
            lines.Select(line => line.Kind));
        Assert.Equal(PartL.DiffLines, lines.Take(4).Select(line => (line.Kind switch { DiffLineKind.Added => "+ ", DiffLineKind.Removed => "- ", _ => "  " }) + line.Text.Trim()));
        Assert.Equal((22, string.Empty), (lines[10].Number, lines[10].Text.Trim()));
        Assert.Equal((23, PartL.Line23), (lines[11].Number, lines[11].Text.Trim()));
        Assert.Equal("}", lines[12].Text.Trim());

        // The rows' backgrounds, the changed words and C#'s colours.
        var removedRow = RowIndex(rows, lines[2]);
        var addedRow = RowIndex(rows, lines[3]);
        DiffRowColour(app, picture, 0, PartL.Hunk(dark), "the first hunk's header (VcDiffHunkBrush)");
        DiffRowColour(app, picture, rows.ToList().FindIndex(row => row.Kind == DiffRowKind.HunkHeader && row.Hunk == 1), PartL.Hunk(dark), "the second hunk's header");
        DiffRowColour(app, picture, removedRow, palette.DiffRemoved, "the removed line 3 (VcDiffRemovedBrush)");
        DiffRowColour(app, picture, addedRow, palette.DiffAdded, "the added line 3 (VcDiffAddedBrush)");
        DiffRowColour(app, picture, RowIndex(rows, lines[11]), palette.DiffAdded, "the added line 23");
        WordBackground(app, picture, lines[2], "words", PartL.RemovedWord(dark));
        WordBackground(app, picture, lines[3], "lines", PartL.AddedWord(dark));
        var line23 = lines[11];
        foreach (var (word, colour) in new[] { ("public", PartL.Keyword(dark)), ("int", PartL.Keyword(dark)), ("=>", PartL.Operator(dark)) })
        {
            var start = line23.Text.IndexOf(word, StringComparison.Ordinal);
            var brush = view.ForegroundAt(line23.Line, start) as ISolidColorBrush;
            Assert.True(brush?.Color == colour, $"\"{word}\" of line 23 is drawn with {brush?.Color}, not {colour}.");
            var area = view.TextRangeBounds(line23.Line, start, word.Length) ?? throw new Xunit.Sdk.XunitException($"\"{word}\" of line 23 is not in view.");
            AssertInk(picture, area.Translate((Vector)app.BoundsOf(view).TopLeft), colour, palette.DiffAdded, $"\"{word}\" of line 23 (C#'s Dark+ / Light+, D64)");
        }
    }

    /// <summary>
    /// L28 step 3's blame in the variant shown (phase 5's B6): its header, the gutter 130 wide from
    /// x 260, the text to x 700 from y 124; 24 lines in 15 runs, runs 1 and 13 <c>Not committed
    /// yet</c> in the warning colour without a SHA or date, every other run's first line its short
    /// SHA and <c>2026-01-01</c>; the runs' backgrounds alternating; C#'s colours.
    /// </summary>
    private static void AssertBlame(ShellDriver app, Screenshot picture, Palette palette)
    {
        var blame = app.BlameSnapshot();
        Assert.Equal((PartL.BlameOfReader, PartL.InTheWorkingTree), (blame.Title, blame.Caption));
        Assert.Equal(PartL.BlameOfReader, app.Find<TextBlock>("BlameTitle").Text);
        Assert.True(app.FindByAutomationId("BlameHistory").IsEffectivelyVisible, "File history does not show on the blame page.");
        Assert.True(Math.Abs(blame.GutterBounds.Left - 260) <= 0.5 && Math.Abs(blame.GutterBounds.Width - 130) <= 0.5 && Math.Abs(blame.GutterBounds.Top - 124) <= 0.5, $"The gutter is at {blame.GutterBounds}, not 130 wide from (260, 124).");
        Assert.True(Math.Abs(blame.TextBounds.Left - 390) <= 0.5 && Math.Abs(blame.TextBounds.Right - 700) <= 0.5, $"The text is at {blame.TextBounds}, not x 390 to 700.");
        Assert.Equal(PartL.BlameLines, blame.Text.TrimEnd('\n').Split('\n').Length);
        Assert.Equal(PartL.BlameRuns, blame.Runs.Count);
        foreach (var run in blame.Runs)
        {
            var texts = run.Texts.Select(part => part.Text).ToList();
            var uncommitted = PartL.UncommittedRuns.Any(entry => entry.Run == run.Index);
            if (uncommitted)
            {
                var entry = PartL.UncommittedRuns.Single(candidate => candidate.Run == run.Index);
                Assert.Equal((entry.First, entry.Last), (run.FirstLine, run.LastLine));
                Assert.Equal([PartL.NotCommittedYet], texts);
            }
            else
            {
                Assert.True(texts.Count == 2 && texts[0].Length == 7 && run.Sha.StartsWith(texts[0], StringComparison.Ordinal) && texts[1] == "2026-01-01", $"Run {run.Index}'s gutter shows {string.Join(" | ", texts)}, not its short SHA and 2026-01-01.");
            }

            var background = run.Index % 2 == 0 ? palette.Panel : palette.Window;
            Assert.Equal(run.Index % 2 == 0 ? "VcPanelBackgroundBrush" : "VcWindowBackgroundBrush", run.Background);
            var y = app.BlameGutterPoint(run.FirstLine).Y;
            AssertColour(background, picture, new Point(blame.GutterBounds.Right - 3, y), $"run {run.Index}'s background");
            if (uncommitted)
            {
                var text = run.Texts[0];
                AssertInk(picture, new Rect(text.X, y - 7, text.Width, 14), palette.Warning, background, $"run {run.Index}'s Not committed yet (VcWarningBrush, 11.10)");
            }
        }

        var line4 = blame.Text.Split('\n')[3];
        var column = line4.IndexOf("public", StringComparison.Ordinal) + 1;
        var start = app.BlameTextPoint(4, column);
        var end = app.BlameTextPoint(4, column + 6);
        var runOf4 = blame.Runs.Single(run => run.FirstLine <= 4 && run.LastLine >= 4);
        AssertInk(picture, new Rect(start.X, start.Y - 7, end.X - start.X, 14), PartL.Keyword(IsDark(palette)), runOf4.Index % 2 == 0 ? palette.Panel : palette.Window, "public of line 4 (C#'s keyword)");
        Assert.NotNull(Shown(app, "StagePanel"));
    }

    // ---- Small helpers of L28 -------------------------------------------------------------------

    private static DiffTextView DiffText(ShellDriver app) =>
        ShownDiffText(app) ?? throw new Xunit.Sdk.XunitException("No diff is shown.");

    private static DiffTextView? ShownDiffText(ShellDriver app) =>
        app.Window.GetVisualDescendants().OfType<DiffTextView>().FirstOrDefault(view => view.Name == "DiffText" && view.IsEffectivelyVisible);

    /// <summary>The display row of a cell.</summary>
    private static int RowIndex(IReadOnlyList<DiffDisplayRow> rows, DiffCell cell) =>
        rows.ToList().FindIndex(row => ReferenceEquals(row.Left, cell));

    /// <summary>A diff row's background, 6 from its right edge and 3 below its top (phase 2's point).</summary>
    private static void DiffRowColour(ShellDriver app, Screenshot picture, int row, Color colour, string what)
    {
        var view = DiffText(app);
        var bounds = view.RowBounds(row) ?? throw new Xunit.Sdk.XunitException($"Row {row} of the diff is not in view.");
        AssertColour(colour, picture, app.BoundsOf(view).TopLeft + new Point(bounds.Right - 6, bounds.Y + 3), what);
    }

    /// <summary>The background behind a changed word: 1 inside its first character's cell (phase 2's point).</summary>
    private static void WordBackground(ShellDriver app, Screenshot picture, DiffCell cell, string word, Color colour)
    {
        var view = DiffText(app);
        var start = cell.Text.IndexOf(word, StringComparison.Ordinal);
        Assert.True(start >= 0, $"\"{word}\" is not in \"{cell.Text}\".");
        var bounds = view.TextRangeBounds(cell.Line, start, 1) ?? throw new Xunit.Sdk.XunitException($"\"{word}\" is not in view.");
        AssertColour(colour, picture, app.BoundsOf(view).TopLeft + bounds.TopLeft + new Point(1, 1), $"the background behind \"{word}\"");
    }

    /// <summary>Waits until the stage panel shows with these counts and its message read.</summary>
    private static Task WaitForStagePanelAsync(ShellDriver app, int unstaged, int staged) =>
        app.WaitForAsync(
            () => Shown(app, "StagePanel") is not null
                && app.Find<TextBlock>("UnstagedTitle").Text == $"Unstaged files ({unstaged})"
                && app.Find<TextBlock>("StagedTitle").Text == $"Staged files ({staged})"
                && app.StagePanelSettled,
            $"the stage panel with {unstaged} unstaged and {staged} staged files");

    /// <summary>A stage list's rows as "letter name folder".</summary>
    private static List<string> FileRows(ShellDriver app, string list)
    {
        app.Settle();
        var items = (ItemsControl)app.FindByAutomationId(list);
        return [.. ((IEnumerable<ChangedFileRow>)(items.ItemsSource ?? Array.Empty<ChangedFileRow>()))
            .Where(row => row.IsFile)
            .Select(row => string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part))))];
    }

    /// <summary>A file row as the report writes it: "letter name folder".</summary>
    private static string DescribeFile(ChangedFileRow row) =>
        string.Join(' ', new[] { row.StatusLetter, row.Name, row.Folder }.Where(part => !string.IsNullOrEmpty(part)));

    /// <summary>A file row's status letter.</summary>
    private static TextBlock LetterOf(Control row) =>
        row.GetVisualDescendants().OfType<TextBlock>().First(text => text.Classes.Contains("status") && text.IsEffectivelyVisible);

    /// <summary>Every status letter of a file list in its colour (warning for M) on its row's background.</summary>
    private static void AssertFileLetters(ShellDriver app, Screenshot picture, Palette palette, string list)
    {
        var letters = app.FindByAutomationId(list).GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.Classes.Contains("status") && text.IsEffectivelyVisible && text.Text == "M").ToList();
        Assert.NotEmpty(letters);
        foreach (var letter in letters)
        {
            var bounds = app.BoundsOf(letter);
            var row = letter.GetVisualAncestors().OfType<Button>().First();
            var background = picture.PixelAt(new Point(app.BoundsOf(row).Right - 4, app.BoundsOf(row).Top + 2));
            AssertInk(picture, bounds, palette.Warning, background, $"an M of {list} (VcWarningBrush, 11.10)");
        }
    }

    /// <summary>Where a commit row of H0t's background is sampled (below the working-changes row): x 575, 3 below its top.</summary>
    private static Point HistoryRowBackground(int row) => new(575, 142 + (26 * row) + 3);

    /// <summary>The bounds of a left-panel row's name text as laid out.</summary>
    private static Rect NameBounds(ShellDriver app, string[] path)
    {
        var centre = app.RefRowNamePoint(path);
        var row = app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.Name == "RefName" && app.BoundsOf(text).Contains(centre));
        var bounds = app.BoundsOf(row);
        return new Rect(bounds.X, bounds.Y, Math.Min(row.TextLayout.Width, bounds.Width), bounds.Height);
    }

    /// <summary><c>git status --porcelain=v1 --untracked-files=all</c> as lines.</summary>
    private static async Task<List<string>> StatusAsync(TempRepo repo) =>
        [.. (await repo.GitAsync("status", "--porcelain=v1", "--untracked-files=all")).StandardOutput.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>The blob a working file's bytes make.</summary>
    private static async Task<string> WorkingBlobAsync(TempRepo repo, string path) =>
        (await repo.GitAsync("hash-object", "--", path)).StandardOutput.Trim();

    /// <summary>Every ref with what it points at, in name order.</summary>
    private static async Task<List<string>> RefsAsync(TempRepo repo) =>
        [.. (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal)];

    /// <summary>The data folder's <c>settings.json</c>, read as text.</summary>
    private static System.Text.Json.JsonElement SettingsJson(TempDirectory data)
    {
        using var stream = new FileStream(Path.Combine(data.Path, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var json = System.Text.Json.JsonDocument.Parse(stream);
        return json.RootElement.Clone();
    }
}
