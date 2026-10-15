using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitFailureClassifier"/> (phase 3 design 1.6): texts recorded from Git 2.36 on the
/// Windows machine, and failures that real git produces while the test runs, so that a change of
/// git's wording on the version CI runs shows up here. The push rejections and leases are also
/// classified in <see cref="RemoteOperationTests"/>.
/// </summary>
public class FailureClassifierTests
{
    private const string Origin = @"C:\Users\me\AppData\Local\Temp\VisualCommit.Tests\sync-8e1212a11f34\origin.git";

    public static TheoryData<string, string, GitFailureKind> RecordedTexts => new()
    {
        {
            "git switch --no-guess feature/other",
            "error: Your local changes to the following files would be overwritten by checkout:\n\tnotes.txt\n" +
            "Please commit your changes or stash them before you switch branches.\nAborting\n",
            GitFailureKind.CheckoutBlockedByLocalChanges
        },
        {
            "git switch --no-guess feature/addfile (both lists)",
            "error: Your local changes to the following files would be overwritten by checkout:\n\tnotes.txt\n" +
            "Please commit your changes or stash them before you switch branches.\n" +
            "error: The following untracked working tree files would be overwritten by checkout:\n\tnewfile.txt\n" +
            "Please move or remove them before you switch branches.\nAborting\n",
            GitFailureKind.CheckoutBlockedByUntrackedFiles
        },
        {
            "git switch (untracked only)",
            "error: The following untracked working tree files would be overwritten by checkout:\n\tnewfile.txt\n" +
            "Please move or remove them before you switch branches.\nAborting\n",
            GitFailureKind.CheckoutBlockedByUntrackedFiles
        },
        {
            "git push (fetch first)",
            $"To {Origin}\n ! [rejected]        main -> main (fetch first)\nerror: failed to push some refs to '{Origin}'\n" +
            "hint: Updates were rejected because the remote contains work that you do\nhint: not have locally. This is usually caused by another repository pushing\n",
            GitFailureKind.PushRejected
        },
        {
            "git push (non-fast-forward)",
            $"To {Origin}\n ! [rejected]        main -> main (non-fast-forward)\nerror: failed to push some refs to '{Origin}'\n" +
            "hint: Updates were rejected because the tip of your current branch is behind\nhint: its remote counterpart. Integrate the remote changes (e.g.\n",
            GitFailureKind.PushRejected
        },
        {
            "git push --force-with-lease (stale info)",
            $"To {Origin}\n ! [rejected]        published -> published (stale info)\nerror: failed to push some refs to '{Origin}'\n",
            GitFailureKind.PushRejectedStale
        },
        {
            "git push --force-with-lease --force-if-includes",
            $"To {Origin}\n ! [rejected]        published -> published (remote ref updated since checkout)\nerror: failed to push some refs to '{Origin}'\n" +
            "hint: Updates were rejected because the tip of the remote-tracking\nhint: branch has been updated since the last checkout. You may want\n" +
            "hint: to integrate those changes locally (e.g., 'git pull ...')\nhint: before forcing an update.\n",
            GitFailureKind.PushRejectedStale
        },
        {
            "git push --force-with-lease=<ref>:<sha> --delete",
            $"To {Origin}\n ! [rejected]        (delete) -> published (stale info)\nerror: failed to push some refs to '{Origin}'\n",
            GitFailureKind.PushRejectedStale
        },
        {
            "git push --force-with-lease=refs/tags/<t>:<sha> --delete, no such tag there",
            $"To {Origin}\n ! [rejected]        (delete) -> v1.1 (stale info)\nerror: failed to push some refs to '{Origin}'\n",
            GitFailureKind.PushRejectedStale
        },
        {
            "git push, a hook refused",
            $"To {Origin}\n ! [remote rejected] main -> main (pre-receive hook declined)\nerror: failed to push some refs to '{Origin}'\n",
            GitFailureKind.Other
        },
        {
            "git fetch . main:side, not a push",
            "From .\n ! [rejected]        main       -> side  (non-fast-forward)\n",
            GitFailureKind.Other
        },
        {
            "git fetch over https, wrong password",
            "remote: Unauthorized\nfatal: Authentication failed for 'http://127.0.0.1:52817/remote.git/'\n",
            GitFailureKind.AuthenticationFailed
        },
        {
            "git fetch over https, no prompt",
            "fatal: could not read Username for 'http://127.0.0.1:52817': terminal prompts disabled\n",
            GitFailureKind.AuthenticationFailed
        },
        {
            "git fetch over https, no password",
            "fatal: could not read Password for 'http://alice@127.0.0.1:52817': terminal prompts disabled\n",
            GitFailureKind.AuthenticationFailed
        },
        {
            "git fetch over ssh, key refused",
            "git@github.com: Permission denied (publickey).\r\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\n",
            GitFailureKind.AuthenticationFailed
        },
        {
            "git fetch over ssh, unknown host denied",
            "Host key verification failed.\r\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\n",
            GitFailureKind.AuthenticationFailed
        },
        {
            "git merge --ff-only, diverged",
            "fatal: Not possible to fast-forward, aborting.\n",
            GitFailureKind.NotFastForward
        },
        {
            "git add while another git runs",
            "fatal: Unable to create 'C:/Users/me/repo/.git/index.lock': File exists.\n\n" +
            "Another git process seems to be running in this repository, e.g.\nan editor opened by 'git commit'. Please make sure all processes\n",
            GitFailureKind.IndexLocked
        },
        {
            "git fetch . main:side, side checked out in a linked worktree",
            "fatal: refusing to fetch into branch 'refs/heads/side' checked out at 'C:/Users/me/AppData/Local/Temp/edges/edges-wt'\n",
            GitFailureKind.Other
        },
        {
            "git pull with nothing set, diverged (the app merges instead, D78)",
            "From .\n * branch            main       -> FETCH_HEAD\nhint: You have divergent branches and need to specify how to reconcile them.\n" +
            "fatal: Need to specify how to reconcile divergent branches.\n",
            GitFailureKind.Other
        },
        {
            "git switch during a merge",
            "fatal: cannot switch branch while merging\nConsider \"git merge --quit\" or \"git worktree add\".\n",
            GitFailureKind.Other
        },
        {
            "git fetch of a remote that is gone",
            "fatal: 'nosuch' does not appear to be a git repository\nfatal: Could not read from remote repository.\n\n" +
            "Please make sure you have the correct access rights\nand the repository exists.\n",
            GitFailureKind.Other
        },
    };

