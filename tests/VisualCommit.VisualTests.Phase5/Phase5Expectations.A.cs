// Part A's expected results (docs/test-reports/phase-5.md, "Part A: the shell and the activity
// log": its conventions, "Scenarios and graphs of part A", "Texts these checks rely on (part A)",
// the decisions AD1 to AD15 and the rows A1 to A4 of "Checks"), copied from the report and not read
// from the app. Only part A's writer edits this file; its members live in
// Phase5Expectations.PartA. Compiled by the real-window pass too: data and pure helpers only (see
// Phase5Expectations.Common.cs for what may be used here).
using System.Globalization;
using Avalonia;

namespace VisualCommit.VisualTests.Phase5;

public static partial class Phase5Expectations
{
    /// <summary>What part A's checks (A1 to A4) compare against.</summary>
    public static class PartA
    {
        // ---- The scenarios' names and commits (Phase5ScenarioTests and Phase4ScenarioTests pin them). ----

        /// <summary>The tab's and the repository's name of the "history" scenario.</summary>
        public const string HistoryRepo = "history";

        /// <summary>The tab's and the repository's name of the "conflicts" scenario.</summary>
        public const string ConflictsRepo = "conflicts";

        /// <summary>The empty tab's title (phase 1, D42).</summary>
        public const string NewTab = "New tab";

        /// <summary>"Read whole lines", HEAD's commit in "history" and origin/main's.</summary>
        public const string ReadWholeLinesSha = "c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9";

        /// <summary>H-A1's commit "Describe the lines", which the app makes in A1 and A4 (plain git makes it alike).</summary>
        public const string DescribeTheLinesSha = "d1e23a37b6b08f4dc9662fab930f232a66ecd8fc";

        /// <summary>"Ours edits", HEAD's commit in "conflicts".</summary>
        public const string OursEditsSha = "da31c977c80e9becdc908c74d973669aad1b3b8d";

        /// <summary>"Theirs edits", feature/theirs' tip: <c>MERGE_HEAD</c> while the merge is stopped.</summary>
        public const string TheirsEditsSha = "5c77ffb0ec78abf986d121f42013eee14422e5a5";

        /// <summary>src/Reader.cs's blob in HEAD ("history").</summary>
        public const string ReaderBlobInHead = "7061deb";

        /// <summary>src/Reader.cs's blob as the working tree holds it ("history"), and in H-A1's commit.</summary>
        public const string ReaderBlobChanged = "3056624";

        /// <summary>The changed file of "history".</summary>
        public const string ReaderPath = "src/Reader.cs";

        // ---- The commit of A1 and A4 (AD8) and its undo and redo (7.3, 7.5, 7.6, 12.1, 12.2). ----

        /// <summary>The summary A1 and A4 commit with (no description).</summary>
        public const string CommitSummary = "Describe the lines";

        /// <summary>What the undo history calls the commit (7.6).</summary>
        public const string CommitText = "commit \"Describe the lines\"";

        /// <summary>Undo's tooltip and help text, and the undo's title.</summary>
        public const string UndoCommit = "Undo " + CommitText;

        /// <summary>Redo's tooltip and help text, and the redo's title.</summary>
        public const string RedoCommit = "Redo " + CommitText;

        /// <summary>The undo's running text.</summary>
        public const string UndoingCommit = "Undoing " + CommitText + "…";

        /// <summary>The redo's running text.</summary>
        public const string RedoingCommit = "Redoing " + CommitText + "…";

        /// <summary>The status bar after the undo (secondary).</summary>
        public const string UndidCommit = "Undid " + CommitText;

        /// <summary>The status bar after the redo (secondary).</summary>
        public const string RedidCommit = "Redid " + CommitText;

        /// <summary>HEAD's reflog after the app's commit.</summary>
        public const string CommitReflog = "commit: Describe the lines";

        /// <summary>HEAD's and main's reflog after the undo.</summary>
        public const string UndoReflog = "VisualCommit: undo commit \"Describe the lines\"";

