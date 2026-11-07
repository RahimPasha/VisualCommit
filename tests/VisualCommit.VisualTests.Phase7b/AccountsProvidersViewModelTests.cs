using VisualCommit.App.Tests;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// The Accounts section for three providers as view models (docs/phase-7b-design.md 10.3, 10.8):
/// the blocks' order, which blocks show (all off too), 10.2's texts per state, Azure DevOps' name
/// line, one provider's event leaving another's toggled switch, an untoggled switch following its
/// account's changes, Save's switches one per block, one block's sign-out disabling every block's
/// buttons, also in a screen reopened while it runs, a block that stops showing busy already showing
/// the sign-out's end, each command's provider, the focus by <c>IdOf</c>, an old git and the
/// unsubscription.
/// The session is a stub; nothing here reaches a fake or the network. No dispatcher runs here, so a
/// block's continuation after a sign-out runs on the thread pool, beside the test: a test reads the
/// block's state at the sign-out's end in <c>IsBusy</c>'s own change, on that continuation's thread,
/// never by polling it from the test's.
/// </summary>
public sealed class AccountsProvidersViewModelTests
{
    private static readonly AccountKey Octo = new(HostingProvider.GitHub, "127.0.0.1:47399", "octo");
    private static readonly AccountKey Ada = new(HostingProvider.GitLab, "127.0.0.1:47398", "ada");
    private static readonly AccountKey Fernhill = new(HostingProvider.AzureDevOps, "127.0.0.1:47397", "Fernhill-Labs");

    [Fact]
    public void The_blocks_are_in_D353_s_order_with_their_labels_and_ids()
    {
        using var model = Model(new ProvidersSession());

        Assert.Equal([HostingProvider.GitHub, HostingProvider.GitLab, HostingProvider.AzureDevOps], model.Blocks.Select(block => block.Provider));
        Assert.Same(model.Blocks[0], model.GitHub);
        Assert.Same(model.Blocks[1], model.GitLab);
        Assert.Same(model.Blocks[2], model.AzureDevOps);
        Assert.Equal(["GitHub", "GitLab", "Azure DevOps"], model.Blocks.Select(block => block.Label));
        Assert.Equal(["", "GitLab", "AzureDevOps"], model.Blocks.Select(block => block.IdSuffix));
        Assert.Equal(["AccountLogin", "AccountLoginGitLab", "AccountLoginAzureDevOps"], model.Blocks.Select(block => block.IdOf("AccountLogin")));
    }

    [Fact]
    public void A_block_shows_while_its_provider_is_on_and_GitHub_s_while_all_are_off()
    {
        using (var three = Model(new ProvidersSession()))
        {
            Assert.All(three.Blocks, block => Assert.True(block.IsShown));
            Assert.All(three.Blocks, block => Assert.True(block.IsSignedOut && block.ShowsSignIn && block.ShowsButtons && !block.IsOff));
        }

        using (var noGitLab = Model(new ProvidersSession { On = [HostingProvider.GitHub, HostingProvider.AzureDevOps] }))
        {
            Assert.Equal([true, false, true], noGitLab.Blocks.Select(block => block.IsShown));
            Assert.True(noGitLab.GitLab.IsOff);
        }

        using (var azureAlone = Model(new ProvidersSession { On = [HostingProvider.AzureDevOps] }))
        {
            Assert.Equal([false, false, true], azureAlone.Blocks.Select(block => block.IsShown));
        }

        foreach (var off in new[] { Model(new ProvidersSession { On = [] }), Model(null) })
        {
            using (off)
            {
                Assert.Equal([true, false, false], off.Blocks.Select(block => block.IsShown));
                Assert.True(off.GitHub.IsOff);
                Assert.False(off.GitHub.ShowsButtons);
                Assert.False(off.GitHub.IsSignedOut);
            }
        }
    }