    /// <summary>
    /// Refusals for changes in the way (docs/phase-5-design.md 0.7, 9.3), recorded from Git 2.36 and
    /// 2.55 on the "protect" scenario (the report's TP1 to TP12): standard error, standard output,
    /// the kind and the paths the error dialog counts.
    /// </summary>
    public static TheoryData<string, string, string, GitFailureKind, string[]> RecordedRefusals => new()
    {
        {
            "TP1, git switch --no-guess other (2.36 and 2.55)",
            "error: Your local changes to the following files would be overwritten by checkout:\n\tsrc/app.txt\n" +
            "Please commit your changes or stash them before you switch branches.\nAborting\n",
            string.Empty,
            GitFailureKind.CheckoutBlockedByLocalChanges,
            ["src/app.txt"]
        },
        {
            "TP3, three changed files",
            "error: Your local changes to the following files would be overwritten by checkout:\n\tdocs/guide.txt\n\tsrc/app.txt\n\tsrc/lib.txt\n" +
            "Please commit your changes or stash them before you switch branches.\nAborting\n",
            string.Empty,
            GitFailureKind.CheckoutBlockedByLocalChanges,
            ["docs/guide.txt", "src/app.txt", "src/lib.txt"]
        },
        {
            "TP4, an untracked file",
            "error: The following untracked working tree files would be overwritten by checkout:\n\tnotes\n" +
            "Please move or remove them before you switch branches.\nAborting\n",
            string.Empty,
            GitFailureKind.CheckoutBlockedByUntrackedFiles,
            ["notes"]
        },
        {
            "TP5, a folder with an untracked file (the empty line before Aborting)",
            "error: Updating the following directories would lose untracked files in them:\n\tnotes\n\nAborting\n",
            string.Empty,
            GitFailureKind.CheckoutBlockedByUntrackedFolder,
            ["notes"]
        },
        {
            "a changed file and a folder",
            "error: Your local changes to the following files would be overwritten by checkout:\n\tsrc/app.txt\n" +
            "Please commit your changes or stash them before you switch branches.\n" +
            "error: Updating the following directories would lose untracked files in them:\n\tnotes\n\nAborting\n",
            string.Empty,
            GitFailureKind.CheckoutBlockedByUntrackedFolder,
            ["src/app.txt", "notes"]
        },
        {
            "a changed file and an untracked one, one Aborting (design 0.7)",
            "error: Your local changes to the following files would be overwritten by checkout:\n\tsrc/app.txt\n" +
            "Please commit your changes or stash them before you switch branches.\n" +
            "error: The following untracked working tree files would be overwritten by checkout:\n\tnotes\n" +
            "Please move or remove them before you switch branches.\nAborting\n",
            string.Empty,
            GitFailureKind.CheckoutBlockedByUntrackedFiles,
            ["src/app.txt", "notes"]
        },
        {
            "an untracked file where the target has a folder",
            "error: The following untracked working tree files would be removed by checkout:\n\tdocs\n" +
            "Please move or remove them before you switch branches.\nAborting\n",
            string.Empty,
            GitFailureKind.CheckoutBlockedByUntrackedFiles,
            ["docs"]
        },
        {
            "TP6, an ignored file with --no-overwrite-ignore",
            "error: The following untracked working tree files would be overwritten by checkout:\n\tsettings.local.json\n" +
            "Please move or remove them before you switch branches.\nAborting\n",
            string.Empty,
            GitFailureKind.CheckoutBlockedByUntrackedFiles,
            ["settings.local.json"]
        },
        {
            "TP7, git merge over a changed file (2.36: exit 128)",
            "error: Your local changes to the following files would be overwritten by merge:\n\tsrc/app.txt\n" +
            "Please commit your changes or stash them before you merge.\nAborting\n",
            string.Empty,
            GitFailureKind.MergeBlockedByLocalChanges,
            ["src/app.txt"]
        },
        {
            "TP7 on 2.55 (exit 2, a fifth line)",
            "error: Your local changes to the following files would be overwritten by merge:\n\tsrc/app.txt\n" +
            "Please commit your changes or stash them before you merge.\nAborting\nMerge with strategy ort failed.\n",
            string.Empty,
            GitFailureKind.MergeBlockedByLocalChanges,
            ["src/app.txt"]
        },
        {
            "git merge over an untracked file",
            "error: The following untracked working tree files would be overwritten by merge:\n\tnotes\n" +
            "Please move or remove them before you merge.\nAborting\n",
            string.Empty,
            GitFailureKind.MergeBlockedByLocalChanges,
            ["notes"]
        },
        {
            "TP8, git cherry-pick over a changed file",
            "error: Your local changes to the following files would be overwritten by merge:\n\tsrc/app.txt\n" +
            "Please commit your changes or stash them before you merge.\nAborting\nfatal: cherry-pick failed\n",
            string.Empty,
            GitFailureKind.MergeBlockedByLocalChanges,
            ["src/app.txt"]
        },
        {
            "TP12, git revert over a changed file",
            "error: Your local changes to the following files would be overwritten by merge:\n\tREADME.md\n" +
            "Please commit your changes or stash them before you merge.\nAborting\nfatal: revert failed\n",
            string.Empty,
            GitFailureKind.MergeBlockedByLocalChanges,
            ["README.md"]
        },
        {
            "TP9, git merge over a staged file on 2.36 (standard output, no error:, no final line feed)",
            string.Empty,
            "Your local changes to the following files would be overwritten by merge:\n  README.md",
            GitFailureKind.MergeBlockedByStagedChanges,
            []
        },
        {
            "TP9 on 2.55 (exit 2, the same first line as TP7's)",
            "error: Your local changes to the following files would be overwritten by merge:\n  README.md\nMerge with strategy ort failed.\n",
            string.Empty,
            GitFailureKind.MergeBlockedByStagedChanges,
            []
        },
        {
            "TP10, git cherry-pick over a staged file",
            "error: your local changes would be overwritten by cherry-pick.\nhint: commit your changes or stash them to proceed.\nfatal: cherry-pick failed\n",
            string.Empty,
            GitFailureKind.MergeBlockedByStagedChanges,
            []
        },
        {
            "TP11, git revert over a staged file",
            "error: your local changes would be overwritten by revert.\nhint: commit your changes or stash them to proceed.\nfatal: revert failed\n",
            string.Empty,
            GitFailureKind.MergeBlockedByStagedChanges,
            []
        },
        {
            "git switch with a conflicted file",
            "error: you need to resolve your current index first\n",
            "notes.txt: needs merge\n",
            GitFailureKind.Other,
            []
        },
    };

