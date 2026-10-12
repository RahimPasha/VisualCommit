// Part C's expected results for checks C10 to C13 (docs/test-reports/phase-4.md, part C: "Texts of
// checks C10 to C13", "The scenario's graphs (checks C10 to C13)", "Commits the checks make",
// "The conflicted files, the panes and the results", "The tints" and the rows C10 to C13 of
// "Checks"), copied from the report and not read from the app. Its members live in
// Phase4Expectations.PartC (partial: checks C14 to C17 add theirs in a file of their own).
// Compiled by the real-window pass too: data and pure helpers only (see
// Phase4Expectations.Common.cs for what may be used here).
using static VisualCommit.Testing.Scenarios;

namespace VisualCommit.VisualTests.Phase4;

public static partial class Phase4Expectations
{
    /// <summary>What part C's checks compare against.</summary>
    public static partial class PartC
    {
        // ---- The conflicts scenario's commits (Phase4ScenarioTests pins them) and those the checks make. ----

        /// <summary>The commits of "conflicts" and of C10 to C13, in full, by the names the report gives them.</summary>
        public static class StopSha
        {
            public const string OursEdits = "da31c977c80e9becdc908c74d973669aad1b3b8d";
            public const string KindsEdits = "3a9de3e";
            public const string PickLast = "5c4c2576f7402a420fa3b4ce83fc72b9b40804e4";
            public const string PickConflicting = "01fd97607f151aa6e4877c0fa026633e97b65288";
            public const string PickClean = "01d2046adb60575079cdbe3d889a2ab3f408c643";
            public const string SecondChange = "305f7f7d23b8d07c0a12c592b9b83d989a67a71a";
            public const string FirstChange = "70e8c78df98b8c7261e991c307061184f314fc78";
            public const string TheirsEdits = "5c77ffb0ec78abf986d121f42013eee14422e5a5";
            public const string Base = "195768b";
            public const string Initial = "6bd579f";

            /// <summary>C10 step 12: the merge commit Continue makes.</summary>
            public const string Merged = "8a12c6942e100029b4018deb672c2ea8b11fe54d";

            /// <summary>C11 step 8: First change, committed at the first stop.</summary>
            public const string NewFirst = "99070f8227e308c2edf7885287586b947f173eb1";

            /// <summary>C11 step 11: Second change, committed at the second stop.</summary>
            public const string NewSecond = "2083808b1f1a77b23b0b354211dd317f02388cb2";

            /// <summary>C12 step 2: the list's first pick, Pick clean's copy.</summary>
            public const string PickedClean = "25c817e0f548bb169036f2bd921a4d01cb8d9f80";

            /// <summary>C12 step 8: Pick conflicting's copy with the edited message.</summary>
            public const string PickedConflicting = "d5129054a81713f8517a53a815ffd34a351e4a88";

            /// <summary>C12 step 8: Pick last's copy, made by <c>git cherry-pick --continue</c>.</summary>
            public const string PickedLast = "9d814c45b70681eef953b6b62d2fc5a05b9de00a";

            /// <summary>C13 part B: Pick clean's copy the terminal's list made at 12:10 (pinned).</summary>
            public const string TerminalPickedClean = "d71cdc4e9eb7fa80dc917e6f859204a0286e78ac";

            /// <summary>The first 7 characters of a SHA, as the screen shows it.</summary>
            public static string Short(string sha)
            {
                ArgumentNullException.ThrowIfNull(sha);
                return sha[..7];
            }
        }

        /// <summary>The branches of "conflicts" and their tips, as <c>git for-each-ref refs/heads</c> prints them before any check changes them (GC0).</summary>
        public static IReadOnlyList<(string Ref, string ShortSha)> ConflictsBranches { get; } =
        [
            ("refs/heads/feature/kinds", "3a9de3e"),
            ("refs/heads/feature/picks", "5c4c257"),
            ("refs/heads/feature/rebase", "305f7f7"),
            ("refs/heads/feature/theirs", "5c77ffb"),
            ("refs/heads/main", "da31c97"),
        ];

        // ---- The graphs (the report's GC tables). ----

        /// <summary>The working-changes row with one conflicted file (D122's text).</summary>
        public static WorkingChangesRow WorkingOneConflict { get; } = new("Working changes · 1 conflict", 0, 0);

        /// <summary>The working-changes row with nothing conflicted.</summary>
        public static WorkingChangesRow WorkingNoConflict { get; } = new("Working changes", 0, 0);

        /// <summary>GC0's rows 1 to 9 (Kinds edits to Initial commit), the same in every graph of "conflicts" that keeps 5 lanes.</summary>
        private static readonly CommitRow[] Gc0Below =
        [
            Row("Kinds edits", 1, 1, "feature/kinds") with { ShortSha = "3a9de3e" },
            Row("Pick last", 2, 2, "feature/picks") with { ShortSha = "5c4c257" },
            Row("Pick conflicting", 2, 2) with { ShortSha = "01fd976" },
            Row("Pick clean", 2, 2) with { ShortSha = "01d2046" },
            Row("Second change", 3, 3, "feature/rebase") with { ShortSha = "305f7f7" },
            Row("First change", 3, 3) with { ShortSha = "70e8c78" },
            Row("Theirs edits", 4, 4, "feature/theirs") with { ShortSha = "5c77ffb" },
            Row("Base", 0, 0) with { ShortSha = "195768b", Ends = [1, 2, 3, 4], Merge = false },
            Row("Initial commit", 0, 0) with { ShortSha = "6bd579f" },
        ];

