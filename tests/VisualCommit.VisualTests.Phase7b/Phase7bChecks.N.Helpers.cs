// Part N's helpers (docs/test-reports/phase-7b.md, part N's conventions), copied with 7b's names from
// phase 7a's Phase7aChecks.N.Helpers.cs (a branch row's menu by its path, the form opened from a menu
// item, its texts, its creating and idle states, the dialogs recorder, the queue's records since a mark,
// the waits for OperationEnded and for PullRequestsListed n times since a mark, the POST's parsed body,
// git in a bare folder) and from part P's Phase7bChecks.P.Helpers.cs (the three fakes' requests since a
// mark, no token on screen, the sign-in through Settings, Accounts, the store's calls and the log since
// a mark, the state without a list, the method's end, AssertBadges' core as AssertOtherBadges) and from
// part A's Phase7bChecks.A.Helpers.cs (AssertDialog as AssertSignInFirst, over the window instead of the
// settings screen; AssertLinkLook), which are private to their classes.
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json;
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
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7b.Phase7bExpectations;
using N = VisualCommit.VisualTests.Phase7b.Phase7bExpectations.PartN;
using P = VisualCommit.VisualTests.Phase7b.Phase7bExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7b;

/// <summary>
/// Part N's helpers (part N's conventions): the fakes' requests since a mark, the form's opening,
/// texts and places by the 8-px rule and the cut rule, its creating and idle states, the operation's
/// end, the list lines, the POST's body, git in a bare folder and every method's end.
/// </summary>
public sealed partial class Phase7bChecksN
{
    private const HostingProvider GitLab = HostingProvider.GitLab;
    private const HostingProvider Azure = HostingProvider.AzureDevOps;

    /// <summary>The form's controls that 7a's 12.4 creating state disables; Cancel apart (7a's open point 59).</summary>
    private static readonly string[] FormInputs = ["PullRequestBaseChooseButton", "PullRequestTitle", "PullRequestDescription", "PullRequestDraft", "ConfirmButton"];

    /// <summary>The rows Into and below, which a shown head note moves down (13.4).</summary>
    private static readonly HashSet<string> BelowFrom = [N.Into, "PullRequestBase", "PullRequestBaseRepository", "PullRequestBaseChooseButton", "PullRequestTitle", "PullRequestDescription", "PullRequestDraft", "CancelButton", "ConfirmButton"];

    private static readonly HashSet<string> Buttons = ["CancelButton", "ConfirmButton"];

    // ---- The fakes' requests (part P's helpers) ------------------------------------------------------

    /// <summary>Each fake's <c>Requests.Count</c> before a step.</summary>
    private readonly record struct Marks(int AzureDevOps, int GitLab, int GitHub);

    private static Marks Mark(ServedHosting7b served) => new(served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count, served.GitHub.Requests.Count);

    private static List<FakeServiceRequest> AzureSince(ServedHosting7b served, Marks mark) => [.. served.AzureDevOps.Requests.Skip(mark.AzureDevOps)];

    private static List<FakeServiceRequest> GitLabSince(ServedHosting7b served, Marks mark) => [.. served.GitLab.Requests.Skip(mark.GitLab)];

    private static List<FakeGitHubRequest> GitHubSince(ServedHosting7b served, Marks mark) => [.. served.GitHub.Requests.Skip(mark.GitHub)];

    /// <summary>The requests of <paramref name="p"/>'s fake since <paramref name="mark"/>; the other two fakes' none.</summary>
    private static List<FakeServiceRequest> RequestsSince(ServedHosting7b served, HostingProvider p, Marks mark)
    {
        Assert.Equal([], (p == GitLab ? AzureSince(served, mark) : GitLabSince(served, mark)).Select(Describe));
        Assert.Equal([], GitHubSince(served, mark).Select(request => $"{request.Method} {request.Route} {request.Status}"));
        return p == GitLab ? GitLabSince(served, mark) : AzureSince(served, mark);
    }

    /// <summary>"Requests none": none in any of the three fakes since <paramref name="mark"/>.</summary>
    private static void AssertNoRequests(ServedHosting7b served, Marks mark)
    {
        Assert.Equal([], AzureSince(served, mark).Select(Describe));
        Assert.Equal([], GitLabSince(served, mark).Select(Describe));
        Assert.Equal([], GitHubSince(served, mark).Select(request => $"{request.Method} {request.Route} {request.Status}"));
    }

    private static string Describe(FakeServiceRequest request) => $"{request.Method} {request.Route} {request.Status}";

    /// <summary>
    /// Every request of <paramref name="requests"/> sent as the scenario's signed-in user (part N's
    /// "Around the POST": "Every request <c>Bearer</c> with <c>Login</c> <c>ada</c>, or
    /// <c>BasicEmptyUser</c> with <c>Login</c> Ada's"; the lists' requests as part P's scenario facts
    /// give them; the lead's ruling R4 on hunt 3): GitLab's <c>Bearer</c> with <c>Login</c> <c>ada</c>;
    /// Azure DevOps' <c>BasicEmptyUser</c> with <c>Login</c> Ada's (the organization and her id) on
    /// every request but <c>areas</c>, whose <c>Login</c> the report never gives (the fake reads no
    /// credential there).
    /// </summary>
    private static void AssertSentAsAda(ServedHosting7b served, HostingProvider p, IEnumerable<FakeServiceRequest> requests) =>
        Assert.All(requests, request =>
        {
            Assert.Equal(p == GitLab ? FakeAuthorizationForm.Bearer : FakeAuthorizationForm.BasicEmptyUser, request.Authorization);
            if (p == GitLab || request.Route != P.AzureDevOpsAreasRoute)
            {
                Assert.True(
                    (p == GitLab ? AdaLogin : AdaAzureLogin(served)) == request.Login,
                    $"{request.Method} {request.Route} {request.Status} was sent as {request.Login ?? "nobody"}, not as {(p == GitLab ? AdaLogin : "Ada")}.");
            }
        });

    private static string AdaAzureLogin(ServedHosting7b served) => $"{Organization}/{served.AzureDevOps.User(Organization, AdaName).Id:D}";

