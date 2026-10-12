// Part B's expected results (docs/test-reports/phase-3.md, part B: the graphs G0 to G12, E0 and
// E1, the commits the checks make, the menus and texts), copied from the report. Only part B's
// writer edits this file; its members live in Phase3Expectations.PartB. Compiled by the real-window
// pass too: see Phase3Expectations.Common.cs for what may be used here.
namespace VisualCommit.VisualTests.Phase3;

public static partial class Phase3Expectations
{
    /// <summary>What part B's checks (8, 9, 9b, 10, 11, 11b, 12, 32) compare against.</summary>
    public static class PartB
    {
        /// <summary>The repository's name in the tab, the log and the error dialog ("In ops").</summary>
        public const string Ops = "ops";

        /// <summary>The edge cases scenario's name.</summary>
        public const string Edges = "edges";

        /// <summary>G0, the ops scenario as built: the rows' messages from the top.</summary>
        public static readonly string[] G0Subjects =
        [
            "On main: Experiment",
            "Conflicting change",
            "Fast-forward me",
            "Main work",
            "Alpha two",
            "Alpha one",
            "Add core",
            "Initial commit",
        ];

        // ---------------------------------------------------------------------------------------
        // The commits ("Conventions of this part": ScenarioTests' ids and the commits the checks make).

        /// <summary>The ops scenario's commits by their short ids.</summary>
        public static class Sha
        {
            public const string Initial = "4ef1f7c";
            public const string AddCore = "8beca34";
            public const string AlphaOne = "dff8fbf";
            public const string AlphaTwo = "ce5ea6a";
            public const string MainWork = "88d6b91";
            public const string FastForwardMe = "d93814d";
            public const string Conflicting = "078ef05";
            public const string Stash = "0e95aca";

            public const string MainWorkFull = "88d6b91e821a43cd39c94aa7892c8095b628caf8";
            public const string AddCoreFull = "8beca345049bf7e8bab3a7b4f1427eff941b87e3";
            public const string AlphaTwoFull = "ce5ea6ab16759bede02ce706dd5c6865443c2051";

            /// <summary>Check 8 part B: "Merge branch 'feature/ff'".</summary>
            public const string MergeFf = "22a08ae80b78afd6ee714929be263ada7ad53abd";

            /// <summary>Check 8 part C: "Merge remote-tracking branch 'origin/feature/alpha'".</summary>
            public const string MergeRemoteAlpha = "aac947ea8509069c5bb3bd48207b97fef85d2571";

            /// <summary>Check 9b: the stage panel's merge commit.</summary>
            public const string MergeConflict = "58e558d2070047d8b005ca92564b0ec9ef371b90";

            /// <summary>Checks 11 and 11b: the pick of Alpha one.</summary>
            public const string PickedAlphaOne = "d1f9fb5fa4728b88c1d36136f0f65fb8456ac586";

            /// <summary>Check 11 part B: the pick of Alpha two.</summary>
            public const string PickedAlphaTwo = "c0f718d25dff3ea0ef8d67ada2d3411c9ab45b94";

            /// <summary>Check 12 parts A, C and E: "Revert "Main work"".</summary>
            public const string RevertMainWork = "13c773dd19dbe8f7d2261eb3a2d7c2392670cd22";

            /// <summary>Check 12 part B: the merge of feature/alpha.</summary>
            public const string MergeAlpha = "f19f50a4fbefcf6a7e4a61a00a87c5e677639657";

            /// <summary>Check 12 part B: its revert.</summary>
            public const string RevertMergeAlpha = "f5df804f48e2e0967b6f55b974da35041a12eb26";

            /// <summary>Check 12 part C: the second revert.</summary>
            public const string RevertAddCore = "123d13b08940d7900cf9e76ca7621692a8d30e36";

            /// <summary>Check 12 part D: "Core again", made through TempRepo before the app starts.</summary>
            public const string CoreAgain = "963800ff2b9def6e9bec0baa6608507afcf83824";

            // The edges scenario.
            public const string EdgesInitial = "691f8cf";
            public const string EdgesMainWork = "8e16408";
            public const string EdgesAlphaWork = "a024e50";
            public const string DetachedWork = "419190f";
        }