        /// <summary>HEAD's and main's reflog after the redo.</summary>
        public const string RedoReflog = "VisualCommit: redo commit \"Describe the lines\"";

        /// <summary>The undo's transaction (7.3's soft move in 7.5's one transaction), as the call log names it.</summary>
        public const string UndoUpdateRef = "git update-ref -m \"VisualCommit: undo commit \\\"Describe the lines\\\"\" --stdin";

        /// <summary>The redo's transaction.</summary>
        public const string RedoUpdateRef = "git update-ref -m \"VisualCommit: redo commit \\\"Describe the lines\\\"\" --stdin";

        /// <summary>The undo transaction's one line (main back to Read whole lines).</summary>
        public const string UndoInput = "update refs/heads/main " + ReadWholeLinesSha + " " + DescribeTheLinesSha;

        /// <summary>The redo transaction's one line (main forward to Describe the lines).</summary>
        public const string RedoInput = "update refs/heads/main " + DescribeTheLinesSha + " " + ReadWholeLinesSha;

        /// <summary>A4's typing into the summary (AD8): four characters, one text input in the scripted run.</summary>
        public const string TypedSummary = "Next";

        /// <summary>The summary box's placeholder (phase 2).</summary>
        public const string SummaryPlaceholder = "Summary";

        /// <summary>The commit button after A4's undo (one staged file).</summary>
        public const string CommitOneFile = "Commit 1 file";

        /// <summary>A4's untracked file, written before the start, and its text.</summary>
        public const string ScratchPath = "scratch.txt";

        public const string ScratchText = "scratch\n";

        // ---- The toolbar and the status bar (1.1, 1.2, 7.4, phase 3's 4d). ----

        /// <summary>The toolbar's buttons, left to right (phase 0).</summary>
        public static IReadOnlyList<string> ToolbarButtons { get; } =
            ["UndoButton", "RedoButton", "FetchButton", "PullButton", "PushButton", "BranchButton", "StashButton", "PopButton", "SearchButton", "ThemeSwitch"];

        public const string NothingToUndo = "Nothing to undo";

        public const string NothingToRedo = "Nothing to redo";

        /// <summary>7.4's reason 2 while the merge is stopped.</summary>
        public const string FinishMerge = "Finish or abort the merge first";

        /// <summary>7.4's reason 1 while the merge tool runs.</summary>
        public const string WaitForMergeTool = "Wait for Merge tool for notes.txt to finish";

        /// <summary>Search's reason while the resolver is open (11.2).</summary>
        public const string CloseResolverFirst = "Close the conflict resolver first";

        /// <summary>Pop's reason with no stash (phase 3's 4d).</summary>
        public const string NoStashes = "There are no stashes";

        /// <summary>Stash's reason with nothing to stash (phase 3's 4d).</summary>
        public const string NoChangesToStash = "There are no changes to stash";

        /// <summary>Theme's tooltip in the dark theme (phase 0).</summary>
        public const string SwitchToLight = "Switch to the light theme";

        /// <summary>
        /// Search's tooltip and help text while it is enabled (1.1): <c>Search commits (Ctrl+F)</c>,
        /// <c>Cmd+F</c> where the platform's command modifier is Cmd (part A's "Keys").
        /// </summary>
        public static string SearchToolTip(bool commandIsMeta) => $"Search commits ({(commandIsMeta ? "Cmd" : "Ctrl")}+F)";

        /// <summary>A toolbar button as part A asserts it: by its automation id, enabled or not, and its tooltip (null: none).</summary>
        /// <param name="Id">Its automation id (<c>UndoButton</c>, …, <c>ThemeSwitch</c>).</param>
        /// <param name="Enabled">Whether it is enabled.</param>
        /// <param name="ToolTip">Its tooltip, also its help text for Undo, Redo and Search; null for none.</param>
        public sealed record ButtonState(string Id, bool Enabled, string? ToolTip);

