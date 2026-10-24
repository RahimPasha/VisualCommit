// Part P's expected results (docs/test-reports/phase-5.md, "Part P: protected changes and the
// automatic stash (C12)" and the rows P24 to P27 of "Checks"), copied from the report. Only part
// P's writer edits this file; its members live in Phase5Expectations.PartP. Compiled by the
// real-window pass too: see Phase5Expectations.Common.cs for what may be used here (no type of
// the walk-through's own files, so the error dialogs are given as their parts).
using Avalonia;
using VisualCommit.Core.Graph;

namespace VisualCommit.VisualTests.Phase5;

public static partial class Phase5Expectations
{
    /// <summary>What part P's checks (P24 to P27) compare against.</summary>
    public static class PartP
    {
        // ---- The "protect" scenario (Scenarios.ProtectAsync), as Phase5ScenarioTests pins it. ----

        /// <summary>The tab's and the repository's name of the "protect" scenario.</summary>
        public const string Repo = "protect";

        public const string MainWorkSha = "41f8398f95fa73a5614e32760cc511673aa6a80d";

        public const string OtherFilesSha = "19a90447e124bf3a74124a5673df655601c53d5b";

        public const string OtherEditsSha = "4f5e8215c9321acae3a2ea386bda05107915f2da";

        public const string DescribeSha = "f39ea2d9addec45f926151cd2267cb57ea0b16aa";

        public const string StopTrackingSha = "64166f7723b59f263065d3ff2ee199263469846e";

        public const string AddGuideSha = "73e0babca7d61aaa9088f167ddbf0951f5f13744";

        public const string InitialCommitSha = "035a2f5b2cca78bd25cc95f5af43b0daf9a28a0c";

        /// <summary>The annotated tag <c>v1.1</c>'s object ("Version 1.1", on Describe the app).</summary>
        public const string TagObjectSha = "a2f81f7936a088a58e61025fc7fd9adfe842b307";

        /// <summary>P0's commit rows, as <c>SelectedIndex</c> counts them (without the working-changes row).</summary>
        public const int MainWorkRow = 0;

        public const int OtherFilesRow = 1;

        public const int OtherEditsRow = 2;

        public const int DescribeRow = 3;

        public const int StopTrackingRow = 4;

        public const int AddGuideRow = 5;

        public const int InitialCommitRow = 6;

        /// <summary>P1's rows: the stash on top, every commit one lower.</summary>
        public const int StashRow = 0;

        /// <summary>Describe the app in P1 and P1m (P0's row 3, one lower under the stash).</summary>
        public const int DescribeRowUnderStash = 4;

        /// <summary>Main work in P1, P1m and P0s.</summary>
        public const int MainWorkRowUnderStash = 1;

        /// <summary>Every graph of part P has two lanes (Graph column 48, the Message column at x 438).</summary>
        public const int Lanes = 2;

        /// <summary>The x a row is clicked at "in its Message column": 30 into the Message column at x 438.</summary>
        public const double RowPointX = 468;

        /// <summary>
        /// Where commit row <paramref name="row"/> is clicked, as the conventions' Rows give it: its
        /// centre y 129 + 26 r without the working-changes row, y 155 + 26 r while it shows
        /// (<paramref name="belowWorkingRow"/>); for example (468, 181), (468, 259) and (468, 285).
        /// </summary>
        public static Point RowPoint(int row, bool belowWorkingRow) => new(RowPointX, (belowWorkingRow ? 155 : 129) + (26 * row));

        /// <summary>Where the working-changes row (display row 0) is clicked: (468, 129).</summary>
        public static readonly Point WorkingRowPoint = new(RowPointX, 129);

        /// <summary>The files of "protect" on <c>main</c> and of its ignored settings (Scenarios.ProtectFiles).</summary>
        public const string ReadmeMainWork = "# Protect\n\nAn app to protect.\n\nMain work.\n";

        public const string SettingsLocal = "{\n  \"theme\": \"dark\"\n}\n";

        // ---- The changes the checks make (PD1). ----

        /// <summary>P24, P26 part A and P27 part A: <c>src/app.txt</c> changed, not staged.</summary>
        public const string AppLocal = "app\nlocal\n";

