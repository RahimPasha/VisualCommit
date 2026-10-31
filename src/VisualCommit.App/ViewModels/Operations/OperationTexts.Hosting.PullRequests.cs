using System.Globalization;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The texts of the left panel's "Pull requests" section, its menus, the branch and drop menus'
/// pull-request items and a pull request's checkout (docs/phase-7a-design.md 10.2; D251, D303 to
/// D308). None of them appears without a GitHub remote. A failure to list is worded by
/// <c>HostingTexts.Describe</c> (D338), not here.
/// </summary>
public static partial class OperationTexts
{
    // The section's message rows (10.3).

    /// <summary>Signed out (D257): the row opens "Add GitHub account" (8.5).</summary>
    public const string SignInToSeePullRequests = "Sign in to GitHub to see pull requests…";

    public const string LoadingPullRequests = "Loading pull requests…";

    public const string NoOpenPullRequests = "No open pull requests";

    /// <summary>The provider stopped at 300 (ruling 14): the row opens the list in the browser.</summary>
    public const string MorePullRequests = "More than 300 open pull requests";

    /// <summary>A draft's mark in its row and its tooltip (10.3).</summary>
    public const string PullRequestDraft = "Draft";

    /// <summary>A pull request's row and its menu's header: <c>#1 Add the login form</c>.</summary>
    public static string PullRequestHeader(int number, string title) => $"#{Number(number)} {title}";

    /// <summary>A branch row's <c>#1</c> tooltip (D308): <c>Pull request #1: Add the login form</c>.</summary>
    public static string PullRequestOfBranch(int number, string title) => $"Pull request #{Number(number)}: {title}";

    /// <summary>
    /// A pull request's row's tooltip (10.3; the report's open point 26): its header, a line break,
    /// <c>{author} · {head label} → {base ref}</c> (the author left out when GitHub gave none) and,
    /// for a draft, <c> · Draft</c>.
    /// </summary>
    public static string PullRequestToolTip(int number, string title, string? author, string headLabel, string baseRef, bool isDraft)
    {
        var second = $"{headLabel} → {baseRef}";
        if (!string.IsNullOrEmpty(author))
        {
            second = $"{author} · {second}";
        }

        if (isDraft)
        {
            second += " · " + PullRequestDraft;
        }

        return PullRequestHeader(number, title) + "\n" + second;
    }

    // The checkout of a pull request through FETCH_HEAD (10.5, D305).

    public static OperationText FetchPullRequest(int number, string remote) => new(
        $"Fetch pull request #{Number(number)}",
        $"Fetching pull request #{Number(number)} from {remote}…",
        "Pull request not fetched",
        $"Git could not fetch pull request #{Number(number)} from {remote}.");

    public static string FetchedPullRequest(int number, string branch) => $"Fetched #{Number(number)} into {branch}";

    /// <summary>FETCH_HEAD held another fetch's line when it was read (10.5): no branch is created.</summary>
    public const string FetchHeadChanged = "Another fetch changed FETCH_HEAD; try again.";

    private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);

    public static partial class MenuTexts
    {
        // Pull requests (docs/phase-7a-design.md 10.2, 10.4; D306, D307).

        public const string OpenInBrowser = "Open in browser";

        public const string CopyLink = "Copy link";

        public const string RefreshPullRequests = "Refresh pull requests";

        public const string OpenPullRequestsInBrowser = "Open pull requests in browser";

        /// <summary>In a branch or drop menu without an open pull request (D306); WP-N's form.</summary>
        public const string CreatePullRequest = "Create pull request…";

        /// <summary>In a branch or drop menu in place of "Create pull request…" (ruling 11).</summary>
        public static string OpenPullRequest(int number) => $"Open pull request #{number.ToString(CultureInfo.InvariantCulture)}";

        /// <summary>"Refresh pull requests" while a list is read (10.4).</summary>
        public const string PullRequestsLoading = "Pull requests are loading";

        /// <summary>A fork's pull request whose base has no remote here (10.5 step 2).</summary>
        public static string AddRemoteFor(string fullName) => $"Add a remote for {fullName} to check out this pull request";
    }
}
