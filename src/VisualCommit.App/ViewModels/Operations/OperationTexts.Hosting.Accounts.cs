using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Hosting.AzureDevOps;

namespace VisualCommit.App.ViewModels.Operations;

public static partial class OperationTexts
{
    /// <summary>
    /// The texts of the settings screen's Accounts section and of the "Add … account" dialogs
    /// (docs/phase-7a-design.md 8.2; D249, D253, D256, D277, D291, D294 to D298; for GitLab and
    /// Azure DevOps docs/phase-7b-design.md 10.2, D351, D362, D378, D385). 7a's constants stay word
    /// for word, and each <c>…Of(p)</c> member returns them for GitHub; a provider's name comes from
    /// <see cref="HostingTexts.Name"/>, its request words from <see cref="HostingTexts.Requests"/>. A
    /// <c>{reason}</c> or <c>{message}</c> loses a full stop at its end before the sentence's own.
    /// One wording names every platform's store: <see cref="HostingTexts.Keychain"/> (ruling 17).
    /// </summary>
    public static class AccountTexts
    {
        // The section (8.3).
        /// <summary>One sentence for the three providers (D353; docs/phase-7b-design.md 10.2).</summary>
        public const string AccountsIntro =
            "With a GitHub, GitLab or Azure DevOps account, VisualCommit lists and creates pull requests (merge requests on GitLab) and clones your repositories.";
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

        /// <summary>
        /// The dialog's error when the check failed in a way the session did not word, such as its own
        /// end: word for word WP-G's fixed text for a request that got no answer (D284), which its
        /// internal client keeps.
        /// </summary>
        public const string SignInUnreachable = "Could not reach GitHub.";

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
            (gitUsesTheAccount ? GitAsksTheHelpersAgain : string.Empty);

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

        // Phase 7b (docs/phase-7b-design.md 10.2): GitLab's and Azure DevOps' words beside GitHub's.

        /// <summary>GitLab's second link (D385): the legacy token's form, prefilled.</summary>
        public const string LegacyTokenLink = "Or create a legacy token, filled in";

        public const string LegacyTokenLinkTip = "Opens GitLab's form for a legacy token with the api scope in your browser.";

        /// <summary>Azure DevOps' first box's label.</summary>
        public const string OrganizationLabel = "Organization";

        /// <summary>Azure DevOps' organization box gives no organization (<see cref="AzureDevOpsProvider.OrganizationOf"/>): refused without a request.</summary>
        public const string SignInNotAnOrganization =
            "Type the organization's name, of letters, digits and hyphens, or its address on dev.azure.com or visualstudio.com.";

        /// <summary>The sign-out question's last sentence with git 2.31 or newer (D277).</summary>
        private const string GitAsksTheHelpersAgain = " Git over HTTPS asks your own credential helpers again.";

        /// <summary>The block's "refused" note: GitHub's 7a's, the others' with their name.</summary>
        public static string NeedsSignInRefusedOf(HostingProvider p) =>
            p == HostingProvider.GitHub ? NeedsSignInRefused : $"{HostingTexts.Name(p)} refused the token. Sign in again with a new token.";

        /// <summary>
        /// The switch's note with git 2.31 or newer: GitHub's 7a's; GitLab's with its name; Azure
        /// DevOps' names the organization's repositories (D350), <paramref name="login"/> being the organization.
        /// </summary>
        public static string AccountUseForGitNoteOf(HostingProvider p, string login) => p switch
        {
            HostingProvider.GitHub => AccountUseForGitNote,
            HostingProvider.AzureDevOps =>
                $"Clone, fetch, pull and push to {login}'s repositories on Azure DevOps over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.",
            _ => $"Clone, fetch, pull and push to {HostingTexts.Name(p)} over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers.",
        };

        /// <summary>The sign-out's confirmation's title.</summary>
        public static string SignOutTitleOf(HostingProvider p) => p == HostingProvider.GitHub ? SignOutTitle : $"Sign out of {HostingTexts.Name(p)}?";

        /// <summary>
        /// The sign-out's question: GitHub's 7a's (<see cref="SignOutQuestion"/>); GitLab's names the
        /// login and merge requests, Azure DevOps' the organization; the last sentence left out when
        /// git is older than 2.31 (D277).
        /// </summary>
        public static string SignOutQuestionOf(HostingProvider p, string login, bool gitUsesTheAccount)
        {
            if (p == HostingProvider.GitHub)
            {
                return SignOutQuestion(login, gitUsesTheAccount);
            }

            var whom = p == HostingProvider.AzureDevOps ? $"for {login}" : $"as {login}";
            return $"Sign out of {HostingTexts.Name(p)} {whom}? VisualCommit removes its token from this computer and no longer lists {HostingTexts.Requests(p)}."
                + (gitUsesTheAccount ? GitAsksTheHelpersAgain : string.Empty);
        }

