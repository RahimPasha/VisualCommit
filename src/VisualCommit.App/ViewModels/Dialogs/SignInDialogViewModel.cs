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
/// give, D298's token and a token that holds another provider's form anywhere, bare or wrapped
/// (<see cref="OtherProviderFormIn"/>); a token of its own form or of none goes on. Each failure shows in its provider's words
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
    [NotifyPropertyChangedFor(nameof(CanOpenTokenPage))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand), nameof(OpenTokenPageCommand))]
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

        // Another provider's form anywhere in the box, refused without a request (D378): as the
        // whole value or wrapped (quotes, a .env line, a clone URL, escaped, joined by "_", "-" or
        // "."); its own form or none goes on.
        if (OtherProviderFormIn(token, Provider) is { } form)
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

    /// <summary>
    /// The organization the box gives by <see cref="AzureDevOpsProvider.OrganizationOf"/> on the
    /// provider's endpoints; null without one or while Azure DevOps is off. A token pasted into the
    /// box, which opens focused while empty, is no organization: a name of a provider's token form
    /// (<see cref="HostingProviders.TokenFormOf"/>), of the shape of a token of no form
    /// (<see cref="HasShapeOfUnmarkedToken"/>) or equal to the token box's text is refused, so that it
    /// is never sent in a path, opened in the browser, logged or shown in an error (design 1).
    /// </summary>
    private string? OrganizationOrNull()
    {
        if (_hosting.ProviderOf(HostingProvider.AzureDevOps) is not AzureDevOpsProvider azureDevOps
            || AzureDevOpsProvider.OrganizationOf(Organization, azureDevOps.Endpoints) is not { } organization)
        {
            return null;
        }

        var token = Token.Trim();
        return HostingProviders.TokenFormOf(organization) is not null
            || HasShapeOfUnmarkedToken(organization)
            || (token.Length > 0 && string.Equals(organization, token, StringComparison.OrdinalIgnoreCase))
            ? null
            : organization;
    }

    /// <summary>
    /// The shapes of the tokens that <see cref="HostingProviders.TokenFormOf"/> leaves as forms of
    /// none, which the organization's grammar would take: Azure DevOps' legacy tokens, 52 characters
    /// of base32 (<c>a</c> to <c>z</c> and <c>2</c> to <c>7</c>), and GitHub's classic ones, 40
    /// hexadecimal digits, either in any case. Read in the organization box only, which fails closed
    /// on such a name; the token box still takes them (D378).
    /// </summary>
    private static bool HasShapeOfUnmarkedToken(string name) =>
        (name.Length == 52 && name.All(c => char.IsAsciiLetter(c) || c is >= '2' and <= '7'))
        || (name.Length == 40 && name.All(char.IsAsciiHexDigit));

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

    /// <summary>
    /// The provider other than <paramref name="provider"/> whose token form <paramref name="token"/>
    /// holds anywhere (D378; the lead's ruling of 2026-11-06 and its verifiers), or null. First the
    /// whole value's form by <see cref="HostingProviders.TokenFormOf"/> (D378 as it was); else the
    /// value is read as typed and then with its escapes read as breaks (<see cref="Unescape"/>:
    /// percent escapes, once and twice encoded, and backslash escapes such as <c>\u0022</c> and
    /// <c>\n</c>), and in each reading, run by run of token characters (letters, digits, <c>_</c>,
    /// <c>-</c> and <c>.</c>) between any other characters, the first of these that a run holds, by its place:
    /// <list type="bullet">
    /// <item>the run's own form by <see cref="HostingProviders.TokenFormOf"/>: a token in quotes, angle
    /// brackets, a <c>.env</c> line or a clone URL;</item>
    /// <item>GitLab's <c>glpat-</c> anywhere, in GitHub's and Azure DevOps' dialogs, whose tokens hold
    /// no <c>-</c>;</item>
    /// <item>GitHub's prefixes anywhere, in Azure DevOps' dialog, whose tokens hold no <c>_</c>;</item>
    /// <item>GitHub's prefix at the run's start or after <c>_</c>, <c>-</c> or <c>.</c>, in GitLab's
    /// dialog, in a run that is not a GitLab token's shape (<see cref="IsOwnShaped"/>), as GitLab's
    /// tokens may hold <c>_</c> and so a GitHub prefix by chance;</item>
    /// <item>Azure DevOps' form as a run of 84 letters and digits between other characters, in GitHub's
    /// and GitLab's dialogs, in a run that is not the dialog's token's shape.</item>
    /// </list>
    /// A token of <paramref name="provider"/>'s own holds no <c>%</c> or <c>\</c> and is one run, the
    /// whole value: GitHub's a GitHub prefix and letters, digits and <c>_</c> (or 40 hexadecimal
    /// digits), GitLab's <c>glpat-</c> and letters, digits, <c>_</c>, <c>-</c> and <c>.</c> (or, from
    /// before that prefix, 20 characters), Azure DevOps' letters and digits; none meets a rule above but
    /// its whole value's, so no own token is refused here that D378's rule did not refuse.
    /// </summary>
    public static HostingProvider? OtherProviderFormIn(string token, HostingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (HostingProviders.TokenFormOf(token) is { } whole && whole != provider)
        {
            return whole;
        }

        // As typed, then with its escapes read, up to three times (a value encoded twice).
        var text = token;
        for (var reading = 0; reading < 4; reading++)
        {
            if (FirstOtherFormIn(text, provider) is { } form)
            {
                return form;
            }

            var unescaped = Unescape(text);
            if (string.Equals(unescaped, text, StringComparison.Ordinal))
            {
                break;
            }

            text = unescaped;
        }

        return null;
    }

    // GitHub's token prefixes, as HostingProviders.TokenFormOf reads them; case kept.
    private static readonly string[] GitHubPrefixes = ["github_pat_", "ghp_", "gho_", "ghu_", "ghs_", "ghr_"];

    private const string GitLabPrefix = "glpat-";

    // GitLab's tokens from before its glpat- prefix (GitLab 14.5): 20 characters.
    private const int UnprefixedGitLabLength = 20;

    // Azure DevOps' form: 84 characters with AZDO at the 0-based characters 76 to 79 (D378).
    private const int AzureDevOpsLength = 84;
    private const int AzureDevOpsMarkIndex = 76;

    /// <summary>The first other provider's form that <paramref name="text"/>'s runs of token characters hold, by its place.</summary>
    private static HostingProvider? FirstOtherFormIn(string text, HostingProvider provider)
    {
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && IsTokenCharacter(text[i]))
            {
                continue;
            }

            if (i > start && OtherFormInRun(text[start..i], provider) is { } form)
            {
                return form;
            }

            start = i + 1;
        }

        return null;
    }

    /// <summary>The other provider's form that one run of token characters holds first, by the rules of <see cref="OtherProviderFormIn"/>; null for none.</summary>
    private static HostingProvider? OtherFormInRun(string run, HostingProvider provider)
    {
        if (HostingProviders.TokenFormOf(run) is { } form && form != provider)
        {
            return form;
        }

        var first = int.MaxValue;
        HostingProvider? found = null;
        void Consider(int at, HostingProvider other)
        {
            if (at >= 0 && at < first)
            {
                first = at;
                found = other;
            }
        }

        if (provider != HostingProvider.GitLab)
        {
            Consider(run.IndexOf(GitLabPrefix, StringComparison.Ordinal), HostingProvider.GitLab);
        }

        if (provider == HostingProvider.AzureDevOps)
        {
            foreach (var prefix in GitHubPrefixes)
            {
                Consider(run.IndexOf(prefix, StringComparison.Ordinal), HostingProvider.GitHub);
            }
        }

        if (provider != HostingProvider.AzureDevOps && !IsOwnShaped(run, provider))
        {
            if (provider == HostingProvider.GitLab)
            {
                foreach (var prefix in GitHubPrefixes)
                {
                    Consider(PrefixAtBoundary(run, prefix), HostingProvider.GitHub);
                }
            }

            Consider(AzureDevOpsFormAt(run), HostingProvider.AzureDevOps);
        }

        return found;
    }

    /// <summary>
    /// The run has the shape of <paramref name="provider"/>'s tokens, so that no other form is looked
    /// for inside it: on GitHub it starts with a GitHub prefix, on GitLab with <c>glpat-</c> or is
    /// GitLab's unprefixed 20 characters; either after any leading <c>_</c>, <c>-</c> and <c>.</c>
    /// (Markdown's underscores around the dialog's own token).
    /// </summary>
    private static bool IsOwnShaped(string run, HostingProvider provider)
    {
        var bare = run.TrimStart('_', '-', '.');
        return provider switch
        {
            HostingProvider.GitHub => GitHubPrefixes.Any(prefix => bare.StartsWith(prefix, StringComparison.Ordinal)),
            HostingProvider.GitLab => bare.StartsWith(GitLabPrefix, StringComparison.Ordinal) || run.Length == UnprefixedGitLabLength,
            _ => false,
        };
    }

    /// <summary>The first place of <paramref name="prefix"/> in the run at its start or after <c>_</c>, <c>-</c> or <c>.</c>; -1 for none.</summary>
    private static int PrefixAtBoundary(string run, string prefix)
    {
        for (var at = run.IndexOf(prefix, StringComparison.Ordinal); at >= 0; at = run.IndexOf(prefix, at + 1, StringComparison.Ordinal))
        {
            if (at == 0 || run[at - 1] is '_' or '-' or '.')
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>The start of the run's first run of letters and digits that is Azure DevOps' form, 84 long with <c>AZDO</c> at 76; -1 for none.</summary>
    private static int AzureDevOpsFormAt(string run)
    {
        var start = 0;
        for (var i = 0; i <= run.Length; i++)
        {
            if (i < run.Length && char.IsAsciiLetterOrDigit(run[i]))
            {
                continue;
            }

            if (i - start == AzureDevOpsLength && HostingProviders.TokenFormOf(run[start..i]) == HostingProvider.AzureDevOps)
            {
                return start;
            }

            start = i + 1;
        }

        return -1;
    }

    /// <summary>
    /// <paramref name="text"/> with each escape read once as a break between runs, so that the letters
    /// and digits of an escape (<c>%3A</c>'s <c>3A</c>, <c>\u0022</c>'s <c>u0022</c>, <c>\n</c>'s
    /// <c>n</c>) never join a token: <c>%</c> and two hexadecimal digits, <c>\u</c> and four, <c>\x</c>
    /// and two, and a backslash and any one character, each as a space; but one that stands for
    /// <c>%</c> or <c>\</c> as that character, so that a value escaped twice is read again. Read only to
    /// find another provider's form; never sent or shown.
    /// </summary>
    private static string Unescape(string text)
    {
        if (text.IndexOfAny(['%', '\\']) < 0)
        {
            return text;
        }

        var read = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            char? escaped = null;
            var length = 0;
            if (character == '%' && HexAt(text, i + 1, 2) is { } percent)
            {
                (escaped, length) = (percent, 3);
            }
            else if (character == '\\' && i + 1 < text.Length)
            {
                (escaped, length) = text[i + 1] switch
                {
                    'u' when HexAt(text, i + 2, 4) is { } unicode => (unicode, 6),
                    'x' when HexAt(text, i + 2, 2) is { } hex => (hex, 4),
                    var next => ((char?)next, 2),
                };
            }

            if (escaped is not { } stands)
            {
                read.Append(character);
                continue;
            }

            read.Append(stands is '%' or '\\' ? stands : ' ');
            i += length - 1;
        }

        return read.ToString();
    }

    /// <summary>The character whose code the <paramref name="count"/> hexadecimal digits at <paramref name="at"/> give; null if they are not there.</summary>
    private static char? HexAt(string text, int at, int count)
    {
        if (at + count > text.Length)
        {
            return null;
        }

        var digits = text.AsSpan(at, count);
        foreach (var digit in digits)
        {
            if (!char.IsAsciiHexDigit(digit))
            {
                return null;
            }
        }

        return (char)int.Parse(digits, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A character of some provider's tokens: an ASCII letter or digit, <c>_</c>, <c>-</c> or <c>.</c>.</summary>
    private static bool IsTokenCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.';
}
