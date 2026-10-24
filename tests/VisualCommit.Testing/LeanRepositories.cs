using System.Globalization;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Git;

namespace VisualCommit.Testing;

/// <summary>
/// Builds small repositories the lean way (docs/phase-6-design.md 17.1): each from a stream of
/// <c>git fast-import</c>, as <see cref="LargeHistory"/> builds its 100k commits, in a copy of
/// <see cref="TempRepo"/>'s blank git folder, with its bare remote beside it under
/// <c>remotes/&lt;name&gt;.git</c> by a relative URL. Three git processes for a repository with a
/// remote (the working repository's import, the remote's, <c>reset --hard</c>), two without, where
/// the TempRepo way takes eight to ten: the ten repositories of the workspace take 28.
/// <para>
/// The steps, per repository: (1) the blank git folder (its six repository settings included)
/// copied to <c>&lt;name&gt;/.git</c> and the blank bare one to <c>remotes/&lt;name&gt;.git</c>, and
/// <c>.git/config</c> given, as text, <c>[remote "origin"]</c> with the relative URL and the
/// default fetch line and a <c>[branch "&lt;b&gt;"]</c> per tracked branch; (2) <c>git fast-import
/// --quiet --export-marks=&lt;file&gt;</c> in the working repository with the commits it knows and
/// its refs, remote-tracking refs included; (3) <c>git fast-import --quiet</c> in the remote with
/// the commits it holds and its <c>main</c>; (4) <c>HEAD</c> written as text where the repository
/// asks for it, a detached one by the SHA from the marks file; (5) <c>git reset --hard -q</c>;
/// (6) the repository's own last steps (<see cref="LeanRepository.ThenAsync"/>).
/// </para>
/// <para>
/// Every commit is by <see cref="TempRepo.AuthorName"/> at 2026-01-01 12:00 UTC plus its minute,
/// its message ends with a line feed, its files are written byte for byte: the SHAs depend only on
/// the stream, the same on every machine and every git (0.10). The working repository's reflogs
/// hold the build's own entries: <c>fast-import</c> for each ref it set (HEAD's log too, for the
/// branch HEAD names) and, in HEAD's alone, <c>reset: moving to HEAD</c>, dated by the
/// repository's clock; the bare remote keeps no reflogs.
/// </para>
/// </summary>
internal static class LeanRepositories
{
    /// <summary>2026-01-01 12:00:00 UTC, the minute 0 of every lean commit, as <see cref="LargeHistory"/>'s first.</summary>
    private const long FirstCommitTime = 1767268800;

    /// <summary>The folder of the bare remotes, beside the working trees.</summary>
    public const string RemotesFolder = "remotes";

    /// <summary>The time of a lean commit made at 12:<paramref name="minute"/> on 2026-01-01 UTC.</summary>
    public static DateTimeOffset TimeOf(int minute) => DateTimeOffset.FromUnixTimeSeconds(FirstCommitTime + (minute * 60L));

    /// <summary>
    /// Builds <paramref name="repository"/> in <paramref name="root"/>: its working tree at
    /// <c>&lt;root&gt;/&lt;name&gt;</c> and, when it has a remote, the bare remote at
    /// <c>&lt;root&gt;/remotes/&lt;name&gt;.git</c>. Every git call goes through
    /// <paramref name="runner"/>, so that its log counts them.
    /// </summary>
    public static async Task BuildAsync(TempDirectory root, LeanRepository repository, IGitRunner runner)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(runner);
        var path = root.Combine(repository.Name);
        var gitFolder = Path.Combine(path, ".git");
        var blank = await TempRepo.BlankGitFolderAsync("main").ConfigureAwait(false);
        TempRepo.CopyDirectory(blank, gitFolder);

