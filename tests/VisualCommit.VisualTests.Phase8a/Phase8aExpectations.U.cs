namespace VisualCommit.VisualTests.Phase8a;

public static partial class Phase8aExpectations
{
    /// <summary>
    /// Part U, screen readers (docs/test-reports/phase-8a.md, "Part U"; docs/phase-8a-design.md 1.12,
    /// 9.3.5, 9.3.6, 9.5, 12.3.4 launch 13): the graph's exposed row as a client reads it on the
    /// "ops" scenario and on the stopped merge, its names and hints word for word; the names of
    /// 1.12's table with the tooltips, help texts and keys they keep (their Windows words); the
    /// scenarios' extra state; the sweep's states; the picture names; and for the real window's
    /// launch 13 its scenario, session, the untracked file, the rows it reads, the names it reads
    /// and the time it waits for each event. Written from the report, not from the app's texts. The
    /// real-window pass links this file (design 0.3): it uses the BCL only.
    /// </summary>
    public static class U
    {
        // ---- The ids and the graph's peer (9.3.2, 9.3.3) -------------------------------------

        public const string GraphRowsId = "GraphRows";
        public const string SelectedRowId = "GraphSelectedRow";
        public const string SelectedRowClass = "CommitGraphRow";
        public const string GraphName = "Commit graph";
        public const string RefItemId = "RefItem";
        public const string RefListId = "RefList";
        public const string UnstagedListId = "UnstagedFileList";
        public const string StagedListId = "StagedFileList";

        /// <summary>A display row's height, which the exposed row's bounds have (<c>WindowLayout.RowHeight</c>).</summary>
        public const double RowHeight = 26;

        // ---- U1 on "ops" (method 1): the exposed row after each step -------------------------

        /// <summary>"Main work", HEAD's commit, display row 4 with README.md changed.</summary>
        public static Row MainWork { get; } = new("Main work", 4, "Main work, Test Author, 2026-01-01 12:04, current branch main, remote branch origin/main, tag v1.1", "Commit 88d6b91");

        public static Row AlphaTwo { get; } = new("Alpha two", 5, "Alpha two, Test Author, 2026-01-01 12:03, branch feature/alpha, remote branch origin/feature/alpha", "Commit ce5ea6a");

        public static Row AlphaOne { get; } = new("Alpha one", 6, "Alpha one, Test Author, 2026-01-01 12:02", "Commit dff8fbf");

        public static Row FastForwardMe { get; } = new("Fast-forward me", 3, "Fast-forward me, Test Author, 2026-01-01 12:05, branch feature/ff", "Commit d93814d");

        public static Row ConflictingChange { get; } = new("Conflicting change", 2, "Conflicting change, Test Author, 2026-01-01 12:06, branch feature/conflict", "Commit 078ef05");

        /// <summary>The stash's row: <c>Stash: {subject}, {date}</c>, its label; its hint the stash commit's short id (open point 78).</summary>
        public static Row Stash { get; } = new("On main: Experiment", 1, "Stash: On main: Experiment, 2026-01-01 12:07, stash@{0}", "Commit 0e95aca");

        /// <summary>The working-changes row: its text, no hint.</summary>
        public static Row WorkingRow { get; } = new("Working changes", 0, "Working changes", string.Empty);

        /// <summary>The hint of Alpha two as the focus row of two selected commits (step 6's first Shift+Down).</summary>
        public const string AlphaTwoOfTwo = "Commit ce5ea6a, 2 commits selected";

        /// <summary>The hint of Alpha one as the focus row of three selected commits (step 6's second Shift+Down).</summary>
        public const string AlphaOneOfThree = "Commit dff8fbf, 3 commits selected";

