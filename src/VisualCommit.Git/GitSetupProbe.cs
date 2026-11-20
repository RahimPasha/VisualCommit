using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// The machine's search for git and its guidance facts: the app session's
/// <see cref="IGitSetupProbe"/> unless a harness gives another. The search is
/// <see cref="GitLocator.DetectAsync(GitLookup, IGitCallLog?, IAppLog?, TimeProvider?, IReadOnlyDictionary{string, string?}?, CancellationToken)"/>'s;
/// the guidance names the running platform, with no winget and no Linux distribution.
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
    /// <remarks>Reads no file and no variable: the running platform, with no winget and no distribution.</remarks>
    public GitGuidance ReadGuidance() => new(RunningPlatform, null, null);

    /// <summary>The platform this process runs on, as the guidance names it.</summary>
    private static GuidancePlatform RunningPlatform =>
        OperatingSystem.IsWindows() ? GuidancePlatform.Windows
        : OperatingSystem.IsMacOS() ? GuidancePlatform.MacOS
        : GuidancePlatform.Linux;
}
