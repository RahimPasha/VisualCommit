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
    /// <summary>Whether the line is a pattern: its source, and its destination when it has one, hold one <c>*</c>.</summary>
    internal bool IsPattern => Source.Contains('*', StringComparison.Ordinal);

    /// <summary>
    /// The line <paramref name="value"/> of <paramref name="remote"/>, or null for an empty value,
    /// one without a source, or one git refuses (<c>fatal: invalid refspec</c>), by git's own
    /// parse of a fetch refspec (0.5's line forms): a leading <c>+</c> is force and may not be
    /// followed by <c>^</c>; otherwise a leading <c>^</c> makes the line negative, which has no
    /// <c>:</c>, names a full ref or a pattern and not a commit id; the rest is split at its last
    /// <c>:</c>; each side holds at most one <c>*</c>, and both sides or neither (a pattern needs a
    /// destination); each side is a ref name by <c>git check-ref-format --allow-onelevel</c>'s
    /// rules; <c>@</c> alone is <c>HEAD</c>. No destination, or an empty one, maps nothing. An
    /// empty source (<c>:refs/remotes/origin/x</c>), which git takes for HEAD and its tracking
    /// setup for a branch with an empty name, names no branch and is left out (10.1).
    /// </summary>
    public static FetchRefspec? Parse(string remote, string value)
    {
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            return null;
        }

        var text = value;
        var force = false;
        var negative = false;
        if (text[0] == '+')
        {
            // "+^…" keeps its "^" in the source, which no ref name may hold: git refuses it.
            force = true;
            text = text[1..];
        }
        else if (text[0] == '^')
        {
            negative = true;
            text = text[1..];
        }

        var colon = text.LastIndexOf(':');
        if (negative && colon >= 0)
        {
            return null; // A negative line has one side only.
        }

        var source = colon >= 0 ? text[..colon] : text;
        var destination = colon >= 0 ? text[(colon + 1)..] : null;

        // Both sides a pattern or neither; a positive pattern needs a destination (git refuses
        // "refs/heads/*" and "refs/heads/*:" as fetch lines).
        var destinationIsPattern = destination is not null && destination.Contains('*', StringComparison.Ordinal);
        var sidesAgree = source.Contains('*', StringComparison.Ordinal)
            ? (destination is null ? negative : destinationIsPattern)
            : !destinationIsPattern;
        if (!sidesAgree)
        {
            return null;
        }

        if (source == "@")
        {
            source = "HEAD";
        }

        if (negative)
        {
            return source.Length > 0 && !IsCommitId(source) && IsValidSide(source)
                ? new FetchRefspec(remote, source, null, IsNegative: true, Force: false)
                : null;
        }

        if (source.Length == 0 || !IsValidSide(source))
        {
            return null;
        }

        if (destination is { Length: > 0 } && !IsValidSide(destination))
        {
            return null;
        }

        return new FetchRefspec(remote, source, destination is { Length: > 0 } ? destination : null, IsNegative: false, Force: force);
    }

    /// <summary>
    /// Whether <paramref name="name"/> matches this line's source: the same name, or for a pattern
    /// its text before <c>*</c> starts it and its text after <c>*</c> ends it (git's
    /// <c>match_name_with_pattern</c>: the text between may be empty and may hold slashes).
    /// </summary>
    internal bool SourceMatches(string name) => IsPattern
        ? FetchRefspecs.MatchPattern(Source, name, Source) is not null
        : string.Equals(Source, name, StringComparison.Ordinal);

    /// <summary>
    /// One side of a line by <c>check_refname_format</c> with <c>REFNAME_ALLOW_ONELEVEL</c> and,
    /// for a pattern, <c>REFNAME_REFSPEC_PATTERN</c>: one <c>*</c> at most, anywhere in a component.
    /// </summary>
    private static bool IsValidSide(string side)
    {
        if (side == "@")
        {
            return false;
        }

        var star = side.IndexOf('*', StringComparison.Ordinal);
        if (star >= 0)
        {
            if (side.IndexOf('*', star + 1) >= 0)
            {
                return false;
            }

            // A letter in the star's place keeps every other rule as it was: no "..", "@{" or
            // ".lock" appears or goes away.
            side = string.Concat(side.AsSpan(0, star), "x", side.AsSpan(star + 1));
        }

        // A folder in front allows one level ("main") and leaves every other rule as it was.
        return RefNameRules.RefNameProblem("x/" + side) is null;
    }

    /// <summary>A full commit id, which a negative line may not name (git: "negative refspecs cannot be exact sha1").</summary>
    private static bool IsCommitId(string text) => text.Length == 40 && text.All(Uri.IsHexDigit);
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
/// does. Pure, tested without git.
/// </summary>
public sealed class FetchRefspecs
{
    /// <summary>Each remote with lines and its lines, in the order of the remote's first line.</summary>
    private readonly IReadOnlyList<(string Remote, FetchRefspec[] Lines)> _byRemote;

