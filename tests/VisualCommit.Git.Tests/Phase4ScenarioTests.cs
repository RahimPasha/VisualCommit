using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;
using static VisualCommit.Git.Tests.ScenarioTestSupport;
using Files = VisualCommit.Testing.Scenarios.ConflictsFiles;

namespace VisualCommit.Git.Tests;

/// <summary>
/// Pins down phase 4's scenario repos (docs/phase-4-design.md 13.1): their graphs as the app lays
/// them out, refs and files; each stopped state of <see cref="Scenarios.ConflictsStoppedAsync"/>
/// with its status and conflict blocks; and, with real git, every property the visual checks rely
/// on: where each rebase, pick and merge stops, what taking a side then continuing does, the
/// interactive plan of check B7 applied by a todo as the app writes one (design 2.5), and the fake
/// merge tool. A class of its own beside <see cref="Phase3ScenarioTests"/>, so that xunit runs them
/// side by side. The SHAs come from <see cref="TempRepo"/>'s clock, the same on every machine.
/// </summary>
public class Phase4ScenarioTests
{
    // ---- RebaseAsync ----------------------------------------------------------------------------

    [Fact]
    public async Task Rebase_has_its_graph_in_the_documented_rows_and_lanes()
    {
        using var repo = await Scenarios.RebaseAsync();

        var (rows, lanes) = await GraphOfAsync(repo);

        Assert.Equal(
            [
                "2d4d79a Other one · lane 0",
                "1dd58b8 Main more · lane 1",
                "dd6a6fc Main work · lane 1",
                "b0dfa3f Topic two · lane 2",
                "1219e45 Topic one · lane 2",
                "996fdc1 Add core · lane 0 · ends 1, 2",
                "7bce6bf Initial commit · lane 0",
            ],
            rows);
        Assert.Equal(3, lanes);
        Assert.Equal(
            ["Other one|12:06", "Main more|12:05", "Main work|12:04", "Topic two|12:03", "Topic one|12:02", "Add core|12:01", "Initial commit|12:00"],
            Lines(await repo.GitAsync("log", "--date-order", "--format=%s|%ad", "--date=format:%H:%M", "--branches", "--remotes", "HEAD")));
    }