        /// <summary>GC0: "conflicts" as built, HEAD on a clean main. 5 lanes, the Date column hidden at 1100×700.</summary>
        public static IReadOnlyList<CommitRow> Gc0 { get; } =
        [
            Row("Ours edits", 0, 0, Current("main")) with { ShortSha = "da31c97" },
            .. Gc0Below,
        ];

        /// <summary>GC0r: GC0 with feature/rebase checked out.</summary>
        public static IReadOnlyList<CommitRow> Gc0r { get; } =
        [
            Row("Ours edits", 0, 0, "main") with { ShortSha = "da31c97" },
            Gc0Below[0],
            Gc0Below[1],
            Gc0Below[2],
            Gc0Below[3],
            Row("Second change", 3, 3, Current("feature/rebase")) with { ShortSha = "305f7f7" },
            .. Gc0Below[5..],
        ];

        /// <summary>GC-M, the merge stopped: GC0's commit rows below the working-changes row (<see cref="WorkingOneConflict"/>).</summary>
        public static IReadOnlyList<CommitRow> GcMerge => Gc0;

        /// <summary>GC-R1, a rebase stopped with HEAD detached at Ours edits: the filled label HEAD, then main; feature/rebase without a check mark.</summary>
        public static IReadOnlyList<CommitRow> GcRebaseFirst { get; } =
        [
            Row("Ours edits", 0, 0, "HEAD", "main") with { ShortSha = "da31c97" },
            .. Gc0Below,
        ];

        /// <summary>GC-R2, C11's second stop: HEAD detached at the new First change.</summary>
        public static IReadOnlyList<CommitRow> GcRebaseSecond { get; } =
        [
            Row("First change", 0, 0, "HEAD") with { ShortSha = "99070f8" },
            Row("Ours edits", 0, 0, "main") with { ShortSha = "da31c97" },
            .. Gc0Below,
        ];

        /// <summary>GC-Rend, C11 after the rebase. 4 lanes.</summary>
        public static IReadOnlyList<CommitRow> GcRebased { get; } =
        [
            Row("Second change", 0, 0, Current("feature/rebase")) with { ShortSha = "2083808" },
            Row("First change", 0, 0) with { ShortSha = "99070f8" },
            Row("Ours edits", 0, 0, "main") with { ShortSha = "da31c97" },
            Row("Kinds edits", 1, 1, "feature/kinds") with { ShortSha = "3a9de3e" },
            Row("Pick last", 2, 2, "feature/picks") with { ShortSha = "5c4c257" },
            Row("Pick conflicting", 2, 2) with { ShortSha = "01fd976" },
            Row("Pick clean", 2, 2) with { ShortSha = "01d2046" },
            Row("Theirs edits", 3, 3, "feature/theirs") with { ShortSha = "5c77ffb" },
            Row("Base", 0, 0) with { ShortSha = "195768b", Ends = [1, 2, 3], Merge = false },
            Row("Initial commit", 0, 0) with { ShortSha = "6bd579f" },
        ];

        /// <summary>GC10, C10 after Continue: the merge commit on main, its second parent in lane 1. 5 lanes.</summary>
        public static IReadOnlyList<CommitRow> GcMerged { get; } =
        [
            Row("Merge branch 'feature/theirs'", 0, 0, Current("main")) with { ShortSha = "8a12c69", Merge = true },
            Row("Ours edits", 0, 0) with { ShortSha = "da31c97" },
            Row("Kinds edits", 2, 2, "feature/kinds") with { ShortSha = "3a9de3e" },
            Row("Pick last", 3, 3, "feature/picks") with { ShortSha = "5c4c257" },
            Row("Pick conflicting", 3, 3) with { ShortSha = "01fd976" },
            Row("Pick clean", 3, 3) with { ShortSha = "01d2046" },
            Row("Second change", 4, 4, "feature/rebase") with { ShortSha = "305f7f7" },
            Row("First change", 4, 4) with { ShortSha = "70e8c78" },
            Row("Theirs edits", 1, 1, "feature/theirs") with { ShortSha = "5c77ffb" },
            Row("Base", 0, 0) with { ShortSha = "195768b", Ends = [1, 2, 3, 4], Merge = false },
            Row("Initial commit", 0, 0) with { ShortSha = "6bd579f" },
        ];

        /// <summary>GC-P, C12's list stopped: main at the copy of Pick clean (25c817e), below the working-changes row.</summary>
        public static IReadOnlyList<CommitRow> GcPicksStopped { get; } =
        [
            Row("Pick clean", 0, 0, Current("main")) with { ShortSha = "25c817e" },
            Row("Ours edits", 0, 0) with { ShortSha = "da31c97" },
            .. Gc0Below,
        ];

        /// <summary>GC-PB, the terminal's list stopped (C13 part B): GC-P with d71cdc4 in place of 25c817e.</summary>
        public static IReadOnlyList<CommitRow> GcTerminalPicksStopped { get; } =
        [
            Row("Pick clean", 0, 0, Current("main")) with { ShortSha = "d71cdc4" },
            Row("Ours edits", 0, 0) with { ShortSha = "da31c97" },
            .. Gc0Below,
        ];

