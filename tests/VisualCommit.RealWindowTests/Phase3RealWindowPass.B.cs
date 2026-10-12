// Part B's real-window checks (docs/test-reports/phase-3.md, part B, "How the checks of each part
// are run"): check 9 steps 2, 3 and 8; check 10 steps 8 and 9 (part C's files written before the
// launch); check 11 steps 5, 8, 10 and 11 (the real Ctrl and Shift held during real clicks). Each
// on a fresh copy of its scenario, in a launch (and a test method) of its own. While a banner is
// shown, rows are clicked below the ConflictBanner element (RealApp.GraphRowCentreY, D122), never
// at a fixed y; in check 9 step 3 the pointer rests on BannerText's centre until the banner's
// tooltip shows (RealApp.RestPointerOn). Only part B's writer edits this file. Shared helpers are
// in Phase3RealWindowPass.cs and RealApp.Checks.cs.
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.VisualTests.Phase3;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations.PartB;

namespace VisualCommit.RealWindowTests;

public partial class Phase3RealWindowPass
{
    /// <summary>
    /// Check 9, steps 2, 3 and 8, with the real mouse on a fresh ops copy at 1100×700: the merge
    /// stopped by its conflict and the banner over the graph (09a), the banner's tooltip resting
    /// under the real pointer and drawn inside the window on its overlay layer (09b, D105), and the
    /// Abort confirmation (09f). Steps 4 to 7 are the scripted run's alone, so the pictures of
    /// step 8 differ in the details panel ("Conflicting change" here, "Alpha two" there) and the
    /// status text under the backdrop.
    /// </summary>
    [Fact]
    public async Task Check_09_conflict_banner_its_tooltip_and_the_abort_confirmation()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = Launch(repo, DefaultSize.Width, DefaultSize.Height);

        // Step 2: right-click the label feature/conflict on row 1 and merge it.
        app.RightClickAt((float)FeatureConflictLabelCentre.X, (float)FeatureConflictLabelCentre.Y);
        app.WaitForMenu();
        Assert.Contains("Merge feature/conflict into main", app.MenuItemNames());
        var merge = ExpectOperation(app, Ops, MergeConflictTitle);
        app.ClickMenuItem("Merge feature/conflict into main");
        merge.Wait(OperationOutcome.Conflicts);
        app.WaitFor(() => app.BannerBounds() is not null, "the conflict banner", OperationTimeout);
        WaitForDetails(app, "Conflicting change");
        Assert.Equal(MergeBanner, app.Find("BannerText").Name);
        Assert.Equal("Abort merge", app.Find("BannerAbortButton").Name);

        // Phase 4's Continue, disabled while the file conflicts, and no Skip for a merge; at
        // 1100×700 the buttons stand under the text, so the banner takes more than one line
        // (phase 4's report, "Phase 3's banner").
        var resume = app.Find("BannerContinueButton");
        Assert.Equal("Continue", resume.Name);
        Assert.False(resume.IsEnabled, "The banner's Continue is enabled while a file conflicts.");
        Assert.Empty(app.FindAll("BannerSkipButton"));
        var banner = app.BannerBounds()!.Value;
        Assert.True(banner.Height > WindowLayout.BannerMinHeight + 0.5, $"The banner is {banner.Height} high: one line at 1100×700 (D122, phase 4's design 5.2).");
        var text = app.BoundsOf(app.Find("BannerText"));
        Assert.True(app.BoundsOf(resume).Top >= text.Bottom - 0.5, $"The banner's buttons at {app.BoundsOf(resume)} are not under its text at {text}.");
        Assert.Empty(app.FindAll("ErrorDialog"));
        Assert.Empty(app.FindAll("ConfirmationDialog"));
        Assert.Equal(StatusMergeStopped, app.OperationStatusText());
        Compare(app, "09a-banner");

