using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.OperationTestSupport;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.SequenceTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Continue and Skip for a rebase (docs/phase-4-design.md 5.1, 5.4), against real git, for every
/// stop of section 8: a pick with a typed message (the commit the app makes keeps it, with the
/// author and the author date, and runs no hook, as git's own Continue runs none), a pick resolved
/// to nothing, unresolved files, a rescheduled pick, a failed exec, the app's own amend refused by
/// a hook, an edit stop, an interactive rebase's empty commit, and a group's target and member.
/// Todos from elsewhere and the app's own (2.5) are handed to <c>git rebase -i</c> through a
/// sequence editor that copies them.
/// </summary>
public class ContinueSkipRebaseTests
{
    // ---- A pick or reword stop ---------------------------------------------------------------------

    [Fact]
    public async Task Continue_at_a_pick_keeps_the_typed_message_the_author_and_the_author_date()
    {
        var (repo, main) = await TopicByAnotherAuthorAsync();
        using var _repo = repo;
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("rebase", "main")).ExitCode);
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal(RebaseStopKind.Pick, state.StopKind);

        var result = await repository.ContinueAsync(state, "Typed summary\n\n#12 issue line\n", cancellationToken: TestCancelled);

        // One commit, the message exactly as typed, the stopped commit's author and date; the
        // refusing pre-commit hook did not run (--no-verify, as git's own Continue runs none).
        Assert.Equal(new ContinueResult(ContinueOutcome.Done, await repo.HeadAsync()), result);
        Assert.Equal("Typed summary\n\n#12 issue line", await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal(["Zoë O'Brien", "zoe@example.com", "1767272400 +0230"], await GitLinesAsync(repo, "log", "-1", "--date=raw", "--format=%an%n%ae%n%ad"));
        Assert.Equal(main, await RevAsync(repo, "HEAD~1"));
        Assert.Equal("topic", await repo.CurrentBranchAsync());
        Assert.False(repository.RebaseFolderExists());
        var commands = calls.Snapshot().Select(call => call.CommandText).ToList();
        Assert.True(commands.IndexOf("git commit --no-verify --cleanup=whitespace --file=-") < commands.IndexOf("git rebase --continue"));
    }

    [Fact]
    public async Task Continue_without_a_message_keeps_the_commits_own_message_with_its_hash_lines()
    {
        var (repo, _) = await TopicByAnotherAuthorAsync();
        using var _repo = repo;
        var repository = await OpenAsync(repo);
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("rebase", "main")).ExitCode);
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        // rebase-merge/message with no line removed (5.3); git's own Continue would lose the line (0.1).
        Assert.Equal("Topic edit\n\n#3 from the commit", await repository.ReadOperationMessageAsync(state, cancellationToken: TestCancelled));
        Assert.Equal(ContinueOutcome.Done, (await repository.ContinueAsync(state, null, cancellationToken: TestCancelled)).Outcome);

        Assert.Equal("Topic edit\n\n#3 from the commit", await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal("1767272400 +0230", await GitTextAsync(repo, "log", "-1", "--date=raw", "--format=%ad"));
    }

    [Fact]
    public async Task Continue_at_a_pick_resolved_to_nothing_leaves_it_out()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Rebase);
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        await repo.GitAsync("checkout", "--ours", "--", "src/core.txt");
        await repo.GitAsync("add", "src/core.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal(RebaseStopKind.Empty, state.StopKind);

        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(new ContinueResult(ContinueOutcome.Done, await RevAsync(repo, "refs/heads/main")), result);
        Assert.Equal(await RevAsync(repo, "refs/heads/main"), await RevAsync(repo, "refs/heads/feature/conflict"));
        Assert.DoesNotContain(calls.Snapshot(), call => call.CommandText.StartsWith("git commit", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Skip_drops_the_stopped_commit_and_its_resolution()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Rebase);
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var result = await repository.SkipAsync(state, cancellationToken: TestCancelled);

        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Contains("git rebase --skip", calls.Snapshot().Select(call => call.CommandText));
        Assert.Equal(await RevAsync(repo, "refs/heads/main"), await RevAsync(repo, "refs/heads/feature/conflict"));
        Assert.Equal("feature/conflict", await repo.CurrentBranchAsync());
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, ReadText(repo, "src/core.txt"));
        Assert.Empty(await GitLinesAsync(repo, "status", "--porcelain"));
    }

    [Fact]
    public async Task Continue_with_unresolved_files_is_refused_and_the_rebase_stays()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Rebase);
        var repository = await OpenAsync(repo);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var error = await Assert.ThrowsAsync<GitException>(() => repository.ContinueAsync(state, "Typed\n", cancellationToken: TestCancelled));

        Assert.Contains("You must edit all merge conflicts", error.StandardOutput + error.StandardError, StringComparison.Ordinal);
        Assert.True(repository.RebaseFolderExists());
        Assert.Equal(["UU src/core.txt"], await GitLinesAsync(repo, "status", "--porcelain"));
        Assert.Equal(state, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
    }

    [Fact]
    public async Task Continue_with_an_unrelated_unstaged_change_is_refused_before_anything_is_committed()
    {
        var (repo, _, commits) = await TopicAsync(("g.txt", "G adds g"), ("f.txt", "T conflicts"));
        using var _repo = repo;
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("rebase", "main")).ExitCode);
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");
        repo.WriteFile("g.txt", "an editor touched it\n");
        var head = await repo.HeadAsync();
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal((RebaseStopKind.Pick, commits[1]), (state.StopKind!.Value, state.Commit));

        var refused = await Assert.ThrowsAsync<GitException>(() => repository.ContinueAsync(state, "Typed\n", cancellationToken: TestCancelled));

        // Git's own --continue would refuse after the app's commit (finding 1): the app refuses
        // first, names the file, and nothing is committed; the stop is still the resolved pick.
        Assert.Equal(GitRepository.RebaseUnstagedChangesMessage + "\ng.txt\n", refused.StandardError);
        Assert.DoesNotContain(calls.Snapshot(), call => call.CommandText.StartsWith("git commit", StringComparison.Ordinal) || call.CommandText.StartsWith("git rebase", StringComparison.Ordinal));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(state, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
        Assert.Equal(["M  f.txt", " M g.txt"], await GitLinesAsync(repo, "status", "--porcelain"));

        // Stashed (here: put back), the same Continue goes on.
        await repo.GitAsync("checkout", "--", "g.txt");
        Assert.Equal(ContinueOutcome.Done, (await repository.ContinueAsync(state, "Typed\n", cancellationToken: TestCancelled)).Outcome);
        Assert.Equal(["Typed", "G adds g", "Main edit"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
    }

    [Fact]
    public async Task Continue_after_another_stop_goes_on_to_the_next_conflict()
    {
        var (repo, main, commits) = await TopicAsync(("f.txt", "T1 conflicts"), ("g.txt", "T2 adds g"), ("f.txt", "T3 conflicts again"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("rebase", main)).ExitCode);
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(new ContinueResult(ContinueOutcome.Stopped, await repo.HeadAsync()), result);
        var next = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal((commits[2], RebaseStopKind.Pick, 3, 3), (next.Commit, next.StopKind!.Value, next.Done, next.Total));
        Assert.Equal(["T2 adds g", "T1 conflicts"], await GitLinesAsync(repo, "log", "--format=%s", "-2"));
    }

    // ---- Stops with nothing conflicted --------------------------------------------------------------

    [Fact]
    public async Task A_rescheduled_pick_is_tried_again_by_Continue_until_the_file_is_moved()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("README.md", "readme\n", "Initial commit");
        await repo.CreateBranchAsync("topic");
        await repo.CommitFileAsync("main.txt", "main\n", "Main work");
        await repo.CheckoutAsync("topic");
        await repo.CommitFileAsync("x.txt", "x\n", "X first");
        var addIn = await repo.CommitFileAsync("in.txt", "tracked\n", "Add in");
        repo.DeleteFile("in.txt");
        await repo.CommitAsync("Remove in");
        repo.WriteFile("in.txt", "untracked\n");
        var repository = await OpenAsync(repo);
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("rebase", "main")).ExitCode);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal(RebaseStopKind.Rescheduled, state.StopKind);

        var again = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(ContinueOutcome.Stopped, again.Outcome);
        Assert.Equal(state, (await repository.ReadStatusAsync(TestCancelled)).InProgress);
        Assert.Equal("untracked\n", ReadText(repo, "in.txt"));

        repo.DeleteFile("in.txt");
        var done = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(ContinueOutcome.Done, done.Outcome);
        Assert.Equal(["Remove in", "Add in", "X first", "Main work"], await GitLinesAsync(repo, "log", "--format=%s", "-4"));
        Assert.NotEqual(addIn, await RevAsync(repo, "HEAD~1"));
    }

    [Fact]
    public async Task Continue_after_a_failed_exec_goes_on_without_running_it_again()
    {
        var (repo, main, commits) = await TopicAsync(("a.txt", "A adds a"), ("b.txt", "B adds b"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);
        Assert.Equal(1, (await InteractiveRebaseAsync(repo, main, $"pick {commits[0]}\nexec false\npick {commits[1]}\n")).ExitCode);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal(RebaseStopKind.ExecFailed, state.StopKind);

        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal(["B adds b", "A adds a", "Main edit"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
    }

    [Fact]
    public async Task The_apps_amend_refused_by_a_hook_is_made_by_Continue_once_the_hook_allows_it()
    {
        var (repo, main, commits) = await TopicAsync(("t.txt", "T target"), ("p.txt", "P plain"));
        using var _repo = repo;
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        await StopOnRefusedAmendAsync(repo, main, commits);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal((RebaseStopKind.OwnAmendFailed, (int?)1), (state.StopKind!.Value, state.Group));
        Assert.Equal("T reworded\n\n#4 kept", await repository.ReadOperationMessageAsync(state, cancellationToken: TestCancelled));

        // The hook still refuses: Continue fails with its output, and the rebase stays stopped.
        var refused = await Assert.ThrowsAsync<GitException>(() => repository.ContinueAsync(state, null, cancellationToken: TestCancelled));
        Assert.Contains("the hook refuses this message", refused.StandardError, StringComparison.Ordinal);
        Assert.StartsWith("git commit --amend --only --allow-empty --cleanup=whitespace --file=", refused.CommandText, StringComparison.Ordinal);
        Assert.Equal(state, (await repository.ReadStatusAsync(TestCancelled)).InProgress);

        File.Delete(Path.Combine(repo.Path, ".git", "refuse"));
        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal("T reworded\n\n#4 kept", await GitTextAsync(repo, "log", "-1", "--format=%B", "HEAD~1"));
        Assert.Equal(["P plain", "T reworded", "Main edit"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
        Assert.Contains("git rebase --continue", calls.Snapshot().Select(call => call.CommandText));
    }

    [Fact]
    public async Task Continue_at_the_apps_failed_amend_takes_a_typed_message()
    {
        var (repo, main, commits) = await TopicAsync(("t.txt", "T target"), ("p.txt", "P plain"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);
        await StopOnRefusedAmendAsync(repo, main, commits);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        File.Delete(Path.Combine(repo.Path, ".git", "refuse"));

        var result = await repository.ContinueAsync(state, "Typed instead\n\n#5 kept too\n", cancellationToken: TestCancelled);

        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal("Typed instead\n\n#5 kept too", await GitTextAsync(repo, "log", "-1", "--format=%B", "HEAD~1"));
    }

    [Fact]
    public async Task Skip_at_the_apps_failed_amend_goes_on_without_the_new_message()
    {
        var (repo, main, commits) = await TopicAsync(("t.txt", "T target"), ("p.txt", "P plain"));
        using var _repo = repo;
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        await StopOnRefusedAmendAsync(repo, main, commits);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;

        var result = await repository.SkipAsync(state, cancellationToken: TestCancelled);

        // git rebase --continue, which does not run the failed exec again: no hook is asked.
        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Contains("git rebase --continue", calls.Snapshot().Select(call => call.CommandText));
        Assert.DoesNotContain("git rebase --skip", calls.Snapshot().Select(call => call.CommandText));
        Assert.Equal(["P plain", "T target", "Main edit"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
    }

    [Fact]
    public async Task Continue_at_an_edit_stop_lets_git_amend_what_is_staged()
    {
        var (repo, main, commits) = await TopicAsync(("a.txt", "A adds a"), ("b.txt", "B adds b"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);
        Assert.Equal(0, (await InteractiveRebaseAsync(repo, main, $"edit {commits[0]}\npick {commits[1]}\n")).ExitCode);
        repo.WriteFile("e.txt", "edited\n");
        await repo.GitAsync("add", "e.txt");
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal(RebaseStopKind.Edit, state.StopKind);

        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal(["B adds b", "A adds a", "Main edit"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
        Assert.Equal(["a.txt", "e.txt"], await GitLinesAsync(repo, "diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD~1"));
    }

    [Fact]
    public async Task Continue_at_an_interactive_rebases_empty_commit_leaves_it_out()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
        await repo.CreateBranchAsync("topic");
        var main = await repo.CommitFileAsync("m.txt", "m\n", "Main adds m");
        await repo.CheckoutAsync("topic");
        var same = await repo.CommitFileAsync("m.txt", "m\n", "E same as main");
        var later = await repo.CommitFileAsync("b.txt", "b\n", "B adds b");
        var repository = await OpenAsync(repo);
        Assert.Equal(1, (await InteractiveRebaseAsync(repo, main, $"pick {same}\npick {later}\n")).ExitCode);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal(RebaseStopKind.Empty, state.StopKind);

        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal(["B adds b", "Main adds m", "Initial commit"], await GitLinesAsync(repo, "log", "--format=%s"));
        Assert.False(GitFileExists(repo, "CHERRY_PICK_HEAD"));
    }

    // ---- Inside a group the app wrote (2.5) ---------------------------------------------------------

    [Fact]
    public async Task Continue_at_a_groups_target_writes_the_typed_message_for_the_groups_amend()
    {
        var (repo, main, commits) = await TopicAsync(("f.txt", "T conflicts"), ("h.txt", "F fixup"), ("p.txt", "P plain"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);
        Assert.Equal(1, (await InteractiveRebaseAsync(repo, main, Group(1, commits[0], commits[1]) + $"pick {commits[2]}\n", "Group message\n\n#9 kept\n")).ExitCode);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal((true, (int?)1), (state.NextIsSquash, state.Group));
        Assert.Equal("Group message\n\n#9 kept", await repository.ReadOperationMessageAsync(state, cancellationToken: TestCancelled));
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");

        var result = await repository.ContinueAsync(state, "Edited group message\n\n#9 still here\n", cancellationToken: TestCancelled);

        // The group is one commit with the typed message (the amend read the file the app wrote),
        // T's author date, T's resolution and F's file.
        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal(["P plain", "Edited group message", "Main edit"], await GitLinesAsync(repo, "log", "--format=%s", "-3"));
        Assert.Equal("Edited group message\n\n#9 still here", await GitTextAsync(repo, "log", "-1", "--format=%B", "HEAD~1"));
        Assert.Equal(await GitTextAsync(repo, "log", "-1", "--format=%at", commits[0]), await GitTextAsync(repo, "log", "-1", "--format=%at", "HEAD~1"));
        Assert.Equal(["f.txt", "h.txt"], await GitLinesAsync(repo, "diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD~1"));
        Assert.Equal("resolved\n", await ShowFileAsync(repo, "HEAD~1", "f.txt"));
    }

    [Fact]
    public async Task Continue_at_a_groups_member_lets_the_groups_amend_restore_its_message()
    {
        var (repo, main, commits) = await TopicAsync(("t.txt", "T target"), ("f.txt", "F1 conflicts"), ("h.txt", "F2 more"));
        using var _repo = repo;
        var repository = await OpenAsync(repo);
        Assert.Equal(1, (await InteractiveRebaseAsync(repo, main, Group(1, commits[0], commits[1], commits[2]), "Group message\n\n#9 kept\n")).ExitCode);
        var state = (await repository.ReadStatusAsync(TestCancelled)).InProgress;
        Assert.Equal((RebaseStopKind.Pick, false, (int?)1), (state.StopKind!.Value, state.NextIsSquash, state.Group));
        Assert.Equal("Group message\n\n#9 kept", await repository.ReadOperationMessageAsync(state, cancellationToken: TestCancelled));
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");

        var result = await repository.ContinueAsync(state, null, cancellationToken: TestCancelled);

        // git rebase --continue for a fixup stop; the group's amend restores the message.
        Assert.Equal(ContinueOutcome.Done, result.Outcome);
        Assert.Equal("Group message\n\n#9 kept", await GitTextAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal(main, await RevAsync(repo, "HEAD~1"));
        Assert.Equal(["f.txt", "h.txt", "t.txt"], await GitLinesAsync(repo, "diff-tree", "--no-commit-id", "--name-only", "-r", "HEAD"));
    }

    /// <summary>
    /// main and topic from "Initial commit" (f.txt "base\n"): main changes f.txt ("Main edit");
    /// topic changes it too, as "Zoë O'Brien" &lt;zoe@example.com&gt; at <c>@1767272400 +0230</c>, with
    /// the message "Topic edit\n\n#3 from the commit" kept verbatim. A pre-commit hook refuses every
    /// commit. HEAD on topic; returns main's tip.
    /// </summary>
    private static async Task<(TempRepo Repo, string Main)> TopicByAnotherAuthorAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
            await repo.CreateBranchAsync("topic");
            var main = await repo.CommitFileAsync("f.txt", "main\n", "Main edit");
            await repo.CheckoutAsync("topic");
            repo.WriteFile("f.txt", "topic\n");
            await repo.GitAsync("add", "f.txt");
            var author = new Dictionary<string, string?>
            {
                ["GIT_AUTHOR_NAME"] = "Zoë O'Brien",
                ["GIT_AUTHOR_EMAIL"] = "zoe@example.com",
                ["GIT_AUTHOR_DATE"] = "@1767272400 +0230",
            };
            (await GitWithEnvironmentAsync(repo, author, "commit", "--quiet", "--cleanup=verbatim", "--message", "Topic edit\n\n#3 from the commit")).EnsureSuccess(new GitCommand("commit"));
            WriteHook(repo, "pre-commit", "echo 'the pre-commit hook refuses' >&2\nexit 1");
            return (repo, main);
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Rewords T through the app's group 1 ("T reworded\n\n#4 kept\n"), then picks P, with a
    /// commit-msg hook that refuses while <c>.git/refuse</c> exists: the guarded amend fails and the
    /// rebase stops after T.
    /// </summary>
    private static async Task StopOnRefusedAmendAsync(TempRepo repo, string main, IReadOnlyList<string> commits)
    {
        WriteHook(repo, "commit-msg", RefusingWhileMarked);
        File.WriteAllText(Path.Combine(repo.Path, ".git", "refuse"), string.Empty);
        var result = await InteractiveRebaseAsync(repo, main, Group(1, commits[0]) + $"pick {commits[1]}\n", "T reworded\n\n#4 kept\n");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("the hook refuses this message", result.StandardError, StringComparison.Ordinal);
    }
}
