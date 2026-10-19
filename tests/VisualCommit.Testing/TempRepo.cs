using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using VisualCommit.Core.Git;
using VisualCommit.Git;

namespace VisualCommit.Testing;

/// <summary>
/// Builds a git repository in a temporary folder for a test, with real git.
/// <para>
/// The repo is cut off from the machine's git configuration, and every commit gets a fixed
/// author and a time from a clock that starts at 2026-01-01 12:00 UTC and advances one minute per
/// commit. The same build steps therefore give the same commit SHAs on every machine and every
/// run, so tests and screenshots can rely on them.
/// </para>
/// </summary>
public sealed class TempRepo : IDisposable
{
    public const string AuthorName = "Test Author";
    public const string AuthorEmail = "author@example.com";

    private static readonly Lazy<string> GitExecutable = new(() =>
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    private readonly TempDirectory _root;

    /// <summary>Whether disposing this repo deletes <see cref="_root"/>: false for a clone made by <see cref="CloneSiblingAsync"/>, which lives in its parent's folder.</summary>
    private readonly bool _ownsRoot;

    private Clock _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private TempRepo(TempDirectory root, string path, bool ownsRoot = true)
    {
        _root = root;
        _ownsRoot = ownsRoot;
        Path = path;
        Calls = new GitCallLog();
        Runner = new GitRunner(GitExecutable.Value, Calls);

        var environment = new Dictionary<string, string?>(IsolatedEnvironment(root.Combine("home")))
        {
            ["GIT_AUTHOR_NAME"] = AuthorName,
            ["GIT_AUTHOR_EMAIL"] = AuthorEmail,
            ["GIT_COMMITTER_NAME"] = AuthorName,
            ["GIT_COMMITTER_EMAIL"] = AuthorEmail,
        };

        Environment = environment;
    }

    /// <summary>The git executable the tests run, found as the app finds it.</summary>
    internal static string GitPath => GitExecutable.Value;

    /// <summary>
    /// The environment variables that cut a git call off from the machine, with
    /// <paramref name="home"/> (created, with an empty <c>.gitconfig</c>, when missing) as its
    /// home folder: no system or user configuration, no search for a repository above the tests'
    /// own folder, none of the variables through which a surrounding process hands git extra
    /// configuration or another repository, <see cref="GitIsolation.MachineIndependence"/>
    /// (no askpass program, ssh command, editor or proxy of the machine, D112), and
    /// <c>GIT_EDITOR</c> and <c>GIT_SEQUENCE_EDITOR</c> set to <c>:</c>, so that no step waits
    /// for an editor (docs/phase-4-design.md 0.5). <see cref="Environment"/> is this plus the author.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> IsolatedEnvironment(string home)
    {
        Directory.CreateDirectory(home);
        var globalConfig = System.IO.Path.Combine(home, ".gitconfig");
        if (!File.Exists(globalConfig))
        {
            File.WriteAllText(globalConfig, string.Empty);
        }

        var environment = new Dictionary<string, string?>
        {
            // No system or user configuration: no aliases, hooks, signing or line-ending
            // settings from the machine. GIT_CONFIG_GLOBAL needs Git 2.32; HOME covers 2.30 and 2.31.
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_GLOBAL"] = globalConfig,
            ["HOME"] = home,
            ["XDG_CONFIG_HOME"] = System.IO.Path.Combine(home, ".config"),

            // No search for a repository above the tests' own folder.
            [GitIsolation.CeilingVariable] = GitIsolation.CeilingDirectory,
        };

        // Nor anything the surrounding process passes to git through its environment: extra
        // configuration, or another repository to work on. A null value removes the variable
        // for the call.
        foreach (var variable in GitIsolation.InheritedVariables)
        {
            environment[variable] = null;
        }

        // Nor an askpass program, an ssh command, an editor or a proxy of the machine (D112).
        foreach (var (variable, value) in GitIsolation.MachineIndependence)
        {
            environment[variable] = value;
        }

        // A step that wants an editor (git rebase --continue, a merge's message) goes on without
        // one, as the app's own calls do (D102, docs/phase-4-design.md 0.5), whatever runs the call.
        environment["GIT_EDITOR"] = ":";
        environment["GIT_SEQUENCE_EDITOR"] = ":";
        return environment;
    }

    /// <summary>The working tree of the repository.</summary>
    public string Path { get; }

    /// <summary>The runner the builder uses. Tests can use it for their own git calls.</summary>
    public IGitRunner Runner { get; }

    /// <summary>Every git call made through <see cref="GitAsync"/>.</summary>
    public IGitCallLog Calls { get; }

    /// <summary>The environment variables that isolate this repo. Pass them to any other git call on it.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; }

    /// <summary>
    /// Creates an empty repository on the branch <paramref name="initialBranch"/>: a copy of the
    /// git folder that <c>git init</c> and <see cref="RepositorySettings"/> made once per test
    /// process for that branch (<see cref="BlankGitFolderAsync"/>), which is the same as running
    /// them again for each repository, without their six git processes.
    /// </summary>
    public static async Task<TempRepo> CreateAsync(string name = "repo", string initialBranch = "main")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initialBranch);
        var blank = await BlankGitFolderAsync(initialBranch);
        var root = new TempDirectory(name);
        try
        {
            var path = root.Combine(name);
            var gitFolder = System.IO.Path.Combine(path, ".git");
            CopyDirectory(blank, gitFolder);

            // Git for Windows hides the git folder it makes (core.hideDotFiles); so is the copy.
            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(gitFolder, File.GetAttributes(blank));
            }

