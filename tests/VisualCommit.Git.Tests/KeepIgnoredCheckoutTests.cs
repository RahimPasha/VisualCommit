using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Keep ignored files' git option on the checkouts and the merges (D193, D221;
/// docs/phase-6-design.md 9.4, 9.11, 9.17; the facts of 0.4 and 0.5), run by the app's
/// <see cref="GitRepository"/> against real git: with <c>keepIgnored</c> every checkout form
/// refuses each shape of an ignored file in its way and changes nothing; without it git's default
/// overwrites or deletes them; a merge's option refuses a fast-forward and does nothing for a real
/// merge; <c>noTrack</c> makes a branch without an upstream where git would give it the wrong one
/// or fail.
/// </summary>
public class KeepIgnoredCheckoutTests
{
    private const string Local = "LOCAL\n";

    /// <summary>The repository every test starts from, built once and copied per test.</summary>
    private static readonly Lazy<Task<TempRepo>> Template = new(BuildAsync);

    /// <summary>
    /// The four shapes of 0.4, one target branch each, all from <c>base</c> (README): S1X,
    /// <c>t-secret</c> tracks <c>secret.env</c>; S2, <c>t-build</c> has a file <c>build</c>; S3,
    /// <c>t-logs</c> has <c>logs/a.txt</c>; S4, <c>t-modules</c> force-adds
    /// <c>node_modules/pkg/index.js</c>; <c>t-all</c> has all four. <c>main</c> adds
    /// <c>main.txt</c> after <c>base</c> (so a merge of a target is a real merge), <c>ff-base</c>
    /// stays at <c>base</c> (a fast-forward), <c>-dash</c> is at <c>t-secret</c>, and the remote
    /// <c>mirror</c> (the repository itself) has every branch. <c>.git/info/exclude</c> ignores
    /// <c>secret.env</c>, <c>build/</c>, <c>logs</c> and <c>node_modules/</c>, and on disk on
    /// <c>main</c> are the ignored <c>secret.env</c>, <c>build/out.bin</c>, the file <c>logs</c>
    /// and <c>node_modules/pkg/index.js</c>.
    /// </summary>
    private static async Task<TempRepo> BuildAsync()
    {
        var repo = await TempRepo.CreateAsync("keep");
        await repo.CommitFileAsync("README", "base\n", "base");
        foreach (var (branch, files) in new (string, (string Path, string Content)[])[]
        {
            ("t-secret", [("secret.env", "T\n")]),
            ("t-build", [("build", "F\n")]),
            ("t-logs", [("logs/a.txt", "A\n")]),
            ("t-modules", [("node_modules/pkg/index.js", "T\n")]),
            ("t-all", [("secret.env", "T\n"), ("build", "F\n"), ("logs/a.txt", "A\n"), ("node_modules/pkg/index.js", "T\n")]),
        })
        {
            await repo.GitAsync("switch", "--quiet", "--create", branch, "main");
            foreach (var (path, content) in files)
            {
                repo.WriteFile(path, content);
            }

            await repo.CommitAsync("Add to " + branch);
        }

        await repo.GitAsync("switch", "--quiet", "main");
        await repo.CreateBranchAsync("ff-base");
        await repo.CommitFileAsync("main.txt", "main\n", "Main work");
        await repo.GitAsync("update-ref", "refs/heads/-dash", "refs/heads/t-secret");
        await repo.GitAsync("remote", "add", "mirror", repo.Path);
        await repo.GitAsync("fetch", "--quiet", "mirror");
        await File.AppendAllTextAsync(Path.Combine(repo.Path, ".git", "info", "exclude"), "secret.env\nbuild/\nlogs\nnode_modules/\n");
        WriteIgnoredFiles(repo);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => repo.Dispose();
        return repo;
    }

    private static void WriteIgnoredFiles(TempRepo repo) =>
        repo.WriteFile("secret.env", Local).WriteFile("build/out.bin", "OUT\n").WriteFile("logs", "LOGS\n").WriteFile("node_modules/pkg/index.js", Local);

    private static async Task<TempRepo> CopyAsync() =>
        await (await HangWatchdogAttribute.SharedPreparationAsync("the keep-ignored checkout template", Template.Value)).CopyAsync();

