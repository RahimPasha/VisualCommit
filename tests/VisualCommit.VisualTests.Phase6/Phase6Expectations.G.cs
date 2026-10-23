// Part G's expected results (docs/test-reports/phase-6.md, "Part G: git behaviour: ignored files
// kept, and fetch refspecs" and the rows G22 to G25 of "Checks"), copied from the report: the
// scenarios' SHAs and files, the graphs IG0 to IG3 and RS0 to RS5, the left panel, the menus, the
// texts of design 14 the checks rely on, the commands, the app's own reads TR1 to TR9 with their
// outputs, and git's texts TG1 to TG12 (Git 2.36.0.windows.1's, asserted word for word only on that
// git, phase 3's rule). Only part G's writer edits this file; its members live in
// Phase6Expectations.PartG. Compiled by the real-window pass too: see Phase6Expectations.Common.cs
// for what may be used here (no type of the walk-through's own files).
// A file named *.G.cs is generated code to the compiler (its *.g.cs rule, which ignores case):
// nullable annotations are off in it unless the file turns them on, as it does here, and the
// analyzers that leave generated code alone leave it alone too.
#nullable enable
using Avalonia;
using VisualCommit.Core.Graph;

namespace VisualCommit.VisualTests.Phase6;

public static partial class Phase6Expectations
{
    /// <summary>What part G's checks (G22 to G25) compare against.</summary>
    public static class PartG
    {
        // ---- "ignored" (Scenarios.IgnoredAsync), as the report's scenarios section and Phase6ScenarioTests give it. ----

        /// <summary>The tab's and the repository's name of the "ignored" scenario.</summary>
        public const string Ignored = "ignored";

        public const string InitialCommitSha = "2e4599b4149a2686ea72c962920d8ba1764446d2";

        public const string TrackSecretSha = "1ac759d23fd78a78f7660d289a1373ea85378b69";

        public const string StopTrackingSha = "39bd8031f98ec9a1a6d02519ffabd35ca693bc63";

        public const string VendorSecretSha = "133a796ef1cfa3b4f2dc3237b5342751f85fcc3f";

        public const string BuildFileSha = "c8446bb6a7efb5eb9cc91d4a95e2bd37c0be7ced";

        public const string MainWorkSha = "c49cac40cc05ce7ebe709555621751082c868662";

        /// <summary>The stash <c>stash@{0}</c> "On main: Stash the secret": parents Main work and its index commit, no untracked part.</summary>
        public const string StashSha = "c8795683e3b86ad70873326b9897659e3c5140dd";

        /// <summary>The stash's index commit, "index on main: c49cac4 Main work".</summary>
        public const string StashIndexSha = "ea37eb6d131f2c233f9b69403441926176582a5b";

        /// <summary>IG1's "Start the side branch": a commit on Track the secret with its tree (GD7), 12:07.</summary>
        public const string SideStartSha = "bf49f82d2de88c816e12a4ef8560c983a2c4c8db";

        /// <summary>IG1's "Merge branch 'side'" (parents Main work and Start the side branch; Main work's tree; 12:07).</summary>
        public const string SideMergeSha = "6e68a258092ca05b31cd7d3a8c359276bafc2bf7";

        /// <summary>IG2's "Merge branch 'vendor'" (parents Main work and Add the vendor secret; Test Author, D72's 2026-01-02 09:00).</summary>
        public const string VendorMergeSha = "63b8fe1713a49735d84bb334cf1b86c66cef8cdb";

        /// <summary>How <c>git stash list</c>, the left panel and the graph name the stash.</summary>
        public const string StashListed = "On main: Stash the secret";

        /// <summary>The ignored <c>secret.env</c> in the working tree.</summary>
        public const string SecretLocal = "LOCAL\n";

        /// <summary>The ignored <c>build/out.bin</c> in the working tree.</summary>
        public const string BuildOutput = "binary\n";

        /// <summary><c>secret.env</c> as Add the vendor secret tracks it.</summary>
        public const string SecretVendor = "vendor\n";

        /// <summary>The file <c>build</c> as Add the build file tracks it.</summary>
        public const string BuildFile = "build\n";

        /// <summary><c>app.txt</c> on <c>main</c>.</summary>
        public const string AppMain = "app\nmain\n";

        /// <summary><c>app.txt</c> on <c>tools</c> (Initial commit's).</summary>
        public const string AppInitial = "app\n";

        /// <summary><c>git status --porcelain=v1 --untracked-files=all --ignored</c> of IG0 (GD17).</summary>
        public static readonly string[] IgnoredStatus = ["!! build/out.bin", "!! secret.env"];

        /// <summary>The same after G24's merge (IG2): <c>secret.env</c> is tracked now.</summary>
        public static readonly string[] MergedStatus = ["!! build/out.bin"];

        /// <summary>IG0's refs, as <c>git for-each-ref --format="%(refname) %(objectname)"</c> lists them.</summary>
        public static readonly string[] Ig0Refs =
        [
            "refs/heads/main " + MainWorkSha,
            "refs/heads/tools " + BuildFileSha,
            "refs/heads/vendor " + VendorSecretSha,
            "refs/remotes/origin/main " + MainWorkSha,
            "refs/remotes/origin/vendor " + VendorSecretSha,
            "refs/remotes/origin/vendor-remote " + VendorSecretSha,
            "refs/stash " + StashSha,
        ];

        /// <summary>IG1's refs: IG0's with <c>main</c> at the side merge and the branch <c>side</c>.</summary>
        public static readonly string[] Ig1Refs =
        [
            "refs/heads/main " + SideMergeSha,
            "refs/heads/side " + SideStartSha,
            "refs/heads/tools " + BuildFileSha,
            "refs/heads/vendor " + VendorSecretSha,
            "refs/remotes/origin/main " + MainWorkSha,
            "refs/remotes/origin/vendor " + VendorSecretSha,
            "refs/remotes/origin/vendor-remote " + VendorSecretSha,
            "refs/stash " + StashSha,
        ];

        /// <summary>The index of IG0 (and IG1): <c>.gitignore</c> <c>53985eb</c>, <c>README.md</c> <c>6f4e424</c>, <c>app.txt</c> <c>20982c0</c>, as abbreviated object names.</summary>
        public static readonly (string Path, string Blob)[] Ig0Index = [(".gitignore", "53985eb"), ("README.md", "6f4e424"), ("app.txt", "20982c0")];

        /// <summary>The branch keys of "ignored" (<c>git config --get-regexp ^branch\.</c>), in the file's order.</summary>
        public static readonly string[] IgnoredBranchKeys =
        [
            "branch.main.remote origin",
            "branch.main.merge refs/heads/main",
            "branch.vendor.remote origin",
            "branch.vendor.merge refs/heads/vendor",
        ];

        /// <summary>HEAD's reflog of IG0 has 10 entries, the newest the stash's reset.</summary>
        public const int Ig0ReflogEntries = 10;

