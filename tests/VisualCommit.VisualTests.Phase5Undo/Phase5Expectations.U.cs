// Part U's expected results (docs/test-reports/phase-5.md, "Part U: undo and redo": its
// conventions, "Scenarios and graphs of part U", "Commits and objects the checks make", "Texts
// these checks rely on (part U)" and the rows U16 to U23 of "Checks"), copied from the report. Its
// members live in Phase5Expectations.PartU. Two writers share this file: each adds its members in
// its own region (U16 to U19, U20 to U23) and edits no other. Compiled by the real-window pass
// too: see Phase5Expectations.Common.cs for what may be used here (data and pure helpers, no
// headless types).
using Avalonia;
using VisualCommit.Core.Git;

namespace VisualCommit.VisualTests.Phase5;

public static partial class Phase5Expectations
{
    /// <summary>What part U's checks (U16 to U23) compare against.</summary>
    public static partial class PartU
    {
        #region U16 to U19: the stage panel's writes, resets, branch operations, history operations

        // ---- A step (the conventions' "A step ran", design 12.1, 12.2, 7.3) ----------------------

        /// <summary>
        /// The texts of one direction of a step: its operation's title (<c>Undo {text}</c>), its
        /// running text (<c>Undoing {text}…</c>), its result in the status bar (<c>Undid {text}</c>)
        /// and the reflog message of its ref writes (<c>VisualCommit: undo {text}</c>).
        /// </summary>
        public sealed record StepTexts(string Verb, string Running, string Done, string Reflog)
        {
            public string Title(string text) => $"{Verb} {text}";

            public string RunningText(string text) => $"{Running} {text}…";

            public string DoneText(string text) => $"{Done} {text}";

            public string Message(string text) => $"{Reflog} {text}";
        }

        /// <summary>Undo's texts.</summary>
        public static StepTexts Undo { get; } = new("Undo", "Undoing", "Undid", "VisualCommit: undo");

        /// <summary>Redo's texts.</summary>
        public static StepTexts Redo { get; } = new("Redo", "Redoing", "Redid", "VisualCommit: redo");

        /// <summary>
        /// A step's ref transaction as the call log writes it (UD20, design 15.8):
        /// <c>git update-ref -m &lt;message&gt; [--create-reflog] --stdin</c>, the message one argument.
        /// </summary>
        public static string Transaction(string message, bool createReflog = false) =>
            Git(createReflog ? ["update-ref", "-m", message, "--create-reflog", "--stdin"] : ["update-ref", "-m", message, "--stdin"]);

        /// <summary>A Keep move's positional <c>update-ref</c> (7.3, D169): <c>git update-ref -m &lt;message&gt; refs/heads/&lt;b&gt; &lt;to&gt; &lt;from&gt;</c>.</summary>
        public static string KeepMoveRef(string message, string branch, string to, string from) =>
            Git("update-ref", "-m", message, "refs/heads/" + branch, to, from);

        /// <summary>A Keep move's refresh before its <c>read-tree</c> (0.4).</summary>
        public static string Refresh { get; } = Git("update-index", "-q", "--refresh");

        /// <summary>A Keep move's <c>git read-tree -m -u &lt;from&gt; &lt;to&gt;</c>.</summary>
        public static string ReadTree(string from, string to) => Git("read-tree", "-m", "-u", from, to);

        /// <summary>The undo's switch to a branch (7.3): <c>git switch --no-guess --no-overwrite-ignore &lt;b&gt;</c>.</summary>
        public static string UndoSwitch(string branch) => Git("switch", "--no-guess", "--no-overwrite-ignore", branch);

        /// <summary>The undo's switch to a commit (7.3): <c>git switch --detach --no-overwrite-ignore &lt;sha&gt;</c>.</summary>
        public static string UndoDetach(string sha) => Git("switch", "--detach", "--no-overwrite-ignore", sha);

        /// <summary>A call's command line as the call log writes it (<see cref="GitCommand.DisplayText"/>).</summary>
        public static string Git(params string[] arguments) => new GitCommand(arguments).DisplayText;

        /// <summary>The transaction's line that moves <paramref name="reference"/> from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static string UpdateLine(string reference, string to, string from) => $"update {reference} {to} {from}";

        /// <summary>The transaction's line that creates <paramref name="reference"/> at <paramref name="to"/>.</summary>
        public static string CreateLine(string reference, string to) => $"create {reference} {to}";

        /// <summary>The transaction's line that deletes <paramref name="reference"/>, which is at <paramref name="from"/>.</summary>
        public static string DeleteLine(string reference, string from) => $"delete {reference} {from}";

        // ---- The shell (earlier phases' texts the checks assert) ---------------------------------

        /// <summary>The details panel with nothing selected.</summary>
        public const string NothingSelectedPlaceholder = "Select a commit to see its details.";

        /// <summary>The stage panel's empty unstaged list.</summary>
        public const string NoUnstagedChanges = "No unstaged changes";

        /// <summary>The status bar after a stage-panel write (it shows no operation's result).</summary>
        public const string Ready = "Ready";

        /// <summary>The working-changes row's text and ring (lane 0, colour 0) in every graph of part U that has one.</summary>
        public static WorkingChangesRow WorkingRow { get; } = new("Working changes", 0, 0);

        /// <summary>The working-changes row's point "in its Message column" at 1100×700 with two lanes (display row 0).</summary>
        public static Point WorkingRowPoint { get; } = new(468, 129);

        /// <summary>The author "Test Author" &lt;author@example.com&gt;, as git's <c>%an &lt;%ae&gt;</c> writes it.</summary>
        public const string TestAuthor = "Test Author <author@example.com>";

        /// <summary>The snapshot commits' author (D67).</summary>
        public const string SnapshotAuthor = "VisualCommit <snapshot@visualcommit.invalid>";

        /// <summary>
        /// The name of the <paramref name="index"/>-th backup a session makes (from 1), of
        /// <paramref name="kind"/>: the harness's backup clock names the first
        /// <c>&lt;kind&gt;-20260102T090001000Z</c> and each later one a second after the last (design 2.3).
        /// </summary>
        public static string BackupStem(string kind, int index) =>
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{kind}-20260102T0900{index:00}000Z");

