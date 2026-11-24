using VisualCommit.App.Services;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The search for git that the session's services wait on (docs/phase-8a-design.md 1.2, 1.10), with
/// no session: a listener of <see cref="GitAccess.Changed"/> that throws changes neither how the
/// search ended nor what the other listeners hear, and a fixed search is <see cref="GitAccess.Known"/>
/// as soon as its task has completed. Since the first-run screen: a later search swaps in a new
/// pending detection, which a runner asked for meanwhile waits for; a search asked for while one runs
/// joins it; each search is told the lookup of its start; the stand-in's warning once; a probe of a
/// chosen file changes nothing; a fixed search's guidance is the platform's alone.
/// </summary>
public class GitAccessTests
{
    private static readonly GitDetection NotFound = new(GitAvailability.NotFound, null, null, null);

    [Fact]
    public async Task A_listener_that_throws_changes_neither_the_searchs_result_nor_what_the_other_listeners_hear()
    {
        var log = new RecordingLog();
        var access = new GitAccess(new AnsweringProbe(NotFound), () => new GitLookup(null, null), null, log, null, null);
        var heard = 0;
        access.Changed += (_, _) => throw new InvalidOperationException("A listener's fault.");
        access.Changed += (_, _) => heard++;

        var found = await access.DetectAsync(TestContext.Current.CancellationToken);

        Assert.Same(NotFound, found);
        Assert.Same(NotFound, await access.Detection);
        Assert.Same(NotFound, access.Known);
        Assert.Equal(1, heard);
        var entry = Assert.Single(log.Entries);
        Assert.Equal((LogLevel.Error, "A listener of the search for git failed."), (entry.Level, entry.Message));
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task A_cancelled_search_still_ends_cancelled_when_a_listener_throws()
    {
        var access = new GitAccess(new AnsweringProbe(NotFound), () => new GitLookup(null, null), null, new RecordingLog(), null, null);
        access.Changed += (_, _) => throw new InvalidOperationException("A listener's fault.");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => access.DetectAsync(cancellation.Token));

        Assert.True(access.Detection.IsCanceled);
        Assert.Null(access.Known);
    }

    [Fact]
    public void A_fixed_search_is_known_as_soon_as_its_task_has_completed()
    {
        // The task's continuations run later, on the pool: Known must not wait for them.
        var search = new TaskCompletionSource<GitDetection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var access = new GitAccess(search.Task);
        Assert.Null(access.Known);

        search.SetResult(NotFound);

        Assert.Same(NotFound, access.Known);
    }

    [Fact]
    public void A_fixed_search_that_failed_is_not_known()
    {
        var access = new GitAccess(Task.FromException<GitDetection>(new InvalidOperationException("No search.")));

        Assert.Null(access.Known);
    }

    [Fact]
    public async Task A_second_search_swaps_in_a_new_pending_detection_and_a_runner_asked_for_meanwhile_is_the_new_ones()
    {
        // docs/phase-8a-design.md 1.2: "Check again" searches again; GetRunnerAsync waits for that search.
        var runner = new NoRunner();
        var found = new GitDetection(GitAvailability.Available, "/usr/bin/git", new GitVersion(2, 47, 1), runner);
        var probe = new QueuedProbe(NotFound);
        var access = new GitAccess(probe, () => new GitLookup(null, null), null, new RecordingLog(), null, null);
        var changes = 0;
        access.Changed += (_, _) => changes++;

        Assert.Same(NotFound, await access.DetectAsync(TestContext.Current.CancellationToken));
        var first = access.Detection;
        await Assert.ThrowsAsync<GitUnavailableException>(() => access.GetRunnerAsync(TestContext.Current.CancellationToken));

        var held = probe.Hold();
        var second = access.DetectAsync(TestContext.Current.CancellationToken);
        var waiting = access.GetRunnerAsync(TestContext.Current.CancellationToken);

        Assert.NotSame(first, access.Detection);
        Assert.False(access.Detection.IsCompleted);
        Assert.False(waiting.IsCompleted);
        Assert.Same(NotFound, access.Known);

        held.SetResult(found);
        Assert.Same(found, await second);
        Assert.Same(runner, await waiting);
        Assert.Same(found, access.Known);
        Assert.Same(found, await access.Detection);
        Assert.Equal(2, changes);
        Assert.Equal(2, probe.Lookups.Count);
    }

