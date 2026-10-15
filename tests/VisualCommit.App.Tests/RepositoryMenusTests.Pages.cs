using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Pages;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Phase 5's items and page menus (docs/phase-5-design.md 4.4, 5.1, 5.2, 5.3, 7.10, 12.3, 12.4,
/// 13.1; docs/test-reports/phase-5.md part B and decisions BD7, BD8, BD21, BD22): "File history"
/// and "Blame" in the file menus in each place of 4.4's table, "Show reflog" and "Show backups",
/// "Show reflog of {b}", and the menus of the history's rows, the blame's runs, the reflog's
/// entries, the backups and the reflog's source, with their actions and heights.
/// </summary>
public partial class RepositoryMenusTests
{
    private const string Head = "c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9";
    private const string Rename = "0575c7f6631b3ba6c9d623f53b28489405230cfd";
    private const string AddTracing = "14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c";
    private const string AddParser = "1d5a7cf999727e7f724054348c81866a62025184";
    private const string Shortcut = "d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f";
    private const string WriteNotes = "9dd61fde06301292b9b14d0911560b6d987f645d";
    private const string NoCommitYet = "This file is in no commit yet";
    private static readonly string Zero = new('0', 40);

    // --- The file menus (4.4) ---------------------------------------------------------------------

    [Fact]
    public void A_stage_list_file_has_its_history_from_HEAD_and_the_working_trees_blame()
    {
        var (actions, recorder) = RecordingActions.Create();
        var context = Context(HistoryRefs());
        var reader = new FileMenuTarget(new ChangedFile("src/Reader.cs", FileChangeKind.Modified), IsUntracked: false, ExistsInWorkingTree: true);

        // Report B5 step 2: 9 items and 3 separators, 293 high.
        var menu = RepositoryMenus.ForUnstagedFile(context, reader, actions);
        Assert.Equal(
            ["Reader.cs", "Stage", "Discard changes…", "—", "Ignore Reader.cs (disabled: \"Only untracked files can be ignored\")", "Ignore all *.cs files (disabled: \"Only untracked files can be ignored\")", "—", "File history", "Blame", "—", "Open file", "Show in folder", "Copy path"],
            Lines(menu));
        Assert.Equal(293, Height(menu));
        Invoke(menu, "File history", "Blame");
        Assert.Equal(
            [$"ShowFileHistoryAsync({new FileHistoryRequest("src/Reader.cs", "HEAD")})", $"ShowBlameAsync({new BlameRequest("src/Reader.cs", null)})"],
            recorder.Calls.Select(call => call.ToString()));

        // A staged rename: the history goes by its old path, the name it has in HEAD.
        recorder = Clear(ref actions);
        var renamed = new FileMenuTarget(new ChangedFile("src/helpers.py", FileChangeKind.Renamed, "src/util.py"), false, true);
        menu = RepositoryMenus.ForStagedFile(context, renamed, actions);
        Assert.Equal(["helpers.py", "Unstage", "—", "File history", "Blame", "—", "Open file", "Show in folder", "Copy path"], Lines(menu));
        Invoke(menu, "File history", "Blame");
        Assert.Equal(
            [$"ShowFileHistoryAsync({new FileHistoryRequest("src/util.py", "HEAD")})", $"ShowBlameAsync({new BlameRequest("src/helpers.py", null)})"],
            recorder.Calls.Select(call => call.ToString()));
    }