        /// <summary>The full ref of the <paramref name="index"/>-th backup of <paramref name="kind"/>, with what follows its stem (<c>/feature/alpha</c>, <c>-changes</c>).</summary>
        public static string BackupRefName(string kind, int index, string suffix = "") => BackupRef.Prefix + BackupStem(kind, index) + suffix;

        // ---- U16: "history" (scenario facts 2; "Scenarios and graphs of part U") -----------------

        public const string ReadWholeLinesSha = "c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9";

        /// <summary>U16's commit: "Describe the reader and add Length" with its body, on Read whole lines.</summary>
        public const string U16CommitSha = "8d845e81333e9fd0e441c4d1813368bfc68aa722";

        /// <summary>U16's amend: "Read whole lines" again, on Stop tracing, with the staged change.</summary>
        public const string U16AmendSha = "bd74e66213b5ef2b87762cf21cd6ecff5321ff75";

        public const string StopTracingShort = "20d7fbe";

        public const string CommitSummary = "Describe the reader and add Length";

        public const string CommitDescription = "The summary says lines now.";

        /// <summary>The commit's message as git keeps it.</summary>
        public const string CommitMessage = CommitSummary + "\n\n" + CommitDescription;

        /// <summary>What is left of the summary's 72 characters with the commit's summary in it (34).</summary>
        public const string CommitSummaryCounter = "38";

        public const string AmendSummary = "Read whole lines";

        /// <summary>What is left of the summary's 72 characters with "Read whole lines" in it (16).</summary>
        public const string AmendSummaryCounter = "56";

        /// <summary>Read whole lines' author, kept by the amend.</summary>
        public const string Jonas = "Jonas Berg <jonas@example.com>";

        public static string CommitText { get; } = $"commit \"{CommitSummary}\"";

        public static string AmendText { get; } = $"amend \"{AmendSummary}\"";

        public const string DiscardReaderText = "discard of Reader.cs";

        public const string DiscardLinesText = "discard of lines in Reader.cs";

        public const string DiscardTwoText = "discard of 2 files";

        public const string RestoreText = "restore of the discarded files";

        public const string CreateKeepText = "creation of keep";

        /// <summary>The commit's call (phase 2's).</summary>
        public static string CommitCall { get; } = Git("commit", "--file=-", "--cleanup=whitespace");

        public const string ReaderPath = "src/Reader.cs";

        public const string TodoPath = "docs/todo.md";

        public const string Todo = "# To do\n";

        /// <summary>UD1: README.md with its last line replaced by "Staged.", staged before part F's app starts.</summary>
        public const string ReadmeStaged = "# History\n\nA repository for the history checks.\n\nStaged.\n";

        /// <summary>The line D75's case appends to src/Reader.cs outside the app (UD1).</summary>
        public const string ChangedOutsideLine = "// Changed outside.\n";

        /// <summary>src/Reader.cs as committed (Read whole lines').</summary>
        public const string ReaderCommittedBlob = "7061deb";

        /// <summary>src/Reader.cs as the scenario's working tree holds it (two hunks).</summary>
        public const string ReaderWorkingBlob = "3056624";

        /// <summary>src/Reader.cs after the second hunk's discard: the committed text with line 3 changed.</summary>
        public const string ReaderFirstHunkBlob = "f4e302e";

        /// <summary>src/Reader.cs after line 23's discard: the working text without it (23 lines).</summary>
        public const string ReaderWithoutLine23Blob = "14bf8a8";

        /// <summary>src/Reader.cs as committed with <see cref="ChangedOutsideLine"/> appended.</summary>
        public const string ReaderChangedOutsideBlob = "597f7de";

        /// <summary>The first discard snapshot of src/Reader.cs's working text, as plain git makes it (D72's dates); the second is the same commit.</summary>
        public const string ReaderSnapshotShort = "0bd36b6";

        /// <summary>The snapshot D75's Restore saves of the newer file, as plain git makes it.</summary>
        public const string ChangedOutsideSnapshotShort = "d76813b";

        public const string RestoreBarReader = "Discarded changes to Reader.cs.";

        public const string RestoreBarTwoFiles = "Discarded changes to 2 files.";

        /// <summary>D75's question when a file changed since the discard (phase 2's), asserted at U16 step 15.</summary>
        public const string ChangedSinceTitle = "Restore discarded changes?";

        public const string ChangedSinceQuestion = "Reader.cs has changed since the discard. Put back the version from before the discard?";

        public const string ChangedSinceDetail = "A snapshot of the newer changes is saved first.";

        public const string ChangedSinceConfirm = "Restore";

        /// <summary>The discard dialog's questions (phase 2's).</summary>
        public const string DiscardReaderQuestion = "Discard the changes to Reader.cs?";

        public const string DiscardHunkQuestion = "Discard this hunk of Reader.cs?";

        public const string DiscardLineQuestion = "Discard 1 selected line of Reader.cs?";

        public const string DiscardTwoQuestion = "Discard the changes to 2 files?";

        public const string DiscardTitle = "Discard changes?";

        public const string FirstHunkHeader = "@@ -1,6 +1,6 @@";

        public const string SecondHunkHeader = "@@ -19,4 +19,6 @@";

        public const string SecondHunkWithoutLine23 = "@@ -19,4 +19,5 @@";

        /// <summary>The snapshot commit's subject before a discard of <paramref name="files"/> files.</summary>
        public static string DiscardSnapshotSubject(int files) =>
            files == 1 ? "VisualCommit: snapshot before discarding 1 file" : $"VisualCommit: snapshot before discarding {files} files";

        public const string StatusCreatedKeep = "Created keep";

        /// <summary>src/Reader.cs's line 3 in the working text, the first hunk's change (without its indentation).</summary>
        public const string ReaderLine3 = "/// <summary>Reads the lines of a text.</summary>";

        /// <summary>The working text's added line 23, which U16 part E discards.</summary>
        public const int Line23 = 23;

        /// <summary>Line 23's text without its indentation.</summary>
        public const string Line23Text = "public int Length => _text.Length;";