        /// <summary>GC-Pend, C12 after Continue. 5 lanes.</summary>
        public static IReadOnlyList<CommitRow> GcPicked { get; } =
        [
            Row("Pick last", 0, 0, Current("main")) with { ShortSha = "9d814c4" },
            Row("Pick conflicting, resolved", 0, 0) with { ShortSha = "d512905" },
            Row("Pick clean", 0, 0) with { ShortSha = "25c817e" },
            Row("Ours edits", 0, 0) with { ShortSha = "da31c97" },
            .. Gc0Below,
        ];

        // ---- Texts of checks C10 to C13. ----

        /// <summary>The banner of C10's merge.</summary>
        public const string MergeBanner = "Merging: \"Merge branch 'feature/theirs'\". 1 file has conflicts.";

        /// <summary>The banner of C10's merge once notes.txt is resolved.</summary>
        public const string MergeResolvedBanner = "Merging: \"Merge branch 'feature/theirs'\". Conflicts resolved: commit to finish the merge.";

        /// <summary>The banner at the rebase's first stop (C11, C13).</summary>
        public const string RebaseFirstBanner = "Rebasing feature/rebase onto main: stopped at 70e8c78 \"First change\" (1 of 2). 1 file has conflicts.";

        /// <summary>The banner at the rebase's first stop once notes.txt is resolved (C11).</summary>
        public const string RebaseFirstResolvedBanner = "Rebasing feature/rebase onto main: stopped at 70e8c78 \"First change\" (1 of 2). Conflicts resolved: continue to commit it and go on.";

        /// <summary>The banner at the rebase's second stop (C11, C13).</summary>
        public const string RebaseSecondBanner = "Rebasing feature/rebase onto main: stopped at 305f7f7 \"Second change\" (2 of 2). 1 file has conflicts.";

        /// <summary>The banner of a pick in a list (C12, C13).</summary>
        public const string PickListBanner = "Cherry-picking 01fd976 \"Pick conflicting\". 1 file has conflicts. 1 more commit waits: Continue picks it.";

        /// <summary>The banner of a pick in a list once notes.txt is resolved (C12).</summary>
        public const string PickListResolvedBanner = "Cherry-picking 01fd976 \"Pick conflicting\". Conflicts resolved: continue to commit it. 1 more commit waits: Continue picks it.";

        /// <summary>The banner's icon tooltip of the merge (10.3).</summary>
        public const string MergeBannerTip = "Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.";

        /// <summary>The banner's icon tooltip of the rebase (10.3).</summary>
        public const string RebaseBannerTip = "Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts feature/rebase back where it was before the rebase.";

        /// <summary>The banner's icon tooltip of a pick in a list (10.3).</summary>
        public const string PickListBannerTip = "Continue commits the resolved files and goes on with the commits that wait. Skip leaves this commit out and goes on. Abort puts main back where it was before the cherry-pick: the commits it already made are dropped too.";

        /// <summary>Continue's disabled reason while one file conflicts (10.4).</summary>
        public const string ResolveFirstOneFile = "Resolve the conflicts first (1 file)";

        /// <summary>The banner's Abort of a merge, a rebase and a list.</summary>
        public const string AbortMerge = "Abort merge";

        public const string AbortRebase = "Abort rebase";

        public const string AbortCherryPick = "Abort cherry-pick";

        /// <summary>The conflicted row's tooltip in the stage panel (5.3).</summary>
        public const string ConflictRowTip = "Conflict: click to resolve it";

        /// <summary>The stage panel's titles.</summary>
        public const string UnstagedOneConflict = "Unstaged files (1, 1 with conflicts)";

        public const string UnstagedNone = "Unstaged files (0)";

        public const string StagedNone = "Staged files (0)";

        public const string StagedOne = "Staged files (1)";

        public const string NoUnstagedChanges = "No unstaged changes";

        public const string NoStagedChanges = "No staged changes";

        /// <summary>The stage panel's commit button.</summary>
        public const string StageFilesToCommit = "Stage files to commit";

        public const string CommitOneFile = "Commit 1 file";

        public const string ContinueRebaseButton = "Continue rebase";

        public const string ContinueCherryPickButton = "Continue cherry-pick";

        /// <summary>Amend's reason during a rebase stop and a list (5.3).</summary>
        public const string FinishRebaseFirst = "Finish or abort the rebase first";

        public const string FinishCherryPickFirst = "Finish or abort the cherry-pick first";

        /// <summary>The summaries the stage panel is filled with (5.3).</summary>
        public const string MergeSummary = "Merge branch 'feature/theirs'";

        // ---- The resolver (11.2, 11.4, 11.5). ----

        public const string ResolverFile = "notes.txt";

        public const string BothModified = "Both modified";

        public const string FileOneOfOne = "File 1 of 1";

        public const string ConflictOneOfTwo = "Conflict 1 of 2";

        public const string ConflictTwoOfTwo = "Conflict 2 of 2";

        public const string ConflictOneOfOne = "Conflict 1 of 1";

        public const string NoConflictsLeft = "No conflicts left";

        public const string TwoConflictsLeft = "2 conflicts left";

        public const string OneConflictLeft = "1 conflict left";

        public const string ResultTitle = "Result";

        public const string TakeOurs = "Take ours";

        public const string TakeTheirs = "Take theirs";

        public const string MergeToolButton = "Merge tool…";

        public const string MarkResolved = "Mark resolved";

        /// <summary>Merge tool…'s reason without <c>merge.tool</c> (10.4, 11.9).</summary>
        public const string SetMergeToolReason = "Set merge.tool in git's configuration to use a merge tool";

        public const string UsedInResult = "Used in the result";

        public const string NotUsed = "Not used";

