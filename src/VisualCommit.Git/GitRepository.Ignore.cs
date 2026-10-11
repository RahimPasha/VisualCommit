using System.Text;
using VisualCommit.Core.Logging;

namespace VisualCommit.Git;

/// <summary>Ignore a file: a line appended to the working tree's <c>.gitignore</c> (docs/phase-3-design.md 2.30). No git call.</summary>
public sealed partial class GitRepository
{
    public async Task AppendToGitignoreAsync(string pattern, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        if (pattern.Contains('\n', StringComparison.Ordinal) || pattern.Contains('\r', StringComparison.Ordinal))
        {
            throw new ArgumentException("A .gitignore pattern is one line.", nameof(pattern));
        }

        var path = Path.Combine(WorkingDirectory, ".gitignore");
        var existing = File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : [];

        // The file's own line ending: a file written with CRLF gets CRLF lines.
        var text = Encoding.UTF8.GetString(existing);
        var lineEnding = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (GitignoreAlreadyHas(text, pattern))
        {
            _log.Info($"{pattern} is already in .gitignore; nothing added.");
            return;
        }

        var addition = new StringBuilder();
        if (existing.Length > 0 && existing[^1] != (byte)'\n')
        {
            addition.Append(lineEnding);
        }

        addition.Append(pattern).Append(lineEnding);

        // Appended as bytes: the rest of the file, its encoding and a byte order mark stay as they are.
        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        await stream.WriteAsync(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(addition.ToString()), cancellationToken).ConfigureAwait(false);
        _log.Info($"Added {pattern} to .gitignore.");
    }

    /// <summary>
    /// Whether <paramref name="text"/> already has the line <paramref name="pattern"/> with no
    /// negation (<c>!</c>) after it that could take it back: then a second copy changes nothing.
    /// </summary>
    private static bool GitignoreAlreadyHas(string text, string pattern)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var last = lines.FindLastIndex(line => string.Equals(line, pattern, StringComparison.Ordinal));
        return last >= 0 && !lines.Skip(last + 1).Any(line => line.StartsWith('!'));
    }
}

/// <summary>
/// The lines "Ignore &lt;file name&gt;" and "Ignore all *.&lt;ext&gt; files" add to <c>.gitignore</c>
/// (docs/phase-3-design.md 2.30): git's pattern characters are escaped, so that the line means
/// that file, or that extension, and nothing else.
/// </summary>
public static class GitignorePattern
{
    /// <summary>
    /// <c>/&lt;path&gt;</c> for a file or folder <paramref name="repositoryPath"/> (relative to the
    /// working tree, with <c>/</c>): anchored at the top, so it matches that path alone.
    /// <c>drafts/[wip].txt</c> gives <c>/drafts/\[wip].txt</c>.
    /// </summary>
    public static string ForPath(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(repositoryPath);
        return Escape("/" + repositoryPath.TrimStart('/'));
    }

    /// <summary><c>*.&lt;ext&gt;</c> for <paramref name="extension"/>, given without its dot (<c>log</c> gives <c>*.log</c>).</summary>
    public static string ForExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrEmpty(extension);
        return "*." + Escape(extension.TrimStart('.'));
    }

    /// <summary>
    /// Puts <c>\</c> before <c>*</c>, <c>?</c>, <c>[</c> and <c>\</c>, before a leading <c>#</c>
    /// or <c>!</c>, and before each trailing space, which git would otherwise drop.
    /// </summary>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal))
        {
            throw new ArgumentException("A .gitignore pattern is one line.", nameof(text));
        }

        var trailingSpaces = text.Length - text.TrimEnd(' ').Length;
        var result = new StringBuilder(text.Length + 4);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var escape = c is '*' or '?' or '[' or '\\'
                || (i == 0 && c is '#' or '!')
                || (c == ' ' && i >= text.Length - trailingSpaces);
            if (escape)
            {
                result.Append('\\');
            }

            result.Append(c);
        }

        return result.ToString();
    }
}
