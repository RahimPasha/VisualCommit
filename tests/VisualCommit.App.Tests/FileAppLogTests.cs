using VisualCommit.App.Services;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

public class FileAppLogTests
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 14, 12, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void An_entry_is_one_line_with_time_level_and_message_in_the_file_of_the_day()
    {
        using var folder = new TempDirectory("logs");
        var clock = new FixedClock(Noon);
        var log = new FileAppLog(folder.Path, clock: clock);

        log.Info("VisualCommit started.");

        Assert.Equal(folder.Combine("visualcommit-20260314.log"), log.CurrentFile);
        Assert.Equal(["2026-03-14 12:00:00.000 +02:00 [INF] VisualCommit started."], File.ReadAllLines(log.CurrentFile));
    }

    [Fact]
    public void Entries_are_appended_in_order_with_their_level()
    {
        using var folder = new TempDirectory("logs");
        var log = new FileAppLog(folder.Path, clock: new FixedClock(Noon));

        log.Debug("one");
        log.Info("two");
        log.Warning("three");
        log.Error("four");

        var lines = File.ReadAllLines(log.CurrentFile);
        Assert.Equal(4, lines.Length);
        Assert.EndsWith("[DBG] one", lines[0]);
        Assert.EndsWith("[INF] two", lines[1]);
        Assert.EndsWith("[WRN] three", lines[2]);
        Assert.EndsWith("[ERR] four", lines[3]);
    }

    [Fact]
    public void An_exception_is_written_under_its_entry_with_type_and_message()
    {
        using var folder = new TempDirectory("logs");
        var log = new FileAppLog(folder.Path);

        log.Error("Saving failed.", new InvalidOperationException("The disk is full."));

        var text = File.ReadAllText(log.CurrentFile);
        Assert.Contains("[ERR] Saving failed.", text);
        Assert.Contains("System.InvalidOperationException: The disk is full.", text);
    }

    [Fact]
    public void Entries_below_the_minimum_level_are_left_out()
    {
        using var folder = new TempDirectory("logs");
        var log = new FileAppLog(folder.Path, LogLevel.Warning);

        log.Debug("quiet");
        log.Info("quiet");
        log.Warning("loud");

        Assert.Single(File.ReadAllLines(log.CurrentFile));
    }

    [Fact]
    public void A_new_day_starts_a_new_file()
    {
        using var folder = new TempDirectory("logs");
        var clock = new FixedClock(Noon);
        var log = new FileAppLog(folder.Path, clock: clock);

        log.Info("today");
        clock.Now = Noon.AddDays(1);
        log.Info("tomorrow");

        Assert.Single(File.ReadAllLines(folder.Combine("visualcommit-20260314.log")));
        Assert.Single(File.ReadAllLines(folder.Combine("visualcommit-20260315.log")));
    }

    [Fact]
    public void The_log_folder_is_created_when_the_first_entry_is_written()
    {
        using var folder = new TempDirectory("logs");
        var log = new FileAppLog(folder.Combine("data", "logs"));

        log.Info("first");

        Assert.True(File.Exists(log.CurrentFile));
    }

    [Fact]
    public void Old_log_files_are_deleted_when_a_log_is_created_and_other_files_are_left_alone()
    {
        using var folder = new TempDirectory("logs");
        var old = folder.Combine("visualcommit-20260227.log");      // 15 days before
        var kept = folder.Combine("visualcommit-20260228.log");     // 14 days before
        var recent = folder.Combine("visualcommit-20260313.log");
        var unrelated = folder.Combine("notes-20200101.log");
        foreach (var file in new[] { old, kept, recent, unrelated })
        {
            File.WriteAllText(file, "x");
        }

        _ = new FileAppLog(folder.Path, clock: new FixedClock(Noon));

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(kept));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(unrelated));
    }

    [Fact]
    public async Task Several_logs_and_threads_can_write_to_the_same_file_without_losing_entries()
    {
        using var folder = new TempDirectory("logs");
        var clock = new FixedClock(Noon);
        var logs = new[] { new FileAppLog(folder.Path, clock: clock), new FileAppLog(folder.Path, clock: clock) };

        await Task.WhenAll(Enumerable.Range(0, 8).Select(writer => Task.Run(
            () =>
            {
                for (var entry = 0; entry < 50; entry++)
                {
                    logs[writer % 2].Info($"writer {writer} entry {entry}");
                }
            },
            TestContext.Current.CancellationToken)));

        var lines = File.ReadAllLines(logs[0].CurrentFile);
        Assert.Equal(400, lines.Length);
        Assert.All(lines, line => Assert.Matches(@"^2026-03-14 12:00:00\.000 \+02:00 \[INF\] writer \d entry \d+$", line));
    }

    [Fact]
    public void A_log_that_cannot_write_does_not_throw()
    {
        using var folder = new TempDirectory("logs");

        // A file where the log folder should be makes every write fail.
        var blocked = folder.Combine("logs");
        File.WriteAllText(blocked, "in the way");
        var log = new FileAppLog(blocked);

        log.Error("nobody will read this");
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("test", Now.Offset, "test", "test");

        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }
}
