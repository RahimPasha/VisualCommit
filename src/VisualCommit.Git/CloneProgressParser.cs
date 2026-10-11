using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// Reads the progress lines <c>git clone --progress</c> writes to standard error, such as
/// <c>Receiving objects:  45% (9/20), 1.20 MiB | 2.00 MiB/s</c> or, from the other side,
/// <c>remote: Counting objects: 100% (5/5), done.</c> The rule is <see cref="GitProgressParser"/>'s.
/// </summary>
public static class CloneProgressParser
{
    /// <summary>
    /// Turns one line into a <see cref="CloneProgress"/>: the stage is the text before the first
    /// colon that ends a word (so <c>Cloning into 'C:\x'...</c> keeps its path whole), the percent
    /// is the first <c>NN%</c>, and the text is the line without a leading <c>remote: </c>. A line
    /// without such a colon is all stage. Returns null for a line with no text. Unlike
    /// <see cref="GitProgressParser.Parse"/>, every line counts, git's messages included.
    /// </summary>
    public static CloneProgress? Parse(string line) =>
        GitProgressParser.ParseAny(line) is { } parsed ? new CloneProgress(parsed.Stage, parsed.Percent, parsed.Text) : null;
}
