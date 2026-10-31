using System.Globalization;
using VisualCommit.App.ViewModels.Hosting;

namespace VisualCommit.App.ViewModels.Operations;

public static partial class OperationTexts
{
    /// <summary>
    /// The texts of a new pull request (docs/phase-7a-design.md 12.6; D312 to D318): the form, the
    /// operation, the head's notes and the failures the form shows on its error line. A failure of
    /// GitHub's that the table gives no row of its own is <c>HostingTexts.Describe</c>'s (D338).
    /// Counts use the singular for one (D113).
    /// </summary>
    public static class CreatePullRequestTexts
    {
        // The form (12.4) and the status bar (12.2).

        public const string DialogTitle = "Create pull request";

        public const string From = "From";

        public const string Into = "Into";

        /// <summary>Into while A3 is read (12.4's loading state), in the secondary colour.</summary>
        public const string ReadingGitHub = "Reading " + HostingTexts.GitHub + "…";

        public const string TitlePlaceholder = "Title";

        public const string DescriptionPlaceholder = "Description";

        public const string Draft = "Draft";

        public const string Create = "Create";

        public const string Creating = "Creating…";

        /// <summary>The stacked Pick's title (12.3).</summary>
        public const string ChooseBaseTitle = "Choose the branch to merge into";

        /// <summary>Create's reason without a base.</summary>
        public const string ChooseBase = "Choose the branch to merge into.";

        /// <summary>Create's reason with a blank title.</summary>
        public const string EnterTitle = "Enter a title.";

        /// <summary>The status bar's offer after a create (D317; the owner's answer of 2026-10-29).</summary>
        public const string OpenInBrowser = "Open in browser";

        /// <summary>The form's button after a 422 that found the pull request open (D316): <c>Open pull request #1</c>.</summary>
        public static string OpenExisting(int number) => $"Open pull request #{Number(number)}";

        // The operation (12.5).

        public static OperationText Operation(string head) => new(
            $"Create pull request for {head}",
            CreatingPullRequest,
            "Create pull request failed",
            HostingTexts.GitHub + " could not create the pull request.");

        /// <summary>The running text from the POST on (12.5 step 2).</summary>
        public const string CreatingPullRequest = "Creating pull request…";

        /// <summary>The status bar's message: <c>Created pull request #9</c>, <c>Created draft pull request #9</c>.</summary>
        public static string Created(int number, bool draft) =>
            $"Created {(draft ? "draft " : string.Empty)}pull request #{Number(number)}";

        // The head's notes (12.3 step 3, D313): what Create pushes first.

        /// <summary>A branch without an upstream: pushed with <c>--set-upstream</c>.</summary>
        public static string PushedFirstWithUpstream(string target) =>
            $"Not on {HostingTexts.GitHub} yet: Create pushes it to {target} first and sets that as its upstream.";

        /// <summary>A branch whose upstream lies elsewhere, missing on the first GitHub remote: pushed, its upstream left as it is.</summary>
        public static string PushedFirst(string target) =>
            $"Not on {HostingTexts.GitHub} yet: Create pushes it to {target} first.";

        /// <summary>Commits GitHub's branch lacks: pushed first.</summary>
        public static string AheadPushedFirst(int count, string target) => count == 1
            ? $"1 commit is not on {HostingTexts.GitHub} yet: Create pushes it to {target} first."
            : $"{Number(count)} commits are not on {HostingTexts.GitHub} yet: Create pushes them to {target} first.";

        /// <summary>The upstream of the same name is gone: pushed to it again.</summary>
        public static string GonePushedFirst(string target) => $"{target} is gone: Create pushes the branch to it again first.";

        // The error line (12.5's "Afterwards", 12.6's table).

        /// <summary>Head and base one branch of one repository (D318): <paramref name="branch"/> is GitHub's name of it.</summary>
        public static string SameBranch(string branch) => $"Choose a branch other than {branch} to merge into.";

        /// <summary>A 422 "already exists" whose pull request the list read again holds open (D316).</summary>
        public static string AlreadyOpen(int number, string head, string baseText) =>
            $"Pull request #{Number(number)} is already open from {head} into {baseText}.";

        /// <summary>A 422 "No commits between".</summary>
        public static string NoCommits(string baseText, string head) =>
            $"{baseText} already has every commit of {head}: there is nothing to merge.";

        /// <summary>A 422 "head is invalid": <paramref name="label"/> is GitHub's <c>owner:branch</c>.</summary>
        public static string HeadNotAccepted(string label, string repository) =>
            $"{HostingTexts.GitHub} does not accept {label} as the branch to merge from. It must be in {repository} or in a fork of it.";

        /// <summary>A 422 "base is invalid".</summary>
        public static string BaseUnknown(string branch, string repository) =>
            $"{HostingTexts.GitHub} has no branch {branch} in {repository}. Choose another branch to merge into.";

        /// <summary>A 403 with head and base in different repositories.</summary>
        public static string ForkTokenRefused(string repository) =>
            $"{HostingTexts.GitHub} refused to create a pull request in {repository} with this token. A fine-grained token cannot open pull requests in another owner's repository; sign in with a classic token that has the repo scope to do that.";

        /// <summary>Any other 403; with GitHub's permission header, the permission it needs.</summary>
        public static string Refused(string repository, bool namesPermission) =>
            $"{HostingTexts.GitHub} refused to create a pull request in {repository} with this token."
            + (namesPermission ? " It needs the permission pull_requests=write." : string.Empty);

        /// <summary>A 404.</summary>
        public static string NotVisible(string repository) => $"This account cannot see {repository} on {HostingTexts.GitHub}.";

        /// <summary>A git failure of the push, other than rejected: git's first line after the colon.</summary>
        public static string PushFailed(string branch, string target, string gitLine) =>
            $"Git could not push {branch} to {target}: {gitLine}";

        public const string Cancelled = "Cancelled before the pull request was created.";

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
