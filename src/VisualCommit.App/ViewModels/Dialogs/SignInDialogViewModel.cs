using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting;
using VisualCommit.Hosting.AzureDevOps;
using VisualCommit.Hosting.GitLab;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.AccountTexts;

namespace VisualCommit.App.ViewModels.Dialogs;

/// <summary>
/// "Add GitHub account" (<c>SignInDialog</c>, docs/phase-7a-design.md 8.4; D249, D253, D288, D295,
/// D297, D298, ruling 19): a personal access token, masked, checked with GitHub and kept by the
/// session in the keychain, else for the session. When it opens it asks whether a keychain can be
/// reached and, if none can, says so before anything is typed. Sign in (Enter too) refuses a token
/// with a character outside printable ASCII before any request; otherwise the box and Sign in are
/// disabled while GitHub checks it. A refusal shows GitHub's words with the box enabled and its text
/// selected; a sign-in closes the dialog with the account's status, unless the save failed, which
/// leaves one "Close" (and Enter and Esc) that closes it signed in. Cancel during the check cancels
/// the request; if GitHub had already answered, the session completes the sign-in all the same
/// (D288). Nothing typed is logged, and the token leaves the box only for the session.
/// <para>
/// Since phase 7b one dialog per provider (docs/phase-7b-design.md 10.4; D345, D360, D378, D385):
/// "Add GitLab account" with a second link to GitLab's legacy form, and "Add Azure DevOps account"
/// with an organization box above the token's, read by <see cref="AzureDevOpsProvider.OrganizationOf"/>
/// alone. Sign in refuses, without a request and in this order, an organization the box does not
/// give, D298's token and a token of another provider's form (<see cref="HostingProviders.TokenFormOf"/>);
/// a token of its own form or of none goes on. Each failure shows in its provider's words
/// (<see cref="Texts.SignInFailedOf"/>); <c>NotFound</c> selects the organization box's text, any other
/// failure the token box's. GitHub's dialog keeps 7a's texts and behaviour.
/// </para>
/// </summary>
public sealed partial class SignInDialogViewModel : DialogViewModel<AccountStatus?>
{
    /// <summary>A check that ended without the service's answer, for a reason the session did not word (D284).</summary>
    private readonly HostingFailure _unreachable;

    private readonly IHostingSession _hosting;
    private readonly IFileLauncher? _launcher;
    private readonly IAppLog _log;
    private readonly CancellationTokenSource _cancellation = new();
    private AccountStatus? _signedIn;

    /// <param name="hosting">The session that checks and keeps the token.</param>
    /// <param name="provider">The provider the dialog signs in to.</param>
    /// <param name="launcher">Opens the provider's page for a new token in the browser (D295); null for none.</param>
    /// <param name="log">The app's log.</param>
    /// <param name="organization">Azure DevOps' organization box's first text (10.5); null for none.</param>
    public SignInDialogViewModel(IHostingSession hosting, HostingProvider provider, IFileLauncher? launcher, IAppLog log, string? organization = null)
    {
        ArgumentNullException.ThrowIfNull(hosting);
        ArgumentNullException.ThrowIfNull(log);
        _hosting = hosting;
        Provider = provider;
        _launcher = launcher;
        _log = log;
        _unreachable = new HostingFailure(HostingFailureKind.Network, Texts.SignInUnreachableOf(provider)) { Provider = provider };
        Organization = provider == HostingProvider.AzureDevOps ? organization ?? string.Empty : string.Empty;
        Opened = AskForTheKeychainAsync();
    }

    /// <summary>The provider the dialog signs in to.</summary>
    public HostingProvider Provider { get; }

    public override string Title => HostingTexts.AddAccountTitleOf(Provider);

    /// <summary><c>SignInHelp</c>: the provider's help.</summary>
    public string HelpText => Texts.SignInHelpOf(Provider);

    /// <summary><c>SignInTokenLink</c>'s words.</summary>
    public string TokenLinkText => Texts.TokenLinkOf(Provider);

    /// <summary><c>SignInTokenLink</c>'s tooltip.</summary>
    public string TokenLinkTip => Texts.TokenLinkTipOf(Provider);