        /// <summary>An empty tab's toolbar (A1 steps 1 and 6; phase 0's shell): every button but Theme disabled, none with a tooltip.</summary>
        public static IReadOnlyList<ButtonState> EmptyTabToolbar { get; } =
        [
            .. ToolbarButtons.Take(9).Select(id => new ButtonState(id, false, null)),
            new ButtonState("ThemeSwitch", true, SwitchToLight),
        ];

        /// <summary>"history" open with nothing undone (A1 step 2): Undo and Redo with nothing to act on, Pop with no stash, Search enabled.</summary>
        public static IReadOnlyList<ButtonState> OpenToolbar(bool commandIsMeta) =>
        [
            new("UndoButton", false, NothingToUndo),
            new("RedoButton", false, NothingToRedo),
            new("FetchButton", true, null),
            new("PullButton", true, null),
            new("PushButton", true, null),
            new("BranchButton", true, null),
            new("StashButton", true, null),
            new("PopButton", false, NoStashes),
            new("SearchButton", true, SearchToolTip(commandIsMeta)),
            new("ThemeSwitch", true, SwitchToLight),
        ];

        /// <summary>"history" after A1's commit (A1 steps 4 and 7): Undo the commit, the tree clean, so Stash has nothing to stash.</summary>
        public static IReadOnlyList<ButtonState> CommittedToolbar(bool commandIsMeta) =>
        [
            new("UndoButton", true, UndoCommit),
            new("RedoButton", false, NothingToRedo),
            new("FetchButton", true, null),
            new("PullButton", true, null),
            new("PushButton", true, null),
            new("BranchButton", true, null),
            new("StashButton", false, NoChangesToStash),
            new("PopButton", false, NoStashes),
            new("SearchButton", true, SearchToolTip(commandIsMeta)),
            new("ThemeSwitch", true, SwitchToLight),
        ];

        /// <summary>The Activity log toggle's label (1.2).</summary>
        public const string ActivityLogLabel = "Activity log";

        /// <summary>The toggle's tooltip and help text while the panel is closed.</summary>
        public const string ShowActivityLog = "Show every git command the app has run";

        /// <summary>The toggle's tooltip and help text while the panel is open.</summary>
        public const string HideActivityLog = "Hide the activity log";

        /// <summary>The status bar's branch text on an empty tab.</summary>
        public const string NoRepository = "No repository";

        public const string Ready = "Ready";

        /// <summary>Where the toggle's background is sampled: inside its left padding (part A's conventions).</summary>
        public static readonly Point ToggleSample = new(1008, 678);

        /// <summary>The toggle's place at 1100×700: x 1005 to 1100, y 675 to 700 (±2).</summary>
        public static readonly Rect ToggleBounds = new(1005, 675, 95, 25);

        // ---- The empty tab (phase 0's shell, phase 1's welcome page). ----

        /// <summary>The welcome page of a tab without a repository, in reading order.</summary>
        public static IReadOnlyList<string> WelcomeTexts { get; } =
        [
            "No repository open", "Open, clone or init a repository to see its history.",
            "Open", "Clone", "Init", "Recent repositories", "No recent repositories",
        ];

        /// <summary>The left panel's sections, each with the count 0 on an empty tab.</summary>
        public static IReadOnlyList<string> LeftPanelSections { get; } = ["Local branches", "Remotes", "Pull requests", "Tags", "Stashes"];

        /// <summary>The right panel with nothing selected.</summary>
        public static IReadOnlyList<string> NothingSelectedTexts { get; } = ["Commit details", "Select a commit to see its details."];

        /// <summary>The details panel's placeholder with nothing selected.</summary>
        public const string NothingSelected = "Select a commit to see its details.";

        // ---- The graphs (the report's "Scenarios and graphs of part A"). ----

        /// <summary>The graphs of "history" have two lanes.</summary>
        public const int HistoryLanes = 2;

        /// <summary>"conflicts" has five lanes (no Date column at 1100×700).</summary>
        public const int ConflictsLanes = 5;

