// Part L's expected results (docs/test-reports/phase-6.md, "Part L: the look": its conventions,
// "Scenarios and graphs of part L", "Texts these checks rely on (part L)", the rows L26 to L34 of
// "Checks" and decisions LD1 to LD24), copied from the report, not read from the app, so that the
// two are checked against each other. Compiled by the real-window pass too (its pattern
// ..\VisualCommit.VisualTests.Phase6*\Phase6Expectations*.cs takes this file from the project
// VisualCommit.VisualTests.Phase6Look): see Phase6Expectations.Common.cs for what may be used here
// (data and pure helpers over the BCL, Avalonia, Xunit, Core, Git and Testing; no App types). Key
// texts are written as literals (design 6.12).
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase6;

public static partial class Phase6Expectations
{
    /// <summary>What part L's checks (L26 to L34) compare against.</summary>
    public static class PartL
    {
        // ---- The pictures (part L's "Pictures", LD24) ------------------------------------------

        /// <summary>The sweep's pairs (L26 to L31): each is saved as <c>{name}-dark</c> and <c>{name}-light</c>.</summary>
        public static IReadOnlyList<string> Pairs { get; } =
        [
            "l26a-welcome", "l26b-clone-form",
            "l27a-drag", "l27b-three-selected", "l27c-danger-item", "l27d-disabled-tooltip", "l27e-menu-item-tooltip", "l27f-name-dialog", "l27g-push-dialog", "l27h-remotes-dialog",
            "l28a-diff", "l28b-restore-bar", "l28c-blame", "l28d-compare", "l28e-all-files", "l28f-hidden",
            "l29a-reflog", "l29b-backups",
            "l30a-dashboard", "l30b-row-menu", "l30c-scan-checklist",
            "l31a-activity-log", "l31b-delete-question", "l31c-palette", "l31d-settings-system", "l31e-settings-git", "l31f-enabled-tooltip",
            "l31g-credentials", "l31h-operation-held",
        ];

        /// <summary>L32's pairs: the long folder in both themes.</summary>
        public static IReadOnlyList<string> LongFolderPairs { get; } = ["l32a-not-a-repository", "l32b-clone-failed"];

        /// <summary>The sweep's single picture, L34's twin (11.8): Light saved and the screen opened again.</summary>
        public const string SettingsLight = "l31d-settings-light";

        /// <summary>L33's five single pictures, dark.</summary>
        public static IReadOnlyList<string> StripPictures { get; } =
            ["l33a-eight-tabs-1000", "l33b-eleven-tabs", "l33c-twelve-tabs", "l33d-fourteen-tabs-scrolled", "l33e-first-tab-in-view"];

        /// <summary>Every scripted picture of part L, in the order the checks save them: 68 (LD24).</summary>
        public static IReadOnlyList<string> Pictures { get; } =
        [
            .. Pairs.Take(Pairs.Count - 2).SelectMany(Both),
            SettingsLight,
            .. Pairs.Skip(Pairs.Count - 2).SelectMany(Both),
            .. LongFolderPairs.SelectMany(Both),
            .. StripPictures,
        ];

        /// <summary>The real window's captures of L34, each named as its scripted twin (11.8).</summary>
        public static IReadOnlyList<string> RealWindowCaptures { get; } =
            ["l31a-activity-log-light", "l31b-delete-question-light", "l31c-palette-light", SettingsLight];

        /// <summary>A pair's two picture names.</summary>
        public static IEnumerable<string> Both(string name) => [name + "-dark", name + "-light"];

        // ---- The window (part L's conventions, scenario facts 1) ------------------------------

        /// <summary>Every method's window but L33's first step (1000×700).</summary>
        public static readonly (int Width, int Height) Size = (1100, 700);

        /// <summary>L33's first step's window.</summary>
        public static readonly (int Width, int Height) NarrowSize = (1000, 700);

        /// <summary>The neutral point at 1100×700 and 1000×700: in the status bar (part L's "The neutral point").</summary>
        public static Point NeutralPoint { get; } = new(760, 687);

        /// <summary>The graph area at 1100×700 (x 260 to 700, y 88 to 674).</summary>
        public static Rect GraphArea { get; } = new(260, 88, 440, 586);

        /// <summary>Positions the report gives are asserted within this (±2), the bounds of a pair within <see cref="PairTolerance"/>.</summary>
        public const double Tolerance = 2;

        /// <summary>A pair's two pictures have the same bounds within this (part L's "The pairs").</summary>
        public const double PairTolerance = 0.5;

        /// <summary>A translucent thing (the drag pill, a backdrop) is within this per channel of its blend (part L's "Colours").</summary>
        public const int BlendTolerance = 8;

        // ---- The theme (11.4 to 11.7, 14.11, 14.14) -----------------------------------------------

        /// <summary>The start-up line of a start with System and the fake system dark (11.5).</summary>
        public const string SystemDarkStart = "Theme: System (the system's theme is dark).";

        /// <summary>The line of the follow to light (11.4).</summary>
        public const string FollowsLight = "The system's theme changed to light; the app follows it.";

        /// <summary>The line of the follow to dark (11.4).</summary>
        public const string FollowsDark = "The system's theme changed to dark; the app follows it.";

        /// <summary>The line of L31 step 7's Save (<c>UseTheme</c>).</summary>
        public const string ThemeChangedToLight = "Theme changed to Light.";

        /// <summary><c>ThemeSwitch</c>'s tooltip and help text while System shows dark (11.6).</summary>
        public const string SwitchToLightSystem = "Switch to the light theme (stops following the system)";

        /// <summary><c>ThemeSwitch</c>'s tooltip and help text while Light is chosen (11.6).</summary>
        public const string SwitchToDark = "Switch to the dark theme";

        // ---- Colours by token (part L's table; Dark and Light of the common expectations) ---------

        /// <summary>Fluent's old tooltip colours that 11.12's tokens replaced, which no picture of a tooltip may still hold (LD19).</summary>
        public static IReadOnlyList<Color> FluentToolTipColours(bool dark) =>
            dark ? [Color.Parse("#2B2B2B"), Colors.White] : [Color.Parse("#F2F2F2"), Colors.Black];

        /// <summary>C#'s keywords in the diff and blame (Dark+ / Light+, D64).</summary>
        public static Color Keyword(bool dark) => Color.Parse(dark ? "#569CD6" : "#0000FF");

        /// <summary>C#'s operators in the diff (Dark+ / Light+, D64).</summary>
        public static Color Operator(bool dark) => Color.Parse(dark ? "#D4D4D4" : "#000000");