            return new TempRepo(root, path);
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The repository's own settings that every repo of this class has. Files are stored byte
    /// for byte, so content and SHAs do not depend on the platform. Neither gc nor git's
    /// automatic maintenance runs: nothing packs, prunes or expires behind a test's back, and a
    /// commit, merge or rebase does not start a <c>git maintenance run --auto</c> process that
    /// would do nothing. The identity is that of commits the app under test makes here, which has
    /// no other: the tests leave out the user's configuration (D72); the builder's own commits set
    /// it through their environment.
    /// </summary>
    private static readonly KeyValuePair<string, string>[] RepositorySettings =
    [
        new("core.autocrlf", "false"),
        new("commit.gpgsign", "false"),
        new("gc.auto", "0"),
        new("maintenance.auto", "false"),
        new("user.name", AuthorName),
        new("user.email", AuthorEmail),
    ];

    /// <summary>
    /// The settings of every bare remote <see cref="AddBareRemoteAsync"/> makes: no gc after a
    /// push (<c>receive.autogc</c>) and no automatic maintenance, so that a push starts no process
    /// that would do nothing in a repository this small.
    /// </summary>
    private static readonly KeyValuePair<string, string>[] BareRemoteSettings =
    [
        new("receive.autogc", "false"),
        new("maintenance.auto", "false"),
    ];

    /// <summary>The git folders of the blank repositories, one per initial branch (or <see cref="BareKey"/>), made once per test process.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> BlankGitFolders = new(StringComparer.Ordinal);

    /// <summary>The key of the blank bare repository in <see cref="BlankGitFolders"/>: no branch name can be empty.</summary>
    private const string BareKey = "";

    /// <summary>
    /// The git folder of a repository that <c>git init --initial-branch <paramref name="initialBranch"/></c>
    /// made with this class's isolation, with <see cref="RepositorySettings"/> set; or, for
    /// <see cref="BareKey"/>, of a bare repository that <c>git init --bare</c> made, with
    /// <see cref="BareRemoteSettings"/> set. Made once per test process, in a folder of its own
    /// under the tests' temp folder like every other repository (git probes that file system for
    /// its <c>core</c> settings), and deleted when the process ends. A git folder holds no path of
    /// its own, so a copy of it is a repository of its own.
    /// </summary>
    internal static Task<string> BlankGitFolderAsync(string initialBranch) =>
        BlankGitFolders.GetOrAdd(initialBranch, key => new Lazy<Task<string>>(() => MakeBlankGitFolderAsync(key))).Value;