    /// <summary>
    /// A sign-in's requests and the list since its mark (methods 1 and 2 after step 1, as part P's P1;
    /// N8 after methods 3's step 4 and 4's step 3), each 200 in order with its path and query as part
    /// P's <c>AssertGitLabSignInList</c> and <c>AssertAzureSignInList</c> assert them (Azure DevOps'
    /// connection with <c>api-version=7.1-preview.1</c>, <c>areas</c> as part B's B1), then
    /// <see cref="AssertSentAsAda"/>.
    /// </summary>
    private static void AssertSignInRequests(ServedHosting7b served, HostingProvider p, IReadOnlyList<FakeServiceRequest> requests)
    {
        Assert.Equal(P.SignInRoutesOf(p).Select(route => "GET " + route + " 200"), requests.Select(Describe));
        (string Path, string Query)[] expected = p == GitLab
            ? [(P.GitLabUserPath, string.Empty), (P.GitLabProjectPath, string.Empty), (P.GitLabListPath, P.GitLabListQuery), ($"/api/v4/projects/{served.GitLab.Project(WebAppFork).Id}", string.Empty)]
            : [(P.AzureDevOpsConnectionPath, P.AzureDevOpsConnectionQuery), ($"/{Organization}/_apis/resourceAreas/{PartA.CoreAreaId}", PartA.AzureDevOpsSignInQuery), (P.AzureDevOpsRepositoryPath, P.AzureDevOpsRepositoryQuery), (P.AzureDevOpsListPath, P.AzureDevOpsListQuery)];
        Assert.Equal(expected, requests.Select(request => (request.Path, request.Query.TrimStart('?'))));
        AssertSentAsAda(served, p, requests);
    }

    /// <summary>The create's POST since <paramref name="mark"/>: exactly one.</summary>
    private static FakeServiceRequest PostSince(ServedHosting7b served, HostingProvider p, Marks mark) =>
        Assert.Single(p == GitLab ? GitLabSince(served, mark) : AzureSince(served, mark), request => request.Route == N.CreateRouteOf(p));

    /// <summary>The POST's JSON body as its members and their raw JSON, in the order sent.</summary>
    private static List<(string Member, string Json)> BodyOf(FakeServiceRequest post)
    {
        using var json = JsonDocument.Parse(post.Body!);
        return [.. json.RootElement.EnumerateObject().Select(member => (member.Name, member.Value.GetRawText()))];
    }

    /// <summary>The POST's body exactly <paramref name="members"/> (any order) and none of <paramref name="absent"/>.</summary>
    private static void AssertBody(FakeServiceRequest post, IReadOnlyList<(string Member, string Json)> members, IReadOnlyList<string> absent)
    {
        var body = BodyOf(post);
        Assert.Equal(members.OrderBy(member => member.Member, StringComparer.Ordinal), body.OrderBy(member => member.Member, StringComparer.Ordinal));
        Assert.DoesNotContain(body, member => absent.Contains(member.Member));
    }

    /// <summary>A body of <paramref name="p"/>'s own form (the scenario facts): GitLab's four members, Azure DevOps' five.</summary>
    private static (string Member, string Json)[] BodyFor(HostingProvider p, string head, string title, string description, bool draft) => p == GitLab
        ? [("source_branch", Json(head)), ("target_branch", Json("main")), ("title", Json(draft ? "Draft: " + title : title)), ("description", Json(description))]
        : [("sourceRefName", Json("refs/heads/" + head)), ("targetRefName", Json("refs/heads/main")), ("title", Json(title)), ("description", Json(description)), ("isDraft", draft ? "true" : "false")];

    private static string Json(string text) => JsonSerializer.Serialize(text);

    private static IReadOnlyList<AuthRemoteRequest> GitRequestsOf(ServedHosting7b served, HostingProvider p) => p == GitLab ? served.GitLab.GitRequests : served.AzureDevOps.GitRequests;

    // ---- Pictures and the screen (part P's helpers) --------------------------------------------------

    /// <summary>A picture of part N: no token on screen before it; after it no text cut but by an ellipsis whose whole text is its tooltip.</summary>
    private static Screenshot ShotN(ShellDriver app, string name, bool keepPointer = false)
    {
        AssertNoTokenOnScreen(app);
        var screenshot = Shot(app, name, keepPointer);
        AssertNoClippedText(app);
        return screenshot;
    }

    /// <summary>No visible text, automation value or name holds one of the scenario's tokens; a failure names the control and the token's constant, never the token.</summary>
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

    // ---- Signing in, waits and the log (part P's and 7a's part N's helpers) --------------------------

    private static string TokenOf(ServedHosting7b served, HostingProvider p) => p == GitLab ? served.GitLabToken : served.AzureDevOpsToken;

    private static string KeyOf(ServedHosting7b served, HostingProvider p) => "save " + WithPorts(p == GitLab ? P.GitLabKey : P.AzureDevOpsKey, served);

