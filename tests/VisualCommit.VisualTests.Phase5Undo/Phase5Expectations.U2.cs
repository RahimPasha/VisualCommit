// Part U's second half (docs/test-reports/phase-5.md, "Part U: undo and redo", checks U20 to U23):
// what the checks of stashes and tags, remotes, local configuration and the refusals compare
// against, copied from the report's sections "Scenarios and graphs of part U" and "Texts these checks
// rely on (part U)" and from its rows U20 to U23, not read from the app. In PartU2, apart from the
// first half's PartU (U16 to U19), so that the two never edit one file. The real-window pass
// compiles this file too (VisualCommit.RealWindowTests.csproj links Phase5Expectations*.cs from both
// projects): only the BCL, Avalonia, Xunit, VisualCommit.Core, VisualCommit.Git and
// VisualCommit.Testing here.
using VisualCommit.Core.Git;

namespace VisualCommit.VisualTests.Phase5;

public static partial class Phase5Expectations
{
    /// <summary>The expected results of checks U20 to U23 (part U's second half).</summary>
    public static class PartU2
    {
        // ---- The scenarios' repositories: the tab's name, the error dialog's "In …" and the log's ----

        public const string OpsRepo = "ops";

        public const string SyncRepo = "sync";

        public const string RebaseRepo = "rebase";

        // ---- The scenarios' commits ("Full SHAs of the scenarios used in commands") ----

        /// <summary>"ops": Main work, where main, origin/main and v1.1 are.</summary>
        public const string MainWork = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

        /// <summary>"ops": Add core, where v1.0's tag object points.</summary>
        public const string AddCore = "8beca345049bf7e8bab3a7b4f1427eff941b87e3";

        /// <summary>"ops": Fast-forward me, feature/ff's tip.</summary>
        public const string FastForwardMe = "d93814d4ea6e21d1a70dd93066dbb23512c03b56";

        /// <summary>"ops": Alpha two, feature/alpha's tip.</summary>
        public const string AlphaTwo = "ce5ea6ab16759bede02ce706dd5c6865443c2051";

        /// <summary>"ops": the stash "On main: Experiment".</summary>
        public const string Experiment = "0e95aca13f7975ef4a1fe029a8b5e9497f31028d";

        /// <summary>"ops": v1.0's tag object ("Version 1.0").</summary>
        public const string V10TagObject = "c8e997667b6705e41b3450489f8e0dda2da5c069";

        /// <summary>"ops": the blob of README.md as committed ("# Operations"), the index entry an apply's undo writes back.</summary>
        public const string ReadmeBlob = "114493d";

        /// <summary>"sync": Shared work, main's tip.</summary>
        public const string SharedWork = "a7f2cfd9a076c4c746e13caaa60f4fa2f0d76a4c";

        /// <summary>"sync": Published work, origin's published.</summary>
        public const string PublishedWork = "8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6";

        /// <summary>"sync": Unpushed work, the local published.</summary>
        public const string UnpushedWork = "8e653159ca5e460a187117d51b3f18bdf81f5c96";

        /// <summary>"sync": Topic work, topic's tip.</summary>
        public const string TopicWork = "77d782ebfce02f1fe2b29c2ee2b6b43a1f682a02";

        /// <summary>"rebase": Topic two as built, feature/topic's tip before the rebase.</summary>
        public const string TopicTwo = "b0dfa3f405dffff59bea3f4f434b16e5490cc597";

        /// <summary>"rebase": Main more, main's tip.</summary>
        public const string MainMore = "1dd58b8cfc17d4d9b80550eddd80b11ebf94c146";

        /// <summary>"rebase": Other one, feature/other's tip.</summary>
        public const string OtherOne = "2d4d79a03112c8a5e2f9a783318d588df519af5d";

        // ---- Commits and objects the checks make ----

        /// <summary>U20: the stash "On main: Save my work", made again with the same id by its redo (D72).</summary>
        public const string SaveMyWork = "b80f1ca8abb5f1eadcd6c56b76e1b13034251dd5";

        /// <summary>U20: v1.2's tag object ("#42 hotfix", tagger Test Author 09:00).</summary>
        public const string V12TagObject = "bbc27cb2e708f4ae0fa2559b4c2205f10898f4cb";