        /// <summary>The changed words' backgrounds (<c>VcDiffAddedWordBrush</c>, <c>VcDiffRemovedWordBrush</c>).</summary>
        public static Color AddedWord(bool dark) => Color.Parse(dark ? "#2B5A3F" : "#B4E5C6");

        public static Color RemovedWord(bool dark) => Color.Parse(dark ? "#6A2D37" : "#F3BAC1");

        /// <summary>The diff's hunk headers' background (<c>VcDiffHunkBrush</c>).</summary>
        public static Color Hunk(bool dark) => Color.Parse(dark ? "#1C2130" : "#EEF0FB");

        /// <summary>A text box's background, border and focused border (<c>TextControlBackground</c>, …).</summary>
        public static Color BoxBackground(bool dark) => Color.Parse(dark ? "#272C36" : "#FFFFFF");

        public static Color BoxBorder(bool dark) => Color.Parse(dark ? "#323845" : "#D2D6DE");

        public static Color BoxFocusedBorder(bool dark) => Color.Parse(dark ? "#7C8CFF" : "#4353D8");

        /// <summary>
        /// <paramref name="top"/> at <paramref name="opacity"/> over the opaque <paramref name="under"/>,
        /// each channel rounded: the colour a translucent thing gives (the pill at 85 %, a backdrop).
        /// </summary>
        public static Color Blend(Color top, double opacity, Color under)
        {
            byte Channel(byte a, byte b) => (byte)Math.Round((opacity * a) + ((1 - opacity) * b), MidpointRounding.AwayFromZero);
            return Color.FromRgb(Channel(top.R, under.R), Channel(top.G, under.G), Channel(top.B, under.B));
        }

        /// <summary>A backdrop (<c>VcBackdropBrush</c>, black at its alpha) over <paramref name="under"/>.</summary>
        public static Color OverBackdrop(Color backdrop, Color under) => Blend(Colors.Black, backdrop.A / 255.0, under);

        /// <summary>The pill's point in L27 step 1 (0.85 × lane 3 over the selected row): dark <c>#C95967</c>, light <c>#CC5361</c>.</summary>
        public static Color PillPoint(bool dark) => Color.Parse(dark ? "#C95967" : "#CC5361");

        /// <summary>The backdrop at (300, 600) over the graph area (L27 step 6): dark <c>#08090B</c>, light <c>#999999</c>.</summary>
        public static Color BackdropPoint(bool dark) => Color.Parse(dark ? "#08090B" : "#999999");

        /// <summary>Where L27 step 6 samples the backdrop: in the graph area below its last row, left of the card, clear of every lane, label and row background.</summary>
        public static Point BackdropSample { get; } = new(300, 600);

        // ---- L26: the welcome page (13.1, 14.9, LD2, LD3) ----------------------------------------

        /// <summary>The workspace "Web" and its id.</summary>
        public const string Workspace = "Web";

        public const string WorkspaceId = "web";

        /// <summary>"Web"'s ten folder names in its order (scenario facts 2.1).</summary>
        public static IReadOnlyList<string> WebNames { get; } = ["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch", "notes", "old-site"];

        /// <summary>A workspace item's tooltip: the folder names joined by ", ", at most 8, then ", …" (13.1).</summary>
        public static string WorkspaceToolTip(IReadOnlyList<string> names) =>
            names.Count <= 8 ? string.Join(", ", names) : string.Join(", ", names.Take(8)) + ", …";

        /// <summary>The item "Web"'s tooltip: <c>api, web-app, docs, mobile, design-system, billing, infra, scratch, …</c>.</summary>
        public const string WebItemToolTip = "api, web-app, docs, mobile, design-system, billing, infra, scratch, …";

        /// <summary>A workspace's count as its item and the dashboard's header write it.</summary>
        public static string Count(int repositories) => repositories switch
        {
            0 => "No repositories",
            1 => "1 repository",
            _ => string.Create(CultureInfo.InvariantCulture, $"{repositories} repositories"),
        };

        public const string NewTab = "New tab";
        public const string NoRepositoryOpen = "No repository open";
        public const string OpenCloneOrInit = "Open, clone or init a repository to see its history.";
        public const string WorkspacesHeading = "Workspaces";
        public const string NewWorkspace = "New workspace…";
        public const string NewWorkspaceToolTip = "Create a workspace: repositories you check and update together";
        public const string RecentRepositories = "Recent repositories";
        public const string NoRepository = "No repository";

        /// <summary>
        /// The welcome page's texts in reading order (13.1, <c>VisibleTexts.In</c>) with one workspace,
        /// "Web", and the recent repositories <paramref name="recent"/> (name, folder as the session holds it).
        /// </summary>
        public static IReadOnlyList<string> WelcomeTexts(IReadOnlyList<(string Name, string Folder)> recent) =>
        [
            NoRepositoryOpen, OpenCloneOrInit, "Open", "Clone", "Init",
            WorkspacesHeading, NewWorkspace, Workspace, Count(10),
            RecentRepositories,
            .. recent.SelectMany(item => new[] { item.Name, item.Folder }),
        ];

        /// <summary>The stack's width and its left and right in the graph area at 1100 (13.1).</summary>
        public const double StackLeft = 284;

        public const double StackRight = 676;

        /// <summary>The stack's vertical centre while the page does not scroll: the graph area's, y 381 ± 1 (LD3).</summary>
        public const double StackCentreY = 381;

        /// <summary>The Workspaces section's height with <paramref name="workspaces"/> (13.1 as built: 68, or 52 + 26 N).</summary>
        public static double SectionHeight(int workspaces) => workspaces == 0 ? 68 : 52 + (26 * workspaces);

        /// <summary>The heading row's top below the buttons' row, its height, and an item's height (13.1 as built).</summary>
        public const double HeadingRowGap = 24;

        public const double HeadingRowHeight = 20;

        public const double ItemHeight = 26;

        /// <summary>The left panel's section titles as phase 0 gives them, each with its count 0 on an empty tab.</summary>
        public static IReadOnlyList<string> LeftPanelSections { get; } = ["Local branches", "Remotes", "Pull requests", "Tags", "Stashes"];

        /// <summary>The details panel's texts on an empty tab (phase 0).</summary>
        public static IReadOnlyList<string> DetailsPlaceholder { get; } = ["Commit details", "Select a commit to see its details."];

        /// <summary>The clone form's texts in reading order above its error (phase 1), under the page's heading and line.</summary>
        public static IReadOnlyList<string> CloneFormTexts { get; } =
            [NoRepositoryOpen, OpenCloneOrInit, "Clone a repository", "Repository URL", "Parent folder", "Browse", "Folder name", "Clone", "Cancel"];

