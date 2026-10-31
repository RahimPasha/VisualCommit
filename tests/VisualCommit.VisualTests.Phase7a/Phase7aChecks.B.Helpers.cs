using System.Globalization;
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
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part B's own helpers (docs/test-reports/phase-7a.md, part B's conventions): the start, the clone
/// form, the browser and its rows, the fake's requests and the log's lines since a mark, and every
/// method's end.
/// </summary>
public sealed partial class Phase7aChecksB
{
    private static readonly Regex LogLine = new(@"^\S+ \S+ \S+ \[(?<level>[A-Z]{3})\] (?<message>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex RequestId = new(@"\(request (?<id>FAKE-\d{4})\)", RegexOptions.CultureInvariant);

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>
    /// A picture of part B: before it, no visible text, automation value or name in the window holds
    /// a token; after it, no text is cut (part B's conventions).
    /// </summary>
    private static Screenshot ShotB(ShellDriver app, string name)
    {
        AssertNoTokenOnScreen(app);
        var screenshot = Shot(app, name);
        AssertNoClippedText(app);
        return screenshot;
    }

    /// <summary>No visible text, automation value or name holds one of part B's tokens; a failure names the control and the token's constant, never the token.</summary>
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
                ("automation value", control is TextBox valueBox ? ControlAutomationPeer.CreatePeerForElement(valueBox).GetProvider<IValueProvider>()?.Value : null),
            };
            foreach (var (what, text) in texts.Where(entry => entry.Text is not null))
            {
                foreach (var (tokenName, token) in PartB.Tokens)
                {
                    Assert.False(
                        text!.Contains(token, StringComparison.Ordinal),
                        $"The {what} of {AutomationProperties.GetAutomationId(control) ?? control.Name ?? control.GetType().Name} holds {tokenName}.");
                }
            }
        }
    }

    /// <summary>
    /// Every method's end, after its app has closed (part B's conventions): the tripwire for part B's
    /// tokens over the data folder, the copy's <c>hello/</c> and <c>tool/</c> and <paramref name="clones"/>
    /// with the session's call log; the fake's errors and the refused requests empty; no API request
    /// answered 400 or 415; nothing launched.
    /// </summary>
    private static void AssertCleanEnd(ShellDriver app, TempDirectory data, ServedHosting served, string? clones = null)
    {
        List<string> folders = [data.Path, served.Hello.Path, served.Hello.SiblingPath(Tool)];
        if (clones is not null)
        {
            folders.Add(clones);
        }

        foreach (var (_, token) in PartB.Tokens)
        {
            TokenTripwire.AssertAbsent(token, folders, app.Session.GitCalls);
        }

        Assert.Equal(0, app.RefusedRequests.Count);
        Assert.Empty(served.GitHub.Errors);
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
        Assert.Empty(app.Launcher.Requests);
    }

    /// <summary>
    /// The working tree <paramref name="folder"/> as git names it (<c>rev-parse --show-toplevel</c>),
    /// which is how the tab and the recent list keep it: the physical folder (macOS: <c>/private/var/…</c>
    /// for a temporary folder made as <c>/var/…</c>), full and without a trailing separator. Run with
    /// the copy's isolated environment, outside the app's call log.
    /// </summary>
    private static async Task<string> TopLevelAsync(ServedHosting served, string folder)
    {
        var topLevel = (await served.Hello.GitAsync("-C", folder, "rev-parse", "--show-toplevel")).StandardOutput.TrimEnd('\r', '\n');
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(topLevel));
    }

    /// <summary>The fake's API requests since <paramref name="mark"/> (<c>Requests.Count</c> read before the step).</summary>
    private static List<FakeGitHubRequest> RequestsSince(ServedHosting served, int mark) => [.. served.GitHub.Requests.Skip(mark)];

    /// <summary>The store's calls since <paramref name="mark"/> (<c>Calls.Count</c> read before the step).</summary>
    private static List<string> StoreCallsSince(ShellDriver app, int mark) => [.. app.Tokens.Calls.Skip(mark)];

    /// <summary>Waits until the fake has recorded <paramref name="count"/> requests of <paramref name="route"/> since <paramref name="mark"/> (it records a request after its answer).</summary>
    private static Task WaitForRequestsAsync(ShellDriver app, ServedHosting served, int mark, string route, int count = 1) =>
        app.WaitForAsync(
            () => served.GitHub.Requests.Skip(mark).Count(request => request.Route == route) >= count,
            $"the fake to record {count} request(s) of {route}");

    /// <summary>Asserts a <c>GET api/v3/user/repos</c> with the token, answered <paramref name="status"/>, octo's when 200.</summary>
    private static void AssertListRequest(FakeGitHubRequest request, int status, string? query = PartB.ListQuery)
    {
        Assert.Equal(("GET", "/api/v3/user/repos", PartB.ListRoute), (request.Method, request.Path, request.Route));
        Assert.True(request.TokenPresent, "The list's request carried no token.");
        Assert.Equal(status, request.Status);
        if (status == 200)
        {
            Assert.Equal(OctoLogin, request.Login);
        }

        if (query is not null)
        {
            Assert.Equal(query, request.Query.TrimStart('?'));
        }
    }

    /// <summary>Asserts that <paramref name="request"/> is <c>GET api/v3/user</c> with the token, answered 200 for octo.</summary>
    private static void AssertUserRequest(FakeGitHubRequest request)
    {
        Assert.Equal(("GET", "/api/v3/user", "user"), (request.Method, request.Path, request.Route));
        Assert.True(request.TokenPresent, "The sign-in's request carried no token.");
        Assert.Equal((200, OctoLogin), (request.Status, request.Login));
    }

    /// <summary>The log's lines since <paramref name="mark"/> as (level, message), Debug included.</summary>
    private static List<(string Level, string Message)> LogSince(ShellDriver app, int mark) =>
        [.. app.LogLinesSince(mark).Select(line => LogLine.Match(line)).Where(match => match.Success).Select(match => (match.Groups["level"].Value, match.Groups["message"].Value))];

    /// <summary>The <c>X-GitHub-Request-Id</c> the line beginning <paramref name="prefix"/> names (<c>FAKE-</c> and four digits).</summary>
    private static string RequestIdOf(IEnumerable<(string Level, string Message)> lines, string prefix)
    {
        var line = lines.Single(entry => entry.Message.StartsWith(prefix, StringComparison.Ordinal));
        var match = RequestId.Match(line.Message);
        Assert.True(match.Success, $"\"{line.Message}\" names no request id of the fake.");
        return match.Groups["id"].Value;
    }

    /// <summary>Asserts that the Warning <paramref name="message"/> is in <paramref name="lines"/> exactly once.</summary>
    private static void AssertWarningOnce(IReadOnlyList<(string Level, string Message)> lines, string message) =>
        Assert.True(
            lines.Count(entry => entry.Message == message) == 1 && lines.Single(entry => entry.Message == message).Level == "WRN",
            $"The Warning \"{message}\" is not in the log exactly once. The log since the mark:\n" + string.Join('\n', lines.Select(entry => $"[{entry.Level}] {entry.Message}")));

    private static string Format(string text, params object[] values) => string.Format(CultureInfo.InvariantCulture, text, values);

    // ---- The clone form ------------------------------------------------------------------------

    /// <summary>The active tab's welcome page.</summary>
    private static WelcomeViewModel Welcome(ShellDriver app) => app.Session.Shell.ActiveTab.Welcome;

    /// <summary>Clicks <c>CloneRepoButton</c> and waits until <c>CloneUrlBox</c> has the keyboard focus.</summary>
    private static async Task OpenFormAsync(ShellDriver app)
    {
        app.Click(app.FindByAutomationId("CloneRepoButton"));
        await app.WaitForAsync(() => app.FocusedAutomationId() == "CloneUrlBox", "the focus in the clone form's URL box");
        app.Settle();
    }

    private static Button GitHubButton(ShellDriver app) => app.Find<Button>("CloneFromGitHubButton");

    private static TextBox Box(ShellDriver app, string name) => app.Find<TextBox>(name);

    /// <summary>
    /// Asserts the clone form (part B's conventions, "The form"): open on the active tab, its boxes
    /// holding the given texts, no error line, and "From GitHub…" shown or collapsed. Collapsed, the
    /// URL box spans the form, x 284 to 676, the name box's edges; shown, the button ends at 676, as
    /// high as Browse, 6 right of the URL box, which still starts at 284, and enabled while no
    /// dialog is over the form.
    /// </summary>
    private static void AssertForm(ShellDriver app, bool button, string url = "", string name = "", string parent = "")
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
        var gitHub = GitHubButton(app);
        Assert.Equal(button, gitHub.IsEffectivelyVisible);
        Assert.Equal(button, welcome.ShowsGitHubButton);
        if (!button)
        {
            Assert.Equal(PartB.FormRight, urlBox.Right, PartB.Tolerance);
            return;
        }

        var bounds = app.BoundsOf(gitHub);
        // Under the browser the button's own command runs, so it is drawn disabled there; the report
        // asks it enabled only with no dialog over the form (B1, B6).
        if (app.Session.Services.DialogHost.Current is null)
        {
            Assert.True(gitHub.IsEffectivelyEnabled, "From GitHub… is disabled.");
        }

        Assert.Same(Application.Current!.FindResource("VcActionButton"), gitHub.Theme);
        Assert.Equal(PartB.FromGitHubButton, AutomationProperties.GetName(gitHub));
        Assert.Equal(PartB.FromGitHubButton, gitHub.GetVisualDescendants().OfType<TextBlock>().Single().Text);
        Assert.Null(ToolTip.GetTip(gitHub));
        Assert.Equal(PartB.FormRight, bounds.Right, PartB.Tolerance);
        Assert.Equal(PartB.ButtonGap, bounds.X - urlBox.Right, PartB.Tolerance);
        Assert.Equal(app.BoundsOf(app.Find<Button>("CloneBrowseButton")).Height, bounds.Height, PartB.Tolerance);
        Assert.Equal(urlBox.Center.Y, bounds.Center.Y, PartB.Tolerance);
        var urlRow = app.BoundsOf((Visual)Box(app, "CloneUrlBox").GetVisualParent()!);
        var parentRow = app.BoundsOf((Visual)Box(app, "CloneParentBox").GetVisualParent()!);
        Assert.Equal(parentRow.Height, urlRow.Height, PartB.Tolerance);
    }

    /// <summary>The texts the clone form shows, which phase 1's <c>13a</c> shows too.</summary>
    private static void AssertFormTexts(ShellDriver app)
    {
        var texts = TextsIn(app.Find<Control>("CloneForm"));
        foreach (var text in new[] { "Clone a repository", "Repository URL", "Parent folder", "Browse", "Folder name", "Clone" })
        {
            Assert.Contains(text, texts);
        }
    }

    /// <summary>
    /// "Add GitHub account" as it opens over the welcome page (B7 step 3, as part A's conventions,
    /// "The dialog", and A2's step 3 give it; part A's <c>AssertDialog</c> without the settings screen
    /// under it): alone in the dialog host; named and titled <c>Add GitHub account</c>; its card x 350
    /// to 750 at 1100×700, centred vertically; top to bottom the help, the link in the accent colour 6
    /// below it, the label <c>Token</c> and the box, empty, named <c>Token</c> and with the focus; no
    /// no-keychain warning, no status line and no "In …" line; <c>Cancel</c> enabled, then
    /// <c>Sign in</c> disabled in the primary look, right-aligned.
    /// </summary>
    private static void AssertSignInDialog(ShellDriver app, SignInDialogViewModel dialog)
    {
        var host = app.Session.Services.DialogHost;
        Assert.Equal([dialog], host.Open.Cast<DialogViewModel>());
        Assert.Same(dialog, host.Current);
        var view = app.SignInDialogView();
        Assert.Equal(PartA.DialogTitle, AutomationProperties.GetName(view));
        var card = app.BoundsOf(app.Window.GetVisualDescendants().OfType<Border>().First(border => border.Name == "SignInCard" && border.IsEffectivelyVisible));
        var (left, right) = PartA.DialogCard;
        Assert.Equal(left, card.Left, PartA.Tolerance);
        Assert.Equal(right, card.Right, PartA.Tolerance);
        Assert.Equal(app.Window.ClientSize.Height / 2, card.Center.Y, PartA.Tolerance);
        Assert.Equal(PartA.DialogTitle, ((TextBlock)app.FindByAutomationId(view, "DialogTitle")).Text);
        var help = (TextBlock)app.FindByAutomationId(view, "SignInHelp");
        Assert.Equal(PartA.Help, help.Text);
        var link = app.FindByAutomationId(view, "SignInTokenLink");
        var linkText = link.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.Equal(PartA.TokenLink, linkText.Text);
        Assert.Equal(PaletteOf(app).Accent, (linkText.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(PartA.LinkGap, app.BoundsOf(link).Top - app.BoundsOf(help).Bottom, PartA.Tolerance);
        var label = view.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == PartA.TokenLabel && block.IsEffectivelyVisible);
        var box = (TextBox)app.FindByAutomationId(view, "SignInToken");
        Assert.True(app.BoundsOf(label).Top > app.BoundsOf(link).Bottom && app.BoundsOf(box).Top > app.BoundsOf(label).Bottom, "The label and the box are not below the link in order.");
        Assert.Equal(PartA.TokenLabel, AutomationProperties.GetName(box));
        Assert.Equal(string.Empty, box.Text ?? string.Empty);
        Assert.Equal("SignInToken", app.FocusedAutomationId());
        var shown = view.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible).Select(AutomationProperties.GetAutomationId).ToHashSet();
        foreach (var id in new[] { "SignInNoKeychain", "SignInChecking", "SignInError", "SignInSaveFailed", "DialogRepository" })
        {
            Assert.False(shown.Contains(id), $"{id} shows.");
        }

        Assert.Equal([PartA.Cancel, PartA.SignIn], ButtonsOf(app, view));
        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.True(cancel.IsEffectivelyEnabled, "Cancel is disabled.");
        Assert.False(confirm.IsEffectivelyEnabled, "Sign in is enabled.");
        Assert.Same(Application.Current!.FindResource("VcPrimaryButton"), confirm.Theme);
        Assert.DoesNotContain("danger", confirm.Classes);
        Assert.True(app.BoundsOf(confirm).Right > app.BoundsOf(cancel).Right && app.BoundsOf(confirm).Right >= card.Right - 22, "The buttons are not right-aligned.");
    }

    // ---- The browser ---------------------------------------------------------------------------

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

    /// <summary>Waits until the browser shows with the keyboard focus in its filter; with <paramref name="loaded"/>, also until its load has ended.</summary>
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

    /// <summary>
    /// Asserts the browser (part B's conventions, "The browser"): the card 460 wide at x 320 to 780,
    /// centred vertically; the title and no repository line; the filter holding <paramref name="filter"/>,
    /// enabled, with the keyboard focus; the lines shown exactly as given, in their order under the
    /// list; Cancel enabled and Choose as given.
    /// </summary>
    private static void AssertBrowser(ShellDriver app, string filter, bool chooseEnabled, string? status = null, string? error = null, string? more = null, string? sso = null)
    {
        app.Settle();
        var view = BrowserView(app);
        Assert.Equal(PartB.DialogTitle, AutomationProperties.GetName(view));
        var card = app.BoundsOf(view.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("dialogCard")));
        Assert.Equal((PartB.CardLeft, PartB.CardRight, PartB.CardWidth), (card.X, card.Right, card.Width));
        Assert.Equal((DefaultSize.Height - card.Height) / 2, card.Y, PartB.Tolerance);
        var title = (TextBlock)InBrowser(app, "CloneFromGitHubTitle");
        Assert.Equal(PartB.DialogTitle, title.Text);
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), block => block.Classes.Contains("dialogRepository") && block.IsEffectivelyVisible);
        var box = (TextBox)InBrowser(app, "CloneFromGitHubFilter");
        Assert.Equal(filter, box.Text ?? string.Empty);
        Assert.Equal(PartB.Filter, box.PlaceholderText);
        Assert.True(box.IsEffectivelyEnabled, "The filter is disabled.");
        Assert.Equal("CloneFromGitHubFilter", app.FocusedAutomationId());

        var list = app.BoundsOf(InBrowser(app, "CloneFromGitHubList"));
        var below = list.Bottom;
        foreach (var (id, text) in new[] { ("CloneFromGitHubStatus", status), ("CloneFromGitHubError", error), ("CloneFromGitHubMore", more), ("CloneFromGitHubSso", sso) })
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
            if (id != "CloneFromGitHubError")
            {
                Assert.Equal(PartB.NoteSize, line.FontSize);
            }

            var bounds = app.BoundsOf(line);
            Assert.True(bounds.Top >= below - 0.5, $"{id} at {bounds} is not below what comes before it (ending at {below}).");
            below = bounds.Bottom;
        }

        Assert.True(InBrowser(app, "CancelButton").IsEffectivelyEnabled, "Cancel is disabled.");
        Assert.Equal(PartB.Choose, AutomationProperties.GetName(InBrowser(app, "ConfirmButton")));
        Assert.Equal(chooseEnabled, InBrowser(app, "ConfirmButton").IsEffectivelyEnabled);
    }

    /// <summary>The list's height inside its border.</summary>
    private static double ListHeight(ShellDriver app) => app.BoundsOf(InBrowser(app, "CloneFromGitHubList")).Height;

    /// <summary>The list's realised items in the window, top to bottom.</summary>
    private static List<ListBoxItem> Items(ShellDriver app) =>
        [.. InBrowser(app, "CloneFromGitHubList").GetVisualDescendants().OfType<ListBoxItem>().Where(item => item.IsEffectivelyVisible).OrderBy(item => app.BoundsOf(item).Y)];

    /// <summary>The item whose full name is <paramref name="fullName"/>.</summary>
    private static ListBoxItem Item(ShellDriver app, string fullName) =>
        Items(app).Single(item => AutomationProperties.GetName(item) == fullName);

    /// <summary>
    /// Asserts the browser's rows: the model's and the drawn ones exactly <paramref name="rows"/> in
    /// order, each <c>CloneFromGitHubItem</c> named by its full name, its help text its detail, its
    /// tooltip the full name, a line break and the detail, 28 high, the name in the primary text
    /// colour and the detail 12 after it in the secondary one at 11.5, whole; none selected unless
    /// <paramref name="selected"/>.
    /// </summary>
    private static void AssertRows(ShellDriver app, IReadOnlyList<(string FullName, string? Detail)> rows, string? selected = null)
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
            var blocks = item.GetVisualDescendants().OfType<TextBlock>().ToList();
            var name = blocks.First(block => block.Text == fullName);
            Assert.Equal(palette.TextPrimary, (name.Foreground as ISolidColorBrush)?.Color);
            Assert.False(name.TextLayout.TextLines.Any(line => line.HasCollapsed), $"{fullName} is cut.");
            var shown = blocks.Single(block => block.Name == "CloneFromGitHubItemDetail");
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

    /// <summary>Whether the list scrolls: its content taller than what it shows.</summary>
    private static bool ListScrolls(ShellDriver app)
    {
        var scroll = InBrowser(app, "CloneFromGitHubList").GetVisualDescendants().OfType<ScrollViewer>().First();
        return scroll.Extent.Height > scroll.Viewport.Height + 0.5;
    }

    /// <summary>The selected item's background (<c>VcSelectionBrush</c> when selected).</summary>
    private static Color? BackgroundOf(ListBoxItem item) =>
        (item.GetVisualDescendants().OfType<ContentPresenter>().First(presenter => presenter.Name == "PART_ContentPresenter").Background as ISolidColorBrush)?.Color;

    /// <summary>Selects all the focused box holds, with the platform's command modifier (Control headless).</summary>
    private static void PressSelectAll(ShellDriver app) =>
        app.PressKey(Key.A, app.CommandModifier == KeyModifiers.Meta ? RawInputModifiers.Meta : RawInputModifiers.Control);

    // ---- The account ---------------------------------------------------------------------------

    private static string AccountsFile(TempDirectory data) => Path.Combine(data.Path, "accounts.json");

    /// <summary>The one record of <c>accounts.json</c>, or null when there is none (or it is being replaced).</summary>
    private static JsonElement? Record(TempDirectory data)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(AccountsFile(data)));
            return document.RootElement.TryGetProperty("accounts", out var accounts) && accounts.GetArrayLength() == 1 ? accounts[0].Clone() : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Asserts <c>accounts.json</c>'s one record: octo's, with <c>useForGit</c> true and <paramref name="tokenState"/>.</summary>
    private static void AssertRecord(TempDirectory data, ServedHosting served, string tokenState)
    {
        var record = Record(data) ?? throw new Xunit.Sdk.XunitException("accounts.json holds no single record.");
        Assert.Equal(
            ["avatarUrl", "host", "id", "login", "name", "provider", "tokenState", "useForGit"],
            record.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(("github", PartB.Host(served.GitHub.Port), OctoLogin, OctoId, OctoName), (record.GetProperty("provider").GetString(), record.GetProperty("host").GetString(), record.GetProperty("login").GetString(), record.GetProperty("id").GetInt64(), record.GetProperty("name").GetString()));
        Assert.True(record.GetProperty("useForGit").GetBoolean(), "useForGit is false.");
        Assert.Equal(tokenState, record.GetProperty("tokenState").GetString());
    }
}