        /// <summary>U21 part B, before the app starts: "Rewritten work" on published (12:05).</summary>
        public const string RewrittenWork = "4f6a37288fadb5c453061cefedee620a9f98121e";

        /// <summary>U21 part F, outside: "Published elsewhere", on origin.git only (12:05).</summary>
        public const string PublishedElsewhere = "c16ddfa18af735846643ca9f16750d778b536fe0";

        /// <summary>U23 part A, outside: "Terminal work" on main (12:08).</summary>
        public const string TerminalWork = "c7d8c711db8fe72237a33847de1e3961895c0b4a";

        /// <summary>U23 part C: Topic two rebased onto main, feature/topic's tip after the rebase.</summary>
        public const string TopicTwoOntoMain = "5f71ab8bad51a15fdfdfd19fc1b0084ef6e956c5";

        // ---- Files the checks write ----

        /// <summary>U20 part A, before the app starts (phase 3's check 15): src/core.txt.</summary>
        public const string CoreMoreWork = "core\nmain work\nmore work\n";

        /// <summary>U20 part A, before the app starts: the untracked todo.txt.</summary>
        public const string Todo = "Later\n";

        /// <summary>U21 part B, before the app starts: src/published.txt of "Rewritten work".</summary>
        public const string Rewritten = "rewritten\n";

        /// <summary>U21 part F, outside: src/published.txt of "Published elsewhere".</summary>
        public const string Elsewhere = "published\nunpushed\nelsewhere\n";

        /// <summary>U22 part F, before the app starts: logs/run.log.</summary>
        public const string RunLog = "run\n";

        /// <summary>U22 part F: .gitignore as "Ignore run.log" creates it, 14 bytes.</summary>
        public const string Gitignore = "/logs/run.log\n";

        /// <summary>U22 part D: the URL the edit types.</summary>
        public const string ExampleUrl = "https://example.invalid/sync.git";

        /// <summary>U23 part A, outside: src/terminal.txt of "Terminal work".</summary>
        public const string Terminal = "terminal\n";

        /// <summary>U23 part C, outside: src/core.txt changed in the move's way.</summary>
        public const string CoreMine = "core\nmain work\nmine\n";

        /// <summary>U23 part C, outside: docs/main.txt changed in the move's way.</summary>
        public const string MainMine = "main more\nmine\n";

        /// <summary>U23 part D, outside: the ignored docs/ff.txt in the redo's way.</summary>
        public const string FfNotes = "my ff notes\n";

        // ---- Entry texts (7.6) ----

        public const string StashText = "stash \"On main: Save my work\"";

        public const string ApplyText = "apply of stash \"On main: Experiment\"";

        public const string PopText = "pop of stash \"On main: Experiment\"";

        public const string DropText = "drop of stash \"On main: Experiment\"";

        public const string CreateTagText = "creation of tag v1.2";

        public const string DeleteTagText = "deletion of tag v1.0";

        public const string PushTopicText = "push of topic to origin";

        public const string PushPublishedText = "push of published to origin";

        public const string ForcePushText = "force push of published to origin";

        public const string DeleteRemoteBranchText = "deletion of published on origin";

        public const string PushTagText = "push of tag v1.1 to origin";

        public const string DeleteRemoteTagText = "deletion of tag v1.1 on origin";

        public const string UpstreamText = "upstream change of topic";

        public const string AddRemoteText = "addition of remote mirror";

        public const string RenameRemoteText = "rename of remote origin to upstream";

        public const string UrlText = "URL change of origin";

        public const string RemoveRemoteText = "removal of remote origin";

        public const string IgnoreText = "ignore of /logs/run.log";

        public const string MergeFfText = "merge of feature/ff into main";

        /// <summary>U23 part B: the merge that stops with a conflict, pending while its banner shows (7.2).</summary>
        public const string MergeConflictText = "merge of feature/conflict into main";

        public const string CreateHotfixText = "creation of hotfix";

        public const string RebaseText = "rebase of feature/topic onto main";

        // ---- Status texts of the actions (earlier phases', unchanged) ----

        public const string Stashed = "Stashed the changes";

        public const string Applied = "Applied stash@{0}";

        public const string Popped = "Popped stash@{0}";

