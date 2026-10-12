// Part C's expected results (docs/test-reports/phase-3.md, part C: the scenarios as these checks
// use them, git's texts T1 to T7, the menus, dialogs and status texts), copied from the report.
// Only part C's writer edits this file; its members live in Phase3Expectations.PartC. Compiled by
// the real-window pass too: see Phase3Expectations.Common.cs for what may be used here.
namespace VisualCommit.VisualTests.Phase3;

public static partial class Phase3Expectations
{
    /// <summary>What part C's checks (13, 13b, 14, 14b, 15, 15b, 16, 25) compare against.</summary>
    public static class PartC
    {
        /// <summary>The label notation of <see cref="Row.Labels"/>: a branch with the check mark (HEAD is on it).</summary>
        public const string Check = " ✓";

        /// <summary>
        /// One commit row of a graph as the report's tables write it: its message, its node's lane
        /// and colour number, its labels in phase 1's order (a name ending in <see cref="Check"/> is
        /// the branch HEAD is on; "HEAD" is the filled label of a detached HEAD), whether its node
        /// is a stash's ring, its Date column (null: not stated) and the lanes that end in its node
        /// besides its own ("lanes 1 and 2 end here").
        /// </summary>
        public sealed record Row(string Message, int Lane, int Color, IReadOnlyList<string> Labels, bool Stash = false, string? Date = null, IReadOnlyList<int>? Ends = null)
        {
            /// <summary>The lanes that end in this row's node, other than its own: none unless the report names them.</summary>
            public IReadOnlyList<int> EndingLanes => Ends ?? [];
        }

        /// <summary>A commit row (<see cref="Row"/>).</summary>
        public static Row R(string message, int lane, int color, params string[] labels) => new(message, lane, color, labels);

        /// <summary>A stash row: its node is a ring.</summary>
        public static Row S(string message, int lane, int color, string selector = "stash@{0}") => new(message, lane, color, [selector], Stash: true);

        /// <summary>The working-changes row as a graph shows it: its text, and its ring's lane and colour.</summary>
        public sealed record WorkingRow(string Text, int Lane, int Color);

        public const string WorkingChanges = "Working changes";

        public const string WorkingChangesOneConflict = "Working changes · 1 conflict";

        public static readonly WorkingRow Working = new(WorkingChanges, 0, 0);

        public static readonly WorkingRow WorkingConflict = new(WorkingChangesOneConflict, 0, 0);

        // ---- The ops scenario (G0) and the graphs the checks make from it. ----

        /// <summary>Copies a graph with one row replaced.</summary>
        public static IReadOnlyList<Row> With(IReadOnlyList<Row> rows, int index, Row row)
        {
            ArgumentNullException.ThrowIfNull(rows);
            var copy = rows.ToList();
            copy[index] = row;
            return copy;
        }

        /// <summary>Copies a graph with one row's labels replaced.</summary>
        public static IReadOnlyList<Row> WithLabels(IReadOnlyList<Row> rows, int index, params string[] labels)
        {
            ArgumentNullException.ThrowIfNull(rows);
            return With(rows, index, rows[index] with { Labels = labels });
        }

        /// <summary>G0, the ops scenario's graph (three lanes).</summary>
        public static readonly IReadOnlyList<Row> G0 =
        [
            S("On main: Experiment", 0, 0) with { Date = "2026-01-01 12:07" },
            R("Conflicting change", 1, 1, "feature/conflict") with { Date = "2026-01-01 12:06" },
            R("Fast-forward me", 2, 2, "feature/ff") with { Date = "2026-01-01 12:05" },
            R("Main work", 0, 0, "main" + Check, "origin/main", "v1.1") with { Date = "2026-01-01 12:04", Ends = [2] },
            R("Alpha two", 2, 3, "feature/alpha", "origin/feature/alpha") with { Date = "2026-01-01 12:03" },
            R("Alpha one", 2, 3) with { Date = "2026-01-01 12:02" },
            R("Add core", 0, 0, "v1.0") with { Date = "2026-01-01 12:01", Ends = [1, 2] },
            R("Initial commit", 0, 0) with { Date = "2026-01-01 12:00" },
        ];

        /// <summary>Full SHAs of the ops scenario's commits (pinned by ScenarioTests).</summary>
        public const string MainWorkSha = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

        public const string AlphaTwoSha = "ce5ea6ab16759bede02ce706dd5c6865443c2051";

