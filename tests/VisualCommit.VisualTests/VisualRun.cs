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

    private static readonly Lock Gate = new();
    private static readonly HashSet<int> PhasesStarted = [];

    /// <summary>The screenshot folder of a phase's scripted walk-through.</summary>
    public static string FolderFor(int phase) =>
        Path.Combine(RepoRoot, "artifacts", "visual", $"phase-{phase}", "scripted");

    /// <summary>
    /// Saves a screenshot of a phase's walk-through as <c>&lt;name&gt;.png</c> and returns its path.
    /// The first save of a phase in a test run empties the phase's folder, so every picture in it
    /// comes from that run and none is left over from a check that was renamed or removed. After
    /// a run of only some checks the folder therefore holds only their pictures.
    /// </summary>
    public static string Save(this Screenshot screenshot, int phase, string name)
    {
        lock (Gate)
        {
            if (PhasesStarted.Add(phase) && Directory.Exists(FolderFor(phase)))
            {
                foreach (var old in Directory.EnumerateFiles(FolderFor(phase), "*.png"))
                {
                    File.Delete(old);
                }
            }
        }

        return screenshot.Save(Path.Combine(FolderFor(phase), name + ".png"));
    }
}