    /// <summary>Each shape's target, the failure git's refusal classifies as, and the paths it names.</summary>
    public static TheoryData<string, GitFailureKind, string> Shapes => new()
    {
        { "t-secret", GitFailureKind.CheckoutBlockedByUntrackedFiles, "secret.env" },
        { "t-build", GitFailureKind.CheckoutBlockedByUntrackedFolder, "build" },
        { "t-logs", GitFailureKind.CheckoutBlockedByUntrackedFiles, "logs" },
        { "t-modules", GitFailureKind.CheckoutBlockedByUntrackedFiles, "node_modules/pkg/index.js" },
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task Every_checkout_form_with_the_option_refuses_the_shape_and_changes_nothing(string target, GitFailureKind kind, string blocked)
    {
        using var repo = await CopyAsync();
        var repository = await OpenAsync(repo);
        var sha = await repo.RefTargetAsync("refs/heads/" + target);
        var before = await repo.ExactStateAsync();

        await AssertRefusedAsync(() => repository.CheckoutBranchAsync(target, keepIgnored: true, cancellationToken: TestCancelled), $"git switch --no-guess --no-overwrite-ignore {target}");
        await AssertRefusedAsync(
            () => repository.CheckoutTrackingBranchAsync("x-" + target, "refs/remotes/mirror/" + target, keepIgnored: true, cancellationToken: TestCancelled),
            $"git switch --create x-{target} --track --no-overwrite-ignore refs/remotes/mirror/{target}");
        await AssertRefusedAsync(() => repository.CheckoutDetachedAsync(sha!, keepIgnored: true, cancellationToken: TestCancelled), $"git switch --detach --no-overwrite-ignore {sha}");
        await AssertRefusedAsync(
            () => repository.CreateBranchAsync("y-" + target, "refs/heads/" + target, checkout: true, noTrack: false, keepIgnored: true, cancellationToken: TestCancelled),
            $"git switch --create y-{target} --no-overwrite-ignore refs/heads/{target}");
        await AssertRefusedAsync(
            () => repository.CreateBranchAsync("z-" + target, "refs/remotes/mirror/" + target, checkout: true, noTrack: true, keepIgnored: true, cancellationToken: TestCancelled),
            $"git switch --create z-{target} --no-track --no-overwrite-ignore refs/remotes/mirror/{target}");

        // Nothing changed: HEAD, every ref (no branch from --create), the index, the status with
        // the ignored files, and every file's bytes.
        Assert.Equal(before, await repo.ExactStateAsync());

        async Task AssertRefusedAsync(Func<Task> checkout, string command)
        {
            var refused = await Assert.ThrowsAsync<GitException>(checkout);
            Assert.Equal(1, refused.ExitCode);
            Assert.Equal(command, refused.CommandText);
            Assert.Equal(kind, GitFailureClassifier.Classify(refused.StandardError, refused.StandardOutput, refused.ExitCode));
            Assert.Equal([blocked], GitFailureClassifier.BlockedPaths(refused.StandardError));
        }
    }

    [Fact]
    public async Task The_option_refuses_also_over_a_file_with_the_targets_own_bytes_and_for_a_branch_named_with_a_dash()
    {
        using var repo = await CopyAsync();
        repo.WriteFile("secret.env", "T\n");
        var repository = await OpenAsync(repo);
        var before = await repo.ExactStateAsync();

        var same = await Assert.ThrowsAsync<GitException>(() => repository.CheckoutBranchAsync("t-secret", keepIgnored: true, cancellationToken: TestCancelled));
        Assert.Equal(["secret.env"], GitFailureClassifier.BlockedPaths(same.StandardError));

        // The option stands before --end-of-options, which keeps the name from being read as one.
        var dash = await Assert.ThrowsAsync<GitException>(() => repository.CheckoutBranchAsync("-dash", keepIgnored: true, cancellationToken: TestCancelled));
        Assert.Equal("git switch --no-guess --no-overwrite-ignore --end-of-options -dash", dash.CommandText);
        Assert.Equal(1, dash.ExitCode);
        Assert.Equal(["secret.env"], GitFailureClassifier.BlockedPaths(dash.StandardError));

        Assert.Equal(before, await repo.ExactStateAsync());
    }

    public static TheoryData<string> Forms => new() { "branch", "tracking", "detached", "create" };

    [Theory]
    [MemberData(nameof(Forms))]
    public async Task Without_the_option_every_checkout_form_overwrites_and_deletes_the_ignored_files_as_git_does(string form)
    {
        using var repo = await CopyAsync();
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);

        var command = form switch
        {
            "branch" => await RunAsync(() => repository.CheckoutBranchAsync("t-all", keepIgnored: false, cancellationToken: TestCancelled)),
            "tracking" => await RunAsync(() => repository.CheckoutTrackingBranchAsync("x", "refs/remotes/mirror/t-all", keepIgnored: false, cancellationToken: TestCancelled)),
            "detached" => await RunAsync(async () => await repository.CheckoutDetachedAsync((await repo.RefTargetAsync("refs/heads/t-all"))!, keepIgnored: false, cancellationToken: TestCancelled)),
            _ => await RunAsync(() => repository.CreateBranchAsync("x", "refs/heads/t-all", checkout: true, noTrack: false, keepIgnored: false, cancellationToken: TestCancelled)),
        };

        Assert.DoesNotContain("--no-overwrite-ignore", command, StringComparison.Ordinal);
        Assert.Equal(await repo.RefTargetAsync("refs/heads/t-all"), await repo.HeadAsync());

        // S1X and S4 overwritten, S2's folder deleted for the file, S3's file deleted for the folder.
        Assert.Equal("T\n", File.ReadAllText(Path.Combine(repo.Path, "secret.env")));
        Assert.Equal("T\n", File.ReadAllText(Path.Combine(repo.Path, "node_modules", "pkg", "index.js")));
        Assert.Equal("F\n", File.ReadAllText(Path.Combine(repo.Path, "build")));
        Assert.Equal("A\n", File.ReadAllText(Path.Combine(repo.Path, "logs", "a.txt")));

        async Task<string> RunAsync(Func<Task> checkout)
        {
            var mark = calls.Snapshot().Count;
            await checkout();
            return Assert.Single(calls.Snapshot().Skip(mark), call => call.CommandText.StartsWith("git switch ", StringComparison.Ordinal)).CommandText;
        }
    }

