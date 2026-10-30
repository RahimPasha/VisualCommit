using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using P = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part P's helpers (docs/test-reports/phase-7a.md, part P's conventions): a picture with no token
/// on screen and no text cut, a snapshot of the whole panel (the section's, <c>Section</c>, is the
/// support's, which part G shares), a row's control by
/// number or message kind, the waits for a list (D322) and for a state without one (open point 34),
/// the API requests, the store's calls and the log's lines since a mark, and the end of every method.
/// </summary>
public sealed partial class Phase7aChecksP
{
    private static readonly Regex LogLine = new(@"^\S+ \S+ \S+ \[(?<level>[A-Z]{3})\] (?<message>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex RequestId = new(@"\(request (?<id>FAKE-\d{4})\)", RegexOptions.CultureInvariant);

    /// <summary>
    /// A picture of part P: before it, no visible text, automation value or name in the window holds
    /// one of part P's tokens; after it, no text is cut but by an ellipsis whose whole text is in its
    /// tooltip or an ancestor's (part P's conventions, the shared conventions' <c>AssertNoClippedText</c>).
    /// </summary>
    private static Screenshot ShotP(ShellDriver app, string name, bool keepPointer = false)
    {
        AssertNoTokenOnScreen(app);
        var screenshot = Shot(app, name, keepPointer);
        AssertNoClippedText(app);
        return screenshot;
    }

    /// <summary>No visible text, automation value or name holds one of part P's tokens; a failure names the control and the token's constant, never the token.</summary>
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
                ("tooltip", ToolTip.GetTip(control) as string),
            };
            foreach (var (what, text) in texts.Where(entry => entry.Text is not null))
            {
                foreach (var (tokenName, token) in P.Tokens)
                {
                    Assert.False(
                        text!.Contains(token, StringComparison.Ordinal),
                        $"The {what} of {AutomationProperties.GetAutomationId(control) ?? control.Name ?? control.GetType().Name} holds {tokenName}.");
                }
            }
        }
    }

    /// <summary>
    /// The active tab's whole left panel as lines (P8): a header's title and count; under it each
    /// row indented two spaces a level, a branch row with its <c>#n</c> when it shows one, a message
    /// row as "[Kind] text", and " [selected]" after a selected branch or pull request.
    /// </summary>
    private static List<string> Panel(ShellDriver app)
    {
        app.Settle();
        return
        [
            .. app.Session.Shell.ActiveTab.LeftPanel.Rows.Select(row => row switch
            {
                RefSectionRow section => $"{section.Text} {section.CountText}",
                RefItemRow item => $"{new string(' ', 2 * item.Depth)}{item.Text}{(item.ShowsPullRequest ? " " + item.PullRequestText : string.Empty)}{(item.IsSelected ? " [selected]" : string.Empty)}",
                PullRequestRow pull => $"{new string(' ', 2 * pull.Depth)}{pull.Text}{(pull.IsSelected ? " [selected]" : string.Empty)}",
                PullRequestMessageRow message => $"{new string(' ', 2 * message.Depth)}[{message.Kind}] {message.Text}",
                _ => row.Text,
            }),
        ];
    }

    private static List<PullRequestRow> PullRequestRows(ShellDriver app) =>
        [.. app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<PullRequestRow>()];

    /// <summary>The control of the row <paramref name="which"/> picks, scrolled into view and laid out.</summary>
    private static async Task<Control> RowAsync(ShellDriver app, Func<RefListRow, bool> which, string what)
    {
        var list = app.Find<ItemsControl>("RefList");
        Control? found = null;
        await app.WaitForAsync(
            () =>
            {
                var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
                var index = rows.FindIndex(row => which(row));
                if (index < 0)
                {
                    return false;
                }

                found = list.GetVisualDescendants().OfType<Button>()
                    .FirstOrDefault(button => ReferenceEquals(button.DataContext, rows[index]) && button.IsEffectivelyVisible && button.Bounds.Height > 0
                        && AutomationProperties.GetAutomationId(button) is "PullRequestItem" or "PullRequestMessage" or "Section");
                if (found is null)
                {
                    list.ScrollIntoView(index);
                    return false;
                }

                return app.BoundsOf(found).Bottom <= app.BoundsOf(list).Bottom + 0.5;
            },
            what);
        return found!;
    }

    private static Task<Control> PullRequestAsync(ShellDriver app, int number) =>
        RowAsync(app, row => row is PullRequestRow pull && pull.Number == number, $"pull request #{number}'s row");

    private static Task<Control> MessageAsync(ShellDriver app) =>
        RowAsync(app, row => row is PullRequestMessageRow, "the section's message row");

    private static Task<Control> HeaderAsync(ShellDriver app) =>
        RowAsync(app, row => row is RefSectionRow { Section: RefSection.PullRequests }, "the section's header");

    /// <summary>The section's <c>PullRequestItem</c> controls the panel's list has laid out, in their order on screen.</summary>
    private static List<Button> PullRequestControls(ShellDriver app)
    {
        app.Settle();
        return
        [
            .. app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsEffectivelyVisible && AutomationProperties.GetAutomationId(button) == "PullRequestItem")
                .OrderBy(button => app.BoundsOf(button).Top),
        ];
    }

    /// <summary>A control's named part (<c>PullRequestDraft</c>, <c>RefPullRequest</c>, <c>RefName</c> …).</summary>
    private static TextBlock Part(Control row, string name) =>
        row.GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Name == name);

    /// <summary>
    /// P6 step 1: the four rows' controls in order, each whole inside the panel's list and
    /// <c>PullRequestDraft</c> shown on #2's only.
    /// </summary>
    private static void AssertHelloRowControls(ShellDriver app)
    {
        var controls = PullRequestControls(app);
        Assert.Equal(P.HelloRows.Select(row => row.Name), controls.Select(AutomationProperties.GetName));
        var list = app.BoundsOf(app.Find<ItemsControl>("RefList"));
        foreach (var control in controls)
        {
            var bounds = app.BoundsOf(control);
            Assert.True(
                bounds.Top >= list.Top - 0.5 && bounds.Bottom <= list.Bottom + 0.5 && bounds.Left >= list.Left - 0.5 && bounds.Right <= list.Right + 0.5,
                $"{AutomationProperties.GetName(control)} at {bounds} is not whole in the panel's list at {list}.");
        }

        Assert.Equal(
            P.HelloRows.Select(row => row.Draft),
            controls.Select(control => Part(control, "PullRequestDraft").IsEffectivelyVisible));
        Assert.Equal(P.Draft, Part(controls[2], "PullRequestDraft").Text);
    }

    /// <summary>Asserts a menu's height (±2) as the report quotes it.</summary>
    private static void AssertMenuHeight(MenuSnapshot menu, double height) =>
        Assert.True(
            Math.Abs(menu.Bounds.Height - height) <= 2,
            string.Create(CultureInfo.InvariantCulture, $"The menu is {menu.Bounds.Height} high; the report gives {height} ±2."));

    /// <summary>D322: the tab's <c>{repo}: listed {count} open pull requests of {listed}</c> since <paramref name="logMark"/> and its avatars' line once since the start (<c>AvatarsShown</c>), then quiet watchers.</summary>
    private static async Task WaitForListedAsync(ShellDriver app, string repository, int count, string listed, int logMark)
    {
        await WaitForListLineAsync(app, repository, count, listed, logMark);

        // From WP-V's merge (open point 29, design 17 items 3 and 6): the tab's avatars once since the
        // start, hello's 2 authors, tool's 1 (open point 42).
        var avatars = await app.WaitForLogLineAsync(AppLogFile.AvatarsShown(repository), mark: 0);
        Assert.Equal(repository == Phase7aExpectations.Tool ? "1" : "2", AppLogFile.AvatarsShown(repository).Match(avatars).Groups["count"].Value);
        await app.WaitForWatchersQuietAsync();
    }

    /// <summary>
    /// D322: the tab's <c>{repo}: listed {count} open pull requests of {listed}</c> since
    /// <paramref name="logMark"/>, alone: the list of a tab in the background (P13 step 1), whose
    /// avatars' line only the shown tab writes (11.3).
    /// </summary>
    private static async Task WaitForListLineAsync(ShellDriver app, string repository, int count, string listed, int logMark)
    {
        var line = await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed(repository), mark: logMark);
        var match = AppLogFile.PullRequestsListed(repository).Match(line);
        Assert.Equal((count.ToString(CultureInfo.InvariantCulture), listed), (match.Groups["count"].Value, match.Groups["repository"].Value));
    }

    /// <summary>Open point 34: a state without a list waits for the view model's state, the section's message row, then quiet watchers.</summary>
    private static async Task WaitForStateAsync(ShellDriver app, RepositoryViewModel repository, PullRequestsState state, PullRequestMessageKind message)
    {
        await app.WaitForAsync(() => repository.HostingState.PullRequests == state, $"the hosting {state}");
        await app.WaitForAsync(() => repository.LeftPanel.Rows.OfType<PullRequestMessageRow>().Any(row => row.Kind == message), $"the {message} row");
        await app.WaitForWatchersQuietAsync();
    }

    /// <summary>The API requests since <paramref name="mark"/>, leaving out the avatars' (routes <c>commits:</c> and <c>avatar:</c>, 7.9).</summary>
    private static List<FakeGitHubRequest> ApiSince(ServedHosting served, int mark) =>
        [.. served.GitHub.Requests.Skip(mark).Where(request => request.Route is not { } route || !(route.StartsWith("commits:", StringComparison.Ordinal) || route.StartsWith("avatar:", StringComparison.Ordinal)))];

    /// <summary>Asserts one <c>GET api/v3/repos/octo/hello/pulls</c>: with a token, <paramref name="status"/>, the first page's query and, when the report says, whether it was conditional.</summary>
    private static void AssertPullsRequest(FakeGitHubRequest request, int status, bool? conditional = null)
    {
        Assert.Equal(("GET", "pulls:octo/hello", P.PullsQuery), (request.Method, request.Route, request.Query));
        Assert.True(request.TokenPresent, "The request of pulls:octo/hello carried no token.");
        Assert.Equal(status, request.Status);
        if (conditional is { } expected)
        {
            Assert.Equal(expected, request.Conditional);
        }
    }

    /// <summary>The vertical extent and viewport of the panel's list's scroll viewer: a scroll bar shows when the first is the larger.</summary>
    private static (double Extent, double Viewport) PanelScroll(ShellDriver app)
    {
        app.Settle();
        var list = app.Find<ItemsControl>("RefList");
        var viewer = list.FindAncestorOfType<ScrollViewer>() ?? list.GetVisualDescendants().OfType<ScrollViewer>().First();
        return (viewer.Extent.Height, viewer.Viewport.Height);
    }

    /// <summary>The store's calls since <paramref name="mark"/> (<c>Calls.Count</c> read before the step).</summary>
    private static List<string> StoreCallsSince(ShellDriver app, int mark) => [.. app.Tokens.Calls.Skip(mark)];

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

    /// <summary><c>accounts.json</c>'s one record's <c>tokenState</c>, or null when there is no file or no single record.</summary>
    private static string? TokenStateOf(TempDirectory data)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(data.Path, "accounts.json")));
            return document.RootElement.TryGetProperty("accounts", out var accounts) && accounts.GetArrayLength() == 1
                ? accounts[0].GetProperty("tokenState").GetString()
                : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The refs, with what each points at, and the working tree's state of <paramref name="repository"/>, read outside the app's call log (P12: no ref or file changed).</summary>
    private static async Task<(string Refs, string Status)> GitStateAsync(TempRepo repository) =>
        ((await repository.GitAsync("for-each-ref", "--format=%(objectname) %(refname)")).StandardOutput,
         (await repository.GitAsync("status", "--porcelain=v1", "--untracked-files=all")).StandardOutput);

    private static List<string> OpenedUrls(ShellDriver app) =>
        [.. app.Launcher.Requests.Where(request => request.Kind == FileLaunchKind.OpenUrl).Select(request => request.FullPath)];

    /// <summary>
    /// Every method's end (part P's conventions): the app closed, the tripwire for the three tokens,
    /// the fake's errors and refusals none, no 400 or 415, and the launcher's requests exactly the
    /// <c>OpenUrl</c> launches of <paramref name="launches"/>.
    /// </summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting served, string[] launches, params string[] more)
    {
        var calls = app.Session.GitCalls;
        app.Dispose();
        foreach (var (_, token) in P.Tokens)
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.Hello.Path, served.Tool.Path, .. more], calls);
        }

        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(app.RefusedRequests.Addresses);
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
        Assert.Equal(launches.Select(url => $"{FileLaunchKind.OpenUrl} {url}"), app.Launcher.Requests.Select(request => $"{request.Kind} {request.FullPath}"));
    }
}