        /// <summary>P24 step 4: <c>src/lib.txt</c> changed outside the app.</summary>
        public const string LibLocal = "lib\nlocal\n";

        /// <summary>P24 step 4: <c>docs/guide.txt</c> changed outside the app.</summary>
        public const string GuideLocal = "guide\nlocal\n";

        /// <summary>P24 part B: the untracked file <c>notes</c>.</summary>
        public const string MyNotes = "my own notes\n";

        /// <summary>P24 step 7: the untracked <c>notes/today.txt</c>, where the file <c>notes</c> was.</summary>
        public const string Today = "today\n";

        /// <summary>P25 and P26 part B: <c>src/lib.txt</c> staged with this…</summary>
        public const string LibStaged = "lib\nstaged\n";

        /// <summary>…then changed further in the working tree.</summary>
        public const string LibStagedAndMore = "lib\nstaged\nand more\n";

        /// <summary>P25 and P26 part B: <c>src/app.txt</c> changed, not staged.</summary>
        public const string AppUnstaged = "app\nunstaged\n";

        /// <summary>P25 and P26 part B: the untracked <c>scratch.txt</c>.</summary>
        public const string Scratch = "scratch\n";

        /// <summary>P27 step 4: <c>README.md</c> changed, not staged.</summary>
        public const string ReadmeLocal = "# Protect\n\nLocal.\n";

        /// <summary>P27 step 5: <c>README.md</c> staged.</summary>
        public const string ReadmeStaged = "# Protect\n\nStaged.\n";

        /// <summary>P27 part B: <c>src/app.txt</c> as the stash "App experiment" holds it.</summary>
        public const string AppStashed = "app\nstashed\n";

        /// <summary>P27 part B: the untracked <c>todo.txt</c> the stash "App experiment" holds.</summary>
        public const string TodoStashed = "todo\n";

        /// <summary>P27 step 10: <c>todo.txt</c> written outside the app.</summary>
        public const string TodoMine = "mine\n";

        /// <summary>The new branch's name in P24 step 3.</summary>
        public const string NewBranch = "topic";

        // ---- The stashes (P1, 5.3, 5.4, P0s). ----

        /// <summary>The automatic stash of P25: "On main: Auto stash before checking out f39ea2d".</summary>
        public const string AutoStashSha = "256077fbd990adad7fe00df31789590d7370aff0";

        public const string AutoStashIndexSha = "f632741c7c7e1ac98bda443a390dc123e8fda26c";

        public const string AutoStashUntrackedSha = "9d22397dd9081ff84ccfc699db779eafbfaedb8c";

        /// <summary>The automatic stash before the refused checkout of Add the guide (P26 step 6), popped back.</summary>
        public const string GuideStashSha = "f0c95a478241bba9511b657bc7279278402915f1";

        /// <summary>P27 part B's stash "On main: App experiment" (made before the app starts, at 12:07).</summary>
        public const string ExperimentStashSha = "cc3e07a6f75cacfae5f8d8edaec55844a4f2c428";

        public const string ExperimentIndexSha = "9c08a4212288dc9f1418cbe06ecf0e9fb98c6295";

        public const string ExperimentUntrackedSha = "a058536929790a82d5316990cb3664692068bc9a";

        /// <summary>The automatic stash's message for a target named <paramref name="name"/> (9.2): "Auto stash before checking out f39ea2d".</summary>
        public static string AutoStashMessage(string name) => $"Auto stash before checking out {name}";

        /// <summary>How <c>git stash list</c> and the graph show the automatic stash of P25.</summary>
        public const string AutoStashListed = "On main: Auto stash before checking out f39ea2d";

        /// <summary>How <c>git stash list</c> and the graph show P27 part B's stash.</summary>
        public const string ExperimentListed = "On main: App experiment";

        // ---- The graphs (P0, P0w, P0h, P1, P1m, P0s). ----

