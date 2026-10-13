// What check D18 of phase 4's walk-through compares against (docs/test-reports/phase-4.md, "Part D:
// the light theme at 1920×1080": "Graph G-I at 1920×1080 and the diff3 file N3", "Texts these
// checks rely on (part D)" and the row D18 of "Checks"): copied from the report, not read from the
// app. The banner's, the resolver's and the stage panel's texts are part C's (Phase4Expectations.C2.cs,
// PartC2), the list's part B's; the light colours are Phase4Expectations.Light. Data only: the
// real-window pass compiles this file too, so it uses nothing of the app and no headless type.
using Avalonia.Media;

namespace VisualCommit.VisualTests.Phase4;

public static partial class Phase4Expectations
{
    /// <summary>The expected results of check D18.</summary>
    public static class PartD
    {
        // ---- The interactive list (part 1) --------------------------------------------------------

        /// <summary>G-I at 1920×1080: "interactive" as built (pinned), one lane, every column shown.</summary>
        public static IReadOnlyList<CommitRow> GraphGI { get; } =
        [
            Row("Five", 0, 0, Current("main")) with { ShortSha = "57f089d", Date = "2026-01-01 12:07" },
            Row("Four", 0, 0) with { ShortSha = "eef5c15", Date = "2026-01-01 12:06" },
            Row("Three", 0, 0) with { ShortSha = "ff42e7f", Date = "2026-01-01 12:05" },
            Row("Fix one", 0, 0) with { ShortSha = "ed58902", Date = "2026-01-01 12:04" },
            Row("Two", 0, 0) with { ShortSha = "938844a", Date = "2026-01-01 12:03" },
            Row("One", 0, 0) with { ShortSha = "fc6c8fd", Date = "2026-01-01 12:02" },
            Row("Base", 0, 0) with { ShortSha = "4b4622c", Date = "2026-01-01 12:01" },
            Row("Initial commit", 0, 0) with { ShortSha = "da32da3", Date = "2026-01-01 12:00" },
        ];

        /// <summary>G-I's row of "Base", whose Message cell step 2 right-clicks.</summary>
        public const int BaseRow = 6;

        /// <summary>main's tip, "Five": git is unchanged while the list is open.</summary>
        public const string MainTip = "57f089d442eea23fee65c387f560002ab1ccd1fc";

        public const string InteractiveItem = "Interactive rebase of main from here…";
        public const string ListHeader = "Interactive rebase of main from 4b4622c";
        public const string ListCount = "6 commits";

        /// <summary>Where the list stands at 1920×1080: in place of the graph, x 260 to 1520 (1260 wide).</summary>
        public const double ListLeft = 260;
        public const double ListWidth = 1260;

        /// <summary>A list row's height, its action button's size and the author column's width (design 2.2).</summary>
        public const double RowHeight = 30;
        public const double ActionButtonWidth = 84;
        public const double ActionButtonHeight = 22;
        public const double AuthorWidth = 140;
        public const double SecondLineHeight = 22;

        /// <summary>How far a squash or fixup row's grip, button, SHA and subject stand to the right.</summary>
        public const double SquashIndent = 16;

        /// <summary>The list's rows after step 3, top to bottom: short SHA, subject, action as the button says it, struck through, indent.</summary>
        public static IReadOnlyList<(string Sha, string Subject, string Action, bool StruckThrough, double Indent)> Rows { get; } =
        [
            ("57f089d", "Five", "Drop", true, 0),
            ("eef5c15", "Four", "Squash", false, SquashIndent),
            ("ff42e7f", "Three", "Pick", false, 0),
            ("ed58902", "Fix one", "Pick", false, 0),
            ("938844a", "Two", "Pick", false, 0),
            ("fc6c8fd", "One", "Pick", false, 0),
        ];

        public const string Author = "Test Author";
        public const string ThreeSecondLine = "Message: Three";
        public const string EditButton = "Edit…";
        public const string BaseLabel = "Base";
        public const string BaseSha = "4b4622c";
        public const string BaseSubject = "Base";
        public const string Cancel = "Cancel";
        public const string StartRebase = "Start rebase";

        /// <summary>Where Four's selected background is sampled at 1920×1080: its vertical centre, between its subject and the author column.</summary>
        public static readonly (int X, int Y) FourBackgroundPoint = (900, 169);

        /// <summary>The join line's pixel column: 12 from the list's left edge (window x 272); sampled 6 px above the top of Three's row.</summary>
        public const int JoinLineX = 272;
        public const int JoinLineAboveThree = 6;

