using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The graph loaded with what it hides or solos (docs/phase-5-design.md 8.2, 0.6, D178), against
/// real git: the commits are exactly those git shows for the refs the filter leaves, named one by
/// one, in git's order; HEAD's history always shows; past the command line's limit the revisions
/// go on standard input; a name that no longer exists changes nothing; a stash is placed by date
/// when HEAD, a shown ref or a row already loaded names its base, waits for its base otherwise,
/// and is left out when its base is not shown; and without a filter the command is today's. The
/// arguments themselves are in <see cref="GraphFilterArgumentsTests"/>.
/// </summary>
public class GraphFilterTests(GraphFilterTests.FiltersScenario scenario) : IClassFixture<GraphFilterTests.FiltersScenario>
{
    private const string Alpha = "refs/heads/feature/alpha";
    private const string OriginAlpha = "refs/remotes/origin/feature/alpha";
    private const string Beta = "refs/heads/feature/beta";
    private const string OriginBeta = "refs/remotes/origin/feature/beta";

    /// <summary>A branch that no longer exists: a stale name the session still holds (8.4).</summary>
    private const string Gone = "refs/heads/gone";

    /// <summary>
    /// A repository like phase 5's "filters" scenario (15.1), built here once for the class:
    /// <list type="bullet">
    /// <item><c>main</c> (checked out): Initial, Main one, the merge of <c>feature/merged</c>; the
    /// annotated tag <c>v1</c> on Main one; pushed as <c>origin/main</c>.</item>
    /// <item><c>feature/alpha</c> from the merge: Alpha one, Alpha two, with its upstream
    /// <c>origin/feature/alpha</c> one commit ahead (Alpha three).</item>
    /// <item><c>feature/beta</c> from Initial: Beta one to three, pushed as its upstream
    /// <c>origin/feature/beta</c>, at which <c>origin/HEAD</c> points.</item>
    /// <item>A stash on Beta three, and a newer one on Alpha one, which only the alpha refs reach.</item>
    /// <item>A branch <c>x</c> from Initial (X one, X two) and a tag <c>x</c> on X one.</item>
    /// <item><c>feature/local</c> from the merge (Local work), whose upstream is <c>main</c> itself.</item>
    /// </list>
    /// Every commit and stash is a minute after the one before, from 2026-01-01 12:00.
    /// </summary>
    public sealed class FiltersScenario : IAsyncLifetime
    {
        public TempRepo Repo { get; private set; } = null!;

        /// <summary>The commits by message, and the two stashes as "Stash on beta" and "Stash on alpha one".</summary>
        public IReadOnlyDictionary<string, string> Sha { get; private set; } = null!;