        public const string FastForwardShort = "d93814d";

        public const string AddCoreShort = "8beca34";

        public const string AlphaOneShort = "dff8fbf";

        public const string ConflictingShort = "078ef05";

        public const string ExperimentStashSha = "0e95aca13f7975ef4a1fe029a8b5e9497f31028d";

        public const string V10TagObjectShort = "c8e9976";

        /// <summary>The picked copy of "Alpha one" that StoppedAsync(CherryPick) leaves on main.</summary>
        public const string PickedAlphaOneShort = "187627e";

        public const string BackupDetail = "A backup is saved first, so this can be undone.";

        // ---- Check 13 ----

        /// <summary>Check 13 step 1: HEAD's own commit, with phase 4's rebase items (phase 4's OC3; 284 high).</summary>
        public static readonly string[] Check13CommitMenu =
        [
            "88d6b91 Main work",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto main (disabled: \"This commit is already on main\")",
            "Revert this commit",
            "Rebase main onto this commit (disabled: \"This commit is already on main\")",
            "Interactive rebase of main from here… (disabled: \"main has no commits after this one\")",
            "Reset main to this commit…",
            "—",
            "Copy SHA",
        ];

        public const string Check13Context = "From 88d6b91 \"Main work\"";

        public const string FolderClash = "This conflicts with the branch feature/alpha.";

        /// <summary>Step 4's names and what <c>NameError</c> says for each (null: no message, the button enabled).</summary>
        public static IReadOnlyList<(string Name, string? Error)> Check13Names(bool caseInsensitive) =>
        [
            ("feature/alpha/x", FolderClash),
            ("main", "A branch named main exists."),
            ("Main", caseInsensitive ? "A branch named main exists in another case." : null),
            // The reason "it contains .." and the closing full stop.
            ("bad..name", "This is not a valid branch name: it contains ..."),
            ("-x", "This is not a valid branch name: it starts with -."),
            ("HEAD", "This is not a valid branch name: it cannot be HEAD."),
        ];

        public static readonly IReadOnlyList<Row> Check13Hotfix = WithLabels(G0, 3, "hotfix" + Check, "main", "origin/main", "v1.1");

        public static readonly IReadOnlyList<Row> Check13Renamed = WithLabels(Check13Hotfix, 1, "feature/conflicting");

        public static readonly IReadOnlyList<Row> Check13FfDeleted =
        [
            S("On main: Experiment", 0, 0),
            R("Conflicting change", 1, 1, "feature/conflicting"),
            R("Main work", 0, 0, "hotfix" + Check, "main", "origin/main", "v1.1"),
            R("Alpha two", 2, 2, "feature/alpha", "origin/feature/alpha"),
            R("Alpha one", 2, 2),
            R("Add core", 0, 0, "v1.0") with { Ends = [1, 2] },
            R("Initial commit", 0, 0),
        ];

        /// <summary>Check 13 step 6 (HEAD on hotfix at Main work), with phase 4's rebase items; 380 high.</summary>
        public static readonly string[] Check13BranchMenu =
        [
            "feature/conflict",
            "Check out feature/conflict",
            "Merge feature/conflict into hotfix",
            "Merge feature/conflict into hotfix (create a merge commit)",
            "Rebase hotfix onto feature/conflict",
            "Interactive rebase of hotfix onto feature/conflict…",
            "—",
            "Push feature/conflict…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename feature/conflict…",
            "Delete feature/conflict… (danger)",
            "—",
            "Set upstream…",
            "—",
            "Copy branch name",
        ];

        /// <summary>Check 13 step 8 (main, whose tip hotfix is on), with phase 4's rebase items; 458 high.</summary>
        public static readonly string[] Check13MainMenu =
        [
            "main",
            "Check out main",
            "Merge main into hotfix",
            "Merge main into hotfix (create a merge commit)",
            "Rebase hotfix onto main (disabled: \"hotfix is up to date with main\")",
            "Interactive rebase of hotfix onto main…",
            "Fast-forward main to origin/main (disabled: \"main is up to date with origin/main\")",
            "—",
            "Push main to origin/main",
            "Force push main… (danger)",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename main…",
            "Delete main… (danger)",
            "—",
            "Change upstream…",
            "Stop tracking origin/main",
            "—",
            "Copy branch name",
        ];

        public static readonly string[] Check13HotfixMenu =
        [
            "hotfix",
            "Push hotfix…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename hotfix…",
            "Delete hotfix… (disabled: \"You cannot delete the branch you are on\")",
            "—",
            "Set upstream…",
            "—",
            "Copy branch name",
        ];

        public const string DeleteCurrentReason = "You cannot delete the branch you are on";

        // ---- Check 13b ----

        /// <summary>Check 13b step 1: check 2 step 4's list, with phase 4's rebase items; 293 high.</summary>
        public static readonly string[] Check13bRemoteMenu =
        [
            "origin/feature/alpha",
            "Check out origin/feature/alpha",
            "Merge origin/feature/alpha into main",
            "Merge origin/feature/alpha into main (create a merge commit)",
            "Rebase main onto origin/feature/alpha",
            "Interactive rebase of main onto origin/feature/alpha…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Delete feature/alpha on origin… (danger)",
            "—",
            "Copy branch name",
        ];

        public static readonly string[] Check13bTagMenu =
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

        public static readonly IReadOnlyList<Row> Check13bAlphaCopy = WithLabels(G0, 4, "alpha-copy", "feature/alpha", "origin/feature/alpha");

        public static readonly IReadOnlyList<Row> Check13bRelease = WithLabels(Check13bAlphaCopy, 6, "release-1.0", "v1.0");

        // ---- Check 14 ----

        /// <summary>G14a, the blocked checkout scenario's commit rows from the top (below its working-changes row).</summary>
        public static readonly string[] G14aSubjects =
        [
            "Add newfile",
            "Clashing change",
            "Other change",
            "Add notes",
        ];

        /// <summary>G14a with its lanes, colours, labels and dates (two lanes).</summary>
        public static readonly IReadOnlyList<Row> G14a =
        [
            R("Add newfile", 0, 0, "feature/addfile") with { Date = "2026-01-01 12:03" },
            R("Clashing change", 1, 1, "feature/clash") with { Date = "2026-01-01 12:02" },
            R("Other change", 0, 0, "feature/other") with { Date = "2026-01-01 12:01" },
            R("Add notes", 0, 0, "main" + Check) with { Date = "2026-01-01 12:00", Ends = [1] },
        ];

        public static readonly IReadOnlyList<Row> Check14OnOther =
            WithLabels(WithLabels(G14a, 2, "feature/other" + Check), 3, "main");

        public const string Check14StashMessage = "On feature/other: VisualCommit: before checking out feature/clash";

        /// <summary>Check 14 step 8's graph (three lanes), below its working-changes row.</summary>
        public static readonly IReadOnlyList<Row> Check14Conflicts =
        [
            S(Check14StashMessage, 0, 0) with { Date = "2026-01-02 09:00" },
            R("Add newfile", 1, 1, "feature/addfile"),
            R("Clashing change", 2, 2, "feature/clash" + Check),
            R("Other change", 0, 0, "feature/other") with { Ends = [1] },
            R("Add notes", 0, 0, "main") with { Ends = [2] },
        ];

        public static readonly WorkingRow Check14ConflictsWorking = new(WorkingChangesOneConflict, 2, 2);

        /// <summary>T1, git's refusal of the checkout of feature/addfile (standard error), as ErrorOutput shows it.</summary>
        public static readonly string[] T1 =
        [
            "error: Your local changes to the following files would be overwritten by checkout:",
            "\tnotes.txt",
            "Please commit your changes or stash them before you switch branches.",
            "error: The following untracked working tree files would be overwritten by checkout:",
            "\tnewfile.txt",
            "Please move or remove them before you switch branches.",
            "Aborting",
        ];

        public const string T1Key = "error: The following untracked working tree files would be overwritten by checkout:";

        /// <summary>T2, git's refusal of a checkout over the changed notes.txt.</summary>
        public static readonly string[] T2 =
        [
            "error: Your local changes to the following files would be overwritten by checkout:",
            "\tnotes.txt",
            "Please commit your changes or stash them before you switch branches.",
            "Aborting",
        ];

        public const string T2Key = "error: Your local changes to the following files would be overwritten by checkout:";

        /// <summary>T4, git's refusal of a checkout while notes.txt is conflicted (standard error only).</summary>
        public static readonly string[] T4 = ["error: you need to resolve your current index first"];

        public const string T4Key = "error: you need to resolve your current index first";

        public const string UntrackedSummary = "Untracked files are in the way: feature/addfile has files with the same names. Move or delete them, then check out again.";

        public static string BlockedSummary(string branch) => $"Your changes to these files would be overwritten by checking out {branch}.";

        public const string Check14ConflictsSummary =
            "Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file. Your changes are kept in the stash \"VisualCommit: before checking out feature/clash\".";

        public const string Check14ConflictsStatus = "Checked out feature/clash. Re-applying your changes stopped with conflicts in 1 file.";

        public const string ConflictsBanner = "1 file has conflicts. Resolve it, then stage it.";

        /// <summary>The banner's tooltip of conflicts without an operation (phase 4's design 10.3).</summary>
        public const string ConflictsBannerTip = "Click a conflicted file to resolve it, then mark it resolved.";

        /// <summary>The tooltip of a conflicted file's row in the stage panel (phase 4's design 5.3).</summary>
        public const string ConflictRowTip = "Conflict: click to resolve it";

        /// <summary>The banner's Continue, and the stage panel's "Continue cherry-pick", while one file conflicts (phase 4's design 10.4).</summary>
        public const string ResolveConflictsFirstOneFile = "Resolve the conflicts first (1 file)";

        public const string ResolveFirst = "Resolve the conflicts first";

        // ---- Check 15 ----

        /// <summary>Check 15 step 3's graph (four lanes): two stashes.</summary>
        public static readonly IReadOnlyList<Row> Check15Stashed =
        [
            S("On main: Save my work", 0, 0) with { Date = "2026-01-02 09:00" },
            S("On main: Experiment", 1, 1, "stash@{1}") with { Date = "2026-01-01 12:07" },
            R("Conflicting change", 2, 2, "feature/conflict"),
            R("Fast-forward me", 3, 3, "feature/ff"),
            R("Main work", 0, 0, "main" + Check, "origin/main", "v1.1") with { Ends = [1, 3] },
            R("Alpha two", 1, 4, "feature/alpha", "origin/feature/alpha"),
            R("Alpha one", 1, 4),
            R("Add core", 0, 0, "v1.0") with { Ends = [1, 2] },
            R("Initial commit", 0, 0),
        ];

        public const string WipMessage = "WIP on main: 88d6b91 Main work";

        /// <summary>Check 15 step 7's graph: step 3's with the new stash's message.</summary>
        public static readonly IReadOnlyList<Row> Check15WithoutUntracked = With(Check15Stashed, 0, S(WipMessage, 0, 0) with { Date = "2026-01-02 09:00" });

        /// <summary>Check 15 step 8's graph after the drop (three lanes).</summary>
        public static readonly IReadOnlyList<Row> Check15Dropped = With(G0, 0, S(WipMessage, 0, 0) with { Date = "2026-01-02 09:00" });

        public static readonly string[] Check15StashMenu =
        [
            "stash@{1} On main: Experiment",
            "Apply stash",
            "Pop stash",
            "Drop stash… (danger)",
            "—",
            "Copy SHA",
        ];

        public const string NoChangesToStash = "There are no changes to stash";

        // ---- Check 15b ----

        /// <summary>Check 15b step 1's graph (four lanes), below its working-changes row.</summary>
        public static readonly IReadOnlyList<Row> Check15bBefore =
        [
            R("Change README", 0, 0, "main" + Check) with { Date = "2026-01-01 12:08" },
            S("On main: Experiment", 1, 1),
            R("Conflicting change", 2, 2, "feature/conflict"),
            R("Fast-forward me", 3, 3, "feature/ff"),
            R("Main work", 0, 0, "origin/main", "v1.1") with { Ends = [1, 3] },
            R("Alpha two", 1, 4, "feature/alpha", "origin/feature/alpha"),
            R("Alpha one", 1, 4),
            R("Add core", 0, 0, "v1.0") with { Ends = [1, 2] },
            R("Initial commit", 0, 0),
        ];

        /// <summary>T5, git's refusal of the pop over the edited README.md (standard error).</summary>
        public static readonly string[] T5 =
        [
            "error: Your local changes to the following files would be overwritten by merge:",
            "\tREADME.md",
            "Please commit your changes or stash them before you merge.",
            "Aborting",
        ];

        public const string T5Key = "error: Your local changes to the following files would be overwritten by merge:";

        public const string PopConflictStatus = "Applying the stash stopped: 1 file has conflicts. The stash is kept.";

        // ---- Check 16 ----

        /// <summary>Check 16 step 1: phase 4's OC1 for "Alpha two" (284 high).</summary>
        public static readonly string[] Check16CommitMenu =
        [
            "ce5ea6a Alpha two",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto main",
            "Revert this commit",
            "Rebase main onto this commit",
            "Interactive rebase of main from here… (disabled: \"This commit is not on main\")",
            "Reset main to this commit…",
            "—",
            "Copy SHA",
        ];

        public const string Check16Context = "At ce5ea6a \"Alpha two\"";

        public const string Check16FfContext = "At d93814d \"Fast-forward me\"";

        public const string TagMessagePlaceholder = "Message (optional: makes an annotated tag)";

        public const string TagMessage = "#42 hotfix";

        public static readonly IReadOnlyList<Row> Check16Annotated = WithLabels(G0, 4, "feature/alpha", "origin/feature/alpha", "v1.2");

        public static readonly IReadOnlyList<Row> Check16Lightweight = WithLabels(Check16Annotated, 2, "feature/ff", "ff-point");

        public static readonly IReadOnlyList<Row> Check16Deleted = WithLabels(Check16Lightweight, 6);

        public static readonly string[] Check16TagMenu =
        [
            "v1.2",
            "Check out v1.2",
            "Create branch here…",
            "—",
            "Push v1.2 to origin",
            "Delete v1.2… (danger)",
            "Delete v1.2 on origin… (danger)",
            "—",
            "Copy tag name",
        ];

        /// <summary>T6, git's refusal of the leased deletion of v1.2 on origin; <c>{0}</c> is origin's URL.</summary>
        public static string[] T6(string origin) =>
        [
            "To " + origin,
            " ! [rejected]        (delete) -> v1.2 (stale info)",
            $"error: failed to push some refs to '{origin}'",
        ];

        public const string T6Key = " ! [rejected]        (delete) -> v1.2 (stale info)";

        // ---- Check 25 ----

        /// <summary>Check 25 step 1's graph (four lanes), below the working-changes row.</summary>
        public static readonly IReadOnlyList<Row> Check25Sequence =
        [
            R("Alpha one", 0, 0, "main" + Check) with { Date = "2026-01-01 12:02" },
            S("On main: Experiment", 1, 1),
            R("Conflicting change", 2, 2, "feature/conflict"),
            R("Fast-forward me", 3, 3, "feature/ff"),
            R("Main work", 0, 0, "origin/main", "v1.1") with { Ends = [1, 3] },
            R("Alpha two", 1, 4, "feature/alpha", "origin/feature/alpha"),
            R("Alpha one", 1, 4),
            R("Add core", 0, 0, "v1.0") with { Ends = [1, 2] },
            R("Initial commit", 0, 0),
        ];

        /// <summary>Check 25 step 1: a pick of git's list stopped by a conflict, one commit waiting (phase 4's design 5.2, "a pick in a list").</summary>
        public const string Check25SequenceBanner =
            "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts. 1 more commit waits: Continue picks it.";

        /// <summary>Check 25 step 1: the banner's tooltip for a pick in a list (phase 4's design 10.3).</summary>
        public const string Check25PickBannerTip =
            "Continue commits the resolved files and goes on with the commits that wait. Skip leaves this commit out and goes on. Abort puts main back where it was before the cherry-pick: the commits it already made are dropped too.";

        /// <summary>Check 25 step 4 (the new step on a fresh copy): the stopped pick's file resolved and staged (phase 4's design 5.2).</summary>
        public const string Check25ResolvedBanner =
            "Cherry-picking 078ef05 \"Conflicting change\". Conflicts resolved: continue to commit it. 1 more commit waits: Continue picks it.";

        /// <summary>Check 25 step 4: the list paused after the stopped pick was committed in a terminal (phase 4's design 5.2, "a list paused").</summary>
        public const string Check25PausedBanner = "Cherry-pick paused: 1 more commit waits. Continue picks it.";

        /// <summary>Check 25 step 4: Skip's reason on the paused list (phase 4's design 10.4).</summary>
        public const string NothingToSkip = "There is nothing to skip: the stopped commit was committed";

        /// <summary>The stage panel's commit button during a stop of a list of picks (phase 4's design 5.3).</summary>
        public const string ContinueCherryPick = "Continue cherry-pick";

        /// <summary>Amend's reason during a stop of a list of picks (phase 4's design 5.3).</summary>
        public const string FinishCherryPickFirst = "Finish or abort the cherry-pick first";

        /// <summary>Check 25 step 7: the banner's Continue on the empty pick (phase 4's design 10.4).</summary>
        public const string PickChangesNothing = "This cherry-pick changes nothing: skip it";

        public const string AbortPickTitle = "Abort the cherry-pick?";

        public const string AbortPickQuestion = "Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost.";

        public const string AbortPickSequenceQuestion = AbortPickQuestion + " Commits this cherry-pick already made are dropped too.";

        public const string AbortPickKeepsHeadQuestion = "Abort the cherry-pick? The 1 commit left is not picked. HEAD and the commits made so far stay as they are.";

        /// <summary>Check 25 step 4's graph after the commit (four lanes, no working-changes row).</summary>
        public static readonly IReadOnlyList<Row> Check25Committed =
        [
            R("Conflicting change", 0, 0, "main" + Check) with { Date = "2026-01-01 12:06" },
            R("Alpha one", 0, 0) with { Date = "2026-01-01 12:02" },
            S("On main: Experiment", 1, 1),
            R("Conflicting change", 2, 2, "feature/conflict"),
            R("Fast-forward me", 3, 3, "feature/ff"),
            R("Main work", 0, 0, "origin/main", "v1.1") with { Ends = [1, 3] },
            R("Alpha two", 1, 4, "feature/alpha", "origin/feature/alpha"),
            R("Alpha one", 1, 4),
            R("Add core", 0, 0, "v1.0") with { Ends = [1, 2] },
            R("Initial commit", 0, 0),
        ];

        /// <summary>Check 25 step 5's graph (four lanes), below the working-changes row.</summary>
        public static readonly IReadOnlyList<Row> Check25Revert = With(Check25Sequence, 0, R("Core again", 0, 0, "main" + Check) with { Date = "2026-01-01 12:08" });

        public const string Check25RevertBanner = "Reverting 88d6b91 \"Main work\". 1 file has conflicts.";

        public const string Check25RevertSummary = "Revert \"Main work\"";

        public const string Check25RevertDescription = "This reverts commit " + MainWorkSha + ".";

        /// <summary>Check 25 step 6's graph: G0 with HEAD detached at Main work by the rebase.</summary>
        public static readonly IReadOnlyList<Row> Check25Rebase = WithLabels(G0, 3, "HEAD", "main", "origin/main", "v1.1");

        /// <summary>
        /// Check 25 step 6: phase 4's rebase text (design 5.2, 14.3): <c>{onto}</c> is main, the local
        /// branch at the rebase's onto; the stopped commit and where it is in the rebase.
        /// </summary>
        public const string Check25RebaseBanner = "Rebasing feature/conflict onto main: stopped at 078ef05 \"Conflicting change\" (1 of 1). 1 file has conflicts.";

        public static readonly string[] T7 =
        [
            "The previous cherry-pick is now empty, possibly due to conflict resolution.",
            "If you wish to commit it anyway, use:",
            "    git commit --allow-empty",
            "Otherwise, please use 'git cherry-pick --skip'",
        ];

        public const string T7Key = "The previous cherry-pick is now empty, possibly due to conflict resolution.";

        public const string Check25EmptySummary = "dff8fbf changes nothing on main: its changes are already there. Abort the cherry-pick to go on.";

        /// <summary>Check 25 step 7: phase 4's empty pick text (design 5.2: "Skip it to go on.").</summary>
        public const string Check25EmptyBanner = "Cherry-picking dff8fbf \"Alpha one\" changes nothing: its changes are already here. Skip it to go on.";

        /// <summary>Check 25 step 8's graph (five lanes), below the working-changes row.</summary>
        public static readonly IReadOnlyList<Row> Check25StashConflicts =
        [
            R("Core again", 0, 0, "main" + Check) with { Date = "2026-01-01 12:09" },
            S("On main: Core experiment", 1, 1) with { Date = "2026-01-01 12:08" },
            S("On main: Experiment", 2, 2, "stash@{1}") with { Date = "2026-01-01 12:07" },
            R("Conflicting change", 3, 3, "feature/conflict"),
            R("Fast-forward me", 4, 4, "feature/ff"),
            R("Main work", 0, 0, "origin/main", "v1.1") with { Ends = [1, 2, 4] },
            R("Alpha two", 1, 5, "feature/alpha", "origin/feature/alpha"),
            R("Alpha one", 1, 5),
            R("Add core", 0, 0, "v1.0") with { Ends = [1, 3] },
            R("Initial commit", 0, 0),
        ];
    }
}