        /// <summary>P0, "protect" as built: 2 lanes, no working-changes row.</summary>
        public static readonly CommitRow[] P0 =
        [
            Commit("Main work", 0, 0, "41f8398", "2026-01-01 12:06", Current("main")),
            Commit("Other files", 1, 1, "19a9044", "2026-01-01 12:05", "other"),
            Commit("Other edits", 1, 1, "4f5e821", "2026-01-01 12:04"),
            Commit("Describe the app", 0, 0, "f39ea2d", "2026-01-01 12:03", "v1.1"),
            Commit("Stop tracking the local settings", 0, 0, "64166f7", "2026-01-01 12:02") with { Ends = [1] },
            Commit("Add the guide", 0, 0, "73e0bab", "2026-01-01 12:01"),
            Commit("Initial commit", 0, 0, "035a2f5", "2026-01-01 12:00"),
        ];

        /// <summary>The working-changes row of P0w, P0h and P0s: its ring in lane 0, colour 0 (Main work's).</summary>
        public static readonly WorkingChangesRow Working = new("Working changes", 0, 0);

        /// <summary>P0h, P26 part A after step 3: P0w with HEAD detached at Main work (row 0's labels <c>HEAD</c> and <c>main</c>).</summary>
        public static readonly CommitRow[] P0h =
        [
            P0[0] with { Labels = ["HEAD", "main"] },
            .. P0.Skip(1),
        ];

        /// <summary>P1, after the automatic stash and the checkout of Describe the app (P25 step 2): 2 lanes.</summary>
        public static readonly CommitRow[] P1 =
        [
            Stash(AutoStashListed, "256077f", "2026-01-02 09:00"),
            Commit("Main work", 0, 0, "41f8398", "2026-01-01 12:06", "main"),
            Commit("Other files", 1, 1, "19a9044", "2026-01-01 12:05", "other"),
            Commit("Other edits", 1, 1, "4f5e821", "2026-01-01 12:04"),
            Commit("Describe the app", 0, 0, "f39ea2d", "2026-01-01 12:03", "HEAD", "v1.1"),
            Commit("Stop tracking the local settings", 0, 0, "64166f7", "2026-01-01 12:02") with { Ends = [1] },
            Commit("Add the guide", 0, 0, "73e0bab", "2026-01-01 12:01"),
            Commit("Initial commit", 0, 0, "035a2f5", "2026-01-01 12:00"),
        ];

        /// <summary>P1m, P1 with <c>main</c> checked out again (P25 step 3): the stash still drawn.</summary>
        public static readonly CommitRow[] P1m =
        [
            P1[0],
            P1[1] with { Labels = [Current("main")] },
            P1[2],
            P1[3],
            P1[4] with { Labels = ["v1.1"] },
            .. P1.Skip(5),
        ];

        /// <summary>P0s, "protect" with the stash "On main: App experiment" (P27 part B), below the working-changes row.</summary>
        public static readonly CommitRow[] P0s =
        [
            Stash(ExperimentListed, "cc3e07a", "2026-01-01 12:07"),
            .. P1m.Skip(1),
        ];

        /// <summary>
        /// The lines a stash's graph draws dashed (D161), as (commit row, kind, lane): the stash's
        /// line out of its ring in row 0 and into its base's node in row 1, lane 0 (P1, P1m, P0s).
        /// </summary>
        public static readonly (int Row, GraphLineKind Kind, int Lane)[] StashOnTopDashes =
        [
            (0, GraphLineKind.OutOfNode, 0),
            (1, GraphLineKind.IntoNode, 0),
        ];

        /// <summary>P24 part B: the label <c>other</c> of commit row 1 below the working-changes row, x 266 to 306.6, y 172 to 190.</summary>
        public static readonly (double Left, double Top, double Right, double Bottom) OtherLabelBelowWorking = (266, 172, 306.6, 190);

        /// <summary>A label as the graph draws it at 1100×700: its drawn text and where it starts and ends (the report's widths, from x 266, 4 apart).</summary>
        public sealed record DrawnLabel(string Text, double Left, double Right);

        /// <summary>P0's (and P0w's) drawn labels by commit row: <c>main</c> ✓ 266 to 316.8, <c>other</c> 266 to 306.6, <c>v1.1</c> 266 to 309.7.</summary>
        public static readonly (int Row, DrawnLabel[] Labels)[] P0Labels =
        [
            (MainWorkRow, [new("main", 266, 316.8)]),
            (OtherFilesRow, [new("other", 266, 306.6)]),
            (DescribeRow, [new("v1.1", 266, 309.7)]),
        ];

