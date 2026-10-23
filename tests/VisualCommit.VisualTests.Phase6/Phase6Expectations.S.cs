// Part S's expected results (docs/test-reports/phase-6.md, "Part S: the settings screen and its
// effects" and the rows S14 to S21 of "Checks"), copied from the report: the "settings" scenario's
// SHAs, files and graphs (ST0 to STR), the session's global file and .git/config through S18 to
// S20, the screen's reads and git's answers (SD29), the commits part S makes, the texts design 14
// gives the screen, its Pick dialogs and its Keyboard shortcuts list, the operations' texts and
// the log lines, and the window's numbers (design 7.1, SD2). Git's own texts are Git
// 2.36.0.windows.1's, asserted word for word only on that git (phase 3's rule). Only part S's
// writer edits this file; its members live in Phase6Expectations.PartS. Compiled by the
// real-window pass too: see Phase6Expectations.Common.cs for what may be used here (no type of the
// walk-through's own files, no App type).
using Avalonia;
using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase6;

public static partial class Phase6Expectations
{
    /// <summary>What part S's checks (S14 to S21) compare against.</summary>
    public static class PartS
    {
        // ---- "settings" (Scenarios.GitSettingsAsync, scenario facts 5) ----

        /// <summary>The tab's and the repository's name of the "settings" scenario.</summary>
        public const string Settings = "settings";

        /// <summary>The branch HEAD is on in "settings".</summary>
        public const string Main = "main";

        public const string InitialCommitSha = "9f9a01440ea7e170d173fb8878837ec545ec8b7c";

        public const string RemoteWorkSha = "e0cbfafef6677bc3e79b2a79650eea4c1675c3e0";

        public const string LocalWorkSha = "73f8cadf9ea2ae5e574a3e047d1a8fc4af933b98";

        /// <summary>S18 step 6's "Add a third note" as Ann Example (the global identity), D72's dates.</summary>
        public const string GlobalCommitSha = "3e519fba64d22688690e5920cc3300e3c757e5d9";

        /// <summary>S18 step 9's "Add a third note" as Bo Local (the repository's identity), on a fresh copy.</summary>
        public const string RepositoryCommitSha = "ead110cb6ae86125d9789cf40d64fb1608f55612";

        /// <summary>The tree of both third-note commits (SD5), abbreviated.</summary>
        public const string ThirdNoteTree = "d8e00c9";

        /// <summary>S19 step 6's rebased Local work: author 12:02 kept, committer Test Author 2026-01-02 09:00.</summary>
        public const string RebasedSha = "b4253bb077f7b07e212ac8f16861a728072f58b5";

        /// <summary>The working tree of a fresh "settings" copy, in git's order.</summary>
        public static IReadOnlyList<string> WorkingTreeFiles { get; } = ["README.md", "local.txt", "notes.txt", "run.py"];

        public const string ReadmeText = "# Settings\n";

        public const string NotesText = "one\ntwo\n";

        public const string RunPyText = "print(\"run\")\n";

        /// <summary><c>notes.txt</c> as S16, S17 and S18 write it before the start (SD5).</summary>
        public const string NotesThreeLines = "one\ntwo\nthree\n";

        /// <summary><c>draft.txt</c> as S16 and S17 write it before the start (untracked).</summary>
        public const string DraftText = "draft\n";

        public const string NotesFile = "notes.txt";

        public const string DraftFile = "draft.txt";

        public const string RunPyFile = "run.py";

        /// <summary><c>git status --porcelain=v1</c> after S17's set-up: staged notes.txt and the untracked draft.txt.</summary>
        public static IReadOnlyList<string> S17Status { get; } = ["M  notes.txt", "?? draft.txt"];

        /// <summary>The copy's <c>.git/config</c> on Windows as <c>LeanRepositories</c> writes it (part S's scenarios; the <c>core</c> lines differ elsewhere).</summary>
        public const string SettingsConfigOnWindows =
            "[core]\n\trepositoryformatversion = 0\n\tfilemode = false\n\tbare = false\n\tlogallrefupdates = true\n\tsymlinks = false\n\tignorecase = true\n\tautocrlf = false\n"
            + "[commit]\n\tgpgsign = false\n[gc]\n\tauto = 0\n[maintenance]\n\tauto = false\n"
            + SettingsConfigRemoteAndBranch;

        /// <summary>The copy's <c>.git/config</c> from <c>[remote "origin"]</c> on, on every platform.</summary>
        public const string SettingsConfigRemoteAndBranch =
            "[remote \"origin\"]\n\turl = ../remotes/settings.git\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n" + BranchMainSection;

        /// <summary><c>[branch "main"]</c>'s two lines, the last of the copy's <c>.git/config</c>.</summary>
        public const string BranchMainSection = "[branch \"main\"]\n\tremote = origin\n\tmerge = refs/heads/main\n";

        /// <summary>What S18 step 7 appends to <c>.git/config</c>: the repository's identity.</summary>
        public const string LocalIdentitySection = "[user]\n\tname = Bo Local\n\temail = bo@example.org\n";

        /// <summary>What S19 step 4 appends (and parts B and C write before the start): both pull keys.</summary>
        public const string LocalPullSection = "[pull]\n\trebase = true\n\tff = true\n";

        /// <summary>The line S19 step 8 adds to <c>[branch "main"]</c>, outside the app, after its <c>merge</c>.</summary>
        public const string BranchRebaseLine = "\trebase = false\n";

        /// <summary>What S19 step 10 appends outside the app.</summary>
        public const string LocalPreserveSection = "[pull]\n\trebase = preserve\n";

        // ---- The session's global file (part S's scenarios, SD3) ----

        /// <summary>S14's and S18 part A's seed (SD3).</summary>
        public const string SeedConfigOnly = "[user]\n\tuseConfigOnly = true\n";

