// What checks C14 to C17 of phase 4's walk-through compare against (docs/test-reports/phase-4.md,
// "Conventions of checks C14 to C17 and D18", "Texts of checks C14 to C17", "The scenarios, graphs
// and files of checks C14 to C17", "The tints" and the rows C14 to C17 of "Checks"): copied from
// the report, not read from the app or from the scenarios, so that the two are checked against
// each other. Data and pure helpers only: the real-window pass compiles this file too (it links
// Phase4Expectations*.cs), so it uses nothing of the app and no headless type. The nested class is
// PartC2, apart from checks C10 to C13's PartC, so that the two writers never edit one file.
using System.Globalization;

namespace VisualCommit.VisualTests.Phase4;

public static partial class Phase4Expectations
{
    /// <summary>The expected results of checks C14 to C17 (part C's second half).</summary>
    public static class PartC2
    {
        // ---- Tints and pane rows (design 7.3, the report's "The tints") ----------------------------

        /// <summary>
        /// The background the report names for a pane row or a result line, by its token: none,
        /// <c>VcConflictOursBrush</c>, <c>VcConflictTheirsBrush</c>, <c>VcConflictBaseBrush</c>,
        /// <c>VcConflictMarkerBrush</c>, <c>VcConflictUsedBrush</c>, a filler, or a block bar on
        /// <c>VcDiffHunkBrush</c>. The names are the app's <c>ConflictTint</c>'s, which the checks map
        /// them to by name (this file uses no type of the app).
        /// </summary>
        public enum Tint
        {
            None,
            Ours,
            Theirs,
            Base,
            Marker,
            Used,
            Filler,
            Bar,
        }

        /// <summary>
        /// One row of a pane as the report lists it: a text row, the pane's line <paramref name="Number"/>
        /// with its text and tint, or a block bar (no number, no text, <see cref="Tint.Bar"/>).
        /// </summary>
        public sealed record PaneRow(int? Number, string Text, Tint Tint)
        {
            public bool IsBar => Number is null;

            public override string ToString() => IsBar ? "bar" : string.Create(CultureInfo.InvariantCulture, $"{Number} `{Text}` {Tint}");
        }

        /// <summary>A block bar row.</summary>
        public static PaneRow Bar { get; } = new(null, string.Empty, Tint.Bar);

        /// <summary>
        /// The ours pane of PM (the merge of feature/theirs): block 1's <c>line 2 ours</c> tinted
        /// <paramref name="block1"/> and block 2's <c>line 10 ours</c> <paramref name="block2"/>
        /// (<see cref="Tint.Ours"/> while the block is unresolved, <see cref="Tint.Used"/> once ours
        /// is used, <see cref="Tint.None"/> when it is not).
        /// </summary>
        public static IReadOnlyList<PaneRow> PmOurs(Tint block1 = Tint.Ours, Tint block2 = Tint.Ours) => PmPane("ours", block1, block2);

        /// <summary>The theirs pane of PM, likewise with <c>line 2 theirs</c> and <c>line 10 theirs</c>.</summary>
        public static IReadOnlyList<PaneRow> PmTheirs(Tint block1 = Tint.Theirs, Tint block2 = Tint.Theirs) => PmPane("theirs", block1, block2);

        private static List<PaneRow> PmPane(string side, Tint block1, Tint block2) =>
        [
            new(1, "line 1", Tint.None),
            Bar,
            new(2, $"line 2 {side}", block1),
            new(3, "line 3", Tint.None),
            new(4, "line 4", Tint.None),
            new(5, "line 5", Tint.None),
            new(6, "line 6 theirs", Tint.None),
            new(7, "line 7", Tint.None),
            new(8, "line 8", Tint.None),
            new(9, "line 9", Tint.None),
            Bar,
            new(10, $"line 10 {side}", block2),
            new(11, "line 11", Tint.None),
            new(12, "line 12", Tint.None),
        ];

        /// <summary>The ours pane of new.txt in the kinds merge (C15 step 4): <c>Ours: main</c>.</summary>
        public static IReadOnlyList<PaneRow> NewOursPane { get; } = [new(1, "new file", Tint.None), Bar, new(2, "added on main", Tint.Ours)];

