using System.Collections.Concurrent;
using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary><see cref="GitRepository.LoadCommitsAsync"/>: the history the graph shows, in pages.</summary>
public class CommitLogTests(CommitLogTests.LongHistory longHistory) : IClassFixture<CommitLogTests.LongHistory>
{
    /// <summary>A line of 5,000 commits on main, built once for the tests that need many pages.</summary>
    public sealed class LongHistory : IAsyncLifetime
    {
        public const int Count = 5000;

        public TempRepo Repo { get; private set; } = null!;

        public GitRepository Repository { get; private set; } = null!;

        public RepoRefs Refs { get; private set; } = null!;

        public async ValueTask InitializeAsync()
        {
            Repo = await TempRepo.CreateAsync("long");
            await ImportAsync(Repo, HistoryStream.Line(Count));
            Repository = await OpenAsync(Repo);
            Refs = await Repository.ReadRefsAsync(TestCancelled);
        }

        public ValueTask DisposeAsync()
        {
            Repo?.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<(RepoRefs Refs, List<IReadOnlyList<CommitInfo>> Pages)> LoadAsync(TempRepo repo)
    {
        var repository = await OpenAsync(repo);
        var refs = await repository.ReadRefsAsync(TestCancelled);
        var pages = new List<IReadOnlyList<CommitInfo>>();
        await repository.LoadCommitsAsync(refs, pages.Add, TestCancelled);
        return (refs, pages);
    }

    [Fact]
    public async Task Delivers_every_commit_once_in_date_order_with_its_parents_and_author()
    {
        // Two branches and a merge, a tag on a commit no branch holds, a remote branch, and a
        // detached HEAD on a commit that only HEAD reaches. The refs/visualcommit/ refs are
        // neither branches nor tags, so the graph does not see them.
        using var repo = await TempRepo.CreateAsync();
        var history = new HistoryStream();
        var rootMark = history.Commit("refs/heads/main", "Root", file: "a.txt", content: "a\n");
        var featureMark = history.Commit("refs/heads/feature", "Feature work", from: rootMark, file: "f.txt", content: "f\n");
        var mainWorkMark = history.Commit("refs/heads/main", "Main work", file: "a.txt", content: "b\n");
        var mergeMark = history.Commit("refs/heads/main", "Merge branch 'feature'", merge: featureMark);
        var taggedMark = history.Commit("refs/visualcommit/dropped", "Only a tag holds this", from: mergeMark, file: "d.txt", content: "d\n");
        history.Reset("refs/tags/keep", taggedMark);
        history.Reset("refs/remotes/origin/main", rootMark);
        var detachedMark = history.Commit("refs/visualcommit/detached", "Only HEAD holds this", from: mergeMark, file: "h.txt", content: "h\n");
        var marks = await ImportAsync(repo, history);
        var (root, feature, mainWork, merge, tagged, detached) =
            (marks[rootMark], marks[featureMark], marks[mainWorkMark], marks[mergeMark], marks[taggedMark], marks[detachedMark]);
        await repo.GitAsync("update-ref", "--no-deref", "HEAD", detached);

        var (_, pages) = await LoadAsync(repo);

        var commits = Assert.Single(pages);
        Assert.Equal([detached, tagged, merge, mainWork, feature, root], commits.Select(commit => commit.Sha));
        Assert.All(commits, commit => Assert.Equal(CommitKind.Commit, commit.Kind));

        var bySha = commits.ToDictionary(commit => commit.Sha);
        Assert.Equal([mainWork, feature], bySha[merge].Parents);
        Assert.Equal([merge], bySha[detached].Parents);
        Assert.Empty(bySha[root].Parents);
        Assert.Equal("Merge branch 'feature'", bySha[merge].Subject);

        var rootCommit = bySha[root];
        Assert.Equal(TempRepo.AuthorName, rootCommit.AuthorName);
        Assert.Equal(TempRepo.AuthorEmail, rootCommit.AuthorEmail);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), rootCommit.AuthorDate);
        Assert.Equal(rootCommit.AuthorDate, rootCommit.CommitDate);
        Assert.Equal("Root", rootCommit.Subject);
        Assert.Equal(root[..7], rootCommit.ShortSha);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 5, 0, TimeSpan.Zero), bySha[detached].CommitDate);

        // One string per author: a long history does not hold a copy for every commit.
        Assert.All(commits, commit => Assert.Same(rootCommit.AuthorName, commit.AuthorName));
    }

    [Fact]
    public async Task A_long_history_comes_in_a_small_first_page_and_then_pages_of_two_thousand()
    {
        var threads = new ConcurrentBag<bool>();
        var pages = new List<IReadOnlyList<CommitInfo>>();

        await longHistory.Repository.LoadCommitsAsync(
            longHistory.Refs,
            page =>
            {
                threads.Add(Thread.CurrentThread.IsBackground);
                pages.Add(page);
            },
            TestCancelled);

        Assert.Equal([100, 2000, 2000, 900], pages.Select(page => page.Count));
        Assert.All(threads, Assert.True);

        // Every commit exactly once, each above its parent.
        var commits = pages.SelectMany(page => page).ToList();
        Assert.Equal(LongHistory.Count, commits.Select(commit => commit.Sha).Distinct().Count());
        Assert.Equal($"Commit {LongHistory.Count}", commits[0].Subject);
        Assert.Equal("Commit 1", commits[^1].Subject);
        for (var i = 0; i < commits.Count - 1; i++)
        {
            Assert.Equal(commits[i + 1].Sha, Assert.Single(commits[i].Parents));
        }
    }

    [Fact]
    public async Task Cancelling_stops_the_load_and_hands_over_no_more_pages()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);
        var pages = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => longHistory.Repository.LoadCommitsAsync(
            longHistory.Refs,
            _ =>
            {
                pages++;
                cancellation.Cancel();
            },
            cancellation.Token));

        Assert.Equal(1, pages);
    }

    [Fact]
    public async Task An_exception_from_the_page_handler_ends_the_load_with_that_exception()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => longHistory.Repository.LoadCommitsAsync(
            longHistory.Refs,
            _ => throw new InvalidOperationException("The view failed."),
            TestCancelled));

        Assert.Equal("The view failed.", exception.Message);
    }

    [Fact]
    public async Task A_stash_lies_above_the_commit_it_was_made_on()
    {
        using var repo = await TempRepo.CreateAsync();
        var history = new HistoryStream();
        var firstMark = history.Commit("refs/heads/main", "First", file: "a.txt", content: "a\n");
        var secondMark = history.Commit("refs/heads/main", "Second", file: "a.txt", content: "b\n");
        var thirdMark = history.Commit("refs/heads/main", "Third", file: "a.txt", content: "c\n");
        history.Commit("refs/visualcommit/lost", "Lost", file: "lost.txt", content: "lost\n");
        var marks = await ImportAsync(repo, history);
        var (first, second, third) = (marks[firstMark], marks[secondMark], marks[thirdMark]);

        // A stash made on Second half a minute after it (12:01:30): it belongs between Second and
        // Third. The import left the working tree empty; the checkout fills it.
        await repo.GitAsync("checkout", "--quiet", "--force", "--detach", second);
        repo.WriteFile("a.txt", "work on second\n");
        await StashAsync(repo, "On second", new DateTimeOffset(2026, 1, 1, 12, 1, 30, TimeSpan.Zero));

        // A stash made on First later than everything: it belongs at the top.
        await repo.GitAsync("checkout", "--quiet", "--detach", first);
        repo.WriteFile("a.txt", "work on first\n");
        await StashAsync(repo, "Late, on first", new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.Zero));

        // A stash on a commit that no branch holds, older than everything: it ends the history.
        await repo.GitAsync("checkout", "--quiet", "--detach", "refs/visualcommit/lost");
        repo.WriteFile("lost.txt", "work on lost\n");
        await StashAsync(repo, "On lost", new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await repo.CheckoutAsync("main");

        var (refs, pages) = await LoadAsync(repo);

        var commits = Assert.Single(pages);
        Assert.Equal(
            ["Late, on first", "Third", "On second", "Second", "First", "On lost"],
            commits.Select(commit => commit.Kind == CommitKind.Stash ? commit.Subject.Split(": ", 2)[1] : commit.Subject));
        Assert.Equal(
            [CommitKind.Stash, CommitKind.Commit, CommitKind.Stash, CommitKind.Commit, CommitKind.Commit, CommitKind.Stash],
            commits.Select(commit => commit.Kind));

        var stashOnSecond = commits[2];
        var stashEntry = refs.Stashes.Single(stash => stash.Sha == stashOnSecond.Sha);
        Assert.Equal([second], stashOnSecond.Parents);
        Assert.Equal(stashEntry.Message, stashOnSecond.Subject);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 1, 30, TimeSpan.Zero), stashOnSecond.CommitDate);
        Assert.Equal(TempRepo.AuthorName, stashOnSecond.AuthorName);
        Assert.Equal([first], commits[0].Parents);
        Assert.Equal(third, commits[1].Sha);
    }

    [Fact]
    public async Task A_page_handler_that_takes_its_time_still_gets_every_commit()
    {
        // The second page fills while git still has 10 commits to print. Git prints them and
        // exits while the handler holds that page for longer than a process left behind is
        // waited for; the last 10 commits must still arrive.
        const int Count = 2110;
        using var repo = await TempRepo.CreateAsync();
        await ImportAsync(repo, HistoryStream.Line(Count));
        var repository = await OpenAsync(repo);
        var refs = await repository.ReadRefsAsync(TestCancelled);
        var pages = new List<IReadOnlyList<CommitInfo>>();

        await repository.LoadCommitsAsync(
            refs,
            page =>
            {
                pages.Add(page);
                if (page.Count == CommitPager.PageSize)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(3));
                }
            },
            TestCancelled);

        Assert.Equal([CommitPager.FirstPageSize, CommitPager.PageSize, 10], pages.Select(page => page.Count));
        var commits = pages.SelectMany(page => page).ToList();
        Assert.Equal(Count, commits.Select(commit => commit.Sha).Distinct().Count());
        Assert.Equal("Commit 1", commits[^1].Subject);
    }

    [Fact]
    public async Task A_stash_comes_after_a_commit_of_the_same_second_on_another_branch()
    {
        using var repo = await TempRepo.CreateAsync();
        var history = new HistoryStream();
        var firstMark = history.Commit("refs/heads/main", "First", file: "a.txt", content: "a\n");
        history.Commit("refs/heads/main", "Second", file: "a.txt", content: "b\n");
        history.Commit("refs/heads/other", "Other", from: firstMark, file: "o.txt", content: "o\n");
        await ImportAsync(repo, history);

        // Stashed on Second at 12:02:00, the very second Other was committed. The stash goes
        // before the first commit that is older than it (D54): after Other, before Second.
        await repo.GitAsync("checkout", "--quiet", "--force", "main");
        repo.WriteFile("a.txt", "work on second\n");
        await StashAsync(repo, "Same second", new DateTimeOffset(2026, 1, 1, 12, 2, 0, TimeSpan.Zero));

        var (_, pages) = await LoadAsync(repo);

        var commits = Assert.Single(pages);
        Assert.Equal(
            ["Other", "Same second", "Second", "First"],
            commits.Select(commit => commit.Kind == CommitKind.Stash ? commit.Subject.Split(": ", 2)[1] : commit.Subject));
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 2, 0, TimeSpan.Zero), commits[0].CommitDate);
        Assert.Equal(commits[0].CommitDate, commits[1].CommitDate);
    }

    [Fact]
    public async Task A_commit_whose_dates_git_cannot_print_appears_with_the_epoch_date_and_its_details_load()
    {
        using var repo = await TempRepo.CreateAsync();
        var history = new HistoryStream();
        var firstMark = history.Commit("refs/heads/main", "First", file: "a.txt", content: "a\n");
        var first = (await ImportAsync(repo, history))[firstMark];
        var tree = (await repo.GitAsync("rev-parse", first + "^{tree}")).StandardOutput.Trim();

        // Idents without a time zone, as broken tools have written them. Git stores such a
        // commit only with --literally and cannot print its dates; it shows them as 1970.
        var broken = (await repo.GitWithInputAsync(
            $"tree {tree}\nparent {first}\nauthor Broken Clock <broken@example.com> 1767268800\ncommitter Broken Clock <broken@example.com> 1767268800\n\nBroken dates\n",
            "hash-object", "-t", "commit", "--literally", "-w", "--stdin")).StandardOutput.Trim();
        var third = (await repo.GitAsync("commit-tree", tree, "-p", broken, "-m", "Third")).StandardOutput.Trim();
        await repo.GitAsync("update-ref", "refs/heads/main", third);

        var (_, pages) = await LoadAsync(repo);

        var commits = Assert.Single(pages);
        Assert.Equal([third, broken, first], commits.Select(commit => commit.Sha));
        var shown = commits[1];
        Assert.Equal([first], shown.Parents);
        Assert.Equal("Broken Clock", shown.AuthorName);
        Assert.Equal("Broken dates", shown.Subject);
        Assert.Equal(DateTimeOffset.UnixEpoch, shown.AuthorDate);
        Assert.Equal(DateTimeOffset.UnixEpoch, shown.CommitDate);

        var details = await (await OpenAsync(repo)).ReadCommitDetailsAsync(broken, TestCancelled);
        Assert.Equal(broken, details.Sha);
        Assert.Equal([first], details.Parents);
        Assert.Equal("broken@example.com", details.CommitterEmail);
        Assert.Equal(DateTimeOffset.UnixEpoch, details.AuthorDate);
        Assert.Equal(DateTimeOffset.UnixEpoch, details.CommitDate);
        Assert.Equal("Broken dates", details.Subject);
        Assert.Empty(details.Files);
    }

    [Fact]
    public async Task An_unborn_head_beside_other_branches_still_shows_their_history()
    {
        using var repo = await TempRepo.CreateAsync();
        var history = new HistoryStream();
        var firstMark = history.Commit("refs/heads/main", "First", file: "a.txt", content: "a\n");
        var first = (await ImportAsync(repo, history))[firstMark];
        await repo.GitAsync("symbolic-ref", "HEAD", "refs/heads/fresh");

        var (refs, pages) = await LoadAsync(repo);

        Assert.True(refs.Head.IsUnborn);
        Assert.Equal("fresh", refs.Head.BranchName);
        Assert.Equal([first], Assert.Single(pages).Select(commit => commit.Sha));
    }

    /// <summary>Stashes the working tree's changes with a stash date of its own, which the repo builder's clock cannot give.</summary>
    private static async Task StashAsync(TempRepo repo, string message, DateTimeOffset date)
    {
        var time = date.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        var environment = new Dictionary<string, string?>(repo.Environment)
        {
            ["GIT_AUTHOR_DATE"] = time,
            ["GIT_COMMITTER_DATE"] = time,
        };
        var command = new GitCommand("stash", "push", "--quiet", "-m", message) { WorkingDirectory = repo.Path, Environment = environment };
        (await repo.Runner.RunAsync(command, TestCancelled)).EnsureSuccess(command);
    }
}