        /// <summary>The file S18 step 4 writes, and S18 part B's seed: the same bytes.</summary>
        public const string SeedAnnExample = "[user]\n\tuseConfigOnly = true\n\tname = Ann Example\n\temail = ann@example.com\n";

        /// <summary>S19's seed (all three parts; never written).</summary>
        public const string SeedPullFastForwardOnly = "[user]\n\tname = Test Author\n\temail = author@example.com\n[pull]\n\tff = only\n";

        /// <summary>S20's seed: a tool of one's own that does nothing (17.1).</summary>
        public const string SeedSlowTool = "[mergetool \"slow\"]\n\tcmd = true\n";

        /// <summary>The file after S20 step 3's <c>--replace-all merge.tool slow</c>.</summary>
        public const string SlowToolChosen = "[mergetool \"slow\"]\n\tcmd = true\n[merge]\n\ttool = slow\n";

        // ---- The identities (SD4) ----

        public const string AnnName = "Ann Example";

        public const string AnnEmail = "ann@example.com";

        public const string BoName = "Bo Local";

        public const string BoEmail = "bo@example.org";

        /// <summary>S18 step 3's name, which the screen refuses.</summary>
        public const string RefusedName = "Ann <Example>";

        /// <summary>S18 step 3's email, which the screen warns about.</summary>
        public const string WarnedEmail = "ann.example.com";

        public const string AnnIdent = "Ann Example <ann@example.com>";

        public const string BoIdent = "Bo Local <bo@example.org>";

        public const string TestAuthorIdent = "Test Author <author@example.com>";

        /// <summary><c>1767344400 +0000</c>: 2026-01-02 09:00 UTC, D72's date, as <c>git var</c> prints it.</summary>
        public const string IdentDate = "1767344400 +0000";

        /// <summary>S14's and S18's name typed per scope (step 6).</summary>
        public const string LocalName = "Local Name";

        public const string GlobalName = "Global Name";

        /// <summary>The summary of S18's commits (SD5).</summary>
        public const string ThirdNote = "Add a third note";

        /// <summary>
        /// <c>git var GIT_AUTHOR_IDENT</c>'s standard error with the seed <c>user.useConfigOnly = true</c>
        /// and nothing else (part S's "The identity reads", 2.36 and 2.55), exit 128; the committer's
        /// starts <c>Committer identity unknown</c>.
        /// </summary>
        public static IReadOnlyList<string> IdentityUnknown(string who) =>
        [
            $"{who} identity unknown",
            string.Empty,
            "*** Please tell me who you are.",
            string.Empty,
            "Run",
            string.Empty,
            "  git config --global user.email \"you@example.com\"",
            "  git config --global user.name \"Your Name\"",
            string.Empty,
            "to set your account's default identity.",
            "Omit --global to set the identity only in this repository.",
            string.Empty,
            NoEmailGiven,
        ];

        /// <summary>The refusal's last line (2.36 and 2.55): it names no machine.</summary>
        public const string NoEmailGiven = "fatal: no email was given and auto-detection is disabled";

        // ---- The commands (the call log's CommandText) ----

        /// <summary>8.1's read, outside (in the data folder) and in the repository: the same command.</summary>
        public const string ConfigListCommand = "git config --show-scope --show-origin -z --list";

        public const string VarAuthorCommand = "git var GIT_AUTHOR_IDENT";

        public const string VarCommitterCommand = "git var GIT_COMMITTER_IDENT";

        public const string GetPullFfCommand = "git config --get pull.ff";

        public const string BoolPullFfCommand = "git config --type=bool --get pull.ff";

        public const string GetBranchRebaseCommand = "git config --get branch.main.rebase";

        public const string GetPullRebaseCommand = "git config --get pull.rebase";

        public const string BoolPullRebaseCommand = "git config --type=bool --get pull.rebase";

        public const string GetMergeToolCommand = "git config --get merge.tool";

        public const string MergeToolHelpCommand = "git mergetool --tool-help";

        public const string WriteGlobalAnnName = "git config --global --replace-all user.name \"Ann Example\"";

        public const string WriteGlobalAnnEmail = "git config --global --replace-all user.email ann@example.com";

        public const string WriteLocalBoName = "git config --local --replace-all user.name \"Bo Local\"";

        public const string WriteLocalBoEmail = "git config --local --replace-all user.email bo@example.org";

        public const string WriteLocalPullRebase = "git config --local --replace-all pull.rebase true";

        public const string WriteLocalPullFf = "git config --local --replace-all pull.ff true";

        public const string UnsetLocalPullRebase = "git config --local --unset-all pull.rebase";

        public const string UnsetLocalPullFf = "git config --local --unset-all pull.ff";

        public const string UnsetLocalBranchRebase = "git config --local --unset-all branch.main.rebase";

        public const string WriteGlobalSlow = "git config --global --replace-all merge.tool slow";

        public const string UnsetGlobalMergeTool = "git config --global --unset-all merge.tool";

        /// <summary>A write's marks (part S's "The screen's reads": "No write" means no call with either).</summary>
        public static IReadOnlyList<string> WriteMarks { get; } = ["--replace-all", "--unset-all"];

        public const string CommitCommand = "git commit --file=- --cleanup=whitespace";

        public const string FetchOriginCommand = "git fetch --progress origin";

        public const string AddedFilesCommand = "git diff-tree -r -z --no-renames --name-only --diff-filter=A HEAD FETCH_HEAD --";

        /// <summary>The added-files read's output: <c>remote.txt</c>, not on disk, so no <c>check-ignore</c> (9.3).</summary>
        public const string AddedFilesOutput = "remote.txt\0";

        public const string FastForwardOnlyCommand = "git merge --ff-only --no-overwrite-ignore FETCH_HEAD";

