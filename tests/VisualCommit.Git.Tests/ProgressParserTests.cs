using System.Collections.Concurrent;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="GitProgressParser"/> (phase 3 design 1.9): git's progress lines, as Git 2.36 prints
/// them for a fetch, a push and a checkout, and the lines that are not progress. The last tests
/// run real git and read what reaches <see cref="NetworkOptions.Progress"/>.
/// </summary>
public class ProgressParserTests
{
    [Theory]
    [InlineData("remote: Enumerating objects: 6, done.        ", "Enumerating objects", null, "Enumerating objects: 6, done.")]
    [InlineData("remote: Counting objects:  16% (1/6)        ", "Counting objects", 16, "Counting objects:  16% (1/6)")]
    [InlineData("remote: Counting objects: 100% (6/6), done.        ", "Counting objects", 100, "Counting objects: 100% (6/6), done.")]
    [InlineData("remote: Compressing objects:  33% (1/3)\u001b[K", "Compressing objects", 33, "Compressing objects:  33% (1/3)")]
    [InlineData("Receiving objects:  45% (9/20), 1.20 MiB | 2.00 MiB/s", "Receiving objects", 45, "Receiving objects:  45% (9/20), 1.20 MiB | 2.00 MiB/s")]
    [InlineData("Resolving deltas: 100% (1/1), done.", "Resolving deltas", 100, "Resolving deltas: 100% (1/1), done.")]
    [InlineData("Unpacking objects:  75% (3/4)", "Unpacking objects", 75, "Unpacking objects:  75% (3/4)")]
    [InlineData("Unpacking objects: 100% (4/4), 334 bytes | 2.00 KiB/s, done.", "Unpacking objects", 100, "Unpacking objects: 100% (4/4), 334 bytes | 2.00 KiB/s, done.")]
    [InlineData("Enumerating objects: 6, done.", "Enumerating objects", null, "Enumerating objects: 6, done.")]
    [InlineData("Counting objects:  50% (3/6)", "Counting objects", 50, "Counting objects:  50% (3/6)")]
    [InlineData("Delta compression using up to 8 threads", "Delta compression using up to 8 threads", null, "Delta compression using up to 8 threads")]
    [InlineData("Compressing objects: 100% (3/3), done.", "Compressing objects", 100, "Compressing objects: 100% (3/3), done.")]
    [InlineData("Writing objects:  25% (1/4)", "Writing objects", 25, "Writing objects:  25% (1/4)")]
    [InlineData("Writing objects: 100% (4/4), 354 bytes | 88.00 KiB/s, done.", "Writing objects", 100, "Writing objects: 100% (4/4), 354 bytes | 88.00 KiB/s, done.")]
    [InlineData("Updating files:  45% (450/1000)", "Updating files", 45, "Updating files:  45% (450/1000)")]
    [InlineData("Checking out files: 100% (3/3), done.", "Checking out files", 100, "Checking out files: 100% (3/3), done.")]
    public void A_progress_line_gives_its_stage_percent_and_text(string line, string stage, int? percent, string text) =>
        Assert.Equal(new OperationProgress(stage, percent, text), GitProgressParser.Parse(line));

    [Theory]
    [InlineData("remote: Total 4 (delta 0), reused 0 (delta 0), pack-reused 0        ")]
    [InlineData("Total 4 (delta 0), reused 0 (delta 0), pack-reused 0")]
    [InlineData(@"From C:\Users\me\AppData\Local\Temp\sync\origin")]
    [InlineData("   a7f2cfd..5df264c  main       -> origin/main")]
    [InlineData(@"To C:\Users\me\AppData\Local\Temp\sync\origin.git")]
    [InlineData(" ! [rejected]        main -> main (fetch first)")]
    [InlineData(" * [new branch]      topic -> topic")]
    [InlineData("error: failed to push some refs to 'https://example.com/x.git'")]
    [InlineData("fatal: 'nosuch' does not appear to be a git repository")]
    [InlineData("Fetching origin")]
    [InlineData("Cloning into 'project'...")]
    [InlineData("Updating a7f2cfd..5df264c")]
    [InlineData("Fast-forward")]
    [InlineData("Counting objectsX: 50% (3/6)")]
    [InlineData("remote: Counting")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("remote:")]
    [InlineData("remote: ")]
    public void A_line_that_is_not_progress_gives_nothing(string line) => Assert.Null(GitProgressParser.Parse(line));

    [Fact]
    public void The_clone_parser_keeps_its_rule_for_every_line()
    {
        // The clone page shows git's other lines too (phase 1), so its parser does not filter.
        Assert.Equal(new CloneProgress("Cloning into 'project'...", null, "Cloning into 'project'..."), CloneProgressParser.Parse("Cloning into 'project'..."));
        Assert.Equal(new CloneProgress("Receiving objects", 45, "Receiving objects:  45% (9/20)"), CloneProgressParser.Parse("Receiving objects:  45% (9/20)"));
        Assert.Null(CloneProgressParser.Parse("remote: "));
    }

    [Fact]
    public async Task A_fetch_and_a_push_report_gits_progress_through_the_options()
    {
        using var sync = await Scenarios.RemoteSyncAsync();
        await Scenarios.PushFromElsewhereAsync(sync, "main", "src/other.txt", "other\n", "Other work");
        var repository = await OpenAsync(sync);

        var fetched = new CollectingProgress();
        await repository.FetchAsync("origin", new NetworkOptions { Progress = fetched }, cancellationToken: TestCancelled);

        // The other side counts and compresses the 4 objects of the commit, as git 2.36 prints it.
        Assert.Contains(fetched.Reports, report => report.Stage == "Counting objects" && report.Percent == 100);
        Assert.All(fetched.Reports, report => Assert.DoesNotContain("remote:", report.Text, StringComparison.Ordinal));
        Assert.DoesNotContain(fetched.Reports, report => report.Text.StartsWith("From ", StringComparison.Ordinal));

        var pushed = new CollectingProgress();
        await repository.PushAsync(new PushRequest("origin", "topic", "refs/heads/topic"), new NetworkOptions { Progress = pushed }, TestCancelled);

        Assert.Contains(pushed.Reports, report => report.Stage == "Writing objects" && report.Percent == 100);
        Assert.DoesNotContain(pushed.Reports, report => report.Text.Contains("[new branch]", StringComparison.Ordinal));
    }

    /// <summary>Keeps every report, on the thread that reports it (unlike <see cref="Progress{T}"/>, which posts them).</summary>
    internal sealed class CollectingProgress : IProgress<OperationProgress>
    {
        private readonly ConcurrentQueue<OperationProgress> _reports = new();

        public IReadOnlyList<OperationProgress> Reports => [.. _reports];

        public void Report(OperationProgress value) => _reports.Enqueue(value);
    }
}