    [Fact]
    public async Task A_search_asked_for_while_one_runs_joins_it()
    {
        var probe = new QueuedProbe(NotFound);
        var access = new GitAccess(probe, () => new GitLookup(null, null), null, new RecordingLog(), null, null);
        var held = probe.Hold();

        var first = access.DetectAsync(TestContext.Current.CancellationToken);
        var joined = access.DetectAsync(TestContext.Current.CancellationToken);
        var detection = access.Detection;
        held.SetResult(NotFound);

        Assert.Same(NotFound, await first);
        Assert.Same(NotFound, await joined);
        Assert.Same(detection, access.Detection);
        Assert.Single(probe.Lookups);
    }

    [Fact]
    public async Task A_search_asked_for_with_another_lookup_while_one_runs_does_not_join_it_but_searches_after_it()
    {
        // A git chosen while a search runs (D423): that search was told the old setting.
        var chosen = (string?)null;
        var file = Path.Combine(Path.GetTempPath(), "PortableGit", OperatingSystem.IsWindows() ? "git.exe" : "git");
        var found = new GitDetection(GitAvailability.Available, file, new GitVersion(2, 47, 1), new NoRunner()) { Source = GitSource.Setting };
        var probe = new QueuedProbe(found);
        var access = new GitAccess(probe, () => new GitLookup(chosen, null), null, new RecordingLog(), null, null);
        var held = probe.Hold();
        var first = access.DetectAsync(TestContext.Current.CancellationToken);
        var firstDetection = access.Detection;

        chosen = file;
        var second = access.DetectAsync(TestContext.Current.CancellationToken);
        var waiting = access.GetRunnerAsync(TestContext.Current.CancellationToken);

        // The second waits for the first before it asks the probe; a runner asked for meanwhile is the second's.
        Assert.NotSame(firstDetection, access.Detection);
        Assert.Single(probe.Lookups);
        held.SetResult(NotFound);
        Assert.Same(NotFound, await first);
        Assert.Same(found, await second);
        Assert.Same(found.Runner, await waiting);
        Assert.Equal([new GitLookup(null, null), new GitLookup(file, null)], probe.Lookups);
        Assert.Same(found, await access.Detection);
        Assert.Same(found, access.Known);
    }

    [Fact]
    public async Task The_search_that_ended_last_stays_known_when_the_one_it_waited_for_finishes_after_it_on_another_thread()
    {
        // A search asked for with another lookup starts once the running one's result is handed
        // over, not once that search has finished: off the UI thread the two finish on different
        // threads, in either order. Here the first search's thread is kept, as it hands its result
        // to the continuations of its task, until the second search has ended.
        var chosen = (string?)null;
        var file = Path.Combine(Path.GetTempPath(), "PortableGit", OperatingSystem.IsWindows() ? "git.exe" : "git");
        var found = new GitDetection(GitAvailability.Available, file, new GitVersion(2, 47, 1), new NoRunner()) { Source = GitSource.Setting };
        var probe = new QueuedProbe(found);
        var access = new GitAccess(probe, () => new GitLookup(chosen, null), null, new RecordingLog(), null, null);
        var held = probe.Hold();
        var first = access.DetectAsync(TestContext.Current.CancellationToken);
        var firstDetection = access.Detection;
        chosen = file;
        var second = access.DetectAsync(TestContext.Current.CancellationToken);
        var changes = 0;
        access.Changed += (_, _) => Interlocked.Increment(ref changes);
        _ = firstDetection.ContinueWith(_ => { }, CancellationToken.None, TaskContinuationOptions.None, new HoldingScheduler(second));

        held.SetResult(NotFound);

        Assert.Same(NotFound, await first);
        Assert.Same(found, await second);
        Assert.Same(found, await access.Detection);
        Assert.Same(found, access.Known);
        Assert.Equal(2, Volatile.Read(ref changes));
    }

