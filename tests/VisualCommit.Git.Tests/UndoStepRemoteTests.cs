using VisualCommit.Core.Git;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.UndoTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// docs/phase-5-design.md 7.3's sequences for tags, upstreams, remotes and pushes, each action made
/// by the git call the app makes and recorded as the app records it (a push's remote values from
/// its <see cref="PushOutcome"/>, D170), against a local bare remote: the repository and the
/// remote after an undo are as they were before the action, and after the redo as it left them.
/// </summary>
public class UndoStepRemoteTests
{
    [Fact]
    public async Task Tags_created_and_deleted_come_back_as_the_same_objects()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var start = await PictureAsync(repo);

        var annotated = await RecordAsync(repository, UndoScope.CreateTag("v1"), () => repository.CreateTagAsync("v1", "HEAD", "Release 1", TestCancelled));
        var tagObject = annotated.After.Refs["refs/tags/v1"];
        var tagged = await PictureAsync(repo);
        var undone = await UndoAsync(repository, annotated);
        Assert.Equal(start, await PictureAsync(repo));

        // The tag object is kept in a tag-undone backup, which the entry now names (7.8).
        var kept = Assert.Single(undone.Captured.Backups);
        Assert.StartsWith("refs/visualcommit/backup/tag-undone-", kept, StringComparison.Ordinal);
        Assert.EndsWith("/v1", kept, StringComparison.Ordinal);
        Assert.Equal(tagObject, await TargetAsync(repo, kept));
        await RedoAsync(repository, undone);
        Assert.Equal(tagged, await PictureAsync(repo));
        Assert.Equal(tagObject, await TargetAsync(repo, "refs/tags/v1"));

