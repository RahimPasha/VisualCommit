using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.Services;

/// <summary>
/// <see cref="IFileLauncher"/> on the platform (docs/phase-3-design.md 2.32): "Open file" through
/// Avalonia's launcher (<c>TopLevel.Launcher.LaunchFileInfoAsync</c>), "Show in folder" through
/// the platform's file manager: <c>explorer.exe /select,"&lt;path&gt;"</c> on Windows,
/// <c>open -R &lt;path&gt;</c> on macOS, and on Linux the
/// <c>org.freedesktop.FileManager1.ShowItems</c> D-Bus call through <c>dbus-send</c>, falling back
/// to <c>xdg-open &lt;folder&gt;</c>. A missing file shows its folder. The commands are built by
/// <see cref="RevealCommands"/>, which the tests check without launching anything; the default
/// test run never creates this class (<c>ShellDriver</c> injects a fake).
/// </summary>
public sealed class FileLauncher : IFileLauncher
{
    /// <summary>How long <c>dbus-send</c> may take for its answer before the fallback is tried.</summary>
    private static readonly TimeSpan CheckedCommandTimeout = TimeSpan.FromSeconds(6);

    public FileLauncher(Func<TopLevel?> topLevel, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        TopLevel = topLevel;
        Log = log ?? NullAppLog.Instance;
    }

    private Func<TopLevel?> TopLevel { get; }

    private IAppLog Log { get; }

