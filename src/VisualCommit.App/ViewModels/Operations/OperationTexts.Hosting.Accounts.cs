using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels.Operations;

public static partial class OperationTexts
{
    /// <summary>
    /// The texts of the settings screen's Accounts section and of the "Add GitHub account" dialog
    /// (docs/phase-7a-design.md 8.2; D249, D253, D256, D277, D291, D294 to D298). A
    /// <c>{reason}</c> or <c>{message}</c> loses a full stop at its end before the sentence's own.
    /// One wording names every platform's store: <see cref="HostingTexts.Keychain"/> (ruling 17).
    /// </summary>
    public static class AccountTexts
    {
        // The section (8.3).
        public const string AccountsIntro = "With a GitHub account, VisualCommit lists and creates pull requests, shows authors' pictures and clones your repositories.";
        public const string AccountsOff = "GitHub is turned off for this session.";
        public const string AccountsSignedOut = "Not signed in.";
        public const string AccountsSignInButton = "Sign in…";

        /// <summary>It opens the confirmation, as "Discard all changes…" and "Drop stash…" do.</summary>
        public const string AccountsSignOutButton = "Sign out…";

        public const string AccountsBusy = "Signing out…";

        /// <summary>Where the token is: the keychain.</summary>
        public const string TokenInKeychain = "The token is kept in " + HostingTexts.Keychain + ".";

        /// <summary>No keychain can be reached (D253): the section's note and the dialog's warning, word for word.</summary>
        public const string NoKeychain =
            "No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start.";

        public const string NeedsSignInRefused = "GitHub refused the token. Sign in again with a new token.";
        public const string NeedsSignInTokenMissing = "The token is no longer in " + HostingTexts.Keychain + ". Sign in again.";
        public const string NeedsSignInSessionEnded = "The token was kept for the last session only. Sign in again.";

        /// <summary>The second row's label.</summary>
        public const string GitLabel = "Git";

        public const string AccountUseForGit = "Use for git over HTTPS";

        /// <summary>The switch's note with git 2.31 or newer, or the version unknown.</summary>
        public const string AccountUseForGitNote =
            "Clone, fetch, pull and push to GitHub over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.";

        // The sign-out's confirmation (D291, D296).
        public const string SignOutTitle = "Sign out of GitHub?";
        public const string SignOutConfirm = "Sign out";

        // The dialog (8.4).
        public const string SignInHelp =
            "Paste a personal access token from GitHub. A fine-grained token needs read and write access to Contents and Pull requests of your repositories; " +
            "a classic token needs the repo scope. To push changes to workflow files, it also needs Workflows (fine-grained) or workflow (classic).";

        public const string SignInTokenLink = "Create a token on GitHub";
        public const string SignInTokenLinkTip = "Opens GitHub's page for a new token in your browser.";

        /// <summary>GitHub's page for a new fine-grained token, relative to the web base (D295).</summary>
        public const string TokenPage = "settings/personal-access-tokens/new?name=VisualCommit&contents=write&pull_requests=write";

        public const string TokenLabel = "Token";
        public const string SignInChecking = "Checking the token…";

        /// <summary>A token with a character outside printable ASCII, refused before any request (D298).</summary>
        public const string SignInNotAToken = "The token can contain only letters, digits and punctuation, without spaces or line breaks.";

        public const string Cancel = "Cancel";
        public const string SignIn = "Sign in";

        /// <summary>The one button after a save that failed (ruling 19).</summary>
        public const string Close = "Close";

        /// <summary>The section's note when a save failed after the token checked out (ruling 19).</summary>
        public static string SaveFailed(string reason) =>
            $"The token could not be saved in {HostingTexts.Keychain}: {WithoutFullStop(reason)}. VisualCommit keeps it until it closes and asks for it again at the next start.";

        public static string NeedsSignInReadFailed(string reason) =>
            $"The token could not be read from {HostingTexts.Keychain}: {WithoutFullStop(reason)}. Sign in again.";

        public static string AccountsSignOutNote(string reason) =>
            $"Signed out, but the token could not be removed from {HostingTexts.Keychain}: {WithoutFullStop(reason)}.";

        /// <summary>The switch's note with a detected git older than 2.31 (D277), in place of <see cref="AccountUseForGitNote"/>.</summary>
        public static string AccountUseForGitOldGit(string version) =>
            $"This needs Git 2.31 or newer. With Git {version}, clone, fetch, pull and push still use your git credential helpers.";

        /// <summary>
        /// The confirmation's question (D291); the last sentence left out when git is older than
        /// 2.31, which asked the user's helpers all along (D277).
        /// </summary>
        public static string SignOutQuestion(string login, bool gitUsesTheAccount) =>
            $"Sign out of GitHub as {login}? VisualCommit removes its token and the authors' pictures it saved from this computer, and no longer lists pull requests." +
            (gitUsesTheAccount ? " Git over HTTPS asks your own credential helpers again." : string.Empty);

        /// <summary>The dialog's message after the token checked out but its save failed (ruling 19).</summary>
        public static string SignInSaveFailed(string login, string reason) =>
            $"Signed in as {login}, but the token could not be saved in {HostingTexts.Keychain}: {WithoutFullStop(reason)}. VisualCommit keeps it until it closes and asks for it again at the next start.";

        /// <summary>
        /// The dialog's error for GitHub's answer (8.2): its refusal of the token, the network's
        /// message as it is (D284), or GitHub's own words for any other kind. The dialog keeps its
        /// own texts, not <c>HostingTexts.Describe</c>'s (1.7).
        /// </summary>
        public static string SignInFailed(HostingFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return failure.Kind switch
            {
                HostingFailureKind.BadCredentials => $"GitHub refused the token: {WithoutFullStop(failure.Message)}.",
                HostingFailureKind.Network => failure.Message,
                _ => $"GitHub answered: {WithoutFullStop(failure.Message)}.",
            };
        }

        /// <summary>A reason or message without the full stop at its end, before the sentence's own.</summary>
        private static string WithoutFullStop(string text) => text.EndsWith('.') ? text[..^1] : text;
    }
}