        /// <summary>The dialog's help (D345, D360, D385).</summary>
        public static string SignInHelpOf(HostingProvider p) => p switch
        {
            HostingProvider.GitLab =>
                "Paste a personal access token from GitLab. A fine-grained token needs User: Read and Project: Read among its user permissions, and Project: Read, Merge Request: Read and Create, " +
                "and Code: Download and Push for the groups and projects you work in; a legacy token needs the api scope.",
            HostingProvider.AzureDevOps =>
                "Type your organization, then paste a personal access token made for it in Azure DevOps with the scope Code (Read & write). " +
                "The organization can be its name or its address on dev.azure.com or visualstudio.com.",
            _ => SignInHelp,
        };

        /// <summary>The dialog's link to the provider's page for a new token.</summary>
        public static string TokenLinkOf(HostingProvider p) => p switch
        {
            HostingProvider.GitLab => "Create a fine-grained token on GitLab",
            HostingProvider.AzureDevOps => "Create a token on Azure DevOps",
            _ => SignInTokenLink,
        };

        /// <summary>The link's tooltip.</summary>
        public static string TokenLinkTipOf(HostingProvider p) => p switch
        {
            HostingProvider.GitLab => "Opens GitLab's form for a new fine-grained token in your browser.",
            HostingProvider.AzureDevOps => "Opens the organization's page for new tokens in your browser.",
            _ => SignInTokenLinkTip,
        };

        /// <summary>The dialog's error when the check ended without a worded failure (D284): "Could not reach {name}."</summary>
        public static string SignInUnreachableOf(HostingProvider p) => p == HostingProvider.GitHub ? SignInUnreachable : $"Could not reach {HostingTexts.Name(p)}.";

        /// <summary>
        /// The dialog's error for the service's answer, by <see cref="HostingFailure.Provider"/>:
        /// GitHub's 7a's (<see cref="SignInFailed"/>), GitLab's 7a's words with its name; Azure
        /// DevOps' fixed texts (its 401 bodies are empty, HTML or a GUID's, g1 G0.2): <c>NotFound</c>
        /// no such organization, the Anonymous identity (<see cref="AzureDevOpsProvider.AnonymousDetail"/>)
        /// apart from another refusal, the network's message as it is (D284), any other kind its
        /// message. <paramref name="organization"/> is the organization the box gave, read by Azure
        /// DevOps' texts alone. No service sentence decides a text (r1).
        /// </summary>
        public static string SignInFailedOf(HostingFailure failure, string? organization)
        {
            ArgumentNullException.ThrowIfNull(failure);
            if (failure.Provider == HostingProvider.GitHub)
            {
                return SignInFailed(failure);
            }

            if (failure.Provider == HostingProvider.AzureDevOps)
            {
                switch (failure.Kind)
                {
                    case HostingFailureKind.NotFound:
                        return $"Azure DevOps has no organization named {organization}.";
                    case HostingFailureKind.BadCredentials when failure.Detail == AzureDevOpsProvider.AnonymousDetail:
                        return $"Azure DevOps did not take the token as a sign-in and answered as it does for a visitor. Paste the whole token, made for {organization}.";
                    case HostingFailureKind.BadCredentials:
                        return $"Azure DevOps refused the token for {organization}. Check that it was made for this organization and has not expired.";
                }
            }

            var name = HostingTexts.Name(failure.Provider);
            return failure.Kind switch
            {
                HostingFailureKind.BadCredentials => $"{name} refused the token: {WithoutFullStop(failure.Message)}.",
                HostingFailureKind.Network => failure.Message,
                _ => $"{name} answered: {WithoutFullStop(failure.Message)}.",
            };
        }

        /// <summary>
        /// A token of another provider's form (<see cref="HostingProviders.TokenFormOf"/>), refused
        /// without a request (D378): "a" before a name, "an" before "Azure DevOps"; Azure DevOps'
        /// names the organization the box gives (<paramref name="organization"/>, null for the
        /// others). It names no Settings, Accounts: a provider that is off has no block there (D362).
        /// </summary>
        public static string SignInOtherProviderTokenOf(HostingProvider p, HostingProvider other, string? organization)
        {
            var article = other == HostingProvider.AzureDevOps ? "an" : "a";
            var paste = p == HostingProvider.AzureDevOps
                ? $"Paste a token made for {organization} on Azure DevOps."
                : $"Paste a token made on {HostingTexts.Name(p)}.";
            return $"This looks like {article} {HostingTexts.Name(other)} token. {paste}";
        }


        /// <summary>A reason or message without the full stop at its end, before the sentence's own.</summary>
        private static string WithoutFullStop(string text) => text.EndsWith('.') ? text[..^1] : text;
    }
}
