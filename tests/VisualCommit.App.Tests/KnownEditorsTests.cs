using VisualCommit.App.Services;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The editor catalogue's candidates (docs/phase-6-design.md 12.2, 12.10), per platform, from a
/// fake file system: every platform's are built on any machine; nothing is started.
/// </summary>
public class KnownEditorsTests
{
    private const string LocalAppData = @"C:\Users\Ann\AppData\Local";
    private const string ProgramFiles = @"C:\Program Files";
    private const string ProgramFilesX86 = @"C:\Program Files (x86)";

    [Fact]
    public void Windows_lists_the_editors_of_12_2_in_order_with_their_candidates()
    {
        var context = Windows(folders: new Dictionary<string, string[]>());

        Assert.Equal(
            ["vscode", "vscode-insiders", "cursor", "vscodium", "sublime", "notepadpp", "idea", "rider", "pycharm", "webstorm", "clion", "goland", "phpstorm", "rubymine", "rustrover", "visualstudio", "gvim", "notepad"],
            KnownEditors.For(EditorPlatform.Windows).Select(editor => editor.Id));
        Assert.Equal(
            [$@"{LocalAppData}\Programs\Microsoft VS Code\Code.exe", $@"{ProgramFiles}\Microsoft VS Code\Code.exe"],
            Candidates("vscode", context));
        Assert.Equal(
            [$@"{LocalAppData}\Programs\Microsoft VS Code Insiders\Code - Insiders.exe", $@"{ProgramFiles}\Microsoft VS Code Insiders\Code - Insiders.exe"],
            Candidates("vscode-insiders", context));
        Assert.Equal([$@"{LocalAppData}\Programs\cursor\Cursor.exe"], Candidates("cursor", context));
        Assert.Equal([$@"{LocalAppData}\Programs\VSCodium\VSCodium.exe", $@"{ProgramFiles}\VSCodium\VSCodium.exe"], Candidates("vscodium", context));
        Assert.Equal([$@"{ProgramFiles}\Sublime Text\subl.exe", $@"{ProgramFiles}\Sublime Text 3\subl.exe"], Candidates("sublime", context));
        Assert.Equal([$@"{ProgramFiles}\Notepad++\notepad++.exe", $@"{ProgramFilesX86}\Notepad++\notepad++.exe"], Candidates("notepadpp", context));
        Assert.Equal([@"C:\Windows\System32\notepad.exe"], Candidates("notepad", context));
    }

