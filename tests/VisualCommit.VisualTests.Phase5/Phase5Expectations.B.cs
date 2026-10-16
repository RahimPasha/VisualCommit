// What checks B5 to B9 of phase 5's walk-through compare against (docs/test-reports/phase-5.md,
// part B: "Conventions of part B", "Scenarios and graphs of part B", "Texts these checks rely on
// (part B)", its decisions BD1 to BD24 and the rows B5 to B9 of "Checks"): copied from the report,
// not read from the app. Data only: the real-window pass compiles this file too, so it uses
// nothing of the app and no headless type.
using System.Globalization;
using Avalonia;

namespace VisualCommit.VisualTests.Phase5;

public static partial class Phase5Expectations
{
    /// <summary>The expected results of part B: the graph area's pages, "All files", the reflog and the backups.</summary>
    public static class PartB
    {
        // ---- Geometry (BD1 to BD5, the conventions) ------------------------------------------------

        /// <summary>A page at 1100×700: the graph area's place, its column headers included (BD1).</summary>
        public static Rect PageBounds { get; } = new(260, 88, 440, 586);

        /// <summary>A page at 1920×1080 (B5 step 12).</summary>
        public static Rect FullPageBounds { get; } = new(260, 88, 1260, 966);

        /// <summary>A page's header: 36 high at the graph area's top (BD1).</summary>
        public const double HeaderHeight = 36;

        /// <summary>Where a page's content starts: under its header (BD1).</summary>
        public const double ContentTop = 124;

        /// <summary>A page row's height (BD1).</summary>
        public const double PageRowHeight = 26;

        /// <summary>Where the checks click a page row (BD1): window x 420.</summary>
        public const double PageRowX = 420;

        /// <summary>The centre of page row <paramref name="row"/> at window y 137 + 26 k (BD1).</summary>
        public static double PageRowCentreY(int row) => ContentTop + (PageRowHeight * row) + (PageRowHeight / 2);

        /// <summary>The point of page row <paramref name="row"/> the report clicks: (420, 137 + 26 k).</summary>
        public static Point PageRowPoint(int row) => new(PageRowX, PageRowCentreY(row));

        /// <summary>The history list's bottom at 1100×700: 40 % of the height below the header (BD2).</summary>
        public const double HistoryListBottom = 344;

        /// <summary>The history list's right edge at 1920×1080: 45 % of 1260 (BD2).</summary>
        public const double SideBySideListRight = 827;

        /// <summary>The blame gutter at 1100×700: 130 wide (x 260 to 390, the page under 700 wide, 4.2).</summary>
        public static Rect GutterBounds { get; } = new(260, ContentTop, 130, 550);

        /// <summary>The blame text at 1100×700: x 390 to 700.</summary>
        public static Rect BlameTextBounds { get; } = new(390, ContentTop, 310, 550);

        /// <summary>The code font's line height, which the blame's text and gutter share (13.4).</summary>
        public const double BlameLineHeight = 15.84;

        /// <summary>The centre of blame line <paramref name="line"/> (from 1): top + (L − 0.5) × 15.84.</summary>
        public static double BlameLineCentreY(int line) => ContentTop + ((line - 0.5) * BlameLineHeight);

        /// <summary>Where the checks click a line in the gutter: its left + 40 (window x 300).</summary>
        public const double GutterPointX = 300;

        /// <summary>Column <paramref name="column"/> of the blame text at 1100×700: the text's left + 30.4 + 2 + 7.2 × (C − 1).</summary>
        public static double BlameColumnX(int column) => 390 + 30.4 + 2 + (7.2 * (column - 1));

        /// <summary>The number column's width up to 99 lines (13.4, phase 4's <c>ResultPoint</c>): 8 + 7.2 × 2 + 8.</summary>
        public const double NumberColumnWidth = 30.4;

        /// <summary>The space right of the numbers in their column, and left of the widest (13.4).</summary>
        public const double NumberPadding = 8;

        /// <summary>A character's advance in the code font at 12: 7.2 (13.4).</summary>
        public const double CodeDigitWidth = 7.2;

        /// <summary>The gutter's parts at 130 wide (BD12): 4 padding, the short SHA 50.4, 6, the date 61.1, 4.</summary>
        public const double GutterPadding = 4;

        public const double GutterShaWidth = 50.4;

        public const double GutterGap = 6;

        public const double GutterDateWidth = 61.1;

        /// <summary>The width of <c>Not committed yet</c> in the gutter (BD12): 97.2.</summary>
        public const double NotCommittedYetWidth = 97.2;

        /// <summary>The reflog's list at 1100×700: y 124 to 648 above the footer (BD5).</summary>
        public const double ReflogListBottom = 648;

        /// <summary>The reflog's footer at 1100×700 (BD5).</summary>
        public static Rect FooterBounds { get; } = new(260, 648, 440, 26);

        /// <summary>A reflog row's parts at 440 (BD3): selector, action, commit and lost mark, left and right; the time hidden.</summary>
        public static (double Left, double Right) ReflogSelectorX { get; } = (268, 340);

        public static (double Left, double Right) ReflogActionX { get; } = (348, 496);

        public static (double Left, double Right) ReflogCommitX { get; } = (504, 664);

        public static (double Left, double Right) ReflogLostMarkX { get; } = (672, 692);

        /// <summary>
        /// A backup row's parts at 440 (BD4, as changed after the report's first commit: the time 136,
        /// the words at least 112): time, words and commit.
        /// </summary>
        public static (double Left, double Right) BackupTimeX { get; } = (268, 404);

        public static (double Left, double Right) BackupWordsX { get; } = (412, 524);

        public static (double Left, double Right) BackupCommitX { get; } = (532, 692);

        // ---- "history" ------------------------------------------------------------------------------

        /// <summary>The working-changes row of "history": "Working changes", ring in lane 0, colour 0.</summary>
        public static WorkingChangesRow HistoryWorkingRow { get; } = new("Working changes", 0, 0);

