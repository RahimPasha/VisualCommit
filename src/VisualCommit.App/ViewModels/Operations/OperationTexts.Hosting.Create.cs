using System.Globalization;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Hosting;

namespace VisualCommit.App.ViewModels.Operations;

public static partial class OperationTexts
{
    /// <summary>
    /// The texts of a new pull request (docs/phase-7a-design.md 12.6; D312 to D318): the form, the
    /// operation, the head's notes and the failures the form shows on its error line. A failure of
    /// GitHub's that the table gives no row of its own is <c>HostingTexts.Describe</c>'s (D338).
    /// Counts use the singular for one (D113). The forms that take a provider first
    /// (docs/phase-7b-design.md 13.2; D351, D355, D362) word them for the listed repository's
    /// provider from <c>HostingTexts</c>' <c>Name</c>, <c>Request</c>, <c>RequestTitle</c> and
    /// <c>Number</c>, and return 7a's text for GitHub, which 7a's members keep word for word.
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

        /// <summary>An <c>AlreadyExists</c> refusal whose pull request the list read again holds open (D316).</summary>
        public static string AlreadyOpen(int number, string head, string baseText) =>
            $"Pull request #{Number(number)} is already open from {head} into {baseText}.";

        /// <summary>GitHub's refusal <c>NoCommits</c> (a 422).</summary>
        public static string NoCommits(string baseText, string head) =>
            $"{baseText} already has every commit of {head}: there is nothing to merge.";

        /// <summary>GitHub's refusal <c>HeadMissing</c> (a 422): <paramref name="label"/> is GitHub's <c>owner:branch</c>.</summary>
        public static string HeadNotAccepted(string label, string repository) =>
            $"{HostingTexts.GitHub} does not accept {label} as the branch to merge from. It must be in {repository} or in a fork of it.";

        /// <summary>GitHub's refusal <c>BaseMissing</c> (a 422).</summary>
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

        // The provider forms (docs/phase-7b-design.md 13.2): 7a's texts above for GitHub.

        /// <summary>The form's title: <c>Create merge request</c>, <c>Create pull request</c>.</summary>
        public static string DialogTitleOf(HostingProvider p) => $"Create {HostingTexts.Request(p)}";

        /// <summary>Into while A3 is read: <c>Reading GitLab…</c>.</summary>
        public static string ReadingOf(HostingProvider p) => $"Reading {HostingTexts.Name(p)}…";

        /// <summary>The running text from the POST on: <c>Creating merge request…</c>.</summary>
        public static string CreatingRequest(HostingProvider p) => $"Creating {HostingTexts.Request(p)}…";

        /// <summary>The operation: <c>Create merge request for {head}</c>, its running text, error title and summary.</summary>
        public static OperationText Operation(HostingProvider p, string head) => new(
            $"Create {HostingTexts.Request(p)} for {head}",
            CreatingRequest(p),
            $"Create {HostingTexts.Request(p)} failed",
            $"{HostingTexts.Name(p)} could not create the {HostingTexts.Request(p)}.");

        /// <summary>The status bar's message: <c>Created merge request !24</c>, <c>Created draft pull request !14</c>.</summary>
        public static string Created(HostingProvider p, int number, bool draft) =>
            $"Created {(draft ? "draft " : string.Empty)}{HostingTexts.Request(p)} {HostingTexts.Number(p, number)}";

        /// <summary>The form's button after a refusal that found the request open (D316): <c>Open merge request !24</c>.</summary>
        public static string OpenExisting(HostingProvider p, int number) => $"Open {HostingTexts.Request(p)} {HostingTexts.Number(p, number)}";

        /// <summary>An "already exists" whose request the list read again holds open (D316).</summary>
        public static string AlreadyOpen(HostingProvider p, int number, string head, string baseText) =>
            $"{HostingTexts.RequestTitle(p)} {HostingTexts.Number(p, number)} is already open from {head} into {baseText}.";

        /// <summary>A branch without an upstream: pushed with <c>--set-upstream</c>.</summary>
        public static string PushedFirstWithUpstream(HostingProvider p, string target) =>
            $"Not on {HostingTexts.Name(p)} yet: Create pushes it to {target} first and sets that as its upstream.";

        /// <summary>A branch whose upstream lies elsewhere, missing on the first remote of the provider: pushed, its upstream left as it is.</summary>
        public static string PushedFirst(HostingProvider p, string target) =>
            $"Not on {HostingTexts.Name(p)} yet: Create pushes it to {target} first.";

        /// <summary>Commits the provider's branch lacks: pushed first.</summary>
        public static string AheadPushedFirst(HostingProvider p, int count, string target) => count == 1
            ? $"1 commit is not on {HostingTexts.Name(p)} yet: Create pushes it to {target} first."
            : $"{Number(count)} commits are not on {HostingTexts.Name(p)} yet: Create pushes them to {target} first.";

        /// <summary>The base's branch is missing (<c>BaseMissing</c>).</summary>
        public static string BaseUnknown(HostingProvider p, string branch, string repository) =>
            $"{HostingTexts.Name(p)} has no branch {branch} in {repository}. Choose another branch to merge into.";

        /// <summary>The head's branch is missing on GitLab or Azure DevOps (<c>HeadMissing</c>): <paramref name="branch"/> the head's branch there, <paramref name="repository"/> its repository.</summary>
        public static string HeadUnknown(HostingProvider p, string branch, string repository) =>
            $"{HostingTexts.Name(p)} has no branch {branch} in {repository} to merge from. Fetch, then try again.";

        /// <summary>Head or base missing, the service naming neither alone (<c>BranchesMissing</c>): the head's and the base's branches.</summary>
        public static string BranchesUnknown(HostingProvider p, string head, string baseBranch) =>
            $"{HostingTexts.Name(p)} cannot find {head} or {baseBranch}. Fetch, then try again.";

        /// <summary>
        /// A 403 (on GitHub one within one repository): with the provider's detail, what the token
        /// needs, GitHub's permission or GitLab's scope; Azure DevOps names none.
        /// </summary>
        public static string Refused(HostingProvider p, string repository, bool namesPermission) =>
            $"{HostingTexts.Name(p)} refused to create a {HostingTexts.Request(p)} in {repository} with this token."
            + (!namesPermission ? string.Empty : p switch
            {
                HostingProvider.GitHub => " It needs the permission pull_requests=write.",
                HostingProvider.GitLab => " It needs the api scope, or Merge Request: Create.",
                _ => string.Empty,
            });

        /// <summary>A 404.</summary>
        public static string NotVisible(HostingProvider p, string repository) => $"This account cannot see {repository} on {HostingTexts.Name(p)}.";

        /// <summary>Cancelled within the push's window: <c>Cancelled before the merge request was created.</c> (7a's <see cref="Cancelled"/>).</summary>
        public static string CancelledOf(HostingProvider p) => $"Cancelled before the {HostingTexts.Request(p)} was created.";

        /// <summary>GitLab's sent title under Draft (13.4, ruling 20): <c>Title on GitLab: Draft: Add the search box</c>.</summary>
        public static string SentTitle(HostingProvider p, string title) => $"Title on {HostingTexts.Name(p)}: {title}";

        /// <summary>GitLab's note under the description (13.4): the description goes as typed.</summary>
        public static string QuickActionsNote => $"{HostingTexts.Name(HostingProvider.GitLab)} runs quick actions in the description, such as /close.";

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
