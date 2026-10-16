// Part F of phase 5's scripted walk-through (docs/test-reports/phase-5.md, "Part F: search,
// compare, solo and hide, and dashed stashes"): checks F10 to F15 of design 15.4. This class is
// part F's alone; its expected values are in Phase5Expectations.F.cs (Phase5Expectations.PartF).
// One test method per app start, named as "Conventions of part F" names them. This file holds F10
// and F11 (the search); Phase5Checks.F.Compare.cs F12 and F13, Phase5Checks.F.Filters.cs F14,
// Phase5Checks.F.Stashes.cs F15, and Phase5Checks.F.Helpers.cs what they share.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.App.Controls;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

/// <summary>The checks of part F: search, compare, solo and hide, and dashed stashes.</summary>
public sealed partial class Phase5ChecksF : Phase5Checks
{
    /// <summary>
    /// Check F10, part A: the search bar in the Commits mode on "history": a word only in commit
    /// bodies, Enter and Shift+Enter wrapping round, an author, a SHA prefix, Esc; dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_F10_search_commits()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var palette = Dark;
        var refs = await RefsAsync(repo);
        var config = ConfigFile(repo);

        // At the start: H0 with the bar closed, nothing selected.
        AssertGraph(app, PartF.H0, PartF.HistoryLanes, PartF.Working, showsDate: true);
        AssertNothingSelected(app);
        AssertBranch(app, "main");
        Assert.Null(app.SearchBar());
        AssertHistoryRowsAt(app, barOpen: false);

        // Step 1: the toolbar's Search opens the bar, the focus in its empty box.
        var calls = app.GitCalls().Count;
        await OpenSearchAsync(app);
        var shot = Shot(app, PartF.F10BarOpen);
        var bar = AssertBar(app, PartF.CommitsMode, string.Empty, PartF.CommitsPlaceholder, focus: true, count: string.Empty);
        AssertBarLayout(app, bar, right: 700);
        Assert.Equal(PartF.CloseRightAt1100, bar.CloseBounds.Right, PartF.PositionTolerance);
        AssertNear(palette.Chrome, shot, PartF.BarChromeAt, "search bar's background");
        AssertNear(palette.Border, shot, PartF.BarBorderAt, "search bar's bottom line");
        AssertNear(palette.Chrome, shot, PartF.BarAboveBorderAt, "search bar's background above its 1-px bottom line");
        AssertBarIcons(app);
        AssertPlaceholderShown(app, PartF.CommitsPlaceholder, palette);
        AssertHistoryRowsAt(app, barOpen: true);
        Assert.Equal(PartF.MessageWidth, Graph(app).Columns.Message.Width, PartF.PositionTolerance);
        Assert.True(Graph(app).Columns.ShowsDate, "The Date column is hidden with the bar open.");
        AssertGraph(app, PartF.H0, PartF.HistoryLanes, PartF.Working, showsDate: true, picture: shot);
        AssertMarks(app, marked: null);
        AssertNear(palette.Lane(0), shot, PartF.Row0NodeAt, "row 0's node at full strength");
        Assert.Empty(SearchCallsSince(app, calls));
        AssertNothingSelected(app);
        AssertNoClippedText(app);

        // Step 2: "tokenizer", only in two commits' bodies.
        await SearchForAsync(app, PartF.Tokenizer);
        shot = Shot(app, PartF.F10Tokenizer);
        AssertBar(app, PartF.CommitsMode, PartF.Tokenizer, PartF.CommitsPlaceholder, focus: true, count: "2 matches");
        AssertMarks(app, PartF.TokenizerMatches);
        AssertNear(palette.SearchMatch, shot, PartF.MarkRow6At, "mark of row 6");
        AssertNear(palette.SearchMatch, shot, PartF.MarkRow10At, "mark of row 10");
        AssertMarkWidth(shot, PartF.MarkRow6At, palette);
        AssertNear(PartF.DimmedColour0Dark, shot, PartF.Row0NodeAt, "dimmed node of row 0");
        AssertNear(PartF.DimmedColour1Dark, shot, PartF.Row4NodeAt, "dimmed node of row 4");
        AssertNear(PartF.DimmedColour2Dark, shot, PartF.Row8NodeAt, "dimmed node of row 8");
        AssertNear(palette.Lane(0), shot, PartF.Lane0InRow4At, "lane 0's line through the dimmed row 4");
        AssertNear(palette.Lane(2), shot, PartF.Row6NodeAt, "marked node of row 6");
        AssertNear(palette.Lane(0), shot, PartF.Row10NodeAt, "marked node of row 10");
        var search = AssertCall(app, calls, PartF.CommitsCall(PartF.Tokenizer));
        Assert.Equal([PartF.CompareWholeLines, PartF.HandleEmptyInput], OutputLines(search));
        AssertNothingSelected(app);
        AssertNoClippedText(app);
        var afterTokenizer = app.GitCalls().Count;

