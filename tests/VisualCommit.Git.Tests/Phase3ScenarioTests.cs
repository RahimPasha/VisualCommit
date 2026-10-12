using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.ScenarioTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Pins down phase 3's scenario repos (design 7.1): their graphs as the app lays them out, refs,
/// remotes and states. A class of its own beside <see cref="ScenarioTests"/> (the scenarios of
/// phases 0 to 2), so that xunit runs the two side by side.
/// </summary>
public class Phase3ScenarioTests
{
    // ---- OperationsAsync ------------------------------------------------------------------------

    [Fact]
    public async Task Operations_has_its_graph_in_the_documented_rows_and_lanes()
    {
        using var repo = await Scenarios.OperationsAsync();

        var (rows, lanes) = await GraphOfAsync(repo);

        Assert.Equal(
            [
                "0e95aca On main: Experiment · lane 0",
                "078ef05 Conflicting change · lane 1",
                "d93814d Fast-forward me · lane 2",
                "88d6b91 Main work · lane 0 · ends 2",
                "ce5ea6a Alpha two · lane 2",
                "dff8fbf Alpha one · lane 2",
                "8beca34 Add core · lane 0 · ends 1, 2",
                "4ef1f7c Initial commit · lane 0",
            ],
            rows);
        Assert.Equal(3, lanes);
        Assert.Equal(
            ["Conflicting change|12:06", "Fast-forward me|12:05", "Main work|12:04", "Alpha two|12:03", "Alpha one|12:02", "Add core|12:01", "Initial commit|12:00"],
            Lines(await repo.GitAsync("log", "--date-order", "--format=%s|%ad", "--date=format:%H:%M", "--branches", "--remotes", "--tags", "HEAD")));
    }

    [Fact]
    public async Task Operations_has_its_branches_tags_remote_and_stash()
    {
        using var repo = await Scenarios.OperationsAsync();

        Assert.Equal(
            [
                "refs/heads/feature/alpha ce5ea6a refs/remotes/origin/feature/alpha ",
                "refs/heads/feature/conflict 078ef05  ",
                "refs/heads/feature/ff d93814d  ",
                "refs/heads/main 88d6b91 refs/remotes/origin/main ",
                "refs/remotes/origin/feature/alpha ce5ea6a  ",
                "refs/remotes/origin/main 88d6b91  ",
                "refs/stash 0e95aca  ",
                "refs/tags/v1.0 c8e9976  ",
                "refs/tags/v1.1 88d6b91  ",
            ],
            await RefsAsync(repo));

        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty((await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput);
        Assert.Equal([new StashListEntry("stash@{0}", "0e95aca", "On main: Experiment")], (await repo.StashListAsync()).Select(Short));
        Assert.Equal(Scenarios.OperationsFiles.ReadmeStashed, (await repo.GitAsync("show", "refs/stash:README.md")).StandardOutput);

        Assert.Equal("tag", (await repo.GitAsync("cat-file", "-t", "refs/tags/v1.0")).StandardOutput.Trim());
        Assert.Equal("Version 1.0", (await repo.GitAsync("tag", "--list", "--format=%(contents:subject)", "v1.0")).StandardOutput.Trim());
        Assert.Equal("Add core", Subject(await repo.GitAsync("log", "-1", "--format=%s", "refs/tags/v1.0")));
        Assert.Equal("commit", (await repo.GitAsync("cat-file", "-t", "refs/tags/v1.1")).StandardOutput.Trim());

        // The remote has main and feature/alpha only.
        Assert.Equal(
            ["refs/heads/feature/alpha ce5ea6a", "refs/heads/main 88d6b91"],
            Lines(await repo.GitAsync("ls-remote", "--heads", "origin")).Select(line => $"{line.Split('\t')[1]} {line[..7]}"));
        RepositoryTestSupport.AssertSameFolder(repo.SiblingPath("origin.git"), Path.GetFullPath((await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim()));
    }

    [Fact]
    public async Task Operations_has_the_documented_files_on_each_branch()
    {
        using var repo = await Scenarios.OperationsAsync();

        Assert.Equal(Scenarios.OperationsFiles.Readme, await FileAtAsync(repo, "refs/heads/main", "README.md"));
        Assert.Equal(Scenarios.OperationsFiles.CoreMainWork, await FileAtAsync(repo, "refs/heads/main", "src/core.txt"));
        Assert.Equal(Scenarios.OperationsFiles.Core, await FileAtAsync(repo, "refs/tags/v1.0", "src/core.txt"));
        Assert.Equal(Scenarios.OperationsFiles.AlphaOne, await FileAtAsync(repo, "refs/heads/feature/alpha~1", "src/alpha.txt"));
        Assert.Equal(Scenarios.OperationsFiles.AlphaTwo, await FileAtAsync(repo, "refs/heads/feature/alpha", "src/alpha.txt"));
        Assert.Equal(Scenarios.OperationsFiles.FastForward, await FileAtAsync(repo, "refs/heads/feature/ff", "docs/ff.txt"));
        Assert.Equal(Scenarios.OperationsFiles.CoreConflict, await FileAtAsync(repo, "refs/heads/feature/conflict", "src/core.txt"));
        Assert.Equal(Scenarios.OperationsFiles.Readme, File.ReadAllText(Path.Combine(repo.Path, "README.md")));
    }

    // ---- StoppedAsync ---------------------------------------------------------------------------

    [Fact]
    public async Task Stopped_merge_has_merge_head_and_a_conflicted_core()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);

        Assert.Equal(["UU src/core.txt"], await StatusAsync(repo));
        Assert.Equal("88d6b91 Main work", await LogOneAsync(repo, "HEAD"));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(await repo.RefTargetAsync("refs/heads/feature/conflict"), ReadGitFile(repo, "MERGE_HEAD")?.Trim());
        Assert.Equal("Merge branch 'feature/conflict'", FirstLine(ReadGitFile(repo, "MERGE_MSG")));
        Assert.Equal(["MERGE_HEAD", "MERGE_MSG"], InProgressFiles(repo));
    }

