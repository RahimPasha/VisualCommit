using VisualCommit.Core.Git;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The fetch refspecs' texts (docs/phase-6-design.md 10.4, 10.5, 14.1, 14.2, 14.5; D201, D222):
/// the reasons R1 to R4 of the items a remote branch fetched otherwise disables, the checkout
/// without an upstream, the push that would stop every fetch of its remote, and the drop's push
/// onto a renamed remote branch, which names the remote-tracking ref in its title (GD24). Like
/// every disabled reason, R1 to R4 have no full stop.
/// </summary>
public static partial class OperationTexts
{
    /// <summary>
    /// The drop's push of <paramref name="branch"/> onto a remote branch that its remote's fetch
    /// lines rename (10.4 #8, 14.1; the lead's ruling GD24): titled <c>Push {A} to {T}</c>, the
    /// drop menu's own words, while every other push keeps <see cref="Push"/>'s <c>Push {b}</c>;
    /// its running text, error title and summary are <see cref="Push"/>'s.
    /// </summary>
    public static OperationText PushToRemoteBranch(string branch, string trackingName) =>
        new($"Push {branch} to {trackingName}", $"Pushing {branch} to {trackingName}…", "Push failed", $"Git could not push {branch} to {trackingName}.");

    /// <summary>
    /// The result of checking out a remote branch one remote fetches from two refs (R2, D201,
    /// 10.4 #10): <c>Created {n} at {T} without an upstream: {R2}</c>, no full stop.
    /// </summary>
    public static string CreatedWithoutUpstream(string branch, string trackingName, string reason) =>
        $"Created {branch} at {trackingName} without an upstream: {reason}";

    /// <summary>
    /// The summary of a push refused before git (10.4's guard): pushing <paramref name="branch"/>
    /// to <paramref name="target"/>, as the push's running text names it, would give the tracking
    /// ref <paramref name="trackingName"/> a second source on <paramref name="remote"/>, and every
    /// later fetch of it would fail.
    /// </summary>
    public static string PushWouldStopFetch(string branch, string target, string trackingName, string remote) =>
        $"Pushing {branch} to {target} would stop every fetch of {remote}: {MenuTexts.FetchedFromSeveralRefs(trackingName, remote)}.";

    public static partial class MenuTexts
    {
        /// <summary>R1: two or more remotes fetch the tracking ref (git's "not tracking: ambiguous information").</summary>
        public static string FetchedFromRemotes(string trackingName, IReadOnlyList<string> remotes) =>
            $"{trackingName} is fetched from {List(remotes)}";

        /// <summary>R2: one remote fetches the tracking ref from two or more refs.</summary>
        public static string FetchedFromSeveralRefs(string trackingName, string remote) =>
            $"{trackingName} is fetched from more than one ref of {remote}";

        /// <summary>R3: no remote's fetch line fills the tracking ref.</summary>
        public static string NotFetchedFromAnyRemote(string trackingName) =>
            $"{trackingName} is not fetched from any remote";

        /// <summary>R4: the tracking ref is fetched from a ref that is no branch on its remote (the drop's pushes and the deletion only).</summary>
        public static string NotABranch(string trackingName, string remoteRef, string remote) =>
            $"{trackingName} is {remoteRef} on {remote}, not a branch";

        /// <summary>
        /// The reason of R1 to R4 that applies to the remote branch <paramref name="trackingName"/>
        /// fetched as <paramref name="source"/> says (10.5): null for a branch mapped from one ref
        /// of one remote, and for a mapped ref that is no branch while
        /// <paramref name="needsBranch"/> is false (its checkout, which git tracks as it is).
        /// </summary>
        public static string? RemoteSourceReason(RemoteSource source, string trackingName, bool needsBranch)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(trackingName);
            return source.Kind switch
            {
                RemoteSourceKind.AmbiguousRemotes => FetchedFromRemotes(trackingName, source.Remotes),
                RemoteSourceKind.AmbiguousRefs => FetchedFromSeveralRefs(trackingName, source.Remote ?? string.Empty),
                RemoteSourceKind.NotFetched => NotFetchedFromAnyRemote(trackingName),
                _ when source.IsBranch || !needsBranch => null,
                _ => NotABranch(trackingName, source.RemoteRef ?? string.Empty, source.Remote ?? string.Empty),
            };
        }
    }
}
