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
/// tested against real repositories.
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

        // Git writes objects and lock files on the way to a change; the change itself follows.
        File.WriteAllText(Path.Combine(repository.CommonDirectory, "objects", "visualcommit-test"), "x");
        File.WriteAllText(Path.Combine(repository.GitDirectory, "refs", "heads", "main.lock"), "x");
        File.Delete(Path.Combine(repository.GitDirectory, "refs", "heads", "main.lock"));

        await Task.Delay(Settle, TestCancelled);
        Assert.Equal(0, changed.Count);
    }

    [Fact]
    public async Task A_burst_of_changes_raises_changed_once()
    {
        using var folder = new TempDirectory("watch");
        using var watcher = new RepositoryWatcher(folder.Path, folder.Path, quietPeriod: Quiet);
        var changed = new Counter(watcher);
        watcher.Start();

        for (var i = 0; i < 20; i++)
        {
            File.WriteAllText(folder.Combine($"burst-{i}"), "x");
        }

        Assert.True(await changed.WaitForAsync(1, Timeout), "Changed was not raised.");
        await Task.Delay(Settle, TestCancelled);
        Assert.Equal(1, changed.Count);
    }

    [Fact]
    public async Task Changes_that_keep_coming_are_still_reported_after_the_maximum_delay()
    {
        using var folder = new TempDirectory("watch");
        using var watcher = new RepositoryWatcher(folder.Path, folder.Path, quietPeriod: Quiet, maximumDelay: TimeSpan.FromMilliseconds(500));
        var changed = new Counter(watcher);
        watcher.Start();

        // A write every 50 ms for 2 seconds: never quiet for the quiet period.
        var writing = Stopwatch.StartNew();
        var countWhileWriting = 0;
        for (var i = 0; writing.Elapsed < TimeSpan.FromSeconds(2); i++)
        {
            File.WriteAllText(folder.Combine("busy"), i.ToString(CultureInfo.InvariantCulture));
            await Task.Delay(50, TestCancelled);
            countWhileWriting = changed.Count;
        }

        Assert.True(countWhileWriting >= 1, "Changed was not raised while the changes kept coming.");
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
    public void Leaves_out_objects_and_lock_files(string relativePath, bool relevant) =>
        Assert.Equal(relevant, RepositoryWatcher.IsRelevant(relativePath));

    /// <summary>Counts the watcher's Changed events.</summary>
    private sealed class Counter
    {
        private readonly SemaphoreSlim _raised = new(0);
        private int _count;

        public Counter(RepositoryWatcher watcher) =>
            watcher.Changed += (_, _) =>
            {
                OnThreadPool = Thread.CurrentThread.IsThreadPoolThread;
                Interlocked.Increment(ref _count);
                _raised.Release();
            };

        public int Count => Volatile.Read(ref _count);

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
}