    [Fact]
    public async Task Rebase_has_its_branches_remote_and_files()
    {
        using var repo = await Scenarios.RebaseAsync();

        Assert.Equal(
            [
                "refs/heads/feature/other 2d4d79a  ",
                "refs/heads/feature/topic b0dfa3f refs/remotes/origin/feature/topic ",
                "refs/heads/main 1dd58b8 refs/remotes/origin/main ",
                "refs/remotes/origin/feature/topic b0dfa3f  ",
                "refs/remotes/origin/main 1dd58b8  ",
            ],
            await RefsAsync(repo));
        Assert.Equal("feature/topic", await repo.CurrentBranchAsync());
        Assert.Empty(await StatusAsync(repo));
        Assert.Equal(
            ["refs/heads/feature/topic b0dfa3f", "refs/heads/main 1dd58b8"],
            Lines(await repo.GitAsync("ls-remote", "--heads", "origin")).Select(line => $"{line.Split('\t')[1]} {line[..7]}"));
        AssertSameFolder(repo.SiblingPath("origin.git"), Path.GetFullPath((await repo.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim()));

        Assert.Equal(Scenarios.RebaseFiles.Readme, await FileAtAsync(repo, "refs/heads/main", "README.md"));
        Assert.Equal(Scenarios.RebaseFiles.Core, await FileAtAsync(repo, "refs/heads/feature/topic", "src/core.txt"));
        Assert.Equal(Scenarios.RebaseFiles.CoreMainWork, await FileAtAsync(repo, "refs/heads/main", "src/core.txt"));
        Assert.Equal(Scenarios.RebaseFiles.MainMore, await FileAtAsync(repo, "refs/heads/main", "docs/main.txt"));
        Assert.Equal(Scenarios.RebaseFiles.TopicOne, await FileAtAsync(repo, "refs/heads/feature/topic", "topic/one.txt"));
        Assert.Equal(Scenarios.RebaseFiles.TopicTwo, await FileAtAsync(repo, "refs/heads/feature/topic", "topic/two.txt"));
        Assert.Equal(Scenarios.RebaseFiles.OtherOne, await FileAtAsync(repo, "refs/heads/feature/other", "other/one.txt"));
        Assert.Equal(["other/one.txt"], await ChangedFilesAsync(repo, "refs/heads/feature/other~1", "refs/heads/feature/other"));
    }

    [Fact]
    public async Task Rebase_of_feature_topic_and_of_feature_other_onto_main_has_no_conflict()
    {
        using var repo = await Scenarios.RebaseAsync();

        // As a terminal would, on the checked-out feature/topic (checks A2, A4's sync aside).
        var topic = await repo.GitAllowingFailureAsync("rebase", "main");
        Assert.True(topic.Succeeded, topic.StandardError);
        Assert.Equal("feature/topic", await repo.CurrentBranchAsync());
        Assert.Equal(
            ["Topic two", "Topic one", "Main more", "Main work", "Add core", "Initial commit"],
            Lines(await repo.GitAsync("log", "--format=%s", "refs/heads/feature/topic")));
        Assert.Empty(InProgressFiles(repo));

        // feature/other, not checked out, rebased after a checkout (check A3).
        await repo.CheckoutAsync("feature/other");
        var other = await repo.GitAllowingFailureAsync("rebase", "main");
        Assert.True(other.Succeeded, other.StandardError);
        Assert.Equal(
            ["Other one", "Main more", "Main work", "Add core", "Initial commit"],
            Lines(await repo.GitAsync("log", "--format=%s", "refs/heads/feature/other")));
        Assert.Empty(await StatusAsync(repo));
        Assert.Empty(InProgressFiles(repo));
    }

    // ---- InteractiveAsync -----------------------------------------------------------------------

    [Fact]
    public async Task Interactive_has_its_line_of_history_and_three_keeps_its_hash_line()
    {
        using var repo = await Scenarios.InteractiveAsync();

        var (rows, lanes) = await GraphOfAsync(repo);
        Assert.Equal(
            [
                "57f089d Five · lane 0",
                "eef5c15 Four · lane 0",
                "ff42e7f Three · lane 0",
                "ed58902 Fix one · lane 0",
                "938844a Two · lane 0",
                "fc6c8fd One · lane 0",
                "4b4622c Base · lane 0",
                "da32da3 Initial commit · lane 0",
            ],
            rows);
        Assert.Equal(1, lanes);
        Assert.Equal(
            ["Five|12:07", "Four|12:06", "Three|12:05", "Fix one|12:04", "Two|12:03", "One|12:02", "Base|12:01", "Initial commit|12:00"],
            Lines(await repo.GitAsync("log", "--format=%s|%ad", "--date=format:%H:%M", "HEAD")));
        Assert.Equal(["refs/heads/main 57f089d  "], await RefsAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty(await StatusAsync(repo));
        Assert.Empty(Lines(await repo.GitAsync("remote")));

        Assert.Equal(Scenarios.InteractiveFiles.ThreeMessage, await MessageAsync(repo, "refs/heads/main~2"));
        Assert.Equal(Scenarios.InteractiveFiles.One, await FileAtAsync(repo, "refs/heads/main~5", "src/one.txt"));
        Assert.Equal(Scenarios.InteractiveFiles.OneFixed, await FileAtAsync(repo, "refs/heads/main", "src/one.txt"));
        Assert.Equal(Scenarios.InteractiveFiles.Base, await FileAtAsync(repo, "refs/heads/main", "src/base.txt"));
        Assert.Equal(Scenarios.InteractiveFiles.Two, await FileAtAsync(repo, "refs/heads/main", "src/two.txt"));
        Assert.Equal(Scenarios.InteractiveFiles.Three, await FileAtAsync(repo, "refs/heads/main", "src/three.txt"));
        Assert.Equal(Scenarios.InteractiveFiles.Four, await FileAtAsync(repo, "refs/heads/main", "src/four.txt"));
        Assert.Equal(Scenarios.InteractiveFiles.Five, await FileAtAsync(repo, "refs/heads/main", "src/five.txt"));
        foreach (var (revision, file) in new[] { ("main~6", "src/base.txt"), ("main~5", "src/one.txt"), ("main~4", "src/two.txt"), ("main~3", "src/one.txt"), ("main~2", "src/three.txt"), ("main~1", "src/four.txt"), ("main", "src/five.txt") })
        {
            Assert.Equal([file], await ChangedFilesAsync(repo, revision + "~1", revision));
        }
    }

    /// <summary>
    /// Check B7's plan (design 13.4) through git: Fix one moved right above One and made its
    /// fixup, Four squashed into Three with an edited message that keeps "#42 is the issue", Two
    /// reworded, Five dropped. The todo and the messages are what the app writes (design 2.5:
    /// every group and reword ends with the guarded amend of its message file), handed to git by a
    /// sequence editor that copies them, as the app's does.
    /// </summary>
    [Fact]
    public async Task Interactive_plan_of_check_B7_applies_through_the_apps_kind_of_todo_without_a_conflict()
    {
        using var repo = await Scenarios.InteractiveAsync();
        var sha = await ShasBySubjectAsync(repo);
        const string TwoReworded = "Two reworded\n";
        const string ThreeAndFour = "Three and four\n\n#42 is the issue\n";
        var oneMessage = await MessageAsync(repo, sha["One"]);

        using var folder = new TempDirectory("vc todo's");
        var result = await RebaseWithTodoAsync(
            repo,
            folder,
            sha["Base"],
            [oneMessage, TwoReworded, ThreeAndFour],
            [
                "label vc-1",
                $"pick {sha["One"]}",
                $"fixup {sha["Fix one"]}",
                GuardedAmend(1),
                "label vc-2",
                $"pick {sha["Two"]}",
                GuardedAmend(2),
                "label vc-3",
                $"pick {sha["Three"]}",
                $"fixup {sha["Four"]}",
                GuardedAmend(3),
                $"drop {sha["Five"]}",
            ]);

        Assert.True(result.Succeeded, result.StandardError);
        Assert.Empty(InProgressFiles(repo));
        Assert.Empty(await StatusAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty(Lines(await repo.GitAsync("for-each-ref", "refs/rewritten/")));

        Assert.Equal(["Three and four", "Two reworded", "One", "Base", "Initial commit"], Lines(await repo.GitAsync("log", "--format=%s", "HEAD")));
        Assert.Equal(sha["Base"], (await repo.GitAsync("rev-parse", "HEAD~3")).StandardOutput.Trim());
        Assert.Equal(ThreeAndFour, await MessageAsync(repo, "HEAD"));
        Assert.Equal(TwoReworded, await MessageAsync(repo, "HEAD~1"));
        Assert.Equal("One\n", await MessageAsync(repo, "HEAD~2"));

        // The commits keep their authors' dates (the targets'), and hold what their groups made.
        Assert.Equal(["12:05", "12:03", "12:02"], Lines(await repo.GitAsync("log", "-3", "--format=%ad", "--date=format:%H:%M", "HEAD")));
        Assert.Equal(["src/four.txt", "src/three.txt"], await ChangedFilesAsync(repo, "HEAD~1", "HEAD"));
        Assert.Equal(["src/two.txt"], await ChangedFilesAsync(repo, "HEAD~2", "HEAD~1"));
        Assert.Equal(["src/one.txt"], await ChangedFilesAsync(repo, "HEAD~3", "HEAD~2"));
        Assert.Equal(Scenarios.InteractiveFiles.OneFixed, await FileAtAsync(repo, "HEAD~2", "src/one.txt"));
        Assert.False((await repo.GitAllowingFailureAsync("cat-file", "-e", "HEAD:src/five.txt")).Succeeded, "Five was not dropped.");
    }

    [Fact]
    public async Task Interactive_reorders_apply_except_fix_one_below_one()
    {
        using var reordered = await Scenarios.InteractiveAsync();
        var sha = await ShasBySubjectAsync(reordered);
        using var folder = new TempDirectory("vc todo's");

        // Every commit in the opposite order, One still before Fix one: no conflict.
        var clean = await RebaseWithTodoAsync(
            reordered,
            folder,
            sha["Base"],
            [],
            [.. new[] { "Five", "Four", "Three", "Two", "One", "Fix one" }.Select(subject => $"pick {sha[subject]}")]);
        Assert.True(clean.Succeeded, clean.StandardError);
        Assert.Equal(["Fix one", "One", "Two", "Three", "Four", "Five", "Base", "Initial commit"], Lines(await reordered.GitAsync("log", "--format=%s", "HEAD")));
        Assert.Empty(InProgressFiles(reordered));

        // Fix one below One: git cannot apply it, as src/one.txt does not exist yet.
        using var broken = await Scenarios.InteractiveAsync();
        using var brokenFolder = new TempDirectory("vc todo's");
        var stopped = await RebaseWithTodoAsync(
            broken,
            brokenFolder,
            sha["Base"],
            [],
            [.. new[] { "Fix one", "One", "Two", "Three", "Four", "Five" }.Select(subject => $"pick {sha[subject]}")]);
        Assert.False(stopped.Succeeded);
        Assert.Contains("rebase-merge", InProgressFiles(broken));
        Assert.Equal(["DU src/one.txt"], await StatusAsync(broken));
        Assert.Equal(sha["Fix one"], ReadGitFile(broken, "rebase-merge/stopped-sha")?.Trim());
    }

    // ---- ConflictsAsync -------------------------------------------------------------------------

    [Fact]
    public async Task Conflicts_has_its_graph_in_the_documented_rows_and_lanes()
    {
        using var repo = await Scenarios.ConflictsAsync();

        var (rows, lanes) = await GraphOfAsync(repo);

        Assert.Equal(ConflictsGraph, rows);
        Assert.Equal(5, lanes);
        Assert.Equal(
            ["Ours edits|12:09", "Kinds edits|12:08", "Pick last|12:07", "Pick conflicting|12:06", "Pick clean|12:05", "Second change|12:04", "First change|12:03", "Theirs edits|12:02", "Base|12:01", "Initial commit|12:00"],
            Lines(await repo.GitAsync("log", "--date-order", "--format=%s|%ad", "--date=format:%H:%M", "--branches", "HEAD")));
    }

    [Fact]
    public async Task Conflicts_has_its_branches_and_the_documented_files_on_each_branch()
    {
        using var repo = await Scenarios.ConflictsAsync();

        Assert.Equal(ConflictsRefs, await RefsAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Empty(await StatusAsync(repo));
        Assert.Empty(Lines(await repo.GitAsync("remote")));

        Assert.Equal(Files.Readme, await FileAtAsync(repo, "refs/heads/main", "README.md"));
        Assert.Equal(Files.Notes, await FileAtAsync(repo, "refs/heads/main~1", "notes.txt"));
        Assert.Equal(Files.Other, await FileAtAsync(repo, "refs/heads/main~1", "other.txt"));
        Assert.Equal(Files.ImageBase, await BlobAsync(repo, "refs/heads/main~1:image.png"));

        Assert.Equal(Files.NotesOurs, await FileAtAsync(repo, "refs/heads/main", "notes.txt"));
        Assert.Equal(Files.OtherOurs, await FileAtAsync(repo, "refs/heads/main", "other.txt"));
        Assert.Equal(Files.ImageOurs, await BlobAsync(repo, "refs/heads/main:image.png"));
        Assert.Equal(Files.NewOurs, await FileAtAsync(repo, "refs/heads/main", "new.txt"));

        Assert.Equal(Files.NotesTheirs, await FileAtAsync(repo, "refs/heads/feature/theirs", "notes.txt"));
        Assert.Equal(Files.NotesFirst, await FileAtAsync(repo, "refs/heads/feature/rebase~1", "notes.txt"));
        Assert.Equal(Files.NotesSecond, await FileAtAsync(repo, "refs/heads/feature/rebase", "notes.txt"));
        Assert.Equal(Files.Pick, await FileAtAsync(repo, "refs/heads/feature/picks~2", "pick.txt"));
        Assert.Equal(Files.NotesPicked, await FileAtAsync(repo, "refs/heads/feature/picks~1", "notes.txt"));
        Assert.Equal(Files.Last, await FileAtAsync(repo, "refs/heads/feature/picks", "last.txt"));
        Assert.Equal(Files.ImageKinds, await BlobAsync(repo, "refs/heads/feature/kinds:image.png"));
        Assert.Equal(Files.NewKinds, await FileAtAsync(repo, "refs/heads/feature/kinds", "new.txt"));
        Assert.False((await repo.GitAllowingFailureAsync("cat-file", "-e", "refs/heads/feature/kinds:other.txt")).Succeeded, "feature/kinds still has other.txt.");

        // What each commit touches: feature/theirs, feature/rebase and feature/picks leave other.txt, image.png and new.txt alone.
        Assert.Equal(["image.png", "notes.txt", "other.txt"], await ChangedFilesAsync(repo, "refs/heads/main~2", "refs/heads/main~1"));
        Assert.Equal(["image.png", "new.txt", "notes.txt", "other.txt"], await ChangedFilesAsync(repo, "refs/heads/main~1", "refs/heads/main"));
        Assert.Equal(["notes.txt"], await ChangedFilesAsync(repo, "refs/heads/main~1", "refs/heads/feature/theirs"));
        Assert.Equal(["notes.txt"], await ChangedFilesAsync(repo, "refs/heads/main~1", "refs/heads/feature/rebase~1"));
        Assert.Equal(["notes.txt"], await ChangedFilesAsync(repo, "refs/heads/feature/rebase~1", "refs/heads/feature/rebase"));
        Assert.Equal(["pick.txt"], await ChangedFilesAsync(repo, "refs/heads/main~1", "refs/heads/feature/picks~2"));
        Assert.Equal(["notes.txt"], await ChangedFilesAsync(repo, "refs/heads/feature/picks~2", "refs/heads/feature/picks~1"));
        Assert.Equal(["last.txt"], await ChangedFilesAsync(repo, "refs/heads/feature/picks~1", "refs/heads/feature/picks"));
        Assert.Equal(["image.png", "new.txt", "other.txt"], await ChangedFilesAsync(repo, "refs/heads/main~1", "refs/heads/feature/kinds"));

        // Git takes the picture for binary: no lines are counted for it.
        Assert.Equal(["-\t-\timage.png"], Lines(await repo.GitAsync("diff", "--numstat", "refs/heads/main~1", "refs/heads/feature/kinds", "--", "image.png")));
    }

    // ---- ConflictsStoppedAsync ------------------------------------------------------------------

    [Fact]
    public async Task Stopped_merge_has_notes_both_modified_with_two_separate_blocks()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);

        Assert.Equal(["UU notes.txt"], await StatusAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal("da31c97 Ours edits", await LogOneAsync(repo, "HEAD"));
        Assert.Equal(["MERGE_HEAD", "MERGE_MSG"], InProgressFiles(repo));
        Assert.Equal(await repo.RefTargetAsync("refs/heads/feature/theirs"), ReadGitFile(repo, "MERGE_HEAD")?.Trim());
        Assert.Equal("Merge branch 'feature/theirs'\n\n# Conflicts:\n#\tnotes.txt\n", ReadGitFile(repo, "MERGE_MSG"));

        // Two blocks seven lines apart; line 6 (theirs only) merged cleanly between them.
        Assert.Equal(Files.NotesMergeConflicted, WorkingFile(repo, "notes.txt"));
        Assert.Equal(2, BlockCount(WorkingFile(repo, "notes.txt")));
        await AssertStagesAsync(repo, "notes.txt", ("1", "refs/heads/main~1"), ("2", "refs/heads/main"), ("3", "refs/heads/feature/theirs"));
        Assert.Equal(Files.OtherOurs, WorkingFile(repo, "other.txt"));
        Assert.Equal(Files.NewOurs, WorkingFile(repo, "new.txt"));
    }

    [Fact]
    public async Task Stopped_rebase_stops_at_first_change_with_one_block_and_again_at_second_change_after_taking_theirs()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Rebase);
        var first = await RevParseAsync(repo, "refs/heads/feature/rebase~1");
        var second = await RevParseAsync(repo, "refs/heads/feature/rebase");
        var main = await RevParseAsync(repo, "refs/heads/main");

        // The first stop: HEAD detached on main, First change being replayed (1 of 2).
        Assert.Equal(["UU notes.txt"], await StatusAsync(repo));
        Assert.Equal(main, await repo.HeadAsync());
        Assert.False((await repo.GitAllowingFailureAsync("symbolic-ref", "--quiet", "HEAD")).Succeeded, "HEAD is detached during a rebase.");
        Assert.Equal("305f7f7 Second change", await LogOneAsync(repo, "refs/heads/feature/rebase"));
        Assert.Equal("70e8c78", first[..7]);
        Assert.Equal("refs/heads/feature/rebase", ReadGitFile(repo, "rebase-merge/head-name")?.Trim());
        Assert.Equal(main, ReadGitFile(repo, "rebase-merge/onto")?.Trim());
        Assert.Equal(second, ReadGitFile(repo, "rebase-merge/orig-head")?.Trim());
        Assert.Equal(first, ReadGitFile(repo, "rebase-merge/stopped-sha")?.Trim());
        Assert.Equal(first, ReadGitFile(repo, "REBASE_HEAD")?.Trim());
        Assert.Equal("1", ReadGitFile(repo, "rebase-merge/msgnum")?.Trim());
        Assert.Equal("2", ReadGitFile(repo, "rebase-merge/end")?.Trim());
        // The id's length and anything after it are git's to choose.
        AssertPick(Assert.Single(TodoLines(ReadGitFile(repo, "rebase-merge/done"))), first);
        AssertPick(Assert.Single(TodoLines(ReadGitFile(repo, "rebase-merge/git-rebase-todo"))), second);
        Assert.Equal(["MERGE_MSG", "rebase-merge"], InProgressFiles(repo));
        Assert.Equal("First change\n\n# Conflicts:\n#\tnotes.txt\n", ReadGitFile(repo, "MERGE_MSG"));
        Assert.Equal(Files.NotesRebaseFirstConflicted("70e8c78"), WorkingFile(repo, "notes.txt"));
        Assert.Equal(1, BlockCount(WorkingFile(repo, "notes.txt")));
        await AssertStagesAsync(repo, "notes.txt", ("1", "refs/heads/main~1"), ("2", "refs/heads/main"), ("3", first));

        // Its theirs side taken (the user's commit), marked resolved and continued: the second stop.
        File.WriteAllText(Path.Combine(repo.Path, "notes.txt"), Files.NotesRebaseFirstResolved);
        await repo.GitAsync("add", "--", "notes.txt");
        var continued = await repo.GitAllowingFailureAsync("rebase", "--continue");
        Assert.False(continued.Succeeded, "The rebase did not stop at Second change.");
        Assert.Equal(["UU notes.txt"], await StatusAsync(repo));
        Assert.Equal(second, ReadGitFile(repo, "rebase-merge/stopped-sha")?.Trim());
        Assert.Equal("2", ReadGitFile(repo, "rebase-merge/msgnum")?.Trim());
        Assert.Equal("2", ReadGitFile(repo, "rebase-merge/end")?.Trim());
        Assert.Empty(TodoLines(ReadGitFile(repo, "rebase-merge/git-rebase-todo")));
        Assert.Equal(["First change", "Ours edits"], Lines(await repo.GitAsync("log", "-2", "--format=%s", "HEAD")));
        Assert.Equal("731d9d6 First change", await LogOneAsync(repo, "HEAD"));
        Assert.Equal(Files.NotesRebaseFirstResolved, await FileAtAsync(repo, "HEAD", "notes.txt"));
        Assert.Equal(Files.NotesRebaseSecondConflicted("305f7f7"), WorkingFile(repo, "notes.txt"));
        Assert.Equal(1, BlockCount(WorkingFile(repo, "notes.txt")));
        Assert.Equal("refs/heads/feature/rebase", ReadGitFile(repo, "rebase-merge/head-name")?.Trim());
    }

    [Fact]
    public async Task Stopped_cherry_picks_stop_at_the_second_with_one_more_waiting()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.CherryPicks);
        var conflicting = await RevParseAsync(repo, "refs/heads/feature/picks~1");
        var last = await RevParseAsync(repo, "refs/heads/feature/picks");

