using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.GitSetup;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels.Settings;

/// <summary>
/// The settings screen's Accounts section (docs/phase-7a-design.md 8.3; docs/phase-7b-design.md
/// 10.3; D256, D294, D353): one block per provider in D353's order, GitHub, GitLab, Azure DevOps
/// (<see cref="Blocks"/>), each 7a's account section over its provider's account
/// (<see cref="AccountBlockViewModel"/>). It reads the session's accounts when the screen opens and
/// follows <see cref="IHostingSession.AccountChanged"/>, which the session raises on the UI thread,
/// handing each event to its provider's block alone; it makes no git call and no request when the
/// screen opens. Sign in and sign out act at once (D256): the screen's Cancel cannot undo them. The
/// switches are settings: the screen's Save applies each changed one (<see cref="ApplySwitches"/>,
/// D216), and Cancel and Esc drop them.
/// </summary>
public sealed partial class AccountsSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IHostingSession? _hosting;
    private readonly GitVersion? _gitVersion;
    private readonly Func<bool>? _homeHasNetrc;
    private readonly Action? _dismissNetrcNote;
    private readonly IAppLog _log;
    private bool _disposed;

    /// <summary>What <see cref="ReadNetrcAsync"/> found: whether the home folder git is given holds a netrc file; null until it has read, and when it could not.</summary>
    private bool? _homeHasNetrcFile;

    private bool _netrcNoteDismissed;

    /// <param name="hosting">The session's hosting; null without one (all off).</param>
    /// <param name="signIn">The window's sign-in entry (10.5), which stacks the provider's dialog over the screen; null without one.</param>
    /// <param name="dialogs">Where a sign-out's confirmation stacks over the screen (D104).</param>
    /// <param name="log">The app's log.</param>
    /// <param name="gitVersion">
    /// The detected git's version, null when unknown. No block reads it: every git the app supports
    /// takes the account's credentials (D420), so the switch's note and the sign-out's question are
    /// the provider's on every git. The netrc note reads it: below 2.46 a netrc entry can answer
    /// git's requests before the account (D424).
    /// </param>
    /// <param name="homeHasNetrc">
    /// Whether the home folder git is given holds a <c>.netrc</c> or <c>_netrc</c> file, asked off the
    /// UI thread when the screen opens (<see cref="ReadNetrcAsync"/>); null asks nothing and shows no
    /// note (the note dismissed for the session, or no git configuration).
    /// </param>
    /// <param name="dismissNetrcNote">Tells the window that the note was dismissed, so that it stays away for the session; null for none.</param>
    public AccountsSettingsViewModel(
        IHostingSession? hosting,
        Func<HostingProvider, Task<bool>>? signIn,
        IDialogService dialogs,
        IAppLog log,
        GitVersion? gitVersion = null,
        Func<bool>? homeHasNetrc = null,
        Action? dismissNetrcNote = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(log);
        _hosting = hosting;
        _gitVersion = gitVersion;
        _homeHasNetrc = homeHasNetrc;
        _dismissNetrcNote = dismissNetrcNote;
        _log = log;
        Blocks = [.. HostingProviders.InAccountsOrder.Select(provider => new AccountBlockViewModel(hosting, provider, signIn, dialogs, log))];
        foreach (var block in Blocks)
        {
            // While any block's sign-out runs, every block's buttons are disabled (7a's rule, section-wide).
            block.Joins(() => Blocks.Any(any => any.IsBusy));
            block.FocusRequested += (_, id) => FocusRequested?.Invoke(this, id);
            block.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(AccountBlockViewModel.IsChanged))
                {
                    OnPropertyChanged(nameof(IsChanged));
                }
                else if (e.PropertyName is nameof(AccountBlockViewModel.IsBusy))
                {
                    foreach (var other in Blocks)
                    {
                        other.RefreshCommands();
                    }
                }
            };
        }

        if (hosting is not null)
        {
            hosting.AccountChanged += OnAccountChanged;
        }
    }

    /// <summary>The blocks in D353's order (<see cref="HostingProviders.InAccountsOrder"/>).</summary>
    public IReadOnlyList<AccountBlockViewModel> Blocks { get; }

    /// <summary>GitHub's block (7a's section).</summary>
    public AccountBlockViewModel GitHub => BlockOf(HostingProvider.GitHub);

    /// <summary>GitLab's block.</summary>
    public AccountBlockViewModel GitLab => BlockOf(HostingProvider.GitLab);

    /// <summary>Azure DevOps' block.</summary>
    public AccountBlockViewModel AzureDevOps => BlockOf(HostingProvider.AzureDevOps);

    /// <summary>Any block's switch differs from its account's: Save applies it.</summary>
    public bool IsChanged => Blocks.Any(block => block.IsChanged);

    /// <summary>Raised when a control should take the keyboard focus, by automation id; the screen forwards it.</summary>
    public event EventHandler<string>? FocusRequested;

    /// <summary>The block of <paramref name="provider"/>.</summary>
    public AccountBlockViewModel BlockOf(HostingProvider provider) => Blocks.First(block => block.Provider == provider);

    /// <summary>
    /// The netrc note (<c>AccountsNetrcNote</c>, D424) shows: an account is signed in with "Use for
    /// git over HTTPS" saved on, the git found is older than 2.46, the home folder git is given
    /// holds a <c>.netrc</c> or <c>_netrc</c> file, and the note was not dismissed this session.
    /// </summary>
    public bool ShowsNetrcNote =>
        !_netrcNoteDismissed
        && _homeHasNetrcFile == true
        && _gitVersion is { } version && version < GitSetupTexts.NetrcSafeFrom
        && _hosting is { } hosting
        && HostingProviders.InAccountsOrder.Any(provider => hosting.AccountOf(provider) is { State: AccountState.SignedIn, Account.UseForGit: true });

    /// <summary>The note's text, which names the git found; empty while it does not show.</summary>
    public string NetrcNote => _gitVersion is { } version ? GitSetupTexts.NetrcNote(version) : string.Empty;

    /// <summary>
    /// Asks, off the UI thread, whether the home folder git is given holds a netrc file: one of the
    /// screen's reads when it opens. Only whether the file exists, never what it holds. A failure
    /// shows no note.
    /// </summary>
    public async Task ReadNetrcAsync()
    {
        if (_homeHasNetrc is not { } ask)
        {
            return;
        }

        try
        {
            _homeHasNetrcFile = await Task.Run(ask);
        }
        catch (Exception ex)
        {
            _log.Warning("Whether the home folder holds a netrc file could not be read.", ex);
            _homeHasNetrcFile = null;
        }

        OnPropertyChanged(nameof(ShowsNetrcNote));
    }

    /// <summary>The note's Dismiss: gone, and away for the session; nothing is written.</summary>
    [RelayCommand]
    private void DismissNetrcNote()
    {
        _netrcNoteDismissed = true;
        _dismissNetrcNote?.Invoke();
        OnPropertyChanged(nameof(ShowsNetrcNote));
    }

    /// <summary>The screen's Save (D216): each changed switch, one <see cref="IHostingSession.SetUseForGit(HostingProvider, bool)"/> per block, which the session writes off the UI thread.</summary>
    public void ApplySwitches()
    {
        if (_hosting is null)
        {
            return;
        }

        foreach (var block in Blocks.Where(block => block.IsChanged))
        {
            _hosting.SetUseForGit(block.Provider, block.UseForGit);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var block in Blocks)
        {
            block.Close();
        }

        if (_hosting is not null)
        {
            _hosting.AccountChanged -= OnAccountChanged;
        }
    }

    /// <summary>One provider's event reaches its block alone, so another provider's never resets a toggled switch.</summary>
    private void OnAccountChanged(object? sender, HostingAccountChangedEventArgs e)
    {
        if (!_disposed && Blocks.FirstOrDefault(block => block.Provider == e.Provider) is { } block)
        {
            block.Follow(_hosting?.AccountOf(e.Provider));
        }

        // A sign-in, a sign-out or a saved switch can show or hide the netrc note.
        OnPropertyChanged(nameof(ShowsNetrcNote));
    }
}
