// Check C14 of phase 4's walk-through (docs/test-reports/phase-4.md, the row C14 of "Checks"):
// editing the result freely in the resolver on the merge of feature/theirs stopped in a terminal.
// What checks C14 to C17 share is in Phase4Checks.C.EditingSupport.cs; the expected values in
// Phase4Expectations.C2.cs (PartC2).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.Controls;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using Tint = VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC2.Tint;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksC
{
    /// <summary>
    /// Check C14: typing in the result, the platform's undo and redo, Esc inside the result, the
    /// result's and a pane's menus, Take theirs and its undo, Mark resolved with a block left,
    /// leaving with a modified result, Stage all and Continue with markers left; the merge of
    /// feature/theirs stopped, dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_C14_editing_freely()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var start = app.LogMark();
        var unmergedAtStop = await C14To17.UnmergedAsync(repo);

        // When the app has started: GC-M, the merge's banner with its buttons below the text, git's stop.
        AssertGraph(app, PartC2.GcM, PartC2.ConflictsLanes, PartC2.WorkingRow(PartC2.WorkingConflicts(1)), showsDate: false);
        AssertBanner(app, PartC2.MergeTheirsBanner, PartC2.AbortMerge, BannerButton.ContinueDisabled(PartC2.ResolveFirst(1)), oneLine: false, placement: BannerButtonPlacement.OwnRow);
        Assert.Equal(PartC2.MergeStopStatus, await C14To17.PorcelainAsync(repo));
        Assert.Equal(C14To17.Bytes(PartC2.Fm), C14To17.WorkingFile(repo, "notes.txt"));

        // Step 1: the working-changes row, then notes.txt in the unstaged list: the resolver opens.
        await C14To17.OpenResolverAsync(app, "notes.txt", PartC2.UnstagedTitle(1, 1));
        C14To17.AssertResolverPlace(app);
        AssertBanner(app, PartC2.MergeTheirsBanner, PartC2.AbortMerge, BannerButton.ContinueDisabled(PartC2.ResolveFirst(1)), oneLine: null, placement: BannerButtonPlacement.BesideText);
        var resolver = app.Resolver();
        C14To17.AssertHeader(app, resolver, "notes.txt", PartC2.BothModified, PartC2.FilePosition(1, 1));
        C14To17.AssertTextToolbar(app, resolver, PartC2.ConflictPosition(1, 2), mergeTool: false);
        C14To17.AssertTitles(resolver, PartC2.OursMain, PartC2.TheirsTheirs, 2);
        C14To17.AssertPanes(resolver, PartC2.PmOurs(), PartC2.PmTheirs());
        C14To17.AssertResult(resolver, PartC2.Fm, PartC2.FmTints);
        Assert.Equal((C14State.Color("#3A2E16"), C14State.Color("#E0A23B"), C14State.Color("#15323A"), C14State.Color("#2C2140")), (Dark.ConflictMarker, Dark.Warning, Dark.ConflictOurs, Dark.ConflictTheirs));
        C14To17.AssertFocusAndCaret(app, 2, 1);
        Assert.False(resolver.IsModified);

        // Step 2: the typed text in place of block 1, parsed.
        app.ClickResultAt(2, 1);
        for (var press = 0; press < 5; press++)
        {
            app.PressKey(Key.Down, RawInputModifiers.Shift);
        }

        app.Type("line 2 edited by hand");
        app.PressKey(Key.Enter);
        await app.WaitForResultParsedAsync();
        var shot = Shot(app, "c14a-typed");
        C14State.AssertTyped(app);
        C14To17.AssertResultColours(app, shot, PartC2.TypedTints, 820);
        Assert.Equal(PaletteOf(app).TextPrimary, C14State.Color("#E4E7EC"));
        await C14To17.AssertNothingWrittenAsync(app, repo, "notes.txt", PartC2.Fm, PartC2.MergeStopStatus, unmergedAtStop, start);

        // Step 3: Esc inside the result does nothing; the result's menu; the ours pane's menu.
        app.PressKey(Key.Escape);
        app.Settle();
        await app.WaitForIdleAsync();
        Assert.NotNull(app.ShownResolver);
        await AssertNoDialogAsync(app);
        await AssertNoMenuAsync(app, logMark: null);
        C14State.AssertTyped(app);
        var pointer = app.ResultPoint(5, 1);
        app.RightClick(pointer);
        var menu = app.Menu();
        Shot(app, "c14b-result-menu");
        AssertMenu(menu, [.. PartC2.ResultMenuAfterTyping]);
        AssertHighlighted(menu, "Undo");
        Assert.Equal(PartC2.ResultMenuHeight, menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuShape(app, menu);
        // At line 5 (y 533) the 197-high menu does not fit below the pointer, so phase 3's 4a
        // opens it above: its left 2 right of the pointer, its bottom edge 2 above it (the
        // report's expected result, corrected after the UI was built).
        Assert.Equal(pointer.X + WindowLayout.MenuPointerGap, menu.Bounds.X, tolerance: 1.0);
        Assert.Equal(pointer.Y - WindowLayout.MenuPointerGap, menu.Bounds.Bottom, tolerance: 1.0);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.Equal(PartC2.Typed, app.ResultText);

        pointer = app.PaneRowPoint(ConflictPaneSide.Ours, 0);
        app.RightClick(pointer);
        menu = app.Menu();
        Shot(app, "c14c-pane-menu");
        AssertMenu(menu, [.. PartC2.OursPaneMenu]);
        AssertHighlighted(menu, null);
        Assert.Equal(PartC2.PaneMenuHeight, menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuShape(app, menu);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        C14State.AssertTyped(app, focusInResult: false);

        // Step 4: Use theirs in block 2's theirs bar.
        app.ClickBarButton(1, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs);
        shot = Shot(app, "c14d-used-theirs");
        C14State.AssertTypedThenTheirs(app);
        C14To17.AssertResultColours(app, shot, PartC2.Tints(12), 820);
        await C14To17.AssertNothingWrittenAsync(app, repo, "notes.txt", PartC2.Fm, PartC2.MergeStopStatus, unmergedAtStop, start);

        // Step 5: the platform's undo takes back the choice, one step.
        app.Undo();
        shot = Shot(app, "c14e-undone");
        C14State.AssertUndone(app);
        C14To17.AssertResultColours(app, shot, PartC2.TypedTints, 820);

        // Step 6: redo, then undo again.
        app.Redo();
        app.Settle();
        Assert.Equal(PartC2.TypedThenTheirs, app.ResultText);
        Assert.Equal((PartC2.NoConflictsLeft, PartC2.NoConflictsLeft), (app.Resolver().ResultCount, app.Resolver().ConflictPosition));
        app.Undo();
        app.Settle();
        Assert.Equal(PartC2.Typed, app.ResultText);
        Assert.Equal((PartC2.ConflictsLeft(1), PartC2.ConflictPosition(1, 2)), (app.Resolver().ResultCount, app.Resolver().ConflictPosition));

        // Step 7: Take theirs replaces the whole result with feature/theirs' notes.txt; then the undo.
        app.ClickResolverButton("ResolverTakeTheirs");
        await app.WaitForAsync(() => app.ResultText == PartC2.TheirsNotes, "the result to hold feature/theirs' notes.txt");
        shot = Shot(app, "c14f-take-theirs");
        resolver = app.Resolver();
        C14To17.AssertResult(resolver, PartC2.TheirsNotes, PartC2.Tints(12));
        Assert.Equal((PartC2.NoConflictsLeft, PartC2.NoConflictsLeft), (resolver.ResultCount, resolver.ConflictPosition));
        C14To17.AssertBars(app, 0, PartC2.NotUsed, PartC2.Used);
        C14To17.AssertBars(app, 1, PartC2.NotUsed, PartC2.Used);
        C14To17.AssertPanes(resolver, PartC2.PmOurs(Tint.None, Tint.None), PartC2.PmTheirs(Tint.Used, Tint.Used));
        Assert.Equal(PartC2.TheirsNotesBlob, (await repo.GitAsync("rev-parse", "--short=7", ":3:notes.txt")).StandardOutput.Trim());
        Assert.Equal(PartC2.TheirsNotes, (await repo.GitAsync("cat-file", "--filters", "--path=notes.txt", ":3:notes.txt")).StandardOutput);
        C14To17.AssertResultColours(app, shot, PartC2.Tints(12), 820);
        await C14To17.AssertNothingWrittenAsync(app, repo, "notes.txt", PartC2.Fm, PartC2.MergeStopStatus, unmergedAtStop, start);
        app.Undo();
        C14State.AssertUndone(app);

        // Step 8: Mark resolved with a block left asks; Cancel.
        app.ClickResolverButton("ResolverMarkResolved");
        await AssertConfirmationAsync(app, PartC2.MarkersTitle("notes.txt"), PartC2.MarkersQuestion, string.Empty, PartC2.MarkResolved, destructive: false);
        C14To17.AssertConfirmPrimary(app);
        Shot(app, "c14g-markers-question");
        C14To17.ClickDialogButton(app, "CancelButton");
        await AssertNoDialogAsync(app);
        Assert.Equal(PartC2.Typed, app.ResultText);
        await C14To17.AssertNothingWrittenAsync(app, repo, "notes.txt", PartC2.Fm, PartC2.MergeStopStatus, unmergedAtStop, start);

        // Step 9: the close button with a modified result asks; Keep editing; then Discard.
        app.ClickResolverButton("ResolverCloseButton");
        await AssertConfirmationAsync(app, PartC2.DiscardTitle("notes.txt"), PartC2.LeaveQuestion, string.Empty, PartC2.Discard, destructive: true, cancelText: PartC2.KeepEditing);
        C14To17.AssertConfirmDanger(app);
        Shot(app, "c14h-leave-question");
        C14To17.ClickDialogButton(app, "CancelButton");
        await AssertNoDialogAsync(app);
        Assert.NotNull(app.ShownResolver);
        Assert.Equal(PartC2.Typed, app.ResultText);
        app.ClickResolverButton("ResolverCloseButton");
        await AssertConfirmationAsync(app, PartC2.DiscardTitle("notes.txt"), PartC2.LeaveQuestion, string.Empty, PartC2.Discard, destructive: true, cancelText: PartC2.KeepEditing);
        C14To17.ClickDialogButton(app, "ConfirmButton");
        await app.WaitForResolverClosedAsync();
        await AssertNoDialogAsync(app);
        await C14To17.WaitForStagePanelAsync(app, PartC2.UnstagedTitle(1, 1), PartC2.StagedTitle(0));
        await app.WaitForAsync(() => app.Window.FocusManager?.GetFocusedElement() is CommitGraphControl, "the keyboard focus on the graph");
        Shot(app, "c14i-closed");
        AssertGraph(app, PartC2.GcM, PartC2.ConflictsLanes, PartC2.WorkingRow(PartC2.WorkingConflicts(1)), showsDate: false);
        Assert.True(Graph(app).IsWorkingRowSelected, "The working-changes row is not selected.");
        Assert.Equal(["U notes.txt"], C14To17.FileRows(app, "UnstagedFileList"));
        await C14To17.AssertConflictRowAsync(app, "notes.txt");
        C14To17.AssertEmptyLists(app, unstagedEmpty: false, stagedEmpty: true);
        C14To17.AssertMessage(app, PartC2.MergeTheirsSummary, string.Empty);
        C14To17.AssertCommitButton(app, PartC2.StageFilesToCommit, enabled: false);
        AssertBanner(app, PartC2.MergeTheirsBanner, PartC2.AbortMerge, BannerButton.ContinueDisabled(PartC2.ResolveFirst(1)), oneLine: false, placement: BannerButtonPlacement.OwnRow);
        await C14To17.AssertNothingWrittenAsync(app, repo, "notes.txt", PartC2.Fm, PartC2.MergeStopStatus, unmergedAtStop, start);

        // Step 10: Stage all with markers asks; Cancel; again, Mark resolved.
        app.Click(app.Find<Button>("StageAllButton"));
        await AssertConfirmationAsync(app, PartC2.MarkersTitle("notes.txt"), PartC2.MarkersQuestion, string.Empty, PartC2.MarkResolved, destructive: false);
        C14To17.AssertConfirmPrimary(app);
        Shot(app, "c14j-stage-all-question");
        C14To17.ClickDialogButton(app, "CancelButton");
        await AssertNoDialogAsync(app);
        Assert.Equal(PartC2.MergeStopStatus, await C14To17.PorcelainAsync(repo));
        var calls = app.GitCalls().Count;
        app.Click(app.Find<Button>("StageAllButton"));
        await AssertConfirmationAsync(app, PartC2.MarkersTitle("notes.txt"), PartC2.MarkersQuestion, string.Empty, PartC2.MarkResolved, destructive: false);
        C14To17.ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
        await C14To17.WaitForStagePanelAsync(app, PartC2.UnstagedTitle(0), PartC2.StagedTitle(1));
        await app.WaitForIdleAsync();
        Shot(app, "c14k-staged-with-markers");
        var add = AssertCalled(app, calls, PartC2.StageAllNotes);
        Assert.Equal(0, add.ExitCode);
        Assert.Equal(["M  notes.txt"], await C14To17.PorcelainAsync(repo));
        Assert.Equal(PartC2.FmBlobFull, await C14To17.StagedBlobAsync(repo, "notes.txt"));
        Assert.StartsWith(PartC2.TheirsSha, C14To17.GitFile(repo, "MERGE_HEAD"), StringComparison.Ordinal);
        Assert.StartsWith(PartC2.MainSha, await repo.HeadAsync(), StringComparison.Ordinal);
        AssertBanner(app, PartC2.MergeTheirsResolvedBanner, PartC2.AbortMerge, BannerButton.Continue, oneLine: false, placement: BannerButtonPlacement.OwnRow);
        AssertTip(app.BannerContinueButton, null);
        Assert.Equal(PartC2.Working, Graph(app).WorkingRowText);
        C14To17.AssertEmptyLists(app, unstagedEmpty: true, stagedEmpty: false);
        Assert.Equal(["M notes.txt"], C14To17.FileRows(app, "StagedFileList"));
        C14To17.AssertCommitButton(app, PartC2.Commit1File, enabled: true);
        AssertStatus(app, "Ready");

        // Step 11: Continue with markers staged asks first; Cancel commits nothing.
        calls = app.GitCalls().Count;
        var beforeContinue = app.LogMark();
        app.ClickBannerContinue();
        await AssertConfirmationAsync(app, PartC2.ContinueMarkersTitle, PartC2.ContinueMarkersQuestion, string.Empty, PartC2.ContinueText, destructive: false);
        C14To17.AssertConfirmPrimary(app);
        Shot(app, "c14l-continue-question");
        var check = AssertCalled(app, calls, PartC2.CheckStagedMarkers);
        Assert.Equal(2, check.ExitCode);
        if (await IsReportGitAsync())
        {
            Assert.Equal(PartC2.LeftoverMarkerLines, C14To17.Lines(check.StandardOutput).Where(line => line.Contains("leftover", StringComparison.Ordinal)));
        }
        else
        {
            // On another git, that the check named notes.txt (what ReadStagedConflictMarkersAsync reads).
            Assert.Contains(C14To17.Lines(check.StandardOutput), line => line.StartsWith("notes.txt:", StringComparison.Ordinal));
        }

        C14To17.ClickDialogButton(app, "CancelButton");
        await AssertNoDialogAsync(app);
        Assert.StartsWith(PartC2.MainSha, await repo.HeadAsync(), StringComparison.Ordinal);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.NotNull(C14To17.GitFile(repo, "MERGE_HEAD"));
        Assert.Equal(["M  notes.txt"], await C14To17.PorcelainAsync(repo));
        AssertBanner(app, PartC2.MergeTheirsResolvedBanner, PartC2.AbortMerge, BannerButton.Continue, oneLine: false, placement: BannerButtonPlacement.OwnRow);
        Assert.False(C14To17.Logged(app, repo, PartC2.ContinueMergeTitle, beforeContinue), "A Continue the merge operation was logged.");
        AssertNoClippedText(app);
    }

    /// <summary>The states check C14 comes back to, asserted the same way each time.</summary>
    private static class C14State
    {
        /// <summary>
        /// C14 after step 2 (and after step 3): the typed result, its bars and tints, the caret at
        /// line 3, modified; the keyboard focus in the result unless <paramref name="focusInResult"/>
        /// is false (after the pane's menu, whose right-click gave the pane the focus: the report
        /// says only that nothing changed).
        /// </summary>
        public static void AssertTyped(ShellDriver app, bool focusInResult = true)
        {
            var resolver = app.Resolver();
            C14To17.AssertResult(resolver, PartC2.Typed, PartC2.TypedTints);
            Assert.Equal((PartC2.ConflictsLeft(1), PartC2.ConflictPosition(1, 2)), (resolver.ResultCount, resolver.ConflictPosition));
            C14To17.AssertBars(app, 0, PartC2.NotUsed, PartC2.NotUsed);
            C14To17.AssertBars(app, 1, null, null);
            C14To17.AssertPanes(resolver, PartC2.PmOurs(Tint.None, Tint.Ours), PartC2.PmTheirs(Tint.None, Tint.Theirs));
            if (focusInResult)
            {
                C14To17.AssertFocusAndCaret(app, 3, 1);
            }
            else
            {
                Assert.Equal(new AvaloniaEdit.Document.TextLocation(3, 1), app.ResultCaret);
            }

            Assert.True(resolver.IsModified, "The result is not modified.");
            Assert.False(resolver.IsParsePending);
        }

        /// <summary>C14 after step 4: block 2 resolved with its theirs side, nothing left.</summary>
        public static void AssertTypedThenTheirs(ShellDriver app)
        {
            var resolver = app.Resolver();
            C14To17.AssertResult(resolver, PartC2.TypedThenTheirs, PartC2.Tints(12));
            Assert.Equal((PartC2.NoConflictsLeft, PartC2.NoConflictsLeft), (resolver.ResultCount, resolver.ConflictPosition));
            C14To17.AssertBars(app, 0, PartC2.NotUsed, PartC2.NotUsed);
            C14To17.AssertBars(app, 1, PartC2.NotUsed, PartC2.Used);
            C14To17.AssertPanes(resolver, PartC2.PmOurs(Tint.None, Tint.None), PartC2.PmTheirs(Tint.None, Tint.Used));
            C14To17.AssertFocusAndCaret(app, 10, 1);
        }

        /// <summary>C14 after step 5 (and after step 7's undo): step 2's result, block 2 open again.</summary>
        public static void AssertUndone(ShellDriver app)
        {
            var resolver = app.Resolver();
            C14To17.AssertResult(resolver, PartC2.Typed, PartC2.TypedTints);
            Assert.Equal((PartC2.ConflictsLeft(1), PartC2.ConflictPosition(1, 2)), (resolver.ResultCount, resolver.ConflictPosition));
            C14To17.AssertBars(app, 0, PartC2.NotUsed, PartC2.NotUsed);
            C14To17.AssertBars(app, 1, null, null);
            C14To17.AssertPanes(resolver, PartC2.PmOurs(Tint.None, Tint.Ours), PartC2.PmTheirs(Tint.None, Tint.Theirs));
        }

        public static Avalonia.Media.Color Color(string hex) => Avalonia.Media.Color.Parse(hex);
    }
}
