// Part U, check U19 (docs/test-reports/phase-5.md): undo and redo of history operations, each a
// Keep move of the branch (and a switch for the "Check out and …" ones): a merge as a fast-forward
// and with a merge commit, a cherry-pick of two commits, a revert ("ops"), a rebase, "Check out
// and rebase", "Check out and merge" ("rebase"), an interactive rebase with a drop and a reword, a
// squash ("interactive"), a pull that rebased ("sync"). One method per action, each on a fresh copy
// (design 15.4, UD17); the actions are phase 3's checks 8, 11 and 12 and phase 4's A2, A3, A4, B7
// and B8 (UD8).
using Avalonia;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase5;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5Undo;

public sealed partial class Phase5ChecksU
{
    /// <summary>Check U19, part A: "Merge feature/ff into main", which fast-forwards; undone and redone by Keep moves ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U19_merge_fast_forward()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 1: the label's "Merge feature/ff into main": phase 3's merge, a fast-forward here.
        RightClickLabel(app, 2, "feature/ff");
        var calls = await ActAsync(app, () => app.ClickMenuItem("Merge feature/ff into main"));
        AssertCalled(app, calls, PartU.MergeFfCall);
        AssertStatus(app, PartU.StatusFastForwardedMain);
        Assert.Equal(PartU.FastForwardMeSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await StatusAsync(repo));
        Assert.Equal([PartU.MergeFfReflog], await ReflogAsync(repo, "HEAD", 1));
        AssertGraph(app, PartU.G1, PartU.OpsLanes, showsDate: true);
        AssertArrows(app, 1, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.MergeFfText));

        // Step 2: Undo: the Keep move back to Main work.
        calls = await UndoAsync(app, repo, PartU.MergeFfText);
        var shot = Shot(app, "u19a-ff-undone");
        AssertKeepMove(app, calls, PartU.Undo.Message(PartU.MergeFfText), "main", PartU.FastForwardMeSha, PartU.MainWorkSha);
        Assert.Equal(PartU.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        Assert.False(Directory.Exists(Path.Combine(repo.Path, "docs")), "The docs folder is still there.");
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.MergeFfText));

        // Step 3: Redo: the Keep move to Fast-forward me again.
        calls = await RedoAsync(app, repo, PartU.MergeFfText);
        shot = Shot(app, "u19b-ff-redone");
        AssertKeepMove(app, calls, PartU.Redo.Message(PartU.MergeFfText), "main", PartU.MainWorkSha, PartU.FastForwardMeSha);
        Assert.Equal(Scenarios.OperationsFiles.FastForward, WorkingFile(repo, "docs/ff.txt"));
        AssertGraph(app, PartU.G1, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertArrows(app, 1, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.MergeFfText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U19, part B: "Merge feature/ff into main (create a merge commit)"; undone (the merge commit leaves the graph) and redone (the same commit) ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U19_merge_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 4: the merge with a merge commit.
        RightClickLabel(app, 2, "feature/ff");
        await ActAsync(app, () => app.ClickMenuItem("Merge feature/ff into main (create a merge commit)"));
        AssertStatus(app, PartU.StatusMergedFf);
        Assert.Equal(PartU.MergeCommitSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal($"{PartU.MainWorkSha} {PartU.FastForwardMeSha}", await GitTextAsync(repo, "log", "-1", "--format=%P", PartU.MergeCommitSha));
        Assert.Equal(["## main...origin/main [ahead 2]"], await StatusAsync(repo));
        AssertGraph(app, PartU.G2, 4, showsDate: false);
        AssertArrows(app, 2, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.MergeFfText));

        // Step 5: Undo: main back to Main work; the merge commit leaves the graph.
        var calls = await UndoAsync(app, repo, PartU.MergeFfText);
        var shot = Shot(app, "u19c-merge-undone");
        AssertKeepMove(app, calls, PartU.Undo.Message(PartU.MergeFfText), "main", PartU.MergeCommitSha, PartU.MainWorkSha);
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        Assert.Equal(-1, Repo(app).Graph.IndexOf(PartU.MergeCommitSha));
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.MergeFfText));

        // Step 6: Redo: the same merge commit again.
        calls = await RedoAsync(app, repo, PartU.MergeFfText);
        shot = Shot(app, "u19d-merge-redone");
        AssertKeepMove(app, calls, PartU.Redo.Message(PartU.MergeFfText), "main", PartU.MainWorkSha, PartU.MergeCommitSha);
        Assert.Equal(PartU.MergeCommitSha, await RevParseAsync(repo, "refs/heads/main"));
        AssertGraph(app, PartU.G2, 4, showsDate: false, picture: shot);
        AssertArrows(app, 2, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.MergeFfText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U19, part C: a cherry-pick of Alpha one and Alpha two; undone and redone, the two selected commits kept selected ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U19_cherry_pick_two()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 7: Alpha one, then Alpha two added; "Cherry-pick 2 commits onto main".
        ClickRow(app, 5);
        app.Click(app.GraphRowPoint(4), app.CommandModifier);
        await app.WaitForAsync(() => Repo(app).Selection.Count == 2, "Alpha one and Alpha two to be selected");
        app.RightClick(app.GraphRowPoint(4));
        app.Menu();
        var calls = await ActAsync(app, () => app.ClickMenuItem("Cherry-pick 2 commits onto main"));
        AssertCalled(app, calls, PartU.PickTwoCall);
        AssertStatus(app, PartU.StatusPickedTwo);
        Assert.Equal(PartU.PickedAlphaTwoSha, await RevParseAsync(repo, "refs/heads/main"));
        await AssertCommitAsync(repo, PartU.PickedAlphaTwoSha, "Alpha two", [PartU.PickedAlphaOneSha], PartU.TestAuthor, ScenarioTime(3), AppCommitTime);
        await AssertCommitAsync(repo, PartU.PickedAlphaOneSha, "Alpha one", [PartU.MainWorkSha], PartU.TestAuthor, ScenarioTime(2), AppCommitTime);
        Assert.Equal(["## main...origin/main [ahead 2]"], await StatusAsync(repo));
        AssertGraph(app, PartU.G8, 4, showsDate: false);
        AssertArrows(app, 2, 0, "Local branches", "main");
        AssertRowsSelected(app, [6, 7], PartU.AlphaTwoSha, PartU.AlphaOneSha);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.PickTwoText));

        // Step 8: Undo: main back to Main work, src/alpha.txt removed; rows 4 and 5 still selected.
        calls = await UndoAsync(app, repo, PartU.PickTwoText);
        var shot = Shot(app, "u19e-pick-undone");
        AssertKeepMove(app, calls, PartU.Undo.Message(PartU.PickTwoText), "main", PartU.PickedAlphaTwoSha, PartU.MainWorkSha);
        Assert.Null(WorkingFile(repo, "src/alpha.txt"));
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertRowsSelected(app, [4, 5], PartU.AlphaTwoSha, PartU.AlphaOneSha);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.PickTwoText));

        // Step 9: Redo: the two picks again.
        calls = await RedoAsync(app, repo, PartU.PickTwoText);
        shot = Shot(app, "u19f-pick-redone");
        AssertKeepMove(app, calls, PartU.Redo.Message(PartU.PickTwoText), "main", PartU.MainWorkSha, PartU.PickedAlphaTwoSha);
        AssertGraph(app, PartU.G8, 4, showsDate: false, picture: shot);
        AssertArrows(app, 2, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.PickTwoText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U19, part D: "Revert this commit" on Main work; undone and redone ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U19_revert()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 10: Main work's "Revert this commit".
        await ActAsync(app, () => RightClickRowAndChoose(app, 3, null, "Revert this commit"));
        AssertStatus(app, PartU.StatusRevertedMainWork);
        Assert.Equal(PartU.RevertSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal("Revert \"Main work\"", await GitTextAsync(repo, "log", "-1", "--format=%s", PartU.RevertSha));
        Assert.Equal(Scenarios.OperationsFiles.Core, WorkingFile(repo, "src/core.txt"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await StatusAsync(repo));
        AssertGraph(app, PartU.G9, 4, showsDate: false);
        AssertArrows(app, 1, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RevertMainWorkText));

        // Step 11: Undo: main back to Main work, its change back in src/core.txt.
        var calls = await UndoAsync(app, repo, PartU.RevertMainWorkText);
        var shot = Shot(app, "u19g-revert-undone");
        AssertKeepMove(app, calls, PartU.Undo.Message(PartU.RevertMainWorkText), "main", PartU.RevertSha, PartU.MainWorkSha);
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, WorkingFile(repo, "src/core.txt"));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.RevertMainWorkText));

        // Step 12: Redo.
        calls = await RedoAsync(app, repo, PartU.RevertMainWorkText);
        shot = Shot(app, "u19h-revert-redone");
        AssertKeepMove(app, calls, PartU.Redo.Message(PartU.RevertMainWorkText), "main", PartU.MainWorkSha, PartU.RevertSha);
        AssertGraph(app, PartU.G9, 4, showsDate: false, picture: shot);
        AssertArrows(app, 1, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RevertMainWorkText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U19, part E: "Rebase feature/topic onto main" from main's menu; undone (only feature/topic moves back) and redone ("rebase").</summary>
    [AvaloniaFact]
    public async Task Check_U19_rebase()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        AssertGraph(app, PartU.R0, 3, showsDate: true);
        var backup = $"{PartU.BackupRefName("rebase", 1, "/feature/topic")} {PartU.TopicTwoSha}";

        // Step 13: main's "Rebase feature/topic onto main" (phase 4's A2 part A).
        app.RightClick(await app.RefRowAsync("Local branches", "main"));
        app.Menu();
        await ActAsync(app, () => app.ClickMenuItem("Rebase feature/topic onto main"));
        AssertStatus(app, PartU.StatusRebasedTopic);
        Assert.Equal(PartU.TopicTwoOntoMainSha, await RevParseAsync(repo, "refs/heads/feature/topic"));
        Assert.Equal([backup], await BackupLinesAsync(repo));
        Assert.Equal(["## feature/topic...origin/feature/topic [ahead 4, behind 2]"], await StatusAsync(repo));
        AssertGraph(app, PartU.R1, 3, showsDate: true);
        AssertArrows(app, 4, 2, "Local branches", "feature", "topic");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RebaseTopicText));

        // Step 14: Undo: the Keep move of feature/topic back to its old tip; no other branch written.
        var calls = await UndoAsync(app, repo, PartU.RebaseTopicText);
        var shot = Shot(app, "u19i-rebase-undone");
        AssertKeepMove(app, calls, PartU.Undo.Message(PartU.RebaseTopicText), "feature/topic", PartU.TopicTwoOntoMainSha, PartU.TopicTwoSha);
        Assert.Null(WorkingFile(repo, "docs/main.txt"));
        Assert.Equal("core\n", WorkingFile(repo, "src/core.txt"));
        Assert.Equal(["## feature/topic...origin/feature/topic"], await StatusAsync(repo));
        Assert.Equal([PartU.Undo.Message(PartU.RebaseTopicText)], await ReflogAsync(repo, "HEAD", 1));
        Assert.Equal(PartU.MainMoreSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(PartU.OtherOneSha, await RevParseAsync(repo, "refs/heads/feature/other"));
        Assert.Equal([backup], await BackupLinesAsync(repo));
        AssertGraph(app, PartU.R0, 3, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.RebaseTopicText));

        // Step 15: Redo.
        calls = await RedoAsync(app, repo, PartU.RebaseTopicText);
        shot = Shot(app, "u19j-rebase-redone");
        AssertKeepMove(app, calls, PartU.Redo.Message(PartU.RebaseTopicText), "feature/topic", PartU.TopicTwoSha, PartU.TopicTwoOntoMainSha);
        AssertGraph(app, PartU.R1, 3, showsDate: true, picture: shot);
        AssertArrows(app, 4, 2, "Local branches", "feature", "topic");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RebaseTopicText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U19, part F: feature/other dragged onto main, "Check out feature/other and rebase it
    /// onto main"; undone (the Keep move while HEAD is still on it, then the switch back) and
    /// redone (the switch, then the Keep move) ("rebase").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U19_check_out_and_rebase()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 16: the drag and the drop menu's rebase item (phase 4's A3).
        DragOtherOntoMain(app);
        await ActAsync(app, () => app.ClickMenuItem("Check out feature/other and rebase it onto main"));
        AssertStatus(app, PartU.StatusRebasedOther);
        Assert.Equal("feature/other", await repo.CurrentBranchAsync());
        Assert.Equal(PartU.OtherOneOntoMainSha, await repo.HeadAsync());
        Assert.Equal([$"{PartU.BackupRefName("rebase", 1, "/feature/other")} {PartU.OtherOneSha}"], await BackupLinesAsync(repo));
        AssertGraph(app, PartU.R3, 2, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RebaseOtherText));

        // Step 17: Undo: the Keep move of feature/other, then the switch back to feature/topic.
        var calls = await UndoAsync(app, repo, PartU.RebaseOtherText);
        var shot = Shot(app, "u19k-checkout-rebase-undone");
        AssertWrites(
            app,
            calls,
            ["git update-index", "git read-tree", "git update-ref", "git switch"],
            PartU.Refresh,
            PartU.ReadTree(PartU.OtherOneOntoMainSha, PartU.OtherOneSha),
            PartU.KeepMoveRef(PartU.Undo.Message(PartU.RebaseOtherText), "feature/other", PartU.OtherOneSha, PartU.OtherOneOntoMainSha),
            PartU.UndoSwitch("feature/topic"));
        AssertNoReplay(app, calls);
        Assert.Equal(["## feature/topic...origin/feature/topic"], await StatusAsync(repo));
        Assert.Equal(PartU.OtherOneSha, await RevParseAsync(repo, "refs/heads/feature/other"));
        Assert.Equal(["checkout: moving from feature/other to feature/topic", PartU.Undo.Message(PartU.RebaseOtherText)], await ReflogAsync(repo, "HEAD", 2));
        AssertGraph(app, PartU.R0, 3, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.RebaseOtherText));

        // Step 18: Redo: the switch to feature/other first, then the Keep move.
        calls = await RedoAsync(app, repo, PartU.RebaseOtherText);
        shot = Shot(app, "u19l-checkout-rebase-redone");
        AssertWrites(
            app,
            calls,
            ["git update-index", "git read-tree", "git update-ref", "git switch"],
            PartU.UndoSwitch("feature/other"),
            PartU.Refresh,
            PartU.ReadTree(PartU.OtherOneSha, PartU.OtherOneOntoMainSha),
            PartU.KeepMoveRef(PartU.Redo.Message(PartU.RebaseOtherText), "feature/other", PartU.OtherOneOntoMainSha, PartU.OtherOneSha));
        AssertNoReplay(app, calls);
        Assert.Equal([PartU.Redo.Message(PartU.RebaseOtherText), "checkout: moving from feature/topic to feature/other"], await ReflogAsync(repo, "HEAD", 2));
        AssertGraph(app, PartU.R3, 2, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RebaseOtherText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U19, part G: feature/other dragged onto main, "Check out main and merge feature/other
    /// into it"; undone (the Keep move of main, then the switch back to feature/topic) and redone
    /// ("rebase").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U19_check_out_and_merge()
    {
        using var repo = await Scenarios.RebaseAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 19: the same drag; "Check out main and merge feature/other into it".
        DragOtherOntoMain(app);
        var calls = await ActAsync(app, () => app.ClickMenuItem("Check out main and merge feature/other into it"));
        AssertCalledInOrder(app, calls, PartU.Git("switch", "--no-guess", "main"), PartU.MergeOtherCall);
        AssertStatus(app, PartU.StatusMergedOther);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(PartU.MergeOtherSha, await repo.HeadAsync());
        Assert.Equal($"{PartU.MainMoreSha} {PartU.OtherOneSha}", await GitTextAsync(repo, "log", "-1", "--format=%P", PartU.MergeOtherSha));
        Assert.Equal(["## main...origin/main [ahead 2]"], await StatusAsync(repo));
        AssertGraph(app, PartU.R4, 3, showsDate: true);
        AssertArrows(app, 2, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.MergeOtherText));

        // Step 20: Undo: the Keep move of main (other/one.txt removed), then the switch to feature/topic.
        calls = await UndoAsync(app, repo, PartU.MergeOtherText);
        var shot = Shot(app, "u19m-checkout-merge-undone");
        AssertWrites(
            app,
            calls,
            ["git update-index", "git read-tree", "git update-ref", "git switch"],
            PartU.Refresh,
            PartU.ReadTree(PartU.MergeOtherSha, PartU.MainMoreSha),
            PartU.KeepMoveRef(PartU.Undo.Message(PartU.MergeOtherText), "main", PartU.MainMoreSha, PartU.MergeOtherSha),
            PartU.UndoSwitch("feature/topic"));
        AssertNoReplay(app, calls);
        Assert.Equal(["## feature/topic...origin/feature/topic"], await StatusAsync(repo));
        Assert.Null(WorkingFile(repo, "other/one.txt"));
        Assert.Equal(PartU.MainMoreSha, await RevParseAsync(repo, "refs/heads/main"));
        AssertGraph(app, PartU.R0, 3, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.MergeOtherText));

        // Step 21: Redo: the switch to main, then the Keep move to the merge commit.
        calls = await RedoAsync(app, repo, PartU.MergeOtherText);
        shot = Shot(app, "u19n-checkout-merge-redone");
        AssertWrites(
            app,
            calls,
            ["git update-index", "git read-tree", "git update-ref", "git switch"],
            PartU.UndoSwitch("main"),
            PartU.Refresh,
            PartU.ReadTree(PartU.MainMoreSha, PartU.MergeOtherSha),
            PartU.KeepMoveRef(PartU.Redo.Message(PartU.MergeOtherText), "main", PartU.MergeOtherSha, PartU.MainMoreSha));
        AssertNoReplay(app, calls);
        Assert.Equal(PartU.MergeOtherSha, await repo.HeadAsync());
        AssertGraph(app, PartU.R4, 3, showsDate: true, picture: shot);
        AssertArrows(app, 2, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.MergeOtherText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U19, part H: an interactive rebase from Base with Five dropped (D) and Two reworded
    /// (R); undone (Five back) and redone ("interactive").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U19_interactive_rebase()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        AssertGraph(app, PartU.GI, 1, showsDate: true);

        // Step 22: the list from Base, Five dropped, Two reworded, Start rebase.
        RightClickRowAndChoose(app, 6, new Point(468, 285), PartU.InteractiveFromHereItem);
        await app.WaitForRebaseListAsync();
        await app.WaitForAsync(() => app.RebaseListSnapshot().Row("Five").IsSelected, "Five selected in the list");
        Assert.Equal(PartU.L0Rows, app.RebaseListSnapshot().Rows.Select(row => row.Name));
        app.PressKey(Key.D);
        await app.WaitForAsync(() => app.RebaseListSnapshot().Row("Five").ActionText == "Drop", "Five's button to read Drop");
        var two = app.RebaseRowPoint("Two");
        Assert.True(Math.Abs(two.Y - PartU.ListRowTwoPoint.Y) <= 2, $"The list row Two is at y {two.Y}; the report says {PartU.ListRowTwoPoint.Y}.");
        app.Click(PartU.ListRowTwoPoint);
        app.PressKey(Key.R);
        await app.WaitForDialogAsync<MessageDialogViewModel>();
        Assert.Equal(PartU.RewordTwoTitle, app.MessageDialog().Title);
        app.ReplaceText("MessageSummary", PartU.TwoReworded);
        Assert.Equal("Save", app.MessageDialog().ConfirmText);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => app.RebaseListSnapshot().Row("Two").SecondLine == PartU.TwoRewordedSecondLine, "Two's second line");
        var calls = await ActAsync(app, app.ClickRebaseStart);
        await app.WaitForAsync(() => !app.IsRebaseListOpen, "the list to close");
        AssertCalled(app, calls, "git rebase -i " + PartU.BaseSha);
        AssertStatus(app, PartU.StatusRebasedMainFive);
        Assert.Equal(PartU.InteractiveTipSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal([$"{PartU.BackupRefName("rebase", 1, "/main")} {PartU.FiveSha}"], await BackupLinesAsync(repo));
        Assert.Equal(["## main"], await StatusAsync(repo));
        Assert.Null(WorkingFile(repo, "src/five.txt"));
        AssertGraph(app, PartU.GU19, 1, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.InteractiveText));

        // Step 23: Undo: main back to Five, src/five.txt back.
        calls = await UndoAsync(app, repo, PartU.InteractiveText);
        var shot = Shot(app, "u19o-interactive-undone");
        AssertKeepMove(app, calls, PartU.Undo.Message(PartU.InteractiveText), "main", PartU.InteractiveTipSha, PartU.FiveSha);
        Assert.Equal(Scenarios.InteractiveFiles.Five, WorkingFile(repo, "src/five.txt"));
        AssertGraph(app, PartU.GI, 1, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.InteractiveText));

        // Step 24: Redo.
        calls = await RedoAsync(app, repo, PartU.InteractiveText);
        shot = Shot(app, "u19p-interactive-redone");
        AssertKeepMove(app, calls, PartU.Redo.Message(PartU.InteractiveText), "main", PartU.FiveSha, PartU.InteractiveTipSha);
        Assert.Null(WorkingFile(repo, "src/five.txt"));
        AssertGraph(app, PartU.GU19, 1, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.InteractiveText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U19, part I: phase 4's B8 squash of Three and Four; undone and redone (the trees equal: no file changes) ("interactive").</summary>
    [AvaloniaFact]
    public async Task Check_U19_squash()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 25: Three, then Four added; "Squash 2 commits…"; "Squash" with the message as it opens.
        ClickRow(app, 2);
        app.Click(app.GraphRowPoint(1), app.CommandModifier);
        await app.WaitForAsync(() => Repo(app).Selection.Count == 2, "Three and Four to be selected");
        app.RightClick(app.GraphRowPoint(1));
        app.Menu();
        app.ClickMenuItem(PartU.SquashItem);
        await app.WaitForDialogAsync<MessageDialogViewModel>();
        await ActAsync(app, () => app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton")));
        AssertStatus(app, PartU.StatusSquashed);
        Assert.Equal(PartU.SquashTipSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal([$"{PartU.BackupRefName("rebase", 1, "/main")} {PartU.FiveSha}"], await BackupLinesAsync(repo));
        AssertGraph(app, PartU.GB8, 1, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.SquashText));

        // Step 26: Undo: main back to Five; the two trees are the same.
        var files = WorkingTreeFiles(repo);
        var calls = await UndoAsync(app, repo, PartU.SquashText);
        var shot = Shot(app, "u19q-squash-undone");
        AssertKeepMove(app, calls, PartU.Undo.Message(PartU.SquashText), "main", PartU.SquashTipSha, PartU.FiveSha);
        Assert.Equal(files, WorkingTreeFiles(repo));
        AssertGraph(app, PartU.GI, 1, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.SquashText));

        // Step 27: Redo.
        calls = await RedoAsync(app, repo, PartU.SquashText);
        shot = Shot(app, "u19r-squash-redone");
        AssertKeepMove(app, calls, PartU.Redo.Message(PartU.SquashText), "main", PartU.FiveSha, PartU.SquashTipSha);
        AssertGraph(app, PartU.GB8, 1, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.SquashText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U19, part J: phase 4's A4 part A "Pull from origin/main (rebase)"; undone (the fetch
    /// stays: origin/main is not moved back) and redone ("sync" with work pushed from elsewhere and
    /// a local commit).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U19_pull_with_rebase()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        Assert.Equal(PartU.ElsewhereWorkSha, await Scenarios.PushFromElsewhereAsync(repo, "main", "src/elsewhere.txt", "elsewhere\n", "Elsewhere work"));
        Assert.Equal(PartU.LocalWorkSha, await repo.CommitFileAsync("src/local.txt", "local\n", "Local work"));
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        AssertGraph(app, PartU.S1, 3, showsDate: true);

        // Step 28: main's "Pull from origin/main (rebase)".
        app.RightClick(await app.RefRowAsync("Local branches", "main"));
        app.Menu();
        var calls = await ActAsync(app, () => app.ClickMenuItem(PartU.PullRebaseItem), NetworkTimeout);
        var fetch = app.GitCalls().Skip(calls).First(call => call.CommandText.StartsWith("git fetch", StringComparison.Ordinal));
        if (await IsReportGitAsync())
        {
            Assert.Contains(PartU.FetchMainLine, fetch.StandardError.Split(['\r', '\n']).Select(line => line.TrimEnd()));
        }

        // The fetch, the backup, then git rebase (phase 4's A4 part A).
        Assert.Equal(PartU.PullFetchCall, fetch.CommandText);
        AssertCalledInOrder(app, calls, PartU.PullFetchCall, PartU.BackupWrite, PartU.PullRebaseCall);
        AssertBackupWrite(app, calls, [PartU.CreateLine(PartU.BackupRefName("rebase", 1, "/main"), PartU.LocalWorkSha)]);
        Assert.Single(CallsSince(app, calls), call => call.StartsWith("git rebase", StringComparison.Ordinal));
        AssertStatus(app, PartU.StatusPulledAndRebased);
        Assert.Equal(PartU.LocalWorkRebasedSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal([$"{PartU.BackupRefName("rebase", 1, "/main")} {PartU.LocalWorkSha}"], await BackupLinesAsync(repo));
        Assert.Equal(["## main...origin/main [ahead 1]"], await StatusAsync(repo));
        AssertGraph(app, PartU.S2, 3, showsDate: true);
        AssertArrows(app, 1, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.PullText));

        // Step 29: Undo: main back to Local work; origin/main stays fetched.
        calls = await UndoAsync(app, repo, PartU.PullText);
        var shot = Shot(app, "u19s-pull-undone");
        AssertKeepMove(app, calls, PartU.Undo.Message(PartU.PullText), "main", PartU.LocalWorkRebasedSha, PartU.LocalWorkSha);
        Assert.Null(WorkingFile(repo, "src/elsewhere.txt"));
        Assert.Equal("local\n", WorkingFile(repo, "src/local.txt"));
        Assert.Equal(PartU.ElsewhereWorkSha, await RevParseAsync(repo, "refs/remotes/origin/main"));
        Assert.Equal(["## main...origin/main [ahead 1, behind 1]"], await StatusAsync(repo));
        AssertGraph(app, PartU.S2u, 4, showsDate: false, picture: shot);
        AssertArrows(app, 1, 1, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.PullText));

        // Step 30: Redo.
        calls = await RedoAsync(app, repo, PartU.PullText);
        shot = Shot(app, "u19t-pull-redone");
        AssertKeepMove(app, calls, PartU.Redo.Message(PartU.PullText), "main", PartU.LocalWorkSha, PartU.LocalWorkRebasedSha);
        AssertGraph(app, PartU.S2, 3, showsDate: true, picture: shot);
        AssertArrows(app, 1, 0, "Local branches", "main");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.PullText));
        AssertNoClippedText(app);
    }

    // ---- U19's own helpers ----

    /// <summary>
    /// A Keep move (7.3, D169) since <paramref name="calls"/>: <c>git update-index -q --refresh</c>,
    /// then <c>git read-tree -m -u &lt;from&gt; &lt;to&gt;</c> (exit 0), then the positional
    /// <c>git update-ref -m &lt;message&gt; refs/heads/&lt;branch&gt; &lt;to&gt; &lt;from&gt;</c>; no
    /// other ref write. Neither merges, picks nor rebases again.
    /// </summary>
    private static void AssertKeepMove(ShellDriver app, int calls, string message, string branch, string from, string to)
    {
        AssertWrites(
            app,
            calls,
            ["git update-index", "git read-tree", "git update-ref", "git switch"],
            PartU.Refresh,
            PartU.ReadTree(from, to),
            PartU.KeepMoveRef(message, branch, to, from));
        Assert.Equal(0, app.GitCalls().Skip(calls).Last(call => call.CommandText == PartU.ReadTree(from, to)).ExitCode);
        AssertNoReplay(app, calls);
    }

    /// <summary>A step since <paramref name="calls"/> neither merged, picked, reverted nor rebased again: the branch went back to commits that exist.</summary>
    private static void AssertNoReplay(ShellDriver app, int calls)
    {
        foreach (var command in new[] { "git merge", "git cherry-pick", "git revert", "git rebase" })
        {
            AssertNoCall(app, calls, command);
        }
    }

    /// <summary>Right-clicks the label <paramref name="label"/> of commit row <paramref name="row"/> at its centre and waits for its menu.</summary>
    private static void RightClickLabel(ShellDriver app, int row, string label)
    {
        app.RightClick(app.GraphLabelBounds(row, label).Center);
        app.Menu();
    }

    /// <summary>Phase 4's A3 drag: the label feature/other (row 0) onto the label main (row 1), five moves with the button held; its drop menu open.</summary>
    private static void DragOtherOntoMain(ShellDriver app)
    {
        var other = app.GraphLabelBounds(0, "feature/other").Center;
        var main = app.GraphLabelBounds(1, "main").Center;
        app.PressMouse(other);
        app.MoveMouseHeld(main, steps: PartU.DragSteps);
        Assert.True(app.BranchDrag.IsDragging, "The moves did not start a drag.");
        app.ReleaseMouse(main);
        app.Menu();
    }

    /// <summary>Every file of the working tree (outside .git) with its text, by path.</summary>
    private static Dictionary<string, string> WorkingTreeFiles(TempRepo repo) =>
        Directory.EnumerateFiles(repo.Path, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(repo.Path, file).Replace('\\', '/'))
            .Where(path => !path.StartsWith(".git/", StringComparison.Ordinal))
            .ToDictionary(path => path, path => WorkingFile(repo, path)!, StringComparer.Ordinal);
}
