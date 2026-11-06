using System.Globalization;
using Avalonia.Media;
using VisualCommit.Core.Hosting;

namespace VisualCommit.VisualTests.Phase7b;

public static partial class Phase7bExpectations
{
    /// <summary>
    /// Part A, accounts and sign-in (docs/test-reports/phase-7b.md, part A; design 10.2 to 10.7): its
    /// texts, picture names, real-window pictures, places, part A's own made-up values, and for WP-R
    /// each provider's sign-in requests as route keys in order, the organization texts typed, the box
    /// with the keyboard focus in each dialog state and the three-account picture's place, written
    /// from the report and not from the App's texts (<c>Phase7bExpectationTests.A</c> compares them).
    /// Compiled by the real-window pass too: it uses nothing of the 7b project.
    /// </summary>
    public static class PartA
    {
        // ---- Part A's own values (the report's "Part A's own values") ----------------------------

        /// <summary>The organization as the scenario names it, typed in A6, A10's second try, A12 and by WP-R's Azure DevOps launch.</summary>
        public const string Organization = Phase7bExpectations.Organization;

        /// <summary>The organization in lower case, typed in A2 and by WP-R's three-account launch; the account shows the server's spelling.</summary>
        public const string OrganizationLower = "fernhill-labs";

        /// <summary>In the grammar, unknown to the fake: connectionData's 404 (A10).</summary>
        public const string UnknownOrganization = "Fernhill-Lab";

        /// <summary>Outside the grammar (a space): refused without a request (A11).</summary>
        public const string NotAnOrganization = "Fernhill Labs";

        /// <summary>GitLab's <c>glpat-</c> form, never issued: GitLab's 401 (A8).</summary>
        public const string UnknownGitLabToken = "glpat-FAKE-nobody-9";

        /// <summary>The fake's <c>azdo_FAKE-</c> form, never issued: no provider's form by <c>TokenFormOf</c>, PAT-shaped to the fake: the empty 401 (A8).</summary>
        public const string UnknownAzureDevOpsToken = "azdo_FAKE-nobody-9";

        /// <summary>Letters, digits and hyphens, of no provider's form and in none of the fake's PAT forms: the Anonymous 200 (A9).</summary>
        public const string PlainValue = "plain-value-7";

        /// <summary>84 characters with <c>AZDO</c> at 0-based 76 (design 10.8's made-up form): Azure DevOps' by <c>TokenFormOf</c> (A11).</summary>
        public static readonly string AzureDevOpsForm = new string('a', 76) + "AZDO" + "abcd";

        /// <summary>Every token part A uses or types, for the tripwire and the screen's check ("No token on screen").</summary>
        public static IReadOnlyList<(string Name, string Token)> Tokens { get; } =
        [
            (nameof(GitHubToken), GitHubToken),
            (nameof(GitLabToken), GitLabToken),
            (nameof(GitLabLegacyToken), GitLabLegacyToken),
            (nameof(AzureDevOpsToken), AzureDevOpsToken),
            (nameof(UnknownGitLabToken), UnknownGitLabToken),
            (nameof(UnknownAzureDevOpsToken), UnknownAzureDevOpsToken),
            (nameof(PlainValue), PlainValue),
            (nameof(AzureDevOpsForm), AzureDevOpsForm),
        ];

        // ---- The section (10.2, 10.3; 7a's that part A keeps) ------------------------------------

        public const string AccountsButton = "Accounts";

        /// <summary>The one intro for the three providers (10.2).</summary>
        public const string Intro = "With a GitHub, GitLab or Azure DevOps account, VisualCommit lists and creates pull requests (merge requests on GitLab) and clones your repositories.";

        /// <summary>7a's sentence, which A1 measures the intro against.</summary>
        public const string Intro7a = "With a GitHub account, VisualCommit lists and creates pull requests, shows authors' pictures and clones your repositories.";

        public const string GitHubLabel = "GitHub";
        public const string GitLabLabel = "GitLab";
        public const string AzureDevOpsLabel = "Azure DevOps";
        public const string GitLabel = "Git";
        public const string Off = "GitHub is turned off for this session.";
        public const string SignedOut = "Not signed in.";
        public const string SignInButton = "Sign in…";
        public const string SignOutButton = "Sign out…";
        public const string Busy = "Signing out…";
        public const string InKeychain = "The token is kept in this computer's keychain.";
        public const string NoKeychain = "No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start.";
        public const string SessionEnded = "The token was kept for the last session only. Sign in again.";
        public const string UseForGit = "Use for git over HTTPS";