        /// <summary>The panes' titles at each stop (11.4).</summary>
        public const string OursMain = "Ours: main";

        public const string TheirsMerged = "Theirs: feature/theirs";

        public const string OursNewBase = "Ours: main (the new base)";

        public const string TheirsFirstChange = "Theirs: 70e8c78 First change (your commit)";

        public const string TheirsSecondChange = "Theirs: 305f7f7 Second change (your commit)";

        public const string TheirsPick = "Theirs: 01fd976 Pick conflicting";

        // ---- Questions (7.2, phase 3's AbortConfirmation). ----

        public const string SkipTitle = "Skip 70e8c78?";

        public const string SkipQuestion = "\"First change\" is left out of the rebase, and changes made to resolve its conflicts are lost.";

        public const string BackupDetail = "A backup is saved first, so this can be undone.";

        public const string AbortRebaseTitle = "Abort the rebase?";

        public const string AbortRebaseQuestion = "Abort the rebase of feature/rebase? The branch goes back to where it was before it started.";

        public const string AbortRebaseReturnQuestion = "Abort the rebase of feature/rebase? The branch goes back to where it was before it started, and main is checked out again.";

        public const string AbortPickTitle = "Abort the cherry-pick?";

        public const string AbortPickListQuestion = "Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost. Commits this cherry-pick already made are dropped too.";

        // ---- Operations: title and status (10.1, 10.2). ----

        public const string ResolveOperation = "Resolve notes.txt";

        public const string ResolvedStatus = "Resolved notes.txt";

        public const string ContinueMergeOperation = "Continue the merge";

        public const string MergeCommittedStatus = "Merge committed";

        public const string ContinueRebaseOperation = "Continue the rebase";

        public const string RebaseStoppedStatus = "Rebase stopped: 1 file has conflicts";

        public const string RebasedStatus = "Rebased feature/rebase onto main";

        public const string ContinuePickOperation = "Continue the cherry-pick";

        public const string PickFinishedStatus = "Cherry-pick finished";

        public const string RebaseOperation = "Rebase feature/rebase onto main";

        public const string PickListOperation = "Cherry-pick 3 commits";

        public const string PickListStoppedStatus = "Cherry-picked 1 of 3 commits; stopped at 01fd976 with conflicts in 1 file";

        public const string SkipOperation = "Skip 70e8c78";

        public const string CheckOutAndRebaseOperation = "Check out feature/rebase and rebase it onto main";

        public const string AbortRebaseOperation = "Abort the rebase";

        public const string AbortedRebaseStatus = "Aborted the rebase";

        public const string AbortPickOperation = "Abort the cherry-pick";

        public const string AbortedPickStatus = "Aborted the cherry-pick";

        // ---- Menus (7.1). ----

        /// <summary>C12 step 1: the menu of rows 2 to 4 selected (Pick last, Pick conflicting, Pick clean).</summary>
        public static string[] ThreeCommitsMenu { get; } =
        [
            "3 commits",
            "Squash 3 commits… (disabled: \"Only commits of main can be squashed\")",
            "Cherry-pick 3 commits onto main",
            "Revert 3 commits",
            "—",
            "Copy SHAs",
        ];

        /// <summary>C13 step 9: the drop of feature/rebase onto main.</summary>
        public static string[] RebaseOntoMainDropMenu { get; } =
        [
            "feature/rebase → main",
            "Merge feature/rebase into main",
            "Merge feature/rebase into main (create a merge commit)",
            "Fast-forward main to feature/rebase (disabled: \"main has commits that feature/rebase does not have\")",
            "—",
            "Check out feature/rebase and rebase it onto main",
            "Check out feature/rebase and rebase it onto main interactively…",
        ];

        // ---- The details panel. ----

        /// <summary>The details' date of the app's commits (D72), as the panel shows it.</summary>
        public const string AppDate = "2026-01-02 09:00";

        // ---- The conflicted files, the panes and the results. ----

        /// <summary>FM: notes.txt at the merge's stop (C10), as the result shows it when the resolver opens (R0).</summary>
        public static string NotesMerge => ConflictsFiles.NotesMergeConflicted;

        /// <summary>R1: FM after Use ours on block 1 (and again after the undo): 16 lines, block 2 still at lines 10 to 14.</summary>
        public static string NotesMergeR1 { get; } = Text(
            "line 1", "line 2 ours", "line 3", "line 4", "line 5", "line 6 theirs", "line 7", "line 8", "line 9",
            "<<<<<<< HEAD", "line 10 ours", "=======", "line 10 theirs", ">>>>>>> feature/theirs",
            "line 11", "line 12");

        /// <summary>R2: R1 after Use theirs on block 2: 12 lines, no block.</summary>
        public static string NotesMergeR2 { get; } = Text(
            "line 1", "line 2 ours", "line 3", "line 4", "line 5", "line 6 theirs", "line 7", "line 8", "line 9",
            "line 10 theirs", "line 11", "line 12");

        /// <summary>R3: R1 after Use both from the ours pane on block 2: 13 lines; what C10 commits.</summary>
        public static string NotesMergeR3 { get; } = Text(
            "line 1", "line 2 ours", "line 3", "line 4", "line 5", "line 6 theirs", "line 7", "line 8", "line 9",
            "line 10 ours", "line 10 theirs", "line 11", "line 12");

        /// <summary>FR1: notes.txt at the rebase's first stop.</summary>
        public static string NotesRebaseFirst { get; } = ConflictsFiles.NotesRebaseFirstConflicted("70e8c78");

