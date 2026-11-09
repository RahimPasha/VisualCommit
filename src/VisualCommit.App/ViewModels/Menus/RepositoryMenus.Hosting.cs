// The pull requests' menus and items (docs/phase-7a-design.md 10.4; D251, D257, D306, D307,
// D313): a pull request's row, the "Pull requests" section's header, and the pull-request item of
// the branch and drop menus. Pure functions of the context (D86): without a hosted remote
// RepositoryMenuContext.Hosting is null and every one gives null, so every earlier menu is as it was.
// Since phase 7b (docs/phase-7b-design.md 12.4; D351, D355) each text is the listed repository's
// provider's, and the branch and drop items read RepositoryHostingState.RequestRemotes: only a
// remote of the listed provider (and on Azure DevOps of its organization) can take a request, and
// D356's OtherOrganization state offers none (D350).

using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
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
            // A gone one's number in the listed provider's form (12.4).
            var gone = HostingTexts.Number(context.Hosting?.Repository?.Provider ?? HostingProvider.GitHub, number);
            return Build(Header(gone));
        }

        var header = OperationTexts.RequestHeader(pullRequest.BaseRepository.Provider, pullRequest.Number, pullRequest.Title);
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
    /// The "Pull requests" header's menu (10.4, D307; docs/phase-7b-design.md 12.4): null without a
    /// hosted remote (D92); its header the section's title, the listed provider's (<c>Merge
    /// requests</c> on GitLab); signed out sign in to {P}…, open {Rs} in browser (which asks the
    /// provider nothing, D257); D356's other organization open {Rs} in browser alone (D376: the
    /// user's own browser; the app sends nothing); else refresh {Rs}, disabled while they load, and
    /// open {Rs} in browser.
    /// </summary>
    private static MenuModel? PullRequestsSection(RepositoryMenuContext context, IRepositoryActions actions)
    {
        if (context.Hosting is not { } hosting)
        {
            return null;
        }

        var provider = hosting.Repository?.Provider ?? HostingProvider.GitHub;
        var title = hosting.Repository is null ? LeftPanelViewModel.PullRequestsTitle : HostingTexts.RequestsTitle(provider);
        var browse = Item(actions, OpenRequestsInBrowser(provider), actions.OpenPullRequestsInBrowserAsync);
        return hosting.PullRequests switch
        {
            PullRequestsState.SignedOut => Build(Header(title), [Item(actions, HostingTexts.SignInToItemOf(provider), actions.SignInToHostAsync), browse]),
            PullRequestsState.OtherOrganization => Build(Header(title), [browse]),
            _ => Build(
                Header(title),
                [
                    Item(actions, RefreshRequests(provider), actions.RefreshPullRequestsAsync, hosting.PullRequests == PullRequestsState.Loading ? RequestsLoading(provider) : null),
                    browse,
                ]),
        };
    }

    /// <summary>
    /// A branch menu's pull-request item (10.4, D306; ruling 11; docs/phase-7b-design.md 12.4): null
    /// without a hosted remote, while D356's other organization holds, for a local branch when no
    /// remote can take a request, and for a remote branch of a remote that is no key of
    /// <see cref="RepositoryHostingState.RequestRemotes"/> (<c>upstream/main</c> of GitHub beside a
    /// listed GitLab origin); "Open {R} {N}" when the hosting's <see cref="RepositoryHostingState.ByRef"/>
    /// holds the branch (a local branch through an upstream of the same name, D313), else "Create
    /// {R}…" (WP-N's form). Never disabled: signed out, the create signs in first.
    /// </summary>
    private static MenuItem? BranchPullRequestItem(RepositoryMenuContext context, GitRef branch, IRepositoryActions actions)
    {
        if (context.Hosting is not { } hosting || hosting.PullRequests == PullRequestsState.OtherOrganization)
        {
            return null;
        }

        var remotes = hosting.RequestRemotes;
        if (remotes.Count == 0
            || (branch.Kind == RefKind.RemoteBranch && (branch.RemoteName is not { } remote || !remotes.ContainsKey(remote))))
        {
            return null;
        }

        var provider = hosting.Repository?.Provider ?? HostingProvider.GitHub;
        return hosting.ByRef.TryGetValue(branch.FullName, out var open)
            ? Item(actions, OpenRequest(open.BaseRepository.Provider, open.Number), () => actions.OpenPullRequestAsync(open))
            : Item(actions, CreateRequest(provider), () => actions.CreatePullRequestAsync(branch.FullName, null));
    }

    /// <summary>
    /// A drop's pull-request item (10.4, D306), in a group between the rebase items and the compare
    /// item: only onto a remote branch of a remote that can take a request
    /// (<see cref="RepositoryHostingState.RequestRemotes"/>), or onto a local branch whose upstream is
    /// a branch of one under the same name (12.3 step 3); never while D356's other organization holds;
    /// "Open {R} {N}" when the source has an open one, else "Create {R}…" from the source into the
    /// target.
    /// </summary>
    private static MenuItem?[] DropPullRequestItems(RepositoryMenuContext context, string sourceRef, string targetRef, GitRef? target, IRepositoryActions actions)
    {
        if (context.Hosting is not { } hosting || hosting.PullRequests == PullRequestsState.OtherOrganization
            || target is null || !IsRequestBase(hosting.RequestRemotes, target))
        {
            return [];
        }

        var provider = hosting.Repository?.Provider ?? HostingProvider.GitHub;
        return hosting.ByRef.TryGetValue(sourceRef, out var open)
            ? [Item(actions, OpenRequest(open.BaseRepository.Provider, open.Number), () => actions.OpenPullRequestAsync(open))]
            : [Item(actions, CreateRequest(provider), () => actions.CreatePullRequestAsync(sourceRef, targetRef))];
    }

    /// <summary>
    /// Whether a drop's target names a branch a request can go to: a branch of one of
    /// <paramref name="remotes"/>, or a local branch tracking one of the same name.
    /// </summary>
    private static bool IsRequestBase(IReadOnlyDictionary<string, HostedRepository> remotes, GitRef target) => target.Kind switch
    {
        RefKind.RemoteBranch => target.RemoteName is { } remote && remotes.ContainsKey(remote),
        RefKind.LocalBranch => target.UpstreamRemote is { } remote
            && remotes.ContainsKey(remote)
            && string.Equals(target.UpstreamRemoteRef, HeadsPrefix + target.Name, StringComparison.Ordinal),
        _ => false,
    };
}
