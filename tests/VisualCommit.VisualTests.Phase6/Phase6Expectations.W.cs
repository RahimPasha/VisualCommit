// Part W's expected results (docs/test-reports/phase-6.md, "Part W: workspaces and the dashboard"
// and the rows W1 to W9 of "Checks"), copied from the report: the workspace "Web" and its
// repositories as scenario facts 2.2 give them (SHAs, refs, statuses), the dashboard's rows W0, WF,
// WP, WL and their cells' texts, the texts of design 14 the checks rely on (the welcome page, the
// name dialog, the deletion, the checklist, the header, the run, the menus, the reasons, the log
// lines), the commands the app runs and git's own texts of the fetches, the pulls, the missing
// remote and the undo (Git 2.36.0.windows.1's, asserted word for word only on that git, phase 3's
// rule), W8's commit, and the pictures' names. Data only: only part W's writer edits this file, its
// members live in Phase6Expectations.PartW, and it is compiled by the real-window pass too, so it
// uses no type of the app or of the walk-through's own files (a cell's tone is its word,
// "primary", "secondary", "warning" or "danger"; the checks turn it into the app's tone).
using System.Globalization;
using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase6;

public static partial class Phase6Expectations
{
    /// <summary>What part W's checks (W1 to W9) compare against.</summary>
    public static class PartW
    {
        // ---- The workspace "Web" (Scenarios.WorkspaceAsync, TempWorkspace; scenario facts 2.1). ----

        /// <summary>The workspace's name, as <c>TempWorkspace.ToWorkspace()</c> writes it and W1 types it (WD9).</summary>
        public const string Workspace = "Web";

        /// <summary>Its id in <c>workspaces.json</c> as the checks write it.</summary>
        public const string WorkspaceId = "web";

        /// <summary>W1's new name (WD16).</summary>
        public const string RenamedWorkspace = "Web team";

        /// <summary>The ten repositories in the dashboard's order (W0).</summary>
        public static readonly string[] Names = ["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch", "notes", "old-site"];

        /// <summary>The nine repositories with a folder, in the dashboard's order.</summary>
        public static readonly string[] WithFolder = ["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch", "notes"];

        /// <summary>The eight repositories with a remote, in the dashboard's order.</summary>
        public static readonly string[] WithRemote = ["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch"];

        /// <summary><c>{root}</c>'s folders as scenario facts 2.1 list them, in ordinal order.</summary>
        public static readonly string[] RootFolders = ["api", "billing", "design-system", "docs", "home", "infra", "mobile", "notes", "remotes", "scratch", "web-app"];

        /// <summary>The bare remotes in <c>{root}/remotes</c>, in ordinal order.</summary>
        public static readonly string[] RemoteFolders = ["api.git", "billing.git", "design-system.git", "docs.git", "infra.git", "mobile.git", "scratch.git", "web-app.git"];

        /// <summary>The scan's checklist (scenario facts 2.7): the nine by relative path, ordinal ignoring case; each its name, relative path and name alike.</summary>
        public static readonly string[] Checklist = ["api", "billing", "design-system", "docs", "infra", "mobile", "notes", "scratch", "web-app"];

        /// <summary>WA: W1's nine rows in the order they were added (api by Add repository…, then the checklist's eight).</summary>
        public static readonly string[] WaOrder = ["api", "billing", "design-system", "docs", "infra", "mobile", "notes", "scratch", "web-app"];

        /// <summary>WB8: the written "Web" less web-app and docs (WD19).</summary>
        public static readonly string[] Wb8Order = ["api", "mobile", "design-system", "billing", "infra", "scratch", "notes", "old-site"];

        /// <summary>WB10: WB8 with web-app and docs appended in tab order.</summary>
        public static readonly string[] Wb10Order = ["api", "mobile", "design-system", "billing", "infra", "scratch", "notes", "old-site", "web-app", "docs"];

        /// <summary>WR: W0 without old-site.</summary>
        public static readonly string[] WrOrder = ["api", "web-app", "docs", "mobile", "design-system", "billing", "infra", "scratch", "notes"];

        /// <summary>WM: WR with docs moved up one place.</summary>
        public static readonly string[] WmOrder = ["api", "docs", "web-app", "mobile", "design-system", "billing", "infra", "scratch", "notes"];

        /// <summary>The folder W2 makes for Locate… (WD7): <c>{root}/archive/old-site</c>, a file-by-file copy of <c>{root}/notes</c>.</summary>
        public static string LocatedFolder(string root) => Path.Combine(root, "archive", "old-site");

        // ---- The repositories' commits and refs (scenario facts 2.2). ----

        public const string ApiInitial = "e1d2947c77c56bc945a29150b7a8bc3b0845393c";
        public const string ApiHealth = "2248c9dd04d8a32c1aff7c14dfbbf832a1104d70";
        public const string WebAppInitial = "147c6e39738519f76b29747ef00e42a57903d718";
        public const string WebAppAbout = "0d6e6c54821ad03b4c1856172f30d66c82cb45e6";
        public const string WebAppStyle = "cbd9037ee561a55b59ae926291397798d07649b4";
        public const string DocsInitial = "ec855bae3aa27bbf3f546b0ec7934cca41d65c01";
        public const string DocsInstall = "245235b4bc9cde2f2136b0046248ec77dd9dfffe";
        public const string DocsFaq = "43723b691dbb278618338f433fcb205b965b961f";
        public const string DocsTypos = "23e2ad1a7d93c6b71c2a4ea032fab8c136037672";
        public const string DocsChangelog = "245b72964a7ee4952ba789ed47201369ab4e2f50";
        public const string MobileInitial = "617d476ca326993202795ad542db9b97cd607b27";
        public const string MobileLogin = "a17bccbba4b8747b9f63c7b2a4526807331f804c";
        public const string MobileSettings = "f419c6e88b913030d5f8bb4e9a279b067e47170e";
        public const string MobileName = "21b8aeb0563a099a1a0bae837707ac45e2ba3bc0";
        public const string MobileAbout = "67a26608eb41dcde8cb5f32e12173c99dc741bd1";
        public const string DesignInitial = "98ba31338d5e916371f9f77a97fc11686deec5e9";
        public const string DesignEven = "948f7d187454e2096a92ef2c5d7281ce72aa34f5";
        public const string BillingInitial = "fb577bdb1bf5b4965bf0088f26748eda7f466a7a";
        public const string BillingTax = "8236f530eac42c26fdf90b73daa64df549f264fd";
        public const string BillingRound = "0e943ed7edee1c1345a7a02b1b11ef89bac5c093";
        public const string InfraInitial = "09d320a17f4690c9317493bf1d77ff7e3f7209c7";
        public const string InfraStaging = "229cea5552f11d6279c15a5497fd9ebb4e54fa6f";
        public const string ScratchInitial = "42a778beba5311150c8b7c392b54079db8ee24c1";
        public const string ScratchIdea = "ba189765ad74fcaff7fc9262f34e332cdfa8ceed";
        public const string NotesInitial = "bd355fa96270d3eebf738e2bde71fc45e4487ad8";
        public const string NotesReading = "937a43bda4c1286a93ea7ff081f40d54385d595a";