        /// <summary>P0h's row 0: <c>HEAD</c> 266 to 308.8 and <c>main</c> 312.8 to 350.6, both drawn.</summary>
        public static readonly (int Row, DrawnLabel[] Labels)[] P0hLabels =
        [
            (MainWorkRow, [new("HEAD", 266, 308.8), new("main", 312.8, 350.6)]),
        ];

        /// <summary>P1's drawn labels: <c>stash@{0}</c> 266 to 334.3, <c>main</c> (no check mark) 266 to 303.8, <c>HEAD</c> 266 to 308.8 and <c>v1.1</c> 312.8 to 356.5.</summary>
        public static readonly (int Row, DrawnLabel[] Labels)[] P1Labels =
        [
            (StashRow, [new("stash@{0}", 266, 334.3)]),
            (MainWorkRowUnderStash, [new("main", 266, 303.8)]),
            (DescribeRowUnderStash, [new("HEAD", 266, 308.8), new("v1.1", 312.8, 356.5)]),
        ];

        /// <summary>P1m's row 1: <c>main</c> ✓ 266 to 316.8.</summary>
        public static readonly (int Row, DrawnLabel[] Labels)[] P1mLabels =
        [
            (MainWorkRowUnderStash, [new("main", 266, 316.8)]),
        ];

        // ---- The left panel (P0, P1, P1m, P0s). ----

        /// <summary>
        /// The left panel's sections as part P writes them, top to bottom, with their counts: Local
        /// branches 2, Remotes 0, Pull requests 0, Tags 1, Stashes <paramref name="stashes"/>.
        /// </summary>
        public static (string Title, int Count)[] Sections(int stashes) =>
            [("Local branches", 2), ("Remotes", 0), ("Pull requests", 0), ("Tags", 1), ("Stashes", stashes)];

        /// <summary>The local branches, in order: <c>main</c>, then <c>other</c>.</summary>
        public static readonly string[] LocalBranches = ["main", "other"];

        /// <summary>The tags: <c>v1.1</c>.</summary>
        public static readonly string[] Tags = ["v1.1"];

        // ---- The checks' operations (phase 3's 2.33 titles, 9.2, 12.1, 12.2). ----

        public const string CheckOutOther = "Check out other";

        public const string CreateBranchTopic = "Create branch topic";

        public const string CheckOutMain = "Check out main";

        /// <summary>Check out {short}: Describe the app, Other edits, Main work, Add the guide.</summary>
        public static string CheckOut(string sha) => "Check out " + sha[..7];

        public const string MergeOther = "Merge other";

        public static readonly string CherryPickOtherEdits = "Cherry-pick " + OtherEditsSha[..7];

        public static readonly string RevertDescribe = "Revert " + DescribeSha[..7];

        public const string PopStash = "Pop stash@{0}";

        public const string ApplyStash = "Apply stash@{0}";

        /// <summary>A failed operation's status text, in the danger colour: "Check out other failed".</summary>
        public static string Failed(string title) => title + " failed";

        // ---- The menu items the checks click. ----

        public const string CheckOutThisCommitItem = "Check out this commit";

        public const string CreateBranchHereItem = "Create branch here…";

        public const string CheckOutOtherItem = "Check out other";

        public const string MergeOtherItem = "Merge other into main";

        public const string CherryPickItem = "Cherry-pick onto main";

        public const string RevertItem = "Revert this commit";

        public const string ApplyStashItem = "Apply stash";

        // ---- The dialogs and the status bar (9.2, 9.3, phase 3's texts). ----

        /// <summary>9.3's title of every refused checkout (PD5).</summary>
        public const string CheckoutBlockedTitle = "Checkout blocked";

        public const string MergeFailedTitle = "Merge failed";

        public const string CherryPickFailedTitle = "Cherry-pick failed";

        public const string RevertFailedTitle = "Revert failed";

        public const string ApplyingStashFailedTitle = "Applying the stash failed";

        public const string OneConflictPreventsCheckout = "1 conflict prevents checkout. Commit, stash, or discard your changes first.";

        public const string ThreeConflictsPreventCheckout = "3 conflicts prevent checkout. Commit, stash, or discard your changes first.";