        /// <summary>The theirs pane of new.txt in the kinds merge (C15 step 4): <c>Theirs: feature/kinds</c>.</summary>
        public static IReadOnlyList<PaneRow> NewTheirsPane { get; } = [new(1, "new file", Tint.None), Bar, new(2, "added on feature/kinds", Tint.Theirs)];

        /// <summary>
        /// The tints of a result of <paramref name="lines"/> lines, line 1 first: <see cref="Tint.None"/>
        /// but for the lines named.
        /// </summary>
        public static IReadOnlyList<Tint> Tints(int lines, params (int Line, Tint Tint)[] tinted)
        {
            var tints = Enumerable.Repeat(Tint.None, lines).ToArray();
            foreach (var (line, tint) in tinted)
            {
                tints[line - 1] = tint;
            }

            return tints;
        }

        /// <summary>The lines given joined by LF, each ended by LF, as git writes the file and the result holds it.</summary>
        public static string Text(params string[] lines) => string.Concat(lines.Select(line => line + "\n"));

        // ---- The files (the report's "The files as git leaves them" and the rows) ---------------

        /// <summary>FM: notes.txt after <c>git merge feature/theirs</c>, 20 lines, two blocks (C14, C16).</summary>
        public static string Fm { get; } = Text(
            "line 1",
            "<<<<<<< HEAD",
            "line 2 ours",
            "=======",
            "line 2 theirs",
            ">>>>>>> feature/theirs",
            "line 3",
            "line 4",
            "line 5",
            "line 6 theirs",
            "line 7",
            "line 8",
            "line 9",
            "<<<<<<< HEAD",
            "line 10 ours",
            "=======",
            "line 10 theirs",
            ">>>>>>> feature/theirs",
            "line 11",
            "line 12");

        /// <summary>FM's tints as the result shows it when the resolver opens (R0).</summary>
        public static IReadOnlyList<Tint> FmTints { get; } = Tints(
            20,
            (2, Tint.Marker),
            (3, Tint.Ours),
            (4, Tint.Marker),
            (5, Tint.Theirs),
            (6, Tint.Marker),
            (14, Tint.Marker),
            (15, Tint.Ours),
            (16, Tint.Marker),
            (17, Tint.Theirs),
            (18, Tint.Marker));

        /// <summary>The result after C14 step 2: block 1 typed over (16 lines, block 2 left).</summary>
        public static string Typed { get; } = Text(
            "line 1",
            "line 2 edited by hand",
            "line 3",
            "line 4",
            "line 5",
            "line 6 theirs",
            "line 7",
            "line 8",
            "line 9",
            "<<<<<<< HEAD",
            "line 10 ours",
            "=======",
            "line 10 theirs",
            ">>>>>>> feature/theirs",
            "line 11",
            "line 12");

        /// <summary>The tints of <see cref="Typed"/>: block 2's lines 10 to 14.</summary>
        public static IReadOnlyList<Tint> TypedTints { get; } = Tints(16, (10, Tint.Marker), (11, Tint.Ours), (12, Tint.Marker), (13, Tint.Theirs), (14, Tint.Marker));

        /// <summary>The result after C14 step 4: <see cref="Typed"/> with block 2's theirs side (12 lines).</summary>
        public static string TypedThenTheirs { get; } = Text(
            "line 1",
            "line 2 edited by hand",
            "line 3",
            "line 4",
            "line 5",
            "line 6 theirs",
            "line 7",
            "line 8",
            "line 9",
            "line 10 theirs",
            "line 11",
            "line 12");

        /// <summary>feature/theirs' notes.txt (stage 3, blob 6216606): what Take theirs gives (C14 step 7).</summary>
        public static string TheirsNotes { get; } = Text(
            "line 1",
            "line 2 theirs",
            "line 3",
            "line 4",
            "line 5",
            "line 6 theirs",
            "line 7",
            "line 8",
            "line 9",
            "line 10 theirs",
            "line 11",
            "line 12");