    [Fact]
    public void A_file_deleted_in_the_working_tree_blames_HEADs_version_and_one_in_no_commit_has_both_items_disabled()
    {
        var (actions, recorder) = RecordingActions.Create();
        var context = Context(HistoryRefs());

        var deleted = new FileMenuTarget(new ChangedFile("docs/old-notes.txt", FileChangeKind.Deleted), false, ExistsInWorkingTree: false);
        Invoke(RepositoryMenus.ForUnstagedFile(context, deleted, actions), "File history", "Blame");
        Assert.Equal(
            [$"ShowFileHistoryAsync({new FileHistoryRequest("docs/old-notes.txt", "HEAD")})", $"ShowBlameAsync({new BlameRequest("docs/old-notes.txt", Head, "at HEAD")})"],
            recorder.Calls.Select(call => call.ToString()));

        // An untracked file and a staged new one (reports' 04c, 04d).
        var untracked = new FileMenuTarget(new ChangedFile("docs/guide.md", FileChangeKind.Added), IsUntracked: true, ExistsInWorkingTree: true);
        Assert.Equal(
            ["guide.md", "Stage", "Discard changes…", "—", "Ignore guide.md", "Ignore all *.md files", "—", $"File history (disabled: \"{NoCommitYet}\")", $"Blame (disabled: \"{NoCommitYet}\")", "—", "Open file", "Show in folder", "Copy path"],
            Lines(RepositoryMenus.ForUnstagedFile(context, untracked, actions)));
        var stagedNew = new FileMenuTarget(new ChangedFile("config/settings.json", FileChangeKind.Added), false, true);
        var menu = RepositoryMenus.ForStagedFile(context, stagedNew, actions);
        Assert.Equal(["settings.json", "Unstage", "—", $"File history (disabled: \"{NoCommitYet}\")", $"Blame (disabled: \"{NoCommitYet}\")", "—", "Open file", "Show in folder", "Copy path"], Lines(menu));
        Assert.Equal(206, Height(menu));

        // Before the first commit (12.4).
        var unborn = Context(new RepoRefs(new HeadState("main", null), [], [], []));
        Assert.Contains("Blame (disabled: \"Make the first commit first\")", Lines(RepositoryMenus.ForUnstagedFile(unborn, new FileMenuTarget(new ChangedFile("a.txt", FileChangeKind.Modified), false, true), actions)));
        Assert.Contains("File history (disabled: \"Make the first commit first\")", Lines(RepositoryMenus.ForUnstagedFile(unborn, new FileMenuTarget(new ChangedFile("a.txt", FileChangeKind.Modified), false, true), actions)));
    }

    [Fact]
    public void A_nested_repository_and_the_resolvers_header_have_no_history_or_blame_and_a_conflicted_file_blames_the_working_tree()
    {
        var (actions, recorder) = RecordingActions.Create();
        var status = new WorkingTreeStatus([], [new ChangedFile("src/core.txt", FileChangeKind.Conflicted)], new HashSet<string>(StringComparer.Ordinal))
        {
            Conflicts = [new ConflictEntry("src/core.txt", ConflictKind.BothModified, false, "100644", "100644", "100644", Zero, Zero, Zero)],
        };
        var context = Context(HistoryRefs(), status);
        var core = new FileMenuTarget(new ChangedFile("src/core.txt", FileChangeKind.Conflicted), false, true);

        Assert.DoesNotContain(Lines(RepositoryMenus.ForUnstagedFile(context, new FileMenuTarget(new ChangedFile("vendor/lib/", FileChangeKind.Added), true, true), actions)), line => line.StartsWith("File history", StringComparison.Ordinal));
        Assert.DoesNotContain(Lines(RepositoryMenus.ForUnstagedFile(context, core with { InConflictResolver = true }, actions)), line => line.StartsWith("Blame", StringComparison.Ordinal));

        var menu = RepositoryMenus.ForUnstagedFile(context, core, actions);
        Assert.Equal(345, Height(menu));
        Invoke(menu, "File history", "Blame");
        Assert.Equal(
            [$"ShowFileHistoryAsync({new FileHistoryRequest("src/core.txt", "HEAD")})", $"ShowBlameAsync({new BlameRequest("src/core.txt", null)})"],
            recorder.Calls.Select(call => call.ToString()));

        // Gone from the working tree: HEAD's version when HEAD has it, none when only theirs has it.
        recorder = Clear(ref actions);
        Invoke(RepositoryMenus.ForUnstagedFile(context, core with { ExistsInWorkingTree = false }, actions), "Blame");
        Assert.Equal([$"ShowBlameAsync({new BlameRequest("src/core.txt", Head, "at HEAD")})"], recorder.Calls.Select(call => call.ToString()));
        var theirs = Context(HistoryRefs(), status with { Conflicts = [new ConflictEntry("src/core.txt", ConflictKind.AddedByThem, false, "000000", "000000", "100644", Zero, Zero, Zero)] });
        Assert.Contains($"Blame (disabled: \"{NoCommitYet}\")", Lines(RepositoryMenus.ForUnstagedFile(theirs, core with { ExistsInWorkingTree = false }, actions)));
    }