    [Fact]
    public void Each_block_s_texts_follow_its_provider_and_state()
    {
        var session = new ProvidersSession();
        session.Accounts[HostingProvider.GitHub] = Status(Octo, AccountState.SignedIn, TokenPlace.Keychain);
        session.Accounts[HostingProvider.GitLab] = Status(Ada, AccountState.NeedsSignIn, null, AccountNoteKind.Refused);
        session.Accounts[HostingProvider.AzureDevOps] = Status(Fernhill, AccountState.SignedIn, TokenPlace.Session, AccountNoteKind.NoKeychain);
        using var model = Model(session);

        Assert.Equal(PartA.GitHubUseForGitNote, model.GitHub.UseForGitNote);
        Assert.Equal(PartA.GitLabUseForGitNote, model.GitLab.UseForGitNote);
        Assert.Equal(PartA.AzureDevOpsUseForGitNote, model.AzureDevOps.UseForGitNote);
        Assert.Equal(PartA.InKeychain, model.GitHub.TokenPlaceText);
        Assert.Equal(PartA.GitLabRefusedNote, model.GitLab.NeedsSignInText);
        Assert.Null(model.GitLab.TokenPlaceText);
        Assert.True(model.GitLab.ShowsSignIn && model.GitLab.ShowsSignOut);
        Assert.Equal(PartA.NoKeychain, model.AzureDevOps.TokenPlaceText);
        Assert.Equal(["octo", "ada", "Fernhill-Labs"], model.Blocks.Select(block => block.Login));

        session.Change(HostingProvider.AzureDevOps, Status(Fernhill, AccountState.NeedsSignIn, null, AccountNoteKind.Refused));
        Assert.Equal(PartA.AzureDevOpsRefusedNote, model.AzureDevOps.NeedsSignInText);
        session.Change(HostingProvider.GitHub, Status(Octo, AccountState.NeedsSignIn, null, AccountNoteKind.Refused));
        Assert.Equal("GitHub refused the token. Sign in again with a new token.", model.GitHub.NeedsSignInText);
        session.Change(HostingProvider.GitLab, Status(Ada, AccountState.NeedsSignIn, null, AccountNoteKind.SessionEnded));
        Assert.Equal(PartA.SessionEnded, model.GitLab.NeedsSignInText);
    }

    [Fact]
    public void Azure_DevOps_alone_shows_the_account_s_name_under_the_organization()
    {
        var session = new ProvidersSession();
        session.Accounts[HostingProvider.GitHub] = Status(Octo, AccountState.SignedIn, TokenPlace.Keychain);
        session.Accounts[HostingProvider.GitLab] = Status(Ada, AccountState.SignedIn, TokenPlace.Keychain);
        session.Accounts[HostingProvider.AzureDevOps] = Status(Fernhill, AccountState.SignedIn, TokenPlace.Keychain);
        using var model = Model(session);

        Assert.Equal([null, null, AdaName], model.Blocks.Select(block => block.UserName));
        Assert.Equal([false, false, true], model.Blocks.Select(block => block.ShowsUserName));

        session.Change(HostingProvider.AzureDevOps, Status(Fernhill, AccountState.SignedIn, TokenPlace.Keychain, name: null));
        Assert.Null(model.AzureDevOps.UserName);
        Assert.False(model.AzureDevOps.ShowsUserName);
        session.Change(HostingProvider.AzureDevOps, null);
        Assert.Null(model.AzureDevOps.UserName);
    }

