using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;
using Xunit;
using static VisualCommit.App.Tests.InteractiveList;

namespace VisualCommit.App.Tests;

/// <summary>
/// The interactive rebase's list without a UI (docs/phase-4-design.md 2.1 to 2.5, 7.2, 10.4, 14.3),
/// with a recording fake repository (<see cref="RecordingRebaseHost"/>): how it opens, every action
/// from the menu and git's letters, squash and fixup joining the nearest Pick or Reword below with
/// their refusals, reordering, "changed", Start's plan for each case of 2.5 (compared with the todo
/// <see cref="RebaseTodo"/> writes from it), the questions of Cancel and Esc, and the details.
/// </summary>
public class InteractiveRebaseViewModelTests
{
    private static (InteractiveRebaseViewModel List, RecordingRebaseHost Host) Open(
        RebaseList? list = null,
        InteractiveRebaseRequest? request = null)
    {
        var host = new RecordingRebaseHost();
        return (new InteractiveRebaseViewModel(request ?? FromHere(), list ?? FromBase(), "Base", host), host);
    }

    private static InteractiveRebaseRowViewModel Row(InteractiveRebaseViewModel list, string subject) =>
        list.Rows.Single(row => row.Subject == subject);

    private static IEnumerable<string> Subjects(InteractiveRebaseViewModel list) => list.Rows.Select(row => row.Subject);

    private static IReadOnlyList<string> Lines(string todo) => todo.TrimEnd('\n').Split('\n');

    private static string Pick(string sha) => "pick " + sha;

    [Fact]
    public void It_opens_newest_on_top_with_the_base_row_the_top_row_selected_and_its_details_shown()
    {
        var (list, host) = Open();

        Assert.Equal(["Five", "Four", "Three", "Fix one", "Two", "One"], Subjects(list));
        Assert.All(list.Rows, row => Assert.Equal((RebaseAction.Pick, "Pick", 0.0, null), (row.Action, row.ActionText, row.Indent, row.SecondLine)));
        Assert.Equal("57f089d Five", list.Rows[0].Name);
        Assert.Equal(("4b4622c", "Base", "Base 4b4622c Base"), (list.BaseRow.ShortSha, list.BaseRow.Subject, list.BaseRow.Name));
        Assert.Equal([.. list.Rows, list.BaseRow], list.Items);
        Assert.Equal(("Interactive rebase of main from 4b4622c", "6 commits", "main"), (list.HeaderText, list.CountText, list.BranchName));
        Assert.Null(list.Message);
        Assert.Same(list.Rows[0], list.SelectedRow);
        Assert.True(list.Rows[0].IsSelected);
        Assert.Equal([Five], host.Details);
        Assert.False(list.IsChanged);
        Assert.False(list.CanStart);
        Assert.False(list.StartCommand.CanExecute(null));
        Assert.Equal("Change an action or the order first", list.StartToolTip);
        Assert.True(list.CancelCommand.CanExecute(null));
    }

    [Fact]
    public void The_header_names_the_target_and_the_count_says_one_commit_in_the_singular()
    {
        var (onto, _) = Open(Made(1), new InteractiveRebaseRequest(new RebaseTarget(BaseSha, "origin/main"), "refs/heads/feature/topic", FromHere: false, CheckoutFirst: true));

        Assert.Equal(("Interactive rebase of feature/topic onto origin/main", "1 commit"), (onto.HeaderText, onto.CountText));
    }

    [Theory]
    [InlineData(3, 1, "A merge lies between 4b4622c and main: interactive rebase would flatten it.")]
    [InlineData(0, 0, "main has no commits of its own after 4b4622c.")]
    [InlineData(1001, 0, "1001 commits are too many for one interactive rebase.")]
    public void A_list_that_cannot_be_made_says_why_in_place_of_the_rows_with_only_Cancel(int commits, int merges, string message)
    {
        var (list, host) = Open(Made(commits, merges));

        Assert.Equal(message, list.Message);
        Assert.True(list.HasMessage);
        Assert.Empty(list.Rows);
        Assert.Empty(list.Items);
        Assert.Null(list.SelectedRow);
        Assert.Empty(host.Details);
        Assert.False(list.CanStart);
        Assert.Null(list.StartToolTip);
        Assert.True(list.CancelCommand.CanExecute(null));
    }

