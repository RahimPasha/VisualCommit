using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>Part A's own helpers (docs/test-reports/phase-7a.md, part A's conventions).</summary>
public sealed partial class Phase7aChecksA
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>One empty tab with the fake as GitHub (<c>gitHub:</c>) and <paramref name="tokens"/> or a new store.</summary>
    private static ShellDriver StartEmpty(TempDirectory data, ServedHosting served, FakeTokenStore? tokens = null, int width = 1100, int height = 700)
    {
        var app = ShellDriver.Start(data.Path, width, height, gitHub: served.GitHub, tokens: tokens);
        app.MoveToNeutral();
        return app;
    }

    /// <summary>
    /// A picture of part A: before it, no visible text, automation value or name in the window holds
    /// any of part A's tokens; after it, no text is cut (part A's conventions, "No token on screen").
    /// </summary>
    private static Screenshot ShotA(ShellDriver app, string name, bool keepPointer = false)
    {
        AssertNoTokenOnScreen(app);
        var screenshot = Shot(app, name, keepPointer);
        AssertNoClippedText(app);
        return screenshot;
    }

    /// <summary>No visible text, automation value or automation name in the window holds a token; a failure names the control and the token's constant, never the token.</summary>
    private static void AssertNoTokenOnScreen(ShellDriver app)
    {
        app.Settle();
        foreach (var control in app.Window.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible))
        {
            var texts = new List<(string What, string? Text)>
            {
                ("automation name", AutomationProperties.GetName(control)),
                ("automation help text", AutomationProperties.GetHelpText(control)),
                ("text", control is TextBlock block ? block.Text : null),
                ("text", control is TextBox box and not SecretTextBox ? box.Text : null),
                ("automation value", control is TextBox valueBox ? ValueOf(valueBox) : null),
            };
            foreach (var (what, text) in texts)
            {
                if (text is null)
                {
                    continue;
                }

                foreach (var (tokenName, token) in PartA.Tokens)
                {
                    Assert.False(
                        text.Contains(token, StringComparison.Ordinal),
                        $"The {what} of {AutomationProperties.GetAutomationId(control) ?? control.Name ?? control.GetType().Name} holds {tokenName}.");
                }
            }
        }
    }

    /// <summary>A text box's value as UI Automation reads it: empty for a masked box (D103, D130).</summary>
    private static string? ValueOf(TextBox box) =>
        ControlAutomationPeer.CreatePeerForElement(box).GetProvider<IValueProvider>()?.Value;

    /// <summary>
    /// Every method's end, after its app has closed (design 16.3): the tripwire for part A's five
    /// tokens over the data folder and the copy's repositories with each start's call log; the fake's
    /// errors and the harness's refused requests empty; no API request answered 400 or 415; the
    /// launcher's requests only those the steps name.
    /// </summary>
    private static void AssertCleanEnd(IReadOnlyList<ShellDriver> apps, TempDirectory data, ServedHosting served, int launches = 0)
    {
        string[] folders = [data.Path, served.Hello.Path, served.Hello.SiblingPath(Tool)];
        foreach (var app in apps)
        {
            foreach (var (_, token) in PartA.Tokens)
            {
                TokenTripwire.AssertAbsent(token, folders, app.Session.GitCalls);
            }

            Assert.Equal(0, app.RefusedRequests.Count);
        }

        Assert.Empty(served.GitHub.Errors);
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
        Assert.Equal(launches, apps.Sum(app => app.Launcher.Requests.Count));
    }

    /// <summary>The fake's API requests since <paramref name="mark"/> (<c>Requests.Count</c> read before the step).</summary>
    private static List<FakeGitHubRequest> RequestsSince(ServedHosting served, int mark) => [.. served.GitHub.Requests.Skip(mark)];

    /// <summary>The store's calls since <paramref name="mark"/> (<c>Calls.Count</c> read before the step).</summary>
    private static List<string> StoreCallsSince(ShellDriver app, int mark) => [.. app.Tokens.Calls.Skip(mark)];

    /// <summary>Asserts that <paramref name="requests"/> is exactly one <c>GET api/v3/user</c> with a token, answered <paramref name="status"/>.</summary>
    private static void AssertOneUserRequest(IReadOnlyList<FakeGitHubRequest> requests, int status, string? login = OctoLogin)
    {
        var request = Assert.Single(requests);
        AssertUserRequest(request, status, login);
    }

    private static void AssertUserRequest(FakeGitHubRequest request, int status, string? login = OctoLogin)
    {
        Assert.Equal(("GET", "/api/v3/user", "user"), (request.Method, request.Path, request.Route));
        Assert.True(request.TokenPresent, "The sign-in's request carried no token.");

        // A later check of a token GitHub answered before is conditional (WP-G's kept answer and its
        // ETag, design 6.2, 16.10): the fake's 304 is GitHub's 200 for it.
        Assert.True(
            request.Status == status || (status == 200 && request.Status == 304 && request.Conditional),
            $"GET /api/v3/user was answered {request.Status} (conditional: {request.Conditional}), expected {status}.");
        if (status == 200)
        {
            Assert.Equal(login, request.Login);
        }
    }

    /// <summary>The key octo's token is kept under, in the store.</summary>
    private static VisualCommit.Hosting.Tokens.TokenKey OctoKey(ServedHosting served) =>
        new("github", PartA.Host(served.GitHub.Port), OctoLogin);

    private static string AccountsFile(TempDirectory data) => Path.Combine(data.Path, "accounts.json");

    /// <summary>The parsed <c>accounts.json</c>, or null when there is none (or it is being replaced).</summary>
    private static JsonElement? ReadAccounts(TempDirectory data)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(AccountsFile(data)));
            return document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The one record of <c>accounts.json</c>, or null when there is no file or no record.</summary>
    private static JsonElement? RecordOf(JsonElement? file) =>
        file is { } root && root.TryGetProperty("accounts", out var accounts) && accounts.GetArrayLength() == 1 ? accounts[0] : null;

    /// <summary>Waits until the parsed <c>accounts.json</c>'s record shows <paramref name="useForGit"/> and <paramref name="tokenState"/> (written off the UI thread).</summary>
    private static async Task WaitForRecordAsync(ShellDriver app, TempDirectory data, bool useForGit, string tokenState) =>
        await app.WaitForAsync(
            () => RecordOf(ReadAccounts(data)) is { } record
                && record.TryGetProperty("useForGit", out var use) && use.ValueKind == (useForGit ? JsonValueKind.True : JsonValueKind.False)
                && record.TryGetProperty("tokenState", out var state) && state.GetString() == tokenState,
            $"accounts.json to show useForGit {useForGit} and tokenState {tokenState}");

    /// <summary>
    /// Asserts <c>accounts.json</c>: <c>version</c> 1 and one record with exactly the scenario's
    /// eight fields (design 7.2, D336), with <paramref name="useForGit"/> and <paramref name="tokenState"/>.
    /// </summary>
    private static void AssertRecord(TempDirectory data, ServedHosting served, bool useForGit, string tokenState)
    {
        var file = ReadAccounts(data) ?? throw new Xunit.Sdk.XunitException("accounts.json could not be read.");
        Assert.Equal(1, file.GetProperty("version").GetInt32());
        var record = RecordOf(file) ?? throw new Xunit.Sdk.XunitException("accounts.json holds no single record.");
        Assert.Equal(
            ["avatarUrl", "host", "id", "login", "name", "provider", "tokenState", "useForGit"],
            record.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        var port = served.GitHub.Port;
        Assert.Equal("github", record.GetProperty("provider").GetString());
        Assert.Equal(PartA.Host(port), record.GetProperty("host").GetString());
        Assert.Equal(OctoLogin, record.GetProperty("login").GetString());
        Assert.Equal(OctoId, record.GetProperty("id").GetInt64());
        Assert.Equal(OctoName, record.GetProperty("name").GetString());
        Assert.Equal(PartA.AvatarUrl(port), record.GetProperty("avatarUrl").GetString());
        Assert.Equal(useForGit, record.GetProperty("useForGit").GetBoolean());
        Assert.Equal(tokenState, record.GetProperty("tokenState").GetString());
    }

    /// <summary>Asserts <c>accounts.json</c> after a sign-out: <c>version</c> 1 and no record (7.5, open point 10).</summary>
    private static void AssertNoRecord(TempDirectory data)
    {
        var file = ReadAccounts(data) ?? throw new Xunit.Sdk.XunitException("accounts.json could not be read.");
        Assert.Equal(1, file.GetProperty("version").GetInt32());
        Assert.Equal(0, file.TryGetProperty("accounts", out var accounts) ? accounts.GetArrayLength() : 0);
    }

    /// <summary>The automation id of the focused control or of its nearest ancestor with one.</summary>
    private static string? FocusedId(ShellDriver app)
    {
        app.Settle();
        return app.Window.FocusManager?.GetFocusedElement() is Control focused
            ? focused.GetSelfAndVisualAncestors().OfType<Control>().Select(AutomationProperties.GetAutomationId).FirstOrDefault(id => id is { Length: > 0 })
            : null;
    }

    /// <summary>The control <paramref name="id"/> inside the settings screen or the sign-in dialog (the topmost drawn).</summary>
    private static Control Find(ShellDriver app, Visual scope, string id) => app.FindByAutomationId(scope, id);

    /// <summary>The named border (a dialog's card) in the window.</summary>
    private static Border CardNamed(ShellDriver app, string name) =>
        app.Window.GetVisualDescendants().OfType<Border>().First(border => border.Name == name && border.IsEffectivelyVisible);

    /// <summary>Asserts a rectangle's left and top within the part's tolerance.</summary>
    private static void AssertAt(Rect bounds, double x, double y, string what)
    {
        Assert.True(
            Math.Abs(bounds.X - x) <= PartA.Tolerance && Math.Abs(bounds.Y - y) <= PartA.Tolerance,
            $"{what} is at ({bounds.X}, {bounds.Y}), expected ({x}, {y}) ±{PartA.Tolerance}.");
    }

    /// <summary>A text block's colour.</summary>
    private static Color? ColourOf(TextBlock block) => (block.Foreground as ISolidColorBrush)?.Color;

    /// <summary>
    /// Asserts the settings card's frame and section list at 1100×700 or 1920×1080: the card exactly,
    /// five buttons in order, 176 × 30 and 2 apart, Accounts at its place, only it selected and
    /// without an error dot.
    /// </summary>
    private static void AssertFrame(ShellDriver app, bool full)
    {
        var card = app.BoundsOf(CardNamed(app, "SettingsCard"));
        var (x, y, width, height) = full ? PartA.CardFull : PartA.Card;
        Assert.Equal(new Rect(x, y, width, height), card);
        var nav = app.FindByAutomationId("SettingsNav");
        var buttons = nav.GetVisualChildren().OfType<Button>().ToList();
        Assert.Equal(PartA.NavIds, buttons.Select(button => AutomationProperties.GetAutomationId(button)));
        Assert.Equal(PartA.NavButtons, buttons.Select(button => AutomationProperties.GetName(button)));
        for (var i = 0; i < buttons.Count; i++)
        {
            var bounds = app.BoundsOf(buttons[i]);
            Assert.Equal((PartA.NavWidth, PartA.NavHeight), (bounds.Width, bounds.Height));
            if (i > 0)
            {
                Assert.Equal(PartA.NavGap, bounds.Top - app.BoundsOf(buttons[i - 1]).Bottom, 1.0);
            }

            Assert.Equal(i == 4, buttons[i].Classes.Contains("selected"));
        }

        var (ax, ay) = full ? PartA.AccountsButtonAtFull : PartA.AccountsButtonAt;
        AssertAt(app.BoundsOf(buttons[4]), ax, ay, "SettingsNavAccounts");
        Assert.DoesNotContain(buttons[4].GetVisualDescendants().OfType<Control>(), control => AutomationProperties.GetAutomationId(control) == "SettingsNavError" && control.IsEffectivelyVisible);
    }

    /// <summary>The settings snapshot's ids, so that a state's controls and absences read at once.</summary>
    private static void AssertShows(SettingsSnapshot snapshot, IEnumerable<string> shown, IEnumerable<string> hidden)
    {
        foreach (var id in shown)
        {
            Assert.True(snapshot.Shows(id), $"{id} does not show; the screen shows: {string.Join(", ", snapshot.Fields.Select(field => field.Id))}");
        }

        foreach (var id in hidden)
        {
            Assert.False(snapshot.Shows(id), $"{id} shows.");
        }
    }

    /// <summary>The visible buttons of a dialog's view, left to right, as their texts.</summary>
    private static List<string> ButtonsOf(ShellDriver app, Visual view) =>
        [.. view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && button.Bounds.Height > 0
                && AutomationProperties.GetAutomationId(button) is "CancelButton" or "ConfirmButton" or "ErrorCopyButton" or "ErrorExtraButton")
            .OrderBy(button => app.BoundsOf(button).X)
            .Select(button => AutomationProperties.GetName(button) ?? button.Content as string ?? string.Empty)];
}