        /// <summary>The newest entry of IG0's HEAD reflog (<c>git log -g --format=%gs HEAD</c>).</summary>
        public const string StashResetReflog = "reset: moving to HEAD";

        /// <summary>IG1's construction adds this entry to HEAD's reflog.</summary>
        public const string SideMergeReflog = "merge side: Merge made by the 'ort' strategy.";

        /// <summary>G24's merge's entry.</summary>
        public const string VendorMergeReflog = "merge refs/heads/vendor: Merge made by the 'ort' strategy.";

        /// <summary>G24's checkout's entry.</summary>
        public const string ToolsCheckoutReflog = "checkout: moving from main to tools";

        // ---- The graphs of "ignored" (IG0, IG1, IG2, IG3). ----

        /// <summary>IG0, IG2 and IG3 have three lanes (Graph 64, Message x 454, Date x 580 to 700).</summary>
        public const int IgLanes = 3;

        /// <summary>IG1 has four lanes (Graph 80, Message x 470 to 700, no Date column at 1100).</summary>
        public const int Ig1Lanes = 4;

        /// <summary>IG0's rows, as <c>SelectedIndex</c> counts them.</summary>
        public const int StashRow = 0;

        public const int MainWorkRow = 1;

        public const int BuildFileRow = 2;

        public const int VendorSecretRow = 3;

        public const int StopTrackingRow = 4;

        public const int TrackSecretRow = 5;

        public const int InitialCommitRow = 6;

        /// <summary>IG0, "ignored" as built (scenario facts 4.1).</summary>
        public static readonly CommitRow[] Ig0 =
        [
            Stash(StashListed, 0, 0, "c879568", "2026-01-01 12:06"),
            Commit("Main work", 0, 0, "c49cac4", "2026-01-01 12:05", Current("main"), "origin/main"),
            Commit("Add the build file", 1, 1, "c8446bb", "2026-01-01 12:04", "tools"),
            Commit("Add the vendor secret", 2, 2, "133a796", "2026-01-01 12:03", "vendor", "origin/vendor", "origin/vendor-remote"),
            Commit("Stop tracking the secret", 0, 0, "39bd803", "2026-01-01 12:02") with { Ends = [1, 2] },
            Commit("Track the secret", 0, 0, "1ac759d", "2026-01-01 12:01"),
            Commit("Initial commit", 0, 0, "2e4599b", "2026-01-01 12:00"),
        ];

        /// <summary>IG0's drawn labels (<c>LabelsInRow</c>): row 1 <c>main</c> ✓ and <c>+1</c>, row 3 <c>vendor</c> and <c>+2</c>.</summary>
        public static readonly (int Row, string[] Drawn)[] Ig0Drawn = [(MainWorkRow, ["main", "+1"]), (VendorSecretRow, ["vendor", "+2"])];

        /// <summary>IG0's dashed lines: lane 0 from the stash's ring (row 0) into Main work's node (row 1).</summary>
        public static readonly (int Row, GraphLineKind Kind, int Lane)[] Ig0Dashes = [(0, GraphLineKind.OutOfNode, 0), (1, GraphLineKind.IntoNode, 0)];

        /// <summary>IG1, "ignored" with the side branch merged into <c>main</c> (G23 part B, GD7): no Date column.</summary>
        public static readonly CommitRow[] Ig1 =
        [
            Commit("Merge branch 'side'", 0, 0, "6e68a25", null, Current("main")) with { Merge = true },
            Commit("Start the side branch", 1, 1, "bf49f82", null, "side"),
            Stash(StashListed, 2, 2, "c879568", null),
            Commit("Main work", 0, 0, "c49cac4", null, "origin/main"),
            Commit("Add the build file", 2, 3, "c8446bb", null, "tools"),
            Commit("Add the vendor secret", 3, 4, "133a796", null, "vendor", "origin/vendor", "origin/vendor-remote"),
            Commit("Stop tracking the secret", 0, 0, "39bd803", null) with { Ends = [2, 3] },
            Commit("Track the secret", 0, 0, "1ac759d", null) with { Ends = [1] },
            Commit("Initial commit", 0, 0, "2e4599b", null),
        ];

        /// <summary>IG1's dashed lines: lane 2 from the stash's ring (row 2) into Main work's node (row 3).</summary>
        public static readonly (int Row, GraphLineKind Kind, int Lane)[] Ig1Dashes = [(2, GraphLineKind.OutOfNode, 2), (3, GraphLineKind.IntoNode, 2)];

        /// <summary>IG2, IG0 after G24's merge (setting off): eight rows, three lanes.</summary>
        public static readonly CommitRow[] Ig2 =
        [
            Commit("Merge branch 'vendor'", 0, 0, "63b8fe1", "2026-01-02 09:00", Current("main")) with { Merge = true },
            Stash(StashListed, 2, 2, "c879568", "2026-01-01 12:06"),
            Commit("Main work", 0, 0, "c49cac4", "2026-01-01 12:05", "origin/main"),
            Commit("Add the build file", 2, 3, "c8446bb", "2026-01-01 12:04", "tools"),
            Commit("Add the vendor secret", 1, 1, "133a796", "2026-01-01 12:03", "vendor", "origin/vendor", "origin/vendor-remote"),
            Commit("Stop tracking the secret", 0, 0, "39bd803", "2026-01-01 12:02") with { Ends = [1, 2] },
            Commit("Track the secret", 0, 0, "1ac759d", "2026-01-01 12:01"),
            Commit("Initial commit", 0, 0, "2e4599b", "2026-01-01 12:00"),
        ];

        /// <summary>IG2's and IG3's dashed lines: lane 2 from the stash's ring (row 1) into Main work's node (row 2).</summary>
        public static readonly (int Row, GraphLineKind Kind, int Lane)[] Ig2Dashes = [(1, GraphLineKind.OutOfNode, 2), (2, GraphLineKind.IntoNode, 2)];

        /// <summary>IG3, IG2 after G24's checkout of <c>tools</c>: row 0 <c>main</c> without the check mark, row 3 <c>tools</c> ✓.</summary>
        public static readonly CommitRow[] Ig3 =
        [
            Ig2[0] with { Labels = ["main"] },
            Ig2[1],
            Ig2[2],
            Ig2[3] with { Labels = [Current("tools")] },
            .. Ig2[4..],
        ];

        /// <summary>The x a row is clicked at "in its Message column" in IG0, IG2 and IG3: 30 into the Message column at x 454.</summary>
        public const double IgRowPointX = 484;

        /// <summary>Where commit row <paramref name="row"/> of IG0 is clicked: (484, 129 + 26 r), for example (484, 207) and (484, 233).</summary>
        public static Point IgRowPoint(int row) => new(IgRowPointX, 129 + (26 * row));

