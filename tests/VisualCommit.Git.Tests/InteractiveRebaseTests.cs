using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.InteractiveRebaseScenarios;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RebaseTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The interactive rebase and the squash in the git layer
/// (<see cref="GitRepository.InteractiveRebaseAsync(RebasePlan, NetworkOptions?, CancellationToken)"/>,
/// docs/phase-4-design.md 2.4, 2.5, section 3, D143) with real git: reorder, drop, squash,
/// fixup and reword, the squash plan of selected commits, and the delivery of the todo and its
/// message files through the app's sequence editor. <see cref="InteractiveRebaseGuardTests"/>
/// has the guarded amend and the hooks, <see cref="InteractiveRebaseRefusalTests"/> what is
/// refused; classes of their own, so that they run side by side.
/// </summary>
public class InteractiveRebaseTests
{
    [Fact]
    public async Task Reordered_commits_are_replayed_in_the_new_order_and_unchanged_leading_ones_keep_their_ids()
    {
        using var repo = await LineAsync();
        var (repository, list) = await OpenListAsync(repo);
        var oldTip = list.BranchTip;
        var branchesBefore = await OtherBranchesAsync(repo);

        var result = await repository.InteractiveRebaseAsync(Reorder(Plan(list, Topic, Pick, Pick, Pick, Pick), 0, 2, 1, 3), cancellationToken: TestCancelled);

        Assert.Equal(new RebaseResult(RebaseOutcome.Rebased, await repo.HeadAsync(), 0), result);
        Assert.Equal(["Add one", "Add three", "Add two", "Add four"], await SubjectsAsync(repo, $"{list.Onto}..HEAD"));
        Assert.Equal(await RevAsync(repo, oldTip + "^{tree}"), await RevAsync(repo, "HEAD^{tree}"));
        Assert.Equal(list.CommitsOldestFirst[0].Sha, await RevAsync(repo, "HEAD~3"));
        Assert.Equal(TwoMessage, await MessageAsync(repo, "HEAD~1"));
        Assert.Equal(Topic, await HeadBranchAsync(repo));
        Assert.Equal(branchesBefore, await OtherBranchesAsync(repo));
        Assert.False(RebaseInProgress(repo));
    }

