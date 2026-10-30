namespace VisualCommit.VisualTests.Phase7a;

public static partial class Phase7aExpectations
{
    /// <summary>
    /// Part B, browse and clone (docs/test-reports/phase-7a.md, part B; design 9.2 to 9.7): its
    /// texts, the list's rows in order, the clone's URL and folder, the palette's query, picture
    /// names, the real-window picture, places and tolerances, written from the report and not from
    /// the App's texts (<c>Phase7aExpectationTests.B</c> compares them). Compiled by the real-window
    /// pass too.
    /// </summary>
    public static class PartB
    {
        /// <summary>Every token part B could meet, for the tripwire and the screen's check.</summary>
        public static IReadOnlyList<(string Name, string Token)> Tokens { get; } =
        [
            (nameof(OctoToken), OctoToken),
            (nameof(OctoClassicToken), OctoClassicToken),
            (nameof(AdaToken), AdaToken),
        ];

        // ---- Design 9.2's texts ----------------------------------------------------------------

        public const string FromGitHubButton = "From GitHub…";
        public const string DialogTitle = "Clone from GitHub";
        public const string PaletteCommand = "Clone from GitHub…";
        public const string Loading = "Loading repositories…";
        public const string Empty = "No repositories to list";
        public const string MoreExist = "Only the 1,000 most recently updated repositories are listed.";
        public const string SomeHiddenBySso = "Some organisations' repositories are hidden until the token is authorised for single sign-on.";
        public const string Unexpected = "The repositories could not be loaded.";
        public const string Choose = "Choose";
        public const string Cancel = "Cancel";
        public const string Filter = "Filter";
        public const string Private = "Private";
        public const string Fork = "Fork";
        public const string Archived = "Archived";
        public const string DetailSeparator = " · ";

        // ---- HostingTexts.Describe for the two failures pictured (B5) -------------------------

        public const string ServerErrorText = "GitHub had a problem. Try again later.";
        public const string RefusedOcto = "GitHub refused the token of octo. Sign in to GitHub in Settings, Accounts.";

        // ---- The palette (B7, B8; phase 6's 5.4) ------------------------------------------------

        /// <summary>What the checks type in the palette.</summary>
        public const string PaletteQuery = "Clone from GitHub";
        public const string CloneRepositoryCommand = "Clone repository…";
        public const string InitRepositoryCommand = "Init repository…";

        /// <summary>The palette's card with one row: 600 wide at x 250 to 850, from y 96, 84 high.</summary>
        public static readonly (double X, double Y, double Width, double Height) PaletteCardOneRow = (250, 96, 600, 84);

        // ---- The scenario's list (part B's facts, with ruling 11's description) ---------------

        /// <summary>octo/tool's description (the report's open point 11, the lead's ruling).</summary>
        public const string ToolDescription = "Small command-line tools.";

        /// <summary>The browser's rows of the scenario, in GitHub's order (most recently updated first).</summary>
        public static IReadOnlyList<(string FullName, string? Detail)> Rows { get; } =
        [
            (OctoHello, Private),
            (OctoTool, Fork + DetailSeparator + ToolDescription),
            (OctoNotes, Private),
            (OctoWebsite, Archived),
        ];

        /// <summary>How many API-only repositories B9 adds before its start.</summary>
        public const int ListedAdded = 1001;

        /// <summary>The list's length at the provider's cap of ten pages of 100.</summary>
        public const int ListedCap = 1000;

        /// <summary>B9's added repository number <paramref name="index"/>: <c>octo/listed-0001</c>.</summary>
        public static string Listed(int index) => FormattableString.Invariant($"octo/listed-{index:0000}");

        /// <summary>The list's query (design 6.3): its first page.</summary>
        public const string ListQuery = "per_page=100&sort=updated";

        /// <summary>The fake's route of the account's list.</summary>
        public const string ListRoute = "user/repos";

        // ---- The clone (B6) --------------------------------------------------------------------

        /// <summary>octo/hello's plain clone URL on <paramref name="port"/> (ruling 5).</summary>
        public static string HelloCloneUrl(int port) => WithPort("http://127.0.0.1:47395/octo/hello.git", port);

        public const string HelloFolder = "hello";

        /// <summary>The clone's commit at <c>main</c>: hello's <c>origin/main</c>.</summary>
        public const string HelloMainSubject = "Add the docs folder";

        /// <summary>The binding's account (<c>AccountKey.Id</c>).</summary>
        public static string OctoAccountId(int port) => WithPort("github:127.0.0.1:47395:octo", port);