        // Git for Windows hides the git folder it makes (core.hideDotFiles); so is this one, as
        // TempRepo.CreateAsync's.
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(gitFolder, File.GetAttributes(blank));
        }

        File.AppendAllText(Path.Combine(gitFolder, "config"), ConfigText(repository), Utf8);
        var environment = EnvironmentOf(root, repository);
        var marks = Path.Combine(gitFolder, "lean-import.marks");
        await RunAsync(runner, path, environment, StreamOf(repository, repository.Known, repository.Refs), "fast-import", "--quiet", "--export-marks=" + marks).ConfigureAwait(false);

        if (repository.RemoteHolds is { } holds)
        {
            var remote = root.Combine(RemotesFolder, repository.Name + ".git");
            TempRepo.CopyDirectory(await TempRepo.BlankBareFolderAsync().ConfigureAwait(false), remote);
            var remoteRefs = new Dictionary<string, string> { ["refs/heads/main"] = repository.RemoteMain ?? throw new InvalidOperationException($"{repository.Name}'s remote has no main.") };
            await RunAsync(runner, remote, environment, StreamOf(repository, holds, remoteRefs), "fast-import", "--quiet").ConfigureAwait(false);
        }

        var shas = ReadMarks(marks, repository);
        File.Delete(marks);
        if (repository.Head is { } head)
        {
            var text = head.StartsWith("refs/", StringComparison.Ordinal) ? "ref: " + head : shas[head];
            File.WriteAllText(Path.Combine(gitFolder, "HEAD"), text + "\n", Utf8);
        }

        await RunAsync(runner, path, environment, null, "reset", "--hard", "-q").ConfigureAwait(false);
        if (repository.ThenAsync is { } then)
        {
            await then(new LeanSteps(runner, path, environment)).ConfigureAwait(false);
        }
    }

    /// <summary>The configuration added to the blank folder's: the remote and the tracked branches.</summary>
    private static string ConfigText(LeanRepository repository)
    {
        if (repository.RemoteHolds is null)
        {
            return string.Empty;
        }

        var text = new StringBuilder()
            .Append("[remote \"origin\"]\n")
            .Append("\turl = ../").Append(RemotesFolder).Append('/').Append(repository.Name).Append(".git\n")
            .Append("\tfetch = +refs/heads/*:refs/remotes/origin/*\n");
        foreach (var branch in repository.TrackedBranches)
        {
            text.Append("[branch \"").Append(branch).Append("\"]\n")
                .Append("\tremote = origin\n")
                .Append("\tmerge = refs/heads/").Append(branch).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// The fast-import stream of the commits <paramref name="ids"/> of <paramref name="repository"/>
    /// (in the repository's order, each parent before its children), every one on
    /// <c>refs/heads/main</c> with its parent named, then <paramref name="refs"/>, each set to its
    /// commit. A commit's mark is its place among all the repository's commits, the same in both
    /// streams.
    /// </summary>
    private static string StreamOf(LeanRepository repository, IReadOnlyCollection<string> ids, IReadOnlyDictionary<string, string> refs)
    {
        var stream = new StringBuilder();
        foreach (var commit in repository.Commits.Where(commit => ids.Contains(commit.Id)))
        {
            var time = (FirstCommitTime + (commit.Minute * 60L)).ToString(CultureInfo.InvariantCulture);
            var message = commit.Message + "\n";
            stream.Append("commit refs/heads/main\n")
                .Append("mark :").Append(MarkOf(repository, commit.Id)).Append('\n')
                .Append("author ").Append(TempRepo.AuthorName).Append(" <").Append(TempRepo.AuthorEmail).Append("> ").Append(time).Append(" +0000\n")
                .Append("committer ").Append(TempRepo.AuthorName).Append(" <").Append(TempRepo.AuthorEmail).Append("> ").Append(time).Append(" +0000\n");
            AppendData(stream, message);
            if (commit.Parent is { } parent)
            {
                if (!ids.Contains(parent))
                {
                    throw new InvalidOperationException($"{repository.Name}: {commit.Id}'s parent {parent} is not in the stream.");
                }

                stream.Append("from :").Append(MarkOf(repository, parent)).Append('\n');
            }

            foreach (var (file, content) in commit.Files)
            {
                if (content is null)
                {
                    stream.Append("D ").Append(file).Append('\n');
                }
                else
                {
                    stream.Append("M 644 inline ").Append(file).Append('\n');
                    AppendData(stream, content);
                }
            }

            stream.Append('\n');
        }

        foreach (var (name, id) in refs)
        {
            stream.Append("reset ").Append(name).Append('\n')
                .Append("from :").Append(MarkOf(repository, id)).Append("\n\n");
        }

        return stream.ToString();
    }

    private static void AppendData(StringBuilder stream, string data) =>
        stream.Append("data ").Append(Utf8.GetByteCount(data)).Append('\n').Append(data).Append('\n');

    private static int MarkOf(LeanRepository repository, string id)
    {
        for (var index = 0; index < repository.Commits.Count; index++)
        {
            if (repository.Commits[index].Id == id)
            {
                return index + 1;
            }
        }

        throw new InvalidOperationException($"{repository.Name} has no commit {id}.");
    }

    /// <summary>Each commit's SHA by its id, from the marks file fast-import exported (<c>:&lt;mark&gt; &lt;sha&gt;</c>).</summary>
    private static Dictionary<string, string> ReadMarks(string marks, LeanRepository repository)
    {
        var byMark = File.ReadAllLines(marks)
            .Select(line => line.Split(' ', 2))
            .ToDictionary(fields => int.Parse(fields[0].TrimStart(':'), CultureInfo.InvariantCulture), fields => fields[1].Trim());
        return repository.Commits
            .Where(commit => byMark.ContainsKey(MarkOf(repository, commit.Id)))
            .ToDictionary(commit => commit.Id, commit => byMark[MarkOf(repository, commit.Id)], StringComparer.Ordinal);
    }

    /// <summary>
    /// The environment of the builder's git calls in a repository: <see cref="TempRepo"/>'s
    /// isolation with its author, and the repository's clock (a minute after its last commit) as
    /// the date of what they record, such as <c>reset --hard</c>'s reflog entry.
    /// </summary>
    private static Dictionary<string, string?> EnvironmentOf(TempDirectory root, LeanRepository repository)
    {
        var time = repository.Clock.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        return new Dictionary<string, string?>(TempRepo.IsolatedEnvironment(root.Combine("home")))
        {
            ["GIT_AUTHOR_NAME"] = TempRepo.AuthorName,
            ["GIT_AUTHOR_EMAIL"] = TempRepo.AuthorEmail,
            ["GIT_COMMITTER_NAME"] = TempRepo.AuthorName,
            ["GIT_COMMITTER_EMAIL"] = TempRepo.AuthorEmail,
            ["GIT_AUTHOR_DATE"] = time,
            ["GIT_COMMITTER_DATE"] = time,
        };
    }

    private static async Task<GitResult> RunAsync(IGitRunner runner, string folder, IReadOnlyDictionary<string, string?> environment, string? input, params string[] arguments)
    {
        var command = new GitCommand(arguments) { WorkingDirectory = folder, Environment = environment, StandardInput = input };
        return (await runner.RunAsync(command).ConfigureAwait(false)).EnsureSuccess(command);
    }

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>What a lean repository's last steps (<see cref="LeanRepository.ThenAsync"/>) do in its working tree.</summary>
    internal sealed class LeanSteps(IGitRunner runner, string path, IReadOnlyDictionary<string, string?> environment)
    {
        /// <summary>The working tree.</summary>
        public string Path { get; } = path;

        /// <summary>Writes a file of the working tree, byte for byte.</summary>
        public LeanSteps Write(string relativePath, string content)
        {
            File.WriteAllText(System.IO.Path.Combine(Path, relativePath), content, Utf8);
            return this;
        }

        /// <summary>Deletes a file of the working tree.</summary>
        public LeanSteps Delete(string relativePath)
        {
            File.Delete(System.IO.Path.Combine(Path, relativePath));
            return this;
        }

        /// <summary>Runs git in the working tree; throws when it fails.</summary>
        public Task<GitResult> GitAsync(params string[] arguments) => RunAsync(runner, Path, environment, null, arguments);

        /// <summary>Runs git in the working tree and returns its result, also when it fails: a step meant to stop, such as a merge with a conflict.</summary>
        public Task<GitResult> GitAllowingFailureAsync(params string[] arguments) =>
            runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = Path, Environment = environment });
    }
}