        /// <summary>The working-changes row with nothing conflicted.</summary>
        public static WorkingChangesRow Working { get; } = new("Working changes", 0, 0);

        /// <summary>The working-changes row while the merge is stopped.</summary>
        public static WorkingChangesRow WorkingOneConflict { get; } = new("Working changes · 1 conflict", 0, 0);

        /// <summary>H0's rows 1 to 13 (Stop tracing to Initial commit), which H-A1 keeps below its two first rows.</summary>
        private static readonly CommitRow[] HistoryBelow =
        [
            Row("Stop tracing", 0, 0) with { ShortSha = "20d7fbe", Date = "2026-01-01 12:12" },
            Row("Write new notes", 0, 0) with { ShortSha = "e765529", Date = "2026-01-01 12:11" },
            Row("Merge branch 'feature/readme'", 0, 0) with { ShortSha = "f513566", Date = "2026-01-01 12:10" },
            Row("Explain the reader", 1, 1) with { ShortSha = "c1b6375", Date = "2026-01-01 12:09" },
            Row("Remove the notes", 0, 0) with { ShortSha = "884b462", Date = "2026-01-01 12:08", Ends = [1] },
            Row("Compare whole lines", 1, 2, "feature/compare", "origin/feature/compare") with { ShortSha = "245c395", Date = "2026-01-01 12:07" },
            Row("Rename the parser to reader", 0, 0) with { ShortSha = "0575c7f", Date = "2026-01-01 12:06" },
            Row("Add the comparer", 1, 2) with { ShortSha = "d0d3c77", Date = "2026-01-01 12:05" },
            Row("Add tracing", 0, 0) with { ShortSha = "14a5c6c", Date = "2026-01-01 12:04", Ends = [1] },
            Row("Handle empty input", 0, 0) with { ShortSha = "365482b", Date = "2026-01-01 12:03" },
            Row("Add the logo", 0, 0) with { ShortSha = "775113f", Date = "2026-01-01 12:02" },
            Row("Add the parser", 0, 0) with { ShortSha = "1d5a7cf", Date = "2026-01-01 12:01" },
            Row("Initial commit", 0, 0) with { ShortSha = "7829335", Date = "2026-01-01 12:00" },
        ];

        /// <summary>H0, "history" as built: 14 rows, two lanes; the working-changes row above them.</summary>
        public static IReadOnlyList<CommitRow> H0 { get; } =
        [
            Row("Read whole lines", 0, 0, Current("main"), "origin/main") with { ShortSha = "c0bc2dd", Date = "2026-01-01 12:13" },
            .. HistoryBelow,
        ];

        /// <summary>H-A1, "history" after the commit "Describe the lines": 15 rows, two lanes.</summary>
        public static IReadOnlyList<CommitRow> HA1 { get; } =
        [
            Row("Describe the lines", 0, 0, Current("main")) with { ShortSha = "d1e23a3", Date = "2026-01-02 09:00" },
            Row("Read whole lines", 0, 0, "origin/main") with { ShortSha = "c0bc2dd", Date = "2026-01-01 12:13" },
            .. HistoryBelow,
        ];

        /// <summary>"conflicts" for A2: GC0's rows, five lanes, below the working-changes row.</summary>
        public static IReadOnlyList<CommitRow> ConflictsGraph { get; } =
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

        /// <summary>"Kinds edits"'s commit row in "conflicts" (display row 2 under the working-changes row).</summary>
        public const int KindsEditsRow = 1;

        // ---- Geometry at 1100×700 (part A's conventions, AD1, AD9; ±2 where the report gives it). ----

        /// <summary>The graph area: x 260 to 700, y 88 to 674.</summary>
        public static readonly Rect GraphArea = new(260, 88, 440, 586);

        /// <summary>The splitter's height above the panel (2.1).</summary>
        public const double SplitterHeight = 5;

        /// <summary>The panel's header (2.2).</summary>
        public const double HeaderHeight = 30;

        /// <summary>The panel's default height (2.1).</summary>
        public const double DefaultHeight = 200;

