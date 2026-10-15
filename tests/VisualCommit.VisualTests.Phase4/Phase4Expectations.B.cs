// Part B's expected results (docs/test-reports/phase-4.md, "Part B: the interactive rebase and
// squash": the "interactive" scenario and its commits, the graphs G-I, G-B7, G-B8 and G-B9, the
// commits the checks make, the list's geometry L0 and L7, the texts these checks rely on, and the
// rows B6 to B9 of "Checks"), copied from the report. Only part B's writer edits this file; its
// members live in Phase4Expectations.PartB. Compiled by the real-window pass too: see
// Phase4Expectations.Common.cs for what may be used here (data and pure helpers, no headless types).
using System.Globalization;
using Avalonia;
using VisualCommit.Core.Git;

namespace VisualCommit.VisualTests.Phase4;

public static partial class Phase4Expectations
{
    /// <summary>What part B's checks (B6 to B9) compare against.</summary>
    public static class PartB
    {
        /// <summary>The scenario's tab and log name.</summary>
        public const string Repository = "interactive";

        // ---- The scenario's commits (the SHAs Phase4ScenarioTests pins) ------------------------

        public const string InitialSha = "da32da349cff2210ec951624b25f62825dd435ec";

        public const string BaseSha = "4b4622c8ca70c5cfe305043f40ea99c288fbefb3";

        public const string OneSha = "fc6c8fd1a2ee786e8602d9a0ca51ec84f48bfa7b";

        public const string TwoSha = "938844a7a995d3769fbac6d4e9ff4a3095f15898";

        public const string FixOneSha = "ed58902397c0b9512ef667d9c4299a2cc8757906";

        public const string ThreeSha = "ff42e7fddcaf41bcac67a9bd8990e3cb6f9d4ae3";

        public const string FourSha = "eef5c1549f0e0f729bf7e63734e97eeb0988ba3c";

        public const string FiveSha = "57f089d442eea23fee65c387f560002ab1ccd1fc";

        // ---- The commits the checks make ("Commits the checks make") ---------------------------

        /// <summary>B7's One with Fix one folded in (its SHA: the guarded amend changed nothing).</summary>
        public const string B7OneSha = "ab4b49d005eb36535c58e54b01db091946b74b2e";

        /// <summary>B7's reworded Two.</summary>
        public const string B7TwoSha = "b3507d37adfe56baf58cf29ec0912ff444225b8c";

        /// <summary>B7's Three with Four squashed in and the edited message.</summary>
        public const string B7ThreeSha = "a081bffd034ab3d4fda4e1c073fe510a24f7c0d7";

        /// <summary>B8's squashed commit (Three and Four).</summary>
        public const string B8ThreeSha = "f34248d062c44eadf27e78a47435b55161bf75d7";

        /// <summary>B8's replayed Five, main's new tip.</summary>
        public const string B8FiveSha = "eb6f702889d08c7879c1daba704553fea072a42c";

        /// <summary>B9 part B's "Six", made outside the app through TempRepo.CommitFileAsync at 12:08.</summary>
        public const string SixSha = "c3cc217816ade59e0b8ccf262d83050f491ba812";

        /// <summary>A commit's short SHA as the screen shows it: the first 7 characters.</summary>
        public static string Short(string sha)
        {
            ArgumentNullException.ThrowIfNull(sha);
            return sha[..7];
        }

        /// <summary>B9 part B's commit made outside the app: the file, its content and the message.</summary>
        public const string SixPath = "src/six.txt";

        public const string SixContent = "six\n";

        public const string SixSubject = "Six";

        // ---- Messages, byte for byte as git keeps them (git cat-file commit, after the header) ----

        public const string B7OneMessage = "One\n";

        public const string B7TwoMessage = "Two reworded\n";

        public const string B7ThreeMessage = "Three and four\n\n#42 is the issue\n";

        /// <summary>B8's squashed message: Three's with its "#" line, a blank line, Four's (git's own squash would lose the "#" line, design 0.2).</summary>
        public const string B8ThreeMessage = "Three\n\n#42 is the issue\n\nFour\n";

        public const string B8FiveMessage = "Five\n";

