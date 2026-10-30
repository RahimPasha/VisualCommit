using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using VisualCommit.Git.Prompts;
using VisualCommit.Hosting;
using VisualCommit.Hosting.Tokens;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.RealWindowTests;

/// <summary>
/// What phase 7a's real-window pass (Phase7aRealWindowPass*.cs, WP-R) needs of the real app
/// (docs/phase-7a-design.md 13.3): the environment that points the app at the fake of GitHub with
/// the memory keychain, the askpass helper beside the app, the Accounts section opened by a real
/// Ctrl+, and a click, the "Add GitHub account" dialog with its masked token box, the sign-in
/// through Settings, and the reads of the new screens (the repository browser's rows, the left
/// panel's pull requests). No token is ever put in a message: a failure names the place. Every
/// wait is on a condition with a limit, never on a fixed time.
/// </summary>
public sealed partial class RealApp
{
    /// <summary>"Add GitHub account"'s root (a pane named by its title, design 8.4).</summary>
    public const string SignInDialogId = "SignInDialog";

    /// <summary>The repository browser's root ("Clone from GitHub", design 9.4).</summary>
    public const string CloneFromGitHubDialogId = "CloneFromGitHubDialog";

    /// <summary>The create form's root ("Create pull request", design 12.4).</summary>
    public const string CreatePullRequestDialogId = "CreatePullRequestDialog";

    /// <summary>
    /// The askpass helper the app runs beside its executable (<c>visualcommit-askpass.exe</c>,
    /// <see cref="AskPassLocator.FileName"/>): the program git runs, through Git for Windows'
    /// <c>sh</c>, as the app's credential helper (design 0.2, 4.5, 13.1). Computed on each read, not in
    /// a field's initializer: <see cref="Executable"/> is initialised in another file of this partial
    /// class, and the order of the two would be undefined.
    /// </summary>
    public static string HelperPath => Path.Combine(Path.GetDirectoryName(Executable)!, AskPassLocator.FileName);