        /// <summary>The token's key in the store, as <c>FakeTokenStore.Calls</c> names it.</summary>
        public static string OctoKey(int port) => OctoAccountId(port);

        /// <summary>The host as log lines name it.</summary>
        public static string Host(int port) => WithPort("127.0.0.1:47395", port);

        /// <summary>9.3's outside read of git's configuration, before a bound clone (D331).</summary>
        public const string ConfigRead = "git config --show-scope --show-origin -z --list";

        /// <summary>The clone's command, run in the parent folder.</summary>
        public static string CloneCommand(int port) => $"git clone --progress -- {HelloCloneUrl(port)} {HelloFolder}";

        /// <summary>4.6's line of the clone's prompt scope when git's <c>get</c> is answered from the account.</summary>
        public static string AnsweredFromTheAccount(int port) => $"hello: Clone: git's credential get for {Host(port)} answered from the account.";

        /// <summary>
        /// The first git that sends the account's token with its first request to a bound URL
        /// (<c>http.&lt;url&gt;.proactiveAuth</c>, 4.6, D340): from it on B6's clone starts with a
        /// request answered 200; older git's starts with one without credentials, which the private
        /// repository refuses (2.2).
        /// </summary>
        public static readonly Core.Git.GitVersion ProactiveAuthVersion = new(2, 46, 0);

        /// <summary>D322's line once hello's pull requests are listed.</summary>
        public const string HelloListed = "hello: listed 4 open pull requests of octo/hello";

        // ---- Log lines of a failed list (part B's texts, "Log lines") --------------------------

        public const string ListFailedPrefix = "Listing GitHub repositories failed:";
        public const string ListFailedServerError = "Listing GitHub repositories failed: ServerError: Server Error (request {0})";
        public const string ListFailedBadCredentials = "Listing GitHub repositories failed: BadCredentials: Bad credentials (request {0})";
        public const string ClientFailed = "GitHub: GET /api/v3/user/repos failed: {1} (request {0}).";
        public const string SessionListFailedServerError = "GitHub: listing the account's repositories failed: ServerError, Server Error (request {0})";
        public const string SessionListFailedPrefix = "GitHub: listing the account's repositories failed:";
        public const string SessionRefusedOcto = "GitHub account octo on {1}: GitHub refused the token (the API, request {0}); it needs signing in again.";
        public const string ConfigNotRead = "git's configuration could not be read";

        // ---- Places (1100×700) -----------------------------------------------------------------

        /// <summary>The clone form's left and right edges on an empty tab (phase 1's 13a).</summary>
        public const double FormLeft = 284;
        public const double FormRight = 676;

        /// <summary>The gap between the URL box and "From GitHub…".</summary>
        public const double ButtonGap = 6;

        /// <summary>The browser's card on the wide dialog card.</summary>
        public const double CardLeft = 320;
        public const double CardRight = 780;
        public const double CardWidth = 460;

        /// <summary>The list's least and largest heights inside its border, and a row's height.</summary>
        public const double ListMinHeight = 58;
        public const double ListMaxHeight = 232;
        public const double RowHeight = 28;

        /// <summary>The whole rows the list shows at its largest.</summary>
        public const int RowsShown = 8;

        /// <summary>The gap between a row's full name and its detail (a <c>PickRowPanel</c>).</summary>
        public const double DetailGap = 12;

        /// <summary>The detail's and the lines' size.</summary>
        public const double NoteSize = 11.5;

        /// <summary>Positions come from the controls' bounds, ±1.</summary>
        public const double Tolerance = 1;

        // ---- Pictures --------------------------------------------------------------------------

        public const string B01aFormSignedIn = "b01a-form-signed-in";
        public const string B02aLoading = "b02a-loading";
        public const string B03aListed = "b03a-listed";
        public const string B04aFiltered = "b04a-filtered";
        public const string B05aFailed = "b05a-failed";
        public const string B05bRefused = "b05b-refused";
        public const string B06aChosen = "b06a-chosen";
        public const string B07aPaletteCommand = "b07a-palette-command";
        public const string B07bSignInFirst = "b07b-sign-in-first";
        public const string B09aCapAndSso = "b09a-cap-and-sso";

        /// <summary>Every picture of part B, in the order its methods take them.</summary>
        public static IReadOnlyList<string> Pictures { get; } =
        [
            B01aFormSignedIn, B02aLoading, B04aFiltered,
            B07aPaletteCommand, B07bSignInFirst, B03aListed, B06aChosen,
            B09aCapAndSso, B05aFailed, B05bRefused,
        ];

        /// <summary>The pictures the real window repeats (13.4 B): the browser listed.</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [B03aListed];
    }
}
