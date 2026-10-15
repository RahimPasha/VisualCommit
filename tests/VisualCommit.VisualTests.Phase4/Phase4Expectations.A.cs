// Part A's expected results (docs/test-reports/phase-4.md, "Part A: rebase from the menus and by
// drag, pull with rebase", and the rows A1 to A5 of "Checks"), copied from the report. Only part
// A's writer edits this file; its members live in Phase4Expectations.PartA. Compiled by the
// real-window pass too: see Phase4Expectations.Common.cs for what may be used here (no type of
// the walk-through's own files, so the error dialogs are given as their parts).
namespace VisualCommit.VisualTests.Phase4;

public static partial class Phase4Expectations
{
    /// <summary>What part A's checks (A1 to A5) compare against.</summary>
    public static class PartA
    {
        // ---- The "rebase" scenario (Scenarios.RebaseAsync), as Phase4ScenarioTests pins it. ----

        /// <summary>The tab's and the repository's name of the "rebase" scenario.</summary>
        public const string RebaseRepo = "rebase";

        public const string InitialCommitSha = "7bce6bf8d12d1b2fac2dae78ef358ce48d0df22c";

        public const string AddCoreSha = "996fdc12d7467abcae213421b92ad016c5ed26f9";

        public const string TopicOneSha = "1219e45225ea55ad2c7a0e9412adaeeffd07a43c";

        public const string TopicTwoSha = "b0dfa3f405dffff59bea3f4f434b16e5490cc597";

        public const string MainWorkSha = "dd6a6fc8095ffa05dc783ce011e322a7241551a4";

        public const string MainMoreSha = "1dd58b8cfc17d4d9b80550eddd80b11ebf94c146";

        public const string OtherOneSha = "2d4d79a03112c8a5e2f9a783318d588df519af5d";

        /// <summary>R0's rows, as <c>SelectedIndex</c> counts them (no working-changes row).</summary>
        public const int OtherOneRow = 0;

        public const int MainMoreRow = 1;

        public const int MainWorkRow = 2;

        public const int TopicTwoRow = 3;

        public const int TopicOneRow = 4;

        public const int AddCoreRow = 5;

        public const int InitialCommitRow = 6;

        /// <summary>R0 and R1 and R2 have three lanes (Graph column 64, the Message column at x 454); R3 two (48, x 438).</summary>
        public const int RebaseLanes = 3;

        /// <summary>R3's lanes.</summary>
        public const int R3Lanes = 2;

        // ---- Commits the checks make ("Commits the checks make"). ----

        /// <summary>A2 part A: "Topic one" rebased onto main (parent Main more).</summary>
        public const string TopicOneOntoMainSha = "befae2677a3a259f2c9423912af47575e319c852";

        /// <summary>A2 part A: "Topic two" rebased onto main (parent the new Topic one): feature/topic's new tip.</summary>
        public const string TopicTwoOntoMainSha = "5f71ab8bad51a15fdfdfd19fc1b0084ef6e956c5";

        /// <summary>A2 part B: "Topic one" rebased onto Main work.</summary>
        public const string TopicOneOntoMainWorkSha = "c09318c0e69377cb1150a1ebb1f19c6d4af2dd61";

        /// <summary>A2 part B: "Topic two" rebased onto Main work: feature/topic's new tip.</summary>
        public const string TopicTwoOntoMainWorkSha = "09b923a8b8da60f7ba4f86e6d4cab9221a78326e";

        /// <summary>A3: "Other one" rebased onto main: feature/other's new tip.</summary>
        public const string OtherOneOntoMainSha = "14e607bf248c8917ab93680c48cba392f60546ff";

        // ---- The graphs of the "rebase" scenario (R0 to R3). ----

        /// <summary>R0, "rebase" as built: 3 lanes.</summary>
        public static readonly CommitRow[] R0 =
        [
            Commit("Other one", 0, 0, "2d4d79a", "2026-01-01 12:06", "feature/other"),
            Commit("Main more", 1, 1, "1dd58b8", "2026-01-01 12:05", "main", "origin/main"),
            Commit("Main work", 1, 1, "dd6a6fc", "2026-01-01 12:04"),
            Commit("Topic two", 2, 2, "b0dfa3f", "2026-01-01 12:03", Current("feature/topic"), "origin/feature/topic"),
            Commit("Topic one", 2, 2, "1219e45", "2026-01-01 12:02"),
            Commit("Add core", 0, 0, "996fdc1", "2026-01-01 12:01") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "7bce6bf", "2026-01-01 12:00"),
        ];

