using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>What the tests of <see cref="GitRepository"/> and <see cref="RepositoryWatcher"/> share.</summary>
internal static class RepositoryTestSupport
{
    private static readonly string GitPath =
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");

    public static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    /// <summary>
    /// A runner as the app makes it. Its calls inherit the test process's environment, which the
    /// module initializer has cut off from the machine's git configuration.
    /// </summary>
    public static GitRunner NewRunner(IGitCallLog? calls = null, IAppLog? log = null) => new(GitPath, calls, log);

    public static Task<GitRepository> OpenAsync(TempRepo repo) => GitRepository.OpenAsync(NewRunner(), repo.Path, TestCancelled);

    /// <summary>A folder next to the repo's working tree, inside the folder the repo deletes when disposed.</summary>
    public static string Sibling(TempRepo repo, string name) => Path.Combine(Path.GetDirectoryName(repo.Path)!, name);

    /// <summary>
    /// Asserts that <paramref name="actual"/> is an absolute path, written with the platform's
    /// separators and without a trailing one, of the same folder as <paramref name="expected"/>.
    /// The paths themselves may differ: git reports the physical path, and the test folder can lie
    /// behind a symbolic link (macOS's /var) or a short name (C:\Users\RUNNER~1 on CI's Windows).
    /// So a file with a unique name is written through one path and looked for through the other.
    /// </summary>
    public static void AssertSameFolder(string expected, string actual)
    {
        Assert.True(Path.IsPathFullyQualified(actual), $"Not an absolute path: {actual}");
        Assert.Equal(Path.GetFullPath(actual), actual);
        Assert.False(Path.EndsInDirectorySeparator(actual), $"Ends with a separator: {actual}");

        var marker = $"same-folder-{Guid.NewGuid():N}";
        var markerPath = Path.Combine(expected, marker);
        File.WriteAllText(markerPath, string.Empty);
        try
        {
            Assert.True(File.Exists(Path.Combine(actual, marker)), $"{actual} is not the folder {expected}.");
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    /// <summary>
    /// Imports <paramref name="history"/> into the repo with one git fast-import call, much
    /// quicker than a git call per step (D51), and returns the commit id of each mark.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> ImportAsync(TempRepo repo, HistoryStream history)
    {
        var marksFile = Sibling(repo, $"marks-{Guid.NewGuid():N}.txt");
        await repo.GitWithInputAsync(history.ToString(), "fast-import", "--quiet", "--export-marks=" + marksFile);
        return File.ReadAllLines(marksFile)
            .Select(line => line.Split(' '))
            .ToDictionary(fields => fields[0], fields => fields[1]);
    }
}

/// <summary>
/// Writes a git fast-import stream: commits by <see cref="TempRepo.AuthorName"/>, a minute apart
/// from 2026-01-01 12:00 UTC, as the repo builder's clock would give them.
/// </summary>
internal sealed class HistoryStream
{
    private readonly StringBuilder _text = new();
    private int _marks;
    private long _time = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    /// <summary>
    /// Adds a commit to <paramref name="reference"/> and returns its mark (<c>:1</c>). Without
    /// <paramref name="from"/> it follows the reference's last commit in this stream, or starts a
    /// new history.
    /// </summary>
    public string Commit(string reference, string message, string? from = null, string? merge = null, string? file = null, string? content = null)
    {
        var mark = ":" + ++_marks;
        _text.Append("commit ").Append(reference).Append('\n')
            .Append("mark ").Append(mark).Append('\n')
            .Append("committer ").Append(TempRepo.AuthorName).Append(" <").Append(TempRepo.AuthorEmail).Append("> ")
            .Append(_time.ToString(CultureInfo.InvariantCulture)).Append(" +0000\n");
        AppendData(message);
        if (from is not null)
        {
            _text.Append("from ").Append(from).Append('\n');
        }

        if (merge is not null)
        {
            _text.Append("merge ").Append(merge).Append('\n');
        }

        if (file is not null)
        {
            _text.Append("M 100644 inline ").Append(file).Append('\n');
            AppendData(content ?? string.Empty);
        }

        _text.Append('\n');
        _time += 60;
        return mark;
    }

    /// <summary>Points <paramref name="reference"/> at <paramref name="target"/>, such as a lightweight tag or a remote branch.</summary>
    public void Reset(string reference, string target) =>
        _text.Append("reset ").Append(reference).Append("\nfrom ").Append(target).Append("\n\n");

    /// <summary>Adds an annotated tag <paramref name="name"/> on the commit <paramref name="target"/>.</summary>
    public void Tag(string name, string target, string message)
    {
        _text.Append("tag ").Append(name).Append('\n')
            .Append("from ").Append(target).Append('\n')
            .Append("tagger ").Append(TempRepo.AuthorName).Append(" <").Append(TempRepo.AuthorEmail).Append("> ")
            .Append(_time.ToString(CultureInfo.InvariantCulture)).Append(" +0000\n");

        // Unlike a commit, a tag ends with its message: a blank line after it would be read as a command.
        AppendData(message);
    }

    /// <summary>A line of <paramref name="count"/> commits on main, all with the same tree: a long history built in about a second (D51).</summary>
    public static HistoryStream Line(int count)
    {
        var history = new HistoryStream();
        history.Commit("refs/heads/main", "Commit 1", file: "file.txt", content: "content\n");
        for (var i = 2; i <= count; i++)
        {
            history.Commit("refs/heads/main", $"Commit {i}");
        }

        return history;
    }

    public override string ToString() => _text.ToString();

    private void AppendData(string text) =>
        _text.Append("data ").Append(Encoding.UTF8.GetByteCount(text)).Append('\n').Append(text).Append('\n');
}

/// <summary>A log that keeps what is written to it.</summary>
internal sealed class ListLog : IAppLog
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries => [.. _entries];

    public void Write(LogLevel level, string message, Exception? exception = null) =>
        _entries.Enqueue((level, exception is null ? message : $"{message} {exception.Message}"));
}
