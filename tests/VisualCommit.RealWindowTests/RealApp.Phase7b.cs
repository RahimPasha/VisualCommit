using System.Diagnostics;
using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Hosting.Tokens;
using VisualCommit.Testing;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// What phase 7b's real-window pass (Phase7bRealWindowPass*.cs, WP-R) needs of the real app beyond
/// 7a's (docs/phase-7b-design.md 14.3; RealApp.Phase7a.cs is used as it is): the environment that
/// points the app at the three fakes with the memory keychain, the Accounts section's three blocks
/// (D353) reached by the real wheel over <c>SettingsSection</c>, each provider's sign-in dialog opened
/// from its block, Azure DevOps' organization typed, and the sign-in through Settings per provider.
/// No token is ever put in a message: a failure names the place. Every wait is on a condition with a
/// limit, never on a fixed time.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>The settings screen's scrolling section (its <c>SectionScroller</c>), where the real wheel turns.</summary>
    public const string SettingsSectionId = "SettingsSection";

    /// <summary>
    /// The least <c>VerticalScrollPercent</c> that counts as the section's end, its largest offset
    /// (design 10.7, 14.3): 100, less a share of a pixel for the division the scroll pattern makes.
    /// </summary>
    public const double AtEndPercent = 99.95;

    /// <summary>How many notches <see cref="ScrollAccountsTo"/> and <see cref="ScrollAccountsToEnd"/> turn at most before they fail.</summary>
    private const int MaxAccountsNotches = 40;

    /// <summary>
    /// The variables a launch of phase 7b gives the app, applied after the isolation's own (which sets
    /// the three providers off and the memory keychain, design 1.8): <c>VISUALCOMMIT_GITHUB_URL</c>,
    /// <c>VISUALCOMMIT_AZURE_DEVOPS_URL</c> and <c>VISUALCOMMIT_GITLAB_URL</c> each its fake's
    /// <c>http://127.0.0.1:&lt;port&gt;/</c>, and <c>VISUALCOMMIT_KEYCHAIN</c> <c>memory</c>, as
    /// <see cref="HostingEnvironment"/> does for 7a's one fake (design 14.3, 14.2: the three providers
    /// on in every launch, as in every twin).
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Hosting7bEnvironment(ServedHosting7b served)
    {
        ArgumentNullException.ThrowIfNull(served);
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [HostingOptions.GitHubVariable] = WebBase(served.GitHub.Port),
            [HostingOptions.AzureDevOpsVariable] = WebBase(served.AzureDevOps.Port),
            [HostingOptions.GitLabVariable] = WebBase(served.GitLab.Port),
            [TokenStores.Variable] = "memory",
        };
    }

    /// <summary>The suffix of a block's ids (design 1.7): GitHub's block keeps 7a's ids, the others end in the provider.</summary>
    public static string AccountsSuffix(HostingProvider provider) => provider switch
    {
        HostingProvider.GitHub => string.Empty,
        HostingProvider.GitLab => "GitLab",
        HostingProvider.AzureDevOps => "AzureDevOps",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "No such provider."),
    };

    /// <summary>The container of a provider's block in the Accounts section: <c>AccountsGitHub</c>, <c>AccountsGitLab</c>, <c>AccountsAzureDevOps</c> (design 1.7).</summary>
    public static string AccountsBlockId(HostingProvider provider) =>
        "Accounts" + (provider == HostingProvider.GitHub ? "GitHub" : AccountsSuffix(provider));

    /// <summary>
    /// Turns the real wheel over <c>SettingsSection</c>, one notch at a time at its centre, until
    /// <paramref name="provider"/>'s block shows: wholly inside the section's viewport, or, when it is
    /// taller than the viewport, its top inside it (design 14.3). The block is found by its container's
    /// id, or by its "Sign in…" or its login when UI Automation lists no container. Turns at most
    /// <see cref="MaxAccountsNotches"/> notches within <paramref name="timeout"/>.
    /// </summary>
    public void ScrollAccountsTo(HostingProvider provider, TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        for (var notch = 0; ; notch++)
        {
            var section = BoundsOf(SettingsSectionElement());
            var block = BoundsOf(AccountsBlockElement(provider));
            var topInView = block.Top >= section.Top - 0.5f && block.Top <= section.Bottom - 1f;
            var shows = block.Height <= section.Height + 0.5f
                ? block.Top >= section.Top - 0.5f && block.Bottom <= section.Bottom + 0.5f
                : topInView;
            if (shows)
            {
                return;
            }

            if (notch >= MaxAccountsNotches || clock.Elapsed >= timeout)
            {
                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{provider}'s block did not come into the Accounts section's view after {notch} notches of the wheel: it lies at y {block.Top:0.#} to {block.Bottom:0.#}, the section's view at y {section.Top:0.#} to {section.Bottom:0.#}."));
            }

            // Up (a positive notch) while the block lies above the view, down otherwise.
            ScrollWheel(section.X + (section.Width / 2), section.Y + (section.Height / 2), block.Top < section.Top - 0.5f ? 1 : -1);
        }
    }

    /// <summary>
    /// Turns the real wheel down over <c>SettingsSection</c>, one notch at a time at its centre, until
    /// UI Automation's <c>VerticalScrollPercent</c> of the section is 100 (<see cref="AtEndPercent"/>):
    /// the section's end, its largest offset, where part A's three-account picture is taken (design
    /// 10.7, 14.3, 14.4 A). Fails when the section does not scroll, or after
    /// <see cref="MaxAccountsNotches"/> notches or <paramref name="timeout"/>. The caller asserts the
    /// end again (<see cref="SettingsSectionScroll"/>) before the capture.
    /// </summary>
    public void ScrollAccountsToEnd(TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        for (var notch = 0; ; notch++)
        {
            var (scrollable, percent) = SettingsSectionScroll();
            if (!scrollable)
            {
                throw new InvalidOperationException("The Accounts section does not scroll: UI Automation reports SettingsSection as not vertically scrollable.");
            }

            if (percent >= AtEndPercent)
            {
                return;
            }

            if (notch >= MaxAccountsNotches || clock.Elapsed >= timeout)
            {
                throw new TimeoutException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The Accounts section did not reach its end after {notch} notches of the wheel: its VerticalScrollPercent is {percent:0.##}."));
            }

            var section = BoundsOf(SettingsSectionElement());
            ScrollWheel(section.X + (section.Width / 2), section.Y + (section.Height / 2), -1);
        }
    }

    /// <summary>
    /// The settings screen's section as UI Automation's Scroll pattern reports it: whether it scrolls
    /// vertically and its <c>VerticalScrollPercent</c> (0 at the top, 100 at the end). Throws when the
    /// section gives no Scroll pattern.
    /// </summary>
    public (bool Scrollable, double Percent) SettingsSectionScroll()
    {
        var pattern = SettingsSectionElement().Patterns.Scroll.PatternOrDefault
            ?? throw new InvalidOperationException("SettingsSection gives no Scroll pattern to UI Automation.");
        return (pattern.VerticallyScrollable.ValueOrDefault, pattern.VerticalScrollPercent.ValueOrDefault);
    }

    /// <summary>
    /// Opens <paramref name="provider"/>'s sign-in dialog from its block (design 14.3): the Accounts
    /// section first when the settings screen is not open (<see cref="OpenAccountsSectionForSignIn"/>);
    /// then <see cref="ScrollAccountsTo"/>; a real click on <c>AccountsSignInButton&lt;suffix&gt;</c>;
    /// the pointer to the neutral point at once (so that it rests on no link of the dialog); and waits
    /// until one sign-in dialog shows, stacked on the screen (D104), with the keyboard focus in one of
    /// its boxes (<c>SignInToken</c>, or Azure DevOps' <c>SignInOrganization</c> while empty). Returns
    /// its root, named by its title. A settings screen that is open already shows Accounts: the
    /// pass opens it on no other section.
    /// </summary>
    public AutomationElement OpenSignInDialog(HostingProvider provider, TimeSpan timeout)
    {
        if (!IsSettingsShown)
        {
            OpenAccountsSectionForSignIn(timeout);
        }

        ScrollAccountsTo(provider, timeout);
        var id = "AccountsSignInButton" + AccountsSuffix(provider);
        var button = FindAll(id) is [var only]
            ? only
            : throw new InvalidOperationException($"The Accounts section shows {FindAll(id).Count} {id}, not one.");
        ClickWithMouse(button);
        MoveToNeutral();
        WaitForTelling(
            () => ShownElements(SignInDialogId).Count == 1 && (HasFocusOnScreen("SignInToken") || HasFocusOnScreen("SignInOrganization")),
            $"{provider}'s sign-in dialog with the keyboard focus in one of its boxes",
            timeout,
            () => $"{ShownElements(SignInDialogId).Count} sign-in dialogs show; the focus is on {FocusedAutomationId() ?? "nothing"}.");
        return ShownElements(SignInDialogId)[0];
    }

    /// <summary>
    /// The open sign-in dialog's organization box's text (Azure DevOps'; part A's
    /// <c>SignInOrganization</c>). An organization is no secret, but the caller still puts it in a
    /// message only after comparing it with a value of its own.
    /// </summary>
    public string OrganizationBoxText() => FindIn(OpenSignInDialogRoot(), "SignInOrganization").AsTextBox().Text;

    /// <summary>
    /// Types <paramref name="organization"/> into the open dialog's organization box as part A's real
    /// steps give it (design 14.3): a real click on <c>SignInOrganization</c>, Ctrl+A on the real
    /// keyboard, the organization typed; then waits until the box's value reads it back.
    /// </summary>
    public void TypeOrganization(string organization)
    {
        ArgumentException.ThrowIfNullOrEmpty(organization);
        ClickWithMouse(FindIn(OpenSignInDialogRoot(), "SignInOrganization"));
        PressKeys(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        TypeText(organization);

        // The box's text is not printed: it is compared with the organization alone.
        WaitForTelling(
            () => OrganizationBoxText() == organization,
            $"the organization box to read \"{organization}\"",
            DefaultWaitTimeout,
            () => string.Create(CultureInfo.InvariantCulture, $"It holds {OrganizationBoxText().Length} characters, not the organization."));
    }

    /// <summary>
    /// Waits until <paramref name="provider"/>'s block names <paramref name="login"/>
    /// (<c>AccountLogin&lt;suffix&gt;</c>, in the server's spelling: <c>Fernhill-Labs</c> for Azure
    /// DevOps whatever case was typed). The element is found whether or not it is scrolled out of the
    /// section's view; a time-out's message names no text the block reads.
    /// </summary>
    public void WaitForAccountLogin(HostingProvider provider, string login, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(login);
        var id = "AccountLogin" + AccountsSuffix(provider);
        WaitFor(() => FindAll(id).Any(element => element.Name == login), $"{id} to read \"{login}\"", timeout);
    }

    /// <summary>
    /// Signs in to <paramref name="provider"/> through Settings, Accounts, as the R steps do (design
    /// 14.3): <see cref="OpenSignInDialog(HostingProvider, TimeSpan)"/>; for Azure DevOps the focus
    /// checked against part A's (<c>SignInOrganization</c> while the box is empty, <c>SignInToken</c>
    /// once prefilled from the tab's remote, ruling 16) and <paramref name="organization"/> typed unless
    /// the box holds it already (null: it must be prefilled); a real click on <c>SignInToken</c>;
    /// <see cref="TypeToken"/> (masked); <see cref="ConfirmSignIn"/>; waits until the block names
    /// <paramref name="login"/>; then Esc closes the screen unless <paramref name="keepOpen"/> (the
    /// sign-in stays, D256).
    /// </summary>
    public void SignInThroughSettings(HostingProvider provider, string token, string? organization, string login, TimeSpan timeout, bool keepOpen = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentException.ThrowIfNullOrEmpty(login);
        var dialog = OpenSignInDialog(provider, timeout);
        if (provider == HostingProvider.AzureDevOps && organization is null)
        {
            // Prefilled from the tab's remote (ruling 16): the box holds it and the focus is in the
            // token box (10.4, part A's FocusOpened); waited for, should the prefill come a moment
            // after the dialog shows.
            WaitForTelling(
                () => OrganizationBoxText().Length > 0 && HasFocusOnScreen(PartA.FocusOpened),
                $"Azure DevOps' dialog prefilled from the tab's remote with the keyboard focus on {PartA.FocusOpened} (ruling 16, design 10.4)",
                DefaultWaitTimeout,
                () => string.Create(CultureInfo.InvariantCulture, $"The organization box holds {OrganizationBoxText().Length} characters; the focus is on {FocusedAutomationId() ?? "nothing"}."));
        }
        else if (provider == HostingProvider.AzureDevOps && OrganizationBoxText() != organization)
        {
            // Empty on an empty tab: the focus in the organization box (10.4, part A's FocusOpenedEmpty).
            if (OrganizationBoxText().Length == 0 && !HasFocusOnScreen(PartA.FocusOpenedEmpty))
            {
                throw new InvalidOperationException($"Azure DevOps' dialog opened empty with the keyboard focus on {FocusedAutomationId() ?? "nothing"}, not on {PartA.FocusOpenedEmpty} (design 10.4).");
            }

            TypeOrganization(organization!);
        }

        ClickWithMouse(FindIn(dialog, "SignInToken"));
        WaitFor(() => HasFocusOnScreen("SignInToken"), "the keyboard focus in the token box after a click on it", DefaultWaitTimeout);
        TypeToken(token);
        ConfirmSignIn(token, timeout);
        WaitForAccountLogin(provider, login, timeout);
        if (!keepOpen)
        {
            PressKey(VirtualKeyShort.ESCAPE);
            WaitUntilGone(SettingsDialogId, timeout);
        }
    }

    /// <summary>
    /// Opens the settings screen on Accounts for a sign-in (7a's <see cref="OpenAccountsSection(TimeSpan)"/>):
    /// a real click at the neutral point, a real Ctrl+, and a click on Accounts. On a repository's tab
    /// it first waits, between the click and the key, until the keyboard focus is on the window itself
    /// (7a's <see cref="SignInThroughSettings(string, string, TimeSpan)"/>, its open point 30): the
    /// screen gives the focus back on Esc to what had it. On an empty tab, whose welcome page gives its
    /// own box the focus, it does not wait (7a's part A).
    /// </summary>
    private void OpenAccountsSectionForSignIn(TimeSpan timeout)
    {
        if (ShownElements("CloneRepoButton").Count == 1)
        {
            OpenAccountsSection(timeout);
            return;
        }

        OpenAccountsSection(
            timeout,
            afterClick: () => WaitForTelling(
                FocusIsOnTheWindow,
                "the keyboard focus on the window itself, on none of its controls, after the click at the neutral point (the twin's state when the screen opens, D104, 7a's open point 30)",
                DefaultWaitTimeout,
                () => $"The focus is on {FocusedAutomationId() switch { null or "" => "an element without an automation id", var focused => focused }}."));
    }

    /// <summary>The settings screen's section, <c>SettingsSection</c>; throws when there is not exactly one.</summary>
    private AutomationElement SettingsSectionElement() =>
        FindAll(SettingsSectionId) is [var only]
            ? only
            : throw new InvalidOperationException($"The window holds {FindAll(SettingsSectionId).Count} {SettingsSectionId}, not one: the settings screen is not open.");

    /// <summary>
    /// <paramref name="provider"/>'s block in the Accounts section: its container
    /// (<see cref="AccountsBlockId"/>), or, when UI Automation lists none, its "Sign in…" or its login,
    /// whichever it shows.
    /// </summary>
    private AutomationElement AccountsBlockElement(HostingProvider provider)
    {
        var suffix = AccountsSuffix(provider);
        string[] ids = [AccountsBlockId(provider), "AccountsSignInButton" + suffix, "AccountLogin" + suffix];
        foreach (var id in ids)
        {
            if (FindAll(id) is [var only])
            {
                return only;
            }
        }

        throw new InvalidOperationException($"The Accounts section shows no single {string.Join(", ", ids)}.");
    }
}