        public const string Dropped = "Dropped stash@{0}";

        public const string CreatedTag = "Created tag v1.2";

        public const string DeletedTag = "Deleted tag v1.0";

        public const string PushedTopic = "Pushed topic to origin/topic, and set it as upstream";

        public const string ForcePushed = "Force-pushed published to origin/published";

        public const string DeletedPublished = "Deleted published on origin";

        public const string PushedPublished = "Pushed published to origin/published";

        public const string PushedTag = "Pushed tag v1.1 to origin";

        public const string DeletedRemoteTag = "Deleted tag v1.1 on origin";

        public const string TracksPublished = "topic now tracks origin/published";

        /// <summary>U22 step 4: the add's own result, before its fetch's status replaces it.</summary>
        public const string AddedRemote = "Added remote mirror";

        public const string FetchedFromMirror = "Fetched from mirror";

        public const string RenamedRemote = "Renamed remote origin to upstream";

        public const string ChangedUrl = "Changed the URL of origin";

        public const string RemovedRemote = "Removed remote origin";

        public const string Ignored = "Added /logs/run.log to .gitignore";

        public const string FastForwarded = "Fast-forwarded main to feature/ff";

        public const string CreatedHotfix = "Created hotfix";

        public const string MergeStopped = "Merge stopped: 1 file has conflicts";

        public const string AbortedMerge = "Aborted the merge";

        public const string Rebased = "Rebased feature/topic onto main";

        // ---- The confirmations of the actions (phase 3's) ----

        /// <summary>Phase 3's detail line of a confirmation whose action saves a backup.</summary>
        public const string BackupDetail = "A backup is saved first, so this can be undone.";

        public const string DropTitle = "Drop stash?";

        public const string DropQuestion = "Drop stash@{0} \"On main: Experiment\"?";

        public const string DeleteTagTitle = "Delete tag?";

        public const string DeleteTagQuestion = "Delete the tag v1.0 here? It stays on remotes it was pushed to.";

        public const string ForcePushTitle = "Force push published?";

        public const string ForcePushQuestion =
            "Replace published on origin with your published? Commits on origin that your branch does not have are dropped there. The push stops if origin has commits you have not integrated into your published, even ones the last fetch brought.";

        public const string DeleteBranchTitle = "Delete published on origin?";

        public const string DeleteBranchQuestion = "Delete the branch published on origin? Everyone who uses origin loses it.";

        public const string DeleteRemoteTagTitle = "Delete v1.1 on origin?";

        public const string DeleteRemoteTagQuestion = "Delete the tag v1.1 on origin? Everyone who uses origin loses it.";

        public const string RemoveRemoteTitle = "Remove remote origin?";

        /// <summary>Phase 3's question before removing origin, which names its URL: the copy's origin.git folder.</summary>
        public static string RemoveRemoteQuestion(string url) =>
            $"Remove the remote origin and its 2 remote branches? Your local branches stay; those that track it lose their upstream. Its URL was {url}.";

        // ---- The questions of a step that changes a remote (7.5, UD10) ----

        /// <summary>A question's title and text as <c>ConfirmTitle</c> and <c>ConfirmQuestion</c> show them; it has no detail line, and its button is "Undo" or "Redo" in the primary style.</summary>
        public sealed record Question(string Title, string Text);

        public static Question UndoPushTopic { get; } = new("Delete topic on origin?", "Undoing the push deletes topic on origin. Anyone who fetched it keeps it.");

        public static Question RedoPushTopic { get; } = new("Push topic to origin again?", "origin gets topic back at 77d782e.");

        public static Question UndoForcePush { get; } = new("Undo the push to origin?", "published on origin goes back to 8c6f545, which it was before the push. Anyone who fetched 4f6a372 keeps it.");

        public static Question RedoForcePush { get; } = new("Redo the push to origin?", "published on origin moves to 4f6a372 again.");

        public static Question UndoDeletePublished { get; } = new("Undo the deletion of published on origin?", "origin gets published back at 8c6f545.");

        public static Question RedoDeletePublished { get; } = new("Delete published on origin again?", "published is deleted on origin again.");

        public static Question UndoPushTag { get; } = new("Delete tag v1.1 on origin?", "Undoing the push deletes tag v1.1 on origin. Anyone who fetched it keeps it.");