    [Theory]
    [MemberData(nameof(RecordedTexts))]
    public void A_recorded_text_gives_its_kind(string command, string standardError, GitFailureKind kind)
    {
        Assert.False(string.IsNullOrEmpty(command));
        Assert.Equal(kind, GitFailureClassifier.Classify(standardError, string.Empty, 1));
    }

    [Theory]
    [MemberData(nameof(RecordedRefusals))]
    public void A_recorded_refusal_gives_its_kind_and_the_paths_under_gits_headers(string command, string standardError, string standardOutput, GitFailureKind kind, string[] paths)
    {
        Assert.False(string.IsNullOrEmpty(command));
        Assert.Equal(kind, GitFailureClassifier.Classify(standardError, standardOutput, 1));
        Assert.Equal(paths, GitFailureClassifier.BlockedPaths(standardError + "\n" + standardOutput));

        // The error dialog's failure reads them from its cleaned output (design 1.6), the same paths.
        var failure = new GitFailure(kind, "x failed.", GitOutputCleaner.Clean(standardError, standardOutput), "git x", 1);
        Assert.Equal(paths, failure.BlockedPaths);
    }

    [Fact]
    public void Every_path_counts_once_names_keep_their_spaces_and_accents_and_a_line_that_is_no_path_ends_a_block()
    {
        var many = Enumerable.Range(1, 43).Select(n => $"src/file {n:00}.txt").ToArray();
        var text =
            "error: Your local changes to the following files would be overwritten by checkout:\n" +
            string.Concat(many.Select(path => "\t" + path + "\n")) +
            "\tdocs/café notes.md\n" +
            "Please commit your changes or stash them before you switch branches.\n" +
            "\tnot/a/path.txt\n" +
            "error: The following untracked working tree files would be overwritten by checkout:\n" +
            "\tsrc/file 01.txt\n\tnew file.txt\n" +
            "Please move or remove them before you switch branches.\r\n" +
            "error: Updating the following directories would lose untracked files in them:\r\n\tbuild\r\n\r\nAborting\n";

        Assert.Equal([.. many, "docs/café notes.md", "new file.txt", "build"], GitFailureClassifier.BlockedPaths(text));
        Assert.Equal(46, GitFailureClassifier.BlockedPaths(text).Count);
        Assert.Empty(GitFailureClassifier.BlockedPaths("fatal: cannot switch branch while merging\n"));
        // Failures without a block still compare equal as records.
        Assert.Equal(new GitFailure(GitFailureKind.Other, "x", "out", "git x", 1), new GitFailure(GitFailureKind.Other, "x", "out", "git x", 1));
        Assert.Throws<ArgumentNullException>(() => GitFailureClassifier.BlockedPaths(null!));
    }

