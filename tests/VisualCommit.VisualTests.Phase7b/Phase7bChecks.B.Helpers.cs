using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Git;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part B's own helpers (docs/test-reports/phase-7b.md, part B's conventions): the waits for the browser
/// and its load and the snapshots of the form, the browser, its switch and its rows, copied from
/// <c>Phase7aChecks.B.Helpers.cs</c> with 7b's names and the provider's words; the request marks and
/// the clean end as part A's (<c>Phase7bChecks.A.Helpers.cs</c>), whose helpers are that class's own.
/// </summary>
public sealed partial class Phase7bChecksB
{
    private static readonly Regex LogLine = new(@"^\S+ \S+ \S+ \[(?<level>[A-Z]{3})\] (?<message>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex RequestId = new(@"\(request (?<id>[A-Za-z0-9:._-]+)\)", RegexOptions.CultureInvariant);

    /// <summary>Git 2.46 sends a bound URL's first request with the account's token (8.6; 7a's <c>ProactiveAuthVersion</c>).</summary>
    private static readonly GitVersion ProactiveAuthVersion = new(2, 46, 0);

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    // ---- Requests (the shared conventions' mark) ---------------------------------------------

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

    /// <summary>Asserts a GET of a new fake: raw path, route key, query, the authorization's form, status and, when given, login.</summary>
    private static void AssertRequest(FakeServiceRequest request, string path, string route, string query, FakeAuthorizationForm form, int status, string? login = null)
    {
        Assert.Equal(("GET", path, route), (request.Method, request.Path, request.Route));
        Assert.Equal(query, request.Query.TrimStart('?'));
        Assert.Equal(form, request.Authorization);
        Assert.Equal(status, request.Status);
        if (login is not null)
        {
            Assert.Equal(login, request.Login);
        }
    }

    /// <summary>GitLab's list (A2): one <c>GET /api/v4/projects</c> with the list's query, <c>Bearer</c>.</summary>
    private static void AssertGitLabList(FakeServiceRequest request, int status) =>
        AssertRequest(request, PartB.GitLabListPath, PartB.GitLabListRoute, PartB.GitLabListQuery, FakeAuthorizationForm.Bearer, status, status == 200 ? AdaLogin : null);

    /// <summary>Azure DevOps' list (A2): one <c>GET /Fernhill-Labs/_apis/git/repositories?api-version=7.1</c>, <c>BasicEmptyUser</c>.</summary>
    private static void AssertAzureList(ServedHosting7b served, FakeServiceRequest request, int status) =>
        AssertRequest(request, PartB.AzureDevOpsListPath, PartB.AzureDevOpsListRoute, PartB.AzureDevOpsListQuery, FakeAuthorizationForm.BasicEmptyUser, status, status == 200 ? AdaAzureLogin(served) : null);

    /// <summary>The login the fake records for Ada's token in Fernhill-Labs: its organization and her made-up id (WP-FA).</summary>
    private static string AdaAzureLogin(ServedHosting7b served) => $"{Organization}/{served.AzureDevOps.User(Organization, AdaName).Id:D}";

    /// <summary>Waits until a fake has recorded a request of <paramref name="route"/> since its mark (a fake records after its answer).</summary>
    private static Task WaitForRouteAsync(ShellDriver app, Func<IEnumerable<string?>> routes, string route, string what) =>
        app.WaitForAsync(() => routes().Contains(route), what);

    private static Task WaitForGitLabAsync(ShellDriver app, ServedHosting7b served, Marks mark, string route) =>
        WaitForRouteAsync(app, () => GitLabSince(served, mark).Select(request => request.Route), route, $"GitLab's fake to record {route}");

    private static Task WaitForAzureAsync(ShellDriver app, ServedHosting7b served, Marks mark, string route) =>
        WaitForRouteAsync(app, () => AzureSince(served, mark).Select(request => request.Route), route, $"Azure DevOps' fake to record {route}");

    private static Task WaitForGitHubAsync(ShellDriver app, ServedHosting7b served, Marks mark, string route) =>
        WaitForRouteAsync(app, () => GitHubSince(served, mark).Select(request => request.Route), route, $"GitHub's fake to record {route}");

    /// <summary>The store's calls since <paramref name="mark"/>.</summary>
    private static List<string> StoreCallsSince(ShellDriver app, int mark) => [.. app.Tokens.Calls.Skip(mark)];

    // ---- The log ------------------------------------------------------------------------------

    /// <summary>The log's lines since <paramref name="mark"/> as (level, message), Debug included.</summary>
    private static List<(string Level, string Message)> LogSince(ShellDriver app, int mark) =>
        [.. app.LogLinesSince(mark).Select(line => LogLine.Match(line)).Where(match => match.Success).Select(match => (match.Groups["level"].Value, match.Groups["message"].Value))];

    /// <summary>The request id the one line beginning <paramref name="prefix"/> names.</summary>
    private static string RequestIdOf(IEnumerable<(string Level, string Message)> lines, string prefix)
    {
        var line = lines.Single(entry => entry.Message.StartsWith(prefix, StringComparison.Ordinal));
        var match = RequestId.Match(line.Message);
        Assert.True(match.Success, $"\"{line.Message}\" names no request id.");
        return match.Groups["id"].Value;
    }

    /// <summary>Asserts that the Warning <paramref name="message"/> is in <paramref name="lines"/> exactly once.</summary>
    private static void AssertWarningOnce(IReadOnlyList<(string Level, string Message)> lines, string message) =>
        Assert.True(
            lines.Count(entry => entry.Message == message) == 1 && lines.Single(entry => entry.Message == message).Level == "WRN",
            $"The Warning \"{message}\" is not in the log exactly once. The log since the mark:\n" + string.Join('\n', lines.Select(entry => $"[{entry.Level}] {entry.Message}")));

    // ---- Pictures and the end -------------------------------------------------------------------

    /// <summary>A picture of part B: before it no token on screen; after it no text cut.</summary>
    private static Screenshot ShotB(ShellDriver app, string name, bool keepPointer = false)
    {
        AssertNoTokenOnScreen(app);
        var screenshot = Shot(app, name, keepPointer);
        AssertNoClippedText(app);
        return screenshot;
    }

    /// <summary>No visible text, automation value or automation name in the window holds a scenario token; a failure names the control and the constant.</summary>
    private static void AssertNoTokenOnScreen(ShellDriver app)
    {
        app.Settle();
        foreach (var control in app.Window.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible))
        {
            var texts = new[]
            {
                AutomationProperties.GetName(control),
                AutomationProperties.GetHelpText(control),
                control is TextBlock block ? block.Text : null,
                control is TextBox box and not SecretTextBox ? box.Text : null,
                control is TextBox valueBox ? ControlAutomationPeer.CreatePeerForElement(valueBox).GetProvider<IValueProvider>()?.Value : null,
            };
            foreach (var text in texts.OfType<string>())
            {
                foreach (var (tokenName, token) in PartB.Tokens)
                {
                    Assert.False(text.Contains(token, StringComparison.Ordinal), $"A text of {AutomationProperties.GetAutomationId(control) ?? control.Name ?? control.GetType().Name} holds {tokenName}.");
                }
            }
        }
    }