        public const string GitHubUseForGitNote = "Clone, fetch, pull and push to GitHub over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.";
        public const string GitLabUseForGitNote = "Clone, fetch, pull and push to GitLab over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.";

        /// <summary>Azure DevOps' switch note (D350; the report's open point 3).</summary>
        public const string AzureDevOpsUseForGitNote = "Clone, fetch, pull and push to Fernhill-Labs's repositories on Azure DevOps over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.";

        public const string GitLabRefusedNote = "GitLab refused the token. Sign in again with a new token.";
        public const string AzureDevOpsRefusedNote = "Azure DevOps refused the token. Sign in again with a new token.";

        /// <summary>The section list's buttons, top to bottom (7a's).</summary>
        public static IReadOnlyList<string> NavButtons { get; } = ["General", "Git", "Tools", "Keyboard shortcuts", AccountsButton];

        public static IReadOnlyList<string> NavIds { get; } = ["SettingsNavGeneral", "SettingsNavGit", "SettingsNavTools", "SettingsNavShortcuts", "SettingsNavAccounts"];

        /// <summary>The blocks' containers in D353's order.</summary>
        public static IReadOnlyList<string> BlockIds { get; } = ["AccountsGitHub", "AccountsGitLab", "AccountsAzureDevOps"];

        /// <summary>The id suffix of a provider's block (1.7): GitHub's keeps 7a's ids.</summary>
        public static string SuffixOf(HostingProvider provider) => provider switch
        {
            HostingProvider.GitHub => string.Empty,
            HostingProvider.GitLab => "GitLab",
            _ => "AzureDevOps",
        };

        /// <summary>7a's <paramref name="id"/> in the block of <paramref name="provider"/>.</summary>
        public static string IdOf(HostingProvider provider, string id) => id + SuffixOf(provider);

        /// <summary>The block's label: the provider's name.</summary>
        public static string LabelOf(HostingProvider provider) => provider switch
        {
            HostingProvider.GitHub => GitHubLabel,
            HostingProvider.GitLab => GitLabLabel,
            _ => AzureDevOpsLabel,
        };

        // ---- The confirmations (D291, D296; 10.2) ------------------------------------------------

        public const string GitLabSignOutTitle = "Sign out of GitLab?";
        public const string GitLabSignOutQuestionAda = "Sign out of GitLab as ada? VisualCommit removes its token from this computer and no longer lists merge requests. Git over HTTPS asks your own credential helpers again.";
        public const string AzureDevOpsSignOutTitle = "Sign out of Azure DevOps?";
        public const string AzureDevOpsSignOutQuestion = "Sign out of Azure DevOps for Fernhill-Labs? VisualCommit removes its token from this computer and no longer lists pull requests. Git over HTTPS asks your own credential helpers again.";
        public const string SignOutConfirm = "Sign out";

        // ---- The dialogs (10.4) ------------------------------------------------------------------

        public const string GitHubDialogTitle = "Add GitHub account";
        public const string GitLabDialogTitle = "Add GitLab account";
        public const string AzureDevOpsDialogTitle = "Add Azure DevOps account";

        public const string GitHubHelp = "Paste a personal access token from GitHub. A fine-grained token needs read and write access to Contents and Pull requests of your repositories; a classic token needs the repo scope. To push changes to workflow files, it also needs Workflows (fine-grained) or workflow (classic).";
        public const string GitLabHelp = "Paste a personal access token from GitLab. A fine-grained token needs User: Read and Project: Read among its user permissions, and Project: Read, Merge Request: Read and Create, and Code: Download and Push for the groups and projects you work in; a legacy token needs the api scope.";
        public const string AzureDevOpsHelp = "Type your organization, then paste a personal access token made for it in Azure DevOps with the scope Code (Read & write). The organization can be its name or its address on dev.azure.com or visualstudio.com.";

        public const string GitHubTokenLink = "Create a token on GitHub";
        public const string GitHubTokenLinkTip = "Opens GitHub's page for a new token in your browser.";
        public const string GitLabTokenLink = "Create a fine-grained token on GitLab";
        public const string GitLabTokenLinkTip = "Opens GitLab's form for a new fine-grained token in your browser.";
        public const string GitLabLegacyLink = "Or create a legacy token, filled in";
        public const string GitLabLegacyLinkTip = "Opens GitLab's form for a legacy token with the api scope in your browser.";
        public const string AzureDevOpsTokenLink = "Create a token on Azure DevOps";
        public const string AzureDevOpsTokenLinkTip = "Opens the organization's page for new tokens in your browser.";

