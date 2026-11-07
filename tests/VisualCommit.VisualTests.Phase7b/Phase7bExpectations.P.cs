using System.Globalization;
using VisualCommit.Core.Hosting;

namespace VisualCommit.VisualTests.Phase7b;

public static partial class Phase7bExpectations
{
    /// <summary>
    /// Part P, requests in the left panel and the graph (docs/test-reports/phase-7b.md, part P: its
    /// conventions, scenario facts and texts; design 12.2 to 12.8), written from the report and not
    /// from the App's texts: 12.2's texts per provider and 7a's that part P keeps, each provider's
    /// rows, the badges, the menus' lines, the addresses (through <c>WithPorts</c>),
    /// the routes and queries, the log lines and the picture names. For WP-R (12.8) per provider the
    /// repository, the section's title and rows, the section's menu and <c>!21</c>'s or <c>!11</c>'s
    /// menu lines (none of them changed by <see cref="PrepareRequestsAsync"/>) and the R pictures.
    /// Compiled by the real-window pass too: it uses nothing of the 7b project.
    /// </summary>
    public static class PartP
    {
        /// <summary>The scenario's tokens, by their constants' names (no token on screen; every method's tripwire).</summary>
        public static IReadOnlyList<(string Name, string Token)> Tokens { get; } =
        [
            (nameof(GitLabToken), GitLabToken),
            (nameof(AzureDevOpsToken), AzureDevOpsToken),
            (nameof(GitHubToken), GitHubToken),
        ];

        // ---- 12.2's texts: GitLab's, Azure DevOps' and 7a's (GitHub) ---------------------------------

        public const string GitLabTitle = "Merge requests";
        public const string AzureDevOpsTitle = "Pull requests";
        public const string GitHubTitle = "Pull requests";

        public const string GitLabSignInToSee = "Sign in to GitLab to see merge requests…";
        public const string AzureDevOpsSignInToSee = "Sign in to Azure DevOps to see pull requests…";
        public const string GitHubSignInToSee = "Sign in to GitHub to see pull requests…";

        public const string GitLabLoading = "Loading merge requests…";
        public const string AzureDevOpsLoading = "Loading pull requests…";

        public const string GitLabNone = "No open merge requests";
        public const string AzureDevOpsNone = "No open pull requests";

        public const string GitLabMore = "More than 300 open merge requests";
        public const string AzureDevOpsMore = "More than 300 open pull requests";

        public const string GitLabRefresh = "Refresh merge requests";
        public const string AzureDevOpsRefresh = "Refresh pull requests";

        public const string GitLabOpenInBrowser = "Open merge requests in browser";
        public const string AzureDevOpsOpenInBrowser = "Open pull requests in browser";

        public const string GitLabCreate = "Create merge request…";
        public const string AzureDevOpsCreate = "Create pull request…";
        public const string GitHubCreate = "Create pull request…";

        public const string GitLabLoadingReason = "Merge requests are loading";
        public const string AzureDevOpsLoadingReason = "Pull requests are loading";

        public const string GitLabSignInItem = "Sign in to GitLab…";
        public const string AzureDevOpsSignInItem = "Sign in to Azure DevOps…";
        public const string GitHubSignInItem = "Sign in to GitHub…";

        /// <summary>7a's, for every provider.</summary>
        public const string OpenInBrowser = "Open in browser";
        public const string CopyLink = "Copy link";
        public const string Draft = "Draft";
        public const string FilterPlaceholder = "Filter";

        public static string TitleOf(HostingProvider p) => p == HostingProvider.GitLab ? GitLabTitle : AzureDevOpsTitle;

        public static string SignInToSeeOf(HostingProvider p) => p switch
        {
            HostingProvider.GitLab => GitLabSignInToSee,
            HostingProvider.AzureDevOps => AzureDevOpsSignInToSee,
            _ => GitHubSignInToSee,
        };

        public static string LoadingOf(HostingProvider p) => p == HostingProvider.GitLab ? GitLabLoading : AzureDevOpsLoading;

