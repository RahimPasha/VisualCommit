// Part D's expected results (docs/test-reports/phase-3.md, part D: the scenario graphs sync, auth,
// ops and edges, git's texts, the dialogs and status texts), copied from the report. Texts that
// show the server's port are written with 47391; a check asserts them through
// ServerScenario.WithPort. Only part D's writer edits this file; its members live in
// Phase3Expectations.PartD. Compiled by the real-window pass too: see Phase3Expectations.Common.cs
// for what may be used here.
using System.Globalization;

namespace VisualCommit.VisualTests.Phase3;

public static partial class Phase3Expectations
{
    /// <summary>What part D's checks (17 to 22, 27 to 31) compare against.</summary>
    public static class PartD
    {
        /// <summary>
        /// One row of a graph as the report lists it: the subject, the node's lane and colour
        /// number, the labels in phase 1's order (" ✓" after the branch HEAD is on), the date when
        /// the report gives it, whether it is a merge when the report says, and what the Branch /
        /// Tag column draws when the report names a "+N".
        /// </summary>
        public sealed record Row(string Message, int Lane, int Colour, IReadOnlyList<string> Labels, string? Date = null, bool? Merge = null, IReadOnlyList<string>? Drawn = null)
        {
            /// <summary>The row with the server's port in place of 47391 in its subject (the pull's merge subject).</summary>
            public Row WithPort(int port) => this with { Message = Phase3Expectations.WithPort(Message, port) };

            public override string ToString() => string.Create(
                CultureInfo.InvariantCulture,
                $"\"{Message}\" lane {Lane} colour {Colour}{(Merge == true ? " merge" : string.Empty)} [{string.Join(", ", Labels)}]{(Drawn is null ? string.Empty : " drawn [" + string.Join(", ", Drawn) + "]")} {Date}");
        }

        /// <summary>The text a dialog of git's output expects, the app's parts and git's lines (2.36's), for <c>ExpectedErrorDialog</c>.</summary>
        public sealed record ErrorTexts(string Title, string Summary, string Repository, string? Command, IReadOnlyList<string> Buttons, IReadOnlyList<string> Lines, string? KeyLine = null, bool OutputFromGit = true);

        // ---- The scenarios -------------------------------------------------------------------

        /// <summary>Check 17 step 1: the toolbar's buttons enabled on the auth scenario.</summary>
        public static readonly string[] EnabledOnAuth = ["FetchButton", "PullButton", "PushButton", "BranchButton"];

        /// <summary>"Scenario graphs": auth as it opens. One lane.</summary>
        public static readonly Row[] AuthGraph =
        [
            new("Initial commit", 0, 0, ["main ✓", "origin/main"], "2026-01-01 12:00", Drawn: ["main ✓", "+1"]),
        ];

        /// <summary>Auth after a fetch brought "Remote work" (check 17 after step 4, 17b step 4, 21 step 2). One lane.</summary>
        public static readonly Row[] AuthFetchedGraph =
        [
            new("Remote work", 0, 0, ["origin/main"], "2026-01-01 12:01"),
            new("Initial commit", 0, 0, ["main ✓"], "2026-01-01 12:00"),
        ];

        /// <summary>"Scenario graphs": sync as it opens. Two lanes.</summary>
        public static readonly Row[] SyncGraph =
        [
            new("Topic work", 0, 0, ["topic"], "2026-01-01 12:04"),
            new("Unpushed work", 1, 1, ["published"], "2026-01-01 12:03"),
            new("Published work", 1, 1, ["origin/published"], "2026-01-01 12:02"),
            new("Shared work", 0, 0, ["main ✓", "origin/main"], "2026-01-01 12:01", Drawn: ["main ✓", "+1"]),
            new("Initial commit", 0, 0, [], "2026-01-01 12:00"),
        ];

