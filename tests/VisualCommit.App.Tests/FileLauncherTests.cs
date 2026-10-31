using VisualCommit.App.Services;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The commands "Show in folder" runs on each platform (phase 3 design 2.32), built for paths of
/// every platform on any machine. Nothing is launched: <see cref="FileLauncher.RevealCommands"/>
/// only builds the commands, and the file system is given as functions. Also the addresses
/// <see cref="FileLauncher.OpenUrlAsync"/> refuses before it opens anything, which the headless
/// tests' fake refuses too (docs/phase-7a-design.md 1.7).
/// </summary>
public class FileLauncherTests
{
    [Fact]
    public void A_program_is_started_from_a_fully_qualified_folder_of_the_path_never_from_the_current_one()
    {
        // A cloned repository, the current folder when the app was started in it, can hold an
        // executable named xdg-open or dbus-send: an empty or relative PATH entry names it.
        var system = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "system-bin"));
        var other = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "other-bin"));
        var existing = new HashSet<string>(StringComparer.Ordinal)
        {
            Path.Combine(".", "xdg-open"),
            Path.Combine("tools", "xdg-open"),
            "xdg-open",
            Path.Combine(system, "xdg-open"),
        };
        var separator = Path.PathSeparator;

        Assert.Equal(
            Path.Combine(system, "xdg-open"),
            FileLauncher.FindOnPath("xdg-open", string.Join(separator, string.Empty, ".", "tools", other, system), separator, existing.Contains));
        Assert.Null(FileLauncher.FindOnPath("xdg-open", string.Join(separator, string.Empty, ".", "tools"), separator, existing.Contains));
        Assert.Null(FileLauncher.FindOnPath("dbus-send", string.Join(separator, ".", system), separator, existing.Contains));
        Assert.Null(FileLauncher.FindOnPath("xdg-open", null, separator, existing.Contains));
        Assert.True(Path.IsPathFullyQualified(FileLauncher.FindOnPath("xdg-open", system, separator, existing.Contains)!));
    }
    [Fact]
    public void Windows_selects_the_file_in_explorer_with_its_path_quoted_after_select()
    {
        var commands = Reveal(@"C:\Users\Ann\My repo\src\a, b.txt", RevealPlatform.Windows, files: [@"C:\Users\Ann\My repo\src\a, b.txt"]);

        var command = Assert.Single(commands);
        Assert.Equal("explorer.exe", command.FileName);
        Assert.Equal("/select,\"C:\\Users\\Ann\\My repo\\src\\a, b.txt\"", command.CommandLine);
        Assert.Empty(command.Arguments);
        Assert.False(command.FallBackOnFailure);

        var startInfo = command.ToStartInfo();
        Assert.Equal("/select,\"C:\\Users\\Ann\\My repo\\src\\a, b.txt\"", startInfo.Arguments);
        Assert.Empty(startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void Windows_takes_forward_slashes_and_a_folder_as_backslashes()
    {
        var commands = Reveal("C:/repo/docs/", RevealPlatform.Windows, folders: [@"C:\repo\docs", @"C:\repo"]);

        Assert.Equal("/select,\"C:\\repo\\docs\"", Assert.Single(commands).CommandLine);
    }

    [Fact]
    public void Windows_opens_the_nearest_folder_of_a_missing_file()
    {
        var commands = Reveal(@"C:\repo\gone\deeper\file.txt", RevealPlatform.Windows, folders: [@"C:\repo", @"C:\"]);

        Assert.Equal("\"C:\\repo\"", Assert.Single(commands).CommandLine);
    }

    [Fact]
    public void Windows_opens_the_drive_when_nothing_above_the_file_exists()
    {
        var commands = Reveal(@"Q:\nothing\file.txt", RevealPlatform.Windows);

        // Unquoted: a backslash before the closing quote would escape it.
        Assert.Equal(@"Q:\", Assert.Single(commands).CommandLine);
    }

    [Fact]
    public void MacOS_reveals_the_file_with_open_r()
    {
        var commands = Reveal("/Users/ann/My repo/src/a b.txt", RevealPlatform.MacOS, files: ["/Users/ann/My repo/src/a b.txt"]);

        var command = Assert.Single(commands);
        Assert.Equal("open", command.FileName);
        Assert.Equal(["-R", "/Users/ann/My repo/src/a b.txt"], command.Arguments);
        Assert.Null(command.CommandLine);

        var startInfo = command.ToStartInfo();
        Assert.Equal(["-R", "/Users/ann/My repo/src/a b.txt"], startInfo.ArgumentList);
        Assert.Equal(string.Empty, startInfo.Arguments);
    }

    [Fact]
    public void MacOS_opens_the_folder_of_a_missing_file()
    {
        var commands = Reveal("/Users/ann/repo/gone.txt", RevealPlatform.MacOS, folders: ["/Users/ann/repo"]);

        var command = Assert.Single(commands);
        Assert.Equal("open", command.FileName);
        Assert.Equal(["/Users/ann/repo"], command.Arguments);
    }

    [Fact]
    public void Linux_asks_the_file_manager_over_d_bus_and_falls_back_to_opening_the_folder()
    {
        var commands = Reveal("/home/ann/my repo/src/a,b #1.txt", RevealPlatform.Linux, files: ["/home/ann/my repo/src/a,b #1.txt"]);

        Assert.Equal(2, commands.Count);
        var dbus = commands[0];
        Assert.Equal("dbus-send", dbus.FileName);
        Assert.Equal(
            [
                "--session",
                "--print-reply",
                "--reply-timeout=5000",
                "--dest=org.freedesktop.FileManager1",
                "--type=method_call",
                "/org/freedesktop/FileManager1",
                "org.freedesktop.FileManager1.ShowItems",
                "array:string:file:///home/ann/my%20repo/src/a%2Cb%20%231.txt",
                "string:",
            ],
            dbus.Arguments);
        Assert.True(dbus.FallBackOnFailure);
        Assert.True(dbus.ToStartInfo().RedirectStandardError);

        var fallback = commands[1];
        Assert.Equal("xdg-open", fallback.FileName);
        Assert.Equal(["/home/ann/my repo/src"], fallback.Arguments);
        Assert.False(fallback.FallBackOnFailure);
    }

    [Fact]
    public void Linux_opens_the_nearest_folder_of_a_missing_file_without_d_bus()
    {
        var commands = Reveal("/home/ann/repo/gone/file.txt", RevealPlatform.Linux, folders: ["/home/ann/repo"]);

        var command = Assert.Single(commands);
        Assert.Equal("xdg-open", command.FileName);
        Assert.Equal(["/home/ann/repo"], command.Arguments);
    }

    [Fact]
    public void Linux_opens_the_root_when_nothing_above_the_file_exists()
    {
        var commands = Reveal("/gone/file.txt", RevealPlatform.Linux);

        Assert.Equal(["/"], Assert.Single(commands).Arguments);
    }

    [Theory]
    [InlineData("/plain/path.txt", "file:///plain/path.txt")]
    [InlineData("/a b/c,d.txt", "file:///a%20b/c%2Cd.txt")]
    [InlineData("/x/[id]%.tsx", "file:///x/%5Bid%5D%25.tsx")]
    [InlineData("/x/ünï.txt", "file:///x/%C3%BCn%C3%AF.txt")]
    [InlineData("/x/a-b_c.d~e", "file:///x/a-b_c.d~e")]
    public void File_uris_encode_every_byte_but_the_unreserved_ones(string path, string expected) =>
        Assert.Equal(expected, FileLauncher.FileUri(path));

    [Fact]
    public void The_platform_is_the_one_the_tests_run_on()
    {
        var expected = OperatingSystem.IsWindows() ? RevealPlatform.Windows
            : OperatingSystem.IsMacOS() ? RevealPlatform.MacOS
            : RevealPlatform.Linux;

        Assert.Equal(expected, FileLauncher.CurrentPlatform);
    }

    [Fact]
    public void An_editors_start_info_has_its_folder_its_arguments_as_a_list_no_shell_and_lacks_the_removed_variables()
    {
        // docs/phase-6-design.md 12.4: no shell anywhere, the arguments one by one, the working
        // directory given, ELECTRON_RUN_AS_NODE removed from the app's environment.
        var folder = Path.GetFullPath(Path.GetTempPath());
        var command = new LaunchCommand("/opt/tools/vscode/code", ["--goto", "/src/a b.cs:1"])
        {
            WorkingDirectory = folder,
            RemovedVariables = [EditorCommands.ElectronRunAsNode, "PATH"],
        };

        var startInfo = command.ToStartInfo();

        Assert.False(startInfo.UseShellExecute);
        Assert.Equal("/opt/tools/vscode/code", startInfo.FileName);
        Assert.Equal(["--goto", "/src/a b.cs:1"], startInfo.ArgumentList);
        Assert.Equal(string.Empty, startInfo.Arguments);
        Assert.Equal(folder, startInfo.WorkingDirectory);
        Assert.DoesNotContain(startInfo.Environment.Keys, key => string.Equals(key, EditorCommands.ElectronRunAsNode, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(startInfo.Environment.Keys, key => string.Equals(key, "PATH", StringComparison.OrdinalIgnoreCase));
        Assert.False(startInfo.RedirectStandardOutput);
    }

    [Fact]
    public void A_command_without_a_folder_or_removed_variables_starts_as_before()
    {
        var startInfo = new LaunchCommand("xdg-open", ["/home/ann"]).ToStartInfo();

        Assert.Equal(string.Empty, startInfo.WorkingDirectory);
        Assert.Contains(startInfo.Environment.Keys, key => string.Equals(key, "PATH", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Opening_in_an_editor_whose_program_is_gone_throws_file_not_found_from_its_task_before_any_start()
    {
        // 12.4: the check runs off the UI thread, inside the launcher's Task.Run; nothing starts.
        using var folder = new TempDirectory("editor");
        var missing = Path.Combine(folder.Path, OperatingSystem.IsWindows() ? "Missing.exe" : "missing");
        var launcher = new FileLauncher(() => null);

        var opening = launcher.OpenInEditorAsync(new LaunchCommand(missing, [Path.Combine(folder.Path, "a.txt")]), TestContext.Current.CancellationToken);
        var thrown = await Assert.ThrowsAsync<FileNotFoundException>(() => opening);
        Assert.Equal(missing, thrown.FileName);

        // The check comes first: a program the check says is gone is never started.
        var asked = new List<string>();
        Assert.Throws<FileNotFoundException>(() => FileLauncher.StartEditor(new LaunchCommand("/bin/sh", ["-c", "exit 0"]), path =>
        {
            asked.Add(path);
            return false;
        }));
        Assert.Equal(["/bin/sh"], asked);
    }

    [Theory]
    [InlineData("octocat/hello/pull/7")]
    [InlineData("git@github.com:octocat/hello.git")]
    [InlineData("ssh://git@github.com/octocat/hello.git")]
    [InlineData("file:///tmp/index.html")]
    [InlineData("javascript:alert(1)")]
    public async Task Only_an_http_or_https_address_is_opened_and_the_fake_refuses_what_the_launcher_refuses(string address)
    {
        // docs/phase-7a-design.md 1.7: the launcher's task fails before the UI thread or a window
        // is needed; the headless tests' fake fails the same way and records nothing, so a test
        // sees the app hand over an address the real launcher would refuse.
        var url = new Uri(address, UriKind.RelativeOrAbsolute);
        var fake = new FakeFileLauncher();

        await Assert.ThrowsAsync<ArgumentException>("url", () => new FileLauncher(() => null).OpenUrlAsync(url, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>("url", () => fake.OpenUrlAsync(url, TestContext.Current.CancellationToken));
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task The_fake_records_an_http_or_https_address_as_opened_in_the_browser()
    {
        var fake = new FakeFileLauncher();

        await fake.OpenUrlAsync(new Uri("https://github.com/octocat/hello/pull/7"), TestContext.Current.CancellationToken);
        await fake.OpenUrlAsync(new Uri("http://127.0.0.1:47395/octocat/hello/pull/7"), TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new FileLaunch(FileLaunchKind.OpenUrl, "https://github.com/octocat/hello/pull/7"),
                new FileLaunch(FileLaunchKind.OpenUrl, "http://127.0.0.1:47395/octocat/hello/pull/7"),
            ],
            fake.Requests);
    }

    private static IReadOnlyList<LaunchCommand> Reveal(string path, RevealPlatform platform, string[]? files = null, string[]? folders = null)
    {
        var fileSet = new HashSet<string>(files ?? [], StringComparer.Ordinal);
        var folderSet = new HashSet<string>(folders ?? [], StringComparer.Ordinal);
        return FileLauncher.RevealCommands(path, platform, fileSet.Contains, folderSet.Contains);
    }
}
