using System.Globalization;
using System.Text.RegularExpressions;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Reads the progress lines <c>git clone --progress</c> writes to standard error, such as
/// <c>Receiving objects:  45% (9/20), 1.20 MiB | 2.00 MiB/s</c> or, from the other side,
/// <c>remote: Counting objects: 100% (5/5), done.</c>
/// </summary>
public static partial class CloneProgressParser
{
    private const string RemotePrefix = "remote: ";

    /// <summary>
    /// Turns one line into a <see cref="CloneProgress"/>: the stage is the text before the first
    /// colon that ends a word (so <c>Cloning into 'C:\x'...</c> keeps its path whole), the percent
    /// is the first <c>NN%</c>, and the text is the line without a leading <c>remote: </c>. A line
    /// without such a colon is all stage. Returns null for a line with no text.
    /// </summary>
    public static CloneProgress? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        // Messages from the other side end with spaces (or an escape code on a terminal) that
        // clear the rest of the line; they are not part of the text.
        var text = line.Replace("\u001b[K", string.Empty, StringComparison.Ordinal).TrimEnd();
        if (text.StartsWith(RemotePrefix, StringComparison.Ordinal))
        {
            text = text[RemotePrefix.Length..].TrimEnd();
        }
        else if (text == "remote:")
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var stageEnd = FindStageEnd(text);
        var stage = stageEnd < 0 ? text.Trim() : text[..stageEnd].Trim();
        var rest = stageEnd < 0 ? string.Empty : text[(stageEnd + 1)..];

        int? percent = null;
        var match = PercentPattern().Match(rest);
        if (match.Success && int.TryParse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            percent = Math.Clamp(value, 0, 100);
        }

        return new CloneProgress(stage, percent, text);
    }

    /// <summary>The position of the first colon that is followed by a space or ends the line, or -1.</summary>
    private static int FindStageEnd(string text)
    {
        for (var i = 1; i < text.Length; i++)
        {
            if (text[i] == ':' && (i == text.Length - 1 || text[i + 1] == ' '))
            {
                return i;
            }
        }

        return -1;
    }

    [GeneratedRegex(@"(?<![0-9])([0-9]{1,3})%")]
    private static partial Regex PercentPattern();
}