    [Fact]
    public void A_commits_file_has_its_history_and_blame_at_that_commit_and_a_deleted_one_blames_the_first_parent()
    {
        var (actions, recorder) = RecordingActions.Create();
        var context = Context(HistoryRefs());

        // Report B5 step 5: a commit's file, 171 high.
        var reader = new FileMenuTarget(new ChangedFile("src/Reader.cs", FileChangeKind.Renamed, "src/Parser.cs"), false, true, Rename) { CommitParent = AddTracing };
        var menu = RepositoryMenus.ForCommitFile(context, reader, actions);
        Assert.Equal(["Reader.cs", "File history", "Blame", "—", "Open file", "Show in folder", "Copy path"], Lines(menu));
        Assert.Equal(171, Height(menu));
        InvokeAll(menu);
        Assert.Equal(
            [$"ShowFileHistoryAsync({new FileHistoryRequest("src/Reader.cs", Rename)})", $"ShowBlameAsync({new BlameRequest("src/Reader.cs", Rename)})"],
            recorder.Calls.Take(2).Select(call => call.ToString()));

        // A file the commit deleted: the parent's version, titled "before {short}".
        recorder = Clear(ref actions);
        var notes = new FileMenuTarget(new ChangedFile("docs/notes.md", FileChangeKind.Deleted), false, false, "884b4622b22f86236cbaae78b04234e63b4a6a88") { CommitParent = AddParser };
        Invoke(RepositoryMenus.ForCommitFile(context, notes, actions), "Blame");
        Assert.Equal([$"ShowBlameAsync({new BlameRequest("docs/notes.md", AddParser, "before 884b462")})"], recorder.Calls.Select(call => call.ToString()));

        // A commit's diff (B5 step 7) has them too.
        recorder = Clear(ref actions);
        var diff = RepositoryMenus.ForDiff(context, new DiffMenuTarget(new FileMenuTarget(new ChangedFile("src/Parser.cs", FileChangeKind.Modified), false, false, "365482be62488b4c1259f0fb7dfa1631c797470b"), DiffSide.Commit, false, 0, true), actions, new NoLines());
        Assert.Equal(["Parser.cs", "File history", "Blame", "—", "Open file (disabled: \"The file is not in the working tree\")", "Show in folder", "Copy path"], Lines(diff));
    }

    [Fact]
    public void A_stashs_file_has_its_history_from_the_stashs_base_and_its_blame_at_the_stash()
    {
        var (actions, recorder) = RecordingActions.Create();
        const string Stash = "95daa96b4c242f0f653cadd7d0218a76679f0cf5";
        var refs = HistoryRefs() with { Stashes = [new StashEntry(0, Stash, Head, "On main: Work", "Test Author", "author@example.com", default, default)] };
        var file = new FileMenuTarget(new ChangedFile("README.md", FileChangeKind.Modified), false, true, Stash) { CommitParent = Head };

        Invoke(RepositoryMenus.ForCommitFile(Context(refs), file, actions), "File history", "Blame");

        Assert.Equal(
            [$"ShowFileHistoryAsync({new FileHistoryRequest("README.md", Head)})", $"ShowBlameAsync({new BlameRequest("README.md", Stash)})"],
            recorder.Calls.Select(call => call.ToString()));
    }

    // --- Show reflog, Show backups, Show reflog of {b} (5.1, BD22) --------------------------------

    [Fact]
    public void Show_reflog_and_Show_backups_are_in_the_menus_that_always_exist_also_in_an_unborn_repository()
    {
        var (actions, recorder) = RecordingActions.Create();
        var unborn = Context(new RepoRefs(new HeadState("main", null), [], [], []));

        Assert.Equal(["Local branches", "Create branch… (disabled: \"Make the first commit first\")", "Show reflog", "Show backups", "Show all hidden branches (disabled: \"No branch is hidden\")", "Stop soloing (disabled: \"No branch is soloed\")"], Lines(RepositoryMenus.ForSection(unborn, RefSection.LocalBranches, actions)!));
        var empty = RepositoryMenus.ForEmptyGraphArea(unborn, actions);
        Assert.Equal(["Show reflog", "Show backups", "Manage remotes…"], Lines(empty)[^3..]);
        Assert.True(empty.Items.Single(item => item.Label == "Show reflog").IsEnabled);
        Invoke(empty, "Show reflog", "Show backups");
        Assert.Equal(["ShowReflogAsync()", "ShowBackupsAsync()"], recorder.Calls.Select(call => call.ToString()));

        // B8 step 1: the Local branches section, 162 high with these and part F's two items (110 without part F's).
        Assert.Equal(162, Height(RepositoryMenus.ForSection(Context(HistoryRefs()), RefSection.LocalBranches, actions)!));

        // Only a local branch has "Show reflog of {b}".
        Assert.DoesNotContain(Lines(RepositoryMenus.ForRemoteBranch(Context(HistoryRefs()), "refs/remotes/origin/main", actions)), line => line.StartsWith("Show reflog", StringComparison.Ordinal));
        recorder = Clear(ref actions);
        Invoke(RepositoryMenus.ForLocalBranch(Context(HistoryRefs()), "refs/heads/feature/compare", actions), "Show reflog of feature/compare");
        Assert.Equal(["ShowReflogAsync(refs/heads/feature/compare)"], recorder.Calls.Select(call => call.ToString()));
    }

