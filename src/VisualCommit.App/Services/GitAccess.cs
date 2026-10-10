using VisualCommit.Core.Git;

namespace VisualCommit.App.Services;

/// <summary>
/// The app's way to the git runner (D45). Git is looked for once, after the window exists, and
/// may not be found; services hold this instead of a runner and ask for the runner when they
/// need it.
/// </summary>
public sealed class GitAccess
{
    private readonly Task<GitDetection> _detection;

    public GitAccess(Task<GitDetection> detection)
    {
        ArgumentNullException.ThrowIfNull(detection);
        _detection = detection;
    }

    /// <summary>What was found when the app looked for git.</summary>
    public Task<GitDetection> Detection => _detection;

    /// <summary>Waits for the search for git to finish and returns the runner.</summary>
    /// <exception cref="GitUnavailableException">No usable git was found.</exception>
    public async Task<IGitRunner> GetRunnerAsync(CancellationToken cancellationToken = default)
    {
        var detection = await _detection.WaitAsync(cancellationToken).ConfigureAwait(false);
        return detection.Runner ?? throw new GitUnavailableException(detection.Availability switch
        {
            GitAvailability.NotFound => "Git was not found on this computer.",
            GitAvailability.TooOld => $"Git {detection.Version} is too old; VisualCommit needs {GitVersion.Minimum} or newer.",
            _ => "Git is not working on this computer.",
        });
    }
}