        /// <summary>"Scenario graphs": ops as it opens. Three lanes.</summary>
        public static readonly Row[] OpsGraph =
        [
            new("On main: Experiment", 0, 0, ["stash@{0}"], "2026-01-01 12:07"),
            new("Conflicting change", 1, 1, ["feature/conflict"], "2026-01-01 12:06"),
            new("Fast-forward me", 2, 2, ["feature/ff"], "2026-01-01 12:05"),
            new("Main work", 0, 0, ["main ✓", "origin/main", "v1.1"], "2026-01-01 12:04"),
            new("Alpha two", 2, 3, ["feature/alpha", "origin/feature/alpha"], "2026-01-01 12:03"),
            new("Alpha one", 2, 3, [], "2026-01-01 12:02"),
            new("Add core", 0, 0, ["v1.0"], "2026-01-01 12:01"),
            new("Initial commit", 0, 0, [], "2026-01-01 12:00"),
        ];

        /// <summary>Ops' rows of "Alpha two" and "Add core" (check 27).</summary>
        public const int AlphaTwoRow = 4;

        public const int AddCoreRow = 6;

        // ---- Check 17 ------------------------------------------------------------------------

        /// <summary>The menu of the remote <c>origin</c> under Remotes (check 17 step 2, 31 step 1).</summary>
        public static readonly string[] OriginMenu =
        [
            "origin", "Fetch origin", "—", "Change URL…", "Rename origin…", "Remove origin… (danger)", "—", "Hide every branch of origin", "—", "Copy URL", "Manage remotes…",
        ];

        public const string FetchingFromOrigin = "Fetching from origin…";

        public const string FetchingFromAll = "Fetching from all remotes…";

        public const string FetchOriginCancelled = "Fetch origin cancelled";

        public const string FetchedFromAll = "Fetched from all remotes";

        public const string FetchedFromOrigin = "Fetched from origin";

        /// <summary>The app's fetch of one remote: with no other remote, the toolbar's Fetch runs only this (D109).</summary>
        public const string FetchOriginCommand = "git fetch --progress origin";

        public const string AuthBehindOne = "## main...origin/main [behind 1]";

        // ---- Check 17b -----------------------------------------------------------------------

        public const string LockFile = ".git/refs/remotes/origin/main.lock";

        public static readonly ErrorTexts LockFilesDialog = new(
            "Fetch origin cancelled",
            "Git was stopped while it was updating references. These lock files were left behind and will make later git commands fail:",
            "auth",
            FetchOriginCommand,
            ["Copy output", "Remove lock files", "Close"],
            [LockFile],
            OutputFromGit: false);

        // ---- Check 18 ------------------------------------------------------------------------

        /// <summary>Sync with "Remote change" pushed from elsewhere and "Local change" committed (check 18 and 19 after step 1). Three lanes.</summary>
        public static readonly Row[] SyncLocalChangeGraph =
        [
            new("Local change", 0, 0, ["main ✓"], "2026-01-01 12:06"),
            new("Topic work", 1, 1, ["topic"], "2026-01-01 12:04"),
            new("Unpushed work", 2, 2, ["published"], "2026-01-01 12:03"),
            new("Published work", 2, 2, ["origin/published"], "2026-01-01 12:02"),
            new("Shared work", 0, 0, ["origin/main"], "2026-01-01 12:01"),
            new("Initial commit", 0, 0, []),
        ];

        public const string PullingMain = "Pulling origin/main into main…";

        public const string PullCancelled = "Pull cancelled";

        public const string PulledAndMerged = "Pulled and merged origin/main into main";

        public const string MergeSubject = "Merge branch 'main' of http://127.0.0.1:47391/origin";

        /// <summary>Check 18 after step 3 (and 19 after step 2): the pull's merge. Four lanes, so the Date column is hidden.</summary>
        public static readonly Row[] SyncPulledGraph =
        [
            new(MergeSubject, 0, 0, ["main ✓"], Merge: true),
            new("Local change", 0, 0, []),
            new("Remote change", 1, 1, ["origin/main"], "2026-01-01 12:05"),
            new("Topic work", 2, 2, ["topic"]),
            new("Unpushed work", 3, 3, ["published"]),
            new("Published work", 3, 3, ["origin/published"]),
            new("Shared work", 0, 0, []),
            new("Initial commit", 0, 0, []),
        ];