        /// <summary>The result after C16 step 2's <c>Use ours</c> on block 1 (16 lines).</summary>
        public static string OursOnBlock1 { get; } = Text(
            "line 1",
            "line 2 ours",
            "line 3",
            "line 4",
            "line 5",
            "line 6 theirs",
            "line 7",
            "line 8",
            "line 9",
            "<<<<<<< HEAD",
            "line 10 ours",
            "=======",
            "line 10 theirs",
            ">>>>>>> feature/theirs",
            "line 11",
            "line 12");

        /// <summary>new.txt as the kinds merge leaves it (6 lines, one block).</summary>
        public static string NewConflicted { get; } = Text(
            "new file",
            "<<<<<<< HEAD",
            "added on main",
            "=======",
            "added on feature/kinds",
            ">>>>>>> feature/kinds");

        /// <summary>The tints of <see cref="NewConflicted"/>.</summary>
        public static IReadOnlyList<Tint> NewConflictedTints { get; } = Tints(6, (2, Tint.Marker), (3, Tint.Ours), (4, Tint.Marker), (5, Tint.Theirs), (6, Tint.Marker));

        /// <summary>new.txt after <c>Use both</c> (ours first): what Mark resolved writes, blob b14fd8d (C15 step 5).</summary>
        public static string NewResolved { get; } = Text("new file", "added on main", "added on feature/kinds");

        /// <summary>other.txt as main has it and the kinds merge leaves it (C15 step 3).</summary>
        public static string OtherOurs { get; } = Text("other file", "changed on main");

        /// <summary>What the fake merge tool writes to notes.txt, blob 17dfe0d (C16 step 4).</summary>
        public static string FakeToolResult { get; } = Text("Resolved by the fake merge tool.");

        /// <summary>notes.txt as C17's terminal rebase stops at Pick conflicting.</summary>
        public static string PicksStopNotes { get; } = Text(
            "line 1",
            "<<<<<<< HEAD",
            "line 2 ours",
            "=======",
            "line 2 picked",
            ">>>>>>> 01fd976 (Pick conflicting)",
            "line 3",
            "line 4",
            "line 5",
            "line 6",
            "line 7",
            "line 8",
            "line 9",
            "line 10 ours",
            "line 11",
            "line 12");

        /// <summary>notes.txt as C17 step 5 writes it outside the app (12 lines, LF).</summary>
        public static string PicksResolvedNotes { get; } = Text(
            "line 1",
            "line 2 picked",
            "line 3",
            "line 4",
            "line 5",
            "line 6",
            "line 7",
            "line 8",
            "line 9",
            "line 10 ours",
            "line 11",
            "line 12");

        // ---- Git's objects and states -----------------------------------------------------------

        public const string MainSha = "da31c97";
        public const string TheirsSha = "5c77ffb";
        public const string KindsSha = "3a9de3e";
        public const string FmBlob = "cdd7b39";
        public const string FmBlobFull = "cdd7b3996a63184d1ed12272da6ed2320e72c59a";
        public const string TheirsNotesBlob = "6216606";
        public const string MainNotesBlob = "439eb6b";
        public const string BaseNotesBlob = "624b469";
        public const string ImageBaseBlob = "e73f509";
        public const string ImageOursBlob = "c22a1df";
        public const string ImageKindsBlob = "23eb33f";
        public const string NewOursBlob = "0608103";
        public const string NewKindsBlob = "8240019";
        public const string NewResolvedBlob = "b14fd8d";
        public const string OtherBaseBlob = "595dfd6";
        public const string OtherOursBlob = "ec92a8c";
        public const string FakeToolBlob = "17dfe0d";

        /// <summary>The terminal rebase's replay of Pick clean (K1, K2), the same commit as the CherryPicks stop's.</summary>
        public const string PickCleanReplayed = "d71cdc4e9eb7fa80dc917e6f859204a0286e78ac";
        public const string PickCleanSha = "01d2046";
        public const string PickConflictingSha = "01fd976";
        public const string PickLastSha = "5c4c257";