        /// <summary>The toolbar's buttons that are disabled on an empty tab, with no tooltip (phase 6's L26 start).</summary>
        public static IReadOnlyList<string> DisabledOnEmptyTab { get; } =
            ["FetchButton", "PullButton", "PushButton", "BranchButton", "StashButton", "PopButton", "UndoButton", "RedoButton", "SearchButton"];

        /// <summary>The Settings button's tooltip (6.8).</summary>
        public const string SettingsToolTip = "Open settings (Ctrl+,)";

        // ---- L27: "ops" (OPS0 in part L, LD4 to LD6) ---------------------------------------------

        /// <summary>OPS0's rows: subject, node lane, colour, labels (phase 3's G0; scenario facts 6).</summary>
        public static IReadOnlyList<CommitRow> Ops0 { get; } =
        [
            new("On main: Experiment", 0, 0, ["stash@{0}"], "0e95aca", Stash: true),
            Row("Conflicting change", 1, 1, "feature/conflict") with { ShortSha = "078ef05" },
            Row("Fast-forward me", 2, 2, "feature/ff") with { ShortSha = "d93814d" },
            Row("Main work", 0, 0, Current("main"), "origin/main", "v1.1") with { ShortSha = "88d6b91" },
            Row("Alpha two", 2, 3, "feature/alpha", "origin/feature/alpha") with { ShortSha = "ce5ea6a" },
            Row("Alpha one", 2, 3) with { ShortSha = "dff8fbf" },
            Row("Add core", 0, 0, "v1.0") with { ShortSha = "8beca34" },
            Row("Initial commit", 0, 0) with { ShortSha = "4ef1f7c" },
        ];

        /// <summary>OPS0's lane count (Graph column 64, Message from x 454).</summary>
        public const int Ops0Lanes = 3;

        /// <summary>Where a row of OPS0 is clicked "in its Message column": (484, 129 + 26 r).</summary>
        public static Point OpsRowPoint(int row) => new(484, 129 + (26 * row));

        /// <summary>OPS0's nodes the pictures sample (rows 1, 2, 4, 5): lane L's centre x 406 + 16 L, row r's y 129 + 26 r.</summary>
        public static Point OpsNode(int row) => new(406 + (16 * Ops0[row].Lane), 129 + (26 * row));

        /// <summary>The short SHAs of OPS0's commits the checks name.</summary>
        public const string AlphaTwo = "ce5ea6a";
        public const string AlphaOne = "dff8fbf";
        public const string AddCore = "8beca34";
        public const string InitialCommit = "4ef1f7c";
        public const string MainWork = "88d6b91";

        /// <summary>The toolbar on OPS0 (OPS0 in part L): enabled with their tooltips, or disabled with their reasons.</summary>
        public static IReadOnlyList<(string Id, bool Enabled, string? ToolTip)> OpsToolbar { get; } =
        [
            ("FetchButton", true, "Fetch all remotes (Ctrl+Shift+F)"),
            ("PullButton", true, null),
            ("PushButton", true, null),
            ("BranchButton", true, null),
            ("StashButton", false, "There are no changes to stash"),
            ("PopButton", true, null),
            ("UndoButton", false, "Nothing to undo"),
            ("RedoButton", false, "Nothing to redo"),
            ("SearchButton", true, null),
            ("SettingsButton", true, SettingsToolTip),
            ("ThemeSwitch", true, SwitchToLightSystem),
        ];

        /// <summary><c>feature/alpha</c>'s label menu (L27 step 3, L31 step 2): 19 items and 5 separators, 571 high.</summary>
        public static IReadOnlyList<string> AlphaMenu { get; } =
        [
            "feature/alpha",
            "Check out feature/alpha",
            "Merge feature/alpha into main",
            "Merge feature/alpha into main (create a merge commit)",
            "Rebase main onto feature/alpha",
            "Interactive rebase of main onto feature/alpha…",
            "Fast-forward feature/alpha to origin/feature/alpha (disabled: \"feature/alpha is up to date with origin/feature/alpha\")",
            "—",
            "Push feature/alpha to origin/feature/alpha",
            "Force push feature/alpha… (danger)",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename feature/alpha…",
            "Delete feature/alpha… (danger)",
            "—",
            "Change upstream…",
            "Stop tracking origin/feature/alpha",
            "—",
            "Compare feature/alpha with main",
            "Show reflog of feature/alpha",
            "Hide feature/alpha",
            "Solo feature/alpha",
            "—",
            "Copy branch name",
        ];

        public const double AlphaMenuHeight = 571;

        /// <summary>The danger item L27 step 3 points at.</summary>
        public const string DeleteAlpha = "Delete feature/alpha…";

        public const string ForcePushAlpha = "Force push feature/alpha…";

        public const string FastForwardAlpha = "Fast-forward feature/alpha to origin/feature/alpha";

        /// <summary><c>main</c>'s label menu (L27 step 5; phase 5's F14 menu with phase 6's keys): 14 items and 5 separators, 441 high.</summary>
        public static IReadOnlyList<string> MainMenu { get; } =
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

        public const double MainMenuHeight = 441;

        public const string PullFromOrigin = "Pull from origin/main";
        public const string DeleteMain = "Delete main…";
        public const string CannotDeleteCurrent = "You cannot delete the branch you are on";

        /// <summary>The disabled toolbar button's reason (L27 step 4).</summary>
        public const string NoChangesToStash = "There are no changes to stash";

        /// <summary>The multi-selection panel (L27 step 2; phase 3's check 11).</summary>
        public const string ThreeSelected = "3 commits selected";

        public const string MultiSelectionHint = "Right-click a selected commit for what can be done with all of them.";

        public static IReadOnlyList<(string Sha, string Subject)> ThreeSelectedList { get; } =
            [(AlphaOne, "Alpha one"), (AddCore, "Add core"), (InitialCommit, "Initial commit")];

        /// <summary>The name dialog from the toolbar's Branch (L27 step 6; phase 3's check 23).</summary>
        public const string CreateBranch = "Create branch";

        public const string CreateBranchContext = "From 88d6b91 \"Main work\"";
        public const string BranchNamePlaceholder = "Branch name";
        public const string CheckOutTheNewBranch = "Check out the new branch";

        /// <summary>The push dialog from <c>feature/ff</c>'s menu (L27 step 7; phase 3's check 29).</summary>
        public const string PushFf = "Push feature/ff…";

        public const string PushFfTitle = "Push feature/ff";
        public const string SetAsUpstream = "Set as upstream";

        /// <summary>The remotes dialog (L27 step 8; phase 3's check 22).</summary>
        public const string ManageRemotes = "Manage remotes…";

        public const string RemotesTitle = "Remotes";