        /// <summary>
        /// The history scenario's graph H0 (commit rows; the working-changes row above it is
        /// display row 0): two lanes, Message from x 438.
        /// </summary>
        public static IReadOnlyList<CommitRow> H0 { get; } =
        [
            HistoryRow("Read whole lines", 0, 0, "c0bc2dd", 13, Current("main"), "origin/main"),
            HistoryRow("Stop tracing", 0, 0, "20d7fbe", 12),
            HistoryRow("Write new notes", 0, 0, "e765529", 11),
            HistoryRow("Merge branch 'feature/readme'", 0, 0, "f513566", 10) with { Merge = true },
            HistoryRow("Explain the reader", 1, 1, "c1b6375", 9),
            HistoryRow("Remove the notes", 0, 0, "884b462", 8) with { Ends = [1] },
            HistoryRow("Compare whole lines", 1, 2, "245c395", 7, "feature/compare", "origin/feature/compare"),
            HistoryRow("Rename the parser to reader", 0, 0, "0575c7f", 6),
            HistoryRow("Add the comparer", 1, 2, "d0d3c77", 5),
            HistoryRow("Add tracing", 0, 0, "14a5c6c", 4) with { Ends = [1] },
            HistoryRow("Handle empty input", 0, 0, "365482b", 3),
            HistoryRow("Add the logo", 0, 0, "775113f", 2),
            HistoryRow("Add the parser", 0, 0, "1d5a7cf", 1),
            HistoryRow("Initial commit", 0, 0, "7829335", 0),
        ];

        /// <summary>H-U1, after U16's commit: no working-changes row, 15 rows, two lanes.</summary>
        public static IReadOnlyList<CommitRow> HU1 { get; } =
        [
            new CommitRow(CommitSummary, 0, 0, [Current("main")], "8d845e8", Date: "2026-01-02 09:00"),
            HistoryRow("Read whole lines", 0, 0, "c0bc2dd", 13, "origin/main"),
            .. H0.Skip(1),
        ];

        /// <summary>H-U2, after U16's amend: the two "Read whole lines" side by side, the side branches' colours one higher.</summary>
        public static IReadOnlyList<CommitRow> HU2 { get; } =
        [
            HistoryRow("Read whole lines", 0, 0, "bd74e66", 13, Current("main")),
            HistoryRow("Read whole lines", 1, 1, "c0bc2dd", 13, "origin/main"),
            HistoryRow("Stop tracing", 0, 0, "20d7fbe", 12) with { Ends = [1] },
            .. H0.Skip(2).Select(row => row.Lane == 1 ? row with { Colour = row.Colour + 1 } : row),
        ];

        /// <summary>A row of the history scenario, dated 2026-01-01 12:<paramref name="minute"/>.</summary>
        private static CommitRow HistoryRow(string message, int lane, int colour, string sha, int minute, params string[] labels) =>
            new(message, lane, colour, labels, sha, Date: ScenarioDate(minute));

        /// <summary>The Date column's text of a scenario commit made at 12:<paramref name="minute"/> on 2026-01-01.</summary>
        private static string ScenarioDate(int minute) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"2026-01-01 12:{minute:00}");

        // ---- "ops" (phase 3's G0 and the graphs part U derives from it) -------------------------

        public const string MainWorkSha = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

        public const string AddCoreSha = "8beca345049bf7e8bab3a7b4f1427eff941b87e3";

        public const string FastForwardMeSha = "d93814d4ea6e21d1a70dd93066dbb23512c03b56";

        public const string AlphaTwoSha = "ce5ea6ab16759bede02ce706dd5c6865443c2051";

        public const string AlphaOneSha = "dff8fbf3d84b0feb999ef8b3ea0e22c20d898be4";

        public const string ExperimentStashSha = "0e95aca13f7975ef4a1fe029a8b5e9497f31028d";

        public const string ConflictingChangeShort = "078ef05";

        /// <summary>A short SHA as the screen and the entry texts write it: the first 7 characters.</summary>
        public static string Short(string sha) => sha[..7];

        /// <summary>
        /// G0: "ops" as built, the stash's line dashed (3 lanes, Message from x 454). With
        /// <see cref="WorkingRow"/> above it, G0+w.
        /// </summary>
        public static IReadOnlyList<CommitRow> G0 { get; } = OpsRows(["main ✓", "origin/main", "v1.1"], ["v1.0"]);

        /// <summary>
        /// G0's rows with other labels on Main work (row 3) and Add core (row 6), the label-only
        /// changes of G0 (G0 hotfix, G0 HEAD at Add core, …), and G6's rows under its working-changes row.
        /// </summary>
        public static IReadOnlyList<CommitRow> OpsRows(
            IReadOnlyList<string> mainWork,
            IReadOnlyList<string> addCore,
            IReadOnlyList<string>? conflict = null,
            IReadOnlyList<string>? fastForward = null,
            IReadOnlyList<string>? alphaTwo = null) =>
        [
            new CommitRow("On main: Experiment", 0, 0, ["stash@{0}"], "0e95aca", Stash: true, Date: ScenarioDate(7)),
            new CommitRow("Conflicting change", 1, 1, conflict ?? ["feature/conflict"], ConflictingChangeShort, Date: ScenarioDate(6)),
            new CommitRow("Fast-forward me", 2, 2, fastForward ?? ["feature/ff"], "d93814d", Date: ScenarioDate(5)),
            new CommitRow("Main work", 0, 0, mainWork, "88d6b91", Date: ScenarioDate(4), Ends: [2]),
            new CommitRow("Alpha two", 2, 3, alphaTwo ?? ["feature/alpha", "origin/feature/alpha"], "ce5ea6a", Date: ScenarioDate(3)),
            new CommitRow("Alpha one", 2, 3, [], "dff8fbf", Date: ScenarioDate(2)),
            new CommitRow("Add core", 0, 0, addCore, "8beca34", Date: ScenarioDate(1), Ends: [1, 2]),
            new CommitRow("Initial commit", 0, 0, [], "4ef1f7c", Date: ScenarioDate(0)),
        ];

        /// <summary>G0's lanes.</summary>
        public const int OpsLanes = 3;