        /// <summary>The date the commits the app makes in a check carry (D72), as the graph and the details show it.</summary>
        public const string AppDate = "2026-01-02 09:00";

        // ---------------------------------------------------------------------------------------
        // The graphs ("Graphs these checks refer to").

        /// <summary>What a label stands for, as the report writes it (✓ for the branch HEAD is on).</summary>
        public enum LabelKind
        {
            Head,
            Current,
            Local,
            Remote,
            Tag,
            Stash,
        }

        /// <summary>One label of a row, in drawing order.</summary>
        public sealed record GraphLabel(string Text, LabelKind Kind)
        {
            public override string ToString() => Kind switch
            {
                LabelKind.Current => Text + " ✓",
                LabelKind.Remote => Text + " (remote)",
                LabelKind.Tag => Text + " (tag)",
                _ => Text,
            };
        }

        /// <summary>How a row's node is drawn: a filled circle 10 across, a merge commit's 7 across, a stash's ring.</summary>
        public enum Node
        {
            Commit,
            Merge,
            Stash,
        }

        /// <summary>One row of a graph table: its message, node (lane, colour, shape), date, short id and labels.</summary>
        public sealed record Row(string Message, int Lane, int Colour, Node Node, string Date, string ShortSha, IReadOnlyList<GraphLabel> Labels)
        {
            /// <summary>The row with other labels (G1, G6: labels moved).</summary>
            public Row WithLabels(params GraphLabel[] labels) => this with { Labels = labels };
        }

        /// <summary>
        /// One line of a graph as the report's "Lines" give it: the line of <paramref name="Lane"/>
        /// in colour <paramref name="Colour"/> leaves the node of <paramref name="FromRow"/> (in
        /// <paramref name="StartLane"/>, the lane itself when null: "From row 0's node a line goes
        /// to lane 1"), runs down the lane through every row between, a node of its own lane on the
        /// way included ("runs to row 3's node and on into row 4's node"), and ends in the node of
        /// <paramref name="ToRow"/>.
        /// </summary>
        public sealed record Segment(int Lane, int Colour, int FromRow, int ToRow, int? StartLane = null);

        /// <summary>A graph of the report: its lane count (the Graph column's width), rows and lines.</summary>
        public sealed record GraphTable(string Name, int Lanes, IReadOnlyList<Row> Rows, IReadOnlyList<Segment> Lines);

        public static GraphLabel Cur(string name) => new(name, LabelKind.Current);

        public static GraphLabel Loc(string name) => new(name, LabelKind.Local);

        public static GraphLabel Rem(string name) => new(name, LabelKind.Remote);

        public static GraphLabel Tag(string name) => new(name, LabelKind.Tag);

        public static GraphLabel StashLabel { get; } = new("stash@{0}", LabelKind.Stash);

        public static GraphLabel HeadLabel { get; } = new("HEAD", LabelKind.Head);

        private static Row R(string message, int lane, int colour, string date, string sha, params GraphLabel[] labels) =>
            new(message, lane, colour, Node.Commit, date, sha, labels);

        private static Row M(string message, int lane, int colour, string date, string sha, params GraphLabel[] labels) =>
            new(message, lane, colour, Node.Merge, date, sha, labels);

        private static Row S(int lane, int colour) =>
            new("On main: Experiment", lane, colour, Node.Stash, "2026-01-01 12:07", Sha.Stash, [StashLabel]);

        private static Row Conflicting(int lane, int colour) =>
            R("Conflicting change", lane, colour, "2026-01-01 12:06", Sha.Conflicting, Loc("feature/conflict"));

        private static Row FastForwardMe(int lane, int colour, params GraphLabel[] labels) =>
            R("Fast-forward me", lane, colour, "2026-01-01 12:05", Sha.FastForwardMe, labels.Length == 0 ? [Loc("feature/ff")] : labels);

        private static Row MainWork(params GraphLabel[] labels) => R("Main work", 0, 0, "2026-01-01 12:04", Sha.MainWork, labels);

        private static Row AlphaTwo(int lane, int colour) =>
            R("Alpha two", lane, colour, "2026-01-01 12:03", Sha.AlphaTwo, Loc("feature/alpha"), Rem("origin/feature/alpha"));

        private static Row AlphaOne(int lane, int colour) => R("Alpha one", lane, colour, "2026-01-01 12:02", Sha.AlphaOne);