        /// <summary>R1, after "Rebase feature/topic onto main" (A2 part A): 3 lanes.</summary>
        public static readonly CommitRow[] R1 =
        [
            Commit("Topic two", 0, 0, "5f71ab8", "2026-01-01 12:03", Current("feature/topic")),
            Commit("Topic one", 0, 0, "befae26", "2026-01-01 12:02"),
            Commit("Other one", 1, 1, "2d4d79a", "2026-01-01 12:06", "feature/other"),
            Commit("Main more", 0, 0, "1dd58b8", "2026-01-01 12:05", "main", "origin/main"),
            Commit("Main work", 0, 0, "dd6a6fc", "2026-01-01 12:04"),
            Commit("Topic two", 2, 2, "b0dfa3f", "2026-01-01 12:03", "origin/feature/topic"),
            Commit("Topic one", 2, 2, "1219e45", "2026-01-01 12:02"),
            Commit("Add core", 0, 0, "996fdc1", "2026-01-01 12:01") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "7bce6bf", "2026-01-01 12:00"),
        ];

        /// <summary>R2, after "Rebase feature/topic onto this commit" on Main work (A2 part B): 3 lanes.</summary>
        public static readonly CommitRow[] R2 =
        [
            Commit("Topic two", 0, 0, "09b923a", "2026-01-01 12:03", Current("feature/topic")),
            Commit("Topic one", 0, 0, "c09318c", "2026-01-01 12:02"),
            Commit("Other one", 1, 1, "2d4d79a", "2026-01-01 12:06", "feature/other"),
            Commit("Main more", 2, 2, "1dd58b8", "2026-01-01 12:05", "main", "origin/main"),
            Commit("Main work", 0, 0, "dd6a6fc", "2026-01-01 12:04") with { Ends = [2] },
            Commit("Topic two", 2, 3, "b0dfa3f", "2026-01-01 12:03", "origin/feature/topic"),
            Commit("Topic one", 2, 3, "1219e45", "2026-01-01 12:02"),
            Commit("Add core", 0, 0, "996fdc1", "2026-01-01 12:01") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "7bce6bf", "2026-01-01 12:00"),
        ];

        /// <summary>A2 part B: "Main work" after the rebase, R2's row 4 (it was row 2), still the selected commit.</summary>
        public const int MainWorkRowInR2 = 4;

        /// <summary>R3, after "Check out feature/other and rebase it onto main" (A3): 2 lanes.</summary>
        public static readonly CommitRow[] R3 =
        [
            Commit("Other one", 0, 0, "14e607b", "2026-01-01 12:06", Current("feature/other")),
            Commit("Main more", 0, 0, "1dd58b8", "2026-01-01 12:05", "main", "origin/main"),
            Commit("Main work", 0, 0, "dd6a6fc", "2026-01-01 12:04"),
            Commit("Topic two", 1, 1, "b0dfa3f", "2026-01-01 12:03", "feature/topic", "origin/feature/topic"),
            Commit("Topic one", 1, 1, "1219e45", "2026-01-01 12:02"),
            Commit("Add core", 0, 0, "996fdc1", "2026-01-01 12:01") with { Ends = [1] },
            Commit("Initial commit", 0, 0, "7bce6bf", "2026-01-01 12:00"),
        ];

        /// <summary>
        /// R0's left panel when the repository opens, in part A's notation (a section <c>▾ Title N</c>,
        /// two spaces per level, a container with ▾ or ▸, the head branch with <c> (head)</c>, then
        /// <c> ↑N</c> and <c> ↓N</c>): the sections Local branches and Remotes. Pull requests 0,
        /// Tags 0 and Stashes 0 follow (<see cref="RebaseEmptySections"/>).
        /// </summary>
        public static readonly string[] R0LeftPanel =
        [
            "▾ Local branches 3",
            "  ▾ feature",
            "    other",
            "    topic (head)",
            "  main",
            "▾ Remotes 2",
            "  ▾ origin",
            "    ▸ feature",
            "    main",
        ];

        /// <summary>R1's left panel: <c>topic</c> ↑4 ↓2.</summary>
        public static readonly string[] R1LeftPanel = WithTopic("    topic (head) ↑4 ↓2");

        /// <summary>R2's left panel: <c>topic</c> ↑3 ↓2.</summary>
        public static readonly string[] R2LeftPanel = WithTopic("    topic (head) ↑3 ↓2");

        /// <summary>R3's left panel: <c>other</c> in the accent colour, no arrows; <c>topic</c> not accented, no arrows.</summary>
        public static readonly string[] R3LeftPanel =
        [
            "▾ Local branches 3",
            "  ▾ feature",
            "    other (head)",
            "    topic",
            "  main",
            "▾ Remotes 2",
            "  ▾ origin",
            "    ▸ feature",
            "    main",
        ];

        /// <summary>The sections below Remotes in every "rebase" and "sync" state of part A, with their counts.</summary>
        public static readonly (string Title, int Count)[] RebaseEmptySections = [("Pull requests", 0), ("Tags", 0), ("Stashes", 0)];

        // ---- The "sync" scenario as A4 changes it (Scenarios.RemoteSyncAsync). ----

        /// <summary>The tab's and the repository's name of the "sync" scenario.</summary>
        public const string SyncRepo = "sync";

        public const string SyncInitialCommitSha = "6688774dc2d9715638c7c8ef1c976c97ad44d0c5";

        public const string SharedWorkSha = "a7f2cfd9a076c4c746e13caaa60f4fa2f0d76a4c";

        public const string PublishedWorkSha = "8c6f545b9ecdb1ab1c2337214b735cdc5b381ce6";

        public const string UnpushedWorkSha = "8e653159ca5e460a187117d51b3f18bdf81f5c96";

        public const string TopicWorkSha = "77d782ebfce02f1fe2b29c2ee2b6b43a1f682a02";

        /// <summary>A4 part A, before the app starts: "Elsewhere work" pushed to origin's main (pinned).</summary>
        public const string ElsewhereWorkSha = "b2e9a9e3724a696d2565eaa632dc7b04bca768a0";

        /// <summary>A4 part A, before the app starts: "Local work" committed on main.</summary>
        public const string LocalWorkSha = "f4f8c060b1e68a3e560113b942f1f984c6b5d4f0";

        /// <summary>A4 part A: "Local work" rebased onto Elsewhere work by the pull.</summary>
        public const string LocalWorkRebasedSha = "8a1f07b0ca2c177d76be50610feaafb872a34863";

        /// <summary>A4 parts B and C, before the app starts: "Published elsewhere" pushed to origin's published.</summary>
        public const string PublishedElsewhereSha = "899ce97113fa266c09b5a8ab4025f6d78568c497";

        /// <summary>A4 part B: "Unpushed work" committed by Continue onto Published elsewhere.</summary>
        public const string UnpushedWorkContinuedSha = "cd8de5c655a6ad19814e84d96c6d83bb00a2c9eb";

        /// <summary>A4 part A's push from elsewhere: main, src/elsewhere.txt "elsewhere\n", "Elsewhere work" (12:05).</summary>
        public static readonly (string Branch, string File, string Content, string Message) ElsewhereWork = ("main", "src/elsewhere.txt", "elsewhere\n", "Elsewhere work");

        /// <summary>A4 part A's commit on main before the app starts: src/local.txt "local\n", "Local work" (12:06).</summary>
        public static readonly (string File, string Content, string Message) LocalWork = ("src/local.txt", "local\n", "Local work");

        /// <summary>A4 parts B and C's push from elsewhere: published, src/published.txt "published\nelsewhere\n", "Published elsewhere" (12:05).</summary>
        public static readonly (string Branch, string File, string Content, string Message) PublishedElsewhere = ("published", "src/published.txt", "published\nelsewhere\n", "Published elsewhere");

        /// <summary>A4 step 5: what is written to src/published.txt outside the app (three lines, LF).</summary>
        public const string PublishedResolved = "published\nelsewhere\nunpushed\n";

        /// <summary>A4 step 3: src/published.txt as the stopped rebase leaves it, markers included.</summary>
        public static readonly string[] PublishedConflicted =
            ["published", "<<<<<<< HEAD", "elsewhere", "=======", "unpushed", ">>>>>>> 8e65315 (Unpushed work)"];

        /// <summary>S1, A4 part A when the app starts: 3 lanes.</summary>
        public static readonly CommitRow[] S1 =
        [
            Commit("Local work", 0, 0, "f4f8c06", "2026-01-01 12:06", Current("main")),
            Commit("Topic work", 1, 1, "77d782e", "2026-01-01 12:04", "topic"),
            Commit("Unpushed work", 2, 2, "8e65315", "2026-01-01 12:03", "published"),
            Commit("Published work", 2, 2, "8c6f545", "2026-01-01 12:02", "origin/published"),
            Commit("Shared work", 0, 0, "a7f2cfd", "2026-01-01 12:01", "origin/main") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "6688774", "2026-01-01 12:00"),
        ];

        /// <summary>S2, after "Pull from origin/main (rebase)" (A4 part A): 3 lanes.</summary>
        public static readonly CommitRow[] S2 =
        [
            Commit("Local work", 0, 0, "8a1f07b", "2026-01-01 12:06", Current("main")),
            Commit("Elsewhere work", 0, 0, "b2e9a9e", "2026-01-01 12:05", "origin/main"),
            Commit("Topic work", 1, 1, "77d782e", "2026-01-01 12:04", "topic"),
            Commit("Unpushed work", 2, 2, "8e65315", "2026-01-01 12:03", "published"),
            Commit("Published work", 2, 2, "8c6f545", "2026-01-01 12:02", "origin/published"),
            Commit("Shared work", 0, 0, "a7f2cfd", "2026-01-01 12:01") with { Ends = [1, 2] },
            Commit("Initial commit", 0, 0, "6688774", "2026-01-01 12:00"),
        ];

        /// <summary>S3, A4 parts B and C when the app starts (HEAD on published): 2 lanes.</summary>
        public static readonly CommitRow[] S3 =
        [
            Commit("Topic work", 0, 0, "77d782e", "2026-01-01 12:04", "topic"),
            Commit("Unpushed work", 1, 1, "8e65315", "2026-01-01 12:03", Current("published")),
            Commit("Published work", 1, 1, "8c6f545", "2026-01-01 12:02", "origin/published"),
            Commit("Shared work", 0, 0, "a7f2cfd", "2026-01-01 12:01", "main", "origin/main") with { Ends = [1] },
            Commit("Initial commit", 0, 0, "6688774", "2026-01-01 12:00"),
        ];

        /// <summary>S4, A4 parts B and C while the pull's rebase is stopped: 3 lanes, the commit rows below the working-changes row.</summary>
        public static readonly CommitRow[] S4 =
        [
            Commit("Published elsewhere", 0, 0, "899ce97", "2026-01-01 12:05", "HEAD", "origin/published"),
            Commit("Topic work", 1, 1, "77d782e", "2026-01-01 12:04", "topic"),
            Commit("Unpushed work", 2, 2, "8e65315", "2026-01-01 12:03", "published"),
            Commit("Published work", 0, 0, "8c6f545", "2026-01-01 12:02") with { Ends = [2] },
            Commit("Shared work", 0, 0, "a7f2cfd", "2026-01-01 12:01", "main", "origin/main") with { Ends = [1] },
            Commit("Initial commit", 0, 0, "6688774", "2026-01-01 12:00"),
        ];

        /// <summary>S4's working-changes row: "Working changes · 1 conflict", its ring in lane 0, colour 0.</summary>
        public static readonly WorkingChangesRow S4Working = new("Working changes · 1 conflict", 0, 0);

        /// <summary>A4 step 5: the working-changes row once the file is staged (S4's rows below it).</summary>
        public static readonly WorkingChangesRow S4Resolved = new("Working changes", 0, 0);

        /// <summary>S5, after Continue (A4 part B): 2 lanes.</summary>
        public static readonly CommitRow[] S5 =
        [
            Commit("Unpushed work", 0, 0, "cd8de5c", "2026-01-01 12:03", Current("published")),
            Commit("Published elsewhere", 0, 0, "899ce97", "2026-01-01 12:05", "origin/published"),
            Commit("Topic work", 1, 1, "77d782e", "2026-01-01 12:04", "topic"),
            Commit("Published work", 0, 0, "8c6f545", "2026-01-01 12:02"),
            Commit("Shared work", 0, 0, "a7f2cfd", "2026-01-01 12:01", "main", "origin/main") with { Ends = [1] },
            Commit("Initial commit", 0, 0, "6688774", "2026-01-01 12:00"),
        ];

        /// <summary>S6, after Skip (A4 part C): 2 lanes.</summary>
        public static readonly CommitRow[] S6 =
        [
            Commit("Published elsewhere", 0, 0, "899ce97", "2026-01-01 12:05", Current("published"), "origin/published"),
            Commit("Topic work", 1, 1, "77d782e", "2026-01-01 12:04", "topic"),
            Commit("Published work", 0, 0, "8c6f545", "2026-01-01 12:02"),
            Commit("Shared work", 0, 0, "a7f2cfd", "2026-01-01 12:01", "main", "origin/main") with { Ends = [1] },
            Commit("Initial commit", 0, 0, "6688774", "2026-01-01 12:00"),
        ];

        /// <summary>S1's and S2's left panel (part A's notation): main (accent) ↑1, published ↑1, topic; origin open with main and published.</summary>
        public static readonly string[] S1LeftPanel = SyncLeftPanel("  main (head) ↑1", "  published ↑1", "  topic");

        /// <summary>S3's left panel: main (no arrows), published (accent) ↑1, topic.</summary>
        public static readonly string[] S3LeftPanel = SyncLeftPanel("  main", "  published (head) ↑1", "  topic");

        /// <summary>S4's left panel: no local branch accented (HEAD is detached); published ↑1 ↓1.</summary>
        public static readonly string[] S4LeftPanel = SyncLeftPanel("  main", "  published ↑1 ↓1", "  topic");

        /// <summary>S5's left panel: published (accent) ↑1.</summary>
        public static readonly string[] S5LeftPanel = SyncLeftPanel("  main", "  published (head) ↑1", "  topic");

        /// <summary>S6's left panel: published (accent), no arrows.</summary>
        public static readonly string[] S6LeftPanel = SyncLeftPanel("  main", "  published (head)", "  topic");

        /// <summary>A5's start: the working-changes row in Topic two's lane and colour, R0's rows one lower.</summary>
        public static readonly WorkingChangesRow A5Working = new("Working changes", 2, 2);

        // ---- Menus (A1, A3, A4; "Texts these checks rely on (part A)"). ----

        /// <summary>A1 step 1: the commit menu of "Main more" (not on HEAD), 284 high.</summary>
        public static readonly string[] MainMoreCommitMenu =
        [
            "1dd58b8 Main more",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto feature/topic",
            "Revert this commit",
            "Rebase feature/topic onto this commit",
            "Interactive rebase of feature/topic from here… (disabled: \"This commit is not on feature/topic\")",
            "Reset feature/topic to this commit…",
            "—",
            "Copy SHA",
        ];

        /// <summary>A2 step 4: the commit menu of "Main work" (A1 step 1's items with its header).</summary>
        public static readonly string[] MainWorkCommitMenu = ["dd6a6fc Main work", .. MainMoreCommitMenu.Skip(1)];

        /// <summary>A1 step 2: the commit menu of "Topic one" (on HEAD's linear line, depth 1).</summary>
        public static readonly string[] TopicOneCommitMenu =
        [
            "1219e45 Topic one",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto feature/topic (disabled: \"This commit is already on feature/topic\")",
            "Revert this commit",
            "Rebase feature/topic onto this commit (disabled: \"This commit is already on feature/topic\")",
            "Interactive rebase of feature/topic from here…",
            "Reset feature/topic to this commit…",
            "—",
            "Copy SHA",
        ];

        /// <summary>A1 step 3: the commit menu of "Topic two", HEAD's own commit.</summary>
        public static readonly string[] TopicTwoCommitMenu =
        [
            "b0dfa3f Topic two",
            "Check out this commit",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Cherry-pick onto feature/topic (disabled: \"This commit is already on feature/topic\")",
            "Revert this commit",
            "Rebase feature/topic onto this commit (disabled: \"This commit is already on feature/topic\")",
            "Interactive rebase of feature/topic from here… (disabled: \"feature/topic has no commits after this one\")",
            "Reset feature/topic to this commit…",
            "—",
            "Copy SHA",
        ];

        /// <summary>A1 step 4: the menu of the local branch main (with an upstream, not checked out), with phase 5's reflog, 493 high.</summary>
        public static readonly string[] MainBranchMenu =
        [
            "main",
            "Check out main",
            "Merge main into feature/topic",
            "Merge main into feature/topic (create a merge commit)",
            "Rebase feature/topic onto main",
            "Interactive rebase of feature/topic onto main…",
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
            "Show reflog of main",
            "—",
            "Copy branch name",
        ];

        /// <summary>A1 step 5: the menu of the remote branch origin/main, 293 high.</summary>
        public static readonly string[] OriginMainMenu =
        [
            "origin/main",
            "Check out origin/main",
            "Merge origin/main into feature/topic",
            "Merge origin/main into feature/topic (create a merge commit)",
            "Rebase feature/topic onto origin/main",
            "Interactive rebase of feature/topic onto origin/main…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Delete main on origin… (danger)",
            "—",
            "Copy branch name",
        ];

        /// <summary>A1 step 6: the menu of the remote branch origin/feature/topic, HEAD's upstream, 293 high.</summary>
        public static readonly string[] OriginFeatureTopicMenu =
        [
            "origin/feature/topic",
            "Check out origin/feature/topic (disabled: \"feature/topic is already checked out\")",
            "Merge origin/feature/topic into feature/topic",
            "Merge origin/feature/topic into feature/topic (create a merge commit)",
            "Rebase feature/topic onto origin/feature/topic (disabled: \"feature/topic is up to date with origin/feature/topic\")",
            "Interactive rebase of feature/topic onto origin/feature/topic…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Delete feature/topic on origin… (danger)",
            "—",
            "Copy branch name",
        ];

        /// <summary>A1 step 7: the menu of the current branch feature/topic with its upstream, with phase 5's reflog, 389 high.</summary>
        public static readonly string[] FeatureTopicMenu = CurrentBranchMenu("feature/topic", "origin/feature/topic");

        /// <summary>A4 step 2: the menu of the current branch main in "sync" (A1 step 7's for main), 389 high.</summary>
        public static readonly string[] SyncMainMenu = CurrentBranchMenu("main", "origin/main");

        /// <summary>A1 step 8: two commits that form a chain on HEAD's line ("Topic two", "Topic one"), 145 high.</summary>
        public static readonly string[] ChainMenu =
        [
            "2 commits",
            "Squash 2 commits…",
            "Cherry-pick 2 commits onto feature/topic (disabled: \"Some of these commits are already on feature/topic\")",
            "Revert 2 commits",
            "—",
            "Copy SHAs",
        ];

        /// <summary>A1 step 9: "Topic two" and "Add core", not a chain.</summary>
        public static readonly string[] NotAChainMenu =
        [
            "2 commits",
            "Squash 2 commits… (disabled: \"Select commits that follow one another\")",
            "Cherry-pick 2 commits onto feature/topic (disabled: \"Some of these commits are already on feature/topic\")",
            "Revert 2 commits",
            "—",
            "Copy SHAs",
        ];

        /// <summary>A1 step 10: "Main more" and "Main work", not on HEAD.</summary>
        public static readonly string[] NotOnHeadMenu =
        [
            "2 commits",
            "Squash 2 commits… (disabled: \"Only commits of feature/topic can be squashed\")",
            "Cherry-pick 2 commits onto feature/topic",
            "Revert 2 commits",
            "—",
            "Copy SHAs",
        ];

        /// <summary>A1 step 11: "Add core" and "Initial commit", the root among them.</summary>
        public static readonly string[] RootMenu =
        [
            "2 commits",
            "Squash 2 commits… (disabled: \"The first commit cannot be squashed\")",
            "Cherry-pick 2 commits onto feature/topic (disabled: \"Some of these commits are already on feature/topic\")",
            "Revert 2 commits",
            "—",
            "Copy SHAs",
        ];

        /// <summary>A1 step 9: the tooltip of the disabled squash the pointer rests on.</summary>
        public const string NotAChainReason = "Select commits that follow one another";

        /// <summary>A1 step 1: the tooltip of the disabled "from here" the pointer rests on.</summary>
        public const string NotOnTopicReason = "This commit is not on feature/topic";

        /// <summary>A1 step 2: the tooltip of the disabled rebase the pointer rests on.</summary>
        public const string AlreadyOnTopicReason = "This commit is already on feature/topic";

        /// <summary>A3 step 1: the drop of the current branch feature/topic onto main, 145 high.</summary>
        public static readonly string[] TopicOntoMainDropMenu =
        [
            "feature/topic → main",
            "Fast-forward main to feature/topic (disabled: \"main has commits that feature/topic does not have\")",
            "Check out main and merge feature/topic into it",
            "—",
            "Rebase feature/topic onto main",
            "Interactive rebase of feature/topic onto main…",
        ];

        /// <summary>A3 step 2: the drop of feature/topic onto the remote branch origin/main, 145 high.</summary>
        public static readonly string[] TopicOntoOriginMainDropMenu =
        [
            "feature/topic → origin/main",
            "Push feature/topic to origin/main",
            "Push feature/topic to origin/main and track it",
            "—",
            "Rebase feature/topic onto origin/main",
            "Interactive rebase of feature/topic onto origin/main…",
        ];

        /// <summary>A3 step 3: the drop of the remote branch origin/main onto the current branch: no rebase item, 110 high.</summary>
        public static readonly string[] OriginMainOntoTopicDropMenu =
        [
            "origin/main → feature/topic",
            "Merge origin/main into feature/topic",
            "Merge origin/main into feature/topic (create a merge commit)",
            "Fast-forward feature/topic to origin/main (disabled: \"feature/topic has commits that origin/main does not have\")",
        ];

        /// <summary>A3 step 4 (and A5 step 3): the drop of feature/other, not current, onto main, 145 high.</summary>
        public static readonly string[] OtherOntoMainDropMenu =
        [
            "feature/other → main",
            "Fast-forward main to feature/other (disabled: \"main has commits that feature/other does not have\")",
            "Check out main and merge feature/other into it",
            "—",
            "Check out feature/other and rebase it onto main",
            "Check out feature/other and rebase it onto main interactively…",
        ];

        /// <summary>A4 step 5: the conflicted file's menu in the unstaged list, with phase 5's File history and Blame, 345 high.</summary>
        public static readonly string[] ConflictedFileMenu =
        [
            "published.txt",
            "Resolve conflict",
            "Open in merge tool (disabled: \"Set merge.tool in git's configuration to use a merge tool\")",
            "Mark as resolved",
            "Discard changes… (disabled: \"Resolve the conflict first\")",
            "—",
            "Ignore published.txt (disabled: \"Only untracked files can be ignored\")",
            "Ignore all *.txt files (disabled: \"Only untracked files can be ignored\")",
            "—",
            "File history",
            "Blame",
            "—",
            "Open file",
            "Show in folder",
            "Copy path",
        ];

        /// <summary>The menus' heights the report states (4a's formula on the items above).</summary>
        public const double CommitMenuHeight = 284;

        public const double SeveralCommitsMenuHeight = 145;

        public const double LocalBranchWithUpstreamMenuHeight = 493;

        public const double CurrentBranchWithUpstreamMenuHeight = 389;

        public const double RemoteBranchMenuHeight = 293;

        public const double DropWithRebaseMenuHeight = 145;

        public const double DropOfRemoteBranchMenuHeight = 110;

        public const double ConflictedFileMenuHeight = 345;

        /// <summary>
        /// The drag of check A3 (and A5 step 3): 5 equal moves with the button held, the first more
        /// than 4 px from the press, as phase 3's check 7.
        /// </summary>
        public const int DragSteps = 5;

        /// <summary>
        /// A3 (no banner, no working-changes row, 1100×700): the centre of the label feature/other
        /// on row 0 in the window, where both harnesses press to drag it (measured by the scripted
        /// run, ±1).
        /// </summary>
        public static readonly (double X, double Y) OtherLabelCentre = (307.5, 129);

        /// <summary>A3: the centre of the label main on row 1, where both harnesses release.</summary>
        public static readonly (double X, double Y) MainLabelCentre = (284.9, 155);

        /// <summary>A3: the centre of the label feature/topic on row 3.</summary>
        public static readonly (double X, double Y) TopicLabelCentre = (313.2, 207);

        // ---- Operations, the status bar and the log (10.1, 10.2). ----

        public const string RebaseTopicOntoMainTitle = "Rebase feature/topic onto main";

        public const string RebasingTopicOntoMain = "Rebasing feature/topic onto main…";

        public const string RebasedTopicOntoMain = "Rebased feature/topic onto main";

        public const string RebaseTopicOntoMainWorkTitle = "Rebase feature/topic onto dd6a6fc";

        public const string RebasingTopicOntoMainWork = "Rebasing feature/topic onto dd6a6fc…";

        public const string RebasedTopicOntoMainWork = "Rebased feature/topic onto dd6a6fc";

        public const string CheckOutOtherAndRebaseTitle = "Check out feature/other and rebase it onto main";

        public const string CheckingOutOther = "Checking out feature/other…";

        public const string RebasingOtherOntoMain = "Rebasing feature/other onto main…";

        public const string RebasedOtherOntoMain = "Rebased feature/other onto main";

        public const string PullRebaseTitle = "Pull (rebase)";

        public const string PullingMain = "Pulling origin/main into main…";

        public const string PullingPublished = "Pulling origin/published into published…";

        public const string PulledAndRebasedMain = "Pulled and rebased main onto origin/main";

        public const string PullStopped = "Pull stopped: 1 file has conflicts";

        public const string ContinueTitle = "Continue the rebase";

        public const string ContinuingTheRebase = "Continuing the rebase…";

        public const string SkipTitle = "Skip 8e65315";

        public const string Skipping = "Skipping 8e65315…";

        public const string RebasedPublished = "Rebased published onto origin/published";

        public const string Ready = "Ready";

        /// <summary>A5: the status of a rebase git refused.</summary>
        public const string RebaseTopicOntoMainFailed = "Rebase feature/topic onto main failed";

        /// <summary>A5 step 3: the status of "Check out … and rebase it" the app refused.</summary>
        public const string CheckOutOtherAndRebaseFailed = "Check out feature/other and rebase it onto main failed";

        // ---- The banner, the stage panel and the toolbar of the pull's stop (A4, 5.2, 5.3, 10.3). ----

        public const string StoppedBanner = "Rebasing published onto origin/published: stopped at 8e65315 \"Unpushed work\" (1 of 1). 1 file has conflicts.";

        public const string ResolvedBanner = "Rebasing published onto origin/published: stopped at 8e65315 \"Unpushed work\" (1 of 1). Conflicts resolved: continue to commit it and go on.";

        public const string BannerToolTip = "Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts published back where it was before the rebase.";

        public const string AbortRebaseText = "Abort rebase";

        public const string ResolveConflictsFirstOneFile = "Resolve the conflicts first (1 file)";

        public const string ContinueRebaseText = "Continue rebase";

        public const string AmendDuringRebase = "Finish or abort the rebase first";

        public const string ConflictRowToolTip = "Conflict: click to resolve it";

        public const string StoppedUnstagedTitle = "Unstaged files (1, 1 with conflicts)";

        public const string NoStagedTitle = "Staged files (0)";

        public const string NoUnstagedTitle = "Unstaged files (0)";

        public const string OneStagedTitle = "Staged files (1)";

        public const string StoppedSummary = "Unpushed work";

        /// <summary>A4 step 3: the toolbar while the rebase is stopped (4d with D119): disabled buttons and their tooltips.</summary>
        public static readonly (string Button, string ToolTip)[] StoppedToolbarDisabled =
        [
            ("PullButton", "HEAD is detached: check out a branch to pull"),
            ("PushButton", "HEAD is detached: check out a branch to push"),
            ("StashButton", "Finish or abort the rebase first"),
            ("PopButton", "There are no stashes"),
        ];

        /// <summary>A4 step 3: the toolbar's buttons that stay enabled while the rebase is stopped.</summary>
        public static readonly string[] StoppedToolbarEnabled = ["FetchButton", "BranchButton"];

        /// <summary>A4 step 3: the status bar's branch while the rebase is stopped.</summary>
        public const string DetachedAtPublishedElsewhere = "Detached at 899ce97";

        public const string SkipQuestionTitle = "Skip 8e65315?";

        public const string SkipQuestion = "\"Unpushed work\" is left out of the rebase, and changes made to resolve its conflicts are lost.";

        public const string BackupDetail = "A backup is saved first, so this can be undone.";

        public const string NothingSelectedPlaceholder = "Select a commit to see its details.";

        // ---- The error dialogs of A5 (10.1 with 14.3). ----

        public const string RebaseFailedTitle = "Rebase failed";

        public const string DirtyTreeSummary = "Git rebases only a clean working tree: commit or stash your changes first.";

        public const string UntrackedInTheWaySummary = "Untracked files are in the way: move or remove them first.";

        /// <summary>A5 steps 2 and 4: the failed call as <c>ErrorCommand</c> shows it.</summary>
        public const string RebaseOntoMainCommand = "git rebase " + MainMoreSha;

        /// <summary>A2 part B: the rebase onto Main work.</summary>
        public const string RebaseOntoMainWorkCommand = "git rebase " + MainWorkSha;

        public static readonly string[] ButtonsWithOutput = ["Copy output", "Close"];

        public static readonly string[] CloseOnly = ["Close"];

        // ---- Git's texts (Git 2.36.0.windows.1; "key" lines asserted on every git). ----

        /// <summary>TA1's key line: <c>git rebase &lt;sha&gt;</c> refused for an unstaged change.</summary>
        public const string Ta1Key = "error: cannot rebase: You have unstaged changes.";

        /// <summary>TA1: standard error of <c>git rebase &lt;sha&gt;</c> with an unstaged change (exit 1; standard output empty).</summary>
        public static readonly string[] Ta1 = [Ta1Key, "error: Please commit or stash them."];

        /// <summary>TA2's key line: <c>git rebase &lt;sha&gt;</c> refused for an untracked file in the way.</summary>
        public const string Ta2Key = "error: The following untracked working tree files would be overwritten by checkout:";

        /// <summary>TA2: standard error of <c>git rebase &lt;sha&gt;</c> with docs/main.txt untracked (exit 1; standard output empty).</summary>
        public static readonly string[] Ta2 =
            [Ta2Key, "\tdocs/main.txt", "Please move or remove them before you switch branches.", "Aborting", "error: could not detach HEAD"];

        /// <summary>TA3: standard error of a clean <c>git rebase</c> of two commits, after the split at carriage returns and the removal of <c>ESC[K</c>.</summary>
        public static string[] Ta3(string branch, int commits) =>
        [
            .. Enumerable.Range(1, commits).Select(index => $"Rebasing ({index}/{commits})"),
            $"Successfully rebased and updated refs/heads/{branch}.",
        ];

        /// <summary>TA4: standard output of the pull's <c>git rebase</c> that stops (exit 1).</summary>
        public static readonly string[] Ta4Output = ["Auto-merging src/published.txt", "CONFLICT (content): Merge conflict in src/published.txt"];

        /// <summary>TA4: the first two lines of its standard error, before git's four <c>hint:</c> lines.</summary>
        public static readonly string[] Ta4ErrorStart = ["Rebasing (1/1)", "error: could not apply 8e65315... Unpushed work"];

        /// <summary>TA4: the last line of its standard error, after the hints.</summary>
        public const string Ta4ErrorEnd = "Could not apply 8e65315... Unpushed work";

        /// <summary>TA4: how many <c>hint:</c> lines git 2.36 prints between them.</summary>
        public const int Ta4Hints = 4;

        /// <summary>A4 part A: the last line of the pull's fetch.</summary>
        public const string FetchMainLine = "   a7f2cfd..b2e9a9e  main       -> origin/main";

        /// <summary>A4 parts B and C: the last line of the pull's fetch.</summary>
        public const string FetchPublishedLine = "   8c6f545..899ce97  published  -> origin/published";

        /// <summary>A3, A5: the call that reads <c>rebase.autoStash</c> before "Check out {A} and rebase it" (1.3 step 1).</summary>
        public const string AutoStashCommand = "git config --type=bool --get rebase.autoStash";

        public const string SwitchToOtherCommand = "git switch --no-guess feature/other";

        public const string FetchOriginCommand = "git fetch --progress origin";

        public const string RebaseWithoutArgumentsCommand = "git rebase";

        public const string AddPublishedCommand = "git add --all -- src/published.txt";

        public const string MarkerCheckCommand = "git diff --cached --check --no-color --no-ext-diff -- src/published.txt";

        public const string ContinueCommitCommand = "git commit --no-verify --cleanup=whitespace --file=-";

        public const string RebaseContinueCommand = "git rebase --continue";

        public const string RebaseSkipCommand = "git rebase --skip";

        /// <summary>A4: the pull's fast-forward test, <c>git merge-base --is-ancestor &lt;old tip&gt; FETCH_HEAD</c>.</summary>
        public static string IsAncestorOfFetchHead(string oldTip) => $"git merge-base --is-ancestor {oldTip} FETCH_HEAD";

        /// <summary>A4: the fork point, <c>git merge-base --fork-point &lt;upstream&gt; HEAD</c>.</summary>
        public static string ForkPoint(string upstream) => $"git merge-base --fork-point refs/remotes/{upstream} HEAD";

        /// <summary>A4: design 14.2's first count, the commits <c>git rebase</c> replays without the fork point.</summary>
        public static string CountAll(string upstream) => $"git rev-list --count --no-merges --right-only --cherry-pick refs/remotes/{upstream}...HEAD --";

        /// <summary>A4: design 14.2's second count, restricted to the commits after the fork point.</summary>
        public static string CountAfterForkPoint(string upstream, string fork) => $"git rev-list --count --no-merges --right-only --cherry-pick refs/remotes/{upstream}...HEAD ^{fork} --";

        /// <summary>A3: a drop's fact, <c>git merge-base --is-ancestor &lt;B&gt; &lt;A&gt;</c> (exit 1 for every drop of A3).</summary>
        public static string DropFact(string target, string source) => $"git merge-base --is-ancestor {target} {source}";

        // ---- Helpers. ----

        /// <summary>A row of the report's tables with the short SHA and date it gives, and no lane ending in its node unless <c>Ends</c> says so.</summary>
        public static CommitRow Commit(string message, int lane, int colour, string shortSha, string date, params string[] labels) =>
            new(message, lane, colour, labels, ShortSha: shortSha, Date: date, Ends: []);

        /// <summary>The menu of the current branch with an upstream (A1 step 7): "Pull from {up}", then "Pull from {up} (rebase)", phase 3's other groups and phase 5's reflog.</summary>
        public static string[] CurrentBranchMenu(string branch, string upstream) =>
        [
            branch,
            $"Pull from {upstream}",
            $"Pull from {upstream} (rebase)",
            "—",
            $"Push {branch} to {upstream}",
            $"Force push {branch}… (danger)",
            "—",
            "Create branch here…",
            "Create tag here…",
            $"Rename {branch}…",
            $"Delete {branch}… (disabled: \"You cannot delete the branch you are on\")",
            "—",
            "Change upstream…",
            $"Stop tracking {upstream}",
            "—",
            $"Show reflog of {branch}",
            "—",
            "Copy branch name",
        ];

        /// <summary>"sync"'s left panel with these lines for main, published and topic (Local branches 3; Remotes 2, origin open with main and published).</summary>
        private static string[] SyncLeftPanel(string main, string published, string topic) =>
            ["▾ Local branches 3", main, published, topic, "▾ Remotes 2", "  ▾ origin", "    main", "    published"];

        /// <summary>R0's left panel with feature/topic's row replaced by <paramref name="topic"/>.</summary>
        private static string[] WithTopic(string topic) => [.. R0LeftPanel.Select(line => line == "    topic (head)" ? topic : line)];

        /// <summary>A menu's items and separators as 4a's height formula counts them: the lines after the header, "—" a separator.</summary>
        public static (int Items, int Separators) MenuCounts(IReadOnlyList<string> menu)
        {
            ArgumentNullException.ThrowIfNull(menu);
            var separators = menu.Skip(1).Count(line => line == "—");
            return (menu.Count - 1 - separators, separators);
        }
    }
}
