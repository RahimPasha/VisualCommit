namespace VisualCommit.VisualTests.Phase7a;

public static partial class Phase7aExpectations
{
    /// <summary>
    /// Part A, accounts and sign-in (docs/test-reports/phase-7a.md, part A; design 8.2 to 8.7):
    /// its texts, picture names, real-window pictures, places and part A's own tokens, written from
    /// the report and not from the App's texts (<c>Phase7aExpectationTests.A</c> compares them).
    /// Compiled by the real-window pass too.
    /// </summary>
    public static class PartA
    {
        // ---- Part A's own tokens (part A's conventions) ----------------------------------------

        /// <summary>A token of the fine-grained form that the fake never issued: GitHub answers 401.</summary>
        public const string UnknownToken = "github_pat_FAKE-nobody-9";

        /// <summary><see cref="OctoToken"/>'s text with one space inside: refused before any request (D298).</summary>
        public const string TokenWithSpace = "github_pat_FAKE-octo -1";

        /// <summary>Every token part A uses or types, for the tripwire and the screen's check.</summary>
        public static IReadOnlyList<(string Name, string Token)> Tokens { get; } =
        [
            (nameof(OctoToken), OctoToken),
            (nameof(OctoClassicToken), OctoClassicToken),
            (nameof(AdaToken), AdaToken),
            (nameof(UnknownToken), UnknownToken),
            (nameof(TokenWithSpace), TokenWithSpace),
        ];

        // ---- The section (8.2, 8.3) ------------------------------------------------------------

        public const string AccountsButton = "Accounts";
        public const string GitHubLabel = "GitHub";
        public const string GitLabel = "Git";
        public const string Intro = "With a GitHub account, VisualCommit lists and creates pull requests, shows authors' pictures and clones your repositories.";
        public const string Off = "GitHub is turned off for this session.";
        public const string SignedOut = "Not signed in.";
        public const string SignInButton = "Sign in…";
        public const string SignOutButton = "Sign out…";
        public const string Busy = "Signing out…";
        public const string InKeychain = "The token is kept in this computer's keychain.";
        public const string NoKeychain = "No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start.";
        public const string SaveFailedLocked = "The token could not be saved in this computer's keychain: the keychain stayed locked. VisualCommit keeps it until it closes and asks for it again at the next start.";
        public const string Refused = "GitHub refused the token. Sign in again with a new token.";
        public const string TokenMissing = "The token is no longer in this computer's keychain. Sign in again.";
        public const string ReadFailedLocked = "The token could not be read from this computer's keychain: the keychain stayed locked. Sign in again.";
        public const string SessionEnded = "The token was kept for the last session only. Sign in again.";
        public const string SignOutNoteLocked = "Signed out, but the token could not be removed from this computer's keychain: the keychain stayed locked.";
        public const string UseForGit = "Use for git over HTTPS";
        public const string UseForGitNote = "Clone, fetch, pull and push to GitHub over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.";
        public const string UseForGitOldGit230 = "This needs Git 2.31 or newer. With Git 2.30.0, clone, fetch, pull and push still use your git credential helpers.";

        /// <summary>The section list's buttons, top to bottom.</summary>
        public static IReadOnlyList<string> NavButtons { get; } = ["General", "Git", "Tools", "Keyboard shortcuts", AccountsButton];

        /// <summary>The section list's buttons' automation ids, top to bottom.</summary>
        public static IReadOnlyList<string> NavIds { get; } = ["SettingsNavGeneral", "SettingsNavGit", "SettingsNavTools", "SettingsNavShortcuts", "SettingsNavAccounts"];

        // ---- The confirmation (D291, D296) -----------------------------------------------------

        public const string SignOutTitle = "Sign out of GitHub?";
        public const string SignOutQuestionOcto = "Sign out of GitHub as octo? VisualCommit removes its token and the authors' pictures it saved from this computer, and no longer lists pull requests. Git over HTTPS asks your own credential helpers again.";
        public const string SignOutConfirm = "Sign out";