        private static Row AddCore(params GraphLabel[] labels) => R("Add core", 0, 0, "2026-01-01 12:01", Sha.AddCore, labels.Length == 0 ? [Tag("v1.0")] : labels);

        private static Row Initial() => R("Initial commit", 0, 0, "2026-01-01 12:00", Sha.Initial);

        /// <summary>G0, the ops scenario as built: 3 lanes.</summary>
        public static GraphTable G0 { get; } = new(
            "G0",
            3,
            [
                S(0, 0),
                Conflicting(1, 1),
                FastForwardMe(2, 2),
                MainWork(Cur("main"), Rem("origin/main"), Tag("v1.1")),
                AlphaTwo(2, 3),
                AlphaOne(2, 3),
                AddCore(),
                Initial(),
            ],
            [new(0, 0, 0, 7), new(1, 1, 1, 6), new(2, 2, 2, 3), new(2, 3, 4, 6)]);

        /// <summary>G1, after merging feature/ff as a fast-forward: G0 with main ✓ moved to "Fast-forward me".</summary>
        public static GraphTable G1 { get; } = G0 with
        {
            Name = "G1",
            Rows = [.. G0.Rows.Select((row, index) => index switch
            {
                2 => row.WithLabels(Cur("main"), Loc("feature/ff")),
                3 => row.WithLabels(Rem("origin/main"), Tag("v1.1")),
                _ => row,
            })],
        };

        /// <summary>G2, after "Merge feature/ff into main (create a merge commit)": 4 lanes.</summary>
        public static GraphTable G2 { get; } = new(
            "G2",
            4,
            [
                M("Merge branch 'feature/ff'", 0, 0, AppDate, "22a08ae", Cur("main")),
                S(2, 2),
                Conflicting(3, 3),
                FastForwardMe(1, 1),
                MainWork(Rem("origin/main"), Tag("v1.1")),
                AlphaTwo(1, 4),
                AlphaOne(1, 4),
                AddCore(),
                Initial(),
            ],
            [new(0, 0, 0, 8), new(1, 1, 0, 4, StartLane: 0), new(2, 2, 1, 4), new(3, 3, 2, 7), new(1, 4, 5, 7)]);

        /// <summary>G3, after "Merge origin/feature/alpha into main": 5 lanes.</summary>
        public static GraphTable G3 { get; } = new(
            "G3",
            5,
            [
                M("Merge remote-tracking branch 'origin/feature/alpha'", 0, 0, AppDate, "aac947e", Cur("main")),
                S(2, 2),
                Conflicting(3, 3),
                FastForwardMe(4, 4),
                MainWork(Rem("origin/main"), Tag("v1.1")),
                AlphaTwo(1, 1),
                AlphaOne(1, 1),
                AddCore(),
                Initial(),
            ],
            [new(0, 0, 0, 8), new(1, 1, 0, 7, StartLane: 0), new(2, 2, 1, 4), new(3, 3, 2, 7), new(4, 4, 3, 4)]);

        /// <summary>G5, after the stopped merge of check 9b is committed: 4 lanes.</summary>
        public static GraphTable G5 { get; } = new(
            "G5",
            4,
            [
                M("Merge branch 'feature/conflict'", 0, 0, AppDate, "58e558d", Cur("main")),
                S(2, 2),
                Conflicting(1, 1),
                FastForwardMe(3, 3),
                MainWork(Rem("origin/main"), Tag("v1.1")),
                AlphaTwo(2, 4),
                AlphaOne(2, 4),
                AddCore(),
                Initial(),
            ],
            [new(0, 0, 0, 8), new(1, 1, 0, 7, StartLane: 0), new(2, 2, 1, 4), new(3, 3, 3, 4), new(2, 4, 5, 7)]);

        /// <summary>G6, after any reset of main to "Add core": G0's rows (below a working-changes row) with main ✓ on "Add core"; 3 lanes.</summary>
        public static GraphTable G6 { get; } = G0 with
        {
            Name = "G6",
            Rows = [.. G0.Rows.Select((row, index) => index switch
            {
                3 => row.WithLabels(Rem("origin/main"), Tag("v1.1")),
                6 => row.WithLabels(Cur("main"), Tag("v1.0")),
                _ => row,
            })],
        };

