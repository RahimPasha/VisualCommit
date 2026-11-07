using System.Globalization;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Hosting;

namespace VisualCommit.App.ViewModels.Operations;

/// <summary>
/// The texts of the left panel's "Pull requests" section, its menus, the branch and drop menus'
/// pull-request items and a pull request's checkout (docs/phase-7a-design.md 10.2; D251, D303 to
/// D308). None of them appears without a hosted remote. A failure to list is worded by
/// <c>HostingTexts.Describe</c> (D338), not here.
/// </summary>
/// <remarks>
/// Since phase 7b each text has a provider form (docs/phase-7b-design.md 12.2; D351, D355, D362),
/// <c>p</c> first: the listed repository's provider, or a request's base repository's. For GitHub
/// each returns 7a's text word for word; the others are built from <see cref="HostingTexts"/>.
/// </remarks>
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

    // The provider forms (docs/phase-7b-design.md 12.2).

    /// <summary>Signed out: <c>Sign in to {P} to see {Rs}…</c>.</summary>
    public static string SignInToSee(HostingProvider p) =>
        p == HostingProvider.GitHub ? SignInToSeePullRequests : $"Sign in to {HostingTexts.Name(p)} to see {HostingTexts.Requests(p)}…";

    /// <summary><c>Loading {Rs}…</c>.</summary>
    public static string LoadingRequests(HostingProvider p) =>
        p == HostingProvider.GitHub ? LoadingPullRequests : $"Loading {HostingTexts.Requests(p)}…";

    /// <summary><c>No open {Rs}</c>.</summary>
    public static string NoOpenRequests(HostingProvider p) =>
        p == HostingProvider.GitHub ? NoOpenPullRequests : $"No open {HostingTexts.Requests(p)}";

    /// <summary><c>More than 300 open {Rs}</c>.</summary>
    public static string MoreRequests(HostingProvider p) =>
        p == HostingProvider.GitHub ? MorePullRequests : $"More than 300 open {HostingTexts.Requests(p)}";

    /// <summary>A request's row and its menu's header: <c>{N} {title}</c> (<c>!21 Add the search box</c>).</summary>
    public static string RequestHeader(HostingProvider p, int number, string title) =>
        p == HostingProvider.GitHub ? PullRequestHeader(number, title) : $"{HostingTexts.Number(p, number)} {title}";

    /// <summary>A branch row's number's tooltip: <c>{RT} {N}: {title}</c> (<c>Merge request !21: Add the search box</c>).</summary>
    public static string RequestOfBranch(HostingProvider p, int number, string title) =>
        p == HostingProvider.GitHub ? PullRequestOfBranch(number, title) : $"{HostingTexts.RequestTitle(p)} {HostingTexts.Number(p, number)}: {title}";

    /// <summary><see cref="PullRequestToolTip"/> with <see cref="RequestHeader"/>'s first line.</summary>
    public static string RequestToolTip(HostingProvider p, int number, string title, string? author, string headLabel, string baseRef, bool isDraft)
    {
        var github = PullRequestToolTip(number, title, author, headLabel, baseRef, isDraft);
        return p == HostingProvider.GitHub
            ? github
            : RequestHeader(p, number, title) + github[PullRequestHeader(number, title).Length..];
    }

    /// <summary>A request's checkout through FETCH_HEAD (12.5): <c>Fetch {R} {N}</c> and its three other texts.</summary>
    public static OperationText FetchRequest(HostingProvider p, int number, string remote)
    {
        if (p == HostingProvider.GitHub)
        {
            return FetchPullRequest(number, remote);
        }

        var request = HostingTexts.Request(p);
        var n = HostingTexts.Number(p, number);
        return new(
            $"Fetch {request} {n}",
            $"Fetching {request} {n} from {remote}…",
            $"{HostingTexts.RequestTitle(p)} not fetched",
            $"Git could not fetch {request} {n} from {remote}.");
    }

    /// <summary><c>Fetched {N} into {branch}</c>.</summary>
    public static string FetchedRequest(HostingProvider p, int number, string branch) =>
        p == HostingProvider.GitHub ? FetchedPullRequest(number, branch) : $"Fetched {HostingTexts.Number(p, number)} into {branch}";

    /// <summary>
    /// D356's row: the listed remote is in another Azure DevOps organization than the held
    /// account's: <c>In {repo}; signed in to {account}</c>.
    /// </summary>
    public static string OtherOrganization(string repository, string account) => $"In {repository}; signed in to {account}";

    /// <summary>D356's row's tooltip: why nothing is asked, and how to see the requests.</summary>
    public static string OtherOrganizationToolTip(string repository, string account)
    {
        var azure = HostingTexts.Name(HostingProvider.AzureDevOps);
        return $"This repository is in the {azure} organization {repository}; the signed-in account is in {account}, "
            + $"so nothing is asked of {azure} for it. To see its {HostingTexts.Requests(HostingProvider.AzureDevOps)}, "
            + $"sign out and sign in to {repository} in Settings, Accounts.";
    }

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

        // The provider forms (docs/phase-7b-design.md 12.2).

        /// <summary><c>Refresh {Rs}</c>.</summary>
        public static string RefreshRequests(HostingProvider p) =>
            p == HostingProvider.GitHub ? RefreshPullRequests : $"Refresh {HostingTexts.Requests(p)}";

        /// <summary><c>Open {Rs} in browser</c>.</summary>
        public static string OpenRequestsInBrowser(HostingProvider p) =>
            p == HostingProvider.GitHub ? OpenPullRequestsInBrowser : $"Open {HostingTexts.Requests(p)} in browser";

        /// <summary><c>Create {R}…</c>, WP-N's action; public for the live command (docs/phase-7b-design.md 1.7, 15.6).</summary>
        public static string CreateRequest(HostingProvider p) =>
            p == HostingProvider.GitHub ? CreatePullRequest : $"Create {HostingTexts.Request(p)}…";

        /// <summary><c>Open {R} {N}</c>.</summary>
        public static string OpenRequest(HostingProvider p, int number) =>
            p == HostingProvider.GitHub ? OpenPullRequest(number) : $"Open {HostingTexts.Request(p)} {HostingTexts.Number(p, number)}";

        /// <summary><c>{RsT} are loading</c>.</summary>
        public static string RequestsLoading(HostingProvider p) =>
            p == HostingProvider.GitHub ? PullRequestsLoading : $"{HostingTexts.RequestsTitle(p)} are loading";

        /// <summary><c>Add a remote for {fullName} to check out this {R}</c>.</summary>
        public static string AddRemoteFor(HostingProvider p, string fullName) =>
            p == HostingProvider.GitHub ? AddRemoteFor(fullName) : $"Add a remote for {fullName} to check out this {HostingTexts.Request(p)}";

        /// <summary>An Azure DevOps request without a head repository (D361): <c>{P} names no repository for this {R}'s branch</c>.</summary>
        public static string HeadNotNamed(HostingProvider p) =>
            $"{HostingTexts.Name(p)} names no repository for this {HostingTexts.Request(p)}'s branch";

        /// <summary>An Azure DevOps request's own branch that the remote's fetch lines leave out (D361): <c>{remote} does not fetch {branch}</c>.</summary>
        public static string NotFetchedBy(string remote, string branch) => $"{remote} does not fetch {branch}";
    }
}
