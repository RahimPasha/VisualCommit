using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// The machine's search for git and its guidance facts: the app session's
/// <see cref="IGitSetupProbe"/> unless a harness gives another. The search is
/// <see cref="GitLocator.DetectAsync(GitLookup, IGitCallLog?, IAppLog?, TimeProvider?, IReadOnlyDictionary{string, string?}?, CancellationToken)"/>'s;
/// the guidance names the running platform, with winget's path on Windows and the distribution on Linux.
/// </summary>
public sealed class MachineGitSetupProbe : IGitSetupProbe
{
    private MachineGitSetupProbe()
    {
    }

    /// <summary>The one instance.</summary>
    public static MachineGitSetupProbe Instance { get; } = new();

    /// <inheritdoc/>
    public Task<GitDetection> DetectAsync(
        GitLookup lookup,
        IGitCallLog? callLog,
        IAppLog log,
        TimeProvider? recordClock,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken) =>
        GitLocator.DetectAsync(lookup, callLog, log, recordClock, environment, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Reads the disk, so it is called off the UI thread. On Windows <c>winget.exe</c> is looked for
    /// in the PATH's fully qualified folders (<see cref="ProgramSearch.FindOnPath"/>): the file the
    /// screen's install starts by its full path, never <c>cmd.exe</c>'s own search of a bare name,
    /// which starts in its current folder. On Linux the distribution comes from <c>os-release</c>
    /// (<see cref="OsRelease.Read()"/>).
    /// </remarks>
    public GitGuidance ReadGuidance()
    {
        if (OperatingSystem.IsWindows())
        {
            return new GitGuidance(GuidancePlatform.Windows, ProgramSearch.FindOnPath("winget.exe", Environment.GetEnvironmentVariable("PATH"), ';', File.Exists), null);
        }

        return OperatingSystem.IsMacOS()
            ? new GitGuidance(GuidancePlatform.MacOS, null, null)
            : new GitGuidance(GuidancePlatform.Linux, null, OsRelease.Read());
    }
}
