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
        OpenMenu(app, LabelPoint(app, 5, "feature/alpha"), AlphaMenu("main"), "Check out feature/alpha");
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

        // Step 2: the merge stops; no dialog, the banner on two lines.
        OpenMenu(app, LabelPoint(app, 1, "feature/conflict"), FeatureConflictMenu, "Check out feature/conflict");
        await RunOperationAsync(app, "Merge feature/conflict", () => app.ClickMenuItem("Merge feature/conflict into main"));
        await app.WaitForAsync(() => Graph(app).ShowsWorkingRow && app.BannerBounds is not null, "the banner and the working-changes row");
        await WaitForDetailsAsync(app, "Conflicting change");
        var stopped = Shot(app, "09a-banner");
        await AssertNoDialogAsync(app);
        var banner = AssertBanner(app, MergeBanner, "Abort merge", oneLine: false);
        AssertBannerLook(app, banner, stopped);
        Assert.False(((TextBlock)app.FindByAutomationId("BannerText")).TextLayout.TextLines.Count < 2, "The merge banner's text is on one line at 1100×700.");
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

        // Step 3: the banner's tooltip under the pointer.
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

        // Step 7: a checkout git refuses.
        await RunOperationAsync(app, "Check out feature/alpha", () => app.DoubleClick(LabelPoint(app, 4, "feature/alpha")));
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, "09e-checkout-refused");
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            CheckoutFailedTitle, CheckoutFailedSummary, Ops, CheckoutCommand, ["Copy output", "Close"], CheckoutWhileMergingLines, KeyLine: CheckoutWhileMergingLines[0]));
        AssertStatus(app, StatusCheckoutAlphaFailed, OperationStatusTone.Danger);
        Assert.NotNull(GitDirFile(repo, "MERGE_HEAD"));
        Assert.Equal(["## main...origin/main", "UU src/core.txt"], await StatusAsync(repo));
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await AssertNoDialogAsync(app);

        // Step 8: the banner's Abort asks first.
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await AssertConfirmationAsync(app, AbortMergeTitle, AbortMergeQuestion, BackupDetail, "Abort merge");
        var confirmation = Shot(app, "09f-abort-confirmation");
        AssertBackdrop(app, confirmation);

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

        // Step 2: the banner of the terminal's merge.
        var start = Shot(app, "09b1-terminal-merge");
        var banner = AssertBanner(app, MergeBanner, "Abort merge", oneLine: false);
        AssertBannerLook(app, banner, start);
        AssertGraph(app, G0, start);
        AssertWorkingRow(app, WorkingChangesOneConflict, start);
        AssertSelected(app, start);
        AssertStatus(app, "Ready");
        AssertNoClippedText(app);

        // Step 3: the conflicted file's menu; the right-click selects no row and opens no diff (4b).
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (1, 1 with conflicts)", "Staged files (0)");
        var fileRow = await app.FileRowAsync("UnstagedFileList", "src/core.txt");
        OpenMenu(app, app.BoundsOf(fileRow).Center, ConflictedFileMenu, "Mark as resolved");
        var menu = Shot(app, "09b2-conflicted-file-menu");
        AssertConflictedStagePanel(app, palette);
        Assert.False(((VisualCommit.App.ViewModels.Panels.ChangedFileRow)fileRow.DataContext!).IsSelected, "The right-click selected the file's row.");
        Assert.False(app.Session.Shell.ActiveTab.Repository!.IsDiffOpen, "The right-click opened the diff.");
        _ = menu;

        // Step 4: "Mark as resolved" stages the file; the merge waits for its commit.
        app.ClickMenuItem("Mark as resolved");
        await WaitForStagePanelAsync(app, "Unstaged files (0)", "Staged files (1)");
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => ((TextBlock)app.FindByAutomationId("BannerText")).Text == MergeResolvedBanner, "the banner to say the conflicts are resolved");
        var resolved = Shot(app, "09b3-resolved");
        AssertBanner(app, MergeResolvedBanner, "Abort merge");
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

    /// <summary>The label feature/conflict's menu with HEAD on main, nothing in progress.</summary>
    private static readonly string[] FeatureConflictMenu =
    [
        "feature/conflict",
        "Check out feature/conflict",
        "Merge feature/conflict into main",
        "Merge feature/conflict into main (create a merge commit)",
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

    /// <summary>The local branch feature/alpha's menu with HEAD on <paramref name="current"/>, nothing in progress (check 2 step 1's).</summary>
    private static string[] AlphaMenu(string current) =>
    [
        "feature/alpha",
        "Check out feature/alpha",
        $"Merge feature/alpha into {current}",
        $"Merge feature/alpha into {current} (create a merge commit)",
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
