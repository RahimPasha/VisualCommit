// Part P, checks P26 and P27 (docs/test-reports/phase-5.md): when the automatic stash is not taken
// (HEAD's own commit, a commit off the branch) or is taken and put back (an ignored file in the
// way); and the refusals of a merge, a cherry-pick and a revert over a changed or a staged file,
// and of a stash's apply and pop by the app's own check before git.
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksP
{
    /// <summary>
    /// Check P26, part A: a commit only <c>other</c> reaches (no stash; refused with 9.3's sentence
    /// and the button), then HEAD's own commit (no stash; HEAD detached, the change kept).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_P26_no_stash_at_head_or_off_the_branch()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", PartP.AppLocal);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var refs = await RefsAsync(repo);
        var reflog = await ReflogAsync(repo);

        // Step 1: P0w.
        AssertGraph(app, PartP.P0, PartP.Lanes, PartP.Working, showsDate: true);
        AssertUndoRedo(app, UndoRedoButtons.Nothing);

        // Step 2: Other edits, not on main: no stash, git refuses the detached checkout (TP1d).
        var menu = RightClickCommit(app, PartP.OtherEditsRow, belowWorkingRow: true);
        AssertItemEnabled(menu, "4f5e821 Other edits", PartP.CheckOutThisCommitItem, first: true);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartP.CheckOutThisCommitItem);
        await WaitForFailureAsync(app, repo, PartP.CheckOut(PartP.OtherEditsSha), mark);
        Shot(app, "p26a-off-the-branch");
        await AssertRowSelectedAsync(app, PartP.OtherEditsRow, "Other edits");
        AssertCall(app, calls, PartP.DetachCommand(PartP.OtherEditsSha), 1);
        AssertNoStashCall(app, calls);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartP.CheckoutBlockedTitle, PartP.OneConflictPreventsCheckout, PartP.Repo, PartP.DetachCommand(PartP.OtherEditsSha), PartP.ButtonsWithStash, PartP.Tp1, PartP.CheckoutChangesKey));
        AssertExtraButtonEnabled(app, PartP.StashAndCheckOutButton);
        AssertStatus(app, PartP.Failed(PartP.CheckOut(PartP.OtherEditsSha)), OperationStatusTone.Danger);
        await AssertOnMainAsync(repo);
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        Assert.Empty(await StashListAsync(repo));
        Assert.Equal(reflog, await ReflogAsync(repo));
        Assert.Equal(refs, await RefsAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 3: HEAD's own commit: git's plain detached checkout, no stash, the change carried.
        menu = RightClickCommit(app, PartP.MainWorkRow, belowWorkingRow: true);
        AssertItemEnabled(menu, "41f8398 Main work", PartP.CheckOutThisCommitItem, first: true);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartP.CheckOutThisCommitItem);
        await WaitForSuccessAsync(app, repo, PartP.CheckOut(PartP.MainWorkSha), mark);
        var shot = Shot(app, "p26b-head-own-commit");
        var detach = AssertCall(app, calls, PartP.DetachCommand(PartP.MainWorkSha), 0);
        await AssertGitPrintedAsync(detach.StandardOutput, [PartP.CarriedAppChange], "The detached checkout's standard output");
        await AssertGitPrintedAsync(detach.StandardError, [PartP.HeadNowAtMainWork], "The detached checkout's standard error");
        AssertNoStashCall(app, calls);
        AssertStatus(app, PartP.DetachedAtStatus(PartP.MainWorkSha));
        AssertBranch(app, PartP.DetachedAt(PartP.MainWorkSha));
        AssertGraph(app, PartP.P0h, PartP.Lanes, PartP.Working, showsDate: true, picture: shot);
        AssertDrawnLabels(app, PartP.P0hLabels);
        AssertDashedLines(app);
        await AssertRowSelectedAsync(app, PartP.MainWorkRow, "Main work");
        await AssertLeftPanelAsync(app, stashes: 0, head: null);
        AssertUndoRedo(app, UndoRedoButtons.CanUndo(PartP.CheckoutEntry(PartP.MainWorkSha[..7])));
        await AssertDetachedAtAsync(repo, PartP.MainWorkSha);
        Assert.Equal(PartP.AppLocal, WorkingFile(repo, "src/app.txt"));
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        Assert.Empty(await StashListAsync(repo));
        Assert.Equal(PartP.MainToMainWorkReflog, (await ReflogAsync(repo))[0]);
        Assert.Equal(PartP.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));
        await AssertNoBackupAsync(repo);
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check P26, part B: Add the guide tracks the now-ignored settings.local.json: on a clean tree
    /// no stash and the ignored-files text; with changes the stash taken, the checkout refused, the
    /// stash popped back and everything as before, the file intact.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_P26_ignored_file_in_the_way()
    {
        using var repo = await Scenarios.ProtectAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var reflog = await ReflogAsync(repo);

        // Step 4: on a clean tree (an ignored file is no change): refused, nothing to stash (TP6).
        AssertGraph(app, PartP.P0, PartP.Lanes, showsDate: true);
        var menu = RightClickCommit(app, PartP.AddGuideRow, belowWorkingRow: false);
        AssertItemEnabled(menu, "73e0bab Add the guide", PartP.CheckOutThisCommitItem, first: true);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartP.CheckOutThisCommitItem);
        await WaitForFailureAsync(app, repo, PartP.CheckOut(PartP.AddGuideSha), mark);
        Shot(app, "p26c-ignored-file");
        await AssertRowSelectedAsync(app, PartP.AddGuideRow, "Add the guide");
        AssertCall(app, calls, PartP.DetachNoOverwriteIgnoreCommand(PartP.AddGuideSha), 1);
        AssertNoStashCall(app, calls);
        var ignored = AssertCall(app, calls, PartP.CheckIgnoreCommand, 0);
        Assert.Equal("settings.local.json", ignored.StandardOutput.TrimEnd('\0'));
        var dialog = new ExpectedErrorDialog(
            PartP.CheckoutBlockedTitle, PartP.IgnoredFileInTheWay, PartP.Repo, PartP.DetachNoOverwriteIgnoreCommand(PartP.AddGuideSha), PartP.ButtonsWithOutput, PartP.Tp6, PartP.CheckoutUntrackedKey);
        await AssertErrorDialogAsync(app, dialog);
        AssertStatus(app, PartP.Failed(PartP.CheckOut(PartP.AddGuideSha)), OperationStatusTone.Danger);
        await AssertOnMainAsync(repo);
        Assert.Equal(PartP.SettingsLocal, WorkingFile(repo, "settings.local.json"));
        Assert.Equal(PartP.CleanStatus, await StatusAsync(repo));
        Assert.Equal(reflog, await ReflogAsync(repo));
        Assert.Empty(await StashListAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 5: P25's changes made outside the app.
        mark = app.LogMark();
        await MakeP25ChangesAsync(repo);
        await WaitForStatusReadAsync(app, 1, 3, mark);
        var before = await ExactStateAsync(repo);
        AssertGraph(app, PartP.P0, PartP.Lanes, PartP.Working, showsDate: true);
        Assert.Equal(PartP.P25Status, await StatusAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);

        // Step 6: stashed, refused, popped back: everything as before.
        menu = RightClickCommit(app, PartP.AddGuideRow, belowWorkingRow: true);
        AssertItemEnabled(menu, "73e0bab Add the guide", PartP.CheckOutThisCommitItem, first: true);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartP.CheckOutThisCommitItem);
        await WaitForFailureAsync(app, repo, PartP.CheckOut(PartP.AddGuideSha), mark);
        Shot(app, "p26d-ignored-after-stash");
        var push = AssertCall(app, calls, PartP.AutoStashCommand("73e0bab"), 0);
        await AssertGitPrintedAsync(push.StandardOutput, [PartP.SavedStash("73e0bab")], "The automatic stash's push");
        var detach = AssertCall(app, calls, PartP.DetachNoOverwriteIgnoreCommand(PartP.AddGuideSha), 1);
        var pop = AssertCall(app, calls, PartP.PopIndexCommand, 0);
        Assert.Equal(PartP.Dropped(PartP.GuideStashSha), Printed(pop.StandardOutput)[^1]);
        AssertCalledInOrder(app, calls, push.CommandText, detach.CommandText, pop.CommandText);
        Assert.Equal([push.CommandText, pop.CommandText], app.StashCallsSince(calls).Select(call => call.CommandText));
        await AssertErrorDialogAsync(app, dialog);
        AssertStatus(app, PartP.Failed(PartP.CheckOut(PartP.AddGuideSha)), OperationStatusTone.Danger);
        Assert.Equal(before, await ExactStateAsync(repo));
        AssertP25Files(repo);
        Assert.Empty(await StashListAsync(repo));
        await AssertOnMainAsync(repo);
        string[] reflogAfter = [PartP.ResetToHeadReflog, PartP.ResetToHeadReflog, .. reflog];
        Assert.Equal(reflogAfter, await ReflogAsync(repo));
        await AssertNoBackupAsync(repo);
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);
    }

    /// <summary>
    /// Check P27, part A: a merge, a cherry-pick and a revert over a changed file (9.3's sentence
    /// for each), then the three over a staged file ("Your staged changes prevent the …"); nothing
    /// changes, nothing is in progress.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_P27_merge_pick_and_revert_refused()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", PartP.AppLocal);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var refs = await RefsAsync(repo);
        var reflog = await ReflogAsync(repo);

        // Step 1: P0w.
        AssertGraph(app, PartP.P0, PartP.Lanes, PartP.Working, showsDate: true);
        AssertUndoRedo(app, UndoRedoButtons.Nothing);

        // Step 2: "Merge other into main" over the changed src/app.txt (TP7).
        var calls = await MergeOtherAsync(app, repo);
        AssertNothingSelected(app);
        Assert.Empty(SelectedRefRows(app));
        Shot(app, "p27a-merge-blocked");
        await AssertRefusedAsync(app, repo, calls, PartP.MergeOther, PartP.MergeFailedTitle, PartP.OneConflictPreventsMerge, PartP.MergeOtherCommand, PartP.Tp7, PartP.MergeChangesKey, mergeExit: true);
        Assert.Equal(PartP.AppLocal, WorkingFile(repo, "src/app.txt"));
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        Assert.Equal(reflog, await ReflogAsync(repo));
        await CloseDialogAsync(app);

        // Step 3: "Cherry-pick onto main" of Other edits (TP8).
        calls = await CherryPickOtherEditsAsync(app, repo);
        Shot(app, "p27b-pick-blocked");
        await AssertRowSelectedAsync(app, PartP.OtherEditsRow, "Other edits");
        await AssertRefusedAsync(app, repo, calls, PartP.CherryPickOtherEdits, PartP.CherryPickFailedTitle, PartP.OneConflictPreventsCherryPick, PartP.CherryPickOtherEditsCommand, PartP.Tp8, PartP.MergeChangesKey);
        Assert.Equal(PartP.AppLocal, WorkingFile(repo, "src/app.txt"));
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        await CloseDialogAsync(app);

        // Step 4: README.md changed outside the app; "Revert this commit" on Describe the app (TP12).
        var mark = app.LogMark();
        repo.WriteFile("README.md", PartP.ReadmeLocal);
        await WaitForStatusReadAsync(app, 0, 2, mark);
        Assert.Equal([" M README.md", " M src/app.txt"], await PorcelainAsync(repo));
        calls = await RevertDescribeAsync(app, repo);
        Shot(app, "p27c-revert-blocked");
        await AssertRowSelectedAsync(app, PartP.DescribeRow, "Describe the app");
        await AssertRefusedAsync(app, repo, calls, PartP.RevertDescribe, PartP.RevertFailedTitle, PartP.OneConflictPreventsRevert, PartP.RevertDescribeCommand, PartP.Tp12, PartP.MergeChangesKey);
        Assert.Equal(PartP.ReadmeLocal, WorkingFile(repo, "README.md"));
        Assert.Equal(PartP.AppLocal, WorkingFile(repo, "src/app.txt"));
        Assert.Equal([" M README.md", " M src/app.txt"], await PorcelainAsync(repo));
        Assert.Equal(reflog, await ReflogAsync(repo));
        await CloseDialogAsync(app);

        // Step 5: src/app.txt put back and README.md staged outside the app.
        mark = app.LogMark();
        await repo.GitAsync("checkout", "--", "src/app.txt");
        repo.WriteFile("README.md", PartP.ReadmeStaged);
        await repo.GitAsync("add", "--", "README.md");
        await WaitForStatusReadAsync(app, 1, 0, mark);
        AssertGraph(app, PartP.P0, PartP.Lanes, PartP.Working, showsDate: true);
        Assert.Equal(["M  README.md"], await PorcelainAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);

        // Step 6: the merge again (TP9: the staged form).
        calls = await MergeOtherAsync(app, repo);
        Shot(app, "p27d-merge-staged");
        await AssertRefusedAsync(app, repo, calls, PartP.MergeOther, PartP.MergeFailedTitle, PartP.StagedPreventMerge, PartP.MergeOtherCommand, PartP.Tp9, PartP.Tp9Key, mergeExit: true);
        Assert.Equal(["M  README.md"], await PorcelainAsync(repo));
        await CloseDialogAsync(app);

        // Step 7: the cherry-pick again (TP10).
        calls = await CherryPickOtherEditsAsync(app, repo);
        Shot(app, "p27e-pick-staged");
        await AssertRefusedAsync(app, repo, calls, PartP.CherryPickOtherEdits, PartP.CherryPickFailedTitle, PartP.StagedPreventCherryPick, PartP.CherryPickOtherEditsCommand, PartP.Tp10, PartP.Tp10Key);
        await CloseDialogAsync(app);

        // Step 8: the revert again (TP11); nothing changed in the whole part.
        calls = await RevertDescribeAsync(app, repo);
        Shot(app, "p27f-revert-staged");
        await AssertRowSelectedAsync(app, PartP.DescribeRow, "Describe the app");
        await AssertRefusedAsync(app, repo, calls, PartP.RevertDescribe, PartP.RevertFailedTitle, PartP.StagedPreventRevert, PartP.RevertDescribeCommand, PartP.Tp11, PartP.Tp11Key);
        Assert.Equal(["M  README.md"], await PorcelainAsync(repo));
        Assert.Equal(PartP.ReadmeStaged, WorkingFile(repo, "README.md"));
        Assert.Equal(reflog, await ReflogAsync(repo));
        await AssertNoBackupAsync(repo);
        Assert.Equal(refs, await RefsAsync(repo));
        await CloseDialogAsync(app);
    }

    /// <summary>
    /// Check P27, part B: a stash's apply and pop refused by the app's own check before git (one
    /// local change in its way, then also its untracked file existing); git not run, nothing
    /// changes, no backup.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_P27_stash_apply_refused_by_the_app()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", PartP.AppStashed).WriteFile("todo.txt", PartP.TodoStashed);
        await repo.GitWithEnvironmentAsync(new Dictionary<string, string?> { ["GIT_LITERAL_PATHSPECS"] = null }, "stash", "push", "--include-untracked", "--message", "App experiment");
        repo.WriteFile("src/app.txt", PartP.AppLocal);
        Assert.Equal(PartP.ExperimentStashSha, await RevParseAsync(repo, "refs/stash"));
        Assert.Equal(
            $"{PartP.MainWorkSha} {PartP.ExperimentIndexSha} {PartP.ExperimentUntrackedSha}",
            (await GitLinesAsync(repo, "log", "-1", "--format=%P", "refs/stash")).Single());
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 9: P0s; the stash's menu; "Apply stash" refused before git.
        AssertGraph(app, PartP.P0s, PartP.Lanes, PartP.Working, showsDate: true);
        AssertDashedLines(app, PartP.StashOnTopDashes);
        await AssertLeftPanelAsync(app, stashes: 1, head: "main", PartP.ExperimentListed);
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        var menu = await RightClickRefAsync(app, "Stashes", PartP.ExperimentListed);
        AssertMenu(menu, PartP.ExperimentStashMenu);
        AssertHighlighted(menu, PartP.ApplyStashItem);
        Assert.True(Math.Abs(menu.Bounds.Height - PartP.StashMenuHeight) <= WindowLayout.MenuHeightTolerance, $"The stash's menu is {menu.Bounds.Height} high; the report says {PartP.StashMenuHeight}.");
        AssertMenuShape(app, menu);
        AssertNothingSelected(app);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartP.ApplyStashItem);
        await WaitForFailureAsync(app, repo, PartP.ApplyStash, mark);
        Shot(app, "p27g-apply-refused");
        AssertNoStashCall(app, calls);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartP.ApplyingStashFailedTitle, PartP.OneConflictPreventsApplyingStash, PartP.Repo, null, PartP.CloseOnly, []));
        AssertStatus(app, PartP.Failed(PartP.ApplyStash), OperationStatusTone.Danger);
        Assert.Equal(["stash@{0}: On main: App experiment"], await StashListAsync(repo));
        Assert.Equal(PartP.AppLocal, WorkingFile(repo, "src/app.txt"));
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        Assert.False(InWorkingTree(repo, "todo.txt"), "todo.txt was written.");
        await AssertNoBackupAsync(repo);
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 10: todo.txt written outside the app.
        mark = app.LogMark();
        repo.WriteFile("todo.txt", PartP.TodoMine);
        await WaitForStatusReadAsync(app, 0, 2, mark);
        AssertUndoRedo(app, UndoRedoButtons.Nothing);

        // Step 11: the toolbar's Pop refused before git: two paths in the way.
        AssertToolbarButton(app, "PopButton", null);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "PopButton"));
        await WaitForFailureAsync(app, repo, PartP.PopStash, mark);
        Shot(app, "p27h-pop-refused");
        AssertNoStashCall(app, calls);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartP.ApplyingStashFailedTitle, PartP.TwoConflictsPreventApplyingStash, PartP.Repo, null, PartP.CloseOnly, []));
        AssertStatus(app, PartP.Failed(PartP.PopStash), OperationStatusTone.Danger);
        Assert.Equal(["stash@{0}: On main: App experiment"], await StashListAsync(repo));
        Assert.Equal([" M src/app.txt", "?? todo.txt"], await PorcelainAsync(repo));
        Assert.Equal(PartP.TodoMine, WorkingFile(repo, "todo.txt"));
        await AssertNoBackupAsync(repo);
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);
    }

    /// <summary>P27 steps 2 and 6: Local branches › other's menu (opened without selecting anything, D114), "Merge other into main", the operation failed. Returns the git calls' count before the click.</summary>
    private static async Task<int> MergeOtherAsync(ShellDriver app, TempRepo repo)
    {
        var (selectedRow, selectedRefs) = (Repo(app).SelectedIndex, SelectedRefRows(app));
        var menu = await RightClickRefAsync(app, "Local branches", "other");
        AssertItemEnabled(menu, "other", PartP.MergeOtherItem);
        Assert.Equal(selectedRow, Repo(app).SelectedIndex);
        Assert.Equal(selectedRefs, SelectedRefRows(app));
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartP.MergeOtherItem);
        await WaitForFailureAsync(app, repo, PartP.MergeOther, mark);
        return calls;
    }

    /// <summary>P27 steps 3 and 7: Other edits' menu, "Cherry-pick onto main" (enabled), the operation failed. Returns the git calls' count before the click.</summary>
    private static async Task<int> CherryPickOtherEditsAsync(ShellDriver app, TempRepo repo)
    {
        var menu = RightClickCommit(app, PartP.OtherEditsRow, belowWorkingRow: true);
        AssertItemEnabled(menu, "4f5e821 Other edits", PartP.CherryPickItem);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartP.CherryPickItem);
        await WaitForFailureAsync(app, repo, PartP.CherryPickOtherEdits, mark);
        return calls;
    }

    /// <summary>P27 steps 4 and 8: Describe the app's menu, "Revert this commit" (enabled), the operation failed. Returns the git calls' count before the click.</summary>
    private static async Task<int> RevertDescribeAsync(ShellDriver app, TempRepo repo)
    {
        var menu = RightClickCommit(app, PartP.DescribeRow, belowWorkingRow: true);
        AssertItemEnabled(menu, "f39ea2d Describe the app", PartP.RevertItem);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartP.RevertItem);
        await WaitForFailureAsync(app, repo, PartP.RevertDescribe, mark);
        return calls;
    }

    /// <summary>
    /// A merge, cherry-pick or revert refused by git (P27 part A): its call made once since
    /// <paramref name="calls"/> (the git calls' count before the click) with exit 128
    /// (a merge on another git than 2.36: 2, design 5.6's surprise), the error dialog with the
    /// operation's own title and 9.3's summary, "Copy output" and "Close"; the status bar
    /// "&lt;Title&gt; failed" in the danger colour; no banner; nothing in progress; HEAD on main at
    /// Main work; nothing recorded for Undo.
    /// </summary>
    private static async Task AssertRefusedAsync(
        ShellDriver app,
        TempRepo repo,
        int calls,
        string title,
        string errorTitle,
        string summary,
        string command,
        string[] lines,
        string key,
        bool mergeExit = false)
    {
        var call = Assert.Single(app.GitCalls().Skip(calls), record => record.CommandText == command);
        var reportGit = await IsReportGitAsync();
        Assert.True(
            call.ExitCode == 128 || (mergeExit && !reportGit && call.ExitCode == 2),
            $"\"{command}\" exited {call.ExitCode}; the report says 128{(mergeExit ? " (2 on Git 2.55)" : string.Empty)}.\n{call.StandardError}{call.StandardOutput}");
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(errorTitle, summary, PartP.Repo, command, PartP.ButtonsWithOutput, lines, key));
        AssertStatus(app, PartP.Failed(title), OperationStatusTone.Danger);
        Assert.Null(app.BannerBounds);
        AssertNothingInProgress(repo);
        await AssertOnMainAsync(repo);
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
    }
}
