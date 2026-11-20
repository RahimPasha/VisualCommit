using System.Diagnostics;
using System.Text.RegularExpressions;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>Finds the git executable on this machine and checks that it is new enough.</summary>
public static partial class GitLocator
{
    /// <summary>
    /// The test switch that names the machine's only git (a full path), or says it has none
    /// (<c>none</c>); read once by the app session at its start. Never set outside a test.
    /// </summary>
    public const string StandInVariable = "VISUALCOMMIT_GIT_STAND_IN";

    /// <summary>The most of git's words a detection keeps (<see cref="GitDetection.ErrorText"/>).</summary>
    public const int ErrorTextLimit = 4000;

    /// <summary>The git of Apple's Command Line Tools, a shim that offers to install them while they are missing.</summary>
    public const string MacOSSystemGit = "/usr/bin/git";

    /// <summary>
    /// Finds git, asks it for its version and returns a runner for it when it is usable.
    /// Never throws for a missing or broken git; the result says what was found. The runner records
    /// its calls in <paramref name="callLog"/> with the times of <paramref name="recordClock"/>
    /// (<see cref="GitRunner"/>) and runs them with the session's git environment
    /// <paramref name="environment"/> (docs/phase-6-design.md 17.2; null for none).
    /// </summary>
    public static Task<GitDetection> DetectAsync(
        IGitCallLog? callLog = null,
        IAppLog? log = null,
        TimeProvider? recordClock = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default) =>
        DetectAsync(new GitLookup(null, null), callLog, log, recordClock, environment, cancellationToken);

