using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Headless tests of the interactive rebase's list as drawn and used (docs/phase-4-design.md 2.2,
/// 7.2, 7.4, 14.3; docs/test-reports/phase-4.md part B: "The list's geometry", L0, L7, and the
/// steps of B6, B7, B9 and D18 that need no git). The view lies over the real window's graph area
/// at 1100×700 (x 260 to 700, y 88 to 674), dark unless said, so every position is the report's
/// window position; the window's menu layer, dialog layer and Esc chain serve it as in the app.
/// </summary>
public class InteractiveRebaseViewTests
{
    private static readonly Color DarkSelection = Color.Parse("#2A3150");
    private static readonly Color DarkSecondary = Color.Parse("#9BA3B0");
    private static readonly Color DarkDisabled = Color.Parse("#5F6775");
    private static readonly Color DarkLane0 = Color.Parse("#7C8CFF");

    private static RawInputModifiers Raw(KeyModifiers modifiers) =>
        modifiers == KeyModifiers.Meta ? RawInputModifiers.Meta : RawInputModifiers.Control;

    private static void SelectAll(ShellDriver app) => app.PressKey(Key.A, Raw(app.CommandModifier));

    private static bool FocusIsOn(ShellDriver app, string subject) => app.RebaseList().Row(subject).HasKeyboardFocus;

    [AvaloniaFact]
    public async Task The_list_opens_as_L0_with_the_reports_geometry()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        var app = harness.App;
        await app.WaitForRebaseListAsync();
        TestPictures.Save(app, "rebase-list-l0");
        var list = app.RebaseList();

        Assert.Equal(new Rect(260, 88, 440, 586), list.Bounds);
        Assert.Equal(new Rect(260, 88, 440, 36), list.HeaderBounds);
        Assert.Equal(new Rect(260, 630, 440, 44), list.FooterBounds);
        Assert.Equal(("Interactive rebase of main from 4b4622c", null, "6 commits", true), (list.Header, list.HeaderToolTip, list.Count, list.CountShown));
        Assert.Equal(["Five", "Four", "Three", "Fix one", "Two", "One"], list.Subjects);
        Assert.Equal(["57f089d Five", "eef5c15 Four", "ff42e7f Three", "ed58902 Fix one", "938844a Two", "fc6c8fd One"], list.Rows.Select(row => row.Name));
        for (var index = 0; index < list.Rows.Count; index++)
        {
            var row = list.Rows[index];
            var top = 124 + (30 * index);
            Assert.Equal(new Rect(260, top, 440, 30), row.Bounds);
            Assert.Equal(("Pick", 0.0, 1.0, false, null), (row.ActionText, row.Indent, row.Opacity, row.IsStruckThrough, row.SecondLine));
            Assert.Equal(new Rect(265, top + 8, 14, 14), row.GripBounds);
            Assert.Equal(new Rect(288, top + 4, 84, 22), row.ActionButtonBounds);
            Assert.Equal((380, 64), (row.ShaBounds!.Value.X, row.ShaBounds.Value.Width));
            Assert.Equal(452, row.SubjectBounds!.Value.X);
            Assert.Null(row.AuthorBounds);
            Assert.Equal((DarkSecondary, DarkSecondary), (row.ShaColor, row.GripColor));
            Assert.Null(row.SubjectToolTip);
            Assert.Equal(index == 0, row.IsSelected);
            Assert.Equal(index == 0 ? DarkSelection : null, row.Background);
        }

        Assert.False(list.ShowsAuthor);
        Assert.Empty(list.JoinLines);
        Assert.Equal(new Rect(260, 304, 440, 30), list.BaseRow!.Bounds);
        Assert.Equal(("4b4622c", "Base", "Base 4b4622c Base", DarkSecondary), (list.BaseRow.ShortSha, list.BaseRow.Subject, list.BaseRow.Name, list.BaseRow.LabelColor));
        Assert.True(list.Rows[0].HasKeyboardFocus);
        Assert.Equal([InteractiveList.Five], harness.Host.Details);

        // The footer: Cancel and Start rebase right-aligned, 8 apart, Start's right edge 12 from the list's.
        Assert.Equal((false, "Change an action or the order first", true), (list.CanStart, list.StartToolTip, list.StartShown));
        Assert.Equal(688, list.StartBounds.Right);
        Assert.Equal(8, list.StartBounds.X - list.CancelBounds.Right);
        Assert.Equal(list.FooterBounds.Center.Y, list.StartBounds.Center.Y, 0.5);

        // The header's text and the count do not overlap.
        var header = app.FindByAutomationId("RebaseListHeader");
        var count = app.FindByAutomationId("RebaseListCount");
        Assert.True(app.BoundsOf(header).Right <= app.BoundsOf(count).X);

