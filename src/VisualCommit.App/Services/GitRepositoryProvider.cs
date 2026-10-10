using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Git;

namespace VisualCommit.App.Services;

/// <summary>Opens, initialises and clones repositories with real git, once git has been found (D45).</summary>
public sealed class GitRepositoryProvider : IRepositoryProvider
{
    private readonly GitAccess _git;
    private readonly IAppLog _log;

    public GitRepositoryProvider(GitAccess git, IAppLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(git);
        _git = git;
        _log = log ?? NullAppLog.Instance;
    }

    public async Task<IGitRepository> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        var runner = await _git.GetRunnerAsync(cancellationToken);
        return await GitRepository.OpenAsync(runner, path, cancellationToken, _log);
    }

    public async Task<IGitRepository> InitAsync(string path, CancellationToken cancellationToken = default)
    {
        var runner = await _git.GetRunnerAsync(cancellationToken);
        return await GitRepository.InitAsync(runner, path, cancellationToken, _log);
    }

    public async Task<IGitRepository> CloneAsync(string url, string destination, IProgress<CloneProgress>? progress, CancellationToken cancellationToken = default)
    {
        var runner = await _git.GetRunnerAsync(cancellationToken);
        return await GitRepository.CloneAsync(runner, url, destination, progress, cancellationToken, _log);
    }
}
