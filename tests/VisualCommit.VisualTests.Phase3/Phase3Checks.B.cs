// Part B of phase 3's scripted walk-through (docs/test-reports/phase-3.md, "Part B: merge, the
// conflict banner, reset, cherry-pick, revert and the detached HEAD"): checks 8, 9, 9b, 10, 11,
// 11b, 12 and 32. This class is part B's alone; its expected texts and graphs (G0 to G12, E0, E1)
// go into Phase3Expectations.B.cs (Phase3Expectations.PartB), and further files of its own may
// hold more of the class (Phase3Checks.B.<topic>.cs: Phase3Checks.B.Support.cs holds what the
// checks share). Everything the parts share is in Phase3Checks.Support.cs; read its header for the
// conventions (one test method per app start, pictures through Shot, no fixed waits). While a
// banner is shown, rows are found from the graph control's bounds (ShellDriver.GraphRowPoint,
// WorkingRowPoint, GraphLabelBounds), never at a fixed y (D122).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations.PartB;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>The checks of part B: merge, the conflict banner, reset, cherry-pick, revert and the detached HEAD.</summary>
public sealed partial class Phase3ChecksB : Phase3Checks
{
    // ---------------------------------------------------------------------------------------
    // Check 8: merge as a fast-forward, with a merge commit, and a remote branch (1400×900).

