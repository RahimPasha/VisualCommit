using System.Globalization;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Keep ignored files' reads (D221; docs/phase-6-design.md 9.3 to 9.8, 9.17), run by the app's
/// <see cref="GitRepository"/> against real git on the shapes of 0.4 (each derived with Git 2.36 and
/// 2.55, which overwrite or delete the file named in every "found" case unless a case says
/// otherwise): the ignored files in the way of a merge, a cherry-pick or revert (one or a list), a
/// rebase onto a commit, an interactive rebase or squash, and a stash's apply. Also what each read
/// leaves out, that no read changes the index or the state, and each read's usual count of git calls.
/// </summary>
public class IgnoredInTheWayTests
{
    private const string Local = "LOCAL\n";

    /// <summary>The repository the tests start from, built once and copied per test.</summary>
    private static readonly Lazy<Task<Shapes>> Template = new(BuildAsync);

    /// <summary>
    /// 0.4's S1O on <c>main</c>: <c>base</c> (README), "Add secret" (<c>secret.env</c> "OLD"),
    /// "Edit secret" ("old2"), "Ignore secret" (<c>git rm --cached</c>, a <c>.gitignore</c> of
    /// <c>secret.env</c>, <c>build/</c>, <c>logs</c> and <c>*.ignored</c>), "X" (<c>x.txt</c>, and
    /// <c>keep.ignored</c> force-added: a tracked file a rule matches). From <c>base</c>: <c>t</c>
    /// adds <c>secret.env</c> (S1X), <c>t-build</c> a file <c>build</c> (S2), <c>t-logs</c>
    /// <c>logs/a.txt</c> (S3), <c>t-name</c> <c>my file é.ignored</c>, <c>side</c> adds
    /// <c>secret.env</c> and then deletes it. From "Edit secret": <c>feature</c> edits README only,
    /// <c>feature2</c> edits <c>secret.env</c> (S1D: m15 and m16), <c>m1</c> adds <c>y.txt</c> and
    /// merges "Ignore secret" (a merge that stops tracking it). <c>u-main</c> is U5: from "Add
    /// secret" it adds <c>f.txt</c> and merges "Ignore secret", ahead of "Edit secret" only.
    /// <c>t-tracked</c> changes <c>keep.ignored</c> after X; <c>lone</c> is a root commit of its
    /// own with <c>secret.env</c>. Nothing ignored is on disk: each test writes what it needs.
    /// </summary>
    private static async Task<Shapes> BuildAsync()
    {
        var repo = await TempRepo.CreateAsync("ignored");
        var root = await repo.CommitFileAsync("README", "base\n", "base");
        var add = await repo.CommitFileAsync("secret.env", "OLD\n", "Add secret");
        var edit = await repo.CommitFileAsync("secret.env", "old2\n", "Edit secret");

        async Task<string> BranchAsync(string name, string from, params (string Path, string Content)[] files)
        {
            await repo.GitAsync("switch", "--quiet", "--create", name, from);
            foreach (var (path, content) in files)
            {
                repo.WriteFile(path, content);
            }

            return await repo.CommitAsync("Add to " + name);
        }

        var t = await BranchAsync("t", root, ("secret.env", "T\n"));
        await BranchAsync("t-build", root, ("build", "F\n"));
        await BranchAsync("t-logs", root, ("logs/a.txt", "A\n"));
        await BranchAsync("t-name", root, ("my file é.ignored", "N\n"));
        var sideAdds = await BranchAsync("side", root, ("secret.env", "S\n"));
        await repo.GitAsync("rm", "--quiet", "secret.env");
        var sideDeletes = await repo.CommitAsync("Side deletes secret");
        await BranchAsync("feature", edit, ("README", "base\nfeature\n"));
        await BranchAsync("feature2", edit, ("secret.env", "F2\n"));

        await repo.GitAsync("switch", "--quiet", "main");
        await repo.GitAsync("rm", "--quiet", "--cached", "secret.env");
        repo.DeleteFile("secret.env").WriteFile(".gitignore", "secret.env\nbuild/\nlogs\n*.ignored\n");
        var ignore = await repo.CommitAsync("Ignore secret");
        repo.WriteFile("x.txt", "x\n").WriteFile("keep.ignored", "tracked\n");
        await repo.GitAsync("add", "--force", "keep.ignored");
        var x = await repo.CommitAsync("X");
        var tracked = await BranchAsync("t-tracked", x, ("keep.ignored", "changed\n"));

        await BranchAsync("m1", edit, ("y.txt", "y\n"));
        var merge = await repo.MergeAsync(ignore, "Merge Ignore secret");
        var mergeParent = (await repo.GitAsync("rev-parse", merge + "^1")).StandardOutput.Trim();
        await BranchAsync("u-main", add, ("f.txt", "f\n"));
        await repo.MergeAsync(ignore, "Merge Ignore secret into the feature");

        await repo.GitAsync("switch", "--quiet", "--orphan", "lone");
        var lone = await repo.CommitFileAsync("secret.env", "L\n", "Lone");
        await repo.GitAsync("switch", "--quiet", "main");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => repo.Dispose();
        return new Shapes(repo, root, add, edit, ignore, x, t, sideAdds, sideDeletes, tracked, merge, mergeParent, lone);
    }

