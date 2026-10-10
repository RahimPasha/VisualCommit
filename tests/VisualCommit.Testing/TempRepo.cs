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
    private DateTimeOffset _clock = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private TempRepo(TempDirectory root, string path)
    {
        _root = root;
        Path = path;
        Calls = new GitCallLog();
        Runner = new GitRunner(GitExecutable.Value, Calls);

        var home = root.Combine("home");
        Directory.CreateDirectory(home);
        File.WriteAllText(System.IO.Path.Combine(home, ".gitconfig"), string.Empty);
        var environment = new Dictionary<string, string?>
        {
            // No system or user configuration: no aliases, hooks, signing or line-ending
            // settings from the machine. GIT_CONFIG_GLOBAL needs Git 2.32; HOME covers 2.30 and 2.31.
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_GLOBAL"] = System.IO.Path.Combine(home, ".gitconfig"),
            ["HOME"] = home,
            ["XDG_CONFIG_HOME"] = System.IO.Path.Combine(home, ".config"),
            ["GIT_AUTHOR_NAME"] = AuthorName,
            ["GIT_AUTHOR_EMAIL"] = AuthorEmail,
            ["GIT_COMMITTER_NAME"] = AuthorName,
            ["GIT_COMMITTER_EMAIL"] = AuthorEmail,

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

        Environment = environment;
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

            // Files are stored byte for byte, so content and SHAs do not depend on the platform.
            await repo.GitAsync("config", "core.autocrlf", "false");
            await repo.GitAsync("config", "commit.gpgsign", "false");
            await repo.GitAsync("config", "gc.auto", "0");
            return repo;
        }
        catch
        {
            root.Dispose();
            throw;
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
        var time = _clock.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        var environment = new Dictionary<string, string?>(Environment)
        {
            ["GIT_AUTHOR_DATE"] = time,
            ["GIT_COMMITTER_DATE"] = time,
        };

        var command = new GitCommand(arguments) { WorkingDirectory = Path, Environment = environment, StandardInput = input };
        var result = await Runner.RunAsync(command);
        return result.EnsureSuccess(command);
    }

    /// <summary>Writes a file in the working tree, creating its folders. Line endings are written as given.</summary>
    public TempRepo WriteFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
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
        _clock = _clock.AddMinutes(1);
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
        _clock = _clock.AddMinutes(1);
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
        _clock = _clock.AddMinutes(1);
        return await HeadAsync();
    }

    /// <summary>
    /// Makes an independent copy of this repository and everything next to it (bare remotes,
    /// clones) in a new temporary folder, with remote URLs that pointed into the old folder
    /// pointed at the copy. The copy's clock carries on from this one's. Copying a built
    /// scenario is much faster than building it again, because building runs git dozens of times.
    /// </summary>
    public async Task<TempRepo> CopyAsync()
    {
        var root = new TempDirectory(System.IO.Path.GetFileName(Path));
        try
        {
            CopyDirectory(_root.Path, root.Path);
            var copy = new TempRepo(root, root.Combine(System.IO.Path.GetFileName(Path))) { _clock = _clock };

            var remotes = (await copy.GitAsync("remote")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var remote in remotes)
            {
                var url = (await copy.GitAsync("remote", "get-url", remote)).StandardOutput.Trim();
                var full = System.IO.Path.GetFullPath(url);
                if (full.StartsWith(_root.Path, StringComparison.OrdinalIgnoreCase))
                {
                    await copy.GitAsync("remote", "set-url", remote, root.Path + full[_root.Path.Length..]);
                }
            }

            return copy;
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }

    private static void CopyDirectory(string from, string to)
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

    public void Dispose() => _root.Dispose();
}