        /// <summary>
        /// Each repository's refs as built, as <c>git for-each-ref --format="%(refname) %(objectname)"</c>
        /// lists them (scenario facts 2.2): W0's, and every ref but the three fetched ones of WF.
        /// </summary>
        public static IReadOnlyDictionary<string, string[]> W0Refs { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["api"] = ["refs/heads/main " + ApiHealth, "refs/remotes/origin/main " + ApiHealth],
            ["web-app"] = ["refs/heads/main " + WebAppStyle, "refs/remotes/origin/main " + WebAppInitial],
            ["docs"] = ["refs/heads/main " + DocsInitial, "refs/remotes/origin/main " + DocsTypos],
            ["mobile"] = ["refs/heads/main " + MobileName, "refs/remotes/origin/main " + MobileSettings],
            ["design-system"] = ["refs/heads/main " + DesignInitial, "refs/remotes/origin/main " + DesignInitial],
            ["billing"] = ["refs/heads/feature/tax " + BillingTax, "refs/heads/main " + BillingRound, "refs/remotes/origin/main " + BillingRound],
            ["infra"] = ["refs/heads/main " + InfraStaging, "refs/remotes/origin/main " + InfraStaging, "refs/tags/v1.0 " + InfraInitial],
            ["scratch"] = ["refs/heads/experiment " + ScratchIdea, "refs/heads/main " + ScratchInitial, "refs/remotes/origin/main " + ScratchInitial],
            ["notes"] = ["refs/heads/main " + NotesReading],
        };

        /// <summary>WF's refs (scenario facts 2.5): W0's, with docs', mobile's and design-system's <c>origin/main</c> fetched.</summary>
        public static IReadOnlyDictionary<string, string[]> WfRefs { get; } = new Dictionary<string, string[]>(W0Refs, StringComparer.Ordinal)
        {
            ["docs"] = ["refs/heads/main " + DocsInitial, "refs/remotes/origin/main " + DocsChangelog],
            ["mobile"] = ["refs/heads/main " + MobileName, "refs/remotes/origin/main " + MobileAbout],
            ["design-system"] = ["refs/heads/main " + DesignInitial, "refs/remotes/origin/main " + DesignEven],
        };

        /// <summary>WP's refs (scenario facts 2.6): WF's, with docs' <c>main</c> fast-forwarded to <c>origin/main</c>.</summary>
        public static IReadOnlyDictionary<string, string[]> WpRefs { get; } = new Dictionary<string, string[]>(WfRefs, StringComparer.Ordinal)
        {
            ["docs"] = ["refs/heads/main " + DocsChangelog, "refs/remotes/origin/main " + DocsChangelog],
        };

        /// <summary>The refs after W5 part B's run: W0's, with mobile's and design-system's <c>origin/main</c> fetched and docs' not.</summary>
        public static IReadOnlyDictionary<string, string[]> W5PartBRefs { get; } = new Dictionary<string, string[]>(W0Refs, StringComparer.Ordinal)
        {
            ["mobile"] = WfRefs["mobile"],
            ["design-system"] = WfRefs["design-system"],
        };

        /// <summary>The refs after W7's undo of docs' pull: WP's, with docs' <c>main</c> back at Initial commit.</summary>
        public static IReadOnlyDictionary<string, string[]> UndoneRefs { get; } = new Dictionary<string, string[]>(WpRefs, StringComparer.Ordinal)
        {
            ["docs"] = ["refs/heads/main " + DocsInitial, "refs/remotes/origin/main " + DocsChangelog],
        };

        /// <summary>
        /// <c>git status --porcelain=v2 --branch</c> of each repository as built, less <c>branch.oid</c>
        /// (scenario facts 2.2): a line written with "…" stands for a line that starts with what is
        /// before it and ends with what is after it (the modes and object names between).
        /// </summary>
        public static IReadOnlyDictionary<string, string[]> W0Status { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["api"] = ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -0"],
            ["web-app"] = ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +2 -0"],
            ["docs"] = ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -3"],
            ["mobile"] = ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +1 -2"],
            ["design-system"] = ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -0", "1 M. … colours.txt", "1 .D … fonts.txt", "1 .M … spacing.txt", "? icons.txt"],
            ["billing"] = ["# branch.head main", "# branch.upstream origin/main", "# branch.ab +0 -0", "u UU … invoice.txt"],
            ["infra"] = ["# branch.head (detached)"],
            ["scratch"] = ["# branch.head experiment"],
            ["notes"] = ["# branch.head main"],
        };

        /// <summary>billing's <c>MERGE_HEAD</c> (scenario facts 2.2).</summary>
        public const string BillingMergeHead = BillingTax;

        // ---- A dashboard row as the report writes it (part W's "A row is written"). ----

        /// <summary>
        /// One cell: its text (empty for none; the Ahead / behind cell's arrows as <c>↑ 2</c>,
        /// <c>↓ 3</c>, <c>↑ 1 ↓ 2</c>), its tone's word (<c>primary</c>, <c>secondary</c>,
        /// <c>warning</c>, <c>danger</c>), its tooltip (null for none) and the automation name where it
        /// is not the text (the arrows' <c>1 ahead, 2 behind</c>).
        /// </summary>
        public sealed record Cell(string Text, string Tone, string? ToolTip = null, string? Name = null)
        {
            /// <summary>An empty cell.</summary>
            public static Cell Empty { get; } = new(string.Empty, Secondary);

            /// <summary>The cell shows nothing.</summary>
            public bool IsEmpty => Text.Length == 0;
        }

        /// <summary>A row: its name, its Branch cell's text (primary, WD34) and its four other cells.</summary>
        public sealed record Row(string Name, string Branch, Cell AheadBehind, Cell Changes, Cell State, Cell Operation);

