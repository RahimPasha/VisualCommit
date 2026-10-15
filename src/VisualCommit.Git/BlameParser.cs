using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Reads <c>git blame --porcelain</c> (docs/phase-5-design.md 0.1, 4.2). The output comes in as
/// Latin-1 characters, one per byte (D66), so that each line's bytes survive as diff lines do.
/// <para>
/// Each line of the file is a group header (<c>&lt;sha&gt; &lt;original line&gt; &lt;final
/// line&gt; [&lt;count&gt;]</c>), then the commit's metadata the first time the commit appears
/// (<c>author</c>, <c>author-mail</c>, <c>author-time</c>, <c>author-tz</c>, the committer's,
/// <c>summary</c>, <c>boundary</c> for a root, <c>previous</c> and <c>filename</c>), then a tab
/// and the line's bytes. <c>previous</c> and <c>filename</c> come again at a group of a commit
/// that blame met under more than one path, so each line takes the path its commit had when the
/// line was read: a three-state machine (a header, metadata, a tab line).
/// </para>
/// </summary>
internal static partial class BlameParser
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Parses the porcelain of one blame.</summary>
    /// <param name="latin1">Git's output, read as Latin-1.</param>
    /// <exception cref="FormatException">The output is not a blame: a line out of place, a field missing, or final line numbers that do not run 1 to N.</exception>
    public static BlameResult Parse(string latin1)
    {
        ArgumentNullException.ThrowIfNull(latin1);
        var lines = new List<BlameLine>();
        var commits = new Dictionary<string, CommitFields>(StringComparer.Ordinal);

        // One record per commit and path, shared by every line that names them.
        var shown = new Dictionary<(string Sha, string Path, string? PreviousSha, string? PreviousPath), BlameCommit>();
        CommitFields? current = null;
        (string Sha, string Path)? previous = null;

        var rows = latin1.Split('\n');

        // Every line of the porcelain ends with a line feed: the piece after the last one is empty.
        var count = rows.Length > 0 && rows[^1].Length == 0 ? rows.Length - 1 : rows.Length;
        for (var i = 0; i < count; i++)
        {
            var row = rows[i];
            if (current is null)
            {
                var header = HeaderPattern().Match(row);
                if (!header.Success)
                {
                    throw new FormatException($"Line {i + 1} of the blame is no group header: {Shorten(row)}");
                }

                if (!int.TryParse(header.Groups["final"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var final) || final != lines.Count + 1)
                {
                    throw new FormatException($"Line {i + 1} of the blame names final line {header.Groups["final"].Value} where {lines.Count + 1} comes next.");
                }

                var sha = header.Groups["sha"].Value;
                if (!commits.TryGetValue(sha, out current))
                {
                    current = new CommitFields(sha);
                    commits.Add(sha, current);
                }

                previous = null;
            }
            else if (row.StartsWith('\t'))
            {
                if (current.Path is null)
                {
                    throw new FormatException($"Line {i + 1} of the blame comes before the file name of {current.Sha}.");
                }

                var key = (current.Sha, current.Path, current.Previous?.Sha, current.Previous?.Path);
                if (!shown.TryGetValue(key, out var commit))
                {
                    commit = current.ToCommit();
                    shown.Add(key, commit);
                }

                lines.Add(new BlameLine(row[1..], commit));
                current = null;
            }
            else
            {
                var space = row.IndexOf(' ', StringComparison.Ordinal);
                var key = space < 0 ? row : row[..space];
                var value = space < 0 ? string.Empty : row[(space + 1)..];
                switch (key)
                {
                    case "author":
                        current.AuthorName = DecodeUtf8(value);
                        break;
                    case "author-mail":
                        current.AuthorEmail = DecodeUtf8(value.StartsWith('<') && value.EndsWith('>') ? value[1..^1] : value);
                        break;
                    case "author-time":
                        current.AuthorSeconds = value;
                        break;
                    case "author-tz":
                        current.AuthorZone = value;
                        break;
                    case "summary":
                        // Git names a commit with an empty message by its id in parentheses.
                        current.Summary = value == $"({current.Sha})" ? string.Empty : DecodeUtf8(value);
                        break;
                    case "boundary":
                        current.IsBoundary = true;
                        break;
                    case "previous":
                        var separator = value.IndexOf(' ', StringComparison.Ordinal);
                        if (separator <= 0)
                        {
                            throw new FormatException($"Line {i + 1} of the blame has no path after the previous commit: {Shorten(row)}");
                        }

                        previous = (value[..separator], GitQuotedPaths.Unquote(value[(separator + 1)..]));
                        break;
                    case "filename":
                        // The file name ends a commit's path information; a previous commit not
                        // named just before it does not apply to this path.
                        current.Path = GitQuotedPaths.Unquote(value);
                        current.Previous = previous;
                        previous = null;
                        break;
                }
            }
        }

        if (current is not null)
        {
            throw new FormatException($"The blame ends after the header of a line of {current.Sha}, without the line.");
        }

        return new BlameResult(lines);
    }

    /// <summary>Text read as Latin-1 (one character per byte) decoded as the UTF-8 it is.</summary>
    private static string DecodeUtf8(string latin1) => Utf8.GetString(Encoding.Latin1.GetBytes(latin1));

    private static string Shorten(string text) => text.Length <= 80 ? text : text[..80] + "…";

    /// <summary>A group header: the commit (40 or 64 hex digits), its line there, the line in the file, and the group's size on its first line.</summary>
    [GeneratedRegex(@"^(?<sha>[0-9a-f]{40}(?:[0-9a-f]{24})?) (?<orig>\d+) (?<final>\d+)(?: (?<count>\d+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderPattern();

    /// <summary>What the porcelain has said about one commit so far; the path and previous commit are the latest.</summary>
    private sealed class CommitFields(string sha)
    {
        public string Sha { get; } = sha;

        public string AuthorName { get; set; } = string.Empty;

        public string AuthorEmail { get; set; } = string.Empty;

        public string AuthorSeconds { get; set; } = "0";

        public string AuthorZone { get; set; } = "+0000";

        public string Summary { get; set; } = string.Empty;

        public bool IsBoundary { get; set; }

        public string? Path { get; set; }

        public (string Sha, string Path)? Previous { get; set; }

        public BlameCommit ToCommit()
        {
            // A time git could not print is read as the epoch, as the graph reads one.
            var time = GitRawDates.TryParse(AuthorSeconds, AuthorZone, out var value) ? value : DateTimeOffset.UnixEpoch;
            return new BlameCommit(Sha, AuthorName, AuthorEmail, time, Summary, Path!, Previous?.Sha, Previous?.Path, IsBoundary);
        }
    }
}