        /// <summary>G6: main reset to Add core (a working-changes row above, its ring's dashed line to Add core's node).</summary>
        public static IReadOnlyList<CommitRow> G6 { get; } = OpsRows(["origin/main", "v1.1"], ["main ✓", "v1.0"]);

        // ---- U17: resets on "ops" (phase 3's check 10) -------------------------------------------

        public static string ResetText(string mode) => $"reset of main to 8beca34 ({mode})";

        public static string StatusReset(string mode) => $"Reset main to 8beca34 ({mode})";

        public const string ResetMenuItem = "Reset main to this commit…";

        /// <summary>Phase 3's check 10 part C changes (UD5), every line ending in a line feed.</summary>
        public const string CoreStaged = "core\nmain work\nstaged line\n";

        public const string CoreUnstaged = "core\nmain work\nstaged line\nunstaged line\n";

        public const string ReadmeLocalEdit = "# Operations\n\nLocal edit.\n";

        public const string Scratch = "scratch\n";

        public const string OpsTodo = "todo\n";

        public const string MixedIndexSubject = "VisualCommit: index before a mixed reset";

        public const string HardIndexSubject = "VisualCommit: index before a hard reset";

        public const string HardSnapshotSubject = "VisualCommit: snapshot before a hard reset to 8beca34";

        /// <summary>The stage panel before U17's steps 7 and 10 (phase 3's check 10 step 7).</summary>
        public static string[] PartCUnstaged { get; } = ["M README.md", "M core.txt src", "A todo.txt"];

        public static string[] PartCStaged { get; } = ["A scratch.txt", "M core.txt src"];

        /// <summary>The conventions' status with part C's changes on Main work.</summary>
        public static string[] PartCStatus { get; } = ["## main...origin/main", " M README.md", "A  scratch.txt", "MM src/core.txt", "?? todo.txt"];

        // ---- U18: branch operations on "ops", "edges" and "blocked" (phase 3's checks 6, 13, 13b, 14, 30, 32) ----

        /// <summary>Main work's labels with main not checked out (G0 ff✓, G0 alpha✓, G0 HEAD at Add core).</summary>
        public static IReadOnlyList<string> MainWorkNotCurrent { get; } = ["main", "origin/main", "v1.1"];

        /// <summary>G0 hotfix: hotfix created at Main work, not checked out.</summary>
        public static IReadOnlyList<CommitRow> G0Hotfix { get; } = OpsRows(["main ✓", "hotfix", "origin/main", "v1.1"], ["v1.0"]);

        /// <summary>G0 hotfix✓: hotfix created at Main work and checked out.</summary>
        public static IReadOnlyList<CommitRow> G0HotfixCurrent { get; } = OpsRows(["hotfix ✓", "main", "origin/main", "v1.1"], ["v1.0"]);

        /// <summary>G0 conflicting: feature/conflict renamed.</summary>
        public static IReadOnlyList<CommitRow> G0Conflicting { get; } = OpsRows(["main ✓", "origin/main", "v1.1"], ["v1.0"], conflict: ["feature/conflicting"]);

        /// <summary>G0 −alpha: the local feature/alpha gone, its upstream left.</summary>
        public static IReadOnlyList<CommitRow> G0WithoutAlpha { get; } = OpsRows(["main ✓", "origin/main", "v1.1"], ["v1.0"], alphaTwo: ["origin/feature/alpha"]);

        /// <summary>G0 ff✓: feature/ff checked out.</summary>
        public static IReadOnlyList<CommitRow> G0FfCurrent { get; } = OpsRows(MainWorkNotCurrent, ["v1.0"], fastForward: ["feature/ff ✓"]);

        /// <summary>G0 alpha✓: feature/alpha checked out.</summary>
        public static IReadOnlyList<CommitRow> G0AlphaCurrent { get; } = OpsRows(MainWorkNotCurrent, ["v1.0"], alphaTwo: ["feature/alpha ✓", "origin/feature/alpha"]);

        /// <summary>G0 HEAD at Add core: HEAD detached at Add core.</summary>
        public static IReadOnlyList<CommitRow> G0HeadAtAddCore { get; } = OpsRows(MainWorkNotCurrent, ["HEAD", "v1.0"]);

        public const string HotfixText = "creation of hotfix";

        public const string RenameText = "rename of feature/conflict to feature/conflicting";

        public const string DeleteAlphaText = "deletion of feature/alpha";

        public const string CheckoutFfText = "checkout of feature/ff";

        public const string CheckoutRemoteAlphaText = "checkout of origin/feature/alpha";

        public const string CheckoutAddCoreText = "checkout of 8beca34";

        public const string CheckoutMainText = "checkout of main";

        public const string FastForwardLocText = "fast-forward of loc";

        public const string CheckoutOtherText = "checkout of feature/other";

        public const string StatusCreatedHotfix = "Created hotfix";

        public const string StatusCreatedHotfixCheckedOut = "Created hotfix and checked it out";

        public const string StatusRenamed = "Renamed feature/conflict to feature/conflicting";

        public const string StatusDeletedAlpha = "Deleted feature/alpha";

        public const string StatusCheckedOutFf = "Checked out feature/ff";

        public const string StatusTrackingAlpha = "Created feature/alpha tracking origin/feature/alpha and checked it out";

        public const string StatusDetachedAtAddCore = "HEAD is now detached at 8beca34";

        public const string StatusCheckedOutMain = "Checked out main";

        public const string StatusFastForwardedLoc = "Fast-forwarded loc to main";

        public const string StatusCheckedOutOtherWithChanges = "Checked out feature/other and re-applied your changes";

        public const string CreateBranchMenuItem = "Create branch here…";

        public const string DeleteBranchTitle = "Delete branch?";

        public const string DeleteAlphaQuestion = "Delete the branch feature/alpha? It has commits that main does not have.";

        public const string DeleteBranchConfirm = "Delete";

        /// <summary>Phase 3's question before leaving a detached HEAD's commit (check 32).</summary>
        public const string LeaveTitle = "Leave these commits?";

        public const string LeaveQuestion = "HEAD is detached on 1 commit that is on no branch. After this checkout it no longer shows in the graph.";

