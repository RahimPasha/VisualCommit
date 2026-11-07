// Part P's helpers, copied from phase 7a's Phase7aChecks.P.Helpers.cs (the panel's snapshot, a row's
// control by number or message kind, the waits for a list and for a state without one, the store's
// calls and the log's lines since a mark, the end of every method) and Phase7aChecks.G.Helpers.cs (the
// graph, a label's place in the window) with 7b's names: the three fakes' requests since a mark (as
// parts A's and B's), the provider's words, and the sign-in through Settings, Accounts of 14.4.
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;
using P = VisualCommit.VisualTests.Phase7b.Phase7bExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part P's helpers (docs/test-reports/phase-7b.md, part P's conventions): a picture with no token on
/// screen and no text cut, the whole panel, a row's control, the waits for a list (D322) and for a
/// state without one (7a's open point 34), the three fakes' requests, the store's calls and the log
/// since a mark, the graph's labels, the sign-in of methods 1 and 2, and the end of every method.
/// </summary>
public sealed partial class Phase7bChecksP
{
    private static readonly Regex LogLine = new(@"^\S+ \S+ \S+ \[(?<level>[A-Z]{3})\] (?<message>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex RequestId = new(@"\(request (?<id>[A-Za-z0-9:._-]+)\)", RegexOptions.CultureInvariant);

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>Each fake's <c>Requests.Count</c> before a step.</summary>
    private readonly record struct Marks(int AzureDevOps, int GitLab, int GitHub);

    private static Marks Mark(ServedHosting7b served) => new(served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count, served.GitHub.Requests.Count);

    private static List<FakeServiceRequest> AzureSince(ServedHosting7b served, Marks mark) => [.. served.AzureDevOps.Requests.Skip(mark.AzureDevOps)];

    private static List<FakeServiceRequest> GitLabSince(ServedHosting7b served, Marks mark) => [.. served.GitLab.Requests.Skip(mark.GitLab)];

    private static List<FakeGitHubRequest> GitHubSince(ServedHosting7b served, Marks mark) => [.. served.GitHub.Requests.Skip(mark.GitHub)];

    /// <summary>"Requests none": none in any of the three fakes since <paramref name="mark"/>.</summary>
    private static void AssertNoRequests(ServedHosting7b served, Marks mark)
    {
        Assert.Equal([], AzureSince(served, mark).Select(Describe));
        Assert.Equal([], GitLabSince(served, mark).Select(Describe));
        Assert.Equal([], GitHubSince(served, mark).Select(request => $"{request.Method} {request.Route} {request.Status}"));
    }

    private static string Describe(FakeServiceRequest request) => $"{request.Method} {request.Route} {request.Status}";

    /// <summary>Asserts one request's method, path, route, query, credential form, status and login.</summary>
    private static void AssertRequest(FakeServiceRequest request, string path, string route, string query, FakeAuthorizationForm form, int status, string? login)
    {
        Assert.Equal(("GET", path, route, query, form, status), (request.Method, request.Path, request.Route, request.Query, request.Authorization, request.Status));
        if (login is not null)
        {
            Assert.Equal(login, request.Login);
        }
    }

    /// <summary>Ada's login as Azure DevOps' fake records it: the organization and her id.</summary>
    private static string AdaAzureLogin(ServedHosting7b served) => $"{Organization}/{served.AzureDevOps.User(Organization, AdaName).Id:D}";

    /// <summary>A sign-in's GitLab requests and the list (P1 step 1, P9 step 1): user, A3, A4, the fork's lookup, each 200, Bearer, ada.</summary>
    private static void AssertGitLabSignInList(IReadOnlyList<FakeServiceRequest> requests, ServedHosting7b served)
    {
        Assert.Equal(P.SignInRoutesOf(HostingProvider.GitLab).Select(route => route + " 200"), requests.Select(request => $"{request.Route} {request.Status}"));
        AssertRequest(requests[0], P.GitLabUserPath, P.GitLabUserRoute, string.Empty, FakeAuthorizationForm.Bearer, 200, AdaLogin);
        AssertRequest(requests[1], P.GitLabProjectPath, P.GitLabProjectRoute, string.Empty, FakeAuthorizationForm.Bearer, 200, AdaLogin);
        AssertRequest(requests[2], P.GitLabListPath, P.GitLabListRoute, P.GitLabListQuery, FakeAuthorizationForm.Bearer, 200, AdaLogin);
        AssertRequest(requests[3], GitLabForkPath(served), P.GitLabForkRoute, string.Empty, FakeAuthorizationForm.Bearer, 200, AdaLogin);
    }

    /// <summary>The fork's lookup by its number (design 7.4): <c>/api/v4/projects/&lt;ada/web-app's number&gt;</c>, never by its path.</summary>
    private static string GitLabForkPath(ServedHosting7b served) => $"/api/v4/projects/{served.GitLab.Project(WebAppFork).Id}";

    /// <summary>
    /// A picture of part P: before it, no visible text, automation value or name in the window holds
    /// one of part P's tokens; after it, no text is cut but by an ellipsis whose whole text is in its
    /// tooltip (part P's conventions).
    /// </summary>
    private static Screenshot ShotP(ShellDriver app, string name, bool keepPointer = false)
    {
        AssertNoTokenOnScreen(app);
        var screenshot = Shot(app, name, keepPointer);
        AssertNoClippedText(app);
        return screenshot;
    }

    /// <summary>
    /// <see cref="ShotP"/> for a picture that holds one of the two message rows that part P's
    /// conventions let be cut with "…" at 260 although their tooltips are not their text (the lead's
    /// ruling of 2026-11-07, the report's "Changed in this report after its first commit"): D356's row
    /// (P8: p08a, p08b, p10d), whose tooltip is the reason and names both organizations, and GitLab's
    /// <c>More than 300 open merge requests</c> (P6: p06a), whose tooltip is the action and which loses
    /// only the end of "requests" under the header <c>Merge requests</c>. The audit runs in full and
    /// lets through the one finding on <paramref name="cutRow"/>'s text alone, where that row is cut;
    /// any other finding fails the picture.
    /// </summary>
    private static Screenshot ShotPCutRow(ShellDriver app, string name, string cutRow, bool keepPointer = false)
    {
        AssertNoTokenOnScreen(app);
        var screenshot = Shot(app, name, keepPointer);
        var allowed = $"\"{cutRow}\" is shortened with an ellipsis.";
        Assert.Equal([], LayoutAudit.FindClippedText(app.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true).Where(problem => problem != allowed));
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
    /// The active tab's whole left panel as lines (7a's P8): a header's title and count; under it each
    /// row indented two spaces a level, a branch row with its <c>!n</c> when it shows one, a message row
    /// as "[Kind] text", and " [selected]" after a selected branch or request.
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

    private static Task<Control> RequestAsync(ShellDriver app, int number) =>
        RowAsync(app, row => row is PullRequestRow pull && pull.Number == number, $"request {number}'s row");

    private static Task<Control> MessageAsync(ShellDriver app) =>
        RowAsync(app, row => row is PullRequestMessageRow, "the section's message row");

    private static Task<Control> HeaderAsync(ShellDriver app) =>
        RowAsync(app, row => row is RefSectionRow { Section: RefSection.PullRequests }, "the section's header");

    /// <summary>The section's <c>PullRequestItem</c> controls the panel's list has laid out, in their order on screen.</summary>
    private static List<Button> RequestControls(ShellDriver app)
    {
        app.Settle();
        return
        [
            .. app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsEffectivelyVisible && AutomationProperties.GetAutomationId(button) == "PullRequestItem")
                .OrderBy(button => app.BoundsOf(button).Top),
        ];
    }

    /// <summary>A control's named part (<c>PullRequestDraft</c>, <c>RefPullRequest</c>, <c>RefName</c>, <c>PullRequestMessageText</c> …).</summary>
    private static TextBlock Part(Control row, string name) =>
        row.GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Name == name);

