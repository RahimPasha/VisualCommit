using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The todo and message files the app hands git (<see cref="RebaseTodo"/>, docs/phase-4-design.md
/// 2.5), character for character: every group's shape, the guarded amend, explicit drops, LF
/// line ends, no comments and no byte order mark.
/// </summary>
public class RebaseTodoTests
{
    private static readonly string A = new('a', 40);
    private static readonly string B = new('b', 40);
    private static readonly string C = new('c', 40);
    private static readonly string D = new('d', 40);
    private static readonly string E = new('e', 40);
    private static readonly string Onto = new('0', 40);
    private const string Branch = "refs/heads/topic";

    /// <summary>The guarded amend of group 1 as the design writes it (2.5), character for character.</summary>
    private const string GuardOne =
        "exec if l=$(git rev-parse -q --verify refs/rewritten/vc-1) && [ -n \"$l\" ] && [ \"$(git rev-parse -q --verify HEAD^)\" = \"$l\" ]; "
        + "then git commit --amend --only --allow-empty --cleanup=whitespace --quiet --file=\"$(git rev-parse --git-path rebase-merge/vc-message-1)\"; fi";

    [Fact]
    public void The_guarded_amend_is_the_designs_line()
    {
        Assert.Equal(GuardOne, RebaseTodo.GuardedAmend(1));
        Assert.Equal(GuardOne.Replace("vc-1)", "vc-12)", StringComparison.Ordinal).Replace("vc-message-1)", "vc-message-12)", StringComparison.Ordinal), RebaseTodo.GuardedAmend(12));
        Assert.Equal("vc-3", RebaseTodo.Label(3));
        Assert.Equal("vc-message-3", RebaseTodo.MessageFileName(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => RebaseTodo.GuardedAmend(0));
    }

    [Fact]
    public void Picks_and_drops_are_one_line_each_with_full_ids_and_no_message_file()
    {
        var files = RebaseTodo.Build(Plan(Step(B), Step(A, RebaseAction.Drop), Step(C)));

        Assert.Equal($"pick {B}\ndrop {A}\npick {C}\n", files.Todo);
        Assert.Empty(files.Messages);
    }

    [Fact]
    public void A_reword_is_a_group_without_members()
    {
        var files = RebaseTodo.Build(Plan(Step(A), Step(B, RebaseAction.Reword, "New subject\r\n\r\n#7 kept\r\n"), Step(C)));

        Assert.Equal($"pick {A}\nlabel vc-1\npick {B}\n{GuardOne}\npick {C}\n", files.Todo);
        Assert.Equal([new RebaseMessageFile("vc-message-1", "New subject\n\n#7 kept\n")], files.Messages);
    }

    [Fact]
    public void A_squash_group_joins_its_members_as_fixups_and_takes_the_groups_message()
    {
        // Oldest first: A is the target, B squashes into it, D (dropped) lies between, C fixes it up.
        var plan = Plan(Step(A, RebaseAction.Pick, "Group message\n\n#3 kept\n"), Step(B, RebaseAction.Squash), Step(D, RebaseAction.Drop), Step(C, RebaseAction.Fixup), Step(E));

        var files = RebaseTodo.Build(plan);

        Assert.Equal($"label vc-1\npick {A}\nfixup {B}\nfixup {C}\n{GuardOne}\ndrop {D}\npick {E}\n", files.Todo);
        Assert.Equal([new RebaseMessageFile("vc-message-1", "Group message\n\n#3 kept\n")], files.Messages);
        Assert.Empty(RebaseTodo.OwnMessagesNeeded(plan));
    }

    [Fact]
    public void A_group_of_fixups_only_keeps_the_targets_exact_own_message()
    {
        var plan = Plan(Step(A), Step(B, RebaseAction.Fixup));
        const string Own = "Target subject\n\n#12 issue line\n   indented  \n";

        Assert.Equal([A], RebaseTodo.OwnMessagesNeeded(plan));
        var files = RebaseTodo.Build(plan, new Dictionary<string, string> { [A] = Own });

        Assert.Equal($"label vc-1\npick {A}\nfixup {B}\n{GuardOne}\n", files.Todo);
        Assert.Equal([new RebaseMessageFile("vc-message-1", Own)], files.Messages);
    }

    [Fact]
    public void A_reworded_target_of_fixups_keeps_its_new_message()
    {
        var plan = Plan(Step(A, RebaseAction.Reword, "Reworded\n"), Step(B, RebaseAction.Fixup));

        Assert.Empty(RebaseTodo.OwnMessagesNeeded(plan));
        Assert.Equal([new RebaseMessageFile("vc-message-1", "Reworded\n")], RebaseTodo.Build(plan).Messages);
    }

    [Fact]
    public void A_squash_group_without_a_message_gets_gits_squash_message_of_the_own_messages()
    {
        var plan = Plan(Step(A), Step(B, RebaseAction.Fixup), Step(C, RebaseAction.Squash));

        Assert.Equal([A, C], RebaseTodo.OwnMessagesNeeded(plan));
        var files = RebaseTodo.Build(plan, new Dictionary<string, string> { [A] = "First\n\n#1 body\n\n", [C] = "Third\n" });

        Assert.Equal([new RebaseMessageFile("vc-message-1", "First\n\n#1 body\n\nThird\n")], files.Messages);
    }

    [Fact]
    public void Groups_are_numbered_in_order_and_drops_before_every_target_come_first()
    {
        var plan = Plan(Step(D, RebaseAction.Drop), Step(A, RebaseAction.Reword, "One\n"), Step(B), Step(C, RebaseAction.Squash), Step(E, RebaseAction.Drop));

        var files = RebaseTodo.Build(plan with
        {
            StepsOldestFirst = [.. plan.StepsOldestFirst.Select(step => step.Sha == B ? step with { Message = "Two\n" } : step)],
        });

        var two = GuardOne.Replace("vc-1)", "vc-2)", StringComparison.Ordinal).Replace("vc-message-1)", "vc-message-2)", StringComparison.Ordinal);
        Assert.Equal($"drop {D}\nlabel vc-1\npick {A}\n{GuardOne}\nlabel vc-2\npick {B}\nfixup {C}\n{two}\ndrop {E}\n", files.Todo);
        Assert.Equal([new RebaseMessageFile("vc-message-1", "One\n"), new RebaseMessageFile("vc-message-2", "Two\n")], files.Messages);
    }

    [Fact]
    public void A_pick_that_nothing_joins_ignores_a_message()
    {
        var files = RebaseTodo.Build(Plan(Step(A, RebaseAction.Pick, "Stale\n"), Step(B)));

        Assert.Equal($"pick {A}\npick {B}\n", files.Todo);
        Assert.Empty(files.Messages);
    }

    [Fact]
    public void The_todo_has_no_comment_no_carriage_return_and_its_encoding_no_byte_order_mark()
    {
        var files = RebaseTodo.Build(Plan(Step(A), Step(B, RebaseAction.Squash), Step(C, RebaseAction.Reword, "x\r\ny")), new Dictionary<string, string> { [A] = "a\n", [B] = "b\n" });

        Assert.DoesNotContain('\r', files.Todo);
        Assert.All(files.Todo.Split('\n')[..^1], line => Assert.False(line.StartsWith('#') || line.Length == 0, line));
        Assert.EndsWith("\n", files.Todo, StringComparison.Ordinal);
        Assert.All(files.Messages, message => Assert.DoesNotContain('\r', message.Text));
        Assert.Empty(RebaseTodo.FileEncoding.GetPreamble());
    }

    [Theory]
    [InlineData("squash first")]
    [InlineData("fixup after a drop only")]
    [InlineData("short id")]
    [InlineData("id with a line feed")]
    [InlineData("twice")]
    [InlineData("empty")]
    public void A_plan_that_cannot_be_written_is_refused(string problem)
    {
        RebasePlan plan = problem switch
        {
            "squash first" => Plan(Step(A, RebaseAction.Squash), Step(B)),
            "fixup after a drop only" => Plan(Step(A, RebaseAction.Drop), Step(B, RebaseAction.Fixup)),
            "short id" => Plan(Step("abc1234")),
            "id with a line feed" => Plan(Step(A[..39] + "\n")),
            "twice" => Plan(Step(A), Step(A)),
            _ => Plan(),
        };

        Assert.Throws<ArgumentException>(() => RebaseTodo.Build(plan, new Dictionary<string, string> { [A] = "a\n", [B] = "b\n" }));
        Assert.Throws<ArgumentException>(() => RebaseTodo.OwnMessagesNeeded(plan));
    }

    [Fact]
    public void An_own_message_that_is_needed_and_not_given_is_refused() =>
        Assert.Throws<ArgumentException>(() => RebaseTodo.Build(Plan(Step(A), Step(B, RebaseAction.Fixup))));

    [Fact]
    public void Sha_256_ids_are_accepted()
    {
        var id = new string('f', 64);

        Assert.Equal($"pick {id}\n", RebaseTodo.Build(Plan(Step(id))).Todo);
    }

    [Fact]
    public void The_squash_message_joins_the_messages_by_a_blank_line_and_keeps_their_hash_lines()
    {
        Assert.Equal("One\n\n#1 kept\n\nTwo\n", RebaseTodo.SquashMessage(["One\r\n\r\n#1 kept\r\n\r\n", "Two"]));
        Assert.Equal(string.Empty, RebaseTodo.SquashMessage([]));
    }

    [Fact]
    public void The_squash_plan_joins_the_oldest_commits_of_the_list_and_picks_the_rest()
    {
        var list = new RebaseList(Onto, C, [Entry(A), Entry(B), Entry(C)], 0);

        var plan = RebaseTodo.SquashPlan(list, Branch, [B, A], "Squashed\n");

        Assert.Equal(new RebasePlan(Onto, C, Branch, plan.StepsOldestFirst), plan);
        Assert.Equal([new RebaseStep(A, RebaseAction.Pick, "Squashed\n"), new RebaseStep(B, RebaseAction.Squash, null), new RebaseStep(C, RebaseAction.Pick, null)], plan.StepsOldestFirst);
        Assert.Equal($"label vc-1\npick {A}\nfixup {B}\n{GuardOne}\npick {C}\n", RebaseTodo.Build(plan).Todo);
    }

    [Fact]
    public void A_squash_plan_of_commits_that_are_not_the_oldest_of_the_list_is_refused()
    {
        var list = new RebaseList(Onto, C, [Entry(A), Entry(B), Entry(C)], 0);

        Assert.Throws<ArgumentException>(() => RebaseTodo.SquashPlan(list, Branch, [B, C], "x\n"));
        Assert.Throws<ArgumentException>(() => RebaseTodo.SquashPlan(list, Branch, [A], "x\n"));
        Assert.Throws<ArgumentException>(() => RebaseTodo.SquashPlan(list, Branch, [A, A], "x\n"));
    }

    // ---- Keep ignored files: where git starts and what it applies (docs/phase-6-design.md 9.7) ----

    [Fact]
    public void Plain_picks_are_all_leading_and_all_applied()
    {
        var plan = Plan(Step(A), Step(B), Step(C));

        Assert.Equal($"pick {A}\npick {B}\npick {C}\n", RebaseTodo.Build(plan).Todo);
        Assert.Equal([A, B, C], RebaseTodo.LeadingPicks(plan));
        Assert.Equal([A, B, C], RebaseTodo.AppliedCommits(plan));
    }

    [Fact]
    public void A_reword_first_starts_with_its_label_so_no_pick_leads()
    {
        // T6's shape: label vc-1 before the first pick, which stops git's skipping.
        var plan = Plan(Step(A, RebaseAction.Reword, "New\n"), Step(B), Step(C));

        Assert.StartsWith($"label vc-1\npick {A}\n", RebaseTodo.Build(plan).Todo, StringComparison.Ordinal);
        Assert.Empty(RebaseTodo.LeadingPicks(plan));
        Assert.Equal([A, B, C], RebaseTodo.AppliedCommits(plan));
    }

    [Fact]
    public void A_reword_after_plain_picks_ends_the_leading_ones_before_its_label()
    {
        var plan = Plan(Step(A), Step(B), Step(C, RebaseAction.Reword, "New\n"));

        Assert.Equal([A, B], RebaseTodo.LeadingPicks(plan));
        Assert.Equal([A, B, C], RebaseTodo.AppliedCommits(plan));
    }

    [Fact]
    public void A_drop_first_leaves_no_leading_pick_and_is_not_applied()
    {
        var plan = Plan(Step(A, RebaseAction.Drop), Step(B), Step(C));

        Assert.Equal($"drop {A}\npick {B}\npick {C}\n", RebaseTodo.Build(plan).Todo);
        Assert.Empty(RebaseTodo.LeadingPicks(plan));
        Assert.Equal([B, C], RebaseTodo.AppliedCommits(plan));
    }

    [Fact]
    public void A_pick_with_a_drop_after_it_is_the_last_leading_pick()
    {
        // The drop line ends the run: the next pick's parent is the dropped commit.
        var plan = Plan(Step(A), Step(B, RebaseAction.Drop), Step(C), Step(D));

        Assert.Equal($"pick {A}\ndrop {B}\npick {C}\npick {D}\n", RebaseTodo.Build(plan).Todo);
        Assert.Equal([A], RebaseTodo.LeadingPicks(plan));
        Assert.Equal([A, C, D], RebaseTodo.AppliedCommits(plan));
    }

    [Fact]
    public void A_squash_group_applies_its_target_then_its_members_and_leads_with_nothing()
    {
        // A first, B squashed into it, D dropped inside the group, C a fixup, then E: the todo is
        // label, pick A, fixup B, fixup C, the guard, drop D, pick E (as Build writes it above).
        var plan = Plan(Step(A, RebaseAction.Pick, "Group\n"), Step(B, RebaseAction.Squash), Step(D, RebaseAction.Drop), Step(C, RebaseAction.Fixup), Step(E));

        Assert.Empty(RebaseTodo.LeadingPicks(plan));
        Assert.Equal([A, B, C, E], RebaseTodo.AppliedCommits(plan));

        // A squash's plan (section 3) changes its first group's message: nothing leads either.
        var squash = RebaseTodo.SquashPlan(new RebaseList(Onto, C, [Entry(A), Entry(B), Entry(C)], 0), Branch, [A, B], "Squashed\n");
        Assert.Empty(RebaseTodo.LeadingPicks(squash));
        Assert.Equal([A, B, C], RebaseTodo.AppliedCommits(squash));
    }

    [Fact]
    public void The_leading_picks_stop_at_a_group_and_a_plan_that_cannot_be_written_is_refused()
    {
        var plan = Plan(Step(A), Step(B), Step(C, RebaseAction.Fixup), Step(D));

        Assert.Equal([A], RebaseTodo.LeadingPicks(plan));
        Assert.Equal([A, B, C, D], RebaseTodo.AppliedCommits(plan));
        Assert.Throws<ArgumentException>(() => RebaseTodo.LeadingPicks(Plan(Step(A, RebaseAction.Squash))));
        Assert.Throws<ArgumentException>(() => RebaseTodo.AppliedCommits(Plan()));
    }

    private static RebaseStep Step(string sha, RebaseAction action = RebaseAction.Pick, string? message = null) => new(sha, action, message);

    private static RebasePlan Plan(params RebaseStep[] steps) => new(Onto, C, Branch, steps);

    private static RebaseListEntry Entry(string sha) => new(sha, "subject", "Test Author", "subject\n");
}
