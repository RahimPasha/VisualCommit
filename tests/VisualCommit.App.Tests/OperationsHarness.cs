using System.Globalization;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Session;
using VisualCommit.Core.Undo;
using VisualCommit.Git;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Runs <see cref="RepositoryOperations"/> against a real scenario repository (docs/phase-3-design.md
/// 7.4, section 8's WP7): the repository's view model, opened and read as a tab opens it, with a
/// real dialog layer whose dialogs <see cref="Dialogs"/> answers as the test scripts them, a
/// <see cref="FakeClipboard"/> and a <see cref="FakeFileLauncher"/>, and a log to look at. No
/// window and no UI thread: the operations run on the test's own context, as phase 2's
/// <c>WorkingChangesTests</c> run the writes. A typical test:
/// <code>
/// using var repo = await Scenarios.OperationsAsync();
/// using var app = await OperationsHarness.OpenAsync(repo);
/// app.Dialogs.Confirm();
/// await app.Operations.DeleteBranchAsync("refs/heads/feature/ff");
/// Assert.Equal("Deleted feature/ff", app.StatusText);
/// Assert.Null(await repo.RefTargetAsync("refs/heads/feature/ff"));
/// </code>
/// Every public operation completes when its operation and its refresh have ended, so a test
/// asserts right after awaiting it.
/// <para>
/// The harness's own steps (the open, the first read, <see cref="WaitForIdleAsync"/>) are held to
/// a limit: what the hang watchdog leaves the test, less <see cref="WatchdogMargin"/>, or, for the
/// first read and <see cref="WaitForIdleAsync()"/>, the limit the test gave the open
/// (<c>stepLimit</c>). A step past it fails with a
/// <see cref="TimeoutException"/> that names it and says what
/// ran meanwhile (the git calls that ended, the queue's operation), so that a step that stalls is
/// told from one that crawls before the watchdog stops the whole test process, and a step that
/// only crawls fails only when the watchdog was about to stop the test anyway.
/// </para>
/// <para>
/// The awaits the harness does not hold to a limit, above all the operations a test runs through
/// <see cref="Operations"/>, get a stall note instead: a harness still open when the watchdog leaves
/// its test no more than <see cref="WatchdogMargin"/> writes one line to the standard error and
/// output, as the watchdog does, with the test and its counted time, the repository, the queue's
/// operation and the git calls that ended since the open. The watchdog's stop that follows then has
/// the stalled operation named; a test that ends in time is not changed by it.
/// </para>
/// </summary>
internal sealed class OperationsHarness : IDisposable
{
    /// <summary>How long before the hang watchdog's limit a step of the harness gives up, and its stall note is written: time for the test to fail and end on its own.</summary>
    public static readonly TimeSpan WatchdogMargin = TimeSpan.FromSeconds(15);

    /// <summary>The least a step is given when the watchdog leaves the test less than that.</summary>
    private static readonly TimeSpan LeastStepLimit = TimeSpan.FromSeconds(5);

    /// <summary>The longest the stall note's watch waits at once before it reads the test's counted time again (a wait's limit in milliseconds is an int).</summary>
    private static readonly TimeSpan LongestNoteWait = TimeSpan.FromHours(1);

    private static readonly string GitPath = GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");

    /// <summary>The limit the test gave the harness's steps, or null for the watchdog's.</summary>
    private readonly TimeSpan? _stepLimit;

    /// <summary>Set by <see cref="Dispose"/>: ends every stall note's watch.</summary>
    private readonly ManualResetEventSlim _closed = new();

    /// <summary>The unique id of the test that opened the harness, null outside a test; and its name.</summary>
    private readonly string? _testId;

    private readonly string _testName;

    /// <summary>How many git calls had ended when the open was done.</summary>
    private int _callsAtOpen;

    /// <summary>The prompt broker <see cref="OpenAsync(TempRepo, Func{DialogHostViewModel, IGitPromptHandler})"/> made, disposed with the harness.</summary>
    private IDisposable? _broker;

    private OperationsHarness(TempRepo repo, RepositoryViewModel repository, DialogHostViewModel host, TabServices services, RecordingLog log, FakeClipboard clipboard, FakeFileLauncher launcher, GitCallLog calls, TimeSpan? stepLimit)
    {
        _stepLimit = stepLimit;
        _testId = TestContext.Current.Test?.UniqueID;
        _testName = TestContext.Current.Test?.TestDisplayName ?? "no test";
        Calls = calls;
        Repo = repo;
        Repository = repository;
        DialogHost = host;
        Services = services;
        Log = log;
        Clipboard = clipboard;
        Launcher = launcher;
        Dialogs = new ScriptedDialogs(host);
    }

    /// <summary>The scenario repository, for git's state (<see cref="TempRepo.GitAsync"/>, <see cref="TempRepo.RefTargetAsync"/>, …).</summary>
    public TempRepo Repo { get; }

    /// <summary>Every git call the repository's view model and its operations made, as the app's session records them (<see cref="TabServices.GitCalls"/>).</summary>
    public GitCallLog Calls { get; }

    /// <summary>The repository's view model, started.</summary>
    public RepositoryViewModel Repository { get; }

    /// <summary>The operations under test.</summary>
    public RepositoryOperations Operations => Repository.Operations;

    /// <summary>The dialog layer the operations open their dialogs on.</summary>
    public DialogHostViewModel DialogHost { get; }

    /// <summary>The script that answers the dialogs, and the dialogs it saw.</summary>
    public ScriptedDialogs Dialogs { get; }

    public TabServices Services { get; }

    public RecordingLog Log { get; }

    /// <summary>What "Copy …" put on the clipboard.</summary>
    public FakeClipboard Clipboard { get; }

    /// <summary>What "Open file" and "Show in folder" asked for.</summary>
    public FakeFileLauncher Launcher { get; }

    /// <summary>What the status bar says about the operations (4e).</summary>
    public string StatusText => Repository.OperationStatus.Text;

    public OperationStatusTone StatusTone => Repository.OperationStatus.Tone;

    /// <summary>The last operation's result.</summary>
    public OperationResult? LastResult => Repository.Queue.LastResult;

    /// <summary>The settings the operations read and write (the stash dialog's last choice, 6.2).</summary>
    public MainWindowViewModelTests.FakeSettings Settings => (MainWindowViewModelTests.FakeSettings)Services.Settings;

    /// <summary>Opens <paramref name="repo"/> as a tab would and reads it.</summary>
    public static Task<OperationsHarness> OpenAsync(TempRepo repo) => OpenAsync(repo, prompts: null);

    /// <summary>Opens <paramref name="repo"/> as <see cref="OpenAsync(TempRepo)"/> does, with <paramref name="session"/> as the app's session (its graph filters, docs/phase-5-design.md 8.4). Added by phase 5's WP7.</summary>
    public static Task<OperationsHarness> OpenAsync(TempRepo repo, ISessionStore session) => OpenAsync(repo, prompts: null, session: session);

    /// <summary>
    /// Opens <paramref name="repo"/> as <see cref="OpenAsync(TempRepo)"/> does, with its git calls
    /// going through what <paramref name="wrap"/> makes of the real repository: a test that needs
    /// a git method to answer what the installed git never answers (a revert that stops with
    /// <c>REVERT_HEAD</c>, D117) changes that one method.
    /// </summary>
    public static Task<OperationsHarness> OpenWrappedAsync(TempRepo repo, Func<IGitRepository, IGitRepository> wrap) => OpenAsync(repo, prompts: null, wrap);

    /// <summary>
    /// Opens <paramref name="repo"/> as <see cref="OpenAsync(TempRepo)"/> does, with the undo
    /// histories of <paramref name="session"/>'s services: as a second tab of the same app session,
    /// on another worktree of the repository or on the same one (docs/phase-5-design.md 7.1).
    /// </summary>
    public static Task<OperationsHarness> OpenAsync(TempRepo repo, TabServices session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return OpenAsync(repo, prompts: null, undoHistories: session.UndoHistories);
    }

    /// <summary>
    /// Opens <paramref name="repo"/> as <see cref="OpenAsync(TempRepo)"/> does, with the app's
    /// prompt broker (the real askpass helper, design 3.1) answering git's and ssh's prompts
    /// through the handler <paramref name="prompts"/> makes for the harness's dialog layer:
    /// <c>host =&gt; new PromptDialogs(host)</c> shows the app's prompt dialogs there, for
    /// <see cref="Dialogs"/> to answer; a <see cref="ScriptedPromptHandler"/> answers without any.
    /// Null gives no broker, as <see cref="OpenAsync(TempRepo)"/>. Added by WP7-remotes.
    /// <paramref name="undoHistories"/> are the app session's undo histories, new ones when null.
    /// <paramref name="hosting"/> is the tabs' hosting session (docs/phase-7a-design.md 4.1), also
    /// the broker's credential source; null: none, as before. <paramref name="gitEnvironment"/> is
    /// the session's git environment (D220), which the runner applies to every call and the broker
    /// reads as what git already gets (4.6), as an app session hands them both; null: none.
    /// <paramref name="stepLimit"/> holds the first read and <see cref="WaitForIdleAsync()"/> to
    /// that limit instead of the watchdog's (the harness's own tests); the open before the first
    /// read keeps the watchdog's, so that a short limit meets only the step a test holds back.
    /// </summary>
    public static async Task<OperationsHarness> OpenAsync(TempRepo repo, Func<DialogHostViewModel, IGitPromptHandler>? prompts, Func<IGitRepository, IGitRepository>? wrap = null, ISessionStore? session = null, UndoHistories? undoHistories = null, IHostingSession? hosting = null, IReadOnlyDictionary<string, string?>? gitEnvironment = null, TimeSpan? stepLimit = null)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var calls = new GitCallLog();
        IGitRepository git = await WithinLimitAsync(
            GitRepository.OpenAsync(new GitRunner(GitPath, calls, environment: gitEnvironment), repo.Path, TestContext.Current.CancellationToken),
            $"opening {repo.Path}",
            limit: null,
            calls,
            queue: null);
        git = wrap?.Invoke(git) ?? git;
        var log = new RecordingLog();
        var host = new DialogHostViewModel();
        var clipboard = new FakeClipboard();
        var launcher = new FakeFileLauncher();
        var settings = new MainWindowViewModelTests.FakeSettings();
        var dates = new DateDisplay(TimeZoneInfo.Utc);
        var broker = prompts is null ? null : PromptDialogs.CreateBroker(prompts(host), log, credentials: hosting, sessionEnvironment: gitEnvironment);
        var services = new TabServices(new NoRepositories(), new NoFolders(), settings, session ?? new MemorySession(), dates, log, dialogs: host, prompts: broker, launcher: launcher, clipboard: clipboard, gitCalls: calls, undoHistories: undoHistories, hosting: hosting);
        var repository = new RepositoryViewModel(git, settings, dates, log, dialogs: host, services: services);
        var harness = new OperationsHarness(repo, repository, host, services, log, clipboard, launcher, calls, stepLimit) { _broker = broker as IDisposable };
        try
        {
            await WithinLimitAsync(repository.StartAsync(), $"the first read of {repo.Path}", stepLimit, calls, repository.Queue);
        }
        catch
        {
            harness.Dispose();
            throw;
        }

        harness._callsAtOpen = calls.Snapshot().Count;
        _ = harness.StartStallNote(after: null, to: null);
        return harness;
    }

    /// <summary>The full id <paramref name="revision"/> names now.</summary>
    public async Task<string> ShaAsync(string revision) => (await Repo.GitAsync("rev-parse", "--verify", revision)).StandardOutput.Trim();

    /// <summary>Selects these commits in the graph, as clicks and Ctrl+clicks would (4h).</summary>
    public void Select(params string[] shas) => Repository.Selection = new GraphSelection(shas);

    /// <summary>Waits until the repository's queue has nothing running or waiting, within the harness's step limit.</summary>
    /// <exception cref="TimeoutException">The queue was still busy when the limit was up; the message says with what.</exception>
    public Task WaitForIdleAsync() => WaitForIdleWithinAsync(_stepLimit);

    /// <summary>Waits as <see cref="WaitForIdleAsync()"/> does, within <paramref name="limit"/> (the harness's own tests).</summary>
    /// <exception cref="TimeoutException">The queue was still busy when the limit was up; the message says with what.</exception>
    public Task WaitForIdleAsync(TimeSpan limit) => WaitForIdleWithinAsync(limit);

    private Task WaitForIdleWithinAsync(TimeSpan? limit) =>
        WithinLimitAsync(Repository.Queue.WhenIdleAsync(), $"the queue of {Repo.Path} to be idle", limit, Calls, Repository.Queue);

    /// <summary>
    /// Waits until the repository's watcher is quiet (<see cref="RepositoryViewModel.IsWatcherQuiet"/>):
    /// no change it saw waits, and no refresh or status read runs. Polled every 15 ms, at most
    /// <see cref="ShellDriver.DefaultWaitTimeout"/> (docs/phase-7a-design.md 5.3).
    /// </summary>
    /// <exception cref="TimeoutException">The watcher was still busy when the time was up.</exception>
    public async Task WaitForWatcherQuietAsync()
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (!Repository.IsWatcherQuiet)
        {
            if (waited.Elapsed > ShellDriver.DefaultWaitTimeout)
            {
                throw new TimeoutException($"Timed out after {ShellDriver.DefaultWaitTimeout.TotalSeconds:F0} s waiting for the repository watcher to be quiet.");
            }

            await Task.Delay(15, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Waits until the opening's clean-up has read the backups it might remove (docs/phase-4-design.md
    /// 9 (b)): <see cref="ShellDriver.OpeningCleanUpRead"/> recorded outside every operation, or the
    /// line saying that the read failed. <see cref="RepositoryViewModel.StartAsync"/> starts that read
    /// once the first load is done and does not await it, and
    /// <see cref="RepositoryViewModel.IsWatcherQuiet"/> does not cover it, so a step that marks its
    /// calls right after the opening waits for it first: a read still running never straddles a mark
    /// (S8, docs/phase-7a-design.md 5.3). Polled every 15 ms, at most <see cref="ShellDriver.DefaultWaitTimeout"/>.
    /// </summary>
    /// <exception cref="TimeoutException">Neither came in time.</exception>
    public async Task WaitForOpeningCleanUpReadAsync()
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (!Calls.Snapshot().Any(call => call.OperationId is null && call.CommandText == ShellDriver.OpeningCleanUpRead)
            && !Log.Messages.Any(message => message.EndsWith(": the backups to clean up could not be read; they stay.", StringComparison.Ordinal)))
        {
            if (waited.Elapsed > ShellDriver.DefaultWaitTimeout)
            {
                throw new TimeoutException($"Timed out after {ShellDriver.DefaultWaitTimeout.TotalSeconds:F0} s waiting for the opening's clean-up to read the backups.");
            }

            await Task.Delay(15, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>The mark of a step's git calls, the highest <see cref="GitCallRecord.Sequence"/> so far (0 for none), taken once the watcher is quiet (S8, <see cref="GitCallBudget"/>).</summary>
    public async Task<long> QuietCallMarkAsync()
    {
        await WaitForWatcherQuietAsync();
        return Calls.Snapshot().Select(call => call.Sequence).DefaultIfEmpty(0).Max();
    }

    /// <summary>The git calls after <paramref name="sequence"/>, read once the watcher is quiet again.</summary>
    public async Task<IReadOnlyList<GitCallRecord>> QuietCallsSinceAsync(long sequence)
    {
        await WaitForWatcherQuietAsync();
        return [.. Calls.Snapshot().Where(call => call.Sequence > sequence)];
    }

    /// <summary>
    /// Fails the test with what the dialog script noted: answers that did not fit, dialogs that
    /// came unscripted (error dialogs aside), and answers that were never used.
    /// </summary>
    public void AssertScriptFollowed()
    {
        Assert.Empty(Dialogs.Problems);
        Assert.Equal(0, Dialogs.Unused);
    }

    /// <summary>
    /// A step's limit when the test gives none: what the hang watchdog leaves the current test, less
    /// <see cref="WatchdogMargin"/>, and at least <see cref="LeastStepLimit"/>; outside a test the
    /// watchdog's whole limit less the margin.
    /// </summary>
    public static TimeSpan StepLimit()
    {
        var limit = (HangWatchdogAttribute.TimeLeftForCurrentTest() ?? HangWatchdogAttribute.TestLimit) - WatchdogMargin;
        return limit > LeastStepLimit ? limit : LeastStepLimit;
    }

    /// <summary>
    /// Writes the stall note to <paramref name="to"/> alone once <paramref name="after"/> has passed
    /// with the harness still open and its test running (the harness's own tests). Completes when
    /// the note's watch has ended.
    /// </summary>
    internal Task StartStallNote(TimeSpan after, TextWriter to)
    {
        ArgumentNullException.ThrowIfNull(to);
        return StartStallNote((TimeSpan?)after, to);
    }

    public void Dispose()
    {
        _closed.Set();
        Dialogs.Dispose();
        Repository.Dispose();
        _broker?.Dispose();
    }

    /// <summary>
    /// Awaits <paramref name="step"/> within <paramref name="limit"/> (<see cref="StepLimit"/> when
    /// null). Past it, a <see cref="TimeoutException"/> names the step (<paramref name="what"/>) and
    /// says what ran meanwhile: the git calls that ended, and what <paramref name="queue"/> runs. The
    /// step itself goes on.
    /// </summary>
    private static async Task WithinLimitAsync(Task step, string what, TimeSpan? limit, GitCallLog calls, IOperationQueue? queue)
    {
        var given = limit ?? StepLimit();
        var before = calls.Snapshot().Count;
        try
        {
            await step.WaitAsync(given);
        }
        catch (TimeoutException ex) when (!ReferenceEquals(ex, step.Exception?.InnerException))
        {
            // The limit's, not one the step threw itself.
            throw new TimeoutException(StepPastItsLimit(what, given, calls.Snapshot().Skip(before).ToList(), queue), ex);
        }
    }

    /// <inheritdoc cref="WithinLimitAsync(Task, string, TimeSpan?, GitCallLog, IOperationQueue?)"/>
    private static async Task<T> WithinLimitAsync<T>(Task<T> step, string what, TimeSpan? limit, GitCallLog calls, IOperationQueue? queue)
    {
        await WithinLimitAsync((Task)step, what, limit, calls, queue);
        return await step;
    }

    /// <summary>What a step past its limit says: which step, the git calls that ended during it (none: a stall; many, the last just now: a crawl) and the queue's operation.</summary>
    private static string StepPastItsLimit(string what, TimeSpan limit, IReadOnlyList<GitCallRecord> ended, IOperationQueue? queue)
    {
        var running = queue is null ? string.Empty : "; " + QueueRuns(queue);
        return string.Create(CultureInfo.InvariantCulture, $"OperationsHarness: {what} did not end within {limit.TotalSeconds:0.#} s: {CallsEnded(ended, "meanwhile")}{running}.");
    }

    /// <summary>The git calls that ended <paramref name="when"/>: how many, and the last with its time.</summary>
    private static string CallsEnded(IReadOnlyList<GitCallRecord> ended, string when) =>
        ended.Count == 0
            ? $"no git call ended {when}"
            : string.Create(CultureInfo.InvariantCulture, $"{ended.Count} git call(s) ended {when}, the last \"{ended[^1].CommandText}\" after {ended[^1].Duration.TotalSeconds:0.0} s");

    /// <summary>What <paramref name="queue"/> runs and how many operations wait behind it.</summary>
    private static string QueueRuns(IOperationQueue queue) => queue.Current is { } current
        ? string.Create(CultureInfo.InvariantCulture, $"the queue runs \"{current.Title}\", {queue.Waiting} waiting")
        : string.Create(CultureInfo.InvariantCulture, $"the queue runs nothing, {queue.Waiting} waiting");

    /// <summary>
    /// Starts the stall note's watch on a thread of its own, not a timer: the hang watchdog's reason,
    /// a stall can be a thread pool with no thread to spare. With <paramref name="after"/> null the
    /// note is due when the watchdog leaves the test no more than <see cref="WatchdogMargin"/>, and
    /// goes to the standard error and output; else after that time, to <paramref name="to"/>. The
    /// returned task completes when the watch has ended.
    /// </summary>
    private Task StartStallNote(TimeSpan? after, TextWriter? to)
    {
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var watch = new Thread(() =>
        {
            try
            {
                WatchForStall(after, to);
            }
            catch
            {
                // A note that cannot be read or written is lost: it never fails the test process.
            }
            finally
            {
                ended.TrySetResult();
            }
        })
        {
            IsBackground = true,
            Name = "OperationsHarness stall note",
        };
        watch.Start();
        return ended.Task;
    }

    /// <summary>Waits until the note is due or the harness is disposed, then writes the note if the harness's test still runs.</summary>
    private void WatchForStall(TimeSpan? after, TextWriter? to)
    {
        if (_testId is null)
        {
            return;
        }

        if (after is { } given)
        {
            if (_closed.Wait(given))
            {
                return;
            }
        }
        else
        {
            while (HangWatchdogAttribute.CountedTimeOf(_testId) is { } counted)
            {
                var due = HangWatchdogAttribute.TestLimit - WatchdogMargin - counted;
                if (due <= TimeSpan.Zero)
                {
                    break;
                }

                if (_closed.Wait(due < LongestNoteWait ? due : LongestNoteWait))
                {
                    return;
                }
            }
        }

        if (_closed.IsSet || HangWatchdogAttribute.CountedTimeOf(_testId) is not { } now)
        {
            return;
        }

        var note = string.Create(
            CultureInfo.InvariantCulture,
            $"OperationsHarness: {_testName} has run {now.TotalSeconds:0} s counted with its harness on {Repo.Path} still open: {QueueRuns(Repository.Queue)}; {CallsEnded([.. Calls.Snapshot().Skip(_callsAtOpen)], "since the open")}.");
        if (to is not null)
        {
            to.WriteLine(note);
            return;
        }

        // Both streams, as the hang watchdog writes its own message.
        Console.Error.WriteLine(note);
        Console.Error.Flush();
        Console.Out.WriteLine(note);
        Console.Out.Flush();
    }

    private sealed class MemorySession : ISessionStore
    {
        public SessionState Current { get; private set; } = new();

        public void Update(Func<SessionState, SessionState> change) => Current = change(Current);
    }

    private sealed class NoRepositories : IRepositoryProvider
    {
        public Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IGitRepository> CloneAsync(
            string url,
            string destination,
            IProgress<CloneProgress>? progress,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string?>? environment = null) => throw new NotSupportedException();
    }

    private sealed class NoFolders : IFolderPicker
    {
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
}