        public static string NoneOf(HostingProvider p) => p == HostingProvider.GitLab ? GitLabNone : AzureDevOpsNone;

        public static string MoreOf(HostingProvider p) => p == HostingProvider.GitLab ? GitLabMore : AzureDevOpsMore;

        public static string RefreshOf(HostingProvider p) => p == HostingProvider.GitLab ? GitLabRefresh : AzureDevOpsRefresh;

        public static string OpenInBrowserOf(HostingProvider p) => p == HostingProvider.GitLab ? GitLabOpenInBrowser : AzureDevOpsOpenInBrowser;

        public static string LoadingReasonOf(HostingProvider p) => p == HostingProvider.GitLab ? GitLabLoadingReason : AzureDevOpsLoadingReason;

        public static string SignInItemOf(HostingProvider p) => p switch
        {
            HostingProvider.GitLab => GitLabSignInItem,
            HostingProvider.AzureDevOps => AzureDevOpsSignInItem,
            _ => GitHubSignInItem,
        };

        /// <summary><c>{N}</c>: <c>!21</c> on GitLab and Azure DevOps, 7a's <c>#21</c> on GitHub.</summary>
        public static string NumberOf(HostingProvider p, int number) =>
            (p == HostingProvider.GitHub ? "#" : "!") + number.ToString(CultureInfo.InvariantCulture);

        /// <summary><c>RequestHeader</c>: <c>!21 Add the search box</c>.</summary>
        public static string Header(HostingProvider p, int number, string title) => $"{NumberOf(p, number)} {title}";

        /// <summary><c>RequestOfBranch</c>: <c>Merge request !21: Add the search box</c>, <c>Pull request !11: …</c>.</summary>
        public static string OfBranch(HostingProvider p, int number, string title) =>
            $"{(p == HostingProvider.GitLab ? "Merge request" : "Pull request")} {NumberOf(p, number)}: {title}";

        /// <summary><c>MenuTexts.OpenRequest</c>: <c>Open merge request !21</c>, <c>Open pull request !11</c>.</summary>
        public static string OpenRequest(HostingProvider p, int number) =>
            $"Open {(p == HostingProvider.GitLab ? "merge request" : "pull request")} {NumberOf(p, number)}";

        /// <summary><c>MenuTexts.AddRemoteFor</c> of Azure DevOps' fork (P10, P11).</summary>
        public const string AzureDevOpsForkReason = "Add a remote for Fernhill-Labs/Mobile App/mobile-app-fork to check out this pull request";

        /// <summary><c>MenuTexts.HeadNotNamed(AzureDevOps)</c>, which no part P check shows.</summary>
        public const string AzureDevOpsHeadNotNamed = "Azure DevOps names no repository for this pull request's branch";

        /// <summary><c>MenuTexts.NotFetchedBy</c>, which no part P check shows.</summary>
        public static string NotFetchedBy(string remote, string branch) => $"{remote} does not fetch {branch}";

        /// <summary><c>FetchRequest(GitLab, 23, origin)</c>: title, running, failed title and summary.</summary>
        public static (string Title, string Running, string ErrorTitle, string Summary) FetchMergeRequest23 { get; } =
            ("Fetch merge request !23", "Fetching merge request !23 from origin…", "Merge request not fetched", "Git could not fetch merge request !23 from origin.");

        /// <summary><c>FetchedRequest(GitLab, 23, fix-typo)</c>.</summary>
        public const string FetchedMergeRequest23 = "Fetched !23 into fix-typo";

        /// <summary>D356's row and its tooltip (<c>OtherOrganization(harbor-co, Fernhill-Labs)</c>).</summary>
        public const string OtherOrganizationRow = "In harbor-co; signed in to Fernhill-Labs";

        public const string OtherOrganizationToolTip =
            "This repository is in the Azure DevOps organization harbor-co; the signed-in account is in Fernhill-Labs, so nothing is asked of Azure DevOps for it. To see its pull requests, sign out and sign in to harbor-co in Settings, Accounts.";