    /// <summary>
    /// P1 step 1: the section's rows as the report gives them: names in order, authors, drafts and
    /// tooltips, none selected, no message row, each control whole in the panel's list and
    /// <c>PullRequestDraft</c> shown on the draft's alone.
    /// </summary>
    private static void AssertRows(ShellDriver app, HostingProvider provider, IReadOnlyList<P.Row> rows)
    {
        Assert.Equal([$"v {P.TitleOf(provider)} {rows.Count}", .. rows.Select(row => row.Name)], Section(app));
        Assert.Equal(rows.Select(row => (row.Author, row.Draft, row.ToolTip)), PullRequestRows(app).Select(row => (row.Author, row.IsDraft, row.ToolTip)));
        Assert.Equal(rows.Select(row => P.NumberOf(provider, row.Number)), PullRequestRows(app).Select(row => row.NumberText));
        Assert.DoesNotContain(PullRequestRows(app), row => row.IsSelected);
        var controls = RequestControls(app);
        Assert.Equal(rows.Select(row => row.Name), controls.Select(AutomationProperties.GetName));
        var list = app.BoundsOf(app.Find<ItemsControl>("RefList"));
        foreach (var control in controls)
        {
            var bounds = app.BoundsOf(control);
            Assert.True(
                bounds.Top >= list.Top - 0.5 && bounds.Bottom <= list.Bottom + 0.5 && bounds.Left >= list.Left - 0.5 && bounds.Right <= list.Right + 0.5,
                $"{AutomationProperties.GetName(control)} at {bounds} is not whole in the panel's list at {list}.");
            Assert.Equal(P.NumberOf(provider, rows[controls.IndexOf(control)].Number), Part(control, "PullRequestNumber").Text);
        }

        Assert.Equal(rows.Select(row => row.Draft), controls.Select(control => Part(control, "PullRequestDraft").IsEffectivelyVisible));
    }

