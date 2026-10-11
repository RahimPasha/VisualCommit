using VisualCommit.App.Services;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The commands "Show in folder" runs on each platform (phase 3 design 2.32), built for paths of
/// every platform on any machine. Nothing is launched: <see cref="FileLauncher.RevealCommands"/>
/// only builds the commands, and the file system is given as functions.
/// </summary>
public class FileLauncherTests
{
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

    private static IReadOnlyList<LaunchCommand> Reveal(string path, RevealPlatform platform, string[]? files = null, string[]? folders = null)
    {
        var fileSet = new HashSet<string>(files ?? [], StringComparer.Ordinal);
        var folderSet = new HashSet<string>(folders ?? [], StringComparer.Ordinal);
        return FileLauncher.RevealCommands(path, platform, fileSet.Contains, folderSet.Contains);
    }
}