        public static Question RedoPushTag { get; } = new("Push tag v1.1 to origin again?", "origin gets tag v1.1 back at 88d6b91.");

        public static Question UndoDeleteTag { get; } = new("Undo the deletion of tag v1.1 on origin?", "origin gets tag v1.1 back at 88d6b91.");

        public static Question RedoDeleteTag { get; } = new("Delete tag v1.1 on origin again?", "tag v1.1 is deleted on origin again.");

        public static Question UndoLeasedPush { get; } = new("Undo the push to origin?", "published on origin goes back to 8c6f545, which it was before the push. Anyone who fetched 8e65315 keeps it.");

        // ---- Refusals (7.4, 7.6, UD9, UD13, UD14) ----

        /// <summary>U23 part A: Undo's tooltip once main moved outside the app.</summary>
        public const string MovedSince = "main has moved since (it is at c7d8c71)";

        /// <summary>U23 part B: both buttons' tooltip during the merge.</summary>
        public const string FinishOrAbortMerge = "Finish or abort the merge first";

        /// <summary>U23 part B: the conflict banner during the merge (phase 3's check 9).</summary>
        public const string MergeBanner = "Merging: \"Merge branch 'feature/conflict'\". 1 file has conflicts.";

        /// <summary>U21 part F: the lease's summary, without 7.6's prefix (UD9).</summary>
        public const string LeaseSummary = "published on origin has changed since: nothing was changed.";

        /// <summary>U23 part C: two changed files in the Keep move's way, counted by the app.</summary>
        public const string ChangesInTheWaySummary = "Can't undo rebase of feature/topic onto main: 2 conflicts prevent it. Commit, stash, or discard your changes first.";

        /// <summary>U23 part D: the ignored file in the redo's way, the app's own check.</summary>
        public const string IgnoredInTheWaySummary = "Can't redo merge of feature/ff into main: 1 ignored file is in the way: docs/ff.txt. Move it away first.";

        // ---- Git's texts (Git 2.36.0.windows.1, the same on 2.55.0.windows.5) ----

        /// <summary>TU1: read-tree's refusal of the Keep move, exit 128 (key: its only line).</summary>
        public const string Tu1 = "error: Entry 'docs/main.txt' not uptodate. Cannot merge.";

        /// <summary>The dry run's refusal of U23 part E: the same words for docs/ff.txt.</summary>
        public const string StaleRefusal = "error: Entry 'docs/ff.txt' not uptodate. Cannot merge.";

        /// <summary>TU2's key line: the push whose lease fails.</summary>
        public const string Tu2Key = " ! [rejected]        8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6 -> published (stale info)";

        /// <summary>TU2: the push whose lease fails, exit 1, three lines; <paramref name="origin"/> is the copy's origin.git as <c>git remote get-url origin</c> prints it.</summary>
        public static IReadOnlyList<string> Tu2(string origin) =>
            [$"To {origin}", Tu2Key, $"error: failed to push some refs to '{origin}'"];

        /// <summary>TU3: the summary lines of the pushes that succeed.</summary>
        public const string DeletedTopicLine = " - [deleted]         topic";

        public const string NewTopicLine = " * [new branch]      77d782ebfce02f1fe2b29c2ee2b6b43a1f682a02 -> topic";

        public const string ForceUndoneLine = " + 4f6a372...8c6f545 8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6 -> published (forced update)";

        public const string ForceRedoneLine = " + 8c6f545...4f6a372 4f6a37288fadb5c453061cefedee620a9f98121e -> published (forced update)";

        public const string NewPublishedLine = " * [new branch]      8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6 -> published";

        public const string DeletedPublishedLine = " - [deleted]         published";

        public const string DeletedTagLine = " - [deleted]         v1.1";

        public const string NewTagLine = " * [new tag]         88d6b91e821a43cd39c94aa7892c8095b628caf8 -> v1.1";

        // ---- Commands (as GitCallRecord.CommandText writes them) ----

        /// <summary>A command line as the call log writes it (<see cref="GitCommand.DisplayText"/>): an argument with a space in quotes.</summary>
        public static string Command(params string[] arguments) => new GitCommand(arguments).DisplayText;