    [Fact]
    public void A_thousand_commits_are_one_list()
    {
        var (list, _) = Open(Made(1000));

        Assert.Null(list.Message);
        Assert.Equal(1000, list.Rows.Count);
        Assert.Equal("1000 commits", list.CountText);
    }

    [Fact]
    public void The_row_menu_has_the_header_and_every_action_but_the_rows_own_with_Squash_and_Fixup_disabled_on_the_bottom_row()
    {
        var (list, _) = Open();

        var three = list.MenuFor(Row(list, "Three"));
        Assert.Equal("ff42e7f Three", three.HeaderText);
        Assert.Equal(
            [("Reword…", true, null), ("Squash", true, null), ("Fixup", true, null), ("Drop", true, null)],
            three.Items.Select(item => (item.Label, item.IsEnabled, item.DisabledReason)));
        Assert.All(three.Items, item => Assert.False(item.IsDestructive));
        Assert.Single(three.Entries.OfType<MenuHeader>());
        Assert.Empty(three.Entries.OfType<MenuSeparator>());

        var one = list.MenuFor(Row(list, "One"));
        Assert.Equal("fc6c8fd One", one.HeaderText);
        Assert.Equal(
            [
                ("Reword…", true, null),
                ("Squash", false, "The oldest commit has nothing below it to join"),
                ("Fixup", false, "The oldest commit has nothing below it to join"),
                ("Drop", true, (string?)null),
            ],
            one.Items.Select(item => (item.Label, item.IsEnabled, item.DisabledReason)));
    }

    [Fact]
    public async Task A_menu_item_sets_the_action_and_the_menu_then_lists_Pick_in_its_place()
    {
        var (list, _) = Open();
        var four = Row(list, "Four");

        list.MenuFor(four).Items.Single(item => item.Label == "Drop").Invoke();
        await Task.Yield();

        Assert.Equal(RebaseAction.Drop, four.Action);
        Assert.True(four.IsDropped);
        Assert.Equal(["Pick", "Reword…", "Squash", "Fixup"], list.MenuFor(four).Items.Select(item => item.Label));
        Assert.True(list.IsChanged);
        Assert.True(list.CanStart);
        Assert.Null(list.StartToolTip);
    }

    [Fact]
    public async Task Gits_letters_set_the_selected_rows_action_and_a_letter_that_cannot_apply_does_nothing()
    {
        var (list, host) = Open();

        Assert.True(await list.ApplyLetterAsync('d'));
        Assert.Equal(RebaseAction.Drop, list.Rows[0].Action);
        Assert.True(await list.ApplyLetterAsync('p'));
        Assert.Equal(RebaseAction.Pick, list.Rows[0].Action);
        Assert.False(await list.ApplyLetterAsync('p'));
        Assert.True(await list.ApplyLetterAsync('f'));
        Assert.Equal((RebaseAction.Fixup, "Four"), (list.Rows[0].Action, list.Rows[0].JoinTarget!.Subject));
        Assert.True(await list.ApplyLetterAsync('s'));
        Assert.Equal(RebaseAction.Squash, list.Rows[0].Action);
        Assert.False(await list.ApplyLetterAsync('x'));

        // On the bottom row a squash or fixup cannot apply: nothing changes.
        list.SelectLast();
        Assert.False(await list.ApplyLetterAsync('s'));
        Assert.False(await list.ApplyLetterAsync('f'));
        Assert.Equal(RebaseAction.Pick, Row(list, "One").Action);

        // r opens the message dialog; Cancel there leaves the action as it was.
        Assert.False(await list.ApplyLetterAsync('r'));
        Assert.Equal(RebaseAction.Pick, Row(list, "One").Action);
        Assert.Equal([("Reword fc6c8fd", "One\n", "Save")], host.MessageRequests);
    }

