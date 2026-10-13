// Part B's real-window steps (docs/test-reports/phase-4.md, part B, the "Real window" column of B6
// to B9): B6 steps 1, 2, 5, 6 and 7, and the whole of B7, each in a launch of its own on a fresh
// "interactive" copy at 1100×700, dark. B8 and B9 are "n/a" (the report gives the reasons). The
// list's rows are found by their RebaseRow names ({short} {subject}) and clicked at their points
// (x 600, the row's vertical centre: RealApp.RebaseRowPoint), the action and Edit… buttons by their
// automation ids, menu items by their ContextMenuItem names; the dialogs are typed with the real
// keyboard.
using System.Drawing;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartB;

namespace VisualCommit.RealWindowTests;

public partial class Phase4RealWindowPass
{
    /// <summary>
    /// Check B6, steps 1, 2, 5, 6 and 7, with the real mouse in one launch: Base's commit menu,
    /// right-clicked at the constants' point (<c>b6a-commit-menu</c>); the list it opens, L0 with
    /// Five selected (<c>b6b-list</c>); Three's row clicked at (600, 199), as the scripted step 4
    /// ends, and its action button's menu (<c>b6e-action-menu</c>), Esc (Shift+F10 is scripted only);
    /// One's row clicked and right-clicked, the pointer resting on the disabled Squash until its
    /// tooltip shows inside the window (<c>b6f-squash-disabled</c>), Esc; Cancel on the unchanged
    /// list (<c>b6g-list-closed</c>). Steps 3 and 4 (the keys, the base row, the diff over the list)
    /// are the scripted run's alone; the details panel's file list of <c>b6e</c> may differ there by
    /// the row three.txt that step 4 selects in the scripted run.
    /// </summary>
    [Fact]
    public async Task Check_B6_interactive_list()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var app = Launch(repo);
        var name = NameOf(repo);
        Assert.Equal(Repository, name);

        // Step 1: Base's commit menu, right-clicked in its Message column at (468, 285).
        var pointer = new PointF((float)GraphRowPoint(6).X, (float)GraphRowPoint(6).Y);
        AssertNear(pointer, RowPoint(app, 6, Lanes), "Base's row point");
        var mark = app.LogMark();
        app.RightClickAt(pointer.X, pointer.Y);
        var menu = AssertMenu(app, BaseCommitMenu, BaseCommitMenuHeight);
        AssertNear(new PointF(pointer.X + 2, pointer.Y + 2), menu.Location, "Base's commit menu's top-left");
        WaitForDetails(app, "Base");
        AssertFocusedItem(app, BaseCommitMenuHighlighted);
        Assert.Single(app.LogLinesSince(mark), line => RealApp.MenuOpened(name).IsMatch(line));
        Assert.Single(app.LogLinesSince(mark), line => RealApp.MenuOpened(name, "4b4622c Base", "pointer").IsMatch(line));
        Compare(app, "b6a-commit-menu", keepPointer: true);

        // Step 2: the list L0 in the graph area; Five selected, with the keyboard focus.
        app.ClickMenuItem(InteractiveFromHereItem);
        app.WaitForRebaseList();
        app.WaitForRebaseRowOrder([.. L0.Select(row => row.Name)]);
        app.WaitForRebaseRowFocus(L0[0].Name);
        WaitForDetails(app, "Five");
        Assert.Empty(app.FindAll("ContextMenu"));
        AssertNoDialog(app);
        AssertListFrame(app, canStart: false);
        Compare(app, "b6b-list");

        // Step 5 (the scripted step 4 ends with this click): Three's row at (600, 199), then its
        // action button: the menu at the button's bottom-left corner.
        var three = L0[2];
        AssertNear(ToPointF(three.Point), app.RebaseRowPoint(three.Name), "Three's point");
        app.ClickRebaseRow(three.Name);
        app.WaitForRebaseRowFocus(three.Name);
        WaitForDetails(app, "Three");
        var corner = app.RebaseActionButtonCorner(three.Name);
        mark = app.LogMark();
        app.ClickRebaseActionButton(three.Name);
        menu = AssertMenu(app, RowMenu(three.Name, RebaseAction.Pick, bottomRow: false), RowMenuHeight);
        AssertNear(corner, menu.Location, "Three's menu's top-left");
        AssertFocusedItem(app, RowMenuHighlighted);
        Assert.Single(app.LogLinesSince(mark), line => RealApp.MenuOpened(name, three.Name, "pointer").IsMatch(line));
        Compare(app, "b6e-action-menu", keepPointer: true);
        CloseMenuWithEscape(app);
        app.WaitForRebaseRowFocus(three.Name);
        Assert.Equal(ActionName(RebaseAction.Pick), app.RebaseActionButton(three.Name).Name);