    /// <summary>Methods 1 and 2's sign-in through Settings, Accounts (part P's): the provider's dialog, the token typed, Enter, signed in, Esc, the screen closed.</summary>
    private static async Task SignInThroughSettingsAsync(ShellDriver app, HostingProvider provider, string token)
    {
        var screen = await app.OpenAccountsAsync();
        var dialog = await app.OpenSignInDialogAsync(provider);
        if (provider == Azure)
        {
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

    /// <summary>D322: <paramref name="times"/> lines <c>{repo}: listed {count} open pull requests of {full name}</c> since <paramref name="logMark"/>, then quiet watchers.</summary>
    private static async Task WaitForListedAsync(ShellDriver app, HostingProvider p, int count, int logMark, int times = 1)
    {
        var listed = AppLogFile.PullRequestsListed(N.RepositoryOf(p));
        for (var occurrence = 1; occurrence <= times; occurrence++)
        {
            var line = await app.WaitForLogLineAsync(listed, occurrence, logMark);
            var match = listed.Match(line);
            Assert.Equal((count.ToString(CultureInfo.InvariantCulture), N.FullNameOf(p)), (match.Groups["count"].Value, match.Groups["repository"].Value));
        }

        await app.WaitForWatchersQuietAsync();
    }

    /// <summary>The number of D322's lines for <paramref name="p"/>'s repository since <paramref name="logMark"/>.</summary>
    private static int ListedSince(ShellDriver app, HostingProvider p, int logMark) =>
        app.LogLinesSince(logMark).Count(line => AppLogFile.PullRequestsListed(N.RepositoryOf(p)).IsMatch(line));

    /// <summary>A state without a list (part P's): the hosting's state, the section's message row, then quiet watchers.</summary>
    private static async Task WaitForStateAsync(ShellDriver app, RepositoryViewModel repository, PullRequestsState state, PullRequestMessageKind message)
    {
        await app.WaitForAsync(() => repository.HostingState.PullRequests == state, $"the hosting {state}");
        await app.WaitForAsync(() => repository.LeftPanel.Rows.OfType<PullRequestMessageRow>().Any(row => row.Kind == message), $"the {message} row");
        await app.WaitForWatchersQuietAsync();
    }

    /// <summary>The operation's end since <paramref name="logMark"/>: <c>{repo}: {Title}: {Outcome} after …</c>.</summary>
    private static Task<string> WaitForEndAsync(ShellDriver app, HostingProvider p, string head, int logMark, OperationOutcome outcome) =>
        app.WaitForLogLineAsync(AppLogFile.OperationEnded(N.RepositoryOf(p), N.OperationOf(p, head).Title, outcome), mark: logMark);

    private static List<string> StoreCallsSince(ShellDriver app, int mark) => [.. app.Tokens.Calls.Skip(mark)];

    /// <summary>The log's messages since <paramref name="mark"/> with their level (<c>[INF] message</c>).</summary>
    private static List<string> LogSince(ShellDriver app, int mark) =>
        [.. app.LogLinesSince(mark).Select(line => line.IndexOf(" [", StringComparison.Ordinal) is var at and >= 0 ? line[(at + 1)..] : line)];

    /// <summary>Exactly one line since <paramref name="mark"/> is the Info <paramref name="message"/>.</summary>
    private static void AssertInfoOnce(ShellDriver app, int mark, string message) =>
        Assert.True(LogSince(app, mark).Count(line => line == "[INF] " + message) == 1, $"The Info \"{message}\" is not in the log once:\n" + string.Join('\n', LogSince(app, mark)));

    /// <summary>Exactly one Warning since <paramref name="mark"/> matches <paramref name="pattern"/> (<c>{id}</c> any request id).</summary>
    private static void AssertWarningOnce(ShellDriver app, int mark, string pattern)
    {
        var regex = new System.Text.RegularExpressions.Regex("^\\[WRN\\] " + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\{id}", "[A-Za-z0-9:._-]+", StringComparison.Ordinal) + "$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        Assert.True(LogSince(app, mark).Count(regex.IsMatch) == 1, $"The Warning \"{pattern}\" is not in the log once:\n" + string.Join('\n', LogSince(app, mark)));
    }

    // ---- Menus and the form (7a's part N helpers) ---------------------------------------------------

    /// <summary>Right-clicks the row at <paramref name="path"/> at its centre and returns its menu once shown, placed by the pointer (<see cref="AssertMenuPlaced"/>).</summary>
    private static async Task<MenuSnapshot> MenuOfAsync(ShellDriver app, params string[] path)
    {
        var pointer = app.BoundsOf(await app.RefRowAsync(path)).Center;
        app.RightClick(pointer);
        await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "the row's menu");
        var menu = app.Menu();
        AssertMenuPlaced(app, menu, pointer);
        return menu;
    }

    /// <summary>
    /// Part N's conventions, Menus: a menu opened by the pointer at <paramref name="pointer"/> has its
    /// top-left 2 right of and below it (<see cref="Phase7bChecks.AssertMenuAtPointer"/>) where it fits
    /// there, 4 inside the window; where it does not (a 597-px or 519-px branch menu at a row low in
    /// the panel), phase 3's 4a rule (<c>Phase3Checks.Support.cs</c>' <c>AssertMenuPlaced</c>, copied):
    /// flipped to the pointer's other side past the right or bottom edge, then moved to stay 4 inside
    /// the window (±2).
    /// </summary>
    private static void AssertMenuPlaced(ShellDriver app, MenuSnapshot menu, Point pointer)
    {
        var window = app.Window.ClientSize;
        var size = menu.Bounds.Size;
        var gap = WindowLayout.MenuPointerGap;
        var inset = WindowLayout.MenuEdgeInset;
        if (pointer.X + gap + size.Width <= window.Width - inset && pointer.Y + gap + size.Height <= window.Height - inset)
        {
            AssertMenuAtPointer(menu, pointer);
            return;
        }

        var x = pointer.X + gap + size.Width > window.Width ? pointer.X - gap - size.Width : pointer.X + gap;
        var y = pointer.Y + gap + size.Height > window.Height ? pointer.Y - gap - size.Height : pointer.Y + gap;
        x = Math.Max(Math.Min(x, window.Width - inset - size.Width), inset);
        y = Math.Max(Math.Min(y, window.Height - inset - size.Height), inset);
        Assert.True(
            Math.Abs(menu.Bounds.X - x) <= 2 && Math.Abs(menu.Bounds.Y - y) <= 2,
            string.Create(CultureInfo.InvariantCulture, $"The menu's top-left is {menu.Bounds.TopLeft}; 4a places a {size} menu opened at {pointer} at ({x}, {y})."));
    }

    /// <summary>The open menu is <paramref name="lines"/>, <paramref name="highlighted"/> highlighted, phase 3's shape and the report's height (±2).</summary>
    private static void AssertWholeMenu(ShellDriver app, string[] lines, string highlighted, double height)
    {
        var menu = app.Menu();
        AssertMenu(menu, lines);
        AssertHighlighted(menu, highlighted);
        AssertMenuShape(app, menu);
        Assert.True(Math.Abs(menu.Bounds.Height - height) <= 2, string.Create(CultureInfo.InvariantCulture, $"The menu is {menu.Bounds.Height} high; the report gives {height} ±2."));
    }

    /// <summary>Drags <c>local-work</c> onto <c>Remotes › origin › main</c> (7a's N5 step 4) and waits for the drop's menu, placed by the release point (<see cref="AssertMenuPlaced"/>, phase 3's 07b).</summary>
    private static async Task DropLocalWorkOnOriginMainAsync(ShellDriver app)
    {
        // 24 px left of the row's centre, where the last right-click on it was: a press there within
        // the double-tap time would count as a double-click and check local-work out.
        var source = app.BoundsOf(await app.RefRowAsync("Local branches", N.LocalWork)).Center - new Point(24, 0);
        var target = app.BoundsOf(await app.RefRowAsync("Remotes", "origin", "main")).Center;
        app.PressMouse(source);
        app.MoveMouseHeld(source + new Point(0, 12), steps: 3);
        app.MoveMouseHeld(target, steps: 6);
        app.ReleaseMouse(target);
        await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "the drop's menu");
        AssertMenuPlaced(app, app.Menu(), target);
    }

    /// <summary>Clicks <paramref name="p"/>'s create item in the open menu and waits for the form with Into <paramref name="baseText"/>.</summary>
    private static async Task<CreatePullRequestDialogViewModel> OpenFormFromMenuAsync(ShellDriver app, HostingProvider p, string baseText = N.Base)
    {
        app.ClickMenuItem(N.CreateItemOf(p));
        return await WaitForFormAsync(app, baseText);
    }

    private static async Task<CreatePullRequestDialogViewModel> WaitForFormAsync(ShellDriver app, string baseText = N.Base)
    {
        var form = await app.WaitForDialogAsync<CreatePullRequestDialogViewModel>();
        await app.WaitForAsync(() => form.BaseText == baseText, $"the form's base {baseText}");
        app.Settle();
        return form;
    }

    private static string? Text(ShellDriver app, string id) => ((TextBlock)app.FindByAutomationId(id)).Text;

    private static bool Shown(ShellDriver app, string id) => app.FindByAutomationId(id).IsEffectivelyVisible;

    /// <summary>
    /// The form's texts (13.4): its title and the view's automation name, no repository line, From and
    /// Into with their repositories and tooltips, the head's note, the title box, the error line and
    /// the existing button, and GitLab's two lines shown as <paramref name="p"/> and Draft give them.
    /// </summary>
    private static void AssertForm(ShellDriver app, CreatePullRequestDialogViewModel form, HostingProvider p, string head, string headRepository, string? note, string baseText, string baseRepository, string title, string? error = null, int? existing = null)
    {
        Assert.Equal(N.DialogTitleOf(p), form.Title);
        Assert.Equal(N.DialogTitleOf(p), Text(app, "CreatePullRequestDialogTitle"));
        Assert.Equal(N.DialogTitleOf(p), AutomationProperties.GetName(app.FindByAutomationId("CreatePullRequestDialog")));
        Assert.False(form.ShowsRepository);
        Assert.False(Shown(app, "DialogRepository"));
        Assert.Equal((head, headRepository), (Text(app, "PullRequestHead"), Text(app, "PullRequestHeadRepository")));
        Assert.Equal((head, headRepository), (ToolTip.GetTip(app.FindByAutomationId("PullRequestHead")) as string, ToolTip.GetTip(app.FindByAutomationId("PullRequestHeadRepository")) as string));
        Assert.Equal(note is not null, Shown(app, "PullRequestHeadNote"));
        Assert.Equal(note, form.HeadNote);
        Assert.Equal((baseText, baseRepository), (Text(app, "PullRequestBase"), Text(app, "PullRequestBaseRepository")));
        Assert.Equal((baseText, baseRepository), (ToolTip.GetTip(app.FindByAutomationId("PullRequestBase")) as string, ToolTip.GetTip(app.FindByAutomationId("PullRequestBaseRepository")) as string));
        Assert.Equal(N.Choose, ((Button)app.FindByAutomationId("PullRequestBaseChooseButton")).Content);
        Assert.Equal(title, ((TextBox)app.FindByAutomationId("PullRequestTitle")).Text);
        Assert.Equal(error, form.Error);
        Assert.Equal(error is not null, Shown(app, "PullRequestError"));
        if (error is not null)
        {
            Assert.Equal(error, Text(app, "PullRequestError"));
        }

        Assert.Equal(existing, form.ExistingNumber);
        Assert.Equal(existing is not null, Shown(app, "PullRequestOpenExisting"));
        if (existing is { } number)
        {
            Assert.Equal(N.OpenExistingOf(p, number), ((Button)app.FindByAutomationId("PullRequestOpenExisting")).Content);
        }

        Assert.Equal(p == GitLab, Shown(app, "PullRequestQuickActionsNote"));
        if (p == GitLab)
        {
            Assert.Equal(N.QuickActionsNote, Text(app, "PullRequestQuickActionsNote"));
            Assert.Contains("settingsNote", app.FindByAutomationId("PullRequestQuickActionsNote").Classes);
        }

        Assert.Equal(p == GitLab && form.IsDraft, Shown(app, "PullRequestSentTitle"));
    }

    /// <summary>A new form (7a's): the focus in the title with the caret at its end, the description empty with its placeholder, Draft unticked, Choose…, Cancel and Create enabled, Create without a tooltip.</summary>
    private static async Task AssertNewFormAsync(ShellDriver app, CreatePullRequestDialogViewModel form)
    {
        await app.WaitForAsync(() => app.FocusedAutomationId() == "PullRequestTitle", "the focus in the title");
        var title = (TextBox)app.FindByAutomationId("PullRequestTitle");
        Assert.Equal((title.Text ?? string.Empty).Length, title.CaretIndex);
        var description = (TextBox)app.FindByAutomationId("PullRequestDescription");
        Assert.Equal((string.Empty, N.DescriptionPlaceholder), (description.Text ?? string.Empty, description.PlaceholderText));
        Assert.Equal((string.Empty, false), (form.Description, form.IsDraft));
        Assert.False(((CheckBox)app.FindByAutomationId("PullRequestDraft")).IsChecked);
        Assert.All(["PullRequestBaseChooseButton", "CancelButton", "ConfirmButton"], id => Assert.True(app.FindByAutomationId(id).IsEffectivelyEnabled, $"{id} is disabled."));
        Assert.Null(ToolTip.GetTip(app.FindByAutomationId("ConfirmButton")));
    }

    /// <summary>The form creating (part N's conventions): every control disabled, <c>Creating…</c>; Cancel enabled only while Create pushes.</summary>
    private static void AssertCreating(ShellDriver app, CreatePullRequestDialogViewModel form, bool cancelEnabled)
    {
        Assert.True(form.IsCreating);
        Assert.Equal(N.CreatingButton, form.CreateText);
        Assert.All(FormInputs, id => Assert.False(app.FindByAutomationId(id).IsEffectivelyEnabled, $"{id} is enabled while creating."));
        Assert.Equal(cancelEnabled, app.FindByAutomationId("CancelButton").IsEffectivelyEnabled);
    }

    /// <summary>The form ready or failed: every control enabled, <c>Create</c>.</summary>
    private static void AssertIdle(ShellDriver app, CreatePullRequestDialogViewModel form)
    {
        Assert.False(form.IsCreating);
        Assert.Equal(N.Create, form.CreateText);
        Assert.All([.. FormInputs, "CancelButton"], id => Assert.True(app.FindByAutomationId(id).IsEffectivelyEnabled, $"{id} is disabled."));
    }

    private static Border Card(ShellDriver app) =>
        app.FindByAutomationId("CreatePullRequestDialog").GetVisualDescendants().OfType<Border>().First(border => border.Name == "CreatePullRequestDialogCard");

    /// <summary>A text's natural width: laid out in the control's font with no width limit (the cut rule).</summary>
    private static double NaturalWidth(TextBlock block)
    {
        var copy = new TextBlock { Text = block.Text, FontFamily = block.FontFamily, FontSize = block.FontSize, FontWeight = block.FontWeight, FontStyle = block.FontStyle, FontStretch = block.FontStretch };
        copy.Measure(Size.Infinity);
        return copy.DesiredSize.Width;
    }

    private static bool IsCut(TextBlock block) => block.TextLayout.TextLines.Any(line => line.HasCollapsed);

    /// <summary>
    /// The form's places (part N's conventions, "The form"; ±1): the card at <paramref name="cardLeft"/>,
    /// 460 wide and centred vertically; <c>FormLayout</c>'s places relative to it but the rows'
    /// children's x and width, each Into-and-below place lower by the head note's height plus 4 when
    /// shown, Draft and below by GitLab's quick-actions note's, the buttons and the card's bottom by
    /// the sent title's; the rows' children by the 8-px rule and the cut rule, from the controls.
    /// Returns the card's height.
    /// </summary>
    private static double AssertFormPlaces(ShellDriver app, double cardLeft)
    {
        app.Settle();
        var card = Card(app);
        var origin = app.BoundsOf(card);
        var labels = card.GetVisualDescendants().OfType<TextBlock>().ToList();
        double LineShift(string id) => Shown(app, id) ? app.BoundsOf(app.FindByAutomationId(id)).Height + N.LineGap : 0;
        var headShift = LineShift("PullRequestHeadNote");
        var noteShift = LineShift("PullRequestQuickActionsNote");
        var sentShift = LineShift("PullRequestSentTitle");
        Visual PartOf(string name) => name is N.From or N.Into ? labels.Single(label => label.Text == name) : name == "CreatePullRequestDialogCard" ? card : app.FindByAutomationId(name);
        var problems = new List<string>();
        void Near(string what, double actual, double expected)
        {
            if (Math.Abs(actual - expected) > N.Tolerance)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{what} is {actual:0.##}; expected {expected:0.##} ±1."));
            }
        }

        Near("The card's left", origin.X, cardLeft);
        Near("The card's top", origin.Y, (app.Window.Bounds.Height - origin.Height) / 2);
        foreach (var (name, x, y, width, height) in N.FormLayout)
        {
            var bounds = app.BoundsOf(PartOf(name));
            var shift = (BelowFrom.Contains(name) ? headShift : 0) + (name == "PullRequestDraft" || Buttons.Contains(name) ? noteShift : 0) + (Buttons.Contains(name) ? sentShift : 0);
            if (name == "CreatePullRequestDialogCard")
            {
                Near("The card's width", bounds.Width, width);
                Near("The card's height", bounds.Height, height + headShift + noteShift + sentShift);
                continue;
            }

            Near($"{name}'s y", bounds.Y - origin.Y, y + shift);
            Near($"{name}'s height", bounds.Height, height);
            if (!N.RowChildren.Contains(name))
            {
                Near($"{name}'s x", bounds.X - origin.X, x);
                Near($"{name}'s width", bounds.Width, width);
            }
        }

        // The rows' children (13.4's DockPanels): each 8 right of the one before, each as wide as its text.
        var head = (TextBlock)app.FindByAutomationId("PullRequestHead");
        var headRepository = (TextBlock)app.FindByAutomationId("PullRequestHeadRepository");
        var baseText = (TextBlock)app.FindByAutomationId("PullRequestBase");
        var baseRepository = (TextBlock)app.FindByAutomationId("PullRequestBaseRepository");
        var choose = app.FindByAutomationId("PullRequestBaseChooseButton");
        var columnLeft = origin.X + N.FormLayout.Single(part => part.Name == "PullRequestHead").X;
        var columnRight = cardLeft + (N.ColumnRight - N.CardLeft);
        Near("PullRequestHead's x", app.BoundsOf(head).X, columnLeft);
        Near("PullRequestHead's width", app.BoundsOf(head).Width, NaturalWidth(head));
        Near("PullRequestBase's x", app.BoundsOf(baseText).X, columnLeft);
        Near("PullRequestBase's width", app.BoundsOf(baseText).Width, NaturalWidth(baseText));
        Near("PullRequestHeadRepository's x", app.BoundsOf(headRepository).X, app.BoundsOf(head).Right + N.Gap);
        Near("PullRequestBaseRepository's x", app.BoundsOf(baseRepository).X, app.BoundsOf(baseText).Right + N.Gap);
        Near("PullRequestBaseChooseButton's width", app.BoundsOf(choose).Width, N.ChooseWidth);
        Near("PullRequestBaseChooseButton's x", app.BoundsOf(choose).X, app.BoundsOf(baseRepository).Right + N.Gap);
        AssertCutRule(app, headRepository, N.ColumnWidth - app.BoundsOf(head).Width - N.Gap, columnRight, null, problems);
        AssertCutRule(app, baseRepository, N.ColumnWidth - app.BoundsOf(baseText).Width - N.ChooseWidth - 2 * N.Gap, columnRight, choose, problems);
        Assert.True(problems.Count == 0, "The form's places are not part N's:\n" + string.Join('\n', problems));
        return origin.Height;
    }

    /// <summary>
    /// The cut rule (part N's conventions): a row's repository text is one collapsed line exactly when
    /// its natural width passes <paramref name="room"/> (within 1 not asserted either way); while cut,
    /// the row's last child (<c>Choose…</c>, or the text itself on From) ends at the column's right;
    /// while whole, the text is its natural width. Its tooltip is its whole text either way.
    /// </summary>
    private static void AssertCutRule(ShellDriver app, TextBlock text, double room, double columnRight, Control? choose, List<string> problems)
    {
        var natural = NaturalWidth(text);
        var bounds = app.BoundsOf(text);
        if (ToolTip.GetTip(text) as string != text.Text)
        {
            problems.Add($"{text.Name}'s tooltip is not its whole text.");
        }

        if (natural > room + N.Tolerance)
        {
            if (!IsCut(text) || text.TextLayout.TextLines.Count != 1)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{text.Name} ({natural:0.##} natural, {room:0.##} of room) is not cut on one line."));
            }

            var right = choose is null ? bounds.Right : app.BoundsOf(choose).Right;
            if (Math.Abs(right - columnRight) > N.Tolerance)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{choose?.Name ?? text.Name} ends at {right:0.##} while {text.Name} is cut; the column's right is {columnRight:0.##} ±1."));
            }
        }
        else if (natural < room - N.Tolerance)
        {
            if (IsCut(text))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{text.Name} ({natural:0.##} natural, {room:0.##} of room) is cut."));
            }

            if (Math.Abs(bounds.Width - natural) > N.Tolerance)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{text.Name} is {bounds.Width:0.##} wide; its text {natural:0.##}."));
            }
        }
    }

    /// <summary>GitLab's sent title (13.4): shown 4 below Draft, its text, <c>settingsNote</c>, one line.</summary>
    private static void AssertSentTitle(ShellDriver app, string text)
    {
        var line = (TextBlock)app.FindByAutomationId("PullRequestSentTitle");
        Assert.True(line.IsEffectivelyVisible, "PullRequestSentTitle is not shown.");
        Assert.Equal(text, line.Text);
        Assert.Contains("settingsNote", line.Classes);
        Assert.Equal(app.BoundsOf(app.FindByAutomationId("PullRequestDraft")).Bottom + N.LineGap, app.BoundsOf(line).Top, 1.0);
        var note = app.FindByAutomationId("PullRequestQuickActionsNote");
        Assert.Equal(app.BoundsOf(app.FindByAutomationId("PullRequestDescription")).Bottom + N.LineGap, app.BoundsOf(note).Top, 1.0);
    }

    // ---- The section, branch rows and graph ----------------------------------------------------------

    private static List<PullRequestRow> PullRequestRows(ShellDriver app) => [.. app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<PullRequestRow>()];

    /// <summary>The section's rows after a create: the new row first (its name, author, draft and tooltip), then the scenario's three as part P gives them.</summary>
    private static void AssertSectionAfterCreate(ShellDriver app, HostingProvider p, int number, string title, string author, string headLabel, bool draft)
    {
        var name = $"{N.NumberOf(number)} {title}";
        var rows = P.RowsOf(p);
        Assert.Equal([$"v {P.TitleOf(p)} {rows.Count + 1}", name, .. rows.Select(row => row.Name)], Section(app));
        var first = PullRequestRows(app)[0];
        Assert.Equal((author, draft, $"{name}\n{author} · {headLabel} → main{(draft ? " · Draft" : string.Empty)}"), (first.Author, first.IsDraft, first.ToolTip));
        Assert.Equal(rows.Select(row => (row.Author, row.Draft, row.ToolTip)), PullRequestRows(app).Skip(1).Select(row => (row.Author, row.IsDraft, row.ToolTip)));
    }

    /// <summary>A branch row's <c>!n</c> and its tooltip (<c>RequestOfBranch</c>), or none when <paramref name="number"/> is null.</summary>
    private static async Task AssertBranchNumberAsync(ShellDriver app, HostingProvider p, int? number, string? title, params string[] path)
    {
        var block = (await app.RefRowAsync(path)).GetLogicalDescendants().OfType<TextBlock>().Single(text => text.Name == "RefPullRequest");
        if (number is not { } shown)
        {
            Assert.False(block.IsEffectivelyVisible, $"{string.Join(" › ", path)} shows {block.Text}.");
            return;
        }

        Assert.True(block.IsEffectivelyVisible, $"{string.Join(" › ", path)} shows no !n.");
        Assert.Equal((N.NumberOf(shown), N.RequestOfBranch(p, shown, title!)), (block.Text, ToolTip.GetTip(block) as string));
    }

    /// <summary>The graph's row <paramref name="row"/>: its first label <paramref name="label"/> with the new badge, the remote label on the row without one.</summary>
    private static void AssertNewBadge(ShellDriver app, HostingProvider p, int row, string label, int number, string title, bool draft)
    {
        app.Settle();
        var graph = app.Find<CommitGraphControl>("GraphRows");
        var labels = graph.LabelsInRow(row);
        Assert.Equal((label, RefLabelKind.LocalBranch), (labels[0].FullText, labels[0].Kind));
        Assert.Equal(new PullRequestBadge(number, title, draft, p), labels[0].Badge);
        Assert.All(labels.Skip(1), other => Assert.Null(other.Badge));
        Assert.True(labels.Skip(1).Any(other => other.IsOverflow ? other.Text == "+1" : other.FullText == "origin/" + label), $"Row {row} has no origin/{label}.");
        var badge = graph.BadgeInRow(row, 0) ?? throw new InvalidOperationException("The new request's label draws no badge.");
        Assert.True(badge.Number.Left >= badge.Icon.Right && badge.Number.Right <= labels[0].Bounds.Right + 0.5, $"The badge's number at {badge.Number} is not after the icon inside the label {labels[0].Bounds}.");
        Assert.All(Enumerable.Range(0, 5), index => Assert.All(graph.LabelsInRow(index), each => Assert.True(each.Bounds.Right <= P.BranchColumnWidth + 0.5, $"Row {index}'s label {each.Text} passes the column.")));
    }

    /// <summary>
    /// After a create, "the other badges as P1's" (N3, N6; part P's <c>AssertBadges</c>, its core):
    /// the request's local label first on <see cref="P.RequestRow"/> and the draft's remote label first
    /// on <see cref="P.DraftRow"/>, each with its badge drawn and no badge on the labels beside it; no
    /// other row but <paramref name="newRow"/> holds a badge.
    /// </summary>
    private static void AssertOtherBadges(ShellDriver app, HostingProvider p, int newRow)
    {
        app.Settle();
        var graph = app.Find<CommitGraphControl>("GraphRows");
        var (requestLabel, request, draftLabel, draft) = p == GitLab
            ? (P.GitLabRequestLabel, P.GitLabRequestBadge, P.GitLabDraftLabel, P.GitLabDraftBadge)
            : (P.AzureDevOpsRequestLabel, P.AzureDevOpsRequestBadge, P.AzureDevOpsDraftLabel, P.AzureDevOpsDraftBadge);
        foreach (var (row, label, badge) in new[] { (P.RequestRow, requestLabel, request), (P.DraftRow, draftLabel, draft) })
        {
            var labels = graph.LabelsInRow(row);
            Assert.Equal(label, labels[0].FullText);
            Assert.Equal(new PullRequestBadge(badge.Number, badge.Title, badge.IsDraft, badge.Provider), labels[0].Badge);
            Assert.NotNull(graph.BadgeInRow(row, 0));
            Assert.All(labels.Skip(1), other => Assert.Null(other.Badge));
        }

        var rows = app.Session.Shell.ActiveTab.Repository!.Graph.Count;
        Assert.True(rows > Math.Max(P.RequestRow, newRow), $"The graph holds {rows} rows.");
        for (var row = 0; row < Math.Min(rows, 50); row++)
        {
            if (row != newRow && row != P.RequestRow && row != P.DraftRow)
            {
                Assert.DoesNotContain(graph.LabelsInRow(row), label => label.Badge is not null);
            }
        }
    }

    // ---- N8's sign-in first (part A's AssertDialog, copied from Phase7bChecks.A.Helpers.cs) ----------

    /// <summary>
    /// N8: part A's dialog of <paramref name="provider"/> as the window's entry opens it (10.5), as
    /// part A's <c>AssertDialog</c> asserts it but over the window, no settings screen under it: the
    /// open dialogs exactly <paramref name="signIn"/>; <c>SignInDialog</c> named by its title; the card
    /// 400 wide at x 350 to 750, centred; title, help, the provider's link(s) in their look and places,
    /// the organization's box (Azure DevOps, holding <paramref name="organization"/>) and the token's,
    /// each under its label; no status line; Cancel and a disabled Sign in, right-aligned.
    /// </summary>
    private static void AssertSignInFirst(ShellDriver app, SignInDialogViewModel signIn, HostingProvider provider, string organization)
    {
        app.Settle();
        var host = app.Session.Services.DialogHost;
        Assert.Equal([signIn], host.Open.Cast<DialogViewModel>());
        Assert.Same(signIn, host.Current);
        Assert.Null(app.SettingsDialog);
        Assert.Equal(provider, signIn.Provider);
        var titleText = provider == GitLab ? PartA.GitLabDialogTitle : PartA.AzureDevOpsDialogTitle;
        var view = app.SignInDialogView();
        Assert.Equal("SignInDialog", AutomationProperties.GetAutomationId(view));
        Assert.Equal(titleText, AutomationProperties.GetName(view));
        var card = app.BoundsOf(view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "SignInCard"));
        Assert.Equal(PartA.DialogCard, (card.Left, card.Right));
        Assert.Equal(app.Window.Bounds.Height / 2, card.Center.Y, PartA.Tolerance);
        Assert.Equal(titleText, ((TextBlock)app.FindByAutomationId(view, "DialogTitle")).Text);
        var help = (TextBlock)app.FindByAutomationId(view, "SignInHelp");
        Assert.Equal(provider == GitLab ? PartA.GitLabHelp : PartA.AzureDevOpsHelp, help.Text);
        var link = app.FindByAutomationId(view, "SignInTokenLink");
        Assert.Equal(provider == GitLab ? PartA.GitLabTokenLink : PartA.AzureDevOpsTokenLink, AutomationProperties.GetName(link));
        Assert.Equal(PartA.LinkGap, app.BoundsOf(link).Top - app.BoundsOf(help).Bottom, PartA.Tolerance);
        AssertLinkLook(link, enabled: provider != Azure || organization.Length > 0);
        var legacy = app.FindByAutomationId(view, "SignInLegacyTokenLink");
        Assert.Equal(provider == GitLab, legacy.IsEffectivelyVisible);
        if (provider == GitLab)
        {
            Assert.Equal(PartA.GitLabLegacyLink, AutomationProperties.GetName(legacy));
            Assert.Equal(PartA.LegacyLinkGap, app.BoundsOf(legacy).Top - app.BoundsOf(link).Bottom, PartA.Tolerance);
            AssertLinkLook(legacy, enabled: true);
        }

        var box = (TextBox)app.FindByAutomationId(view, "SignInOrganization");
        Assert.Equal(provider == Azure, box.IsEffectivelyVisible);
        var token = (TextBox)app.FindByAutomationId(view, "SignInToken");
        Assert.Equal(PartA.TokenLabel, AutomationProperties.GetName(token));
        var labels = view.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Classes.Contains("promptLabel") && block.IsEffectivelyVisible).ToList();
        var tokenLabel = Assert.Single(labels, block => block.Text == PartA.TokenLabel);
        Control aboveToken = provider == Azure ? box : legacy;
        Assert.True(app.BoundsOf(tokenLabel).Top >= app.BoundsOf(aboveToken).Bottom - 0.5 && app.BoundsOf(tokenLabel).Bottom <= app.BoundsOf(token).Top + 0.5, "The label Token is not between its box and what is above it.");
        if (provider == Azure)
        {
            Assert.Equal(organization, box.Text ?? string.Empty);
            Assert.Equal(PartA.OrganizationLabel, AutomationProperties.GetName(box));
            Assert.Contains("dialogBox", box.Classes);
            var organizationLabel = Assert.Single(labels, block => block.Text == PartA.OrganizationLabel);
            Assert.True(app.BoundsOf(organizationLabel).Top >= app.BoundsOf(link).Bottom - 0.5 && app.BoundsOf(organizationLabel).Bottom <= app.BoundsOf(box).Top + 0.5, "The label Organization is not between the link and its box.");
        }
        else
        {
            Assert.DoesNotContain(labels, block => block.Text == PartA.OrganizationLabel);
        }

        foreach (var id in new[] { "SignInNoKeychain", "SignInChecking", "SignInError", "SignInSaveFailed" })
        {
            Assert.False(app.FindByAutomationId(view, id).IsEffectivelyVisible, $"{id} shows.");
        }

        Assert.True(string.IsNullOrEmpty(token.Text), "SignInToken is not empty as the dialog opens.");
        var cancel = (Button)app.FindByAutomationId(view, "CancelButton");
        var confirm = (Button)app.FindByAutomationId(view, "ConfirmButton");
        Assert.True(cancel.IsEffectivelyEnabled);
        Assert.Equal(PartA.SignIn, AutomationProperties.GetName(confirm));
        Assert.False(confirm.IsEffectivelyEnabled, "Sign in is enabled as the dialog opens.");
        Assert.Equal([PartA.Cancel, PartA.SignIn], ButtonsOf(app, view));
        Assert.True(app.BoundsOf(confirm).Right > app.BoundsOf(cancel).Right && app.BoundsOf(confirm).Right >= card.Right - PartA.SaveRightInset, "The dialog's buttons are not right-aligned.");
    }

    /// <summary>A token link's look (part A's <c>AssertLinkLook</c>): enabled, the accent and the hand cursor; disabled, the disabled foreground and the arrow.</summary>
    private static void AssertLinkLook(Control link, bool enabled)
    {
        var name = AutomationProperties.GetAutomationId(link);
        Assert.True(enabled == link.IsEffectivelyEnabled, $"{name} is {(enabled ? "disabled" : "enabled")}.");
        Assert.Equal(enabled ? PartA.Accent : PartA.Disabled, (link.GetVisualDescendants().OfType<TextBlock>().First().Foreground as Avalonia.Media.ISolidColorBrush)?.Color);
        Assert.Equal(enabled ? PartA.HandCursor : PartA.ArrowCursor, link.Cursor?.ToString());
    }

    // ---- The queue, dialogs and git (7a's part N helpers) --------------------------------------------

    /// <summary>Records every dialog that opens from now on, until disposed (D312: "no dialog at any time").</summary>
    private sealed class DialogRecorder : IDisposable
    {
        private readonly ObservableCollection<DialogViewModel> _open;

        public DialogRecorder(ShellDriver app)
        {
            _open = app.Session.Services.DialogHost.Open;
            _open.CollectionChanged += OnChanged;
        }

        public List<DialogViewModel> Opened { get; } = [];

        public void Dispose() => _open.CollectionChanged -= OnChanged;

        private void OnChanged(object? sender, NotifyCollectionChangedEventArgs e) => Opened.AddRange(e.NewItems?.OfType<DialogViewModel>() ?? []);
    }

    /// <summary>
    /// The base of every form that opens from now on, until disposed (N4's and N8's "Into origin/main,
    /// given", open point 45; the lead's ruling R2 on hunt 3): the form's <c>IsReadingBase</c> and
    /// <c>BaseText</c> as it is added to <c>DialogHost.Open</c>, then at each change of either. A form
    /// given its base shows it from the start; one that reads A3 shows <c>Reading …</c> first unless
    /// the read completes inside its constructor.
    /// </summary>
    private sealed class BaseRecorder : IDisposable
    {
        private readonly ObservableCollection<DialogViewModel> _open;
        private readonly List<CreatePullRequestDialogViewModel> _forms = [];

        public BaseRecorder(ShellDriver app)
        {
            _open = app.Session.Services.DialogHost.Open;
            _open.CollectionChanged += OnChanged;
        }

        public List<(bool IsReadingBase, string BaseText)> States { get; } = [];

        public void Dispose()
        {
            _open.CollectionChanged -= OnChanged;
            foreach (var form in _forms)
            {
                form.PropertyChanged -= OnFormChanged;
            }
        }

        /// <summary>
        /// At least one state recorded; every one <c>(false, origin/main)</c>, so none reads
        /// <c>Reading {P}…</c> or is empty: the base was given from the start.
        /// </summary>
        public void AssertGivenFromTheStart(HostingProvider p)
        {
            var seen = string.Join(" ; ", States.Select(state => $"{state.IsReadingBase}|{state.BaseText}"));
            Assert.True(States.Count > 0, "No form opened while the base was recorded.");
            Assert.True(States.TrueForAll(state => state.BaseText != N.ReadingOf(p)), $"The drop's form read its base ({N.ReadingOf(p)}): {seen}.");
            Assert.True(States.TrueForAll(state => state == (false, N.Base)), $"The drop's form did not show its given base {N.Base} from the start: {seen}.");
        }

        private void OnChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var form in e.NewItems?.OfType<CreatePullRequestDialogViewModel>() ?? [])
            {
                _forms.Add(form);
                States.Add((form.IsReadingBase, form.BaseText));
                form.PropertyChanged += OnFormChanged;
            }
        }

