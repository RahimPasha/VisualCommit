using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Operations;

namespace VisualCommit.Testing;

/// <summary>
/// The app's log as the checks read it, in both harnesses: the files of a data folder
/// (<c>logs/visualcommit-yyyyMMdd.log</c>, Debug lines included) and the patterns of the lines the
/// checks wait for or count. Every entry is one line,
/// <c>yyyy-MM-dd HH:mm:ss.fff zzz [LVL] message</c>; an exception's stack follows its entry on
/// lines of its own.
/// </summary>
public static class AppLogFile
{
    /// <summary>
    /// The app's log files in a data folder, oldest first: one per day. A test's data folder is
    /// new, so they hold that run's log and nothing older.
    /// </summary>
    public static IReadOnlyList<string> Files(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var folder = new AppPaths(dataDirectory).LogDirectory;
        return Directory.Exists(folder)
            ? [.. Directory.GetFiles(folder, FileAppLog.FilePrefix + "*.log").Order(StringComparer.Ordinal)]
            : [];
    }

    /// <summary>The text of the log in a data folder, all files in order. It can be read while the app writes.</summary>
    public static string ReadText(string dataDirectory)
    {
        var text = new StringBuilder();
        foreach (var file in Files(dataDirectory))
        {
            // The app keeps no file open between entries, and lets others read while it appends.
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            text.Append(reader.ReadToEnd());
        }

        return text.ToString();
    }

    /// <summary>The log in a data folder, one entry per line, without the line ends; no empty last line.</summary>
    public static IReadOnlyList<string> ReadLines(string dataDirectory)
    {
        var lines = ReadText(dataDirectory).Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    /// <summary>
    /// The line the operation queue logs when an operation has ended (design 1.7):
    /// <c>&lt;repo&gt;: &lt;Title&gt;: &lt;outcome&gt; after &lt;N&gt; ms: &lt;message&gt;</c>, at
    /// Info, or at Warning for a failure, whose message is then git's first error line. The
    /// repository, the title and the message are matched as written; a null
    /// <paramref name="outcome"/> matches any outcome and a null <paramref name="message"/> any
    /// message.
    /// </summary>
    public static Regex OperationEnded(string repository, string title, OperationOutcome? outcome = null, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var outcomePattern = outcome is { } known ? known.ToString() : @"\w+";
        var messagePattern = message is null ? string.Empty : ": " + Regex.Escape(message) + "$";
        return new Regex(
            string.Create(CultureInfo.InvariantCulture, $@"\] {Regex.Escape(repository)}: {Regex.Escape(title)}: {outcomePattern} after \d+ ms{messagePattern}"),
            RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// The line the menu layer logs at Debug for every menu it opens (4a, D106):
    /// <c>&lt;repo&gt;: Context menu opened: &lt;header&gt; by pointer</c> or <c>by keyboard</c>.
    /// A null <paramref name="header"/> or <paramref name="by"/> matches any.
    /// </summary>
    public static Regex MenuOpened(string repository, string? header = null, string? by = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        var headerPattern = header is null ? ".*" : Regex.Escape(header);
        var byPattern = by is null ? "(pointer|keyboard)" : Regex.Escape(by);
        return new Regex(
            string.Create(CultureInfo.InvariantCulture, $@"\] {Regex.Escape(repository)}: Context menu opened: {headerPattern} by {byPattern}$"),
            RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// The line a repository logs every time it has read its working tree's status:
    /// <c>&lt;repo&gt;: working tree: &lt;S&gt; staged, &lt;U&gt; unstaged</c>. The first one
    /// says the repository has opened and shown its status.
    /// </summary>
    public static Regex WorkingTreeRead(string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        return new Regex(
            string.Create(CultureInfo.InvariantCulture, $@"\] {Regex.Escape(repository)}: working tree: (?<staged>\d+) staged, (?<unstaged>\d+) unstaged$"),
            RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// The Debug line a repository's hosting logs on the UI thread once a list of open pull requests
    /// is applied (docs/phase-7a-design.md 1.9, 7.8, D322):
    /// <c>&lt;repo&gt;: listed &lt;n&gt; open pull requests of &lt;owner/name&gt;</c>, the count in
    /// the group <c>count</c> and the repository listed in <c>repository</c>.
    /// </summary>
    public static Regex PullRequestsListed(string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        return new Regex(
            string.Create(CultureInfo.InvariantCulture, $@"\] {Regex.Escape(repository)}: listed (?<count>\d+) open pull requests of (?<repository>[^/\s]+/[^/\s]+)$"),
            RegexOptions.CultureInvariant);
    }

    /// <summary>A line that holds <paramref name="text"/> as written, anywhere in it.</summary>
    public static Regex Containing(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return new Regex(Regex.Escape(text), RegexOptions.CultureInvariant);
    }
}