        /// <summary>Check 18 after step 4 (and 19 after step 3): the merge pushed. Four lanes.</summary>
        public static readonly Row[] SyncPushedGraph =
        [
            new(MergeSubject, 0, 0, ["main ✓", "origin/main"], Merge: true, Drawn: ["main ✓", "+1"]),
            new("Local change", 0, 0, []),
            new("Remote change", 1, 1, []),
            new("Topic work", 2, 2, ["topic"]),
            new("Unpushed work", 3, 3, ["published"]),
            new("Published work", 3, 3, ["origin/published"]),
            new("Shared work", 0, 0, []),
            new("Initial commit", 0, 0, []),
        ];

        public const string PushingMain = "Pushing main to origin/main…";

        public const string PushedMain = "Pushed main to origin/main";

        public const string PushMainCommand = "git push --progress origin refs/heads/main:refs/heads/main";

        public const string MergeFetchHeadCommand = "git merge --no-edit FETCH_HEAD";

        // ---- Check 18b -----------------------------------------------------------------------

        /// <summary>The menu of <c>main</c> under Remotes › origin while HEAD is on main (check 18b step 1), with phase 4's rebase items and phase 5's compare, hide and solo items; 380 high.</summary>
        public static readonly string[] RemoteMainMenu =
        [
            "origin/main",
            "Check out origin/main (disabled: \"main is already checked out\")",
            "Merge origin/main into main",
            "Merge origin/main into main (create a merge commit)",
            "Rebase main onto origin/main (disabled: \"main is up to date with origin/main\")",
            "Interactive rebase of main onto origin/main…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Delete main on origin… (danger)",
            "—",
            "Compare origin/main with main",
            "Hide origin/main",
            "Solo origin/main",
            "—",
            "Copy branch name",
        ];

        /// <summary>The menu of the current branch <c>main</c> under Local branches (check 18b step 1), with phase 4's pull with rebase and phase 5's hide and solo items; 415 high.</summary>
        public static readonly string[] CurrentMainMenu =
        [
            "main",
            "Pull from origin/main",
            "Pull from origin/main (rebase)",
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
            "Hide main (disabled: \"The checked-out branch is always shown\")",
            "Solo main",
            "—",
            "Copy branch name",
        ];

        public const string PulledFastForward = "Pulled 1 commit into main (fast-forward)";

        public const string AlreadyUpToDate = "main is already up to date";

        /// <summary>Check 18b after step 2: "Remote change" fast-forwarded. Three lanes.</summary>
        public static readonly Row[] SyncFastForwardedGraph =
        [
            new("Remote change", 0, 0, ["main ✓", "origin/main"], "2026-01-01 12:05", Drawn: ["main ✓", "+1"]),
            new("Topic work", 1, 1, ["topic"]),
            new("Unpushed work", 2, 2, ["published"]),
            new("Published work", 2, 2, ["origin/published"]),
            new("Shared work", 0, 0, []),
            new("Initial commit", 0, 0, []),
        ];

        public static readonly ErrorTexts PullRefusedDialog = new(
            "Pull failed",
            "Git could not pull origin/main into main.",
            "sync",
            "git merge --ff-only FETCH_HEAD",
            ["Copy output", "Close"],
            ["fatal: Not possible to fast-forward, aborting."],
            "fatal: Not possible to fast-forward, aborting.");

        public const string PullFailed = "Pull failed";

        /// <summary>Check 18b after step 4: fetched, refused. Three lanes.</summary>
        public static readonly Row[] SyncPullRefusedGraph =
        [
            new("Local change", 0, 0, ["main ✓"], "2026-01-01 12:07"),
            new("More remote work", 1, 1, ["origin/main"], "2026-01-01 12:06"),
            new("Remote change", 0, 0, []),
            new("Topic work", 1, 2, ["topic"]),
            new("Unpushed work", 2, 3, ["published"]),
            new("Published work", 2, 3, ["origin/published"]),
            new("Shared work", 0, 0, []),
            new("Initial commit", 0, 0, []),
        ];

        // ---- Check 19 ------------------------------------------------------------------------

