// Part B of phase 4's scripted walk-through (docs/test-reports/phase-4.md, "Part B: the interactive
// rebase and squash"): checks B6 to B9, one test method per app start as part B's conventions name
// them (Check_B6_interactive_list, Check_B7_edit_list_and_start, Check_B8_squash_two_commits,
// Check_B9_leave_changed_list, Check_B9_head_moved_while_open). This class is part B's alone; its
// expected texts, menus, graphs, geometry and git states are in Phase4Expectations.B.cs
// (Phase4Expectations.PartB). The class's other files: Phase4Checks.B.Support.cs (what the checks
// share), Phase4Checks.B.Edit.cs (B7), Phase4Checks.B.Squash.cs (B8), Phase4Checks.B.Leave.cs (B9).
// Everything the parts share is in Phase4Checks.Support.cs; read its header for the conventions
// (one test method per app start, pictures through Shot, no fixed waits).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations;
using static VisualCommit.VisualTests.Phase4.Phase4Expectations.PartB;

namespace VisualCommit.VisualTests.Phase4;

/// <summary>The checks of part B: the interactive rebase's list, editing and starting it, squashing selected commits, leaving the list.</summary>
public sealed partial class Phase4ChecksB : Phase4Checks
{
    /// <summary>
    /// Check B6 (steps 1 to 7): Base's commit menu, the list it opens (L0), the selection with the
    /// keys and the base row that takes none, a commit's diff over the list closed by Esc, a row's
    /// menu from its action button and from Shift+F10, the bottom row's S and its disabled Squash
    /// and Fixup, Cancel on an unchanged list.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_B6_interactive_list()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        Assert.Equal(Repository, NameOf(repo));
        AssertGraph(app, GI, Lanes, showsDate: true);
        var palette = PaletteOf(app);

        // Step 1: Base's commit menu; the right-click selects Base.
        var pointer = GraphRowPoint(6);
        Assert.Equal(pointer, app.GraphRowPoint(6));
        app.RightClick(pointer);
        var menu = app.Menu();
        await AssertDetailsAsync(app, BaseDetails);
        var commitMenu = Shot(app, "b6a-commit-menu");
        AssertGraph(app, GI, Lanes, showsDate: true, picture: commitMenu);
        AssertSelectedRows(app, commitMenu, 6);
        AssertBaseCommitMenu(app, menu, pointer);
        Assert.Equal(1, app.CountLogLines(AppLogFile.MenuOpened(Repository)));
        Assert.Equal(1, app.CountLogLines(AppLogFile.MenuOpened(Repository, "4b4622c Base", "pointer")));

        // Step 2: the list L0 in the graph area; Five selected with the keyboard focus.
        var logMark = app.LogMark();
        app.ClickMenuItem(InteractiveFromHereItem);
        await app.WaitForIdleAsync();
        await app.WaitForRebaseListAsync();
        await WaitForListFocusAsync(app, "Five");
        await AssertDetailsAsync(app, FiveDetails);
        var listPicture = Shot(app, "b6b-list");
        await AssertNoMenuAsync(app, logMark);
        await AssertNoDialogAsync(app);
        AssertList(app, L0, L0BaseTop, selected: "Five", canStart: false, startToolTip: UnchangedReason);
        AssertListFocus(app, "Five");
        AssertGraphSelection(app, FiveSha);
        AssertColour(palette.Selection, listPicture, new Point(690, L0[0].Top + 2), "Five's selection background");
        AssertColour(palette.Selection, listPicture, new Point(262, L0[0].Bottom - 2), "Five's selection background at its left end");
        AssertNotSelectionColour(listPicture, palette.Selection, new Point(690, L0[1].Top + 2), "Four's background");
        AssertNoClippedText(app);
        await AssertGitUnchangedAsync(repo);