        public const string IsAncestorCommand = "git merge-base --is-ancestor " + LocalWorkSha + " FETCH_HEAD";

        public const string UpdateRefCommand = "git update-ref --stdin";

        /// <summary>The rebase's backup (the fixed backup clock's first name, design 2.3 of phase 5).</summary>
        public const string RebaseBackupRef = "refs/visualcommit/backup/rebase-20260102T090001000Z/main";

        public const string RebaseBackupInput = "create " + RebaseBackupRef + " " + LocalWorkSha;

        public const string RebaseCommand = "git rebase";

        /// <summary>The prefix no call of a pull without a merge starts with (S19's "no call starting <c>git merge </c>").</summary>
        public const string MergePrefix = "git merge ";

        /// <summary><c>FETCH_HEAD</c> after S19 step 2's fetch: <c>e0cbfaf…⇥⇥branch 'main' of ../remotes/settings</c>.</summary>
        public const string FetchHeadLine = RemoteWorkSha + "\t\tbranch 'main' of ../remotes/settings";

        /// <summary>HEAD's reflog after S19's rebase, newest first.</summary>
        public static IReadOnlyList<string> RebaseReflog { get; } =
        [
            "rebase (finish): returning to refs/heads/main",
            "rebase (pick): Local work",
            "rebase (start): checkout refs/remotes/origin/main",
        ];

        // ---- Git's texts of part S (2.36; the key lines on every git) ----

        /// <summary>The fast-forward's refusal (key; 2.55 prints nine <c>hint:</c> lines first).</summary>
        public const string NotPossibleToFastForward = "fatal: Not possible to fast-forward, aborting.";

        public const string BadBooleanOnly = "fatal: bad boolean config value 'only' for 'pull.ff'";

        public const string BadBooleanPreserve = "fatal: bad boolean config value 'preserve' for 'pull.rebase'";

        /// <summary>The rebase's standard error, its two lines (2.36 joins them with a carriage return and <c>ESC[K</c>).</summary>
        public static IReadOnlyList<string> RebaseLines { get; } = ["Rebasing (1/1)", "Successfully rebased and updated refs/heads/main."];

        // ---- Pull's reads (8.9, SD29) ----

        /// <summary>One read of Pull's reader as the report gives it: its command, exit code and standard output.</summary>
        public sealed record PullRead(string Command, int ExitCode, string Output = "");

        /// <summary>Pull's reader with S14's and S18's state: nothing set, three reads, each exit 1.</summary>
        public static IReadOnlyList<PullRead> PullReadsNothingSet { get; } =
        [
            new(GetPullFfCommand, 1),
            new(GetBranchRebaseCommand, 1),
            new(GetPullRebaseCommand, 1),
        ];

        /// <summary>Pull's reader with S19's seed only: <c>only</c>, its <c>--type=bool</c> refusal, then two exit 1.</summary>
        public static IReadOnlyList<PullRead> PullReadsFastForwardOnly { get; } =
        [
            new(GetPullFfCommand, 0, "only\n"),
            new(BoolPullFfCommand, 128),
            new(GetBranchRebaseCommand, 1),
            new(GetPullRebaseCommand, 1),
        ];

        /// <summary>Pull's reader with the repository's <c>pull.rebase = true</c> and <c>pull.ff = true</c>.</summary>
        public static IReadOnlyList<PullRead> PullReadsRebase { get; } =
        [
            new(GetPullFfCommand, 0, "true\n"),
            new(GetBranchRebaseCommand, 1),
            new(GetPullRebaseCommand, 0, "true\n"),
        ];

        /// <summary>Pull's reader with <c>branch.main.rebase = false</c> and no local pull key: the branch's key answers.</summary>
        public static IReadOnlyList<PullRead> PullReadsBranchRebase { get; } =
        [
            new(GetPullFfCommand, 0, "only\n"),
            new(BoolPullFfCommand, 128),
            new(GetBranchRebaseCommand, 0, "false\n"),
        ];

        /// <summary>Pull's reader with the repository's <c>pull.rebase = preserve</c>: five reads (nine processes at an opening).</summary>
        public static IReadOnlyList<PullRead> PullReadsPreserve { get; } =
        [
            new(GetPullFfCommand, 0, "only\n"),
            new(BoolPullFfCommand, 128),
            new(GetBranchRebaseCommand, 1),
            new(GetPullRebaseCommand, 0, "preserve\n"),
            new(BoolPullRebaseCommand, 128),
        ];

        // ---- The graphs (part S's scenarios) ----

        /// <summary>ST0's and ST1's two lanes (Graph 48, Message x 438).</summary>
        public const int StLanes = 2;

        /// <summary>STR's one lane.</summary>
        public const int StrLanes = 1;

        /// <summary>The x a row is clicked at "in its Message column" with one or two lanes.</summary>
        public const double StRowPointX = 468;

        /// <summary>ST0, "settings" as built.</summary>
        public static IReadOnlyList<CommitRow> St0 { get; } =
        [
            Row("Local work", 0, 0, Current(Main)) with { ShortSha = "73f8cad", Date = "2026-01-01 12:02" },
            Row("Remote work", 1, 1, "origin/main") with { ShortSha = "e0cbfaf", Date = "2026-01-01 12:01" },
            Row("Initial commit", 0, 0) with { ShortSha = "9f9a014", Date = "2026-01-01 12:00", Ends = [1] },
        ];

        /// <summary>ST0w's working-changes row: display row 0, its ring in lane 0, colour 0.</summary>
        public static WorkingChangesRow St0wWorking { get; } = new("Working changes", 0, 0);

        /// <summary>ST1, after S18 step 6's commit as Ann Example.</summary>
        public static IReadOnlyList<CommitRow> St1 { get; } =
        [
            Row(ThirdNote, 0, 0, Current(Main)) with { ShortSha = "3e519fb", Date = "2026-01-02 09:00" },
            St0[0] with { Labels = [] },
            St0[1],
            St0[2],
        ];

