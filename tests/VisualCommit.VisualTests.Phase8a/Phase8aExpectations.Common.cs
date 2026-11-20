using System.Text.RegularExpressions;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// What phase 8a's checks compare against that more than one part uses (docs/phase-8a-design.md
/// 0.3, 0.5, 1.15): the phase's name, the window's sizes, the picture names' pattern and the
/// version every headless start shows. Compiled by the real-window pass too, so it uses only the
/// BCL, Avalonia, xunit, Core, Git and Testing, and nothing of this project. The parts add
/// <c>Phase8aExpectations.&lt;part&gt;.cs</c>.
/// </summary>
public static partial class Phase8aExpectations
{
    /// <summary>The phase as the docs write it: folders <c>artifacts/visual/phase-8a/</c> (design 0.3).</summary>
    public const string Phase = "8a";

    /// <summary>The window's size unless a check names another (design 0.5).</summary>
    public static readonly (int Width, int Height) DefaultSize = (1100, 700);

    /// <summary>The size a check names when it needs room.</summary>
    public static readonly (int Width, int Height) FullSize = (1920, 1080);

    /// <summary>
    /// The version every headless start shows (<c>TestIdentity.Pinned</c>, design 1.6), so that no
    /// scripted picture changes from one commit to the next. The real window shows the built commit.
    /// </summary>
    public const string PinnedVersion = "0.1.0 (1a2b3c4)";

    /// <summary>
    /// A picture's name (design 0.3): the check (its part's letter, as the report numbers phase 8a's
    /// checks: a About and the notices, b the icon, f first run, g the guide's links, i one instance,
    /// k keyboard, l the contrast round, u screen readers; and its number), the step (up to three
    /// letters or digits), then words joined by hyphens: <c>f3a-chosen-gone</c>,
    /// <c>g2a-first-run-guide-link</c>. The letters C, O, Q, R and T are no part's, so that a name never reads as a
    /// requirement's number (D230).
    /// </summary>
    [GeneratedRegex(@"^[abfgiklu]\d{1,2}[a-z0-9]{0,3}-[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    public static partial Regex PictureName();
}
