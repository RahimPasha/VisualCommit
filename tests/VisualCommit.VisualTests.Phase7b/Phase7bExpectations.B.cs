using VisualCommit.Core.Hosting;

namespace VisualCommit.VisualTests.Phase7b;

public static partial class Phase7bExpectations
{
    /// <summary>
    /// Part B, browse and clone (docs/test-reports/phase-7b.md, checks B1 to B8; design 11.7): the texts,
    /// rows, routes, positions and picture names its checks compare against, written from the report
    /// and not from the App's texts. The real-window pass links this file (design 0.3): it uses the
    /// BCL, Core and the other linked expectations only. For WP-R (11.7), per provider: the button's
    /// text (<see cref="ButtonOf"/>), the row list (<see cref="RowsOf"/>), the row to choose
    /// (<see cref="ChosenOf"/>), its clone URL through <see cref="WithPorts(string, int, int, int)"/>
    /// (<see cref="CloneUrlOf"/>) and the R picture's name (<see cref="ListedPictureOf"/>).
    /// </summary>
    public static class PartB
    {
        // ---- Design 11.2's texts and 7a's that part B keeps ----------------------------------

        public const string FromGitHubButton = "From GitHub…";
        public const string GitLabButton = "From GitLab…";
        public const string AzureDevOpsButton = "From Azure DevOps…";
        public const string FromAnAccount = "From an account…";

        public const string GitHubTitle = "Clone from GitHub";
        public const string GitLabTitle = "Clone from GitLab";
        public const string AzureDevOpsTitle = "Clone from Azure DevOps";

        public const string GitHubCommand = "Clone from GitHub…";
        public const string GitLabCommand = "Clone from GitLab…";
        public const string AzureDevOpsCommand = "Clone from Azure DevOps…";

        public const string GitHubMoreExist = "Only the 1,000 most recently updated repositories are listed.";
        public const string GitLabMoreExist = "Only the 1,000 most recently active repositories are listed.";

        public const string Loading = "Loading repositories…";
        public const string Empty = "No repositories to list";
        public const string Unexpected = "The repositories could not be loaded.";
        public const string Choose = "Choose";
        public const string Cancel = "Cancel";
        public const string Filter = "Filter";
        public const string Private = "Private";
        public const string PrivateFork = "Private · Fork";

        /// <summary>The switch's buttons' texts (<c>HostingTexts.Name</c>).</summary>
        public const string GitHubName = "GitHub";
        public const string GitLabName = "GitLab";
        public const string AzureDevOpsName = "Azure DevOps";

        /// <summary>The palette's neighbours (phase 6's 5.4).</summary>
        public const string CloneRepository = "Clone repository…";
        public const string InitRepository = "Init repository…";

        // ---- The failures pictured (B5; 11.2) -------------------------------------------------

        public const string GitLabServerError = "GitLab had a problem. Try again later.";
        public const string AzureDevOpsRefused = "Azure DevOps refused the token of Fernhill-Labs. Sign in to Azure DevOps in Settings, Accounts.";

        /// <summary>WP-B's line once per failed list (GitHub's byte for byte with the provider's name); <c>{0}</c> the request id.</summary>
        public const string GitLabListingFailed = "Listing GitLab repositories failed: ServerError: 500 Internal Server Error (request {0})";
        public const string AzureDevOpsListingFailed = "Listing Azure DevOps repositories failed: BadCredentials: Unauthorized (request {0})";

        /// <summary>The beginnings of WP-B's lines.</summary>
        public const string GitLabListingFailedStart = "Listing GitLab repositories failed:";
        public const string AzureDevOpsListingFailedStart = "Listing Azure DevOps repositories failed:";
        public const string GitHubListingFailedStart = "Listing GitHub repositories failed:";

        /// <summary>The HTTP core's warnings (5.2).</summary>
        public const string GitLabCoreWarning = "GitLab: GET /api/v4/projects failed: 500 (request {0}).";
        public const string AzureDevOpsCoreWarning = "Azure DevOps: GET /Fernhill-Labs/_apis/git/repositories failed: 401 (request {0}).";

