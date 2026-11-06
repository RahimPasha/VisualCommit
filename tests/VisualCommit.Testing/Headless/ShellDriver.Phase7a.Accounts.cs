using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Phase 7a's part A as the checks drive it (docs/phase-7a-design.md 8.7; the report's part A
/// conventions): the settings screen's Accounts section and the "Add GitHub account" dialog, every
/// action a click or a key on their own controls. Since phase 7b (docs/phase-7b-design.md 1.9,
/// 10.3; 7b's report, part A's conventions) beside 7a's GitHub forms, which keep their behaviour,
/// the provider forms: each block's own controls (ids 7a's followed by the provider's suffix) and
/// each provider's dialog, Azure DevOps' organization box among them.
/// </summary>
public sealed partial class ShellDriver
{
    /// <summary>The open "Add GitHub account" dialog (anywhere in the dialog stack), or null.</summary>
    public SignInDialogViewModel? SignInDialog =>
        Session.Services.DialogHost.Open.OfType<SignInDialogViewModel>().LastOrDefault();

    /// <summary>
    /// Opens the settings screen with the command modifier and <c>,</c>, waits until it shows and its
    /// reads have ended, then clicks <c>SettingsNavAccounts</c> (the real window's
    /// <c>OpenAccountsSection</c>).
    /// </summary>
    public async Task<SettingsDialogViewModel> OpenAccountsAsync(TimeSpan? timeout = null)
    {
        var dialog = await OpenSettingsAsync(SettingsOpening.Key, timeout);
        ChooseSection(SettingsSection.Accounts);
        return dialog;
    }

    /// <summary>The open settings screen as drawn (<see cref="SettingsSnapshot()"/>), on Accounts.</summary>
    public SettingsSnapshot AccountsSnapshot() => SettingsSnapshot();

    /// <summary>
    /// Clicks <c>AccountsSignInButton</c> and waits until "Add GitHub account" shows, its question to
    /// the keychain has been answered and the keyboard focus is in <c>SignInToken</c>.
    /// </summary>
    public async Task<SignInDialogViewModel> OpenSignInDialogAsync(TimeSpan? timeout = null)
    {
        Click(SettingsControl("AccountsSignInButton"));
        var dialog = await WaitForDialogAsync<SignInDialogViewModel>(timeout);
        var opened = dialog.Opened;
        await WaitForAsync(() => opened.IsCompleted, "the sign-in dialog's question to the keychain", timeout);
        await WaitForAsync(
            () => FocusManagerOf()?.GetFocusedElement() is TextBox { Name: "SignInToken" },
            "the keyboard focus in the token box",
            timeout);
        Settle();
        return dialog;
    }

    /// <summary>Types <paramref name="token"/> into <c>SignInToken</c> after what it holds (<see cref="TypeInto"/>).</summary>
    public void TypeToken(string token)
    {
        TypeInto("SignInToken", token);
        Settle();
    }

    /// <summary>
    /// Clicks the dialog's own <c>ConfirmButton</c> and waits until the check has ended: the dialog
    /// closed, or showing <c>SignInError</c> or <c>SignInSaveFailed</c>.
    /// </summary>
    public async Task ConfirmSignInAsync(TimeSpan? timeout = null)
    {
        var dialog = SignInDialog ?? throw new InvalidOperationException("The sign-in dialog is not open.");
        Click(FindByAutomationId(SignInDialogView(), "ConfirmButton"));
        await WaitForSignInCheckAsync(dialog, timeout);
    }

    /// <summary>Waits until a check of <paramref name="dialog"/> started by a click or by Enter has ended.</summary>
    public async Task WaitForSignInCheckAsync(SignInDialogViewModel dialog, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        Settle();
        await WaitForAsync(
            () => dialog.IsClosed || (!dialog.ConfirmCommand.IsRunning && (dialog.ErrorText is not null || dialog.IsSaveFailed)),
            "the sign-in dialog's check to end",
            timeout);
        Settle();
    }

    /// <summary>
    /// "Sign out…", the confirmation, then its "Sign out" (<paramref name="confirm"/>) or "Cancel",
    /// and waits for the section's state: the question closed and, when confirmed, the sign-out ended.
    /// </summary>
    public async Task SignOutAsync(bool confirm, TimeSpan? timeout = null)
    {
        var settings = SettingsDialog ?? throw new InvalidOperationException("The settings screen is not open.");
        Click(SettingsControl("AccountsSignOutButton"));
        var question = await WaitForDialogAsync<ConfirmationViewModel>(timeout);
        Click(FindByAutomationId(TopDialogView!, confirm ? "ConfirmButton" : "CancelButton"));
        await WaitForAsync(() => question.IsClosed, "the sign-out's question to close", timeout);
        var accounts = settings.Accounts.GitHub;
        await WaitForAsync(() => !accounts.SignOutCommand.IsRunning, "the sign-out to end", timeout);
        Settle();
    }