        /// <summary>9.3's ignored-files text for Add the guide (PD9).</summary>
        public const string IgnoredFileInTheWay = "1 ignored file is in the way: 73e0bab tracks a file of the same name. Move it away first.";

        public const string OneConflictPreventsMerge = "1 conflict prevents the merge. Commit, stash, or discard your changes first.";

        public const string OneConflictPreventsCherryPick = "1 conflict prevents the cherry-pick. Commit, stash, or discard your changes first.";

        public const string OneConflictPreventsRevert = "1 conflict prevents the revert. Commit, stash, or discard your changes first.";

        public const string StagedPreventMerge = "Your staged changes prevent the merge. Commit, stash, or unstage them first.";

        public const string StagedPreventCherryPick = "Your staged changes prevent the cherry-pick. Commit, stash, or unstage them first.";

        public const string StagedPreventRevert = "Your staged changes prevent the revert. Commit, stash, or unstage them first.";

        public const string OneConflictPreventsApplyingStash = "1 conflict prevents applying the stash. Commit, stash, or discard your changes first.";

        public const string TwoConflictsPreventApplyingStash = "2 conflicts prevent applying the stash. Commit, stash, or discard your changes first.";

        /// <summary>The error dialog's button that stashes and checks out (D80, D91).</summary>
        public const string StashAndCheckOutButton = "Stash changes and check out";

        /// <summary>A dialog with git's output and the stash button: "Copy output", "Stash changes and check out", "Close".</summary>
        public static readonly string[] ButtonsWithStash = ["Copy output", StashAndCheckOutButton, "Close"];

        /// <summary>A dialog with git's output and no extra action.</summary>
        public static readonly string[] ButtonsWithOutput = ["Copy output", "Close"];

        /// <summary>A refusal the app makes before git: "Close" only (D114).</summary>
        public static readonly string[] CloseOnly = ["Close"];

        /// <summary>The status bar's first running text of the automatic stash's operation (9.2).</summary>
        public const string StashingYourChanges = "Stashing your changes…";

        /// <summary>The checkout's own running text: "Checking out f39ea2d…".</summary>
        public static string CheckingOut(string name) => $"Checking out {name}…";

        /// <summary>9.2 step 6: the status bar and its tooltip after the automatic stash's checkout of Describe the app.</summary>
        public const string ChangesInAutoStash = "Your changes are in the stash \"Auto stash before checking out f39ea2d\".";

        /// <summary>The widest the status bar's operation text is laid out (<c>MaxWidth</c>, PD14).</summary>
        public const double StatusTextMaxWidth = 520;

        public const string CheckedOutMain = "Checked out main";

        public const string PoppedStash = "Popped stash@{0}";

        /// <summary>Phase 3's status after a detached checkout without a stash: "HEAD is now detached at 41f8398".</summary>
        public static string DetachedAtStatus(string sha) => "HEAD is now detached at " + sha[..7];

        /// <summary><c>CurrentBranch</c> with HEAD detached: "Detached at f39ea2d".</summary>
        public static string DetachedAt(string sha) => "Detached at " + sha[..7];

        /// <summary>The details panel with nothing selected (phase 3's placeholder).</summary>
        public const string NothingSelectedPlaceholder = "Select a commit to see its details.";

        /// <summary>The name dialog of "Create branch here…" on Other files (P24 step 3).</summary>
        public const string CreateBranchTitle = "Create branch";

        public const string OtherFilesContext = "From 19a9044 \"Other files\"";

        public const string CheckOutNewBranchOption = "Check out the new branch";

        // ---- The toolbar (1.1, 7.4 to 7.6; phase 3's texts). ----

        /// <summary>The undo history's entry of a checkout: "checkout of f39ea2d", "checkout of main".</summary>
        public static string CheckoutEntry(string name) => "checkout of " + name;

        /// <summary>The entry of the toolbar's Pop of the automatic stash (P25 step 4).</summary>
        public const string PopAutoStashEntry = "pop of stash \"On main: Auto stash before checking out f39ea2d\"";

        public const string NoChangesToStash = "There are no changes to stash";

        public const string NoStashes = "There are no stashes";

