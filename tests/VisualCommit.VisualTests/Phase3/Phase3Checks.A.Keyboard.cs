// Part A, checks 5 and 5b (docs/test-reports/phase-3.md): menus opened and driven by the keyboard,
// and the menu's placement at the window's edges and its light dismiss.
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksA
{
    /// <summary>Check 5: Shift+F10 and the Menu key, Down, Up, Home, End, Enter and Esc in the graph and the left panel (ops).</summary>
    [AvaloniaFact]
    public async Task Check_05_keyboard()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var before = await GitStateAsync(repo);
        var opened = AppLogFile.MenuOpened("ops");
        var byKeyboard = AppLogFile.MenuOpened("ops", "ce5ea6a Alpha two", "keyboard");

        // Step 1: click "Alpha two", rest the pointer at the neutral point, Shift+F10.
        app.Click(app.GraphRowPoint(PartA.AlphaTwoRow));
        await WaitForDetailsAsync(app, "Alpha two");
        app.MoveToNeutral();
        var mark = app.LogMark();
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var menu = app.Menu();
        var shot = Shot(app, "05a-shift-f10-menu");
        Assert.Equal(1, app.MenuCount);
        AssertRowSelected(app, PartA.AlphaTwoRow);
        AssertMenu(menu, PartA.AlphaTwoCommitMenu);
        AssertHighlighted(menu, "Check out this commit");
        Assert.True(app.MenuItemControl("Check out this commit").IsFocused, "\"Check out this commit\" does not have the focus.");
        AssertMenuLook(app, menu, shot);
        var corner = new Point(PartA.AlphaTwoMessageCellBottomLeft.X, PartA.AlphaTwoMessageCellBottomLeft.Y);
        Assert.True(Math.Abs(menu.Bounds.X - corner.X) <= 2 && Math.Abs(menu.Bounds.Y - corner.Y) <= 2, $"The menu's top-left is {menu.Bounds.TopLeft}, expected {corner}.");
        Assert.Equal(1, app.CountLogLines(opened, mark));
        Assert.Equal(1, app.CountLogLines(byKeyboard, mark));

        // Step 2: Down, Enter, Esc.
        app.PressKey(Key.Down);
        menu = app.Menu();
        shot = Shot(app, "05b-second-item-highlighted");
        AssertHighlighted(menu, "Create branch here…");
        Assert.True(app.MenuItemControl("Create branch here…").IsFocused);
        AssertMenuLook(app, menu, shot);
        app.PressKey(Key.Enter);
        await AssertNameDialogAsync(app, new NameDialogExpected(
            "Create branch", PartA.AlphaTwoBranchContext, "Branch name", null, false, "Check out the new branch", true, "Create branch", false));
        Assert.False(app.IsMenuOpen);
        Shot(app, "05c-enter-opens-dialog");
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(PartA.OpsBranches, await GitLinesAsync(repo, "for-each-ref", "--format=%(refname:short)", "refs/heads"));

        // Step 3: the Menu key; End, Home, Up; Esc; Down in the graph.
        mark = app.LogMark();
        app.PressKey(Key.Apps);
        menu = app.Menu();
        AssertMenu(menu, PartA.AlphaTwoCommitMenu);
        AssertHighlighted(menu, "Check out this commit");
        Assert.True(Math.Abs(menu.Bounds.X - corner.X) <= 2 && Math.Abs(menu.Bounds.Y - corner.Y) <= 2, $"The menu's top-left is {menu.Bounds.TopLeft}, expected {corner}.");
        Assert.Equal(1, app.CountLogLines(opened, mark));
        Assert.Equal(1, app.CountLogLines(byKeyboard, mark));
        app.PressKey(Key.End);
        AssertHighlighted(app.Menu(), "Copy SHA");
        app.PressKey(Key.Home);
        AssertHighlighted(app.Menu(), "Check out this commit");
        app.PressKey(Key.Up);
        AssertHighlighted(app.Menu(), "Copy SHA");
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.Empty(app.Clipboard.Texts);
        app.PressKey(Key.Down);
        AssertRowSelected(app, PartA.AlphaOneRow);
        await WaitForDetailsAsync(app, "Alpha one");

        // Step 4: Up twice to "Main work"; Shift+F10; three Downs skip the separator and the disabled pick.
        app.PressKey(Key.Up);
        app.PressKey(Key.Up);
        AssertRowSelected(app, PartA.MainWorkRow);
        await WaitForDetailsAsync(app, "Main work");
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        menu = app.Menu();
        AssertMenu(menu, PartA.MainWorkCommitMenu);
        AssertHighlighted(menu, "Check out this commit");
        foreach (var expected in new[] { "Create branch here…", "Create tag here…", "Revert this commit" })
        {
            app.PressKey(Key.Down);
            AssertHighlighted(app.Menu(), expected);
        }

        menu = app.Menu();
        shot = Shot(app, "05d-disabled-item-skipped");
        AssertMenuLook(app, menu, shot);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 5: click main in the left panel; Shift+F10 opens its menu 4 inside the window's left edge.
        var main = await app.RefRowAsync("Local branches", "main");
        app.Click(main);
        await WaitForDetailsAsync(app, "Main work");
        app.MoveToNeutral();
        app.PressKey(Key.F10, RawInputModifiers.Shift);
        menu = app.Menu();
        shot = Shot(app, "05e-left-panel-shift-f10");
        AssertRowSelected(app, PartA.MainWorkRow);
        await AssertRefHighlightedAsync(app, "Local branches", "main");
        AssertMenu(menu, PartA.MainMenu);
        AssertHighlighted(menu, "Pull from origin/main");
        AssertMenuLook(app, menu, shot);
        var row = app.BoundsOf(main);
        Assert.True(Math.Abs(menu.Bounds.X - WindowLayout.MenuEdgeInset) <= 2 && Math.Abs(menu.Bounds.Y - row.Bottom) <= 2, $"The menu's top-left is {menu.Bounds.TopLeft}, expected (4, {row.Bottom}).");
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.Same(main, app.Window.FocusManager?.GetFocusedElement());

        Assert.Equal(before, await GitStateAsync(repo));
    }

    /// <summary>Check 5b: the menu flipped above and to the left of the pointer, and light dismiss by a press outside and by the wheel (ops).</summary>
    [AvaloniaFact]
    public async Task Check_05b_placement_and_light_dismiss()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var opened = AppLogFile.MenuOpened("ops");

        // Step 1: below the last row, near the window's bottom: the menu opens above the pointer.
        var pointer = new Point(PartA.BelowLastRowPoint.X, PartA.BelowLastRowPoint.Y);
        app.RightClick(pointer);
        var menu = app.Menu();
        var shot = Shot(app, "05b1-menu-above-pointer");
        AssertMenu(menu, PartA.OpsEmptyAreaMenu);
        AssertHighlighted(menu, "Fetch all remotes");
        AssertMenuLook(app, menu, shot);
        Assert.Equal(PartA.EmptyAreaMenuHeight, menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        Assert.Equal(pointer.Y - 2, menu.Bounds.Bottom, 2);
        Assert.Equal(pointer.X + 2, menu.Bounds.Left, 2);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 2: "Main work"'s file row at x 1090: the menu opens to the left of the pointer.
        app.Click(app.GraphRowPoint(PartA.MainWorkRow));
        await WaitForDetailsAsync(app, "Main work");
        await app.WaitForAsync(() => FileRows(app, "FileList").Count == 1, "the changed files of \"Main work\"");
        Assert.Equal(["M core.txt src"], FileRows(app, "FileList"));
        var file = app.BoundsOf(await app.FileRowAsync("FileList", "src/core.txt"));
        pointer = new Point(PartA.DetailsFileRowX, file.Center.Y);
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "05b2-menu-left-of-pointer");
        AssertMenu(menu, PartA.CoreFileMenu);
        AssertHighlighted(menu, "Open file");
        AssertMenuLook(app, menu, shot);
        Assert.Equal(pointer.X - 2, menu.Bounds.Right, 2);
        Assert.True(menu.Bounds.Right <= app.Window.ClientSize.Width - WindowLayout.MenuEdgeInset + 0.5);
        Assert.Equal(pointer.Y + 2, menu.Bounds.Top, 2);
        AssertRowSelected(app, PartA.MainWorkRow);

        // Step 3: a click on "Alpha one" with the menu open closes it and is not passed on.
        var mark = app.LogMark();
        app.Click(app.GraphRowPoint(PartA.AlphaOneRow));
        await AssertNoMenuAsync(app, mark);
        shot = Shot(app, "05b3-press-outside-not-passed-on");
        AssertRowSelected(app, PartA.MainWorkRow, shot);

        // Step 4: right-click "Alpha two" (its menu opens), then right-click "Conflicting change":
        // the second press only closes the menu.
        mark = app.LogMark();
        app.RightClick(app.GraphRowPoint(PartA.AlphaTwoRow));
        Assert.Equal("ce5ea6a Alpha two", app.Menu().Header);
        AssertRowSelected(app, PartA.AlphaTwoRow);
        app.RightClick(app.GraphRowPoint(PartA.ConflictingChangeRow));
        app.Settle();
        await app.WaitForIdleAsync();
        Assert.False(app.Session.Shell.Menu.IsOpen);
        Assert.Null(app.Session.Shell.Menu.Current);
        Assert.Equal(0, app.MenuCount);
        Assert.Equal(1, app.CountLogLines(opened, mark));
        await WaitForDetailsAsync(app, "Alpha two");
        shot = Shot(app, "05b4-right-press-outside-closes-only");
        AssertRowSelected(app, PartA.AlphaTwoRow, shot);

        // Step 5: right-click "Conflicting change" again: its menu, and only one.
        var conflicting = app.GraphRowPoint(PartA.ConflictingChangeRow);
        app.RightClick(conflicting);
        menu = app.Menu();
        Assert.Equal(1, app.MenuCount);
        Assert.Equal("078ef05 Conflicting change", menu.Header);
        AssertRowSelected(app, PartA.ConflictingChangeRow);
        AssertMenuAtPointer(menu, conflicting);

        // Step 6: one wheel notch over the left panel closes it; the selection stays.
        mark = app.LogMark();
        app.Wheel(new Point(PartA.LeftPanelWheelPoint.X, PartA.LeftPanelWheelPoint.Y), -1);
        await AssertNoMenuAsync(app, mark);
        AssertRowSelected(app, PartA.ConflictingChangeRow);
    }
}
