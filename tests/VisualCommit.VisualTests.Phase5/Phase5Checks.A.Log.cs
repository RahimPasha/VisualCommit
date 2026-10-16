// Check A2 of part A (docs/test-reports/phase-5.md): the activity log on "conflicts" with old and
// many backups, the panel 320 high from the session. Two test methods on fresh copies (AD7): the
// clean-up's notes, a refused checkout with its failure's summary and its error, "Show reads", and
// Esc with a page open; then the merge's stop and its output, the merge tool running and
// cancelled, and the abort with its backup's input and its clean-up note.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.Views.Pages;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksA
{
    /// <summary>
    /// Check A2, method 1 (steps 1 to 6): the log opened from the status bar, the clean-up's notes
    /// and its transaction's input, a refused checkout with its summary and git's error, the reads
    /// under "Show reads", and Esc in the log with the backups list open closing only the log.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A2_activity_log()
    {
        using var repo = await ConflictsWithBackupsDueAsync(waitingMergeTool: false);
        using var data = new TempDirectory("data");
        using var app = await OpenWithBackupsDueAsync(data, repo, PartA.A2Height);
        await AssertA2StartAsync(app, repo);
        var palette = PaletteOf(app);

        // Step 1: the log opened under the graph area, 320 high, with the clean-up's one row.
        app.ClickActivityLogToggle();
        app.Settle();
        var shot = Shot(app, PartA.A2LogOpen);
        var log = AssertPanelAt(app, PartA.A2Height, shot);
        AssertHeader(app, log, PartA.ConflictsRepo, showReads: false);
        var row = Assert.Single(log.Rows);
        AssertOperationRow(app, row, PartA.IconCheck, PartA.CleanUpTitle, PartA.CleanUpMessage, palette.TextSecondary, open: false);
        AssertNear(new Rect(PartA.GraphArea.Left, log.ListBounds.Top, PartA.GraphArea.Width, PartA.OperationRowHeight), row.Bounds!.Value, "The clean-up's row");
        AssertToggle(app, enabled: true, selected: true, shot);
        Assert.Equal(WindowLayout.RowsTop, app.BoundsOf(Graph(app)).Top, 0.5);

        // Step 2: the clean-up opened: its three notes and exactly its two calls; the transaction's input.
        ClickRow(app, PartA.CleanUpTitle);
        ClickRow(app, PartA.UpdateRefStdin);
        shot = Shot(app, PartA.A2CleanUpOpened);
        log = app.ActivityLog();
        var cleanUp = Record(app, PartA.CleanUpTitle);
        Assert.True(
            (cleanUp.IsBackground, cleanUp.Kind, cleanUp.Outcome) == (true, OperationKind.Local, OperationOutcome.Succeeded),
            $"The clean-up's record: background {cleanUp.IsBackground}, {cleanUp.Kind}, {cleanUp.Outcome}.");
        Assert.Equal(PartA.CleanUpNotes, cleanUp.Notes);
        Assert.Equal(7, log.Rows.Count);

        // The box's wrapped input has scrolled the clean-up's row out of the list's view (AD15).
        AssertOperationRow(app, log.Rows[0], PartA.IconCheck, PartA.CleanUpTitle, PartA.CleanUpMessage, palette.TextSecondary, open: true, inView: false);
        for (var note = 0; note < 3; note++)
        {
            AssertLineRow(app, log.Rows[1 + note], ActivityLogRowKind.Note, PartA.CleanUpNotes[note], inView: null);
        }

        AssertCallRow(app, log.Rows[4], PartA.Exit0, PartA.BackupsForEachRef, open: false);
        AssertCallRow(app, log.Rows[5], PartA.Exit0, PartA.UpdateRefStdin, open: true);
        AssertOutputBox(log.Rows[6], 2, (PartA.InputPart, PartA.CleanUpInput));
        Assert.Equal([PartA.BackupsForEachRef, PartA.UpdateRefStdin], AssertOperationCalls(app, log, PartA.CleanUpTitle).Select(call => call.CommandText));
        AssertListAtEnd(app);

        // Step 3: the clean-up closed, its call first (the box's wrapped input scrolled the clean-up's
        // row out of the list's view: the report's "Changed in this report after its first
        // commit"); kinds checked out by a double-click, which git refuses.
        ClickRow(app, PartA.UpdateRefStdin);
        ClickRow(app, PartA.CleanUpTitle);
        app.Click(await app.RefRowAsync("Local branches", "feature"));
        await app.WaitForAsync(() => app.RefRowModel("Local branches", "feature").IsOpen, "the folder feature to open");
        var calls = app.GitCalls().Count;
        app.DoubleClick(await app.RefRowAsync("Local branches", "feature", "kinds"));
        await app.WaitForDialogAsync<VisualCommit.App.ViewModels.Dialogs.ErrorDialogViewModel>();
        await app.WaitForIdleAsync();
        Assert.Equal(PartA.KindsEditsRow, Graph(app).SelectedIndex);
        Assert.True(((RefItemRow)app.RefRowModel("Local branches", "feature", "kinds")).IsSelected, "kinds is not selected in the left panel.");
        var switchCall = app.GitCalls().Skip(calls).Single(call => call.CommandText == PartA.CheckoutCommand);
        Assert.Equal(1, switchCall.ExitCode);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartA.CheckoutBlocked,
            PartA.CheckoutSummary,
            PartA.ConflictsRepo,
            PartA.CheckoutCommand,
            [.. PartA.CheckoutButtons],
            [.. PartA.CheckoutError],
            PartA.CheckoutError[0]));
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await AssertNoDialogAsync(app);
        AssertStatus(app, PartA.CheckoutFailed, OperationStatusTone.Danger);
        await AssertGitAtA2StartAsync(repo);
        Assert.Equal(UndoRedoButtons.Nothing, app.UndoRedoState());
        Assert.Equal(PartA.CheckoutSummary, Record(app, PartA.CheckoutTitle).FailureSummary);
        Assert.Equal(OperationOutcome.Failed, Record(app, PartA.CheckoutTitle).Outcome);
        if (await IsReportGitAsync())
        {
            Assert.Equal(PartA.CheckoutError, switchCall.StandardError.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n'));
            Assert.Equal(string.Empty, switchCall.StandardOutput);
        }

        // The checkout's row opened, its call not yet: the row and its summary are in the list's
        // view, and asserted as drawn (the report's "Changed in this report after its first commit").
        ClickRow(app, PartA.CheckoutTitle);
        log = app.ActivityLog();
        var checkout = IndexOf(log, PartA.CheckoutTitle);
        Assert.Equal(1, checkout);
        AssertOperationRow(app, log.Rows[0], PartA.IconCheck, PartA.CleanUpTitle, PartA.CleanUpMessage, palette.TextSecondary, open: false);
        AssertOperationRow(app, log.Rows[checkout], PartA.IconClose, PartA.CheckoutTitle, PartA.CheckoutFailed, palette.Danger, open: true);
        AssertLineRow(app, log.Rows[checkout + 1], ActivityLogRowKind.FailureSummary, PartA.CheckoutSummary);

        ClickRow(app, PartA.CheckoutCommand);
        shot = Shot(app, PartA.A2CheckoutFailedOpened);
        log = app.ActivityLog();
        Assert.Equal(PartA.CleanUpTitle, log.Rows[0].AutomationName);
        AssertOperationRow(app, log.Rows[0], PartA.IconCheck, PartA.CleanUpTitle, PartA.CleanUpMessage, palette.TextSecondary, open: false, inView: false);
        Assert.Equal(checkout, IndexOf(log, PartA.CheckoutTitle));

        // The switch's box has scrolled the checkout's row and its summary out of the list's view (AD15).
        AssertOperationRow(app, log.Rows[checkout], PartA.IconClose, PartA.CheckoutTitle, PartA.CheckoutFailed, palette.Danger, open: true, inView: false);
        var under = Under(log, checkout);
        AssertLineRow(app, under[0], ActivityLogRowKind.FailureSummary, PartA.CheckoutSummary, inView: false);
        Assert.DoesNotContain(under, line => line.Kind == ActivityLogRowKind.Note);
        var checkoutCalls = AssertOperationCalls(app, log, PartA.CheckoutTitle);
        Assert.Contains(PartA.CheckoutCommand, checkoutCalls.Select(call => call.CommandText));
        var switchRow = under.Single(line => line.Kind == ActivityLogRowKind.Call && line.AutomationName == PartA.CheckoutCommand);
        AssertCallRow(app, switchRow, PartA.Exit1, PartA.CheckoutCommand, open: true);
        var box = under[under.IndexOf(switchRow) + 1];
        AssertOutputBox(box, 2, (PartA.ErrorPart, ErrorLines(switchCall)));
        if (await IsReportGitAsync())
        {
            AssertOutputBox(box, 2, (PartA.ErrorPart, PartA.CheckoutError));
        }

        AssertListAtEnd(app);

        // Step 4: Show reads: the worktree's reads at the top level among the operations, in start order.
        var key = await WorktreeKeyAsync(repo);
        var workingTree = await WorkingTreeAsync(repo);
        app.Click(app.FindByAutomationId("ActivityLogShowReads"));
        ActivityLogSnapshot? withReads = null;
        await app.WaitForAsync(
            () => ShowsTheWorktreesReads(app, withReads = app.ActivityLog(), key, workingTree),
            "the log to show the worktree's reads and operations in start order");
        AssertShowReads(app, withReads!);
        Shot(app, PartA.A2ShowReads);
        app.Click(app.FindByAutomationId("ActivityLogShowReads"));
        app.Settle();
        log = app.ActivityLog();
        Assert.False(log.ShowReads, "Show reads is still checked.");
        Assert.Equal([PartA.CleanUpTitle, PartA.CheckoutTitle], TopLevel(log).Select(top => top.AutomationName));
        Assert.True(log.Rows[IndexOf(log, PartA.CheckoutTitle)].IsOpen, "The checkout's row closed when the reads were hidden.");
        Assert.False(log.Rows[0].IsOpen, "The clean-up's row opened when the reads were hidden.");

        // Step 5: the backups list from the Local branches section's menu; the checkout's row closed.
        var rightPanel = TextsIn(app.FindByAutomationId("RightPanel"));
        var operations = app.Session.Operations.Snapshot().Count;
        app.RightClick(await app.RefRowAsync("Local branches"));
        app.Menu();
        app.ClickMenuItem(PartA.ShowBackups);
        await app.WaitForPageAsync<ReflogView>();
        await app.WaitForAsync(() => app.ReflogSnapshot().Backups.Count == PartA.BackupsAfterOpening.Count, "the backups list to list its rows");

        // The switch's call closed first: its open box kept the checkout's row out of the list's
        // view (the report's "Changed in this report after its first commit").
        ClickRow(app, PartA.CheckoutCommand);
        ClickRow(app, PartA.CheckoutTitle);
        shot = Shot(app, PartA.A2PageAndLog);
        var page = AssertBackupsList(app, PartA.A2Height);
        log = AssertPanelAt(app, PartA.A2Height, shot);
        Assert.Equal([PartA.CleanUpTitle, PartA.CheckoutTitle], log.Rows.Select(top => top.AutomationName));
        AssertOperationRow(app, log.Rows[0], PartA.IconCheck, PartA.CleanUpTitle, PartA.CleanUpMessage, palette.TextSecondary, open: false);
        AssertOperationRow(app, log.Rows[1], PartA.IconClose, PartA.CheckoutTitle, PartA.CheckoutFailed, palette.Danger, open: false);
        Assert.True(app.ActivityLogView.HasFocusWithin, "The keyboard focus is not in the activity log.");
        Assert.Equal(rightPanel, TextsIn(app.FindByAutomationId("RightPanel")));
        Assert.Equal(operations, app.Session.Operations.Snapshot().Count);
        Assert.False(page.ListHasFocus, "The backups list has the keyboard focus.");

        // Step 6: the first Esc closes only the log; the second the page.
        app.PressKey(Key.Escape);
        app.Settle();
        shot = Shot(app, PartA.A2LogClosedPageKept);
        Assert.False(app.ActivityLogModel.IsShown, "The activity log is still open.");
        AssertNoPanel(app);
        AssertToggle(app, enabled: true, selected: false, shot);
        AssertBackupsList(app, logHeight: null);
        Assert.False(app.ActivityLogView.HasFocusWithin, "The keyboard focus is still in the hidden log.");
        var focused = app.Window.FocusManager?.GetFocusedElement();
        Assert.False(focused is TextBox, "The keyboard focus is in a text box.");
        Assert.True(focused is Visual visual && app.ShownPage is { } shown && (ReferenceEquals(visual, shown) || shown.IsVisualAncestorOf(visual)), $"The keyboard focus is on {focused?.GetType().Name ?? "nothing"}, not on the page.");
        AssertNoClippedText(app);
        app.PressKey(Key.Escape);
        app.Settle();
        Assert.Null(app.ShownPage);
        AssertGraph(app, PartA.ConflictsGraph, PartA.ConflictsLanes, PartA.Working, showsDate: false);
        Assert.Equal(PartA.KindsEditsRow, Graph(app).SelectedIndex);
        await app.WaitForAsync(() => TextsIn(app.FindByAutomationId("RightPanel")).SequenceEqual(rightPanel), "the right panel as it was before the page opened");
        AssertNoClippedText(app);
        Assert.Equal(operations, app.Session.Operations.Snapshot().Count);
    }

    /// <summary>
    /// Check A2, method 2 (steps 7 to 11): the merge's stop and its call's output, the merge tool
    /// running and then cancelled, the abort with its note, and its backup's transaction's input.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_A2_activity_log_stop_cancel_abort()
    {
        using var repo = await ConflictsWithBackupsDueAsync(waitingMergeTool: true);
        try
        {
            using var data = new TempDirectory("data");
            using var app = await OpenWithBackupsDueAsync(data, repo, PartA.A2Height);
            await AssertA2StartAsync(app, repo);
            Assert.Equal(Scenarios.WaitingMergeTool, await GitTextAsync(repo, "config", "--get", "merge.tool"));
            await StopCancelAbortAsync(app, repo);
        }
        finally
        {
            Scenarios.ReleaseWaitingMergeTool(repo);
        }
    }

    private static async Task StopCancelAbortAsync(ShellDriver app, TempRepo repo)
    {
        var palette = PaletteOf(app);

        // Step 7: the merge of feature/theirs stops; its row opened to its calls and the merge call's output.
        app.ClickActivityLogToggle();
        app.Click(await app.RefRowAsync("Local branches", "feature"));
        await app.WaitForAsync(() => app.RefRowModel("Local branches", "feature").IsOpen, "the folder feature to open");
        app.RightClick(await app.RefRowAsync("Local branches", "feature", "theirs"));
        app.Menu();
        var calls = app.GitCalls().Count;
        using (var texts = app.RecordStatusTexts())
        {
            app.ClickMenuItem(PartA.MergeMenuItem);
            await app.WaitForIdleAsync();
            Assert.Contains(PartA.MergeRunning, texts.Texts);
        }

        AssertStatus(app, PartA.MergeStopped, OperationStatusTone.Warning);
        AssertBanner(app, PartA.MergeBanner, PartA.AbortMerge, BannerButton.ContinueDisabled(PartA.ContinueReason), placement: BannerButtonPlacement.OwnRow);
        AssertGraph(app, PartA.ConflictsGraph, PartA.ConflictsLanes, PartA.WorkingOneConflict, showsDate: false);
        Assert.Equal(new UndoRedoButtons(false, PartA.FinishMerge, false, PartA.FinishMerge), app.UndoRedoState());
        await AssertMergeStopAsync(repo);
        var merge = Record(app, PartA.MergeTitle);
        Assert.Equal(OperationOutcome.Conflicts, merge.Outcome);
        Assert.Null(merge.FailureSummary);
        var mergeCall = app.GitCalls().Skip(calls).Single(call => call.CommandText == PartA.MergeCommand);
        Assert.Equal(1, mergeCall.ExitCode);
        Assert.Equal(string.Empty, mergeCall.StandardError);
        var log = app.ActivityLog();
        Assert.Equal([PartA.CleanUpTitle, PartA.MergeTitle], log.Rows.Select(top => top.AutomationName));
        AssertOperationRow(app, log.Rows[1], PartA.IconAlert, PartA.MergeTitle, PartA.MergeStopped, palette.Warning, open: false);

        ClickRow(app, PartA.MergeTitle);
        ClickRow(app, PartA.MergeCommand);
        var shot = Shot(app, PartA.A2MergeOutputOpened);
        log = AssertPanelAt(app, PartA.A2Height, shot);
        AssertOperationRow(app, log.Rows[0], PartA.IconCheck, PartA.CleanUpTitle, PartA.CleanUpMessage, palette.TextSecondary, open: false, inView: null);
        AssertOperationRow(app, log.Rows[1], PartA.IconAlert, PartA.MergeTitle, PartA.MergeStopped, palette.Warning, open: true, inView: null);
        var under = Under(log, 1);
        Assert.DoesNotContain(under, line => line.Kind is ActivityLogRowKind.FailureSummary or ActivityLogRowKind.Note);
        var mergeCalls = AssertOperationCalls(app, log, PartA.MergeTitle);
        Assert.Contains(PartA.MergeCommand, mergeCalls.Select(call => call.CommandText));
        var mergeRow = under.Single(line => line.Kind == ActivityLogRowKind.Call && line.AutomationName == PartA.MergeCommand);
        AssertCallRow(app, mergeRow, PartA.Exit1, PartA.MergeCommand, open: true);
        var box = under[under.IndexOf(mergeRow) + 1];
        if (await IsReportGitAsync())
        {
            AssertOutputBox(box, 2, (PartA.OutputPart, PartA.MergeOutput));
        }
        else
        {
            AssertOutputBox(box, 2, (PartA.OutputPart, OutputLines(mergeCall)));
            Assert.Contains(PartA.MergeKeyLine, box.Parts![0].Text.Split('\n'));
        }

        Assert.True(mergeRow.IsInView && box.IsInView, "The merge call's row or its box is not whole in the list's view.");
        AssertListAtEnd(app);

        // The merge's row closed with the keys of 2.2: its many calls keep it out of the list's
        // view (the report's "Changed in this report after its first commit"). The call's own
        // click closes its box; Left moves the focus to the merge's row, which comes into view;
        // Left again closes it.
        ClickRow(app, PartA.MergeCommand);
        app.PressKey(Key.Left);
        app.Settle();
        Assert.True(app.ActivityLogRow(PartA.MergeTitle).IsInView, "Left did not bring the merge's row into view.");
        app.PressKey(Key.Left);
        app.Settle();
        log = app.ActivityLog();
        Assert.Equal(2, log.Rows.Count);
        Assert.All(log.Rows, top => Assert.False(top.IsOpen, $"The row \"{top.AutomationName}\" is open."));

        // Step 8: the merge tool on notes.txt runs, waiting; the status bar offers Cancel.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, PartA.UnstagedWithConflict, "Staged files (0)");
        await app.WaitForIdleAsync();
        AssertWorkingRowSelected(app);
        Assert.Equal(PartA.UnstagedAtStop, FileRows(app, "UnstagedFileList"));
        Assert.Equal(PartA.MergeMessage, BoxText(app, "CommitSummary"));
        var notesRow = await app.FileRowAsync("UnstagedFileList", PartA.NotesPath);
        var letter = notesRow.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("status"));
        Assert.Equal("U", letter.Text);
        Assert.True(palette.Danger == (letter.Foreground as ISolidColorBrush)?.Color, $"The U of notes.txt is drawn in {(letter.Foreground as ISolidColorBrush)?.Color}, expected {palette.Danger}.");
        app.RightClick(notesRow);
        app.Menu();
        app.ClickMenuItem(PartA.OpenInMergeTool);
        await app.WaitForAsync(
            () => Scenarios.WaitingMergeToolStarted(repo) && app.FindByAutomationId("OperationCancelButton").IsEffectivelyVisible,
            "the merge tool to start and the status bar to show Cancel");
        app.MoveToNeutral();
        await app.WaitForAsync(() => app.ActivityLog().Rows.Any(line => line.AutomationName == PartA.MergeToolTitle), "the merge tool's running row");
        shot = Shot(app, PartA.A2MergeToolRunning);
        Assert.Equal(PartA.MergeToolRunning, ((TextBlock)app.FindByAutomationId("OperationStatus")).Text);
        Assert.True(app.FindByAutomationId("OperationProgress") is ProgressBar { IsEffectivelyVisible: true, IsIndeterminate: true }, "The status bar shows no indeterminate progress bar.");
        var cancel = (Button)app.FindByAutomationId("OperationCancelButton");
        Assert.Equal(PartA.CancelToolTip, ToolTip.GetTip(cancel) as string);
        Assert.NotNull(app.BannerBounds);
        AssertWorkingRowSelected(app);
        Assert.Equal(new UndoRedoButtons(false, PartA.WaitForMergeTool, false, PartA.WaitForMergeTool), app.UndoRedoState());
        log = app.ActivityLog();
        Assert.Equal([PartA.CleanUpTitle, PartA.MergeTitle, PartA.MergeToolTitle], log.Rows.Select(top => top.AutomationName));
        Assert.False(log.Rows[0].IsOpen || log.Rows[1].IsOpen, "The first rows are open.");
        AssertOperationRow(app, log.Rows[2], null, PartA.MergeToolTitle, PartA.Running, palette.TextSecondary, open: false);

        // Step 9: Cancel stops it within the bound; the tool is released; its row opened to its calls.
        var notesAtStop = await File.ReadAllBytesAsync(Path.Combine(repo.Path, PartA.NotesPath));
        app.Click(cancel);
        await app.WaitForIdleAsync(timeout: PartA.CancelBound);
        Scenarios.ReleaseWaitingMergeTool(repo);
        await AssertNoDialogAsync(app);
        AssertStatus(app, PartA.MergeToolCancelled);
        Assert.False(app.FindByAutomationId("OperationCancelButton").IsEffectivelyVisible, "Cancel is still shown.");
        Assert.Equal(new UndoRedoButtons(false, PartA.FinishMerge, false, PartA.FinishMerge), app.UndoRedoState());
        Assert.Equal(["UU notes.txt", " M other.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(notesAtStop, await File.ReadAllBytesAsync(Path.Combine(repo.Path, PartA.NotesPath)));
        Assert.Empty(Directory.EnumerateFiles(repo.Path, "notes_*"));
        Assert.Equal(PartA.TheirsEditsSha, await RevParseAsync(repo, "MERGE_HEAD"));
        Assert.Equal(OperationOutcome.Cancelled, Record(app, PartA.MergeToolTitle).Outcome);
        ClickRow(app, PartA.MergeToolTitle);
        shot = Shot(app, PartA.A2MergeToolCancelled);
        log = AssertPanelAt(app, PartA.A2Height, shot);
        var tool = IndexOf(log, PartA.MergeToolTitle);
        AssertOperationRow(app, log.Rows[tool], PartA.IconMinus, PartA.MergeToolTitle, PartA.MergeToolCancelled, palette.TextSecondary, open: true);
        under = Under(log, tool);
        Assert.Equal(3, under.Count);
        AssertCallRow(app, under[0], PartA.Exit0, PartA.LsFilesNotes, open: false);
        AssertCallRow(app, under[1], PartA.Cancelled, PartA.MergeToolCommand, open: false);
        AssertCallRow(app, under[2], PartA.Exit0, PartA.LsFilesNotes, open: false);
        Assert.Equal([PartA.LsFilesNotes, PartA.MergeToolCommand, PartA.LsFilesNotes], AssertOperationCalls(app, log, PartA.MergeToolTitle).Select(call => call.CommandText));
        AssertListAtEnd(app);

        // Step 10: the abort, after its question; its row opened: its note and its calls.
        ClickRow(app, PartA.MergeToolTitle);
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await AssertConfirmationAsync(app, PartA.AbortQuestionTitle, PartA.AbortQuestion, PartA.AbortDetail, PartA.AbortMerge);
        using (var texts = app.RecordStatusTexts())
        {
            app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
            await app.WaitForIdleAsync();
            Assert.Contains(PartA.AbortRunning, texts.Texts);
        }

        await AssertNoDialogAsync(app);
        AssertStatus(app, PartA.Aborted);
        Assert.Null(app.BannerBounds);
        AssertGraph(app, PartA.ConflictsGraph, PartA.ConflictsLanes, PartA.Working, showsDate: false);
        Assert.Equal(UndoRedoButtons.Nothing, app.UndoRedoState());
        var snapshot = await AssertAbortedAsync(repo);
        ClickRow(app, PartA.AbortTitle);
        shot = Shot(app, PartA.A2AbortOpened);
        log = AssertPanelAt(app, PartA.A2Height, shot);
        Assert.Equal([PartA.CleanUpTitle, PartA.MergeTitle, PartA.MergeToolTitle, PartA.AbortTitle], TopLevel(log).Select(top => top.AutomationName));
        Assert.All(TopLevel(log).Take(3), top => Assert.False(top.IsOpen, $"The row \"{top.AutomationName}\" is open."));
        var abort = IndexOf(log, PartA.AbortTitle);

        // The row lies just above the list's top, its note the first row in view (AD15).
        AssertOperationRow(app, log.Rows[abort], PartA.IconCheck, PartA.AbortTitle, PartA.Aborted, palette.TextSecondary, open: true, inView: false);
        under = Under(log, abort);
        Assert.DoesNotContain(under, line => line.Kind == ActivityLogRowKind.FailureSummary);
        Assert.Equal(ActivityLogRowKind.Note, under[0].Kind);
        AssertLineRow(app, under[0], ActivityLogRowKind.Note, PartA.AbortNote);
        Assert.Equal(under[0].Index, log.Rows.First(line => line.IsInView).Index);
        Assert.Single(under, line => line.Kind == ActivityLogRowKind.Note);
        Assert.Equal([PartA.AbortNote], Record(app, PartA.AbortTitle).Notes);
        var abortCalls = AssertOperationCalls(app, log, PartA.AbortTitle);
        AssertInOrder([.. abortCalls.Select(call => call.CommandText)], PartA.AbortCalls, "The abort's calls");
        var callRows = under.Where(line => line.Kind == ActivityLogRowKind.Call).ToList();
        for (var index = 0; index < PartA.AbortCalls.Count; index++)
        {
            var command = PartA.AbortCalls[index];
            var occurrence = PartA.AbortCalls.Take(index).Count(earlier => earlier == command);
            var callRow = callRows.Where(line => line.AutomationName == command).ElementAt(occurrence);
            AssertCallRow(app, callRow, PartA.Exit0, command, open: false, onTemporaryIndex: PartA.AbortCallsOnTemporaryIndex[index]);
        }

        Assert.Equal(PartA.AbortCalls.Count(call => call == PartA.UpdateRefStdin), callRows.Count(line => line.AutomationName == PartA.UpdateRefStdin));
        AssertListAtEnd(app);
        Assert.True(under[^1].IsInView, "The abort's last call is not in the list's view.");

        // Step 11: the backup's transaction (the first update-ref shown, the clean-up's row being closed) opened to its input.
        ClickRow(app, PartA.UpdateRefStdin, 0);
        shot = Shot(app, PartA.A2AbortBackupInput);
        log = AssertPanelAt(app, PartA.A2Height, shot);
        under = Under(log, IndexOf(log, PartA.AbortTitle));
        var backupRow = under.Where(line => line.Kind == ActivityLogRowKind.Call && line.AutomationName == PartA.UpdateRefStdin).First();
        Assert.True(under.IndexOf(backupRow) < under.FindIndex(line => line.AutomationName == "git merge --abort"), "The opened update-ref is not the one before git merge --abort.");
        AssertCallRow(app, backupRow, PartA.Exit0, PartA.UpdateRefStdin, open: true);
        var input = under[under.IndexOf(backupRow) + 1];
        AssertOutputBox(input, 2, (PartA.InputPart, PartA.AbortBackupInput(snapshot)));
        Assert.True(backupRow.IsInView && input.IsInView, "The backup's call or its box is not whole in the list's view.");
        AssertListAtEnd(app);
        AssertNoClippedText(app);
    }

    /// <summary>
    /// A fresh copy of "conflicts" with old and many backups (AD7), and with the waiting merge tool
    /// configured when <paramref name="waitingMergeTool"/>, then other.txt changed.
    /// </summary>
    private static async Task<TempRepo> ConflictsWithBackupsDueAsync(bool waitingMergeTool)
    {
        var repo = await Scenarios.ConflictsAsync();
        try
        {
            await Scenarios.AddOldAndManyBackupsAsync(repo);
            if (waitingMergeTool)
            {
                await Scenarios.ConfigureWaitingMergeToolAsync(repo);
            }

            repo.WriteFile("other.txt", PartA.OtherChanged);
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// "At the start (both methods)": the graph with "Working changes", nothing selected, Ready,
    /// main; no dialog; Undo and Redo with nothing to act on (the clean-up gives no reason), Search
    /// enabled; git with its 50 backups and other.txt changed; the log file's clean-up lines.
    /// </summary>
    private static async Task AssertA2StartAsync(ShellDriver app, TempRepo repo)
    {
        AssertGraph(app, PartA.ConflictsGraph, PartA.ConflictsLanes, PartA.Working, showsDate: false);
        AssertNothingSelected(app);
        AssertStatus(app, PartA.Ready);
        AssertBranch(app, "main");
        await AssertNoDialogAsync(app);
        Assert.Equal(UndoRedoButtons.Nothing, app.UndoRedoState());
        Assert.True(ToolbarButton(app, "SearchButton").IsEffectivelyEnabled, "Search is disabled.");
        await AssertGitAtA2StartAsync(repo);
        var lines = app.LogLines().Where(line => line.Contains("Removed backup ", StringComparison.Ordinal)).ToList();
        Assert.Equal(PartA.CleanUpNotes.Count, lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            Assert.Contains(PartA.CleanUpNotes[index], lines[index], StringComparison.Ordinal);
        }

        Assert.False(app.ActivityLogView.IsEffectivelyVisible, "The activity log is open at start.");
    }

    /// <summary>Git as A2 starts and as the refused checkout leaves it: main at Ours edits, other.txt changed, the 50 backups left by the clean-up, each of Ours edits.</summary>
    private static async Task AssertGitAtA2StartAsync(TempRepo repo)
    {
        Assert.Equal(["## main", " M other.txt"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal(PartA.OursEditsSha, await RevParseAsync(repo, "refs/heads/main"));
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(PartA.BackupsAfterOpening, backups.Select(entry => entry.Name[BackupRef.Prefix.Length..]));
        Assert.All(backups, entry => Assert.Equal(PartA.OursEditsSha, entry.Sha));
        Assert.Equal(PartA.OtherChanged, WorkingFile(repo, "other.txt"));
    }

    /// <summary>"A2's merge stop": notes.txt unmerged with its three stages, other.txt carried, MERGE_HEAD and MERGE_MSG, the markers naming the full ref.</summary>
    private static async Task AssertMergeStopAsync(TempRepo repo)
    {
        Assert.Equal(["UU notes.txt", " M other.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        var stages = (await GitLinesAsync(repo, "ls-files", "--unmerged", "--", PartA.NotesPath)).Select(line => line.Split(' ', '\t')).Select(parts => (parts[2], parts[1][..7])).ToList();
        Assert.Equal([("1", "624b469"), ("2", "439eb6b"), ("3", "6216606")], stages);
        Assert.Equal(PartA.TheirsEditsSha, await RevParseAsync(repo, "MERGE_HEAD"));
        Assert.Equal("Merge branch 'feature/theirs'\n\n# Conflicts:\n#\tnotes.txt\n", File.ReadAllText(Path.Combine(repo.Path, ".git", "MERGE_MSG")).Replace("\r\n", "\n", StringComparison.Ordinal));
        var notes = WorkingFile(repo, PartA.NotesPath).Split('\n');
        Assert.Equal(">>>>>>> refs/heads/feature/theirs", notes[5]);
        Assert.Equal(">>>>>>> refs/heads/feature/theirs", notes[17]);
        Assert.Equal(PartA.OtherChanged, WorkingFile(repo, "other.txt"));
    }

    /// <summary>
    /// "A2's backups" after the abort: no merge in progress, other.txt still changed, main still at
    /// Ours edits, HEAD's newest reflog entry the abort's reset; the abort's backup and its snapshot
    /// (parent Ours edits, the expected tree, by VisualCommit), and 50 backups in all, the clean-up
    /// having removed reset-20251220T090002000Z. Returns the snapshot's id.
    /// </summary>
    private static async Task<string> AssertAbortedAsync(TempRepo repo)
    {
        Assert.Equal(["## main", " M other.txt"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal(PartA.OtherChanged, WorkingFile(repo, "other.txt"));
        Assert.Null(await RevParseAsync(repo, "MERGE_HEAD"));
        Assert.Equal(PartA.OursEditsSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["reset: moving to HEAD"], await ReflogAsync(repo, "HEAD", 1));
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(51, backups.Count);
        Assert.Equal(PartA.BackupsAfterAbort.Count, backups.Select(entry => BackupRef.Parse(entry.Name).Stem).Distinct().Count());
        Assert.Equal(PartA.OursEditsSha, await RevParseAsync(repo, BackupRef.Prefix + PartA.AbortBackup));
        var snapshot = await RevParseAsync(repo, BackupRef.Prefix + PartA.AbortBackup + "-changes")
            ?? throw new Xunit.Sdk.XunitException("The abort saved no -changes snapshot.");
        Assert.Equal(PartA.OursEditsSha, await GitTextAsync(repo, "log", "-1", "--format=%P", snapshot));
        Assert.Equal(PartA.AbortSnapshotTree, await GitTextAsync(repo, "log", "-1", "--format=%T", snapshot));
        Assert.Equal("VisualCommit <snapshot@visualcommit.invalid>", await GitTextAsync(repo, "log", "-1", "--format=%an <%ae>", snapshot));
        Assert.Null(await RevParseAsync(repo, BackupRef.Prefix + PartA.BackupsAfterOpening[0]));
        Assert.Equal(PartA.BackupsAfterAbort.Order(StringComparer.Ordinal), backups.Select(entry => BackupRef.Parse(entry.Name).Stem).Distinct().Order(StringComparer.Ordinal));
        return snapshot;
    }

    /// <summary>
    /// The backups list (5.3, 12.3) in the graph area, its source <c>Backups</c>, its 50 rows newest
    /// first; down to the panel's splitter while the log is open at <paramref name="logHeight"/>,
    /// else to the status bar.
    /// </summary>
    private static ReflogSnapshot AssertBackupsList(ShellDriver app, double? logHeight)
    {
        var page = app.ReflogSnapshot();
        Assert.Equal(PartA.BackupsSource, page.SourceLabel);
        Assert.Equal(PartA.GraphArea.Top, page.Bounds.Top, PartA.Tolerance);
        Assert.Equal(PartA.GraphArea.Left, page.Bounds.Left, PartA.Tolerance);
        Assert.Equal(PartA.GraphArea.Right, page.Bounds.Right, PartA.Tolerance);
        Assert.Equal(PartA.GraphArea.Bottom - (logHeight is { } height ? height + PartA.SplitterHeight : 0), page.Bounds.Bottom, PartA.Tolerance);
        Assert.Equal(PartA.BackupsAfterOpening.Reverse(), page.Backups.Select(backup => backup.Name));
        var first = page.Backups[0];
        Assert.Equal((PartA.NewestBackup, PartA.NewestBackupTime, PartA.BeforeAReset, PartA.OursEditsShort, PartA.OursEditsSubject), (first.Name, first.Time, first.Words, first.ShortSha, first.Subject));
        Assert.Equal(PartA.OldestBackup, page.Backups[^1].Name);
        return page;
    }

    /// <summary>
    /// "Show reads" (A2 step 4, asserted by structure) on a snapshot whose top level was found to
    /// be, in start order, every operation of this worktree and every read the call log has for it
    /// (2.3), each once (<see cref="ShowsTheWorktreesReads"/>): the opening's <c>rev-parse</c> comes
    /// first (AD11 as ruled), the read that found the backups due before the clean-up; <c>git
    /// --version</c> is not shown; every call at the top level is a read, no operation's call; the
    /// operations keep their order and their open or closed state.
    /// </summary>
    private static void AssertShowReads(ShellDriver app, ActivityLogSnapshot log)
    {
        Assert.True(log.ShowReads, "Show reads is not checked.");
        var top = TopLevel(log);
        Assert.Equal(PartA.OpeningRevParse, top[0].AutomationName);
        Assert.True(top[0].Kind == ActivityLogRowKind.Call, "The first row is not a read.");
        var cleanUp = top.FindIndex(row => row.AutomationName == PartA.CleanUpTitle);
        Assert.True(top.Take(cleanUp).Any(row => row.AutomationName == PartA.BackupsForEachRef), "The read of the backups is not shown before the clean-up.");
        Assert.DoesNotContain(top, row => row.AutomationName == PartA.GitVersionCall);
        Assert.All(
            top.Where(row => row.Kind == ActivityLogRowKind.Call),
            row => Assert.True(((ActivityLogCallRow)ModelRow(app, row)).IsRead, $"The top-level call \"{row.AutomationName}\" belongs to an operation."));
        Assert.True(
            log.Rows.Where(row => row.Kind == ActivityLogRowKind.Operation).Select(row => (row.AutomationName, row.IsOpen)).SequenceEqual([(PartA.CleanUpTitle, false), (PartA.CheckoutTitle, true)]),
            "The operations' rows changed with Show reads.");
    }

    /// <summary>Whether a snapshot's top level is what <see cref="ExpectedTopLevel"/> gives now: the reads still coming settle first.</summary>
    private static bool ShowsTheWorktreesReads(ShellDriver app, ActivityLogSnapshot log, string key, string workingTree) =>
        TopLevel(log).Select(row => row.AutomationName).SequenceEqual(ExpectedTopLevel(app, key, workingTree).Select(entry => entry.Name));

    /// <summary>
    /// The top level under "Show reads", from the logs and not from the panel: this worktree's
    /// operations (their key) and the reads (no operation id) made in its working tree or a folder
    /// under it, in the order of their sequence numbers.
    /// </summary>
    private static List<(long Sequence, string Name)> ExpectedTopLevel(ShellDriver app, string key, string workingTree)
    {
        var comparison = WorktreeKey.Comparer.Equals("a", "A") ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        bool InWorkingTree(string? folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return false;
            }

            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            return string.Equals(full, workingTree, comparison)
                || (full.StartsWith(workingTree + Path.DirectorySeparatorChar, comparison) || full.StartsWith(workingTree + Path.AltDirectorySeparatorChar, comparison));
        }

        var operations = app.Session.Operations.Snapshot()
            .Where(record => WorktreeKey.Comparer.Equals(record.RepositoryKey, key))
            .Select(record => (record.Sequence, Name: record.Title));
        var reads = app.GitCalls()
            .Where(call => call.OperationId is null && InWorkingTree(call.WorkingDirectory))
            .Select(call => (call.Sequence, Name: call.CommandText));
        return [.. operations.Concat(reads).OrderBy(entry => entry.Sequence)];
    }

    /// <summary>A call's standard error as the box shows it: its lines without the last line break.</summary>
    private static IReadOnlyList<string> ErrorLines(GitCallRecord call) => call.StandardError.TrimEnd('\r', '\n').Split('\n');

    /// <summary>A call's standard output as the box shows it: its lines without the last line break.</summary>
    private static IReadOnlyList<string> OutputLines(GitCallRecord call) => call.StandardOutput.TrimEnd('\r', '\n').Split('\n');
}
