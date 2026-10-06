using System.Reflection;
using VisualCommit.Testing.Headless;

namespace VisualCommit.VisualTests;

/// <summary>
/// Where the scripted walk-throughs keep their screenshots:
/// <c>artifacts/visual/phase-N/scripted/</c> under the repo root. The folder is git-ignored.
/// </summary>
public static class VisualRun
{
    private static readonly string RepoRoot =
        typeof(VisualRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == "RepoRoot").Value!;

    /// <summary>The screenshot folder of a phase's scripted walk-through.</summary>
    public static string FolderFor(int phase) =>
        Path.Combine(RepoRoot, "artifacts", "visual", $"phase-{phase}", "scripted");

    /// <summary>Saves a screenshot of a phase's walk-through as <c>&lt;name&gt;.png</c> and returns its path.</summary>
    public static string Save(this Screenshot screenshot, int phase, string name) =>
        screenshot.Save(Path.Combine(FolderFor(phase), name + ".png"));
}
