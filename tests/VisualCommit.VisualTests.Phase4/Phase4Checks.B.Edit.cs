// Check B7 of part B (docs/test-reports/phase-4.md): editing the list (a drag, a fixup, a squash
// with an edited message, a reword cancelled and then made by its key, a drop by its key) and
// Start rebase: the history, the messages, the dates and the backup. Part of Phase4ChecksB; see
// Phase4Checks.B.cs for the class.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartB;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksB
{
    /// <summary>Check B7 (steps 1 to 10): the list edited to L7 and started; G-B7, the messages byte for byte, the backup.</summary>
    [AvaloniaFact]
    public async Task Check_B7_edit_list_and_start()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertGraph(app, GI, Lanes, showsDate: true);
        var palette = PaletteOf(app);

        // Step 1: the list as B6 steps 1 and 2 open it: L0, Five selected.
        await OpenListAsync(app);
        var callMark = app.GitCalls().Count;
        var logMark = app.LogMark();

        // Step 2: the keys select Fix one; a drag of it held over the gap between Two and One.
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        await WaitForListFocusAsync(app, "Fix one");
        await AssertDetailsAsync(app, FixOneDetails);
        AssertList(app, L0, L0BaseTop, selected: "Fix one", canStart: false, startToolTip: UnchangedReason);
        var from = app.RebaseRowPoint("Fix one");
        Assert.Equal(L0[3].Point, from);
        Assert.True((B7DragTo.Y - from.Y) / DragSteps > InteractiveRebaseThreshold, "The drag's first step does not pass the 4-px threshold.");
        app.PressMouse(from);
        try
        {
            app.MoveMouseHeld(B7DragTo, steps: DragSteps);
            var drag = Shot(app, "b7a-drag-fix-one", keepPointer: true);
            var held = app.RebaseListSnapshot();
            Assert.True(app.RebaseListView.IsDragging, "No row drag runs.");
            Assert.Equal(B7DragGap, held.DragGap);
            AssertDragLine(held, B7DragGapY);
            AssertColour(palette.DragTarget, drag, new Point(690, B7DragGapY - 1), "the drag line");
            AssertColour(palette.DragTarget, drag, new Point(690, B7DragGapY), "the drag line");
            AssertList(app, L0, L0BaseTop, selected: "Fix one", canStart: false, startToolTip: UnchangedReason, dragged: "Fix one");
            await AssertNoMenuAsync(app, logMark);
            Assert.Null(app.TopDialogView);
        }
        finally
        {
            // Step 3: the release drops Fix one right above One.
            app.ReleaseMouse(B7DragTo);
        }

        var moved = AssertList(app, B7Moved, L0BaseTop, selected: "Fix one", canStart: true, startToolTip: null);
        Assert.Null(moved.DragLine);
        Assert.Null(moved.DragGap);
        await AssertDetailsAsync(app, FixOneDetails);
        Shot(app, "b7b-fix-one-moved");
        await AssertGitUnchangedAsync(repo);

        // Step 4: Fix one's menu from its action button; Fixup joins it to One.
        var corner = app.RebaseActionButtonCorner("Fix one");
        app.ClickRebaseActionButton("Fix one");
        var fixOneMenu = app.Menu();
        Shot(app, "b7c-fix-one-menu");
        AssertRowMenu(app, fixOneMenu, RowMenu("ed58902 Fix one", RebaseAction.Pick, bottomRow: false), corner);
        Assert.All(fixOneMenu.Items, item => Assert.True(item.IsEnabled, $"\"{item.Label}\" is disabled."));
        app.ClickMenuItem("Fixup");
        await AssertNoMenuAsync(app, logMark: null);
        var fixup = AssertList(app, B7Fixup, L0BaseTop, selected: "Fix one", canStart: true, startToolTip: null);
        Assert.Equal(JoinLine(244, 274), Assert.Single(fixup.JoinLines).Bounds);

        // Step 5: Four right-clicked at its point; Squash joins it to Three.
        var fourPoint = app.RebaseRowPoint("Four");
        Assert.Equal(B7Fixup[1].Point, fourPoint);
        app.ClickRebaseRow("Four");
        app.RightClick(fourPoint);
        var fourMenu = app.Menu();
        AssertMenu(fourMenu, [.. RowMenu("eef5c15 Four", RebaseAction.Pick, bottomRow: false)]);
        AssertHighlighted(fourMenu, RowMenuHighlighted);
        AssertMenuShape(app, fourMenu);
        AssertMenuAtPointer(fourMenu, fourPoint);
        app.ClickMenuItem("Squash");
        await AssertNoMenuAsync(app, logMark: null);
        await AssertDetailsAsync(app, FourDetails);
        var squash = AssertList(app, B7Squash, B7SquashBaseTop, selected: "Four", canStart: true, startToolTip: null);
        Assert.Equal([JoinLine(154, 184), JoinLine(266, 296)], squash.JoinLines.Select(line => line.Bounds));

        // Step 6: Edit… on Three's second line: the group's message as git would write it.
        app.ClickRebaseEditMessage("Three");
        await AssertMessageDialogAsync(app, SquashedMessageTitle, SquashSummary, SquashDescription, SaveText);
        var squashDialog = Shot(app, "b7d-squash-message");
        AssertOverBackdrop(app);
        AssertPrimaryConfirm(app, squashDialog);

        // Step 7: the summary emptied (Save disabled), then rewritten; the description keeps the "#" line.
        SelectAll(app);
        app.PressKey(Key.Delete);
        await AssertMessageDialogAsync(app, SquashedMessageTitle, string.Empty, SquashDescription, SaveText, confirmEnabled: false, confirmToolTip: WriteSummaryReason);
        app.Type(EditedSummary);
        var description = app.FindByAutomationId(app.TopDialogView!, "MessageDescription");
        app.Click(description);
        await app.WaitForAsync(() => app.MessageDialog().DescriptionHasFocus, "the description to have the keyboard focus");
        SelectAll(app);
        app.Type(EditedDescription);
        await AssertMessageDialogAsync(app, SquashedMessageTitle, EditedSummary, EditedDescription, SaveText, summaryFocused: false);
        Shot(app, "b7e-squash-message-edited");
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await AssertNoDialogAsync(app);
        AssertList(app, B7Edited, B7SquashBaseTop, selected: "Four", canStart: true, startToolTip: null);

        // Step 8: Reword… from Two's menu, Esc in the dialog; then R and Enter.
        Assert.Equal(new Point(RowPointX, 251), app.RebaseRowPoint("Two"));
        app.ClickRebaseRow("Two");
        await AssertDetailsAsync(app, TwoDetails);
        var twoCorner = app.RebaseActionButtonCorner("Two");
        app.ClickRebaseActionButton("Two");
        var twoMenu = app.Menu();
        AssertRowMenu(app, twoMenu, RowMenu("938844a Two", RebaseAction.Pick, bottomRow: false), twoCorner);
        app.ClickMenuItem("Reword…");
        await AssertMessageDialogAsync(app, RewordTitle(TwoSha), RewordSummary, string.Empty, SaveText);
        var reword = Shot(app, "b7f-reword-dialog");
        AssertOverBackdrop(app);
        AssertPrimaryConfirm(app, reword);
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.True(app.IsRebaseListOpen, "Esc in the dialog closed the list.");
        AssertList(app, B7Edited, B7SquashBaseTop, selected: "Two", canStart: true, startToolTip: null);

        app.ClickRebaseRow("Two");
        app.PressKey(Key.R);
        await AssertMessageDialogAsync(app, RewordTitle(TwoSha), RewordSummary, string.Empty, SaveText);
        SelectAll(app);
        app.Type(RewordedSummary);
        app.PressKey(Key.Enter);
        await AssertNoDialogAsync(app);
        await app.WaitForAsync(() => app.RebaseListSnapshot().Row("Two").Action == RebaseAction.Reword, "Two to be reworded");
        var reworded = AssertList(app, B7Reworded, L7BaseTop, selected: "Two", canStart: true, startToolTip: null);
        Assert.Equal(JoinLine(288, 318), reworded.JoinLines.Single(line => line.MemberSha == FixOneSha).Bounds);

        // Step 9: D drops Five: the list is L7. No write call has run.
        app.ClickRebaseRow("Five");
        app.PressKey(Key.D);
        await app.WaitForAsync(() => app.RebaseListSnapshot().Row("Five").Action == RebaseAction.Drop, "Five to be dropped");
        await AssertDetailsAsync(app, FiveDetails);
        var edited = Shot(app, "b7g-list-edited");
        var l7 = AssertList(app, L7, L7BaseTop, selected: "Five", canStart: true, startToolTip: null);
        AssertColour(palette.Lane(0), edited, new Point(JoinLineX, 178), "Four's join line");
        AssertColour(palette.Lane(0), edited, new Point(JoinLineX + 1, 310), "Fix one's join line");
        AssertColour(palette.Accent, edited, new Point(l7.StartBounds.Left + 3, l7.StartBounds.Center.Y), "Start rebase's background (primary)");
        AssertColour(palette.Selection, edited, new Point(690, L7[0].Top + 2), "Five's selection background");
        AssertNoClippedText(app);
        await AssertGitUnchangedAsync(repo);
        AssertNoWriteSince(app, callMark, logMark);
        app.ClickRebaseRow("Three");
        await AssertDetailsAsync(app, ThreeDetails);
        AssertList(app, L7, L7BaseTop, selected: "Three", canStart: true, startToolTip: null);

        // Step 10: Start rebase.
        var startLog = app.LogMark();
        var startCalls = app.GitCalls().Count;
        using var statuses = app.RecordStatusTexts();
        app.ClickRebaseStart();
        await WaitForOperationAsync(app, repo, B7Title, startLog);
        Assert.Equal(1, app.CountLogLines(AppLogFile.OperationEnded(Repository, B7Title, OperationOutcome.Succeeded, B7Status), startLog));
        await WaitForListClosedAsync(app);
        await WaitForGraphFocusAsync(app);
        await AssertDetailsAsync(app, B7TipDetails);
        var rebased = Shot(app, "b7h-rebased");
        await AssertNoDialogAsync(app);
        Assert.Null(app.BannerBounds);
        AssertGraph(app, GB7, Lanes, showsDate: true, picture: rebased);
        AssertSelectedRows(app, rebased, 0);
        AssertGraphSelection(app, B7ThreeSha);
        AssertStatus(app, B7Status);
        Assert.Contains(B7Running, statuses.Texts);
        AssertNoClippedText(app);

        // Git: one git rebase -i from Base, and the commits the report derived.
        var rebaseCalls = app.GitCalls().Skip(startCalls).Where(call => call.CommandText.StartsWith("git rebase", StringComparison.Ordinal)).ToList();
        var call = Assert.Single(rebaseCalls);
        Assert.Equal((RebaseCall(BaseSha), 0), (call.CommandText, call.ExitCode ?? -1));
        Assert.Equal([B7ThreeSha, B7TwoSha, B7OneSha, BaseSha, InitialSha], await GitLinesAsync(repo, "log", "--format=%H", "main"));
        Assert.Equal(B7ThreeMessage, await RawMessageAsync(repo, B7ThreeSha));
        Assert.Equal(B7TwoMessage, await RawMessageAsync(repo, B7TwoSha));
        Assert.Equal(B7OneMessage, await RawMessageAsync(repo, B7OneSha));
        Assert.Equal((ScenarioTime(5), AppCommitTime), await DatesAsync(repo, B7ThreeSha));
        Assert.Equal((ScenarioTime(3), AppCommitTime), await DatesAsync(repo, B7TwoSha));
        Assert.Equal((ScenarioTime(2), AppCommitTime), await DatesAsync(repo, B7OneSha));
        foreach (var sha in new[] { B7ThreeSha, B7TwoSha, B7OneSha })
        {
            Assert.Equal(Author + " / " + Author, await PeopleAsync(repo, sha));
        }

        Assert.Equal([B7TwoSha], await ParentsAsync(repo, B7ThreeSha));
        Assert.Equal([B7OneSha], await ParentsAsync(repo, B7TwoSha));
        Assert.Equal([BaseSha], await ParentsAsync(repo, B7OneSha));
        Assert.Equal(["A\tsrc/four.txt", "A\tsrc/three.txt"], await ChangesAsync(repo, B7ThreeSha));
        Assert.Equal(["A\tsrc/two.txt"], await ChangesAsync(repo, B7TwoSha));
        Assert.Equal(["A\tsrc/one.txt"], await ChangesAsync(repo, B7OneSha));
        Assert.Equal(Scenarios.InteractiveFiles.OneFixed, await FileAtAsync(repo, B7OneSha, "src/one.txt"));
        Assert.Empty(await GitLinesAsync(repo, "log", "--format=%H", "main", "--", "src/five.txt"));
        Assert.Equal("ref: refs/heads/main", File.ReadAllText(Path.Combine(repo.Path, ".git", "HEAD")).TrimEnd('\r', '\n'));
        Assert.Equal([CleanStatus], await StatusAsync(repo));
        AssertNoRebaseFolders(repo);
        Assert.Empty(await GitLinesAsync(repo, "for-each-ref", "refs/rewritten/"));
        await AssertRebaseBackupAsync(repo, FiveSha);
    }

    /// <summary>The 4-px threshold a row drag passes before it starts (D87, design 2.2).</summary>
    private const double InteractiveRebaseThreshold = 4;

    /// <summary>The drag's 2-px line lies on the gap at window y <paramref name="gapY"/>, across the list.</summary>
    private static void AssertDragLine(VisualCommit.App.Views.Rebase.InteractiveRebaseSnapshot list, double gapY)
    {
        var line = list.DragLine ?? throw new Xunit.Sdk.XunitException("No drag line is drawn.");
        Assert.Equal(DragLineHeight, line.Height, 0.5);
        Assert.Equal(gapY, line.Center.Y, 0.5);
        Assert.Equal((ListLeft, ListWidth), (line.Left, line.Width));
    }
}