        /// <summary>A dialog card's width and left at 1100 (400: x 350 to 750; the wide card 460: x 320 to 780).</summary>
        public const double CardWidth = 400;

        public const double CardLeft = 350;

        public const double WideCardWidth = 460;

        public const double WideCardLeft = 320;

        /// <summary>What <c>git ls-remote origin</c> lists on "ops" before and after L27 (no push ran).</summary>
        public static IReadOnlyList<string> OpsRemoteHeads { get; } = ["refs/heads/feature/alpha", "refs/heads/main"];

        // ---- L28: "history" with docs/todo.md (H0t, H0d, H0r, HS1; LD7 to LD10) --------------------

        public const string TodoPath = "docs/todo.md";
        public const string Todo = "# To do\n";
        public const string ReaderPath = "src/Reader.cs";

        /// <summary>The working-changes row of H0t (display row 0, its ring in lane 0, colour 0).</summary>
        public static WorkingChangesRow WorkingRow { get; } = new("Working changes", 0, 0);

        /// <summary>H0's commit rows (phase 5's scenario facts 2), below the working-changes row.</summary>
        public static IReadOnlyList<CommitRow> H0 { get; } =
        [
            Row("Read whole lines", 0, 0, Current("main"), "origin/main") with { ShortSha = "c0bc2dd" },
            Row("Stop tracing", 0, 0) with { ShortSha = "20d7fbe" },
            Row("Write new notes", 0, 0) with { ShortSha = "e765529" },
            Row("Merge branch 'feature/readme'", 0, 0) with { ShortSha = "f513566", Merge = true },
            Row("Explain the reader", 1, 1) with { ShortSha = "c1b6375" },
            Row("Remove the notes", 0, 0) with { ShortSha = "884b462", Ends = [1] },
            Row("Compare whole lines", 1, 2, "feature/compare", "origin/feature/compare") with { ShortSha = "245c395" },
            Row("Rename the parser to reader", 0, 0) with { ShortSha = "0575c7f" },
            Row("Add the comparer", 1, 2) with { ShortSha = "d0d3c77" },
            Row("Add tracing", 0, 0) with { ShortSha = "14a5c6c", Ends = [1] },
            Row("Handle empty input", 0, 0) with { ShortSha = "365482b" },
            Row("Add the logo", 0, 0) with { ShortSha = "775113f" },
            Row("Add the parser", 0, 0) with { ShortSha = "1d5a7cf" },
            Row("Initial commit", 0, 0) with { ShortSha = "7829335" },
        ];

        /// <summary>HS1: H0t with <c>feature/compare</c> hidden and <c>main</c> soloed (12 commit rows, 2 lanes).</summary>
        public static IReadOnlyList<CommitRow> Hs1 { get; } =
        [
            Row("Read whole lines", 0, 0, Current("main"), "origin/main") with { ShortSha = "c0bc2dd" },
            Row("Stop tracing", 0, 0) with { ShortSha = "20d7fbe" },
            Row("Write new notes", 0, 0) with { ShortSha = "e765529" },
            Row("Merge branch 'feature/readme'", 0, 0) with { ShortSha = "f513566", Merge = true },
            Row("Explain the reader", 1, 1) with { ShortSha = "c1b6375" },
            Row("Remove the notes", 0, 0) with { ShortSha = "884b462", Ends = [1] },
            Row("Rename the parser to reader", 0, 0) with { ShortSha = "0575c7f" },
            Row("Add tracing", 0, 0) with { ShortSha = "14a5c6c" },
            Row("Handle empty input", 0, 0) with { ShortSha = "365482b" },
            Row("Add the logo", 0, 0) with { ShortSha = "775113f" },
            Row("Add the parser", 0, 0) with { ShortSha = "1d5a7cf" },
            Row("Initial commit", 0, 0) with { ShortSha = "7829335" },
        ];

        /// <summary>The graph's log after Hide (phase 5 design 8.2), as the call log writes it.</summary>
        public const string HideLog =
            "git log --date-order --format=%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%s --no-show-signature --no-color --encoding=UTF-8 --exclude=feature/compare --branches --exclude=origin/feature/compare --exclude=origin/HEAD --remotes --tags --ignore-missing HEAD --";

        /// <summary>The graph's log after Solo (phase 5 design 8.2).</summary>
        public const string SoloLog =
            "git log --date-order --format=%H%x1f%P%x1f%aN%x1f%aE%x1f%aI%x1f%cI%x1f%s --no-show-signature --no-color --encoding=UTF-8 --ignore-missing refs/heads/main refs/remotes/origin/main HEAD --";

        /// <summary>Where a row of H0t is clicked: (468, 155 + 26 r) below the working-changes row (y 129).</summary>
        public static Point HistoryRowPoint(int row) => new(468, 155 + (26 * row));

        public static Point HistoryWorkingRowPoint { get; } = new(468, 129);

        /// <summary>H0t's nodes of lanes 1: row 4 (colour 1) at (422, 259), row 6 (colour 2) at (422, 311).</summary>
        public static Point HistoryNode4 { get; } = new(422, 259);

        public static Point HistoryNode6 { get; } = new(422, 311);

        /// <summary>The stage panel of H0t, as "letter name folder" rows.</summary>
        public static IReadOnlyList<string> H0tUnstaged { get; } = ["A todo.md docs", "M Reader.cs src"];

        /// <summary>The stage panel of H0d, after the discard.</summary>
        public static IReadOnlyList<string> H0dUnstaged { get; } = ["A todo.md docs"];

        /// <summary><c>git status --porcelain=v1 --untracked-files=all</c> of H0t and H0r, and of H0d.</summary>
        public static IReadOnlyList<string> H0tStatus { get; } = [" M src/Reader.cs", "?? docs/todo.md"];

        public static IReadOnlyList<string> H0dStatus { get; } = ["?? docs/todo.md"];

        /// <summary>src/Reader.cs's blobs in HEAD and in the working tree.</summary>
        public const string ReaderCommittedBlob = "7061debb42034635a05bb901042e55362d23c470";

        public const string ReaderWorkingBlob = "3056624e938122ca4a98893380cdbeb2dfec5a0b";

        /// <summary>The discard's snapshot (H0d): the session's first backup and the commit plain git makes.</summary>
        public const string DiscardBackup = "refs/visualcommit/backup/discard-20260102T090001000Z";

        public const string DiscardSnapshot = "0bd36b6c3aded07487326d5d4f98d9eb72d11334";

        public const string DiscardSnapshotSubject = "VisualCommit: snapshot before discarding 1 file";

