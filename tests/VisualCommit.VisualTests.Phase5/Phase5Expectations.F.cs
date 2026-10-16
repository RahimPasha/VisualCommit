// What part F of phase 5's walk-through compares against (docs/test-reports/phase-5.md, "Part F:
// search, compare, solo and hide, and dashed stashes": its conventions, "Scenarios and graphs of
// part F", "Texts these checks rely on (part F)", the rows F10 to F15 of "Checks" and decisions FD1
// to FD21): copied from the report, not read from the app. Data only: the real-window pass compiles
// this file too, so it uses nothing of the app and no headless type.
using Avalonia.Media;

namespace VisualCommit.VisualTests.Phase5;

public static partial class Phase5Expectations
{
    /// <summary>The expected results of part F's checks F10 to F15.</summary>
    public static class PartF
    {
        // ---- The pictures (the Screenshots column) ----------------------------------------------

        public const string F10BarOpen = "f10a-bar-open";
        public const string F10Tokenizer = "f10b-tokenizer";
        public const string F10FirstMatch = "f10c-first-match";
        public const string F10Previous = "f10d-previous";
        public const string F10Author = "f10e-author";
        public const string F10Sha = "f10f-sha";
        public const string F10Closed = "f10g-closed";
        public const string F10LightWide = "f10h-light-wide";
        public const string F11ModeMenu = "f11a-mode-menu";
        public const string F11FilesParser = "f11b-files-parser";
        public const string F11FilesReader = "f11c-files-reader";
        public const string F11NoMatches = "f11d-no-matches";
        public const string F11Changes = "f11e-changes";
        public const string F11AfterRefresh = "f11f-after-refresh";
        public const string F12TwoCommits = "f12a-two-commits";
        public const string F12FileDiff = "f12b-file-diff";
        public const string F12Swapped = "f12c-swapped";
        public const string F12CompareFileMenu = "f12d-compare-file-menu";
        public const string F13BranchMenu = "f13a-branch-menu";
        public const string F13ThreeDot = "f13b-three-dot";
        public const string F13MergeBaseDiff = "f13c-merge-base-diff";
        public const string F13AllDifferences = "f13d-all-differences";
        public const string F13DraftKept = "f13e-draft-kept";
        public const string F13DropMenu = "f13f-drop-menu";
        public const string F13DropCompare = "f13g-drop-compare";
        public const string F14FoldersOpen = "f14a-filters-folders-open";
        public const string F14CurrentBranchMenu = "f14b-current-branch-menu";
        public const string F14AlphaMenu = "f14c-alpha-menu";
        public const string F14AlphaHidden = "f14d-alpha-hidden";
        public const string F14HiddenRowTooltip = "f14e-hidden-row-tooltip";
        public const string F14CompareHidden = "f14f-compare-hidden";
        public const string F14AlphaShown = "f14g-alpha-shown";
        public const string F14BetaSoloed = "f14h-beta-soloed";
        public const string F14SectionMenu = "f14i-section-menu";
        public const string F14AfterRestart = "f14j-after-restart";
        public const string F14ShownByToggle = "f14k-shown-by-toggle";
        public const string F15GraphStash = "f15a-graph-stash";
        public const string F15FiltersStashes = "f15b-filters-stashes";

        // ---- "history" and H0 ------------------------------------------------------------------

        /// <summary>The history scenario's folder and tab.</summary>
        public const string HistoryRepo = "history";

        /// <summary>H0's lanes: the Graph column 48 wide.</summary>
        public const int HistoryLanes = 2;

        /// <summary>The working-changes row of "history": its ring in lane 0, colour 0.</summary>
        public static WorkingChangesRow Working { get; } = new("Working changes", 0, 0);

        /// <summary>The authors as the details panel shows them.</summary>
        public const string TestAuthor = "Test Author <author@example.com>";
        public const string Jonas = "Jonas Berg <jonas@example.com>";
        public const string Mira = "Mira Kovac <mira@example.com>";

        // The full SHAs of H0's rows (the scenario's pins).
        public const string ReadWholeLines = "c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9";
        public const string StopTracing = "20d7fbe203a4a8ceeb5ef00da13ceda91ed70739";
        public const string WriteNewNotes = "e765529af7bef5e017fb41b2de5e384d1eebc858";
        public const string MergeReadme = "f5135668320ac7e48c60dd634a18f4eb3906f160";
        public const string ExplainTheReader = "c1b63754e14cab0e146b0811ca11371ba2bbce55";
        public const string RemoveTheNotes = "884b4622b22f86236cbaae78b04234e63b4a6a88";
        public const string CompareWholeLines = "245c395674db6ae89390be2db018db5356b446fe";
        public const string RenameParser = "0575c7f6631b3ba6c9d623f53b28489405230cfd";
        public const string AddTheComparer = "d0d3c77aa36bc7ad3ef71cff57e454453d4463b2";
        public const string AddTracing = "14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c";
        public const string HandleEmptyInput = "365482be62488b4c1259f0fb7dfa1631c797470b";
        public const string AddTheLogo = "775113f4ae456d7dc7dc5380c669b0d2950289e2";
        public const string AddTheParser = "1d5a7cf999727e7f724054348c81866a62025184";
        public const string InitialCommit = "7829335c71bb8f1161b668a29afb602632228425";

