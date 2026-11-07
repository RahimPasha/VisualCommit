using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts;

namespace VisualCommit.App.ViewModels.Settings;

/// <summary>
/// One provider's block of the Accounts section (docs/phase-7b-design.md 10.3; D353, D362): 7a's
/// GitHub account section (docs/phase-7a-design.md 8.3) over <see cref="IHostingSession.AccountOf"/>
/// of its <see cref="Provider"/>, signed out, signed in or needing sign-in, with "Sign in…", "Sign
/// out…" and the "Use for git over HTTPS" switch. The section (<see cref="AccountsSettingsViewModel"/>)
/// subscribes to <see cref="IHostingSession.AccountChanged"/> once and hands each event to its
/// provider's block alone (<see cref="Follow"/>), so another provider's event never resets a toggled
/// switch. No git call or request when the screen opens. Sign in and sign out act at once (D256);
/// the switch waits for Save (D216). A sign-out goes on when its screen closes, so a block made
/// while its provider's sign-out runs, in a screen reopened meanwhile, shows it busy until it ends
/// (<see cref="RunningSignOuts"/>; Hunt 3).
/// </summary>
public sealed partial class AccountBlockViewModel : ObservableObject
{
    /// <summary>The first git that takes the account's credentials through <c>GIT_CONFIG_COUNT</c> (D277).</summary>
    private static readonly GitVersion GitThatUsesTheAccount = new(2, 31, 0);

    /// <summary>
    /// The sign-outs that run, per session and provider, each ending with its reason (null, or why the
    /// keychain kept its entry). A sign-out outlives the screen that started it (D256); a block made
    /// while one runs for its provider shows <c>Signing out…</c>, its buttons and, through the section,
    /// every block's disabled, until it ends (Hunt 3: a reopened screen offered "Sign out…" again).
    /// Keyed weakly by the session, so a session that ends takes its entries with it.
    /// </summary>
    private static readonly ConditionalWeakTable<IHostingSession, Dictionary<HostingProvider, Task<string?>>> RunningSignOuts = new();

    private readonly IHostingSession? _hosting;
    private readonly Func<HostingProvider, Task<bool>>? _signIn;
    private readonly IDialogService _dialogs;
    private readonly IAppLog _log;
    private readonly GitVersion? _gitVersion;
    private AccountStatus? _account;
    private Func<bool>? _sectionBusy;
    private bool _disposed;

    /// <param name="hosting">The session's hosting; null without one (all off).</param>
    /// <param name="provider">The block's provider.</param>
    /// <param name="signIn">The window's sign-in entry (10.5), which stacks the provider's dialog over the screen; null without one.</param>
    /// <param name="dialogs">Where the sign-out's confirmation stacks over the screen (D104).</param>
    /// <param name="log">The app's log.</param>
    /// <param name="gitVersion">The detected git's version, null when unknown (D277).</param>
    public AccountBlockViewModel(IHostingSession? hosting, HostingProvider provider, Func<HostingProvider, Task<bool>>? signIn, IDialogService dialogs, IAppLog log, GitVersion? gitVersion = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(log);
        _hosting = hosting;
        Provider = provider;
        _signIn = signIn;
        _dialogs = dialogs;
        _log = log;
        _gitVersion = gitVersion;
        _account = hosting?.AccountOf(provider);
        UseForGit = _account?.Account.UseForGit ?? false;
        if (hosting is not null && RunningSignOutOf(hosting, provider) is { } running)
        {
            BusyText = Texts.AccountsBusy;
            _ = FollowRunningSignOutAsync(hosting, running);
        }
    }

    /// <summary>The block's provider.</summary>
    public HostingProvider Provider { get; }

    /// <summary>The first row's label: the provider's name (<see cref="HostingTexts.Name"/>).</summary>
    public string Label => HostingTexts.Name(Provider);

    /// <summary>What the block's automation ids end in (1.7): GitHub's keep 7a's ids, the others end in their provider.</summary>
    public string IdSuffix => Provider switch
    {
        HostingProvider.GitHub => string.Empty,
        HostingProvider.GitLab => "GitLab",
        _ => "AzureDevOps",
    };

    /// <summary>The block's automation id of 7a's <paramref name="id"/>.</summary>
    public string IdOf(string id) => id + IdSuffix;

    /// <summary>
    /// The block shows while its provider is on; GitHub's also while every provider is off or there
    /// is no session, holding 7a's <c>AccountsOff</c> (D362).
    /// </summary>
    public bool IsShown => _hosting is { } hosting && hosting.IsOn(Provider)
        || (Provider == HostingProvider.GitHub && (_hosting is null || _hosting.IsOff));

    /// <summary>The provider is off for this session (<c>AccountsOff</c>, no button); only GitHub's block shows so.</summary>
    public bool IsOff => _hosting is null || !_hosting.IsOn(Provider);

