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
    }

    [Fact]
    public void The_default_data_folder_is_a_VisualCommit_folder_in_the_users_app_data()
    {
        var folder = AppPaths.DefaultDataDirectory();

        Assert.True(Path.IsPathRooted(folder));
        Assert.Equal(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                "VisualCommit"),
            folder);
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
}