        public const string OrganizationLabel = "Organization";
        public const string TokenLabel = "Token";
        public const string Checking = "Checking the token…";
        public const string Cancel = "Cancel";
        public const string SignIn = "Sign in";
        public const string Close = "Close";

        /// <summary>The masking character of a secret box (D103).</summary>
        public const char Mask = '•';

        /// <summary>Azure DevOps' card's greatest height without a status line (10.4; the report's open point 7).</summary>
        public const double AzureDevOpsCardMaxHeight = 345;

        // ---- The dialogs' errors (10.2) ----------------------------------------------------------

        public const string GitLabRefused401 = "GitLab refused the token: 401 Unauthorized.";
        public const string AzureDevOpsRefused = "Azure DevOps refused the token for Fernhill-Labs. Check that it was made for this organization and has not expired.";
        public const string AzureDevOpsAnonymous = "Azure DevOps did not take the token as a sign-in and answered as it does for a visitor. Paste the whole token, made for Fernhill-Labs.";
        public const string AzureDevOpsNoOrganization = "Azure DevOps has no organization named Fernhill-Lab.";
        public const string NotAToken = "The token can contain only letters, digits and punctuation, without spaces or line breaks.";
        public const string NotAnOrganizationError = "Type the organization's name, of letters, digits and hyphens, or its address on dev.azure.com or visualstudio.com.";
        public const string GitLabGivenAzureDevOpsToken = "This looks like an Azure DevOps token. Paste a token made on GitLab.";
        public const string GitHubGivenGitLabToken = "This looks like a GitLab token. Paste a token made on GitHub.";
        public const string AzureDevOpsGivenGitHubToken = "This looks like a GitHub token. Paste a token made for Fernhill-Labs on Azure DevOps.";
        public const string GitLabUnreachable = "Could not reach GitLab.";
        public const string AzureDevOpsUnreachable = "Could not reach Azure DevOps.";
        public const string GitHubUnreachable = "Could not reach GitHub.";

        // ---- The keyboard focus in each dialog state (10.4; for WP-R) ----------------------------

        /// <summary>Azure DevOps' dialog opened with an empty organization box.</summary>
        public const string FocusOpenedEmpty = "SignInOrganization";

        /// <summary>A dialog opened with its organization prefilled, or without that box.</summary>
        public const string FocusOpened = "SignInToken";

        /// <summary>After <c>NotFound</c>: the organization box's text selected.</summary>
        public const string FocusAfterNotFound = "SignInOrganization";

        /// <summary>After any other failure of the service: the token box's text selected.</summary>
        public const string FocusAfterRefusal = "SignInToken";

        // ---- The sign-in requests (the scenario facts; for WP-R) ----------------------------------

        /// <summary>GitHub's sign-in: one <c>GET api/v3/user</c>.</summary>
        public static IReadOnlyList<string> GitHubSignInRoutes { get; } = ["user"];

        /// <summary>GitLab's sign-in: one <c>GET /api/v4/user</c>, <c>Bearer</c>.</summary>
        public static IReadOnlyList<string> GitLabSignInRoutes { get; } = ["user"];

        /// <summary>Azure DevOps' sign-in: connectionData of the organization as typed (lower-cased key), then the core resource area.</summary>
        public static IReadOnlyList<string> AzureDevOpsSignInRoutes(string organization) =>
            ["connection:" + organization.ToLowerInvariant(), "areas"];

        /// <summary>The sign-in's query on Azure DevOps.</summary>
        public const string AzureDevOpsSignInQuery = "api-version=7.1-preview.1";

        /// <summary>The core resource area's id (1.4), in the second request's path.</summary>
        public const string CoreAreaId = "79134c72-4a58-4b42-976c-04e7115f32bf";

        /// <summary>Where the three-account picture is taken: at the section's end (its largest offset), never a pixel value (10.7, 14.3).</summary>
        public const string ThreeAccountsPlace = "the end";

        // ---- Keys, hosts, records, pages (the scenario facts) -------------------------------------

        public static string Host(int port) => string.Create(CultureInfo.InvariantCulture, $"127.0.0.1:{port}");

        public static string GitHubKey(int port = GitHubPort) => $"github:{Host(port)}:octo";

