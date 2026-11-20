using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The stand-in for git (docs/phase-8a-design.md 1.1, 1.14), run as the app runs git: its file
/// name, and its answers in order: the failure variable's text with exit 128 whatever the
/// arguments; <c>--version</c> alone, its version line (by default 2.29.0, below the minimum) and
/// exit 0; anything else, its own line and exit 1. Its variables reach it through the runner's
/// environment, never the test process's.
/// </summary>
public class GitStandInTests
{
    private static readonly string StandIn = Path.Combine(AppContext.BaseDirectory, GitStandIn.FileName);

    [Fact]
    public void It_is_built_next_to_the_tests_and_is_never_named_git()
    {
        Assert.True(File.Exists(StandIn), $"The stand-in is missing at {StandIn}.");
        Assert.Equal(OperatingSystem.IsWindows() ? "visualcommit-git-stand-in.exe" : "visualcommit-git-stand-in", Path.GetFileName(StandIn));
        Assert.NotEqual(OperatingSystem.IsWindows() ? "git.exe" : "git", Path.GetFileName(StandIn));
    }

    [Fact]
    public async Task Version_answers_2_29_0_by_default_below_the_minimum()
    {
        var result = await RunAsync(null, null, "--version");

        Assert.Equal((0, "git version 2.29.0"), (result.ExitCode, result.StandardOutput.Trim()));
        Assert.True(GitVersion.TryParse(result.StandardOutput, out var version));
        Assert.False(version.IsSupported);
    }

    [Fact]
    public async Task Version_answers_the_line_its_variable_gives()
    {
        var result = await RunAsync("git version 2.47.0.windows.1", null, "--version");

        Assert.Equal((0, "git version 2.47.0.windows.1"), (result.ExitCode, result.StandardOutput.Trim()));
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("status")]
    public async Task The_failure_text_goes_to_standard_error_with_128_whatever_the_arguments(string argument)
    {
        var result = await RunAsync("git version 2.47.0", "fatal: bad config line 1 in file C:/Users/demo/.gitconfig", argument);

        Assert.Equal((128, string.Empty), (result.ExitCode, result.StandardOutput));
        Assert.Equal("fatal: bad config line 1 in file C:/Users/demo/.gitconfig", result.StandardError.Trim());
    }

    [Theory]
    [InlineData("status")]
    [InlineData("--version", "--build-options")]
    public async Task Anything_but_version_alone_is_refused_with_1(params string[] arguments)
    {
        var result = await RunAsync(null, null, arguments);

        Assert.Equal((1, string.Empty), (result.ExitCode, result.StandardOutput));
        Assert.Equal("fatal: VisualCommit's stand-in for git answers only --version", result.StandardError.Trim());
    }

    private static Task<GitResult> RunAsync(string? version, string? failure, params string[] arguments) =>
        new GitRunner(StandIn, environment: new Dictionary<string, string?>
        {
            [GitStandIn.VersionVariable] = version,
            [GitStandIn.FailureVariable] = failure,
        }).RunAsync(new GitCommand(arguments), TestCancelled);
}
