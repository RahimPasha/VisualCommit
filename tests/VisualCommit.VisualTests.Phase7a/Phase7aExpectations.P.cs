namespace VisualCommit.VisualTests.Phase7a;

public static partial class Phase7aExpectations
{
    /// <summary>
    /// Part P, pull requests in the left panel (docs/test-reports/phase-7a.md, part P; design 10.2 to
    /// 10.7): design 10.2's texts, <c>hello</c>'s and <c>tool</c>'s rows, the menus' lines, the
    /// addresses, the added pull requests' data, the picture names and the R pictures, written from
    /// the report and not from the App's texts (<c>Phase7aExpectationTests.P</c> compares them).
    /// Compiled by the real-window pass too.
    /// </summary>
    public static class PartP
    {
        // ---- Design 10.2's texts -----------------------------------------------------------------

        public const string SignInToSeePullRequests = "Sign in to GitHub to see pull requests…";
        public const string LoadingPullRequests = "Loading pull requests…";
        public const string NoOpenPullRequests = "No open pull requests";
        public const string MorePullRequests = "More than 300 open pull requests";
        public const string OpenInBrowser = "Open in browser";
        public const string CopyLink = "Copy link";
        public const string RefreshPullRequests = "Refresh pull requests";
        public const string OpenPullRequestsInBrowser = "Open pull requests in browser";
        public const string SignInToGitHubItem = "Sign in to GitHub…";
        public const string CreatePullRequest = "Create pull request…";
        public const string PullRequestsLoading = "Pull requests are loading";
        public const string Draft = "Draft";
        public const string Title = "Pull requests";

        public static string PullRequestHeader(int number, string title) => $"#{number} {title}";

        public static string OpenPullRequest(int number) => $"Open pull request #{number}";

        public static string PullRequestOfBranch(int number, string title) => $"Pull request #{number}: {title}";

        public static string CheckOutName(string head) => $"Check out {head}";

        public static string AlreadyCheckedOut(string branch) => $"{branch} is already checked out";

        public static string FetchPullRequestTitle(int number) => $"Fetch pull request #{number}";

        public static string FetchedPullRequest(int number, string branch) => $"Fetched #{number} into {branch}";

        public static string CheckedOut(string branch) => $"Checked out {branch}";

        /// <summary><c>HostingTexts.Describe</c> of P4's 403 (1.7), and its <c>Detail</c>.</summary>
        public const string ForbiddenText = "GitHub answered: Resource not accessible by personal access token.";
        public const string ForbiddenDetail = "pull_requests=read";

        // ---- The scenario's rows (part P's facts) ---------------------------------------------------

        public const string LongBranch = "feature/a-very-long-branch-name-that-the-graph-must-cut";

        /// <summary><c>hello</c>'s rows newest first: name, author, draft.</summary>
        public static IReadOnlyList<(string Name, string Author, bool Draft)> HelloRows { get; } =
        [
            ("#5 Fix a typo in the greeting", "ada", false),
            ("#3 Shorten long labels", "octo", false),
            ("#2 Update the readme", "octo", true),
            ("#1 Add the login form", "ada", false),
        ];

        /// <summary><c>tool</c>'s rows, its parent acme/tool's.</summary>
        public static IReadOnlyList<(string Name, string Author, bool Draft)> ToolRows { get; } =
        [
            ("#8 An old idea", "ada", false),
            ("#7 Fix a typo in the readme", "octo", false),
        ];

        /// <summary>The rows' tooltips (open point 26): the header, a line break, the second line.</summary>
        public static IReadOnlyDictionary<int, string> HelloToolTips { get; } = new Dictionary<int, string>
        {
            [5] = "#5 Fix a typo in the greeting\nada · ada:fix-typo → main",
            [3] = $"#3 Shorten long labels\nocto · {LongBranch} → main",
            [2] = "#2 Update the readme\nocto · docs/readme → main · Draft",
            [1] = "#1 Add the login form\nada · feature/login → main",
        };

        // ---- Addresses (WithPort for another port) ---------------------------------------------------

        public static string PullRequestUrl(int number, int port = ServerPort) => $"{WebBase(port)}octo/hello/pull/{number}";

        public static string PullsUrl(int port = ServerPort) => $"{WebBase(port)}octo/hello/pulls";

        // ---- P7's added pull requests (open point 28) ------------------------------------------------

        public const int FirstAdded = 6;
        public const int LastAdded = 302;
        public const string AddedHead = "topic/no-pr";