    [Fact]
    public async Task Reword_asks_for_the_new_message_and_Edit_changes_it_while_Cancel_keeps_the_action()
    {
        var (list, host) = Open();
        var two = Row(list, "Two");

        Assert.False(await list.SetActionAsync(two, RebaseAction.Reword));
        Assert.Equal((RebaseAction.Pick, null, false), (two.Action, two.SecondLine, list.IsChanged));

        host.MessageAnswers.Enqueue("Two reworded");
        Assert.True(await list.SetActionAsync(two, RebaseAction.Reword));
        Assert.Equal((RebaseAction.Reword, "Reword", "Two reworded", "New message: Two reworded"), (two.Action, two.ActionText, two.NewMessage, two.SecondLine));
        Assert.True(list.IsChanged);

        host.MessageAnswers.Enqueue("Two, said otherwise\n\nWith a body.");
        Assert.True(await list.EditMessageAsync(two));
        Assert.Equal("New message: Two, said otherwise", two.SecondLine);
        Assert.False(await list.EditMessageAsync(two));
        Assert.Equal("Two, said otherwise\n\nWith a body.", two.NewMessage);
        Assert.Equal(
            [("Reword 938844a", "Two\n", "Save"), ("Reword 938844a", "Two\n", "Save"), ("Reword 938844a", "Two reworded", "Save"), ("Reword 938844a", "Two, said otherwise\n\nWith a body.", "Save")],
            host.MessageRequests);

        // Back to Pick: the new message goes, and so does the second line.
        await list.SetActionAsync(two, RebaseAction.Pick);
        Assert.Equal((null, null, false), (two.NewMessage, two.SecondLine, list.IsChanged));
    }

    [Fact]
    public async Task Squash_and_fixup_join_the_nearest_Pick_or_Reword_below_passing_over_dropped_and_joined_rows()
    {
        var (list, _) = Open();
        var five = Row(list, "Five");
        var four = Row(list, "Four");
        var three = Row(list, "Three");
        var fixOne = Row(list, "Fix one");
        var two = Row(list, "Two");

        await list.SetActionAsync(three, RebaseAction.Drop);
        await list.SetActionAsync(fixOne, RebaseAction.Fixup);
        await list.SetActionAsync(four, RebaseAction.Squash);
        await list.SetActionAsync(five, RebaseAction.Fixup);

        // Five and Four pass over the dropped Three and the joined Fix one to Two.
        Assert.Equal([two, two, null, two, null, null], list.Rows.Select(row => row.JoinTarget));
        Assert.Equal([16.0, 16, 0, 16, 0, 0], list.Rows.Select(row => row.Indent));
        Assert.Equal(
            [RebaseJoinLine.FromCentre, RebaseJoinLine.Through, RebaseJoinLine.Through, RebaseJoinLine.Through, RebaseJoinLine.None, RebaseJoinLine.None],
            list.Rows.Select(row => row.JoinLine));
        Assert.Equal([fixOne, four, five], list.MembersOf(two));
        Assert.True(two.IsSquashTarget);
        Assert.Equal("Message: Two", two.SecondLine);
        Assert.Equal("Two\n\nFour\n", list.GroupMessage(two));
        Assert.All(list.Rows.Where(row => !ReferenceEquals(row, two)), row => Assert.Null(row.SecondLine));
    }

    [Fact]
    public async Task A_group_of_fixups_only_keeps_its_targets_message_and_has_no_second_line()
    {
        var (list, _) = Open();
        var fixOne = Row(list, "Fix one");
        var two = Row(list, "Two");

        await list.SetActionAsync(fixOne, RebaseAction.Fixup);

        Assert.Same(two, fixOne.JoinTarget);
        Assert.Equal((false, null), (two.IsSquashTarget, two.SecondLine));
        Assert.Equal("Two\n", list.GroupMessage(two));
        Assert.Equal(RebaseJoinLine.FromCentre, fixOne.JoinLine);
        Assert.Equal(RebaseJoinLine.None, two.JoinLine);
    }

