using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary><see cref="CloneProgressParser"/>: the progress lines of <c>git clone --progress</c>.</summary>
public class CloneProgressParserTests
{
    [Theory]
    [InlineData("Receiving objects:  45% (9/20), 1.20 MiB | 2.00 MiB/s", "Receiving objects", 45, "Receiving objects:  45% (9/20), 1.20 MiB | 2.00 MiB/s")]
    [InlineData("Receiving objects: 100% (20/20), 2.40 MiB | 2.00 MiB/s, done.", "Receiving objects", 100, "Receiving objects: 100% (20/20), 2.40 MiB | 2.00 MiB/s, done.")]
    [InlineData("Resolving deltas:   0% (0/3)", "Resolving deltas", 0, "Resolving deltas:   0% (0/3)")]
    [InlineData("remote: Counting objects: 100% (5/5), done.        ", "Counting objects", 100, "Counting objects: 100% (5/5), done.")]
    [InlineData("remote: Compressing objects:  50% (2/4)\u001b[K", "Compressing objects", 50, "Compressing objects:  50% (2/4)")]
    [InlineData("remote: Enumerating objects: 5, done.", "Enumerating objects", null, "Enumerating objects: 5, done.")]
    [InlineData("remote: Total 5 (delta 0), reused 0 (delta 0), pack-reused 0", "Total 5 (delta 0), reused 0 (delta 0), pack-reused 0", null, "Total 5 (delta 0), reused 0 (delta 0), pack-reused 0")]
    [InlineData("Updating files:  33% (1/3)", "Updating files", 33, "Updating files:  33% (1/3)")]
    [InlineData("Cloning into 'project'...", "Cloning into 'project'...", null, "Cloning into 'project'...")]
    [InlineData(@"Cloning into 'C:\work\project'...", @"Cloning into 'C:\work\project'...", null, @"Cloning into 'C:\work\project'...")]
    [InlineData("fatal: repository 'x' does not exist", "fatal", null, "fatal: repository 'x' does not exist")]
    public void Reads_the_stage_the_percent_and_the_text(string line, string stage, int? percent, string text) =>
        Assert.Equal(new CloneProgress(stage, percent, text), CloneProgressParser.Parse(line));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("remote: ")]
    [InlineData("remote:")]
    public void A_line_without_text_gives_nothing(string line) => Assert.Null(CloneProgressParser.Parse(line));
}