/// <summary>One commit of a lean repository (<see cref="LeanRepositories"/>).</summary>
/// <param name="Id">The name the repository's refs and the other commits use for it.</param>
/// <param name="Minute">The minute past 12:00 on 2026-01-01 UTC it is authored and committed at.</param>
/// <param name="Message">Its message, without the line feed every message ends with.</param>
/// <param name="Parent">Its parent's id; null for the root commit.</param>
/// <param name="Files">What it changes in its parent's tree: a file's path and its new content, byte for byte (null deletes it).</param>
internal sealed record LeanCommit(string Id, int Minute, string Message, string? Parent, params (string Path, string? Content)[] Files);

/// <summary>One repository the lean way (<see cref="LeanRepositories"/>).</summary>
/// <param name="Name">Its folder's name, and its remote's (<c>remotes/&lt;name&gt;.git</c>).</param>
/// <param name="Commits">Every commit of the repository and its remote, each parent before its children.</param>
/// <param name="Known">The commits the working repository holds.</param>
/// <param name="Refs">The working repository's refs (full names, remote-tracking ones included) and their commits.</param>
/// <param name="RemoteHolds">The commits the remote holds; null for a repository without a remote.</param>
/// <param name="RemoteMain">The commit of the remote's <c>main</c>.</param>
/// <param name="TrackedBranches">The branches that track the remote's branch of the same name.</param>
/// <param name="Head">
/// Null for the blank folder's <c>ref: refs/heads/main</c>; a full ref name (<c>refs/heads/experiment</c>)
/// for a symbolic HEAD; a commit's id for a detached HEAD.
/// </param>
/// <param name="Clock">A minute after the repository's last commit: the date of the builder's own records and of the repository's <see cref="TempRepo"/>.</param>
/// <param name="ThenAsync">The repository's last steps after the reset, or null.</param>
internal sealed record LeanRepository(
    string Name,
    IReadOnlyList<LeanCommit> Commits,
    IReadOnlyCollection<string> Known,
    IReadOnlyDictionary<string, string> Refs,
    IReadOnlyCollection<string>? RemoteHolds,
    string? RemoteMain,
    IReadOnlyList<string> TrackedBranches,
    string? Head,
    DateTimeOffset Clock,
    Func<LeanRepositories.LeanSteps, Task>? ThenAsync = null);
