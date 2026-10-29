using System.Globalization;
using Avalonia.Media;
using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// What phase 7a's checks compare against that more than one part uses (docs/phase-7a-design.md
/// 0.3, 2.6, 2.8): the phase's name, the window's sizes, the fake's port, and the names and numbers
/// of the scenario "hosting". Compiled by the real-window pass too, so it uses only the BCL,
/// Avalonia, xunit, Core, Git and Testing. The parts add <c>Phase7aExpectations.&lt;part&gt;.cs</c>.
/// </summary>
public static partial class Phase7aExpectations
{
    /// <summary>The phase as the docs write it: folders <c>artifacts/visual/phase-7a/</c> (design 0.3).</summary>
    public const string Phase = "7a";

    /// <summary>The window's size unless a check names another (design 0.5).</summary>
    public static readonly (int Width, int Height) DefaultSize = (1100, 700);

    /// <summary>The size a check names when it needs room.</summary>
    public static readonly (int Width, int Height) FullSize = (1920, 1080);

    /// <summary>The fake of GitHub's port (D265), whose URLs the pictures show.</summary>
    public const int ServerPort = AuthRemoteServer.Phase7aVisualChecksPort;

    /// <summary>
    /// <paramref name="text"/> with the fake's port in place of <paramref name="from"/> (47395,
    /// <see cref="ServerPort"/>, unless given), as <c>Phase6Expectations.WithPort</c> does for 47393:
    /// for a text that shows the fake's URL in a run whose fake fell back to a free port
    /// (<see cref="AuthRemoteServer.PortWasTaken"/>), which the report's notes then name (D265).
    /// </summary>
    public static string WithPort(string text, int port, int from = ServerPort)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace(
            from.ToString(CultureInfo.InvariantCulture),
            port.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    /// <summary>The fake's web address on <paramref name="port"/>: <c>http://127.0.0.1:47395/</c>.</summary>
    public static string WebBase(int port = ServerPort) => string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/");

    /// <summary>
    /// The height of a menu that fits the window (phase 3's 4a): 8 + 24 + 26 × items + 9 ×
    /// separators (<see cref="WindowLayout.MenuHeight"/>).
    /// </summary>
    public static double MenuHeight(int items, int separators) => WindowLayout.MenuHeight(items, separators);

    // ---- The themes' colours, as phase 6's (Phase6Expectations.Common.cs) -----------------------

    /// <summary>The colours of one theme, from the token table, with phase 3's tokens (phase 3 design 6.4), phase 4's (phase 4 design 7.3), phase 5's (phase 5 design 13.3) and phase 6's (design 11.10, 11.11, 11.13).</summary>
    public sealed record Palette(
        Color Window,
        Color Panel,
        Color Chrome,
        Color Control,
        Color Hover,
        Color Border,
        Color TextPrimary,
        Color TextSecondary,
        Color TextDisabled,
        Color Accent,
        Color Success,
        Color Warning,
        Color Danger,
        Color Selection,
        Color RowHover,
        Color LabelText,
        Color Backdrop,
        Color BannerBackground,
        Color DragTarget,
        Color DiffAdded,
        Color DiffRemoved,
        Color ConflictOurs,
        Color ConflictTheirs,
        Color ConflictBase,
        Color ConflictMarker,
        Color ConflictUsed,
        Color SearchMatch,
        Color DangerText,
        string MenuShadow,
        IReadOnlyList<Color> Lanes)
    {
        /// <summary>The colour of lane colour number <paramref name="color"/>: palette entry <c>color % 8</c> (D48).</summary>
        public Color Lane(int color) => Lanes[color % Lanes.Count];
    }

    /// <summary>The dark theme's colours.</summary>
    public static Palette Dark { get; } = new(
        Window: Color.Parse("#14161B"),
        Panel: Color.Parse("#1A1D24"),
        Chrome: Color.Parse("#20242C"),
        Control: Color.Parse("#272C36"),
        Hover: Color.Parse("#2F3542"),
        Border: Color.Parse("#323845"),
        TextPrimary: Color.Parse("#E4E7EC"),
        TextSecondary: Color.Parse("#9BA3B0"),
        TextDisabled: Color.Parse("#5F6775"),
        Accent: Color.Parse("#7C8CFF"),
        Success: Color.Parse("#3FB97F"),
        Warning: Color.Parse("#E0A23B"),
        Danger: Color.Parse("#E5606B"),
        Selection: Color.Parse("#2A3150"),
        RowHover: Color.Parse("#1C1F27"),
        LabelText: Color.Parse("#10121A"),
        Backdrop: Color.Parse("#99000000"),
        BannerBackground: Color.Parse("#33291A"),
        DragTarget: Color.Parse("#7C8CFF"),
        DiffAdded: Color.Parse("#1A2E24"),
        DiffRemoved: Color.Parse("#331D23"),
        ConflictOurs: Color.Parse("#15323A"),
        ConflictTheirs: Color.Parse("#2C2140"),
        ConflictBase: Color.Parse("#24272F"),
        ConflictMarker: Color.Parse("#3A2E16"),
        ConflictUsed: Color.Parse("#1A2E24"),
        SearchMatch: Color.Parse("#F2C94C"),
        DangerText: Color.Parse("#FFFFFF"),
        MenuShadow: "0 6 16 0 #80000000",
        Lanes: [.. new[] { "#7C8CFF", "#3FB97F", "#E0A23B", "#E5606B", "#4FB3D9", "#B07CE8", "#D97EB6", "#8FB84A" }.Select(Color.Parse)]);

    /// <summary>The light theme's colours: warning, success and danger darker and lanes 1, 2, 4 and 7 darker in phase 6, so that every text reaches 4.5:1 (design 11.10, 11.13, D198).</summary>
    public static Palette Light { get; } = new(
        Window: Color.Parse("#FFFFFF"),
        Panel: Color.Parse("#F5F6F8"),
        Chrome: Color.Parse("#EBEDF1"),
        Control: Color.Parse("#FFFFFF"),
        Hover: Color.Parse("#DEE2E9"),
        Border: Color.Parse("#D2D6DE"),
        TextPrimary: Color.Parse("#1B1F27"),
        TextSecondary: Color.Parse("#5C6572"),
        TextDisabled: Color.Parse("#A1A8B3"),
        Accent: Color.Parse("#4353D8"),
        Success: Color.Parse("#187248"),
        Warning: Color.Parse("#895A17"),
        Danger: Color.Parse("#B7323D"),
        Selection: Color.Parse("#DDE1FA"),
        RowHover: Color.Parse("#F3F4F7"),
        LabelText: Color.Parse("#FFFFFF"),
        Backdrop: Color.Parse("#66000000"),
        BannerBackground: Color.Parse("#FFF4DE"),
        DragTarget: Color.Parse("#4353D8"),
        DiffAdded: Color.Parse("#E6F6EC"),
        DiffRemoved: Color.Parse("#FBE9EB"),
        ConflictOurs: Color.Parse("#DCF1F5"),
        ConflictTheirs: Color.Parse("#F0E6FA"),
        ConflictBase: Color.Parse("#EEF0F3"),
        ConflictMarker: Color.Parse("#FCEFD2"),
        ConflictUsed: Color.Parse("#E6F6EC"),
        SearchMatch: Color.Parse("#B98900"),
        DangerText: Color.Parse("#FFFFFF"),
        MenuShadow: "0 6 16 0 #33000000",
        Lanes: [.. new[] { "#4353D8", "#1C8655", "#A16A1B", "#C93A46", "#1D7EA6", "#8048C7", "#B54A8C", "#59811C" }.Select(Color.Parse)]);


    // ---- The scenario "hosting" (design 2.6) -------------------------------------------------------

    /// <summary>The working tree of octo/hello's clone, beside the fake's folder.</summary>
    public const string Hello = "hello";

    /// <summary>The working tree of octo/tool's clone, acme/tool its upstream.</summary>
    public const string Tool = "tool";

    public const string OctoLogin = "octo";

    public const string AcmeLogin = "acme";

    public const string AdaLogin = "ada";

    public const long OctoId = 1001;

    public const long AcmeId = 1002;

    public const long AdaId = 1003;

    public const string OctoName = Scenarios.OctoName;

    public const string AcmeName = "Acme";

    public const string AdaName = Scenarios.AdaName;

    public const string OctoEmail = Scenarios.OctoEmail;

    public const string AdaEmail = Scenarios.AdaEmail;

    /// <summary>octo's fine-grained token, the one every check signs in with (design 0.5).</summary>
    public const string OctoToken = "github_pat_FAKE-octo-1";

    /// <summary>octo's classic token, with <c>repo</c> and <c>workflow</c>.</summary>
    public const string OctoClassicToken = "ghp_FAKE-octo-2";

    public const string AdaToken = "github_pat_FAKE-ada-3";

    public const string OctoHello = "octo/hello";

    public const string AdaHello = "ada/hello";

    public const string AcmeTool = "acme/tool";

    public const string OctoTool = "octo/tool";

    public const string OctoNotes = "octo/notes";

    public const string OctoWebsite = "octo/website";

    /// <summary>The deleted fork of acme/tool whose pull request #8 is still open.</summary>
    public const string GoneTool = "gone/tool";

    /// <summary>The long branch whose label the graph must cut (part G).</summary>
    public const string LongBranch = Scenarios.LongBranch;

    /// <summary>octo's repositories as <c>user/repos?sort=updated</c> lists them: hello, tool, notes, website.</summary>
    public static IReadOnlyList<string> OctoRepositoriesByUpdate { get; } = [OctoHello, OctoTool, OctoNotes, OctoWebsite];

    /// <summary>octo/hello's open pull requests as GitHub lists them, number descending.</summary>
    public static IReadOnlyList<ScenarioPullRequest> HelloPullRequests { get; } =
    [
        new(5, "Fix a typo in the greeting", AdaLogin, "fix-typo", false, AdaHello),
        new(3, "Shorten long labels", OctoLogin, LongBranch, false, null),
        new(2, "Update the readme", OctoLogin, "docs/readme", true, null),
        new(1, "Add the login form", AdaLogin, "feature/login", false, null),
    ];

    /// <summary>octo/hello's closed pull request #4, "Try an idea", from <c>topic/no-pr</c>.</summary>
    public static ScenarioPullRequest ClosedHelloPullRequest { get; } = new(4, "Try an idea", OctoLogin, "topic/no-pr", false, null);

    /// <summary>acme/tool's open pull requests, number descending: #8's fork is deleted.</summary>
    public static IReadOnlyList<ScenarioPullRequest> ToolPullRequests { get; } =
    [
        new(8, "An old idea", AdaLogin, "idea", false, GoneTool),
        new(7, "Fix a typo in the readme", OctoLogin, "fix-typo", false, OctoTool),
    ];
}

/// <summary>A pull request of the scenario "hosting" (design 2.6).</summary>
/// <param name="Number">Its number.</param>
/// <param name="Title">Its title.</param>
/// <param name="Author">Its author's login.</param>
/// <param name="HeadRef">Its head branch.</param>
/// <param name="IsDraft">Whether it is a draft.</param>
/// <param name="HeadRepository">Its head repository's full name; null for the base repository itself.</param>
public sealed record ScenarioPullRequest(int Number, string Title, string Author, string HeadRef, bool IsDraft, string? HeadRepository);