        /// <summary>K2's Pick conflicting, committed by the app's Continue (C17 step 6).</summary>
        public const string PickConflictingRebased = "e4366268fbb7c6e83e1f4f9d1cd77dd9b5299da0";

        /// <summary>K2's Pick last, replayed by <c>git rebase --continue</c> (C17 step 6).</summary>
        public const string PickLastRebased = "f048313649af31430afc5dce1fa3e2baefdcd547";

        /// <summary>Pick conflicting's author date, as <c>rebase-merge/author-script</c> holds it: 2026-01-01 12:06 UTC.</summary>
        public const string PickConflictingAuthorScriptDate = "GIT_AUTHOR_DATE='@1767269160 +0000'";

        /// <summary>The merge stop of C14 and C16: <c>git status --porcelain=v1</c>.</summary>
        public static IReadOnlyList<string> MergeStopStatus { get; } = ["UU notes.txt"];

        /// <summary>The kinds merge's stop (C15): <c>git status --porcelain=v1</c>, in git's order.</summary>
        public static IReadOnlyList<string> KindsStopStatus { get; } = ["UU image.png", "AA new.txt", "UD other.txt"];

        /// <summary><c>git diff --cached --check</c>'s lines with FM staged (C14 step 11; Git 2.36's words).</summary>
        public static IReadOnlyList<string> LeftoverMarkerLines { get; } =
            [.. new[] { 2, 4, 6, 14, 16, 18 }.Select(line => string.Create(CultureInfo.InvariantCulture, $"notes.txt:{line}: leftover conflict marker"))];

        // ---- Graphs (GC-M, K1, K2) ---------------------------------------------------------------

        /// <summary>The working-changes row's text with conflicts (singular for one, D113).</summary>
        public static string WorkingConflicts(int conflicts) =>
            conflicts == 1 ? "Working changes · 1 conflict" : string.Create(CultureInfo.InvariantCulture, $"Working changes · {conflicts} conflicts");

        /// <summary>The working-changes row's text once nothing conflicts.</summary>
        public const string Working = "Working changes";

        /// <summary>The working-changes row of GC-M and K1: ring in lane 0, colour 0, with <paramref name="text"/>.</summary>
        public static WorkingChangesRow WorkingRow(string text) => new(text, 0, 0);

        /// <summary>
        /// GC-M's commit rows (display rows 1 to 10; display row 0 is the working-changes row): the
        /// "conflicts" scenario as built, a merge stopped. 5 lanes; no Date column at 1100×700.
        /// </summary>
        public static IReadOnlyList<CommitRow> GcM { get; } =
        [
            Row("Ours edits", 0, 0, Current("main")) with { ShortSha = "da31c97" },
            Row("Kinds edits", 1, 1, "feature/kinds") with { ShortSha = "3a9de3e" },
            Row("Pick last", 2, 2, "feature/picks") with { ShortSha = "5c4c257" },
            Row("Pick conflicting", 2, 2) with { ShortSha = "01fd976" },
            Row("Pick clean", 2, 2) with { ShortSha = "01d2046" },
            Row("Second change", 3, 3, "feature/rebase") with { ShortSha = "305f7f7" },
            Row("First change", 3, 3) with { ShortSha = "70e8c78" },
            Row("Theirs edits", 4, 4, "feature/theirs") with { ShortSha = "5c77ffb" },
            Row("Base", 0, 0) with { ShortSha = "195768b", Ends = [1, 2, 3, 4] },
            Row("Initial commit", 0, 0) with { ShortSha = "6bd579f" },
        ];

        /// <summary>GC0: "conflicts" as built, nothing in progress (C17 step 1); the same rows as GC-M without the working-changes row.</summary>
        public static IReadOnlyList<CommitRow> Gc0 => GcM;

        /// <summary>The lanes of GC-M, GC0 and K1.</summary>
        public const int ConflictsLanes = 5;