    [Fact]
    public void One_provider_s_event_leaves_another_s_toggled_switch()
    {
        var session = new ProvidersSession();
        session.Accounts[HostingProvider.GitLab] = Status(Ada, AccountState.SignedIn, TokenPlace.Keychain);
        session.Accounts[HostingProvider.AzureDevOps] = Status(Fernhill, AccountState.SignedIn, TokenPlace.Keychain);
        using var model = Model(session);
        model.GitLab.UseForGit = false;
        Assert.True(model.IsChanged);

        session.Change(HostingProvider.AzureDevOps, Status(Fernhill, AccountState.NeedsSignIn, null, AccountNoteKind.Refused));
        session.Change(HostingProvider.GitHub, Status(Octo, AccountState.SignedIn, TokenPlace.Keychain));

        Assert.False(model.GitLab.UseForGit);
        Assert.True(model.GitLab.IsChanged);
        Assert.True(model.IsChanged);
        Assert.Equal("octo", model.GitHub.Login);

        // Another account of the same provider resets that block's switch alone.
        session.Change(HostingProvider.GitLab, Status(new AccountKey(HostingProvider.GitLab, "127.0.0.1:47398", "octo"), AccountState.SignedIn, TokenPlace.Keychain));
        Assert.True(model.GitLab.UseForGit);
        Assert.False(model.IsChanged);
    }

    [Fact]
    public void An_untoggled_switch_follows_its_account_s_changes_and_a_toggled_one_keeps_its_toggle()
    {
        var session = new ProvidersSession();
        session.Accounts[HostingProvider.GitLab] = Status(Ada, AccountState.SignedIn, TokenPlace.Keychain);
        session.Accounts[HostingProvider.AzureDevOps] = Status(Fernhill, AccountState.SignedIn, TokenPlace.Keychain);
        using var model = Model(session);
        model.AzureDevOps.UseForGit = false;

        // A Save of an earlier screen that lands after this one opened: the same account, its switch off.
        session.Change(HostingProvider.GitLab, Status(Ada, AccountState.SignedIn, TokenPlace.Keychain, useForGit: false));
        Assert.False(model.GitLab.UseForGit);
        Assert.False(model.GitLab.IsChanged);

        // The same account's change (a refusal) keeps the user's toggle.
        session.Change(HostingProvider.AzureDevOps, Status(Fernhill, AccountState.NeedsSignIn, null, AccountNoteKind.Refused));
        Assert.False(model.AzureDevOps.UseForGit);
        Assert.True(model.AzureDevOps.IsChanged);

        // A Save that touched nothing else writes the toggled switch alone, never GitLab's old value.
        model.ApplySwitches();
        Assert.Equal([(HostingProvider.AzureDevOps, false)], session.UseForGitCalls);
    }

