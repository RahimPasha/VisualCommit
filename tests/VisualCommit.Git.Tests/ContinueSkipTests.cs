using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.SequenceTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Continue and Skip (docs/phase-4-design.md 5.1, 5.4) for a merge, a single cherry-pick or
/// revert and git's lists, against real git: what each state's Continue and Skip run and what they
/// leave, and the message the stage panel fills in for the stopped commit (5.3). A rebase's are
/// <see cref="ContinueSkipRebaseTests"/>'. Each state is made by git as a terminal or the app
/// makes it, and read with <see cref="GitRepository.ReadStatusAsync"/> as the app reads it.
/// </summary>
public class ContinueSkipTests
{
    /// <summary>2026-01-01 12:06 UTC, "Conflicting change"'s author date in the operations scenario.</summary>
    private const string ConflictingAuthorTime = "1767269160";

    // ---- Merge ------------------------------------------------------------------------------------

    [Fact]
    public async Task Continue_commits_a_merge_with_the_panels_message_and_its_hash_lines()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var mainWork = await repo.HeadAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var result = await repository.ContinueAsync(state, "Merge the conflict\n\n#12 stays\n", cancellationToken: TestCancelled);

        Assert.Equal(new ContinueResult(ContinueOutcome.Done, await repo.HeadAsync()), result);
        Assert.Contains("git commit --file=- --cleanup=whitespace", calls.Snapshot().Select(call => call.CommandText));
        Assert.Equal("Merge the conflict\n\n#12 stays", await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal($"{mainWork} {await RevAsync(repo, "refs/heads/feature/conflict")}", await GitTextAsync(repo, "log", "-1", "--format=%P"));
        Assert.False(GitFileExists(repo, "MERGE_HEAD"));
        Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);