        // Step 6: One's row clicked and right-clicked at its point; the pointer rests on the
        // disabled Squash by phase 3's path (outside the menu, then sideways).
        var one = L0[5];
        AssertNear(ToPointF(one.Point), app.RebaseRowPoint(one.Name), "One's point");
        app.ClickRebaseRow(one.Name);
        app.WaitForRebaseRowFocus(one.Name);
        WaitForDetails(app, "One");
        var onePoint = app.RebaseRowPoint(one.Name);
        app.RightClickRebaseRow(one.Name);
        menu = AssertMenu(app, RowMenu(one.Name, RebaseAction.Pick, bottomRow: true), RowMenuHeight);
        AssertNear(new PointF(onePoint.X + 2, onePoint.Y + 2), menu.Location, "One's menu's top-left");
        AssertFocusedItem(app, RowMenuHighlighted);
        var tip = app.RestPointerOnMenuItem(ActionName(RebaseAction.Squash), NothingBelowReason);
        AssertToolTipInside(app, tip, NothingBelowReason);
        AssertFocusedItem(app, RowMenuHighlighted);
        Compare(app, "b6f-squash-disabled", keepPointer: true);
        CloseMenuWithEscape(app);
        Assert.Equal(ActionName(RebaseAction.Pick), app.RebaseActionButton(one.Name).Name);

        // Step 7: Cancel on the unchanged list closes it at once; One stays selected in the graph.
        app.ClickRebaseCancel();
        app.WaitForRebaseListClosed();
        AssertNoDialog(app);
        WaitForDetails(app, "One");
        Compare(app, "b6g-list-closed");

        // Git unchanged; no rebase ran (the scripted run reads the call log; this pass the queue's log).
        await AssertInteractiveUnchangedAsync(repo);
        Assert.DoesNotContain(app.LogLines(), line => line.Contains($"] {name}: {B7Title}: ", StringComparison.Ordinal));
        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Check B7, the whole check in one launch (Start needs every edit), with the real mouse and
    /// keyboard: Fix one dragged with the button held over the gap between Two and One
    /// (<c>b7a-drag-fix-one</c>) and dropped (<c>b7b-fix-one-moved</c>); Fixup from its action
    /// button's menu; Squash on Four from its row's menu; Edit… on Three's second line and the
    /// group's message typed; Reword… on Two, its dialog (<c>b7f-reword-dialog</c>), Esc, then R and
    /// Enter; D on Five; Start rebase, the queue's log line awaited, the graph after
    /// (<c>b7h-rebased</c>); git's state asserted as in the scripted run (the same SHAs:
    /// RealApp.Launch gives the app the same commit dates, D72).
    /// </summary>
    [Fact]
    public async Task Check_B7_edit_list_and_start()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var app = Launch(repo);
        var name = NameOf(repo);

        // Step 1: the list as B6 steps 1 and 2 open it.
        var basePoint = RowPoint(app, 6, Lanes);
        app.RightClickAt(basePoint.X, basePoint.Y);
        AssertMenu(app, BaseCommitMenu, BaseCommitMenuHeight);
        app.ClickMenuItem(InteractiveFromHereItem);
        app.WaitForRebaseList();
        app.WaitForRebaseRowOrder([.. L0.Select(row => row.Name)]);
        app.WaitForRebaseRowFocus(L0[0].Name);

        // Step 2: Down three times selects Fix one; the press at its point and 5 moves with the
        // button held to (600, 270): the drag line marks the gap between Two and One.
        var fixOne = L0[3];
        for (var press = 0; press < 3; press++)
        {
            app.PressKey(VirtualKeyShort.DOWN);
        }

        app.WaitForRebaseRowFocus(fixOne.Name);
        WaitForDetails(app, "Fix one");
        var from = app.RebaseRowPoint(fixOne.Name);
        AssertNear(ToPointF(fixOne.Point), from, "Fix one's point");
        var to = ToPointF(B7DragTo);
        app.MouseDownAt(from.X, from.Y);
        var held = true;
        try
        {
            app.MoveHeldTo(to.X, to.Y, steps: DragSteps);
            Assert.Empty(app.FindAll("ContextMenu"));
            AssertNoDialog(app);
            app.WaitForRebaseRowOrder([.. L0.Select(row => row.Name)]);
            Compare(app, "b7a-drag-fix-one", keepPointer: true);

            // Step 3: the release drops Fix one right above One.
            held = false;
            app.MouseUpAt(to.X, to.Y);
        }
        finally
        {
            if (held)
            {
                app.MouseUpAt(to.X, to.Y);
            }
        }