    /// <summary>
    /// Brings the block's <c>AccountsSignInButton&lt;suffix&gt;</c> of <paramref name="provider"/> into
    /// view and clicks it, then waits until that provider's dialog shows, its question to the keychain
    /// has been answered and the keyboard focus is in <c>SignInOrganization</c> when that box shows and
    /// is empty, else in <c>SignInToken</c>.
    /// </summary>
    public async Task<SignInDialogViewModel> OpenSignInDialogAsync(HostingProvider provider, TimeSpan? timeout = null)
    {
        ClickInView(SettingsControl(AccountIdOf(provider, "AccountsSignInButton")));
        await WaitForAsync(() => Session.Services.DialogHost.Current is SignInDialogViewModel { } shown && shown.Provider == provider, $"the {HostingProviders.DisplayName(provider)} sign-in dialog on top", timeout);
        var dialog = Dialog<SignInDialogViewModel>();
        var opened = dialog.Opened;
        await WaitForAsync(() => opened.IsCompleted, "the sign-in dialog's question to the keychain", timeout);
        var box = dialog.ShowsOrganization && dialog.Organization.Length == 0 ? "SignInOrganization" : "SignInToken";
        await WaitForAsync(
            () => FocusManagerOf()?.GetFocusedElement() is TextBox focused && focused.Name == box,
            $"the keyboard focus in {box}",
            timeout);
        Settle();
        return dialog;
    }

    /// <summary>Types <paramref name="organization"/> into <c>SignInOrganization</c> after what it holds (<see cref="TypeInto"/>: focus, caret at the end, typed).</summary>
    public void TypeOrganization(string organization)
    {
        TypeInto("SignInOrganization", organization);
        Settle();
    }

    /// <summary>
    /// The block of <paramref name="provider"/>: its "Sign out…" brought into view and clicked, the
    /// provider's confirmation, then its "Sign out" (<paramref name="confirm"/>) or "Cancel"; waits
    /// until the question has closed and, when confirmed, the sign-out has ended.
    /// </summary>
    public async Task SignOutAsync(HostingProvider provider, bool confirm, TimeSpan? timeout = null)
    {
        var settings = SettingsDialog ?? throw new InvalidOperationException("The settings screen is not open.");
        ClickInView(SettingsControl(AccountIdOf(provider, "AccountsSignOutButton")));
        var question = await WaitForDialogAsync<ConfirmationViewModel>(timeout);
        Click(FindByAutomationId(TopDialogView!, confirm ? "ConfirmButton" : "CancelButton"));
        await WaitForAsync(() => question.IsClosed, "the sign-out's question to close", timeout);
        var block = settings.Accounts.BlockOf(provider);
        await WaitForAsync(() => !block.SignOutCommand.IsRunning, "the sign-out to end", timeout);
        Settle();
    }

    /// <summary>Waits until the session's account of <paramref name="provider"/> is as <paramref name="condition"/> wants.</summary>
    public async Task WaitForAccountAsync(HostingProvider provider, Func<AccountStatus?, bool> condition, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var hosting = Session.Services.Hosting;
        await WaitForAsync(() => condition(hosting.AccountOf(provider)), $"the {HostingProviders.DisplayName(provider)} account's state", timeout);
        Settle();
    }

    /// <summary>The block of <paramref name="provider"/> in the open settings screen: <c>AccountsGitHub</c>, <c>AccountsGitLab</c> or <c>AccountsAzureDevOps</c>.</summary>
    public Control AccountsBlock(HostingProvider provider) => SettingsControl(provider switch
    {
        HostingProvider.GitHub => "AccountsGitHub",
        HostingProvider.GitLab => "AccountsGitLab",
        _ => "AccountsAzureDevOps",
    });

    /// <summary>The automation id of 7a's <paramref name="id"/> in the block of <paramref name="provider"/> (7a's for GitHub, else followed by the provider, 1.7).</summary>
    public static string AccountIdOf(HostingProvider provider, string id) => id + provider switch
    {
        HostingProvider.GitHub => string.Empty,
        HostingProvider.GitLab => "GitLab",
        _ => "AzureDevOps",
    };

    /// <summary>Brings <paramref name="control"/> into its scroll viewer's view, lets the layout settle, then clicks it.</summary>
    private void ClickInView(Control control)
    {
        control.BringIntoView();
        Settle();
        Click(control);
    }

    /// <summary>Waits until the session's account (<c>Session.Services.Hosting.Account</c>) is as <paramref name="condition"/> wants.</summary>
    public async Task WaitForAccountAsync(Func<AccountStatus?, bool> condition, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var hosting = Session.Services.Hosting;
        await WaitForAsync(() => condition(hosting.Account), "the GitHub account's state", timeout);
        Settle();
    }

    /// <summary>The sign-in dialog's view as the dialog layer draws it (also under another dialog).</summary>
    public Control SignInDialogView()
    {
        var dialog = SignInDialog ?? throw new InvalidOperationException("The sign-in dialog is not open.");
        return Window.GetVisualDescendants().OfType<ContentPresenter>()
            .FirstOrDefault(presenter => ReferenceEquals(presenter.Content, dialog) && presenter.Child is not null)?.Child
            ?? throw new InvalidOperationException("The sign-in dialog is not drawn.");
    }

    private IFocusManager? FocusManagerOf() => TopLevel.GetTopLevel(Window)?.FocusManager;
}
