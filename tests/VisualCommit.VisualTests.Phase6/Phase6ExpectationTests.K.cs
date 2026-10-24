using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

/// <summary>
/// Part K's expectations (Phase6Expectations.K.cs) held against plain git on fresh copies of the
/// scenarios "ops", "graph", "conflicts" and "Web", and against each other: OPS0's commits, refs
/// and stash; the palette's checkout (K-OPSa); the commit "Keys only" with D72's dates, its diff,
/// history and blame, and its undo and redo (K-OPSk); the pop and the new stash (K-OPSp, K-OPSs)
/// and the search for <c>alpha</c>; K12's pushes, fetch and pull that change nothing; Fetch all's
/// remote-tracking refs (K-WEBf), "Tools"' rows (K-TOOLS) and docs' graph (K-DOCS); GC-M, FM and R1;
/// GR0. No app: a scenario that drifted, or an expectation copied wrong, fails here, before a
/// window opens. Git's own texts are asserted word for word on the report's git, 2.36 only; on
/// every git their key lines, exit codes and states (part K's "Git's own texts").
/// </summary>
public sealed class Phase6ExpectationTestsK
{
    // ---- "ops" ----

    /// <summary>OPS0: each row's SHA, subject and date, the refs its labels name, the stash, the upstreams and the status.</summary>
    [Fact]
    public async Task Ops_is_OPS0_as_the_report_gives_it()
    {
        using var repo = await Scenarios.OperationsAsync();

        await AssertRowsAsync(repo, PartK.Ops0);
        Assert.Equal(PartK.StashSha, await RevParseAsync(repo, "refs/stash"));
        Assert.Equal(PartK.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal(PartK.AlphaTwoSha, await RevParseAsync(repo, "refs/heads/feature/alpha"));
        Assert.Equal([$"stash@{{0}}: {PartK.StashMessage}"], await LinesAsync(repo, "stash", "list", "--format=%gd: %gs"));
        Assert.Equal(PartK.Ops0LocalBranches, await LinesAsync(repo, "for-each-ref", "--format=%(refname)", "refs/heads"));
        Assert.Equal(
            ["feature/alpha origin/feature/alpha", "feature/conflict ", "feature/ff ", "main origin/main"],
            await LinesAsync(repo, "for-each-ref", "--format=%(refname:short) %(upstream:short)", "refs/heads"));
        Assert.Equal([PartK.MainBranchLine], await LinesAsync(repo, "status", "--porcelain=v1", "--branch"));
        Assert.Equal(["origin"], await LinesAsync(repo, "remote"));

        // The labels: each row's refs as the graph draws them (the current branch's " ✓" aside).
        var refs = await LinesAsync(repo, "for-each-ref", "--format=%(refname:short) %(*objectname)%(objectname)", "refs/heads", "refs/remotes", "refs/tags");
        foreach (var row in PartK.Ops0.Where(row => !row.Stash))
        {
            var sha = await RevParseAsync(repo, row.ShortSha!);
            foreach (var label in row.Labels)
            {
                var name = label.Replace(CurrentMark, string.Empty, StringComparison.Ordinal);
                Assert.Contains(refs, line => line.StartsWith(name + " " + sha, StringComparison.Ordinal));
            }
        }

        Assert.Equal("tag", (await LinesAsync(repo, "cat-file", "-t", "refs/tags/v1.0")).Single());
    }

    /// <summary>K10 step 4's checkout (K-OPSa): the app's command exits 0 with git's two lines, and leaves HEAD on feature/alpha, clean, the stash kept.</summary>
    [Fact]
    public async Task The_palettes_checkout_of_feature_alpha_gives_K_OPSa()
    {
        using var repo = await Scenarios.OperationsAsync();

        var result = await RunAsync(repo, Arguments(PartK.SwitchFeatureAlpha));
        Assert.True(result.ExitCode == 0, $"The checkout exited {result.ExitCode}.\n{result.StandardError}");
        Assert.Contains(PartK.SwitchKey, Lines(result.StandardError));
        if (await IsReportGitAsync())
        {
            Assert.Equal([PartK.SwitchOutput], Lines(result.StandardOutput));
            Assert.Equal([PartK.SwitchKey], Lines(result.StandardError));
        }

        Assert.Equal("refs/heads/feature/alpha", (await LinesAsync(repo, "symbolic-ref", "HEAD")).Single());
        Assert.Equal(PartK.AlphaTwoSha, await RevParseAsync(repo, "HEAD"));
        Assert.Equal([PartK.AlphaBranchLine], await LinesAsync(repo, "status", "--porcelain=v1", "--branch"));
        Assert.Equal(PartK.StashSha, await RevParseAsync(repo, "refs/stash"));
        await AssertRowsAsync(repo, PartK.OpsA);
    }

    /// <summary>
    /// K12 part A (K-OPSk): Stage all of <c>src/core.txt</c>, the commit "Keys only" as the app makes
    /// it (its summary on standard input, D72's dates, the copy's identity): <c>9522dae</c>, its tree
    /// and parent, git's lines; its diff, the file's history and blame; then the undo and the redo by
    /// <c>update-ref --stdin</c> with the report's inputs and HEAD's reflog after them.
    /// </summary>
    [Fact]
    public async Task The_commit_Keys_only_its_pages_and_its_undo_and_redo_are_K_OPSk()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile(PartK.CoreFile, PartK.CoreKeys);
        Assert.Equal([PartK.MainBranchLine, PartK.CoreUnstaged], await LinesAsync(repo, "status", "--porcelain=v1", "--branch"));

        Assert.Equal(0, (await RunAsync(repo, Arguments(PartK.AddCore))).ExitCode);
        Assert.Equal([PartK.MainBranchLine, PartK.CoreStaged], await LinesAsync(repo, "status", "--porcelain=v1", "--branch"));
        var commit = await RunWithInputAsync(repo, Arguments(PartK.CommitCommand), PartK.KeysOnly, D72Dates);
        Assert.True(commit.ExitCode == 0, $"The commit exited {commit.ExitCode}.\n{commit.StandardError}");
        Assert.Contains(PartK.CommitKeyLine, Lines(commit.StandardOutput));
        if (await IsReportGitAsync())
        {
            Assert.Equal([PartK.CommitKeyLine, PartK.CommitSummaryLine.TrimEnd()], commit.StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0));
        }