        /// <summary>7a's <c>MenuTexts.CheckOutName</c>, <c>AlreadyCheckedOut</c> and <c>OperationTexts.CheckedOut</c>.</summary>
        public static string CheckOut(string label) => $"Check out {label}";

        public static string AlreadyCheckedOut(string branch) => $"{branch} is already checked out";

        public static string CheckedOut(string branch) => $"Checked out {branch}";

        // ---- The rows (scenario facts) ----------------------------------------------------------------

        /// <summary>One request's row: its number, name (<c>{N} {title}</c>), author, draft, tooltip, head label and its menu's checkout line.</summary>
        public sealed record Row(int Number, string Title, string Author, bool Draft, string ToolTip, string HeadLabel, string CheckOutLine)
        {
            public string Name => ToolTip[..ToolTip.IndexOf('\n', StringComparison.Ordinal)];
        }

        /// <summary>web-app's merge requests, newest first by <c>created_at</c>.</summary>
        public static IReadOnlyList<Row> GitLabRows { get; } =
        [
            new(23, "Fix a typo in the footer", AdaLogin, false, "!23 Fix a typo in the footer\nada · ada:fix-typo → main", "ada:fix-typo", "Check out ada:fix-typo"),
            new(22, "Draft: Update the readme", OctoLogin, true, "!22 Draft: Update the readme\nocto · docs/readme → main · Draft", "docs/readme", "Check out docs/readme"),
            new(21, "Add the search box", AdaLogin, false, "!21 Add the search box\nada · feature/search → main", "feature/search", "Check out feature/search"),
        ];

        /// <summary>mobile-app's pull requests, id descending.</summary>
        public static IReadOnlyList<Row> AzureDevOpsRows { get; } =
        [
            new(13, "Fix a typo in the greeting", AdaName, false, "!13 Fix a typo in the greeting\nAda Lovelace · mobile-app-fork:fix-typo → main", "mobile-app-fork:fix-typo", $"Check out mobile-app-fork:fix-typo (disabled: \"{AzureDevOpsForkReason}\")"),
            new(12, "Write the guide", OctoName, true, "!12 Write the guide\nOcto Cat · docs/guide → main · Draft", "docs/guide", "Check out docs/guide"),
            new(11, "Add the sign-in page", AdaName, false, "!11 Add the sign-in page\nAda Lovelace · feature/sign-in → main", "feature/sign-in", "Check out feature/sign-in"),
        ];

        /// <summary>The hosting's <c>ByRef</c> after the scenario's list: each ref's request number.</summary>
        public static IReadOnlyDictionary<string, int> GitLabByRef { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["refs/heads/feature/search"] = 21,
            ["refs/remotes/origin/feature/search"] = 21,
            ["refs/remotes/origin/docs/readme"] = 22,
        };

        public static IReadOnlyDictionary<string, int> AzureDevOpsByRef { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["refs/heads/feature/sign-in"] = 11,
            ["refs/remotes/origin/feature/sign-in"] = 11,
            ["refs/remotes/origin/docs/guide"] = 12,
        };

        /// <summary>P6: the 298 merge requests added, !24 to !321, from <c>topic/no-request</c> into <c>main</c> by <c>octo</c>.</summary>
        public const int FirstAdded = 24;
        public const int LastAdded = 321;
        public const string AddedHead = "topic/no-request";

        public static string AddedTitle(int number) => string.Create(CultureInfo.InvariantCulture, $"More work {number}");

        // ---- The badges and the graph's tooltips (12.6) ----------------------------------------------

        /// <summary>A badge as <c>PullRequestBadge(Number, Title, IsDraft, Provider)</c> holds it.</summary>
        public sealed record Badge(int Number, string Title, bool IsDraft, HostingProvider Provider)
        {
            public string Text => NumberOf(Provider, Number);
        }

        /// <summary>The graph's row (as <c>GraphRows</c> counts them, prepared) of the local request's label and of the draft's remote label, the badges on their first labels.</summary>
        public const int RequestRow = 3;
        public const int DraftRow = 2;