        /// <summary>The session's warnings (9.6); the host goes through <c>WithPorts</c>.</summary>
        public const string GitLabSessionWarning = "GitLab: listing the account's repositories failed: ServerError, 500 Internal Server Error (request {0})";
        public const string AzureDevOpsSessionWarning = "Azure DevOps account Fernhill-Labs on 127.0.0.1:47397: Azure DevOps refused the token (the API, request {0}); it needs signing in again.";
        public const string AzureDevOpsSessionListingStart = "Azure DevOps: listing the account's repositories failed:";

        // ---- The lists (A2; the report's "Scenario facts part B uses") -----------------------

        public const string GitLabListRoute = "projects";
        public const string GitLabListPath = "/api/v4/projects";
        public const string GitLabListQuery = "membership=true&archived=false&order_by=last_activity_at&sort=desc&per_page=100";
        public const string AzureDevOpsListRoute = "repos:fernhill-labs";
        public const string AzureDevOpsListPath = "/Fernhill-Labs/_apis/git/repositories";
        public const string AzureDevOpsListQuery = "api-version=7.1";
        public const string GitHubListRoute = "user/repos";
        public const string GitHubListQuery = "per_page=100&sort=updated";

        /// <summary>GitLab's rows: most recently active first.</summary>
        public static IReadOnlyList<(string FullName, string? Detail)> GitLabRows { get; } =
        [
            ("northfield/platform/services/billing/invoice-generation-and-reconciliation", Private),
            ("ada/web-app", PrivateFork),
            ("northfield/platform/web-app", Private),
        ];

        /// <summary>Azure DevOps' rows: by name, then project, without case.</summary>
        public static IReadOnlyList<(string FullName, string? Detail)> AzureDevOpsRows { get; } =
        [
            ("Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service", Private),
            ("Fernhill-Labs/Mobile App/mobile-app", Private),
            ("Fernhill-Labs/Mobile App/mobile-app-fork", PrivateFork),
            ("Fernhill-Labs/Mobile App/sandbox", Private),
        ];

        /// <summary>GitHub's one row (B2's three accounts).</summary>
        public static IReadOnlyList<(string FullName, string? Detail)> GitHubRows { get; } = [("octo/web-app", null)];

        /// <summary>The two long names (B6) and the repository names they end with.</summary>
        public const string GitLabLongName = "northfield/platform/services/billing/invoice-generation-and-reconciliation";
        public const string GitLabLongEnd = "/invoice-generation-and-reconciliation";
        public const string AzureDevOpsLongName = "Fernhill-Labs/Mobile App/customer-onboarding-and-identity-verification-service";
        public const string AzureDevOpsLongEnd = "/customer-onboarding-and-identity-verification-service";

        /// <summary>The text a cut name begins with.</summary>
        public const string Ellipsis = "…";

        // ---- The clones (B7) -------------------------------------------------------------------

        public const string GitLabChosen = "northfield/platform/web-app";
        public const string AzureDevOpsChosen = "Fernhill-Labs/Mobile App/mobile-app";

        /// <summary>The URLs Choose fills in, on the fixed ports (through <c>WithPorts</c>).</summary>
        public const string GitLabCloneUrl = WebAppUrl;
        public const string AzureDevOpsCloneUrl = MobileAppUrl;

        public const string GitLabFolder = "web-app";
        public const string AzureDevOpsFolder = "mobile-app";

        /// <summary>The read of the user's <c>url.</c> rules before a bound clone (D331).</summary>
        public const string ConfigRead = "git config --show-scope --show-origin -z --list";

        /// <summary>The clones' calls as the call record writes them (a URL's user hidden).</summary>
        public const string GitLabCloneCall = "git clone --progress -- http://127.0.0.1:47398/northfield/platform/web-app.git web-app";
        public const string AzureDevOpsCloneCall = "git clone --progress -- http://***@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app mobile-app";