        Assert.Equal(PartK.KeysOnlySha, await RevParseAsync(repo, "HEAD"));
        Assert.Equal(PartK.KeysOnlyTree, await RevParseAsync(repo, "HEAD^{tree}"));
        Assert.Equal(PartK.MainWorkSha, await RevParseAsync(repo, "HEAD^"));
        Assert.Equal([$"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}> {TempRepo.AuthorName} <{TempRepo.AuthorEmail}>"], await LinesAsync(repo, "log", "-1", "--format=%an <%ae> %cn <%ce>"));
        Assert.Equal(["Keys only"], await LinesAsync(repo, "log", "-1", "--format=%B"));
        Assert.Equal([PartK.AheadOneBranchLine], await LinesAsync(repo, "status", "--porcelain=v1", "--branch"));
        Assert.Equal("commit: Keys only", (await LinesAsync(repo, "log", "-g", "-1", "--format=%gs", "HEAD")).Single());
        await AssertRowsAsync(repo, PartK.OpsK);

        // The diff, the file's history and its blame.
        var diff = (await RunAsync(repo, "show", "--format=", "HEAD", "--", PartK.CoreFile)).StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var hunk = diff.FindIndex(line => line.StartsWith("@@", StringComparison.Ordinal));
        Assert.StartsWith(PartK.CoreDiff[0], diff[hunk], StringComparison.Ordinal);
        Assert.Equal(PartK.CoreDiff[1..], diff.Skip(hunk + 1).Where(line => line.Length > 0));
        Assert.Equal(PartK.CoreHistory.Select(entry => $"{entry.ShortSha} {entry.Subject}"), await LinesAsync(repo, "log", "--follow", "--format=%h %s", "--abbrev=7", "HEAD", "--", PartK.CoreFile));
        // Whole SHAs: how many digits --abbrev gives a blame line changed after 2.36 (2.56 prints
        // the 6 asked for, 2.36 one more; CI run 39950853606).
        var blame = await LinesAsync(repo, "blame", "-s", "-l", "HEAD", "--", PartK.CoreFile);
        Assert.Equal(PartK.CoreBlame.Length, blame.Count);
        foreach (var (line, (text, shortSha, _)) in blame.Zip(PartK.CoreBlame))
        {
            Assert.StartsWith(shortSha, line, StringComparison.Ordinal);
            Assert.EndsWith(") " + text, line, StringComparison.Ordinal);
        }