        // ---- The left panel of "ignored", as the report writes it (Phase6ChecksG's LeftPanelLines). ----

        /// <summary>The left panel of IG0 (and of IG0 through G22 and G23 part A).</summary>
        public static readonly string[] Ig0Panel =
        [
            "Local branches 3",
            "  main (accented)",
            "  tools",
            "  vendor",
            "Remotes 3",
            "  origin (open)",
            "    main",
            "    vendor",
            "    vendor-remote",
            "Pull requests 0",
            "Tags 0",
            "Stashes 1",
            "  " + StashListed,
        ];

        /// <summary>The left panel of IG1: <c>main</c> accented with ↑ 2, and <c>side</c>.</summary>
        public static readonly string[] Ig1Panel = IgPanel(("main", true, 2, 0), ("side", false, 0, 0), ("tools", false, 0, 0), ("vendor", false, 0, 0));

        /// <summary>The left panel of IG1v: <c>main</c> ↑ 3 ↓ 1 against <c>origin/vendor</c>.</summary>
        public static readonly string[] Ig1vPanel = IgPanel(("main", true, 3, 1), ("side", false, 0, 0), ("tools", false, 0, 0), ("vendor", false, 0, 0));

        /// <summary>The left panel of IG2: <c>main</c> accented with ↑ 2.</summary>
        public static readonly string[] Ig2Panel = IgPanel(("main", true, 2, 0), ("tools", false, 0, 0), ("vendor", false, 0, 0));

        /// <summary>The left panel of IG3: <c>tools</c> accented, <c>main</c> still ↑ 2.</summary>
        public static readonly string[] Ig3Panel = IgPanel(("main", false, 2, 0), ("tools", true, 0, 0), ("vendor", false, 0, 0));

        private static string[] IgPanel(params (string Name, bool Head, int Ahead, int Behind)[] branches) =>
        [
            $"Local branches {branches.Length}",
            .. branches.Select(branch => "  " + PanelBranch(branch.Name, branch.Head, branch.Ahead, branch.Behind)),
            .. Ig0Panel[4..],
        ];

        /// <summary>A local branch's row as <see cref="Ig0Panel"/> writes it: its name, " (accented)" when HEAD is on it, then " ↑a" and " ↓b" above 0.</summary>
        public static string PanelBranch(string name, bool head, int ahead, int behind) =>
            name + (head ? " (accented)" : string.Empty) + (ahead > 0 ? $" ↑{ahead}" : string.Empty) + (behind > 0 ? $" ↓{behind}" : string.Empty);

        // ---- Texts (design 9.15, 14; D179, D196). ----

        /// <summary>The title of every checkout git refused for something in the way.</summary>
        public const string CheckoutBlockedTitle = "Checkout blocked";

        /// <summary>D179's text for one ignored file git's <c>--no-overwrite-ignore</c> kept (<c>IgnoredFilesInTheWay</c>), naming the target as 9.11's <c>plan.Name</c> does.</summary>
        public static string IgnoredFileInTheWay(string name) => $"1 ignored file is in the way: {name} tracks a file of the same name. Move it away first.";

        /// <summary>The app's own refusal before git (<c>IgnoredFilesInTheWayOf</c>): "merge", "rebase", "cherry-pick", "revert".</summary>
        public static string IgnoredFileInTheWayOf(string operation) => $"1 ignored file is in the way of the {operation}: secret.env. Move it away first.";

        /// <summary>The stash's refusal before git (<c>IgnoredFilesInTheWayOfStash</c>).</summary>
        public const string IgnoredFileInTheWayOfStash = "1 ignored file is in the way of applying the stash: secret.env. Move it away first.";

        /// <summary>The status bar after a failure: "&lt;Title&gt; failed".</summary>
        public static string Failed(string title) => title + " failed";

        /// <summary>The error dialog's buttons with git's output.</summary>
        public static readonly string[] ButtonsWithOutput = ["Copy output", "Close"];

        /// <summary>A refusal the app makes before git: "Close" only.</summary>
        public static readonly string[] CloseOnly = ["Close"];

        /// <summary>The toolbar's Undo and Redo while nothing is recorded.</summary>
        public const string NothingToUndo = "Nothing to undo";

        // ---- G22: the checkouts. ----

        public const string CheckOutVendor = "Check out vendor";

        public const string CheckOutOriginVendor = "Check out origin/vendor";

        public const string CheckOutVendorRemote = "Check out origin/vendor-remote";

        public const string CreateBranchVendorCopy = "Create branch vendor-copy";

        public const string CheckOutTools = "Check out tools";

        /// <summary>The detached checkout's title, by the short SHA (9.11).</summary>
        public const string CheckOutVendorSecret = "Check out 133a796";

        public const string CheckOutThisCommitItem = "Check out this commit";

        public const string CreateBranchHereItem = "Create branch here…";

        /// <summary>The new branch of G22 step 5 (GD3).</summary>
        public const string VendorCopy = "vendor-copy";

        /// <summary>The name dialog's title and its confirm button (phase 3).</summary>
        public const string CreateBranchTitle = "Create branch";

        /// <summary>The name dialog's context when "Create branch here…" starts from vendor's branch menu (GD4).</summary>
        public const string FromVendor = "From vendor";

        /// <summary>The name dialog's option, ticked.</summary>
        public const string CheckOutNewBranchOption = "Check out the new branch";

        public const string SwitchVendorCommand = "git switch --no-guess --no-overwrite-ignore vendor";

        public const string SwitchVendorRemoteCommand = "git switch --create vendor-remote --track --no-overwrite-ignore refs/remotes/origin/vendor-remote";

        public const string CreateVendorCopyCommand = "git switch --create vendor-copy --no-overwrite-ignore refs/heads/vendor";

        public const string SwitchToolsCommand = "git switch --no-guess --no-overwrite-ignore tools";

        public const string DetachVendorSecretCommand = "git switch --detach --no-overwrite-ignore " + VendorSecretSha;

        /// <summary>The app's question to git about the paths in the way (9.3, D74: without <c>GIT_LITERAL_PATHSPECS</c>).</summary>
        public const string CheckIgnoreCommand = "git check-ignore --stdin -z";

        /// <summary><c>check-ignore</c>'s answer: <c>secret.env</c>, NUL-terminated.</summary>
        public const string SecretIgnored = "secret.env\0";

        /// <summary><c>check-ignore</c>'s answer for the folder by its files.</summary>
        public const string BuildOutIgnored = "build/out.bin\0";

        /// <summary>The key line of git's refusal for an untracked (here ignored) file in the way (TG1).</summary>
        public const string UntrackedKey = "error: The following untracked working tree files would be overwritten by checkout:";

        /// <summary>The key line of git's refusal for a folder in the way (TG2).</summary>
        public const string FolderKey = "error: Updating the following directories would lose untracked files in them:";