        public static string AddedTitle(int number) => $"More work {number}";

        // ---- Menus -----------------------------------------------------------------------------------

        public static string[] SectionMenuSignedOut { get; } = [Title, SignInToGitHubItem, OpenPullRequestsInBrowser];

        public static string[] SectionMenuLoading { get; } = [Title, $"{RefreshPullRequests} (disabled: \"{PullRequestsLoading}\")", OpenPullRequestsInBrowser];

        public static string[] SectionMenu { get; } = [Title, RefreshPullRequests, OpenPullRequestsInBrowser];

        public static string[] PullRequestMenu(int number, string title, string checkOut) =>
            [PullRequestHeader(number, title), OpenInBrowser, checkOut, "—", CopyLink];

        /// <summary>The section's menus' height (P11: 84 ±2) and a pull request's (119 ±2).</summary>
        public const double SectionMenuHeight = 84;
        public const double PullRequestMenuHeight = 119;

        /// <summary>P12 step 14: <c>feature/login</c>'s menu, 20 items and 5 separators, 597 high ±2.</summary>
        public static string[] LoginBranchMenu { get; } =
        [
            "feature/login",
            "Check out feature/login",
            "Merge feature/login into main",
            "Merge feature/login into main (create a merge commit)",
            "Rebase main onto feature/login",
            "Interactive rebase of main onto feature/login…",
            "Fast-forward feature/login to origin/feature/login (disabled: \"feature/login is up to date with origin/feature/login\")",
            "—",
            "Push feature/login to origin/feature/login",
            "Force push feature/login… (danger)",
            OpenPullRequest(1),
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename feature/login…",
            "Delete feature/login… (danger)",
            "—",
            "Change upstream…",
            "Stop tracking origin/feature/login",
            "—",
            "Compare feature/login with main",
            "Show reflog of feature/login",
            "Hide feature/login",
            "Solo feature/login",
            "—",
            "Copy branch name",
        ];

        public const double LoginBranchMenuHeight = 597;

        /// <summary>P12 step 15: <c>local-work</c>'s menu, 519 high ±2.</summary>
        public static string[] LocalWorkMenu { get; } =
        [
            "local-work",
            "Check out local-work",
            "Merge local-work into main",
            "Merge local-work into main (create a merge commit)",
            "Rebase main onto local-work",
            "Interactive rebase of main onto local-work…",
            "—",
            "Push local-work…",
            CreatePullRequest,
            "—",
            "Create branch here…",
            "Create tag here…",
            "Rename local-work…",
            "Delete local-work… (danger)",
            "—",
            "Set upstream…",
            "—",
            "Compare local-work with main",
            "Show reflog of local-work",
            "Hide local-work",
            "Solo local-work",
            "—",
            "Copy branch name",
        ];

        public const double LocalWorkMenuHeight = 519;

        /// <summary>P12 step 16: a remote branch's menu on <c>origin</c> with the pull-request item <paramref name="pullRequestItem"/>, 415 high ±2.</summary>
        public static string[] RemoteBranchMenu(string branch, string pullRequestItem) =>
        [
            $"origin/{branch}",
            $"Check out origin/{branch}",
            $"Merge origin/{branch} into main",
            $"Merge origin/{branch} into main (create a merge commit)",
            $"Rebase main onto origin/{branch}",
            $"Interactive rebase of main onto origin/{branch}…",
            "—",
            "Create branch here…",
            "Create tag here…",
            "—",
            $"Delete {branch} on origin… (danger)",
            "—",
            pullRequestItem,
            "—",
            $"Compare origin/{branch} with main",
            $"Hide origin/{branch}",
            $"Solo origin/{branch}",
            "—",
            "Copy branch name",
        ];

        public const double RemoteBranchMenuHeight = 415;

        /// <summary>P12 step 17: the drop of <c>feature/login</c> onto <c>main</c>, 241 high ±2.</summary>
        public static string[] LoginOntoMainDrop { get; } =
        [
            "feature/login → main",
            "Merge feature/login into main",
            "Merge feature/login into main (create a merge commit)",
            "Fast-forward main to feature/login",
            "—",
            "Check out feature/login and rebase it onto main (disabled: \"feature/login is up to date with main\")",
            "Check out feature/login and rebase it onto main interactively…",
            "—",
            OpenPullRequest(1),
            "—",
            "Compare feature/login with main",
        ];

        public const double LoginOntoMainDropHeight = 241;

