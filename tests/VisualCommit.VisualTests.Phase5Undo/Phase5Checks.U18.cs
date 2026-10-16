// Part U, check U18 (docs/test-reports/phase-5.md): undo and redo of branch operations: create,
// create and check out, rename, delete (its upstream back), a checkout of a branch, a checkout that
// created a tracking branch, a checkout of a commit ("ops"), a checkout that left a detached HEAD's
// commit and a fast-forward of a branch not checked out ("edges"), and D91's "Stash changes and
// check out" ("blocked"). One method per action, each on a fresh copy (design 15.4, UD17); the
// actions are phase 3's checks 6, 13, 13b, 14, 30 and 32.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase5;
using Xunit;
using static VisualCommit.VisualTests.Phase5.Phase5Expectations;

namespace VisualCommit.VisualTests.Phase5Undo;

public sealed partial class Phase5ChecksU
{
    /// <summary>Check U18, part A: hotfix created at Main work, not checked out; undone (the ref and its reflog gone) and redone ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U18_create_branch()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 1: "Create branch here…" on Main work, the box unticked.
        var calls = await CreateHotfixAsync(app, checkOut: false);
        AssertCalled(app, calls, PartU.Git("branch", "hotfix", PartU.MainWorkSha));
        AssertStatus(app, PartU.StatusCreatedHotfix);
        Assert.Equal(PartU.MainWorkSha, await RevParseAsync(repo, "refs/heads/hotfix"));
        Assert.Empty(await ConfigAsync(repo, @"^branch\.hotfix\."));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        AssertRowSelected(app, 3, PartU.MainWorkSha);
        AssertGraph(app, PartU.G0Hotfix, PartU.OpsLanes, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.HotfixText));

        // Step 2: Undo: the transaction deletes hotfix; no configuration call.
        calls = await UndoAsync(app, repo, PartU.HotfixText);
        var shot = Shot(app, "u18a-create-undone");
        AssertTransaction(app, calls, PartU.Undo.Message(PartU.HotfixText), [PartU.DeleteLine("refs/heads/hotfix", PartU.MainWorkSha)]);
        AssertNoCall(app, calls, "git config");
        Assert.Null(await RevParseAsync(repo, "refs/heads/hotfix"));
        Assert.False(await ReflogExistsAsync(repo, "refs/heads/hotfix"), "hotfix still has a reflog.");
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertRowSelected(app, 3, PartU.MainWorkSha);
        Assert.Equal(4, SectionCount(app, "Local branches"));
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.HotfixText));

        // Step 3: Redo: the transaction creates it again, with a reflog of one entry.
        calls = await RedoAsync(app, repo, PartU.HotfixText);
        shot = Shot(app, "u18b-create-redone");
        AssertTransaction(app, calls, PartU.Redo.Message(PartU.HotfixText), [PartU.CreateLine("refs/heads/hotfix", PartU.MainWorkSha)]);
        Assert.Equal(PartU.MainWorkSha, await RevParseAsync(repo, "refs/heads/hotfix"));
        Assert.Equal([PartU.Redo.Message(PartU.HotfixText)], await ReflogAsync(repo, "refs/heads/hotfix", 10));
        AssertGraph(app, PartU.G0Hotfix, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.HotfixText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U18, part B: hotfix created and checked out; undone (switch back, then the branch deleted) and redone (the branch first, then the switch) ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U18_create_and_check_out()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 4: as step 1, the box left ticked.
        var calls = await CreateHotfixAsync(app, checkOut: true);
        AssertCalled(app, calls, PartU.Git("switch", "--create", "hotfix", PartU.MainWorkSha));
        AssertStatus(app, PartU.StatusCreatedHotfixCheckedOut);
        Assert.Equal(["## hotfix"], await StatusAsync(repo));
        AssertBranch(app, "hotfix");
        AssertGraph(app, PartU.G0HotfixCurrent, PartU.OpsLanes, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.HotfixText));

        // Step 5: Undo: the switch to main, then hotfix deleted.
        calls = await UndoAsync(app, repo, PartU.HotfixText);
        var shot = Shot(app, "u18c-create-checkout-undone");
        AssertWrites(
            app,
            calls,
            ["git switch", "git update-ref"],
            PartU.UndoSwitch("main"),
            PartU.Transaction(PartU.Undo.Message(PartU.HotfixText)));
        AssertTransaction(app, calls, PartU.Undo.Message(PartU.HotfixText), [PartU.DeleteLine("refs/heads/hotfix", PartU.MainWorkSha)]);
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        AssertBranch(app, "main");
        Assert.Equal(["checkout: moving from hotfix to main"], await ReflogAsync(repo, "HEAD", 1));
        Assert.Null(await RevParseAsync(repo, "refs/heads/hotfix"));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.HotfixText));

        // Step 6: Redo: hotfix created first, then the switch to it.
        calls = await RedoAsync(app, repo, PartU.HotfixText);
        shot = Shot(app, "u18d-create-checkout-redone");
        AssertWrites(
            app,
            calls,
            ["git switch", "git update-ref"],
            PartU.Transaction(PartU.Redo.Message(PartU.HotfixText)),
            PartU.UndoSwitch("hotfix"));
        AssertTransaction(app, calls, PartU.Redo.Message(PartU.HotfixText), [PartU.CreateLine("refs/heads/hotfix", PartU.MainWorkSha)]);
        Assert.Equal(["## hotfix"], await StatusAsync(repo));
        Assert.Equal(["checkout: moving from main to hotfix"], await ReflogAsync(repo, "HEAD", 1));
        AssertGraph(app, PartU.G0HotfixCurrent, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.HotfixText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U18, part C: feature/conflict renamed from its label's menu, undone and redone with <c>git branch --move</c> ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U18_rename()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        var conflicting = await RevParseAsync(repo, "refs/heads/feature/conflict");

        // Step 7: the label's "Rename feature/conflict…", the name replaced, "Rename".
        RightClickLabelAndChoose(app, 1, "feature/conflict", "Rename feature/conflict…");
        await app.WaitForDialogAsync<NameDialogViewModel>();
        app.ReplaceText("NameBox", "feature/conflicting");
        await ActAsync(app, () => app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton")));
        AssertStatus(app, PartU.StatusRenamed);
        AssertRowSelected(app, 1, conflicting!);
        AssertGraph(app, PartU.G0Conflicting, PartU.OpsLanes, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RenameText));

        // Step 8: Undo: the branch moved back; its reflog moved with it.
        var calls = await UndoAsync(app, repo, PartU.RenameText);
        var shot = Shot(app, "u18e-rename-undone");
        AssertCalled(app, calls, PartU.Git("branch", "--move", "feature/conflicting", "feature/conflict"));
        Assert.Equal(["  feature/alpha", "  feature/conflict", "  feature/ff", "* main"], await GitLinesAsync(repo, "branch"));
        Assert.Equal(["Branch: renamed refs/heads/feature/conflicting to refs/heads/feature/conflict"], await ReflogAsync(repo, "refs/heads/feature/conflict", 1));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertRowSelected(app, 1, conflicting!);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.RenameText));

        // Step 9: Redo: renamed again.
        calls = await RedoAsync(app, repo, PartU.RenameText);
        shot = Shot(app, "u18f-rename-redone");
        AssertCalled(app, calls, PartU.Git("branch", "--move", "feature/conflict", "feature/conflicting"));
        Assert.Equal(["  feature/alpha", "  feature/conflicting", "  feature/ff", "* main"], await GitLinesAsync(repo, "branch"));
        AssertGraph(app, PartU.G0Conflicting, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.RenameText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U18, part D: feature/alpha deleted with its upstream; undone (the branch with a reflog, its upstream keys back) and redone ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U18_delete_with_upstream()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        var backup = PartU.BackupRefName("branch", 1, "/feature/alpha");

        // Step 10: the label's "Delete feature/alpha…", phase 3's question, "Delete".
        RightClickLabelAndChoose(app, 4, "feature/alpha", "Delete feature/alpha…");
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        AssertQuestionTexts(app, PartU.DeleteBranchTitle, PartU.DeleteAlphaQuestion, PartU.DeleteBranchConfirm);
        var calls = await ActAsync(app, () => app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton")));
        AssertCalled(app, calls, PartU.Git("branch", "--delete", "--force", "feature/alpha"));
        AssertStatus(app, PartU.StatusDeletedAlpha);
        Assert.Equal([$"{backup} {PartU.AlphaTwoSha}"], await BackupLinesAsync(repo));
        await AssertAlphaGoneAsync(app, repo);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DeleteAlphaText));

        // Step 11: Undo: the branch created with a reflog, then its upstream keys.
        calls = await UndoAsync(app, repo, PartU.DeleteAlphaText);
        var shot = Shot(app, "u18g-delete-undone");
        AssertWrites(
            app,
            calls,
            ["git update-ref", "git config"],
            PartU.Transaction(PartU.Undo.Message(PartU.DeleteAlphaText), createReflog: true),
            PartU.Git("config", "--local", "--add", "branch.feature/alpha.remote", "origin"),
            PartU.Git("config", "--local", "--add", "branch.feature/alpha.merge", "refs/heads/feature/alpha"));
        AssertTransaction(app, calls, PartU.Undo.Message(PartU.DeleteAlphaText), [PartU.CreateLine("refs/heads/feature/alpha", PartU.AlphaTwoSha)], createReflog: true);
        Assert.Equal(PartU.AlphaTwoSha, await RevParseAsync(repo, "refs/heads/feature/alpha"));
        Assert.Equal("origin/feature/alpha", await GitTextAsync(repo, "for-each-ref", "--format=%(upstream:short)", "refs/heads/feature/alpha"));
        Assert.Equal("0\t0", await GitTextAsync(repo, "rev-list", "--left-right", "--count", "feature/alpha...origin/feature/alpha"));
        Assert.Equal([PartU.Undo.Message(PartU.DeleteAlphaText)], await ReflogAsync(repo, "refs/heads/feature/alpha", 10));
        Assert.Equal([$"{backup} {PartU.AlphaTwoSha}"], await BackupLinesAsync(repo));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        Assert.Equal(4, SectionCount(app, "Local branches"));
        await OpenFolderAsync(app, "Local branches", "feature");
        AssertArrows(app, 0, 0, "Local branches", "feature", "alpha");
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.DeleteAlphaText));

        // Step 12: Redo: the transaction deletes it, then its configuration section (UD20).
        calls = await RedoAsync(app, repo, PartU.DeleteAlphaText);
        Shot(app, "u18h-delete-redone");
        AssertWrites(
            app,
            calls,
            ["git update-ref", "git config"],
            PartU.Transaction(PartU.Redo.Message(PartU.DeleteAlphaText)),
            PartU.Git("config", "--local", "--remove-section", "branch.feature/alpha"));
        AssertTransaction(app, calls, PartU.Redo.Message(PartU.DeleteAlphaText), [PartU.DeleteLine("refs/heads/feature/alpha", PartU.AlphaTwoSha)]);
        await AssertAlphaGoneAsync(app, repo);
        Assert.Equal([$"{backup} {PartU.AlphaTwoSha}"], await BackupLinesAsync(repo));
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.DeleteAlphaText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U18, part E: feature/ff checked out by a double-click in the left panel; undone and redone with the undo's switch ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U18_check_out_branch()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 13: the folder Local branches › feature opened, its ff double-clicked.
        await OpenFolderAsync(app, "Local branches", "feature");
        var row = await app.RefRowAsync("Local branches", "feature", "ff");
        var calls = await ActAsync(app, () => app.DoubleClick(row));
        AssertCalled(app, calls, PartU.Git("switch", "--no-guess", "feature/ff"));
        Assert.True(((RefItemRow)app.RefRowModel("Local branches", "feature", "ff")).IsSelected, "ff is not selected in the left panel.");
        AssertRowSelected(app, 2, PartU.FastForwardMeSha);
        AssertStatus(app, PartU.StatusCheckedOutFf);
        Assert.Equal(["## feature/ff"], await StatusAsync(repo));
        AssertGraph(app, PartU.G0FfCurrent, PartU.OpsLanes, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CheckoutFfText));

        // Step 14: Undo: the switch back to main.
        calls = await UndoAsync(app, repo, PartU.CheckoutFfText);
        var shot = Shot(app, "u18i-checkout-undone");
        AssertWrites(app, calls, ["git switch", "git update-ref"], PartU.UndoSwitch("main"));
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        Assert.Equal(["checkout: moving from feature/ff to main"], await ReflogAsync(repo, "HEAD", 1));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertRowSelected(app, 2, PartU.FastForwardMeSha);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.CheckoutFfText));

        // Step 15: Redo: the switch to feature/ff.
        calls = await RedoAsync(app, repo, PartU.CheckoutFfText);
        shot = Shot(app, "u18j-checkout-redone");
        AssertWrites(app, calls, ["git switch", "git update-ref"], PartU.UndoSwitch("feature/ff"));
        Assert.Equal(["## feature/ff"], await StatusAsync(repo));
        AssertGraph(app, PartU.G0FfCurrent, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CheckoutFfText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U18, part F: origin/feature/alpha double-clicked with no local feature/alpha (phase 3's
    /// check 6): the tracking branch created and checked out; undone (switch back, the branch and its
    /// keys gone) and redone ("ops" with feature/alpha deleted outside the app).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U18_tracking_branch()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.GitAsync("branch", "--delete", "--force", "feature/alpha");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        AssertGraph(app, PartU.G0WithoutAlpha, PartU.OpsLanes, showsDate: true);
        Assert.Equal(3, SectionCount(app, "Local branches"));

        // Step 16: Remotes › origin › feature opened, its alpha double-clicked.
        await OpenFolderAsync(app, "Remotes", "origin", "feature");
        var row = await app.RefRowAsync("Remotes", "origin", "feature", "alpha");
        var calls = await ActAsync(app, () => app.DoubleClick(row));
        AssertCalled(app, calls, PartU.Git("switch", "--create", "feature/alpha", "--track", "refs/remotes/origin/feature/alpha"));
        AssertStatus(app, PartU.StatusTrackingAlpha);
        Assert.Equal(["## feature/alpha...origin/feature/alpha"], await StatusAsync(repo));
        Assert.Equal(PartU.AlphaUpstreamKeys, await ConfigAsync(repo, @"^branch\.feature/alpha\."));
        AssertGraph(app, PartU.G0AlphaCurrent, PartU.OpsLanes, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CheckoutRemoteAlphaText));

        // Step 17: Undo: switch back, the branch deleted, then its keys.
        calls = await UndoAsync(app, repo, PartU.CheckoutRemoteAlphaText);
        var shot = Shot(app, "u18k-tracking-undone");
        AssertWrites(
            app,
            calls,
            ["git switch", "git update-ref", "git config"],
            PartU.UndoSwitch("main"),
            PartU.Transaction(PartU.Undo.Message(PartU.CheckoutRemoteAlphaText)),
            PartU.Git("config", "--local", "--remove-section", "branch.feature/alpha"));
        AssertTransaction(app, calls, PartU.Undo.Message(PartU.CheckoutRemoteAlphaText), [PartU.DeleteLine("refs/heads/feature/alpha", PartU.AlphaTwoSha)]);
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        Assert.Null(await RevParseAsync(repo, "refs/heads/feature/alpha"));
        Assert.Empty(await ConfigAsync(repo, @"^branch\.feature/alpha\."));
        AssertGraph(app, PartU.G0WithoutAlpha, PartU.OpsLanes, showsDate: true, picture: shot);
        Assert.Equal(3, SectionCount(app, "Local branches"));
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.CheckoutRemoteAlphaText));

        // Step 18: Redo: the branch first (the switch needs it), the switch, then the keys (UD20).
        calls = await RedoAsync(app, repo, PartU.CheckoutRemoteAlphaText);
        shot = Shot(app, "u18l-tracking-redone");
        AssertWrites(
            app,
            calls,
            ["git switch", "git update-ref", "git config"],
            PartU.Transaction(PartU.Redo.Message(PartU.CheckoutRemoteAlphaText)),
            PartU.UndoSwitch("feature/alpha"),
            PartU.Git("config", "--local", "--add", "branch.feature/alpha.remote", "origin"),
            PartU.Git("config", "--local", "--add", "branch.feature/alpha.merge", "refs/heads/feature/alpha"));
        AssertTransaction(app, calls, PartU.Redo.Message(PartU.CheckoutRemoteAlphaText), [PartU.CreateLine("refs/heads/feature/alpha", PartU.AlphaTwoSha)]);
        Assert.Equal(["## feature/alpha...origin/feature/alpha"], await StatusAsync(repo));
        Assert.Equal(PartU.AlphaUpstreamKeys, await ConfigAsync(repo, @"^branch\.feature/alpha\."));
        AssertGraph(app, PartU.G0AlphaCurrent, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CheckoutRemoteAlphaText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U18, part G: "Check out this commit" on Add core (a clean tree: no stash, 9.1); undone and redone ("ops").</summary>
    [AvaloniaFact]
    public async Task Check_U18_check_out_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);

        // Step 19: "Check out this commit" on Add core: no question.
        var calls = await ActAsync(app, () => RightClickRowAndChoose(app, 6, new Point(484, 285), "Check out this commit"));
        AssertCalled(app, calls, PartU.UndoDetach(PartU.AddCoreSha));
        AssertNoCall(app, calls, "git stash");
        AssertStatus(app, PartU.StatusDetachedAtAddCore);
        AssertBranch(app, "Detached at 8beca34");
        Assert.Equal(["## HEAD (no branch)"], await StatusAsync(repo));
        AssertGraph(app, PartU.G0HeadAtAddCore, PartU.OpsLanes, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CheckoutAddCoreText));

        // Step 20: Undo: the switch back to main.
        calls = await UndoAsync(app, repo, PartU.CheckoutAddCoreText);
        var shot = Shot(app, "u18m-commit-checkout-undone");
        AssertWrites(app, calls, ["git switch", "git update-ref"], PartU.UndoSwitch("main"));
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        Assert.Equal([$"checkout: moving from {PartU.AddCoreSha} to main"], await ReflogAsync(repo, "HEAD", 1));
        AssertGraph(app, PartU.G0, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.CheckoutAddCoreText));

        // Step 21: Redo: detached at Add core again.
        calls = await RedoAsync(app, repo, PartU.CheckoutAddCoreText);
        shot = Shot(app, "u18n-commit-checkout-redone");
        AssertWrites(app, calls, ["git switch", "git update-ref"], PartU.UndoDetach(PartU.AddCoreSha));
        Assert.Equal(["## HEAD (no branch)"], await StatusAsync(repo));
        AssertBranch(app, "Detached at 8beca34");
        AssertGraph(app, PartU.G0HeadAtAddCore, PartU.OpsLanes, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CheckoutAddCoreText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U18, part H: main checked out from a detached HEAD whose commit it leaves (phase 3's
    /// question and its detached- backup); undone and redone without the question or another backup
    /// (UD7) ("edges").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U18_leave_detached_commit()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        AssertGraph(app, PartU.E0, 2, showsDate: true);
        var backup = $"{PartU.BackupRefName("detached", 1)} {PartU.DetachedWorkSha}";

        // Step 22: main double-clicked under Local branches; "Check out" in the question.
        app.DoubleClick(await app.RefRowAsync("Local branches", "main"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        AssertQuestionTexts(app, PartU.LeaveTitle, PartU.LeaveQuestion, "Check out");
        var calls = await ActAsync(app, () => app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton")));
        AssertCalled(app, calls, PartU.Git("switch", "--no-guess", "main"));
        AssertStatus(app, PartU.StatusCheckedOutMain);
        Assert.Equal([backup], await BackupLinesAsync(repo));
        Assert.Equal(["## main...remotes/origin/main"], await StatusAsync(repo));
        AssertGraph(app, PartU.E1, 1, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CheckoutMainText));

        // Step 23: Undo, no question: detached at Detached work again; the backup kept, no other.
        calls = await UndoAsync(app, repo, PartU.CheckoutMainText);
        var shot = Shot(app, "u18o-detached-undone");
        AssertWrites(app, calls, ["git switch", "git update-ref"], PartU.UndoDetach(PartU.DetachedWorkSha));
        Assert.Equal(PartU.DetachedWorkSha, await repo.HeadAsync());
        Assert.Equal(["## HEAD (no branch)"], await StatusAsync(repo));
        AssertBranch(app, "Detached at 419190f");
        Assert.Equal([$"checkout: moving from main to {PartU.DetachedWorkSha}"], await ReflogAsync(repo, "HEAD", 1));
        AssertGraph(app, PartU.E0, 2, showsDate: true, picture: shot);
        Assert.Equal([backup], await BackupLinesAsync(repo));
        await AssertSideInWorktreeAsync(repo);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.CheckoutMainText));

        // Step 24: Redo, no question, no second backup.
        calls = await RedoAsync(app, repo, PartU.CheckoutMainText);
        shot = Shot(app, "u18p-detached-redone");
        AssertWrites(app, calls, ["git switch", "git update-ref"], PartU.UndoSwitch("main"));
        Assert.Equal(["## main...remotes/origin/main"], await StatusAsync(repo));
        AssertGraph(app, PartU.E1, 1, showsDate: true, picture: shot);
        Assert.Equal([backup], await BackupLinesAsync(repo));
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CheckoutMainText));
        AssertNoClippedText(app);
    }

    /// <summary>Check U18, part I: loc fast-forwarded to main (its upstream, a local branch) with HEAD detached; undone and redone in the transaction ("edges").</summary>
    [AvaloniaFact]
    public async Task Check_U18_fast_forward()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        AssertArrows(app, 0, 1, "Local branches", "loc");

        // Step 25: loc's "Fast-forward loc to main".
        app.RightClick(await app.RefRowAsync("Local branches", "loc"));
        app.Menu();
        var calls = await ActAsync(app, () => app.ClickMenuItem("Fast-forward loc to main"));
        var fetch = AssertCalled(app, calls, PartU.Git("fetch", "--no-auto-maintenance", "--no-write-fetch-head", ".", "refs/heads/main:refs/heads/loc"));
        if (await IsReportGitAsync())
        {
            Assert.Contains(PartU.FastForwardLocLine, fetch.StandardError.Split(['\r', '\n']).Select(line => line.TrimEnd()));
        }

        AssertStatus(app, PartU.StatusFastForwardedLoc);
        Assert.Equal(PartU.EdgesMainWorkSha, await RevParseAsync(repo, "refs/heads/loc"));
        AssertArrows(app, 0, 0, "Local branches", "loc");
        Assert.Equal(PartU.DetachedWorkSha, await repo.HeadAsync());
        AssertGraph(app, PartU.E0Loc, 2, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.FastForwardLocText));

        // Step 26: Undo: loc back in the transaction.
        calls = await UndoAsync(app, repo, PartU.FastForwardLocText);
        var shot = Shot(app, "u18q-ff-undone");
        AssertTransaction(app, calls, PartU.Undo.Message(PartU.FastForwardLocText), [PartU.UpdateLine("refs/heads/loc", PartU.EdgesInitialSha, PartU.EdgesMainWorkSha)]);
        Assert.Equal(PartU.EdgesInitialSha, await RevParseAsync(repo, "refs/heads/loc"));
        AssertArrows(app, 0, 1, "Local branches", "loc");
        Assert.Equal([PartU.Undo.Message(PartU.FastForwardLocText)], await ReflogAsync(repo, "refs/heads/loc", 1));
        AssertGraph(app, PartU.E0, 2, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.FastForwardLocText));

        // Step 27: Redo.
        calls = await RedoAsync(app, repo, PartU.FastForwardLocText);
        shot = Shot(app, "u18r-ff-redone");
        AssertTransaction(app, calls, PartU.Redo.Message(PartU.FastForwardLocText), [PartU.UpdateLine("refs/heads/loc", PartU.EdgesMainWorkSha, PartU.EdgesInitialSha)]);
        Assert.Equal(PartU.EdgesMainWorkSha, await RevParseAsync(repo, "refs/heads/loc"));
        AssertGraph(app, PartU.E0Loc, 2, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.FastForwardLocText));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check U18, part J: D91's "Stash changes and check out" from C12's refusal of feature/other;
    /// undone and redone with the step's own stash round trip, the changes byte for byte ("blocked").
    /// </summary>
    [AvaloniaFact]
    public async Task Check_U18_stash_and_check_out()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        AssertButtons(app, UndoRedoButtons.Nothing);
        AssertGraph(app, PartU.G14a, 2, PartU.WorkingRow, showsDate: true);
        var notesBefore = File.ReadAllBytes(Path.Combine(repo.Path, "notes.txt"));

        // Step 28: other double-clicked; C12's dialog; "Stash changes and check out".
        await OpenFolderAsync(app, "Local branches", "feature");
        app.DoubleClick(await app.RefRowAsync("Local branches", "feature", "other"));
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.True(((RefItemRow)app.RefRowModel("Local branches", "feature", "other")).IsSelected, "other is not selected in the left panel.");
        AssertRowSelected(app, 2, PartU.OtherChangeSha);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartU.CheckoutBlockedTitle,
            PartU.OneConflictPreventsCheckout,
            NameOf(repo),
            PartU.Git("switch", "--no-guess", "feature/other"),
            ["Copy output", PartU.StashAndCheckOut, "Close"],
            PartU.T2,
            PartU.T2Key));
        var calls = await ActAsync(app, () => app.Click(app.FindByAutomationId(app.TopDialogView!, "ErrorExtraButton")));
        AssertWrites(
            app,
            calls,
            ["git stash push", "git stash pop", "git switch"],
            PartU.Git("stash", "push", "--message", PartU.D91StashMessage),
            PartU.Git("switch", "--no-guess", "feature/other"),
            PartU.Git("stash", "pop", "--index", "stash@{0}"));
        AssertStatus(app, PartU.StatusCheckedOutOtherWithChanges);
        await AssertOnOtherWithChangesAsync(repo);
        AssertGraph(app, PartU.G14b, 2, PartU.WorkingRow, showsDate: true);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CheckoutOtherText));

        // Step 29: Undo: the step's own stash (tracked files only), the switch back, the pop.
        calls = await UndoAsync(app, repo, PartU.CheckoutOtherText);
        var shot = Shot(app, "u18s-stash-checkout-undone");
        AssertWrites(
            app,
            calls,
            ["git stash push", "git stash pop", "git switch"],
            PartU.Git("stash", "push"),
            PartU.UndoSwitch("main"),
            PartU.Git("stash", "pop", "--index", "stash@{0}"));
        var pop = app.GitCalls().Skip(calls).Last(call => call.CommandText.StartsWith("git stash pop", StringComparison.Ordinal));
        if (await IsReportGitAsync())
        {
            Assert.Contains("Dropped stash@{0} (" + PartU.UndoStashShort, pop.StandardOutput, StringComparison.Ordinal);
        }

        Assert.Equal(["## main", " M notes.txt", "?? newfile.txt"], await StatusAsync(repo));
        Assert.Equal(notesBefore, File.ReadAllBytes(Path.Combine(repo.Path, "notes.txt")));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NewFileUntracked, WorkingFile(repo, "newfile.txt"));
        Assert.Empty(await repo.StashListAsync());
        Assert.Equal(["checkout: moving from feature/other to main", "reset: moving to HEAD"], await ReflogAsync(repo, "HEAD", 2));
        AssertGraph(app, PartU.G14a, 2, PartU.WorkingRow, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanRedo(PartU.CheckoutOtherText));

        // Step 30: Redo: the same round trip to feature/other.
        calls = await RedoAsync(app, repo, PartU.CheckoutOtherText);
        shot = Shot(app, "u18t-stash-checkout-redone");
        AssertWrites(
            app,
            calls,
            ["git stash push", "git stash pop", "git switch"],
            PartU.Git("stash", "push"),
            PartU.UndoSwitch("feature/other"),
            PartU.Git("stash", "pop", "--index", "stash@{0}"));
        await AssertOnOtherWithChangesAsync(repo);
        AssertGraph(app, PartU.G14b, 2, PartU.WorkingRow, showsDate: true, picture: shot);
        AssertButtons(app, UndoRedoButtons.CanUndo(PartU.CheckoutOtherText));
        AssertNoClippedText(app);
    }

    // ---- U18's own helpers ----

    /// <summary>
    /// Steps 1 and 4: Main work (row 3, (484, 207)) right-clicked in its Message column,
    /// "Create branch here…", <c>hotfix</c> typed, the box unticked or left ticked, "Create
    /// branch"; waits until the operation has ended. Returns the call mark.
    /// </summary>
    private static async Task<int> CreateHotfixAsync(ShellDriver app, bool checkOut)
    {
        RightClickRowAndChoose(app, 3, new Point(484, 207), PartU.CreateBranchMenuItem);
        await app.WaitForDialogAsync<NameDialogViewModel>();
        app.TypeInto("NameBox", "hotfix");
        var option = (CheckBox)app.FindByAutomationId(app.TopDialogView!, "NameDialogOption");
        Assert.True(option.IsChecked, "\"Check out the new branch\" is not ticked as the dialog opens.");
        if (!checkOut)
        {
            app.Click(option);
            Assert.False(option.IsChecked, "\"Check out the new branch\" is still ticked.");
        }

        return await ActAsync(app, () => app.Click(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton")));
    }

    /// <summary>A question on top: its title, question and confirm button's label.</summary>
    private static void AssertQuestionTexts(ShellDriver app, string title, string question, string confirm)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No question is open.");
        Assert.Equal(title, ((TextBlock)app.FindByAutomationId(view, "ConfirmTitle")).Text);
        Assert.Equal(question, ((TextBlock)app.FindByAutomationId(view, "ConfirmQuestion")).Text);
        Assert.Equal(confirm, ((Button)app.FindByAutomationId(view, "ConfirmButton")).Content);
    }

    /// <summary>Clicks a closed left-panel folder at <paramref name="path"/> and waits until it is open.</summary>
    private static async Task OpenFolderAsync(ShellDriver app, params string[] path)
    {
        if (app.RefRowModel(path).IsOpen)
        {
            return;
        }

        app.Click(await app.RefRowAsync(path));
        await app.WaitForAsync(() => app.RefRowModel(path).IsOpen, $"the folder {string.Join(" › ", path)} to open");
    }

    /// <summary>After U18 steps 10 and 12: no local feature/alpha, no key and no reflog of it; G0 −alpha; Local branches 3.</summary>
    private static async Task AssertAlphaGoneAsync(ShellDriver app, TempRepo repo)
    {
        Assert.Null(await RevParseAsync(repo, "refs/heads/feature/alpha"));
        Assert.Empty(await ConfigAsync(repo, @"^branch\.feature/alpha\."));
        Assert.False(await ReflogExistsAsync(repo, "refs/heads/feature/alpha"), "feature/alpha still has a reflog.");
        AssertGraph(app, PartU.G0WithoutAlpha, PartU.OpsLanes, showsDate: true);
        Assert.Equal(3, SectionCount(app, "Local branches"));
    }

    /// <summary>The linked worktree edges-wt still has side checked out.</summary>
    private static async Task AssertSideInWorktreeAsync(TempRepo repo)
    {
        var worktrees = await GitLinesAsync(repo, "worktree", "list", "--porcelain");
        var at = Array.FindIndex(worktrees, line => line.StartsWith("worktree ", StringComparison.Ordinal) && line.EndsWith("/" + Scenarios.EdgeCasesWorktree, StringComparison.Ordinal));
        Assert.True(at >= 0, "No worktree edges-wt:\n" + string.Join('\n', worktrees));
        Assert.Contains("branch refs/heads/side", worktrees.Skip(at).TakeWhile(line => line.Length > 0));
    }

    /// <summary>After U18 steps 28 and 30: on feature/other with the local change re-applied, newfile.txt untouched, no stash.</summary>
    private static async Task AssertOnOtherWithChangesAsync(TempRepo repo)
    {
        Assert.Equal(["## feature/other", " M notes.txt", "?? newfile.txt"], await StatusAsync(repo));
        Assert.Equal(PartU.NotesLocalOnOther, WorkingFile(repo, "notes.txt"));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NewFileUntracked, WorkingFile(repo, "newfile.txt"));
        Assert.Empty(await repo.StashListAsync());
    }
}
