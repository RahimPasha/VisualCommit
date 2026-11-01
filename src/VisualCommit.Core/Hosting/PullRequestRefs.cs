using System.Globalization;

namespace VisualCommit.Core.Hosting;

/// <summary>
/// The refs of a pull or merge request on each provider (docs/phase-7b-design.md 1.2, 8.4; ruling
/// 14): the head ref a checkout fetches (D305), the forms the fetch into <c>FETCH_HEAD</c> takes,
/// and the push guard's rule for a request ref that cannot exist on the provider. GitHub's rule is
/// 7a's (docs/phase-7a-design.md 6.3, ruling 8), moved here from <c>GitHubUrls</c>.
/// </summary>
public static class PullRequestRefs
{
    private const string PullPrefix = "refs/pull/";
    private const string MergeRequestsPrefix = "refs/merge-requests/";
    private const string HeadSuffix = "/head";

    /// <summary>
    /// The head ref of request <paramref name="number"/> that D305's fetch takes:
    /// <c>refs/pull/&lt;n&gt;/head</c> on GitHub, <c>refs/merge-requests/&lt;n&gt;/head</c> on GitLab,
    /// null on Azure DevOps, whose request is checked out through a remote's branch alone (D361).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is below 1, or the provider is not known.</exception>
    public static string? HeadOf(HostingProvider provider, int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return provider switch
        {
            HostingProvider.GitHub => PullPrefix + number.ToString(CultureInfo.InvariantCulture) + HeadSuffix,
            HostingProvider.GitLab => MergeRequestsPrefix + number.ToString(CultureInfo.InvariantCulture) + HeadSuffix,
            HostingProvider.AzureDevOps => null,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown hosting provider."),
        };
    }

    /// <summary>
    /// Whether <paramref name="sourceRef"/> is a request's head that the fetch into
    /// <c>FETCH_HEAD</c> takes: <c>refs/pull/&lt;digits&gt;/head</c>, one or more ASCII digits and
    /// nothing after (7a's form; WP-C adds <c>refs/merge-requests/&lt;digits&gt;/head</c>, 8.4).
    /// </summary>
    public static bool IsFetchableHead(string sourceRef)
    {
        ArgumentNullException.ThrowIfNull(sourceRef);
        return IsDigitsBetween(sourceRef, PullPrefix, HeadSuffix);
    }

    /// <summary>
    /// The push guard (ruling 14): false only for a request ref that cannot exist on
    /// <paramref name="provider"/>; true otherwise. GitHub: false for <c>refs/pull/&lt;x&gt;/head</c>
    /// or <c>refs/pull/&lt;x&gt;/merge</c> whose <c>&lt;x&gt;</c> is not one or more ASCII digits
    /// (<c>refs/pull/fix/typo/head</c> included), which GitHub never has (7a's rule). GitLab and
    /// Azure DevOps: true until WP-C builds their rules (8.4).
    /// </summary>
    public static bool SourceCanExist(HostingProvider provider, string remoteRef)
    {
        ArgumentNullException.ThrowIfNull(remoteRef);
        return provider switch
        {
            HostingProvider.GitHub => GitHubSourceCanExist(remoteRef),
            _ => true,
        };
    }

    /// <summary>7a's rule (docs/phase-7a-design.md 6.3, ruling 8), as <c>GitHubUrls.SourceCanExist</c> had it.</summary>
    private static bool GitHubSourceCanExist(string remoteRef)
    {
        if (!remoteRef.StartsWith(PullPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        var rest = remoteRef[PullPrefix.Length..];
        var slash = rest.LastIndexOf('/');
        if (slash < 0 || rest[(slash + 1)..] is not ("head" or "merge"))
        {
            return true;
        }

        var number = rest[..slash];
        return number.Length > 0 && number.All(char.IsAsciiDigit);
    }

    /// <summary>Whether <paramref name="text"/> is <paramref name="prefix"/>, one or more ASCII digits, then <paramref name="suffix"/>.</summary>
    private static bool IsDigitsBetween(string text, string prefix, string suffix)
    {
        if (!text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(suffix, StringComparison.Ordinal)
            || text.Length < prefix.Length + suffix.Length)
        {
            return false;
        }

        var number = text.AsSpan(prefix.Length, text.Length - prefix.Length - suffix.Length);
        return number.Length > 0 && !number.ContainsAnyExceptInRange('0', '9');
    }
}
