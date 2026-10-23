// The checkout of a remote branch (docs/phase-3-design.md 2.2, D93), moved here unchanged from
// RepositoryOperations.Branches.cs (docs/phase-6-design.md 18), with the fetch refspecs' local
// name and the checkout without an upstream (10.4 #10, 10.6, D93 as amended, D201; WP-RA). Every
// git call of it keeps Keep ignored files' option (9.11). Its tests are in
// tests/VisualCommit.App.Tests/RepositoryOperationsCheckoutTests.cs and, for the fetch lines,
// RepositoryOperationsRemotesTests.cs.

using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <summary>
    /// 2.2 (D93 as amended, docs/phase-6-design.md 10.6): the local branch of the remote branch's
    /// local name (<see cref="RepoRefs.LocalNameOf"/>: its name on the remote) when it exists,
    /// checked out as it is; otherwise a new one tracking the remote branch, or, for one its remote
    /// fetches from two refs (R2, D201), a new one without an upstream, since git would track the
    /// wrong first candidate. A ref two remotes fetch, or none (R1, R3), has no local name: the
    /// checkouts' operations refuse it before git (<see cref="RefuseUnfetchedRemoteBranch"/>).
    /// </summary>
    private async Task<string> CheckoutRemoteBranchAsync(string remoteRef, NetworkOptions options)
    {
        var shown = ShortName(remoteRef);
        if (LocalNameOfRemoteBranch(remoteRef) is not { } local)
        {
            // A backstop only: the operation refused such a ref before (RefuseUnfetchedRemoteBranch);
            // git would fail, and for R1 leave the new branch behind (0.5's H).
            throw new InvalidOperationException(UnfetchedReason(remoteRef) ?? $"{shown} is not fetched from any remote");
        }

        if (FindRef(HeadsPrefix + local) is { } existing)
        {
            await Git.CheckoutBranchAsync(local, keepIgnored: KeepIgnored, options);
            if (!string.Equals(existing.UpstreamRef, remoteRef, StringComparison.Ordinal))
            {
                return OperationTexts.CheckedOutTracking(local, UpstreamText(existing));
            }

            return existing.Behind > 0 ? OperationTexts.CheckedOutBehind(local, existing.Behind, shown) : OperationTexts.CheckedOut(local);
        }

        if (SourceOfRemoteBranch(remoteRef) is { Kind: RemoteSourceKind.AmbiguousRefs } ambiguous)
        {
            // D201: without an upstream (0.5's P3), saying why.
            await Git.CreateBranchAsync(local, remoteRef, checkout: true, noTrack: true, keepIgnored: KeepIgnored, options);
            FollowCreatedBranch(HeadsPrefix + local);
            return OperationTexts.CreatedWithoutUpstream(local, shown, OperationTexts.MenuTexts.RemoteSourceReason(ambiguous, shown, needsBranch: false)!);
        }

        await Git.CheckoutTrackingBranchAsync(local, remoteRef, keepIgnored: KeepIgnored, options);
        FollowCreatedBranch(HeadsPrefix + local);
        return OperationTexts.CreatedTrackingAndCheckedOut(local, shown);
    }

    /// <summary>
    /// 10.4 #10's refusal before git, inside a checkout's operation, of a remote branch that two
    /// remotes fetch or none (R1, R3) and so has no local branch to check out into: a menu built
    /// before the fetch lines changed reaches it. The error dialog says "Checkout failed" with the
    /// reason; nothing is written, and "Stash changes and check out" stashes nothing. Null for any
    /// other target. CheckoutAsync and StashAndCheckoutAsync (RepositoryOperations.Branches.cs)
    /// call it first in their operations.
    /// </summary>
    private OperationReport? RefuseUnfetchedRemoteBranch(OperationText text, string target) =>
        target.StartsWith(RemotesPrefix, StringComparison.Ordinal) && LocalNameOfRemoteBranch(target) is null && UnfetchedReason(target) is { } reason
            ? Refused(text, reason)
            : null;

    /// <summary>
    /// The local branch a checkout of <paramref name="target"/> may create (2.2), which the undo
    /// history reads with its upstream keys (docs/phase-5-design.md 7.3): a remote branch's local
    /// name (10.6), or null for any other target and for a remote branch that has none.
    /// </summary>
    private string? CreatedBranchOf(string target) =>
        target.StartsWith(RemotesPrefix, StringComparison.Ordinal) ? LocalNameOfRemoteBranch(target) : null;

    /// <summary>
    /// The local name a remote branch checks out into (10.6): <see cref="RepoRefs.LocalNameOf(GitRef)"/>
    /// of the ref as read, null for one two remotes fetch or none; for a ref the refs do not hold
    /// (fetched since they were read), the same rule by its full name, as the fetch lines read with
    /// the refs map it (<see cref="RepoRefs.LocalNameOf(string)"/>); before any refs are read,
    /// phase 5's name.
    /// </summary>
    private string? LocalNameOfRemoteBranch(string remoteRef)
    {
        var refs = Refs ?? new RepoRefs(new HeadState(null, null), [], [], []);
        return FindRef(remoteRef) is { Kind: RefKind.RemoteBranch } gitRef ? refs.LocalNameOf(gitRef) : refs.LocalNameOf(remoteRef);
    }

    /// <summary>
    /// How the remote branch <paramref name="remoteRef"/> is fetched (10.1): as read, or for a ref
    /// the refs do not hold by its full name, as <see cref="LocalNameOfRemoteBranch"/> maps it; null
    /// before any refs are read.
    /// </summary>
    private RemoteSource? SourceOfRemoteBranch(string remoteRef) =>
        Refs is not { } refs ? null
        : FindRef(remoteRef) is { Kind: RefKind.RemoteBranch } gitRef ? refs.SourceOf(gitRef)
        : refs.SourceOf(remoteRef);

    /// <summary>R1 or R3 for a remote branch that two remotes fetch or none (10.5); null otherwise.</summary>
    private string? UnfetchedReason(string remoteRef) =>
        SourceOfRemoteBranch(remoteRef) is { Kind: RemoteSourceKind.AmbiguousRemotes or RemoteSourceKind.NotFetched } source
            ? OperationTexts.MenuTexts.RemoteSourceReason(source, FindRef(remoteRef)?.Name ?? ShortName(remoteRef), needsBranch: false)
            : null;
}
