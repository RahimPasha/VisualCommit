using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RebaseTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The interactive rebase's list (<see cref="GitRepository.ReadRebaseListAsync"/>,
/// docs/phase-4-design.md 2.3) against the todo git itself writes for the same rebase, captured
/// by a sequence editor that copies the todo out and fails, which leaves nothing started (0.2):
/// a simple branch, a branch with a commit already upstream by patch, a commit that starts
/// empty, a merge in the range, and "from here". Git's todo wins where they differ. Runs on
/// Git 2.36 and on CI's git.
/// </summary>
public class InteractiveRebaseListParityTests
{
    private const string Topic = "refs/heads/topic";

    [Fact]
    public async Task A_simple_branch_lists_what_gits_todo_holds_with_each_commits_subject_author_and_message()
    {
        using var repo = await DivergedAsync();
        var repository = await OpenAsync(repo);
        var main = await RevAsync(repo, "main");

        var list = await repository.ReadRebaseListAsync(main, Topic, fromCommit: false, cancellationToken: TestCancelled);

        Assert.Equal(await GitTodoAsync(repo, main), list.CommitsOldestFirst.Select(entry => entry.Sha));
        Assert.Equal(main, list.Onto);
        Assert.Equal(await RevAsync(repo, "topic"), list.BranchTip);
        Assert.Equal(0, list.MergeCount);
        Assert.Equal(["Topic one", "Topic two", "Topic three"], list.CommitsOldestFirst.Select(entry => entry.Subject));
        Assert.All(list.CommitsOldestFirst, entry => Assert.Equal(TempRepo.AuthorName, entry.AuthorName));
        foreach (var entry in list.CommitsOldestFirst)
        {
            Assert.Equal(await MessageAsync(repo, entry.Sha), entry.Message);
        }

        Assert.Equal("Topic two\n\n#12 issue line\nbody\n", list.CommitsOldestFirst[1].Message);
    }

    [Fact]
    public async Task A_commit_already_upstream_by_patch_is_left_out_as_git_leaves_it_out()
    {
        using var repo = await DivergedAsync();
        await repo.CheckoutAsync("main");
        await repo.GitAsync("cherry-pick", "topic~1");
        await repo.CheckoutAsync("topic");
        var repository = await OpenAsync(repo);
        var main = await RevAsync(repo, "main");

        var list = await repository.ReadRebaseListAsync(main, Topic, fromCommit: false, cancellationToken: TestCancelled);

        Assert.Equal(await GitTodoAsync(repo, main), list.CommitsOldestFirst.Select(entry => entry.Sha));
        Assert.Equal(["Topic one", "Topic three"], list.CommitsOldestFirst.Select(entry => entry.Subject));
    }

    [Fact]
    public async Task A_commit_that_starts_empty_is_listed_as_git_keeps_it()
    {
        using var repo = await DivergedAsync();
        await repo.CommitAsync("Empty on topic");
        var repository = await OpenAsync(repo);
        var main = await RevAsync(repo, "main");

        var list = await repository.ReadRebaseListAsync(main, Topic, fromCommit: false, cancellationToken: TestCancelled);

        Assert.Equal(await GitTodoAsync(repo, main), list.CommitsOldestFirst.Select(entry => entry.Sha));
        Assert.Equal("Empty on topic", list.CommitsOldestFirst[^1].Subject);
    }

    [Fact]
    public async Task An_empty_commit_is_listed_as_git_keeps_it_also_when_the_target_has_an_empty_commit_too()
    {
        // The review's findings 2 and 3: every empty commit has the same patch id, so an empty
        // commit on the target side hid the branch's from --cherry-pick, while git's todo keeps a
        // commit that starts empty; a commit whose non-empty patch is upstream is still left out.
        using var repo = await DivergedAsync();
        await repo.CommitAsync("Empty on topic");
        await repo.CommitFileAsync("four.txt", "four\n", "Topic four");
        await repo.CheckoutAsync("main");
        await repo.CommitAsync("CI trigger");
        await repo.GitAsync("cherry-pick", "topic~3");
        await repo.CheckoutAsync("topic");
        var repository = await OpenAsync(repo);
        var main = await RevAsync(repo, "main");

        var list = await repository.ReadRebaseListAsync(main, Topic, fromCommit: false, cancellationToken: TestCancelled);

        Assert.Equal(await GitTodoAsync(repo, main), list.CommitsOldestFirst.Select(entry => entry.Sha));
        Assert.Equal(["Topic one", "Topic three", "Empty on topic", "Topic four"], list.CommitsOldestFirst.Select(entry => entry.Subject));
        Assert.Equal("Empty on topic\n", list.CommitsOldestFirst[2].Message);
    }

