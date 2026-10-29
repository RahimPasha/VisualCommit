using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts;

namespace VisualCommit.App.ViewModels.Settings;

/// <summary>
/// The settings screen's Accounts section (docs/phase-7a-design.md 8.3; D256, D294): the GitHub
/// account, signed out, signed in or needing sign-in, with "Sign in…", "Sign out…" and the "Use for
/// git over HTTPS" switch. It reads the session's account when the screen opens and follows
/// <see cref="IHostingSession.AccountChanged"/>, which the session raises on the UI thread; it makes
/// no git call and no request when the screen opens. Sign in and sign out act at once (D256): the
/// screen's Cancel cannot undo them. The switch is a setting: the screen's Save applies it
/// (<see cref="IsChanged"/>, D216), and Cancel and Esc drop it.
/// </summary>
public sealed partial class AccountsSettingsViewModel : ObservableObject, IDisposable
{
    /// <summary>The first git that takes the account's credentials through <c>GIT_CONFIG_COUNT</c> (D277).</summary>
    private static readonly GitVersion GitThatUsesTheAccount = new(2, 31, 0);

    private readonly IHostingSession? _hosting;
    private readonly Func<Task<bool>>? _signIn;
    private readonly IDialogService _dialogs;
    private readonly IAppLog _log;
    private readonly GitVersion? _gitVersion;
    private AccountStatus? _account;
    private bool _disposed;

    /// <param name="hosting">The session's hosting; null without one (off).</param>
    /// <param name="signIn">The window's sign-in entry (8.5), which stacks the "Add GitHub account" dialog over the screen; null without one.</param>
    /// <param name="dialogs">Where the sign-out's confirmation stacks over the screen (D104).</param>
    /// <param name="log">The app's log.</param>
    /// <param name="gitVersion">The detected git's version, null when unknown (D277).</param>
    public AccountsSettingsViewModel(IHostingSession? hosting, Func<Task<bool>>? signIn, IDialogService dialogs, IAppLog log, GitVersion? gitVersion = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(log);
        _hosting = hosting;
        _signIn = signIn;
        _dialogs = dialogs;
        _log = log;
        _gitVersion = gitVersion;
        _account = hosting?.Account;
        UseForGit = _account?.Account.UseForGit ?? false;
        if (hosting is not null)
        {
            hosting.AccountChanged += OnAccountChanged;
        }
    }

    /// <summary>GitHub is off for this session (<c>AccountsOff</c>, no button).</summary>
    public bool IsOff => _hosting is null || _hosting.IsOff;

    /// <summary>No account (<c>AccountsSignedOut</c>).</summary>
    public bool IsSignedOut => !IsOff && _account is null;

    /// <summary>An account, signed in or needing sign-in (<c>AccountLogin</c>).</summary>
    public bool HasAccount => !IsOff && _account is not null;

    /// <summary>The account's login in its own case.</summary>
    public string? Login => HasAccount ? _account!.Account.Login : null;

    /// <summary>Where the signed-in account's token is (<c>AccountTokenPlace</c>); null otherwise.</summary>
    public string? TokenPlaceText => HasAccount && _account!.State == AccountState.SignedIn
        ? _account.NoteKind switch
        {
            AccountNoteKind.SaveFailed => Texts.SaveFailed(_account.Note ?? string.Empty),
            AccountNoteKind.NoKeychain => Texts.NoKeychain,
            _ => _account.Place == TokenPlace.Session ? Texts.NoKeychain : Texts.TokenInKeychain,
        }
        : null;

    /// <summary>Why the account needs signing in (<c>AccountNeedsSignIn</c>); null otherwise.</summary>
    public string? NeedsSignInText => HasAccount && _account!.State == AccountState.NeedsSignIn
        ? _account.NoteKind switch
        {
            AccountNoteKind.TokenMissing => Texts.NeedsSignInTokenMissing,
            AccountNoteKind.ReadFailed => Texts.NeedsSignInReadFailed(_account.Note ?? string.Empty),
            AccountNoteKind.SessionEnded => Texts.NeedsSignInSessionEnded,
            _ => Texts.NeedsSignInRefused,
        }
        : null;

    public bool ShowsTokenPlace => TokenPlaceText is not null;

    public bool ShowsNeedsSignIn => NeedsSignInText is not null;

    /// <summary>"Sign in…": signed out or needing sign-in, not off (D294).</summary>
    public bool ShowsSignIn => !IsOff && _account is not { State: AccountState.SignedIn };

    /// <summary>"Sign out…": with an account.</summary>
    public bool ShowsSignOut => HasAccount;

    /// <summary>The buttons' row: not off.</summary>
    public bool ShowsButtons => !IsOff;

