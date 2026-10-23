using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="RemoteBranchNames"/>, the two rules every place that splits a remote-tracking ref's
/// name by its remote's name calls (docs/phase-6-design.md 10.1 to 10.4): the name less
/// <c>&lt;remote&gt;/</c>, and the configured remotes it starts with, which may hold slashes (0.5's
/// T: <c>team</c> and <c>team/fork</c>). Pure, no git.
/// </summary>
public class RemoteBranchNamesTests
{
    private static readonly string[] Remotes = ["origin", "team", "team/fork", "up"];

    [Theory]
    [InlineData("origin/pr/1", "origin", "pr/1")]
    [InlineData("origin/main", "origin", "main")]
    [InlineData("team/fork/main", "team", "fork/main")]
    [InlineData("team/fork/main", "team/fork", "main")]
    [InlineData("upstream/main", "up", "upstream/main")]
    [InlineData("origin", "origin", "origin")]
    [InlineData("origin/main", null, "origin/main")]
    [InlineData("mirror/main", "origin", "mirror/main")]
    public void WithoutRemote_takes_off_the_remote_and_its_slash_only(string name, string? remote, string expected) =>
        Assert.Equal(expected, RemoteBranchNames.WithoutRemote(name, remote));

    [Fact]
    public void RemotesOf_lists_every_configured_remote_the_name_starts_with_longest_first()
    {
        Assert.Equal(["team/fork", "team"], RemoteBranchNames.RemotesOf("team/fork/main", Remotes));
        Assert.Equal(["team"], RemoteBranchNames.RemotesOf("team/fork2/main", Remotes));
        Assert.Equal(["origin"], RemoteBranchNames.RemotesOf("origin/main", [.. Remotes, "origin"]));

        // "up" is no remote of "upstream/main", nor is a remote the whole name.
        Assert.Empty(RemoteBranchNames.RemotesOf("upstream/main", Remotes));
        Assert.Empty(RemoteBranchNames.RemotesOf("origin", Remotes));
        Assert.Empty(RemoteBranchNames.RemotesOf("origin/main", []));
    }

    [Theory]
    [InlineData("team/fork/main", "team/fork", "team/fork")]
    [InlineData("team/main", "team", "team")]
    [InlineData("origin/feature/x", "origin", "origin")]
    [InlineData("gone/topic", null, "gone")]
    [InlineData("upstream/main", null, "upstream")]
    [InlineData("lonely", null, "lonely")]
    public void LongestRemoteOf_is_the_longest_and_RemoteOf_falls_back_to_the_first_part(string name, string? longest, string remote)
    {
        Assert.Equal(longest, RemoteBranchNames.LongestRemoteOf(name, Remotes));
        Assert.Equal(remote, RemoteBranchNames.RemoteOf(name, Remotes));
    }
}
