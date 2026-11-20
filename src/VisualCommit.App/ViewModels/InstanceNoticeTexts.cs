using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels;

/// <summary>
/// Every text of the notice that shows when another VisualCommit holds the same data folder and
/// did not answer (D421, D422). It says "did not answer", never that the other hangs: an elevated
/// first instance on Windows refuses a normal one's connection while it works.
/// </summary>
public static class InstanceNoticeTexts
{
    /// <summary>The window's title.</summary>
    public const string Title = "VisualCommit";

    public const string Heading = "VisualCommit is already running";

    public const string Body = "Another VisualCommit uses the same data folder but did not answer. Switch to its window, or close it and start VisualCommit again.";

    public const string IfNoWindowOnWindows = "If no VisualCommit window is open, end VisualCommit in Task Manager, then choose Try again.";

    public const string IfNoWindowOnMacOS = "If no VisualCommit window is open, quit VisualCommit in Activity Monitor, then choose Try again.";

    public const string IfNoWindowOnLinux = "If no VisualCommit window is open, end VisualCommit in your system monitor, then choose Try again.";

    public const string TryAgain = "Try again";

    /// <summary>"Try again" while its try runs.</summary>
    public const string Trying = "Trying…";

    public const string Close = "Close";

    /// <summary>Under the sentence after a try that the other instance did not answer either.</summary>
    public const string StillNoAnswer = "It still did not answer.";

    /// <summary>The platform's sentence on what to do when no window of the other is to be seen.</summary>
    public static string IfNoWindow(GuidancePlatform platform) => platform switch
    {
        GuidancePlatform.Windows => IfNoWindowOnWindows,
        GuidancePlatform.MacOS => IfNoWindowOnMacOS,
        _ => IfNoWindowOnLinux,
    };
}
