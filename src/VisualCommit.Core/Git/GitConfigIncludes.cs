namespace VisualCommit.Core.Git;

/// <summary>
/// Git's conditional includes (<c>[includeIf "&lt;condition&gt;"] path = …</c>) as a clone's binding
/// judges them (D331, docs/phase-7b-design.md 16.3, fixed at the review before the gate): the outside
/// read lists each directive but none of its file, because no condition holds outside a repository,
/// while <c>git clone</c> applies those of the new folder (<c>gitdir:</c>, <c>gitdir/i:</c>,
/// <c>onbranch:</c>) and of its URL (<c>hasconfig:remote.*.url:</c>) to its <c>insteadOf</c> rules.
/// </summary>
public static class GitConfigIncludes
{
    private const string Section = "includeif.";
    private const string Variable = ".path";
    private const string Prefix = "%(prefix)/";

    /// <summary>
    /// Whether <paramref name="value"/> is a conditional include directive: a key
    /// <c>includeif.&lt;condition&gt;.path</c>, whatever the condition (an empty one included), the
    /// section and the variable without case as git prints them.
    /// </summary>
    public static bool IsConditional(GitConfigValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var key = value.Key;
        return key.Length >= Section.Length + Variable.Length
            && key.StartsWith(Section, StringComparison.OrdinalIgnoreCase)
            && key.EndsWith(Variable, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The file git reads for an include's <paramref name="path"/>, as git's <c>handle_path_include</c>
    /// works it out: <c>~</c> or <c>~/…</c> under <paramref name="home"/>, a relative path against the
    /// folder of <paramref name="origin"/>, the file that holds the directive, then made full. Null when
    /// this app cannot work it out as git would: an empty or missing path, <c>~user/…</c> (another
    /// user's home), <c>%(prefix)/…</c> (git's own install), <c>~</c> without a home folder, a relative
    /// path from no file (the command line, which git refuses), or a path .NET cannot take.
    /// </summary>
    /// <param name="path">The directive's value as git printed it.</param>
    /// <param name="origin">The full path of the file that holds the directive; null for the command line.</param>
    /// <param name="home">The home folder git uses (<see cref="GitConfigLocations.Home"/>); null when there is none.</param>
    public static string? Resolve(string? path, string? origin, string? home)
    {
        if (string.IsNullOrEmpty(path) || path.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var expanded = path;
        if (path[0] == '~')
        {
            // Git takes the user's name up to the first forward slash; an empty one is the home folder.
            var slash = path.IndexOf('/', StringComparison.Ordinal);
            var userLength = (slash < 0 ? path.Length : slash) - 1;
            if (userLength != 0 || string.IsNullOrEmpty(home))
            {
                return null;
            }

            expanded = home + (slash < 0 ? string.Empty : path[slash..]);
        }

        try
        {
            if (Path.IsPathRooted(expanded))
            {
                return Path.GetFullPath(expanded);
            }

            return origin is not null && Path.GetDirectoryName(origin) is { Length: > 0 } folder
                ? Path.GetFullPath(expanded, folder)
                : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