        /// <summary>The diff's two hunk headers and its toolbar's buttons (phase 2's diff view).</summary>
        public static IReadOnlyList<string> HunkHeaders { get; } = ["@@ -1,6 +1,6 @@", "@@ -19,4 +19,6 @@ public sealed class Reader"];

        /// <summary>The inline diff's lines in order (phase 5's scenario facts 2.2), each "K text" (K: space context, - removed, + added), without their indentation.</summary>
        public static IReadOnlyList<string> DiffLines { get; } =
        [
            "  namespace History;",
            "  ",
            "- /// <summary>Reads the words of a text.</summary>",
            "+ /// <summary>Reads the lines of a text.</summary>",
        ];

        /// <summary>Line 23 of the working file, whose <c>public</c> and <c>int</c> the colours sample.</summary>
        public const string Line23 = "public int Length => _text.Length;";

        public const string DiscardTitle = "Discard changes?";
        public const string DiscardReaderQuestion = "Discard the changes to Reader.cs?";
        public const string RestoreBarReader = "Discarded changes to Reader.cs.";

        /// <summary>The entry text of the discard and its operation's title, line and status (phase 5).</summary>
        public const string DiscardReaderText = "discard of Reader.cs";

        public const string UndoDiscardTitle = "Undo discard of Reader.cs";
        public const string UndidDiscard = "Undid discard of Reader.cs";
        public const string UndoDiscardToolTip = "Undo discard of Reader.cs (Ctrl+Z)";
        public const string RedoDiscardToolTip = "Redo discard of Reader.cs (Ctrl+Y)";
        public const string NothingToUndo = "Nothing to undo";
        public const string NothingToRedo = "Nothing to redo";

        /// <summary>The blame page (L28 step 3; phase 5's B6).</summary>
        public const string BlameOfReader = "Blame of Reader.cs";

        public const string InTheWorkingTree = "in the working tree";
        public const string NotCommittedYet = "Not committed yet";
        public const int BlameLines = 24;
        public const int BlameRuns = 15;

        /// <summary>The blame's runs that are not committed yet: run 1 (line 3) and run 13 (lines 22 and 23).</summary>
        public static IReadOnlyList<(int Run, int First, int Last)> UncommittedRuns { get; } = [(1, 3, 3), (13, 22, 23)];

        /// <summary>The comparison of rows 7 and 0 (L28 step 4; phase 5's F12 step 1).</summary>
        public const string CompareFrom = "From 0575c7f Rename the parser to reader";

        public const string CompareTo = "To c0bc2dd Read whole lines";
        public const string ChangedFiles4 = "Changed files (4)";

        public static IReadOnlyList<string> ComparedFiles { get; } = ["M README.md", "M notes.md docs", "M Config.cs src", "M Reader.cs src"];

        /// <summary>All files of <c>c0bc2dd</c> (L28 step 5; phase 5's B7 step 2): only Reader.cs has a letter.</summary>
        public const string AllFiles6 = "All files (6)";

        public static IReadOnlyList<string> AllFilesOfReadWholeLines { get; } = ["LICENSE", "README.md", "logo.png assets", "notes.md docs", "Config.cs src", "M Reader.cs src"];

        /// <summary>The hidden and soloed refs of HS1 (the session's <c>GraphFilters</c>).</summary>
        public static IReadOnlyList<string> Hidden { get; } = ["refs/heads/feature/compare", "refs/remotes/origin/feature/compare"];

        public static IReadOnlyList<string> Soloed { get; } = ["refs/heads/main", "refs/remotes/origin/main"];

        public const string HideCompare = "Hide feature/compare";
        public const string SoloMain = "Solo main";

        // ---- L29: "reflog" (RL0 in part L, LD11) --------------------------------------------------

        /// <summary>HEAD's reflog's selectors (36 entries, no <c>HEAD@{5}</c>), newest first, with the lost ones.</summary>
        public static IReadOnlyList<string> HeadSelectors { get; } =
            [.. Enumerable.Range(0, 37).Where(index => index != 5).Select(index => string.Create(CultureInfo.InvariantCulture, $"HEAD@{{{index}}}"))];

        public static IReadOnlyList<string> HeadLost { get; } = ["HEAD@{1}", "HEAD@{9}", "HEAD@{11}", "HEAD@{17}", "HEAD@{23}", "HEAD@{33}"];

        /// <summary>The lost marks in view and their centres' y (RL0 in part L).</summary>
        public static IReadOnlyList<(string Selector, double Y)> LostMarksInView { get; } =
            [("HEAD@{1}", 163), ("HEAD@{9}", 345), ("HEAD@{11}", 397), ("HEAD@{17}", 553)];

        public const string ReflogOfHead = "Reflog of HEAD";
        public const string Backups = "Backups";
        public const string HeadFooter = "1 entry is not shown: it names no commit that exists";
        public const string ShowReflog = "Show reflog";
        public const string ShowBackups = "Show backups";
        public const string CleanUpTitle = "Clean up backups";
        public const string CleanUpMessage = "Removed 2 backups";

        /// <summary>The list's rows from y 124, the footer y 648 to 674, rows 0 to 19 whole in view.</summary>
        public const double ReflogListTop = 124;

        public const double ReflogListBottom = 648;

        public const double ReflogFooterBottom = 674;

        /// <summary>The backups list after the clean-up at opening (phase 5's scenario facts 4.3), newest first: name and words.</summary>
        public static IReadOnlyList<(string Name, string Words)> BackupRows { get; } =
        [
            ("branch-20260101T122701000Z", "Deleted branch doomed"),
            ("reset-20260101T122401000Z", "Before a hard reset to 9dd61fd"),
            ("discard-20260101T122001000Z", "Before discarding 2 files"),
            ("reset-20260101T121901000Z", "Before a mixed reset"),
            ("remote-20260101T121801000Z", "Removed remote mirror"),
            ("remote-branch-20260101T121701000Z", "Deleted feature/gone on origin"),
            ("remote-tag-20260101T121601000Z", "Deleted tag v0.8 on origin"),
            ("tag-undone-20260101T121501000Z", "Tag v1.0-rc, its creation undone"),
            ("tag-20260101T121401000Z", "Deleted tag v0.9"),
            ("stash-20260101T121301000Z", "Dropped or popped stash \"On main: Try a README\""),
            ("detached-20260101T121101000Z", "Commits a detached HEAD left behind"),
            ("force-push-20260101T120901000Z", "Before a force push of origin/topic"),
            ("rebase-20260101T120801000Z", "Before rebasing topic"),
            ("skip-20260101T120701000Z", "Before skipping a commit"),
            ("abort-20260101T120601000Z", "Before aborting"),
        ];

        /// <summary>Backup row 14's centre's y: all 15 in view.</summary>
        public const double LastBackupRowY = 501;

