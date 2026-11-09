using System.Globalization;
using VisualCommit.Core.Hosting;

namespace VisualCommit.Hosting.Http;

/// <summary>
/// The core's fixed texts, each naming its provider through <see cref="HostingProviders"/>
/// (docs/phase-7b-design.md 5.2; D284): what a failure's <see cref="HostingFailure.Message"/> is
/// when no answer came, the time ran out, an answer could not be read or a server error gave no words.
/// </summary>
internal static class HostingHttpTexts
{
    /// <summary><c>Could not reach {name}.</c>: a request that got no answer.</summary>
    public static string Network(HostingProvider provider) => $"Could not reach {HostingProviders.DisplayName(provider)}.";

    /// <summary><c>{name} did not answer within 30 seconds.</c>: a request whose time ran out.</summary>
    public static string Timeout(HostingProvider provider) => $"{HostingProviders.DisplayName(provider)} did not answer within 30 seconds.";

    /// <summary><c>{possessive} answer could not be read.</c></summary>
    public static string Malformed(HostingProvider provider) => $"{HostingProviders.Possessive(provider)} answer could not be read.";

    /// <summary><c>{name} had a problem: HTTP {status}.</c>: a server error without words of its own.</summary>
    public static string ServerError(HostingProvider provider, int status) =>
        string.Create(CultureInfo.InvariantCulture, $"{HostingProviders.DisplayName(provider)} had a problem: HTTP {status}.");
}