        // Step 3: Enter selects the first match, as a click selects it; the focus stays in the box.
        app.PressKey(Key.Enter);
        await WaitForMatchAsync(app, PartF.CompareWholeLinesRow, "1 of 2");
        await AssertDetailsAsync(app, PartF.CompareWholeLinesDetails);
        shot = Shot(app, PartF.F10FirstMatch);
        AssertBar(app, PartF.CommitsMode, PartF.Tokenizer, PartF.CommitsPlaceholder, focus: true, count: "1 of 2");
        AssertRowSelected(app, PartF.CompareWholeLinesRow);
        AssertRowInView(app, PartF.CompareWholeLinesRow);
        AssertSelectionBackground(app, shot, PartF.CompareWholeLinesRow);
        AssertMarks(app, PartF.TokenizerMatches);

        // Step 4: Enter to row 10, Enter wrapping round to row 6, Shift+Enter wrapping back to 10.
        app.PressKey(Key.Enter);
        await WaitForMatchAsync(app, PartF.HandleEmptyInputRow, "2 of 2");
        app.PressKey(Key.Enter);
        await WaitForMatchAsync(app, PartF.CompareWholeLinesRow, "1 of 2");
        app.PressKey(Key.Enter, RawInputModifiers.Shift);
        await WaitForMatchAsync(app, PartF.HandleEmptyInputRow, "2 of 2");
        await AssertDetailsAsync(app, PartF.HandleEmptyInputDetails);
        shot = Shot(app, PartF.F10Previous);
        AssertBar(app, PartF.CommitsMode, PartF.Tokenizer, PartF.CommitsPlaceholder, focus: true, count: "2 of 2");
        AssertRowSelected(app, PartF.HandleEmptyInputRow);
        AssertSelectionBackground(app, shot, PartF.HandleEmptyInputRow);
        AssertMarks(app, PartF.TokenizerMatches);
        Assert.Empty(SearchCallsSince(app, afterTokenizer));

        // Step 5: "Jonas", an author, matched in memory; row 10 stays selected, the fifth match.
        await ReplaceSearchAsync(app, PartF.JonasText);
        shot = Shot(app, PartF.F10Author);
        AssertBar(app, PartF.CommitsMode, PartF.JonasText, PartF.CommitsPlaceholder, focus: true, count: "5 of 5");
        AssertMarks(app, PartF.JonasMatches);
        AssertRowSelected(app, PartF.HandleEmptyInputRow);
        search = AssertCall(app, afterTokenizer, PartF.CommitsCall(PartF.JonasText));
        Assert.Empty(OutputLines(search));
        AssertNear(palette.Lane(0), shot, PartF.Row0NodeAt, "row 0's node, now a match");
        var afterJonas = app.GitCalls().Count;

        // Step 6: "0575", a SHA prefix; row 10 no match; Enter selects row 7.
        await ReplaceSearchAsync(app, PartF.ShaText);
        AssertBar(app, PartF.CommitsMode, PartF.ShaText, PartF.CommitsPlaceholder, focus: true, count: "1 match");
        AssertRowSelected(app, PartF.HandleEmptyInputRow);
        search = AssertCall(app, afterJonas, PartF.CommitsCall(PartF.ShaText));
        Assert.Empty(OutputLines(search));
        app.PressKey(Key.Enter);
        await WaitForMatchAsync(app, PartF.RenameParserRow, "1 of 1");
        await AssertDetailsAsync(app, PartF.RenameParserDetails);
        shot = Shot(app, PartF.F10Sha);
        AssertBar(app, PartF.CommitsMode, PartF.ShaText, PartF.CommitsPlaceholder, focus: true, count: "1 of 1");
        AssertMarks(app, PartF.ShaMatches);
        AssertRowSelected(app, PartF.RenameParserRow);
        AssertSelectionBackground(app, shot, PartF.RenameParserRow);