        /// <summary>TG1, as <c>ErrorOutput</c> shows it after <c>GitOutputCleaner.Clean</c> (Git 2.36; 2.55 identical).</summary>
        public static readonly string[] Tg1 =
        [
            UntrackedKey,
            "\tsecret.env",
            "Please move or remove them before you switch branches.",
            "Aborting",
        ];

        /// <summary>TG2: the folder <c>build</c> in the way (git's empty line before "Aborting" dropped by the cleaning).</summary>
        public static readonly string[] Tg2 =
        [
            FolderKey,
            "\tbuild",
            "Aborting",
        ];

        // ---- G23 part A: the app's own check before git. ----

        public const string MergeVendorItem = "Merge vendor into main";

        public const string RebaseOntoVendorItem = "Rebase main onto vendor";

        public const string CherryPickItem = "Cherry-pick onto main";

        public const string RevertItem = "Revert this commit";

        public const string ApplyStashItem = "Apply stash";

        public const string PopStashItem = "Pop stash";

        public const string MergeVendor = "Merge vendor";

        public const string RebaseOntoVendor = "Rebase main onto vendor";

        public const string CherryPickVendorSecret = "Cherry-pick 133a796";

        public const string RevertStopTracking = "Revert 39bd803";

        public const string ApplyStash = "Apply stash@{0}";

        public const string PopStash = "Pop stash@{0}";

        public const string MergeFailedTitle = "Merge failed";

        public const string RebaseFailedTitle = "Rebase failed";

        public const string CherryPickFailedTitle = "Cherry-pick failed";

        public const string RevertFailedTitle = "Revert failed";

        public const string ApplyingStashFailedTitle = "Applying the stash failed";

        /// <summary>The stash's menu (G23 step 6), as the report writes menus; "Pop stash" has the key <see cref="PopKey"/> at its right.</summary>
        public static readonly string[] StashMenu =
        [
            "stash@{0} " + StashListed,
            ApplyStashItem,
            PopStashItem,
            "Drop stash… (danger)",
            "—",
            "Copy SHA",
        ];

        /// <summary>The stash menu's height: four items and one separator (8 + 24 + 26 × 4 + 9).</summary>
        public const double StashMenuHeight = 145;

        /// <summary>The newest stash's Pop key (6.7) and the toolbar's Pop (14.11).</summary>
        public const string PopKey = "Ctrl+Shift+O";

        /// <summary>The toolbar's Pop's tooltip while enabled (14.11, 6.8).</summary>
        public const string PopToolTip = "Pop the newest stash (Ctrl+Shift+O)";

        /// <summary>Calls whose command starts with one of these are part G's "write" (its conventions' "No write").</summary>
        public static readonly string[] WritePrefixes =
        [
            "git merge ", "git rebase", "git cherry-pick", "git revert", "git stash apply", "git stash pop", "git stash push",
            "git switch", "git reset", "git commit", "git branch ", "git update-ref", "git push",
        ];

        /// <summary>TR1: the merge of vendor into main (9.4), in this order.</summary>
        public static readonly ExpectedRead[] Tr1 =
        [
            new("git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD refs/heads/vendor --", 0, SecretIgnored),
            new(CheckIgnoreCommand, 0, SecretIgnored),
            new("git merge-base --all HEAD refs/heads/vendor", 0, StopTrackingSha),
            new($"git diff-tree -r -z --no-renames --name-only --diff-filter=AMT {StopTrackingSha} refs/heads/vendor --", 0, SecretIgnored),
        ];

        /// <summary>TR2's four reads, run side by side (asserted as a set, GD18): the rebase of main onto vendor's SHA (9.6).</summary>
        public static readonly ExpectedRead[] Tr2SideBySide =
        [
            new($"git merge-base --is-ancestor {VendorSecretSha} HEAD", 1, string.Empty),
            new($"git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD {VendorSecretSha} --", 0, SecretIgnored),
            new($"git log --no-renames --name-only -z --root --no-merges --no-show-signature --format=%x1e%H --diff-filter=AMT {VendorSecretSha}..HEAD --", 0, "\u001e" + MainWorkSha + "\0\napp.txt\0"),
            new($"git rev-list --min-parents=2 --max-count=1 {VendorSecretSha}..HEAD --", 0, string.Empty),
        ];

        /// <summary>TR2's last read, after the four: <c>check-ignore</c> of <c>app.txt</c> and <c>secret.env</c>.</summary>
        public static readonly ExpectedRead Tr2CheckIgnore = new(CheckIgnoreCommand, 0, SecretIgnored);

        /// <summary>TR3: the cherry-pick of Add the vendor secret (9.5), fed its SHA.</summary>
        public static readonly ExpectedRead[] Tr3 =
        [
            new("git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT", 0, SecretIgnored),
            new(CheckIgnoreCommand, 0, SecretIgnored),
        ];

        /// <summary>TR4: the revert of Stop tracking the secret (9.5), its deletion reversed.</summary>
        public static readonly ExpectedRead[] Tr4 =
        [
            new("git diff-tree --stdin --no-commit-id --root -r -z --no-renames --name-only --diff-filter=AMT -R", 0, SecretIgnored),
            new(CheckIgnoreCommand, 0, SecretIgnored),
        ];

        /// <summary>TR5's three stash reads (D179's count), run once in the session, kept by the stash's SHA (GD23).</summary>
        public static readonly ExpectedRead[] Tr5StashReads =
        [
            new($"git diff-tree -r -z --no-renames --name-status {StashSha}^1 {StashSha}", 0, "A\0secret.env\0"),
            new($"git diff-tree -r -z --no-renames --name-only {StashSha}^1 {StashSha}^2", 0, SecretIgnored),
            new($"git rev-parse --verify --quiet {StashSha}^3", 1, string.Empty),
        ];

        /// <summary>The status read of D179's count (and of every refresh).</summary>
        public const string StatusReadCommand = "git status --porcelain=v2 -z --untracked-files=all --renames";

        // ---- G23 part B: the pull (IG1, IG1v). ----

        /// <summary>IG1's construction before the app starts (GD7): the empty commit's arguments.</summary>
        public static readonly string[] SideStartCommitTree = ["commit-tree", TrackSecretSha + "^{tree}", "-p", TrackSecretSha, "-m", "Start the side branch"];

        public const string SideBranch = "side";

        public const string PullTitle = "Pull";

        public const string PullFailedTitle = "Pull failed";

        /// <summary>The pull's running text on IG1 (4.8, 14.1).</summary>
        public const string PullingOriginMain = "Pulling origin/main into main…";

        /// <summary>The toolbar's Pull's tooltip and help text on IG1 (6.8).</summary>
        public const string PullFromOriginMain = "Pull from origin/main (Ctrl+Shift+L)";

        /// <summary>The same on IG1v, which the step waits for (GD22).</summary>
        public const string PullFromOriginVendor = "Pull from origin/vendor (Ctrl+Shift+L)";

