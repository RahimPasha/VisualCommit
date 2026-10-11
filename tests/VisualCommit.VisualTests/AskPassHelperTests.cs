using VisualCommit.Git.Prompts;
using Xunit;

namespace VisualCommit.VisualTests;

/// <summary>
/// The askpass helper reaches this project's output through the reference of
/// <c>VisualCommit.Git</c> (docs/phase-3-design.md 3.1, 7.7 risk 8): the app the walk-through
/// starts finds it next to itself, so git's prompts can reach the prompt dialogs (check 21b).
/// </summary>
public class AskPassHelperTests
{
    [Fact]
    public void The_helper_is_in_the_output_folder_with_what_it_needs_to_start()
    {
        Assert.NotNull(AskPassLocator.Find());
        Assert.True(File.Exists(Path.Combine(AskPassLocator.Folder, "visualcommit-askpass.dll")));
        Assert.True(File.Exists(Path.Combine(AskPassLocator.Folder, "visualcommit-askpass.runtimeconfig.json")));
    }
}