        // ---- The graphs ---------------------------------------------------------------------------

        /// <summary>The Date column's text of the scenario's commit made at 12:<paramref name="minute"/>.</summary>
        public static string ScenarioDate(int minute) => string.Create(CultureInfo.InvariantCulture, $"2026-01-01 12:{minute:00}");

        /// <summary>G-I, the scenario as built (B6, B7 before Start, B8 before the squash, B9): one lane, colour 0 throughout.</summary>
        public static IReadOnlyList<CommitRow> GI { get; } =
        [
            Row("Five", 0, 0, Current("main")) with { ShortSha = "57f089d", Date = ScenarioDate(7) },
            Row("Four", 0, 0) with { ShortSha = "eef5c15", Date = ScenarioDate(6) },
            Row("Three", 0, 0) with { ShortSha = "ff42e7f", Date = ScenarioDate(5) },
            Row("Fix one", 0, 0) with { ShortSha = "ed58902", Date = ScenarioDate(4) },
            Row("Two", 0, 0) with { ShortSha = "938844a", Date = ScenarioDate(3) },
            Row("One", 0, 0) with { ShortSha = "fc6c8fd", Date = ScenarioDate(2) },
            Row("Base", 0, 0) with { ShortSha = "4b4622c", Date = ScenarioDate(1) },
            Row("Initial commit", 0, 0) with { ShortSha = "da32da3", Date = ScenarioDate(0) },
        ];

        /// <summary>G-B7, after B7's Start.</summary>
        public static IReadOnlyList<CommitRow> GB7 { get; } =
        [
            Row("Three and four", 0, 0, Current("main")) with { ShortSha = "a081bff", Date = ScenarioDate(5) },
            Row("Two reworded", 0, 0) with { ShortSha = "b3507d3", Date = ScenarioDate(3) },
            Row("One", 0, 0) with { ShortSha = "ab4b49d", Date = ScenarioDate(2) },
            Row("Base", 0, 0) with { ShortSha = "4b4622c", Date = ScenarioDate(1) },
            Row("Initial commit", 0, 0) with { ShortSha = "da32da3", Date = ScenarioDate(0) },
        ];

        /// <summary>G-B8, after B8's squash.</summary>
        public static IReadOnlyList<CommitRow> GB8 { get; } =
        [
            Row("Five", 0, 0, Current("main")) with { ShortSha = "eb6f702", Date = ScenarioDate(7) },
            Row("Three", 0, 0) with { ShortSha = "f34248d", Date = ScenarioDate(5) },
            Row("Fix one", 0, 0) with { ShortSha = "ed58902", Date = ScenarioDate(4) },
            Row("Two", 0, 0) with { ShortSha = "938844a", Date = ScenarioDate(3) },
            Row("One", 0, 0) with { ShortSha = "fc6c8fd", Date = ScenarioDate(2) },
            Row("Base", 0, 0) with { ShortSha = "4b4622c", Date = ScenarioDate(1) },
            Row("Initial commit", 0, 0) with { ShortSha = "da32da3", Date = ScenarioDate(0) },
        ];

        /// <summary>G-B9, after B9 part B's commit made outside the app: "Six" on top with main, then G-I's rows ("Five" without a label).</summary>
        public static IReadOnlyList<CommitRow> GB9 { get; } =
        [
            Row("Six", 0, 0, Current("main")) with { ShortSha = "c3cc217", Date = ScenarioDate(8) },
            GI[0] with { Labels = [] },
            .. GI.Skip(1),
        ];

        /// <summary>The graphs' lane count: one lane, so the Graph column is 48 wide.</summary>
        public const int Lanes = 1;

        // ---- The menus ("Texts these checks rely on") ---------------------------------------------

