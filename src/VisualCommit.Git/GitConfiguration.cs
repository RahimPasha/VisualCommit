using System.Text.Json;
using System.Text.Json.Serialization;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>
/// Git's own settings as the settings screen reads and writes them (D190, docs/phase-6-design.md
/// section 8), over the session's runner: the reads of 8.1, the global writes of 8.3, the identity
/// of 8.6 and the merge tools' lookup of 8.10 with its kept list (<c>merge-tools.json</c>, D202).
/// One instance per app session (<c>TabServices.GitSettings</c>); disposing it stops the lookup,
/// which the session does when the app closes.
/// <para>
/// The outside read, the global writes and the lookup run in the data folder with
/// <c>GIT_CEILING_DIRECTORIES</c> set to its parent, so git looks for no repository above it
/// (a home folder kept in git would add entries or fail with "dubious ownership"), and no
/// repository's <c>includeIf</c> applies there (0.3). The session's git environment (8.12) is the
/// runner's to apply to every call; this class is given it too, to work out the files git uses
/// from it over the process's own variables.
/// </para>
/// </summary>
public sealed class GitConfiguration : IGitConfiguration, IDisposable
{
    /// <summary>The environment of every call that only reads (D47).</summary>
    private static readonly IReadOnlyDictionary<string, string?> ReadEnvironment = new Dictionary<string, string?>
    {
        ["GIT_OPTIONAL_LOCKS"] = "0",
    };

    private static readonly StringComparison VariableComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly Func<CancellationToken, Task<IGitRunner>> _runner;
    private readonly string _dataDirectory;
    private readonly string _mergeToolsFile;
    private readonly IReadOnlyDictionary<string, string?> _sessionEnvironment;
    private readonly IAppLog _log;
    private readonly IReadOnlyDictionary<string, string?> _outsideReadEnvironment;
    private readonly IReadOnlyDictionary<string, string?> _outsideWriteEnvironment;

    /// <summary>Two saves never interleave: one global save at a time across the session (8.3).</summary>
    private readonly SemaphoreSlim _globalWrites = new(1, 1);

    private readonly CancellationTokenSource _lifetime = new();
    private readonly Lock _gate = new();
    private ToolLookup? _toolLookup;
    private bool _disposed;
    private volatile string? _globalFile;
    private volatile string? _xdgFile;

    /// <param name="runner">The session's git runner, waited for when a call needs it (the app's <c>GitAccess.GetRunnerAsync</c>).</param>
    /// <param name="dataDirectory">The app's data folder, where the calls that belong to no repository run.</param>
    /// <param name="mergeToolsFile">The kept list of merge tools (<c>AppPaths.MergeToolsFile</c>, 8.10).</param>
    /// <param name="sessionEnvironment">The session's git environment (8.12), which the runner applies; null for none, as the desktop app runs.</param>
    /// <param name="log">The app's log.</param>
    /// <summary>
    /// Whether <c>git mergetool --tool-help</c> runs below normal priority (8.10): true for the app.
    /// The live test of the lookup sets it false: it proves the parse and the kept list with real git,
    /// and below normal priority on a machine whose other test projects keep every processor busy
    /// starved the lookup past the hang watchdog's limit; the priority is proven by
    /// <c>GitRunnerPriorityTests</c> and the command's flag by the held runner's test.
    /// </summary>
    internal bool ToolHelpBelowNormal { get; init; } = true;

