using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.ScenarioTestSupport;
using History = VisualCommit.Testing.Scenarios.HistoryFiles;
using Protect = VisualCommit.Testing.Scenarios.ProtectFiles;
using Reflog = VisualCommit.Testing.Scenarios.ReflogFiles;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Pins down phase 5's scenario repos (docs/phase-5-design.md 15.1): their graphs as the app lays
/// them out, refs, files and reflogs; and, with real git, every property the visual checks rely
/// on: what file history and blame find across the rename, what each search mode matches, what
/// the comparisons list, what hiding and soloing leave out, which reflog entries are lost and
/// where the gap is, the backups of every kind and what the clean-up removes by the harnesses'
/// backup clock, and which checkouts, merges and picks git refuses for which changes. A class of
/// its own beside <see cref="Phase4ScenarioTests"/>, so that xunit runs them side by side. The
/// SHAs come from <see cref="TempRepo"/>'s clock, the same on every machine and every git.
/// </summary>
public class Phase5ScenarioTests
{
    // ---- HistoryAsync -----------------------------------------------------------------------------

    [Fact]
    public async Task History_has_its_graph_in_the_documented_rows_and_lanes()
    {
        using var repo = await Scenarios.HistoryAsync();

        var (rows, lanes) = await GraphOfAsync(repo);

        Assert.Equal(
            [
                "c0bc2dd Read whole lines · lane 0",
                "20d7fbe Stop tracing · lane 0",
                "e765529 Write new notes · lane 0",
                "f513566 Merge branch 'feature/readme' · lane 0",
                "c1b6375 Explain the reader · lane 1",
                "884b462 Remove the notes · lane 0 · ends 1",
                "245c395 Compare whole lines · lane 1",
                "0575c7f Rename the parser to reader · lane 0",
                "d0d3c77 Add the comparer · lane 1",
                "14a5c6c Add tracing · lane 0 · ends 1",
                "365482b Handle empty input · lane 0",
                "775113f Add the logo · lane 0",
                "1d5a7cf Add the parser · lane 0",
                "7829335 Initial commit · lane 0",
            ],
            rows);
        Assert.Equal(2, lanes);
        Assert.Equal(
            [
                "Read whole lines|Jonas Berg <jonas@example.com>|12:13|Jonas Berg",
                "Stop tracing|Test Author <author@example.com>|12:12|Test Author",
                "Write new notes|Mira Kovac <mira@example.com>|12:11|Mira Kovac",
                "Merge branch 'feature/readme'|Test Author <author@example.com>|12:10|Test Author",
                "Explain the reader|Jonas Berg <jonas@example.com>|12:09|Jonas Berg",
                "Remove the notes|Test Author <author@example.com>|12:08|Test Author",
                "Compare whole lines|Jonas Berg <jonas@example.com>|12:07|Jonas Berg",
                "Rename the parser to reader|Mira Kovac <mira@example.com>|12:06|Mira Kovac",
                "Add the comparer|Jonas Berg <jonas@example.com>|12:05|Jonas Berg",
                "Add tracing|Test Author <author@example.com>|12:04|Test Author",
                "Handle empty input|Jonas Berg <jonas@example.com>|12:03|Jonas Berg",
                "Add the logo|Test Author <author@example.com>|12:02|Test Author",
                "Add the parser|Mira Kovac <mira@example.com>|12:01|Mira Kovac",
                "Initial commit|Test Author <author@example.com>|12:00|Test Author",
            ],
            Lines(await repo.GitAsync("log", "--date-order", "--format=%s|%aN <%aE>|%ad|%cN", "--date=format:%H:%M", "--branches", "--remotes", "HEAD")));
    }

