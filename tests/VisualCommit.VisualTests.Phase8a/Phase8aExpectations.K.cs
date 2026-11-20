using Avalonia.Media;
using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase8a;

public static partial class Phase8aExpectations
{
    /// <summary>
    /// What part K's checks compare against (docs/test-reports/phase-8a.md, "Part K: the keyboard
    /// pass and the focus fixes"; docs/phase-8a-design.md 6.5): the texts the steps pass through,
    /// word for word, the ids, the picture names, the key counts of K1's and K2's routes, K1's
    /// <c>--numstat</c> lines and the index's file after the hunk and after the two lines, K2's file
    /// and its count of Down to run 2, the colours a check reads, and for the real window's two
    /// launches (design 12.3.4 launches 11 and 12) each launch's scenario, starting theme, size and
    /// captures. Written from the report, not from the app.
    /// </summary>
    public static class K
    {
        // ---- The scenarios' files and rows (the conventions' "Scenarios and sessions") ---------------

        /// <summary>"changes"'s unstaged rows, in the flat list's order.</summary>
        public static IReadOnlyList<string> UnstagedRows { get; } =
            ["README.md", "assets/logo.png", "data/blob.bin", "data/large.txt", "docs/guide.md", "docs/old-notes.txt", "src/Calculator.cs"];

        /// <summary>"changes"'s staged rows, in the flat list's order.</summary>
        public static IReadOnlyList<string> StagedRows { get; } = ["README.md", "config/settings.json", "src/helpers.py"];

        public const string Readme = "README.md";
        public const string Logo = "assets/logo.png";
        public const string Calculator = "src/Calculator.cs";
        public const string CalculatorName = "Calculator.cs";
        public const string CalculatorFolder = "src";
        public const string Reader = "src/Reader.cs";

        // ---- K1: the walk, the keys, git's values -------------------------------------------------

        /// <summary>Tabs from the unstaged list's first row's own button to <c>src/Calculator.cs</c>'s: each row's own button, its Stage, its Discard (6.3.4).</summary>
        public const int TabsToCalculator = 18;

        /// <summary>The Downs after Ctrl+Home that bring the caret to row 4 of the remaining hunk (its header row 0).</summary>
        public const int DownsToLines = 4;

        /// <summary>The Shift+Downs that select rows 4 and 5 of the remaining hunk.</summary>
        public const int ShiftDownsForLines = 2;

        /// <summary>The most Tabs, and the most Shift+Tabs, of K1's scripted-only step 11, each followed by a wait for the focused id.</summary>
        public const int ModeWalkLimit = 4;

        /// <summary><c>git diff --cached --numstat -- src/Calculator.cs</c> after the hunk, its two tabs written as single spaces.</summary>
        public const string StagedAfterHunk = "1 1 src/Calculator.cs";

        /// <summary><c>git diff --numstat -- src/Calculator.cs</c> after the hunk.</summary>
        public const string UnstagedAfterHunk = "4 0 src/Calculator.cs";

        /// <summary><c>git diff --cached --numstat -- src/Calculator.cs</c> after the two lines.</summary>
        public const string StagedAfterLines = "3 1 src/Calculator.cs";

        /// <summary><c>git diff --numstat -- src/Calculator.cs</c> after the two lines.</summary>
        public const string UnstagedAfterLines = "2 0 src/Calculator.cs";

        /// <summary>The index's <c>src/Calculator.cs</c> after the hunk: the committed file with <c>Add</c>'s changed line and no other.</summary>
        public static string IndexAfterHunk { get; } = Scenarios.ChangesFiles.CalculatorCommitted
            .Replace("        return a + b;\n", "        return a + b + _offset;\n", StringComparison.Ordinal);

        /// <summary>The index's <c>src/Calculator.cs</c> after the two lines: it ends with <c>    }</c>, an empty line, <c>Negate</c> and <c>}</c>.</summary>
        public static string IndexAfterLines { get; } = IndexAfterHunk
            .Replace("        return a / b;\n    }\n}\n", "        return a / b;\n    }\n\n    public int Negate(int a) => -a;\n}\n", StringComparison.Ordinal);

        // ---- K2: the blame -------------------------------------------------------------------------

        /// <summary>The Downs from line 1 to line 4, run 2 (runs numbered from 0, open point 32).</summary>
        public const int DownsToRun2 = 3;

