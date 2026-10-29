using VisualCommit.App.Services;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Git;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The Info line that says R8's watcher lag is on (docs/phase-7a-design.md 5.2, D262): its text,
/// and the provider writing it once for this process's lag (<see cref="GitRepository.WatcherLag"/>),
/// which a lagged run (<c>VISUALCOMMIT_WATCHER_LAG_MS</c>) gives and the default run leaves at zero.
/// </summary>
public class GitRepositoryProviderTests
{
    [Fact]
    public void The_lag_line_names_the_variable_and_the_lag_in_whole_milliseconds()
    {
        Assert.Equal(
            "VISUALCOMMIT_WATCHER_LAG_MS=1500: every repository watcher holds each file-system event for 1500 ms before it counts it (a test switch, D262).",
            GitRepositoryProvider.WatcherLagLine(TimeSpan.FromMilliseconds(1500)));
        Assert.Equal(
            "VISUALCOMMIT_WATCHER_LAG_MS=10000: every repository watcher holds each file-system event for 10000 ms before it counts it (a test switch, D262).",
            GitRepositoryProvider.WatcherLagLine(TimeSpan.FromMilliseconds(10_000)));
        Assert.Null(GitRepositoryProvider.WatcherLagLine(TimeSpan.Zero));
    }

    [Fact]
    public void The_provider_says_this_processs_lag_once_at_info_and_nothing_without_one()
    {
        var log = new RecordingLog();

        _ = new GitRepositoryProvider(new GitAccess(new TaskCompletionSource<GitDetection>().Task), log);

        var expected = GitRepositoryProvider.WatcherLagLine(GitRepository.WatcherLag) is { } line ? new[] { (LogLevel.Info, line) } : [];
        Assert.Equal(expected, log.Entries.Select(entry => (entry.Level, entry.Message)));
    }
}