    /// <summary>
    /// Every method's end, after the app has closed (part B's conventions): the tripwire for each
    /// scenario token over the data folder, the copy's three clones and <paramref name="clones"/>, with
    /// the call log; each fake's errors and the refused requests empty; no API request answered 400 or
    /// 415; nothing launched.
    /// </summary>
    private static void AssertCleanEnd(ShellDriver app, TempDirectory data, ServedHosting7b served, string? clones = null)
    {
        List<string> folders = [data.Path, served.MobileApp.Path, served.Manifest.Path, served.WebApp.Path];
        if (clones is not null)
        {
            folders.Add(clones);
        }

        foreach (var (_, token) in PartB.Tokens)
        {
            TokenTripwire.AssertAbsent(token, folders, app.Session.GitCalls);
        }

        Assert.Equal(0, app.RefusedRequests.Count);
        Assert.Empty(served.AzureDevOps.Errors);
        Assert.Empty(served.GitLab.Errors);
        Assert.Empty(served.GitHub.Errors);
        Assert.DoesNotContain(served.AzureDevOps.Requests, request => request.Status is 400 or 415);
        Assert.DoesNotContain(served.GitLab.Requests, request => request.Status is 400 or 415);
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
        Assert.Empty(app.Launcher.Requests);
    }

    private static bool SameFolder(string? left, string right) =>
        left is not null && string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// The working tree <paramref name="folder"/> as git names it (<c>rev-parse --show-toplevel</c>), which
    /// is how the tab and the recent list keep it: the physical folder (macOS: <c>/private/var/…</c> for a
    /// temporary folder made as <c>/var/…</c>; Windows: a junction or an 8.3 short name resolved), full and
    /// without a trailing separator; run with the tests' isolated environment, outside the app's call log
    /// (7a's <c>TopLevelAsync</c>, <c>Phase7aChecks.B.Helpers.cs</c>).
    /// </summary>
    private static async Task<string> TopLevelAsync(TempDirectory home, string folder)
    {
        var topLevel = (await GitAsync(home, folder, "rev-parse", "--show-toplevel")).StandardOutput.TrimEnd('\r', '\n');
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(topLevel));
    }

    // ---- accounts.json ---------------------------------------------------------------------------

    /// <summary>Each record of <c>accounts.json</c> as its raw JSON, in the file's order (B8 step 6: "the same two records").</summary>
    private static List<string> RecordsJson(TempDirectory data)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(data.Path, "accounts.json")));
        return [.. document.RootElement.GetProperty("accounts").EnumerateArray().Select(record => record.GetRawText())];
    }

    /// <summary>
    /// The one record of <paramref name="provider"/> in <c>accounts.json</c> as its members' raw JSON by
    /// name, without the member <paramref name="except"/> (B5 method 4: "its other fields unchanged").
    /// </summary>
    private static SortedDictionary<string, string> RecordMembers(TempDirectory data, string provider, string except)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(data.Path, "accounts.json")));
        var record = document.RootElement.GetProperty("accounts").EnumerateArray().Single(each => each.GetProperty("provider").GetString() == provider);
        return new(record.EnumerateObject().Where(member => member.Name != except).ToDictionary(member => member.Name, member => member.Value.GetRawText()), StringComparer.Ordinal);
    }

    /// <summary>The records of <c>accounts.json</c> as (provider, tokenState, useForGit); none without the file.</summary>
    private static List<(string Provider, string TokenState, bool UseForGit)> Records(TempDirectory data)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(data.Path, "accounts.json")));
            return [.. document.RootElement.GetProperty("accounts").EnumerateArray()
                .Select(record => (record.GetProperty("provider").GetString()!, record.GetProperty("tokenState").GetString()!, record.GetProperty("useForGit").GetBoolean()))];
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return [];
        }
    }

    // ---- The clone form -------------------------------------------------------------------------

    /// <summary>The active tab's welcome page.</summary>
    private static WelcomeViewModel Welcome(ShellDriver app) => app.Session.Shell.ActiveTab.Welcome;

    /// <summary>Clicks <c>CloneRepoButton</c> and waits until <c>CloneUrlBox</c> has the keyboard focus and, with <paramref name="button"/>, the browse button shows.</summary>
    private static async Task OpenFormAsync(ShellDriver app, bool button = true)
    {
        app.Click(app.FindByAutomationId("CloneRepoButton"));
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneUrlBox", "the focus in the clone form's URL box");
        if (button)
        {
            await app.WaitForAsync(() => BrowseButton(app).IsEffectivelyVisible, "the clone form's browse button");
        }

        app.Settle();
    }

    private static Button BrowseButton(ShellDriver app) => app.Find<Button>("CloneFromGitHubButton");

    private static TextBox Box(ShellDriver app, string name) => app.Find<TextBox>(name);

    /// <summary>
    /// Asserts the clone form (part B's conventions, "The form"): open on the active tab, its boxes
    /// holding the given texts, no error line; the button collapsed (<paramref name="button"/> null,
    /// the URL box x 284 to 676) or shown with that text and automation name, no tooltip,
    /// <c>VcActionButton</c>, its right edge 676, 6 right of the URL box from x 284, as high as
    /// Browse; enabled while no dialog is over the form. Returns the URL box's width.
    /// </summary>
    private static double AssertForm(ShellDriver app, string? button, string url = "", string name = "", string parent = "")
    {
        app.Settle();
        var welcome = Welcome(app);
        Assert.True(welcome.IsCloneFormOpen, "The clone form is not open.");
        Assert.Equal((url, parent, name), (Box(app, "CloneUrlBox").Text ?? string.Empty, Box(app, "CloneParentBox").Text ?? string.Empty, Box(app, "CloneNameBox").Text ?? string.Empty));
        Assert.False(app.Find<Control>("CloneError").IsEffectivelyVisible, "The clone form shows an error.");
        var urlBox = app.BoundsOf(Box(app, "CloneUrlBox"));
        var nameBox = app.BoundsOf(Box(app, "CloneNameBox"));
        Assert.Equal(PartB.FormLeft, urlBox.X, PartB.Tolerance);
        Assert.Equal(PartB.FormLeft, nameBox.X, PartB.Tolerance);
        Assert.Equal(PartB.FormRight, nameBox.Right, PartB.Tolerance);
        var browse = BrowseButton(app);
        Assert.Equal(button is not null, browse.IsEffectivelyVisible);
        Assert.Equal(button is not null, welcome.ShowsBrowseButton);
        if (button is null)
        {
            Assert.Equal(PartB.FormRight, urlBox.Right, PartB.Tolerance);
            return urlBox.Width;
        }

        var bounds = app.BoundsOf(browse);
        if (app.Session.Services.DialogHost.Current is null)
        {
            Assert.True(browse.IsEffectivelyEnabled, $"{button} is disabled.");
        }

        Assert.Same(Application.Current!.FindResource("VcActionButton"), browse.Theme);
        Assert.Equal(button, AutomationProperties.GetName(browse));
        var text = browse.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.Equal(button, text.Text);
        Assert.False(text.TextLayout.TextLines.Any(line => line.HasCollapsed), $"{button} is cut.");
        Assert.Single(text.TextLayout.TextLines);
        Assert.Null(ToolTip.GetTip(browse));
        Assert.Equal(PartB.FormRight, bounds.Right, PartB.Tolerance);
        Assert.Equal(PartB.ButtonGap, bounds.X - urlBox.Right, PartB.Tolerance);
        Assert.Equal(app.BoundsOf(app.Find<Button>("CloneBrowseButton")).Height, bounds.Height, PartB.Tolerance);
        Assert.Equal(urlBox.Center.Y, bounds.Center.Y, PartB.Tolerance);
        var urlRow = app.BoundsOf((Visual)Box(app, "CloneUrlBox").GetVisualParent()!);
        var parentRow = app.BoundsOf((Visual)Box(app, "CloneParentBox").GetVisualParent()!);
        Assert.Equal(parentRow.Height, urlRow.Height, PartB.Tolerance);
        return urlBox.Width;
    }

    // ---- The browser ------------------------------------------------------------------------------

    /// <summary>The open browser (anywhere in the dialog stack), or null.</summary>
    private static CloneFromGitHubDialogViewModel? Browser(ShellDriver app) =>
        app.Session.Services.DialogHost.Open.OfType<CloneFromGitHubDialogViewModel>().LastOrDefault();

    /// <summary>The browser's view as the dialog layer draws it.</summary>
    private static CloneFromGitHubDialogView BrowserView(ShellDriver app)
    {
        var dialog = Browser(app) ?? throw new InvalidOperationException("The browser is not open.");
        return app.Window.GetVisualDescendants().OfType<ContentPresenter>()
            .Select(presenter => ReferenceEquals(presenter.Content, dialog) ? presenter.Child as CloneFromGitHubDialogView : null)
            .FirstOrDefault(view => view is not null)
            ?? throw new InvalidOperationException("The browser is not drawn.");
    }

    /// <summary>Waits until the browser shows with the keyboard focus in its filter; with <paramref name="loaded"/>, also until the shown provider's load has ended.</summary>
    private static async Task<CloneFromGitHubDialogViewModel> WaitForBrowserAsync(ShellDriver app, bool loaded)
    {
        await app.WaitForAsync(() => Browser(app) is not null && app.TopDialogView is CloneFromGitHubDialogView, "the browser");
        var browser = Browser(app)!;
        if (loaded)
        {
            await app.WaitForAsync(() => browser.Load.IsCompleted, "the browser's load to end");
        }

        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneFromGitHubFilter", "the focus in the browser's filter");
        app.Settle();
        return browser;
    }

    private static Control InBrowser(ShellDriver app, string id) => app.FindByAutomationId(BrowserView(app), id);

    /// <summary>The switch's buttons drawn, left to right, as (automation id, text, selected look).</summary>
    private static List<(string Id, string Text, bool Selected, Rect Bounds)> SwitchOf(ShellDriver app) =>
        [.. BrowserView(app).GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && (AutomationProperties.GetAutomationId(button) ?? string.Empty).StartsWith("CloneFromGitHubProvider", StringComparison.Ordinal))
            .Select(button => (AutomationProperties.GetAutomationId(button)!, button.Content as string ?? string.Empty, button.Classes.Contains("selected"), app.BoundsOf(button)))
            .OrderBy(entry => entry.Item4.X)];

    /// <summary>Clicks the switch's button of <paramref name="provider"/>.</summary>
    private static void ClickSwitch(ShellDriver app, HostingProvider provider) =>
        app.Click(InBrowser(app, "CloneFromGitHubProvider" + provider));

    /// <summary>
    /// Asserts the browser (part B's conventions, "The browser", "The switch"): the card 460 wide at x 320
    /// to 780, centred vertically; titled and named <paramref name="title"/>, no repository line; the switch
    /// exactly <paramref name="switchTexts"/> (none: no switch) with <paramref name="selected"/> in the
    /// selected look, 28 high, 4 apart, below the title and 8 above the filter; the filter holding
    /// <paramref name="filter"/>, enabled (with the keyboard focus unless <paramref name="focus"/> is
    /// false); the lines exactly as given, in order under the list; Cancel enabled and Choose as given.
    /// </summary>
    private static void AssertBrowser(
        ShellDriver app,
        string title,
        string filter,
        bool chooseEnabled,
        string? status = null,
        string? error = null,
        IReadOnlyList<string>? switchTexts = null,
        string? selected = null,
        bool focus = true)
    {
        app.Settle();
        var view = BrowserView(app);
        Assert.Equal(title, AutomationProperties.GetName(view));
        var card = app.BoundsOf(view.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("dialogCard")));
        Assert.Equal((PartB.CardLeft, PartB.CardRight, PartB.CardWidth), (card.X, card.Right, card.Width));
        Assert.Equal((DefaultSize.Height - card.Height) / 2, card.Y, PartB.Tolerance);
        var titleBlock = (TextBlock)InBrowser(app, "CloneFromGitHubTitle");
        Assert.Equal(title, titleBlock.Text);
        Assert.Equal(title, AutomationProperties.GetName(InBrowser(app, "CloneFromGitHubList")));
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), block => block.Classes.Contains("dialogRepository") && block.IsEffectivelyVisible);
        var box = (TextBox)InBrowser(app, "CloneFromGitHubFilter");
        Assert.Equal(filter, box.Text ?? string.Empty);
        Assert.Equal(PartB.Filter, box.PlaceholderText);
        Assert.True(box.IsEffectivelyEnabled, "The filter is disabled.");
        if (focus)
        {
            Assert.Equal("CloneFromGitHubFilter", app.FocusedAutomationId());
        }

        // The id prefix is the switch's buttons' alone (1.7; WP-R's B3 R step looks for no such element):
        // no other element of the view, shown or hidden, carries it, as its automation id or as the name
        // an automation peer gives as its id when it has none.
        string[] switchIds = ["CloneFromGitHubProviderGitHub", "CloneFromGitHubProviderGitLab", "CloneFromGitHubProviderAzureDevOps"];
        Assert.All(
            view.GetVisualDescendants().OfType<Control>()
                .Select(control => AutomationProperties.GetAutomationId(control) ?? control.Name)
                .OfType<string>()
                .Where(id => id.StartsWith("CloneFromGitHubProvider", StringComparison.Ordinal)),
            id => Assert.Contains(id, switchIds));
        var buttons = SwitchOf(app);
        Assert.Equal(switchTexts ?? [], buttons.Select(button => button.Text));
        if (buttons.Count > 0)
        {
            var palette = PaletteOf(app);
            foreach (var button in buttons)
            {
                Assert.Equal("CloneFromGitHubProvider" + ProviderNamed(button.Text), button.Id);
                Assert.Equal(button.Text == selected, button.Selected);
                Assert.Equal(PartB.SwitchHeight, button.Bounds.Height, PartB.Tolerance);
                Assert.True(button.Bounds.Top >= app.BoundsOf(titleBlock).Bottom - 0.5, $"{button.Id} is not below the title.");
                Assert.Equal(PartB.SwitchToFilter, app.BoundsOf(box).Top - button.Bounds.Bottom, PartB.Tolerance);
                var drawn = (Button)InBrowser(app, button.Id);
                Assert.Same(Application.Current!.FindResource("VcFlatButton"), drawn.Theme);
                Assert.Equal(button.Text, AutomationProperties.GetName(drawn));
                Assert.Equal(button.Selected ? palette.TextPrimary : palette.TextSecondary, (drawn.Foreground as ISolidColorBrush)?.Color);

                // The selected one on the control background (#272C36), the others transparent.
                Assert.Equal(button.Selected ? palette.Control : null, PresenterBackground(drawn));
            }

            foreach (var (left, right) in buttons.Zip(buttons.Skip(1)))
            {
                Assert.Equal(PartB.SwitchGap, right.Bounds.X - left.Bounds.Right, PartB.Tolerance);
            }
        }
        else
        {
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Control>(), control => control.IsEffectivelyVisible
                && (AutomationProperties.GetAutomationId(control) ?? string.Empty).StartsWith("CloneFromGitHubProvider", StringComparison.Ordinal));
        }

        var list = app.BoundsOf(InBrowser(app, "CloneFromGitHubList"));
        var below = list.Bottom;
        foreach (var (id, text) in new[] { ("CloneFromGitHubStatus", status), ("CloneFromGitHubError", error), ("CloneFromGitHubMore", (string?)null), ("CloneFromGitHubSso", (string?)null) })
        {
            var line = (TextBlock)InBrowser(app, id);
            Assert.True(line.IsEffectivelyVisible == text is not null, $"{id} is {(line.IsEffectivelyVisible ? "shown" : "hidden")}.");
            if (text is null)
            {
                continue;
            }

            Assert.Equal(text, line.Text);
            var palette = PaletteOf(app);
            Assert.Equal(id == "CloneFromGitHubError" ? palette.Danger : palette.TextSecondary, (line.Foreground as ISolidColorBrush)?.Color);
            Assert.Contains(id == "CloneFromGitHubError" ? "dialogError" : "settingsNote", line.Classes);
            var bounds = app.BoundsOf(line);
            Assert.True(bounds.Top >= below - 0.5, $"{id} is not below what comes before it.");
            below = bounds.Bottom;
        }

        Assert.True(InBrowser(app, "CancelButton").IsEffectivelyEnabled, "Cancel is disabled.");
        Assert.Equal(PartB.Choose, AutomationProperties.GetName(InBrowser(app, "ConfirmButton")));
        Assert.Equal(chooseEnabled, InBrowser(app, "ConfirmButton").IsEffectivelyEnabled);
    }

    private static HostingProvider ProviderNamed(string name) => name switch
    {
        PartB.GitLabName => HostingProvider.GitLab,
        PartB.AzureDevOpsName => HostingProvider.AzureDevOps,
        _ => HostingProvider.GitHub,
    };

    /// <summary>The list's height inside its border.</summary>
    private static double ListHeight(ShellDriver app) => app.BoundsOf(InBrowser(app, "CloneFromGitHubList")).Height;

    /// <summary>The list's realised items in the window, top to bottom.</summary>
    private static List<ListBoxItem> Items(ShellDriver app) =>
        [.. InBrowser(app, "CloneFromGitHubList").GetVisualDescendants().OfType<ListBoxItem>().Where(item => item.IsEffectivelyVisible).OrderBy(item => app.BoundsOf(item).Y)];

    /// <summary>The item whose full name is <paramref name="fullName"/>.</summary>
    private static ListBoxItem Item(ShellDriver app, string fullName) =>
        Items(app).Single(item => AutomationProperties.GetName(item) == fullName);

    /// <summary>The full name's text block of an item (the one bound to <c>FullName</c>, not the detail).</summary>
    private static TextBlock NameBlock(ListBoxItem item) =>
        item.GetVisualDescendants().OfType<TextBlock>().First(block => block.Name != "CloneFromGitHubItemDetail");

    private static TextBlock DetailBlock(ListBoxItem item) =>
        item.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "CloneFromGitHubItemDetail");

    /// <summary>
    /// Asserts the browser's rows (7a's <c>AssertRows</c>): the model's and the drawn ones exactly
    /// <paramref name="rows"/> in order, each <c>CloneFromGitHubItem</c> named by its full name, its help
    /// text its detail, its tooltip the full name, a line break and the detail, 28 high, the name in the
    /// primary colour and the detail 12 after it in the secondary one at 11.5, whole, but for the row
    /// <paramref name="cut"/>, whose name is cut at its start (B6) and whose detail has no room; none
    /// selected unless <paramref name="selected"/>.
    /// </summary>
    private static void AssertRows(ShellDriver app, IReadOnlyList<(string FullName, string? Detail)> rows, string? selected = null, string? cut = null)
    {
        app.Settle();
        var browser = Browser(app)!;
        Assert.Equal(rows, browser.Rows.Select(row => (row.FullName, row.Detail)));
        Assert.Equal(selected, browser.Selected?.FullName);
        var items = Items(app);
        Assert.Equal(rows.Select(row => row.FullName), items.Select(item => AutomationProperties.GetName(item)));
        var palette = PaletteOf(app);
        foreach (var (item, (fullName, detail)) in items.Zip(rows))
        {
            Assert.Equal("CloneFromGitHubItem", AutomationProperties.GetAutomationId(item));
            Assert.Equal(detail, AutomationProperties.GetHelpText(item));
            Assert.Equal(detail is null ? fullName : fullName + "\n" + detail, ToolTip.GetTip(item) as string);
            Assert.Equal(PartB.RowHeight, item.Bounds.Height);
            Assert.Equal(new Thickness(10, 0), item.Padding);
            var name = NameBlock(item);
            Assert.Equal(fullName, name.Text);
            Assert.Equal(TextTrimming.LeadingCharacterEllipsis, name.TextTrimming);
            Assert.Equal(palette.TextPrimary, (name.Foreground as ISolidColorBrush)?.Color);
            if (selected == fullName)
            {
                Assert.Equal(palette.Selection, BackgroundOf(item));
            }
            if (fullName == cut)
            {
                AssertCut(app, item, fullName);
                continue;
            }

            Assert.False(name.TextLayout.TextLines.Any(line => line.HasCollapsed), $"{fullName} is cut.");
            var shown = DetailBlock(item);
            Assert.Equal(detail is not null, shown.IsEffectivelyVisible);
            if (detail is not null)
            {
                Assert.Equal(detail, shown.Text);
                Assert.Equal(palette.TextSecondary, (shown.Foreground as ISolidColorBrush)?.Color);
                Assert.Equal(PartB.NoteSize, shown.FontSize);
                Assert.Equal(PartB.DetailGap, app.BoundsOf(shown).X - app.BoundsOf(name).Right, PartB.Tolerance);
                Assert.False(shown.TextLayout.TextLines.Any(line => line.HasCollapsed), $"{fullName}'s detail is cut.");
            }
        }
    }

    /// <summary>
    /// B6: the row's name is one collapsed line that begins with <c>…</c> and ends with the repository's
    /// name whole (the last segment of <paramref name="fullName"/>); its width the row's but for at most
    /// the 12 gap; the detail not drawn whole beside it (open point 12); the item still named by the whole name.
    /// </summary>
    private static void AssertCut(ShellDriver app, ListBoxItem item, string fullName)
    {
        var name = NameBlock(item);
        var lines = name.TextLayout.TextLines;
        Assert.Single(lines);
        Assert.True(lines[0].HasCollapsed, $"{fullName} is not cut.");
        var drawn = DrawnText(name);
        Assert.StartsWith(PartB.Ellipsis, drawn, StringComparison.Ordinal);
        Assert.EndsWith(fullName[fullName.LastIndexOf('/')..], drawn, StringComparison.Ordinal);
        var content = app.BoundsOf(item).Width - item.Padding.Left - item.Padding.Right;
        Assert.InRange(app.BoundsOf(name).Width, content - PartB.DetailGap - PartB.Tolerance, content + PartB.Tolerance);
        var detail = DetailBlock(item);
        Assert.True(!detail.IsEffectivelyVisible || detail.Bounds.Width < 1 || detail.TextLayout.TextLines.Any(line => line.HasCollapsed), $"{fullName}'s detail is drawn whole beside the cut name.");
        Assert.Equal(fullName, AutomationProperties.GetName(item));
    }

    /// <summary>The text a cut text block draws: its line's runs, the ellipsis included.</summary>
    private static string DrawnText(TextBlock block) =>
        string.Concat(block.TextLayout.TextLines.SelectMany(line => line.TextRuns).Select(run => run.Text.ToString()));

    /// <summary>The item's background (<c>VcSelectionBrush</c> when selected), or null when transparent.</summary>
    private static Color? BackgroundOf(ListBoxItem item) => PresenterBackground(item);

    /// <summary>The background a control's template draws (its <c>PART_ContentPresenter</c>'s), or null when transparent or none.</summary>
    private static Color? PresenterBackground(Control control)
    {
        var colour = (control.GetVisualDescendants().OfType<ContentPresenter>().First(presenter => presenter.Name == "PART_ContentPresenter").Background as ISolidColorBrush)?.Color;
        return colour is { A: 0 } ? null : colour;
    }

    /// <summary>
    /// B6: the one open tooltip whole inside the window (phase 6's check, <c>Phase6Checks.W.Rows.cs</c>
    /// <c>AssertToolTipInWindow</c>), none of its texts cut.
    /// </summary>
    private static void AssertToolTipInWindow(ShellDriver app)
    {
        var tip = Assert.Single(app.Window.GetVisualDescendants().OfType<ToolTip>(), candidate => candidate.IsEffectivelyVisible);
        var bounds = app.BoundsOf(tip);
        var window = app.Window.ClientSize;
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= window.Width && bounds.Bottom <= window.Height, $"The tooltip at {bounds} is not inside the window.");
        Assert.All(tip.GetVisualDescendants().OfType<TextBlock>(), block => Assert.DoesNotContain(block.TextLayout.TextLines, line => line.HasCollapsed));
    }

    /// <summary>
    /// A token link in its disabled look (part A's <c>AssertLinkLook(link, enabled: false)</c>,
    /// <c>Phase7bChecks.A.Helpers.cs</c>): disabled, the theme's disabled foreground
    /// (<c>VcTextDisabledBrush</c>) and the arrow cursor.
    /// </summary>
    private static void AssertDisabledLink(Control link)
    {
        Assert.False(link.IsEffectivelyEnabled, $"{AutomationProperties.GetAutomationId(link)} is enabled.");
        Assert.Equal(PartA.Disabled, (link.GetVisualDescendants().OfType<TextBlock>().First().Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(PartA.ArrowCursor, link.Cursor?.ToString());
    }

    /// <summary>Selects all the focused box holds, with the platform's command modifier (Control headless).</summary>
    private static void PressSelectAll(ShellDriver app) =>
        app.PressKey(Key.A, app.CommandModifier == KeyModifiers.Meta ? RawInputModifiers.Meta : RawInputModifiers.Control);

    // ---- Git in a clone -----------------------------------------------------------------------------

    /// <summary>Runs git in <paramref name="folder"/> with the tests' isolated environment (as 7a's B6).</summary>
    private static async Task<GitResult> GitAsync(TempDirectory home, string folder, params string[] arguments)
    {
        var runner = new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem()) ?? throw new InvalidOperationException("The checks need git."));
        return await runner.RunAsync(new GitCommand(arguments) { WorkingDirectory = folder, Environment = TempRepo.IsolatedEnvironment(home.Path) }, Cancelled);
    }
}