        // ---- The dialog (8.4) ------------------------------------------------------------------

        public const string DialogTitle = "Add GitHub account";
        public const string Help = "Paste a personal access token from GitHub. A fine-grained token needs read and write access to Contents and Pull requests of your repositories; a classic token needs the repo scope. To push changes to workflow files, it also needs Workflows (fine-grained) or workflow (classic).";
        public const string TokenLink = "Create a token on GitHub";
        public const string TokenLinkTip = "Opens GitHub's page for a new token in your browser.";

        /// <summary>The token page, after the fake's web base (D295).</summary>
        public const string TokenPage = "settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write";

        public const string TokenLabel = "Token";
        public const string Checking = "Checking the token…";
        public const string NotAToken = "The token can contain only letters, digits and punctuation, without spaces or line breaks.";
        public const string BadCredentials = "GitHub refused the token: Bad credentials.";
        public const string SaveFailedDialogOcto = "Signed in as octo, but the token could not be saved in this computer's keychain: the keychain stayed locked. VisualCommit keeps it until it closes and asks for it again at the next start.";
        public const string Cancel = "Cancel";
        public const string SignIn = "Sign in";
        public const string Close = "Close";

        /// <summary>The masking character of a secret box (D103).</summary>
        public const char Mask = '•';

        /// <summary>The keychain's reason the checks provoke (D268).</summary>
        public const string Locked = "the keychain stayed locked";

        // ---- WP-C's refused-token dialog (A9; 4.7, D276, D330) ---------------------------------

        public const string FetchFailedTitle = "Fetch failed";
        public const string GitRefusedOcto = "GitHub refused git's credentials for octo. If the token has expired or was revoked, sign in to GitHub again in Settings, Accounts.";
        public const string OpenSettingsButton = "Open settings…";

        /// <summary>Git's key line of A9's fetch on the fake's port (Git 2.56.0.windows.4).</summary>
        public static string AuthenticationFailed(int port = ServerPort) => $"fatal: Authentication failed for 'http://127.0.0.1:{port}/octo/hello.git/'";

        public const string CouldNotFetchOrigin = "error: Could not fetch origin";
        public const string FetchCommand = "git fetch --progress origin";

        // ---- accounts.json (7.2, D336) ---------------------------------------------------------

        public static string Host(int port = ServerPort) => $"127.0.0.1:{port}";

        public static string AvatarUrl(int port = ServerPort) => $"http://127.0.0.1:{port}/avatars/u/1001?v=4";

        /// <summary>The token's key in the store's calls: <c>github:127.0.0.1:47395:octo</c>.</summary>
        public static string OctoKey(int port = ServerPort) => $"github:127.0.0.1:{port}:octo";

        /// <summary>The scenario facts' one binding of <c>hello</c>.</summary>
        public static string HelloUrl(int port = ServerPort) => $"http://127.0.0.1:{port}/octo/hello.git";

        /// <summary>WP-S's Debug line once <c>hello</c>'s list is applied (D322).</summary>
        public const string HelloListed = "hello: listed 4 open pull requests of octo/hello";

        // ---- Places (8.3, 8.4; ±2 unless exact) ------------------------------------------------

        public const double Tolerance = 2;

        /// <summary>The settings card at 1100×700, exactly.</summary>
        public static readonly (double X, double Y, double Width, double Height) Card = (120, 40, 860, 620);

        /// <summary>The settings card at 1920×1080, exactly (D216's largest).</summary>
        public static readonly (double X, double Y, double Width, double Height) CardFull = (530, 200, 860, 680);

        /// <summary>"Accounts" at 1100×700 and at 1920×1080: 176 × 30, 2 below "Keyboard shortcuts".</summary>
        public static readonly (double X, double Y) AccountsButtonAt = (140, 223);

