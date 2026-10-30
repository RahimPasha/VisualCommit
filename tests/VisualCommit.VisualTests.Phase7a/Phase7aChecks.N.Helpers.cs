using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using N = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartN;
using P = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>Part N's helpers (the report's "Conventions of part N"): the form's snapshot and opening, the operation's end, the list lines, the POST and the method's end.</summary>
public sealed partial class Phase7aChecksN
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary><see cref="Phase7aChecks.Shot"/> with no token on screen and no text cut but by an ellipsis.</summary>
    private static Screenshot ShotN(ShellDriver app, string name, bool keepPointer = false)
    {
        AssertNoTokenOnScreen(app);
        var shot = Shot(app, name, keepPointer);
        AssertNoClippedText(app);
        return shot;
    }

    private static void AssertNoTokenOnScreen(ShellDriver app)
    {
        var texts = app.Window.GetVisualDescendants().Where(visual => visual is Visual { IsEffectivelyVisible: true }).Select(visual => visual switch
        {
            TextBlock block => block.Text,
            TextBox box => box.Text,
            _ => null,
        }).OfType<string>().ToList();
        foreach (var token in new[] { Phase7aExpectations.OctoToken, Phase7aExpectations.OctoClassicToken, Phase7aExpectations.AdaToken })
        {
            Assert.False(texts.Any(text => text.Contains(token, StringComparison.Ordinal)), "A token is on screen.");
        }
    }

    /// <summary>Right-clicks the row at <paramref name="path"/> and returns its menu once shown.</summary>
    private static async Task<MenuSnapshot> MenuOfAsync(ShellDriver app, params string[] path)
    {
        app.RightClick(await app.RefRowAsync(path));
        await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "the row's menu");
        return app.Menu();
    }

    /// <summary>Clicks <c>Create pull request…</c> in the open menu and waits for the form with Into <paramref name="baseText"/>.</summary>
    private static async Task<CreatePullRequestDialogViewModel> OpenFormFromMenuAsync(ShellDriver app, string baseText = N.OriginMain)
    {
        app.ClickMenuItem(Phase7aExpectations.PartP.CreatePullRequest);
        var form = await app.WaitForDialogAsync<CreatePullRequestDialogViewModel>();
        await app.WaitForAsync(() => form.BaseText == baseText, $"the form's base {baseText}");
        app.Settle();
        return form;
    }

    /// <summary>The form as part N's conventions give it (±1): card, labels, values, boxes and buttons.</summary>
    private static void AssertFormPlaces(ShellDriver app, double cardLeft)
    {
        var card = app.BoundsOf(app.FindByAutomationId("CreatePullRequestDialog").GetVisualDescendants().OfType<Border>().First(border => border.Name == "CreatePullRequestDialogCard"));
        Assert.Equal(cardLeft, card.X, 1.0);
        Assert.Equal(N.CardWidth, card.Width, 1.0);
        Assert.Equal((app.Window.Bounds.Height - card.Height) / 2, card.Y, 1.0);
        var title = app.BoundsOf(app.FindByAutomationId("PullRequestTitle"));
        Assert.Equal(cardLeft + 20, title.X, 1.0);
        Assert.Equal(cardLeft + N.CardWidth - 20, title.Right, 1.0);
        Assert.Equal(N.TitleHeight, title.Height, 1.0);
        Assert.Equal(N.DescriptionHeight, app.BoundsOf(app.FindByAutomationId("PullRequestDescription")).Height, 1.0);
        Assert.Equal(cardLeft + 20 + 48, app.BoundsOf(app.FindByAutomationId("PullRequestHead")).X, 1.0);
        Assert.Equal(cardLeft + 20 + 48, app.BoundsOf(app.FindByAutomationId("PullRequestBase")).X, 1.0);
        Assert.Equal(cardLeft + N.CardWidth - 20, app.BoundsOf(app.FindByAutomationId("ConfirmButton")).Right, 1.0);
    }

    /// <summary>The form's texts: From, Into, the note, the title, the description, Draft, the error line and the existing button.</summary>
    private static void AssertForm(ShellDriver app, CreatePullRequestDialogViewModel form, string head, string? note, string baseText, string title, string? error = null, int? existing = null)
    {
        Assert.Equal(N.DialogTitle, form.Title);
        Assert.False(form.ShowsRepository);
        Assert.Equal((head, N.Repository), (Text(app, "PullRequestHead"), Text(app, "PullRequestHeadRepository")));
        Assert.Equal(head, ToolTip.GetTip(app.FindByAutomationId("PullRequestHead")));
        Assert.Equal(note is not null, app.FindByAutomationId("PullRequestHeadNote").IsEffectivelyVisible);
        Assert.Equal(note, form.HeadNote);
        Assert.Equal((baseText, N.Repository), (Text(app, "PullRequestBase"), Text(app, "PullRequestBaseRepository")));
        Assert.Equal(title, ((TextBox)app.FindByAutomationId("PullRequestTitle")).Text);
        Assert.Equal(error, form.Error);
        Assert.Equal(error is not null, app.FindByAutomationId("PullRequestError").IsEffectivelyVisible);
        Assert.Equal(existing, form.ExistingNumber);
        Assert.Equal(existing is not null, app.FindByAutomationId("PullRequestOpenExisting").IsEffectivelyVisible);
    }

    private static string? Text(ShellDriver app, string id) => ((TextBlock)app.FindByAutomationId(id)).Text;

    /// <summary>
    /// The rest of N1 step 2's form (the report's "the rest as N1's form"): the focus in the title
    /// with the caret at its end, the description empty with its placeholder, Draft unticked, and
    /// <c>Choose…</c>, <c>Cancel</c> and <c>Create</c> enabled.
    /// </summary>
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
    }

    /// <summary>The form's controls that 12.4's creating state disables; Cancel apart (open point 59).</summary>
    private static readonly string[] FormInputs = ["PullRequestBaseChooseButton", "PullRequestTitle", "PullRequestDescription", "PullRequestDraft", "ConfirmButton"];

    /// <summary>The form creating (part N's conventions): every control disabled, <c>Creating…</c>; Cancel enabled only while Create pushes (open point 59).</summary>
    private static void AssertCreating(ShellDriver app, CreatePullRequestDialogViewModel form, bool cancelEnabled)
    {
        Assert.True(form.IsCreating);
        Assert.Equal(N.Creating, form.CreateText);
        Assert.All(FormInputs, id => Assert.False(app.FindByAutomationId(id).IsEffectivelyEnabled, $"{id} is enabled while creating."));
        Assert.Equal(cancelEnabled, app.FindByAutomationId("CancelButton").IsEffectivelyEnabled);
    }

    /// <summary>The form ready or failed with a title and a base of another branch: every control enabled, <c>Create</c>.</summary>
    private static void AssertIdle(ShellDriver app, CreatePullRequestDialogViewModel form)
    {
        Assert.False(form.IsCreating);
        Assert.Equal(N.Create, form.CreateText);
        Assert.All([.. FormInputs, "CancelButton"], id => Assert.True(app.FindByAutomationId(id).IsEffectivelyEnabled, $"{id} is disabled."));
    }

    /// <summary>
    /// The form's parts relative to its card, as N1 measures them at 1100×700 (<see
    /// cref="N.FormLayout"/>, ±1): N10 holds them at 1920×1080, dark and light.
    /// </summary>
    private static void AssertFormLayout(ShellDriver app)
    {
        var card = app.FindByAutomationId("CreatePullRequestDialog").GetVisualDescendants().OfType<Border>().First(border => border.Name == "CreatePullRequestDialogCard");
        var origin = app.BoundsOf(card);
        var labels = card.GetVisualDescendants().OfType<TextBlock>().ToList();
        (string Name, Visual Part)[] parts =
        [
            ("CreatePullRequestDialogCard", card),
            ("CreatePullRequestDialogTitle", app.FindByAutomationId("CreatePullRequestDialogTitle")),
            (N.From, labels.Single(label => label.Text == N.From)),
            ("PullRequestHead", app.FindByAutomationId("PullRequestHead")),
            ("PullRequestHeadRepository", app.FindByAutomationId("PullRequestHeadRepository")),
            (N.Into, labels.Single(label => label.Text == N.Into)),
            ("PullRequestBase", app.FindByAutomationId("PullRequestBase")),
            ("PullRequestBaseRepository", app.FindByAutomationId("PullRequestBaseRepository")),
            ("PullRequestBaseChooseButton", app.FindByAutomationId("PullRequestBaseChooseButton")),
            ("PullRequestTitle", app.FindByAutomationId("PullRequestTitle")),
            ("PullRequestDescription", app.FindByAutomationId("PullRequestDescription")),
            ("PullRequestDraft", app.FindByAutomationId("PullRequestDraft")),
            ("CancelButton", app.FindByAutomationId("CancelButton")),
            ("ConfirmButton", app.FindByAutomationId("ConfirmButton")),
        ];
        var measured = parts.Select(part =>
        {
            var bounds = app.BoundsOf(part.Part);
            return (part.Name, X: bounds.X - origin.X, Y: bounds.Y - origin.Y, bounds.Width, bounds.Height);
        }).ToList();
        var described = string.Join("\n", measured.Select(part => string.Create(CultureInfo.InvariantCulture, $"(\"{part.Name}\", {part.X:0.##}, {part.Y:0.##}, {part.Width:0.##}, {part.Height:0.##}),")));
        Assert.True(
            measured.Count == N.FormLayout.Length && measured.Zip(N.FormLayout).All(pair => pair.First.Name == pair.Second.Name
                && Math.Abs(pair.First.X - pair.Second.X) <= 1 && Math.Abs(pair.First.Y - pair.Second.Y) <= 1
                && Math.Abs(pair.First.Width - pair.Second.Width) <= 1 && Math.Abs(pair.First.Height - pair.Second.Height) <= 1),
            "The form's parts relative to its card are not 1100×700's (±1):\n" + described);
    }

    /// <summary>A menu's shape (4a: its height by its items, 4 px inside the window) and its height as the report gives it (±2).</summary>
    private static void AssertMenuHeight(ShellDriver app, MenuSnapshot menu, double height)
    {
        AssertMenuShape(app, menu);
        Assert.True(
            Math.Abs(menu.Bounds.Height - height) <= 2,
            string.Create(CultureInfo.InvariantCulture, $"The menu is {menu.Bounds.Height} high; the report gives {height} ±2."));
    }

    /// <summary>
    /// The graph's badges (11.2, from <c>ByRef</c>): in each of the scenario's rows only the first
    /// label may carry one, and it carries the one <paramref name="badges"/> gives for its row, the
    /// label's whole name checked where given; every other label carries none.
    /// </summary>
    private static void AssertBadges(ShellDriver app, params (int Row, string? FullText, int Number, string Title, bool Draft)[] badges)
    {
        app.Settle();
        var graph = app.Find<CommitGraphControl>("GraphRows");
        for (var row = 0; row < N.GraphRows; row++)
        {
            var labels = graph.LabelsInRow(row);
            var want = badges.Where(badge => badge.Row == row).ToList();
            Assert.True(labels.Skip(1).All(label => label.Badge is null), $"Row {row} has a badge on a label after its first: {string.Join(", ", labels.Select(label => $"{label.FullText} {label.Badge}"))}.");
            if (want.Count == 0)
            {
                Assert.True(labels.Count == 0 || labels[0].Badge is null, labels.Count == 0 ? null : $"Row {row}'s {labels[0].FullText} has the badge {labels[0].Badge}.");
                continue;
            }

            var (_, fullText, number, title, draft) = Assert.Single(want);
            Assert.NotEmpty(labels);
            Assert.Equal(new PullRequestBadge(number, title, draft), labels[0].Badge);
            if (fullText is not null)
            {
                Assert.Equal(fullText, labels[0].FullText);
            }
        }
    }

    /// <summary>The section's pull request rows (10.3): name, author and draft, top to bottom, and <c>PullRequestDraft</c> shown on the drafts' controls only.</summary>
    private static void AssertSectionRows(ShellDriver app, params (string Name, string Author, bool Draft)[] rows)
    {
        Assert.Equal([$"v {P.Title} {rows.Length.ToString(CultureInfo.InvariantCulture)}", .. rows.Select(row => row.Name)], Section(app));
        Assert.Equal(rows, app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<PullRequestRow>().Select(row => (row.Text, row.Author, row.IsDraft)));
        var controls = app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && AutomationProperties.GetAutomationId(button) == "PullRequestItem")
            .OrderBy(button => app.BoundsOf(button).Top)
            .ToList();
        Assert.Equal(rows.Select(row => row.Name), controls.Select(AutomationProperties.GetName));
        Assert.Equal(
            rows.Select(row => row.Draft),
            controls.Select(control => control.GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Name == "PullRequestDraft").IsEffectivelyVisible));
    }

    /// <summary>A branch row's <c>#n</c> (<c>RefPullRequest</c>, 10.3): shown with its tooltip <c>Pull request #n: {title}</c>, or hidden when <paramref name="number"/> is null.</summary>
    private static async Task AssertRefPullRequestAsync(ShellDriver app, int? number, string? title, params string[] path)
    {
        var block = (await app.RefRowAsync(path)).GetLogicalDescendants().OfType<TextBlock>().Single(text => text.Name == "RefPullRequest");
        if (number is not { } shown)
        {
            Assert.False(block.IsVisible, $"{string.Join(" › ", path)} shows {block.Text}.");
            return;
        }

        Assert.True(block.IsEffectivelyVisible, $"{string.Join(" › ", path)} shows no #n.");
        Assert.Equal(("#" + shown.ToString(CultureInfo.InvariantCulture), P.PullRequestOfBranch(shown, title!)), (block.Text, ToolTip.GetTip(block) as string));
    }

    /// <summary>Records every dialog that opens from now on, until disposed (the report's "no dialog at any time" and "no dialog but the form", D312).</summary>
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

    /// <summary>The newest record's number in the queue's log (the activity log's source), a mark for <see cref="OperationsSince"/>.</summary>
    private static long OperationMark(ShellDriver app) =>
        app.Session.Operations.Snapshot().Select(record => record.Sequence).DefaultIfEmpty(0).Max();

    /// <summary>The operations recorded after <paramref name="mark"/>, in the order they started.</summary>
    private static List<OperationRecord> OperationsSince(ShellDriver app, long mark) =>
        [.. app.Session.Operations.Snapshot().Where(record => record.Sequence > mark).OrderBy(record => record.Sequence)];

    /// <summary>
    /// A refused POST's Warnings since <paramref name="logMark"/>, word for word (part N's "Log
    /// lines"): WP-G's <c>GitHub: POST /api/v3/repos/octo/hello/pulls failed: {status} (request
    /// {id}).</c> and WP-S's <c>GitHub: creating a pull request on octo/hello failed: {kind},
    /// {message} (request {id})</c>.
    /// </summary>
    private static void AssertRefusedPostWarnings(ShellDriver app, int logMark, int status, string kind, string message)
    {
        var lines = app.LogLinesSince(logMark).ToList();
        var provider = new Regex(string.Create(CultureInfo.InvariantCulture, $@"\[WRN\] GitHub: POST /api/v3/repos/octo/hello/pulls failed: {status} \(request [^\s)]+\)\.$"), RegexOptions.CultureInvariant);
        var session = new Regex($@"\[WRN\] GitHub: creating a pull request on octo/hello failed: {Regex.Escape(kind)}, {Regex.Escape(message)} \(request [^\s)]+\)$", RegexOptions.CultureInvariant);
        Assert.True(lines.Count(provider.IsMatch) == 1, "The provider's Warning is not there once:\n" + string.Join('\n', lines));
        Assert.True(lines.Count(session.IsMatch) == 1, "The session's Warning is not there once:\n" + string.Join('\n', lines));
    }

    /// <summary>The operation's end since <paramref name="logMark"/>: <c>hello: {Title}: {Outcome} after …</c> (the report's "the operation").</summary>
    private static Task<string> WaitForEndAsync(ShellDriver app, string head, int logMark, OperationOutcome? outcome = null) =>
        app.WaitForLogLineAsync(AppLogFile.OperationEnded(Phase7aExpectations.Hello, N.OperationTitle(head), outcome), mark: logMark);

    /// <summary><paramref name="times"/> lines <c>hello: listed {count} open pull requests of octo/hello</c> since <paramref name="logMark"/>, then quiet watchers.</summary>
    private static async Task WaitForListedAsync(ShellDriver app, int count, int logMark, int times = 1)
    {
        for (var occurrence = 1; occurrence <= times; occurrence++)
        {
            var line = await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed(Phase7aExpectations.Hello), occurrence, logMark);
            Assert.Equal(count.ToString(System.Globalization.CultureInfo.InvariantCulture), AppLogFile.PullRequestsListed(Phase7aExpectations.Hello).Match(line).Groups["count"].Value);
        }

        await app.WaitForWatchersQuietAsync();
    }

    /// <summary>The start's wait (D322): <c>listed 4</c> and the avatars' line since the start, then quiet watchers.</summary>
    private static async Task WaitForHelloAsync(ShellDriver app, int logMark = 0)
    {
        await WaitForListedAsync(app, 4, logMark);
        await app.WaitForLogLineAsync(AppLogFile.AvatarsShown(Phase7aExpectations.Hello), mark: 0);
        await app.WaitForWatchersQuietAsync();
    }

    /// <summary>The API requests since <paramref name="mark"/>, leaving out the avatars' (part P's rule).</summary>
    private static List<FakeGitHubRequest> ApiSince(ServedHosting served, int mark) =>
        [.. served.GitHub.Requests.Skip(mark).Where(request => request.Route is not { } route || !(route.StartsWith("commits:", StringComparison.Ordinal) || route.StartsWith("avatar:", StringComparison.Ordinal)))];

    private static List<string> Routes(ServedHosting served, int mark) =>
        [.. ApiSince(served, mark).Select(request => $"{request.Method} {request.Route} {request.Status}{(request.Conditional ? " conditional" : string.Empty)}")];

    /// <summary>The POST's body: title, body, head, base, draft and whether <c>maintainer_can_modify</c> was sent.</summary>
    private static (string Title, string Body, string Head, string Base, bool Draft, bool HasMaintainer) Body(FakeGitHubRequest post)
    {
        using var json = JsonDocument.Parse(post.Body!);
        var root = json.RootElement;
        return (root.GetProperty("title").GetString()!, root.GetProperty("body").GetString()!, root.GetProperty("head").GetString()!, root.GetProperty("base").GetString()!, root.GetProperty("draft").GetBoolean(), root.TryGetProperty("maintainer_can_modify", out _));
    }

    private static FakeGitHubRequest PostSince(ServedHosting served, int mark) =>
        Assert.Single(served.GitHub.Requests.Skip(mark), request => request.Route == "create:octo/hello");

    private static List<string> OpenedUrls(ShellDriver app) =>
        [.. app.Launcher.Requests.Where(request => request.Kind == FileLaunchKind.OpenUrl).Select(request => request.FullPath)];

    private static async Task<string> GitAsync(TempRepo repo, params string[] arguments) => (await repo.GitAsync(arguments)).StandardOutput.Trim();

    private static async Task<(int Exit, string Output)> GitInAsync(string gitDirectory, params string[] arguments)
    {
        using var scratch = await TempRepo.CreateAsync();
        var result = await scratch.GitAllowingFailureAsync(["--git-dir", gitDirectory, .. arguments]);
        return (result.ExitCode, result.StandardOutput.Trim());
    }

    private static string Bare(ServedHosting served) => served.GitHub.Repository(N.Repository).BarePath!;

    /// <summary>Every method's end (part P's): the app closed, the tripwire over the data folder and the clones with every start's calls, the fake's errors, no 400 or 415, no request refused, the launches exactly those named.</summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting served, params string[] launches)
    {
        var calls = app.Session.GitCalls;
        var opened = OpenedUrls(app);
        app.Dispose();
        foreach (var token in new[] { served.OctoToken, served.OctoClassicToken, served.AdaToken })
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.Hello.Path, served.Tool.Path], calls);
        }

        Assert.Empty(served.GitHub.Errors);
        Assert.DoesNotContain(served.GitHub.Requests, request => request.Status is 400 or 415);
        Assert.Empty(app.RefusedRequests.Addresses);
        Assert.Equal(launches, opened);
    }
}
