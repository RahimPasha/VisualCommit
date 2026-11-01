using System.Globalization;
using VisualCommit.Core.Hosting;

namespace VisualCommit.App.ViewModels.Hosting;

/// <summary>
/// The App's one place for a provider's words (docs/phase-7b-design.md 1.7; ruling 21, D351, D355).
/// For GitHub each returns 7a's text word for word, its constant where 7a has one.
/// </summary>
public static partial class HostingTexts
{
    /// <summary><c>GitHub</c>, <c>GitLab</c>, <c>Azure DevOps</c> (<see cref="HostingProviders.DisplayName"/>).</summary>
    public static string Name(HostingProvider p) => p == HostingProvider.GitHub ? GitHub : HostingProviders.DisplayName(p);

    /// <summary><c>GitHub's</c>, <c>GitLab's</c>, <c>Azure DevOps'</c> (<see cref="HostingProviders.Possessive"/>).</summary>
    public static string Possessive(HostingProvider p) => HostingProviders.Possessive(p);

    /// <summary><c>pull request</c>; GitLab's <c>merge request</c>.</summary>
    public static string Request(HostingProvider p) => IsGitLab(p) ? "merge request" : "pull request";

    /// <summary><c>pull requests</c>; GitLab's <c>merge requests</c>.</summary>
    public static string Requests(HostingProvider p) => IsGitLab(p) ? "merge requests" : "pull requests";

    /// <summary><c>Pull request</c>; GitLab's <c>Merge request</c>.</summary>
    public static string RequestTitle(HostingProvider p) => IsGitLab(p) ? "Merge request" : "Pull request";

    /// <summary>The section's title: <c>Pull requests</c>; GitLab's <c>Merge requests</c>.</summary>
    public static string RequestsTitle(HostingProvider p) => IsGitLab(p) ? "Merge requests" : "Pull requests";

    /// <summary>A request's number: GitHub's <c>#12</c>; GitLab's and Azure DevOps' <c>!12</c> (D355).</summary>
    public static string Number(HostingProvider p, int n) =>
        (p == HostingProvider.GitHub ? "#" : "!") + n.ToString(CultureInfo.InvariantCulture);

    /// <summary>The sign-in dialog's title: <c>Add GitHub account</c>, <c>Add GitLab account</c>, <c>Add Azure DevOps account</c>.</summary>
    public static string AddAccountTitleOf(HostingProvider p) => p == HostingProvider.GitHub ? AddAccountTitle : $"Add {Name(p)} account";

    /// <summary>The menus' sign-in item: <c>Sign in to GitHub…</c>.</summary>
    public static string SignInToItemOf(HostingProvider p) => p == HostingProvider.GitHub ? SignInToGitHubItem : $"Sign in to {Name(p)}…";

    /// <summary><c>Sign in to GitHub in Settings, Accounts.</c></summary>
    public static string SignInInSettingsOf(HostingProvider p) =>
        p == HostingProvider.GitHub ? SignInInSettings : $"Sign in to {Name(p)} in Settings, {AccountsSection}.";

    private static bool IsGitLab(HostingProvider p) => p == HostingProvider.GitLab;
}