        // Step 3: two Downs select Three; a click on the base row changes nothing.
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        await WaitForListFocusAsync(app, "Three");
        var keyed = app.RebaseListSnapshot();
        Assert.Equal(["Three"], keyed.Rows.Where(row => row.IsSelected).Select(row => row.Subject));
        Assert.All(keyed.Rows.Take(2), row => Assert.NotEqual(palette.Selection, row.Background));
        Assert.True(keyed.BaseRow!.Bounds!.Value.Contains(BaseRowPoint), $"The base row ({keyed.BaseRow.Bounds}) is not at {BaseRowPoint}.");
        app.Click(BaseRowPoint);
        AssertListFocus(app, "Three");
        AssertList(app, L0, L0BaseTop, selected: "Three", canStart: false, startToolTip: UnchangedReason);
        AssertGraphSelection(app, ThreeSha);
        await AssertDetailsAsync(app, ThreeDetails);
        var threeSelected = Shot(app, "b6c-three-selected");
        AssertColour(palette.Selection, threeSelected, new Point(690, L0[2].Top + 2), "Three's selection background");
        AssertNotSelectionColour(threeSelected, palette.Selection, new Point(690, L0[0].Top + 2), "Five's background");
        AssertNotSelectionColour(threeSelected, palette.Selection, new Point(690, L0[1].Top + 2), "Four's background");
        AssertListFocus(app, "Three");
        await AssertGitUnchangedAsync(repo);