        /// <summary>G7, after "Cherry-pick onto main" of "Alpha one": 4 lanes.</summary>
        public static GraphTable G7 { get; } = new(
            "G7",
            4,
            [
                R("Alpha one", 0, 0, "2026-01-01 12:02", "d1f9fb5", Cur("main")),
                S(1, 1),
                Conflicting(2, 2),
                FastForwardMe(3, 3),
                MainWork(Rem("origin/main"), Tag("v1.1")),
                AlphaTwo(1, 4),
                AlphaOne(1, 4),
                AddCore(),
                Initial(),
            ],
            [new(0, 0, 0, 8), new(1, 1, 1, 4), new(2, 2, 2, 7), new(3, 3, 3, 4), new(1, 4, 5, 7)]);

        /// <summary>G8, after "Cherry-pick 2 commits onto main" of "Alpha one" and "Alpha two": 4 lanes.</summary>
        public static GraphTable G8 { get; } = new(
            "G8",
            4,
            [
                R("Alpha two", 0, 0, "2026-01-01 12:03", "c0f718d", Cur("main")),
                R("Alpha one", 0, 0, "2026-01-01 12:02", "d1f9fb5"),
                S(1, 1),
                Conflicting(2, 2),
                FastForwardMe(3, 3),
                MainWork(Rem("origin/main"), Tag("v1.1")),
                AlphaTwo(1, 4),
                AlphaOne(1, 4),
                AddCore(),
                Initial(),
            ],
            [new(0, 0, 0, 9), new(1, 1, 2, 5), new(2, 2, 3, 8), new(3, 3, 4, 5), new(1, 4, 6, 8)]);

        /// <summary>G9, after "Revert this commit" on "Main work": G7 with row 0 replaced.</summary>
        public static GraphTable G9 { get; } = G7 with
        {
            Name = "G9",
            Rows = [R("Revert \"Main work\"", 0, 0, AppDate, "13c773d", Cur("main")), .. G7.Rows.Skip(1)],
        };

        /// <summary>G10, after the merge of feature/alpha and its revert: 5 lanes.</summary>
        public static GraphTable G10 { get; } = new(
            "G10",
            5,
            [
                R("Revert \"Merge branch 'feature/alpha'\"", 0, 0, AppDate, "f5df804", Cur("main")),
                M("Merge branch 'feature/alpha'", 0, 0, AppDate, "f19f50a"),
                S(2, 2),
                Conflicting(3, 3),
                FastForwardMe(4, 4),
                MainWork(Rem("origin/main"), Tag("v1.1")),
                AlphaTwo(1, 1),
                AlphaOne(1, 1),
                AddCore(),
                Initial(),
            ],
            [new(0, 0, 0, 9), new(1, 1, 1, 8, StartLane: 0), new(2, 2, 2, 5), new(3, 3, 3, 8), new(4, 4, 4, 5)]);

        /// <summary>G10 without its row 0 (check 12 step 3, the shape of G3): the merge of feature/alpha with main ✓.</summary>
        public static GraphTable G10BeforeRevert { get; } = G3 with
        {
            Name = "G10 without row 0",
            Rows = [M("Merge branch 'feature/alpha'", 0, 0, AppDate, "f19f50a", Cur("main")), .. G3.Rows.Skip(1)],
        };

        /// <summary>G11, after "Revert 2 commits" of "Main work" and "Add core": G8's shape.</summary>
        public static GraphTable G11 { get; } = G8 with
        {
            Name = "G11",
            Rows =
            [
                R("Revert \"Add core\"", 0, 0, AppDate, "123d13b", Cur("main")),
                R("Revert \"Main work\"", 0, 0, AppDate, "13c773d"),
                .. G8.Rows.Skip(2),
            ],
        };

        /// <summary>G12, the start of check 12 part D: G7's shape with "Core again" on top.</summary>
        public static GraphTable G12 { get; } = G7 with
        {
            Name = "G12",
            Rows = [R("Core again", 0, 0, "2026-01-01 12:08", "963800f", Cur("main")), .. G7.Rows.Skip(1)],
        };