        /// <summary>FR1 resolved with its theirs side (C11 step 6): what is committed as the new First change.</summary>
        public static string NotesRebaseFirstTheirs => ConflictsFiles.NotesRebaseFirstResolved;

        /// <summary>FR2: notes.txt at C11's second stop.</summary>
        public static string NotesRebaseSecond { get; } = ConflictsFiles.NotesRebaseSecondConflicted("305f7f7");

        /// <summary>FR2 after Use both from the theirs pane (C11 step 10): 13 lines, line 10 second then line 10 ours.</summary>
        public static string NotesRebaseSecondBoth { get; } = Text(
            "line 1", "line 2 first", "line 3", "line 4", "line 5", "line 6", "line 7", "line 8", "line 9",
            "line 10 second", "line 10 ours", "line 11", "line 12");

        /// <summary>FS2: notes.txt at the second stop after First change was skipped (C13 step 3): main's line 2 stays.</summary>
        public static string NotesSkippedSecond { get; } = Text(
            "line 1", "line 2 ours", "line 3", "line 4", "line 5", "line 6", "line 7", "line 8", "line 9",
            "<<<<<<< HEAD", "line 10 ours", "=======", "line 10 second", ">>>>>>> 305f7f7 (Second change)",
            "line 11", "line 12");

        /// <summary>FP: notes.txt at the list's stop at Pick conflicting.</summary>
        public static string NotesPicks { get; } = ConflictsFiles.NotesPicksConflicted("01fd976");

        /// <summary>FP after Use ours (C12 step 5): main's notes.txt.</summary>
        public static string NotesPicksOurs => ConflictsFiles.NotesOurs;

        /// <summary>FP after Use ours and the typed edit (C12 step 6): line 2 is <c>line 2 ours and picked</c>.</summary>
        public static string NotesPicksTyped { get; } = Text(
            "line 1", "line 2 ours and picked", "line 3", "line 4", "line 5", "line 6", "line 7", "line 8", "line 9",
            "line 10 ours", "line 11", "line 12");

        /// <summary>The blobs of notes.txt's stages 1, 2 and 3 at each stop (<c>git ls-files -u</c>), abbreviated as the report writes them.</summary>
        public static IReadOnlyList<string> MergeStages { get; } = ["624b469", "439eb6b", "6216606"];

        public static IReadOnlyList<string> RebaseFirstStages { get; } = ["624b469", "439eb6b", "f1c22af"];

        public static IReadOnlyList<string> SkippedSecondStages { get; } = ["f1c22af", "439eb6b", "72a0708"];

        public static IReadOnlyList<string> PicksStages { get; } = ["624b469", "439eb6b", "9aa4b99"];

        /// <summary>Lines joined as git wrote the file: each ended by a line feed.</summary>
        public static string Text(params string[] lines)
        {
            ArgumentNullException.ThrowIfNull(lines);
            return string.Concat(lines.Select(line => line + "\n"));
        }

        /// <summary>A file's lines (each ended by a line feed), without the line ends.</summary>
        public static IReadOnlyList<string> LinesOf(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            return text.EndsWith('\n') ? text[..^1].Split('\n') : text.Split('\n');
        }

        // ---- Tints (7.3) and the panes' rows (11.3, 11.5). ----

        /// <summary>
        /// The background of a pane's row or a result's line, as the report's tables name it (design
        /// 7.3): none, ours, theirs, a marker line, a side used in the result, a block's bar.
        /// </summary>
        public enum LineTint
        {
            None,
            Ours,
            Theirs,
            Marker,
            Used,
            Bar,
        }

        /// <summary>What a block's side shows (11.5): its bar's buttons and its own tint, or <c>Used in the result</c>, or <c>Not used</c>.</summary>
        public enum SideShows
        {
            Buttons,
            Used,
            NotUsed,
        }

        /// <summary>
        /// One row of a pane as the report's tables write it: a numbered line (its text, its number
        /// in the pane's own file, its tint) or a block's bar (its block, from 0, and its text:
        /// null while it shows its buttons, else <c>Used in the result</c> or <c>Not used</c>).
        /// </summary>
        public sealed record PaneRow(string Text, LineTint Tint, bool IsBar, int? Number, int? Block, string? BarText)
        {
            public override string ToString() => IsBar
                ? $"bar of block {Block + 1}{(BarText is null ? " (buttons)" : ": " + BarText)}"
                : $"{Number} {Text}{(Tint == LineTint.None ? string.Empty : " (" + Tint + ")")}";
        }

        /// <summary>
        /// A pane's rows: the pane's twelve lines of notes.txt (<paramref name="lines"/>, line 1
        /// first), with a bar before each block, a block being one line here (every block of C10 to
        /// C13 has one line per side, so there are no filler rows); <paramref name="blocks"/> gives
        /// each block's line and what its side shows, in order.
        /// </summary>
        public static IReadOnlyList<PaneRow> Pane(bool ours, IReadOnlyList<string> lines, params (int Line, SideShows Shows)[] blocks)
        {
            ArgumentNullException.ThrowIfNull(lines);
            ArgumentNullException.ThrowIfNull(blocks);
            var rows = new List<PaneRow>();
            for (var number = 1; number <= lines.Count; number++)
            {
                var block = Array.FindIndex(blocks, entry => entry.Line == number);
                if (block < 0)
                {
                    rows.Add(new PaneRow(lines[number - 1], LineTint.None, false, number, null, null));
                    continue;
                }

                var shows = blocks[block].Shows;
                rows.Add(new PaneRow(string.Empty, LineTint.Bar, true, null, block, shows switch
                {
                    SideShows.Used => UsedInResult,
                    SideShows.NotUsed => NotUsed,
                    _ => null,
                }));
                rows.Add(new PaneRow(lines[number - 1], shows switch
                {
                    SideShows.Used => LineTint.Used,
                    SideShows.NotUsed => LineTint.None,
                    _ => ours ? LineTint.Ours : LineTint.Theirs,
                }, false, number, block, null));
            }

            return rows;
        }

