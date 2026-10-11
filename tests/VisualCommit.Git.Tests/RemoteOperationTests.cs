using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitRepository"/>'s remote operations (phase 3 design 2.16 to 2.23, 2.29) against
/// real git and local bare remotes, in the scenarios of design 7.1: every push form and its
/// refusal, the leases of D100 with <c>--force-if-includes</c> both ways, deletions on a remote,
/// upstreams (a local one and a gone one included), the remotes themselves, and the fetch of all
/// remotes of D109. Git's texts are asserted only where they are stable across versions (the
/// rejected line's reason in parentheses, git's own refusal).
/// </summary>
public class RemoteOperationTests
{
    /// <summary>How long a step that waits on git or the test server may take before the test fails instead of hanging.</summary>
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// No askpass program or proxy of the machine takes part in a call to a remote over http:
    /// the test isolation removes them too once it does (D112); until then these calls do it themselves.
    /// </summary>
    private static readonly NetworkOptions NoPromptsNoProxy = new()
    {
        Environment = new Dictionary<string, string?>
        {
            ["GIT_ASKPASS"] = null,
            ["SSH_ASKPASS"] = null,
            ["http_proxy"] = null,
            ["https_proxy"] = null,
            ["all_proxy"] = null,
            ["HTTP_PROXY"] = null,
            ["HTTPS_PROXY"] = null,
            ["ALL_PROXY"] = null,
            ["no_proxy"] = "127.0.0.1,localhost",
            ["NO_PROXY"] = "127.0.0.1,localhost",
        },
    };

    // Fetch (2.16, D99, D109).

    [Fact]
    public async Task A_fetch_of_one_remote_brings_its_new_commits()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var pushed = await Scenarios.PushFromElsewhereAsync(sync, "main", "src/other.txt", "other\n", "Other work");
        var (repository, calls) = await OpenWithCallsAsync(sync);

        await repository.FetchAsync("origin", NetworkOptions.None, cancellationToken: TestCancelled);

        Assert.Equal(pushed, await sync.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Equal(["git fetch --progress origin"], FetchCalls(calls));
    }

    [Fact]
    public async Task A_fetch_of_all_remotes_fetches_each_in_git_remote_order_and_leaves_out_skipFetchAll()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        await sync.AddBareRemoteAsync("zeta");
        await sync.AddBareRemoteAsync("alpha");
        await sync.ConfigAsync("remote.alpha.skipFetchAll", "yes");
        var pushed = await Scenarios.PushFromElsewhereAsync(sync, "main", "src/other.txt", "other\n", "Other work");
        var (repository, calls) = await OpenWithCallsAsync(sync);

        await repository.FetchAsync(null, NetworkOptions.None, cancellationToken: TestCancelled);