        /// <summary>ST1r, after S18 step 9's commit as Bo Local: ST1 with that SHA.</summary>
        public static IReadOnlyList<CommitRow> St1r { get; } = [St1[0] with { ShortSha = "ead110c" }, .. St1.Skip(1)];

        /// <summary>STR, after S19 step 6's rebasing pull: one lane, Local work rebased onto Remote work.</summary>
        public static IReadOnlyList<CommitRow> Str { get; } =
        [
            Row("Local work", 0, 0, Current(Main)) with { ShortSha = "b4253bb", Date = "2026-01-01 12:02" },
            Row("Remote work", 0, 0, "origin/main") with { ShortSha = "e0cbfaf", Date = "2026-01-01 12:01" },
            Row("Initial commit", 0, 0) with { ShortSha = "9f9a014", Date = "2026-01-01 12:00" },
        ];

        /// <summary>A commit row's click point "in its Message column" (468, 129 + 26 d) at display row <paramref name="displayRow"/>.</summary>
        public static Point StRowPoint(int displayRow) => new(StRowPointX, WindowLayout.RowCentreY(displayRow));

        // ---- "graph" (S15; phase 1's, GR0) ----

        /// <summary>The tab's name of the "graph" scenario.</summary>
        public const string Graph = "graph";

        public const int GraphLanes = 3;

        /// <summary>GR0: phase 1's 13 rows (phase 5's GS0), the stash at row 0.</summary>
        public static IReadOnlyList<CommitRow> Gr0 { get; } =
        [
            Row("On main: Work in progress on README", 0, 0, "stash@{0}") with { ShortSha = "95daa96", Stash = true, Date = "2026-01-01 12:12" },
            Row("Highlight matches", 1, 1, "feature/search") with { ShortSha = "f463b85", Date = "2026-01-01 12:11" },
            Row("Add settings page", 0, 0, Current("main")) with { ShortSha = "47e6ec7", Date = "2026-01-01 12:10" },
            Row("Fix typo in docs", 2, 2, "origin/main") with { ShortSha = "974bd83", Date = "2026-01-01 12:09" },
            Row("Bump version", 0, 0, "v0.2") with { ShortSha = "2775228", Date = "2026-01-01 12:08" },
            Row("Fix crash on start", 2, 3, "bugfix/crash-on-start") with { ShortSha = "72267a3", Date = "2026-01-01 12:07" },
            Row("Add search box", 1, 1, "origin/feature/search") with { ShortSha = "6aab2e4", Date = "2026-01-01 12:06" },
            Row("Merge branch 'feature/login'", 0, 0) with { ShortSha = "f7919b8", Merge = true, Date = "2026-01-01 12:05" },
            Row("Update README", 0, 0) with { ShortSha = "10bcd42", Date = "2026-01-01 12:04" },
            Row("Validate passwords", 1, 4, "feature/login") with { ShortSha = "4a62ef9", Date = "2026-01-01 12:03" },
            Row("Add login form", 1, 4) with { ShortSha = "7b230d3", Date = "2026-01-01 12:02" },
            Row("Add app skeleton", 0, 0, "v0.1") with { ShortSha = "933250d", Date = "2026-01-01 12:01" },
            Row("Initial commit", 0, 0) with { ShortSha = "53347b3", Date = "2026-01-01 12:00" },
        ];

        /// <summary>The spots S15 samples, clear of text, lanes and rows: the graph area, the left panel, the tab strip's chrome.</summary>
        public static Point WindowSpot { get; } = new(480, 600);

        public static Point PanelSpot { get; } = new(130, 600);

        public static Point ChromeSpot { get; } = new(1070, 18);

        /// <summary>
        /// The spots S15 step 2 also samples for "every view light", each clear of text and controls:
        /// the toolbar between its two groups and the details panel below its placeholder (the status
        /// bar at the neutral point besides).
        /// </summary>
        public static Point ToolbarSpot { get; } = new(700, 62);

        public static Point DetailsSpot { get; } = new(1080, 640);

        /// <summary>GR0's stash line sampled as phase 5's F15 samples it (FD15's rule): lane 0's centre, x 406, from y 136 to 175, at least 5 dashes.</summary>
        public const int StashLineX = 406;

        public const int StashLineTop = 136;

        public const int StashLineBottom = 175;

        public const int StashLineDashes = 5;

        // ---- "conflicts" stopped in its merge (S20; phase 4's GC-M) ----

        public const string Conflicts = "conflicts";

        public const string ConflictsFile = "notes.txt";

        /// <summary>GC-M's banner (scenario facts' "conflicts" stopped in its merge).</summary>
        public const string MergingBanner = "Merging: \"Merge branch 'feature/theirs'\". 1 file has conflicts.";

        /// <summary><c>MERGE_HEAD</c> of GC-M: Theirs edits.</summary>
        public const string TheirsEditsSha = "5c77ffb0ec78abf986d121f42013eee14422e5a5";

        // ---- "ignored" (S21; scenario facts 4, IG0) ----

        public const string Ignored = "ignored";

        public const string MainWorkSha = "c49cac40cc05ce7ebe709555621751082c868662";

        public const string VendorSecretSha = "133a796ef1cfa3b4f2dc3237b5342751f85fcc3f";

        public const string TrackSecretSha = "1ac759d23fd78a78f7660d289a1373ea85378b69";

        /// <summary>IG0's three lanes (Message x 454): rows are clicked at x 484.</summary>
        public const double IgRowPointX = 484;

        /// <summary>"Track the secret", IG0's commit row 5, clicked at (484, 259).</summary>
        public const int TrackSecretRow = 5;

        public static Point TrackSecretPoint { get; } = new(IgRowPointX, 259);