        /// <summary>The upstream keys a tracking feature/alpha has, as <c>git config --local --get-regexp</c> lists them.</summary>
        public static string[] AlphaUpstreamKeys { get; } = ["branch.feature/alpha.remote origin", "branch.feature/alpha.merge refs/heads/feature/alpha"];

        // "edges" (phase 3's E0 and E1).

        public const string DetachedWorkSha = "419190f40391bb6b4e317c793b9dd6c9939af6a0";

        public const string EdgesMainWorkSha = "8e164080822465a9cb64c0278d3b30598acbeabf";

        public const string EdgesInitialSha = "691f8cfa93e845f8ab8993d2595d227e967caff5";

        /// <summary>E0: HEAD detached on Detached work, two lanes.</summary>
        public static IReadOnlyList<CommitRow> E0 { get; } = EdgesRows(["main", "origin/main", "feature/alpha"], ["loc", "origin/main", "side"]);

        /// <summary>E0 loc: loc fast-forwarded to Main work.</summary>
        public static IReadOnlyList<CommitRow> E0Loc { get; } = EdgesRows(["loc", "main", "origin/main", "feature/alpha"], ["origin/main", "side"]);

        private static IReadOnlyList<CommitRow> EdgesRows(IReadOnlyList<string> mainWork, IReadOnlyList<string> initial) =>
        [
            new CommitRow("Detached work", 0, 0, ["HEAD"], "419190f", Date: ScenarioDate(3)),
            new CommitRow("Alpha work", 1, 1, ["feature/alpha"], "a024e50", Date: ScenarioDate(2)),
            new CommitRow("Main work", 0, 0, mainWork, "8e16408", Date: ScenarioDate(1), Ends: [1]),
            new CommitRow("Initial commit", 0, 0, initial, "691f8cf", Date: ScenarioDate(0)),
        ];

        /// <summary>E1: main checked out, Detached work left (one lane).</summary>
        public static IReadOnlyList<CommitRow> E1 { get; } =
        [
            new CommitRow("Alpha work", 0, 0, ["feature/alpha"], "a024e50", Date: ScenarioDate(2)),
            new CommitRow("Main work", 0, 0, ["main ✓", "origin/main", "feature/alpha"], "8e16408", Date: ScenarioDate(1)),
            new CommitRow("Initial commit", 0, 0, ["loc", "origin/main", "side"], "691f8cf", Date: ScenarioDate(0)),
        ];

        /// <summary>git's line for the fast-forward of loc (Git 2.36's, standard error).</summary>
        public const string FastForwardLocLine = "   691f8cf..8e16408  main       -> loc";

        // "blocked" (phase 3's G14a and G14b, check 14).

        public const string OtherChangeSha = "850a7c0db4553015af300aeed2cd7d0c6a095912";

        public const string AddNotesSha = "106efba4a731d81895cbb9db6f3ef02453078d9a";

        /// <summary>G14a: on main, the working-changes row above (2 lanes).</summary>
        public static IReadOnlyList<CommitRow> G14a { get; } = BlockedRows(["feature/other"], ["main ✓"]);

        /// <summary>G14b: on feature/other.</summary>
        public static IReadOnlyList<CommitRow> G14b { get; } = BlockedRows(["feature/other ✓"], ["main"]);

        private static IReadOnlyList<CommitRow> BlockedRows(IReadOnlyList<string> other, IReadOnlyList<string> main) =>
        [
            new CommitRow("Add newfile", 0, 0, ["feature/addfile"], Date: ScenarioDate(3)),
            new CommitRow("Clashing change", 1, 1, ["feature/clash"], Date: ScenarioDate(2)),
            new CommitRow("Other change", 0, 0, other, "850a7c0", Date: ScenarioDate(1)),
            new CommitRow("Add notes", 0, 0, main, "106efba", Date: ScenarioDate(0), Ends: [1]),
        ];

        /// <summary>notes.txt with the local change on feature/other's version (re-applied by D91).</summary>
        public const string NotesLocalOnOther = "one (local)\ntwo\nthree\nfour\nfive\nsix\nseven (other)\n";

        /// <summary>The C12 checkout refusal (design 9.3).</summary>
        public const string CheckoutBlockedTitle = "Checkout blocked";

        public const string OneConflictPreventsCheckout = "1 conflict prevents checkout. Commit, stash, or discard your changes first.";

        public const string StashAndCheckOut = "Stash changes and check out";

        /// <summary>Phase 3's T2: git's refusal of a checkout over the changed notes.txt.</summary>
        public static string[] T2 { get; } =
        [
            "error: Your local changes to the following files would be overwritten by checkout:",
            "\tnotes.txt",
            "Please commit your changes or stash them before you switch branches.",
            "Aborting",
        ];

        public const string T2Key = "error: Your local changes to the following files would be overwritten by checkout:";

        /// <summary>D91's stash before the checkout.</summary>
        public const string D91StashMessage = "VisualCommit: before checking out feature/other";

        /// <summary>The undo's stash of D91's round trip, as plain git makes it (popped at once).</summary>
        public const string UndoStashShort = "6cfd0e0";

        // ---- U19: history operations, each undone by a Keep move (7.3, D169) ----------------------

        // "ops" (phase 3's checks 8, 11 and 12).

        public const string MergeFfText = "merge of feature/ff into main";

        public const string PickTwoText = "cherry-pick of 2 commits";

        public const string RevertMainWorkText = "revert of 88d6b91";

        public const string StatusFastForwardedMain = "Fast-forwarded main to feature/ff";

        public const string StatusMergedFf = "Merged feature/ff into main";

        public const string StatusPickedTwo = "Cherry-picked 2 commits onto main";

        public const string StatusRevertedMainWork = "Reverted 88d6b91";

        /// <summary>Phase 3's merge, which fast-forwards here (the report's change after its first commit, U19 part A).</summary>
        public static string MergeFfCall { get; } = Git("merge", "--no-edit", "--no-log", "-m", "Merge branch 'feature/ff'", "refs/heads/feature/ff");

        public static string PickTwoCall { get; } = Git("cherry-pick", AlphaOneSha, AlphaTwoSha);