        public const string Primary = "primary";
        public const string Secondary = "secondary";
        public const string Warning = "warning";
        public const string Danger = "danger";

        /// <summary><c>Up to date</c> with its tooltip.</summary>
        public static Cell UpToDate(string branch = "main") => new("Up to date", Secondary, $"{branch} is level with origin/main");

        /// <summary><c>Clean</c>.</summary>
        public static Cell Clean { get; } = new("Clean", Secondary);

        /// <summary>The arrows: <c>↑ a</c> and <c>↓ b</c>, each only above 0, with the automation name and tooltip of 2.6.</summary>
        public static Cell Arrows(int ahead, int behind)
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

        /// <summary>A secondary Operation cell whose tooltip is its text (a left-out row's reason, 4.2).</summary>
        public static Cell Reason(string text) => new(text, Secondary, text);

        /// <summary>
        /// W0, the ten rows as read (scenario facts 2.3, part W's W0): old-site's tooltip names the
        /// saved folder <paramref name="oldSite"/> (<c>{root}\old-site</c>).
        /// </summary>
        public static IReadOnlyList<Row> W0(string oldSite) =>
        [
            new("api", "main", UpToDate(), Clean, Cell.Empty, Cell.Empty),
            new("web-app", "main", Arrows(2, 0), Clean, Cell.Empty, Cell.Empty),
            new("docs", "main", Arrows(0, 3), Clean, Cell.Empty, Cell.Empty),
            new("mobile", "main", Arrows(1, 2), Clean, Cell.Empty, Cell.Empty),
            new("design-system", "main", UpToDate(), new Cell(DesignSystemChanges, Primary, DesignSystemChanges), Cell.Empty, Cell.Empty),
            new("billing", "main", UpToDate(), new Cell(OneConflict, Danger, OneConflict), new Cell(MergeInProgress, Warning, MergeInProgressToolTip), Cell.Empty),
            new("infra", "Detached at 09d320a", Cell.Empty, Clean, Cell.Empty, Cell.Empty),
            new("scratch", "experiment", new Cell("No upstream", Secondary, "experiment has no upstream"), Clean, Cell.Empty, Cell.Empty),
            new("notes", "main", NoRemote, Clean, Cell.Empty, Cell.Empty),
            new("old-site", string.Empty, Cell.Empty, Cell.Empty, new Cell(FolderNotFound, Warning, DoesNotExist(oldSite)), Cell.Empty),
        ];

        /// <summary>W0's row <paramref name="name"/>.</summary>
        public static Row W0Row(string name, string oldSite) => W0(oldSite).Single(row => row.Name == name);

        /// <summary>notes' Ahead / behind cell: <c>No remote</c> (WD4).</summary>
        public static Cell NoRemote { get; } = new("No remote", Secondary, NoRemotesText);

        public const string DesignSystemChanges = "1 staged, 2 unstaged, 1 untracked";
        public const string OneConflict = "1 conflict";
        public const string MergeInProgress = "Merge in progress";
        public const string MergeInProgressToolTip = "Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.";
        public const string FolderNotFound = "Folder not found";
        public const string NoRemotesText = "This repository has no remotes";

        /// <summary>A folder state's tooltip and a disabled item's reason (14.5): <c>{path} does not exist</c>.</summary>
        public static string DoesNotExist(string path) => $"{path} does not exist";

        /// <summary>WL: old-site read again at its new place (W2 step 5): notes' cells under the name <c>old-site</c>.</summary>
        public static Row Located { get; } = new("old-site", "main", NoRemote, Clean, Cell.Empty, Cell.Empty);

        /// <summary>WF's Ahead / behind cells (W4 step 3): docs ↓ 4, mobile ↑ 1 ↓ 3, design-system ↓ 3.</summary>
        public static IReadOnlyDictionary<string, Cell> WfAheadBehind { get; } = new Dictionary<string, Cell>(StringComparer.Ordinal)
        {
            ["docs"] = Arrows(0, 4),
            ["mobile"] = Arrows(1, 3),
            ["design-system"] = Arrows(0, 3),
        };

        // ---- The dashboard's texts (design 2.5, 2.6, 2.7, 4.9, 4.10, 14). ----

        /// <summary>The header's count (2.5).</summary>
        public static string Count(int repositories) => repositories switch
        {
            0 => "No repositories",
            1 => "1 repository",
            _ => string.Create(CultureInfo.InvariantCulture, $"{repositories} repositories"),
        };

        public static string AddRepositoryToolTip(string workspace) => $"Add a repository to {workspace}";

        public static string FetchAllToolTip(string workspace) => $"Fetch every repository of {workspace} (Ctrl+Shift+F)";

        public static string PullAllToolTip(string workspace) => $"Pull every repository of {workspace} (Ctrl+Shift+L)";

        public static string RefreshToolTip(string workspace) => $"Read the repositories of {workspace} again (F5)";

        public static string NoRepositories(string workspace) => $"{workspace} has no repositories";

        public static string EmptyText(string workspace) => $"{workspace} has no repositories yet.";

        public const string WaitForFetchAll = "Wait for Fetch all to finish";
        public const string WaitForPullAll = "Wait for Pull all to finish";
        public const string CancelRestOfFetchAll = "Cancel the rest of Fetch all";
        public const string More = "More";
        public const string AddRepository = "Add repository…";
        public const string AddOpenTabs = "Add open tabs";
        public const string RenameWorkspace = "Rename workspace…";
        public const string DeleteWorkspace = "Delete workspace…";
        public const string NoTabHasARepositoryOpen = "No tab has a repository open";
        public const string EveryOpenRepositoryIsIn = "Every open repository is in this workspace";

        /// <summary>The column headings at 1100 (no Folder below 1280 wide, 2.6), left to right.</summary>
        public static readonly string[] Headings1100 = ["Repository", "Branch", "Ahead / behind", "Changes", "State", "Operation"];

        /// <summary>The column headings at 1920, left to right.</summary>
        public static readonly string[] Headings1920 = ["Repository", "Folder", "Branch", "Ahead / behind", "Changes", "State", "Operation"];

        /// <summary>The columns at 1100 (scenario facts 1): each heading's column, its left and right edge.</summary>
        public static readonly (string Column, double Left, double Right)[] Columns1100 =
        [
            ("Repository", 0, 132), ("Branch", 132, 282), ("Ahead / behind", 282, 412), ("Changes", 412, 632), ("State", 632, 812), ("Operation", 812, 1100),
        ];

