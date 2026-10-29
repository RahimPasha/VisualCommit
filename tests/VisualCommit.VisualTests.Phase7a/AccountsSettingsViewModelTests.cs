using VisualCommit.App.Tests;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Settings;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using Xunit;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// The settings screen's Accounts section as a view model (docs/phase-7a-design.md 8.3, 8.8): each
/// state and note kind to its texts and visibility, the switch and <c>IsChanged</c>, the sign-out's
/// confirmation and note, the older git's texts (D277), off, and <c>Dispose</c>. The session is a
/// stub that raises <c>AccountChanged</c> where the test says.
/// </summary>
public sealed class AccountsSettingsViewModelTests
{
    private static readonly AccountKey Octo = new(HostingProvider.GitHub, "github.com", "Octo");

    private static AccountStatus Status(AccountState state, TokenPlace? place, AccountNoteKind kind = AccountNoteKind.None, string? note = null, bool useForGit = true, AccountKey? key = null) =>
        new(new HostingAccount(key ?? Octo, (key ?? Octo) == Octo ? "Octo" : key!.Login, 1001, "Octo Cat", null, useForGit), state, place, kind, note);

    [Fact]
    public void Signed_out_shows_not_signed_in_and_sign_in_only()
    {
        using var model = new AccountsSettingsViewModel(new StubSession(), () => Task.FromResult(false), new StubDialogs(), new RecordingLog());

        Assert.False(model.IsOff);
        Assert.True(model.IsSignedOut);
        Assert.False(model.HasAccount);
        Assert.Null(model.Login);
        Assert.Null(model.TokenPlaceText);
        Assert.Null(model.NeedsSignInText);
        Assert.True(model.ShowsSignIn);
        Assert.False(model.ShowsSignOut);
        Assert.True(model.ShowsButtons);
        Assert.False(model.IsChanged);
        Assert.Null(model.BusyText);
        Assert.Null(model.SignOutNote);
    }

    [Fact]
    public void Off_shows_the_off_text_and_no_button()
    {
        foreach (var hosting in new IHostingSession?[] { null, NullHostingSession.Instance, new StubSession { Off = true } })
        {
            using var model = new AccountsSettingsViewModel(hosting, null, new StubDialogs(), new RecordingLog());
            Assert.True(model.IsOff);
            Assert.False(model.IsSignedOut);
            Assert.False(model.HasAccount);
            Assert.False(model.ShowsSignIn);
            Assert.False(model.ShowsSignOut);
            Assert.False(model.ShowsButtons);
            Assert.False(model.IsChanged);
        }

        Assert.Equal("GitHub is turned off for this session.", Texts.AccountsOff);
    }

    [Theory]
    [InlineData(TokenPlace.Keychain, AccountNoteKind.None, null, "The token is kept in this computer's keychain.")]
    [InlineData(TokenPlace.Session, AccountNoteKind.NoKeychain, null, "No keychain was found on this computer. The token is not saved: VisualCommit keeps it until it closes and asks for it again at the next start.")]
    [InlineData(TokenPlace.Session, AccountNoteKind.SaveFailed, "the keychain stayed locked", "The token could not be saved in this computer's keychain: the keychain stayed locked. VisualCommit keeps it until it closes and asks for it again at the next start.")]
    [InlineData(TokenPlace.Session, AccountNoteKind.SaveFailed, "the keychain stayed locked.", "The token could not be saved in this computer's keychain: the keychain stayed locked. VisualCommit keeps it until it closes and asks for it again at the next start.")]
    public void Signed_in_shows_the_login_where_the_token_is_and_sign_out_only(TokenPlace place, AccountNoteKind kind, string? note, string expected)
    {
        using var model = new AccountsSettingsViewModel(new StubSession { Account = Status(AccountState.SignedIn, place, kind, note) }, null, new StubDialogs(), new RecordingLog());

        Assert.True(model.HasAccount);
        Assert.False(model.IsSignedOut);
        Assert.Equal("Octo", model.Login);
        Assert.Equal(expected, model.TokenPlaceText);
        Assert.True(model.ShowsTokenPlace);
        Assert.Null(model.NeedsSignInText);
        Assert.False(model.ShowsNeedsSignIn);
        Assert.False(model.ShowsSignIn);
        Assert.True(model.ShowsSignOut);
        Assert.True(model.UseForGit);
    }