        Assert.Equal(["UU notes.txt"], await StatusAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal(["d71cdc4 Pick clean", "da31c97 Ours edits"], Lines(await repo.GitAsync("log", "-2", "--format=%h %s", "refs/heads/main")));
        Assert.Equal(Files.Pick, await FileAtAsync(repo, "HEAD", "pick.txt"));
        Assert.Equal(conflicting, ReadGitFile(repo, "CHERRY_PICK_HEAD")?.Trim());
        Assert.Equal("Pick conflicting\n\n# Conflicts:\n#\tnotes.txt\n", ReadGitFile(repo, "MERGE_MSG"));
        Assert.Equal(["CHERRY_PICK_HEAD", "MERGE_MSG", "sequencer"], InProgressFiles(repo));

        // The stopped pick stays the todo's first line; Pick last waits after it.
        var todo = TodoLines(ReadGitFile(repo, "sequencer/todo"));
        Assert.Equal(2, todo.Length);
        AssertPick(todo[0], conflicting);
        AssertPick(todo[1], last);
        Assert.Equal(await RevParseAsync(repo, "refs/heads/main~1"), ReadGitFile(repo, "sequencer/head")?.Trim());

        Assert.Equal(Files.NotesPicksConflicted("01fd976"), WorkingFile(repo, "notes.txt"));
        Assert.Equal(1, BlockCount(WorkingFile(repo, "notes.txt")));
        Assert.False(File.Exists(Path.Combine(repo.Path, "last.txt")), "Pick last was picked already.");
    }

