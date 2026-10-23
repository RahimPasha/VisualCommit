using VisualCommit.App.Services;
using VisualCommit.Core.Settings;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// How "Open in editor" builds its command (docs/phase-6-design.md 12.4, 12.5, 12.10), pure:
/// every Windows entry of the catalogue, Other's arguments, the working directory and the removed
/// variable. Nothing is started.
/// </summary>
public class EditorCommandsTests
{
    private const string File = @"C:\src\web-app\Program.cs";

    [Theory]
    [InlineData("vscode", @"C:\Users\Ann\AppData\Local\Programs\Microsoft VS Code\Code.exe", new[] { File }, @"C:\Users\Ann\AppData\Local\Programs\Microsoft VS Code")]
    [InlineData("vscode-insiders", @"C:\Program Files\Microsoft VS Code Insiders\Code - Insiders.exe", new[] { File }, @"C:\Program Files\Microsoft VS Code Insiders")]
    [InlineData("cursor", @"C:\Users\Ann\AppData\Local\Programs\cursor\Cursor.exe", new[] { File }, @"C:\Users\Ann\AppData\Local\Programs\cursor")]
    [InlineData("vscodium", @"C:\Program Files\VSCodium\VSCodium.exe", new[] { File }, @"C:\Program Files\VSCodium")]
    [InlineData("sublime", @"C:\Program Files\Sublime Text\subl.exe", new[] { File }, @"C:\Program Files\Sublime Text")]
    [InlineData("notepadpp", @"C:\Program Files (x86)\Notepad++\notepad++.exe", new[] { File }, @"C:\Program Files (x86)\Notepad++")]
    [InlineData("idea", @"C:\Program Files\JetBrains\IntelliJ IDEA 2023.1\bin\idea64.exe", new[] { File }, @"C:\Program Files\JetBrains\IntelliJ IDEA 2023.1\bin")]
    [InlineData("visualstudio", @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe", new[] { "/Edit", File }, @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE")]
    [InlineData("gvim", @"C:\Program Files\Vim\vim91\gvim.exe", new[] { File }, @"C:\Program Files\Vim\vim91")]
    [InlineData("notepad", @"C:\Windows\System32\notepad.exe", new[] { File }, @"C:\Windows\System32")]
    public void Each_windows_editor_starts_its_program_on_the_files_absolute_path_from_its_folder(string id, string program, string[] arguments, string folder)
    {
        var command = Build(new EditorSetting(id, program), id, EditorPlatform.Windows, line: null);

        Assert.Equal(program, command.FileName);
        Assert.Equal(arguments, command.Arguments);
        Assert.Equal(folder, command.WorkingDirectory);
        Assert.Equal([EditorCommands.ElectronRunAsNode], command.RemovedVariables);
        Assert.Null(command.CommandLine);
        Assert.False(command.FallBackOnFailure);
    }

    [Theory]
    [InlineData("vscode", new[] { "--goto", File + ":12" })]
    [InlineData("sublime", new[] { File + ":12" })]
    [InlineData("notepadpp", new[] { "-n12", File })]
    [InlineData("idea", new[] { "--line", "12", File })]
    [InlineData("gvim", new[] { "+12", File })]
    [InlineData("visualstudio", new[] { "/Edit", File })]
    [InlineData("notepad", new[] { File })]
    public void With_a_line_a_known_editor_takes_its_line_form_and_one_without_keeps_its_plain_one(string id, string[] arguments)
    {
        var command = Build(new EditorSetting(id, @"C:\Tools\Ed.exe"), id, EditorPlatform.Windows, line: 12);

        Assert.Equal(arguments, command.Arguments);
    }

    [Fact]
    public void A_known_editors_arguments_come_from_the_catalogue_never_from_the_setting()
    {
        var command = Build(new EditorSetting("vscode", @"C:\Tools\Code.exe", "--evil {path}"), "vscode", EditorPlatform.Windows, line: null);

        Assert.Equal([File], command.Arguments);
    }

    [Theory]
    [InlineData("{path}", new[] { File })]
    [InlineData("-l {line} {path}", new[] { "-l", "1", File })]
    [InlineData("--goto {path}:{line}", new[] { "--goto", File + ":1" })]
    [InlineData("--new-window", new[] { "--new-window", File })]
    [InlineData("", new[] { File })]
    [InlineData("\"--title=My editor\" {path}", new[] { "--title=My editor", File })]
    [InlineData("  -a   \"\"  {path}  ", new[] { "-a", "", File })]
    [InlineData("x\"y z\"w", new[] { "xy zw", File })]
    public void Others_arguments_split_at_whitespace_outside_quotes_with_the_path_last_without_path(string template, string[] arguments)
    {
        var command = Build(new EditorSetting(KnownEditors.CustomId, @"D:\Tools\Ed.exe", template), id: null, EditorPlatform.Windows, line: null);

        Assert.Equal(arguments, command.Arguments);
        Assert.Equal(@"D:\Tools", command.WorkingDirectory);
    }