    [Fact]
    public async Task A_squash_or_fixup_row_with_no_target_below_turns_into_Pick()
    {
        var (list, _) = Open();
        var two = Row(list, "Two");
        var one = Row(list, "One");

        // Two joins One; dropping One leaves Two with nothing to join.
        await list.SetActionAsync(two, RebaseAction.Fixup);
        await list.SetActionAsync(one, RebaseAction.Drop);
        Assert.Equal((RebaseAction.Pick, null, 0.0), (two.Action, two.JoinTarget, two.Indent));

        // With every row below it dropped, Squash and Fixup are disabled with their reason.
        await list.SetActionAsync(two, RebaseAction.Drop);
        var fixOne = Row(list, "Fix one");
        Assert.Equal(
            [("Squash", false, "Every commit below it is dropped: there is nothing to join"), ("Fixup", false, "Every commit below it is dropped: there is nothing to join")],
            list.MenuFor(fixOne).Items.Where(item => item.Label is "Squash" or "Fixup").Select(item => (item.Label, item.IsEnabled, item.DisabledReason)));
        Assert.False(await list.SetActionAsync(fixOne, RebaseAction.Squash));

        // A fixup row moved to the bottom has nothing below it either.
        var (other, _) = Open();
        var three = Row(other, "Three");
        await other.SetActionAsync(three, RebaseAction.Fixup);
        Assert.True(other.MoveRow(three, other.Rows.Count - 1));
        Assert.Equal((RebaseAction.Pick, null), (three.Action, three.JoinTarget));
    }

    [Fact]
    public async Task A_groups_message_starts_as_gits_own_is_edited_and_is_kept_while_the_group_keeps_its_target()
    {
        var (list, host) = Open();
        var five = Row(list, "Five");
        var four = Row(list, "Four");
        var three = Row(list, "Three");

        await list.SetActionAsync(four, RebaseAction.Squash);
        Assert.Equal("Three\n\n#42 is the issue\n\nFour\n", list.GroupMessage(three));
        Assert.Equal("Message: Three", three.SecondLine);

        host.MessageAnswers.Enqueue("Three and four\n\n#42 is the issue");
        Assert.True(await list.EditMessageAsync(three));
        Assert.Equal(("Message of the squashed commit", "Three\n\n#42 is the issue\n\nFour\n", "Save"), host.MessageRequests[^1]);
        Assert.Equal("Message: Three and four", three.SecondLine);

        // Another squash joining keeps the edited message: the group keeps its target.
        await list.SetActionAsync(five, RebaseAction.Squash);
        Assert.Equal("Three and four\n\n#42 is the issue", list.GroupMessage(three));

        // Cancel in the dialog keeps it too.
        Assert.False(await list.EditMessageAsync(three));
        Assert.Equal("Three and four\n\n#42 is the issue", list.GroupMessage(three));

        // Once no squash joins Three, its edited message goes.
        await list.SetActionAsync(five, RebaseAction.Pick);
        await list.SetActionAsync(four, RebaseAction.Fixup);
        Assert.Equal((false, null, null), (three.IsSquashTarget, three.EditedGroupMessage, three.SecondLine));
        await list.SetActionAsync(four, RebaseAction.Squash);
        Assert.Equal("Three\n\n#42 is the issue\n\nFour\n", list.GroupMessage(three));
    }

    [Fact]
    public async Task A_reworded_targets_group_starts_from_its_new_message()
    {
        var (list, host) = Open();
        var two = Row(list, "Two");
        var fixOne = Row(list, "Fix one");

        host.MessageAnswers.Enqueue("Two reworded");
        await list.SetActionAsync(two, RebaseAction.Reword);
        await list.SetActionAsync(fixOne, RebaseAction.Squash);

        Assert.Equal("Two reworded\n\nFix one\n", list.GroupMessage(two));
        Assert.Equal("Message: Two reworded", two.SecondLine);
        Assert.Equal("Reword", two.ActionText);
    }