        public async ValueTask InitializeAsync()
        {
            var repo = Repo = await TempRepo.CreateAsync("filters");
            var sha = new Dictionary<string, string>(StringComparer.Ordinal);
            await repo.AddBareRemoteAsync();

            sha["Initial"] = await repo.CommitFileAsync("README.md", "Filters\n", "Initial");
            sha["Main one"] = await repo.CommitFileAsync("main.txt", "one\n", "Main one");
            await repo.GitAsync("checkout", "--quiet", "-b", "feature/merged");
            sha["Merged work"] = await repo.CommitFileAsync("merged.txt", "merged\n", "Merged work");
            await repo.CheckoutAsync("main");
            sha["Merge"] = await repo.MergeAsync("feature/merged");
            await repo.PushAsync("origin", true, "main");

            await repo.GitAsync("checkout", "--quiet", "-b", "feature/alpha");
            sha["Alpha one"] = await repo.CommitFileAsync("alpha.txt", "one\n", "Alpha one");
            sha["Alpha two"] = await repo.CommitFileAsync("alpha.txt", "two\n", "Alpha two");
            sha["Alpha three"] = await repo.CommitFileAsync("alpha.txt", "three\n", "Alpha three");
            await repo.PushAsync("origin", true, "feature/alpha");
            await repo.GitAsync("reset", "--quiet", "--hard", sha["Alpha two"]);

            await repo.GitAsync("checkout", "--quiet", "-b", "feature/beta", sha["Initial"]);
            sha["Beta one"] = await repo.CommitFileAsync("beta.txt", "one\n", "Beta one");
            sha["Beta two"] = await repo.CommitFileAsync("beta.txt", "two\n", "Beta two");
            sha["Beta three"] = await repo.CommitFileAsync("beta.txt", "three\n", "Beta three");
            await repo.PushAsync("origin", true, "feature/beta");
            await repo.GitAsync("symbolic-ref", "refs/remotes/origin/HEAD", OriginBeta);

            repo.WriteFile("beta.txt", "work on beta\n");
            sha["Stash on beta"] = await repo.StashAsync("Stash on beta");
            await repo.DetachAsync(sha["Alpha one"]);
            repo.WriteFile("alpha.txt", "work on alpha one\n");
            sha["Stash on alpha one"] = await repo.StashAsync("Stash on alpha one");

            await repo.GitAsync("checkout", "--quiet", "-b", "x", sha["Initial"]);
            sha["X one"] = await repo.CommitFileAsync("x.txt", "one\n", "X one");
            sha["X two"] = await repo.CommitFileAsync("x.txt", "two\n", "X two");
            await repo.TagAsync("x", sha["X one"]);

            await repo.GitAsync("checkout", "--quiet", "-b", "feature/local", sha["Merge"]);
            sha["Local work"] = await repo.CommitFileAsync("local.txt", "local\n", "Local work");
            await repo.ConfigAsync("branch.feature/local.remote", ".");
            await repo.ConfigAsync("branch.feature/local.merge", "refs/heads/main");

            await repo.AnnotatedTagAsync("v1", "Version 1", sha["Main one"]);
            await repo.CheckoutAsync("main");
            Sha = sha;
        }

