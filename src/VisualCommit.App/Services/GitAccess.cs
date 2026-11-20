using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.Services;

/// <summary>
/// The app's way to the git runner (D45). Git is looked for once, after the window exists, and
/// may not be found; services hold this instead of a runner and ask for the runner when they
/// need it. The session's search goes through an <see cref="IGitSetupProbe"/>, told what the
/// lookup function gives when the search starts; the first <see cref="DetectAsync"/> starts it,
/// and every later call answers with that same search.
/// </summary>
public sealed class GitAccess
{
    /// <summary>What <see cref="GetRunnerAsync"/> says of a git that was found but does not work, and the settings screen of no git at all (docs/phase-6-design.md 7.4).</summary>
    public const string GitNotWorking = "Git is not working on this computer.";

    private readonly IGitSetupProbe? _probe;
    private readonly Func<GitLookup>? _lookup;
    private readonly IGitCallLog? _callLog;
    private readonly IAppLog _log;
    private readonly TimeProvider? _recordClock;
    private readonly IReadOnlyDictionary<string, string?>? _environment;

    /// <summary>The session's search, pending until the first <see cref="DetectAsync"/> has ended; null for a fixed search.</summary>
    private readonly TaskCompletionSource<GitDetection>? _search;

    /// <summary>1 once the session's search has started.</summary>
    private int _started;

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
        Detection = detection;
        _ = detection.ContinueWith(
            ended => Ended(ended.IsCompletedSuccessfully ? ended.Result : null),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// The session's search: <paramref name="probe"/> looks for git with what
    /// <paramref name="lookup"/> gives when the search starts, recording its calls in
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
        _search = new TaskCompletionSource<GitDetection>(TaskCreationOptions.RunContinuationsAsynchronously);
        Detection = _search.Task;
    }

    /// <summary>What was found when the app looked for git: pending until the search has ended.</summary>
    public Task<GitDetection> Detection { get; }

    /// <summary>What the search that ended found; null before it has ended, and when it failed or was cancelled.</summary>
    public GitDetection? Known => Volatile.Read(ref _known)
        ?? (_search is null && Detection.IsCompletedSuccessfully ? Detection.Result : null);

    /// <summary>
    /// Raised once the search has ended, however it ended, on the thread that ended it. A listener
    /// that throws is logged, and changes neither how the search ended nor what the other
    /// listeners hear.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Looks for git: the first call starts the search (<paramref name="cancellationToken"/> then
    /// cancels it), and every later call answers with that search, waiting for it at most until its
    /// own <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public Task<GitDetection> DetectAsync(CancellationToken cancellationToken = default)
    {
        if (_search is null || Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Detection.WaitAsync(cancellationToken);
        }

        return SearchAsync(_search, cancellationToken);
    }

    /// <summary>Waits for the search for git to finish and returns the runner.</summary>
    /// <exception cref="GitUnavailableException">No usable git was found.</exception>
    public async Task<IGitRunner> GetRunnerAsync(CancellationToken cancellationToken = default)
    {
        var detection = await Detection.WaitAsync(cancellationToken).ConfigureAwait(false);
        return detection.Runner ?? throw new GitUnavailableException(detection.Availability switch
        {
            GitAvailability.NotFound => "Git was not found on this computer.",
            GitAvailability.TooOld => $"Git {detection.Version} is too old; VisualCommit needs {GitVersion.Minimum} or newer.",
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
    private static GuidancePlatform RunningPlatform =>
        OperatingSystem.IsWindows() ? GuidancePlatform.Windows
        : OperatingSystem.IsMacOS() ? GuidancePlatform.MacOS
        : GuidancePlatform.Linux;

    /// <summary>
    /// Runs the session's search and hands its result, its cancellation or its failure to
    /// <paramref name="search"/> as well as to the caller. It resumes where it was started (the
    /// window's start-up, on the UI thread), so <see cref="Detection"/> ends there.
    /// </summary>
    private async Task<GitDetection> SearchAsync(TaskCompletionSource<GitDetection> search, CancellationToken cancellationToken)
    {
        GitDetection? found = null;
        try
        {
            found = await _probe!.DetectAsync(_lookup!(), _callLog, _log, _recordClock, _environment, cancellationToken);
            Volatile.Write(ref _known, found);
            search.TrySetResult(found);
            return found;
        }
        catch (OperationCanceledException)
        {
            search.TrySetCanceled(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            search.TrySetException(ex);
            throw;
        }
        finally
        {
            Ended(found);
        }
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
