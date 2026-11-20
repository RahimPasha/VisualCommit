using VisualCommit.Core;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

public class AppPathsTests
{
    [Fact]
    public void Everything_lives_under_the_data_folder()
    {
        var paths = new AppPaths(Path.Combine("some", "data"));

        Assert.Equal(Path.Combine("some", "data", "settings.json"), paths.SettingsFile);
        Assert.Equal(Path.Combine("some", "data", "logs"), paths.LogDirectory);
        Assert.Equal(Path.Combine("some", "data", "instance.lock"), paths.InstanceLockFile);
        Assert.Equal(Path.Combine("some", "data", "instance.json"), paths.InstanceFile);
    }

    /// <summary>
    /// The folder the docs name for each platform (ruling 68): <c>%APPDATA%\VisualCommit</c> on
    /// Windows, <c>~/Library/Application Support/VisualCommit</c> on macOS (CI's macOS job runs it),
    /// <c>$XDG_CONFIG_HOME/VisualCommit</c> on Linux when that variable holds an absolute path (the
    /// test processes set it), else <c>~/.config/VisualCommit</c>. Built from the variables' values
    /// alone, never from .NET's special folders, which the method itself uses; nothing is read from
    /// those folders.
    /// </summary>
    [Fact]
    public void The_default_data_folder_is_the_platforms_documented_one()
    {
        string expected;
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetEnvironmentVariable("APPDATA");
            Assert.False(string.IsNullOrEmpty(appData), "APPDATA is not set.");
            expected = Path.Combine(appData, "VisualCommit");
        }
        else if (OperatingSystem.IsMacOS())
        {
            expected = Path.Combine(Home(), "Library", "Application Support", "VisualCommit");
        }
        else
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            expected = xdg is not null && xdg.StartsWith('/')
                ? Path.Combine(xdg, "VisualCommit")
                : Path.Combine(Home(), ".config", "VisualCommit");
        }

        Assert.Equal(expected, AppPaths.DefaultDataDirectory());
    }

    [Fact]
    public void In_tests_the_data_folder_never_resolves_to_the_users_real_one()
    {
        // Building the headless test app points VISUALCOMMIT_DATA_DIR at a temporary folder.
        HeadlessTestApp.BuildAvaloniaApp();

        var resolved = AppPaths.Resolve().DataDirectory;

        Assert.NotEqual(AppPaths.DefaultDataDirectory(), resolved);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), resolved);
        Assert.Equal(resolved, Environment.GetEnvironmentVariable(AppPaths.DataDirectoryVariable));
    }

    /// <summary>The home folder as <c>HOME</c> names it on macOS and Linux.</summary>
    private static string Home()
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        Assert.False(string.IsNullOrEmpty(home), "HOME is not set.");
        return home;
    }
}