        /// <summary>
        /// Steps 4 to 7 of method 1 in order: the key (<c>Down</c>, <c>Up</c>, <c>Shift+Down</c>), the
        /// exposed row, its hint, and how many commits are selected after it.
        /// </summary>
        public static IReadOnlyList<Move> OpsMoves { get; } =
        [
            new("Down", AlphaTwo, AlphaTwo.Hint, 1),
            new("Up", MainWork, MainWork.Hint, 1),
            new("Shift+Down", AlphaTwo, AlphaTwoOfTwo, 2),
            new("Shift+Down", AlphaOne, AlphaOneOfThree, 3),
            new("Up", AlphaTwo, AlphaTwo.Hint, 1),
            new("Up", MainWork, MainWork.Hint, 1),
            new("Up", FastForwardMe, FastForwardMe.Hint, 1),
            new("Up", ConflictingChange, ConflictingChange.Hint, 1),
            new("Up", Stash, Stash.Hint, 1),
            new("Up", WorkingRow, WorkingRow.Hint, 0),
        ];

        /// <summary>
        /// The first name change's old value after the peer is made (open point 77): null. The peer
        /// has raised no name when step 4's Down moves the row, and the old value is the last name
        /// raised; from the second change on it is the name the change before raised.
        /// </summary>
        public const string? FirstOldName = null;

        // ---- U1 on the stopped merge (method 2; launch 13's rows) ----------------------------

        /// <summary>"Ours edits", HEAD's commit, display row 1 below the banner.</summary>
        public static Row OursEdits { get; } = new("Ours edits", 1, "Ours edits, Test Author, 2026-01-01 12:09, current branch main", "Commit da31c97");

        public static Row KindsEdits { get; } = new("Kinds edits", 2, "Kinds edits, Test Author, 2026-01-01 12:08, branch feature/kinds", "Commit 3a9de3e");

        /// <summary>The working-changes row of the stopped merge: <c>Working changes · 1 conflict</c>, read with <c>, </c>.</summary>
        public static Row ConflictWorkingRow { get; } = new("Working changes · 1 conflict", 0, "Working changes, 1 conflict", string.Empty);

        /// <summary>Method 2's step 3 and launch 13's moves from "Ours edits", in order: Up, Down, Down.</summary>
        public static IReadOnlyList<Move> MergeMoves { get; } =
        [
            new("Up", ConflictWorkingRow, ConflictWorkingRow.Hint, 0),
            new("Down", OursEdits, OursEdits.Hint, 1),
            new("Down", KindsEdits, KindsEdits.Hint, 1),
        ];

        // ---- The scenarios' extra state (open point 79) --------------------------------------

        public const string Readme = "README.md";

        /// <summary>README.md as methods 1 and 3 write it in the copy's work tree before the start.</summary>
        public const string ReadmeChanged = "# Operations\n\nA change for the screen readers' checks.\n";

        /// <summary>README.md as "ops" commits it, which method 3's discard brings back.</summary>
        public const string ReadmeCommitted = "# Operations\n";

        public const string Core = "src/core.txt";

        /// <summary>src/core.txt as method 3 writes and stages it before the start.</summary>
        public const string CoreChanged = "core\nmain work\nstaged for the names\n";

        /// <summary>The untracked file methods 2 and 3 and launch 13 write before the start.</summary>
        public const string Untracked = "u-untracked.txt";

        public const string UntrackedContent = "An untracked file whose row's buttons are named.\n";

        /// <summary>The stopped merge's conflicted file.</summary>
        public const string Notes = "notes.txt";

        // ---- The routes' texts (the report's "Texts these checks rely on") -------------------

