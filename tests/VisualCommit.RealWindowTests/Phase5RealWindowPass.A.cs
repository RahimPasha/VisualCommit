// Part A's real-window steps (docs/test-reports/phase-5.md, part A, the "Real window" column of A1
// to A4): A1 steps 1, 2, 4, 6 and 7; A2 every step of both methods (captures after steps 1, 2, 3,
// 5, 6, 7, 9, 10 and 11; steps 4 and 8 vary by nature); A3 steps 1 to 6 with a real drag and a real
// restart of the process; A4 method 1, steps 1 to 5, with real keys. Each launch on a fresh copy at
// 1100×700, dark. The toolbar's and the toggle's states are read as IsEnabled and HelpText through
// UI Automation (their tooltips are the scripted run's pictures); the activity log's rows are found
// by automation id and name, its boxes read through their Value pattern.
using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase5RealWindowPass
{
    /// <summary>
    /// Check A1, steps 1, 2, 4, 6 and 7, with the real mouse and keyboard: a new empty tab by a real
    /// click on <c>+</c> (only Theme enabled, no help texts, the toggle disabled;
    /// <c>a1a-empty-tab</c>), back to "history" (Search and the toggle enabled, Undo and Redo with
    /// nothing to act on; <c>a1b-repository-open</c>), Stage all and a commit typed key by key (Undo
    /// enabled at once with <c>Undo commit "Describe the lines"</c>; <c>a1e-committed</c>), then the
    /// empty tab and "history" again (the toolbar follows the active tab and keeps its undo history).
    /// The tooltips of steps 3 and 5 are the scripted run's: the pass reads the help texts instead.
    /// </summary>
    [Fact]
    public async Task Check_A1_toolbar_and_status_bar()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var app = Launch(repo);
        var name = NameOf(repo);
        Assert.Equal(PartA.HistoryRepo, name);

        // Step 1: a second, empty tab.
        app.ClickWithMouse(app.Find("AddRepoButton"));
        WaitForTabs(app, PartA.HistoryRepo, PartA.NewTab);
        app.WaitForText("CurrentBranch", PartA.NoRepository, StepTimeout);
        AssertEmptyTab(app);
        Compare(app, PartA.A1EmptyTab);

        // Step 2: back to "history": Search enabled, Undo and Redo with nothing to act on.
        app.ClickWithMouse(Tab(app, PartA.HistoryRepo));
        WaitForBranch(app, "main");
        AssertToolbar(app, PartA.OpenToolbar(commandIsMeta: false));
        AssertToggle(app, enabled: true, open: false);
        WaitForStatus(app, PartA.Ready);
        WaitForNothingSelected(app);
        Assert.Null(app.ActivityLogBounds());
        Compare(app, PartA.A1RepositoryOpen);

        // Step 4: Stage all and a commit from the stage panel, the summary typed key by key.
        ClickWorkingRow(app, PartA.HistoryLanes, "Unstaged files (1)", "Staged files (0)");
        var stage = ExpectOperation(app, name, "Stage");
        app.ClickWithMouse(app.Find("StageAllButton"));
        stage.Wait(OperationOutcome.Succeeded);
        app.WaitForStageTitles("Unstaged files (0)", "Staged files (1)", StepTimeout);
        TypeInto(app, "CommitSummary", PartA.CommitSummary);
        var commit = ExpectOperation(app, name, "Commit");
        app.ClickWithMouse(app.Find("CommitButton"));
        commit.Wait(OperationOutcome.Succeeded);

        // The toolbar at once: the states follow the queue's Changed after the commit's end.
        AssertToolbar(app, PartA.CommittedToolbar(commandIsMeta: false));
        WaitForDetails(app, PartA.CommitSummary, PartA.DescribeTheLinesSha);
        WaitForStatus(app, PartA.Ready);
        WaitForBranch(app, "main");
        AssertToggle(app, enabled: true, open: false);
        AssertNoDialog(app);
        Compare(app, PartA.A1Committed);
        Assert.Equal(PartA.DescribeTheLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal([PartA.CommitReflog], await ReflogAsync(repo, "HEAD", 1));
        await AssertCommitAsync(repo, PartA.DescribeTheLinesSha, AppCommitTime, AppCommitTime, PartA.ReadWholeLinesSha);

        // Step 6: the empty tab: the toolbar acts on the active tab.
        app.ClickWithMouse(Tab(app, PartA.NewTab));
        app.WaitForText("CurrentBranch", PartA.NoRepository, StepTimeout);
        AssertToolbar(app, PartA.EmptyTabToolbar);
        AssertToggle(app, enabled: false, open: false);

        // Step 7: back to "history": the undo history was kept (7.1).
        app.ClickWithMouse(Tab(app, PartA.HistoryRepo));
        WaitForBranch(app, "main");
        AssertToolbar(app, PartA.CommittedToolbar(commandIsMeta: false));
        WaitForDetails(app, PartA.CommitSummary, PartA.DescribeTheLinesSha);
        AssertToggle(app, enabled: true, open: false);

        Assert.Equal(0, app.Close());
        Assert.Equal(PartA.DescribeTheLinesSha, await RevParseAsync(repo, "refs/heads/main"));
    }

    /// <summary>
    /// Check A2, method 1 (steps 1 to 6), with the real mouse and keyboard on "conflicts" with old
    /// and many backups, the panel 320 high from the session: the log opened from the status bar
    /// (<c>a2a-log-open</c>), the clean-up's notes and its transaction's input (<c>a2b</c>), a
    /// checkout refused by git and its error (<c>a2c</c>), Show reads ticked and unticked, the
    /// backups list under the open log with the focus in the log (<c>a2e</c>), then two real Esc
    /// presses: the first closes only the log (<c>a2f</c>), the second the page.
    /// </summary>
    [Fact]
    public async Task Check_A2_activity_log()
    {
        using var repo = await ConflictsWithBackupsDueAsync(waitingMergeTool: false);
        using var app = Launch(repo, PartA.A2Height);
        var name = NameOf(repo);
        Assert.Equal(PartA.ConflictsRepo, name);
        WaitForCleanUp(app, name, PartA.CleanUpMessage);
        await AssertA2StartAsync(app, repo);

        // Step 1: the log opened under the graph area, 320 high, with the clean-up's one row.
        app.ClickWithMouse(app.Find("ActivityLogToggle"));
        WaitForPanelAt(app, PartA.A2Height);
        AssertToggle(app, enabled: true, open: true);
        WaitForLogRowsInView(app, PartA.CleanUpTitle);
        Assert.Equal("ActivityLogOperation", Assert.Single(app.ActivityLogRows()).Kind);
        AssertLogHeader(app, PartA.ConflictsRepo, showReads: false);
        NoteLogRows(app, PartA.A2LogOpen);
        Compare(app, PartA.A2LogOpen);

        // Step 2: the clean-up opened: its notes and its two calls; the transaction's input.
        app.ClickActivityLogRow(PartA.CleanUpTitle);
        app.WaitForActivityLogRow(PartA.UpdateRefStdin);
        app.ClickActivityLogRow(PartA.UpdateRefStdin);
        app.WaitForActivityLogOutput(Box(PartA.InputPart, PartA.CleanUpInput));
        AssertMadeInOrder(app, "ActivityLogLine", PartA.CleanUpNotes);
        AssertMadeInOrder(app, "ActivityLogCall", [PartA.BackupsForEachRef, PartA.UpdateRefStdin]);
        NoteLogRows(app, PartA.A2CleanUpOpened);
        Compare(app, PartA.A2CleanUpOpened);

        // Step 3: the call and the clean-up closed; kinds checked out by a real double-click, which git refuses.
        app.ClickActivityLogRow(PartA.UpdateRefStdin);
        app.WaitFor(() => app.ActivityLogOutputs().Count == 0, "the update-ref's box to close", StepTimeout);
        app.ClickActivityLogRow(PartA.CleanUpTitle);
        WaitForLogRowsInView(app, PartA.CleanUpTitle);
        app.ClickWithMouse(app.RefRow("Local branches", "feature"));
        app.WaitFor(() => app.HasRefRow("Local branches", "feature", "kinds"), "the folder feature to open", StepTimeout);
        var checkout = ExpectOperation(app, name, PartA.CheckoutTitle);
        app.DoubleClickWithMouse(app.RefRow("Local branches", "feature", "kinds"));
        checkout.Wait(OperationOutcome.Failed);
        await AssertErrorDialogAsync(app, PartA.CheckoutBlocked, PartA.CheckoutSummary, PartA.CheckoutCommand, PartA.CheckoutButtons[1], PartA.CheckoutError[0], PartA.CheckoutError);
        WaitForDetails(app, "Kinds edits");
        CloseErrorDialog(app);
        WaitForStatus(app, PartA.CheckoutFailed);
        AssertUndoRedo(app, false, PartA.NothingToUndo, false, PartA.NothingToRedo);
        await AssertGitAtA2StartAsync(repo);

        // The checkout's row opened: its failure's summary under it; then its switch's call opened to git's error.
        app.ClickActivityLogRow(PartA.CheckoutTitle);
        app.WaitFor(
            () => app.ActivityLogRows().Any(row => row.Kind == "ActivityLogLine" && row.Name == PartA.CheckoutSummary && row.InView),
            "the checkout's failure summary under its row",
            StepTimeout);
        app.ClickActivityLogRow(PartA.CheckoutCommand);
        app.WaitFor(
            () => app.ActivityLogOutputs().Any(box => box.StartsWith(PartA.ErrorPart + "\n", StringComparison.Ordinal) && box.Split('\n').Contains(PartA.CheckoutError[0])),
            "the switch's box with git's error",
            StepTimeout);
        if (await IsReportGitAsync())
        {
            Assert.Contains(Box(PartA.ErrorPart, PartA.CheckoutError), app.ActivityLogOutputs());
        }

        NoteLogRows(app, PartA.A2CheckoutFailedOpened);
        Compare(app, PartA.A2CheckoutFailedOpened);

        // Step 4: Show reads ticked: the reads join the rows; unticked: the rows as before (not pictured: they vary by nature).
        var before = app.ActivityLogNamesInView();
        var showReads = app.Find("ActivityLogShowReads").AsCheckBox();
        Assert.Equal(PartA.ShowReads, showReads.Name);
        app.ClickWithMouse(showReads);
        app.WaitFor(() => app.Find("ActivityLogShowReads").AsCheckBox().IsChecked == true, "Show reads to be ticked", StepTimeout);
        app.WaitFor(() => !app.ActivityLogNamesInView().SequenceEqual(before), "the reads to join the log's rows", StepTimeout);
        Run.Note($"a2d (not captured): the activity log's rows in view with Show reads: {string.Join(" | ", app.ActivityLogNamesInView())}");
        app.ClickWithMouse(app.Find("ActivityLogShowReads"));
        app.WaitFor(() => app.Find("ActivityLogShowReads").AsCheckBox().IsChecked == false, "Show reads to be unticked", StepTimeout);
        WaitForLogRowsInView(app, [.. before]);

        // Step 5: the backups list from the Local branches section's menu; the switch's call and the checkout's row closed.
        app.RightClickWithMouse(app.RefRow("Local branches"));
        app.ClickMenuItem(PartA.ShowBackups);
        app.WaitForPage("Reflog", PartA.BackupsSource, StepTimeout);
        app.WaitFor(() => app.PageRowNames("Reflog", "BackupRow").FirstOrDefault() == PartA.NewestBackup, "the backups list to list its rows", StepTimeout);
        app.ClickActivityLogRow(PartA.CheckoutCommand);
        app.WaitFor(() => app.ActivityLogOutputs().Count == 0, "the switch's box to close", StepTimeout);
        app.ClickActivityLogRow(PartA.CheckoutTitle);
        WaitForLogRowsInView(app, PartA.CleanUpTitle, PartA.CheckoutTitle);
        app.WaitFor(() => RealApp.HasKeyboardFocus(app.WaitForActivityLogRow(PartA.CheckoutTitle).Element), "the keyboard focus on the checkout's row in the log", StepTimeout);
        AssertPageAt(app, "Reflog", bottom: PageBottom(PartA.A2Height));
        WaitForPanelAt(app, PartA.A2Height);
        WaitForDetails(app, "Kinds edits");
        NoteLogRows(app, PartA.A2PageAndLog);
        Compare(app, PartA.A2PageAndLog);

        // Step 6: the first real Esc closes only the log, the second the page.
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitForActivityLog(shown: false, StepTimeout);
        AssertToggle(app, enabled: true, open: false);
        AssertPageAt(app, "Reflog", bottom: PageBottom(null));
        Assert.Equal(PartA.NewestBackup, app.PageRowNames("Reflog", "BackupRow").First());
        Compare(app, PartA.A2LogClosedPageKept);
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => !app.IsAnyPageShown, "the page to close", StepTimeout);
        WaitForDetails(app, "Kinds edits");

        Assert.Equal(0, app.Close());
        await AssertGitAtA2StartAsync(repo);
    }

    /// <summary>
    /// Check A2, method 2 (steps 7 to 11), with the real mouse and keyboard: the merge's stop and its
    /// call's output (<c>a2g</c>), the call closed and the merge's row closed by two real Left
    /// presses, the merge tool running and a real click on Cancel (<c>a2i</c>), the abort through
    /// the banner and its question (<c>a2j</c>), and its backup's transaction's input (<c>a2k</c>).
    /// </summary>
    [Fact]
    public async Task Check_A2_activity_log_stop_cancel_abort()
    {
        using var repo = await ConflictsWithBackupsDueAsync(waitingMergeTool: true);
        try
        {
            await StopCancelAbortAsync(repo);
        }
        finally
        {
            Scenarios.ReleaseWaitingMergeTool(repo);
        }
    }

    private async Task StopCancelAbortAsync(TempRepo repo)
    {
        using var app = Launch(repo, PartA.A2Height);
        var name = NameOf(repo);
        WaitForCleanUp(app, name, PartA.CleanUpMessage);
        await AssertA2StartAsync(app, repo);
        Assert.Equal(Scenarios.WaitingMergeTool, await GitTextAsync(repo, "config", "--get", "merge.tool"));

        // Step 7: the merge of feature/theirs stops; its row opened to its calls and the merge call's output.
        app.ClickWithMouse(app.Find("ActivityLogToggle"));
        WaitForPanelAt(app, PartA.A2Height);
        app.ClickWithMouse(app.RefRow("Local branches", "feature"));
        app.WaitFor(() => app.HasRefRow("Local branches", "feature", "theirs"), "the folder feature to open", StepTimeout);
        app.RightClickWithMouse(app.RefRow("Local branches", "feature", "theirs"));
        var merge = ExpectOperation(app, name, PartA.MergeTitle);
        app.ClickMenuItem(PartA.MergeMenuItem);
        merge.Wait(OperationOutcome.Conflicts, PartA.MergeStopped);
        WaitForStatus(app, PartA.MergeStopped);
        app.WaitForText("BannerText", PartA.MergeBanner, StepTimeout);
        AssertUndoRedo(app, false, PartA.FinishMerge, false, PartA.FinishMerge);
        await AssertMergeStopAsync(repo);
        WaitForLogRowsInView(app, PartA.CleanUpTitle, PartA.MergeTitle);

        app.ClickActivityLogRow(PartA.MergeTitle);
        app.ClickActivityLogRow(PartA.MergeCommand);
        if (await IsReportGitAsync())
        {
            app.WaitForActivityLogOutput(Box(PartA.OutputPart, PartA.MergeOutput));
        }
        else
        {
            app.WaitFor(
                () => app.ActivityLogOutputs().Any(box => box.StartsWith(PartA.OutputPart + "\n", StringComparison.Ordinal) && box.Split('\n').Contains(PartA.MergeKeyLine)),
                "the merge call's box with git's output",
                StepTimeout);
        }

        Assert.True(app.WaitForActivityLogRow(PartA.MergeCommand).InView, "The merge call's row is not whole in the list's view.");
        NoteLogRows(app, PartA.A2MergeOutputOpened);
        Compare(app, PartA.A2MergeOutputOpened);

        // The call's own click closes its box; a real Left moves the focus to the merge's row, a second closes it.
        app.ClickActivityLogRow(PartA.MergeCommand);
        app.WaitFor(() => app.ActivityLogOutputs().Count == 0, "the merge call's box to close", StepTimeout);
        app.PressKeys(VirtualKeyShort.LEFT);
        app.WaitFor(() => RealApp.HasKeyboardFocus(app.WaitForActivityLogRow(PartA.MergeTitle).Element), "Left to bring the merge's row into view with the focus", StepTimeout);
        app.PressKeys(VirtualKeyShort.LEFT);
        WaitForLogRowsInView(app, PartA.CleanUpTitle, PartA.MergeTitle);

        // Step 8 (not pictured: the progress bar varies): the merge tool on notes.txt runs; the status bar offers Cancel.
        ClickWorkingRow(app, PartA.ConflictsLanes, PartA.UnstagedWithConflict, "Staged files (0)");
        app.RightClickWithMouse(app.StageFileRow("UnstagedFileList", PartA.NotesPath, StepTimeout));
        app.ClickMenuItem(PartA.OpenInMergeTool);
        app.WaitFor(
            () => Scenarios.WaitingMergeToolStarted(repo) && app.ShownElements("OperationCancelButton").Count == 1,
            "the merge tool to start and the status bar to show Cancel",
            OperationTimeout);
        WaitForStatus(app, PartA.MergeToolRunning);
        AssertUndoRedo(app, false, PartA.WaitForMergeTool, false, PartA.WaitForMergeTool);
        WaitForLogRowsInView(app, PartA.CleanUpTitle, PartA.MergeTitle, PartA.MergeToolTitle);

        // Step 9: a real click on Cancel stops it within the bound; the tool is released; its row opened to its calls.
        var notesAtStop = await File.ReadAllBytesAsync(Path.Combine(repo.Path, PartA.NotesPath));
        var tool = ExpectOperation(app, name, PartA.MergeToolTitle);
        app.MoveToNeutral();
        app.ClickWithMouse(app.Find("OperationCancelButton"));
        tool.Wait(OperationOutcome.Cancelled, PartA.MergeToolCancelled, PartA.CancelBound);
        Scenarios.ReleaseWaitingMergeTool(repo);
        WaitForStatus(app, PartA.MergeToolCancelled);
        app.WaitUntilGone("OperationCancelButton", StepTimeout);
        AssertNoDialog(app);
        AssertUndoRedo(app, false, PartA.FinishMerge, false, PartA.FinishMerge);
        Assert.Equal(["UU notes.txt", " M other.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(notesAtStop, await File.ReadAllBytesAsync(Path.Combine(repo.Path, PartA.NotesPath)));
        Assert.Empty(Directory.EnumerateFiles(repo.Path, "notes_*"));
        Assert.Equal(PartA.TheirsEditsSha, await RevParseAsync(repo, "MERGE_HEAD"));
        app.ClickActivityLogRow(PartA.MergeToolTitle);
        app.WaitFor(
            () => app.ActivityLogRows().Where(row => row.Kind == "ActivityLogCall").Select(row => row.Name).SequenceEqual([PartA.LsFilesNotes, PartA.MergeToolCommand, PartA.LsFilesNotes])
                && app.ActivityLogRows().Last().InView,
            "the merge tool's three calls under its row",
            StepTimeout);
        NoteLogRows(app, PartA.A2MergeToolCancelled);
        Compare(app, PartA.A2MergeToolCancelled);

        // Step 10: the abort through the banner and its question; its row opened: its note and its calls.
        app.ClickActivityLogRow(PartA.MergeToolTitle);
        app.WaitFor(() => !app.ActivityLogRows().Any(row => row.Kind == "ActivityLogCall"), "the merge tool's row to close", StepTimeout);
        app.ClickWithMouse(app.Find("BannerAbortButton"));
        app.WaitForText("ConfirmTitle", PartA.AbortQuestionTitle, StepTimeout);
        Assert.Equal([PartA.AbortQuestion], app.NamesOnScreen("ConfirmQuestion"));
        Assert.Equal([PartA.AbortDetail], app.NamesOnScreen("ConfirmDetail"));
        Assert.Equal([PartA.AbortMerge], app.NamesOnScreen("ConfirmButton"));
        var abort = ExpectOperation(app, name, PartA.AbortTitle);
        app.ClickWithMouse(app.Find("ConfirmButton"));
        abort.Wait(OperationOutcome.Succeeded, PartA.Aborted);
        WaitForStatus(app, PartA.Aborted);
        app.WaitFor(() => app.BannerBounds() is null, "the banner to go", StepTimeout);
        AssertNoDialog(app);
        AssertUndoRedo(app, false, PartA.NothingToUndo, false, PartA.NothingToRedo);
        var snapshot = await AssertAbortedAsync(repo);
        app.ClickActivityLogRow(PartA.AbortTitle);
        app.WaitFor(
            () => app.ActivityLogRows() is { Count: > 0 } rows
                && rows.Any(row => row.Kind == "ActivityLogLine" && row.Name == PartA.AbortNote && row.InView)
                && rows.Last().InView
                && PartA.AbortCalls.All(call => rows.Any(row => row.Kind == "ActivityLogCall" && row.Name == call)),
            "the abort's note and calls under its row, the last in view",
            StepTimeout);
        NoteLogRows(app, PartA.A2AbortOpened);
        Compare(app, PartA.A2AbortOpened);

        // Step 11: the backup's transaction (the first update-ref shown, the clean-up's row being closed) opened to its input.
        app.ClickActivityLogRow(PartA.UpdateRefStdin, 0);
        app.WaitForActivityLogOutput(Box(PartA.InputPart, PartA.AbortBackupInput(snapshot)));
        Assert.True(app.WaitForActivityLogRow(PartA.UpdateRefStdin, 0).InView, "The backup's call is not whole in the list's view.");
        NoteLogRows(app, PartA.A2AbortBackupInput);
        Compare(app, PartA.A2AbortBackupInput);

        Assert.Equal(0, app.Close());
    }

    /// <summary>
    /// Check A3, steps 1 to 6, in a real process: the panel at its default height
    /// (<c>a3a-default-height</c>), its splitter dragged 100 up with the real mouse
    /// (<c>a3b-dragged-to-300</c>), the app closed and launched again on the same data folder,
    /// writing no session (the panel closed at start, opened 300 high: <c>a3c-restarted</c>), then
    /// dragged to its maximum (<c>a3d-at-the-maximum</c>) and its minimum (<c>a3e-at-the-minimum</c>).
    /// The panel's bounds read through UI Automation (<c>ActivityLog</c>, ±2); the session read as the
    /// app reads it.
    /// </summary>
    [Fact]
    public async Task Check_A3_panel_height_across_a_real_restart()
    {
        using var repo = await Scenarios.HistoryAsync();
        var status = await GitLinesAsync(repo, "status", "--porcelain");
        var name = NameOf(repo);
        string data;

        // First start. Step 1: the panel at its default height, its list empty.
        using (var app = Launch(repo))
        {
            data = app.DataDirectory;
            app.ClickWithMouse(app.Find("ActivityLogToggle"));
            var panel = WaitForPanelAt(app, PartA.DefaultHeight);
            AssertToggle(app, enabled: true, open: true);
            AssertLogHeader(app, PartA.HistoryRepo, showReads: false);
            Assert.Empty(app.ActivityLogRows());
            Assert.Null(SavedSession(data)?.ActivityLogHeight);
            Compare(app, PartA.A3DefaultHeight);

            // Step 2: the splitter dragged with the real mouse from its centre, 100 up in 5 steps; letting go saved it.
            var splitter = SplitterCentre(panel);
            Assert.True(Math.Abs(splitter.Y - 471.5f) <= PartA.Tolerance && Math.Abs(splitter.X - 480) <= PartA.Tolerance, $"The splitter's centre is at {splitter}, not (480, 471.5).");
            app.DragWithMouse(splitter.X, splitter.Y, splitter.X, splitter.Y + (float)PartA.FirstDrag, PartA.DragSteps);
            WaitForPanelAt(app, PartA.DraggedHeight);
            WaitForSavedHeight(app, data, PartA.DraggedHeight);
            Compare(app, PartA.A3DraggedTo300);

            // Step 3: the app closes.
            Assert.Equal(0, app.Close());
        }

        Assert.Equal(PartA.DraggedHeight, SavedSession(data)!.ActivityLogHeight!.Value, PartA.Tolerance);

        // Second start, on the same data folder, writing no session. Step 4: the panel closed at start; opened, 300 high and empty.
        using (var app = Relaunch(data, name))
        {
            Assert.Null(app.ActivityLogBounds());
            AssertToggle(app, enabled: true, open: false);
            Assert.Equal([PartA.HistoryRepo], app.NamesOnScreen("RepoTab"));
            app.ClickWithMouse(app.Find("ActivityLogToggle"));
            var panel = WaitForPanelAt(app, PartA.DraggedHeight);
            AssertLogHeader(app, PartA.HistoryRepo, showReads: false);
            Assert.Empty(app.ActivityLogRows());
            Compare(app, PartA.A3Restarted);

            // Step 5: dragged 300 up, it stops at its maximum, 421.
            var splitter = SplitterCentre(panel);
            app.DragWithMouse(splitter.X, splitter.Y, splitter.X, splitter.Y + (float)PartA.DragToMaximum, PartA.DragSteps);
            panel = WaitForPanelAt(app, PartA.MaximumHeight);
            WaitForSavedHeight(app, data, PartA.MaximumHeight);
            Compare(app, PartA.A3AtTheMaximum);

            // Step 6: dragged 400 down, it stops at its minimum, 120.
            splitter = SplitterCentre(panel);
            Assert.True(Math.Abs(splitter.Y - 250.5f) <= PartA.Tolerance, $"The splitter's centre is at {splitter}, not (480, 250.5).");
            app.DragWithMouse(splitter.X, splitter.Y, splitter.X, splitter.Y + (float)PartA.DragToMinimum, PartA.DragSteps);
            WaitForPanelAt(app, PartA.MinimumHeight);
            WaitForSavedHeight(app, data, PartA.MinimumHeight);
            AssertNoDialog(app);
            Compare(app, PartA.A3AtTheMinimum);
            Assert.Equal(0, app.Close());
        }

        // Nothing ran in git.
        Assert.Equal(status, await GitLinesAsync(repo, "status", "--porcelain"));
    }

    /// <summary>
    /// Check A4, method 1, steps 1 to 5, with real keys: Ctrl+F opens the search bar with the focus in
    /// its box and Esc closes it (<c>a4a-search-by-key</c>); the summary typed key by key and its
    /// own undo pressed once per key and once more, the commit untouched
    /// (<c>a4b-summary-typing-undone</c>); Ctrl+Z with the focus in the graph undoes the commit
    /// (<c>a4c-undone-by-key</c>) and Ctrl+Y redoes it (<c>a4d-redone-by-key</c>). Step 6 and method 2
    /// are the scripted run's (the report's Real window column says why).
    /// </summary>
    [Fact]
    public async Task Check_A4_real_keys()
    {
        using var repo = await Scenarios.HistoryAsync();
        repo.WriteFile(PartA.ScratchPath, PartA.ScratchText);
        using var app = Launch(repo);
        var name = NameOf(repo);
        AssertUndoRedo(app, false, PartA.NothingToUndo, false, PartA.NothingToRedo);
        app.WaitForButtonState("SearchButton", enabled: true, PartA.SearchToolTip(commandIsMeta: false));
        Assert.Equal([" M src/Reader.cs", "?? scratch.txt"], await GitLinesAsync(repo, "status", "--porcelain"));

        // Step 1: Ctrl+F opens the search bar above the column headers, its box focused; Esc closes it.
        // The window takes the keys once a real click has made it the foreground window: the
        // neutral point, where nothing reacts to a click.
        app.ClickAt(RealApp.NeutralPoint.X, RealApp.NeutralPoint.Y);
        app.PressSearch();
        app.WaitFor(() => app.IsSearchBarShown && RealApp.HasKeyboardFocus(app.Find("SearchBox")), "the search bar with the keyboard focus in its box", StepTimeout);
        AssertSearchBarOpened(app);
        Compare(app, PartA.A4SearchByKey);
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => !app.IsSearchBarShown, "the search bar to close", StepTimeout);
        app.WaitFor(app.GraphHasKeyboardFocus, "the graph to take the keyboard focus", StepTimeout);
        Assert.Equal(WindowLayout.RowsTop, app.GraphRowsBounds().Top, 1.0);
        WaitForNothingSelected(app);

        // Step 2: src/Reader.cs alone staged with its row's Stage button, then committed; scratch.txt stays.
        ClickWorkingRow(app, PartA.HistoryLanes, "Unstaged files (2)", "Staged files (0)");
        var stage = ExpectOperation(app, name, "Stage");
        ClickFileRowButton(app, "UnstagedFileList", PartA.ReaderPath, "RowStageButton");
        stage.Wait(OperationOutcome.Succeeded);
        app.WaitForStageTitles("Unstaged files (1)", "Staged files (1)", StepTimeout);
        TypeInto(app, "CommitSummary", PartA.CommitSummary);
        var commit = ExpectOperation(app, name, "Commit");
        app.ClickWithMouse(app.Find("CommitButton"));
        commit.Wait(OperationOutcome.Succeeded);
        app.WaitForStageTitles("Unstaged files (1)", "Staged files (0)", StepTimeout);
        Assert.Equal(PartA.DescribeTheLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [ahead 1]", "?? scratch.txt"], await GitLinesAsync(repo, "status", "-sb"));
        AssertUndoRedo(app, true, PartA.UndoCommit, false, PartA.NothingToRedo);
        var undoLine = AppLogFile.OperationEnded(name, PartA.UndoCommit);

        // Step 3: the summary typed key by key; its own undo takes it back one key per press, and a press with nothing left does nothing.
        ClickWorkingRow(app, PartA.HistoryLanes, "Unstaged files (1)", "Staged files (0)");
        TypeInto(app, "CommitSummary", PartA.TypedSummary);
        var presses = 0;
        while (BoxText(app, "CommitSummary").Length > 0 && presses < 8)
        {
            var length = BoxText(app, "CommitSummary").Length;
            app.PressUndo();
            app.WaitFor(() => BoxText(app, "CommitSummary").Length < length, "the summary's own undo to take a key back", RealApp.DefaultWaitTimeout);
            presses++;
            await AssertCommitNotUndoneAsync(app, repo, undoLine);
        }

        Assert.Equal(string.Empty, BoxText(app, "CommitSummary"));
        Assert.Equal(PartA.TypedSummary.Length, presses);
        app.PressUndo();
        Assert.Equal(string.Empty, BoxText(app, "CommitSummary"));
        await AssertCommitNotUndoneAsync(app, repo, undoLine);
        ClickWorkingRow(app, PartA.HistoryLanes, "Unstaged files (1)", "Staged files (0)");
        app.WaitFor(app.GraphHasKeyboardFocus, "the graph to have the keyboard focus", StepTimeout);
        Compare(app, PartA.A4SummaryTypingUndone);

        // Step 4: Ctrl+Z with the focus in the graph undoes the commit.
        var undo = ExpectOperation(app, name, PartA.UndoCommit);
        app.PressGitUndo();
        undo.Wait(OperationOutcome.Succeeded, PartA.UndidCommit);
        WaitForStatus(app, PartA.UndidCommit);
        AssertNoDialog(app);
        await AssertUndoneAsync(repo);
        app.WaitForStageTitles("Unstaged files (1)", "Staged files (1)", StepTimeout);
        app.WaitFor(() => BoxText(app, "CommitSummary") == PartA.CommitSummary, "the message back in the summary", StepTimeout);
        app.WaitForText("CommitButton", PartA.CommitOneFile, StepTimeout);
        AssertUndoRedo(app, false, PartA.NothingToUndo, true, PartA.RedoCommit);
        Compare(app, PartA.A4UndoneByKey);

        // Step 5: Ctrl+Y redoes it, and takes the message back out of the summary.
        var redo = ExpectOperation(app, name, PartA.RedoCommit);
        app.PressGitRedo();
        redo.Wait(OperationOutcome.Succeeded, PartA.RedidCommit);
        WaitForStatus(app, PartA.RedidCommit);
        AssertNoDialog(app);
        await AssertRedoneAsync(repo);
        Assert.Equal([PartA.RedoReflog, PartA.UndoReflog, PartA.CommitReflog], await ReflogAsync(repo, "HEAD", 3));
        app.WaitForStageTitles("Unstaged files (1)", "Staged files (0)", StepTimeout);
        app.WaitFor(() => BoxText(app, "CommitSummary").Length == 0, "the summary empty again", StepTimeout);
        AssertUndoRedo(app, true, PartA.UndoCommit, false, PartA.NothingToRedo);
        Compare(app, PartA.A4RedoneByKey);

        Assert.Equal(0, app.Close());
    }

    // ---- Part A's helpers ----

    /// <summary>The tab strip's tab titled <paramref name="title"/> (<c>RepoTab</c> named by its title).</summary>
    private static AutomationElement Tab(RealApp app, string title) =>
        app.ShownElements("RepoTab").Where(tab => tab.Name == title).ToList() switch
        {
            [var only] => only,
            var found => throw new InvalidOperationException($"{found.Count} tabs are titled \"{title}\"; the tabs: {string.Join(" | ", app.NamesOnScreen("RepoTab"))}."),
        };

    /// <summary>Waits until the tab strip shows these tabs, left to right.</summary>
    private static void WaitForTabs(RealApp app, params string[] titles) =>
        app.WaitFor(
            () => app.ShownElements("RepoTab").OrderBy(tab => tab.BoundingRectangle.Left).Select(tab => tab.Name).SequenceEqual(titles),
            $"the tabs {string.Join(", ", titles)}",
            StepTimeout);

    /// <summary>
    /// The empty tab (A1 step 1, phase 0's shell): the welcome page's texts, the toolbar with only
    /// Theme enabled and no help texts, the status bar's <c>No repository</c> and <c>Ready</c>, the
    /// toggle disabled, no activity log.
    /// </summary>
    private static void AssertEmptyTab(RealApp app)
    {
        var texts = app.Texts();
        Assert.All(PartA.WelcomeTexts.Where(text => text is not ("Open" or "Clone" or "Init")), text => Assert.Contains(text, texts));
        Assert.Equal(["Open", "Clone", "Init"], new[] { "OpenRepoButton", "CloneRepoButton", "InitRepoButton" }.Select(id => app.Find(id).Name));
        AssertToolbar(app, PartA.EmptyTabToolbar);
        AssertToggle(app, enabled: false, open: false);
        WaitForStatus(app, PartA.Ready);
        Assert.Null(app.ActivityLogBounds());
    }

    /// <summary>The panel open at <paramref name="height"/> (±2), its bottom on the status bar's top (y 674): x 260 to 700 (AD9).</summary>
    private static System.Drawing.RectangleF WaitForPanelAt(RealApp app, double height)
    {
        var expected = PartA.PanelAt(height);
        var panel = app.WaitForActivityLogAt((float)expected.Top, (float)expected.Bottom, (float)PartA.Tolerance, StepTimeout);
        Assert.True(
            Math.Abs(panel.Left - expected.Left) <= PartA.Tolerance && Math.Abs(panel.Right - expected.Right) <= PartA.Tolerance,
            $"The activity log is at {panel}; the report says x {expected.Left} to {expected.Right} (±{PartA.Tolerance}).");
        return panel;
    }

    /// <summary>The splitter's centre above an open panel: the 5 px over it (2.1), halfway across.</summary>
    private static System.Drawing.PointF SplitterCentre(System.Drawing.RectangleF panel) =>
        new(panel.Left + (panel.Width / 2), panel.Top - ((float)PartA.SplitterHeight / 2));

    /// <summary>The bottom of a page in the graph area: above the log's splitter while the log is open at <paramref name="logHeight"/>, else on the status bar's top.</summary>
    private static double PageBottom(double? logHeight) => PartA.GraphArea.Bottom - (logHeight is { } height ? height + PartA.SplitterHeight : 0);

    /// <summary>Waits until the page <paramref name="pageId"/> fills the graph area's width from its top (y 88) down to <paramref name="bottom"/> (±2).</summary>
    private static void AssertPageAt(RealApp app, string pageId, double bottom) =>
        app.WaitFor(
            () => app.ShownElements(pageId) is [var page] && app.BoundsOf(page) is var bounds
                && Math.Abs(bounds.Left - PartA.GraphArea.Left) <= PartA.Tolerance && Math.Abs(bounds.Right - PartA.GraphArea.Right) <= PartA.Tolerance
                && Math.Abs(bounds.Top - PartA.GraphArea.Top) <= PartA.Tolerance && Math.Abs(bounds.Bottom - bottom) <= PartA.Tolerance,
            string.Create(CultureInfo.InvariantCulture, $"the page {pageId} at x {PartA.GraphArea.Left} to {PartA.GraphArea.Right}, y {PartA.GraphArea.Top} to {bottom}"),
            StepTimeout);

    /// <summary>Waits until the session the app saved holds the panel's height <paramref name="height"/> (±2): letting go of the splitter saved it.</summary>
    private static void WaitForSavedHeight(RealApp app, string data, double height) =>
        app.WaitFor(
            () => SavedSession(data)?.ActivityLogHeight is { } saved && Math.Abs(saved - height) <= PartA.Tolerance,
            string.Create(CultureInfo.InvariantCulture, $"the session's activityLogHeight {height} (±{PartA.Tolerance})"),
            StepTimeout);

    /// <summary>
    /// The panel's header (2.2) as UI Automation reads it: <c>Activity log</c> and the repository's
    /// name among its texts, <c>Show reads</c> ticked or not, the close button.
    /// </summary>
    private static void AssertLogHeader(RealApp app, string repository, bool showReads)
    {
        var panel = app.ShownElements(RealApp.ActivityLogId).Single();
        var texts = panel.FindAllDescendants(condition => condition.ByControlType(ControlType.Text)).Select(text => text.Name).ToList();
        Assert.Contains(PartA.LogTitle, texts);
        Assert.Contains(repository, texts);
        Assert.Equal(showReads, app.Find("ActivityLogShowReads").AsCheckBox().IsChecked == true);
        Assert.Single(app.ShownElements("ActivityLogClose"));
    }

    /// <summary>Waits until the activity log's rows in the list's view are exactly <paramref name="names"/>, top to bottom.</summary>
    private static void WaitForLogRowsInView(RealApp app, params string[] names) =>
        app.WaitFor(
            () => app.ActivityLogNamesInView().SequenceEqual(names),
            $"the activity log's rows in view to be {string.Join(", ", names)} (they are {string.Join(", ", app.ActivityLogNamesInView())})",
            StepTimeout);

    /// <summary>Asserts that the rows of kind <paramref name="kind"/> the list has made are <paramref name="names"/>, in this order.</summary>
    private static void AssertMadeInOrder(RealApp app, string kind, IReadOnlyList<string> names) =>
        Assert.Equal(names, app.ActivityLogRows().Where(row => row.Kind == kind).Select(row => row.Name));

    /// <summary>An output box's text as UI Automation reads it (its Value): one titled part, its lines.</summary>
    private static string Box(string title, IEnumerable<string> lines) => title + "\n" + string.Join('\n', lines);

    /// <summary>
    /// The search bar as Ctrl+F opens it (A4 step 1, 3.1), as UI Automation tells it: x 260 to 700
    /// and y 88 to 124 (±2); the mode <c>Commits</c>, the box empty with the keyboard focus, no
    /// count, previous and next disabled with their help texts, close with its; the graph's rows 36
    /// lower; Search still enabled.
    /// </summary>
    private static void AssertSearchBarOpened(RealApp app)
    {
        var bar = app.BoundsOf(app.ShownElements("SearchBar").Single());
        Assert.True(
            Math.Abs(bar.Left - PartA.GraphArea.Left) <= PartA.Tolerance && Math.Abs(bar.Top - PartA.GraphArea.Top) <= PartA.Tolerance
                && Math.Abs(bar.Width - PartA.GraphArea.Width) <= PartA.Tolerance && Math.Abs(bar.Height - PartA.SearchBarHeight) <= PartA.Tolerance,
            $"The search bar is at {bar}; the report says (260, 88, 440, 36) ±2.");
        Assert.Equal(PartA.SearchMode, app.Find("SearchModeButton").Name);
        Assert.Equal(string.Empty, BoxText(app, "SearchBox"));
        Assert.Equal(string.Empty, app.SearchCountText());
        var previous = app.Find("SearchPrevious");
        var next = app.Find("SearchNext");
        Assert.False(previous.IsEnabled, "Previous is enabled.");
        Assert.False(next.IsEnabled, "Next is enabled.");
        Assert.Equal(PartA.PreviousMatch, RealApp.HelpTextOf(previous));
        Assert.Equal(PartA.NextMatch, RealApp.HelpTextOf(next));
        Assert.Equal(PartA.CloseSearch, RealApp.HelpTextOf(app.Find("SearchClose")));
        Assert.Equal(WindowLayout.RowsTop + PartA.SearchBarHeight, app.GraphRowsBounds().Top, 1.0);
        app.WaitForButtonState("SearchButton", enabled: true, PartA.SearchToolTip(commandIsMeta: false));
    }

    /// <summary>
    /// "At the start (both methods)" of A2: nothing selected, Ready, main; no dialog; Undo and Redo
    /// with nothing to act on (the clean-up gives no reason), Search enabled; git with its 50 backups
    /// and other.txt changed; the log file's clean-up lines; the activity log closed.
    /// </summary>
    private static async Task AssertA2StartAsync(RealApp app, TempRepo repo)
    {
        WaitForNothingSelected(app);
        WaitForStatus(app, PartA.Ready);
        WaitForBranch(app, "main");
        AssertNoDialog(app);
        AssertUndoRedo(app, false, PartA.NothingToUndo, false, PartA.NothingToRedo);
        app.WaitForButtonState("SearchButton", enabled: true, helpText: null);
        await AssertGitAtA2StartAsync(repo);
        var lines = app.LogLines().Where(line => line.Contains("Removed backup ", StringComparison.Ordinal)).ToList();
        Assert.Equal(PartA.CleanUpNotes.Count, lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            Assert.Contains(PartA.CleanUpNotes[index], lines[index], StringComparison.Ordinal);
        }

        Assert.Null(app.ActivityLogBounds());
    }

    /// <summary>A fresh copy of "conflicts" with old and many backups (AD7), and with the waiting merge tool configured when <paramref name="waitingMergeTool"/>, then other.txt changed.</summary>
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

    /// <summary>"A2's merge stop": notes.txt unmerged, other.txt carried, MERGE_HEAD, the markers naming the full ref.</summary>
    private static async Task AssertMergeStopAsync(TempRepo repo)
    {
        Assert.Equal(["UU notes.txt", " M other.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(PartA.TheirsEditsSha, await RevParseAsync(repo, "MERGE_HEAD"));
        var notes = WorkingFile(repo, PartA.NotesPath).Split('\n');
        Assert.Equal(">>>>>>> refs/heads/feature/theirs", notes[5]);
        Assert.Equal(PartA.OtherChanged, WorkingFile(repo, "other.txt"));
    }

    /// <summary>
    /// "A2's backups" after the abort: no merge in progress, other.txt still changed, main at Ours
    /// edits; the abort's backup and its snapshot (parent Ours edits, the expected tree, by
    /// VisualCommit), 50 backups in all. Returns the snapshot's id.
    /// </summary>
    private static async Task<string> AssertAbortedAsync(TempRepo repo)
    {
        Assert.Equal(["## main", " M other.txt"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal(PartA.OtherChanged, WorkingFile(repo, "other.txt"));
        Assert.Null(await RevParseAsync(repo, "MERGE_HEAD"));
        Assert.Equal(PartA.OursEditsSha, await RevParseAsync(repo, "refs/heads/main"));
        var backups = await repo.BackupRefsAsync();
        Assert.Equal(51, backups.Count);
        Assert.Equal(PartA.OursEditsSha, await RevParseAsync(repo, BackupRef.Prefix + PartA.AbortBackup));
        var snapshot = await RevParseAsync(repo, BackupRef.Prefix + PartA.AbortBackup + "-changes")
            ?? throw new Xunit.Sdk.XunitException("The abort saved no -changes snapshot.");
        Assert.Equal(PartA.OursEditsSha, await GitTextAsync(repo, "log", "-1", "--format=%P", snapshot));
        Assert.Equal(PartA.AbortSnapshotTree, await GitTextAsync(repo, "log", "-1", "--format=%T", snapshot));
        Assert.Equal("VisualCommit <snapshot@visualcommit.invalid>", await GitTextAsync(repo, "log", "-1", "--format=%an <%ae>", snapshot));
        Assert.Equal(
            PartA.BackupsAfterAbort.Order(StringComparer.Ordinal),
            backups.Select(entry => BackupRef.Parse(entry.Name).Stem).Distinct().Order(StringComparer.Ordinal));
        return snapshot;
    }

    /// <summary>
    /// After a press the summary took (A4 step 3): the commit is not undone. No log line of an undo,
    /// main still at the commit, and Undo still offers it.
    /// </summary>
    private static async Task AssertCommitNotUndoneAsync(RealApp app, TempRepo repo, System.Text.RegularExpressions.Regex undoLine)
    {
        Assert.Equal(0, app.CountLogLines(undoLine));
        Assert.Equal(PartA.DescribeTheLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        AssertUndoRedo(app, true, PartA.UndoCommit, false, PartA.NothingToRedo);
    }

    /// <summary>Git as H-A0u: main back at Read whole lines, src/Reader.cs staged, scratch.txt untracked, the commit kept, the undo in HEAD's and main's reflogs.</summary>
    private static async Task AssertUndoneAsync(TempRepo repo)
    {
        Assert.Equal(PartA.ReadWholeLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main", "M  src/Reader.cs", "?? scratch.txt"], await GitLinesAsync(repo, "status", "--porcelain=v1", "-b"));
        Assert.True((await repo.GitAllowingFailureAsync("cat-file", "-e", PartA.DescribeTheLinesSha)).Succeeded, "The undone commit no longer exists.");
        Assert.Equal([PartA.UndoReflog], await ReflogAsync(repo, "HEAD", 1));
        Assert.Equal([PartA.UndoReflog], await ReflogAsync(repo, "refs/heads/main", 1));
    }

    /// <summary>Git as after A4 step 2 again: main at Describe the lines, scratch.txt untracked, the redo newest in HEAD's and main's reflogs.</summary>
    private static async Task AssertRedoneAsync(TempRepo repo)
    {
        Assert.Equal(PartA.DescribeTheLinesSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [ahead 1]", "?? scratch.txt"], await GitLinesAsync(repo, "status", "-sb"));
        Assert.Equal([PartA.RedoReflog], await ReflogAsync(repo, "HEAD", 1));
        Assert.Equal([PartA.RedoReflog], await ReflogAsync(repo, "refs/heads/main", 1));
    }
}