        /// <summary>H0, "history" as built (2 lanes; the working-changes row above row 0).</summary>
        public static IReadOnlyList<CommitRow> H0 { get; } =
        [
            Row("Read whole lines", 0, 0, Current("main"), "origin/main") with { ShortSha = "c0bc2dd", Date = "2026-01-01 12:13" },
            Row("Stop tracing", 0, 0) with { ShortSha = "20d7fbe", Date = "2026-01-01 12:12" },
            Row("Write new notes", 0, 0) with { ShortSha = "e765529", Date = "2026-01-01 12:11" },
            Row("Merge branch 'feature/readme'", 0, 0) with { ShortSha = "f513566", Merge = true, Date = "2026-01-01 12:10" },
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

        // H0's rows by name.
        public const int ReadWholeLinesRow = 0;
        public const int StopTracingRow = 1;
        public const int MergeReadmeRow = 3;
        public const int ExplainTheReaderRow = 4;
        public const int CompareWholeLinesRow = 6;
        public const int RenameParserRow = 7;
        public const int AddTheComparerRow = 8;
        public const int AddTracingRow = 9;
        public const int HandleEmptyInputRow = 10;

        /// <summary>H-F1's new commit, "Trace again" (F11 step 6, FD8): its SHA, its date, its file.</summary>
        public const string TraceAgain = "f612569a47849c0326a3022a420c241d56d7ddde";
        public const string TraceAgainSubject = "Trace again";
        public const string TraceAgainPath = "src/Config.cs";

        /// <summary>The copy's clock when F11's commit is made outside the app: 2026-01-01 12:14 UTC.</summary>
        public static DateTimeOffset TraceAgainTime { get; } = new(2026, 1, 1, 12, 14, 0, TimeSpan.Zero);

        /// <summary>H-F1, "history" after "Trace again": the new row 0 with main ✓, then H0's rows with origin/main left at Read whole lines.</summary>
        public static IReadOnlyList<CommitRow> HF1 { get; } =
        [
            Row("Trace again", 0, 0, Current("main")) with { ShortSha = "f612569", Date = "2026-01-01 12:14" },
            H0[0] with { Labels = ["origin/main"] },
            .. H0.Skip(1),
        ];

        /// <summary><c>git status --porcelain</c> of "history" throughout part F.</summary>
        public static IReadOnlyList<string> HistoryStatus { get; } = [" M src/Reader.cs"];

        /// <summary><c>git status -sb</c> after "Trace again".</summary>
        public static IReadOnlyList<string> TraceAgainStatusShort { get; } = ["## main...origin/main [ahead 1]", " M src/Reader.cs"];

        // ---- Geometry (the conventions of part F; derived from design 3.1 for the bar, FD18) -------

        /// <summary>The search bar's height and top (under the toolbar, which ends at y 88).</summary>
        public const double BarTop = 88;
        public const double BarHeight = 36;

        /// <summary>The bar's parts: 8 from its edges and 6 apart; previous, next and close 22 wide; the box at least 120 wide.</summary>
        public const double BarInset = 8;
        public const double BarGap = 6;
        public const double BarIconButtonWidth = 22;
        public const double BarBoxMinWidth = 120;

        /// <summary>The graph control's top: y 116 with the bar closed, 152 with it open; the column headers 28 above it.</summary>
        public const double RowsTopClosed = 116;
        public const double RowsTopOpen = 152;

        /// <summary>A position the report states: asserted ±2 (the conventions of every part).</summary>
        public const double PositionTolerance = 2;

        /// <summary>A sampled pixel: each channel ±3 (the conventions of part F).</summary>
        public const int ChannelTolerance = 3;

        /// <summary>The working-changes row's centre: y 129 with the bar closed, 165 with it open.</summary>
        public static double WorkingCentre(bool barOpen) => barOpen ? 165 : 129;

        /// <summary>Commit row <paramref name="row"/>'s centre in "history" (the working-changes row above it): 155 + 26 r closed, 191 + 26 r open.</summary>
        public static double HistoryRowCentre(int row, bool barOpen) => (barOpen ? 191 : 155) + (26 * row);

        /// <summary>Commit row <paramref name="row"/>'s centre with no working-changes row and the bar closed: 129 + 26 r.</summary>
        public static double RowCentre(int row) => 129 + (26 * row);

        /// <summary>Lane <paramref name="lane"/>'s centre: x 406 + 16 L.</summary>
        public static double LaneX(int lane) => 406 + (16 * lane);

        /// <summary>A sampled point: (x, y) in the window.</summary>
        public readonly record struct Sample(int X, int Y);

        // ---- The search bar (design 3.1, the texts of part F) -----------------------------------

        public const string CommitsMode = "Commits";
        public const string FilesMode = "Files";
        public const string ChangesMode = "Changes";
        public const string CommitsPlaceholder = "Message, author or SHA";
        public const string FilesPlaceholder = "File name or path (case ignored for A–Z)";
        public const string ChangesPlaceholder = "Text added or removed (case ignored for A–Z)";
        public const string PreviousToolTip = "Previous match (Shift+Enter)";
        public const string NextToolTip = "Next match (Enter)";
        public const string CloseToolTip = "Close the search (Esc)";
        public const string NoMatches = "No matches";

        /// <summary>The mode menu (FD5): 110 high, 3 items, no separator; Commits highlighted.</summary>
        public static string[] ModeMenu { get; } =
        [
            "Search",
            "Commits: message, author or SHA",
            "Files: a path or file name",
            "Changes: text added or removed",
        ];

        public const string FilesItem = "Files: a path or file name";
        public const string ChangesItem = "Changes: text added or removed";
        public const double ModeMenuHeight = 110;

        // FD7's texts.
        public const string Tokenizer = "tokenizer";
        public const string JonasText = "Jonas";
        public const string ShaText = "0575";
        public const string ParserText = "parser";
        public const string ReaderText = "reader";
        public const string TraceEverything = "TraceEverything";

        /// <summary>The Commits mode's git part (3.2) for <paramref name="text"/>, as the call log writes it.</summary>
        public static string CommitsCall(string text) =>
            "git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%H -F --regexp-ignore-case --grep=" + text + " --";

        /// <summary>The Files mode's call (3.2) for <paramref name="text"/>, run without <c>GIT_LITERAL_PATHSPECS</c> (0.3).</summary>
        public static string FilesCall(string text) =>
            "git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%x1e%H --first-parent --root --no-renames --name-only -- "
            + $":(glob,icase)**/*{text}* :(glob,icase)**/*{text}*/**";

        /// <summary>The Changes mode's call (3.2) for <paramref name="text"/>.</summary>
        public static string ChangesCall(string text) =>
            "git log --no-walk=unsorted --stdin --no-show-signature --no-color --encoding=UTF-8 --format=%H --first-parent --diff-merges=first-parent --root --find-renames --no-textconv -s --regexp-ignore-case -S" + text + " --";

        /// <summary>What every search call starts with (to count them).</summary>
        public const string SearchCallStart = "git log --no-walk=unsorted ";

        /// <summary>What part F's searches match (the table "What part F's searches match").</summary>
        public static IReadOnlyList<int> TokenizerMatches { get; } = [CompareWholeLinesRow, HandleEmptyInputRow];
        public static IReadOnlyList<int> JonasMatches { get; } = [0, 4, 6, 8, 10];
        public static IReadOnlyList<int> ShaMatches { get; } = [RenameParserRow];
        public static IReadOnlyList<int> ParserMatches { get; } = [7, 10, 12];
        public static IReadOnlyList<int> ReaderMatches { get; } = [0, 7];
        public static IReadOnlyList<int> ChangesMatches { get; } = [StopTracingRow, AddTracingRow];

        /// <summary>The Changes matches over H-F1: Trace again, Stop tracing, Add tracing.</summary>
        public static IReadOnlyList<int> ChangesMatchesAfterRefresh { get; } = [0, 2, 10];

        /// <summary>The rows of 0 to <paramref name="count"/> − 1 that are not in <paramref name="marked"/>: the dimmed ones.</summary>
        public static IReadOnlyList<int> Others(int count, IReadOnlyList<int> marked) => [.. Enumerable.Range(0, count).Except(marked)];

        // F10's samples at 1100×700 (and 1920×1080, the same positions), the bar open.

        /// <summary>The bar's background and its bottom line.</summary>
        public static Sample BarChromeAt { get; } = new(262, 90);
        public static Sample BarBorderAt { get; } = new(262, 123);

        /// <summary>Just above the bar's 1-px bottom line: its background.</summary>
        public static Sample BarAboveBorderAt { get; } = new(262, 122);

        /// <summary>The marks of rows 6 and 10 over the Branch / Tag column's first 3 px.</summary>
        public static Sample MarkRow6At { get; } = new(261, 347);
        public static Sample MarkRow10At { get; } = new(261, 451);

        /// <summary>Nodes sampled 3 px right of their centre (FD6), the bar open.</summary>
        public static Sample Row0NodeAt { get; } = new(409, 191);
        public static Sample Row4NodeAt { get; } = new(425, 295);
        public static Sample Row8NodeAt { get; } = new(425, 399);
        public static Sample Row6NodeAt { get; } = new(425, 347);
        public static Sample Row10NodeAt { get; } = new(409, 451);

        /// <summary>Lane 0's line in row 4, passing through a dimmed row at full strength.</summary>
        public static Sample Lane0InRow4At { get; } = new(406, 295);

        /// <summary>Row 0's node with the bar closed.</summary>
        public static Sample Row0NodeClosedAt { get; } = new(409, 155);

        /// <summary>Dimmed nodes: 35 % over the window's background (FD6).</summary>
        public static Color DimmedColour0Dark { get; } = Color.Parse("#383F6B");
        public static Color DimmedColour1Dark { get; } = Color.Parse("#234F3E");
        public static Color DimmedColour2Dark { get; } = Color.Parse("#5B4726");
        public static Color DimmedColour0Light { get; } = Color.Parse("#BDC3F1");

        /// <summary>H0's Message column at 1100×700, with the bar open as closed.</summary>
        public const double MessageWidth = 142;

        /// <summary>The bar's right edge at 1100×700 less 8: where the close button ends.</summary>
        public const double CloseRightAt1100 = 692;

        // ---- The details panel ------------------------------------------------------------------

        /// <summary>
        /// A commit in the details panel as the report gives it: its subject, its body (null for
        /// none), the author line, the date, the full SHA, the parents' short ids, and the changed
        /// files as "letter name [folder] [renamed from …]".
        /// </summary>
        public sealed record Details(string Subject, string? Body, string Author, string Date, string Sha, IReadOnlyList<string> Parents, IReadOnlyList<string> Files);

        public static Details CompareWholeLinesDetails { get; } = new(
            "Compare whole lines", "It splits the texts with the tokenizer first.", Jonas, "2026-01-01 12:07", CompareWholeLines, ["d0d3c77"], ["M Comparer.cs src"]);

        public static Details HandleEmptyInputDetails { get; } = new(
            "Handle empty input", "The tokenizer stops at an empty text now.", Jonas, "2026-01-01 12:03", HandleEmptyInput, ["775113f"], ["M Parser.cs src"]);

        public static Details RenameParserDetails { get; } = new(
            "Rename the parser to reader", null, Mira, "2026-01-01 12:06", RenameParser, ["14a5c6c"], ["R Reader.cs src renamed from src/Parser.cs"]);

        public static Details MergeReadmeDetails { get; } = new(
            "Merge branch 'feature/readme'", null, TestAuthor, "2026-01-01 12:10", MergeReadme, ["884b462", "c1b6375"], ["M README.md"]);

        public static Details ReadWholeLinesDetails { get; } = new(
            "Read whole lines", null, Jonas, "2026-01-01 12:13", ReadWholeLines, ["20d7fbe"], ["M Reader.cs src"]);

        public const string NothingSelected = "Select a commit to see its details.";

        // ---- Compare (design 6, the texts of part F) --------------------------------------------

        public const string CompareTitle = "Compare";
        public const string SwapToolTip = "Swap From and To";
        public const string CompareCloseToolTip = "Close the comparison";
        public const string NotInGraph = "This commit is not in the graph";
        public const string AllDifferences = "All differences";

        /// <summary>A comparison's file list as the report writes it: "letter name [folder] [renamed from …]", flat, in git's order.</summary>
        public static IReadOnlyList<string> F12Files { get; } = ["M README.md", "M notes.md docs", "M Config.cs src", "M Reader.cs src"];

        public const string F12From = "From 0575c7f Rename the parser to reader";
        public const string F12To = "To c0bc2dd Read whole lines";
        public const string F12SwappedFrom = "From c0bc2dd Read whole lines";
        public const string F12SwappedTo = "To 0575c7f Rename the parser to reader";

        /// <summary>The calls of F12's comparison, From first (FD20).</summary>
        public static string RevParseCall(string from, string to) => $"git rev-parse {from}^{{commit}} {to}^{{commit}} --";

        public static string DiffTreeCall(string old, string to) => $"git diff-tree -r -z --name-status -M --no-ext-diff --no-color --no-commit-id {old} {to}";

        public static string MergeBaseCall(string from, string to) => $"git merge-base --all {from} {to}";

        public static string RevListCall(string from, string to) => $"git rev-list --left-right --count {from}...{to} --";

        /// <summary>The comparison's file list header: <c>Changed files (N)</c>.</summary>
        public static string ChangedFiles(int count) => $"Changed files ({count})";

        /// <summary>src/Reader.cs from 0575c7f to c0bc2dd (db5a108 → 7061deb): one hunk, rows as "K old/new text", the texts as git has them.</summary>
        public static IReadOnlyList<string> ReaderDiffRows { get; } =
        [
            "@@ -10,13 +10,13 @@ public sealed class Reader",
            "C 10/10         _text = text ?? string.Empty;",
            "C 11/11     }",
            "C 12/12 ",
            "R 13/-     public string[] Words()",
            "A -/13     public string[] Lines()",
            "C 14/14     {",
            "C 15/15         if (_text.Length == 0)",
            "C 16/16         {",
            "C 17/17             return [];",
            "C 18/18         }",
            "C 19/19 ",
            "R 20/-         return _text.Split(' ');",
            "A -/20         return _text.Split('\\n');",
            "C 21/21     }",
            "C 22/22 }",
        ];

        public const string ReaderSideLabel = "0575c7f..c0bc2dd";

        /// <summary>The comparison's file menu on Reader.cs (FD21): 171 high, 5 items, 1 separator.</summary>
        public static string[] CompareFileMenu { get; } = ["Reader.cs", "File history", "Blame", "—", "Open file", "Show in folder", "Copy path"];
        public const double CompareFileMenuHeight = 171;

        /// <summary>F12 step 6: phase 3's list of three selected commits.</summary>
        public static string[] ThreeSelected { get; } = ["c0bc2dd Read whole lines", "f513566 Merge branch 'feature/readme'", "0575c7f Rename the parser to reader"];

        public const string MultiSelectionHint = "Right-click a selected commit for what can be done with all of them.";

        // F13.

        public const string DraftSummary = "Keep this draft";
        public const string DraftCounter = "57";

        /// <summary>The stage panel of "history" (F13 steps 1 and 7).</summary>
        public static IReadOnlyList<string> UnstagedReader { get; } = ["M Reader.cs src"];

        /// <summary>The menu of Local branches › feature › compare (F13 step 2): 571 high, 19 items, 5 separators.</summary>
        public static string[] FeatureCompareMenu { get; } =
        [
            "feature/compare",
            "Check out feature/compare",
            "Merge feature/compare into main",
            "Merge feature/compare into main (create a merge commit)",
            "Rebase main onto feature/compare",
            "Interactive rebase of main onto feature/compare…",
            "Fast-forward feature/compare to origin/feature/compare (disabled: \"feature/compare is up to date with origin/feature/compare\")",
            "—",
            "Push feature/compare to origin/feature/compare",
            "Force push feature/compare… (danger)",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename feature/compare…",
            "Delete feature/compare… (danger)",
            "—",
            "Change upstream…",
            "Stop tracking origin/feature/compare",
            "—",
            "Compare feature/compare with main",
            "Show reflog of feature/compare",
            "Hide feature/compare",
            "Solo feature/compare",
            "—",
            "Copy branch name",
        ];

        public const double BranchMenuHeight = 571;
        public const string CompareFeatureWithMain = "Compare feature/compare with main";

        public const string MainRef = "refs/heads/main";
        public const string FeatureCompareRef = "refs/heads/feature/compare";

        public const string F13From = "From main";
        public const string F13To = "To feature/compare";
        public const string F13Counts = "2 commits only on feature/compare, 7 only on main";
        public static IReadOnlyList<string> F13ThreeDotFiles { get; } = ["M README.md", "A Comparer.cs src"];
        public static IReadOnlyList<string> F13AllFiles { get; } =
            ["M README.md", "M notes.md docs", "A Comparer.cs src", "M Config.cs src", "R Parser.cs src renamed from src/Reader.cs"];

        /// <summary>README.md from the merge base 14a5c6c to 245c395 (30ac432 → f950130).</summary>
        public static IReadOnlyList<string> ReadmeDiffRows { get; } =
        [
            "@@ -1,3 +1,5 @@",
            "C 1/1 # History",
            "C 2/2 ",
            "C 3/3 A repository for the history checks.",
            "A -/4 ",
            "A -/5 Compare two texts with Comparer.",
        ];

        public const string ReadmeSideLabel = "14a5c6c..245c395";

        /// <summary>The drop of main onto feature/compare (FD11): its menu, 180 high, 5 items, 2 separators.</summary>
        public static string[] DropMenu { get; } =
        [
            "main → feature/compare",
            "Fast-forward feature/compare to main (disabled: \"feature/compare has commits that main does not have\")",
            "Check out feature/compare and merge main into it",
            "—",
            "Rebase main onto feature/compare",
            "Interactive rebase of main onto feature/compare…",
            "—",
            "Compare main with feature/compare",
        ];

        public const double DropMenuHeight = 180;
        public const string DropHighlighted = "Check out feature/compare and merge main into it";
        public const string CompareMainWithFeature = "Compare main with feature/compare";
        public const string DropAncestorCall = "git merge-base --is-ancestor refs/heads/feature/compare refs/heads/main";

        /// <summary>The drag's moves (phase 3's check 7).</summary>
        public const int DragSteps = 5;

        public const string F13DropFrom = "From feature/compare";
        public const string F13DropTo = "To main";
        public const string F13DropCounts = "7 commits only on main, 2 only on feature/compare";
        public static IReadOnlyList<string> F13DropFiles { get; } =
            ["M README.md", "M notes.md docs", "M Config.cs src", "R Reader.cs src renamed from src/Parser.cs"];

        /// <summary>What <c>rev-list --left-right --count</c> prints: <c>7⇥2</c> and the drop's <c>2⇥7</c>.</summary>
        public const string F13CountsOutput = "7\t2";
        public const string F13DropCountsOutput = "2\t7";

        // ---- "filters", FL0, FL1, FL2 ----------------------------------------------------------

        /// <summary>The filters scenario's folder and tab.</summary>
        public const string FiltersRepo = "filters";

        public const int FiltersLanes = 5;
        public const int FilteredLanes = 3;

        public const string AlphaStash = "d3f16e894a9e233889e358ddb6bef18db6f42a35";
        public const string AlphaOne = "7c60fc46a4178e4f2f88df0f73c4b51808be230a";
        public const string FiltersMerge = "4c0aa3c354e1cda550d852d90b110d93df6b08cf";
        public const string AddTheCore = "369f471b9087f5d12eb03bd8f5ebee5d2ae6d9b8";

        /// <summary>FL0, "filters" as built: 15 rows, 5 lanes, no Date column at 1100×700.</summary>
        public static IReadOnlyList<CommitRow> FL0 { get; } =
        [
            Row("On feature/alpha: Alpha experiment", 0, 0, "stash@{0}") with { ShortSha = "d3f16e8", Stash = true },
            Row("On feature/beta: Beta experiment", 1, 1, "stash@{1}") with { ShortSha = "c8f3db3", Stash = true },
            Row("Beta six", 1, 1, "feature/beta", "origin/feature/beta") with { ShortSha = "89f891e" },
            Row("Beta five", 1, 1) with { ShortSha = "ac357ab" },
            Row("Alpha two", 2, 2, "origin/feature/alpha") with { ShortSha = "e4d9976" },
            Row("Beta four", 1, 1) with { ShortSha = "1cdde63" },
            Row("Merge branch 'feature/merged'", 3, 3, Current("main"), "origin/main") with { ShortSha = "4c0aa3c", Merge = true },
            Row("Main work", 3, 3) with { ShortSha = "ef7bfda" },
            Row("Beta three", 1, 1) with { ShortSha = "0c13077" },
            Row("Merged work", 4, 4, "feature/merged") with { ShortSha = "629bc39" },
            Row("Beta two", 1, 1) with { ShortSha = "8fc77ea" },
            Row("Alpha one", 0, 0, "feature/alpha") with { ShortSha = "7c60fc4", Ends = [2] },
            Row("Beta one", 1, 1) with { ShortSha = "2f6e977" },
            Row("Add the core", 0, 0, "v1") with { ShortSha = "369f471", Ends = [1, 3, 4] },
            Row("Initial commit", 0, 0) with { ShortSha = "67e5696" },
        ];

        /// <summary>FL1, "Hide feature/alpha" with its upstream: 12 rows, 3 lanes, the Date column back.</summary>
        public static IReadOnlyList<CommitRow> FL1 { get; } =
        [
            Row("On feature/beta: Beta experiment", 0, 0, "stash@{1}") with { ShortSha = "c8f3db3", Stash = true },
            Row("Beta six", 0, 0, "feature/beta", "origin/feature/beta") with { ShortSha = "89f891e" },
            Row("Beta five", 0, 0) with { ShortSha = "ac357ab" },
            Row("Beta four", 0, 0) with { ShortSha = "1cdde63" },
            Row("Merge branch 'feature/merged'", 1, 1, Current("main"), "origin/main") with { ShortSha = "4c0aa3c", Merge = true },
            Row("Main work", 1, 1) with { ShortSha = "ef7bfda" },
            Row("Beta three", 0, 0) with { ShortSha = "0c13077" },
            Row("Merged work", 2, 2, "feature/merged") with { ShortSha = "629bc39" },
            Row("Beta two", 0, 0) with { ShortSha = "8fc77ea" },
            Row("Beta one", 0, 0) with { ShortSha = "2f6e977" },
            Row("Add the core", 0, 0, "v1") with { ShortSha = "369f471", Ends = [1, 2] },
            Row("Initial commit", 0, 0) with { ShortSha = "67e5696" },
        ];

        /// <summary>FL2, "Solo feature/beta": FL1's rows with row 4's label main ✓ alone and row 7 without one.</summary>
        public static IReadOnlyList<CommitRow> FL2 { get; } =
        [
            .. FL1.Take(4),
            FL1[4] with { Labels = [Current("main")] },
            .. FL1.Skip(5).Take(2),
            FL1[7] with { Labels = [] },
            .. FL1.Skip(8),
        ];

        /// <summary>
        /// The left panel of "filters" in part F's notation (a section <c>▾ Title N</c>, a row
        /// indented two spaces per level, a container with ▾ or ▸, then <c> (head)</c>,
        /// <c> ↑N</c>, <c> ↓N</c>, <c> (dimmed)</c>, <c> [eye]</c> or <c> [solo]</c>), with both
        /// <c>feature</c> folders opened: FL0's.
        /// </summary>
        public static IReadOnlyList<string> FL0LeftPanel { get; } =
        [
            "▾ Local branches 4",
            "  ▾ feature",
            "    alpha ↓1",
            "    beta",
            "    merged",
            "  main (head)",
            "▾ Remotes 3",
            "  ▾ origin",
            "    ▾ feature",
            "      alpha",
            "      beta",
            "    main",
            "▸ Pull requests 0",
            "▾ Tags 1",
            "  v1",
            "▾ Stashes 2",
            "  On feature/alpha: Alpha experiment",
            "  On feature/beta: Beta experiment",
        ];

        /// <summary>The left panel with feature/alpha and its upstream hidden (F14 step 3): both alpha rows in the disabled colour with the eye.</summary>
        public static IReadOnlyList<string> FL1LeftPanel { get; } =
        [
            .. FL0LeftPanel.Take(2),
            "    alpha ↓1 (dimmed) [eye]",
            .. FL0LeftPanel.Skip(3).Take(6),
            "      alpha (dimmed) [eye]",
            .. FL0LeftPanel.Skip(10),
        ];

        /// <summary>The left panel with feature/beta and its upstream soloed (F14 step 7, 8.3).</summary>
        public static IReadOnlyList<string> FL2LeftPanel { get; } =
        [
            .. FL0LeftPanel.Take(2),
            "    alpha ↓1 (dimmed)",
            "    beta [solo]",
            "    merged (dimmed)",
            "  main (head)",
            .. FL0LeftPanel.Skip(6).Take(3),
            "      alpha (dimmed)",
            "      beta [solo]",
            "    main (dimmed)",
            .. FL0LeftPanel.Skip(12),
        ];

        /// <summary>The menu of Local branches › main, the checked-out branch (F14 step 2): 441 high, 14 items, 5 separators.</summary>
        public static string[] MainMenu { get; } =
        [
            "main",
            "Pull from origin/main",
            "Pull from origin/main (rebase)",
            "—",
            "Push main to origin/main",
            "Force push main… (danger)",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename main…",
            "Delete main… (disabled: \"You cannot delete the branch you are on\")",
            "—",
            "Change upstream…",
            "Stop tracking origin/main",
            "—",
            "Show reflog of main",
            "Hide main (disabled: \"The checked-out branch is always shown\")",
            "Solo main",
            "—",
            "Copy branch name",
        ];

        public const double MainMenuHeight = 441;
        public const string HideMain = "Hide main";
        public const string CheckedOutAlwaysShown = "The checked-out branch is always shown";
        public const string PullFromOriginMain = "Pull from origin/main";

        /// <summary>The menu of a feature branch of "filters" (F14 steps 3, 5, 7): 571 high, 19 items, 5 separators.</summary>
        /// <param name="name">alpha or beta.</param>
        /// <param name="hidden">Whether it is hidden: Show in place of Hide.</param>
        public static string[] FeatureMenu(string name, bool hidden)
        {
            var branch = "feature/" + name;
            var fastForward = name == "alpha"
                ? $"Fast-forward {branch} to origin/{branch}"
                : $"Fast-forward {branch} to origin/{branch} (disabled: \"{branch} is up to date with origin/{branch}\")";
            return
            [
                branch,
                $"Check out {branch}",
                $"Merge {branch} into main",
                $"Merge {branch} into main (create a merge commit)",
                $"Rebase main onto {branch}",
                $"Interactive rebase of main onto {branch}…",
                fastForward,
                "—",
                $"Push {branch} to origin/{branch}",
                $"Force push {branch}… (danger)",
                "—",
                "Create branch here…",
                "Create tag here…",
                $"Rename {branch}…",
                $"Delete {branch}… (danger)",
                "—",
                "Change upstream…",
                $"Stop tracking origin/{branch}",
                "—",
                $"Compare {branch} with main",
                $"Show reflog of {branch}",
                hidden ? $"Show {branch}" : $"Hide {branch}",
                $"Solo {branch}",
                "—",
                "Copy branch name",
            ];
        }

        public const string HideAlpha = "Hide feature/alpha";
        public const string ShowAlpha = "Show feature/alpha";
        public const string SoloBeta = "Solo feature/beta";
        public const string CompareAlphaWithMain = "Compare feature/alpha with main";
        public const string StopSoloingItem = "Stop soloing";

        /// <summary>The Local branches section's menu (F14 step 8): 162 high, 5 items, no separator.</summary>
        public static string[] SectionMenu(bool soloed) =>
        [
            "Local branches",
            "Create branch…",
            "Show reflog",
            "Show backups",
            "Show all hidden branches (disabled: \"No branch is hidden\")",
            soloed ? "Stop soloing" : "Stop soloing (disabled: \"No branch is soloed\")",
        ];

        public const double SectionMenuHeight = 162;

        public const string HiddenNameToolTip = "feature/alpha is hidden in the graph";
        public const string HiddenToggleToolTip = "Hidden in the graph. Click to show it";
        public const string SoloToggleToolTip = "Soloed. Click to stop soloing it";

        /// <summary>The eye and solo buttons: 18 by 18 (F14 step 3).</summary>
        public const double ToggleSize = 18;

        /// <summary>The graph's load as the call log writes it, with the filter's <paramref name="revisions"/> (8.2).</summary>
        public static string GraphLogCall(string revisions) =>
            "git log --date-order --format=%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%s --no-show-signature --no-color --encoding=UTF-8 " + revisions;

        /// <summary>The revisions of FL1 (8.2's hide form; origin/HEAD too, 0.6).</summary>
        public const string HideRevisions = "--exclude=feature/alpha --branches --exclude=origin/feature/alpha --exclude=origin/HEAD --remotes --tags --ignore-missing HEAD --";

        /// <summary>The revisions of FL2 (8.2's solo form).</summary>
        public const string SoloRevisions = "--ignore-missing refs/heads/feature/beta refs/remotes/origin/feature/beta HEAD --";

        /// <summary>The revisions without a filter.</summary>
        public const string UnfilteredRevisions = "--branches --remotes --tags --ignore-missing HEAD --";

        /// <summary>What the graph's load starts with (to find it among the calls).</summary>
        public const string GraphLogStart = "git log --date-order ";

        public const string AlphaRef = "refs/heads/feature/alpha";
        public const string OriginAlphaRef = "refs/remotes/origin/feature/alpha";
        public const string BetaRef = "refs/heads/feature/beta";
        public const string OriginBetaRef = "refs/remotes/origin/feature/beta";

        /// <summary>F14's comparison of the hidden feature/alpha with main (FD16).</summary>
        public const string F14From = "From main";
        public const string F14To = "To feature/alpha";
        public const string F14Counts = "1 commit only on feature/alpha, 3 only on main";
        public static IReadOnlyList<string> F14Files { get; } = ["A alpha.txt src"];
        public const string F14CountsOutput = "3\t1";

        // ---- GS0, phase 1's "graph" scenario ----------------------------------------------------

        public const int GraphLanes = 3;

        /// <summary>GS0: phase 1's 13 rows (its report's table), the stash at row 0.</summary>
        public static IReadOnlyList<CommitRow> GS0 { get; } =
        [
            Row("On main: Work in progress on README", 0, 0, "stash@{0}") with { ShortSha = "95daa96", Stash = true, Date = "2026-01-01 12:12" },
            Row("Highlight matches", 1, 1, "feature/search") with { ShortSha = "f463b85", Date = "2026-01-01 12:11" },
            Row("Add settings page", 0, 0, Current("main")) with { ShortSha = "47e6ec7", Date = "2026-01-01 12:10" },
            Row("Fix typo in docs", 2, 2, "origin/main") with { ShortSha = "974bd83", Date = "2026-01-01 12:09" },
            Row("Bump version", 0, 0, "v0.2") with { ShortSha = "2775228", Date = "2026-01-01 12:08" },
            Row("Fix crash on start", 2, 3, "bugfix/crash-on-start") with { ShortSha = "72267a3", Date = "2026-01-01 12:07" },
            Row("Add search box", 1, 1, "origin/feature/search") with { ShortSha = "6aab2e4", Date = "2026-01-01 12:06" },
            Row("Merge branch 'feature/login'", 0, 0) with { ShortSha = "f7919b8", Merge = true, Date = "2026-01-01 12:05" },
            Row("Update README", 0, 0) with { ShortSha = "10bcd42", Date = "2026-01-01 12:04" },
            Row("Validate passwords", 1, 4, "feature/login") with { ShortSha = "4a62ef9", Date = "2026-01-01 12:03" },
            Row("Add login form", 1, 4) with { ShortSha = "7b230d3", Date = "2026-01-01 12:02" },
            Row("Add app skeleton", 0, 0, "v0.1") with { ShortSha = "933250d", Date = "2026-01-01 12:01" },
            Row("Initial commit", 0, 0) with { ShortSha = "53347b3", Date = "2026-01-01 12:00" },
        ];

        /// <summary>A line of the layout as the report names it: the row, its kind (OutOfNode, PassThrough, IntoNode), from lane, to lane.</summary>
        public readonly record struct LayoutLine(int Row, string Kind, int From, int To);

        /// <summary>GS0's dashed lines: lane 0 from the stash's ring into Add settings page's node.</summary>
        public static IReadOnlyList<LayoutLine> GS0Dashed { get; } =
        [
            new(0, "OutOfNode", 0, 0),
            new(1, "PassThrough", 0, 0),
            new(2, "IntoNode", 0, 0),
        ];

        /// <summary>FL0's dashed lines: lane 0 from row 0's ring to Alpha one (row 11), lane 1 from row 1's ring to Beta six (row 2).</summary>
        public static IReadOnlyList<LayoutLine> FL0Dashed { get; } =
        [
            new(0, "OutOfNode", 0, 0),
            .. Enumerable.Range(1, 10).Select(row => new LayoutLine(row, "PassThrough", 0, 0)),
            new(11, "IntoNode", 0, 0),
            new(1, "OutOfNode", 1, 1),
            new(2, "IntoNode", 1, 1),
        ];

        /// <summary>A stretch of a lane's centre the report samples: x, from y to y (both included), the lane colour, and how many dashes at least (0 for a solid stretch).</summary>
        public readonly record struct Stretch(int X, int Top, int Bottom, int Colour, int MinimumDashes, string What);

        public static IReadOnlyList<Stretch> GS0Stretches { get; } =
        [
            new(406, 136, 175, 0, 5, "the stash's line from its ring into Add settings page"),
            new(406, 187, 219, 0, 0, "lane 0 below Add settings page"),
            new(422, 161, 219, 1, 0, "lane 1 below Highlight matches"),
        ];

        public static IReadOnlyList<Stretch> FL0Stretches { get; } =
        [
            new(406, 136, 404, 0, 40, "the alpha stash's line across rows 1 to 10"),
            new(422, 162, 175, 1, 2, "the beta stash's line into Beta six"),
            new(422, 187, 201, 1, 0, "lane 1 from Beta six to Beta five"),
            new(406, 421, 452, 0, 0, "lane 0 below Alpha one"),
        ];

        /// <summary>The rings: GS0's at (406, 129) in colour 0; FL0's at (406, 129) in colour 0 and (422, 155) in colour 1.</summary>
        public static IReadOnlyList<(Sample At, int Colour)> FL0Rings { get; } = [(new(406, 129), 0), (new(422, 155), 1)];

        public static Sample GS0RingAt { get; } = new(406, 129);
    }
}