        // ---- L30: the dashboard of "Web" (scenario facts 2.3, LD12, LD13) --------------------------

        /// <summary>A dashboard cell as the report writes it: its text, tone (primary, secondary, warning, danger) and tooltip.</summary>
        public sealed record Cell(string Text, string Tone, string? ToolTip = null, string? Name = null)
        {
            public static Cell Empty { get; } = new(string.Empty, Secondary);

            public bool IsEmpty => Text.Length == 0;
        }

        /// <summary>A dashboard row as the report writes it: <c>name · branch · ahead / behind · changes · state · operation</c>.</summary>
        public sealed record DashboardRow(string Name, string Branch, Cell AheadBehind, Cell Changes, Cell State, Cell Operation);

        public const string Primary = "primary";
        public const string Secondary = "secondary";
        public const string Warning = "warning";
        public const string Danger = "danger";

        private static Cell UpToDate { get; } = new("Up to date", Secondary, "main is level with origin/main");

        private static Cell Clean { get; } = new("Clean", Secondary);

        /// <summary>Scenario facts 2.3's ten rows, with old-site's folder <paramref name="oldSite"/> (<c>{root}\old-site</c>).</summary>
        public static IReadOnlyList<DashboardRow> DashboardRows(string oldSite) =>
        [
            new("api", "main", UpToDate, Clean, Cell.Empty, Cell.Empty),
            new("web-app", "main", new("↑ 2", Secondary, "2 commits ahead of origin/main", "2 ahead"), Clean, Cell.Empty, Cell.Empty),
            new("docs", "main", new("↓ 3", Secondary, "3 commits behind origin/main", "3 behind"), Clean, Cell.Empty, Cell.Empty),
            new("mobile", "main", new("↑ 1 ↓ 2", Secondary, "1 commit ahead of and 2 behind origin/main", "1 ahead, 2 behind"), Clean, Cell.Empty, Cell.Empty),
            new("design-system", "main", UpToDate, new("1 staged, 2 unstaged, 1 untracked", Primary), Cell.Empty, Cell.Empty),
            new("billing", "main", UpToDate, new("1 conflict", Danger), new("Merge in progress", Warning, "Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge."), Cell.Empty),
            new("infra", "Detached at 09d320a", Cell.Empty, Clean, Cell.Empty, Cell.Empty),
            new("scratch", "experiment", new("No upstream", Secondary, "experiment has no upstream"), Clean, Cell.Empty, Cell.Empty),
            new("notes", "main", new("No remote", Secondary, "This repository has no remotes"), Clean, Cell.Empty, Cell.Empty),
            new("old-site", string.Empty, Cell.Empty, Cell.Empty, new("Folder not found", Warning, DoesNotExist(oldSite)), Cell.Empty),
        ];

        /// <summary><c>{path} does not exist</c> (2.3).</summary>
        public static string DoesNotExist(string path) => $"{path} does not exist";

        /// <summary>The column headings at 1100 (Folder hidden, 2.6).</summary>
        public static IReadOnlyList<string> Headings { get; } = ["Repository", "Branch", "Ahead / behind", "Changes", "State", "Operation"];

        /// <summary>Dashboard row k's centre: y 165 + 26 k; old-site (row 9) is right-clicked at (66, 399).</summary>
        public static double DashboardRowCentre(int row) => 165 + (26 * row);

        public static Point OldSitePoint { get; } = new(66, 399);

        /// <summary>old-site's row menu (2.3): 9 items and 3 separators, 293 high, its top-left at (68, 401).</summary>
        public static IReadOnlyList<string> OldSiteMenu(string oldSite) =>
        [
            "old-site",
            $"Open (disabled: \"{DoesNotExist(oldSite)}\")",
            "—",
            $"Fetch (disabled: \"{DoesNotExist(oldSite)}\")",
            $"Pull (disabled: \"{DoesNotExist(oldSite)}\")",
            "—",
            $"Show in folder (disabled: \"{DoesNotExist(oldSite)}\")",
            "Copy path",
            "—",
            "Move up",
            "Move down (disabled: \"It is the last repository\")",
            "Locate…",
            "Remove from workspace",
        ];

        public const double OldSiteMenuHeight = 293;

        public static Point OldSiteMenuTopLeft { get; } = new(68, 401);

        public const string CopyPath = "Copy path";

        /// <summary>The log line of a row's menu on a dashboard (2.7).</summary>
        public static string RowMenuOpened(string name) => $"Web: Context menu opened: {name} by pointer";

        /// <summary>The header's and the toolbar's texts on "Web" (2.5, 4.10).</summary>
        public const string AddRepositoryToolTip = "Add a repository to Web";
        public const string FetchAllToolTip = "Fetch every repository of Web (Ctrl+Shift+F)";
        public const string PullAllToolTip = "Pull every repository of Web (Ctrl+Shift+L)";
        public const string RefreshToolTip = "Read the repositories of Web again (F5)";
        public const string More = "More";
        public const string Ready = "Ready";
        public const string SelectRepository = "Select a repository to see its activity";

        /// <summary>The toolbar's buttons disabled without a tooltip on a dashboard tab.</summary>
        public static IReadOnlyList<string> DisabledOnDashboard { get; } =
            ["PushButton", "BranchButton", "StashButton", "PopButton", "UndoButton", "RedoButton", "SearchButton"];

        /// <summary>The scan's checklist on the full "Web" (2.7, LD12).</summary>
        public const string AddRepositoriesTitle = "Add repositories to Web";

        public static string FoundIn(string root) => $"Found 9 repositories in {root}.";

        public static IReadOnlyList<string> Checklist { get; } = ["api", "billing", "design-system", "docs", "infra", "mobile", "notes", "scratch", "web-app"];

        public const string AlreadyInThisWorkspace = "Already in this workspace";
        public const string AddRepositories = "Add repositories";
        public const string Cancel = "Cancel";
        public const double ChecklistRowHeight = 28;
        public const double ChecklistListHeight = 232;

        // ---- L31: overlays and phase 6's screens (LD14 to LD19) -----------------------------------

        public const string FetchedFromAll = "Fetched from all remotes";
        public const string FetchCommand = "git fetch --progress origin";
        public const string HideActivityLog = "Hide the activity log (Ctrl+J)";
        public const string ActivityLog = "Activity log";
        public const string ShowReads = "Show reads";

        /// <summary>The activity log's panel, splitter and the graph area's content at its default height (phase 5's part A).</summary>
        public const double LogTop = 474;

        public const double SplitterTop = 469;

        public static Point ToggleSample { get; } = new(1008, 678);

