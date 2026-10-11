// Part A of phase 3's scripted walk-through (docs/test-reports/phase-3.md, "Part A: the menus, the
// keyboard, double-click, drag, the toolbar and ignoring a file"): checks 1 to 7, 5b, 23, 24 and
// 26. This class is part A's alone; its expected texts go into Phase3Expectations.A.cs
// (Phase3Expectations.PartA), and further files of its own may hold more of the class
// (Phase3Checks.A.<topic>.cs). Everything the checks share is in Phase3Checks.Support.cs; read its
// header for the conventions (one test method per app start, pictures through Shot, no fixed waits).
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>The checks of part A: menus, the keyboard, double-click, drag, the toolbar, the left panel's selection, ignoring a file.</summary>
public sealed partial class Phase3ChecksA : Phase3Checks
{
    /// <summary>
    /// Check 1, step 1 (the skeleton's example; part A's writer goes on from here): the ops
    /// scenario, a right-click on the row "Alpha two" in its Message column opens its commit menu.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_01_commit_menu()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var mark = app.LogMark();

        // Step 1: right-click the row "Alpha two" in its Message column.
        var pointer = app.GraphRowPoint(PartA.AlphaTwoRow);
        app.RightClick(pointer);
        var menu = app.Menu();
        await app.WaitForAsync(
            () => TextsIn(app.FindByAutomationId("RightPanel")).Contains("Alpha two"),
            "the details of \"Alpha two\"");
        Shot(app, "01a-commit-menu");

        Assert.Equal(PartA.AlphaTwoRow, repository.SelectedIndex);
        AssertMenu(menu, PartA.AlphaTwoCommitMenu);
        Assert.All(menu.Items, item => Assert.True(item.IsEnabled && !item.IsDestructive, $"\"{item.Label}\" is not an enabled ordinary item."));
        AssertHighlighted(menu, "Check out this commit");
        AssertMenuShape(app, menu);
        Assert.Equal(MenuHeight(items: 7, separators: 2), menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuAtPointer(menu, pointer);
        Assert.Single(app.LogLinesSince(mark), line => AppLogFile.MenuOpened("ops", "ce5ea6a Alpha two", "pointer").IsMatch(line));

        // Esc closes the menu; "Alpha two" stays selected.
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.Equal(PartA.AlphaTwoRow, repository.SelectedIndex);
    }
}