    [Fact]
    public async Task Each_search_is_told_what_the_lookup_gives_when_it_starts()
    {
        var probe = new QueuedProbe(NotFound);
        var chosen = (string?)null;
        var access = new GitAccess(probe, () => new GitLookup(chosen, "none"), null, new RecordingLog(), null, null);

        await access.DetectAsync(TestContext.Current.CancellationToken);
        chosen = Path.Combine(Path.GetTempPath(), "git.exe");
        await access.DetectAsync(TestContext.Current.CancellationToken);

        Assert.Equal([new GitLookup(null, "none"), new GitLookup(chosen, "none")], probe.Lookups);
    }

    [Fact]
    public async Task The_stand_ins_warning_comes_once_at_the_first_search_told_of_it()
    {
        var full = Path.Combine(Path.GetTempPath(), "stand-in", OperatingSystem.IsWindows() ? "visualcommit-git-stand-in.exe" : "visualcommit-git-stand-in");
        var none = new RecordingLog();
        var path = new RecordingLog();
        var relative = new RecordingLog();
        var noneAccess = new GitAccess(new QueuedProbe(NotFound), () => new GitLookup(null, "none"), null, none, null, null);
        var pathAccess = new GitAccess(new QueuedProbe(NotFound), () => new GitLookup(null, full), null, path, null, null);
        var relativeAccess = new GitAccess(new QueuedProbe(NotFound), () => new GitLookup(null, "stand-in.exe"), null, relative, null, null);

        await noneAccess.DetectAsync(TestContext.Current.CancellationToken);
        await noneAccess.DetectAsync(TestContext.Current.CancellationToken);
        await pathAccess.DetectAsync(TestContext.Current.CancellationToken);
        await relativeAccess.DetectAsync(TestContext.Current.CancellationToken);

        var noGit = (LogLevel.Warning, "VISUALCOMMIT_GIT_STAND_IN is set (a test switch): unless Settings names a Git, this computer is treated as having no Git.");
        Assert.Equal([noGit], none.Entries.Select(entry => (entry.Level, entry.Message)));
        Assert.Equal([(LogLevel.Warning, $"VISUALCOMMIT_GIT_STAND_IN is set (a test switch): unless Settings names a Git, Git is looked for only at {full}.")], path.Entries.Select(entry => (entry.Level, entry.Message)));
        Assert.Equal([noGit], relative.Entries.Select(entry => (entry.Level, entry.Message)));
    }

