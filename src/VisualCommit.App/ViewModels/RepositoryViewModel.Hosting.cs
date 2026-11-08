// The repository's hosting (docs/phase-7a-design.md 1.7, 7.10; docs/phase-7b-design.md 9.9): its pull
// requests, the bindings of git's credentials to the accounts and its authors' avatars, from the
// session's hosting. While any account is signed in, the tab reads the user's URL rewrites after each
// refs read (one
// `git config --null --get-regexp ^url\.`, D281, D290) and gives them to the hosting; a succeeded
// network operation of its queue refreshes the pull requests (D289). A dashboard row's view model
// in summary mode holds none (NullRepositoryHosting) until a tab adopts it. Until the first read
// answers after a sign-in, a network operation awaits the read in flight, and the auto-fetch's
// tick asks for one when none is (D388).

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
    public partial RepositoryHostingState HostingState { get; private set; } = RepositoryHostingState.NoHostedRemote;

    /// <summary>The authors' avatars as last applied (D252).</summary>
    [ObservableProperty]
    public partial AuthorAvatars Avatars { get; private set; } = AuthorAvatars.None;

    /// <summary>
    /// No URL-rewrites read the hosting started is still running (<see cref="IsWatcherQuiet"/>,
    /// D281): the runner records the call before the read's task completes, so a quiet mark never
    /// splits a read from its count. No refresh awaits the read.
    /// </summary>
    private bool IsHostingQuiet => Volatile.Read(ref _rulesReading) is not { IsCompleted: false };

    /// <summary>
    /// The tab is inside D290's window while an account binds git (D388): an account wants the URL
    /// rewrites and none of the tab's reads has answered since (a sign-in with none signed in, or the
    /// tab's first load signed in), so its hosting's bindings are empty. A network operation started
    /// now binds once the rules are known (<see cref="UrlRulesReadAsync"/>, or its own read), and the
    /// auto-fetch's tick starts no fetch (<see cref="RequestUrlRules"/>). On the UI thread. False signed
    /// out, in summary mode (a dashboard row reads the rules itself, D332), once read, closed, or with
    /// no account binding git.
    /// </summary>
    internal bool AwaitsUrlRules => !IsSummary && !_disposed && Hosting.WantsUrlRewrites && !_rulesRead && _services?.Hosting.BindsGit == true;

    /// <summary>
    /// For the auto-fetch's tick inside D290's window (<see cref="AwaitsUrlRules"/>, D388), on the UI
    /// thread: with no read in flight, which after a failed read stays so until the next refs read or
    /// hosting change, starts the tab's read of the URL rules (one
    /// <c>git config --null --get-regexp ^url\.</c>), whose answer ends the window, and returns true.
    /// False, starting nothing, outside the window, while a read is in flight, or before the first
    /// refs read.
    /// </summary>
    internal bool RequestUrlRules()
    {
        if (!AwaitsUrlRules || !IsHostingQuiet || Refs is not { } refs)
        {
            return false;
        }

        ReadRules(refs);
        return true;
    }

    /// <summary>
    /// Waits for the tab's URL-rewrites reads in flight (D388), on the UI thread, until one has given
    /// the hosting the rules: true then, and its <see cref="IRepositoryHosting.Bindings"/> hold them.
    /// False when the rules are still not known and no read is in flight (not started yet, or
    /// failed), when the account no longer wants them, or when <paramref name="cancellationToken"/>
    /// ends the wait. A read runs under the tab's lifetime, so the tab's closing ends it and the
    /// wait. Adds no git call.
    /// </summary>
    internal async Task<bool> UrlRulesReadAsync(CancellationToken cancellationToken)
    {
        // A newer refs read's start drops an older read's answer and starts its own (ReadRulesAsync).
        while (!_disposed && !_rulesRead && Hosting.WantsUrlRewrites && Volatile.Read(ref _rulesReading) is { IsCompleted: false } reading)
        {
            try
            {
                await reading.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        return !_disposed && _rulesRead && Hosting.WantsUrlRewrites;
    }

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
            // The listed repository's provider's account (docs/phase-7b-design.md 9.9), and its
            // login in the server's spelling and its state for D356's row (12.3, D366).
            var held = HostingState.Repository is { } listed ? _services?.Hosting.AccountOf(listed.Provider) : null;
            LeftPanel.UpdatePullRequests(HostingState, held?.Account.Key, held?.Account.Login, held?.State ?? AccountState.SignedIn);
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