    [Fact]
    public async Task A_dropped_commit_is_left_out()
    {
        using var repo = await LineAsync();
        var (repository, list) = await OpenListAsync(repo);

        var result = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Drop, Pick, Pick), cancellationToken: TestCancelled);

        Assert.Equal(RebaseOutcome.Rebased, result.Outcome);
        Assert.Equal(["Add one", "Add three", "Add four"], await SubjectsAsync(repo, $"{list.Onto}..HEAD"));
        Assert.False(File.Exists(Path.Combine(repo.Path, "two.txt")));
    }

    [Fact]
    public async Task A_squash_gets_the_groups_edited_message_exactly_hash_lines_included_and_the_targets_author()
    {
        using var repo = await LineAsync();
        var (repository, list) = await OpenListAsync(repo);
        var twoAuthor = await AuthorAsync(repo, list.CommitsOldestFirst[1].Sha);
        const string Message = "Two and three\n\n#12 issue line\n#13 another\n";

        var result = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Target(Message), Squash, Pick), cancellationToken: TestCancelled);

        Assert.Equal(RebaseOutcome.Rebased, result.Outcome);
        Assert.Equal(["Add one", "Two and three", "Add four"], await SubjectsAsync(repo, $"{list.Onto}..HEAD"));
        Assert.Equal(Message, await MessageAsync(repo, "HEAD~1"));
        Assert.Equal(twoAuthor, await AuthorAsync(repo, "HEAD~1"));
        Assert.Equal(["four.txt", "one.txt", "three.txt", "two.txt"], (await TreeFilesAsync(repo, "HEAD")).Where(file => file.EndsWith(".txt", StringComparison.Ordinal) && file != "f.txt"));
    }

    [Fact]
    public async Task The_squash_plan_of_selected_commits_joins_them_with_the_dialogs_message()
    {
        using var repo = await LineAsync();
        var repository = await OpenAsync(repo);
        var two = await RevAsync(repo, "topic~2");
        var three = await RevAsync(repo, "topic~1");
        var list = await repository.ReadRebaseListAsync(await RevAsync(repo, two + "~1"), Topic, fromCommit: true, cancellationToken: TestCancelled);
        var message = RebaseTodo.SquashMessage(list.CommitsOldestFirst.Take(2).Select(entry => entry.Message));

        var result = await repository.InteractiveRebaseAsync(RebaseTodo.SquashPlan(list, Topic, [three, two], message), cancellationToken: TestCancelled);

        Assert.Equal(RebaseOutcome.Rebased, result.Outcome);
        Assert.Equal("Add two\n\n#12 issue line\n\nAdd three\n", await MessageAsync(repo, "HEAD~1"));
        Assert.Equal(["Add one", "Add two", "Add four"], await SubjectsAsync(repo, $"{list.Onto}~1..HEAD"));
    }

    [Fact]
    public async Task A_group_of_fixups_only_keeps_the_targets_own_message_exactly()
    {
        using var repo = await LineAsync();
        var (repository, list) = await OpenListAsync(repo);

        var result = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Pick, Fixup, Pick), cancellationToken: TestCancelled);

        Assert.Equal(RebaseOutcome.Rebased, result.Outcome);
        Assert.Equal(TwoMessage, await MessageAsync(repo, "HEAD~1"));
        Assert.True(File.Exists(Path.Combine(repo.Path, "three.txt")));
        Assert.Equal(3, (await SubjectsAsync(repo, $"{list.Onto}..HEAD")).Count);
    }

    [Fact]
    public async Task A_reworded_commit_gets_the_new_message_exactly_and_keeps_its_author_and_date()
    {
        using var repo = await LineAsync();
        var (repository, list) = await OpenListAsync(repo);
        var twoAuthor = await AuthorAsync(repo, list.CommitsOldestFirst[1].Sha);
        const string Message = "Two reworded\n\n#99 kept\n";

        var result = await repository.InteractiveRebaseAsync(Plan(list, Topic, Pick, Reword(Message), Pick, Pick), cancellationToken: TestCancelled);

        Assert.Equal(RebaseOutcome.Rebased, result.Outcome);
        Assert.Equal(Message, await MessageAsync(repo, "HEAD~2"));
        Assert.Equal(twoAuthor, await AuthorAsync(repo, "HEAD~2"));
        Assert.Equal(list.CommitsOldestFirst[0].Sha, await RevAsync(repo, "HEAD~3"));
        Assert.Equal(["Add one", "Two reworded", "Add three", "Add four"], await SubjectsAsync(repo, $"{list.Onto}..HEAD"));
    }

    [Fact]
    public async Task The_todo_and_its_message_files_reach_git_from_a_folder_whose_path_has_a_space_and_an_apostrophe_which_is_deleted_after()
    {
        using var repo = await LineAsync();
        var (repository, list) = await OpenListAsync(repo);
        var tempRoot = Sibling(repo, "temp it's here");
        Directory.CreateDirectory(tempRoot);

        var result = await repository.InteractiveRebaseAsync(
            Plan(list, Topic, Reword("One reworded\n"), Target("Two and three\n\n#1 kept\n"), Squash, Reword("Four reworded\n")),
            tempRoot,
            null,
            TestCancelled);

        Assert.Equal(RebaseOutcome.Rebased, result.Outcome);
        Assert.Equal("One reworded\n", await MessageAsync(repo, "HEAD~2"));
        Assert.Equal("Two and three\n\n#1 kept\n", await MessageAsync(repo, "HEAD~1"));
        Assert.Equal("Four reworded\n", await MessageAsync(repo, "HEAD"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(tempRoot));
    }

    [Fact]
    public void The_sequence_editor_is_a_shell_function_with_single_quoted_forward_slash_paths()
    {
        Assert.Equal(
            "vc() { cp 'C:/t/it'\\''s here/visualcommit-rebase-1/'vc-message-* \"$(dirname \"$1\")/\" && cp 'C:/t/it'\\''s here/visualcommit-rebase-1/todo' \"$1\"; }; vc",
            GitRepository.RebaseText.SequenceEditor(@"C:\t\it's here\visualcommit-rebase-1\todo", @"C:\t\it's here\visualcommit-rebase-1"));
        Assert.Equal("vc() { cp '/tmp/x/todo' \"$1\"; }; vc", GitRepository.RebaseText.SequenceEditor("/tmp/x/todo", null));
    }
}