        /// <summary>The clone's tab's one binding: git's URL without its user.</summary>
        public const string GitLabBinding = WebAppUrl;
        public const string AzureDevOpsBinding = "http://127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app";

        /// <summary>The paths git's requests of each clone lie under.</summary>
        public const string GitLabGitPath = "/northfield/platform/web-app.git/";
        public const string AzureDevOpsGitPath = "/Fernhill-Labs/Mobile%20App/_git/mobile-app/";

        /// <summary>The prompt scope's line (7a design 4.6, D277).</summary>
        public const string GitLabAnswered = "web-app: Clone: git's credential get for 127.0.0.1:47398 answered from the account.";
        public const string AzureDevOpsAnswered = "mobile-app: Clone: git's credential get for 127.0.0.1:47397 answered from the account.";

        /// <summary>D322's line of each clone's tab.</summary>
        public const string GitLabListed = "web-app: listed 3 open pull requests of northfield/platform/web-app";
        public const string AzureDevOpsListed = "mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app";

        /// <summary>7a's 9.3 Warning, never expected.</summary>
        public const string ConfigUnread = "git's configuration could not be read";

        /// <summary>The bare repositories' folders in the copy, beside <c>mobile-app/</c>.</summary>
        public const string GitLabBare = "gitlab/northfield/platform/web-app.git";
        public const string AzureDevOpsBare = "azure-devops/Fernhill-Labs/Mobile App/_git/mobile-app";

        /// <summary>The cloned <c>main</c>'s subject in both.</summary>
        public const string MainSubject = "Add the docs folder";

        // ---- The palette (B8) ------------------------------------------------------------------

        public const string PaletteQuery = "Clone from";
        public const string GitLabQuery = "Clone from GitLab";
        public const string AzureDevOpsQuery = "Clone from Azure DevOps";
        public const string GitHubQuery = "Clone from GitHub";

        /// <summary>The empty box's commands, one after another (11.5).</summary>
        public static IReadOnlyList<string> PaletteRun { get; } = [CloneRepository, GitHubCommand, GitLabCommand, AzureDevOpsCommand, InitRepository];

        /// <summary>After typing <see cref="PaletteQuery"/>.</summary>
        public static IReadOnlyList<string> PaletteRows { get; } = [GitHubCommand, GitLabCommand, AzureDevOpsCommand];

        /// <summary>The palette's card (phase 6's 5.3): x 250 to 850 from y 96; 84 high with one row, 28 a row more.</summary>
        public const double PaletteLeft = 250;
        public const double PaletteRight = 850;
        public const double PaletteTop = 96;
        public const double PaletteThreeRows = 140;

        // ---- Positions (part B's conventions) --------------------------------------------------

        public const double FormLeft = 284;
        public const double FormRight = 676;
        public const double ButtonGap = 6;
        public const double CardLeft = 320;
        public const double CardRight = 780;
        public const double CardWidth = 460;
        public const double RowHeight = 28;
        public const double DetailGap = 12;
        public const double ListMinHeight = 58;
        public const double SwitchHeight = 28;
        public const double SwitchGap = 4;
        public const double SwitchToFilter = 8;
        public const double NoteSize = 11.5;
        public const double Tolerance = 1;

        // ---- Pictures --------------------------------------------------------------------------