        /// <summary>
        /// HEAD's reflog after that merge fast-forwarded: git notes that the <c>-m</c> made no commit
        /// (Git 2.36 and 2.55; the report's change after its first commit).
        /// </summary>
        public const string MergeFfReflog = "merge refs/heads/feature/ff: Fast-forward (no commit created; -m option ignored)";

        public const string MergeCommitSha = "22a08ae80b78afd6ee714929be263ada7ad53abd";

        public const string PickedAlphaOneSha = "d1f9fb5fa4728b88c1d36136f0f65fb8456ac586";

        public const string PickedAlphaTwoSha = "c0f718d25dff3ea0ef8d67ada2d3411c9ab45b94";

        public const string RevertSha = "13c773dd19dbe8f7d2261eb3a2d7c2392670cd22";

        /// <summary>G1: main fast-forwarded to Fast-forward me; lanes, colours and lines as G0.</summary>
        public static IReadOnlyList<CommitRow> G1 { get; } = OpsRows(["origin/main", "v1.1"], ["v1.0"], fastForward: ["main ✓", "feature/ff"]);

        /// <summary>G2: the merge commit 22a08ae (4 lanes, no Date column).</summary>
        public static IReadOnlyList<CommitRow> G2 { get; } =
        [
            new CommitRow("Merge branch 'feature/ff'", 0, 0, ["main ✓"], "22a08ae", Merge: true),
            new CommitRow("On main: Experiment", 2, 2, ["stash@{0}"], "0e95aca", Stash: true),
            new CommitRow("Conflicting change", 3, 3, ["feature/conflict"], ConflictingChangeShort),
            new CommitRow("Fast-forward me", 1, 1, ["feature/ff"], "d93814d"),
            new CommitRow("Main work", 0, 0, ["origin/main", "v1.1"], "88d6b91"),
            new CommitRow("Alpha two", 1, 4, ["feature/alpha", "origin/feature/alpha"], "ce5ea6a"),
            new CommitRow("Alpha one", 1, 4, [], "dff8fbf"),
            new CommitRow("Add core", 0, 0, ["v1.0"], "8beca34"),
            new CommitRow("Initial commit", 0, 0, [], "4ef1f7c"),
        ];

        /// <summary>G8: two picks on main (4 lanes).</summary>
        public static IReadOnlyList<CommitRow> G8 { get; } =
        [
            new CommitRow("Alpha two", 0, 0, ["main ✓"], "c0f718d"),
            new CommitRow("Alpha one", 0, 0, [], "d1f9fb5"),
            new CommitRow("On main: Experiment", 1, 1, ["stash@{0}"], "0e95aca", Stash: true),
            new CommitRow("Conflicting change", 2, 2, ["feature/conflict"], ConflictingChangeShort),
            new CommitRow("Fast-forward me", 3, 3, ["feature/ff"], "d93814d"),
            new CommitRow("Main work", 0, 0, ["origin/main", "v1.1"], "88d6b91"),
            new CommitRow("Alpha two", 1, 4, ["feature/alpha", "origin/feature/alpha"], "ce5ea6a"),
            new CommitRow("Alpha one", 1, 4, [], "dff8fbf"),
            new CommitRow("Add core", 0, 0, ["v1.0"], "8beca34"),
            new CommitRow("Initial commit", 0, 0, [], "4ef1f7c"),
        ];

        /// <summary>G9: the revert 13c773d on main (4 lanes).</summary>
        public static IReadOnlyList<CommitRow> G9 { get; } =
        [
            new CommitRow("Revert \"Main work\"", 0, 0, ["main ✓"], "13c773d"),
            new CommitRow("On main: Experiment", 1, 1, ["stash@{0}"], "0e95aca", Stash: true),
            new CommitRow("Conflicting change", 2, 2, ["feature/conflict"], ConflictingChangeShort),
            new CommitRow("Fast-forward me", 3, 3, ["feature/ff"], "d93814d"),
            new CommitRow("Main work", 0, 0, ["origin/main", "v1.1"], "88d6b91"),
            new CommitRow("Alpha two", 1, 4, ["feature/alpha", "origin/feature/alpha"], "ce5ea6a"),
            new CommitRow("Alpha one", 1, 4, [], "dff8fbf"),
            new CommitRow("Add core", 0, 0, ["v1.0"], "8beca34"),
            new CommitRow("Initial commit", 0, 0, [], "4ef1f7c"),
        ];

        // "rebase" (phase 4's R0, R1, R3 and part U's R4).

        public const string TopicTwoSha = "b0dfa3f405dffff59bea3f4f434b16e5490cc597";

        public const string MainMoreSha = "1dd58b8cfc17d4d9b80550eddd80b11ebf94c146";

        public const string OtherOneSha = "2d4d79a03112c8a5e2f9a783318d588df519af5d";

        public const string TopicOneOntoMainSha = "befae2677a3a259f2c9423912af47575e319c852";

        public const string TopicTwoOntoMainSha = "5f71ab8bad51a15fdfdfd19fc1b0084ef6e956c5";

        public const string OtherOneOntoMainSha = "14e607bf248c8917ab93680c48cba392f60546ff";

        public const string MergeOtherSha = "5e5d815290fed817a30eb0225e708a088a714a5c";

        public const string RebaseTopicText = "rebase of feature/topic onto main";

        public const string RebaseOtherText = "rebase of feature/other onto main";

        public const string MergeOtherText = "merge of feature/other into main";

        public const string StatusRebasedTopic = "Rebased feature/topic onto main";

        public const string StatusRebasedOther = "Rebased feature/other onto main";

        public const string StatusMergedOther = "Merged feature/other into main";

        /// <summary>"Check out main and merge feature/other into it": the switch, then phase 3's merge.</summary>
        public static string MergeOtherCall { get; } = Git("merge", "--no-edit", "--no-log", "-m", "Merge branch 'feature/other'", "refs/heads/feature/other");