        Assert.Equal(PartK.CoreBlame.Select(entry => entry.Subject), await Task.WhenAll(PartK.CoreBlame.Select(async entry => (await LinesAsync(repo, "log", "-1", "--format=%s", entry.ShortSha)).Single())));
        Assert.Equal(PartK.BlameAtKeysOnly, $"at {PartK.KeysOnlySha[..7]} {PartK.KeysOnly}");

        // The undo, then the redo, as the app runs them.
        var undo = await RunWithInputAsync(repo, ["update-ref", "-m", "VisualCommit: undo commit \"Keys only\"", "--stdin"], PartK.UndoInput + "\n", D72Dates);
        Assert.True(undo.ExitCode == 0, $"The undo exited {undo.ExitCode}.\n{undo.StandardError}");
        Assert.Equal(PartK.MainWorkSha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal([PartK.MainBranchLine, PartK.CoreStaged], await LinesAsync(repo, "status", "--porcelain=v1", "--branch"));
        Assert.Equal(0, (await repo.GitAllowingFailureAsync("cat-file", "-e", PartK.KeysOnlySha)).ExitCode);
        var redo = await RunWithInputAsync(repo, ["update-ref", "-m", "VisualCommit: redo commit \"Keys only\"", "--stdin"], PartK.RedoInput + "\n", D72Dates);
        Assert.True(redo.ExitCode == 0, $"The redo exited {redo.ExitCode}.\n{redo.StandardError}");
        Assert.Equal(PartK.KeysOnlySha, await RevParseAsync(repo, "refs/heads/main"));
        Assert.Equal([PartK.AheadOneBranchLine], await LinesAsync(repo, "status", "--porcelain=v1", "--branch"));
        Assert.Equal(PartK.RedoReflog, (await LinesAsync(repo, "log", "-g", "--format=%gs", "HEAD")).Take(3));
    }

    /// <summary>
    /// K12 part B: the pushes, the fetch and the pull's merge change nothing (exit 0, git's key
    /// lines); the pop of OPS0's stash (K-OPSp, its output's last line), then the stash "Experiment
    /// again" with D72's dates (K-OPSs: <c>9b4f935</c> and its parents); the search for
    /// <c>alpha</c> over K-OPSs' rows matches Alpha two and Alpha one only.
    /// </summary>
    [Fact]
    public async Task K12_part_Bs_remotes_pop_stash_and_search_are_the_reports()
    {
        using var repo = await Scenarios.OperationsAsync();
        var refs = await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)");

        var push = await RunAsync(repo, Arguments(PartK.PushMainCommand));
        Assert.True(push.ExitCode == 0, $"The push exited {push.ExitCode}.\n{push.StandardError}");
        Assert.Contains(PartK.EverythingUpToDate, Lines(push.StandardError));
        var fetch = await RunAsync(repo, Arguments(PartK.FetchOrigin));
        Assert.True(fetch.ExitCode == 0, $"The fetch exited {fetch.ExitCode}.\n{fetch.StandardError}");
        if (await IsReportGitAsync())
        {
            Assert.Equal((string.Empty, string.Empty), (fetch.StandardOutput, fetch.StandardError));
        }

        var merge = await RunAsync(repo, Arguments(PartK.MergeFetchHead));
        Assert.True(merge.ExitCode == 0, $"The merge exited {merge.ExitCode}.\n{merge.StandardError}");
        Assert.Contains(PartK.AlreadyUpToDate, Lines(merge.StandardOutput));
        Assert.Equal(refs.Where(line => !line.StartsWith("refs/remotes/origin/HEAD", StringComparison.Ordinal)), (await LinesAsync(repo, "for-each-ref", "--format=%(refname) %(objectname)")).Where(line => !line.StartsWith("refs/remotes/origin/HEAD", StringComparison.Ordinal)));
        Assert.Equal(PartK.MainWorkSha, (await LinesAsync(repo, "--git-dir", repo.SiblingPath("origin.git"), "rev-parse", "refs/heads/main")).Single());

