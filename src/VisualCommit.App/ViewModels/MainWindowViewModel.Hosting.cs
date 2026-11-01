using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels;

// Signing in through the window (docs/phase-7a-design.md 1.7, 8.5; docs/phase-7b-design.md 1.7):
// what the settings screen's "Sign in…", a tab's "Sign in to GitHub…" and the welcome page's and
// palette's clone from GitHub start (TabServices.SignIn). GitHub's alone until WP-A.
public partial class MainWindowViewModel
{
    /// <summary>The "Add GitHub account" dialog while it is open, so that a second call waits for it rather than opening another.</summary>
    private SignInDialogViewModel? _signInDialog;

    /// <summary>
    /// Signs in to <paramref name="provider"/> and answers whether its account is signed in
    /// afterwards (8.5): another provider than GitHub, false until WP-A; GitHub off, false without a
    /// dialog; signed in already, true without a dialog; a sign-in dialog already open, its end
    /// awaited; else "Add GitHub account" on top of whatever is open (D104). Every sign-in item of
    /// the app ends here; no palette command signs in (D299). Failures are logged and give false.
    /// </summary>
    private async Task<bool> SignInAsync(HostingProvider provider)
    {
        try
        {
            var hosting = _tabServices.Hosting;
            if (provider != HostingProvider.GitHub || !hosting.IsOn(provider) || hosting.ProviderOf(provider) is null)
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
                var dialog = new SignInDialogViewModel(hosting, _tabServices.Launcher, _log);
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
            _log.Warning("Signing in to GitHub failed.", ex);
            return false;
        }
    }
}