        /// <summary>R0, "rebase" as built (HEAD on feature/topic): 3 lanes.</summary>
        public static IReadOnlyList<CommitRow> R0 { get; } =
        [
            new CommitRow("Other one", 0, 0, ["feature/other"], "2d4d79a", Date: ScenarioDate(6)),
            new CommitRow("Main more", 1, 1, ["main", "origin/main"], "1dd58b8", Date: ScenarioDate(5)),
            new CommitRow("Main work", 1, 1, [], "dd6a6fc", Date: ScenarioDate(4)),
            new CommitRow("Topic two", 2, 2, ["feature/topic ✓", "origin/feature/topic"], "b0dfa3f", Date: ScenarioDate(3)),
            new CommitRow("Topic one", 2, 2, [], "1219e45", Date: ScenarioDate(2)),
            new CommitRow("Add core", 0, 0, [], "996fdc1", Date: ScenarioDate(1), Ends: [1, 2]),
            new CommitRow("Initial commit", 0, 0, [], "7bce6bf", Date: ScenarioDate(0)),
        ];

        /// <summary>R1, after "Rebase feature/topic onto main": 3 lanes.</summary>
        public static IReadOnlyList<CommitRow> R1 { get; } =
        [
            new CommitRow("Topic two", 0, 0, ["feature/topic ✓"], "5f71ab8", Date: ScenarioDate(3)),
            new CommitRow("Topic one", 0, 0, [], "befae26", Date: ScenarioDate(2)),
            new CommitRow("Other one", 1, 1, ["feature/other"], "2d4d79a", Date: ScenarioDate(6)),
            new CommitRow("Main more", 0, 0, ["main", "origin/main"], "1dd58b8", Date: ScenarioDate(5)),
            new CommitRow("Main work", 0, 0, [], "dd6a6fc", Date: ScenarioDate(4)),
            new CommitRow("Topic two", 2, 2, ["origin/feature/topic"], "b0dfa3f", Date: ScenarioDate(3)),
            new CommitRow("Topic one", 2, 2, [], "1219e45", Date: ScenarioDate(2)),
            new CommitRow("Add core", 0, 0, [], "996fdc1", Date: ScenarioDate(1), Ends: [1, 2]),
            new CommitRow("Initial commit", 0, 0, [], "7bce6bf", Date: ScenarioDate(0)),
        ];

        /// <summary>R3, after "Check out feature/other and rebase it onto main": 2 lanes.</summary>
        public static IReadOnlyList<CommitRow> R3 { get; } =
        [
            new CommitRow("Other one", 0, 0, ["feature/other ✓"], "14e607b", Date: ScenarioDate(6)),
            new CommitRow("Main more", 0, 0, ["main", "origin/main"], "1dd58b8", Date: ScenarioDate(5)),
            new CommitRow("Main work", 0, 0, [], "dd6a6fc", Date: ScenarioDate(4)),
            new CommitRow("Topic two", 1, 1, ["feature/topic", "origin/feature/topic"], "b0dfa3f", Date: ScenarioDate(3)),
            new CommitRow("Topic one", 1, 1, [], "1219e45", Date: ScenarioDate(2)),
            new CommitRow("Add core", 0, 0, [], "996fdc1", Date: ScenarioDate(1), Ends: [1]),
            new CommitRow("Initial commit", 0, 0, [], "7bce6bf", Date: ScenarioDate(0)),
        ];

        /// <summary>R4, after "Check out main and merge feature/other into it": 3 lanes.</summary>
        public static IReadOnlyList<CommitRow> R4 { get; } =
        [
            new CommitRow("Merge branch 'feature/other'", 0, 0, ["main ✓"], "5e5d815", Merge: true, Date: "2026-01-02 09:00"),
            new CommitRow("Other one", 1, 1, ["feature/other"], "2d4d79a", Date: ScenarioDate(6)),
            new CommitRow("Main more", 0, 0, ["origin/main"], "1dd58b8", Date: ScenarioDate(5)),
            new CommitRow("Main work", 0, 0, [], "dd6a6fc", Date: ScenarioDate(4)),
            new CommitRow("Topic two", 2, 2, ["feature/topic", "origin/feature/topic"], "b0dfa3f", Date: ScenarioDate(3)),
            new CommitRow("Topic one", 2, 2, [], "1219e45", Date: ScenarioDate(2)),
            new CommitRow("Add core", 0, 0, [], "996fdc1", Date: ScenarioDate(1), Ends: [1, 2]),
            new CommitRow("Initial commit", 0, 0, [], "7bce6bf", Date: ScenarioDate(0)),
        ];

        /// <summary>The drag of phase 4's check A3: five equal moves with the button held.</summary>
        public const int DragSteps = 5;

        // "interactive" (phase 4's G-I and G-B8, part U's G-U19).

        public const string BaseSha = "4b4622c8ca70c5cfe305043f40ea99c288fbefb3";

        public const string FiveSha = "57f089d442eea23fee65c387f560002ab1ccd1fc";

        public const string TwoSha = "938844a7a995d3769fbac6d4e9ff4a3095f15898";

        public const string InteractiveTipSha = "4ad730da7dcb81136e051ae3e0ee8d80bc5d368d";

        public const string SquashTipSha = "eb6f702889d08c7879c1daba704553fea072a42c";

        public const string InteractiveText = "interactive rebase of main";

        public const string SquashText = "squash of 2 commits";

        public const string StatusRebasedMainFive = "Rebased main: 5 commits";

        public const string StatusSquashed = "Squashed 2 commits into f34248d";

        public const string InteractiveFromHereItem = "Interactive rebase of main from here…";

        public const string SquashItem = "Squash 2 commits…";

        public const string RewordTwoTitle = "Reword 938844a";

        public const string TwoReworded = "Two reworded";

        public const string TwoRewordedSecondLine = "New message: Two reworded";

        /// <summary>Phase 4's L0: the list's rows as "short id subject", newest first, as it opens from Base.</summary>
        public static string[] L0Rows { get; } = ["57f089d Five", "eef5c15 Four", "ff42e7f Three", "ed58902 Fix one", "938844a Two", "fc6c8fd One"];

        /// <summary>The list row Two's point in L0 (x 600, its centre y 259).</summary>
        public static Point ListRowTwoPoint { get; } = new(600, 259);

