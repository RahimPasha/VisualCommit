using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using static VisualCommit.VisualTests.Phase7a.Phase7aExpectations;
using G = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartG;
using P = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part G, the graph's badge and avatars (docs/test-reports/phase-7a.md, G1 to G7; design 11.5): four
/// methods, five app starts, each on <c>hello</c> prepared by <see cref="G.PrepareAsync"/>.
/// </summary>
public sealed partial class Phase7aChecksG : Phase7aChecks
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>Method 1: G1, G2 and G3's details panel, signed in through the Accounts screen at 1100×700.</summary>
    [AvaloniaFact]
    public async Task Check_G1_G2_G3_badges_tooltips_and_details()
    {
        await using var served = await ServeAsync();
        await G.PrepareAsync(served.Hello);
        using var data = new TempDirectory("phase7a g1");
        var app = await OpenAsync(data, served, signIn: false);
        try
        {
            // G1 step 1: signed in as the real window does; both lines since the mark.
            var hello = HelloOf(app);
            var requests = served.GitHub.Requests.Count;
            var logMark = app.LogMark();
            var screen = await app.OpenAccountsAsync();
            var dialog = await app.OpenSignInDialogAsync();
            app.TypeToken(OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            await app.WaitForAccountAsync(account => account is { State: AccountState.SignedIn });
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed(Hello), mark: logMark);
            await app.WaitForLogLineAsync(AppLogFile.AvatarsShown(Hello), mark: logMark);
            await app.WaitForWatchersQuietAsync();

            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);
            var api = RequestsSince(served, requests).Where(request => !IsAvatarRun(request)).ToList();
            Assert.Equal(["user 200", "repo:octo/hello 200", "pulls:octo/hello 200"], api.Select(request => $"{request.Route} {request.Status}"));
            Assert.All(api, request => Assert.Equal(("GET", true, "octo"), (request.Method, request.TokenPresent, request.Login)));
            Assert.Equal(P.PullsQuery, api[2].Query);
            Assert.DoesNotContain(RequestsSince(served, requests), request => request.Status == 401);
            var lines = app.LogLinesSince(logMark);
            Assert.Single(lines, line => line.EndsWith(G.HelloListed, StringComparison.Ordinal));
            Assert.Single(lines, line => line.EndsWith(G.HelloShowing, StringComparison.Ordinal));
            Assert.Equal(PullRequestsState.Loaded, hello.HostingState.PullRequests);
            Assert.Equal([5, 3, 2, 1], hello.HostingState.List!.PullRequests.Select(pull => pull.Number));
            Assert.Equal(
                P.HelloByRef.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                hello.HostingState.ByRef.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Number)).OrderBy(pair => pair.Key, StringComparer.Ordinal));
            var columns = Graph(app).Columns;
            Assert.Equal(G.Lanes, columns.LaneCount);
            Assert.Equal((true, true, true, false, false, false), (columns.Refs.IsVisible, columns.Graph.IsVisible, columns.Message.IsVisible, columns.Author.IsVisible, columns.Date.IsVisible, columns.Sha.IsVisible));
            AssertLabels(app, G.SignedInLabels);
            Assert.Equal(P.HelloRows.Select(row => row.Name), hello.LeftPanel.Rows.OfType<PullRequestRow>().Select(row => row.Text));
            var afterStep1 = served.GitHub.Requests.Count;

            // G1 step 2: row 4, octo's commit, with octo's image.
            await SelectAsync(app, G.Row, G.OctoAuthorLine, withAvatar: true);
            var g01a = ShotG(app, G.G01a);
            Assert.Equal(G.Row, hello.SelectedIndex);
            Assert.Equal(("Update the readme", "2026-01-01 12:04", G.TestAuthorLine, "Changed files (1)"), (hello.Details.Subject, hello.Details.DateText, hello.Details.CommitterText, hello.Details.ChangedFilesTitle));
            AssertAuthorLine(app, G.OctoAuthorLine, withAvatar: true);
            Assert.True(ShowsDetailsImage(app, g01a, G.OctoColour), "g01a does not show octo's image before the author.");
            Assert.Equal(afterStep1, served.GitHub.Requests.Count);

            // G1 step 3: the tooltip over row 0's label.
            Assert.Equal(G.LoginTip, await app.RestPointerAtAsync(app.GraphLabelBounds(0, "feature/login").Center));
            ShotG(app, G.G01b, keepPointer: true);

            // G2 steps 4 to 6: the cut label's tooltip whole, the draft's, a row without a pull request's.
            Assert.Equal(G.LongTip, await app.RestPointerAtAsync(app.GraphLabelBounds(3, "origin/featur…").Center));
            ShotG(app, G.G02a, keepPointer: true);
            Assert.Equal(G.ReadmeTip, await app.RestPointerAtAsync(app.GraphLabelBounds(4, "origin/docs/r…").Center));
            Assert.Equal(G.MainTip, await app.RestPointerAtAsync(app.GraphLabelBounds(6, "main").Center));
            AssertLabels(app, G.SignedInLabels);
            Assert.Equal(afterStep1, served.GitHub.Requests.Count);
            app.MoveToNeutral();

            // G3 step 7: row 5, ada's commit; the ellipse keeps octo's image until ada's is decoded.
            app.Click(app.GraphRowPoint(5));
            await app.WaitForAsync(() => hello.SelectedIndex == 5 && hello.Details.AuthorText == G.AdaAuthorLine && Avatar(app).IsVisible, "ada's details");
            await app.WaitForAsync(() => ShowsDetailsImage(app, app.Capture(), G.AdaColour), "ada's image in the details panel");
            AssertAvatarRun(app, data, served, requests);
            AssertAuthorLine(app, G.AdaAuthorLine, withAvatar: true);

            // G3 step 8: row 6, Test Author's, as today.
            await SelectAsync(app, 6, G.TestAuthorLine, withAvatar: false);
            ShotG(app, G.G03a);
            AssertAuthorLine(app, G.TestAuthorLine, withAvatar: false);
            Assert.Equal(("Add the docs folder", "2026-01-01 12:02", string.Empty, "Changed files (1)"), (hello.Details.Subject, hello.Details.DateText, hello.Details.CommitterText, hello.Details.ChangedFilesTitle));
            Assert.Equal(afterStep1, served.GitHub.Requests.Count);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);
    }

    /// <summary>Method 2: G6, G3's Author column and G4 at 1920×1080, then G6's sign-out.</summary>
    [AvaloniaFact]
    public async Task Check_G6_G3_G4_signed_out_avatars_and_light_at_1920x1080()
    {
        await using var served = await ServeAsync();
        await G.PrepareAsync(served.Hello);
        using var data = new TempDirectory("phase7a g6");
        var app = await OpenAsync(data, served, signIn: false, width: 1920, height: 1080);
        try
        {
            // G6 step 1: signed out, as today.
            var hello = HelloOf(app);
            await app.WaitForAsync(() => hello.HostingState.PullRequests == PullRequestsState.SignedOut, "the hosting signed out");
            await app.WaitForAsync(() => hello.LeftPanel.Rows.OfType<PullRequestMessageRow>().Any(row => row.Kind == PullRequestMessageKind.SignIn), "the section's sign-in row");
            await app.WaitForWatchersQuietAsync();
            await SelectAsync(app, G.Row, G.OctoAuthorLine, withAvatar: false);
            var g06a = ShotG(app, G.G06a);
            Assert.Empty(served.GitHub.Requests);
            Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.Contains("--get-regexp ^url", StringComparison.Ordinal));
            Assert.Empty(hello.HostingState.ByRef);
            Assert.Same(AuthorAvatars.None, hello.Avatars);
            Assert.False(Directory.Exists(Path.Combine(data.Path, "avatars")));
            Assert.DoesNotContain(app.LogLinesSince(0), line => line.Contains(": listed ", StringComparison.Ordinal) || line.Contains("showing GitHub avatars", StringComparison.Ordinal));
            Assert.Equal([$"[SignIn] {P.SignInToSeePullRequests}"], hello.LeftPanel.Rows.OfType<PullRequestMessageRow>().Select(row => $"[{row.Kind}] {row.Text}"));
            AssertLabels(app, G.TodayLabels);
            AssertAuthorColumn(app, g06a, images: false);
            Assert.All(new[] { OctoEmail, AdaEmail, G.TestAuthorEmail }, email => Assert.False(Graph(app).ShowsAvatarFor(email)));
            AssertAuthorLine(app, G.OctoAuthorLine, withAvatar: false);

            // G3 method 2 step 2: signed in through the session; both lines, then the image.
            var requests = served.GitHub.Requests.Count;
            var logMark = app.LogMark();
            await app.SignInAsync(OctoToken, Cancelled);
            await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed(Hello), mark: logMark);
            await app.WaitForLogLineAsync(AppLogFile.AvatarsShown(Hello), mark: logMark);
            await app.WaitForWatchersQuietAsync();
            await app.WaitForAsync(() => Avatar(app).IsVisible, "the details panel's avatar");
            var g03b = ShotG(app, G.G03b);
            Assert.Equal(["user 200", "repo:octo/hello 200", "pulls:octo/hello 200"], RequestsSince(served, requests).Where(request => !IsAvatarRun(request)).Select(request => $"{request.Route} {request.Status}"));
            AssertAvatarRun(app, data, served, requests);
            AssertAuthorColumn(app, g03b, images: true);
            Assert.Equal((true, true, false), (Graph(app).ShowsAvatarFor(OctoEmail), Graph(app).ShowsAvatarFor(AdaEmail), Graph(app).ShowsAvatarFor(G.TestAuthorEmail)));
            Assert.Equal(G.Row, hello.SelectedIndex);
            AssertAuthorLine(app, G.OctoAuthorLine, withAvatar: true);
            Assert.True(ShowsDetailsImage(app, g03b, G.OctoColour), "g03b does not show octo's image before the author.");
            AssertLabels(app, G.SignedInLabels);

            // G4 step 3: light.
            requests = served.GitHub.Requests.Count;
            app.Click(app.FindByAutomationId("ThemeSwitch"));
            await app.WaitForAsync(() => app.Window.ActualThemeVariant == ThemeVariant.Light, "the light theme");
            var g04a = ShotG(app, G.G04a);
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);
            Assert.Equal(requests, served.GitHub.Requests.Count);
            AssertLabels(app, G.SignedInLabels);
            AssertAuthorColumn(app, g04a, images: true);
            AssertAuthorLine(app, G.OctoAuthorLine, withAvatar: true);
            Assert.True(ShowsDetailsImage(app, g04a, G.OctoColour), "g04a does not show octo's image before the author.");

            // G6 step 4: signed out through the screen: as today again, avatars/ gone.
            await app.OpenAccountsAsync();
            requests = served.GitHub.Requests.Count;
            var stored = app.Tokens.Calls.Count;
            await app.SignOutAsync(confirm: true);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => hello.HostingState.PullRequests == PullRequestsState.SignedOut, "the hosting signed out");
            await app.WaitForAsync(() => hello.LeftPanel.Rows.OfType<PullRequestMessageRow>().Any(row => row.Kind == PullRequestMessageKind.SignIn), "the section's sign-in row");
            await app.WaitForAsync(() => !Graph(app).ShowsAvatarFor(OctoEmail) && !Graph(app).ShowsAvatarFor(AdaEmail), "the graph without images");
            await app.WaitForWatchersQuietAsync();
            Assert.Equal(requests, served.GitHub.Requests.Count);
            Assert.Equal([$"delete {WithPort("github:127.0.0.1:47395:octo", served.GitHub.Port)}"], app.Tokens.Calls.Skip(stored));
            Assert.Null(app.Session.Services.Hosting.Account);
            Assert.Empty(hello.HostingState.ByRef);
            Assert.Same(AuthorAvatars.None, hello.Avatars);
            Assert.False(Directory.Exists(Path.Combine(data.Path, "avatars")));
            Assert.Equal([$"[SignIn] {P.SignInToSeePullRequests}"], hello.LeftPanel.Rows.OfType<PullRequestMessageRow>().Select(row => $"[{row.Kind}] {row.Text}"));
            AssertLabels(app, G.TodayLabels);
            AssertAuthorColumn(app, app.Capture(), images: false);
            Assert.Equal(G.Row, hello.SelectedIndex);
            AssertAuthorLine(app, G.OctoAuthorLine, withAvatar: false);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);
    }

    /// <summary>Method 3: G5, a repository whose remote is a folder: the same pictures with GitHub off and signed in.</summary>
    [Fact]
    public async Task Check_G5_not_github_as_with_github_off()
    {
        var assembly = typeof(Phase7aChecksG).Assembly;
        await using var served = await ServeAsync();
        await G.PrepareAsync(served.Hello);
        await served.Hello.GitAsync("remote", "set-url", "origin", Path.Combine(served.Hello.SiblingPath("github"), "octo", "hello.git"));
        using var data1 = new TempDirectory("phase7a g5 off");
        using var data2 = new TempDirectory("phase7a g5 signed in");
        Screenshot? off = null;
        List<string>? labels = null;
        Avalonia.Rect author = default;

        // Step 2: GitHub off.
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                var app = ShellDriver.StartWithTabs(data1.Path, [served.Hello.Path]);
                try
                {
                    await app.WaitForRepositoryAsync();
                    await app.WaitForWatchersQuietAsync();
                    app.MoveToNeutral();
                    await SelectAsync(app, G.Row, G.OctoAuthorLine, withAvatar: false);
                    off = ShotG(app, G.G05a);
                    var hello = HelloOf(app);
                    Assert.Equal(PullRequestsState.NotGitHub, hello.HostingState.PullRequests);
                    AssertLabels(app, G.TodayLabels);
                    labels = Labels(app);
                    author = app.BoundsOf(app.FindByAutomationId("DetailsAuthor"));
                    AssertAuthorLine(app, G.OctoAuthorLine, withAvatar: false);
                }
                finally
                {
                    app.Dispose();
                }

                return true;
            },
            Cancelled);

        // Step 3: signed in, the same repository.
        var requests = served.GitHub.Requests.Count;
        await FreshApplication.RunAsync(
            assembly,
            async () =>
            {
                var app = await OpenAsync(data2, served);
                try
                {
                    await SelectAsync(app, G.Row, G.OctoAuthorLine, withAvatar: false);
                    var signedIn = ShotG(app, G.G05b);
                    var hello = HelloOf(app);
                    Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);
                    Assert.Equal(["user 200"], RequestsSince(served, requests).Select(request => $"{request.Route} {request.Status}"));
                    Assert.Equal(PullRequestsState.NotGitHub, hello.HostingState.PullRequests);
                    Assert.Empty(hello.HostingState.Remotes);
                    Assert.Empty(hello.HostingState.ByRef);
                    Assert.Same(AuthorAvatars.None, hello.Avatars);
                    Assert.DoesNotContain(app.LogLinesSince(0), line => line.Contains(": listed ", StringComparison.Ordinal) || line.Contains("showing GitHub avatars", StringComparison.Ordinal));
                    Assert.False(Directory.Exists(Path.Combine(data2.Path, "avatars")));
                    Assert.Equal(labels, Labels(app));
                    Assert.Equal(author, app.BoundsOf(app.FindByAutomationId("DetailsAuthor")));
                    AssertSamePicture(off!, signedIn);
                    End(app, data2, served, data1.Path);
                }
                finally
                {
                    app.Dispose();
                }

                return true;
            },
            Cancelled);
    }

    /// <summary>Method 4: G7, the authors' read answered 500: every author as today, the failure logged, nothing more asked.</summary>
    [AvaloniaFact]
    public async Task Check_G7_the_authors_read_fails()
    {
        await using var served = await ServeAsync();
        await G.PrepareAsync(served.Hello);
        served.GitHub.FailNext("commits:octo/hello", FakeGitHubFault.ServerError);
        using var data = new TempDirectory("phase7a g7");
        var app = await OpenAsync(data, served);
        try
        {
            // Step 1.
            var hello = HelloOf(app);
            await app.WaitForLogLineAsync(AppLogFile.PullRequestsListed(Hello), mark: 0);
            await app.WaitForLogLineAsync(AppLogFile.Containing(G.AuthorsFailedPrefix), mark: 0);
            await app.WaitForWatchersQuietAsync();
            Assert.Equal(
                ["commits:octo/hello 500", "pulls:octo/hello 200", "repo:octo/hello 200", "user 200"],
                served.GitHub.Requests.Select(request => $"{request.Route} {request.Status}").Order(StringComparer.Ordinal));
            Assert.Equal("user", served.GitHub.Requests.First().Route);
            var commits = Assert.Single(served.GitHub.Requests, request => request.Route == "commits:octo/hello");
            Assert.Equal((G.CommitsQuery, true), (commits.Query, commits.TokenPresent));
            var lines = app.LogLinesSince(0);
            var client = Assert.Single(lines, line => line.Contains(G.CommitsFailedPrefix, StringComparison.Ordinal));
            var session = Assert.Single(lines, line => line.Contains(G.AuthorsFailedPrefix, StringComparison.Ordinal));
            Assert.Contains("[WRN]", client, StringComparison.Ordinal);
            Assert.Contains("[WRN]", session, StringComparison.Ordinal);
            Assert.Equal(RequestIdOf(client), RequestIdOf(session));
            Assert.DoesNotContain(lines, line => line.Contains("showing GitHub avatars", StringComparison.Ordinal));
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);
            Assert.Equal(PullRequestsState.Loaded, hello.HostingState.PullRequests);
            Assert.Equal(
                P.HelloByRef.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                hello.HostingState.ByRef.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Number)).OrderBy(pair => pair.Key, StringComparer.Ordinal));
            Assert.Same(AuthorAvatars.None, hello.Avatars);
            Assert.False(Directory.Exists(Path.Combine(data.Path, "avatars")));

            // Step 2.
            await SelectAsync(app, G.Row, G.OctoAuthorLine, withAvatar: false);
            ShotG(app, G.G07a);
            AssertLabels(app, G.SignedInLabels);
            AssertAuthorLine(app, G.OctoAuthorLine, withAvatar: false);
            Assert.All(new[] { OctoEmail, AdaEmail, G.TestAuthorEmail }, email => Assert.False(Graph(app).ShowsAvatarFor(email)));
            Assert.Single(served.GitHub.Requests, request => request.Route == "commits:octo/hello");
            Assert.DoesNotContain(served.GitHub.Requests, request => request.Route?.StartsWith("avatar:", StringComparison.Ordinal) == true);
            Assert.DoesNotContain(app.LogLinesSince(0), line => line.Contains("showing GitHub avatars", StringComparison.Ordinal));
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served);
    }

    /// <summary>Every row's labels as lines, for G5's comparison of two starts.</summary>
    private static List<string> Labels(ShellDriver app) =>
        [.. Enumerable.Range(0, G.Rows.Count).SelectMany(row => Graph(app).LabelsInRow(row).Select(label => $"{row} {label}"))];

    /// <summary>G5: two pictures pixel for pixel the same (the files they are saved to then are too).</summary>
    private static void AssertSamePicture(Screenshot expected, Screenshot actual)
    {
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.True(expected.PixelAt(x, y) == actual.PixelAt(x, y), $"g05b differs from g05a at ({x}, {y}).");
            }
        }
    }
}