        public static Badge GitLabRequestBadge { get; } = new(21, "Add the search box", false, HostingProvider.GitLab);
        public static Badge GitLabDraftBadge { get; } = new(22, "Draft: Update the readme", true, HostingProvider.GitLab);
        public static Badge AzureDevOpsRequestBadge { get; } = new(11, "Add the sign-in page", false, HostingProvider.AzureDevOps);
        public static Badge AzureDevOpsDraftBadge { get; } = new(12, "Write the guide", true, HostingProvider.AzureDevOps);

        public const string GitLabRequestLabel = "feature/search";
        public const string GitLabDraftLabel = "origin/docs/readme";
        public const string AzureDevOpsRequestLabel = "feature/sign-in";
        public const string AzureDevOpsDraftLabel = "origin/docs/guide";

        public const string GitLabRequestRowTip = "feature/search (merge request !21: Add the search box)\norigin/feature/search (merge request !21: Add the search box)";
        public const string GitLabDraftRowTip = "origin/docs/readme (draft merge request !22: Draft: Update the readme)";
        public const string AzureDevOpsRequestRowTip = "feature/sign-in (pull request !11: Add the sign-in page)\norigin/feature/sign-in (pull request !11: Add the sign-in page)";
        public const string AzureDevOpsDraftRowTip = "origin/docs/guide (draft pull request !12: Write the guide)";

        /// <summary>The graph's Branch / Tag column at 1100×700 (7a's part G): every label inside x 0 to 130.</summary>
        public const double BranchColumnWidth = 130;

        // ---- Menus (12.4) -------------------------------------------------------------------------------

        public static string[] SectionMenu(HostingProvider p) => [TitleOf(p), RefreshOf(p), OpenInBrowserOf(p)];

        public static string[] SectionMenuSignedOut(HostingProvider p) => [TitleOf(p), SignInItemOf(p), OpenInBrowserOf(p)];

        public static string[] SectionMenuLoading(HostingProvider p) => [TitleOf(p), $"{RefreshOf(p)} (disabled: \"{LoadingReasonOf(p)}\")", OpenInBrowserOf(p)];

        /// <summary>D356's state: <c>Open pull requests in browser</c> alone (D376).</summary>
        public static string[] SectionMenuOtherOrganization { get; } = [AzureDevOpsTitle, AzureDevOpsOpenInBrowser];

        public static string[] RequestMenu(Row row) => [row.Name, OpenInBrowser, row.CheckOutLine, "—", CopyLink];

        public const double SectionMenuHeight = 84;
        public const double RequestMenuHeight = 119;
        public const double OtherOrganizationMenuHeight = 58;

        /// <summary>
        /// P14: the local request branch's menu, 7a's P12 step 14 menu of <c>feature/login</c> with the
        /// branch in its place and <c>Open merge request !21</c> (or <c>Open pull request !11</c>) for
        /// <c>Open pull request #1</c>, 20 items and 5 separators, 597 high ±2.
        /// </summary>
        public static string[] RequestBranchMenu(HostingProvider p)
        {
            var (branch, number) = p == HostingProvider.GitLab ? ("feature/search", 21) : ("feature/sign-in", 11);
            return
            [
                branch,
                $"Check out {branch}",
                $"Merge {branch} into main",
                $"Merge {branch} into main (create a merge commit)",
                $"Rebase main onto {branch}",
                $"Interactive rebase of main onto {branch}…",
                $"Fast-forward {branch} to origin/{branch} (disabled: \"{branch} is up to date with origin/{branch}\")",
                "—",
                $"Push {branch} to origin/{branch}",
                $"Force push {branch}… (danger)",
                OpenRequest(p, number),
                "—",
                "Create branch here…",
                "Create tag here…",
                $"Rename {branch}…",
                $"Delete {branch}… (danger)",
                "—",
                "Change upstream…",
                $"Stop tracking origin/{branch}",
                "—",
                $"Compare {branch} with main",
                $"Show reflog of {branch}",
                $"Hide {branch}",
                $"Solo {branch}",
                "—",
                "Copy branch name",
            ];
        }

