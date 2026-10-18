using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// What the tests of git's own settings share (docs/phase-6-design.md 8.13): the git version, and
/// <see cref="ConfigLab"/>, a session git environment of a test's own, as 8.12 has the harnesses'.
/// </summary>
internal static class GitConfigTestSupport
{
    private static readonly Lazy<Task<GitVersion>> Version = new(async () =>
        GitVersion.TryParse((await NewRunner().RunAsync(new GitCommand("--version"), CancellationToken.None)).StandardOutput, out var version)
            ? version
            : throw new InvalidOperationException("git --version printed no version."));

    /// <summary>The version of the git the tests run.</summary>
    public static Task<GitVersion> GitVersionAsync() => Version.Value;

    /// <summary>Whether the git the tests run is 2.36, the report's git, whose lines a test may assert word for word.</summary>
    public static async Task<bool> GitIs236Async() => await GitVersionAsync() is { Major: 2, Minor: 36 };

    /// <summary>Git's own path for a file or folder in a configuration file: forward slashes, as git writes and matches them.</summary>
    public static string GitPath(string path) => path.Replace('\\', '/');

    /// <summary>The git folder of <paramref name="repo"/> as git reports it (<c>git rev-parse --absolute-git-dir</c>), which an <c>includeIf "gitdir:"</c> matches.</summary>
    public static async Task<string> GitDirectoryAsync(TempRepo repo) =>
        (await repo.GitAsync("rev-parse", "--absolute-git-dir")).StandardOutput.Trim();
}

/// <summary>
/// A test's own git session (8.12): a global file and an XDG folder of its own, a data folder, and
/// the identity variables removed, applied to every call by <see cref="Runner"/>, as the session's
/// runner applies the session's git environment; <see cref="Configuration"/> over it. The test
/// process's own global file (<see cref="GitIsolation"/>'s) is never written.
/// </summary>
internal sealed class ConfigLab : IDisposable
{
    private readonly TempDirectory _folder = new("cfg");

    /// <param name="globalFile">The session's global file, in the lab's folder; null for <c>global.gitconfig</c>, created empty. A given one is not created.</param>
    /// <param name="extraEnvironment">More of the session's variables (<c>GIT_CONFIG_COUNT</c> and its pairs).</param>
    public ConfigLab(string? globalFile = null, IReadOnlyDictionary<string, string?>? extraEnvironment = null)
    {
        GlobalFile = _folder.Combine(globalFile ?? "global.gitconfig");
        if (globalFile is null)
        {
            File.WriteAllText(GlobalFile, string.Empty);
        }

        XdgHome = _folder.Combine("xdg");
        Directory.CreateDirectory(XdgHome);
        DataDirectory = _folder.Combine("data");
        MergeToolsFile = Path.Combine(DataDirectory, "merge-tools.json");
        Environment = new Dictionary<string, string?>
        {
            [GitConfigLocations.GlobalVariable] = GlobalFile,
            [GitConfigLocations.XdgVariable] = XdgHome,
            ["GIT_AUTHOR_NAME"] = null,
            ["GIT_AUTHOR_EMAIL"] = null,
            ["GIT_COMMITTER_NAME"] = null,
            ["GIT_COMMITTER_EMAIL"] = null,
            ["EMAIL"] = null,
        };

        var environment = (Dictionary<string, string?>)Environment;
        foreach (var (name, value) in extraEnvironment ?? new Dictionary<string, string?>())
        {
            environment[name] = value;
        }

        Runner = new SessionRunner(NewRunner(Calls), Environment);
        Configuration = new GitConfiguration(Runner, DataDirectory, MergeToolsFile, Environment, Log);
    }

    /// <summary>The lab's folder, which holds everything else.</summary>
    public string Folder => _folder.Path;

    /// <summary>The session's global file (<c>GIT_CONFIG_GLOBAL</c>), created empty.</summary>
    public string GlobalFile { get; }

    /// <summary>The session's <c>XDG_CONFIG_HOME</c>, created empty.</summary>
    public string XdgHome { get; }

    /// <summary>The app's data folder (not created: the configuration makes it).</summary>
    public string DataDirectory { get; }

    /// <summary>The kept list of merge tools.</summary>
    public string MergeToolsFile { get; }

    /// <summary>The session's git environment.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; }

    /// <summary>Every call the lab's runner made.</summary>
    public GitCallLog Calls { get; } = new();

    public ListLog Log { get; } = new();

    /// <summary>The session's runner: the test process's, with <see cref="Environment"/> applied to every call.</summary>
    public IGitRunner Runner { get; }

    public GitConfiguration Configuration { get; }

    /// <summary>A file in the lab's folder.</summary>
    public string Combine(params string[] parts) => _folder.Combine(parts);

    /// <summary>Replaces the global file's text.</summary>
    public void WriteGlobal(string text) => File.WriteAllText(GlobalFile, text);

    /// <summary><paramref name="repo"/> opened over the lab's runner, so its calls read the lab's global file.</summary>
    public Task<GitRepository> OpenAsync(TempRepo repo) => GitRepository.OpenAsync(Runner, repo.Path, TestCancelled);

    public void Dispose()
    {
        Configuration.Dispose();
        _folder.Dispose();
    }
}

/// <summary>
/// A runner that applies a session's git environment to every call, after the runner's own
/// defaults and before the command's own variables, as the session's <see cref="GitRunner"/> does
/// (docs/phase-6-design.md 8.12); a null value removes a variable.
/// </summary>
internal sealed class SessionRunner(IGitRunner inner, IReadOnlyDictionary<string, string?> environment) : IGitRunner
{
    public string ExecutablePath => inner.ExecutablePath;

    public Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var merged = new Dictionary<string, string?>(environment);
        foreach (var (name, value) in command.Environment)
        {
            merged[name] = value;
        }

        var copy = new GitCommand([.. command.Arguments])
        {
            WorkingDirectory = command.WorkingDirectory,
            StandardInput = command.StandardInput,
            RecordsStandardInput = command.RecordsStandardInput,
            BelowNormalPriority = command.BelowNormalPriority,
            Encoding = command.Encoding,
            Environment = merged,
            OnOutputLine = command.OnOutputLine,
            OnErrorLine = command.OnErrorLine,
        };
        return inner.RunAsync(copy, cancellationToken);
    }
}

/// <summary>A runner that keeps every command it is given, then runs it through another.</summary>
internal sealed class CapturingRunner(IGitRunner inner, List<GitCommand> commands) : IGitRunner
{
    public string ExecutablePath => inner.ExecutablePath;

    public Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default)
    {
        lock (commands)
        {
            commands.Add(command);
        }

        return inner.RunAsync(command, cancellationToken);
    }
}