        public const string LocalBranches = "Local branches";
        public const string Remotes = "Remotes";
        public const string Origin = "origin";
        public const string Main = "main";
        public const string Feature = "feature";
        public const string Alpha = "alpha";
        public const string Conflict = "conflict";
        public const string Ff = "ff";
        public const string HideFeatureAlpha = "Hide feature/alpha";
        public const string SoloFeatureFf = "Solo feature/ff";
        public const string CompareConflictWithMain = "Compare feature/conflict with main";
        public const string CommitsMode = "Commits";
        public const string ThreeCommitsSelected = "3 commits selected";
        public const string Unstaged1 = "Unstaged files (1)";
        public const string Unstaged2 = "Unstaged files (2)";
        public const string Staged0 = "Staged files (0)";
        public const string Staged1 = "Staged files (1)";
        public const string ConflictToolTip = "Conflict: click to resolve it";
        public const string DiscardTitle = "Discard changes?";
        public const string DiscardButton = "Discard";
        public const string RestoreText = "Discarded changes to README.md.";
        public const string Restore = "Restore";
        public const string BackupRefPrefix = "refs/visualcommit/backup/";

        // ---- U3: the names of 1.12's table and what they keep --------------------------------

        public const string PreviousConflict = "Previous conflict";
        public const string NextConflict = "Next conflict";
        public const string PreviousFile = "Previous file";
        public const string NextFile = "Next file";
        public const string CloseResolver = "Close the resolver";
        public const string CloseDiff = "Close the diff";
        public const string PreviousMatch = "Previous match";
        public const string NextMatch = "Next match";
        public const string CloseSearch = "Close the search";
        public const string CloseActivityLog = "Close the activity log";
        public const string CloseComparison = "Close the comparison";
        public const string SwapFromAndTo = "Swap From and To";
        public const string DismissRestoreOffer = "Dismiss the restore offer";
        public const string ActivityLog = "Activity log";
        public const string ResizeLeftPanel = "Resize the left panel";
        public const string ResizeRightPanel = "Resize the right panel";
        public const string ResizeActivityLog = "Resize the activity log";

        public const string StageFileFormat = "Stage {0}";
        public const string DiscardFileFormat = "Discard {0}";
        public const string UnstageFileFormat = "Unstage {0}";
        public const string ShowRefFormat = "Show {0} in the graph";
        public const string StopSoloingRefFormat = "Stop soloing {0}";

        public const string StageReadme = "Stage README.md";
        public const string DiscardReadme = "Discard README.md";
        public const string StageUntracked = "Stage u-untracked.txt";
        public const string DiscardUntracked = "Discard u-untracked.txt";
        public const string UnstageCore = "Unstage src/core.txt";
        public const string UnstageUntracked = "Unstage u-untracked.txt";
        public const string ShowFeatureAlpha = "Show feature/alpha in the graph";
        public const string ShowOriginFeatureAlpha = "Show origin/feature/alpha in the graph";
        public const string StopSoloingFeatureFf = "Stop soloing feature/ff";

        /// <summary>
        /// U3's table: each control's id, its name, its tooltip (Windows words; null for none), how
        /// its help text is given and its accelerator key (Windows words; null for none). The rows'
        /// buttons and the eyes are in <see cref="RowNames"/>.
        /// </summary>
        public static IReadOnlyList<Named> FixedNames { get; } =
        [
            new("ResolverPreviousConflict", PreviousConflict, "Previous conflict (Alt+Up)", HelpTextSource.ToolTip, "Alt+Up"),
            new("ResolverNextConflict", NextConflict, "Next conflict (Alt+Down)", HelpTextSource.ToolTip, "Alt+Down"),
            new("ResolverPreviousFile", PreviousFile, "Previous file (Alt+Left)", HelpTextSource.ToolTip, "Alt+Left"),
            new("ResolverNextFile", NextFile, "Next file (Alt+Right)", HelpTextSource.ToolTip, "Alt+Right"),
            new("ResolverCloseButton", CloseResolver, "Close (Esc)", HelpTextSource.ToolTip, "Esc"),
            new("SearchPrevious", PreviousMatch, "Previous match (Shift+Enter, Shift+F3)", HelpTextSource.ToolTip, "Shift+F3"),
            new("SearchNext", NextMatch, "Next match (Enter, F3)", HelpTextSource.ToolTip, "F3"),
            new("SearchClose", CloseSearch, "Close the search (Esc)", HelpTextSource.ToolTip, null),
            new("CompareSwap", SwapFromAndTo, "Swap From and To", HelpTextSource.ToolTip, null),
            new("CompareClose", CloseComparison, "Close the comparison", HelpTextSource.ToolTip, null),
            new("CloseDiffButton", CloseDiff, "Close", HelpTextSource.PeerFallback, null),
            new("ActivityLogToggle", ActivityLog, "Hide the activity log (Ctrl+J)", HelpTextSource.ToolTip, "Ctrl+J"),
            new("ActivityLogClose", CloseActivityLog, "Close the activity log", HelpTextSource.PeerFallback, null),
            new("LeftSplitter", ResizeLeftPanel, null, HelpTextSource.None, null),
            new("RightSplitter", ResizeRightPanel, null, HelpTextSource.None, null),
            new("ActivityLogSplitter", ResizeActivityLog, null, HelpTextSource.None, null),
            new("DismissRestoreButton", DismissRestoreOffer, "Close", HelpTextSource.PeerFallback, null),
        ];