    /// <summary>GitLab's second link to its legacy form (<c>SignInLegacyTokenLink</c>, D385).</summary>
    public bool ShowsLegacyLink => Provider == HostingProvider.GitLab;

    /// <summary>Azure DevOps' organization box (<c>SignInOrganization</c>, D360).</summary>
    public bool ShowsOrganization => Provider == HostingProvider.AzureDevOps;

    /// <summary>What the organization box holds (Azure DevOps'); empty for the others.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenTokenPage))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand), nameof(OpenTokenPageCommand))]
    public partial string Organization { get; set; } = string.Empty;

    /// <summary>The token link is enabled: always but on Azure DevOps, where the organization box must give an organization (10.4).</summary>
    public bool CanOpenTokenPage => !ShowsOrganization || OrganizationOrNull() is not null;

    /// <summary>The account once a sign-in whose save failed has signed in (ruling 19), else null: Cancel and Esc never undo a sign-in.</summary>
    protected override AccountStatus? CancelledResult => _signedIn;

    /// <summary>Completes when the opening's question to the keychain has been answered.</summary>
    public Task Opened { get; }

    /// <summary>What the box holds, shown masked (<c>SignInToken</c>, D103, D130).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string Token { get; set; } = string.Empty;

    /// <summary>No keychain can be reached (D253): <c>SignInNoKeychain</c> shows from the opening.</summary>
    [ObservableProperty]
    public partial bool ShowsNoKeychain { get; private set; }

    /// <summary>GitHub checks the token: <c>SignInChecking</c>, the box and Sign in disabled, Cancel enabled.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBoxEnabled))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsChecking { get; private set; }

    /// <summary><c>SignInError</c>: why the token was refused, by GitHub or before any request; null otherwise.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    /// <summary><c>SignInSaveFailed</c>: signed in, but the keychain did not keep the token (ruling 19); null otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSaveFailed), nameof(IsBoxEnabled), nameof(ShowsCancel), nameof(ConfirmText))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? SaveFailedText { get; private set; }

    public bool IsSaveFailed => SaveFailedText is not null;

    /// <summary>The boxes take typing: no check runs and no save failed.</summary>
    public bool IsBoxEnabled => !IsChecking && !IsSaveFailed;

    /// <summary>Cancel shows until a save failed, when one "Close" is left.</summary>
    public bool ShowsCancel => !IsSaveFailed;

    /// <summary>The confirm button's label: "Sign in", or "Close" after a failed save.</summary>
    public string ConfirmText => IsSaveFailed ? Texts.Close : Texts.SignIn;

    /// <summary>Raised when a control should take the keyboard focus, by automation id: the box with its text selected after a refusal, "Close" after a failed save.</summary>
    public event EventHandler<string>? FocusRequested;

    /// <summary>
    /// Sign in, Enter too (8.4): the trimmed token checked locally, then by GitHub through the
    /// session; after a failed save, "Close", which closes the dialog signed in.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        if (IsClosed)
        {
            return;
        }

        if (IsSaveFailed)
        {
            Complete(_signedIn);
            return;
        }

        var token = Token.Trim();
        if (token.Length == 0 || (ShowsOrganization && Organization.Trim().Length == 0))
        {
            return;
        }

        // Azure DevOps' organization, by the one rule (10.4): none, refused without a request.
        var organization = ShowsOrganization ? OrganizationOrNull() : null;
        if (ShowsOrganization && organization is null)
        {
            ErrorText = Texts.SignInNotAnOrganization;
            return;
        }

        // GitHub's tokens are letters, digits and "_"; a space, a line break, a control or a
        // non-ASCII character would fail as Network in a header (D298): refused without a request.
        if (!IsPrintableAscii(token))
        {
            ErrorText = Texts.SignInNotAToken;
            return;
        }

        // Another provider's form, refused without a request (D378); its own form or none goes on.
        if (HostingProviders.TokenFormOf(token) is { } form && form != Provider)
        {
            ErrorText = Texts.SignInOtherProviderTokenOf(Provider, form, organization);
            return;
        }

