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

/// <summary>Where the git executable of a <see cref="GitDetection"/> came from.</summary>
public enum GitSource
{
    /// <summary>A folder of the PATH.</summary>
    Path,

    /// <summary>One of git's usual install folders (Git for Windows', and macOS's and Linux's usual ones).</summary>
    InstallFolder,

    /// <summary>The git chosen in Settings.</summary>
    Setting,

    /// <summary>The test switch <c>VISUALCOMMIT_GIT_STAND_IN</c>, which names the machine's only git.</summary>
    StandIn,
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
    IGitRunner? Runner)
{
    /// <summary>Where <see cref="ExecutablePath"/> came from; <see cref="GitSource.Path"/> unless the search says otherwise.</summary>
    public GitSource Source { get; init; }

    /// <summary>
    /// For <see cref="GitAvailability.Broken"/>: git's standard error, else its standard output, at
    /// most 4,000 characters, or why it could not be started; null otherwise.
    /// </summary>
    public string? ErrorText { get; init; }

    /// <summary>For <see cref="GitAvailability.Broken"/>: git's exit code; null when it did not start, and otherwise.</summary>
    public int? ExitCode { get; init; }

    /// <summary>For <see cref="GitAvailability.Broken"/> when git said <c>fatal: bad config line &lt;n&gt; in file &lt;f&gt;</c>: that line and file; null otherwise.</summary>
    public GitConfigFault? ConfigFault { get; init; }

    /// <summary>
    /// For <see cref="GitAvailability.NotFound"/>: the git chosen in Settings that is not used, because
    /// no file is there or the setting is not a full path to the program (on Windows, not an
    /// <c>.exe</c>); null when Settings names none.
    /// </summary>
    public string? ChosenMissing { get; init; }

    /// <summary>
    /// With <see cref="ChosenMissing"/>: the git the machine's own search finds (the PATH, then the
    /// usual install folders), which was not run; null for none.
    /// </summary>
    public string? PathCandidate { get; init; }

    /// <summary>On macOS: <c>/usr/bin/git</c> was left out because <c>xcode-select -p</c> failed (Apple's Command Line Tools are not installed).</summary>
    public bool CommandLineToolsMissing { get; init; }
}

/// <summary>The line of git's configuration that git could not read: <c>fatal: bad config line &lt;n&gt; in file &lt;f&gt;</c>.</summary>
/// <param name="File">The file, as git names it.</param>
/// <param name="Line">The line's number, from 1.</param>
public sealed record GitConfigFault(string File, int Line);
