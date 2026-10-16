// Part U, check U16 (docs/test-reports/phase-5.md): undo and redo of the stage panel's writes on
// "history": a commit and an amend (the message back in the boxes, cleared again by Redo), a
// discard of a file, of a hunk, of lines and of all, the restore bar going and coming back and its
// Restore as the discard's Undo, the bar's Restore recorded as an entry of its own, D75's question
// (UD18), and two entries undone and two redone in a row. One method per part of the check, each
// on a fresh copy (design 15.4, UD17).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Diff;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase5;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5Undo;

public sealed partial class Phase5ChecksU
{
    /// <summary>
    /// Check U16, part A: a commit made in the stage panel, undone (the branch back, the change
    /// staged again, the message back in the boxes) and redone (the same commit, the boxes
    /// cleared); the Undo button's tooltip ("history"). Repeated in the real window.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U16_commit()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: H0 with its working-changes row; nothing selected; T0.
        AssertGraph(app, PartU.H0, 2, PartU.WorkingRow, showsDate: true);
        AssertNothingSelected(app);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 2: stage all, the message typed, Commit.
        await ClickWorkingRowAsync(app, PartU.WorkingRowPoint);
        app.Click(app.FindByAutomationId("StageAllButton"));
        await AssertStagePanelAsync(app, [], ["M Reader.cs src"]);
        app.Click(app.FindByAutomationId("CommitSummary"));
        app.Type(PartU.CommitSummary);
        app.Click(app.FindByAutomationId("CommitDescription"));
        app.Type(PartU.CommitDescription);
        var commit = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal("Commit 1 file", commit.Content);
        var calls = app.GitCalls().Count;
        app.Click(commit);
        await WaitForHeadAsync(app, PartU.U16CommitSha);
        AssertCalled(app, calls, PartU.CommitCall);
        AssertStatus(app, PartU.Ready);
        Assert.Equal(PartU.U16CommitSha, await RevParseAsync(repo, "refs/heads/main"));
        await AssertCommitAsync(repo, PartU.U16CommitSha, PartU.CommitMessage, [PartU.ReadWholeLinesSha], PartU.TestAuthor, AppCommitTime, AppCommitTime);
        Assert.Equal(["## main...origin/main [ahead 1]"], await StatusAsync(repo));
        AssertGraph(app, PartU.HU1, 2, showsDate: true);
        AssertRowSelected(app, 0, PartU.U16CommitSha);
        await WaitForDetailsAsync(app, PartU.CommitSummary);
        Assert.Equal((string.Empty, string.Empty), (Repo(app).Changes.Summary, Repo(app).Changes.Description));
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CommitText));
        AssertArrows(app, 1, 0, "Local branches", "main");

        // Step 3: the pointer rests on Undo: its tooltip under the button, inside the window.
        var undo = app.FindByAutomationId(ShellDriver.UndoButtonId);
        var tip = await app.RestPointerOnAsync(undo);
        Shot(app, "u16a-undo-tooltip", keepPointer: true);
        Assert.Equal(PartU.Undo.Title(PartU.CommitText), tip);
        AssertToolTipUnder(app, undo, tip);

        // Step 4: Undo: a soft move in the step's transaction, no read-tree.
        calls = await UndoAsync(app, repo, PartU.CommitText);
        AssertTransaction(app, calls, PartU.Undo.Message(PartU.CommitText), [PartU.UpdateLine("refs/heads/main", PartU.ReadWholeLinesSha, PartU.U16CommitSha)]);
        AssertNoCall(app, calls, "git read-tree");
        Assert.Equal(PartU.ReadWholeLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main", "M  src/Reader.cs"], await StatusAsync(repo));
        AssertShort(PartU.ReaderWorkingBlob, await IndexBlobAsync(repo, PartU.ReaderPath), "the index's src/Reader.cs");
        AssertShort(PartU.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "the working src/Reader.cs");
        Assert.Equal([PartU.Undo.Message(PartU.CommitText)], await ReflogAsync(repo, "refs/heads/main", 1));
        Assert.Equal([PartU.Undo.Message(PartU.CommitText)], await ReflogAsync(repo, "HEAD", 1));
        Assert.Equal(PartU.U16CommitSha, await RevParseAsync(repo, "main@{1}"));
        AssertGraph(app, PartU.H0, 2, PartU.WorkingRow, showsDate: true);
        AssertNothingSelected(app);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.CommitText));

        // Step 5: the working-changes row: the change staged, the message back in both boxes.
        await ClickWorkingRowAsync(app);
        Shot(app, "u16b-commit-undone");
        AssertWorkingRowSelected(app);
        await AssertStagePanelAsync(app, [], ["M Reader.cs src"]);
        Assert.Equal(PartU.CommitSummary, BoxText(app, "CommitSummary"));
        Assert.Equal(PartU.CommitSummaryCounter, app.Find<TextBlock>("SummaryCounter").Text);
        Assert.Equal(PartU.CommitDescription, BoxText(app, "CommitDescription"));
        Assert.False(((CheckBox)app.FindByAutomationId("AmendCheckBox")).IsChecked);
        commit = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal("Commit 1 file", commit.Content);
        Assert.True(commit.IsEffectivelyEnabled, "Commit is disabled.");

        // Step 6: "Read whole lines" (display row 1) clicked, then Redo: the same commit, the boxes cleared.
        ClickRow(app, 0, new Point(468, 155));
        calls = await RedoAsync(app, repo, PartU.CommitText);
        var shot = Shot(app, "u16c-commit-redone");
        AssertTransaction(app, calls, PartU.Redo.Message(PartU.CommitText), [PartU.UpdateLine("refs/heads/main", PartU.U16CommitSha, PartU.ReadWholeLinesSha)]);
        AssertNoCall(app, calls, "git read-tree");
        Assert.Equal(PartU.U16CommitSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await StatusAsync(repo));
        Assert.Equal([PartU.Redo.Message(PartU.CommitText)], await ReflogAsync(repo, "refs/heads/main", 1));
        Assert.Equal([PartU.Redo.Message(PartU.CommitText)], await ReflogAsync(repo, "HEAD", 1));
        AssertGraph(app, PartU.HU1, 2, showsDate: true, picture: shot);
        AssertRowSelected(app, 1, PartU.ReadWholeLinesSha);
        Assert.Equal((string.Empty, string.Empty), (Repo(app).Changes.Summary, Repo(app).Changes.Description));
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CommitText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U16, part B: an amend of "Read whole lines" with the staged change, undone (its
    /// message back in the summary) and redone ("history").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U16_amend()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        var stopTracing = await RevParseAsync(repo, PartU.ReadWholeLinesSha + "^");
        AssertShort(PartU.StopTracingShort, stopTracing, "Read whole lines' parent");

        // Step 7: stage all, Amend ticked (the summary filled with HEAD's message), then the amend.
        await ClickWorkingRowAsync(app);
        app.Click(app.FindByAutomationId("StageAllButton"));
        await AssertStagePanelAsync(app, [], ["M Reader.cs src"]);
        app.Click(app.FindByAutomationId("AmendCheckBox"));
        await app.WaitForAsync(() => BoxText(app, "CommitSummary") == PartU.AmendSummary, "the summary filled with HEAD's message");
        Assert.Equal(string.Empty, BoxText(app, "CommitDescription"));
        var amend = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal("Amend previous commit", amend.Content);
        app.Click(amend);
        await WaitForHeadAsync(app, PartU.U16AmendSha);
        await AssertCommitAsync(repo, PartU.U16AmendSha, PartU.AmendSummary, [stopTracing!], PartU.Jonas, ScenarioTime(13), AppCommitTime);
        Assert.Equal(["commit (amend): " + PartU.AmendSummary], await ReflogAsync(repo, "HEAD", 1));
        Assert.Equal(["## main...origin/main [ahead 1, behind 1]"], await StatusAsync(repo));
        AssertGraph(app, PartU.HU2, 2, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.AmendText));
        AssertArrows(app, 1, 1, "Local branches", "main");

        // Step 8: Undo, then the working-changes row: the change staged, the summary "Read whole lines".
        var calls = await UndoAsync(app, repo, PartU.AmendText);
        AssertTransaction(app, calls, PartU.Undo.Message(PartU.AmendText), [PartU.UpdateLine("refs/heads/main", PartU.ReadWholeLinesSha, PartU.U16AmendSha)]);
        AssertNoCall(app, calls, "git read-tree");
        Assert.Equal(PartU.ReadWholeLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main", "M  src/Reader.cs"], await StatusAsync(repo));
        AssertGraph(app, PartU.H0, 2, PartU.WorkingRow, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.AmendText));
        await ClickWorkingRowAsync(app);
        Shot(app, "u16d-amend-undone");
        await AssertStagePanelAsync(app, [], ["M Reader.cs src"]);
        Assert.Equal(PartU.AmendSummary, BoxText(app, "CommitSummary"));
        Assert.Equal(PartU.AmendSummaryCounter, app.Find<TextBlock>("SummaryCounter").Text);
        Assert.Equal(string.Empty, BoxText(app, "CommitDescription"));
        Assert.False(((CheckBox)app.FindByAutomationId("AmendCheckBox")).IsChecked);
        Assert.Equal("Commit 1 file", ((Button)app.FindByAutomationId("CommitButton")).Content);

        // Step 9: "Read whole lines" (display row 1) clicked, then Redo.
        ClickRow(app, 0, new Point(468, 155));
        calls = await RedoAsync(app, repo, PartU.AmendText);
        var shot = Shot(app, "u16e-amend-redone");
        AssertTransaction(app, calls, PartU.Redo.Message(PartU.AmendText), [PartU.UpdateLine("refs/heads/main", PartU.U16AmendSha, PartU.ReadWholeLinesSha)]);
        Assert.Equal(PartU.U16AmendSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [ahead 1, behind 1]"], await StatusAsync(repo));
        AssertGraph(app, PartU.HU2, 2, showsDate: true, picture: shot);
        AssertRowSelected(app, 1, PartU.ReadWholeLinesSha);
        Assert.Equal(string.Empty, Repo(app).Changes.Summary);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.AmendText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U16, part C: a discard of src/Reader.cs undone (the restore bar goes) and redone (it
    /// comes back for the same snapshot); the bar's Restore as the discard's Undo; a second
    /// discard and D75's question when the file changed since it (UD18), the Restore recorded as
    /// an entry of its own ("history" with docs/todo.md untracked).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U16_discard_file()
    {
        using var repo = await HistoryWithTodoAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 10: the file's own Discard, then "Discard" in phase 2's dialog.
        await ClickWorkingRowAsync(app);
        await DiscardReaderAsync(app);
        await AssertStagePanelAsync(app, ["A todo.md docs"], []);
        AssertRestoreBar(app, PartU.RestoreBarReader);
        Assert.Equal(["## main...origin/main", "?? docs/todo.md"], await StatusAsync(repo));
        AssertShort(PartU.ReaderCommittedBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        var first = PartU.BackupRefName("discard", 1);
        Assert.Equal([first], (await BackupsAsync(repo)).Select(backup => backup.Name));
        var snapshot = await AssertDiscardSnapshotAsync(repo, first, [PartU.ReaderPath], (PartU.ReaderPath, PartU.ReaderWorkingBlob));
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DiscardReaderText));

        // Step 11: Undo: the file from the snapshot, the bar gone.
        var calls = await UndoAsync(app, repo, PartU.DiscardReaderText);
        Shot(app, "u16f-discard-undone");
        AssertCalled(app, calls, PartU.Git("restore", "--source=" + snapshot, "--worktree", "--", PartU.ReaderPath));
        AssertShort(PartU.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal(["## main...origin/main", " M src/Reader.cs", "?? docs/todo.md"], await StatusAsync(repo));
        AssertRestoreBar(app, null);
        await AssertStagePanelAsync(app, ["A todo.md docs", "M Reader.cs src"], []);
        Assert.Equal([first], (await BackupsAsync(repo)).Select(backup => backup.Name));
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.DiscardReaderText));

        // Step 12: Redo: the committed text from the recorded bytes, the bar back.
        calls = await RedoAsync(app, repo, PartU.DiscardReaderText);
        Shot(app, "u16g-discard-redone");
        AssertNoCall(app, calls, "git restore");
        AssertShort(PartU.ReaderCommittedBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        AssertRestoreBar(app, PartU.RestoreBarReader);
        await AssertStagePanelAsync(app, ["A todo.md docs"], []);
        Assert.Equal([first], (await BackupsAsync(repo)).Select(backup => backup.Name));
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DiscardReaderText));

        // Step 13: the bar's Restore, with no question: the entry's Undo (7.9).
        var restore = app.FindByAutomationId("RestoreDiscardButton");
        calls = await StepAsync(app, repo, PartU.Undo, PartU.DiscardReaderText, () =>
        {
            app.Click(restore);
            return Task.CompletedTask;
        });
        Shot(app, "u16h-bar-restore-undid");
        AssertCalled(app, calls, PartU.Git("restore", "--source=" + snapshot, "--worktree", "--", PartU.ReaderPath));
        AssertShort(PartU.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        AssertRestoreBar(app, null);
        await AssertStagePanelAsync(app, ["A todo.md docs", "M Reader.cs src"], []);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.DiscardReaderText));

        // Step 14: a second discard, a new entry (the undone one dropped); then the file changed outside.
        await DiscardReaderAsync(app);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DiscardReaderText));
        AssertRestoreBar(app, PartU.RestoreBarReader);
        var second = PartU.BackupRefName("discard", 2);
        Assert.Equal([first, second], (await BackupsAsync(repo)).Select(backup => backup.Name));
        Assert.Equal(snapshot, await RevParseAsync(repo, second));
        AssertShort(PartU.ReaderCommittedBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        File.AppendAllText(Path.Combine(repo.Path, PartU.ReaderPath), PartU.ChangedOutsideLine);
        await app.WaitForAsync(() => FileRows(app, "UnstagedFileList").Contains("M Reader.cs src"), "the stage panel to list src/Reader.cs again", OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        AssertShort(PartU.ReaderChangedOutsideBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal(["## main...origin/main", " M src/Reader.cs", "?? docs/todo.md"], await StatusAsync(repo));
        await AssertStagePanelAsync(app, ["A todo.md docs", "M Reader.cs src"], []);
        AssertRestoreBar(app, PartU.RestoreBarReader);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DiscardReaderText));

        // Step 15: the bar's Restore asks D75's question; "Restore" runs phase 2's Restore, recorded.
        var mark = app.LogMark();
        var operations = ForegroundOperations(app).Count;
        app.Click(app.FindByAutomationId("RestoreDiscardButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Shot(app, "u16i-restore-question", keepPointer: true);
        AssertQuestion(app, PartU.ChangedSinceTitle, PartU.ChangedSinceQuestion, PartU.ChangedSinceDetail, PartU.ChangedSinceConfirm);
        Assert.Equal(operations, ForegroundOperations(app).Count);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => Repo(app).Changes.RestoreText is null, "the restore bar to go");
        await app.WaitForIdleAsync();
        await AssertNoDialogAsync(app);
        Assert.Equal(["Restore"], ForegroundOperations(app).Skip(operations).Select(record => record.Title));
        Assert.DoesNotContain(app.LogLinesSince(mark), line => line.Contains(": Undo ", StringComparison.Ordinal));
        var third = PartU.BackupRefName("discard", 3);
        Assert.Equal([first, second, third], (await BackupsAsync(repo)).Select(backup => backup.Name));
        await AssertDiscardSnapshotAsync(repo, third, [PartU.ReaderPath], (PartU.ReaderPath, PartU.ReaderChangedOutsideBlob));
        AssertShort(PartU.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal(["## main...origin/main", " M src/Reader.cs", "?? docs/todo.md"], await StatusAsync(repo));
        AssertRestoreBar(app, null);
        AssertStatus(app, PartU.Ready);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RestoreText));
    }

    /// <summary>
    /// Check U16, part D: the second hunk of src/Reader.cs discarded in its diff, undone (both
    /// hunks back) and redone (the first hunk kept) ("history" with docs/todo.md untracked).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U16_discard_hunk()
    {
        using var repo = await HistoryWithTodoAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 16: the diff over the graph, the second hunk's "Discard hunk", then "Discard".
        await OpenReaderDiffAsync(app);
        Assert.Equal([PartU.FirstHunkHeader, PartU.SecondHunkHeader], HunkHeaders(app));
        app.Click(DiffButtons(app, "DiscardHunkButton")[1]);
        await AssertDiscardQuestionAsync(app, PartU.DiscardHunkQuestion);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => HunkHeaders(app).Count == 1, "the diff's one hunk left");
        await app.WaitForIdleAsync();
        Shot(app, "u16j-hunk-discarded");
        Assert.Equal([PartU.FirstHunkHeader], HunkHeaders(app));
        Assert.Contains(PartU.ReaderLine3, AddedLines(app));
        AssertShort(PartU.ReaderFirstHunkBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        AssertRestoreBar(app, PartU.RestoreBarReader);
        var backups = await BackupsAsync(repo);
        Assert.Equal([PartU.BackupRefName("discard", 1)], backups.Select(backup => backup.Name));
        var snapshot = await AssertDiscardSnapshotAsync(repo, backups[0].Name, [PartU.ReaderPath], (PartU.ReaderPath, PartU.ReaderWorkingBlob));
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DiscardLinesText));

        // Step 17: Undo: both hunks again in the open diff, no bar.
        var calls = await UndoAsync(app, repo, PartU.DiscardLinesText);
        await app.WaitForAsync(() => HunkHeaders(app).Count == 2, "both hunks again");
        Shot(app, "u16k-hunk-undone");
        AssertCalled(app, calls, PartU.Git("restore", "--source=" + snapshot, "--worktree", "--", PartU.ReaderPath));
        Assert.Equal([PartU.FirstHunkHeader, PartU.SecondHunkHeader], HunkHeaders(app));
        AssertShort(PartU.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        AssertRestoreBar(app, null);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.DiscardLinesText));

        // Step 18: Redo: the bytes the discard left (the first hunk kept), the bar back.
        calls = await RedoAsync(app, repo, PartU.DiscardLinesText);
        await app.WaitForAsync(() => HunkHeaders(app).Count == 1, "the diff's one hunk again");
        Shot(app, "u16l-hunk-redone");
        AssertNoCall(app, calls, "git restore");
        AssertShort(PartU.ReaderFirstHunkBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal([PartU.FirstHunkHeader], HunkHeaders(app));
        AssertRestoreBar(app, PartU.RestoreBarReader);
        Assert.Single(await BackupsAsync(repo));
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DiscardLinesText));
    }

    /// <summary>
    /// Check U16, part E: line 23 of src/Reader.cs discarded in its diff, undone and redone
    /// ("history" with docs/todo.md untracked).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U16_discard_lines()
    {
        using var repo = await HistoryWithTodoAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 19: line 23's number, "Discard lines", then "Discard".
        await OpenReaderDiffAsync(app);
        var line = AddedLine(app, PartU.Line23);
        Assert.Equal(PartU.Line23Text, line.Text.Trim());
        app.Click(app.BoundsOf(DiffText(app)).TopLeft + (DiffText(app).LineNumberPoint(line.Line) ?? throw new InvalidOperationException("Line 23 is not in view.")));
        await app.WaitForAsync(() => app.FindByAutomationId("DiscardLinesButton").IsEffectivelyVisible, "the line buttons");
        app.Click(app.FindByAutomationId("DiscardLinesButton"));
        await AssertDiscardQuestionAsync(app, PartU.DiscardLineQuestion);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => HunkHeaders(app).Contains(PartU.SecondHunkWithoutLine23), "the second hunk without line 23");
        await app.WaitForIdleAsync();
        AssertShort(PartU.ReaderWithoutLine23Blob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal(23, WorkingFile(repo, PartU.ReaderPath)!.TrimEnd('\n').Split('\n').Length);
        AssertWithoutLine23(app);
        AssertRestoreBar(app, PartU.RestoreBarReader);
        var backups = await BackupsAsync(repo);
        var snapshot = await AssertDiscardSnapshotAsync(repo, Assert.Single(backups).Name, [PartU.ReaderPath], (PartU.ReaderPath, PartU.ReaderWorkingBlob));
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DiscardLinesText));

        // Step 20: Undo: both hunks as at the start.
        var calls = await UndoAsync(app, repo, PartU.DiscardLinesText);
        await app.WaitForAsync(() => HunkHeaders(app).Contains(PartU.SecondHunkHeader), "the second hunk with line 23 again");
        Shot(app, "u16m-lines-undone");
        AssertCalled(app, calls, PartU.Git("restore", "--source=" + snapshot, "--worktree", "--", PartU.ReaderPath));
        AssertShort(PartU.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal([PartU.FirstHunkHeader, PartU.SecondHunkHeader], HunkHeaders(app));
        AssertRestoreBar(app, null);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.DiscardLinesText));

        // Step 21: Redo: as after step 19.
        calls = await RedoAsync(app, repo, PartU.DiscardLinesText);
        await app.WaitForAsync(() => HunkHeaders(app).Contains(PartU.SecondHunkWithoutLine23), "the second hunk without line 23 again");
        Shot(app, "u16n-lines-redone");
        AssertNoCall(app, calls, "git restore");
        AssertShort(PartU.ReaderWithoutLine23Blob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        AssertWithoutLine23(app);
        AssertRestoreBar(app, PartU.RestoreBarReader);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DiscardLinesText));
    }

    /// <summary>
    /// Check U16, part F: "Discard all" with an untracked and a changed file and a staged one,
    /// undone (every file back, the index untouched) and redone ("history" with docs/todo.md
    /// untracked and README.md staged).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U16_discard_all()
    {
        using var repo = await HistoryWithTodoAsync();
        repo.WriteFile("README.md", PartU.ReadmeStaged);
        await repo.GitAsync("add", "README.md");
        var readmeIndex = await IndexBlobAsync(repo, "README.md");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 22: "Discard all", then "Discard".
        await ClickWorkingRowAsync(app);
        await AssertStagePanelAsync(app, ["A todo.md docs", "M Reader.cs src"], ["M README.md"]);
        app.Click(app.FindByAutomationId("DiscardAllButton"));
        await AssertDiscardQuestionAsync(app, PartU.DiscardTwoQuestion);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => Repo(app).Changes.RestoreText == PartU.RestoreBarTwoFiles, "the restore bar");
        await app.WaitForIdleAsync();
        await AssertStagePanelAsync(app, [], ["M README.md"]);
        AssertRestoreBar(app, PartU.RestoreBarTwoFiles);
        Assert.Equal(["## main...origin/main", "M  README.md"], await StatusAsync(repo));
        Assert.Null(WorkingFile(repo, PartU.TodoPath));
        AssertShort(PartU.ReaderCommittedBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal(readmeIndex, await IndexBlobAsync(repo, "README.md"));
        var backups = await BackupsAsync(repo);
        Assert.Equal([PartU.BackupRefName("discard", 1)], backups.Select(backup => backup.Name));
        var snapshot = await AssertDiscardSnapshotAsync(repo, backups[0].Name, [PartU.TodoPath, PartU.ReaderPath], (PartU.ReaderPath, PartU.ReaderWorkingBlob));
        Assert.Equal(PartU.Todo, await GitTextAsync(repo, "show", $"{snapshot}:{PartU.TodoPath}") + "\n");
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DiscardTwoText));

        // Step 23: Undo: every file back, the index untouched.
        var calls = await UndoAsync(app, repo, PartU.DiscardTwoText);
        Shot(app, "u16o-all-undone");
        AssertCalled(app, calls, PartU.Git("restore", "--source=" + snapshot, "--worktree", "--", PartU.TodoPath, PartU.ReaderPath));
        Assert.Equal(["## main...origin/main", "M  README.md", " M src/Reader.cs", "?? docs/todo.md"], await StatusAsync(repo));
        Assert.Equal(PartU.Todo, WorkingFile(repo, PartU.TodoPath));
        AssertShort(PartU.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal(PartU.ReadmeStaged, WorkingFile(repo, "README.md"));
        Assert.Equal(readmeIndex, await IndexBlobAsync(repo, "README.md"));
        AssertRestoreBar(app, null);
        await AssertStagePanelAsync(app, ["A todo.md docs", "M Reader.cs src"], ["M README.md"]);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.DiscardTwoText));

        // Step 24: Redo: the recorded state again (todo.md absent, Reader.cs as committed).
        calls = await RedoAsync(app, repo, PartU.DiscardTwoText);
        Shot(app, "u16p-all-redone");
        AssertNoCall(app, calls, "git restore");
        Assert.Null(WorkingFile(repo, PartU.TodoPath));
        AssertShort(PartU.ReaderCommittedBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal(["## main...origin/main", "M  README.md"], await StatusAsync(repo));
        AssertRestoreBar(app, PartU.RestoreBarTwoFiles);
        await AssertStagePanelAsync(app, [], ["M README.md"]);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DiscardTwoText));
    }

    /// <summary>
    /// Check U16, part G: the bar's Restore after another entry (a branch created), recorded as an
    /// entry of its own, undone (the bar stays closed, UD3) and redone ("history" with
    /// docs/todo.md untracked).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U16_restore_recorded()
    {
        using var repo = await HistoryWithTodoAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 25: the discard of step 10.
        await ClickWorkingRowAsync(app);
        await DiscardReaderAsync(app);
        var snapshot = await RevParseAsync(repo, PartU.BackupRefName("discard", 1));

        // Step 26: keep created at "Read whole lines", not checked out.
        await CreateKeepAsync(app);
        AssertStatus(app, PartU.StatusCreatedKeep);
        Assert.Equal(PartU.ReadWholeLinesSha, await RevParseAsync(repo, "refs/heads/keep"));
        Assert.Empty(await ConfigAsync(repo, @"^branch\.keep\."));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        AssertRestoreBar(app, PartU.RestoreBarReader);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CreateKeepText));

        // Step 27: the bar's Restore: phase 2's (the discard is not the entry before the cursor), recorded.
        await ClickWorkingRowAsync(app);
        var mark = app.LogMark();
        var operations = ForegroundOperations(app).Count;
        app.Click(app.FindByAutomationId("RestoreDiscardButton"));
        await app.WaitForAsync(() => Repo(app).Changes.RestoreText is null, "the restore bar to go");
        await app.WaitForIdleAsync();
        await AssertNoDialogAsync(app);
        Assert.Equal(["Restore"], ForegroundOperations(app).Skip(operations).Select(record => record.Title));
        Assert.DoesNotContain(app.LogLinesSince(mark), line => line.Contains(": Undo ", StringComparison.Ordinal));
        AssertShort(PartU.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        AssertRestoreBar(app, null);
        AssertStatus(app, PartU.Ready);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RestoreText));

        // Step 28: Undo: the bytes the restore found written back, the bar closed, keep unchanged.
        var calls = await UndoAsync(app, repo, PartU.RestoreText);
        Shot(app, "u16q-restore-undone");
        AssertNoCall(app, calls, "git restore");
        AssertShort(PartU.ReaderCommittedBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal(["## main...origin/main", "?? docs/todo.md"], await StatusAsync(repo));
        AssertRestoreBar(app, null);
        Assert.Equal(PartU.ReadWholeLinesSha, await RevParseAsync(repo, "refs/heads/keep"));
        AssertButtons(app, new UndoRedoButtons(true, PartU.Undo.Title(PartU.CreateKeepText), true, PartU.Redo.Title(PartU.RestoreText)));

        // Step 29: Redo: the discard's snapshot restored again.
        calls = await RedoAsync(app, repo, PartU.RestoreText);
        Shot(app, "u16r-restore-redone");
        AssertCalled(app, calls, PartU.Git("restore", "--source=" + snapshot, "--worktree", "--", PartU.ReaderPath));
        AssertShort(PartU.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal(["## main...origin/main", " M src/Reader.cs", "?? docs/todo.md"], await StatusAsync(repo));
        AssertRestoreBar(app, null);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RestoreText));
    }

    /// <summary>
    /// Check U16, part H: two entries (a discard, then a branch created) undone in a row, newest
    /// first, and redone in a row ("history" with docs/todo.md untracked).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U16_many_steps()
    {
        using var repo = await HistoryWithTodoAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 30: the discard, then keep.
        await ClickWorkingRowAsync(app);
        await DiscardReaderAsync(app);
        var snapshot = await RevParseAsync(repo, PartU.BackupRefName("discard", 1));
        await CreateKeepAsync(app);
        AssertRestoreBar(app, PartU.RestoreBarReader);
        AssertShort(PartU.ReaderCommittedBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Equal(["## main...origin/main", "?? docs/todo.md"], await StatusAsync(repo));
        Assert.Equal([PartU.BackupRefName("discard", 1)], (await BackupsAsync(repo)).Select(backup => backup.Name));
        Assert.Equal(PartU.ReadWholeLinesSha, await RevParseAsync(repo, "refs/heads/keep"));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CreateKeepText));

        // Step 31: Undo twice, newest entry first.
        var calls = await UndoAsync(app, repo, PartU.CreateKeepText);
        AssertTransaction(app, calls, PartU.Undo.Message(PartU.CreateKeepText), [PartU.DeleteLine("refs/heads/keep", PartU.ReadWholeLinesSha)]);
        calls = await UndoAsync(app, repo, PartU.DiscardReaderText);
        Shot(app, "u16s-two-undone");
        AssertCalled(app, calls, PartU.Git("restore", "--source=" + snapshot, "--worktree", "--", PartU.ReaderPath));
        Assert.Equal(["## main...origin/main", " M src/Reader.cs", "?? docs/todo.md"], await StatusAsync(repo));
        AssertShort(PartU.ReaderWorkingBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        Assert.Null(await RevParseAsync(repo, "refs/heads/keep"));
        Assert.False(await ReflogExistsAsync(repo, "refs/heads/keep"), "keep still has a reflog.");
        Assert.Equal([PartU.BackupRefName("discard", 1)], (await BackupsAsync(repo)).Select(backup => backup.Name));
        AssertRestoreBar(app, null);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.DiscardReaderText));

        // Step 32: Redo twice, oldest entry first.
        calls = await RedoAsync(app, repo, PartU.DiscardReaderText);
        AssertNoCall(app, calls, "git restore");
        AssertShort(PartU.ReaderCommittedBlob, await WorkingBlobAsync(repo, PartU.ReaderPath), "src/Reader.cs");
        AssertRestoreBar(app, PartU.RestoreBarReader);
        calls = await RedoAsync(app, repo, PartU.CreateKeepText);
        Shot(app, "u16t-two-redone");
        AssertTransaction(app, calls, PartU.Redo.Message(PartU.CreateKeepText), [PartU.CreateLine("refs/heads/keep", PartU.ReadWholeLinesSha)]);
        Assert.Equal(PartU.ReadWholeLinesSha, await RevParseAsync(repo, "refs/heads/keep"));
        Assert.Equal([PartU.Redo.Message(PartU.CreateKeepText)], await ReflogAsync(repo, "refs/heads/keep", 10));
        Assert.Equal(["## main...origin/main", "?? docs/todo.md"], await StatusAsync(repo));
        Assert.Equal([PartU.BackupRefName("discard", 1)], (await BackupsAsync(repo)).Select(backup => backup.Name));
        AssertRestoreBar(app, PartU.RestoreBarReader);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CreateKeepText));
    }

    // ---- U16's own helpers ----

    /// <summary>"History" with docs/todo.md "# To do\n" written outside the app, untracked (UD2), before the app starts.</summary>
    private static async Task<TempRepo> HistoryWithTodoAsync()
    {
        var repo = await Scenarios.HistoryAsync();
        repo.WriteFile(PartU.TodoPath, PartU.Todo);
        return repo;
    }

    /// <summary>
    /// Step 10: the pointer rests on the unstaged src/Reader.cs and clicks its Discard; phase 2's
    /// dialog; "Discard"; then waits until the restore bar shows and the tab is idle.
    /// </summary>
    private static async Task DiscardReaderAsync(ShellDriver app)
    {
        await ClickRowButtonAsync(app, "UnstagedFileList", PartU.ReaderPath, "RowDiscardButton");
        await AssertDiscardQuestionAsync(app, PartU.DiscardReaderQuestion);
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => Repo(app).Changes.RestoreText == PartU.RestoreBarReader && !FileRows(app, "UnstagedFileList").Contains("M Reader.cs src"), "the discard to end");
        await app.WaitForIdleAsync();
    }

    /// <summary>Phase 2's discard dialog on top: "Discard changes?" with <paramref name="question"/>.</summary>
    private static async Task AssertDiscardQuestionAsync(ShellDriver app, string question)
    {
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        var view = app.TopDialogView!;
        Assert.Equal(PartU.DiscardTitle, ((TextBlock)app.FindByAutomationId(view, "ConfirmTitle")).Text);
        Assert.Equal(question, ((TextBlock)app.FindByAutomationId(view, "ConfirmQuestion")).Text);
    }

    /// <summary>A question on top as the report writes it: its title, question, detail and the confirm button's label.</summary>
    private static void AssertQuestion(ShellDriver app, string title, string question, string detail, string confirm)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No question is open.");
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId(view, "ConfirmTitle")).Text);
        Assert.Equal(question, ((TextBlock)app.FindByAutomationId(view, "ConfirmQuestion")).Text);
        var detailBlock = (TextBlock)app.FindByAutomationId(view, "ConfirmDetail");
        Assert.True(detailBlock.IsEffectivelyVisible, "The question shows no detail.");
        Assert.Equal(detail, detailBlock.Text);
        Assert.Equal(confirm, ((Button)app.FindByAutomationId(view, "ConfirmButton")).Content);
    }

    /// <summary>
    /// Step 26: "Read whole lines" (display row 1, (468, 155)) right-clicked in its Message
    /// column, "Create branch here…", <c>keep</c> typed, "Check out the new branch" unticked,
    /// "Create branch"; then waits until keep is in the graph and the tab is idle.
    /// </summary>
    private static async Task CreateKeepAsync(ShellDriver app)
    {
        RightClickRowAndChoose(app, 0, new Point(468, 155), "Create branch here…");
        await app.WaitForDialogAsync<NameDialogViewModel>();
        app.TypeInto("NameBox", "keep");
        var option = (CheckBox)app.FindByAutomationId(app.TopDialogView!, "NameDialogOption");
        app.Click(option);
        Assert.False(option.IsChecked, "\"Check out the new branch\" is still ticked.");
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await app.WaitForAsync(() => Repo(app).Graph.Refs.Refs.Any(gitRef => gitRef.FullName == "refs/heads/keep"), "keep in the graph");
        await app.WaitForIdleAsync();
        await AssertNoDialogAsync(app);
    }

    /// <summary>
    /// A discard snapshot as the conventions assert it: its parent (Read whole lines), its author
    /// (VisualCommit), its subject and the paths of its body, and the blobs it holds. Returns its id.
    /// </summary>
    private static async Task<string> AssertDiscardSnapshotAsync(TempRepo repo, string reference, string[] paths, params (string Path, string Blob)[] files)
    {
        var sha = await RevParseAsync(repo, reference) ?? throw new Xunit.Sdk.XunitException($"{reference} does not exist.");
        var fields = (await GitTextAsync(repo, "log", "-1", "--format=%P%x1f%an <%ae>%x1f%s%x1f%b", sha)).Split('\x1f');
        Assert.Equal(PartU.ReadWholeLinesSha, fields[0]);
        Assert.Equal(PartU.SnapshotAuthor, fields[1]);
        Assert.Equal(PartU.DiscardSnapshotSubject(paths.Length), fields[2]);
        Assert.Equal(paths, fields[3].TrimEnd('\n').Split('\n'));
        foreach (var (path, blob) in files)
        {
            AssertShort(blob, await RevParseAsync(repo, $"{sha}:{path}"), $"the snapshot's {path}");
        }

        return sha;
    }

    /// <summary>Steps 16 and 19: the working-changes row, then the unstaged src/Reader.cs: its diff opens over the graph with both hunks.</summary>
    private static async Task OpenReaderDiffAsync(ShellDriver app)
    {
        await ClickWorkingRowAsync(app);
        app.Click(await app.FileRowAsync("UnstagedFileList", PartU.ReaderPath));
        await app.WaitForAsync(() => Repo(app).IsDiffOpen && HunkHeaders(app).Count == 2, "src/Reader.cs's diff with its two hunks");
        await app.WaitForIdleAsync();
    }

    private static DiffTextView DiffText(ShellDriver app) => app.Find<DiffTextView>("DiffText");

    /// <summary>The open diff's hunk headers, each up to its second "@@" (git's range part); none while no diff is laid out.</summary>
    private static List<string> HunkHeaders(ShellDriver app)
    {
        app.Settle();
        var view = app.Window.GetVisualDescendants().OfType<DiffTextView>().FirstOrDefault(control => control.Name == "DiffText" && control.IsEffectivelyVisible);
        return view is null ? [] : [.. view.Rows.Where(row => row.Kind == DiffRowKind.HunkHeader).Select(row => RangeOf(row.HeaderText ?? string.Empty))];
    }

    private static string RangeOf(string header)
    {
        var end = header.IndexOf("@@", 2, StringComparison.Ordinal);
        return end < 0 ? header : header[..(end + 2)];
    }

    /// <summary>The open diff's added lines, their texts without indentation.</summary>
    private static List<string> AddedLines(ShellDriver app) =>
        [.. DiffText(app).Rows.Select(row => row.Left).OfType<DiffCell>().Where(cell => cell.Kind == Core.Diff.DiffLineKind.Added).Select(cell => cell.Text.Trim())];

    /// <summary>The open diff's added line numbered <paramref name="number"/> in the new file.</summary>
    private static DiffCell AddedLine(ShellDriver app, int number) =>
        DiffText(app).Rows.Select(row => row.Left).OfType<DiffCell>().Single(cell => cell.Kind == Core.Diff.DiffLineKind.Added && cell.NewNumber == number);

    /// <summary>The diff after line 23's discard: both hunks, the second with the one added empty line 22.</summary>
    private static void AssertWithoutLine23(ShellDriver app)
    {
        Assert.Equal([PartU.FirstHunkHeader, PartU.SecondHunkWithoutLine23], HunkHeaders(app));
        var added = DiffText(app).Rows.Where(row => row.Hunk == 1).Select(row => row.Left).OfType<DiffCell>().Where(cell => cell.Kind == Core.Diff.DiffLineKind.Added).ToList();
        var line = Assert.Single(added);
        Assert.Equal((22, string.Empty), (line.NewNumber, line.Text));
    }

    /// <summary>The visible buttons of the open diff with this automation id, in order (the hunks' buttons).</summary>
    private static List<Button> DiffButtons(ShellDriver app, string automationId) =>
        [.. DiffText(app).GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetAutomationId(button) == automationId)];
}