        public const string SecretLocal = "LOCAL\n";

        public const string SecretVendor = "vendor\n";

        public const string BuildOutput = "binary\n";

        /// <summary><c>git status --porcelain=v1 --ignored</c> of IG0.</summary>
        public static IReadOnlyList<string> Ig0Ignored { get; } = ["!! build/", "!! secret.env"];

        /// <summary>The same after S21 step 4's checkout of <c>vendor</c> (IG0v): <c>secret.env</c> is tracked there.</summary>
        public static IReadOnlyList<string> Ig0vIgnored { get; } = ["!! build/"];

        public const string SwitchVendorKeptCommand = "git switch --no-guess --no-overwrite-ignore vendor";

        public const string SwitchVendorCommand = "git switch --no-guess vendor";

        public const string DetachTrackSecretCommand = "git switch --detach --no-overwrite-ignore " + TrackSecretSha;

        public const string CheckIgnoreCommand = "git check-ignore --stdin -z";

        /// <summary>The ignored files' read's input and output (9.11).</summary>
        public const string SecretIgnored = "secret.env\0";

        /// <summary>git's refusal of a checkout over <c>secret.env</c> (2.36 and 2.55), exit 1; the first line is the key.</summary>
        public static IReadOnlyList<string> UntrackedOverwritten { get; } =
        [
            "error: The following untracked working tree files would be overwritten by checkout:",
            "\tsecret.env",
            "Please move or remove them before you switch branches.",
            "Aborting",
        ];

        public const string UpToDateWithVendor = "Your branch is up to date with 'origin/vendor'.";

        public const string SwitchedToVendor = "Switched to branch 'vendor'";

        // ---- The window (design 7.1, SD2) ----

        /// <summary>The settings card at 1100×700, asserted exactly (7.1).</summary>
        public static Rect CardAt1100 { get; } = new(120, 40, 860, 620);

        /// <summary>The settings card at 1920×1080, asserted exactly (7.1).</summary>
        public static Rect CardAt1920 { get; } = new(530, 200, 860, 680);

        /// <summary>The rest point at 1100×700: the neutral point, in the status bar (SD2).</summary>
        public static Point RestAt1100 { get; } = new(760, 687);

        /// <summary>The rest point at 1920×1080: on the backdrop below the card (SD2).</summary>
        public static Point RestAt1920 { get; } = new(760, 1000);

        // ---- The toolbar (7.2, 6.8, 11.6, 14.11) ----

        public const string SettingsLabel = "Settings";

        public const string SettingsToolTip = "Open settings (Ctrl+,)";

        public const string SettingsKey = "Ctrl+,";

        public const string ToLight = "Switch to the light theme";

        public const string ToDark = "Switch to the dark theme";

        public const string ToLightLeavingSystem = "Switch to the light theme (stops following the system)";

        public const string ToDarkLeavingSystem = "Switch to the dark theme (stops following the system)";

        public const string PullToolTip = "Pull from origin/main (Ctrl+Shift+L)";

        public const string StashToolTip = "Stash your changes (Ctrl+Shift+S)";

        // ---- The screen's frame (7.1, 7.8, 14.8) ----

        public const string Title = "Settings";

        public const string NavGeneral = "General";

        public const string NavGit = "Git";

        public const string NavTools = "Tools";

        public const string NavShortcuts = "Keyboard shortcuts";

        public const string HasError = "Has an error";

        public const string Cancel = "Cancel";

        public const string Save = "Save";

        // ---- General (7.3, 14.8) ----

        public const string ThemeLabel = "Theme";

        public const string ThemeDark = "Dark";

        public const string ThemeLight = "Light";

        public const string ThemeSystem = "System";

        public const string ThemeNote = "System follows your operating system's light or dark mode.";

        public const string FetchingLabel = "Fetching";

        public const string AutoFetch = "Fetch in the background every";

        public const string Minutes = "minutes";

        public const string AutoFetchNote = "Only the active tab's repository; it never asks for a password.";

        public const string AutoFetchError = "Enter a whole number of minutes from 1 to 60.";

        public const string WorkingTreeLabel = "Working tree";

        public const string KeepIgnored = "Keep ignored files";

        public const string KeepIgnoredNote =
            "A checkout, merge, rebase, cherry-pick, revert, stash apply or stash pop that would overwrite or delete an ignored file does not start. Off: they run as git runs them, which can overwrite or delete such a file without a word; checking out an older commit of the current branch, Undo and Redo still keep it.";

        public const string StashUntracked = "Include untracked files when stashing";

        public const string StashUntrackedNote = "The Stash changes dialog starts with this; its own check box changes it too.";

        public const string OpeningFilesLabel = "Opening files";

        public const string RunnableIntro = "Open file asks first before it opens a file with one of these extensions:";

        public const string RunnableList = ".exe .bat .cmd .com .ps1 .vbs .vbe .js .jse .wsf .hta .msi .scr .pif .cpl .lnk .url .reg .jar .app .command .sh .desktop";

        /// <summary><c>RunnablePathExt</c> on Windows, from the fixed catalogue's <c>PATHEXT</c>; not shown elsewhere.</summary>
        public const string RunnablePathExt = "From PATHEXT: .wsh .msc";

        public const string AlsoAskFor = "Also ask for";

        public const string RunnablePlaceholder = ".py .pl";

        public const string RunnableUnix = "On Linux and macOS it also asks before it opens a file marked as a program.";

        /// <summary>S17's added extension (SD23).</summary>
        public const string PyExtension = ".py";

        public const string DefaultMinutes = "5";

        // ---- Git (7.4, 8.2 to 8.8, 14.8) ----

        public const string ScopeGlobal = "All repositories (global)";

        public const string ScopeRepository = "This repository";

        public const string ScopeRepositoryDisabled = "Open a repository's tab to change its own settings";