        var deleted = await RecordAsync(repository, UndoScope.DeleteTag("v1"), () => repository.DeleteTagAsync("v1", TestCancelled));
        deleted = await UndoAsync(repository, deleted);
        Assert.Equal(tagged, await PictureAsync(repo));
        await RedoAsync(repository, deleted);
        Assert.Equal(start, await PictureAsync(repo));
    }

    [Fact]
    public async Task An_upstream_set_or_stopped_gets_its_keys_back()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var start = await PictureAsync(repo);

        var stop = await RecordAsync(repository, UndoScope.Upstream("main"), () => repository.UnsetUpstreamAsync("main", TestCancelled));
        var stopped = await PictureAsync(repo);
        stop = await UndoAsync(repository, stop);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal("refs/remotes/origin/main", (await repo.GitAsync("rev-parse", "--symbolic-full-name", "main@{upstream}")).StandardOutput.Trim());
        await RedoAsync(repository, stop);
        Assert.Equal(stopped, await PictureAsync(repo));

        var set = await RecordAsync(repository, UndoScope.Upstream("main"), () => repository.SetUpstreamAsync("main", "refs/remotes/origin/feature", TestCancelled));
        var changed = await PictureAsync(repo);
        set = await UndoAsync(repository, set);
        Assert.Equal(stopped, await PictureAsync(repo));
        await RedoAsync(repository, set);
        Assert.Equal(changed, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_remote_added_and_given_a_new_url_comes_back()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var start = await PictureAsync(repo);

        var added = await RecordAsync(repository, UndoScope.AddRemote("mirror"), () => repository.AddRemoteAsync("mirror", repo.SiblingPath("origin.git"), TestCancelled));
        var withMirror = await PictureAsync(repo);

        // "Fetch now", ticked by default, runs as an operation of its own after the add, which
        // records nothing: its remote branches and mirror/HEAD (which a fetch on Git 2.55 writes
        // itself) go with the remote, as git remote remove deletes them.
        await repository.FetchAsync("mirror", NetworkOptions.None, cancellationToken: TestCancelled);
        await repo.GitAsync("remote", "set-head", "mirror", "main");
        Assert.NotNull(await TargetAsync(repo, "refs/remotes/mirror/feature"));
        added = await UndoAsync(repository, added);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, added);
        Assert.Equal(withMirror, await PictureAsync(repo));

        var url = await RecordAsync(repository, UndoScope.SetRemoteUrl("mirror"), () => repository.SetRemoteUrlAsync("mirror", "https://example.com/mirror.git", TestCancelled));
        var newUrl = await PictureAsync(repo);
        url = await UndoAsync(repository, url);
        Assert.Equal(withMirror, await PictureAsync(repo));
        await RedoAsync(repository, url);
        Assert.Equal(newUrl, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_remote_renamed_and_removed_comes_back_with_its_keys_and_its_branches()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.GitAsync("config", "remote.pushDefault", "origin");
        await repo.GitAsync("config", "branch.main.pushRemote", "origin");

        // origin/HEAD, which every clone has and every fetch on Git 2.55 writes: a symbolic ref.
        await repo.GitAsync("remote", "set-head", "origin", "main");
        var start = await PictureAsync(repo);

        var renamed = await RecordAsync(repository, UndoScope.RenameRemote("origin", "upstream"), () => repository.RenameRemoteAsync("origin", "upstream", TestCancelled));
        var afterRename = await PictureAsync(repo);
        renamed = await UndoAsync(repository, renamed);
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, renamed);
        Assert.Equal(afterRename, await PictureAsync(repo));
        renamed = await UndoAsync(repository, renamed);

        // Removed: its keys, remote.pushDefault, the branches' keys that named it and its remote
        // branches go (0.5); the undo brings them back.
        var beforeRemoval = await PictureAsync(repo);
        var removed = await RecordAsync(repository, UndoScope.RemoveRemote("origin"), async () =>
        {
            var tips = (await repository.ReadRefsAsync(TestCancelled)).Refs.Where(gitRef => gitRef.RemoteName == "origin").ToList();
            var backup = await repository.SaveBackupRefsAsync(new BackupRequest("remote", [.. tips.Select(gitRef => new BackupEntry(gitRef.Name, gitRef.TargetSha))]), TestCancelled);
            UndoCapture.Backups(backup.Refs);
            await repository.RemoveRemoteAsync("origin", TestCancelled);
        });
        var afterRemoval = await PictureAsync(repo);
        Assert.DoesNotContain("remote.pushdefault", afterRemoval.Config, StringComparison.Ordinal);
        Assert.DoesNotContain("refs/remotes/origin/HEAD", afterRemoval.Refs, StringComparison.Ordinal);
        removed = await UndoAsync(repository, removed);
        Assert.Equal(beforeRemoval, await PictureAsync(repo));
        Assert.Equal("refs/remotes/origin/main", (await repo.GitAsync("rev-parse", "--symbolic-full-name", "main@{upstream}")).StandardOutput.Trim());
        Assert.Equal("refs/remotes/origin/main", (await repo.GitAsync("symbolic-ref", "refs/remotes/origin/HEAD")).StandardOutput.Trim());
        await RedoAsync(repository, removed);
        Assert.Equal(afterRemoval, await PictureAsync(repo));
    }

    [Fact]
    public async Task A_push_of_a_new_branch_with_its_upstream_is_deleted_on_the_remote_and_pushed_again()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        await repo.CreateBranchAsync("topic", "HEAD~1");
        await repo.CheckoutAsync("topic");
        await repo.CommitFileAsync("t.txt", "topic\n", "t1");
        var start = await PictureAsync(repo);
        var remoteStart = await RemoteAsync(repo);

        var entry = await RecordAsync(repository, UndoScope.Push("topic", "origin", force: false), async () =>
        {
            var outcome = await repository.PushAsync(new PushRequest("origin", "topic", "refs/heads/topic") { SetUpstream = true }, NetworkOptions.None, TestCancelled);
            UndoCapture.Push("origin", outcome);
        });
        var done = await PictureAsync(repo);
        var remoteDone = await RemoteAsync(repo);
        Assert.Equal([new PushedRef("refs/heads/topic", null, entry.After.Head.Sha)], entry.Captured.Push!.Refs);
        Assert.True(UndoStep.For(entry, UndoDirection.Undo).ChangesRemote);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal(remoteStart, await RemoteAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
        Assert.Equal(remoteDone, await RemoteAsync(repo));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_push_that_moved_a_branch_and_a_force_push_set_it_back_with_a_lease(bool force)
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        if (force)
        {
            await repo.GitAsync("reset", "--quiet", "--hard", "HEAD~1");
        }

        await repo.CommitFileAsync("p.txt", "pushed\n", "p1");
        var start = await PictureAsync(repo);
        var remoteStart = await RemoteAsync(repo);
        var old = await TargetAsync(repo, "refs/remotes/origin/main");

        var entry = await RecordAsync(repository, UndoScope.Push("main", "origin", force), async () =>
        {
            var outcome = await repository.PushAsync(new PushRequest("origin", "main", "refs/heads/main") { ForceWithLease = force }, NetworkOptions.None, TestCancelled);
            UndoCapture.Push("origin", outcome);
        });
        var done = await PictureAsync(repo);
        var remoteDone = await RemoteAsync(repo);
        Assert.Equal([new PushedRef("refs/heads/main", old, entry.After.Head.Sha)], entry.Captured.Push!.Refs);

        entry = await UndoAsync(repository, entry);
        Assert.Equal(start, await PictureAsync(repo));
        Assert.Equal(remoteStart, await RemoteAsync(repo));
        await RedoAsync(repository, entry);
        Assert.Equal(done, await PictureAsync(repo));
        Assert.Equal(remoteDone, await RemoteAsync(repo));
    }

    [Fact]
    public async Task Deletions_on_a_remote_and_a_tag_push_are_undone_and_redone()
    {
        using var repo = await UndoRepoAsync();
        var (repository, _) = await OpenDatedAsync(repo);
        var start = await PictureAsync(repo);
        var remoteStart = await RemoteAsync(repo);

        // The deletion's lease value is its old value, captured at the call site.
        var expected = (await TargetAsync(repo, "refs/remotes/origin/feature"))!;
        var deletion = await RecordAsync(repository, UndoScope.DeleteRemoteBranch("feature", "origin"), async () =>
        {
            await repository.DeleteRemoteBranchAsync("origin", "feature", expected, NetworkOptions.None, TestCancelled);
            UndoCapture.Push("origin", new PushOutcome([new PushedRef("refs/heads/feature", expected, null)]));
        });
        var remoteDeleted = await RemoteAsync(repo);
        deletion = await UndoAsync(repository, deletion);
        Assert.Equal(remoteStart, await RemoteAsync(repo));
        Assert.Equal(start, await PictureAsync(repo));
        await RedoAsync(repository, deletion);
        Assert.Equal(remoteDeleted, await RemoteAsync(repo));
        deletion = await UndoAsync(repository, deletion);

        await repo.AnnotatedTagAsync("v2", "Release 2");
        var tagObject = (await TargetAsync(repo, "refs/tags/v2"))!;
        var push = await RecordAsync(repository, UndoScope.PushTag("v2", "origin"), async () =>
            UndoCapture.Push("origin", await repository.PushTagAsync("origin", "v2", NetworkOptions.None, TestCancelled)));
        Assert.Equal([new PushedRef("refs/tags/v2", null, tagObject)], push.Captured.Push!.Refs);
        var remoteTagged = await RemoteAsync(repo);
        push = await UndoAsync(repository, push);
        Assert.Equal(remoteStart, await RemoteAsync(repo));
        await RedoAsync(repository, push);
        Assert.Equal(remoteTagged, await RemoteAsync(repo));

        var tagDeletion = await RecordAsync(repository, UndoScope.DeleteRemoteTag("v2", "origin"), async () =>
        {
            await repository.DeleteRemoteTagAsync("origin", "v2", tagObject, NetworkOptions.None, TestCancelled);
            UndoCapture.Push("origin", new PushOutcome([new PushedRef("refs/tags/v2", tagObject, null)]));
        });
        tagDeletion = await UndoAsync(repository, tagDeletion);
        Assert.Equal(remoteTagged, await RemoteAsync(repo));
        await RedoAsync(repository, tagDeletion);
        Assert.Equal(remoteStart, await RemoteAsync(repo));
    }

    /// <summary>The bare remote's refs, read there.</summary>
    private static async Task<string> RemoteAsync(TempRepo repo) =>
        (await repo.GitAsync("ls-remote", repo.SiblingPath("origin.git"))).StandardOutput;
}