    [Fact]
    public async Task Stopped_kinds_merge_has_a_file_deleted_by_them_a_binary_file_and_a_file_added_by_both()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.MergeKinds);

        Assert.Equal(["UU image.png", "AA new.txt", "UD other.txt"], await StatusAsync(repo));
        Assert.Equal("main", await repo.CurrentBranchAsync());
        Assert.Equal("da31c97 Ours edits", await LogOneAsync(repo, "HEAD"));
        Assert.Equal(["MERGE_HEAD", "MERGE_MSG"], InProgressFiles(repo));
        Assert.Equal("Merge branch 'feature/kinds'\n\n# Conflicts:\n#\timage.png\n#\tnew.txt\n#\tother.txt\n", ReadGitFile(repo, "MERGE_MSG"));

        // Deleted by them: ours stays in the working tree; no stage 3.
        await AssertStagesAsync(repo, "other.txt", ("1", "refs/heads/main~1"), ("2", "refs/heads/main"));
        Assert.Equal(Files.OtherOurs, WorkingFile(repo, "other.txt"));

        // Binary, both modified: ours in the working tree byte for byte, no markers.
        await AssertStagesAsync(repo, "image.png", ("1", "refs/heads/main~1"), ("2", "refs/heads/main"), ("3", "refs/heads/feature/kinds"));
        Assert.Equal(Files.ImageOurs, File.ReadAllBytes(Path.Combine(repo.Path, "image.png")));

        // Both added: no stage 1, the common first line, then one block.
        await AssertStagesAsync(repo, "new.txt", ("2", "refs/heads/main"), ("3", "refs/heads/feature/kinds"));
        Assert.Equal(Files.NewKindsConflicted, WorkingFile(repo, "new.txt"));
        Assert.Equal(1, BlockCount(WorkingFile(repo, "new.txt")));

        Assert.Equal(Files.NotesOurs, WorkingFile(repo, "notes.txt"));
    }

    [Fact]
    public async Task Fake_merge_tool_writes_its_content_and_git_stages_the_file()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge, mergeTool: true);

        Assert.Equal(Scenarios.FakeMergeTool, (await repo.GitAsync("config", "--get", "merge.tool")).StandardOutput.Trim());
        Assert.Equal("true", (await repo.GitAsync("config", "--get", "mergetool.fake.trustExitCode")).StandardOutput.Trim());
        Assert.Equal(
            "sh \"$(git rev-parse --git-common-dir)/vc-fake-merge-tool.sh\" \"$MERGED\"",
            (await repo.GitAsync("config", "--get", "mergetool.fake.cmd")).StandardOutput.Trim());
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", Scenarios.FakeMergeToolScript)));
        Assert.Equal(["UU notes.txt"], await StatusAsync(repo));

        // As the app runs it (design 0.4, 11.9): -c before the subcommand, the tool by name, no prompt.
        var tool = await repo.GitAllowingFailureAsync("-c", "mergetool.keepBackup=false", "mergetool", "--tool=fake", "--no-prompt", "--", "notes.txt");

        Assert.True(tool.Succeeded, tool.StandardError + tool.StandardOutput);
        Assert.Equal(Files.FakeMergeToolResult, WorkingFile(repo, "notes.txt"));
        Assert.Equal(["M  notes.txt"], await StatusAsync(repo));
        Assert.Equal(["MERGE_HEAD", "MERGE_MSG"], InProgressFiles(repo));
    }

    [Fact]
    public async Task Without_the_option_no_merge_tool_is_configured()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.CherryPicks);

        Assert.False((await repo.GitAllowingFailureAsync("config", "--get", "merge.tool")).Succeeded);
        Assert.False(File.Exists(Path.Combine(repo.Path, ".git", Scenarios.FakeMergeToolScript)));
    }

    // ---- TempRepo.GitWithEnvironmentAsync -------------------------------------------------------

    [Fact]
    public async Task A_call_with_its_own_variables_keeps_the_clocks_dates_and_the_isolation()
    {
        using var repo = await TempRepo.CreateAsync();
        var seconds = repo.ClockTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var ident = await repo.GitWithEnvironmentAsync(new Dictionary<string, string?> { ["GIT_COMMITTER_NAME"] = "Someone Else" }, "var", "GIT_COMMITTER_IDENT");
        Assert.Equal($"Someone Else <{TempRepo.AuthorEmail}> {seconds} +0000", ident.StandardOutput.Trim());
        var author = await repo.GitWithEnvironmentAsync(new Dictionary<string, string?>(), "var", "GIT_AUTHOR_IDENT");
        Assert.Equal($"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}> {seconds} +0000", author.StandardOutput.Trim());

        // The isolation stays: no editor of the machine, and a null value removes a variable.
        var editor = await repo.GitWithEnvironmentAsync(new Dictionary<string, string?> { ["GIT_COMMITTER_NAME"] = null }, "var", "GIT_EDITOR");
        Assert.Equal(":", editor.StandardOutput.Trim());

        var failed = await repo.GitWithEnvironmentAllowingFailureAsync(new Dictionary<string, string?>(), "rev-parse", "--verify", "--quiet", "refs/heads/none");
        Assert.False(failed.Succeeded);
        await Assert.ThrowsAnyAsync<Exception>(() => repo.GitWithEnvironmentAsync(new Dictionary<string, string?>(), "rev-parse", "--verify", "--quiet", "refs/heads/none"));
    }

    // ---- The pinned conflicts graph ---------------------------------------------------------------

    /// <summary>The conflicts scenario's graph, unchanged by a stop that makes no commit (a merge, the rebase's first stop, the kinds merge).</summary>
    private static readonly string[] ConflictsGraph =
    [
        "da31c97 Ours edits · lane 0",
        "3a9de3e Kinds edits · lane 1",
        "5c4c257 Pick last · lane 2",
        "01fd976 Pick conflicting · lane 2",
        "01d2046 Pick clean · lane 2",
        "305f7f7 Second change · lane 3",
        "70e8c78 First change · lane 3",
        "5c77ffb Theirs edits · lane 4",
        "195768b Base · lane 0 · ends 1, 2, 3, 4",
        "6bd579f Initial commit · lane 0",
    ];

    private static readonly string[] ConflictsRefs =
    [
        "refs/heads/feature/kinds 3a9de3e  ",
        "refs/heads/feature/picks 5c4c257  ",
        "refs/heads/feature/rebase 305f7f7  ",
        "refs/heads/feature/theirs 5c77ffb  ",
        "refs/heads/main da31c97  ",
    ];

    [Fact]
    public async Task Stopped_states_keep_the_graph_except_the_picked_commit()
    {
        foreach (var kind in new[] { Scenarios.ConflictsStop.Merge, Scenarios.ConflictsStop.Rebase, Scenarios.ConflictsStop.MergeKinds })
        {
            using var repo = await Scenarios.ConflictsStoppedAsync(kind);
            Assert.Equal(ConflictsGraph, (await GraphOfAsync(repo)).Rows);
            Assert.Equal(ConflictsRefs, await RefsAsync(repo));
        }

        using var picks = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.CherryPicks);
        string[] picked = ["d71cdc4 Pick clean · lane 0", .. ConflictsGraph];
        Assert.Equal(picked, (await GraphOfAsync(picks)).Rows);
        Assert.Equal(["Pick clean|12:05|12:10"], Lines(await picks.GitAsync("log", "-1", "--format=%s|%ad|%cd", "--date=format:%H:%M", "HEAD")));
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

    /// <summary>The full SHA a revision names.</summary>
    private static async Task<string> RevParseAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("rev-parse", "--verify", revision)).StandardOutput.Trim();

    private static async Task<string> LogOneAsync(TempRepo repo, string revision) =>
        (await repo.GitAsync("log", "-1", "--format=%h %s", revision)).StandardOutput.Trim();

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

    /// <summary>The paths a commit range changes, in git's order.</summary>
    private static async Task<string[]> ChangedFilesAsync(TempRepo repo, string from, string to) =>
        Lines(await repo.GitAsync("diff", "--name-only", from, to));

    /// <summary>The full SHA of every commit HEAD reaches, by its subject.</summary>
    private static async Task<Dictionary<string, string>> ShasBySubjectAsync(TempRepo repo) =>
        Lines(await repo.GitAsync("log", "--format=%H %s", "HEAD"))
            .Select(line => line.Split(' ', 2))
            .ToDictionary(fields => fields[1], fields => fields[0]);

    /// <summary>A working-tree file's text as git wrote it.</summary>
    private static string WorkingFile(TempRepo repo, string path) => File.ReadAllText(Path.Combine(repo.Path, path));

    /// <summary>How many conflict blocks a text has: lines that start with git's seven "&lt;" and a space.</summary>
    private static int BlockCount(string text) => text.Split('\n').Count(line => line.StartsWith("<<<<<<< ", StringComparison.Ordinal));

    /// <summary>
    /// Asserts the index's stages of a conflicted path (<c>git ls-files -u</c>): exactly these
    /// stage numbers, each holding the blob <paramref name="stages"/> names by revision (its
    /// version of the path) or by a commit's full SHA.
    /// </summary>
    private static async Task AssertStagesAsync(TempRepo repo, string path, params (string Stage, string? Revision)[] stages)
    {
        var actual = Lines(await repo.GitAsync("ls-files", "-u", "--", path)).Select(line => line.Split('\t')[0].Split(' ')).Select(fields => (Stage: fields[2], Blob: fields[1])).ToList();
        Assert.Equal(stages.Select(stage => stage.Stage), actual.Select(stage => stage.Stage));
        foreach (var (stage, revision) in stages)
        {
            var blob = (await repo.GitAsync("rev-parse", $"{revision}:{path}")).StandardOutput.Trim();
            Assert.Equal(blob, actual.Single(entry => entry.Stage == stage).Blob);
        }
    }

    /// <summary>
    /// Runs <c>git rebase -i <paramref name="onto"/></c> with the todo <paramref name="todo"/>
    /// (git's order, oldest first) and the message files <paramref name="messages"/>, delivered as
    /// the app delivers them (design 2.5): written as <c>todo</c> and <c>vc-message-&lt;n&gt;</c>
    /// (UTF-8 without a BOM, LF) into <paramref name="folder"/>, and a <c>GIT_SEQUENCE_EDITOR</c>
    /// for this call only that is a shell function copying the messages next to git's todo and the
    /// todo over it (git appends <c>"$@"</c>, which becomes the function's argument).
    /// </summary>
    private static async Task<GitResult> RebaseWithTodoAsync(TempRepo repo, TempDirectory folder, string onto, IReadOnlyList<string> messages, IReadOnlyList<string> todo)
    {
        var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var todoFile = folder.Combine("todo");
        File.WriteAllText(todoFile, string.Concat(todo.Select(line => line + "\n")), utf8);
        var steps = new List<string>();
        for (var index = 0; index < messages.Count; index++)
        {
            var file = folder.Combine($"vc-message-{index + 1}");
            File.WriteAllText(file, messages[index], utf8);
            steps.Add($"cp {ShellQuoted(file)} \"$(dirname \"$1\")/vc-message-{index + 1}\"");
        }

        steps.Add($"cp {ShellQuoted(todoFile)} \"$1\"");
        var editor = "vc() { " + string.Join(" && ", steps) + "; }; vc";
        return await repo.GitWithEnvironmentAllowingFailureAsync(new Dictionary<string, string?> { ["GIT_SEQUENCE_EDITOR"] = editor }, "rebase", "-i", onto);
    }

    /// <summary>A path for <c>sh</c>: forward slashes, single-quoted, a <c>'</c> written <c>'\''</c>.</summary>
    private static string ShellQuoted(string path) => "'" + path.Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>The guarded amend of design 2.5 for group <paramref name="group"/>: one todo line.</summary>
    private static string GuardedAmend(int group) => string.Create(
        CultureInfo.InvariantCulture,
        $"exec if l=$(git rev-parse -q --verify refs/rewritten/vc-{group}) && [ -n \"$l\" ] && [ \"$(git rev-parse -q --verify HEAD^)\" = \"$l\" ]; then git commit --amend --only --allow-empty --cleanup=whitespace --quiet --file=\"$(git rev-parse --git-path rebase-merge/vc-message-{group})\"; fi");

    /// <summary>A file in the repository's git folder, or null when it does not exist.</summary>
    private static string? ReadGitFile(TempRepo repo, string relativePath)
    {
        var path = Path.Combine(repo.Path, ".git", relativePath);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>Which of the files and folders that mark an operation in progress the git folder holds, in ordinal order.</summary>
    private static string[] InProgressFiles(TempRepo repo) =>
        [.. new[] { "MERGE_HEAD", "MERGE_MSG", "CHERRY_PICK_HEAD", "REVERT_HEAD", "sequencer", "rebase-merge", "rebase-apply" }
            .Where(name => File.Exists(Path.Combine(repo.Path, ".git", name)) || Directory.Exists(Path.Combine(repo.Path, ".git", name)))
            .Order(StringComparer.Ordinal)];

    /// <summary>The lines of a todo file that are neither empty nor comments.</summary>
    private static string[] TodoLines(string? todo) =>
        [.. (todo ?? string.Empty).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'))];

    private static void AssertPick(string line, string sha)
    {
        var fields = line.Split(' ');
        Assert.True(fields.Length >= 2 && fields[0] == "pick" && fields[1].Length >= 7 && sha.StartsWith(fields[1], StringComparison.Ordinal), $"Not a pick of {sha}: {line}");
    }
}
