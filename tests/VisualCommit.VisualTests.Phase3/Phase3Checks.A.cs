// Part A of phase 3's scripted walk-through (docs/test-reports/phase-3.md, "Part A: the menus, the
// keyboard, double-click, drag, the toolbar and ignoring a file"): checks 1 to 7, 5b, 23, 24 and
// 26. This class is part A's alone; its expected texts go into Phase3Expectations.A.cs
// (Phase3Expectations.PartA), and further files of its own may hold more of the class
// (Phase3Checks.A.<topic>.cs). Everything the checks share is in Phase3Checks.Support.cs; read its
// header for the conventions (one test method per app start, pictures through Shot, no fixed waits).
// This file holds check 1 (the commit menu); Phase3Checks.A.Branches.cs checks 2 and 3,
// Phase3Checks.A.Files.cs checks 4 and 26, Phase3Checks.A.Keyboard.cs checks 5 and 5b,
// Phase3Checks.A.Pointer.cs checks 6 and 7, Phase3Checks.A.Toolbar.cs checks 23 and 24, and
// Phase3Checks.A.Helpers.cs what they share.
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>The checks of part A: menus, the keyboard, double-click, drag, the toolbar, the left panel's selection, ignoring a file.</summary>
public sealed partial class Phase3ChecksA : Phase3Checks
{
    /// <summary>Check 1, steps 1 to 5: the commit menu of "Alpha two" and "Add core", "Copy SHA", "Create branch here…" and the "+1" label (ops).</summary>
    [AvaloniaFact]
    public async Task Check_01_commit_menu()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        var before = await GitStateAsync(repo);

        // Step 1: right-click the row "Alpha two" in its Message column.
        var mark = app.LogMark();
        var pointer = app.GraphRowPoint(PartA.AlphaTwoRow);
        app.RightClick(pointer);
        var menu = app.Menu();
        await WaitForDetailsAsync(app, "Alpha two");
        var shot = Shot(app, "01a-commit-menu");

        AssertRowSelected(app, PartA.AlphaTwoRow, shot);
        AssertMenu(menu, PartA.AlphaTwoCommitMenu);