        /// <summary>K1: C17's terminal rebase stopped at Pick conflicting, HEAD detached at d71cdc4.</summary>
        public static IReadOnlyList<CommitRow> K1 { get; } =
        [
            Row("Pick clean", 0, 0, "HEAD") with { ShortSha = "d71cdc4" },
            Row("Ours edits", 0, 0, "main") with { ShortSha = "da31c97" },
            Row("Kinds edits", 1, 1, "feature/kinds") with { ShortSha = "3a9de3e" },
            Row("Pick last", 2, 2, "feature/picks") with { ShortSha = "5c4c257" },
            Row("Pick conflicting", 2, 2) with { ShortSha = "01fd976" },
            Row("Pick clean", 2, 2) with { ShortSha = "01d2046" },
            Row("Second change", 3, 3, "feature/rebase") with { ShortSha = "305f7f7" },
            Row("First change", 3, 3) with { ShortSha = "70e8c78" },
            Row("Theirs edits", 4, 4, "feature/theirs") with { ShortSha = "5c77ffb" },
            Row("Base", 0, 0) with { ShortSha = "195768b", Ends = [1, 2, 3, 4] },
            Row("Initial commit", 0, 0) with { ShortSha = "6bd579f" },
        ];

        /// <summary>K2: C17 after the app's Continue, feature/picks rebased onto main. 4 lanes.</summary>
        public static IReadOnlyList<CommitRow> K2 { get; } =
        [
            Row("Pick last", 0, 0, Current("feature/picks")) with { ShortSha = "f048313" },
            Row("Pick conflicting", 0, 0) with { ShortSha = "e436626" },
            Row("Pick clean", 0, 0) with { ShortSha = "d71cdc4" },
            Row("Ours edits", 0, 0, "main") with { ShortSha = "da31c97" },
            Row("Kinds edits", 1, 1, "feature/kinds") with { ShortSha = "3a9de3e" },
            Row("Second change", 2, 2, "feature/rebase") with { ShortSha = "305f7f7" },
            Row("First change", 2, 2) with { ShortSha = "70e8c78" },
            Row("Theirs edits", 3, 3, "feature/theirs") with { ShortSha = "5c77ffb" },
            Row("Base", 0, 0) with { ShortSha = "195768b", Ends = [1, 2, 3] },
            Row("Initial commit", 0, 0) with { ShortSha = "6bd579f" },
        ];

        /// <summary>The lanes of K2.</summary>
        public const int K2Lanes = 4;

        // ---- The banner (5.2, 10.3, 10.4) --------------------------------------------------------

        public const string MergeTheirsBanner = "Merging: \"Merge branch 'feature/theirs'\". 1 file has conflicts.";
        public const string MergeTheirsResolvedBanner = "Merging: \"Merge branch 'feature/theirs'\". Conflicts resolved: commit to finish the merge.";

        /// <summary>The kinds merge's banner with <paramref name="files"/> conflicted files.</summary>
        public static string MergeKindsBanner(int files) => "Merging: \"Merge branch 'feature/kinds'\". " + FilesHaveConflicts(files);

        public const string MergeKindsResolvedBanner = "Merging: \"Merge branch 'feature/kinds'\". Conflicts resolved: commit to finish the merge.";

        public const string PicksRebaseBanner = "Rebasing feature/picks onto main: stopped at 01fd976 \"Pick conflicting\" (2 of 3). 1 file has conflicts.";
        public const string PicksRebaseResolvedBanner = "Rebasing feature/picks onto main: stopped at 01fd976 \"Pick conflicting\" (2 of 3). Conflicts resolved: continue to commit it and go on.";

        public const string AbortMerge = "Abort merge";
        public const string AbortRebase = "Abort rebase";

        /// <summary>Continue's disabled reason while <paramref name="files"/> files conflict (10.4).</summary>
        public static string ResolveFirst(int files) =>
            string.Create(CultureInfo.InvariantCulture, $"Resolve the conflicts first ({files} {(files == 1 ? "file" : "files")})");

        /// <summary>The banner icon's tooltip for the merge (10.3).</summary>
        public const string MergeToolTip = "Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.";

        /// <summary>The banner icon's tooltip for C17's rebase (10.3).</summary>
        public const string PicksRebaseToolTip = "Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts feature/picks back where it was before the rebase.";