        /// <summary>The resolver's five (method 2's step 5; launch 13).</summary>
        public static IReadOnlyList<string> ResolverIds { get; } =
            ["ResolverPreviousConflict", "ResolverNextConflict", "ResolverPreviousFile", "ResolverNextFile", "ResolverCloseButton"];

        /// <summary>The search bar's three (method 3's step 2; launch 13).</summary>
        public static IReadOnlyList<string> SearchIds { get; } = ["SearchPrevious", "SearchNext", "SearchClose"];

        /// <summary>The comparison's two (method 3's step 3).</summary>
        public static IReadOnlyList<string> CompareIds { get; } = ["CompareSwap", "CompareClose"];

        /// <summary>The activity log's two and the three splitters (method 3's step 8; launch 13).</summary>
        public static IReadOnlyList<string> LogAndSplitterIds { get; } =
            ["ActivityLogToggle", "ActivityLogClose", "LeftSplitter", "RightSplitter", "ActivityLogSplitter"];

        /// <summary>
        /// The rows' buttons and the branch toggles by their row (U3's table): the list or the ref's
        /// full name, the id, the name, the tooltip; each gives its tooltip as its help text by the
        /// peer's fallback and has no key.
        /// </summary>
        public static IReadOnlyList<RowNamed> RowNames { get; } =
        [
            new(UnstagedListId, Readme, "RowStageButton", StageReadme, "Stage"),
            new(UnstagedListId, Readme, "RowDiscardButton", DiscardReadme, "Discard"),
            new(UnstagedListId, Untracked, "RowStageButton", StageUntracked, "Stage"),
            new(UnstagedListId, Untracked, "RowDiscardButton", DiscardUntracked, "Discard"),
            new(StagedListId, Core, "RowUnstageButton", UnstageCore, "Unstage"),
            new(RefListId, "feature/alpha", "RefHiddenToggle", ShowFeatureAlpha, "Hidden in the graph. Click to show it"),
            new(RefListId, "origin/feature/alpha", "RefHiddenToggle", ShowOriginFeatureAlpha, "Hidden in the graph. Click to show it"),
            new(RefListId, "feature/ff", "RefSoloToggle", StopSoloingFeatureFf, "Soloed. Click to stop soloing it"),
        ];

        // ---- The sweep's states ---------------------------------------------------------------

        public const string SweepOpsTab = "U2: the ops tab";
        public const string SweepResolver = "U3: the resolver";
        public const string SweepSoloed = "U3: the ops tab, one soloed";
        public const string SweepRestoreBar = "U3: the restore bar";

        // ---- The pictures (the report's Pictures column) -------------------------------------

        public const string U1aGraphRowHead = "u1a-graph-row-head";
        public const string U1bGraphRowsThreeSelected = "u1b-graph-rows-three-selected";
        public const string U1cGraphWorkingRow = "u1c-graph-working-row";
        public const string U2aSweepOpsTab = "u2a-sweep-ops-tab";
        public const string U3aResolverNames = "u3a-resolver-names";
        public const string U3bStageRowNames = "u3b-stage-row-names";

