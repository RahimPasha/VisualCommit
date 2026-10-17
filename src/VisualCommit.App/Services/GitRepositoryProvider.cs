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
    }

    public async Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        var runner = await _git.GetRunnerAsync(cancellationToken).ConfigureAwait(false);
        return WithClock(await GitRepository.OpenAsync(runner, path, cancellationToken, _log).ConfigureAwait(false));
    }

    public async Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default)
    {
        var runner = await _git.GetRunnerAsync(cancellationToken).ConfigureAwait(false);
        return WithClock(await GitRepository.InitAsync(runner, path, cancellationToken, _log).ConfigureAwait(false));
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
