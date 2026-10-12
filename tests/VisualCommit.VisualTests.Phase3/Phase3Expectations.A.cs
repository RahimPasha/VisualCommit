// Part A's expected results (docs/test-reports/phase-3.md, part A), copied from the report. Only
// part A's writer edits this file; its members live in Phase3Expectations.PartA. Compiled by the
// real-window pass too: see Phase3Expectations.Common.cs for what may be used here.
namespace VisualCommit.VisualTests.Phase3;

public static partial class Phase3Expectations
{
    /// <summary>What part A's checks (1 to 7, 5b, 23, 24, 26) compare against.</summary>
    public static class PartA
    {
        // ---- The ops scenario (part A, "Scenario repos"): rows, SHAs, lanes. ----

        /// <summary>The ops scenario's rows, as SelectedIndex counts them.</summary>
        public const int StashRow = 0;

        public const int ConflictingChangeRow = 1;

        public const int FastForwardMeRow = 2;

        public const int MainWorkRow = 3;

        /// <summary>The ops scenario's row "Alpha two" (G0's row 4).</summary>
        public const int AlphaTwoRow = 4;

        public const int AlphaOneRow = 5;

        public const int AddCoreRow = 6;

        public const int InitialCommitRow = 7;

        /// <summary>The ops graph's lanes: three, so the Graph column is 64 and the Message column starts at 454.</summary>
        public const int OpsLanes = 3;

        /// <summary>Alpha two's full SHA ("Branches").</summary>
        public const string AlphaTwoSha = "ce5ea6ab16759bede02ce706dd5c6865443c2051";

        /// <summary>Main work's full SHA ("Branches").</summary>
        public const string MainWorkSha = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

        /// <summary>The four local branches of the ops scenario, as <c>git for-each-ref --format=%(refname:short) refs/heads</c> prints them.</summary>
        public static readonly string[] OpsBranches = ["feature/alpha", "feature/conflict", "feature/ff", "main"];

        /// <summary>
        /// The ops graph in the checks' notation (<c>&lt;sha7&gt; &lt;subject&gt; [&lt;labels&gt;]
        /// &lt;lane&gt;/&lt;colour&gt;</c>, " ends" for lanes ending in the node, " out" for a line
        /// to another lane): the table and "Lines" of "Scenario repos".
        /// </summary>
        public static readonly string[] OpsGraph =
        [
            "0e95aca On main: Experiment [Stash:stash@{0}] 0/0",
            "078ef05 Conflicting change [LocalBranch:feature/conflict] 1/1",
            "d93814d Fast-forward me [LocalBranch:feature/ff] 2/2",
            "88d6b91 Main work [CurrentBranch:main, RemoteBranch:origin/main, Tag:v1.1] 0/0 ends 2",
            "ce5ea6a Alpha two [LocalBranch:feature/alpha, RemoteBranch:origin/feature/alpha] 2/3",
            "dff8fbf Alpha one [] 2/3",
            "8beca34 Add core [Tag:v1.0] 0/0 ends 1,2",
            "4ef1f7c Initial commit [] 0/0",
        ];

        /// <summary>The left panel when the ops repository opens ("Left panel"), in the checks' notation.</summary>
        public static readonly string[] OpsLeftPanel =
        [
            "▾ Local branches 4",
            "  ▸ feature",
            "  main (head)",
            "▾ Remotes 2",
            "  ▾ origin",
            "    ▸ feature",
            "    main",
            "▸ Pull requests 0",
            "▾ Tags 2",
            "  v1.0",
            "  v1.1",
            "▾ Stashes 1",
            "  On main: Experiment",
        ];

        // ---- Check 1: the commit menu. ----

        /// <summary>Check 1 step 1: the commit menu of "Alpha two", as the report writes it (232 high).</summary>
        public static readonly string[] AlphaTwoCommitMenu =
        [
            "ce5ea6a Alpha two",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto main",
            "Revert this commit",
            "Reset main to this commit…",
            "—",
            "Copy SHA",
        ];