        public static string GitLabKey(int port = GitLabPort) => $"gitlab:{Host(port)}:ada";

        public static string AzureDevOpsKey(int port = AzureDevOpsPort) => $"azure-devops:{Host(port)}:fernhill-labs";

        public static string GitHubAvatarUrl(int port = GitHubPort) => string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/avatars/u/1001?v=4");

        public const long OctoId = 1001;
        public const long AdaGitLabId = 2001;

        public static string GitHubTokenPage(int port = GitHubPort) =>
            string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write");

        public static string GitLabTokenPage(int port = GitLabPort) =>
            string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/-/user_settings/personal_access_tokens/granular/new");

        public static string GitLabLegacyTokenPage(int port = GitLabPort) =>
            string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/-/user_settings/personal_access_tokens?name=VisualCommit&scopes=api");

        public static string AzureDevOpsTokenPage(int port = AzureDevOpsPort, string organization = Organization) =>
            string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/{organization}/_usersSettings/tokens");

        /// <summary>The start's log lines (9.2, 9.4), in D353's order, through <c>WithPorts</c>.</summary>
        public static IReadOnlyList<string> StartLines { get; } =
        [
            "GitHub: 127.0.0.1:47399.",
            "GitLab: 127.0.0.1:47398.",
            "Azure DevOps: 127.0.0.1:47397.",
            "GitHub: no account on 127.0.0.1:47399.",
            "GitLab: no account on 127.0.0.1:47398.",
            "Azure DevOps: no account on 127.0.0.1:47397.",
        ];

        /// <summary>A3's start with GitLab off: its construction lines.</summary>
        public static IReadOnlyList<string> StartLinesGitLabOff { get; } = ["GitHub: 127.0.0.1:47399.", "GitLab: off.", "Azure DevOps: 127.0.0.1:47397."];

        /// <summary>D322's Debug lines once a tab's list is applied.</summary>
        public const string MobileAppListed = "mobile-app: listed 3 open pull requests of Fernhill-Labs/Mobile App/mobile-app";

        public const string WebAppListed = "web-app: listed 3 open pull requests of northfield/platform/web-app";

        // ---- Places at 1100×700 (7a's part A; ±2 unless exact) -----------------------------------

        public const double Tolerance = 2;

        /// <summary>The intro's height against 7a's sentence, ±0.5 (10.9).</summary>
        public const double IntroTolerance = 0.5;

        public const int IntroLines = 2;

        public static readonly (double X, double Y, double Width, double Height) Card = (120, 40, 860, 620);
        public static readonly (double X, double Y) AccountsButtonAt = (140, 223);
        public const double NavWidth = 176;
        public const double NavHeight = 30;
        public const double NavGap = 2;
        public const double SectionX = 336;
        public const double LabelRight = 468;
        public const double ControlsX = 480;
        public const double ControlsRight = 944;
        public static readonly (double Left, double Right) DialogCard = (350, 750);

        /// <summary>An empty tab's title (A14 step 3's tab strip).</summary>
        public const string NewTab = "New tab";

        /// <summary>The screen's <c>Save</c> (7a's, phase 6's part S).</summary>
        public const string Save = "Save";

        /// <summary>Cancel and Save as phase 6's part S at 1100×700 (its S14 step 2): the row y 607 to 639, 8 apart, Save ending 22 inside the card's right.</summary>
        public static readonly (double Top, double Bottom) SettingsButtons = (607, 639);
        public const double SettingsButtonSpacing = 8;
        public const double SaveRightInset = 22;

        /// <summary>The section's rows 18 apart, the blocks 24, the buttons 10 below the texts and 8 apart; the link 6 below the help, GitLab's second 4 below the first.</summary>
        public const double RowSpacing = 18;
        public const double BlockSpacing = 24;
        public const double ButtonsGap = 10;
        public const double ButtonSpacing = 8;
        public const double LinkGap = 6;
        public const double LegacyLinkGap = 4;

        /// <summary>
        /// One line of <c>AccountUseForGitNoteAzureDevOps</c> in <c>a12b</c>: its lines are this far
        /// apart (±0.5), <c>settingsNote</c>'s 11.5 at the font's line spacing of about 1.21 as the
        /// note's layout gives it. A12 after step 4 (open point 2; the lead's ruling of 2026-11-06):
        /// the section scrolls at offset 0, and the note's bottom lies below the viewport by less than
        /// one of its lines, the last line's top inside the viewport, scrolled out of view, not cut.
        /// </summary>
        public const double A12NoteLine = 13.9;

