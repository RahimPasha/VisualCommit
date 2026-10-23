using System.Globalization;
using VisualCommit.Core.Settings;

namespace VisualCommit.App.Services;

/// <summary>The platforms the editor catalogue knows (docs/phase-6-design.md 12.2).</summary>
public enum EditorPlatform
{
    Windows,
    MacOS,
    Linux,
}

/// <summary>
/// What the editor catalogue looks at (docs/phase-6-design.md 12.3), as <c>GitSearchContext</c> is
/// for git: the platform, its folders and environment, and the file system as functions, so that a
/// test gives it a fake file system. Nothing here is a registry, and on Windows nothing is
/// <c>PATH</c> (0.9: it holds <c>.cmd</c> shims there, and a name can be another program).
/// </summary>
/// <param name="Platform">Whose candidates are looked at.</param>
/// <param name="LocalAppData">Windows' <c>%LOCALAPPDATA%</c>, or null.</param>
/// <param name="ProgramFiles">Windows' <c>%ProgramFiles%</c> (the 64-bit one on a 64-bit system), or null.</param>
/// <param name="ProgramFilesX86">Windows' <c>%ProgramFiles(x86)%</c>, or null.</param>
/// <param name="SystemRoot">Windows' <c>%SystemRoot%</c>, or null.</param>
/// <param name="Home">The user's home folder, or null.</param>
/// <param name="PathVariable">The <c>PATH</c> value Linux looks at for command names; ignored elsewhere.</param>
/// <param name="FileExists">Whether a file exists.</param>
/// <param name="DirectoryExists">Whether a folder exists.</param>
/// <param name="ListFolders">The full paths of the folders directly inside a folder; empty when it does not exist.</param>
public sealed record EditorSearchContext(
    EditorPlatform Platform,
    string? LocalAppData,
    string? ProgramFiles,
    string? ProgramFilesX86,
    string? SystemRoot,
    string? Home,
    string? PathVariable,
    Func<string, bool> FileExists,
    Func<string, bool> DirectoryExists,
    Func<string, IReadOnlyList<string>> ListFolders)
{
    /// <summary>The platform this process runs on; any other Unix counts as Linux.</summary>
    public static EditorPlatform CurrentPlatform =>
        OperatingSystem.IsWindows() ? EditorPlatform.Windows
        : OperatingSystem.IsMacOS() ? EditorPlatform.MacOS
        : EditorPlatform.Linux;

    /// <summary>The machine's own folders and file system.</summary>
    public static EditorSearchContext FromSystem() =>
        new(
            CurrentPlatform,
            NullIfEmpty(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            NullIfEmpty(Environment.GetEnvironmentVariable("ProgramW6432")) ?? NullIfEmpty(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
            NullIfEmpty(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)),
            NullIfEmpty(Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
            NullIfEmpty(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            Environment.GetEnvironmentVariable("PATH"),
            File.Exists,
            Directory.Exists,
            ListFoldersOnDisk);

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static IReadOnlyList<string> ListFoldersOnDisk(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.GetDirectories(folder) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

/// <summary>
/// One editor of the catalogue (docs/phase-6-design.md 12.2): its id, its name, where its program
/// may be on a platform, and its arguments without and with a line.
/// </summary>
/// <param name="Id">Its id, stored in <c>settings.json</c> (<c>vscode</c>).</param>
/// <param name="Name">The name the settings screen shows (<c>Visual Studio Code</c>).</param>
/// <param name="Arguments">Its arguments, words split at spaces, <c>{p}</c> the file's absolute path (<c>/Edit {p}</c>).</param>
/// <param name="LineArguments">Its arguments with a line, <c>{n}</c> the line (<c>--goto {p}:{n}</c>); null when it takes none.</param>
public sealed record KnownEditor(string Id, string Name, string Arguments, string? LineArguments)
{
    /// <summary>Where its program may be, first that exists wins: absolute paths built from the context's folders.</summary>
    internal Func<EditorSearchContext, IEnumerable<string>> Candidates { get; init; } = _ => [];
}

/// <summary>
/// The editors "Open in editor" knows (docs/phase-6-design.md 12.2): static data per platform,
/// never from the working directory, nor from <c>PATH</c> on Windows. On Windows only <c>.exe</c>
/// programs, never a <c>.cmd</c> or <c>.bat</c>, which <c>CreateProcess</c> would hand to
/// <c>cmd.exe</c> (0.9). macOS and Linux have not been checked on a machine (12.2).
/// </summary>
public static class KnownEditors
{
    /// <summary>The id of an editor chosen through "Other…" (12.5, 12.6).</summary>
    public const string CustomId = "custom";

    private const string CodeArguments = "{p}";
    private const string CodeLine = "--goto {p}:{n}";
    private const string JetBrainsLine = "--line {n} {p}";

    /// <summary>The JetBrains products (12.2): id, name, the start of their Windows folders' names, their program's name.</summary>
    private static readonly (string Id, string Name, string WindowsFolder, string Program, string[] MacBundles)[] JetBrains =
    [
        ("idea", "IntelliJ IDEA", "IntelliJ IDEA", "idea", ["IntelliJ IDEA.app", "IntelliJ IDEA CE.app"]),
        ("rider", "Rider", "JetBrains Rider", "rider", ["Rider.app"]),
        ("pycharm", "PyCharm", "PyCharm", "pycharm", ["PyCharm.app", "PyCharm CE.app"]),
        ("webstorm", "WebStorm", "WebStorm", "webstorm", ["WebStorm.app"]),
        ("clion", "CLion", "CLion", "clion", ["CLion.app"]),
        ("goland", "GoLand", "GoLand", "goland", ["GoLand.app"]),
        ("phpstorm", "PhpStorm", "PhpStorm", "phpstorm", ["PhpStorm.app"]),
        ("rubymine", "RubyMine", "RubyMine", "rubymine", ["RubyMine.app"]),
        ("rustrover", "RustRover", "RustRover", "rustrover", ["RustRover.app"]),
    ];

    private static readonly IReadOnlyList<KnownEditor> WindowsEditors = BuildWindows();

    private static readonly IReadOnlyList<KnownEditor> MacEditors = BuildMac();

    private static readonly IReadOnlyList<KnownEditor> LinuxEditors = BuildLinux();

    /// <summary>The editors of <paramref name="platform"/>, in the order the settings screen lists those found.</summary>
    public static IReadOnlyList<KnownEditor> For(EditorPlatform platform) => platform switch
    {
        EditorPlatform.Windows => WindowsEditors,
        EditorPlatform.MacOS => MacEditors,
        _ => LinuxEditors,
    };

    /// <summary>The editor <paramref name="id"/> of <paramref name="platform"/>, or null (<see cref="CustomId"/> included).</summary>
    public static KnownEditor? Find(string id, EditorPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(id);
        return For(platform).FirstOrDefault(editor => string.Equals(editor.Id, id, StringComparison.Ordinal));
    }

    /// <summary>The editor <paramref name="id"/> of the platform this process runs on, or null.</summary>
    public static KnownEditor? Find(string id) => Find(id, EditorSearchContext.CurrentPlatform);

    /// <summary>
    /// Whether <paramref name="editor"/> (the settings' <c>Editor</c>) chooses an editor (12.6): Other,
    /// or an id this platform's catalogue knows; null, or an id it no longer knows, chooses none. The
    /// file menus' "Open in editor" and the palette's "Choose an editor…" (12.1) go by it.
    /// </summary>
    public static bool IsChosen(EditorSetting? editor) =>
        editor is not null && (editor.Id == CustomId || Find(editor.Id) is not null);

    /// <summary>Where <paramref name="editor"/>'s program may be, in the order they are tried, as absolute paths.</summary>
    public static IReadOnlyList<string> Candidates(KnownEditor editor, EditorSearchContext context)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(context);
        return [.. editor.Candidates(context)];
    }

    /// <summary>The first of <paramref name="editor"/>'s candidates that exists, or null.</summary>
    public static string? FindProgram(KnownEditor editor, EditorSearchContext context)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(context);
        return editor.Candidates(context).FirstOrDefault(context.FileExists);
    }

    private static List<KnownEditor> BuildWindows()
    {
        static IEnumerable<string> Under(string? root, params string[] parts) =>
            root is null ? [] : [WindowsJoin(root, parts)];

        var editors = new List<KnownEditor>
        {
            new("vscode", "Visual Studio Code", CodeArguments, CodeLine)
            {
                Candidates = c => [.. Under(c.LocalAppData, "Programs", "Microsoft VS Code", "Code.exe"), .. Under(c.ProgramFiles, "Microsoft VS Code", "Code.exe")],
            },
            new("vscode-insiders", "VS Code Insiders", CodeArguments, CodeLine)
            {
                Candidates = c =>
                [
                    .. Under(c.LocalAppData, "Programs", "Microsoft VS Code Insiders", "Code - Insiders.exe"),
                    .. Under(c.ProgramFiles, "Microsoft VS Code Insiders", "Code - Insiders.exe"),
                ],
            },
            new("cursor", "Cursor", CodeArguments, CodeLine)
            {
                Candidates = c => Under(c.LocalAppData, "Programs", "cursor", "Cursor.exe"),
            },
            new("vscodium", "VSCodium", CodeArguments, CodeLine)
            {
                Candidates = c => [.. Under(c.LocalAppData, "Programs", "VSCodium", "VSCodium.exe"), .. Under(c.ProgramFiles, "VSCodium", "VSCodium.exe")],
            },
            new("sublime", "Sublime Text", "{p}", "{p}:{n}")
            {
                Candidates = c => [.. Under(c.ProgramFiles, "Sublime Text", "subl.exe"), .. Under(c.ProgramFiles, "Sublime Text 3", "subl.exe")],
            },
            new("notepadpp", "Notepad++", "{p}", "-n{n} {p}")
            {
                Candidates = c => [.. Under(c.ProgramFiles, "Notepad++", "notepad++.exe"), .. Under(c.ProgramFilesX86, "Notepad++", "notepad++.exe")],
            },
        };

        foreach (var product in JetBrains)
        {
            editors.Add(new KnownEditor(product.Id, product.Name, "{p}", JetBrainsLine)
            {
                Candidates = c => c.ProgramFiles is null
                    ? []
                    : NewestFirst(c.ListFolders(WindowsJoin(c.ProgramFiles, "JetBrains"))
                            .Where(folder => LastSegment(folder).StartsWith(product.WindowsFolder, StringComparison.OrdinalIgnoreCase)))
                        .Select(folder => WindowsJoin(folder, "bin", product.Program + "64.exe")),
            });
        }

        editors.Add(new KnownEditor("visualstudio", "Visual Studio", "/Edit {p}", null)
        {
            Candidates = c => c.ProgramFiles is null
                ? []
                : from version in new[] { "2022", "18" }
                  from edition in new[] { "Enterprise", "Professional", "Community" }
                  select WindowsJoin(c.ProgramFiles, "Microsoft Visual Studio", version, edition, "Common7", "IDE", "devenv.exe"),
        });
        editors.Add(new KnownEditor("gvim", "gVim", "{p}", "+{n} {p}")
        {
            Candidates = c => c.ProgramFiles is null
                ? []
                : NewestFirst(c.ListFolders(WindowsJoin(c.ProgramFiles, "Vim"))
                        .Where(folder => LastSegment(folder).StartsWith("vim", StringComparison.OrdinalIgnoreCase)))
                    .Select(folder => WindowsJoin(folder, "gvim.exe")),
        });
        editors.Add(new KnownEditor("notepad", "Notepad", "{p}", null)
        {
            Candidates = c => Under(c.SystemRoot, "System32", "notepad.exe"),
        });
        return editors;
    }

    private static List<KnownEditor> BuildMac()
    {
        static IEnumerable<string> InApplications(EditorSearchContext c, string bundlePath)
        {
            yield return "/Applications/" + bundlePath;
            if (c.Home is not null)
            {
                yield return UnixJoin(c.Home, "Applications", bundlePath);
            }
        }

        var editors = new List<KnownEditor>
        {
            new("vscode", "Visual Studio Code", CodeArguments, CodeLine)
            {
                Candidates = c => InApplications(c, "Visual Studio Code.app/Contents/Resources/app/bin/code"),
            },
            new("vscode-insiders", "VS Code Insiders", CodeArguments, CodeLine)
            {
                Candidates = c => InApplications(c, "Visual Studio Code - Insiders.app/Contents/Resources/app/bin/code-insiders"),
            },
            new("cursor", "Cursor", CodeArguments, CodeLine)
            {
                Candidates = c => InApplications(c, "Cursor.app/Contents/Resources/app/bin/cursor"),
            },
            new("vscodium", "VSCodium", CodeArguments, CodeLine)
            {
                Candidates = c => InApplications(c, "VSCodium.app/Contents/Resources/app/bin/codium"),
            },
            new("sublime", "Sublime Text", "{p}", "{p}:{n}")
            {
                Candidates = c => InApplications(c, "Sublime Text.app/Contents/SharedSupport/bin/subl"),
            },
        };

        foreach (var product in JetBrains)
        {
            editors.Add(new KnownEditor(product.Id, product.Name, "{p}", JetBrainsLine)
            {
                Candidates = c => product.MacBundles.SelectMany(bundle => InApplications(c, $"{bundle}/Contents/MacOS/{product.Program}")),
            });
        }

        editors.Add(new KnownEditor("bbedit", "BBEdit", "{p}", "+{n} {p}") { Candidates = _ => ["/usr/local/bin/bbedit"] });
        editors.Add(new KnownEditor("xcode", "Xcode", "{p}", "--line {n} {p}")
        {
            // The stub xed without Xcode is not offered (12.2).
            Candidates = c => c.DirectoryExists("/Applications/Xcode.app") ? ["/usr/bin/xed"] : [],
        });
        editors.Add(new KnownEditor("textedit", "TextEdit", "-a TextEdit {p}", null) { Candidates = _ => ["/usr/bin/open"] });
        return editors;
    }

    private static List<KnownEditor> BuildLinux()
    {
        // FileLauncher.FindOnPath's rule (D130) for Linux paths on any machine: only absolute PATH
        // folders, never an empty or relative entry, which would name the current folder.
        static IEnumerable<string> OnPath(EditorSearchContext c, string command) =>
            (c.PathVariable ?? string.Empty)
                .Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(folder => folder.StartsWith('/'))
                .Select(folder => UnixJoin(folder, command))
                .Where(c.FileExists)
                .Take(1);

        var editors = new List<KnownEditor>
        {
            new("vscode", "Visual Studio Code", CodeArguments, CodeLine)
            {
                Candidates = c => ["/usr/bin/code", "/usr/share/code/bin/code", "/snap/bin/code", .. OnPath(c, "code")],
            },
            new("vscode-insiders", "VS Code Insiders", CodeArguments, CodeLine)
            {
                Candidates = c => ["/usr/bin/code-insiders", .. OnPath(c, "code-insiders")],
            },
            new("vscodium", "VSCodium", CodeArguments, CodeLine)
            {
                Candidates = c => ["/usr/bin/codium", .. OnPath(c, "codium")],
            },
            new("sublime", "Sublime Text", "{p}", "{p}:{n}")
            {
                Candidates = _ => ["/usr/bin/subl", "/opt/sublime_text/sublime_text"],
            },
        };

        foreach (var product in JetBrains)
        {
            editors.Add(new KnownEditor(product.Id, product.Name, "{p}", JetBrainsLine)
            {
                Candidates = c =>
                [
                    .. c.Home is null ? [] : new[] { UnixJoin(c.Home, ".local", "share", "JetBrains", "Toolbox", "scripts", product.Program) },
                    "/snap/bin/" + product.Program,
                ],
            });
        }

        editors.Add(new KnownEditor("gvim", "gVim", "{p}", "+{n} {p}") { Candidates = _ => ["/usr/bin/gvim"] });
        editors.Add(new KnownEditor("gedit", "gedit", "{p}", "+{n} {p}") { Candidates = _ => ["/usr/bin/gedit"] });
        editors.Add(new KnownEditor("gnome-text-editor", "GNOME Text Editor", "{p}", null) { Candidates = _ => ["/usr/bin/gnome-text-editor"] });
        editors.Add(new KnownEditor("kate", "Kate", "{p}", "--line {n} {p}") { Candidates = _ => ["/usr/bin/kate"] });
        editors.Add(new KnownEditor("kwrite", "KWrite", "{p}", "--line {n} {p}") { Candidates = _ => ["/usr/bin/kwrite"] });
        return editors;
    }

    /// <summary>
    /// Folders newest version first: by the numbers in their names (<c>IntelliJ IDEA 2023.1</c>
    /// before <c>IntelliJ IDEA Community Edition 2020.3.2</c>, <c>vim91</c> before <c>vim82</c>),
    /// then by name; a name without a number last.
    /// </summary>
    private static IEnumerable<string> NewestFirst(IEnumerable<string> folders) =>
        folders
            .Select(folder => (Folder: folder, Version: VersionOf(LastSegment(folder))))
            .OrderByDescending(item => item.Version, VersionComparer.Instance)
            .ThenBy(item => LastSegment(item.Folder), StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Folder);

    /// <summary>A Windows path: <paramref name="root"/> and <paramref name="parts"/> joined by backslashes, whatever platform builds it (the tests build every platform's).</summary>
    private static string WindowsJoin(string root, params string[] parts) =>
        string.Join('\\', [root.TrimEnd('\\', '/'), .. parts]);

    /// <summary>A macOS or Linux path: <paramref name="root"/> and <paramref name="parts"/> joined by slashes.</summary>
    private static string UnixJoin(string root, params string[] parts) =>
        string.Join('/', [root.TrimEnd('/'), .. parts]);

    /// <summary>The last part of a path of either platform's separators.</summary>
    private static string LastSegment(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        return trimmed[(trimmed.LastIndexOfAny(['\\', '/']) + 1)..];
    }

    /// <summary>The numbers of the last run of digits and dots in <paramref name="name"/> (<c>2020.3.2</c> → 2020, 3, 2); empty without one.</summary>
    private static int[] VersionOf(string name)
    {
        var end = name.Length;
        while (end > 0 && !char.IsAsciiDigit(name[end - 1]))
        {
            end--;
        }

        var start = end;
        while (start > 0 && (char.IsAsciiDigit(name[start - 1]) || name[start - 1] == '.'))
        {
            start--;
        }

        return name[start..end]
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0)
            .ToArray();
    }

    private sealed class VersionComparer : IComparer<int[]>
    {
        public static VersionComparer Instance { get; } = new();

        public int Compare(int[]? x, int[]? y)
        {
            x ??= [];
            y ??= [];
            for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                var a = i < x.Length ? x[i] : -1;
                var b = i < y.Length ? y[i] : -1;
                if (a != b)
                {
                    return a.CompareTo(b);
                }
            }

            return 0;
        }
    }
}
