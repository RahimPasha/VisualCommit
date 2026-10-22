// What is where in phase 3's expectations (docs/test-reports/phase-3.md):
// - Phase3Expectations.Common.cs (this file): what every part uses: the colours of both themes,
//   phase 3's new tokens among them; the layout numbers (WindowLayout: rows, columns, the menu
//   height formula, the banner's least height, the neutral point); backup ref names; which git
//   the report's quoted texts come from.
// - Phase3Expectations.A.cs to .D.cs: each part's own expected results (menus as the report writes
//   them, texts, graphs), each in its nested class PartA to PartD, so the four parts never edit
//   one file and their names never clash.
// These files are compiled by the real-window pass too (VisualCommit.RealWindowTests.csproj links
// Phase3Expectations*.cs), so that both harnesses use the same numbers and texts (design 7.4: the
// pass clicks rows at positions computed from the same constants). Use only the BCL, Avalonia,
// Xunit, VisualCommit.Core, VisualCommit.Git and VisualCommit.Testing here: no other file of
// VisualCommit.VisualTests.Phase3 or VisualCommit.VisualTests (not ShellExpectations or Palettes,
// which the pass does not compile).
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Media;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>
/// What phase 3's checks compare against, copied from docs/test-reports/phase-3.md and the token
/// table of docs/architecture.md (with design 6.4's new tokens), not read from the app, so that
/// the two are checked against each other. Each part adds its own in its nested class
/// (<c>PartA</c> to <c>PartD</c>, in Phase3Expectations.A.cs to .D.cs).
/// </summary>
public static partial class Phase3Expectations
{
    /// <summary>The phase whose report these are, and whose picture folder the checks fill.</summary>
    public const int Phase = 3;

    /// <summary>The window sizes the checks run at: 1100×700 unless a check says otherwise.</summary>
    public static readonly (int Width, int Height) DefaultSize = (1100, 700);

    /// <summary>The size of checks 8, 11, 11b and 12, whose graphs have 4 or 5 lanes.</summary>
    public static readonly (int Width, int Height) LargeSize = (1400, 900);

    /// <summary>The size of check 27 (the light theme).</summary>
    public static readonly (int Width, int Height) FullSize = (1920, 1080);

    /// <summary>The port of the visual checks' credential test server (design 7.3); a check whose server fell back to another asserts that port instead.</summary>
    public const int ServerPort = AuthRemoteServer.VisualChecksPort;

    /// <summary>The colours of one theme, from the token table, with phase 3's tokens (design 6.4).</summary>
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
        MenuShadow: "0 6 16 0 #80000000",
        Lanes: [.. new[] { "#7C8CFF", "#3FB97F", "#E0A23B", "#E5606B", "#4FB3D9", "#B07CE8", "#D97EB6", "#8FB84A" }.Select(Color.Parse)]);

    /// <summary>The light theme's colours.</summary>
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
        MenuShadow: "0 6 16 0 #33000000",
        Lanes: [.. new[] { "#4353D8", "#1C8655", "#A16A1B", "#C93A46", "#1D7EA6", "#8048C7", "#B54A8C", "#59811C" }.Select(Color.Parse)]);

    /// <summary>
    /// The height of a menu that fits the window (4a): 8 + 24 + 26 × items + 9 × separators
    /// (<see cref="WindowLayout.MenuHeight"/>), for example 232 for a commit's menu.
    /// </summary>
    public static double MenuHeight(int items, int separators) => WindowLayout.MenuHeight(items, separators);

    /// <summary>
    /// <paramref name="text"/> with the server's port in place of 47391, for a run whose server fell
    /// back to another port (design 7.3: "the pictures differ in the port only, and the report
    /// says so").
    /// </summary>
    public static string WithPort(string text, int port)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace(
            AuthRemoteServer.VisualChecksPort.ToString(CultureInfo.InvariantCulture),
            port.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether the git on this machine is 2.36, the one the report's quoted git texts come from
    /// (the conventions' "Git's own texts"): only then do the checks assert them word for word.
    /// On any other git (CI runs 2.55) they assert the classifying "key" line and that the
    /// dialog shows <c>GitOutputCleaner.Clean</c> of the failed call's own output.
    /// </summary>
    public static Task<bool> IsReportGitAsync() => ReportGit.Value;

    private static readonly Lazy<Task<bool>> ReportGit = new(async () =>
    {
        var path = GitLocator.FindExecutable(GitSearchContext.FromSystem())
            ?? throw new InvalidOperationException("Git was not found on this machine.");
        var result = await new GitRunner(path).RunAsync(new GitCommand("--version"), CancellationToken.None).ConfigureAwait(false);
        return GitVersion.TryParse(result.StandardOutput, out var version) && version.Major == 2 && version.Minor == 36;
    });

    /// <summary>
    /// A backup ref's name taken apart (design 2.0, D67):
    /// <c>refs/visualcommit/backup/&lt;kind&gt;-&lt;UTC time&gt;[-&lt;n&gt;][-changes | /&lt;name&gt;]</c>,
    /// for example <c>remote-tag-20260102T090000000Z/origin/v1.2</c>. A check asserts the kind, that
    /// the time parses and what the ref points at, never the time itself.
    /// </summary>
    /// <param name="Kind">"reset", "abort", "branch", "remote-tag", "force-push", …</param>
    /// <param name="Time">The operation's UTC time.</param>
    /// <param name="IsChanges">Whether it is the snapshot of changed files that goes with the backup of the same stem.</param>
    /// <param name="Name">What comes after the stem's "/", or null.</param>
    public sealed record BackupRef(string Kind, DateTimeOffset Time, bool IsChanges, string? Name)
    {
        /// <summary>Takes a full backup ref name apart; throws when it is not one.</summary>
        public static BackupRef Parse(string fullName)
        {
            ArgumentNullException.ThrowIfNull(fullName);
            var match = BackupRefPattern().Match(fullName);
            if (!match.Success)
            {
                throw new FormatException($"\"{fullName}\" is not a backup ref name (refs/visualcommit/backup/<kind>-<UTC time>...).");
            }

            var time = DateTimeOffset.ParseExact(match.Groups["time"].Value, "yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            return new BackupRef(
                match.Groups["kind"].Value,
                time,
                match.Groups["changes"].Success,
                match.Groups["name"].Success ? match.Groups["name"].Value : null);
        }
    }

    [GeneratedRegex(@"^refs/visualcommit/backup/(?<kind>[a-z]+(?:-[a-z]+)*)-(?<time>\d{8}T\d{9}Z)(?:-\d+)?(?:(?<changes>-changes)|/(?<name>.+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex BackupRefPattern();
}