    /// <summary>The template and its commits.</summary>
    private sealed record Shapes(
        TempRepo Repo,
        string Base,
        string Add,
        string Edit,
        string Ignore,
        string X,
        string T,
        string SideAdds,
        string SideDeletes,
        string Tracked,
        string Merge,
        string MergeParent,
        string Lone);

    /// <summary>A copy of the template on <c>main</c> (X), with the template's commits.</summary>
    private static async Task<(TempRepo Repo, Shapes Commits)> CopyAsync()
    {
        var shapes = await HangWatchdogAttribute.SharedPreparationAsync("the ignored-files template", Template.Value);
        return (await shapes.Repo.CopyAsync(), shapes);
    }

    private static async Task<(GitRepository Repository, GitCallLog Calls)> OpenWithCallsAsync(TempRepo repo)
    {
        var calls = new GitCallLog();
        return (await GitRepository.OpenAsync(NewRunner(calls), repo.Path, TestCancelled), calls);
    }

    /// <summary>The calls <paramref name="read"/> made, in order.</summary>
    private static async Task<(T Result, IReadOnlyList<GitCallRecord> Calls)> CountAsync<T>(GitCallLog calls, Func<Task<T>> read)
    {
        var mark = calls.Snapshot().Count;
        var result = await read();
        return (result, [.. calls.Snapshot().Skip(mark)]);
    }

    // ---- 9.4: merges ------------------------------------------------------------------------------

