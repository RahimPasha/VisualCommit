using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// Builds and starts the operations of one repository (docs/phase-3-design.md 1.1): asks the
/// confirmations, saves the backups, opens the dialogs, then <c>Queue.RunAsync</c>, and shows the
/// result. One per <see cref="RepositoryViewModel"/> (<see cref="RepositoryViewModel.Operations"/>).
/// Work package WP0 made this skeleton; WP7 adds the operations in partial files
/// <c>RepositoryOperations.*.cs</c>.
/// </summary>
public sealed partial class RepositoryOperations
{
    /// <param name="repository">The repository the operations act on.</param>
    /// <param name="queue">Its queue.</param>
    /// <param name="services">The app's services (dialogs, prompts, the launcher and clipboard), or null in view-model tests.</param>
    /// <param name="log">The app's log.</param>
    public RepositoryOperations(RepositoryViewModel repository, IOperationQueue queue, TabServices? services, IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(log);
        Repository = repository;
        Queue = queue;
        Services = services;
        Log = log;
    }

    public RepositoryViewModel Repository { get; }

    public IOperationQueue Queue { get; }

    /// <summary>The app's services, or null in view-model tests that give none.</summary>
    public TabServices? Services { get; }

    /// <summary>The window's dialog layer, for dialogs beyond a confirmation, or null without one.</summary>
    public DialogHostViewModel? Dialogs => Services?.DialogHost;

    public IAppLog Log { get; }
}
