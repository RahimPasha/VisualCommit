using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// One open repository, read through the real git executable (see <see cref="IGitRepository"/>).
/// It is created by <see cref="OpenAsync"/>, <see cref="InitAsync"/> or <see cref="CloneAsync"/>.
/// <para>
/// Every call that only reads runs in the working tree with <c>GIT_OPTIONAL_LOCKS=0</c>, so that
/// reading never writes to the repository and cannot set off the app's own file watcher (D47).
/// The calls also pass the options that keep the user's git configuration from changing the
/// output they parse: signatures, colours, output encoding, rename detection and external diff
/// tools are fixed on the command line, and paths are read in the <c>-z</c> form, which
/// <c>core.quotepath</c> does not touch.
/// </para>
/// </summary>
public sealed partial class GitRepository : IGitRepository
{
    /// <summary>The environment of every call that only reads.</summary>
    private static readonly IReadOnlyDictionary<string, string?> ReadEnvironment = new Dictionary<string, string?>
    {
        ["GIT_OPTIONAL_LOCKS"] = "0",
    };

    private readonly IGitRunner _runner;
    private readonly IAppLog _log;

    private GitRepository(IGitRunner runner, IAppLog log, string workingDirectory, string gitDirectory, string commonDirectory)
    {
        _runner = runner;
        _log = log;
        WorkingDirectory = workingDirectory;
        GitDirectory = gitDirectory;
        CommonDirectory = commonDirectory;
        var name = Path.GetFileName(workingDirectory);
        Name = name.Length > 0 ? name : workingDirectory;
    }

    public string WorkingDirectory { get; }

    public string GitDirectory { get; }

    public string CommonDirectory { get; }

    public string Name { get; }

    /// <summary>
    /// Opens the repository whose working tree contains <paramref name="path"/>: its top-level
    /// folder or any folder inside it.
    /// </summary>
    /// <exception cref="NotARepositoryException">
    /// The folder does not exist, is not inside a working tree, or is a bare repository or a git folder.
    /// </exception>
    /// <exception cref="GitException">Git failed for another reason, such as a repository owned by another user; the message is git's own.</exception>
    public static async Task<GitRepository> OpenAsync(
        IGitRunner runner,
        string path,
        CancellationToken cancellationToken = default,
        IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        log ??= NullAppLog.Instance;

        string folder;
        try
        {
            folder = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new NotARepositoryException(path);
        }

        if (!Directory.Exists(folder))
        {
            throw new NotARepositoryException(path);
        }

        // One call answers everything in the usual case. --show-toplevel fails in a bare
        // repository and inside a git folder, but --is-inside-work-tree has printed "false" by then.
        var command = ReadCommand(folder, "rev-parse", "--is-inside-work-tree", "--show-toplevel", "--absolute-git-dir", "--git-common-dir");
        var result = await runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        var lines = SplitLines(result.StandardOutput);
        if (lines.Count > 0 && lines[0] == "false")
        {
            throw new NotARepositoryException(path);
        }

        if (!result.Succeeded)
        {
            // The runner makes git's messages English, so this text can be relied on. Any other
            // failure, such as "detected dubious ownership", is git's to explain.
            if (result.StandardError.Contains("not a git repository", StringComparison.Ordinal))
            {
                throw new NotARepositoryException(path);
            }

            result.EnsureSuccess(command);
        }

        if (lines.Count < 4 || lines[0] != "true")
        {
            throw new GitException(command.DisplayText, result.ExitCode, $"Unexpected output: {result.StandardOutput.Trim()}");
        }

        var workingDirectory = NormalizePath(lines[1], folder);
        var gitDirectory = NormalizePath(lines[2], folder);
        var commonDirectory = await ResolveCommonDirectoryAsync(runner, lines[3], folder, workingDirectory, cancellationToken).ConfigureAwait(false);

        log.Info($"Opened the repository at {workingDirectory}.");
        return new GitRepository(runner, log, workingDirectory, gitDirectory, commonDirectory);
    }

    /// <summary>
    /// Runs <c>git init</c> in <paramref name="path"/>, creating the folder and its parents if
    /// needed, and opens the result. HEAD is unborn afterwards: the branch has no commits yet.
    /// </summary>
    /// <exception cref="GitException">Git reported an error; the message is git's own.</exception>
    public static async Task<GitRepository> InitAsync(
        IGitRunner runner,
        string path,
        CancellationToken cancellationToken = default,
        IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var folder = Path.GetFullPath(path);
        Directory.CreateDirectory(folder);
        var command = new GitCommand("init", "--quiet") { WorkingDirectory = folder };
        (await runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return await OpenAsync(runner, folder, cancellationToken, log).ConfigureAwait(false);
    }

    public IRepositoryWatcher CreateWatcher() =>
        new RepositoryWatcher(GitDirectory, CommonDirectory, _log, workingDirectory: WorkingDirectory, allIgnored: AllIgnoredAsync);

    /// <summary>
    /// Whether git ignores every one of <paramref name="paths"/> (relative to the working tree):
    /// <c>git check-ignore</c> prints those it ignores, and exits with 1 when it ignores none.
    /// </summary>
    internal async Task<bool> AllIgnoredAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var command = new GitCommand("check-ignore", "--stdin", "-z")
        {
            WorkingDirectory = WorkingDirectory,
            Environment = ReadEnvironment,
            StandardInput = string.Concat(paths.Select(path => path + "\0")),
        };
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1)
        {
            return false;
        }

        var ignored = result.EnsureSuccess(command).StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        return paths.All(ignored.Contains);
    }

    /// <summary>
    /// <c>--git-common-dir</c> prints a path relative to the folder git ran in, and before Git 2.31
    /// there is no option to make it absolute. Git's folder is the physical one, so where the
    /// folder that was opened is not the top level (a subfolder, or a path through a symbolic
    /// link), the question is asked again in the top level, whose path git itself reported.
    /// </summary>
    private static async Task<string> ResolveCommonDirectoryAsync(
        IGitRunner runner,
        string reported,
        string folder,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (Path.IsPathFullyQualified(reported))
        {
            return NormalizePath(reported, workingDirectory);
        }

        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (string.Equals(Path.TrimEndingDirectorySeparator(folder), workingDirectory, comparison))
        {
            return NormalizePath(reported, workingDirectory);
        }

        var command = ReadCommand(workingDirectory, "rev-parse", "--git-common-dir");
        var result = (await runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        return NormalizePath(result.StandardOutput.Trim(), workingDirectory);
    }

    /// <summary>
    /// Turns a path git printed (forward slashes on every platform, possibly relative to the
    /// folder it ran in) into an absolute path with the platform's separators and no trailing one.
    /// </summary>
    private static string NormalizePath(string reported, string baseFolder) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(reported, baseFolder));

    private static GitCommand ReadCommand(string folder, params string[] arguments) =>
        new(arguments) { WorkingDirectory = folder, Environment = ReadEnvironment };

    /// <summary>A call that only reads, run in the working tree.</summary>
    private GitCommand Read(params string[] arguments) => ReadCommand(WorkingDirectory, arguments);

    /// <summary>Runs a call that only reads and returns its standard output; a failure throws <see cref="GitException"/>.</summary>
    private async Task<string> ReadOutputAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var command = Read(arguments);
        var result = await _runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
        return result.EnsureSuccess(command).StandardOutput;
    }

    /// <summary>The non-empty lines of git's output, without line endings.</summary>
    private static List<string> SplitLines(string output) =>
        output.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0)
            .ToList();
}
