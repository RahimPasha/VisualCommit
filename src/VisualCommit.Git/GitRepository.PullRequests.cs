using VisualCommit.Core.Git;

namespace VisualCommit.Git;

/// <summary>A pull request's head fetched for its checkout (docs/phase-7a-design.md 1.3, 10.5; D305).</summary>
public sealed partial class GitRepository
{
    private const string PullRefPrefix = "refs/pull/";
    private const string PullHeadSuffix = "/head";

    /// <summary>
    /// Fetches <paramref name="sourceRef"/> of <paramref name="remote"/> into <c>FETCH_HEAD</c>
    /// only, with no ref and no configuration written, and returns the commit's SHA from
    /// <c>FETCH_HEAD</c>'s line, or null when that line is not this fetch's.
    /// </summary>
    /// <remarks>
    /// One <c>git fetch --progress --no-tags --refmap= --write-fetch-head &lt;remote&gt; &lt;sourceRef&gt;</c>
    /// (10.5): <c>--refmap=</c> keeps the remote's fetch lines (a <c>refs/pull</c> line among them)
    /// from writing a ref, <c>--no-tags</c> its <c>tagOpt</c> from fetching tags, and no
    /// <c>--prune</c> is given, so a <c>fetch.prune</c> deletes nothing (D99). The file is read here,
    /// never by a <c>rev-parse FETCH_HEAD</c> of its own: another fetch in the same worktree can
    /// rewrite it in between (plan risk 33). <paramref name="sourceRef"/> must be
    /// <c>refs/pull/&lt;digits&gt;/head</c>; a missing one makes git fail (<see cref="GitException"/>).
    /// </remarks>
    public async Task<string?> FetchToFetchHeadAsync(string remote, string sourceRef, NetworkOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remote);
        ArgumentNullException.ThrowIfNull(sourceRef);
        ArgumentNullException.ThrowIfNull(options);
        if (!IsPullRequestHead(sourceRef))
        {
            throw new ArgumentException($"Only a pull request's head (refs/pull/<number>/head) is fetched into FETCH_HEAD, not {sourceRef}.", nameof(sourceRef));
        }

        await RunRemoteCommandAsync(["fetch", "--progress", "--no-tags", "--refmap=", "--write-fetch-head", remote, sourceRef], options, cancellationToken).ConfigureAwait(false);

        // The file is this worktree's (GitDirectory, not the common directory's).
        return FetchHeadShaOf(ReadGitFile("FETCH_HEAD"), sourceRef);
    }

    /// <summary>Whether <paramref name="sourceRef"/> is <c>refs/pull/&lt;digits&gt;/head</c>.</summary>
    internal static bool IsPullRequestHead(string sourceRef)
    {
        if (!sourceRef.StartsWith(PullRefPrefix, StringComparison.Ordinal) || !sourceRef.EndsWith(PullHeadSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var number = sourceRef.AsSpan(PullRefPrefix.Length, sourceRef.Length - PullRefPrefix.Length - PullHeadSuffix.Length);
        return number.Length > 0 && !number.ContainsAnyExceptInRange('0', '9');
    }

    /// <summary>
    /// The SHA of a <c>FETCH_HEAD</c> that is this fetch's alone: one line of three tab-separated
    /// fields, a full SHA, an empty second field (for merge) and a third that starts
    /// <c>'&lt;sourceRef&gt;' of </c>; else null. The URL after <c>of</c> is not compared: git writes
    /// it after <c>insteadOf</c> and without a trailing <c>.git</c> (10.5).
    /// </summary>
    internal static string? FetchHeadShaOf(string? fetchHead, string sourceRef)
    {
        if (fetchHead is null)
        {
            return null;
        }

        var lines = fetchHead.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var count = lines.Length > 0 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        if (count != 1)
        {
            return null;
        }

        return lines[0].Split('\t') is [var sha, { Length: 0 }, var description]
            && IsFullSha(sha)
            && description.StartsWith($"'{sourceRef}' of ", StringComparison.Ordinal)
                ? sha
                : null;
    }

    /// <summary>A whole object name in lower-case hexadecimal: 40 digits (SHA-1) or 64 (SHA-256).</summary>
    private static bool IsFullSha(string text) =>
        text.Length is 40 or 64 && !text.AsSpan().ContainsAnyExcept("0123456789abcdef");
}