    [Fact]
    public async Task One_block_s_sign_out_disables_every_block_s_buttons_until_it_ends()
    {
        var session = new ProvidersSession { SignOutHold = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        session.Accounts[HostingProvider.GitHub] = Status(Octo, AccountState.SignedIn, TokenPlace.Keychain);
        session.Accounts[HostingProvider.GitLab] = Status(Ada, AccountState.SignedIn, TokenPlace.Keychain);
        using var model = Model(session);
        var asked = 0;
        foreach (var block in model.Blocks.Where(block => block.Provider != HostingProvider.GitHub))
        {
            block.SignInCommand.CanExecuteChanged += (_, _) => asked++;
            block.SignOutCommand.CanExecuteChanged += (_, _) => asked++;
        }

        var signOut = model.GitHub.SignOutCommand.ExecuteAsync(null);
        Assert.True(model.GitHub.IsBusy);
        Assert.Equal([HostingProvider.GitHub], session.SignOuts);
        Assert.True(asked >= 4, "The other blocks' buttons were not asked again when GitHub's sign-out started.");
        foreach (var block in model.Blocks)
        {
            Assert.False(block.SignInCommand.CanExecute(null), $"{block.Label}'s Sign in… can run while GitHub's sign-out runs.");
            Assert.False(block.SignOutCommand.CanExecute(null), $"{block.Label}'s Sign out… can run while GitHub's sign-out runs.");
        }

        asked = 0;
        session.SignOutHold.SetResult(null);
        await signOut;

        Assert.False(model.GitHub.IsBusy);
        Assert.True(asked >= 4, "The other blocks' buttons were not asked again when GitHub's sign-out ended.");
        foreach (var block in model.Blocks)
        {
            Assert.True(block.SignInCommand.CanExecute(null), $"{block.Label}'s Sign in… cannot run after GitHub's sign-out.");
            Assert.True(block.SignOutCommand.CanExecute(null), $"{block.Label}'s Sign out… cannot run after GitHub's sign-out.");
        }
    }

    /// <summary>
    /// The sign-out's end in the block that started it (docs/phase-7b-design.md 10.9's as built,
    /// 7a's 8.3): when the block stops showing busy, in <c>IsBusy</c>'s change, on which every block's
    /// buttons are asked again, it already shows the account's state and the keychain's note; the
    /// focus moves to its "Sign in…" after that, enabled.
    /// </summary>
    [Fact]
    public async Task The_block_that_signs_out_shows_the_account_and_the_note_when_it_stops_showing_busy()
    {
        const string Reason = "The keychain is locked.";
        var session = new ProvidersSession { SignOutHold = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        session.Accounts[HostingProvider.GitLab] = Status(Ada, AccountState.SignedIn, TokenPlace.Keychain);
        using var model = Model(session);
        var atTheEnd = new List<(bool SignedOut, bool ShowsSignIn, string? Note)>();
        model.GitLab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountBlockViewModel.IsBusy) && !model.GitLab.IsBusy)
            {
                atTheEnd.Add((model.GitLab.IsSignedOut, model.GitLab.ShowsSignIn, model.GitLab.SignOutNote));
            }
        };
        var focus = new List<(string Id, bool Busy, bool CanSignIn)>();
        model.FocusRequested += (_, id) => focus.Add((id, model.GitLab.IsBusy, model.GitLab.SignInCommand.CanExecute(null)));

        var signOut = model.GitLab.SignOutCommand.ExecuteAsync(null);
        Assert.True(model.GitLab.IsBusy);
        session.SignOutHold.SetResult(Reason);
        await signOut;

