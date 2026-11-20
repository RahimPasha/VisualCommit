using VisualCommit.Core.Logging;

namespace VisualCommit.Core.Git;

/// <summary>What a search for git is told besides the machine itself.</summary>
/// <param name="ChosenPath">The git chosen in Settings (<c>AppSettings.GitExecutable</c>); null for none.</param>
/// <param name="StandIn">The test switch <c>VISUALCOMMIT_GIT_STAND_IN</c> as read when the app started; null when unset.</param>
public sealed record GitLookup(string? ChosenPath, string? StandIn);

/// <summary>The platform whose guidance the first-run screen gives.</summary>
public enum GuidancePlatform
{
    Windows,
    MacOS,
    Linux,
}

/// <summary>A Linux distribution as its <c>os-release</c> file names it.</summary>
/// <param name="Id">Its <c>ID</c>.</param>
/// <param name="IdLike">Its <c>ID_LIKE</c>, split on spaces; empty when the file has none.</param>
/// <param name="PrettyName">Its <c>PRETTY_NAME</c>; null when the file has none.</param>
public sealed record LinuxDistribution(string Id, IReadOnlyList<string> IdLike, string? PrettyName);

/// <summary>What the first-run screen's guidance is built from.</summary>
/// <param name="Platform">The platform the guidance is for.</param>
/// <param name="WingetPath">On Windows, the full path of <c>winget.exe</c> on the PATH; null when there is none.</param>
/// <param name="Distribution">On Linux, the distribution; null when it is unknown.</param>
public sealed record GitGuidance(GuidancePlatform Platform, string? WingetPath, LinuxDistribution? Distribution);

/// <summary>The search for git and the facts its guidance needs; the app's and the harnesses' seam.</summary>
public interface IGitSetupProbe
{
    /// <summary>
    /// Looks for git as <paramref name="lookup"/> says, asks it for its version and returns a runner
    /// for a usable one, recording its calls in <paramref name="callLog"/> with the times of
    /// <paramref name="recordClock"/> and running them with the session's git environment
    /// <paramref name="environment"/>. The order: the git chosen in Settings, used whatever it
    /// answers and with no fallback (<see cref="GitDetection.ChosenMissing"/> when no file is there
    /// or it is not a full path, on Windows not an <c>.exe</c>); else the stand-in, when
    /// <see cref="GitLookup.StandIn"/> is set; else the PATH and then the usual install folders.
    /// Never throws for a missing or broken git; the result says what was found.
    /// </summary>
    Task<GitDetection> DetectAsync(
        GitLookup lookup,
        IGitCallLog? callLog,
        IAppLog log,
        TimeProvider? recordClock,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken);

    /// <summary>The facts of the first-run screen's guidance, read only while that screen shows.</summary>
    GitGuidance ReadGuidance();
}

/// <summary>What the first-run screen shows for a search for git.</summary>
public enum GitSetupState
{
    /// <summary>No search has ended yet.</summary>
    Searching,

    /// <summary>A usable git was found, and the helper for git's questions is there.</summary>
    Found,

    /// <summary>No git was found.</summary>
    Missing,

    /// <summary>The git chosen in Settings is not there, or is not a path the search runs.</summary>
    ChosenMissing,

    /// <summary>Git was found but is older than <see cref="GitVersion.Minimum"/>.</summary>
    TooOld,

    /// <summary>Git was found but did not report its version.</summary>
    NotWorking,

    /// <summary>Git works, but the app's helper for git's questions is missing.</summary>
    HelperMissing,
}

/// <summary>The state of the first-run screen from a search's result.</summary>
public static class GitSetupStates
{
    /// <summary>
    /// The state for <paramref name="git"/> (null while no search has ended: <see cref="GitSetupState.Searching"/>):
    /// git's own states first, and <see cref="GitSetupState.HelperMissing"/> only for a usable git
    /// while <paramref name="missingHelperPath"/>, where the helper was looked for, is set.
    /// </summary>
    public static GitSetupState Of(GitDetection? git, string? missingHelperPath) => git?.Availability switch
    {
        null => GitSetupState.Searching,
        GitAvailability.Available => missingHelperPath is null ? GitSetupState.Found : GitSetupState.HelperMissing,
        GitAvailability.NotFound => git.ChosenMissing is null ? GitSetupState.Missing : GitSetupState.ChosenMissing,
        GitAvailability.TooOld => GitSetupState.TooOld,
        _ => GitSetupState.NotWorking,
    };
}
