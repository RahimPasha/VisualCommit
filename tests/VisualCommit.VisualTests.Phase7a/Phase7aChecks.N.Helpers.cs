using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using N = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartN;

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
