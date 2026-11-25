using System.Globalization;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Git;

namespace VisualCommit.Testing;

/// <summary>
/// The 100k-commit scenario repo (D51): <see cref="CommitCount"/> commits on <c>main</c>, where
/// every cycle of 50 commits is 41 commits on main, a branch <c>feature</c> of 8 commits started
/// from the 41st, and a merge of it back into main. All commits share one tree (a README.md), so
/// the repo is built in seconds with <c>git fast-import</c>.
/// <para>
/// Commit number <c>n</c> (1 to 100,000) is authored at 2026-01-01 12:00 UTC plus <c>n - 1</c>
/// minutes, so <c>git log --date-order</c> prints them from 100,000 down to 1: the graph row
/// <c>r</c> (0 at the top) shows commit <c>100,000 - r</c>. Its subject and lane follow from
/// its number (<see cref="SubjectOf"/>, <see cref="LaneOf"/>). The tag <c>middle</c> is on
/// commit 50,000, a merge, in row 50,000.
/// </para>
/// <para>
/// The repo is built once per machine and kept in the temp folder under a versioned name, so
/// test runs and the parallel test processes of one run share it. It must only be read: change
/// <see cref="Version"/> whenever the generator changes, so an old copy is not used. Each test
/// process proves a copy through git before it uses it (<see cref="IsUsableAsync"/>), and one that
/// fails the proof is built again, as one whose build did not finish is.
/// </para>
/// </summary>
public static class LargeHistory
{
    public const int CommitCount = 100_000;

    /// <summary>Commits per cycle: 41 on main, 8 on the feature branch, 1 merge.</summary>
    public const int CycleLength = 50;

    /// <summary>The name of the tag on commit 50,000.</summary>
    public const string MiddleTag = "middle";

    private const string Version = "v1";
    private const long FirstCommitTime = 1767268800; // 2026-01-01 12:00:00 UTC

    private static readonly SemaphoreSlim BuildGate = new(1, 1);
    private static readonly SemaphoreSlim BareGate = new(1, 1);
    private static string? _path;
    private static string? _barePath;

    /// <summary>
    /// The working tree of the shared repo, built first if this machine does not have it yet. The
    /// wait for the build does not count against the test's time in the hang watchdog
    /// (<see cref="HangWatchdogAttribute.SharedPreparationAsync{T}"/>).
    /// </summary>
    public static Task<string> GetAsync() =>
        _path is { } path
            ? Task.FromResult(path)
            : HangWatchdogAttribute.SharedPreparationAsync("the 100k-commit repo", GetOnceAsync());

    /// <summary>
    /// A bare copy of the shared repo, for a server that serves it in place
    /// (<see cref="AuthRemoteServer.StartAsync"/>: phase 1's checks 13 and 14 clone it over HTTP,
    /// D150). It is made once per machine from the working tree's repo, beside it in the same
    /// folder, under a name that needs no escaping in a URL (<c>large-history-v1.git</c>), and
    /// must only be read, as that one. The wait for it, and for the working tree's repo it is made
    /// from, does not count against the test's time in the hang watchdog.
    /// </summary>
    public static Task<string> GetBareAsync() =>
        _barePath is { } path
            ? Task.FromResult(path)
            : HangWatchdogAttribute.SharedPreparationAsync("the bare copy of the 100k-commit repo", GetBareOnceAsync());