        // The pop (K-OPSp).
        var pop = await RunAsync(repo, "stash", "pop", "stash@{0}");
        Assert.True(pop.ExitCode == 0, $"The pop exited {pop.ExitCode}.\n{pop.StandardError}");
        Assert.Equal(PartK.DroppedStash, Lines(pop.StandardOutput)[^1]);
        Assert.Equal([PartK.MainBranchLine, PartK.ReadmeUnstaged], await LinesAsync(repo, "status", "--porcelain=v1", "--branch"));
        Assert.Equal(PartK.ReadmeExperiment, File.ReadAllText(Path.Combine(repo.Path, "README.md")).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Empty(await LinesAsync(repo, "stash", "list"));
        await AssertRowsAsync(repo, PartK.OpsP);

        // The stash (K-OPSs).
        var stash = await RunAsync(repo, ["stash", "push", "--include-untracked", "--message", PartK.StashAgainTyped], D72Dates);
        Assert.True(stash.ExitCode == 0, $"The stash exited {stash.ExitCode}.\n{stash.StandardError}");
        Assert.Contains(PartK.SavedStash, Lines(stash.StandardOutput));
        Assert.Equal(PartK.StashAgainSha, await RevParseAsync(repo, "refs/stash"));
        Assert.Equal(string.Join(' ', PartK.StashAgainParents), (await LinesAsync(repo, "log", "-1", "--format=%P", "refs/stash")).Single());
        Assert.Equal([$"stash@{{0}}: {PartK.StashAgainMessage}"], await LinesAsync(repo, "stash", "list", "--format=%gd: %gs"));
        Assert.Equal([PartK.MainBranchLine], await LinesAsync(repo, "status", "--porcelain=v1", "--branch"));
        await AssertRowsAsync(repo, PartK.OpsS);

        // The search for alpha over K-OPSs' rows (phase 5 design 3.2).
        var shas = await Task.WhenAll(PartK.OpsS.Select(async row => await RevParseAsync(repo, row.ShortSha!)));
        var found = await RunWithInputAsync(repo, ["log", "--no-walk=unsorted", "--stdin", "--format=%H", "-F", "--regexp-ignore-case", "--grep=" + PartK.SearchQuery], string.Join('\n', shas) + "\n", null);
        Assert.Equal(PartK.AlphaMatches.Select(row => shas[row]), Lines(found.StandardOutput));
    }

    // ---- "Web" and "Tools" ----

    /// <summary>K-WEBf: each of the eight repositories' <c>git fetch --progress origin</c> exits 0; docs', mobile's and design-system's origin/main move and their counts become the report's; notes has no remote.</summary>
    [Fact]
    public async Task Web_after_Fetch_all_is_K_WEBf()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        foreach (var name in PartK.Fetched)
        {
            using var repo = copy.Repo(name);
            var fetch = await RunAsync(repo, Arguments(PartK.FetchOrigin));
            Assert.True(fetch.ExitCode == 0, $"{name}'s fetch exited {fetch.ExitCode}.\n{fetch.StandardError}");
        }

        foreach (var (name, sha) in PartK.FetchedOriginMain)
        {
            using var repo = copy.Repo(name);
            Assert.Equal(sha, await RevParseAsync(repo, "refs/remotes/origin/main"));
        }

        var rows = PartK.WebFetched(copy.PathOf("old-site")).ToDictionary(row => row.Name);
        foreach (var name in new[] { "api", "web-app", "docs", "mobile", "design-system", "billing" })
        {
            using var repo = copy.Repo(name);
            var counts = (await LinesAsync(repo, "rev-list", "--left-right", "--count", "main...origin/main")).Single().Split('\t');
            var cell = rows[name].AheadBehind;
            var expected = cell.Name is { } named ? named : cell.Text;
            Assert.Equal(expected, Described(int.Parse(counts[0], CultureInfo.InvariantCulture), int.Parse(counts[1], CultureInfo.InvariantCulture)));
            Assert.Equal(PartK.FetchedFromAllRemotes, rows[name].Operation.Text);
        }

        using (var notes = copy.Repo("notes"))
        {
            Assert.Empty(await LinesAsync(notes, "remote"));
        }

