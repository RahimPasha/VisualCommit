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
            new(message, lane, colour, labels, sha, Date: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"2026-01-01 12:{minute:00}"));

        #endregion
    }
}