        /// <summary>Phase 3's result of a pull that brings nothing.</summary>
        public const string MainUpToDate = "main is already up to date";

        /// <summary><c>PullNotFastForward</c> (4.8, D211) on IG1v under <c>pull.ff=only</c>.</summary>
        public const string MainAndOriginVendorDiverged = "main and origin/vendor have diverged, and pull.ff is set to only: merge origin/vendor into main or rebase main onto origin/vendor.";

        /// <summary>The refs read the app makes after an outside write of <c>.git/config</c> (10.2's one read of the remotes, GD22).</summary>
        public const string RemotesReadCommand = @"git config --null --get-regexp ^remote\.";

        public const string PullFfRead = "git config --get pull.ff";

        public const string PullFfBoolRead = "git config --type=bool --get pull.ff";

        public const string BranchMainRebaseRead = "git config --get branch.main.rebase";

        public const string PullRebaseRead = "git config --get pull.rebase";

        public const string PullRebaseBoolRead = "git config --type=bool --get pull.rebase";

        public const string FetchOriginCommand = "git fetch --progress origin";

        public const string HeadRead = "git rev-parse --verify --quiet HEAD";

        public const string IsSideMergeAncestorOfFetchHead = $"git merge-base --is-ancestor {SideMergeSha} FETCH_HEAD";

        /// <summary>TR6's four reads, side by side (GD18): the pull's rebase on IG1 against <c>refs/remotes/origin/main</c>.</summary>
        public static readonly ExpectedRead[] Tr6SideBySide =
        [
            new("git merge-base --is-ancestor refs/remotes/origin/main HEAD", 0, string.Empty),
            new("git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD refs/remotes/origin/main --", 0, string.Empty),
            new("git log --no-renames --name-only -z --root --no-merges --no-show-signature --format=%x1e%H --diff-filter=AMT refs/remotes/origin/main..HEAD --", 0, string.Empty),
            new("git rev-list --min-parents=2 --max-count=1 refs/remotes/origin/main..HEAD --", 0, SideMergeSha),
        ];

        /// <summary>TR6's read (5): the range's branch points, asserted as the app reads it (<c>BranchPointsWriteAsync</c>).</summary>
        public const string BranchPointsRead = "git rev-list --topo-order --boundary refs/remotes/origin/main..HEAD --";

        /// <summary>(5)'s output's first line: the tip.</summary>
        public const string BranchPointsTip = SideMergeSha;

        /// <summary>(5)'s lines without <c>-</c>, as a set.</summary>
        public static readonly string[] BranchPointsInRange = [SideMergeSha, SideStartSha];

        /// <summary>(5)'s boundary lines, as a set.</summary>
        public static readonly string[] BranchPointsBoundary = ["-" + TrackSecretSha, "-" + MainWorkSha];

        /// <summary>(5)'s exact output on Git 2.36 (asserted only there), as 2.55 printed it too.</summary>
        public static readonly string[] BranchPointsOn236 = [SideMergeSha, SideStartSha, "-" + TrackSecretSha, "-" + MainWorkSha];

        /// <summary>TR6's read (6): what the boundary commits add that the tip lacks.</summary>
        public static readonly ExpectedRead BranchPointsAddRead = new("git diff-tree --stdin --no-commit-id -r -z --no-renames --name-only --diff-filter=A", 0, SecretIgnored);

        /// <summary>TR7: the pull's merge on IG1 with <c>origin/main</c>: one read, nothing a candidate.</summary>
        public static readonly ExpectedRead Tr7Added = new("git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD FETCH_HEAD --", 0, string.Empty);

        /// <summary>The pull's merge (TG11).</summary>
        public const string PullMergeCommand = "git merge --no-edit --no-overwrite-ignore FETCH_HEAD";

        /// <summary>TR8: the pull's merge on IG1v (9.4), after the HEAD read, in this order.</summary>
        public static readonly ExpectedRead[] Tr8 =
        [
            new("git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD FETCH_HEAD --", 0, SecretIgnored),
            new(CheckIgnoreCommand, 0, SecretIgnored),
            new("git merge-base --all HEAD FETCH_HEAD", 0, StopTrackingSha),
            new($"git diff-tree -r -z --no-renames --name-only --diff-filter=AMT {StopTrackingSha} FETCH_HEAD --", 0, SecretIgnored),
        ];

        /// <summary>TR9's last read on IG1v: the branches have diverged, so nothing counts as in the way.</summary>
        public static readonly ExpectedRead Tr9Diverged = new(IsSideMergeAncestorOfFetchHead, 1, string.Empty);

        /// <summary>The pull's fast-forward under <c>pull.ff=only</c> (TG3).</summary>
        public const string PullFastForwardCommand = "git merge --ff-only --no-overwrite-ignore FETCH_HEAD";

        /// <summary>TG3's key line (2.55 prints nine <c>hint:</c> lines before it).</summary>
        public const string NotFastForwardKey = "fatal: Not possible to fast-forward, aborting.";

        /// <summary>TG3 on Git 2.36: one line.</summary>
        public static readonly string[] Tg3 = [NotFastForwardKey];

        /// <summary>TG11's key line: the pull's merge of an ancestor.</summary>
        public const string AlreadyUpToDate = "Already up to date.";

        // ---- G24: Keep ignored files off. ----

        /// <summary>The merge's message read (phase 5's merge), and its output.</summary>
        public const string FmtMergeMsgCommand = "git fmt-merge-msg";

        public const string VendorMergeMessage = "Merge branch 'vendor'";

        /// <summary>Phase 5's merge, without <c>--no-overwrite-ignore</c> (P27's command before phase 6).</summary>
        public const string MergeVendorWithoutOptionCommand = "git merge --no-edit --no-log -m \"Merge branch 'vendor'\" refs/heads/vendor";

        /// <summary>TG9's key line, on standard output.</summary>
        public const string MergeMadeKey = "Merge made by the 'ort' strategy.";

        /// <summary>TG9 on Git 2.36 (standard output).</summary>
        public static readonly string[] Tg9 = [MergeMadeKey, " secret.env | 1 +", " 1 file changed, 1 insertion(+)", " create mode 100644 secret.env"];

        public const string MergedVendorIntoMain = "Merged vendor into main";

        /// <summary>Phase 5's checkout, without the option.</summary>
        public const string SwitchToolsWithoutOptionCommand = "git switch --no-guess tools";

        /// <summary>TG10's key line, on standard error.</summary>
        public const string SwitchedToTools = "Switched to branch 'tools'";

        public const string CheckedOutTools = "Checked out tools";

        // ---- "refspecs" (Scenarios.RenamingRefspecAsync). ----

        /// <summary>The tab's and the repository's name of the "refspecs" scenario.</summary>
        public const string Refspecs = "refspecs";

