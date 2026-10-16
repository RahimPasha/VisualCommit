// Part P of phase 5's scripted walk-through (docs/test-reports/phase-5.md, "Part P: protected
// changes and the automatic stash (C12)"): checks P24 to P27 of design 15.4. This class is part
// P's alone; its expected texts are in Phase5Expectations.P.cs (Phase5Expectations.PartP). One
// test method per app start, named as part P's conventions name them. This file holds P24 (refused
// checkouts); Phase5Checks.P.Stash.cs P25 (the automatic stash), Phase5Checks.P.Refused.cs P26
// and P27, and Phase5Checks.P.Helpers.cs what they share.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5;

/// <summary>The checks of part P: protected changes and the automatic stash (C12).</summary>
public sealed partial class Phase5ChecksP : Phase5Checks
{
    /// <summary>
    /// Check P24, part A: one tracked change refuses a checkout by a double-click on a branch (9.3's
    /// sentence with "Stash changes and check out"), "Create branch" with its box ticked (the same
    /// sentence, no button, no branch) and three tracked changes the branch menu's "Check out other"
    /// (<c>3 conflicts…</c>); nothing changes, no stash is made and nothing is recorded for Undo.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_P24_tracked_changes_block_a_checkout()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", PartP.AppLocal);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var refs = await RefsAsync(repo);
        var reflog = await ReflogAsync(repo);