    [Fact]
    public void Rows_move_by_a_drop_into_a_gap_and_by_Alt_arrows_and_a_list_moved_back_is_not_changed()
    {
        var (list, _) = Open();
        var fixOne = Row(list, "Fix one");
        var five = Row(list, "Five");

        // B7: Fix one dropped into the gap between Two and One (gap 5).
        Assert.True(list.DropRow(fixOne, 5));
        Assert.Equal(["Five", "Four", "Three", "Two", "Fix one", "One"], Subjects(list));
        Assert.Equal([.. list.Rows, list.BaseRow], list.Items);
        Assert.True(list.IsChanged);

        // The gaps right above and under a row leave it where it is; a drop below the base row
        // lands right above it.
        Assert.False(list.DropRow(fixOne, 4));
        Assert.False(list.DropRow(fixOne, 5));
        Assert.True(list.DropRow(fixOne, 99));
        Assert.Equal(["Five", "Four", "Three", "Two", "One", "Fix one"], Subjects(list));
        Assert.True(list.DropRow(fixOne, 3));
        Assert.Equal(["Five", "Four", "Three", "Fix one", "Two", "One"], Subjects(list));
        Assert.False(list.IsChanged);

        // Alt+Down and Alt+Up move the selected row; it stays selected.
        Assert.Same(five, list.SelectedRow);
        Assert.False(list.MoveSelectedRow(-1));
        Assert.True(list.MoveSelectedRow(1));
        Assert.Equal(["Four", "Five", "Three", "Fix one", "Two", "One"], Subjects(list));
        Assert.Same(five, list.SelectedRow);
        Assert.True(list.IsChanged);
        Assert.True(list.MoveSelectedRow(-1));
        Assert.False(list.IsChanged);

        list.SelectLast();
        Assert.False(list.MoveSelectedRow(1));
    }

    [Fact]
    public async Task Changed_means_the_actions_or_order_differ_from_the_first_state()
    {
        var (list, _) = Open();
        var four = Row(list, "Four");

        await list.SetActionAsync(four, RebaseAction.Drop);
        Assert.True(list.IsChanged);
        await list.SetActionAsync(four, RebaseAction.Pick);
        Assert.False(list.IsChanged);
        Assert.Equal("Change an action or the order first", list.StartToolTip);
    }

    [Fact]
    public void The_selection_moves_with_Up_Down_Home_and_End_and_each_new_row_is_shown_in_the_right_panel()
    {
        var (list, host) = Open();

        Assert.False(list.MoveSelection(-1));
        Assert.True(list.MoveSelection(1));
        Assert.True(list.MoveSelection(1));
        Assert.Equal("Three", list.SelectedRow!.Subject);
        Assert.True(list.SelectLast());
        Assert.Equal("One", list.SelectedRow!.Subject);
        Assert.False(list.MoveSelection(1));
        Assert.False(list.SelectLast());
        Assert.True(list.SelectFirst());
        list.Select(Row(list, "Two"));

        Assert.Equal([Five, Four, Three, One, Five, Two], host.Details);
        Assert.Single(list.Rows, row => row.IsSelected);
        Assert.True(Row(list, "Two").IsSelected);
    }

    [Fact]
    public async Task Start_hands_over_B7s_plan_and_the_todo_git_gets_from_it_is_the_one_the_report_derived()
    {
        var (list, host) = Open();

        Assert.True(list.DropRow(Row(list, "Fix one"), 5));
        await list.SetActionAsync(Row(list, "Fix one"), RebaseAction.Fixup);
        await list.SetActionAsync(Row(list, "Four"), RebaseAction.Squash);
        host.MessageAnswers.Enqueue("Three and four\n\n#42 is the issue");
        await list.EditMessageAsync(Row(list, "Three"));
        host.MessageAnswers.Enqueue("Two reworded");
        await list.SetActionAsync(Row(list, "Two"), RebaseAction.Reword);
        await list.SetActionAsync(Row(list, "Five"), RebaseAction.Drop);
        Assert.True(list.CanStart);

        await list.StartCommand.ExecuteAsync(null);

        var plan = Assert.Single(host.Plans);
        Assert.Equal((BaseSha, Five, "refs/heads/main"), (plan.Onto, plan.ExpectedTip, plan.BranchRef));
        Assert.Equal(
            [
                new RebaseStep(One, RebaseAction.Pick, null),
                new RebaseStep(FixOne, RebaseAction.Fixup, null),
                new RebaseStep(Two, RebaseAction.Reword, "Two reworded"),
                new RebaseStep(Three, RebaseAction.Pick, "Three and four\n\n#42 is the issue"),
                new RebaseStep(Four, RebaseAction.Squash, null),
                new RebaseStep(Five, RebaseAction.Drop, null),
            ],
            plan.StepsOldestFirst);

        var files = RebaseTodo.Build(plan, OwnMessages(FromBase()));
        Assert.Equal(
            [
                "label vc-1", Pick(One), "fixup " + FixOne, RebaseTodo.GuardedAmend(1),
                "label vc-2", Pick(Two), RebaseTodo.GuardedAmend(2),
                "label vc-3", Pick(Three), "fixup " + Four, RebaseTodo.GuardedAmend(3),
                "drop " + Five,
            ],
            Lines(files.Todo));
        Assert.Equal(
            [("vc-message-1", "One\n"), ("vc-message-2", "Two reworded"), ("vc-message-3", "Three and four\n\n#42 is the issue")],
            files.Messages.Select(file => (file.FileName, file.Text)));

        // The rebase ended (done or stopped): the list closes.
        Assert.Equal(1, host.Closed);
        Assert.True(list.IsClosed);
        Assert.False(list.CancelCommand.CanExecute(null));
    }