        // Git has no skip for a merge.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.SkipAsync(state, cancellationToken: TestCancelled));
    }

    [Fact]
    public async Task Continue_without_a_message_commits_a_merge_resolved_to_ours_with_MERGE_MSG_less_gits_block()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        var repository = await OpenAsync(repo);
        await repo.GitAsync("checkout", "--ours", "--", "src/core.txt");
        await repo.GitAsync("add", "src/core.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal("Merge branch 'feature/conflict'\n\n# Conflicts:\n#\tsrc/core.txt\n", ReadGitFile(repo, "MERGE_MSG"));

        Assert.Equal("Merge branch 'feature/conflict'", await repository.ReadOperationMessageAsync(state, cancellationToken: TestCancelled));
        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        // Nothing staged, and still the merge commit (0.3).
        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal("Merge branch 'feature/conflict'", await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal(2, (await GitTextAsync(repo, "log", "-1", "--format=%P")).Split(' ').Length);
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, ReadText(repo, "src/core.txt"));
    }

    // ---- A single cherry-pick or revert -----------------------------------------------------------

    [Fact]
    public async Task Continue_commits_a_single_pick_with_its_author_and_ends_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var conflicting = await RevAsync(repo, "refs/heads/feature/conflict");
        var mainWork = await repo.HeadAsync();
        Assert.Equal(ApplyResult.Conflicts, await repository.CherryPickAsync(conflicting, cancellationToken: TestCancelled));
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal((true, false), (state.HasPickHead, state.Sequenced));

        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(new ContinueResult(ContinueOutcome.Done, await repo.HeadAsync()), result);
        Assert.Equal($"Conflicting change\n{ConflictingAuthorTime}\n{mainWork}", await GitTextAsync(repo, "log", "-1", "--format=%B%at%n%P"));
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task Skip_ends_a_single_pick_and_keeps_HEAD()
    {
        using var repo = await Scenarios.OperationsAsync();
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var head = await repo.HeadAsync();
        Assert.Equal(ApplyResult.Conflicts, await repository.CherryPickAsync(await RevAsync(repo, "refs/heads/feature/conflict"), cancellationToken: TestCancelled));
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var result = await repository.SkipAsync(state, cancellationToken: TestCancelled);

        Assert.Equal(new ContinueResult(ContinueOutcome.Done, head), result);
        Assert.Contains("git cherry-pick --skip", calls.Snapshot().Select(call => call.CommandText));
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, ReadText(repo, "src/core.txt"));
    }

    [Fact]
    public async Task Continue_commits_a_single_revert_with_gits_message()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Revert);
        var repository = await OpenAsync(repo);
        var mainWork = await RevAsync(repo, "refs/heads/main~1");
        repo.WriteFile("src/core.txt", "core\n");
        await repo.GitAsync("add", "src/core.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal($"Revert \"Main work\"\n\nThis reverts commit {mainWork}.", await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.False(GitFileExists(repo, "REVERT_HEAD"));
    }

    // ---- A pick in a list, with its pick head -----------------------------------------------------

    [Fact]
    public async Task Continue_commits_a_lists_stopped_pick_with_the_panels_message_and_picks_the_rest()
    {
        using var repo = await Scenarios.OperationsAsync();
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var picks = await OperationsPicksAsync(repo);
        Assert.Equal(SequenceStop.Conflicts, (await repository.CherryPickAsync(picks, cancellationToken: TestCancelled)).Stop);
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var result = await repository.ContinueAsync(state, "Conflicting change\n\n#5 kept\n", cancellationToken: TestCancelled);

        // The commit with the panel's message, then git's --continue leaves that line out (D110).
        Assert.Equal(new ContinueResult(ContinueOutcome.Done, await repo.HeadAsync()), result);
        var commands = calls.Snapshot().Select(call => call.CommandText).ToList();
        Assert.True(commands.IndexOf("git commit --file=- --cleanup=whitespace") < commands.IndexOf("git cherry-pick --continue"));
        Assert.Equal(["Alpha two", "Conflicting change", "Alpha one", "Main work"], await GitLinesAsync(repo, "log", "--format=%s", "-4"));
        Assert.Equal($"Conflicting change\n\n#5 kept\n{ConflictingAuthorTime}", await GitTextAsync(repo, "log", "-1", "--format=%B%at", "HEAD~1"));
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.Same(InProgressState.None, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task Skip_drops_a_lists_stopped_pick_and_picks_the_rest()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        Assert.Equal(SequenceStop.Conflicts, (await repository.CherryPickAsync(await OperationsPicksAsync(repo), cancellationToken: TestCancelled)).Stop);
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var result = await repository.SkipAsync(state, cancellationToken: TestCancelled);

        // The staged resolution is lost (0.3): the app asked and saved a backup first.
        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal(["Alpha two", "Alpha one", "Main work"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, ReadText(repo, "src/core.txt"));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.False(GitFileExists(repo, "sequencer"));
    }

    [Fact]
    public async Task Continue_that_meets_the_next_conflict_stops_again_at_it()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
        await repo.CreateBranchAsync("side");
        await repo.CommitFileAsync("f.txt", "main\n", "Main edit");
        await repo.CheckoutAsync("side");
        var first = await repo.CommitFileAsync("f.txt", "side 1\n", "Side one");
        var second = await repo.CommitFileAsync("f.txt", "side 2\n", "Side two");
        await repo.CheckoutAsync("main");
        var repository = await OpenAsync(repo);
        Assert.Equal(new SequenceResult(SequenceStop.Conflicts, 0, 2), await repository.CherryPickAsync([first, second], cancellationToken: TestCancelled));
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(new ContinueResult(ContinueOutcome.Stopped, await repo.HeadAsync()), result);
        Assert.Equal("Side one", await GitTextAsync(repo, "log", "-1", "--format=%s"));
        Assert.Equal(second, ReadGitFile(repo, "CHERRY_PICK_HEAD").Trim());
        Assert.Equal(["UU f.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        var again = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal((second, true, true, 0), (again.Commit, again.Sequenced, again.HasPickHead, again.Remaining));
    }

    [Fact]
    public async Task Continue_with_files_still_conflicted_is_refused_and_changes_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        Assert.Equal(SequenceStop.Conflicts, (await repository.CherryPickAsync(await OperationsPicksAsync(repo), cancellationToken: TestCancelled)).Stop);
        var head = await repo.HeadAsync();
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var error = await Assert.ThrowsAsync<GitException>(() => repository.ContinueAsync(state, null, cancellationToken: TestCancelled));

        Assert.Contains("unmerged files", error.StandardError, StringComparison.Ordinal);
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(["UU src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(state, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    // ---- A list paused between commits, or stopped without a pick head ------------------------------

    [Fact]
    public async Task A_list_paused_after_a_commit_in_a_terminal_continues_and_has_nothing_to_skip()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        var repository = await OpenAsync(repo);
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        await repo.GitAsync("commit", "--quiet", "--no-edit");
        var committed = await repo.HeadAsync();
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal((true, false, true), (state.Sequenced, state.HasPickHead, state.AbortKeepsHead));

        // Skip: git has nothing to skip, and nothing changes (5.1 disables it).
        await Assert.ThrowsAsync<GitException>(() => repository.SkipAsync(state, cancellationToken: TestCancelled));
        Assert.Equal(committed, await repo.HeadAsync());
        Assert.Equal(state, (await repository.ReadStatusAsync(TestCancelled)).InProgress);

        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(new ContinueResult(ContinueOutcome.Done, await repo.HeadAsync()), result);
        Assert.Equal(["Alpha two", "Conflicting change", "Alpha one"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
        Assert.Equal(committed, await RevAsync(repo, "HEAD~1"));
        Assert.False(GitFileExists(repo, "sequencer"));
        Assert.Null(await repository.ReadOperationMessageAsync(state, cancellationToken: TestCancelled));
    }

    [Fact]
    public async Task A_list_stopped_on_a_refused_commit_is_skipped_not_continued()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
        await repo.CreateBranchAsync("side");
        await repo.CheckoutAsync("side");
        var a = await repo.CommitFileAsync("a.txt", "a\n", "A adds a");
        var b = await repo.CommitFileAsync("u.txt", "tracked\n", "B adds u");
        var c = await repo.CommitFileAsync("c.txt", "c\n", "C adds c");
        await repo.CheckoutAsync("main");
        repo.WriteFile("u.txt", "untracked\n");
        var repository = await OpenAsync(repo);
        Assert.Equal(new SequenceResult(SequenceStop.Refused, 1, 3), await repository.CherryPickAsync([a, b, c], cancellationToken: TestCancelled));
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        var todo = ReadGitFile(repo, "sequencer/todo");
        var head = await repo.HeadAsync();

        // Git's --continue would leave B out without a word (0.3): Continue refuses before git runs.
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ContinueAsync(state, null, cancellationToken: TestCancelled));
        Assert.Equal(todo, ReadGitFile(repo, "sequencer/todo"));
        Assert.Equal(head, await repo.HeadAsync());

        var result = await repository.SkipAsync(state, cancellationToken: TestCancelled);

        Assert.Equal(new ContinueResult(ContinueOutcome.Done, await repo.HeadAsync()), result);
        Assert.Equal(["C adds c", "A adds a", "Initial commit"], await GitLinesAsync(repo, "log", "--format=%s"));
        Assert.Equal("untracked\n", ReadText(repo, "u.txt"));
        Assert.False(GitFileExists(repo, "sequencer"));
    }

    [Fact]
    public async Task An_empty_pick_in_a_list_is_skipped_and_its_commit_is_refused()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);
        var alphaOne = await RevAsync(repo, "refs/heads/feature/alpha~1");
        await repo.GitAsync("cherry-pick", alphaOne);
        var head = await repo.HeadAsync();
        Assert.Equal(SequenceStop.Empty, (await repository.CherryPickAsync([alphaOne, await RevAsync(repo, "refs/heads/feature/alpha")], cancellationToken: TestCancelled)).Stop);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.True(state.Empty);

        // Continue is disabled there (5.1); git refuses the empty commit if it is run anyway.
        await Assert.ThrowsAsync<GitException>(() => repository.ContinueAsync(state, null, cancellationToken: TestCancelled));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(alphaOne, ReadGitFile(repo, "CHERRY_PICK_HEAD").Trim());

        var result = await repository.SkipAsync(state, cancellationToken: TestCancelled);

        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal(["Alpha two", "Alpha one", "Main work"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
        Assert.False(GitFileExists(repo, "sequencer"));
    }

    [Fact]
    public async Task An_empty_revert_in_a_list_is_left_out_by_Skip_and_without_a_pick_head_by_Continue()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
        var a = await repo.CommitFileAsync("a.txt", "a\n", "A adds a");
        var b = await repo.CommitFileAsync("b.txt", "b\n", "B adds b");
        var c = await repo.CommitFileAsync("c.txt", "c\n", "C adds c");
        await repo.GitAsync("revert", "--no-edit", b);
        var repository = await OpenAsync(repo);
        Assert.Equal(new SequenceResult(SequenceStop.Empty, 1, 3), await repository.RevertAsync([c, b, a], cancellationToken: TestCancelled));
        using var copy = await repo.CopyAsync();
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        var pickHead = GitFileExists(repo, "REVERT_HEAD");
        Assert.Equal((b, true, pickHead, true), (state.Commit, state.Sequenced, state.HasPickHead, state.Empty));
        string[] expected = ["Revert \"A adds a\"", "Revert \"C adds c\"", "Revert \"B adds b\"", "C adds c"];

        if (!pickHead)
        {
            // Git 2.36: no REVERT_HEAD (D117). The stage panel fills nothing (8), and git's
            // --continue leaves the empty revert out (0.3).
            Assert.Null(await repository.ReadOperationMessageAsync(state, cancellationToken: TestCancelled));
            Assert.Equal(ContinueOutcome.Done, (await repository.ContinueAsync(state, null, cancellationToken: TestCancelled)).Outcome);
            Assert.Equal(expected, await GitLinesAsync(repo, "log", "--format=%s", "-4"));
        }
        else
        {
            // A git that keeps REVERT_HEAD stops as an empty pick does: committing it is refused.
            await Assert.ThrowsAsync<GitException>(() => repository.ContinueAsync(state, null, cancellationToken: TestCancelled));
        }

        var copied = await OpenAsync(copy);
        Assert.Equal(ContinueOutcome.Done, (await copied.SkipAsync(state, cancellationToken: TestCancelled)).Outcome);
        Assert.Equal(expected, await GitLinesAsync(copy, "log", "--format=%s", "-4"));
        Assert.False(GitFileExists(copy, "sequencer"));
    }

    [Fact]
    public async Task Continue_and_Skip_refuse_states_they_do_not_continue()
    {
        using var repo = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(repo);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.ContinueAsync(InProgressState.None, null, cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.ContinueAsync(new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, 1, false), null, cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.SkipAsync(InProgressState.None, cancellationToken: TestCancelled));

        // A pick that is not stopped at all: neither a pick head nor a list.
        await Assert.ThrowsAsync<ArgumentException>(() => repository.ContinueAsync(new InProgressState(InProgressKind.CherryPick, null, null, null, null, 0, false), null, cancellationToken: TestCancelled));
    }

    // ---- The stage panel's message (5.3) ------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData(";")]
    public async Task A_picks_message_keeps_the_commits_own_hash_lines_and_drops_gits_block(string? commentChar)
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
        await repo.CreateBranchAsync("side");
        await repo.CommitFileAsync("f.txt", "main\n", "Main edit");
        await repo.CheckoutAsync("side");
        repo.WriteFile("f.txt", "side\n");
        await repo.GitAsync("add", "f.txt");
        await repo.GitAsync("commit", "--quiet", "--cleanup=verbatim", "--message", "Side edit\n\n#7 issue line\n");
        var side = await repo.HeadAsync();
        await repo.CheckoutAsync("main");
        if (commentChar is not null)
        {
            await repo.ConfigAsync("core.commentChar", commentChar);
        }

        var repository = await OpenAsync(repo);
        Assert.Equal(SequenceStop.Conflicts, (await repository.CherryPickAsync([side], cancellationToken: TestCancelled)).Stop);
        var mark = commentChar ?? "#";
        Assert.Equal($"Side edit\n\n#7 issue line\n\n{mark} Conflicts:\n{mark}\tf.txt\n", ReadGitFile(repo, "MERGE_MSG"));
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        Assert.Equal("Side edit\n\n#7 issue line", await repository.ReadOperationMessageAsync(state, cancellationToken: TestCancelled));

        // Continue without a message commits it so, where git's own --continue would drop the line.
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");
        await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);
        Assert.Equal("Side edit\n\n#7 issue line", await GitTextAsync(repo, "log", "-1", "--format=%B"));
    }

    [Theory]
    [InlineData("Subject\n\n# Conflicts:\n#\tf.txt\n", "#", "Subject\n")]
    [InlineData("Subject\n\n#12 own line\n\n# Conflicts:\n#\tf.txt\n#\tg.txt\n", "#", "Subject\n\n#12 own line\n")]
    [InlineData("Subject\n\n#12 own line\n", "#", "Subject\n\n#12 own line\n")]
    [InlineData("Subject\n\n# Conflicts: in the body\nmore\n", "#", "Subject\n\n# Conflicts: in the body\nmore\n")]
    [InlineData("Subject\n\n; Conflicts:\n;\tf.txt\n", ";", "Subject\n")]
    [InlineData("Subject\r\n\r\n# Conflicts:\r\n#\tf.txt\r\n", "#", "Subject\n")]
    [InlineData("Subject\n\n# ------------------------ >8 ------------------------\n#\n# Conflicts:\n#\tf.txt\n", "#", "Subject\n")]
    public void Gits_conflicts_block_is_removed_only_at_the_end(string message, string comment, string expected) =>
        Assert.Equal(expected, GitRepository.WithoutConflictsBlock(message, comment));

    [Fact]
    public void Git_comment_lines_are_removed_as_gits_strip_removes_them()
    {
        const string squash = "# This is a combination of 2 commits.\n# This is the 1st commit message:\n\nT target\n\n# The commit message #2 will be skipped:\n\n# F fixup\n";
        Assert.Equal("T target\n", GitRepository.WithoutCommentLines(squash, "#"));
        Assert.Equal("One\n\nTwo\n", GitRepository.WithoutCommentLines("# a\nOne\n\n\n# b\n\nTwo\n", "#"));
    }

    [Fact]
    public void The_author_script_is_read_as_sh_reads_it()
    {
        const string script = "GIT_AUTHOR_NAME='Zo\u00eb O'\\''Brien'\nGIT_AUTHOR_EMAIL='zoe@example.com'\nGIT_AUTHOR_DATE='@1767272400 +0230'\nGIT_OTHER='x'\n";

        var author = GitRepository.ParseAuthorScript(script);

        Assert.Equal(
            new Dictionary<string, string?>
            {
                ["GIT_AUTHOR_NAME"] = "Zo\u00eb O'Brien",
                ["GIT_AUTHOR_EMAIL"] = "zoe@example.com",
                ["GIT_AUTHOR_DATE"] = "@1767272400 +0230",
            },
            author);
        Assert.Equal("Wow! It's", GitRepository.ParseAuthorScript("GIT_AUTHOR_NAME='Wow'\\!' It'\\''s'\r\n")["GIT_AUTHOR_NAME"]);
    }

    /// <summary>The operations scenario's list that stops: Alpha one, Conflicting change (conflicts with Main work), Alpha two.</summary>
    private static async Task<IReadOnlyList<string>> OperationsPicksAsync(TempRepo repo) =>
    [
        await RevAsync(repo, "refs/heads/feature/alpha~1"),
        await RevAsync(repo, "refs/heads/feature/conflict"),
        await RevAsync(repo, "refs/heads/feature/alpha"),
    ];
}