        private static string FilesHaveConflicts(int files) =>
            files == 1 ? "1 file has conflicts." : string.Create(CultureInfo.InvariantCulture, $"{files} files have conflicts.");

        // ---- The resolver (11.2, 11.4, 11.5, 11.8, 10.4) -----------------------------------------

        public const string Conflicted = "U";
        public const string BothModified = "Both modified";
        public const string BothAdded = "Both added";
        public const string DeletedByThem = "Deleted by them";

        /// <summary><c>File {i} of {n}</c>.</summary>
        public static string FilePosition(int at, int of) => string.Create(CultureInfo.InvariantCulture, $"File {at} of {of}");

        /// <summary><c>Conflict {k} of {m}</c>.</summary>
        public static string ConflictPosition(int at, int of) => string.Create(CultureInfo.InvariantCulture, $"Conflict {at} of {of}");

        public const string NoConflictsLeft = "No conflicts left";

        /// <summary>The result's count: <c>2 conflicts left</c>, <c>1 conflict left</c>, <c>No conflicts left</c>.</summary>
        public static string ConflictsLeft(int blocks) => blocks switch
        {
            0 => NoConflictsLeft,
            1 => "1 conflict left",
            _ => string.Create(CultureInfo.InvariantCulture, $"{blocks} conflicts left"),
        };

        public const string TakeOurs = "Take ours";
        public const string TakeTheirs = "Take theirs";
        public const string MergeToolButton = "Merge tool…";
        public const string MarkResolved = "Mark resolved";

        /// <summary><c>Merge tool…</c>'s disabled reason without <c>merge.tool</c> (10.4, 11.9).</summary>
        public const string NoMergeTool = "Set merge.tool in git's configuration to use a merge tool";

        public const string OursMain = "Ours: main";
        public const string TheirsTheirs = "Theirs: feature/theirs";
        public const string TheirsKinds = "Theirs: feature/kinds";
        public const string ResultTitle = "Result";

        public const string Used = "Used in the result";
        public const string NotUsed = "Not used";

        /// <summary>The "deleted by them" card's text (11.8).</summary>
        public const string DeletedByThemCard = "main changed other.txt; feature/kinds deleted it.";

        /// <summary>The binary card's text (11.8).</summary>
        public const string BinaryCard = "image.png is a binary file: its two versions cannot be merged line by line.";

        public static IReadOnlyList<string> DeletedByThemButtons { get; } = ["Keep the file", "Delete it"];
        public static IReadOnlyList<string> BinaryButtons { get; } = ["Use ours", "Use theirs"];

        /// <summary>The height of the resolver's header and toolbar rows (11.2).</summary>
        public const double ResolverRowHeight = 36;

        /// <summary>Where the resolver spans at 1100×700: the graph and right-panel columns, x 260 to 1100.</summary>
        public const double ResolverLeft = 260;
        public const double ResolverWidth = 840;

        // ---- The questions (7.2) -----------------------------------------------------------------

        /// <summary>Staging or marking resolved with markers left: the title.</summary>
        public static string MarkersTitle(string file) => file + " still has conflict markers.";

        public const string MarkersQuestion = "Mark it resolved anyway? The markers are committed with it.";

        public const string ContinueMarkersTitle = "1 file still has conflict markers.";
        public const string ContinueMarkersQuestion = "Continue anyway? The markers are committed with it.";
        public const string ContinueText = "Continue";

        /// <summary>Leaving the resolver with a modified result, or the merge tool with one: the title.</summary>
        public static string DiscardTitle(string file) => $"Discard your changes to {file}?";

        public const string LeaveQuestion = "The result has not been marked resolved.";
        public const string MergeToolQuestion = "The merge tool starts from the file as it is on disk.";
        public const string Discard = "Discard";
        public const string KeepEditing = "Keep editing";
        public const string Cancel = "Cancel";

        // ---- Menus (7.1, 11.12, 14.3) -------------------------------------------------------------

