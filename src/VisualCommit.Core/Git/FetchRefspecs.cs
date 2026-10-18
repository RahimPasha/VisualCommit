namespace VisualCommit.Core.Git;

/// <summary>
/// One value of <c>remote.&lt;r&gt;.fetch</c>, read as git reads a fetch refspec
/// (docs/phase-6-design.md 10.1, 0.5).
/// </summary>
/// <param name="Remote">The remote whose line it is.</param>
/// <param name="Source">The ref on the remote, or its pattern with one <c>*</c>.</param>
/// <param name="Destination">The tracking ref it fetches into, or its pattern; null for a line that fetches into <c>FETCH_HEAD</c> only and maps nothing.</param>
/// <param name="IsNegative">A line starting with <c>^</c>: what it names is not fetched.</param>
/// <param name="Force">A line starting with <c>+</c>.</param>
public sealed record FetchRefspec(string Remote, string Source, string? Destination, bool IsNegative, bool Force)
{
    /// <summary>
    /// The line <paramref name="value"/> of <paramref name="remote"/>, or null for an empty value,
    /// one without a source, or one git refuses (<c>fatal: invalid refspec</c>). Work package WP3
    /// gives it its body (10.1).
    /// </summary>
    public static FetchRefspec? Parse(string remote, string value) =>
        throw new NotImplementedException("Work package WP3 builds FetchRefspec.Parse (docs/phase-6-design.md 10.1).");
}

/// <summary>How a remote-tracking ref is fetched (docs/phase-6-design.md 10.1).</summary>
public enum RemoteSourceKind
{
    /// <summary>One remote fetches it from one ref.</summary>
    Mapped,

    /// <summary>Two or more remotes fetch it (git's "not tracking: ambiguous information"; R1).</summary>
    AmbiguousRemotes,

    /// <summary>One remote fetches it from two or more different refs (stricter than git, which takes the first; R2).</summary>
    AmbiguousRefs,

    /// <summary>No remote's line fetches it (R3).</summary>
    NotFetched,
}

/// <summary>Where a remote-tracking ref is fetched from (docs/phase-6-design.md 10.1).</summary>
/// <param name="Kind">How it is fetched.</param>
/// <param name="Remote">The remote, for <see cref="RemoteSourceKind.Mapped"/> and <see cref="RemoteSourceKind.AmbiguousRefs"/>; null otherwise.</param>
/// <param name="RemoteRef">The ref on the remote (<c>refs/heads/main</c>), for <see cref="RemoteSourceKind.Mapped"/>; null otherwise.</param>
/// <param name="Remotes">The remotes that fetch it, for <see cref="RemoteSourceKind.AmbiguousRemotes"/>, in configuration order; empty otherwise.</param>
public sealed record RemoteSource(RemoteSourceKind Kind, string? Remote, string? RemoteRef, IReadOnlyList<string> Remotes)
{
    private const string HeadsPrefix = "refs/heads/";

    /// <summary>Mapped, and <see cref="RemoteRef"/> is a branch on the remote (<c>refs/heads/&lt;x&gt;</c>).</summary>
    public bool IsBranch =>
        Kind == RemoteSourceKind.Mapped
        && RemoteRef is { } remoteRef
        && remoteRef.Length > HeadsPrefix.Length
        && remoteRef.StartsWith(HeadsPrefix, StringComparison.Ordinal);

    /// <summary>The branch's name on the remote (<c>main</c>), or null when not <see cref="IsBranch"/>.</summary>
    public string? Branch => IsBranch ? RemoteRef![HeadsPrefix.Length..] : null;

    /// <summary>Whether <paramref name="trackingRef"/> is <c>refs/remotes/&lt;Remote&gt;/&lt;Branch&gt;</c>: the default line's mapping, under which nothing differs from phase 5.</summary>
    public bool IsIdentity(string trackingRef)
    {
        ArgumentNullException.ThrowIfNull(trackingRef);
        return Branch is { } branch && string.Equals(trackingRef, $"refs/remotes/{Remote}/{branch}", StringComparison.Ordinal);
    }
}

/// <summary>
/// Every remote's fetch lines, in configuration order (docs/phase-6-design.md 10.1), read with
/// each refs read (<see cref="RepoRefs.Refspecs"/>, 10.2) and mapped back as git's tracking setup
/// does. Pure, tested without git. Work package WP3 gives its reads their bodies.
/// </summary>
public sealed class FetchRefspecs
{
    private const string Builder = "Work package WP3 builds FetchRefspecs (docs/phase-6-design.md 10.1).";

    private FetchRefspecs(IReadOnlyList<FetchRefspec> lines) => Lines = lines;

    /// <summary>The lines as read, invalid ones left out, in configuration order.</summary>
    public IReadOnlyList<FetchRefspec> Lines { get; }

    /// <summary>The lines of <c>remote.&lt;r&gt;.fetch</c> values, each line parsed by <see cref="FetchRefspec.Parse"/>; an invalid one is skipped and logged.</summary>
    public static FetchRefspecs Parse(IEnumerable<(string Remote, string Value)> lines) => throw new NotImplementedException(Builder);

    /// <summary>The default line of every remote, <c>+refs/heads/*:refs/remotes/&lt;r&gt;/*</c>: for tests.</summary>
    public static FetchRefspecs Default(IEnumerable<string> remotes) => throw new NotImplementedException(Builder);

    /// <summary>Where the remote-tracking ref <paramref name="trackingRef"/> (<c>refs/remotes/…</c>) is fetched from: 10.1's rules 1 to 3.</summary>
    public RemoteSource SourceOf(string trackingRef) => throw new NotImplementedException(Builder);

    /// <summary>
    /// Git's forward mapping, which the fetch uses: the tracking refs <paramref name="remote"/>
    /// fetches <paramref name="remoteRef"/> (<c>refs/heads/…</c>) into, in configuration order,
    /// each once; none when a negative line of the remote matches it (10.1, 10.4's pushes).
    /// </summary>
    public IReadOnlyList<string> DestinationsOf(string remote, string remoteRef) => throw new NotImplementedException(Builder);
}