        app.WaitForRebaseRowOrder([.. B7Moved.Select(row => row.Name)]);
        app.WaitForRebaseRowFocus(fixOne.Name);
        app.WaitFor(() => app.RebaseStartButton().IsEnabled, "Start rebase to be enabled (the order changed)", RealApp.DefaultWaitTimeout);
        WaitForDetails(app, "Fix one");
        Compare(app, "b7b-fix-one-moved");
        await AssertInteractiveUnchangedAsync(repo);

        // Step 4: Fix one's action button; Fixup joins it to One.
        var corner = app.RebaseActionButtonCorner(fixOne.Name);
        app.ClickRebaseActionButton(fixOne.Name);
        var menu = AssertMenu(app, RowMenu(fixOne.Name, RebaseAction.Pick, bottomRow: false), RowMenuHeight);
        AssertNear(corner, menu.Location, "Fix one's menu's top-left");
        AssertFocusedItem(app, RowMenuHighlighted);
        app.ClickMenuItem(ActionName(RebaseAction.Fixup));
        app.WaitUntilGone("ContextMenu");
        app.WaitForRebaseAction(fixOne.Name, ActionName(RebaseAction.Fixup));

        // Step 5: Four clicked and right-clicked at its point; Squash joins it to Three.
        var four = B7Fixup[1];
        var fourPoint = app.RebaseRowPoint(four.Name);
        AssertNear(ToPointF(four.Point), fourPoint, "Four's point");
        app.ClickRebaseRow(four.Name);
        app.WaitForRebaseRowFocus(four.Name);
        app.RightClickRebaseRow(four.Name);
        menu = AssertMenu(app, RowMenu(four.Name, RebaseAction.Pick, bottomRow: false), RowMenuHeight);
        AssertNear(new PointF(fourPoint.X + 2, fourPoint.Y + 2), menu.Location, "Four's menu's top-left");
        app.ClickMenuItem(ActionName(RebaseAction.Squash));
        app.WaitUntilGone("ContextMenu");
        app.WaitForRebaseAction(four.Name, ActionName(RebaseAction.Squash));
        var three = B7Squash[2];
        app.WaitForRebaseSecondLine(three.Name, MessageLine("Three"));
        WaitForDetails(app, "Four");

        // Step 6: Edit… on Three's second line: the group's message as git would write it.
        app.ClickRebaseEditMessage(three.Name);
        WaitForMessageDialog(app, SquashedMessageTitle, SquashSummary, SquashDescription, SaveText, summaryFocused: true);

        // Step 7: the summary emptied (Save disabled), then rewritten; the description keeps the "#" line.
        app.PressSelectAll();
        app.PressKey(VirtualKeyShort.DELETE);
        WaitForMessageDialog(app, SquashedMessageTitle, string.Empty, SquashDescription, SaveText, summaryFocused: true, saveReason: WriteSummaryReason);
        app.TypeText(EditedSummary);
        app.ClickWithMouse(app.Find("MessageDescription"));
        app.PressSelectAll();
        app.TypeText(EditedDescription);
        WaitForMessageDialog(app, SquashedMessageTitle, EditedSummary, EditedDescription, SaveText, summaryFocused: false);
        app.ClickWithMouse(app.Find("ConfirmButton"));
        app.WaitUntilGone("MessageDialog");
        app.WaitForRebaseSecondLine(three.Name, MessageLine(EditedSummary));

        // Step 8: Two clicked, its action button, Reword…: the dialog; Esc; then R and Enter.
        var two = B7Edited[3];
        AssertNear(ToPointF(two.Point), app.RebaseRowPoint(two.Name), "Two's point");
        app.ClickRebaseRow(two.Name);
        app.WaitForRebaseRowFocus(two.Name);
        WaitForDetails(app, "Two");
        corner = app.RebaseActionButtonCorner(two.Name);
        app.ClickRebaseActionButton(two.Name);
        menu = AssertMenu(app, RowMenu(two.Name, RebaseAction.Pick, bottomRow: false), RowMenuHeight);
        AssertNear(corner, menu.Location, "Two's menu's top-left");
        app.ClickMenuItem(MenuLabel(RebaseAction.Reword));
        WaitForMessageDialog(app, RewordTitle(TwoSha), RewordSummary, string.Empty, SaveText, summaryFocused: true);
        Compare(app, "b7f-reword-dialog");
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitUntilGone("MessageDialog");
        Assert.True(app.IsRebaseListShown, "Esc in the dialog closed the list.");
        Assert.Equal(ActionName(RebaseAction.Pick), app.RebaseActionButton(two.Name).Name);
        Assert.Null(app.RebaseSecondLine(two.Name));