        // ---- Colours (7a's theme brushes, dark) ---------------------------------------------------

        public static readonly Color Secondary = Color.Parse("#9BA3B0");
        public static readonly Color Warning = Color.Parse("#E0A23B");
        public static readonly Color Danger = Color.Parse("#E5606B");
        public static readonly Color Accent = Color.Parse("#7C8CFF");

        /// <summary>The theme's disabled foreground (<c>VcTextDisabledBrush</c>): Azure DevOps' token link while disabled (A6, A11; the lead's ruling of 2026-11-06).</summary>
        public static readonly Color Disabled = Color.Parse("#5F6775");

        /// <summary>A token link's cursor: the hand while enabled, the arrow while disabled (as the cursors' names read).</summary>
        public const string HandCursor = "Hand";
        public const string ArrowCursor = "Arrow";

        // ---- Pictures (the report's Screenshots column) --------------------------------------------

        public const string A01ThreeSignedOut = "a01a-three-signed-out";
        public const string A02GitHubSignedIn = "a02a-github-signed-in";
        public const string A02GitLabSignedIn = "a02b-gitlab-signed-in";
        public const string A02ThreeTop = "a02c-three-signed-in-top";
        public const string A02ThreeEnd = "a02d-three-signed-in-end";
        public const string A03GitLabOff = "a03a-gitlab-off";
        public const string A03ThreeOff = "a03b-three-off";
        public const string A04AzureSignedIn = "a04a-azure-signed-in";
        public const string A05GitLabDialog = "a05a-gitlab-dialog";
        public const string A05GitLabTokenMasked = "a05b-gitlab-token-masked";
        public const string A06AzureDialogEmpty = "a06a-azure-dialog-empty";
        public const string A06AzureTokenMasked = "a06b-azure-token-masked";
        public const string A06AzurePrefilled = "a06c-azure-prefilled";
        public const string A07AzureChecking = "a07a-azure-checking";
        public const string A08GitLabRefused = "a08a-gitlab-refused";
        public const string A08AzureRefused = "a08b-azure-refused";
        public const string A09AzureAnonymous = "a09a-azure-anonymous";
        public const string A10AzureNoOrganization = "a10a-azure-no-organization";
        public const string A11GitLabAzureToken = "a11a-gitlab-azure-token";
        public const string A11AzureNotAnOrganization = "a11b-azure-not-an-organization";
        public const string A11AzureGitHubToken = "a11c-azure-github-token";
        public const string A12GitLabNoKeychainDialog = "a12a-gitlab-no-keychain-dialog";
        public const string A12SessionOnly = "a12b-gitlab-azure-session-only";
        public const string A12SessionEnded = "a12c-gitlab-azure-session-ended";
        public const string A13GitLabQuestion = "a13a-gitlab-sign-out-question";
        public const string A13GitLabSignedOut = "a13b-gitlab-signed-out";
        public const string A13AzureQuestion = "a13c-azure-sign-out-question";
        public const string A14GitLabSwitchOff = "a14a-gitlab-switch-off";
        public const string A14AzureSwitchOffEnd = "a14b-azure-switch-off-end";

        /// <summary>The pictures the real window repeats (10.7, 14.4): A2's three accounts at the end, A5's GitLab dialog and A6's Azure DevOps dialog, each with a masked token.</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [A02ThreeEnd, A05GitLabTokenMasked, A06AzureTokenMasked];

        /// <summary>Every picture of part A, in the report's order.</summary>
        public static IReadOnlyList<string> Pictures { get; } =
        [
            A01ThreeSignedOut, A02GitHubSignedIn, A02GitLabSignedIn, A02ThreeTop, A02ThreeEnd, A03GitLabOff, A03ThreeOff,
            A04AzureSignedIn, A05GitLabDialog, A05GitLabTokenMasked, A06AzureDialogEmpty, A06AzureTokenMasked, A06AzurePrefilled,
            A07AzureChecking, A08GitLabRefused, A08AzureRefused, A09AzureAnonymous, A10AzureNoOrganization, A11GitLabAzureToken,
            A11AzureNotAnOrganization, A11AzureGitHubToken, A12GitLabNoKeychainDialog, A12SessionOnly, A12SessionEnded,
            A13GitLabQuestion, A13GitLabSignedOut, A13AzureQuestion, A14GitLabSwitchOff, A14AzureSwitchOffEnd,
        ];
    }
}
