using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.Git.Tests;

public class GitCallLogTests
{
    private static GitCallRecord Record(string command) =>
        new(DateTimeOffset.Now, TimeSpan.FromMilliseconds(5), command, null, GitCallOutcome.Completed, 0, string.Empty, string.Empty);

    [Fact]
    public void Snapshot_returns_the_records_oldest_first()
    {
        var log = new GitCallLog();
        log.Add(Record("git one"));
        log.Add(Record("git two"));
        log.Add(Record("git three"));

        Assert.Equal(["git one", "git two", "git three"], log.Snapshot().Select(record => record.CommandText));
    }

    [Fact]
    public void Only_the_newest_records_are_kept_once_the_capacity_is_reached()
    {
        var log = new GitCallLog(capacity: 2);
        log.Add(Record("git one"));
        log.Add(Record("git two"));
        log.Add(Record("git three"));

        Assert.Equal(["git two", "git three"], log.Snapshot().Select(record => record.CommandText));
    }

    [Fact]
    public void Added_is_raised_for_every_record()
    {
        var log = new GitCallLog();
        var seen = new List<string>();
        log.Added += (sender, record) =>
        {
            Assert.Same(log, sender);
            seen.Add(record.CommandText);
        };

        log.Add(Record("git one"));
        log.Add(Record("git two"));

        Assert.Equal(["git one", "git two"], seen);
    }

    [Fact]
    public void A_snapshot_does_not_change_when_more_records_arrive()
    {
        var log = new GitCallLog();
        log.Add(Record("git one"));
        var snapshot = log.Snapshot();

        log.Add(Record("git two"));

        Assert.Single(snapshot);
    }

    [Fact]
    public void A_capacity_below_one_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GitCallLog(capacity: 0));
    }
}