        private void OnFormChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is CreatePullRequestDialogViewModel form && e.PropertyName is nameof(CreatePullRequestDialogViewModel.BaseText) or nameof(CreatePullRequestDialogViewModel.IsReadingBase))
            {
                States.Add((form.IsReadingBase, form.BaseText));
            }
        }
    }

    private static long OperationMark(ShellDriver app) =>
        app.Session.Operations.Snapshot().Select(record => record.Sequence).DefaultIfEmpty(0).Max();

    private static List<OperationRecord> OperationsSince(ShellDriver app, long mark) =>
        [.. app.Session.Operations.Snapshot().Where(record => record.Sequence > mark).OrderBy(record => record.Sequence)];

    private static async Task<string> GitAsync(TempRepo repo, params string[] arguments) => (await repo.GitAsync(arguments)).StandardOutput.Trim();

    /// <summary>Git in a bare folder through <c>GitRunner</c> with <c>TempRepo.IsolatedEnvironment</c> (a scratch repository's runner, <c>--git-dir</c>).</summary>
    private static async Task<(int Exit, string Output)> GitInAsync(string gitDirectory, params string[] arguments)
    {
        using var scratch = await TempRepo.CreateAsync();
        var result = await scratch.GitAllowingFailureAsync(["--git-dir", gitDirectory, .. arguments]);
        return (result.ExitCode, result.StandardOutput.Trim());
    }

    private static string Bare(ServedHosting7b served, HostingProvider p) => p == GitLab
        ? served.GitLab.Project(WebAppPath).BarePath!
        : served.AzureDevOps.Repository(Organization, Project, MobileApp).BarePath;

    private static TempRepo Clone(ServedHosting7b served, HostingProvider p) => p == GitLab ? served.WebApp : served.MobileApp;

    /// <summary>
    /// The push's git requests since <paramref name="mark"/> (part N's "Git's version", N4): from git
    /// 2.46 the first carries the account's token (D340's <c>proactiveAuth</c>); below 2.46 the first
    /// goes without credentials and, on Azure DevOps' <c>Fernhill-Labs@</c> URL, the second with that
    /// user and an empty password, each 401, before the helper's (B7's order, as
    /// <c>Phase7bChecks.B.cs</c> asserts it for a clone). After that prefix every request is
    /// <c>x-access-token</c>'s answered 200, the receive-pack's POST among them: no other 401 and no
    /// other user name.
    /// </summary>
    private static void AssertPushedThroughHelper(ShellDriver app, ServedHosting7b served, HostingProvider p, int mark)
    {
        var requests = GitRequestsOf(served, p).Skip(mark).ToList();
        var version = app.Session.Shell.Git?.Version;
        (string? GivenUserName, int Status)[] first = version >= ProactiveAuthVersion
            ? [("x-access-token", 200)]
            : p == GitLab ? [(null, 401)] : [(null, 401), (Organization, 401), ("x-access-token", 200)];
        Assert.True(
            requests.Take(first.Length).Select(request => (request.GivenUserName, request.Status)).SequenceEqual(first),
            $"The push's first requests were {string.Join("; ", requests.Take(first.Length).Select(request => $"{request.Method} {request.Path} as {request.GivenUserName ?? "nobody"} {request.Status}"))}, with git {version}.");
        Assert.All(requests.Skip(first.Length), request => Assert.True(
            request is { GivenUserName: "x-access-token", Status: 200 },
            $"{request.Method} {request.Path} as {request.GivenUserName ?? "nobody"} answered {request.Status} after the first requests, with git {version}."));
        var receive = Assert.Single(requests, request => request.Method == "POST" && request.Path.EndsWith("git-receive-pack", StringComparison.Ordinal));
        Assert.Equal((200, "x-access-token"), (receive.Status, receive.GivenUserName));
    }

    /// <summary>Git 2.46 sends a bound URL's first request with the account's token (8.6; part B's <c>ProactiveAuthVersion</c>, private to its class).</summary>
    private static readonly GitVersion ProactiveAuthVersion = new(2, 46, 0);

    /// <summary>
    /// Every method's end (part N's conventions), after the app has closed: the tripwire for each
    /// token used, over the data folder and the copy's three clones with the session's call log; each
    /// fake's errors and the harness's refusals none; no request answered 400 or 415 but the create's
    /// POST when <paramref name="refusedPost"/>; the launches exactly <paramref name="launches"/>; the
    /// clipboard empty; the log's rules (part N's conventions): no line since the start holds any of
    /// <paramref name="keptOut"/> (the titles and descriptions typed or sent, never logged) or
    /// <c>showing GitHub avatars</c> (D367).
    /// </summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting7b served, string[] tokens, string[] launches, bool refusedPost = false, string[]? keptOut = null)
    {
        var lines = app.LogLines();
        Assert.NotEmpty(lines);
        Assert.DoesNotContain(lines, line => line.Contains("showing GitHub avatars", StringComparison.Ordinal));
        foreach (var text in keptOut ?? throw new ArgumentNullException(nameof(keptOut), "Every method names the texts its log must not hold."))
        {
            Assert.DoesNotContain(lines, line => line.Contains(text, StringComparison.Ordinal));
        }

        var calls = app.Session.GitCalls;
        foreach (var token in tokens)
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.MobileApp.Path, served.Manifest.Path, served.WebApp.Path], calls);
        }

        Assert.Empty(served.AzureDevOps.Errors);
        Assert.Empty(served.GitLab.Errors);
        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(app.RefusedRequests.Addresses);
        Assert.DoesNotContain(served.AzureDevOps.Requests, request => request.Status is 400 or 415 && !(refusedPost && request.Route == N.AzureDevOpsCreateRoute));
        Assert.DoesNotContain(served.GitLab.Requests, request => request.Status is 400 or 415 && !(refusedPost && request.Route == N.GitLabCreateRoute));
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
        Assert.Equal(launches.Select(url => $"{FileLaunchKind.OpenUrl} {WithPorts(url, served)}"), app.Launcher.Requests.Select(request => $"{request.Kind} {request.FullPath}"));
        Assert.Empty(app.Clipboard.Texts);
    }
}