        /// <summary><c>GitScopeRepository</c>'s tooltip: <c>{name} — {folder}</c>.</summary>
        public static string ScopeRepositoryTip(string name, string folder) => $"{name} — {folder}";

        public const string CaptionGlobal = "Saved in your global git configuration, which git in a terminal uses too.";

        public const string CaptionRepository = "Saved in this repository's .git/config, which all its worktrees share. These values win over those for all repositories.";

        public const string NameLabel = "Name";

        public const string EmailLabel = "Email";

        public const string PullLabel = "Pull";

        public const string MergeToolLabel = "Merge tool";

        public const string NotSet = "Not set";

        public static string SameAsGlobalValue(string value) => $"Same as global: {value}";

        public const string NameRefused = "A name cannot contain <, > or a line break.";

        public const string EmailWarning = "This does not look like an email address.";

        public static string CommitsMadeAs(string identity) => $"Commits here are made as {identity}.";

        public const string NoEmailSet = "Git cannot make commits here: no email is set.";

        public const string UseConfigOnly = "user.useConfigOnly is set, so git does not guess a missing name or email.";

        public const string PullSameAsGlobal = "Same as global";

        public const string PullMerge = "Merge (fast-forward when possible)";

        public const string PullMergeCommit = "Merge, always with a merge commit";

        public const string PullRebase = "Rebase";

        public const string PullRebaseMerges = "Rebase, keeping merge commits";

        public const string PullFastForwardOnly = "Fast-forward only";

        /// <summary>The six radios after <c>PullNotSet</c>, top to bottom, with their automation ids.</summary>
        public static IReadOnlyList<(string Id, string Label)> PullRadios { get; } =
        [
            ("PullMerge", PullMerge),
            ("PullMergeCommit", PullMergeCommit),
            ("PullRebase", PullRebase),
            ("PullRebaseMerges", PullRebaseMerges),
            ("PullFastForwardOnly", PullFastForwardOnly),
        ];

        public static string PullOther(string key, string value) => $"Other: {key} = {value}";

        public const string PullNotSetGlobalTip = "Nothing set: the app merges, and git in a terminal stops when the branches have diverged";

        public const string PullNotSetRepositoryTip = "Uses the pull mode for all repositories";

        public const string PullsFail = "Pulls fail while this value is set.";

        public static string PullInEffect(string branch, string mode, string key, string value, string file) => $"Pull into {branch}: {mode} ({key} = {value} in {file}).";

        public static string PullNothingSet(string branch) =>
            $"Pull into {branch}: Merge (fast-forward when possible): nothing is set, so the app merges; git in a terminal stops when the branches have diverged.";

        public static string PullFails(string branch, string problem) => $"Pull into {branch} fails: {problem}";

        public static string BranchRebaseNotice(string branch, string value) =>
            $"{branch} has its own pull setting, branch.{branch}.rebase = {value}, which git uses instead of the mode above.";

        public const string UseRepositorySetting = "Use the repository's setting";

        public static string BranchRebaseMarked(string branch) => $"Save removes branch.{branch}.rebase, so {branch} pulls as set above.";

        public static string KeepBranchSetting(string branch) => $"Keep {branch}'s setting";

        public const string MergeToolSameAsGlobalNotSet = "Same as global (not set)";

        public const string Choose = "Choose…";

        public const string MergeToolNote = "Used by Merge tool… in the conflict resolver, and by git mergetool in a terminal.";

        public static string MergeToolNotFound(string tool) => $"git did not find {tool} on this computer: Merge tool… fails until it is installed.";

        /// <summary>SD8's notice in the global scope: <c>{repository} sets its own: {value}.</c></summary>
        public static string SetsItsOwn(string repository, string value) => $"{repository} sets its own: {value}.";

        public const string GlobalFileName = "your global configuration";

        public const string LocalFileName = ".git/config";

        // ---- Tools (7.5, 12.5, 14.8) ----

        public const string EditorLabel = "Editor";

        public const string EditorNone = "None";

        public const string EditorOther = "Other";

        public const string VisualStudioCode = "Visual Studio Code";

        public const string EditorNote = "Open in editor appears in the file menus, beside Open file, once an editor is chosen.";

        public const string ProgramLabel = "Program";

        public const string ArgumentsLabel = "Arguments";

        public const string Browse = "Browse…";

        public const string DefaultArguments = "{path}";

        public const string ArgumentsHint = "{path} is the file's full path, {line} its line (1 for now). Without {path}, the path comes last.";

        public const string NoFileAtPath = "There is no file at this path.";

        public const string ChooseProgramTitle = "Choose the editor's program";

        /// <summary>S14 step 9's program, which does not exist (SD16).</summary>
        public static string MissingProgram => OperatingSystem.IsWindows() ? @"C:\Tools\Missing\Ed.exe" : "/opt/tools/missing/ed";

        /// <summary>The fixed catalogue's Visual Studio Code (12.9).</summary>
        public static string VsCodeProgram => OperatingSystem.IsWindows() ? @"C:\Tools\Microsoft VS Code\Code.exe" : "/opt/tools/vscode/code";

        public static string SublimeProgram => OperatingSystem.IsWindows() ? @"C:\Tools\Sublime Text\subl.exe" : "/opt/tools/sublime/subl";

        public static string IdeaProgram => OperatingSystem.IsWindows() ? @"C:\Tools\JetBrains\IntelliJ IDEA 2026.1\bin\idea64.exe" : "/opt/tools/idea/bin/idea";

        /// <summary>Visual Studio Code's working directory on Windows, the program's folder (12.4); the user's home elsewhere.</summary>
        public const string VsCodeFolderOnWindows = @"C:\Tools\Microsoft VS Code";