        /// <summary>The run each Down of K2's step 5 picks: its index, its line, and the details' subject (null for run 1, not committed yet: the stage panel shows).</summary>
        public static IReadOnlyList<(int Run, int Line, string? Subject)> BlameDowns { get; } =
        [
            (0, 2, AddTheParser),
            (1, 3, null),
            (2, 4, RenameTheParser),
        ];

        public const string Run0Sha = "1d5a7cf";
        public const string Run2Sha = "0575c7f";
        public const int BlameLines = 24;
        public const int BlameRuns = 15;

        // ---- The texts (the report's "Texts these checks rely on (part K)") -----------------------

        public const string CancelFetchOrigin = "Cancel Fetch origin";
        public const string NoLongerRunning = "Cancel Fetch origin: that operation is no longer running.";
        public const string CancelPrefix = "Cancel ";
        public const string FetchOrigin = "Fetch origin";
        public const string FetchingFromOrigin = "Fetching from origin…";
        public const string FetchOriginCancelled = "Fetch origin cancelled";
        public const string StatusCancel = "Cancel";
        public const string Origin = "origin";
        public const string Remotes = "Remotes";
        public const string HeadCommit = "Add settings page";

        public const string StageHunk = "Stage hunk";
        public const string DiscardHunk = "Discard hunk";
        public const string Inline = "Inline";
        public const string SideBySide = "Side by side";
        public const string StageLines = "Stage lines";
        public const string DiscardLines = "Discard lines";
        public const string StageFile = "Stage file";
        public const string DiscardFile = "Discard file";
        public const string StageSelectedLines = "Stage selected lines";
        public const string DiscardSelectedLines = "Discard selected lines…";

        public const string BlameTitle = "Blame of Reader.cs";
        public const string BlameCaption = "in the working tree";
        public const string RunMenuHeader = "0575c7f Rename the parser to reader";
        public const string ShowInGraph = "Show in graph";
        public const string BlameBeforeThisCommit = "Blame before this commit";
        public const string FileHistory = "File history";
        public const string CopySha = "Copy SHA";
        public const string AddTheParser = "Add the parser";
        public const string RenameTheParser = "Rename the parser to reader";
        public const string MiraKovac = "Mira Kovac";
        public const string MiraEmail = "mira@example.com";

        public const string Unstaged7 = "Unstaged files (7)";
        public const string Unstaged6 = "Unstaged files (6)";
        public const string Unstaged1 = "Unstaged files (1)";
        public const string Staged3 = "Staged files (3)";
        public const string Staged4 = "Staged files (4)";
        public const string Staged0 = "Staged files (0)";
        public const string NoStagedChanges = "No staged changes";
        public const string Commit3Files = "Commit 3 files";
        public const string Committing = "Committing…";
        public const string StageFilesToCommit = "Stage files to commit";
        public const string AmendPreviousCommit = "Amend previous commit";
        public const string Ready = "Ready";
        public const string First = "First";
        public const string Next = "Next";
        public const string FirstCounter = "67";
        public const string NextCounter = "68";
        public const string AddCalculator = "Add calculator";
        public const string Main = "main";
        public const string ReadmeWorkingLine = "A small calculator for the visual checks.";

        public const string Clone = "Clone";
        public const string FromGitHub = "From GitHub…";
        public const string CloneFromGitHubTitle = "Clone from GitHub";
        public const string Choose = "Choose";
        public const string OctoHello = "octo/hello";
        public const string Private = "Private";
        public const string Hello = "hello";

        /// <summary>The URL "Choose" fills for <c>octo/hello</c> on the fake's port.</summary>
        public static string HelloUrl(int port) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/octo/hello.git");

        /// <summary>The git calls the K6 and K7 holds wait for: the unstaged diff's read and the commit (6.4).</summary>
        public const string DiffFilesWord = "diff-files";
        public const string CommitWord = "commit";
        public const string HeldDiffStart = "diff-files -p";
        public const string HeldDiffEnd = "-- README.md";
        public const string HeldCommit = "commit --file=- --cleanup=whitespace";

        // ---- The colours (part K's conventions) --------------------------------------------------

        /// <summary><c>TextControlBorderBrushFocused</c>, dark: a focused box's border (K6, K7).</summary>
        public static Color FocusedBorderDark { get; } = Color.Parse("#7C8CFF");