        public const double RequestBranchMenuHeight = 597;

        // ---- Addresses (through WithPorts) ------------------------------------------------------------

        public static string GitLabRequestUrl(int number) => string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{GitLabPort}/northfield/platform/web-app/-/merge_requests/{number}");

        public static string GitLabRequestsPage { get; } = $"http://127.0.0.1:{GitLabPort}/northfield/platform/web-app/-/merge_requests";

        public static string AzureDevOpsRequestUrl(int number) => string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{AzureDevOpsPort}/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/{number}");

        public static string AzureDevOpsRequestsPage { get; } = $"http://127.0.0.1:{AzureDevOpsPort}/Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequests";

        public static string ManifestRequestsPage { get; } = $"http://127.0.0.1:{AzureDevOpsPort}/harbor-co/Docks/_git/manifest/pullrequests";

        public static string RequestUrlOf(HostingProvider p, int number) => p == HostingProvider.GitLab ? GitLabRequestUrl(number) : AzureDevOpsRequestUrl(number);

        public static string RequestsPageOf(HostingProvider p) => p == HostingProvider.GitLab ? GitLabRequestsPage : AzureDevOpsRequestsPage;

        // ---- Routes, paths and queries ----------------------------------------------------------------

        public const string GitLabUserRoute = "user";
        public const string GitLabProjectRoute = "project:northfield/platform/web-app";
        public const string GitLabListRoute = "mrs:northfield/platform/web-app";
        public const string GitLabForkRoute = "project:ada/web-app";
        public const string GitLabUserPath = "/api/v4/user";
        public const string GitLabProjectPath = "/api/v4/projects/northfield%2Fplatform%2Fweb-app";
        public const string GitLabListPath = "/api/v4/projects/northfield%2Fplatform%2Fweb-app/merge_requests";
        public const string GitLabListQuery = "state=opened&per_page=100";

        public const string AzureDevOpsConnectionRoute = "connection:fernhill-labs";
        public const string AzureDevOpsAreasRoute = "areas";
        public const string AzureDevOpsRepositoryRoute = "repo:fernhill-labs/mobile app/mobile-app";
        public const string AzureDevOpsListRoute = "pulls:fernhill-labs/mobile app/mobile-app";
        public const string AzureDevOpsConnectionPath = "/Fernhill-Labs/_apis/connectionData";
        public const string AzureDevOpsConnectionQuery = "api-version=7.1-preview.1";
        public const string AzureDevOpsRepositoryPath = "/Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app";
        public const string AzureDevOpsRepositoryQuery = "api-version=7.1";
        public const string AzureDevOpsListPath = "/Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests";
        public const string AzureDevOpsListQuery = "searchCriteria.status=active&api-version=7.1&$top=100&$skip=0";

        /// <summary>The routes of a sign-in's list in order (P1 step 1; WP-R's launches).</summary>
        public static IReadOnlyList<string> SignInRoutesOf(HostingProvider p) => p == HostingProvider.GitLab
            ? [GitLabUserRoute, GitLabProjectRoute, GitLabListRoute, GitLabForkRoute]
            : [AzureDevOpsConnectionRoute, AzureDevOpsAreasRoute, AzureDevOpsRepositoryRoute, AzureDevOpsListRoute];

        // ---- Failures (P4) and the log ------------------------------------------------------------------

        public const string GitLabForbiddenMessage = "Access denied: This operation requires a fine-grained personal access token with the following project permissions: [Merge Request: Read].";
        public const string GitLabForbiddenDetail = "insufficient_granular_scope";
        public const string GitLabForbiddenText = "GitLab answered: " + GitLabForbiddenMessage;
        public const string AzureDevOpsServerErrorText = "Azure DevOps had a problem. Try again later.";
        public const string AzureDevOpsServerErrorStart = "TF400898: An Internal Error Occurred. Activity Id: ";

