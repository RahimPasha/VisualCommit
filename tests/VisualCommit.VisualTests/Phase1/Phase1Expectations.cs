using Avalonia.Media;
using VisualCommit.App.ViewModels.Graph;

namespace VisualCommit.VisualTests.Phase1;

/// <summary>
/// The colours phase 1's checks compare against, copied from docs/test-reports/phase-1.md and
/// the token table of docs/architecture.md, not from the app's theme files, so the two are
/// checked against each other.
/// </summary>
public static class Palettes
{
    public sealed record Palette(
        Color[] Lanes,
        Color Window,
        Color Panel,
        Color Chrome,
        Color Control,
        Color Border,
        Color Primary,
        Color Secondary,
        Color Accent,
        Color LabelText,
        Color Selection,
        Color RowHover,
        Color Success,
        Color Warning,
        Color Danger)
    {
        /// <summary>The colour of lane colour number <paramref name="color"/>: palette entry <c>color % 8</c> (D48).</summary>
        public Color Lane(int color) => Lanes[color % Lanes.Length];
    }

    public static Palette Dark { get; } = new(
        [.. new[] { "#7C8CFF", "#3FB97F", "#E0A23B", "#E5606B", "#4FB3D9", "#B07CE8", "#D97EB6", "#8FB84A" }.Select(Color.Parse)],
        Window: Color.Parse("#14161B"),
        Panel: Color.Parse("#1A1D24"),
        Chrome: Color.Parse("#20242C"),
        Control: Color.Parse("#272C36"),
        Border: Color.Parse("#323845"),
        Primary: Color.Parse("#E4E7EC"),
        Secondary: Color.Parse("#9BA3B0"),
        Accent: Color.Parse("#7C8CFF"),
        LabelText: Color.Parse("#10121A"),
        Selection: Color.Parse("#2A3150"),
        RowHover: Color.Parse("#1C1F27"),
        Success: Color.Parse("#3FB97F"),
        Warning: Color.Parse("#E0A23B"),
        Danger: Color.Parse("#E5606B"));

    public static Palette Light { get; } = new(
        [.. new[] { "#4353D8", "#1E8E5A", "#B7791F", "#C93A46", "#1F86B0", "#8048C7", "#B54A8C", "#5F8A1E" }.Select(Color.Parse)],
        Window: Color.Parse("#FFFFFF"),
        Panel: Color.Parse("#F5F6F8"),
        Chrome: Color.Parse("#EBEDF1"),
        Control: Color.Parse("#FFFFFF"),
        Border: Color.Parse("#D2D6DE"),
        Primary: Color.Parse("#1B1F27"),
        Secondary: Color.Parse("#5C6572"),
        Accent: Color.Parse("#4353D8"),
        LabelText: Color.Parse("#FFFFFF"),
        Selection: Color.Parse("#DDE1FA"),
        RowHover: Color.Parse("#F3F4F7"),
        Success: Color.Parse("#1E8E5A"),
        Warning: Color.Parse("#B7791F"),
        Danger: Color.Parse("#C93A46"));
}

/// <summary>What the graph shows for the graph scenario: the table in docs/test-reports/phase-1.md.</summary>
public static class GraphScenarioExpectations
{
    public enum Node
    {
        Commit,
        Merge,
        Stash,
    }

    /// <summary>One row of the table.</summary>
    public sealed record Row(string? Label, RefLabelKind? LabelKind, int Lane, int Color, Node Node, string Message, string Date, string Sha);

    public static IReadOnlyList<Row> Rows { get; } =
    [
        new("stash@{0}", RefLabelKind.Stash, 0, 0, Node.Stash, "On main: Work in progress on README", "2026-01-01 12:12", "95daa96"),
        new("feature/search", RefLabelKind.LocalBranch, 1, 1, Node.Commit, "Highlight matches", "2026-01-01 12:11", "f463b85"),
        new("main", RefLabelKind.CurrentBranch, 0, 0, Node.Commit, "Add settings page", "2026-01-01 12:10", "47e6ec7"),
        new("origin/main", RefLabelKind.RemoteBranch, 2, 2, Node.Commit, "Fix typo in docs", "2026-01-01 12:09", "974bd83"),
        new("v0.2", RefLabelKind.Tag, 0, 0, Node.Commit, "Bump version", "2026-01-01 12:08", "2775228"),
        new("bugfix/crash-on-start", RefLabelKind.LocalBranch, 2, 3, Node.Commit, "Fix crash on start", "2026-01-01 12:07", "72267a3"),
        new("origin/feature/search", RefLabelKind.RemoteBranch, 1, 1, Node.Commit, "Add search box", "2026-01-01 12:06", "6aab2e4"),
        new(null, null, 0, 0, Node.Merge, "Merge branch 'feature/login'", "2026-01-01 12:05", "f7919b8"),
        new(null, null, 0, 0, Node.Commit, "Update README", "2026-01-01 12:04", "10bcd42"),
        new("feature/login", RefLabelKind.LocalBranch, 1, 4, Node.Commit, "Validate passwords", "2026-01-01 12:03", "4a62ef9"),
        new(null, null, 1, 4, Node.Commit, "Add login form", "2026-01-01 12:02", "7b230d3"),
        new("v0.1", RefLabelKind.Tag, 0, 0, Node.Commit, "Add app skeleton", "2026-01-01 12:01", "933250d"),
        new(null, null, 0, 0, Node.Commit, "Initial commit", "2026-01-01 12:00", "53347b3"),
    ];
}
