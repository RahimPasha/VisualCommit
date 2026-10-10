namespace VisualCommit.Core.Git;

/// <summary>One call of the git executable: its arguments and how to run it.</summary>
public sealed class GitCommand
{
    public GitCommand(params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        Arguments = [.. arguments];
    }

    /// <summary>The arguments after "git". Each is passed as one argument; no shell quoting is needed.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>The folder git runs in, usually the repo. Null means the app's current folder.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Text written to git's standard input, which is then closed. Null closes it at once.</summary>
    public string? StandardInput { get; init; }

    /// <summary>
    /// Extra environment variables for this call. They override the runner's defaults; a null
    /// value removes the variable.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>
    /// Called for every line git writes to standard output, as it arrives, on a background
    /// thread, never the caller's. When set, <see cref="GitResult.StandardOutput"/> stays empty, so that large
    /// output is not held in memory twice. A call that is not cancelled returns only after the
    /// handler has returned for the last line, however long it takes.
    /// </summary>
    public Action<string>? OnOutputLine { get; init; }

    /// <summary>
    /// Called for every line git writes to standard error, as it arrives, on a background
    /// thread, never the caller's. Progress updates that git ends with a carriage return each count as a line.
    /// </summary>
    public Action<string>? OnErrorLine { get; init; }

    /// <summary>
    /// The command as a user would type it, for logs, the call record and error messages. The
    /// credentials of a URL in it are hidden (<see cref="HideCredentials"/>); git itself still
    /// gets <see cref="Arguments"/> as they are.
    /// </summary>
    public string DisplayText => "git " + string.Join(' ', Arguments.Select(argument => Quote(HideCredentials(argument))));

    public override string ToString() => DisplayText;

    /// <summary>
    /// Replaces the user information of every URL in <paramref name="text"/>
    /// (<c>https://user:token@host/x.git</c>) with <c>***</c>
    /// (<c>https://***@host/x.git</c>), as git does when it shows a URL. The user information is
    /// everything from <c>://</c> to the last <c>@</c> before the host ends (at the first
    /// <c>/</c>, <c>?</c>, <c>#</c> or white space), so a password with an <c>@</c> in it is
    /// hidden whole. An <c>@</c> in the path, or in <c>user@host:path</c>, is left alone.
    /// </summary>
    public static string HideCredentials(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        const string SchemeEnd = "://";
        var schemeEnd = text.IndexOf(SchemeEnd, StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return text;
        }

        var result = new System.Text.StringBuilder(text.Length);
        var copied = 0;
        while (schemeEnd >= 0)
        {
            var authorityStart = schemeEnd + SchemeEnd.Length;
            var authorityEnd = authorityStart;
            while (authorityEnd < text.Length && text[authorityEnd] is not ('/' or '?' or '#') && !char.IsWhiteSpace(text[authorityEnd]))
            {
                authorityEnd++;
            }

            var at = text.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart);
            if (at > authorityStart)
            {
                result.Append(text, copied, authorityStart - copied).Append("***");
                copied = at;
            }

            schemeEnd = text.IndexOf(SchemeEnd, authorityEnd, StringComparison.Ordinal);
        }

        return copied == 0 ? text : result.Append(text, copied, text.Length - copied).ToString();
    }

    private static string Quote(string argument) =>
        argument.Length == 0 || argument.Any(c => char.IsWhiteSpace(c) || c == '"')
            ? "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : argument;
}