    [Fact]
    public async Task Stopped_cherry_pick_has_picked_one_commit_stopped_at_the_second_and_one_waits()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);

        Assert.Equal(["UU src/core.txt"], await StatusAsync(repo));
        Assert.Equal(["187627e Alpha one", "88d6b91 Main work"], Lines(await repo.GitAsync("log", "-2", "--format=%h %s", "refs/heads/main")));
        Assert.Equal("main", await repo.CurrentBranchAsync());

        var conflicting = await repo.RefTargetAsync("refs/heads/feature/conflict");
        var alphaTwo = await repo.RefTargetAsync("refs/heads/feature/alpha");
        Assert.Equal(conflicting, ReadGitFile(repo, "CHERRY_PICK_HEAD")?.Trim());
        Assert.Equal("Conflicting change", FirstLine(ReadGitFile(repo, "MERGE_MSG")));

        // The stopped pick stays the todo's first line; the waiting one follows. The abbreviation
        // length and anything after the id are git's to choose.
        var todo = TodoLines(ReadGitFile(repo, "sequencer/todo"));
        Assert.Equal(2, todo.Length);
        AssertPick(todo[0], conflicting!);
        AssertPick(todo[1], alphaTwo!);
        Assert.Equal(["CHERRY_PICK_HEAD", "MERGE_MSG", "sequencer"], InProgressFiles(repo));
    }

    [Fact]
    public async Task Stopped_revert_has_revert_head_after_core_again()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Revert);

        Assert.Equal(["UU src/core.txt"], await StatusAsync(repo));
        Assert.Equal(["963800f Core again", "88d6b91 Main work"], Lines(await repo.GitAsync("log", "-2", "--format=%h %s", "refs/heads/main")));
        Assert.Equal(Scenarios.OperationsFiles.CoreAgain, await FileAtAsync(repo, "HEAD", "src/core.txt"));
        Assert.Equal(await repo.RefTargetAsync("refs/remotes/origin/main"), ReadGitFile(repo, "REVERT_HEAD")?.Trim());
        Assert.Equal("Revert \"Main work\"", FirstLine(ReadGitFile(repo, "MERGE_MSG")));
        Assert.Equal(["MERGE_MSG", "REVERT_HEAD"], InProgressFiles(repo));
    }

    [Fact]
    public async Task Stopped_rebase_has_rebase_merge_with_feature_conflict_onto_main()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Rebase);

        Assert.Equal(["UU src/core.txt"], await StatusAsync(repo));
        var mainWork = await repo.RefTargetAsync("refs/heads/main");
        Assert.Equal(mainWork, await repo.HeadAsync());
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "--quiet", "HEAD")).Succeeded, "HEAD is detached during a rebase.");
        Assert.Equal("078ef05 Conflicting change", await LogOneAsync(repo, "refs/heads/feature/conflict"));

        Assert.Equal("refs/heads/feature/conflict", ReadGitFile(repo, "rebase-merge/head-name")?.Trim());
        Assert.Equal(mainWork, ReadGitFile(repo, "rebase-merge/onto")?.Trim());
        Assert.Empty(TodoLines(ReadGitFile(repo, "rebase-merge/git-rebase-todo")));
        Assert.Contains("rebase-merge", InProgressFiles(repo));
        Assert.DoesNotContain("rebase-apply", InProgressFiles(repo));
        Assert.DoesNotContain("MERGE_HEAD", InProgressFiles(repo));
    }

    [Fact]
    public async Task Stopped_none_has_a_conflicted_core_from_a_stash_and_no_operation()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.None);

        Assert.Equal(["UU src/core.txt"], await StatusAsync(repo));
        Assert.Equal(["91a5538 Core again", "88d6b91 Main work"], Lines(await repo.GitAsync("log", "-2", "--format=%h %s", "refs/heads/main")));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(
            [new StashListEntry("stash@{0}", "230e84c", "On main: Core experiment"), new StashListEntry("stash@{1}", "0e95aca", "On main: Experiment")],
            (await repo.StashListAsync()).Select(Short));
        Assert.Equal(Scenarios.OperationsFiles.CoreStashed, (await repo.GitAsync("show", "refs/stash:src/core.txt")).StandardOutput);
        Assert.Empty(InProgressFiles(repo));
    }

    // ---- RemoteSyncAsync ------------------------------------------------------------------------

    [Fact]
    public async Task Remote_sync_has_its_graph_branches_and_upstreams()
    {
        using var repo = await Scenarios.RemoteSyncAsync();

        var (rows, lanes) = await GraphOfAsync(repo);
        Assert.Equal(
            [
                "77d782e Topic work · lane 0",
                "8e65315 Unpushed work · lane 1",
                "8c6f545 Published work · lane 1",
                "a7f2cfd Shared work · lane 0 · ends 1",
                "6688774 Initial commit · lane 0",
            ],
            rows);
        Assert.Equal(2, lanes);

        Assert.Equal(
            [
                "refs/heads/main a7f2cfd refs/remotes/origin/main ",
                "refs/heads/published 8e65315 refs/remotes/origin/published [ahead 1]",
                "refs/heads/topic 77d782e  ",
                "refs/remotes/origin/main a7f2cfd  ",
                "refs/remotes/origin/published 8c6f545  ",
            ],
            await RefsAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty((await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput);
        Assert.Equal(["Topic work|12:04", "Unpushed work|12:03", "Published work|12:02", "Shared work|12:01", "Initial commit|12:00"],
            Lines(await repo.GitAsync("log", "--date-order", "--format=%s|%ad", "--date=format:%H:%M", "--branches", "--remotes", "HEAD")));
    }

    [Fact]
    public async Task Remote_sync_push_from_elsewhere_puts_origin_ahead_and_reuses_its_clone()
    {
        using var repo = await Scenarios.RemoteSyncAsync();
        Assert.False(Directory.Exists(repo.SiblingPath("other")), "The clone is made per test, after the copy.");

        var first = await Scenarios.PushFromElsewhereAsync(repo, "main", "src/elsewhere.txt", "elsewhere\n", "Elsewhere work");
        Assert.True(Directory.Exists(repo.SiblingPath("other")));
        await repo.GitAsync("fetch", "--quiet", "origin");
        Assert.Equal("b2e9a9e Elsewhere work", await LogOneAsync(repo, first));
        Assert.Equal("12:05", (await repo.GitAsync("log", "-1", "--format=%ad", "--date=format:%H:%M", first)).StandardOutput.Trim());
        Assert.Equal(first, await repo.RefTargetAsync("refs/remotes/origin/main"));

        // The same clone again, on another branch: origin's published moves on from where origin had it.
        var second = await Scenarios.PushFromElsewhereAsync(repo, "published", "src/published.txt", "published\nelsewhere\n", "Published elsewhere");
        await repo.GitAsync("fetch", "--quiet", "origin");
        Assert.Equal(["Published elsewhere", "Published work"], Lines(await repo.GitAsync("log", "-2", "--format=%s", second)));
        Assert.Equal(
            [
                "refs/heads/main a7f2cfd refs/remotes/origin/main [behind 1]",
                "refs/heads/published 8e65315 refs/remotes/origin/published [ahead 1, behind 1]",
                "refs/heads/topic 77d782e  ",
                "refs/remotes/origin/main b2e9a9e  ",
                $"refs/remotes/origin/published {second[..7]}  ",
            ],
            await RefsAsync(repo));

        // A local commit then makes main diverge, and its push is rejected.
        await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");
        Assert.Equal("refs/heads/main refs/remotes/origin/main [ahead 1, behind 1]", Lines(await repo.GitAsync("for-each-ref", "--format=%(refname) %(upstream) %(upstream:track)", "refs/heads/main")).Single());
        Assert.False((await repo.GitAllowingFailureAsync("push", "--quiet", "origin", "refs/heads/main:refs/heads/main")).Succeeded);
    }

    // ---- CheckoutBlockedAsync -------------------------------------------------------------------

    [Fact]
    public async Task Checkout_blocked_has_its_branches_and_local_changes()
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();

        var (rows, lanes) = await GraphOfAsync(repo);
        Assert.Equal(
            [
                "3b6b5c7 Add newfile · lane 0",
                "9100bdd Clashing change · lane 1",
                "850a7c0 Other change · lane 0",
                "106efba Add notes · lane 0 · ends 1",
            ],
            rows);
        Assert.Equal(2, lanes);
        Assert.Equal(
            ["refs/heads/feature/addfile 3b6b5c7  ", "refs/heads/feature/clash 9100bdd  ", "refs/heads/feature/other 850a7c0  ", "refs/heads/main 106efba  "],
            await RefsAsync(repo));

        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal([" M notes.txt", "?? newfile.txt"], await StatusAsync(repo));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NotesLocal, File.ReadAllText(Path.Combine(repo.Path, "notes.txt")));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NewFileUntracked, File.ReadAllText(Path.Combine(repo.Path, "newfile.txt")));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NotesOther, await FileAtAsync(repo, "refs/heads/feature/other", "notes.txt"));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NotesClash, await FileAtAsync(repo, "refs/heads/feature/clash", "notes.txt"));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NotesOther, await FileAtAsync(repo, "refs/heads/feature/addfile", "notes.txt"));
        Assert.Equal(Scenarios.CheckoutBlockedFiles.NewFileCommitted, await FileAtAsync(repo, "refs/heads/feature/addfile", "newfile.txt"));
    }

    [Theory]
    [InlineData("feature/other", false)]
    [InlineData("feature/clash", false)]
    [InlineData("feature/addfile", true)]
    public async Task Checkout_blocked_refuses_each_branch_for_the_documented_reasons(string branch, bool untrackedInTheWay)
    {
        using var repo = await Scenarios.CheckoutBlockedAsync();

        var checkout = await repo.GitAllowingFailureAsync("checkout", branch);

        Assert.False(checkout.Succeeded);
        Assert.Contains("Your local changes to the following files would be overwritten by checkout", checkout.StandardError, StringComparison.Ordinal);
        Assert.Equal(untrackedInTheWay, checkout.StandardError.Contains("untracked working tree files would be overwritten", StringComparison.Ordinal));
        Assert.Equal("main", await repo.CurrentBranchAsync());
    }

    [Fact]
    public async Task Checkout_blocked_stash_and_checkout_applies_cleanly_on_other_and_conflicts_on_clash()
    {
        using var other = await Scenarios.CheckoutBlockedAsync();
        await other.GitAsync("stash", "push", "--quiet");
        await other.GitAsync("checkout", "--quiet", "feature/other");
        await other.GitAsync("stash", "pop", "--quiet");
        Assert.Equal("one (local)\ntwo\nthree\nfour\nfive\nsix\nseven (other)\n", File.ReadAllText(Path.Combine(other.Path, "notes.txt")));
        Assert.Empty(await other.StashListAsync());

        using var clash = await Scenarios.CheckoutBlockedAsync();
        await clash.GitAsync("stash", "push", "--quiet");
        await clash.GitAsync("checkout", "--quiet", "feature/clash");
        Assert.False((await clash.GitAllowingFailureAsync("stash", "pop")).Succeeded);
        Assert.Equal(["UU notes.txt", "?? newfile.txt"], await StatusAsync(clash));
        Assert.Single(await clash.StashListAsync());
    }

    // ---- EdgeCasesAsync -------------------------------------------------------------------------

    [Fact]
    public async Task Edge_cases_has_its_graph_and_odd_refs()
    {
        using var repo = await Scenarios.EdgeCasesAsync();

        var (rows, lanes) = await GraphOfAsync(repo);
        Assert.Equal(
            [
                "419190f Detached work · lane 0",
                "a024e50 Alpha work · lane 1",
                "8e16408 Main work · lane 0 · ends 1",
                "691f8cf Initial commit · lane 0",
            ],
            rows);
        Assert.Equal(2, lanes);

        Assert.Equal(
            [
                "refs/heads/feature/alpha a024e50  ",
                "refs/heads/loc 691f8cf refs/heads/main [behind 1]",
                "refs/heads/main 8e16408 refs/remotes/origin/main ",
                "refs/heads/origin/main 691f8cf  ",
                "refs/heads/side 691f8cf  ",
                "refs/remotes/origin/main 8e16408  ",
                "refs/tags/feature/alpha 8e16408  ",
            ],
            await RefsAsync(repo));

        // loc's upstream is local, as D101 reads it.
        Assert.Equal(".", (await repo.GitAsync("config", "branch.loc.remote")).StandardOutput.Trim());
        Assert.Equal("refs/heads/main", (await repo.GitAsync("config", "branch.loc.merge")).StandardOutput.Trim());
        Assert.Equal(
            "refs/heads/main|.|refs/heads/main",
            (await repo.GitAsync("for-each-ref", "--format=%(upstream)|%(upstream:remotename)|%(upstream:remoteref)", "refs/heads/loc")).StandardOutput.Trim());

        // HEAD is detached on a commit no ref reaches.
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "--quiet", "HEAD")).Succeeded);
        Assert.Equal("419190f Detached work", await LogOneAsync(repo, "HEAD"));
        Assert.Empty((await repo.GitAsync("for-each-ref", "--contains", "HEAD")).StandardOutput);
        Assert.Empty((await repo.GitAsync("status", "--porcelain", "--untracked-files=all")).StandardOutput);
    }

    [Fact]
    public async Task Edge_cases_side_is_checked_out_in_the_copys_own_worktree_and_cannot_be_fast_forwarded()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var second = await Scenarios.EdgeCasesAsync();
        var worktree = repo.SiblingPath(Scenarios.EdgeCasesWorktree);

        // git knows the worktree at the copy's own folder, and the worktree knows the copy.
        var listed = Lines(await repo.GitAsync("worktree", "list", "--porcelain"))
            .Where(line => line.StartsWith("worktree ", StringComparison.Ordinal))
            .Select(line => line["worktree ".Length..])
            .ToArray();
        Assert.Equal(2, listed.Length);
        AssertSameFolderAs(repo.Path, listed[0]);
        AssertSameFolderAs(worktree, listed[1]);
        Assert.Equal("side", await GitInAsync(repo, worktree, "symbolic-ref", "--short", "HEAD"));
        AssertSameFolderAs(Path.Combine(repo.Path, ".git"), await GitInAsync(repo, worktree, "rev-parse", "--path-format=absolute", "--git-common-dir"));
        Assert.Equal(string.Empty, await GitInAsync(repo, worktree, "status", "--porcelain"));

        // The other copy's worktree is its own.
        var secondListed = Lines(await second.GitAsync("worktree", "list", "--porcelain")).Last(line => line.StartsWith("worktree ", StringComparison.Ordinal));
        AssertSameFolderAs(second.SiblingPath(Scenarios.EdgeCasesWorktree), secondListed["worktree ".Length..]);

        // Fast-forwarding side through git fetch . is refused, and side stays behind main.
        var fetch = await repo.GitAllowingFailureAsync("fetch", ".", "refs/heads/main:refs/heads/side");
        Assert.False(fetch.Succeeded);
        Assert.Contains("refusing to fetch into", fetch.StandardError, StringComparison.Ordinal);
        Assert.Equal("691f8cf Initial commit", await LogOneAsync(repo, "refs/heads/side"));
    }

    // ---- AuthRemoteAsync ------------------------------------------------------------------------

    [Fact]
    public async Task Auth_remote_is_a_clone_from_before_the_servers_remote_work()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);

        Assert.Equal(server.Url, (await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim());
        Assert.Equal(["e4a56bd Initial commit"], Lines(await repo.GitAsync("log", "--format=%h %s", "--branches", "--remotes", "HEAD")));
        Assert.Equal(
            ["refs/heads/main e4a56bd refs/remotes/origin/main ", "refs/remotes/origin/main e4a56bd  "],
            await RefsAsync(repo));

        // The server has one commit more, which the clone has never seen.
        var serverLog = await GitInAsync(repo, server.RepositoryPath, "log", "--format=%h %s|%ad", "--date=format:%H:%M", "refs/heads/main");
        Assert.Equal(["e6761c7 Remote work|12:01", "e4a56bd Initial commit|12:00"], Lines(serverLog));
        var remoteWork = serverLog[..7];
        Assert.False((await repo.GitAllowingFailureAsync("cat-file", "-e", remoteWork + "^{commit}")).Succeeded);
        Assert.Empty(server.Requests);
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// <summary>
    /// The graph as the app lays it out (D48, D54, D55): the commits and stashes the app loads,
    /// fed to <see cref="GraphLayout"/> page by page as the view model does. Each row reads
    /// "short SHA, subject · lane n", plus "· ends a, b" when other lanes end in its node.
    /// </summary>
    private static async Task<(string[] Rows, int Lanes)> GraphOfAsync(TempRepo repo)
    {
        var repository = await OpenAsync(repo);
        var refs = await repository.ReadRefsAsync(TestCancelled);
        var layout = new GraphLayout();
        var rows = new List<string>();
        await repository.LoadCommitsAsync(
            refs,
            page =>
            {
                lock (rows)
                {
                    foreach (var commit in page)
                    {
                        var row = layout.Add(commit);
                        var ends = row.Lines
                            .Where(line => line.Kind == GraphLineKind.IntoNode && line.FromLane != row.NodeLane)
                            .Select(line => line.FromLane)
                            .ToList();
                        rows.Add($"{commit.Sha[..7]} {commit.Subject} · lane {row.NodeLane}" + (ends.Count > 0 ? $" · ends {string.Join(", ", ends)}" : string.Empty));
                    }
                }
            },
            TestCancelled);
        return ([.. rows], layout.MaxLaneCount);
    }

    /// <summary>Every ref: its name, short SHA, upstream and how far ahead or behind.</summary>
    private static async Task<string[]> RefsAsync(TempRepo repo) =>
        Lines(await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname:short=7) %(upstream) %(upstream:track)"));

    private static async Task<string[]> StatusAsync(TempRepo repo) =>
        Lines(await repo.GitAsync("status", "--porcelain=v1", "--untracked-files=all"));

    private static async Task<string> LogOneAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("log", "-1", "--format=%h %s", revision)).StandardOutput.Trim();

    private static async Task<string> FileAtAsync(TempRepo repo, string revision, string path) =>
        (await repo.GitAsync("show", $"{revision}:{path}")).StandardOutput;

    /// <summary>Runs git in <paramref name="folder"/> (a worktree, a bare repository) with <paramref name="repo"/>'s isolation.</summary>
    private static async Task<string> GitInAsync(TempRepo repo, string folder, params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = folder, Environment = repo.Environment };
        return (await repo.Runner.RunAsync(command, TestCancelled)).EnsureSuccess(command).StandardOutput.Trim();
    }

    private static StashListEntry Short(StashListEntry entry) => entry with { Sha = entry.Sha[..7] };

    /// <summary>A file in the repository's git folder, or null when it does not exist.</summary>
    private static string? ReadGitFile(TempRepo repo, string relativePath)
    {
        var path = Path.Combine(repo.Path, ".git", relativePath);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>Which of the files and folders that mark an operation in progress (design 5) the git folder holds, in ordinal order.</summary>
    private static string[] InProgressFiles(TempRepo repo) =>
        [.. new[] { "MERGE_HEAD", "MERGE_MSG", "CHERRY_PICK_HEAD", "REVERT_HEAD", "sequencer", "rebase-merge", "rebase-apply" }
            .Where(name => File.Exists(Path.Combine(repo.Path, ".git", name)) || Directory.Exists(Path.Combine(repo.Path, ".git", name)))
            .Order(StringComparer.Ordinal)];

    private static string FirstLine(string? text) => (text ?? string.Empty).Split('\n')[0];

    /// <summary>The lines of a todo file that are neither empty nor comments.</summary>
    private static string[] TodoLines(string? todo) =>
        [.. (todo ?? string.Empty).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'))];

    private static void AssertPick(string line, string sha)
    {
        var fields = line.Split(' ');
        Assert.True(fields.Length >= 2 && fields[0] == "pick" && fields[1].Length >= 7 && sha.StartsWith(fields[1], StringComparison.Ordinal), $"Not a pick of {sha}: {line}");
    }

    /// <summary>
    /// Asserts that git's <paramref name="actual"/> path is the folder <paramref name="expected"/>:
    /// by a file written through one and looked for through the other, since git writes the
    /// physical path with forward slashes (macOS's /private/var for /var).
    /// </summary>
    private static void AssertSameFolderAs(string expected, string actual)
    {
        var marker = $"same-folder-{Guid.NewGuid():N}";
        var markerPath = Path.Combine(expected, marker);
        File.WriteAllText(markerPath, string.Empty);
        try
        {
            Assert.True(File.Exists(Path.Combine(actual, marker)), $"{actual} is not the folder {expected}.");
        }
        finally
        {
            File.Delete(markerPath);
        }
    }
}
