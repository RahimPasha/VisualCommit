using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
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

        // Newer git refuses "remote add" for a name that another remote's name starts with
        // ("superset of existing remote"); repositories set up before that rule, or by hand,
        // still have such pairs. Configure it directly, as such a repository has it.
        await repo.GitAsync("config", "remote.team.url", originPath);
        await repo.GitAsync("config", "remote.team.fetch", "+refs/heads/*:refs/remotes/team/*");
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
                new GitRef(
                    "refs/heads/main", "main", RefKind.LocalBranch, ours, Upstream: "origin/main", Ahead: 1, Behind: 1, IsHead: true,
                    UpstreamRef: "refs/remotes/origin/main", UpstreamRemote: "origin", UpstreamRemoteRef: "refs/heads/main"),
                new GitRef(
                    "refs/heads/old/topic", "old/topic", RefKind.LocalBranch, first, Upstream: "origin/old/topic", UpstreamGone: true,
                    UpstreamRef: "refs/remotes/origin/old/topic", UpstreamRemote: "origin", UpstreamRemoteRef: "refs/heads/old/topic"),
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

    [Fact]
    public async Task Reads_an_upstream_as_its_full_ref_remote_and_remote_ref_also_when_it_is_local_or_renamed()
    {
        // D101: side tracks main of this repository (remote "."), whose short form is just
        // "main"; topic tracks a branch whose name on the remote differs from the local one.
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.GitAsync("branch", "--quiet", "side");
        await repo.GitAsync("config", "branch.side.remote", ".");
        await repo.GitAsync("config", "branch.side.merge", "refs/heads/main");
        var originPath = Sibling(repo, "origin.git");
        await repo.GitAsync("clone", "--quiet", "--bare", repo.Path, originPath);
        await repo.GitAsync("remote", "add", "origin", originPath);
        await repo.GitAsync("push", "--quiet", "origin", "main:refs/heads/server/topic");
        await repo.GitAsync("fetch", "--quiet", "origin");
        await repo.GitAsync("branch", "--quiet", "--track", "topic", "refs/remotes/origin/server/topic");

        var refs = (await (await OpenAsync(repo)).ReadRefsAsync(TestCancelled)).Refs;

        var side = refs.Single(gitRef => gitRef.FullName == "refs/heads/side");
        Assert.Equal(("refs/heads/main", ".", "refs/heads/main"), (side.UpstreamRef, side.UpstreamRemote, side.UpstreamRemoteRef));
        Assert.True(side.HasLocalUpstream);
        var topic = refs.Single(gitRef => gitRef.FullName == "refs/heads/topic");
        Assert.Equal(("refs/remotes/origin/server/topic", "origin", "refs/heads/server/topic"), (topic.UpstreamRef, topic.UpstreamRemote, topic.UpstreamRemoteRef));
        Assert.False(topic.HasLocalUpstream);
        var main = refs.Single(gitRef => gitRef.FullName == "refs/heads/main");
        Assert.Equal((null, null, null), (main.UpstreamRef, main.UpstreamRemote, main.UpstreamRemoteRef));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Head_and_its_branch_come_from_one_read_when_a_commit_lands_between_the_reads(bool listFirst)
    {
        // The phase 5 gate's f11f: a commit made in a terminal while the refs are read. The five
        // reads run side by side; here one of for-each-ref and rev-parse HEAD runs before the
        // commit and the other after it, the order a busy machine can give them.
        using var repo = await TempRepo.CreateAsync();
        var first = await repo.CommitFileAsync("a.txt", "a\n", "First");
        string? second = null;
        var armed = false;
        var firstDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        static bool IsList(GitCommand command) => command.Arguments[0] == "for-each-ref";
        static bool IsHeadSha(GitCommand command) => command.Arguments.SequenceEqual(["rev-parse", "-q", "--verify", "HEAD"]);
        var runner = new OrderingRunner(
            NewRunner(),
            before: async command =>
            {
                if (armed && (listFirst ? IsHeadSha(command) : IsList(command)))
                {
                    await firstDone.Task.WaitAsync(TimeSpan.FromSeconds(30), TestCancelled);
                    second = await repo.CommitFileAsync("a.txt", "b\n", "Second");
                }
            },
            after: command =>
            {
                if (armed && (listFirst ? IsList(command) : IsHeadSha(command)))
                {
                    firstDone.TrySetResult();
                }
            });
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestCancelled);
        armed = true;

        var refs = await repository.ReadRefsAsync(TestCancelled);

        // The branch as for-each-ref saw it, and HEAD with it: before the commit when the list
        // came first, after it otherwise.
        Assert.NotNull(second);
        var main = Assert.Single(refs.Refs, gitRef => gitRef.IsHead);
        Assert.Equal(listFirst ? first : second, main.TargetSha);
        Assert.Equal(new HeadState("main", main.TargetSha), refs.Head);
    }

    [Fact]
    public async Task Reads_every_remotes_fetch_lines_with_the_refs_in_gits_reading_order()
    {
        // Design 10.2: a remote name with dots keeps its case, a second section of one remote
        // comes later in the order, and the remotes are sorted as before.
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.ConfigAsync("remote.origin.url", Sibling(repo, "origin.git"));
        await repo.ConfigAsync("remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*");
        await repo.ConfigAsync("remote.Team.Dot.x.url", Sibling(repo, "team.git"));
        await repo.ConfigAsync("remote.Team.Dot.x.fetch", "+refs/heads/*:refs/remotes/Team.Dot.x/*");
        File.AppendAllText(
            Path.Combine(repo.Path, ".git", "config"),
            "[remote \"origin\"]\n\tfetch = refs/heads/main:refs/remotes/origin/trunk\n\tfetch = ^refs/heads/secret\n");
        await repo.GitAsync("update-ref", "refs/remotes/Team.Dot.x/main", "HEAD");

        var refs = await (await OpenAsync(repo)).ReadRefsAsync(TestCancelled);

        Assert.Equal(["Team.Dot.x", "origin"], refs.Remotes);
        Assert.NotNull(refs.Refspecs);
        Assert.Equal(
            [
                new FetchRefspec("origin", "refs/heads/*", "refs/remotes/origin/*", false, true),
                new FetchRefspec("Team.Dot.x", "refs/heads/*", "refs/remotes/Team.Dot.x/*", false, true),
                new FetchRefspec("origin", "refs/heads/main", "refs/remotes/origin/trunk", false, false),
                new FetchRefspec("origin", "refs/heads/secret", null, true, false),
            ],
            refs.Refspecs.Lines);
        Assert.Equal(RemoteSourceKind.AmbiguousRefs, refs.SourceOf("refs/remotes/origin/trunk").Kind);
        Assert.Equal(RemoteSourceKind.NotFetched, refs.SourceOf("refs/remotes/origin/secret").Kind);
        var dotted = Assert.Single(refs.Refs, gitRef => gitRef.Kind == RefKind.RemoteBranch);
        Assert.Equal(new GitRef("refs/remotes/Team.Dot.x/main", "Team.Dot.x/main", RefKind.RemoteBranch, await repo.HeadAsync(), RemoteName: "Team.Dot.x"), dotted);
        Assert.Equal((RemoteSourceKind.Mapped, "Team.Dot.x", "refs/heads/main"), Source(refs.SourceOf(dotted)));
    }

    [Fact]
    public async Task Reads_global_fetch_lines_before_the_repositorys_own_as_git_fetches_with_both()
    {
        // Not --local (10.2): git fetches with a global line too, and prints it first.
        using var lab = new ConfigLab();
        lab.WriteGlobal("[remote \"origin\"]\n\tfetch = +refs/pull/*/head:refs/remotes/origin/pr/*\n[remote \"everywhere\"]\n\turl = x\n");
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.ConfigAsync("remote.origin.url", Sibling(repo, "origin.git"));
        await repo.ConfigAsync("remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*");

        var refs = await (await lab.OpenAsync(repo)).ReadRefsAsync(TestCancelled);
        var gitRemote = await lab.Runner.RunAsync(new GitCommand("remote") { WorkingDirectory = repo.Path }, TestCancelled);

        Assert.Equal(
            [
                new FetchRefspec("origin", "refs/pull/*/head", "refs/remotes/origin/pr/*", false, true),
                new FetchRefspec("origin", "refs/heads/*", "refs/remotes/origin/*", false, true),
            ],
            refs.Refspecs!.Lines);
        Assert.Equal(["everywhere", "origin"], refs.Remotes);
        Assert.Equal(GitRemoteNames(gitRemote), refs.Remotes);
        Assert.Equal(RemoteSourceKind.AmbiguousRefs, refs.SourceOf("refs/remotes/origin/pr/1").Kind);
    }

    [Fact]
    public async Task No_fetch_line_gives_empty_refspecs_not_null()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        var repository = await OpenAsync(repo);

        var none = await repository.ReadRefsAsync(TestCancelled);
        await repo.ConfigAsync("remote.origin.url", Sibling(repo, "origin.git"));
        await repo.GitAsync("update-ref", "refs/remotes/origin/main", "HEAD");
        var urlOnly = await repository.ReadRefsAsync(TestCancelled);

        Assert.Empty(none.Remotes);
        Assert.NotNull(none.Refspecs);
        Assert.Empty(none.Refspecs.Lines);

        // G: a remote without a fetch line fetches no tracking ref; its remote is today's guess.
        Assert.Equal(["origin"], urlOnly.Remotes);
        Assert.NotNull(urlOnly.Refspecs);
        Assert.Empty(urlOnly.Refspecs.Lines);
        var tracking = Assert.Single(urlOnly.Refs, gitRef => gitRef.Kind == RefKind.RemoteBranch);
        Assert.Equal("origin", tracking.RemoteName);
        Assert.Equal(RemoteSourceKind.NotFetched, urlOnly.SourceOf(tracking).Kind);
        Assert.Null(urlOnly.LocalNameOf(tracking));
    }

    [Fact]
    public async Task Reads_every_remotes_urls_and_push_urls_as_configured_in_gits_reading_order()
    {
        // docs/phase-7a-design.md 1.2: the url and pushurl values the same read returns, kept as
        // configured (a user part kept, no insteadOf applied), a second section of one remote later
        // in the order; a remote with neither has none.
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.ConfigAsync("remote.origin.url", "https://OctoCat@github.com/octocat/hello.git");
        await repo.ConfigAsync("remote.Team.Dot.x.url", "git@github.com:team/x.git");
        await repo.ConfigAsync("remote.Team.Dot.x.pushurl", "https://github.com/team/x.git");
        await repo.ConfigAsync("remote.aliased.url", "gh:octocat/other.git");
        await repo.ConfigAsync("url.https://github.com/.insteadOf", "gh:");
        await repo.ConfigAsync("remote.onlyprune.prune", "true");
        File.AppendAllText(Path.Combine(repo.Path, ".git", "config"), "[remote \"origin\"]\n\turl = https://example.com/mirror.git\n");

        var refs = await (await OpenAsync(repo)).ReadRefsAsync(TestCancelled);

        Assert.Equal(["Team.Dot.x", "aliased", "onlyprune", "origin"], refs.Remotes);
        Assert.Equal(
            [
                new RemoteUrl("origin", "https://OctoCat@github.com/octocat/hello.git", IsPushUrl: false),
                new RemoteUrl("Team.Dot.x", "git@github.com:team/x.git", IsPushUrl: false),
                new RemoteUrl("Team.Dot.x", "https://github.com/team/x.git", IsPushUrl: true),
                new RemoteUrl("aliased", "gh:octocat/other.git", IsPushUrl: false),
                new RemoteUrl("origin", "https://example.com/mirror.git", IsPushUrl: false),
            ],
            refs.RemoteUrls);
    }

    [Fact]
    public async Task A_remote_branch_takes_the_remote_whose_line_fills_it_and_the_longest_name_when_two_or_none_do()
    {
        // 10.3: B2's namespace without the remote's name gets the remote that fetches it; a ref no
        // line fills, or one two remotes fill (T, as in the first test), keeps the longest
        // configured name, else its first part.
        using var repo = await TempRepo.CreateAsync();
        var head = await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.ConfigAsync("remote.origin.url", Sibling(repo, "origin.git"));
        await repo.ConfigAsync("remote.origin.fetch", "+refs/heads/*:refs/remotes/mirror/*");
        await repo.ConfigAsync("remote.team.url", Sibling(repo, "team.git"));
        await repo.ConfigAsync("remote.team.fetch", "+refs/heads/*:refs/remotes/team/*");
        await repo.ConfigAsync("remote.team/fork.url", Sibling(repo, "fork.git"));
        await repo.ConfigAsync("remote.team/fork.fetch", "+refs/heads/*:refs/remotes/team/fork/*");
        await repo.ConfigAsync("remote.pulls.url", Sibling(repo, "pulls.git"));
        await repo.ConfigAsync("remote.pulls.fetch", "+refs/heads/*:refs/remotes/pulls/*");
        await repo.GitAsync("config", "--add", "remote.pulls.fetch", "+refs/pull/*/head:refs/remotes/pulls/pr/*");
        foreach (var name in new[] { "mirror/main", "origin/stray", "team/fork/main", "team/main", "pulls/pr/1", "gone/topic" })
        {
            await repo.GitAsync("update-ref", "refs/remotes/" + name, head);
        }

        var refs = await (await OpenAsync(repo)).ReadRefsAsync(TestCancelled);

        Assert.Equal(["origin", "pulls", "team", "team/fork"], refs.Remotes);
        Assert.Equal(
            [
                ("refs/remotes/gone/topic", "gone"),
                ("refs/remotes/mirror/main", "origin"),
                ("refs/remotes/origin/stray", "origin"),
                ("refs/remotes/pulls/pr/1", "pulls"),
                ("refs/remotes/team/fork/main", "team/fork"),
                ("refs/remotes/team/main", "team"),
            ],
            refs.Refs.Where(gitRef => gitRef.Kind == RefKind.RemoteBranch).Select(gitRef => (gitRef.FullName, gitRef.RemoteName)));
        Assert.Equal("main", refs.LocalNameOf(refs.Refs.Single(gitRef => gitRef.FullName == "refs/remotes/mirror/main")));
        Assert.Equal("pr/1", refs.LocalNameOf(refs.Refs.Single(gitRef => gitRef.FullName == "refs/remotes/pulls/pr/1")));
        Assert.Null(refs.LocalNameOf(refs.Refs.Single(gitRef => gitRef.FullName == "refs/remotes/team/fork/main")));
    }

    [Fact]
    public async Task The_remotes_are_those_git_remote_lists()
    {
        // 0.5's "names and lines in one read": a dotted name whole, a remote with only prune, a
        // remote.pushDefault and a legacy .git/remotes or .git/branches file, which both leave out.
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.ConfigAsync("remote.Team.Dot.x.url", Sibling(repo, "team.git"));
        await repo.ConfigAsync("remote.Team.Dot.x.fetch", "+refs/heads/*:refs/remotes/Team.Dot.x/*");
        await repo.ConfigAsync("remote.onlyprune.prune", "true");
        await repo.ConfigAsync("remote.onlypushurl.pushurl", Sibling(repo, "push.git"));
        await repo.ConfigAsync("remote.origin.url", Sibling(repo, "origin.git"));
        await repo.ConfigAsync("remote.pushDefault", "origin");
        Directory.CreateDirectory(Path.Combine(repo.Path, ".git", "remotes"));
        File.WriteAllText(Path.Combine(repo.Path, ".git", "remotes", "legacyfile"), "URL: " + Sibling(repo, "legacy.git") + "\nPull: +refs/heads/*:refs/remotes/legacyfile/*\n");
        Directory.CreateDirectory(Path.Combine(repo.Path, ".git", "branches"));
        File.WriteAllText(Path.Combine(repo.Path, ".git", "branches", "legacybranch"), Sibling(repo, "legacy.git") + "\n");

        var refs = await (await OpenAsync(repo)).ReadRefsAsync(TestCancelled);

        Assert.Equal(["Team.Dot.x", "onlyprune", "onlypushurl", "origin"], refs.Remotes);
        Assert.Equal(GitRemoteNames(await repo.GitAsync("remote")), refs.Remotes);
        Assert.Equal([new FetchRefspec("Team.Dot.x", "refs/heads/*", "refs/remotes/Team.Dot.x/*", false, true)], refs.Refspecs!.Lines);
    }

    [Fact]
    public async Task A_refs_read_is_five_processes_and_git_remote_is_not_among_them()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.ConfigAsync("remote.origin.url", Sibling(repo, "origin.git"));
        await repo.ConfigAsync("remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*");
        var calls = new System.Collections.Concurrent.ConcurrentQueue<string[]>();
        var armed = false;
        var runner = new OrderingRunner(
            NewRunner(),
            before: command =>
            {
                if (armed)
                {
                    calls.Enqueue([.. command.Arguments]);
                }

                return Task.CompletedTask;
            },
            after: _ => { });
        var repository = await GitRepository.OpenAsync(runner, repo.Path, TestCancelled);
        armed = true;

        var refs = await repository.ReadRefsAsync(TestCancelled);

        GitCallBudget.AssertAtMost(calls.Select(arguments => "git " + string.Join(' ', arguments)).ToList(), 5, "the refs read");
        Assert.DoesNotContain(calls, arguments => arguments.SequenceEqual(["remote"]));
        Assert.Single(calls, arguments => arguments.SequenceEqual(["config", "--null", "--get-regexp", @"^remote\."]));
        Assert.Equal(["origin"], refs.Remotes);
    }

    [Fact]
    public async Task A_fetch_line_that_names_no_source_is_left_out_and_logged_once()
    {
        // Git takes ":refs/remotes/origin/x" (an empty source, its HEAD); its tracking setup gives
        // a branch with an empty name, so the mapping leaves the line out (10.1). A line git
        // refuses makes every refs read fail with git's "invalid refspec", as "git remote" did.
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        await repo.ConfigAsync("remote.origin.url", Sibling(repo, "origin.git"));
        await repo.GitAsync("config", "--add", "remote.origin.fetch", ":refs/remotes/origin/x");
        await repo.GitAsync("config", "--add", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*");
        var log = new ListLog();
        var repository = await GitRepository.OpenAsync(NewRunner(), repo.Path, TestCancelled, log);

        var refs = await repository.ReadRefsAsync(TestCancelled);
        await repository.ReadRefsAsync(TestCancelled);

        Assert.Equal([new FetchRefspec("origin", "refs/heads/*", "refs/remotes/origin/*", false, true)], refs.Refspecs!.Lines);
        var warning = Assert.Single(log.Entries, entry => entry.Level == Core.Logging.LogLevel.Warning);
        Assert.Contains(":refs/remotes/origin/x", warning.Message, StringComparison.Ordinal);
        Assert.Contains("origin", warning.Message, StringComparison.Ordinal);

        await repo.GitAsync("config", "--add", "remote.origin.fetch", "refs/heads/*:refs/remotes/origin/x");
        await Assert.ThrowsAsync<GitException>(() => repository.ReadRefsAsync(TestCancelled));
    }

    [Theory]
    [InlineData("", 0, 0, false)]
    [InlineData("[ahead 3]", 3, 0, false)]
    [InlineData("[behind 12]", 0, 12, false)]
    [InlineData("[ahead 1, behind 2]", 1, 2, false)]
    [InlineData("[gone]", 0, 0, true)]
    public void Reads_the_upstream_track_text(string track, int ahead, int behind, bool gone) =>
        Assert.Equal((ahead, behind, gone), GitRepository.ParseTrack(track));

    /// <summary>The names <c>git remote</c> printed, sorted as the refs read sorts them.</summary>
    private static List<string> GitRemoteNames(GitResult result)
    {
        Assert.True(result.Succeeded, result.StandardError);
        var names = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static (RemoteSourceKind Kind, string? Remote, string? RemoteRef) Source(RemoteSource source) =>
        (source.Kind, source.Remote, source.RemoteRef);

    /// <summary>A runner that runs <paramref name="before"/> ahead of each call and <paramref name="after"/> once it has ended: the order of calls that run side by side, made certain.</summary>
    private sealed class OrderingRunner(IGitRunner real, Func<GitCommand, Task> before, Action<GitCommand> after) : IGitRunner
    {
        public string ExecutablePath => real.ExecutablePath;

        public async Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
        {
            await before(command);
            var result = await real.RunAsync(command, cancellationToken);
            after(command);
            return result;
        }
    }
}
