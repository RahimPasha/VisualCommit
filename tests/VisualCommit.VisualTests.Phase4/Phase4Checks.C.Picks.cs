// Check C12 (docs/test-reports/phase-4.md, part C): a cherry-pick of three commits that stops at
// the second (git's list, D136): the several-commits menu, the banner with one more commit
// waiting, the stage panel's Continue cherry-pick, the panes with the pick's titles, Use ours then
// a typed edit, Mark resolved, a message edited in the stage panel, and Continue.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksC
{
    /// <summary>
    /// Check C12, one app start on "conflicts": Pick last, Pick conflicting and Pick clean
    /// selected and cherry-picked onto main in one call, which stops at Pick conflicting; the
    /// conflict resolved in the resolver (Use ours, then typing), the message edited in the stage
    /// panel, and the list continued by the stage panel's Continue cherry-pick.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_C12_cherry_pick_list_stopped_and_continued()
    {
        using var repo = await Scenarios.ConflictsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: three commits selected, then their menu.
        app.Click(app.GraphRowPoint(2));
        await WaitForDetailsOfAsync(app, "Pick last");
        app.Click(app.GraphRowPoint(4), KeyModifiers.Shift);
        var pointer = app.GraphRowPoint(4);
        app.RightClick(pointer);
        var menu = app.Menu();
        var shot = Shot(app, "c12a-menu");
        AssertSelectedCommits(app, shot, "5c4c257", "01fd976", "01d2046");
        Assert.Equal("3 commits selected", ((TextBlock)app.FindByAutomationId("MultiSelectionTitle")).Text);
        AssertMenu(menu, ThreeCommitsMenu);
        AssertHighlighted(menu, "Cherry-pick 3 commits onto main");
        Assert.True(app.MenuItemControl("Cherry-pick 3 commits onto main").IsFocused, "The highlighted item does not have the keyboard focus.");
        AssertMenuShape(app, menu);
        AssertMenuAtPointer(menu, pointer);
        Assert.Equal(145, menu.Bounds.Height, 2.0);

        // Step 2: one git cherry-pick of the three, oldest first, stops at Pick conflicting.
        var logMark = app.LogMark();
        var callMark = app.GitCalls().Count;
        app.ClickMenuItem("Cherry-pick 3 commits onto main");
        await WaitForOperationAsync(app, repo, PickListOperation, logMark);
        await AssertNoDialogAsync(app);
        shot = Shot(app, "c12b-stopped");
        var pick = AssertCalled(app, callMark, PickListCall);
        Assert.Equal(1, pick.ExitCode);
        Assert.Single(CallsSince(app, callMark), call => call.StartsWith("git cherry-pick", StringComparison.Ordinal));
        await AssertGitPrintedAsync(pick, "[main 25c817e] Pick clean", ConflictInNotes);
        AssertStatus(app, PickListStoppedStatus, OperationStatusTone.Warning);
        var resolveFirst = BannerButton.ContinueDisabled(ResolveFirstOneFile);
        AssertStopBanner(app, shot, PickListBanner, AbortCherryPick, resolveFirst, BannerButton.Skip);
        AssertGraph(app, GcPicksStopped, 5, WorkingOneConflict, showsDate: false, picture: shot);
        AssertSelectedCommits(app, shot, "5c4c257", "01fd976", "01d2046");
        Assert.Equal("3 commits selected", ((TextBlock)app.FindByAutomationId("MultiSelectionTitle")).Text);
        await AssertPicksStopAsync(repo, StopSha.PickedClean);
        await AssertCommitAsync(repo, StopSha.PickedClean, ScenarioTime(5), AppCommitTime, StopSha.OursEdits);
        Assert.Equal(Scenarios.ConflictsFiles.Pick, WorkingFile(repo, "pick.txt"));

        // Step 3: the stage panel; Continue cherry-pick's reason under the pointer.
        await ClickWorkingRowAsync(app);
        await AssertConflictedNotesRowAsync(app);
        await AssertStagePanelAsync(app, UnstagedOneConflict, ["U notes.txt"], StagedNone, [], "Pick conflicting", ContinueCherryPickButton, commitEnabled: false, ResolveFirstOneFile);
        var tip = await app.RestPointerOnAsync(app.Find<Button>("CommitButton"));
        shot = Shot(app, "c12c-stage-panel", keepPointer: true);
        Assert.Equal(ResolveFirstOneFile, tip);
        AssertTipInsideWindow(app, ResolveFirstOneFile);
        AssertWorkingRowSelected(app, shot);
        AssertAmendDisabled(app, FinishCherryPickFirst);
        Assert.Equal(PickListBannerTip, BannerTip(app));

        // Step 4: the resolver with the pick's titles.
        app.MoveToNeutral();
        app.Click(await app.FileRowAsync("UnstagedFileList", ResolverFile));
        await app.WaitForResolverAsync(ResolverFile);
        await app.WaitForIdleAsync();
        shot = Shot(app, "c12d-resolver");
        AssertResolverLayout(app);
        AssertStopBanner(app, shot, PickListBanner, AbortCherryPick, resolveFirst, BannerButton.Skip, overResolver: true);
        AssertResolverState(app, shot, PickTitles, C12Opened);

        // Step 5: Use ours.
        await ChooseAsync(app, 0, ConflictPaneSide.Ours, ConflictChoice.UseOurs);
        shot = Shot(app, "c12e-ours");
        AssertResolverState(app, shot, PickTitles, C12Ours);

        // Step 6: End, then " and picked" typed; the bars keep the choice (14.3).
        app.PressKey(Key.End);
        app.TypeInResult(" and picked");
        await app.WaitForAsync(() => LinesOf(app.ResultText) is [_, "line 2 ours and picked", ..], "the result's line 2 to read \"line 2 ours and picked\"");
        await app.WaitForResultParsedAsync();
        shot = Shot(app, "c12f-typed");
        AssertResolverState(app, shot, PickTitles, C12Typed);
        AssertNoClippedText(app);
        Assert.Equal(NotesPicks, WorkingFile(repo, ResolverFile));
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));

        // Step 7: Mark resolved.
        callMark = app.GitCalls().Count;
        await MarkResolvedAsync(app, repo);
        shot = Shot(app, "c12g-resolved");
        await AssertNoDialogAsync(app);
        Assert.True(AssertCalled(app, callMark, AddNotes).Succeeded);
        AssertStatus(app, ResolvedStatus);
        await AssertResolverClosedAsync(app);
        AssertStopBanner(app, shot, PickListResolvedBanner, AbortCherryPick, BannerButton.Continue, BannerButton.Skip);
        AssertGraph(app, GcPicksStopped, 5, WorkingNoConflict, showsDate: false, picture: shot);
        await AssertStagePanelAsync(app, UnstagedNone, [], StagedOne, ["M notes.txt"], "Pick conflicting", ContinueCherryPickButton, commitEnabled: true);
        Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
        Assert.True(GitHas(repo, "CHERRY_PICK_HEAD"), "CHERRY_PICK_HEAD is gone.");
        Assert.Equal(NotesPicksTyped, WorkingFile(repo, ResolverFile));

        // Step 8: the message edited in the stage panel, then Continue cherry-pick.
        app.ReplaceText("CommitSummary", "Pick conflicting, resolved");
        app.Click(app.FindByAutomationId("CommitDescription"));
        app.Type("#7 Line 2 keeps main's text.");
        logMark = app.LogMark();
        callMark = app.GitCalls().Count;
        app.Click(app.Find<Button>("CommitButton"));
        await WaitForOperationAsync(app, repo, ContinuePickOperation, logMark);
        await AssertNoDialogAsync(app);
        await ClickCommitRowAsync(app, 1, "Pick conflicting, resolved");
        shot = Shot(app, "c12h-finished");
        AssertCalledInOrder(app, callMark, MergeCommitCall, PickContinueCall);
        Assert.True(AssertCalled(app, callMark, MergeCommitCall).Succeeded);
        var continued = AssertCalled(app, callMark, PickContinueCall);
        Assert.True(continued.Succeeded);
        await AssertGitPrintedAsync(continued, "[main 9d814c4] Pick last");
        Assert.Null(app.BannerBounds);
        AssertStatus(app, PickFinishedStatus);
        AssertGraph(app, GcPicked, 5, working: null, showsDate: false, picture: shot);
        AssertSelectedCommits(app, shot, "d512905");
        await AssertCommitDetailsAsync(app, "Pick conflicting, resolved", "#7 Line 2 keeps main's text.", "2026-01-01 12:06", AppDate, ["25c817e"], "M notes.txt");
        Assert.Equal(["## main"], await LinesOfGitAsync(repo, "status", "--short", "--branch"));
        Assert.Equal(
            ["9d814c4 Pick last", "d512905 Pick conflicting, resolved", "25c817e Pick clean", "da31c97 Ours edits"],
            await LinesOfGitAsync(repo, "log", "--format=%h %s", "-4", "main"));
        Assert.Equal(StopSha.PickedLast, await FullShaAsync(repo, "refs/heads/main"));
        Assert.Equal(PickedConflictingMessage, await CommitMessageAsync(repo, StopSha.PickedConflicting));
        await AssertCommitAsync(repo, StopSha.PickedConflicting, ScenarioTime(6), AppCommitTime, StopSha.PickedClean);
        await AssertCommitAsync(repo, StopSha.PickedLast, ScenarioTime(7), AppCommitTime, StopSha.PickedConflicting);
        Assert.Equal(NotesPicksTyped, await FileAtAsync(repo, StopSha.PickedConflicting, ResolverFile));
        Assert.Equal(Scenarios.ConflictsFiles.Last, WorkingFile(repo, "last.txt"));
        Assert.False(GitHas(repo, "CHERRY_PICK_HEAD"), "CHERRY_PICK_HEAD is still there.");
        Assert.False(GitHas(repo, "MERGE_MSG"), "MERGE_MSG is still there.");
        Assert.False(GitHas(repo, "sequencer"), "sequencer/ is still there.");
        Assert.Empty(await BackupsAsync(repo));
    }
}