        /// <summary>The columns at 1920 (scenario facts 1, W3).</summary>
        public static readonly (string Column, double Left, double Right)[] Columns1920 =
        [
            ("Repository", 0, 132), ("Folder", 132, 780), ("Branch", 780, 930), ("Ahead / behind", 930, 1060), ("Changes", 1060, 1280), ("State", 1280, 1460), ("Operation", 1460, 1920),
        ];

        /// <summary>A cell's text starts 8 into its column (2.6).</summary>
        public const double CellInset = 8;

        /// <summary>The dashboard's geometry (scenario facts 1): the header y 88 to 124, the headings 124 to 152, row k y 152 + 26 k.</summary>
        public const double HeaderTop = 88;
        public const double HeaderBottom = 124;
        public const double HeadingsBottom = 152;
        public const double RowHeight = 26;

        /// <summary>Row k's centre: y 165 + 26 k.</summary>
        public static double RowCentre(int k) => 165 + (26 * k);

        /// <summary>Where a check clicks a row (WD22): in its Repository cell, at x 66.</summary>
        public const double RowClickX = 66;

        /// <summary>Positions are asserted ±2 where stated (part W's "Dashboard rows").</summary>
        public const double Tolerance = 2;

        // ---- The row menus (2.7, scenario facts 2.3). ----

        public const string Open = "Open";
        public const string Fetch = "Fetch";
        public const string Pull = "Pull";
        public const string ErrorDetails = "Error details…";
        public const string ShowInFolder = "Show in folder";
        public const string CopyPath = "Copy path";
        public const string MoveUp = "Move up";
        public const string MoveDown = "Move down";
        public const string Locate = "Locate…";
        public const string RemoveFromWorkspace = "Remove from workspace";
        public const string FirstRepository = "It is the first repository";
        public const string LastRepository = "It is the last repository";

        /// <summary>A Ready row's menu with nothing running and nothing kept (web-app's in W2): 8 items, 3 separators.</summary>
        public static string[] ReadyRowMenu(string name, string? fetchReason = null, string? pullReason = null, string? moveUpReason = null, string? moveDownReason = null, bool errorDetails = false) =>
        [
            name,
            Open,
            "—",
            Item(Fetch, fetchReason),
            Item(Pull, pullReason),
            .. errorDetails ? new[] { ErrorDetails } : [],
            "—",
            ShowInFolder,
            CopyPath,
            "—",
            Item(MoveUp, moveUpReason),
            Item(MoveDown, moveDownReason),
            RemoveFromWorkspace,
        ];

        /// <summary>old-site's menu with its folder gone (W2 step 4): 9 items, 3 separators.</summary>
        public static string[] GoneFolderMenu(string oldSite) =>
        [
            "old-site",
            Item(Open, DoesNotExist(oldSite)),
            "—",
            Item(Fetch, DoesNotExist(oldSite)),
            Item(Pull, DoesNotExist(oldSite)),
            "—",
            Item(ShowInFolder, DoesNotExist(oldSite)),
            CopyPath,
            "—",
            MoveUp,
            Item(MoveDown, LastRepository),
            Locate,
            RemoveFromWorkspace,
        ];

        /// <summary>The header's More menu (W1 steps 9 and 11): 3 items, 1 separator.</summary>
        public static string[] MoreMenu(string workspace, string? addOpenTabsReason) =>
        [
            workspace,
            Item(AddOpenTabs, addOpenTabsReason),
            "—",
            RenameWorkspace,
            DeleteWorkspace + " (danger)",
        ];

        /// <summary>An item as <c>MenuSnapshot.Lines()</c> writes it: its label, or <c>label (disabled: "reason")</c>.</summary>
        public static string Item(string label, string? reason) => reason is null ? label : $"{label} (disabled: \"{reason}\")";

        /// <summary>notes' Pull reason, the toolbar's order (scenario facts 8 item 1).</summary>
        public const string NotesPullReason = "main has no upstream: set one first";
        public const string ScratchPullReason = "experiment has no upstream: set one first";
        public const string InfraPullReason = "HEAD is detached: check out a branch to pull";
        public const string BillingPullReason = "Finish or abort the merge first";

        // ---- The welcome page (13.1, 14.9). ----

        public const string WorkspacesHeading = "Workspaces";
        public const string NewWorkspace = "New workspace…";
        public const string NewWorkspaceName = "New workspace";
        public const string NewWorkspaceToolTip = "Create a workspace: repositories you check and update together";
        public const string NoWorkspaces = "No workspaces";

        /// <summary>The welcome page in reading order with no workspace (W1 step 1).</summary>
        public static readonly string[] WelcomeWithoutWorkspaces =
        [
            "No repository open", "Open, clone or init a repository to see its history.", "Open", "Clone", "Init",
            WorkspacesHeading, NewWorkspace, NoWorkspaces, "Recent repositories", "No recent repositories",
        ];

        /// <summary>The welcome page in reading order with "Web" and its nine repositories (W1 step 7).</summary>
        public static readonly string[] WelcomeWithWeb =
        [
            "No repository open", "Open, clone or init a repository to see its history.", "Open", "Clone", "Init",
            WorkspacesHeading, NewWorkspace, Workspace, "9 repositories", "Recent repositories", "No recent repositories",
        ];

        /// <summary>The Workspaces section's height with no workspace (13.1: 8 + 16 + 20 + 8 + 16), and 52 + 26 N with N.</summary>
        public static double SectionHeight(int workspaces) => workspaces == 0 ? 68 : 52 + (26 * workspaces);

        /// <summary>The welcome page's stack at 1100: 392 wide, x 284 to 676 (13.1).</summary>
        public const double StackLeft = 284;
        public const double StackRight = 676;

        /// <summary>The graph area at 1100×700, where the welcome page is centred: y 88 to 674.</summary>
        public const double GraphAreaTop = 88;
        public const double GraphAreaBottom = 674;

        /// <summary>The item's tooltip after W1's checklist: at most 8 names, then <c>, …</c> (13.1).</summary>
        public const string WaItemToolTip = "api, billing, design-system, docs, infra, mobile, notes, scratch, …";

        // ---- The name dialog and the deletion (1.6, 14.6). ----

