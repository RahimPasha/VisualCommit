// Part C's real-window checks (docs/test-reports/phase-3.md, part C, "Real window for part C"):
// check 13 steps 1 to 3, Esc, then check 16 steps 1 to 3 and Esc, on ONE launch on a fresh ops copy
// (one test method); check 14 steps 4 to 8 on CheckoutBlockedAsync (left-panel rows double-clicked
// with the real mouse, found by RealApp.RefRow; the working-changes row below the banner clicked at
// RealApp.GraphRowCentreY(0), D122); check 15 steps 1 to 3 (its changes written before the launch).
// "Select all and type" is RealApp.ReplaceText (Ctrl+A on the real keyboard). Only part C's writer
// edits this file. Shared helpers are in Phase3RealWindowPass.cs and RealApp.Checks.cs.
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase3RealWindowPass
{
    /// <summary>How long a dialog, a message or a row of part C's checks may take to show.</summary>
    private static readonly TimeSpan PartCWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Checks 13 and 16 on one launch (design 7.7): the name dialog with a validation message
    /// (13 steps 1 to 3, compared as <c>13c-folder-clash</c>), Esc; then the tag dialog filled
    /// with the real keyboard, "#" included (16 steps 1 to 3, compared as
    /// <c>16c-tag-dialog-filled</c>), Esc. Neither creates anything.
    /// </summary>
    [Fact]
    public async Task Checks_13_and_16_on_one_launch()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = Launch(repo);

        // Check 13, steps 1 to 3 (the commit menu with phase 4's rebase items: phase 4's OC3).
        var mainWork = RowPoint(app, 3, lanes: 3);
        app.RightClickAt(mainWork.X, mainWork.Y);
        Assert.Equal(ItemLabels(PartC.Check13CommitMenu), app.MenuItemNames());
        app.ClickMenuItem("Create branch here…");
        WaitForName(app, "NameDialogTitle", "Create branch");
        app.ClickWithMouse(app.Find("NameBox"));
        app.TypeText("feature");
        WaitForName(app, "NameError", PartC.FolderClash);
        Assert.False(app.Find("ConfirmButton").IsEnabled);
        Compare(app, "13c-folder-clash");
        app.PressKey(VirtualKeyShort.ESCAPE);
        WaitForNone(app, "NameDialog");

        // Check 16, steps 1 to 3.
        var alphaTwo = RowPoint(app, 4, lanes: 3);
        app.RightClickAt(alphaTwo.X, alphaTwo.Y);
        app.WaitForMenu();
        app.ClickMenuItem("Create tag here…");
        WaitForName(app, "NameDialogTitle", "Create tag");
        app.ClickWithMouse(app.Find("NameBox"));
        app.TypeText("v1.0");
        WaitForName(app, "NameError", "A tag named v1.0 exists.");
        app.ReplaceText("NameBox", "v1 2");
        WaitForName(app, "NameError", "This is not a valid tag name: it contains a space.");
        app.ReplaceText("NameBox", "v1.2");
        WaitForNone(app, "NameError");
        app.ClickWithMouse(app.Find("MessageBox"));
        app.TypeText(PartC.TagMessage);
        app.WaitFor(() => app.Find("ConfirmButton").IsEnabled, "\"Create tag\" to be enabled", PartCWait);
        Compare(app, "16c-tag-dialog-filled");
        app.PressKey(VirtualKeyShort.ESCAPE);
        WaitForNone(app, "NameDialog");

        Assert.Equal(0, app.Close());
        Assert.Equal(
            ["feature/alpha", "feature/conflict", "feature/ff", "* main"],
            (await repo.GitAsync("branch")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()));
        Assert.Equal(["v1.0", "v1.1"], (await repo.GitAsync("tag", "--list")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Check 14, steps 4 to 8, with the real mouse on the blocked checkout scenario: the folder
    /// opened by a real click, <c>other</c> double-clicked (the dialog with "Stash changes and check
    /// out", compared as <c>14c-checkout-blocked</c>), stashed and checked out, the stage panel,
    /// <c>clash</c> double-clicked and re-applied with conflicts (compared as
    /// <c>14g-reapplied-with-conflicts</c>), then the working-changes row clicked below the banner.
    /// </summary>
    [Fact]
    public async Task Check_14_checkout_blocked_with_the_real_mouse()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();
        using var app = Launch(repo);
        const string Repo = "blocked";

        // The folder Local branches › feature, opened with a real click (step 2's first part).
        app.ClickWithMouse(app.RefRow("Local branches", "feature"));
        app.WaitFor(() => TryRefRow(app, "Local branches", "feature", "other"), "the folder feature to open", PartCWait);

        // Step 4: double-click other.
        var checkout = ExpectOperation(app, Repo, "Check out feature/other");
        app.DoubleClickWithMouse(app.RefRow("Local branches", "feature", "other"));
        checkout.Wait(OperationOutcome.Failed);
        WaitForName(app, "ErrorTitle", "Checkout blocked");
        WaitForName(app, "ErrorExtraButton", "Stash changes and check out");
        Compare(app, "14c-checkout-blocked");

        // Step 5: "Stash changes and check out".
        var withChanges = ExpectOperation(app, Repo, "Check out feature/other with your changes");
        app.ClickWithMouse(app.Find("ErrorExtraButton"));
        withChanges.Wait(OperationOutcome.Succeeded);
        WaitForNone(app, "ErrorDialog");
        Assert.Equal("feature/other", await repo.CurrentBranchAsync());

        // Step 6: the working-changes row (two lanes, no banner).
        var working = RowPoint(app, 0, lanes: 2);
        app.ClickAt(working.X, working.Y);
        WaitForName(app, "UnstagedTitle", "Unstaged files (2)");

        // Step 7: double-click clash, then "Stash changes and check out".
        checkout = ExpectOperation(app, Repo, "Check out feature/clash");
        app.DoubleClickWithMouse(app.RefRow("Local branches", "feature", "clash"));
        checkout.Wait(OperationOutcome.Failed);
        WaitForName(app, "ErrorTitle", "Checkout blocked");
        withChanges = ExpectOperation(app, Repo, "Check out feature/clash with your changes");
        app.ClickWithMouse(app.Find("ErrorExtraButton"));
        withChanges.Wait(OperationOutcome.Conflicts);
        WaitForName(app, "ErrorTitle", "Changes re-applied with conflicts");
        Assert.Empty(app.FindAll("ErrorExtraButton"));
        Compare(app, "14g-reapplied-with-conflicts");

        // Step 8: Close, then the working-changes row below the banner (three lanes now).
        app.ClickWithMouse(app.Find("ConfirmButton"));
        WaitForNone(app, "ErrorDialog");
        app.WaitFor(() => app.BannerBounds() is not null, "the conflicts banner", PartCWait);
        working = RowPoint(app, 0, lanes: 3);
        Assert.True(working.Y > app.BannerBounds()!.Value.Bottom + (float)WindowLayout.ColumnHeaderHeight, "The working-changes row is not below the banner and the column headers.");
        app.ClickAt(working.X, working.Y);
        WaitForName(app, "UnstagedTitle", "Unstaged files (2, 1 with conflicts)");
        WaitForName(app, "BannerText", PartC.ConflictsBanner);

        Assert.Equal(0, app.Close());
        Assert.Equal("feature/clash", await repo.CurrentBranchAsync());
        Assert.Equal(["UU notes.txt", "?? newfile.txt"], (await repo.GitAsync("status", "--porcelain")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(
            ["stash@{0}: " + PartC.Check14StashMessage],
            (await repo.GitAsync("stash", "list")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Check 15, steps 1 to 3: the changes written before the launch, a real click on Stash (the
    /// dialog compared as <c>15b-stash-dialog</c>), the message typed with the real keyboard and a
    /// real click on "Stash".
    /// </summary>
    [Fact]
    public async Task Check_15_stash_dialog_with_the_real_keyboard()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmore work\n").WriteFile("todo.txt", "Later\n");
        using var app = Launch(repo);

        // Step 2: the stash dialog.
        app.WaitFor(() => app.Find("StashButton").IsEnabled, "Stash to be enabled", PartCWait);
        app.ClickWithMouse(app.Find("StashButton"));
        WaitForName(app, "NameDialogTitle", "Stash changes");
        Assert.True(app.Find("ConfirmButton").IsEnabled);
        Compare(app, "15b-stash-dialog");

        // Step 3: the message, then "Stash".
        app.TypeText("Save my work");
        var stash = ExpectOperation(app, "ops", "Stash");
        app.ClickWithMouse(app.Find("ConfirmButton"));
        stash.Wait(OperationOutcome.Succeeded);
        WaitForNone(app, "NameDialog");

        Assert.Equal(0, app.Close());
        Assert.Equal(
            ["stash@{0}: On main: Save my work", "stash@{1}: On main: Experiment"],
            (await repo.GitAsync("stash", "list")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Empty((await repo.GitAsync("status", "--porcelain")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Waits until an element with this automation id is named <paramref name="name"/> (a text's text, a button's label).</summary>
    private static void WaitForName(RealApp app, string automationId, string name) =>
        app.WaitFor(() => app.FindAll(automationId).Any(element => element.Name == name), $"{automationId} \"{name}\"", PartCWait);

    /// <summary>Waits until no element with this automation id is in the window (a dialog closed, a hidden message).</summary>
    private static void WaitForNone(RealApp app, string automationId) =>
        app.WaitFor(() => app.FindAll(automationId).Count == 0, $"no {automationId}", PartCWait);

    /// <summary>Whether the left panel shows the row at <paramref name="path"/> now (<see cref="RealApp.RefRow"/>).</summary>
    private static bool TryRefRow(RealApp app, params string[] path)
    {
        try
        {
            app.RefRow(path);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
