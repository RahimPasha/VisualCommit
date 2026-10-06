using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

public class GitLocatorTests
{
    private static GitSearchContext Windows(string? path, string[] wellKnown, params string[] existingFiles) =>
        new(true, path, wellKnown, file => existingFiles.Contains(file, StringComparer.OrdinalIgnoreCase));

    private static GitSearchContext Unix(string? path, string[] wellKnown, params string[] existingFiles) =>
        new(false, path, wellKnown, file => existingFiles.Contains(file, StringComparer.Ordinal));

    [Fact]
    public void FindExecutable_returns_the_first_git_on_the_path()
    {
        var first = Path.Combine("first", "git");
        var second = Path.Combine("second", "git");
        var context = Unix("empty:first:second", [], first, second);

        Assert.Equal(first, GitLocator.FindExecutable(context));
    }

    [Fact]
    public void FindExecutable_looks_for_git_exe_and_splits_on_semicolons_on_Windows()
    {
        var git = Path.Combine(@"C:\Tools\Git\cmd", "git.exe");
        var context = Windows(@"C:\Windows;C:\Tools\Git\cmd", [], git);

        Assert.Equal(git, GitLocator.FindExecutable(context));
    }

    [Fact]
    public void FindExecutable_ignores_a_file_named_git_without_the_extension_on_Windows()
    {
        var context = Windows(@"C:\Tools", [], Path.Combine(@"C:\Tools", "git"));

        Assert.Null(GitLocator.FindExecutable(context));
    }

    [Fact]
    public void FindExecutable_accepts_quoted_and_padded_path_entries()
    {
        var git = Path.Combine(@"C:\Program Files\Git\cmd", "git.exe");
        var context = Windows(" ;\"C:\\Program Files\\Git\\cmd\" ; ", [], git);

        Assert.Equal(git, GitLocator.FindExecutable(context));
    }

    [Fact]
    public void FindExecutable_falls_back_to_the_usual_install_locations_in_order()
    {
        var context = Unix("/nowhere", ["/usr/bin/git", "/usr/local/bin/git", "/opt/homebrew/bin/git"], "/opt/homebrew/bin/git", "/usr/local/bin/git");

        Assert.Equal("/usr/local/bin/git", GitLocator.FindExecutable(context));
    }

    [Fact]
    public void FindExecutable_prefers_the_path_over_the_usual_install_locations()
    {
        var onPath = Path.Combine("custom", "git");
        var context = Unix("custom", ["/usr/bin/git"], onPath, "/usr/bin/git");

        Assert.Equal(onPath, GitLocator.FindExecutable(context));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/somewhere:/else")]
    public void FindExecutable_returns_null_when_git_is_nowhere(string? path)
    {
        Assert.Null(GitLocator.FindExecutable(Unix(path, ["/usr/bin/git"])));
    }

    [Fact]
    public void The_system_search_context_lists_install_locations_for_this_platform()
    {
        var context = GitSearchContext.FromSystem();

        Assert.Equal(OperatingSystem.IsWindows(), context.IsWindows);
        Assert.NotEmpty(context.WellKnownLocations);
        Assert.All(context.WellKnownLocations, location => Assert.True(Path.IsPathRooted(location)));
        Assert.All(
            context.WellKnownLocations,
            location => Assert.Equal(OperatingSystem.IsWindows() ? "git.exe" : "git", Path.GetFileName(location)));
    }

    [Fact]
    public async Task DetectAsync_finds_the_git_on_this_machine_and_returns_a_working_runner()
    {
        var calls = new GitCallLog();

        var detection = await GitLocator.DetectAsync(calls, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GitAvailability.Available, detection.Availability);
        Assert.True(File.Exists(detection.ExecutablePath));
        Assert.NotNull(detection.Version);
        Assert.True(detection.Version.Value.IsSupported);
        Assert.NotNull(detection.Runner);
        Assert.Equal(detection.ExecutablePath, detection.Runner.ExecutablePath);

        // The version check itself is a recorded git call.
        var call = Assert.Single(calls.Snapshot());
        Assert.Equal("git --version", call.CommandText);
        Assert.True(call.Succeeded);
        Assert.Contains(detection.Version.Value.ToString(), call.StandardOutput);

        var result = await detection.Runner.RunAsync(new GitCommand("--version"), TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
    }
}
