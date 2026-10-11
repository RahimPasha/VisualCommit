using System.Text.RegularExpressions;
using VisualCommit.Core.Git;

namespace VisualCommit.Core.Operations;

/// <summary>
/// Makes git's output fit to show (phase 3 design 1.6, C11): progress lines out, credentials
/// hidden, nothing shortened. The rule of phase 1's clone error text, for every command.
/// </summary>
public static partial class GitOutputCleaner
{
    /// <summary>
    /// The text the error dialog shows for a failed call: <paramref name="standardError"/>
    /// cleaned (<see cref="CleanText"/>), or, when nothing is left of it,
    /// <paramref name="standardOutput"/> cleaned the same way (a merge writes its messages there).
    /// Empty when both are.
    /// </summary>
    public static string Clean(string standardError, string standardOutput = "")
    {
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(standardOutput);
        var error = CleanText(standardError);
        return error.Length > 0 ? error : CleanText(standardOutput);
    }

    /// <summary>
    /// <paramref name="text"/> split into lines at every carriage return and line feed (git ends
    /// each progress update with a carriage return), without git's progress lines
    /// (<see cref="IsProgressLine"/>), its <c>Cloning into</c> line and empty lines, each line
    /// without trailing spaces, joined with line feeds; with the credentials of every URL hidden
    /// (<see cref="GitCommand.HideCredentials"/>).
    /// </summary>
    public static string CleanText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd())
            .Where(line => line.Length > 0 && !line.StartsWith("Cloning into ", StringComparison.Ordinal) && !IsProgressLine(line));
        return GitCommand.HideCredentials(string.Join('\n', lines));
    }

    /// <summary>
    /// Whether <paramref name="line"/> is a progress line of git or of the other side: an optional
    /// <c>remote: </c>, a stage made of words, a colon, spaces and a percentage, as in
    /// <c>Receiving objects:  45% (9/20), 1.20 MiB</c> or <c>remote: Counting objects: 100% (5/5), done.</c>
    /// A message that merely holds a percent sign after a digit, such as a URL with <c>%20</c>, is not.
    /// </summary>
    public static bool IsProgressLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return ProgressLinePattern().IsMatch(line.TrimStart());
    }

    [GeneratedRegex(@"^(?:remote: )?[A-Za-z][A-Za-z ]*: +[0-9]{1,3}%")]
    private static partial Regex ProgressLinePattern();
}
