// Part P's real-window steps (docs/test-reports/phase-5.md, part P, the "Real window" column of P24
// to P27): P24 part A, steps 1 and 2 (a real double-click on a branch; the error dialog read through
// UI Automation); P25 part A, steps 1 and 2 (the automatic stash by a real right-click and click),
// then, in the same launch, part B's step 6 (the toolbar's Undo by a real click). Each launch on a
// fresh "protect" copy at 1100×700, dark. P26 and P27 are "n/a" (the report gives the reasons).
using System.Drawing;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase5RealWindowPass
{
    /// <summary>
    /// Check P24, part A, steps 1 and 2, with the real mouse: "protect" with a changed file
    /// (<c>p24a-changed-file</c>), then a real double-click on Local branches › other, which git
    /// refuses: the dialog's title, summary, extra button and git's output read through UI
    /// Automation (<c>p24b-one-conflict</c>), then a real click on Close. Nothing changes in git, no
    /// stash is made, nothing is recorded for Undo.
    /// </summary>
    [Fact]
    public async Task Check_P24_checkout_refused_by_a_real_double_click()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", PartP.AppLocal);
        using var app = Launch(repo);
        var name = NameOf(repo);
        Assert.Equal(PartP.Repo, name);
        var refs = await RefsAsync(repo);
        var reflog = await GitLinesAsync(repo, "log", "-g", "--format=%gs", "HEAD");

        // Step 1: P0w, nothing selected, the status bar "Ready".
        WaitForNothingSelected(app);
        WaitForStatus(app, "Ready");
        WaitForBranch(app, "main");
        AssertUndoRedo(app, false, PartA.NothingToUndo, false, PartA.NothingToRedo);
        Assert.Equal([" M src/app.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Compare(app, "p24a-changed-file");

        // Step 2: a real double-click on Local branches › other; git refuses the checkout (TP1).
        var checkout = ExpectOperation(app, name, PartP.CheckOutOther);
        app.DoubleClickWithMouse(app.RefRow("Local branches", "other"));
        checkout.Wait(OperationOutcome.Failed);
        await AssertErrorDialogAsync(app, PartP.CheckoutBlockedTitle, PartP.OneConflictPreventsCheckout, PartP.SwitchOtherCommand, PartP.StashAndCheckOutButton, PartP.CheckoutChangesKey, PartP.Tp1);
        Assert.Equal(["In " + PartP.Repo], app.NamesOnScreen("ErrorRepository"));
        WaitForDetails(app, "Other files", PartP.OtherFilesSha);
        WaitForStatus(app, PartP.Failed(PartP.CheckOutOther));
        AssertUndoRedo(app, false, PartA.NothingToUndo, false, PartA.NothingToRedo);
        Assert.Equal("refs/heads/main", await GitTextAsync(repo, "symbolic-ref", "-q", "HEAD"));
        Assert.Equal(PartP.MainWorkSha, await repo.HeadAsync());
        Assert.Equal(PartP.AppLocal, WorkingFile(repo, "src/app.txt"));
        Assert.Equal([" M src/app.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Empty(await GitLinesAsync(repo, "stash", "list"));
        Assert.Equal(PartP.MainWorkReflog, reflog[0]);
        Assert.Equal(reflog, await GitLinesAsync(repo, "log", "-g", "--format=%gs", "HEAD"));
        Assert.Equal(refs, await RefsAsync(repo));
        Compare(app, "p24b-one-conflict");

        // A real click on Close.
        CloseErrorDialog(app);
        AssertNoDialog(app);
        Assert.Equal(0, app.Close());
        Assert.Equal(refs, await RefsAsync(repo));
    }

    /// <summary>
    /// Check P25, part A, steps 1 and 2, then part B's step 6, in one launch, with the real mouse:
    /// the working-changes row with staged, unstaged and untracked changes (<c>p25a-changes</c>); a
    /// real right-click on "Describe the app" and a real click on "Check out this commit": no
    /// question, the automatic stash, the detached HEAD (<c>p25b-auto-stashed</c>); then Undo's help
    /// text <c>Undo checkout of f39ea2d</c> read through UI Automation and a real click on it: main
    /// checked out again, every change back with the index, the exact state as before the app
    /// started (<c>p25e-undone</c>, compared with part B's scripted picture of the same state).
    /// </summary>
    [Fact]
    public async Task Check_P25_automatic_stash_and_its_undo()
    {
        using var repo = await Scenarios.ProtectAsync();
        await MakeP25ChangesAsync(repo);
        var before = await repo.ExactStateAsync();
        using var app = Launch(repo);
        var name = NameOf(repo);
        var describeCheckout = PartP.CheckoutEntry(PartP.DescribeSha[..7]);

        // Step 1: the working-changes row: the stage panel with every kind of change.
        ClickWorkingRow(app, PartP.Lanes, "Unstaged files (3)", "Staged files (1)");
        Assert.Equal(PartP.P25Status, await GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all", "--ignored"));
        AssertUndoRedo(app, false, PartA.NothingToUndo, false, PartA.NothingToRedo);
        Compare(app, "p25a-changes");

        // Step 2: "Check out this commit" on Describe the app (commit row 3, display row 4): stashed, then checked out, no question.
        var describe = RowPoint(app, PartP.DescribeRow + 1, PartP.Lanes);
        var reported = PartP.RowPoint(PartP.DescribeRow, belowWorkingRow: true);
        AssertNear(new PointF((float)reported.X, (float)reported.Y), describe, "Describe the app's Message column");
        app.RightClickAt(describe.X, describe.Y);
        var items = app.MenuItemNames();
        Assert.Equal(PartP.CheckOutThisCommitItem, items[0]);
        Assert.True(app.FindAll("ContextMenuItem").Single(item => item.Name == PartP.CheckOutThisCommitItem).IsEnabled, "\"Check out this commit\" is disabled.");
        var checkout = ExpectOperation(app, name, PartP.CheckOut(PartP.DescribeSha));
        app.ClickMenuItem(PartP.CheckOutThisCommitItem);
        checkout.Wait(OperationOutcome.Succeeded, PartP.ChangesInAutoStash);
        WaitForStatus(app, PartP.ChangesInAutoStash);
        WaitForBranch(app, PartP.DetachedAt(PartP.DescribeSha));
        AssertNoDialog(app);
        WaitForDetails(app, "Describe the app", PartP.DescribeSha);
        AssertUndoRedo(app, true, "Undo " + describeCheckout, false, PartA.NothingToRedo);
        app.WaitForButtonState("PopButton", enabled: true, helpText: null);
        app.WaitForButtonState("StashButton", enabled: false, helpText: null);
        Assert.Null(await GitAllowingFailureTextAsync(repo, "symbolic-ref", "-q", "HEAD"));
        Assert.Equal(PartP.DescribeSha, await repo.HeadAsync());
        Assert.Equal([$"stash@{{0}}: {PartP.AutoStashListed}"], await GitLinesAsync(repo, "stash", "list"));
        Assert.Equal(PartP.AutoStashSha, await RevParseAsync(repo, "stash@{0}"));
        Assert.Equal(["scratch.txt"], await GitLinesAsync(repo, "ls-tree", "-r", "--name-only", "stash@{0}^3"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "scratch.txt")), "scratch.txt is still in the working tree.");
        Assert.Equal(PartP.CleanStatus, await GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all", "--ignored"));
        Assert.Equal(PartP.SettingsLocal, WorkingFile(repo, "settings.local.json"));
        Assert.Equal(PartP.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal([PartP.MainToDescribeReflog, PartP.ResetToHeadReflog], (await GitLinesAsync(repo, "log", "-g", "--format=%gs", "HEAD")).Take(2));
        Assert.Empty(await repo.BackupRefsAsync());
        Compare(app, "p25b-auto-stashed");

        // Part B's step 6: Undo's help text names the checkout; a real click on it.
        app.WaitForButtonState("UndoButton", enabled: true, "Undo " + describeCheckout);
        var undo = ExpectOperation(app, name, "Undo " + describeCheckout);
        app.ClickWithMouse(app.Find("UndoButton"));
        undo.Wait(OperationOutcome.Succeeded, "Undid " + describeCheckout);
        WaitForStatus(app, "Undid " + describeCheckout);
        WaitForBranch(app, "main");
        AssertNoDialog(app);
        WaitForDetails(app, "Describe the app", PartP.DescribeSha);
        AssertUndoRedo(app, false, PartA.NothingToUndo, true, "Redo " + describeCheckout);
        Assert.Equal("refs/heads/main", await GitTextAsync(repo, "symbolic-ref", "-q", "HEAD"));
        Assert.Equal(PartP.MainWorkSha, await repo.HeadAsync());
        Assert.Equal(PartP.P25Status, await GitLinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all", "--ignored"));
        Assert.Equal(before, await repo.ExactStateAsync());
        Assert.Empty(await GitLinesAsync(repo, "stash", "list"));
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal([PartP.ResetToHeadReflog, PartP.DescribeToMainReflog], (await GitLinesAsync(repo, "log", "-g", "--format=%gs", "HEAD")).Take(2));
        Compare(app, "p25e-undone");

        Assert.Equal(0, app.Close());
    }

    // ---- Part P's helpers ----

    /// <summary>P25's changes, before the app starts: src/lib.txt staged then changed again, src/app.txt changed, scratch.txt untracked.</summary>
    private static async Task MakeP25ChangesAsync(TempRepo repo)
    {
        repo.WriteFile("src/lib.txt", PartP.LibStaged);
        await repo.GitAsync("add", "--", "src/lib.txt");
        repo.WriteFile("src/lib.txt", PartP.LibStagedAndMore)
            .WriteFile("src/app.txt", PartP.AppUnstaged)
            .WriteFile("scratch.txt", PartP.Scratch);
    }

    /// <summary>Plain git's output trimmed, or null when the call fails (<c>symbolic-ref -q HEAD</c> on a detached HEAD).</summary>
    private static async Task<string?> GitAllowingFailureTextAsync(TempRepo repo, params string[] arguments)
    {
        var result = await repo.GitAllowingFailureAsync(arguments);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }
}