        /// <summary>E0, the edges scenario as built: 2 lanes, HEAD detached on "Detached work".</summary>
        public static GraphTable E0 { get; } = new(
            "E0",
            2,
            [
                R("Detached work", 0, 0, "2026-01-01 12:03", Sha.DetachedWork, HeadLabel),
                R("Alpha work", 1, 1, "2026-01-01 12:02", Sha.EdgesAlphaWork, Loc("feature/alpha")),
                R("Main work", 0, 0, "2026-01-01 12:01", Sha.EdgesMainWork, Loc("main"), Rem("origin/main"), Tag("feature/alpha")),
                R("Initial commit", 0, 0, "2026-01-01 12:00", Sha.EdgesInitial, Loc("loc"), Loc("origin/main"), Loc("side")),
            ],
            [new(0, 0, 0, 3), new(1, 1, 1, 2)]);

        /// <summary>E1, edges after checking out main from the detached HEAD: 1 lane.</summary>
        public static GraphTable E1 { get; } = new(
            "E1",
            1,
            [
                R("Alpha work", 0, 0, "2026-01-01 12:02", Sha.EdgesAlphaWork, Loc("feature/alpha")),
                R("Main work", 0, 0, "2026-01-01 12:01", Sha.EdgesMainWork, Cur("main"), Rem("origin/main"), Tag("feature/alpha")),
                R("Initial commit", 0, 0, "2026-01-01 12:00", Sha.EdgesInitial, Loc("loc"), Loc("origin/main"), Loc("side")),
            ],
            [new(0, 0, 0, 2)]);

        /// <summary>E1 after check 32 step 7: HEAD detached at "Initial commit", main without its check mark.</summary>
        public static GraphTable E1Detached { get; } = E1 with
        {
            Name = "E1 detached at Initial commit",
            Rows =
            [
                E1.Rows[0],
                E1.Rows[1].WithLabels(Loc("main"), Rem("origin/main"), Tag("feature/alpha")),
                E1.Rows[2].WithLabels(HeadLabel, Loc("loc"), Loc("origin/main"), Loc("side")),
            ],
        };

        // ---------------------------------------------------------------------------------------
        // Menus, as the report writes them (header first; "—" a separator).

        private const string Sep = "—";

        /// <summary>A commit's menu on main when the commit is already on it (checks 10 step 2, 11 step 2, 12 step 11).</summary>
        public static string[] CommitMenuOnMain(string shortSha, string subject) =>
        [
            $"{shortSha} {subject}",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            Sep,
            "Cherry-pick onto main (disabled: \"This commit is already on main\")",
            "Revert this commit",
            "Reset main to this commit…",
            Sep,
            "Copy SHA",
        ];

        /// <summary>Check 8 step 2: the label feature/ff's menu (no upstream); 328 high.</summary>
        public static readonly string[] FeatureFfMenu =
        [
            "feature/ff",
            "Check out feature/ff",
            "Merge feature/ff into main",
            "Merge feature/ff into main (create a merge commit)",
            Sep,
            "Push feature/ff…",
            Sep,
            "Create branch here…",
            "Create tag here…",
            "Rename feature/ff…",
            "Delete feature/ff… (danger)",
            Sep,
            "Set upstream…",
            Sep,
            "Copy branch name",
        ];

        /// <summary>Check 8 step 6, the first menu: the remote branch origin/main, whose main is checked out; 241 high.</summary>
        public static readonly string[] RemoteMainMenu =
        [
            "origin/main",
            "Check out origin/main (disabled: \"main is already checked out\")",
            "Merge origin/main into main",
            "Merge origin/main into main (create a merge commit)",
            Sep,
            "Create branch here…",
            "Create tag here…",
            Sep,
            "Delete main on origin… (danger)",
            Sep,
            "Copy branch name",
        ];

        /// <summary>Check 8 step 6, the second menu: the remote branch origin/feature/alpha; 241 high.</summary>
        public static readonly string[] RemoteAlphaMenu =
        [
            "origin/feature/alpha",
            "Check out origin/feature/alpha",
            "Merge origin/feature/alpha into main",
            "Merge origin/feature/alpha into main (create a merge commit)",
            Sep,
            "Create branch here…",
            "Create tag here…",
            Sep,
            "Delete feature/alpha on origin… (danger)",
            Sep,
            "Copy branch name",
        ];