        public const string B01GitLab = "b01a-gitlab-button";
        public const string B01Azure = "b01b-azure-button";
        public const string B02FromAnAccount = "b02a-gitlab-azure-from-an-account";
        public const string B02SwitchGitLab = "b02b-gitlab-azure-switch-gitlab";
        public const string B02SwitchAzureLoading = "b02c-gitlab-azure-switch-azure-loading";
        public const string B02SwitchAzure = "b02d-gitlab-azure-switch-azure";
        public const string B02Three = "b02e-three-switch-github";
        public const string B03GitLab = "b03a-gitlab-listed";
        public const string B03Azure = "b03b-azure-listed";
        public const string B04GitLab = "b04a-gitlab-loading";
        public const string B04Azure = "b04b-azure-loading";
        public const string B05GitLab = "b05a-gitlab-failed";
        public const string B05Azure = "b05b-azure-refused";
        public const string B06GitLab = "b06a-gitlab-long-name-tip";
        public const string B06Azure = "b06b-azure-long-name-tip";
        public const string B07GitLab = "b07a-gitlab-web-app-chosen";
        public const string B07Azure = "b07b-azure-mobile-app-chosen";
        public const string B08Commands = "b08a-three-palette-commands";
        public const string B08SignInFirst = "b08b-azure-sign-in-first";
        public const string B08Browser = "b08c-gitlab-azure-palette-browser";

        /// <summary>Every picture of part B, in the report's order.</summary>
        public static IReadOnlyList<string> Pictures { get; } =
        [
            B01GitLab, B01Azure, B02FromAnAccount, B02SwitchGitLab, B02SwitchAzureLoading, B02SwitchAzure, B02Three,
            B03GitLab, B03Azure, B04GitLab, B04Azure, B05GitLab, B05Azure, B06GitLab, B06Azure, B07GitLab, B07Azure,
            B08Commands, B08SignInFirst, B08Browser,
        ];

        /// <summary>The R pictures (11.7, 14.4): B3's two lists.</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [B03GitLab, B03Azure];

        /// <summary>The scenario's tokens, for "no token on screen" (named by their constants).</summary>
        public static IReadOnlyList<(string Name, string Token)> Tokens { get; } =
        [
            (nameof(GitHubToken), GitHubToken), (nameof(GitLabToken), GitLabToken),
            (nameof(GitLabLegacyToken), GitLabLegacyToken), (nameof(AzureDevOpsToken), AzureDevOpsToken),
        ];

        // ---- For WP-R, per provider (11.7) -----------------------------------------------------

        /// <summary>The form's button with <paramref name="provider"/> alone signed in.</summary>
        public static string ButtonOf(HostingProvider provider) => provider switch
        {
            HostingProvider.GitLab => GitLabButton,
            HostingProvider.AzureDevOps => AzureDevOpsButton,
            _ => FromGitHubButton,
        };

        /// <summary>The browser's title on <paramref name="provider"/>.</summary>
        public static string TitleOf(HostingProvider provider) => provider switch
        {
            HostingProvider.GitLab => GitLabTitle,
            HostingProvider.AzureDevOps => AzureDevOpsTitle,
            _ => GitHubTitle,
        };

        /// <summary>The provider's rows in the browser's order.</summary>
        public static IReadOnlyList<(string FullName, string? Detail)> RowsOf(HostingProvider provider) => provider switch
        {
            HostingProvider.GitLab => GitLabRows,
            HostingProvider.AzureDevOps => AzureDevOpsRows,
            _ => GitHubRows,
        };

        /// <summary>The row to choose (B7): GitLab's <c>northfield/platform/web-app</c>, Azure DevOps' <c>mobile-app</c>.</summary>
        public static string ChosenOf(HostingProvider provider) => provider == HostingProvider.AzureDevOps ? AzureDevOpsChosen : GitLabChosen;

        /// <summary>The chosen row's clone URL on the fixed ports; give it to <c>WithPorts</c>.</summary>
        public static string CloneUrlOf(HostingProvider provider) => provider == HostingProvider.AzureDevOps ? AzureDevOpsCloneUrl : GitLabCloneUrl;

        /// <summary>The clone's folder name.</summary>
        public static string FolderOf(HostingProvider provider) => provider == HostingProvider.AzureDevOps ? AzureDevOpsFolder : GitLabFolder;

        /// <summary>The R list picture's name.</summary>
        public static string ListedPictureOf(HostingProvider provider) => provider == HostingProvider.AzureDevOps ? B03Azure : B03GitLab;
    }
}