        /// <summary>A2's height, from the session (AD7).</summary>
        public const double A2Height = 320;

        /// <summary>A3's height after its first drag, 100 up (AD9).</summary>
        public const double DraggedHeight = 300;

        /// <summary>The panel's maximum at 1100×700: the graph area's 586 less 165 (AD1).</summary>
        public const double MaximumHeight = 421;

        /// <summary>The panel's minimum (2.1).</summary>
        public const double MinimumHeight = 120;

        /// <summary>The tolerance on every height and position of the panel (AD9).</summary>
        public const double Tolerance = 2;

        /// <summary>A3's drags (AD9): 100 up, then after the restart 300 up and 400 down, each in 5 steps.</summary>
        public const double FirstDrag = -100;

        public const double DragToMaximum = -300;

        public const double DragToMinimum = 400;

        public const int DragSteps = 5;

        /// <summary>The panel at height <paramref name="height"/>: x 260 to 700, its bottom on the status bar's top (y 674).</summary>
        public static Rect PanelAt(double height) => new(GraphArea.X, GraphArea.Bottom - height, GraphArea.Width, height);

        /// <summary>The splitter above a panel of height <paramref name="height"/>: the 5 px over it.</summary>
        public static Rect SplitterAt(double height) => new(GraphArea.X, GraphArea.Bottom - height - SplitterHeight, GraphArea.Width, SplitterHeight);

        /// <summary>A row of the log: an operation's 24 high, a line's or a call's 22 (2.2, AD3); the indent per level 24.</summary>
        public const double OperationRowHeight = 24;

        public const double LineRowHeight = 22;

        public const double IndentStep = 24;

        /// <summary>The search bar's height (3.1), by which it moves the column headers and the rows down.</summary>
        public const double SearchBarHeight = 36;

        // ---- The activity log's texts (2.2, 2.3, 12, the report's texts table). ----

        public const string LogTitle = "Activity log";

        public const string ShowReads = "Show reads";

        /// <summary>Every time the frozen record clock gives (2.3).</summary>
        public const string Time = "09:00:00";

        /// <summary>Every duration the frozen record clock gives (2.3).</summary>
        public const string Duration = "0 ms";

        public const string Running = "Running…";

        public const string Exit0 = "exit 0";

        public const string Exit1 = "exit 1";

        public const string Cancelled = "cancelled";

        /// <summary>What a call made on a temporary index shows after its command.</summary>
        public const string OnTemporaryIndex = "(on a temporary index)";

        public const string IconCheck = "IconCheck";

        public const string IconAlert = "IconAlert";

        public const string IconClose = "IconClose";

        public const string IconMinus = "IconMinus";

        public const string ChevronClosed = "IconChevronRight";

        public const string ChevronOpen = "IconChevronDown";

        /// <summary>The output box's part titles (2.2).</summary>
        public const string InputPart = "Input";

        public const string OutputPart = "Output";

        public const string ErrorPart = "Error";

        // ---- The backups' clean-up at opening ("A2's backups", 2.3, phase 4's 9). ----

        public const string CleanUpTitle = "Clean up backups";

        public const string CleanUpMessage = "Removed 3 backups";

        /// <summary>The clean-up's notes at opening, in order.</summary>
        public static IReadOnlyList<string> CleanUpNotes { get; } =
        [
            "Removed backup reset-20251120T090001000Z (older than 30 days)",
            "Removed backup branch-20251120T090002000Z (older than 30 days)",
            "Removed backup reset-20251220T090001000Z (more than 50 backups)",
        ];

        /// <summary>The read that finds the backups due, and the clean-up's first call.</summary>
        public const string BackupsForEachRef = "git for-each-ref \"--format=%(refname) %(objectname)\" refs/visualcommit/backup/";

        /// <summary>The clean-up's transaction, and the abort's backup.</summary>
        public const string UpdateRefStdin = "git update-ref --stdin";