        // Step 4: three.txt's diff over the list; Esc closes the diff, not the list.
        var fileRow = await app.FileRowAsync("FileList", DiffFile);
        app.Click(fileRow);
        await app.WaitForAsync(
            () => Repo(app).Diff is { } diff && diff.Target.Path == DiffFile
                && app.Window.GetVisualDescendants().OfType<VisualCommit.App.Controls.Diff.DiffTextView>().Any(view => view.Name == "DiffText" && view.IsEffectivelyVisible && view.Rows.Count > 0),
            "the diff of src/three.txt over the list",
            OutsideChangeTimeout);
        Shot(app, "b6d-diff-over-list");
        var diffView = app.FindByAutomationId("DiffView");
        Assert.True(diffView.IsEffectivelyVisible, "The diff view does not show.");
        Assert.Equal(ListBounds, app.BoundsOf(diffView));
        Assert.Equal(DiffStatus, InDiff<TextBlock>(app, "DiffStatus").Text);
        Assert.Equal(DiffFileName, InDiff<TextBlock>(app, "DiffFileName").Text);
        Assert.True(InDiff<TextBlock>(app, "DiffFolder").IsEffectivelyVisible, "The diff's header shows no folder.");
        Assert.Equal(DiffFolder, InDiff<TextBlock>(app, "DiffFolder").Text);
        Assert.Equal(DiffSideLabel, InDiff<TextBlock>(app, "DiffSideLabel").Text);
        Assert.True(InDiff<Button>(app, "CloseDiffButton").IsEffectivelyVisible, "The diff has no close button.");
        Assert.Equal(["@@", "A " + DiffAddedLine], DiffRows(app));
        Assert.False(app.IsRebaseListOpen, "The list shows over the diff.");
        Assert.True(Repo(app).IsInteractiveRebaseOpen, "The diff closed the list.");
        await AssertDetailsAsync(app, ThreeDetails);
        Assert.Equal([DiffFile], DetailsFiles(app).Where(row => row.IsSelected).Select(row => row.Key));
        Assert.Equal(DiffFile, Repo(app).Details.SelectedFilePath);

        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !Repo(app).IsDiffOpen && app.IsRebaseListOpen, "the diff to close and the list to show again");
        await AssertNoDialogAsync(app);
        await WaitForListFocusAsync(app, "Three");
        AssertList(app, L0, L0BaseTop, selected: "Three", canStart: false, startToolTip: UnchangedReason);
        Assert.Equal(L0[2].Point, app.RebaseRowPoint("Three"));
        app.ClickRebaseRow("Three");
        AssertListFocus(app, "Three");

        // Step 5: Three's menu from its action button, then from Shift+F10, at the same place.
        var corner = app.RebaseActionButtonCorner("Three");
        app.ClickRebaseActionButton("Three");
        var actionMenu = app.Menu();
        var actionPicture = Shot(app, "b6e-action-menu");
        AssertRowMenu(app, actionMenu, RowMenu("ff42e7f Three", RebaseAction.Pick, bottomRow: false), corner);
        Assert.All(actionMenu.Items, item => Assert.True(item.IsEnabled, $"\"{item.Label}\" is disabled."));
        AssertColour(palette.Hover, actionPicture, new Point(actionMenu.Item(RowMenuHighlighted).Bounds.Right - 4, actionMenu.Item(RowMenuHighlighted).Bounds.Center.Y), "the highlighted item's background");
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        await WaitForListFocusAsync(app, "Three");
        AssertList(app, L0, L0BaseTop, selected: "Three", canStart: false, startToolTip: UnchangedReason);

        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var keyboardMenu = app.Menu();
        AssertRowMenu(app, keyboardMenu, RowMenu("ff42e7f Three", RebaseAction.Pick, bottomRow: false), corner);
        Assert.Equal(actionMenu.Bounds, keyboardMenu.Bounds);
        Assert.Equal(1, app.CountLogLines(AppLogFile.MenuOpened(Repository, "ff42e7f Three", "keyboard")));
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        await WaitForListFocusAsync(app, "Three");
        AssertList(app, L0, L0BaseTop, selected: "Three", canStart: false, startToolTip: UnchangedReason);

        // Step 6: One selected; S cannot apply on the bottom row; its menu with Squash and Fixup disabled.
        Assert.Equal(L0[5].Point, app.RebaseRowPoint("One"));
        app.ClickRebaseRow("One");
        AssertListFocus(app, "One");
        var sMark = app.LogMark();
        app.PressKey(Key.S);
        await AssertNoMenuAsync(app, sMark);
        await AssertNoDialogAsync(app);
        AssertList(app, L0, L0BaseTop, selected: "One", canStart: false, startToolTip: UnchangedReason);
        var onePoint = app.RebaseRowPoint("One");
        app.RightClick(onePoint);
        var oneMenu = app.Menu();
        AssertRowMenu(app, oneMenu, RowMenu("fc6c8fd One", RebaseAction.Pick, bottomRow: true), new Point(602, 291));
        AssertMenuAtPointer(oneMenu, onePoint);
        var tip = await RestOnMenuItemAsync(app, "Squash");
        Assert.Equal(NothingBelowReason, tip);
        AssertToolTipInsideWindow(app, NothingBelowReason);
        var rested = app.Menu();
        AssertHighlighted(rested, RowMenuHighlighted);
        Assert.Equal(palette.TextDisabled, rested.Item("Squash").TextColor);
        Assert.Equal(palette.TextDisabled, rested.Item("Fixup").TextColor);
        Shot(app, "b6f-squash-disabled", keepPointer: true);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        AssertList(app, L0, L0BaseTop, selected: "One", canStart: false, startToolTip: UnchangedReason);

        // Step 7: Cancel on the unchanged list closes it at once; One stays selected in the graph.
        app.ClickRebaseCancel();
        await WaitForListClosedAsync(app);
        await AssertNoDialogAsync(app);
        await WaitForGraphFocusAsync(app);
        await AssertDetailsAsync(app, OneDetails);
        var closed = Shot(app, "b6g-list-closed");
        AssertGraph(app, GI, Lanes, showsDate: true, picture: closed);
        AssertSelectedRows(app, closed, 5);
        AssertGraphSelection(app, OneSha);
        Assert.True(Graph(app).IsFocused, "The graph does not have the keyboard focus.");
        AssertNoClippedText(app);
        await AssertGitUnchangedAsync(repo);
        AssertNoRebaseCall(app);
    }
}
