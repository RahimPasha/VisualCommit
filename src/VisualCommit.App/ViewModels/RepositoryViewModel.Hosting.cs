// The repository's hosting (docs/phase-7a-design.md 1.7, 7.10): its pull requests, the bindings of
// git's credentials to the account and its authors' avatars, from the session's hosting. A dashboard
// row's view model in summary mode holds none (NullRepositoryHosting) until a tab adopts it.

using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.Core.Git;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels;

public sealed partial class RepositoryViewModel
{
    /// <summary>
    /// The repository's hosting: the session's <see cref="IHostingSession.ForRepository"/> of its
    /// working tree; <see cref="NullRepositoryHosting"/> in summary mode and without services.
    /// </summary>
    public IRepositoryHosting Hosting { get; private set; } = NullRepositoryHosting.Instance;

    /// <summary>The hosting's state as last applied: the "Pull requests" section and the branches' pull requests.</summary>
    [ObservableProperty]
    public partial RepositoryHostingState HostingState { get; private set; } = RepositoryHostingState.NotGitHub;

    /// <summary>The authors' avatars as last applied (D252).</summary>
    [ObservableProperty]
    public partial AuthorAvatars Avatars { get; private set; } = AuthorAvatars.None;

    /// <summary>No URL-rewrites read the hosting started is still running (<see cref="IsWatcherQuiet"/>).</summary>
    private bool IsHostingQuiet => true;

    /// <summary>The constructor's set-up, after <see cref="IsSummary"/> is set: no hosting for a dashboard row.</summary>
    private void SetUpHosting()
    {
        Hosting = IsSummary || _services is null ? NullRepositoryHosting.Instance : _services.Hosting.ForRepository(Path);
        Hosting.Changed += OnHostingChanged;
        HostingState = Hosting.State;
        Avatars = Hosting.Avatars;
    }

    /// <summary>After each refs read, on the UI thread (<see cref="ApplyRefs"/>).</summary>
    private void ApplyHosting(RepoRefs refs) => Hosting.Update(refs, UrlRewrites.None);

    /// <summary>A tab adopts this summary-mode view model (<see cref="Promote"/>).</summary>
    private void PromoteHosting()
    {
    }

    /// <summary>The hosting's state, bindings or avatars changed: raised through the post delegate, on the UI thread.</summary>
    private void OnHostingChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        HostingState = Hosting.State;
        Avatars = Hosting.Avatars;

        // Summary mode shows no left panel (3.1).
        if (!IsSummary)
        {
            LeftPanel.UpdatePullRequests(HostingState);
        }
    }

    private void DisposeHosting()
    {
        Hosting.Changed -= OnHostingChanged;
        Hosting.Dispose();
    }
}
