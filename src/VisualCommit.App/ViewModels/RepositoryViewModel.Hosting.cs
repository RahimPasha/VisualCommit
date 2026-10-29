// The repository's hosting (docs/phase-7a-design.md 1.7, 7.10): its pull requests, the bindings of
// git's credentials to the account and its authors' avatars, from the session's hosting. While the
// account is signed in, the tab reads the user's URL rewrites after each refs read (one
// `git config --null --get-regexp ^url\.`, D281, D290) and gives them to the hosting; a succeeded
// network operation of its queue refreshes the pull requests (D289). A dashboard row's view model
// in summary mode holds none (NullRepositoryHosting) until a tab adopts it.

using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels;

public sealed partial class RepositoryViewModel
{
    // On the UI thread: the rewrites last read and given to the hosting, whether a read has
    // answered since the account began to want them, and the read's generation (a newer refs
    // read's start drops an older read's answer).
    private UrlRewrites _rewrites = UrlRewrites.None;
    private bool _rulesRead;
    private int _rulesGeneration;

    // Set on the UI thread at every start of a read, read from any thread (IsWatcherQuiet).
    private Task? _rulesReading;

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

    /// <summary>
    /// No URL-rewrites read the hosting started is still running (<see cref="IsWatcherQuiet"/>,
    /// D281): the runner records the call before the read's task completes, so a quiet mark never
    /// splits a read from its count. No refresh awaits the read.
    /// </summary>
    private bool IsHostingQuiet => Volatile.Read(ref _rulesReading) is not { IsCompleted: false };

    /// <summary>The constructor's set-up, after <see cref="IsSummary"/> is set: no hosting for a dashboard row.</summary>
    private void SetUpHosting()
    {
        Hosting = IsSummary || _services is null ? NullRepositoryHosting.Instance : _services.Hosting.ForRepository(Path);
        SubscribeHosting();
    }

    /// <summary>Listens to <see cref="Hosting"/> and, for a real one, to the queue's ends; takes its state.</summary>
    private void SubscribeHosting()
    {
        Hosting.Changed += OnHostingChanged;

        // A summary row's network operations read the rules themselves (4.7, D332) and refresh nothing.
        if (Hosting is not NullRepositoryHosting)
        {
            _queue.Finished += OnQueueFinished;
        }

        HostingState = Hosting.State;
        Avatars = Hosting.Avatars;
    }

    /// <summary>
    /// After each refs read, on the UI thread (<see cref="ApplyRefs"/>, 7.10): signed out, the
    /// hosting gets the refs without rewrites; signed in, the rewrites read last at once and a new
    /// read, or before the first read only the read (D290: no bindings from unrewritten URLs).
    /// </summary>
    private void ApplyHosting(RepoRefs refs)
    {
        if (!Hosting.WantsUrlRewrites)
        {
            _rulesRead = false;
            _rewrites = UrlRewrites.None;
            Hosting.Update(refs, UrlRewrites.None);
            return;
        }

        if (_rulesRead)
        {
            Hosting.Update(refs, _rewrites);
        }

        ReadRules(refs);
    }

    // On the UI thread: one read of the user's URL rewrites for refs, off the UI thread.
    private void ReadRules(RepoRefs refs) => Volatile.Write(ref _rulesReading, ReadRulesAsync(refs, ++_rulesGeneration));

    private async Task ReadRulesAsync(RepoRefs refs, int generation)
    {
        UrlRewrites rules;
        try
        {
            // The continuation comes back to the UI thread, as the refs read's does.
            rules = await Repository.ReadUrlRewritesAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_disposed)
        {
            return;
        }
        catch (Exception ex)
        {
            if (!_disposed && generation == _rulesGeneration)
            {
                _log.Warning($"{Name}: the URL rewrites could not be read; the last ones read are kept.", ex);
            }

            return;
        }

        if (_disposed || generation != _rulesGeneration || !Hosting.WantsUrlRewrites)
        {
            return;
        }

        var first = !_rulesRead;
        _rulesRead = true;
        if (first || !rules.Rules.SequenceEqual(_rewrites.Rules))
        {
            _rewrites = rules;
            Hosting.Update(refs, rules);
        }
    }

    /// <summary>A tab adopts this summary-mode view model (<see cref="Promote"/>): a real hosting in place of none.</summary>
    private void PromoteHosting()
    {
        if (_services is null || Hosting is not NullRepositoryHosting)
        {
            return;
        }

        Hosting.Changed -= OnHostingChanged;
        Hosting = _services.Hosting.ForRepository(Path);
        SubscribeHosting();
        OnPropertyChanged(nameof(Hosting));
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

        // Signed out the rewrites are forgotten; a sign-in while the tab is open starts their read.
        if (!Hosting.WantsUrlRewrites)
        {
            _rulesRead = false;
            _rewrites = UrlRewrites.None;
        }
        else if (!_rulesRead && IsHostingQuiet && Refs is { } refs)
        {
            ReadRules(refs);
        }
    }

    /// <summary>An operation of the queue ended, off the UI thread: a succeeded network one refreshes the pull requests (D289).</summary>
    private void OnQueueFinished(object? sender, OperationFinishedEventArgs e)
    {
        if (e.Operation.Kind != OperationKind.Network || e.Result.Outcome != OperationOutcome.Succeeded)
        {
            return;
        }

        OnUiThread(() =>
        {
            if (!_disposed)
            {
                _ = Hosting.RefreshPullRequestsAsync(CancellationToken.None);
            }
        });
    }

    private void DisposeHosting()
    {
        Hosting.Changed -= OnHostingChanged;
        _queue.Finished -= OnQueueFinished;
        Hosting.Dispose();
    }
}
