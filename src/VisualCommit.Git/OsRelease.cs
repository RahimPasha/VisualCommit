using System.Text;
using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>
/// The Linux distribution as <c>os-release</c> names it (<c>/etc/os-release</c>, else
/// <c>/usr/lib/os-release</c>): the first-run screen's package command is chosen by its
/// <c>ID</c> and <c>ID_LIKE</c>, and its <c>PRETTY_NAME</c> is shown.
/// </summary>
public static class OsRelease
{
    /// <summary>The files read, in order: the first that exists is the distribution's.</summary>
    public static IReadOnlyList<string> Files { get; } = ["/etc/os-release", "/usr/lib/os-release"];

    /// <summary>
    /// The distribution of this machine from the first of <see cref="Files"/> that can be read, or
    /// null when none can or it names no <c>ID</c>.
    /// </summary>
    public static LinuxDistribution? Read() => Read(Files, File.Exists, File.ReadAllText);

    /// <summary>
    /// <see cref="Read()"/> over <paramref name="files"/> with the given disk: the tests' way to the
    /// <c>/usr/lib</c> fallback. A file that cannot be read counts as none.
    /// </summary>
    public static LinuxDistribution? Read(IReadOnlyList<string> files, Func<string, bool> fileExists, Func<string, string> readText)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(readText);
        foreach (var file in files)
        {
            try
            {
                if (fileExists(file))
                {
                    return Parse(readText(file));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The next file, as for one that is not there.
            }
        }

        return null;
    }

    /// <summary>
    /// The distribution an <c>os-release</c> text names: lines of <c>KEY=value</c>, a value bare or
    /// in single or double quotes, in double quotes with <c>\"</c>, <c>\\</c>, <c>\$</c> and
    /// <c>\`</c> escaped; a line that starts with <c>#</c> and a blank line are skipped, and a later
    /// line of a key wins. <c>ID_LIKE</c> is split on spaces. Null when there is no <c>ID</c>.
    /// </summary>
    public static LinuxDistribution? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }

            values[line[..equals].Trim()] = Unquote(line[(equals + 1)..].Trim());
        }

        if (!values.TryGetValue("ID", out var id) || id.Length == 0)
        {
            return null;
        }

        var like = values.TryGetValue("ID_LIKE", out var idLike)
            ? idLike.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        return new LinuxDistribution(id, like, values.TryGetValue("PRETTY_NAME", out var pretty) && pretty.Length > 0 ? pretty : null);
    }

    /// <summary>A value without its quotes, double-quoted escapes undone.</summary>
    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1];
        }

        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        var inner = value[1..^1];
        var unescaped = new StringBuilder(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length && inner[i + 1] is '"' or '\\' or '$' or '`')
            {
                unescaped.Append(inner[++i]);
            }
            else
            {
                unescaped.Append(inner[i]);
            }
        }

        return unescaped.ToString();
    }
}