        // Step 1: P0w, nothing selected, the status bar "Ready".
        var shot = Shot(app, "p24a-changed-file");
        AssertGraph(app, PartP.P0, PartP.Lanes, PartP.Working, showsDate: true, picture: shot);
        AssertDashedLines(app);
        AssertNothingSelected(app);
        AssertStatus(app, "Ready");
        AssertBranch(app, "main");
        await AssertLeftPanelAsync(app, stashes: 0, head: "main");
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);

        // Step 2: a double-click on Local branches › other; git refuses the checkout (TP1).
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.DoubleClick(await app.RefRowAsync("Local branches", "other"));
        await WaitForFailureAsync(app, repo, PartP.CheckOutOther, mark);
        Shot(app, "p24b-one-conflict");
        Assert.True(app.RefRowModel("Local branches", "other") is App.ViewModels.Panels.RefItemRow { IsSelected: true }, "The double-click's first click did not select other in the left panel.");
        await AssertRowSelectedAsync(app, PartP.OtherFilesRow, "Other files");
        AssertCall(app, calls, PartP.SwitchOtherCommand, 1);
        AssertNoStashCall(app, calls);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartP.CheckoutBlockedTitle, PartP.OneConflictPreventsCheckout, PartP.Repo, PartP.SwitchOtherCommand, PartP.ButtonsWithStash, PartP.Tp1, PartP.CheckoutChangesKey));
        AssertExtraButtonEnabled(app, PartP.StashAndCheckOutButton);
        AssertStatus(app, PartP.Failed(PartP.CheckOutOther), OperationStatusTone.Danger);
        await AssertOnMainAsync(repo);
        Assert.Equal(PartP.AppLocal, WorkingFile(repo, "src/app.txt"));
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        Assert.Empty(await StashListAsync(repo));
        Assert.Equal(PartP.MainWorkReflog, (await ReflogAsync(repo))[0]);
        Assert.Equal(reflog, await ReflogAsync(repo));
        Assert.Equal(refs, await RefsAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 3: "Create branch here…" on Other files with "Check out the new branch" ticked (TP2).
        var menu = RightClickCommit(app, PartP.OtherFilesRow);
        AssertItemEnabled(menu, "19a9044 Other files", PartP.CreateBranchHereItem);
        await AssertRowSelectedAsync(app, PartP.OtherFilesRow, "Other files");
        app.ClickMenuItem(PartP.CreateBranchHereItem);
        await app.WaitForDialogAsync<NameDialogViewModel>();
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The name dialog has no view.");
        Assert.Equal(PartP.CreateBranchTitle, ((TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        Assert.Equal(PartP.OtherFilesContext, ((TextBlock)app.FindByAutomationId(view, "NameDialogContext")).Text);
        var nameBox = (TextBox)app.FindByAutomationId(view, "NameBox");
        await app.WaitForAsync(() => nameBox.IsFocused, "the name box to have the keyboard focus");
        var option = (CheckBox)app.FindByAutomationId(view, "NameDialogOption");
        Assert.Equal((PartP.CheckOutNewBranchOption, (bool?)true), (option.Content as string, option.IsChecked));
        Assert.Equal("Cancel", ((Button)app.FindByAutomationId(view, "CancelButton")).Content);
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(PartP.CreateBranchTitle, confirm.Content);
        app.TypeInto("NameBox", PartP.NewBranch);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.Click(confirm);
        await WaitForFailureAsync(app, repo, PartP.CreateBranchTopic, mark);
        Shot(app, "p24c-create-branch-blocked");
        AssertCall(app, calls, PartP.CreateTopicCommand, 1);
        AssertNoStashCall(app, calls);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartP.CheckoutBlockedTitle, PartP.OneConflictPreventsCheckout, PartP.Repo, PartP.CreateTopicCommand, PartP.ButtonsWithOutput, PartP.Tp1, PartP.CheckoutChangesKey));
        AssertStatus(app, PartP.Failed(PartP.CreateBranchTopic), OperationStatusTone.Danger);
        Assert.False((await repo.GitAllowingFailureAsync("show-ref", "--verify", "refs/heads/topic")).Succeeded, "git made the branch topic.");
        await AssertLeftPanelAsync(app, stashes: 0, head: "main");
        await AssertOnMainAsync(repo);
        Assert.Equal([" M src/app.txt"], await PorcelainAsync(repo));
        Assert.Equal(reflog, await ReflogAsync(repo));
        Assert.Equal(refs, await RefsAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 4: two more files changed outside the app.
        mark = app.LogMark();
        repo.WriteFile("src/lib.txt", PartP.LibLocal).WriteFile("docs/guide.txt", PartP.GuideLocal);
        await WaitForStatusReadAsync(app, 0, 3, mark);
        Assert.True(Graph(app).ShowsWorkingRow, "The working-changes row is gone.");
        Assert.Equal([" M docs/guide.txt", " M src/app.txt", " M src/lib.txt"], await PorcelainAsync(repo));

        // Step 5: the branch menu's "Check out other" (TP3: three conflicts).
        var (selectedRow, selectedRefs) = (Repo(app).SelectedIndex, SelectedRefRows(app));
        menu = await RightClickRefAsync(app, "Local branches", "other");
        AssertItemEnabled(menu, "other", PartP.CheckOutOtherItem, first: true);
        Assert.Equal(selectedRow, Repo(app).SelectedIndex);
        Assert.Equal(selectedRefs, SelectedRefRows(app));
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartP.CheckOutOtherItem);
        await WaitForFailureAsync(app, repo, PartP.CheckOutOther, mark);
        Shot(app, "p24d-three-conflicts");
        AssertCall(app, calls, PartP.SwitchOtherCommand, 1);
        AssertNoStashCall(app, calls);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartP.CheckoutBlockedTitle, PartP.ThreeConflictsPreventCheckout, PartP.Repo, PartP.SwitchOtherCommand, PartP.ButtonsWithStash, PartP.Tp3, PartP.CheckoutChangesKey));
        AssertExtraButtonEnabled(app, PartP.StashAndCheckOutButton);
        AssertStatus(app, PartP.Failed(PartP.CheckOutOther), OperationStatusTone.Danger);
        await AssertOnMainAsync(repo);
        Assert.Equal(PartP.AppLocal, WorkingFile(repo, "src/app.txt"));
        Assert.Equal(PartP.LibLocal, WorkingFile(repo, "src/lib.txt"));
        Assert.Equal(PartP.GuideLocal, WorkingFile(repo, "docs/guide.txt"));
        Assert.Empty(await StashListAsync(repo));
        Assert.Equal(reflog, await ReflogAsync(repo));
        await AssertNoBackupAsync(repo);
        Assert.Equal(refs, await RefsAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);
    }

    /// <summary>
    /// Check P24, part B: an untracked file refuses a checkout by a double-click on the graph's
    /// label (no button), then a folder in the way (no button); nothing changes, no stash is made
    /// and nothing is recorded for Undo.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_P24_untracked_files_block_a_checkout()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("notes", PartP.MyNotes);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var refs = await RefsAsync(repo);
        var reflog = await ReflogAsync(repo);

        // At the start: P0w for "?? notes".
        AssertGraph(app, PartP.P0, PartP.Lanes, PartP.Working, showsDate: true);
        Assert.Equal(["?? notes"], await PorcelainAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);

        // Step 6: a double-click on the label other of Other files (TP4).
        var label = app.GraphLabelBounds(PartP.OtherFilesRow, "other");
        AssertBounds(label, PartP.OtherLabelBelowWorking, "The label other");
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.DoubleClick(label.Center);
        await WaitForFailureAsync(app, repo, PartP.CheckOutOther, mark);
        Shot(app, "p24e-untracked-file");
        await AssertRowSelectedAsync(app, PartP.OtherFilesRow, "Other files");
        AssertCall(app, calls, PartP.SwitchOtherCommand, 1);
        AssertNoStashCall(app, calls);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartP.CheckoutBlockedTitle, PartP.OneConflictPreventsCheckout, PartP.Repo, PartP.SwitchOtherCommand, PartP.ButtonsWithOutput, PartP.Tp4, PartP.CheckoutUntrackedKey));
        AssertStatus(app, PartP.Failed(PartP.CheckOutOther), OperationStatusTone.Danger);
        var ignored = await repo.GitWithEnvironmentAllowingFailureAsync(new Dictionary<string, string?> { ["GIT_LITERAL_PATHSPECS"] = null }, "check-ignore", "--", "notes");
        Assert.True(ignored.ExitCode == 1, $"git check-ignore exited {ignored.ExitCode} for notes: it is not ignored.");
        await AssertOnMainAsync(repo);
        Assert.Equal(PartP.MyNotes, WorkingFile(repo, "notes"));
        Assert.Equal(["?? notes"], await PorcelainAsync(repo));
        Assert.Equal(refs, await RefsAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 7: the file notes made a folder outside the app; the counts stay 0 and 1.
        mark = app.LogMark();
        File.Delete(Path.Combine(repo.Path, "notes"));
        repo.WriteFile("notes/today.txt", PartP.Today);
        await WaitForStatusReadAsync(app, 0, 1, mark, status => status.IsUntracked("notes/today.txt") && !status.IsUntracked("notes"));

        // Step 8: a double-click on Local branches › other (TP5: the folder in the way).
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.DoubleClick(await app.RefRowAsync("Local branches", "other"));
        await WaitForFailureAsync(app, repo, PartP.CheckOutOther, mark);
        Shot(app, "p24f-folder-in-the-way");
        AssertCall(app, calls, PartP.SwitchOtherCommand, 1);
        AssertNoStashCall(app, calls);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartP.CheckoutBlockedTitle, PartP.OneConflictPreventsCheckout, PartP.Repo, PartP.SwitchOtherCommand, PartP.ButtonsWithOutput, PartP.Tp5, PartP.CheckoutFolderKey));
        AssertStatus(app, PartP.Failed(PartP.CheckOutOther), OperationStatusTone.Danger);
        await AssertOnMainAsync(repo);
        Assert.Equal(PartP.Today, WorkingFile(repo, "notes/today.txt"));
        Assert.Equal(["?? notes/today.txt"], await GitLinesAsync(repo, "status", "--porcelain", "--untracked-files=all"));
        Assert.Equal(reflog, await ReflogAsync(repo));
        Assert.Empty(await StashListAsync(repo));
        await AssertNoBackupAsync(repo);
        Assert.Equal(refs, await RefsAsync(repo));
        AssertUndoRedo(app, UndoRedoButtons.Nothing);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);
    }
}