        public static readonly (double X, double Y) AccountsButtonAtFull = (550, 383);

        public const double NavWidth = 176;
        public const double NavHeight = 30;
        public const double NavGap = 2;

        /// <summary>The section's text from x 336, its labels to 468, the row's controls from 480 to 944, at 1100×700.</summary>
        public const double SectionX = 336;

        public const double LabelRight = 468;
        public const double ControlsX = 480;
        public const double ControlsRight = 944;

        /// <summary>The same at 1920×1080: the section from 746, the controls from 890.</summary>
        public const double SectionXFull = 746;

        public const double ControlsXFull = 890;

        /// <summary>Cancel and Save at 1920×1080: y 827 to 859.</summary>
        public static readonly (double Top, double Bottom) ButtonsFull = (827, 859);

        /// <summary>The dialog's card: 400 wide, x 350 to 750 at 1100×700 and 760 to 1160 at 1920×1080, centred vertically.</summary>
        public static readonly (double Left, double Right) DialogCard = (350, 750);

        public static readonly (double Left, double Right) DialogCardFull = (760, 1160);

        /// <summary>The section's rows are 18 apart; the buttons 10 below the texts and 8 apart; the link 6 below the help.</summary>
        public const double RowSpacing = 18;

        public const double ButtonsGap = 10;
        public const double ButtonSpacing = 8;
        public const double LinkGap = 6;

        // ---- Pictures (the report's Screenshots column) ----------------------------------------

        public const string A01SignedOut = "a01a-signed-out";
        public const string A02DialogEmpty = "a02a-dialog-empty";
        public const string A02TokenMasked = "a02b-token-masked";
        public const string A03Checking = "a03a-checking";
        public const string A04NotAToken = "a04a-not-a-token";
        public const string A04Refused = "a04b-refused";
        public const string A05SignedIn = "a05a-signed-in";
        public const string A05SignedInFull = "a05b-signed-in-1920";
        public const string A05SignedInFullLight = "a05c-signed-in-1920-light";
        public const string A06NoKeychainDialog = "a06a-no-keychain-dialog";
        public const string A06SessionOnly = "a06b-session-only";
        public const string A07SaveFailedDialog = "a07a-save-failed-dialog";
        public const string A07SaveFailedSection = "a07b-save-failed-section";
        public const string A08Question = "a08a-sign-out-question";
        public const string A08SigningOut = "a08b-signing-out";
        public const string A08SignedOut = "a08c-signed-out";
        public const string A08SignOutNote = "a08d-sign-out-note";
        public const string A09NeedsSignInApi = "a09a-needs-sign-in-api";
        public const string A09GitRefused = "a09b-git-refused";
        public const string A09NeedsSignInGit = "a09c-needs-sign-in-git";
        public const string A10SessionEnded = "a10a-session-ended";
        public const string A10StillSignedIn = "a10b-still-signed-in";
        public const string A10TokenGone = "a10c-token-gone";
        public const string A11SwitchOff = "a11a-switch-off";

        /// <summary>The pictures the real window repeats (design 13.4 A): A1's, A2's masked token and A5's signed-in row.</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [A01SignedOut, A02TokenMasked, A05SignedIn];

        /// <summary>Every picture of part A, in the report's order.</summary>
        public static IReadOnlyList<string> Pictures { get; } =
        [
            A01SignedOut, A02DialogEmpty, A02TokenMasked, A03Checking, A04NotAToken, A04Refused, A05SignedIn, A05SignedInFull,
            A05SignedInFullLight, A06NoKeychainDialog, A06SessionOnly, A07SaveFailedDialog, A07SaveFailedSection, A08Question,
            A08SigningOut, A08SignedOut, A08SignOutNote, A09NeedsSignInApi, A09GitRefused, A09NeedsSignInGit, A10SessionEnded,
            A10StillSignedIn, A10TokenGone, A11SwitchOff,
        ];
    }
}
