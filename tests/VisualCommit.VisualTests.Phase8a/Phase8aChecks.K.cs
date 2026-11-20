// Part K of phase 8a's walk-through, the keyboard pass and the focus fixes (docs/test-reports/phase-8a.md,
// "Part K"; docs/phase-8a-design.md 6.5; D417, D425 to D427, D452): a hunk and chosen lines by the
// keys (K1), a blame run picked by the arrows (K2), "Cancel {title}" in the palette (K3), a stage
// row's buttons by Tab (K4), the focus after "Choose" (K5), a loading diff that leaves a moved focus
// (K6), the commit boxes and Amend while committing (K7), and the walks to Cancel and a splitter
// (K8). Seven methods, seven starts, as the report's methods table has them; methods 1 and 2 are
// the twins of the real window's launches 11 and 12 and take their steps in the same order. Its
// helpers are at the end of this file.
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Blame;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>Part K: the keyboard pass and the focus fixes (docs/test-reports/phase-8a.md; design 6.5).</summary>
public sealed partial class Phase8aChecksK : Phase8aChecks
{
    /// <summary>
    /// Method 1, K1 on a fresh "changes", dark (the twin of the real window's launch 11): F6 and 18
    /// Tabs to <c>src/Calculator.cs</c>'s row, Enter, Tab to the first "Stage hunk" and Enter, Shift+Tab
    /// to "Discard file", Tab back, Ctrl+Home, four Downs and two Shift+Downs, Shift+F10's "Stage
    /// selected lines"; then, scripted only, Tab and Shift+Tab to "Side by side" and Enter.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K1_hunk_by_keys()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var exact = await repo.ExactStateAsync();

        // Step 2: the working-changes row; the lists; nothing focused in the right panel.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, K.Unstaged7, K.Staged3);
        Assert.Equal(K.UnstagedRows, Rows(app).Unstaged.Rows.Select(row => row.Key));
        Assert.Equal(K.StagedRows, Rows(app).Staged.Rows.Select(row => row.Key));
        Assert.NotEqual(KeyRegionRole.Right, app.FocusedRegion());

