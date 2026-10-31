using System.Globalization;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Git;

namespace VisualCommit.App.Services;

/// <summary>
/// Opens, initialises and clones repositories with real git, once git has been found (D45). Every
/// repository it opens names its backups by the session's backup clock (docs/phase-5-design.md 2.3).
/// </summary>
public sealed class GitRepositoryProvider : IRepositoryProvider
{
    private readonly GitAccess _git;
    private readonly IAppLog _log;
    private readonly BackupClock _backupClock;

    /// <param name="git">The way to the git runner.</param>
    /// <param name="log">The app's log, which the repositories write to.</param>
    /// <param name="backupClock">The clock every repository names its backups by, and its clean-up's "now"; the system clock by default.</param>
    public GitRepositoryProvider(GitAccess git, IAppLog? log = null, BackupClock? backupClock = null)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
        _log = log ?? NullAppLog.Instance;
        _backupClock = backupClock ?? BackupClock.System;
        // R8's test switch (D262), said once per session: the session makes one provider.
        if (WatcherLagLine(GitRepository.WatcherLag) is { } line)
        {
            _log.Info(line);
        }
    }

    /// <summary>
    /// The Info line that says R8's test switch is on (docs/phase-7a-design.md 5.2, D262), with the
    /// lag in whole milliseconds; null when <paramref name="lag"/> is not positive. Public for the
    /// tests (the app has no <c>InternalsVisibleTo</c>).
    /// </summary>
    public static string? WatcherLagLine(TimeSpan lag)
    {
        if (lag <= TimeSpan.Zero)
        {
            return null;
        }

        var milliseconds = ((long)lag.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        return $"{GitRepository.WatcherLagVariable}={milliseconds}: every repository watcher holds each file-system event for {milliseconds} ms before it counts it (a test switch, D262).";
    }

    /// <remarks>
    /// The whole open runs on the thread pool (docs/phase-6-design.md 2.10, D59), so that
    /// <c>GitRepository.OpenAsync</c>'s own folder check never holds the UI thread, also for a
    /// folder on a share that does not answer: a dashboard row, a restored tab, the welcome page.
    /// </remarks>
    public async Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        var runner = await _git.GetRunnerAsync(cancellationToken).ConfigureAwait(false);
        return WithClock(await Task.Run(() => GitRepository.OpenAsync(runner, path, cancellationToken, _log), cancellationToken).ConfigureAwait(false));
    }

    /// <remarks>On the thread pool too, as <see cref="OpenAsync"/>.</remarks>
    public async Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default)
    {
        var runner = await _git.GetRunnerAsync(cancellationToken).ConfigureAwait(false);
        return WithClock(await Task.Run(() => GitRepository.InitAsync(runner, path, cancellationToken, _log), cancellationToken).ConfigureAwait(false));
    }

    /// <remarks>
    /// <paramref name="environment"/> holds the variables of the clone's prompt scope (phase 3
    /// design 3.3), which <c>WelcomeViewModel</c> opens; git gets them with the clone.
    /// </remarks>
    public async Task<IGitRepository> CloneAsync(
        string url,
        string destination,
        IProgress<CloneProgress>? progress,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        // Nothing here needs the UI thread, so the app can wait for a clone to clean up while it
        // closes (MainWindowViewModel.CloseAll) without the clone waiting for the UI thread.
        var runner = await _git.GetRunnerAsync(cancellationToken).ConfigureAwait(false);
        return WithClock(await GitRepository.CloneAsync(runner, url, destination, progress, cancellationToken, _log, environment).ConfigureAwait(false));
    }

    private GitRepository WithClock(GitRepository repository)
    {
        repository.BackupClock = _backupClock;
        return repository;
    }
}