        /// <summary>G-I: "interactive" as built, one lane.</summary>
        public static IReadOnlyList<CommitRow> GI { get; } =
        [
            new CommitRow("Five", 0, 0, ["main ✓"], "57f089d", Date: ScenarioDate(7)),
            new CommitRow("Four", 0, 0, [], "eef5c15", Date: ScenarioDate(6)),
            new CommitRow("Three", 0, 0, [], "ff42e7f", Date: ScenarioDate(5)),
            new CommitRow("Fix one", 0, 0, [], "ed58902", Date: ScenarioDate(4)),
            new CommitRow("Two", 0, 0, [], "938844a", Date: ScenarioDate(3)),
            new CommitRow("One", 0, 0, [], "fc6c8fd", Date: ScenarioDate(2)),
            new CommitRow("Base", 0, 0, [], "4b4622c", Date: ScenarioDate(1)),
            new CommitRow("Initial commit", 0, 0, [], "da32da3", Date: ScenarioDate(0)),
        ];

        /// <summary>G-U19, after U19's interactive rebase (Five dropped, Two reworded): one lane, 7 rows.</summary>
        public static IReadOnlyList<CommitRow> GU19 { get; } =
        [
            new CommitRow("Four", 0, 0, ["main ✓"], "4ad730d", Date: ScenarioDate(6)),
            new CommitRow("Three", 0, 0, [], "ad3ce4e", Date: ScenarioDate(5)),
            new CommitRow("Fix one", 0, 0, [], "4e61f14", Date: ScenarioDate(4)),
            new CommitRow(TwoReworded, 0, 0, [], "97e6de9", Date: ScenarioDate(3)),
            new CommitRow("One", 0, 0, [], "fc6c8fd", Date: ScenarioDate(2)),
            new CommitRow("Base", 0, 0, [], "4b4622c", Date: ScenarioDate(1)),
            new CommitRow("Initial commit", 0, 0, [], "da32da3", Date: ScenarioDate(0)),
        ];

        /// <summary>G-B8, after phase 4's B8 squash.</summary>
        public static IReadOnlyList<CommitRow> GB8 { get; } =
        [
            new CommitRow("Five", 0, 0, ["main ✓"], "eb6f702", Date: ScenarioDate(7)),
            new CommitRow("Three", 0, 0, [], "f34248d", Date: ScenarioDate(5)),
            new CommitRow("Fix one", 0, 0, [], "ed58902", Date: ScenarioDate(4)),
            new CommitRow("Two", 0, 0, [], "938844a", Date: ScenarioDate(3)),
            new CommitRow("One", 0, 0, [], "fc6c8fd", Date: ScenarioDate(2)),
            new CommitRow("Base", 0, 0, [], "4b4622c", Date: ScenarioDate(1)),
            new CommitRow("Initial commit", 0, 0, [], "da32da3", Date: ScenarioDate(0)),
        ];

        // "sync" (phase 4's A4 part A, S1 and S2, and part U's S2u).

        public const string LocalWorkSha = "f4f8c060b1e68a3e560113b942f1f984c6b5d4f0";

        public const string ElsewhereWorkSha = "b2e9a9e3724a696d2565eaa632dc7b04bca768a0";

        public const string LocalWorkRebasedSha = "8a1f07b0ca2c177d76be50610feaafb872a34863";

        public const string PullText = "pull into main";

        public const string StatusPulledAndRebased = "Pulled and rebased main onto origin/main";

        public const string PullRebaseItem = "Pull from origin/main (rebase)";

        /// <summary>git's fetch line of the pull (Git 2.36's, standard error).</summary>
        public const string FetchMainLine = "   a7f2cfd..b2e9a9e  main       -> origin/main";

        /// <summary>S1, the pull's start: 3 lanes.</summary>
        public static IReadOnlyList<CommitRow> S1 { get; } =
        [
            new CommitRow("Local work", 0, 0, ["main ✓"], "f4f8c06", Date: ScenarioDate(6)),
            new CommitRow("Topic work", 1, 1, ["topic"], "77d782e", Date: ScenarioDate(4)),
            new CommitRow("Unpushed work", 2, 2, ["published"], "8e65315", Date: ScenarioDate(3)),
            new CommitRow("Published work", 2, 2, ["origin/published"], "8c6f545", Date: ScenarioDate(2)),
            new CommitRow("Shared work", 0, 0, ["origin/main"], "a7f2cfd", Date: ScenarioDate(1), Ends: [1, 2]),
            new CommitRow("Initial commit", 0, 0, [], "6688774", Date: ScenarioDate(0)),
        ];

        /// <summary>S2, after "Pull from origin/main (rebase)": 3 lanes.</summary>
        public static IReadOnlyList<CommitRow> S2 { get; } =
        [
            new CommitRow("Local work", 0, 0, ["main ✓"], "8a1f07b", Date: ScenarioDate(6)),
            new CommitRow("Elsewhere work", 0, 0, ["origin/main"], "b2e9a9e", Date: ScenarioDate(5)),
            new CommitRow("Topic work", 1, 1, ["topic"], "77d782e", Date: ScenarioDate(4)),
            new CommitRow("Unpushed work", 2, 2, ["published"], "8e65315", Date: ScenarioDate(3)),
            new CommitRow("Published work", 2, 2, ["origin/published"], "8c6f545", Date: ScenarioDate(2)),
            new CommitRow("Shared work", 0, 0, [], "a7f2cfd", Date: ScenarioDate(1), Ends: [1, 2]),
            new CommitRow("Initial commit", 0, 0, [], "6688774", Date: ScenarioDate(0)),
        ];

        /// <summary>S2u: S2's pull undone, origin/main still fetched (4 lanes, no Date column).</summary>
        public static IReadOnlyList<CommitRow> S2u { get; } =
        [
            new CommitRow("Local work", 0, 0, ["main ✓"], "f4f8c06"),
            new CommitRow("Elsewhere work", 1, 1, ["origin/main"], "b2e9a9e"),
            new CommitRow("Topic work", 2, 2, ["topic"], "77d782e"),
            new CommitRow("Unpushed work", 3, 3, ["published"], "8e65315"),
            new CommitRow("Published work", 3, 3, ["origin/published"], "8c6f545"),
            new CommitRow("Shared work", 0, 0, [], "a7f2cfd", Ends: [1, 2, 3]),
            new CommitRow("Initial commit", 0, 0, [], "6688774"),
        ];

        #endregion
    }
}
