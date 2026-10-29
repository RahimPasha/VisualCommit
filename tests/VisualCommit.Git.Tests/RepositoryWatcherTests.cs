using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="RepositoryWatcher"/>: changes made outside the app, debounced (D52). The timing
/// tests watch a plain folder and write files into it; what a repository's own git calls do is
/// tested against real repositories. The tests of the quiet period and the maximum delay hold the
/// watcher's clock (<see cref="ManualTimeProvider"/>) and move it only once the watcher has
/// counted every event they wait for, so that a slow machine cannot split a burst (D160).
/// </summary>
public class RepositoryWatcherTests
{
    /// <summary>A quiet period short enough to keep the tests quick, long enough for a burst of writes to fall inside it.</summary>
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(250);

    /// <summary>How long to wait for events that must not come. File-system events arrive within milliseconds; this leaves room for slow machines.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_commit_made_outside_the_app_raises_changed()
    {
        using var repo = await TempRepo.CreateAsync();
        var gitFolder = Path.Combine(repo.Path, ".git");
        using var watcher = new RepositoryWatcher(gitFolder, gitFolder, quietPeriod: Quiet);
        var changed = new Counter(watcher);
        watcher.Start();

        await repo.CommitFileAsync("a.txt", "a\n", "First");

        Assert.True(await changed.WaitForAsync(1, Timeout), "Changed was not raised.");
        Assert.True(changed.OnThreadPool);
    }

    [Fact]
    public async Task Reading_through_the_repository_and_writing_objects_or_lock_files_raise_nothing()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("a.txt", "a\n", "First");
        repo.WriteFile("a.txt", "stashed\n");
        await repo.GitAsync("stash", "push", "--quiet");
        var repository = await OpenAsync(repo);
        using var watcher = (RepositoryWatcher)repository.CreateWatcher();
        var changed = new Counter(watcher);
        watcher.Start();

        // Let anything the set-up left in the system's queues pass first.
        await Task.Delay(Settle, TestCancelled);
        changed.Reset();

        // Everything the views read. With GIT_OPTIONAL_LOCKS=0 none of it writes to the repository.
        var refs = await repository.ReadRefsAsync(TestCancelled);
        await repository.LoadCommitsAsync(refs, _ => { }, TestCancelled);
        await repository.ReadCommitDetailsAsync(refs.Head.Sha!, TestCancelled);
        await repository.ReadCommitDetailsAsync(refs.Stashes[0].Sha, TestCancelled);
        await Task.Delay(Settle, TestCancelled);
        Assert.True(changed.Count == 0, $"Reading the refs, history and details raised Changed {changed.Count} times.");

        // A file whose index entry no longer matches its time: a status that wrote the index
        // would now rewrite it. The touch itself is reported once the ignore check has run, which
        // takes a call of git, slow on a busy machine; it is waited for, not timed.
        File.SetLastWriteTimeUtc(Path.Combine(repo.Path, "a.txt"), DateTime.UtcNow.AddMinutes(-5));
        Assert.True(await changed.WaitForAsync(1, Timeout), "Touching a.txt was not reported.");
        await Task.Delay(Settle, TestCancelled);
        changed.Reset();
        await repository.ReadStatusAsync(TestCancelled);
        await repository.ReadDiffAsync(new Core.Diff.DiffTarget(Core.Diff.DiffSide.Unstaged, "a.txt", Core.Git.FileChangeKind.Modified), TestCancelled);

        // Git writes objects and lock files on the way to a change; the change itself follows.
        File.WriteAllText(Path.Combine(repository.CommonDirectory, "objects", "visualcommit-test"), "x");
        File.WriteAllText(Path.Combine(repository.GitDirectory, "refs", "heads", "main.lock"), "x");
        File.Delete(Path.Combine(repository.GitDirectory, "refs", "heads", "main.lock"));

