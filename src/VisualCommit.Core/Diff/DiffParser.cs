using System.Globalization;
using System.Text;

namespace VisualCommit.Core.Diff;

/// <summary>
/// Reads the unified diff of one file as <c>git diff</c> prints it. The text comes in as Latin-1
/// characters, one per byte (D66), so that each line's bytes survive for patches; display text is
/// decoded as UTF-8, or as Latin-1 when the file's lines are not valid UTF-8.
/// </summary>
public static class DiffParser
{
    private const string NoNewlineMarker = "\\ No newline at end of file";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Parses the diff of one file. Text before the first <c>diff --git</c> line (or before the
    /// first hunk when there is none) is the header; a second <c>diff --git</c> line ends the file.
    /// </summary>
    /// <param name="latin1">Git's output, read as Latin-1.</param>
    public static FileDiff Parse(string latin1)
    {
        ArgumentNullException.ThrowIfNull(latin1);
        var lines = SplitLines(latin1);
        if (lines.Count == 0)
        {
            return FileDiff.Empty;
        }

        var header = new List<string>();
        var index = 0;
        var seenDiffLine = false;
        var isBinary = false;
        var isNew = false;
        var isDeleted = false;
        string? oldMode = null;
        string? newMode = null;

        for (; index < lines.Count && !lines[index].StartsWith("@@", StringComparison.Ordinal); index++)
        {
            var line = lines[index];
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                if (seenDiffLine)
                {
                    break;
                }

                seenDiffLine = true;
            }

            header.Add(line);
            if (line.StartsWith("new file mode ", StringComparison.Ordinal))
            {
                isNew = true;
                newMode = line["new file mode ".Length..];
            }
            else if (line.StartsWith("deleted file mode ", StringComparison.Ordinal))
            {
                isDeleted = true;
                oldMode = line["deleted file mode ".Length..];
            }
            else if (line.StartsWith("old mode ", StringComparison.Ordinal))
            {
                oldMode = line["old mode ".Length..];
            }
            else if (line.StartsWith("new mode ", StringComparison.Ordinal))
            {
                newMode = line["new mode ".Length..];
            }
            else if (line.StartsWith("index ", StringComparison.Ordinal))
            {
                // "index abc..def 100644": the mode of both sides when it did not change.
                var space = line.IndexOf(' ', "index ".Length);
                if (space > 0)
                {
                    var mode = line[(space + 1)..];
                    oldMode ??= isNew ? null : mode;
                    newMode ??= isDeleted ? null : mode;
                }
            }
            else if (line.StartsWith("Binary files ", StringComparison.Ordinal) || line == "GIT binary patch")
            {
                isBinary = true;
            }
            else if (line == "--- /dev/null")
            {
                isNew = true;
            }
            else if (line == "+++ /dev/null")
            {
                isDeleted = true;
            }
        }

        var rawHunks = new List<(string Header, List<string> Lines)>();
        for (; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                rawHunks.Add((line, []));
            }
            else if (rawHunks.Count > 0)
            {
                rawHunks[^1].Lines.Add(line);
            }
        }

        var decoder = ChooseDecoder(rawHunks);
        var hunks = new List<DiffHunk>(rawHunks.Count);
        long length = 0;
        foreach (var (hunkHeader, hunkLines) in rawHunks)
        {
            hunks.Add(ParseHunk(hunkHeader, hunkLines, decoder));
            length += hunkLines.Sum(line => line.Length + 1L);
        }

        return new FileDiff(header, hunks, isBinary, isNew, isDeleted, oldMode, newMode, length);
    }

    /// <summary>Splits on line feeds; a final line feed does not start another line. Carriage returns stay.</summary>
    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                lines.Add(text[start..]);
                break;
            }

            lines.Add(text[start..end]);
            start = end + 1;
        }

        return lines;
    }

    private static DiffHunk ParseHunk(string headerRaw, List<string> rawLines, Func<string, string> decode)
    {
        var (oldStart, oldCount, newStart, newCount) = ParseRange(headerRaw);
        var lines = new List<DiffLine>(rawLines.Count);
        var oldNumber = oldStart;
        var newNumber = newStart;
        foreach (var raw in rawLines)
        {
            if (raw.StartsWith(NoNewlineMarker, StringComparison.Ordinal))
            {
                if (lines.Count > 0)
                {
                    lines[^1] = lines[^1] with { NoNewlineAtEnd = true };
                }

                continue;
            }

            // A hunk line always starts with its marker; an empty line can only be a context line
            // whose space was lost on the way (some tools strip trailing spaces).
            var marker = raw.Length > 0 ? raw[0] : ' ';
            var content = raw.Length > 0 ? raw[1..] : string.Empty;
            var text = decode(content.EndsWith('\r') ? content[..^1] : content);
            switch (marker)
            {
                case '+':
                    lines.Add(new DiffLine(DiffLineKind.Added, content, text, null, newNumber++));
                    break;
                case '-':
                    lines.Add(new DiffLine(DiffLineKind.Removed, content, text, oldNumber++, null));
                    break;
                default:
                    lines.Add(new DiffLine(DiffLineKind.Context, content, text, oldNumber++, newNumber++));
                    break;
            }
        }

        return new DiffHunk(headerRaw, decode(headerRaw), oldStart, oldCount, newStart, newCount, lines);
    }

    /// <summary>Reads the ranges of a hunk header, <c>@@ -a,b +c,d @@</c>; a missing count is 1.</summary>
    public static (int OldStart, int OldCount, int NewStart, int NewCount) ParseRange(string header)
    {
        var end = header.IndexOf(" @@", 2, StringComparison.Ordinal);
        var ranges = (end > 0 ? header[2..end] : header[2..]).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (ranges.Length < 2 || ranges[0][0] != '-' || ranges[1][0] != '+')
        {
            throw new FormatException($"Not a hunk header: {header}");
        }

        var (oldStart, oldCount) = ParsePart(ranges[0][1..]);
        var (newStart, newCount) = ParsePart(ranges[1][1..]);
        return (oldStart, oldCount, newStart, newCount);

        static (int Start, int Count) ParsePart(string part)
        {
            var comma = part.IndexOf(',', StringComparison.Ordinal);
            return comma < 0
                ? (int.Parse(part, CultureInfo.InvariantCulture), 1)
                : (int.Parse(part[..comma], CultureInfo.InvariantCulture), int.Parse(part[(comma + 1)..], CultureInfo.InvariantCulture));
        }
    }

    /// <summary>UTF-8 when every line of the file is valid UTF-8, else Latin-1 (the characters as they are).</summary>
    private static Func<string, string> ChooseDecoder(List<(string Header, List<string> Lines)> hunks)
    {
        foreach (var (_, lines) in hunks)
        {
            foreach (var line in lines)
            {
                if (!IsAscii(line) && !IsValidUtf8(line))
                {
                    return static latin1 => latin1;
                }
            }
        }

        return DecodeUtf8;
    }

    /// <summary>Decodes a line read as Latin-1 as the UTF-8 it holds.</summary>
    public static string DecodeUtf8(string latin1)
    {
        ArgumentNullException.ThrowIfNull(latin1);
        return IsAscii(latin1) ? latin1 : Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(latin1));
    }

    private static bool IsAscii(string text)
    {
        foreach (var c in text)
        {
            if (c > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidUtf8(string latin1)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(Encoding.Latin1.GetBytes(latin1));
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
