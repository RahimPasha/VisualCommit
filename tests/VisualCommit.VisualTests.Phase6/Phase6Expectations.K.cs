// Part K's expected results (docs/test-reports/phase-6.md, "Part K: the command palette and the
// keys" and the rows K10 to K13 of "Checks"), copied from the report: the graphs OPS0 to K-DOCS,
// the palette's rows, the menus with their keys, the tooltips, help texts and accelerator keys,
// the dashboard rows K-WEBf, K-WEBm and K-TOOLS, the log lines, the commands and git's own texts
// (Git 2.36.0.windows.1's, asserted word for word only on that git, phase 3's rule; the lines the
// report marks "key" on every git). Key texts are written as literals in the Windows and Linux
// words (design 6.12); OnPlatform gives a Mac's words where the command modifier is Cmd. Only part
// K's writer edits this file; its members live in Phase6Expectations.PartK. Compiled by the
// real-window pass too: see Phase6Expectations.Common.cs for what may be used here (no type of the
// walk-through's own files).
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase6;

public static partial class Phase6Expectations
{
    /// <summary>What part K's checks (K10 to K13) compare against.</summary>
    public static partial class PartK
    {
        // ---- "ops" (OPS0, scenario facts 6; part K's scenarios). ----

        /// <summary>The tab's and the repository's name of "ops".</summary>
        public const string Ops = "ops";

        public const string MainWorkSha = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

        public const string AlphaTwoSha = "ce5ea6ab16759bede02ce706dd5c6865443c2051";

        /// <summary>The stash <c>stash@{0}</c> "On main: Experiment" of OPS0.</summary>
        public const string StashSha = "0e95aca13f7975ef4a1fe029a8b5e9497f31028d";

        /// <summary>K-OPSk's commit "Keys only" (Test Author, D72's 2026-01-02 09:00, parent Main work).</summary>
        public const string KeysOnlySha = "9522dae2b1c72b6c45e5dadcf572af1e26798d7f";

        /// <summary>K-OPSk's commit's tree.</summary>
        public const string KeysOnlyTree = "a48324397cdc5097f091a20ef00043726eca3e86";

        /// <summary>K-OPSs' stash "On main: Experiment again" (2026-01-02 09:00).</summary>
        public const string StashAgainSha = "9b4f935718ae99ea51d393ae2290bb5f93f7c0ce";

        /// <summary>K-OPSs' stash's three parents: Main work, its index commit and its (empty) untracked commit.</summary>
        public static readonly string[] StashAgainParents = [MainWorkSha, "a5da8933403f8d7f11267650c6c418280b2a9857", "86c954de3f30b0e476c7829f31a2ebc41a8a4eb7"];

        /// <summary>OPS0's stash as the stash list names it.</summary>
        public const string StashMessage = "On main: Experiment";

        /// <summary>K-OPSs' stash as the stash list names it.</summary>
        public const string StashAgainMessage = "On main: Experiment again";

        /// <summary>The message typed into the stash dialog (KD19).</summary>
        public const string StashAgainTyped = "Experiment again";

        /// <summary>OPS0's lanes: three, a row clicked at x 484.</summary>
        public const int Ops0Lanes = 3;

        /// <summary>K-OPSk's lanes: four, a row clicked at x 500.</summary>
        public const int KeysOnlyLanes = 4;

        /// <summary>OPS0, "ops" as built (scenario facts 6).</summary>
        public static readonly CommitRow[] Ops0 =
        [
            new("On main: Experiment", 0, 0, ["stash@{0}"], ShortSha: "0e95aca", Stash: true, Date: "2026-01-01 12:07"),
            Commit("Conflicting change", 1, 1, "078ef05", "2026-01-01 12:06", "feature/conflict"),
            Commit("Fast-forward me", 2, 2, "d93814d", "2026-01-01 12:05", "feature/ff"),
            Commit("Main work", 0, 0, "88d6b91", "2026-01-01 12:04", Current("main"), "origin/main", "v1.1") with { Ends = [2] },
            Commit("Alpha two", 2, 3, "ce5ea6a", "2026-01-01 12:03", "feature/alpha", "origin/feature/alpha"),
            Commit("Alpha one", 2, 3, "dff8fbf", "2026-01-01 12:02"),
            Commit("Add core", 0, 0, "8beca34", "2026-01-01 12:01", "v1.0") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "4ef1f7c", "2026-01-01 12:00"),
        ];

        /// <summary>OPS0's dashed line: lane 0 from the stash's ring through rows 1 and 2 into Main work's node.</summary>
        public static readonly (int Row, GraphLineKind Kind, int Lane)[] Ops0Dashes =
            [(0, GraphLineKind.OutOfNode, 0), (1, GraphLineKind.PassThrough, 0), (2, GraphLineKind.PassThrough, 0), (3, GraphLineKind.IntoNode, 0)];

        /// <summary>OPS0's row "Main work" (row 3), selected in K10.</summary>
        public const int MainWorkRow = 3;

        /// <summary>OPS0's row "Alpha two" (row 4).</summary>
        public const int AlphaTwoRow = 4;

        /// <summary>K-OPSa: OPS0 after the palette's checkout of feature/alpha (K10 step 4).</summary>
        public static readonly CommitRow[] OpsA =
        [
            Ops0[0], Ops0[1], Ops0[2],
            Ops0[3] with { Labels = ["main", "origin/main", "v1.1"] },
            Ops0[4] with { Labels = [Current("feature/alpha"), "origin/feature/alpha"] },
            Ops0[5], Ops0[6], Ops0[7],
        ];

