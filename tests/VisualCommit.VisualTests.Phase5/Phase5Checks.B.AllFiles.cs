// Check B7 of phase 5's walk-through (docs/test-reports/phase-5.md, part B): "All files", the details
// panel's button (design 4.5, D164, BD14): an unchanged file's blame in one click and through its
// menu in two, a line traced to its commit, a changed file still opening its diff, the setting kept
// when another commit is shown and after a restart (D32). The part's map is in Phase5Checks.B.cs.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.Services;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations.PartB;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksB
{
    /// <summary>
    /// Check B7, method 1: "All files" on Read whole lines; LICENSE's blame in one click, a line
    /// of it traced to Initial commit; README.md's blame through its menu, a line traced to the
    /// side branch's commit; a changed file still opening its diff. "history", dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_B7_all_files()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        var refs = await RefsAsync(repo);
        var palette = Dark;

        // Step 1: Read whole lines; the list's three buttons.
        app.Click(GraphRowPointAsReported(app, ReadWholeLinesRow, 155));
        await AssertDetailsAsync(app, ReadWholeLines, ReadWholeLinesRow);
        AssertFileListButtons(app, allFiles: false);

        // Step 2: All files.
        app.Click(app.FindByAutomationId("AllFilesButton"));
        await AssertDetailsAsync(app, ReadWholeLinesAllFiles, ReadWholeLinesRow);
        Shot(app, "b7a-all-files");
        AssertFileListButtons(app, allFiles: true);
        Assert.Equal([("M", false)], app.DetailsFileRows().Where(row => row.Letter.Length > 0).Select(row => (row.Letter, row.IsUnchanged)));
        Assert.All(app.DetailsFileRows().Where(row => row.Letter.Length == 0), row => Assert.True(row.IsUnchanged));
        AssertShowAllFilesSetting(data, true);
        Assert.Equal(refs, await RefsAsync(repo));
        AssertNoClippedText(app);

        // Step 3: LICENSE, an unchanged file: its blame at the commit in one click, no diff.
        app.Click(await app.FileRowAsync("FileList", "LICENSE"));
        var blame = await WaitForBlameAsync(app, LicenseBlameTitle, At("c0bc2dd", "Read whole lines"), 6);
        var shot = Shot(app, "b7b-license-blame");
        AssertNoClippedText(app);
        AssertBlameLayout(app, blame);
        Assert.Equal(License, blame.Text.TrimEnd('\n'));
        AssertRuns(blame, LicenseRuns);
        AssertColour(palette.Panel, shot, new Point(GutterBounds.Right - 2, BlameLineCentreY(1)), "LICENSE's run's background");
        var license = app.DetailsFileRows().Single(row => row.Name == "LICENSE");
        Assert.True(license.IsSelected, "The row LICENSE has not the selection background.");
        var licenseRow = app.BoundsOf(await app.FileRowAsync("FileList", "LICENSE"));
        AssertColour(palette.Selection, shot, new Point(licenseRow.Right - 4, licenseRow.Center.Y), "row LICENSE's background");
        Assert.False(repository.IsDiffOpen, "A diff opened.");
        await AssertDetailsAsync(app, ReadWholeLinesAllFiles, ReadWholeLinesRow);

        // Step 4: line 3 in the gutter: Initial commit, with its all files.
        app.Click(GutterPointAsReported(app, 3));
        await AssertDetailsAsync(app, InitialCommitAllFiles, InitialCommitRow);
        shot = Shot(app, "b7c-initial-commit");
        AssertNoClippedText(app);
        AssertRuns(app.BlameSnapshot(), LicenseRuns, selected: 0);
        AssertColour(palette.Selection, shot, new Point(GutterBounds.Right - 2, BlameLineCentreY(6)), "line 6's gutter");

        // Step 5: Esc reveals row 13; Read whole lines again keeps "All files".
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null && repository.ShowsGraph, "the page to close");
        Assert.Equal(InitialCommitRow, repository.SelectedIndex);
        AssertRowWhollyInView(app, InitialCommitRow, 493);
        app.Click(GraphRowPointAsReported(app, ReadWholeLinesRow, 155));
        await AssertDetailsAsync(app, ReadWholeLinesAllFiles, ReadWholeLinesRow);

        // Step 6: README.md's menu (every row's is ForCommitFile, 4.5).
        var pointer = app.BoundsOf(await app.FileRowAsync("FileList", "README.md")).Center;
        app.RightClick(pointer);
        var menu = app.Menu();
        shot = Shot(app, "b7d-file-menu");
        AssertMenuAsReported(app, menu, CommitFileMenu("README.md"), FileHistoryItem, FileMenuHeight, shot);

        // Step 7: Blame.
        app.ClickMenuItem(BlameItem);
        blame = await WaitForBlameAsync(app, ReadmeBlameTitle, At("c0bc2dd", "Read whole lines"), 5);
        shot = Shot(app, "b7e-readme-blame");
        AssertNoClippedText(app);
        AssertBlameLayout(app, blame);
        Assert.Equal(Readme, blame.Text.TrimEnd('\n'));
        AssertRuns(blame, ReadmeRuns);
        AssertColour(palette.Panel, shot, new Point(GutterBounds.Right - 2, BlameLineCentreY(1)), "run 0's background");
        AssertColour(palette.Window, shot, new Point(GutterBounds.Right - 2, BlameLineCentreY(4)), "run 1's background");

        // Step 8: line 5, column 5 of the text: the side branch's commit, two clicks from the file's row.
        var point = app.BlameTextPoint(5, 5);
        Assert.Equal(new Point(BlameColumnX(5), BlameLineCentreY(5)), point, PointWithin(2));
        app.Click(point);
        await AssertDetailsAsync(app, ExplainTheReaderAllFiles, ExplainTheReaderRow);
        shot = Shot(app, "b7f-traced");
        AssertNoClippedText(app);
        AssertRuns(app.BlameSnapshot(), ReadmeRuns, selected: 1);
        AssertColour(palette.Selection, shot, new Point(GutterBounds.Right - 2, BlameLineCentreY(5)), "line 5's gutter");

        // Step 9: Esc; the changed README.md opens phase 2's diff (D63), not a blame; Esc closes it.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.ShownPage is null && repository.ShowsGraph, "the page to close");
        Assert.Equal(ExplainTheReaderRow, repository.SelectedIndex);
        AssertRowWhollyInView(app, ExplainTheReaderRow, 259);
        app.Click(await app.FileRowAsync("FileList", "README.md"));
        await app.WaitForAsync(() => repository.Diff?.ShowsText == true, "README.md's diff");
        Assert.Null(app.ShownPage);
        Assert.Equal(("M", "README.md", string.Empty, "c1b6375"), (repository.Diff!.StatusLetter, repository.Diff.FileName, repository.Diff.Folder, repository.Diff.SideLabel));
        Assert.Equal(ExplainTheReaderHunk.Header, Assert.Single(repository.Diff.TextDiff!.Hunks).Header);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !repository.IsDiffOpen && repository.ShowsGraph, "the diff to close");

        // Git unchanged throughout.
        await AssertGitUnchangedAsync(repo, refs, HistoryStatus);
        AssertNoOperationRan(app, repo);
    }

    /// <summary>
    /// Check B7, method 2: "All files" kept after a restart of the app on the same data folder
    /// (D32), then turned off. A plain [Fact]: each half runs with an application of its own.
    /// </summary>
    [Fact]
    public async Task Check_B7_all_files_kept_after_a_restart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var assembly = typeof(Phase5ChecksB).Assembly;
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");

        // Step 10: the first instance turns "All files" on and closes.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = await OpenAsync(data, repo);
                app.Click(app.GraphRowPoint(ReadWholeLinesRow));
                await AssertDetailsAsync(app, ReadWholeLines, ReadWholeLinesRow);
                app.Click(app.FindByAutomationId("AllFilesButton"));
                await AssertDetailsAsync(app, ReadWholeLinesAllFiles, ReadWholeLinesRow);
                AssertShowAllFilesSetting(data, true);
                return true;
            },
            cancellationToken);

        // Step 11: the second instance shows Read whole lines with all files at once; a click turns it off.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                using var app = await OpenAsync(data, repo);
                app.Click(app.GraphRowPoint(ReadWholeLinesRow));
                await AssertDetailsAsync(app, ReadWholeLinesAllFiles, ReadWholeLinesRow);
                Shot(app, "b7g-after-restart");
                AssertNoClippedText(app);
                AssertFileListButtons(app, allFiles: true);
                app.Click(app.FindByAutomationId("AllFilesButton"));
                await AssertDetailsAsync(app, ReadWholeLines, ReadWholeLinesRow);
                AssertFileListButtons(app, allFiles: false);
                AssertShowAllFilesSetting(data, false);
                Assert.Equal(HistoryStatus, await StatusAsync(repo));
                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Asserts the details' list buttons (BD14): Flat (selected), Tree and All files right of the
    /// title in that order, All files without an icon, with its tooltip, selected (the class
    /// <c>selected</c>) when <paramref name="allFiles"/>.
    /// </summary>
    private static void AssertFileListButtons(ShellDriver app, bool allFiles)
    {
        var flat = (Button)app.FindByAutomationId("FlatFilesButton");
        var tree = (Button)app.FindByAutomationId("TreeFilesButton");
        var all = (Button)app.FindByAutomationId("AllFilesButton");
        var title = app.FindByAutomationId("ChangedFilesTitle");
        Assert.True(app.BoundsOf(title).Right <= app.BoundsOf(flat).Left, "The buttons are not right of the title.");
        Assert.True(app.BoundsOf(flat).Right <= app.BoundsOf(tree).Left && app.BoundsOf(tree).Right <= app.BoundsOf(all).Left, "The buttons are not Flat, Tree, All files from the left.");
        Assert.Equal((true, false, allFiles), (flat.Classes.Contains("selected"), tree.Classes.Contains("selected"), all.Classes.Contains("selected")));
        Assert.Equal(["All files"], TextsIn(all));
        Assert.Empty(all.GetVisualDescendants().OfType<Icon>());
        Assert.Equal("Show every file of the commit", ToolTip.GetTip(all) as string);
    }

    /// <summary>Asserts the data folder's settings.json: <c>"showAllFiles": value</c> as the store writes it (camelCase), and read back as <c>AppSettings.ShowAllFiles</c>.</summary>
    private static void AssertShowAllFilesSetting(TempDirectory data, bool value)
    {
        var file = Path.Combine(data.Path, "settings.json");
        Assert.Contains($"\"showAllFiles\": {(value ? "true" : "false")}", File.ReadAllText(file), StringComparison.Ordinal);
        Assert.Equal(value, new JsonSettingsStore(file).Current.ShowAllFiles);
    }
}
