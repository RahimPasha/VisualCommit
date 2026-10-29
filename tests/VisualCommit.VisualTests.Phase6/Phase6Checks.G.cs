// Part G of phase 6's scripted walk-through (docs/test-reports/phase-6.md, "Part G: git behaviour:
// ignored files kept, and fetch refspecs"): checks G22 to G25 of design 17.4. This class is part
// G's alone; its expected texts are in Phase6Expectations.G.cs (Phase6Expectations.PartG). One test
// method per app start, named as part G's "Test methods and app starts" names them (GD1). This file
// holds G22 (checkouts refused for an ignored file), G23 (the app's own check before git, parts A
// and B) and G24 (Keep ignored files off); Phase6Checks.G.Refspecs.cs holds G25 (fetch refspecs),
// and Phase6Checks.G.Helpers.cs what they share.
using Avalonia.Headless.XUnit;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

/// <summary>The checks of part G: ignored files kept (G22 to G24) and fetch refspecs (G25).</summary>
public sealed partial class Phase6ChecksG : Phase6Checks
{
    /// <summary>
    /// Check G22: every checkout path refuses to overwrite or delete an ignored file (D193, design
    /// 9.2's first row, 9.11, 9.15): a local branch by a double-click on its graph label; a remote
    /// branch whose local branch exists (D93); a remote branch as a new tracking branch; "Create
    /// branch here…" with its box ticked; an ignored folder where the target has a file; a commit
    /// <c>main</c> does not reach, detached from the commit menu. Each refused by git with
    /// <c>--no-overwrite-ignore</c>, told in D179's words with git's count, nothing changed.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G22_checkouts_refused_for_ignored_files()
    {
        using var repo = await Scenarios.IgnoredAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: IG0; the exact state read.
        var exact = await repo.ExactStateAsync();
        var reflog = await ReflogAsync(repo);
        AssertIg0(app);
        AssertBranch(app, "main");
        AssertNothingToUndo(app);
        Assert.Equal(PartG.IgnoredStatus, await IgnoredStatusAsync(repo));
        Assert.Equal(PartG.Ig0ReflogEntries, reflog.Length);

        // Step 2: a double-click on the graph label vendor (row 3); git refuses (TG1).
        var label = app.GraphLabelBounds(PartG.VendorSecretRow, "vendor");
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.DoubleClick(label.Center);
        await WaitForFailureAsync(app, repo, PartG.CheckOutVendor, mark);
        Shot(app, "g22a-vendor-blocked");
        await AssertRowSelectedAsync(app, PartG.VendorSecretRow, "Add the vendor secret");
        await AssertRefusedCheckoutCallsAsync(app, calls, PartG.SwitchVendorCommand, PartG.Tg1, PartG.UntrackedKey, PartG.SecretIgnored);
        await AssertCheckoutBlockedAsync(app, PartG.IgnoredFileInTheWay("vendor"), PartG.SwitchVendorCommand, PartG.Tg1, PartG.UntrackedKey);
        AssertStatus(app, PartG.Failed(PartG.CheckOutVendor), OperationStatusTone.Danger);
        AssertBranch(app, "main");
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Equal(PartG.SecretLocal, WorkingFile(repo, "secret.env"));
        Assert.Equal(reflog, await ReflogAsync(repo));
        AssertNothingToUndo(app);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 3: Remotes › origin › vendor's checkout goes into the local vendor (D93, 10.6).
        var (menu, _) = await RightClickRefAsync(app, "Remotes", "origin", "vendor");
        await AssertRowSelectedAsync(app, PartG.VendorSecretRow, "Add the vendor secret");
        AssertItemEnabled(menu, "origin/vendor", PartG.CheckOutOriginVendor);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.CheckOutOriginVendor);
        await WaitForFailureAsync(app, repo, PartG.CheckOutOriginVendor, mark);
        Shot(app, "g22b-origin-vendor-blocked");
        await AssertRefusedCheckoutCallsAsync(app, calls, PartG.SwitchVendorCommand, PartG.Tg1, PartG.UntrackedKey, PartG.SecretIgnored);
        AssertNoCallContaining(app, calls, "--create", "--track");
        await AssertCheckoutBlockedAsync(app, PartG.IgnoredFileInTheWay("origin/vendor"), PartG.SwitchVendorCommand, PartG.Tg1, PartG.UntrackedKey);
        AssertStatus(app, PartG.Failed(PartG.CheckOutOriginVendor), OperationStatusTone.Danger);
        Assert.Equal(exact, await repo.ExactStateAsync());
        AssertNothingToUndo(app);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 4: Remotes › origin › vendor-remote as a new tracking branch.
        (menu, _) = await RightClickRefAsync(app, "Remotes", "origin", "vendor-remote");
        AssertItemEnabled(menu, "origin/vendor-remote", PartG.CheckOutVendorRemote);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.CheckOutVendorRemote);
        await WaitForFailureAsync(app, repo, PartG.CheckOutVendorRemote, mark);
        Shot(app, "g22c-tracking-checkout-blocked");
        await AssertRefusedCheckoutCallsAsync(app, calls, PartG.SwitchVendorRemoteCommand, PartG.Tg1, PartG.UntrackedKey, PartG.SecretIgnored);
        await AssertCheckoutBlockedAsync(app, PartG.IgnoredFileInTheWay("origin/vendor-remote"), PartG.SwitchVendorRemoteCommand, PartG.Tg1, PartG.UntrackedKey);
        AssertStatus(app, PartG.Failed(PartG.CheckOutVendorRemote), OperationStatusTone.Danger);
        Assert.Null(await RevParseAsync(repo, "refs/heads/vendor-remote"));
        Assert.Empty(await BranchKeysAsync(repo, @"^branch\.vendor-remote\."));
        Assert.Equal(exact, await repo.ExactStateAsync());
        AssertNothingToUndo(app);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 5: "Create branch here…" on Local branches › vendor, "Check out the new branch" ticked (GD4).
        (menu, _) = await RightClickRefAsync(app, "Local branches", "vendor");
        AssertItemEnabled(menu, "vendor", PartG.CreateBranchHereItem);
        app.ClickMenuItem(PartG.CreateBranchHereItem);
        await app.WaitForDialogAsync<NameDialogViewModel>();
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The name dialog has no view.");
        Assert.Equal(PartG.CreateBranchTitle, ((Avalonia.Controls.TextBlock)app.FindByAutomationId(view, "NameDialogTitle")).Text);
        Assert.Equal(PartG.FromVendor, ((Avalonia.Controls.TextBlock)app.FindByAutomationId(view, "NameDialogContext")).Text);
        var option = (Avalonia.Controls.CheckBox)app.FindByAutomationId(view, "NameDialogOption");
        Assert.Equal((PartG.CheckOutNewBranchOption, (bool?)true), (option.Content as string, option.IsChecked));
        var nameBox = (Avalonia.Controls.TextBox)app.FindByAutomationId(view, "NameBox");
        await app.WaitForAsync(() => nameBox.IsFocused, "the name box to have the keyboard focus");
        app.TypeInto("NameBox", PartG.VendorCopy);
        var create = (Avalonia.Controls.Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal(PartG.CreateBranchTitle, create.Content);
        await app.WaitForAsync(() => create.IsEffectivelyEnabled, "\"Create branch\" to be enabled once vendor-copy is typed");
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.Click(create);
        await WaitForFailureAsync(app, repo, PartG.CreateBranchVendorCopy, mark);
        Shot(app, "g22d-create-branch-blocked");
        await AssertRefusedCheckoutCallsAsync(app, calls, PartG.CreateVendorCopyCommand, PartG.Tg1, PartG.UntrackedKey, PartG.SecretIgnored);
        Assert.DoesNotContain(Since(app, calls), call => call.CommandText.StartsWith("git branch ", StringComparison.Ordinal));
        await AssertCheckoutBlockedAsync(app, PartG.IgnoredFileInTheWay("vendor"), PartG.CreateVendorCopyCommand, PartG.Tg1, PartG.UntrackedKey);
        AssertStatus(app, PartG.Failed(PartG.CreateBranchVendorCopy), OperationStatusTone.Danger);
        Assert.Null(await RevParseAsync(repo, "refs/heads/vendor-copy"));
        Assert.Equal(exact, await repo.ExactStateAsync());
        AssertNothingToUndo(app);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 6: Local branches › tools: the ignored folder build/ where tools has a file (TG2).
        (menu, _) = await RightClickRefAsync(app, "Local branches", "tools");
        AssertItemEnabled(menu, "tools", PartG.CheckOutTools);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.CheckOutTools);
        await WaitForFailureAsync(app, repo, PartG.CheckOutTools, mark);
        Shot(app, "g22e-folder-blocked");
        await AssertRefusedCheckoutCallsAsync(app, calls, PartG.SwitchToolsCommand, PartG.Tg2, PartG.FolderKey, PartG.BuildOutIgnored);
        await AssertCheckoutBlockedAsync(app, PartG.IgnoredFileInTheWay("tools"), PartG.SwitchToolsCommand, PartG.Tg2, PartG.FolderKey);
        AssertStatus(app, PartG.Failed(PartG.CheckOutTools), OperationStatusTone.Danger);
        Assert.Equal(PartG.BuildOutput, WorkingFile(repo, "build/out.bin"));
        Assert.Equal(exact, await repo.ExactStateAsync());
        AssertNothingToUndo(app);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 7: "Check out this commit" on Add the vendor secret, which main does not reach (D193's detached checkout).
        menu = RightClickCommit(app, PartG.VendorSecretRow, PartG.IgRowPoint(PartG.VendorSecretRow));
        await AssertRowSelectedAsync(app, PartG.VendorSecretRow, "Add the vendor secret");
        AssertItemEnabled(menu, "133a796 Add the vendor secret", PartG.CheckOutThisCommitItem);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.CheckOutThisCommitItem);
        await WaitForFailureAsync(app, repo, PartG.CheckOutVendorSecret, mark);
        Shot(app, "g22f-commit-blocked");
        await AssertRowSelectedAsync(app, PartG.VendorSecretRow, "Add the vendor secret");
        await AssertRefusedCheckoutCallsAsync(app, calls, PartG.DetachVendorSecretCommand, PartG.Tg1, PartG.UntrackedKey, PartG.SecretIgnored);
        await AssertCheckoutBlockedAsync(app, PartG.IgnoredFileInTheWay("133a796"), PartG.DetachVendorSecretCommand, PartG.Tg1, PartG.UntrackedKey);
        AssertStatus(app, PartG.Failed(PartG.CheckOutVendorSecret), OperationStatusTone.Danger);
        AssertBranch(app, "main");
        await AssertOnBranchAsync(repo, "main", PartG.MainWorkSha);
        Assert.Equal(PartG.SecretLocal, WorkingFile(repo, "secret.env"));
        AssertNothingToUndo(app);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        // Step 8: nothing changed in the whole check.
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Equal(PartG.Ig0Refs, await RefsAsync(repo));
        Assert.Equal(PartG.SecretLocal, WorkingFile(repo, "secret.env"));
        Assert.Equal(PartG.BuildOutput, WorkingFile(repo, "build/out.bin"));
        Assert.Equal(PartG.IgnoredBranchKeys, await BranchKeysAsync(repo));
        Assert.Equal(reflog, await ReflogAsync(repo));
        Assert.Equal(PartG.StashResetReflog, (await ReflogAsync(repo))[0]);
        await AssertNoBackupAsync(repo);
        AssertNothingToUndo(app);
    }

    /// <summary>
    /// Check G23, part A (steps 1 to 8): a merge, a rebase onto a branch, a cherry-pick, a revert, a
    /// stash's apply and its pop refused by the app's own check before git (D196, design 9.3 to 9.9),
    /// each with its own error title and 14.4's text naming <c>secret.env</c>, no git output, nothing
    /// written.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G23_merge_rebase_pick_revert_and_stash_refused()
    {
        using var repo = await Scenarios.IgnoredAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: IG0, nothing selected.
        var exact = await repo.ExactStateAsync();
        var reflog = await ReflogAsync(repo);
        var stashes = await repo.StashListAsync();
        AssertIg0(app);
        AssertNothingSelected(app);
        AssertNothingToUndo(app);

        // Step 2: "Merge vendor into main" (TR1); the check comes before Git.MergeAsync (9.9).
        var (menu, _) = await RightClickRefAsync(app, "Local branches", "vendor");
        AssertNothingSelected(app);
        AssertItemEnabled(menu, "vendor", PartG.MergeVendorItem);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.MergeVendorItem);
        await WaitForFailureAsync(app, repo, PartG.MergeVendor, mark);
        Shot(app, "g23a-merge-blocked");
        AssertNothingSelected(app);
        AssertReadsInOrder(OperationCalls(app, calls, PartG.Tr1[0].Command), -1, PartG.Tr1);
        await AssertNoWriteAsync(app, repo, calls, []);
        Assert.DoesNotContain(Since(app, calls), call => call.CommandText.StartsWith(PartG.FmtMergeMsgCommand, StringComparison.Ordinal));
        await AssertRefusedBeforeGitAsync(app, repo, PartG.MergeVendor, PartG.MergeFailedTitle, PartG.IgnoredFileInTheWayOf("merge"), exact);
        await CloseDialogAsync(app);

        // Step 3: "Rebase main onto vendor" (TR2: four reads side by side, then check-ignore).
        (menu, _) = await RightClickRefAsync(app, "Local branches", "vendor");
        AssertNothingSelected(app);
        AssertItemEnabled(menu, "vendor", PartG.RebaseOntoVendorItem);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.RebaseOntoVendorItem);
        await WaitForFailureAsync(app, repo, PartG.RebaseOntoVendor, mark);
        Shot(app, "g23b-rebase-blocked");
        AssertNothingSelected(app);
        var operation = OperationCalls(app, calls, PartG.Tr2SideBySide[1].Command);
        var last = AssertReadsAsSet(operation, -1, PartG.Tr2SideBySide);
        AssertReadsInOrder(operation, last, PartG.Tr2CheckIgnore);
        await AssertNoWriteAsync(app, repo, calls, []);
        await AssertRefusedBeforeGitAsync(app, repo, PartG.RebaseOntoVendor, PartG.RebaseFailedTitle, PartG.IgnoredFileInTheWayOf("rebase"), exact);
        await CloseDialogAsync(app);

        // Step 4: "Cherry-pick onto main" on Add the vendor secret (TR3).
        menu = RightClickCommit(app, PartG.VendorSecretRow, PartG.IgRowPoint(PartG.VendorSecretRow));
        await AssertRowSelectedAsync(app, PartG.VendorSecretRow, "Add the vendor secret");
        AssertItemEnabled(menu, "133a796 Add the vendor secret", PartG.CherryPickItem);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.CherryPickItem);
        await WaitForFailureAsync(app, repo, PartG.CherryPickVendorSecret, mark);
        Shot(app, "g23c-pick-blocked");
        await AssertRowSelectedAsync(app, PartG.VendorSecretRow, "Add the vendor secret");
        AssertReadsInOrder(OperationCalls(app, calls, PartG.Tr3[0].Command), -1, PartG.Tr3);
        await AssertNoWriteAsync(app, repo, calls, []);
        await AssertRefusedBeforeGitAsync(app, repo, PartG.CherryPickVendorSecret, PartG.CherryPickFailedTitle, PartG.IgnoredFileInTheWayOf("cherry-pick"), exact);
        await CloseDialogAsync(app);

        // Step 5: "Revert this commit" on Stop tracking the secret (TR4).
        menu = RightClickCommit(app, PartG.StopTrackingRow, PartG.IgRowPoint(PartG.StopTrackingRow));
        await AssertRowSelectedAsync(app, PartG.StopTrackingRow, "Stop tracking the secret");
        AssertItemEnabled(menu, "39bd803 Stop tracking the secret", PartG.RevertItem);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.RevertItem);
        await WaitForFailureAsync(app, repo, PartG.RevertStopTracking, mark);
        Shot(app, "g23d-revert-blocked");
        await AssertRowSelectedAsync(app, PartG.StopTrackingRow, "Stop tracking the secret");
        AssertReadsInOrder(OperationCalls(app, calls, PartG.Tr4[0].Command), -1, PartG.Tr4);
        await AssertNoWriteAsync(app, repo, calls, []);
        await AssertRefusedBeforeGitAsync(app, repo, PartG.RevertStopTracking, PartG.RevertFailedTitle, PartG.IgnoredFileInTheWayOf("revert"), exact);
        await CloseDialogAsync(app);

        // Step 6: the stash's menu, then "Apply stash" (TR5: D179's count, then check-ignore).
        var (stashMenu, pointer) = await RightClickRefAsync(app, "Stashes", PartG.StashListed);
        await AssertRowSelectedAsync(app, PartG.StopTrackingRow, "Stop tracking the secret");
        AssertWholeMenu(app, stashMenu, PartG.StashMenu, PartG.ApplyStashItem, PartG.StashMenuHeight, pointer);
        AssertMenuKey(app, PartG.PopStashItem, PartG.PopKey);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.ApplyStashItem);
        await WaitForFailureAsync(app, repo, PartG.ApplyStash, mark);
        Shot(app, "g23e-apply-blocked");
        await AssertRowSelectedAsync(app, PartG.StopTrackingRow, "Stop tracking the secret");
        AssertStashCheck(app, calls, stashReadsInStep: null);
        AssertNoStashCall(app, calls);
        await AssertNoWriteAsync(app, repo, calls, []);
        await AssertRefusedBeforeGitAsync(app, repo, PartG.ApplyStash, PartG.ApplyingStashFailedTitle, PartG.IgnoredFileInTheWayOfStash, exact);
        Assert.Equal(stashes, await repo.StashListAsync());
        Assert.Equal([$"stash@{{0}}: {PartG.StashListed}"], await GitLinesAsync(repo, "stash", "list"));
        await CloseDialogAsync(app);

        // Step 7: the toolbar's Pop: D179's status read and check-ignore only (the stash's reads kept by its SHA, GD23).
        AssertEnabledToolTip(app, "PopButton", PartG.PopToolTip);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "PopButton"));
        await WaitForFailureAsync(app, repo, PartG.PopStash, mark);
        Shot(app, "g23f-pop-blocked");
        await AssertRowSelectedAsync(app, PartG.StopTrackingRow, "Stop tracking the secret");
        AssertStashCheck(app, calls, stashReadsInStep: false);
        AssertNoStashCall(app, calls);
        await AssertNoWriteAsync(app, repo, calls, []);
        await AssertRefusedBeforeGitAsync(app, repo, PartG.PopStash, PartG.ApplyingStashFailedTitle, PartG.IgnoredFileInTheWayOfStash, exact);
        await CloseDialogAsync(app);

        // Step 8: nothing changed in the whole part.
        Assert.Equal(exact, await repo.ExactStateAsync());
        await AssertNoBackupAsync(repo);
        Assert.Equal(stashes, await repo.StashListAsync());
        AssertNothingInProgress(repo);
        Assert.Equal(reflog, await ReflogAsync(repo));
        AssertNothingToUndo(app);
    }

    /// <summary>
    /// Check G23, part B, steps 9 to 11 (D264, D283: part B is three tests of at most two pulls):
    /// the pull's check inside its integration (design 9.10) on IG1, a branch only ahead of its
    /// upstream with a local merge whose side branch starts at a commit that tracks the file:
    /// refused under <c>pull.rebase</c> <c>merges</c> and <c>true</c> through the branch points
    /// (9.6's (5) and (6)).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G23_pull_rebase_refused_inside_the_integration()
    {
        using var repo = await Scenarios.IgnoredAsync();

        // Step 9: IG1 built before the app starts (GD7), pull.rebase = merges in .git/config.
        await BuildIg1Async(repo);
        await repo.ConfigAsync("pull.rebase", "merges");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var exact = await repo.ExactStateAsync();
        AssertIg1Opened(app, repo, PartG.Ig1Panel, PartG.PullFromOriginMain);

        // Step 10: Pull under pull.rebase = merges: the rebase keeping merges refused (TR6).
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        using (var running = app.RecordStatusTexts())
        {
            app.Click(ToolbarButton(app, "PullButton"));
            await WaitForFailureAsync(app, repo, PartG.PullTitle, mark, NetworkTimeout);
            Assert.Contains(PartG.PullingOriginMain, running.Texts);
        }

        Shot(app, "g23g-pull-rebase-merges-blocked");
        var fetch = AssertPullConfigReads(app, calls, (PartG.PullFfRead, 1, string.Empty), (PartG.BranchMainRebaseRead, 1, string.Empty), (PartG.PullRebaseRead, 0, "merges"), (PartG.PullRebaseBoolRead, 128, string.Empty));
        await AssertQuietFetchAsync(fetch);
        await AssertPullRebaseCheckAsync(app, calls);
        await AssertNoWriteAsync(app, repo, calls, []);
        await AssertPullRefusedAsync(app, repo, PartG.IgnoredFileInTheWayOf("rebase"), exact);
        await CloseDialogAsync(app);

        // Step 11: pull.rebase = true outside the app: the plain rebase refused all the same (9.6's last sentence).
        await ConfigOutsideAsync(app, repo, "pull.rebase", "true");
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "PullButton"));
        await WaitForFailureAsync(app, repo, PartG.PullTitle, mark, NetworkTimeout);
        fetch = AssertPullConfigReads(app, calls, (PartG.PullFfRead, 1, string.Empty), (PartG.BranchMainRebaseRead, 1, string.Empty), (PartG.PullRebaseRead, 0, "true"));
        Assert.DoesNotContain(Since(app, calls), call => call.CommandText == PartG.PullRebaseBoolRead);
        await AssertQuietFetchAsync(fetch);
        await AssertPullRebaseCheckAsync(app, calls);
        await AssertNoWriteAsync(app, repo, calls, []);
        await AssertPullRefusedAsync(app, repo, PartG.IgnoredFileInTheWayOf("rebase"), exact);
        await CloseDialogAsync(app);

        await AssertG23PartBUnchangedAsync(app, repo, exact);
    }

    /// <summary>
    /// Check G23, part B, steps 9 and 12 (D264, D283): on IG1 with <c>pull.rebase</c> unset, a
    /// merge whose source adds nothing HEAD lacks is let through.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G23_pull_merge_that_adds_nothing_goes_on()
    {
        using var repo = await Scenarios.IgnoredAsync();

        // Step 9: IG1 built before the app starts (GD7).
        await BuildIg1Async(repo);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var exact = await repo.ExactStateAsync();
        AssertIg1Opened(app, repo, PartG.Ig1Panel, PartG.PullFromOriginMain);

        // Step 12: pull.rebase unset (never set here): the merge of origin/main, which adds nothing, goes on (TR7, TG11).
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "PullButton"));
        await WaitForSuccessAsync(app, repo, PartG.PullTitle, mark, NetworkTimeout);
        var fetch = AssertPullConfigReads(app, calls, (PartG.PullFfRead, 1, string.Empty), (PartG.BranchMainRebaseRead, 1, string.Empty), (PartG.PullRebaseRead, 1, string.Empty));
        await AssertQuietFetchAsync(fetch);
        var pull = OperationCalls(app, calls, PartG.FetchOriginCommand);
        var at = AssertReadsInOrder(pull, pull.FindIndex(call => call.CommandText == PartG.FetchOriginCommand), new ExpectedRead(PartG.HeadRead, 0, PartG.SideMergeSha), PartG.Tr7Added);
        Assert.DoesNotContain(pull, call => call.CommandText == PartG.CheckIgnoreCommand);
        Assert.DoesNotContain(pull, call => call.CommandText.StartsWith("git merge-base --all", StringComparison.Ordinal));
        var merge = pull[IndexOf(pull, PartG.PullMergeCommand, at)];
        Assert.True(merge.ExitCode == 0, $"\"{merge.CommandText}\" exited {merge.ExitCode}.\n{merge.StandardError}");
        await AssertGitPrintedAsync(merge.StandardOutput, [PartG.AlreadyUpToDate], PartG.AlreadyUpToDate, "The pull's merge's standard output");
        AssertStatus(app, PartG.MainUpToDate);
        Assert.Equal(PartG.SideMergeSha, await repo.HeadAsync());
        Assert.Equal(exact, await repo.ExactStateAsync());
        AssertNothingToUndo(app);

        await AssertG23PartBUnchangedAsync(app, repo, exact);
    }

    /// <summary>
    /// Check G23, part B, steps 9, 13 and 14 (D264, D283): on IG1v, main tracking origin/vendor
    /// (written before the start), a diverged upstream's merge refused; under <c>pull.ff=only</c>
    /// git's own refusal.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G23_pull_merge_refused_then_ff_only_refused_by_git()
    {
        using var repo = await Scenarios.IgnoredAsync();

        // Step 9 on IG1v: IG1 built before the app starts (GD7), branch.main.merge = refs/heads/vendor in .git/config.
        await BuildIg1Async(repo);
        await repo.ConfigAsync("branch.main.merge", "refs/heads/vendor");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var exact = await repo.ExactStateAsync();
        AssertIg1Opened(app, repo, PartG.Ig1vPanel, PartG.PullFromOriginVendor);

        // Step 13: main tracks origin/vendor (IG1v, written before the start): the merge of vendor's tip refused (TR8).
        AssertLeftPanel(app, PartG.Ig1vPanel);
        AssertGraph(app, PartG.Ig1, PartG.Ig1Lanes, showsDate: false);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "PullButton"));
        await WaitForFailureAsync(app, repo, PartG.PullTitle, mark, NetworkTimeout);
        Shot(app, "g23h-pull-merge-blocked");
        var fetch = AssertPullConfigReads(app, calls, (PartG.PullFfRead, 1, string.Empty), (PartG.BranchMainRebaseRead, 1, string.Empty), (PartG.PullRebaseRead, 1, string.Empty));
        await AssertQuietFetchAsync(fetch);
        AssertFetchHeadMarksVendor(repo);
        var pull = OperationCalls(app, calls, PartG.FetchOriginCommand);
        AssertReadsInOrder(pull, pull.FindIndex(call => call.CommandText == PartG.FetchOriginCommand), [new ExpectedRead(PartG.HeadRead, 0, PartG.SideMergeSha), .. PartG.Tr8]);
        await AssertNoWriteAsync(app, repo, calls, []);
        await AssertPullRefusedAsync(app, repo, PartG.IgnoredFileInTheWayOf("merge"), exact);
        await CloseDialogAsync(app);

        // Step 14: pull.ff = only: nothing counts as in the way on diverged branches; git's own refusal (TR9, TG3).
        await ConfigOutsideAsync(app, repo, "pull.ff", "only");
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "PullButton"));
        await WaitForFailureAsync(app, repo, PartG.PullTitle, mark, NetworkTimeout);
        Shot(app, "g23i-pull-not-fast-forward");
        fetch = AssertPullConfigReads(app, calls, (PartG.PullFfRead, 0, "only"), (PartG.PullFfBoolRead, 128, string.Empty), (PartG.BranchMainRebaseRead, 1, string.Empty), (PartG.PullRebaseRead, 1, string.Empty));
        await AssertQuietFetchAsync(fetch);
        pull = OperationCalls(app, calls, PartG.FetchOriginCommand);
        var at = AssertReadsInOrder(pull, pull.FindIndex(call => call.CommandText == PartG.FetchOriginCommand), [new ExpectedRead(PartG.HeadRead, 0, PartG.SideMergeSha), .. PartG.Tr8, PartG.Tr9Diverged]);
        var fastForward = pull[IndexOf(pull, PartG.PullFastForwardCommand, at)];
        Assert.True(fastForward.ExitCode == 128, $"\"{fastForward.CommandText}\" exited {fastForward.ExitCode}; the report says 128.\n{fastForward.StandardError}");
        await AssertGitPrintedAsync(fastForward.StandardOutput, [], null, "The fast-forward's standard output");
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            PartG.PullFailedTitle, PartG.MainAndOriginVendorDiverged, PartG.Ignored, PartG.PullFastForwardCommand, PartG.ButtonsWithOutput, PartG.Tg3, PartG.NotFastForwardKey));
        AssertStatus(app, PartG.Failed(PartG.PullTitle), OperationStatusTone.Danger);
        Assert.Equal(exact, await repo.ExactStateAsync());
        AssertNothingToUndo(app);
        AssertNoClippedText(app);
        await CloseDialogAsync(app);

        await AssertG23PartBUnchangedAsync(app, repo, exact);
    }

    /// <summary>G23 part B's step 9 before the app starts: IG1 built on "ignored" (GD7), the side branch merged into main.</summary>
    private static async Task BuildIg1Async(TempRepo repo)
    {
        var side = (await repo.GitAsync(PartG.SideStartCommitTree)).StandardOutput.Trim();
        Assert.Equal(PartG.SideStartSha, side);
        await repo.CreateBranchAsync(PartG.SideBranch, side);
        Assert.Equal(PartG.SideMergeSha, await repo.MergeAsync(PartG.SideBranch));
    }

    /// <summary>G23 part B's step 9 once the app has opened IG1: its graph and dashed lines, the left panel <paramref name="panel"/>, main checked out, Pull's tooltip <paramref name="pullTip"/>, the ignored file and nothing to undo.</summary>
    private static void AssertIg1Opened(ShellDriver app, TempRepo repo, IReadOnlyList<string> panel, string pullTip)
    {
        AssertGraph(app, PartG.Ig1, PartG.Ig1Lanes, showsDate: false);
        AssertDashedLines(app, PartG.Ig1Dashes);
        AssertLeftPanel(app, panel);
        AssertBranch(app, "main");
        AssertEnabledToolTip(app, "PullButton", pullTip);
        Assert.Equal(PartG.SecretLocal, WorkingFile(repo, "secret.env"));
        AssertNothingToUndo(app);
    }

    /// <summary>G23 part B's step 15: nothing changed since step 9 (<paramref name="exact"/>): no backup, nothing in progress, the ignored file kept, nothing to undo.</summary>
    private static async Task AssertG23PartBUnchangedAsync(ShellDriver app, TempRepo repo, string exact)
    {
        // Step 15: nothing changed in the whole part.
        Assert.Equal(exact, await repo.ExactStateAsync());
        await AssertNoBackupAsync(repo);
        AssertNothingInProgress(repo);
        Assert.Equal(PartG.SecretLocal, WorkingFile(repo, "secret.env"));
        AssertNothingToUndo(app);
    }

    /// <summary>
    /// Check G24: Keep ignored files off restores phase 5 exactly (D221, 9.1): a merge runs as git
    /// runs it, with none of 9.4's reads and no <c>--no-overwrite-ignore</c>, and overwrites the
    /// ignored <c>secret.env</c>; a checkout takes git's default and deletes the ignored folder
    /// <c>build/</c> where the target has a file.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G24_keep_ignored_files_off_restores_phase_5()
    {
        using var repo = await Scenarios.IgnoredAsync();
        using var data = new TempDirectory("data");

        // Step 1: "keepIgnoredFiles": false in settings.json before the start.
        new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Update(settings => settings with { KeepIgnoredFiles = false });
        using var app = await OpenAsync(data, repo);
        Assert.False(app.Session.Services.Settings.Current.KeepIgnoredFiles, "The app read Keep ignored files as on.");
        AssertIg0(app);
        AssertNothingToUndo(app);

        // Step 2: "Merge vendor into main" runs as phase 5's merge (TG9): secret.env overwritten.
        var (menu, _) = await RightClickRefAsync(app, "Local branches", "vendor");
        AssertItemEnabled(menu, "vendor", PartG.MergeVendorItem);
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.MergeVendorItem);
        await WaitForSuccessAsync(app, repo, PartG.MergeVendor, mark);
        var shot = Shot(app, "g24a-merged-without-the-check");
        var since = Since(app, calls);
        var message = since[IndexOf(since, PartG.FmtMergeMsgCommand)];
        Assert.Equal(PartG.VendorMergeMessage, PartG.Normalised(message.StandardOutput));
        var merged = since[IndexOf(since, PartG.MergeVendorWithoutOptionCommand, since.IndexOf(message))];
        Assert.True(merged.ExitCode == 0, $"\"{merged.CommandText}\" exited {merged.ExitCode}.\n{merged.StandardError}");
        await AssertGitPrintedAsync(merged.StandardOutput, PartG.Tg9, PartG.MergeMadeKey, "The merge's standard output");
        Assert.DoesNotContain(since, call => call.CommandText.Contains("--no-overwrite-ignore", StringComparison.Ordinal));
        Assert.DoesNotContain(since, call => call.CommandText == PartG.Tr1[0].Command);
        Assert.DoesNotContain(since, call => call.CommandText.StartsWith("git merge-base --all", StringComparison.Ordinal));
        AssertNoCheckIgnore(app, calls, PartG.MergeVendorWithoutOptionCommand);
        AssertStatus(app, PartG.MergedVendorIntoMain);
        AssertGraph(app, PartG.Ig2, PartG.IgLanes, showsDate: true, picture: shot);
        AssertDashedLines(app, PartG.Ig2Dashes);
        AssertLeftPanel(app, PartG.Ig2Panel);
        Assert.Equal("refs/heads/main", await SymbolicHeadAsync(repo));
        Assert.Equal(PartG.VendorMergeSha, await repo.HeadAsync());
        var fields = (await GitLinesAsync(repo, "log", "-1", "--format=%P%x1f%an <%ae>%x1f%cn <%ce>%x1f%aI%x1f%cI", "HEAD")).Single().Split('\x1f');
        Assert.Equal($"{PartG.MainWorkSha} {PartG.VendorSecretSha}", fields[0]);
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[1]);
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}>", fields[2]);
        AssertGitDate(AppCommitTime, fields[3]);
        AssertGitDate(AppCommitTime, fields[4]);
        Assert.Equal(PartG.SecretVendor, WorkingFile(repo, "secret.env"));
        Assert.Equal(PartG.MergedStatus, await IgnoredStatusAsync(repo));
        Assert.Equal(PartG.VendorMergeReflog, (await ReflogAsync(repo))[0]);
        AssertUndoEnabled(app);

        // Step 3: "Check out tools" takes git's default (TG10): build/out.bin and secret.env gone.
        (menu, _) = await RightClickRefAsync(app, "Local branches", "tools");
        AssertItemEnabled(menu, "tools", PartG.CheckOutTools);
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.CheckOutTools);
        await WaitForSuccessAsync(app, repo, PartG.CheckOutTools, mark);
        shot = Shot(app, "g24b-tools-checked-out");
        since = Since(app, calls);
        var checkout = since[IndexOf(since, PartG.SwitchToolsWithoutOptionCommand)];
        Assert.True(checkout.ExitCode == 0, $"\"{checkout.CommandText}\" exited {checkout.ExitCode}.\n{checkout.StandardError}");
        await AssertGitPrintedAsync(checkout.StandardOutput, [], null, "The checkout's standard output");
        await AssertGitPrintedAsync(checkout.StandardError, [PartG.SwitchedToTools], PartG.SwitchedToTools, "The checkout's standard error");
        Assert.DoesNotContain(since, call => call.CommandText.Contains("--no-overwrite-ignore", StringComparison.Ordinal));
        AssertNoCheckIgnore(app, calls, PartG.SwitchToolsWithoutOptionCommand);
        AssertStatus(app, PartG.CheckedOutTools);
        AssertBranch(app, "tools");
        AssertGraph(app, PartG.Ig3, PartG.IgLanes, showsDate: true, picture: shot);
        AssertDashedLines(app, PartG.Ig2Dashes);
        AssertLeftPanel(app, PartG.Ig3Panel);
        await AssertToolsCheckedOutAsync(repo);

        // Step 4: HEAD, the refs, the status, the files and HEAD's reflog read back.
        await AssertToolsCheckedOutAsync(repo);
        Assert.Equal(PartG.VendorMergeSha, await RevParseAsync(repo, "refs/heads/main"));
        await AssertNoBackupAsync(repo);
    }

    // ---- What G22 to G24 share ----

    /// <summary>IG0 as the report's scenarios section gives it: the graph, its dashed line, its drawn labels and the left panel.</summary>
    private static void AssertIg0(ShellDriver app)
    {
        AssertGraph(app, PartG.Ig0, PartG.IgLanes, showsDate: true);
        AssertDashedLines(app, PartG.Ig0Dashes);
        AssertDrawnLabels(app, PartG.Ig0Drawn);
        AssertLeftPanel(app, PartG.Ig0Panel);
    }

    /// <summary>The error dialog of a checkout git refused for the ignored file (9.11): "Checkout blocked", D179's text, git's output and command, "Copy output" and "Close" (no "Stash changes and check out", D91).</summary>
    private static Task AssertCheckoutBlockedAsync(ShellDriver app, string summary, string command, string[] lines, string key) =>
        AssertErrorDialogAsync(app, new ExpectedErrorDialog(PartG.CheckoutBlockedTitle, summary, PartG.Ignored, command, PartG.ButtonsWithOutput, lines, key));

    /// <summary>
    /// The app's own refusal before git (9.9, 9.15): the error dialog with the operation's own title
    /// and 14.4's text, "In ignored", no output, no command, "Close" only; the status bar
    /// "&lt;Title&gt; failed" in the danger colour; nothing in progress; the exact state unchanged;
    /// nothing recorded for Undo.
    /// </summary>
    private static async Task AssertRefusedBeforeGitAsync(ShellDriver app, TempRepo repo, string title, string errorTitle, string summary, string exact)
    {
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(errorTitle, summary, PartG.Ignored, null, PartG.CloseOnly, []));
        AssertStatus(app, PartG.Failed(title), OperationStatusTone.Danger);
        AssertNothingInProgress(repo);
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Equal(PartG.SecretLocal, WorkingFile(repo, "secret.env"));
        AssertNothingToUndo(app);
        AssertNoClippedText(app);
    }

    /// <summary>
    /// TR5 (9.8, GD23): in the apply's or pop's operation since <paramref name="calls"/>, D179's
    /// status read, then <c>git check-ignore --stdin -z</c> answering <c>secret.env</c>; the stash's
    /// three reads made in the session before that check-ignore (where is not asserted), or, with
    /// <paramref name="stashReadsInStep"/> false, none of them since the step began (kept by the
    /// stash's SHA).
    /// </summary>
    private static void AssertStashCheck(ShellDriver app, int calls, bool? stashReadsInStep)
    {
        var all = app.GitCalls().ToList();
        var since = all.Skip(calls).ToList();
        var check = since.FirstOrDefault(call => call.CommandText == PartG.CheckIgnoreCommand && call.OperationId is not null)
            ?? throw new Xunit.Sdk.XunitException("The stash's operation asked no git check-ignore. It ran:\n" + string.Join('\n', since.Select(Described)));
        var operation = since.Where(call => call.OperationId == check.OperationId).ToList();
        var at = operation.IndexOf(check);
        Assert.True(
            operation.Take(at).Any(call => call.CommandText == PartG.StatusReadCommand),
            "D179's count read no status before the check-ignore. The operation ran:\n" + string.Join('\n', operation.Select(Described)));
        AssertRead(check, new ExpectedRead(PartG.CheckIgnoreCommand, 0, PartG.SecretIgnored));
        var before = all.IndexOf(check);
        foreach (var read in PartG.Tr5StashReads)
        {
            var made = all.Take(before).LastOrDefault(call => call.CommandText == read.Command);
            Assert.True(made is not null, $"The stash's read \"{read.Command}\" was not made before the check-ignore.");
            AssertRead(made!, read);
            if (stashReadsInStep == false)
            {
                Assert.DoesNotContain(since, call => call.CommandText == read.Command);
            }
        }
    }

    /// <summary>
    /// The pull's configuration reads (8.9, GD20) since <paramref name="calls"/>, in this order, each
    /// with its exit code and output, then <c>git fetch --progress origin</c> after them; returns the
    /// fetch's record.
    /// </summary>
    private static GitCallRecord AssertPullConfigReads(ShellDriver app, int calls, params (string Command, int Exit, string Output)[] reads)
    {
        var since = Since(app, calls);
        var at = AssertReadsInOrder(since, -1, [.. reads.Select(read => new ExpectedRead(read.Command, read.Exit, read.Output))]);
        return since[IndexOf(since, PartG.FetchOriginCommand, at)];
    }

    /// <summary>The fetch of each pull (TG12): exit 0, nothing on either stream (on 2.36; its exit on every git), and no ref changed by it.</summary>
    private static async Task AssertQuietFetchAsync(GitCallRecord fetch)
    {
        Assert.True(fetch.ExitCode == 0, $"\"{fetch.CommandText}\" exited {fetch.ExitCode}.\n{fetch.StandardError}");
        await AssertGitPrintedAsync(fetch.StandardOutput, [], null, "The fetch's standard output");
        await AssertGitPrintedAsync(fetch.StandardError, [], null, "The fetch's standard error");
    }

    /// <summary>
    /// TR6 (9.10, 9.6) in the pull's operation since <paramref name="calls"/>: after the fetch, the
    /// HEAD read and <c>git merge-base --is-ancestor 6e68a25… FETCH_HEAD</c> (exit 1: the rebase is
    /// chosen); then 9.6's four reads as a set; then (5) as the app reads it, its exact order on 2.36
    /// only; then (6) and <c>check-ignore</c>, in this order.
    /// </summary>
    private static async Task AssertPullRebaseCheckAsync(ShellDriver app, int calls)
    {
        var pull = OperationCalls(app, calls, PartG.FetchOriginCommand);
        var fetch = pull.FindIndex(call => call.CommandText == PartG.FetchOriginCommand);
        var at = AssertReadsInOrder(pull, fetch, new ExpectedRead(PartG.HeadRead, 0, PartG.SideMergeSha), new ExpectedRead(PartG.IsSideMergeAncestorOfFetchHead, 1, string.Empty));
        at = AssertReadsAsSet(pull, at, PartG.Tr6SideBySide);
        var points = pull[IndexOf(pull, PartG.BranchPointsRead, at)];
        Assert.True(points.ExitCode == 0, $"\"{points.CommandText}\" exited {points.ExitCode}.\n{points.StandardError}");
        var lines = PartG.Normalised(points.StandardOutput).Split('\n');
        Assert.Equal(PartG.BranchPointsTip, lines[0]);
        Assert.Equal(PartG.BranchPointsInRange.Order(StringComparer.Ordinal), lines.Where(line => !line.StartsWith('-')).Order(StringComparer.Ordinal));
        Assert.Equal(PartG.BranchPointsBoundary.Order(StringComparer.Ordinal), lines.Where(line => line.StartsWith('-')).Order(StringComparer.Ordinal));
        if (await IsReportGitAsync())
        {
            Assert.Equal(PartG.BranchPointsOn236, lines);
        }

        AssertReadsInOrder(pull, pull.IndexOf(points), PartG.BranchPointsAddRead, new ExpectedRead(PartG.CheckIgnoreCommand, 0, PartG.SecretIgnored));
    }

    /// <summary>The pull refused by its own check (GD8): "Pull failed", 14.4's text, no output, no command, "Close" only; the status bar "Pull failed" in the danger colour; nothing changed.</summary>
    private static async Task AssertPullRefusedAsync(ShellDriver app, TempRepo repo, string summary, string exact)
    {
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(PartG.PullFailedTitle, summary, PartG.Ignored, null, PartG.CloseOnly, []));
        AssertStatus(app, PartG.Failed(PartG.PullTitle), OperationStatusTone.Danger);
        AssertNothingInProgress(repo);
        await AssertNoBackupAsync(repo);
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Equal(PartG.SideMergeSha, await repo.HeadAsync());
        AssertNothingToUndo(app);
        AssertNoClippedText(app);
    }

    /// <summary>The fetch on IG1v marks vendor's tip for merge in <c>FETCH_HEAD</c>, the other lines <c>not-for-merge</c>.</summary>
    private static void AssertFetchHeadMarksVendor(TempRepo repo)
    {
        var lines = File.ReadAllLines(Path.Combine(repo.Path, ".git", "FETCH_HEAD")).Where(line => line.Length > 0).ToList();
        var forMerge = lines.Where(line => !line.Contains("\tnot-for-merge\t", StringComparison.Ordinal)).ToList();
        var line = Assert.Single(forMerge);
        Assert.StartsWith(PartG.VendorSecretSha + "\t\tbranch 'vendor' of ", line, StringComparison.Ordinal);
        Assert.Equal(3, lines.Count);
    }

    /// <summary>No call since <paramref name="calls"/> has all of <paramref name="parts"/> among its arguments.</summary>
    private static void AssertNoCallContaining(ShellDriver app, int calls, params string[] parts)
    {
        var found = Since(app, calls).Where(call => call.CommandText.StartsWith("git switch", StringComparison.Ordinal) && parts.Any(part => call.CommandText.Split(' ').Contains(part))).ToList();
        Assert.True(found.Count == 0, "The checkout ran with " + string.Join(" or ", parts) + ":\n" + string.Join('\n', found.Select(Described)));
    }

    /// <summary>IG3's git state (G24 steps 3 and 4): HEAD on tools, its files, build/out.bin and secret.env gone, nothing in the status, the checkout in HEAD's reflog.</summary>
    private static async Task AssertToolsCheckedOutAsync(TempRepo repo)
    {
        await AssertOnBranchAsync(repo, "tools", PartG.BuildFileSha);
        Assert.Equal(PartG.AppInitial, WorkingFile(repo, "app.txt"));
        Assert.Equal(PartG.BuildFile, WorkingFile(repo, "build"));
        Assert.True(File.Exists(Path.Combine(repo.Path, "build")), "build is no file.");
        Assert.False(InWorkingTree(repo, "build/out.bin"), "build/out.bin is still there.");
        Assert.False(InWorkingTree(repo, "secret.env"), "secret.env is still there.");
        var files = Directory.EnumerateFiles(repo.Path, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(repo.Path, file).Replace('\\', '/'))
            .Where(file => !file.StartsWith(".git/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        Assert.Equal([".gitignore", "README.md", "app.txt", "build"], files);
        Assert.Empty(await IgnoredStatusAsync(repo));
        Assert.Equal(PartG.ToolsCheckoutReflog, (await ReflogAsync(repo))[0]);
    }
}