        // The automation ids of design 7.4 are there.
        foreach (var id in new[] { "InteractiveRebaseView", "RebaseListHeader", "RebaseListCount", "RebaseRow", "RebaseActionButton", "RebaseBaseRow", "RebaseCancelButton", "RebaseStartButton" })
        {
            Assert.NotNull(app.FindByAutomationId(id));
        }
    }

    [AvaloniaFact]
    public async Task Keys_move_the_selection_with_the_focus_and_a_click_on_the_base_row_changes_nothing()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        var app = harness.App;
        await app.WaitForRebaseListAsync();

        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        Assert.Equal("Three", app.RebaseList().Selected!.Subject);
        Assert.True(FocusIsOn(app, "Three"));
        Assert.Equal([null, null, DarkSelection], app.RebaseList().Rows.Take(3).Select(row => row.Background));

        app.Click(new Point(600, 319));
        Assert.Equal("Three", app.RebaseList().Selected!.Subject);
        Assert.True(FocusIsOn(app, "Three"));
        Assert.Equal([InteractiveList.Five, InteractiveList.Four, InteractiveList.Three], harness.Host.Details);

        app.PressKey(Key.End);
        Assert.Equal("One", app.RebaseList().Selected!.Subject);
        Assert.True(FocusIsOn(app, "One"));
        app.PressKey(Key.Down);
        Assert.True(FocusIsOn(app, "One"));
        app.PressKey(Key.Home);
        Assert.True(FocusIsOn(app, "Five"));
        app.PressKey(Key.Up);
        Assert.True(FocusIsOn(app, "Five"));

        // A click selects a row and gives it the focus.
        app.ClickRebaseRow("Two");
        Assert.True(FocusIsOn(app, "Two"));
        Assert.Equal("Two", app.RebaseList().Selected!.Subject);
    }

    [AvaloniaFact]
    public async Task The_action_button_and_Shift_F10_open_the_rows_menu_at_the_buttons_bottom_left_corner()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        var app = harness.App;
        await app.WaitForRebaseListAsync();
        app.ClickRebaseRow("Three");
        var mark = app.LogMark();

        app.ClickRebaseActionButton("Three");
        var menu = app.Menu();
        TestPictures.Save(app, "rebase-list-action-menu");
        Assert.Equal(["ff42e7f Three", "Reword…", "Squash", "Fixup", "Drop"], menu.Lines());
        Assert.Equal("Reword…", menu.Highlighted!.Label);
        Assert.Equal(WindowLayout.MenuHeight(4, 0), menu.Bounds.Height, 2);
        Assert.Equal(app.RebaseActionButtonCorner("Three"), menu.Bounds.TopLeft);
        Assert.Equal(new Point(288, 210), menu.Bounds.TopLeft);
        Assert.Equal(1, app.CountLogLines(new Regex(@"Context menu opened: ff42e7f Three by pointer$"), mark));

        app.PressKey(Key.Escape);
        Assert.False(app.IsMenuOpen);
        Assert.True(FocusIsOn(app, "Three"));
        Assert.Equal(RebaseAction.Pick, app.RebaseList().Row("Three").Action);

        app.PressKey(Key.F10, RawInputModifiers.Shift);
        var byKeyboard = app.Menu();
        Assert.Equal(menu.Lines(), byKeyboard.Lines());
        Assert.Equal("Reword…", byKeyboard.Highlighted!.Label);
        Assert.Equal(menu.Bounds, byKeyboard.Bounds);
        Assert.Equal(1, app.CountLogLines(new Regex(@"Context menu opened: ff42e7f Three by keyboard$"), mark));
        app.PressKey(Key.Escape);
        Assert.False(app.IsMenuOpen);
        Assert.True(FocusIsOn(app, "Three"));

        // The Menu key opens it at the same place.
        app.PressKey(Key.Apps);
        Assert.Equal(menu.Bounds, app.Menu().Bounds);
        Assert.Equal(2, app.CountLogLines(new Regex(@"Context menu opened: ff42e7f Three by keyboard$"), mark));
        app.PressKey(Key.Escape);
        Assert.True(FocusIsOn(app, "Three"));
        Assert.False(harness.ViewModel.IsChanged);
    }

    [AvaloniaFact]
    public async Task On_the_bottom_row_S_does_nothing_and_its_menu_has_Squash_and_Fixup_disabled_with_their_reason()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        var app = harness.App;
        await app.WaitForRebaseListAsync();

        app.ClickRebaseRow("One");
        app.PressKey(Key.S);
        Assert.Equal(RebaseAction.Pick, app.RebaseList().Row("One").Action);
        Assert.False(app.IsMenuOpen);
        Assert.Null(app.TopDialogView);
        Assert.False(app.RebaseList().CanStart);

        app.RightClickRebaseRow("One");
        var menu = app.Menu();
        Assert.Equal(new Point(602, 291), menu.Bounds.TopLeft);
        Assert.Equal(
            [
                "fc6c8fd One",
                "Reword…",
                "Squash (disabled: \"The oldest commit has nothing below it to join\")",
                "Fixup (disabled: \"The oldest commit has nothing below it to join\")",
                "Drop",
            ],
            menu.Lines());
        Assert.Equal(DarkDisabled, menu.Item("Squash").TextColor);
        Assert.Equal("The oldest commit has nothing below it to join", await app.RestPointerOnMenuItemAsync("Squash"));
        Assert.Equal("Reword…", app.Menu().Highlighted!.Label);
        TestPictures.Save(app, "rebase-list-squash-disabled");
        app.PressKey(Key.Escape);
        Assert.False(harness.ViewModel.IsChanged);
    }

    [AvaloniaFact]
    public async Task A_drag_shows_the_line_on_the_nearest_gap_and_the_drop_moves_the_row_there()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        var app = harness.App;
        await app.WaitForRebaseListAsync();
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        Assert.Equal("Fix one", app.RebaseList().Selected!.Subject);

        // The row with the keyboard focus has the selection background and no focus frame.
        var focused = app.Capture();
        Assert.Equal(DarkSelection, focused.PixelAt(261, 215));
        Assert.Equal(DarkSelection, focused.PixelAt(698, 242));

        app.PressMouse(new Point(600, 229));
        app.MoveMouseHeld(new Point(600, 270), steps: 5);
        var held = app.RebaseList();
        TestPictures.Save(app, "rebase-list-drag");
        Assert.Equal(["Five", "Four", "Three", "Fix one", "Two", "One"], held.Subjects);
        Assert.Equal(0.5, held.Row("Fix one").Opacity);
        Assert.All(held.Rows.Where(row => row.Subject != "Fix one"), row => Assert.Equal(1, row.Opacity));
        Assert.Equal(5, held.DragGap);
        Assert.Equal(new Rect(260, 273, 440, 2), held.DragLine);
        Assert.True(harness.View.IsDragging);
        Assert.False(app.IsMenuOpen);

        app.ReleaseMouse(new Point(600, 270));
        var moved = app.RebaseList();
        Assert.Equal(["Five", "Four", "Three", "Two", "Fix one", "One"], moved.Subjects);
        Assert.Null(moved.DragLine);
        Assert.Null(moved.DragGap);
        Assert.Equal(new Rect(260, 244, 440, 30), moved.Row("Fix one").Bounds);
        Assert.Equal((1.0, true, true), (moved.Row("Fix one").Opacity, moved.Row("Fix one").IsSelected, moved.Row("Fix one").HasKeyboardFocus));
        Assert.All(moved.Rows, row => Assert.Equal("Pick", row.ActionText));
        Assert.True(moved.CanStart);
        Assert.Null(moved.StartToolTip);
        Assert.Equal(new Rect(260, 304, 440, 30), moved.BaseRow!.Bounds);
    }

    [AvaloniaFact]
    public async Task A_press_that_moves_no_more_than_4_px_is_no_drag()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        var app = harness.App;
        await app.WaitForRebaseListAsync();

        app.PressMouse(new Point(600, 199));
        app.MoveMouseHeld(new Point(600, 203));
        Assert.False(harness.View.IsDragging);
        app.ReleaseMouse(new Point(600, 203));
        Assert.Equal("Three", app.RebaseList().Selected!.Subject);
        Assert.False(harness.ViewModel.IsChanged);
    }

    [AvaloniaFact]
    public async Task B7s_edits_draw_the_indent_the_join_lines_and_the_second_lines_as_L7()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        var app = harness.App;
        await app.WaitForRebaseListAsync();
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        app.PressMouse(new Point(600, 229));
        app.MoveMouseHeld(new Point(600, 270), steps: 5);
        app.ReleaseMouse(new Point(600, 270));

        // Step 4: Fix one's menu from its action button, then Fixup.
        app.ClickRebaseActionButton("Fix one");
        var fixOneMenu = app.Menu();
        Assert.Equal(["ed58902 Fix one", "Reword…", "Squash", "Fixup", "Drop"], fixOneMenu.Lines());
        Assert.Equal(app.RebaseActionButtonCorner("Fix one"), fixOneMenu.Bounds.TopLeft);
        TestPictures.Save(app, "rebase-list-fix-one-menu");
        app.ClickMenuItem("Fixup");
        var fixup = app.RebaseList();
        var fixOne = fixup.Row("Fix one");
        Assert.Equal(("Fixup", 16.0), (fixOne.ActionText, fixOne.Indent));
        Assert.Equal(new Rect(281, 252, 14, 14), fixOne.GripBounds);
        Assert.Equal(304, fixOne.ActionButtonBounds!.Value.X);
        Assert.Equal(396, fixOne.ShaBounds!.Value.X);
        Assert.Equal(468, fixOne.SubjectBounds!.Value.X);
        var line = Assert.Single(fixup.JoinLines);
        Assert.Equal((InteractiveList.FixOne, InteractiveList.One, new Rect(272, 259, 2, 15), DarkLane0), (line.MemberSha, line.TargetSha, line.Bounds, line.Color));
        Assert.Null(fixup.Row("One").SecondLine);

        // Step 5: Four right-clicked and squashed into Three.
        app.ClickRebaseRow("Four");
        app.RightClickRebaseRow("Four");
        Assert.Equal(["eef5c15 Four", "Reword…", "Squash", "Fixup", "Drop"], app.Menu().Lines());
        app.ClickMenuItem("Squash");
        var squash = app.RebaseList();
        Assert.Equal(("Squash", 16.0), (squash.Row("Four").ActionText, squash.Row("Four").Indent));
        Assert.Equal(("Message: Three", new Rect(260, 214, 440, 22)), (squash.Row("Three").SecondLine, squash.Row("Three").SecondLineBounds));
        Assert.Equal([124.0, 154, 184, 236, 266, 296], squash.Rows.Select(row => row.Bounds!.Value.Y));
        Assert.Equal(326, squash.BaseRow!.Bounds!.Value.Y);
        Assert.Equal(
            [(InteractiveList.Four, new Rect(272, 169, 2, 15)), (InteractiveList.FixOne, new Rect(272, 281, 2, 15))],
            squash.JoinLines.Select(joined => (joined.MemberSha, joined.Bounds)));
        var edit = squash.Row("Three").EditButtonBounds!.Value;
        Assert.True(edit.Right <= 688 && edit.Y >= 214 && edit.Bottom <= 236);

        // Step 6 and 7: the group's message edited through the dialog.
        app.ClickRebaseEditMessage("Three");
        var dialog = app.MessageDialog();
        TestPictures.Save(app, "rebase-list-squash-message");
        Assert.Equal(
            ("Message of the squashed commit", "Three", 5, true, "67", "#42 is the issue\n\nFour", "Save", true, null, "Cancel", null),
            (dialog.Title, dialog.Summary, dialog.SummaryCaret, dialog.SummaryHasFocus, dialog.Counter, dialog.Description, dialog.ConfirmText, dialog.ConfirmEnabled, dialog.ConfirmToolTip, dialog.CancelText, dialog.Repository));
        Assert.Equal(460, dialog.CardBounds.Width);
        SelectAll(app);
        app.PressKey(Key.Delete);
        var empty = app.MessageDialog();
        Assert.Equal(("72", false, "Write a summary"), (empty.Counter, empty.ConfirmEnabled, empty.ConfirmToolTip));
        app.Type("Three and four");
        app.Click(dialog.DescriptionBounds.Center);
        SelectAll(app);
        app.Type("#42 is the issue");
        var edited = app.MessageDialog();
        Assert.Equal(("Three and four", "58", "#42 is the issue", true), (edited.Summary, edited.Counter, edited.Description, edited.ConfirmEnabled));
        app.Click(app.FindByAutomationId("ConfirmButton"));
        Assert.Null(app.TopDialogView);
        Assert.Equal("Message: Three and four", app.RebaseList().Row("Three").SecondLine);
        Assert.Equal("Four", app.RebaseList().Selected!.Subject);

        // Step 8: Reword from Two's menu, Esc in the dialog, then R and Enter.
        app.ClickRebaseRow("Two");
        Assert.Equal(new Point(600, 251), app.RebaseRowPoint("Two"));
        app.ChooseRebaseAction("Two", "Reword…");
        var reword = app.MessageDialog();
        Assert.Equal(("Reword 938844a", "Two", 3, true, "69", "", "Save", true), (reword.Title, reword.Summary, reword.SummaryCaret, reword.SummaryHasFocus, reword.Counter, reword.Description, reword.ConfirmText, reword.ConfirmEnabled));
        TestPictures.Save(app, "rebase-list-reword-dialog");
        app.PressKey(Key.Escape);
        Assert.Null(app.TopDialogView);
        Assert.True(harness.App.Window.IsVisible);
        Assert.Equal(("Pick", null), (app.RebaseList().Row("Two").ActionText, app.RebaseList().Row("Two").SecondLine));
        Assert.False(harness.ViewModel.IsClosed);
        app.ClickRebaseRow("Two");
        app.PressKey(Key.R);
        Assert.Equal("Reword 938844a", app.MessageDialog().Title);
        SelectAll(app);
        app.Type("Two reworded");
        app.PressKey(Key.Enter);
        Assert.Null(app.TopDialogView);
        var reworded = app.RebaseList();
        Assert.Equal(("Reword", "New message: Two reworded", new Rect(260, 266, 440, 22)), (reworded.Row("Two").ActionText, reworded.Row("Two").SecondLine, reworded.Row("Two").SecondLineBounds));
        Assert.Equal(new Rect(272, 303, 2, 15), reworded.JoinLines.Single(joined => joined.MemberSha == InteractiveList.FixOne).Bounds);

        // Step 9: D drops Five; the list is L7.
        app.ClickRebaseRow("Five");
        app.PressKey(Key.D);
        app.MoveToNeutral();
        TestPictures.Save(app, "rebase-list-l7");
        var l7 = app.RebaseList();
        Assert.Equal(
            [
                ("Five", 124.0, "Drop", 0.0, true, null),
                ("Four", 154, "Squash", 16, false, null),
                ("Three", 184, "Pick", 0, false, "Message: Three and four"),
                ("Two", 236, "Reword", 0, false, "New message: Two reworded"),
                ("Fix one", 288, "Fixup", 16, false, null),
                ("One", 318, "Pick", 0, false, (string?)null),
            ],
            l7.Rows.Select(row => (row.Subject, row.Bounds!.Value.Y, row.ActionText, row.Indent, row.IsStruckThrough, row.SecondLine)));
        Assert.Equal((DarkDisabled, DarkDisabled), (l7.Row("Five").ShaColor, l7.Row("Five").SubjectColor));
        Assert.Equal(new Rect(260, 348, 440, 30), l7.BaseRow!.Bounds);
        Assert.Equal(
            [(InteractiveList.Four, InteractiveList.Three, new Rect(272, 169, 2, 15)), (InteractiveList.FixOne, InteractiveList.One, new Rect(272, 303, 2, 15))],
            l7.JoinLines.Select(joined => (joined.MemberSha, joined.TargetSha, joined.Bounds)));
        Assert.Equal(("Interactive rebase of main from 4b4622c", "6 commits", true), (l7.Header, l7.Count, l7.CanStart));
        Assert.Equal(DarkSelection, l7.Row("Five").Background);

        // The pixels: the join line in the lane colour over the rows' backgrounds.
        var shot = app.Capture();
        Assert.Equal(DarkLane0, shot.PixelAt(272, 178));
        Assert.Equal(DarkLane0, shot.PixelAt(273, 310));

        // Step 10: Start hands B7's plan over and the list closes.
        app.ClickRebaseRow("Three");
        app.ClickRebaseStart();
        await app.WaitForAsync(() => harness.Host.Closed == 1, "the list to close after Start");
        var plan = Assert.Single(harness.Host.Plans);
        Assert.Equal(
            [
                (InteractiveList.One, RebaseAction.Pick, null),
                (InteractiveList.FixOne, RebaseAction.Fixup, null),
                (InteractiveList.Two, RebaseAction.Reword, "Two reworded"),
                (InteractiveList.Three, RebaseAction.Pick, "Three and four\n\n#42 is the issue"),
                (InteractiveList.Four, RebaseAction.Squash, null),
                (InteractiveList.Five, RebaseAction.Drop, (string?)null),
            ],
            plan.StepsOldestFirst.Select(step => (step.Sha, step.Action, step.Message)));
        Assert.False(app.IsRebaseListOpen);
    }

    [AvaloniaFact]
    public async Task On_the_interactive_scenario_B7s_edits_hand_git_a_plan_that_makes_the_reports_commits()
    {
        using var repo = await Scenarios.InteractiveAsync();
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path, repository: repo.Path);
        var app = harness.App;
        var repository = harness.Repository!;
        await app.WaitForRebaseListAsync();

        // The list the git layer read is L0, and the graph behind it selected Five.
        Assert.Equal(["Five", "Four", "Three", "Fix one", "Two", "One"], app.RebaseList().Subjects);
        Assert.Equal(InteractiveList.Five, repository.Selection.Single);
        app.MoveToNeutral();
        app.WaitForCommitDetails();
        TestPictures.Save(app, "rebase-list-on-repository");

        // B7 steps 2 to 9.
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        Assert.Equal(InteractiveList.FixOne, repository.Selection.Single);
        app.DragRebaseRow("Fix one", new Point(600, 270));
        app.ChooseRebaseAction("Fix one", "Fixup");
        app.ClickRebaseRow("Four");
        app.RightClickRebaseRow("Four");
        app.ClickMenuItem("Squash");
        app.ClickRebaseEditMessage("Three");
        app.PressKey(Key.A, Raw(app.CommandModifier));
        app.PressKey(Key.Delete);
        app.Type("Three and four");
        app.Click(app.MessageDialog().DescriptionBounds.Center);
        app.PressKey(Key.A, Raw(app.CommandModifier));
        app.Type("#42 is the issue");
        app.Click(app.FindByAutomationId("ConfirmButton"));
        app.ClickRebaseRow("Two");
        app.PressKey(Key.R);
        app.PressKey(Key.A, Raw(app.CommandModifier));
        app.Type("Two reworded");
        app.PressKey(Key.Enter);
        app.ClickRebaseRow("Five");
        app.PressKey(Key.D);
        app.MoveToNeutral();
        Assert.Equal(InteractiveList.Five, repository.Selection.Single);
        app.WaitForCommitDetails();
        TestPictures.Save(app, "rebase-list-on-repository-l7");
        Assert.Equal(["Drop", "Squash", "Pick", "Reword", "Fixup", "Pick"], app.RebaseList().Rows.Select(row => row.ActionText));

        // Start: the plan goes to the real git layer, as the repository's operation hands it over.
        harness.Host.OnStart = async plan =>
        {
            var result = await repository.Repository.InteractiveRebaseAsync(plan);
            return result.Outcome == RebaseOutcome.Rebased;
        };
        app.ClickRebaseRow("Three");
        app.ClickRebaseStart();
        await app.WaitForAsync(() => harness.Host.Closed == 1, "the rebase to end and the list to close", TimeSpan.FromSeconds(60));

        var log = await repo.GitAsync("log", "--format=%H", "main");
        Assert.Equal(
            [
                "a081bffd034ab3d4fda4e1c073fe510a24f7c0d7",
                "b3507d37adfe56baf58cf29ec0912ff444225b8c",
                "ab4b49d005eb36535c58e54b01db091946b74b2e",
                InteractiveList.BaseSha,
                "da32da349cff2210ec951624b25f62825dd435ec",
            ],
            log.StandardOutput.TrimEnd('\n').Split('\n'));
        var squashed = await repo.GitAsync("cat-file", "commit", "a081bffd034ab3d4fda4e1c073fe510a24f7c0d7");
        Assert.EndsWith("\n\nThree and four\n\n#42 is the issue\n", squashed.StandardOutput, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Esc_cancels_a_drag_first_and_then_closes_a_changed_list_only_after_the_question()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        var app = harness.App;
        await app.WaitForRebaseListAsync();

        // Step 2: a drag of Five, Esc while held, then the release.
        app.PressMouse(new Point(600, 139));
        app.MoveMouseHeld(new Point(600, 210), steps: 5);
        Assert.Equal((3, 0.5), (app.RebaseList().DragGap, app.RebaseList().Row("Five").Opacity));
        Assert.Equal(new Rect(260, 213, 440, 2), app.RebaseList().DragLine);
        app.PressKey(Key.Escape);
        Assert.False(harness.View.IsDragging);
        Assert.Null(app.RebaseList().DragLine);
        Assert.Equal(1, app.RebaseList().Row("Five").Opacity);
        Assert.Null(app.TopDialogView);
        app.ReleaseMouse(new Point(600, 210));
        Assert.Equal(["Five", "Four", "Three", "Fix one", "Two", "One"], app.RebaseList().Subjects);
        Assert.False(harness.ViewModel.IsChanged);
        Assert.True(app.IsRebaseListOpen);

        // Steps 3 and 4: Alt+Down and Alt+Up.
        app.PressKey(Key.Down, RawInputModifiers.Alt);
        Assert.Equal(["Four", "Five", "Three", "Fix one", "Two", "One"], app.RebaseList().Subjects);
        Assert.Equal((new Rect(260, 154, 440, 30), true, true), (app.RebaseList().Row("Five").Bounds, app.RebaseList().Row("Five").IsSelected, FocusIsOn(app, "Five")));
        Assert.True(app.RebaseList().CanStart);
        app.PressKey(Key.Up, RawInputModifiers.Alt);
        Assert.Equal(["Five", "Four", "Three", "Fix one", "Two", "One"], app.RebaseList().Subjects);
        Assert.True(FocusIsOn(app, "Five"));
        Assert.Equal("Change an action or the order first", await app.RestPointerOnAsync(app.FindByAutomationId("RebaseStartButton")));

        // Step 5: a changed list asks first.
        app.PressKey(Key.Down, RawInputModifiers.Alt);
        app.PressKey(Key.Escape);
        var question = app.Dialog<ConfirmationViewModel>();
        Assert.Equal(("Discard the changes to this list?", "", "", "Discard", true, "Keep editing"),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive, question.CancelText));
        Assert.True(app.FindByAutomationId("CancelButton").IsFocused);
        TestPictures.Save(app, "rebase-list-discard-question");

        // Step 6: Keep editing.
        app.Click(app.FindByAutomationId("CancelButton"));
        Assert.Null(app.TopDialogView);
        Assert.True(app.IsRebaseListOpen);
        Assert.Equal(["Four", "Five", "Three", "Fix one", "Two", "One"], app.RebaseList().Subjects);
        Assert.Equal(0, harness.Host.Closed);

        // Step 7: Cancel asks the same; Discard closes the list.
        app.ClickRebaseCancel();
        Assert.Equal("Discard the changes to this list?", app.Dialog<ConfirmationViewModel>().Title);
        app.Click(app.FindByAutomationId("ConfirmButton"));
        Assert.Null(app.TopDialogView);
        Assert.Equal(1, harness.Host.Closed);
        Assert.False(app.IsRebaseListOpen);
        Assert.Empty(harness.Host.Plans);
        Assert.Equal(2, harness.Host.Questions.Count);
    }

    [AvaloniaFact]
    public async Task Esc_closes_an_unchanged_list_at_once_and_a_hidden_list_lets_Esc_go_on()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        var app = harness.App;
        await app.WaitForRebaseListAsync();

        // Hidden (a diff over it): Esc is not the list's.
        harness.View.IsVisible = false;
        app.PressKey(Key.Escape);
        Assert.Equal(0, harness.Host.Closed);
        harness.View.IsVisible = true;
        app.Settle();

        app.PressKey(Key.Escape);
        Assert.Null(app.TopDialogView);
        Assert.Empty(harness.Host.Questions);
        Assert.Equal(1, harness.Host.Closed);
        Assert.False(app.IsRebaseListOpen);
    }

    [AvaloniaFact]
    public async Task While_Start_runs_the_rows_and_buttons_take_no_input()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path);
        var app = harness.App;
        await app.WaitForRebaseListAsync();
        app.PressKey(Key.D);
        harness.Host.HeldStart = new TaskCompletionSource<bool>();

        app.ClickRebaseStart();
        var running = app.RebaseList();
        Assert.False(running.IsInteractive);
        Assert.False(running.CanStart);
        Assert.False(app.FindByAutomationId("RebaseCancelButton").IsEffectivelyEnabled);
        Assert.False(app.RebaseList().Rows[0].Bounds is null);
        app.PressKey(Key.Down);
        app.PressKey(Key.P);
        app.PressKey(Key.Escape);
        Assert.Equal((RebaseAction.Drop, "Five", 0), (harness.ViewModel.Rows[0].Action, harness.ViewModel.SelectedRow!.Subject, harness.Host.Closed));
        Assert.Null(app.TopDialogView);

        // Git refused before anything started: the list is as it was, and takes input again.
        harness.Host.HeldStart.SetResult(false);
        await app.WaitForAsync(() => harness.ViewModel.IsInteractive, "Start to end");
        Assert.True(app.RebaseList().CanStart);
        Assert.True(app.IsRebaseListOpen);
        Assert.Equal(RebaseAction.Drop, app.RebaseList().Rows[0].Action);
    }

    [AvaloniaFact]
    public async Task A_list_that_cannot_be_made_shows_its_message_in_the_rows_place_with_only_Cancel()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path, InteractiveList.Made(4, mergeCount: 1));
        var app = harness.App;
        await app.WaitForRebaseListAsync();
        TestPictures.Save(app, "rebase-list-message");

        var list = app.RebaseList();
        Assert.Equal("A merge lies between 4b4622c and main: interactive rebase would flatten it.", list.Message);
        Assert.True(app.FindByAutomationId("RebaseListMessage").IsEffectivelyVisible);
        Assert.Empty(list.Rows);
        Assert.Null(list.BaseRow);
        Assert.False(list.CountShown);
        Assert.False(list.StartShown);
        Assert.True(app.FindByAutomationId("RebaseCancelButton").IsFocused);
        app.ClickRebaseCancel();
        Assert.Equal(1, harness.Host.Closed);
        Assert.Empty(harness.Host.Questions);
    }

    [AvaloniaFact]
    public async Task A_long_list_virtualises_its_rows_and_the_keys_scroll_the_selected_row_into_view()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path, InteractiveList.Made(300));
        var app = harness.App;
        await app.WaitForRebaseListAsync();

        var list = app.RebaseList();
        Assert.Equal(("300 commits", "Commit 300"), (list.Count, list.Rows[0].Subject));
        Assert.True(list.Rows.Count(row => row.Bounds is not null) < 60);

        app.PressKey(Key.End);
        var end = app.RebaseList();
        Assert.Equal("Commit 1", end.Selected!.Subject);
        Assert.True(end.Selected.HasKeyboardFocus);
        Assert.InRange(end.Selected.Bounds!.Value.Bottom, 124, 630);
        Assert.InRange(end.BaseRow!.Bounds!.Value.Bottom, 124, 631);

        app.PressKey(Key.Up, RawInputModifiers.Alt);
        Assert.Equal("Commit 1", app.RebaseList().Rows[298].Subject);
        Assert.True(app.RebaseList().Rows[298].HasKeyboardFocus);
        app.PressKey(Key.Home);
        Assert.True(app.RebaseList().Rows[0].HasKeyboardFocus);
        Assert.Equal(new Rect(260, 124, 440, 30), app.RebaseList().Rows[0].Bounds);

        // A drag held near the rows' bottom edge scrolls them, so a row goes past the rows in view.
        app.PressMouse(new Point(600, 139));
        app.MoveMouseHeld(new Point(600, 600), steps: 5);
        for (var move = 0; move < 20; move++)
        {
            app.MoveMouseHeld(new Point(600, 625 + (move % 2)));
        }

        Assert.True(app.RebaseList().Rows[0].Bounds is null || app.RebaseList().Rows[0].Bounds!.Value.Y < 124);
        app.ReleaseMouse(new Point(600, 626));
        var moved = harness.ViewModel.IndexOf(harness.ViewModel.Rows.Single(row => row.Subject == "Commit 300"));
        Assert.InRange(moved, 20, 299);
        Assert.True(app.RebaseList().Rows[moved].HasKeyboardFocus);
    }

    [AvaloniaFact]
    public async Task At_1920_wide_and_light_the_author_column_shows_and_the_colours_are_the_light_tokens()
    {
        using var data = new TempDirectory("data");
        using var harness = await RebaseListHarness.StartAsync(data.Path, width: 1920, height: 1080);
        var app = harness.App;
        app.Window.RequestedThemeVariant = ThemeVariant.Light;
        await app.WaitForRebaseListAsync();

        // D18 step 3: d on Five, Down, s on Four.
        app.PressKey(Key.D);
        app.PressKey(Key.Down);
        app.PressKey(Key.S);
        app.MoveToNeutral();
        TestPictures.Save(app, "rebase-list-light-1920");
        var list = app.RebaseList();

        Assert.Equal(new Rect(260, 88, 1260, 966), list.Bounds);
        Assert.True(list.ShowsAuthor);
        Assert.All(list.Rows, row => Assert.Equal(140, row.AuthorBounds!.Value.Width));
        Assert.All(list.Rows, row => Assert.Equal(1508, row.AuthorBounds!.Value.Right));
        Assert.Equal((Color.Parse("#5C6572"), Color.Parse("#A1A8B3")), (list.Row("Four").GripColor, list.Row("Five").SubjectColor));
        Assert.Equal(("Drop", "Squash", "Message: Three"), (list.Row("Five").ActionText, list.Row("Four").ActionText, list.Row("Three").SecondLine));
        Assert.True(list.Row("Four").HasKeyboardFocus);
        Assert.Equal(Color.Parse("#5C6572"), list.BaseRow!.LabelColor);

        var shot = app.Capture();
        Assert.Equal(Color.Parse("#DDE1FA"), shot.PixelAt(900, 169));
        Assert.Equal(Color.Parse("#4353D8"), shot.PixelAt(272, 178));
        Assert.Equal(Color.Parse("#4353D8"), shot.PixelAt((int)list.StartBounds.X + 3, (int)list.StartBounds.Center.Y));
        Assert.Equal(Colors.White, ((ISolidColorBrush)((Button)app.FindByAutomationId("RebaseStartButton")).Foreground!).Color);

        // D18 step 4: Edit… on Three's second line; Cancel leaves the list as it was.
        app.ClickRebaseEditMessage("Three");
        var dialog = app.MessageDialog();
        TestPictures.Save(app, "rebase-list-message-dialog-light");
        Assert.Equal(("Message of the squashed commit", "Three", "67", "#42 is the issue\n\nFour"), (dialog.Title, dialog.Summary, dialog.Counter, dialog.Description));
        app.Click(app.FindByAutomationId("CancelButton"));
        Assert.Null(app.TopDialogView);
        Assert.Equal("Four", app.RebaseList().Selected!.Subject);
        Assert.Equal("Message: Three", app.RebaseList().Row("Three").SecondLine);
    }
}
