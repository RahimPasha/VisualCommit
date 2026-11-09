namespace VisualCommit.Core.Hosting;

/// <summary>
/// The one place for a provider's name in Core and Hosting (docs/phase-7b-design.md 1.2, ruling 21):
/// its display name, possessive and id, the order of the settings screen's blocks (D353), and which
/// provider's form a token has (D378). The App's words are in <c>HostingTexts</c>.
/// </summary>
public static class HostingProviders
{
    // GitHub's token prefixes (the live command's LiveSettings, READ); case kept.
    private static readonly string[] GitHubPrefixes = ["github_pat_", "ghp_", "gho_", "ghu_", "ghs_", "ghr_"];

    // GitLab's personal access token prefix (r6 2.4, READ); case kept.
    private const string GitLabPrefix = "glpat-";

    // Azure DevOps' tokens (r5 5, READ): 84 characters with AZDO at the 0-based characters 76 to 79.
    private const int AzureDevOpsLength = 84;
    private const int AzureDevOpsMarkIndex = 76;
    private const string AzureDevOpsMark = "AZDO";

    /// <summary>GitHub, GitLab, Azure DevOps: the Accounts section's blocks (D353).</summary>
    public static IReadOnlyList<HostingProvider> InAccountsOrder { get; } = [HostingProvider.GitHub, HostingProvider.GitLab, HostingProvider.AzureDevOps];

    /// <summary><c>GitHub</c>, <c>GitLab</c>, <c>Azure DevOps</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a known provider.</exception>
    public static string DisplayName(HostingProvider provider) => provider switch
    {
        HostingProvider.GitHub => "GitHub",
        HostingProvider.GitLab => "GitLab",
        HostingProvider.AzureDevOps => "Azure DevOps",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown hosting provider."),
    };

    /// <summary><c>GitHub's</c>, <c>GitLab's</c>, <c>Azure DevOps'</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a known provider.</exception>
    public static string Possessive(HostingProvider provider) => provider switch
    {
        HostingProvider.GitHub => "GitHub's",
        HostingProvider.GitLab => "GitLab's",
        HostingProvider.AzureDevOps => "Azure DevOps'",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown hosting provider."),
    };

    /// <summary>The id in files and keys: <c>github</c>, <c>azure-devops</c>, <c>gitlab</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a known provider.</exception>
    public static string Id(HostingProvider provider) => provider switch
    {
        HostingProvider.GitHub => "github",
        HostingProvider.AzureDevOps => "azure-devops",
        HostingProvider.GitLab => "gitlab",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown hosting provider."),
    };

    /// <summary>The provider whose <see cref="Id"/> <paramref name="id"/> is, without case; null for any other text.</summary>
    public static HostingProvider? FromId(string? id)
    {
        foreach (var provider in InAccountsOrder)
        {
            if (string.Equals(Id(provider), id, StringComparison.OrdinalIgnoreCase))
            {
                return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// The provider whose token form <paramref name="token"/> has (D378), from its prefix and
    /// length alone: GitHub for <c>github_pat_</c>, <c>ghp_</c>, <c>gho_</c>, <c>ghu_</c>,
    /// <c>ghs_</c> or <c>ghr_</c>, GitLab for <c>glpat-</c> (ordinal, case kept), Azure DevOps for
    /// exactly 84 characters whose 0-based characters 76 to 79 are <c>AZDO</c>; null otherwise
    /// (GitHub's 40-hex classic tokens, Azure DevOps' 52-character legacy ones, anything unprefixed).
    /// The sign-in dialogs and each provider's local refusal refuse another provider's form; none
    /// requires its own.
    /// </summary>
    public static HostingProvider? TokenFormOf(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (GitHubPrefixes.Any(prefix => token.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return HostingProvider.GitHub;
        }

        if (token.StartsWith(GitLabPrefix, StringComparison.Ordinal))
        {
            return HostingProvider.GitLab;
        }

        if (token.Length == AzureDevOpsLength
            && string.CompareOrdinal(token, AzureDevOpsMarkIndex, AzureDevOpsMark, 0, AzureDevOpsMark.Length) == 0)
        {
            return HostingProvider.AzureDevOps;
        }

        return null;
    }
}