        /// <summary><c>TextControlBorderBrush</c>, dark: the box's own <c>BorderBrush</c>, which stays as it is.</summary>
        public static Color BoxBorderDark { get; } = Color.Parse("#323845");

        /// <summary><c>VcSelectionBrush</c>, light: the picked run's gutter (K2).</summary>
        public static Color SelectionLight { get; } = Color.Parse("#DDE1FA");

        /// <summary><c>VcPanelBackgroundBrush</c>, light: an even run's gutter (K2).</summary>
        public static Color PanelLight { get; } = Color.Parse("#F5F6F8");

        /// <summary><c>VcWindowBackgroundBrush</c>, light: an odd run's gutter (K2).</summary>
        public static Color WindowLight { get; } = Color.Parse("#FFFFFF");

        /// <summary>K8: the left panel's width before and after one Right on its splitter (<c>PanelLayout.LeftDefault</c> and 10 more), and the right panel's, which stays.</summary>
        public const double LeftBefore = 260;
        public const double LeftAfter = 270;
        public const double RightWidth = 400;

        /// <summary>The bound of each of K8's walks of Tabs (6.4's G7 bound).</summary>
        public const int WalkLimit = 200;

        /// <summary>The rounds of method 3's hold: 150 s, under the hang watchdog's 3 minutes (open point 42).</summary>
        public const int FetchHoldRounds = 300;

        // ---- The pictures (the report's Screenshots column) -------------------------------------------

        public const string K1aHunkButton = "k1a-hunk-button";
        public const string K1bHunkStaged = "k1b-hunk-staged";
        public const string K1cToolbar = "k1c-toolbar";
        public const string K1dLinesMenu = "k1d-lines-menu";
        public const string K2aBlameRun = "k2a-blame-run";
        public const string K2bBlameMenu = "k2b-blame-menu";
        public const string K3aPaletteCancel = "k3a-palette-cancel";
        public const string K8aSplitter = "k8a-splitter";
        public const string K4aRowStage = "k4a-row-stage";
        public const string K4bRowStaged = "k4b-row-staged";
        public const string K5aChosen = "k5a-chosen";
        public const string K6aSummaryKept = "k6a-summary-kept";
        public const string K7aCommitting = "k7a-committing";
        public const string K7bCommitted = "k7b-committed";

        /// <summary>Every picture of part K, in the order of the report's methods table.</summary>
        public static IReadOnlyList<string> Pictures { get; } =
        [
            K1aHunkButton, K1bHunkStaged, K1cToolbar, K1dLinesMenu,
            K2aBlameRun, K2bBlameMenu,
            K3aPaletteCancel, K8aSplitter,
            K4aRowStage, K4bRowStaged,
            K5aChosen,
            K6aSummaryKept,
            K7aCommitting, K7bCommitted,
        ];

        // ---- The real window's two launches (design 12.3.4 launches 11 and 12; D442) -----------

        /// <summary>The real window's launch 11, the twin of method 1: one tab on a fresh "changes" (<c>Scenarios.ChangesAsync</c>), dark, 1100×700; its captures in order.</summary>
        public static Launch ChangesLaunch { get; } = new(
            "Check_K1_hunk_by_keys",
            "changes",
            Dark: true,
            DefaultSize,
            [K1aHunkButton, K1bHunkStaged, K1cToolbar, K1dLinesMenu]);

        /// <summary>The real window's launch 12, the twin of method 2: one tab on a fresh "history" (<c>Scenarios.HistoryAsync</c>), <c>{"theme": "Light"}</c>, 1100×700; its captures in order.</summary>
        public static Launch HistoryLaunch { get; } = new(
            "Check_K2_blame_by_arrows",
            "history",
            Dark: false,
            DefaultSize,
            [K2aBlameRun, K2bBlameMenu]);

        /// <summary>
        /// A real-window launch of part K: its method's name, the scenario its one tab opens (a fresh
        /// copy), whether it starts dark, its window's size, and the captures it takes, each compared
        /// with the scripted picture of the same name.
        /// </summary>
        public sealed record Launch(string Method, string Scenario, bool Dark, (int Width, int Height) Size, IReadOnlyList<string> Captures);
    }
}