    /// <summary>Asserts a menu's height (±2) as the report quotes it.</summary>
    private static void AssertMenuHeight(MenuSnapshot menu, double height) =>
        Assert.True(
            Math.Abs(menu.Bounds.Height - height) <= 2,
            string.Create(CultureInfo.InvariantCulture, $"The menu is {menu.Bounds.Height} high; the report gives {height} ±2."));

    /// <summary>The open menu is <paramref name="lines"/> whole, <paramref name="highlighted"/> highlighted, 4a's shape and the report's height.</summary>
    private static MenuSnapshot AssertWholeMenu(ShellDriver app, string[] lines, string highlighted, double height)
    {
        var menu = app.Menu();
        AssertMenu(menu, lines);
        AssertHighlighted(menu, highlighted);
        AssertMenuShape(app, menu);
        AssertMenuHeight(menu, height);
        return menu;
    }

    /// <summary>Right-clicks the section's header at its centre and asserts the menu there (P10).</summary>
    private static async Task<MenuSnapshot> SectionMenuAsync(ShellDriver app, string[] lines, string highlighted, double height)
    {
        var header = await HeaderAsync(app);
        var pointer = app.BoundsOf(header).Center;
        app.RightClick(header);
        var menu = AssertWholeMenu(app, lines, highlighted, height);
        AssertMenuAtPointer(menu, pointer);
        return menu;
    }

