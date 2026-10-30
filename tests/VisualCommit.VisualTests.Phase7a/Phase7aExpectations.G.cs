using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase7a;

public static partial class Phase7aExpectations
{
    /// <summary>
    /// Part G, the graph's badge and avatars (docs/test-reports/phase-7a.md, part G; design 11.2,
    /// 11.3, 11.5): <see cref="PrepareAsync"/> and its data, the prepared <c>hello</c>'s rows and
    /// labels signed in and as today, the tooltips, the avatars' colours and places, the picture names
    /// and the R picture, written from the report and not from the App's texts. Compiled by the
    /// real-window pass too.
    /// </summary>
    public static class PartG
    {
        // ---- PrepareAsync (11.5; open point 37) ---------------------------------------------------

        public const string PreparedPath = "src/login.txt";
        public const string PreparedContent = "login form\nremember me\n";
        public const string PreparedMessage = "Remember the login";

        /// <summary>
        /// On the copy's <c>hello</c> before the app starts (11.5): <c>feature/login</c> one commit
        /// ahead of <c>origin/feature/login</c>, so the two labels sit on rows 0 and 5. The commit is
        /// the newest (TempRepo's clock, 12:12), Test Author's.
        /// </summary>
        public static async Task PrepareAsync(TempRepo hello)
        {
            ArgumentNullException.ThrowIfNull(hello);
            await hello.CheckoutAsync("feature/login");
            await hello.CommitFileAsync(PreparedPath, PreparedContent, PreparedMessage);
            await hello.CheckoutAsync("main");
        }

        // ---- The prepared hello (part G's scenario facts) -------------------------------------------

        /// <summary>The row G1 to G7 select: <c>Update the readme</c> by Octo Cat, with the draft's badge.</summary>
        public const int Row = 4;

        /// <summary>Row <see cref="Row"/>'s author's address.</summary>
        public const string Author = OctoEmail;

        /// <summary>The prepared graph's lanes (open point 37), which the real window's <c>GraphRowPoint(row, lanes)</c> takes.</summary>
        public const int Lanes = 5;

        public const string TestAuthorEmail = "author@example.com";
        public const string OctoAuthorLine = "Octo Cat <octo@example.com>";
        public const string AdaAuthorLine = "Ada Lovelace <ada@example.com>";
        public const string TestAuthorLine = "Test Author <author@example.com>";

        /// <summary>The prepared graph's rows: subject, time, author's name and address.</summary>
        public static IReadOnlyList<(string Subject, string Time, string Name, string Email)> Rows { get; } =
        [
            (PreparedMessage, "2026-01-01 12:12", "Test Author", TestAuthorEmail),
            ("Local change", "2026-01-01 12:10", "Test Author", TestAuthorEmail),
            ("Try an idea", "2026-01-01 12:06", "Test Author", TestAuthorEmail),
            ("Shorten long labels", "2026-01-01 12:05", "Octo Cat", OctoEmail),
            ("Update the readme", "2026-01-01 12:04", "Octo Cat", OctoEmail),
            ("Add the login form", "2026-01-01 12:03", "Ada Lovelace", AdaEmail),
            ("Add the docs folder", "2026-01-01 12:02", "Test Author", TestAuthorEmail),
            ("Add the greeting", "2026-01-01 12:01", "Ada Lovelace", AdaEmail),
            ("Initial commit", "2026-01-01 12:00", "Octo Cat", OctoEmail),
        ];

        /// <summary>One label as the facts' table gives it: the text as drawn, the whole name, the kind's name, the ref, the right edge (±1) and the badge's number (0: none).</summary>
        public sealed record Label(string Text, string FullText, string Kind, string? RefName, double Right, int Badge = 0);

        private const string Long = "origin/" + LongBranch;

        /// <summary>Every row's labels signed in with octo/hello's list (the facts' table's signed-in column), each from x 6.</summary>
        public static IReadOnlyList<IReadOnlyList<Label>> SignedInLabels { get; } =
        [
            [new("feature/login", "feature/login", "LocalBranch", "refs/heads/feature/login", 117.8, 1)],
            [new("local-work", "local-work", "LocalBranch", "refs/heads/local-work", 74.6)],
            [new("origin/topic/no-pr", "origin/topic/no-pr", "RemoteBranch", "refs/remotes/origin/topic/no-pr", 113.3)],
            [new("origin/featur…", Long, "RemoteBranch", "refs/remotes/" + Long, 126.0, 3)],
            [new("origin/docs/r…", "origin/docs/readme", "RemoteBranch", "refs/remotes/origin/docs/readme", 128.2, 2)],
            [new("origin/featur…", "origin/feature/login", "RemoteBranch", "refs/remotes/origin/feature/login", 124.1, 1)],
            [new("main", "main", "CurrentBranch", "refs/heads/main", 56.8), new("+1", "origin/main", "RemoteBranch", null, 85.5)],
            [],
            [],
        ];