    [Fact]
    public async Task Without_a_stand_in_nothing_is_said_of_it()
    {
        var log = new RecordingLog();
        var access = new GitAccess(new QueuedProbe(NotFound), () => new GitLookup(null, null), null, log, null, null);

        await access.DetectAsync(TestContext.Current.CancellationToken);

        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task Probe_asks_the_probe_for_the_path_as_the_chosen_git_and_changes_nothing()
    {
        var probe = new QueuedProbe(NotFound);
        var access = new GitAccess(probe, () => new GitLookup(null, "none"), null, new RecordingLog(), null, null);
        await access.DetectAsync(TestContext.Current.CancellationToken);
        var detection = access.Detection;
        var file = Path.Combine(Path.GetTempPath(), "PortableGit", "git.exe");
        var changes = 0;
        access.Changed += (_, _) => changes++;

        var probed = await access.ProbeAsync(file, TestContext.Current.CancellationToken);

        Assert.Same(NotFound, probed);
        Assert.Equal(new GitLookup(file, null), probe.Lookups[^1]);
        Assert.Same(detection, access.Detection);
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task A_fixed_search_probes_to_not_found_and_reads_no_guidance_but_the_platforms()
    {
        var access = new GitAccess(Task.FromResult(NotFound));

        var probed = await access.ProbeAsync(Path.Combine(Path.GetTempPath(), "git.exe"), TestContext.Current.CancellationToken);
        var guidance = access.ReadGuidance();

        Assert.Equal(GitAvailability.NotFound, probed.Availability);
        Assert.Equal(GitAccess.RunningPlatform, guidance.Platform);
        Assert.Null(guidance.WingetPath);
        Assert.Null(guidance.Distribution);
        Assert.Same(NotFound, await access.DetectAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_runner_of_a_chosen_git_that_is_not_used_says_why()
    {
        var gone = Path.Combine(Path.GetTempPath(), "gone", OperatingSystem.IsWindows() ? "git.exe" : "git");

        var goneAccess = new GitAccess(Task.FromResult(NotFound with { ChosenMissing = gone }));
        var relativeAccess = new GitAccess(Task.FromResult(NotFound with { ChosenMissing = "git.exe" }));

        Assert.Equal($"The Git chosen in Settings was not found at {gone}.", (await Assert.ThrowsAsync<GitUnavailableException>(() => goneAccess.GetRunnerAsync(TestContext.Current.CancellationToken))).Message);
        Assert.Equal("The Git chosen in Settings is not used: git.exe is not a full path.", (await Assert.ThrowsAsync<GitUnavailableException>(() => relativeAccess.GetRunnerAsync(TestContext.Current.CancellationToken))).Message);
        Assert.Equal(GitAccess.GitNotFound, (await Assert.ThrowsAsync<GitUnavailableException>(() => new GitAccess(Task.FromResult(NotFound)).GetRunnerAsync(TestContext.Current.CancellationToken))).Message);
        Assert.Equal(
            $"Git 2.29.0 is too old; VisualCommit needs {GitVersion.Minimum} or newer.",
            (await Assert.ThrowsAsync<GitUnavailableException>(() => new GitAccess(Task.FromResult(new GitDetection(GitAvailability.TooOld, "/usr/bin/git", new GitVersion(2, 29, 0), null))).GetRunnerAsync(TestContext.Current.CancellationToken))).Message);
    }

    /// <summary>A probe that answers from a queue, the last repeated, a held answer first when one is set, and keeps every lookup.</summary>
    private sealed class QueuedProbe(params GitDetection[] answers) : IGitSetupProbe
    {
        private readonly Queue<GitDetection> _answers = new(answers);
        private TaskCompletionSource<GitDetection>? _held;

        public List<GitLookup> Lookups { get; } = [];

        /// <summary>The next search waits until the returned source is given its answer.</summary>
        public TaskCompletionSource<GitDetection> Hold()
        {
            _held = new TaskCompletionSource<GitDetection>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _held;
        }

        public Task<GitDetection> DetectAsync(GitLookup lookup, IGitCallLog? callLog, IAppLog log, TimeProvider? recordClock, IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken)
        {
            Lookups.Add(lookup);
            if (_held is { } held)
            {
                _held = null;
                return held.Task;
            }

            return Task.FromResult(_answers.Count > 1 ? _answers.Dequeue() : _answers.Peek());
        }

        public GitGuidance ReadGuidance() => new(GuidancePlatform.Linux, null, null);
    }

    /// <summary>
    /// A scheduler for one continuation that keeps the thread which completes the continuation's
    /// task until <paramref name="until"/> has completed (30 seconds at most): a task hands a
    /// continuation to its scheduler on the thread that completes it, also when the task's source
    /// runs its continuations asynchronously.
    /// </summary>
    private sealed class HoldingScheduler(Task until) : TaskScheduler
    {
        protected override void QueueTask(Task task)
        {
            // A wait that times out is seen by the test's own awaits; a throw here would be swallowed.
            ((IAsyncResult)until).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
            TryExecuteTask(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        protected override IEnumerable<Task> GetScheduledTasks() => [];
    }

    /// <summary>A runner that is never run.</summary>
    private sealed class NoRunner : IGitRunner
    {
        public string ExecutablePath => "/usr/bin/git";

        public Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>A search for git that answers with one detection, or ends cancelled when its token is.</summary>
    private sealed class AnsweringProbe(GitDetection answer) : IGitSetupProbe
    {
        public Task<GitDetection> DetectAsync(GitLookup lookup, IGitCallLog? callLog, IAppLog log, TimeProvider? recordClock, IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested ? Task.FromCanceled<GitDetection>(cancellationToken) : Task.FromResult(answer);

        public GitGuidance ReadGuidance() => new(GuidancePlatform.Linux, null, null);
    }
}