        public const string RsInitialSha = "cc31e4ae22a8e076d63050ef2e87c59113ae1f62";

        public const string ParserSha = "8394a5e2e769c4601161cbce8c239ff2aa246eb9";

        public const string TopicSha = "de4a87cd5a84d5ed135863187487f81fe89d030c";

        /// <summary>Every RS graph has one lane (Graph 48, Message x 438).</summary>
        public const int RsLanes = 1;

        /// <summary>The fetch lines of the refs read (10.2), in this order.</summary>
        public static readonly string[] RemoteConfigLines =
        [
            "remote.origin.url ../origin.git",
            "remote.origin.fetch +refs/heads/*:refs/remotes/origin/*",
            "remote.origin.fetch +refs/pull/*/head:refs/remotes/origin/pr/*",
            "remote.fork.url ../fork.git",
            "remote.fork.fetch +refs/heads/*:refs/remotes/fork/up/*",
            "remote.upstream.url ../upstream.git",
            "remote.upstream.fetch +refs/pull/*/head:refs/remotes/upstream/pr/*",
        ];

        /// <summary>The tracking refs of RS0.</summary>
        public static readonly string[] Rs0RemoteRefs =
        [
            "refs/remotes/fork/up/main " + ParserSha,
            "refs/remotes/origin/feature/x " + ParserSha,
            "refs/remotes/origin/main " + ParserSha,
            "refs/remotes/origin/pr/1 " + TopicSha,
            "refs/remotes/upstream/pr/7 " + TopicSha,
        ];

        /// <summary>RS0's rows: Work on the topic, Add the parser, Initial commit.</summary>
        public static CommitRow[] RsGraph(string[] row0, string[] row1) =>
        [
            Commit("Work on the topic", 0, 0, "de4a87c", "2026-01-01 12:02", row0),
            Commit("Add the parser", 0, 0, "8394a5e", "2026-01-01 12:01", row1),
            Commit("Initial commit", 0, 0, "cc31e4a", "2026-01-01 12:00"),
        ];

        /// <summary>RS0, "refspecs" as built.</summary>
        public static readonly CommitRow[] Rs0 = RsGraph(["topic", "origin/pr/1", "upstream/pr/7"], [Current("main"), "fork/up/main", "origin/feature/x", "origin/main"]);

        /// <summary>RS0's drawn labels: row 1 <c>main</c> ✓ and <c>+3</c> (GD11).</summary>
        public static readonly (int Row, string[] Drawn)[] Rs0Drawn = [(1, ["main", "+3"])];

        /// <summary>RS1, after the drop's push: git moved <c>fork/up/main</c> to Work on the topic.</summary>
        public static readonly CommitRow[] Rs1 = RsGraph(["topic", "fork/up/main", "origin/pr/1", "upstream/pr/7"], [Current("main"), "origin/feature/x", "origin/main"]);

        /// <summary>RS2, <c>pr/1</c> made (or checked out again) and checked out.</summary>
        public static readonly CommitRow[] Rs2 = RsGraph([Current("pr/1"), "topic", "fork/up/main", "origin/pr/1", "upstream/pr/7"], ["main", "origin/feature/x", "origin/main"]);

        /// <summary>RS3, <c>main</c> checked out again.</summary>
        public static readonly CommitRow[] Rs3 = RsGraph(["pr/1", "topic", "fork/up/main", "origin/pr/1", "upstream/pr/7"], [Current("main"), "origin/feature/x", "origin/main"]);

        /// <summary>RS4, fork's <c>main</c> deleted (part B, step 10).</summary>
        public static readonly CommitRow[] Rs4 = RsGraph(["topic", "origin/pr/1", "upstream/pr/7"], [Current("main"), "origin/feature/x", "origin/main"]);

        /// <summary>RS5, <c>pr/7</c> made and checked out (step 11).</summary>
        public static readonly CommitRow[] Rs5 = RsGraph([Current("pr/7"), "topic", "origin/pr/1", "upstream/pr/7"], ["main", "origin/feature/x", "origin/main"]);

        /// <summary>The x a row of an RS graph is clicked at: 30 into the Message column at x 438.</summary>
        public const double RsRowPointX = 468;

        // ---- The left panel of "refspecs". ----

        /// <summary>Local branches of RS0, RS1 and RS4: <c>main</c> accented, <c>topic</c>.</summary>
        public static readonly string[] RsLocalMain = ["Local branches 2", "  main (accented)", "  topic"];

        /// <summary>Local branches of RS2: the folder <c>pr</c> open (it holds HEAD's branch) with <c>1</c> accented, then <c>main</c>, <c>topic</c>.</summary>
        public static readonly string[] RsLocalPr1 = ["Local branches 3", "  pr (open)", "    1 (accented)", "  main", "  topic"];

        /// <summary>Local branches of RS3: <c>pr</c> closed (it no longer holds HEAD's branch), <c>main</c> accented.</summary>
        public static readonly string[] RsLocalPrClosed = ["Local branches 3", "  pr (closed)", "  main (accented)", "  topic"];

        /// <summary>Local branches of RS5: <c>pr</c> open with <c>7</c> accented.</summary>
        public static readonly string[] RsLocalPr7 = ["Local branches 3", "  pr (open)", "    7 (accented)", "  main", "  topic"];

        /// <summary>
        /// Remotes of RS0 as built: 5 remote branches; folders before leaves, and every folder that
        /// does not hold HEAD's branch closed (<c>up</c>, <c>feature</c>, both <c>pr</c>).
        /// </summary>
        public static readonly string[] RsRemotesClosed =
        [
            "Remotes 5",
            "  fork (open)",
            "    up (closed)",
            "  origin (open)",
            "    feature (closed)",
            "    pr (closed)",
            "    main",
            "  upstream (open)",
            "    pr (closed)",
        ];

        /// <summary>Remotes after part A's step 1: Remotes › fork › up and Remotes › origin › pr opened.</summary>
        public static readonly string[] RsRemotesUpAndPrOpen =
        [
            "Remotes 5",
            "  fork (open)",
            "    up (open)",
            "      main",
            "  origin (open)",
            "    feature (closed)",
            "    pr (open)",
            "      1",
            "    main",
            "  upstream (open)",
            "    pr (closed)",
        ];

        /// <summary>Remotes after part B's step 8: Remotes › fork › up opened.</summary>
        public static readonly string[] RsRemotesUpOpen =
        [
            "Remotes 5",
            "  fork (open)",
            "    up (open)",
            "      main",
            "  origin (open)",
            "    feature (closed)",
            "    pr (closed)",
            "    main",
            "  upstream (open)",
            "    pr (closed)",
        ];

        /// <summary>Remotes of RS4: 4 remote branches; <c>fork</c> with no branch under it, its <c>up</c> folder gone.</summary>
        public static readonly string[] Rs4Remotes =
        [
            "Remotes 4",
            "  fork (open)",
            "  origin (open)",
            "    feature (closed)",
            "    pr (closed)",
            "    main",
            "  upstream (open)",
            "    pr (closed)",
        ];