        // Step 7: Esc closes the bar: the marks go, the selection stays, the graph takes the focus.
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.SearchBar() is null, "the search bar to close");
        await app.WaitForAsync(() => app.Window.FocusManager?.GetFocusedElement() is CommitGraphControl, "the focus on the graph");
        shot = Shot(app, PartF.F10Closed);
        Assert.Null(app.SearchBar());
        AssertHistoryRowsAt(app, barOpen: false);
        Assert.Equal(PartF.HistoryRowCentre(PartF.RenameParserRow, barOpen: false), app.GraphRowPoint(PartF.RenameParserRow).Y, PartF.PositionTolerance);
        AssertMarks(app, marked: null);
        AssertNear(palette.Lane(0), shot, PartF.Row0NodeClosedAt, "row 0's node at full strength");
        AssertRowSelected(app, PartF.RenameParserRow);
        await AssertDetailsAsync(app, PartF.RenameParserDetails);
        Assert.Same(Graph(app), app.Window.FocusManager?.GetFocusedElement());

        // Step 8: Down moves the selection: the graph had the focus.
        app.PressKey(Key.Down);
        await WaitForDetailsAsync(app, PartF.H0[PartF.AddTheComparerRow].Message);
        AssertRowSelected(app, PartF.AddTheComparerRow);
        await AssertGitUnchangedAsync(repo, PartF.HistoryStatus, refs, config);
        await AssertNoDialogAsync(app);
    }

    /// <summary>Check F10, part B: the bar and the marks in the light theme at full width (FD17); light, 1920×1080.</summary>
    [AvaloniaFact]
    public async Task Check_F10_search_light_1920()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, FullSize.Width, FullSize.Height, AppTheme.Light);
        var palette = Light;
        Assert.Equal(ThemeVariant.Light, app.Window.ActualThemeVariant);
        Assert.Equal(new Size(FullSize.Width, FullSize.Height), app.Window.ClientSize);

        // Step 9: Search, "tokenizer", Enter.
        await OpenSearchAsync(app);
        await SearchForAsync(app, PartF.Tokenizer);
        app.PressKey(Key.Enter);
        await WaitForMatchAsync(app, PartF.CompareWholeLinesRow, "1 of 2");
        await AssertDetailsAsync(app, PartF.CompareWholeLinesDetails);
        var shot = Shot(app, PartF.F10LightWide);
        var bar = AssertBar(app, PartF.CommitsMode, PartF.Tokenizer, PartF.CommitsPlaceholder, focus: true, count: "1 of 2");
        AssertBarLayout(app, bar, right: FullSize.Width - WindowLayout.RightPanelWidth);
        AssertNear(palette.Chrome, shot, PartF.BarChromeAt, "search bar's light background");
        AssertNear(palette.Border, shot, PartF.BarBorderAt, "search bar's light bottom line");
        AssertBarIcons(app);
        var columns = Graph(app).Columns;
        Assert.True(columns.ShowsAuthor && columns.ShowsDate && columns.ShowsSha, "Not every graph column shows at 1920×1080.");
        var count = (TextBlock)app.FindByAutomationId("SearchCount");
        Assert.False(count.TextLayout.TextLines.Any(line => line.HasCollapsed), "The count is cut.");
        AssertRowSelected(app, PartF.CompareWholeLinesRow);
        AssertMarks(app, PartF.TokenizerMatches);
        AssertNear(palette.SearchMatch, shot, PartF.MarkRow6At, "light mark of row 6");
        AssertNear(palette.SearchMatch, shot, PartF.MarkRow10At, "light mark of row 10");
        AssertNear(PartF.DimmedColour0Light, shot, PartF.Row0NodeAt, "light dimmed node of row 0");
        AssertSelectionBackground(app, shot, PartF.CompareWholeLinesRow);
        Assert.Equal(palette.TextPrimary, (((SelectableTextBlock)app.FindByAutomationId("DetailsSubject")).Foreground as ISolidColorBrush)?.Color);
        AssertHistoryRowsAt(app, barOpen: true);
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check F11: the Files and Changes modes on "history": the mode menu, both names of the
    /// renamed file, a text no path holds, a string added and later removed, and a commit made
    /// outside the app while the search is open (only the new row given to git); dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_F11_search_files_and_changes()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        Assert.Equal(PartF.TraceAgainTime, repo.ClockTime);

        // Step 1: Search, then the mode button: the mode menu at the button's bottom-left corner.
        await OpenSearchAsync(app);
        var bar = AssertBar(app, PartF.CommitsMode, string.Empty, PartF.CommitsPlaceholder, focus: true, count: string.Empty);
        AssertBarLayout(app, bar, right: 700);
        AssertBarIcons(app);
        app.Click(app.FindByAutomationId("SearchModeButton"));
        var menu = app.Menu();
        Shot(app, PartF.F11ModeMenu);
        AssertMenuAsReported(app, menu, PartF.ModeMenu, PartF.ModeMenu[1], PartF.ModeMenuHeight);
        Assert.Equal(bar.ModeButtonBounds.BottomLeft.X, menu.Bounds.X, PartF.PositionTolerance);
        Assert.Equal(bar.ModeButtonBounds.BottomLeft.Y, menu.Bounds.Y, PartF.PositionTolerance);
        Assert.All(menu.Items, item => Assert.True(item.IsEnabled, $"\"{item.Label}\" is disabled."));

        // Step 2: Files; the box takes the focus again with the Files placeholder; "parser".
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartF.FilesItem);
        await AssertNoMenuAsync(app, logMark: null);
        await app.WaitForAsync(() => app.SearchBar() is { BoxHasFocus: true }, "the focus back in the search box");
        AssertBar(app, PartF.FilesMode, string.Empty, PartF.FilesPlaceholder, focus: true, count: string.Empty);
        AssertPlaceholderShown(app, PartF.FilesPlaceholder, Dark);
        await SearchForAsync(app, PartF.ParserText);
        Shot(app, PartF.F11FilesParser);
        AssertBar(app, PartF.FilesMode, PartF.ParserText, PartF.FilesPlaceholder, focus: true, count: "3 matches");
        AssertMarks(app, PartF.ParserMatches);
        AssertCall(app, calls, PartF.FilesCall(PartF.ParserText));
        AssertNoClippedText(app);

        // Step 3: "reader": the rename's row matched by its new name too.
        calls = app.GitCalls().Count;
        await ReplaceSearchAsync(app, PartF.ReaderText);
        Shot(app, PartF.F11FilesReader);
        AssertBar(app, PartF.FilesMode, PartF.ReaderText, PartF.FilesPlaceholder, focus: true, count: "2 matches");
        AssertMarks(app, PartF.ReaderMatches);
        AssertCall(app, calls, PartF.FilesCall(PartF.ReaderText));

        // Step 4: "TraceEverything": no path holds it.
        calls = app.GitCalls().Count;
        await ReplaceSearchAsync(app, PartF.TraceEverything);
        Shot(app, PartF.F11NoMatches);
        AssertBar(app, PartF.FilesMode, PartF.TraceEverything, PartF.FilesPlaceholder, focus: true, count: PartF.NoMatches);
        AssertMarks(app, []);
        AssertCall(app, calls, PartF.FilesCall(PartF.TraceEverything));

        // Step 5: Changes: the same text searched in the new mode (FD4).
        calls = app.GitCalls().Count;
        app.ChooseSearchMode(PartF.ChangesItem);
        await app.WaitForAsync(() => app.SearchBar() is { Mode: PartF.ChangesMode, BoxHasFocus: true }, "the Changes mode with the focus in the box");
        await app.WaitForSearchAsync();
        Shot(app, PartF.F11Changes);
        AssertBar(app, PartF.ChangesMode, PartF.TraceEverything, PartF.ChangesPlaceholder, focus: true, count: "2 matches");
        Assert.Equal(PartF.TraceEverything, ((TextBox)app.FindByAutomationId("SearchBox")).SelectedText);
        AssertMarks(app, PartF.ChangesMatches);
        var search = AssertCall(app, calls, PartF.ChangesCall(PartF.TraceEverything));
        Assert.Equal([PartF.StopTracing, PartF.AddTracing], OutputLines(search));
        Assert.Single(SearchCallsSince(app, calls));
        AssertNoClippedText(app);

        // Step 6: "Trace again" committed outside the app, only src/Config.cs staged (FD8).
        calls = app.GitCalls().Count;
        repo.WriteFile(PartF.TraceAgainPath, Scenarios.HistoryFiles.ConfigTraced);
        await repo.GitAsync("add", "--", PartF.TraceAgainPath);
        await repo.GitAsync("commit", "--quiet", "--message", PartF.TraceAgainSubject);
        await app.WaitForAsync(
            () => Repo(app).Graph.Count == PartF.HF1.Count && Repo(app).Graph.CommitAt(0).Subject == PartF.TraceAgainSubject,
            "the graph's row 0 to be \"Trace again\"",
            OutsideChangeTimeout);
        await app.WaitForSearchAsync(OutsideChangeTimeout);
        await app.WaitForIdleAsync(timeout: OutsideChangeTimeout);
        var shot = Shot(app, PartF.F11AfterRefresh);
        Assert.Equal(PartF.TraceAgain, await repo.RefTargetAsync("refs/heads/main"));
        await AssertTraceAgainAsync(repo);
        Assert.Equal(PartF.HistoryStatus, await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(PartF.TraceAgainStatusShort, await GitLinesAsync(repo, "status", "-sb"));
        AssertGraph(app, PartF.HF1, PartF.HistoryLanes, PartF.Working, showsDate: true);
        AssertNear(Dark.SearchMatch, shot, new PartF.Sample(261, (int)PartF.HistoryRowCentre(0, barOpen: true)), "mark of Trace again");
        var main = (VisualCommit.App.ViewModels.Panels.RefItemRow)app.RefRowModel("Local branches", "main");
        Assert.Equal((1, 0, true), (main.Ahead, main.Behind, main.IsHead));
        AssertBar(app, PartF.ChangesMode, PartF.TraceEverything, PartF.ChangesPlaceholder, focus: true, count: "3 matches");
        AssertMarks(app, PartF.ChangesMatchesAfterRefresh);
        search = Assert.Single(SearchCallsSince(app, calls));
        Assert.Equal(PartF.ChangesCall(PartF.TraceEverything), search.CommandText);
        Assert.Equal(0, search.ExitCode);
        Assert.Equal([PartF.TraceAgain], OutputLines(search));
        await AssertNoDialogAsync(app);
        AssertNoClippedText(app);
    }

    /// <summary>"Trace again" as git stores it: parent Read whole lines, Test Author as author and committer at 12:14, M src/Config.cs.</summary>
    private static async Task AssertTraceAgainAsync(TempRepo repo)
    {
        var fields = (await GitLinesAsync(repo, "log", "-1", "--format=%s%x1f%P%x1f%an <%ae>%x1f%cn <%ce>%x1f%aI%x1f%cI", PartF.TraceAgain))[0].Split('\x1f');
        Assert.Equal((PartF.TraceAgainSubject, PartF.ReadWholeLines), (fields[0], fields[1]));
        Assert.Equal((PartF.TestAuthor, PartF.TestAuthor), (fields[2], fields[3]));
        AssertGitDate(PartF.TraceAgainTime, fields[4]);
        AssertGitDate(PartF.TraceAgainTime, fields[5]);
        Assert.Equal(["M\tsrc/Config.cs"], await GitLinesAsync(repo, "diff-tree", "--no-commit-id", "-r", "--name-status", PartF.TraceAgain));
    }

    /// <summary>A match's mark is 3 px wide (3.3): the colour at x 260 to 262 of the row, not at x 263.</summary>
    private static void AssertMarkWidth(Screenshot shot, PartF.Sample at, Palette palette)
    {
        AssertNear(palette.SearchMatch, shot, at with { X = at.X - 1 }, "mark's first pixel");
        AssertNear(palette.SearchMatch, shot, at with { X = at.X + 1 }, "mark's last pixel");
        Assert.NotEqual(palette.SearchMatch, shot.PixelAt(at.X + 2, at.Y));
    }

    /// <summary>The box shows its placeholder <paramref name="text"/> in the secondary colour while it is empty.</summary>
    private static void AssertPlaceholderShown(ShellDriver app, string text, Palette palette)
    {
        var box = (TextBox)app.FindByAutomationId("SearchBox");
        var placeholder = TextsIn(box);
        Assert.Contains(text, placeholder);
        var block = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(box).OfType<TextBlock>().First(candidate => candidate.Text == text && candidate.IsEffectivelyVisible);
        Assert.Equal(palette.TextSecondary, (block.Foreground as ISolidColorBrush)?.Color);
    }
}