        /// <summary>PM's ours pane (C10): main's side of both blocks, line 6 as git merged it.</summary>
        public static IReadOnlyList<string> MergeOursLines { get; } = LinesOf(Text(
            "line 1", "line 2 ours", "line 3", "line 4", "line 5", "line 6 theirs", "line 7", "line 8", "line 9", "line 10 ours", "line 11", "line 12"));

        /// <summary>PM's theirs pane (C10).</summary>
        public static IReadOnlyList<string> MergeTheirsLines { get; } = LinesOf(Text(
            "line 1", "line 2 theirs", "line 3", "line 4", "line 5", "line 6 theirs", "line 7", "line 8", "line 9", "line 10 theirs", "line 11", "line 12"));

        /// <summary>PM: the merge's panes with block 1 (line 2) and block 2 (line 10) showing <paramref name="block1"/> and <paramref name="block2"/>, per side.</summary>
        public static (IReadOnlyList<PaneRow> Ours, IReadOnlyList<PaneRow> Theirs) MergePanes((SideShows Ours, SideShows Theirs) block1, (SideShows Ours, SideShows Theirs) block2) =>
            (Pane(true, MergeOursLines, (2, block1.Ours), (10, block2.Ours)), Pane(false, MergeTheirsLines, (2, block1.Theirs), (10, block2.Theirs)));

        /// <summary>PR1's ours pane (the new base, main's notes.txt) and PP's ours pane: main's notes.txt.</summary>
        public static IReadOnlyList<string> MainLines { get; } = LinesOf(ConflictsFiles.NotesOurs);

        /// <summary>PR1's theirs pane: First change's line 2 on main's line 10.</summary>
        public static IReadOnlyList<string> RebaseFirstTheirsLines { get; } = LinesOf(ConflictsFiles.NotesRebaseFirstResolved);

        /// <summary>PR2's ours pane: the new First change's notes.txt.</summary>
        public static IReadOnlyList<string> RebaseSecondOursLines { get; } = LinesOf(ConflictsFiles.NotesRebaseFirstResolved);

        /// <summary>PR2's theirs pane: Second change's line 10 on the new First change.</summary>
        public static IReadOnlyList<string> RebaseSecondTheirsLines { get; } = LinesOf(ConflictsFiles.NotesSecond);

        /// <summary>PP's theirs pane: Pick conflicting's line 2 on main's line 10.</summary>
        public static IReadOnlyList<string> PicksTheirsLines { get; } = LinesOf(Text(
            "line 1", "line 2 picked", "line 3", "line 4", "line 5", "line 6", "line 7", "line 8", "line 9", "line 10 ours", "line 11", "line 12"));

        /// <summary>The tints of a result's lines (line 1 first), none but those named.</summary>
        public static IReadOnlyList<LineTint> ResultTints(int lineCount, params (int Line, LineTint Tint)[] tinted)
        {
            ArgumentNullException.ThrowIfNull(tinted);
            var tints = Enumerable.Repeat(LineTint.None, lineCount).ToArray();
            foreach (var (line, tint) in tinted)
            {
                tints[line - 1] = tint;
            }

            return tints;
        }

        /// <summary>The tints of an unresolved one-line-per-side block whose <c>&lt;</c> line is <paramref name="start"/>: marker, ours, marker, theirs, marker.</summary>
        public static (int Line, LineTint Tint)[] BlockTints(int start) =>
        [
            (start, LineTint.Marker),
            (start + 1, LineTint.Ours),
            (start + 2, LineTint.Marker),
            (start + 3, LineTint.Theirs),
            (start + 4, LineTint.Marker),
        ];

        /// <summary>
        /// The resolver as a step of C10 to C13 expects it: the toolbar's position text, the result's
        /// count, its text, the caret (line and column from 1), whether it is modified, its lines'
        /// tints, and both panes' rows. The header (<c>U</c>, notes.txt, no folder, Both modified,
        /// File 1 of 1), the titles and the buttons are the same at every step of a stop.
        /// </summary>
        public sealed record ResolverState(
            string ConflictPosition,
            string ResultCount,
            string Result,
            (int Line, int Column)? Caret,
            bool IsModified,
            IReadOnlyList<LineTint> Tints,
            IReadOnlyList<PaneRow> OursRows,
            IReadOnlyList<PaneRow> TheirsRows);

        /// <summary>The titles of a stop's panes (11.4).</summary>
        public sealed record ResolverTitles(string Ours, string Theirs);

        public static ResolverTitles MergeTitles { get; } = new(OursMain, TheirsMerged);

        public static ResolverTitles RebaseFirstTitles { get; } = new(OursNewBase, TheirsFirstChange);

        public static ResolverTitles RebaseSecondTitles { get; } = new(OursNewBase, TheirsSecondChange);

        public static ResolverTitles PickTitles { get; } = new(OursMain, TheirsPick);

        // C10's steps.