        /// <summary>K-OPSk: OPS0 after the commit "Keys only" (K12 part A step 10, and again after its redo).</summary>
        public static readonly CommitRow[] OpsK =
        [
            Commit("Keys only", 0, 0, "9522dae", "2026-01-02 09:00", Current("main")),
            new("On main: Experiment", 1, 1, ["stash@{0}"], ShortSha: "0e95aca", Stash: true, Date: "2026-01-01 12:07"),
            Commit("Conflicting change", 2, 2, "078ef05", "2026-01-01 12:06", "feature/conflict"),
            Commit("Fast-forward me", 3, 3, "d93814d", "2026-01-01 12:05", "feature/ff"),
            Commit("Main work", 0, 0, "88d6b91", "2026-01-01 12:04", "origin/main", "v1.1") with { Ends = [1, 3] },
            Commit("Alpha two", 1, 4, "ce5ea6a", "2026-01-01 12:03", "feature/alpha", "origin/feature/alpha"),
            Commit("Alpha one", 1, 4, "dff8fbf", "2026-01-01 12:02"),
            Commit("Add core", 0, 0, "8beca34", "2026-01-01 12:01", "v1.0") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "4ef1f7c", "2026-01-01 12:00"),
        ];

        /// <summary>K-OPSk's dashed line: lane 1 from the stash's ring (row 1) through rows 2 and 3 into Main work's node (row 4).</summary>
        public static readonly (int Row, GraphLineKind Kind, int Lane)[] OpsKDashes =
            [(1, GraphLineKind.OutOfNode, 1), (2, GraphLineKind.PassThrough, 1), (3, GraphLineKind.PassThrough, 1), (4, GraphLineKind.IntoNode, 1)];

        /// <summary>K-OPSp: OPS0 after the pop of its stash (K12 part B step 21), under the working-changes row: no stash.</summary>
        public static readonly CommitRow[] OpsP =
        [
            Commit("Conflicting change", 0, 0, "078ef05", "2026-01-01 12:06", "feature/conflict"),
            Commit("Fast-forward me", 1, 1, "d93814d", "2026-01-01 12:05", "feature/ff"),
            Commit("Main work", 1, 1, "88d6b91", "2026-01-01 12:04", Current("main"), "origin/main", "v1.1"),
            Commit("Alpha two", 2, 2, "ce5ea6a", "2026-01-01 12:03", "feature/alpha", "origin/feature/alpha"),
            Commit("Alpha one", 2, 2, "dff8fbf", "2026-01-01 12:02"),
            Commit("Add core", 0, 0, "8beca34", "2026-01-01 12:01", "v1.0") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "4ef1f7c", "2026-01-01 12:00"),
        ];

        /// <summary>K-OPSs: OPS0 with row 0 the new stash "On main: Experiment again" (K12 part B step 22).</summary>
        public static readonly CommitRow[] OpsS =
        [
            new("On main: Experiment again", 0, 0, ["stash@{0}"], ShortSha: "9b4f935", Stash: true, Date: "2026-01-02 09:00"),
            .. Ops0[1..],
        ];

        /// <summary>The working-changes row over "ops" (K-OPSw): its ring in lane 0, colour 0.</summary>
        public static WorkingChangesRow OpsWorking { get; } = new("Working changes", 0, 0);

        /// <summary>K11's change of <c>src/core.txt</c>, written outside the app at step 3 (KD19).</summary>
        public const string CoreMore = "core\nmain work\nmore\n";

        /// <summary>K12 part A's change of <c>src/core.txt</c>, written before the start (KD19).</summary>
        public const string CoreKeys = "core\nmain work\nkeys\n";

        /// <summary>The changed file.</summary>
        public const string CoreFile = "src/core.txt";

        /// <summary>K11's summary (KD19).</summary>
        public const string NoteMoreCoreWork = "Note more core work";

        /// <summary>K12's summary (KD19): the commit "Keys only".</summary>
        public const string KeysOnly = "Keys only";

        /// <summary>K12's filter text (KD19).</summary>
        public const string FilterText = "fe";

        /// <summary>K12's search query (KD19).</summary>
        public const string SearchQuery = "alpha";

        /// <summary>README.md after the pop of OPS0's stash (K-OPSp).</summary>
        public const string ReadmeExperiment = "# Operations\n\nExperiment.\n";

        /// <summary>
        /// The working tree's status lines as <c>git status --porcelain=v1</c> writes them: K-OPSw
        /// (unstaged), after Stage all (staged), K-OPSp.
        /// </summary>
        public const string CoreUnstaged = " M src/core.txt";

        public const string CoreStaged = "M  src/core.txt";

        public const string ReadmeUnstaged = " M README.md";

        /// <summary>K-OPSa's branch line (<c>git status --porcelain=v1 --branch</c>).</summary>
        public const string AlphaBranchLine = "## feature/alpha...origin/feature/alpha";

        /// <summary>K-OPSk's branch line.</summary>
        public const string AheadOneBranchLine = "## main...origin/main [ahead 1]";

        /// <summary>OPS0's branch line.</summary>
        public const string MainBranchLine = "## main...origin/main";

        /// <summary>OPS0's local branches as <c>git for-each-ref refs/heads</c> lists them (none made in K10 step 5).</summary>
        public static readonly string[] Ops0LocalBranches = ["refs/heads/feature/alpha", "refs/heads/feature/conflict", "refs/heads/feature/ff", "refs/heads/main"];

        /// <summary>The file history of <c>src/core.txt</c> from "Keys only" (K-OPSk, phase 5's reader): subject and short SHA, newest first.</summary>
        public static readonly (string Subject, string ShortSha)[] CoreHistory = [("Keys only", "9522dae"), ("Main work", "88d6b91"), ("Add core", "8beca34")];

        /// <summary>The blame of <c>src/core.txt</c> at "Keys only": each line with its commit.</summary>
        public static readonly (string Line, string ShortSha, string Subject)[] CoreBlame = [("core", "8beca34", "Add core"), ("main work", "88d6b91", "Main work"), ("keys", "9522dae", "Keys only")];

        /// <summary>The diff of <c>src/core.txt</c> in "Keys only": one hunk adding <c>keys</c>.</summary>
        public static readonly string[] CoreDiff = ["@@ -1,2 +1,3 @@", " core", " main work", "+keys"];

        public const string HistoryOfCore = "History of core.txt";

        public const string BlameOfCore = "Blame of core.txt";

        public const string BlameAtKeysOnly = "at 9522dae Keys only";

        /// <summary>HEAD's reflog after K12 part A's redo, newest first (<c>git log -g --format=%gs HEAD</c>, its first three).</summary>
        public static readonly string[] RedoReflog = ["VisualCommit: redo commit \"Keys only\"", "VisualCommit: undo commit \"Keys only\"", "commit: Keys only"];

        /// <summary>The commits K12's search for <c>alpha</c> matches (K-OPSs rows 4 and 5).</summary>
        public static readonly int[] AlphaMatches = [4, 5];

        // ---- Points and geometry at 1100×700 (part K's conventions; ±2 where the report gives it). ----

        /// <summary>The tolerance of a position the report gives ±2.</summary>
        public const double Tolerance = 2;

        /// <summary>The tolerance of a menu's width (part K's "Menus", after the 2026-10-23 correction).</summary>
        public const double MenuWidthTolerance = 3;

        /// <summary>"Main work" in its Message column (OPS0's row 3).</summary>
        public static readonly Point MainWorkPoint = new(484, 207);

        /// <summary>The empty graph area below OPS0's rows (KD19).</summary>
        public static readonly Point EmptyAreaPoint = new(484, 360);

        /// <summary>The stash's row (OPS0's row 0) in its Message column; with the working-changes row, the working-changes row.</summary>
        public static readonly Point TopRowPoint = new(484, 129);

        /// <summary>The palette's list where K10 turns the wheel (KD19).</summary>
        public static readonly Point WheelPoint = new(550, 297);

        /// <summary>The palette's row 1 (KD19).</summary>
        public static readonly Point PaletteRow1Point = new(550, 185);

        /// <summary>The palette's row 0 (KD19).</summary>
        public static readonly Point PaletteRow0Point = new(550, 157);

        /// <summary>A press outside the palette's card, inside the window (KD19).</summary>
        public static readonly Point OutsideCardPoint = new(480, 600);

        /// <summary>The palette's card (5.3): x 250 to 850, from y 96.</summary>
        public const double CardLeft = 250;

        public const double CardRight = 850;

        public const double CardTop = 96;

        /// <summary>The card's height for <paramref name="rows"/> rows shown (5.3): 56 + 28 × max(1, rows), at most 12 rows.</summary>
        public static double CardHeight(int rows) => 56 + (28 * Math.Clamp(rows, 1, 12));

        /// <summary>The box: x 259 to 841, y 105 to 137.</summary>
        public static readonly Rect BoxBounds = new(259, 105, 582, 32);

        /// <summary>The list's top (5.3).</summary>
        public const double ListTop = 143;

        /// <summary>A row's height (5.3).</summary>
        public const double RowHeight = 28;

        /// <summary>Row <paramref name="k"/>'s centre in the list as shown (5.3): y 157 + 28 k.</summary>
        public static double RowCentreY(int k) => 157 + (RowHeight * k);

        /// <summary>Where a row's label starts (the row's padding): x 269.</summary>
        public const double LabelLeft = 269;

        /// <summary>Where a row's key, or a keyless row's detail, ends: x 831.</summary>
        public const double ContentRight = 831;

        /// <summary>The gap between a row's label and its detail (5.3).</summary>
        public const double DetailGap = 8;

        /// <summary>The rows the list shows at once (5.3).</summary>
        public const int RowsInView = 12;

        /// <summary>The label's size and the detail's and key's (5.3).</summary>
        public const double LabelSize = 12.5;

        public const double DetailSize = 11.5;

        /// <summary>The empty palette's commands with nothing typed (K10 step 2): 33 on "ops", 22 on "Web"'s dashboard, 34 on web-app beside it.</summary>
        public const int OpsCommandCount = 33;

        public const int DashboardCommandCount = 22;

        public const int WebAppCommandCount = 34;

        /// <summary>The highlight after each of K10 step 2's three PageDowns: rows 11, 22 and 32 (clamped at the end).</summary>
        public static readonly int[] PageDownRows = [11, 22, 32];

        /// <summary>The row the pointer's move to <see cref="WheelPoint"/> highlights: the sixth in view, rows 21 to 32 showing.</summary>
        public const int WheelRow = 26;

        /// <summary>The first row in view after the Up that wraps to the last (rows 21 to 32 showing).</summary>
        public const int FirstRowInViewAtEnd = 21;

        /// <summary>The width of <c>Open repository web-app</c> at 12.5 (k10e's Notes).</summary>
        public const double OpenWebAppLabelWidth = 152.1;

        /// <summary>The menu layer's <c>by pointer</c> and <c>by keyboard</c> (D106).</summary>
        public const string ByPointer = "pointer";

        public const string ByKeyboard = "keyboard";

        /// <summary>A keyed item's key: right-aligned this far before the item's right edge (6.7).</summary>
        public const double MenuKeyRightInset = 10;

        /// <summary>A keyed item's key: at least this far right of its label (6.7).</summary>
        public const double MenuKeyGap = 24;

        /// <summary>The empty graph area's menu's width (K11 step 1: Fetch all remotes with its key): 4 + 10 + 114.4 + 24 + 72.4 + 10 + 4.</summary>
        public const double EmptyAreaMenuWidth = 239;

        /// <summary>The working-changes row's menu's width (K11 step 4: Stash changes… with its key): 4 + 10 + 109.1 + 24 + 73.1 + 10 + 4.</summary>
        public const double WorkingMenuWidth = 234;

        /// <summary>main's menu's width (K12 step 18: Push main to origin/main with its key): 4 + 10 + 162.9 + 24 + 73.3 + 10 + 4.</summary>
        public const double MainMenuWidth = 288;

        /// <summary>The menus' heights by 4a's formula: 8 items and 2 separators, 14 items and 5 separators.</summary>
        public const double EightItemsTwoSeparators = 258;

        public const double FourteenItemsFiveSeparators = 441;

        /// <summary>The activity log at its default height (K12 step 12): the panel y 474 to 674, its header y 474 to 504, the graph area's content up to y 469.</summary>
        public const double LogTop = 474;

        public const double LogBottom = 674;

        public const double LogHeaderBottom = 504;

        public const double GraphContentBottom = 469;

        /// <summary>An activity log row's height (K12 step 12).</summary>
        public const double LogRowHeight = 24;

        // ---- The palette's rows as the report writes them. ----

        /// <summary>
        /// One row of the palette (part K's "The palette"): its label, its detail (the item's menu
        /// header, the repository's name; for a disabled entry its item status), its key, its reason
        /// while disabled (null: enabled), and whether it is an enabled destructive entry.
        /// </summary>
        public sealed record Entry(string Label, string? Detail, string? Key, string? Reason = null, bool Danger = false)
        {
            /// <summary>It can run.</summary>
            public bool IsEnabled => Reason is null;

            /// <summary>The row as the report writes it: <c>label · detail · key</c>, then "(danger)" or "(disabled: …)".</summary>
            public override string ToString() =>
                string.Join(" · ", new[] { Label, Detail, Key }.OfType<string>())
                + (Reason is null ? string.Empty : $" (disabled: \"{Reason}\")")
                + (Danger ? " (danger)" : string.Empty);
        }

        private static Entry E(string label, string? key = null, string? detail = null) => new(label, detail, key);

        private static Entry D(string label, string? key, string reason, string? detail = null) => new(label, detail, key, reason);

        /// <summary>K10 step 2 (<c>k10a</c>): the empty palette on "ops" with one tab and no dashboard, 5.4's order (KD1), no detail on the repository's commands.</summary>
        public static readonly Entry[] OpsCommands =
        [
            E("Fetch all remotes", "Ctrl+Shift+F"),
            E("Pull", "Ctrl+Shift+L"),
            E("Push", "Ctrl+Shift+K"),
            E("Create branch…", "Ctrl+B"),
            D("Stash changes…", "Ctrl+Shift+S", NoChangesToStash),
            E("Pop the newest stash", "Ctrl+Shift+O"),
            D("Undo", "Ctrl+Z", NothingToUndo),
            D("Redo", "Ctrl+Y", NothingToRedo),
            E("Search commits", "Ctrl+F"),
            D("Go to the commit message", "Ctrl+Shift+M", NoChangesToCommit),
            E("Show reflog", "Ctrl+Shift+R"),
            E("Show backups"),
            E("Refresh", "F5"),
            E("Create tag at HEAD…"),
            E("Add remote…"),
            E("Manage remotes…"),
            D("Show all hidden branches", null, "No branch is hidden"),
            D("Stop soloing", null, "No branch is soloed"),
            E("Filter branches and tags", "Ctrl+Shift+E"),
            E("New tab", "Ctrl+T"),
            E("Close tab", "Ctrl+W", Ops),
            D("Next tab", "Ctrl+Tab", OnlyOneTab),
            D("Previous tab", "Ctrl+Shift+Tab", OnlyOneTab),
            E("Open repository…", "Ctrl+O"),
            E("Clone repository…", "Ctrl+Shift+C"),
            E("Init repository…", "Ctrl+Shift+I"),
            E("New workspace…"),
            E("Show the activity log", "Ctrl+J"),
            E("Open settings…", "Ctrl+,"),
            E("Show keyboard shortcuts…"),
            E("Choose an editor…"),
            E("Use the light theme"),
            E("Follow the system's theme"),
        ];

        /// <summary>K10 step 3 (<c>k10b</c>): <c>delete main</c>, both tier 0, the enabled first.</summary>
        public static readonly Entry[] DeleteMain =
        [
            new("Delete main on origin…", "origin/main", null, Danger: true),
            new("Delete main…", "main", null, CannotDeleteCurrentBranch),
        ];

        /// <summary>K10 step 4: <c>check out feature/alpha</c>: the local branch's item (tier 0), the remote branch's (tier 1).</summary>
        public static readonly Entry[] CheckOutRows =
        [
            new("Check out feature/alpha", "feature/alpha", null),
            new("Check out origin/feature/alpha", "origin/feature/alpha", null),
        ];

        /// <summary>K10 step 5 (<c>k10d</c>): <c>create branch</c>: the command, then the items in 5.5's source order (KD1).</summary>
        public static readonly Entry[] CreateBranch =
        [
            E("Create branch…", "Ctrl+B"),
            .. new[] { "feature/alpha", "feature/conflict", "feature/ff", "main", "origin/feature/alpha", "origin/main", "v1.0", "v1.1", "88d6b91 Main work" }
                .Select(detail => new Entry(CreateBranchHere, detail, null)),
        ];

        /// <summary>K10 step 6: the empty palette's first twelve after the session's runs (5.6): the run first, then the catalogue's order without it.</summary>
        public static readonly Entry[] AfterTheRuns =
        [
            E("Create branch…", "Ctrl+B"),
            E("Fetch all remotes", "Ctrl+Shift+F"),
            E("Pull", "Ctrl+Shift+L"),
            E("Push", "Ctrl+Shift+K"),
            D("Stash changes…", "Ctrl+Shift+S", NoChangesToStash),
            E("Pop the newest stash", "Ctrl+Shift+O"),
            E("Undo checkout of feature/alpha", "Ctrl+Z"),
            D("Redo", "Ctrl+Y", NothingToRedo),
            E("Search commits", "Ctrl+F"),
            D("Go to the commit message", "Ctrl+Shift+M", NoChangesToCommit),
            E("Show reflog", "Ctrl+Shift+R"),
            E("Show backups"),
        ];

        /// <summary>K10 step 7, before typing: the empty palette on "Web"'s dashboard, the only tab (no detail on a dashboard, KD2; Go to the next dashboard names Web, KD3).</summary>
        public static readonly Entry[] DashboardCommands =
        [
            E("Refresh", "F5"),
            E("Fetch every repository of Web", "Ctrl+Shift+F"),
            E("Pull every repository of Web", "Ctrl+Shift+L"),
            E("Add repository…"),
            D("Add open tabs", null, "No tab has a repository open"),
            E("Rename workspace…"),
            new("Delete workspace…", null, null, Danger: true),
            E("New tab", "Ctrl+T"),
            E("Close tab", "Ctrl+W", Workspace),
            D("Next tab", "Ctrl+Tab", OnlyOneTab),
            D("Previous tab", "Ctrl+Shift+Tab", OnlyOneTab),
            E("Go to the next dashboard", "Ctrl+Shift+D", Workspace),
            E("Open repository…", "Ctrl+O"),
            E("Clone repository…", "Ctrl+Shift+C"),
            E("Init repository…", "Ctrl+Shift+I"),
            E("New workspace…"),
            D("Show the activity log", "Ctrl+J", "Select a repository to see its activity"),
            E("Open settings…", "Ctrl+,"),
            E("Show keyboard shortcuts…"),
            E("Choose an editor…"),
            E("Use the light theme"),
            E("Follow the system's theme"),
        ];

        /// <summary>K10 step 8 (<c>k10f</c>): web-app's palette beside the dashboard, its first twelve: the repository's commands name it (5.4, 3.8, KD2).</summary>
        public static readonly Entry[] WebAppCommands =
        [
            E("Fetch all remotes", "Ctrl+Shift+F", WebApp),
            E("Pull", "Ctrl+Shift+L", WebApp),
            E("Push", "Ctrl+Shift+K", WebApp),
            E("Create branch…", "Ctrl+B", WebApp),
            D("Stash changes…", "Ctrl+Shift+S", NoChangesToStash, WebApp),
            D("Pop the newest stash", "Ctrl+Shift+O", "There are no stashes", WebApp),
            D("Undo", "Ctrl+Z", NothingToUndo, WebApp),
            D("Redo", "Ctrl+Y", NothingToRedo, WebApp),
            E("Search commits", "Ctrl+F", WebApp),
            D("Go to the commit message", "Ctrl+Shift+M", NoChangesToCommit, WebApp),
            E("Show reflog", "Ctrl+Shift+R", WebApp),
            E("Show backups", null, WebApp),
        ];

        /// <summary>K10 step 8, further down: the tab commands enabled (two tabs) and the next dashboard's entry.</summary>
        public static readonly Entry[] WebAppTabCommands =
        [
            E("Next tab", "Ctrl+Tab"),
            E("Previous tab", "Ctrl+Shift+Tab"),
            E("Go to the next dashboard", "Ctrl+Shift+D", Workspace),
        ];

        /// <summary>K10 step 7's item: <c>Open repository web-app</c> with its folder as saved, no key.</summary>
        public static Entry OpenWebApp(string folder) => new("Open repository web-app", folder, null);

        /// <summary>K10 step 8's item: <c>Open workspace Web</c>.</summary>
        public static Entry OpenWorkspaceWeb { get; } = new("Open workspace Web", "10 repositories", null);

        /// <summary>K12 step 25's only row: <c>New workspace…</c> (tier 1, no key).</summary>
        public static Entry NewWorkspace { get; } = E("New workspace…");

        // ---- The palette's texts (14.7, 5.3, 5.11). ----

        public const string PaletteName = "Command palette";

        public const string PaletteBoxName = "Search commands";

        public const string PaletteListName = "Commands";

        public const string Placeholder = "Search commands, branches, tags, stashes, remotes, repositories and workspaces";

        /// <summary>The placeholder's width at 12.5 (KD13).</summary>
        public const double PlaceholderWidth = 490.1;

        public const string NoMatches = "No matches";

        /// <summary>The box's starting text when Mod+Shift+D finds no dashboard (KD16).</summary>
        public const string WorkspaceQuery = "workspace";

        public const string QueryNothing = "zzz";

        public const string QueryDeleteMain = "delete main";

        public const string QueryCheckOut = "check out feature/alpha";

        public const string QueryCreateBranch = "create branch";

        public const string QueryOpenWebApp = "open repository web-app";

        public const string QueryOpenWorkspace = "open workspace web";

        public const string CreateBranchHere = "Create branch here…";

        public const string CheckOutFeatureAlphaLabel = "Check out feature/alpha";

        // ---- Reasons and other texts (14.5, 14.7, 14.11). ----

        public const string NoChangesToStash = "There are no changes to stash";

        public const string NothingToUndo = "Nothing to undo";

        public const string NothingToRedo = "Nothing to redo";

        public const string NoChangesToCommit = "There are no changes to commit";

        public const string OnlyOneTab = "There is only one tab";

        public const string CannotDeleteCurrentBranch = "You cannot delete the branch you are on";

        public const string NothingStaged = "Nothing staged";

        public const string CheckedOutAlwaysShown = "The checked-out branch is always shown";

        public const string CloseResolverFirst = "Close the conflict resolver first";

        public const string FinishMerge = "Finish or abort the merge first";

        public const string NoRepositoryOfToolsCanBePulled = "No repository of Tools can be pulled";

        /// <summary>The empty tab's welcome text (phase 1).</summary>
        public const string NoRepositoryOpen = "No repository open";

        public const string NewTabTitle = "New tab";

        /// <summary>"+"'s automation name.</summary>
        public const string NewTabButtonName = "Open a new tab";

        /// <summary>The folder dialogs' titles (5.10).</summary>
        public const string OpenRepositoryDialog = "Open a repository";

        public const string InitRepositoryDialog = "Choose a folder for the new repository";

        /// <summary>The name dialog of Create branch (phase 3).</summary>
        public const string CreateBranchTitle = "Create branch";

        public const string BranchNamePlaceholder = "Branch name";

        public const string CheckOutTheNewBranch = "Check out the new branch";

        /// <summary>The name dialog's context from HEAD's commit: Alpha two after K10's checkout, Main work in K12.</summary>
        public const string FromAlphaTwo = "From ce5ea6a \"Alpha two\"";

        public const string FromMainWork = "From 88d6b91 \"Main work\"";

        /// <summary>The stash dialog (phase 3).</summary>
        public const string StashChangesTitle = "Stash changes";

        public const string StashMessagePlaceholder = "Message (optional)";

        public const string IncludeUntracked = "Include untracked files";

        public const string StashConfirm = "Stash";

        public const string Cancel = "Cancel";

        /// <summary>The stage panel's titles and texts (phases 2 and 4).</summary>
        public static string UnstagedTitle(int count) => string.Create(CultureInfo.InvariantCulture, $"Unstaged files ({count})");

        public static string StagedTitle(int count) => string.Create(CultureInfo.InvariantCulture, $"Staged files ({count})");

        public const string NoStagedChanges = "No staged changes";

        public const string StageFilesToCommit = "Stage files to commit";

        public const string CommitOneFile = "Commit 1 file";

        /// <summary>The summary's counter: 72 less the summary's length (<c>72</c> empty, <c>63</c> with <c>Keys only</c>, <c>53</c> with <c>Note more core work</c>).</summary>
        public static string Counter(string summary) => (72 - summary.Length).ToString(CultureInfo.InvariantCulture);

        /// <summary>The changed file as the flat lists show it.</summary>
        public const string CoreName = "core.txt";

        public const string CoreFolder = "src";

        // ---- Statuses and operations (phases 2, 3 and 5). ----

        public const string CheckOutFeatureAlpha = "Check out feature/alpha";

        public const string CheckedOutFeatureAlpha = "Checked out feature/alpha";

        public const string Fetch = "Fetch";

        public const string FetchedFromAllRemotes = "Fetched from all remotes";

        public const string Pull = "Pull";

        public const string PullingIntoMain = "Pulling origin/main into main…";

        public const string MainIsUpToDate = "main is already up to date";

        public const string PushMain = "Push main";

        public const string PushedMain = "Pushed main to origin/main";

        public const string PopStash = "Pop stash@{0}";

        public const string PoppingStash = "Popping stash@{0}…";

        public const string PoppedStash = "Popped stash@{0}";

        public const string Stash = "Stash";

        public const string StashingTheChanges = "Stashing the changes…";

        public const string StashedTheChanges = "Stashed the changes";

        public const string Stage = "Stage";

        public const string CommitTitle = "Commit";

        public const string UndoCommitKeysOnly = "Undo commit \"Keys only\"";

        public const string RedoCommitKeysOnly = "Redo commit \"Keys only\"";

        public const string UndidCommitKeysOnly = "Undid commit \"Keys only\"";

        public const string RedidCommitKeysOnly = "Redid commit \"Keys only\"";

        /// <summary>The undo history's entry texts (phase 5's U20 for a stash: the stash list's message).</summary>
        public const string CheckoutOfFeatureAlpha = "checkout of feature/alpha";

        public const string CommitKeysOnly = "commit \"Keys only\"";

        public const string PopOfStash = "pop of stash \"On main: Experiment\"";

        public const string StashAgainEntry = "stash \"On main: Experiment again\"";

        /// <summary>Undo's and Redo's tooltips while they can act (6.8): <c>Undo {text} (Ctrl+Z)</c>, <c>Redo {text} (Ctrl+Y)</c>.</summary>
        public static string UndoToolTip(string text) => $"Undo {text} (Ctrl+Z)";

        public static string RedoToolTip(string text) => $"Redo {text} (Ctrl+Y)";

        // ---- Menus with their keys (part K's "Menus"; MenuSnapshot.LinesWithKeys). ----

        /// <summary>K11 step 1 (<c>k11a</c>): the empty graph area's menu on OPS0.</summary>
        public static readonly string[] EmptyAreaMenu =
        [
            Ops,
            "Fetch all remotes [Ctrl+Shift+F]",
            "Pull [Ctrl+Shift+L]",
            "Push [Ctrl+Shift+K]",
            "—",
            "Create branch… [Ctrl+B]",
            "Stash changes… [Ctrl+Shift+S] (disabled: \"There are no changes to stash\")",
            "—",
            "Show reflog [Ctrl+Shift+R]",
            "Show backups",
            "Manage remotes…",
        ];

        /// <summary>K11 step 2: the newest stash's menu: only <c>Pop stash</c> keyed.</summary>
        public static readonly string[] StashMenu =
        [
            "stash@{0} On main: Experiment",
            "Apply stash",
            "Pop stash [Ctrl+Shift+O]",
            "Drop stash… (danger)",
            "—",
            "Copy SHA",
        ];

        /// <summary>K11 step 2: another branch's push, not keyed (6.7).</summary>
        public const string PushFeatureAlpha = "Push feature/alpha to origin/feature/alpha";

        /// <summary>K11 step 4 (<c>k11c</c>): the working-changes row's menu of K-OPSw.</summary>
        public static readonly string[] WorkingChangesMenu =
        [
            "Working changes",
            "Stage all [Ctrl+Shift+A]",
            "Unstage all [Ctrl+Shift+X] (disabled: \"Nothing staged\")",
            "Discard all changes… (danger)",
            "—",
            "Stash changes… [Ctrl+Shift+S]",
            "Create branch here… [Ctrl+B]",
            "—",
            "Show reflog [Ctrl+Shift+R]",
            "Show backups",
            "Show in folder",
        ];

        public const string StageAll = "Stage all";

        /// <summary>K12 step 18 (<c>k12e</c>): main's menu, opened by Shift+F10.</summary>
        public static readonly string[] MainMenu =
        [
            "main",
            "Pull from origin/main [Ctrl+Shift+L]",
            "Pull from origin/main (rebase)",
            "—",
            "Push main to origin/main [Ctrl+Shift+K]",
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

        public const string PullFromOriginMain = "Pull from origin/main";

        public const string PushMainToOriginMain = "Push main to origin/main";

        public const string FetchAllRemotes = "Fetch all remotes";

        public const string StashChanges = "Stash changes…";

        // ---- Tooltips, help texts and accelerator keys (6.8, 14.11). ----

        /// <summary>A control's tooltip (also its help text; null for none) and its <c>AutomationProperties.AcceleratorKey</c> (null for none).</summary>
        public sealed record Keyed(string Id, bool Enabled, string? ToolTip, string? Key);

        /// <summary>K11 step 2: the toolbar on OPS0, then the tab strip's and the status bar's controls.</summary>
        public static readonly Keyed[] OpsToolbar =
        [
            new("FetchButton", true, "Fetch all remotes (Ctrl+Shift+F)", "Ctrl+Shift+F"),
            new("PullButton", true, "Pull from origin/main (Ctrl+Shift+L)", "Ctrl+Shift+L"),
            new("PushButton", true, "Push main to origin/main (Ctrl+Shift+K)", "Ctrl+Shift+K"),
            new("BranchButton", true, "Create a branch at HEAD (Ctrl+B)", "Ctrl+B"),
            new("StashButton", false, NoChangesToStash, "Ctrl+Shift+S"),
            new("PopButton", true, "Pop the newest stash (Ctrl+Shift+O)", "Ctrl+Shift+O"),
            new("UndoButton", false, NothingToUndo, "Ctrl+Z"),
            new("RedoButton", false, NothingToRedo, "Ctrl+Y"),
            new("SettingsButton", true, "Open settings (Ctrl+,)", "Ctrl+,"),
            new("SearchButton", true, "Search commits (Ctrl+F)", "Ctrl+F"),
            new("ThemeSwitch", true, "Switch to the light theme", null),
        ];

        /// <summary>K11 step 2: "+" (<c>AddRepoButton</c>, named <c>Open a new tab</c>).</summary>
        public static Keyed AddRepo { get; } = new("AddRepoButton", true, "Open, clone or init a repository (Ctrl+T)", "Ctrl+T");

        /// <summary>The active tab's close button and another tab's (6.8).</summary>
        public static Keyed CloseActiveTab { get; } = new("CloseTabButton", true, "Close this tab (Ctrl+W)", "Ctrl+W");

        public static Keyed CloseOtherTab { get; } = new("CloseTabButton", true, "Close this tab", null);

        /// <summary>The activity log toggle while the log is hidden and while it is shown.</summary>
        public static Keyed ShowLog { get; } = new("ActivityLogToggle", true, ShowActivityLog, "Ctrl+J");

        public const string ShowActivityLog = "Show every git command the app has run (Ctrl+J)";

        public const string HideActivityLog = "Hide the activity log (Ctrl+J)";

        /// <summary>K11 step 3: Stash once there are changes.</summary>
        public static Keyed StashEnabled { get; } = new("StashButton", true, "Stash your changes (Ctrl+Shift+S)", "Ctrl+Shift+S");

        /// <summary>K11 step 6: the welcome page's three buttons.</summary>
        public static readonly Keyed[] WelcomeButtons =
        [
            new("OpenRepoButton", true, "Open a repository on this computer (Ctrl+O)", "Ctrl+O"),
            new("CloneRepoButton", true, "Clone a repository from a URL (Ctrl+Shift+C)", "Ctrl+Shift+C"),
            new("InitRepoButton", true, "Create a new repository in a folder (Ctrl+Shift+I)", "Ctrl+Shift+I"),
        ];

        /// <summary>K11 step 6: the toolbar on an empty tab: Fetch to Search disabled without a tooltip, their keys still set.</summary>
        public static readonly Keyed[] EmptyTabToolbar =
        [
            new("FetchButton", false, null, "Ctrl+Shift+F"),
            new("PullButton", false, null, "Ctrl+Shift+L"),
            new("PushButton", false, null, "Ctrl+Shift+K"),
            new("BranchButton", false, null, "Ctrl+B"),
            new("StashButton", false, null, "Ctrl+Shift+S"),
            new("PopButton", false, null, "Ctrl+Shift+O"),
            new("UndoButton", false, null, "Ctrl+Z"),
            new("RedoButton", false, null, "Ctrl+Y"),
            new("SearchButton", false, null, "Ctrl+F"),
        ];

        /// <summary>K11 step 5: the commit button's tooltip with its key once it can commit (D199), and its key.</summary>
        public const string CommitOneFileToolTip = "Commit 1 file (Ctrl+Enter)";

        public const string CommitKey = "Ctrl+Enter";

        /// <summary>K13 step 2: the resolver's five buttons (6.8), their texts kept while disabled.</summary>
        public static readonly Keyed[] ResolverButtons =
        [
            new("ResolverNextConflict", true, "Next conflict (Alt+Down)", "Alt+Down"),
            new("ResolverPreviousConflict", true, "Previous conflict (Alt+Up)", "Alt+Up"),
            new("ResolverPreviousFile", false, "Previous file (Alt+Left)", "Alt+Left"),
            new("ResolverNextFile", false, "Next file (Alt+Right)", "Alt+Right"),
            new("ResolverCloseButton", true, "Close (Esc)", "Esc"),
        ];

        /// <summary>K13: a dashboard's toolbar and its Refresh (4.10, 2.5).</summary>
        public static string FetchEveryRepository(string workspace) => $"Fetch every repository of {workspace} (Ctrl+Shift+F)";

        public static string PullEveryRepository(string workspace) => $"Pull every repository of {workspace} (Ctrl+Shift+L)";

        public static string ReadAgain(string workspace) => $"Read the repositories of {workspace} again (F5)";

        // ---- The log (14.14). ----

        /// <summary>The palette's opening line, with the repository's prefix when the active tab has one.</summary>
        public static string PaletteOpened(string? repository) => Prefix(repository) + "Command palette opened";

        /// <summary>The palette's run line: <c>{repo}: Command palette ran: {label}</c> with <c> ({detail})</c>.</summary>
        public static string PaletteRan(string? repository, string label, string? detail = null) =>
            Prefix(repository) + "Command palette ran: " + label + (detail is null ? string.Empty : $" ({detail})");

        /// <summary>The start of every run line of the palette.</summary>
        public const string PaletteRanText = "Command palette ran: ";

        /// <summary>The menu layer's line (D106).</summary>
        public static string MenuOpened(string repository, string header, string by) => $"{repository}: Context menu opened: {header} by {by}";

        private static string Prefix(string? repository) => repository is null ? string.Empty : repository + ": ";

        /// <summary>The start of a read pass's line of <paramref name="workspace"/> reading <paramref name="count"/> rows (14.14): <c>Web: read 10 repositories in </c>.</summary>
        public static string ReadPassStart(string workspace, int count) =>
            string.Create(CultureInfo.InvariantCulture, $"{workspace}: read {count} {(count == 1 ? "repository" : "repositories")} in ");

        /// <summary>Fetch all's lines of "Web" (K-WEBf).</summary>
        public const string FetchAllStarted = "Web: Fetch all: started for 8 repositories, 2 skipped";

        public const string FetchAllFinished = "Web: Fetch all: Fetched 8 repositories; 2 skipped";

        public const string FetchAllResult = "Fetched 8 repositories; 2 skipped";

        // ---- Commands as the call log renders them, and git's own texts (Git 2.36.0.windows.1; 2.55.0.windows.5 the same). ----

        public const string SwitchFeatureAlpha = "git switch --no-guess --no-overwrite-ignore feature/alpha";

        public const string AddCore = "git add --all -- src/core.txt";

        public const string CommitCommand = "git commit --file=- --cleanup=whitespace";

        public const string UndoCommitUpdateRef = "git update-ref -m \"VisualCommit: undo commit \\\"Keys only\\\"\" --stdin";

        public const string RedoCommitUpdateRef = "git update-ref -m \"VisualCommit: redo commit \\\"Keys only\\\"\" --stdin";

        /// <summary>The undo's and the redo's input to <c>update-ref --stdin</c>.</summary>
        public const string UndoInput = "update refs/heads/main " + MainWorkSha + " " + KeysOnlySha;

        public const string RedoInput = "update refs/heads/main " + KeysOnlySha + " " + MainWorkSha;

        public const string PushMainCommand = "git push --progress origin refs/heads/main:refs/heads/main";

        public const string FetchOrigin = "git fetch --progress origin";

        public const string MergeFetchHead = "git merge --no-edit --no-overwrite-ignore FETCH_HEAD";

        public const string StashPushCommand = "git stash push --include-untracked --message \"Experiment again\"";

        /// <summary>The worktrees' read the palette's opening starts (5.5).</summary>
        public const string WorktreeList = "git worktree list --porcelain";

        /// <summary>The pop's command starts so (its stash named by the app).</summary>
        public const string StashPopPrefix = "git stash pop";

        /// <summary>The checkout's standard output and its standard error (key).</summary>
        public const string SwitchOutput = "Your branch is up to date with 'origin/feature/alpha'.";

        public const string SwitchKey = "Switched to branch 'feature/alpha'";

        /// <summary>The commit's standard output: the key line, then the summary of the change.</summary>
        public const string CommitKeyLine = "[main 9522dae] Keys only";

        public const string CommitSummaryLine = " 1 file changed, 1 insertion(+)";

        /// <summary>The push's standard error (key).</summary>
        public const string EverythingUpToDate = "Everything up-to-date";

        /// <summary>The pull's merge's standard output (key).</summary>
        public const string AlreadyUpToDate = "Already up to date.";

        /// <summary>The pop's output's last line (key).</summary>
        public const string DroppedStash = "Dropped stash@{0} (" + StashSha + ")";

        /// <summary>The stash's standard output (key).</summary>
        public const string SavedStash = "Saved working directory and index state On main: Experiment again";

        /// <summary>
        /// The start of every command of git that writes (part K's "No write" read as the call log's
        /// forms, beside every call of an operation): what no check step but those that run an
        /// operation may call.
        /// </summary>
        public static readonly string[] WritePrefixes =
        [
            "git add", "git commit", "git switch", "git checkout", "git push", "git fetch", "git pull", "git merge", "git rebase",
            "git cherry-pick", "git revert", "git reset", "git update-ref", "git stash push", "git stash pop", "git stash apply",
            "git stash drop", "git branch ", "git tag ", "git remote add", "git remote remove", "git remote rename", "git config --",
            "git restore", "git rm", "git mv", "git clean", "git init", "git clone", "git gc", "git maintenance",
        ];

        // ---- "graph" (GR0, phase 1; K12's Ctrl+O). ----

        /// <summary>GR0's thirteen rows' subjects, top to bottom (scenario facts 6).</summary>
        public static readonly string[] Gr0Subjects =
        [
            "Work in progress on README", "Highlight matches", "Add settings page", "Fix typo in docs", "Bump version", "Fix crash on start",
            "Add search box", "Merge branch 'feature/login'", "Update README", "Validate passwords", "Add login form", "Add app skeleton", "Initial commit",
        ];

        public const string Graph = "graph";

        // ---- "conflicts" (GC-M, FM and R1; K13 method 1, phase 4's report as phase 5's A4 uses it). ----

        public const string Conflicts = "conflicts";

        public const string NotesFile = "notes.txt";

        /// <summary>notes.txt's blob on disk at the merge's stop.</summary>
        public const string NotesBlobAtStop = "cdd7b39";

        /// <summary>The merge's other side: Theirs edits.</summary>
        public const string TheirsEditsSha = "5c77ffb0ec78abf986d121f42013eee14422e5a5";

        public const string ConflictedNotes = "UU notes.txt";

        public const string TwoConflictsLeft = "2 conflicts left";

        public const string OneConflictLeft = "1 conflict left";

        /// <summary>FM: the result as the resolver opens it on notes.txt (20 lines, two blocks).</summary>
        public static string Fm => Scenarios.ConflictsFiles.NotesMergeConflicted;

        /// <summary>R1: FM after Use ours on block 1: 16 lines, one block left.</summary>
        public static string R1 { get; } = string.Join(
            '\n',
            "line 1", "line 2 ours", "line 3", "line 4", "line 5", "line 6 theirs", "line 7", "line 8", "line 9",
            "<<<<<<< HEAD", "line 10 ours", "=======", "line 10 theirs", ">>>>>>> feature/theirs",
            "line 11", "line 12") + "\n";

        /// <summary>GC-M's rows (five lanes, no Date at 1100) under the working-changes row <c>Working changes · 1 conflict</c>.</summary>
        public static readonly CommitRow[] Gcm =
        [
            Commit("Ours edits", 0, 0, "da31c97", null, Current("main")),
            Commit("Kinds edits", 1, 1, "3a9de3e", null, "feature/kinds"),
            Commit("Pick last", 2, 2, "5c4c257", null, "feature/picks"),
            Commit("Pick conflicting", 2, 2, "01fd976", null),
            Commit("Pick clean", 2, 2, "01d2046", null),
            Commit("Second change", 3, 3, "305f7f7", null, "feature/rebase"),
            Commit("First change", 3, 3, "70e8c78", null),
            Commit("Theirs edits", 4, 4, "5c77ffb", null, "feature/theirs"),
            Commit("Base", 0, 0, "195768b", null) with { Ends = [1, 2, 3, 4] },
            Commit("Initial commit", 0, 0, "6bd579f", null),
        ];

        public const int GcmLanes = 5;

        public static WorkingChangesRow GcmWorking { get; } = new("Working changes · 1 conflict", 0, 0);

        public const string MergeBanner = "Merging: \"Merge branch 'feature/theirs'\". 1 file has conflicts.";

        // ---- "Web" and "Tools" (K-WEBf, K-WEBm, K-TOOLS, K-DOCS; K10 method 2, K13 methods 2 and 3). ----

        public const string Workspace = "Web";

        public const string WorkspaceId = "web";

        public const string Tools = "Tools";

        public const string ToolsId = "tools";

        /// <summary>"Tools"' repositories, in its order (K-TOOLS).</summary>
        public static readonly string[] ToolsRepositories = ["infra", "scratch"];

        public const string WebApp = "web-app";

        public const string Docs = "docs";

        public static string Count(int repositories) => repositories == 1 ? "1 repository" : string.Create(CultureInfo.InvariantCulture, $"{repositories} repositories");

        /// <summary>One cell of a dashboard row (part W's "A row is written"): its text, its tone's word, its tooltip and its automation name where not the text.</summary>
        public sealed record Cell(string Text, string Tone, string? ToolTip = null, string? Name = null)
        {
            public static Cell Empty { get; } = new(string.Empty, Secondary);

            public bool IsEmpty => Text.Length == 0;
        }

        /// <summary>A dashboard row: its name, its Branch cell and its four other cells.</summary>
        public sealed record Row(string Name, string Branch, Cell AheadBehind, Cell Changes, Cell State, Cell Operation);

        public const string Primary = "primary";
        public const string Secondary = "secondary";
        public const string Warning = "warning";
        public const string Danger = "danger";

        private static Cell UpToDate { get; } = new("Up to date", Secondary, "main is level with origin/main");

        private static Cell Clean { get; } = new("Clean", Secondary);

        private static Cell Arrows(int ahead, int behind)
        {
            var text = string.Join(' ', new[]
            {
                ahead > 0 ? string.Create(CultureInfo.InvariantCulture, $"↑ {ahead}") : null,
                behind > 0 ? string.Create(CultureInfo.InvariantCulture, $"↓ {behind}") : null,
            }.OfType<string>());
            var name = (ahead, behind) switch
            {
                ( > 0, > 0) => string.Create(CultureInfo.InvariantCulture, $"{ahead} ahead, {behind} behind"),
                ( > 0, _) => string.Create(CultureInfo.InvariantCulture, $"{ahead} ahead"),
                _ => string.Create(CultureInfo.InvariantCulture, $"{behind} behind"),
            };
            string Commits(int count) => count == 1 ? "1 commit" : string.Create(CultureInfo.InvariantCulture, $"{count} commits");
            var tip = (ahead, behind) switch
            {
                ( > 0, > 0) => string.Create(CultureInfo.InvariantCulture, $"{Commits(ahead)} ahead of and {behind} behind origin/main"),
                ( > 0, _) => $"{Commits(ahead)} ahead of origin/main",
                _ => $"{Commits(behind)} behind origin/main",
            };
            return new Cell(text, Secondary, tip, name);
        }

        private static Cell NoUpstream { get; } = new("No upstream", Secondary, "experiment has no upstream");

        private static Cell NoRemote { get; } = new("No remote", Secondary, NoRemotesText);

        public const string NoRemotesText = "This repository has no remotes";

        public const string DesignSystemChanges = "1 staged, 2 unstaged, 1 untracked";

        public const string OneConflict = "1 conflict";

        public const string MergeInProgress = "Merge in progress";

        public const string MergeInProgressToolTip = "Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.";

        public const string FolderNotFound = "Folder not found";

        /// <summary>A row's Operation cell for a result or a left-out row's reason (2.6, 4.2): secondary, its tooltip its text.</summary>
        private static Cell Shown(string text) => new(text, Secondary, text);

        /// <summary>Scenario facts 2.3's ten rows of "Web" as read (W0), old-site's tooltip naming its saved folder <paramref name="oldSite"/>.</summary>
        public static IReadOnlyList<Row> Web(string oldSite) =>
        [
            new("api", "main", UpToDate, Clean, Cell.Empty, Cell.Empty),
            new("web-app", "main", Arrows(2, 0), Clean, Cell.Empty, Cell.Empty),
            new("docs", "main", Arrows(0, 3), Clean, Cell.Empty, Cell.Empty),
            new("mobile", "main", Arrows(1, 2), Clean, Cell.Empty, Cell.Empty),
            new("design-system", "main", UpToDate, new Cell(DesignSystemChanges, Primary, DesignSystemChanges), Cell.Empty, Cell.Empty),
            new("billing", "main", UpToDate, new Cell(OneConflict, Danger, OneConflict), new Cell(MergeInProgress, Warning, MergeInProgressToolTip), Cell.Empty),
            new("infra", "Detached at 09d320a", Cell.Empty, Clean, Cell.Empty, Cell.Empty),
            new("scratch", "experiment", NoUpstream, Clean, Cell.Empty, Cell.Empty),
            new("notes", "main", NoRemote, Clean, Cell.Empty, Cell.Empty),
            new("old-site", string.Empty, Cell.Empty, Cell.Empty, new Cell(FolderNotFound, Warning, $"{oldSite} does not exist"), Cell.Empty),
        ];

        /// <summary>K-WEBf: "Web" after K13's Fetch all on the file remotes: docs ↓ 4, mobile ↑ 1 ↓ 3, design-system ↓ 3; every fetched row <c>Fetched from all remotes</c>, notes left out with its reason, old-site its state.</summary>
        public static IReadOnlyList<Row> WebFetched(string oldSite) =>
        [
            .. Web(oldSite).Select(row => row.Name switch
            {
                "docs" => row with { AheadBehind = Arrows(0, 4), Operation = Shown(FetchedFromAllRemotes) },
                "mobile" => row with { AheadBehind = Arrows(1, 3), Operation = Shown(FetchedFromAllRemotes) },
                "design-system" => row with { AheadBehind = Arrows(0, 3), Operation = Shown(FetchedFromAllRemotes) },
                "notes" => row with { Operation = Shown(NoRemotesText) },
                "old-site" => row,
                _ => row with { Operation = Shown(FetchedFromAllRemotes) },
            }),
        ];

        /// <summary>The order of "Web" after K13 method 3's Alt+Down (docs below mobile).</summary>
        public static readonly string[] MovedOrder = ["api", "web-app", "mobile", "docs", "design-system", "billing", "infra", "scratch", "notes", "old-site"];

        /// <summary>K-WEBm: the rows after the Alt+Down and the Delete of old-site, each as scenario facts 2.3 gives it.</summary>
        public static readonly string[] RemovedOrder = ["api", "web-app", "mobile", "docs", "design-system", "billing", "infra", "scratch", "notes"];

        /// <summary>K-TOOLS' rows once read after Web's Fetch all: the shared summary-mode view models show its results (KD12).</summary>
        public static IReadOnlyList<Row> ToolsFetched { get; } =
        [
            new("infra", "Detached at 09d320a", Cell.Empty, Clean, Cell.Empty, Shown(FetchedFromAllRemotes)),
            new("scratch", "experiment", NoUpstream, Clean, Cell.Empty, Shown(FetchedFromAllRemotes)),
        ];

        /// <summary>K-DOCS: docs' graph in its own tab (scenario facts 2.4), 4 rows, 1 lane.</summary>
        public static readonly CommitRow[] DocsGraph =
        [
            Commit("Fix the guide's typos", 0, 0, "23e2ad1", "2026-01-01 12:03", "origin/main"),
            Commit("Write the FAQ", 0, 0, "43723b6", "2026-01-01 12:02"),
            Commit("Write the install guide", 0, 0, "245235b", "2026-01-01 12:01"),
            Commit("Initial commit", 0, 0, "ec855ba", "2026-01-01 12:00", Current("main")),
        ];

        /// <summary>web-app's graph in its own tab (scenario facts 2.4), K10 step 7.</summary>
        public static readonly CommitRow[] WebAppGraph =
        [
            Commit("Style the pages", 0, 0, "cbd9037", "2026-01-01 12:02", Current("main")),
            Commit("Add the about page", 0, 0, "0d6e6c5", "2026-01-01 12:01"),
            Commit("Initial commit", 0, 0, "147c6e3", "2026-01-01 12:00", "origin/main"),
        ];

        /// <summary>The remote-tracking refs Fetch all moves (K-WEBf): docs', mobile's and design-system's origin/main.</summary>
        public static readonly (string Name, string Sha)[] FetchedOriginMain =
        [
            ("docs", "245b72964a7ee4952ba789ed47201369ab4e2f50"),
            ("mobile", "67a26608eb41dcde8cb5f32e12173c99dc741bd1"),
            ("design-system", "948f7d187454e2096a92ef2c5d7281ce72aa34f5"),
        ];

        /// <summary>The eight repositories Fetch all fetches, in row order (K-WEBf).</summary>
        public static readonly string[] Fetched = ["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch"];

        /// <summary>The nine rows with a folder (old-site has none).</summary>
        public static readonly string[] WithFolder = ["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch", "notes"];

        /// <summary>The ready status bar's text (phase 0).</summary>
        public const string Ready = "Ready";

        // ---- Pictures (part K's 17, KD18). ----

        public static readonly string[] Pictures =
        [
            "k10a-palette-commands", "k10b-palette-delete-main", "k10c-checked-out-by-palette", "k10d-create-branch-from-palette",
            "k10e-palette-open-repository", "k10f-palette-names-repository",
            "k11a-empty-area-menu-keys", "k11b-push-tooltip-key", "k11c-working-row-menu-keys", "k11d-commit-tooltip-key",
            "k12a-f6-graph", "k12b-f6-stage-row", "k12c-shift-f6-log", "k12d-f6-details-row", "k12e-branch-menu-by-keys",
            "k13a-next-conflict-tooltip", "k13b-next-dashboard",
        ];

        // ---- The platform's words. ----

        /// <summary>
        /// <paramref name="text"/> (written in the Windows and Linux words) in the words of a platform
        /// whose command modifier is Cmd when <paramref name="commandIsMeta"/> (design 6.4): the command
        /// modifier <c>Cmd</c> for <c>Ctrl</c> but where the key is Control itself (<c>Ctrl+Tab</c>,
        /// <c>Ctrl+Shift+Tab</c>, <c>Ctrl+PageDown</c>, <c>Ctrl+PageUp</c>), <c>Option</c> for
        /// <c>Alt</c>, and the shown keys a Mac shows instead (<c>Cmd+R</c> for Refresh's <c>F5</c>,
        /// <c>Cmd+Shift+Z</c> for Redo's <c>Ctrl+Y</c>). The headless platform's command modifier is
        /// Control on every platform (design 0.7), so a scripted run keeps the text as it is.
        /// </summary>
        public static string OnPlatform(string text, bool commandIsMeta)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (!commandIsMeta)
            {
                return text;
            }

            var mac = KeyWord().Replace(text, match => match.Value switch
            {
                "Ctrl+Y" => "Cmd+Shift+Z",
                "F5" => "Cmd+R",
                _ => match.Value,
            });
            mac = ControlKey().Replace(mac, "Cmd+");
            return AltKey().Replace(mac, "Option+");
        }

        /// <summary><see cref="OnPlatform(string, bool)"/> for an optional text.</summary>
        public static string? OnPlatformOrNull(string? text, bool commandIsMeta) => text is null ? null : OnPlatform(text, commandIsMeta);

        /// <summary>Redo's and Refresh's shown keys, whole words.</summary>
        [GeneratedRegex(@"(?<![\w+])(Ctrl\+Y|F5)(?![\w+])", RegexOptions.CultureInvariant)]
        private static partial Regex KeyWord();

        /// <summary><c>Ctrl+</c> as the command modifier: not before <c>Tab</c>, <c>Shift+Tab</c>, <c>PageDown</c> or <c>PageUp</c>, which keep Control.</summary>
        [GeneratedRegex(@"(?<![\w+])Ctrl\+(?!Tab\b|Shift\+Tab\b|PageDown\b|PageUp\b)", RegexOptions.CultureInvariant)]
        private static partial Regex ControlKey();

        [GeneratedRegex(@"(?<![\w+])Alt\+", RegexOptions.CultureInvariant)]
        private static partial Regex AltKey();

        // ---- Helpers. ----

        /// <summary>A commit row with its short SHA, its date as the Date column shows it (null: not asserted) and its labels in drawing order.</summary>
        public static CommitRow Commit(string message, int lane, int colour, string shortSha, string? date, params string[] labels) =>
            new(message, lane, colour, labels, ShortSha: shortSha, Date: date);

        /// <summary>A menu's items and separators, as the report writes it (the header first, "—" for a separator).</summary>
        public static (int Items, int Separators) MenuCounts(IReadOnlyList<string> menu)
        {
            ArgumentNullException.ThrowIfNull(menu);
            return (menu.Skip(1).Count(line => line != "—"), menu.Count(line => line == "—"));
        }

        /// <summary>A menu line's label: before its key, " (danger)" or " (disabled: …)".</summary>
        public static string LabelOf(string line)
        {
            ArgumentNullException.ThrowIfNull(line);
            var end = line.Length;
            foreach (var mark in new[] { " [", " (danger)", " (disabled" })
            {
                var at = line.IndexOf(mark, StringComparison.Ordinal);
                if (at >= 0)
                {
                    end = Math.Min(end, at);
                }
            }

            return line[..end];
        }

        /// <summary>A menu line's key, or null for an item without one.</summary>
        public static string? KeyOf(string line)
        {
            ArgumentNullException.ThrowIfNull(line);
            var start = line.IndexOf(" [", StringComparison.Ordinal);
            return start < 0 ? null : line[(start + 2)..line.IndexOf(']', start)];
        }
    }
}
