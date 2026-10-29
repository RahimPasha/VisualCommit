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
    /// The call record keeps <see cref="StandardInput"/> (<see cref="GitCallRecord.StandardInput"/>),
    /// so that the activity log can show it. Set only for <c>update-ref --stdin</c>, whose lines name
    /// refs and SHAs and carry no secret (docs/phase-5-design.md 2.3); a commit message or a patch on
    /// standard input is never recorded.
    /// </summary>
    public bool RecordsStandardInput { get; init; }

    /// <summary>
    /// On Windows the git process runs at below-normal priority, set right after it starts (the
    /// processes it starts inherit it): the merge tools' lookup, <c>git mergetool --tool-help</c>,
    /// which runs in the background for 20 to 60 s and must not slow the first repository's reads
    /// (docs/phase-6-design.md 8.10, D202, Q1). Ignored elsewhere.
    /// </summary>
    public bool BelowNormalPriority { get; init; }

    /// <summary>
    /// How <see cref="StandardInput"/> and standard output are turned into bytes and back. Null
    /// means UTF-8. <see cref="Latin1"/> carries every byte as one character, for content that need
    /// not be UTF-8: diffs, patches and file contents (D66). Standard error is always UTF-8.
    /// </summary>
    public System.Text.Encoding? Encoding { get; init; }

    /// <summary>One character per byte, both ways: what <see cref="Encoding"/> takes for byte-exact content (D66).</summary>
    public static System.Text.Encoding Latin1 => System.Text.Encoding.Latin1;

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
    /// <c>/</c>, <c>?</c>, <c>#</c>, white space or a control character: <see cref="EndsAuthority"/>),
    /// so a password with an <c>@</c> in it is hidden whole. An <c>@</c> in the path, or in <c>user@host:path</c>, is left alone.
    /// </summary>
    public static string HideCredentials(string text) => ReplaceCredentials(text, "***@");

    /// <summary>
    /// Removes the user information of every URL in <paramref name="text"/>, the <c>@</c>
    /// included (<c>https://user:token@host/x.git</c> becomes <c>https://host/x.git</c>): what
    /// "Copy URL" copies, which can be shared and cloned (phase 3 design 2.23, D103). The user
    /// information is found as <see cref="HideCredentials"/> finds it.
    /// </summary>
    public static string RemoveCredentials(string text) => ReplaceCredentials(text, string.Empty);

    /// <summary>
    /// Whether <paramref name="c"/> ends a URL's authority where credentials are looked for: a
    /// <c>/</c>, <c>?</c> or <c>#</c>, white space, or a control character, so that the <c>\0</c>
    /// between <c>config --null</c>'s entries ends a value without a trailing <c>/</c>
    /// (docs/phase-7a-design.md 4.2, 4.3). Public for the runner's cut, as Core grants no
    /// <c>InternalsVisibleTo</c>.
    /// </summary>
    public static bool EndsAuthority(char c) => c is '/' or '?' or '#' || char.IsWhiteSpace(c) || char.IsControl(c);

    private static string ReplaceCredentials(string text, string replacement)
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
            while (authorityEnd < text.Length && !EndsAuthority(text[authorityEnd]))
            {
                authorityEnd++;
            }

            var at = text.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart);
            if (at > authorityStart)
            {
                result.Append(text, copied, authorityStart - copied).Append(replacement);
                copied = at + 1;
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