    [Fact]
    public void No_windows_candidate_is_a_cmd_or_bat_file()
    {
        // 12.2: CreateProcess would hand them to cmd.exe, which parses the arguments (0.9).
        var context = Windows(new Dictionary<string, string[]>
        {
            [$@"{ProgramFiles}\JetBrains"] = [$@"{ProgramFiles}\JetBrains\IntelliJ IDEA 2023.1", $@"{ProgramFiles}\JetBrains\JetBrains Rider 2024.2"],
            [$@"{ProgramFiles}\Vim"] = [$@"{ProgramFiles}\Vim\vim91"],
        });

        var all = KnownEditors.For(EditorPlatform.Windows).SelectMany(editor => KnownEditors.Candidates(editor, context)).ToList();

        Assert.NotEmpty(all);
        Assert.All(all, candidate => Assert.EndsWith(".exe", candidate, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void JetBrains_folders_are_tried_newest_first_by_the_programs_exact_name()
    {
        // 0.9: fsnotifier64.exe and runnerw64.exe lie beside idea64.exe, so *64.exe is no rule.
        var root = $@"{ProgramFiles}\JetBrains";
        var context = Windows(
            new Dictionary<string, string[]>
            {
                [root] =
                [
                    $@"{root}\IntelliJ IDEA Community Edition 2020.3.2",
                    $@"{root}\IntelliJ IDEA 2023.1",
                    $@"{root}\PyCharm 2022.3",
                    $@"{root}\JetBrains Rider 2024.2.1",
                ],
            },
            files:
            [
                $@"{root}\IntelliJ IDEA Community Edition 2020.3.2\bin\idea64.exe",
                $@"{root}\IntelliJ IDEA Community Edition 2020.3.2\bin\fsnotifier64.exe",
                $@"{root}\IntelliJ IDEA Community Edition 2020.3.2\bin\runnerw64.exe",
            ]);

        Assert.Equal(
            [$@"{root}\IntelliJ IDEA 2023.1\bin\idea64.exe", $@"{root}\IntelliJ IDEA Community Edition 2020.3.2\bin\idea64.exe"],
            Candidates("idea", context));
        Assert.Equal([$@"{root}\JetBrains Rider 2024.2.1\bin\rider64.exe"], Candidates("rider", context));
        Assert.Equal([$@"{root}\PyCharm 2022.3\bin\pycharm64.exe"], Candidates("pycharm", context));
        Assert.Empty(Candidates("webstorm", context));

        // The newer folder lacks the program: the older one's is found, never a neighbour program.
        Assert.Equal($@"{root}\IntelliJ IDEA Community Edition 2020.3.2\bin\idea64.exe", KnownEditors.FindProgram(KnownEditors.Find("idea", EditorPlatform.Windows)!, context));
    }

    [Fact]
    public void Visual_studio_is_tried_2022_then_18_by_edition_and_gvim_newest_first()
    {
        var context = Windows(new Dictionary<string, string[]>
        {
            [$@"{ProgramFiles}\Vim"] = [$@"{ProgramFiles}\Vim\vim82", $@"{ProgramFiles}\Vim\vim91", $@"{ProgramFiles}\Vim\vimfiles"],
        });

        Assert.Equal(
            [
                $@"{ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe",
                $@"{ProgramFiles}\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe",
                $@"{ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe",
                $@"{ProgramFiles}\Microsoft Visual Studio\18\Enterprise\Common7\IDE\devenv.exe",
                $@"{ProgramFiles}\Microsoft Visual Studio\18\Professional\Common7\IDE\devenv.exe",
                $@"{ProgramFiles}\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe",
            ],
            Candidates("visualstudio", context));
        Assert.Equal(
            [$@"{ProgramFiles}\Vim\vim91\gvim.exe", $@"{ProgramFiles}\Vim\vim82\gvim.exe", $@"{ProgramFiles}\Vim\vimfiles\gvim.exe"],
            Candidates("gvim", context));
    }

    [Fact]
    public void The_arguments_are_12_2s_with_and_without_a_line()
    {
        Assert.Equal(("{p}", "--goto {p}:{n}"), Arguments("vscode"));
        Assert.Equal(("{p}", "--goto {p}:{n}"), Arguments("cursor"));
        Assert.Equal(("{p}", "{p}:{n}"), Arguments("sublime"));
        Assert.Equal(("{p}", "-n{n} {p}"), Arguments("notepadpp"));
        Assert.Equal(("{p}", "--line {n} {p}"), Arguments("idea"));
        Assert.Equal(("/Edit {p}", null), Arguments("visualstudio"));
        Assert.Equal(("{p}", "+{n} {p}"), Arguments("gvim"));
        Assert.Equal(("{p}", null), Arguments("notepad"));
        Assert.Null(KnownEditors.Find(KnownEditors.CustomId, EditorPlatform.Windows));
    }

    [Fact]
    public void MacOS_looks_in_both_applications_folders_and_offers_xed_only_with_xcode()
    {
        var context = Unix(EditorPlatform.MacOS, "/Users/ann", directories: []);

        Assert.Equal(
            ["/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code", "/Users/ann/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code"],
            Candidates("vscode", context, EditorPlatform.MacOS));
        Assert.Equal(
            [
                "/Applications/IntelliJ IDEA.app/Contents/MacOS/idea", "/Users/ann/Applications/IntelliJ IDEA.app/Contents/MacOS/idea",
                "/Applications/IntelliJ IDEA CE.app/Contents/MacOS/idea", "/Users/ann/Applications/IntelliJ IDEA CE.app/Contents/MacOS/idea",
            ],
            Candidates("idea", context, EditorPlatform.MacOS));
        Assert.Empty(Candidates("xcode", context, EditorPlatform.MacOS));
        Assert.Equal(["/usr/bin/xed"], Candidates("xcode", Unix(EditorPlatform.MacOS, "/Users/ann", directories: ["/Applications/Xcode.app"]), EditorPlatform.MacOS));
        Assert.Equal(["/usr/bin/open"], Candidates("textedit", context, EditorPlatform.MacOS));
        Assert.Equal("-a TextEdit {p}", KnownEditors.Find("textedit", EditorPlatform.MacOS)!.Arguments);
    }

    [Fact]
    public void Linux_tries_the_fixed_paths_then_path_folders_that_are_fully_qualified()
    {
        var context = Unix(EditorPlatform.Linux, "/home/ann", directories: [], path: "relative:/opt/bin", files: ["/opt/bin/code", "relative/code"]);

        Assert.Equal(["/usr/bin/code", "/usr/share/code/bin/code", "/snap/bin/code", "/opt/bin/code"], Candidates("vscode", context, EditorPlatform.Linux));
        Assert.Equal(["/home/ann/.local/share/JetBrains/Toolbox/scripts/rider", "/snap/bin/rider"], Candidates("rider", context, EditorPlatform.Linux));
        Assert.Equal(["/usr/bin/gedit"], Candidates("gedit", context, EditorPlatform.Linux));
        Assert.Equal(("{p}", "--line {n} {p}"), (KnownEditors.Find("kate", EditorPlatform.Linux)!.Arguments, KnownEditors.Find("kate", EditorPlatform.Linux)!.LineArguments));
    }

    private static (string Arguments, string? LineArguments) Arguments(string id)
    {
        var editor = KnownEditors.Find(id, EditorPlatform.Windows)!;
        return (editor.Arguments, editor.LineArguments);
    }

    private static IReadOnlyList<string> Candidates(string id, EditorSearchContext context, EditorPlatform platform = EditorPlatform.Windows) =>
        KnownEditors.Candidates(KnownEditors.Find(id, platform)!, context);

    private static EditorSearchContext Windows(IReadOnlyDictionary<string, string[]> folders, string[]? files = null)
    {
        var existing = new HashSet<string>(files ?? [], StringComparer.OrdinalIgnoreCase);
        return new EditorSearchContext(
            EditorPlatform.Windows,
            LocalAppData,
            ProgramFiles,
            ProgramFilesX86,
            @"C:\Windows",
            @"C:\Users\Ann",
            PathVariable: @"C:\Windows\System32",
            existing.Contains,
            _ => false,
            folder => folders.TryGetValue(folder, out var children) ? children : []);
    }

    private static EditorSearchContext Unix(EditorPlatform platform, string home, string[] directories, string? path = null, string[]? files = null)
    {
        var existing = new HashSet<string>(files ?? [], StringComparer.Ordinal);
        var folders = new HashSet<string>(directories, StringComparer.Ordinal);
        return new EditorSearchContext(platform, null, null, null, null, home, path, existing.Contains, folders.Contains, _ => []);
    }
}