        /// <summary>Git 2.36's eight lines of a push rejected because the remote has more (check 19), for the branch <paramref name="branch"/>.</summary>
        public static string[] PushRejectedLines(string branch) =>
        [
            "To http://127.0.0.1:47391/origin.git",
            $" ! [rejected]        {branch} -> {branch} (fetch first)",
            "error: failed to push some refs to 'http://127.0.0.1:47391/origin.git'",
            "hint: Updates were rejected because the remote contains work that you do",
            "hint: not have locally. This is usually caused by another repository pushing",
            "hint: to the same ref. You may want to first integrate the remote changes",
            "hint: (e.g., 'git pull ...') before pushing again.",
            "hint: See the 'Note about fast-forwards' in 'git push --help' for details.",
        ];

        public static readonly ErrorTexts PushRejectedMain = new(
            "Push rejected",
            "origin has commits that your main does not have. Pull first, then push again.",
            "sync",
            PushMainCommand,
            ["Copy output", "Pull", "Close"],
            PushRejectedLines("main"),
            " ! [rejected]        main -> main (fetch first)");

        public static readonly ErrorTexts PushRejectedPublished = new(
            "Push rejected",
            "origin has commits that your published does not have. Pull first, then push again.",
            "sync",
            "git push --progress origin refs/heads/published:refs/heads/published",
            ["Copy output", "Fetch origin", "Close"],
            PushRejectedLines("published"),
            " ! [rejected]        published -> published (fetch first)");

        public const string PushMainFailed = "Push main failed";

        public const string PushPublishedFailed = "Push published failed";

        /// <summary>Check 19 after step 5: "Published elsewhere" fetched. Five lanes, the Date column hidden.</summary>
        public static readonly Row[] SyncPublishedElsewhereGraph =
        [
            new(MergeSubject, 0, 0, ["main ✓", "origin/main"], Merge: true, Drawn: ["main ✓", "+1"]),
            new("Published elsewhere", 2, 2, ["origin/published"], "2026-01-01 12:07"),
            new("Local change", 0, 0, []),
            new("Remote change", 1, 1, []),
            new("Topic work", 3, 3, ["topic"]),
            new("Unpushed work", 4, 4, ["published"]),
            new("Published work", 2, 2, []),
            new("Shared work", 0, 0, []),
            new("Initial commit", 0, 0, []),
        ];

        // ---- Check 20 ------------------------------------------------------------------------

        /// <summary>Check 20 after step 1: published rewritten. Three lanes.</summary>
        public static readonly Row[] SyncRewrittenGraph =
        [
            new("Rewritten work", 0, 0, ["published"], "2026-01-01 12:05"),
            new("Topic work", 1, 1, ["topic"]),
            new("Published work", 2, 2, ["origin/published"]),
            new("Shared work", 0, 0, ["main ✓", "origin/main"], Drawn: ["main ✓", "+1"]),
            new("Initial commit", 0, 0, []),
        ];

        /// <summary>The menu of <c>published</c> when it has diverged from its upstream (check 20 step 2), with phase 4's rebase items and phase 5's compare, hide and solo items; 545 high.</summary>
        public static readonly string[] DivergedPublishedMenu =
        [
            "published",
            "Check out published",
            "Merge published into main",
            "Merge published into main (create a merge commit)",
            "Rebase main onto published",
            "Interactive rebase of main onto published…",
            "Fast-forward published to origin/published (disabled: \"published has commits that origin/published does not have\")",
            "—",
            "Push published to origin/published",
            "Force push published… (danger)",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename published…",
            "Delete published… (danger)",
            "—",
            "Change upstream…",
            "Stop tracking origin/published",
            "—",
            "Compare published with main",
            "Hide published",
            "Solo published",
            "—",
            "Copy branch name",
        ];

        public const string ForcePushTitle = "Force push published?";

        public const string ForcePushQuestion =
            "Replace published on origin with your published? Commits on origin that your branch does not have are dropped there. The push stops if origin has commits you have not integrated into your published, even ones the last fetch brought.";

        public const string BackupDetail = "A backup is saved first, so this can be undone.";

