using VisualCommit.App.ViewModels.GitSetup;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Git;

namespace VisualCommit.App.Services;

/// <summary>
/// The app's way to the git runner (D45). Git is looked for once the window exists, and again on
/// "Check again", and may not be found; services hold this instead of a runner and ask for the
/// runner when they need it. The session's search goes through an <see cref="IGitSetupProbe"/>,
/// told what the lookup function gives when each search starts. A search that starts swaps in a
/// new pending <see cref="Detection"/>, so that a runner asked for meanwhile is the new search's; a
/// call while one runs joins it when the lookup gives what that search was told, and otherwise
/// (a git chosen meanwhile, D423) starts a search of its own once that one has ended.
/// </summary>
public sealed class GitAccess
{
    /// <summary>What <see cref="GetRunnerAsync"/> says of a git that was found but does not work, and the settings screen of no git at all (docs/phase-6-design.md 7.4).</summary>
    public const string GitNotWorking = "Git is not working on this computer.";

    /// <summary>What <see cref="GetRunnerAsync"/> says when no git was found.</summary>
    public const string GitNotFound = "Git was not found on this computer.";

    private readonly IGitSetupProbe? _probe;
    private readonly Func<GitLookup>? _lookup;
    private readonly IGitCallLog? _callLog;
    private readonly IAppLog _log;
    private readonly TimeProvider? _recordClock;
    private readonly IReadOnlyDictionary<string, string?>? _environment;
    private readonly Lock _gate = new();

    /// <summary>The latest search's result, pending until it ends; a fixed search's task for the first constructor.</summary>
    private Task<GitDetection> _detection;

    /// <summary>The session's search that runs now, which a call joins; null while none runs and for a fixed search.</summary>
    private Task<GitDetection>? _running;

    /// <summary>What <see cref="_running"/> was told; null when reading the lookup failed.</summary>
    private GitLookup? _runningLookup;

    /// <summary>The pending result of the session's first search, made with the access so that <see cref="Detection"/> is there at once.</summary>
    private TaskCompletionSource<GitDetection>? _first;

    /// <summary>1 once the stand-in's warning was logged.</summary>
    private int _standInWarned;

    private GitDetection? _known;

    /// <summary>
    /// One fixed search, <paramref name="detection"/>, for tests of the services that wait on it:
    /// <see cref="DetectAsync"/> answers with it, <see cref="ProbeAsync"/> with "not found", and
    /// <see cref="ReadGuidance"/> names the running platform without winget or a distribution,
    /// reading no file and no variable. <see cref="Known"/> is the given task's result once it has
    /// completed, so that code which awaited the task never finds it still unknown.
    /// </summary>
    public GitAccess(Task<GitDetection> detection)
    {
        ArgumentNullException.ThrowIfNull(detection);
        _log = NullAppLog.Instance;
        _detection = detection;
        _ = detection.ContinueWith(
            ended => Ended(ended.IsCompletedSuccessfully ? ended.Result : null),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// The session's search: <paramref name="probe"/> looks for git with what
    /// <paramref name="lookup"/> gives when each search starts, recording its calls in
    /// <paramref name="callLog"/> with the times of <paramref name="recordClock"/>, and running them
    /// with the session's git environment <paramref name="environment"/>.
    /// </summary>
    public GitAccess(
        IGitSetupProbe probe,
        Func<GitLookup> lookup,
        IGitCallLog? callLog,
        IAppLog log,
        TimeProvider? recordClock,
        IReadOnlyDictionary<string, string?>? environment)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(log);
        _probe = probe;
        _lookup = lookup;
        _callLog = callLog;
        _log = log;
        _recordClock = recordClock;
        _environment = environment;
        _first = new TaskCompletionSource<GitDetection>(TaskCreationOptions.RunContinuationsAsynchronously);
        _detection = _first.Task;
    }

    /// <summary>What the latest search for git found: pending until it has ended.</summary>
    public Task<GitDetection> Detection
    {
        get
        {
            lock (_gate)
            {
                return _detection;
            }
        }
    }

    /// <summary>What the last search that ended found; null before one has ended, and when the last failed or was cancelled.</summary>
    public GitDetection? Known => Volatile.Read(ref _known)
        ?? (_probe is null && Detection.IsCompletedSuccessfully ? Detection.Result : null);

    /// <summary>
    /// Raised once each search has ended, however it ended, on the thread that ended it. A listener
    /// that throws is logged, and changes neither how the search ended nor what the other
    /// listeners hear.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Looks for git: a call while no search runs starts one (<paramref name="cancellationToken"/>
    /// then cancels it), first swapping in a new pending <see cref="Detection"/> after the first;
    /// a call while one runs joins it when the lookup gives what that search was told, waiting for
    /// it at most until its own token is cancelled. A call whose lookup gives something else starts
    /// a search of its own as one that starts while none runs does, but asks the probe only once the
    /// running search has ended, however it ended. A fixed search answers with its task.
    /// </summary>
    public Task<GitDetection> DetectAsync(CancellationToken cancellationToken = default)
    {
        if (_probe is null)
        {
            return Detection.WaitAsync(cancellationToken);
        }

        // What the search is told is read when it starts; a lookup that cannot be read fails the
        // search, as before, and is never joined.
        GitLookup? lookup = null;
        Exception? unread = null;
        try
        {
            lookup = _lookup!();
        }
        catch (Exception ex)
        {
            unread = ex;
        }

        TaskCompletionSource<GitDetection> search;
        Task<GitDetection>? before = null;
        lock (_gate)
        {
            if (_running is { IsCompleted: false } running)
            {
                if (lookup is not null && Equals(lookup, _runningLookup))
                {
                    return running.WaitAsync(cancellationToken);
                }

                before = running;
            }

            if (_first is { } first)
            {
                search = first;
                _first = null;
            }
            else
            {
                search = new TaskCompletionSource<GitDetection>(TaskCreationOptions.RunContinuationsAsynchronously);
                _detection = search.Task;
            }

            _running = search.Task;
            _runningLookup = lookup;
        }

        // Started outside the lock: a probe that answers at once ends the search, and tells its
        // listeners, before this returns.
        return SearchAsync(search, lookup, unread, before, cancellationToken);
    }

    /// <summary>Waits for the latest search for git to finish and returns the runner.</summary>
    /// <exception cref="GitUnavailableException">No usable git was found.</exception>
    public async Task<IGitRunner> GetRunnerAsync(CancellationToken cancellationToken = default)
    {
        var detection = await Detection.WaitAsync(cancellationToken).ConfigureAwait(false);
        return detection.Runner ?? throw new GitUnavailableException(detection switch
        {
            { Availability: GitAvailability.NotFound, ChosenMissing: { } chosen } => GitSetupTexts.ChosenNotUsed(chosen),
            { Availability: GitAvailability.NotFound } => GitNotFound,
            { Availability: GitAvailability.TooOld } => $"Git {detection.Version} is too old; VisualCommit needs {GitVersion.Minimum} or newer.",
            _ => GitNotWorking,
        });
    }

    /// <summary>
    /// Asks the probe what <paramref name="path"/> would give as the chosen git, with the session's
    /// call log, log, clock and environment; changes nothing here. A fixed search answers "not found".
    /// </summary>
    public Task<GitDetection> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _probe is null
            ? Task.FromResult(new GitDetection(GitAvailability.NotFound, null, null, null))
            : _probe.DetectAsync(new GitLookup(path, null), _callLog, _log, _recordClock, _environment, cancellationToken);
    }