        public const string NewWorkspaceTitle = "New workspace";
        public const string RenameWorkspaceTitle = "Rename workspace";
        public const string NamePlaceholder = "Workspace name";
        public const string Create = "Create";
        public const string Rename = "Rename";
        public static string RenameContext(string name) => $"Rename {name}";
        public const string DeleteTitle = "Delete workspace?";
        public static string DeleteQuestion(string name, int repositories) =>
            string.Create(CultureInfo.InvariantCulture, $"Delete the workspace {name}? Its {repositories} repositories stay where they are: nothing on disk changes.");
        public const string DeleteDetail = "Its dashboard tab closes.";
        public const string Delete = "Delete";

        // ---- Folder dialogs and the checklist (1.7, 1.9, 1.10, 14.6). ----

        public static string AddRepositoryFolderTitle(string workspace) => $"Add a repository to {workspace}";
        public static string LocateFolderTitle(string folderName) => $"Locate {folderName}";
        public static string AddRepositoriesTitle(string workspace) => $"Add repositories to {workspace}";
        public static string FoundIn(int found, string folder) => string.Create(CultureInfo.InvariantCulture, $"Found {found} repositories in {folder}.");
        public const string AlreadyInThisWorkspace = "Already in this workspace";
        public const string Add8Repositories = "Add 8 repositories";
        public const string Cancel = "Cancel";

        /// <summary>The checklist's card (1.9): 460 wide, x 320 to 780 at 1100.</summary>
        public const double ChecklistLeft = 320;
        public const double ChecklistWidth = 460;

        /// <summary>The checklist's rows: 28 high, 8 in view (<c>MaxHeight</c> 232).</summary>
        public const double ChecklistRowHeight = 28;
        public const double ChecklistListHeight = 232;

        // ---- The run (4.9, 14.3) and its log lines (14.14). ----

        public static string Fetching(int repositories, int done = 0) => done > 0
            ? string.Create(CultureInfo.InvariantCulture, $"Fetching {repositories} repositories… {done} done")
            : string.Create(CultureInfo.InvariantCulture, $"Fetching {repositories} repositories…");

        public static string Cancelling(int running) => running == 1
            ? "Cancelling: waiting for 1 repository…"
            : string.Create(CultureInfo.InvariantCulture, $"Cancelling: waiting for {running} repositories…");

        public const string Fetched8Skipped2 = "Fetched 8 repositories; 2 skipped";
        public const string FetchedNoneCancelled8 = "Fetched no repositories; 8 cancelled; 2 skipped";
        public const string Fetched7Cancelled1 = "Fetched 7 repositories; 1 cancelled; 2 skipped";
        public const string Fetched7Failed1 = "Fetched 7 repositories; 1 failed; 2 skipped";
        public const string Pulled3Failed2 = "Pulled 3 repositories; 2 failed; 5 skipped";

        public static string Waiting(int rows) => string.Create(CultureInfo.InvariantCulture, $"{rows} waiting");

        public const string FetchStarted = "Web: Fetch all: started for 8 repositories, 2 skipped";
        public const string PullStarted = "Web: Pull all: started for 5 repositories, 5 skipped";

        public static string FetchEnded(string result) => "Web: Fetch all: " + result;

        public static string PullEnded(string result) => "Web: Pull all: " + result;

        /// <summary>A read pass's line (14.14): <c>Web: read 1 repository in</c> / <c>Web: read {n} repositories in</c>, the milliseconds after it.</summary>
        public static string ReadPassStart(string workspace, int repositories) => repositories == 1
            ? $"{workspace}: read 1 repository in "
            : string.Create(CultureInfo.InvariantCulture, $"{workspace}: read {repositories} repositories in ");

        /// <summary>A row's summary read (Debug): <c>{name}: summary read in</c>.</summary>
        public static string SummaryRead(string name) => $"{name}: summary read in ";

        /// <summary>The menu layer's line on a dashboard (2.7): <c>Web: Context menu opened: {name} by pointer</c>.</summary>
        public static string MenuOpened(string name) => $"Web: Context menu opened: {name} by pointer";

        /// <summary>The line the provider logs per confirmed candidate (1.8).</summary>
        public const string OpenedTheRepositoryAt = "Opened the repository at ";

        // ---- The operations' texts (OperationTexts, scenario facts 7; 4.8). ----

        public const string FetchedAll = "Fetched from all remotes";
        public const string FetchingAll = "Fetching from all remotes…";
        public const string FetchCancelled = "Fetch cancelled";
        public const string FetchFailed = "Fetch failed";
        public const string WaitingToFetch = "Waiting to fetch…";
        public const string WaitingToPull = "Waiting to pull…";
        public const string PullFailed = "Pull failed";
        public const string StopGit = "Stop git";
        public const string MainUpToDate = "main is already up to date";
        public const string PulledDocs = "Pulled 4 commits into main (fast-forward)";
        public const string FetchFailedSummary = "Git could not fetch from origin.";
        public const string NotFastForwardSummary = "main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main.";
        public const string LocalChangesSummary = "1 conflict prevents the merge. Commit, stash, or discard your changes first.";
        public const string UndoPullTitle = "Undo pull into main";
        public const string UndidPull = "Undid pull into main";
        public const string UndoPullToolTip = "Undo pull into main (Ctrl+Z)";
        public const string RedoPullToolTip = "Redo pull into main (Ctrl+Y)";
        public const string NothingToUndo = "Nothing to undo";
        public const string NothingToRedo = "Nothing to redo";

        /// <summary>A running fetch's compact text at a percentage (2.6) and its tooltip, the status bar's whole text.</summary>
        public static string FetchingAt(int percent) => string.Create(CultureInfo.InvariantCulture, $"Fetching from all remotes… {percent}%");

        public static string FetchingAtToolTip(int percent) => string.Create(CultureInfo.InvariantCulture, $"Fetching from all remotes… Counting objects {percent}%");

        /// <summary>W4's three holds: docs at 25, mobile at 75, design-system at 45 (scenario facts 2.5).</summary>
        public static IReadOnlyList<(string Name, int Percent)> W4Holds { get; } = [("docs", 25), ("mobile", 75), ("design-system", 45)];

        /// <summary>The bar's value: ⌊100 × done / n⌋ (WD13).</summary>
        public static int Percent(int done, int repositories) => 100 * done / repositories;

        // ---- The toolbar and the status bar (TD, TR; 2.8, 4.9, 4.10). ----