    [Fact]
    public void Standard_output_is_read_too_and_empty_texts_are_other()
    {
        Assert.Equal(GitFailureKind.NotFastForward, GitFailureClassifier.Classify(string.Empty, "fatal: Not possible to fast-forward, aborting.\n", 128));
        Assert.Equal(GitFailureKind.Other, GitFailureClassifier.Classify(string.Empty, string.Empty, null));
        Assert.Throws<ArgumentNullException>(() => GitFailureClassifier.Classify(null!, string.Empty, 1));
    }

    [Fact]
    public async Task Real_checkouts_blocked_by_tracked_and_by_untracked_files()
    {
        using var blocked = await Scenarios.CheckoutBlockedAsync();

        var tracked = await blocked.GitAllowingFailureAsync("switch", "--no-guess", "feature/other");
        Assert.False(tracked.Succeeded);
        Assert.Equal(GitFailureKind.CheckoutBlockedByLocalChanges, GitFailureClassifier.Classify(tracked.StandardError, tracked.StandardOutput, tracked.ExitCode));

        var both = await blocked.GitAllowingFailureAsync("switch", "--no-guess", "feature/addfile");
        Assert.False(both.Succeeded);
        Assert.Equal(GitFailureKind.CheckoutBlockedByUntrackedFiles, GitFailureClassifier.Classify(both.StandardError, both.StandardOutput, both.ExitCode));
    }

