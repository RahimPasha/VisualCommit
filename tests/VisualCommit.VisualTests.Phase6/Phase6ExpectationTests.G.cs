// A file named *.G.cs is generated code to the compiler (its *.g.cs rule, which ignores case):
// nullable annotations are off in it unless the file turns them on, as it does here, and the
// analyzers that leave generated code alone leave it alone too.
#nullable enable
using Avalonia;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

/// <summary>
/// Part G's expectations (Phase6Expectations.G.cs) held against plain git on fresh copies of the
/// scenarios "ignored" and "refspecs", and against each other: the SHAs, refs, files and statuses
/// the checks compare with; the app's own reads TR1 to TR9 run as the app runs them, with their exit
/// codes and outputs; git's refusals TG1 to TG3 and the states G24 and G25 lead to. No app: a
/// scenario that drifted, or an expectation copied wrong, fails here, before a window opens. Git's
/// own texts are asserted word for word on the report's git, 2.36 only; on every git their key
/// lines, exit codes and states (part G's "Git's own texts").
/// </summary>
public sealed class Phase6ExpectationTestsG
{
    // ---- "ignored" ----

    /// <summary>IG0: the commits' SHAs and subjects as the graph's rows name them, the refs, the stash, the index, the files, the status, the branch keys and HEAD's reflog.</summary>
    [Fact]
    public async Task Ignored_is_IG0_as_the_report_gives_it()
    {
        using var repo = await Scenarios.IgnoredAsync();

        Assert.Equal(PartG.Ig0Refs, await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)"));
        var shas = new[] { PartG.StashSha, PartG.MainWorkSha, PartG.BuildFileSha, PartG.VendorSecretSha, PartG.StopTrackingSha, PartG.TrackSecretSha, PartG.InitialCommitSha };
        Assert.Equal(shas.Select(sha => sha[..7]), PartG.Ig0.Select(row => row.ShortSha));
        foreach (var (row, sha) in PartG.Ig0.Zip(shas))
        {
            Assert.Equal(row.Message, (await LinesAsync(repo, "log", "-1", "--format=%s", sha)).Single());
        }

        Assert.Equal($"{PartG.MainWorkSha} {PartG.StashIndexSha}", (await LinesAsync(repo, "log", "-1", "--format=%P", "refs/stash")).Single());
        Assert.Equal("index on main: c49cac4 Main work", (await LinesAsync(repo, "log", "-1", "--format=%s", PartG.StashIndexSha)).Single());
        Assert.Equal([$"stash@{{0}}: {PartG.StashListed}"], await LinesAsync(repo, "stash", "list"));
        Assert.Equal(
            PartG.Ig0Index.Select(entry => entry.Path),
            (await LinesAsync(repo, "ls-files", "--stage")).Select(line => line.Split('\t')[1]));
        Assert.All(
            (await LinesAsync(repo, "ls-files", "--stage")).Zip(PartG.Ig0Index),
            pair => Assert.StartsWith("100644 " + pair.Second.Blob, pair.First, StringComparison.Ordinal));
        Assert.Equal(PartG.SecretLocal, File.ReadAllText(Path.Combine(repo.Path, "secret.env")));
        Assert.Equal(PartG.BuildOutput, File.ReadAllText(Path.Combine(repo.Path, "build", "out.bin")));
        Assert.Equal(PartG.AppMain, File.ReadAllText(Path.Combine(repo.Path, "app.txt")));
        Assert.Equal(PartG.IgnoredStatus, await LinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all", "--ignored"));
        Assert.Equal(["! build/out.bin", "! secret.env"], (await LinesAsync(repo, "status", "--porcelain=v2", "--untracked-files=all", "--ignored")).Where(line => line.StartsWith('!')));
        Assert.Equal(PartG.IgnoredBranchKeys, await LinesAsync(repo, "config", "--get-regexp", @"^branch\."));
        var reflog = await LinesAsync(repo, "log", "-g", "--format=%gs", "HEAD");
        Assert.Equal((PartG.Ig0ReflogEntries, PartG.StashResetReflog), (reflog.Length, reflog[0]));
        Assert.Equal(["refs/heads/main " + PartG.MainWorkSha, "refs/heads/vendor " + PartG.VendorSecretSha, "refs/heads/vendor-remote " + PartG.VendorSecretSha], await LinesAsync(repo, "-C", repo.SiblingPath("origin.git"), "for-each-ref", "--format=%(refname) %(objectname)"));
    }

    /// <summary>
    /// G22's checkouts, each refused by git with <c>--no-overwrite-ignore</c> (TG1, TG2) and nothing
    /// changed; <c>check-ignore</c> without <c>GIT_LITERAL_PATHSPECS</c> answers with the paths
    /// (D74), and git refuses it with the variable set (2.36).
    /// </summary>
    [Fact]
    public async Task The_checkouts_git_refuses_print_TG1_and_TG2_and_change_nothing()
    {
        using var repo = await Scenarios.IgnoredAsync();
        var exact = await repo.ExactStateAsync();
        var reportGit = await IsReportGitAsync();

        foreach (var command in new[] { PartG.SwitchVendorCommand, PartG.SwitchVendorRemoteCommand, PartG.CreateVendorCopyCommand, PartG.DetachVendorSecretCommand })
        {
            var refused = await RunAsync(repo, command);
            Assert.True(refused.ExitCode == 1, $"\"{command}\" exited {refused.ExitCode}.\n{refused.StandardError}");
            Assert.Contains(PartG.UntrackedKey, Lines(refused.StandardError));
            if (reportGit)
            {
                Assert.Equal(string.Empty, refused.StandardOutput);
                Assert.Equal(PartG.Tg1, GitOutputCleaner.Clean(refused.StandardError, refused.StandardOutput).Split('\n'));
            }

            Assert.Equal(exact, await repo.ExactStateAsync());
        }

        var folder = await RunAsync(repo, PartG.SwitchToolsCommand);
        Assert.True(folder.ExitCode == 1, $"\"{PartG.SwitchToolsCommand}\" exited {folder.ExitCode}.\n{folder.StandardError}");
        Assert.Contains(PartG.FolderKey, Lines(folder.StandardError));
        if (reportGit)
        {
            Assert.Equal(PartG.Tg2, GitOutputCleaner.Clean(folder.StandardError, folder.StandardOutput).Split('\n'));
        }

        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Null(await RevParseAsync(repo, "refs/heads/vendor-remote"));
        Assert.Null(await RevParseAsync(repo, "refs/heads/vendor-copy"));

        AssertRead(await RunAsync(repo, PartG.CheckIgnoreCommand, "secret.env\0", literalPathspecs: false), new ExpectedRead(PartG.CheckIgnoreCommand, 0, PartG.SecretIgnored));
        AssertRead(await RunAsync(repo, PartG.CheckIgnoreCommand, "build/out.bin\0", literalPathspecs: false), new ExpectedRead(PartG.CheckIgnoreCommand, 0, PartG.BuildOutIgnored));
        if (reportGit)
        {
            var literal = await RunAsync(repo, PartG.CheckIgnoreCommand, "secret.env\0", literalPathspecs: true);
            Assert.Equal(128, literal.ExitCode);
            Assert.Contains("fatal: secret.env: pathspec magic not supported by this command: 'literal'", Lines(literal.StandardError));
        }
    }

    /// <summary>TR1 to TR5, the app's reads before git of G23 part A, run as the app runs them on IG0: each exit code and output as the report gives them.</summary>
    [Fact]
    public async Task The_reads_before_git_TR1_to_TR5_give_the_reports_outputs()
    {
        using var repo = await Scenarios.IgnoredAsync();

        // TR1: the merge of vendor; check-ignore is fed what the first read found.
        AssertRead(await RunAsync(repo, PartG.Tr1[0].Command), PartG.Tr1[0]);
        AssertRead(await RunAsync(repo, PartG.Tr1[1].Command, "secret.env\0", literalPathspecs: false), PartG.Tr1[1]);
        AssertRead(await RunAsync(repo, PartG.Tr1[2].Command), PartG.Tr1[2]);
        AssertRead(await RunAsync(repo, PartG.Tr1[3].Command), PartG.Tr1[3]);

        // TR2: the rebase onto vendor's SHA; check-ignore fed the replayed and added paths.
        foreach (var read in PartG.Tr2SideBySide)
        {
            AssertRead(await RunAsync(repo, read.Command), read);
        }

        AssertRead(await RunAsync(repo, PartG.Tr2CheckIgnore.Command, "app.txt\0secret.env\0", literalPathspecs: false), PartG.Tr2CheckIgnore);

        // TR3 and TR4: the pick and the revert, fed the commit's SHA.
        AssertRead(await RunAsync(repo, PartG.Tr3[0].Command, PartG.VendorSecretSha + "\n"), PartG.Tr3[0]);
        AssertRead(await RunAsync(repo, PartG.Tr4[0].Command, PartG.StopTrackingSha + "\n"), PartG.Tr4[0]);
        AssertRead(await RunAsync(repo, PartG.CheckIgnoreCommand, "secret.env\0", literalPathspecs: false), PartG.Tr3[1]);

        // TR5: the stash's three reads.
        foreach (var read in PartG.Tr5StashReads)
        {
            AssertRead(await RunAsync(repo, read.Command), read);
        }

        Assert.Equal(PartG.Ig0Refs, await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)"));
    }

    /// <summary>
    /// IG1 built as GD7 says (its SHAs on every git), and TR6 to TR9 run on it as the pull runs them,
    /// with the fetch's <c>FETCH_HEAD</c> of <c>origin/main</c>, then of <c>origin/vendor</c>
    /// (IG1v); under <c>pull.ff=only</c> git's own refusal (TG3).
    /// </summary>
    [Fact]
    public async Task IG1_and_the_pulls_reads_TR6_to_TR9_are_the_reports()
    {
        using var repo = await Scenarios.IgnoredAsync();
        var side = (await repo.GitAsync(PartG.SideStartCommitTree)).StandardOutput.Trim();
        Assert.Equal(PartG.SideStartSha, side);
        await repo.CreateBranchAsync(PartG.SideBranch, side);
        Assert.Equal(PartG.SideMergeSha, await repo.MergeAsync(PartG.SideBranch));
        Assert.Equal($"{PartG.MainWorkSha} {PartG.SideStartSha}", (await LinesAsync(repo, "log", "-1", "--format=%P", "HEAD")).Single());
        Assert.Equal($"{PartG.TrackSecretSha}", (await LinesAsync(repo, "log", "-1", "--format=%P", PartG.SideStartSha)).Single());
        Assert.Equal(PartG.SideMergeReflog, (await LinesAsync(repo, "log", "-g", "--format=%gs", "HEAD"))[0]);
        Assert.Equal(PartG.Ig1Refs, await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)"));
        Assert.Equal(PartG.SecretLocal, File.ReadAllText(Path.Combine(repo.Path, "secret.env")));
        Assert.Equal(PartG.IgnoredStatus, await LinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all", "--ignored"));
        Assert.Equal(["# branch.ab +2 -0"], (await LinesAsync(repo, "status", "--porcelain=v2", "--branch")).Where(line => line.StartsWith("# branch.ab", StringComparison.Ordinal)));

        // TR6 (and TR7's first read) after the pull's fetch of origin/main.
        await AssertQuietFetchAsync(repo);
        AssertRead(await RunAsync(repo, PartG.HeadRead), new ExpectedRead(PartG.HeadRead, 0, PartG.SideMergeSha));
        AssertRead(await RunAsync(repo, PartG.IsSideMergeAncestorOfFetchHead), new ExpectedRead(PartG.IsSideMergeAncestorOfFetchHead, 1, string.Empty));
        foreach (var read in PartG.Tr6SideBySide)
        {
            AssertRead(await RunAsync(repo, read.Command), read);
        }

        var points = await RunAsync(repo, PartG.BranchPointsRead);
        Assert.Equal(0, points.ExitCode);
        var lines = PartG.Normalised(points.StandardOutput).Split('\n');
        Assert.Equal(PartG.BranchPointsTip, lines[0]);
        Assert.Equal(PartG.BranchPointsInRange.Order(StringComparer.Ordinal), lines.Where(line => !line.StartsWith('-')).Order(StringComparer.Ordinal));
        Assert.Equal(PartG.BranchPointsBoundary.Order(StringComparer.Ordinal), lines.Where(line => line.StartsWith('-')).Order(StringComparer.Ordinal));
        if (await IsReportGitAsync())
        {
            Assert.Equal(PartG.BranchPointsOn236, lines);
        }

        var pairs = string.Concat(lines.Where(line => line.StartsWith('-')).Select(line => $"{line[1..]} {PartG.SideMergeSha}\n"));
        AssertRead(await RunAsync(repo, PartG.BranchPointsAddRead.Command, pairs), PartG.BranchPointsAddRead);
        AssertRead(await RunAsync(repo, PartG.Tr7Added.Command), PartG.Tr7Added);

        // TR8 and TR9 on IG1v: main tracks origin/vendor, 3 ahead and 1 behind.
        await repo.ConfigAsync("branch.main.merge", "refs/heads/vendor");
        Assert.Equal(["# branch.upstream origin/vendor", "# branch.ab +3 -1"], (await LinesAsync(repo, "status", "--porcelain=v2", "--branch")).Where(line => line.StartsWith("# branch.upstream", StringComparison.Ordinal) || line.StartsWith("# branch.ab", StringComparison.Ordinal)));
        await AssertQuietFetchAsync(repo);
        var forMerge = File.ReadAllLines(Path.Combine(repo.Path, ".git", "FETCH_HEAD")).Where(line => line.Length > 0 && !line.Contains("\tnot-for-merge\t", StringComparison.Ordinal)).ToList();
        Assert.StartsWith(PartG.VendorSecretSha + "\t\tbranch 'vendor' of ", Assert.Single(forMerge), StringComparison.Ordinal);
        AssertRead(await RunAsync(repo, PartG.Tr8[0].Command), PartG.Tr8[0]);
        AssertRead(await RunAsync(repo, PartG.Tr8[1].Command, "secret.env\0", literalPathspecs: false), PartG.Tr8[1]);
        AssertRead(await RunAsync(repo, PartG.Tr8[2].Command), PartG.Tr8[2]);
        AssertRead(await RunAsync(repo, PartG.Tr8[3].Command), PartG.Tr8[3]);
        AssertRead(await RunAsync(repo, PartG.Tr9Diverged.Command), PartG.Tr9Diverged);

        // TG3: the fast-forward under pull.ff=only refused, nothing changed.
        var exact = await repo.ExactStateAsync();
        var refused = await RunAsync(repo, PartG.PullFastForwardCommand);
        Assert.True(refused.ExitCode == 128, $"\"{PartG.PullFastForwardCommand}\" exited {refused.ExitCode}.\n{refused.StandardError}");
        Assert.Contains(PartG.NotFastForwardKey, Lines(refused.StandardError));
        if (await IsReportGitAsync())
        {
            Assert.Equal(PartG.Tg3, GitOutputCleaner.Clean(refused.StandardError, refused.StandardOutput).Split('\n'));
        }

        Assert.Equal(exact, await repo.ExactStateAsync());
    }

    /// <summary>
    /// G24's states, as phase 5's merge and checkout make them with D72's dates: the merge
    /// <c>63b8fe1…</c> overwriting <c>secret.env</c> (TG9), then <c>tools</c> deleting the ignored
    /// <c>build/</c> (TG10); and the merge's message <c>git fmt-merge-msg</c> gives.
    /// </summary>
    [Fact]
    public async Task Without_the_check_git_merges_IG2_and_checks_out_IG3()
    {
        using var repo = await Scenarios.IgnoredAsync();
        var dates = new Dictionary<string, string?> { ["GIT_AUTHOR_DATE"] = "2026-01-02T09:00:00+00:00", ["GIT_COMMITTER_DATE"] = "2026-01-02T09:00:00+00:00" };

        var message = await RunAsync(repo, PartG.FmtMergeMsgCommand, PartG.VendorSecretSha + "\t\tbranch 'vendor' of .\n");
        Assert.Equal((0, PartG.VendorMergeMessage), (message.ExitCode, PartG.Normalised(message.StandardOutput)));
        var merge = await repo.GitWithEnvironmentAsync(dates, "merge", "--no-edit", "--no-log", "-m", PartG.VendorMergeMessage, "refs/heads/vendor");
        Assert.Contains(PartG.MergeMadeKey, Lines(merge.StandardOutput));
        if (await IsReportGitAsync())
        {
            Assert.Equal(PartG.Tg9, Lines(merge.StandardOutput));
        }

        Assert.Equal(PartG.VendorMergeSha, await repo.HeadAsync());
        Assert.Equal(PartG.Ig2[0].ShortSha, PartG.VendorMergeSha[..7]);
        Assert.Equal(PartG.SecretVendor, File.ReadAllText(Path.Combine(repo.Path, "secret.env")));
        Assert.Equal(PartG.MergedStatus, await LinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all", "--ignored"));
        Assert.Equal(PartG.VendorMergeReflog, (await LinesAsync(repo, "log", "-g", "--format=%gs", "HEAD"))[0]);
        Assert.Equal(["# branch.ab +2 -0"], (await LinesAsync(repo, "status", "--porcelain=v2", "--branch")).Where(line => line.StartsWith("# branch.ab", StringComparison.Ordinal)));

        var checkout = await RunAsync(repo, PartG.SwitchToolsWithoutOptionCommand);
        Assert.True(checkout.ExitCode == 0, $"\"{PartG.SwitchToolsWithoutOptionCommand}\" exited {checkout.ExitCode}.\n{checkout.StandardError}");
        Assert.Contains(PartG.SwitchedToTools, Lines(checkout.StandardError));
        Assert.Equal(PartG.BuildFile, File.ReadAllText(Path.Combine(repo.Path, "build")));
        Assert.Equal(PartG.AppInitial, File.ReadAllText(Path.Combine(repo.Path, "app.txt")));
        Assert.False(File.Exists(Path.Combine(repo.Path, "secret.env")), "secret.env is still there.");
        Assert.Empty(await LinesAsync(repo, "status", "--porcelain=v1", "--untracked-files=all", "--ignored"));
        Assert.Equal(PartG.ToolsCheckoutReflog, (await LinesAsync(repo, "log", "-g", "--format=%gs", "HEAD"))[0]);
    }

    // ---- "refspecs" ----

    /// <summary>RS0: the fetch lines the refs read gets (10.2), the tracking refs and the commits; and what git's own <c>--track</c> makes of the three refs the mappings name (scenario facts 3.2).</summary>
    [Fact]
    public async Task Refspecs_is_RS0_with_the_fetch_lines_of_3_1()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();

        var config = await RunAsync(repo, PartG.RemotesReadCommand);
        Assert.Equal(0, config.ExitCode);
        Assert.Equal(PartG.RemoteConfigLines, config.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries).Select(entry => entry.Replace('\n', ' ')));
        Assert.Equal(PartG.Rs0RemoteRefs, await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)", "refs/remotes"));
        Assert.Equal(
            ["refs/heads/main " + PartG.ParserSha, "refs/heads/topic " + PartG.TopicSha],
            await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)", "refs/heads"));
        var shas = new[] { PartG.TopicSha, PartG.ParserSha, PartG.RsInitialSha };
        Assert.Equal(shas.Select(sha => sha[..7]), PartG.Rs0.Select(row => row.ShortSha));
        foreach (var (row, sha) in PartG.Rs0.Zip(shas))
        {
            Assert.Equal(row.Message, (await LinesAsync(repo, "log", "-1", "--format=%s", sha)).Single());
        }

        Assert.Equal(["branch.main.remote origin", "branch.main.merge refs/heads/main"], await LinesAsync(repo, "config", "--get-regexp", @"^branch\."));
        Assert.Equal(["fork", "origin", "upstream"], await LinesAsync(repo, "remote"));

        foreach (var (tracking, remote, merge) in new[] { ("fork/up/main", "fork", "refs/heads/main"), ("origin/pr/1", "origin", "refs/heads/pr/1"), ("upstream/pr/7", "upstream", "refs/pull/7/head") })
        {
            await repo.GitAsync("branch", "--track", "probe", "refs/remotes/" + tracking);
            Assert.Equal([$"branch.probe.remote {remote}", $"branch.probe.merge {merge}"], await LinesAsync(repo, "config", "--get-regexp", @"^branch\.probe\."));
            await repo.GitAsync("branch", "-D", "probe");
        }
    }

    /// <summary>
    /// G25's operations as plain git makes them on fresh copies (scenario facts 3.3): the drop's push
    /// onto <c>fork/up/main</c> and git moving the tracking ref; <c>pr/1</c> made without an upstream;
    /// fork's <c>main</c> deleted with the mapped lease while phase 5's form is rejected as stale;
    /// <c>pr/7</c> tracking <c>refs/pull/7/head</c> and git's status naming it <c>upstream/pr/7</c>.
    /// </summary>
    [Fact]
    public async Task The_refspec_operations_give_the_reports_states()
    {
        using (var repo = await Scenarios.RenamingRefspecAsync())
        {
            var push = await RunAsync(repo, PartG.PushTopicCommand);
            Assert.True(push.ExitCode == 0, $"\"{PartG.PushTopicCommand}\" exited {push.ExitCode}.\n{push.StandardError}");
            Assert.Contains(PartG.PushTopicKey, Lines(push.StandardError));
            Assert.Equal(["refs/heads/main " + PartG.TopicSha], await LinesAsync(repo, "-C", repo.SiblingPath("fork.git"), "for-each-ref", "--format=%(refname) %(objectname)"));
            Assert.Equal(PartG.TopicSha, await RevParseAsync(repo, "refs/remotes/fork/up/main"));
            Assert.Null(await RevParseAsync(repo, "refs/remotes/fork/up/up/main"));
            Assert.Equal(["topic", "fork/up/main", "origin/pr/1", "upstream/pr/7"], PartG.Rs1[0].Labels);
        }

        using (var repo = await Scenarios.RenamingRefspecAsync())
        {
            var create = await RunAsync(repo, PartG.CreatePr1Command);
            Assert.True(create.ExitCode == 0, $"\"{PartG.CreatePr1Command}\" exited {create.ExitCode}.\n{create.StandardError}");
            Assert.Contains(PartG.SwitchedToNewPr1, Lines(create.StandardError));
            Assert.Equal("refs/heads/pr/1", (await repo.GitAsync("symbolic-ref", "HEAD")).StandardOutput.Trim());
            Assert.Equal(1, (await repo.GitAllowingFailureAsync("config", "--get-regexp", @"^branch\.pr/1\.")).ExitCode);
            Assert.Equal(PartG.Pr1StatusBranch, (await LinesAsync(repo, "status", "--porcelain=v2", "--branch")).Where(line => line.StartsWith("# ", StringComparison.Ordinal)));

            var main = await RunAsync(repo, PartG.SwitchMainCommand);
            Assert.True(main.ExitCode == 0, main.StandardError);
            Assert.Contains(PartG.SwitchedToMain, Lines(main.StandardError));
            var pr1 = await RunAsync(repo, PartG.SwitchPr1Command);
            Assert.True(pr1.ExitCode == 0, pr1.StandardError);
            Assert.Contains(PartG.SwitchedToPr1, Lines(pr1.StandardError));
        }

        using (var repo = await Scenarios.RenamingRefspecAsync())
        {
            var stale = await repo.GitAllowingFailureAsync("push", "--progress", $"--force-with-lease=refs/heads/up/main:{PartG.ParserSha}", "fork", "--delete", "refs/heads/up/main");
            Assert.Equal(1, stale.ExitCode);
            Assert.Equal(["refs/heads/main " + PartG.ParserSha], await LinesAsync(repo, "-C", repo.SiblingPath("fork.git"), "for-each-ref", "--format=%(refname) %(objectname)"));
            var delete = await RunAsync(repo, PartG.DeleteMainOnForkCommand);
            Assert.True(delete.ExitCode == 0, $"\"{PartG.DeleteMainOnForkCommand}\" exited {delete.ExitCode}.\n{delete.StandardError}");
            Assert.Contains(PartG.DeletedKey, Lines(delete.StandardError));
            Assert.Empty(await LinesAsync(repo, "-C", repo.SiblingPath("fork.git"), "for-each-ref", "--format=%(refname) %(objectname)"));
            Assert.Null(await RevParseAsync(repo, "refs/remotes/fork/up/main"));
        }

        using (var repo = await Scenarios.RenamingRefspecAsync())
        {
            var create = await RunAsync(repo, PartG.CreatePr7Command);
            Assert.True(create.ExitCode == 0, $"\"{PartG.CreatePr7Command}\" exited {create.ExitCode}.\n{create.StandardError}");
            Assert.Contains(PartG.SwitchedToNewPr7, Lines(create.StandardError));
            if (await IsReportGitAsync())
            {
                Assert.Equal([PartG.Pr7TrackingLine], Lines(create.StandardOutput));
            }

            Assert.Equal(["branch.pr/7.remote upstream", "branch.pr/7.merge refs/pull/7/head"], await LinesAsync(repo, "config", "--get-regexp", @"^branch\.pr/7\."));
            Assert.Equal(PartG.Pr7StatusBranch, (await LinesAsync(repo, "status", "--porcelain=v2", "--branch")).Where(line => line.StartsWith("# ", StringComparison.Ordinal)));
        }
    }

    // ---- The expectations against each other ----

    /// <summary>The menus' heights by 4a's formula: the stash's 145 (4 items, 1 separator), the drops' 180 (5, 2), the remote branches' 380 (12, 4).</summary>
    [Fact]
    public void The_menus_heights_are_4as_formula()
    {
        Assert.Equal((4, 1), PartG.MenuCounts(PartG.StashMenu));
        Assert.Equal(PartG.StashMenuHeight, MenuHeight(4, 1));
        foreach (var drop in new[] { PartG.DropOntoRenamedMenu, PartG.DropOntoAmbiguousMenu })
        {
            Assert.Equal((5, 2), PartG.MenuCounts(drop));
        }

        Assert.Equal(PartG.DropMenuHeight, MenuHeight(5, 2));
        foreach (var menu in new[] { PartG.AmbiguousRefMenu, PartG.RenamedRefMenu, PartG.NotABranchMenu })
        {
            Assert.Equal((12, 4), PartG.MenuCounts(menu));
        }

        Assert.Equal(PartG.RemoteBranchMenuHeight, MenuHeight(12, 4));
        Assert.Equal(PartG.ApplyStashItem, PartG.StashMenu[1]);
        Assert.Equal(PartG.PopStashItem, PartG.StashMenu[2]);
    }

    /// <summary>The graphs derived from one another: IG3 is IG2 with the check mark on tools; RS1 to RS5 keep RS0's rows; IG1 and IG2 have their merges on top.</summary>
    [Fact]
    public void The_graphs_follow_from_one_another()
    {
        Assert.Equal(PartG.Ig2.Length, PartG.Ig3.Length);
        for (var row = 0; row < PartG.Ig2.Length; row++)
        {
            Assert.Equal(PartG.Ig2[row] with { Labels = PartG.Ig3[row].Labels }, PartG.Ig3[row]);
        }

        Assert.Equal(["main"], PartG.Ig3[0].Labels);
        Assert.Equal([Current("tools")], PartG.Ig3[3].Labels);
        Assert.True(PartG.Ig1[0].Merge == true && PartG.Ig2[0].Merge == true);
        Assert.Equal(PartG.SideMergeSha[..7], PartG.Ig1[0].ShortSha);
        Assert.Equal(PartG.SideStartSha[..7], PartG.Ig1[1].ShortSha);
        Assert.Equal(PartG.Ig0.Select(row => row.Message).Where(message => message != PartG.StashListed), PartG.Ig2.Skip(1).Select(row => row.Message).Where(message => message != PartG.StashListed));
        foreach (var graph in new[] { PartG.Rs1, PartG.Rs2, PartG.Rs3, PartG.Rs4, PartG.Rs5 })
        {
            Assert.Equal(PartG.Rs0.Select(row => (row.Message, row.ShortSha, row.Date)), graph.Select(row => (row.Message, row.ShortSha, row.Date)));
            Assert.Empty(graph[2].Labels);
        }

        Assert.Equal(2, PartG.Rs0Drawn[0].Drawn.Length);
        Assert.Equal("Local branches 3", PartG.Ig0Panel[0]);
        Assert.Equal("Remotes 5", PartG.RsRemotesClosed[0]);
        Assert.Equal("Remotes 4", PartG.Rs4Remotes[0]);
        Assert.Equal(PartG.Ig0Panel[4..], PartG.Ig1Panel[5..]);
    }

    /// <summary>The points the checks click are the report's: Add the vendor secret (484, 207), Stop tracking the secret (484, 233); the x is the Message column of three lanes, 30 in.</summary>
    [Fact]
    public void The_click_points_are_the_reports()
    {
        Assert.Equal(PartG.IgRowPointX, WindowLayout.MessageColumnX(PartG.IgLanes) + WindowLayout.MessageClickInset);
        Assert.Equal(PartG.RsRowPointX, WindowLayout.MessageColumnX(PartG.RsLanes) + WindowLayout.MessageClickInset);
        Assert.Equal(new Point(484, 207), PartG.IgRowPoint(PartG.VendorSecretRow));
        Assert.Equal(new Point(484, 233), PartG.IgRowPoint(PartG.StopTrackingRow));
    }

    /// <summary>The texts, commands and git's key lines agree with one another and name the scenarios' commits.</summary>
    [Fact]
    public void The_texts_and_commands_agree()
    {
        Assert.Equal(PartG.UntrackedKey, PartG.Tg1[0]);
        Assert.Equal(PartG.FolderKey, PartG.Tg2[0]);
        Assert.Equal(["\tsecret.env"], PartG.Tg1.Where(line => line.StartsWith('\t')));
        Assert.Equal(["\tbuild"], PartG.Tg2.Where(line => line.StartsWith('\t')));
        Assert.Equal(PartG.MergeMadeKey, PartG.Tg9[0]);
        Assert.Equal("git switch --detach --no-overwrite-ignore 133a796ef1cfa3b4f2dc3237b5342751f85fcc3f", PartG.DetachVendorSecretCommand);
        Assert.Equal(PartG.CheckOutVendorSecret, "Check out " + PartG.VendorSecretSha[..7]);
        Assert.Equal(PartG.CherryPickVendorSecret, "Cherry-pick " + PartG.VendorSecretSha[..7]);
        Assert.Equal(PartG.RevertStopTracking, "Revert " + PartG.StopTrackingSha[..7]);
        Assert.Equal("1 ignored file is in the way: vendor tracks a file of the same name. Move it away first.", PartG.IgnoredFileInTheWay("vendor"));
        Assert.Equal("1 ignored file is in the way of the merge: secret.env. Move it away first.", PartG.IgnoredFileInTheWayOf("merge"));
        Assert.Equal("Check out vendor failed", PartG.Failed(PartG.CheckOutVendor));
        Assert.StartsWith(PartG.R2OriginPr1, PartG.CreatedPr1WithoutUpstream[(PartG.CreatedPr1WithoutUpstream.IndexOf(": ", StringComparison.Ordinal) + 2)..], StringComparison.Ordinal);
        Assert.EndsWith(PartG.R2OriginPr1 + ".", PartG.PushWouldStopFetch, StringComparison.Ordinal);
        Assert.Equal("git push --progress --force-with-lease=refs/heads/main:8394a5e2e769c4601161cbce8c239ff2aa246eb9 fork --delete refs/heads/main", PartG.DeleteMainOnForkCommand);
        Assert.Equal(PartG.PushTopicItem, PartG.PushTopicToRenamed);
        Assert.Equal("Pull from upstream/pr/7 (Ctrl+Shift+L)", PartG.PullFromUpstreamPr7);
        Assert.NotEqual(PartG.PullFromFullRef, PartG.PullFromUpstreamPr7);
        Assert.All(PartG.WritePrefixes, prefix => Assert.StartsWith("git ", prefix, StringComparison.Ordinal));
        Assert.DoesNotContain(PartG.WritePrefixes, prefix => PartG.Tr1.Any(read => read.Command.StartsWith(prefix, StringComparison.Ordinal)));
    }

    /// <summary>The deletion's backup is the fixed backup clock's first name, of the kind "remote-branch", fork's main (10.4 #9).</summary>
    [Fact]
    public void The_deletions_backup_is_the_first_of_the_fixed_clock()
    {
        var backup = BackupRef.Parse(PartG.DeletionBackup);
        Assert.Equal(("remote-branch", new DateTimeOffset(2026, 1, 2, 9, 0, 1, TimeSpan.Zero), "fork/main"), (backup.Kind, backup.Time, backup.Name));
    }

    // ---- Helpers ----

    /// <summary>
    /// Runs a command as the call log renders it (<c>git …</c>, split at its spaces: part G's commands
    /// have no argument with a space) in <paramref name="repo"/> as the app runs its reads: with
    /// <c>GIT_OPTIONAL_LOCKS=0</c>, <paramref name="input"/> on its standard input, and without
    /// <c>GIT_LITERAL_PATHSPECS</c> when <paramref name="literalPathspecs"/> is false (check-ignore, D74).
    /// </summary>
    private static Task<GitResult> RunAsync(TempRepo repo, string commandText, string? input = null, bool literalPathspecs = true)
    {
        Assert.StartsWith("git ", commandText, StringComparison.Ordinal);
        var extra = new Dictionary<string, string?>(repo.Environment) { ["GIT_OPTIONAL_LOCKS"] = "0" };
        if (!literalPathspecs)
        {
            extra["GIT_LITERAL_PATHSPECS"] = null;
        }
        else
        {
            extra["GIT_LITERAL_PATHSPECS"] = "1";
        }

        var command = new GitCommand(commandText["git ".Length..].Split(' ')) { WorkingDirectory = repo.Path, Environment = extra, StandardInput = input };
        return repo.Runner.RunAsync(command);
    }

    /// <summary>A read's result against the report's: its exit code and its output (<see cref="PartG.Normalised"/>).</summary>
    private static void AssertRead(GitResult result, ExpectedRead expected)
    {
        Assert.True(result.ExitCode == expected.ExitCode, $"\"{expected.Command}\" exited {result.ExitCode}; the report says {expected.ExitCode}.\n{result.StandardError}");
        Assert.Equal(expected.Output, PartG.Normalised(result.StandardOutput));
    }

    /// <summary>The pull's fetch (TG12): exit 0, and on Git 2.36 nothing on either stream.</summary>
    private static async Task AssertQuietFetchAsync(TempRepo repo)
    {
        var fetch = await RunAsync(repo, PartG.FetchOriginCommand);
        Assert.True(fetch.ExitCode == 0, $"\"{PartG.FetchOriginCommand}\" exited {fetch.ExitCode}.\n{fetch.StandardError}");
        if (await IsReportGitAsync())
        {
            Assert.Equal((string.Empty, string.Empty), (fetch.StandardOutput, fetch.StandardError));
        }
    }

    /// <summary>Plain git's output as lines, line ends as line feeds (none for an empty output).</summary>
    private static async Task<string[]> LinesAsync(TempRepo repo, params string[] arguments)
    {
        var text = PartG.Normalised((await repo.GitAsync(arguments)).StandardOutput);
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary>The lines of a stream, split at every carriage return and line feed, without trailing spaces and empty lines.</summary>
    private static List<string> Lines(string output) =>
        [.. output.Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];

    /// <summary>What a revision names, or null.</summary>
    private static async Task<string?> RevParseAsync(TempRepo repo, string revision)
    {
        var result = await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", revision);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }
}