        /// <summary>H0, "history" as built: 14 rows, 2 lanes.</summary>
        public static IReadOnlyList<CommitRow> H0 { get; } =
        [
            Row("Read whole lines", 0, 0, Current("main"), "origin/main") with { ShortSha = "c0bc2dd", Date = Date(13) },
            Row("Stop tracing", 0, 0) with { ShortSha = "20d7fbe", Date = Date(12) },
            Row("Write new notes", 0, 0) with { ShortSha = "e765529", Date = Date(11) },
            Row("Merge branch 'feature/readme'", 0, 0) with { ShortSha = "f513566", Date = Date(10), Merge = true },
            Row("Explain the reader", 1, 1) with { ShortSha = "c1b6375", Date = Date(9) },
            Row("Remove the notes", 0, 0) with { ShortSha = "884b462", Date = Date(8), Ends = [1] },
            Row("Compare whole lines", 1, 2, "feature/compare", "origin/feature/compare") with { ShortSha = "245c395", Date = Date(7) },
            Row("Rename the parser to reader", 0, 0) with { ShortSha = "0575c7f", Date = Date(6) },
            Row("Add the comparer", 1, 2) with { ShortSha = "d0d3c77", Date = Date(5) },
            Row("Add tracing", 0, 0) with { ShortSha = "14a5c6c", Date = Date(4), Ends = [1] },
            Row("Handle empty input", 0, 0) with { ShortSha = "365482b", Date = Date(3) },
            Row("Add the logo", 0, 0) with { ShortSha = "775113f", Date = Date(2) },
            Row("Add the parser", 0, 0) with { ShortSha = "1d5a7cf", Date = Date(1) },
            Row("Initial commit", 0, 0) with { ShortSha = "7829335", Date = Date(0) },
        ];

        /// <summary>H0's lanes.</summary>
        public const int HistoryLanes = 2;

        /// <summary>H0's rows the checks name.</summary>
        public const int ReadWholeLinesRow = 0;

        public const int ExplainTheReaderRow = 4;

        public const int RenameRow = 7;

        public const int HandleEmptyInputRow = 10;

        public const int AddTheParserRow = 12;

        public const int InitialCommitRow = 13;

        public const string ReadWholeLinesSha = "c0bc2dd152226dddfbb4830f3f74a5d2b6e290c9";
        public const string StopTracingSha = "20d7fbe203a4a8ceeb5ef00da13ceda91ed70739";
        public const string ExplainTheReaderSha = "c1b63754e14cab0e146b0811ca11371ba2bbce55";
        public const string RemoveTheNotesSha = "884b4622b22f86236cbaae78b04234e63b4a6a88";
        public const string RenameSha = "0575c7f6631b3ba6c9d623f53b28489405230cfd";
        public const string AddTracingSha = "14a5c6ca3e6afa5f8d0eb4e10b5068df51f4793c";
        public const string HandleEmptyInputSha = "365482be62488b4c1259f0fb7dfa1631c797470b";
        public const string AddTheLogoSha = "775113f4ae456d7dc7dc5380c669b0d2950289e2";
        public const string AddTheParserSha = "1d5a7cf999727e7f724054348c81866a62025184";
        public const string InitialCommitSha = "7829335c71bb8f1161b668a29afb602632228425";

        /// <summary>"history"'s working tree: <c>git status --porcelain</c> (2.2).</summary>
        public static IReadOnlyList<string> HistoryStatus { get; } = [" M src/Reader.cs"];

        public const string JonasBerg = "Jonas Berg <jonas@example.com>";
        public const string MiraKovac = "Mira Kovac <mira@example.com>";
        public const string TestAuthor = "Test Author <author@example.com>";

        /// <summary>
        /// What the details panel shows of a commit (phase 1's panel, "The right panel" of part B's
        /// conventions): subject, body (or none), author, Date, a "Commit date" only where it
        /// differs, the parents' short SHAs, the list's title and each file as "letter name
        /// folder [origin]" (no letter for an unchanged file of "All files").
        /// </summary>
        public sealed record ShownDetails(
            string Sha,
            string Subject,
            string? Body,
            string Author,
            string Date,
            string? CommitDate,
            IReadOnlyList<string> Parents,
            string FilesTitle,
            IReadOnlyList<string> Files);

        public static ShownDetails ReadWholeLines { get; } = new(ReadWholeLinesSha, "Read whole lines", null, JonasBerg, Date(13), null, ["20d7fbe"], "Changed files (1)", ["M Reader.cs src"]);

        /// <summary>Read whole lines with "All files" on (B7 step 2): its tree in path order, src/Reader.cs with its M.</summary>
        public static ShownDetails ReadWholeLinesAllFiles { get; } = ReadWholeLines with
        {
            FilesTitle = "All files (6)",
            Files = ["LICENSE", "README.md", "logo.png assets", "notes.md docs", "Config.cs src", "M Reader.cs src"],
        };

        public static ShownDetails Rename { get; } = new(RenameSha, "Rename the parser to reader", null, MiraKovac, Date(6), null, ["14a5c6c"], "Changed files (1)", ["R Reader.cs src renamed from src/Parser.cs"]);

        public static ShownDetails HandleEmptyInput { get; } = new(HandleEmptyInputSha, "Handle empty input", "The tokenizer stops at an empty text now.", JonasBerg, Date(3), null, ["775113f"], "Changed files (1)", ["M Parser.cs src"]);

        public static ShownDetails AddTheParser { get; } = new(AddTheParserSha, "Add the parser", null, MiraKovac, Date(1), null, ["7829335"], "Changed files (1)", ["A Parser.cs src"]);

        /// <summary>Initial commit with "All files" on (B7 step 4): every file is changed in the root commit.</summary>
        public static ShownDetails InitialCommitAllFiles { get; } = new(InitialCommitSha, "Initial commit", null, TestAuthor, Date(0), null, [], "All files (3)", ["A LICENSE", "A README.md", "A notes.md docs"]);

        /// <summary>Explain the reader with "All files" on (B7 step 8).</summary>
        public static ShownDetails ExplainTheReaderAllFiles { get; } = new(ExplainTheReaderSha, "Explain the reader", null, JonasBerg, Date(9), null, ["884b462"], "All files (5)", ["LICENSE", "M README.md", "logo.png assets", "Config.cs src", "Reader.cs src"]);

        // ---- The stage panel (B5 step 1, B6 step 1) ---------------------------------------------------

        public const string UnstagedOneFile = "Unstaged files (1)";
        public const string StagedNone = "Staged files (0)";
        public const string NoStagedChanges = "No staged changes";
        public static IReadOnlyList<string> HistoryUnstagedRows { get; } = ["M Reader.cs src"];

        // ---- The file menus (4.4, 13.1) ----------------------------------------------------------------

        public const string FileHistoryItem = "File history";
        public const string BlameItem = "Blame";