        public ValueTask DisposeAsync()
        {
            Repo?.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// What is hidden in <see cref="Hiding_shows_exactly_what_git_shows_for_every_other_ref_and_HEAD"/>,
    /// one set of full names per case, separated by spaces.
    /// </summary>
    public static TheoryData<string> HiddenSets => new()
    {
        // A branch with its upstream: its own commits go, and the stash on one of them.
        $"{Alpha} {OriginAlpha}",

        // A branch alone: its upstream, one commit ahead, keeps every commit of it on screen.
        Alpha,

        // A branch and its upstream at which origin/HEAD points: origin/HEAD is excluded too.
        $"{Beta} {OriginBeta}",

        // The branch x, not the tag x: X one stays through the tag.
        "refs/heads/x",

        // A branch whose upstream is local: only the branch is named, main stays.
        "refs/heads/feature/local",

        // A merged branch: nothing goes, its commit is main's.
        "refs/heads/feature/merged",

        // The checked-out branch with its upstream: HEAD keeps its history.
        "refs/heads/main refs/remotes/origin/main",

        // Every branch: the tags and HEAD are left.
        $"{Alpha} {Beta} refs/heads/feature/local refs/heads/feature/merged refs/heads/main refs/heads/x {OriginAlpha} {OriginBeta} refs/remotes/origin/main",
    };

    [Fact]
    public async Task Without_a_filter_or_with_an_empty_one_the_command_and_the_rows_are_todays()
    {
        var calls = new GitCallLog();

        var today = await LoadTodayAsync(scenario.Repo, calls);
        var withNull = await LoadAsync(scenario.Repo, null, calls);
        var withEmpty = await LoadAsync(scenario.Repo, new GraphFilter([], [], ["origin"]), calls);

        var logs = LogCalls(calls);
        Assert.Equal(3, logs.Count);
        Assert.EndsWith(" --branches --remotes --tags --ignore-missing HEAD --", logs[0], StringComparison.Ordinal);
        Assert.Equal(logs[0], logs[1]);
        Assert.Equal(logs[0], logs[2]);
        Assert.Equal(Describe(today), Describe(withNull));
        Assert.Equal(Describe(today), Describe(withEmpty));
        Assert.Equal(2, today.Count(commit => commit.Kind == CommitKind.Stash));
    }

    [Theory]
    [MemberData(nameof(HiddenSets))]
    public async Task Hiding_shows_exactly_what_git_shows_for_every_other_ref_and_HEAD(string names)
    {
        var hidden = names.Split(' ');
        var filter = new GraphFilter(hidden, [], ["origin"]);
        var refs = await ReadRefsAsync(scenario.Repo);

        var rows = await LoadAsync(scenario.Repo, filter);

        var shown = refs.Refs.Select(gitRef => gitRef.FullName).Where(name => !hidden.Contains(name, StringComparer.Ordinal));
        Assert.Equal(await GitShowsAsync(scenario.Repo, shown), Commits(rows));
        AssertStashesWhereTheirBaseIs(rows, refs);
    }

    [Fact]
    public async Task Hiding_a_branch_with_its_upstream_takes_its_commits_and_its_stash_away()
    {
        var rows = await LoadAsync(scenario.Repo, new GraphFilter([Alpha, OriginAlpha], [], ["origin"]));

        var commits = Commits(rows);
        Assert.DoesNotContain(scenario.Sha["Alpha one"], commits);
        Assert.DoesNotContain(scenario.Sha["Alpha two"], commits);
        Assert.DoesNotContain(scenario.Sha["Alpha three"], commits);
        Assert.Equal([scenario.Sha["Stash on beta"]], Stashes(rows));
    }

    [Fact]
    public async Task Hiding_a_remote_branch_excludes_its_remotes_HEAD_which_otherwise_keeps_it_on_screen()
    {
        var rows = await LoadAsync(scenario.Repo, new GraphFilter([Beta, OriginBeta], [], ["origin"]));

        Assert.DoesNotContain(scenario.Sha["Beta one"], Commits(rows));
        Assert.Equal([scenario.Sha["Stash on alpha one"]], Stashes(rows));

        // Git's own behaviour (0.6): without origin/HEAD's exclude, origin/HEAD keeps beta's history.
        var withoutRemoteHead = await scenario.Repo.GitAsync(
            "log", "--format=%H", "--exclude=feature/beta", "--branches", "--exclude=origin/feature/beta", "--remotes", "--tags", "--");
        Assert.Contains(scenario.Sha["Beta one"], withoutRemoteHead.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_checked_out_branch_shows_while_it_is_hidden()
    {
        using var copy = await scenario.Repo.CopyAsync();
        await copy.CheckoutAsync("feature/beta");
        var refs = await ReadRefsAsync(copy);

        var rows = await LoadAsync(copy, new GraphFilter([Beta, OriginBeta], [], ["origin"]));

        Assert.Equal(await GitShowsAsync(copy, refs.Refs.Select(gitRef => gitRef.FullName).Where(name => name is not (Beta or OriginBeta))), Commits(rows));
        Assert.Contains(scenario.Sha["Beta three"], Commits(rows));
        Assert.Equal(2, Stashes(rows).Count);
    }

    [Fact]
    public async Task Solo_shows_the_soloed_refs_and_HEADs_history_and_leaves_the_other_stash_out()
    {
        var rows = await LoadAsync(scenario.Repo, new GraphFilter([], [Beta, OriginBeta], ["origin"]));

        Assert.Equal(await GitShowsAsync(scenario.Repo, [Beta, OriginBeta]), Commits(rows));
        Assert.DoesNotContain(scenario.Sha["Alpha two"], Commits(rows));
        Assert.DoesNotContain(scenario.Sha["X two"], Commits(rows));
        Assert.Contains(scenario.Sha["Main one"], Commits(rows));

        // The stash on beta's tip is placed by date, as without a filter: first, newer than every
        // commit shown. The stash on Alpha one is left out: its base is not shown.
        Assert.Equal(scenario.Sha["Stash on beta"], rows[0].Sha);
        Assert.Equal([scenario.Sha["Stash on beta"]], Stashes(rows));
    }

    [Fact]
    public async Task A_stash_whose_base_is_not_a_tip_waits_until_a_shown_commit_names_its_base()
    {
        // Without a filter the stash on Alpha one, newer than every alpha commit, is placed above
        // the first older commit; with alpha soloed nothing says Alpha one is shown until Alpha
        // two names it, so the stash goes in just after Alpha two (amending D54 while a filter is on).
        var today = await LoadTodayAsync(scenario.Repo);
        var rows = await LoadAsync(scenario.Repo, new GraphFilter([], [Alpha, OriginAlpha], ["origin"]));

        var stash = scenario.Sha["Stash on alpha one"];
        Assert.Equal(today.FindIndex(row => row.Sha == scenario.Sha["Beta three"]) - 2, today.FindIndex(row => row.Sha == stash));
        Assert.Equal(
            [scenario.Sha["Alpha three"], scenario.Sha["Alpha two"], stash, scenario.Sha["Alpha one"], scenario.Sha["Merge"]],
            rows.Take(5).Select(row => row.Sha));
        Assert.Equal([stash], Stashes(rows));
    }

    [Fact]
    public async Task A_stash_on_a_detached_HEADs_commit_is_placed_by_date_as_without_a_filter()
    {
        // HEAD's commit is always shown (0.6, 8.1), so the pager knows it from the start. Here only
        // HEAD names Initial: without HEAD among the known commits, the stash on it would wait
        // until X one, through the tag x, names Initial, and go in two rows down.
        using var copy = await scenario.Repo.CopyAsync();
        await copy.DetachAsync(scenario.Sha["Initial"]);
        copy.WriteFile("README.md", "Work on Initial\n");
        var stash = await copy.StashAsync("Stash on Initial");
        var refs = await ReadRefsAsync(copy);
        Assert.DoesNotContain(refs.Refs, gitRef => gitRef.TargetSha == scenario.Sha["Initial"]);

        var today = await LoadTodayAsync(copy);
        var rows = await LoadAsync(copy, new GraphFilter(["refs/heads/x"], [], ["origin"]));

        Assert.Equal(0, today.FindIndex(row => row.Sha == stash));
        Assert.Equal(0, rows.FindIndex(row => row.Sha == stash));
        Assert.Equal(3, Stashes(rows).Count);
        AssertStashesWhereTheirBaseIs(rows, refs);
    }

    /// <summary>
    /// A name that no longer exists, hidden or soloed, with the names of the filter it belongs to
    /// in <see cref="A_hidden_or_soloed_name_that_no_longer_exists_changes_nothing"/>: a branch, and
    /// a remote branch of a known remote.
    /// </summary>
    public static TheoryData<bool, string> StaleNames => new()
    {
        { false, $"{Gone} {Alpha} {OriginAlpha} refs/remotes/origin/gone" },
        { true, $"{Gone} {Beta}" },
    };

    [Theory]
    [MemberData(nameof(StaleNames))]
    public async Task A_hidden_or_soloed_name_that_no_longer_exists_changes_nothing(bool solo, string names)
    {
        // The session keeps a stale name until the repository's first refs read drops it (8.4), so
        // the first load runs with it. Excluded, it matches nothing; soloed, --ignore-missing before
        // it skips it (0.6), where git would otherwise stop at a bad revision.
        var refs = await ReadRefsAsync(scenario.Repo);
        var named = names.Split(' ');
        var existing = named.Where(name => refs.Refs.Any(gitRef => gitRef.FullName == name)).ToList();
        Assert.Equal(solo ? 1 : 2, existing.Count);
        GraphFilter Filter(IEnumerable<string> refNames) => solo ? new([], refNames, ["origin"]) : new(refNames, [], ["origin"]);
        var calls = new GitCallLog();

        var rows = await LoadAsync(scenario.Repo, Filter(named), calls);

        Assert.Contains(solo ? Gone : "--exclude=gone", Assert.Single(LogCalls(calls)), StringComparison.Ordinal);
        var shown = solo ? existing : refs.Refs.Select(gitRef => gitRef.FullName).Where(name => !existing.Contains(name, StringComparer.Ordinal));
        Assert.Equal(await GitShowsAsync(scenario.Repo, shown), Commits(rows));
        Assert.Equal(Describe(await LoadAsync(scenario.Repo, Filter(existing))), Describe(rows));
    }

    [Fact]
    public async Task Past_the_command_lines_limit_a_soloed_name_that_no_longer_exists_changes_nothing()
    {
        using var copy = await scenario.Repo.CopyAsync();
        await AddManyBranchesAsync(copy);
        var filter = new GraphFilter([], [.. ManyNames, Gone], ["origin"]);
        Assert.True(filter.UsesStandardInput);
        var calls = new GitCallLog();

        var rows = await LoadAsync(copy, filter, calls);

        // The stale name is on standard input, after --ignore-missing on the command line (0.6).
        // Solo's input does not depend on the refs.
        Assert.EndsWith(" --ignore-missing --stdin --", Assert.Single(LogCalls(calls)), StringComparison.Ordinal);
        Assert.Contains(Gone + "\n", filter.StandardInput(await ReadRefsAsync(copy)), StringComparison.Ordinal);
        Assert.Equal(Describe(await LoadAsync(copy, new GraphFilter([], ManyNames, ["origin"]))), Describe(rows));
    }

    [Fact]
    public async Task Past_the_command_lines_limit_hiding_skips_a_ref_deleted_after_the_refs_were_read()
    {
        // Hiding this way names every ref of the refs read; one deleted before git log runs is a
        // name that no longer exists, which --ignore-missing before --stdin skips (0.6).
        using var copy = await scenario.Repo.CopyAsync();
        await AddManyBranchesAsync(copy);
        var read = await ReadRefsAsync(copy);
        await copy.GitAsync("branch", "--quiet", "-D", "feature/local");
        var filter = new GraphFilter(ManyNames, [], ["origin"]);
        Assert.True(filter.UsesStandardInput);
        var calls = new GitCallLog();

        var repository = await GitRepository.OpenAsync(NewRunner(calls), copy.Path, TestCancelled);
        var pages = new List<IReadOnlyList<CommitInfo>>();
        await repository.LoadCommitsAsync(Task.FromResult(read), filter, pages.Add, TestCancelled);
        var rows = pages.SelectMany(page => page).ToList();

        Assert.EndsWith(" --ignore-missing --stdin --", Assert.Single(LogCalls(calls)), StringComparison.Ordinal);
        Assert.Contains("refs/heads/feature/local\n", filter.StandardInput(read), StringComparison.Ordinal);
        var current = await ReadRefsAsync(copy);
        Assert.Equal(
            await GitShowsAsync(copy, current.Refs.Select(gitRef => gitRef.FullName).Where(name => !name.StartsWith("refs/heads/many/", StringComparison.Ordinal))),
            Commits(rows));
        Assert.DoesNotContain(scenario.Sha["Local work"], Commits(rows));
    }

    [Fact]
    public async Task Solo_names_full_refs_so_the_branch_x_is_not_the_tag_x()
    {
        var soloed = await LoadAsync(scenario.Repo, new GraphFilter([], ["refs/heads/x"], ["origin"]));
        var hidden = await LoadAsync(scenario.Repo, new GraphFilter(["refs/heads/x"], [], ["origin"]));

        // The short name x is the tag to git (0.6): soloing it by that name would show X one alone.
        Assert.Equal(scenario.Sha["X two"], Commits(soloed)[0]);
        Assert.Contains(scenario.Sha["X one"], Commits(soloed));
        Assert.DoesNotContain(scenario.Sha["X two"], Commits(hidden));
        Assert.Contains(scenario.Sha["X one"], Commits(hidden));
        Assert.Equal(scenario.Sha["X one"], (await scenario.Repo.GitAsync("rev-parse", "x")).StandardOutput.Trim());
    }

    [Fact]
    public async Task A_branch_that_a_hidden_remote_gains_later_shows_until_the_filter_names_it()
    {
        // The session expands a hidden remote to the branches it knew (8.1, 8.2); a fetch then
        // brings feature/gamma, which shows until the next refs read adds it to the filter.
        using var copy = await scenario.Repo.CopyAsync();
        string[] known = ["refs/remotes/origin/feature/alpha", "refs/remotes/origin/feature/beta", "refs/remotes/origin/main"];
        var gamma = (await copy.GitAsync("commit-tree", scenario.Sha["Initial"] + "^{tree}", "-p", scenario.Sha["Initial"], "-m", "Gamma")).StandardOutput.Trim();
        await copy.GitAsync("push", "--quiet", "origin", gamma + ":refs/heads/feature/gamma");
        await copy.GitAsync("fetch", "--quiet", "origin");

        var stale = await LoadAsync(copy, new GraphFilter(known, [], ["origin"]));
        var current = await LoadAsync(copy, new GraphFilter([.. known, "refs/remotes/origin/feature/gamma"], [], ["origin"]));

        Assert.Contains(gamma, Commits(stale));
        Assert.DoesNotContain(gamma, Commits(current));
        Assert.Contains(scenario.Sha["Alpha two"], Commits(current));
    }

    [Fact]
    public async Task Past_the_command_lines_limit_hiding_names_every_other_ref_on_standard_input()
    {
        using var copy = await scenario.Repo.CopyAsync();
        var many = await AddManyBranchesAsync(copy);
        var refs = await ReadRefsAsync(copy);
        var filter = new GraphFilter(ManyNames, [], ["origin"]);
        Assert.True(filter.UsesStandardInput);
        var calls = new GitCallLog();

        var rows = await LoadAsync(copy, filter, calls);

        Assert.EndsWith(" --ignore-missing --stdin --", Assert.Single(LogCalls(calls)), StringComparison.Ordinal);
        Assert.DoesNotContain(many, Commits(rows));
        Assert.Equal(await GitShowsAsync(copy, refs.Refs.Select(gitRef => gitRef.FullName).Where(name => !name.StartsWith("refs/heads/many/", StringComparison.Ordinal))), Commits(rows));
        Assert.Equal(2, Stashes(rows).Count);
    }

    [Fact]
    public async Task Past_the_command_lines_limit_solo_names_the_soloed_refs_on_standard_input()
    {
        using var copy = await scenario.Repo.CopyAsync();
        var many = await AddManyBranchesAsync(copy);
        var filter = new GraphFilter([], ManyNames, ["origin"]);
        Assert.True(filter.UsesStandardInput);
        var calls = new GitCallLog();

        var rows = await LoadAsync(copy, filter, calls);

        // Every one of the branches points at Many: one of them shows what all of them do, and fits
        // on a command line.
        Assert.EndsWith(" --ignore-missing --stdin --", Assert.Single(LogCalls(calls)), StringComparison.Ordinal);
        Assert.Equal(await GitShowsAsync(copy, [ManyNames[0]]), Commits(rows));
        Assert.Contains(many, Commits(rows));
        Assert.DoesNotContain(scenario.Sha["Beta one"], Commits(rows));
        Assert.Empty(Stashes(rows));
    }

    [Fact]
    public async Task A_repository_without_commits_hands_over_nothing_with_a_filter()
    {
        using var repo = await TempRepo.CreateAsync();

        var rows = await LoadAsync(repo, new GraphFilter(["refs/heads/main"], [], []));

        Assert.Empty(rows);
    }

    // ---- Loading and comparing ------------------------------------------------------------------

    /// <summary>The full names of the 1,200 branches <see cref="AddManyBranchesAsync"/> makes: over 30,000 characters as excludes or as names.</summary>
    private static readonly string[] ManyNames =
        [.. Enumerable.Range(0, 1200).Select(i => string.Create(CultureInfo.InvariantCulture, $"refs/heads/many/branch-number-{i:D4}"))];

    /// <summary>
    /// Adds a commit "Many" on Initial that only the 1,200 branches of <see cref="ManyNames"/>
    /// reach. Returns its SHA. The branches are written straight into the packed refs, as
    /// <c>git pack-refs</c> would leave them: made one file each, they take tens of seconds on Windows.
    /// </summary>
    private async Task<string> AddManyBranchesAsync(TempRepo repo)
    {
        var many = (await repo.GitAsync("commit-tree", scenario.Sha["Initial"] + "^{tree}", "-p", scenario.Sha["Initial"], "-m", "Many")).StandardOutput.Trim();
        var packedRefs = Path.Combine(repo.Path, (await repo.GitAsync("rev-parse", "--git-path", "packed-refs")).StandardOutput.Trim());
        Assert.False(File.Exists(packedRefs), "The scenario has packed refs; the branches would replace them.");
        await File.WriteAllTextAsync(packedRefs, "# pack-refs with: peeled fully-peeled sorted \n" + string.Concat(ManyNames.Select(name => $"{many} {name}\n")), TestCancelled);
        Assert.Equal(many, (await repo.GitAsync("rev-parse", ManyNames[^1])).StandardOutput.Trim());
        return many;
    }

    private static async Task<RepoRefs> ReadRefsAsync(TempRepo repo) => await (await OpenAsync(repo)).ReadRefsAsync(TestCancelled);

    /// <summary>Loads the graph as the view model does, the refs read side by side with git's walk.</summary>
    private static async Task<List<CommitInfo>> LoadAsync(TempRepo repo, GraphFilter? filter, IGitCallLog? calls = null)
    {
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var pages = new List<IReadOnlyList<CommitInfo>>();
        await repository.LoadCommitsAsync(repository.ReadRefsAsync(TestCancelled), filter, pages.Add, TestCancelled);
        return [.. pages.SelectMany(page => page)];
    }

    /// <summary>Loads the graph through the overload without a filter, as every phase before 5 did.</summary>
    private static async Task<List<CommitInfo>> LoadTodayAsync(TempRepo repo, IGitCallLog? calls = null)
    {
        var repository = await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled);
        var pages = new List<IReadOnlyList<CommitInfo>>();
        await repository.LoadCommitsAsync(repository.ReadRefsAsync(TestCancelled), pages.Add, TestCancelled);
        return [.. pages.SelectMany(page => page)];
    }

    /// <summary>
    /// The commits git shows, in its order, for <paramref name="refs"/> named one by one and HEAD:
    /// what a filter that leaves exactly those refs must load.
    /// </summary>
    private static async Task<List<string>> GitShowsAsync(TempRepo repo, IEnumerable<string> refs)
    {
        var output = (await repo.GitAsync(["log", "--date-order", "--format=%H", "--ignore-missing", .. refs, "HEAD", "--"])).StandardOutput;
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>The commands of the graph's <c>git log</c> calls.</summary>
    private static List<string> LogCalls(GitCallLog calls) =>
        [.. calls.Snapshot().Select(call => call.CommandText).Where(command => command.StartsWith("git log --date-order", StringComparison.Ordinal))];

    private static List<string> Commits(IEnumerable<CommitInfo> rows) =>
        [.. rows.Where(row => row.Kind == CommitKind.Commit).Select(row => row.Sha)];

    private static List<string> Stashes(IEnumerable<CommitInfo> rows) =>
        [.. rows.Where(row => row.Kind == CommitKind.Stash).Select(row => row.Sha)];

    private static List<string> Describe(IEnumerable<CommitInfo> rows) =>
        [.. rows.Select(row => $"{row.Kind} {row.Sha} {string.Join(' ', row.Parents)}")];

    /// <summary>Every stash whose base is a row is a row too, above its base; every other stash is left out.</summary>
    private static void AssertStashesWhereTheirBaseIs(List<CommitInfo> rows, RepoRefs refs)
    {
        var index = rows.Select((row, i) => (row.Sha, i)).ToDictionary(pair => pair.Sha, pair => pair.i, StringComparer.Ordinal);
        foreach (var stash in refs.Stashes)
        {
            if (index.TryGetValue(stash.BaseSha, out var baseRow))
            {
                Assert.True(index.TryGetValue(stash.Sha, out var stashRow) && stashRow < baseRow, $"{stash.Message} is not above its base.");
            }
            else
            {
                Assert.False(index.ContainsKey(stash.Sha), $"{stash.Message} is shown without its base.");
            }
        }
    }
}