        /// <summary>The full names in D322's line <c>{repo}: listed {n} open pull requests of {full name}</c>.</summary>
        public const string GitLabListed = "northfield/platform/web-app";
        public const string AzureDevOpsListed = "Fernhill-Labs/Mobile App/mobile-app";

        public static string GitLabRefusedWarning(string id) => $"GitLab: GET {GitLabListPath} failed: 403 (request {id}).";

        public static string GitLabListFailedWarning(string id) => $"GitLab: listing the pull requests of {GitLabListed} failed: Forbidden, {GitLabForbiddenMessage} (request {id})";

        public static string AzureDevOpsRefusedWarning(string id) => $"Azure DevOps: GET {AzureDevOpsListPath} failed: 500 (request {id}).";

        public static string AzureDevOpsListFailedWarning(string id, string message) => $"Azure DevOps: listing the pull requests of {AzureDevOpsListed} failed: ServerError, {message} (request {id})";

        /// <summary>The store's keys (lower-cased), through <c>WithPorts</c>.</summary>
        public static string GitLabKey { get; } = $"gitlab:127.0.0.1:{GitLabPort}:ada";
        public static string AzureDevOpsKey { get; } = $"azure-devops:127.0.0.1:{AzureDevOpsPort}:fernhill-labs";

        // ---- For WP-R (12.8) ----------------------------------------------------------------------------

        public static string RepositoryOf(HostingProvider p) => p == HostingProvider.GitLab ? WebApp : MobileApp;

        public static IReadOnlyList<Row> RowsOf(HostingProvider p) => p == HostingProvider.GitLab ? GitLabRows : AzureDevOpsRows;

        /// <summary>The row whose menu WP-R reads: <c>!21</c>'s or <c>!11</c>'s.</summary>
        public static Row MenuRowOf(HostingProvider p) => RowsOf(p)[^1];

        public static string[] RowMenuOf(HostingProvider p) => RequestMenu(MenuRowOf(p));

        public static string RowsPictureOf(HostingProvider p) => p == HostingProvider.GitLab ? P01a : P01c;

        public static string SectionMenuPictureOf(HostingProvider p) => p == HostingProvider.GitLab ? P10a : P10c;

        // ---- Pictures -----------------------------------------------------------------------------------

        public const string P01a = "p01a-gitlab-rows";
        public const string P01b = "p01b-gitlab-badge-tooltip";
        public const string P01c = "p01c-azure-rows";
        public const string P02a = "p02a-gitlab-signed-out";
        public const string P02b = "p02b-azure-signed-out";
        public const string P03a = "p03a-gitlab-loading";
        public const string P04a = "p04a-gitlab-failed";
        public const string P04b = "p04b-gitlab-failed-tooltip";
        public const string P04c = "p04c-azure-failed";
        public const string P05a = "p05a-gitlab-none";
        public const string P06a = "p06a-gitlab-more-than-300";
        public const string P07a = "p07a-gitlab-filter-number";
        public const string P08a = "p08a-azure-other-organization";
        public const string P08b = "p08b-azure-other-organization-tooltip";
        public const string P09a = "p09a-gitlab-listed-remote";
        public const string P10a = "p10a-gitlab-section-menu";
        public const string P10b = "p10b-gitlab-request-menu";
        public const string P10c = "p10c-azure-section-menu";
        public const string P10d = "p10d-azure-section-menu-other-organization";
        public const string P11a = "p11a-gitlab-checked-out-fork";
        public const string P11b = "p11b-azure-fork-reason";
        public const string P14a = "p14a-gitlab-branch-rows";

        /// <summary>Part P's 22 scripted pictures, in the order of its methods table.</summary>
        public static IReadOnlyList<string> Pictures { get; } =
        [
            P01a, P10a, P10b, P01b, P14a, P07a,
            P01c, P10c,
            P02a, P04a, P04b, P03a,
            P02b, P04c, P08a, P08b, P10d,
            P09a, P06a, P05a,
            P11a,
            P11b,
        ];

        /// <summary>The real window's captures (14.4).</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [P01a, P01c, P10a, P10c];
    }
}
