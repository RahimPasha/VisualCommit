// Remotes and the network: docs/phase-3-design.md 2.16, 2.18 to 2.23, 2.29 (pushing and
// deleting tags on a remote), the Remotes dialog's actions (IRemoteActions), 1.8's cancelled
// fetches and pushes, and their texts in 2.33 (OperationTexts). Clone is not here (the welcome
// page), nor auto-fetch (WP9). Stubs left by WP7-core: the sub-package WP7-remotes replaces each
// body with the operation, built on the helpers of RepositoryOperations.Run.cs, and keeps every
// signature (IRepositoryActions and IRemoteActions list them). Its tests go into
// tests/VisualCommit.App.Tests/RepositoryOperationsTests.Remotes.cs (a part of the partial class
// RepositoryOperationsTests, through OperationsHarness).

using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    private const string RemotesPackage = "remotes";

    public Task FetchAllAsync() => NotBuilt(RemotesPackage);

    public Task FetchAsync(string remote) => NotBuilt(RemotesPackage);

    public Task PullAsync() => NotBuilt(RemotesPackage);

    public Task PushCurrentBranchAsync() => NotBuilt(RemotesPackage);

    public Task PushBranchAsync(string branchRef) => NotBuilt(RemotesPackage);

    public Task PushBranchToAsync(string branchRef, string remoteBranchRef, bool setUpstream) => NotBuilt(RemotesPackage);

    public Task ForcePushAsync(string branchRef) => NotBuilt(RemotesPackage);

    public Task SetUpstreamAsync(string branchRef) => NotBuilt(RemotesPackage);

    public Task UnsetUpstreamAsync(string branchRef) => NotBuilt(RemotesPackage);

    public Task DeleteRemoteBranchAsync(string remoteBranchRef) => NotBuilt(RemotesPackage);

    public Task ManageRemotesAsync() => NotBuilt(RemotesPackage);

    public Task ShowAddRemoteAsync() => NotBuilt(RemotesPackage);

    public Task ShowEditRemoteAsync(string remote) => NotBuilt(RemotesPackage);

    public Task RemoveRemoteAsync(string remote) => NotBuilt(RemotesPackage);

    public Task PushTagAsync(string tagRef) => NotBuilt(RemotesPackage);

    public Task DeleteRemoteTagAsync(string tagRef) => NotBuilt(RemotesPackage);

    // The Remotes dialog's own actions (IRemoteActions, 2.23).

    /// <summary>The configured remotes, in git's order (<c>git remote -v</c>), for the Remotes dialog.</summary>
    public Task<IReadOnlyList<RemoteInfo>> ReadRemotesAsync() => NotBuilt<IReadOnlyList<RemoteInfo>>(RemotesPackage);

    /// <summary>Adds a remote and waits; with <paramref name="fetchNow"/> also queues its fetch. Null, or git's message for the form.</summary>
    public Task<string?> AddRemoteAsync(string name, string url, bool fetchNow) => NotBuilt<string?>(RemotesPackage);

    /// <summary>Renames a remote and waits. Null, or git's message for the form.</summary>
    public Task<string?> RenameRemoteAsync(string name, string newName) => NotBuilt<string?>(RemotesPackage);

    /// <summary>Changes a remote's URL and waits. Null, or git's message for the form.</summary>
    public Task<string?> SetRemoteUrlAsync(string name, string url) => NotBuilt<string?>(RemotesPackage);
}