        /// <summary>Check 1 step 1: the commit menu is 232 high (7 items, 2 separators).</summary>
        public const double CommitMenuHeight = 232;

        /// <summary>Check 1 step 3 and check 5 step 2: the branch dialog from "Alpha two"'s menu.</summary>
        public const string AlphaTwoBranchContext = "From ce5ea6a \"Alpha two\"";

        /// <summary>Check 1 step 4: "Add core"'s commit menu, "Cherry-pick onto main" disabled.</summary>
        public static readonly string[] AddCoreCommitMenu =
        [
            "8beca34 Add core",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto main (disabled: \"This commit is already on main\")",
            "Revert this commit",
            "Reset main to this commit…",
            "—",
            "Copy SHA",
        ];

        /// <summary>Check 1 step 4 (and the real window's 01c): the tooltip of the disabled pick.</summary>
        public const string AlreadyOnMain = "This commit is already on main";

        /// <summary>The edges scenario's graph ("Edges scenario"): HEAD detached at "Detached work".</summary>
        public static readonly string[] EdgesGraph =
        [
            "419190f Detached work [DetachedHead:HEAD] 0/0",
            "a024e50 Alpha work [LocalBranch:feature/alpha] 1/1",
            "8e16408 Main work [LocalBranch:main, RemoteBranch:origin/main, Tag:feature/alpha] 0/0 ends 1",
            "691f8cf Initial commit [LocalBranch:loc, LocalBranch:origin/main, LocalBranch:side] 0/0",
        ];

        /// <summary>Check 1 step 6: the commit menu of a detached HEAD's commit.</summary>
        public static readonly string[] DetachedCommitMenu =
        [
            "419190f Detached work",
            "Check out this commit (disabled: \"HEAD is already at this commit\")",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto HEAD (disabled: \"This commit is already on HEAD\")",
            "Revert this commit",
            "Reset HEAD to this commit…",
            "—",
            "Copy SHA",
        ];

        /// <summary>The graph scenario's merge commit row (phase 1's report: "Merge branch 'feature/login'" is row 7).</summary>
        public const int GraphMergeRow = 7;

        /// <summary>Check 1 step 7: the commit menu of a merge commit.</summary>
        public static readonly string[] MergeCommitMenu =
        [
            "f7919b8 Merge branch 'feature/login'",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto main (disabled: \"Merge commits cannot be cherry-picked here\")",
            "Revert this merge commit",
            "Reset main to this commit…",
            "—",
            "Copy SHA",
        ];

        // ---- Check 2: branch menus. ----

        /// <summary>Check 2 step 1: <c>feature/alpha</c>'s menu (406 high).</summary>
        public static readonly string[] FeatureAlphaMenu =
        [
            "feature/alpha",
            "Check out feature/alpha",
            "Merge feature/alpha into main",
            "Merge feature/alpha into main (create a merge commit)",
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
            "Copy branch name",
        ];

        /// <summary>Check 2 step 1: the menu is 406 high.</summary>
        public const double FeatureAlphaMenuHeight = 406;

        /// <summary>Check 2 step 2: <c>feature/ff</c>'s menu (no upstream).</summary>
        public static readonly string[] FeatureFfMenu =
        [
            "feature/ff",
            "Check out feature/ff",
            "Merge feature/ff into main",
            "Merge feature/ff into main (create a merge commit)",
            "—",
            "Push feature/ff…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename feature/ff…",
            "Delete feature/ff… (danger)",
            "—",
            "Set upstream…",
            "—",
            "Copy branch name",
        ];

        /// <summary>Check 2 step 3 (and check 5 step 5): the current branch's menu.</summary>
        public static readonly string[] MainMenu =
        [
            "main",
            "Pull from origin/main",
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
            "Copy branch name",
        ];

        /// <summary>Check 2 step 3: the tooltip of the disabled delete.</summary>
        public const string CannotDeleteCurrent = "You cannot delete the branch you are on";

        /// <summary>Check 2 step 4: <c>origin/feature/alpha</c>'s menu (and check 6 step 5's, with no local branch).</summary>
        public static readonly string[] OriginFeatureAlphaMenu =
        [
            "origin/feature/alpha",
            "Check out origin/feature/alpha",
            "Merge origin/feature/alpha into main",
            "Merge origin/feature/alpha into main (create a merge commit)",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Delete feature/alpha on origin… (danger)",
            "—",
            "Copy branch name",
        ];

        /// <summary>Check 6 step 5: the remote-branch menu is 241 high.</summary>
        public const double OriginFeatureAlphaMenuHeight = 241;

        /// <summary>Check 2 step 5: <c>origin/main</c>'s menu while <c>main</c> is checked out.</summary>
        public static readonly string[] OriginMainCheckedOutMenu =
        [
            "origin/main",
            "Check out origin/main (disabled: \"main is already checked out\")",
            "Merge origin/main into main",
            "Merge origin/main into main (create a merge commit)",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Delete main on origin… (danger)",
            "—",
            "Copy branch name",
        ];

        /// <summary>Check 2 step 5: the tooltip of the disabled checkout.</summary>
        public const string MainAlreadyCheckedOut = "main is already checked out";

        /// <summary>The edges scenario with <c>loc</c> checked out ("Edges with loc checked out").</summary>
        public static readonly string[] EdgesLocGraph =
        [
            "a024e50 Alpha work [LocalBranch:feature/alpha] 0/0",
            "8e16408 Main work [LocalBranch:main, RemoteBranch:origin/main, Tag:feature/alpha] 0/0",
            "691f8cf Initial commit [CurrentBranch:loc, LocalBranch:origin/main, LocalBranch:side] 0/0",
        ];

        /// <summary>Its Local branches section: the folders first (closed), then loc (head, ↓1), main, side.</summary>
        public static readonly string[] EdgesLocLocalBranches =
        [
            "▾ Local branches 5",
            "  ▸ feature",
            "  ▸ origin",
            "  loc (head) ↓1",
            "  main",
            "  side",
        ];

        /// <summary>Check 2 step 6: <c>loc</c>'s menu (a local upstream: no push items, D101).</summary>
        public static readonly string[] LocMenu =
        [
            "loc",
            "Pull from main",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename loc…",
            "Delete loc… (disabled: \"You cannot delete the branch you are on\")",
            "—",
            "Change upstream…",
            "Stop tracking main",
            "—",
            "Copy branch name",
        ];

        /// <summary>Check 2 step 7: <c>side</c>'s menu.</summary>
        public static readonly string[] SideMenu =
        [
            "side",
            "Check out side",
            "Merge side into loc",
            "Merge side into loc (create a merge commit)",
            "—",
            "Push side…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename side…",
            "Delete side… (danger)",
            "—",
            "Set upstream…",
            "—",
            "Copy branch name",
        ];

        /// <summary>Check 2 step 8: the local branch named <c>origin/main</c>.</summary>
        public static readonly string[] LocalOriginMainMenu =
        [
            "origin/main",
            "Check out origin/main",
            "Merge origin/main into loc",
            "Merge origin/main into loc (create a merge commit)",
            "—",
            "Push origin/main…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename origin/main…",
            "Delete origin/main… (danger)",
            "—",
            "Set upstream…",
            "—",
            "Copy branch name",
        ];

        /// <summary>Check 2 step 9: the remote branch <c>origin/main</c> while HEAD is on <c>loc</c>.</summary>
        public static readonly string[] RemoteOriginMainMenu =
        [
            "origin/main",
            "Check out origin/main",
            "Merge origin/main into loc",
            "Merge origin/main into loc (create a merge commit)",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Delete main on origin… (danger)",
            "—",
            "Copy branch name",
        ];

        // ---- Check 3: tag, stash, remote, section headers, the empty area. ----

        public static readonly string[] TagMenu =
        [
            "v1.0",
            "Check out v1.0",
            "Create branch here…",
            "—",
            "Push v1.0 to origin",
            "Delete v1.0… (danger)",
            "Delete v1.0 on origin… (danger)",
            "—",
            "Copy tag name",
        ];

        public static readonly string[] StashMenu =
        [
            "stash@{0} On main: Experiment",
            "Apply stash",
            "Pop stash",
            "Drop stash… (danger)",
            "—",
            "Copy SHA",
        ];

        public static readonly string[] RemoteMenu =
        [
            "origin",
            "Fetch origin",
            "—",
            "Change URL…",
            "Rename origin…",
            "Remove origin… (danger)",
            "—",
            "Copy URL",
            "Manage remotes…",
        ];

        public static readonly string[] LocalBranchesHeaderMenu = ["Local branches", "Create branch…"];

        public static readonly string[] RemotesHeaderMenu = ["Remotes", "Add remote…", "Fetch all remotes", "Manage remotes…"];

        public static readonly string[] TagsHeaderMenu = ["Tags", "Create tag at HEAD…"];

        public static readonly string[] StashesHeaderMenu = ["Stashes", "Stash changes… (disabled: \"There are no changes to stash\")"];

        /// <summary>Stash's reason on a clean working tree (4d), in the menus and the toolbar.</summary>
        public const string NoChangesToStash = "There are no changes to stash";

        /// <summary>Check 3 step 6: where the empty graph area is right-clicked, 100 below "Initial commit".</summary>
        public static readonly (double X, double Y) EmptyAreaPoint = (520, 424);

        /// <summary>Check 3 step 6 and check 5b step 1: the empty area's menu (206 high).</summary>
        public static readonly string[] OpsEmptyAreaMenu =
        [
            "ops",
            "Fetch all remotes",
            "Pull",
            "Push",
            "—",
            "Create branch…",
            "Stash changes… (disabled: \"There are no changes to stash\")",
            "—",
            "Manage remotes…",
        ];

        public const double EmptyAreaMenuHeight = 206;

        /// <summary>Check 3 step 8: the tag dialog's context.</summary>
        public const string MainWorkTagContext = "At 88d6b91 \"Main work\"";

        // ---- Check 4: file menus (the changes scenario). ----

        /// <summary>
        /// Check 4 step 6: "the diff of phase 2's check 3 step 2", src/Calculator.cs unstaged, inline
        /// (phase 2's report, as phase 2's checks write the rows).
        /// </summary>
        public static readonly string[] CalculatorInline =
        [
            "@@ -12,7 +12,7 @@ public sealed class Calculator",
            "C 12/12",
            "C 13/13 public int Add(int a, int b)",
            "C 14/14 {",
            "R 15/- return a + b;",
            "A -/15 return a + b + _offset;",
            "C 16/16 }",
            "C 17/17",
            "C 18/18 public int Subtract(int a, int b)",
            "@@ -29,4 +29,8 @@ public sealed class Calculator",
            "C 29/29 {",
            "C 30/30 return a / b;",
            "C 31/31 }",
            "A -/32",
            "A -/33 public int Negate(int a) => -a;",
            "A -/34",
            "A -/35 public int Square(int a) => a * a;",
            "C 32/36 }",
        ];

        public const string SnapshotLine = "A snapshot is saved first, so the changes can be restored.";

        public static readonly string[] CalculatorUnstagedMenu =
        [
            "Calculator.cs",
            "Stage",
            "Discard changes…",
            "—",
            "Ignore Calculator.cs (disabled: \"Only untracked files can be ignored\")",
            "Ignore all *.cs files (disabled: \"Only untracked files can be ignored\")",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        /// <summary>The reason a tracked file's Ignore items are disabled.</summary>
        public const string OnlyUntracked = "Only untracked files can be ignored";

        public static readonly string[] GuideUntrackedMenu =
        [
            "guide.md",
            "Stage",
            "Discard changes…",
            "—",
            "Ignore guide.md",
            "Ignore all *.md files",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        public static readonly string[] SettingsStagedMenu = ["settings.json", "Unstage", "—", "Open file", "Show in folder", "Copy path"];

        /// <summary>Check 4 step 3, after "Unstage": the staged list (phase 2's notation).</summary>
        public static readonly string[] StagedAfterUnstage = ["M README.md", "R helpers.py src renamed from src/util.py"];

        /// <summary>Check 4 step 3, after "Unstage": the unstaged list.</summary>
        public static readonly string[] UnstagedAfterUnstage =
        [
            "M README.md",
            "M logo.png assets",
            "A settings.json config",
            "M blob.bin data",
            "M large.txt data",
            "A guide.md docs",
            "D old-notes.txt docs",
            "M Calculator.cs src",
        ];

        /// <summary>Check 4 step 5: README.md's staged diff: its one hunk header and the lines it adds (4 to 7); the others are context.</summary>
        public static readonly string[] ReadmeStagedDiffHeaderAndAdded =
        [
            "@@ -1,3 +1,7 @@",
            "A -/4",
            "A -/5 ## Usage",
            "A -/6",
            "A -/7 Create a Calculator and call Add.",
        ];

        /// <summary>Check 4 step 5: the line right-clicked, new line 5.</summary>
        public const int UsageLine = 5;

        public static readonly string[] ReadmeStagedDiffMenu =
        [
            "README.md",
            "Unstage hunk",
            "—",
            "Unstage file",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        public const double ReadmeStagedDiffMenuHeight = 180;

        public static readonly string[] CalculatorDiffMenu =
        [
            "Calculator.cs",
            "Stage hunk",
            "Discard hunk…",
            "—",
            "Stage file",
            "Discard file…",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        public static readonly string[] CalculatorDiffLinesMenu =
        [
            "Calculator.cs",
            "Stage selected lines",
            "Discard selected lines…",
            "—",
            "Stage hunk",
            "Discard hunk…",
            "—",
            "Stage file",
            "Discard file…",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        /// <summary>Check 4 step 7: the one hunk header of <c>git diff --cached -- src/Calculator.cs</c>.</summary>
        public const string NegateStagedHunkHeader = "@@ -29,4 +29,5 @@ public sealed class Calculator";

        /// <summary>Check 4 step 7: the line that hunk adds, as line 32 of the index's 33-line file.</summary>
        public const string NegateLine = "    public int Negate(int a) => -a;";

        /// <summary>Check 4 step 7: the unstaged diff's second hunk after "Stage selected lines".</summary>
        public static readonly string[] CalculatorSecondHunkAfterNegate =
        [
            "@@ -29,5 +29,8 @@ public sealed class Calculator",
            "C 29/29 {",
            "C 30/30 return a / b;",
            "C 31/31 }",
            "A -/32",
            "C 32/33 public int Negate(int a) => -a;",
            "A -/34",
            "A -/35 public int Square(int a) => a * a;",
            "C 33/36 }",
        ];

        public static readonly string[] StagedAfterNegate = ["M README.md", "M Calculator.cs src", "R helpers.py src renamed from src/util.py"];

        public static readonly string[] CommitFileMenu = ["Calculator.cs", "Open file", "Show in folder", "Copy path"];

        public const double CommitFileMenuHeight = 110;

        public static readonly string[] WorkingRowMenu =
        [
            "Working changes",
            "Stage all",
            "Unstage all",
            "Discard all changes… (danger)",
            "—",
            "Stash changes…",
            "Create branch here…",
            "—",
            "Show in folder",
        ];

        /// <summary>Check 4 step 10: the staged list after "Stage all" (phase 2's check 9 step 3).</summary>
        public static readonly string[] StagedAfterStageAll =
        [
            "M README.md",
            "M logo.png assets",
            "A settings.json config",
            "M blob.bin data",
            "M large.txt data",
            "A guide.md docs",
            "D old-notes.txt docs",
            "M Calculator.cs src",
            "R helpers.py src renamed from src/util.py",
        ];

        public static readonly string[] WorkingRowMenuAllStaged =
        [
            "Working changes",
            "Stage all (disabled: \"Nothing to stage\")",
            "Unstage all",
            "Discard all changes… (disabled: \"Nothing to discard\")",
            "—",
            "Stash changes…",
            "Create branch here…",
            "—",
            "Show in folder",
        ];

        // ---- Check 5: the keyboard. ----

        /// <summary>Check 5 step 1: the keyboard menu's top-left, the bottom-left of "Alpha two"'s message cell (116 + 5 × 26).</summary>
        public static readonly (double X, double Y) AlphaTwoMessageCellBottomLeft = (454, 246);

        public static readonly string[] MainWorkCommitMenu =
        [
            "88d6b91 Main work",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto main (disabled: \"This commit is already on main\")",
            "Revert this commit",
            "Reset main to this commit…",
            "—",
            "Copy SHA",
        ];

        // ---- Check 5b: placement and light dismiss. ----

        /// <summary>Check 5b step 1: the right-click below the last row.</summary>
        public static readonly (double X, double Y) BelowLastRowPoint = (690, 660);

        /// <summary>Check 5b step 2: the right-click on core.txt's row in the details panel, at window x 1090.</summary>
        public const double DetailsFileRowX = 1090;

        public static readonly string[] CoreFileMenu = ["core.txt", "Open file", "Show in folder", "Copy path"];

        /// <summary>Check 5b step 6: where the wheel turns, over the left panel.</summary>
        public static readonly (double X, double Y) LeftPanelWheelPoint = (130, 400);

        // ---- Check 6: double-click. ----

        /// <summary>Check 6: the ops graph with the local feature/alpha deleted before the app starts.</summary>
        public static readonly string[] OpsWithoutAlphaGraph =
        [
            "0e95aca On main: Experiment [Stash:stash@{0}] 0/0",
            "078ef05 Conflicting change [LocalBranch:feature/conflict] 1/1",
            "d93814d Fast-forward me [LocalBranch:feature/ff] 2/2",
            "88d6b91 Main work [CurrentBranch:main, RemoteBranch:origin/main, Tag:v1.1] 0/0 ends 2",
            "ce5ea6a Alpha two [RemoteBranch:origin/feature/alpha] 2/3",
            "dff8fbf Alpha one [] 2/3",
            "8beca34 Add core [Tag:v1.0] 0/0 ends 1,2",
            "4ef1f7c Initial commit [] 0/0",
        ];

        /// <summary>Check 6 at the start: Local branches 3.</summary>
        public static readonly string[] OpsWithoutAlphaLocalBranches = ["▾ Local branches 3", "  ▸ feature", "  main (head)"];

        /// <summary>Check 6 step 6: the new tracking branch's message.</summary>
        public const string TrackedMessage = "Created feature/alpha tracking origin/feature/alpha and checked it out";

        /// <summary>Check 6 step 6: the graph with HEAD on the new feature/alpha.</summary>
        public static readonly string[] OpsOnNewAlphaGraph =
        [
            "0e95aca On main: Experiment [Stash:stash@{0}] 0/0",
            "078ef05 Conflicting change [LocalBranch:feature/conflict] 1/1",
            "d93814d Fast-forward me [LocalBranch:feature/ff] 2/2",
            "88d6b91 Main work [LocalBranch:main, RemoteBranch:origin/main, Tag:v1.1] 0/0 ends 2",
            "ce5ea6a Alpha two [CurrentBranch:feature/alpha, RemoteBranch:origin/feature/alpha] 2/3",
            "dff8fbf Alpha one [] 2/3",
            "8beca34 Add core [Tag:v1.0] 0/0 ends 1,2",
            "4ef1f7c Initial commit [] 0/0",
        ];

        /// <summary>Check 6 step 7: README.md after "Applied stash@{0}".</summary>
        public const string ReadmeStashed = "# Operations\n\nExperiment.\n";

        /// <summary>Check 6 step 7: the working-changes ring in Alpha two's lane and colour (lane 2, colour 3).</summary>
        public const int WorkingRingLane = 2;

        public const int WorkingRingColour = 3;

        // ---- Check 7: drag a branch onto a branch. ----

        /// <summary>
        /// Check 7: the centre of the label <c>feature/alpha</c> on "Alpha two" (row 4) in the window
        /// at 1100×700, measured by the scripted run (its label widths come from the font) and
        /// asserted there (±1): the real-window pass presses here.
        /// </summary>
        public static readonly (double X, double Y) FeatureAlphaLabelCentre = (307.8, 233);

        /// <summary>Check 7: the centre of the label <c>main</c> on "Main work" (row 3), as above: the real-window pass drops here.</summary>
        public static readonly (double X, double Y) MainLabelCentre = (291.4, 207);

        /// <summary>Check 7: how many equal steps every drag of the check moves in.</summary>
        public const int DragSteps = 5;

        /// <summary>Check 7 step 1: the pill is this far right of and below the pointer (4g).</summary>
        public const double PillOffset = 8;

        public static readonly string[] AlphaOntoMainDropMenu =
        [
            "feature/alpha → main",
            "Merge feature/alpha into main",
            "Merge feature/alpha into main (create a merge commit)",
            "Fast-forward main to feature/alpha (disabled: \"main has commits that feature/alpha does not have\")",
        ];

        public const double DropMenuHeight = 110;

        public static readonly string[] FfOntoMainDropMenu =
        [
            "feature/ff → main",
            "Merge feature/ff into main",
            "Merge feature/ff into main (create a merge commit)",
            "Fast-forward main to feature/ff",
        ];

        public static readonly string[] AlphaOntoConflictDropMenu =
        [
            "feature/alpha → feature/conflict",
            "Fast-forward feature/conflict to feature/alpha (disabled: \"feature/conflict has commits that feature/alpha does not have\")",
            "Check out feature/conflict and merge feature/alpha into it",
        ];

        public static readonly string[] MainOntoOriginAlphaDropMenu =
        [
            "main → origin/feature/alpha",
            "Push main to origin/feature/alpha",
            "Push main to origin/feature/alpha and track it",
        ];

        /// <summary>Check 7 step 7: the merge commit plain git gives for the same merge.</summary>
        public const string DropMergeShort = "f19f50a";

        public const string DropMergeSubject = "Merge branch 'feature/alpha'";

        public const string MergedMessage = "Merged feature/alpha into main";

        /// <summary>Check 7 step 7: the graph after the merge, 9 rows, 5 lanes; "Alpha two" is row 5.</summary>
        public static readonly string[] MergedByDropGraph =
        [
            "f19f50a Merge branch 'feature/alpha' [CurrentBranch:main] 0/0 out 1/1",
            "0e95aca On main: Experiment [Stash:stash@{0}] 2/2",
            "078ef05 Conflicting change [LocalBranch:feature/conflict] 3/3",
            "d93814d Fast-forward me [LocalBranch:feature/ff] 4/4",
            "88d6b91 Main work [RemoteBranch:origin/main, Tag:v1.1] 0/0 ends 2,4",
            "ce5ea6a Alpha two [LocalBranch:feature/alpha, RemoteBranch:origin/feature/alpha] 1/1",
            "dff8fbf Alpha one [] 1/1",
            "8beca34 Add core [Tag:v1.0] 0/0 ends 1,3",
            "4ef1f7c Initial commit [] 0/0",
        ];

        public const int MergedAlphaTwoRow = 5;

        public const int MergedLanes = 5;

        // ---- Check 23: the toolbar. ----

        public const string BranchCreatedMessage = "Created topic/new and checked it out";

        public const string TopicNoUpstream = "topic/new has no upstream: set one first";

        /// <summary>Check 23 step 3: the texts written outside the app.</summary>
        public const string ToolbarReadme = "# Operations\n\nToolbar change.\n";

        public const string Scratch = "scratch\n";

        /// <summary>Check 23 step 3: the graph after "Stash", 9 rows, four lanes; "???????" for the stash the app made.</summary>
        public static readonly string[] StashedGraph =
        [
            "??????? On topic/new: Toolbar stash [Stash:stash@{0}] 0/0",
            "0e95aca On main: Experiment [Stash:stash@{1}] 1/1",
            "078ef05 Conflicting change [LocalBranch:feature/conflict] 2/2",
            "d93814d Fast-forward me [LocalBranch:feature/ff] 3/3",
            "88d6b91 Main work [CurrentBranch:topic/new, LocalBranch:main, RemoteBranch:origin/main, Tag:v1.1] 0/0 ends 1,3",
            "ce5ea6a Alpha two [LocalBranch:feature/alpha, RemoteBranch:origin/feature/alpha] 1/4",
            "dff8fbf Alpha one [] 1/4",
            "8beca34 Add core [Tag:v1.0] 0/0 ends 1,2",
            "4ef1f7c Initial commit [] 0/0",
        ];

        /// <summary>Check 23 step 4: after "Pop", the ops graph again with topic/new on "Main work".</summary>
        public static readonly string[] PoppedGraph =
        [
            "0e95aca On main: Experiment [Stash:stash@{0}] 0/0",
            "078ef05 Conflicting change [LocalBranch:feature/conflict] 1/1",
            "d93814d Fast-forward me [LocalBranch:feature/ff] 2/2",
            "88d6b91 Main work [CurrentBranch:topic/new, LocalBranch:main, RemoteBranch:origin/main, Tag:v1.1] 0/0 ends 2",
            "ce5ea6a Alpha two [LocalBranch:feature/alpha, RemoteBranch:origin/feature/alpha] 2/3",
            "dff8fbf Alpha one [] 2/3",
            "8beca34 Add core [Tag:v1.0] 0/0 ends 1,2",
            "4ef1f7c Initial commit [] 0/0",
        ];

        public const string MakeFirstCommit = "Make the first commit first";

        public const string MainNoUpstream = "main has no upstream: set one first";

        public const string NoStashes = "There are no stashes";

        public const string NoRemotes = "This repository has no remotes";

        public const string AddRemoteFirst = "Add a remote first";

        public const string DetachedPull = "HEAD is detached: check out a branch to pull";

        public const string DetachedPush = "HEAD is detached: check out a branch to push";

        public const string LocTracksLocal = "loc tracks a local branch";

        public const string FinishMerge = "Finish or abort the merge first";

        /// <summary>Check 23 step 5: where the unborn repository's graph area is right-clicked, below "No commits yet".</summary>
        public static readonly (double X, double Y) UnbornAreaPoint = (480, 470);

        public static readonly string[] UnbornEmptyAreaMenu =
        [
            "unborn",
            "Fetch all remotes",
            "Pull (disabled: \"main has no upstream: set one first\")",
            "Push (disabled: \"Make the first commit first\")",
            "—",
            "Create branch… (disabled: \"Make the first commit first\")",
            "Stash changes… (disabled: \"Make the first commit first\")",
            "—",
            "Manage remotes…",
        ];

        // ---- Check 26: ignore. ----

        public static readonly string[] IgnoreStartUnstaged =
        [
            "A .gitignore",
            "A TODO",
            "A [wip].txt drafts",
            "A run.log logs",
            "M core.txt src",
            "A trace.log",
        ];

        public static readonly string[] RunLogMenu =
        [
            "run.log",
            "Stage",
            "Discard changes…",
            "—",
            "Ignore run.log",
            "Ignore all *.log files",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        public static readonly string[] TraceLogDiffMenu =
        [
            "trace.log",
            "Stage hunk",
            "Discard hunk…",
            "—",
            "Stage file",
            "Discard file…",
            "—",
            "Ignore trace.log",
            "Ignore all *.log files",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        public static readonly string[] TodoMenu =
        [
            "TODO",
            "Stage",
            "Discard changes…",
            "—",
            "Ignore TODO",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        public static readonly string[] CoreTrackedMenu =
        [
            "core.txt",
            "Stage",
            "Discard changes…",
            "—",
            "Ignore core.txt (disabled: \"Only untracked files can be ignored\")",
            "Ignore all *.txt files (disabled: \"Only untracked files can be ignored\")",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];
    }
}