        /// <summary>Check 9 step 5: the label feature/alpha's menu while the merge is stopped; 406 high.</summary>
        public static readonly string[] AlphaMenuWhileMerging =
        [
            "feature/alpha",
            "Check out feature/alpha",
            "Merge feature/alpha into main (disabled: \"Finish or abort the merge first\")",
            "Merge feature/alpha into main (create a merge commit) (disabled: \"Finish or abort the merge first\")",
            "Fast-forward feature/alpha to origin/feature/alpha (disabled: \"feature/alpha is up to date with origin/feature/alpha\")",
            Sep,
            "Push feature/alpha to origin/feature/alpha",
            "Force push feature/alpha… (danger)",
            Sep,
            "Create branch here…",
            "Create tag here…",
            "Rename feature/alpha…",
            "Delete feature/alpha… (danger)",
            Sep,
            "Change upstream…",
            "Stop tracking origin/feature/alpha",
            Sep,
            "Copy branch name",
        ];

        /// <summary>Check 9b step 3: the conflicted src/core.txt's menu in the stage panel; 232 high.</summary>
        public static readonly string[] ConflictedFileMenu =
        [
            "core.txt",
            "Mark as resolved",
            "Discard changes… (disabled: \"Resolve the conflict first\")",
            Sep,
            "Ignore core.txt (disabled: \"Only untracked files can be ignored\")",
            "Ignore all *.txt files (disabled: \"Only untracked files can be ignored\")",
            Sep,
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        /// <summary>Check 11 step 6: three commits, one of them on main; 119 high.</summary>
        public static readonly string[] ThreeCommitsMenu =
        [
            "3 commits",
            "Cherry-pick 3 commits onto main (disabled: \"Some of these commits are already on main\")",
            "Revert 3 commits",
            Sep,
            "Copy SHAs",
        ];

        /// <summary>Check 11 step 10: two commits that can be picked; 119 high.</summary>
        public static readonly string[] TwoCommitsMenu =
        [
            "2 commits",
            "Cherry-pick 2 commits onto main",
            "Revert 2 commits",
            Sep,
            "Copy SHAs",
        ];

        /// <summary>Check 12 step 4: the merge and "Main work"; 119 high.</summary>
        public static readonly string[] MergeAndOtherMenu =
        [
            "2 commits",
            "Cherry-pick 2 commits onto main (disabled: \"Merge commits and stashes cannot be cherry-picked\")",
            "Revert 2 commits (disabled: \"Revert merge commits one at a time\")",
            Sep,
            "Copy SHAs",
        ];

        /// <summary>Check 12 step 5: the merge commit's menu; 232 high.</summary>
        public static readonly string[] MergeCommitMenu =
        [
            "f19f50a Merge branch 'feature/alpha'",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            Sep,
            "Cherry-pick onto main (disabled: \"Merge commits cannot be cherry-picked here\")",
            "Revert this merge commit",
            "Reset main to this commit…",
            Sep,
            "Copy SHA",
        ];

        /// <summary>Check 32 step 2: the detached HEAD's commit menu; 232 high.</summary>
        public static readonly string[] DetachedCommitMenu =
        [
            "419190f Detached work",
            "Check out this commit (disabled: \"HEAD is already at this commit\")",
            "Create branch here…",
            "Create tag here…",
            Sep,
            "Cherry-pick onto HEAD (disabled: \"This commit is already on HEAD\")",
            "Revert this commit",
            "Reset HEAD to this commit…",
            Sep,
            "Copy SHA",
        ];

        /// <summary>Check 32 step 3: main's menu while HEAD is detached; 406 high.</summary>
        public static readonly string[] MainMenuWhileDetached =
        [
            "main",
            "Check out main",
            "Merge main into HEAD (disabled: \"Check out a branch to merge into\")",
            "Merge main into HEAD (create a merge commit) (disabled: \"Check out a branch to merge into\")",
            "Fast-forward main to origin/main (disabled: \"main is up to date with origin/main\")",
            Sep,
            "Push main to origin/main",
            "Force push main… (danger)",
            Sep,
            "Create branch here…",
            "Create tag here…",
            "Rename main…",
            "Delete main… (danger)",
            Sep,
            "Change upstream…",
            "Stop tracking origin/main",
            Sep,
            "Copy branch name",
        ];

        // ---------------------------------------------------------------------------------------
        // Texts.

        public const string MergeBanner = "Merging: \"Merge branch 'feature/conflict'\". 1 file has conflicts.";

        public const string MergeResolvedBanner = "Merging: \"Merge branch 'feature/conflict'\". Conflicts resolved: commit to finish the merge.";

        public const string CherryPickBanner = "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts.";

        public const string RevertBanner = "Reverting 88d6b91 \"Main work\". 1 file has conflicts.";

        /// <summary>Check 12 part E on a git that keeps REVERT_HEAD after an empty revert (D117's other case).</summary>
        public const string EmptyRevertBanner = "Reverting 88d6b91 \"Main work\" changes nothing: it is already undone. Abort to go on.";

        /// <summary>The banner's tooltip (section 5), check 9 step 3.</summary>
        public const string BannerToolTip = "Continue and Skip, and the conflict resolver, come in a later version. Resolve the files in your editor and stage them, or abort.";

        /// <summary>The tooltip of a conflicted file's row in the stage panel (section 5).</summary>
        public const string ConflictRowToolTip = "Conflict: resolve it in your editor, then stage it";

        public const string FinishMergeFirst = "Finish or abort the merge first";

        public const string BackupDetail = "A backup is saved first, so this can be undone.";

        public const string WorkingChangesOneConflict = "Working changes · 1 conflict";

        public const string WorkingChanges = "Working changes";

        /// <summary>MultiSelectionView's hint.</summary>
        public const string MultiSelectionHint = "Right-click a selected commit for what can be done with all of them.";

        /// <summary>A file's text as git writes it after the merge of feature/conflict stopped (check 9 step 2).</summary>
        public const string CoreMergeConflict = "core\n<<<<<<< HEAD\nmain work\n=======\nconflict\n>>>>>>> refs/heads/feature/conflict\n";

        /// <summary>src/core.txt as the cherry-pick of "Conflicting change" leaves it (check 11b step 2).</summary>
        public const string CoreCherryPickConflict = "core\n<<<<<<< HEAD\nmain work\n=======\nconflict\n>>>>>>> 078ef05 (Conflicting change)\n";

        /// <summary>src/core.txt as the revert of "Main work" over "Core again" leaves it (check 12 step 8).</summary>
        public const string CoreRevertConflict = "core\n<<<<<<< HEAD\ncore again\n=======\n>>>>>>> parent of 88d6b91 (Main work)\n";

        /// <summary>src/core.txt as check 9b's editor resolves it.</summary>
        public const string CoreResolved = "core\nmain work\nconflict\n";

        /// <summary>Check 8 step 3: the reflog subject of the fast-forward on Git 2.36.</summary>
        public const string FastForwardReflog = "merge refs/heads/feature/ff: Fast-forward (no commit created; -m option ignored)";

        /// <summary>Check 8 step 3 on any other git: how the reflog subject starts.</summary>
        public const string FastForwardReflogStart = "merge refs/heads/feature/ff: Fast-forward";

        // The reset dialog (check 10 step 3).
        public const string ResetTitle = "Reset main to 8beca34?";
        public const string ResetText = "Move main to \"Add core\" (8beca34).";
        public const string ResetSoftLine = "Keep every change; the changes of the commits after it are staged.";
        public const string ResetMixedLine = "Keep every change; nothing is staged.";
        public const string ResetHardLine = "Discard every change to tracked files.";

        // Check 10 part C: the files a terminal leaves before the app starts.
        public const string CoreStaged = "core\nmain work\nstaged line\n";
        public const string CoreUnstaged = "core\nmain work\nstaged line\nunstaged line\n";
        public const string ReadmeLocalEdit = "# Operations\n\nLocal edit.\n";
        public const string Scratch = "scratch\n";
        public const string Todo = "todo\n";

        // The confirmations of the banner's Abort (2.15) and of leaving a detached HEAD (2.1).
        public const string AbortMergeTitle = "Abort the merge?";
        public const string AbortMergeQuestion = "Abort merging \"Merge branch 'feature/conflict'\"? Changes made to resolve conflicts are lost.";
        public const string AbortCherryPickTitle = "Abort the cherry-pick?";
        public const string AbortCherryPickQuestion = "Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost.";
        public const string AbortRevertTitle = "Abort the revert?";
        public const string AbortRevertQuestion = "Abort the revert? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost.";
        public const string LeaveTitle = "Leave these commits?";
        public const string LeaveQuestion = "HEAD is detached on 1 commit that is on no branch. After this checkout it no longer shows in the graph.";

        // The status texts (4e).
        public const string StatusFastForwarded = "Fast-forwarded main to feature/ff";
        public const string StatusMergedFf = "Merged feature/ff into main";
        public const string StatusMergedRemoteAlpha = "Merged origin/feature/alpha into main";
        public const string StatusMergedAlpha = "Merged feature/alpha into main";
        public const string StatusUpToDate = "main is already up to date with feature/alpha";
        public const string StatusMergeStopped = "Merge stopped: 1 file has conflicts";
        public const string StatusCheckoutAlphaFailed = "Check out feature/alpha failed";
        public const string StatusAbortedMerge = "Aborted the merge";
        public const string StatusResetSoft = "Reset main to 8beca34 (soft)";
        public const string StatusResetMixed = "Reset main to 8beca34 (mixed)";
        public const string StatusResetHard = "Reset main to 8beca34 (hard)";
        public const string StatusPickedAlphaOne = "Cherry-picked dff8fbf onto main";
        public const string StatusPickedTwo = "Cherry-picked 2 commits onto main";
        public const string StatusPickStopped = "Cherry-picked 1 of 2 commits. 078ef05 stopped with conflicts in 1 file.";
        public const string StatusAbortedPick = "Aborted the cherry-pick";
        public const string StatusRevertedMainWork = "Reverted 88d6b91";
        public const string StatusRevertedMerge = "Reverted f19f50a";
        public const string StatusRevertedTwo = "Reverted 2 commits";
        public const string StatusRevertStopped = "Revert stopped: 1 file has conflicts";
        public const string StatusAbortedRevert = "Aborted the revert";
        public const string StatusRevertFailed = "Revert 88d6b91 failed";
        public const string StatusCheckedOutMain = "Checked out main";
        public const string StatusDetachedAtInitial = "HEAD is now detached at 691f8cf";

        // Check 9 step 7: the checkout git refuses during the merge (the error dialog's parts).
        public const string CheckoutFailedTitle = "Checkout failed";
        public const string CheckoutFailedSummary = "Git could not check out feature/alpha.";
        public const string CheckoutCommand = "git switch --no-guess feature/alpha";
        public static readonly string[] CheckoutWhileMergingLines =
            ["fatal: cannot switch branch while merging", "Consider \"git merge --quit\" or \"git worktree add\"."];

        // Check 12 step 11: the revert that changes nothing (D117), on Git 2.36.
        public const string RevertFailedTitle = "Revert failed";
        public const string EmptyRevertSummary = "88d6b91 changes nothing on main: it is already undone.";
        public const string EmptyRevertCommand = "git revert --no-edit " + Sha.MainWorkFull;
        public static readonly string[] EmptyRevertLines =
        [
            "On branch main",
            "Your branch is ahead of 'origin/main' by 1 commit.",
            "  (use \"git push\" to publish your local commits)",
            "nothing to commit, working tree clean",
        ];

        /// <summary>Git's MERGE_MSG an empty revert leaves behind on Git 2.36 (check 12 step 11).</summary>
        public const string EmptyRevertMergeMsg = "Revert \"Main work\"\n\nThis reverts commit " + Sha.MainWorkFull + ".\n";

        // ---------------------------------------------------------------------------------------
        // The real-window pass: the operations' titles in the log (design 1.7) and the lanes its
        // row points are computed for.

        /// <summary>
        /// The centre of the label feature/conflict on G0's row 1 ("Conflicting change") at
        /// 1100×700, in the window, before any banner shows: where check 9 step 2 right-clicks.
        /// Only the scripted run can measure a label (D49); check 9 asserts this constant (±1) and
        /// the real-window pass right-clicks it.
        /// </summary>
        public static readonly (double X, double Y) FeatureConflictLabelCentre = (314, 155);

        public const string MergeConflictTitle = "Merge feature/conflict";
        public const string AbortMergeOperation = "Abort the merge";
        public const string ResetOperation = "Reset main";
        public const string CherryPickTwoOperation = "Cherry-pick 2 commits";
    }
}