        Assert.Equal(PartK.NoRemotesText, rows["notes"].Operation.Text);
        Assert.False(Directory.Exists(copy.PathOf("old-site")));
        Assert.Equal(PartK.Web(copy.PathOf("old-site")).Select(row => row.Name), copy.Names);
        Assert.Equal(PartK.MovedOrder.Order(StringComparer.Ordinal), copy.Names.Order(StringComparer.Ordinal));
        Assert.Equal(PartK.RemovedOrder, PartK.MovedOrder.Where(name => name != "old-site"));
    }

    /// <summary>K-TOOLS: infra detached at <c>09d320a</c>, scratch on <c>experiment</c> without an upstream, both clean; K-DOCS: docs' four commits with their labels.</summary>
    [Fact]
    public async Task Tools_rows_and_docs_graph_are_K_TOOLS_and_K_DOCS()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using (var infra = copy.Repo("infra"))
        {
            Assert.Equal(["# branch.head (detached)"], (await LinesAsync(infra, "status", "--porcelain=v2", "--branch")).Where(line => !line.StartsWith("# branch.oid ", StringComparison.Ordinal)));
            Assert.Equal("Detached at " + (await RevParseAsync(infra, "HEAD"))![..7], PartK.ToolsFetched[0].Branch);
            Assert.Equal(["origin"], await LinesAsync(infra, "remote"));
        }

        using (var scratch = copy.Repo("scratch"))
        {
            Assert.Equal(["# branch.head experiment"], (await LinesAsync(scratch, "status", "--porcelain=v2", "--branch")).Where(line => !line.StartsWith("# branch.oid ", StringComparison.Ordinal)));
            Assert.Equal("experiment", PartK.ToolsFetched[1].Branch);
            Assert.Equal(["origin"], await LinesAsync(scratch, "remote"));
        }

        using var docs = copy.Repo(PartK.Docs);
        await AssertRowsAsync(docs, PartK.DocsGraph);
        Assert.Equal("0\t3", (await LinesAsync(docs, "rev-list", "--left-right", "--count", "main...origin/main")).Single());
        using var webApp = copy.Repo(PartK.WebApp);
        await AssertRowsAsync(webApp, PartK.WebAppGraph);
    }

    // ---- "conflicts" and "graph" ----

    /// <summary>GC-M: "conflicts" stopped in its merge: notes.txt unmerged with FM on disk (<c>cdd7b39</c>), MERGE_HEAD Theirs edits; GC-M's rows; R1 is FM with its first block replaced by ours.</summary>
    [Fact]
    public async Task Conflicts_stopped_in_a_merge_is_GC_M_with_FM_and_R1()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        Assert.Equal([PartK.ConflictedNotes], await LinesAsync(repo, "status", "--porcelain"));
        Assert.StartsWith(PartK.NotesBlobAtStop, (await LinesAsync(repo, "hash-object", PartK.NotesFile)).Single(), StringComparison.Ordinal);
        Assert.Equal(PartK.TheirsEditsSha, await RevParseAsync(repo, "MERGE_HEAD"));
        Assert.Equal(PartK.Fm, File.ReadAllText(Path.Combine(repo.Path, PartK.NotesFile)).Replace("\r\n", "\n", StringComparison.Ordinal));
        await AssertRowsAsync(repo, PartK.Gcm);

        // R1: the first block resolved with ours, the second kept.
        var lines = PartK.Fm.TrimEnd('\n').Split('\n').ToList();
        var start = lines.FindIndex(line => line.StartsWith("<<<<<<< ", StringComparison.Ordinal));
        var middle = lines.FindIndex(start, line => line == "=======");
        var end = lines.FindIndex(middle, line => line.StartsWith(">>>>>>> ", StringComparison.Ordinal));
        var r1 = lines.Take(start).Concat(lines.Skip(start + 1).Take(middle - start - 1)).Concat(lines.Skip(end + 1));
        Assert.Equal(PartK.R1, string.Join('\n', r1) + "\n");
        Assert.Equal(20, lines.Count);
        Assert.Equal(16, PartK.R1.TrimEnd('\n').Split('\n').Length);
    }

    /// <summary>GR0: "graph"'s thirteen commits (the branches', remote branches', tags' and the stash's) are the report's subjects.</summary>
    [Fact]
    public async Task Graph_is_GR0()
    {
        using var repo = await Scenarios.GraphAsync();
        var subjects = await LinesAsync(repo, "log", "--format=%s", "--branches", "--remotes", "--tags");
        var stash = (await LinesAsync(repo, "log", "-1", "--format=%s", "refs/stash")).Single();
        Assert.Equal(PartK.Gr0Subjects.Order(StringComparer.Ordinal), subjects.Append(stash).Order(StringComparer.Ordinal));
        Assert.Equal(PartK.Gr0Subjects[0], stash);
        Assert.Equal("Add settings page", (await LinesAsync(repo, "log", "-1", "--format=%s", "refs/heads/main")).Single());
    }

    // ---- The texts ----

    /// <summary>The texts, counts and geometry agree with one another and with design 5.3 and 4a.</summary>
    [Fact]
    public void The_texts_and_counts_agree()
    {
        // The palette's lists (KD1, KD2, KD3).
        Assert.Equal(PartK.OpsCommandCount, PartK.OpsCommands.Length);
        Assert.Equal(PartK.DashboardCommandCount, PartK.DashboardCommands.Length);
        Assert.Equal(PartK.OpsCommands[3], PartK.AfterTheRuns[0]);
        Assert.Equal(
            PartK.OpsCommands.Where(entry => entry.Label != "Create branch…").Take(11),
            PartK.AfterTheRuns.Skip(1).Select(entry => entry.Label == "Undo checkout of feature/alpha" ? PartK.OpsCommands[6] : entry));
        Assert.Equal(10, PartK.CreateBranch.Length);
        Assert.Equal((84, 112, 336, 392, 392), (PartK.CardHeight(1), PartK.CardHeight(2), PartK.CardHeight(10), PartK.CardHeight(12), PartK.CardHeight(33)));
        Assert.Equal((157, 185, 465), (PartK.RowCentreY(0), PartK.RowCentreY(1), PartK.RowCentreY(11)));
        Assert.Equal(PartK.WheelRow, PartK.FirstRowInViewAtEnd + (int)((PartK.WheelPoint.Y - PartK.ListTop) / PartK.RowHeight));
        Assert.Equal([11, 22, 32], PartK.PageDownRows);
        Assert.Equal(PartK.OpsCommandCount - 1, PartK.FirstRowInViewAtEnd + PartK.RowsInView - 1);
        Assert.All(PartK.OpsCommands.Concat(PartK.DashboardCommands).Concat(PartK.WebAppCommands), entry => Assert.True(entry.Key is null || !entry.Key.Contains("Cmd", StringComparison.Ordinal)));
        Assert.Equal("Delete main on origin… · origin/main (danger)", PartK.DeleteMain[0].ToString());
        Assert.Equal("Delete main… · main (disabled: \"You cannot delete the branch you are on\")", PartK.DeleteMain[1].ToString());

        // The menus (4a's height; 6.7's widths).
        Assert.Equal((8, 2), PartK.MenuCounts(PartK.EmptyAreaMenu));
        Assert.Equal((8, 2), PartK.MenuCounts(PartK.WorkingChangesMenu));
        Assert.Equal((14, 5), PartK.MenuCounts(PartK.MainMenu));
        Assert.Equal(PartK.EightItemsTwoSeparators, MenuHeight(8, 2));
        Assert.Equal(PartK.FourteenItemsFiveSeparators, MenuHeight(14, 5));
        Assert.Equal(239, Math.Round(4 + 10 + 114.4 + 24 + 72.4 + 10 + 4));
        Assert.Equal(234, Math.Round(4 + 10 + 109.1 + 24 + 73.1 + 10 + 4));
        Assert.Equal(288, Math.Round(4 + 10 + 162.9 + 24 + 73.3 + 10 + 4));
        Assert.Equal(("Stash changes…", "Ctrl+Shift+S"), (PartK.LabelOf(PartK.EmptyAreaMenu[6]), PartK.KeyOf(PartK.EmptyAreaMenu[6])));
        Assert.Equal(("Force push main…", (string?)null), (PartK.LabelOf(PartK.MainMenu[5]), PartK.KeyOf(PartK.MainMenu[5])));

        // The texts and commands.
        Assert.Equal(("72", "63", "53"), (PartK.Counter(string.Empty), PartK.Counter(PartK.KeysOnly), PartK.Counter(PartK.NoteMoreCoreWork)));
        Assert.Equal("Undo commit \"Keys only\" (Ctrl+Z)", PartK.UndoToolTip(PartK.CommitKeysOnly));
        Assert.Equal("Redo commit \"Keys only\" (Ctrl+Y)", PartK.RedoToolTip(PartK.CommitKeysOnly));
        Assert.Equal(PartK.UndoCommitKeysOnly, "Undo " + PartK.CommitKeysOnly);
        Assert.Equal(PartK.RedoCommitKeysOnly, "Redo " + PartK.CommitKeysOnly);
        Assert.Equal("ops: Command palette ran: Check out feature/alpha (feature/alpha)", PartK.PaletteRan(PartK.Ops, PartK.CheckOutFeatureAlphaLabel, "feature/alpha"));
        Assert.Equal("Command palette opened", PartK.PaletteOpened(null));
        Assert.Equal("ops: Context menu opened: main by keyboard", PartK.MenuOpened(PartK.Ops, "main", PartK.ByKeyboard));
        Assert.Equal("Web: read 10 repositories in ", PartK.ReadPassStart(PartK.Workspace, 10));
        Assert.Equal("Tools: read 2 repositories in ", PartK.ReadPassStart(PartK.Tools, 2));
        Assert.Equal(PartK.CommitKeyLine, "[main " + PartK.KeysOnlySha[..7] + "] Keys only");
        Assert.EndsWith(PartK.StashSha + ")", PartK.DroppedStash, StringComparison.Ordinal);
        Assert.Equal("git update-ref -m \"VisualCommit: undo commit \\\"Keys only\\\"\" --stdin", new GitCommand("update-ref", "-m", "VisualCommit: undo commit \"Keys only\"", "--stdin").DisplayText);
        Assert.Equal(PartK.StashPushCommand, new GitCommand("stash", "push", "--include-untracked", "--message", PartK.StashAgainTyped).DisplayText);
        Assert.True(PartK.IsConfigWrite("git config --global user.name Alice"));
        Assert.False(PartK.IsConfigWrite("git config --null --get-regexp ^remote\\."));

        // The pictures (KD18).
        Assert.Equal(17, PartK.Pictures.Length);
        Assert.Equal(PartK.Pictures.Length, PartK.Pictures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PartK.Pictures, picture => Assert.Matches(@"^k1[0-3][a-z]-[a-z0-9]+(-[a-z0-9]+)*$", picture));
    }

    /// <summary>The platform's words (design 6.4): a Mac's Cmd for the command modifier, Control kept for the tab keys, Option for Alt, Cmd+R and Cmd+Shift+Z for the shown Refresh and Redo; the Windows and Linux words unchanged.</summary>
    [Theory]
    [InlineData("Ctrl+Shift+P", "Cmd+Shift+P")]
    [InlineData("Ctrl+,", "Cmd+,")]
    [InlineData("Ctrl+Tab", "Ctrl+Tab")]
    [InlineData("Ctrl+Shift+Tab", "Ctrl+Shift+Tab")]
    [InlineData("Ctrl+PageDown", "Ctrl+PageDown")]
    [InlineData("Alt+Down", "Option+Down")]
    [InlineData("F5", "Cmd+R")]
    [InlineData("Ctrl+Y", "Cmd+Shift+Z")]
    [InlineData("Esc", "Esc")]
    [InlineData("Undo checkout of feature/alpha (Ctrl+Z)", "Undo checkout of feature/alpha (Cmd+Z)")]
    [InlineData("Read the repositories of Web again (F5)", "Read the repositories of Web again (Cmd+R)")]
    [InlineData("Redo commit \"Keys only\" (Ctrl+Y)", "Redo commit \"Keys only\" (Cmd+Shift+Z)")]
    [InlineData("Next conflict (Alt+Down)", "Next conflict (Option+Down)")]
    [InlineData("Stash changes… [Ctrl+Shift+S] (disabled: \"There are no changes to stash\")", "Stash changes… [Cmd+Shift+S] (disabled: \"There are no changes to stash\")")]
    public void OnPlatform_gives_a_Macs_words_and_keeps_the_others(string windows, string mac)
    {
        Assert.Equal(mac, PartK.OnPlatform(windows, commandIsMeta: true));
        Assert.Equal(windows, PartK.OnPlatform(windows, commandIsMeta: false));
    }

    // ---- Helpers ----

    /// <summary>D72's dates for what the app writes (2026-01-02 09:00 UTC).</summary>
    private static readonly IReadOnlyDictionary<string, string?> D72Dates = new Dictionary<string, string?>
    {
        ["GIT_AUTHOR_DATE"] = "2026-01-02T09:00:00+00:00",
        ["GIT_COMMITTER_DATE"] = "2026-01-02T09:00:00+00:00",
    };

    /// <summary>A command's arguments from its text as the call log renders it (none of these has an argument with a space).</summary>
    private static string[] Arguments(string commandText)
    {
        Assert.StartsWith("git ", commandText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"", commandText, StringComparison.Ordinal);
        return commandText["git ".Length..].Split(' ');
    }

    /// <summary>Runs git in <paramref name="repo"/> with <c>GIT_LITERAL_PATHSPECS=1</c> (and <paramref name="extra"/>), its result whatever its exit.</summary>
    private static Task<GitResult> RunAsync(TempRepo repo, string[] arguments, IReadOnlyDictionary<string, string?>? extra = null)
    {
        var environment = new Dictionary<string, string?> { ["GIT_LITERAL_PATHSPECS"] = "1" };
        foreach (var (key, value) in extra ?? new Dictionary<string, string?>())
        {
            environment[key] = value;
        }

        return repo.GitWithEnvironmentAllowingFailureAsync(environment, arguments);
    }

    private static Task<GitResult> RunAsync(TempRepo repo, params string[] arguments) => RunAsync(repo, arguments, null);

    /// <summary>Runs git in <paramref name="repo"/> with <paramref name="input"/> on its standard input, the copy's environment and <paramref name="extra"/>.</summary>
    private static Task<GitResult> RunWithInputAsync(TempRepo repo, string[] arguments, string input, IReadOnlyDictionary<string, string?>? extra)
    {
        var environment = new Dictionary<string, string?>(repo.Environment) { ["GIT_LITERAL_PATHSPECS"] = "1" };
        foreach (var (key, value) in extra ?? new Dictionary<string, string?>())
        {
            environment[key] = value;
        }

        return repo.Runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = repo.Path, Environment = environment, StandardInput = input });
    }

    /// <summary>Plain git's standard output as lines (exit 0 asserted), without empty lines.</summary>
    private static async Task<List<string>> LinesAsync(TempRepo repo, params string[] arguments)
    {
        var result = await repo.GitAllowingFailureAsync(arguments);
        Assert.True(result.ExitCode == 0, $"git {string.Join(' ', arguments)} exited {result.ExitCode}.\n{result.StandardError}");
        return [.. result.StandardOutput.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];
    }

    /// <summary>The lines of a stream, split at every carriage return and line feed, without trailing spaces and empty lines.</summary>
    private static List<string> Lines(string output) =>
        [.. output.Split(['\r', '\n']).Select(line => line.TrimEnd()).Where(line => line.Length > 0)];

    /// <summary>What a revision names, or null.</summary>
    private static async Task<string?> RevParseAsync(TempRepo repo, string revision)
    {
        var result = await repo.GitAllowingFailureAsync("rev-parse", "--verify", "--quiet", revision + "^{}");
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }

    /// <summary>
    /// Each row of a graph names its commit: its short SHA's subject is the row's message and its
    /// author date (UTC) the row's date where given; a stash row is the stash's commit.
    /// </summary>
    private static async Task AssertRowsAsync(TempRepo repo, IReadOnlyList<CommitRow> rows)
    {
        foreach (var row in rows)
        {
            var line = (await LinesAsync(repo, "log", "-1", "--format=%s%x1f%aI", row.ShortSha!)).Single().Split('\u001f');
            Assert.Equal(row.Message, line[0]);
            if (row.Date is { } date)
            {
                Assert.Equal(date, GitIsoDates.Parse(line[1]).UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            }
        }
    }

    /// <summary>An Ahead / behind cell's automation name for <paramref name="ahead"/> and <paramref name="behind"/> (2.6), or <c>Up to date</c>.</summary>
    private static string Described(int ahead, int behind) => (ahead, behind) switch
    {
        (0, 0) => "Up to date",
        ( > 0, > 0) => string.Create(CultureInfo.InvariantCulture, $"{ahead} ahead, {behind} behind"),
        ( > 0, _) => string.Create(CultureInfo.InvariantCulture, $"{ahead} ahead"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{behind} behind"),
    };
}