        /// <summary>The clean-up's transaction's input at opening: three deletes.</summary>
        public static IReadOnlyList<string> CleanUpInput { get; } =
        [
            "delete refs/visualcommit/backup/reset-20251120T090001000Z " + OursEditsSha,
            "delete refs/visualcommit/backup/branch-20251120T090002000Z/old-idea " + OursEditsSha,
            "delete refs/visualcommit/backup/reset-20251220T090001000Z " + OursEditsSha,
        ];

        /// <summary>The log file's lines of the clean-up at opening (phase 4's 9), in order.</summary>
        public static IReadOnlyList<string> CleanUpLogLines => CleanUpNotes;

        /// <summary>The 50 backups left after the opening's clean-up, oldest first: reset-20251220T090002000Z to reset-20251220T090051000Z.</summary>
        public static IReadOnlyList<string> BackupsAfterOpening { get; } =
            [.. Enumerable.Range(2, 50).Select(second => string.Create(CultureInfo.InvariantCulture, $"reset-20251220T0900{second:00}000Z"))];

        /// <summary>The opening's read that comes first under "Show reads" (AD11 as ruled, D183).</summary>
        public const string OpeningRevParse = "git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir";

        /// <summary>The read of git's version, made with no working folder: never shown.</summary>
        public const string GitVersionCall = "git --version";

        // ---- The refused checkout (A2 step 3; 9.3, phase 3's 2.1, D153). ----

        public const string CheckoutTitle = "Check out feature/kinds";

        public const string CheckoutFailed = "Check out feature/kinds failed";

        public const string CheckoutCommand = "git switch --no-guess feature/kinds";

        public const string CheckoutBlocked = "Checkout blocked";

        public const string CheckoutSummary = "1 conflict prevents checkout. Commit, stash, or discard your changes first.";

        public static IReadOnlyList<string> CheckoutButtons { get; } = ["Copy output", "Stash changes and check out", "Close"];

        /// <summary>Git's refusal on standard error (2.36 and 2.55 alike); its first line classifies it.</summary>
        public static IReadOnlyList<string> CheckoutError { get; } =
        [
            "error: Your local changes to the following files would be overwritten by checkout:",
            "\tother.txt",
            "Please commit your changes or stash them before you switch branches.",
            "Aborting",
        ];

        // ---- The merge (A2 step 7; phase 3's 2.8, phase 4). ----

        public const string MergeMenuItem = "Merge feature/theirs into main";

        public const string MergeTitle = "Merge feature/theirs";

        public const string MergeRunning = "Merging feature/theirs into main…";

        public const string MergeStopped = "Merge stopped: 1 file has conflicts";

        public const string MergeCommand = "git merge --no-edit --no-log -m \"Merge branch 'feature/theirs'\" refs/heads/feature/theirs";

        /// <summary>Git's lines on standard output (2.36 and 2.55 alike); the second classifies the stop.</summary>
        public static IReadOnlyList<string> MergeOutput { get; } =
        [
            "Auto-merging notes.txt",
            "CONFLICT (content): Merge conflict in notes.txt",
            "Automatic merge failed; fix conflicts and then commit the result.",
        ];

        public const string MergeKeyLine = "CONFLICT (content): Merge conflict in notes.txt";

        public const string MergeMessage = "Merge branch 'feature/theirs'";

        public const string MergeBanner = "Merging: \"Merge branch 'feature/theirs'\". 1 file has conflicts.";

        public const string ContinueReason = "Resolve the conflicts first (1 file)";

        public const string AbortMerge = "Abort merge";

        /// <summary>The stage panel while the merge is stopped.</summary>
        public const string UnstagedWithConflict = "Unstaged files (2, 1 with conflicts)";

        public static IReadOnlyList<string> UnstagedAtStop { get; } = ["U notes.txt", "M other.txt"];

        // ---- The merge tool (A2 steps 8 and 9; phase 4's 11.9). ----

        public const string OpenInMergeTool = "Open in merge tool";

        public const string MergeToolTitle = "Merge tool for notes.txt";

