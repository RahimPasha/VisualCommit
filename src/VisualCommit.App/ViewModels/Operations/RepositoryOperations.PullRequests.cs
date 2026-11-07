// The pull requests of the left panel's section (docs/phase-7a-design.md 1.7, 10.4, 10.5; D251,
// D305, D307): open in the browser, check out, copy the link, refresh, and sign in to GitHub. Only
// the checkout through FETCH_HEAD is an operation of its own; opening and copying are not
// operations, and the refresh runs outside the queue.

using System.Globalization;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels.Operations;

public sealed partial class RepositoryOperations
{
    /// <inheritdoc cref="IRepositoryActions.OpenPullRequestAsync"/>
    /// <remarks>Its page through the launcher (D321): not an operation; a failure is logged.</remarks>
    public Task OpenPullRequestAsync(PullRequest pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        return LaunchAsync(OperationTexts.MenuTexts.OpenInBrowser, launcher => launcher.OpenUrlAsync(pullRequest.HtmlUrl));
    }

    /// <inheritdoc cref="IRepositoryActions.CheckOutPullRequestAsync"/>
    /// <remarks>
    /// <see cref="PullRequestCheckout.Plan"/>'s way (10.5, D305); a plan with a reason does nothing (a
    /// stale menu). A head on a remote: that remote branch's existing checkout, after the remote's
    /// existing fetch when the ref is not read yet, and only if it then exists. A local branch at the
    /// head: its checkout. Otherwise one network operation fetches the request's head ref
    /// (<c>refs/pull/&lt;n&gt;/head</c>, GitLab's <c>refs/merge-requests/&lt;iid&gt;/head</c>; Azure DevOps'
    /// plans of that kind are disabled, D361) into
    /// <c>FETCH_HEAD</c> only and creates the branch without upstream at its SHA, and the existing
    /// checkout follows as its own operation (its stash, detached-HEAD question, blocked dialog and
    /// undo). Never a <c>refs/pull</c> fetch line or a <c>refs/remotes/&lt;r&gt;/pr/&lt;n&gt;</c> ref.
    /// </remarks>
    public async Task CheckOutPullRequestAsync(PullRequest pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        if (IsClosed || Refs is not { } refs)
        {
            return;
        }

        var number = pullRequest.Number.ToString(CultureInfo.InvariantCulture);
        var plan = PullRequestCheckout.Plan(Repository.HostingState, refs, pullRequest);
        if (plan.DisabledReason is { } reason)
        {
            Log.Info($"{Repository.Name}: checking out pull request #{number}: not started, {reason}.");
            return;
        }

        switch (plan.Kind)
        {
            case PullRequestCheckoutKind.RemoteBranch:
                if (FindRef(plan.RemoteRef!) is null)
                {
                    await FetchAsync(plan.Remote!);
                    if (FindRef(plan.RemoteRef!) is null)
                    {
                        Log.Info($"{Repository.Name}: checking out pull request #{number}: not started, {plan.RemoteRef} does not exist after the fetch.");
                        return;
                    }
                }

                await CheckoutAsync(plan.RemoteRef!);
                return;

            case PullRequestCheckoutKind.LocalBranch:
                await CheckoutAsync(HeadsPrefix + plan.LocalBranch);
                return;

            default:
                if (await FetchPullRequestAsync(pullRequest, plan.Remote!, plan.LocalBranch!) is { Outcome: OperationOutcome.Succeeded })
                {
                    await CheckoutAsync(HeadsPrefix + plan.LocalBranch);
                }

                return;
        }
    }

