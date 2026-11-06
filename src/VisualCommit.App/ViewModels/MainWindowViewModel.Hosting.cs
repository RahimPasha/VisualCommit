using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels;

// Signing in through the window (docs/phase-7a-design.md 1.7, 8.5; docs/phase-7b-design.md 1.7,
// 10.5): what the settings screen's "Sign in…" of each block, a tab's "Sign in to …" and the
// welcome page's and palette's clone start (TabServices.SignIn), for any provider.
public partial class MainWindowViewModel
{
    /// <summary>The sign-in dialog of any provider while it is open, so that a second call waits for it rather than opening another (ruling 22).</summary>
    private SignInDialogViewModel? _signInDialog;

    /// <summary>
    /// Signs in to <paramref name="provider"/> and answers whether its account is signed in
    /// afterwards (8.5, 10.5): off or without its provider, false without a dialog; signed in
    /// already, true without a dialog; a sign-in dialog of any provider already open, its end
    /// awaited, then the answer for <paramref name="provider"/> with no second dialog (ruling 22);
    /// else the provider's dialog on top of whatever is open (D104), Azure DevOps' prefilled with the
    /// held account's login, else with the <see cref="HostedRepository.OwnerRoot"/> of the active
    /// tab's first Azure DevOps remote in <see cref="HostingRemoteOrder"/>. Every sign-in item of the
    /// app ends here; no palette command signs in (D299). Failures are logged and give false.
    /// </summary>
    private async Task<bool> SignInAsync(HostingProvider provider)
    {
        try
        {
            var hosting = _tabServices.Hosting;
            if (!hosting.IsOn(provider) || hosting.ProviderOf(provider) is null)
            {
                return false;
            }

            if (hosting.AccountOf(provider) is { State: AccountState.SignedIn })
            {
                return true;
            }

            if (_signInDialog is { IsClosed: false } open)
            {
                await open.Closed;
            }
            else
            {
                var dialog = new SignInDialogViewModel(hosting, provider, _tabServices.Launcher, _log, OrganizationToPrefill(hosting, provider));
                _signInDialog = dialog;
                try
                {
                    await Dialogs.ShowAsync(dialog);
                }
                finally
                {
                    if (ReferenceEquals(_signInDialog, dialog))
                    {
                        _signInDialog = null;
                    }
                }
            }

            return hosting.AccountOf(provider) is { State: AccountState.SignedIn };
        }
        catch (Exception ex)
        {
            _log.Warning($"Signing in to {HostingProviders.DisplayName(provider)} failed.", ex);
            return false;
        }
    }

    /// <summary>Azure DevOps' dialog's first organization (10.5): the held account's login, else the active tab's first Azure DevOps remote's organization; null for the others.</summary>
    private string? OrganizationToPrefill(IHostingSession hosting, HostingProvider provider)
    {
        if (provider != HostingProvider.AzureDevOps)
        {
            return null;
        }

        if (hosting.AccountOf(provider) is { } held)
        {
            return held.Account.Login;
        }

        var remotes = ActiveTab?.Repository?.HostingState.Remotes;
        return remotes is null
            ? null
            : HostingRemoteOrder.Of(remotes.Keys).Select(remote => remotes[remote]).FirstOrDefault(repository => repository.Provider == HostingProvider.AzureDevOps)?.OwnerRoot;
    }
}
