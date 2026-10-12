using System.Globalization;
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

    /// <summary>Creates an empty repository on the branch <paramref name="initialBranch"/>.</summary>
    public static async Task<TempRepo> CreateAsync(string name = "repo", string initialBranch = "main")
    {
        var root = new TempDirectory(name);
        try
        {
            var path = root.Combine(name);
            Directory.CreateDirectory(path);
            var repo = new TempRepo(root, path);
            await repo.GitAsync("init", "--quiet", "--initial-branch", initialBranch);
            await repo.ConfigureAsync();
            return repo;
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The repository's own settings that every repo of this class has. Files are stored byte
    /// for byte, so content and SHAs do not depend on the platform. The identity is that of
    /// commits the app under test makes here, which has no other: the tests leave out the user's
    /// configuration (D72); the builder's own commits set it through their environment.
    /// </summary>
    private static readonly KeyValuePair<string, string>[] RepositorySettings =
    [
        new("core.autocrlf", "false"),
        new("commit.gpgsign", "false"),
        new("gc.auto", "0"),
        new("user.name", AuthorName),
        new("user.email", AuthorEmail),
    ];

    private async Task ConfigureAsync()
    {
        foreach (var (key, value) in RepositorySettings)
        {
            await ConfigAsync(key, value);
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

    /// <summary>A git call in this repository, with its isolation and the clock's time as the author and committer date.</summary>
    private GitCommand Command(string? input, string[] arguments)
    {
        var time = _clock.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        var environment = new Dictionary<string, string?>(Environment)
        {
            ["GIT_AUTHOR_DATE"] = time,
            ["GIT_COMMITTER_DATE"] = time,
        };

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
        await GitAsync("commit", "--quiet", "--allow-empty", "--message", message);
        _clock.Advance();
        return await HeadAsync();
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
    /// with <see cref="PushAsync"/>.
    /// </summary>
    public async Task<string> AddBareRemoteAsync(string name = "origin")
    {
        var barePath = _root.Combine(name + ".git");
        Directory.CreateDirectory(barePath);
        var command = new GitCommand("init", "--quiet", "--bare") { WorkingDirectory = barePath, Environment = Environment };
        (await Runner.RunAsync(command)).EnsureSuccess(command);
        await GitAsync("remote", "add", name, barePath);
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

            var remotes = (await copy.GitAsync("remote")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var remote in remotes)
            {
                var url = (await copy.GitAsync("remote", "get-url", remote)).StandardOutput.Trim();
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !uri.IsFile)
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
