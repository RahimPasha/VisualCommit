using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.UndoTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// What a push changed on the remote (docs/phase-5-design.md 0.5, 7.7, D170): git's summary lines
/// read (<see cref="GitRepository.ReadPushSummary"/>, in the form Git 2.36 and 2.55 print alike),
/// and <see cref="GitRepository.PushAsync"/>'s and <see cref="GitRepository.PushTagAsync"/>'s
/// <see cref="PushOutcome"/> against a local bare remote: the old value from git's own line, never
/// from the remote-tracking ref, which can be stale.
/// </summary>
public class PushOutcomeTests
{
    /// <summary>A push's standard error as git prints it, progress lines with their carriage returns included.</summary>
    private const string Pushed =
        "Enumerating objects: 5, done.\n" +
        "Counting objects:  20% (1/5)\rCounting objects: 100% (5/5)\rCounting objects: 100% (5/5), done.\n" +
        "remote:  * [new branch]      fake -> fake\n" +
        "branch 'main' set up to track 'origin/main'.\n" +
        "To ../remote.git\n" +
        "   46d717e..205f6cb  main -> main\n" +
        " + 205f6cb...f698b61 feature -> feature (forced update)\n" +
        " * [new branch]      topic -> review/topic\n" +
        " * [new branch]      46d717e7fa9c27fe7c04ec43f1eea86fca08fb35 -> x\n" +
        " * [new tag]         v1 -> v1\n" +
        " - [deleted]         old\n" +
        " ! [rejected]        46d717e7fa9c27fe7c04ec43f1eea86fca08fb35 -> stale (stale info)\n" +
        " ! [remote rejected] hooked -> hooked (pre-receive hook declined)\n" +
        "error: failed to push some refs to '../remote.git'\n" +
        "hint: Updates were rejected because the tag already exists in the remote.\n";

    [Fact]
    public void Each_ref_line_of_a_push_summary_is_read_with_its_values()
    {
        Assert.Equal(
            [
                new GitRepository.PushSummaryLine(' ', "46d717e..205f6cb", "main", "main", null, "46d717e", "205f6cb"),
                new GitRepository.PushSummaryLine('+', "205f6cb...f698b61", "feature", "feature", "forced update", "205f6cb", "f698b61"),
                new GitRepository.PushSummaryLine('*', "[new branch]", "topic", "review/topic", null, null, null),
                new GitRepository.PushSummaryLine('*', "[new branch]", "46d717e7fa9c27fe7c04ec43f1eea86fca08fb35", "x", null, null, null),
                new GitRepository.PushSummaryLine('*', "[new tag]", "v1", "v1", null, null, null),
                new GitRepository.PushSummaryLine('-', "[deleted]", null, "old", null, null, null),
                new GitRepository.PushSummaryLine('!', "[rejected]", "46d717e7fa9c27fe7c04ec43f1eea86fca08fb35", "stale", "stale info", null, null),
                new GitRepository.PushSummaryLine('!', "[remote rejected]", "hooked", "hooked", "pre-receive hook declined", null, null),
            ],
            GitRepository.ReadPushSummary(Pushed));

        Assert.Empty(GitRepository.ReadPushSummary("Everything up-to-date\n"));
        Assert.Single(GitRepository.ReadPushSummary("To ../remote.git\r\n * [new branch]      main -> main\r\n"));
    }

    [Fact]
    public async Task A_new_branch_a_fast_forward_and_a_forced_update_give_their_old_and_new_values()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);

        // A forced update with the lease (--force-if-includes reads the reflogs' times: the
        // rewrite comes after the remote-tracking ref's last update, as the clock runs).
        var before = (await TargetAsync(repo, "refs/remotes/origin/main"))!;
        await repo.GitAsync("reset", "--quiet", "--hard", "HEAD~1");
        var rewritten = await repo.CommitFileAsync("p.txt", "rewritten\n", "c3 rewritten");
        var forced = await repository.PushAsync(new PushRequest("origin", "main", "refs/heads/main") { ForceWithLease = true }, NetworkOptions.None, TestCancelled);
        Assert.Equal([new PushedRef("refs/heads/main", before, rewritten)], forced.Refs);

        // A fast-forward.
        var next = await repo.CommitFileAsync("p.txt", "pushed\n", "p1");
        var moved = await repository.PushAsync(new PushRequest("origin", "main", "refs/heads/main"), NetworkOptions.None, TestCancelled);
        Assert.Equal([new PushedRef("refs/heads/main", rewritten, next)], moved.Refs);

        // New, under another name on the remote, with its upstream set.
        await repo.CreateBranchAsync("topic");
        var created = await repository.PushAsync(new PushRequest("origin", "topic", "refs/heads/review/topic") { SetUpstream = true }, NetworkOptions.None, TestCancelled);
        Assert.Equal([new PushedRef("refs/heads/review/topic", null, next)], created.Refs);

        // Nothing new: nothing changed on the remote.
        Assert.Same(PushOutcome.None, await repository.PushAsync(new PushRequest("origin", "main", "refs/heads/main"), NetworkOptions.None, TestCancelled));
    }

    [Fact]
    public async Task The_old_value_is_the_remotes_own_when_the_remote_tracking_ref_is_stale()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);

        // Someone else sets origin's main back to c2; this repository has not fetched and still
        // has origin/main at c3, which a push of c4 fast-forwards from c2 (0.5's stale case).
        var c2 = (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim();
        var stale = (await TargetAsync(repo, "refs/remotes/origin/main"))!;
        await repo.GitAsync("--git-dir=" + repo.SiblingPath("origin.git"), "update-ref", "refs/heads/main", c2);
        var c4 = await repo.CommitFileAsync("p.txt", "pushed\n", "c4");

        var outcome = await repository.PushAsync(new PushRequest("origin", "main", "refs/heads/main"), NetworkOptions.None, TestCancelled);

        Assert.NotEqual(stale, c2);
        Assert.Equal([new PushedRef("refs/heads/main", c2, c4)], outcome.Refs);
    }

    [Fact]
    public async Task A_tag_push_gives_the_tag_object_and_nothing_when_the_remote_has_it()
    {
        using var repo = await UndoRepoAsync();
        var repository = await OpenAsync(repo);
        await repo.AnnotatedTagAsync("v1", "Release 1");
        var tagObject = (await TargetAsync(repo, "refs/tags/v1"))!;

        var pushed = await repository.PushTagAsync("origin", "v1", NetworkOptions.None, TestCancelled);
        Assert.Equal([new PushedRef("refs/tags/v1", null, tagObject)], pushed.Refs);
        Assert.Same(PushOutcome.None, await repository.PushTagAsync("origin", "v1", NetworkOptions.None, TestCancelled));
    }
}
