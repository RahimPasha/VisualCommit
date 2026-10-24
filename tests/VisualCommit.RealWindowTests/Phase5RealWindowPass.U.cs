// Part U's real-window steps (docs/test-reports/phase-5.md, part U, the "Real window" column of U16
// to U23): U16 part A, steps 2 to 6, with the real mouse and keyboard on a fresh "history" copy at
// 1100×700, dark: the commit typed key by key, Undo's tooltip found in the window's UI Automation
// tree, and the toolbar's Undo and Redo clicked by automation id. U16's other parts and U17 to U23
// are "n/a" (the report gives the reasons: the same two buttons, earlier phases' dialogs, git states
// both harnesses read alike).
using FlaUI.Core.AutomationElements;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase5RealWindowPass
{
    /// <summary>
    /// Check U16, part A, steps 2 to 6: Stage all and a commit with its summary and description
    /// typed on the real keyboard (T+); the pointer resting on Undo until its tooltip is in the
    /// window, under the button (<c>u16a-undo-tooltip</c>); a real click on Undo (the branch back, the
    /// change staged again; T−) and the working-changes row with the message back in both boxes
    /// (<c>u16b-commit-undone</c>); "Read whole lines" clicked and a real click on Redo (the same
    /// commit, the boxes cleared; <c>u16c-commit-redone</c>). Each button's help text read after every
    /// step; git's state as in the scripted run.
    /// </summary>
    [Fact]
    public async Task Check_U16_commit_undone_and_redone_with_the_toolbar()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var app = Launch(repo);
        var name = NameOf(repo);
        var undoText = PartU.Undo.Title(PartU.CommitText);
        var redoText = PartU.Redo.Title(PartU.CommitText);

        // The buttons' tooltips and help texts name their keys since phase 6 (docs/phase-6-design.md
        // 6.8; phase 6's report, "Changes to expected results", U16): Undo's tooltip, u16a's, grows
        // by " (Ctrl+Z)". The operations keep their titles.
        var undoTip = PartU.Undo.ToolTip(PartU.CommitText);
        var redoTip = PartU.Redo.ToolTip(PartU.CommitText);

        // Step 1: H0 with its working-changes row; nothing selected; T0.
        WaitForNothingSelected(app);
        AssertUndoRedo(app, false, PartA.NothingToUndo, false, PartA.NothingToRedo);

        // Step 2: Stage all, the summary and the description typed key by key, Commit.
        ClickWorkingRow(app, 2, "Unstaged files (1)", "Staged files (0)");
        var stage = ExpectOperation(app, name, "Stage");
        app.ClickWithMouse(app.Find("StageAllButton"));
        stage.Wait(OperationOutcome.Succeeded);
        app.WaitForStageTitles("Unstaged files (0)", "Staged files (1)", StepTimeout);
        TypeInto(app, "CommitSummary", PartU.CommitSummary);
        TypeInto(app, "CommitDescription", PartU.CommitDescription);
        app.WaitForText("CommitButton", "Commit 1 file", StepTimeout);
        var commit = ExpectOperation(app, name, "Commit");
        app.ClickWithMouse(app.Find("CommitButton"));
        commit.Wait(OperationOutcome.Succeeded);
        WaitForDetails(app, PartU.CommitSummary, PartU.U16CommitSha);
        WaitForStatus(app, PartU.Ready);
        AssertUndoRedo(app, true, undoTip, false, PartA.NothingToRedo);
        Assert.Equal(PartU.U16CommitSha, await RevParseAsync(repo, "refs/heads/main"));
        await AssertCommitAsync(repo, PartU.U16CommitSha, AppCommitTime, AppCommitTime, PartU.ReadWholeLinesSha);
        Assert.Equal(PartU.CommitMessage, (await GitTextAsync(repo, "log", "-1", "--format=%B", PartU.U16CommitSha)).TrimEnd('\n'));
        Assert.Equal(["## main...origin/main [ahead 1]"], await GitLinesAsync(repo, "status", "-sb"));

        // Step 3: the pointer rests on Undo: its tooltip in the window's UI Automation tree, under the button.
        var undo = app.Find("UndoButton");
        var button = app.BoundsOf(undo);
        var tip = app.RestPointerOn(undo, undoTip);
        AssertToolTipInside(app, tip, undoTip);
        var place = app.ToolTipPlace(tip).Bounds;
        Assert.True(place.Top >= button.Bottom - 2, $"The tooltip at {place} is not under the Undo button at {button}.");
        Compare(app, "u16a-undo-tooltip", keepPointer: true);
        app.MoveToNeutral();

        // Step 4: a real click on Undo: a soft move back, the change staged again.
        var undone = ExpectOperation(app, name, undoText);
        app.ClickWithMouse(app.Find("UndoButton"));
        undone.Wait(OperationOutcome.Succeeded, PartU.Undo.DoneText(PartU.CommitText));
        WaitForStatus(app, PartU.Undo.DoneText(PartU.CommitText));
        AssertNoDialog(app);
        WaitForNothingSelected(app);
        AssertUndoRedo(app, false, PartA.NothingToUndo, true, redoTip);
        Assert.Equal(PartU.ReadWholeLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main", "M  src/Reader.cs"], await GitLinesAsync(repo, "status", "--porcelain=v1", "-b"));
        Assert.Equal([PartU.Undo.Message(PartU.CommitText)], await ReflogAsync(repo, "refs/heads/main", 1));
        Assert.Equal([PartU.Undo.Message(PartU.CommitText)], await ReflogAsync(repo, "HEAD", 1));
        Assert.Equal(PartU.U16CommitSha, await RevParseAsync(repo, "main@{1}"));

        // Step 5: the working-changes row: the change staged, the message back in both boxes.
        ClickWorkingRow(app, 2, "Unstaged files (0)", "Staged files (1)");
        app.WaitFor(() => BoxText(app, "CommitSummary") == PartU.CommitSummary, "the summary back in its box", StepTimeout);
        Assert.Equal(PartU.CommitDescription, BoxText(app, "CommitDescription"));
        Assert.Equal([PartU.CommitSummaryCounter], app.NamesOnScreen("SummaryCounter"));
        Assert.True(app.Find("AmendCheckBox").AsCheckBox().IsChecked == false, "Amend is ticked.");
        var commitButton = app.Find("CommitButton");
        Assert.Equal("Commit 1 file", commitButton.Name);
        Assert.True(commitButton.IsEnabled, "Commit is disabled.");
        Compare(app, "u16b-commit-undone");

        // Step 6: "Read whole lines" (display row 1), then a real click on Redo: the same commit, the boxes cleared.
        var readWholeLines = RowPoint(app, 1, 2);
        app.ClickAt(readWholeLines.X, readWholeLines.Y);
        WaitForDetails(app, "Read whole lines", PartU.ReadWholeLinesSha);
        var redone = ExpectOperation(app, name, redoText);
        app.ClickWithMouse(app.Find("RedoButton"));
        redone.Wait(OperationOutcome.Succeeded, PartU.Redo.DoneText(PartU.CommitText));
        WaitForStatus(app, PartU.Redo.DoneText(PartU.CommitText));
        AssertNoDialog(app);
        WaitForDetails(app, "Read whole lines", PartU.ReadWholeLinesSha);
        AssertUndoRedo(app, true, undoTip, false, PartA.NothingToRedo);
        Assert.Equal(PartU.U16CommitSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal([PartU.Redo.Message(PartU.CommitText)], await ReflogAsync(repo, "refs/heads/main", 1));
        Assert.Equal([PartU.Redo.Message(PartU.CommitText)], await ReflogAsync(repo, "HEAD", 1));
        Compare(app, "u16c-commit-redone");

        Assert.Equal(0, app.Close());
    }
}
