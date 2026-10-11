using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// Builds and starts the operations of one repository (docs/phase-3-design.md 1.1): asks the
/// confirmations, saves the backups, opens the dialogs, then <c>Queue.RunAsync</c>, and shows the
/// result. One per <see cref="RepositoryViewModel"/> (<see cref="RepositoryViewModel.Operations"/>).
/// <para>
/// Its files: this one (the constructor and the interfaces the toolbar and the Remotes dialog
/// call); <c>RepositoryOperations.Run.cs</c>, the helpers every operation shares (running through
/// the queue with 2.33's texts, the error dialog and its extra buttons, confirmations with a
/// backup, name validation, lookups, a closed tab); and one file per group of operations, each
/// built by its own sub-package of WP7: <c>.Branches.cs</c> (branches and history),
/// <c>.Remotes.cs</c> (remotes and the network), <c>.StashTags.cs</c> (stashes and tags) and
/// <c>.Files.cs</c> (file actions). <see cref="IRepositoryActions"/> lists every public operation.
/// </para>
/// </summary>
public sealed partial class RepositoryOperations : IRepositoryActions, IToolbarActions, IRemoteActions
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

    // The toolbar's buttons (4d, D90).

    Task IToolbarActions.FetchAllAsync() => FetchAllAsync();

    Task IToolbarActions.PullAsync() => PullAsync();

    Task IToolbarActions.PushAsync() => PushCurrentBranchAsync();

    Task IToolbarActions.CreateBranchAtHeadAsync() => CreateBranchAsync(null);

    Task IToolbarActions.StashAsync() => StashAsync();

    Task IToolbarActions.PopAsync() => PopNewestStashAsync();

    // The Remotes dialog (2.23): the members of the same names in RepositoryOperations.Remotes.cs.

    Task<IReadOnlyList<Core.Git.RemoteInfo>> IRemoteActions.ReadRemotesAsync() => ReadRemotesAsync();

    Task<string?> IRemoteActions.AddRemoteAsync(string name, string url, bool fetchNow) => AddRemoteAsync(name, url, fetchNow);

    Task<string?> IRemoteActions.RenameRemoteAsync(string name, string newName) => RenameRemoteAsync(name, newName);

    Task<string?> IRemoteActions.SetRemoteUrlAsync(string name, string url) => SetRemoteUrlAsync(name, url);

    Task IRemoteActions.RemoveRemoteAsync(string name) => RemoveRemoteAsync(name);
}
