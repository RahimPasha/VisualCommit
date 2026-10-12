// Check C13 (docs/test-reports/phase-4.md, part C): Skip and Abort. A rebase stopped at First
// change in a terminal, skipped (its question, the skip backup, the stop at Second change) and
// aborted; a terminal's list of picks stopped and aborted (the whole list undone); and the app's
// own "Check out feature/rebase and rebase it onto main" stopped and aborted (main checked out
// again). Three app starts, one test method each.
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartC;

namespace VisualCommit.VisualTests.Phase4;

public sealed partial class Phase4ChecksC
{
    /// <summary>
    /// Check C13 part A (steps 1 to 5), on a "conflicts" copy where <c>git rebase main</c> on
    /// feature/rebase stopped at First change in a terminal: Skip asks, saves its backup and runs
    /// <c>git rebase --skip</c>, which stops at Second change; Abort asks, saves its backup and
    /// puts feature/rebase back.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_C13_skip_then_abort_a_rebase()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Rebase);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var resolveFirst = BannerButton.ContinueDisabled(ResolveFirstOneFile);

        // Step 1: the terminal's rebase stopped at First change.
        await AssertNoDialogAsync(app);
        var shot = Shot(app, "c13a-rebase-stopped");
        AssertStopBanner(app, shot, RebaseFirstBanner, AbortRebase, resolveFirst, BannerButton.Skip);
        AssertGraph(app, GcRebaseFirst, 5, WorkingOneConflict, showsDate: false, picture: shot);
        await AssertLocalBranchesAsync(app, mainIsHead: false);
        AssertStatus(app, "Ready");
        AssertBranch(app, "Detached at da31c97");
        await AssertRebaseFirstStopAsync(repo);
        Assert.Empty(await BackupsAsync(repo));

        // Step 2: Skip asks first (7.2); nothing has changed yet.
        app.ClickBannerSkip();
        await AssertConfirmationAsync(app, SkipTitle, SkipQuestion, BackupDetail, "Skip");
        Shot(app, "c13b-skip-question");
        Assert.Empty(await BackupsAsync(repo));
        await AssertRebaseFirstStopAsync(repo);