        app.ClickRebaseRow(two.Name);
        app.WaitForRebaseRowFocus(two.Name);
        app.PressKey(VirtualKeyShort.KEY_R);
        WaitForMessageDialog(app, RewordTitle(TwoSha), RewordSummary, string.Empty, SaveText, summaryFocused: true);
        app.PressSelectAll();
        app.TypeText(RewordedSummary);
        app.PressKey(VirtualKeyShort.RETURN);
        app.WaitUntilGone("MessageDialog");
        app.WaitForRebaseAction(two.Name, ActionName(RebaseAction.Reword));
        app.WaitForRebaseSecondLine(two.Name, NewMessageLine(RewordedSummary));

        // Step 9: D drops Five: the list is L7; git is unchanged. Then Three is clicked.
        var five = L7[0];
        AssertNear(ToPointF(five.Point), app.RebaseRowPoint(five.Name), "Five's point");
        app.ClickRebaseRow(five.Name);
        app.WaitForRebaseRowFocus(five.Name);
        app.PressKey(VirtualKeyShort.KEY_D);
        app.WaitForRebaseAction(five.Name, ActionName(RebaseAction.Drop));
        WaitForDetails(app, "Five");
        app.WaitForRebaseRowOrder([.. L7.Select(row => row.Name)]);
        Assert.True(app.RebaseStartButton().IsEnabled, "Start rebase is disabled on the edited list.");
        await AssertInteractiveUnchangedAsync(repo);
        app.ClickRebaseRow(three.Name);
        app.WaitForRebaseRowFocus(three.Name);
        WaitForDetails(app, "Three");

        // Step 10: Start rebase.
        var start = ExpectOperation(app, name, B7Title);
        app.ClickRebaseStart();
        start.Wait(OperationOutcome.Succeeded, B7Status);
        app.WaitForRebaseListClosed(OperationTimeout);
        WaitForDetails(app, "Three and four", B7ThreeSha);
        WaitForStatus(app, B7Status);
        AssertNoDialog(app);
        Assert.Null(app.BannerBounds());
        Compare(app, "b7h-rebased");

        // Git: the commits the report derived, their messages byte for byte, the backup.
        Assert.Equal([B7ThreeSha, B7TwoSha, B7OneSha, BaseSha, InitialSha], await GitLinesAsync(repo, "log", "--format=%H", "main"));
        Assert.Equal(B7ThreeMessage, await RawMessageAsync(repo, B7ThreeSha));
        Assert.Equal(B7TwoMessage, await RawMessageAsync(repo, B7TwoSha));
        Assert.Equal(B7OneMessage, await RawMessageAsync(repo, B7OneSha));
        await AssertCommitAsync(repo, B7ThreeSha, ScenarioTime(5), AppCommitTime, B7TwoSha);
        await AssertCommitAsync(repo, B7TwoSha, ScenarioTime(3), AppCommitTime, B7OneSha);
        await AssertCommitAsync(repo, B7OneSha, ScenarioTime(2), AppCommitTime, BaseSha);
        Assert.Equal(["A\tsrc/four.txt", "A\tsrc/three.txt"], await GitLinesAsync(repo, "diff-tree", "--no-commit-id", "--name-status", "-r", B7ThreeSha + "^", B7ThreeSha));
        Assert.Equal(["A\tsrc/two.txt"], await GitLinesAsync(repo, "diff-tree", "--no-commit-id", "--name-status", "-r", B7TwoSha + "^", B7TwoSha));
        Assert.Equal(["A\tsrc/one.txt"], await GitLinesAsync(repo, "diff-tree", "--no-commit-id", "--name-status", "-r", B7OneSha + "^", B7OneSha));
        Assert.Equal(Scenarios.InteractiveFiles.OneFixed, (await repo.GitAsync("show", B7OneSha + ":src/one.txt")).StandardOutput);
        Assert.Empty(await GitLinesAsync(repo, "log", "--format=%H", "main", "--", "src/five.txt"));
        Assert.Equal("ref: refs/heads/main", File.ReadAllText(Path.Combine(repo.Path, ".git", "HEAD")).TrimEnd('\r', '\n'));
        Assert.Equal([CleanStatus], await GitLinesAsync(repo, "status", "--short", "--branch"));
        AssertNoRebaseFolder(repo);
        Assert.Empty(await GitLinesAsync(repo, "for-each-ref", "refs/rewritten/"));
        var backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal(("rebase", (int?)null, false, "main"), (backup.Ref.Kind, backup.Ref.Number, backup.Ref.IsChanges, backup.Ref.Name));
        Assert.Equal(FiveSha, backup.Sha);
        Assert.Single(await GitLinesAsync(repo, "for-each-ref", "refs/visualcommit/"));

        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// The list's frame as B6 step 2 gives it: the header and the count, the base row, Cancel
    /// enabled and Start rebase enabled or not (disabled with <c>Change an action or the order
    /// first</c> as its tooltip on an unchanged list), every row Pick.
    /// </summary>
    private static void AssertListFrame(RealApp app, bool canStart)
    {
        Assert.Equal(ListHeader, app.RebaseListHeaderText());
        Assert.Equal(ListCount, app.RebaseListCountText());
        Assert.Equal($"{BaseLabel} {Short(BaseSha)} Base", app.RebaseBaseRow().Name);
        var start = app.RebaseStartButton();
        Assert.Equal(StartText, start.Name);
        Assert.Equal(canStart, start.IsEnabled);
        if (!canStart)
        {
            Assert.Equal(UnchangedReason, start.HelpText);
        }

        var cancel = app.RebaseCancelButton();
        Assert.Equal(CancelText, cancel.Name);
        Assert.True(cancel.IsEnabled, "Cancel is disabled.");
        Assert.All(app.RebaseRows(), row => Assert.Equal(ActionName(RebaseAction.Pick), app.RebaseActionButton(row.Name).Name));
    }