    /// <summary>D322: the tab's <c>{repo}: listed {count} open pull requests of {listed}</c> since <paramref name="logMark"/>, then quiet watchers (the new providers write no avatars line, D349).</summary>
    private static async Task WaitForListedAsync(ShellDriver app, string repository, int count, string listed, int logMark)
    {
        var line = await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed(repository), mark: logMark);
        var match = AppLogFile.PullRequestsListed(repository).Match(line);
        Assert.Equal((count.ToString(CultureInfo.InvariantCulture), listed), (match.Groups["count"].Value, match.Groups["repository"].Value));
        await app.WaitForWatchersQuietAsync();
    }

    /// <summary>7a's open point 34: a state without a list waits for the view model's state, the section's message row, then quiet watchers.</summary>
    private static async Task WaitForStateAsync(ShellDriver app, RepositoryViewModel repository, PullRequestsState state, PullRequestMessageKind message)
    {
        await app.WaitForAsync(() => repository.HostingState.PullRequests == state, $"the hosting {state}");
        await app.WaitForAsync(() => repository.LeftPanel.Rows.OfType<PullRequestMessageRow>().Any(row => row.Kind == message), $"the {message} row");
        await app.WaitForWatchersQuietAsync();
    }

    /// <summary>
    /// Methods 1 and 2's sign-in through Settings, Accounts, as WP-R's "Requests and create" launches
    /// sign in (14.4; open point 28): the provider's dialog (Azure DevOps' prefilled from the tab), the
    /// token typed, Enter, the dialog closed and the account signed in, Esc, the screen closed.
    /// </summary>
    private static async Task SignInThroughSettingsAsync(ShellDriver app, HostingProvider provider, string token)
    {
        var screen = await app.OpenAccountsAsync();
        var dialog = await app.OpenSignInDialogAsync(provider);
        if (provider == HostingProvider.AzureDevOps)
        {
            // P1 method 2: the organization prefilled, the focus in SignInToken.
            Assert.Equal(Organization, dialog.Organization);
            await app.WaitForAsync(() => app.FocusedAutomationId() == "SignInToken", "the focus in SignInToken");
        }

        app.TypeToken(token);
        app.PressKey(Key.Enter);
        await app.WaitForSignInCheckAsync(dialog);
        Assert.True(dialog.IsClosed, "The sign-in dialog did not close after the sign-in.");
        await app.WaitForAccountAsync(provider, account => account is { State: AccountState.SignedIn });
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
    }

    /// <summary>The vertical extent and viewport of the panel's list's scroll viewer: a scroll bar shows when the first is the larger.</summary>
    private static (double Extent, double Viewport) PanelScroll(ShellDriver app)
    {
        app.Settle();
        var list = app.Find<ItemsControl>("RefList");
        var viewer = list.FindAncestorOfType<ScrollViewer>() ?? list.GetVisualDescendants().OfType<ScrollViewer>().First();
        return (viewer.Extent.Height, viewer.Viewport.Height);
    }

    private static List<string> StoreCallsSince(ShellDriver app, int mark) => [.. app.Tokens.Calls.Skip(mark)];

    /// <summary>The log's lines since <paramref name="mark"/> as (level, message), Debug included.</summary>
    private static List<(string Level, string Message)> LogSince(ShellDriver app, int mark) =>
        [.. app.LogLinesSince(mark).Select(line => LogLine.Match(line)).Where(match => match.Success).Select(match => (match.Groups["level"].Value, match.Groups["message"].Value))];

    /// <summary>The request id the line beginning <paramref name="prefix"/> names.</summary>
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

    private static List<string> OpenedUrls(ShellDriver app) =>
        [.. app.Launcher.Requests.Where(request => request.Kind == FileLaunchKind.OpenUrl).Select(request => request.FullPath)];

    /// <summary>The graph of the active tab (7a's part G).</summary>
    private static CommitGraphControl Graph(ShellDriver app) => app.Find<CommitGraphControl>("GraphRows");

    /// <summary>Where the active graph draws row <paramref name="row"/>'s label of <paramref name="fullText"/>, in the window (as 7a's <c>GraphLabelBounds</c>, by the label's whole name).</summary>
    private static Rect LabelBounds(ShellDriver app, int row, string fullText)
    {
        app.Settle();
        var graph = Graph(app);
        var label = graph.LabelsInRow(row).FirstOrDefault(candidate => candidate.FullText == fullText)
            ?? throw new InvalidOperationException($"Row {row} has no label of {fullText}; it has: {string.Join(", ", graph.LabelsInRow(row).Select(candidate => candidate.FullText))}.");
        var topLeft = graph.TranslatePoint(label.Bounds.TopLeft, app.Window) ?? throw new InvalidOperationException("The graph is not in the window.");
        return new Rect(topLeft, label.Bounds.Size);
    }

    /// <summary>
    /// P1: the graph's labels per row of the prepared clone: the request's local label on
    /// <see cref="P.RequestRow"/> first with its badge drawn whole after the icon inside the label, the
    /// remote label beside it shown or folded into <c>+1</c> without a badge; the draft's remote label
    /// first on <see cref="P.DraftRow"/> with its badge; no badge on rows 0, 1 and 4; every label inside
    /// the Branch / Tag column.
    /// </summary>
    private static void AssertBadges(ShellDriver app, string requestLabel, P.Badge request, string draftLabel, P.Badge draft)
    {
        app.Settle();
        var graph = Graph(app);
        var row = graph.LabelsInRow(P.RequestRow);
        Assert.Equal((requestLabel, RefLabelKind.LocalBranch), (row[0].FullText, row[0].Kind));
        Assert.Equal(new PullRequestBadge(request.Number, request.Title, request.IsDraft, request.Provider), row[0].Badge);
        Assert.Equal(2, row.Count);
        Assert.True(row[1].IsOverflow ? row[1].Text == "+1" : row[1].FullText == "origin/" + requestLabel, $"Row {P.RequestRow}'s second label is {row[1].Text}.");
        Assert.Null(row[1].Badge);
        var badge = graph.BadgeInRow(P.RequestRow, 0) ?? throw new InvalidOperationException("The request's label draws no badge.");
        Assert.True(badge.Number.Left >= badge.Icon.Right && badge.Number.Right <= row[0].Bounds.Right + 0.5, $"The badge's number at {badge.Number} is not after the icon {badge.Icon} inside the label {row[0].Bounds}.");
        var drafts = graph.LabelsInRow(P.DraftRow);
        Assert.Equal(draftLabel, drafts[0].FullText);
        Assert.Equal(new PullRequestBadge(draft.Number, draft.Title, draft.IsDraft, draft.Provider), drafts[0].Badge);
        Assert.NotNull(graph.BadgeInRow(P.DraftRow, 0));
        foreach (var other in new[] { 0, 1, 4 })
        {
            Assert.DoesNotContain(graph.LabelsInRow(other), label => label.Badge is not null);
        }

        foreach (var index in Enumerable.Range(0, 5))
        {
            Assert.All(graph.LabelsInRow(index), label => Assert.True(label.Bounds.Right <= P.BranchColumnWidth + 0.5, $"Row {index}'s label {label.Text} ends at {label.Bounds.Right}, past the column."));
        }

        // Open point 26's record: each label's text, full text, kind, ref and badge as PartP writes them (no pixel).
        var table = P.LabelsOf(request.Provider);
        foreach (var index in Enumerable.Range(0, table.Count))
        {
            Assert.Equal(
                table[index].Select(label => (label.Text, label.FullText, label.Kind, label.RefName, label.Badge)),
                graph.LabelsInRow(index).Select(label => (label.Text, label.FullText, label.Kind.ToString(), label.RefName, label.Badge?.Number ?? 0)));
        }
    }

    /// <summary>No label of any row the graph holds shows a badge.</summary>
    private static void AssertNoBadge(ShellDriver app)
    {
        app.Settle();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var graph = Graph(app);
        for (var row = 0; row < Math.Min(repository.Graph.Count, 50); row++)
        {
            Assert.DoesNotContain(graph.LabelsInRow(row), label => label.Badge is not null);
        }
    }

    /// <summary>The titles of the operations that end in the session from now on, with their outcomes and messages, in order.</summary>
    private static List<VisualCommit.Core.Operations.OperationRecord> EndedOperations(ShellDriver app)
    {
        var ended = new List<VisualCommit.Core.Operations.OperationRecord>();
        app.Session.Operations.Added += (_, record) =>
        {
            lock (ended)
            {
                ended.Add(record);
            }
        };
        return ended;
    }

    /// <summary>
    /// Every method's end (part P's conventions), after the app has closed and only when every step
    /// passed, so that a step's failure is the one reported: the tripwire for each token the method
    /// used, over the data folder and the copy's three clones with the session's call log; each fake's
    /// errors and the harness's refusals none; no request answered 400 or 415; the launcher's requests
    /// exactly the <c>OpenUrl</c> launches of <paramref name="launches"/> and the clipboard's texts
    /// exactly <paramref name="clipboard"/> (both with the fakes' ports).
    /// </summary>
    private void End(ShellDriver app, TempDirectory data, ServedHosting7b served, string[] tokens, string[] launches, string[] clipboard)
    {
        var calls = app.Session.GitCalls;
        foreach (var token in tokens)
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.MobileApp.Path, served.Manifest.Path, served.WebApp.Path], calls);
        }

        Assert.Empty(served.AzureDevOps.Errors);
        Assert.Empty(served.GitLab.Errors);
        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(app.RefusedRequests.Addresses);
        Assert.DoesNotContain(served.AzureDevOps.Requests, request => request.Status is 400 or 415);
        Assert.DoesNotContain(served.GitLab.Requests, request => request.Status is 400 or 415);
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
        Assert.Equal(launches.Select(url => $"{FileLaunchKind.OpenUrl} {WithPorts(url, served)}"), app.Launcher.Requests.Select(request => $"{request.Kind} {request.FullPath}"));
        Assert.Equal(clipboard.Select(url => WithPorts(url, served)), app.Clipboard.Texts);
    }
}