        /// <summary>The reflog message of an undo's ref writes (7.3).</summary>
        public static string UndoMessage(string text) => "VisualCommit: undo " + text;

        /// <summary>The reflog message of a redo's ref writes (7.3).</summary>
        public static string RedoMessage(string text) => "VisualCommit: redo " + text;

        /// <summary>A step's <c>update-ref -m &lt;message&gt; --stdin</c> transaction (UD20).</summary>
        public static string Transaction(string message) => Command("update-ref", "-m", message, "--stdin");

        /// <summary>
        /// A step's push of a remote ref (0.5, D170): with the lease on what the remote must hold now
        /// (<paramref name="from"/>, empty when it must not exist), to <paramref name="to"/>, or
        /// <c>--delete</c> when <paramref name="to"/> is null.
        /// </summary>
        public static string StepPush(string fullRef, string? from, string? to) =>
            to is null
                ? Command("push", "--progress", $"--force-with-lease={fullRef}:{from}", "origin", "--delete", fullRef)
                : Command("push", "--progress", $"--force-with-lease={fullRef}:{from}", "origin", $"{to}:{fullRef}");

        public const string StashPush = "git stash push --include-untracked --message \"Save my work\"";

        public const string StashPopIndex = "git stash pop --index stash@{0}";

        public const string StashApply = "git stash apply stash@{0}";

        public const string StashPop = "git stash pop stash@{0}";

        public const string StashDrop = "git stash drop stash@{0}";

        public const string StashStore = $"git stash store -m \"On main: Experiment\" {Experiment}";

        public const string IndexInfo = "git update-index -z --index-info";

        public const string CreateTag = $"git tag --annotate --cleanup=whitespace --file=- v1.2 {AlphaTwo}^{{commit}}";

        public const string DeleteTag = "git tag --delete v1.0";

        public const string PushTopic = "git push --progress --set-upstream origin refs/heads/topic:refs/heads/topic";

        public const string ForcePush = "git push --progress --force-with-lease=refs/heads/published --force-if-includes origin refs/heads/published:refs/heads/published";

        public const string PushPublished = "git push --progress origin refs/heads/published:refs/heads/published";

        public const string PushTag = "git push --progress origin refs/tags/v1.1:refs/tags/v1.1";

        public const string RemoveTopicSection = "git config --local --remove-section branch.topic";

        public const string SetUpstream = "git branch --set-upstream-to=refs/remotes/origin/published topic";

        public const string RenameRemote = "git remote rename origin upstream";

        public const string RenameRemoteBack = "git remote rename upstream origin";

        public const string RemoveRemote = "git remote remove origin";

        public const string FetchMirror = "git fetch --progress mirror";

        public const string RefreshIndex = "git update-index -q --refresh";

        /// <summary>U23's "Merge feature/ff into main" (U19's step 1): phase 3's merge, which fast-forwards here.</summary>
        public const string MergeFf = "git merge --no-edit --no-log -m \"Merge branch 'feature/ff'\" --no-overwrite-ignore refs/heads/feature/ff";

        /// <summary>HEAD's reflog's newest entry after that merge: a merge given -m that fast-forwards says it made no commit.</summary>
        public const string MergeFfReflog = "merge refs/heads/feature/ff: Fast-forward (no commit created; -m option ignored)";

        /// <summary>The status line of "ops" after that merge.</summary>
        public const string OnMainAheadOne = "## main...origin/main [ahead 1]";

        // ---- The graphs (rows from the top; "Scenarios and graphs of part U") ----

        /// <summary>The working-changes row with its ring in lane 0, colour 0.</summary>
        public static WorkingChangesRow Working { get; } = new("Working changes", 0, 0);

        /// <summary>The working-changes row after a pop (G0-s): its ring in Main work's lane 1, colour 1.</summary>
        public static WorkingChangesRow WorkingAfterPop { get; } = new("Working changes", 1, 1);

        /// <summary>A stash row: a ring.</summary>
        public static CommitRow Stash(string message, int lane, int colour, string selector, string sha) => new(message, lane, colour, [selector], sha, Stash: true);

        /// <summary>A commit row with its short SHA.</summary>
        public static CommitRow Commit(string message, int lane, int colour, string sha, params string[] labels) => new(message, lane, colour, labels, sha);