        var end = Assert.Single(atTheEnd);
        Assert.True(end.SignedOut, "GitLab's block stopped showing busy before it showed the account's state.");
        Assert.True(end.ShowsSignIn, "GitLab's block stopped showing busy before it offered Sign in….");
        Assert.Equal(VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts.AccountsSignOutNote(Reason), end.Note);
        Assert.Equal([("AccountsSignInButtonGitLab", false, true)], focus);
    }

    /// <summary>
    /// A screen reopened while a sign-out runs (Hunt 3; a sign-out goes on when its screen closes,
    /// D256): the block of the sign-out's provider shows "Signing out…" and every block's buttons are
    /// disabled until it ends, so no second "Sign out…" is offered; then it shows the account's state
    /// and the keychain's note as the screen that started it would, already when it stops showing busy
    /// (read in <c>IsBusy</c>'s change: CI 42339567989 polled <c>IsBusy</c> from the test's thread and
    /// read the note before the block's continuation had set it). A screen opened after the end shows
    /// nothing busy. One sign-out reaches the session.
    /// </summary>
    [Fact]
    public async Task A_screen_reopened_while_a_sign_out_runs_shows_the_block_busy_until_it_ends()
    {
        const string Reason = "The keychain is locked.";
        var session = new ProvidersSession { SignOutHold = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        session.Accounts[HostingProvider.GitLab] = Status(Ada, AccountState.SignedIn, TokenPlace.Keychain);
        session.Accounts[HostingProvider.AzureDevOps] = Status(Fernhill, AccountState.SignedIn, TokenPlace.Keychain);
        var first = Model(session);
        var signOut = first.GitLab.SignOutCommand.ExecuteAsync(null);
        Assert.True(first.GitLab.IsBusy);
        first.Dispose();

        using var reopened = Model(session);
        Assert.True(reopened.GitLab.IsBusy, "The reopened screen's GitLab block is not busy while its sign-out runs.");
        Assert.Equal(PartA.Busy, reopened.GitLab.BusyText);
        Assert.True(reopened.GitLab.ShowsSignOut, "The account left the block before its sign-out ended.");
        Assert.False(reopened.AzureDevOps.IsBusy);
        foreach (var block in reopened.Blocks)
        {
            Assert.False(block.SignInCommand.CanExecute(null), $"{block.Label}'s Sign in… can run while GitLab's sign-out runs.");
            Assert.False(block.SignOutCommand.CanExecute(null), $"{block.Label}'s Sign out… can run while GitLab's sign-out runs.");
        }

        var ended = new TaskCompletionSource<(bool SignedOut, bool ShowsSignIn, string? Note)>(TaskCreationOptions.RunContinuationsAsynchronously);
        reopened.GitLab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountBlockViewModel.IsBusy) && !reopened.GitLab.IsBusy)
            {
                ended.TrySetResult((reopened.GitLab.IsSignedOut, reopened.GitLab.ShowsSignIn, reopened.GitLab.SignOutNote));
            }
        };

        session.SignOutHold.SetResult(Reason);
        await signOut;
        var end = await ended.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(end.SignedOut, "The reopened screen's GitLab block stopped showing busy before it showed the account's state.");
        Assert.True(end.ShowsSignIn, "The reopened screen's GitLab block stopped showing busy before it offered Sign in….");
        Assert.Equal(VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts.AccountsSignOutNote(Reason), end.Note);
        Assert.False(reopened.GitLab.IsBusy, "The reopened screen's GitLab block is still busy after its sign-out ended.");
        Assert.True(reopened.GitLab.IsSignedOut);
        Assert.Equal(VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts.AccountsSignOutNote(Reason), reopened.GitLab.SignOutNote);
        foreach (var block in reopened.Blocks)
        {
            Assert.True(block.SignInCommand.CanExecute(null), $"{block.Label}'s Sign in… cannot run after GitLab's sign-out.");
            Assert.True(block.SignOutCommand.CanExecute(null), $"{block.Label}'s Sign out… cannot run after GitLab's sign-out.");
        }

        Assert.Equal([HostingProvider.GitLab], session.SignOuts);
        using var later = Model(session);
        Assert.False(later.GitLab.IsBusy);
        Assert.Null(later.GitLab.SignOutNote);
        Assert.True(later.GitLab.IsSignedOut);
    }

    [Fact]
    public void Save_applies_each_changed_switch_once_with_its_provider()
    {
        var session = new ProvidersSession();
        session.Accounts[HostingProvider.GitHub] = Status(Octo, AccountState.SignedIn, TokenPlace.Keychain);
        session.Accounts[HostingProvider.GitLab] = Status(Ada, AccountState.SignedIn, TokenPlace.Keychain);
        session.Accounts[HostingProvider.AzureDevOps] = Status(Fernhill, AccountState.SignedIn, TokenPlace.Keychain, useForGit: false);
        using var model = Model(session);

        model.ApplySwitches();
        Assert.Empty(session.UseForGitCalls);

        model.GitLab.UseForGit = false;
        model.AzureDevOps.UseForGit = true;
        model.ApplySwitches();
        Assert.Equal([(HostingProvider.GitLab, false), (HostingProvider.AzureDevOps, true)], session.UseForGitCalls);
    }

    [Theory]
    [InlineData(HostingProvider.GitHub, "AccountsSignOutButton", "AccountsSignInButton")]
    [InlineData(HostingProvider.GitLab, "AccountsSignOutButtonGitLab", "AccountsSignInButtonGitLab")]
    [InlineData(HostingProvider.AzureDevOps, "AccountsSignOutButtonAzureDevOps", "AccountsSignInButtonAzureDevOps")]
    public async Task Each_command_acts_on_its_provider_and_focuses_by_its_ids(HostingProvider provider, string signOutId, string signInId)
    {
        var session = new ProvidersSession();
        var asked = new List<HostingProvider>();
        var dialogs = new RecordingDialogs();
        using var model = new AccountsSettingsViewModel(
            session,
            p =>
            {
                asked.Add(p);
                session.Accounts[p] = Status(KeyOf(p), AccountState.SignedIn, TokenPlace.Keychain);
                return Task.FromResult(true);
            },
            dialogs,
            new RecordingLog());
        var focus = new List<string>();
        model.FocusRequested += (_, id) => focus.Add(id);
        var block = model.BlockOf(provider);

        await block.SignInCommand.ExecuteAsync(null);
        Assert.Equal([provider], asked);
        Assert.Equal([signOutId], focus);
        Assert.True(block.ShowsSignOut);
        Assert.All(model.Blocks.Where(other => other.Provider != provider), other => Assert.True(other.IsSignedOut));

        await block.SignOutCommand.ExecuteAsync(null);
        Assert.Equal([provider], session.SignOuts);
        Assert.Equal([signOutId, signInId], focus);
        var request = Assert.Single(dialogs.Requests);
        Assert.False(request.IsDestructive);
        Assert.Equal(provider switch
        {
            HostingProvider.GitHub => "Sign out of GitHub?",
            HostingProvider.GitLab => PartA.GitLabSignOutTitle,
            _ => PartA.AzureDevOpsSignOutTitle,
        }, request.Title);
        if (provider == HostingProvider.GitLab)
        {
            Assert.Equal(PartA.GitLabSignOutQuestionAda, request.Question);
        }
        else if (provider == HostingProvider.AzureDevOps)
        {
            Assert.Equal(PartA.AzureDevOpsSignOutQuestion, request.Question);
        }

        Assert.True(block.IsSignedOut);
    }

    [Fact]
    public async Task An_old_git_takes_7a_s_older_note_and_question_for_every_provider()
    {
        var session = new ProvidersSession();
        session.Accounts[HostingProvider.GitLab] = Status(Ada, AccountState.SignedIn, TokenPlace.Keychain);
        session.Accounts[HostingProvider.AzureDevOps] = Status(Fernhill, AccountState.SignedIn, TokenPlace.Keychain);
        var dialogs = new RecordingDialogs { Answer = false };
        using var model = new AccountsSettingsViewModel(session, null, dialogs, new RecordingLog(), new GitVersion(2, 30, 0));

        Assert.Equal(
            "This needs Git 2.31 or newer. With Git 2.30.0, clone, fetch, pull and push still use your git credential helpers.",
            model.GitLab.UseForGitNote);
        Assert.Equal(model.GitLab.UseForGitNote, model.AzureDevOps.UseForGitNote);
        await model.GitLab.SignOutCommand.ExecuteAsync(null);
        await model.AzureDevOps.SignOutCommand.ExecuteAsync(null);
        Assert.Equal(
            [
                "Sign out of GitLab as ada? VisualCommit removes its token from this computer and no longer lists merge requests.",
                "Sign out of Azure DevOps for Fernhill-Labs? VisualCommit removes its token from this computer and no longer lists pull requests.",
            ],
            dialogs.Requests.Select(request => request.Question));
        Assert.Empty(session.SignOuts);
    }

    [Fact]
    public void Dispose_unsubscribes_once_for_the_three_blocks()
    {
        var session = new ProvidersSession();
        var model = Model(session);
        Assert.Equal(1, session.Subscribers);

        model.Dispose();
        model.Dispose();

        Assert.Equal(0, session.Subscribers);
        session.Change(HostingProvider.GitLab, Status(Ada, AccountState.SignedIn, TokenPlace.Keychain));
        Assert.True(model.GitLab.IsSignedOut);
    }

    private static AccountsSettingsViewModel Model(IHostingSession? session) =>
        new(session, _ => Task.FromResult(false), new RecordingDialogs(), new RecordingLog());

    private static AccountKey KeyOf(HostingProvider provider) => provider switch
    {
        HostingProvider.GitHub => Octo,
        HostingProvider.GitLab => Ada,
        _ => Fernhill,
    };

    private static AccountStatus Status(AccountKey key, AccountState state, TokenPlace? place, AccountNoteKind kind = AccountNoteKind.None, bool useForGit = true, string? name = AdaName) =>
        new(new HostingAccount(key, key.Provider == HostingProvider.AzureDevOps ? PartA.Organization : key.Login, key.Provider == HostingProvider.AzureDevOps ? 0 : 1, name, null, useForGit), state, place, kind, null);

    /// <summary>A confirmation that answers <see cref="Answer"/> and keeps each request.</summary>
    private sealed class RecordingDialogs : IDialogService
    {
        public bool Answer { get; init; } = true;

        public List<ConfirmationRequest> Requests { get; } = [];

        public Task<bool> ConfirmAsync(ConfirmationRequest request)
        {
            Requests.Add(request);
            return Task.FromResult(Answer);
        }
    }

    /// <summary>A session of three providers whose accounts the test sets; it records sign-outs and switches by provider and counts subscribers.</summary>
    private sealed class ProvidersSession : IHostingSession
    {
        private EventHandler<HostingAccountChangedEventArgs>? _accountChanged;

        public IReadOnlyList<HostingProvider> On { get; init; } = HostingProviders.InAccountsOrder;

        public Dictionary<HostingProvider, AccountStatus?> Accounts { get; } = [];

        public List<HostingProvider> SignOuts { get; } = [];

        /// <summary>When set, each sign-out waits for it before it clears the account (a slow keychain).</summary>
        public TaskCompletionSource<string?>? SignOutHold { get; init; }

        public List<(HostingProvider, bool)> UseForGitCalls { get; } = [];

        public int Subscribers => _accountChanged?.GetInvocationList().Length ?? 0;

        public bool IsOff => On.Count == 0;

        public IReadOnlyList<HostingProvider> Providers => On;

        public bool BindsGit => false;

        public event EventHandler<HostingAccountChangedEventArgs>? AccountChanged
        {
            add => _accountChanged += value;
            remove => _accountChanged -= value;
        }

        public bool IsOn(HostingProvider provider) => On.Contains(provider);

        public IHostingProvider? ProviderOf(HostingProvider provider) => null;

        public AccountStatus? AccountOf(HostingProvider provider) => Accounts.GetValueOrDefault(provider);

        public void Change(HostingProvider provider, AccountStatus? account)
        {
            Accounts[provider] = account;
            _accountChanged?.Invoke(this, new HostingAccountChangedEventArgs(provider));
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<bool> KeychainAvailableAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<SignInResult> SignInAsync(SignInRequest request, CancellationToken ct) => throw new NotSupportedException();

        public async Task<string?> SignOutAsync(HostingProvider provider, CancellationToken ct)
        {
            SignOuts.Add(provider);
            var reason = SignOutHold is { } hold ? await hold.Task : null;
            Accounts[provider] = null;
            return reason;
        }

        public void SetUseForGit(HostingProvider provider, bool useForGit) => UseForGitCalls.Add((provider, useForGit));

        public HostedRepository? Recognise(string url) => null;

        public bool SourceCanExist(string remoteUrl, string remoteRef) => true;

        public IReadOnlyList<CredentialBinding> BindingsForClone(string url) => [];

        public IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites) => [];

        public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(HostingProvider provider, CancellationToken ct) => throw new NotSupportedException();

        public IRepositoryHosting ForRepository(string workingDirectory) => NullRepositoryHosting.Instance;

        public Task<GitCredential?> GetAsync(AccountKey account, CancellationToken cancellationToken) => Task.FromResult<GitCredential?>(null);

        public void Refused(AccountKey account)
        {
        }

        public void Dispose()
        {
        }
    }
}