    [Fact]
    public async Task History_has_its_branches_remote_and_files_with_the_reader_changed_in_the_working_tree()
    {
        using var repo = await Scenarios.HistoryAsync();

        Assert.Equal(
            [
                "refs/heads/feature/compare 245c395 refs/remotes/origin/feature/compare ",
                "refs/heads/main c0bc2dd refs/remotes/origin/main ",
                "refs/remotes/origin/feature/compare 245c395  ",
                "refs/remotes/origin/main c0bc2dd  ",
            ],
            await RefsAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal([" M src/Reader.cs"], await StatusAsync(repo));
        Assert.Equal(History.ReaderWorking, WorkingFile(repo, "src/Reader.cs"));
        Assert.Equal(
            ["refs/heads/feature/compare 245c395", "refs/heads/main c0bc2dd"],
            Lines(await repo.GitAsync("ls-remote", "--heads", "origin")).Select(line => $"{line.Split('\t')[1]} {line[..7]}"));

        Assert.Equal(["LICENSE", "README.md", "assets/logo.png", "docs/notes.md", "src/Config.cs", "src/Reader.cs"], Lines(await repo.GitAsync("ls-tree", "-r", "--name-only", "refs/heads/main")));
        Assert.Equal(History.ReadmeReader, await FileAtAsync(repo, "refs/heads/main", "README.md"));
        Assert.Equal(History.ReadmeCompare, await FileAtAsync(repo, "refs/heads/feature/compare", "README.md"));
        Assert.Equal(History.NotesSecond, await FileAtAsync(repo, "refs/heads/main", "docs/notes.md"));
        Assert.Equal(History.NotesFirst, await FileAtAsync(repo, "refs/heads/feature/compare", "docs/notes.md"));
        Assert.Equal(History.ConfigUntraced, await FileAtAsync(repo, "refs/heads/main", "src/Config.cs"));
        Assert.Equal(History.ConfigTraced, await FileAtAsync(repo, "refs/heads/feature/compare", "src/Config.cs"));
        Assert.Equal(History.ReaderLines, await FileAtAsync(repo, "refs/heads/main", "src/Reader.cs"));
        Assert.Equal(History.ParserEmptyInput, await FileAtAsync(repo, "refs/heads/feature/compare", "src/Parser.cs"));
        Assert.Equal(History.ComparerLines, await FileAtAsync(repo, "refs/heads/feature/compare", "src/Comparer.cs"));
        Assert.Equal(History.Logo, await BlobAsync(repo, "refs/heads/main:assets/logo.png"));

        // LICENSE is the same blob in every commit, and only the first adds it: "All files" shows it unchanged.
        var license = (await repo.GitAsync("rev-parse", await ShaOfAsync(repo, "Initial commit") + ":LICENSE")).StandardOutput.Trim();
        foreach (var commit in Lines(await repo.GitAsync("rev-list", "--branches")))
        {
            Assert.Equal(license, (await repo.GitAsync("rev-parse", commit + ":LICENSE")).StandardOutput.Trim());
        }

        Assert.Equal(History.License, await FileAtAsync(repo, "refs/heads/main", "LICENSE"));
        Assert.Equal(["Initial commit"], Lines(await repo.GitAsync("log", "--format=%s", "--branches", "--", "LICENSE")));
    }

    [Fact]
    public async Task History_of_the_reader_follows_its_rename_and_the_notes_show_both_lives()
    {
        using var repo = await Scenarios.HistoryAsync();

        Assert.Equal(
            [
                "Read whole lines|M|src/Reader.cs",
                "Rename the parser to reader|R084|src/Parser.cs|src/Reader.cs",
                "Handle empty input|M|src/Parser.cs",
                "Add the parser|A|src/Parser.cs",
            ],
            await FollowAsync(repo, "refs/heads/main", "src/Reader.cs"));
        Assert.Equal(
            ["Write new notes|A|docs/notes.md", "Remove the notes|D|docs/notes.md", "Initial commit|A|docs/notes.md"],
            await FollowAsync(repo, "refs/heads/main", "docs/notes.md"));

        // No merge is listed (design 0.1): README.md's history has the side branch's edit, not the merge.
        Assert.Equal(["Explain the reader|M|README.md", "Initial commit|A|README.md"], await FollowAsync(repo, "refs/heads/main", "README.md"));
        Assert.Equal(["Add the comparer|M|README.md", "Initial commit|A|README.md"], await FollowAsync(repo, "refs/heads/feature/compare", "README.md"));
        Assert.Equal(["Initial commit|A|LICENSE"], await FollowAsync(repo, "refs/heads/main", "LICENSE"));

        // The image is one commit's: blame shows its bytes, which hold a NUL (design 4.2).
        Assert.Equal(["Add the logo|A|assets/logo.png"], await FollowAsync(repo, "refs/heads/main", "assets/logo.png"));
        Assert.Contains((byte)0, History.Logo);
    }

    [Fact]
    public async Task History_blame_of_the_reader_has_lines_of_four_commits_by_two_authors_and_two_not_committed_yet()
    {
        using var repo = await Scenarios.HistoryAsync();

        const string Parser = "Add the parser";
        const string Empty = "Handle empty input";
        const string Rename = "Rename the parser to reader";
        const string Lines = "Read whole lines";
        var atMain = await BlameAsync(repo, "refs/heads/main", "src/Reader.cs");
        Assert.Equal(
            [
                Parser, Parser, Parser, Rename, Parser, Parser, Parser, Rename, Parser, Empty, Parser, Parser,
                Lines, Parser, Empty, Empty, Empty, Empty, Empty, Lines, Parser, Parser,
            ],
            atMain.Lines);
        Assert.Equal(History.ReaderLines.Split('\n').Length - 1, atMain.Lines.Length);
        Assert.Equal(["Jonas Berg", "Mira Kovac"], atMain.Authors.Values.Distinct().Order(StringComparer.Ordinal));
        Assert.Equal("Mira Kovac", atMain.Authors[Rename]);
        Assert.Equal("Jonas Berg", atMain.Authors[Lines]);

        // "Blame before this commit" follows previous, the commit's first parent and the path
        // there: the rename's lines to src/Parser.cs; the first commit's lines have none (design 4.2).
        Assert.Equal($"{await RevParseAsync(repo, await ShaOfAsync(repo, Rename) + "^")} src/Parser.cs", atMain.Previous[Rename]);
        Assert.Equal($"{await RevParseAsync(repo, await ShaOfAsync(repo, Lines) + "^")} src/Reader.cs", atMain.Previous[Lines]);
        Assert.Equal($"{await RevParseAsync(repo, await ShaOfAsync(repo, Empty) + "^")} src/Parser.cs", atMain.Previous[Empty]);
        Assert.Equal(await ShaOfAsync(repo, "Add tracing"), atMain.Previous[Rename]![..40]);
        Assert.Null(atMain.Previous[Parser]);
        Assert.Equal("src/Parser.cs", atMain.Paths[Parser]);
        Assert.Equal("src/Reader.cs", atMain.Paths[Lines]);

        // In the working tree: the changed summary line and the added property are not committed yet.
        var working = await BlameAsync(repo, null, "src/Reader.cs");
        var committed = atMain.Lines[..21];
        committed[2] = NotCommitted;
        Assert.Equal([.. committed, NotCommitted, NotCommitted, atMain.Lines[21]], working.Lines);
    }

    [Fact]
    public async Task History_search_texts_match_the_documented_commits_in_the_graphs_order()
    {
        using var repo = await Scenarios.HistoryAsync();
        var rows = Lines(await repo.GitAsync("log", "--date-order", "--format=%H", "--branches", "--remotes", "HEAD"));

        // Commits mode, git's half (design 3.2): the whole message, case ignored, a fixed string.
        Assert.Equal(["Compare whole lines", "Handle empty input"], await SearchAsync(repo, rows, NoVariables, "-F", "--regexp-ignore-case", "--grep=TOKENIZER"));
        Assert.DoesNotContain(Lines(await repo.GitAsync("log", "--format=%s", "--branches", "--remotes")), subject => subject.Contains(History.BodyWord, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(History.EmptyInputMessage + "\n", await MessageAsync(repo, await ShaOfAsync(repo, "Handle empty input")));
        Assert.Equal(History.CompareLinesMessage + "\n", await MessageAsync(repo, await ShaOfAsync(repo, "Compare whole lines")));

        // Changes mode: the pickaxe string is added by one commit and removed by a later one.
        Assert.Equal(
            ["Stop tracing", "Add tracing"],
            await SearchAsync(repo, rows, NoVariables, "--first-parent", "--diff-merges=first-parent", "--root", "--find-renames", "--no-textconv", "-s", "--regexp-ignore-case", "-S" + History.PickaxeText.ToLowerInvariant()));

        // Files mode: a part of a name finds the commits whose own change touches it, a rename's
        // both names included.
        Assert.Equal(["Rename the parser to reader", "Handle empty input", "Add the parser"], await FilesSearchAsync(repo, rows, "parser"));
        Assert.Equal(["Read whole lines", "Rename the parser to reader"], await FilesSearchAsync(repo, rows, "READER"));
        Assert.Equal(["Write new notes", "Remove the notes", "Initial commit"], await FilesSearchAsync(repo, rows, "notes"));
    }

    /// <summary>
    /// Design 0.3 and 3.2 take the Files mode's matches from <c>git log --no-walk=unsorted --stdin
    /// --first-parent -- &lt;pathspecs&gt;</c>, which prints only the rows whose change touches the
    /// paths on Git 2.36. Git 2.55 (CI's) prints every row given and limits only the diff to the
    /// paths: a row matches when its change lists a path (<see cref="FilesSearchAsync"/>), on
    /// every git. Which version between them changed it is not known; a git in between does one or
    /// the other.
    /// </summary>
    [Fact]
    public async Task History_rows_given_with_no_walk_are_limited_by_a_pathspec_on_git_2_36_but_not_on_git_2_55()
    {
        using var repo = await Scenarios.HistoryAsync();
        var rows = Lines(await repo.GitAsync("log", "--date-order", "--format=%H", "--branches", "--remotes", "HEAD"));
        var everyRow = Lines(await repo.GitAsync("log", "--date-order", "--format=%s", "--branches", "--remotes", "HEAD"));
        var matches = await FilesSearchAsync(repo, rows, "parser");
        Assert.Equal(["Rename the parser to reader", "Handle empty input", "Add the parser"], matches);

        var limited = await SearchAsync(repo, rows, WithoutLiteralPathspecs, "--first-parent", "--", ":(glob,icase)**/*parser*", ":(glob,icase)**/*parser*/**");

        Assert.True(GitVersion.TryParse((await repo.GitAsync("--version")).StandardOutput, out var version));
        if (version.Major == 2 && version.Minor == 36)
        {
            Assert.Equal(matches, limited);
        }
        else if (version >= new GitVersion(2, 55, 0))
        {
            Assert.Equal(everyRow, limited);
        }
        else
        {
            Assert.True(limited.SequenceEqual(matches) || limited.SequenceEqual(everyRow), $"Git {version} printed neither the matching rows nor every row:\n{string.Join('\n', limited)}");
        }
    }

    [Fact]
    public async Task History_branches_compare_from_their_merge_base_and_both_trees_show_mains_rename_reversed()
    {
        using var repo = await Scenarios.HistoryAsync();
        var main = await RevParseAsync(repo, "refs/heads/main");
        var compare = await RevParseAsync(repo, "refs/heads/feature/compare");

        var mergeBase = (await repo.GitAsync("merge-base", main, compare)).StandardOutput.Trim();
        Assert.Equal(await ShaOfAsync(repo, "Add tracing"), mergeBase);
        Assert.Equal(["M\tREADME.md", "A\tsrc/Comparer.cs"], await NameStatusAsync(repo, mergeBase, compare));
        Assert.Equal(
            ["M\tREADME.md", "M\tdocs/notes.md", "A\tsrc/Comparer.cs", "M\tsrc/Config.cs", "R067\tsrc/Reader.cs\tsrc/Parser.cs"],
            await NameStatusAsync(repo, main, compare));
        Assert.Equal("7\t2", (await repo.GitAsync("rev-list", "--left-right", "--count", $"{main}...{compare}")).StandardOutput.Trim());

        // Two commits of one line of history, two-dot (check F12): what happened between them.
        Assert.Equal(
            ["M\tREADME.md", "M\tdocs/notes.md", "M\tsrc/Config.cs", "M\tsrc/Reader.cs"],
            await NameStatusAsync(repo, await ShaOfAsync(repo, "Rename the parser to reader"), main));
    }

    // ---- FiltersAsync -----------------------------------------------------------------------------

    [Fact]
    public async Task Filters_has_its_graph_in_the_documented_rows_and_lanes()
    {
        using var repo = await Scenarios.FiltersAsync();

        var (rows, lanes) = await GraphOfAsync(repo);

        Assert.Equal(FiltersGraph, rows);
        Assert.Equal(5, lanes);
        Assert.Equal(
            ["Beta six|12:12", "Beta five|12:11", "Alpha two|12:10", "Beta four|12:09", "Merge branch 'feature/merged'|12:08", "Main work|12:07", "Beta three|12:06", "Merged work|12:05", "Beta two|12:04", "Alpha one|12:03", "Beta one|12:02", "Add the core|12:01", "Initial commit|12:00"],
            Lines(await repo.GitAsync("log", "--date-order", "--format=%s|%ad", "--date=format:%H:%M", "--branches", "--remotes", "HEAD")));
    }

    [Fact]
    public async Task Filters_has_its_branches_upstreams_remote_head_and_two_stashes()
    {
        using var repo = await Scenarios.FiltersAsync();

        Assert.Equal(
            [
                "refs/heads/feature/alpha 7c60fc4 refs/remotes/origin/feature/alpha [behind 1]",
                "refs/heads/feature/beta 89f891e refs/remotes/origin/feature/beta ",
                "refs/heads/feature/merged 629bc39  ",
                "refs/heads/main 4c0aa3c refs/remotes/origin/main ",
                "refs/remotes/origin/HEAD 89f891e  ",
                "refs/remotes/origin/feature/alpha e4d9976  ",
                "refs/remotes/origin/feature/beta 89f891e  ",
                "refs/remotes/origin/main 4c0aa3c  ",
                "refs/stash d3f16e8  ",
                "refs/tags/v1 369f471  ",
            ],
            await RefsAsync(repo));
        Assert.Equal("refs/remotes/origin/feature/beta", (await repo.GitAsync("symbolic-ref", "refs/remotes/origin/HEAD")).StandardOutput.Trim());
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty(await StatusAsync(repo));
        Assert.Equal("Add the core", Subject(await repo.GitAsync("log", "-1", "--format=%s", "refs/tags/v1")));
        Assert.Equal(["Merge branch 'feature/merged'", "Merged work"], Lines(await repo.GitAsync("log", "-2", "--format=%s", "--topo-order", "refs/heads/main")));

        // Each stash's base: Beta six (feature/beta's tip) and Alpha one (feature/alpha's tip, which
        // only feature/alpha and its upstream reach).
        var stashes = await repo.StashListAsync();
        Assert.Equal(["stash@{0} On feature/alpha: Alpha experiment", "stash@{1} On feature/beta: Beta experiment"], stashes.Select(stash => $"{stash.Selector} {stash.Message}"));
        Assert.Equal(await RevParseAsync(repo, "refs/heads/feature/alpha"), await RevParseAsync(repo, "stash@{0}^1"));
        Assert.Equal(await RevParseAsync(repo, "refs/heads/feature/beta"), await RevParseAsync(repo, "stash@{1}^1"));
        Assert.Equal(
            ["refs/heads/feature/alpha", "refs/remotes/origin/feature/alpha"],
            Lines(await repo.GitAsync("for-each-ref", "--format=%(refname)", "--contains", "stash@{0}^1", "refs/heads", "refs/remotes", "refs/tags")));
        Assert.Equal("alpha one\nexperiment\n", await FileAtAsync(repo, "stash@{0}", "src/alpha.txt"));
        Assert.Equal("beta one\nexperiment\n", await FileAtAsync(repo, "stash@{1}", "beta/one.txt"));
    }

    [Fact]
    public async Task Filters_hide_and_solo_leave_out_what_the_upstreams_and_the_remotes_head_would_keep()
    {
        using var repo = await Scenarios.FiltersAsync();
        string[] alpha = ["Alpha two", "Alpha one"];
        string[] beta = ["Beta six", "Beta five", "Beta four", "Beta three", "Beta two", "Beta one"];

        // Hiding feature/alpha alone leaves its commits on screen through its upstream (design 0.6);
        // with the upstream and origin/HEAD excluded too (8.2), they are gone.
        Assert.Equal(alpha, (await LoadedAsync(repo, "--exclude=feature/alpha", "--branches", "--remotes", "--tags", "--ignore-missing", "HEAD", "--")).Intersect(alpha));
        var hidden = await LoadedAsync(repo, "--exclude=feature/alpha", "--branches", "--exclude=origin/feature/alpha", "--exclude=origin/HEAD", "--remotes", "--tags", "--ignore-missing", "HEAD", "--");
        Assert.Empty(hidden.Intersect(alpha));
        Assert.Equal(beta, hidden.Intersect(beta));

        // origin/HEAD points at origin/feature/beta: hiding feature/beta with its upstream keeps its
        // commits until origin/HEAD is excluded too.
        Assert.Equal(beta, (await LoadedAsync(repo, "--exclude=feature/beta", "--branches", "--exclude=origin/feature/beta", "--remotes", "--tags", "--ignore-missing", "HEAD", "--")).Intersect(beta));
        Assert.Empty((await LoadedAsync(repo, "--exclude=feature/beta", "--branches", "--exclude=origin/feature/beta", "--exclude=origin/HEAD", "--remotes", "--tags", "--ignore-missing", "HEAD", "--")).Intersect(beta));

        // Soloing feature/beta with its upstream: its history and HEAD's; Alpha one, the alpha
        // stash's base, is not among them, so that stash is left out (8.2).
        var soloed = await LoadedAsync(repo, "--ignore-missing", "refs/heads/feature/beta", "refs/remotes/origin/feature/beta", "HEAD", "--");
        Assert.Equal(
            ["Beta six", "Beta five", "Beta four", "Merge branch 'feature/merged'", "Main work", "Beta three", "Merged work", "Beta two", "Beta one", "Add the core", "Initial commit"],
            soloed);
        Assert.DoesNotContain("Alpha one", soloed);
    }

    // ---- ReflogAsync ------------------------------------------------------------------------------

    [Fact]
    public async Task Reflog_has_its_graph_in_the_documented_rows_and_lanes()
    {
        using var repo = await Scenarios.ReflogAsync();

        var (rows, lanes) = await GraphOfAsync(repo);

        Assert.Equal(
            [
                "6f8d627 Draft work · lane 0",
                "9dd61fd Write the notes · lane 0",
                "362fbd6 Topic work · lane 1",
                "316166a Clash notes · lane 0 · ends 1",
                "fe14262 Clash notes · lane 1",
                "3b2501a Clashing work · lane 1",
                "6d7a40b Main work · lane 0",
                "7dd8e8f Add the app · lane 0 · ends 1",
                "5c9c1cb Initial commit · lane 0",
            ],
            rows);
        Assert.Equal(2, lanes);
    }

    [Fact]
    public async Task Reflog_has_its_branches_tags_and_remote_without_the_branch_and_tag_deleted_there()
    {
        using var repo = await Scenarios.ReflogAsync();

        Assert.Equal(
            [
                "refs/heads/clash fe14262  ",
                "refs/heads/feature/renamed 6f8d627  ",
                "refs/heads/main 9dd61fd refs/remotes/origin/main [ahead 2]",
                "refs/heads/topic 362fbd6 refs/remotes/origin/topic ",
                "refs/remotes/origin/main 6d7a40b  ",
                "refs/remotes/origin/topic 362fbd6  ",
                "refs/tags/v0.8 5c9c1cb  ",
            ],
            (await RefsAsync(repo)).Where(line => !line.StartsWith(GitRepository.BackupRefPrefix, StringComparison.Ordinal)));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty(await StatusAsync(repo));
        Assert.Empty(await repo.StashListAsync());
        Assert.Equal(["origin"], Lines(await repo.GitAsync("remote")));
        Assert.Equal(
            ["refs/heads/main 6d7a40b", "refs/heads/topic 362fbd6"],
            Lines(await repo.GitAsync("ls-remote", "origin")).Select(line => $"{line.Split('\t')[1]} {line[..7]}"));
        Assert.True(Directory.Exists(repo.SiblingPath("mirror.git")), "The removed remote's repository is still next to the working tree.");

        Assert.Equal(Reflog.Notes, await FileAtAsync(repo, "refs/heads/main", "docs/notes.txt"));
        Assert.Equal(Reflog.AppMainWork, await FileAtAsync(repo, "refs/heads/main", "src/app.txt"));
        Assert.Equal(Reflog.Readme, await FileAtAsync(repo, "refs/heads/main", "README.md"));
        Assert.Equal(["Write the notes", "Clash notes", "Main work", "Add the app", "Initial commit"], Lines(await repo.GitAsync("log", "--format=%s", "refs/heads/main")));
        Assert.Equal(["Topic work", "Clash notes", "Main work", "Add the app", "Initial commit"], Lines(await repo.GitAsync("log", "--format=%s", "refs/heads/topic")));
    }

    [Fact]
    public async Task Reflog_of_HEAD_has_a_gap_and_lost_commits_and_each_reflog_its_own_story()
    {
        using var repo = await Scenarios.ReflogAsync();

        // The rows of design 5.2's first call, and the times of its second, zipped by position.
        var entries = Lines(await repo.GitAsync("log", "--walk-reflogs", "--format=%gd %h %gs", "HEAD", "--"));
        Assert.Equal(HeadReflog, entries);
        var dated = Lines(await repo.GitAsync("log", "--walk-reflogs", "--date=raw", "--format=%h", "HEAD", "--"));
        Assert.Equal(entries.Select(entry => entry.Split(' ')[1]), dated);
        Assert.Equal(
            ["12:27", "12:26", "12:26", "12:26", "12:26", "12:25", "12:25", "12:25"],
            Lines(await repo.GitAsync("log", "--walk-reflogs", "-8", "--date=format:%H:%M", "--format=%gd", "HEAD", "--")).Select(selector => selector["HEAD@{".Length..^1]));

        // Lost: no branch, tag, remote branch or HEAD reaches them (design 5.2).
        var shas = string.Concat(Lines(await repo.GitAsync("log", "--walk-reflogs", "--format=%H", "HEAD", "--")).Distinct().Select(sha => sha + "\n"));
        var lost = Lines(await repo.GitWithInputAsync(shas, "rev-list", "--ignore-missing", "--stdin", "--not", "--branches", "--tags", "--remotes", "HEAD"));
        Assert.Equal(
            ["44e646b Doomed work", "7e2ce3b Detached experiment", "c7bf508 Draft the notes", "d07c677 Try a shortcut", "f7a69be Topic work"],
            (await Task.WhenAll(lost.Select(sha => repo.GitAsync("log", "-1", "--format=%h %s", sha)))).Select(Subject).Order(StringComparer.Ordinal));

        Assert.Equal(
            [
                "main@{0} reset: moving to 9dd61fde06301292b9b14d0911560b6d987f645d",
                "main@{1} commit: Try a shortcut",
                "main@{2} commit (amend): Write the notes",
                "main@{3} commit: Draft the notes",
                "main@{4} cherry-pick: Clash notes",
                "main@{5} commit: Main work",
                "main@{6} commit: Add the app",
                "main@{7} commit (initial): Initial commit",
            ],
            Lines(await repo.GitAsync("log", "--walk-reflogs", "--format=%gd %gs", "refs/heads/main", "--")));
        Assert.Equal(
            ["feature/renamed@{0} Branch: renamed refs/heads/draft to refs/heads/feature/renamed", "feature/renamed@{1} commit: Draft work", "feature/renamed@{2} branch: Created from HEAD"],
            Lines(await repo.GitAsync("log", "--walk-reflogs", "--format=%gd %gs", "refs/heads/feature/renamed", "--")));
        Assert.Equal(
            ["topic@{0} rebase (finish): refs/heads/topic onto 316166adc8e550ebb513a7f8c0ea99ecbdd88983", "topic@{1} commit: Topic work", "topic@{2} branch: Created from HEAD"],
            Lines(await repo.GitAsync("log", "--walk-reflogs", "--format=%gd %gs", "refs/heads/topic", "--")));

        // The deleted branch's reflog went with it; --ignore-missing reads it as empty.
        var doomed = await repo.GitAllowingFailureAsync("log", "--walk-reflogs", "--ignore-missing", "--format=%gd", "refs/heads/doomed", "--");
        Assert.True(doomed.Succeeded, doomed.StandardError);
        Assert.Empty(Lines(doomed));

        // origin/topic's reflog has the force push whose older neighbour is the backup's commit (design 7.10).
        Assert.Equal(
            ["362fbd6 update by push", "f7a69be update by push"],
            Lines(await repo.GitAsync("log", "--walk-reflogs", "--format=%h %gs", "refs/remotes/origin/topic", "--")));
    }

    [Fact]
    public async Task Reflog_has_a_backup_of_every_kind_the_list_shows_each_keeping_what_its_step_left()
    {
        using var repo = await Scenarios.ReflogAsync();

        Assert.Equal(ReflogBackups, (await RefsAsync(repo)).Where(line => line.StartsWith(GitRepository.BackupRefPrefix, StringComparison.Ordinal)).Select(line => line[GitRepository.BackupRefPrefix.Length..].TrimEnd()));
        var kinds = (await repo.BackupRefsAsync()).Select(entry => entry.Name[GitRepository.BackupRefPrefix.Length..]).Select(name => name[..name.IndexOf("-20", StringComparison.Ordinal)]).ToHashSet();
        Assert.Equal(
            ["abort", "branch", "detached", "discard", "force-push", "rebase", "remote", "remote-branch", "remote-tag", "reset", "skip", "stash", "tag", "tag-undone"],
            kinds.Order(StringComparer.Ordinal));

        // The snapshots, in the app's own form (D67, phase 4 design 5.1).
        const string Backup = GitRepository.BackupRefPrefix;
        Assert.Equal(
            ("VisualCommit: snapshot before an abort\n\ndocs/clash.txt\nsrc/app.txt\n", "6d7a40b"),
            await SnapshotOfAsync(repo, Backup + "abort-20260101T120601000Z-changes"));
        Assert.Equal(Reflog.AppAbortResolution, await FileAtAsync(repo, Backup + "abort-20260101T120601000Z-changes", "src/app.txt"));
        Assert.Equal("clash notes\n", await FileAtAsync(repo, Backup + "abort-20260101T120601000Z-changes", "docs/clash.txt"));
        Assert.Equal(
            ("VisualCommit: snapshot before an abort\n\nsrc/app.txt\n", "6d7a40b"),
            await SnapshotOfAsync(repo, Backup + "skip-20260101T120701000Z-changes"));
        Assert.Equal(Reflog.AppSkipResolution, await FileAtAsync(repo, Backup + "skip-20260101T120701000Z-changes", "src/app.txt"));
        Assert.Equal(
            ("VisualCommit: index before a mixed reset\n", "316166a"),
            await SnapshotOfAsync(repo, Backup + "reset-20260101T121901000Z-changes"));
        Assert.Equal(Reflog.AppStaged, await FileAtAsync(repo, Backup + "reset-20260101T121901000Z-changes", "src/app.txt"));
        Assert.Equal(
            ("VisualCommit: snapshot before discarding 2 files\n\nnotes/todo.txt\nsrc/app.txt\n", "316166a"),
            await SnapshotOfAsync(repo, Backup + "discard-20260101T122001000Z"));
        Assert.Equal(Reflog.Todo, await FileAtAsync(repo, Backup + "discard-20260101T122001000Z", "notes/todo.txt"));
        Assert.Equal(Reflog.AppStaged, await FileAtAsync(repo, Backup + "discard-20260101T122001000Z", "src/app.txt"));
        var hard = Backup + "reset-20260101T122401000Z-changes";
        Assert.Equal(("VisualCommit: snapshot before a hard reset to 9dd61fd\n\nREADME.md\n", "d07c677 f174bf8"), await SnapshotOfAsync(repo, hard));
        Assert.Equal(("VisualCommit: index before a hard reset\n", "d07c677"), await SnapshotOfAsync(repo, hard + "^2"));
        Assert.Equal(Reflog.ReadmeEdited, await FileAtAsync(repo, hard, "README.md"));
        Assert.Equal("shortcut\n", await FileAtAsync(repo, hard + "^2", "src/shortcut.txt"));
        foreach (var snapshot in new[] { "abort-20260101T120601000Z-changes", "skip-20260101T120701000Z-changes", "reset-20260101T121901000Z-changes", "discard-20260101T122001000Z", "reset-20260101T122401000Z-changes" })
        {
            Assert.Equal("VisualCommit <snapshot@visualcommit.invalid>|VisualCommit <snapshot@visualcommit.invalid>", Subject(await repo.GitAsync("log", "-1", "--format=%an <%ae>|%cn <%ce>", Backup + snapshot)));
        }

        // What the other backups keep: the dropped stash, the tag objects, the commits left behind.
        Assert.Equal(("On main: Try a README", "commit"), await ObjectAsync(repo, Backup + "stash-20260101T121301000Z"));
        Assert.Equal(Reflog.ReadmeStashed, await FileAtAsync(repo, Backup + "stash-20260101T121301000Z", "README.md"));
        Assert.Equal(("Version 0.9", "tag"), await ObjectAsync(repo, Backup + "tag-20260101T121401000Z/v0.9"));
        Assert.Equal(("Release candidate", "tag"), await ObjectAsync(repo, Backup + "tag-undone-20260101T121501000Z/v1.0-rc"));
        Assert.Equal("Clash notes", Subject(await repo.GitAsync("log", "-1", "--format=%s", Backup + "tag-undone-20260101T121501000Z/v1.0-rc")));
        Assert.Equal(await RevParseAsync(repo, "refs/tags/v0.8"), await RevParseAsync(repo, Backup + "remote-tag-20260101T121601000Z/origin/v0.8"));
        Assert.Equal(("Try a shortcut", "commit"), await ObjectAsync(repo, Backup + "reset-20260101T122401000Z"));
        Assert.Equal(("Doomed work", "commit"), await ObjectAsync(repo, Backup + "branch-20260101T122701000Z/doomed"));
        Assert.Equal(("Detached experiment", "commit"), await ObjectAsync(repo, Backup + "detached-20260101T121101000Z"));
        Assert.Equal(await RevParseAsync(repo, "refs/heads/topic"), await RevParseAsync(repo, Backup + "remote-20260101T121801000Z/mirror/topic"));
        foreach (var gone in new[] { "refs/tags/v0.9", "refs/tags/v1.0-rc", "refs/heads/doomed", "refs/heads/draft", "refs/remotes/origin/feature/gone", "refs/remotes/mirror/main" })
        {
            Assert.Null(await repo.RefTargetAsync(gone));
        }
    }

    [Fact]
    public async Task Reflog_backups_the_clean_up_removes_by_the_harness_clock_are_the_two_old_ones_and_one_of_51_young_ones()
    {
        using var repo = await Scenarios.ReflogAsync();
        var repository = await OpenAsync(repo);
        repository.BackupClock = BackupClock.Fixed(AppClocks.HarnessTime);
        Assert.Equal(Reflog.OldBackups, await repository.FindBackupsToRemoveAsync(BackupRetention.Default, new HashSet<string>(), cancellationToken: TestCancelled));

        using var many = await Scenarios.ReflogManyBackupsAsync();
        var manyRepository = await OpenAsync(many);
        manyRepository.BackupClock = BackupClock.Fixed(AppClocks.HarnessTime);
        var removed = await manyRepository.CleanUpBackupsAsync(BackupRetention.Default, new HashSet<string>(), cancellationToken: TestCancelled);
        Assert.Equal(
            [
                "Removed backup reset-20251120T090001000Z (older than 30 days)",
                "Removed backup branch-20251120T090002000Z (older than 30 days)",
                $"Removed backup {Reflog.FirstFillerBackup} (more than 50 backups)",
            ],
            removed.Select(backup => backup.Describe(BackupRetention.Default)));
        Assert.Empty(await manyRepository.FindBackupsToRemoveAsync(BackupRetention.Default, new HashSet<string>(), cancellationToken: TestCancelled));

        // The same on a scenario without backups of its own (the conflicts scenario, for check A2).
        using var conflicts = await Scenarios.ConflictsAsync();
        await Scenarios.AddOldAndManyBackupsAsync(conflicts);
        var conflictsRepository = await OpenAsync(conflicts);
        conflictsRepository.BackupClock = BackupClock.Fixed(AppClocks.HarnessTime);
        Assert.Equal(
            [.. Reflog.OldBackups, Reflog.FirstFillerBackup],
            await conflictsRepository.FindBackupsToRemoveAsync(BackupRetention.Default, new HashSet<string>(), cancellationToken: TestCancelled));
        Assert.Equal(Reflog.YoungBackupsForCount + Reflog.OldBackups.Count, (await conflicts.BackupRefsAsync()).Count);

        // The backups the checks make are named later than all of the scenario's.
        Assert.True(BackupClock.Fixed(AppClocks.HarnessTime).NextName() > new DateTimeOffset(2026, 1, 1, 23, 59, 59, TimeSpan.Zero));
    }

    // ---- ProtectAsync -----------------------------------------------------------------------------

    [Fact]
    public async Task Protect_has_its_graph_branches_and_an_ignored_file_the_two_oldest_commits_track()
    {
        using var repo = await Scenarios.ProtectAsync();

        var (rows, lanes) = await GraphOfAsync(repo);
        Assert.Equal(
            [
                "41f8398 Main work · lane 0",
                "19a9044 Other files · lane 1",
                "4f5e821 Other edits · lane 1",
                "f39ea2d Describe the app · lane 0",
                "64166f7 Stop tracking the local settings · lane 0 · ends 1",
                "73e0bab Add the guide · lane 0",
                "035a2f5 Initial commit · lane 0",
            ],
            rows);
        Assert.Equal(2, lanes);
        Assert.Equal(["refs/heads/main 41f8398  ", "refs/heads/other 19a9044  ", "refs/tags/v1.1 a2f81f7  "], await RefsAsync(repo));
        Assert.Equal("Describe the app", Subject(await repo.GitAsync("log", "-1", "--format=%s", "refs/tags/v1.1")));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(["!! settings.local.json"], Lines(await repo.GitAsync("status", "--porcelain=v1", "--untracked-files=all", "--ignored")));
        Assert.Equal(Protect.SettingsLocal, WorkingFile(repo, "settings.local.json"));

        foreach (var (commit, tracks) in new[] { ("Initial commit", true), ("Add the guide", true), ("Stop tracking the local settings", false), ("Describe the app", false), ("Main work", false), ("Other files", false) })
        {
            var sha = await ShaOfAsync(repo, commit);
            Assert.True(tracks == (await repo.GitAllowingFailureAsync("cat-file", "-e", sha + ":settings.local.json")).Succeeded, $"{commit} {(tracks ? "does not track" : "tracks")} settings.local.json.");
        }

        Assert.Equal(Protect.SettingsCommitted, await FileAtAsync(repo, await ShaOfAsync(repo, "Add the guide"), "settings.local.json"));
        Assert.Equal(["README.md", "docs/guide.txt", "docs/old.txt", "notes", "src/app.txt", "src/lib.txt"], Lines(await repo.GitAsync("diff", "--name-only", "refs/heads/main", "refs/heads/other")));
        Assert.Equal(["M\tdocs/guide.txt", "D\tdocs/old.txt", "A\tnotes", "M\tsrc/app.txt", "M\tsrc/lib.txt"], Lines(await repo.GitAsync("diff", "--name-status", "refs/heads/other~2", "refs/heads/other")));
        Assert.Equal(await RevParseAsync(repo, "refs/heads/other~2"), await RevParseAsync(repo, "refs/heads/main~2"));
        Assert.Equal(["README.md"], Lines(await repo.GitAsync("diff", "--name-only", "refs/heads/main~2", "refs/heads/main")));
        Assert.Equal(Protect.NotesOther, await FileAtAsync(repo, "refs/heads/other", "notes"));

        // The current branch reaches its older commits and the tag, not other's commits (design 9.1).
        Assert.Equal(0, (await repo.GitAllowingFailureAsync("merge-base", "--is-ancestor", "refs/tags/v1.1", "HEAD")).ExitCode);
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("merge-base", "--is-ancestor", "refs/heads/other~1", "HEAD")).ExitCode);
    }

    [Fact]
    public async Task Protect_checkout_of_other_is_refused_for_each_kind_of_change_in_the_way_and_changes_nothing()
    {
        // One tracked change, three, an untracked file, a folder with an untracked file (check P24).
        await AssertCheckoutRefusedAsync(
            repo => repo.WriteFile("src/app.txt", "app\nlocal\n"),
            "error: Your local changes to the following files would be overwritten by checkout:",
            ["src/app.txt"]);
        await AssertCheckoutRefusedAsync(
            repo => repo.WriteFile("src/app.txt", "app\nlocal\n").WriteFile("src/lib.txt", "lib\nlocal\n").WriteFile("docs/guide.txt", "guide\nlocal\n"),
            "error: Your local changes to the following files would be overwritten by checkout:",
            ["docs/guide.txt", "src/app.txt", "src/lib.txt"]);
        await AssertCheckoutRefusedAsync(
            repo => repo.WriteFile("notes", "my own notes\n"),
            "error: The following untracked working tree files would be overwritten by checkout:",
            ["notes"]);
        await AssertCheckoutRefusedAsync(
            repo => repo.WriteFile("notes/today.txt", "today\n"),
            "error: Updating the following directories would lose untracked files in them:",
            ["notes"]);
    }

    [Fact]
    public async Task Protect_older_commit_of_main_is_checked_out_after_an_automatic_stash_and_the_way_back_gives_every_change_back()
    {
        using var repo = await Scenarios.ProtectAsync();
        var main = await repo.HeadAsync();
        var target = await ShaOfAsync(repo, "Describe the app");
        await repo.WriteFile("src/lib.txt", "lib\nstaged\n").GitAsync("add", "--", "src/lib.txt");
        repo.WriteFile("src/lib.txt", "lib\nstaged\nand more\n").WriteFile("src/app.txt", "app\nunstaged\n").WriteFile("scratch.txt", "scratch\n");
        var before = await ExactStateAsync(repo);

        // Design 9.2's steps 3 and 4, as the app runs them (stash push without GIT_LITERAL_PATHSPECS, D108).
        await repo.GitWithEnvironmentAsync(WithoutLiteralPathspecs, "stash", "push", "--include-untracked", "--message", $"Auto stash before checking out {target[..7]}");
        var checkout = await repo.GitAllowingFailureAsync("switch", "--detach", "--no-overwrite-ignore", target);
        Assert.True(checkout.Succeeded, checkout.StandardError);
        Assert.Equal(target, await repo.HeadAsync());
        Assert.Equal($"On main: Auto stash before checking out {target[..7]}", Assert.Single(await repo.StashListAsync()).Message);
        Assert.Equal(main, await RevParseAsync(repo, "stash@{0}^1"));
        Assert.Equal(["!! settings.local.json"], Lines(await repo.GitAsync("status", "--porcelain=v1", "--untracked-files=all", "--ignored")));
        Assert.Equal(Protect.SettingsLocal, WorkingFile(repo, "settings.local.json"));

        await repo.GitAsync("switch", "main");
        await repo.GitAsync("stash", "pop", "--index", "stash@{0}");
        Assert.Equal(before, await ExactStateAsync(repo));
        Assert.Equal(Protect.SettingsLocal, WorkingFile(repo, "settings.local.json"));
    }

    [Fact]
    public async Task Protect_ignored_settings_refuse_a_checkout_of_an_old_commit_only_with_no_overwrite_ignore_and_are_lost_without_it()
    {
        using var repo = await Scenarios.ProtectAsync();
        var guide = await ShaOfAsync(repo, "Add the guide");
        var head = await repo.HeadAsync();

        var refused = await repo.GitAllowingFailureAsync("switch", "--detach", "--no-overwrite-ignore", guide);
        Assert.Equal(1, refused.ExitCode);
        Assert.Equal(["settings.local.json"], BlockedPaths(refused.StandardError, "error: The following untracked working tree files would be overwritten by checkout:"));
        Assert.Equal(head, await repo.HeadAsync());
        Assert.Equal(Protect.SettingsLocal, WorkingFile(repo, "settings.local.json"));
        Assert.Equal(["settings.local.json"], Lines(await repo.GitWithEnvironmentAsync(WithoutLiteralPathspecs, "check-ignore", "--", "settings.local.json")));

        // Without the option git overwrites the ignored file, and the way back deletes it (design 0.7).
        var overwritten = await repo.GitAllowingFailureAsync("switch", "--detach", guide);
        Assert.True(overwritten.Succeeded, overwritten.StandardError);
        Assert.Equal(Protect.SettingsCommitted, WorkingFile(repo, "settings.local.json"));
        await repo.GitAsync("switch", "main");
        Assert.False(File.Exists(Path.Combine(repo.Path, "settings.local.json")), "The way back kept the file the old commit tracked.");
    }

    [Fact]
    public async Task Protect_merge_and_cherry_pick_of_others_work_are_refused_over_a_changed_or_a_staged_file()
    {
        // A change to a file other changes: git's "by merge" block, for a merge and a pick alike (check P27).
        foreach (var operation in new[] { new[] { "merge", "--no-edit", "refs/heads/other" }, ["cherry-pick", "refs/heads/other~1"] })
        {
            using var repo = await Scenarios.ProtectAsync();
            repo.WriteFile("src/app.txt", "app\nlocal\n");
            var before = await ExactStateAsync(repo);
            var refused = await repo.GitAllowingFailureAsync(operation);
            Assert.False(refused.Succeeded, $"git {operation[0]} was not refused.");
            Assert.Equal(["src/app.txt"], BlockedPaths(refused.StandardError, "error: Your local changes to the following files would be overwritten by merge:"));
            Assert.Equal(before, await ExactStateAsync(repo));
            Assert.Empty(InProgressFiles(repo));
        }

        // A staged change to a file other does not touch refuses them too, git naming no conflict.
        using (var merge = await Scenarios.ProtectAsync())
        {
            await merge.WriteFile("README.md", "# Protect\n\nStaged.\n").GitAsync("add", "--", "README.md");
            var before = await ExactStateAsync(merge);
            var refused = await merge.GitAllowingFailureAsync("merge", "--no-edit", "refs/heads/other");
            Assert.False(refused.Succeeded, "git merge was not refused.");
            Assert.Contains("Your local changes to the following files would be overwritten by merge:", refused.StandardError + refused.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(before, await ExactStateAsync(merge));
            Assert.Empty(InProgressFiles(merge));
        }

        using var pick = await Scenarios.ProtectAsync();
        await pick.WriteFile("README.md", "# Protect\n\nStaged.\n").GitAsync("add", "--", "README.md");
        var pickBefore = await ExactStateAsync(pick);
        var pickRefused = await pick.GitAllowingFailureAsync("cherry-pick", "refs/heads/other~1");
        Assert.False(pickRefused.Succeeded, "git cherry-pick was not refused.");
        Assert.Contains("error: your local changes would be overwritten by cherry-pick.", pickRefused.StandardError, StringComparison.Ordinal);
        Assert.Equal(pickBefore, await ExactStateAsync(pick));
        Assert.Empty(InProgressFiles(pick));
    }

    // ---- The pinned graphs and refs ---------------------------------------------------------------

    private static readonly string[] FiltersGraph =
    [
        "d3f16e8 On feature/alpha: Alpha experiment · lane 0",
        "c8f3db3 On feature/beta: Beta experiment · lane 1",
        "89f891e Beta six · lane 1",
        "ac357ab Beta five · lane 1",
        "e4d9976 Alpha two · lane 2",
        "1cdde63 Beta four · lane 1",
        "4c0aa3c Merge branch 'feature/merged' · lane 3",
        "ef7bfda Main work · lane 3",
        "0c13077 Beta three · lane 1",
        "629bc39 Merged work · lane 4",
        "8fc77ea Beta two · lane 1",
        "7c60fc4 Alpha one · lane 0 · ends 2",
        "2f6e977 Beta one · lane 1",
        "369f471 Add the core · lane 0 · ends 1, 3, 4",
        "67e5696 Initial commit · lane 0",
    ];

    /// <summary>HEAD's reflog as <c>git log --walk-reflogs --format="%gd %h %gs"</c> prints it: HEAD@{5}, the renamed branch's empty entry, is skipped.</summary>
    private static readonly string[] HeadReflog =
    [
        "HEAD@{0} 9dd61fd checkout: moving from doomed to main",
        "HEAD@{1} 44e646b commit: Doomed work",
        "HEAD@{2} 9dd61fd checkout: moving from main to doomed",
        "HEAD@{3} 9dd61fd checkout: moving from feature/renamed to main",
        "HEAD@{4} 6f8d627 Branch: renamed refs/heads/draft to refs/heads/feature/renamed",
        "HEAD@{6} 6f8d627 commit: Draft work",
        "HEAD@{7} 9dd61fd checkout: moving from main to draft",
        "HEAD@{8} 9dd61fd reset: moving to 9dd61fde06301292b9b14d0911560b6d987f645d",
        "HEAD@{9} d07c677 commit: Try a shortcut",
        "HEAD@{10} 9dd61fd commit (amend): Write the notes",
        "HEAD@{11} c7bf508 commit: Draft the notes",
        "HEAD@{12} 316166a checkout: moving from topic to main",
        "HEAD@{13} 362fbd6 checkout: moving from main to topic",
        "HEAD@{14} 316166a reset: moving to 316166adc8e550ebb513a7f8c0ea99ecbdd88983",
        "HEAD@{15} 316166a reset: moving to HEAD",
        "HEAD@{16} 316166a checkout: moving from 7e2ce3b66ecd286fc7ce6694d1f57ca6acf4f0d3 to main",
        "HEAD@{17} 7e2ce3b commit: Detached experiment",
        "HEAD@{18} 316166a checkout: moving from main to 316166adc8e550ebb513a7f8c0ea99ecbdd88983",
        "HEAD@{19} 316166a checkout: moving from topic to main",
        "HEAD@{20} 362fbd6 rebase (finish): returning to refs/heads/topic",
        "HEAD@{21} 362fbd6 rebase (pick): Topic work",
        "HEAD@{22} 316166a rebase (start): checkout 316166adc8e550ebb513a7f8c0ea99ecbdd88983",
        "HEAD@{23} f7a69be checkout: moving from main to topic",
        "HEAD@{24} 316166a cherry-pick: Clash notes",
        "HEAD@{25} 6d7a40b reset: moving to 6d7a40b76bb9156c0969e533b292cbe2357e6783",
        "HEAD@{26} 6d7a40b reset: moving to HEAD",
        "HEAD@{27} 6d7a40b checkout: moving from clash to main",
        "HEAD@{28} fe14262 commit: Clash notes",
        "HEAD@{29} 3b2501a commit: Clashing work",
        "HEAD@{30} 7dd8e8f checkout: moving from main to clash",
        "HEAD@{31} 6d7a40b commit: Main work",
        "HEAD@{32} 7dd8e8f checkout: moving from topic to main",
        "HEAD@{33} f7a69be commit: Topic work",
        "HEAD@{34} 7dd8e8f checkout: moving from main to topic",
        "HEAD@{35} 7dd8e8f commit: Add the app",
        "HEAD@{36} 5c9c1cb commit (initial): Initial commit",
    ];

    /// <summary>The reflog scenario's backup refs after the prefix, with the short SHA each points at, in git's order of their names.</summary>
    private static readonly string[] ReflogBackups =
    [
        "abort-20260101T120601000Z 6d7a40b",
        "abort-20260101T120601000Z-changes 08b41a4",
        "branch-20251120T090002000Z/old-idea 9dd61fd",
        "branch-20260101T122701000Z/doomed 44e646b",
        "detached-20260101T121101000Z 7e2ce3b",
        "discard-20260101T122001000Z 9849838",
        "force-push-20260101T120901000Z/origin/topic f7a69be",
        "rebase-20260101T120801000Z/topic f7a69be",
        "remote-20260101T121801000Z/mirror/main 316166a",
        "remote-20260101T121801000Z/mirror/topic 362fbd6",
        "remote-branch-20260101T121701000Z/origin/feature/gone 7dd8e8f",
        "remote-tag-20260101T121601000Z/origin/v0.8 5c9c1cb",
        "reset-20251120T090001000Z 9dd61fd",
        "reset-20260101T121901000Z 316166a",
        "reset-20260101T121901000Z-changes 57a5e6e",
        "reset-20260101T122401000Z d07c677",
        "reset-20260101T122401000Z-changes 21c5a38",
        "skip-20260101T120701000Z 6d7a40b",
        "skip-20260101T120701000Z-changes 946fb4b",
        "stash-20260101T121301000Z 39daf6c",
        "tag-20260101T121401000Z/v0.9 fa4c0b7",
        "tag-undone-20260101T121501000Z/v1.0-rc 7abc965",
    ];

    // ---- Helpers ----------------------------------------------------------------------------------

    /// <summary>What blame's porcelain shows for a line not committed yet (the zero SHA), as these tests write it.</summary>
    private const string NotCommitted = "not committed yet";

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

    private static async Task<string> RevParseAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("rev-parse", "--verify", revision)).StandardOutput.Trim();

    /// <summary>The full SHA of the one commit the branches, remote branches and HEAD reach with this subject.</summary>
    private static async Task<string> ShaOfAsync(TempRepo repo, string subject) =>
        Assert.Single(Lines(await repo.GitAsync("log", "--format=%H %s", "--branches", "--remotes", "HEAD")), line => line[41..] == subject)[..40];

    private static async Task<string> FileAtAsync(TempRepo repo, string revision, string path) =>
        (await repo.GitAsync("show", $"{revision}:{path}")).StandardOutput;

    /// <summary>A blob's bytes, exactly: <c>git cat-file blob</c> read one byte per character.</summary>
    private static async Task<byte[]> BlobAsync(TempRepo repo, string blob)
    {
        var command = new GitCommand("cat-file", "blob", blob) { WorkingDirectory = repo.Path, Environment = repo.Environment, Encoding = GitCommand.Latin1 };
        var output = (await repo.Runner.RunAsync(command, TestCancelled)).EnsureSuccess(command).StandardOutput;
        return GitCommand.Latin1.GetBytes(output);
    }

    /// <summary>A commit's message exactly as git stores it: what <c>git cat-file commit</c> shows after the header.</summary>
    private static async Task<string> MessageAsync(TempRepo repo, string revision)
    {
        var commit = (await repo.GitAsync("cat-file", "commit", revision)).StandardOutput;
        return commit[(commit.IndexOf("\n\n", StringComparison.Ordinal) + 2)..];
    }

    /// <summary>A snapshot commit's message and its parents' short SHAs.</summary>
    private static async Task<(string Message, string Parents)> SnapshotOfAsync(TempRepo repo, string revision) =>
        (await MessageAsync(repo, revision), Subject(await repo.GitAsync("log", "-1", "--format=%p", revision)));

    /// <summary>What a ref points at: its subject (a tag object's message) and its object's type.</summary>
    private static async Task<(string Subject, string Type)> ObjectAsync(TempRepo repo, string fullRef)
    {
        var fields = Subject(await repo.GitAsync("for-each-ref", "--format=%(subject)|%(objecttype)", fullRef)).Split('|');
        return (fields[0], fields[1]);
    }

    private static string WorkingFile(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

    /// <summary>
    /// A file's history as design 0.1's command lists it (<c>--follow --root -M --name-status</c>,
    /// without <c>-z</c>): per commit its subject, its status (with the score of an <c>R</c>) and
    /// the paths, joined by "|".
    /// </summary>
    private static async Task<string[]> FollowAsync(TempRepo repo, string start, string path)
    {
        var output = Lines(await repo.GitAsync("log", "--follow", "--root", "-M", "--name-status", "--no-show-signature", "--no-color", "--format=\u0001%s", start, "--", path));
        var rows = new List<string>();
        foreach (var line in output)
        {
            if (line.StartsWith('\u0001'))
            {
                rows.Add(line[1..]);
            }
            else
            {
                rows[^1] += "|" + line.Replace('\t', '|');
            }
        }

        return [.. rows];
    }

    /// <summary>
    /// A blame (<c>blame --porcelain --no-textconv</c>, of the working tree when
    /// <paramref name="revision"/> is null): each final line's commit by its subject (or
    /// <see cref="NotCommitted"/>), and per subject its author, its <c>previous</c> (SHA and path)
    /// and the path at that commit.
    /// </summary>
    private static async Task<(string[] Lines, Dictionary<string, string> Authors, Dictionary<string, string?> Previous, Dictionary<string, string> Paths)> BlameAsync(TempRepo repo, string? revision, string path)
    {
        string[] arguments = revision is null ? ["blame", "--porcelain", "--no-textconv", "--", path] : ["blame", "--porcelain", "--no-textconv", revision, "--", path];
        var output = (await repo.GitAsync(arguments)).StandardOutput;
        var shaOfLine = new SortedDictionary<int, string>();
        var subjects = new Dictionary<string, string>();
        var authors = new Dictionary<string, string>();
        var previous = new Dictionary<string, string?>();
        var paths = new Dictionary<string, string>();
        var current = string.Empty;
        foreach (var line in output.Split('\n'))
        {
            var fields = line.Split(' ');
            if (line.StartsWith('\t') || line.Length == 0)
            {
                continue;
            }

            if (fields.Length >= 3 && fields[0].Length == 40 && fields[0].All(Uri.IsHexDigit) && int.TryParse(fields[2], out var final))
            {
                current = fields[0];
                shaOfLine[final] = current;
                previous.TryAdd(current, null);
            }
            else if (line.StartsWith("summary ", StringComparison.Ordinal))
            {
                subjects[current] = current.All(digit => digit == '0') ? NotCommitted : line["summary ".Length..];
            }
            else if (line.StartsWith("author ", StringComparison.Ordinal))
            {
                authors[current] = line["author ".Length..];
            }
            else if (line.StartsWith("previous ", StringComparison.Ordinal))
            {
                previous[current] = line["previous ".Length..];
            }
            else if (line.StartsWith("filename ", StringComparison.Ordinal))
            {
                paths[current] = line["filename ".Length..];
            }
        }

        Assert.Equal(Enumerable.Range(1, shaOfLine.Count), shaOfLine.Keys);
        return (
            [.. shaOfLine.Values.Select(sha => subjects[sha])],
            subjects.Where(entry => entry.Value != NotCommitted).ToDictionary(entry => entry.Value, entry => authors[entry.Key]),
            subjects.Where(entry => entry.Value != NotCommitted).ToDictionary(entry => entry.Value, entry => previous[entry.Key]),
            subjects.Where(entry => entry.Value != NotCommitted).ToDictionary(entry => entry.Value, entry => paths[entry.Key]));
    }

    /// <summary>
    /// What a search mode matches among <paramref name="rows"/> (design 3.2): <c>git log
    /// --no-walk=unsorted --stdin</c> with the rows' SHAs, in their order, and the mode's options,
    /// with <paramref name="variables"/> for the call; the matches' subjects.
    /// </summary>
    private static async Task<string[]> SearchAsync(TempRepo repo, IReadOnlyList<string> rows, IReadOnlyDictionary<string, string?> variables, params string[] options)
    {
        string[] arguments = ["log", "--no-walk=unsorted", "--stdin", "--no-show-signature", "--no-color", "--format=%s", .. options];
        var command = new GitCommand(arguments)
        {
            WorkingDirectory = repo.Path,
            Environment = new Dictionary<string, string?>(repo.Environment.Concat(variables)),
            StandardInput = string.Concat(rows.Select(row => row + "\n")),
        };
        return Lines((await repo.Runner.RunAsync(command, TestCancelled)).EnsureSuccess(command));
    }

    /// <summary>
    /// What the Files mode matches among <paramref name="rows"/> for <paramref name="text"/>
    /// (design 3.2's pathspecs, with <c>GIT_LITERAL_PATHSPECS</c> removed), on every git: the rows
    /// whose own change (against the first parent) lists a path, as <c>--name-only</c> shows
    /// them; their subjects.
    /// </summary>
    private static async Task<string[]> FilesSearchAsync(TempRepo repo, IReadOnlyList<string> rows, string text)
    {
        var output = await SearchAsync(repo, rows, WithoutLiteralPathspecs, "--first-parent", "--name-only", "--format=\u0001%s", "--", $":(glob,icase)**/*{text}*", $":(glob,icase)**/*{text}*/**");
        var matches = new List<string>();
        string? subject = null;
        foreach (var line in output)
        {
            if (line.StartsWith('\u0001'))
            {
                subject = line[1..];
            }
            else if (subject is not null)
            {
                matches.Add(subject);
                subject = null;
            }
        }

        return [.. matches];
    }

    /// <summary>No variables of its own for a call.</summary>
    private static readonly IReadOnlyDictionary<string, string?> NoVariables = new Dictionary<string, string?>();

    /// <summary>
    /// The runner's <c>GIT_LITERAL_PATHSPECS</c> removed for a call, as the app removes it for a
    /// search by file name (design 3.2) and for <c>stash push</c> (D108).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string?> WithoutLiteralPathspecs = new Dictionary<string, string?> { ["GIT_LITERAL_PATHSPECS"] = null };

    /// <summary>The two-SHA list of design 6.3: <c>diff-tree -r --name-status -M</c>, its lines in git's order.</summary>
    private static async Task<string[]> NameStatusAsync(TempRepo repo, string from, string to) =>
        Lines(await repo.GitAsync("diff-tree", "-r", "--name-status", "-M", "--no-ext-diff", "--no-color", "--no-commit-id", from, to));

    /// <summary>The subjects of the commits the graph would load with these revision arguments, newest first.</summary>
    private static async Task<string[]> LoadedAsync(TempRepo repo, params string[] revisions) =>
        Lines(await repo.GitAsync(["log", "--date-order", "--format=%s", .. revisions]));

    /// <summary>
    /// Asserts that a checkout of other (<c>switch --no-guess other</c>, as the app checks a branch
    /// out) is refused, on a fresh copy of the protect scenario with <paramref name="change"/>
    /// made: exit code 1, <paramref name="header"/> naming exactly <paramref name="paths"/>, and
    /// nothing changed (design 0.7).
    /// </summary>
    private static async Task AssertCheckoutRefusedAsync(Action<TempRepo> change, string header, string[] paths)
    {
        using var repo = await Scenarios.ProtectAsync();
        change(repo);
        var before = await ExactStateAsync(repo);

        var refused = await repo.GitAllowingFailureAsync("switch", "--no-guess", "other");

        Assert.Equal(1, refused.ExitCode);
        Assert.Equal(paths, BlockedPaths(refused.StandardError, header));
        Assert.Equal(before, await ExactStateAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
    }

    /// <summary>The tab lines under <paramref name="header"/> in git's refusal, without their tab (design 0.7's count).</summary>
    private static string[] BlockedPaths(string text, string header)
    {
        var lines = text.Split('\n');
        var start = Array.IndexOf(lines, header);
        Assert.True(start >= 0, $"Git did not say \"{header}\". It said:\n{text}");
        return [.. lines.Skip(start + 1).TakeWhile(line => line.StartsWith('\t')).Select(line => line[1..])];
    }

    /// <summary>
    /// Everything a refused operation must leave as it was: HEAD, every ref, the index's entries,
    /// the status with the ignored files, and the working tree's files and their contents.
    /// </summary>
    private static async Task<string> ExactStateAsync(TempRepo repo)
    {
        var head = Subject(await repo.GitAsync("rev-parse", "HEAD"));
        var refs = (await repo.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput;
        var index = (await repo.GitAsync("ls-files", "--stage")).StandardOutput;
        var status = (await repo.GitAsync("status", "--porcelain=v2", "--untracked-files=all", "--ignored")).StandardOutput;
        var files = Directory.EnumerateFiles(repo.Path, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(repo.Path, file).StartsWith(".git" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(file => $"{Path.GetRelativePath(repo.Path, file)}: {File.ReadAllText(file)}");
        return string.Join('\n', [head, refs, index, status, .. files]);
    }

    /// <summary>Which of the files and folders that mark an operation in progress the git folder holds, in ordinal order.</summary>
    private static string[] InProgressFiles(TempRepo repo) =>
        [.. new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "sequencer", "rebase-merge", "rebase-apply" }
            .Where(name => File.Exists(Path.Combine(repo.Path, ".git", name)) || Directory.Exists(Path.Combine(repo.Path, ".git", name)))
            .Order(StringComparer.Ordinal)];
}