        // git remote lists alpha, origin, zeta; git's maintenance runs once, after the last fetch.
        Assert.Equal(["git fetch --progress --no-auto-maintenance origin", "git fetch --progress zeta"], FetchCalls(calls));
        Assert.Equal(pushed, await sync.RefTargetAsync("refs/remotes/origin/main"));
    }

    [Fact]
    public async Task Auto_fetch_passes_no_auto_maintenance_to_every_fetch()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        await sync.AddBareRemoteAsync("zeta");
        var (repository, calls) = await OpenWithCallsAsync(sync);

        await repository.FetchAsync(null, NetworkOptions.None, noAutoMaintenance: true, TestCancelled);
        await repository.FetchAsync("origin", NetworkOptions.None, noAutoMaintenance: true, TestCancelled);

        Assert.Equal(
            ["git fetch --progress --no-auto-maintenance origin", "git fetch --progress --no-auto-maintenance zeta", "git fetch --progress --no-auto-maintenance origin"],
            FetchCalls(calls));
    }

    [Fact]
    public async Task A_fetch_of_all_remotes_goes_on_after_a_failure_and_names_every_failed_remote_at_the_end()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        await sync.GitAsync("remote", "add", "broken", sync.SiblingPath("no-such-repository.git"));
        await sync.GitAsync("remote", "add", "gone", sync.SiblingPath("gone.git"));
        var pushed = await Scenarios.PushFromElsewhereAsync(sync, "main", "src/other.txt", "other\n", "Other work");
        var (repository, calls) = await OpenWithCallsAsync(sync);

        var failure = await Assert.ThrowsAsync<GitException>(() => repository.FetchAsync(null, NetworkOptions.None, cancellationToken: TestCancelled));

        // The remote after the failures was fetched all the same.
        Assert.Equal(pushed, await sync.RefTargetAsync("refs/remotes/origin/main"));
        Assert.Equal(
            ["git fetch --progress --no-auto-maintenance broken", "git fetch --progress --no-auto-maintenance gone", "git fetch --progress origin"],
            FetchCalls(calls));

        var firstCall = calls.Snapshot().First(call => call.CommandText.StartsWith("git fetch", StringComparison.Ordinal));
        Assert.Equal("git fetch --progress --no-auto-maintenance broken", failure.CommandText);
        Assert.Equal(firstCall.ExitCode, failure.ExitCode);

        // Each failed call's own text, then the line git fetch --all prints for it, in order.
        var brokenLine = failure.StandardError.IndexOf("error: Could not fetch broken\n", StringComparison.Ordinal);
        var goneLine = failure.StandardError.IndexOf("error: Could not fetch gone\n", StringComparison.Ordinal);
        Assert.True(brokenLine > 0, failure.StandardError);
        Assert.True(goneLine > brokenLine, failure.StandardError);
        Assert.Contains("no-such-repository", failure.StandardError[..brokenLine], StringComparison.Ordinal);
        Assert.Contains("gone.git", failure.StandardError[brokenLine..goneLine], StringComparison.Ordinal);
        Assert.DoesNotContain("Could not fetch origin", failure.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fetch_of_all_remotes_without_remotes_fetches_nothing()
    {
        using var repo = await TempRepo.CreateAsync("lonely");
        await repo.CommitFileAsync("README.md", "# Lonely\n", "Initial commit");
        var (repository, calls) = await OpenWithCallsAsync(repo);

        await repository.FetchAsync(null, NetworkOptions.None, cancellationToken: TestCancelled);

        Assert.Empty(FetchCalls(calls));
    }

    [Fact]
    public async Task A_cancelled_fetch_of_all_remotes_stops_the_running_fetch_and_skips_the_rest()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.RequireAuth = false;
        using var repo = await Scenarios.AuthRemoteAsync(server);
        await repo.AddBareRemoteAsync("zz-local");
        var (repository, calls) = await OpenWithCallsAsync(repo);
        var progress = new ProgressParserTests.CollectingProgress();
        var options = NoPromptsNoProxy with { Progress = progress };

        var held = server.HoldNextFetchPack();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);
        var fetch = repository.FetchAsync(null, options, cancellationToken: cancellation.Token);
        await held.WaitAsync(StepTimeout, TestCancelled);

        // The server held the pack after its first progress line with a percentage.
        await WaitUntilAsync(() => progress.Reports.Any(report => report.Percent is not null));
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetch.WaitAsync(StepTimeout, TestCancelled));
        server.Release();

        var fetches = calls.Snapshot().Where(call => call.CommandText.StartsWith("git fetch", StringComparison.Ordinal)).ToList();
        var only = Assert.Single(fetches);
        Assert.Equal("git fetch --progress --no-auto-maintenance origin", only.CommandText);
        Assert.Equal(GitCallOutcome.Cancelled, only.Outcome);
    }

    // Push (2.19), force push with lease (2.20, D100, D111).

    [Fact]
    public async Task A_push_updates_the_remote_branch_and_can_set_the_upstream_or_another_name()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var (repository, calls) = await OpenWithCallsAsync(sync);

        await repository.PushAsync(new PushRequest("origin", "published", "refs/heads/published"), NetworkOptions.None, TestCancelled);
        await repository.PushAsync(new PushRequest("origin", "topic", "refs/heads/topic") { SetUpstream = true }, NetworkOptions.None, TestCancelled);
        await repository.PushAsync(new PushRequest("origin", "topic", "refs/heads/review/topic"), NetworkOptions.None, TestCancelled);

        Assert.Equal(await sync.RefTargetAsync("refs/heads/published"), await RemoteRefAsync(sync, "refs/heads/published"));
        Assert.Equal(await sync.RefTargetAsync("refs/heads/topic"), await RemoteRefAsync(sync, "refs/heads/topic"));
        Assert.Equal(await sync.RefTargetAsync("refs/heads/topic"), await RemoteRefAsync(sync, "refs/heads/review/topic"));
        Assert.Equal(
            [
                "git push --progress origin refs/heads/published:refs/heads/published",
                "git push --progress --set-upstream origin refs/heads/topic:refs/heads/topic",
                "git push --progress origin refs/heads/topic:refs/heads/review/topic",
            ],
            PushCalls(calls));

        var refs = await repository.ReadRefsAsync(TestCancelled);
        var topic = Branch(refs, "topic");
        Assert.Equal(("refs/remotes/origin/topic", "origin", "refs/heads/topic", 0), (topic.UpstreamRef, topic.UpstreamRemote, topic.UpstreamRemoteRef, topic.Ahead));
        Assert.Equal(0, Branch(refs, "published").Ahead);
    }

    [Fact]
    public async Task A_push_names_the_branch_by_its_full_name_so_a_tag_or_a_branch_named_like_a_remote_branch_is_never_taken()
    {
        using var edges = await Scenarios.EdgeCasesAsync();
        var repository = await OpenAsync(edges);

        // A tag feature/alpha (at Main work) stands beside the branch feature/alpha (at Alpha work);
        // a local branch origin/main (at Initial commit) beside the remote branch origin/main.
        await repository.PushAsync(new PushRequest("origin", "feature/alpha", "refs/heads/feature/alpha"), NetworkOptions.None, TestCancelled);
        await repository.PushAsync(new PushRequest("origin", "origin/main", "refs/heads/from-local"), NetworkOptions.None, TestCancelled);

        Assert.Equal(await edges.RefTargetAsync("refs/heads/feature/alpha"), await RemoteRefAsync(edges, "refs/heads/feature/alpha"));
        Assert.Null(await RemoteRefAsync(edges, "refs/tags/feature/alpha"));
        Assert.Equal(await edges.RefTargetAsync("refs/heads/origin/main"), await RemoteRefAsync(edges, "refs/heads/from-local"));
        Assert.NotEqual(await edges.RefTargetAsync("refs/remotes/origin/main"), await RemoteRefAsync(edges, "refs/heads/from-local"));
    }

    [Fact]
    public async Task A_push_behind_the_remote_is_rejected_with_gits_text_before_and_after_a_fetch()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var theirs = await Scenarios.PushFromElsewhereAsync(sync, "main", "src/other.txt", "other\n", "Other work");
        await sync.CommitFileAsync("src/local.txt", "local\n", "Local work");
        var repository = await OpenAsync(sync);
        var push = new PushRequest("origin", "main", "refs/heads/main");

        var unknown = await Assert.ThrowsAsync<GitException>(() => repository.PushAsync(push, NetworkOptions.None, TestCancelled));
        AssertRejected(unknown, "(fetch first)", GitFailureKind.PushRejected);

        await repository.FetchAsync("origin", NetworkOptions.None, cancellationToken: TestCancelled);
        var known = await Assert.ThrowsAsync<GitException>(() => repository.PushAsync(push, NetworkOptions.None, TestCancelled));
        AssertRejected(known, "(non-fast-forward)", GitFailureKind.PushRejected);

        Assert.Equal(theirs, await RemoteRefAsync(sync, "refs/heads/main"));
    }

    [Fact]
    public async Task A_force_push_replaces_a_rewritten_branch_whose_old_tip_is_in_its_reflog()
    {
        using var sync = await Scenarios.RemoteSyncAsync();

        // published (1 ahead of origin/published) rewritten from Shared work. Every reflog entry
        // is TempRepo's (D111): origin/published's last one is the scenario's push at 12:03, and
        // published's entries from 12:03 on lead to its entry for Published work, the remote's tip.
        await sync.CheckoutAsync("published");
        await sync.GitAsync("reset", "--quiet", "--hard", "HEAD~2");
        var rewritten = await sync.CommitFileAsync("src/published.txt", "rewritten\n", "Rewritten work");
        var (repository, calls) = await OpenWithCallsAsync(sync);

        await repository.PushAsync(new PushRequest("origin", "published", "refs/heads/published") { ForceWithLease = true }, NetworkOptions.None, TestCancelled);

        Assert.Equal(rewritten, await RemoteRefAsync(sync, "refs/heads/published"));
        Assert.Equal(
            ["git push --progress --force-with-lease=refs/heads/published --force-if-includes origin refs/heads/published:refs/heads/published"],
            PushCalls(calls));
    }

    [Fact]
    public async Task A_force_push_stops_when_the_remote_moved_since_the_last_fetch()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var theirs = await Scenarios.PushFromElsewhereAsync(sync, "published", "src/elsewhere.txt", "elsewhere\n", "Work from elsewhere");
        var repository = await OpenAsync(sync);

        var failure = await Assert.ThrowsAsync<GitException>(
            () => repository.PushAsync(new PushRequest("origin", "published", "refs/heads/published") { ForceWithLease = true }, NetworkOptions.None, TestCancelled));

        AssertRejected(failure, "(stale info)", GitFailureKind.PushRejectedStale);
        Assert.Equal(theirs, await RemoteRefAsync(sync, "refs/heads/published"));
    }

    [Fact]
    public async Task A_force_push_stops_when_a_fetch_brought_commits_the_branch_never_had()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var theirs = await Scenarios.PushFromElsewhereAsync(sync, "published", "src/elsewhere.txt", "elsewhere\n", "Work from elsewhere");
        var repository = await OpenAsync(sync);

        // The app's fetch moves origin/published (the lease's value) onto their commit; its reflog
        // entry is dated 2026-01-02 09:00 (D72), after all of published's (D111), so only
        // published's tip counts, and it does not include their commit.
        await repository.FetchAsync("origin", NetworkOptions.None, cancellationToken: TestCancelled);
        var failure = await Assert.ThrowsAsync<GitException>(
            () => repository.PushAsync(new PushRequest("origin", "published", "refs/heads/published") { ForceWithLease = true }, NetworkOptions.None, TestCancelled));

        AssertRejected(failure, "(remote ref updated since checkout)", GitFailureKind.PushRejectedStale);
        Assert.Equal(theirs, await RemoteRefAsync(sync, "refs/heads/published"));
    }

    [Fact]
    public async Task A_force_push_goes_through_once_the_fetched_commits_were_integrated_even_if_dropped_since()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(sync, "published", "src/elsewhere.txt", "elsewhere\n", "Work from elsewhere");

        // All through TempRepo at one minute of its clock (D111): the fetch, the merge that takes
        // their commit in, the reset that drops it again and the new commit. published's reflog
        // entries from the fetch's minute on include the merge, which holds their commit.
        await sync.GitAsync("fetch", "--quiet", "origin");
        await sync.CheckoutAsync("published");
        await sync.GitAsync("merge", "--quiet", "--no-edit", "refs/remotes/origin/published");
        await sync.GitAsync("reset", "--quiet", "--hard", "HEAD~1");
        var mine = await sync.CommitFileAsync("src/published.txt", "published\nunpushed\nmine\n", "My work");
        var repository = await OpenAsync(sync);

        await repository.PushAsync(new PushRequest("origin", "published", "refs/heads/published") { ForceWithLease = true }, NetworkOptions.None, TestCancelled);

        Assert.Equal(mine, await RemoteRefAsync(sync, "refs/heads/published"));
    }

    // Deleting on a remote (2.22, 2.29, D100), pushing a tag (2.29).

    [Fact]
    public async Task Deleting_a_remote_branch_at_the_expected_commit_removes_it_there_and_here()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var expected = await sync.RefTargetAsync("refs/remotes/origin/published");
        var (repository, calls) = await OpenWithCallsAsync(sync);

        await repository.DeleteRemoteBranchAsync("origin", "published", expected!, NetworkOptions.None, TestCancelled);

        Assert.Null(await RemoteRefAsync(sync, "refs/heads/published"));
        Assert.Null(await sync.RefTargetAsync("refs/remotes/origin/published"));
        Assert.Equal([$"git push --progress --force-with-lease=refs/heads/published:{expected} origin --delete refs/heads/published"], PushCalls(calls));
    }

    [Fact]
    public async Task Deleting_a_remote_branch_that_moved_since_the_last_fetch_is_refused()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var expected = await sync.RefTargetAsync("refs/remotes/origin/published");
        var theirs = await Scenarios.PushFromElsewhereAsync(sync, "published", "src/elsewhere.txt", "elsewhere\n", "Work from elsewhere");
        var repository = await OpenAsync(sync);

        var failure = await Assert.ThrowsAsync<GitException>(
            () => repository.DeleteRemoteBranchAsync("origin", "published", expected!, NetworkOptions.None, TestCancelled));

        AssertRejected(failure, "(stale info)", GitFailureKind.PushRejectedStale);
        Assert.Equal(theirs, await RemoteRefAsync(sync, "refs/heads/published"));
    }

    [Fact]
    public async Task A_tag_is_pushed_and_deleted_on_the_remote_while_it_is_the_same_tag_object()
    {
        using var ops = await Scenarios.OperationsAsync();
        var tagObject = await ops.RefTargetAsync("refs/tags/v1.0");
        var (repository, calls) = await OpenWithCallsAsync(ops);

        await repository.PushTagAsync("origin", "v1.0", NetworkOptions.None, TestCancelled);
        Assert.Equal(tagObject, await RemoteRefAsync(ops, "refs/tags/v1.0"));

        await repository.DeleteRemoteTagAsync("origin", "v1.0", tagObject!, NetworkOptions.None, TestCancelled);
        Assert.Null(await RemoteRefAsync(ops, "refs/tags/v1.0"));
        Assert.Equal(tagObject, await ops.RefTargetAsync("refs/tags/v1.0"));

        Assert.Equal(
            [
                "git push --progress origin refs/tags/v1.0:refs/tags/v1.0",
                $"git push --progress --force-with-lease=refs/tags/v1.0:{tagObject} origin --delete refs/tags/v1.0",
            ],
            PushCalls(calls));
    }

    [Fact]
    public async Task Deleting_a_tag_that_the_remote_lacks_or_has_another_way_is_refused()
    {
        using var ops = await Scenarios.OperationsAsync();
        var repository = await OpenAsync(ops);

        // origin has no tags: without the lease git would report the tag as deleted.
        var local = (await ops.RefTargetAsync("refs/tags/v1.1"))!;
        var missing = await Assert.ThrowsAsync<GitException>(
            () => repository.DeleteRemoteTagAsync("origin", "v1.1", local, NetworkOptions.None, TestCancelled));
        AssertRejected(missing, "(stale info)", GitFailureKind.PushRejectedStale);

        // origin's v1.1 is at Main work; the local one is moved to Alpha two.
        await repository.PushTagAsync("origin", "v1.1", NetworkOptions.None, TestCancelled);
        var remote = await RemoteRefAsync(ops, "refs/tags/v1.1");
        await ops.GitAsync("tag", "--force", "v1.1", "refs/heads/feature/alpha");
        var moved = (await ops.RefTargetAsync("refs/tags/v1.1"))!;
        var different = await Assert.ThrowsAsync<GitException>(
            () => repository.DeleteRemoteTagAsync("origin", "v1.1", moved, NetworkOptions.None, TestCancelled));
        AssertRejected(different, "(stale info)", GitFailureKind.PushRejectedStale);
        Assert.Equal(remote, await RemoteRefAsync(ops, "refs/tags/v1.1"));
    }

    // Upstreams (2.21, D101).

    [Fact]
    public async Task The_upstream_is_set_to_a_remote_branch_or_a_local_one_and_removed()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var (repository, calls) = await OpenWithCallsAsync(sync);

        await repository.SetUpstreamAsync("topic", "refs/remotes/origin/main", TestCancelled);
        var remote = Branch(await repository.ReadRefsAsync(TestCancelled), "topic");
        Assert.Equal(("refs/remotes/origin/main", "origin", "refs/heads/main", false, 1, 0), (remote.UpstreamRef, remote.UpstreamRemote, remote.UpstreamRemoteRef, remote.HasLocalUpstream, remote.Ahead, remote.Behind));

        await repository.SetUpstreamAsync("topic", "refs/heads/main", TestCancelled);
        var local = Branch(await repository.ReadRefsAsync(TestCancelled), "topic");
        Assert.Equal(("refs/heads/main", ".", "refs/heads/main", true, 1), (local.UpstreamRef, local.UpstreamRemote, local.UpstreamRemoteRef, local.HasLocalUpstream, local.Ahead));

        await repository.UnsetUpstreamAsync("topic", TestCancelled);
        var none = Branch(await repository.ReadRefsAsync(TestCancelled), "topic");
        Assert.Equal((null, null, null), (none.UpstreamRef, none.UpstreamRemote, none.Upstream));

        Assert.Equal(
            ["git branch --set-upstream-to=refs/remotes/origin/main topic", "git branch --set-upstream-to=refs/heads/main topic", "git branch --unset-upstream topic"],
            calls.Snapshot().Select(call => call.CommandText).Where(text => text.StartsWith("git branch", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_gone_upstream_is_removed_or_replaced_and_a_missing_one_is_refused()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        await sync.GitAsync("push", "--quiet", "origin", "--delete", "refs/heads/published");
        var repository = await OpenAsync(sync);
        Assert.True(Branch(await repository.ReadRefsAsync(TestCancelled), "published").UpstreamGone);

        await repository.UnsetUpstreamAsync("published", TestCancelled);
        Assert.Null(Branch(await repository.ReadRefsAsync(TestCancelled), "published").UpstreamRef);

        await sync.GitAsync("branch", "--set-upstream-to=refs/remotes/origin/main", "topic");
        await sync.GitAsync("update-ref", "-d", "refs/remotes/origin/main");
        Assert.True(Branch(await repository.ReadRefsAsync(TestCancelled), "topic").UpstreamGone);
        await sync.GitAsync("fetch", "--quiet", "origin");
        await repository.SetUpstreamAsync("topic", "refs/remotes/origin/main", TestCancelled);
        Assert.False(Branch(await repository.ReadRefsAsync(TestCancelled), "topic").UpstreamGone);

        await Assert.ThrowsAsync<GitException>(() => repository.SetUpstreamAsync("topic", "refs/remotes/origin/no-such-branch", TestCancelled));
    }

    [Fact]
    public async Task A_local_upstream_and_a_branch_named_like_a_remote_branch_keep_their_upstreams_apart()
    {
        using var edges = await Scenarios.EdgeCasesAsync();
        var repository = await OpenAsync(edges);
        Assert.True(Branch(await repository.ReadRefsAsync(TestCancelled), "loc").HasLocalUpstream);

        await repository.UnsetUpstreamAsync("loc", TestCancelled);
        Assert.Null(Branch(await repository.ReadRefsAsync(TestCancelled), "loc").UpstreamRef);
        await repository.SetUpstreamAsync("loc", "refs/heads/main", TestCancelled);
        await repository.SetUpstreamAsync("origin/main", "refs/remotes/origin/main", TestCancelled);

        var refs = await repository.ReadRefsAsync(TestCancelled);
        var loc = Branch(refs, "loc");
        Assert.Equal(("refs/heads/main", ".", true, 1), (loc.UpstreamRef, loc.UpstreamRemote, loc.HasLocalUpstream, loc.Behind));
        var named = Branch(refs, "origin/main");
        Assert.Equal(("refs/remotes/origin/main", "origin", "refs/heads/main", 1), (named.UpstreamRef, named.UpstreamRemote, named.UpstreamRemoteRef, named.Behind));
    }

    // Remotes (2.23, D103).

    [Fact]
    public async Task Remotes_are_added_renamed_changed_and_removed_and_a_token_reaches_no_record_and_no_log()
    {
        const string Secret = "ghp_SECRET123";
        const string OtherSecret = "ghp_OTHER456";
        using var sync = await Scenarios.RemoteSyncAsync();
        var calls = new GitCallLog();
        var log = new ListLog();
        var repository = await GitRepository.OpenAsync(NewRunner(calls, log), sync.Path, TestCancelled, log);
        var origin = sync.SiblingPath("origin.git");
        var spaced = sync.SiblingPath("with space/x.git");

        await repository.AddRemoteAsync("mirror", $"https://alice:{Secret}@127.0.0.1:1/x.git", TestCancelled);
        await repository.AddRemoteAsync("spaced", spaced, TestCancelled);
        await repository.AddRemoteAsync("dash", "-dash-url", TestCancelled);
        Assert.Equal(
            [
                new RemoteInfo("dash", "-dash-url", null),
                new RemoteInfo("mirror", $"https://alice:{Secret}@127.0.0.1:1/x.git", null),
                new RemoteInfo("origin", origin, null),
                new RemoteInfo("spaced", spaced, null),
            ],
            await repository.ReadRemotesAsync(TestCancelled));
        Assert.Equal("https://***@127.0.0.1:1/x.git", (await repository.ReadRemotesAsync(TestCancelled))[1].ShownUrl);

        // Nothing listens on port 1: git fails after it has used the URL.
        var failure = await Assert.ThrowsAsync<GitException>(() => repository.FetchAsync("mirror", NoPromptsNoProxy, cancellationToken: TestCancelled));
        Assert.DoesNotContain(Secret, failure.CommandText, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, GitOutputCleaner.Clean(failure.StandardError, failure.StandardOutput), StringComparison.Ordinal);

        await repository.RenameRemoteAsync("mirror", "backup", TestCancelled);
        await repository.SetRemoteUrlAsync("backup", $"https://bob:{OtherSecret}@127.0.0.1:1/y.git", TestCancelled);
        await sync.GitAsync("remote", "set-url", "--push", "backup", origin);
        await repository.RemoveRemoteAsync("spaced", TestCancelled);
        Assert.Equal(
            [
                new RemoteInfo("backup", $"https://bob:{OtherSecret}@127.0.0.1:1/y.git", origin),
                new RemoteInfo("dash", "-dash-url", null),
                new RemoteInfo("origin", origin, null),
            ],
            await repository.ReadRemotesAsync(TestCancelled));

        // Removing origin removes its remote branches, and main and published lose their upstream.
        await repository.RemoveRemoteAsync("origin", TestCancelled);
        var refs = await repository.ReadRefsAsync(TestCancelled);
        Assert.DoesNotContain(refs.Refs, gitRef => gitRef.Kind == RefKind.RemoteBranch);
        Assert.Null(Branch(refs, "main").UpstreamRef);
        Assert.Null(Branch(refs, "published").UpstreamRef);
        Assert.Equal(["backup", "dash"], (await repository.ReadRemotesAsync(TestCancelled)).Select(remote => remote.Name));

        var records = calls.Snapshot();
        Assert.Contains(records, record => record.CommandText == "git remote -v" && record.StandardOutput.Contains("https://***@127.0.0.1:1/x.git", StringComparison.Ordinal));
        Assert.Contains(records, record => record.CommandText == "git remote add -- mirror https://***@127.0.0.1:1/x.git");
        foreach (var secret in new[] { Secret, OtherSecret })
        {
            Assert.All(records, record => Assert.DoesNotContain(secret, record.CommandText + record.StandardOutput + record.StandardError, StringComparison.Ordinal));
            Assert.All(log.Entries, entry => Assert.DoesNotContain(secret, entry.Message, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task A_clone_runs_with_the_environment_it_is_given()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        var destination = sync.SiblingPath("cloned");

        // A setting handed over through the environment, as the prompt scope's variables are.
        var environment = new Dictionary<string, string?>
        {
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "clone.defaultRemoteName",
            ["GIT_CONFIG_VALUE_0"] = "upstream",
        };
        var clone = await GitRepository.CloneAsync(NewRunner(), sync.SiblingPath("origin.git"), destination, null, TestCancelled, environment: environment);

        Assert.Equal(["upstream"], (await clone.ReadRemotesAsync(TestCancelled)).Select(remote => remote.Name));
    }

    [Fact]
    public async Task Settings_passed_with_the_options_go_before_the_command_and_a_low_speed_limit_ends_a_silent_connection()
    {
        using var sync = await Scenarios.RemoteSyncAsync();

        // A server that accepts the connection and never answers, as one whose network went away.
        using var silent = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        silent.Start();
        var accepted = new List<System.Net.Sockets.TcpClient>();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                accepted.Add(await silent.AcceptTcpClientAsync(TestCancelled));
            }
        }, TestCancelled);
        var port = ((System.Net.IPEndPoint)silent.LocalEndpoint).Port;
        await sync.GitAsync("remote", "add", "silent", $"http://127.0.0.1:{port}/x.git");
        var (repository, calls) = await OpenWithCallsAsync(sync);
        var options = NoPromptsNoProxy with { Configuration = ["http.lowSpeedLimit=1", "http.lowSpeedTime=2"] };

        var failure = await Assert.ThrowsAsync<GitException>(() => repository.FetchAsync("silent", options, noAutoMaintenance: true, TestCancelled))
            .WaitAsync(StepTimeout, TestCancelled);

        Assert.Equal(["git -c http.lowSpeedLimit=1 -c http.lowSpeedTime=2 fetch --progress --no-auto-maintenance silent"], calls.Snapshot().Select(call => call.CommandText).Where(text => text.Contains(" fetch ", StringComparison.Ordinal)));
        Assert.NotEqual(0, failure.ExitCode ?? 0);
        accepted.ForEach(client => client.Dispose());
    }

    private static async Task<(GitRepository Repository, GitCallLog Calls)> OpenWithCallsAsync(TempRepo repo)
    {
        var calls = new GitCallLog();
        return (await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled), calls);
    }

    private static List<string> FetchCalls(GitCallLog calls) =>
        [.. calls.Snapshot().Select(call => call.CommandText).Where(text => text.StartsWith("git fetch", StringComparison.Ordinal))];

    private static List<string> PushCalls(GitCallLog calls) =>
        [.. calls.Snapshot().Select(call => call.CommandText).Where(text => text.StartsWith("git push", StringComparison.Ordinal))];

    private static GitRef Branch(RepoRefs refs, string name) =>
        refs.Refs.Single(gitRef => gitRef.Kind == RefKind.LocalBranch && gitRef.Name == name);

    /// <summary>Where <c>origin</c> has <paramref name="fullRef"/> (<c>git ls-remote</c>, the object as stored: a tag object for an annotated tag), or null.</summary>
    internal static async Task<string?> RemoteRefAsync(TempRepo repo, string fullRef, string remote = "origin")
    {
        var output = (await repo.GitAsync("ls-remote", remote, fullRef)).StandardOutput;
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r').Split('\t'))
            .FirstOrDefault(fields => fields.Length == 2 && fields[1] == fullRef)?[0];
    }

    /// <summary>Git's rejected line with <paramref name="reason"/>, and the kind the classifier gives the failure.</summary>
    private static void AssertRejected(GitException failure, string reason, GitFailureKind kind)
    {
        Assert.Contains(failure.StandardError.Split('\n'), line => line.StartsWith(" ! [rejected]", StringComparison.Ordinal) && line.Contains(reason, StringComparison.Ordinal));
        Assert.Equal(kind, GitFailureClassifier.Classify(failure.StandardError, failure.StandardOutput, failure.ExitCode));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + StepTimeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not come true in time.");
            await Task.Delay(20, TestCancelled);
        }
    }
}
