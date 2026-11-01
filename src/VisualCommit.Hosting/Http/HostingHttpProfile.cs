using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VisualCommit.Core.Hosting;

namespace VisualCommit.Hosting.Http;

/// <summary>
/// What one provider's API adds to the shared HTTP core (docs/phase-7b-design.md 1.4, 5.2; D358):
/// its headers and its token's form, the one origin the token goes to, how a failed answer is
/// classified, which answers are refused before parsing, and how its pages and request ids are
/// read. The core does the rest (one request at a time, the 30 s, redirects by hand, the 10 MB
/// bound, kept answers, the log), and sets the provider, address and request id on every failure.
/// </summary>
internal abstract class HostingHttpProfile
{
    /// <summary>The provider every failure names.</summary>
    public abstract HostingProvider Provider { get; }

    /// <summary>
    /// The one origin the token goes to (scheme, host and port), and the only one a redirect is
    /// followed on; its path is the one a next page must stay under.
    /// </summary>
    public abstract Uri TokenOrigin { get; }

    /// <summary>The shapes of request bodies; a body of a type outside it is an <see cref="ArgumentException"/>.</summary>
    public abstract JsonSerializerContext Json { get; }

    /// <summary>Puts the token on a request to <see cref="TokenOrigin"/>; the core never calls it for another origin.</summary>
    public abstract void Authorize(HttpRequestMessage request, string token);

    /// <summary>The provider's own headers, on every request and every hop, after the core's <c>User-Agent</c>.</summary>
    public virtual void AddHeaders(HttpRequestMessage request)
    {
    }

    /// <summary>
    /// The failure an answer stands for whose status is none of 2xx, 304 and a redirect, and which
    /// <see cref="SucceedsEmpty"/> does not take. The core fills <see cref="HostingFailure.Url"/>
    /// and <see cref="HostingFailure.RequestId"/> when this leaves them null.
    /// </summary>
    public abstract HostingFailure Classify(HostingExchange exchange);

    /// <summary>Asked of every 2xx and 304 before it is parsed: a failure ends the request with it; null lets it be parsed.</summary>
    public virtual HostingFailure? RefuseSuccess(HostingExchange exchange) => null;

    /// <summary>
    /// Asked of every redirect that names a <paramref name="location"/>, one on the same origin
    /// too, before anything is sent there: a failure ends the request with it; null leaves it to
    /// the core's rule (followed on <see cref="TokenOrigin"/>'s origin up to three hops).
    /// </summary>
    public virtual HostingFailure? RefuseRedirect(HostingExchange exchange, Uri location) => null;

    /// <summary>Whether an answer of <paramref name="status"/> is success without a value; asked before it is parsed or classified.</summary>
    public virtual bool SucceedsEmpty(int status) => false;

    /// <summary>The next page an answer names, followed by <see cref="HostingHttpClient.GetPagesAsync{T}"/>; null: the last page.</summary>
    public virtual Uri? NextPage(HostingExchange exchange) => null;

    /// <summary>The request id an answer's headers carry, for the failure and the log; null: none.</summary>
    public virtual string? RequestIdOf(IReadOnlyDictionary<string, string> headers) => null;

    /// <summary>The headers kept with an answer beside its body and served under a 304's own; none unless named.</summary>
    public virtual IReadOnlyCollection<string> KeptHeaderNames => [];

    /// <summary>
    /// The <c>&lt;status or kind&gt;</c> of the warning a failure this profile gave writes (by
    /// <see cref="Classify"/>, <see cref="RefuseSuccess"/> or <see cref="RefuseRedirect"/>): the
    /// answer's status unless the profile says otherwise. Never a header, body, query or token.
    /// </summary>
    public virtual string WarningOf(HostingExchange exchange, HostingFailure failure) => exchange.Status.ToString(CultureInfo.InvariantCulture);
}

/// <summary>An answer's <c>Link</c> header (RFC 8288), as the providers that page by it read it.</summary>
internal static partial class HostingLinks
{
    /// <summary>
    /// The URL of <c>rel="next"</c> in <paramref name="headers"/>' <c>Link</c>, as given; null when
    /// there is none or it is not an absolute address, one that begins with a scheme (RFC 3986):
    /// off Windows .NET reads <c>/p?page=2</c> as <c>file:///p%3Fpage=2</c> (CI's run 41311809889),
    /// and on Windows <c>//host/p</c> as a share. One whose host has no IDN form is given too:
    /// the core's origin check refuses it, so its list is <see cref="HostingFailureKind.Malformed"/> (D380).
    /// </summary>
    public static Uri? Next(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var link = headers.TryGetValue("Link", out var given) ? given
            : headers.FirstOrDefault(header => string.Equals(header.Key, "Link", StringComparison.OrdinalIgnoreCase)).Value;
        if (link is null)
        {
            return null;
        }

        foreach (Match match in LinkPattern().Matches(link))
        {
            if (match.Groups["rel"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("next", StringComparer.OrdinalIgnoreCase))
            {
                var url = match.Groups["url"].Value;
                return SchemePattern().IsMatch(url) && Uri.TryCreate(url, UriKind.Absolute, out var next) ? next : null;
            }
        }

        return null;
    }

    [GeneratedRegex("<(?<url>[^>]*)>\\s*;\\s*rel=\"(?<rel>[^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]*:", RegexOptions.CultureInvariant)]
    private static partial Regex SchemePattern();
}