    /// <summary>
    /// Waits for the message dialog (design 7.2) with this title and asserts it as it is then: the
    /// summary (its Value), the counter (72 less the summary's length), the description (its Value,
    /// line ends as line feeds), the confirm button's label, enabled, or disabled with
    /// <paramref name="saveReason"/> as its tooltip; and, with <paramref name="summaryFocused"/>,
    /// the summary with the keyboard focus. Typing is followed by a wait, not a fixed pause: the
    /// texts are polled until they are the expected ones.
    /// </summary>
    private static void WaitForMessageDialog(RealApp app, string title, string summary, string description, string confirm, bool summaryFocused, string? saveReason = null)
    {
        app.WaitForText("MessageDialogTitle", title);
        app.WaitFor(
            () => app.MessageSummaryText() == summary
                && app.MessageDescriptionText().Replace("\r\n", "\n", StringComparison.Ordinal) == description
                && app.MessageSummaryCounterText() == Counter(summary),
            $"the message dialog to hold the summary \"{summary}\" and the description \"{description.Replace("\n", "\\n", StringComparison.Ordinal)}\"",
            RealApp.DefaultWaitTimeout);
        var button = app.Find("ConfirmButton");
        Assert.Equal(confirm, button.Name);
        Assert.Equal(saveReason is null, button.IsEnabled);
        if (saveReason is not null)
        {
            Assert.Equal(saveReason, button.HelpText);
        }

        Assert.Equal(CancelText, app.Find("CancelButton").Name);
        if (summaryFocused)
        {
            app.WaitFor(() => RealApp.HasKeyboardFocus(app.Find("MessageSummary")), "the summary to have the keyboard focus", RealApp.DefaultWaitTimeout);
        }
    }

    /// <summary>
    /// "Git unchanged" (part B's conventions): refs/heads/main at Five (57f089d), <c>git status
    /// --short --branch</c> prints exactly <c>## main</c>, nothing under refs/visualcommit/, and no
    /// rebase folder.
    /// </summary>
    private static async Task AssertInteractiveUnchangedAsync(TempRepo repo)
    {
        Assert.Equal(FiveSha, await repo.RefTargetAsync("refs/heads/main"));
        Assert.Equal([CleanStatus], await GitLinesAsync(repo, "status", "--short", "--branch"));
        Assert.Empty(await GitLinesAsync(repo, "for-each-ref", "refs/visualcommit/"));
        AssertNoRebaseFolder(repo);
    }

    /// <summary>An Avalonia point of the expectations as the driver's point.</summary>
    private static PointF ToPointF(Avalonia.Point point) => new((float)point.X, (float)point.Y);
}