    [Fact]
    public async Task A_merge_in_the_range_is_counted_and_left_out_of_the_list_as_git_leaves_it_out()
    {
        using var repo = await DivergedAsync();
        await repo.CreateBranchAsync("side", "topic~2");
        await repo.CheckoutAsync("side");
        await repo.CommitFileAsync("side.txt", "side\n", "Side work");
        await repo.CheckoutAsync("topic");
        await repo.MergeAsync("side");
        var repository = await OpenAsync(repo);
        var main = await RevAsync(repo, "main");

        var list = await repository.ReadRebaseListAsync(main, Topic, fromCommit: false, cancellationToken: TestCancelled);

        Assert.Equal(await GitTodoAsync(repo, main), list.CommitsOldestFirst.Select(entry => entry.Sha));
        Assert.Equal(1, list.MergeCount);
        Assert.DoesNotContain(list.CommitsOldestFirst, entry => entry.Subject.StartsWith("Merge", StringComparison.Ordinal));
    }

    [Fact]
    public async Task From_here_lists_the_commits_after_the_base_as_git_rebase_i_of_that_commit_does()
    {
        using var repo = await DivergedAsync();
        var repository = await OpenAsync(repo);
        var baseCommit = await RevAsync(repo, "topic~2");

        var list = await repository.ReadRebaseListAsync(baseCommit, Topic, fromCommit: true, cancellationToken: TestCancelled);

        Assert.Equal(await GitTodoAsync(repo, baseCommit), list.CommitsOldestFirst.Select(entry => entry.Sha));
        Assert.Equal(["Topic two", "Topic three"], list.CommitsOldestFirst.Select(entry => entry.Subject));
        Assert.Equal(baseCommit, list.Onto);
        Assert.Equal(0, list.MergeCount);
    }

    [Fact]
    public async Task A_branch_that_is_not_checked_out_is_listed_from_its_own_tip()
    {
        using var repo = await DivergedAsync();
        var tip = await RevAsync(repo, "topic");
        await repo.CheckoutAsync("main");
        var repository = await OpenAsync(repo);

        var list = await repository.ReadRebaseListAsync(await RevAsync(repo, "main"), Topic, fromCommit: false, cancellationToken: TestCancelled);

        Assert.Equal(tip, list.BranchTip);
        Assert.Equal(["Topic one", "Topic two", "Topic three"], list.CommitsOldestFirst.Select(entry => entry.Subject));
    }

    /// <summary>
    /// The commits of the todo <c>git rebase -i &lt;onto&gt;</c> writes on the current branch, as full
    /// SHAs, oldest first: a sequence editor copies the todo out and fails, so git starts nothing
    /// (checked).
    /// </summary>
    private static async Task<IReadOnlyList<string>> GitTodoAsync(TempRepo repo, string onto)
    {
        var copy = Sibling(repo, $"todo-{Guid.NewGuid():N}");
        var head = await repo.HeadAsync();
        var editor = $"vc() {{ cp \"$1\" {GitRepository.RebaseText.Quote(copy)}; return 1; }}; vc";
        var command = new GitCommand("rebase", "-i", onto)
        {
            WorkingDirectory = repo.Path,
            Environment = new Dictionary<string, string?> { ["GIT_SEQUENCE_EDITOR"] = editor },
        };

        var result = await NewRunner().RunAsync(command, TestCancelled);

        Assert.Equal(1, result.ExitCode);
        Assert.False(RebaseInProgress(repo));
        Assert.Equal(head, await repo.HeadAsync());
        var shas = new List<string>();
        foreach (var line in File.ReadAllLines(copy).Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')))
        {
            var fields = line.Split(' ');
            Assert.True(fields[0] is "pick" or "p", $"Not a pick: {line}");
            shas.Add(await RevAsync(repo, fields[1] + "^{commit}"));
        }

        return shas;
    }

    /// <summary>
    /// main and topic diverged from an initial commit: main adds main.txt; topic adds one.txt
    /// ("Topic one"), two.txt ("Topic two" with a <c>#12 issue line</c>) and three.txt ("Topic
    /// three"). HEAD on topic.
    /// </summary>
    private static async Task<TempRepo> DivergedAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("README.md", "readme\n", "Initial commit");
            await repo.CreateBranchAsync("topic");
            await repo.CommitFileAsync("main.txt", "main\n", "Main work");
            await repo.CheckoutAsync("topic");
            await repo.CommitFileAsync("one.txt", "one\n", "Topic one");
            await repo.CommitFileAsync("two.txt", "two\n", "Topic two\n\n#12 issue line\nbody\n");
            await repo.CommitFileAsync("three.txt", "three\n", "Topic three");
            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }
}