    public GitConfiguration(
        Func<CancellationToken, Task<IGitRunner>> runner,
        string dataDirectory,
        string mergeToolsFile,
        IReadOnlyDictionary<string, string?>? sessionEnvironment = null,
        IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(mergeToolsFile);
        _runner = runner;
        _dataDirectory = Path.GetFullPath(dataDirectory);
        _mergeToolsFile = mergeToolsFile;
        _sessionEnvironment = sessionEnvironment ?? new Dictionary<string, string?>();
        _log = log ?? NullAppLog.Instance;

        var ceiling = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(_dataDirectory)) ?? _dataDirectory;
        _outsideWriteEnvironment = new Dictionary<string, string?> { ["GIT_CEILING_DIRECTORIES"] = ceiling };
        _outsideReadEnvironment = new Dictionary<string, string?>(_outsideWriteEnvironment) { ["GIT_OPTIONAL_LOCKS"] = "0" };
    }

    /// <summary>A configuration over a runner that is already there (tests, tools).</summary>
    public GitConfiguration(
        IGitRunner runner,
        string dataDirectory,
        string mergeToolsFile,
        IReadOnlyDictionary<string, string?>? sessionEnvironment = null,
        IAppLog? log = null)
        : this(RunnerOf(runner), dataDirectory, mergeToolsFile, sessionEnvironment, log)
    {
    }

    /// <summary>
    /// The file <c>git config --global</c> writes (8.2), by <see cref="GitConfigLocations.GlobalFile"/>
    /// over the environment git gets, as a full path; empty when there is no home folder, or
    /// <c>GIT_CONFIG_GLOBAL</c> is set but empty (git then reads no global file and refuses a global
    /// write). Worked out again by every outside read, after its git call, off the UI thread, so the
    /// screen, which reads it after the outside read, touches no disk (a home share that does not
    /// answer can hold a file check for minutes, 0.9); before the first such read, the first use
    /// works it out. A global save never changes it: git writes the very file the rule names (and
    /// creates <c>~/.gitconfig</c> only when the rule already names it), or none.
    /// </summary>
    public string GlobalFile => _globalFile ??= WorkOutGlobalFile();

    /// <summary>
    /// The XDG file git reads before the global file when <c>GIT_CONFIG_GLOBAL</c> is unset
    /// (<see cref="GitConfigLocations.XdgConfigFile"/>), as a full path, for 8.2's naming
    /// (<c>git/config under XDG_CONFIG_HOME</c>, <c>.config/git/config</c> in the home folder); empty
    /// without a home folder. Worked out as <see cref="GlobalFile"/> is.
    /// </summary>
    public string XdgFile => _xdgFile ??= WorkOutXdgFile();

    /// <summary>
    /// A variable of the environment git gets: the session's (8.12) over the process's; null when
    /// unset or removed. What 8.6's notices read (<c>GIT_AUTHOR_NAME</c>, <c>EMAIL</c>).
    /// </summary>
    public string? Variable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        foreach (var (key, value) in _sessionEnvironment)
        {
            if (string.Equals(key, name, VariableComparison))
            {
                return value;
            }
        }

        return Environment.GetEnvironmentVariable(name);
    }

    /// <summary>
    /// One <c>git config --show-scope --show-origin -z --list</c> (8.1): in
    /// <paramref name="workingTree"/>, where the repository's <c>includeIf</c> applies as for its
    /// commits and pulls; or for null the outside read, in the data folder under its ceiling, whose
    /// <c>local</c> and <c>worktree</c> entries are dropped all the same. A read
    /// (<c>GIT_OPTIONAL_LOCKS=0</c>). Git's failure (a broken file: <c>fatal: bad config line</c>,
    /// exit 128) throws <see cref="GitException"/>.
    /// </summary>
    public async Task<GitConfigRead> ReadAsync(string? workingTree, CancellationToken cancellationToken = default)
    {
        var runner = await _runner(cancellationToken).ConfigureAwait(false);
        var outside = workingTree is null;
        var folder = outside ? PrepareDataDirectory() : Path.GetFullPath(workingTree!);
        var command = new GitCommand("config", "--show-scope", "--show-origin", "-z", "--list")
        {
            WorkingDirectory = folder,
            Environment = outside ? _outsideReadEnvironment : ReadEnvironment,
        };

        var result = (await runner.RunAsync(command, cancellationToken).ConfigureAwait(false)).EnsureSuccess(command);
        IReadOnlyList<GitConfigValue> values;
        try
        {
            values = ParseList(result.StandardOutput, folder);
        }
        catch (FormatException ex)
        {
            throw new GitException(command.DisplayText, result.ExitCode, $"Unexpected output: {ex.Message}");
        }

        if (outside)
        {
            values = [.. values.Where(value => value.Scope is not (GitConfigScope.Local or GitConfigScope.Worktree))];
            _globalFile = WorkOutGlobalFile();
            _xdgFile = WorkOutXdgFile();
        }

        return new GitConfigRead(values, InRepository: !outside);
    }

    /// <summary>
    /// <c>git var GIT_AUTHOR_IDENT</c> and <c>git var GIT_COMMITTER_IDENT</c> in
    /// <paramref name="workingTree"/>, side by side (8.6): each <c>Name &lt;email&gt;</c> without
    /// git's time and zone, or null when git refused, with git's last <c>fatal:</c> line (the
    /// author's first) as <see cref="GitIdentityRead.Refusal"/>, as git wrote it.
    /// </summary>
    public async Task<GitIdentityRead> ReadIdentityAsync(string workingTree, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingTree);
        var runner = await _runner(cancellationToken).ConfigureAwait(false);
        var folder = Path.GetFullPath(workingTree);
        GitCommand Var(string variable) => new("var", variable) { WorkingDirectory = folder, Environment = ReadEnvironment };
        var author = runner.RunAsync(Var("GIT_AUTHOR_IDENT"), cancellationToken);
        var committer = runner.RunAsync(Var("GIT_COMMITTER_IDENT"), cancellationToken);
        await Task.WhenAll(author, committer).ConfigureAwait(false);
        return ParseIdentity(await author.ConfigureAwait(false), await committer.ConfigureAwait(false));
    }

    /// <summary>
    /// The global writes of a Save (8.3), in order, one <c>git config --global --replace-all</c> or
    /// <c>--unset-all</c> each (exit 5 of an unset counting as done: the key was absent), in the data
    /// folder under its ceiling, one save at a time across the session. The first failure stops the
    /// list and throws <see cref="GitException"/> with git's output (a held lock: <c>could not lock
    /// config file</c>, exit 255; 8.5). <paramref name="cancellationToken"/> stops the list before a
    /// write, never during one, which would leave git's lock file behind. An empty list returns at
    /// once, without waiting for git or the other saves: nothing of it runs on the caller's thread,
    /// the UI thread for the settings screen's Save.
    /// </summary>
    public async Task WriteGlobalAsync(IReadOnlyList<GitConfigWrite> writes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writes);
        if (writes.Count == 0)
        {
            return;
        }

        var runner = await _runner(cancellationToken).ConfigureAwait(false);
        await _globalWrites.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var folder = PrepareDataDirectory();
            foreach (var write in writes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var command = GitConfigWrites.Command("--global", write, folder, _outsideWriteEnvironment);
                var result = await runner.RunAsync(command, CancellationToken.None).ConfigureAwait(false);
                GitConfigWrites.EnsureWritten(command, result, write, _log);
            }
        }
        finally
        {
            // No file checks here: a save cancelled before its first write would make them on the
            // caller's thread, and a write never changes the files GlobalFile and XdgFile name.
            _globalWrites.Release();
        }
    }

    /// <summary>
    /// The merge tools git knows (8.10, D202): <c>git mergetool --tool-help</c>, started by the first
    /// call and run once per session in the background, in the data folder under its ceiling, at
    /// below-normal priority on Windows, until it ends or the session is disposed; no time limit.
    /// While it runs, a call is answered at once with the kept list (<c>merge-tools.json</c>) when that
    /// list is this session's git's (its path and <c>git --version</c>'s text); without one it waits
    /// for the run. Once the run has ended, every call gets its result, which a run that did not fail
    /// also keeps in the file, written whole and swapped in. A run that fails (exit not 0, git not
    /// started) is logged, leaves the file as it is, and answers with the kept list, or with
    /// <see cref="MergeToolHelp.FailedLookup"/> when there is none. <paramref name="cancellationToken"/>
    /// stops only this call's wait.
    /// </summary>
    public Task<MergeToolHelp> ReadToolHelpAsync(CancellationToken cancellationToken = default)
    {
        ToolLookup lookup;
        lock (_gate)
        {
            if (_disposed)
            {
                return Task.FromResult(MergeToolHelp.FailedLookup);
            }

            _toolLookup ??= StartToolLookup();
            lookup = _toolLookup;
        }

        return AnswerAsync(lookup, cancellationToken);
    }

    /// <summary>Stops the merge tools' lookup (the app closes). Reads and writes keep working.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        // The source is cancelled, not disposed: the lookup's calls may still hold its token.
        _lifetime.Cancel();
    }

    /// <summary>
    /// Reads <c>git config --show-scope --show-origin -z --list</c>'s output (8.1, 0.3): per entry
    /// <c>&lt;scope&gt;\0&lt;origin&gt;\0&lt;key&gt;\n&lt;value&gt;\0</c>, or <c>&lt;key&gt;\0</c> for a key
    /// written without a value (null). The origin's <c>file:</c> is removed and a relative path
    /// (the main worktree's <c>.git/config</c>) resolved against <paramref name="folder"/>, the
    /// folder the call ran in, every path made full; <c>command line:</c> is a null origin. Fields
    /// are never split further: an empty value is an entry, and a value keeps its line breaks.
    /// An entry of a scope this app does not know is skipped.
    /// </summary>
    /// <exception cref="FormatException">The output is not three fields per entry.</exception>
    internal static IReadOnlyList<GitConfigValue> ParseList(string output, string folder)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(folder);
        if (output.Length == 0)
        {
            return [];
        }

        var fields = output.Split('\0');

        // The last entry ends with a NUL too, which leaves one empty field after it.
        var count = output.EndsWith('\0') ? fields.Length - 1 : fields.Length;
        if (count % 3 != 0)
        {
            throw new FormatException($"{count} fields, not three per entry.");
        }

        var values = new List<GitConfigValue>(count / 3);
        for (var i = 0; i < count; i += 3)
        {
            if (ScopeOf(fields[i]) is not { } scope)
            {
                continue;
            }

            var keyAndValue = fields[i + 2];
            var lineFeed = keyAndValue.IndexOf('\n', StringComparison.Ordinal);
            var key = lineFeed < 0 ? keyAndValue : keyAndValue[..lineFeed];
            var value = lineFeed < 0 ? null : keyAndValue[(lineFeed + 1)..];
            values.Add(new GitConfigValue(scope, OriginOf(fields[i + 1], folder), key, value));
        }

        return values;
    }

    /// <summary>
    /// The identity of 8.6 from <c>git var</c>'s two results: each <c>Name &lt;email&gt;</c> (git's
    /// time and zone after the address dropped), or null when git refused; the refusal is git's
    /// last <c>fatal:</c> line (the author's when both refused), else its last line.
    /// </summary>
    internal static GitIdentityRead ParseIdentity(GitResult author, GitResult committer)
    {
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(committer);
        return new GitIdentityRead(IdentOf(author), IdentOf(committer), RefusalOf(author) ?? RefusalOf(committer));
    }

    private static string? IdentOf(GitResult result)
    {
        if (!result.Succeeded)
        {
            return null;
        }

        var line = result.StandardOutput.Split('\n')[0].TrimEnd('\r');
        var end = line.LastIndexOf('>');
        return end < 0 ? line.Trim() : line[..(end + 1)];
    }

    private static string? RefusalOf(GitResult result)
    {
        if (result.Succeeded)
        {
            return null;
        }

        var lines = result.StandardError.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Trim().Length > 0).ToList();
        return lines.LastOrDefault(line => line.StartsWith("fatal: ", StringComparison.Ordinal))
            ?? lines.LastOrDefault()
            ?? $"git var exited with {result.ExitCode}.";
    }

    private static GitConfigScope? ScopeOf(string scope) => scope switch
    {
        "system" => GitConfigScope.System,
        "global" => GitConfigScope.Global,
        "local" => GitConfigScope.Local,
        "worktree" => GitConfigScope.Worktree,
        "command" => GitConfigScope.Command,
        _ => null,
    };

    private static string? OriginOf(string origin, string folder)
    {
        const string File = "file:";
        if (!origin.StartsWith(File, StringComparison.Ordinal))
        {
            // "command line:" (-c, GIT_CONFIG_PARAMETERS, GIT_CONFIG_COUNT), and anything else that is no file.
            return null;
        }

        var path = origin[File.Length..];
        try
        {
            return Path.GetFullPath(path, folder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private static Func<CancellationToken, Task<IGitRunner>> RunnerOf(IGitRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        return _ => Task.FromResult(runner);
    }

    private static string FullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private string WorkOutGlobalFile() =>
        GitConfigLocations.GlobalFile(Variable, File.Exists, Directory.Exists, OperatingSystem.IsWindows()) is { } file
            ? FullPath(file)
            : string.Empty;

    private string WorkOutXdgFile() =>
        GitConfigLocations.XdgConfigFile(Variable, Directory.Exists, OperatingSystem.IsWindows()) is { } file
            ? FullPath(file)
            : string.Empty;

    private string PrepareDataDirectory()
    {
        Directory.CreateDirectory(_dataDirectory);
        return _dataDirectory;
    }

    private ToolLookup StartToolLookup()
    {
        var token = _lifetime.Token;
        var git = Task.Run(() => IdentifyGitAsync(token), CancellationToken.None);
        var kept = Task.Run(async () => ReadKept(await git.ConfigureAwait(false)), CancellationToken.None);
        var run = Task.Run(async () => await RunToolHelpAsync(await git.ConfigureAwait(false), kept, token).ConfigureAwait(false), CancellationToken.None);
        return new ToolLookup(kept, run);
    }

    private static async Task<MergeToolHelp> AnswerAsync(ToolLookup lookup, CancellationToken cancellationToken)
    {
        if (!lookup.Run.IsCompleted && await lookup.Kept.WaitAsync(cancellationToken).ConfigureAwait(false) is { } kept && !lookup.Run.IsCompleted)
        {
            return kept;
        }

        return await lookup.Run.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The runner, its git's path and <c>git --version</c>'s text; null when git cannot be used.</summary>
    private async Task<KnownGit?> IdentifyGitAsync(CancellationToken cancellationToken)
    {
        try
        {
            var runner = await _runner(cancellationToken).ConfigureAwait(false);
            var command = new GitCommand("--version") { WorkingDirectory = PrepareDataDirectory(), Environment = _outsideReadEnvironment };
            var result = await runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                _log.Warning($"The merge tools could not be listed: git --version failed with exit code {result.ExitCode}.");
                return null;
            }

            return new KnownGit(runner, runner.ExecutablePath, result.StandardOutput.Trim());
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            // GitAccess's GitUnavailableException, git that did not start, a data folder that cannot be made.
            _log.Warning("The merge tools could not be listed: git could not be used.", ex);
            return null;
        }
    }

    private async Task<MergeToolHelp> RunToolHelpAsync(KnownGit? git, Task<MergeToolHelp?> kept, CancellationToken cancellationToken)
    {
        if (git is null)
        {
            return MergeToolHelp.FailedLookup;
        }

        var command = new GitCommand("mergetool", "--tool-help")
        {
            WorkingDirectory = _dataDirectory,
            Environment = _outsideReadEnvironment,
            BelowNormalPriority = ToolHelpBelowNormal,
        };

        try
        {
            var result = await git.Runner.RunAsync(command, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                _log.Warning($"{command.DisplayText} failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
                return await kept.ConfigureAwait(false) ?? MergeToolHelp.FailedLookup;
            }

            var help = GitMergeTools.ParseToolHelp(result.StandardOutput);

            // The kept list is read first: replacing the file while it is read would fail on Windows.
            await kept.ConfigureAwait(false);
            WriteKept(git, help);
            return help;
        }
        catch (OperationCanceledException)
        {
            // The session ended.
            return await kept.ConfigureAwait(false) ?? MergeToolHelp.FailedLookup;
        }
        catch (GitException ex)
        {
            _log.Warning($"{command.DisplayText} could not be run.", ex);
            return await kept.ConfigureAwait(false) ?? MergeToolHelp.FailedLookup;
        }
    }

    /// <summary>The kept list when it is <paramref name="git"/>'s; null when it is missing, unreadable or another git's (logged).</summary>
    private MergeToolHelp? ReadKept(KnownGit? git)
    {
        if (git is null)
        {
            return null;
        }

        KeptMergeTools? kept;
        try
        {
            if (!File.Exists(_mergeToolsFile))
            {
                _log.Debug($"No merge tools are kept in {_mergeToolsFile} yet.");
                return null;
            }

            kept = JsonSerializer.Deserialize(File.ReadAllText(_mergeToolsFile), MergeToolsJsonContext.Default.KeptMergeTools);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            _log.Warning($"The kept merge tools in {_mergeToolsFile} could not be read; git is asked again.", ex);
            return null;
        }

        if (kept is not { Git: not null, Version: not null, Found: not null, NotFound: not null } || kept.Found.Concat(kept.NotFound).Any(tool => tool is not { Name.Length: > 0 }))
        {
            _log.Warning($"The kept merge tools in {_mergeToolsFile} are incomplete; git is asked again.");
            return null;
        }

        var sameGit = string.Equals(kept.Git, git.Path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            && string.Equals(kept.Version, git.Version, StringComparison.Ordinal);
        if (!sameGit)
        {
            _log.Info($"The kept merge tools are {kept.Git}'s ({kept.Version}), not {git.Path}'s ({git.Version}); git is asked again.");
            return null;
        }

        return new MergeToolHelp([.. kept.Found], [.. kept.NotFound], Failed: false);
    }

    /// <summary>Keeps <paramref name="help"/> as <paramref name="git"/>'s, written beside the file, flushed and swapped in, as settings.json is (D46, <see cref="DurableFile"/>).</summary>
    private void WriteKept(KnownGit git, MergeToolHelp help)
    {
        try
        {
            if (Path.GetDirectoryName(_mergeToolsFile) is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
            }

            var kept = new KeptMergeTools(git.Path, git.Version, help.Found, help.NotFound);
            DurableFile.Replace(_mergeToolsFile, JsonSerializer.Serialize(kept, MergeToolsJsonContext.Default.KeptMergeTools));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning($"The merge tools could not be kept in {_mergeToolsFile}.", ex);
        }
    }

    /// <summary>The session's lookup: the kept list as read (null: none of this git's) and the run.</summary>
    private sealed record ToolLookup(Task<MergeToolHelp?> Kept, Task<MergeToolHelp> Run);

    /// <summary>The git the lookup runs, as the kept list names it.</summary>
    private sealed record KnownGit(IGitRunner Runner, string Path, string Version);
}

/// <summary>
/// The writes of the settings screen's Save (docs/phase-6-design.md 8.3, 8.5), global and
/// <c>--local</c> alike: only changed keys are written, never <c>--worktree</c>, <c>--system</c>
/// or <c>--file</c>, so git chooses the file as in a terminal.
/// </summary>
internal static class GitConfigWrites
{
    /// <summary>Git's exit code for <c>--unset-all</c> of a key that is not set: the write is done.</summary>
    public const int Absent = 5;

    /// <summary>
    /// <c>git config &lt;scope&gt; --replace-all &lt;key&gt; &lt;value&gt;</c> for a value (absent, single
    /// and multi-valued keys alike; a value starting with <c>-</c> stays a value, since it follows the
    /// key), or <c>git config &lt;scope&gt; --unset-all &lt;key&gt;</c> for null.
    /// </summary>
    public static GitCommand Command(string scope, GitConfigWrite write, string folder, IReadOnlyDictionary<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentException.ThrowIfNullOrWhiteSpace(write.Key, nameof(write));
        string[] arguments = write.Value is { } value
            ? ["config", scope, "--replace-all", write.Key, value]
            : ["config", scope, "--unset-all", write.Key];
        return environment is null
            ? new GitCommand(arguments) { WorkingDirectory = folder }
            : new GitCommand(arguments) { WorkingDirectory = folder, Environment = environment };
    }

    /// <summary>
    /// Returns when the write is done (exit 0; exit 5 of an unset), otherwise throws
    /// <see cref="GitException"/> with git's output (8.5). Exit 1 or 2 (an invalid key) is the app's
    /// bug, which is logged too.
    /// </summary>
    public static void EnsureWritten(GitCommand command, GitResult result, GitConfigWrite write, IAppLog log)
    {
        if (result.Succeeded || (write.Value is null && result.ExitCode == Absent))
        {
            return;
        }

        if (result.ExitCode is 1 or 2)
        {
            log.Warning($"{command.DisplayText} was refused as invalid (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }

        result.EnsureSuccess(command);
    }
}

/// <summary>The kept list of merge tools, <c>merge-tools.json</c> (docs/phase-6-design.md 8.10, D202).</summary>
/// <param name="Git">The git executable's path that listed them.</param>
/// <param name="Version"><c>git --version</c>'s text of that git.</param>
/// <param name="Found">The tools it found.</param>
/// <param name="NotFound">The tools it knows but did not find.</param>
internal sealed record KeptMergeTools(string Git, string Version, IReadOnlyList<MergeTool> Found, IReadOnlyList<MergeTool> NotFound);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(KeptMergeTools))]
internal sealed partial class MergeToolsJsonContext : JsonSerializerContext;