    public async Task OpenAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => OpenAsync(fullPath, cancellationToken)).ConfigureAwait(false);
            return;
        }

        if (TopLevel() is not { } topLevel)
        {
            Log.Warning($"Could not open {fullPath}: there is no window.");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await topLevel.Launcher.LaunchFileInfoAsync(new FileInfo(fullPath)))
        {
            Log.Warning($"The system could not open {fullPath}.");
        }
    }

    /// <summary>
    /// Opens a web address in the default browser through Avalonia's launcher
    /// (<c>TopLevel.Launcher.LaunchUriAsync</c>), on the UI thread (docs/phase-7a-design.md 1.7).
    /// Only <c>http</c> and <c>https</c>: any other scheme throws <see cref="ArgumentException"/>.
    /// </summary>
    public async Task OpenUrlAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Only an http or https address can be opened in the browser.", nameof(url));
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => OpenUrlAsync(url, cancellationToken)).ConfigureAwait(false);
            return;
        }

        if (TopLevel() is not { } topLevel)
        {
            Log.Warning($"Could not open {url.AbsoluteUri}: there is no window.");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await topLevel.Launcher.LaunchUriAsync(url))
        {
            Log.Warning($"The system could not open {url.AbsoluteUri} in the browser.");
        }
    }

    public Task RevealAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        var commands = RevealCommands(Path.GetFullPath(fullPath), CurrentPlatform, File.Exists, Directory.Exists);

        // Off the UI thread: starting a process can hold it for a few hundred milliseconds (D59).
        return Task.Run(() => RunAsync(commands, cancellationToken), CancellationToken.None);
    }

    /// <summary>
    /// Starts an editor (docs/phase-6-design.md 12.4) off the UI thread (D59): first checks there
    /// that its program exists (a <see cref="FileNotFoundException"/> when not: a check of a share
    /// that does not answer blocks for minutes, 0.9), then starts it with no shell, not waited for,
    /// its process object disposed at once. Throws when it cannot start (12.7).
    /// </summary>
    public Task OpenInEditorAsync(LaunchCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Task.Run(() => StartEditor(command, File.Exists), cancellationToken);
    }

    /// <summary>
    /// The start <see cref="OpenInEditorAsync"/> makes, given how files are checked: throws
    /// <see cref="FileNotFoundException"/> before any start when <paramref name="fileExists"/> says
    /// the program is not there.
    /// </summary>
    public static void StartEditor(LaunchCommand command, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(fileExists);
        if (!fileExists(command.FileName))
        {
            throw new FileNotFoundException($"{command.FileName} does not exist.", command.FileName);
        }

        using var process = Process.Start(command.ToStartInfo())
            ?? throw new InvalidOperationException($"{command.FileName} did not start.");
    }

    /// <summary>The platform this process runs on, as <see cref="RevealCommands"/> takes it; any other Unix counts as Linux.</summary>
    public static RevealPlatform CurrentPlatform =>
        OperatingSystem.IsWindows() ? RevealPlatform.Windows
        : OperatingSystem.IsMacOS() ? RevealPlatform.MacOS
        : RevealPlatform.Linux;

    /// <summary>
    /// The commands that show <paramref name="fullPath"/> (absolute) in the file manager of
    /// <paramref name="platform"/>, in the order they are tried: the next one runs only when one
    /// with <see cref="LaunchCommand.FallBackOnFailure"/> could not start or exited with an error.
    /// A path that exists (a file or a folder) is shown selected in its folder; a missing one
    /// shows the nearest folder above it that exists.
    /// </summary>
    public static IReadOnlyList<LaunchCommand> RevealCommands(
        string fullPath,
        RevealPlatform platform,
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(directoryExists);

        // Explorer takes backslashes only.
        var path = TrimSeparator(platform == RevealPlatform.Windows ? fullPath.Replace('/', '\\') : fullPath, platform);
        var exists = fileExists(path) || directoryExists(path);
        var folder = exists ? ParentFolder(path, platform) : NearestFolder(path, platform, directoryExists);

        switch (platform)
        {
            case RevealPlatform.Windows:
                // Explorer reads its command line itself: "/select," and the quoted path must stay
                // one piece, which .NET's quoting of an argument list would break up.
                return [exists
                    ? new LaunchCommand("explorer.exe", []) { CommandLine = "/select," + QuoteForExplorer(path) }
                    : new LaunchCommand("explorer.exe", []) { CommandLine = QuoteForExplorer(folder) }];

            case RevealPlatform.MacOS:
                return [exists ? new LaunchCommand("open", ["-R", path]) : new LaunchCommand("open", [folder])];

            case RevealPlatform.Linux:
                var openFolder = new LaunchCommand("xdg-open", [folder]);
                if (!exists)
                {
                    return [openFolder];
                }

                return
                [
                    new LaunchCommand(
                        "dbus-send",
                        [
                            "--session",
                            "--print-reply",
                            "--reply-timeout=5000",
                            "--dest=org.freedesktop.FileManager1",
                            "--type=method_call",
                            "/org/freedesktop/FileManager1",
                            "org.freedesktop.FileManager1.ShowItems",
                            "array:string:" + FileUri(path),
                            "string:",
                        ])
                    {
                        FallBackOnFailure = true,
                    },
                    openFolder,
                ];

            default:
                throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
        }
    }

    /// <summary>
    /// A <c>file://</c> URI for an absolute Unix path, with every byte of its UTF-8 form other than
    /// letters, digits, <c>-._~</c> and <c>/</c> percent-encoded: a comma, which <c>dbus-send</c>
    /// would take as the end of an array element, included.
    /// </summary>
    public static string FileUri(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        var uri = new StringBuilder("file://", absolutePath.Length + 16);
        foreach (var b in Encoding.UTF8.GetBytes(absolutePath))
        {
            var c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~' or '/')
            {
                uri.Append(c);
            }
            else
            {
                uri.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return uri.ToString();
    }

    private async Task RunAsync(IReadOnlyList<LaunchCommand> commands, CancellationToken cancellationToken)
    {
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryRunAsync(command, cancellationToken).ConfigureAwait(false) || !command.FallBackOnFailure)
            {
                return;
            }
        }

        Log.Warning($"Could not show the file in its folder: {string.Join(", then ", commands.Select(command => command.DisplayText))} failed.");
    }

    /// <summary>
    /// The absolute path of <paramref name="program"/> in the first folder of
    /// <paramref name="searchPath"/> (a PATH value, folders split by <paramref name="separator"/>)
    /// that holds it, or null. Only fully qualified folders are searched: an empty or relative
    /// entry would name the current folder, which can be a cloned repository that holds a file of
    /// that name. Started by its bare name, .NET would also look in the app's folder and the
    /// current folder first, before PATH (on Linux and macOS).
    /// </summary>
    public static string? FindOnPath(string program, string? searchPath, char separator, Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(program);
        ArgumentNullException.ThrowIfNull(fileExists);
        foreach (var folder in (searchPath ?? string.Empty).Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Path.IsPathFullyQualified(folder))
            {
                continue;
            }

            var candidate = Path.Combine(folder, program);
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Where <paramref name="program"/> of a <see cref="LaunchCommand"/> is started from: Explorer
    /// from the Windows folder, anything else from <see cref="FindOnPath"/>; null when it is not found.
    /// </summary>
    private static string? ResolveProgram(string program)
    {
        if (OperatingSystem.IsWindows() && string.Equals(program, "explorer.exe", StringComparison.OrdinalIgnoreCase))
        {
            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), program);
            if (File.Exists(explorer))
            {
                return explorer;
            }
        }

        return FindOnPath(program, Environment.GetEnvironmentVariable("PATH"), Path.PathSeparator, File.Exists);
    }

    /// <summary>
    /// Starts <paramref name="command"/> from the absolute path of its program (<see cref="ResolveProgram"/>).
    /// One without <see cref="LaunchCommand.FallBackOnFailure"/> is left to run (Explorer's exit
    /// code says nothing); one with it is waited for and must exit with 0.
    /// </summary>
    private async Task<bool> TryRunAsync(LaunchCommand command, CancellationToken cancellationToken)
    {
        if (ResolveProgram(command.FileName) is not { } program)
        {
            Log.Info($"Could not start {command.DisplayText}: {command.FileName} is not on the PATH.");
            return false;
        }

        var startInfo = command.ToStartInfo();
        startInfo.FileName = program;
        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            if (!command.FallBackOnFailure)
            {
                return true;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CheckedCommandTimeout);
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                await Task.WhenAll(output, error).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                Log.Info($"{command.DisplayText} did not answer within {CheckedCommandTimeout.TotalSeconds:F0} seconds.");
                return false;
            }

            if (process.ExitCode != 0)
            {
                Log.Info($"{command.DisplayText} exited with {process.ExitCode}: {(await error.ConfigureAwait(false)).Trim()}");
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            Log.Info($"Could not start {command.DisplayText}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// A path in double quotes, which Windows paths cannot contain. Only a drive's root ends with
    /// a backslash, which would escape the closing quote; it has no spaces and goes unquoted.
    /// </summary>
    private static string QuoteForExplorer(string path) => path.EndsWith('\\') ? path : $"\"{path}\"";

    private static readonly char[] WindowsSeparators = ['\\', '/'];

    private static readonly char[] UnixSeparators = ['/'];

    private static char[] Separators(RevealPlatform platform) => platform == RevealPlatform.Windows ? WindowsSeparators : UnixSeparators;

    private static string TrimSeparator(string path, RevealPlatform platform)
    {
        var trimmed = path.TrimEnd(Separators(platform));

        // A root ("C:\", "/") keeps its separator.
        return trimmed.Length == 0 || trimmed.EndsWith(':') ? path : trimmed;
    }

    /// <summary>The folder that holds <paramref name="path"/>, as text for <paramref name="platform"/>, or the path itself for a root.</summary>
    private static string ParentFolder(string path, RevealPlatform platform)
    {
        var separators = Separators(platform);
        var cut = path.TrimEnd(separators).LastIndexOfAny(separators);
        if (cut < 0)
        {
            return path;
        }

        // "C:\file" → "C:\", "/file" → "/".
        var parent = path[..cut];
        return parent.Length == 0 || parent.EndsWith(':') ? path[..(cut + 1)] : parent;
    }

    /// <summary>The nearest folder above <paramref name="path"/> that exists, or the path's root.</summary>
    private static string NearestFolder(string path, RevealPlatform platform, Func<string, bool> directoryExists)
    {
        var folder = ParentFolder(path, platform);
        while (!directoryExists(folder))
        {
            var parent = ParentFolder(folder, platform);
            if (parent == folder)
            {
                break;
            }

            folder = parent;
        }

        return folder;
    }
}

/// <summary>The platforms whose file managers <see cref="FileLauncher.RevealCommands"/> knows.</summary>
public enum RevealPlatform
{
    Windows,
    MacOS,
    Linux,
}

/// <summary>A program to start for <see cref="FileLauncher"/>.</summary>
/// <param name="FileName">The program's name, started from its absolute path on the PATH (<see cref="FileLauncher.FindOnPath"/>; Explorer from the Windows folder).</param>
/// <param name="Arguments">Its arguments, each passed as one argument.</param>
public sealed record LaunchCommand(string FileName, IReadOnlyList<string> Arguments)
{
    /// <summary>
    /// The whole command line, passed as it is, in place of <see cref="Arguments"/>: for a program
    /// that parses its command line itself, as Explorer does. Null for none.
    /// </summary>
    public string? CommandLine { get; init; }

    /// <summary>Whether the command is waited for and the next one tried when it cannot start or exits with an error.</summary>
    public bool FallBackOnFailure { get; init; }

    /// <summary>The folder the program starts in (an editor's, docs/phase-6-design.md 12.4); null for this process's.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Variables of this process's environment the program does not get (<c>ELECTRON_RUN_AS_NODE</c> for an editor, 12.4).</summary>
    public IReadOnlyList<string> RemovedVariables { get; init; } = [];

    /// <summary>The command as a user would type it, for the log.</summary>
    public string DisplayText => CommandLine is not null
        ? $"{FileName} {CommandLine}"
        : string.Join(' ', [FileName, .. Arguments.Select(argument => argument.Length == 0 || argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument)]);

    /// <summary>The process to start: no shell, the output read when the command is waited for.</summary>
    public ProcessStartInfo ToStartInfo()
    {
        var startInfo = new ProcessStartInfo(FileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = FallBackOnFailure,
            RedirectStandardError = FallBackOnFailure,
        };

        if (CommandLine is not null)
        {
            startInfo.Arguments = CommandLine;
        }
        else
        {
            foreach (var argument in Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        if (WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = WorkingDirectory;
        }

        foreach (var variable in RemovedVariables)
        {
            // The environment's keys ignore case on Windows, as the process's do.
            foreach (var key in startInfo.Environment.Keys.Where(key => string.Equals(key, variable, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).ToList())
            {
                startInfo.Environment.Remove(key);
            }
        }

        return startInfo;
    }
}