        /// <summary>C10 step 5: FM as read, both blocks open, the caret at block 1's <c>&lt;</c> line.</summary>
        public static ResolverState C10Opened { get; } = MergeState(
            ConflictOneOfTwo, TwoConflictsLeft, NotesMerge, (2, 1), false,
            ResultTints(20, [.. BlockTints(2), .. BlockTints(14)]),
            (SideShows.Buttons, SideShows.Buttons),
            (SideShows.Buttons, SideShows.Buttons));

        /// <summary>C10 step 6: Use ours on block 1 (R1).</summary>
        public static ResolverState C10Block1Ours { get; } = MergeState(
            ConflictOneOfTwo, OneConflictLeft, NotesMergeR1, (2, 1), true,
            ResultTints(16, BlockTints(10)),
            (SideShows.Used, SideShows.NotUsed),
            (SideShows.Buttons, SideShows.Buttons));

        /// <summary>C10 step 7: the next-conflict button: block 2 reached, the caret at its <c>&lt;</c> line.</summary>
        public static ResolverState C10Conflict2 { get; } = C10Block1Ours with { ConflictPosition = ConflictTwoOfTwo, Caret = (10, 1) };

        /// <summary>C10 step 8: Use theirs on block 2 (R2).</summary>
        public static ResolverState C10Block2Theirs { get; } = MergeState(
            NoConflictsLeft, NoConflictsLeft, NotesMergeR2, (10, 1), true,
            ResultTints(12),
            (SideShows.Used, SideShows.NotUsed),
            (SideShows.NotUsed, SideShows.Used));

        /// <summary>
        /// C10 step 9: the platform's undo: R1 again, block 2 the block last reached. The report
        /// states no caret for this step.
        /// </summary>
        public static ResolverState C10Undone { get; } = C10Conflict2 with { Caret = null };

        /// <summary>C10 step 10: Use both from the ours pane on block 2 (R3).</summary>
        public static ResolverState C10Block2Both { get; } = MergeState(
            NoConflictsLeft, NoConflictsLeft, NotesMergeR3, (10, 1), true,
            ResultTints(13),
            (SideShows.Used, SideShows.NotUsed),
            (SideShows.Used, SideShows.Used));

        // C11's steps.

        /// <summary>C11 step 5: FR1 as read.</summary>
        public static ResolverState C11FirstOpened { get; } = new(
            ConflictOneOfOne, OneConflictLeft, NotesRebaseFirst, (2, 1), false,
            ResultTints(16, BlockTints(2)),
            Pane(true, MainLines, (2, SideShows.Buttons)),
            Pane(false, RebaseFirstTheirsLines, (2, SideShows.Buttons)));

        /// <summary>C11 step 6: Use theirs.</summary>
        public static ResolverState C11FirstTheirs { get; } = new(
            NoConflictsLeft, NoConflictsLeft, NotesRebaseFirstTheirs, (2, 1), true,
            ResultTints(12),
            Pane(true, MainLines, (2, SideShows.NotUsed)),
            Pane(false, RebaseFirstTheirsLines, (2, SideShows.Used)));

        /// <summary>C11 step 9: FR2 as read, the caret at the block's <c>&lt;</c> line (line 10).</summary>
        public static ResolverState C11SecondOpened { get; } = new(
            ConflictOneOfOne, OneConflictLeft, NotesRebaseSecond, (10, 1), false,
            ResultTints(16, BlockTints(10)),
            Pane(true, RebaseSecondOursLines, (10, SideShows.Buttons)),
            Pane(false, RebaseSecondTheirsLines, (10, SideShows.Buttons)));

        /// <summary>C11 step 10: Use both from the theirs pane.</summary>
        public static ResolverState C11SecondBoth { get; } = new(
            NoConflictsLeft, NoConflictsLeft, NotesRebaseSecondBoth, (10, 1), true,
            ResultTints(13),
            Pane(true, RebaseSecondOursLines, (10, SideShows.Used)),
            Pane(false, RebaseSecondTheirsLines, (10, SideShows.Used)));

        // C12's steps.

        /// <summary>C12 step 4: FP as read.</summary>
        public static ResolverState C12Opened { get; } = new(
            ConflictOneOfOne, OneConflictLeft, NotesPicks, (2, 1), false,
            ResultTints(16, BlockTints(2)),
            Pane(true, MainLines, (2, SideShows.Buttons)),
            Pane(false, PicksTheirsLines, (2, SideShows.Buttons)));

        /// <summary>C12 step 5: Use ours.</summary>
        public static ResolverState C12Ours { get; } = new(
            NoConflictsLeft, NoConflictsLeft, NotesPicksOurs, (2, 1), true,
            ResultTints(12),
            Pane(true, MainLines, (2, SideShows.Used)),
            Pane(false, PicksTheirsLines, (2, SideShows.NotUsed)));

        /// <summary>C12 step 6: End, then " and picked" typed on line 2. The report states no caret for this step.</summary>
        public static ResolverState C12Typed { get; } = C12Ours with { Result = NotesPicksTyped, Caret = null };

        private static ResolverState MergeState(
            string position,
            string count,
            string result,
            (int Line, int Column)? caret,
            bool modified,
            IReadOnlyList<LineTint> tints,
            (SideShows Ours, SideShows Theirs) block1,
            (SideShows Ours, SideShows Theirs) block2)
        {
            var (ours, theirs) = MergePanes(block1, block2);
            return new ResolverState(position, count, result, caret, modified, tints, ours, theirs);
        }