        /// <summary>G0: "ops" as built, 3 lanes, the stash's line dashed.</summary>
        public static IReadOnlyList<CommitRow> G0 { get; } =
        [
            Stash("On main: Experiment", 0, 0, "stash@{0}", "0e95aca") with { Date = "2026-01-01 12:07" },
            Commit("Conflicting change", 1, 1, "078ef05", "feature/conflict") with { Date = "2026-01-01 12:06" },
            Commit("Fast-forward me", 2, 2, "d93814d", "feature/ff") with { Date = "2026-01-01 12:05" },
            Commit("Main work", 0, 0, "88d6b91", Current("main"), "origin/main", "v1.1") with { Date = "2026-01-01 12:04", Ends = [2] },
            Commit("Alpha two", 2, 3, "ce5ea6a", "feature/alpha", "origin/feature/alpha") with { Date = "2026-01-01 12:03" },
            Commit("Alpha one", 2, 3, "dff8fbf") with { Date = "2026-01-01 12:02" },
            Commit("Add core", 0, 0, "8beca34", "v1.0") with { Date = "2026-01-01 12:01", Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "4ef1f7c") with { Date = "2026-01-01 12:00" },
        ];

        /// <summary>G0's lanes.</summary>
        public const int G0Lanes = 3;

        /// <summary>G0's rows of "Fast-forward me", "Main work", "Alpha two" and "Add core".</summary>
        public const int FastForwardRow = 2;

        public const int MainWorkRow = 3;

        public const int AlphaTwoRow = 4;

        public const int ConflictingRow = 1;

        /// <summary>G0-S: two stashes, 4 lanes (after U20's stash and its redo).</summary>
        public static IReadOnlyList<CommitRow> G0TwoStashes { get; } =
        [
            Stash("On main: Save my work", 0, 0, "stash@{0}", "b80f1ca") with { Date = "2026-01-02 09:00" },
            Stash("On main: Experiment", 1, 1, "stash@{1}", "0e95aca"),
            Commit("Conflicting change", 2, 2, "078ef05", "feature/conflict"),
            Commit("Fast-forward me", 3, 3, "d93814d", "feature/ff"),
            Commit("Main work", 0, 0, "88d6b91", Current("main"), "origin/main", "v1.1"),
            Commit("Alpha two", 1, 4, "ce5ea6a", "feature/alpha", "origin/feature/alpha"),
            Commit("Alpha one", 1, 4, "dff8fbf"),
            Commit("Add core", 0, 0, "8beca34", "v1.0"),
            Commit("Initial commit", 0, 0, "4ef1f7c"),
        ];

        /// <summary>G0-s: no stash (after a pop or a drop), 3 lanes, 7 rows.</summary>
        public static IReadOnlyList<CommitRow> G0NoStash { get; } =
        [
            Commit("Conflicting change", 0, 0, "078ef05", "feature/conflict"),
            Commit("Fast-forward me", 1, 1, "d93814d", "feature/ff"),
            Commit("Main work", 1, 1, "88d6b91", Current("main"), "origin/main", "v1.1"),
            Commit("Alpha two", 2, 2, "ce5ea6a", "feature/alpha", "origin/feature/alpha"),
            Commit("Alpha one", 2, 2, "dff8fbf"),
            Commit("Add core", 0, 0, "8beca34", "v1.0") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "4ef1f7c"),
        ];

        /// <summary>G0 v1.2: row 4 with v1.2 after its upstream.</summary>
        public static IReadOnlyList<CommitRow> G0WithV12 { get; } = WithLabels(G0, AlphaTwoRow, "feature/alpha", "origin/feature/alpha", "v1.2");

        /// <summary>G0 −v1.0: row 6 without a label.</summary>
        public static IReadOnlyList<CommitRow> G0WithoutV10 { get; } = WithLabels(G0, 6);

        /// <summary>G0 hotfix: hotfix beside main on row 3.</summary>
        public static IReadOnlyList<CommitRow> G0Hotfix { get; } = WithLabels(G0, MainWorkRow, Current("main"), "hotfix", "origin/main", "v1.1");

