using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>Part N's expectations (docs/test-reports/phase-7a.md, part N; design 12.6, 12.8), written from the report, not from the App's texts; the real-window pass links them.</summary>
public static partial class Phase7aExpectations
{
    public static class PartN
    {
        // ---- Design 12.6's texts, by hand -----------------------------------------------------------

        public const string DialogTitle = "Create pull request";
        public const string From = "From";
        public const string Into = "Into";
        public const string ReadingGitHub = "Reading GitHub…";
        public const string TitlePlaceholder = "Title";
        public const string DescriptionPlaceholder = "Description";
        public const string Draft = "Draft";
        public const string Create = "Create";
        public const string Creating = "Creating…";
        public const string ChooseBaseTitle = "Choose the branch to merge into";
        public const string ChooseBase = "Choose the branch to merge into.";
        public const string EnterTitle = "Enter a title.";
        public const string OpenInBrowser = "Open in browser";
        public const string CreatingPullRequest = "Creating pull request…";
        public const string OperationErrorTitle = "Create pull request failed";
        public const string OperationSummary = "GitHub could not create the pull request.";
        public const string Cancelled = "Cancelled before the pull request was created.";
        public const string Choose = "Choose…";
        public const string PickConfirm = "Choose";
        public const string StopGit = "Stop git";
        public const string FetchedFromAllRemotes = "Fetched from all remotes";

        public static string OpenExisting(int number) => $"Open pull request #{number}";

        public static string OperationTitle(string head) => $"Create pull request for {head}";

        public static string Created(int number, bool draft) => $"Created {(draft ? "draft " : string.Empty)}pull request #{number}";

        public static string Failed(string head) => $"{OperationTitle(head)} failed";

        public static string CancelledStatus(string head) => $"{OperationTitle(head)} cancelled";

        public static string PushedFirstWithUpstream(string target) => $"Not on GitHub yet: Create pushes it to {target} first and sets that as its upstream.";

        public static string SameBranch(string branch) => $"Choose a branch other than {branch} to merge into.";

        public static string AlreadyOpen(int number, string head, string baseText) => $"Pull request #{number} is already open from {head} into {baseText}.";

        public static string NoCommits(string baseText, string head) => $"{baseText} already has every commit of {head}: there is nothing to merge.";

        public const string RefusedWithPermission = "GitHub refused to create a pull request in octo/hello with this token. It needs the permission pull_requests=write.";

        public static string Pushing(string branch, string target) => $"Pushing {branch} to {target}…";

        // ---- The scenario as part N prepares it (12.8; the report's "Scenario facts part N uses") ----

        public const string Topic = "topic/no-pr";
        public const string TopicRef = "refs/heads/topic/no-pr";
        public const string OriginTopic = "origin/topic/no-pr";
        public const string OriginTopicRef = "refs/remotes/origin/topic/no-pr";
        public const string LocalWork = "local-work";
        public const string LocalWorkRef = "refs/heads/local-work";
        public const string Main = "main";
        public const string OriginMain = "origin/main";
        public const string Repository = "octo/hello";
        public const string TopicTitle = "Try an idea";
        public const string LocalWorkTitle = "Local change";
        public const string MainTitle = "Add the docs folder";
        public const string Note = "A note for the reviewers.";
        public const int NewNumber = 6;

        /// <summary>The Pick's items (open point 50): <c>origin</c>'s remote branches in the refs' order, each with its repository.</summary>
        public static readonly string[] BaseOptions =
        [
            "origin/docs/readme",
            "origin/feature/a-very-long-branch-name-that-the-graph-must-cut",
            "origin/feature/login",
            "origin/main",
            "origin/topic/no-pr",
        ];

        /// <summary>
        /// 12.8: a local <c>topic/no-pr</c> tracking <c>origin/topic/no-pr</c>, level with it, made on
        /// the copy before the app starts (every method, and N1's R twin).
        /// </summary>
        public static Task PrepareAsync(TempRepo hello)
        {
            ArgumentNullException.ThrowIfNull(hello);
            return hello.GitAsync("branch", "--track", Topic, OriginTopic);
        }

        /// <summary>The page of the created pull request: <c>http://127.0.0.1:47395/octo/hello/pull/6</c>.</summary>
        public static string PullRequestPage(int port) => WithPort($"http://127.0.0.1:{ServerPort}/octo/hello/pull/{NewNumber}", port);

        // ---- Menus (P12's, with part N's lines) ---------------------------------------------------------