    /// <summary>The probe's guidance facts; for a fixed search the running platform's, with no winget and no distribution.</summary>
    public GitGuidance ReadGuidance() => _probe?.ReadGuidance() ?? new GitGuidance(RunningPlatform, null, null);

    /// <summary>The platform this process runs on, as the guidance names it.</summary>
    public static GuidancePlatform RunningPlatform =>
        OperatingSystem.IsWindows() ? GuidancePlatform.Windows
        : OperatingSystem.IsMacOS() ? GuidancePlatform.MacOS
        : GuidancePlatform.Linux;

    /// <summary>
    /// Runs one search of the session, told <paramref name="lookup"/> (or failing with
    /// <paramref name="unread"/>, the lookup's own failure), after <paramref name="before"/>, the
    /// search that ran when it was asked for, if any, has ended; and hands its result, its
    /// cancellation or its failure to <paramref name="search"/> as well as to the caller. It resumes
    /// where it was started (the window's start-up or "Check again", on the UI thread), so
    /// <see cref="Detection"/> ends there.
    /// </summary>
    private async Task<GitDetection> SearchAsync(
        TaskCompletionSource<GitDetection> search,
        GitLookup? lookup,
        Exception? unread,
        Task<GitDetection>? before,
        CancellationToken cancellationToken)
    {
        GitDetection? found = null;
        try
        {
            if (before is not null)
            {
                // Its outcome is its own: whoever started it reports it.
                await Task.WhenAny(before).WaitAsync(cancellationToken);
            }

            if (unread is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(unread);
            }

            WarnOfStandIn(lookup!);
            found = await _probe!.DetectAsync(lookup!, _callLog, _log, _recordClock, _environment, cancellationToken);
            Volatile.Write(ref _known, found);
            search.TrySetResult(found);
            return found;
        }
        catch (OperationCanceledException)
        {
            Volatile.Write(ref _known, null);
            search.TrySetCanceled(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _known, null);
            search.TrySetException(ex);
            throw;
        }
        finally
        {
            Ended(found);
        }
    }

    /// <summary>The stand-in's warning (a test switch), once, at the first search that is told of one.</summary>
    private void WarnOfStandIn(GitLookup lookup)
    {
        if (lookup.StandIn is not { } standIn || Interlocked.Exchange(ref _standInWarned, 1) != 0)
        {
            return;
        }

        _log.Warning(GitLocator.IsRunnablePath(standIn)
            ? $"{GitLocator.StandInVariable} is set (a test switch): unless Settings names a Git, Git is looked for only at {standIn}."
            : $"{GitLocator.StandInVariable} is set (a test switch): unless Settings names a Git, this computer is treated as having no Git.");
    }

    /// <summary>
    /// A search ended with <paramref name="found"/> (null when it failed or was cancelled): keeps it
    /// and says so to each listener in turn. It never throws: it runs in the search's
    /// <c>finally</c>, where a listener's exception would replace the search's own result.
    /// </summary>
    private void Ended(GitDetection? found)
    {
        if (found is not null)
        {
            Volatile.Write(ref _known, found);
        }

        if (Changed is not { } changed)
        {
            return;
        }

        foreach (var listener in changed.GetInvocationList())
        {
            try
            {
                ((EventHandler)listener)(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _log.Error("A listener of the search for git failed.", ex);
            }
        }
    }
}