        await Task.Delay(Settle, TestCancelled);
        Assert.True(changed.Count == 0, $"Changed was raised {changed.Count} times, the last for the git folder: {changed.Last?.GitFolder}, the working tree: {changed.Last?.WorkingTree}.");
    }

    [Fact]
    public async Task A_burst_of_changes_raises_changed_once()
    {
        // On a clock the test holds: however long the writes and their events take on a busy
        // machine, the burst stays one burst (on the system's clock, CI's loaded runner once
        // split it in two), and the quiet period runs only when the test moves the clock.
        using var folder = new TempDirectory("watch");
        var clock = new ManualTimeProvider();
        using var watcher = new RepositoryWatcher(folder.Path, folder.Path, quietPeriod: Quiet, time: clock);
        var changed = new Counter(watcher);
        var counted = new CountedEvents(watcher);
        watcher.Start();

        var names = Enumerable.Range(0, 20).Select(i => $"burst-{i}").ToList();
        foreach (var name in names)
        {
            File.WriteAllText(folder.Combine(name), "x");
        }

        Assert.True(await counted.WaitForAsync(names, Timeout), $"The watcher did not count events for {counted.Missing(names)}.");
        Assert.Equal(0, changed.Count);

        clock.Advance(Quiet - TimeSpan.FromTicks(1));
        Assert.Equal(0, changed.Count);

        // The raise runs on this thread, within the advance.
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(1, changed.Count);
        Assert.True(changed.Last!.GitFolder);

        // A late event of the burst (a second notice of a write) may start another burst, but
        // that cannot be raised while the clock stands still.
        await Task.Delay(Settle, TestCancelled);
        Assert.Equal(1, changed.Count);
    }

    [Fact]
    public async Task It_is_not_quiet_from_a_bursts_first_change_until_its_raise_has_ended()
    {
        // IsQuiet, which the harnesses wait for before they mark a check's next calls: a burst
        // waiting for its quiet period and one being raised are not quiet. On a clock the test
        // holds; a folder made gives one event, so no late notice starts a second burst.
        using var folder = new TempDirectory("watch");
        var clock = new ManualTimeProvider();
        using var watcher = new RepositoryWatcher(folder.Path, folder.Path, quietPeriod: Quiet, time: clock);
        var counted = new CountedEvents(watcher);
        bool? quietWhileRaised = null;
        watcher.Changed += (_, _) => quietWhileRaised = watcher.IsQuiet;
        watcher.Start();
        Assert.True(watcher.IsQuiet);

        Directory.CreateDirectory(folder.Combine("made"));
        Assert.True(await counted.WaitForAsync(["made"], Timeout), $"The watcher did not count an event for {counted.Missing(["made"])}.");
        Assert.False(watcher.IsQuiet, "A change waits for its quiet period, and the watcher is quiet.");
        clock.Advance(Quiet - TimeSpan.FromTicks(1));
        Assert.False(watcher.IsQuiet);

        // The raise runs on this thread, within the advance.
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.False(quietWhileRaised, "The watcher was quiet while its handlers ran.");
        Assert.True(watcher.IsQuiet);
    }

    [Fact]
    public async Task Changes_that_keep_coming_are_still_reported_after_the_maximum_delay()
    {
        using var folder = new TempDirectory("watch");
        var clock = new ManualTimeProvider();
        var maximumDelay = TimeSpan.FromMilliseconds(500);
        using var watcher = new RepositoryWatcher(folder.Path, folder.Path, quietPeriod: Quiet, maximumDelay: maximumDelay, time: clock);
        var changed = new Counter(watcher);
        var counted = new CountedEvents(watcher);
        watcher.Start();

        // A change every 100 ms of the clock, each counted before the clock moves on: never quiet
        // for the quiet period (250 ms). The first change is at 0 ms, the last at 400 ms.
        var step = TimeSpan.FromMilliseconds(100);
        for (var i = 0; clock.Elapsed < maximumDelay; i++)
        {
            Assert.True(changed.Count == 0, $"Changed was raised {clock.Elapsed.TotalMilliseconds} ms after the first change, while the changes kept coming.");
            var name = $"busy-{i}";
            File.WriteAllText(folder.Combine(name), i.ToString(CultureInfo.InvariantCulture));
            Assert.True(await counted.WaitForAsync([name], Timeout), $"The watcher did not count the event for {name}.");
            clock.Advance(step);
        }

        // At 500 ms, the maximum delay, although the last change was only 100 ms before.
        Assert.Equal(1, changed.Count);
        await Task.Delay(Settle, TestCancelled);
        Assert.Equal(1, changed.Count);
    }

    [Fact]
    public async Task Nothing_is_raised_before_start_or_after_dispose()
    {
        using var folder = new TempDirectory("watch");
        using var watcher = new RepositoryWatcher(folder.Path, folder.Path, quietPeriod: Quiet);
        var changed = new Counter(watcher);

        File.WriteAllText(folder.Combine("before-start"), "x");
        await Task.Delay(Quiet * 2, TestCancelled);
        Assert.Equal(0, changed.Count);

        watcher.Start();
        File.WriteAllText(folder.Combine("while-watching"), "x");
        Assert.True(await changed.WaitForAsync(1, Timeout), "Changed was not raised.");

        // A change just before Dispose, still waiting for its quiet period, is dropped too.
        File.WriteAllText(folder.Combine("just-before-dispose"), "x");
        watcher.Dispose();
        File.WriteAllText(folder.Combine("after-dispose"), "x");
        await Task.Delay(Settle, TestCancelled);
        Assert.Equal(1, changed.Count);
    }

    [Fact]
    public async Task A_lag_reports_a_write_made_inside_a_pause_that_ended_before_it()
    {
        // R8's lag (D262): the pause is checked when the held event is delivered, late, as on
        // macOS, so a write inside a pause that has ended by then counts as an outside change.
        var lag = TimeSpan.FromSeconds(1);
        using var folder = new TempDirectory("watch");
        using var watcher = new RepositoryWatcher(folder.Path, folder.Path, quietPeriod: Quiet, eventLag: lag);
        var changed = new Counter(watcher);
        watcher.Start();
        var started = Stopwatch.StartNew();
        long firstCounted = -1;
        watcher.EventCounted = _ => Interlocked.CompareExchange(ref firstCounted, started.Elapsed.Ticks, -1);

        using (watcher.Pause())
        {
            File.WriteAllText(folder.Combine("written-in-a-pause"), "x");
        }

        Assert.True(await changed.WaitForAsync(1, Timeout), "Changed was not raised.");
        Assert.True(changed.Last!.GitFolder);
        var counted = TimeSpan.FromTicks(Interlocked.Read(ref firstCounted));
        Assert.True(counted >= lag, $"The first event was counted {counted.TotalMilliseconds} ms after the stopwatch started, before the lag of {lag.TotalMilliseconds} ms.");
    }

    [Fact]
    public async Task A_lagged_watcher_disposed_with_events_held_drops_them()
    {
        var lag = TimeSpan.FromSeconds(1);
        using var folder = new TempDirectory("watch");
        var log = new ListLog();
        var watcher = new RepositoryWatcher(folder.Path, folder.Path, log, quietPeriod: Quiet, eventLag: lag);
        var changed = new Counter(watcher);
        watcher.Start();

        File.WriteAllText(folder.Combine("held"), "x");
        watcher.Dispose();

        await Task.Delay(lag + Settle, TestCancelled);
        Assert.Equal(0, changed.Count);
        Assert.DoesNotContain(log.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData("1500", 1500)]
    [InlineData("0", 0)]
    [InlineData("10000", 10_000)]
    [InlineData("10001", 0)]
    [InlineData("-1", 0)]
    [InlineData("+5", 0)]
    [InlineData(" 1500", 0)]
    [InlineData("1.5", 0)]
    [InlineData("abc", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void The_lag_variable_takes_whole_milliseconds_from_0_to_10000(string? value, int milliseconds) =>
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), GitRepository.ParseWatcherLag(value));

    [Fact]
    public async Task A_linked_worktree_is_watched_through_the_common_folder_that_holds_its_git_folder()
    {
        using var common = new TempDirectory("common");
        var gitFolder = common.Combine("worktrees", "linked");
        Directory.CreateDirectory(gitFolder);
        using var watcher = new RepositoryWatcher(gitFolder, common.Path, quietPeriod: Quiet);
        var changed = new Counter(watcher);
        watcher.Start();

        File.WriteAllText(common.Combine("packed-refs"), "x");

        Assert.Equal([common.Path], watcher.Folders);
        Assert.True(await changed.WaitForAsync(1, Timeout), "Changed was not raised.");
    }

    [Fact]
    public async Task A_file_edited_in_the_working_tree_raises_a_working_tree_change()
    {
        using var repo = await Scenarios.LinearAsync();
        var repository = await OpenAsync(repo);
        using var watcher = (RepositoryWatcher)repository.CreateWatcher();
        var changed = new Counter(watcher);
        watcher.Start();

        repo.WriteFile("notes.txt", "A note\n");

        Assert.True(await changed.WaitForAsync(1, Timeout), "Changed was not raised.");
        Assert.True(changed.Last!.WorkingTree);
        Assert.False(changed.Last.GitFolder);
    }

    [Fact]
    public async Task A_commit_made_outside_the_app_is_a_git_folder_change()
    {
        using var repo = await Scenarios.LinearAsync();
        var repository = await OpenAsync(repo);
        using var watcher = (RepositoryWatcher)repository.CreateWatcher();
        var changed = new Counter(watcher);
        watcher.Start();

        await repo.GitAsync("commit", "--quiet", "--allow-empty", "--message", "Empty");

        Assert.True(await changed.WaitForAsync(1, Timeout), "Changed was not raised.");
        Assert.True(changed.Last!.GitFolder);
    }

    [Fact]
    public async Task Changes_to_ignored_files_alone_raise_nothing()
    {
        using var repo = await Scenarios.LinearAsync();
        File.AppendAllText(Path.Combine(repo.Path, ".git", "info", "exclude"), "build/\n");
        var repository = await OpenAsync(repo);
        using var watcher = (RepositoryWatcher)repository.CreateWatcher();
        var changed = new Counter(watcher);
        watcher.Start();
        await Task.Delay(Settle, TestCancelled);
        changed.Reset();

        repo.WriteFile("build/out.txt", "output\n").WriteFile("build/deeper/more.txt", "more\n");
        await Task.Delay(Settle + Settle, TestCancelled);
        Assert.Equal(0, changed.Count);

        repo.WriteFile("notes.txt", "A note\n");
        Assert.True(await changed.WaitForAsync(1, Timeout), "A change to a file that is not ignored was not reported.");
    }

    [Fact]
    public async Task Changes_while_paused_are_not_reported()
    {
        using var repo = await Scenarios.LinearAsync();
        var repository = await OpenAsync(repo);
        using var watcher = (RepositoryWatcher)repository.CreateWatcher();
        var changed = new Counter(watcher);
        watcher.Start();

        using (watcher.Pause())
        {
            repo.WriteFile("written-by-the-app.txt", "x\n");
            await repo.GitAsync("add", "written-by-the-app.txt");
            await Task.Delay(Settle, TestCancelled);
        }

        await Task.Delay(Settle, TestCancelled);
        changed.Reset();
        repo.WriteFile("later.txt", "y\n");
        Assert.True(await changed.WaitForAsync(1, Timeout), "A change after the pause was not reported.");
    }

    [Theory]
    [InlineData(".git", true)]
    [InlineData(".git/index", true)]
    [InlineData(@".git\refs\heads\main", true)]
    [InlineData(".gitignore", false)]
    [InlineData("src/.git-like.txt", false)]
    public void Leaves_the_working_trees_git_folder_to_the_git_folder_watcher(string relativePath, bool inGitFolder) =>
        Assert.Equal(inGitFolder, RepositoryWatcher.IsInGitFolder(relativePath));

    [Fact]
    public void A_folder_that_cannot_be_watched_is_logged_and_does_not_throw()
    {
        using var folder = new TempDirectory("watch");
        var missing = folder.Combine("no-such-folder");
        var log = new ListLog();
        using var watcher = new RepositoryWatcher(missing, missing, log);

        watcher.Start();

        Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains(missing, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("HEAD", true)]
    [InlineData("refs/heads/main", true)]
    [InlineData(@"refs\heads\feature\login", true)]
    [InlineData("index", true)]
    [InlineData("objects", false)]
    [InlineData("objects/ab/cdef0123", false)]
    [InlineData(@"objects\pack\pack-1.idx", false)]
    [InlineData("HEAD.lock", false)]
    [InlineData("refs/heads/main.lock", false)]
    [InlineData("fsmonitor--daemon", false)]
    [InlineData("fsmonitor--daemon/cookies/1234-1", false)]
    [InlineData(@"fsmonitor--daemon\cookies\1234-2", false)]
    [InlineData("fsmonitor--daemon.ipc", false)]
    [InlineData("objectsfile", true)]
    public void Leaves_out_objects_lock_files_and_the_file_system_monitor(string relativePath, bool relevant) =>
        Assert.Equal(relevant, RepositoryWatcher.IsRelevant(relativePath));

    /// <summary>Counts the watcher's Changed events.</summary>
    private sealed class Counter
    {
        private readonly SemaphoreSlim _raised = new(0);
        private int _count;

        public Counter(RepositoryWatcher watcher) =>
            watcher.Changed += (_, e) =>
            {
                OnThreadPool = Thread.CurrentThread.IsThreadPoolThread;
                Last = e;
                Interlocked.Increment(ref _count);
                _raised.Release();
            };

        public int Count => Volatile.Read(ref _count);

        /// <summary>The arguments of the last Changed event.</summary>
        public Core.Git.RepositoryChangedEventArgs? Last { get; private set; }

        public bool OnThreadPool { get; private set; }

        public void Reset() => Interlocked.Exchange(ref _count, 0);

        /// <summary>Waits until the count reaches <paramref name="count"/>; false if it does not within <paramref name="timeout"/>.</summary>
        public async Task<bool> WaitForAsync(int count, TimeSpan timeout)
        {
            var deadline = Stopwatch.StartNew();
            while (Count < count)
            {
                var left = timeout - deadline.Elapsed;
                if (left <= TimeSpan.Zero || !await _raised.WaitAsync(left, TestCancelled))
                {
                    return Count >= count;
                }
            }

            return true;
        }
    }

    /// <summary>The names of the events the watcher counted in a burst, through its test hook (<see cref="RepositoryWatcher.EventCounted"/>).</summary>
    private sealed class CountedEvents
    {
        private readonly ConcurrentDictionary<string, bool> _names = new(StringComparer.Ordinal);

        public CountedEvents(RepositoryWatcher watcher) =>
            watcher.EventCounted = name =>
            {
                if (name is not null)
                {
                    _names[name] = true;
                }
            };

        /// <summary>Waits, polling, until an event for each of <paramref name="names"/> has been counted; false if that does not happen within <paramref name="timeout"/>.</summary>
        public async Task<bool> WaitForAsync(IReadOnlyCollection<string> names, TimeSpan timeout)
        {
            var waited = Stopwatch.StartNew();
            while (!names.All(_names.ContainsKey))
            {
                if (waited.Elapsed > timeout)
                {
                    return false;
                }

                await Task.Delay(10, TestCancelled);
            }

            return true;
        }

        public string Missing(IEnumerable<string> names) => string.Join(", ", names.Where(name => !_names.ContainsKey(name)));
    }
}