        public const string ForcePushedPublished = "Force-pushed published to origin/published";

        public const string ForcePushCommand = "git push --progress --force-with-lease=refs/heads/published --force-if-includes origin refs/heads/published:refs/heads/published";

        /// <summary>Check 20 after step 3. Two lanes.</summary>
        public static readonly Row[] SyncForcePushedGraph =
        [
            new("Rewritten work", 0, 0, ["published", "origin/published"]),
            new("Topic work", 1, 1, ["topic"]),
            new("Shared work", 0, 0, ["main ✓", "origin/main"], Drawn: ["main ✓", "+1"]),
            new("Initial commit", 0, 0, []),
        ];

        /// <summary>Check 20 after step 4. Two lanes.</summary>
        public static readonly Row[] SyncSomeoneElseGraph =
        [
            new("More rewritten work", 0, 0, ["published"], "2026-01-01 12:07"),
            new("Someone else's work", 1, 1, ["origin/published"], "2026-01-01 12:06"),
            new("Rewritten work", 0, 0, []),
            new("Topic work", 1, 2, ["topic"]),
            new("Shared work", 0, 0, ["main ✓", "origin/main"], Drawn: ["main ✓", "+1"]),
            new("Initial commit", 0, 0, []),
        ];

        public static readonly ErrorTexts ForcePushStoppedDialog = new(
            "Force push stopped",
            "origin/published has commits that your published does not include. Pull or look at them before forcing.",
            "sync",
            ForcePushCommand,
            ["Copy output", "Close"],
            [
                "To http://127.0.0.1:47391/origin.git",
                " ! [rejected]        published -> published (remote ref updated since checkout)",
                "error: failed to push some refs to 'http://127.0.0.1:47391/origin.git'",
                "hint: Updates were rejected because the tip of the remote-tracking",
                "hint: branch has been updated since the last checkout. You may want",
                "hint: to integrate those changes locally (e.g., 'git pull ...')",
                "hint: before forcing an update.",
            ],
            " ! [rejected]        published -> published (remote ref updated since checkout)");

        public const string ForcePushPublishedFailed = "Force push published failed";

        // ---- Check 21 ------------------------------------------------------------------------

        public const string CredentialsTitle = "Sign in to 127.0.0.1:47391";

        public const string CredentialsText = "Git needs a user name and a password or token for http://127.0.0.1:47391.";

        public const string FetchCancelled = "Fetch cancelled";

        public const string FetchOriginFailed = "Fetch origin failed";

        public static readonly ErrorTexts WrongPasswordDialog = new(
            "Fetch failed",
            "Git could not fetch from origin.",
            "auth",
            FetchOriginCommand,
            ["Copy output", "Close"],
            ["remote: Authentication required.", "fatal: Authentication failed for 'http://127.0.0.1:47391/remote.git/'"],
            "fatal: Authentication failed for 'http://127.0.0.1:47391/remote.git/'");

        // ---- Check 21b -----------------------------------------------------------------------

        public const string PassphraseTitle = "Key passphrase";

        public const string PassphraseText = "Enter the passphrase for /home/u/.ssh/id_ed25519.";

        public const string FetchingFromFakeSsh = "Fetching from fake-ssh…";

        public const string HostKeyTitle = "Unknown host";

        /// <summary>ssh's four lines as the dialog shows them, the last without its trailing space.</summary>
        public const string HostKeyText =
            "The authenticity of host '[127.0.0.1]:47392 ([127.0.0.1]:47392)' can't be established.\n" +
            "ED25519 key fingerprint is SHA256:skokdAxs76eWz/jm50ga0Qz+ewMVrjE4KE91GAFIgTE.\n" +
            "This key is not known by any other names\n" +
            "Are you sure you want to continue connecting (yes/no/[fingerprint])?";

        public const string HostKeyQuestion = "Trust this host and connect?";

        public const string PromptTitle = "Git asks";

        public const string PinText = "Enter PIN for ED25519-SK key /home/u/.ssh/id_ed25519_sk:";