        // ---- Git's states. ----

        /// <summary>C10 after step 1: <c>.git/MERGE_MSG</c> of the merge started in a terminal.</summary>
        public const string MergeMsg = "Merge branch 'feature/theirs'\n\n# Conflicts:\n#\tnotes.txt\n";

        /// <summary><c>MERGE_MSG</c> at the rebase's first stop.</summary>
        public const string RebaseFirstMergeMsg = "First change\n\n# Conflicts:\n#\tnotes.txt\n";

        /// <summary><c>MERGE_MSG</c> at the list's stop.</summary>
        public const string PicksMergeMsg = "Pick conflicting\n\n# Conflicts:\n#\tnotes.txt\n";

        /// <summary>
        /// The files of <c>.git/rebase-merge/</c> at the rebase's first stop (C11 after step 2, C13
        /// after steps 1 and 10), each as git wrote it, line ends included; <c>author-script</c>
        /// is asserted by its date line only.
        /// </summary>
        public static IReadOnlyDictionary<string, string> RebaseFirstStopFiles { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["head-name"] = "refs/heads/feature/rebase\n",
            ["onto"] = StopSha.OursEdits + "\n",
            ["orig-head"] = StopSha.SecondChange + "\n",
            ["msgnum"] = "1\n",
            ["end"] = "2\n",
            ["stopped-sha"] = StopSha.FirstChange + "\n",
            ["done"] = $"pick {StopSha.FirstChange} First change\n",
            ["git-rebase-todo"] = $"pick {StopSha.SecondChange} Second change\n",
            ["message"] = "First change\n\n",
        };

        /// <summary>The author date line of <c>rebase-merge/author-script</c> at the first stop (12:03).</summary>
        public const string FirstAuthorDateLine = "GIT_AUTHOR_DATE='@1767268980 +0000'";

        /// <summary>The <c>rebase-merge/</c> files at C11's second stop (after step 8).</summary>
        public static IReadOnlyDictionary<string, string> RebaseSecondStopFiles { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["msgnum"] = "2\n",
            ["end"] = "2\n",
            ["stopped-sha"] = StopSha.SecondChange + "\n",
            ["done"] = $"pick {StopSha.FirstChange} First change\npick {StopSha.SecondChange} Second change\n",
            ["git-rebase-todo"] = string.Empty,
            ["rewritten-list"] = $"{StopSha.FirstChange} {StopSha.NewFirst}\n",
            ["message"] = "Second change\n\n",
        };

        /// <summary>The <c>rebase-merge/</c> files at the second stop after the skip (C13 after step 3).</summary>
        public static IReadOnlyDictionary<string, string> SkippedSecondStopFiles { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["msgnum"] = "2\n",
            ["end"] = "2\n",
            ["stopped-sha"] = StopSha.SecondChange + "\n",
            ["done"] = $"pick {StopSha.FirstChange} First change\npick {StopSha.SecondChange} Second change\n",
            ["git-rebase-todo"] = string.Empty,
            ["rewritten-list"] = $"{StopSha.FirstChange} {StopSha.OursEdits}\n",
        };

        /// <summary><c>.git/sequencer/todo</c> at the list's stop (C12 after step 2, C13 after step 6).</summary>
        public const string PicksTodo = "pick 01fd976 Pick conflicting\npick 5c4c257 Pick last\n";

        /// <summary>The message of C12's Pick conflicting copy, as <c>git cat-file commit</c> ends.</summary>
        public const string PickedConflictingMessage = "Pick conflicting, resolved\n\n#7 Line 2 keeps main's text.\n";

        /// <summary><c>rebase-merge/visualcommit-return</c> after "Check out feature/rebase and rebase it onto main" (14.3).</summary>
        public const string ReturnFile = "refs/heads/main\n";

        // ---- The app's calls. ----

        public const string AddNotes = "git add -- notes.txt";

        /// <summary>The call that writes the backup refs (phase 3's backups, D83: one <c>update-ref</c> transaction), which the report's "saved … then ran …" orders before git's own call.</summary>
        public const string BackupWriteCall = "git update-ref --stdin";

        public const string DiffCachedCheck = "git diff --cached --check -- notes.txt";

        public const string MergeCommitCall = "git commit --file=- --cleanup=whitespace";

        public const string RebaseCommitCall = "git commit --no-verify --cleanup=whitespace --file=-";

        public const string RebaseContinueCall = "git rebase --continue";

        public const string RebaseOntoMainCall = "git rebase " + StopSha.OursEdits;

        public const string PickListCall = "git cherry-pick " + StopSha.PickClean + " " + StopSha.PickConflicting + " " + StopSha.PickLast;

        public const string PickContinueCall = "git cherry-pick --continue";

        public const string RebaseSkipCall = "git rebase --skip";

        public const string RebaseAbortCall = "git rebase --abort";

        public const string PickAbortCall = "git cherry-pick --abort";

        public const string AutoStashCall = "git config --type=bool --get rebase.autoStash";

        public const string SwitchToRebaseCall = "git switch --no-guess feature/rebase";

        public const string SwitchToMainCall = "git switch --no-guess main";

        /// <summary>Git 2.36's lines of the conflicts (asserted on 2.36 only).</summary>
        public const string AutoMergingNotes = "Auto-merging notes.txt";

        public const string ConflictInNotes = "CONFLICT (content): Merge conflict in notes.txt";

        public const string SuccessfullyRebased = "Successfully rebased and updated refs/heads/feature/rebase.";
    }
}
