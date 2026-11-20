namespace VisualCommit.Core;

/// <summary>
/// The app's one search of a <c>PATH</c> value for a program, shared by the launcher and the search
/// for git: only fully qualified folders count (D130).
/// </summary>
public static class ProgramSearch
{
    /// <summary>
    /// The absolute path of <paramref name="program"/> in the first folder of
    /// <paramref name="searchPath"/> (a PATH value, folders split by <paramref name="separator"/>)
    /// that holds it, or null. Only fully qualified folders are searched: an empty or relative
    /// entry would name the current folder, which can be a cloned repository that holds a file of
    /// that name. Started by its bare name, .NET would also look in the app's folder and the
    /// current folder first, before PATH (on Linux and macOS).
    /// </summary>
    public static string? FindOnPath(string program, string? searchPath, char separator, Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(program);
        ArgumentNullException.ThrowIfNull(fileExists);
        foreach (var folder in (searchPath ?? string.Empty).Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Path.IsPathFullyQualified(folder))
            {
                continue;
            }

            var candidate = Path.Combine(folder, program);
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