        /// <summary>The deletion question of <c>feature/alpha</c> (L31 step 2, L34 step 3).</summary>
        public const string DeleteBranchTitle = "Delete branch?";

        public const string DeleteAlphaQuestion = "Delete the branch feature/alpha? It has commits that main does not have.";
        public const string BackupFirst = "A backup is saved first, so this can be undone.";
        public const string Delete = "Delete";

        /// <summary>The palette's placeholder and its log line (5.3, 5.8).</summary>
        public const string PalettePlaceholder = "Search commands, branches, tags, stashes, remotes, repositories and workspaces";

        public const string PaletteOpened = "ops: Command palette opened";

        /// <summary>The palette's card (5.3): 600 wide, x 250 to 850, y 96 to 488 with 12 rows; its box 582 × 32 at (259, 105).</summary>
        public static Rect PaletteCard { get; } = new(250, 96, 600, 392);

        public static Rect PaletteBox { get; } = new(259, 105, 582, 32);

        /// <summary>Palette row k's centre: y 157 + 28 k.</summary>
        public static double PaletteRowCentre(int row) => 157 + (28 * row);

        /// <summary>A palette row as L31 step 3 writes it: label, enabled, the reason in the detail's place, the key.</summary>
        public sealed record PaletteEntry(string Label, bool Enabled, string? Reason, string? Key);

        /// <summary>The palette's first 12 rows on "ops" with nothing typed (5.4, 5.7).</summary>
        public static IReadOnlyList<PaletteEntry> PaletteFirst12 { get; } =
        [
            new("Fetch all remotes", true, null, "Ctrl+Shift+F"),
            new("Pull", true, null, "Ctrl+Shift+L"),
            new("Push", true, null, "Ctrl+Shift+K"),
            new("Create branch…", true, null, "Ctrl+B"),
            new("Stash changes…", false, "There are no changes to stash", "Ctrl+Shift+S"),
            new("Pop the newest stash", true, null, "Ctrl+Shift+O"),
            new("Undo", false, "Nothing to undo", "Ctrl+Z"),
            new("Redo", false, "Nothing to redo", "Ctrl+Y"),
            new("Search commits", true, null, "Ctrl+F"),
            new("Go to the commit message", false, "There are no changes to commit", "Ctrl+Shift+M"),
            new("Show reflog", true, null, "Ctrl+Shift+R"),
            new("Show backups", true, null, null),
        ];

        public const string PaletteThirteenth = "Refresh";
        public const string UseTheDarkTheme = "Use the dark theme";
        public const string UseTheLightTheme = "Use the light theme";
        public const string FollowTheSystem = "Follow the system's theme";

        /// <summary>The settings screen's card at 1100×700 (7.1): x 120 to 980, y 40 to 660; its navigation x 140 to 316.</summary>
        public static Rect SettingsCard { get; } = new(120, 40, 860, 620);

        public const double SettingsNavLeft = 140;
        public const double SettingsNavRight = 316;

        public static IReadOnlyList<string> SettingsSections { get; } = ["General", "Git", "Tools", "Keyboard shortcuts"];

        public const string SystemFollows = "System follows your operating system's light or dark mode.";
        public const string FetchEvery = "Fetch in the background every";
        public const string OnlyActiveTab = "Only the active tab's repository; it never asks for a password.";
        public const string KeepIgnoredFiles = "Keep ignored files";

        public const string KeepIgnoredNote =
            "A checkout, merge, rebase, cherry-pick, revert, stash apply or stash pop that would overwrite or delete an ignored file does not start. Off: they run as git runs them, which can overwrite or delete such a file without a word; checking out an older commit of the current branch, Undo and Redo still keep it.";

        public const string IncludeUntracked = "Include untracked files when stashing";
        public const string StashNote = "The Stash changes dialog starts with this; its own check box changes it too.";
        public const string RunnableIntro = "Open file asks first before it opens a file with one of these extensions:";
        public const string RunnableList = ".exe .bat .cmd .com .ps1 .vbs .vbe .js .jse .wsf .hta .msi .scr .pif .cpl .lnk .url .reg .jar .app .command .sh .desktop";
        public const string RunnablePathExt = "From PATHEXT: .wsh .msc";
        public const string RunnablePlaceholder = ".py .pl";
        public const string RunnableUnix = "On Linux and macOS it also asks before it opens a file marked as a program.";

        /// <summary>The Git section on <c>All repositories (global)</c> (7.4, D200).</summary>
        public const string AllRepositoriesGlobal = "All repositories (global)";

        public const string ThisRepository = "This repository";
        public const string GlobalCaption = "Saved in your global git configuration, which git in a terminal uses too.";
        public const string NotSet = "Not set";

        public static IReadOnlyList<string> PullModes { get; } =
            ["Not set", "Merge (fast-forward when possible)", "Merge, always with a merge commit", "Rebase", "Rebase, keeping merge commits", "Fast-forward only"];

        public const string MergeToolNote = "Used by Merge tool… in the conflict resolver, and by git mergetool in a terminal.";

        /// <summary>The scope's tooltip: <c>{name} — {folder}</c> (7.4).</summary>
        public static string RepositoryScopeToolTip(string name, string folder) => $"{name} — {folder}";

        /// <summary>The merge tool's Pick dialog (7.7, 8.10, 12.9's fixed catalogue): text and detail of each row.</summary>
        public const string ChooseAMergeTool = "Choose a merge tool";

        public const string Filter = "Filter";

        public static IReadOnlyList<(string Text, string Detail)> MergeTools { get; } =
        [
            ("Not set", "Merge tool… stays disabled"),
            ("kdiff3", "found on this computer"),
            ("meld", "found on this computer"),
            ("araxis", "not found on this computer"),
            ("bc", "not found on this computer"),
            ("p4merge", "not found on this computer"),
            ("winmerge", "not found on this computer"),
        ];

        public const string Choose = "Choose";

        /// <summary>Fetch's tooltip with its key (L31 step 6, 6.8, D215).</summary>
        public const string FetchToolTip = "Fetch all remotes (Ctrl+Shift+F)";

        public const string FetchKey = "Ctrl+Shift+F";

        // ---- L31 part B: "auth" and its held fetch (AUTH0, LD18) ------------------------------------

        /// <summary>The port part L's server listens on (47394, the lead's ruling of 2026-10-22).</summary>
        public const int LookServerPort = AuthRemoteServer.Phase6LookVisualChecksPort;