        public static readonly ErrorTexts FakeSshFailedDialog = new(
            "Fetch failed",
            "Git could not fetch from fake-ssh.",
            "auth",
            "git fetch --progress fake-ssh",
            ["Copy output", "Close"],
            [
                "git@example.invalid: Permission denied (publickey).",
                "fatal: Could not read from remote repository.",
                "Please make sure you have the correct access rights",
                "and the repository exists.",
            ],
            "fatal: Could not read from remote repository.");

        public const string FetchFakeSshFailed = "Fetch fake-ssh failed";

        public const string VerificationPrompt = "Verification code: ";

        public const string VerificationText = "Verification code:";

        public const string ConfirmPromptText =
            "Allow use of key /home/u/.ssh/id_ed25519?\nKey fingerprint SHA256:skokdAxs76eWz/jm50ga0Qz+ewMVrjE4KE91GAFIgTE.";

        public const string SshAsksTitle = "ssh asks";

        // ---- Check 22 ------------------------------------------------------------------------

        public static readonly string[] RemotesHeaderMenu = ["Remotes", "Add remote…", "Fetch all remotes", "Manage remotes…", "Show all hidden branches (disabled: \"No branch is hidden\")", "Stop soloing (disabled: \"No branch is soloed\")"];

        public const string ServerUrl = "http://127.0.0.1:47391/origin.git";

        public const string FetchedFromMirror = "Fetched from mirror";

        public const string TokenUrl = "https://alice:ghp_SECRET123@127.0.0.1:1/x.git";

        public const string HiddenTokenUrl = "https://***@127.0.0.1:1/x.git";

        public const string Token = "ghp_SECRET123";

        public const string ChangedUrlOfMirror = "Changed the URL of mirror";

        public const string RenamedMirror = "Renamed remote mirror to backup";

        public const string RemoveBackupTitle = "Remove remote backup?";

        public const string RemoveBackupQuestion =
            "Remove the remote backup and its 2 remote branches? Your local branches stay; those that track it lose their upstream. Its URL was https://***@127.0.0.1:1/x.git.";

        public const string RemovedBackup = "Removed remote backup";

        /// <summary>Sync with <c>mirror</c> added and fetched (check 22 after step 2). Two lanes.</summary>
        public static readonly Row[] SyncMirrorGraph =
        [
            new("Topic work", 0, 0, ["topic"]),
            new("Unpushed work", 1, 1, ["published"]),
            new("Published work", 1, 1, ["mirror/published", "origin/published"]),
            new("Shared work", 0, 0, ["main ✓", "mirror/main", "origin/main"]),
            new("Initial commit", 0, 0, []),
        ];

        // ---- Check 27 ------------------------------------------------------------------------

        /// <summary>The commit menu of "Alpha two" (check 27 step 1): phase 4's OC1, 9 items, 2 separators, 284 high.</summary>
        public static readonly string[] AlphaTwoMenu =
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

        /// <summary>Check 27 step 1: 284 high (232 before phase 4).</summary>
        public const double AlphaTwoMenuHeight = 284;

        /// <summary>Check 27 step 3: the banner's Continue while the merge's one file conflicts (phase 4's design 10.4).</summary>
        public const string ResolveConflictsFirst = "Resolve the conflicts first (1 file)";

        public const string ResetTitle = "Reset main to 8beca34?";

        public const string ResetText = "Move main to \"Add core\" (8beca34).";

        public static readonly (string Mode, string Text)[] ResetModes =
        [
            ("Soft", "Keep every change; the changes of the commits after it are staged."),
            ("Mixed", "Keep every change; nothing is staged."),
            ("Hard", "Discard every change to tracked files."),
        ];

        public const string MergeStopped = "Merge stopped: 1 file has conflicts";

        public const string MergeBanner = "Merging: \"Merge branch 'feature/conflict'\". 1 file has conflicts.";

        public const string AbortMerge = "Abort merge";

        public const string WorkingRowConflict = "Working changes · 1 conflict";

