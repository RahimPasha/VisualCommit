// The pull requests' menus and items (docs/phase-7a-design.md 10.4; D251, D257, D306, D307,
// D313): a pull request's row, the "Pull requests" section's header, and the pull-request item of
// the branch and drop menus. Pure functions of the context (D86): without a GitHub remote
// RepositoryMenuContext.Hosting is null and every one gives null, so every earlier menu is as it was.

using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Hosting;
using static VisualCommit.App.ViewModels.Operations.OperationTexts.MenuTexts;

namespace VisualCommit.App.ViewModels.Menus;

public static partial class RepositoryMenus
{
    /// <summary>
    /// A pull request's row (10.4, D307): header <c>#1 Add the login form</c> (cut by the view, whole
    /// in its tooltip); open in browser, check out <c>{head}</c> (<see cref="PullRequestCheckout"/>'s
    /// label and reason) — copy link. The pull request is the listed one of
    /// <paramref name="number"/>, else <paramref name="shown"/>, the row's own (the rows a failed
    /// refresh keeps, design 17 item 3); neither: the header alone.
    /// </summary>
    public static MenuModel ForPullRequest(RepositoryMenuContext context, int number, IRepositoryActions actions, PullRequest? shown = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actions);
        var pullRequest = context.Hosting?.List?.PullRequests.FirstOrDefault(pull => pull.Number == number)
            ?? (shown?.Number == number ? shown : null);
        if (context.Hosting is not { } hosting || pullRequest is null)
        {
            var gone = "#" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Build(Header(gone));
        }

        var header = OperationTexts.PullRequestHeader(pullRequest.Number, pullRequest.Title);
        var checkout = PullRequestCheckout.Plan(hosting, context.Refs, pullRequest);
        return Build(
            Header(header, header),
            [
                Item(actions, OpenInBrowser, () => actions.OpenPullRequestAsync(pullRequest)),
                Item(actions, checkout.Label, () => actions.CheckOutPullRequestAsync(pullRequest), checkout.DisabledReason),
            ],
            [
                Item(actions, CopyLink, () => actions.CopyPullRequestLinkAsync(pullRequest)),
            ]);
    }

    /// <summary>
    /// The "Pull requests" header's menu (10.4, D307): null without a GitHub remote (D92); signed out
    /// sign in to GitHub…, open pull requests in browser (which asks GitHub nothing, D257); else
    /// refresh pull requests, disabled while they load, and open pull requests in browser.
    /// </summary>
    private static MenuModel? PullRequestsSection(RepositoryMenuContext context, IRepositoryActions actions)
    {
        if (context.Hosting is not { } hosting)
        {
            return null;
        }

        var browse = Item(actions, OpenPullRequestsInBrowser, actions.OpenPullRequestsInBrowserAsync);
        return hosting.PullRequests == PullRequestsState.SignedOut
            ? Build(Header(LeftPanelViewModel.PullRequestsTitle), [Item(actions, HostingTexts.SignInToGitHubItem, actions.SignInToGitHubAsync), browse])
            : Build(
                Header(LeftPanelViewModel.PullRequestsTitle),
                [
                    Item(actions, RefreshPullRequests, actions.RefreshPullRequestsAsync, hosting.PullRequests == PullRequestsState.Loading ? PullRequestsLoading : null),
                    browse,
                ]);
    }

    /// <summary>
    /// A branch menu's pull-request item (10.4, D306; ruling 11): null without a GitHub remote and
    /// for a remote branch of a remote GitHub does not have; "Open pull request #n" when the hosting's
    /// <see cref="RepositoryHostingState.ByRef"/> holds the branch (a local branch through an upstream
    /// of the same name, D313), else "Create pull request…" (WP-N's form). Never disabled: signed out,
    /// the create signs in first.
    /// </summary>
    private static MenuItem? BranchPullRequestItem(RepositoryMenuContext context, GitRef branch, IRepositoryActions actions)
    {
        if (context.Hosting is not { } hosting
            || (branch.Kind == RefKind.RemoteBranch && (branch.RemoteName is not { } remote || !hosting.Remotes.ContainsKey(remote))))
        {
            return null;
        }

        return hosting.ByRef.TryGetValue(branch.FullName, out var open)
            ? Item(actions, OpenPullRequest(open.Number), () => actions.OpenPullRequestAsync(open))
            : Item(actions, CreatePullRequest, () => actions.CreatePullRequestAsync(branch.FullName, null));
    }

    /// <summary>
    /// A drop's pull-request item (10.4, D306), in a group between the rebase items and the compare
    /// item: only onto a remote branch of a GitHub remote, or onto a local branch whose upstream is a
    /// branch of one under the same name (12.3 step 3); "Open pull request #n" when the source has an
    /// open one, else "Create pull request…" from the source into the target.
    /// </summary>
    private static MenuItem?[] DropPullRequestItems(RepositoryMenuContext context, string sourceRef, string targetRef, GitRef? target, IRepositoryActions actions)
    {
        if (context.Hosting is not { } hosting || target is null || !IsGitHubBase(hosting, target))
        {
            return [];
        }

        return hosting.ByRef.TryGetValue(sourceRef, out var open)
            ? [Item(actions, OpenPullRequest(open.Number), () => actions.OpenPullRequestAsync(open))]
            : [Item(actions, CreatePullRequest, () => actions.CreatePullRequestAsync(sourceRef, targetRef))];
    }

    /// <summary>Whether a drop's target names a branch on GitHub: a GitHub remote's branch, or a local branch tracking one of the same name.</summary>
    private static bool IsGitHubBase(RepositoryHostingState hosting, GitRef target) => target.Kind switch
    {
        RefKind.RemoteBranch => target.RemoteName is { } remote && hosting.Remotes.ContainsKey(remote),
        RefKind.LocalBranch => target.UpstreamRemote is { } remote
            && hosting.Remotes.ContainsKey(remote)
            && string.Equals(target.UpstreamRemoteRef, HeadsPrefix + target.Name, StringComparison.Ordinal),
        _ => false,
    };
}