    // --- The pages' menus (BD8) -------------------------------------------------------------------

    [Fact]
    public void A_history_rows_menu_shows_it_in_the_graph_blames_it_and_copies_its_SHA()
    {
        var (actions, recorder) = RecordingActions.Create();
        var context = Context(HistoryRefs());
        var row = new FileHistoryRow(Commit(AddParser, "Add the parser", "7829335c71bb8f1161b668a29afb602632228425"), FileChangeKind.Added, "src/Parser.cs");

        // Report B5 step 9: 119 high.
        var menu = RepositoryMenus.ForFileHistoryRow(context, row, inGraph: true, actions);
        Assert.Equal(["1d5a7cf Add the parser", "Show in graph", "Blame at this commit", "—", "Copy SHA"], Lines(menu));
        Assert.Equal(119, Height(menu));
        InvokeAll(menu);
        Assert.Equal(
            [$"ShowInGraphAsync({AddParser})", $"ShowBlameAsync({new BlameRequest("src/Parser.cs", AddParser)})", $"CopyShaAsync({AddParser})"],
            recorder.Calls.Select(call => call.ToString()));

        // A commit the graph does not have (4.3), and a row that deleted the file (its parent's version).
        recorder = Clear(ref actions);
        var deletion = new FileHistoryRow(Commit("884b4622b22f86236cbaae78b04234e63b4a6a88", "Remove the notes", AddParser), FileChangeKind.Deleted, "docs/notes.md");
        menu = RepositoryMenus.ForFileHistoryRow(context, deletion, inGraph: false, actions);
        Assert.Equal("Show in graph (disabled: \"This commit is not in the graph\")", Lines(menu)[1]);
        Invoke(menu, "Blame at this commit");
        Assert.Equal([$"ShowBlameAsync({new BlameRequest("docs/notes.md", AddParser, "before 884b462")})"], recorder.Calls.Select(call => call.ToString()));
    }

    [Fact]
    public void A_blame_runs_menu_goes_back_through_previous_and_a_run_not_committed_yet_has_Show_in_graph_alone()
    {
        var (actions, recorder) = RecordingActions.Create();
        var context = Context(HistoryRefs());
        var rename = new BlameCommit(Rename, "Mira Kovac", "mira@example.com", default, "Rename the parser to reader", "src/Reader.cs", AddTracing, "src/Parser.cs", false);

        // Report B6 step 6: 154 high.
        var menu = RepositoryMenus.ForBlameRun(context, rename, inGraph: true, actions);
        Assert.Equal(["0575c7f Rename the parser to reader", "Show in graph", "—", "Blame before this commit", "File history", "—", "Copy SHA"], Lines(menu));
        Assert.Equal(154, Height(menu));
        InvokeAll(menu);
        Assert.Equal(
            [$"ShowInGraphAsync({Rename})", $"ShowBlameAsync({new BlameRequest("src/Parser.cs", AddTracing)})", $"ShowFileHistoryAsync({new FileHistoryRequest("src/Reader.cs", Rename)})", $"CopyShaAsync({Rename})"],
            recorder.Calls.Select(call => call.ToString()));

        // B6 step 8: the file's first commit.
        var first = rename with { Sha = AddParser, Summary = "Add the parser", Path = "src/Parser.cs", PreviousSha = null, PreviousPath = null };
        Assert.Equal("Blame before this commit (disabled: \"This is the file's first commit\")", Lines(RepositoryMenus.ForBlameRun(context, first, true, actions))[3]);

        // B6 step 3: lines not committed yet, 58 high.
        var uncommitted = rename with { Sha = new string('0', 40), AuthorName = "Not Committed Yet" };
        menu = RepositoryMenus.ForBlameRun(context, uncommitted, inGraph: true, actions);
        Assert.Equal(["Not committed yet", "Show in graph"], Lines(menu));
        Assert.Equal(58, Height(menu));
    }

    [Fact]
    public void A_reflog_entrys_menu_has_phase_3s_operations_with_reasons_from_the_entry()
    {
        var (actions, recorder) = RecordingActions.Create();
        var refs = new RepoRefs(new HeadState("main", WriteNotes), [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, WriteNotes, IsHead: true)], [], ["origin"]);
        var entry = new ReflogEntry("HEAD@{9}", 9, Shortcut, [WriteNotes], "commit: Try a shortcut", "Try a shortcut", default);

