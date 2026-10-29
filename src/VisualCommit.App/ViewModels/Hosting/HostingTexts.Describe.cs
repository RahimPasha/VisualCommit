using System.Globalization;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels.Hosting;

public static partial class HostingTexts
{
    /// <summary>A rate limit with the time GitHub gave (<c>HH:mm</c>, local time).</summary>
    public const string RateLimitedAt = "GitHub's rate limit was reached. Try again after {0}.";

    /// <summary>A rate limit without a time.</summary>
    public const string RateLimited = "GitHub's rate limit was reached. Try again in a few minutes.";

    /// <summary>Single sign-on, the organisation's name first.</summary>
    public const string SsoRequired = "{0} requires single sign-on: authorise the token for it on GitHub, then try again.";

    /// <summary>Single sign-on's subject when the failure names no organisation.</summary>
    public const string SsoOrganisation = "The organisation";

    public const string ApiVersion = "This version of VisualCommit needs an update to work with GitHub.";
    public const string ServerError = "GitHub had a problem. Try again later.";
    public const string Malformed = "GitHub's answer could not be read.";

    /// <summary>GitHub's own words for a refusal, a missing repository or a form it rejected (ruling 15).</summary>
    public const string Answered = "GitHub answered: {0}.";

    /// <summary>
    /// The one wording of a GitHub failure that the clone browser, the "Pull requests" section and
    /// the create form show (D338, docs/phase-7a-design.md 1.7), shown as it is; <paramref name="login"/>
    /// is the signed-in account's, or null:
    /// <list type="bullet">
    /// <item><c>Network</c>: the message as it is (WP-G's fixed texts, D284).</item>
    /// <item><c>BadCredentials</c>: with a login <see cref="OperationTexts.TokenRefused"/> (D276);
    /// without, the message with a full stop, then <see cref="SignInInSettings"/>.</item>
    /// <item><c>RateLimited</c>: the time to try again after (<c>RetryAt</c> in local time,
    /// <c>HH:mm</c>, the invariant culture), or "in a few minutes".</item>
    /// <item><c>SsoRequired</c>: the organisation named by the path segment after <c>orgs/</c> in the
    /// detail's URL, else "The organisation".</item>
    /// <item><c>ApiVersion</c>, <c>ServerError</c>, <c>Malformed</c>: the app's fixed sentences.</item>
    /// <item><c>Forbidden</c>, <c>NotFound</c>, <c>Validation</c>: "GitHub answered: " and GitHub's
    /// words, a full stop at their end dropped first.</item>
    /// </list>
    /// </summary>
    public static string Describe(HostingFailure failure, string? login)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Kind switch
        {
            HostingFailureKind.Network => failure.Message,
            HostingFailureKind.BadCredentials => string.IsNullOrEmpty(login)
                ? $"{WithFullStop(failure.Message)} {SignInInSettings}"
                : OperationTexts.TokenRefused(login),
            HostingFailureKind.RateLimited => failure.RetryAt is { } retryAt
                ? string.Format(CultureInfo.InvariantCulture, RateLimitedAt, retryAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture))
                : RateLimited,
            HostingFailureKind.SsoRequired => string.Format(CultureInfo.InvariantCulture, SsoRequired, OrganisationOf(failure.Detail) ?? SsoOrganisation),
            HostingFailureKind.ApiVersion => ApiVersion,
            HostingFailureKind.ServerError => ServerError,
            HostingFailureKind.Malformed => Malformed,
            _ => string.Format(CultureInfo.InvariantCulture, Answered, WithoutFullStop(failure.Message)),
        };
    }

    /// <summary><paramref name="text"/> ending with one full stop.</summary>
    private static string WithFullStop(string text)
    {
        var trimmed = text.TrimEnd();
        return trimmed.EndsWith('.') ? trimmed : trimmed + ".";
    }

    /// <summary><paramref name="text"/> without the full stop at its end, if it has one.</summary>
    private static string WithoutFullStop(string text)
    {
        var trimmed = text.TrimEnd();
        return trimmed.EndsWith('.') ? trimmed[..^1] : trimmed;
    }

    /// <summary>The path segment after <c>orgs/</c> in single sign-on's URL (<c>https://github.com/orgs/acme/sso?…</c>), or null.</summary>
    private static string? OrganisationOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var path = Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ? uri.AbsolutePath : url.Trim();
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var orgs = Array.FindIndex(segments, segment => segment.Equals("orgs", StringComparison.OrdinalIgnoreCase));
        if (orgs < 0 || orgs + 1 >= segments.Length)
        {
            return null;
        }

        var organisation = Uri.UnescapeDataString(segments[orgs + 1]).Trim();
        return organisation.Length == 0 ? null : organisation;
    }
}