    private static async Task<string> GetOnceAsync()
    {
        await BuildGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _path ??= await GetOrBuildAsync().ConfigureAwait(false);
            return _path;
        }
        finally
        {
            BuildGate.Release();
        }
    }

    private static async Task<string> GetBareOnceAsync()
    {
        await BareGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_barePath is null)
            {
                var working = await GetOnceAsync().ConfigureAwait(false);
                _barePath = await GetOrBuildBareAsync(working).ConfigureAwait(false);
            }

            return _barePath;
        }
        finally
        {
            BareGate.Release();
        }
    }

    /// <summary>The number of the commit in graph row <paramref name="row"/> (0 is the top row).</summary>
    public static int CommitInRow(int row) => CommitCount - row;

    /// <summary>The subject of commit number <paramref name="number"/>: "Main commit n", "Feature commit n" or "Merge commit n".</summary>
    public static string SubjectOf(int number) =>
        IsMerge(number) ? $"Merge commit {number}"
        : IsFeature(number) ? $"Feature commit {number}"
        : $"Main commit {number}";

    /// <summary>The lane the graph draws commit <paramref name="number"/>'s node in: 1 for the feature commits, 0 for the others.</summary>
    public static int LaneOf(int number) => IsFeature(number) ? 1 : 0;

    public static bool IsMerge(int number) => number % CycleLength == 0;

    public static bool IsFeature(int number) => number % CycleLength is >= 42 and <= 49;

    /// <summary>When commit <paramref name="number"/> was authored and committed.</summary>
    public static DateTimeOffset DateOf(int number) =>
        DateTimeOffset.FromUnixTimeSeconds(FirstCommitTime + ((number - 1) * 60L));

    private static async Task<string> GetOrBuildAsync()
    {
        var shared = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests", "shared");
        Directory.CreateDirectory(shared);
        var final = Path.Combine(shared, $"large-history-{Version}");
        var marker = final + ".complete";

        // Test processes of one run start at the same time: one builds, the others wait for it.
        using var lockFile = await LockAsync(Path.Combine(shared, $"large-history-{Version}.lock")).ConfigureAwait(false);
        if (MarkedSha(marker) is { } sha && await IsUsableAsync(final, sha, bare: false).ConfigureAwait(false))
        {
            return final;
        }

        // A copy without its marker is left over from a build that did not finish, and one that
        // git does not take for the repository its marker names has lost files or folders since
        // (IsUsableAsync). Either is built again; the bare copy, which has object files of its own,
        // is proven on its own.
        DeleteCopy(final, marker);

        using var repo = await TempRepo.CreateAsync("large").ConfigureAwait(false);
        await repo.GitWithInputAsync(BuildStream(), "fast-import", "--quiet").ConfigureAwait(false);
        await repo.GitAsync("reset", "--quiet", "--hard", "main").ConfigureAwait(false);
        var head = await repo.HeadAsync().ConfigureAwait(false);

        Directory.Move(repo.Path, final);
        File.WriteAllText(marker, head + "\n");
        return final;
    }

    /// <summary>
    /// The bare copy of <paramref name="working"/>, made beside it with a marker of its own as
    /// <see cref="GetOrBuildAsync"/> makes the working tree's repo. A test can make it beside a
    /// small working copy of its own, which leaves the shared folder alone.
    /// </summary>
    internal static async Task<string> GetOrBuildBareAsync(string working)
    {
        var shared = Path.GetDirectoryName(working)!;
        var final = Path.Combine(shared, $"large-history-{Version}.git");
        var marker = final + ".complete";

        // Test processes of one run start at the same time: one makes the copy, the others wait for it.
        using var lockFile = await LockAsync(final + ".lock").ConfigureAwait(false);
        if (MarkedSha(marker) is { } sha && await IsUsableAsync(final, sha, bare: true).ConfigureAwait(false))
        {
            return final;
        }

        // A copy without its marker is left over from one that did not finish, and one that git
        // does not take for the repository its marker names has lost files or folders since
        // (IsUsableAsync): either is made again.
        DeleteCopy(final, marker);

        // Its own object files, not links to the working tree's repo's: nothing done to one can
        // reach the other.
        using var home = new TempDirectory("large-bare");
        var environment = TempRepo.IsolatedEnvironment(home.Combine("home"));
        var runner = new GitRunner(TempRepo.GitPath);
        var clone = new GitCommand("clone", "--quiet", "--bare", "--no-hardlinks", working, final) { WorkingDirectory = shared, Environment = environment };
        (await runner.RunAsync(clone).ConfigureAwait(false)).EnsureSuccess(clone);
        var main = new GitCommand("rev-parse", "refs/heads/main") { WorkingDirectory = final, Environment = environment };
        var head = (await runner.RunAsync(main).ConfigureAwait(false)).EnsureSuccess(main).StandardOutput.Trim();

        File.WriteAllText(marker, head + "\n");
        return final;
    }

    /// <summary>
    /// The SHA a shared copy's marker holds: the copy's <c>main</c> when it was made. Null when
    /// there is no marker or it holds no SHA (40 or 64 lower-case hexadecimal digits).
    /// </summary>
    internal static string? MarkedSha(string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        if (!File.Exists(marker))
        {
            return null;
        }

        var text = File.ReadAllText(marker).Trim();
        return text.Length is 40 or 64 && text.All(char.IsAsciiHexDigitLower) ? text : null;
    }

    /// <summary>
    /// Whether git takes <paramref name="folder"/> for the shared copy whose marker holds
    /// <paramref name="sha"/>: run in the folder with the tests' isolation, <c>git rev-parse
    /// --is-bare-repository</c> says it is a repository of the kind asked for (<c>true</c> with
    /// <paramref name="bare"/>, <c>false</c> for one with a working tree), its
    /// <c>refs/heads/main</c> is <paramref name="sha"/>, and that commit can be read
    /// (<c>git cat-file -e &lt;sha&gt;^{commit}</c>). Three git calls, once per test process and
    /// copy (<see cref="GetAsync"/> and <see cref="GetBareAsync"/> keep what they found).
    /// <para>
    /// The marker alone proves none of it, as the copies outlive every test process: a cleaner of
    /// the temp folder that removes every empty folder, whatever its age, as Windows' Storage Sense
    /// does when the disk runs low, empties the bare copy's <c>refs</c>, whose refs are all in
    /// <c>packed-refs</c>, and removes it, and git then takes the folder for no repository (the
    /// laptop on 2026-11-24); a cleaner that removes old files could take a ref or a pack.
    /// </para>
    /// </summary>
    /// <param name="folder">The copy's folder: the working tree, or the bare repository.</param>
    /// <param name="sha">The SHA the copy's marker holds.</param>
    /// <param name="bare">Whether the copy is the bare one.</param>
    /// <param name="refs">Other refs of the copy that its users read, by their full names; not proven yet.</param>
    internal static async Task<bool> IsUsableAsync(string folder, string sha, bool bare, params string[] refs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        ArgumentNullException.ThrowIfNull(refs);
        if (!Directory.Exists(folder))
        {
            return false;
        }

        using var home = new TempDirectory("large-proof");
        var environment = TempRepo.IsolatedEnvironment(home.Combine("home"));
        var runner = new GitRunner(TempRepo.GitPath);
        Task<GitResult> Git(params string[] arguments) =>
            runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = folder, Environment = environment });

        var kind = await Git("rev-parse", "--is-bare-repository").ConfigureAwait(false);
        if (!kind.Succeeded || kind.StandardOutput.Trim() != (bare ? "true" : "false"))
        {
            return false;
        }

        var main = await Git("rev-parse", "--verify", "--quiet", "refs/heads/main").ConfigureAwait(false);
        if (!main.Succeeded || !string.Equals(main.StandardOutput.Trim(), sha, StringComparison.Ordinal))
        {
            return false;
        }

        return (await Git("cat-file", "-e", sha + "^{commit}").ConfigureAwait(false)).Succeeded;
    }

    /// <summary>Deletes a shared copy and its marker, the marker first, so that no marker is left beside a copy deleted in part.</summary>
    private static void DeleteCopy(string folder, string marker)
    {
        File.Delete(marker);
        if (Directory.Exists(folder))
        {
            DeleteDirectory(folder);
        }
    }

    /// <summary>The fast-import stream of the whole history.</summary>
    private static string BuildStream()
    {
        var stream = new StringBuilder(20_000_000);
        for (var number = 1; number <= CommitCount; number++)
        {
            var position = ((number - 1) % CycleLength) + 1;
            var fork = number - position + 41;
            var branch = IsFeature(number) ? "refs/heads/feature" : "refs/heads/main";
            var message = SubjectOf(number);
            var time = (FirstCommitTime + ((number - 1) * 60L)).ToString(CultureInfo.InvariantCulture);

            stream.Append("commit ").Append(branch).Append('\n')
                .Append("mark :").Append(number).Append('\n')
                .Append("author Test Author <author@example.com> ").Append(time).Append(" +0000\n")
                .Append("committer Test Author <author@example.com> ").Append(time).Append(" +0000\n")
                .Append("data ").Append(Encoding.UTF8.GetByteCount(message)).Append('\n').Append(message).Append('\n');

            if (position == 42)
            {
                // The first feature commit starts from the 41st commit of the cycle.
                stream.Append("from :").Append(fork).Append('\n');
            }
            else if (IsMerge(number))
            {
                stream.Append("from :").Append(fork).Append('\n')
                    .Append("merge :").Append(number - 1).Append('\n');
            }

            if (number == 1)
            {
                const string readme = "# Large history\n";
                stream.Append("M 644 inline README.md\n")
                    .Append("data ").Append(Encoding.UTF8.GetByteCount(readme)).Append('\n').Append(readme).Append('\n');
            }

            stream.Append('\n');
        }

        stream.Append("reset refs/tags/").Append(MiddleTag).Append('\n')
            .Append("from :").Append(CommitCount / 2).Append("\n\n");
        return stream.ToString();
    }

    private static async Task<FileStream> LockAsync(string path)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(200).ConfigureAwait(false);
            }
        }
    }

    private static void DeleteDirectory(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