        // ---- The stash's menu (P27 step 9; phase 3's, 4a's height). ----

        /// <summary>The menu of Stashes › On main: App experiment.</summary>
        public static readonly string[] ExperimentStashMenu =
        [
            "stash@{0} On main: App experiment",
            "Apply stash",
            "Pop stash",
            "Drop stash… (danger)",
            "—",
            "Copy SHA",
        ];

        /// <summary>4a's height of <see cref="ExperimentStashMenu"/>: 8 + 24 + 26 × 4 + 9 × 1.</summary>
        public const double StashMenuHeight = 145;

        // ---- The git commands (as the call log renders them). ----

        // Phase 6's Keep ignored files (D193, docs/phase-6-design.md 9.4, 9.11, 16.1): every checkout
        // and merge passes --no-overwrite-ignore just before its positional argument.

        public const string SwitchOtherCommand = "git switch --no-guess --no-overwrite-ignore other";

        public const string CreateTopicCommand = "git switch --create topic --no-overwrite-ignore " + OtherFilesSha;

        public const string SwitchMainCommand = "git switch --no-guess --no-overwrite-ignore main";

        /// <summary>A detached checkout as phase 3 runs it (9.1's second row), with phase 6's option (D193): <c>git switch --detach --no-overwrite-ignore &lt;sha&gt;</c>.</summary>
        public static string DetachCommand(string sha) => "git switch --detach --no-overwrite-ignore " + sha;

        /// <summary>The automatic stash's checkout (9.1's first row, D179): <c>git switch --detach --no-overwrite-ignore &lt;sha&gt;</c>.</summary>
        public static string DetachNoOverwriteIgnoreCommand(string sha) => "git switch --detach --no-overwrite-ignore " + sha;

        /// <summary>The automatic stash (9.2): <c>git stash push --include-untracked --message "Auto stash before checking out f39ea2d"</c>.</summary>
        public static string AutoStashCommand(string name) => $"git stash push --include-untracked --message \"{AutoStashMessage(name)}\"";

        /// <summary>The toolbar's Pop (phase 3's plain pop, PD12).</summary>
        public const string PlainPopCommand = "git stash pop stash@{0}";

        /// <summary>The pop with the index: the automatic stash's pop-back (9.2 step 5) and its Undo (7.3).</summary>
        public const string PopIndexCommand = "git stash pop --index stash@{0}";

        /// <summary>The Undo's switch back (7.3, D166).</summary>
        public const string SwitchMainNoOverwriteIgnoreCommand = "git switch --no-guess --no-overwrite-ignore main";

        /// <summary>9.3's ignored-files classification (without <c>GIT_LITERAL_PATHSPECS</c>, D74).</summary>
        public const string CheckIgnoreCommand = "git check-ignore --stdin -z";

        public const string MergeOtherCommand = "git merge --no-edit --no-log -m \"Merge branch 'other'\" --no-overwrite-ignore refs/heads/other";

        public const string CherryPickOtherEditsCommand = "git cherry-pick " + OtherEditsSha;

        public const string RevertDescribeCommand = "git revert --no-edit " + DescribeSha;

        // ---- Git's texts (Git 2.36.0.windows.1; "⇥" is a tab, as ErrorOutput shows it). ----

        /// <summary>The key line of git's refusal for tracked changes in a checkout's way (TP1, TP1d, TP2, TP3).</summary>
        public const string CheckoutChangesKey = "error: Your local changes to the following files would be overwritten by checkout:";

        /// <summary>The key line of git's refusal for untracked files in a checkout's way (TP4, TP6).</summary>
        public const string CheckoutUntrackedKey = "error: The following untracked working tree files would be overwritten by checkout:";

        /// <summary>The key line of git's refusal for a folder in a checkout's way (TP5).</summary>
        public const string CheckoutFolderKey = "error: Updating the following directories would lose untracked files in them:";

        /// <summary>The key line of git's refusal for changes in a merge's way (TP7, TP8, TP12).</summary>
        public const string MergeChangesKey = "error: Your local changes to the following files would be overwritten by merge:";