        public static readonly ErrorTexts CheckoutDuringMergeDialog = new(
            "Checkout failed",
            "Git could not check out feature/alpha.",
            "ops",
            "git switch --no-guess feature/alpha",
            ["Copy output", "Close"],
            ["fatal: cannot switch branch while merging", "Consider \"git merge --quit\" or \"git worktree add\"."],
            "fatal: cannot switch branch while merging");

        public const string CheckoutAlphaFailed = "Check out feature/alpha failed";

        // ---- Check 28 ------------------------------------------------------------------------

        /// <summary>The menu of <c>topic</c>, which has no upstream (check 28 step 1), with phase 4's rebase items and phase 5's compare, hide and solo items; 467 high.</summary>
        public static readonly string[] TopicMenu =
        [
            "topic",
            "Check out topic",
            "Merge topic into main",
            "Merge topic into main (create a merge commit)",
            "Rebase main onto topic",
            "Interactive rebase of main onto topic…",
            "—",
            "Push topic…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename topic…",
            "Delete topic… (danger)",
            "—",
            "Set upstream…",
            "—",
            "Compare topic with main",
            "Hide topic",
            "Solo topic",
            "—",
            "Copy branch name",
        ];

        /// <summary>The menu of <c>topic</c> tracking origin/published (check 28 step 4), with phase 4's rebase items and phase 5's compare, hide and solo items; 545 high.</summary>
        public static readonly string[] TopicTrackingMenu =
        [
            "topic",
            "Check out topic",
            "Merge topic into main",
            "Merge topic into main (create a merge commit)",
            "Rebase main onto topic",
            "Interactive rebase of main onto topic…",
            "Fast-forward topic to origin/published (disabled: \"topic has commits that origin/published does not have\")",
            "—",
            "Push topic to origin/published",
            "Force push topic… (danger)",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename topic…",
            "Delete topic… (danger)",
            "—",
            "Change upstream…",
            "Stop tracking origin/published",
            "—",
            "Compare topic with main",
            "Hide topic",
            "Solo topic",
            "—",
            "Copy branch name",
        ];

        public const string PickTitle = "Set upstream for topic";

        public static readonly string[] UpstreamCandidates = ["origin/main", "origin/published"];

        public const string TracksPublished = "topic now tracks origin/published";

        public const string TracksMain = "topic now tracks origin/main";

        public const string StoppedTracking = "topic no longer tracks origin/main";

        // ---- Check 29 ------------------------------------------------------------------------

        public const string PushTopicTitle = "Push topic";

        public const string PushedTopic = "Pushed topic to origin/topic, and set it as upstream";

        public const string PushTopicCommand = "git push --progress --set-upstream origin refs/heads/topic:refs/heads/topic";

        /// <summary>The menu of the tag <c>v1.0</c> (check 29 steps 3 and 5).</summary>
        public static readonly string[] TagMenu =
        [
            "v1.0", "Check out v1.0", "Create branch here…", "—", "Push v1.0…", "Delete v1.0… (danger)", "Delete v1.0 on a remote… (danger)", "—", "Compare v1.0 with main", "—", "Copy tag name",
        ];

        public const string PushTagTitle = "Push v1.0 to…";

        public const string PushedTag = "Pushed tag v1.0 to origin";

        public const string PushTagCommand = "git push --progress origin refs/tags/v1.0:refs/tags/v1.0";

        public const string DeleteTagChoiceTitle = "Delete v1.0 on…";

        public const string DeleteTagTitle = "Delete v1.0 on origin?";

        public const string DeleteTagQuestion = "Delete the tag v1.0 on origin? Everyone who uses origin loses it.";

        public const string DeletedTag = "Deleted tag v1.0 on origin";

        // ---- Check 30 ------------------------------------------------------------------------

        public const string PushedPublished = "Pushed published to origin/published";

        /// <summary>The menu of <c>published</c> behind its upstream (check 30 step 3), with phase 4's rebase items and phase 5's compare, hide and solo items; 545 high.</summary>
        public static readonly string[] BehindPublishedMenu =
        [
            "published",
            "Check out published",
            "Merge published into main",
            "Merge published into main (create a merge commit)",
            "Rebase main onto published",
            "Interactive rebase of main onto published…",
            "Fast-forward published to origin/published",
            "—",
            "Push published to origin/published",
            "Force push published… (danger)",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename published…",
            "Delete published… (danger)",
            "—",
            "Change upstream…",
            "Stop tracking origin/published",
            "—",
            "Compare published with main",
            "Hide published",
            "Solo published",
            "—",
            "Copy branch name",
        ];