        /// <summary>Remotes of RS5: RS4's with Remotes › upstream › pr opened by step 11.</summary>
        public static readonly string[] Rs5Remotes =
        [
            "Remotes 4",
            "  fork (open)",
            "  origin (open)",
            "    feature (closed)",
            "    pr (closed)",
            "    main",
            "  upstream (open)",
            "    pr (open)",
            "      7",
        ];

        /// <summary>The sections after Remotes in every RS graph: none has a pull request, a tag or a stash.</summary>
        public static readonly string[] RsRest = ["Pull requests 0", "Tags 0", "Stashes 0"];

        /// <summary>A whole left panel of "refspecs": its local branches, its remotes, then <see cref="RsRest"/>.</summary>
        public static string[] RsPanel(string[] local, string[] remotes) => [.. local, .. remotes, .. RsRest];

        // ---- G25: the menus. ----

        /// <summary>The drop of <c>topic</c> onto <c>fork/up/main</c> (step 2): both pushes enabled (a renamed branch, 10.4 #5).</summary>
        public static readonly string[] DropOntoRenamedMenu =
        [
            "topic → fork/up/main",
            "Push topic to fork/up/main",
            "Push topic to fork/up/main and track it",
            "—",
            "Check out topic and rebase it onto fork/up/main (disabled: \"topic is up to date with fork/up/main\")",
            "Check out topic and rebase it onto fork/up/main interactively…",
            "—",
            "Compare topic with fork/up/main",
        ];

        /// <summary>The drop of <c>topic</c> onto <c>origin/pr/1</c> (step 3): both pushes disabled with R2.</summary>
        public static readonly string[] DropOntoAmbiguousMenu =
        [
            "topic → origin/pr/1",
            "Push topic to origin/pr/1 (disabled: \"" + R2OriginPr1 + "\")",
            "Push topic to origin/pr/1 and track it (disabled: \"" + R2OriginPr1 + "\")",
            "—",
            "Check out topic and rebase it onto origin/pr/1 (disabled: \"topic is up to date with origin/pr/1\")",
            "Check out topic and rebase it onto origin/pr/1 interactively…",
            "—",
            "Compare topic with origin/pr/1",
        ];

        /// <summary>A drop menu's height: five items and two separators (8 + 24 + 26 × 5 + 9 × 2).</summary>
        public const double DropMenuHeight = 180;

        /// <summary>R2 (10.5) for <c>origin/pr/1</c>.</summary>
        public const string R2OriginPr1 = "origin/pr/1 is fetched from more than one ref of origin";

        /// <summary>R4 (10.5) for <c>upstream/pr/7</c>.</summary>
        public const string R4UpstreamPr7 = "upstream/pr/7 is refs/pull/7/head on upstream, not a branch";

        /// <summary>A remote branch's menu on RS graphs with HEAD on <c>main</c> (<c>RepositoryMenus.ForRemoteBranch</c>), with the check-out and delete items given.</summary>
        public static string[] RemoteBranchMenu(string branch, string checkOut, string rebase, string delete) =>
        [
            branch,
            checkOut,
            $"Merge {branch} into main",
            $"Merge {branch} into main (create a merge commit)",
            rebase,
            $"Interactive rebase of main onto {branch}…",
            "—",
            CreateBranchHereItem,
            "Create tag here…",
            "—",
            delete,
            "—",
            $"Compare {branch} with main",
            $"Hide {branch}",
            $"Solo {branch}",
            "—",
            "Copy branch name",
        ];

        /// <summary>The menu of <c>origin/pr/1</c> (step 4): its checkout enabled (D201), its deletion disabled with R2.</summary>
        public static readonly string[] AmbiguousRefMenu = RemoteBranchMenu(
            "origin/pr/1",
            "Check out origin/pr/1",
            "Rebase main onto origin/pr/1",
            "Delete pr/1 on origin… (disabled: \"" + R2OriginPr1 + "\")");

        /// <summary>The menu of <c>fork/up/main</c> (step 8): its checkout disabled (its name on fork is HEAD's branch), its deletion by that name.</summary>
        public static readonly string[] RenamedRefMenu = RemoteBranchMenu(
            "fork/up/main",
            "Check out fork/up/main (disabled: \"main is already checked out\")",
            "Rebase main onto fork/up/main (disabled: \"main is up to date with fork/up/main\")",
            "Delete main on fork… (danger)");

        /// <summary>The menu of <c>upstream/pr/7</c> (step 11): its checkout enabled (R4 keeps it), its deletion disabled with R4.</summary>
        public static readonly string[] NotABranchMenu = RemoteBranchMenu(
            "upstream/pr/7",
            "Check out upstream/pr/7",
            "Rebase main onto upstream/pr/7",
            "Delete pr/7 on upstream… (disabled: \"" + R4UpstreamPr7 + "\")");

        /// <summary>A remote branch's menu's height: twelve items and four separators (8 + 24 + 26 × 12 + 9 × 4).</summary>
        public const double RemoteBranchMenuHeight = 380;

        // ---- G25: the operations and their texts. ----

        public const string PushTopicItem = "Push topic to fork/up/main";

        /// <summary>The drop's push onto a renamed remote branch: its title names the target (GD24).</summary>
        public const string PushTopicToRenamed = "Push topic to fork/up/main";

        public const string PushingTopicToRenamed = "Pushing topic to fork/up/main…";

        public const string PushedTopicToRenamed = "Pushed topic to fork/up/main";

        /// <summary>The drop's push names the branch's name on fork (10.4 #8), not phase 5's <c>up/main</c>.</summary>
        public const string PushTopicCommand = "git push --progress fork refs/heads/topic:refs/heads/main";

        /// <summary>TG4's key line.</summary>
        public const string PushTopicKey = "   8394a5e..de4a87c  topic -> main";

        public const string CheckOutOriginPr1Item = "Check out origin/pr/1";

        public const string CheckOutOriginPr1 = "Check out origin/pr/1";

        /// <summary>D201's checkout of an R2 ref: no upstream.</summary>
        public const string CreatePr1Command = "git switch --create pr/1 --no-track --no-overwrite-ignore refs/remotes/origin/pr/1";

        /// <summary>TG5's key line.</summary>
        public const string SwitchedToNewPr1 = "Switched to a new branch 'pr/1'";

        /// <summary><c>CreatedWithoutUpstream</c> (14.2, D201): longer than the status bar draws (GD14).</summary>
        public const string CreatedPr1WithoutUpstream = "Created pr/1 at origin/pr/1 without an upstream: origin/pr/1 is fetched from more than one ref of origin";

        /// <summary>The most the status bar draws of its text (phase 5's PD14).</summary>
        public const double StatusTextMaxWidth = 520;