    [Fact]
    public async Task Plans_for_each_case_of_the_todo_reorder_drop_reword_squash_fixup_and_a_dropped_row_inside_a_group()
    {
        var own = OwnMessages(FromBase());

        // Picks in a new order: plain picks, no labels.
        var (reordered, h1) = Open();
        reordered.MoveRow(Row(reordered, "One"), 0);
        await reordered.StartCommand.ExecuteAsync(null);
        Assert.Equal([Pick(Two), Pick(FixOne), Pick(Three), Pick(Four), Pick(Five), Pick(One)], Lines(RebaseTodo.Build(h1.Plans[0], own).Todo));

        // A group with an unedited squash: no message in the plan, git's own squash message of the
        // commits' exact messages in the file.
        var (squash, h2) = Open();
        await squash.SetActionAsync(Row(squash, "Four"), RebaseAction.Squash);
        await squash.StartCommand.ExecuteAsync(null);
        Assert.Contains(new RebaseStep(Three, RebaseAction.Pick, null), h2.Plans[0].StepsOldestFirst);
        var squashFiles = RebaseTodo.Build(h2.Plans[0], own);
        Assert.Equal(
            [Pick(One), Pick(Two), Pick(FixOne), "label vc-1", Pick(Three), "fixup " + Four, RebaseTodo.GuardedAmend(1), Pick(Five)],
            Lines(squashFiles.Todo));
        Assert.Equal("Three\n\n#42 is the issue\n\nFour\n", squashFiles.Messages.Single().Text);

        // A group of fixups only under a reworded target: the target's new message.
        var (rewordFixup, h3) = Open();
        h3.MessageAnswers.Enqueue("Fix one reworded");
        await rewordFixup.SetActionAsync(Row(rewordFixup, "Fix one"), RebaseAction.Reword);
        await rewordFixup.SetActionAsync(Row(rewordFixup, "Three"), RebaseAction.Fixup);
        await rewordFixup.StartCommand.ExecuteAsync(null);
        Assert.Contains(new RebaseStep(FixOne, RebaseAction.Reword, "Fix one reworded"), h3.Plans[0].StepsOldestFirst);
        var rewordFiles = RebaseTodo.Build(h3.Plans[0], own);
        Assert.Equal(
            [Pick(One), Pick(Two), "label vc-1", Pick(FixOne), "fixup " + Three, RebaseTodo.GuardedAmend(1), Pick(Four), Pick(Five)],
            Lines(rewordFiles.Todo));
        Assert.Equal("Fix one reworded", rewordFiles.Messages.Single().Text);

        // A reworded target with a squash: the group's message starts from the new message.
        var (rewordSquash, h4) = Open();
        h4.MessageAnswers.Enqueue("Two reworded");
        await rewordSquash.SetActionAsync(Row(rewordSquash, "Two"), RebaseAction.Reword);
        await rewordSquash.SetActionAsync(Row(rewordSquash, "Fix one"), RebaseAction.Squash);
        await rewordSquash.StartCommand.ExecuteAsync(null);
        Assert.Contains(new RebaseStep(Two, RebaseAction.Reword, "Two reworded\n\nFix one\n"), h4.Plans[0].StepsOldestFirst);
        Assert.Equal("Two reworded\n\nFix one\n", RebaseTodo.Build(h4.Plans[0], own).Messages.Single().Text);

        // A dropped row between a fixup and its target: written after the group, outside it.
        var (dropInside, h5) = Open();
        await dropInside.SetActionAsync(Row(dropInside, "Three"), RebaseAction.Drop);
        await dropInside.SetActionAsync(Row(dropInside, "Four"), RebaseAction.Fixup);
        Assert.Equal(RebaseJoinLine.Through, Row(dropInside, "Three").JoinLine);
        Assert.Equal(0, Row(dropInside, "Three").Indent);
        await dropInside.StartCommand.ExecuteAsync(null);
        Assert.Equal(
            [Pick(One), Pick(Two), "label vc-1", Pick(FixOne), "fixup " + Four, RebaseTodo.GuardedAmend(1), "drop " + Three, Pick(Five)],
            Lines(RebaseTodo.Build(h5.Plans[0], own).Todo));
        Assert.Equal("Fix one\n", RebaseTodo.Build(h5.Plans[0], own).Messages.Single().Text);

        // Every row dropped: only drop lines.
        var (allDropped, h6) = Open();
        foreach (var row in allDropped.Rows)
        {
            await allDropped.SetActionAsync(row, RebaseAction.Drop);
        }

        await allDropped.StartCommand.ExecuteAsync(null);
        Assert.All(Lines(RebaseTodo.Build(h6.Plans[0], own).Todo), line => Assert.StartsWith("drop ", line, StringComparison.Ordinal));
    }