        // Report B8 step 5: 206 high.
        var menu = RepositoryMenus.ForReflogEntry(Context(refs), entry, inGraph: false, onHead: false, actions);
        Assert.Equal(
            ["d07c677 Try a shortcut", "Create branch here…", "Check out this commit", "—", "Reset main to this commit…", "Cherry-pick onto main", "—", "Show in graph (disabled: \"This commit is not in the graph\")", "Copy SHA"],
            Lines(menu));
        Assert.Equal(206, Height(menu));
        InvokeAll(menu);
        Assert.Equal(
            [$"CreateBranchAtKeptCommitAsync({Shortcut})", $"CheckoutKeptCommitAsync({Shortcut})", $"ResetToKeptCommitAsync({Shortcut})", $"CherryPickKeptCommitAsync({Shortcut})", $"CopyShaAsync({Shortcut})"],
            recorder.Calls.Select(call => call.ToString()));

        // HEAD's own commit, and a merge commit.
        var head = entry with { Sha = WriteNotes, Subject = "Write the notes" };
        Assert.Contains("Cherry-pick onto main (disabled: \"This commit is already on main\")", Lines(RepositoryMenus.ForReflogEntry(Context(refs), head, true, false, actions)));
        var merge = entry with { Parents = [WriteNotes, AddParser] };
        Assert.Contains("Cherry-pick onto main (disabled: \"Merge commits cannot be cherry-picked here\")", Lines(RepositoryMenus.ForReflogEntry(Context(refs), merge, true, false, actions)));
        var detached = refs with { Head = new HeadState(null, WriteNotes) };
        Assert.Contains("Check out this commit (disabled: \"HEAD is already at this commit\")", Lines(RepositoryMenus.ForReflogEntry(Context(detached), head, true, false, actions)));

