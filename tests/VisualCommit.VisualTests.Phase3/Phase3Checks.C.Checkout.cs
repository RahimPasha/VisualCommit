// Part C, checks 14 and 14b (docs/test-reports/phase-3.md): a checkout blocked by local changes,
// "Stash changes and check out" that re-applies cleanly and with conflicts, untracked files in the
// way, the conflicts banner without an operation, and a checkout git refuses for another reason.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksC
{
    private const string BlockedRepo = "blocked";

    /// <summary>
    /// Check 14: the blocked checkout scenario as it opens; checking out feature/addfile (untracked
    /// files in the way: no "Stash changes and check out"); feature/other, blocked, then stashed,
    /// checked out and re-applied; feature/clash, whose re-apply stops with conflicts; and the
    /// conflicts banner, the stage panel and the graph that leaves.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_14_checkout_blocked()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: the scenario as it opens.
        var shot = Shot(app, "14a-blocked-scenario");
        Assert.Equal(BlockedRepo, app.Session.Shell.ActiveTab.Title);
        var graph = Graph(app);
        Assert.Equal(PartC.G14aSubjects, Enumerable.Range(0, graph.Data!.Count).Select(row => graph.Data.CommitAt(row).Subject));
        AssertGraph(app, shot, PartC.G14a, lanes: 2, PartC.Working);
        AssertSelection(app, shot, null);
        AssertBranch(app, "main");
        AssertStatus(app, "Ready");
        Assert.Equal(4, SectionRow(app, "Local branches").Count);
        Assert.Equal(["feature", "main"], SectionLines(app, "Local branches"));
        await AssertBranchRowAsync(app, ["Local branches", "main"], head: true);
        Assert.Equal(0, SectionRow(app, "Stashes").Count);
        AssertNoClippedText(app);

        // Step 2: open the folder, double-click addfile: untracked files are in the way.
        await OpenLocalFeatureFolderAsync(app);
        var mark = app.LogMark();
        app.DoubleClick(await app.RefRowAsync("Local branches", "feature", "addfile"));
        await WaitForOperationAsync(app, repo, "Check out feature/addfile", mark);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        shot = Shot(app, "14b-untracked-in-the-way");
        Assert.Equal(["feature", "  addfile", "  clash", "  other", "main"], SectionLines(app, "Local branches"));
        Assert.True(ItemRow(app, "Local branches", "feature", "addfile").IsSelected);
        AssertSelection(app, null, 0);
        Assert.Equal("Add newfile", RightPanelText(app, "DetailsSubject"));
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Checkout blocked",
            PartC.UntrackedSummary,
            BlockedRepo,
            "git switch --no-guess feature/addfile",
            ["Copy output", "Close"],
            PartC.T1,
            PartC.T1Key));
        AssertErrorDialogParts(app, extra: null);
        AssertStatus(app, "Check out feature/addfile failed", OperationStatusTone.Danger);
        await AssertBlockedUnchangedAsync(repo);

        // Step 3: Close.
        ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
        await AssertBlockedUnchangedAsync(repo);

        // Step 4: double-click other: blocked by the local change.
        mark = app.LogMark();
        app.DoubleClick(await app.RefRowAsync("Local branches", "feature", "other"));
        await WaitForOperationAsync(app, repo, "Check out feature/other", mark);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, "14c-checkout-blocked");
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Checkout blocked",
            PartC.BlockedSummary("feature/other"),
            BlockedRepo,
            "git switch --no-guess feature/other",
            ["Copy output", "Stash changes and check out", "Close"],
            PartC.T2,
            PartC.T2Key));
        AssertErrorDialogParts(app, extra: "Stash changes and check out");
        AssertStatus(app, "Check out feature/other failed", OperationStatusTone.Danger);
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.True(ItemRow(app, "Local branches", "feature", "other").IsSelected);
        AssertSelection(app, null, 2);

        // Step 5: "Stash changes and check out".
        var calls = app.GitCalls().Count;
        mark = app.LogMark();
        ClickDialogButton(app, "ErrorExtraButton");
        await WaitForOperationAsync(app, repo, "Check out feature/other with your changes", mark);
        await AssertNoDialogAsync(app);
        shot = Shot(app, "14d-stashed-and-checked-out");
        var push = AssertCalled(app, calls, "git stash push --message \"VisualCommit: before checking out feature/other\"");
        Assert.Contains("Saved working directory and index state On main: VisualCommit: before checking out feature/other", push.StandardOutput, StringComparison.Ordinal);
        var pop = AssertCalled(app, calls, "git stash pop --index stash@{0}");
        Assert.Equal(0, pop.ExitCode);
        Assert.Matches(@"Dropped stash@\{0\} \([0-9a-f]{40}\)\s*$", pop.StandardOutput);
        AssertCalledInOrder(app, calls, push.CommandText, "git switch --no-guess feature/other", pop.CommandText);
        AssertBranch(app, "feature/other");
        AssertStatus(app, "Checked out feature/other and re-applied your changes");
        Assert.Empty(await StashListAsync(repo));
        Assert.Equal([" M notes.txt", "?? newfile.txt"], await StatusAsync(repo));
        Assert.Equal(["one (local)", "two", "three", "four", "five", "six", "seven (other)"], FileLines(repo, "notes.txt"));
        AssertGraph(app, shot, PartC.Check14OnOther, lanes: 2, PartC.Working);
        Assert.Equal(["feature", "  addfile", "  clash", "  other", "main"], SectionLines(app, "Local branches"));
        await AssertBranchRowAsync(app, ["Local branches", "feature", "other"], head: true);
        await AssertBranchRowAsync(app, ["Local branches", "main"], head: false);

        // Step 6: the stage panel.
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (2)", "Staged files (0)");
        Shot(app, "14e-stage-panel");
        Assert.Equal(["A newfile.txt", "M notes.txt"], FileRows(app, "UnstagedFileList"));
        Assert.Empty(FileRows(app, "StagedFileList"));
        Assert.True(app.Find<TextBlock>("NoStaged").IsEffectivelyVisible);
        Assert.Equal("No staged changes", app.Find<TextBlock>("NoStaged").Text);

        // Step 7: double-click clash, then "Stash changes and check out": the re-apply conflicts.
        mark = app.LogMark();
        app.DoubleClick(await app.RefRowAsync("Local branches", "feature", "clash"));
        await WaitForOperationAsync(app, repo, "Check out feature/clash", mark);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, "14f-clash-blocked");
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Checkout blocked",
            PartC.BlockedSummary("feature/clash"),
            BlockedRepo,
            "git switch --no-guess feature/clash",
            ["Copy output", "Stash changes and check out", "Close"],
            PartC.T2,
            PartC.T2Key));
        AssertStatus(app, "Check out feature/clash failed", OperationStatusTone.Danger);
        Assert.True(ItemRow(app, "Local branches", "feature", "clash").IsSelected);
        AssertSelection(app, null, 1);

        calls = app.GitCalls().Count;
        mark = app.LogMark();
        ClickDialogButton(app, "ErrorExtraButton");
        await WaitForOperationAsync(app, repo, "Check out feature/clash with your changes", mark);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, "14g-reapplied-with-conflicts");
        AssertCalledInOrder(
            app,
            calls,
            "git stash push --message \"VisualCommit: before checking out feature/clash\"",
            "git switch --no-guess feature/clash",
            "git stash pop --index stash@{0}");
        Assert.True(AssertCalled(app, calls, "git switch --no-guess feature/clash").Succeeded);
        var conflictPop = AssertCalled(app, calls, "git stash pop --index stash@{0}");
        Assert.Equal(1, conflictPop.ExitCode);
        Assert.Contains("CONFLICT (content): Merge conflict in notes.txt", conflictPop.StandardOutput, StringComparison.Ordinal);
        Assert.Single(CallsSince(app, calls), call => call.StartsWith("git stash pop", StringComparison.Ordinal));
        if (await IsReportGitAsync())
        {
            Assert.Equal("Index was not unstashed.", conflictPop.StandardError.Trim());
            Assert.StartsWith("Auto-merging notes.txt\nCONFLICT (content): Merge conflict in notes.txt\nOn branch feature/clash", conflictPop.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.EndsWith("The stash entry is kept in case you need it again.", conflictPop.StandardOutput.TrimEnd(), StringComparison.Ordinal);
        }

        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Changes re-applied with conflicts",
            PartC.Check14ConflictsSummary,
            BlockedRepo,
            null,
            ["Close"],
            []));
        AssertErrorDialogParts(app, extra: null, copy: false);
        AssertBranch(app, "feature/clash");
        AssertStatus(app, PartC.Check14ConflictsStatus, OperationStatusTone.Warning);
        Assert.Equal(["stash@{0}: " + PartC.Check14StashMessage], await StashListAsync(repo));
        Assert.Equal(["UU notes.txt", "?? newfile.txt"], await StatusAsync(repo));
        Assert.Equal(
            ["<<<<<<< Updated upstream", "one (clash)", "=======", "one (local)", ">>>>>>> Stashed changes", "two", "three", "four", "five", "six", "seven"],
            FileLines(repo, "notes.txt"));
        AssertNoOperationFiles(repo);

        // Step 8: Close, then the working-changes row below the banner.
        ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (2, 1 with conflicts)", "Staged files (0)");
        shot = Shot(app, "14h-stash-conflict-banner");
        AssertConflictsBanner(app, shot, PartC.ConflictsBanner, PartC.ConflictsBannerTip);
        AssertToolbar(app, "StashButton", enabled: false, PartC.ResolveFirst);
        AssertGraph(app, shot, PartC.Check14Conflicts, lanes: 3, PartC.Check14ConflictsWorking);
        AssertSelection(app, shot, null, workingRow: true);
        AssertWorkingRowCutWithToolTip(app);
        Assert.Equal(1, SectionRow(app, "Stashes").Count);
        Assert.Equal([PartC.Check14StashMessage], SectionLines(app, "Stashes"));
        await AssertBranchRowAsync(app, ["Local branches", "feature", "clash"], head: true);
        Assert.Equal(["A newfile.txt", "U notes.txt"], FileRows(app, "UnstagedFileList"));
        await AssertConflictRowAsync(app, "notes.txt");
        Assert.Empty(FileRows(app, "StagedFileList"));
        Assert.Equal(string.Empty, CommitBoxText(app, "CommitSummary"));
        Assert.Equal(string.Empty, CommitBoxText(app, "CommitDescription"));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check 14b, continuing from check 14 (its steps repeated without their pictures): a checkout
    /// of main while notes.txt is conflicted and no operation is in progress: "Checkout failed".
    /// </summary>
    [AvaloniaFact]
    public async Task Check_14b_checkout_failed()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: check 14's steps 1 to 8.
        await OpenLocalFeatureFolderAsync(app);
        await DoubleClickBranchAsync(app, repo, "addfile", "Check out feature/addfile");
        ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
        await DoubleClickBranchAsync(app, repo, "other", "Check out feature/other");
        var mark = app.LogMark();
        ClickDialogButton(app, "ErrorExtraButton");
        await WaitForOperationAsync(app, repo, "Check out feature/other with your changes", mark);
        await AssertNoDialogAsync(app);
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (2)", "Staged files (0)");
        await DoubleClickBranchAsync(app, repo, "clash", "Check out feature/clash");
        mark = app.LogMark();
        ClickDialogButton(app, "ErrorExtraButton");
        await WaitForOperationAsync(app, repo, "Check out feature/clash with your changes", mark);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.Equal("Changes re-applied with conflicts", app.ErrorDialog().Title);
        ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
        app.Click(app.WorkingRowPoint());
        await WaitForStagePanelAsync(app, "Unstaged files (2, 1 with conflicts)", "Staged files (0)");
        Assert.NotNull(app.BannerBounds);

        // Step 2: double-click main.
        mark = app.LogMark();
        app.DoubleClick(await app.RefRowAsync("Local branches", "main"));
        await WaitForOperationAsync(app, repo, "Check out main", mark);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Shot(app, "14b1-checkout-failed");
        Assert.True(ItemRow(app, "Local branches", "main").IsSelected);
        AssertSelection(app, null, 4);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Checkout failed",
            "Git could not check out main.",
            BlockedRepo,
            "git switch --no-guess main",
            ["Copy output", "Close"],
            PartC.T4,
            PartC.T4Key));
        AssertErrorDialogParts(app, extra: null);
        Assert.Equal(1, app.LastCall("git switch --no-guess main").ExitCode);
        AssertStatus(app, "Check out main failed", OperationStatusTone.Danger);

        // Step 3: Close: nothing changed.
        ClickDialogButton(app, "ConfirmButton");
        await AssertNoDialogAsync(app);
        Assert.Equal("feature/clash", await repo.CurrentBranchAsync());
        Assert.Equal(["UU notes.txt", "?? newfile.txt"], await StatusAsync(repo));
        Assert.Equal(["stash@{0}: " + PartC.Check14StashMessage], await StashListAsync(repo));
        AssertBanner(app, PartC.ConflictsBanner, null);
    }

    /// <summary>Clicks the folder Local branches › feature open, as check 14's step 2 starts.</summary>
    private static async Task OpenLocalFeatureFolderAsync(ShellDriver app)
    {
        app.Click(await app.RefRowAsync("Local branches", "feature"));
        await app.WaitForAsync(() => ItemRow(app, "Local branches", "feature").IsOpen, "the folder Local branches › feature to open");
    }

    /// <summary>Double-clicks Local branches › feature › <paramref name="name"/> and waits for the refused checkout's dialog.</summary>
    private static async Task DoubleClickBranchAsync(ShellDriver app, TempRepo repo, string name, string title)
    {
        var mark = app.LogMark();
        app.DoubleClick(await app.RefRowAsync("Local branches", "feature", name));
        await WaitForOperationAsync(app, repo, title, mark);
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
    }

    /// <summary>
    /// The error dialog's parts beyond its texts: git's output in the code font when shown, "Copy
    /// output" (<c>ErrorCopyButton</c>) when <paramref name="copy"/>, the extra action
    /// (<c>ErrorExtraButton</c>) with its label or hidden, and "Close" (<c>ConfirmButton</c>).
    /// </summary>
    private static void AssertErrorDialogParts(ShellDriver app, string? extra, bool copy = true)
    {
        var view = app.TopDialogView!;
        var output = (TextBox)app.FindByAutomationId(view, "ErrorOutput");
        if (output.IsEffectivelyVisible)
        {
            Assert.Equal(app.Window.FindResource("VcCodeFontFamily"), output.FontFamily);
        }

        Assert.Equal(copy, app.FindByAutomationId(view, "ErrorCopyButton").IsEffectivelyVisible);
        var extraButton = (Button)app.FindByAutomationId(view, "ErrorExtraButton");
        Assert.Equal(extra is not null, extraButton.IsEffectivelyVisible);
        if (extra is not null)
        {
            Assert.Equal(extra, extraButton.Content);
        }

        Assert.Equal("Close", ((Button)app.FindByAutomationId(view, "ConfirmButton")).Content);
    }

    /// <summary>
    /// The working-changes row's "Working changes · 1 conflict" does not fit a three-lane graph's
    /// 126-wide Message column at 1100×700: it is cut with "…", and the graph's tooltip over it
    /// gives the whole text (phase 2's rule for cut text). The pointer goes back to the neutral point.
    /// </summary>
    private static void AssertWorkingRowCutWithToolTip(ShellDriver app)
    {
        var graph = Graph(app);
        app.MoveMouse(app.WorkingRowPoint());
        Assert.Equal(PartC.WorkingChangesOneConflict, ToolTip.GetTip(graph) as string);
        app.MoveToNeutral();
    }

    /// <summary>Nothing changed in the blocked scenario: HEAD on main, notes.txt changed, newfile.txt untracked, no stash.</summary>
    private static async Task AssertBlockedUnchangedAsync(TempRepo repo)
    {
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal([" M notes.txt", "?? newfile.txt"], await StatusAsync(repo));
        Assert.Empty(await StashListAsync(repo));
    }

    /// <summary>
    /// The in-progress banner (section 5, D122) as part C's steps state it: its text, its buttons
    /// (phase 4's Continue and Skip as design 5.1 shows them, null: not shown; Abort, null: none),
    /// whether the buttons stand on a row of their own under the text (phase 4's design 5.2: at
    /// 1100×700 they do, so the banner takes more than one line), its tooltip, above the column
    /// headers, at least 36 high, its background and its bottom line in the warning colour, and
    /// the warning icon, by pixel.
    /// </summary>
    private static Rect AssertConflictsBanner(
        ShellDriver app,
        Screenshot screenshot,
        string text,
        string? toolTip,
        string? abort = null,
        BannerButton? continueButton = null,
        BannerButton? skipButton = null,
        bool? buttonsBelow = null)
    {
        var banner = AssertBanner(app, text, abort, buttonsBelow == true ? false : null, continueButton, skipButton, buttonsBelow);
        if (toolTip is not null)
        {
            // Asserted where the report states it; null: the step names none.
            Assert.Equal(toolTip, BannerToolTip(app));
        }
        Assert.Equal(banner.Bottom + WindowLayout.ColumnHeaderHeight, app.GraphRowsTop, 1.0);
        var palette = PaletteOf(app);
        var right = abort is null ? banner.Right - 4 : banner.Right - 3;
        AssertColour(palette.BannerBackground, screenshot, new Point(right, banner.Top + 2), "the banner's background");
        AssertColour(palette.Warning, screenshot, new Point(right, banner.Bottom - 0.5), "the banner's bottom line");
        var icon = app.Find<Control>("BannerIcon");
        var iconBounds = app.BoundsOf(icon);
        var found = false;
        for (var x = (int)iconBounds.Left; x < (int)iconBounds.Right && !found; x++)
        {
            for (var y = (int)iconBounds.Top; y < (int)iconBounds.Bottom && !found; y++)
            {
                found = screenshot.PixelAt(x, y) == palette.Warning;
            }
        }

        Assert.True(found, "The banner's icon has no pixel in the warning colour.");
        return banner;
    }
}