    [Fact]
    public async Task Real_checkouts_of_the_protect_scenario_give_each_kind_with_gits_paths()
    {
        // The report's TP1, TP3, TP4 and TP5, on the git that runs the tests.
        await AssertRefusedAsync(repo => repo.WriteFile("src/app.txt", "app\nlocal\n"), GitFailureKind.CheckoutBlockedByLocalChanges, ["src/app.txt"], "switch", "--no-guess", "other");
        await AssertRefusedAsync(
            repo => repo.WriteFile("src/app.txt", "app\nlocal\n").WriteFile("src/lib.txt", "lib\nlocal\n").WriteFile("docs/guide.txt", "guide\nlocal\n"),
            GitFailureKind.CheckoutBlockedByLocalChanges,
            ["docs/guide.txt", "src/app.txt", "src/lib.txt"],
            "switch",
            "--no-guess",
            "other");
        await AssertRefusedAsync(repo => repo.WriteFile("notes", "my own notes\n"), GitFailureKind.CheckoutBlockedByUntrackedFiles, ["notes"], "switch", "--no-guess", "other");
        await AssertRefusedAsync(repo => repo.WriteFile("notes/today.txt", "today\n"), GitFailureKind.CheckoutBlockedByUntrackedFolder, ["notes"], "switch", "--no-guess", "other");
        await AssertRefusedAsync(
            repo => repo.WriteFile("src/app.txt", "app\nlocal\n").WriteFile("notes", "my own notes\n"),
            GitFailureKind.CheckoutBlockedByUntrackedFiles,
            ["src/app.txt", "notes"],
            "switch",
            "--no-guess",
            "other");

        // TP6: the ignored settings an old commit tracks, with --no-overwrite-ignore.
        await AssertRefusedAsync(_ => { }, GitFailureKind.CheckoutBlockedByUntrackedFiles, ["settings.local.json"], "switch", "--detach", "--no-overwrite-ignore", "main~3");
    }

    [Fact]
    public async Task Real_merges_picks_and_reverts_refused_for_changes_give_the_merge_kinds_on_any_git()
    {
        // The report's TP7, TP8, TP12 (a changed file: git's "by merge" block) and TP9 to TP11
        // (a staged one: no names), each changing nothing; 2.55's merge exits 2 with a line more.
        await AssertRefusedAsync(repo => repo.WriteFile("src/app.txt", "app\nlocal\n"), GitFailureKind.MergeBlockedByLocalChanges, ["src/app.txt"], "merge", "--no-edit", "refs/heads/other");
        await AssertRefusedAsync(repo => repo.WriteFile("src/app.txt", "app\nlocal\n"), GitFailureKind.MergeBlockedByLocalChanges, ["src/app.txt"], "cherry-pick", "refs/heads/other~1");
        await AssertRefusedAsync(repo => repo.WriteFile("notes", "my own notes\n"), GitFailureKind.MergeBlockedByLocalChanges, ["notes"], "merge", "--no-edit", "refs/heads/other");
        await AssertRefusedAsync(
            repo => repo.WriteFile("README.md", "# Protect\n\nLocal.\n").WriteFile("src/app.txt", "app\nlocal\n"),
            GitFailureKind.MergeBlockedByLocalChanges,
            ["README.md"],
            "revert",
            "--no-edit",
            "refs/heads/main~1");

        static void Staged(TempRepo repo) => repo.WriteFile("README.md", "# Protect\n\nStaged.\n");
        await AssertRefusedAsync(Staged, GitFailureKind.MergeBlockedByStagedChanges, [], "merge", "--no-edit", "refs/heads/other");
        await AssertRefusedAsync(Staged, GitFailureKind.MergeBlockedByStagedChanges, [], "cherry-pick", "refs/heads/other~1");
        await AssertRefusedAsync(Staged, GitFailureKind.MergeBlockedByStagedChanges, [], "revert", "--no-edit", "refs/heads/main~1");
    }