        /// <summary>The conflicted file's menu (7.1): 9 items and 2 separators, 284 high.</summary>
        public static IReadOnlyList<string> NotesFileMenu { get; } =
        [
            "notes.txt",
            "Resolve conflict",
            "Open in merge tool",
            "Mark as resolved",
            "Discard changes… (disabled: \"Resolve the conflict first\")",
            "—",
            "Ignore notes.txt (disabled: \"Only untracked files can be ignored\")",
            "Ignore all *.txt files (disabled: \"Only untracked files can be ignored\")",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        public const double NotesFileMenuHeight = 284;

        /// <summary>The result's menu after typing, nothing selected (C14 step 3): 6 items and 1 separator, 197 high.</summary>
        public static IReadOnlyList<string> ResultMenuAfterTyping { get; } =
        [
            "notes.txt (result)",
            "Undo",
            "Redo (disabled: \"Nothing to redo\")",
            "—",
            "Cut (disabled: \"Select some text first\")",
            "Copy (disabled: \"Select some text first\")",
            "Paste",
            "Select all",
        ];

        public const double ResultMenuHeight = 197;

        /// <summary>The ours pane's menu with nothing selected (C14 step 3): 58 high.</summary>
        public static IReadOnlyList<string> OursPaneMenu { get; } = ["Ours: main", "Copy (disabled: \"Select some text first\")"];

        public const double PaneMenuHeight = 58;

        // ---- The stage panel (5.3, phase 3) -------------------------------------------------------

        public const string ConflictRowTip = "Conflict: click to resolve it";
        public const string StageFilesToCommit = "Stage files to commit";
        public const string Commit1File = "Commit 1 file";
        public const string Commit2Files = "Commit 2 files";
        public const string ContinueRebaseButton = "Continue rebase";
        public const string FinishRebaseFirst = "Finish or abort the rebase first";
        public const string NoUnstaged = "No unstaged changes";
        public const string NoStaged = "No staged changes";
        public const string MergeTheirsSummary = "Merge branch 'feature/theirs'";
        public const string MergeKindsSummary = "Merge branch 'feature/kinds'";
        public const string PickConflictingSummary = "Pick conflicting";

        /// <summary><c>Unstaged files (N)</c> or <c>Unstaged files (N, M with conflicts)</c>.</summary>
        public static string UnstagedTitle(int files, int conflicts = 0) => conflicts == 0
            ? string.Create(CultureInfo.InvariantCulture, $"Unstaged files ({files})")
            : string.Create(CultureInfo.InvariantCulture, $"Unstaged files ({files}, {conflicts} with conflicts)");

        /// <summary><c>Staged files (N)</c>.</summary>
        public static string StagedTitle(int files) => string.Create(CultureInfo.InvariantCulture, $"Staged files ({files})");

        // ---- Operations and status (10.1, 10.2) ---------------------------------------------------

        /// <summary>The operation that resolves a file: <c>Resolve {file}</c>.</summary>
        public static string ResolveTitle(string file) => "Resolve " + file;

        /// <summary>Its status: <c>Resolved {file}</c> (secondary).</summary>
        public static string Resolved(string file) => "Resolved " + file;

        public const string MergeToolTitle = "Merge tool for notes.txt";
        public const string MergeToolRunning = "Waiting for fake to close…";
        public const string ResolvedInFake = "Resolved notes.txt in fake";
        public const string ContinueRebaseTitle = "Continue the rebase";
        public const string ContinueRebaseRunning = "Continuing the rebase…";
        public const string RebasedPicks = "Rebased feature/picks onto main";
        public const string ContinueMergeTitle = "Continue the merge";

        // ---- The git commands the app runs (as the call log writes them) ------------------------

        public const string StageAllNotes = "git add --all -- notes.txt";
        public const string CheckStagedMarkers = "git diff --cached --check --no-color --no-ext-diff -- notes.txt";
        public const string AddOther = "git add -- other.txt";
        public const string CheckoutTheirsImage = "git checkout --theirs -- image.png";
        public const string AddImage = "git add -- image.png";
        public const string AddNew = "git add -- new.txt";
        public const string MergeToolCommand = "git -c mergetool.keepBackup=false mergetool --tool=fake --no-prompt -- notes.txt";
        public const string RebaseContinue = "git rebase --continue";
    }
}
