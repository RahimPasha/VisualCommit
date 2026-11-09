using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Hosting;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The two texts of a refusal (D276, D330, docs/phase-7a-design.md 4.7, docs/phase-7b-design.md
/// 1.7, 8.5): git's refusal of the credentials the app's helper gave it, which does not prove the
/// token bad, and the API's 401, which does. Each names the provider; GitHub's are 7a's word for word.
/// </summary>
public static partial class OperationTexts
{
    /// <summary>
    /// The error dialog's summary for an operation whose credentials git refused (its
    /// <c>erase</c> to the app's helper, <see cref="Core.Operations.OperationResult.RefusedAccount"/>).
    /// A stale <c>http.extraHeader</c> or a redirect makes git erase a valid token too, so it names
    /// git's credentials and says when to sign in again. <paramref name="login"/> is the account's
    /// own case, the organization on Azure DevOps.
    /// </summary>
    public static string GitCredentialsRefused(HostingProvider provider, string login) =>
        $"{HostingTexts.Name(provider)} refused git's credentials for {login}. If the token has expired or was revoked, sign in to {HostingTexts.Name(provider)} again in Settings, {HostingTexts.AccountsSection}.";

    /// <summary>The API's 401 to a request made with the token, which is certain: <c>HostingTexts.Describe</c>'s <c>BadCredentials</c> with a login, and the create form's POST.</summary>
    public static string TokenRefused(HostingProvider provider, string login) =>
        $"{HostingTexts.Name(provider)} refused the token of {login}. {HostingTexts.SignInInSettingsOf(provider)}";

    /// <summary>7a's form: <see cref="GitCredentialsRefused(HostingProvider, string)"/> for GitHub.</summary>
    public static string GitCredentialsRefused(string login) => GitCredentialsRefused(HostingProvider.GitHub, login);

    /// <summary>7a's form: <see cref="TokenRefused(HostingProvider, string)"/> for GitHub.</summary>
    public static string TokenRefused(string login) => TokenRefused(HostingProvider.GitHub, login);
}