    private FetchRefspecs(IReadOnlyList<FetchRefspec> lines)
    {
        Lines = lines;
        _byRemote = [.. lines
            .GroupBy(line => line.Remote, StringComparer.Ordinal)
            .Select(group => (group.Key, group.ToArray()))];
    }

    /// <summary>The lines as read, invalid ones left out, in configuration order.</summary>
    public IReadOnlyList<FetchRefspec> Lines { get; }

    /// <summary>
    /// The lines of <c>remote.&lt;r&gt;.fetch</c> values, each line parsed by
    /// <see cref="FetchRefspec.Parse"/>; one it gives null for is skipped (the reader logs it).
    /// </summary>
    public static FetchRefspecs Parse(IEnumerable<(string Remote, string Value)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var parsed = new List<FetchRefspec>();
        foreach (var (remote, value) in lines)
        {
            if (FetchRefspec.Parse(remote, value) is { } line)
            {
                parsed.Add(line);
            }
        }

        return new FetchRefspecs(parsed);
    }

    /// <summary>The default line of every remote, <c>+refs/heads/*:refs/remotes/&lt;r&gt;/*</c>: for tests.</summary>
    public static FetchRefspecs Default(IEnumerable<string> remotes)
    {
        ArgumentNullException.ThrowIfNull(remotes);
        return Parse(remotes.Select(remote => (remote, $"+refs/heads/*:refs/remotes/{remote}/*")));
    }

    /// <summary>
    /// Where the remote-tracking ref <paramref name="trackingRef"/> (<c>refs/remotes/…</c>) is
    /// fetched from: 10.1's rules 1 to 3. Per remote, in configuration order: its candidates are
    /// the sources of its positive lines whose destination is <paramref name="trackingRef"/> or,
    /// for a pattern, matches it, each source once; a candidate that a negative line of the remote
    /// matches leaves the remote mapping nothing (git's rule, 0.5's N, applied here to the source
    /// of an exact line too, which git's own tracking setup does not check, though its fetch
    /// leaves such a ref alone); one candidate maps it, two or more make it
    /// <see cref="RemoteSourceKind.AmbiguousRefs"/> (git takes the first). Then no remote gives
    /// <see cref="RemoteSourceKind.NotFetched"/>, two or more
    /// <see cref="RemoteSourceKind.AmbiguousRemotes"/>, one its own result.
    /// </summary>
    public RemoteSource SourceOf(string trackingRef)
    {
        ArgumentNullException.ThrowIfNull(trackingRef);
        var mapping = new List<RemoteSource>();
        foreach (var (remote, lines) in _byRemote)
        {
            if (SourceOn(remote, lines, trackingRef) is { } source)
            {
                mapping.Add(source);
            }
        }

        return mapping.Count switch
        {
            0 => NotFetched,
            1 => mapping[0],
            _ => new RemoteSource(RemoteSourceKind.AmbiguousRemotes, null, null, [.. mapping.Select(source => source.Remote!)]),
        };
    }

    /// <summary>
    /// Where <paramref name="remote"/>'s own lines fetch <paramref name="trackingRef"/> from, by
    /// 10.1's rules 1 to 3 for that remote alone: <see cref="RemoteSourceKind.Mapped"/>,
    /// <see cref="RemoteSourceKind.AmbiguousRefs"/> or <see cref="RemoteSourceKind.NotFetched"/>,
    /// whatever another remote maps it to. 10.4's push guard asks it of the push's remote, so that
    /// another remote whose lines also fill the ref (a mirror line, <c>+refs/*:refs/*</c>) does not
    /// hide that this one fetches it from two refs, which the push would make every fetch of it fail on.
    /// </summary>
    public RemoteSource SourceOf(string trackingRef, string remote)
    {
        ArgumentNullException.ThrowIfNull(trackingRef);
        ArgumentNullException.ThrowIfNull(remote);
        foreach (var (name, lines) in _byRemote)
        {
            if (string.Equals(name, remote, StringComparison.Ordinal))
            {
                return SourceOn(name, lines, trackingRef) ?? NotFetched;
            }
        }

        return NotFetched;
    }