    [Fact]
    public async Task A_merge_with_the_option_refuses_a_fast_forward_and_overwrites_in_a_real_merge()
    {
        using var repo = await CopyAsync();
        await repo.CheckoutAsync("ff-base");
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var before = await repo.ExactStateAsync();

        // A fast-forward: git's option is exact, for both modes that may fast-forward.
        var ff = await Assert.ThrowsAsync<GitException>(() => repository.MergeAsync("refs/heads/t-secret", MergeMode.FastForwardOnly, keepIgnored: true, cancellationToken: TestCancelled));
        Assert.Equal("git merge --ff-only --no-overwrite-ignore refs/heads/t-secret", ff.CommandText);
        Assert.Equal(1, ff.ExitCode);
        Assert.Equal(GitFailureKind.MergeBlockedByLocalChanges, GitFailureClassifier.Classify(ff.StandardError, ff.StandardOutput, ff.ExitCode));
        Assert.Equal(["secret.env"], GitFailureClassifier.BlockedPaths(ff.StandardError));
        var merge = await Assert.ThrowsAsync<GitException>(() => repository.MergeAsync("refs/heads/t-secret", MergeMode.Default, keepIgnored: true, cancellationToken: TestCancelled));
        Assert.Equal("git merge --no-edit --no-log -m \"Merge branch 't-secret' into ff-base\" --no-overwrite-ignore refs/heads/t-secret", merge.CommandText);
        Assert.Equal(["secret.env"], GitFailureClassifier.BlockedPaths(merge.StandardError));
        Assert.Equal(before, await repo.ExactStateAsync());

        // A real merge: the option does nothing, git overwrites the ignored file (0.4).
        await repo.CheckoutAsync("main");
        Assert.Equal(MergeResult.Merged, await repository.MergeAsync("refs/heads/t-secret", MergeMode.NoFastForward, keepIgnored: true, cancellationToken: TestCancelled));
        Assert.Contains(calls.Snapshot(), call => call.CommandText == "git merge --no-edit --no-log -m \"Merge branch 't-secret'\" --no-ff --no-overwrite-ignore refs/heads/t-secret");
        Assert.Equal("T\n", File.ReadAllText(Path.Combine(repo.Path, "secret.env")));
    }

    [Fact]
    public async Task Without_the_option_a_fast_forward_overwrites_the_ignored_file()
    {
        using var repo = await CopyAsync();
        await repo.CheckoutAsync("ff-base");
        var repository = await OpenAsync(repo);

        Assert.Equal(MergeResult.FastForward, await repository.MergeAsync("refs/heads/t-secret", MergeMode.FastForwardOnly, keepIgnored: false, cancellationToken: TestCancelled));

        Assert.Equal("T\n", File.ReadAllText(Path.Combine(repo.Path, "secret.env")));
    }