    /// <summary>No account (<c>AccountsSignedOut</c>).</summary>
    public bool IsSignedOut => !IsOff && _account is null;

    /// <summary>An account, signed in or needing sign-in (<c>AccountLogin</c>).</summary>
    public bool HasAccount => !IsOff && _account is not null;

    /// <summary>The account's login in the server's spelling (Azure DevOps' organization).</summary>
    public string? Login => HasAccount ? _account!.Account.Login : null;

    /// <summary>Azure DevOps' account's name under the organization (<c>AccountUser</c>); null for the others and without a name (6.4).</summary>
    public string? UserName => HasAccount && Provider == HostingProvider.AzureDevOps ? _account!.Account.Name : null;

    public bool ShowsUserName => UserName is not null;

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
            _ => Texts.NeedsSignInRefusedOf(Provider),
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

    /// <summary>The switch's note: the provider's (<see cref="Texts.AccountUseForGitNoteOf"/>), or the older git's when the detected git is known and older than 2.31 (D277).</summary>
    public string UseForGitNote => UsesTheAccount
        ? Texts.AccountUseForGitNoteOf(Provider, Login ?? string.Empty)
        : Texts.AccountUseForGitOldGit(_gitVersion!.Value.ToString());

    /// <summary><c>Signing out…</c> while the sign-out runs; null otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand), nameof(SignOutCommand))]
    public partial string? BusyText { get; private set; }

    /// <summary>This block's sign-out runs: its own buttons, and every other block's (<see cref="Joins"/>), are disabled.</summary>
    public bool IsBusy => BusyText is not null;

    /// <summary>The sign-out's note when the keychain kept its entry (<c>AccountsSignOutNote</c>); cleared at the next sign-in, and with the screen.</summary>
    [ObservableProperty]
    public partial string? SignOutNote { get; private set; }

    /// <summary>Raised when a control should take the keyboard focus, by automation id (<see cref="IdOf"/>); the section forwards it.</summary>
    public event EventHandler<string>? FocusRequested;

    /// <summary>Whether git takes the account's credentials: 2.31 or newer, or the version unknown (D277).</summary>
    private bool UsesTheAccount => _gitVersion is not { } version || version >= GitThatUsesTheAccount;

