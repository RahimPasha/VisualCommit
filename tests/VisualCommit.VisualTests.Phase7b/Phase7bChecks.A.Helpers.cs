using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>Part A's own helpers (docs/test-reports/phase-7b.md, part A's conventions).</summary>
public sealed partial class Phase7bChecksA
{
    /// <summary>Every id a block may show, 7a's, before the provider's suffix.</summary>
    private static readonly string[] BlockIds =
    [
        "AccountsOff", "AccountsSignedOut", "AccountLogin", "AccountUser", "AccountTokenPlace", "AccountNeedsSignIn", "AccountsSignOutNote",
        "AccountsSignInButton", "AccountsSignOutButton", "AccountsBusy", "AccountUseForGit", "AccountUseForGitNote",
    ];

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>The three fakes' request counts before a step (the shared conventions' mark).</summary>
    private readonly record struct Marks(int AzureDevOps, int GitLab, int GitHub);

    private static Marks Mark(ServedHosting7b served) => new(served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count, served.GitHub.Requests.Count);

    private static List<FakeServiceRequest> AzureSince(ServedHosting7b served, Marks mark) => [.. served.AzureDevOps.Requests.Skip(mark.AzureDevOps)];

    private static List<FakeServiceRequest> GitLabSince(ServedHosting7b served, Marks mark) => [.. served.GitLab.Requests.Skip(mark.GitLab)];

    private static List<FakeGitHubRequest> GitHubSince(ServedHosting7b served, Marks mark) => [.. served.GitHub.Requests.Skip(mark.GitHub)];

    /// <summary>"Requests none": none in any of the three fakes since <paramref name="mark"/>.</summary>
    private static void AssertNoRequests(ServedHosting7b served, Marks mark)
    {
        Assert.Empty(AzureSince(served, mark));
        Assert.Empty(GitLabSince(served, mark));
        Assert.Empty(GitHubSince(served, mark));
    }

    /// <summary>"Requests exactly …" of one fake: none in the other two since <paramref name="mark"/>.</summary>
    private static void AssertNoRequestsBut(ServedHosting7b served, Marks mark, HostingProvider provider)
    {
        Assert.True(provider == HostingProvider.AzureDevOps || AzureSince(served, mark).Count == 0, "Azure DevOps' fake was asked since the mark.");
        Assert.True(provider == HostingProvider.GitLab || GitLabSince(served, mark).Count == 0, "GitLab's fake was asked since the mark.");
        Assert.True(provider == HostingProvider.GitHub || GitHubSince(served, mark).Count == 0, "GitHub's fake was asked since the mark.");
    }

    /// <summary>The store's calls since <paramref name="mark"/>.</summary>
    private static List<string> CallsSince(FakeTokenStore tokens, int mark) => [.. tokens.Calls.Skip(mark)];

    /// <summary>Asserts a request of a new fake: method, raw path, query, route key, the authorization's form, status and login.</summary>
    private static void AssertRequest(FakeServiceRequest request, string path, string route, FakeAuthorizationForm form, int status, string? login, string? query = null)
    {
        Assert.Equal(("GET", path, route), (request.Method, request.Path, request.Route));
        if (query is not null)
        {
            Assert.Equal(query, request.Query.TrimStart('?'));
        }

        Assert.Equal(form, request.Authorization);
        Assert.Equal(status, request.Status);
        Assert.Equal(login, request.Login);
    }

    /// <summary>GitLab's sign-in: one <c>GET /api/v4/user</c>, <c>Bearer</c>.</summary>
    private static void AssertGitLabUser(IReadOnlyList<FakeServiceRequest> requests, int status, string? login) =>
        AssertRequest(Assert.Single(requests), "/api/v4/user", "user", FakeAuthorizationForm.Bearer, status, login);

    /// <summary>Azure DevOps' connectionData of <paramref name="organization"/> as typed, with the token.</summary>
    private static void AssertConnection(FakeServiceRequest request, string organization, int status, string? login) =>
        AssertRequest(request, $"/{organization}/_apis/connectionData", "connection:" + organization.ToLowerInvariant(), FakeAuthorizationForm.BasicEmptyUser, status, login, PartA.AzureDevOpsSignInQuery);

    /// <summary>Azure DevOps' second read: the core resource area.</summary>
    private static void AssertArea(FakeServiceRequest request, string organization) =>
        AssertRequest(request, $"/{organization}/_apis/resourceAreas/{PartA.CoreAreaId}", "areas", FakeAuthorizationForm.BasicEmptyUser, 200, null, PartA.AzureDevOpsSignInQuery);

    /// <summary>Azure DevOps' whole sign-in as a member: connectionData then the area, as the organization was typed.</summary>
    private static void AssertAzureSignIn(ServedHosting7b served, IReadOnlyList<FakeServiceRequest> requests, string organization)
    {
        Assert.Equal(PartA.AzureDevOpsSignInRoutes(organization), requests.Select(request => request.Route));
        AssertConnection(requests[0], organization, 200, AdaAzureLogin(served));
        AssertArea(requests[1], organization);
    }

    /// <summary>The login the fake records for Ada's token in Fernhill-Labs (WP-FA: its organization and her made-up id).</summary>
    private static string AdaAzureLogin(ServedHosting7b served) => $"{Organization}/{AdaAzureId(served)}";

    /// <summary>GitHub's sign-in: one <c>GET api/v3/user</c> with a token.</summary>
    private static void AssertGitHubUser(IReadOnlyList<FakeGitHubRequest> requests, int status, string? login)
    {
        var request = Assert.Single(requests);
        Assert.Equal(("GET", "/api/v3/user", "user"), (request.Method, request.Path, request.Route));
        Assert.True(request.TokenPresent, "The sign-in's request carried no token.");
        Assert.Equal(status, request.Status);
        if (status == 200)
        {
            Assert.Equal(login, request.Login);
        }
    }

    /// <summary>A picture of part A: before it no token on screen; after it no text cut.</summary>
    private static Screenshot ShotA(ShellDriver app, string name, bool keepPointer = false)
    {
        AssertNoTokenOnScreen(app);
        var screenshot = Shot(app, name, keepPointer);
        AssertNoClippedText(app);
        return screenshot;
    }

    /// <summary>No visible text, automation value or automation name in the window holds one of part A's tokens; a failure names the control and the token's constant.</summary>
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
    /// Every method's end, after its apps have closed: the tripwire for part A's tokens over the data
    /// folder and the copy's three clones with each start's call log; each fake's errors and the
    /// harness's refused requests empty; no request answered 400 or 415; the launcher's launches only
    /// those the steps name.
    /// </summary>
    private static void AssertCleanEnd(IReadOnlyList<ShellDriver> apps, TempDirectory data, ServedHosting7b? served, int launches = 0)
    {
        List<string> folders = [data.Path];
        if (served is not null)
        {
            folders.AddRange([served.MobileApp.Path, served.Manifest.Path, served.WebApp.Path]);
        }

        foreach (var app in apps)
        {
            foreach (var (_, token) in PartA.Tokens)
            {
                TokenTripwire.AssertAbsent(token, folders, app.Session.GitCalls);
            }

            Assert.Equal(0, app.RefusedRequests.Count);
        }

        if (served is not null)
        {
            Assert.Empty(served.AzureDevOps.Errors);
            Assert.Empty(served.GitLab.Errors);
            Assert.Empty(served.GitHub.Errors);
            Assert.DoesNotContain(served.AzureDevOps.Requests, request => request.Status is 400 or 415);
            Assert.DoesNotContain(served.GitLab.Requests, request => request.Status is 400 or 415);
            Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
        }

        Assert.Equal(launches, apps.Sum(app => app.Launcher.Requests.Count));
    }

    /// <summary>The start's Info lines, in order, through <c>WithPorts</c>.</summary>
    private static void AssertStartLines(ShellDriver app, ServedHosting7b served, IReadOnlyList<string> expected)
    {
        var lines = app.LogLines();
        var at = -1;
        foreach (var line in expected.Select(text => WithPorts(text, served)))
        {
            var next = lines.Select((text, index) => (text, index)).FirstOrDefault(entry => entry.index > at && entry.text.EndsWith(line, StringComparison.Ordinal));
            Assert.True(next.text is not null, $"The log has no line '{line}' after line {at}: {string.Join(" | ", lines)}");
            at = next.index;
        }
    }

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

    /// <summary>The records of <c>accounts.json</c>, none when there is no file.</summary>
    private static List<JsonElement> RecordsOf(JsonElement? file) =>
        file is { } root && root.TryGetProperty("accounts", out var accounts) ? [.. accounts.EnumerateArray()] : [];

    /// <summary>The record of <paramref name="provider"/> (its <c>provider</c> id) on <paramref name="host"/>, or null.</summary>
    private static JsonElement? RecordOf(JsonElement? file, string provider, string host) =>
        RecordsOf(file).Cast<JsonElement?>().FirstOrDefault(record =>
            record!.Value.GetProperty("provider").GetString() == provider && record.Value.GetProperty("host").GetString() == host);

    private static string IdOf(HostingProvider provider) => HostingProviders.Id(provider);

    private static string HostOf(ServedHosting7b served, HostingProvider provider) => PartA.Host(provider switch
    {
        HostingProvider.GitHub => served.GitHub.Port,
        HostingProvider.GitLab => served.GitLab.Port,
        _ => served.AzureDevOps.Port,
    });

    private static JsonElement? RecordOf(TempDirectory data, ServedHosting7b served, HostingProvider provider) =>
        RecordOf(ReadAccounts(data), IdOf(provider), HostOf(served, provider));

    /// <summary>
    /// Asserts a provider's record: exactly the scenario facts' fields (GitHub's and GitLab's eight,
    /// Azure DevOps' nine with <c>userId</c>) and values, with <paramref name="useForGit"/> and
    /// <paramref name="tokenState"/>.
    /// </summary>
    private static void AssertRecord(JsonElement? file, ServedHosting7b served, HostingProvider provider, bool useForGit, string tokenState)
    {
        var record = RecordOf(file, IdOf(provider), HostOf(served, provider)) ?? throw new Xunit.Sdk.XunitException($"accounts.json holds no {provider} record.");
        List<string> names = ["avatarUrl", "host", "id", "login", "name", "provider", "tokenState", "useForGit"];
        if (provider == HostingProvider.AzureDevOps)
        {
            names.Add("userId");
        }

        Assert.Equal(names.Order(StringComparer.Ordinal), record.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        var (login, id, name, avatar) = provider switch
        {
            HostingProvider.GitHub => (OctoLogin, PartA.OctoId, OctoName, (string?)PartA.GitHubAvatarUrl(served.GitHub.Port)),
            HostingProvider.GitLab => (AdaLogin, PartA.AdaGitLabId, AdaName, null),
            _ => (PartA.Organization, 0L, AdaName, null),
        };
        Assert.Equal(login, record.GetProperty("login").GetString());
        Assert.Equal(id, record.GetProperty("id").GetInt64());
        Assert.Equal(name, record.GetProperty("name").GetString());
        Assert.Equal(avatar, record.GetProperty("avatarUrl").GetString());
        Assert.Equal(useForGit, record.GetProperty("useForGit").GetBoolean());
        Assert.Equal(tokenState, record.GetProperty("tokenState").GetString());
        if (provider == HostingProvider.AzureDevOps)
        {
            Assert.Equal(AdaAzureId(served), record.GetProperty("userId").GetString());
        }
    }

    /// <summary>Ada's made-up id in Fernhill-Labs, in lower case.</summary>
    private static string AdaAzureId(ServedHosting7b served) => served.AzureDevOps.User(Organization, AdaName).Id.ToString("D");

    /// <summary>The file's text holds no <c>@</c>: no field holds an e-mail address (D368).</summary>
    private static void AssertNoAddress(TempDirectory data) =>
        Assert.DoesNotContain("@", File.ReadAllText(AccountsFile(data)), StringComparison.Ordinal);

    /// <summary>Waits until <paramref name="condition"/> holds of the parsed <c>accounts.json</c> (written off the UI thread).</summary>
    private static Task WaitForFileAsync(ShellDriver app, TempDirectory data, Func<JsonElement?, bool> condition, string what) =>
        app.WaitForAsync(() => condition(ReadAccounts(data)), what);

    /// <summary>Asserts the session's account of <paramref name="provider"/> as the scenario facts give it.</summary>
    private static void AssertAccount(ShellDriver app, ServedHosting7b served, HostingProvider provider, TokenPlace place, AccountNoteKind note, bool useForGit = true, AccountState state = AccountState.SignedIn)
    {
        var account = app.Session.Services.Hosting.AccountOf(provider) ?? throw new Xunit.Sdk.XunitException($"No {provider} account.");
        Assert.Equal(state, account.State);
        if (state == AccountState.SignedIn)
        {
            Assert.Equal(place, account.Place);
        }

        Assert.Equal(note, account.NoteKind);
        Assert.Equal(useForGit, account.Account.UseForGit);
        switch (provider)
        {
            case HostingProvider.GitHub:
                Assert.Equal((PartA.GitHubKey(served.GitHub.Port), OctoLogin, PartA.OctoId, OctoName), (KeyText(account), account.Account.Login, account.Account.Id, account.Account.Name));
                Assert.Equal(PartA.GitHubAvatarUrl(served.GitHub.Port), account.Account.AvatarUrl?.AbsoluteUri);
                break;
            case HostingProvider.GitLab:
                Assert.Equal((PartA.GitLabKey(served.GitLab.Port), AdaLogin, PartA.AdaGitLabId, AdaName), (KeyText(account), account.Account.Login, account.Account.Id, account.Account.Name));
                Assert.Null(account.Account.AvatarUrl);
                break;
            default:
                Assert.Equal((PartA.AzureDevOpsKey(served.AzureDevOps.Port), PartA.Organization, 0L, AdaName), (KeyText(account), account.Account.Login, account.Account.Id, account.Account.Name));
                Assert.Equal(AdaAzureId(served), account.Account.UserId);
                Assert.Null(account.Account.AvatarUrl);
                break;
        }
    }

    /// <summary>The account's key as the store's calls write it: provider, host and login, lower-cased.</summary>
    private static string KeyText(AccountStatus account) => account.Account.Key.Id.ToLowerInvariant();

    /// <summary>The automation id of the focused control or of its nearest ancestor with one.</summary>
    private static string? FocusedId(ShellDriver app)
    {
        app.Settle();
        return app.Window.FocusManager?.GetFocusedElement() is Control focused
            ? focused.GetSelfAndVisualAncestors().OfType<Control>().Select(AutomationProperties.GetAutomationId).FirstOrDefault(id => id is { Length: > 0 })
            : null;
    }

    private static Task WaitForFocusAsync(ShellDriver app, string id) =>
        app.WaitForAsync(() => FocusedId(app) == id, $"the keyboard focus on {id}");

    /// <summary>The named border (a dialog's card) in the window.</summary>
    private static Border CardNamed(ShellDriver app, string name) =>
        app.Window.GetVisualDescendants().OfType<Border>().First(border => border.Name == name && border.IsEffectivelyVisible);

    /// <summary>The section's scroll viewer, <c>SectionScroller</c>.</summary>
    private static ScrollViewer Scroller(ShellDriver app) =>
        app.Window.GetVisualDescendants().OfType<ScrollViewer>().First(viewer => viewer.Name == "SectionScroller");

    /// <summary>The section fits: its extent within its viewport, offset 0.</summary>
    private static void AssertFits(ShellDriver app)
    {
        var scroller = Scroller(app);
        Assert.True(scroller.Extent.Height <= scroller.Viewport.Height + 0.5, $"The section scrolls: extent {scroller.Extent.Height}, viewport {scroller.Viewport.Height}.");
        Assert.Equal(0, scroller.Offset.Y, 0.5);
    }

    /// <summary>"Turn the wheel over SettingsSection until the offset stops changing": one notch at a time at the section's centre, the layout settled after each.</summary>
    private static void WheelUntilStill(ShellDriver app, bool up)
    {
        var scroller = Scroller(app);
        var centre = app.BoundsOf(scroller).Center;
        for (var notch = 0; notch < 40; notch++)
        {
            var before = scroller.Offset.Y;
            app.Wheel(centre, up ? 1 : -1);
            if (Math.Abs(scroller.Offset.Y - before) < 0.01)
            {
                return;
            }
        }

        throw new Xunit.Sdk.XunitException("The section's offset kept changing under the wheel.");
    }

    /// <summary>"The end": the largest offset, above 0.</summary>
    private static void AssertAtEnd(ShellDriver app)
    {
        var scroller = Scroller(app);
        Assert.True(scroller.Offset.Y > 0, "The section is not scrolled.");
        Assert.Equal(scroller.Extent.Height - scroller.Viewport.Height, scroller.Offset.Y, 0.5);
    }

    /// <summary>Whether <paramref name="control"/> lies wholly inside the section's viewport.</summary>
    private static bool WhollyInView(ShellDriver app, Control control)
    {
        var view = app.BoundsOf(Scroller(app));
        var bounds = app.BoundsOf(control);
        return bounds.Top >= view.Top - 0.5 && bounds.Bottom <= view.Bottom + 0.5;
    }

    /// <summary>A control of the screen or the dialog on top (<see cref="ShellDriver.FindByAutomationId(string)"/>).</summary>
    private static Control Find(ShellDriver app, string id) => app.FindByAutomationId(id);

    /// <summary>A control of a block, by 7a's id and the provider.</summary>
    private static Control InBlock(ShellDriver app, HostingProvider provider, string id) =>
        app.FindByAutomationId(app.AccountsBlock(provider), PartA.IdOf(provider, id));

    private static bool Shows(ShellDriver app, HostingProvider provider, string id) =>
        app.AccountsBlock(provider).GetVisualDescendants().OfType<Control>()
            .Any(control => AutomationProperties.GetAutomationId(control) == PartA.IdOf(provider, id) && control.IsEffectivelyVisible);

    private static string? TextOf(ShellDriver app, HostingProvider provider, string id) => (InBlock(app, provider, id) as TextBlock)?.Text;

    /// <summary>A text block's colour.</summary>
    private static Color? ColourOf(TextBlock block) => (block.Foreground as ISolidColorBrush)?.Color;

    private static Color? ColourOf(Control control) => control is TextBlock block ? ColourOf(block) : null;

    /// <summary>Asserts that a block shows exactly <paramref name="shown"/> of its ids, each in its own suffix.</summary>
    private static void AssertBlockShows(ShellDriver app, HostingProvider provider, params string[] shown)
    {
        Assert.True(app.AccountsBlock(provider).IsEffectivelyVisible, $"The {provider} block does not show.");
        foreach (var id in BlockIds)
        {
            Assert.True(shown.Contains(id) == Shows(app, provider, id), $"{PartA.IdOf(provider, id)} {(shown.Contains(id) ? "does not show" : "shows")}.");
        }

        // The label, from x 336 in the 132-wide column (to x 468), the provider's name; the row's
        // controls from x 480 to 944 (part A's conventions).
        var label = app.AccountsBlock(provider).GetVisualDescendants().OfType<TextBlock>().First(block => block.Classes.Contains("settingsLabel"));
        Assert.Equal(PartA.LabelOf(provider), label.Text);
        Assert.Equal(PartA.SectionX, app.BoundsOf(label).X, PartA.Tolerance);
        Assert.True(app.BoundsOf(label).Right <= PartA.LabelRight + PartA.Tolerance, $"{provider}'s label ends at x {app.BoundsOf(label).Right:0.#}, past {PartA.LabelRight}.");
        foreach (var id in shown)
        {
            var bounds = app.BoundsOf(InBlock(app, provider, id));
            Assert.True(bounds.X >= PartA.ControlsX - PartA.Tolerance && bounds.Right <= PartA.ControlsRight + PartA.Tolerance, $"{PartA.IdOf(provider, id)} lies at x {bounds.X:0.#} to {bounds.Right:0.#}, outside the controls' {PartA.ControlsX} to {PartA.ControlsRight}.");
        }
    }

    /// <summary>
    /// A block signed out: 7a's signed-out block, <c>Not signed in.</c> from x 480, <c>Sign in…</c> 10
    /// below, enabled; behind a stacked dialog (<paramref name="behindDialog"/>) the same block, whose
    /// button the dialog's layer leaves disabled, so its enablement is not asserted there.
    /// </summary>
    private static void AssertSignedOutBlock(ShellDriver app, HostingProvider provider, bool behindDialog = false)
    {
        AssertBlockShows(app, provider, "AccountsSignedOut", "AccountsSignInButton");
        var text = InBlock(app, provider, "AccountsSignedOut");
        Assert.Equal(PartA.SignedOut, ((TextBlock)text).Text);
        Assert.Equal(PartA.ControlsX, app.BoundsOf(text).X, PartA.Tolerance);
        var button = InBlock(app, provider, "AccountsSignInButton");
        Assert.True(behindDialog || button.IsEffectivelyEnabled, $"{PartA.IdOf(provider, "AccountsSignInButton")} is disabled.");
        Assert.Equal(PartA.SignInButton, AutomationProperties.GetName(button));
        Assert.Equal(PartA.ButtonsGap, app.BoundsOf(button).Top - app.BoundsOf(text).Bottom, PartA.Tolerance);
    }

    /// <summary>
    /// A block signed in: the login (SemiBold) from x 480, Azure DevOps' name under it, the token's
    /// place, 10 below it "Sign out…", and 18 below the buttons the row labelled <c>Git</c> with the
    /// switch and its note.
    /// </summary>
    private static void AssertSignedInBlock(ShellDriver app, HostingProvider provider, string place, bool useForGit = true)
    {
        string[] shown = provider == HostingProvider.AzureDevOps
            ? ["AccountLogin", "AccountUser", "AccountTokenPlace", "AccountsSignOutButton", "AccountUseForGit", "AccountUseForGitNote"]
            : ["AccountLogin", "AccountTokenPlace", "AccountsSignOutButton", "AccountUseForGit", "AccountUseForGitNote"];
        AssertBlockShows(app, provider, shown);
        var above = AssertLogin(app, provider);
        var placeText = (TextBlock)InBlock(app, provider, "AccountTokenPlace");
        Assert.Equal(place, placeText.Text);
        Assert.Contains("settingsNote", placeText.Classes);
        Assert.True(app.BoundsOf(placeText).Top >= app.BoundsOf(above).Bottom - 0.5);
        var signOut = InBlock(app, provider, "AccountsSignOutButton");
        Assert.True(signOut.IsEffectivelyEnabled);
        Assert.Equal(PartA.SignOutButton, AutomationProperties.GetName(signOut));
        Assert.Equal(PartA.ButtonsGap, app.BoundsOf(signOut).Top - app.BoundsOf(placeText).Bottom, PartA.Tolerance);
        AssertGitRow(app, provider, signOut, useForGit);
    }

    /// <summary>
    /// A block needing sign-in after a session-only start: the login (and Azure DevOps' name),
    /// <c>SessionEnded</c>'s warning, 10 below it both buttons, enabled and 8 apart, and 18 below
    /// them the row labelled <c>Git</c>, checked.
    /// </summary>
    private static void AssertSessionEndedBlock(ShellDriver app, HostingProvider provider)
    {
        string[] shown = provider == HostingProvider.AzureDevOps
            ? ["AccountLogin", "AccountUser", "AccountNeedsSignIn", "AccountsSignInButton", "AccountsSignOutButton", "AccountUseForGit", "AccountUseForGitNote"]
            : ["AccountLogin", "AccountNeedsSignIn", "AccountsSignInButton", "AccountsSignOutButton", "AccountUseForGit", "AccountUseForGitNote"];
        AssertBlockShows(app, provider, shown);
        var above = AssertLogin(app, provider);
        var warning = (TextBlock)InBlock(app, provider, "AccountNeedsSignIn");
        Assert.Equal(PartA.SessionEnded, warning.Text);
        Assert.Contains("dialogWarning", warning.Classes);
        Assert.True(app.BoundsOf(warning).Top >= app.BoundsOf(above).Bottom - 0.5);
        var signIn = InBlock(app, provider, "AccountsSignInButton");
        var signOut = InBlock(app, provider, "AccountsSignOutButton");
        Assert.True(signIn.IsEffectivelyEnabled);
        Assert.True(signOut.IsEffectivelyEnabled);
        Assert.Equal(PartA.ButtonsGap, app.BoundsOf(signIn).Top - app.BoundsOf(warning).Bottom, PartA.Tolerance);
        Assert.Equal(app.BoundsOf(signIn).Top, app.BoundsOf(signOut).Top, PartA.Tolerance);
        Assert.Equal(PartA.ButtonSpacing, app.BoundsOf(signOut).X - app.BoundsOf(signIn).Right, PartA.Tolerance);
        AssertGitRow(app, provider, signOut, useForGit: true);
    }

    /// <summary>
    /// A block's first row's column: the login in the server's spelling, SemiBold, from x 480, and on
    /// Azure DevOps the account's name right under it (secondary, normal weight). Returns the lower
    /// of the two, under which the note lies.
    /// </summary>
    private static Control AssertLogin(ShellDriver app, HostingProvider provider)
    {
        var login = (TextBlock)InBlock(app, provider, "AccountLogin");
        Assert.Equal(provider switch { HostingProvider.GitHub => OctoLogin, HostingProvider.GitLab => AdaLogin, _ => PartA.Organization }, login.Text);
        Assert.Equal(FontWeight.SemiBold, login.FontWeight);
        Assert.Equal(PartA.ControlsX, app.BoundsOf(login).X, PartA.Tolerance);
        if (provider != HostingProvider.AzureDevOps)
        {
            return login;
        }

        var user = (TextBlock)InBlock(app, provider, "AccountUser");
        Assert.Equal(AdaName, user.Text);
        Assert.Equal(PartA.Secondary, ColourOf(user));
        Assert.Equal(FontWeight.Normal, user.FontWeight);
        Assert.Equal(PartA.ControlsX, app.BoundsOf(user).X, PartA.Tolerance);
        Assert.Equal(app.BoundsOf(login).Bottom, app.BoundsOf(user).Top, 0.5);
        return user;
    }

    /// <summary>
    /// The row labelled <c>Git</c> (<c>settingsLabel</c>, from x 336), 18 below the buttons' row:
    /// <c>AccountUseForGit</c> from x 480 in <paramref name="useForGit"/>'s state and its note under
    /// it with the provider's text.
    /// </summary>
    private static void AssertGitRow(ShellDriver app, HostingProvider provider, Control buttons, bool useForGit)
    {
        var check = (CheckBox)InBlock(app, provider, "AccountUseForGit");
        Assert.Equal(useForGit, check.IsChecked);
        Assert.Equal(PartA.UseForGit, AutomationProperties.GetName(check));
        Assert.Equal(PartA.RowSpacing, app.BoundsOf(check).Top - app.BoundsOf(buttons).Bottom, PartA.Tolerance);
        Assert.Equal(PartA.ControlsX, app.BoundsOf(check).X, PartA.Tolerance);
        var label = Assert.Single(
            app.AccountsBlock(provider).GetVisualDescendants().OfType<TextBlock>(),
            block => block.Classes.Contains("settingsLabel") && block.IsEffectivelyVisible && block.Text == PartA.GitLabel);
        Assert.Equal(PartA.SectionX, app.BoundsOf(label).X, PartA.Tolerance);
        Assert.Equal(app.BoundsOf(check).Top, app.BoundsOf(label).Top, PartA.Tolerance);
        Assert.Equal(PartA.Secondary, ColourOf(label));
        var note = InBlock(app, provider, "AccountUseForGitNote");
        Assert.Equal(NoteOf(provider), TextOf(app, provider, "AccountUseForGitNote"));
        Assert.True(app.BoundsOf(note).Top >= app.BoundsOf(check).Bottom - 0.5, $"{PartA.IdOf(provider, "AccountUseForGitNote")} is not under the switch.");
    }

    /// <summary>
    /// The section scrolls (its extent above its viewport) and <paramref name="note"/>'s bottom lies
    /// below the viewport by less than one of its lines: its lines <paramref name="line"/> apart, read
    /// from its layout (±0.5) and stacked without gaps, so the last line's top, the note's bottom less
    /// one line, lies inside the viewport, the line scrolled out of view (A12's <c>a12b</c>, open point 2).
    /// </summary>
    private static void AssertLastLineScrolledOut(ShellDriver app, TextBlock note, double line)
    {
        var scroller = Scroller(app);
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height + 0.5, $"The section does not scroll: extent {scroller.Extent.Height:0.#}, viewport {scroller.Viewport.Height:0.#}.");
        var lines = note.TextLayout.TextLines;
        Assert.True(lines.Count > 1, $"{AutomationProperties.GetAutomationId(note)} is laid out in {lines.Count} line.");
        Assert.All(lines, text => Assert.Equal(line, text.Height, 0.5));
        Assert.Equal(note.Bounds.Height, lines.Sum(text => text.Height), 0.5);
        var below = app.BoundsOf(note).Bottom - app.BoundsOf(scroller).Bottom;
        var lastLine = lines[^1].Height;
        Assert.True(
            below > 0.5 && below < lastLine,
            $"{AutomationProperties.GetAutomationId(note)}'s bottom lies {below:0.#} below the viewport; less than one line ({lastLine:0.#}) and more than none expected.");
    }

    private static string NoteOf(HostingProvider provider) => provider switch
    {
        HostingProvider.GitHub => PartA.GitHubUseForGitNote,
        HostingProvider.GitLab => PartA.GitLabUseForGitNote,
        _ => PartA.AzureDevOpsUseForGitNote,
    };

    /// <summary>The shown blocks in D353's order, each <see cref="PartA.BlockSpacing"/> below the one before, the first 18 below the intro.</summary>
    private static void AssertBlockGaps(ShellDriver app, params HostingProvider[] shown)
    {
        var intro = app.BoundsOf(Find(app, "AccountsIntro"));
        var previous = intro.Bottom;
        var gap = PartA.RowSpacing;
        foreach (var provider in HostingProviders.InAccountsOrder)
        {
            var block = app.AccountsBlock(provider);
            Assert.Equal(shown.Contains(provider), block.IsEffectivelyVisible);
            if (!block.IsEffectivelyVisible)
            {
                continue;
            }

            var bounds = app.BoundsOf(block);
            Assert.Equal(gap, bounds.Top - previous, PartA.Tolerance);
            previous = bounds.Bottom;
            gap = PartA.BlockSpacing;
        }
    }

    /// <summary>
    /// The intro (10.3, 10.9): 10.2's sentence, secondary, in exactly two lines, as high (±0.5) as
    /// 7a's sentence laid out with its font in its width.
    /// </summary>
    private static void AssertIntro(ShellDriver app)
    {
        var intro = (TextBlock)Find(app, "AccountsIntro");
        Assert.Equal(PartA.Intro, intro.Text);
        Assert.Equal(PartA.Secondary, ColourOf(intro));
        Assert.Equal(TextWrapping.Wrap, intro.TextWrapping);
        Assert.Equal(PartA.IntroLines, intro.TextLayout.TextLines.Count);
        var bounds = app.BoundsOf(intro);
        Assert.Equal(PartA.SectionX, bounds.X, PartA.Tolerance);
        var sevenA = new TextBlock
        {
            Text = PartA.Intro7a,
            FontFamily = intro.FontFamily,
            FontSize = intro.FontSize,
            FontWeight = intro.FontWeight,
            FontStyle = intro.FontStyle,
            LineHeight = intro.LineHeight,
            TextWrapping = TextWrapping.Wrap,
        };
        sevenA.Measure(new Size(bounds.Width, double.PositiveInfinity));
        Assert.Equal(PartA.IntroLines, sevenA.TextLayout.TextLines.Count);
        Assert.Equal(sevenA.DesiredSize.Height, bounds.Height, PartA.IntroTolerance);
    }

    /// <summary>
    /// The settings card and section list at 1100×700 as 7a's A1: the card exactly, five buttons in
    /// order, 176 × 30 and 2 apart, Accounts at its place (±2), only it selected, no error dot; and
    /// <c>Cancel</c> and <c>Save</c> as phase 6's part S at 1100×700 (its S14 step 2): Save in the
    /// primary look, 8 right of Cancel, ending about 22 inside the card's right, both on the row y 607
    /// to 639 (±2).
    /// </summary>
    private static void AssertFrame(ShellDriver app)
    {
        var (x, y, width, height) = PartA.Card;
        var card = CardNamed(app, "SettingsCard");
        var cardBounds = app.BoundsOf(card);
        Assert.Equal(new Rect(x, y, width, height), cardBounds);
        var buttons = app.FindByAutomationId("SettingsNav").GetVisualChildren().OfType<Button>().ToList();
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

            Assert.True(buttons[i].Classes.Contains("selected") == (i == 4), $"{PartA.NavIds[i]} is {(i == 4 ? "not " : string.Empty)}in the selected look.");
            Assert.DoesNotContain(buttons[i].GetVisualDescendants().OfType<Control>(), control => AutomationProperties.GetAutomationId(control) == "SettingsNavError" && control.IsEffectivelyVisible);
        }

        var accounts = app.BoundsOf(buttons[4]);
        Assert.True(
            Math.Abs(accounts.X - PartA.AccountsButtonAt.X) <= PartA.Tolerance && Math.Abs(accounts.Y - PartA.AccountsButtonAt.Y) <= PartA.Tolerance,
            $"SettingsNavAccounts is at ({accounts.X}, {accounts.Y}), expected {PartA.AccountsButtonAt} ±{PartA.Tolerance}.");

        var cancel = (Button)app.FindByAutomationId(card, "CancelButton");
        var save = (Button)app.FindByAutomationId(card, "ConfirmButton");
        Assert.Equal((PartA.Cancel, PartA.Save), ((string?)cancel.Content, (string?)save.Content));
        Assert.True(cancel.IsEffectivelyEnabled && save.IsEffectivelyEnabled, "Cancel or Save is disabled.");
        Assert.Same(Application.Current!.FindResource("VcPrimaryButton"), save.Theme);
        Assert.Equal(PartA.SettingsButtonSpacing, app.BoundsOf(save).X - app.BoundsOf(cancel).Right, PartA.Tolerance);
        Assert.Equal(cardBounds.Right - PartA.SaveRightInset, app.BoundsOf(save).Right, PartA.Tolerance);
        foreach (var button in new[] { cancel, save })
        {
            Assert.Equal(PartA.SettingsButtons.Top, app.BoundsOf(button).Top, PartA.Tolerance);
            Assert.Equal(PartA.SettingsButtons.Bottom, app.BoundsOf(button).Bottom, PartA.Tolerance);
        }
    }

    /// <summary>Whether two folder paths name one folder (the call log's working directory as git was given it; 7a's).</summary>
    private static bool SameFolder(string? left, string right) =>
        left is not null && string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>Whether <paramref name="control"/>'s top lies above the section's viewport: scrolled out of view.</summary>
    private static bool TopScrolledOut(ShellDriver app, Control control) =>
        app.BoundsOf(control).Top < app.BoundsOf(Scroller(app)).Top - 0.5;

    /// <summary>The dialog's <c>Sign in</c> and <c>Cancel</c> both enabled.</summary>
    private static void AssertDialogButtonsEnabled(ShellDriver app)
    {
        Assert.True(ConfirmOf(app).IsEffectivelyEnabled, "Sign in is disabled.");
        Assert.True(app.FindByAutomationId(app.SignInDialogView(), "CancelButton").IsEffectivelyEnabled, "Cancel is disabled.");
    }

    /// <summary>
    /// The dialog of <paramref name="provider"/> as it opens, as 7a's A-checks assert it: stacked over
    /// the screen on Accounts (D104), <c>SignInDialog</c> named by its title, 7a's card 400 wide at x
    /// 350 to 750, centred; title, help and the provider's link(s) and boxes; the warning when no
    /// keychain, below the token box; no status line and no "In …" line; Cancel and Sign in, in that
    /// order, right-aligned.
    /// </summary>
    private static void AssertDialog(ShellDriver app, HostingProvider provider, string organization, bool noKeychain = false)
    {
        var host = app.Session.Services.DialogHost;
        var screen = app.SettingsDialog ?? throw new Xunit.Sdk.XunitException("The settings screen is not open under the dialog.");
        var dialog = app.SignInDialog ?? throw new Xunit.Sdk.XunitException("The sign-in dialog is not open.");
        Assert.Equal([screen, dialog], host.Open.Cast<DialogViewModel>());
        Assert.Same(dialog, host.Current);
        Assert.Equal(SettingsSection.Accounts, screen.SelectedSection);
        Assert.Equal(provider, dialog.Provider);
        var titleText = provider switch { HostingProvider.GitHub => PartA.GitHubDialogTitle, HostingProvider.GitLab => PartA.GitLabDialogTitle, _ => PartA.AzureDevOpsDialogTitle };
        var view = app.SignInDialogView();
        Assert.Equal("SignInDialog", AutomationProperties.GetAutomationId(view));
        Assert.Equal(titleText, AutomationProperties.GetName(view));
        var card = app.BoundsOf(CardNamed(app, "SignInCard"));
        Assert.Equal(PartA.DialogCard, (card.Left, card.Right));
        Assert.Equal(app.Window.Bounds.Height / 2, card.Center.Y, PartA.Tolerance);
        var title = (TextBlock)app.FindByAutomationId(view, "DialogTitle");
        Assert.Equal(titleText, title.Text);
        var help = (TextBlock)app.FindByAutomationId(view, "SignInHelp");
        Assert.Equal(provider switch { HostingProvider.GitHub => PartA.GitHubHelp, HostingProvider.GitLab => PartA.GitLabHelp, _ => PartA.AzureDevOpsHelp }, help.Text);
        var link = app.FindByAutomationId(view, "SignInTokenLink");
        Assert.Equal(provider switch { HostingProvider.GitHub => PartA.GitHubTokenLink, HostingProvider.GitLab => PartA.GitLabTokenLink, _ => PartA.AzureDevOpsTokenLink }, AutomationProperties.GetName(link));
        Assert.Equal(PartA.LinkGap, app.BoundsOf(link).Top - app.BoundsOf(help).Bottom, PartA.Tolerance);

        // The accent while enabled; Azure DevOps' without an organization disabled, in the disabled look.
        AssertLinkLook(link, enabled: provider != HostingProvider.AzureDevOps || organization.Length > 0);
        var legacy = app.FindByAutomationId(view, "SignInLegacyTokenLink");
        Assert.Equal(provider == HostingProvider.GitLab, legacy.IsEffectivelyVisible);
        if (provider == HostingProvider.GitLab)
        {
            Assert.Equal(PartA.GitLabLegacyLink, AutomationProperties.GetName(legacy));
            Assert.Equal(PartA.LegacyLinkGap, app.BoundsOf(legacy).Top - app.BoundsOf(link).Bottom, PartA.Tolerance);
            AssertLinkLook(legacy, enabled: true);
        }

        var box = (TextBox)app.FindByAutomationId(view, "SignInOrganization");
        Assert.Equal(provider == HostingProvider.AzureDevOps, box.IsEffectivelyVisible);
        var token = (TextBox)app.FindByAutomationId(view, "SignInToken");
        if (provider == HostingProvider.AzureDevOps)
        {
            Assert.Equal(organization, box.Text ?? string.Empty);
            Assert.Equal(PartA.OrganizationLabel, AutomationProperties.GetName(box));
            Assert.Contains("dialogBox", box.Classes);
            Assert.True(app.BoundsOf(box).Bottom <= app.BoundsOf(token).Top);
            Assert.True(app.BoundsOf(box).Top >= app.BoundsOf(link).Bottom);
        }

        Assert.Equal(PartA.TokenLabel, AutomationProperties.GetName(token));

        // The labels (promptLabel): "Token" right above its box, below the links or the organization's
        // box; on Azure DevOps "Organization" right above its box, below the link; elsewhere none.
        var labels = view.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Classes.Contains("promptLabel") && block.IsEffectivelyVisible).ToList();
        var tokenLabel = Assert.Single(labels, block => block.Text == PartA.TokenLabel);
        Control aboveToken = provider switch { HostingProvider.AzureDevOps => box, HostingProvider.GitLab => legacy, _ => link };
        Assert.True(app.BoundsOf(tokenLabel).Top >= app.BoundsOf(aboveToken).Bottom - 0.5 && app.BoundsOf(tokenLabel).Bottom <= app.BoundsOf(token).Top + 0.5, "The label Token is not between its box and what is above it.");
        if (provider == HostingProvider.AzureDevOps)
        {
            var organizationLabel = Assert.Single(labels, block => block.Text == PartA.OrganizationLabel);
            Assert.True(app.BoundsOf(organizationLabel).Top >= app.BoundsOf(link).Bottom - 0.5 && app.BoundsOf(organizationLabel).Bottom <= app.BoundsOf(box).Top + 0.5, "The label Organization is not between the link and its box.");
        }
        else
        {
            Assert.DoesNotContain(labels, block => block.Text == PartA.OrganizationLabel);
        }

        Assert.Equal(noKeychain, app.FindByAutomationId(view, "SignInNoKeychain").IsEffectivelyVisible);
        if (noKeychain)
        {
            AssertStatusLine(app, "SignInNoKeychain", "dialogWarning");
        }

        foreach (var id in new[] { "SignInChecking", "SignInError", "SignInSaveFailed" })
        {
            Assert.False(app.FindByAutomationId(view, id).IsEffectivelyVisible, $"{id} shows.");
        }

        // No "In {repo}" line (7a's A2 step 3).
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<Control>(), control => AutomationProperties.GetAutomationId(control) == "DialogRepository" && control.IsEffectivelyVisible);

        // As it opens the token box is empty, so Sign in (VcPrimaryButton) is disabled, right of
        // Cancel and right-aligned in the card (7a's AssertDialog).
        Assert.True(string.IsNullOrEmpty(token.Text), "SignInToken is not empty as the dialog opens.");
        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.True(cancel.IsEffectivelyEnabled);
        Assert.Equal(PartA.SignIn, AutomationProperties.GetName(confirm));
        Assert.False(confirm.IsEffectivelyEnabled, "Sign in is enabled as the dialog opens.");
        Assert.Equal([PartA.Cancel, PartA.SignIn], ButtonsOf(app, view));
        Assert.Same(Application.Current!.FindResource("VcPrimaryButton"), confirm.Theme);
        Assert.DoesNotContain("danger", confirm.Classes);
        Assert.True(
            app.BoundsOf(confirm).Right > app.BoundsOf(cancel).Right && app.BoundsOf(confirm).Right >= card.Right - PartA.SaveRightInset,
            "The dialog's buttons are not right-aligned.");
    }

    /// <summary>
    /// The token box masked and holding <paramref name="token"/>: one <c>•</c> per character, its
    /// automation value empty and its name <c>Token</c>. The text is compared without printing it.
    /// </summary>
    private static void AssertMasked(ShellDriver app, string token)
    {
        var box = (SecretTextBox)app.FindByAutomationId(app.SignInDialogView(), "SignInToken");
        Assert.Equal(PartA.Mask, box.PasswordChar);
        Assert.True(string.Equals(token, box.Text, StringComparison.Ordinal), $"SignInToken does not hold the token typed ({box.Text?.Length ?? 0} characters, {token.Length} typed).");
        Assert.Equal(string.Empty, ValueOf(box) ?? string.Empty);
        Assert.Equal(PartA.TokenLabel, AutomationProperties.GetName(box));
    }

    /// <summary>The dialog's error line: its text in <c>dialogError</c>, the one status line, below the token box and above the buttons.</summary>
    private static void AssertError(ShellDriver app, string text)
    {
        var error = (TextBlock)app.FindByAutomationId(app.SignInDialogView(), "SignInError");
        Assert.True(error.IsEffectivelyVisible, "SignInError does not show.");
        Assert.Equal(text, error.Text);
        AssertStatusLine(app, "SignInError", "dialogError");
        Assert.False(app.FindByAutomationId(app.SignInDialogView(), "SignInChecking").IsEffectivelyVisible, "SignInChecking shows beside SignInError.");
        Assert.False(app.FindByAutomationId(app.SignInDialogView(), "SignInSaveFailed").IsEffectivelyVisible, "SignInSaveFailed shows beside SignInError.");
    }

    /// <summary>
    /// A line of the dialog's state where part A's "The dialog" puts it: shown, in <paramref name="style"/>,
    /// below the token box (and below <c>SignInNoKeychain</c> when that shows and the line is another),
    /// from the token box's left, and above the buttons.
    /// </summary>
    private static void AssertStatusLine(ShellDriver app, string id, string style)
    {
        var view = app.SignInDialogView();
        var line = app.FindByAutomationId(view, id);
        Assert.True(line.IsEffectivelyVisible, $"{id} does not show.");
        Assert.Contains(style, line.Classes);
        var bounds = app.BoundsOf(line);
        var token = app.BoundsOf(app.FindByAutomationId(view, "SignInToken"));
        Assert.True(bounds.Top >= token.Bottom - 0.5, $"{id} is not below the token box.");
        Assert.Equal(token.Left, bounds.Left, PartA.Tolerance);
        var warning = app.FindByAutomationId(view, "SignInNoKeychain");
        if (id != "SignInNoKeychain" && warning.IsEffectivelyVisible)
        {
            Assert.True(bounds.Top >= app.BoundsOf(warning).Bottom - 0.5, $"{id} is not below SignInNoKeychain.");
        }

        var buttons = Math.Min(app.BoundsOf(app.FindByAutomationId(view, "CancelButton")).Top, app.BoundsOf(app.FindByAutomationId(view, "ConfirmButton")).Top);
        Assert.True(bounds.Bottom <= buttons + 0.5, $"{id} is not above the buttons.");
    }

    /// <summary>Whether <paramref name="control"/> lies wholly above the section's viewport: scrolled out of view.</summary>
    private static bool WhollyOutAbove(ShellDriver app, Control control) =>
        app.BoundsOf(control).Bottom <= app.BoundsOf(Scroller(app)).Top + 0.5;

    /// <summary>A box after the service's refusal: enabled, its text kept and all of it selected, the keyboard focus in it.</summary>
    private static void AssertSelected(ShellDriver app, string id, string text)
    {
        var box = (TextBox)app.FindByAutomationId(app.SignInDialogView(), id);
        Assert.True(box.IsEffectivelyEnabled);
        Assert.True(string.Equals(text, box.Text, StringComparison.Ordinal), $"{id} does not keep the text typed ({box.Text?.Length ?? 0} characters, {text.Length} typed).");
        Assert.Equal((0, text.Length), (Math.Min(box.SelectionStart, box.SelectionEnd), Math.Max(box.SelectionStart, box.SelectionEnd)));
        Assert.Equal(id, FocusedId(app));
    }

    /// <summary>Replaces a box's text: a click into it, Ctrl+A, then the text typed (part A's conventions).</summary>
    private static void Replace(ShellDriver app, string id, string text)
    {
        app.Click(app.FindByAutomationId(app.SignInDialogView(), id));
        app.Settle();
        app.PressKey(Avalonia.Input.Key.A, Avalonia.Input.RawInputModifiers.Control);
        app.Type(text);
        app.Settle();
    }

    private static Control ConfirmOf(ShellDriver app) => app.FindByAutomationId(app.SignInDialogView(), "ConfirmButton");

    /// <summary>
    /// A token link's look (part A's "The dialog"; A6's and A11's disabled look, the lead's ruling of
    /// 2026-11-06): enabled, the accent foreground and the hand cursor; disabled, the theme's disabled
    /// foreground (<c>VcTextDisabledBrush</c>, as its other disabled buttons) and the arrow cursor.
    /// </summary>
    private static void AssertLinkLook(Control link, bool enabled)
    {
        var name = AutomationProperties.GetAutomationId(link);
        Assert.True(enabled == link.IsEffectivelyEnabled, $"{name} is {(enabled ? "disabled" : "enabled")}.");
        Assert.Equal(enabled ? PartA.Accent : PartA.Disabled, ColourOf(link.GetVisualDescendants().OfType<TextBlock>().First()));
        Assert.Equal(enabled ? PartA.HandCursor : PartA.ArrowCursor, link.Cursor?.ToString());
    }

    /// <summary>
    /// A disabled link has no hover effect: with the pointer moved onto it no underline and the arrow
    /// cursor (A6 and A11, the lead's ruling of 2026-11-06); then the pointer goes back to the neutral point.
    /// </summary>
    private static void AssertNoHoverWhileDisabled(ShellDriver app, string id)
    {
        var link = Find(app, id);
        AssertLinkLook(link, enabled: false);
        app.MoveMouse(link);
        app.Settle();
        var text = link.GetVisualDescendants().OfType<TextBlock>().First();
        Assert.True(text.TextDecorations is not { Count: > 0 }, $"{id} is underlined under the pointer while disabled.");
        Assert.Equal(PartA.ArrowCursor, link.Cursor?.ToString());
        app.MoveToNeutral();
        app.Settle();
    }
}