    private static RemoteSource NotFetched { get; } = new(RemoteSourceKind.NotFetched, null, null, []);

    /// <summary>Rules 1 to 3 for one remote's <paramref name="lines"/>: its one candidate, AmbiguousRefs for two or more, null for none or one a negative line excludes.</summary>
    private static RemoteSource? SourceOn(string remote, FetchRefspec[] lines, string trackingRef)
    {
        var candidates = new List<string>();
        foreach (var line in lines)
        {
            if (line.IsNegative || line.Destination is not { } destination)
            {
                continue;
            }

            var candidate = line.IsPattern
                ? MatchPattern(destination, trackingRef, line.Source)
                : string.Equals(destination, trackingRef, StringComparison.Ordinal) ? line.Source : null;
            if (candidate is not null && !candidates.Contains(candidate, StringComparer.Ordinal))
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0 || candidates.Exists(candidate => IsExcluded(lines, candidate)))
        {
            return null;
        }

        return candidates.Count == 1
            ? new RemoteSource(RemoteSourceKind.Mapped, remote, candidates[0], [])
            : new RemoteSource(RemoteSourceKind.AmbiguousRefs, remote, null, []);
    }

    /// <summary>
    /// Git's forward mapping, which the fetch uses: the tracking refs <paramref name="remote"/>
    /// fetches <paramref name="remoteRef"/> (<c>refs/heads/…</c>) into, in configuration order,
    /// each once; none when a negative line of the remote matches it (10.1, 10.4's pushes). A
    /// push updates only the first of them (git's <c>remote_find_tracking</c>: C2's push of
    /// <c>main</c> leaves <c>origin/trunk</c> alone), and the next fetch fills the rest; a mirror
    /// line (0.5's F) gives the local branch of the same name too, which is no tracking ref.
    /// </summary>
    public IReadOnlyList<string> DestinationsOf(string remote, string remoteRef)
    {
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(remoteRef);
        FetchRefspec[] lines = [];
        foreach (var entry in _byRemote)
        {
            if (string.Equals(entry.Remote, remote, StringComparison.Ordinal))
            {
                lines = entry.Lines;
            }
        }

        if (IsExcluded(lines, remoteRef))
        {
            return [];
        }

        var destinations = new List<string>();
        foreach (var line in lines)
        {
            if (line.IsNegative || line.Destination is not { } destination)
            {
                continue;
            }

            var mapped = line.IsPattern
                ? MatchPattern(line.Source, remoteRef, destination)
                : string.Equals(line.Source, remoteRef, StringComparison.Ordinal) ? destination : null;
            if (mapped is not null && !destinations.Contains(mapped, StringComparer.Ordinal))
            {
                destinations.Add(mapped);
            }
        }

        return destinations;
    }

    /// <summary>
    /// Git's <c>match_name_with_pattern</c>: when <paramref name="name"/> starts with
    /// <paramref name="key"/>'s text before its <c>*</c> and ends with its text after it (and is
    /// at least as long as both together), <paramref name="value"/> with its <c>*</c> replaced by
    /// the text between, which may be empty and may hold slashes; otherwise null.
    /// </summary>
    internal static string? MatchPattern(string key, string name, string value)
    {
        var star = key.IndexOf('*', StringComparison.Ordinal);
        var (prefix, suffix) = (key[..star], key[(star + 1)..]);
        if (name.Length < prefix.Length + suffix.Length
            || !name.StartsWith(prefix, StringComparison.Ordinal)
            || !name.EndsWith(suffix, StringComparison.Ordinal))
        {
            return null;
        }

        var matched = name[prefix.Length..^suffix.Length];
        var valueStar = value.IndexOf('*', StringComparison.Ordinal);
        return valueStar < 0 ? value : string.Concat(value.AsSpan(0, valueStar), matched, value.AsSpan(valueStar + 1));
    }

    /// <summary>Whether a negative line among one remote's <paramref name="lines"/> matches <paramref name="remoteRef"/>: its full name, or its pattern.</summary>
    private static bool IsExcluded(FetchRefspec[] lines, string remoteRef) =>
        Array.Exists(lines, line => line.IsNegative && line.SourceMatches(remoteRef));
}