        public const string MergeToolRunning = "Waiting for slow to close…";

        public const string CancelToolTip = "Stop git and the merge tool";

        public const string MergeToolCancelled = "Merge tool for notes.txt cancelled";

        public const string LsFilesNotes = "git ls-files --unmerged -z -- notes.txt";

        public const string MergeToolCommand = "git -c mergetool.keepBackup=false mergetool --tool=slow --no-prompt -- notes.txt";

        /// <summary>The bound on the wait after the click on Cancel (AD7).</summary>
        public static readonly TimeSpan CancelBound = TimeSpan.FromSeconds(30);

        // ---- The abort (A2 steps 10 and 11; phase 3's 2.15, phase 4, D165). ----

        public const string AbortQuestionTitle = "Abort the merge?";

        public const string AbortQuestion = "Abort merging \"Merge branch 'feature/theirs'\"? Changes made to resolve conflicts are lost.";

        public const string AbortDetail = "A backup is saved first, so this can be undone.";

        public const string AbortTitle = "Abort the merge";

        public const string AbortRunning = "Aborting the merge…";

        public const string Aborted = "Aborted the merge";

        /// <summary>The abort's backup (the backup clock's first name of the session).</summary>
        public const string AbortBackup = "abort-20260102T090001000Z";

        /// <summary>The abort's snapshot's tree: Ours edits' files with notes.txt as the merge left it and other.txt as changed.</summary>
        public const string AbortSnapshotTree = "912dd413f445e484334bad13e5f44a294ebc2dfc";

        /// <summary>The note of the abort's clean-up.</summary>
        public const string AbortNote = "Removed backup reset-20251220T090002000Z (more than 50 backups)";

        /// <summary>The abort's calls the report names, in this order (the commit-tree's tree is the snapshot's).</summary>
        public static IReadOnlyList<string> AbortCalls { get; } =
        [
            "git read-tree " + OursEditsSha,
            "git add --all --force --pathspec-from-file=- --pathspec-file-nul",
            "git write-tree",
            "git commit-tree " + AbortSnapshotTree + " -p " + OursEditsSha,
            UpdateRefStdin,
            "git merge --abort",
            BackupsForEachRef,
            UpdateRefStdin,
        ];

        /// <summary>Which of <see cref="AbortCalls"/> run on a temporary index.</summary>
        public static IReadOnlyList<bool> AbortCallsOnTemporaryIndex { get; } = [true, true, true, false, false, false, false, false];

        /// <summary>The abort backup's transaction's input (the snapshot's id read from git).</summary>
        public static IReadOnlyList<string> AbortBackupInput(string snapshot) =>
        [
            "create refs/visualcommit/backup/" + AbortBackup + " " + OursEditsSha,
            "create refs/visualcommit/backup/" + AbortBackup + "-changes " + snapshot,
        ];

        /// <summary>The 50 backups after the abort: reset-20251220T090003000Z to reset-20251220T090051000Z and the abort's (51 refs).</summary>
        public static IReadOnlyList<string> BackupsAfterAbort { get; } = [AbortBackup, .. BackupsAfterOpening.Skip(1)];

        /// <summary>other.txt as A2 changes it before the start, kept by the merge and the abort.</summary>
        public const string OtherChanged = "other file\nchanged on main\nchanged here\n";

        // ---- The backups list (A2 step 5; 5.1, 5.3, 12.3). ----

        public const string ShowBackups = "Show backups";

        public const string BackupsSource = "Backups";

        /// <summary>The backups list's first row: the newest filler.</summary>
        public const string NewestBackup = "reset-20251220T090051000Z";

        public const string NewestBackupTime = "2025-12-20 09:00:51";

        /// <summary>The list's last row: the oldest left.</summary>
        public const string OldestBackup = "reset-20251220T090002000Z";

        public const string BeforeAReset = "Before a reset";

        public const string OursEditsShort = "da31c97";

        public const string OursEditsSubject = "Ours edits";

        // ---- The search bar (A4 step 1; 3.1). ----

