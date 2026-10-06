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
    /// Called for every line git writes to standard output, as it arrives, on a thread-pool
    /// thread. When set, <see cref="GitResult.StandardOutput"/> stays empty, so that large
    /// output is not held in memory twice.
    /// </summary>
    public Action<string>? OnOutputLine { get; init; }

    /// <summary>
    /// Called for every line git writes to standard error, as it arrives, on a thread-pool
    /// thread. Progress updates that git ends with a carriage return each count as a line.
    /// </summary>
    public Action<string>? OnErrorLine { get; init; }

    /// <summary>The command as a user would type it, for logs and error messages.</summary>
    public string DisplayText => "git " + string.Join(' ', Arguments.Select(Quote));

    public override string ToString() => DisplayText;

    private static string Quote(string argument) =>
        argument.Length == 0 || argument.Any(c => char.IsWhiteSpace(c) || c == '"')
            ? "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : argument;
}