        /// <summary>The variable an editor's start removes (12.4).</summary>
        public const string ElectronRunAsNode = "ELECTRON_RUN_AS_NODE";

        public const string EditorStoredId = "vscode";

        // ---- The Pick dialogs (7.7, 14.6) ----

        public const string ChooseEditorTitle = "Choose an editor";

        public const string ChooseMergeToolTitle = "Choose a merge tool";

        public const string PickConfirm = "Choose";

        public const string OtherEditor = "Other…";

        /// <summary>"Choose an editor"'s options, text — detail, in order, with the fixed catalogue (12.9).</summary>
        public static IReadOnlyList<(string Text, string Detail)> EditorOptions =>
        [
            (EditorNone, "no Open in editor in the file menus"),
            (VisualStudioCode, VsCodeProgram),
            ("Sublime Text", SublimeProgram),
            ("IntelliJ IDEA", IdeaProgram),
            (OtherEditor, "a program of your choice"),
        ];

        /// <summary>"Choose a merge tool"'s options in the global scope with S20's seed, text — detail, in SD7's order: no <c>vimdiff</c>.</summary>
        public static IReadOnlyList<(string Text, string Detail)> MergeToolOptions { get; } =
        [
            (NotSet, "Merge tool… stays disabled"),
            ("kdiff3", "found on this computer"),
            ("meld", "found on this computer"),
            ("slow", "your own tool (mergetool.slow.cmd)"),
            ("araxis", "not found on this computer"),
            ("bc", "not found on this computer"),
            ("p4merge", "not found on this computer"),
            ("winmerge", "not found on this computer"),
        ];

        public const string SlowTool = "slow";

        public const string AraxisTool = "araxis";

        // ---- Keyboard shortcuts (6.1, 6.11, 7.6; SD21) ----

        public const string ShortcutsIntro = "These keys are fixed.";

        /// <summary>The Repository group's note.</summary>
        public const string RepositoryNote = "On a dashboard, Fetch and Pull fetch and pull every repository of its workspace.";

        /// <summary>The row whose keys take two lines.</summary>
        public const string ExtendSelection = "Extend the graph's selection";

        public const string ExtendSelectionKeys = "Shift+Up, Shift+Down, Shift+PageUp, Shift+PageDown, Shift+Home, Shift+End";

        /// <summary>The list's eleven groups in 6.11's order, each with its 50 rows' actions and keys as Windows, Linux and the headless platform write them (literals, 6.12).</summary>
        public static IReadOnlyList<(string Group, IReadOnlyList<(string Action, string Keys)> Rows)> ShortcutGroups { get; } =
        [
            ("App",
            [
                ("Command palette", "Ctrl+Shift+P"),
                ("Open settings…", "Ctrl+,"),
                ("Show or hide the activity log", "Ctrl+J"),
                ("Refresh", "F5, Ctrl+R"),
            ]),
            ("Tabs",
            [
                ("New tab", "Ctrl+T"),
                ("Close tab", "Ctrl+W"),
                ("Next tab", "Ctrl+Tab, Ctrl+PageDown"),
                ("Previous tab", "Ctrl+Shift+Tab, Ctrl+PageUp"),
                ("Go to tab 1 to 8", "Ctrl+1 to Ctrl+8"),
                ("Go to the last tab", "Ctrl+9"),
                ("Go to the next dashboard", "Ctrl+Shift+D"),
            ]),
            ("Repositories",
            [
                ("Open repository…", "Ctrl+O"),
                ("Clone repository…", "Ctrl+Shift+C"),
                ("Init repository…", "Ctrl+Shift+I"),
            ]),
            ("Repository",
            [
                ("Fetch all remotes", "Ctrl+Shift+F"),
                ("Pull", "Ctrl+Shift+L"),
                ("Push", "Ctrl+Shift+K"),
                ("Create branch…", "Ctrl+B"),
                ("Stash changes…", "Ctrl+Shift+S"),
                ("Pop the newest stash", "Ctrl+Shift+O"),
                ("Undo", "Ctrl+Z"),
                ("Redo", "Ctrl+Y, Ctrl+Shift+Z"),
                ("Search commits", "Ctrl+F"),
                ("Next match", "F3, Ctrl+G"),
                ("Previous match", "Shift+F3, Ctrl+Shift+G"),
                ("Show reflog", "Ctrl+Shift+R"),
            ]),
            ("Changes",
            [
                ("Stage all", "Ctrl+Shift+A"),
                ("Unstage all", "Ctrl+Shift+X"),
                ("Go to the commit message", "Ctrl+Shift+M"),
                ("Commit, in the message boxes", "Ctrl+Enter"),
            ]),
            ("Files",
            [
                ("File history of the open diff's file", "Ctrl+Shift+H"),
                ("Blame of the open diff's file", "Ctrl+Shift+B"),
                ("Close the diff, a page or the search bar", "Esc"),
            ]),
            ("Moving around",
            [
                ("Filter branches and tags", "Ctrl+Shift+E"),
                ("Next region", "F6"),
                ("Previous region", "Shift+F6"),
                ("Context menu", "Shift+F10, Menu"),
                ("Move in the graph", "Up, Down, PageUp, PageDown, Home, End"),
                (ExtendSelection, ExtendSelectionKeys),
            ]),
            ("Interactive rebase",
            [
                ("Move the selected row", "Alt+Up, Alt+Down"),
                ("Pick, reword, squash, fixup, drop", "P, R, S, F, D"),
            ]),
            ("Conflict resolver",
            [
                ("Previous or next conflict", "Alt+Up, Alt+Down"),
                ("Previous or next file", "Alt+Left, Alt+Right"),
                ("Leave the result", "Ctrl+Tab"),
            ]),
            ("Dashboard",
            [
                ("Open the repository", "Enter"),
                ("Remove from workspace", "Delete"),
                ("Move the repository", "Alt+Up, Alt+Down"),
            ]),
            ("Dialogs and menus",
            [
                ("Confirm", "Enter"),
                ("Confirm from a box of several lines", "Ctrl+Enter"),
                ("Cancel or close", "Esc"),
            ]),
        ];