        /// <summary>Base's commit menu (design 7.1): 9 items and 2 separators.</summary>
        public static IReadOnlyList<string> BaseCommitMenu { get; } =
        [
            "4b4622c Base",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto main (disabled: \"This commit is already on main\")",
            "Revert this commit",
            "Rebase main onto this commit (disabled: \"This commit is already on main\")",
            "Interactive rebase of main from here…",
            "Reset main to this commit…",
            "—",
            "Copy SHA",
        ];

        /// <summary>The item of Base's menu that is highlighted when it opens.</summary>
        public const string BaseCommitMenuHighlighted = "Check out this commit";

        /// <summary>Base's commit menu's height (4a's formula for 9 items and 2 separators).</summary>
        public const double BaseCommitMenuHeight = 284;

        /// <summary>The item that opens the list (design 2.1).</summary>
        public const string InteractiveFromHereItem = "Interactive rebase of main from here…";

        /// <summary>The several-commits menu of Four and Three (design 3, 7.1): 4 items and 1 separator.</summary>
        public static IReadOnlyList<string> SeveralCommitsMenu { get; } =
        [
            "2 commits",
            "Squash 2 commits…",
            "Cherry-pick 2 commits onto main (disabled: \"Some of these commits are already on main\")",
            "Revert 2 commits",
            "—",
            "Copy SHAs",
        ];

        /// <summary>The several-commits menu's highlighted item and the item that squashes.</summary>
        public const string SquashItem = "Squash 2 commits…";

        /// <summary>The several-commits menu's height (4a's formula for 4 items and 1 separator).</summary>
        public const double SeveralCommitsMenuHeight = 145;

        /// <summary>A list row's menu's height: 4 items, no separator.</summary>
        public const double RowMenuHeight = 136;

        /// <summary>The actions in the order a row's menu lists them (2.2).</summary>
        public static IReadOnlyList<RebaseAction> MenuActions { get; } =
            [RebaseAction.Pick, RebaseAction.Reword, RebaseAction.Squash, RebaseAction.Fixup, RebaseAction.Drop];

        /// <summary>An action's label in a row's menu: <c>Reword…</c> opens a dialog.</summary>
        public static string MenuLabel(RebaseAction action) => action == RebaseAction.Reword ? "Reword…" : ActionName(action);

        /// <summary>An action's name as the row's action button shows it.</summary>
        public static string ActionName(RebaseAction action) => action switch
        {
            RebaseAction.Pick => "Pick",
            RebaseAction.Reword => "Reword",
            RebaseAction.Squash => "Squash",
            RebaseAction.Fixup => "Fixup",
            RebaseAction.Drop => "Drop",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Not an action of the list."),
        };

        /// <summary>
        /// A list row's menu as the report writes it (2.2): the header <c>{short} {subject}</c>,
        /// then Pick, Reword…, Squash, Fixup and Drop without the row's current action, no
        /// separator; on the bottom commit row Squash and Fixup are disabled with
        /// <see cref="NothingBelowReason"/>.
        /// </summary>
        public static IReadOnlyList<string> RowMenu(string header, RebaseAction current, bool bottomRow)
        {
            ArgumentNullException.ThrowIfNull(header);
            return
            [
                header,
                .. MenuActions.Where(action => action != current).Select(action =>
                    bottomRow && action is RebaseAction.Squash or RebaseAction.Fixup
                        ? $"{MenuLabel(action)} (disabled: \"{NothingBelowReason}\")"
                        : MenuLabel(action)),
            ];
        }

        /// <summary>The item a row's menu highlights when it opens: the first enabled one (D105).</summary>
        public const string RowMenuHighlighted = "Reword…";

        // ---- The list's texts --------------------------------------------------------------------

        public const string ListHeader = "Interactive rebase of main from 4b4622c";

        public const string ListCount = "6 commits";

        public const string UnchangedReason = "Change an action or the order first";

        public const string HeadMovedReason = "HEAD moved: open the list again";

        public const string NothingBelowReason = "The oldest commit has nothing below it to join";

        public const string CancelText = "Cancel";

        public const string StartText = "Start rebase";

        public const string EditText = "Edit…";

        /// <summary>The base row's label.</summary>
        public const string BaseLabel = "Base";

        /// <summary>The second line of the target of a group with a squash.</summary>
        public static string MessageLine(string subject) => "Message: " + subject;

        /// <summary>The second line of a reworded row.</summary>
        public static string NewMessageLine(string subject) => "New message: " + subject;

        // ---- The message dialog and the question (7.2) -------------------------------------------

        public const string SquashedMessageTitle = "Message of the squashed commit";

        /// <summary>The reword dialog's title: <c>Reword {short}</c>.</summary>
        public static string RewordTitle(string sha) => "Reword " + Short(sha);

        public const string SquashTitle = "Squash 2 commits";

        public const string SaveText = "Save";

        public const string SquashText = "Squash";

        public const string WriteSummaryReason = "Write a summary";

        /// <summary>The message dialog's card: the wide one.</summary>
        public const double MessageCardWidth = 460;

        /// <summary>What the summary counter counts down from.</summary>
        public const int SummaryLimit = 72;

        /// <summary>The summary counter's text for a summary: what is left of 72.</summary>
        public static string Counter(string summary)
        {
            ArgumentNullException.ThrowIfNull(summary);
            return (SummaryLimit - summary.Length).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The group's message as the dialog is filled with it in B7 step 6 and B8 step 3: Three's summary, and Three's "#" line, a blank line and Four as the description.</summary>
        public const string SquashSummary = "Three";

        public const string SquashDescription = "#42 is the issue\n\nFour";

        public const string EditedSummary = "Three and four";

        public const string EditedDescription = "#42 is the issue";

        public const string RewordSummary = "Two";

        public const string RewordedSummary = "Two reworded";

        public const string DiscardTitle = "Discard the changes to this list?";

        public const string DiscardText = "Discard";

        public const string KeepEditingText = "Keep editing";

        // ---- Operations (10.1, 10.2) -------------------------------------------------------------

        public const string B7Title = "Interactive rebase of main";

        public const string B7Running = "Rebasing main…";

        public const string B7Status = "Rebased main: 3 commits";

        public const string B8Title = "Squash 2 commits";

        public const string B8Running = "Squashing 2 commits…";

        public const string B8Status = "Squashed 2 commits into f34248d";

        /// <summary>The call log's line of the interactive rebase from <paramref name="onto"/> (a full SHA).</summary>
        public static string RebaseCall(string onto) => "git rebase -i " + onto;

        /// <summary><c>git status --short --branch</c> on a clean main without an upstream.</summary>
        public const string CleanStatus = "## main";

        // ---- The details panel ---------------------------------------------------------------------

        /// <summary>
        /// A commit as the details panel shows it: subject, body (null: none shown), Date, the
        /// "Commit date" row (null: not shown), the parents' short SHAs and the changed files as
        /// "letter name folder". The author is always Test Author &lt;author@example.com&gt;.
        /// </summary>
        public sealed record ShownDetails(string Subject, string? Body, string Date, string? CommitDate, IReadOnlyList<string> Parents, IReadOnlyList<string> Files);

        /// <summary>The author every commit of the scenario and of the checks has.</summary>
        public const string Author = "Test Author <author@example.com>";

        /// <summary>The commit date of every commit B7 and B8 make (D72).</summary>
        public const string AppDate = "2026-01-02 09:00";

        public static ShownDetails BaseDetails { get; } = new("Base", null, ScenarioDate(1), null, ["da32da3"], ["A base.txt src"]);

        public static ShownDetails OneDetails { get; } = new("One", null, ScenarioDate(2), null, ["4b4622c"], ["A one.txt src"]);

        public static ShownDetails TwoDetails { get; } = new("Two", null, ScenarioDate(3), null, ["fc6c8fd"], ["A two.txt src"]);

        public static ShownDetails FixOneDetails { get; } = new("Fix one", null, ScenarioDate(4), null, ["938844a"], ["M one.txt src"]);

        public static ShownDetails ThreeDetails { get; } = new("Three", "#42 is the issue", ScenarioDate(5), null, ["ed58902"], ["A three.txt src"]);

        public static ShownDetails FourDetails { get; } = new("Four", null, ScenarioDate(6), null, ["ff42e7f"], ["A four.txt src"]);

        public static ShownDetails FiveDetails { get; } = new("Five", null, ScenarioDate(7), null, ["eef5c15"], ["A five.txt src"]);

        /// <summary>B7's new tip, "Three and four" (a081bff).</summary>
        public static ShownDetails B7TipDetails { get; } = new("Three and four", "#42 is the issue", ScenarioDate(5), AppDate, ["b3507d3"], ["A four.txt src", "A three.txt src"]);

        /// <summary>B8's squashed commit, "Three" (f34248d).</summary>
        public static ShownDetails B8SquashedDetails { get; } = new("Three", "#42 is the issue\n\nFour", ScenarioDate(5), AppDate, ["ed58902"], ["A four.txt src", "A three.txt src"]);

        /// <summary>
        /// Four and Three selected (B8 step 1), in row order: phase 3's multi-selection view listed
        /// them until phase 5; their comparison now names Three From and Four To
        /// (docs/phase-5-design.md 6.1, D162).
        /// </summary>
        public static IReadOnlyList<string> MultiSelectionItems { get; } = ["eef5c15 Four", "ff42e7f Three"];

        // ---- The diff over the list (B6 step 4) ----------------------------------------------------

        public const string DiffFile = "src/three.txt";

        public const string DiffStatus = "A";

        public const string DiffFileName = "three.txt";

        public const string DiffFolder = "src";

        /// <summary>The diff's side label: the commit's short SHA.</summary>
        public const string DiffSideLabel = "ff42e7f";

        /// <summary>The one line the diff's one hunk adds.</summary>
        public const string DiffAddedLine = "three";

        // ---- The graph's points at 1100×700 (no working-changes row, no banner) -------------------

        /// <summary>Window x of "a row in its Message column": 438 + 30.</summary>
        public const double GraphMessageX = 468;

        /// <summary>The vertical centre of graph row <paramref name="row"/>: 129 + 26 × row.</summary>
        public static Point GraphRowPoint(int row) => new(GraphMessageX, 129 + (26 * row));

        // ---- The list's geometry at 1100×700 (design 2.2, "The list's geometry") ------------------

        /// <summary>The whole list: the graph area's place, column headers included.</summary>
        public static Rect ListBounds { get; } = new(260, 88, 440, 586);

        public static Rect HeaderBounds { get; } = new(260, 88, 440, 36);

        public static Rect FooterBounds { get; } = new(260, 630, 440, 44);

        public const double ListLeft = 260;

        public const double ListWidth = 440;

        public const double FirstRowTop = 124;

        public const double RowHeight = 30;

        public const double SecondLineHeight = 22;

        /// <summary>Window x of a list row's point: in the subject column, on no button, at every indent.</summary>
        public const double RowPointX = 600;

        /// <summary>The drag handle's column (24 wide, the grip centred in it) at no indent.</summary>
        public const double GripColumnX = 260;

        public const double GripColumnWidth = 24;

        public const double GripSize = 14;

        public const double ActionButtonX = 288;

        public const double ActionButtonWidth = 84;

        public const double ActionButtonHeight = 22;

        public const double ShaX = 380;

        public const double ShaWidth = 64;

        public const double SubjectX = 452;

        /// <summary>How far a squash or fixup row's whole content stands to the right.</summary>
        public const double JoinIndent = 16;

        /// <summary>Window x of the 2-px join line.</summary>
        public const double JoinLineX = 272;

        public const double JoinLineWidth = 2;

        /// <summary>Start rebase's right edge: 12 from the list's right edge.</summary>
        public const double StartRight = 688;

        /// <summary>The gap between Cancel and Start rebase.</summary>
        public const double FooterButtonGap = 8;

        /// <summary>The 2-px drag line's height.</summary>
        public const double DragLineHeight = 2;

        /// <summary>One commit row of the list as the report's tables give it.</summary>
        /// <param name="Subject">The commit's subject: the row's name in the checks.</param>
        /// <param name="Sha">The commit's full SHA.</param>
        /// <param name="Action">The row's action.</param>
        /// <param name="Top">The row's top edge (window y).</param>
        /// <param name="Indent">0, or <see cref="JoinIndent"/> for a squash or fixup row.</param>
        /// <param name="SecondLine">The second line's text under it, or null.</param>
        /// <param name="StruckThrough">Its SHA and subject are struck through (a dropped row).</param>
        public sealed record ListRow(string Subject, string Sha, RebaseAction Action, double Top, double Indent = 0, string? SecondLine = null, bool StruckThrough = false)
        {
            /// <summary>The row's <c>RebaseRow</c> name: <c>{short} {subject}</c>.</summary>
            public string Name => Short(Sha) + " " + Subject;

            /// <summary>The row's bottom edge.</summary>
            public double Bottom => Top + RowHeight;

            /// <summary>The row's vertical centre.</summary>
            public double Centre => Top + (RowHeight / 2);

            /// <summary>The row's point (window): x 600 at its vertical centre.</summary>
            public Point Point => new(RowPointX, Centre);

            /// <summary>The row's 30-high bounds.</summary>
            public Rect Bounds => new(ListLeft, Top, ListWidth, RowHeight);

            /// <summary>The second line's bounds under the row, or null.</summary>
            public Rect? SecondLineBounds => SecondLine is null ? null : new Rect(ListLeft, Top + RowHeight, ListWidth, SecondLineHeight);
        }

        /// <summary>
        /// The top edge of each commit row and of the base row of a list whose rows have a second
        /// line or not (<paramref name="secondLineUnder"/>, top to bottom): rows 30 high from y 124,
        /// each second line 22 high right under its row, the base row under the last.
        /// </summary>
        public static (IReadOnlyList<double> Rows, double BaseRow) RowTops(IReadOnlyList<bool> secondLineUnder)
        {
            ArgumentNullException.ThrowIfNull(secondLineUnder);
            var tops = new List<double>(secondLineUnder.Count);
            var y = FirstRowTop;
            foreach (var hasSecondLine in secondLineUnder)
            {
                tops.Add(y);
                y += RowHeight + (hasSecondLine ? SecondLineHeight : 0);
            }

            return (tops, y);
        }

        /// <summary>The join line of a squash or fixup row at <paramref name="memberTop"/> whose target's top is <paramref name="targetTop"/>: 2 wide at x 272, from the member's centre to the target's top edge.</summary>
        public static Rect JoinLine(double memberTop, double targetTop) =>
            new(JoinLineX, memberTop + (RowHeight / 2), JoinLineWidth, targetTop - memberTop - (RowHeight / 2));

        /// <summary>L0, the list as it opens on Base (B6, B7, B9): every row Pick, Five selected.</summary>
        public static IReadOnlyList<ListRow> L0 { get; } =
        [
            new("Five", FiveSha, RebaseAction.Pick, 124),
            new("Four", FourSha, RebaseAction.Pick, 154),
            new("Three", ThreeSha, RebaseAction.Pick, 184),
            new("Fix one", FixOneSha, RebaseAction.Pick, 214),
            new("Two", TwoSha, RebaseAction.Pick, 244),
            new("One", OneSha, RebaseAction.Pick, 274),
        ];

        /// <summary>L0's base row's top.</summary>
        public const double L0BaseTop = 304;

        /// <summary>B7 after step 3: Fix one dropped between Two and One, every row still Pick.</summary>
        public static IReadOnlyList<ListRow> B7Moved { get; } =
        [
            new("Five", FiveSha, RebaseAction.Pick, 124),
            new("Four", FourSha, RebaseAction.Pick, 154),
            new("Three", ThreeSha, RebaseAction.Pick, 184),
            new("Two", TwoSha, RebaseAction.Pick, 214),
            new("Fix one", FixOneSha, RebaseAction.Pick, 244),
            new("One", OneSha, RebaseAction.Pick, 274),
        ];

        /// <summary>B7 after step 4's Fixup: Fix one indented, joined to One.</summary>
        public static IReadOnlyList<ListRow> B7Fixup { get; } =
        [
            new("Five", FiveSha, RebaseAction.Pick, 124),
            new("Four", FourSha, RebaseAction.Pick, 154),
            new("Three", ThreeSha, RebaseAction.Pick, 184),
            new("Two", TwoSha, RebaseAction.Pick, 214),
            new("Fix one", FixOneSha, RebaseAction.Fixup, 244, JoinIndent),
            new("One", OneSha, RebaseAction.Pick, 274),
        ];

        /// <summary>B7 after step 5's Squash: Four joined to Three, Three's second line, the rows below moved down 22.</summary>
        public static IReadOnlyList<ListRow> B7Squash { get; } =
        [
            new("Five", FiveSha, RebaseAction.Pick, 124),
            new("Four", FourSha, RebaseAction.Squash, 154, JoinIndent),
            new("Three", ThreeSha, RebaseAction.Pick, 184, SecondLine: MessageLine("Three")),
            new("Two", TwoSha, RebaseAction.Pick, 236),
            new("Fix one", FixOneSha, RebaseAction.Fixup, 266, JoinIndent),
            new("One", OneSha, RebaseAction.Pick, 296),
        ];

        public const double B7SquashBaseTop = 326;

        /// <summary>B7 after step 7's Save: Three's second line with the edited summary.</summary>
        public static IReadOnlyList<ListRow> B7Edited { get; } =
        [
            .. B7Squash.Take(2),
            B7Squash[2] with { SecondLine = MessageLine("Three and four") },
            .. B7Squash.Skip(3),
        ];

        /// <summary>B7 after step 8's reword: Two's second line, the rows below moved down 22 more.</summary>
        public static IReadOnlyList<ListRow> B7Reworded { get; } =
        [
            new("Five", FiveSha, RebaseAction.Pick, 124),
            new("Four", FourSha, RebaseAction.Squash, 154, JoinIndent),
            new("Three", ThreeSha, RebaseAction.Pick, 184, SecondLine: MessageLine("Three and four")),
            new("Two", TwoSha, RebaseAction.Reword, 236, SecondLine: NewMessageLine("Two reworded")),
            new("Fix one", FixOneSha, RebaseAction.Fixup, 288, JoinIndent),
            new("One", OneSha, RebaseAction.Pick, 318),
        ];

        /// <summary>L7, the list after B7's edits (step 9, at its picture): Five dropped and selected.</summary>
        public static IReadOnlyList<ListRow> L7 { get; } =
        [
            new("Five", FiveSha, RebaseAction.Drop, 124, StruckThrough: true),
            .. B7Reworded.Skip(1),
        ];

        /// <summary>L7's (and B7Reworded's) base row's top.</summary>
        public const double L7BaseTop = 348;

        /// <summary>B9 after step 3's Alt+Down: Four on top, Five (selected) under it, all Pick.</summary>
        public static IReadOnlyList<ListRow> B9MovedDown { get; } =
        [
            new("Four", FourSha, RebaseAction.Pick, 124),
            new("Five", FiveSha, RebaseAction.Pick, 154),
            new("Three", ThreeSha, RebaseAction.Pick, 184),
            new("Fix one", FixOneSha, RebaseAction.Pick, 214),
            new("Two", TwoSha, RebaseAction.Pick, 244),
            new("One", OneSha, RebaseAction.Pick, 274),
        ];

        /// <summary>B9 part B after step 9's D: L0 with Five dropped.</summary>
        public static IReadOnlyList<ListRow> B9Dropped { get; } =
        [
            L0[0] with { Action = RebaseAction.Drop, StruckThrough = true },
            .. L0.Skip(1),
        ];

        // ---- The drag of B7 step 2 and B9 step 2 -------------------------------------------------

        /// <summary>B7 step 2: Fix one pressed at its point, held at (600, 270): the line marks the gap between Two and One (gap 5, window y 274).</summary>
        public static Point B7DragTo { get; } = new(600, 270);

        public const int B7DragGap = 5;

        public const double B7DragGapY = 274;

        /// <summary>B9 step 2: Five pressed at its point, held at (600, 210): the line marks the gap between Three and Fix one (gap 3, window y 214).</summary>
        public static Point B9DragTo { get; } = new(600, 210);

        public const int B9DragGap = 3;

        public const double B9DragGapY = 214;

        /// <summary>How many equal moves the drags take (the first of B7's is 8.2 px, past the 4-px threshold).</summary>
        public const int DragSteps = 5;

        /// <summary>The base row's point (B6 step 3): a click there changes nothing.</summary>
        public static Point BaseRowPoint { get; } = new(RowPointX, L0BaseTop + (RowHeight / 2));
    }
}