    /// <summary>
    /// Shows <paramref name="account"/>, the session's account of <see cref="Provider"/>: the switch
    /// reset when it is another account, and while it is not toggled (<see cref="IsChanged"/>) the
    /// account's own, so a Save that lands after the screen reopened moves an untoggled switch with
    /// it and a later Save cannot write the old value back; a toggled one keeps the user's toggle
    /// through the same account's changes (docs/phase-7a-design.md 17, WP-A's reading). The
    /// sign-out's note is cleared by a sign-in.
    /// </summary>
    public void Follow(AccountStatus? account)
    {
        var another = account?.Account.Key != _account?.Account.Key;
        var toggled = IsChanged;
        _account = account;
        if (another || !toggled)
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
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(ShowsUserName));
        OnPropertyChanged(nameof(TokenPlaceText));
        OnPropertyChanged(nameof(NeedsSignInText));
        OnPropertyChanged(nameof(ShowsTokenPlace));
        OnPropertyChanged(nameof(ShowsNeedsSignIn));
        OnPropertyChanged(nameof(ShowsSignIn));
        OnPropertyChanged(nameof(ShowsSignOut));
        OnPropertyChanged(nameof(IsChanged));
        OnPropertyChanged(nameof(UseForGitNote));
    }

    /// <summary>The section's end: the block follows no more and focuses nothing.</summary>
    internal void Close() => _disposed = true;

    /// <summary>
    /// Joins the section: while <paramref name="sectionBusy"/> holds (any block's sign-out runs), this
    /// block's "Sign in…" and "Sign out…" are disabled too, 7a's rule for one block extended to the
    /// section, so that no dialog of another block can open over the screen while a sign-out runs and
    /// the sign-out's end moves the focus under it.
    /// </summary>
    internal void Joins(Func<bool> sectionBusy) => _sectionBusy = sectionBusy;

    /// <summary>Asks both commands again whether they can run, when a block's sign-out starts or ends.</summary>
    internal void RefreshCommands()
    {
        SignInCommand.NotifyCanExecuteChanged();
        SignOutCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// "Sign in…": the window's entry for <see cref="Provider"/>, whose dialog stacks over the
    /// screen (D104); afterwards, signed in, the focus goes to this block's "Sign out…".
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
            signedIn = await _signIn(Provider);
        }
        catch (Exception ex)
        {
            _log.Warning($"Signing in to {HostingProviders.DisplayName(Provider)} from the settings screen failed.", ex);
            signedIn = false;
        }

        Follow(_hosting?.AccountOf(Provider));
        if (signedIn && !_disposed && _account is { State: AccountState.SignedIn })
        {
            FocusRequested?.Invoke(this, IdOf("AccountsSignOutButton"));
        }
    }

    private bool CanSignIn() => !IsBusy && _sectionBusy?.Invoke() != true;

    /// <summary>
    /// "Sign out…": the provider's confirmation, stacked; declined, nothing. Accepted: <c>Signing
    /// out…</c> with both buttons disabled while the session signs out of <see cref="Provider"/>, at
    /// once (D256); a reason the keychain kept its entry shows as the note; the focus goes to this
    /// block's "Sign in…".
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSignOut))]
    private async Task SignOutAsync()
    {
        if (_hosting is null || _account is not { } account)
        {
            return;
        }

        var question = Texts.SignOutQuestionOf(Provider, account.Account.Login, UsesTheAccount);
        if (!await _dialogs.ConfirmAsync(new ConfirmationRequest(Texts.SignOutTitleOf(Provider), question, string.Empty, Texts.SignOutConfirm, IsDestructive: false)))
        {
            return;
        }

        BusyText = Texts.AccountsBusy;
        var hosting = _hosting;
        var running = SignOutOfTheSessionAsync(hosting);
        Register(hosting, Provider, running);
        string? reason = null;
        try
        {
            reason = await running;
        }
        finally
        {
            Unregister(hosting, Provider, running);
            EndSignOut(hosting, reason);
        }

        if (!_disposed && ShowsSignIn)
        {
            FocusRequested?.Invoke(this, IdOf("AccountsSignInButton"));
        }
    }

    /// <summary>The session's sign-out of <see cref="Provider"/> at once (D256): its reason, or null when it failed (logged).</summary>
    private async Task<string?> SignOutOfTheSessionAsync(IHostingSession hosting)
    {
        try
        {
            return await hosting.SignOutAsync(Provider, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.Warning($"Signing out of {HostingProviders.DisplayName(Provider)} failed.", ex);
            return null;
        }
    }

    /// <summary>
    /// A block made while its provider's sign-out runs (a screen reopened meanwhile): busy until the
    /// sign-out ends, then the account's state and the keychain's note, as the block that started it
    /// shows them. It moves no focus: this screen did not start the sign-out.
    /// </summary>
    private async Task FollowRunningSignOutAsync(IHostingSession hosting, Task<string?> running)
    {
        var reason = await running;
        EndSignOut(hosting, reason);
    }

    /// <summary>
    /// The sign-out's end, the one place for the block that started it and a block that follows it:
    /// the account's state and the keychain's note (<paramref name="reason"/>, null when it was removed
    /// or the sign-out failed) first, busy's end last. So a block that shows not busy already shows the
    /// end ("until it ends, then the account and the keychain's note", docs/phase-7b-design.md 10.9),
    /// for whoever reads it when <c>IsBusy</c> changes (the section asks every block's buttons again
    /// then) and on whatever thread the sign-out ends on when no dispatcher holds the block (the view
    /// models' tests; CI 42339567989 read a reopened block not busy and without its note). Busy ends
    /// even if a handler of the account's or the note's change throws, as it did when it ended first.
    /// </summary>
    private void EndSignOut(IHostingSession hosting, string? reason)
    {
        try
        {
            Follow(hosting.AccountOf(Provider));
            SignOutNote = reason is null ? null : Texts.AccountsSignOutNote(reason);
        }
        finally
        {
            BusyText = null;
        }
    }

    /// <summary>The sign-out of <paramref name="provider"/> that runs in <paramref name="hosting"/>, or null.</summary>
    private static Task<string?>? RunningSignOutOf(IHostingSession hosting, HostingProvider provider)
    {
        if (!RunningSignOuts.TryGetValue(hosting, out var running))
        {
            return null;
        }

        lock (running)
        {
            return running.TryGetValue(provider, out var task) && !task.IsCompleted ? task : null;
        }
    }

    private static void Register(IHostingSession hosting, HostingProvider provider, Task<string?> task)
    {
        var running = RunningSignOuts.GetValue(hosting, _ => []);
        lock (running)
        {
            running[provider] = task;
        }
    }

    private static void Unregister(IHostingSession hosting, HostingProvider provider, Task<string?> task)
    {
        if (!RunningSignOuts.TryGetValue(hosting, out var running))
        {
            return;
        }

        lock (running)
        {
            if (running.TryGetValue(provider, out var registered) && ReferenceEquals(registered, task))
            {
                running.Remove(provider);
            }
        }
    }

    private bool CanSignOut() => !IsBusy && _sectionBusy?.Invoke() != true;
}