        public const string SettingsToolTip = "Open settings (Ctrl+,)";
        public const string ThemeToLight = "Switch to the light theme";
        public const string ThemeToDark = "Switch to the dark theme";
        public const string Ready = "Ready";
        public const string SelectRepository = "Select a repository to see its activity";
        public const string ShowActivityLog = "Show every git command the app has run (Ctrl+J)";
        public const string HideActivityLog = "Hide the activity log (Ctrl+J)";
        public const string ItemStatusWorkspace = "Workspace";
        public const string DashboardTabId = "DashboardTab";
        public const string NewTab = "New tab";

        /// <summary>The toolbar buttons a dashboard tab leaves disabled without a tooltip (4.10).</summary>
        public static readonly string[] DisabledWithoutToolTip = ["PushButton", "BranchButton", "StashButton", "PopButton", "UndoButton", "RedoButton", "SearchButton"];

        // ---- The sign-in (W6; phase 3's dialog with the port and 3.8's line). ----

        public const string User = "alice";
        public const string Password = "s3cret";

        public static string SignInTitle(int port) => WithPort("Sign in to 127.0.0.1:47393", port);

        public static string SignInText(int port) => WithPort("Git needs a user name and a password or token for http://127.0.0.1:47393.", port);

        /// <summary>The two prompts git asks of docs and of mobile alike (scenario facts 2.5).</summary>
        public static string UsernamePrompt(int port) => WithPort("Username for 'http://127.0.0.1:47393': ", port);

        public static string PasswordPrompt(int port) => WithPort("Password for 'http://alice@127.0.0.1:47393': ", port);

        /// <summary>The requests each signed-in fetch makes, in order (scenario facts 2.5): method, path's end, query, v2 command, user, status.</summary>
        public static IReadOnlyList<(string Method, string PathEnd, string Query, string? Command, string? User, int Status)> SignedInRequests { get; } =
        [
            ("GET", "/info/refs", "service=git-upload-pack", null, null, 401),
            ("GET", "/info/refs", "service=git-upload-pack", null, User, 200),
            ("POST", "/git-upload-pack", string.Empty, "ls-refs", User, 200),
            ("POST", "/git-upload-pack", string.Empty, "fetch", User, 200),
        ];

        // ---- The commands the app runs (as GitCallRecord.CommandText renders them). ----

        public const string FetchCommand = "git fetch --progress origin";
        public const string MergeCommand = "git merge --no-edit --no-overwrite-ignore FETCH_HEAD";
        public const string FastForwardOnlyCommand = "git merge --ff-only --no-overwrite-ignore FETCH_HEAD";
        public const string PullAddedRead = "git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD FETCH_HEAD --";
        public const string PullFastForwardRead = "git config --get pull.ff";
        public const string UpdateIndexRefresh = "git update-index -q --refresh";
        public const string UndoReadTree = "git read-tree -m -u " + DocsChangelog + " " + DocsInitial;
        public const string UndoUpdateRef = "git update-ref -m \"VisualCommit: undo pull into main\" refs/heads/main " + DocsInitial + " " + DocsChangelog;
        public const string UndoReflogMessage = "VisualCommit: undo pull into main";
        public const string GitVersionCommand = "git --version";
        public const string OpenCommand = "git rev-parse --is-inside-work-tree --show-toplevel --absolute-git-dir --git-common-dir";
        public const string StatusCommand = "git status --porcelain=v2 -z --untracked-files=all --renames";

        /// <summary>
        /// The reads a row or a tab makes (part W's "Only reads", the call log): each of these
        /// starts a record's command text (an argument list after it where it has one). A call of
        /// "only reads" has no operation id and is one of them, or a repository tab's history load.
        /// </summary>
        public static readonly string[] ReadForms =
        [
            GitVersionCommand,
            OpenCommand,
            "git rev-parse --show-prefix",
            "git rev-parse --git-common-dir",
            "git for-each-ref --format=",
            "git remote",
            "git config --null --get-regexp ^remote\\.",
            "git stash list ",
            "git symbolic-ref -q HEAD",
            "git rev-parse -q --verify HEAD",
            StatusCommand,
        ];

        /// <summary>The refs read's calls (for-each-ref, the remotes, the stashes, HEAD's ref and its commit) and the status read, by their starts.</summary>
        public static readonly string[] SummaryReadForms =
        [
            "git for-each-ref --format=",
            "git config --null --get-regexp ^remote\\.",
            "git stash list ",
            "git symbolic-ref -q HEAD",
            "git rev-parse -q --verify HEAD",
            StatusCommand,
        ];

        // ---- Git's texts (Git 2.36.0.windows.1's; scenario facts 2.5, 2.6). ----

        /// <summary>scratch's missing remote (W6): its URL as the check sets it.</summary>
        public const string MissingRemote = "../remotes/missing.git";

        /// <summary>The missing remote's key line (W6 step 4), printed on every git.</summary>
        public const string MissingRemoteKey = "fatal: '../remotes/missing.git' does not appear to be a git repository";

        /// <summary>The missing remote's standard error, cleaned (2.36; 2.55 identical), without the app's line.</summary>
        public static readonly string[] MissingRemoteLines =
        [
            MissingRemoteKey,
            "fatal: Could not read from remote repository.",
            "Please make sure you have the correct access rights",
            "and the repository exists.",
        ];

        /// <summary>The line <c>FetchAsync(null)</c> appends to a failed fetch's output (D109): the app's, not git's.</summary>
        public const string CouldNotFetchOrigin = "error: Could not fetch origin";

        /// <summary>mobile's refused fast-forward under <c>pull.ff = only</c>: its key, and its cleaned standard error on 2.36.</summary>
        public const string NotFastForwardKey = "fatal: Not possible to fast-forward, aborting.";

        public static readonly string[] NotFastForwardLines = [NotFastForwardKey];

        /// <summary>design-system's refusal (W7 step 4): its key, and the four lines of its standard error (2.36; 2.55 identical).</summary>
        public const string LocalChangesKey = "error: Your local changes to the following files would be overwritten by merge:";

        public static readonly string[] LocalChangesLines =
        [
            LocalChangesKey,
            "\tspacing.txt",
            "Please commit your changes or stash them before you merge.",
            "Aborting",
        ];

        /// <summary>design-system's merge writes this on standard output, never shown (scenario facts 8 item 9, as corrected).</summary>
        public const string DesignSystemUpdating = "Updating 98ba313..948f7d1";

