using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

public class GitVersionTests
{
    [Theory]
    [InlineData("git version 2.36.0.windows.1", 2, 36, 0)]
    [InlineData("git version 2.39.3 (Apple Git-146)", 2, 39, 3)]
    [InlineData("git version 2.43.0\n", 2, 43, 0)]
    [InlineData("git version 2.47.1.windows.2", 2, 47, 1)]
    [InlineData("git version 2.50.0-rc1", 2, 50, 0)]
    [InlineData("2.30", 2, 30, 0)]
    [InlineData("git version 10.2.14", 10, 2, 14)]
    public void TryParse_reads_the_version_from_git_output(string output, int major, int minor, int patch)
    {
        Assert.True(GitVersion.TryParse(output, out var version));
        Assert.Equal(new GitVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("git: command not found")]
    [InlineData("version two")]
    public void TryParse_rejects_text_without_a_version(string? output)
    {
        Assert.False(GitVersion.TryParse(output, out _));
    }

    [Fact]
    public void Versions_compare_by_major_then_minor_then_patch()
    {
        Assert.True(new GitVersion(2, 30, 0) < new GitVersion(2, 30, 1));
        Assert.True(new GitVersion(2, 9, 9) < new GitVersion(2, 30, 0));
        Assert.True(new GitVersion(3, 0, 0) > new GitVersion(2, 99, 99));
        Assert.True(new GitVersion(2, 30, 0) >= new GitVersion(2, 30, 0));
        Assert.True(new GitVersion(2, 30, 0) <= new GitVersion(2, 30, 0));
    }

    [Theory]
    [InlineData(2, 30, 0, true)]
    [InlineData(2, 36, 0, true)]
    [InlineData(3, 0, 0, true)]
    [InlineData(2, 29, 9, false)]
    [InlineData(1, 99, 0, false)]
    public void IsSupported_is_true_from_the_minimum_version_up(int major, int minor, int patch, bool expected)
    {
        Assert.Equal(new GitVersion(2, 30, 0), GitVersion.Minimum);
        Assert.Equal(expected, new GitVersion(major, minor, patch).IsSupported);
    }

    [Fact]
    public void ToString_gives_three_numbers()
    {
        Assert.Equal("2.36.0", new GitVersion(2, 36, 0).ToString());
    }
}
