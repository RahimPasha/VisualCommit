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
        // whole value or wrapped (quotes, a .env line, a clone URL, escaped outside or inside its form,
        // with a letter, a digit, "_", "-" or "." against it); its own form or none goes on.
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
    /// The provider other than <paramref name="provider"/> whose token <paramref name="token"/> holds
    /// (D378; the lead's ruling of 2026-11-06, Hunt 3, its verifiers and the skeptic of <c>587b425</c>),
    /// or null. First the whole value's form by <see cref="HostingProviders.TokenFormOf"/> (D378 as it
    /// was). Else the value is read as typed and then with its escapes read (<see cref="Unescape"/>, up to
    /// three times, for a value encoded twice); each reading is cut into runs of token characters
    /// (letters, digits, <c>_</c>, <c>-</c> and <c>.</c>) at every other character, and in each run
    /// another provider's signature counts wherever it lies, but only where it begins before the dialog's
    /// own token:
    /// <list type="bullet">
    /// <item>GitHub's: one of its prefixes (<c>github_pat_</c>, <c>ghp_</c>, <c>gho_</c>, <c>ghu_</c>,
    /// <c>ghs_</c>, <c>ghr_</c>);</item>
    /// <item>GitLab's: <c>glpat-</c>;</item>
    /// <item>Azure DevOps': <c>AZDO</c> inside one stretch of letters and digits with at least 76 of
    /// them before it and 4 after it, beginning 76 before the mark.</item>
    /// </list>
    /// The dialog's own token is, in GitHub's and GitLab's dialogs, the run from the first place of the
    /// dialog's own prefix (a GitHub prefix; <c>glpat-</c>) to its end, and in GitLab's also a run of 20
    /// characters, the length of its unprefixed tokens; Azure DevOps' tokens hold no <c>_</c> or
    /// <c>-</c>, and so none of the others' signatures. The first signature by its place, in the first
    /// reading that holds one, names the provider. A token of <paramref name="provider"/>'s own form,
    /// bare, is one run that is its own token from its first character (or holds no signature), so no
    /// own token is refused here that D378's rule on the whole value did not refuse. Linear in the
    /// value's length.
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

    // Azure DevOps' form: 84 letters and digits with AZDO at the 0-based characters 76 to 79 (D378).
    private const string AzureDevOpsMark = "AZDO";
    private const int AzureDevOpsLength = 84;
    private const int AzureDevOpsMarkIndex = 76;

    /// <summary>The first other provider's signature that <paramref name="text"/>'s runs of token characters hold, by its place.</summary>
    private static HostingProvider? FirstOtherFormIn(string text, HostingProvider provider)
    {
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && IsTokenCharacter(text[i]))
            {
                continue;
            }

            if (i > start && OtherFormInRun(text.AsSpan(start, i - start), provider) is { } form)
            {
                return form;
            }

            start = i + 1;
        }

        return null;
    }

    /// <summary>The other provider whose signature begins first in one run of token characters, before the dialog's own token (<see cref="OtherProviderFormIn"/>); null for none.</summary>
    private static HostingProvider? OtherFormInRun(ReadOnlySpan<char> run, HostingProvider provider)
    {
        var own = provider switch
        {
            HostingProvider.GitHub => FirstOf(run, GitHubPrefixes),
            HostingProvider.GitLab => run.Length == UnprefixedGitLabLength ? 0 : run.IndexOf(GitLabPrefix, StringComparison.Ordinal),
            _ => -1,
        };

        // A signature counts where it begins before the dialog's own token; no two begin at one place.
        var limit = own < 0 ? run.Length : own;
        HostingProvider? found = null;
        if (provider != HostingProvider.GitHub && FirstOf(run, GitHubPrefixes) is >= 0 and var gitHub && gitHub < limit)
        {
            (found, limit) = (HostingProvider.GitHub, gitHub);
        }

        if (provider != HostingProvider.GitLab && run.IndexOf(GitLabPrefix, StringComparison.Ordinal) is >= 0 and var gitLab && gitLab < limit)
        {
            (found, limit) = (HostingProvider.GitLab, gitLab);
        }

        if (provider != HostingProvider.AzureDevOps && AzureDevOpsFormAt(run) is >= 0 and var azureDevOps && azureDevOps < limit)
        {
            found = HostingProvider.AzureDevOps;
        }

        return found;
    }

    /// <summary>The first place in <paramref name="run"/> of any of <paramref name="values"/>; -1 for none.</summary>
    private static int FirstOf(ReadOnlySpan<char> run, string[] values)
    {
        var first = -1;
        foreach (var value in values)
        {
            var at = run.IndexOf(value, StringComparison.Ordinal);
            if (at >= 0 && (first < 0 || at < first))
            {
                first = at;
            }
        }

        return first;
    }

    /// <summary>
    /// Where Azure DevOps' form first begins in the run: 76 characters before an <c>AZDO</c> that has at
    /// least 76 letters and digits before it and 4 after it in one stretch of letters and digits; -1 for none.
    /// </summary>
    private static int AzureDevOpsFormAt(ReadOnlySpan<char> run)
    {
        const int After = AzureDevOpsLength - AzureDevOpsMarkIndex - 4;
        var start = 0;
        for (var i = 0; i <= run.Length; i++)
        {
            if (i < run.Length && char.IsAsciiLetterOrDigit(run[i]))
            {
                continue;
            }

            // The stretch [start, i): the mark searched from its 0-based character 76 to 4 before its end.
            if (i - start >= AzureDevOpsLength
                && run[(start + AzureDevOpsMarkIndex)..(i - After)].IndexOf(AzureDevOpsMark, StringComparison.Ordinal) is >= 0 and var mark)
            {
                return start + mark;
            }

            start = i + 1;
        }

        return -1;
    }

    /// <summary>
    /// <paramref name="text"/> with each escape read once: one that stands for a token character (a
    /// letter, a digit, <c>_</c>, <c>-</c> or <c>.</c>) as that character, so that a token escaped
    /// inside its form (<c>glpat%2D</c>, <c>ghp_</c>) is read whole; one that stands for <c>%</c>
    /// or <c>\</c> as that character, so that a value escaped twice is read again; every other one as a
    /// break (a space). The escapes: <c>%</c> and two hexadecimal digits; a backslash and <c>u</c> and
    /// four, <c>U</c> and eight, or <c>x</c> and two hexadecimal digits, one to three octal digits, or
    /// one of <c>a</c>, <c>b</c>, <c>e</c>, <c>f</c>, <c>n</c>, <c>r</c>, <c>t</c> and <c>v</c> (a
    /// control character); a backslash before any other character stands for that character. Read only
    /// to find another provider's token; never sent or shown.
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
            var (code, length) = EscapeAt(text, i);
            if (length == 0)
            {
                read.Append(text[i]);
                continue;
            }

            read.Append(code is >= 0 and < 0x80 && ((char)code is '%' or '\\' || IsTokenCharacter((char)code)) ? (char)code : ' ');
            i += length - 1;
        }

        return read.ToString();
    }

    /// <summary>The code that the escape at <paramref name="at"/> stands for and its length (<see cref="Unescape"/>); length 0 where none begins.</summary>
    private static (long Code, int Length) EscapeAt(string text, int at)
    {
        if (text[at] == '%')
        {
            return HexAt(text, at + 1, 2) is { } percent ? (percent, 3) : (0, 0);
        }

        if (text[at] != '\\' || at + 1 == text.Length)
        {
            return (0, 0);
        }

        var next = text[at + 1];
        switch (next)
        {
            case 'u' when HexAt(text, at + 2, 4) is { } unicode:
                return (unicode, 6);
            case 'U' when HexAt(text, at + 2, 8) is { } wide:
                return (wide, 10);
            case 'x' when HexAt(text, at + 2, 2) is { } hex:
                return (hex, 4);
            case >= '0' and <= '7':
                {
                    var octal = 0;
                    var digits = 0;
                    while (digits < 3 && at + 1 + digits < text.Length && text[at + 1 + digits] is >= '0' and <= '7')
                    {
                        octal = (octal * 8) + (text[at + 1 + digits] - '0');
                        digits++;
                    }

                    return (octal, 1 + digits);
                }

            case 'a' or 'b' or 'e' or 'f' or 'n' or 'r' or 't' or 'v':
                // A control character: a break.
                return (0, 2);
            default:
                return (next, 2);
        }
    }

    /// <summary>The code that the <paramref name="count"/> hexadecimal digits at <paramref name="at"/> give; null if they are not there.</summary>
    private static long? HexAt(string text, int at, int count)
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

        return long.Parse(digits, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A character of some provider's tokens: an ASCII letter or digit, <c>_</c>, <c>-</c> or <c>.</c>.</summary>
    private static bool IsTokenCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.';
}