        /// <summary>api's and web-app's merge (stdout).</summary>
        public const string AlreadyUpToDate = "Already up to date.";

        /// <summary>docs' fast-forward (stdout, 2.36), line by line.</summary>
        public static readonly string[] DocsFastForwardLines =
        [
            "Updating ec855ba..245b729",
            "Fast-forward",
            " changelog.md | 1 +",
            " faq.md       | 1 +",
            " guide.md     | 2 +-",
            " install.md   | 1 +",
            " 4 files changed, 4 insertions(+), 1 deletion(-)",
            " create mode 100644 changelog.md",
            " create mode 100644 faq.md",
            " create mode 100644 install.md",
        ];

        /// <summary>9.10's read before each merge (W7): the paths the pull adds, by repository.</summary>
        public static IReadOnlyDictionary<string, string[]> AddedByPull { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["api"] = [],
            ["web-app"] = [],
            ["docs"] = ["changelog.md", "faq.md", "install.md"],
            ["mobile"] = ["about.txt", "login.txt", "settings.txt"],
            ["design-system"] = [],
        };

        /// <summary>The fetches' standard error (2.36) with the lines holding <c>%</c> left out (scenario facts 2.5): the three with new commits, over the file remote.</summary>
        public static IReadOnlyDictionary<string, string[]> FetchLines { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["docs"] = ["remote: Enumerating objects: 4, done.", "remote: Total 3 (delta 1), reused 0 (delta 0), pack-reused 0", "From ../remotes/docs", "   23e2ad1..245b729  main       -> origin/main"],
            ["mobile"] = ["remote: Enumerating objects: 4, done.", "remote: Total 3 (delta 1), reused 0 (delta 0), pack-reused 0", "From ../remotes/mobile", "   f419c6e..67a2660  main       -> origin/main"],
            ["design-system"] = ["remote: Enumerating objects: 11, done.", "remote: Total 9 (delta 3), reused 0 (delta 0), pack-reused 0", "From ../remotes/design-system", "   98ba313..948f7d1  main       -> origin/main"],
        };

        /// <summary>The fetched line each of the three prints on every git (the key of its standard error).</summary>
        public static IReadOnlyDictionary<string, string> FetchedRefLine { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["docs"] = "   23e2ad1..245b729  main       -> origin/main",
            ["mobile"] = "   f419c6e..67a2660  main       -> origin/main",
            ["design-system"] = "   98ba313..948f7d1  main       -> origin/main",
        };

        // ---- The pull's undo in docs (W7), its state after it. ----

        /// <summary>docs' files after Pull all (WP), with their contents.</summary>
        public static readonly (string Path, string Text)[] DocsPulledFiles =
        [
            ("README.md", "# docs\n"), ("changelog.md", "Changes.\n"), ("faq.md", "Questions.\n"), ("guide.md", "Guide.\n"), ("install.md", "Install it.\n"),
        ];

        /// <summary>docs' files after the undo: README.md and guide.md <c>Guide</c> only.</summary>
        public static readonly (string Path, string Text)[] DocsUndoneFiles = [("README.md", "# docs\n"), ("guide.md", "Guide\n")];

        // ---- W8's commit and file (WD17, WD18). ----

        public const string ContactFile = "contact.html";
        public const string ContactText = "<h1>Contact</h1>\n";
        public const string ContactSummary = "Add the contact page";
        public const string ContactCommit = "092116c01a4d62eefc50c08ac9f1e9109fbe0059";
        public const string ContactTree = "fcfe571";
        public const string TodoFile = "todo.txt";
        public const string TodoText = "Try it\n";
        public const string OneUntracked = "1 untracked";

        /// <summary>mobile's Fetch as the activity log lists it in W8 step 10: its time, title, message and duration.</summary>
        public static readonly (string Time, string Title, string Message, string Duration) MobileFetchLogRow = ("09:00:00", "Fetch", FetchedAll, "0 ms");

        // ---- The graphs in the repository tabs part W opens ("Scenarios and graphs of part W"). ----

        /// <summary>A0, api as built: (message, lane, colour, labels in drawing order, short SHA).</summary>
        public static readonly (string Message, int Lane, int Colour, string[] Labels, string Sha)[] A0 =
        [
            ("Add the health route", 0, 0, ["main ✓", "origin/main"], "2248c9d"),
            ("Initial commit", 0, 0, [], "e1d2947"),
        ];

        /// <summary>WA0, web-app as built.</summary>
        public static readonly (string Message, int Lane, int Colour, string[] Labels, string Sha)[] WebApp0 =
        [
            ("Style the pages", 0, 0, ["main ✓"], "cbd9037"),
            ("Add the about page", 0, 0, [], "0d6e6c5"),
            ("Initial commit", 0, 0, ["origin/main"], "147c6e3"),
        ];

        /// <summary>WC, web-app after W8's commit.</summary>
        public static readonly (string Message, int Lane, int Colour, string[] Labels, string Sha)[] WebAppCommitted =
        [
            (ContactSummary, 0, 0, ["main ✓"], "092116c"),
            ("Style the pages", 0, 0, [], "cbd9037"),
            ("Add the about page", 0, 0, [], "0d6e6c5"),
            ("Initial commit", 0, 0, ["origin/main"], "147c6e3"),
        ];

        /// <summary>D0, docs as built.</summary>
        public static readonly (string Message, int Lane, int Colour, string[] Labels, string Sha)[] Docs0 =
        [
            ("Fix the guide's typos", 0, 0, ["origin/main"], "23e2ad1"),
            ("Write the FAQ", 0, 0, [], "43723b6"),
            ("Write the install guide", 0, 0, [], "245235b"),
            ("Initial commit", 0, 0, ["main ✓"], "ec855ba"),
        ];

        /// <summary>DP, docs after Pull all.</summary>
        public static readonly (string Message, int Lane, int Colour, string[] Labels, string Sha)[] DocsPulled =
        [
            ("Add the changelog", 0, 0, ["main ✓", "origin/main"], "245b729"),
            ("Fix the guide's typos", 0, 0, [], "23e2ad1"),
            ("Write the FAQ", 0, 0, [], "43723b6"),
            ("Write the install guide", 0, 0, [], "245235b"),
            ("Initial commit", 0, 0, [], "ec855ba"),
        ];