        /// <summary>TP1: <c>git switch --no-guess other</c> over the changed <c>src/app.txt</c> (TP2 and TP1d print the same).</summary>
        public static readonly string[] Tp1 =
        [
            CheckoutChangesKey,
            "\tsrc/app.txt",
            "Please commit your changes or stash them before you switch branches.",
            "Aborting",
        ];

        /// <summary>TP3: the same with <c>src/app.txt</c>, <c>src/lib.txt</c> and <c>docs/guide.txt</c> changed.</summary>
        public static readonly string[] Tp3 =
        [
            CheckoutChangesKey,
            "\tdocs/guide.txt",
            "\tsrc/app.txt",
            "\tsrc/lib.txt",
            "Please commit your changes or stash them before you switch branches.",
            "Aborting",
        ];

        /// <summary>TP4: <c>git switch --no-guess other</c> over the untracked <c>notes</c>.</summary>
        public static readonly string[] Tp4 =
        [
            CheckoutUntrackedKey,
            "\tnotes",
            "Please move or remove them before you switch branches.",
            "Aborting",
        ];

        /// <summary>TP5: the same over the untracked <c>notes/today.txt</c> (git's empty line before "Aborting" dropped by the cleaning).</summary>
        public static readonly string[] Tp5 =
        [
            CheckoutFolderKey,
            "\tnotes",
            "Aborting",
        ];

        /// <summary>TP6: <c>git switch --detach --no-overwrite-ignore 73e0bab…</c> over the ignored <c>settings.local.json</c>.</summary>
        public static readonly string[] Tp6 =
        [
            CheckoutUntrackedKey,
            "\tsettings.local.json",
            "Please move or remove them before you switch branches.",
            "Aborting",
        ];

        /// <summary>TP7: the merge of <c>other</c> over the changed <c>src/app.txt</c> (exit 128; 2.55: exit 2 and a fifth line).</summary>
        public static readonly string[] Tp7 =
        [
            MergeChangesKey,
            "\tsrc/app.txt",
            "Please commit your changes or stash them before you merge.",
            "Aborting",
        ];

        /// <summary>TP8: the cherry-pick of Other edits over the changed <c>src/app.txt</c> (exit 128).</summary>
        public static readonly string[] Tp8 = [.. Tp7, "fatal: cherry-pick failed"];

        /// <summary>TP9: the merge with <c>README.md</c> staged (exit 128; no "error:", the names indented by two spaces).</summary>
        public static readonly string[] Tp9 =
        [
            "Your local changes to the following files would be overwritten by merge:",
            "  README.md",
        ];

        /// <summary>TP9's key line, printed by both gits (PD10).</summary>
        public const string Tp9Key = "  README.md";

        /// <summary>TP10: the cherry-pick with <c>README.md</c> staged (exit 128).</summary>
        public static readonly string[] Tp10 =
        [
            "error: your local changes would be overwritten by cherry-pick.",
            "hint: commit your changes or stash them to proceed.",
            "fatal: cherry-pick failed",
        ];

        public const string Tp10Key = "error: your local changes would be overwritten by cherry-pick.";

        /// <summary>TP11: the revert of Describe the app with <c>README.md</c> staged (exit 128).</summary>
        public static readonly string[] Tp11 =
        [
            "error: your local changes would be overwritten by revert.",
            "hint: commit your changes or stash them to proceed.",
            "fatal: revert failed",
        ];

        public const string Tp11Key = "error: your local changes would be overwritten by revert.";

        /// <summary>TP12: the revert of Describe the app with <c>README.md</c> and <c>src/app.txt</c> changed, not staged (exit 128).</summary>
        public static readonly string[] Tp12 =
        [
            MergeChangesKey,
            "\tREADME.md",
            "Please commit your changes or stash them before you merge.",
            "Aborting",
            "fatal: revert failed",
        ];

        /// <summary>The automatic stash's push, on standard output: "Saved working directory and index state On main: Auto stash before checking out f39ea2d".</summary>
        public static string SavedStash(string name) => "Saved working directory and index state On main: " + AutoStashMessage(name);

        /// <summary>A successful detached checkout, on standard error: "HEAD is now at f39ea2d Describe the app".</summary>
        public const string HeadNowAtDescribe = "HEAD is now at f39ea2d Describe the app";

