using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary><see cref="GitRepository.ReadRefsAsync"/>: HEAD, branches, remote branches, tags, stashes and remotes.</summary>
public class RefReadingTests
{
    [Fact]
    public async Task Reads_branches_remotes_tags_stashes_and_where_head_is()
    {
        // The history in one import: main at Second, two local branches in folders, a
        // lightweight and an annotated tag, and two commits on Second kept aside for later,
        // under refs/visualcommit/, which is neither a branch nor a tag.
        using var repo = await TempRepo.CreateAsync();
        var history = new HistoryStream();
        var firstMark = history.Commit("refs/heads/main", "First", file: "a.txt", content: "a\n");
        var secondMark = history.Commit("refs/heads/main", "Second", file: "a.txt", content: "b\n");
        var theirsMark = history.Commit("refs/visualcommit/theirs", "Their change", from: secondMark, file: "theirs.txt", content: "theirs\n");
        var oursMark = history.Commit("refs/visualcommit/ours", "Our change", from: secondMark);
        history.Reset("refs/heads/feature/login", firstMark);
        history.Reset("refs/heads/old/topic", firstMark);
        history.Reset("refs/tags/v0.1", firstMark);
        history.Tag("v1.0", secondMark, "Release 1.0");
        var marks = await ImportAsync(repo, history);
        var (first, second, theirs, ours) = (marks[firstMark], marks[secondMark], marks[theirsMark], marks[oursMark]);
        await repo.GitAsync("reset", "--quiet", "--hard");

        // A remote: a bare clone of main next to the repo, fetched, and main's upstream. Also a
        // remote name with a slash, and a shorter remote whose name it starts with, so that
        // remote branches must be matched to the right one.
        var originPath = Sibling(repo, "origin.git");
        await repo.GitAsync("clone", "--quiet", "--bare", "--single-branch", "--branch", "main", "--no-tags", repo.Path, originPath);
        await repo.GitAsync("remote", "add", "origin", originPath);
        await repo.GitAsync("remote", "add", "team/fork", originPath);
        await repo.GitAsync("remote", "add", "team", originPath);
        await repo.GitAsync("fetch", "--quiet", "--multiple", "origin", "team/fork");
        await repo.GitAsync("remote", "set-head", "origin", "main");
        await repo.GitAsync("branch", "--quiet", "--set-upstream-to=origin/main", "main");

        // main ends one commit ahead of origin/main and one behind it: their commit is pushed,
        // which moves origin/main too, and ours goes on main. Ours keeps Second's tree, so the
        // working tree stays in step.
        await repo.GitAsync("push", "--quiet", "origin", "refs/visualcommit/theirs:refs/heads/main");
        await repo.GitAsync("update-ref", "refs/heads/main", ours);

        // old/topic's upstream is a branch the remote does not have (any more).
        await repo.GitAsync("config", "branch.old/topic.remote", "origin");
        await repo.GitAsync("config", "branch.old/topic.merge", "refs/heads/old/topic");

        // A tag of the annotated tag.
        await repo.GitAsync("tag", "-a", "-m", "About the release", "v1.0-note", "v1.0");

        // Two stashes, the newer first in the list.
        repo.WriteFile("a.txt", "changed once\n");
        await repo.GitAsync("stash", "push", "--quiet", "-m", "Older stash");
        repo.WriteFile("a.txt", "changed twice\n");
        await repo.GitAsync("stash", "push", "--quiet", "-m", "Newer stash");
        var stashShas = (await repo.GitAsync("rev-parse", "stash@{0}", "stash@{1}")).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var repository = await OpenAsync(repo);
        var refs = await repository.ReadRefsAsync(TestCancelled);

        Assert.Equal(new HeadState("main", ours), refs.Head);
        Assert.False(refs.Head.IsDetached);
        Assert.False(refs.Head.IsUnborn);
        Assert.Equal(["origin", "team", "team/fork"], refs.Remotes);

        // In for-each-ref's order: by full name. origin/HEAD is left out.
        Assert.Equal(
            [
                new GitRef("refs/heads/feature/login", "feature/login", RefKind.LocalBranch, first),
                new GitRef("refs/heads/main", "main", RefKind.LocalBranch, ours, Upstream: "origin/main", Ahead: 1, Behind: 1, IsHead: true),
                new GitRef("refs/heads/old/topic", "old/topic", RefKind.LocalBranch, first, Upstream: "origin/old/topic", UpstreamGone: true),
                new GitRef("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, theirs, RemoteName: "origin"),
                new GitRef("refs/remotes/team/fork/main", "team/fork/main", RefKind.RemoteBranch, second, RemoteName: "team/fork"),
                new GitRef("refs/tags/v0.1", "v0.1", RefKind.Tag, first),
                new GitRef("refs/tags/v1.0", "v1.0", RefKind.Tag, second),
                new GitRef("refs/tags/v1.0-note", "v1.0-note", RefKind.Tag, second),
            ],
            refs.Refs);

        Assert.Equal(stashShas, refs.Stashes.Select(stash => stash.Sha));
        Assert.Equal([0, 1], refs.Stashes.Select(stash => stash.Index));
        Assert.Equal(["stash@{0}", "stash@{1}"], refs.Stashes.Select(stash => stash.Name));
        Assert.Equal(["On main: Newer stash", "On main: Older stash"], refs.Stashes.Select(stash => stash.Message));
        Assert.All(refs.Stashes, stash =>
        {
            Assert.Equal(ours, stash.BaseSha);
            Assert.Equal(TempRepo.AuthorName, stash.AuthorName);
            Assert.Equal(TempRepo.AuthorEmail, stash.AuthorEmail);
            Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), stash.Date);
            Assert.Equal(stash.Date, stash.AuthorDate);
        });

        // Detached: HEAD names its commit and no branch.
        await repo.GitAsync("checkout", "--quiet", "--detach", first);
        var detached = await repository.ReadRefsAsync(TestCancelled);

        Assert.Equal(new HeadState(null, first), detached.Head);
        Assert.True(detached.Head.IsDetached);
        Assert.DoesNotContain(detached.Refs, gitRef => gitRef.IsHead);
    }

    [Theory]
    [InlineData("", 0, 0, false)]
    [InlineData("[ahead 3]", 3, 0, false)]
    [InlineData("[behind 12]", 0, 12, false)]
    [InlineData("[ahead 1, behind 2]", 1, 2, false)]
    [InlineData("[gone]", 0, 0, true)]
    public void Reads_the_upstream_track_text(string track, int ahead, int behind, bool gone) =>
        Assert.Equal((ahead, behind, gone), GitRepository.ParseTrack(track));
}
