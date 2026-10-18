// The checkout of a remote branch (docs/phase-3-design.md 2.2, D93), moved here unchanged from
// RepositoryOperations.Branches.cs (docs/phase-6-design.md 18), so that work package WP-RA gives it
// the fetch refspecs' local name and the checkout without an upstream (10.4, 10.6, D201). Its tests
// are in tests/VisualCommit.App.Tests/RepositoryOperationsCheckoutTests.cs.

using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <summary>
    /// 2.2 (D93): the local branch of the same name when it exists, checked out as it is; otherwise
    /// a new one tracking the remote branch.
    /// </summary>
    private async Task<string> CheckoutRemoteBranchAsync(string remoteRef, NetworkOptions options)
    {
        var shown = ShortName(remoteRef);
        var local = LocalNameOfRemoteBranch(remoteRef);
        if (FindRef(HeadsPrefix + local) is { } existing)
        {
            await Git.CheckoutBranchAsync(local, keepIgnored: KeepIgnored, options);
            if (!string.Equals(existing.UpstreamRef, remoteRef, StringComparison.Ordinal))
            {
                return OperationTexts.CheckedOutTracking(local, UpstreamText(existing));
            }

            return existing.Behind > 0 ? OperationTexts.CheckedOutBehind(local, existing.Behind, shown) : OperationTexts.CheckedOut(local);
        }

        await Git.CheckoutTrackingBranchAsync(local, remoteRef, keepIgnored: KeepIgnored, options);
        FollowCreatedBranch(HeadsPrefix + local);
        return OperationTexts.CreatedTrackingAndCheckedOut(local, shown);
    }

    /// <summary>
    /// The local branch a checkout of <paramref name="target"/> may create (2.2), which the undo
    /// history reads with its upstream keys (docs/phase-5-design.md 7.3): a remote branch's local
    /// name, or null for any other target.
    /// </summary>
    private string? CreatedBranchOf(string target) =>
        target.StartsWith(RemotesPrefix, StringComparison.Ordinal) ? LocalNameOfRemoteBranch(target) : null;

    /// <summary>The local name a remote branch checks out into: the part after <c>&lt;r&gt;/</c>, the remote found by its ref or, for a ref not read yet, by the longest remote name it starts with.</summary>
    private string LocalNameOfRemoteBranch(string remoteRef)
    {
        if (FindRef(remoteRef) is { } gitRef && RepositoryMenuContext.LocalNameOf(gitRef) is { } local)
        {
            return local;
        }

        var name = remoteRef[RemotesPrefix.Length..];
        var remote = RemoteNames
            .Where(candidate => name.StartsWith(candidate + "/", StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Length)
            .FirstOrDefault();
        if (remote is not null)
        {
            return name[(remote.Length + 1)..];
        }

        var slash = name.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 ? name[(slash + 1)..] : name;
    }
}
