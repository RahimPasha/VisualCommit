// Check B9 of part B (docs/test-reports/phase-4.md): leaving the list. Part A: a row drag cancelled
// by Esc, Alt+Down and Alt+Up, Start disabled in the first order, Esc and Cancel on a changed list
// (the question, Keep editing, Discard), Esc on an unchanged list. Part B, a new start: HEAD moved
// by a commit made outside the app while the list was open. Part of Phase4ChecksB; see
// Phase4Checks.B.cs for the class.
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartB;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksB
{
    /// <summary>Check B9 part A (steps 1 to 8): a cancelled drag, the keys that move a row, the question on leaving a changed list, Esc on an unchanged one.</summary>
    [AvaloniaFact]
    public async Task Check_B9_leave_changed_list()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, GI, Lanes, showsDate: true);

        // Step 1: the list as B6 steps 1 and 2 open it.
        await OpenListAsync(app);
        var logMark = app.LogMark();

        // Step 2: Five dragged to (600, 210); Esc while the button is held cancels the drag; the release moves nothing.
        var from = app.RebaseRowPoint("Five");
        Assert.Equal(L0[0].Point, from);
        app.PressMouse(from);
        try
        {
            app.MoveMouseHeld(B9DragTo, steps: DragSteps);
            var held = app.RebaseListSnapshot();
            Assert.True(app.RebaseListView.IsDragging, "No row drag runs.");
            Assert.Equal(B9DragGap, held.DragGap);
            AssertDragLine(held, B9DragGapY);
            AssertList(app, L0, L0BaseTop, selected: "Five", canStart: false, startToolTip: UnchangedReason, dragged: "Five");

            app.PressKey(Key.Escape);
            var cancelled = app.RebaseListSnapshot();
            Assert.False(app.RebaseListView.IsDragging, "Esc did not cancel the drag.");
            Assert.Null(cancelled.DragLine);
            Assert.Null(cancelled.DragGap);
            Assert.True(app.IsRebaseListOpen, "Esc closed the list during a drag.");
            Assert.Null(app.TopDialogView);
            AssertList(app, L0, L0BaseTop, selected: "Five", canStart: false, startToolTip: UnchangedReason);
        }
        finally
        {
            app.ReleaseMouse(B9DragTo);
        }

        AssertList(app, L0, L0BaseTop, selected: "Five", canStart: false, startToolTip: UnchangedReason);
        await AssertNoMenuAsync(app, logMark);
        await AssertNoDialogAsync(app);

        // Step 3: Alt+Down moves Five one row down.
        app.PressKey(Key.Down, RawInputModifiers.Alt);
        AssertList(app, B9MovedDown, L0BaseTop, selected: "Five", canStart: true, startToolTip: null);
        await AssertDetailsAsync(app, FiveDetails);
        Shot(app, "b9a-moved-by-keys");

        // Step 4: Alt+Up gives the first order back: the list is no longer changed.
        app.PressKey(Key.Up, RawInputModifiers.Alt);
        AssertList(app, L0, L0BaseTop, selected: "Five", canStart: false, startToolTip: UnchangedReason);
        var tip = await RestOnAsync(app, app.FindByAutomationId("RebaseStartButton"));
        Assert.Equal(UnchangedReason, tip);
        AssertToolTipInsideWindow(app, UnchangedReason);
        Shot(app, "b9b-start-disabled", keepPointer: true);

        // Step 5: Alt+Down, then Esc on the changed list: the question.
        app.PressKey(Key.Down, RawInputModifiers.Alt);
        app.PressKey(Key.Escape);
        await AssertConfirmationAsync(app, DiscardTitle, string.Empty, string.Empty, DiscardText, destructive: true, cancelText: KeepEditingText);
        Shot(app, "b9c-discard-question");
        Assert.Equal(B9MovedDown.Select(row => row.Subject), app.RebaseListSnapshot().Subjects);

        // Step 6: Keep editing leaves the list as it was.
        app.Click(app.FindByAutomationId(app.TopDialogView!, "CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.True(app.IsRebaseListOpen, "Keep editing closed the list.");
        AssertList(app, B9MovedDown, L0BaseTop, selected: "Five", canStart: true, startToolTip: null);

        // Step 7: Cancel asks the same question; Discard closes the list.
        app.ClickRebaseCancel();
        await AssertConfirmationAsync(app, DiscardTitle, string.Empty, string.Empty, DiscardText, destructive: true, cancelText: KeepEditingText);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await WaitForListClosedAsync(app);
        await AssertNoDialogAsync(app);
        await WaitForGraphFocusAsync(app);
        await AssertDetailsAsync(app, FiveDetails);
        var discarded = Shot(app, "b9d-discarded");
        AssertGraph(app, GI, Lanes, showsDate: true, picture: discarded);
        AssertSelectedRows(app, discarded, 0);
        AssertGraphSelection(app, FiveSha);
        await AssertGitUnchangedAsync(repo);
        AssertNoRebaseCall(app);

        // Step 8: the list opened again and closed by Esc at once: nothing was changed, so no question.
        var pointer = GraphRowPoint(6);
        app.RightClick(pointer);
        AssertBaseCommitMenu(app, app.Menu(), pointer);
        app.ClickMenuItem(InteractiveFromHereItem);
        await app.WaitForIdleAsync();
        await app.WaitForRebaseListAsync();
        await WaitForListFocusAsync(app, "Five");
        AssertList(app, L0, L0BaseTop, selected: "Five", canStart: false, startToolTip: UnchangedReason);
        app.PressKey(Key.Escape);
        await WaitForListClosedAsync(app);
        await AssertNoDialogAsync(app);
        AssertGraph(app, GI, Lanes, showsDate: true);
        AssertSelectedRows(app, null, 0);
        AssertGraphSelection(app, FiveSha);
        await AssertGitUnchangedAsync(repo);
        AssertNoRebaseCall(app);
    }

    /// <summary>Check B9 part B (steps 9 to 11): a commit made outside the app while the list is open disables Start; Discard closes the list on G-B9.</summary>
    [AvaloniaFact]
    public async Task Check_B9_head_moved_while_open()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, GI, Lanes, showsDate: true);

        // Step 9: the list, and D on Five.
        await OpenListAsync(app);
        app.PressKey(Key.D);
        await app.WaitForAsync(() => app.RebaseListSnapshot().Row("Five").Action == VisualCommit.Core.Git.RebaseAction.Drop, "Five to be dropped");
        AssertList(app, B9Dropped, L0BaseTop, selected: "Five", canStart: true, startToolTip: null);

        // Step 10: "Six" committed outside the app; Start rebase is disabled, the list unchanged.
        var six = await repo.CommitFileAsync(SixPath, SixContent, SixSubject);
        Assert.Equal(SixSha, six);
        Assert.Equal(SixSha, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal(SixSubject + "\n", await RawMessageAsync(repo, SixSha));
        Assert.Equal((ScenarioTime(8), ScenarioTime(8)), await DatesAsync(repo, SixSha));
        Assert.Equal([FiveSha], await ParentsAsync(repo, SixSha));
        await app.WaitForAsync(() => !app.RebaseListSnapshot().CanStart, "Start rebase to be disabled after HEAD moved", OutsideChangeTimeout);
        AssertList(app, B9Dropped, L0BaseTop, selected: "Five", canStart: false, startToolTip: HeadMovedReason);
        var tip = await RestOnAsync(app, app.FindByAutomationId("RebaseStartButton"));
        Assert.Equal(HeadMovedReason, tip);
        AssertToolTipInsideWindow(app, HeadMovedReason);
        Shot(app, "b9e-head-moved", keepPointer: true);
        await AssertNoDialogAsync(app);

        // Step 11: Esc asks; Discard closes the list on G-B9, Five still selected.
        app.PressKey(Key.Escape);
        await AssertConfirmationAsync(app, DiscardTitle, string.Empty, string.Empty, DiscardText, destructive: true, cancelText: KeepEditingText);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await WaitForListClosedAsync(app);
        await AssertNoDialogAsync(app);
        await app.WaitForAsync(
            () => Graph(app).Data is { IsComplete: true } graph && graph.Count == GB9.Count && graph.CommitAt(0).Sha == SixSha,
            "the graph G-B9",
            OutsideChangeTimeout);
        await AssertDetailsAsync(app, FiveDetails);
        var discarded = Shot(app, "b9f-discarded-after-head-moved");
        AssertGraph(app, GB9, Lanes, showsDate: true, picture: discarded);
        AssertSelectedRows(app, discarded, 1);
        AssertGraphSelection(app, FiveSha);
        await AssertGitUnchangedAsync(repo, tip: SixSha);
        AssertNoRebaseCall(app);
    }
}