    [Fact]
    public void An_unclosed_quote_is_refused()
    {
        Assert.Null(EditorCommands.SplitArguments("--title=\"My editor {path}"));
        var thrown = Assert.Throws<EditorCommandException>(() => Build(new EditorSetting(KnownEditors.CustomId, @"D:\Tools\Ed.exe", "\"{path}"), null, EditorPlatform.Windows, null));
        Assert.Equal(EditorCommands.UnclosedQuote, thrown.Message);
    }

    [Fact]
    public void A_refusals_message_is_the_reason_alone_as_the_error_dialog_shows_it()
    {
        // 12.7's summary takes the message: no " (Parameter '…')" of .NET's argument exceptions.
        Assert.Equal(
            @"C:\Tools\ed.cmd is not an .exe or .com program.",
            Assert.Throws<EditorCommandException>(() => Build(new EditorSetting(KnownEditors.CustomId, @"C:\Tools\ed.cmd", "{path}"), null, EditorPlatform.Windows, null)).Message);
        Assert.Equal(
            "The editor's program code is not an absolute path.",
            Assert.Throws<EditorCommandException>(() => EditorCommands.Build(new EditorSetting(KnownEditors.CustomId, "code", "{path}"), null, "/a.txt", null, EditorPlatform.Linux, "/home/ann")).Message);
    }

    [Fact]
    public void A_path_holding_the_other_token_is_put_in_as_it_is()
    {
        // Both tokens are replaced in one pass: the path's own "{line}" or "{n}" is no token.
        var other = EditorCommands.Build(new EditorSetting(KnownEditors.CustomId, "/opt/ed", "--goto {path}:{line}"), null, "/src/{line}/{path}.cs", 7, EditorPlatform.Linux, "/home/ann");
        Assert.Equal(["--goto", "/src/{line}/{path}.cs:7"], other.Arguments);

        var known = EditorCommands.Build(new EditorSetting("vscode", "/opt/code"), KnownEditors.Find("vscode", EditorPlatform.Linux), "/src/{n}/{p}.cs", 3, EditorPlatform.Linux, "/home/ann");
        Assert.Equal(["--goto", "/src/{n}/{p}.cs:3"], known.Arguments);
    }

    [Theory]
    [InlineData(@"C:\Tools\code.cmd")]
    [InlineData(@"C:\Tools\ed.bat")]
    [InlineData(@"C:\Tools\ed.ps1")]
    [InlineData(@"C:\Tools\ed")]
    public void On_windows_a_program_that_is_not_an_exe_or_com_is_refused(string program)
    {
        // 12.4: CreateProcess hands a .cmd or .bat to cmd.exe, which parses the arguments (0.9).
        Assert.Throws<EditorCommandException>(() => Build(new EditorSetting(KnownEditors.CustomId, program, "{path}"), null, EditorPlatform.Windows, null));
        Assert.False(EditorCommands.IsWindowsProgram(program));
    }

    [Fact]
    public void On_windows_a_com_program_and_one_on_a_drive_root_start()
    {
        var command = Build(new EditorSetting(KnownEditors.CustomId, @"E:\Ed.COM", "{path}"), null, EditorPlatform.Windows, null);

        Assert.Equal(@"E:\", command.WorkingDirectory);
        Assert.True(EditorCommands.IsWindowsProgram(@"E:\Ed.COM"));
    }

    [Fact]
    public void Off_windows_the_working_directory_is_the_users_home_and_scripts_start()
    {
        var command = EditorCommands.Build(new EditorSetting("vscode", "/opt/tools/vscode/code"), KnownEditors.Find("vscode", EditorPlatform.Linux), "/home/ann/src/run.py", null, EditorPlatform.Linux, "/home/ann");

        Assert.Equal("/opt/tools/vscode/code", command.FileName);
        Assert.Equal(["/home/ann/src/run.py"], command.Arguments);
        Assert.Equal("/home/ann", command.WorkingDirectory);
        Assert.Equal([EditorCommands.ElectronRunAsNode], command.RemovedVariables);

        var script = EditorCommands.Build(new EditorSetting(KnownEditors.CustomId, "/usr/local/bin/ed.sh", "{path}"), null, "/home/ann/a.txt", null, EditorPlatform.MacOS, "/Users/ann");
        Assert.Equal("/usr/local/bin/ed.sh", script.FileName);
        Assert.Equal("/Users/ann", script.WorkingDirectory);
    }

    [Fact]
    public void A_program_that_is_not_absolute_is_refused()
    {
        Assert.Throws<EditorCommandException>(() => Build(new EditorSetting(KnownEditors.CustomId, "code.exe", "{path}"), null, EditorPlatform.Windows, null));
        Assert.Throws<EditorCommandException>(() => EditorCommands.Build(new EditorSetting(KnownEditors.CustomId, "code", "{path}"), null, "/a.txt", null, EditorPlatform.Linux, "/home/ann"));
    }

    private static LaunchCommand Build(EditorSetting editor, string? id, EditorPlatform platform, int? line) =>
        EditorCommands.Build(editor, id is null ? null : KnownEditors.Find(id, platform), File, line, platform, home: @"C:\Users\Ann");
}
