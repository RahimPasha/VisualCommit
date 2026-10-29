namespace VisualCommit.App.ViewModels;

// Signing in to GitHub through the window (docs/phase-7a-design.md 1.7, 8.5): what a tab's
// "Sign in to GitHub…" and the welcome page's and palette's clone from GitHub start
// (TabServices.SignInToGitHub). Not built yet: WP-A builds it.
public partial class MainWindowViewModel
{
    /// <summary>
    /// Signs in to GitHub and answers whether an account is signed in afterwards.
    /// </summary>
    /// <remarks>Answers false and shows nothing yet: WP-A builds it (docs/phase-7a-design.md 8.5).</remarks>
    private Task<bool> SignInToGitHubAsync() => Task.FromResult(false);
}