        /// <summary>DU, docs after the pull's undo.</summary>
        public static readonly (string Message, int Lane, int Colour, string[] Labels, string Sha)[] DocsUndone =
        [
            ("Add the changelog", 0, 0, ["origin/main"], "245b729"),
            ("Fix the guide's typos", 0, 0, [], "23e2ad1"),
            ("Write the FAQ", 0, 0, [], "43723b6"),
            ("Write the install guide", 0, 0, [], "245235b"),
            ("Initial commit", 0, 0, ["main ✓"], "ec855ba"),
        ];

        /// <summary>M0, mobile as built.</summary>
        public static readonly (string Message, int Lane, int Colour, string[] Labels, string Sha)[] Mobile0 =
        [
            ("Name the app", 0, 0, ["main ✓"], "21b8aeb"),
            ("Add the settings screen", 1, 1, ["origin/main"], "f419c6e"),
            ("Add the login screen", 1, 1, [], "a17bccb"),
            ("Initial commit", 0, 0, [], "617d476"),
        ];

        /// <summary>MF, mobile after W8's fetch.</summary>
        public static readonly (string Message, int Lane, int Colour, string[] Labels, string Sha)[] MobileFetched =
        [
            ("Add the about screen", 0, 0, ["origin/main"], "67a2660"),
            ("Name the app", 1, 1, ["main ✓"], "21b8aeb"),
            ("Add the settings screen", 0, 0, [], "f419c6e"),
            ("Add the login screen", 0, 0, [], "a17bccb"),
            ("Initial commit", 0, 0, [], "617d476"),
        ];

        // ---- The pictures (the report's Screenshots column of W1 to W9), in the order the checks save them. ----

        public const string W1aWelcomeWorkspaces = "w1a-welcome-workspaces";
        public const string W1bNewWorkspaceDialog = "w1b-new-workspace-dialog";
        public const string W1cEmptyDashboard = "w1c-empty-dashboard";
        public const string W1dApiAdded = "w1d-api-added";
        public const string W1eScanChecklist = "w1e-scan-checklist";
        public const string W1fNineRows = "w1f-nine-rows";
        public const string W1gWelcomeListsWeb = "w1g-welcome-lists-web";
        public const string W1hMoreMenu = "w1h-more-menu";
        public const string W1iOpenTabsAdded = "w1i-open-tabs-added";
        public const string W1jRenameDialog = "w1j-rename-dialog";
        public const string W1kDeleteQuestion = "w1k-delete-question";
        public const string W1lDashboardDeleted = "w1l-dashboard-deleted";
        public const string W2aTenRows = "w2a-ten-rows";
        public const string W2bAheadBehindToolTip = "w2b-ahead-behind-tooltip";
        public const string W2cRowMenu = "w2c-row-menu";
        public const string W2dGoneFolderMenu = "w2d-gone-folder-menu";
        public const string W2eLocated = "w2e-located";
        public const string W2fRemoved = "w2f-removed";
        public const string W2gMovedUp = "w2g-moved-up";
        public const string W3aTenRowsWideLight = "w3a-ten-rows-wide-light";
        public const string W4aHeldAt257545 = "w4a-held-at-25-75-45";
        public const string W4bFetched = "w4b-fetched";
        public const string W5aFourRunning = "w5a-four-running";
        public const string W5bAllCancelled = "w5b-all-cancelled";
        public const string W5cRowCancelled = "w5c-row-cancelled";
        public const string W5dRowCancelRunEnded = "w5d-row-cancel-run-ended";
        public const string W6aOneSignIn = "w6a-one-sign-in";
        public const string W6bFailureInRow = "w6b-failure-in-row";
        public const string W6cDetails = "w6c-details";
        public const string W7aPullAllEnded = "w7a-pull-all-ended";
        public const string W7bNotFastForwardDetails = "w7b-not-fast-forward-details";
        public const string W7cLocalChangesDetails = "w7c-local-changes-details";
        public const string W7dUndoPullToolTip = "w7d-undo-pull-tooltip";
        public const string W7ePullUndone = "w7e-pull-undone";
        public const string W8aSelectARepository = "w8a-select-a-repository";
        public const string W8bWebAppOpened = "w8b-web-app-opened";
        public const string W8cDocsOpened = "w8c-docs-opened";
        public const string W8dRowFetching = "w8d-row-fetching";
        public const string W8eFetchInTheTab = "w8e-fetch-in-the-tab";
        public const string W8fRowFollowsItsTab = "w8f-row-follows-its-tab";
        public const string W8gReadAfterTenSeconds = "w8g-read-after-ten-seconds";
        public const string W8hLogFollowsTheRow = "w8h-log-follows-the-row";
        public const string W9aApiOpened = "w9a-api-opened";
        public const string W9bRestarted = "w9b-restarted";
        public const string W9cDashboardRead = "w9c-dashboard-read";

        /// <summary>Every picture of part W, 45 (the report's count in "What the parts hold").</summary>
        public static readonly string[] Pictures =
        [
            W1aWelcomeWorkspaces, W1bNewWorkspaceDialog, W1cEmptyDashboard, W1dApiAdded, W1eScanChecklist, W1fNineRows, W1gWelcomeListsWeb,
            W1hMoreMenu, W1iOpenTabsAdded, W1jRenameDialog, W1kDeleteQuestion, W1lDashboardDeleted,
            W2aTenRows, W2bAheadBehindToolTip, W2cRowMenu, W2dGoneFolderMenu, W2eLocated, W2fRemoved, W2gMovedUp,
            W3aTenRowsWideLight,
            W4aHeldAt257545, W4bFetched,
            W5aFourRunning, W5bAllCancelled, W5cRowCancelled, W5dRowCancelRunEnded,
            W6aOneSignIn, W6bFailureInRow, W6cDetails,
            W7aPullAllEnded, W7bNotFastForwardDetails, W7cLocalChangesDetails, W7dUndoPullToolTip, W7ePullUndone,
            W8aSelectARepository, W8bWebAppOpened, W8cDocsOpened, W8dRowFetching, W8eFetchInTheTab, W8fRowFollowsItsTab, W8gReadAfterTenSeconds, W8hLogFollowsTheRow,
            W9aApiOpened, W9bRestarted, W9cDashboardRead,
        ];

        /// <summary>A copy's folder of <paramref name="name"/> as the workspace holds it (<c>{root}\&lt;name&gt;</c>).</summary>
        public static string PathOf(TempWorkspace copy, string name)
        {
            ArgumentNullException.ThrowIfNull(copy);
            return copy.PathOf(name);
        }
    }
}