    /// <summary>
    /// The variables a launch of phase 7a gives the app, applied after the isolation's own
    /// (<see cref="Launch(string, IReadOnlyDictionary{string, string?}?)"/>, which sets GitHub off and
    /// the memory keychain, ruling 24): <c>VISUALCOMMIT_GITHUB_URL</c> the fake's
    /// <c>http://127.0.0.1:&lt;port&gt;/</c> and <c>VISUALCOMMIT_KEYCHAIN</c> <c>memory</c>, so the app
    /// keeps the token in memory, as a keychain (design 1.8, 13.1).
    /// </summary>
    public static IReadOnlyDictionary<string, string?> HostingEnvironment(int port) => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        [HostingOptions.GitHubVariable] = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"),
        [TokenStores.Variable] = "memory",
    };

    /// <summary>The app's exit code once it has exited (<see cref="Close"/> returned it), or null while it runs.</summary>
    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    /// <summary>
    /// Opens the settings screen on Accounts as the scripted run's <c>OpenAccountsAsync</c> does: a
    /// real click at the neutral point (the status bar's empty middle, where nothing reacts), which
    /// gives the window the activation so that the key goes to it, then a real Ctrl+, , the screen
    /// on General, then a real click on Accounts, and waits until the section shows.
    /// </summary>
    public void OpenAccountsSection(TimeSpan timeout) => OpenAccountsSection(timeout, afterClick: null);

    /// <summary><see cref="OpenAccountsSection(TimeSpan)"/>, running <paramref name="afterClick"/> between the click at the neutral point and the key.</summary>
    private void OpenAccountsSection(TimeSpan timeout, Action? afterClick)
    {
        ClickAt(NeutralPoint.X, NeutralPoint.Y);
        afterClick?.Invoke();
        PressSettingsKey();
        WaitForSettings(timeout);
        WaitForSettingsSection("General", timeout);
        ChooseSettingsSection("Accounts", timeout);
    }

    /// <summary>
    /// A real click on the Accounts section's "Sign in…", the pointer moved to the neutral point
    /// at once (so that it rests on no link of the dialog), and waits for the dialog
    /// (<see cref="WaitForSignInDialog"/>).
    /// </summary>
    public AutomationElement OpenSignInDialog(TimeSpan timeout)
    {
        ClickWithMouse(Find("AccountsSignInButton"));
        MoveToNeutral();
        return WaitForSignInDialog(timeout);
    }

    /// <summary>
    /// Waits until "Add GitHub account" shows, stacked on what opened it (D104), with the keyboard
    /// focus in its token box (<c>SignInToken</c>), and returns its root.
    /// </summary>
    public AutomationElement WaitForSignInDialog(TimeSpan timeout)
    {
        WaitForTelling(
            () => ShownElements(SignInDialogId).Count == 1 && HasFocusOnScreen("SignInToken"),
            "\"Add GitHub account\" with the keyboard focus in its token box",
            timeout,
            () => $"{ShownElements(SignInDialogId).Count} sign-in dialogs show; the focus is on {FocusedAutomationId() ?? "nothing"}.");
        return ShownElements(SignInDialogId)[0];
    }

    /// <summary>The open sign-in dialog's root; throws when not exactly one shows.</summary>
    private AutomationElement OpenSignInDialogRoot() =>
        ShownElements(SignInDialogId) is [var dialog]
            ? dialog
            : throw new InvalidOperationException($"{ShownElements(SignInDialogId).Count} sign-in dialogs show, not one.");

    /// <summary>
    /// The text box <paramref name="automationId"/>'s value through its Value pattern: empty while a
    /// <c>SecretTextBox</c> is masked (<c>src/VisualCommit.App/Controls/SecretTextBox.cs</c>, D103,
    /// D130). The caller tests it with <see cref="string.Length"/> or <see cref="string.Contains(string)"/>
    /// and never passes it to an assertion: it could hold a token.
    /// </summary>
    public string SecretBoxValue(string automationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(automationId);
        return Find(automationId).Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? string.Empty;
    }

    /// <summary>
    /// Types <paramref name="token"/> on the real keyboard into the open dialog's token box, which has
    /// the keyboard focus (<see cref="WaitForSignInDialog"/>), then throws unless the box is named
    /// <c>Token</c> (<see cref="PartA.TokenLabel"/>) and its value reads empty to UI Automation
    /// (<see cref="SecretBoxValue"/>). No message holds the token.
    /// </summary>
    public void TypeToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        if (!HasFocusOnScreen("SignInToken"))
        {
            throw new InvalidOperationException($"The token box does not have the keyboard focus; {FocusedAutomationId() ?? "nothing"} has it.");
        }

        TypeText(token);

        // "Sign in" is enabled once the box holds a token (8.4): the typing has reached the box.
        WaitFor(() => FindIn(OpenSignInDialogRoot(), "ConfirmButton").IsEnabled, "the dialog's Sign in to be enabled by the typed token", DefaultWaitTimeout);
        var box = FindIn(OpenSignInDialogRoot(), "SignInToken");
        if (box.Name != PartA.TokenLabel)
        {
            throw new InvalidOperationException($"The token box is named \"{WithoutToken(box.Name, token, "The token box's name")}\", not \"{PartA.TokenLabel}\".");
        }

        if (SecretBoxValue("SignInToken").Length != 0)
        {
            throw new InvalidOperationException("The token box's Value pattern is not empty while it is masked (D103).");
        }
    }

    /// <summary>
    /// <paramref name="text"/>, read from the app, for an exception's message, after checking that it
    /// does not hold <paramref name="token"/>, the token typed into the dialog: when it does, throws
    /// naming <paramref name="place"/> instead, so that no message holds the token and the leak fails
    /// by its place (design 13.2).
    /// </summary>
    private static string WithoutToken(string text, string token, string place) =>
        text.Contains(token, StringComparison.Ordinal) ? throw new InvalidOperationException($"{place} holds the typed token.") : text;

    /// <summary>
    /// A real click on the open dialog's own "Sign in" (<c>ConfirmButton</c>, found inside the dialog:
    /// the settings screen below has one too), named <c>Sign in</c> (<see cref="PartA.SignIn"/>) and
    /// enabled, and waits until the dialog has gone: the token checked out and kept. A time-out's
    /// message gives the dialog's error and notes, each checked first for <paramref name="token"/>, the
    /// token typed (<see cref="WithoutToken"/>).
    /// </summary>
    public void ConfirmSignIn(string token, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        var confirm = FindIn(OpenSignInDialogRoot(), "ConfirmButton");
        if (confirm.Name != PartA.SignIn || !confirm.IsEnabled)
        {
            throw new InvalidOperationException($"The dialog's confirm button is \"{confirm.Name}\" and {(confirm.IsEnabled ? "enabled" : "disabled")}; \"{PartA.SignIn}\", enabled, was expected.");
        }

        ClickWithMouse(confirm);

        // Off the screen below at once: rows and links with a tooltip or a pointer-over look may come
        // under the pointer when the dialog goes.
        MoveToNeutral();
        WaitForTelling(
            () => ShownElements(SignInDialogId).Count == 0,
            "\"Add GitHub account\" to close after the sign-in",
            timeout,
            () => $"It still shows: {WithoutToken(string.Join(" | ", NamesOnScreen("SignInError").Concat(NamesOnScreen("SignInSaveFailed")).Concat(NamesOnScreen("SignInChecking"))), token, "The dialog's error or note")}.");
    }

    /// <summary>
    /// <see cref="TypeToken"/> then <see cref="ConfirmSignIn"/> (design 13.3): the token typed and
    /// masked, the dialog's own "Sign in" clicked, the dialog gone.
    /// </summary>
    public void TypeTokenAndConfirm(string token, TimeSpan timeout)
    {
        TypeToken(token);
        ConfirmSignIn(token, timeout);
    }

    /// <summary>
    /// Signs in through Settings as the scripted R twins do (design 13.3, the report's open point 30):
    /// <see cref="OpenAccountsSection(TimeSpan)"/>, "Sign in…", <see cref="TypeTokenAndConfirm"/>, waits until
    /// <c>AccountLogin</c> reads <paramref name="login"/>, then Esc closes the screen; the sign-in stays
    /// (D256).
    /// <para>
    /// Between the click at the neutral point and the key, waits until the keyboard focus is on the
    /// window itself, on none of its controls (<see cref="FocusIsOnTheWindow"/>): the screen gives the
    /// focus back on Esc to what had it when it opened (D104, <c>MainWindow.OnDialogRemoving</c>). The
    /// twin opens the screen by the key alone (<c>SettingsOpening.Key</c>), with the focus where the
    /// launch left it, and no view of a repository's tab takes the focus when it opens; the click here
    /// comes before the key, so a click that gave a control the focus would have that control given
    /// the focus back, and the pictures after it (p06a first) taken in another state than their
    /// twins' (open point 30), seen only as a difference in pixels. This fails by name instead. For a
    /// launch on hello only: an empty tab's welcome page gives its URL box the focus
    /// (<c>WelcomeView.axaml.cs</c>).
    /// </para>
    /// </summary>
    public void SignInThroughSettings(string token, string login, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(login);
        OpenAccountsSection(
            timeout,
            afterClick: () => WaitForTelling(
                FocusIsOnTheWindow,
                "the keyboard focus on the window itself, on none of its controls, after the click at the neutral point (the twin's state when the screen opens, D104, open point 30)",
                DefaultWaitTimeout,
                () => $"The focus is on {FocusedAutomationId() switch { null or "" => "an element without an automation id", var id => id }}."));
        OpenSignInDialog(timeout);
        TypeTokenAndConfirm(token, timeout);
        WaitForText("AccountLogin", login, timeout);
        PressKey(VirtualKeyShort.ESCAPE);
        WaitUntilGone(SettingsDialogId, timeout);
    }

    /// <summary>
    /// Whether UI Automation's focused element is the window itself, on none of its controls: what it
    /// reports for the foreground window while the app's own focus is on the window or on nothing in
    /// it. False when it cannot be read, and while another window has the focus.
    /// </summary>
    public bool FocusIsOnTheWindow()
    {
        try
        {
            return _automation.FocusedElement() is { } focused
                && (focused.Equals(Window) || focused.Properties.NativeWindowHandle.ValueOrDefault == Handle);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or FlaUI.Core.Exceptions.ElementNotAvailableException)
        {
            return false;
        }
    }

    /// <summary>The repository browser's rows (<c>CloneFromGitHubItem</c>, named by the full name), top to bottom.</summary>
    public IReadOnlyList<string> CloneBrowserRowNames() => NamesOnScreen("CloneFromGitHubItem");

    /// <summary>The repository browser's row named <paramref name="fullName"/>; throws when there is not exactly one on screen.</summary>
    public AutomationElement CloneBrowserRow(string fullName) =>
        ShownElements("CloneFromGitHubItem").Where(row => row.Name == fullName).ToList() switch
        {
            [var only] => only,
            var found => throw new InvalidOperationException($"The browser shows {found.Count} rows \"{fullName}\"; it shows: {string.Join(" | ", CloneBrowserRowNames())}."),
        };

    /// <summary>The left panel's pull requests (<c>PullRequestItem</c> in <c>RefList</c>, named <c>#n title</c>) on screen, top to bottom.</summary>
    private IReadOnlyList<AutomationElement> PullRequestRows() =>
        [.. Find("RefList").FindAllDescendants(condition => condition.ByAutomationId("PullRequestItem"))
            .Where(row => !row.IsOffscreen && row.BoundingRectangle is { Width: > 0, Height: > 0 })
            .OrderBy(row => row.BoundingRectangle.Top)];

    /// <summary>The names of the left panel's pull requests, top to bottom (<c>#5 Fix a typo in the greeting</c>).</summary>
    public IReadOnlyList<string> PullRequestRowNames() => [.. PullRequestRows().Select(row => row.Name)];

    /// <summary>
    /// The left panel's pull request named <paramref name="name"/> (<c>#1 Add the login form</c>).
    /// <see cref="RefRow"/> finds <c>RefItem</c> and <c>Section</c> elements only, so the section's header
    /// is <c>RefRow("Pull requests")</c> and its rows come from here (design 13.3).
    /// </summary>
    public AutomationElement PullRequestRow(string name) =>
        PullRequestRows().Where(row => row.Name == name).ToList() switch
        {
            [var only] => only,
            var found => throw new InvalidOperationException($"The left panel shows {found.Count} pull requests \"{name}\"; it shows: {string.Join(" | ", PullRequestRowNames())}."),
        };

    /// <summary>
    /// Whether the pull request row <paramref name="row"/> shows its <c>Draft</c> mark: a text element
    /// inside it, on screen, named <c>Draft</c> (<see cref="PartP.Draft"/>). The mark's TextBlock has a
    /// name (<c>PullRequestDraft</c>) but no automation id of its own, so it is found by its text; while
    /// hidden it has no place on screen.
    /// </summary>
    public static bool ShowsDraftMark(AutomationElement row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.FindAllDescendants(condition => condition.ByControlType(FlaUI.Core.Definitions.ControlType.Text))
            .Any(text => text.Name == PartP.Draft && !text.IsOffscreen && text.BoundingRectangle is { Width: > 0, Height: > 0 });
    }
}