        // Step 3: the real pointer rests on the banner's text until its tooltip shows; the
        // tooltip is part of the window (D105) and lies inside it.
        var tip = app.RestPointerOn(app.Find("BannerText"), BannerToolTip);
        var tipBounds = app.BoundsOf(tip);
        var client = app.ClientSize;
        Assert.True(
            tipBounds.Left >= 0 && tipBounds.Top >= 0 && tipBounds.Right <= client.Width && tipBounds.Bottom <= client.Height,
            $"The banner's tooltip at {tipBounds} is not inside the {client.Width}x{client.Height} client area.");
        Compare(app, "09b-banner-tooltip", keepPointer: true);

        // Step 8: the banner's Abort asks first. The pointer leaves the banner first, as in the
        // scripted run (the tooltip lies over the rows below it).
        app.MoveToNeutral();
        app.ClickWithMouse(app.Find("BannerAbortButton"));
        app.WaitFor(() => app.FindAll("ConfirmTitle").Any(title => title.Name == AbortMergeTitle), "the Abort confirmation", RealApp.DefaultWaitTimeout);
        Assert.Equal(AbortMergeQuestion, app.Find("ConfirmQuestion").Name);
        Assert.Equal(BackupDetail, app.Find("ConfirmDetail").Name);
        Assert.Equal("Abort merge", app.Find("ConfirmButton").Name);
        Assert.True(app.Find("CancelButton").Properties.HasKeyboardFocus.ValueOrDefault, "Cancel does not have the keyboard focus.");
        Compare(app, "09f-abort-confirmation");