        /// <summary>
        /// <paramref name="text"/> with the server's port in place of 47394 (<see cref="LookServerPort"/>):
        /// part L's own <c>WithPort</c>, since the common one stands for part W's 47393 (D97, design 17.2).
        /// </summary>
        public static string WithLookPort(string text, int port)
        {
            ArgumentNullException.ThrowIfNull(text);
            return text.Replace(
                LookServerPort.ToString(CultureInfo.InvariantCulture),
                port.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        public const string CredentialsTitle = "Sign in to 127.0.0.1:47394";
        public const string CredentialsText = "Git needs a user name and a password or token for http://127.0.0.1:47394.";
        public const string UserName = "User name";
        public const string Password = "Password";
        public const string SignIn = "Sign in";
        public const string FetchOrigin = "Fetch origin";
        public const string HeldAt60 = "Fetching from origin… Counting objects 60%";
        public const string FetchingFromOrigin = "Fetching from origin…";
        public const string OneWaiting = "1 waiting";
        public const string StopGit = "Stop git";
        public const string FetchOriginCancelled = "Fetch origin cancelled";
        public const string FetchingFromAll = "Fetching from all remotes…";
        public const string FetchCancelled = "Fetch cancelled";
        public const int HoldPercent = 60;
        public const string AuthInitial = "e4a56bd";
        public const string AuthStatus = "## main...origin/main";

        // ---- L32: the long folder (13.2, LD20) ------------------------------------------------------

        public static string NotARepository(string folder) => $"{folder} is not a git repository.";

        /// <summary>The cleaned clone error on Git 2.36 and 2.55 (the four lines, <c>{path}</c> the URL's path).</summary>
        public static IReadOnlyList<string> CloneErrorLines(string path) =>
        [
            $"fatal: '{path}' does not appear to be a git repository",
            "fatal: Could not read from remote repository.",
            "Please make sure you have the correct access rights",
            "and the repository exists.",
        ];

        /// <summary>The key line's end on every git (part L's "Git's own texts").</summary>
        public const string CloneKeyEnd = "does not appear to be a git repository";

        // ---- L33: the tab strip (13.3, 13.4, LD21, LD22) -------------------------------------------

        /// <summary>L33's session: the dashboard, then seven repositories of natural width 120, scratch active (LD21).</summary>
        public static IReadOnlyList<string> StripStart { get; } = ["Web", "api", "web-app", "docs", "mobile", "billing", "infra", "scratch"];

        public const int StripActive = 7;

        /// <summary>The strip's room: the window's width less 38 ("+" 34 wide with 2 either side).</summary>
        public static double Room(double width) => width - 38;

        /// <summary>The natural width of a repository tab (clamp(51 + title, 120, 220)) and of a dashboard tab (clamp(69 + title, 120, 220)).</summary>
        public static double NaturalWidth(double title, bool dashboard) => Math.Clamp((dashboard ? 69 : 51) + title, 120, 220);

        /// <summary>
        /// 13.3's widths for tabs of natural widths <paramref name="natural"/> in a room of
        /// <paramref name="room"/>: the natural widths while they fit; else, while 80 each fits, the
        /// largest cap c with Σ min(n, c) = room, each tab min(n, ⌊c⌋) and the pixels left over one
        /// each to the leftmost tabs at the cap; else 80 each (and the strip scrolls).
        /// </summary>
        public static IReadOnlyList<double> TabWidths(IReadOnlyList<double> natural, double room)
        {
            ArgumentNullException.ThrowIfNull(natural);
            if (natural.Sum() <= room)
            {
                return [.. natural];
            }

            if (80 * natural.Count > room)
            {
                return [.. natural.Select(_ => 80d)];
            }

            // The cap: tabs below it keep their width, the rest share what is left.
            var sorted = natural.Order().ToList();
            double cap = 0;
            for (var below = 0; below <= sorted.Count; below++)
            {
                var rest = sorted.Count - below;
                var candidate = (room - sorted.Take(below).Sum()) / rest;
                if (below == sorted.Count || candidate <= sorted[below])
                {
                    cap = candidate;
                    break;
                }
            }

            var floor = Math.Floor(cap);
            var widths = natural.Select(width => Math.Min(width, floor)).ToList();
            var left = (int)Math.Round(room - widths.Sum());
            for (var index = 0; index < widths.Count && left > 0; index++)
            {
                if (natural[index] >= cap)
                {
                    widths[index]++;
                    left--;
                }
            }

            return widths;
        }

        /// <summary>The scrolled offset that brings the active tab whole into view from an offset of 0 (13.3: as little as it takes).</summary>
        public static double OffsetFor(IReadOnlyList<double> widths, double room, int active)
        {
            ArgumentNullException.ThrowIfNull(widths);
            var right = widths.Take(active + 1).Sum();
            return Math.Max(0, right - room);
        }

        /// <summary>The report's widths of the states TS8 to TS14 at 1000 or 1100 (LD21's table), each tab left to right.</summary>
        public static IReadOnlyList<double> Ts8 { get; } = [.. Enumerable.Repeat(120d, 8)];

        public static IReadOnlyList<double> Ts9 { get; } = [.. Enumerable.Repeat(118d, 9)];

        public static IReadOnlyList<double> Ts10 { get; } = [107, 107, .. Enumerable.Repeat(106d, 8)];

        public static IReadOnlyList<double> Ts11 { get; } = [.. Enumerable.Repeat(97d, 6), .. Enumerable.Repeat(96d, 5)];

        public static IReadOnlyList<double> Ts12 { get; } = [.. Enumerable.Repeat(89d, 6), .. Enumerable.Repeat(88d, 6)];

        public static IReadOnlyList<double> Ts13 { get; } = [.. Enumerable.Repeat(82d, 9), .. Enumerable.Repeat(81d, 4)];

        public static IReadOnlyList<double> Ts14 { get; } = [.. Enumerable.Repeat(80d, 14)];

        /// <summary>TS14's offset with the last tab active (58), and its first tab's last 22 px.</summary>
        public const double Ts14Offset = 58;

        /// <summary>"+" at 1000 (x 962 to 996) while 8 tabs of 120 fit, and at 1100 from TS9 on (x 1064 to 1098).</summary>
        public static Rect PlusAt1000 { get; } = new(962, 0, 34, 36);

        public static Rect PlusAt1100Shrunk { get; } = new(1064, 0, 34, 36);

        /// <summary>"+"'s name and tooltip (13.4, 6.8).</summary>
        public const string OpenANewTab = "Open a new tab";

        public const string PlusToolTip = "Open, clone or init a repository (Ctrl+T)";
        public const string DashboardTabId = "DashboardTab";
        public const string RepoTabId = "RepoTab";
        public const string ItemStatusWorkspace = "Workspace";

        /// <summary>The palette's queries of L33 step 2 (5.10).</summary>
        public const string OpenDesignSystem = "open repository design-system";

        public const string OpenNotes = "open repository notes";
    }
}