        // Step 3: F6, the first unstaged row (phase 6's k12b state).
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRowPath() == K.Readme, "the focus on README.md's row");
        AssertFocusedRowAlone(app, K.Readme);

        // Step 4: 18 Tabs along each row's own button, its Stage and its Discard.
        for (var tab = 1; tab <= K.CalculatorWalk.Count; tab++)
        {
            var (row, id) = K.CalculatorWalk[tab - 1];
            await app.TabToAsync(focused => IsRowButton(focused, row, id), $"{row}'s {id} (Tab {tab})");
            Assert.True(app.FocusVisible, $"{row}'s {id} shows no keyboard frame (Tab {tab}).");
            AssertRowButtonsAfterTab(app, row, id, previous: id == K.Ids.StageFileRow ? K.CalculatorWalk[tab - 2].Row : null);
        }

        Assert.Equal(K.Calculator, app.FocusedRowPath());
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 5: Enter opens the diff; the text takes the focus, without a frame.
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true } diff && diff.Target.Path == K.Calculator, "src/Calculator.cs's diff");
        await app.WaitForAsync(() => app.FocusedAutomationId() == K.Ids.DiffText, $"the focus in DiffText (it is on {app.DescribeFocus()})");
        Assert.False(app.FocusVisible, "The diff's text shows the keyboard's frame.");
        AssertDiffHeader(app);
        AssertToolbar(app, lines: false);
        Assert.Equal([K.Ids.StageHunkButton, K.Ids.DiscardHunkButton, K.Ids.StageHunkButton, K.Ids.DiscardHunkButton], HunkButtons(app).Select(AutomationProperties.GetAutomationId));
        Assert.True(Rows(app).Unstaged.Rows.Single(row => row.Key == K.Calculator).IsSelected, "src/Calculator.cs's row is not selected.");

        // Step 6 (k1a): Tab, the first hunk's Stage hunk with the frame.
        app.PressKey(Key.Tab);
        var stageHunk = HunkButtons(app)[0];
        Assert.True(stageHunk.IsFocused, $"Tab did not reach the first Stage hunk: the focus is on {app.DescribeFocus()}.");
        Assert.True(app.FocusVisible, "Stage hunk shows no keyboard frame.");
        Picture(app, K.K1aHunkButton);
        Assert.Equal(K.StageHunk, AutomationProperties.GetName(stageHunk));
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 7 (k1b): Enter stages that hunk alone; the text has the focus, without a frame.
        var diff = Repository(app).Diff!;
        var before = diff.Diff;
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !ReferenceEquals(diff.Diff, before) && diff.ShowsText && Rows(app).StagedTitle == K.Staged4, "the stage to end and the diff read again");
        await app.WaitForIdleAsync();
        Picture(app, K.K1bHunkStaged);
        Assert.Equal(K.StagedAfterHunk, await NumstatAsync(repo, cached: true));
        Assert.Equal(K.UnstagedAfterHunk, await NumstatAsync(repo, cached: false));
        Assert.Equal(K.IndexAfterHunk, await IndexFileAsync(repo));
        Assert.Equal([K.Ids.StageHunkButton, K.Ids.DiscardHunkButton], HunkButtons(app).Select(AutomationProperties.GetAutomationId));
        Assert.Equal(4, diff.Diff!.Hunks.Single().Lines.Count(line => line.Kind == DiffLineKind.Added));
        Assert.Contains(K.Calculator, Rows(app).Staged.Rows.Select(row => row.Key));
        Assert.Contains(K.Calculator, Rows(app).Unstaged.Rows.Select(row => row.Key));
        Assert.Equal(K.Unstaged7, Rows(app).UnstagedTitle);
        Assert.True(app.FocusedAutomationId() == K.Ids.DiffText, $"The focus is on {app.DescribeFocus()}, not in the diff's text.");
        Assert.False(app.FocusVisible, "The text shows the keyboard's frame after the hunk.");

        // Step 8 (k1c): Shift+Tab, Discard file with the frame.
        app.PressKey(Key.Tab, RawInputModifiers.Shift);
        Assert.True(app.FocusedAutomationId() == K.Ids.DiscardFileButton, $"Shift+Tab did not reach Discard file: the focus is on {app.DescribeFocus()}.");
        Assert.True(app.FocusVisible, "Discard file shows no keyboard frame.");
        Picture(app, K.K1cToolbar);

        // Step 9 (k1d): Tab back into the text; Ctrl+Home, four Downs, two Shift+Downs; Shift+F10.
        await app.TabToAsync(focused => FocusedId(focused) == K.Ids.DiffText, "the diff's text");
        app.PressKey(Key.Home, RawInputModifiers.Control);
        for (var down = 0; down < K.DownsToLines; down++)
        {
            app.PressKey(Key.Down);
        }

        for (var down = 0; down < K.ShiftDownsForLines; down++)
        {
            app.PressKey(Key.Down, RawInputModifiers.Shift);
        }

        await app.WaitForAsync(() => diff.SelectedChanges.Count == 2, "two lines selected");
        var selected = diff.SelectedChanges.OrderBy(line => line.Line).Select(line => diff.Diff!.Hunks[line.Hunk].Lines[line.Line].Text).ToList();
        Assert.Equal([string.Empty, "    public int Negate(int a) => -a;"], selected);
        AssertToolbar(app, lines: true);
        app.MoveToNeutral();
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var menu = app.Menu();
        Picture(app, K.K1dLinesMenu, keepPointer: true);
        Assert.Equal(K.CalculatorName, menu.Header);
        Assert.Equal([K.StageSelectedLines, K.DiscardSelectedLines], menu.Items.Take(2).Select(item => item.Label));
        AssertHighlighted(menu, K.StageSelectedLines);
        Assert.Single(menu.Entries, entry => entry.IsHighlighted);

        // Step 10: Enter stages the two lines.
        before = diff.Diff;
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !app.IsMenuOpen && !ReferenceEquals(diff.Diff, before) && diff.ShowsText, "the menu closed and the diff read again");
        await app.WaitForIdleAsync();
        Assert.Equal(K.StagedAfterLines, await NumstatAsync(repo, cached: true));
        Assert.Equal(K.UnstagedAfterLines, await NumstatAsync(repo, cached: false));
        Assert.Equal(K.IndexAfterLines, await IndexFileAsync(repo));
        Assert.Equal(
            [string.Empty, "    public int Square(int a) => a * a;"],
            diff.Diff!.Hunks.SelectMany(hunk => hunk.Lines).Where(line => line.Kind == DiffLineKind.Added).Select(line => line.Text));

        // Step 11, scripted only: Tab to the text, Shift+Tab to Side by side, Enter.
        for (var tab = 0; FocusedId(app.FocusedElement) != K.Ids.DiffText; tab++)
        {
            Assert.True(tab < K.ModeWalkLimit, $"{K.ModeWalkLimit} Tabs did not reach the diff's text: the focus is on {app.DescribeFocus()}.");
            await app.TabToAsync(_ => true, "the next element");
        }

        for (var tab = 0; FocusedId(app.FocusedElement) != K.Ids.SideBySideDiffButton; tab++)
        {
            Assert.True(tab < K.ModeWalkLimit, $"{K.ModeWalkLimit} Shift+Tabs did not reach Side by side: the focus is on {app.DescribeFocus()}.");
            await app.TabToAsync(_ => true, "the element before", shift: true);
        }

        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => app.Find<Button>(K.Ids.SideBySideDiffButton).Classes.Contains("selected"), "Side by side selected");
        Assert.Equal(K.StagedAfterLines, await NumstatAsync(repo, cached: true));
        Assert.Equal(K.UnstagedAfterLines, await NumstatAsync(repo, cached: false));

        // The method's end: only src/Calculator.cs's index entry and status line changed.
        AssertChangedOnlyIn(exact, await repo.ExactStateAsync(), K.Calculator);
    }

    /// <summary>
    /// Method 2, K2 on a fresh "history", light (the twin of the real window's launch 12): the blame
    /// of <c>src/Reader.cs</c> by Ctrl+Shift+B, three Downs picking runs 0, 1 and 2, Shift+F10 run 2's
    /// menu; Esc twice.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K2_blame_by_arrows()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path, theme: AppTheme.Light);
        await app.WaitForAsync(() => !app.ShowsDarkTheme, "the window to show the light theme");
        var exact = await repo.ExactStateAsync();

        // Steps 2 and 3: the working-changes row; F6 to src/Reader.cs; Enter opens its diff.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, K.Unstaged1, K.Staged0);
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRowPath() == K.Reader, "the focus on src/Reader.cs's row");
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true } && app.FocusedAutomationId() == K.Ids.DiffText, "src/Reader.cs's diff with the focus in its text");

        // Step 4: Ctrl+Shift+B, the blame with the focus in its text; nothing picked.
        app.PressKey(Key.B, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(
            () => Repository(app).Page is BlameViewModel { ShowsText: true } && app.FocusedAutomationId() == K.Ids.BlameText,
            $"the blame with the focus in its text (it is on {app.DescribeFocus()})");
        var blame = (BlameViewModel)Repository(app).Page!;
        var text = app.Find<BlameTextEditor>(K.Ids.BlameText);
        Assert.Equal(K.BlameTitle, app.Find<TextBlock>(K.Ids.BlameTitle).Text);
        Assert.Equal(K.BlameCaption, app.Find<TextBlock>(K.Ids.BlameCaption).Text);
        Assert.Equal(K.BlameLines, text.Document.LineCount);
        Assert.Equal(K.BlameRuns, blame.Runs.Count);
        Assert.Null(blame.SelectedRun);
        Assert.Null(app.Find<BlameGutter>(K.Ids.BlameGutter).SelectedRun);
        Assert.Equal(1, text.TextArea.Caret.Line);

        // Step 5 (k2a): three Downs, each picking the caret's run and showing its commit.
        foreach (var (run, line, subject) in K.BlameDowns)
        {
            app.PressKey(Key.Down);
            await app.WaitForAsync(() => blame.SelectedRun?.Index == run && text.TextArea.Caret.Line == line, $"run {run} picked on line {line}");
            if (subject is null)
            {
                await app.WaitForAsync(() => app.Session.Shell.ActiveTab.ShowsStagePanel && TextOf(app, K.Ids.UnstagedTitle) == K.Unstaged1, "the stage panel for the run not committed yet");
                Assert.True(Repository(app).IsWorkingRowSelected, "The working-changes row is not selected behind the page.");
            }
            else
            {
                await WaitForSubjectAsync(app, subject);
            }

            Assert.True(app.FocusedAutomationId() == K.Ids.BlameText, $"The focus left the blame's text for {app.DescribeFocus()}.");
        }

        var shot = Picture(app, K.K2aBlameRun);
        var gutter = app.BoundsOf(app.Find<BlameGutter>(K.Ids.BlameGutter));
        AssertColour(K.SelectionLight, shot, new Point(gutter.Right - 2, LineCentreY(app, text, 4)), "run 2's gutter beside line 4 (VcSelectionBrush)");
        AssertColour(K.PanelLight, shot, new Point(gutter.Right - 2, LineCentreY(app, text, 1)), "run 0's gutter beside line 1 (VcPanelBackgroundBrush)");
        AssertColour(K.WindowLight, shot, new Point(gutter.Right - 2, LineCentreY(app, text, 3)), "run 1's gutter beside line 3 (VcWindowBackgroundBrush)");
        Assert.StartsWith(K.Run2Sha, Assert.Single(Repository(app).SelectedCommits).Sha, StringComparison.Ordinal);
        Assert.Equal(K.RenameTheParser, TextOf(app, K.Ids.DetailsSubject));
        Assert.Contains(app.FindByAutomationId(K.Ids.RightPanel).GetVisualDescendants().OfType<TextBlock>(), block => block.IsEffectivelyVisible && block.Text is { } author && author.Contains(K.MiraKovac, StringComparison.Ordinal) && author.Contains(K.MiraEmail, StringComparison.Ordinal));
        Assert.False(app.IsMenuOpen);

        // Step 6 (k2b): Shift+F10, run 2's menu.
        app.MoveToNeutral();
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var menu = app.Menu();
        Picture(app, K.K2bBlameMenu, keepPointer: true);
        AssertMenu(menu, K.RunMenuHeader, K.ShowInGraph, "—", K.BlameBeforeThisCommit, K.FileHistory, "—", K.CopySha);
        AssertHighlighted(menu, K.ShowInGraph);
        AssertMenuShape(app, menu);
        Assert.Equal(2, blame.SelectedRun?.Index);

        // Step 7: Esc twice: no menu, no page.
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => Repository(app).Page is null, "the blame page to close");
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// Method 3, K3 and K8 on a fresh "graph", its fetches held (300 rounds): "Cancel Fetch origin"
    /// first in the palette while a held fetch runs, Enter cancels it; then a second held fetch,
    /// the walk of Tabs from the right panel to the status bar's Cancel, and the walk to
    /// <c>LeftSplitter</c>, whose Right widens the left panel by 10.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K3_K8_cancel_in_the_palette_and_the_walks()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var hold = FetchHold.Install(data.Path, rounds: K.FetchHoldRounds);

        // The hold's script names its marker after the remote's folder, which it finds after the
        // last forward slash: the copy's origin, written with backslashes on Windows, is written
        // with forward slashes, the same folder, so that the marker lands in the harness folder.
        var origin = (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim();
        await repo.GitAsync("remote", "set-url", "origin", origin.Replace(Path.DirectorySeparatorChar, '/'));
        hold.HoldIn(repo.Path);
        using var app = await OpenAsync(data, repo.Path);
        var exact = await repo.ExactStateAsync();
        var refs = await RefsAsync(repo);

        // K3 step 1: HEAD's commit's details.
        app.Click(app.GraphRowPoint(HeadRow));
        await WaitForSubjectAsync(app, K.HeadCommit);
        app.MoveToNeutral();

        // Step 2: nothing runs: no Cancel entry.
        await AssertNoCancelEntryAsync(app);

        // Step 3: Fetch origin from the remote's menu, held.
        var fetch = await StartHeldFetchAsync(app, hold);

        // Step 4 (k3a): the palette, "Cancel Fetch origin" first.
        app.MoveToNeutral();
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen && app.PaletteBox.IsFocused, "the palette with its box focused");
        Picture(app, K.K3aPaletteCancel);
        var first = app.PaletteSnapshot().Rows[0];
        Assert.Equal((K.CancelFetchOrigin, (string?)null, (string?)null, true, true), (first.Label, first.Detail, first.KeyText, first.IsEnabled, first.IsHighlighted));
        Assert.False(app.Palette.Shown[0].IsDestructive);
        Assert.True(string.IsNullOrEmpty(app.PaletteBox.Text));
        Assert.False(FetchEnded(app, fetch), "The fetch ended before the palette's Cancel.");

        // Step 5: Enter cancels it; no remote ref moved; no line of a stale entry.
        app.PressKey(Key.Enter);
        await AssertFetchCancelledAsync(app, fetch, repo, refs);
        Assert.DoesNotContain(app.LogLines(), line => line.Contains(K.NoLongerRunning, StringComparison.Ordinal));

        // Step 6: the hold released; no Cancel entry.
        hold.Release();
        await AssertNoCancelEntryAsync(app);

        // K8 step 7: a second held fetch.
        hold.Arm();
        fetch = await StartHeldFetchAsync(app, hold);

        // Step 8: F6 to the right panel, Tab until Cancel, Enter.
        await F6ToRightPanelAsync(app);
        var cancel = app.Find<Button>(K.Ids.OperationCancelButton);
        var tabs = await WalkAsync(app, () => cancel.IsFocused, K.Ids.OperationCancelButton);
        TestContext.Current.TestOutputHelper?.WriteLine($"K8: Tabs from the right panel to Cancel: {tabs.ToString(CultureInfo.InvariantCulture)}");
        app.PressKey(Key.Enter);
        await AssertFetchCancelledAsync(app, fetch, repo, refs);

        // Step 9 (k8a): the hold released; F6 to the right panel, Tab until LeftSplitter, Right.
        hold.Release();
        await F6ToRightPanelAsync(app);
        var splitter = app.Find<GridSplitter>(K.Ids.LeftSplitter);
        tabs = await WalkAsync(app, () => splitter.IsFocused, K.Ids.LeftSplitter);
        TestContext.Current.TestOutputHelper?.WriteLine($"K8: Tabs from the right panel to LeftSplitter: {tabs.ToString(CultureInfo.InvariantCulture)}");
        Assert.True(app.FocusVisible, "LeftSplitter shows no keyboard frame.");
        var columns = app.Find<Grid>(K.Ids.MainArea).ColumnDefinitions;
        var graphLeft = columns[0].ActualWidth;
        var graphWidth = columns[1].ActualWidth;
        Assert.Equal(K.LeftBefore, graphLeft, 1.0);
        app.MoveToNeutral();
        app.PressKey(Key.Right);
        await app.WaitForAsync(() => Math.Abs(columns[0].ActualWidth - K.LeftAfter) < 0.5, "the left panel 270 wide");
        await app.WaitForAsync(() => app.Session.SessionState.Current.LeftPanelWidth is { } width && Math.Abs(width - K.LeftAfter) < 0.5, "the session's left width 270");
        Picture(app, K.K8aSplitter);
        Assert.True(splitter.IsFocused && app.FocusVisible, "LeftSplitter lost the focus or its frame.");
        Assert.Equal(K.LeftAfter, app.FindByAutomationId(K.Ids.LeftPanel).Bounds.Width, 1.0);
        Assert.Equal(graphWidth - 10, columns[1].ActualWidth, 1.0);
        Assert.Equal(K.RightWidth, columns[2].ActualWidth, 1.0);
        Assert.Equal(K.RightWidth, app.FindByAutomationId(K.Ids.RightPanel).Bounds.Width, 1.0);
        Assert.Equal(exact, await repo.ExactStateAsync());

        // The method's end: no held fetch gave up (the hold is disposed, a release, by its using).
        Assert.Empty(hold.GaveUp());
    }

    /// <summary>
    /// Method 4, K4 on a fresh "changes": F6 to the first unstaged row, its buttons undrawn; Tab,
    /// Stage drawn and framed; Enter stages README.md and the focus goes to the row now first.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K4_row_buttons_by_tab()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo.Path);
        var exact = await repo.ExactStateAsync();

        // Steps 2 and 3.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, K.Unstaged7, K.Staged3);
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRowPath() == K.Readme, "the focus on README.md's row");
        AssertFocusedRowAlone(app, K.Readme);

        // Step 4 (k4a): Tab, Stage drawn and framed.
        await app.TabToAsync(focused => IsRowButton(focused, K.Readme, K.Ids.RowStageButton), "README.md's Stage");
        Picture(app, K.K4aRowStage);
        var row = app.StageRow(K.Ids.UnstagedFileList, K.Readme)!;
        Assert.True(app.FocusVisible, "Stage shows no keyboard frame.");
        Assert.Equal((1.0, 0.0), (ShellDriver.RowButton(row, K.Ids.RowStageButton).Opacity, ShellDriver.RowButton(row, K.Ids.RowDiscardButton).Opacity));
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 5 (k4b): Enter stages README.md; the focus on the row now first.
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => Rows(app).UnstagedTitle == K.Unstaged6 && app.FocusedRowPath() == K.Logo, $"README.md staged and the focus on assets/logo.png's row (it is on {app.DescribeFocus()})");
        await app.WaitForIdleAsync();
        Picture(app, K.K4bRowStaged);
        Assert.Equal("M  README.md", (await repo.GitAsync("status", "--porcelain", "--", K.Readme)).StandardOutput.TrimEnd('\r', '\n'));
        Assert.Equal("5 1 README.md", (await repo.GitAsync("diff", "--cached", "--numstat", "--", K.Readme)).StandardOutput.Trim().Replace('\t', ' '));
        Assert.Empty((await repo.GitAsync("diff", "--numstat", "--", K.Readme)).StandardOutput.Trim());
        AssertChangedOnlyIn(exact, await repo.ExactStateAsync(), K.Readme);
        Assert.Equal(K.Logo, Rows(app).Unstaged.Rows[0].Key);
        Assert.Equal(K.Staged3, Rows(app).StagedTitle);
        Assert.True(app.FocusIsWithin(app.FindByAutomationId(K.Ids.UnstagedFileList)), "The focus is not in the unstaged list.");
        Assert.True(app.FocusVisible, "assets/logo.png's row shows no keyboard frame.");
        var logo = app.StageRow(K.Ids.UnstagedFileList, K.Logo)!;
        Assert.Equal((0.0, 0.0), (ShellDriver.RowButton(logo, K.Ids.RowStageButton).Opacity, ShellDriver.RowButton(logo, K.Ids.RowDiscardButton).Opacity));
        Assert.Null(Repository(app).Diff);
    }

    /// <summary>
    /// Method 5, K5 on one empty tab, the fake of GitHub serving a copy of 7a's "hosting" on a free
    /// port: "From GitHub…" by Shift+Tab and Enter, <c>octo/hello</c> by Tab and Home, Choose by the
    /// keys; the form filled, "From GitHub…" focused without a frame, no <c>clone</c> call.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K5_focus_after_choose()
    {
        // Step 1: the fake serving a copy of "hosting"; the start as 7a's StartEmptyAsync(data, served); signed in.
        var copy = await Scenarios.HostingAsync();
        ServedHosting served;
        try
        {
            served = await Scenarios.ServeHostingAsync(copy, port: 0, ct: TestContext.Current.CancellationToken, ownsCopy: true);
        }
        catch
        {
            copy.Dispose();
            throw;
        }

        await using var serving = served;
        using var data = new TempDirectory("data");
        using var app = await StartEmptyServedAsync(data, served);
        var mark = app.CallMark();
        await app.SignInAsync(served.OctoToken, TestContext.Current.CancellationToken);

        // Step 2: the clone form, its URL box focused.
        app.Click(app.FindByAutomationId(K.Ids.CloneRepoButton));
        var button = app.Find<Button>(K.Ids.CloneFromGitHubButton);
        await app.WaitForAsync(() => app.FocusedAutomationId() == K.Ids.CloneUrlBox && button.IsEffectivelyVisible, "the clone form with the URL box focused");
        Assert.Equal(K.FromGitHub, AutomationProperties.GetName(button));

        // Step 3: Shift+Tab to From GitHub….
        await app.TabToAsync(focused => ReferenceEquals(focused, button), K.FromGitHub, shift: true);
        Assert.True(app.FocusVisible, "From GitHub… shows no keyboard frame.");

        // Step 4: Enter opens the browser.
        app.PressKey(Key.Enter);
        var browser = await app.WaitForDialogAsync<CloneFromGitHubDialogViewModel>();
        await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's list");
        await app.WaitForAsync(() => app.FocusedAutomationId() == K.Ids.CloneFromGitHubFilter, "the filter focused");
        Assert.Equal(K.CloneFromGitHubTitle, browser.Title);
        Assert.True(string.IsNullOrEmpty(browser.Filter));
        Assert.Equal(K.BrowserRows, browser.Rows.Select(row => row.FullName));
        Assert.Contains(K.Private, browser.Rows[0].Detail ?? string.Empty, StringComparison.Ordinal);
        Assert.Null(browser.Selected);
        Assert.False(app.FindByAutomationId(app.TopDialogView!, K.Ids.ConfirmButton).IsEffectivelyEnabled, "Choose is enabled with nothing selected.");

        // Step 5: Tab into the list, Home selects octo/hello.
        await app.TabToAsync(focused => focused is Control control && control.GetSelfAndVisualAncestors().OfType<Control>().Any(ancestor => AutomationProperties.GetAutomationId(ancestor) == K.Ids.CloneFromGitHubList), "the browser's list");
        app.PressKey(Key.Home);
        await app.WaitForAsync(() => browser.Selected?.FullName == K.OctoHello, "octo/hello selected");
        Assert.True(app.FindByAutomationId(app.TopDialogView!, K.Ids.ConfirmButton).IsEffectivelyEnabled, "Choose is disabled with octo/hello selected.");

        // Step 6 (k5a): Choose, by the keys. Enter in the list is the list's item's (it selects
        // and chooses nothing), so Choose is reached by Tab and pressed by Enter.
        await app.TabToAsync(focused => focused is Control { Name: K.Ids.CancelButton }, "Cancel");
        await app.TabToAsync(focused => focused is Control { Name: K.Ids.ConfirmButton }, K.Choose);
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => browser.IsClosed && button.IsFocused, $"the browser closed and From GitHub… focused (it is on {app.DescribeFocus()})");
        app.MoveToNeutral();
        Picture(app, K.K5aChosen);
        var welcome = app.Session.Shell.ActiveTab.Welcome;
        Assert.Equal(K.HelloUrl(served.GitHub.Port), welcome.CloneUrl);
        Assert.Equal(K.HelloUrl(served.GitHub.Port), app.Find<TextBox>(K.Ids.CloneUrlBox).Text);
        Assert.True(string.IsNullOrEmpty(app.Find<TextBox>(K.Ids.CloneParentBox).Text));
        Assert.Equal(K.Hello, app.Find<TextBox>(K.Ids.CloneNameBox).Text);
        Assert.True(button.IsEffectivelyEnabled && button.IsFocused, "From GitHub… is not enabled with the focus.");
        Assert.False(app.FocusVisible, "From GitHub… shows the keyboard's frame.");
        Assert.DoesNotContain(CallsSince(app, mark), call => call.StartsWith("git clone", StringComparison.Ordinal));
    }

    /// <summary>
    /// Method 6, K6 on a fresh "changes", the held runner handed over: README.md's diff opened by
    /// Enter with its <c>diff-files</c> read held, Ctrl+Shift+M, the release: the summary keeps the
    /// focus and its accent border.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K6_loading_diff_keeps_the_summary()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        var held = new HeldGitRunner();
        using var app = await OpenAsync(data, repo.Path, gitProbe: held.Probe);
        var exact = await repo.ExactStateAsync();

        // Step 2.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, K.Unstaged7, K.Staged3);
        await app.WaitForIdleAsync();

        // Step 3: F6 to README.md; the read held; Enter.
        app.PressF6();
        await app.WaitForAsync(() => app.FocusedRowPath() == K.Readme, "the focus on README.md's row");
        held.HoldNext(K.DiffFilesWord);
        app.PressKey(Key.Enter);
        var call = string.Join(' ', (await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken)).Arguments);
        Assert.StartsWith(K.HeldDiffStart, call, StringComparison.Ordinal);
        Assert.EndsWith(K.HeldDiffEnd, call, StringComparison.Ordinal);
        app.Settle();
        Assert.Equal(K.Readme, Repository(app).Diff?.Target.Path);
        Assert.False(app.Find<Panel>(K.Ids.DiffTextHost).IsEffectivelyVisible, "The diff's text shows while its read is held.");
        Assert.Equal(K.Readme, app.FocusedRowPath());

        // Step 4: Ctrl+Shift+M, the summary with its focused border.
        app.PressKey(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.FocusedAutomationId() == K.Ids.CommitSummary, "the focus in the summary");
        AssertFocusedBorder(app, picture: null);

        // Step 5 (k6a): the release; the diff shows, the summary keeps the focus.
        held.Release();
        await app.WaitForAsync(() => Repository(app).Diff is { ShowsText: true }, "README.md's diff");
        await app.WaitForIdleAsync();
        var shot = Picture(app, K.K6aSummaryKept);
        Assert.Contains(Repository(app).Diff!.Diff!.Hunks.SelectMany(hunk => hunk.Lines), line => line.Text == K.ReadmeWorkingLine);
        Assert.True(app.FocusedAutomationId() == K.Ids.CommitSummary, $"The read took the focus from the summary to {app.DescribeFocus()}.");
        AssertFocusedBorder(app, shot);
        Assert.False(app.FocusIsWithin(app.FindByAutomationId(K.Ids.DiffView)), "Something in the diff has the focus.");
        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// Method 7, K7 on a fresh "changes", the held runner handed over: "First", Ctrl+Enter, the
    /// <c>commit</c> held: Committing…, Amend disabled; "Next" typed meanwhile; the release: HEAD's
    /// message "First", the summary "Next".
    /// </summary>
    [AvaloniaFact]
    public async Task Check_K7_committing()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        var held = new HeldGitRunner();
        using var app = await OpenAsync(data, repo.Path, gitProbe: held.Probe);
        var unstaged = (await repo.GitAsync("diff", "--numstat")).StandardOutput;
        var untracked = (await repo.GitAsync("ls-files", "--others", "--exclude-standard")).StandardOutput;

        // Steps 2 and 3: the summary "First".
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, K.Unstaged7, K.Staged3);
        app.PressKey(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift);
        await app.WaitForAsync(() => app.FocusedAutomationId() == K.Ids.CommitSummary, "the focus in the summary");
        app.Type(K.First);
        var summary = app.Find<TextBox>(K.Ids.CommitSummary);
        var commit = app.Find<Button>(K.Ids.CommitButton);
        var amend = app.Find<CheckBox>(K.Ids.AmendCheckBox);
        Assert.Equal(K.First, summary.Text);
        Assert.Equal(K.FirstCounter, TextOf(app, K.Ids.SummaryCounter));
        Assert.Equal((K.Commit3Files, true), (commit.Content as string, commit.IsEffectivelyEnabled));
        Assert.Equal((K.AmendPreviousCommit, true, false), (amend.Content as string, amend.IsEffectivelyEnabled, amend.IsChecked == true));

        // Step 4 (k7a): the commit held.
        held.HoldNext(K.CommitWord);
        app.PressKey(Key.Enter, RawInputModifiers.Control);
        var call = string.Join(' ', (await held.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken)).Arguments);
        Assert.Equal(K.HeldCommit, call);
        await app.WaitForAsync(() => commit.Content as string == K.Committing && !amend.IsEffectivelyEnabled, "Committing… with Amend disabled");
        var shot = Picture(app, K.K7aCommitting);
        Assert.False(commit.IsEffectivelyEnabled, "The commit button is enabled while committing.");
        Assert.False(amend.IsChecked == true);
        Assert.True(summary.IsEffectivelyEnabled && app.Find<TextBox>(K.Ids.CommitDescription).IsEffectivelyEnabled, "A message box is disabled while committing.");
        Assert.Equal(K.First, summary.Text);
        Assert.True(summary.IsFocused, $"The summary lost the focus to {app.DescribeFocus()}.");
        AssertFocusedBorder(app, shot);
        AssertIdleStatusBar(app);
        Assert.Equal(K.AddCalculator, (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());

        // Step 5: Ctrl+A and "Next".
        app.PressKey(Key.A, RawInputModifiers.Control);
        app.Type(K.Next);
        Assert.Equal(K.Next, summary.Text);
        Assert.Equal(K.NextCounter, TextOf(app, K.Ids.SummaryCounter));
        Assert.Equal(K.Committing, commit.Content as string);
        Assert.False(amend.IsEffectivelyEnabled, "Amend is enabled while committing.");
        AssertIdleStatusBar(app);

        // Step 6 (k7b): the release; the commit carries "First", the summary keeps "Next".
        held.Release();
        await app.WaitForAsync(() => !Rows(app).IsCommitting && Rows(app).StagedTitle == K.Staged0, "the commit to end and the lists read again");
        await app.WaitForIdleAsync();
        shot = Picture(app, K.K7bCommitted);
        Assert.Equal(K.First, (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.Trim());
        Assert.Equal(K.AddCalculator, (await repo.GitAsync("log", "-1", "--format=%s", "HEAD~1")).StandardOutput.Trim());
        Assert.Empty((await repo.GitAsync("diff", "--cached", "--name-only")).StandardOutput.Trim());
        Assert.Equal(unstaged, (await repo.GitAsync("diff", "--numstat")).StandardOutput);
        Assert.Equal(untracked, (await repo.GitAsync("ls-files", "--others", "--exclude-standard")).StandardOutput);
        Assert.Equal(K.Next, summary.Text);
        Assert.Equal(K.NextCounter, TextOf(app, K.Ids.SummaryCounter));
        Assert.True(summary.IsFocused, $"The summary lost the focus to {app.DescribeFocus()}.");
        AssertFocusedBorder(app, shot);
        Assert.True(string.IsNullOrEmpty(app.Find<TextBox>(K.Ids.CommitDescription).Text));
        Assert.Equal((true, false), (amend.IsEffectivelyEnabled, amend.IsChecked == true));
        Assert.Equal((K.StageFilesToCommit, false), (commit.Content as string, commit.IsEffectivelyEnabled));
        Assert.Equal(K.Staged0, Rows(app).StagedTitle);
        Assert.True(app.Find<TextBlock>(K.Ids.NoStaged).IsEffectivelyVisible && app.Find<TextBlock>(K.Ids.NoStaged).Text == K.NoStagedChanges, "No staged changes does not show.");
        Assert.Equal(K.Unstaged7, Rows(app).UnstagedTitle);
        var top = Repository(app).Graph.CommitAt(0);
        Assert.Equal(K.First, top.Subject);
        Assert.Equal((K.Main, top.Sha), (Repository(app).Refs?.Head.BranchName, Repository(app).Refs?.Head.Sha));
        Assert.True(Repository(app).IsWorkingRowSelected, "The working-changes row is no longer selected.");
        Assert.False(app.Find<TextBlock>(K.Ids.CommitError).IsEffectivelyVisible, "CommitError shows.");
        AssertIdleStatusBar(app);
    }

    // ---- Helpers ------------------------------------------------------------------------------

    /// <summary>The graph's row of "Add settings page", HEAD's commit, on "graph".</summary>
    private const int HeadRow = 2;

    private static RepositoryViewModel Repository(ShellDriver app) => app.Session.Shell.ActiveTab.Repository!;

    private static WorkingChangesViewModel Rows(ShellDriver app) => Repository(app).Changes;

    /// <summary><see cref="Phase8aChecks.Shot"/>, then no clipped text in the window (the report's conventions).</summary>
    private static Screenshot Picture(ShellDriver app, string name, bool keepPointer = false)
    {
        var picture = Shot(app, name, keepPointer);
        AssertNoClippedText(app);
        return picture;
    }

    /// <summary>
    /// The start of K5 (open point 43): one empty tab, the fake of GitHub as GitHub, dark, 1100×700,
    /// as phase 7a's <c>StartEmptyAsync(data, served)</c> starts; then the pointer at rest.
    /// </summary>
    private static async Task<ShellDriver> StartEmptyServedAsync(TempDirectory data, ServedHosting served)
    {
        var app = ShellDriver.Start(data.Path, DefaultSize.Width, DefaultSize.Height, gitHub: served.GitHub);
        try
        {
            await app.WaitUntilReadyAsync();
        }
        catch
        {
            app.Dispose();
            throw;
        }

        app.MoveToNeutral();
        return app;
    }

    /// <summary>The stage panel shown with its two titles, the app idle.</summary>
    private static async Task WaitForStagePanelAsync(ShellDriver app, string unstaged, string staged)
    {
        await app.WaitForAsync(
            () => app.Session.Shell.ActiveTab.ShowsStagePanel && Rows(app).UnstagedTitle == unstaged && Rows(app).StagedTitle == staged
                && app.StageRows(K.Ids.UnstagedFileList).Count + app.StageRows(K.Ids.StagedFileList).Count > 0,
            $"the stage panel with {unstaged} and {staged}");
        await app.WaitForIdleAsync();
    }

    /// <summary>The details' subject, once the right panel shows it.</summary>
    private static Task WaitForSubjectAsync(ShellDriver app, string subject) =>
        app.WaitForAsync(() => TextOf(app, K.Ids.DetailsSubject) == subject, $"the details of \"{subject}\"");

    /// <summary>The text of the shown control whose automation id is <paramref name="automationId"/>, or null while none shows.</summary>
    private static string? TextOf(ShellDriver app, string automationId) =>
        app.Window.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.IsEffectivelyVisible && AutomationProperties.GetAutomationId(control) == automationId) switch
        {
            TextBlock block => block.Text,
            TextBox box => box.Text,
            _ => null,
        };

    /// <summary>The automation id of <paramref name="focused"/> or of the nearest ancestor that has one.</summary>
    private static string? FocusedId(IInputElement? focused) =>
        (focused as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().Select(AutomationProperties.GetAutomationId).FirstOrDefault(id => !string.IsNullOrEmpty(id));

    /// <summary>Whether <paramref name="focused"/> is the button <paramref name="id"/> of the unstaged list's row <paramref name="path"/>.</summary>
    private static bool IsRowButton(IInputElement? focused, string path, string id) =>
        focused is Control control && AutomationProperties.GetAutomationId(control) == id
        && ShellDriver.StageRowOf(control)?.DataContext is ChangedFileRow { Key: var key } && key == path
        && control.GetVisualAncestors().OfType<Control>().Any(ancestor => AutomationProperties.GetAutomationId(ancestor) == K.Ids.UnstagedFileList);

    /// <summary>
    /// The state after F6 (K1 step 3, K4 step 3; phase 6's k12b): <paramref name="path"/>'s own button
    /// has the focus with the frame, in the right panel; its row, neither hovered nor selected, has its
    /// buttons in the tree's shown part at opacity 0; no other row shows its buttons.
    /// </summary>
    private static void AssertFocusedRowAlone(ShellDriver app, string path)
    {
        Assert.True(app.FocusVisible, $"{path}'s row shows no keyboard frame.");
        Assert.Equal(KeyRegionRole.Right, app.FocusedRegion());
        var row = app.StageRow(K.Ids.UnstagedFileList, path)!;
        Assert.False(row.IsPointerOver || row.Classes.Contains("selected"), $"{path}'s row is hovered or selected.");
        var stage = ShellDriver.RowButton(row, K.Ids.RowStageButton);
        var discard = ShellDriver.RowButton(row, K.Ids.RowDiscardButton);
        Assert.True(stage.IsEffectivelyVisible && discard.IsEffectivelyVisible, $"{path}'s buttons are not shown in the tree.");
        Assert.Equal((0.0, 0.0), (stage.Opacity, discard.Opacity));
        AssertNoOtherRowButtons(app, row);

        // Every realized row keeps its buttons in the visual tree, shown or not: an unstaged row its
        // Stage and Discard, a staged row its Unstage (RowButton fails for a row without one).
        foreach (var unstaged in app.StageRows(K.Ids.UnstagedFileList))
        {
            _ = ShellDriver.RowButton(unstaged, K.Ids.RowStageButton);
            _ = ShellDriver.RowButton(unstaged, K.Ids.RowDiscardButton);
        }

        foreach (var staged in app.StageRows(K.Ids.StagedFileList))
        {
            _ = ShellDriver.RowButton(staged, K.Ids.RowUnstageButton);
        }
    }

    /// <summary>
    /// After a Tab of K1's walk to <paramref name="id"/> of <paramref name="path"/>'s row: a focused
    /// Stage or Discard is drawn (opacity 1) and the row's other button is not (0); a row's own
    /// button focused draws neither; <paramref name="previous"/>, the row the focus has just left,
    /// shows no buttons; nor does any other row.
    /// </summary>
    private static void AssertRowButtonsAfterTab(ShellDriver app, string path, string id, string? previous)
    {
        var row = app.StageRow(K.Ids.UnstagedFileList, path)!;
        var stage = ShellDriver.RowButton(row, K.Ids.RowStageButton);
        var discard = ShellDriver.RowButton(row, K.Ids.RowDiscardButton);
        var expected = id switch
        {
            K.Ids.RowStageButton => (1.0, 0.0),
            K.Ids.RowDiscardButton => (0.0, 1.0),
            _ => (0.0, 0.0),
        };
        Assert.Equal(expected, (stage.Opacity, discard.Opacity));
        if (previous is not null)
        {
            Assert.False(
                app.StageRow(K.Ids.UnstagedFileList, previous) is { } left && ShellDriver.RowButton(left, K.Ids.RowStageButton).IsEffectivelyVisible,
                $"{previous}'s buttons still show after the focus left its row.");
        }

        AssertNoOtherRowButtons(app, row);
    }

    private static void AssertNoOtherRowButtons(ShellDriver app, Panel row)
    {
        foreach (var other in app.StageRows(K.Ids.UnstagedFileList).Concat(app.StageRows(K.Ids.StagedFileList)).Where(other => !ReferenceEquals(other, row)))
        {
            Assert.DoesNotContain(
                other.GetVisualDescendants().OfType<Button>(),
                button => button.IsEffectivelyVisible && AutomationProperties.GetAutomationId(button) is K.Ids.RowStageButton or K.Ids.RowDiscardButton or K.Ids.RowUnstageButton);
        }
    }

    /// <summary>K1's diff header: <c>Calculator.cs</c> in <c>src</c>, inline.</summary>
    private static void AssertDiffHeader(ShellDriver app)
    {
        Assert.Equal(K.CalculatorName, app.Find<TextBlock>(K.Ids.DiffFileName).Text);
        Assert.Equal(K.CalculatorFolder, app.Find<TextBlock>(K.Ids.DiffFolder).Text);
        Assert.True(app.Find<Button>(K.Ids.InlineDiffButton).Classes.Contains("selected"), "Inline is not selected.");
        Assert.False(app.Find<Button>(K.Ids.SideBySideDiffButton).Classes.Contains("selected"), "Side by side is selected.");
        Assert.Equal((K.Inline, K.SideBySide), (app.Find<Button>(K.Ids.InlineDiffButton).Content as string, app.Find<Button>(K.Ids.SideBySideDiffButton).Content as string));
    }

    /// <summary>
    /// The diff's toolbar on an unstaged diff: Inline and Side by side shown and enabled, Stage file and
    /// Discard file shown and enabled, or, with <paramref name="lines"/>, Stage lines and Discard lines
    /// in their place; the unstage buttons never. The others are hidden, not disabled: no toolbar
    /// button is disabled.
    /// </summary>
    private static void AssertToolbar(ShellDriver app, bool lines)
    {
        string[] shown = lines
            ? [K.Ids.InlineDiffButton, K.Ids.SideBySideDiffButton, K.Ids.StageLinesButton, K.Ids.DiscardLinesButton]
            : [K.Ids.InlineDiffButton, K.Ids.SideBySideDiffButton, K.Ids.StageFileButton, K.Ids.DiscardFileButton];
        string[] hidden = lines
            ? [K.Ids.StageFileButton, K.Ids.DiscardFileButton, K.Ids.UnstageLinesButton, K.Ids.UnstageFileButton]
            : [K.Ids.StageLinesButton, K.Ids.DiscardLinesButton, K.Ids.UnstageLinesButton, K.Ids.UnstageFileButton];
        foreach (var id in shown)
        {
            var button = app.Find<Button>(id);
            Assert.True(button.IsEffectivelyVisible && button.IsEffectivelyEnabled, $"{id} is not shown and enabled.");
        }

        foreach (var id in hidden)
        {
            Assert.False(app.Find<Button>(id).IsEffectivelyVisible, $"{id} is shown.");
        }

        Assert.Equal(lines ? (K.StageLines, K.DiscardLines) : (K.StageFile, K.DiscardFile), (app.Find<Button>(shown[2]).Content as string, app.Find<Button>(shown[3]).Content as string));
    }

    /// <summary>The hunk bars' buttons shown in the diff, in hunk order.</summary>
    private static List<Button> HunkButtons(ShellDriver app) =>
        [.. app.Find<DiffTextView>(K.Ids.DiffText).GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible)];

    /// <summary>The summary has the focus with Fluent's focused border: its template's <c>PART_BorderElement</c> in <c>TextControlBorderBrushFocused</c>, the box's own brush as it was, and in <paramref name="picture"/> the left border's pixel.</summary>
    private static void AssertFocusedBorder(ShellDriver app, Screenshot? picture)
    {
        var summary = app.Find<TextBox>(K.Ids.CommitSummary);
        Assert.True(summary.IsFocused, $"The summary does not have the focus: {app.DescribeFocus()}.");
        var border = summary.GetVisualDescendants().OfType<Border>().First(part => part.Name == "PART_BorderElement");
        Assert.Equal(K.FocusedBorderDark, (border.BorderBrush as ISolidColorBrush)?.Color);
        Assert.Equal(K.BoxBorderDark, (summary.BorderBrush as ISolidColorBrush)?.Color);
        if (picture is not null)
        {
            var bounds = app.BoundsOf(summary);
            AssertColour(K.FocusedBorderDark, picture, new Point(bounds.Left + 0.5, bounds.Center.Y), "the summary's left border (TextControlBorderBrushFocused)");
        }
    }

    /// <summary>The status bar while a stage-panel write runs or after it: Ready, no progress, no Cancel (K7).</summary>
    private static void AssertIdleStatusBar(ShellDriver app)
    {
        Assert.Equal(K.Ready, TextOf(app, K.Ids.OperationStatus));
        Assert.False(app.FindByAutomationId(K.Ids.OperationProgress).IsEffectivelyVisible, "The progress bar shows.");
        Assert.False(app.FindByAutomationId(K.Ids.OperationCancelButton).IsEffectivelyVisible, "Cancel shows.");
    }

    private static void AssertColour(Color expected, Screenshot picture, Point point, string what)
    {
        var actual = picture.PixelAt(point);
        Assert.True(actual == expected, $"The {what} at {point} is {actual}, not {expected}.");
    }

    /// <summary>The window's y of the centre of the blame's line <paramref name="line"/> (from 1).</summary>
    private static double LineCentreY(ShellDriver app, BlameTextEditor text, int line)
    {
        var view = text.TextView;
        var top = view.GetVisualTopByDocumentLine(line) - view.VerticalOffset;
        return view.TranslatePoint(new Point(0, top + (view.DefaultLineHeight / 2)), app.Window)!.Value.Y;
    }

    /// <summary>Opens the palette, asserts that no row's label starts with "Cancel ", and closes it (K3 steps 2 and 6).</summary>
    private static async Task AssertNoCancelEntryAsync(ShellDriver app)
    {
        app.MoveToNeutral();
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen, "the palette");
        Assert.DoesNotContain(app.Palette.Shown, entry => entry.Label.StartsWith(K.CancelPrefix, StringComparison.Ordinal));
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsPaletteOpen, "the palette to close");
    }

    /// <summary>
    /// "Fetch origin" from the menu of <c>origin</c> under Remotes; waits until it is held and Cancel
    /// shows (K3 step 3, K8 step 7). Returns the operation log's count before it, to find its record.
    /// </summary>
    private static async Task<int> StartHeldFetchAsync(ShellDriver app, FetchHold hold)
    {
        var operations = app.Session.Operations.Snapshot().Count;
        app.RightClick(await app.RefRowAsync(K.Remotes, K.Origin));
        var menu = app.Menu();
        Assert.Equal(K.Origin, menu.Header);
        app.ClickMenuItem(K.FetchOrigin);
        await hold.WaitForHeldAsync(1);
        await app.WaitForAsync(() => app.FindByAutomationId(K.Ids.OperationCancelButton).IsEffectivelyVisible, "Cancel to show");
        AssertStatus(app, K.FetchingFromOrigin);
        var progress = (ProgressBar)app.FindByAutomationId(K.Ids.OperationProgress);
        Assert.True(progress.IsEffectivelyVisible && progress.IsIndeterminate, "The progress bar is not shown without a percentage.");
        Assert.Contains(K.StatusCancel, VisibleTexts.In(app.FindByAutomationId(K.Ids.OperationCancelButton)).Append(AutomationProperties.GetName(app.FindByAutomationId(K.Ids.OperationCancelButton)) ?? string.Empty));
        return operations;
    }

    /// <summary>Whether the fetch started after the log's count <paramref name="operations"/> has its record: it has ended.</summary>
    private static bool FetchEnded(ShellDriver app, int operations) =>
        app.Session.Operations.Snapshot().Skip(operations).Any(record => record.Title == K.FetchOrigin);

    /// <summary>The fetch ended cancelled: its record, the status bar's text, no progress or Cancel, and no remote ref moved (K3 step 5, K8 step 8).</summary>
    private static async Task AssertFetchCancelledAsync(ShellDriver app, int fetch, TempRepo repo, string refs)
    {
        await app.WaitForAsync(() => FetchEnded(app, fetch), "the fetch to end");
        await app.WaitForAsync(() => !app.IsPaletteOpen, "the palette to close");
        Assert.Equal(OperationOutcome.Cancelled, app.Session.Operations.Snapshot().Skip(fetch).Last(record => record.Title == K.FetchOrigin).Outcome);
        await app.WaitForIdleAsync();
        AssertStatus(app, K.FetchOriginCancelled);
        Assert.False(app.FindByAutomationId(K.Ids.OperationProgress).IsEffectivelyVisible, "The progress bar shows.");
        Assert.False(app.FindByAutomationId(K.Ids.OperationCancelButton).IsEffectivelyVisible, "Cancel shows.");
        Assert.Equal(refs, await RefsAsync(repo));
    }

    /// <summary>F6, at most four times, until the focus is in the right panel (a held fetch may run meanwhile).</summary>
    private static async Task F6ToRightPanelAsync(ShellDriver app)
    {
        for (var f6 = 0; app.FocusedRegion() != KeyRegionRole.Right; f6++)
        {
            Assert.True(f6 < 4, $"Four F6 did not reach the right panel: the focus is on {app.DescribeFocus()}.");
            app.PressF6();
            await app.WaitForAsync(() => app.FocusedElement is not null, "the focus after F6");
        }
    }

    /// <summary>Tab, one at a time and at most <see cref="K.WalkLimit"/> times, until <paramref name="reached"/>; the number of Tabs.</summary>
    private static async Task<int> WalkAsync(ShellDriver app, Func<bool> reached, string what)
    {
        var tabs = 0;
        while (!reached())
        {
            Assert.True(tabs < K.WalkLimit, $"{K.WalkLimit} Tabs did not reach {what}: the focus is on {app.DescribeFocus()}.");
            await app.TabToAsync(_ => true, $"the next element on the way to {what}");
            tabs++;
        }

        return tabs;
    }

    /// <summary><c>git for-each-ref</c>: every ref with its object.</summary>
    private static async Task<string> RefsAsync(TempRepo repo) =>
        (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput;

    /// <summary><c>git diff [--cached] --numstat -- src/Calculator.cs</c>, its two tabs written as single spaces.</summary>
    private static async Task<string> NumstatAsync(TempRepo repo, bool cached)
    {
        string[] arguments = cached ? ["diff", "--cached", "--numstat", "--", K.Calculator] : ["diff", "--numstat", "--", K.Calculator];
        return (await repo.GitAsync(arguments)).StandardOutput.Trim().Replace('\t', ' ');
    }

    /// <summary>The index's <c>src/Calculator.cs</c> (<c>git show :src/Calculator.cs</c>).</summary>
    private static async Task<string> IndexFileAsync(TempRepo repo) =>
        (await repo.GitAsync("show", ":" + K.Calculator)).StandardOutput.ReplaceLineEndings("\n");

    /// <summary>Two exact states (<see cref="TempRepo.ExactStateAsync"/>) differ only in lines that name <paramref name="path"/>: its index entry and its status line.</summary>
    private static void AssertChangedOnlyIn(string before, string after, string path)
    {
        var old = before.Split('\n');
        var now = after.Split('\n');
        Assert.Equal(old.Length, now.Length);
        var changed = old.Zip(now).Where(pair => pair.First != pair.Second).ToList();
        Assert.NotEmpty(changed);
        Assert.All(changed, pair => Assert.True(pair.First.Contains(path, StringComparison.Ordinal) && pair.Second.Contains(path, StringComparison.Ordinal), $"A line changed that is not {path}'s: \"{pair.First}\" became \"{pair.Second}\"."));
    }
}
