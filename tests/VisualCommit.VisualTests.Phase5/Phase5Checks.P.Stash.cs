// Part P, check P25 (docs/test-reports/phase-5.md): the automatic stash before "Check out this
// commit" on an older commit of main, the way back (main checked out again, the toolbar's Pop),
// and, on a fresh copy, Undo of that checkout, Redo and Undo again.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

public sealed partial class Phase5ChecksP
{
    /// <summary>The automatic stash's checkout's undo entry, "checkout of f39ea2d".</summary>
    private static readonly string DescribeCheckout = PartP.CheckoutEntry(PartP.DescribeSha[..7]);

    /// <summary>
    /// Check P25, part A: "Check out this commit" on Describe the app with staged, unstaged and
    /// untracked changes: no question; the stash drawn as a ring on main's tip with a dashed line;
    /// the status text whole; the detached HEAD; the ignored file untouched; then main checked out
    /// again (the stash kept) and the toolbar's Pop (phase 3's plain pop, PD12).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_P25_automatic_stash_and_the_way_back()
    {
        using var repo = await Scenarios.ProtectAsync();
        await MakeP25ChangesAsync(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the working-changes row clicked: the stage panel with every kind of change.
        await SelectWorkingRowAsync(app, "Staged files (1)");
        var shot = Shot(app, "p25a-changes");
        AssertGraph(app, PartP.P0, PartP.Lanes, PartP.Working, showsDate: true, picture: shot);
        AssertWorkingRowSelected(app);
        Assert.Equal(PartP.P25Unstaged, FileRows(app, "UnstagedFileList"));
        Assert.Equal(PartP.P25Staged, FileRows(app, "StagedFileList"));
        Assert.Equal(PartP.P25Status, await StatusAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);

        // Step 2: "Check out this commit" on Describe the app: stashed, then checked out, no question.
        var calls = await CheckOutDescribeAsync(app, repo, recordTexts: true);
        shot = Shot(app, "p25b-auto-stashed");
        await AssertAutoStashedAsync(app, repo, calls, shot);

        // Step 3: a double-click on Local branches › main: no question, the stash stays.
        var mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.DoubleClick(await app.RefRowAsync("Local branches", "main"));
        await WaitForSuccessAsync(app, repo, PartP.CheckOutMain, mark);
        shot = Shot(app, "p25c-main-again");
        AssertBranchRowSelected(app, "main");
        await AssertRowSelectedAsync(app, PartP.MainWorkRowUnderStash, "Main work");
        var switched = AssertCall(app, calls, PartP.SwitchMainCommand, 0);
        await AssertGitPrintedAsync(switched.StandardError, PartP.SwitchedBackToMain, "The switch back to main");
        AssertNoStashCall(app, calls);
        AssertStatus(app, PartP.CheckedOutMain);
        AssertBranch(app, "main");
        AssertGraph(app, PartP.P1m, PartP.Lanes, showsDate: true, picture: shot);
        AssertDrawnLabels(app, PartP.P1mLabels);
        AssertDashedLines(app, PartP.StashOnTopDashes);
        await AssertLeftPanelAsync(app, stashes: 1, head: "main", PartP.AutoStashListed);
        AssertUndoRedo(app, UndoRedoButtons.CanUndo(PartP.CheckoutEntry("main")));
        await AssertOnMainAsync(repo);
        Assert.Equal(PartP.CleanStatus, await StatusAsync(repo));
        Assert.Equal([$"stash@{{0}}: {PartP.AutoStashListed}"], await StashListAsync(repo));
        Assert.Equal(PartP.DescribeToMainReflog, (await ReflogAsync(repo))[0]);
        AssertNoClippedText(app);

        // Step 4: the toolbar's Pop: phase 3's plain pop, its backup first (D171).
        AssertToolbarButton(app, "PopButton", null);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "PopButton"));
        await WaitForSuccessAsync(app, repo, PartP.PopStash, mark);
        var pop = AssertCall(app, calls, PartP.PlainPopCommand, 0);
        Assert.Equal(PartP.Dropped(PartP.AutoStashSha), Printed(pop.StandardOutput)[^1]);
        Assert.Equal([PartP.PlainPopCommand], app.StashCallsSince(calls).Select(call => call.CommandText));
        var backup = Assert.Single(await BackupsAsync(repo));
        Assert.Equal((PartP.PopBackup, PartP.AutoStashSha), (backup.Name, backup.Sha));
        AssertStatus(app, PartP.PoppedStash);
        await SelectWorkingRowAsync(app, "Staged files (0)");
        shot = Shot(app, "p25d-popped");
        AssertGraph(app, PartP.P0, PartP.Lanes, PartP.Working, showsDate: true, picture: shot);
        AssertDashedLines(app);
        await AssertLeftPanelAsync(app, stashes: 0, head: "main");
        AssertWorkingRowSelected(app);
        Assert.Equal(PartP.P25Unstaged, FileRows(app, "UnstagedFileList"));
        Assert.Empty(FileRows(app, "StagedFileList"));
        var noStaged = app.Find<TextBlock>("NoStaged");
        Assert.True(noStaged.IsEffectivelyVisible, "The staged list does not say it is empty.");
        Assert.Equal("No staged changes", noStaged.Text);
        AssertUndoRedo(app, UndoRedoButtons.CanUndo(PartP.PopAutoStashEntry));
        AssertToolbarButton(app, "PopButton", PartP.NoStashes);
        await AssertOnMainAsync(repo);
        Assert.Equal(PartP.PlainPoppedStatus, await StatusAsync(repo));
        Assert.Empty(await GitLinesAsync(repo, "diff", "--cached"));
        AssertP25Files(repo);
        Assert.Empty(await StashListAsync(repo));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check P25, part B: Undo of the automatic stash's checkout (switched back, every change back,
    /// the index too, the exact state as before), Redo (the same stash again) and Undo again.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_P25_undo_and_redo_of_the_automatic_stash()
    {
        using var repo = await Scenarios.ProtectAsync();
        await MakeP25ChangesAsync(repo);
        var before = await ExactStateAsync(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 5: steps 1 and 2 again, without their pictures.
        await SelectWorkingRowAsync(app, "Staged files (1)");
        var calls = await CheckOutDescribeAsync(app, repo, recordTexts: false);
        await AssertAutoStashedAsync(app, repo, calls, picture: null);

        // Step 6: Undo: switched back to main and the stash popped with its index.
        AssertUndoRedo(app, UndoRedoButtons.CanUndo(DescribeCheckout));
        calls = await StepAsync(app, repo, undo: true);
        var shot = Shot(app, "p25e-undone");
        await AssertUndoneAsync(app, repo, calls, before, shot);
        await AssertRowSelectedAsync(app, PartP.DescribeRow, "Describe the app");
        Assert.Equal([PartP.ResetToHeadReflog, PartP.DescribeToMainReflog], (await ReflogAsync(repo)).Take(2));
        AssertNoClippedText(app);

        // Step 7: Redo: the same stash again, then the checkout.
        AssertUndoRedo(app, UndoRedoButtons.CanRedo(DescribeCheckout));
        calls = await StepAsync(app, repo, undo: false);
        shot = Shot(app, "p25f-redone");
        var push = AssertCall(app, calls, PartP.AutoStashCommand("f39ea2d"), 0);
        var detach = AssertCall(app, calls, PartP.DetachNoOverwriteIgnoreCommand(PartP.DescribeSha), 0);
        AssertCalledInOrder(app, calls, push.CommandText, detach.CommandText);
        Assert.Equal([push.CommandText], app.StashCallsSince(calls).Select(call => call.CommandText));
        AssertBranch(app, PartP.DetachedAt(PartP.DescribeSha));
        AssertGraph(app, PartP.P1, PartP.Lanes, showsDate: true, picture: shot);
        AssertDashedLines(app, PartP.StashOnTopDashes);
        await AssertRowSelectedAsync(app, PartP.DescribeRowUnderStash, "Describe the app");
        AssertUndoRedo(app, UndoRedoButtons.CanUndo(DescribeCheckout));
        await AssertDetachedAtAsync(repo, PartP.DescribeSha);
        Assert.Equal([$"stash@{{0}}: {PartP.AutoStashListed}"], await StashListAsync(repo));
        await AssertStashCommitAsync(repo, PartP.AutoStashSha, PartP.AutoStashListed, PartP.MainWorkSha, PartP.AutoStashIndexSha, PartP.AutoStashUntrackedSha);
        Assert.Equal(PartP.CleanStatus, await StatusAsync(repo));
        Assert.Equal(PartP.SettingsLocal, WorkingFile(repo, "settings.local.json"));
        await AssertNoBackupAsync(repo);
        AssertNoClippedText(app);

        // Step 8: Undo again; the working-changes row shows the index back.
        calls = await StepAsync(app, repo, undo: true);
        await AssertUndoneAsync(app, repo, calls, before, picture: null);
        await SelectWorkingRowAsync(app, "Staged files (1)");
        shot = Shot(app, "p25g-index-back");
        AssertGraph(app, PartP.P0, PartP.Lanes, PartP.Working, showsDate: true, picture: shot);
        AssertWorkingRowSelected(app);
        Assert.Equal(PartP.P25Unstaged, FileRows(app, "UnstagedFileList"));
        Assert.Equal(PartP.P25Staged, FileRows(app, "StagedFileList"));
        Assert.Equal(before, await ExactStateAsync(repo));
        AssertNoClippedText(app);
    }

    /// <summary>Clicks the working-changes row and waits for the stage panel with P25's three unstaged files and <paramref name="stagedTitle"/>.</summary>
    private static async Task SelectWorkingRowAsync(ShellDriver app, string stagedTitle)
    {
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (3)", stagedTitle);
    }

    /// <summary>
    /// P25 step 2's input: "Check out this commit" in Describe the app's menu (first and enabled),
    /// then the operation "Check out f39ea2d" succeeded with the automatic stash's result. With
    /// <paramref name="recordTexts"/>, the status bar's texts while it ran are asserted too:
    /// <c>Stashing your changes…</c>, <c>Checking out f39ea2d…</c>, then the result. Returns the git
    /// calls' count before the click.
    /// </summary>
    private static async Task<int> CheckOutDescribeAsync(ShellDriver app, TempRepo repo, bool recordTexts)
    {
        var menu = RightClickCommit(app, PartP.DescribeRow);
        AssertItemEnabled(menu, "f39ea2d Describe the app", PartP.CheckOutThisCommitItem, first: true);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        using var texts = app.RecordStatusTexts();
        app.ClickMenuItem(PartP.CheckOutThisCommitItem);
        await WaitForSuccessAsync(app, repo, PartP.CheckOut(PartP.DescribeSha), mark, PartP.ChangesInAutoStash);
        if (recordTexts)
        {
            Assert.Equal([PartP.StashingYourChanges, PartP.CheckingOut("f39ea2d"), PartP.ChangesInAutoStash], texts.Texts);
        }

        return calls;
    }

    /// <summary>
    /// P25 "After step 2" (and step 5's "as after step 2"): the stash's push then the detached
    /// checkout with <c>--no-overwrite-ignore</c>, no pop or apply; the status text whole in the
    /// secondary colour; the graph P1 with the stash's line dashed; Describe the app selected; the
    /// left panel with the stash and no branch accented; the toolbar; and git's state: HEAD
    /// detached, the stash 256077f with its parents and its untracked part, nothing left but the
    /// ignored settings, HEAD's reflog, no backup.
    /// </summary>
    private static async Task AssertAutoStashedAsync(ShellDriver app, TempRepo repo, int calls, Screenshot? picture)
    {
        var push = AssertCall(app, calls, PartP.AutoStashCommand("f39ea2d"), 0);
        await AssertGitPrintedAsync(push.StandardOutput, [PartP.SavedStash("f39ea2d")], "The automatic stash's push");
        var detach = AssertCall(app, calls, PartP.DetachNoOverwriteIgnoreCommand(PartP.DescribeSha), 0);
        await AssertGitPrintedAsync(detach.StandardError, [PartP.HeadNowAtDescribe], "The checkout");
        AssertCalledInOrder(app, calls, push.CommandText, detach.CommandText);
        Assert.Equal([push.CommandText], app.StashCallsSince(calls).Select(call => call.CommandText));
        AssertStatus(app, PartP.ChangesInAutoStash);
        AssertStatusWhole(app, PartP.ChangesInAutoStash);
        AssertBranch(app, PartP.DetachedAt(PartP.DescribeSha));
        AssertGraph(app, PartP.P1, PartP.Lanes, showsDate: true, picture: picture);
        AssertDrawnLabels(app, PartP.P1Labels);
        AssertDashedLines(app, PartP.StashOnTopDashes);
        await AssertRowSelectedAsync(app, PartP.DescribeRowUnderStash, "Describe the app");
        await AssertLeftPanelAsync(app, stashes: 1, head: null, PartP.AutoStashListed);
        AssertUndoRedo(app, UndoRedoButtons.CanUndo(DescribeCheckout));
        AssertToolbarButton(app, "PopButton", null);
        AssertToolbarButton(app, "StashButton", PartP.NoChangesToStash);

        await AssertDetachedAtAsync(repo, PartP.DescribeSha);
        Assert.Equal([$"stash@{{0}}: {PartP.AutoStashListed}"], await StashListAsync(repo));
        Assert.Equal(PartP.AutoStashSha, await RevParseAsync(repo, "stash@{0}"));
        await AssertStashCommitAsync(repo, PartP.AutoStashSha, PartP.AutoStashListed, PartP.MainWorkSha, PartP.AutoStashIndexSha, PartP.AutoStashUntrackedSha);
        Assert.Equal(["M\tsrc/app.txt", "M\tsrc/lib.txt"], await GitLinesAsync(repo, "diff-tree", "-r", "--name-status", "stash@{0}^1", "stash@{0}"));
        Assert.Equal(["scratch.txt"], await GitLinesAsync(repo, "ls-tree", "-r", "--name-only", "stash@{0}^3"));
        Assert.False(InWorkingTree(repo, "scratch.txt"), "scratch.txt is still in the working tree.");
        Assert.Equal(PartP.CleanStatus, await StatusAsync(repo));
        Assert.Equal(PartP.SettingsLocal, WorkingFile(repo, "settings.local.json"));
        Assert.Equal(PartP.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal([PartP.MainToDescribeReflog, PartP.ResetToHeadReflog], (await ReflogAsync(repo)).Take(2));
        await AssertNoBackupAsync(repo);
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Clicks Undo (or Redo) and waits until the step ran: one operation, no question, the status
    /// bar's running text then its result, the queue's log line. Returns the git calls' count
    /// before the click.
    /// </summary>
    private static async Task<int> StepAsync(ShellDriver app, TempRepo repo, bool undo)
    {
        var (title, running, result) = undo
            ? ("Undo " + DescribeCheckout, "Undoing " + DescribeCheckout + "…", "Undid " + DescribeCheckout)
            : ("Redo " + DescribeCheckout, "Redoing " + DescribeCheckout + "…", "Redid " + DescribeCheckout);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        using var texts = app.RecordStatusTexts();
        await (undo ? app.ClickUndoAsync() : app.ClickRedoAsync());
        await WaitForSuccessAsync(app, repo, title, mark, result);
        Assert.Equal([running, result], texts.Texts);
        AssertStatus(app, result);
        return calls;
    }

    /// <summary>
    /// P25 "After step 6" (and step 8's "the calls of step 6 again"): the switch back to main with
    /// <c>--no-overwrite-ignore</c>, then the pop with <c>--index</c>, and no push; main checked out,
    /// the graph P0w, no stash; Undo disabled, Redo the checkout; the exact state as before the
    /// app started, the stash list empty, no backup.
    /// </summary>
    private static async Task AssertUndoneAsync(ShellDriver app, TempRepo repo, int calls, string before, Screenshot? picture)
    {
        var switched = AssertCall(app, calls, PartP.SwitchMainNoOverwriteIgnoreCommand, 0);
        var pop = AssertCall(app, calls, PartP.PopIndexCommand, 0);
        Assert.Equal(PartP.Dropped(PartP.AutoStashSha), Printed(pop.StandardOutput)[^1]);
        AssertCalledInOrder(app, calls, switched.CommandText, pop.CommandText);
        Assert.Equal([pop.CommandText], app.StashCallsSince(calls).Select(call => call.CommandText));
        AssertBranch(app, "main");
        AssertGraph(app, PartP.P0, PartP.Lanes, PartP.Working, showsDate: true, picture: picture);
        AssertDashedLines(app);
        await AssertLeftPanelAsync(app, stashes: 0, head: "main");
        AssertUndoRedo(app, UndoRedoButtons.CanRedo(DescribeCheckout));
        await AssertOnMainAsync(repo);
        Assert.Equal(PartP.P25Status, await StatusAsync(repo));
        Assert.Equal(before, await ExactStateAsync(repo));
        Assert.Empty(await StashListAsync(repo));
        await AssertNoBackupAsync(repo);
    }
}
