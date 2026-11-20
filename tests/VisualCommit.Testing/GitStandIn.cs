namespace VisualCommit.Testing;

/// <summary>
/// The names of the stand-in for git, a small program of the tests that answers <c>--version</c>
/// as an old or a broken git would: its file name and the two variables it reads. The app is
/// pointed at it by <c>VISUALCOMMIT_GIT_STAND_IN</c> (<c>GitLocator.StandInVariable</c>). Every test
/// process removes all three variables (<see cref="GitIsolation.MachineIndependence"/>).
/// </summary>
public static class GitStandIn
{
    /// <summary>The stand-in's file name: <c>visualcommit-git-stand-in</c>, with <c>.exe</c> on Windows. Never <c>git</c>.</summary>
    public static string FileName { get; } = OperatingSystem.IsWindows() ? "visualcommit-git-stand-in.exe" : "visualcommit-git-stand-in";

    /// <summary>The line the stand-in prints for <c>--version</c> (unset: <c>git version 2.29.0</c>).</summary>
    public const string VersionVariable = "VISUALCOMMIT_GIT_STAND_IN_VERSION";

    /// <summary>A text the stand-in writes to standard error, exiting with 128, whatever its arguments.</summary>
    public const string FailureVariable = "VISUALCOMMIT_GIT_STAND_IN_FAILURE";
}