        // Leave git as the step found it: Cancel keeps the merge (step 9's state).
        app.ClickWithMouse(app.Find("CancelButton"));
        app.WaitFor(() => app.FindAll("ConfirmationDialog").Count == 0, "the confirmation to close", RealApp.DefaultWaitTimeout);
        Assert.Equal(0, app.Close());
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")), "The merge is no longer in progress.");
        Assert.Empty(await repo.BackupRefsAsync());
    }

    /// <summary>
    /// Check 10, steps 8 and 9, with the real mouse on a fresh ops copy whose part C files are
    /// written before the launch, at 1100×700: the reset dialog with Hard chosen (10f) and the
    /// hard reset done (10g), with git's state and the backups read back afterwards.
    /// </summary>
    [Fact]
    public async Task Check_10_hard_reset_dialog_and_result()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", CoreStaged);
        await repo.GitAsync("add", "src/core.txt");
        repo.WriteFile("src/core.txt", CoreUnstaged);
        repo.WriteFile("README.md", ReadmeLocalEdit);
        repo.WriteFile("scratch.txt", Scratch);
        await repo.GitAsync("add", "scratch.txt");
        repo.WriteFile("todo.txt", Todo);
        using var app = Launch(repo, DefaultSize.Width, DefaultSize.Height);

        // Step 8: right-click "Add core" (display row 7, below the working-changes row), open the
        // reset dialog and choose Hard.
        var addCore = RowPoint(app, 7, G0.Lanes);
        app.RightClickAt(addCore.X, addCore.Y);
        app.ClickMenuItem("Reset main to this commit…");
        app.WaitFor(() => app.FindAll("ResetDialog").Count == 1, "the reset dialog", RealApp.DefaultWaitTimeout);
        app.ClickWithMouse(app.Find("ResetHard"));
        WaitForDetails(app, "Add core");
        Assert.Equal(ResetTitle, app.Find("ResetTitle").Name);
        Assert.Equal(ResetText, app.Find("ResetText").Name);
        Assert.Equal(BackupDetail, app.Find("ConfirmDetail").Name);
        Assert.Equal("Reset", app.Find("ConfirmButton").Name);
        Compare(app, "10f-hard-dialog");

        // Step 9: Reset.
        var reset = ExpectOperation(app, Ops, ResetOperation);
        app.ClickWithMouse(app.Find("ConfirmButton"));
        reset.Wait(OperationOutcome.Succeeded);
        app.WaitFor(() => app.FindAll("ResetDialog").Count == 0, "the reset dialog to close", RealApp.DefaultWaitTimeout);
        Assert.Equal(StatusResetHard, app.OperationStatusText());
        Compare(app, "10g-hard-done");

        Assert.Equal(0, app.Close());
        Assert.Equal("## main...origin/main [behind 1]\n?? todo.txt", (await repo.GitAsync("status", "--short", "--branch")).StandardOutput.TrimEnd('\n'));
        Assert.Equal(Scenarios.OperationsFiles.Core, File.ReadAllText(Path.Combine(repo.Path, "src", "core.txt")));
        Assert.False(File.Exists(Path.Combine(repo.Path, "scratch.txt")), "scratch.txt is still there.");
        var backups = (await repo.BackupRefsAsync()).Select(entry => (Ref: BackupRef.Parse(entry.Name), entry.Sha)).ToList();
        Assert.Equal(2, backups.Count);
        Assert.Equal(Sha.MainWorkFull, Assert.Single(backups, backup => !backup.Ref.IsChanges).Sha);
        Assert.All(backups, backup => Assert.Equal("reset", backup.Ref.Kind));
    }

    /// <summary>
    /// Check 11, steps 5, 8, 10 and 11, at 1400×900 on a fresh ops copy, with the real Ctrl and
    /// Shift keys held during real clicks: two commits selected with Ctrl (11d), a Shift
    /// selection, the several-commits menu (11g) and the pick of two (11h). Steps 6, 7 and 9 are
    /// the scripted run's alone; step 8 ends in the selection they leave, so 11g and 11h show the
    /// scripted run's state.
    /// </summary>
    [Fact]
    public async Task Check_11_ctrl_and_shift_selection_and_the_pick_of_two()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = Launch(repo, LargeSize.Width, LargeSize.Height);
        var alphaTwo = RowPoint(app, 4, G0.Lanes);
        var alphaOne = RowPoint(app, 5, G0.Lanes);

        // Step 5: click "Alpha one", Ctrl+click "Alpha two".
        app.ClickAt(alphaOne.X, alphaOne.Y);
        app.ClickAt(alphaTwo.X, alphaTwo.Y, VirtualKeyShort.CONTROL);
        WaitForMultiSelection(app, "2 commits selected");
        Compare(app, "11d-two-selected");

        // Step 8: click "Alpha two", then Shift+click "Alpha one".
        app.ClickAt(alphaTwo.X, alphaTwo.Y);
        WaitForDetails(app, "Alpha two");
        app.ClickAt(alphaOne.X, alphaOne.Y, VirtualKeyShort.SHIFT);
        WaitForMultiSelection(app, "2 commits selected");

        // Step 10: a right-click on a selected row keeps the selection and opens the
        // several-commits menu, with phase 4's squash (disabled: neither commit is on main); the
        // pointer stays where it opened it.
        app.RightClickAt(alphaOne.X, alphaOne.Y);
        Assert.Equal(ItemLabels(TwoCommitsMenu), app.MenuItemNames());
        Assert.False(app.FindAll("ContextMenuItem").Single(item => item.Name == "Squash 2 commits…").IsEnabled, "\"Squash 2 commits…\" is enabled.");
        Assert.Equal("2 commits selected", app.Find("MultiSelectionTitle").Name);
        Compare(app, "11g-multi-menu", keepPointer: true);

        // Step 11: the pick of two.
        var pick = ExpectOperation(app, Ops, CherryPickTwoOperation);
        app.ClickMenuItem("Cherry-pick 2 commits onto main");
        pick.Wait(OperationOutcome.Succeeded);
        WaitForMultiSelection(app, "2 commits selected");
        Assert.Equal(StatusPickedTwo, app.OperationStatusText());
        Compare(app, "11h-two-picked");

        Assert.Equal(0, app.Close());
        Assert.Equal(
            "c0f718d Alpha two\nd1f9fb5 Alpha one\n88d6b91 Main work",
            (await repo.GitAsync("log", "--format=%h %s", "-3", "main")).StandardOutput.TrimEnd('\n'));
        Assert.Equal(Sha.PickedAlphaTwo, await repo.HeadAsync());
    }

    /// <summary>Waits until the right panel shows the multi-selection view with this title.</summary>
    private static void WaitForMultiSelection(RealApp app, string title) =>
        app.WaitFor(() => app.FindAll("MultiSelectionTitle").Any(text => text.Name == title), $"the multi-selection view's \"{title}\"", RealApp.DefaultWaitTimeout);
}