    [Fact]
    public async Task A_branch_made_without_tracking_under_a_pull_request_line_has_no_upstream_where_git_would_give_it_one()
    {
        // 0.5's P and P3: GitHub's pull-request line beside the default one fetches pr/1 from two
        // refs of origin; git's implicit upstream would be the first, refs/heads/pr/1.
        using var repo = await CopyAsync();
        await repo.GitAsync("remote", "add", "origin", repo.SiblingPath("nowhere.git"));
        await repo.GitAsync("config", "--add", "remote.origin.fetch", "+refs/pull/*/head:refs/remotes/origin/pr/*");
        await repo.GitAsync("update-ref", "refs/remotes/origin/pr/1", "HEAD");
        var calls = new GitCallLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var head = await repo.HeadAsync();

        await repository.CreateBranchAsync("pr/1", "refs/remotes/origin/pr/1", checkout: true, noTrack: true, keepIgnored: true, cancellationToken: TestCancelled);
        Assert.Equal("pr/1", await repo.CurrentBranchAsync());
        await repo.CheckoutAsync("main");
        await repository.CreateBranchAsync("pr-copy", "refs/remotes/origin/pr/1", checkout: false, noTrack: true, keepIgnored: false, cancellationToken: TestCancelled);

        Assert.Contains(calls.Snapshot(), call => call.CommandText == "git switch --create pr/1 --no-track --no-overwrite-ignore refs/remotes/origin/pr/1");
        Assert.Contains(calls.Snapshot(), call => call.CommandText == "git branch --no-track pr-copy refs/remotes/origin/pr/1");
        Assert.Equal(head, await repo.RefTargetAsync("refs/heads/pr/1"));
        Assert.Equal(head, await repo.RefTargetAsync("refs/heads/pr-copy"));
        Assert.Empty(await BranchKeysAsync(repo));

        // Without it, git gives the branch the pull-request line's first candidate as upstream.
        await repository.CreateBranchAsync("review", "refs/remotes/origin/pr/1", checkout: false, noTrack: false, keepIgnored: false, cancellationToken: TestCancelled);
        Assert.Equal(["branch.review.remote origin", "branch.review.merge refs/heads/pr/1"], await BranchKeysAsync(repo));
    }

    [Fact]
    public async Task A_branch_made_without_tracking_under_two_remotes_is_made_where_git_would_fail_and_leave_it_behind()
    {
        // 0.5's H: origin and backup both fetch into refs/remotes/origin/*.
        using var repo = await CopyAsync();
        await repo.GitAsync("remote", "add", "origin", repo.SiblingPath("nowhere.git"));
        await repo.GitAsync("remote", "add", "backup", repo.SiblingPath("nowhere-backup.git"));
        await repo.GitAsync("config", "remote.backup.fetch", "+refs/heads/*:refs/remotes/origin/*");
        await repo.GitAsync("update-ref", "refs/remotes/origin/main", "HEAD");
        var repository = await OpenAsync(repo);
        var head = await repo.HeadAsync();

        await repository.CreateBranchAsync("h-switch", "refs/remotes/origin/main", checkout: true, noTrack: true, keepIgnored: true, cancellationToken: TestCancelled);
        Assert.Equal("h-switch", await repo.CurrentBranchAsync());
        await repo.CheckoutAsync("main");
        await repository.CreateBranchAsync("h-branch", "refs/remotes/origin/main", checkout: false, noTrack: true, keepIgnored: false, cancellationToken: TestCancelled);
        Assert.Equal(head, await repo.RefTargetAsync("refs/heads/h-switch"));
        Assert.Equal(head, await repo.RefTargetAsync("refs/heads/h-branch"));
        Assert.Empty(await BranchKeysAsync(repo));

        // Without it git fails ("not tracking: ambiguous information", 128) and leaves the branch behind.
        var switched = await Assert.ThrowsAsync<GitException>(() => repository.CreateBranchAsync("h-left", "refs/remotes/origin/main", checkout: true, noTrack: false, keepIgnored: true, cancellationToken: TestCancelled));
        var branched = await Assert.ThrowsAsync<GitException>(() => repository.CreateBranchAsync("h-left2", "refs/remotes/origin/main", checkout: false, noTrack: false, keepIgnored: false, cancellationToken: TestCancelled));
        Assert.Equal(128, switched.ExitCode);
        Assert.Equal(128, branched.ExitCode);
        Assert.Equal(head, await repo.RefTargetAsync("refs/heads/h-left"));
        Assert.Equal(head, await repo.RefTargetAsync("refs/heads/h-left2"));
        Assert.Equal("main", await repo.CurrentBranchAsync());
    }

    /// <summary>The repository's <c>branch.&lt;b&gt;.*</c> keys as <c>git config --get-regexp</c> lists them (none: exit 1).</summary>
    private static async Task<IReadOnlyList<string>> BranchKeysAsync(TempRepo repo)
    {
        var result = await repo.GitAllowingFailureAsync("config", "--get-regexp", @"^branch\.");
        return result.ExitCode == 1
            ? []
            : [.. result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }
}
