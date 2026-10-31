using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;

namespace VisualCommit.App.ViewModels;

// Signing in to GitHub through the window (docs/phase-7a-design.md 1.7, 8.5): what the settings
// screen's "Sign in…", a tab's "Sign in to GitHub…" and the welcome page's and palette's clone from
// GitHub start (TabServices.SignInToGitHub). Work package WP-A builds it.
public partial class MainWindowViewModel
{
    /// <summary>The "Add GitHub account" dialog while it is open, so that a second call waits for it rather than opening another.</summary>
    private SignInDialogViewModel? _signInDialog;

    /// <summary>
    /// Signs in to GitHub and answers whether an account is signed in afterwards (8.5): GitHub off,
    /// false without a dialog; signed in already, true without a dialog; a sign-in dialog already
    /// open, its end awaited; else "Add GitHub account" on top of whatever is open (D104). Every
    /// sign-in item of the app ends here; no palette command signs in (D299). Failures are logged and
    /// give false.
    /// </summary>
    private async Task<bool> SignInToGitHubAsync()
    {
        try
        {
            var hosting = _tabServices.Hosting;
            if (hosting.IsOff || hosting.GitHub is null)
            {
                return false;
            }

            if (hosting.Account is { State: AccountState.SignedIn })
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

            return hosting.Account is { State: AccountState.SignedIn };
        }
        catch (Exception ex)
        {
            _log.Warning("Signing in to GitHub failed.", ex);
            return false;
        }
    }
}