        /// <summary>src/Reader.cs's menu in the stage panel (B5 step 2): 9 items, 3 separators, 293 high.</summary>
        public static IReadOnlyList<string> StageFileMenu { get; } =
        [
            "Reader.cs",
            "Stage",
            "Discard changes…",
            "—",
            "Ignore Reader.cs (disabled: \"Only untracked files can be ignored\")",
            "Ignore all *.cs files (disabled: \"Only untracked files can be ignored\")",
            "—",
            "File history",
            "Blame",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        public const double StageFileMenuHeight = 293;

        /// <summary>A commit's file's menu (<c>ForCommitFile</c>, 4.5) of a file in the working tree: 171 high.</summary>
        public static IReadOnlyList<string> CommitFileMenu(string name) =>
            [name, "File history", "Blame", "—", "Open file", "Show in folder", "Copy path"];

        /// <summary>The diff's menu of src/Parser.cs (B5 step 7): the file is not in the working tree.</summary>
        public static IReadOnlyList<string> ParserDiffMenu { get; } =
            ["Parser.cs", "File history", "Blame", "—", "Open file (disabled: \"The file is not in the working tree\")", "Show in folder", "Copy path"];

        public const double FileMenuHeight = 171;

        // ---- The file histories (4.1, BD2, BD13) ------------------------------------------------------

        /// <summary>A history row as the page shows it: letter, short SHA, subject, secondary text (null for none), author, date.</summary>
        public sealed record HistoryRow(string Letter, string ShortSha, string Subject, string? Secondary, string Author, string Date)
        {
            /// <summary>Its automation name: <c>{short} {subject}</c>.</summary>
            public string Name => ShortSha + " " + Subject;
        }

        /// <summary>src/Reader.cs's history from HEAD (c0bc2dd): 4 rows.</summary>
        public static IReadOnlyList<HistoryRow> ReaderHistory { get; } =
        [
            new("M", "c0bc2dd", "Read whole lines", null, "Jonas Berg", Date(13)),
            new("R", "0575c7f", "Rename the parser to reader", "renamed from src/Parser.cs", "Mira Kovac", Date(6)),
            new("M", "365482b", "Handle empty input", "as src/Parser.cs", "Jonas Berg", Date(3)),
            new("A", "1d5a7cf", "Add the parser", "added", "Mira Kovac", Date(1)),
        ];

        /// <summary>src/Reader.cs's history from 0575c7f: rows 1 to 3 of <see cref="ReaderHistory"/>.</summary>
        public static IReadOnlyList<HistoryRow> ReaderHistoryFromRename { get; } = [.. ReaderHistory.Skip(1)];

        /// <summary>src/Parser.cs's history from 365482b or 14a5c6c: 2 rows.</summary>
        public static IReadOnlyList<HistoryRow> ParserHistory { get; } =
        [
            new("M", "365482b", "Handle empty input", null, "Jonas Berg", Date(3)),
            new("A", "1d5a7cf", "Add the parser", "added", "Mira Kovac", Date(1)),
        ];

        public const string ReaderTitle = "History of Reader.cs";
        public const string ParserTitle = "History of Parser.cs";
        public const string SrcFolder = "src";

        /// <summary>The history's <c>from {short}</c> (4.1, BD24).</summary>
        public static string From(string shortSha) => "from " + shortSha;

        /// <summary>A history row's menu (<c>ForFileHistoryRow</c>, BD8): 119 high.</summary>
        public static IReadOnlyList<string> HistoryRowMenu(string shortSha, string subject) =>
            [shortSha + " " + subject, "Show in graph", "Blame at this commit", "—", "Copy SHA"];

        public const double HistoryRowMenuHeight = 119;

        /// <summary>A hunk of a diff as the report gives it: its header, and its rows as "+text", "-text" or " text".</summary>
        public sealed record ExpectedHunk(string Header, IReadOnlyList<string>? Rows = null);

        /// <summary>c0bc2dd's step of src/Reader.cs: one hunk of 15 rows (blob db5a108 → 7061deb).</summary>
        public static ExpectedHunk ReadWholeLinesHunk { get; } = new(
            "@@ -10,13 +10,13 @@ public sealed class Reader",
            [
                "         _text = text ?? string.Empty;",
                "     }",
                " ",
                "-    public string[] Words()",
                "+    public string[] Lines()",
                "     {",
                "         if (_text.Length == 0)",
                "         {",
                "             return [];",
                "         }",
                " ",
                "-        return _text.Split(' ');",
                "+        return _text.Split('\\n');",
                "     }",
                " }",
            ]);

        /// <summary>The rename's hunk: its changed rows (the rest is context).</summary>
        public static ExpectedHunk RenameHunk { get; } = new("@@ -1,11 +1,11 @@");

        public static IReadOnlyList<string> RenameChanges { get; } =
        [
            "-public sealed class Parser",
            "+public sealed class Reader",
            "-    public Parser(string text)",
            "+    public Reader(string text)",
        ];

        /// <summary>365482b's step of src/Parser.cs: one hunk; its changed rows.</summary>
        public static ExpectedHunk HandleEmptyInputHunk { get; } = new("@@ -7,11 +7,16 @@ public sealed class Parser");

        public static IReadOnlyList<string> HandleEmptyInputChanges { get; } =
        [
            "-        _text = text;",
            "+        _text = text ?? string.Empty;",
            "+        if (_text.Length == 0)",
            "+        {",
            "+            return [];",
            "+        }",
            "+",
        ];

        /// <summary>
        /// New line 10 of src/Parser.cs at 365482b, the first added line of its hunk, whose text B5
        /// step 7 right-clicks (the removed old line 10, <c>_text = text;</c>, lies just above it).
        /// </summary>
        public const string ParserNewLine10 = "        _text = text ?? string.Empty;";

        /// <summary>1d5a7cf's step: a new file of 17 added rows from <c>namespace History;</c>.</summary>
        public static ExpectedHunk AddTheParserHunk { get; } = new("@@ -0,0 +1,17 @@");

        public const int AddTheParserRows = 17;

        public const string FirstLine = "namespace History;";

        /// <summary>README.md's diff at c1b6375 (B7 step 9): one hunk.</summary>
        public static ExpectedHunk ExplainTheReaderHunk { get; } = new("@@ -1,3 +1,5 @@");

        // ---- The blames (4.2, BD11, BD12) --------------------------------------------------------------

        /// <summary>src/Reader.cs in the working tree: 24 lines.</summary>
        public static string ReaderWorkingTree { get; } = Lines(
            "namespace History;",
            string.Empty,
            "/// <summary>Reads the lines of a text.</summary>",
            "public sealed class Reader",
            "{",
            "    private readonly string _text;",
            string.Empty,
            "    public Reader(string text)",
            "    {",
            "        _text = text ?? string.Empty;",
            "    }",
            string.Empty,
            "    public string[] Lines()",
            "    {",
            "        if (_text.Length == 0)",
            "        {",
            "            return [];",
            "        }",
            string.Empty,
            "        return _text.Split('\\n');",
            "    }",
            string.Empty,
            "    public int Length => _text.Length;",
            "}");

        /// <summary>A gutter run: its first and last line and its commit's short SHA, or null for lines not committed yet.</summary>
        public sealed record BlameRun(int First, int Last, string? ShortSha);

        /// <summary>The working tree's blame of src/Reader.cs: 15 runs.</summary>
        public static IReadOnlyList<BlameRun> ReaderWorkingTreeRuns { get; } =
        [
            new(1, 2, "1d5a7cf"),
            new(3, 3, null),
            new(4, 4, "0575c7f"),
            new(5, 7, "1d5a7cf"),
            new(8, 8, "0575c7f"),
            new(9, 9, "1d5a7cf"),
            new(10, 10, "365482b"),
            new(11, 12, "1d5a7cf"),
            new(13, 13, "c0bc2dd"),
            new(14, 14, "1d5a7cf"),
            new(15, 19, "365482b"),
            new(20, 20, "c0bc2dd"),
            new(21, 21, "1d5a7cf"),
            new(22, 23, null),
            new(24, 24, "1d5a7cf"),
        ];

        /// <summary>src/Parser.cs at 14a5c6c and at 365482b: 22 lines, 5 runs.</summary>
        public static IReadOnlyList<BlameRun> ParserRuns { get; } =
        [
            new(1, 9, "1d5a7cf"),
            new(10, 10, "365482b"),
            new(11, 14, "1d5a7cf"),
            new(15, 19, "365482b"),
            new(20, 22, "1d5a7cf"),
        ];

        public const int ParserLines = 22;

        /// <summary>The lines of src/Parser.cs at 14a5c6c (and 365482b) the report names, by number.</summary>
        public static IReadOnlyList<(int Line, string Text)> ParserNamedLines { get; } =
        [
            (4, "public sealed class Parser"),
            (8, "    public Parser(string text)"),
            (13, "    public string[] Words()"),
            (20, "        return _text.Split(' ');"),
        ];

        /// <summary>LICENSE at c0bc2dd: 6 lines, one run 7829335.</summary>
        public static string License { get; } = Lines(
            "MIT License",
            string.Empty,
            "Copyright (c) 2026 The History Authors",
            string.Empty,
            "Permission is hereby granted, free of charge, to any person obtaining a copy",
            "of this software, to deal in the software without restriction.");

        public static IReadOnlyList<BlameRun> LicenseRuns { get; } = [new(1, 6, "7829335")];

        /// <summary>README.md at c0bc2dd: 5 lines.</summary>
        public static string Readme { get; } = Lines(
            "# History",
            string.Empty,
            "A repository for the history checks.",
            string.Empty,
            "The reader splits a text into lines.");

        public static IReadOnlyList<BlameRun> ReadmeRuns { get; } = [new(1, 3, "7829335"), new(4, 5, "c1b6375")];

        /// <summary>Every committed run's date in the gutter (BD12): <c>yyyy-MM-dd</c>.</summary>
        public const string GutterDate = "2026-01-01";

        public const string NotCommittedYet = "Not committed yet";

        public const string ReaderBlameTitle = "Blame of Reader.cs";
        public const string ParserBlameTitle = "Blame of Parser.cs";
        public const string LicenseBlameTitle = "Blame of LICENSE";
        public const string ReadmeBlameTitle = "Blame of README.md";
        public const string InTheWorkingTree = "in the working tree";

        /// <summary>The blame's secondary text at a commit: <c>at {short} {subject}</c>.</summary>
        public static string At(string shortSha, string subject) => $"at {shortSha} {subject}";

        /// <summary>Line 1's run's tooltip (BD11): five lines, the last because the run's path differs from the page's.</summary>
        public const string AddTheParserRunToolTip = "1d5a7cf999727e7f724054348c81866a62025184\nMira Kovac <mira@example.com>\n2026-01-01 12:01\nAdd the parser\nas src/Parser.cs";

        /// <summary>A run not committed yet's menu (BD8): 58 high.</summary>
        public static IReadOnlyList<string> UncommittedRunMenu { get; } = ["Not committed yet", "Show in graph"];

        public const double UncommittedRunMenuHeight = 58;

        /// <summary>A committed run's menu (<c>ForBlameRun</c>, BD8): 154 high.</summary>
        public static IReadOnlyList<string> RunMenu(string shortSha, string subject, string? beforeDisabled = null) =>
        [
            shortSha + " " + subject,
            "Show in graph",
            "—",
            beforeDisabled is null ? "Blame before this commit" : $"Blame before this commit (disabled: \"{beforeDisabled}\")",
            "File history",
            "—",
            "Copy SHA",
        ];

        public const double RunMenuHeight = 154;

        public const string BlameBeforeThisCommit = "Blame before this commit";
        public const string FilesFirstCommit = "This is the file's first commit";

        // ---- "reflog" ------------------------------------------------------------------------------------

        /// <summary>RL0, "reflog" as built: 9 rows, 2 lanes, no working-changes row.</summary>
        public static IReadOnlyList<CommitRow> RL0 { get; } =
        [
            Row("Draft work", 0, 0, "feature/renamed") with { ShortSha = "6f8d627", Date = Date(25) },
            Row("Write the notes", 0, 0, Current("main")) with { ShortSha = "9dd61fd", Date = Date(21) },
            Row("Topic work", 1, 1, "topic", "origin/topic") with { ShortSha = "362fbd6", Date = Date(2) },
            Row("Clash notes", 0, 0) with { ShortSha = "316166a", Date = Date(5), Ends = [1] },
            Row("Clash notes", 1, 2, "clash") with { ShortSha = "fe14262", Date = Date(5) },
            Row("Clashing work", 1, 2) with { ShortSha = "3b2501a", Date = Date(4) },
            Row("Main work", 0, 0, "origin/main") with { ShortSha = "6d7a40b", Date = Date(3) },
            Row("Add the app", 0, 0) with { ShortSha = "7dd8e8f", Date = Date(1), Ends = [1] },
            Row("Initial commit", 0, 0, "v0.8") with { ShortSha = "5c9c1cb", Date = Date(0) },
        ];

        /// <summary>RL-B8: after "Create branch here…" named shortcut at d07c677 (B8): 10 rows, 2 lanes.</summary>
        public static IReadOnlyList<CommitRow> RLB8 { get; } =
        [
            Row("Draft work", 0, 0, "feature/renamed") with { ShortSha = "6f8d627" },
            Row("Try a shortcut", 1, 1, "shortcut") with { ShortSha = "d07c677", Date = Date(23) },
            Row("Write the notes", 0, 0, Current("main")) with { ShortSha = "9dd61fd", Ends = [1] },
            Row("Topic work", 1, 2, "topic", "origin/topic") with { ShortSha = "362fbd6" },
            Row("Clash notes", 0, 0) with { ShortSha = "316166a", Ends = [1] },
            Row("Clash notes", 1, 3, "clash") with { ShortSha = "fe14262" },
            Row("Clashing work", 1, 3) with { ShortSha = "3b2501a" },
            Row("Main work", 0, 0, "origin/main") with { ShortSha = "6d7a40b" },
            Row("Add the app", 0, 0) with { ShortSha = "7dd8e8f", Ends = [1] },
            Row("Initial commit", 0, 0, "v0.8") with { ShortSha = "5c9c1cb" },
        ];

        /// <summary>RL-B9a: after "Restore branch doomed" (B9): 10 rows, 2 lanes.</summary>
        public static IReadOnlyList<CommitRow> RLB9a { get; } =
        [
            Row("Doomed work", 0, 0, "doomed") with { ShortSha = "44e646b", Date = Date(26) },
            Row("Draft work", 1, 1, "feature/renamed") with { ShortSha = "6f8d627" },
            Row("Write the notes", 0, 0, Current("main")) with { ShortSha = "9dd61fd", Ends = [1] },
            Row("Topic work", 1, 2, "topic", "origin/topic") with { ShortSha = "362fbd6" },
            Row("Clash notes", 0, 0) with { ShortSha = "316166a", Ends = [1] },
            Row("Clash notes", 1, 3, "clash") with { ShortSha = "fe14262" },
            Row("Clashing work", 1, 3) with { ShortSha = "3b2501a" },
            Row("Main work", 0, 0, "origin/main") with { ShortSha = "6d7a40b" },
            Row("Add the app", 0, 0) with { ShortSha = "7dd8e8f", Ends = [1] },
            Row("Initial commit", 0, 0, "v0.8") with { ShortSha = "5c9c1cb" },
        ];

        /// <summary>RL-B9b: RL-B9a after the push back too, with <c>origin/feature/gone</c> on Add the app.</summary>
        public static IReadOnlyList<CommitRow> RLB9b { get; } =
            [.. RLB9a.Select((row, index) => index == 8 ? row with { Labels = ["origin/feature/gone"] } : row)];

        public const int ReflogLanes = 2;

        public const string MainTip = "9dd61fde06301292b9b14d0911560b6d987f645d";
        public const string DoomedWorkSha = "44e646bdcb1d920659f2b28b1b44e19f6ae45a7b";
        public const string ShortcutSha = "d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f";
        public const string AddTheAppSha = "7dd8e8f7958138d967be47cb167b84bac7fd7467";
        public const string MainWorkSha = "6d7a40b76bb9156c0969e533b292cbe2357e6783";
        public const string TopicWorkSha = "362fbd633bc18c7553bf271ad7d31f1db055af2d";
        public const string ClashNotesSha = "316166adc8e550ebb513a7f8c0ea99ecbdd88983";

        /// <summary>The details of the commits part B shows from "reflog".</summary>
        public static ShownDetails TryAShortcut { get; } = new(ShortcutSha, "Try a shortcut", null, TestAuthor, Date(23), null, ["9dd61fd"], "Changed files (1)", ["A shortcut.txt src"]);

        public static ShownDetails AddTheApp { get; } = new(AddTheAppSha, "Add the app", null, TestAuthor, Date(1), null, ["5c9c1cb"], "Changed files (1)", ["A app.txt src"]);

        public static ShownDetails DiscardSnapshot { get; } = new(
            "9849838c39b980387b976a8a864b7c6d1ffda865",
            "VisualCommit: snapshot before discarding 2 files",
            "notes/todo.txt\nsrc/app.txt",
            "VisualCommit <snapshot@visualcommit.invalid>",
            Date(20),
            null,
            ["316166a"],
            "Changed files (2)",
            ["A todo.txt notes", "M app.txt src"]);

        /// <summary>The section header's menu of Local branches after phases 3 and 5 (13.1, <c>ForSection</c>): 5 items, 162 high.</summary>
        public static IReadOnlyList<string> LocalBranchesMenu { get; } =
        [
            "Local branches",
            "Create branch…",
            "Show reflog",
            "Show backups",
            "Show all hidden branches (disabled: \"No branch is hidden\")",
            "Stop soloing (disabled: \"No branch is soloed\")",
        ];

        public const double LocalBranchesMenuHeight = 162;

        /// <summary>A reflog row as the page holds it: selector, time (hidden at 440), action, short SHA, subject, lost.</summary>
        public sealed record ReflogRow(string Selector, string Time, string Action, string ShortSha, string Subject, bool Lost = false);

        /// <summary>HEAD's reflog (5.2's two walks): 36 entries newest first, HEAD@{5} skipped.</summary>
        public static IReadOnlyList<ReflogRow> HeadReflog { get; } =
        [
            new("HEAD@{0}", Time(27), "checkout: moving from doomed to main", "9dd61fd", "Write the notes"),
            new("HEAD@{1}", Time(26), "commit: Doomed work", "44e646b", "Doomed work", true),
            new("HEAD@{2}", Time(26), "checkout: moving from main to doomed", "9dd61fd", "Write the notes"),
            new("HEAD@{3}", Time(26), "checkout: moving from feature/renamed to main", "9dd61fd", "Write the notes"),
            new("HEAD@{4}", Time(26), "Branch: renamed refs/heads/draft to refs/heads/feature/renamed", "6f8d627", "Draft work"),
            new("HEAD@{6}", Time(25), "commit: Draft work", "6f8d627", "Draft work"),
            new("HEAD@{7}", Time(25), "checkout: moving from main to draft", "9dd61fd", "Write the notes"),
            new("HEAD@{8}", Time(25), "reset: moving to 9dd61fde06301292b9b14d0911560b6d987f645d", "9dd61fd", "Write the notes"),
            new("HEAD@{9}", Time(23), "commit: Try a shortcut", "d07c677", "Try a shortcut", true),
            new("HEAD@{10}", Time(22), "commit (amend): Write the notes", "9dd61fd", "Write the notes"),
            new("HEAD@{11}", Time(21), "commit: Draft the notes", "c7bf508", "Draft the notes", true),
            new("HEAD@{12}", Time(21), "checkout: moving from topic to main", "316166a", "Clash notes"),
            new("HEAD@{13}", Time(21), "checkout: moving from main to topic", "362fbd6", "Topic work"),
            new("HEAD@{14}", Time(20), "reset: moving to 316166adc8e550ebb513a7f8c0ea99ecbdd88983", "316166a", "Clash notes"),
            new("HEAD@{15}", Time(12), "reset: moving to HEAD", "316166a", "Clash notes"),
            new("HEAD@{16}", Time(12), "checkout: moving from 7e2ce3b66ecd286fc7ce6694d1f57ca6acf4f0d3 to main", "316166a", "Clash notes"),
            new("HEAD@{17}", Time(10), "commit: Detached experiment", "7e2ce3b", "Detached experiment", true),
            new("HEAD@{18}", Time(10), "checkout: moving from main to 316166adc8e550ebb513a7f8c0ea99ecbdd88983", "316166a", "Clash notes"),
            new("HEAD@{19}", Time(10), "checkout: moving from topic to main", "316166a", "Clash notes"),
            new("HEAD@{20}", Time(9), "rebase (finish): returning to refs/heads/topic", "362fbd6", "Topic work"),
            new("HEAD@{21}", Time(9), "rebase (pick): Topic work", "362fbd6", "Topic work"),
            new("HEAD@{22}", Time(9), "rebase (start): checkout 316166adc8e550ebb513a7f8c0ea99ecbdd88983", "316166a", "Clash notes"),
            new("HEAD@{23}", Time(9), "checkout: moving from main to topic", "f7a69be", "Topic work", true),
            new("HEAD@{24}", Time(8), "cherry-pick: Clash notes", "316166a", "Clash notes"),
            new("HEAD@{25}", Time(8), "reset: moving to 6d7a40b76bb9156c0969e533b292cbe2357e6783", "6d7a40b", "Main work"),
            new("HEAD@{26}", Time(7), "reset: moving to HEAD", "6d7a40b", "Main work"),
            new("HEAD@{27}", Time(6), "checkout: moving from clash to main", "6d7a40b", "Main work"),
            new("HEAD@{28}", Time(5), "commit: Clash notes", "fe14262", "Clash notes"),
            new("HEAD@{29}", Time(4), "commit: Clashing work", "3b2501a", "Clashing work"),
            new("HEAD@{30}", Time(4), "checkout: moving from main to clash", "7dd8e8f", "Add the app"),
            new("HEAD@{31}", Time(3), "commit: Main work", "6d7a40b", "Main work"),
            new("HEAD@{32}", Time(3), "checkout: moving from topic to main", "7dd8e8f", "Add the app"),
            new("HEAD@{33}", Time(2), "commit: Topic work", "f7a69be", "Topic work", true),
            new("HEAD@{34}", Time(2), "checkout: moving from main to topic", "7dd8e8f", "Add the app"),
            new("HEAD@{35}", Time(1), "commit: Add the app", "7dd8e8f", "Add the app"),
            new("HEAD@{36}", Time(0), "commit (initial): Initial commit", "5c9c1cb", "Initial commit"),
        ];

        /// <summary>HEAD's reflog's lost rows at the start (B8 step 2).</summary>
        public static IReadOnlyList<string> HeadLost { get; } = ["HEAD@{1}", "HEAD@{9}", "HEAD@{11}", "HEAD@{17}", "HEAD@{23}", "HEAD@{33}"];

        /// <summary>HEAD's reflog's lost rows after shortcut was made at d07c677 (B8 step 7).</summary>
        public static IReadOnlyList<string> HeadLostAfterShortcut { get; } = ["HEAD@{1}", "HEAD@{11}", "HEAD@{17}", "HEAD@{23}", "HEAD@{33}"];

        /// <summary>main's reflog (8 entries, none skipped) after shortcut was made: only main@{3} lost.</summary>
        public static IReadOnlyList<ReflogRow> MainReflog { get; } =
        [
            new("main@{0}", Time(25), "reset: moving to 9dd61fde06301292b9b14d0911560b6d987f645d", "9dd61fd", "Write the notes"),
            new("main@{1}", Time(23), "commit: Try a shortcut", "d07c677", "Try a shortcut"),
            new("main@{2}", Time(22), "commit (amend): Write the notes", "9dd61fd", "Write the notes"),
            new("main@{3}", Time(21), "commit: Draft the notes", "c7bf508", "Draft the notes", true),
            new("main@{4}", Time(8), "cherry-pick: Clash notes", "316166a", "Clash notes"),
            new("main@{5}", Time(3), "commit: Main work", "6d7a40b", "Main work"),
            new("main@{6}", Time(1), "commit: Add the app", "7dd8e8f", "Add the app"),
            new("main@{7}", Time(0), "commit (initial): Initial commit", "5c9c1cb", "Initial commit"),
        ];

        /// <summary>topic's reflog (3 entries), topic@{1} lost.</summary>
        public static IReadOnlyList<ReflogRow> TopicReflog { get; } =
        [
            new("topic@{0}", Time(9), "rebase (finish): refs/heads/topic onto 316166adc8e550ebb513a7f8c0ea99ecbdd88983", "362fbd6", "Topic work"),
            new("topic@{1}", Time(2), "commit: Topic work", "f7a69be", "Topic work", true),
            new("topic@{2}", Time(2), "branch: Created from HEAD", "7dd8e8f", "Add the app"),
        ];

        /// <summary>The reflog's footer for HEAD (BD5).</summary>
        public const string HeadFooter = "1 entry is not shown: it names no commit that exists";

        /// <summary>The lost mark's tooltip (5.2).</summary>
        public const string LostToolTip = "No branch, tag or remote branch reaches this commit";

        /// <summary>The reflog page's pane names and source labels (BD6).</summary>
        public static string ReflogOf(string source) => "Reflog of " + source;

        public const string BackupsTitle = "Backups";

        /// <summary>The reads of HEAD's reflog (5.2), as the call log writes them.</summary>
        public static IReadOnlyList<string> HeadReflogReads { get; } =
        [
            "git log --walk-reflogs --no-show-signature --no-color --encoding=UTF-8 --ignore-missing -z -n 501 --format=%gD%x1f%H%x1f%P%x1f%gs%x1f%s HEAD --",
            "git log --walk-reflogs --no-show-signature --no-color --encoding=UTF-8 --ignore-missing -z -n 501 --date=raw --format=%gD%x1f%H HEAD --",
            "git rev-list --ignore-missing --stdin --not --branches --tags --remotes HEAD --",
        ];

        /// <summary>A reflog entry's menu (<c>ForReflogEntry</c>, BD8): 206 high.</summary>
        public static IReadOnlyList<string> ShortcutEntryMenu { get; } =
        [
            "d07c677 Try a shortcut",
            "Create branch here…",
            "Check out this commit",
            "—",
            "Reset main to this commit…",
            "Cherry-pick onto main",
            "—",
            "Show in graph (disabled: \"This commit is not in the graph\")",
            "Copy SHA",
        ];

        public const double EntryMenuHeight = 206;

        public const string ShowInGraph = "Show in graph";
        public const string NotInTheGraph = "This commit is not in the graph";
        public const string CreateBranchHere = "Create branch here…";

        /// <summary>Phase 3's name dialog from d07c677 (2.4).</summary>
        public const string CreateBranchTitle = "Create branch";
        public const string ShortcutContext = "From d07c677 \"Try a shortcut\"";
        public const string BranchNamePlaceholder = "Branch name";
        public const string CheckOutTheNewBranch = "Check out the new branch";
        public const string ShortcutName = "shortcut";

        /// <summary>The source menu (BD7): 7 items and a separator, 223 high, at <c>ReflogSource</c>'s bottom-left corner.</summary>
        public static IReadOnlyList<string> SourceMenu { get; } =
            ["Reflog and backups", "HEAD", "clash", "feature/renamed", "main", "shortcut", "topic", "—", "Backups"];

        public const double SourceMenuHeight = 223;

        public const string ShowReflogOfTopic = "Show reflog of topic";

        /// <summary>The Local branches section after B8 step 7: its first-level rows ("▸" before a closed folder).</summary>
        public static IReadOnlyList<string> LocalBranchesAfterShortcut { get; } = ["▸ feature", "clash", "main", "shortcut", "topic"];

        // ---- The backups list (5.3, 12.3, BD4) ---------------------------------------------------------

        /// <summary>A backup row as the list holds it: name, time, words, short SHA, subject.</summary>
        public sealed record BackupRow(string Name, string Time, string Words, string ShortSha, string Subject);

        /// <summary>The backups list after the clean-up at opening: 15 backups, newest first.</summary>
        public static IReadOnlyList<BackupRow> Backups { get; } =
        [
            new("branch-20260101T122701000Z", "2026-01-01 12:27:01", "Deleted branch doomed", "44e646b", "Doomed work"),
            new("reset-20260101T122401000Z", "2026-01-01 12:24:01", "Before a hard reset to 9dd61fd", "d07c677", "Try a shortcut"),
            new("discard-20260101T122001000Z", "2026-01-01 12:20:01", "Before discarding 2 files", "9849838", "VisualCommit: snapshot before discarding 2 files"),
            new("reset-20260101T121901000Z", "2026-01-01 12:19:01", "Before a mixed reset", "316166a", "Clash notes"),
            new("remote-20260101T121801000Z", "2026-01-01 12:18:01", "Removed remote mirror", "316166a", "Clash notes"),
            new("remote-branch-20260101T121701000Z", "2026-01-01 12:17:01", "Deleted feature/gone on origin", "7dd8e8f", "Add the app"),
            new("remote-tag-20260101T121601000Z", "2026-01-01 12:16:01", "Deleted tag v0.8 on origin", "5c9c1cb", "Initial commit"),
            new("tag-undone-20260101T121501000Z", "2026-01-01 12:15:01", "Tag v1.0-rc, its creation undone", "316166a", "Clash notes"),
            new("tag-20260101T121401000Z", "2026-01-01 12:14:01", "Deleted tag v0.9", "7dd8e8f", "Add the app"),
            new("stash-20260101T121301000Z", "2026-01-01 12:13:01", "Dropped or popped stash \"On main: Try a README\"", "39daf6c", "On main: Try a README"),
            new("detached-20260101T121101000Z", "2026-01-01 12:11:01", "Commits a detached HEAD left behind", "7e2ce3b", "Detached experiment"),
            new("force-push-20260101T120901000Z", "2026-01-01 12:09:01", "Before a force push of origin/topic", "f7a69be", "Topic work"),
            new("rebase-20260101T120801000Z", "2026-01-01 12:08:01", "Before rebasing topic", "f7a69be", "Topic work"),
            new("skip-20260101T120701000Z", "2026-01-01 12:07:01", "Before skipping a commit", "6d7a40b", "Main work"),
            new("abort-20260101T120601000Z", "2026-01-01 12:06:01", "Before aborting", "6d7a40b", "Main work"),
        ];

        /// <summary>The backups list's rows the checks name.</summary>
        public const int DoomedBackupRow = 0;
        public const int DiscardBackupRow = 2;
        public const int MixedResetBackupRow = 3;
        public const int RemoteBranchBackupRow = 5;
        public const int StashBackupRow = 9;
        public const int SkipBackupRow = 13;
        public const int AbortBackupRow = 14;

        /// <summary>Where B9 step 2 rests the pointer: on the words of row 9 (464, 371).</summary>
        public static Point StashWordsPoint { get; } = new(464, 371);

        /// <summary>The clean-up at opening on "reflog" (15.2, BD23): its record's message and its notes, in order.</summary>
        public const string CleanUpTitle = "Clean up backups";
        public const string CleanUpMessage = "Removed 2 backups";

        public static IReadOnlyList<string> CleanUpNotes { get; } =
        [
            "Removed backup reset-20251120T090001000Z (older than 30 days)",
            "Removed backup branch-20251120T090002000Z (older than 30 days)",
        ];

        /// <summary>The backups left after the clean-up at opening.</summary>
        public const int BackupsLeft = 15;

        /// <summary>A backup's menu (<c>ForBackup</c>, BD8): the header, its restore, then the generic items; 154 high.</summary>
        public static IReadOnlyList<string> BackupMenu(string words, string restore) =>
            [words, restore, "—", "Create branch here…", "Check out this commit", "—", "Copy SHA"];

        public const double BackupMenuHeight = 154;

        public const string RestoreBranchDoomed = "Restore branch doomed";
        public const string BranchDoomedExists = "A branch named doomed exists";
        public const string PushFeatureGoneBack = "Push feature/gone back to origin…";
        public const string RestoreTheDiscardedFiles = "Restore the discarded files…";
        public const string RestoreTheChanges = "Restore the changes…";
        public const string RestoreTheStagedChanges = "Restore the staged changes";
        public const string UnstageYourChangesFirst = "Unstage your changes first";

        /// <summary>The push back's question (7.5, 13.2): not destructive in style, no detail.</summary>
        public const string PushBackTitle = "Push feature/gone back to origin?";
        public const string PushBackQuestion = "origin gets feature/gone back at 7dd8e8f.";
        public const string Push = "Push";

        /// <summary>D75's question (13.2) for one file changed since the discard.</summary>
        public const string RestoreQuestionTitle = "Restore discarded changes?";
        public const string RestoreQuestionOneFile = "app.txt has changed since the discard. Put back the version from before the discard?";
        public const string RestoreQuestionDetail = "A snapshot of the newer changes is saved first.";
        public const string Restore = "Restore";

        /// <summary>An operation part B starts, as "Operations" of its texts give it (12.1, 12.2, 7.6).</summary>
        public sealed record Operation(string Title, string Status, string UndoText);

        public static Operation CreateShortcut { get; } = new("Create branch shortcut", "Created shortcut", "creation of shortcut");
        public static Operation RestoreDoomed { get; } = new("Restore branch doomed", "Restored branch doomed", "restore of branch doomed");
        public static Operation PushFeatureGone { get; } = new("Push feature/gone back to origin", "Pushed feature/gone back to origin", "push of feature/gone back to origin");
        public static Operation RestoreDiscardedFiles { get; } = new("Restore the discarded files", "Restored 2 files", "restore of the discarded files");
        public static Operation RestoreAbortChanges { get; } = new("Restore the changes", "Restored 2 files", "restore of the changes");
        public static Operation RestoreSkipChanges { get; } = new("Restore the changes", "Restored 1 file", "restore of the changes");
        public static Operation RestoreStagedChanges { get; } = new("Restore the staged changes", "Restored the staged changes", "restore of the staged changes");

        /// <summary>The app's commands of B8 and B9 (4.4 of the scenario facts, BD16, BD17).</summary>
        public const string CreateShortcutCommand = "git branch shortcut d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f";
        public const string ShortcutReflog = "branch: Created from d07c677b8a64284adc1f0f3ed0733f97ca3d3a7f";
        public const string RestoreDoomedCommand = "git update-ref -m \"VisualCommit: restore of branch doomed\" --create-reflog --stdin";

        /// <summary>The restore's one line of standard input (design 7.3's <c>SetRef</c>, BD17): the branch created at Doomed work.</summary>
        public const string RestoreDoomedInput = "create refs/heads/doomed " + DoomedWorkSha;

        public const string DoomedReflog = "VisualCommit: restore of branch doomed";
        public const string PushBackCommand = "git push --progress --force-with-lease=refs/heads/feature/gone: origin 7dd8e8f7958138d967be47cb167b84bac7fd7467:refs/heads/feature/gone";
        public const string PushBackSummary = " * [new branch]      7dd8e8f7958138d967be47cb167b84bac7fd7467 -> feature/gone";
        public const string RestoreDiscardCommand = "git restore --source=9849838c39b980387b976a8a864b7c6d1ffda865 --worktree -- notes/todo.txt src/app.txt";
        public const string RestoreAbortCommand = "git restore --source=08b41a45cd0d6f8571d150651e979bd6be73a9c4 --worktree -- docs/clash.txt src/app.txt";
        public const string RestoreSkipCommand = "git restore --source=946fb4b9d6331facefff73c01cdd1e8271276db2 --worktree -- src/app.txt";
        public const string RestoreStagedCommand = "git restore --staged --source=57a5e6edfbd470dc411b4ea3bb4eaabc405aa571 -- src/app.txt";

        /// <summary>origin's heads after the push back (<c>git ls-remote origin</c>).</summary>
        public static IReadOnlyList<(string Ref, string Sha)> OriginHeadsAfterPush { get; } =
        [
            ("refs/heads/feature/gone", AddTheAppSha),
            ("refs/heads/main", MainWorkSha),
            ("refs/heads/topic", TopicWorkSha),
        ];

        /// <summary>B9's outside edit (BD19).</summary>
        public const string OwnEdit = "app\nmy own edit\n";

        /// <summary>The first backup the app makes in a run (the backup clock, 2.3) and the snapshots B9 makes, asserted by their ids.</summary>
        public const string FirstNewBackup = "refs/visualcommit/backup/discard-20260102T090001000Z";
        public const string FirstNewBackupName = "discard-20260102T090001000Z";
        public const string FirstNewBackupTime = "2026-01-02 09:00:01";
        public const string OwnEditSnapshot = "27c8989ccb8c834f2788d01bd7ce7358ddd61c30";
        public const string SkipNewerSnapshot = "c424282c53a875a664e97e94bd148e389057a6e4";
        public const string NewSnapshotWords = "Before discarding 1 file";
        public const string NewSnapshotSubject = "VisualCommit: snapshot before discarding 1 file";
        public const string NewSnapshotMessage = "VisualCommit: snapshot before discarding 1 file\n\nsrc/app.txt";
        public const string SnapshotIdentity = "VisualCommit <snapshot@visualcommit.invalid>";

        /// <summary>The files' contents B9 asserts.</summary>
        public const string AppStaged = "app\nmain work\nstaged\n";
        public const string Todo = "todo\n";
        public const string AppMainWorkAndClash = "app\nmain work and clash\n";
        public const string AppResolvedThenSkipped = "app\nresolved, then skipped\n";
        public const string ClashNotes = "clash notes\n";
        public const string NotesWritten = "notes\nwritten\n";

        /// <summary>The index entry of src/app.txt after the staged changes' restore (B9 step 14).</summary>
        public const string StagedAppEntry = "100644 99a66a7dafbd532316960597a6609b8fe32bccae 0\tsrc/app.txt";

        /// <summary>Clash notes' details as B9 step 13 shows them: its commit date differs.</summary>
        public static ShownDetails ClashNotes316 { get; } = new(ClashNotesSha, "Clash notes", null, TestAuthor, Date(5), Date(8), ["6d7a40b"], "Changed files (1)", ["A clash.txt docs"]);

        /// <summary>Main work's details (B9 step 11).</summary>
        public static ShownDetails MainWork { get; } = new(MainWorkSha, "Main work", null, TestAuthor, Date(3), null, ["7dd8e8f"], "Changed files (1)", ["M app.txt src"]);

        /// <summary>Doomed work's details (B9 step 3).</summary>
        public static ShownDetails DoomedWork { get; } = new(DoomedWorkSha, "Doomed work", null, TestAuthor, Date(26), null, ["9dd61fd"], "Changed files (1)", ["A doomed.txt docs"]);

        // ---- Git's own texts and the dates ------------------------------------------------------------

        /// <summary>
        /// Whether this machine's git wrote the reflog texts the report quotes (part B's "Git's own
        /// texts"): they came out the same on Git 2.36 and 2.55, so they are asserted word for word
        /// on both; on any other git the checks assert the selectors, commits, lost marks and the
        /// footer only.
        /// </summary>
        public static async Task<bool> IsReflogTextGitAsync() => await MachineGitVersionAsync().ConfigureAwait(false) is { Major: 2, Minor: 36 or 55 };

        /// <summary>The scenario's date of the commit made at 12:<paramref name="minute"/> on 2026-01-01, as the Date column shows it.</summary>
        public static string Date(int minute) => string.Create(CultureInfo.InvariantCulture, $"2026-01-01 12:{minute:00}");

        /// <summary>A reflog entry's time at 12:<paramref name="minute"/> on 2026-01-01, as the time column (hidden at 440) holds it.</summary>
        public static string Time(int minute) => Date(minute);

        /// <summary>Lines joined by line feeds, without a final one (as an editor's document holds a file that ends with one).</summary>
        public static string Lines(params string[] lines) => string.Join('\n', lines);
    }
}
