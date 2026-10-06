namespace VisualCommit.Core.Git;

/// <summary>Runs the git executable. Every part of the app that needs git goes through this.</summary>
public interface IGitRunner
{
    /// <summary>The full path of the git executable this runner starts.</summary>
    string ExecutablePath { get; }

    /// <summary>
    /// Runs git without blocking the caller and returns when git has exited. A non-zero exit code
    /// is returned, not thrown; use <see cref="GitResult.EnsureSuccess"/> to turn it into an exception.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled; git and its child processes were stopped.</exception>
    /// <exception cref="GitException">Git could not be started.</exception>
    Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default);
}

public enum GitAvailability
{
    /// <summary>Git was found and is new enough.</summary>
    Available,

    /// <summary>No git executable was found.</summary>
    NotFound,

    /// <summary>Git was found but is older than <see cref="GitVersion.Minimum"/>.</summary>
    TooOld,

    /// <summary>A git executable was found but did not report a version.</summary>
    Broken,
}

/// <summary>The result of looking for git on this machine.</summary>
/// <param name="Availability">Whether a usable git was found.</param>
/// <param name="ExecutablePath">The git executable, or null when none was found.</param>
/// <param name="Version">Its version, or null when it is unknown.</param>
/// <param name="Runner">A runner for it. Set only when <paramref name="Availability"/> is <see cref="GitAvailability.Available"/>.</param>
public sealed record GitDetection(
    GitAvailability Availability,
    string? ExecutablePath,
    GitVersion? Version,
    IGitRunner? Runner);
