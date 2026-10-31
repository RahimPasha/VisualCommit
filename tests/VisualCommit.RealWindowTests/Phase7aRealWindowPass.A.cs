// Part A's real-window steps (docs/test-reports/phase-7a.md, part A, the R steps in the Notes of A1,
// A2 and A5; docs/phase-7a-design.md 13.4 A): one empty tab served by the fake of GitHub, the settings
// screen's Accounts section signed out by a real Ctrl+, and a click, the "Add GitHub account" dialog
// with the token typed on the real keyboard and masked to UI Automation, the sign-in with the memory
// keychain, and accounts.json after Esc. No preparation (13.4).
using VisualCommit.Core;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.RealWindowTests;

public partial class Phase7aRealWindowPass
{
    /// <summary>
    /// Part A (13.4 A steps 1 to 5): the launch asks GitHub nothing (D257); the section signed out with
    /// its five nav buttons (<c>a01a-signed-out</c>); the dialog with the typed token masked
    /// (<c>a02b-token-masked</c>); "Sign in" makes exactly one <c>GET api/v3/user</c> and shows the
    /// signed-in row (<c>a05a-signed-in</c>); Esc closes the screen and <c>accounts.json</c> names octo.
    /// </summary>
    [Fact]
    public async Task Check_A_accounts()
    {
        using var hello = await Scenarios.HostingAsync();
        await using var served = await Scenarios.ServeHostingAsync(hello, ct: TestContext.Current.CancellationToken);
        using var app = LaunchServed(served, emptyTab: true);
        var port = served.GitHub.Port;

        // Step 1: the launch makes no request (D257).
        AssertNoRequests(Recorded(served), "The launch asked GitHub (D257)");

        // Step 2 (A1): a real Ctrl+, then a click on Accounts.
        app.OpenAccountsSection(StepTimeout);
        var nav = PartA.NavIds.Select(id => app.ShownElements(id) is [var button] ? button : throw new InvalidOperationException($"The settings screen shows no single {id}."))
            .OrderBy(button => app.BoundsOf(button).Top)
            .Select(button => button.Name)
            .ToList();
        Assert.Equal(PartA.NavButtons, nav);
        Assert.Equal("Accounts", app.ShownSettingsSection());
        app.WaitFor(() => app.HasFocusOnScreen("SettingsNavAccounts"), "the keyboard focus on Accounts' button", StepTimeout);
        app.WaitForText("AccountsIntro", PartA.Intro, StepTimeout);
        app.WaitForText("AccountsSignedOut", PartA.SignedOut, StepTimeout);
        var signIn = app.Find("AccountsSignInButton");
        Assert.Equal((PartA.SignInButton, true), (signIn.Name, signIn.IsEnabled));
        Assert.Empty(app.ShownElements("AccountLogin"));
        AssertNoRequests(Recorded(served), "Opening the Accounts section asked GitHub");
        Compare(app, served, PartA.A01SignedOut);

        // Step 3 (A2): "Add GitHub account" over the screen, the token typed and masked.
        var dialog = app.OpenSignInDialog(StepTimeout);
        Assert.Equal(PartA.DialogTitle, dialog.Name);
        Assert.True(app.IsSettingsShown, "The settings screen is gone behind the sign-in dialog (D104).");
        app.TypeToken(served.OctoToken);
        foreach (var text in app.Texts())
        {
            AssertNoToken(served, text, "A text element of the window");
        }

        AssertNoRequests(Recorded(served), "Typing the token asked GitHub");
        Compare(app, served, PartA.A02TokenMasked);

        // Step 4 (A5): "Sign in": exactly one request, GET api/v3/user with the token, 200.
        var requests = served.GitHub.Requests.Count;
        app.ConfirmSignIn(served.OctoToken, NetworkTimeout);
        WaitForFake(served, requests, request => request.Route == "user", "GET api/v3/user", NetworkTimeout);
        Assert.Equal(
            [("GET", "/api/v3/user", "user", true, OctoLogin, 200)],
            Recorded(served, requests).Select(request => (request.Method, request.Path, request.Route, request.TokenPresent, request.Login, request.Status)));
        app.WaitForText("AccountLogin", OctoLogin, StepTimeout);
        app.WaitForText("AccountTokenPlace", PartA.InKeychain, StepTimeout);
        Assert.True(RealApp.IsOn(app.Find("AccountUseForGit")), "\"Use for git over HTTPS\" is off after a first sign-in (D255).");
        Assert.Empty(app.ShownElements("AccountsSignedOut"));
        app.WaitFor(() => app.HasFocusOnScreen("AccountsSignOutButton"), "the keyboard focus on Sign out… (8.3)", StepTimeout);
        Compare(app, served, PartA.A05SignedIn);

        // Step 5: Esc closes the screen; the sign-in stays (D256) and accounts.json names octo.
        app.PressKey(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        app.WaitUntilGone(RealApp.SettingsDialogId, StepTimeout);
        WaitTelling(
            app,
            () => ReadSavedAccount(app.DataDirectory, served) is { Version: 1, Provider: "github", Login: OctoLogin, UseForGit: true, TokenState: "keychain" } saved && saved.Host == PartA.Host(port),
            "accounts.json to name octo on the fake's host with useForGit true",
            () => $"It holds: {ReadSavedAccount(app.DataDirectory, served) switch { null => "no single record", { HoldsAToken: true } => "a token (its fields are not printed)", var saved => $"version {saved.Version}, {saved.Provider}, {saved.Host}, {saved.Login}, useForGit {saved.UseForGit}, {saved.TokenState}" }}.");
        Assert.False(ReadSavedAccount(app.DataDirectory, served)!.HoldsAToken, $"{new AppPaths(app.DataDirectory).AccountsFile} holds a token.");
        Assert.Equal([("GET", "user")], Recorded(served).Select(request => (request.Method, request.Route ?? string.Empty)));
        AssertCleanEnd(app, served);
    }
}
