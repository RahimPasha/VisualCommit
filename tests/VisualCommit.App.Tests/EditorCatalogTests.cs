using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Settings;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Finding the editors on a computer (docs/phase-6-design.md 12.3, 12.10) over a fake file system:
/// what is found, git's editor as a mark, and a known editor that moved.
/// </summary>
public class EditorCatalogTests
{
    private const string ProgramFiles = @"C:\Program Files";
    private const string LocalAppData = @"C:\Users\Ann\AppData\Local";

    [Fact]
    public async Task The_editors_whose_program_exists_are_found_in_the_catalogues_order()
    {
        var catalog = new EditorCatalog(Windows(
            [
                @"C:\Windows\System32\notepad.exe",
                $@"{LocalAppData}\Programs\Microsoft VS Code\Code.exe",
                $@"{ProgramFiles}\Microsoft VS Code\Code.exe",
                @"C:\Program Files (x86)\Notepad++\notepad++.exe",
                $@"{ProgramFiles}\JetBrains\IntelliJ IDEA Community Edition 2020.3.2\bin\idea64.exe",
            ],
            new Dictionary<string, string[]> { [$@"{ProgramFiles}\JetBrains"] = [$@"{ProgramFiles}\JetBrains\IntelliJ IDEA Community Edition 2020.3.2"] }));

        var found = await catalog.FindAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new FoundEditor("vscode", "Visual Studio Code", $@"{LocalAppData}\Programs\Microsoft VS Code\Code.exe"),
                new FoundEditor("notepadpp", "Notepad++", @"C:\Program Files (x86)\Notepad++\notepad++.exe"),
                new FoundEditor("idea", "IntelliJ IDEA", $@"{ProgramFiles}\JetBrains\IntelliJ IDEA Community Edition 2020.3.2\bin\idea64.exe"),
                new FoundEditor("notepad", "Notepad", @"C:\Windows\System32\notepad.exe"),
            ],
            found);
        Assert.Equal("Visual Studio Code", catalog.Known("vscode")?.Name);
        Assert.Null(catalog.Known(KnownEditors.CustomId));
    }

    [AvaloniaFact]
    public async Task The_file_checks_run_off_the_ui_thread()
    {
        // 12.3: a check of a program on a share that does not answer blocks for minutes (0.9).
        var onUiThread = 0;
        var checks = 0;
        var context = Windows([], NoFolders) with
        {
            FileExists = path =>
            {
                Interlocked.Increment(ref checks);
                if (Dispatcher.UIThread.CheckAccess())
                {
                    Interlocked.Increment(ref onUiThread);
                }

                return false;
            },
        };

        await new EditorCatalog(context).FindAsync(TestContext.Current.CancellationToken);

        Assert.True(checks > 0);
        Assert.Equal(0, onUiThread);
    }

    [Theory]
    [InlineData("\"C:\\Program Files (x86)\\Notepad++\\notepad++.exe\" -multiInst -notabbar -nosession -noPlugin", "notepadpp")]
    [InlineData("'C:/Program Files (x86)/Notepad++/NOTEPAD++.EXE' -multiInst", "notepadpp")]
    [InlineData("C:\\Windows\\System32\\notepad.exe", "notepad")]
    [InlineData("vi", null)]
    [InlineData("\"C:\\Program Files (x86)\\Notepad++\\notepad++.exe", "notepadpp")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    public void Gits_editor_marks_the_found_editor_its_first_word_names(string? coreEditor, string? id)
    {
        // 12.3: core.editor is a shell command line, never run; its first token, unquoted, ignoring case on Windows.
        IReadOnlyList<FoundEditor> found =
        [
            new("notepadpp", "Notepad++", @"C:\Program Files (x86)\Notepad++\notepad++.exe"),
            new("notepad", "Notepad", @"C:\Windows\System32\notepad.exe"),
        ];

        Assert.Equal(id, EditorCatalog.GitEditor(coreEditor, found, EditorPlatform.Windows)?.Id);
    }

    [Fact]
    public void Off_windows_gits_editor_compares_case_exactly()
    {
        IReadOnlyList<FoundEditor> found = [new("gedit", "gedit", "/usr/bin/gedit")];

        Assert.Equal("gedit", EditorCatalog.GitEditor("/usr/bin/gedit --wait", found, EditorPlatform.Linux)?.Id);
        Assert.Null(EditorCatalog.GitEditor("/usr/bin/GEDIT", found, EditorPlatform.Linux));
    }

    [Fact]
    public async Task A_known_editor_that_moved_is_followed_by_the_setting_with_a_log_line()
    {
        // A JetBrains update renames its folder (12.3): the stored program is gone, found elsewhere.
        var root = $@"{ProgramFiles}\JetBrains";
        var newer = $@"{root}\IntelliJ IDEA 2024.1\bin\idea64.exe";
        var settings = new MainWindowViewModelTests.FakeSettings();
        settings.Update(current => current with { Editor = new EditorSetting("idea", $@"{root}\IntelliJ IDEA 2023.1\bin\idea64.exe") });
        var log = new RecordingLog();
        var catalog = new EditorCatalog(Windows([newer], new Dictionary<string, string[]> { [root] = [$@"{root}\IntelliJ IDEA 2024.1"] }), settings, log);

        await catalog.FindAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new EditorSetting("idea", newer), settings.Current.Editor);
        Assert.Contains($"IntelliJ IDEA moved to {newer}; the editor setting follows it.", log.Messages);
    }

    [Fact]
    public async Task A_search_stopped_as_it_ends_leaves_the_setting_alone()
    {
        // The screen that asked closed (its Cancel, or the session's end) just as the search ended:
        // nothing writes the setting from the pool after that (the session's data folder may be gone).
        var root = $@"{ProgramFiles}\JetBrains";
        var newer = $@"{root}\IntelliJ IDEA 2024.1\bin\idea64.exe";
        var stored = new EditorSetting("idea", $@"{root}\IntelliJ IDEA 2023.1\bin\idea64.exe");
        var settings = new MainWindowViewModelTests.FakeSettings();
        settings.Update(current => current with { Editor = stored });
        using var stop = new CancellationTokenSource();
        var files = Windows([newer], new Dictionary<string, string[]> { [root] = [$@"{root}\IntelliJ IDEA 2024.1"] });
        var context = files with
        {
            // Notepad is the catalogue's last Windows editor: its check is the search's last.
            FileExists = path =>
            {
                if (path.EndsWith(@"\notepad.exe", StringComparison.OrdinalIgnoreCase))
                {
                    stop.Cancel();
                }

                return files.FileExists(path);
            },
        };
        var log = new RecordingLog();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EditorCatalog(context, settings, log).FindAsync(stop.Token));

        Assert.Equal(stored, settings.Current.Editor);
        Assert.DoesNotContain(log.Messages, message => message.Contains("moved to", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_editor_whose_program_is_there_or_that_is_not_found_again_and_others_choice_stay()
    {
        var settings = new MainWindowViewModelTests.FakeSettings();
        var log = new RecordingLog();
        var code = $@"{LocalAppData}\Programs\Microsoft VS Code\Code.exe";

        // Still there, though another candidate exists too.
        settings.Update(current => current with { Editor = new EditorSetting("vscode", $@"{ProgramFiles}\Microsoft VS Code\Code.exe") });
        await new EditorCatalog(Windows([code, $@"{ProgramFiles}\Microsoft VS Code\Code.exe"], NoFolders), settings, log).FindAsync(TestContext.Current.CancellationToken);
        Assert.Equal($@"{ProgramFiles}\Microsoft VS Code\Code.exe", settings.Current.Editor!.Program);

        // Gone and not found anywhere: kept, so that Open in editor says it is gone (12.7).
        settings.Update(current => current with { Editor = new EditorSetting("sublime", @"D:\Old\subl.exe") });
        await new EditorCatalog(Windows([code], NoFolders), settings, log).FindAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new EditorSetting("sublime", @"D:\Old\subl.exe"), settings.Current.Editor);

        // Other is never moved.
        settings.Update(current => current with { Editor = new EditorSetting(KnownEditors.CustomId, @"C:\Program Files\Microsoft VS Code\Gone.exe", "{path}") });
        await new EditorCatalog(Windows([code], NoFolders), settings, log).FindAsync(TestContext.Current.CancellationToken);
        Assert.Equal(KnownEditors.CustomId, settings.Current.Editor!.Id);
        Assert.DoesNotContain(log.Messages, message => message.Contains("moved to", StringComparison.Ordinal));
    }

    private static readonly IReadOnlyDictionary<string, string[]> NoFolders = new Dictionary<string, string[]>();

    private static EditorSearchContext Windows(string[] files, IReadOnlyDictionary<string, string[]> folders)
    {
        var existing = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        return new EditorSearchContext(
            EditorPlatform.Windows,
            LocalAppData,
            ProgramFiles,
            @"C:\Program Files (x86)",
            @"C:\Windows",
            @"C:\Users\Ann",
            null,
            existing.Contains,
            _ => false,
            folder => folders.TryGetValue(folder, out var children) ? children : []);
    }
}