    [Fact]
    public async Task A_merge_finds_the_ignored_file_the_source_adds_also_in_a_fast_forward_and_from_unrelated_histories()
    {
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        repo.WriteFile("secret.env", Local);
        var repository = await OpenAsync(repo);

        // S1X's real merge, a fast-forward (into is the base: "Check out and merge"'s refs/heads/<A>
        // or a commit), and unrelated histories (no base, which git refuses anyway).
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", "refs/heads/t", TestCancelled));
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfMergeAsync(c.Base, "refs/heads/t", TestCancelled));
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", "refs/heads/lone", TestCancelled));
    }

    [Fact]
    public async Task A_merge_keeps_a_deletion_into_made_and_finds_a_modify_delete_conflicts_side()
    {
        // S1D: feature (README only) leaves the deletion of secret.env to the merge, which keeps
        // it (m15); feature2 edits secret.env, a modify/delete conflict whose side git writes (m16).
        var (repo, _) = await CopyAsync();
        using var __ = repo;
        repo.WriteFile("secret.env", Local);
        var (repository, calls) = await OpenWithCallsAsync(repo);

        var (m15, m15Calls) = await CountAsync(calls, () => repository.FindIgnoredInTheWayOfMergeAsync("HEAD", "refs/heads/feature", TestCancelled));
        Assert.Empty(m15);
        Assert.Equal(
            ["diff-tree --diff-filter=A", "check-ignore", "merge-base --all", "diff-tree --diff-filter=AMT"],
            m15Calls.Select(call => Kind(call.CommandText)));

        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", "refs/heads/feature2", TestCancelled));
    }

    [Fact]
    public async Task A_merge_of_an_ancestor_finds_nothing()
    {
        // U4: the base is Add secret, which the source is; git says "Already up to date." and keeps it.
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        repo.WriteFile("secret.env", Local);
        var repository = await OpenAsync(repo);

        Assert.Empty(await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", c.Add, TestCancelled));
    }

    [Fact]
    public async Task A_merge_finds_an_ignored_folder_where_the_source_has_a_file_and_an_ignored_file_where_it_has_a_folder()
    {
        // S2 by the files under the folder, S3 by the parent that is a file.
        var (repo, _) = await CopyAsync();
        using var __ = repo;
        repo.WriteFile("build/out.bin", "OUT\n").WriteFile("build/sub/deep.bin", "DEEP\n").WriteFile("logs", "LOGS\n");
        var repository = await OpenAsync(repo);

        Assert.Equal(["build/out.bin", "build/sub/deep.bin"], await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", "refs/heads/t-build", TestCancelled));
        Assert.Equal(["logs"], await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", "refs/heads/t-logs", TestCancelled));
    }

    [Fact]
    public async Task A_merge_names_a_path_with_a_space_and_an_accent_as_it_is_and_reads_past_a_file_named_HEAD()
    {
        var (repo, _) = await CopyAsync();
        using var __ = repo;
        repo.WriteFile("my file é.ignored", Local).WriteFile("secret.env", Local);

        // Files named like the revisions: without "--" diff-tree would fail as ambiguous (0.4).
        repo.WriteFile("HEAD", "h\n").WriteFile("main", "m\n");
        var repository = await OpenAsync(repo);

        Assert.Equal(["my file é.ignored"], await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", "refs/heads/t-name", TestCancelled));
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", "refs/heads/t", TestCancelled));
    }

    [Fact]
    public async Task A_criss_cross_merge_keeps_a_file_that_any_of_its_bases_lists()
    {
        // b1 adds secret.env, a1 changes README; each side merges the other (two bases, a1 and
        // b1); main then stops tracking and ignores it. B2 (the other side's merge) has it from
        // b1: a1's list names it, b1's does not, and the read keeps it, the safe side (git's
        // virtual base has it, so git keeps the deletion here). B3 then edits it: both bases list
        // it, and git's modify/delete conflict writes B3's side over the file.
        using var repo = await TempRepo.CreateAsync("criss");
        await repo.CommitFileAsync("README", "base\n", "O");
        await repo.CreateBranchAsync("b");
        await repo.CheckoutAsync("b");
        var b1 = await repo.CommitFileAsync("secret.env", "OLD\n", "b1");
        await repo.CheckoutAsync("main");
        var a1 = await repo.CommitFileAsync("README", "base\na1\n", "a1");
        await repo.MergeAsync(b1, "a2");
        await repo.CheckoutAsync("b");
        var b2 = await repo.MergeAsync(a1, "b2");
        var b3 = await repo.CommitFileAsync("secret.env", "B3\n", "b3");
        await repo.CheckoutAsync("main");
        await repo.GitAsync("rm", "--quiet", "--cached", "secret.env");
        repo.WriteFile(".gitignore", "secret.env\n");
        await repo.CommitAsync("Stop tracking secret");
        repo.WriteFile("secret.env", Local);
        var repository = await OpenAsync(repo);
        Assert.Equal(2, (await repo.GitAsync("merge-base", "--all", "HEAD", b2)).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", b2, TestCancelled));
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", b3, TestCancelled));
    }

    // ---- 9.5: cherry-pick and revert ----------------------------------------------------------------

    [Fact]
    public async Task A_pick_or_a_revert_finds_what_it_adds_or_the_side_of_its_modify_delete_conflict()
    {
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        repo.WriteFile("secret.env", Local);
        var repository = await OpenAsync(repo);

        // S1X's pick; T9, a pick of Edit secret onto X (modify/delete, git writes its side); T4,
        // its revert (the parent's side); the revert of Ignore secret, which adds it back.
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfCommitsAsync([c.T], revert: false, cancellationToken: TestCancelled));
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfCommitsAsync([c.Edit], revert: false, cancellationToken: TestCancelled));
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfCommitsAsync([c.Edit], revert: true, cancellationToken: TestCancelled));
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfCommitsAsync([c.Ignore], revert: true, cancellationToken: TestCancelled));

        // Picking Ignore secret writes .gitignore, a tracked file: never ignored.
        Assert.Empty(await repository.FindIgnoredInTheWayOfCommitsAsync([c.Ignore], revert: false, cancellationToken: TestCancelled));
    }

    [Fact]
    public async Task A_list_of_picks_finds_a_file_its_first_pick_adds_and_its_second_deletes()
    {
        // git writes it over the ignored file, then deletes it: gone either way.
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        repo.WriteFile("secret.env", Local);
        var repository = await OpenAsync(repo);

        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfCommitsAsync([c.SideAdds, c.SideDeletes], revert: false, cancellationToken: TestCancelled));
    }

    [Fact]
    public async Task A_merge_reverted_with_its_first_parent_and_a_root_commit_picked_are_read_against_the_right_parent()
    {
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        repo.WriteFile("secret.env", Local);
        var repository = await OpenAsync(repo);

        // The merge stopped tracking secret.env; git revert -m 1 writes it back. Without its
        // parent the merge's line prints nothing (0.4).
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfCommitsAsync([c.Merge], revert: true, mergeParent: c.MergeParent, TestCancelled));
        Assert.Empty(await repository.FindIgnoredInTheWayOfCommitsAsync([c.Merge], revert: true, cancellationToken: TestCancelled));

        // A root commit's change is read against the empty tree (--root).
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfCommitsAsync([c.Lone], revert: false, cancellationToken: TestCancelled));
    }

    [Fact]
    public async Task A_pick_of_a_tracked_file_a_rule_matches_finds_nothing_and_names_are_refused()
    {
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        var repository = await OpenAsync(repo);

        Assert.Empty(await repository.FindIgnoredInTheWayOfCommitsAsync([c.Tracked], revert: false, cancellationToken: TestCancelled));

        // diff-tree --stdin resolves no name: it would print the name back and find nothing.
        await Assert.ThrowsAsync<ArgumentException>(() => repository.FindIgnoredInTheWayOfCommitsAsync(["HEAD"], revert: false, cancellationToken: TestCancelled));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.FindIgnoredInTheWayOfCommitsAsync([c.Merge], revert: true, mergeParent: "HEAD^1", TestCancelled));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.FindIgnoredInTheWayOfCommitsAsync([c.Merge, c.X], revert: true, mergeParent: c.MergeParent, TestCancelled));
    }

    // ---- 9.6: rebase onto ---------------------------------------------------------------------------

    [Fact]
    public async Task A_rebase_finds_what_the_checkout_of_onto_writes_and_what_a_replayed_commit_adds()
    {
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        repo.WriteFile("build/out.bin", "OUT\n").WriteFile("logs", "LOGS\n");
        var repository = await OpenAsync(repo);

        // S2 and S3: git's first checkout, of onto, writes them.
        Assert.Equal(["build/out.bin"], await repository.FindIgnoredInTheWayOfRebaseAsync("HEAD", (await repo.RefTargetAsync("refs/heads/t-build"))!, TestCancelled));
        Assert.Equal(["logs"], await repository.FindIgnoredInTheWayOfRebaseAsync("HEAD", (await repo.RefTargetAsync("refs/heads/t-logs"))!, TestCancelled));

        // S1X likewise; and replaying main onto t-build replays Add secret, which writes it too.
        repo.WriteFile("secret.env", Local);
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfRebaseAsync("HEAD", c.T, TestCancelled));
        Assert.Equal(["build/out.bin", "secret.env"], await repository.FindIgnoredInTheWayOfRebaseAsync("HEAD", (await repo.RefTargetAsync("refs/heads/t-build"))!, TestCancelled));

        // "Check out and rebase" of side onto X: side's first commit adds secret.env and its
        // second deletes it; git writes it over the ignored file and then deletes it (0.4).
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfRebaseAsync("refs/heads/side", c.X, TestCancelled));
    }

    [Fact]
    public async Task A_branch_only_ahead_of_onto_finds_nothing_when_linear_and_the_deleted_file_when_it_holds_a_merge()
    {
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        repo.WriteFile("secret.env", Local);
        var (repository, calls) = await OpenWithCallsAsync(repo);

        // U2: main is linear and only ahead of Add secret: "Current branch main is up to date."
        // and nothing written, so no check-ignore either.
        var (linear, linearCalls) = await CountAsync(calls, () => repository.FindIgnoredInTheWayOfRebaseAsync("HEAD", c.Add, TestCancelled));
        Assert.Empty(linear);
        Assert.Equal(4, linearCalls.Count);
        Assert.DoesNotContain(linearCalls, call => call.CommandText.StartsWith("git check-ignore", StringComparison.Ordinal));

        // U5: u-main is only ahead of Edit secret but holds a merge; git checks out Edit secret
        // and replays it, plain or with --rebase-merges, and secret.env is gone (0.4). The read is
        // the same for both: it does not depend on the rebase's options.
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfRebaseAsync("refs/heads/u-main", c.Edit, TestCancelled));
    }

    [Fact]
    public void The_replayed_commits_paths_are_read_from_logs_layout()
    {
        // git log --name-only -z --format=%x1e%H as Git 2.36 and 2.55 write it: a line feed before
        // a commit's first path only; a commit the filter leaves no path (a deletion) not at all.
        var output = "\x1e" + new string('a', 40) + "\0\ne f.txt\0\x1e" + new string('b', 40) + "\0\na.txt\0b.txt\0d/c.txt\0";

        Assert.Equal(["e f.txt", "a.txt", "b.txt", "d/c.txt"], GitRepository.LoggedPaths(output));
        Assert.Empty(GitRepository.LoggedPaths(string.Empty));
    }

    // ---- 9.7: interactive rebase and squash -------------------------------------------------------

    [Fact]
    public async Task An_interactive_rebase_starts_where_git_does_after_skipping_its_leading_picks()
    {
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        repo.WriteFile("secret.env", Local);
        var repository = await OpenAsync(repo);
        RebasePlan Plan(params RebaseStep[] steps) => new(c.Add, c.X, "refs/heads/main", steps);

        // T5: the leading picks of Edit and Ignore secret are skipped, git starts at Ignore secret
        // and rewords X: nothing in the way, which a start at Add secret would have refused.
        Assert.Empty(await repository.FindIgnoredInTheWayOfRebaseAsync(
            Plan(new(c.Edit, RebaseAction.Pick, null), new(c.Ignore, RebaseAction.Pick, null), new(c.X, RebaseAction.Reword, "X again\n")), TestCancelled));

        // T6: a reword first; its label stops the skipping: git checks out Add secret (writing
        // secret.env) and Ignore secret's pick deletes it.
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfRebaseAsync(
            Plan(new(c.Edit, RebaseAction.Reword, "Edit again\n"), new(c.Ignore, RebaseAction.Pick, null), new(c.X, RebaseAction.Pick, null)), TestCancelled));

        // T7: reordered, X first, whose parent is not the start: nothing is skipped.
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfRebaseAsync(
            Plan(new(c.X, RebaseAction.Pick, null), new(c.Edit, RebaseAction.Pick, null), new(c.Ignore, RebaseAction.Pick, null)), TestCancelled));

        // Ignore secret dropped: git skips Edit secret, checks it out (writing secret.env) and picks X.
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfRebaseAsync(
            Plan(new(c.Edit, RebaseAction.Pick, null), new(c.Ignore, RebaseAction.Drop, null), new(c.X, RebaseAction.Pick, null)), TestCancelled));
    }

    [Fact]
    public async Task A_squash_whose_base_tracks_the_file_finds_it()
    {
        // Squash Edit and Ignore secret: the first group changes its message, so git checks out
        // the base, Add secret, which tracks secret.env.
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        repo.WriteFile("secret.env", Local);
        var repository = await OpenAsync(repo);
        var list = await repository.ReadRebaseListAsync(c.Add, "refs/heads/main", fromCommit: true, cancellationToken: TestCancelled);
        var plan = RebaseTodo.SquashPlan(list, "refs/heads/main", [c.Edit, c.Ignore], "Edit and ignore secret\n");

        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfRebaseAsync(plan, TestCancelled));
    }

    [Fact]
    public async Task A_list_of_a_thousand_commits_whose_newest_is_reworded_passes_its_leading_picks_on_standard_input()
    {
        // 999 leading picks, then a reword of "Ignore secret" (S1O's): git checks out the 999th,
        // which tracks secret.env, and the reworded pick deletes it. As arguments the SHAs would
        // be some 41,000 characters, which Windows does not start (0.4); the call log's rev-list
        // line holds no SHA, so an arguments form would fail here on every platform.
        using var repo = await TempRepo.CreateAsync("thousand");
        var shas = await ImportThousandAsync(repo);
        await repo.GitAsync("reset", "--quiet", "--hard", "refs/heads/main");
        repo.WriteFile("secret.env", Local);
        var (repository, calls) = await OpenWithCallsAsync(repo);
        var onto = shas[1];
        var picks = shas.Skip(2).Take(999).Select(sha => new RebaseStep(sha, RebaseAction.Pick, null));
        var plan = new RebasePlan(onto, shas[^1], "refs/heads/main", [.. picks, new RebaseStep(shas[^1], RebaseAction.Reword, "Ignore secret, again\n")]);

        var (found, made) = await CountAsync(calls, () => repository.FindIgnoredInTheWayOfRebaseAsync(plan, TestCancelled));

        Assert.Equal(["secret.env"], found);
        var revList = Assert.Single(made, call => call.CommandText.StartsWith("git rev-list", StringComparison.Ordinal));
        Assert.Equal("git rev-list --stdin --no-walk=unsorted --parents", revList.CommandText);
        Assert.Equal(4, made.Count);
    }

    /// <summary>
    /// <c>base</c> (README), "Add secret", 999 commits that each change <c>count.txt</c>, then
    /// "Ignore secret" (deletes <c>secret.env</c>, adds a <c>.gitignore</c> of it), on <c>main</c>
    /// with one <c>git fast-import</c>. Returns the 1,002 SHAs, oldest first.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ImportThousandAsync(TempRepo repo)
    {
        var stream = new StringBuilder();
        var time = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        void Commit(string message, params string[] changes)
        {
            stream.Append("commit refs/heads/main\ncommitter ").Append(TempRepo.AuthorName).Append(" <").Append(TempRepo.AuthorEmail).Append("> ")
                .Append((time += 60).ToString(CultureInfo.InvariantCulture)).Append(" +0000\n");
            Data(message);
            foreach (var change in changes)
            {
                stream.Append(change);
            }

            stream.Append('\n');
        }

        void Data(string text) => stream.Append("data ").Append(Encoding.UTF8.GetByteCount(text)).Append('\n').Append(text).Append('\n');
        static string Modify(string path, string content) => $"M 100644 inline {path}\ndata {Encoding.UTF8.GetByteCount(content)}\n{content}\n";

        Commit("base", Modify("README", "base\n"));
        Commit("Add secret", Modify("secret.env", "OLD\n"));
        for (var i = 1; i <= 999; i++)
        {
            Commit($"Count {i}", Modify("count.txt", $"{i}\n"));
        }

        Commit("Ignore secret", "D secret.env\n", Modify(".gitignore", "secret.env\n"));
        await repo.GitWithInputAsync(stream.ToString(), "fast-import", "--quiet");
        var shas = (await repo.GitAsync("rev-list", "--reverse", "refs/heads/main")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(1002, shas.Length);
        return shas;
    }

    // ---- 9.8: stash apply and pop -------------------------------------------------------------------

    [Fact]
    public async Task A_stash_finds_its_staged_new_file_and_its_side_of_a_modify_delete_but_not_a_deletion()
    {
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        var repository = await OpenAsync(repo);

        // ST1: a staged new file (git apply overwrites the ignored one on both gits).
        repo.WriteFile("secret.env", "STASHED\n");
        await repo.GitAsync("add", "--force", "secret.env");
        var staged = await repo.StashAsync("st1");
        repo.WriteFile("secret.env", Local);
        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfStashAsync(staged, TestCancelled));
        repo.DeleteFile("secret.env");

        // ST3: a change of secret.env made on Edit secret, applied on X, which stopped tracking it
        // (Git 2.55 overwrites the ignored file; 2.36 leaves "secret.env~Stashed changes").
        await repo.DetachAsync(c.Edit);
        repo.WriteFile("secret.env", "MINE\n");
        var modified = await repo.StashAsync("st3");

        // A stash that only deletes it writes nothing there.
        repo.DeleteFile("secret.env");
        var deleting = await repo.StashAsync("delete");
        await repo.CheckoutAsync("main");
        repo.WriteFile("secret.env", Local);

        Assert.Equal(["secret.env"], await repository.FindIgnoredInTheWayOfStashAsync(modified, TestCancelled));
        Assert.Empty(await repository.FindIgnoredInTheWayOfStashAsync(deleting, TestCancelled));
        Assert.Equal(["secret.env"], (await repository.ReadStashPathsAsync(deleting, TestCancelled)).Deleted);
    }

    [Fact]
    public async Task A_stashs_untracked_file_under_a_folder_that_is_now_an_ignored_file_finds_that_file()
    {
        // The untracked part's logs/a.txt, where X has an ignored file logs: D179's count looks
        // only for a file at logs/a.txt.
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        var repository = await OpenAsync(repo);
        await repo.DetachAsync(c.Base);
        repo.WriteFile("logs/a.txt", "A\n");

        // The app's stash, which removes the untracked files it saves (D108; the tests' own git
        // calls keep GIT_LITERAL_PATHSPECS, with which git stash leaves them on disk).
        var stash = await repository.StashAsync("st2", includeUntracked: true, cancellationToken: TestCancelled);
        Assert.NotNull(stash);
        await repo.CheckoutAsync("main");

        // The stash may leave the emptied folder behind; the file takes its place.
        if (Directory.Exists(Path.Combine(repo.Path, "logs")))
        {
            Directory.Delete(Path.Combine(repo.Path, "logs"));
        }

        repo.WriteFile("logs", "LOGS\n");

        Assert.Equal(["logs"], await repository.FindIgnoredInTheWayOfStashAsync(stash!, TestCancelled));
    }

    // ---- Every read --------------------------------------------------------------------------------

    [Fact]
    public async Task The_shared_helper_pairs_each_written_path_with_the_ignored_files_in_its_way_and_asks_nothing_under_a_missing_folder()
    {
        // The ignored file logs above logs/a.txt and logs/b/c.txt and at logs itself; the ignored
        // folder build/ where build is written; secret.env at its own path; x.txt, tracked, which
        // check-ignore leaves out; nothing under gone/, which does not exist.
        var (repo, _) = await CopyAsync();
        using var __ = repo;
        repo.WriteFile("logs", "LOGS\n").WriteFile("build/out.bin", "OUT\n").WriteFile("build/sub/deep.bin", "DEEP\n").WriteFile("secret.env", Local);
        var (repository, calls) = await OpenWithCallsAsync(repo);

        var (pairs, made) = await CountAsync(calls, () => repository.FindIgnoredAtAsync(
            ["logs/b/c.txt", "gone/deeper/x.txt", "build", "logs/a.txt", "secret.env", "gone/y.txt", "x.txt", "logs", "logs/a.txt"],
            TestCancelled));

        Assert.Equal(
            [
                ("build", "build/out.bin"),
                ("build", "build/sub/deep.bin"),
                ("logs", "logs"),
                ("logs/a.txt", "logs"),
                ("logs/b/c.txt", "logs"),
                ("secret.env", "secret.env"),
            ],
            pairs);
        Assert.Equal(["check-ignore"], made.Select(call => Kind(call.CommandText)));

        // Only paths under folders that do not exist: no candidate, so no process.
        var (none, noCalls) = await CountAsync(calls, () => repository.FindIgnoredAtAsync(["gone/a.txt", "gone/deeper/b.txt", "none/c.txt"], TestCancelled));
        Assert.Empty(none);
        Assert.Empty(noCalls);
    }

    [Fact]
    public async Task No_read_changes_the_index_or_the_state()
    {
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        repo.WriteFile("secret.env", "STASHED\n");
        await repo.GitAsync("add", "--force", "secret.env");
        var stash = await repo.StashAsync("st1");
        repo.WriteFile("secret.env", Local).WriteFile("logs", "LOGS\n");
        var repository = await OpenAsync(repo);

        // The status read of the state refreshes the index first; then nothing may write it.
        var before = await repo.ExactStateAsync();
        var index = await File.ReadAllBytesAsync(Path.Combine(repo.Path, ".git", "index"), TestCancelled);
        var indexTime = File.GetLastWriteTimeUtc(Path.Combine(repo.Path, ".git", "index"));

        Assert.NotEmpty(await repository.FindIgnoredInTheWayOfMergeAsync("HEAD", "refs/heads/t", TestCancelled));
        Assert.NotEmpty(await repository.FindIgnoredInTheWayOfCommitsAsync([c.Edit, c.Ignore], revert: true, cancellationToken: TestCancelled));
        Assert.NotEmpty(await repository.FindIgnoredInTheWayOfRebaseAsync("HEAD", (await repo.RefTargetAsync("refs/heads/t-logs"))!, TestCancelled));
        Assert.NotEmpty(await repository.FindIgnoredInTheWayOfRebaseAsync(
            new RebasePlan(c.Add, c.X, "refs/heads/main", [new(c.X, RebaseAction.Pick, null), new(c.Edit, RebaseAction.Pick, null), new(c.Ignore, RebaseAction.Pick, null)]),
            TestCancelled));
        Assert.NotEmpty(await repository.FindIgnoredInTheWayOfStashAsync(stash, TestCancelled));

        Assert.Equal(index, await File.ReadAllBytesAsync(Path.Combine(repo.Path, ".git", "index"), TestCancelled));
        Assert.Equal(indexTime, File.GetLastWriteTimeUtc(Path.Combine(repo.Path, ".git", "index")));
        Assert.Equal(before, await repo.ExactStateAsync());
    }

    [Fact]
    public async Task Each_read_makes_its_usual_number_of_git_calls()
    {
        var (repo, c) = await CopyAsync();
        using var _ = repo;
        var (repository, calls) = await OpenWithCallsAsync(repo);

        // A merge whose added path is not on disk: its first read alone.
        var (merge, mergeCalls) = await CountAsync(calls, () => repository.FindIgnoredInTheWayOfMergeAsync("HEAD", "refs/heads/feature", TestCancelled));
        Assert.Empty(merge);
        Assert.Equal(["diff-tree --diff-filter=A"], mergeCalls.Select(call => Kind(call.CommandText)));

        repo.WriteFile("secret.env", Local);

        // A pick, a revert and a list: diff-tree --stdin and check-ignore.
        Assert.Equal(["diff-tree --stdin", "check-ignore"], (await CountAsync(calls, () => repository.FindIgnoredInTheWayOfCommitsAsync([c.Edit], revert: false, cancellationToken: TestCancelled))).Calls.Select(call => Kind(call.CommandText)));
        Assert.Equal(["diff-tree --stdin", "check-ignore"], (await CountAsync(calls, () => repository.FindIgnoredInTheWayOfCommitsAsync([c.Edit], revert: true, cancellationToken: TestCancelled))).Calls.Select(call => Kind(call.CommandText)));
        Assert.Equal(["diff-tree --stdin", "check-ignore"], (await CountAsync(calls, () => repository.FindIgnoredInTheWayOfCommitsAsync([c.SideAdds, c.SideDeletes], revert: false, cancellationToken: TestCancelled))).Calls.Select(call => Kind(call.CommandText)));

        // A rebase: four reads side by side, then check-ignore.
        var rebase = (await CountAsync(calls, () => repository.FindIgnoredInTheWayOfRebaseAsync("HEAD", c.T, TestCancelled))).Calls;
        Assert.Equal(["diff-tree --diff-filter=A", "log", "merge-base --is-ancestor", "rev-list"], rebase.Take(4).Select(call => Kind(call.CommandText)).Order(StringComparer.Ordinal));
        Assert.Equal("check-ignore", Kind(rebase[4].CommandText));
        Assert.Equal(5, rebase.Count);

        // An interactive rebase: three reads without leading picks, four with them; a squash three.
        RebasePlan Plan(params RebaseStep[] steps) => new(c.Add, c.X, "refs/heads/main", steps);
        Assert.Equal(3, (await CountAsync(calls, () => repository.FindIgnoredInTheWayOfRebaseAsync(
            Plan(new(c.Edit, RebaseAction.Reword, "Edit again\n"), new(c.Ignore, RebaseAction.Pick, null), new(c.X, RebaseAction.Pick, null)), TestCancelled))).Calls.Count);
        Assert.Equal(4, (await CountAsync(calls, () => repository.FindIgnoredInTheWayOfRebaseAsync(
            Plan(new(c.X, RebaseAction.Pick, null), new(c.Edit, RebaseAction.Pick, null), new(c.Ignore, RebaseAction.Pick, null)), TestCancelled))).Calls.Count);
        var list = await repository.ReadRebaseListAsync(c.Add, "refs/heads/main", fromCommit: true, cancellationToken: TestCancelled);
        Assert.Equal(3, (await CountAsync(calls, () => repository.FindIgnoredInTheWayOfRebaseAsync(RebaseTodo.SquashPlan(list, "refs/heads/main", [c.Edit, c.Ignore], "Squashed\n"), TestCancelled))).Calls.Count);

        // A stash after D179's count, which read its paths: check-ignore alone.
        repo.DeleteFile("secret.env").WriteFile("secret.env", "STASHED\n");
        await repo.GitAsync("add", "--force", "secret.env");
        var stash = await repo.StashAsync("st1");
        repo.WriteFile("secret.env", Local);
        await repository.ReadStashPathsAsync(stash, TestCancelled);
        Assert.Equal(["check-ignore"], (await CountAsync(calls, () => repository.FindIgnoredInTheWayOfStashAsync(stash, TestCancelled))).Calls.Select(call => Kind(call.CommandText)));
    }

    /// <summary>What a recorded read is, by its command's first words: <c>diff-tree --stdin</c>, <c>diff-tree --diff-filter=A</c>, <c>merge-base --all</c>, …</summary>
    private static string Kind(string commandText)
    {
        var words = commandText.Split(' ');
        Assert.Equal("git", words[0]);
        return words[1] switch
        {
            "diff-tree" when words.Contains("--stdin") => "diff-tree --stdin",
            "diff-tree" => "diff-tree " + words.Single(word => word.StartsWith("--diff-filter=", StringComparison.Ordinal)),
            "merge-base" => "merge-base " + words[2],
            var command => command,
        };
    }
}