        /// <summary>G1: main fast-forwarded to Fast-forward me; lanes, colours and lines as G0's.</summary>
        public static IReadOnlyList<CommitRow> G1 { get; } =
            WithLabels(WithLabels(G0, FastForwardRow, Current("main"), "feature/ff"), MainWorkRow, "origin/main", "v1.1");

        /// <summary>G0-T: "Terminal work" committed outside on top of G1, 9 rows, 3 lanes.</summary>
        public static IReadOnlyList<CommitRow> G0Terminal { get; } =
        [
            Commit("Terminal work", 0, 0, "c7d8c71", Current("main")) with { Date = "2026-01-01 12:08" },
            Stash("On main: Experiment", 1, 1, "stash@{0}", "0e95aca"),
            Commit("Conflicting change", 2, 2, "078ef05", "feature/conflict"),
            Commit("Fast-forward me", 0, 0, "d93814d", "feature/ff"),
            Commit("Main work", 0, 0, "88d6b91", "origin/main", "v1.1"),
            Commit("Alpha two", 1, 3, "ce5ea6a", "feature/alpha", "origin/feature/alpha"),
            Commit("Alpha one", 1, 3, "dff8fbf"),
            Commit("Add core", 0, 0, "8beca34", "v1.0"),
            Commit("Initial commit", 0, 0, "4ef1f7c"),
        ];

        /// <summary>G0-T's row of "Fast-forward me", which U23 step 4 clicks.</summary>
        public const int TerminalFastForwardRow = 3;

        /// <summary>The "sync" graph as built: 2 lanes.</summary>
        public static IReadOnlyList<CommitRow> Sync { get; } =
        [
            Commit("Topic work", 0, 0, "77d782e", "topic") with { Date = "2026-01-01 12:04" },
            Commit("Unpushed work", 1, 1, "8e65315", "published") with { Date = "2026-01-01 12:03" },
            Commit("Published work", 1, 1, "8c6f545", "origin/published") with { Date = "2026-01-01 12:02" },
            Commit("Shared work", 0, 0, "a7f2cfd", Current("main"), "origin/main") with { Date = "2026-01-01 12:01", Ends = [1] },
            Commit("Initial commit", 0, 0, "6688774") with { Date = "2026-01-01 12:00" },
        ];

        /// <summary>The sync graph's lanes.</summary>
        public const int SyncLanes = 2;

        /// <summary>S-topic: topic pushed (origin/topic beside it).</summary>
        public static IReadOnlyList<CommitRow> SyncTopicPushed { get; } = WithLabels(Sync, 0, "topic", "origin/topic");

        /// <summary>S-pushed: published pushed (origin/published on Unpushed work).</summary>
        public static IReadOnlyList<CommitRow> SyncPublishedPushed { get; } = WithLabels(WithLabels(Sync, 1, "published", "origin/published"), 2);

        /// <summary>S-del: published deleted on origin.</summary>
        public static IReadOnlyList<CommitRow> SyncPublishedDeleted { get; } = WithLabels(Sync, 2);

        /// <summary>S-up: the remote renamed upstream.</summary>
        public static IReadOnlyList<CommitRow> SyncRenamed { get; } = WithLabels(WithLabels(Sync, 2, "upstream/published"), 3, Current("main"), "upstream/main");

        /// <summary>S-none: the remote removed.</summary>
        public static IReadOnlyList<CommitRow> SyncWithoutRemote { get; } = WithLabels(WithLabels(Sync, 2), 3, Current("main"));

        /// <summary>The sync graph with mirror added and fetched (U22 after step 4).</summary>
        public static IReadOnlyList<CommitRow> SyncWithMirror { get; } =
            WithLabels(WithLabels(Sync, 2, "mirror/published", "origin/published"), 3, Current("main"), "mirror/main", "origin/main");

        /// <summary>S-F0: "Rewritten work" on published (phase 3's check 20 rewrite), 3 lanes.</summary>
        public static IReadOnlyList<CommitRow> SyncRewritten { get; } =
        [
            Commit("Rewritten work", 0, 0, "4f6a372", "published") with { Date = "2026-01-01 12:05" },
            Commit("Topic work", 1, 1, "77d782e", "topic"),
            Commit("Published work", 2, 2, "8c6f545", "origin/published"),
            Commit("Shared work", 0, 0, "a7f2cfd", Current("main"), "origin/main"),
            Commit("Initial commit", 0, 0, "6688774"),
        ];

