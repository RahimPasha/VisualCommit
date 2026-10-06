using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>Finds the git executable on this machine and checks that it is new enough.</summary>
public static class GitLocator
{
    /// <summary>
    /// Finds git, asks it for its version and returns a runner for it when it is usable.
    /// Never throws for a missing or broken git; the result says what was found.
    /// </summary>
    public static async Task<GitDetection> DetectAsync(
        IGitCallLog? callLog = null,
        IAppLog? log = null,
        CancellationToken cancellationToken = default)
    {
        log ??= NullAppLog.Instance;

        var path = FindExecutable(GitSearchContext.FromSystem());
        if (path is null)
        {
            log.Warning("Git was not found on the PATH or in the usual install folders.");
            return new GitDetection(GitAvailability.NotFound, null, null, null);
        }

        var runner = new GitRunner(path, callLog, log);
        GitResult result;
        try
        {
            result = await runner.RunAsync(new GitCommand("--version"), cancellationToken).ConfigureAwait(false);
        }
        catch (GitException ex)
        {
            log.Warning($"Git at {path} could not be started.", ex);
            return new GitDetection(GitAvailability.Broken, path, null, null);
        }

        if (!result.Succeeded || !GitVersion.TryParse(result.StandardOutput, out var version))
        {
            log.Warning($"Git at {path} did not report a version: exit {result.ExitCode}, \"{result.StandardOutput.Trim()}\".");
            return new GitDetection(GitAvailability.Broken, path, null, null);
        }

        if (!version.IsSupported)
        {
            log.Warning($"Git {version} at {path} is older than the minimum, {GitVersion.Minimum}.");
            return new GitDetection(GitAvailability.TooOld, path, version, null);
        }

        log.Info($"Using Git {version} at {path}.");
        return new GitDetection(GitAvailability.Available, path, version, runner);
    }

    /// <summary>
    /// Returns the full path of the git executable: the first one on the PATH, otherwise the
    /// first that exists among the usual install locations. Null when there is none.
    /// </summary>
    public static string? FindExecutable(GitSearchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var fileName = context.IsWindows ? "git.exe" : "git";
        var separator = context.IsWindows ? ';' : ':';
        var pathFolders = (context.PathVariable ?? string.Empty)
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(folder => folder.Trim('"'))
            .Where(folder => folder.Length > 0);

        foreach (var folder in pathFolders)
        {
            string candidate;
            try
            {
                candidate = Path.Combine(folder, fileName);
            }
            catch (ArgumentException)
            {
                continue; // A PATH entry with characters that are not valid in a path.
            }

            if (context.FileExists(candidate))
            {
                return candidate;
            }
        }

        return context.WellKnownLocations.FirstOrDefault(context.FileExists);
    }
}

/// <summary>What <see cref="GitLocator.FindExecutable"/> looks at. Tests supply their own.</summary>
/// <param name="IsWindows">Whether to look for "git.exe" and split the PATH on semicolons.</param>
/// <param name="PathVariable">The value of the PATH environment variable.</param>
/// <param name="WellKnownLocations">Full paths to try, in order, when git is not on the PATH.</param>
/// <param name="FileExists">Whether a file exists at a path.</param>
public sealed record GitSearchContext(
    bool IsWindows,
    string? PathVariable,
    IReadOnlyList<string> WellKnownLocations,
    Func<string, bool> FileExists)
{
    public static GitSearchContext FromSystem() =>
        new(
            OperatingSystem.IsWindows(),
            Environment.GetEnvironmentVariable("PATH"),
            SystemWellKnownLocations(),
            File.Exists);

    private static List<string> SystemWellKnownLocations()
    {
        if (!OperatingSystem.IsWindows())
        {
            // An app started from the macOS Finder or a Linux desktop launcher gets a short PATH
            // that can miss these.
            return ["/usr/bin/git", "/usr/local/bin/git", "/opt/homebrew/bin/git", "/opt/local/bin/git"];
        }

        var roots = new[]
        {
            Environment.GetEnvironmentVariable("ProgramW6432"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        };

        return roots
            .Where(root => !string.IsNullOrEmpty(root))
            .Select(root => Path.Combine(root!, "Git", "cmd", "git.exe"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