        /// <summary>P12 step 18: the drop of <c>local-work</c> onto <c>origin/main</c>, 215 high ±2.</summary>
        public static string[] LocalWorkOntoOriginMainDrop { get; } =
        [
            "local-work → origin/main",
            "Push local-work to origin/main",
            "Push local-work to origin/main and track it",
            "—",
            "Check out local-work and rebase it onto origin/main (disabled: \"local-work is up to date with origin/main\")",
            "Check out local-work and rebase it onto origin/main interactively…",
            "—",
            CreatePullRequest,
            "—",
            "Compare local-work with origin/main",
        ];

        public const double LocalWorkOntoOriginMainDropHeight = 215;

        // ---- Requests, the store, the log --------------------------------------------------------------

        /// <summary>The tokens no screen of part P shows (part P's conventions), by their constants' names.</summary>
        public static IReadOnlyList<(string Name, string Token)> Tokens { get; } =
        [
            (nameof(OctoToken), OctoToken),
            (nameof(OctoClassicToken), OctoClassicToken),
            (nameof(AdaToken), AdaToken),
        ];

        /// <summary>The list's first page's query (6.3).</summary>
        public const string PullsQuery = "state=open&per_page=100";

        /// <summary>The store's key of octo's token (P4 step 5's <c>save</c>).</summary>
        public static string OctoKey(int port = ServerPort) => $"github:127.0.0.1:{port}:octo";

        /// <summary><c>hello</c>'s one binding's URL (the scenario facts' bindings).</summary>
        public static string HelloGitUrl(int port = ServerPort) => $"{WebBase(port)}octo/hello.git";

        /// <summary>P4's 403's <c>Message</c>.</summary>
        public const string ForbiddenMessage = "Resource not accessible by personal access token";

        /// <summary>WP-G's Warning for P4's refused list.</summary>
        public static string ListRefusedWarning(string requestId) => $"GitHub: GET /api/v3/repos/octo/hello/pulls failed: 403 (request {requestId}).";

        /// <summary>WP-S's Warning for P4's refused list.</summary>
        public static string ListFailedWarning(string requestId) => $"GitHub: listing the pull requests of octo/hello failed: Forbidden, {ForbiddenMessage} (request {requestId})";

        /// <summary><c>hello</c>'s <c>ByRef</c> with its four rows listed (part P's facts).</summary>
        public static IReadOnlyDictionary<string, int> HelloByRef { get; } = new Dictionary<string, int>
        {
            ["refs/heads/feature/login"] = 1,
            ["refs/remotes/origin/feature/login"] = 1,
            ["refs/remotes/origin/docs/readme"] = 2,
            [$"refs/remotes/origin/{LongBranch}"] = 3,
        };

        /// <summary>The filter box's placeholder (phase 1).</summary>
        public const string FilterPlaceholder = "Filter";

        // ---- Pictures ----------------------------------------------------------------------------------

        public const string P01a = "p01a-not-github";
        public const string P02a = "p02a-signed-out";
        public const string P03a = "p03a-loading";
        public const string P04a = "p04a-failed";
        public const string P04b = "p04b-failed-tooltip";
        public const string P05a = "p05a-none";
        public const string P06a = "p06a-rows";
        public const string P06b = "p06b-fork-lists-parent";
        public const string P07a = "p07a-more-than-300";
        public const string P08a = "p08a-filter-author";
        public const string P08b = "p08b-filter-login";
        public const string P09a = "p09a-head-selected";
        public const string P09b = "p09b-nothing-selected";
        public const string P10a = "p10a-branch-rows";
        public const string P11a = "p11a-section-menu-signed-out";
        public const string P11b = "p11b-section-menu-loading";
        public const string P11c = "p11c-section-menu";
        public const string P11d = "p11d-pull-request-menu";
        public const string P12a = "p12a-branch-menu-open";
        public const string P12b = "p12b-drop-menu-open";
        public const string P13a = "p13a-checked-out-fork";

        public static IReadOnlyList<string> Pictures { get; } =
        [
            P01a, P02a, P03a, P04a, P04b, P05a, P06a, P06b, P07a, P08a, P08b, P09a, P09b, P10a, P11a, P11b, P11c, P11d, P12a, P12b, P13a,
        ];

        /// <summary>The R pictures (the real window repeats them): <c>p06a-rows</c> and <c>p11c-section-menu</c>.</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [P06a, P11c];
    }
}
