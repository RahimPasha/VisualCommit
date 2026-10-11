// Part A's real-window checks (docs/test-reports/phase-3.md, part A, "How the checks of each part
// are run"): check 23 step 1, check 1 steps 1, 3 and 4, check 5 steps 1 to 3 and check 7 steps 1,
// 2 and 7, in that order, on ONE launch on one ops copy at 1100×700 (check 7's merge last): one
// test method. Graph rows and labels are clicked and dragged at positions computed from the same
// constants as the scripted expectations (RealApp.GraphRowPoint, Phase3Expectations.PartA); menus
// are found by ContextMenu and ContextMenuItem; a pointer resting on a menu item takes the path of
// RealApp.RestPointerOnMenuItem. Only part A's writer edits this file. Shared helpers are in
// Phase3RealWindowPass.cs and RealApp.Checks.cs.
using System.Drawing;
using System.Text.RegularExpressions;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase3RealWindowPass
{
    /// <summary>
    /// Part A's real-window steps on one launch: the toolbar's disabled Stash with its tooltip
    /// (23b), the commit menu, the branch dialog and a disabled item's tooltip (01a to 01c), the
    /// menu opened and driven with the real keyboard (05a, 05b), and the drag of feature/alpha onto
    /// main with its drop menu and the merge (07a, 07b, 07f). Every step before the merge leaves
    /// git's state as it found it and ends with no menu or dialog open.
    /// </summary>
    [Fact]
    public async Task Checks_23_1_5_and_7_on_one_launch()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = Launch(repo);
        var before = await StateAsync(repo);

        // Check 23 step 1: the pointer rests on the disabled Stash; its tooltip inside the window.
        var stash = app.Find("StashButton");
        Assert.False(stash.IsEnabled, "Stash is enabled on a clean ops repository.");
        app.RestPointerOn(stash, PartA.NoChangesToStash);
        Compare(app, "23b-stash-disabled-tooltip", keepPointer: true);
        app.MoveToNeutral();

        // Check 1 step 1: the commit menu of "Alpha two", opened by a right-click in its Message column.
        var alphaTwo = RowPoint(app, PartA.AlphaTwoRow, PartA.OpsLanes);
        var mark = app.LogMark();
        app.RightClickAt(alphaTwo.X, alphaTwo.Y);
        var menu = app.BoundsOf(app.WaitForMenu());
        WaitForDetails(app, "Alpha two");
        Assert.Equal(ItemLabels(PartA.AlphaTwoCommitMenu), app.MenuItemNames());
        AssertNear(new PointF(alphaTwo.X + 2, alphaTwo.Y + 2), menu.Location, "the commit menu's top-left");
        Assert.Equal(PartA.CommitMenuHeight, menu.Height, 2);
        Assert.Single(app.LogLinesSince(mark), line => RealApp.MenuOpened("ops").IsMatch(line));
        Assert.Single(app.LogLinesSince(mark), line => RealApp.MenuOpened("ops", "ce5ea6a Alpha two", "pointer").IsMatch(line));
        Compare(app, "01a-commit-menu", keepPointer: true);
        CloseMenuWithEscape(app);

        // Check 1 step 3: "Create branch here…" opens the name dialog; "Cancel".
        app.RightClickAt(alphaTwo.X, alphaTwo.Y);
        app.WaitForMenu();
        app.ClickMenuItem("Create branch here…");
        app.WaitFor(() => app.FindAll("NameDialog").Count == 1 && app.FindAll("ContextMenu").Count == 0, "the name dialog", RealApp.DefaultWaitTimeout);
        Assert.Equal("Create branch", app.Find("NameDialogTitle").Name);
        Assert.Equal(PartA.AlphaTwoBranchContext, app.Find("NameDialogContext").Name);
        app.WaitFor(() => app.Find("NameBox").Properties.HasKeyboardFocus.ValueOrDefault, "the name box to have the keyboard focus", RealApp.DefaultWaitTimeout);
        Compare(app, "01b-create-branch-dialog");
        app.ClickWithMouse(app.Find("CancelButton"));
        app.WaitFor(() => app.FindAll("NameDialog").Count == 0, "the name dialog to close", RealApp.DefaultWaitTimeout);

        // Check 1 step 4: "Add core"'s menu; the pointer rests on the disabled pick by the
        // conventions' path; its reason shows inside the window.
        var addCore = RowPoint(app, PartA.AddCoreRow, PartA.OpsLanes);
        app.RightClickAt(addCore.X, addCore.Y);
        app.WaitForMenu();
        WaitForDetails(app, "Add core");
        Assert.Equal(ItemLabels(PartA.AddCoreCommitMenu), app.MenuItemNames());
        var pick = app.FindAll("ContextMenuItem").Single(item => item.Name == "Cherry-pick onto main");
        Assert.False(pick.IsEnabled);
        Assert.Equal(PartA.AlreadyOnMain, pick.HelpText);
        app.RestPointerOnMenuItem("Cherry-pick onto main", PartA.AlreadyOnMain);
        Compare(app, "01c-cherry-pick-disabled-tooltip", keepPointer: true);
        CloseMenuWithEscape(app);
        Assert.Equal(before, await StateAsync(repo));

        // Check 5 step 1: click "Alpha two", rest the pointer at the neutral point, Shift+F10 on
        // the real keyboard: one menu, at the message cell's bottom-left, one log line by keyboard.
        app.ClickAt(alphaTwo.X, alphaTwo.Y);
        WaitForDetails(app, "Alpha two");
        app.MoveToNeutral();
        var keyboard = RealApp.MenuOpened("ops", "ce5ea6a Alpha two", "keyboard");
        mark = app.LogMark();
        var opened = app.CountLogLines(keyboard);
        app.PressKeys(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
        menu = app.BoundsOf(app.WaitForMenu());
        app.WaitForLogLine(keyboard, RealApp.DefaultWaitTimeout, opened + 1);
        Assert.Single(app.FindAll("ContextMenu"));
        Assert.Equal(ItemLabels(PartA.AlphaTwoCommitMenu), app.MenuItemNames());
        AssertNear(new PointF((float)PartA.AlphaTwoMessageCellBottomLeft.X, (float)PartA.AlphaTwoMessageCellBottomLeft.Y), menu.Location, "the keyboard menu's top-left");
        Assert.Single(app.LogLinesSince(mark), line => RealApp.MenuOpened("ops").IsMatch(line));
        Assert.Single(app.LogLinesSince(mark), line => keyboard.IsMatch(line));
        AssertFocusedItem(app, "Check out this commit");
        Compare(app, "05a-shift-f10-menu", keepPointer: true);

        // Step 2: Down, then Enter opens the branch dialog; Esc closes it.
        app.PressKey(VirtualKeyShort.DOWN);
        AssertFocusedItem(app, "Create branch here…");
        Compare(app, "05b-second-item-highlighted", keepPointer: true);
        app.PressKey(VirtualKeyShort.RETURN);
        app.WaitFor(() => app.FindAll("NameDialog").Count == 1 && app.FindAll("ContextMenu").Count == 0, "the name dialog", RealApp.DefaultWaitTimeout);
        Assert.Equal(PartA.AlphaTwoBranchContext, app.Find("NameDialogContext").Name);
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => app.FindAll("NameDialog").Count == 0, "the name dialog to close", RealApp.DefaultWaitTimeout);
        Assert.Equal(before, await StateAsync(repo));

        // Step 3: the Menu key (VK_APPS) opens the same menu; End, Home, Up; Esc; Down in the graph.
        mark = app.LogMark();
        opened = app.CountLogLines(keyboard);
        app.PressKey(VirtualKeyShort.APPS);
        menu = app.BoundsOf(app.WaitForMenu());
        app.WaitForLogLine(keyboard, RealApp.DefaultWaitTimeout, opened + 1);
        Assert.Single(app.LogLinesSince(mark), line => keyboard.IsMatch(line));
        AssertNear(new PointF((float)PartA.AlphaTwoMessageCellBottomLeft.X, (float)PartA.AlphaTwoMessageCellBottomLeft.Y), menu.Location, "the Menu key's menu's top-left");
        AssertFocusedItem(app, "Check out this commit");
        app.PressKey(VirtualKeyShort.END);
        AssertFocusedItem(app, "Copy SHA");
        app.PressKey(VirtualKeyShort.HOME);
        AssertFocusedItem(app, "Check out this commit");
        app.PressKey(VirtualKeyShort.UP);
        AssertFocusedItem(app, "Copy SHA");
        CloseMenuWithEscape(app);
        app.PressKey(VirtualKeyShort.DOWN);
        WaitForDetails(app, "Alpha one");
        Assert.Equal(before, await StateAsync(repo));

        // Check 7 step 1: press feature/alpha and move onto main in 5 steps with the real button
        // held (the labels' centres as the scripted run measured them), captured mid-drag.
        var from = new PointF((float)PartA.FeatureAlphaLabelCentre.X, (float)PartA.FeatureAlphaLabelCentre.Y);
        var onto = new PointF((float)PartA.MainLabelCentre.X, (float)PartA.MainLabelCentre.Y);
        app.MouseDownAt(from.X, from.Y);
        var held = true;
        try
        {
            app.MoveHeldTo(onto.X, onto.Y, steps: PartA.DragSteps);
            WaitForDetails(app, "Alpha two");
            Assert.Empty(app.FindAll("ContextMenu"));
            Compare(app, "07a-drag-over-main", keepPointer: true);

            // Step 2: the release opens the drop menu at the release point.
            held = false;
            app.MouseUpAt(onto.X, onto.Y);
        }
        finally
        {
            if (held)
            {
                app.MouseUpAt(onto.X, onto.Y);
            }
        }

        menu = app.BoundsOf(app.WaitForMenu());
        Assert.Equal(ItemLabels(PartA.AlphaOntoMainDropMenu), app.MenuItemNames());
        AssertNear(new PointF(onto.X + 2, onto.Y + 2), menu.Location, "the drop menu's top-left");
        Assert.Equal(PartA.DropMenuHeight, menu.Height, 2);
        Compare(app, "07b-drop-menu", keepPointer: true);
        CloseMenuWithEscape(app);
        Assert.Equal(before, await StateAsync(repo));

        // Step 5's folder clicks only (both "feature" folders open), so that the left panel shows
        // what the scripted 07f shows; the step's own drag is not repeated.
        app.ClickWithMouse(app.RefRow("Local branches", "feature"));
        app.ClickWithMouse(app.RefRow("Remotes", "origin", "feature"));

        // Step 7: feature/alpha onto main once more; "Merge feature/alpha into main".
        app.MouseDownAt(from.X, from.Y);
        held = true;
        try
        {
            app.MoveHeldTo(onto.X, onto.Y, steps: PartA.DragSteps);
            held = false;
            app.MouseUpAt(onto.X, onto.Y);
        }
        finally
        {
            if (held)
            {
                app.MouseUpAt(onto.X, onto.Y);
            }
        }

        app.WaitForMenu();
        var merge = ExpectOperation(app, "ops", "Merge feature/alpha");
        var statusReads = app.CountLogLines(AppLogFile.WorkingTreeRead("ops"));
        app.ClickMenuItem("Merge feature/alpha into main");
        var line = merge.Wait(OperationOutcome.Succeeded);
        Assert.EndsWith(": " + PartA.MergedMessage, line, StringComparison.Ordinal);
        app.WaitFor(() => app.OperationStatusText() == PartA.MergedMessage, "the status bar's merge message", RealApp.DefaultWaitTimeout);
        app.WaitForLogLine(AppLogFile.WorkingTreeRead("ops"), RealApp.DefaultWaitTimeout, statusReads + 1);
        WaitForDetails(app, "Alpha two");
        Compare(app, "07f-merged-by-drop");
        Assert.StartsWith(PartA.DropMergeShort, await repo.HeadAsync(), StringComparison.Ordinal);
        Assert.Equal(PartA.DropMergeSubject, (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.Equal("## main...origin/main [ahead 3]", (await repo.GitAsync("status", "-sb")).StandardOutput.Split('\n')[0].Trim());

        Assert.Equal(0, app.Close());
    }

    /// <summary>The labels of a menu as the report writes it: its items without the header, the separators and the marks after a label.</summary>
    private static List<string> ItemLabels(IEnumerable<string> lines) =>
        [.. lines.Skip(1).Where(line => line != "—").Select(line => MenuMark().Replace(line, string.Empty))];

    /// <summary>Waits until the details panel shows the commit <paramref name="subject"/>.</summary>
    private static void WaitForDetails(RealApp app, string subject) =>
        app.WaitFor(() => app.FindAll("DetailsSubject").Any(element => element.Name == subject), $"the details of \"{subject}\"", RealApp.DefaultWaitTimeout);

    /// <summary>Esc closes the open menu; waits until no menu is in the window.</summary>
    private static void CloseMenuWithEscape(RealApp app)
    {
        app.PressKey(VirtualKeyShort.ESCAPE);
        app.WaitFor(() => app.FindAll("ContextMenu").Count == 0, "the menu to close", RealApp.DefaultWaitTimeout);
    }

    /// <summary>The open menu's item <paramref name="label"/> has the keyboard focus (its highlight, D105).</summary>
    private static void AssertFocusedItem(RealApp app, string label) =>
        app.WaitFor(
            () => app.FindAll("ContextMenuItem").Where(item => item.Properties.HasKeyboardFocus.ValueOrDefault).Select(item => item.Name).SequenceEqual([label]),
            $"\"{label}\" alone to have the keyboard focus",
            RealApp.DefaultWaitTimeout);

    /// <summary>A position within 2 logical pixels of the expected one (4a's ±2).</summary>
    private static void AssertNear(PointF expected, PointF actual, string what) =>
        Assert.True(
            Math.Abs(expected.X - actual.X) <= 2 && Math.Abs(expected.Y - actual.Y) <= 2,
            $"{what} is at {actual}, expected {expected} (±2).");

    /// <summary>Git's state as "leaves git's state as it found it" compares it: refs, HEAD, status, stashes.</summary>
    private static async Task<string> StateAsync(TempRepo repo) =>
        string.Join(
            "\n---\n",
            (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput,
            (await repo.GitAsync("symbolic-ref", "HEAD")).StandardOutput,
            (await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput,
            (await repo.GitAsync("stash", "list")).StandardOutput);

    [GeneratedRegex(@" \((danger|disabled: "".*""|disabled)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex MenuMark();
}
