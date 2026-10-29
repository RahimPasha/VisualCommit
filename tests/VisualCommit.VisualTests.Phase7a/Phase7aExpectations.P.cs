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
