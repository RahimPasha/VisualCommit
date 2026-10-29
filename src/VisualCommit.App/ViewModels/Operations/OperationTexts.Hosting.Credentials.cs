using VisualCommit.App.ViewModels.Hosting;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The two texts of a refusal (D276, D330, docs/phase-7a-design.md 4.7): git's refusal of the
/// credentials the app's helper gave it, which does not prove the token bad, and the API's 401,
/// which does.
/// </summary>
public static partial class OperationTexts
{
    /// <summary>
    /// The error dialog's summary for an operation whose credentials git refused (its
    /// <c>erase</c> to the app's helper, <see cref="Core.Operations.OperationResult.RefusedAccount"/>).
    /// A stale <c>http.extraHeader</c> or a redirect makes git erase a valid token too, so it names
    /// git's credentials and says when to sign in again.
    /// </summary>
    public static string GitCredentialsRefused(string login) =>
        $"{HostingTexts.GitHub} refused git's credentials for {login}. If the token has expired or was revoked, sign in to {HostingTexts.GitHub} again in Settings, {HostingTexts.AccountsSection}.";

    /// <summary>The API's 401 to a request made with the token, which is certain: <c>HostingTexts.Describe</c>'s <c>BadCredentials</c> with a login, and the create form's POST.</summary>
    public static string TokenRefused(string login) =>
        $"{HostingTexts.GitHub} refused the token of {login}. {HostingTexts.SignInInSettings}";
}