    /// <summary>
    /// Finds git in the order <paramref name="lookup"/> gives, off the caller's thread (it may start
    /// <c>xcode-select</c>): the git chosen in Settings, run whatever it answers and with no
    /// fallback; else the stand-in, the machine's only git while it is set; else the PATH's fully
    /// qualified folders, then the usual install folders. A chosen git or a stand-in is run only from
    /// a full path, on Windows to an <c>.exe</c> file (<see cref="IsRunnablePath"/>). On macOS
    /// <c>/usr/bin/git</c> is run only while <c>xcode-select -p</c> succeeds. Never throws for a
    /// missing or broken git; the result says what was found and where it came from.
    /// </summary>
    public static Task<GitDetection> DetectAsync(
        GitLookup lookup,
        IGitCallLog? callLog = null,
        IAppLog? log = null,
        TimeProvider? recordClock = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        return Task.Run(
            () => DetectAsync(lookup, GitSearchContext.FromSystem(), callLog, log, recordClock, environment, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Whether a git chosen in Settings, or the stand-in, may be run on this machine: a full path
    /// (<see cref="Path.IsPathFullyQualified(string)"/>), on Windows to an <c>.exe</c> file. A
    /// relative path would resolve against the app's current folder, and a <c>.cmd</c> runs through
    /// <c>cmd.exe</c>, where the quoting of git's arguments does not hold.
    /// </summary>
    public static bool IsRunnablePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return WhyNotRunnable(path, OperatingSystem.IsWindows()) is null;
    }

    /// <summary>
    /// <see cref="DetectAsync(GitLookup, IGitCallLog?, IAppLog?, TimeProvider?, IReadOnlyDictionary{string, string?}?, CancellationToken)"/>
    /// over <paramref name="context"/> instead of the machine, on the caller's thread: the tests'
    /// way to the platform's rules.
    /// </summary>
    internal static async Task<GitDetection> DetectAsync(
        GitLookup lookup,
        GitSearchContext context,
        IGitCallLog? callLog,
        IAppLog? log,
        TimeProvider? recordClock,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(context);
        log ??= NullAppLog.Instance;
        cancellationToken.ThrowIfCancellationRequested();
        var tools = new ToolsQuestion(context);

        if (lookup.ChosenPath is { } chosen)
        {
            // The shim of the Command Line Tools offers to install them instead of running git.
            if (context.IsMacOS && chosen == MacOSSystemGit && !tools.Installed)
            {
                log.Warning($"The Git chosen in Settings, {MacOSSystemGit}, is not used: Apple's Command Line Tools are not installed (xcode-select -p failed).");
                return new GitDetection(GitAvailability.NotFound, null, null, null) { Source = GitSource.Setting, CommandLineToolsMissing = true };
            }

            if (WhyNotRunnable(chosen, context.IsWindows) is { } why)
            {
                log.Warning($"The Git chosen in Settings is not used: {chosen} is {why}.");
                return ChosenMissing(chosen, lookup, context, tools);
            }

            if (!context.FileExists(chosen))
            {
                log.Warning($"The Git chosen in Settings was not found at {chosen}.");
                return ChosenMissing(chosen, lookup, context, tools);
            }

            return await VersionAsync(chosen, GitSource.Setting, context, callLog, log, recordClock, environment, cancellationToken).ConfigureAwait(false);
        }

        if (lookup.StandIn is { } standIn)
        {
            if (StandInPath(standIn, context) is not { } stand)
            {
                log.Info($"Git was not looked for on this computer: {StandInVariable} names no git program ({standIn}).");
                return new GitDetection(GitAvailability.NotFound, null, null, null) { Source = GitSource.StandIn };
            }

            return await VersionAsync(stand, GitSource.StandIn, context, callLog, log, recordClock, environment, cancellationToken).ConfigureAwait(false);
        }

        var found = Search(context, tools);
        if (found.Path is not { } path)
        {
            log.Warning(found.CommandLineToolsMissing
                ? $"Git was not found on the PATH or in the usual install folders; {MacOSSystemGit} was left out: Apple's Command Line Tools are not installed (xcode-select -p failed)."
                : "Git was not found on the PATH or in the usual install folders.");
            return new GitDetection(GitAvailability.NotFound, null, null, null) { CommandLineToolsMissing = found.CommandLineToolsMissing };
        }

        var detection = await VersionAsync(path, found.Source, context, callLog, log, recordClock, environment, cancellationToken).ConfigureAwait(false);
        return detection with { CommandLineToolsMissing = found.CommandLineToolsMissing };
    }

    /// <summary>
    /// Returns the full path of the git executable: the first one in a fully qualified folder of
    /// the PATH, otherwise the first that exists among the usual install locations; on macOS
    /// <c>/usr/bin/git</c> only while the Command Line Tools are installed. Null when there is none.
    /// </summary>
    public static string? FindExecutable(GitSearchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Search(context, new ToolsQuestion(context)).Path;
    }

    /// <summary>
    /// Why <paramref name="path"/> may not be run as git on a machine that is Windows or not
    /// (<paramref name="windows"/>): <c>not a full path</c>, <c>not an .exe file</c>; null when it may.
    /// </summary>
    internal static string? WhyNotRunnable(string path, bool windows)
    {
        if (!IsFullyQualified(path, windows))
        {
            return "not a full path";
        }

        return windows && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? "not an .exe file" : null;
    }

    /// <summary>
    /// Whether <paramref name="path"/> is fully qualified on a machine that is Windows or not: this
    /// machine's own rule (<see cref="Path.IsPathFullyQualified(string)"/>) when it is that machine,
    /// so that the app's search is the platform's; else the other platform's, for a test's context.
    /// </summary>
    internal static bool IsFullyQualified(string path, bool windows)
    {
        if (windows == OperatingSystem.IsWindows())
        {
            return Path.IsPathFullyQualified(path);
        }

        return windows
            ? (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/') || path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)
            : path.StartsWith('/');
    }

    /// <summary>The first line of <paramref name="text"/>, without its line end.</summary>
    public static string FirstLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var end = text.IndexOf('\n', StringComparison.Ordinal);
        return (end < 0 ? text : text[..end]).TrimEnd('\r');
    }

    /// <summary>The line of git's configuration that git said it could not read (<c>fatal: bad config line &lt;n&gt; in file &lt;f&gt;</c>); null for none.</summary>
    internal static GitConfigFault? ConfigFaultOf(string said)
    {
        ArgumentNullException.ThrowIfNull(said);
        var match = BadConfigLine().Match(said.Replace("\r\n", "\n", StringComparison.Ordinal));
        return match.Success && int.TryParse(match.Groups[1].ValueSpan, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var line)
            ? new GitConfigFault(match.Groups[2].Value.TrimEnd(), line)
            : null;
    }

    [GeneratedRegex(@"^fatal: bad config line (\d+) in file (.+)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex BadConfigLine();

    /// <summary>A gone or unrunnable choice (no fallback): what the next steps would find, not run, as the candidate.</summary>
    private static GitDetection ChosenMissing(string chosen, GitLookup lookup, GitSearchContext context, ToolsQuestion tools)
    {
        var next = lookup.StandIn is { } standIn ? (StandInPath(standIn, context), GitSource.StandIn, false) : Search(context, tools);
        return new GitDetection(GitAvailability.NotFound, null, null, null)
        {
            Source = GitSource.Setting,
            ChosenMissing = chosen,
            PathCandidate = next.Item1,
            CommandLineToolsMissing = next.Item3,
        };
    }

    /// <summary>The stand-in's program when it names one that may be run and is there; null for <c>none</c> and any other value.</summary>
    private static string? StandInPath(string standIn, GitSearchContext context) =>
        WhyNotRunnable(standIn, context.IsWindows) is null && context.FileExists(standIn) ? standIn : null;

    /// <summary>The machine's search: the PATH's fully qualified folders, then the usual install folders; on macOS <c>/usr/bin/git</c> left out while the tools are missing.</summary>
    private static (string? Path, GitSource Source, bool CommandLineToolsMissing) Search(GitSearchContext context, ToolsQuestion tools)
    {
        var fileName = context.IsWindows ? "git.exe" : "git";
        var separator = context.IsWindows ? ';' : ':';
        var skipped = false;
        bool Usable(string candidate)
        {
            if (!context.FileExists(candidate))
            {
                return false;
            }

            if (context.IsMacOS && candidate == MacOSSystemGit && !tools.Installed)
            {
                skipped = true;
                return false;
            }

            return true;
        }

        var pathFolders = (context.PathVariable ?? string.Empty)
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(folder => folder.Trim('"'))
            .Where(folder => folder.Length > 0);

        foreach (var folder in pathFolders)
        {
            // A relative entry would name the app's current folder, which can be a cloned repository (D449).
            if (!IsFullyQualified(folder, context.IsWindows))
            {
                continue;
            }

            string candidate;
            try
            {
                candidate = Path.Combine(folder, fileName);
            }
            catch (ArgumentException)
            {
                continue; // A PATH entry with characters that are not valid in a path.
            }

            if (Usable(candidate))
            {
                return (candidate, GitSource.Path, skipped);
            }
        }

        foreach (var location in context.WellKnownLocations)
        {
            if (Usable(location))
            {
                return (location, GitSource.InstallFolder, skipped);
            }
        }

        return (null, GitSource.Path, skipped);
    }

    /// <summary>Asks <paramref name="path"/> for its version (<c>git --version</c>) and says what it is.</summary>
    private static async Task<GitDetection> VersionAsync(
        string path,
        GitSource source,
        GitSearchContext context,
        IGitCallLog? callLog,
        IAppLog log,
        TimeProvider? recordClock,
        IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken)
    {
        var runner = new GitRunner(path, callLog, log, recordClock, environment);
        GitResult result;
        try
        {
            result = await runner.RunAsync(new GitCommand("--version"), cancellationToken).ConfigureAwait(false);
        }
        catch (GitException ex)
        {
            var message = Limit((ex.InnerException?.Message ?? ex.StandardError).Trim());
            log.Warning($"Git at {path} could not be started: {FirstLine(message)}", ex);
            return new GitDetection(GitAvailability.Broken, path, null, null) { Source = source, ErrorText = message };
        }

        if (!result.Succeeded || !GitVersion.TryParse(result.StandardOutput, out var version))
        {
            // Git's own words: standard error, else what it printed.
            var said = Limit((result.StandardError.Trim().Length > 0 ? result.StandardError : result.StandardOutput).Trim());
            log.Warning($"Git at {path} did not report its version: exit {result.ExitCode}, \"{FirstLine(said)}\".");
            return new GitDetection(GitAvailability.Broken, path, null, null)
            {
                Source = source,
                ErrorText = said,
                ExitCode = result.ExitCode,
                ConfigFault = ConfigFaultOf(said),
            };
        }

        if (!version.IsSupported)
        {
            log.Warning($"Git {version} at {path} is older than the minimum, {GitVersion.Minimum}.");
            return new GitDetection(GitAvailability.TooOld, path, version, null) { Source = source };
        }

        log.Info($"Using Git {version} at {path}.");
        log.Debug($"The Git in use came from {SourceWords(source)}.");
        if (context.IsWindows && !result.StandardOutput.Contains(".windows.", StringComparison.Ordinal))
        {
            log.Warning($"Git {version} at {path} is not Git for Windows; VisualCommit is tested with Git for Windows.");
        }

        return new GitDetection(GitAvailability.Available, path, version, runner) { Source = source };
    }

    private static string SourceWords(GitSource source) => source switch
    {
        GitSource.Setting => "the setting",
        GitSource.StandIn => StandInVariable,
        GitSource.InstallFolder => "a usual install folder",
        _ => "the PATH",
    };

    private static string Limit(string text) => text.Length <= ErrorTextLimit ? text : text[..ErrorTextLimit];

    /// <summary>
    /// The question of macOS's <c>xcode-select -p</c>, asked at most once per search and only when
    /// <c>/usr/bin/git</c> is met: true without a question to ask.
    /// </summary>
    private sealed class ToolsQuestion(GitSearchContext context)
    {
        private bool? _installed;

        public bool Installed => _installed ??= context.CommandLineToolsInstalled?.Invoke() ?? true;
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
    /// <summary>How long <c>xcode-select -p</c> may take before the tools count as missing.</summary>
    private static readonly TimeSpan XcodeSelectLimit = TimeSpan.FromSeconds(5);

    /// <summary>Whether the search runs on macOS, where <c>/usr/bin/git</c> is the Command Line Tools' shim.</summary>
    public bool IsMacOS { get; init; }

    /// <summary>On macOS, whether Apple's Command Line Tools are installed (<c>xcode-select -p</c> exits 0); asked at most once per search. Null: installed.</summary>
    public Func<bool>? CommandLineToolsInstalled { get; init; }

    public static GitSearchContext FromSystem() =>
        new(
            OperatingSystem.IsWindows(),
            Environment.GetEnvironmentVariable("PATH"),
            SystemWellKnownLocations(),
            File.Exists)
        {
            IsMacOS = OperatingSystem.IsMacOS(),
            CommandLineToolsInstalled = OperatingSystem.IsMacOS() ? XcodeSelectSucceeds : null,
        };

    private static List<string> SystemWellKnownLocations()
    {
        if (!OperatingSystem.IsWindows())
        {
            // An app started from the macOS Finder or a Linux desktop launcher gets a short PATH
            // that can miss these.
            return [GitLocator.MacOSSystemGit, "/usr/local/bin/git", "/opt/homebrew/bin/git", "/opt/local/bin/git"];
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

    /// <summary>
    /// <c>/usr/bin/xcode-select -p</c> exits 0 within <see cref="XcodeSelectLimit"/>: the Command
    /// Line Tools (or Xcode) are installed. A tool that cannot start, fails or does not answer counts
    /// as missing; one that does not answer is stopped.
    /// </summary>
    private static bool XcodeSelectSucceeds()
    {
        try
        {
            var start = new ProcessStartInfo("/usr/bin/xcode-select")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("-p");
            using var process = Process.Start(start);
            if (process is null)
            {
                return false;
            }

            // Read both streams, so that a full pipe cannot hold the tool.
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(XcodeSelectLimit))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            Task.WaitAll([output, error], XcodeSelectLimit);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }
}
