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

    [Theory]
    [MemberData(nameof(RecordedTexts))]
    public void A_recorded_text_gives_its_kind(string command, string standardError, GitFailureKind kind)
    {
        Assert.False(string.IsNullOrEmpty(command));
        Assert.Equal(kind, GitFailureClassifier.Classify(standardError, string.Empty, 1));
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
}