    /// <summary>"Use for git over HTTPS" as the screen shows it: the account's until toggled; applied at Save (D216).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    public partial bool UseForGit { get; set; }

    /// <summary>The switch differs from the account's: Save applies it.</summary>
    public bool IsChanged => HasAccount && UseForGit != _account!.Account.UseForGit;

    /// <summary>The switch's note: the older git's when the detected git is known and older than 2.31 (D277).</summary>
    public string UseForGitNote => UsesTheAccount ? Texts.AccountUseForGitNote : Texts.AccountUseForGitOldGit(_gitVersion!.Value.ToString());

    /// <summary><c>Signing out…</c> while the sign-out runs; null otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand), nameof(SignOutCommand))]
    public partial string? BusyText { get; private set; }

    /// <summary>A sign-out runs: both buttons are disabled.</summary>
    public bool IsBusy => BusyText is not null;

    /// <summary>The sign-out's note when the keychain kept its entry (<c>AccountsSignOutNote</c>); cleared at the next sign-in, and with the screen.</summary>
    [ObservableProperty]
    public partial string? SignOutNote { get; private set; }

    /// <summary>Raised when a control should take the keyboard focus, by automation id; the screen forwards it.</summary>
    public event EventHandler<string>? FocusRequested;

    /// <summary>Whether git takes the account's credentials: 2.31 or newer, or the version unknown (D277).</summary>
    private bool UsesTheAccount => _gitVersion is not { } version || version >= GitThatUsesTheAccount;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_hosting is not null)
        {
            _hosting.AccountChanged -= OnAccountChanged;
        }
    }

    /// <summary>
    /// "Sign in…": the window's entry, whose dialog stacks over the screen (D104); afterwards, signed
    /// in, the focus goes to "Sign out…".
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync()
    {
        if (_signIn is null)
        {
            return;
        }

        bool signedIn;
        try
        {
            signedIn = await _signIn();
        }
        catch (Exception ex)
        {
            _log.Warning("Signing in to GitHub from the settings screen failed.", ex);
            signedIn = false;
        }

        Follow(_hosting?.Account);
        if (signedIn && !_disposed && _account is { State: AccountState.SignedIn })
        {
            FocusRequested?.Invoke(this, "AccountsSignOutButton");
        }
    }

    private bool CanSignIn() => !IsBusy;

    /// <summary>
    /// "Sign out…": the confirmation, stacked; declined, nothing. Accepted: <c>Signing out…</c> with
    /// both buttons disabled while the session signs out, at once (D256: the screen's Cancel cannot
    /// undo it); a reason the keychain kept its entry shows as the note; the focus goes to "Sign in…".
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSignOut))]
    private async Task SignOutAsync()
    {
        if (_hosting is null || _account is not { } account)
        {
            return;
        }

        var question = Texts.SignOutQuestion(account.Account.Login, UsesTheAccount);
        if (!await _dialogs.ConfirmAsync(new ConfirmationRequest(Texts.SignOutTitle, question, string.Empty, Texts.SignOutConfirm, IsDestructive: false)))
        {
            return;
        }

        BusyText = Texts.AccountsBusy;
        string? reason;
        try
        {
            reason = await _hosting.SignOutAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.Warning("Signing out of GitHub failed.", ex);
            reason = null;
        }
        finally
        {
            BusyText = null;
        }

        Follow(_hosting.Account);
        SignOutNote = reason is null ? null : Texts.AccountsSignOutNote(reason);
        if (!_disposed && ShowsSignIn)
        {
            FocusRequested?.Invoke(this, "AccountsSignInButton");
        }
    }

    private bool CanSignOut() => !IsBusy;

    private void OnAccountChanged(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            Follow(_hosting?.Account);
        }
    }

    /// <summary>Shows <paramref name="account"/>: the switch reset when it is another account, the sign-out's note cleared by a sign-in.</summary>
    private void Follow(AccountStatus? account)
    {
        var another = account?.Account.Key != _account?.Account.Key;
        _account = account;
        if (another)
        {
            UseForGit = account?.Account.UseForGit ?? false;
        }

        if (account is { State: AccountState.SignedIn })
        {
            SignOutNote = null;
        }

        OnPropertyChanged(nameof(IsSignedOut));
        OnPropertyChanged(nameof(HasAccount));
        OnPropertyChanged(nameof(Login));
        OnPropertyChanged(nameof(TokenPlaceText));
        OnPropertyChanged(nameof(NeedsSignInText));
        OnPropertyChanged(nameof(ShowsTokenPlace));
        OnPropertyChanged(nameof(ShowsNeedsSignIn));
        OnPropertyChanged(nameof(ShowsSignIn));
        OnPropertyChanged(nameof(ShowsSignOut));
        OnPropertyChanged(nameof(IsChanged));
    }
}