    /// <summary>Check 8 part A (steps 1 to 3): G0 with six columns, feature/ff's menu, the fast-forward.</summary>
    [AvaloniaFact]
    public async Task Check_08_merges_fast_forward()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);

        // Step 1: G0 with all six columns; nothing selected; "Ready".
        var before = Shot(app, "08a-before");
        AssertGraph(app, G0, before);
        AssertColumns(app, author: true, date: true, sha: true);
        Assert.All(Enumerable.Range(0, Graph(app).Data!.Count), row => Assert.Equal(TempRepo.AuthorName, Graph(app).Data!.CommitAt(row).AuthorName));
        Assert.Equal(PartB.G0Subjects, Enumerable.Range(0, Graph(app).Data!.Count).Select(row => Graph(app).Data!.CommitAt(row).Subject));
        AssertSelected(app, before);
        AssertStatus(app, "Ready");
        AssertBranch(app, "main");
        await AssertLocalBranchAsync(app, "main", 0, 0, head: true);
        AssertNoClippedText(app);

        // Step 2: the label feature/ff's menu; the right-click selects its row.
        OpenMenu(app, LabelPoint(app, 2, "feature/ff"), FeatureFfMenu, "Check out feature/ff");
        await WaitForDetailsAsync(app, "Fast-forward me");
        var menu = Shot(app, "08b-branch-menu");
        Assert.All(app.Menu().Items, item => Assert.True(item.IsEnabled, $"\"{item.Label}\" is disabled."));
        AssertSelected(app, menu, 2);

        // Step 3: "Merge feature/ff into main" fast-forwards.
        var mark = app.LogMark();
        await RunOperationAsync(app, "Merge feature/ff", () => app.ClickMenuItem("Merge feature/ff into main"));
        await WaitForGraphAsync(app, 8, Sha.Stash);
        var merged = Shot(app, "08c-fast-forwarded");
        await AssertNoMenuAsync(app, mark);
        AssertGraph(app, G1, merged);
        AssertSelected(app, merged, 2);
        AssertNoWorkingRow(app);
        await AssertLocalBranchAsync(app, "main", 1, 0, head: true);
        AssertStatus(app, StatusFastForwarded);
        AssertBranch(app, "main");
        AssertNoClippedText(app);

        Assert.Equal(await RevAsync(repo, "refs/heads/feature/ff"), await RevAsync(repo, "refs/heads/main"));
        Assert.StartsWith(Sha.FastForwardMe, await RevAsync(repo, "refs/heads/main"), StringComparison.Ordinal);
        Assert.Equal(["## main...origin/main [ahead 1]"], await StatusAsync(repo));
        var reflog = await GitOutAsync(repo, "reflog", "-1", "--format=%gs", "refs/heads/main");
        if (await IsReportGitAsync())
        {
            Assert.Equal(FastForwardReflog, reflog);
        }
        else
        {
            Assert.StartsWith(FastForwardReflogStart, reflog, StringComparison.Ordinal);
        }

        await AssertNoBackupsAsync(repo);
    }

    /// <summary>Check 8 part B (steps 4 and 5): "Merge feature/ff into main (create a merge commit)" and its details.</summary>
    [AvaloniaFact]
    public async Task Check_08_merges_with_a_merge_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);

        // Step 4.
        OpenMenu(app, LabelPoint(app, 2, "feature/ff"), FeatureFfMenu, "Check out feature/ff");
        await RunOperationAsync(app, "Merge feature/ff", () => app.ClickMenuItem("Merge feature/ff into main (create a merge commit)"));
        await WaitForGraphAsync(app, 9, "22a08ae");
        var merged = Shot(app, "08d-merge-commit");
        AssertGraph(app, G2, merged);
        AssertColumns(app, author: true, date: true, sha: true);
        AssertSelected(app, merged, 3);
        AssertStatus(app, StatusMergedFf);
        await AssertLocalBranchAsync(app, "main", 2, 0, head: true);
        AssertNoWorkingRow(app);
        AssertNoClippedText(app);

        Assert.Equal(Sha.MergeFf, await RevAsync(repo, "HEAD"));
        Assert.Equal("Merge branch 'feature/ff'", await MessageAsync(repo, "HEAD"));
        Assert.Equal([Sha.MainWorkFull, await RevAsync(repo, "refs/heads/feature/ff")], await ParentsAsync(repo, "HEAD"));
        Assert.Equal("Test Author <author@example.com> / Test Author <author@example.com>", await PeopleAsync(repo, "HEAD"));
        Assert.Equal((AppCommitTime, AppCommitTime), await DatesAsync(repo, "HEAD"));
        Assert.Equal("A\tdocs/ff.txt", await GitOutAsync(repo, "diff", "--name-status", "HEAD^1", "HEAD"));
        Assert.Equal(["## main...origin/main [ahead 2]"], await StatusAsync(repo));

        // Step 5: the merge's details.
        app.Click(app.GraphRowPoint(0));
        await AssertDetailsAsync(app, "Merge branch 'feature/ff'", AppDate, null, [Sha.MainWork, Sha.FastForwardMe], "A ff.txt docs");
        var details = Shot(app, "08e-merge-details");
        AssertSelected(app, details, 0);
        AssertNoClippedText(app);
    }

    /// <summary>Check 8 part C (steps 6 to 9): the remote branches' menus, merging origin/feature/alpha, and "already up to date".</summary>
    [AvaloniaFact]
    public async Task Check_08_merges_a_remote_branch()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);

        // Step 6, the first menu: origin/main, whose main is checked out (D114).
        var remoteMain = await app.RefRowAsync("Remotes", "origin", "main");
        OpenMenu(app, app.BoundsOf(remoteMain).Center, RemoteMainMenu, "Merge origin/main into main");
        var first = Shot(app, "08f-remote-current-menu");
        AssertSelected(app, first);
        AssertGraph(app, G0);
        Assert.False(app.RefRowModel("Remotes", "origin", "main") is VisualCommit.App.ViewModels.Panels.RefItemRow { IsSelected: true }, "The right-click selected the row.");
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, null);

        // The second menu: origin/feature/alpha.
        app.Click(await app.RefRowAsync("Remotes", "origin", "feature"));
        var remoteAlpha = await app.RefRowAsync("Remotes", "origin", "feature", "alpha");
        OpenMenu(app, app.BoundsOf(remoteAlpha).Center, RemoteAlphaMenu, "Check out origin/feature/alpha");
        var second = Shot(app, "08g-remote-branch-menu");
        AssertSelected(app, second);
        AssertGraph(app, G0);

        // Step 7: the merge of the remote branch.
        await RunOperationAsync(app, "Merge origin/feature/alpha", () => app.ClickMenuItem("Merge origin/feature/alpha into main"));
        await WaitForGraphAsync(app, 9, "aac947e");
        var merged = Shot(app, "08h-remote-merged");
        AssertGraph(app, G3, merged);
        AssertColumns(app, author: true, date: true, sha: true);
        AssertSelected(app, merged);
        AssertStatus(app, StatusMergedRemoteAlpha);
        await AssertLocalBranchAsync(app, "main", 3, 0, head: true);
        AssertNoClippedText(app);
        Assert.Equal(Sha.MergeRemoteAlpha, await RevAsync(repo, "HEAD"));
        Assert.Equal("Merge remote-tracking branch 'origin/feature/alpha'", await MessageAsync(repo, "HEAD"));
        Assert.Equal([Sha.MainWorkFull, Sha.AlphaTwoFull], await ParentsAsync(repo, "HEAD"));
        Assert.Equal("A\tsrc/alpha.txt", await GitOutAsync(repo, "diff", "--name-status", "HEAD^1", "HEAD"));

        // Step 8: its details.
        app.Click(app.GraphRowPoint(0));
        await AssertDetailsAsync(app, "Merge remote-tracking branch 'origin/feature/alpha'", AppDate, null, [Sha.MainWork, Sha.AlphaTwo], "A alpha.txt src");

        // Step 9: merging feature/alpha, already in main, changes nothing.
        OpenMenu(app, LabelPoint(app, 5, "feature/alpha"), AlphaMenu("main", upToDate: true), "Check out feature/alpha");
        await RunOperationAsync(app, "Merge feature/alpha", () => app.ClickMenuItem("Merge feature/alpha into main"));
        var upToDate = Shot(app, "08i-up-to-date");
        AssertGraph(app, G3, upToDate);
        AssertSelected(app, upToDate, 5);
        AssertStatus(app, StatusUpToDate);
        Assert.Equal(Sha.MergeRemoteAlpha, await RevAsync(repo, "HEAD"));
        AssertNoClippedText(app);
    }

    // ---------------------------------------------------------------------------------------
    // Check 9: a merge stopped by a conflict (1100×700); check 9b: one stopped in a terminal.

    /// <summary>Check 9 (steps 1 to 10, one launch): the banner, its tooltip, the flagged file, the disabled actions, a refused checkout and Abort.</summary>
    [AvaloniaFact]
    public async Task Check_09_merge_stopped_by_a_conflict_banner_and_abort()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var palette = PaletteOf(app);

        // Step 2: the merge stops; no dialog, the banner on more than one line: phase 4's Continue
        // (disabled while the file conflicts; a merge has no Skip) and Abort stand on a row of their
        // own under the text, because beside them the 440-wide banner would leave its text less than
        // 240 px (phase 4's report, "Phase 3's banner"). The label's centre is the point the
        // real-window pass right-clicks.
        var label = LabelPoint(app, 1, "feature/conflict");
        Assert.True(
            Math.Abs(label.X - FeatureConflictLabelCentre.X) <= 1 && Math.Abs(label.Y - FeatureConflictLabelCentre.Y) <= 1,
            $"The label feature/conflict's centre is {label}; the real-window pass right-clicks {FeatureConflictLabelCentre}.");
        OpenMenu(app, label, FeatureConflictMenu, "Check out feature/conflict");
        await RunOperationAsync(app, "Merge feature/conflict", () => app.ClickMenuItem("Merge feature/conflict into main"));
        await app.WaitForAsync(() => Graph(app).ShowsWorkingRow && app.BannerBounds is not null, "the banner and the working-changes row");
        await WaitForDetailsAsync(app, "Conflicting change");
        var stopped = Shot(app, "09a-banner");
        await AssertNoDialogAsync(app);
        var banner = AssertBanner(app, MergeBanner, "Abort merge", oneLine: false, continueButton: BannerButton.Disabled(ResolveConflictsFirst), buttonsBelow: true);
        AssertBannerLook(app, banner, stopped);
        AssertGraph(app, G0, stopped);
        AssertWorkingRow(app, WorkingChangesOneConflict, stopped);
        AssertSelected(app, stopped, 1);
        AssertStatus(app, StatusMergeStopped, OperationStatusTone.Warning);
        AssertNoClippedText(app);
        var status = await GitOutAsync(repo, "status");
        Assert.Contains("You have unmerged paths.", status, StringComparison.Ordinal);
        Assert.Contains("both modified:   src/core.txt", status, StringComparison.Ordinal);
        Assert.StartsWith(Sha.Conflicting, GitDirFile(repo, "MERGE_HEAD"), StringComparison.Ordinal);
        Assert.Equal("Merge branch 'feature/conflict'\n\n# Conflicts:\n#\tsrc/core.txt\n", GitDirFile(repo, "MERGE_MSG"));
        Assert.Equal(CoreMergeConflict, WorkingFile(repo, "src/core.txt"));
        Assert.Equal(Sha.MainWorkFull, await RevAsync(repo, "HEAD"));
        await AssertNoBackupsAsync(repo);

        // Step 3: the banner's tooltip under the pointer (phase 4's design 10.3 for a merge).
        var tip = await RestOnAsync(app, app.FindByAutomationId("BannerText"));
        Shot(app, "09b-banner-tooltip", keepPointer: true);
        Assert.Equal(BannerToolTip, tip);

        // Step 4: the stage panel flags the conflicted file.
        // The pointer leaves the banner first: its tooltip, open below the pointer, lies over the
        // working-changes row and would take the click while the pointer is on it.
        app.MoveToNeutral();
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1, 1 with conflicts)", "Staged files (0)");
        var fileRow = await app.FileRowAsync("UnstagedFileList", "src/core.txt");
        var rowTip = await RestOnAsync(app, fileRow);
        var conflicted = Shot(app, "09c-conflicted-file", keepPointer: true);
        Assert.Equal(ConflictRowToolTip, rowTip);
        AssertConflictedStagePanel(app, palette);
        Assert.Equal("Stage files to commit", ((Button)app.FindByAutomationId("CommitButton")).Content);
        Assert.False(app.FindByAutomationId("CommitButton").IsEffectivelyEnabled, "The commit button is enabled with a conflict left.");
        _ = conflicted;

        // Step 5: the merge items are disabled while the merge is in progress.
        app.MoveToNeutral();
        OpenMenu(app, LabelPoint(app, 4, "feature/alpha"), AlphaMenuWhileMerging, "Check out feature/alpha");
        var disabledTip = await RestOnMenuItemAsync(app, "Merge feature/alpha into main");
        Shot(app, "09d-merge-disabled", keepPointer: true);
        Assert.Equal(FinishMergeFirst, disabledTip);
        AssertHighlighted(app.Menu(), "Check out feature/alpha");
        AssertSelected(app, null, 4);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, null);

        // Step 6: Pull and Stash are disabled with the same reason (D119).
        var pull = ToolbarButton(app, "PullButton");
        Assert.False(pull.IsEffectivelyEnabled, "Pull is enabled during the merge.");
        Assert.Equal(FinishMergeFirst, await RestOnAsync(app, pull));
        var stash = ToolbarButton(app, "StashButton");
        Assert.False(stash.IsEffectivelyEnabled, "Stash is enabled during the merge.");
        AssertTip(stash, FinishMergeFirst);
        app.MoveToNeutral();

        // Step 7: a checkout refused before git (phase 5's C12, design 9.3): no git switch runs.
        var callsBefore = app.GitCalls().Count;
        await RunOperationAsync(app, "Check out feature/alpha", () => app.DoubleClick(LabelPoint(app, 4, "feature/alpha")));
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, "09e-checkout-refused");
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(CheckoutBlockedTitle, FinishMergeFirst, Ops, null, ["Close"], []));
        Assert.DoesNotContain(app.GitCalls().Skip(callsBefore), call => call.CommandText == CheckoutCommand);
        AssertStatus(app, StatusCheckoutAlphaFailed, OperationStatusTone.Danger);
        AssertNoClippedText(app);
        Assert.NotNull(GitDirFile(repo, "MERGE_HEAD"));
        Assert.Equal(["## main...origin/main", "UU src/core.txt"], await StatusAsync(repo));
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await AssertNoDialogAsync(app);

        // Step 8: the banner's Abort asks first.
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await AssertConfirmationAsync(app, AbortMergeTitle, AbortMergeQuestion, BackupDetail, "Abort merge");
        var confirmation = Shot(app, "09f-abort-confirmation");
        AssertBackdrop(app, confirmation);
        AssertNoClippedText(app);

        // Step 9: Cancel changes nothing.
        app.Click(app.FindByAutomationId("CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.NotNull(GitDirFile(repo, "MERGE_HEAD"));
        await AssertNoBackupsAsync(repo);

        // Step 10: Abort.
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        await RunOperationAsync(app, "Abort the merge", () => app.Click(app.FindByAutomationId("ConfirmButton")));
        await app.WaitForAsync(() => app.BannerBounds is null && !Graph(app).ShowsWorkingRow, "the banner and the working-changes row to go");
        var aborted = Shot(app, "09g-aborted");
        AssertNoBanner(app);
        AssertNoWorkingRow(app);
        AssertGraph(app, G0, aborted);
        AssertSelected(app, aborted, 4);
        AssertStatus(app, StatusAbortedMerge);
        AssertNoClippedText(app);
        Assert.Null(GitDirFile(repo, "MERGE_HEAD"));
        Assert.Null(GitDirFile(repo, "MERGE_MSG"));
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, WorkingFile(repo, "src/core.txt"));
        var snapshot = await AssertBackupsAsync(repo, "abort", Sha.MainWorkFull, changes: true);
        Assert.Equal([Sha.MainWorkFull], await ParentsAsync(repo, snapshot!));
        Assert.Equal(CoreMergeConflict, await ShowAsync(repo, snapshot!, "src/core.txt"));
        AssertBackupsHidden(app);
    }

    /// <summary>Check 9b (steps 1 to 5): a merge stopped in a terminal, resolved in an editor, marked as resolved and committed in the stage panel.</summary>
    [AvaloniaFact]
    public async Task Check_09b_merge_stopped_in_a_terminal_resolved_and_committed()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        repo.WriteFile("src/core.txt", CoreResolved);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var palette = PaletteOf(app);

        // Step 2: the banner of the terminal's merge, Continue disabled and Abort under the text
        // (phase 4's report, "Phase 3's banner").
        var start = Shot(app, "09b1-terminal-merge");
        var banner = AssertBanner(app, MergeBanner, "Abort merge", oneLine: false, continueButton: BannerButton.Disabled(ResolveConflictsFirst), buttonsBelow: true);
        AssertBannerLook(app, banner, start);
        AssertGraph(app, G0, start);
        AssertWorkingRow(app, WorkingChangesOneConflict, start);
        AssertSelected(app, start);
        AssertStatus(app, "Ready");
        AssertNoClippedText(app);

        // Step 3: the conflicted file's menu; the right-click selects no row and opens no diff (4b).
        // Phase 4's "Resolve conflict" comes first and takes the highlight from "Mark as resolved".
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1, 1 with conflicts)", "Staged files (0)");
        var fileRow = await app.FileRowAsync("UnstagedFileList", "src/core.txt");
        OpenMenu(app, app.BoundsOf(fileRow).Center, ConflictedFileMenu, "Resolve conflict");
        var menu = Shot(app, "09b2-conflicted-file-menu");
        AssertConflictedStagePanel(app, palette);
        Assert.False(((VisualCommit.App.ViewModels.Panels.ChangedFileRow)fileRow.DataContext!).IsSelected, "The right-click selected the file's row.");
        Assert.False(app.Session.Shell.ActiveTab.Repository!.IsDiffOpen, "The right-click opened the diff.");
        _ = menu;

        // Step 4: "Mark as resolved" stages the file; the merge waits for its commit. The file has
        // no conflict markers left, so phase 4 asks nothing first (design 7.1, 7.2).
        app.ClickMenuItem("Mark as resolved");
        await WaitForStagePanelAsync(app, "Unstaged files (0)", "Staged files (1)");
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => ((TextBlock)app.FindByAutomationId("BannerText")).Text == MergeResolvedBanner, "the banner to say the conflicts are resolved");
        var resolved = Shot(app, "09b3-resolved");
        await AssertNoDialogAsync(app);
        AssertBanner(app, MergeResolvedBanner, "Abort merge", continueButton: BannerButton.Enabled, buttonsBelow: true);
        AssertWorkingRow(app, WorkingChanges, resolved);
        Assert.True(app.Find<TextBlock>("NoUnstaged").IsEffectivelyVisible, "\"No unstaged changes\" does not show.");
        Assert.Equal(["M core.txt src"], StageRows(app, "StagedFileList"));
        Assert.Equal("Merge branch 'feature/conflict'", app.Find<TextBox>("CommitSummary").Text);
        Assert.True(string.IsNullOrEmpty(app.Find<TextBox>("CommitDescription").Text), "The description is not empty.");
        var commit = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal("Commit 1 file", commit.Content);
        Assert.True(commit.IsEffectivelyEnabled, "The commit button is disabled.");
        AssertStatus(app, "Ready");
        AssertTip(ToolbarButton(app, "StashButton"), FinishMergeFirst);
        Assert.False(ToolbarButton(app, "StashButton").IsEffectivelyEnabled, "Stash is enabled while the merge waits for its commit (D119).");
        Assert.Equal(["## main...origin/main", "M  src/core.txt"], await StatusAsync(repo));
        Assert.NotNull(GitDirFile(repo, "MERGE_HEAD"));
        AssertNoClippedText(app);

        // Step 5: the commit concludes the merge.
        app.Click(commit);
        await WaitForGraphAsync(app, 9, "58e558d");
        await app.WaitForIdleAsync();
        await WaitForDetailsAsync(app, "Merge branch 'feature/conflict'");
        var committed = Shot(app, "09b4-merge-committed");
        AssertNoBanner(app);
        AssertNoWorkingRow(app);
        AssertGraph(app, G5, committed);
        AssertSelected(app, committed, 0);
        await AssertDetailsAsync(app, "Merge branch 'feature/conflict'", AppDate, null, [Sha.MainWork, Sha.Conflicting], "M core.txt src");
        Assert.True(string.IsNullOrEmpty(app.Find<TextBox>("CommitSummary").Text), "The summary is not empty after the commit.");
        Assert.True(string.IsNullOrEmpty(app.Find<TextBox>("CommitDescription").Text), "The description is not empty after the commit.");
        AssertStatus(app, "Ready");
        await AssertLocalBranchAsync(app, "main", 2, 0, head: true);
        AssertNoClippedText(app);
        Assert.Equal(Sha.MergeConflict, await RevAsync(repo, "HEAD"));
        Assert.Equal("Merge branch 'feature/conflict'", await MessageAsync(repo, "HEAD"));
        Assert.Equal([Sha.MainWorkFull, await RevAsync(repo, "refs/heads/feature/conflict")], await ParentsAsync(repo, "HEAD"));
        Assert.Equal((AppCommitTime, AppCommitTime), await DatesAsync(repo, "HEAD"));
        Assert.Equal(CoreResolved, await ShowAsync(repo, "HEAD", "src/core.txt"));
        Assert.Null(GitDirFile(repo, "MERGE_HEAD"));
        Assert.Null(GitDirFile(repo, "MERGE_MSG"));
        Assert.Equal(["## main...origin/main [ahead 2]"], await StatusAsync(repo));
    }

    // ---------------------------------------------------------------------------------------
    // Check 10: reset main to "Add core", soft, mixed and hard (1100×700).

    /// <summary>Check 10 part A (steps 1 to 5): the commit menu, the reset dialog, Esc, and a soft reset.</summary>
    [AvaloniaFact]
    public async Task Check_10_reset_dialog_and_soft_reset()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 2: "Add core"'s menu.
        OpenMenu(app, app.GraphRowPoint(6), CommitMenuOnMain(Sha.AddCore, "Add core"), "Check out this commit");
        await WaitForDetailsAsync(app, "Add core");
        var menu = Shot(app, "10a-commit-menu");
        AssertSelected(app, menu, 6);

        // Step 3: the reset dialog, Mixed chosen.
        app.ClickMenuItem("Reset main to this commit…");
        await app.WaitForDialogAsync<ResetDialogViewModel>();
        var dialog = Shot(app, "10b-reset-dialog");
        AssertResetDialog(app, dialog, hard: false);
        Assert.True(((RadioButton)app.FindByAutomationId("ResetMixed")).IsChecked == true, "Mixed is not chosen.");

        // Step 4: Esc closes it; nothing changed.
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(Sha.MainWorkFull, await RevAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        await AssertNoBackupsAsync(repo);

        // Step 5: a soft reset.
        app.RightClick(app.GraphRowPoint(6));
        app.ClickMenuItem("Reset main to this commit…");
        await app.WaitForDialogAsync<ResetDialogViewModel>();
        app.Click(app.FindByAutomationId("ResetSoft"));
        await RunOperationAsync(app, "Reset main", () => app.Click(app.FindByAutomationId("ConfirmButton")));
        await app.WaitForAsync(() => Graph(app).ShowsWorkingRow, "the working-changes row");
        var soft = Shot(app, "10c-soft");
        await AssertNoDialogAsync(app);
        AssertGraph(app, G6, soft);
        AssertWorkingRow(app, WorkingChanges, soft);
        AssertSelected(app, soft, 6);
        AssertStatus(app, StatusResetSoft);
        await AssertLocalBranchAsync(app, "main", 0, 1, head: true);
        AssertNoClippedText(app);
        Assert.Equal(Sha.AddCoreFull, await RevAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [behind 1]", "M  src/core.txt"], await StatusAsync(repo));
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, await ShowAsync(repo, string.Empty, "src/core.txt"));
        await AssertBackupsAsync(repo, "reset", Sha.MainWorkFull, changes: false);
        AssertBackupsHidden(app);
    }

    /// <summary>Check 10 part B (step 6): a mixed reset.</summary>
    [AvaloniaFact]
    public async Task Check_10_mixed_reset()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        app.RightClick(app.GraphRowPoint(6));
        app.ClickMenuItem("Reset main to this commit…");
        await app.WaitForDialogAsync<ResetDialogViewModel>();
        Assert.True(((RadioButton)app.FindByAutomationId("ResetMixed")).IsChecked == true, "Mixed is not chosen.");
        await RunOperationAsync(app, "Reset main", () => app.Click(app.FindByAutomationId("ConfirmButton")));
        await app.WaitForAsync(() => Graph(app).ShowsWorkingRow, "the working-changes row");
        await WaitForDetailsAsync(app, "Add core");
        var mixed = Shot(app, "10d-mixed");
        await AssertNoDialogAsync(app);
        AssertGraph(app, G6, mixed);
        AssertWorkingRow(app, WorkingChanges, mixed);
        AssertSelected(app, mixed, 6);
        AssertStatus(app, StatusResetMixed);
        await AssertLocalBranchAsync(app, "main", 0, 1, head: true);
        AssertNoClippedText(app);
        Assert.Equal(Sha.AddCoreFull, await RevAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main [behind 1]", " M src/core.txt"], await StatusAsync(repo));
        Assert.Equal(Scenarios.OperationsFiles.Core, await ShowAsync(repo, string.Empty, "src/core.txt"));
        await AssertBackupsAsync(repo, "reset", Sha.MainWorkFull, changes: false);
    }

    /// <summary>Check 10 part C (steps 7 to 10): a hard reset over staged, unstaged, new and untracked files, with both backups.</summary>
    [AvaloniaFact]
    public async Task Check_10_hard_reset_with_backups()
    {
        using var repo = await Scenarios.OperationsAsync();
        await WriteResetPartCFilesAsync(repo);
        var indexBefore = await GitOutAsync(repo, "write-tree");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 7: the working-changes row and the stage panel.
        AssertWorkingRow(app, WorkingChanges);
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (3)", "Staged files (2)");
        var before = Shot(app, "10e-hard-before");
        AssertGraph(app, G0, before);
        Assert.Equal(["M README.md", "M core.txt src", "A todo.txt"], StageRows(app, "UnstagedFileList"));
        Assert.Equal(["A scratch.txt", "M core.txt src"], StageRows(app, "StagedFileList"));
        AssertNoClippedText(app);

        // Step 8: the dialog with Hard chosen.
        app.RightClick(app.GraphRowPoint(6));
        app.ClickMenuItem("Reset main to this commit…");
        await app.WaitForDialogAsync<ResetDialogViewModel>();
        app.Click(app.FindByAutomationId("ResetHard"));
        await WaitForDetailsAsync(app, "Add core");
        var dialog = Shot(app, "10f-hard-dialog");
        AssertResetDialog(app, dialog, hard: true);
        AssertSelected(app, null, 6);

        // Step 9: the hard reset.
        await RunOperationAsync(app, "Reset main", () => app.Click(app.FindByAutomationId("ConfirmButton")));
        await app.WaitForAsync(() => Graph(app).Data!.LabelsAt(6).Any(label => label.Text == "main"), "main on \"Add core\"");
        await WaitForDetailsAsync(app, "Add core");
        var done = Shot(app, "10g-hard-done");
        await AssertNoDialogAsync(app);
        AssertGraph(app, G6, done);
        AssertWorkingRow(app, WorkingChanges, done);
        AssertSelected(app, done, 6);
        AssertStatus(app, StatusResetHard);
        AssertNoClippedText(app);

        // Step 10: only the untracked file is left.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1)", "Staged files (0)");
        var panel = Shot(app, "10h-hard-stage-panel");
        Assert.Equal(["A todo.txt"], StageRows(app, "UnstagedFileList"));
        Assert.True(app.Find<TextBlock>("NoStaged").IsEffectivelyVisible, "\"No staged changes\" does not show.");
        AssertNoClippedText(app);
        _ = panel;

        Assert.Equal(["## main...origin/main [behind 1]", "?? todo.txt"], await StatusAsync(repo));
        Assert.Equal(Scenarios.OperationsFiles.Readme, WorkingFile(repo, "README.md"));
        Assert.Equal(Scenarios.OperationsFiles.Core, WorkingFile(repo, "src/core.txt"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "scratch.txt")), "scratch.txt is still there.");
        Assert.Equal(Todo, WorkingFile(repo, "todo.txt"));
        var snapshot = await AssertBackupsAsync(repo, "reset", Sha.MainWorkFull, changes: true);
        var parents = await ParentsAsync(repo, snapshot!);
        Assert.Equal(2, parents.Length);
        Assert.Equal(Sha.MainWorkFull, parents[0]);
        Assert.Equal(indexBefore, await GitOutAsync(repo, "rev-parse", parents[1] + "^{tree}"));
        Assert.Equal(ReadmeLocalEdit, await ShowAsync(repo, snapshot!, "README.md"));
        Assert.Equal(Scratch, await ShowAsync(repo, snapshot!, "scratch.txt"));
        Assert.Equal(CoreUnstaged, await ShowAsync(repo, snapshot!, "src/core.txt"));
        Assert.DoesNotContain("todo.txt", (await GitOutAsync(repo, "ls-tree", "-r", "--name-only", snapshot!)).Split('\n'));
        Assert.Equal(Scenarios.OperationsFiles.Readme, await ShowAsync(repo, parents[1], "README.md"));
        Assert.Equal(Scratch, await ShowAsync(repo, parents[1], "scratch.txt"));
        Assert.Equal(CoreStaged, await ShowAsync(repo, parents[1], "src/core.txt"));
        AssertBackupsHidden(app);
    }

    // ---------------------------------------------------------------------------------------
    // Check 11: cherry-pick one commit, select several with Ctrl and Shift, pick two (1400×900);
    // check 11b: a pick of two that stops at the second.

    /// <summary>Check 11 part A (steps 1 to 4): the disabled pick of a commit on main, the pick of "Alpha one", its details.</summary>
    [AvaloniaFact]
    public async Task Check_11_cherry_pick_one_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);

        // Step 2: "Main work" is already on main (HEAD's own commit: phase 4's OC3).
        OpenMenu(app, app.GraphRowPoint(3), CommitMenuOnMain(Sha.MainWork, "Main work", headsOwn: true), "Check out this commit");
        await WaitForDetailsAsync(app, "Main work");
        var tip = await RestOnMenuItemAsync(app, "Cherry-pick onto main");
        var disabled = Shot(app, "11a-already-on-main", keepPointer: true);
        Assert.Equal("This commit is already on main", tip);
        AssertHighlighted(app.Menu(), "Check out this commit");
        AssertSelected(app, disabled, 3);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, null);

        // Step 3: the pick of "Alpha one".
        app.RightClick(app.GraphRowPoint(5));
        await RunOperationAsync(app, "Cherry-pick " + Sha.AlphaOne, () => app.ClickMenuItem("Cherry-pick onto main"));
        await WaitForGraphAsync(app, 9, "d1f9fb5");
        var picked = Shot(app, "11b-picked");
        AssertGraph(app, G7, picked);
        AssertColumns(app, author: true, date: true, sha: true);
        Assert.Equal(TempRepo.AuthorName, Graph(app).Data!.CommitAt(0).AuthorName);
        AssertSelected(app, picked, 6);
        AssertStatus(app, StatusPickedAlphaOne);
        await AssertLocalBranchAsync(app, "main", 1, 0, head: true);
        AssertNoClippedText(app);
        Assert.Equal(Sha.PickedAlphaOne, await RevAsync(repo, "HEAD"));
        Assert.Equal("Alpha one", await MessageAsync(repo, "HEAD"));
        Assert.Equal([Sha.MainWorkFull], await ParentsAsync(repo, "HEAD"));
        Assert.Equal((ScenarioTime(2), AppCommitTime), await DatesAsync(repo, "HEAD"));
        Assert.Equal("A\tsrc/alpha.txt", await GitOutAsync(repo, "diff", "--name-status", "HEAD^", "HEAD"));
        Assert.Equal(Scenarios.OperationsFiles.AlphaOne, await ShowAsync(repo, "HEAD", "src/alpha.txt"));
        Assert.Null(GitDirFile(repo, "CHERRY_PICK_HEAD"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await StatusAsync(repo));
        await AssertNoBackupsAsync(repo);

        // Step 4: the picked commit's details show its commit date.
        app.Click(app.GraphRowPoint(0));
        await AssertDetailsAsync(app, "Alpha one", "2026-01-01 12:02", AppDate, [Sha.MainWork], "A alpha.txt src");
        var details = Shot(app, "11c-picked-details");
        AssertSelected(app, details, 0);
        AssertNoClippedText(app);
    }

    /// <summary>Check 11 part B (steps 5 to 12): Ctrl and Shift selections, the several-commits menu, the pick of two.</summary>
    [AvaloniaFact]
    public async Task Check_11_select_several_and_cherry_pick_two()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);
        var command = app.CommandModifier;

        // Step 5: a click and a Ctrl+click.
        app.Click(app.GraphRowPoint(5));
        app.Click(app.GraphRowPoint(4), command);
        await app.WaitForAsync(() => Shown(app, "MultiSelectionView") is not null, "the multi-selection view");
        var two = Shot(app, "11d-two-selected");
        AssertSelected(app, two, 4, 5);
        AssertMultiSelection(app, "ce5ea6a Alpha two", "dff8fbf Alpha one");
        AssertNoClippedText(app);

        // Step 6: a third commit, on main: its pick is disabled, and so is phase 4's squash. The
        // pointer reaches the pick crossing only the disabled squash.
        app.Click(app.GraphRowPoint(3), command);
        OpenMenu(app, app.GraphRowPoint(3), ThreeCommitsMenu, "Revert 3 commits");
        var tip = await RestOnMenuItemAsync(app, "Cherry-pick 3 commits onto main");
        var three = Shot(app, "11e-three-disabled", keepPointer: true);
        Assert.Equal("Some of these commits are already on main", tip);
        AssertHighlighted(app.Menu(), "Revert 3 commits");
        AssertSelected(app, three, 3, 4, 5);
        AssertMultiSelection(app, "88d6b91 Main work", "ce5ea6a Alpha two", "dff8fbf Alpha one");
        Assert.DoesNotContain(app.Menu().Items, item => item.Label.Contains("Compare", StringComparison.Ordinal));
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, null);

        // Step 7: Ctrl+click takes a row out again.
        app.Click(app.GraphRowPoint(3), command);
        AssertSelected(app, null, 4, 5);
        AssertMultiSelection(app, "ce5ea6a Alpha two", "dff8fbf Alpha one");

        // Step 8: a click, then a Shift+click.
        app.Click(app.GraphRowPoint(4));
        AssertSelected(app, null, 4);
        await WaitForDetailsAsync(app, "Alpha two");
        app.Click(app.GraphRowPoint(5), KeyModifiers.Shift);
        AssertSelected(app, null, 4, 5);
        AssertMultiSelection(app, "ce5ea6a Alpha two", "dff8fbf Alpha one");

        // Step 9: Shift with the arrow keys.
        app.PressKey(Key.Down, RawInputModifiers.Shift);
        var shiftDown = Shot(app, "11f-shift-down");
        AssertSelected(app, shiftDown, 4, 5, 6);
        AssertMultiSelection(app, "ce5ea6a Alpha two", "dff8fbf Alpha one", "8beca34 Add core");
        app.PressKey(Key.Up, RawInputModifiers.Shift);
        AssertSelected(app, null, 4, 5);

        // Step 10: a right-click on a selected row keeps the selection. Every item is enabled but
        // phase 4's squash (no longer "all enabled": neither commit is on main).
        OpenMenu(app, app.GraphRowPoint(5), TwoCommitsMenu, "Cherry-pick 2 commits onto main");
        var menu = Shot(app, "11g-multi-menu");
        Assert.All(app.Menu().Items.Where(item => item.Label != "Squash 2 commits…"), item => Assert.True(item.IsEnabled, $"\"{item.Label}\" is disabled."));
        AssertSelected(app, menu, 4, 5);

        // Step 11: the pick of two, oldest first.
        await RunOperationAsync(app, "Cherry-pick 2 commits", () => app.ClickMenuItem("Cherry-pick 2 commits onto main"));
        await WaitForGraphAsync(app, 10, "c0f718d");
        var picked = Shot(app, "11h-two-picked");
        AssertGraph(app, G8, picked);
        AssertSelected(app, picked, 6, 7);
        AssertMultiSelection(app, "ce5ea6a Alpha two", "dff8fbf Alpha one");
        AssertStatus(app, StatusPickedTwo);
        await AssertLocalBranchAsync(app, "main", 2, 0, head: true);
        AssertNoClippedText(app);
        Assert.Equal(["c0f718d Alpha two", "d1f9fb5 Alpha one", "88d6b91 Main work"], (await GitOutAsync(repo, "log", "--format=%h %s", "-3", "main")).Split('\n'));
        Assert.Equal(Sha.PickedAlphaTwo, await RevAsync(repo, "HEAD"));
        Assert.Equal(Sha.PickedAlphaOne, await RevAsync(repo, "HEAD^"));
        Assert.Equal((ScenarioTime(3), AppCommitTime), await DatesAsync(repo, "HEAD"));
        Assert.Equal((ScenarioTime(2), AppCommitTime), await DatesAsync(repo, "HEAD^"));
        Assert.Equal(Scenarios.OperationsFiles.AlphaTwo, WorkingFile(repo, "src/alpha.txt"));
        Assert.False(HasSequencer(repo), "git's folder has a sequencer folder.");
        Assert.Null(GitDirFile(repo, "CHERRY_PICK_HEAD"));

        // Step 12: Ctrl on a stash row acts as a plain click.
        app.Click(app.GraphRowPoint(2), command);
        AssertSelected(app, null, 2);
        await WaitForDetailsAsync(app, "On main: Experiment");
        Assert.Null(Shown(app, "MultiSelectionView"));
    }

    /// <summary>
    /// Check 11b (steps 1 to 5): a pick of two that stops at the second, its banner and status, and
    /// an Abort. Phase 4 picks the two with one git call, so the stop is git's list
    /// (<c>sequencer/</c>), the banner offers Continue and Skip, and Abort drops the first pick too
    /// (phase 4's report, "Changes to expected results": phase 3's check 11b).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_11b_cherry_pick_of_two_stops_at_the_second()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);
        var alphaOne = await RevAsync(repo, Sha.AlphaOne);
        var conflicting = await RevAsync(repo, Sha.Conflicting);

        // Step 2: "Alpha one" is picked, "Conflicting change" stops. The menu is check 11 step
        // 10's (145 high); the app runs one git call with both commits, oldest first.
        app.Click(app.GraphRowPoint(1));
        app.Click(app.GraphRowPoint(5), app.CommandModifier);
        OpenMenu(app, app.GraphRowPoint(5), TwoCommitsMenu, "Cherry-pick 2 commits onto main");
        var calls = app.GitCalls().Count;
        await RunOperationAsync(app, "Cherry-pick 2 commits", () => app.ClickMenuItem("Cherry-pick 2 commits onto main"));
        await app.WaitForAsync(() => app.BannerBounds is not null && Graph(app).ShowsWorkingRow, "the banner and the working-changes row");
        await WaitForGraphAsync(app, 9, "d1f9fb5");
        var stopped = Shot(app, "11b1-pick-stopped");
        await AssertNoDialogAsync(app);

        // At 1400×900 the 740-wide banner keeps more than 240 px for its text: the buttons stand
        // beside it; no commit waits, so the text is phase 3's.
        var banner = AssertBanner(
            app,
            CherryPickBanner,
            "Abort cherry-pick",
            continueButton: BannerButton.Disabled(ResolveConflictsFirst),
            skipButton: BannerButton.Enabled,
            buttonsBelow: false);
        AssertBannerLook(app, banner, stopped);
        AssertGraph(app, G7, stopped);
        AssertWorkingRow(app, WorkingChangesOneConflict, stopped);
        AssertSelected(app, stopped, 2, 6);
        AssertMultiSelection(app, "078ef05 Conflicting change", "dff8fbf Alpha one");
        AssertStatus(app, StatusPickStopped, OperationStatusTone.Warning);
        AssertNoClippedText(app);
        var picks = app.GitCalls().Skip(calls).Where(call => call.CommandText.StartsWith("git cherry-pick ", StringComparison.Ordinal)).ToList();
        var pick = Assert.Single(picks);
        Assert.Equal($"git cherry-pick {alphaOne} {conflicting}", pick.CommandText);
        Assert.Equal(1, pick.ExitCode);
        Assert.Equal(Sha.PickedAlphaOne, await RevAsync(repo, "refs/heads/main"));
        Assert.Equal([Sha.MainWorkFull], await ParentsAsync(repo, "refs/heads/main"));
        Assert.StartsWith(Sha.Conflicting, GitDirFile(repo, "CHERRY_PICK_HEAD"), StringComparison.Ordinal);
        Assert.True(HasSequencer(repo), "git's folder has no sequencer folder: the two picks were not one list.");
        Assert.Equal("Conflicting change\n\n# Conflicts:\n#\tsrc/core.txt\n", GitDirFile(repo, "MERGE_MSG"));
        Assert.Equal("UU src/core.txt", await GitOutAsync(repo, "status", "--short"));
        Assert.Equal(CoreCherryPickConflict, WorkingFile(repo, "src/core.txt"));

        // Step 3: the status's tooltip holds the whole message.
        var tip = await RestOnAsync(app, app.FindByAutomationId("OperationStatus"));
        Shot(app, "11b2-status-tooltip", keepPointer: true);
        Assert.Equal(StatusPickStopped, tip);

        // Step 4: the banner's Abort asks first; the list's commits already made are dropped too.
        app.MoveToNeutral();
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await AssertConfirmationAsync(app, AbortCherryPickTitle, AbortCherryPickListQuestion, BackupDetail, "Abort cherry-pick");
        var confirmation = Shot(app, "11b3-abort-confirmation");
        AssertBackdrop(app, confirmation);
        AssertNoClippedText(app);
        AssertBanner(
            app,
            CherryPickBanner,
            "Abort cherry-pick",
            continueButton: BannerButton.Disabled(ResolveConflictsFirst),
            skipButton: BannerButton.Enabled,
            buttonsBelow: false);

        // Step 5: Abort undoes the whole list: main goes back to where it was before the first
        // pick (G0), and the backup keeps the pick of "Alpha one" with the conflicted file.
        await RunOperationAsync(app, "Abort the cherry-pick", () => app.Click(app.FindByAutomationId("ConfirmButton")));
        await app.WaitForAsync(() => app.BannerBounds is null && !Graph(app).ShowsWorkingRow, "the banner and the working-changes row to go");
        await WaitForGraphAsync(app, 8, Sha.Stash);
        var aborted = Shot(app, "11b4-aborted");
        await AssertNoDialogAsync(app);
        AssertNoBanner(app);
        AssertNoWorkingRow(app);
        AssertGraph(app, G0, aborted);
        AssertSelected(app, aborted, 1, 5);
        AssertMultiSelection(app, "078ef05 Conflicting change", "dff8fbf Alpha one");
        AssertStatus(app, StatusAbortedPick);
        await AssertLocalBranchAsync(app, "main", 0, 0, head: true);
        AssertNoClippedText(app);
        Assert.Equal(Sha.MainWorkFull, await RevAsync(repo, "refs/heads/main"));
        Assert.Equal(["## main...origin/main"], await StatusAsync(repo));
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, WorkingFile(repo, "src/core.txt"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "src", "alpha.txt")), "src/alpha.txt, which only the pick of \"Alpha one\" added, is still there.");
        Assert.Null(GitDirFile(repo, "CHERRY_PICK_HEAD"));
        Assert.Null(GitDirFile(repo, "MERGE_MSG"));
        Assert.False(HasSequencer(repo), "git's folder still has a sequencer folder.");
        var snapshot = await AssertBackupsAsync(repo, "abort", Sha.PickedAlphaOne, changes: true);
        Assert.Equal([Sha.PickedAlphaOne], await ParentsAsync(repo, snapshot!));
        Assert.Equal(CoreCherryPickConflict, await ShowAsync(repo, snapshot!, "src/core.txt"));
        AssertBackupsHidden(app);
    }

    // ---------------------------------------------------------------------------------------
    // Check 12: revert a commit, a merge commit, two commits, a revert stopped by a conflict,
    // and one that changes nothing (1400×900).

    /// <summary>Check 12 part A (step 2): "Revert this commit" on "Main work".</summary>
    [AvaloniaFact]
    public async Task Check_12_revert_a_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);

        await RevertMainWorkAsync(app, 3);
        var reverted = Shot(app, "12a-reverted");
        AssertRevertedMainWork(app, reverted);
        AssertNoClippedText(app);
        Assert.Equal(Sha.RevertMainWork, await RevAsync(repo, "HEAD"));
        Assert.Equal($"Revert \"Main work\"\n\nThis reverts commit {Sha.MainWorkFull}.", await MessageAsync(repo, "HEAD"));
        Assert.Equal([Sha.MainWorkFull], await ParentsAsync(repo, "HEAD"));
        Assert.Equal((AppCommitTime, AppCommitTime), await DatesAsync(repo, "HEAD"));
        Assert.Equal(Scenarios.OperationsFiles.Core, WorkingFile(repo, "src/core.txt"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await StatusAsync(repo));
    }

    /// <summary>Check 12 part B (steps 3 to 6): a merge commit with another commit, the merge's own menu, and its revert (<c>-m 1</c>).</summary>
    [AvaloniaFact]
    public async Task Check_12_revert_a_merge_commit()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);
        var command = app.CommandModifier;

        // Step 3: the merge of feature/alpha (no picture).
        app.RightClick(LabelPoint(app, 4, "feature/alpha"));
        await RunOperationAsync(app, "Merge feature/alpha", () => app.ClickMenuItem("Merge feature/alpha into main (create a merge commit)"));
        await WaitForGraphAsync(app, 9, "f19f50a");
        AssertGraph(app, G10BeforeRevert);
        AssertStatus(app, StatusMergedAlpha);
        await AssertLocalBranchAsync(app, "main", 3, 0, head: true);
        Assert.Equal(Sha.MergeAlpha, await RevAsync(repo, "HEAD"));

        // Step 4: the merge and "Main work" together.
        app.Click(app.GraphRowPoint(0));
        app.Click(app.GraphRowPoint(4), command);
        OpenMenu(app, app.GraphRowPoint(4), MergeAndOtherMenu, "Copy SHAs");
        var both = Shot(app, "12b-merge-and-other-menu");
        AssertSelected(app, both, 0, 4);
        AssertMultiSelection(app, "f19f50a Merge branch 'feature/alpha'", "88d6b91 Main work");
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, null);

        // Step 5: the merge alone.
        app.Click(app.GraphRowPoint(0));
        OpenMenu(app, app.GraphRowPoint(0), MergeCommitMenu, "Check out this commit");
        await WaitForDetailsAsync(app, "Merge branch 'feature/alpha'");
        var merge = Shot(app, "12c-merge-menu");
        AssertSelected(app, merge, 0);

        // Step 6: its revert.
        await RunOperationAsync(app, "Revert f19f50a", () => app.ClickMenuItem("Revert this merge commit"));
        await WaitForGraphAsync(app, 10, "f5df804");
        var reverted = Shot(app, "12d-merge-reverted");
        AssertGraph(app, G10, reverted);
        AssertSelected(app, reverted, 1);
        AssertStatus(app, StatusRevertedMerge);
        await AssertLocalBranchAsync(app, "main", 4, 0, head: true);
        AssertNoClippedText(app);
        Assert.Equal(Sha.RevertMergeAlpha, await RevAsync(repo, "HEAD"));
        Assert.Equal(
            $"Revert \"Merge branch 'feature/alpha'\"\n\nThis reverts commit {Sha.MergeAlpha}, reversing\nchanges made to {Sha.MainWorkFull}.",
            await MessageAsync(repo, "HEAD"));
        Assert.Equal([Sha.MergeAlpha], await ParentsAsync(repo, "HEAD"));
        Assert.Equal("D\tsrc/alpha.txt", await GitOutAsync(repo, "diff", "--name-status", "HEAD^", "HEAD"));
    }

    /// <summary>
    /// Check 12 part C (step 7): "Revert 2 commits", newest first: phase 4's one git call with the
    /// whole list (design section 4, D136), which makes the same two commits.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_12_revert_two_commits()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);

        // The two commits are a chain on main's line: phase 4's squash is enabled and highlighted
        // first; the step still clicks "Revert 2 commits".
        app.Click(app.GraphRowPoint(3));
        app.Click(app.GraphRowPoint(6), app.CommandModifier);
        OpenMenu(app, app.GraphRowPoint(6), TwoCommitsOnMainMenu, "Squash 2 commits…");
        var calls = app.GitCalls().Count;
        await RunOperationAsync(app, "Revert 2 commits", () => app.ClickMenuItem("Revert 2 commits"));
        await WaitForGraphAsync(app, 10, "123d13b");
        var reverts = app.GitCalls().Skip(calls).Where(call => call.CommandText.StartsWith("git revert ", StringComparison.Ordinal)).ToList();
        var revert = Assert.Single(reverts);
        Assert.Equal(RevertTwoCommand, revert.CommandText);
        Assert.Equal(0, revert.ExitCode);
        Assert.False(HasSequencer(repo), "git's folder still has a sequencer folder.");
        var reverted = Shot(app, "12e-two-reverted");
        AssertGraph(app, G11, reverted);
        AssertSelected(app, reverted, 5, 8);
        AssertMultiSelection(app, "88d6b91 Main work", "8beca34 Add core");
        AssertStatus(app, StatusRevertedTwo);
        await AssertLocalBranchAsync(app, "main", 2, 0, head: true);
        AssertNoClippedText(app);
        Assert.Equal(
            ["123d13b Revert \"Add core\"", "13c773d Revert \"Main work\"", "88d6b91 Main work"],
            (await GitOutAsync(repo, "log", "--format=%h %s", "-3", "main")).Split('\n'));
        Assert.Equal(Sha.RevertAddCore, await RevAsync(repo, "HEAD"));
        Assert.Equal(Sha.RevertMainWork, await RevAsync(repo, "HEAD^"));
        Assert.Equal($"Revert \"Add core\"\n\nThis reverts commit {Sha.AddCoreFull}.", await MessageAsync(repo, "HEAD"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "src", "core.txt")), "src/core.txt still exists.");
        Assert.Equal(["## main...origin/main [ahead 2]"], await StatusAsync(repo));
    }

    /// <summary>Check 12 part D (steps 8 and 9): a revert stopped by a conflict, its banner, and its Abort.</summary>
    [AvaloniaFact]
    public async Task Check_12_revert_stopped_by_a_conflict_and_aborted()
    {
        using var repo = await Scenarios.OperationsAsync();
        Assert.Equal(Sha.CoreAgain, await repo.CommitFileAsync("src/core.txt", Scenarios.OperationsFiles.CoreAgain, "Core again"));
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);
        AssertGraph(app, G12);
        await AssertLocalBranchAsync(app, "main", 1, 0, head: true);

        // Step 8: the revert stops.
        await RevertMainWorkAsync(app, 4);
        await app.WaitForAsync(() => app.BannerBounds is not null && Graph(app).ShowsWorkingRow, "the banner and the working-changes row");
        await WaitForDetailsAsync(app, "Main work");
        var stopped = Shot(app, "12f-revert-stopped");
        await AssertNoDialogAsync(app);

        // A single revert: Continue (disabled while the file conflicts), Skip and Abort beside the
        // 740-wide banner's text (phase 4's report, "Phase 3's banner").
        var banner = AssertBanner(
            app,
            RevertBanner,
            "Abort revert",
            continueButton: BannerButton.Disabled(ResolveConflictsFirst),
            skipButton: BannerButton.Enabled,
            buttonsBelow: false);
        AssertBannerLook(app, banner, stopped);
        AssertGraph(app, G12, stopped);
        AssertWorkingRow(app, WorkingChangesOneConflict, stopped);
        AssertSelected(app, stopped, 4);
        AssertStatus(app, StatusRevertStopped, OperationStatusTone.Warning);
        AssertNoClippedText(app);
        Assert.Equal(["## main...origin/main [ahead 1]", "UU src/core.txt"], await StatusAsync(repo));
        Assert.StartsWith(Sha.MainWork, GitDirFile(repo, "REVERT_HEAD"), StringComparison.Ordinal);
        Assert.False(HasSequencer(repo), "git's folder has a sequencer folder.");
        Assert.Equal($"Revert \"Main work\"\n\nThis reverts commit {Sha.MainWorkFull}.\n\n# Conflicts:\n#\tsrc/core.txt\n", GitDirFile(repo, "MERGE_MSG"));
        Assert.Equal(CoreRevertConflict, WorkingFile(repo, "src/core.txt"));

        // Step 9: Abort, with its confirmation.
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await AssertConfirmationAsync(app, AbortRevertTitle, AbortRevertQuestion, BackupDetail, "Abort revert");
        var confirmation = Shot(app, "12g-abort-revert-confirmation");
        AssertBackdrop(app, confirmation);
        AssertNoClippedText(app);
        AssertBanner(
            app,
            RevertBanner,
            "Abort revert",
            continueButton: BannerButton.Disabled(ResolveConflictsFirst),
            skipButton: BannerButton.Enabled,
            buttonsBelow: false);
        await RunOperationAsync(app, "Abort the revert", () => app.Click(app.FindByAutomationId("ConfirmButton")));
        await app.WaitForAsync(() => app.BannerBounds is null && !Graph(app).ShowsWorkingRow, "the banner and the working-changes row to go");
        var aborted = Shot(app, "12h-revert-aborted");
        await AssertNoDialogAsync(app);
        AssertNoBanner(app);
        AssertNoWorkingRow(app);
        AssertGraph(app, G12, aborted);
        AssertSelected(app, aborted, 4);
        AssertStatus(app, StatusAbortedRevert);
        AssertNoClippedText(app);
        Assert.Equal(Sha.CoreAgain, await RevAsync(repo, "HEAD"));
        Assert.Equal(["## main...origin/main [ahead 1]"], await StatusAsync(repo));
        Assert.Equal(Scenarios.OperationsFiles.CoreAgain, WorkingFile(repo, "src/core.txt"));
        Assert.Null(GitDirFile(repo, "REVERT_HEAD"));
        Assert.Null(GitDirFile(repo, "MERGE_MSG"));
        var snapshot = await AssertBackupsAsync(repo, "abort", Sha.CoreAgain, changes: true);
        Assert.Equal([Sha.CoreAgain], await ParentsAsync(repo, snapshot!));
        Assert.Equal(CoreRevertConflict, await ShowAsync(repo, snapshot!, "src/core.txt"));
        AssertBackupsHidden(app);
    }

    /// <summary>Check 12 part E (steps 10 to 12): reverting "Main work" twice; the second changes nothing (D117).</summary>
    [AvaloniaFact]
    public async Task Check_12_revert_that_changes_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, LargeSize.Width, LargeSize.Height);

        // Step 10: as part A.
        await RevertMainWorkAsync(app, 3);
        AssertRevertedMainWork(app, null);

        // Step 11: the menu still offers the revert (2.14: git decides), which changes nothing.
        OpenMenu(app, app.GraphRowPoint(4), CommitMenuOnMain(Sha.MainWork, "Main work"), "Check out this commit");
        await RunOperationAsync(app, "Revert " + Sha.MainWork, () => app.ClickMenuItem("Revert this commit"));
        if (GitDirFile(repo, "REVERT_HEAD") is null)
        {
            // Git 2.36: an ordinary failure with git's own output (D117).
            await app.WaitForDialogAsync<ErrorDialogViewModel>();
            Shot(app, "12i-revert-changes-nothing");
            await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
                RevertFailedTitle, EmptyRevertSummary, Ops, EmptyRevertCommand, ["Copy output", "Close"], EmptyRevertLines, KeyLine: EmptyRevertLines[^1]));
            AssertStatus(app, StatusRevertFailed, OperationStatusTone.Danger);
            AssertNoClippedText(app);
            AssertNoBanner(app);
            AssertNoWorkingRow(app);
            Assert.Equal(Sha.RevertMainWork, await RevAsync(repo, "HEAD"));
            Assert.False(HasSequencer(repo), "git's folder has a sequencer folder.");
            Assert.Equal(["## main...origin/main [ahead 1]"], await StatusAsync(repo));
            await AssertNoBackupsAsync(repo);
            if (await IsReportGitAsync())
            {
                Assert.Equal(EmptyRevertMergeMsg, GitDirFile(repo, "MERGE_MSG"));
            }

            // Step 12.
            app.Click(app.FindByAutomationId("ConfirmButton"));
            await AssertNoDialogAsync(app);
            AssertRevertedMainWork(app, null, StatusRevertFailed, OperationStatusTone.Danger);
            return;
        }

        // A git that keeps REVERT_HEAD after an empty revert (D117's other case): the banner of an
        // empty revert in phase 4's words, Continue disabled ("skip it"), Skip and Abort beside the
        // text, and Abort leaves HEAD where it was.
        Assert.False(await IsReportGitAsync(), "Git 2.36 kept REVERT_HEAD after an empty revert.");
        await AssertNoDialogAsync(app);
        await app.WaitForAsync(() => app.BannerBounds is not null, "the banner of the empty revert");
        Shot(app, "12i-revert-changes-nothing");
        AssertBanner(
            app,
            EmptyRevertBanner,
            "Abort revert",
            continueButton: BannerButton.Disabled(RevertChangesNothing),
            skipButton: BannerButton.Enabled,
            buttonsBelow: false);
        Assert.StartsWith("Revert stopped", app.Session.Shell.ActiveTab.OperationStatus.Text, StringComparison.Ordinal);
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        await RunOperationAsync(app, "Abort the revert", () => app.Click(app.FindByAutomationId("ConfirmButton")));
        Assert.Equal(Sha.RevertMainWork, await RevAsync(repo, "HEAD"));
    }

    /// <summary>Right-clicks "Main work" (commit row <paramref name="row"/>) and clicks "Revert this commit", waiting until the revert has ended.</summary>
    private static async Task RevertMainWorkAsync(ShellDriver app, int row)
    {
        app.RightClick(app.GraphRowPoint(row));
        await RunOperationAsync(app, "Revert " + Sha.MainWork, () => app.ClickMenuItem("Revert this commit"));
    }

    /// <summary>G9, "Main work" (row 4) selected, main ↑1, and the status of check 12 step 2 (or the one given).</summary>
    private static void AssertRevertedMainWork(ShellDriver app, Screenshot? picture, string status = StatusRevertedMainWork, OperationStatusTone tone = OperationStatusTone.Secondary)
    {
        AssertGraph(app, G9, picture);
        AssertColumns(app, author: true, date: true, sha: true);
        AssertSelected(app, picture, 4);
        AssertStatus(app, status, tone);
        AssertNoWorkingRow(app);
        var main = (VisualCommit.App.ViewModels.Panels.RefItemRow)app.RefRowModel("Local branches", "main");
        Assert.Equal((1, 0), (main.Ahead, main.Behind));
    }

    // ---------------------------------------------------------------------------------------
    // Check 32: leaving a detached HEAD whose commit no ref reaches (edges, 1100×700).

    /// <summary>Check 32 (steps 1 to 8): the detached labels and menus, the confirmation before leaving the commit, its backup, and a detached HEAD that needs none.</summary>
    [AvaloniaFact]
    public async Task Check_32_leaving_a_detached_head()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var palette = PaletteOf(app);

        // Step 1: E0.
        var start = Shot(app, "32a-detached");
        AssertGraph(app, E0, start);
        AssertColumns(app, author: false, date: true, sha: false);
        AssertSelected(app, start);
        AssertStatus(app, "Ready");
        AssertBranch(app, "Detached at " + Sha.DetachedWork);
        var head = Graph(app).LabelsInRow(0)[0];
        Assert.Equal(("HEAD", VisualCommit.App.ViewModels.Graph.RefLabelKind.DetachedHead), (head.Text, head.Kind));
        AssertColour(palette.Lane(0), start, app.BoundsOf(Graph(app)).TopLeft + new Point(head.Bounds.Left + 2, head.Bounds.Center.Y), "the filled HEAD label");
        await AssertLocalBranchAsync(app, "main", 0, 0, head: false);
        AssertNoClippedText(app);

        // Step 2: the detached commit's menu.
        OpenMenu(app, app.GraphRowPoint(0), DetachedCommitMenu, "Create branch here…");
        await WaitForDetailsAsync(app, "Detached work");
        var commitMenu = Shot(app, "32b-detached-commit-menu");
        AssertSelected(app, commitMenu, 0);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, null);

        // Step 3: main's menu while HEAD is detached; the right-click selects nothing (D114).
        var mainRow = await app.RefRowAsync("Local branches", "main");
        OpenMenu(app, app.BoundsOf(mainRow).Center, MainMenuWhileDetached, "Check out main");
        var branchMenu = Shot(app, "32c-branch-menu-detached");
        AssertSelected(app, branchMenu, 0);
        Assert.False(((VisualCommit.App.ViewModels.Panels.RefItemRow)app.RefRowModel("Local branches", "main")).IsSelected, "The right-click selected main's row.");
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, null);

        // Step 4: a double-click asks before leaving "Detached work".
        Assert.Equal("1", await GitOutAsync(repo, "rev-list", "--count", "HEAD", "--not", "--branches", "--remotes", "--tags"));
        app.DoubleClick(await app.RefRowAsync("Local branches", "main"));
        await AssertConfirmationAsync(app, LeaveTitle, LeaveQuestion, BackupDetail, "Check out", destructive: false);
        await WaitForDetailsAsync(app, "Main work");
        var confirmation = Shot(app, "32d-leave-confirmation");
        AssertSelected(app, null, 2);
        Assert.True(((VisualCommit.App.ViewModels.Panels.RefItemRow)app.RefRowModel("Local branches", "main")).IsSelected, "The first click did not select main's row.");
        Assert.StartsWith(Sha.DetachedWork, await RevAsync(repo, "HEAD"), StringComparison.Ordinal);
        AssertBackdrop(app, confirmation);
        AssertNoClippedText(app);

        // Step 5: Cancel.
        app.Click(app.FindByAutomationId("CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.StartsWith(Sha.DetachedWork, await RevAsync(repo, "HEAD"), StringComparison.Ordinal);
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "-q", "HEAD")).Succeeded, "HEAD is on a branch.");
        await AssertNoBackupsAsync(repo);

        // Step 6: "Check out" leaves the commit, after its backup.
        app.DoubleClick(await app.RefRowAsync("Local branches", "main"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        await RunOperationAsync(app, "Check out main", () => app.Click(app.FindByAutomationId("ConfirmButton")), Edges);
        await WaitForGraphAsync(app, 3, Sha.EdgesAlphaWork);
        var checkedOut = Shot(app, "32e-checked-out");
        await AssertNoDialogAsync(app);
        AssertGraph(app, E1, checkedOut);
        AssertSelected(app, checkedOut, 1);
        AssertBranch(app, "main");
        AssertStatus(app, StatusCheckedOutMain);
        await AssertLocalBranchAsync(app, "main", 0, 0, head: true);
        AssertNoClippedText(app);
        Assert.Equal("refs/heads/main", await GitOutAsync(repo, "symbolic-ref", "HEAD"));
        Assert.Equal(["## main...remotes/origin/main"], await StatusAsync(repo));
        var backups = await BackupsAsync(repo);
        var backup = Assert.Single(backups);
        Assert.Equal(("detached", false, (string?)null), (backup.Ref.Kind, backup.Ref.IsChanges, backup.Ref.Name));
        Assert.StartsWith(Sha.DetachedWork, backup.Sha, StringComparison.Ordinal);
        Assert.Equal("side", await GitOutAsync(repo, "-C", repo.SiblingPath(Scenarios.EdgeCasesWorktree), "symbolic-ref", "--short", "HEAD"));
        AssertBackupsHidden(app);

        // Step 7: detaching at a commit main reaches asks nothing.
        app.RightClick(app.GraphRowPoint(2));
        await RunOperationAsync(app, "Check out " + Sha.EdgesInitial, () => app.ClickMenuItem("Check out this commit"), Edges);
        await app.WaitForAsync(() => Graph(app).Data!.LabelsAt(2).FirstOrDefault()?.Text == "HEAD", "the HEAD label on \"Initial commit\"");
        await WaitForDetailsAsync(app, "Initial commit");
        var detached = Shot(app, "32f-detached-reachable");
        await AssertNoDialogAsync(app);
        AssertGraph(app, E1Detached, detached);
        AssertSelected(app, detached, 2);
        AssertBranch(app, "Detached at " + Sha.EdgesInitial);
        AssertStatus(app, StatusDetachedAtInitial);
        await AssertLocalBranchAsync(app, "main", 0, 0, head: false);
        AssertNoClippedText(app);
        Assert.Equal(["## HEAD (no branch)"], await StatusAsync(repo));
        Assert.Single(await repo.BackupRefsAsync());

        // Step 8: back to main without a confirmation: nothing would be left behind.
        Assert.Equal("0", await GitOutAsync(repo, "rev-list", "--count", "HEAD", "--not", "--branches", "--remotes", "--tags"));
        var mainAgain = await app.RefRowAsync("Local branches", "main");
        await RunOperationAsync(app, "Check out main", () => app.DoubleClick(mainAgain), Edges);
        await app.WaitForAsync(() => Graph(app).Data!.LabelsAt(1).FirstOrDefault()?.Kind == VisualCommit.App.ViewModels.Graph.RefLabelKind.CurrentBranch, "main's check mark");
        await WaitForDetailsAsync(app, "Main work");
        var back = Shot(app, "32g-back-without-confirmation");
        await AssertNoDialogAsync(app);
        AssertGraph(app, E1, back);
        AssertSelected(app, back, 1);
        AssertBranch(app, "main");
        AssertStatus(app, StatusCheckedOutMain);
        await AssertLocalBranchAsync(app, "main", 0, 0, head: true);
        AssertNoClippedText(app);
        Assert.Equal("refs/heads/main", await GitOutAsync(repo, "symbolic-ref", "HEAD"));
        Assert.Single(await repo.BackupRefsAsync());
    }

    /// <summary>Check 10 part C's files, written as a terminal would before the app starts.</summary>
    private static async Task WriteResetPartCFilesAsync(TempRepo repo)
    {
        repo.WriteFile("src/core.txt", CoreStaged);
        await repo.GitAsync("add", "src/core.txt");
        repo.WriteFile("src/core.txt", CoreUnstaged);
        repo.WriteFile("README.md", ReadmeLocalEdit);
        repo.WriteFile("scratch.txt", Scratch);
        await repo.GitAsync("add", "scratch.txt");
        repo.WriteFile("todo.txt", Todo);
    }

    /// <summary>
    /// The reset dialog of check 10 steps 3 and 8 (2.11, 4f): its title, text, three choices with
    /// their lines (Hard's in the danger colour), the backup's line, Cancel and Reset (the danger
    /// style while Hard is chosen, the primary one otherwise), on the 400-wide card over the backdrop.
    /// </summary>
    private static void AssertResetDialog(ShellDriver app, Screenshot picture, bool hard)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The reset dialog has no view.");
        Assert.Equal(ResetTitle, ((TextBlock)app.FindByAutomationId(view, "ResetTitle")).Text);
        Assert.Equal(ResetText, ((TextBlock)app.FindByAutomationId(view, "ResetText")).Text);
        Assert.Equal(["Soft", ResetSoftLine], TextsIn(app.FindByAutomationId(view, "ResetSoft")));
        Assert.Equal(["Mixed", ResetMixedLine], TextsIn(app.FindByAutomationId(view, "ResetMixed")));
        Assert.Equal(["Hard", ResetHardLine], TextsIn(app.FindByAutomationId(view, "ResetHard")));
        Assert.Equal(hard, ((RadioButton)app.FindByAutomationId(view, "ResetHard")).IsChecked == true);
        Assert.Equal(!hard, ((RadioButton)app.FindByAutomationId(view, "ResetMixed")).IsChecked == true);
        Assert.False(((RadioButton)app.FindByAutomationId(view, "ResetSoft")).IsChecked == true, "Soft is chosen.");
        var palette = PaletteOf(app);
        Assert.Equal(palette.Danger, (app.Find<TextBlock>("ResetHardText").Foreground as Avalonia.Media.ISolidColorBrush)?.Color);
        Assert.Equal(BackupDetail, ((TextBlock)app.FindByAutomationId(view, "ConfirmDetail")).Text);
        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.Equal("Cancel", cancel.Content);
        Assert.Equal("Reset", confirm.Content);
        Assert.Equal(hard, confirm.Classes.Contains("danger"));
        Assert.Equal(hard ? palette.Danger : palette.Accent, (confirm.Background as Avalonia.Media.ISolidColorBrush)?.Color);
        Assert.Equal(400, app.Find<Border>("ResetDialogCard").Bounds.Width, 0.5);
        AssertBackdrop(app, picture);
        AssertNoClippedText(app);
    }

    /// <summary>The stage panel of check 9 step 4 and check 9b step 3: the conflicted file flagged, nothing staged, the merge's message prefilled.</summary>
    private static void AssertConflictedStagePanel(ShellDriver app, Phase3Expectations.Palette palette)
    {
        Assert.Equal("Unstaged files (1, 1 with conflicts)", app.Find<TextBlock>("UnstagedTitle").Text);
        Assert.Equal(["U core.txt src"], StageRows(app, "UnstagedFileList"));
        var letter = app.Find<ItemsControl>("UnstagedFileList").GetVisualDescendants().OfType<TextBlock>().Single(text => text.IsEffectivelyVisible && text.Text == "U");
        Assert.Equal(palette.Danger, (letter.Foreground as Avalonia.Media.ISolidColorBrush)?.Color);
        Assert.Equal("Staged files (0)", app.Find<TextBlock>("StagedTitle").Text);
        Assert.True(app.Find<TextBlock>("NoStaged").IsEffectivelyVisible, "\"No staged changes\" does not show.");
        Assert.Equal("Merge branch 'feature/conflict'", app.Find<TextBox>("CommitSummary").Text);
        Assert.True(string.IsNullOrEmpty(app.Find<TextBox>("CommitDescription").Text), "The description is not empty.");
    }

    /// <summary>
    /// A dialog shows over the backdrop (phase 2's dialog layer): the empty graph area below the
    /// rows, at (270, 600), is the window's colour seen through the backdrop (±2 a channel).
    /// </summary>
    private static void AssertBackdrop(ShellDriver app, Screenshot picture)
    {
        var palette = PaletteOf(app);
        var alpha = palette.Backdrop.A / 255.0;
        int Mix(byte under, byte top) => (int)Math.Round((top * alpha) + (under * (1 - alpha)));
        var actual = picture.PixelAt(new Point(270, 600));
        var close = Math.Abs(actual.R - Mix(palette.Window.R, palette.Backdrop.R)) <= 2
            && Math.Abs(actual.G - Mix(palette.Window.G, palette.Backdrop.G)) <= 2
            && Math.Abs(actual.B - Mix(palette.Window.B, palette.Backdrop.B)) <= 2;
        Assert.True(close, $"The graph area under the dialog is {actual}, not the window's colour under the backdrop.");
    }

    /// <summary>
    /// The label feature/conflict's menu with HEAD on main, nothing in progress, with phase 4's
    /// rebase items: phase 4's report gives this list for check 27 step 3 ("check 13 step 6's list
    /// with main for hotfix"; 380 high).
    /// </summary>
    private static readonly string[] FeatureConflictMenu =
    [
        "feature/conflict",
        "Check out feature/conflict",
        "Merge feature/conflict into main",
        "Merge feature/conflict into main (create a merge commit)",
        "Rebase main onto feature/conflict",
        "Interactive rebase of main onto feature/conflict…",
        "—",
        "Push feature/conflict…",
        "—",
        "Create branch here…",
        "Create tag here…",
        "Rename feature/conflict…",
        "Delete feature/conflict… (danger)",
        "—",
        "Set upstream…",
        "—",
        "Copy branch name",
    ];

    /// <summary>The centre of the label named <paramref name="name"/> (its whole name, however it is drawn) of commit row <paramref name="row"/>, in the window.</summary>
    private static Point LabelPoint(ShellDriver app, int row, string name)
    {
        var graph = Graph(app);
        var label = graph.LabelsInRow(row).FirstOrDefault(candidate => !candidate.IsOverflow && candidate.FullText == name)
            ?? throw new InvalidOperationException($"Row {row} draws no label {name}; it draws: {string.Join(", ", graph.LabelsInRow(row).Select(candidate => candidate.Text))}.");
        return app.GraphLabelBounds(row, label.Text).Center;
    }

    /// <summary>
    /// The local branch feature/alpha's menu with HEAD on <paramref name="current"/>, nothing in
    /// progress (check 2 step 1's), with phase 4's rebase items: "Rebase {cur} onto feature/alpha"
    /// disabled with <c>{cur} is up to date with feature/alpha</c> when HEAD already reaches it
    /// (<paramref name="upToDate"/>: check 8 step 9, after the merge of origin/feature/alpha).
    /// </summary>
    private static string[] AlphaMenu(string current, bool upToDate = false) =>
    [
        "feature/alpha",
        "Check out feature/alpha",
        $"Merge feature/alpha into {current}",
        $"Merge feature/alpha into {current} (create a merge commit)",
        upToDate ? $"Rebase {current} onto feature/alpha (disabled: \"{current} is up to date with feature/alpha\")" : $"Rebase {current} onto feature/alpha",
        $"Interactive rebase of {current} onto feature/alpha…",
        "Fast-forward feature/alpha to origin/feature/alpha (disabled: \"feature/alpha is up to date with origin/feature/alpha\")",
        "—",
        "Push feature/alpha to origin/feature/alpha",
        "Force push feature/alpha… (danger)",
        "—",
        "Create branch here…",
        "Create tag here…",
        "Rename feature/alpha…",
        "Delete feature/alpha… (danger)",
        "—",
        "Change upstream…",
        "Stop tracking origin/feature/alpha",
        "—",
        "Copy branch name",
    ];
}
