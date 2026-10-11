using System.Globalization;
using System.Text.RegularExpressions;
using VisualCommit.Core.Operations;

namespace VisualCommit.Git;

/// <summary>
/// Reads git's progress lines from standard error (phase 3 design 1.9): those of the local side,
/// such as <c>Receiving objects:  45% (9/20), 1.20 MiB | 2.00 MiB/s</c> or
/// <c>Updating files:  33% (1/3)</c>, and those of the other side, such as
/// <c>remote: Counting objects: 100% (5/5), done.</c>
/// </summary>
public static partial class GitProgressParser
{
    private const string RemotePrefix = "remote: ";

    /// <summary>
    /// The stages git reports progress for, as the first words of a line (after an optional
    /// <c>remote: </c>). Other lines, git's messages among them, are not progress.
    /// </summary>
    private static readonly string[] Stages =
    [
        "Enumerating objects",
        "Counting objects",
        "Compressing objects",
        "Receiving objects",
        "Resolving deltas",
        "Unpacking objects",
        "Writing objects",
        "Updating files",
        "Checking out files",
        "Delta compression",
    ];

    /// <summary>
    /// Turns a line of git's progress shape into an <see cref="OperationProgress"/>, by the rule
    /// of <see cref="CloneProgressParser.Parse"/>: the stage is the text before the first colon
    /// that ends a word, the percent the first <c>NN%</c>, the text the line without a leading
    /// <c>remote: </c> and without trailing spaces. A line that does not start with one of git's
    /// progress stages (<c>Counting objects</c>, <c>Receiving objects</c>, <c>Updating files</c> …)
    /// gives null, so that a message such as <c>From https://host/x</c> or
    /// <c>fatal: …</c> leaves the shown progress as it is.
    /// </summary>
    public static OperationProgress? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (ParseAny(line) is not { } parsed || !IsProgressStage(parsed.Text))
        {
            return null;
        }

        return new OperationProgress(parsed.Stage, parsed.Percent, parsed.Text);
    }

    /// <summary>
    /// The rule for any line (phase 1's clone progress, which <see cref="CloneProgressParser"/>
    /// keeps): stage, percent and text, or null for a line with no text.
    /// </summary>
    internal static (string Stage, int? Percent, string Text)? ParseAny(string line)
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

        return (stage, percent, text);
    }

    /// <summary>Whether <paramref name="text"/> (without <c>remote: </c>) starts with one of <see cref="Stages"/>, followed by a colon, a space or nothing.</summary>
    private static bool IsProgressStage(string text) =>
        Stages.Any(stage =>
            text.StartsWith(stage, StringComparison.Ordinal)
            && (text.Length == stage.Length || text[stage.Length] is ':' or ' '));

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