    /// <summary>The blank bare repository's folder (<see cref="BlankGitFolderAsync"/> with <see cref="BareKey"/>), for builders that copy it themselves (the workspace's <c>LeanRepositories</c>).</summary>
    internal static Task<string> BlankBareFolderAsync() => BlankGitFolderAsync(BareKey);

    private static async Task<string> MakeBlankGitFolderAsync(string initialBranch)
    {
        var root = new TempDirectory("blank");
        try
        {
            var bare = initialBranch.Length == 0;
            var path = root.Combine("blank");
            Directory.CreateDirectory(path);
            var blank = new TempRepo(root, path);
            string[] init = bare ? ["init", "--quiet", "--bare"] : ["init", "--quiet", "--initial-branch", initialBranch];
            await blank.GitAsync(init);
            foreach (var (key, value) in bare ? BareRemoteSettings : RepositorySettings)
            {
                await blank.ConfigAsync(key, value);
            }

            var gitFolder = bare ? path : System.IO.Path.Combine(path, ".git");
            RemoveHookSamples(gitFolder);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => root.Dispose();
            return gitFolder;
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Deletes the hook samples <c>git init</c> copies into a new git folder (<c>hooks/*.sample</c>:
    /// 13 on Git 2.36, 14 on 2.55 with <c>sendemail-validate.sample</c>), which no git call runs:
    /// every copy of a blank folder, and of every scenario built from one, then has that many fewer
    /// files to copy (docs/phase-6-design.md 17.1; the ten-repository workspace's copy 323 files in
    /// place of 544). The folder itself stays, so that a test that installs a hook finds it as git
    /// left it.
    /// </summary>
    private static void RemoveHookSamples(string gitFolder)
    {
        var hooks = System.IO.Path.Combine(gitFolder, "hooks");
        if (!Directory.Exists(hooks))
        {
            return;
        }

        foreach (var sample in Directory.EnumerateFiles(hooks, "*.sample"))
        {
            File.SetAttributes(sample, FileAttributes.Normal);
            File.Delete(sample);
        }
    }

    /// <summary>Runs git in the repository and returns its result. Throws when git reports an error.</summary>
    public Task<GitResult> GitAsync(params string[] arguments) => GitWithInputAsync(null, arguments);

    /// <summary>
    /// Runs git in the repository with <paramref name="input"/> on its standard input, for
    /// commands that read a stream, such as <c>fast-import</c> to build a large history quickly.
    /// Throws when git reports an error.
    /// </summary>
    public async Task<GitResult> GitWithInputAsync(string? input, params string[] arguments)
    {
        var command = Command(input, arguments);
        var result = await Runner.RunAsync(command);
        return result.EnsureSuccess(command);
    }

    /// <summary>
    /// Runs git in the repository as <see cref="GitAsync"/> does, but returns its result also
    /// when git reports an error: for a step that is meant to stop, such as a merge with a conflict.
    /// </summary>
    public Task<GitResult> GitAllowingFailureAsync(params string[] arguments) => Runner.RunAsync(Command(null, arguments));

    /// <summary>
    /// Runs git in the repository as <see cref="GitAsync"/> does, with the variables
    /// <paramref name="extra"/> added for this call only (a null value removes one), such as a
    /// <c>GIT_SEQUENCE_EDITOR</c> that writes the todo of an interactive rebase
    /// (docs/phase-4-design.md 2.5, 13.2). The call keeps the isolation and the clock's dates as
    /// its author and committer date, unless <paramref name="extra"/> names a date variable
    /// itself. Throws when git reports an error.
    /// </summary>
    public async Task<GitResult> GitWithEnvironmentAsync(IReadOnlyDictionary<string, string?> extra, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(extra);
        var command = Command(null, arguments, extra);
        var result = await Runner.RunAsync(command);
        return result.EnsureSuccess(command);
    }

    /// <summary>
    /// Runs git as <see cref="GitWithEnvironmentAsync"/> does, but returns its result also when git
    /// reports an error: for a step with its own variables that is meant to stop.
    /// </summary>
    public Task<GitResult> GitWithEnvironmentAllowingFailureAsync(IReadOnlyDictionary<string, string?> extra, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(extra);
        return Runner.RunAsync(Command(null, arguments, extra));
    }

    /// <summary>
    /// A git call in this repository, with its isolation and the clock's time as the author and
    /// committer date, then the call's own <paramref name="extra"/> variables.
    /// </summary>
    private GitCommand Command(string? input, string[] arguments, IReadOnlyDictionary<string, string?>? extra = null)
    {
        var time = _clock.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        var environment = new Dictionary<string, string?>(Environment)
        {
            ["GIT_AUTHOR_DATE"] = time,
            ["GIT_COMMITTER_DATE"] = time,
        };

        foreach (var (name, value) in extra ?? new Dictionary<string, string?>())
        {
            environment[name] = value;
        }

        return new GitCommand(arguments) { WorkingDirectory = Path, Environment = environment, StandardInput = input };
    }

    /// <summary>Sets <paramref name="key"/> to <paramref name="value"/> in the repository's own configuration.</summary>
    public Task ConfigAsync(string key, string value) => GitAsync("config", key, value);

    /// <summary>
    /// The object <paramref name="fullRef"/> (<c>refs/heads/main</c>, <c>refs/stash</c>) points
    /// at, as git stores it: for an annotated tag the tag object, not its commit. Null when the
    /// ref does not exist. Only a full ref name is looked up; nothing is guessed.
    /// </summary>
    public async Task<string?> RefTargetAsync(string fullRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullRef);

        // for-each-ref also lists the refs below the name (refs/heads/main/x); only the name itself counts.
        var output = (await GitAsync("for-each-ref", "--format=%(objectname) %(refname)", fullRef)).StandardOutput;
        return Lines(output)
            .Select(line => line.Split(' ', 2))
            .FirstOrDefault(fields => string.Equals(fields[1], fullRef, StringComparison.Ordinal))?[0];
    }

    /// <summary>
    /// "The exact state" of the repository (phase 5's report, part P): what a refused operation
    /// must leave as it was, joined into one text to be compared whole. HEAD (<c>git rev-parse
    /// HEAD</c>), every ref with its object (<c>git for-each-ref</c>), the index (<c>git ls-files
    /// --stage</c>), the status with the untracked and ignored files (<c>git status --porcelain=v2
    /// --untracked-files=all --ignored</c>) and every file of the working tree outside <c>.git</c>
    /// with its contents, in ordinal order. The scripted checks, the real-window pass and the App
    /// and Git tests all read it here.
    /// </summary>
    public async Task<string> ExactStateAsync()
    {
        var head = (await GitAsync("rev-parse", "HEAD")).StandardOutput.Trim();
        var refs = (await GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).StandardOutput;
        var index = (await GitAsync("ls-files", "--stage")).StandardOutput;
        var status = (await GitAsync("status", "--porcelain=v2", "--untracked-files=all", "--ignored")).StandardOutput;
        var files = Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories)
            .Where(file => !System.IO.Path.GetRelativePath(Path, file).StartsWith(".git" + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(file => $"{System.IO.Path.GetRelativePath(Path, file)}: {File.ReadAllText(file)}");
        return string.Join('\n', [head, refs, index, status, .. files]);
    }

    /// <summary>The backup refs the app has made (<c>refs/visualcommit/backup/...</c>), in git's order of their names.</summary>
    public async Task<IReadOnlyList<RefEntry>> BackupRefsAsync()
    {
        var output = (await GitAsync("for-each-ref", "--format=%(refname)%09%(objectname)", "refs/visualcommit/backup")).StandardOutput;
        return [.. Lines(output).Select(line =>
        {
            var fields = line.Split('\t');
            return new RefEntry(fields[0], fields[1]);
        })];
    }

    /// <summary>The stash entries, newest first, as <c>git stash list</c> shows them.</summary>
    public async Task<IReadOnlyList<StashListEntry>> StashListAsync()
    {
        var output = (await GitAsync("stash", "list", "--format=%gd%x1f%H%x1f%gs")).StandardOutput;
        return [.. Lines(output).Select(line =>
        {
            var fields = line.Split('\x1f');
            return new StashListEntry(fields[0], fields[1], fields[2]);
        })];
    }

    private static string[] Lines(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Clones <paramref name="from"/> (a folder or a URL) into the folder <paramref name="name"/>
    /// next to this repository's working tree (<see cref="SiblingPath"/>), with the same isolation
    /// and settings as a repo this class creates. The clone shares this repository's clock, so
    /// commits made in either follow each other a minute apart, and its folder is deleted with this
    /// repository's: disposing the clone deletes nothing. A copy (<see cref="CopyAsync"/>) of this
    /// repository copies the clone's folder too, but does not point the clone's remotes at the copy.
    /// </summary>
    public async Task<TempRepo> CloneSiblingAsync(string name, string from)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        var path = _root.Combine(name);
        var clone = new TempRepo(_root, path, ownsRoot: false) { _clock = _clock };

        // The settings go in with the clone (-c writes them into its configuration), before the checkout.
        string[] arguments = ["clone", "--quiet", "--no-hardlinks", .. RepositorySettings.SelectMany(setting => new[] { "-c", $"{setting.Key}={setting.Value}" }), "--", from, path];
        var command = new GitCommand(arguments) { WorkingDirectory = _root.Path, Environment = Environment };
        (await Runner.RunAsync(command)).EnsureSuccess(command);
        return clone;
    }

    /// <summary>A repository made earlier next to this one (<see cref="CloneSiblingAsync"/>), with this one's clock, as the clone was returned.</summary>
    public TempRepo OpenSibling(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var path = _root.Combine(name);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"No repository {name} next to {Path}.");
        }

        return new TempRepo(_root, path, ownsRoot: false) { _clock = _clock };
    }

    /// <summary>
    /// A repository at <paramref name="path"/> inside <paramref name="root"/>, made by other means
    /// (the workspace's <c>LeanRepositories</c>, docs/phase-6-design.md 17.1), with this class's
    /// isolation and a clock of its own that starts at <paramref name="clock"/>. It does not own
    /// the folder: disposing it deletes nothing; the folder goes with <paramref name="root"/>.
    /// With <paramref name="ownsRoot"/> it does, as a repository <see cref="CreateAsync"/> made: a
    /// lean scenario's template ("settings"), which <see cref="CopyAsync"/> then copies whole.
    /// </summary>
    internal static TempRepo Over(TempDirectory root, string path, DateTimeOffset clock, bool ownsRoot = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new TempRepo(root, path, ownsRoot) { _clock = new Clock(clock) };
    }

    /// <summary>Writes a file in the working tree, creating its folders. Line endings are written as given.</summary>
    public TempRepo WriteFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return this;
    }

    /// <summary>Writes a file in the working tree byte for byte, creating its folders: for binary files and images.</summary>
    public TempRepo WriteBytes(string relativePath, byte[] content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        return this;
    }

    public TempRepo DeleteFile(string relativePath)
    {
        File.Delete(System.IO.Path.Combine(Path, relativePath));
        return this;
    }

    /// <summary>Stages every change in the working tree and commits it. Returns the new commit's SHA.</summary>
    public async Task<string> CommitAsync(string message)
    {
        await GitAsync("add", "--all");
        var sha = CommittedSha(await GitAsync(CommitArguments("--allow-empty", "--message", message)));
        _clock.Advance();
        return sha;
    }

    /// <summary>
    /// The arguments of a <c>git commit</c> whose summary line names the new commit in full
    /// (<c>-c core.abbrev=no</c>), for <see cref="CommittedSha"/>: the commit is the same as with
    /// <c>--quiet</c>, and no <c>rev-parse HEAD</c> is needed after it. <paramref name="options"/>
    /// follow <c>commit</c>.
    /// </summary>
    public static string[] CommitArguments(params string[] options) => ["-c", "core.abbrev=no", "commit", .. options];

    /// <summary>
    /// The SHA of the commit a <c>git commit</c> made with <see cref="CommitArguments"/>, read from
    /// the first line it prints: <c>[main (root-commit) &lt;sha&gt;] subject</c>, or
    /// <c>[detached HEAD &lt;sha&gt;] subject</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The output has no such line.</exception>
    public static string CommittedSha(GitResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var line = result.StandardOutput.Split('\n', 2)[0];
        var match = Regex.Match(line, @"^\[.*? ([0-9a-f]{40}|[0-9a-f]{64})\] ");
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException($"git commit did not name its commit in full. It printed:\n{result.StandardOutput}");
    }

    /// <summary>Writes one file and commits it. Returns the new commit's SHA.</summary>
    public Task<string> CommitFileAsync(string relativePath, string content, string message) =>
        WriteFile(relativePath, content).CommitAsync(message);

    public Task CreateBranchAsync(string name, string startPoint = "HEAD") => GitAsync("branch", name, startPoint);

    public Task CheckoutAsync(string name) => GitAsync("checkout", "--quiet", name);

    /// <summary>Detaches HEAD at <paramref name="target"/>.</summary>
    public Task DetachAsync(string target = "HEAD") => GitAsync("checkout", "--quiet", "--detach", target);

    /// <summary>Creates a lightweight tag at <paramref name="target"/>.</summary>
    public Task TagAsync(string name, string target = "HEAD") => GitAsync("tag", name, target);

    /// <summary>Creates an annotated tag at <paramref name="target"/>. Its tagger date is the clock's current time.</summary>
    public Task AnnotatedTagAsync(string name, string message, string target = "HEAD") =>
        GitAsync("tag", "--annotate", "--message", message, name, target);

    /// <summary>
    /// Stashes the changes in the working tree with <paramref name="message"/> (git records it as
    /// "On &lt;branch&gt;: &lt;message&gt;"). Returns the stash commit's SHA.
    /// </summary>
    public async Task<string> StashAsync(string message)
    {
        await GitAsync("stash", "push", "--quiet", "--message", message);
        _clock.Advance();
        return (await GitAsync("rev-parse", "refs/stash")).StandardOutput.Trim();
    }

    /// <summary>
    /// Creates an empty bare repository next to this one's working tree (not inside it) and adds
    /// it as the remote <paramref name="name"/>. Returns the bare repository's folder. Push to it
    /// with <see cref="PushAsync"/>. The bare repository is a copy of one that <c>git init --bare</c>
    /// made once per test process, with <see cref="BareRemoteSettings"/> (<see cref="BlankGitFolderAsync"/>).
    /// With <paramref name="relative"/> the remote's URL is <c>../&lt;name&gt;.git</c>, which git
    /// resolves against the working tree's top (docs/phase-6-design.md 0.10), so that a copy fetches
    /// from its own remote with no URL rewritten (<see cref="CopyAsync"/>).
    /// </summary>
    public async Task<string> AddBareRemoteAsync(string name = "origin", bool relative = false)
    {
        var barePath = _root.Combine(name + ".git");
        CopyDirectory(await BlankGitFolderAsync(BareKey), barePath);
        await GitAsync("remote", "add", name, relative ? "../" + name + ".git" : barePath);
        return barePath;
    }

    /// <summary>Pushes <paramref name="refspecs"/> to <paramref name="remote"/>; with <paramref name="setUpstream"/>, each pushed branch tracks the remote branch.</summary>
    public Task PushAsync(string remote, bool setUpstream, params string[] refspecs) =>
        GitAsync([.. new[] { "push", "--quiet" }, .. setUpstream ? new[] { "--set-upstream" } : [], remote, .. refspecs]);

    /// <summary>A folder next to this repository's working tree, inside the same temporary folder, for clones and other repos a test needs.</summary>
    public string SiblingPath(string name) => _root.Combine(name);

    /// <summary>Merges <paramref name="branch"/> into the current branch with a merge commit. Returns the merge commit's SHA.</summary>
    public async Task<string> MergeAsync(string branch, string? message = null)
    {
        await GitAsync("merge", "--quiet", "--no-ff", "--message", message ?? $"Merge branch '{branch}'", branch);
        _clock.Advance();
        return await HeadAsync();
    }

    /// <summary>
    /// Makes an independent copy of this repository and everything next to it (bare remotes,
    /// clones) in a new temporary folder, with remote URLs that pointed into the old folder
    /// pointed at the copy, and linked worktrees that lay in the old folder joined to the copy
    /// (<c>git worktree repair</c>). The copy's clock carries on from this one's, separately.
    /// Copying a built scenario is much faster than building it again, because building runs git
    /// dozens of times.
    /// </summary>
    public async Task<TempRepo> CopyAsync()
    {
        var root = new TempDirectory(System.IO.Path.GetFileName(Path));
        try
        {
            CopyDirectory(_root.Path, root.Path);
            var copy = new TempRepo(root, root.Combine(System.IO.Path.GetFileName(Path))) { _clock = new Clock(_clock.Now) };

            foreach (var (remote, url) in await copy.RemoteUrlsAsync())
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !uri.IsFile)
                {
                    continue;
                }

                // A relative URL (../origin.git) is resolved by git against the working tree's
                // top, so in the copy it already names the copy's own remote. Path.GetFullPath
                // would resolve it against this process's current folder instead, and leave it
                // alone only by accident (docs/phase-6-design.md 17.1).
                if (!System.IO.Path.IsPathRooted(url))
                {
                    continue;
                }

                var full = System.IO.Path.GetFullPath(url);
                if (full.StartsWith(_root.Path, StringComparison.OrdinalIgnoreCase))
                {
                    await copy.GitAsync("remote", "set-url", remote, root.Path + full[_root.Path.Length..]);
                }
            }

            await copy.RejoinWorktreesAsync(_root.Path);
            return copy;
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Each remote with its URL as configured (the first <c>remote.&lt;name&gt;.url</c>, as
    /// <c>git remote get-url</c> gives it without an <c>insteadOf</c> rule), in the order of the
    /// configuration, read with one git call.
    /// </summary>
    private async Task<IReadOnlyList<(string Remote, string Url)>> RemoteUrlsAsync()
    {
        const string Section = "remote.", Key = ".url";
        var command = Command(null, ["config", "--get-regexp", @"^remote\..*\.url$"]);
        var listed = await Runner.RunAsync(command);
        if (listed.ExitCode == 1)
        {
            // No such key: no remote.
            return [];
        }

        var urls = new List<(string Remote, string Url)>();
        foreach (var line in Lines(listed.EnsureSuccess(command).StandardOutput))
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            if (space < 0)
            {
                continue;
            }

            var remote = line[Section.Length..(space - Key.Length)];
            if (!urls.Exists(entry => entry.Remote == remote))
            {
                urls.Add((remote, line[(space + 1)..].TrimEnd('\r')));
            }
        }

        return urls;
    }

    /// <summary>
    /// Joins the copied linked worktrees that lay inside <paramref name="oldRoot"/> to this copy.
    /// Each worktree's <c>.git</c> file still names the original repository's administrative
    /// folder, which exists, so <c>git worktree repair</c> alone would repair the original: the
    /// file is first pointed at this copy's folder, and git then fixes the way back.
    /// </summary>
    private async Task RejoinWorktreesAsync(string oldRoot)
    {
        var administrative = System.IO.Path.Combine(Path, ".git", "worktrees");
        if (!Directory.Exists(administrative))
        {
            return;
        }

        foreach (var folder in Directory.EnumerateDirectories(administrative))
        {
            var gitdirFile = System.IO.Path.Combine(folder, "gitdir");
            if (!File.Exists(gitdirFile))
            {
                continue;
            }

            // The worktree's .git file, as the original knew it. Git writes the physical path
            // (macOS's /private/var for /var), so the old folder is found by its name, which is unique.
            var oldDotGit = File.ReadAllText(gitdirFile).Trim().Replace('\\', '/');
            var rootName = "/" + System.IO.Path.GetFileName(oldRoot) + "/";
            var inRoot = oldDotGit.IndexOf(rootName, StringComparison.OrdinalIgnoreCase);
            if (inRoot < 0)
            {
                continue;
            }

            // Git for Windows makes the file hidden, which a plain write refuses.
            var relative = oldDotGit[(inRoot + rootName.Length)..];
            var worktree = System.IO.Path.GetDirectoryName(System.IO.Path.Combine(_root.Path, relative))!;
            var dotGit = System.IO.Path.Combine(worktree, ".git");
            var attributes = File.GetAttributes(dotGit);
            File.SetAttributes(dotGit, FileAttributes.Normal);
            File.WriteAllText(dotGit, "gitdir: " + folder.Replace('\\', '/') + "\n");
            File.SetAttributes(dotGit, attributes);
            await GitAsync("worktree", "repair", worktree);
        }
    }

    /// <summary>Deletes a folder and everything in it, read-only files (git's objects) included.</summary>
    internal static void DeleteDirectory(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }

    /// <summary>Copies the folder <paramref name="from"/> and everything in it to <paramref name="to"/>, which must not hold any of its files yet.</summary>
    internal static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var directory in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(System.IO.Path.Combine(to, System.IO.Path.GetRelativePath(from, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, System.IO.Path.Combine(to, System.IO.Path.GetRelativePath(from, file)));
        }
    }

    /// <summary>The SHA of the current commit.</summary>
    public async Task<string> HeadAsync() => (await GitAsync("rev-parse", "HEAD")).StandardOutput.Trim();

    /// <summary>The name of the current branch.</summary>
    public async Task<string> CurrentBranchAsync() =>
        (await GitAsync("symbolic-ref", "--short", "HEAD")).StandardOutput.Trim();

    public void Dispose()
    {
        if (_ownsRoot)
        {
            _root.Dispose();
        }
    }

    /// <summary>
    /// The time this repository's clock shows: the author, committer and ref-update date of its
    /// next git call. Shared with the repositories next to it (<see cref="CloneSiblingAsync"/>).
    /// </summary>
    public DateTimeOffset ClockTime => _clock.Now;

    /// <summary>
    /// Moves the clock forward to <paramref name="time"/>, for a step that must look newer in
    /// the reflog than an operation of the app under test, whose ref updates are dated
    /// <see cref="GitIsolation.AppCommitDate"/> (D111): git's <c>--force-if-includes</c> looks only
    /// at reflog entries newer than the remote-tracking ref's last update. The clock goes on from
    /// there a minute per commit, stash and merge, and moves for the repositories next to this
    /// one too (they share it). It never goes back: an earlier time throws.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="time"/> is before <see cref="ClockTime"/>.</exception>
    public TempRepo MoveClockTo(DateTimeOffset time)
    {
        _clock.MoveTo(time);
        return this;
    }

    /// <summary>The builder's clock, shared by a repo and the clones made next to it.</summary>
    private sealed class Clock(DateTimeOffset now)
    {
        public DateTimeOffset Now { get; private set; } = now;

        public void Advance() => Now = Now.AddMinutes(1);

        public void MoveTo(DateTimeOffset time)
        {
            if (time < Now)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(time),
                    time,
                    string.Create(CultureInfo.InvariantCulture, $"The clock only moves forward; it shows {Now:yyyy-MM-dd HH:mm:ss zzz}."));
            }

            Now = time;
        }
    }
}

/// <summary>A ref and the object it points at.</summary>
/// <param name="Name">The full ref name, such as <c>refs/visualcommit/backup/main</c>.</param>
/// <param name="Sha">The object's id.</param>
public sealed record RefEntry(string Name, string Sha);

/// <summary>One line of <c>git stash list</c>.</summary>
/// <param name="Selector">Such as <c>stash@{0}</c>.</param>
/// <param name="Sha">The stash commit's id.</param>
/// <param name="Message">As git shows it, such as <c>On main: Experiment</c>.</param>
public sealed record StashListEntry(string Selector, string Sha, string Message);