        public const string CheckOutMain = "Check out main";

        public const string SwitchMainCommand = "git switch --no-guess --no-overwrite-ignore main";

        /// <summary>TG6's first key line.</summary>
        public const string SwitchedToMain = "Switched to branch 'main'";

        public const string CheckedOutMain = "Checked out main";

        /// <summary>The double-click's checkout of the local branch of today's name (10.4 #10, D201).</summary>
        public const string SwitchPr1Command = "git switch --no-guess --no-overwrite-ignore pr/1";

        /// <summary>TG6's second key line.</summary>
        public const string SwitchedToPr1 = "Switched to branch 'pr/1'";

        /// <summary>Phase 3's 2.2 result for a branch without an upstream (GD21).</summary>
        public const string CheckedOutPr1WithoutUpstream = "Checked out pr/1, which has no upstream";

        /// <summary>The checked-out branch's push with the dialog, keyed (6.7).</summary>
        public const string PushPr1Item = "Push pr/1…";

        /// <summary>The checked-out branch's Push key (6.7).</summary>
        public const string PushKey = "Ctrl+Shift+K";

        /// <summary>The push dialog's title and the push's operation title.</summary>
        public const string PushPr1 = "Push pr/1";

        /// <summary>The push dialog's remotes, in <c>git remote</c>'s order, <c>fork</c> checked.</summary>
        public static readonly (string Remote, bool Checked)[] PushDialogRemotes = [("fork", true), ("origin", false), ("upstream", false)];

        public const string PushFailedTitle = "Push failed";

        /// <summary><c>PushWouldStopFetch</c> (10.4's guard, 14.1).</summary>
        public const string PushWouldStopFetch = "Pushing pr/1 to origin/pr/1 would stop every fetch of origin: origin/pr/1 is fetched from more than one ref of origin.";

        public const string DeleteMainOnForkItem = "Delete main on fork…";

        public const string DeleteMainOnForkQuestionTitle = "Delete main on fork?";

        public const string DeleteMainOnForkQuestion = "Delete the branch main on fork? Everyone who uses fork loses it.";

        public const string BackupDetail = "A backup is saved first, so this can be undone.";

        public const string DeleteOnFork = "Delete on fork";

        public const string DeleteMainOnFork = "Delete main on fork";

        public const string DeletingMainOnFork = "Deleting main on fork…";

        public const string DeletedMainOnFork = "Deleted main on fork";

        /// <summary>The deletion's lease names the branch on fork and the tracking ref's commit (10.4 #9).</summary>
        public const string DeleteMainOnForkCommand = "git push --progress --force-with-lease=refs/heads/main:" + ParserSha + " fork --delete refs/heads/main";

        /// <summary>TG7's key line.</summary>
        public const string DeletedKey = " - [deleted]         main";

        /// <summary>The deletion's backup: the fixed clock's first name (<c>remote-branch-&lt;t&gt;/&lt;r&gt;/&lt;y&gt;</c>).</summary>
        public const string DeletionBackup = "refs/visualcommit/backup/remote-branch-20260102T090001000Z/fork/main";

        public const string CheckOutUpstreamPr7Item = "Check out upstream/pr/7";

        public const string CheckOutUpstreamPr7 = "Check out upstream/pr/7";

        /// <summary>R4's checkout tracks the ref on the remote, as git does.</summary>
        public const string CreatePr7Command = "git switch --create pr/7 --track --no-overwrite-ignore refs/remotes/upstream/pr/7";

        /// <summary>TG8's key line (standard error).</summary>
        public const string SwitchedToNewPr7 = "Switched to a new branch 'pr/7'";

        /// <summary>TG8's standard output on Git 2.36.</summary>
        public const string Pr7TrackingLine = "branch 'pr/7' set up to track 'upstream/refs/pull/7/head'.";

        public const string CreatedPr7Tracking = "Created pr/7 tracking upstream/pr/7 and checked it out";

        /// <summary>Pull's tooltip and help text for <c>pr/7</c>: its upstream named by its tracking ref (GD15).</summary>
        public const string PullFromUpstreamPr7 = "Pull from upstream/pr/7 (Ctrl+Shift+L)";

        /// <summary>What GD15 rules out: today's <c>UpstreamText</c> of <c>refs/pull/7/head</c>.</summary>
        public const string PullFromFullRef = "Pull from upstream/refs/pull/7/head (Ctrl+Shift+L)";

        /// <summary>Git's status lines of <c>pr/7</c> (<c>git status --porcelain=v2 --branch</c>, its header).</summary>
        public static readonly string[] Pr7StatusBranch = ["# branch.oid " + TopicSha, "# branch.head pr/7", "# branch.upstream upstream/pr/7", "# branch.ab +0 -0"];

        /// <summary>Git's status lines of <c>pr/1</c>: no upstream lines.</summary>
        public static readonly string[] Pr1StatusBranch = ["# branch.oid " + TopicSha, "# branch.head pr/1"];

        // ---- Helpers ----

        /// <summary>A commit row with its short SHA and date (null where the Date column does not show), its labels, and no lane ending in its node unless <c>Ends</c> is set (null: not stated).</summary>
        public static CommitRow Commit(string message, int lane, int colour, string shortSha, string? date, params string[] labels) =>
            new(message, lane, colour, labels, ShortSha: shortSha, Date: date);

        /// <summary>A stash's row: a ring, labelled <c>stash@{0}</c>.</summary>
        public static CommitRow Stash(string message, int lane, int colour, string shortSha, string? date) =>
            new(message, lane, colour, ["stash@{0}"], ShortSha: shortSha, Stash: true, Date: date);

        /// <summary>A menu's items and separators, as the report writes it (the header first, "—" for a separator).</summary>
        public static (int Items, int Separators) MenuCounts(IReadOnlyList<string> menu)
        {
            ArgumentNullException.ThrowIfNull(menu);
            var separators = menu.Skip(1).Count(line => line == "—");
            return (menu.Count - 1 - separators, separators);
        }

        /// <summary>A git output as the checks compare it: line ends as line feeds, without the last line feed.</summary>
        public static string Normalised(string output)
        {
            ArgumentNullException.ThrowIfNull(output);
            return output.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        }
    }

    /// <summary>
    /// One of the app's own reads as the report's TR tables write it: the command as the call log
    /// renders it, its exit code and its output (NUL-separated as git prints it, written without the
    /// last line feed; <see cref="PartG.Normalised"/>), the same on every git (git's data, part G's
    /// "Git's own texts").
    /// </summary>
    /// <param name="Command">The command, as <c>GitCallRecord.CommandText</c> renders it.</param>
    /// <param name="ExitCode">Its exit code.</param>
    /// <param name="Output">Its standard output.</param>
    public sealed record ExpectedRead(string Command, int ExitCode, string Output);
}