        public const string SearchMode = "Commits";

        public const string SearchPlaceholder = "Message, author or SHA";

        public const string PreviousMatch = "Previous match (Shift+Enter)";

        public const string NextMatch = "Next match (Enter)";

        public const string CloseSearch = "Close the search (Esc)";

        // ---- The resolver (A4 method 2; phase 4's FM, R1 and 11.5). ----

        /// <summary>R1: FM after Use ours on block 1: 16 lines, one block left.</summary>
        public static string NotesR1 { get; } = string.Join(
            '\n',
            "line 1", "line 2 ours", "line 3", "line 4", "line 5", "line 6 theirs", "line 7", "line 8", "line 9",
            "<<<<<<< HEAD", "line 10 ours", "=======", "line 10 theirs", ">>>>>>> feature/theirs",
            "line 11", "line 12") + "\n";

        public const string OneConflictLeft = "1 conflict left";

        public const string TwoConflictsLeft = "2 conflicts left";

        /// <summary>notes.txt's blob at the terminal's merge stop (FM).</summary>
        public const string NotesBlobAtStop = "cdd7b39";

        public const string NotesPath = "notes.txt";

        // ---- Pictures (AD13, the Screenshots column). ----

        public const string A1EmptyTab = "a1a-empty-tab";

        public const string A1RepositoryOpen = "a1b-repository-open";

        public const string A1UndoTooltipNothing = "a1c-undo-tooltip-nothing";

        public const string A1SearchTooltip = "a1d-search-tooltip";

        public const string A1Committed = "a1e-committed";

        public const string A1UndoTooltip = "a1f-undo-tooltip";

        public const string A2LogOpen = "a2a-log-open";

        public const string A2CleanUpOpened = "a2b-clean-up-opened";

        public const string A2CheckoutFailedOpened = "a2c-checkout-failed-opened";

        public const string A2ShowReads = "a2d-show-reads";

        public const string A2PageAndLog = "a2e-page-and-log";

        public const string A2LogClosedPageKept = "a2f-log-closed-page-kept";

        public const string A2MergeOutputOpened = "a2g-merge-output-opened";

        public const string A2MergeToolRunning = "a2h-merge-tool-running";

        public const string A2MergeToolCancelled = "a2i-merge-tool-cancelled";

        public const string A2AbortOpened = "a2j-abort-opened";

        public const string A2AbortBackupInput = "a2k-abort-backup-input";

        public const string A3DefaultHeight = "a3a-default-height";

        public const string A3DraggedTo300 = "a3b-dragged-to-300";

        public const string A3Restarted = "a3c-restarted";

        public const string A3AtTheMaximum = "a3d-at-the-maximum";

        public const string A3AtTheMinimum = "a3e-at-the-minimum";

        public const string A4SearchByKey = "a4a-search-by-key";

        public const string A4SummaryTypingUndone = "a4b-summary-typing-undone";

        public const string A4UndoneByKey = "a4c-undone-by-key";

        public const string A4RedoneByKey = "a4d-redone-by-key";

        public const string A4ResolverUndo = "a4e-resolver-undo";

        /// <summary>Every picture of part A's scripted run, in the order the checks take them.</summary>
        public static IReadOnlyList<string> Pictures { get; } =
        [
            A1EmptyTab, A1RepositoryOpen, A1UndoTooltipNothing, A1SearchTooltip, A1Committed, A1UndoTooltip,
            A2LogOpen, A2CleanUpOpened, A2CheckoutFailedOpened, A2ShowReads, A2PageAndLog, A2LogClosedPageKept,
            A2MergeOutputOpened, A2MergeToolRunning, A2MergeToolCancelled, A2AbortOpened, A2AbortBackupInput,
            A3DefaultHeight, A3DraggedTo300, A3Restarted, A3AtTheMaximum, A3AtTheMinimum,
            A4SearchByKey, A4SummaryTypingUndone, A4UndoneByKey, A4RedoneByKey, A4ResolverUndo,
        ];
    }
}