    [Fact]
    public async Task While_Start_runs_the_list_takes_no_input_and_when_git_refused_it_stays_open_as_it_was()
    {
        var (list, host) = Open();
        await list.SetActionAsync(Row(list, "Five"), RebaseAction.Drop);
        host.HeldStart = new TaskCompletionSource<bool>();

        var starting = list.StartCommand.ExecuteAsync(null);
        Assert.True(list.IsBusy);
        Assert.False(list.IsInteractive);
        Assert.False(list.CanStart);
        Assert.False(list.CancelCommand.CanExecute(null));
        Assert.False(await list.SetActionAsync(Row(list, "Four"), RebaseAction.Drop));
        Assert.False(list.MoveSelectedRow(1));
        Assert.False(list.MoveSelection(1));

        host.HeldStart.SetResult(false);
        await starting;

        Assert.False(list.IsBusy);
        Assert.False(list.IsClosed);
        Assert.Equal(0, host.Closed);
        Assert.Equal(RebaseAction.Drop, Row(list, "Five").Action);
        Assert.True(list.CanStart);
    }

    [Fact]
    public async Task While_Start_waits_or_runs_the_list_cannot_be_left_asks_nothing_and_closes_itself_when_the_rebase_ends()
    {
        var (list, host) = Open();
        await list.SetActionAsync(Row(list, "Five"), RebaseAction.Drop);
        host.HeldStart = new TaskCompletionSource<bool>();
        var starting = list.StartCommand.ExecuteAsync(null);

        // Another list, a diff or the resolver asks through TryLeaveAsync; Cancel and Esc too,
        // past their disabled state. A Discard is queued that must never be asked for.
        host.ConfirmAnswers.Enqueue(true);
        Assert.False(await list.TryLeaveAsync());
        await list.CancelCommand.ExecuteAsync(null);
        Assert.Empty(host.Questions);
        Assert.Equal(0, host.Closed);
        Assert.False(list.IsClosed);
        Assert.True(list.IsBusy);

        // The rebase ends: the list closes itself.
        host.HeldStart.SetResult(true);
        await starting;
        Assert.True(list.IsClosed);
        Assert.Equal(1, host.Closed);
        Assert.Empty(host.Questions);
        Assert.True(await list.TryLeaveAsync());
    }