        public const string FastForwarded = "Fast-forwarded published to origin/published";

        /// <summary>Check 30 after step 3. Two lanes.</summary>
        public static readonly Row[] SyncFastForwardedPublishedGraph =
        [
            new("Published elsewhere", 0, 0, ["published", "origin/published"]),
            new("Topic work", 1, 1, ["topic"]),
            new("Unpushed work", 0, 0, []),
            new("Published work", 0, 0, []),
            new("Shared work", 0, 0, ["main ✓", "origin/main"], Drawn: ["main ✓", "+1"]),
            new("Initial commit", 0, 0, []),
        ];

        /// <summary>The menu of <c>published</c> under Remotes › origin (check 30 step 4), with phase 4's rebase items and phase 5's compare, hide and solo items; 380 high.</summary>
        public static readonly string[] RemotePublishedMenu =
        [
            "origin/published",
            "Check out origin/published",
            "Merge origin/published into main",
            "Merge origin/published into main (create a merge commit)",
            "Rebase main onto origin/published",
            "Interactive rebase of main onto origin/published…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Delete published on origin… (danger)",
            "—",
            "Compare origin/published with main",
            "Hide origin/published",
            "Solo origin/published",
            "—",
            "Copy branch name",
        ];

        public const string DeleteBranchTitle = "Delete published on origin?";

        public const string DeleteBranchQuestion = "Delete the branch published on origin? Everyone who uses origin loses it.";

        public const string DeletedBranch = "Deleted published on origin";

        /// <summary>The menu of <c>published</c> after its upstream was deleted (check 30 step 5), with phase 4's rebase items and phase 5's compare, hide and solo items; 545 high.</summary>
        public static readonly string[] GonePublishedMenu =
        [
            "published",
            "Check out published",
            "Merge published into main",
            "Merge published into main (create a merge commit)",
            "Rebase main onto published",
            "Interactive rebase of main onto published…",
            "Fast-forward published to origin/published (disabled: \"origin/published no longer exists\")",
            "—",
            "Push published to origin/published",
            "Force push published… (disabled: \"origin/published no longer exists: push normally\")",
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename published…",
            "Delete published… (danger)",
            "—",
            "Change upstream…",
            "Stop tracking origin/published",
            "—",
            "Compare published with main",
            "Hide published",
            "Solo published",
            "—",
            "Copy branch name",
        ];

        public const string GoneForcePushTip = "origin/published no longer exists: push normally";

        /// <summary>
        /// The drop menu of main onto side, side checked out in the worktree at
        /// <paramref name="worktree"/> (check 30 step 6), with phase 4's group "Check out main and
        /// rebase it": the rebase disabled because main already contains side, the interactive one
        /// the only enabled item (a detached HEAD does not disable the items that check a branch out
        /// first, phase 4's design 14.3), with phase 5's compare; 180 high.
        /// </summary>
        public static string[] WorktreeDropMenu(string worktree) =>
        [
            "main → side",
            $"Fast-forward side to main (disabled: \"side is checked out in another worktree ({worktree})\")",
            $"Check out side and merge main into it (disabled: \"side is checked out in another worktree ({worktree})\")",
            "—",
            "Check out main and rebase it onto side (disabled: \"main is up to date with side\")",
            "Check out main and rebase it onto side interactively…",
            "—",
            "Compare main with side",
        ];

        public static string WorktreeReason(string worktree) => $"side is checked out in another worktree ({worktree})";

        // ---- Check 31 ------------------------------------------------------------------------

        public const string FinishingTitle = "Finishing operations";

        public const string FinishingText = "VisualCommit closes when these operations end.";

        public const string FinishingItem = "Fetch origin — auth";

        public const string OneWaiting = "1 waiting";
    }
}
