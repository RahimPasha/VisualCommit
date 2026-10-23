using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.Services;

/// <summary>
/// How "Open in editor" starts the chosen editor (docs/phase-6-design.md 12.4), pure, tested as
/// <see cref="FileLauncher.RevealCommands"/> is: the program by its absolute path (on Windows only
/// an <c>.exe</c> or a <c>.com</c>), the arguments as a list with no shell anywhere, the file as its
/// absolute path, the app's environment without <c>ELECTRON_RUN_AS_NODE</c>, and the program's
/// folder (Windows) or the user's home (elsewhere) as the working directory, never the repository.
/// </summary>
public static class EditorCommands
{
    /// <summary>The variable that would make <c>Code.exe</c> and <c>Cursor.exe</c> run the file as a Node script (12.4).</summary>
    public const string ElectronRunAsNode = "ELECTRON_RUN_AS_NODE";

    /// <summary>The line "Open in editor" opens a file at while no menu knows one (12.1, 12.5).</summary>
    public const int FirstLine = 1;

    /// <summary>The validation text of a quote that is not closed (12.5).</summary>
    public const string UnclosedQuote = "A quote in the arguments is not closed.";

    /// <summary>The command that opens <paramref name="fullPath"/> in <paramref name="editor"/> on this platform.</summary>
    /// <param name="editor">The editor as stored.</param>
    /// <param name="known">The catalogue's entry of a known editor, whose arguments win over anything stored; null for Other (<see cref="KnownEditors.CustomId"/>).</param>
    /// <param name="fullPath">The file's absolute path.</param>
    /// <param name="line">The line, or null for none (phase 6 opens a file at its top, 12.1).</param>
    public static LaunchCommand Build(EditorSetting editor, KnownEditor? known, string fullPath, int? line) =>
        Build(editor, known, fullPath, line, EditorSearchContext.CurrentPlatform, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>
    /// <see cref="Build(EditorSetting, KnownEditor?, string, int?)"/> for <paramref name="platform"/>,
    /// whose working directory off Windows is <paramref name="home"/>.
    /// </summary>
    /// <exception cref="EditorCommandException">The program is not absolute, on Windows is not an <c>.exe</c> or <c>.com</c>, or Other's arguments have a quote that is not closed; its message is the reason, as the error dialog shows it (12.7).</exception>
    public static LaunchCommand Build(EditorSetting editor, KnownEditor? known, string fullPath, int? line, EditorPlatform platform, string? home)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        var program = editor.Program;
        if (!IsAbsolute(program, platform))
        {
            throw new EditorCommandException($"The editor's program {program} is not an absolute path.");
        }

        if (platform == EditorPlatform.Windows && !IsWindowsProgram(program))
        {
            // CreateProcess hands a .cmd or .bat to cmd.exe, which parses the arguments (0.9).
            throw new EditorCommandException($"{program} is not an .exe or .com program.");
        }

        var arguments = known is not null
            ? Fill(SplitTemplate(line is not null && known.LineArguments is not null ? known.LineArguments : known.Arguments), fullPath, line, "{p}", "{n}")
            : FillOther(editor.Arguments ?? "{path}", fullPath, line);

        var workingDirectory = platform == EditorPlatform.Windows
            ? WindowsFolderOf(program)
            : string.IsNullOrEmpty(home) ? null : home;
        return new LaunchCommand(program, arguments)
        {
            WorkingDirectory = workingDirectory,
            RemovedVariables = [ElectronRunAsNode],
        };
    }

    /// <summary>
    /// Other's arguments (12.5): split at whitespace outside double quotes, a quoted part one
    /// argument without its quotes, no other escaping; null when a quote is not closed.
    /// </summary>
    public static IReadOnlyList<string>? SplitArguments(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var arguments = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var started = false;
        foreach (var c in template)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                started = true;
            }
            else if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (started)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
            }
            else
            {
                current.Append(c);
                started = true;
            }
        }

        if (inQuotes)
        {
            return null;
        }

        if (started)
        {
            arguments.Add(current.ToString());
        }

        return arguments;
    }

    /// <summary>Whether a Windows program may be started directly: an <c>.exe</c> or a <c>.com</c> (12.4).</summary>
    public static bool IsWindowsProgram(string program)
    {
        ArgumentNullException.ThrowIfNull(program);
        var extension = ExtensionOf(program);
        return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".com", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> FillOther(string template, string fullPath, int? line)
    {
        var parts = SplitArguments(template) ?? throw new EditorCommandException(UnclosedQuote);
        var filled = Fill(parts, fullPath, line, "{path}", "{line}").ToList();

        // Without {path}, the path comes last.
        if (!parts.Any(part => part.Contains("{path}", StringComparison.Ordinal)))
        {
            filled.Add(fullPath);
        }

        return filled;
    }

    private static List<string> SplitTemplate(string template) =>
        [.. template.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>
    /// Puts the file's path and the line into each part, both tokens in one pass, so that a path
    /// holding the other token (a folder named <c>{line}</c>) is put in as it is.
    /// </summary>
    private static IReadOnlyList<string> Fill(IEnumerable<string> parts, string fullPath, int? line, string pathToken, string lineToken)
    {
        var lineText = (line ?? FirstLine).ToString(CultureInfo.InvariantCulture);
        var tokens = new Regex($"{Regex.Escape(pathToken)}|{Regex.Escape(lineToken)}", RegexOptions.CultureInvariant);
        return [.. parts.Select(part => tokens.Replace(part, match => match.Value == pathToken ? fullPath : lineText))];
    }

    private static bool IsAbsolute(string program, EditorPlatform platform) =>
        platform == EditorPlatform.Windows
            ? (program.Length >= 3 && char.IsAsciiLetter(program[0]) && program[1] == ':' && program[2] is '\\' or '/')
              || program.StartsWith(@"\\", StringComparison.Ordinal)
            : program.StartsWith('/');

    private static string? WindowsFolderOf(string program)
    {
        var cut = program.LastIndexOfAny(['\\', '/']);
        if (cut <= 0)
        {
            return null;
        }

        // "C:\Ed.exe" → "C:\".
        return program[cut - 1] == ':' ? program[..(cut + 1)] : program[..cut];
    }

    private static string ExtensionOf(string program)
    {
        var name = program[(program.LastIndexOfAny(['\\', '/']) + 1)..];
        var dot = name.LastIndexOf('.');
        return dot < 0 ? string.Empty : name[dot..];
    }
}

/// <summary>Why an editor's command could not be built (docs/phase-6-design.md 12.4, 12.5): its message is the reason the error dialog shows.</summary>
public sealed class EditorCommandException(string message) : Exception(message);
