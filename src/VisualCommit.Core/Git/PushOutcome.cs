namespace VisualCommit.Core.Git;

/// <summary>
/// What a push changed on the remote (docs/phase-5-design.md 7.7 step 3, D170): each remote ref it
/// updated, created or deleted, with its old and new value as git's own output says, never as the
/// remote-tracking ref says, which can be stale (0.5). An undo of the push sets them back with a
/// lease on these values. A push that changed nothing (<c>Everything up-to-date</c>) lists none.
/// </summary>
/// <param name="Refs">The remote refs the push changed.</param>
public sealed record PushOutcome(IReadOnlyList<PushedRef> Refs)
{
    /// <summary>Nothing changed on the remote.</summary>
    public static PushOutcome None { get; } = new([]);
}

/// <summary>One remote ref a push changed.</summary>
/// <param name="RemoteRef">Its full name on the remote: <c>refs/heads/main</c>, <c>refs/tags/v1</c>.</param>
/// <param name="OldValue">The object it pointed at before, full; null when the push created it.</param>
/// <param name="NewValue">The object it points at now, full; null when the push deleted it.</param>
public sealed record PushedRef(string RemoteRef, string? OldValue, string? NewValue);
