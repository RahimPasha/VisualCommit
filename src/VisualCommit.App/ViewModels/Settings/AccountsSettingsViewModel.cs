using CommunityToolkit.Mvvm.ComponentModel;
using VisualCommit.App.ViewModels.Dialogs;
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
public sealed class AccountsSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IHostingSession? _hosting;
    private bool _disposed;

    /// <param name="hosting">The session's hosting; null without one (all off).</param>
    /// <param name="signIn">The window's sign-in entry (10.5), which stacks the provider's dialog over the screen; null without one.</param>
    /// <param name="dialogs">Where a sign-out's confirmation stacks over the screen (D104).</param>
    /// <param name="log">The app's log.</param>
    /// <param name="gitVersion">The detected git's version, null when unknown (D277).</param>
    public AccountsSettingsViewModel(IHostingSession? hosting, Func<HostingProvider, Task<bool>>? signIn, IDialogService dialogs, IAppLog log, GitVersion? gitVersion = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(log);
        _hosting = hosting;
        Blocks = [.. HostingProviders.InAccountsOrder.Select(provider => new AccountBlockViewModel(hosting, provider, signIn, dialogs, log, gitVersion))];
        foreach (var block in Blocks)
        {
            block.FocusRequested += (_, id) => FocusRequested?.Invoke(this, id);
            block.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(AccountBlockViewModel.IsChanged))
                {
                    OnPropertyChanged(nameof(IsChanged));
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
    }
}