        /// <summary>Four's details in the right panel (step 3).</summary>
        public const string FourSha = "eef5c15";
        public const string FourDate = "2026-01-01 12:06";
        public static IReadOnlyList<string> FourFiles { get; } = ["A four.txt src"];

        // ---- The message dialog (step 4) -----------------------------------------------------------

        public const string MessageTitle = "Message of the squashed commit";
        public const string MessageSummary = "Three";
        public const string MessageDescription = "#42 is the issue\n\nFour";
        public const string MessageCounter = "67";
        public const string Save = "Save";

        /// <summary>The wide dialog card (design 7.2).</summary>
        public const double WideCard = 460;

        // ---- The resolver (part 2) ------------------------------------------------------------------

        /// <summary>The resolver's and the banner's width at 1920×1080: 1920 − 260.</summary>
        public const double ResolverWidth = 1660;

        /// <summary>N3: notes.txt after <c>git merge feature/theirs</c> with <c>merge.conflictStyle=diff3</c>, 24 lines.</summary>
        public static string N3 { get; } = PartC2.Text(
            "line 1",
            "<<<<<<< HEAD",
            "line 2 ours",
            "||||||| 195768b",
            "line 2",
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
            "||||||| 195768b",
            "line 10",
            "=======",
            "line 10 theirs",
            ">>>>>>> feature/theirs",
            "line 11",
            "line 12");

        /// <summary>N3's tints in the result (step 6).</summary>
        public static IReadOnlyList<PartC2.Tint> N3Tints { get; } = PartC2.Tints(
            24,
            (2, PartC2.Tint.Marker),
            (3, PartC2.Tint.Ours),
            (4, PartC2.Tint.Marker),
            (5, PartC2.Tint.Base),
            (6, PartC2.Tint.Marker),
            (7, PartC2.Tint.Theirs),
            (8, PartC2.Tint.Marker),
            (16, PartC2.Tint.Marker),
            (17, PartC2.Tint.Ours),
            (18, PartC2.Tint.Marker),
            (19, PartC2.Tint.Base),
            (20, PartC2.Tint.Marker),
            (21, PartC2.Tint.Theirs),
            (22, PartC2.Tint.Marker));

        /// <summary>The result after step 7's <c>Use ours</c> in block 2: 18 lines.</summary>
        public static string N3OursOnBlock2 { get; } = PartC2.Text(
            "line 1",
            "<<<<<<< HEAD",
            "line 2 ours",
            "||||||| 195768b",
            "line 2",
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
            "line 10 ours",
            "line 11",
            "line 12");

        /// <summary>The tints of <see cref="N3OursOnBlock2"/>: block 1 as in N3, line 16 (the chosen text) none.</summary>
        public static IReadOnlyList<PartC2.Tint> N3OursOnBlock2Tints { get; } = PartC2.Tints(
            18,
            (2, PartC2.Tint.Marker),
            (3, PartC2.Tint.Ours),
            (4, PartC2.Tint.Marker),
            (5, PartC2.Tint.Base),
            (6, PartC2.Tint.Marker),
            (7, PartC2.Tint.Theirs),
            (8, PartC2.Tint.Marker));

        /// <summary>The light colours the report gives by hex for D18, to hold the palette against (design 7.3, the token table).</summary>
        public static IReadOnlyList<(string Token, Color Colour)> LightHexes { get; } =
        [
            ("VcConflictOursBrush", Color.Parse("#DCF1F5")),
            ("VcConflictTheirsBrush", Color.Parse("#F0E6FA")),
            ("VcConflictBaseBrush", Color.Parse("#EEF0F3")),
            ("VcConflictMarkerBrush", Color.Parse("#FCEFD2")),
            ("VcConflictUsedBrush", Color.Parse("#E6F6EC")),
            ("VcWarningBrush", Color.Parse("#B7791F")),
            ("VcBannerBackgroundBrush", Color.Parse("#FFF4DE")),
            ("VcDangerBrush", Color.Parse("#C93A46")),
            ("VcAccentBrush", Color.Parse("#4353D8")),
            ("VcLane0Brush", Color.Parse("#4353D8")),
            ("VcSelectionBrush", Color.Parse("#DDE1FA")),
            ("VcTextSecondaryBrush", Color.Parse("#5C6572")),
            ("VcTextDisabledBrush", Color.Parse("#A1A8B3")),
        ];

        /// <summary>The light backdrop under a dialog.</summary>
        public static Color Backdrop { get; } = Color.Parse("#66000000");

        /// <summary>A primary button's label in the light theme.</summary>
        public static Color PrimaryLabel { get; } = Color.Parse("#FFFFFF");
    }
}