        public static IReadOnlyList<string> Pictures { get; } =
            [U1aGraphRowHead, U1bGraphRowsThreeSelected, U1cGraphWorkingRow, U2aSweepOpsTab, U3aResolverNames, U3bStageRowNames];

        // ---- The real window's launch 13 (design 12.3.4; the report's "The real window (part U)") --

        /// <summary>
        /// Launch 13, <c>Check_U_graph_row_and_names</c>: a copy of
        /// <c>Scenarios.ConflictsStoppedAsync(ConflictsStop.Merge)</c> with <see cref="Untracked"/>
        /// written into its work tree, one tab, dark, 1100×700, no capture. Method 2 is its scripted
        /// counterpart (no twin).
        /// </summary>
        public static Launch GraphRowLaunch { get; } = new(
            "Check_U_graph_row_and_names",
            "Check_U1_U3_stopped_merge_rows_and_resolver",
            "ConflictsStoppedAsync(ConflictsStop.Merge)",
            Dark: true,
            Size: (1100, 700),
            Untracked,
            UntrackedContent);

        /// <summary>The longest launch 13 waits for a live region change from the row after a key (D445, D450).</summary>
        public static readonly TimeSpan EventWait = TimeSpan.FromSeconds(5);

        /// <summary>The names launch 13 reads after the rows, in its order, each by its id (the rows' buttons by their row, <see cref="LaunchRowNames"/>).</summary>
        public static IReadOnlyList<string> LaunchNameIds { get; } =
        [
            "ResolverPreviousConflict", "ResolverNextConflict", "ResolverPreviousFile", "ResolverNextFile", "ResolverCloseButton",
            "ActivityLogToggle", "ActivityLogClose", "LeftSplitter", "RightSplitter", "ActivityLogSplitter",
            "SearchPrevious", "SearchNext", "SearchClose",
        ];

        /// <summary>The untracked file's row buttons launch 13 reads: Stage and Discard while unstaged, then Unstage once staged.</summary>
        public static IReadOnlyList<RowNamed> LaunchRowNames { get; } =
        [
            new(UnstagedListId, Untracked, "RowStageButton", StageUntracked, "Stage"),
            new(UnstagedListId, Untracked, "RowDiscardButton", DiscardUntracked, "Discard"),
            new(StagedListId, Untracked, "RowUnstageButton", UnstageUntracked, "Unstage"),
        ];

        /// <summary>A display row the graph exposes: its subject (or the row's text), its place among the display rows, and what UI Automation reads.</summary>
        public sealed record Row(string Subject, int DisplayRow, string Name, string Hint);

        /// <summary>A key and what the exposed row is after it, with how many commits are then selected (0 on the working-changes row).</summary>
        public sealed record Move(string Key, Row Row, string Hint, int Selected);

        /// <summary>A control of 1.12's table, found by its id.</summary>
        public sealed record Named(string Id, string Name, string? ToolTip, HelpTextSource HelpText, string? Key);

        /// <summary>A control of a row (a file row's button or a branch row's toggle), found by its id within its row.</summary>
        public sealed record RowNamed(string List, string Row, string Id, string Name, string ToolTip);

        /// <summary>A real-window launch: its method, its scripted counterpart, the scenario, the session and the file written into the copy.</summary>
        public sealed record Launch(string Method, string Counterpart, string Scenario, bool Dark, (int Width, int Height) Size, string File, string FileContent);

        /// <summary>
        /// How a control gives its help text (U3's table): <c>AutomationProperties.HelpText</c> set to
        /// the tooltip; no help text of its own, the peer giving the tooltip by its fallback; or none.
        /// </summary>
        public enum HelpTextSource
        {
            ToolTip,
            PeerFallback,
            None,
        }
    }
}