        /// <summary>P26 step 3, on standard error.</summary>
        public const string HeadNowAtMainWork = "HEAD is now at 41f8398 Main work";

        /// <summary>P26 step 3, on standard output: the carried change.</summary>
        public const string CarriedAppChange = "M\tsrc/app.txt";

        /// <summary><c>git switch --no-guess main</c> from the detached Describe the app (P25 step 3).</summary>
        public static readonly string[] SwitchedBackToMain = ["Previous HEAD position was f39ea2d Describe the app", "Switched to branch 'main'"];

        /// <summary>The last line of a pop of the stash <paramref name="sha"/>: "Dropped stash@{0} (&lt;sha&gt;)".</summary>
        public static string Dropped(string sha) => $"Dropped stash@{{0}} ({sha})";

        // ---- Git's state (status, reflog, backups). ----

        /// <summary><c>git status --porcelain=v1 --untracked-files=all --ignored</c> of "protect" as built: the ignored settings only.</summary>
        public static readonly string[] CleanStatus = ["!! settings.local.json"];

        /// <summary>The same with P25's changes (step 1): staged, unstaged and untracked, and the ignored settings.</summary>
        public static readonly string[] P25Status = [" M src/app.txt", "MM src/lib.txt", "?? scratch.txt", "!! settings.local.json"];

        /// <summary>The same after the toolbar's plain pop (P25 step 4): <c>src/lib.txt</c>'s staged version back unstaged.</summary>
        public static readonly string[] PlainPoppedStatus = [" M src/app.txt", " M src/lib.txt", "?? scratch.txt", "!! settings.local.json"];

        /// <summary>HEAD's reflog's newest entry as built.</summary>
        public const string MainWorkReflog = "commit: Main work";

        /// <summary>A stash's push or a pop with <c>--index</c>: what it writes into HEAD's reflog.</summary>
        public const string ResetToHeadReflog = "reset: moving to HEAD";

        public const string MainToDescribeReflog = "checkout: moving from main to " + DescribeSha;

        public const string DescribeToMainReflog = "checkout: moving from " + DescribeSha + " to main";

        public const string MainToMainWorkReflog = "checkout: moving from main to " + MainWorkSha;

        /// <summary>
        /// What a merge of <c>other</c> refused for something staged writes into HEAD's reflog on
        /// gits later than 2.36 (P27 step 6; design 15.9): a no-op entry, its old and new value
        /// both HEAD's commit. Git 2.36 writes none, and no git writes one for a merge refused
        /// for an unstaged change (step 2).
        /// </summary>
        public const string RefusedMergeReflog = "merge refs/heads/other: updating HEAD";

        /// <summary>The backup the toolbar's Pop saves (D171; the fixed backup clock's first name).</summary>
        public const string PopBackup = "refs/visualcommit/backup/stash-20260102T090001000Z";

        // ---- The stage panel (P25). ----

        /// <summary>The stage panel's unstaged list with P25's changes, as "letter name folder".</summary>
        public static readonly string[] P25Unstaged = ["A scratch.txt", "M app.txt src", "M lib.txt src"];

        /// <summary>The staged list with P25's changes.</summary>
        public static readonly string[] P25Staged = ["M lib.txt src"];

        /// <summary>A commit row as the report's tables write it, with no lanes ending in its node unless <c>Ends</c> says so.</summary>
        public static CommitRow Commit(string message, int lane, int colour, string shortSha, string date, params string[] labels) =>
            new(message, lane, colour, labels, ShortSha: shortSha, Date: date, Ends: []);

        /// <summary>A stash's row at the top: lane 0, colour 0, a ring, labelled <c>stash@{0}</c>.</summary>
        public static CommitRow Stash(string message, string shortSha, string date) =>
            new(message, 0, 0, ["stash@{0}"], ShortSha: shortSha, Stash: true, Date: date, Ends: []);

        /// <summary>A menu's items and separators, as the report writes it (the header first, "—" for a separator).</summary>
        public static (int Items, int Separators) MenuCounts(IReadOnlyList<string> menu)
        {
            ArgumentNullException.ThrowIfNull(menu);
            var separators = menu.Skip(1).Count(line => line == "—");
            return (menu.Count - 1 - separators, separators);
        }
    }
}