    [Fact]
    public async Task Cancel_closes_an_unchanged_list_at_once_and_asks_first_on_a_changed_one()
    {
        var (unchanged, quiet) = Open();
        await unchanged.CancelCommand.ExecuteAsync(null);
        Assert.Empty(quiet.Questions);
        Assert.Equal(1, quiet.Closed);
        Assert.True(unchanged.IsClosed);

        var (list, host) = Open();
        await list.SetActionAsync(Row(list, "Five"), RebaseAction.Drop);

        // Keep editing: the list stays as it is.
        host.ConfirmAnswers.Enqueue(false);
        await list.CancelCommand.ExecuteAsync(null);
        var question = Assert.Single(host.Questions);
        Assert.Equal(("Discard the changes to this list?", "", "", "Discard", true, "Keep editing"),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive, question.CancelText));
        Assert.Equal(0, host.Closed);
        Assert.False(list.IsClosed);

        // Discard: the list closes, and nothing was started.
        host.ConfirmAnswers.Enqueue(true);
        await list.CancelCommand.ExecuteAsync(null);
        Assert.Equal(1, host.Closed);
        Assert.Empty(host.Plans);
    }

    [Fact]
    public async Task Leaving_for_something_else_asks_only_for_a_changed_list_and_closes_nothing_itself()
    {
        var (list, host) = Open();
        Assert.True(await list.TryLeaveAsync());
        Assert.Empty(host.Questions);

        await list.SetActionAsync(Row(list, "Five"), RebaseAction.Drop);
        host.ConfirmAnswers.Enqueue(false);
        Assert.False(await list.TryLeaveAsync());
        host.ConfirmAnswers.Enqueue(true);
        Assert.True(await list.TryLeaveAsync());

        Assert.Equal(2, host.Questions.Count);
        Assert.Equal(0, host.Closed);
        Assert.False(list.IsClosed);
    }

    [Fact]
    public async Task Start_is_disabled_once_HEAD_left_the_branch_or_its_tip_moved_with_the_reasons_in_the_designs_order()
    {
        var (list, _) = Open();
        var head = new HeadState("main", Five);
        RepoRefs Refs(HeadState state, string tip) => new(state, [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, tip, IsHead: state.BranchName == "main")], [], []);

        list.ObserveRefs(Refs(head, Five));
        Assert.False(list.IsHeadMoved);

        // Unchanged and HEAD moved: the first reason of the design's order.
        const string Six = "c3cc217816ade59e0b8ccf262d83050f491ba812";
        list.ObserveRefs(Refs(new HeadState("main", Six), Six));
        Assert.True(list.IsHeadMoved);
        Assert.Equal("Change an action or the order first", list.StartToolTip);

        await list.ApplyLetterAsync('d');
        Assert.False(list.CanStart);
        Assert.Equal("HEAD moved: open the list again", list.StartToolTip);

        // Back on main at the tip it was read from; then detached at it.
        list.ObserveRefs(Refs(head, Five));
        Assert.True(list.CanStart);
        list.ObserveRefs(Refs(new HeadState(null, Five), Five));
        Assert.True(list.IsHeadMoved);

        // A branch that Start checks out first: only its tip counts, wherever HEAD is.
        var (drop, _) = Open(request: new InteractiveRebaseRequest(new RebaseTarget(BaseSha, "4b4622c"), "refs/heads/main", FromHere: false, CheckoutFirst: true));
        drop.ObserveRefs(Refs(new HeadState("other", BaseSha), Five));
        Assert.False(drop.IsHeadMoved);
        drop.ObserveRefs(Refs(new HeadState("other", BaseSha), Six));
        Assert.True(drop.IsHeadMoved);
    }

    [Fact]
    public async Task Once_closed_the_list_takes_no_more_input()
    {
        var (list, host) = Open();
        await list.CancelCommand.ExecuteAsync(null);

        Assert.False(await list.SetActionAsync(Row(list, "Five"), RebaseAction.Drop));
        Assert.False(list.MoveSelection(1));
        Assert.False(list.MoveSelectedRow(1));
        Assert.True(await list.TryLeaveAsync());
        Assert.Equal(1, host.Closed);
    }
}