    [Fact]
    public async Task A_real_fast_forward_that_is_not_possible_and_a_real_index_lock()
    {
        using var ops = await Scenarios.OperationsAsync();

        var merge = await ops.GitAllowingFailureAsync("merge", "--ff-only", "refs/heads/feature/conflict");
        Assert.False(merge.Succeeded);
        Assert.Equal(GitFailureKind.NotFastForward, GitFailureClassifier.Classify(merge.StandardError, merge.StandardOutput, merge.ExitCode));

        var lockFile = Path.Combine(ops.Path, ".git", "index.lock");
        await File.WriteAllTextAsync(lockFile, string.Empty, TestCancelled);
        ops.WriteFile("README.md", "# Changed\n");
        var add = await ops.GitAllowingFailureAsync("add", "README.md");
        File.Delete(lockFile);
        Assert.False(add.Succeeded);
        Assert.Equal(GitFailureKind.IndexLocked, GitFailureClassifier.Classify(add.StandardError, add.StandardOutput, add.ExitCode));
    }

    [Fact]
    public async Task A_real_fetch_refused_by_a_server_that_asks_for_a_password()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var repository = await OpenAsync(repo);

        // No askpass program of the machine may answer, and no proxy may carry the request.
        var options = new NetworkOptions
        {
            Environment = new Dictionary<string, string?>
            {
                ["GIT_ASKPASS"] = null,
                ["SSH_ASKPASS"] = null,
                ["http_proxy"] = null,
                ["https_proxy"] = null,
                ["no_proxy"] = "127.0.0.1,localhost",
            },
        };
        var failure = await Assert.ThrowsAsync<GitException>(() => repository.FetchAsync("origin", options, cancellationToken: TestCancelled));

        Assert.Equal(GitFailureKind.AuthenticationFailed, GitFailureClassifier.Classify(failure.StandardError, failure.StandardOutput, failure.ExitCode));
    }

    /// <summary>
    /// On a fresh copy of the protect scenario with <paramref name="change"/> made (a staged change
    /// when the change writes README.md "Staged."): git refuses <paramref name="arguments"/>, the
    /// classifier gives <paramref name="kind"/> and <paramref name="paths"/>, and nothing changed:
    /// HEAD, the refs, the index, the status and no operation in progress (design 0.7).
    /// </summary>
    private static async Task AssertRefusedAsync(Action<TempRepo> change, GitFailureKind kind, string[] paths, params string[] arguments)
    {
        using var repo = await Scenarios.ProtectAsync();
        change(repo);
        if (File.ReadAllText(Path.Combine(repo.Path, "README.md")).EndsWith("Staged.\n", StringComparison.Ordinal))
        {
            await repo.GitAsync("add", "--", "README.md");
        }

        var before = await StateAsync(repo);
        var refused = await repo.GitAllowingFailureAsync(arguments);

        Assert.False(refused.Succeeded, $"git {string.Join(' ', arguments)} was not refused.");
        Assert.Equal(kind, GitFailureClassifier.Classify(refused.StandardError, refused.StandardOutput, refused.ExitCode));
        Assert.Equal(paths, GitFailureClassifier.BlockedPaths(refused.StandardError + "\n" + refused.StandardOutput));
        Assert.Equal(paths, new GitFailure(kind, "x failed.", GitOutputCleaner.Clean(refused.StandardError, refused.StandardOutput), "git x", refused.ExitCode).BlockedPaths);
        Assert.Equal(before, await StateAsync(repo));
        foreach (var name in new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "sequencer" })
        {
            Assert.False(Path.Exists(Path.Combine(repo.Path, ".git", name)), $"git left {name} after refusing.");
        }
    }

    /// <summary>HEAD, the refs, the index's entries and the status with the ignored files, as one text.</summary>
    private static async Task<string> StateAsync(TempRepo repo) => string.Join(
        '\n',
        (await repo.GitAsync("rev-parse", "HEAD")).StandardOutput,
        (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput,
        (await repo.GitAsync("ls-files", "--stage")).StandardOutput,
        (await repo.GitAsync("status", "--porcelain=v2", "--untracked-files=all", "--ignored")).StandardOutput);
}