        // Step 3: Skip: the backup, git rebase --skip, the stop at Second change.
        var logMark = app.LogMark();
        var callMark = app.GitCalls().Count;
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await WaitForOperationAsync(app, repo, SkipOperation, logMark);
        await AssertNoDialogAsync(app);
        shot = Shot(app, "c13c-second-stop");
        var backups = await BackupsAsync(repo);
        Assert.Equal(2, backups.Count);
        var skip = Assert.Single(backups, backup => !backup.Ref.IsChanges);
        var skipChanges = Assert.Single(backups, backup => backup.Ref.IsChanges);
        Assert.Equal(("skip", "skip"), (skip.Ref.Kind, skipChanges.Ref.Kind));
        Assert.Equal(skip.Name + "-changes", skipChanges.Name);
        Assert.Null(skip.Ref.Name);
        Assert.Equal(StopSha.OursEdits, skip.Sha);
        await AssertChangesSnapshotAsync(repo, skipChanges.Sha, StopSha.OursEdits, NotesRebaseFirst);
        var skipped = AssertCalled(app, callMark, RebaseSkipCall);
        Assert.Equal(1, skipped.ExitCode);
        await AssertGitPrintedAsync(skipped, ConflictInNotes);
        AssertStatus(app, RebaseStoppedStatus, OperationStatusTone.Warning);
        AssertStopBanner(app, shot, RebaseSecondBanner, AbortRebase, resolveFirst, BannerButton.Skip);
        AssertGraph(app, GcRebaseFirst, 5, WorkingOneConflict, showsDate: false, picture: shot);
        await AssertLocalBranchesAsync(app, mainIsHead: false);
        AssertBranch(app, "Detached at da31c97");
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));
        Assert.Equal(StopSha.OursEdits, await repo.HeadAsync());
        Assert.Equal(string.Empty, (await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD")).StandardOutput.Trim());
        AssertRebaseFiles(repo, SkippedSecondStopFiles);
        Assert.Equal(NotesSkippedSecond, WorkingFile(repo, ResolverFile));
        Assert.Equal(SkippedSecondStages, await StagesOfNotesAsync(repo));
        await AssertBranchesAsync(repo);

        // Step 4: Abort asks first, without the return sentence (a terminal began this rebase).
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await AssertConfirmationAsync(app, AbortRebaseTitle, AbortRebaseQuestion, BackupDetail, AbortRebase);
        Shot(app, "c13d-abort-question");

        // Step 5: Abort: the backup, git rebase --abort, feature/rebase as before.
        logMark = app.LogMark();
        callMark = app.GitCalls().Count;
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await WaitForOperationAsync(app, repo, AbortRebaseOperation, logMark);
        await AssertNoDialogAsync(app);
        shot = Shot(app, "c13e-aborted");
        var aborted = AssertCalled(app, callMark, RebaseAbortCall);
        Assert.True(aborted.Succeeded);
        Assert.Equal(string.Empty, aborted.StandardOutput + aborted.StandardError);
        backups = await BackupsAsync(repo);
        Assert.Equal(4, backups.Count);
        Assert.Equal(["abort", "abort", "skip", "skip"], backups.Select(backup => backup.Ref.Kind).ToList());
        Assert.Equal([false, true, false, true], backups.Select(backup => backup.Ref.IsChanges).ToList());
        Assert.Equal(backups[0].Name + "-changes", backups[1].Name);
        Assert.Equal(backups[2].Name + "-changes", backups[3].Name);
        Assert.True(backups[2].Ref.Time < backups[0].Ref.Time, $"The skip's time {backups[2].Ref.Time:O} is not before the abort's {backups[0].Ref.Time:O}.");
        Assert.Equal(StopSha.OursEdits, backups[0].Sha);
        await AssertChangesSnapshotAsync(repo, backups[1].Sha, StopSha.OursEdits, NotesSkippedSecond);
        Assert.Equal((skip.Name, skip.Sha), (backups[2].Name, backups[2].Sha));
        Assert.Equal((skipChanges.Name, skipChanges.Sha), (backups[3].Name, backups[3].Sha));
        Assert.Null(app.BannerBounds);
        AssertStatus(app, AbortedRebaseStatus);
        AssertGraph(app, Gc0r, 5, working: null, showsDate: false, picture: shot);
        AssertBranch(app, "feature/rebase");
        Assert.Equal(["## feature/rebase"], await LinesOfGitAsync(repo, "status", "--short", "--branch"));
        Assert.Equal("feature/rebase", await repo.CurrentBranchAsync());
        Assert.Equal(StopSha.SecondChange, await repo.HeadAsync());
        Assert.Equal(Scenarios.ConflictsFiles.NotesSecond, WorkingFile(repo, ResolverFile));
        Assert.False(GitHas(repo, "rebase-merge"), "rebase-merge/ is still there.");
        await AssertBranchesAsync(repo);
    }

    /// <summary>
    /// Check C13 part B (steps 6 to 8), on a "conflicts" copy where a terminal's
    /// <c>git cherry-pick</c> of the three feature/picks commits stopped at Pick conflicting:
    /// Abort asks with the list's sentence, saves its backup and undoes the whole list.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_C13_abort_a_pick_list()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.CherryPicks);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 6: the terminal's list stopped.
        await AssertNoDialogAsync(app);
        var shot = Shot(app, "c13f-picks-stopped");
        AssertStopBanner(app, shot, PickListBanner, AbortCherryPick, BannerButton.ContinueDisabled(ResolveFirstOneFile), BannerButton.Skip);
        AssertGraph(app, GcTerminalPicksStopped, 5, WorkingOneConflict, showsDate: false, picture: shot);
        AssertStatus(app, "Ready");
        AssertBranch(app, "main");
        await AssertPicksStopAsync(repo, StopSha.TerminalPickedClean);
        Assert.Empty(await BackupsAsync(repo));

        // Step 7: Abort asks, HEAD being at sequencer/abort-safety.
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await AssertConfirmationAsync(app, AbortPickTitle, AbortPickListQuestion, BackupDetail, AbortCherryPick);
        Shot(app, "c13g-abort-question");

        // Step 8: Abort: the backup, git cherry-pick --abort, main back at Ours edits.
        var logMark = app.LogMark();
        var callMark = app.GitCalls().Count;
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await WaitForOperationAsync(app, repo, AbortPickOperation, logMark);
        await AssertNoDialogAsync(app);
        shot = Shot(app, "c13h-picks-aborted");
        Assert.True(AssertCalled(app, callMark, PickAbortCall).Succeeded);
        var backups = await BackupsAsync(repo);
        Assert.Equal(2, backups.Count);
        Assert.Equal(["abort", "abort"], backups.Select(backup => backup.Ref.Kind).ToList());
        Assert.Equal([false, true], backups.Select(backup => backup.Ref.IsChanges).ToList());
        Assert.Equal(backups[0].Name + "-changes", backups[1].Name);
        Assert.Equal(StopSha.TerminalPickedClean, backups[0].Sha);
        await AssertChangesSnapshotAsync(repo, backups[1].Sha, StopSha.TerminalPickedClean, NotesPicks);
        Assert.Null(app.BannerBounds);
        AssertStatus(app, AbortedPickStatus);
        AssertGraph(app, Gc0, 5, working: null, showsDate: false, picture: shot);
        await AssertLocalBranchesAsync(app, mainIsHead: true);
        AssertBranch(app, "main");
        Assert.Equal(["## main"], await LinesOfGitAsync(repo, "status", "--short", "--branch"));
        Assert.Equal(StopSha.OursEdits, await repo.HeadAsync());
        Assert.False(File.Exists(Path.Combine(repo.Path, "pick.txt")), "pick.txt is still there.");
        Assert.Equal(Scenarios.ConflictsFiles.NotesOurs, WorkingFile(repo, ResolverFile));
        Assert.False(GitHas(repo, "CHERRY_PICK_HEAD"), "CHERRY_PICK_HEAD is still there.");
        Assert.False(GitHas(repo, "MERGE_MSG"), "MERGE_MSG is still there.");
        Assert.False(GitHas(repo, "sequencer"), "sequencer/ is still there.");
        await AssertBranchesAsync(repo);
    }

    /// <summary>
    /// Check C13 part C (steps 9 to 12), on "conflicts": feature/rebase dragged onto main and
    /// "Check out feature/rebase and rebase it onto main" chosen, which stops at First change;
    /// Abort asks with the return sentence, aborts and checks main out again.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_C13_abort_check_out_and_rebase()
    {
        using var repo = await Scenarios.ConflictsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 9: feature/rebase dropped onto main: the drop menu.
        var source = app.GraphLabelBounds(5, "feature/rebase");
        var target = app.GraphLabelBounds(0, "main");
        app.PressMouse(source.Center);
        Assert.Equal(5, Graph(app).SelectedIndex);
        app.MoveMouseHeld(target.Center, steps: 5);
        app.ReleaseMouse(target.Center);
        var menu = app.Menu();
        await WaitForDetailsOfAsync(app, "Second change");
        var shot = Shot(app, "c13i-drop-menu");
        AssertSelectedCommits(app, shot, "305f7f7");
        AssertMenu(menu, RebaseOntoMainDropMenu);
        AssertHighlighted(menu, "Merge feature/rebase into main");
        AssertMenuShape(app, menu);
        AssertMenuAtPointer(menu, target.Center);
        Assert.Equal(171, menu.Bounds.Height, 2.0);

        // Step 10: the checkout, the backup, the rebase that stops, and the return file (14.3).
        var logMark = app.LogMark();
        var callMark = app.GitCalls().Count;
        app.ClickMenuItem("Check out feature/rebase and rebase it onto main");
        await WaitForOperationAsync(app, repo, CheckOutAndRebaseOperation, logMark);
        await AssertNoDialogAsync(app);
        shot = Shot(app, "c13j-rebase-stopped");
        var step10Calls = callMark;
        Assert.True(AssertCalled(app, callMark, SwitchToRebaseCall).Succeeded);
        Assert.Equal(1, AssertCalled(app, callMark, RebaseOntoMainCall).ExitCode);
        AssertCalledInOrder(app, callMark, SwitchToRebaseCall, RebaseOntoMainCall);
        var backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal(("rebase", false, "feature/rebase"), (backup.Ref.Kind, backup.Ref.IsChanges, backup.Ref.Name));
        Assert.Equal(StopSha.SecondChange, backup.Sha);
        Assert.Equal(ReturnFile, GitFile(repo, "rebase-merge/visualcommit-return"));
        AssertStatus(app, RebaseStoppedStatus, OperationStatusTone.Warning);
        AssertStopBanner(app, shot, RebaseFirstBanner, AbortRebase, BannerButton.ContinueDisabled(ResolveFirstOneFile), BannerButton.Skip);
        AssertGraph(app, GcRebaseFirst, 5, WorkingOneConflict, showsDate: false, picture: shot);
        await AssertLocalBranchesAsync(app, mainIsHead: false);
        AssertSelectedCommits(app, shot, "305f7f7");
        AssertBranch(app, "Detached at da31c97");
        await AssertRebaseFirstStopAsync(repo);

        // Step 11: Abort asks, with the return sentence.
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await AssertConfirmationAsync(app, AbortRebaseTitle, AbortRebaseReturnQuestion, BackupDetail, AbortRebase);
        Shot(app, "c13k-abort-question");

        // Step 12: Abort, then main checked out again.
        logMark = app.LogMark();
        callMark = app.GitCalls().Count;
        app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton"));
        await WaitForOperationAsync(app, repo, AbortRebaseOperation, logMark);
        await AssertNoDialogAsync(app);
        shot = Shot(app, "c13l-returned");
        AssertCalledInOrder(app, callMark, RebaseAbortCall, SwitchToMainCall);
        Assert.True(AssertCalled(app, callMark, RebaseAbortCall).Succeeded);
        Assert.True(AssertCalled(app, callMark, SwitchToMainCall).Succeeded);
        var backups = await BackupsAsync(repo);
        Assert.Equal(3, backups.Count);
        Assert.Equal(["abort", "abort", "rebase"], backups.Select(entry => entry.Ref.Kind).ToList());
        Assert.Equal([false, true, false], backups.Select(entry => entry.Ref.IsChanges).ToList());
        Assert.Equal(backups[0].Name + "-changes", backups[1].Name);
        Assert.Equal(StopSha.OursEdits, backups[0].Sha);
        await AssertChangesSnapshotAsync(repo, backups[1].Sha, StopSha.OursEdits, NotesRebaseFirst);
        Assert.Equal((backup.Name, backup.Sha), (backups[2].Name, backups[2].Sha));
        Assert.True(backups[2].Ref.Time < backups[0].Ref.Time, $"The rebase's time {backups[2].Ref.Time:O} is not before the abort's {backups[0].Ref.Time:O}.");
        Assert.Null(app.BannerBounds);
        AssertStatus(app, AbortedRebaseStatus);
        AssertGraph(app, Gc0, 5, working: null, showsDate: false, picture: shot);
        await AssertLocalBranchesAsync(app, mainIsHead: true);
        AssertSelectedCommits(app, shot, "305f7f7");
        AssertBranch(app, "main");
        Assert.Equal(["## main"], await LinesOfGitAsync(repo, "status", "--short", "--branch"));
        Assert.False(GitHas(repo, "rebase-merge"), "rebase-merge/ is still there.");
        await AssertBranchesAsync(repo);

        // Step 10's read of rebase.autoStash before the checkout (14.3), asserted last so that the
        // steps after it run whatever it shows.
        AssertCalledInOrder(app, step10Calls, AutoStashCall, SwitchToRebaseCall, RebaseOntoMainCall);
    }
}
