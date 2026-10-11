using VisualCommit.Core.Git;

namespace VisualCommit.Git.Tests;

/// <summary>
/// What <see cref="ScenarioTests"/> and <see cref="Phase3ScenarioTests"/> share. They were one
/// partial class, whose tests xunit ran one after another; as two classes they run side by side.
/// </summary>
internal static class ScenarioTestSupport
{
    public static string[] Lines(GitResult result) => Lines(result.StandardOutput);

    public static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    public static string Subject(GitResult result) => result.StandardOutput.Trim();
}
