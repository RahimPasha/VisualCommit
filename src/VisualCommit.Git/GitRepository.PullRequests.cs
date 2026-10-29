using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>A pull request's head fetched for its checkout (docs/phase-7a-design.md 1.3, 10.5).</summary>
public sealed partial class GitRepository
{
    /// <summary>
    /// Fetches <paramref name="sourceRef"/> of <paramref name="remote"/> into <c>FETCH_HEAD</c>
    /// only, with no ref and no configuration written, and returns the commit's SHA from
    /// <c>FETCH_HEAD</c>'s line, or null when that line is not this fetch's.
    /// </summary>
    /// <remarks>Not built yet: WP-P builds it (docs/phase-7a-design.md 10.5).</remarks>
    public Task<string?> FetchToFetchHeadAsync(string remote, string sourceRef, NetworkOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Fetching a pull request's head is not built yet (docs/phase-7a-design.md 10.5).");
}