        /// <summary>The group whose note shows under its rows, and the last group, wholly in view at the end (S14 step 7).</summary>
        public const string NotedGroup = "Repository";

        public const string LastGroup = "Dialogs and menus";

        // ---- Operations, results and dialogs (8.3, 4.8, 8.9, 9.15, 14.1 to 14.4) ----

        public const string SaveRepositorySettings = "Save repository settings";

        public const string SavedRepositorySettings = "Saved the repository's settings";

        public const string PullTitle = "Pull";

        public const string PullFailed = "Pull failed";

        /// <summary><c>OperationTexts.PullNotFastForward</c> for main and origin/main (4.8, D211).</summary>
        public const string PullNotFastForward = "main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main.";

        public const string PreserveNotUnderstood = "pull.rebase is set to preserve, which this app does not understand.";

        public const string PulledAndRebased = "Pulled and rebased main onto origin/main";

        public const string CheckOutVendor = "Check out vendor";

        public const string CheckOutTrackSecret = "Check out 1ac759d";

        public const string CheckoutBlocked = "Checkout blocked";

        public static string IgnoredFileInTheWay(string name) => $"1 ignored file is in the way: {name} tracks a file of the same name. Move it away first.";

        public const string CheckedOutVendor = "Checked out vendor";

        public const string CheckOutThisCommit = "Check out this commit";

        public static string Failed(string title) => title + " failed";

        public const string CommitTitle = "Commit";

        public const string CommitOneFile = "Commit 1 file";

        public const string CommitOneFileTip = "Commit 1 file (Ctrl+Enter)";

        public static string UndoCommit(string subject) => $"Undo commit \"{subject}\" (Ctrl+Z)";

        public const string UndoPull = "Undo pull into main (Ctrl+Z)";

        public const string UndoCheckoutOfVendor = "Undo checkout of vendor (Ctrl+Z)";

        public const string NothingToUndo = "Nothing to undo";

        public const string StashDialogTitle = "Stash changes";

        public const string StashMessagePlaceholder = "Message (optional)";

        public const string StashOption = "Include untracked files";

        public const string StashConfirm = "Stash";

        public static string OpenFileQuestion(string file) => $"Open {file}?";

        public const string OpenFileText = "This file can run programs. Open it anyway?";

        public const string OpenFileConfirm = "Open";

        public const string FileHistory = "File history";

        public const string Blame = "Blame";

        public const string OpenFile = "Open file";

        public const string OpenInEditor = "Open in editor";

        public const string ShowInFolder = "Show in folder";

        public const string CopyPath = "Copy path";

        /// <summary>The resolver's <c>Merge tool…</c> and its reason while no tool is set (8.10).</summary>
        public const string MergeToolButton = "Merge tool…";

        public const string SetMergeToolReason = "Set merge.tool in git's configuration to use a merge tool";

        public const string CopyOutput = "Copy output";

        public const string Close = "Close";

        public const string Ready = "Ready";

        public const string ChangedFilesThree = "Changed files (3)";

        public const string ChangedFilesOne = "Changed files (1)";

        public const string AppCommitDate = "2026-01-02 09:00";

        // ---- The palette (5.4, 11.7, 14.7) ----

        public const string PaletteOpenSettings = "Open settings…";

        public const string PaletteShowShortcuts = "Show keyboard shortcuts…";

        public const string PaletteChooseEditor = "Choose an editor…";

        public const string PaletteDarkTheme = "Use the dark theme";

        public const string PaletteLightTheme = "Use the light theme";

        public const string PaletteFollowSystem = "Follow the system's theme";

        // ---- Log lines (14.14) ----

        public const string AutoFetchOverride = "VISUALCOMMIT_AUTO_FETCH_MINUTES=0 overrides the auto-fetch setting (120 minutes).";

        public const string ThemeDarkAtStart = "Theme: Dark.";

        public static string ThemeSystemAtStart(bool dark) => $"Theme: System (the system's theme is {(dark ? "dark" : "light")}).";

        public static string ThemeChanged(string theme) => $"Theme changed to {theme}.";

        public static string SystemFollowed(bool dark) => $"The system's theme changed to {(dark ? "dark" : "light")}; the app follows it.";

        public static string SystemKept(bool dark, string kept) => $"The system's theme changed to {(dark ? "dark" : "light")}; the app keeps the {kept} theme.";

        public static string PaletteRan(string repository, string label) => $"{repository}: Command palette ran: {label}";

        public const string OpenInEditorLog = "Open in editor: ";

        // ---- settings.json (JsonSettingsStore's camelCase names, part S's scenarios) ----

        /// <summary>S15 part A's file, written before the start.</summary>
        public const string SystemThemeFile = "{ \"theme\": \"System\" }";

        /// <summary>S16's file, written before the start (SD14).</summary>
        public const string S16SettingsFile = "{ \"autoFetchMinutes\": 120, \"stashIncludeUntracked\": false }";

        /// <summary>The properties a save writes, with their defaults (Dark, Flat, Inline, 5, true, false, true, null, empty).</summary>
        public static IReadOnlyList<(string Name, string Default)> SettingsDefaults { get; } =
        [
            ("theme", "\"Dark\""),
            ("fileList", "\"Flat\""),
            ("diffMode", "\"Inline\""),
            ("autoFetchMinutes", "5"),
            ("stashIncludeUntracked", "true"),
            ("showAllFiles", "false"),
            ("keepIgnoredFiles", "true"),
            ("editor", "null"),
            ("extraRunnableExtensions", "\"\""),
        ];
    }
}