    /// <inheritdoc cref="IRepositoryActions.CopyPullRequestLinkAsync"/>
    /// <remarks>Its page's address on the clipboard: not an operation; a failure is logged.</remarks>
    public Task CopyPullRequestLinkAsync(PullRequest pullRequest)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        return CopyAsync(OperationTexts.MenuTexts.CopyLink, pullRequest.HtmlUrl.AbsoluteUri);
    }

    /// <inheritdoc cref="IRepositoryActions.RefreshPullRequestsAsync"/>
    /// <remarks>The hosting's refresh, outside the queue (D289); signed out it asks nothing (D257); a failure is logged as a warning.</remarks>
    public async Task RefreshPullRequestsAsync()
    {
        if (IsClosed)
        {
            return;
        }

        try
        {
            await Repository.Hosting.RefreshPullRequestsAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning($"{Repository.Name}: {OperationTexts.MenuTexts.RefreshPullRequests} failed.", ex);
        }
    }

    /// <inheritdoc cref="IRepositoryActions.OpenPullRequestsInBrowserAsync"/>
    /// <remarks>
    /// The provider's <see cref="IHostingProvider.PullRequestsPage"/> of the repository listed (a
    /// fork's parent; D356's remote too, D376), else of the first remote a request can go to, through
    /// the launcher; signed out too, asking the provider nothing (D257); an info line when neither is
    /// known (docs/phase-7b-design.md 12.5).
    /// </remarks>
    public Task OpenPullRequestsInBrowserAsync()
    {
        if (IsClosed)
        {
            return Task.CompletedTask;
        }

        var state = Repository.HostingState;
        var requestRemotes = state.RequestRemotes;
        var repository = state.Repository
            ?? HostingRemoteOrder.Of(requestRemotes.Keys).Select(remote => requestRemotes[remote]).FirstOrDefault();
        if (repository is null || Services?.Hosting.ProviderOf(repository.Provider) is not { } provider)
        {
            Log.Info($"{Repository.Name}: {OperationTexts.MenuTexts.OpenPullRequestsInBrowser}: the repository has no hosted remote.");
            return Task.CompletedTask;
        }

        var pulls = provider.PullRequestsPage(repository);
        return LaunchAsync(OperationTexts.MenuTexts.OpenRequestsInBrowser(repository.Provider), launcher => launcher.OpenUrlAsync(pulls));
    }

    /// <inheritdoc cref="IRepositoryActions.SignInToHostAsync"/>
    /// <remarks>
    /// The window's entry (8.5): the listed repository's provider's "Add … account" unless signed in;
    /// the session refreshes the pull requests at sign-in. Nothing without a hosted remote or while
    /// D356's other organization holds (docs/phase-7b-design.md 12.5).
    /// </remarks>
    public async Task SignInToHostAsync()
    {
        var state = Repository.HostingState;
        if (IsClosed || Services?.SignIn is not { } signIn
            || state.PullRequests is PullRequestsState.NoHostedRemote or PullRequestsState.OtherOrganization
            || state.Repository is not { } listed)
        {
            return;
        }

        await signIn(listed.Provider);
    }

    /// <summary>
    /// The operation "Fetch pull request #n" (10.5; GitLab's "Fetch merge request !n", 12.5): the
    /// request's head ref (<see cref="PullRequestRefs.HeadOf"/>) of
    /// <paramref name="remote"/> into <c>FETCH_HEAD</c> only, then <paramref name="branch"/> without
    /// upstream at its SHA; it fails with <see cref="OperationTexts.FetchHeadChanged"/> and creates no
    /// branch when another fetch rewrote <c>FETCH_HEAD</c>. Its undo deletes the branch.
    /// </summary>
    private async Task<OperationResult?> FetchPullRequestAsync(PullRequest pullRequest, string remote, string branch)
    {
        var provider = pullRequest.BaseRepository.Provider;
        var text = OperationTexts.FetchRequest(provider, pullRequest.Number, remote);
        var sourceRef = PullRequestRefs.HeadOf(provider, pullRequest.Number)
            ?? throw new InvalidOperationException("A request without a head ref is never fetched (D361).");
        OperationResult? result = null;
        await GuardAsync(text, async () => result = await RunAsync(
            text,
            OperationKind.Network,
            RefreshScope.Refs,
            async context =>
            {
                var options = NetworkOptions.From(context);
                if (await Git.FetchToFetchHeadAsync(remote, sourceRef, options, context.CancellationToken) is not { } sha)
                {
                    return Failed(text, StoppedFailure(OperationTexts.FetchHeadChanged));
                }

                await Git.CreateBranchAsync(branch, sha, checkout: false, noTrack: true, keepIgnored: KeepIgnored, options, context.CancellationToken);
                return OperationReport.Done(OperationTexts.FetchedRequest(provider, pullRequest.Number, branch));
            },
            NetworkRun(UndoScope.CreateBranch(branch, Refs?.Head.BranchName), "fetch-pull-request:" + sourceRef)));
        return result;
    }
}