        // HEAD@{12} names 316166a "Clash notes", an ancestor of main: already on it, as phase 3's
        // commit menu says (2.12); HEAD@{13}'s 362fbd6 is topic's alone, so it can be picked.
        var ancestor = new ReflogEntry("HEAD@{12}", 12, "316166adc8e550ebb513a7f8c0ea99ecbdd88983", ["6d7a40b76bb9156c0969e533b292cbe2357e6783"], "checkout: moving from topic to main", "Clash notes", default);
        Assert.Contains("Cherry-pick onto main (disabled: \"This commit is already on main\")", Lines(RepositoryMenus.ForReflogEntry(Context(refs), ancestor, inGraph: true, onHead: true, actions)));
        var topic = new ReflogEntry("HEAD@{13}", 13, "362fbd633bc18c7553bf271ad7d31f1db055af2d", ["316166adc8e550ebb513a7f8c0ea99ecbdd88983"], "checkout: moving from main to topic", "Topic work", default);
        Assert.Contains("Cherry-pick onto main", Lines(RepositoryMenus.ForReflogEntry(Context(refs), topic, inGraph: true, onHead: false, actions)));
    }

    [Fact]
    public void The_reflog_source_menu_lists_HEAD_the_local_branches_by_full_name_and_the_backups()
    {
        var chosen = new List<string?>();
        var refs = new RepoRefs(
            new HeadState("main", WriteNotes),
            [
                Local("clash"), Local("feature/renamed"), Local("main"), Local("topic"),
                new GitRef("refs/remotes/origin/topic", "origin/topic", RefKind.RemoteBranch, WriteNotes, RemoteName: "origin"),
                new GitRef("refs/tags/v0.8", "v0.8", RefKind.Tag, WriteNotes),
            ],
            [],
            ["origin"]);

        // Report B8 step 9 (before shortcut is made): 7 items and a separator, 223 high with shortcut.
        var menu = RepositoryMenus.ForReflogSource(Context(refs), chosen.Add);
        Assert.Equal(["Reflog and backups", "HEAD", "clash", "feature/renamed", "main", "topic", "—", "Backups"], Lines(menu));
        Assert.Equal(197, Height(menu));
        InvokeAll(menu);
        Assert.Equal(["HEAD", "refs/heads/clash", "refs/heads/feature/renamed", "refs/heads/main", "refs/heads/topic", null], chosen);
    }

    // --- The backups list (5.3, 7.10, 12.3) -------------------------------------------------------

    [Fact]
    public void A_backup_restores_by_its_kind_and_offers_its_commit_to_branch_check_out_and_copy()
    {
        var (actions, recorder) = RecordingActions.Create();
        var refs = ReflogRefs();
        var doomed = Backup("branch-20260101T122701000Z", "branch", Ref("branch-20260101T122701000Z/doomed", "44e646bdcb1d920659f2b28b1b44e19f6ae45a7b", "doomed", "Doomed work"));

        // Report B9 step 3: 154 high.
        var menu = RepositoryMenus.ForBackup(Context(refs), doomed, actions);
        Assert.Equal(["Deleted branch doomed", "Restore branch doomed", "—", "Create branch here…", "Check out this commit", "—", "Copy SHA"], Lines(menu));
        Assert.Equal(154, Height(menu));
        InvokeAll(menu);
        Assert.Equal(
            [$"RestoreBackupAsync({doomed})", "CreateBranchAtKeptCommitAsync(44e646bdcb1d920659f2b28b1b44e19f6ae45a7b)", "CheckoutKeptCommitAsync(44e646bdcb1d920659f2b28b1b44e19f6ae45a7b)", "CopyShaAsync(44e646bdcb1d920659f2b28b1b44e19f6ae45a7b)"],
            recorder.Calls.Select(call => call.ToString()));

        // B9 step 4: once the branch exists, its item says so.
        var restored = refs with { Refs = [.. refs.Refs, Local("doomed")] };
        Assert.Equal("Restore branch doomed (disabled: \"A branch named doomed exists\")", Lines(RepositoryMenus.ForBackup(Context(restored), doomed, actions))[1]);
    }

    [Fact]
    public void Each_backup_kind_has_its_words_and_its_restore()
    {
        var (actions, _) = RecordingActions.Create();
        var context = Context(ReflogRefs());
        string[] Head2(BackupInfo backup) => [.. Lines(RepositoryMenus.ForBackup(context, backup, actions)).Take(2)];

        Assert.Equal(["Before a hard reset to 9dd61fd", "Restore the changes…"], Head2(Backup("reset-20260101T122401000Z", "reset", Ref("reset-20260101T122401000Z", Shortcut), Changes("reset-20260101T122401000Z", "VisualCommit: snapshot before a hard reset to 9dd61fd", "README.md"))));
        Assert.Equal(["Before discarding 2 files", "Restore the discarded files…"], Head2(Backup("discard-20260101T122001000Z", "discard", Ref("discard-20260101T122001000Z", "9849838c39b980387b976a8a864b7c6d1ffda865", body: "notes/todo.txt\nsrc/app.txt"))));
        Assert.Equal(["Before discarding 1 file", "Restore the discarded files…"], Head2(Backup("discard-20260102T090001000Z", "discard", Ref("discard-20260102T090001000Z", "27c8989ccb8c834f2788d01bd7ce7358ddd61c30", body: "src/app.txt"))));
        var mixed = Backup("reset-20260101T121901000Z", "reset", Ref("reset-20260101T121901000Z", "316166adc8e550ebb513a7f8c0ea99ecbdd88983"), Changes("reset-20260101T121901000Z", "VisualCommit: index before a mixed reset", string.Empty));
        Assert.Equal(["Before a mixed reset", "Restore the staged changes"], Head2(mixed));
        Assert.Equal(["Removed remote mirror", "Create branch here…"], Head2(Backup("remote-20260101T121801000Z", "remote", Ref("remote-20260101T121801000Z/mirror/main", "316166adc8e550ebb513a7f8c0ea99ecbdd88983", "mirror/main"), Ref("remote-20260101T121801000Z/mirror/topic", "362fbd633bc18c7553bf271ad7d31f1db055af2d", "mirror/topic"))));
        Assert.Equal(["Deleted feature/gone on origin", "Push feature/gone back to origin…"], Head2(Backup("remote-branch-20260101T121701000Z", "remote-branch", Ref("remote-branch-20260101T121701000Z/origin/feature/gone", "7dd8e8f7958138d967be47cb167b84bac7fd7467", "origin/feature/gone"))));
        Assert.Equal(["Deleted tag v0.8 on origin", "Push v0.8 back to origin…"], Head2(Backup("remote-tag-20260101T121601000Z", "remote-tag", Ref("remote-tag-20260101T121601000Z/origin/v0.8", "5c9c1cb91ada4146b5fb15a7229c98b6d79dbd58", "origin/v0.8"))));
        Assert.Equal(["Tag v1.0-rc, its creation undone", "Restore tag v1.0-rc"], Head2(Backup("tag-undone-20260101T121501000Z", "tag-undone", Ref("tag-undone-20260101T121501000Z/v1.0-rc", "316166adc8e550ebb513a7f8c0ea99ecbdd88983", "v1.0-rc"))));
        Assert.Equal(["Deleted tag v0.9", "Restore tag v0.9"], Head2(Backup("tag-20260101T121401000Z", "tag", Ref("tag-20260101T121401000Z/v0.9", "7dd8e8f7958138d967be47cb167b84bac7fd7467", "v0.9"))));
        Assert.Equal(["Dropped or popped stash \"On main: Try a README\"", "Restore stash"], Head2(Backup("stash-20260101T121301000Z", "stash", Ref("stash-20260101T121301000Z", "39daf6c3dee7c1739b2f1753ad2011de84e70373", subject: "On main: Try a README"))));
        Assert.Equal(["Commits a detached HEAD left behind", "Create branch here…"], Head2(Backup("detached-20260101T121101000Z", "detached", Ref("detached-20260101T121101000Z", "7e2ce3b66ecd286fc7ce6694d1f57ca6acf4f0d3"))));
        Assert.Equal(["Before a force push of origin/topic", "Push topic back to origin…"], Head2(Backup("force-push-20260101T120901000Z", "force-push", Ref("force-push-20260101T120901000Z/origin/topic", "f7a69be38486b8b82fa0f12b0a6c0482af8a0424", "origin/topic"))));
        Assert.Equal(["Before rebasing topic", "Create branch here…"], Head2(Backup("rebase-20260101T120801000Z", "rebase", Ref("rebase-20260101T120801000Z/topic", "f7a69be38486b8b82fa0f12b0a6c0482af8a0424", "topic"))));
        Assert.Equal(["Before skipping a commit", "Restore the changes…"], Head2(Backup("skip-20260101T120701000Z", "skip", Ref("skip-20260101T120701000Z", "6d7a40b76bb9156c0969e533b292cbe2357e6783"), Changes("skip-20260101T120701000Z", "VisualCommit: snapshot before an abort", "src/app.txt"))));
        Assert.Equal(["Before aborting", "Restore the changes…"], Head2(Backup("abort-20260101T120601000Z", "abort", Ref("abort-20260101T120601000Z", "6d7a40b76bb9156c0969e533b292cbe2357e6783"), Changes("abort-20260101T120601000Z", "VisualCommit: snapshot before an abort", "docs/clash.txt\nsrc/app.txt"))));
        Assert.Equal(["Before a reset", "Create branch here…"], Head2(Backup("reset-20260101T110001000Z", "reset", Ref("reset-20260101T110001000Z", WriteNotes))));
        Assert.Equal(["Before aborting", "Create branch here…"], Head2(Backup("abort-20260101T110001000Z", "abort", Ref("abort-20260101T110001000Z", WriteNotes))));
        Assert.Equal(["cleanup", "Create branch here…"], Head2(Backup("cleanup-20260101T110001000Z", "cleanup", Ref("cleanup-20260101T110001000Z", WriteNotes))));

        // B9 step 14: the staged changes while something is staged; B9's item has no "…" (BD21).
        var staged = Context(ReflogRefs(), new WorkingTreeStatus([new ChangedFile("src/app.txt", FileChangeKind.Modified)], [], new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("Restore the staged changes (disabled: \"Unstage your changes first\")", Lines(RepositoryMenus.ForBackup(staged, mixed, actions))[1]);
    }

    [Fact]
    public void A_push_back_needs_the_remote_and_a_force_push_maps_its_name_back_through_an_upstream()
    {
        var (actions, _) = RecordingActions.Create();
        var gone = Backup("remote-branch-20260101T121701000Z", "remote-branch", Ref("remote-branch-20260101T121701000Z/mirror/x", WriteNotes, "mirror/x"));
        Assert.Equal("Push x back to mirror… (disabled: \"mirror no longer exists\")", Lines(RepositoryMenus.ForBackup(Context(ReflogRefs()), gone, actions))[1]);

        // A remote whose name holds "/": the longest configured name wins.
        var nested = ReflogRefs() with { Remotes = ["origin", "origin/eu"] };
        var eu = Backup("remote-branch-20260101T121701000Z", "remote-branch", Ref("remote-branch-20260101T121701000Z/origin/eu/main", WriteNotes, "origin/eu/main"));
        Assert.Equal(["Deleted main on origin/eu", "Push main back to origin/eu…"], Lines(RepositoryMenus.ForBackup(Context(nested), eu, actions)).Take(2));

        // A force push of a remote-tracking name no upstream maps and no remote prefixes.
        var unknown = Backup("force-push-20260101T120901000Z", "force-push", Ref("force-push-20260101T120901000Z/mirror/topic", WriteNotes, "mirror/topic"));
        Assert.Equal("Push topic back to mirror… (disabled: \"The remote this backup came from cannot be found\")", Lines(RepositoryMenus.ForBackup(Context(ReflogRefs()), unknown, actions))[1]);
        Assert.Equal("Push topic back to mirror… (disabled: \"The remote this backup came from cannot be found\")", Lines(RepositoryMenus.ForBackup(Context(ReflogRefs()), unknown, actions, hasForcePushLease: false))[1]);

        // 7.10: once the remote-tracking reflog no longer has the commit the force push left (its
        // lease, which the backups list reads), the push back is disabled.
        var forced = Backup("force-push-20260101T120901000Z", "force-push", Ref("force-push-20260101T120901000Z/origin/topic", "f7a69be38486b8b82fa0f12b0a6c0482af8a0424", "origin/topic"));
        Assert.Equal("Push topic back to origin…", Lines(RepositoryMenus.ForBackup(Context(ReflogRefs()), forced, actions, hasForcePushLease: true))[1]);
        Assert.Equal(
            "Push topic back to origin… (disabled: \"The commit your force push left can no longer be found\")",
            Lines(RepositoryMenus.ForBackup(Context(ReflogRefs()), forced, actions, hasForcePushLease: false))[1]);
        var untracked = ReflogRefs() with { Refs = [Local("main") with { IsHead = true }] };
        Assert.Equal(
            "Push topic back to origin… (disabled: \"The commit your force push left can no longer be found\")",
            Lines(RepositoryMenus.ForBackup(Context(untracked), forced, actions, hasForcePushLease: false))[1]);
    }

    // --- Helpers ------------------------------------------------------------------------------

    /// <summary>"history"'s refs: HEAD on main at Read whole lines; feature/compare; their upstreams on origin.</summary>
    private static RepoRefs HistoryRefs() => new(
        new HeadState("main", Head),
        [
            new GitRef("refs/heads/feature/compare", "feature/compare", RefKind.LocalBranch, "245c395674db6ae89390be2db018db5356b446fe"),
            new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Head, IsHead: true),
            new GitRef("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, Head, RemoteName: "origin"),
        ],
        [],
        ["origin"]);

    /// <summary>"reflog"'s refs as the menus need them: HEAD on main, topic tracking origin/topic, the remote origin.</summary>
    private static RepoRefs ReflogRefs() => new(
        new HeadState("main", WriteNotes),
        [
            Local("clash"),
            Local("main") with { IsHead = true },
            Local("topic") with { Upstream = "origin/topic", UpstreamRef = "refs/remotes/origin/topic", UpstreamRemote = "origin", UpstreamRemoteRef = "refs/heads/topic" },
            new GitRef("refs/remotes/origin/topic", "origin/topic", RefKind.RemoteBranch, "362fbd633bc18c7553bf271ad7d31f1db055af2d", RemoteName: "origin"),
        ],
        [],
        ["origin"]);

    private static GitRef Local(string name) => new("refs/heads/" + name, name, RefKind.LocalBranch, WriteNotes);

    private static CommitInfo Commit(string sha, string subject, params string[] parents) =>
        new(sha, parents, "Mira Kovac", "mira@example.com", default, default, subject);

    private static BackupInfo Backup(string name, string kind, params BackupRefInfo[] refs) =>
        new(name, kind, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), null, refs);

    private static BackupRefInfo Ref(string rest, string commit, string? name = null, string subject = "", string body = "") =>
        new("refs/visualcommit/backup/" + rest, commit, name, IsChanges: false, subject, body, commit, subject);

    private static BackupRefInfo Changes(string backup, string subject, string body) =>
        new($"refs/visualcommit/backup/{backup}-changes", "21c5a38946ba1dc3ce852e135922ae8f676a0ce4", null, IsChanges: true, subject, body, "21c5a38946ba1dc3ce852e135922ae8f676a0ce4", subject);

    /// <summary>Invokes the items with these labels, in the order given.</summary>
    private static void Invoke(MenuModel menu, params string[] labels)
    {
        foreach (var label in labels)
        {
            menu.Items.Single(item => item.Label == label).Invoke();
        }
    }

    /// <summary>A fresh recorder in place of <paramref name="actions"/>'.</summary>
    private static RecordingActions Clear(ref IRepositoryActions actions)
    {
        (actions, var recorder) = RecordingActions.Create();
        return recorder;
    }

    /// <summary>A diff's line actions that a commit's diff never offers.</summary>
    private sealed class NoLines : IDiffMenuActions
    {
        public Task StageLinesAsync() => Task.CompletedTask;

        public Task UnstageLinesAsync() => Task.CompletedTask;

        public Task DiscardLinesAsync() => Task.CompletedTask;

        public Task RunHunkActionAsync(int hunk, ViewModels.Diff.LineAction action) => Task.CompletedTask;
    }
}