        /// <summary>Every row's labels as today (off, signed out, not GitHub).</summary>
        public static IReadOnlyList<IReadOnlyList<Label>> TodayLabels { get; } =
        [
            [new("feature/login", "feature/login", "LocalBranch", "refs/heads/feature/login", 86.4)],
            [new("local-work", "local-work", "LocalBranch", "refs/heads/local-work", 74.6)],
            [new("origin/topic/no-pr", "origin/topic/no-pr", "RemoteBranch", "refs/remotes/origin/topic/no-pr", 113.3)],
            [new("origin/feature/a-ve…", Long, "RemoteBranch", "refs/remotes/" + Long, 127.9)],
            [new("origin/docs/readme", "origin/docs/readme", "RemoteBranch", "refs/remotes/origin/docs/readme", 123.0)],
            [new("origin/feature/login", "origin/feature/login", "RemoteBranch", "refs/remotes/origin/feature/login", 121.0)],
            [new("main", "main", "CurrentBranch", "refs/heads/main", 56.8), new("+1", "origin/main", "RemoteBranch", null, 85.5)],
            [],
            [],
        ];

        /// <summary>The badges' pull requests: number, title, draft.</summary>
        public static IReadOnlyDictionary<int, (string Title, bool Draft)> Badges { get; } = new Dictionary<int, (string Title, bool Draft)>
        {
            [1] = ("Add the login form", false),
            [2] = ("Update the readme", true),
            [3] = ("Shorten long labels", false),
        };

        /// <summary>Row 0's label: the name whole to x 80.4, the icon from 86.4, <c>#1</c> from 99.4 (G1).</summary>
        public const double LoginNameRight = 80.4;
        public const double LoginIconLeft = 86.4;
        public const double LoginNumberLeft = 99.4;

        // ---- Tooltips (11.2) ---------------------------------------------------------------------

        public const string LoginTip = "feature/login (pull request #1: Add the login form)";
        public const string LongTip = Long + " (pull request #3: Shorten long labels)";
        public const string ReadmeTip = "origin/docs/readme (draft pull request #2: Update the readme)";
        public const string MainTip = "main\norigin/main";

        // ---- The avatars (11.3) --------------------------------------------------------------------

        public const string AvatarName = "Author's avatar";
        public const string HelloShowing = "hello: showing GitHub avatars for 2 authors";
        public const string HelloListed = "hello: listed 4 open pull requests of octo/hello";

        /// <summary>The fake's images (2.3): octo's and ada's colours, a white square in the centre; ±24 per channel.</summary>
        public const string OctoColour = "#2F81F7";
        public const string AdaColour = "#1F883D";
        public const int ColourTolerance = 24;

        /// <summary>At 1920×1080 the graph's Author column: x 928 to 1068; an image at x + 8 to + 24, the row's top + 5 to + 21; the name from + 30, without one from + 8.</summary>
        public const double AuthorX = 928;
        public const double AuthorRight = 1068;

        /// <summary>The rows with octo's and ada's images, and without one.</summary>
        public static IReadOnlyList<int> OctoRows { get; } = [3, 4, 8];
        public static IReadOnlyList<int> AdaRows { get; } = [5, 7];
        public static IReadOnlyList<int> BareRows { get; } = [0, 1, 2, 6];

        public const string CommitsQuery = "sha=main&per_page=100";
        public const string AvatarQuery = "v=4&s=64";
        public const string AuthorsFile = "authors/github_127.0.0.1_47395_octo_hello.json";

        /// <summary><c>avatars/index.json</c>'s users (part G's facts): octo's and ada's ids, each with its image; the dates are not asserted.</summary>
        public static IReadOnlyList<(string User, string File)> IndexUsers { get; } = [("1001", "1001.png"), ("1003", "1003.png")];

        /// <summary>
        /// <see cref="AuthorsFile"/>'s entries in e-mail order (part G's facts): ada 1003, Test Author
        /// without a user, octo 1001, with their avatar URLs on port 47395 (<c>WithPort</c> for the
        /// fake's); the dates are not asserted.
        /// </summary>
        public static IReadOnlyList<(string Email, long? UserId, string? AvatarUrl)> AuthorsEntries { get; } =
        [
            (AdaEmail, AdaId, "http://127.0.0.1:47395/avatars/u/1003?v=4"),
            (TestAuthorEmail, null, null),
            (OctoEmail, OctoId, "http://127.0.0.1:47395/avatars/u/1001?v=4"),
        ];

        /// <summary>G7's Warnings' beginnings (WP-G's client, WP-S's session).</summary>
        public const string CommitsFailedPrefix = "GitHub: GET /api/v3/repos/octo/hello/commits failed: 500 (request ";
        public const string AuthorsFailedPrefix = "GitHub: reading the authors of octo/hello failed: ServerError, Server Error (request ";

        // ---- Pictures ---------------------------------------------------------------------------------

        public const string G01a = "g01a-badges-and-avatar";
        public const string G01b = "g01b-badge-tooltip";
        public const string G02a = "g02a-cut-badge-tooltip";
        public const string G03a = "g03a-unknown-author-details";
        public const string G03b = "g03b-avatars-1920";
        public const string G04a = "g04a-light-1920";
        public const string G05a = "g05a-not-github-off";
        public const string G05b = "g05b-not-github-signed-in";
        public const string G06a = "g06a-signed-out-1920";
        public const string G07a = "g07a-authors-failed";

        public static IReadOnlyList<string> Pictures { get; } = [G01a, G01b, G02a, G03a, G03b, G04a, G05a, G05b, G06a, G07a];

        /// <summary>The real window repeats <c>g01a</c> (13.4 G).</summary>
        public static IReadOnlyList<string> RealWindowPictures { get; } = [G01a];
    }
}