    [Theory]
    [InlineData(AccountNoteKind.Refused, null, "GitHub refused the token. Sign in again with a new token.")]
    [InlineData(AccountNoteKind.TokenMissing, null, "The token is no longer in this computer's keychain. Sign in again.")]
    [InlineData(AccountNoteKind.ReadFailed, "the keychain stayed locked", "The token could not be read from this computer's keychain: the keychain stayed locked. Sign in again.")]
    [InlineData(AccountNoteKind.SessionEnded, null, "The token was kept for the last session only. Sign in again.")]
    public void Needing_sign_in_shows_why_and_both_buttons(AccountNoteKind kind, string? note, string expected)
    {
        using var model = new AccountsSettingsViewModel(new StubSession { Account = Status(AccountState.NeedsSignIn, null, kind, note) }, null, new StubDialogs(), new RecordingLog());

        Assert.True(model.HasAccount);
        Assert.Equal("Octo", model.Login);
        Assert.Null(model.TokenPlaceText);
        Assert.Equal(expected, model.NeedsSignInText);
        Assert.True(model.ShowsNeedsSignIn);
        Assert.True(model.ShowsSignIn);
        Assert.True(model.ShowsSignOut);
    }

    [Fact]
    public void The_switch_is_changed_only_while_it_differs_from_the_account()
    {
        var session = new StubSession { Account = Status(AccountState.SignedIn, TokenPlace.Keychain) };
        using var model = new AccountsSettingsViewModel(session, null, new StubDialogs(), new RecordingLog());

        Assert.True(model.UseForGit);
        Assert.False(model.IsChanged);
        model.UseForGit = false;
        Assert.True(model.IsChanged);
        model.UseForGit = true;
        Assert.False(model.IsChanged);
        model.UseForGit = false;

        // The same account's change (a refusal) keeps the toggle; another account resets it.
        session.Change(Status(AccountState.NeedsSignIn, null, AccountNoteKind.Refused));
        Assert.False(model.UseForGit);
        Assert.True(model.IsChanged);
        session.Change(Status(AccountState.SignedIn, TokenPlace.Keychain, useForGit: false, key: new AccountKey(HostingProvider.GitHub, "github.com", "ada")));
        Assert.False(model.UseForGit);
        Assert.False(model.IsChanged);
        session.Change(null);
        Assert.False(model.IsChanged);
        Assert.Empty(session.UseForGitCalls);
    }

    [Theory]
    [InlineData("2.30.0", false)]
    [InlineData("2.31.0", true)]
    [InlineData(null, true)]
    public void The_switch_note_and_the_question_follow_the_detected_git(string? version, bool usesTheAccount)
    {
        GitVersion? git = version is null ? null : GitVersion.TryParse(version, out var parsed) ? parsed : throw new InvalidOperationException(version);
        var dialogs = new StubDialogs { Answer = false };
        using var model = new AccountsSettingsViewModel(new StubSession { Account = Status(AccountState.SignedIn, TokenPlace.Keychain) }, null, dialogs, new RecordingLog(), git);

        Assert.Equal(
            usesTheAccount
                ? "Clone, fetch, pull and push to GitHub over HTTPS use this account's token in place of your git credential helpers. Git in a terminal still uses your helpers."
                : "This needs Git 2.31 or newer. With Git 2.30.0, clone, fetch, pull and push still use your git credential helpers.",
            model.UseForGitNote);

        model.SignOutCommand.Execute(null);
        var request = Assert.Single(dialogs.Requests);
        Assert.Equal("Sign out of GitHub?", request.Title);
        Assert.Equal(
            "Sign out of GitHub as Octo? VisualCommit removes its token and the authors' pictures it saved from this computer, and no longer lists pull requests." +
            (usesTheAccount ? " Git over HTTPS asks your own credential helpers again." : string.Empty),
            request.Question);
        Assert.Equal(string.Empty, request.Detail);
        Assert.Equal("Sign out", request.ConfirmText);
        Assert.False(request.IsDestructive);
    }

