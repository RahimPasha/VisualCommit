using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Media;
using VisualCommit.Core.Hosting;
using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// What phase 7b's checks compare against that more than one part uses (docs/phase-7b-design.md
/// 0.3, 2.6, 2.7): the phase's name, the window's sizes, the fakes' ports, the names and tokens of
/// the scenario "hosting7b", the themes' colours, and the one prepared state that part P's real-window
/// methods, part N's checks and WP-R's "Requests and create" launches share
/// (<see cref="PrepareRequestsAsync"/>). Compiled by the real-window pass too, so it uses only the
/// BCL, Avalonia, xunit, Core, Git and Testing. The parts add <c>Phase7bExpectations.&lt;part&gt;.cs</c>.
/// </summary>
public static partial class Phase7bExpectations
{
    /// <summary>The phase as the docs write it: folders <c>artifacts/visual/phase-7b/</c> (design 0.3).</summary>
    public const string Phase = "7b";

    /// <summary>The window's size unless a check names another (design 0.5).</summary>
    public static readonly (int Width, int Height) DefaultSize = (1100, 700);

    /// <summary>The size a check names when it needs room.</summary>
    public static readonly (int Width, int Height) FullSize = (1920, 1080);

    /// <summary>The fake of Azure DevOps' port (D363), whose URLs the pictures show.</summary>
    public const int AzureDevOpsPort = AuthRemoteServer.Phase7bAzureDevOpsPort;

    /// <summary>The fake of GitLab's port (D363).</summary>
    public const int GitLabPort = AuthRemoteServer.Phase7bGitLabPort;

    /// <summary>7b's own fake of GitHub's port (D363), so that no 7b picture depends on 7a's 47395.</summary>
    public const int GitHubPort = AuthRemoteServer.Phase7bGitHubPort;

    /// <summary>
    /// <paramref name="text"/> with each fake's port in place of its fixed one (47397 to 47399), for a
    /// text that shows a fake's URL in a run whose fake fell back to a free port
    /// (<see cref="Testing.Hosting.FakeHostingService.PortWasTaken"/>), which the report's notes then
    /// name (D265), as 7a's <c>WithPort</c> does for 47395.
    /// </summary>
    public static string WithPorts(string text, ServedHosting7b served)
    {
        ArgumentNullException.ThrowIfNull(served);
        return WithPorts(text, served.AzureDevOps.Port, served.GitLab.Port, served.GitHub.Port);
    }

    /// <summary><see cref="WithPorts(string, ServedHosting7b)"/> with the ports given, all three in one pass.</summary>
    public static string WithPorts(string text, int azureDevOpsPort, int gitLabPort, int gitHubPort)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Regex.Replace(
            text,
            "4739[789]",
            match => (match.Value switch
            {
                "47397" => azureDevOpsPort,
                "47398" => gitLabPort,
                _ => gitHubPort,
            }).ToString(CultureInfo.InvariantCulture),
            RegexOptions.CultureInvariant);
    }

    /// <summary>A fake's web address on <paramref name="port"/>: <c>http://127.0.0.1:47397/</c>, Azure DevOps', unless given.</summary>
    public static string WebBase(int port = AzureDevOpsPort) => string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/");

    /// <summary>
    /// The height of a menu that fits the window (phase 3's 4a): 8 + 24 + 26 × items + 9 ×
    /// separators (<see cref="WindowLayout.MenuHeight"/>).
    /// </summary>
    public static double MenuHeight(int items, int separators) => WindowLayout.MenuHeight(items, separators);

    /// <summary>
    /// The one prepared state of part P's real-window methods, part N's checks and WP-R's "Requests
    /// and create" launches (design 2.7, 12.8, 13.8, 14.2): <c>git branch --track topic/no-request
    /// origin/topic/no-request</c> in the provider's clone of the copy, <c>web-app/</c> for GitLab and
    /// <paramref name="copy"/>'s <c>mobile-app/</c> for Azure DevOps.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="provider"/> is GitHub, whose state is 7a's.</exception>
    public static async Task PrepareRequestsAsync(TempRepo copy, HostingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(copy);
        var clone = provider switch
        {
            HostingProvider.GitLab => copy.OpenSibling(WebApp),
            HostingProvider.AzureDevOps => copy,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "The prepared state is Azure DevOps' or GitLab's."),
        };
        await clone.GitAsync("branch", "--track", "topic/no-request", "origin/topic/no-request");
    }

    // ---- The themes' colours, as phase 7a's (Phase7aExpectations.Common.cs) ------------------------

    /// <summary>The colours of one theme, from the token table, as phase 7a's.</summary>
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

    /// <summary>The light theme's colours, as phase 6 darkened them (design 11.10, 11.13, D198).</summary>
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

    // ---- The scenario "hosting7b" (design 2.6) -----------------------------------------------------

    /// <summary>The working tree of Fernhill-Labs' mobile-app on Azure DevOps, the scenario's copy itself.</summary>
    public const string MobileApp = "mobile-app";

    /// <summary>The working tree of harbor-co's manifest (D356).</summary>
    public const string Manifest = "manifest";

    /// <summary>The working tree of web-app on GitLab, octo/web-app on GitHub its upstream (ruling 5).</summary>
    public const string WebApp = "web-app";

    public const string Organization = Scenarios.Hosting7bOrganization;

    public const string OtherOrganization = Scenarios.Hosting7bOtherOrganization;

    public const string Project = Scenarios.Hosting7bProject;

    public const string OtherProject = Scenarios.Hosting7bOtherProject;

    public const string LongRepository = Scenarios.Hosting7bLongRepository;

    public const string WebAppPath = Scenarios.Hosting7bWebApp;

    public const string WebAppFork = Scenarios.Hosting7bWebAppFork;

    public const string LongProject = Scenarios.Hosting7bLongProject;

    public const string GitHubWebApp = Scenarios.Hosting7bGitHubWebApp;

    public const string AdaLogin = "ada";

    public const string OctoLogin = "octo";

    public const string AdaName = Scenarios.AdaName;

    public const string OctoName = Scenarios.OctoName;

    /// <summary>Ada's token of Fernhill-Labs, the one every Azure DevOps check signs in with (3.5).</summary>
    public const string AzureDevOpsToken = "azdo_FAKE-Fernhill-Labs-1";

    /// <summary>ada's fine-grained token on GitLab, the one every GitLab check signs in with (4.5).</summary>
    public const string GitLabToken = "glpat-FAKE-ada-1";

    /// <summary>ada's legacy token on GitLab (4.5).</summary>
    public const string GitLabLegacyToken = "glpat-FAKE-ada-2";

    /// <summary>octo's fine-grained token on 7b's fake of GitHub.</summary>
    public const string GitHubToken = "github_pat_FAKE-octo-1";

    /// <summary>mobile-app's origin on the fake, in the service's form (2.6).</summary>
    public const string MobileAppUrl = "http://Fernhill-Labs@127.0.0.1:47397/Fernhill-Labs/Mobile%20App/_git/mobile-app";

    /// <summary>manifest's origin, in harbor-co (D356).</summary>
    public const string ManifestUrl = "http://harbor-co@127.0.0.1:47397/harbor-co/Docks/_git/manifest";

    /// <summary>web-app's origin on GitLab.</summary>
    public const string WebAppUrl = "http://127.0.0.1:47398/northfield/platform/web-app.git";

    /// <summary>web-app's upstream on GitHub (ruling 5).</summary>
    public const string WebAppUpstreamUrl = "http://127.0.0.1:47399/octo/web-app.git";
}