        /// <summary>S-F1: after the force push, 2 lanes.</summary>
        public static IReadOnlyList<CommitRow> SyncForcePushed { get; } =
        [
            Commit("Rewritten work", 0, 0, "4f6a372", "published", "origin/published"),
            Commit("Topic work", 1, 1, "77d782e", "topic"),
            Commit("Shared work", 0, 0, "a7f2cfd", Current("main"), "origin/main"),
            Commit("Initial commit", 0, 0, "6688774"),
        ];

        /// <summary>R0: "rebase" as built (phase 4's), 3 lanes.</summary>
        public static IReadOnlyList<CommitRow> R0 { get; } =
        [
            Commit("Other one", 0, 0, "2d4d79a", "feature/other"),
            Commit("Main more", 1, 1, "1dd58b8", "main", "origin/main"),
            Commit("Main work", 1, 1, "dd6a6fc"),
            Commit("Topic two", 2, 2, "b0dfa3f", Current("feature/topic"), "origin/feature/topic"),
            Commit("Topic one", 2, 2, "1219e45"),
            Commit("Add core", 0, 0, "996fdc1") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "7bce6bf"),
        ];

        /// <summary>R1: after "Rebase feature/topic onto main" (phase 4's), 3 lanes.</summary>
        public static IReadOnlyList<CommitRow> R1 { get; } =
        [
            Commit("Topic two", 0, 0, "5f71ab8", Current("feature/topic")),
            Commit("Topic one", 0, 0, "befae26"),
            Commit("Other one", 1, 1, "2d4d79a", "feature/other"),
            Commit("Main more", 0, 0, "1dd58b8", "main", "origin/main"),
            Commit("Main work", 0, 0, "dd6a6fc"),
            Commit("Topic two", 2, 2, "b0dfa3f", "origin/feature/topic"),
            Commit("Topic one", 2, 2, "1219e45"),
            Commit("Add core", 0, 0, "996fdc1") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "7bce6bf"),
        ];

        /// <summary>R0's and R1's lanes.</summary>
        public const int RebaseLanes = 3;

        /// <summary>A copy of <paramref name="rows"/> with one row's labels replaced (none to leave it without).</summary>
        public static IReadOnlyList<CommitRow> WithLabels(IReadOnlyList<CommitRow> rows, int row, params string[] labels)
        {
            ArgumentNullException.ThrowIfNull(rows);
            var copy = rows.ToList();
            copy[row] = copy[row] with { Labels = labels };
            return copy;
        }

        // ---- What git holds ----

        /// <summary><c>git stash list --format=%gd %H %gs</c>'s line of a stash.</summary>
        public static string StashLine(int index, string sha, string message) => $"stash@{{{index}}} {sha} {message}";

        /// <summary>The stash list with "Experiment" alone.</summary>
        public static IReadOnlyList<string> ExperimentOnly { get; } = [StashLine(0, Experiment, "On main: Experiment")];

        /// <summary>The stash list after U20's stash: "Save my work", then "Experiment".</summary>
        public static IReadOnlyList<string> TwoStashes { get; } = [StashLine(0, SaveMyWork, "On main: Save my work"), StashLine(1, Experiment, "On main: Experiment")];

        /// <summary>The status line of the branch in "ops" and "sync" while it matches origin's.</summary>
        public const string OnMain = "## main...origin/main";

        /// <summary>v1.2's tag object as <c>git cat-file -p</c> prints it, line by line.</summary>
        public static IReadOnlyList<string> V12Object { get; } =
            [$"object {AlphaTwo}", "type commit", "tag v1.2", "tagger Test Author <author@example.com> 1767344400 +0000", string.Empty, "#42 hotfix"];

        /// <summary>The name of the first backup a session makes (design 2.3): <c>refs/visualcommit/backup/&lt;kind&gt;-20260102T090001000Z</c>, then <c>/&lt;name&gt;</c> when given.</summary>
        public static string FirstBackup(string kind, string? name = null) =>
            BackupRef.Prefix + kind + "-20260102T090001000Z" + (name is null ? string.Empty : "/" + name);
    }
}
