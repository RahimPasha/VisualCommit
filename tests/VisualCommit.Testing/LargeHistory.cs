using System.Globalization;
using System.Text;

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
/// <see cref="Version"/> whenever the generator changes, so an old copy is not used.
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
    private static string? _path;

    /// <summary>The working tree of the shared repo, built first if this machine does not have it yet.</summary>
    public static async Task<string> GetAsync()
    {
        if (_path is not null)
        {
            return _path;
        }

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
        if (File.Exists(marker) && Directory.Exists(Path.Combine(final, ".git")))
        {
            return final;
        }

        // A copy without its marker is left over from a build that did not finish.
        if (Directory.Exists(final))
        {
            DeleteDirectory(final);
        }

        using var repo = await TempRepo.CreateAsync("large").ConfigureAwait(false);
        await repo.GitWithInputAsync(BuildStream(), "fast-import", "--quiet").ConfigureAwait(false);
        await repo.GitAsync("reset", "--quiet", "--hard", "main").ConfigureAwait(false);
        var head = await repo.HeadAsync().ConfigureAwait(false);

        Directory.Move(repo.Path, final);
        File.WriteAllText(marker, head + "\n");
        return final;
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