    [Fact]
    public async Task A_declined_confirmation_changes_nothing()
    {
        var session = new StubSession { Account = Status(AccountState.SignedIn, TokenPlace.Keychain) };
        var dialogs = new StubDialogs { Answer = false };
        using var model = new AccountsSettingsViewModel(session, null, dialogs, new RecordingLog());

        await model.SignOutCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Requests);
        Assert.Equal(0, session.SignOuts);
        Assert.True(model.ShowsSignOut);
        Assert.Null(model.SignOutNote);
        Assert.Null(model.BusyText);
    }

    [Fact]
    public async Task An_accepted_sign_out_is_busy_until_the_session_answers_then_signed_out_with_the_kept_entry_s_note()
    {
        var session = new StubSession { Account = Status(AccountState.SignedIn, TokenPlace.Keychain) };
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.SignOut = () => answer.Task;
        using var model = new AccountsSettingsViewModel(session, null, new StubDialogs(), new RecordingLog());
        var focus = new List<string>();
        model.FocusRequested += (_, id) => focus.Add(id);

        var running = model.SignOutCommand.ExecuteAsync(null);
        Assert.Equal("Signing out…", model.BusyText);
        Assert.True(model.IsBusy);
        Assert.False(model.SignInCommand.CanExecute(null));
        Assert.False(model.SignOutCommand.CanExecute(null));

        session.Account = null;
        answer.SetResult("the keychain stayed locked");
        await running;

        Assert.Equal(1, session.SignOuts);
        Assert.Null(model.BusyText);
        Assert.True(model.IsSignedOut);
        Assert.True(model.ShowsSignIn);
        Assert.False(model.ShowsSignOut);
        Assert.Equal("Signed out, but the token could not be removed from this computer's keychain: the keychain stayed locked.", model.SignOutNote);
        Assert.Equal(["AccountsSignInButton"], focus);

        // The note clears at the next sign-in.
        session.Change(Status(AccountState.SignedIn, TokenPlace.Keychain));
        Assert.Null(model.SignOutNote);
    }

    [Fact]
    public async Task Sign_in_runs_the_window_s_entry_and_then_focuses_sign_out()
    {
        var session = new StubSession();
        var calls = 0;
        using var model = new AccountsSettingsViewModel(
            session,
            () =>
            {
                calls++;
                session.Account = Status(AccountState.SignedIn, TokenPlace.Keychain);
                return Task.FromResult(true);
            },
            new StubDialogs(),
            new RecordingLog());
        var focus = new List<string>();
        model.FocusRequested += (_, id) => focus.Add(id);

        await model.SignInCommand.ExecuteAsync(null);

        Assert.Equal(1, calls);
        Assert.Equal("Octo", model.Login);
        Assert.True(model.ShowsSignOut);
        Assert.False(model.ShowsSignIn);
        Assert.Equal(["AccountsSignOutButton"], focus);
    }

    [Fact]
    public void Dispose_unsubscribes()
    {
        var session = new StubSession();
        var model = new AccountsSettingsViewModel(session, null, new StubDialogs(), new RecordingLog());
        Assert.Equal(1, session.Subscribers);

        model.Dispose();
        model.Dispose();

        Assert.Equal(0, session.Subscribers);
        session.Change(Status(AccountState.SignedIn, TokenPlace.Keychain));
        Assert.True(model.IsSignedOut);
    }

    /// <summary>A confirmation that answers <see cref="Answer"/> and keeps each request.</summary>
    internal sealed class StubDialogs : IDialogService
    {
        public bool Answer { get; init; } = true;

        public List<ConfirmationRequest> Requests { get; } = [];

        public Task<bool> ConfirmAsync(ConfirmationRequest request)
        {
            Requests.Add(request);
            return Task.FromResult(Answer);
        }
    }

    /// <summary>A hosting session whose account the test sets; it counts sign-outs, switches and subscribers.</summary>
    internal sealed class StubSession : IHostingSession
    {
        private EventHandler? _accountChanged;

        public bool Off { get; init; }

        public AccountStatus? Account { get; set; }

        public Func<Task<string?>> SignOut { get; set; } = () => Task.FromResult<string?>(null);

        public int SignOuts { get; private set; }

        public List<bool> UseForGitCalls { get; } = [];

        public int Subscribers => _accountChanged?.GetInvocationList().Length ?? 0;

        public bool IsOff => Off;

        public IHostingProvider? GitHub => null;

        public bool BindsGit => false;

        public event EventHandler? AccountChanged
        {
            add => _accountChanged += value;
            remove => _accountChanged -= value;
        }

        /// <summary>Sets the account and raises <c>AccountChanged</c>, as the session's post does on the UI thread.</summary>
        public void Change(AccountStatus? account)
        {
            Account = account;
            _accountChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<bool> KeychainAvailableAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<SignInResult> SignInAsync(string token, CancellationToken ct) => throw new NotSupportedException();

        public Task<string?> SignOutAsync(CancellationToken ct)
        {
            SignOuts++;
            return SignOut();
        }

        public void SetUseForGit(bool useForGit) => UseForGitCalls.Add(useForGit);

        public HostedRepository? Recognise(string url) => null;

        public bool SourceCanExist(string remoteUrl, string remoteRef) => true;

        public IReadOnlyList<CredentialBinding> BindingsForClone(string url) => [];

        public IReadOnlyList<CredentialBinding> BindingsFor(IReadOnlyList<RemoteUrl> remoteUrls, UrlRewrites rewrites) => [];

        public Task<HostingResult<RepositoryList>> ListRepositoriesAsync(CancellationToken ct) => throw new NotSupportedException();

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