        // Every item is an ordinary one, enabled but for phase 4's "Interactive rebase of main from
        // here…" (phase 4's report, OC1: "This commit is not on main").
        Assert.All(menu.Items, item => Assert.False(item.IsDestructive, $"\"{item.Label}\" is a destructive item."));
        Assert.All(menu.Items.Where(item => item.Label != "Interactive rebase of main from here…"), item => Assert.True(item.IsEnabled, $"\"{item.Label}\" is disabled."));
        AssertHighlighted(menu, "Check out this commit");
        AssertMenuLook(app, menu, shot);
        Assert.Equal(PartA.CommitMenuHeight, menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuAtPointer(menu, pointer);
        Assert.Single(app.LogLinesSince(mark), line => AppLogFile.MenuOpened("ops", null).IsMatch(line));
        Assert.Single(app.LogLinesSince(mark), line => AppLogFile.MenuOpened("ops", "ce5ea6a Alpha two", "pointer").IsMatch(line));

        // Esc closes the menu; "Alpha two" stays selected.
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        AssertRowSelected(app, PartA.AlphaTwoRow);

        // Step 2: right-click "Alpha two" again and click "Copy SHA" (scripted run only).
        app.RightClick(pointer);
        AssertMenu(app.Menu(), PartA.AlphaTwoCommitMenu);
        app.ClickMenuItem("Copy SHA");
        await AssertNoMenuAsync(app, logMark: null);
        await app.WaitForAsync(() => app.Clipboard.Text is not null, "the SHA on the clipboard");
        Assert.Equal([PartA.AlphaTwoSha], app.Clipboard.Texts);
        await AssertNoDialogAsync(app);
        AssertRowSelected(app, PartA.AlphaTwoRow);
        Assert.Equal(before, await GitStateAsync(repo));

        // Step 3: "Create branch here…" opens the name dialog; "Cancel" creates nothing.
        app.RightClick(pointer);
        app.Menu();
        app.ClickMenuItem("Create branch here…");
        await AssertNameDialogAsync(app, new NameDialogExpected(
            "Create branch", PartA.AlphaTwoBranchContext, "Branch name", null, false, "Check out the new branch", true, "Create branch", false));
        Assert.False(app.IsMenuOpen);
        Shot(app, "01b-create-branch-dialog");
        AssertNoClippedText(app);
        app.Click(app.FindByAutomationId("CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.Equal(PartA.OpsBranches, await GitLinesAsync(repo, "for-each-ref", "--format=%(refname:short)", "refs/heads"));

        // Step 4: "Add core" is already on main: its pick is disabled, with its reason as tooltip.
        var addCore = app.GraphRowPoint(PartA.AddCoreRow);
        app.RightClick(addCore);
        menu = app.Menu();
        await WaitForDetailsAsync(app, "Add core");
        var tip = await app.RestPointerOnMenuItemAsync("Cherry-pick onto main");
        shot = Shot(app, "01c-cherry-pick-disabled-tooltip", keepPointer: true);
        Assert.Equal(PartA.AlreadyOnMain, tip);
        Assert.True(Avalonia.Controls.ToolTip.GetIsOpen(app.MenuItemControl("Cherry-pick onto main")));
        AssertRowSelected(app, PartA.AddCoreRow);
        menu = app.Menu();
        AssertMenu(menu, PartA.AddCoreCommitMenu);
        AssertHighlighted(menu, "Check out this commit");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, addCore);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 5: the "+1" label after feature/alpha opens its commit's menu at the pointer.
        var plusOne = app.GraphLabelBounds(PartA.AlphaTwoRow, "+1").Center;
        app.RightClick(plusOne);
        menu = app.Menu();
        AssertRowSelected(app, PartA.AlphaTwoRow);
        AssertMenu(menu, PartA.AlphaTwoCommitMenu);
        AssertHighlighted(menu, "Check out this commit");
        AssertMenuAtPointer(menu, plusOne);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Git's state is unchanged throughout.
        Assert.Equal(before, await GitStateAsync(repo));
    }

    /// <summary>Check 1, step 6: the commit menu of a detached HEAD's commit, opened on the label "HEAD" (edges).</summary>
    [AvaloniaFact]
    public async Task Check_01_detached_head_menu()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var before = await GitStateAsync(repo);
        AssertGraph(app, PartA.EdgesGraph);
        AssertBranch(app, "Detached at 419190f");

        var pointer = app.GraphLabelBounds(0, "HEAD").Center;
        app.RightClick(pointer);
        var menu = app.Menu();
        await WaitForDetailsAsync(app, "Detached work");
        var shot = Shot(app, "01d-detached-commit-menu");

        AssertRowSelected(app, 0, shot);
        AssertMenu(menu, PartA.DetachedCommitMenu);
        AssertHighlighted(menu, "Create branch here…");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.Equal(before, await GitStateAsync(repo));
    }

    /// <summary>Check 1, step 7: the commit menu of a merge commit (graph).</summary>
    [AvaloniaFact]
    public async Task Check_01_merge_commit_menu()
    {
        using var repo = await Scenarios.GraphAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var before = await GitStateAsync(repo);
        Assert.Equal("Merge branch 'feature/login'", Repo(app).Graph.CommitAt(PartA.GraphMergeRow).Subject);

        var pointer = app.GraphRowPoint(PartA.GraphMergeRow);
        app.RightClick(pointer);
        var menu = app.Menu();
        await WaitForDetailsAsync(app, "Merge branch 'feature/login'");
        var shot = Shot(app, "01e-merge-commit-menu");

        AssertRowSelected(app, PartA.GraphMergeRow, shot);
        AssertMenu(menu, PartA.MergeCommitMenu);
        AssertHighlighted(menu, "Check out this commit");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.Equal(before, await GitStateAsync(repo));
    }
}