        /// <summary>N1 step 2's menu of <c>topic/no-pr</c>; <paramref name="pullRequestItem"/> is <c>Create pull request…</c> or <c>Open pull request #6</c>.</summary>
        public static string[] TopicMenu(string pullRequestItem) =>
        [
            Topic,
            "Check out topic/no-pr",
            "Merge topic/no-pr into main",
            "Merge topic/no-pr into main (create a merge commit)",
            "Rebase main onto topic/no-pr",
            "Interactive rebase of main onto topic/no-pr…",
            "Fast-forward topic/no-pr to origin/topic/no-pr (disabled: \"topic/no-pr is up to date with origin/topic/no-pr\")",
            "—",
            "Push topic/no-pr to origin/topic/no-pr",
            "Force push topic/no-pr… (danger)",
            pullRequestItem,
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename topic/no-pr…",
            "Delete topic/no-pr… (danger)",
            "—",
            "Change upstream…",
            "Stop tracking origin/topic/no-pr",
            "—",
            "Compare topic/no-pr with main",
            "Show reflog of topic/no-pr",
            "Hide topic/no-pr",
            "Solo topic/no-pr",
            "—",
            "Copy branch name",
        ];

        /// <summary>N5 step 2's menu of <c>origin/topic/no-pr</c> (P12 step 16's).</summary>
        public static readonly string[] OriginTopicMenu =
        [
            OriginTopic,
            "Check out origin/topic/no-pr",
            "Merge origin/topic/no-pr into main",
            "Merge origin/topic/no-pr into main (create a merge commit)",
            "Rebase main onto origin/topic/no-pr",
            "Interactive rebase of main onto origin/topic/no-pr…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            "Delete topic/no-pr on origin… (danger)",
            "—",
            "Create pull request…",
            "—",
            "Compare origin/topic/no-pr with main",
            "Hide origin/topic/no-pr",
            "Solo origin/topic/no-pr",
            "—",
            "Copy branch name",
        ];

        /// <summary>N5 step 4's drop menu (P12 step 18's).</summary>
        public static readonly string[] DropMenu =
        [
            "local-work → origin/main",
            "Push local-work to origin/main",
            "Push local-work to origin/main and track it",
            "—",
            "Check out local-work and rebase it onto origin/main (disabled: \"local-work is up to date with origin/main\")",
            "Check out local-work and rebase it onto origin/main interactively…",
            "—",
            "Create pull request…",
            "—",
            "Compare local-work with origin/main",
        ];

        /// <summary>N1 step 5's section menu after a create: its two items, no create item (open point 45).</summary>
        public static readonly string[] SectionMenu = ["Pull requests", "Refresh pull requests", "Open pull requests in browser"];

        // ---- Pictures -----------------------------------------------------------------------------------

        public const string N01a = "n01a-form";
        public const string N01b = "n01b-created";
        public const string N02a = "n02a-not-on-github";
        public const string N02b = "n02b-creating";
        public const string N03a = "n03a-created-draft";
        public const string N04a = "n04a-already-open";
        public const string N04b = "n04b-no-commits";
        public const string N05a = "n05a-remote-branch-form";
        public const string N05b = "n05b-drop-menu";
        public const string N05c = "n05c-drop-form";
        public const string N06a = "n06a-sign-in-first";
        public const string N06b = "n06b-form-after-sign-in";
        public const string N07a = "n07a-pushing";
        public const string N07b = "n07b-cancelled";
        public const string N08a = "n08a-same-branch";
        public const string N08b = "n08b-choose-base";
        public const string N08c = "n08c-enter-a-title";
        public const string N09a = "n09a-refused";
        public const string N10a = "n10a-form-1920";
        public const string N10b = "n10b-form-1920-light";

        /// <summary>The R pictures (13.4 N): N1's form and the status after creating.</summary>
        public static readonly string[] RealWindowPictures = [N01a, N01b];

        public static readonly string[] Pictures = [N01a, N01b, N02a, N02b, N03a, N04a, N04b, N05a, N05b, N05c, N06a, N06b, N07a, N07b, N08a, N08b, N08c, N09a, N10a, N10b];

        // ---- Places (part N's conventions, ±1) ---------------------------------------------------------

        public const double CardWidth = 460;
        public const double CardLeft = 320;
        public const double CardLeft1920 = 730;
        public const double LabelLeft = 340;
        public const double ValueLeft = 388;
        public const double ContentRight = 760;
        public const double TitleHeight = 32;
        public const double DescriptionHeight = 120;

        /// <summary>N10's rest point at 1920×1080 (open point 56): the backdrop under the card.</summary>
        public static readonly (double X, double Y) RestPoint1920 = (760, 1000);
    }
}
