namespace VisualCommit.App.Services;

/// <summary>
/// Where the app's own icon lies among its resources (D407). The icon's builder renders these files
/// from the SVG masters in <c>assets/brand/</c>, and the build embeds them, so the app ships no icon
/// file beside its executable. Named so as not to meet the glyph control <c>Controls.Icon</c>.
/// </summary>
public static class AppIcon
{
    /// <summary>The window icon of the main window and of the single-instance notice: every size Windows asks for, in one <c>.ico</c>.</summary>
    public const string WindowIconUri = "avares://VisualCommit/Assets/Icon/VisualCommit.ico";

    /// <summary>The mark at 48 pixels.</summary>
    public const string Image48Uri = "avares://VisualCommit/Assets/Icon/VisualCommit-48.png";

    /// <summary>The mark at 96 pixels, which About draws at 48×48 so that it stays sharp at 200 % scaling.</summary>
    public const string Image96Uri = "avares://VisualCommit/Assets/Icon/VisualCommit-96.png";
}