        ErrorText = null;
        IsChecking = true;
        SignInResult result;
        try
        {
            result = await _hosting.SignInAsync(new SignInRequest(Provider, token, organization), _cancellation.Token);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            // Cancel during the check: the dialog has closed.
            return;
        }
        catch (Exception ex)
        {
            // Anything else, the session's own end among it, gets WP-G's fixed text (D284); the
            // exception's words go to the log only.
            _log.Warning($"Signing in to {HostingProviders.DisplayName(Provider)} failed.", ex);
            result = new SignInResult(null, _unreachable, null);
        }
        finally
        {
            IsChecking = false;
        }

        if (IsClosed)
        {
            // Cancelled after GitHub had answered: the session completed the sign-in (D288).
            return;
        }

        if (result.Status is not { } status)
        {
            var failure = result.Failure ?? _unreachable;
            ErrorText = Texts.SignInFailedOf(failure, organization);
            FocusRequested?.Invoke(this, ShowsOrganization && failure.Kind == HostingFailureKind.NotFound ? "SignInOrganization" : "SignInToken");
            return;
        }

        Token = string.Empty;
        if (result.SaveFailedReason is { } reason)
        {
            _signedIn = status;
            SaveFailedText = Texts.SignInSaveFailed(status.Account.Login, reason);
            FocusRequested?.Invoke(this, "ConfirmButton");
            return;
        }

        Complete(status);
    }

    private bool CanConfirm() => IsSaveFailed || (!IsChecking && Token.Trim().Length > 0 && (!ShowsOrganization || Organization.Trim().Length > 0));

    /// <summary>
    /// The token link: the provider's page for a new token (<see cref="IHostingProvider.TokenPage"/>):
    /// GitHub's with the access VisualCommit needs (D295), GitLab's fine-grained form, Azure DevOps'
    /// organization's page, enabled only with an organization (10.4).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpenTokenPage))]
    private async Task OpenTokenPageAsync()
    {
        if (_launcher is null || _hosting.ProviderOf(Provider) is not { } provider || !CanOpenTokenPage)
        {
            return;
        }

        try
        {
            await _launcher.OpenUrlAsync(provider.TokenPage(ShowsOrganization ? OrganizationOrNull() : null));
        }
        catch (Exception ex)
        {
            _log.Warning($"Opening {HostingProviders.Possessive(Provider)} page for a new token failed.", ex);
        }
    }

    /// <summary>"Or create a legacy token, filled in": GitLab's legacy form with the api scope (<see cref="GitLabProvider.LegacyTokenPage"/>, D385).</summary>
    [RelayCommand]
    private async Task OpenLegacyTokenPageAsync()
    {
        if (_launcher is null || _hosting.ProviderOf(Provider) is not GitLabProvider gitLab)
        {
            return;
        }

        try
        {
            await _launcher.OpenUrlAsync(gitLab.LegacyTokenPage);
        }
        catch (Exception ex)
        {
            _log.Warning("Opening GitLab's page for a legacy token failed.", ex);
        }
    }

    /// <summary>The organization the box gives by <see cref="AzureDevOpsProvider.OrganizationOf"/> on the provider's endpoints; null without one or while Azure DevOps is off.</summary>
    private string? OrganizationOrNull() =>
        _hosting.ProviderOf(HostingProvider.AzureDevOps) is AzureDevOpsProvider azureDevOps ? AzureDevOpsProvider.OrganizationOf(Organization, azureDevOps.Endpoints) : null;

    /// <summary>Cancel and Esc, and the session's end (<c>CancelAll</c>): the check's request is cancelled.</summary>
    protected override void OnCancel()
    {
        _cancellation.Cancel();
        base.OnCancel();
    }

    private async Task AskForTheKeychainAsync()
    {
        try
        {
            ShowsNoKeychain = !await _hosting.KeychainAvailableAsync(_cancellation.Token);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            // Closed before the keychain answered.
        }
        catch (Exception ex)
        {
            _log.Warning("Asking whether a keychain can be reached failed.", ex);
        }
    }

    /// <summary>Every character is printable ASCII, U+0021 to U+007E (D298).</summary>
    private static bool IsPrintableAscii(string token) => token.All(character => character is >= '!' and <= '~');
}
