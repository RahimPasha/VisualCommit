using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;
using N = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartN;
using P = VisualCommit.VisualTests.Phase7a.Phase7aExpectations.PartP;

namespace VisualCommit.VisualTests.Phase7a;

/// <summary>
/// Part N, a new pull request (docs/test-reports/phase-7a.md, part N; design 12.8): N1 to N10 in
/// seven methods, one app start each, every one on a fresh copy of "hosting" prepared by
/// <see cref="N.PrepareAsync"/>.
/// </summary>
public sealed partial class Phase7aChecksN : Phase7aChecks
{
    [AvaloniaFact]
    public async Task Check_N1_from_a_branch_and_the_offer()
    {
        await using var served = await ServeAsync();
        await N.PrepareAsync(served.Hello);
        using var data = new TempDirectory("phase7a n1");
        var app = await OpenAsync(data, served, signIn: false);
        try
        {
            var hello = app.Session.Shell.ActiveTab.Repository!;
            var requests = served.GitHub.Requests.Count;
            var logMark = app.LogMark();

            // Step 1: signed in through the Accounts screen, as the real window does.
            var screen = await app.OpenAccountsAsync();
            var dialog = await app.OpenSignInDialogAsync();
            app.TypeToken(served.OctoToken);
            app.PressKey(Key.Enter);
            await app.WaitForSignInCheckAsync(dialog);
            await app.WaitForAccountAsync(account => account is { State: AccountState.SignedIn });
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => screen.IsClosed, "the settings screen to close");
            await WaitForHelloAsync(app, logMark);
            Assert.Equal(["GET user 200", "GET repo:octo/hello 200", "GET pulls:octo/hello 200"], Routes(served, requests));
            Assert.All(ApiSince(served, requests), request => Assert.Equal((true, "octo"), (request.TokenPresent, request.Login)));
            Assert.Equal([5, 3, 2, 1], hello.HostingState.List!.PullRequests.Select(pull => pull.Number));
            Assert.DoesNotContain(N.TopicRef, hello.HostingState.ByRef.Keys);
            AssertSectionRows(app, [.. P.HelloRows]);
            Assert.False(((RefItemRow)app.RefRowModel("Local branches", "topic")).IsOpen);

            // Step 2: the menu and the form.
            requests = served.GitHub.Requests.Count;
            app.Click(await app.RefRowAsync("Local branches", "topic"));
            var menu = await MenuOfAsync(app, "Local branches", "topic", "no-pr");
            AssertMenu(menu, N.TopicMenu(P.CreatePullRequest));
            AssertHighlighted(menu, "Check out topic/no-pr");
            AssertMenuHeight(app, menu, N.TopicMenuHeight);
            var form = await OpenFormFromMenuAsync(app);
            await app.WaitForAsync(() => app.FocusedAutomationId() == "PullRequestTitle", "the focus in the title");
            AssertForm(app, form, N.Topic, null, N.OriginMain, N.TopicTitle);
            Assert.Equal(N.TopicTitle.Length, ((TextBox)app.FindByAutomationId("PullRequestTitle")).CaretIndex);
            Assert.Equal((string.Empty, false), (form.Description, form.IsDraft));
            Assert.True(app.FindByAutomationId("PullRequestBaseChooseButton").IsEffectivelyEnabled);
            Assert.True(app.FindByAutomationId("CancelButton").IsEffectivelyEnabled);
            Assert.True(app.FindByAutomationId("ConfirmButton").IsEffectivelyEnabled);
            AssertFormPlaces(app, N.CardLeft);
            AssertFormLayout(app);
            Assert.Empty(ApiSince(served, requests));
            ShotN(app, N.N01a);

            // Step 3: Create, no push.
            requests = served.GitHub.Requests.Count;
            var gitRequests = served.GitHub.GitRequests.Count;
            logMark = app.LogMark();
            var operations = OperationMark(app);
            var calls = await app.QuietCallMarkAsync();
            using (var dialogs = new DialogRecorder(app))
            {
                app.Click(app.FindByAutomationId("ConfirmButton"));
                await WaitForEndAsync(app, N.Topic, logMark, OperationOutcome.Succeeded);
                await WaitForListedAsync(app, 5, logMark, times: 2);
                Assert.Empty(dialogs.Opened);
            }

            Assert.True(form.IsClosed);
            ShotN(app, N.N01b);
            Assert.Null(app.Session.Services.DialogHost.Current);
            Assert.Equal([(N.OperationTitle(N.Topic), OperationOutcome.Succeeded, N.Created(6, false))], OperationsSince(app, operations).Select(record => (record.Title, record.Outcome, record.Message)));
            Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
            Assert.Equal(gitRequests, served.GitHub.GitRequests.Count);
            var post = PostSince(served, requests);
            Assert.Equal(("POST", true, "octo", 201), (post.Method, post.TokenPresent, post.Login, post.Status));
            Assert.Equal((N.TopicTitle, string.Empty, N.Topic, N.Main, false, false), Body(post));
            Assert.Equal(["POST create:octo/hello 201", "GET pulls:octo/hello 200 conditional", "GET pulls:octo/hello 304 conditional"], Routes(served, requests));
            var six = Assert.Single(served.GitHub.PullRequests(N.Repository), pull => pull.Number == N.NewNumber);
            Assert.Equal((true, N.TopicTitle, string.Empty, "octo", N.Topic, (string?)null, N.Main, false), (six.IsOpen, six.Title, six.Body, six.Author, six.HeadRef, six.HeadRepository, six.BaseRef, six.IsDraft));
            Assert.Equal(await GitAsync(served.Hello, "rev-parse", N.TopicRef), (await GitInAsync(Bare(served), "rev-parse", "refs/pull/6/head")).Output);
            Assert.Equal([6, 5, 3, 2, 1], hello.HostingState.List!.PullRequests.Select(pull => pull.Number));
            Assert.Equal((6, 6), (hello.HostingState.ByRef[N.TopicRef].Number, hello.HostingState.ByRef[N.OriginTopicRef].Number));
            AssertStatus(app, N.Created(6, false));
            Assert.Equal(N.Created(6, false), ToolTip.GetTip(app.FindByAutomationId("OperationStatus")));
            var offer = (Button)app.FindByAutomationId("OperationOfferButton");
            Assert.True(offer.IsEffectivelyVisible);
            Assert.Equal(N.OpenInBrowser, Avalonia.Automation.AutomationProperties.GetName(offer));
            Assert.Equal(N.OpenInBrowser, hello.OperationStatus.OfferText);
            Assert.Equal(app.BoundsOf(app.FindByAutomationId("OperationStatus")).Right + 6, app.BoundsOf(offer).X, 1.0);
            Assert.False(app.FindByAutomationId("OperationProgress").IsVisible);
            Assert.False(app.FindByAutomationId("OperationCancelButton").IsVisible);
            AssertSectionRows(app, [("#6 Try an idea", "octo", false), .. P.HelloRows]);
            await AssertRefPullRequestAsync(app, 6, N.TopicTitle, "Local branches", "topic", "no-pr");
            AssertBadges(app, [(1, N.Topic, 6, N.TopicTitle, false), .. N.EarlierBadges]);
            Assert.Contains(app.LogLinesSince(logMark), line => line.EndsWith("[INF] hello: Create pull request for topic/no-pr: into octo/hello main, draft no, no push", StringComparison.Ordinal));
            Assert.DoesNotContain(app.LogLinesSince(logMark), line => line.Contains("[WRN]", StringComparison.Ordinal));

            // Step 4: the offer's click.
            requests = served.GitHub.Requests.Count;
            operations = OperationMark(app);
            app.Click(offer);
            await app.WaitForAsync(() => OpenedUrls(app).Count == 1, "the page handed to the launcher");
            Assert.Equal([N.PullRequestPage(served.GitHub.Port)], OpenedUrls(app));
            Assert.Empty(ApiSince(served, requests));
            Assert.Empty(OperationsSince(app, operations));
            AssertStatus(app, N.Created(6, false));
            Assert.True(offer.IsEffectivelyVisible);

            // Step 5: the branch's menu and the section's.
            menu = await MenuOfAsync(app, "Local branches", "topic", "no-pr");
            AssertMenu(menu, N.TopicMenu(P.OpenPullRequest(6)));
            AssertMenuHeight(app, menu, N.TopicMenuHeight);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => !app.Session.Shell.Menu.IsOpen, "the menu closed");
            app.RightClick(await app.RefRowAsync(P.Title));
            await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "the section's menu");
            AssertMenu(app.Menu(), N.SectionMenu);
            AssertHighlighted(app.Menu(), "Refresh pull requests");
            AssertMenuHeight(app, app.Menu(), N.SectionMenuHeight);
            app.PressKey(Key.Escape);
            Assert.Empty(ApiSince(served, requests));

            // Step 6: the next operation takes the offer away.
            requests = served.GitHub.Requests.Count;
            logMark = app.LogMark();
            app.Click(app.FindByAutomationId("FetchButton"));
            await app.WaitForLogLineAsync(AppLogFile.OperationEnded(Phase7aExpectations.Hello, "Fetch"), mark: logMark);
            await WaitForListedAsync(app, 5, logMark);
            AssertStatus(app, N.FetchedFromAllRemotes);
            Assert.False(offer.IsVisible);
            var pulls = Assert.Single(served.GitHub.Requests.Skip(requests), request => request.Route == "pulls:octo/hello");
            Assert.Equal((true, 304), (pulls.Conditional, pulls.Status));

            End(app, data, served, N.PullRequestPage(served.GitHub.Port));
        }
        finally
        {
            app.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task Check_N2_N7_N3_not_on_github_cancel_and_draft()
    {
        await using var served = await ServeAsync();
        await N.PrepareAsync(served.Hello);
        using var data = new TempDirectory("phase7a n2");
        var app = await OpenAsync(data, served);
        try
        {
            var hello = app.Session.Shell.ActiveTab.Repository!;
            await WaitForHelloAsync(app);
            Assert.Equal(1, (await served.Hello.GitAllowingFailureAsync(["config", "--get-regexp", @"^branch\.local-work\."])).ExitCode);
            Assert.Equal(1, (await GitInAsync(Bare(served), "rev-parse", "--verify", "--quiet", N.LocalWorkRef)).Exit);

            // Step 2: the note.
            var requests = served.GitHub.Requests.Count;
            var menu = await MenuOfAsync(app, "Local branches", N.LocalWork);
            AssertMenu(menu, P.LocalWorkMenu);
            AssertMenuHeight(app, menu, N.LocalWorkMenuHeight);
            var form = await OpenFormFromMenuAsync(app);
            await app.WaitForAsync(() => app.FocusedAutomationId() == "PullRequestTitle", "the focus in the title");
            AssertForm(app, form, N.LocalWork, N.PushedFirstWithUpstream("origin/local-work"), N.OriginMain, N.LocalWorkTitle);
            var note = app.FindByAutomationId("PullRequestHeadNote");
            Assert.Contains("settingsNote", note.Classes);
            Assert.True(app.BoundsOf(note).Top >= app.BoundsOf(app.FindByAutomationId("PullRequestHead")).Bottom - 0.5, "The note is not under the head.");
            Assert.Empty(ApiSince(served, requests));
            ShotN(app, N.N02a);

            // N7, steps 3 and 4: Cancel while the push is held.
            requests = served.GitHub.Requests.Count;
            var logMark = app.LogMark();
            var operations = OperationMark(app);
            using var dialogs = new DialogRecorder(app);
            var calls = await app.QuietCallMarkAsync();
            var pushHold = served.GitHub.HoldNext("git:octo/hello");
            try
            {
                app.Click(app.FindByAutomationId("ConfirmButton"));
                await app.WaitForAsync(() => pushHold.Arrived.IsCompleted, "the push held");
                await app.WaitForAsync(() => hello.OperationStatus.Text == N.Pushing(N.LocalWork, "origin/local-work"), "the push's running text");
                AssertCreating(app, form, cancelEnabled: true);
                Assert.True(hello.OperationStatus.CanCancel);
                Assert.True(hello.OperationStatus.IsIndeterminate);
                Assert.True(app.FindByAutomationId("OperationCancelButton").IsVisible);
                Assert.Equal(N.StopGit, ToolTip.GetTip(app.FindByAutomationId("OperationCancelButton")));
                AssertCalled(app, calls, @"git config --null --get-regexp ^remote\.");
                Assert.Empty(ApiSince(served, requests));
                ShotN(app, N.N07a);
                app.PressKey(Key.Escape);
                app.Settle();
                Assert.False(form.IsClosed);

                app.Click(app.FindByAutomationId("CancelButton"));
                await WaitForEndAsync(app, N.LocalWork, logMark, OperationOutcome.Cancelled);
            }
            finally
            {
                pushHold.Dispose();
            }

            await app.WaitForWatchersQuietAsync();
            await app.WaitForAsync(() => !form.IsCreating, "the form idle again");

            // The call log records a git call when it ends: the push stopped by Cancel is there now.
            AssertCalledInOrder(app, calls, @"git config --null --get-regexp ^remote\.", "git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work");
            Assert.False(form.IsClosed);
            AssertForm(app, form, N.LocalWork, N.PushedFirstWithUpstream("origin/local-work"), N.OriginMain, N.LocalWorkTitle, N.Cancelled);
            AssertIdle(app, form);
            Assert.Same(form, app.Session.Services.DialogHost.Current);
            AssertStatus(app, N.CancelledStatus(N.LocalWork));
            Assert.False(hello.OperationStatus.HasOffer);
            Assert.False(app.FindByAutomationId("OperationCancelButton").IsVisible);
            Assert.DoesNotContain(served.GitHub.Requests.Skip(requests), request => request.Route == "create:octo/hello");
            Assert.DoesNotContain(served.GitHub.GitRequests, request => request.Path.EndsWith("git-receive-pack", StringComparison.Ordinal) && request.Method == "POST");
            Assert.Equal(1, (await served.Hello.GitAllowingFailureAsync(["config", "--get-regexp", @"^branch\.local-work\."])).ExitCode);
            Assert.Equal(1, (await served.Hello.GitAllowingFailureAsync(["rev-parse", "--verify", "--quiet", "refs/remotes/origin/local-work"])).ExitCode);
            Assert.Equal(1, (await GitInAsync(Bare(served), "rev-parse", "--verify", "--quiet", N.LocalWorkRef)).Exit);
            ShotN(app, N.N07b);

            // N2 and N3, step 5: Draft and a description; the POST held.
            app.Click(app.FindByAutomationId("PullRequestDraft"));
            Assert.Null(form.Error);
            app.TypeInto("PullRequestDescription", N.Note);
            Assert.Equal((true, N.Note), (form.IsDraft, form.Description));
            requests = served.GitHub.Requests.Count;
            var gitRequests = served.GitHub.GitRequests.Count;
            logMark = app.LogMark();
            calls = await app.QuietCallMarkAsync();
            var postHold = served.GitHub.HoldNext("create:octo/hello");
            try
            {
                app.Click(app.FindByAutomationId("ConfirmButton"));
                await app.WaitForAsync(() => postHold.Arrived.IsCompleted, "the POST held");
                await app.WaitForAsync(() => hello.OperationStatus.Text == N.CreatingPullRequest, "the POST's running text");
                await app.WaitForWatchersQuietAsync();
                AssertCreating(app, form, cancelEnabled: false);
                Assert.False(app.FindByAutomationId("OperationCancelButton").IsVisible);
                Assert.True(hello.OperationStatus.IsIndeterminate);
                AssertCalledInOrder(app, calls, @"git config --null --get-regexp ^remote\.", "git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work");
                Assert.Single(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
                var receive = Assert.Single(served.GitHub.GitRequests.Skip(gitRequests), request => request.Method == "POST" && request.Path.EndsWith("git-receive-pack", StringComparison.Ordinal));
                Assert.Equal((200, "x-access-token"), (receive.Status, receive.GivenUserName));
                Assert.All(served.GitHub.GitRequests.Skip(gitRequests).Where(request => request.GivenUserName is not null), request => Assert.Equal("x-access-token", request.GivenUserName));
                Assert.Equal(await GitAsync(served.Hello, "rev-parse", N.LocalWorkRef), (await GitInAsync(Bare(served), "rev-parse", N.LocalWorkRef)).Output);
                Assert.Empty(ApiSince(served, requests));
                Assert.Null(hello.Refs!.Refs.FirstOrDefault(gitRef => gitRef.FullName == "refs/remotes/origin/local-work"));
                Assert.Null(hello.Refs.Refs.Single(gitRef => gitRef.FullName == N.LocalWorkRef).Upstream);
                ShotN(app, N.N02b);
                app.PressKey(Key.Escape);
                app.Settle();
                Assert.False(form.IsClosed);
            }
            finally
            {
                postHold.Dispose();
            }

            // Step 6: created as a draft.
            await WaitForEndAsync(app, N.LocalWork, logMark, OperationOutcome.Succeeded);
            await WaitForListedAsync(app, 5, logMark, times: 2);
            ShotN(app, N.N03a);
            Assert.Null(app.Session.Services.DialogHost.Current);
            Assert.Empty(dialogs.Opened);
            Assert.Equal(["POST create:octo/hello 201", "GET pulls:octo/hello 200 conditional", "GET pulls:octo/hello 304 conditional"], Routes(served, requests));
            Assert.Equal((N.LocalWorkTitle, N.Note, N.LocalWork, N.Main, true, false), Body(PostSince(served, requests)));
            var six = Assert.Single(served.GitHub.PullRequests(N.Repository), pull => pull.Number == N.NewNumber);
            Assert.Equal((true, true, N.LocalWorkTitle, N.Note, "octo", N.LocalWork, N.Main), (six.IsOpen, six.IsDraft, six.Title, six.Body, six.Author, six.HeadRef, six.BaseRef));
            AssertStatus(app, N.Created(6, true));
            Assert.True(hello.OperationStatus.HasOffer);
            Assert.Equal(N.OpenInBrowser, hello.OperationStatus.OfferText);
            AssertSectionRows(app, [("#6 Local change", "octo", true), .. P.HelloRows]);
            Assert.Equal(N.DraftSixToolTip, app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<PullRequestRow>().First().ToolTip);
            Assert.Equal((6, 6), (hello.HostingState.ByRef[N.LocalWorkRef].Number, hello.HostingState.ByRef["refs/remotes/origin/local-work"].Number));
            AssertBadges(app, [(0, N.LocalWork, 6, N.LocalWorkTitle, true), .. N.EarlierBadges]);
            await app.RefRowAsync("Remotes", "origin", N.LocalWork);
            await AssertRefPullRequestAsync(app, 6, N.LocalWorkTitle, "Local branches", N.LocalWork);

            // The activity log's operations since step 3 (open point 47): one per Create, the push in the second's calls.
            var records = OperationsSince(app, operations);
            Assert.Equal(
                [(N.OperationTitle(N.LocalWork), OperationOutcome.Cancelled), (N.OperationTitle(N.LocalWork), OperationOutcome.Succeeded)],
                records.Select(record => (record.Title, record.Outcome)));
            Assert.Contains(app.GitCalls(), call => call.OperationId == records[1].Id && call.CommandText == "git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work");
            Assert.DoesNotContain(app.LogLines(), line => line.Contains(N.Note, StringComparison.Ordinal));
            Assert.Contains(app.LogLinesSince(logMark), line => line.EndsWith("[INF] hello: Create pull request for local-work: into octo/hello main, draft yes, push to origin/local-work first", StringComparison.Ordinal));

            End(app, data, served);
        }
        finally
        {
            app.Dispose();
        }

        // Step 7: the app closed.
        Assert.Equal("origin", await GitAsync(served.Hello, "config", "--get", "branch.local-work.remote"));
        Assert.Equal("refs/heads/local-work", await GitAsync(served.Hello, "config", "--get", "branch.local-work.merge"));
        var tip = await GitAsync(served.Hello, "rev-parse", N.LocalWorkRef);
        Assert.Equal(tip, await GitAsync(served.Hello, "rev-parse", "refs/remotes/origin/local-work"));
        Assert.Equal(tip, (await GitInAsync(Bare(served), "rev-parse", N.LocalWorkRef)).Output);
        Assert.Equal(tip, (await GitInAsync(Bare(served), "rev-parse", "refs/pull/6/head")).Output);
    }

    [AvaloniaFact]
    public async Task Check_N4_N8_already_open_the_form_and_no_commits()
    {
        await using var served = await ServeAsync();
        await N.PrepareAsync(served.Hello);
        using var data = new TempDirectory("phase7a n4");
        var app = await OpenAsync(data, served);
        try
        {
            var hello = app.Session.Shell.ActiveTab.Repository!;
            await WaitForHelloAsync(app);
            served.GitHub.AddPullRequest("octo", "hello", N.TopicTitle, "octo", N.Topic, N.Main);
            Assert.Contains(served.GitHub.PullRequests(N.Repository), pull => pull.Number == 6 && pull.IsOpen);
            Assert.Equal(4, hello.HostingState.List!.PullRequests.Count);

            // Step 2: already open.
            app.Click(await app.RefRowAsync("Local branches", "topic"));
            await AssertRefPullRequestAsync(app, null, null, "Local branches", "topic", "no-pr");
            AssertMenu(await MenuOfAsync(app, "Local branches", "topic", "no-pr"), N.TopicMenu(P.CreatePullRequest));
            var form = await OpenFormFromMenuAsync(app);
            var requests = served.GitHub.Requests.Count;
            var logMark = app.LogMark();
            using (var dialogs = new DialogRecorder(app))
            {
                app.Click(app.FindByAutomationId("ConfirmButton"));
                await WaitForEndAsync(app, N.Topic, logMark, OperationOutcome.Failed);
                await WaitForListedAsync(app, 5, logMark);
                await app.WaitForAsync(() => !form.IsCreating, "the form idle again");
                Assert.Empty(dialogs.Opened);
            }

            ShotN(app, N.N04a);
            Assert.Equal(["POST create:octo/hello 422", "GET pulls:octo/hello 200 conditional"], Routes(served, requests));
            Assert.Equal((N.TopicTitle, string.Empty, N.Topic, N.Main, false, false), Body(PostSince(served, requests)));
            AssertForm(app, form, N.Topic, null, N.OriginMain, N.TopicTitle, N.AlreadyOpen(6, N.Topic, N.OriginMain), 6);
            Assert.Equal(N.OpenExisting(6), ((Button)app.FindByAutomationId("PullRequestOpenExisting")).Content);
            AssertIdle(app, form);
            Assert.Same(form, app.Session.Services.DialogHost.Current);
            Assert.Equal([1, 2, 3, 4, 5, 6], served.GitHub.PullRequests(N.Repository).Select(pull => pull.Number).Order());
            AssertStatus(app, N.Failed(N.Topic), OperationStatusTone.Danger);
            Assert.False(hello.OperationStatus.HasOffer);
            AssertSectionRows(app, [("#6 Try an idea", "octo", false), .. P.HelloRows]);
            await AssertRefPullRequestAsync(app, 6, N.TopicTitle, "Local branches", "topic", "no-pr");
            AssertRefusedPostWarnings(app, logMark, 422, "Validation", "Validation Failed");

            // Step 3: the existing pull request's button.
            requests = served.GitHub.Requests.Count;
            app.Click(app.FindByAutomationId("PullRequestOpenExisting"));
            await app.WaitForAsync(() => form.IsClosed, "the form closed");
            Assert.Equal([N.PullRequestPage(served.GitHub.Port)], OpenedUrls(app));
            Assert.Empty(ApiSince(served, requests));
            AssertStatus(app, N.Failed(N.Topic), OperationStatusTone.Danger);

            // N8, step 4: main's form, its own branch.
            await MenuOfAsync(app, "Local branches", N.Main);
            form = await OpenFormFromMenuAsync(app);
            AssertForm(app, form, N.Main, null, N.OriginMain, N.MainTitle, N.SameBranch(N.Main));
            Assert.False(app.FindByAutomationId("ConfirmButton").IsEffectivelyEnabled);
            Assert.True(app.FindByAutomationId("PullRequestBaseChooseButton").IsEffectivelyEnabled);
            Assert.Empty(ApiSince(served, requests));
            ShotN(app, N.N08a);

            // Step 5: the Pick.
            app.Click(app.FindByAutomationId("PullRequestBaseChooseButton"));
            var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
            Assert.Equal(N.ChooseBaseTitle, pick.Title);
            Assert.Equal(N.BaseOptions, pick.Options.Select(option => option.Text));
            Assert.All(pick.Options, option => Assert.Equal(N.Repository, option.Detail));
            Assert.Equal(N.OriginMain, pick.Selected);
            await app.WaitForAsync(() => app.FocusedAutomationId() == "PickFilter", "the Pick's filter focused");
            Assert.Equal(string.Empty, ((TextBox)app.FindByAutomationId("PickFilter")).Text ?? string.Empty);
            Assert.Equal(["Cancel", N.PickConfirm], ButtonsOf(app, app.TopDialogView!));
            ShotN(app, N.N08b);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => pick.IsClosed, "the Pick closed");
            Assert.False(form.IsClosed);
            Assert.Equal(N.SameBranch(N.Main), form.Error);

            // Step 6: another base.
            app.Click(app.FindByAutomationId("PullRequestBaseChooseButton"));
            pick = await app.WaitForDialogAsync<PickDialogViewModel>();
            pick.Selected = N.OriginTopic;
            app.Click(app.FindByAutomationId("ConfirmButton"));
            await app.WaitForAsync(() => form.BaseText == N.OriginTopic, "the chosen base");
            Assert.Null(form.Error);
            Assert.True(app.FindByAutomationId("ConfirmButton").IsEffectivelyEnabled);

            // Step 7: an empty title.
            app.Click(app.FindByAutomationId("PullRequestTitle"));
            app.PressGesture(new KeyGesture(Key.A, KeyModifiers.Control));
            app.PressKey(Key.Delete);
            var confirm = app.FindByAutomationId("ConfirmButton");
            Assert.False(confirm.IsEffectivelyEnabled);
            app.MoveMouse(confirm);

            // Open and faded in: a picture taken during the theme's fade shows the tooltip half transparent.
            Assert.Equal(N.EnterTitle, await app.WaitForToolTipAsync(confirm));
            var titleBox = (TextBox)app.FindByAutomationId("PullRequestTitle");
            Assert.Equal((string.Empty, N.TitlePlaceholder), (titleBox.Text ?? string.Empty, titleBox.PlaceholderText));
            Assert.Null(form.Error);
            ShotN(app, N.N08c, keepPointer: true);
            app.TypeInto("PullRequestTitle", N.MainTitle);
            Assert.True(confirm.IsEffectivelyEnabled);
            Assert.Empty(ApiSince(served, requests));

            // Step 8: no commits between.
            requests = served.GitHub.Requests.Count;
            logMark = app.LogMark();
            var calls = await app.QuietCallMarkAsync();
            using (var dialogs = new DialogRecorder(app))
            {
                app.Click(confirm);
                await WaitForEndAsync(app, N.Main, logMark, OperationOutcome.Failed);
                await app.WaitForAsync(() => !form.IsCreating, "the form idle again");
                await app.WaitForWatchersQuietAsync();
                Assert.Empty(dialogs.Opened);
            }

            ShotN(app, N.N04b);
            Assert.Equal(["POST create:octo/hello 422"], Routes(served, requests));
            Assert.Equal((N.MainTitle, string.Empty, N.Main, N.Topic, false, false), Body(PostSince(served, requests)));
            Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
            AssertForm(app, form, N.Main, null, N.OriginTopic, N.MainTitle, N.NoCommits(N.OriginTopic, N.Main));
            AssertIdle(app, form);
            AssertStatus(app, N.Failed(N.Main), OperationStatusTone.Danger);
            requests = served.GitHub.Requests.Count;
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => form.IsClosed, "the form closed");
            Assert.Empty(ApiSince(served, requests));

            End(app, data, served, N.PullRequestPage(served.GitHub.Port));
        }
        finally
        {
            app.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task Check_N5_remote_branch_and_drop()
    {
        await using var served = await ServeAsync();
        await N.PrepareAsync(served.Hello);
        using var data = new TempDirectory("phase7a n5");
        var app = await OpenAsync(data, served);
        try
        {
            var hello = app.Session.Shell.ActiveTab.Repository!;
            await WaitForHelloAsync(app);

            // Step 2: the remote branch's menu and form.
            var requests = served.GitHub.Requests.Count;
            app.Click(await app.RefRowAsync("Remotes", "origin", "topic"));
            var menu = await MenuOfAsync(app, "Remotes", "origin", "topic", "no-pr");
            AssertMenu(menu, N.OriginTopicMenu);
            AssertHighlighted(menu, "Check out origin/topic/no-pr");
            AssertMenuHeight(app, menu, N.OriginTopicMenuHeight);
            var form = await OpenFormFromMenuAsync(app);
            AssertForm(app, form, N.OriginTopic, null, N.OriginMain, N.TopicTitle);
            Assert.Empty(ApiSince(served, requests));
            ShotN(app, N.N05a);

            // Step 3: never pushed.
            requests = served.GitHub.Requests.Count;
            var gitRequests = served.GitHub.GitRequests.Count;
            var logMark = app.LogMark();
            var operations = OperationMark(app);
            var calls = await app.QuietCallMarkAsync();
            app.Click(app.FindByAutomationId("ConfirmButton"));
            await WaitForEndAsync(app, N.OriginTopic, logMark, OperationOutcome.Succeeded);
            await WaitForListedAsync(app, 5, logMark, times: 2);
            Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
            Assert.Equal(gitRequests, served.GitHub.GitRequests.Count);
            Assert.Equal(["POST create:octo/hello 201", "GET pulls:octo/hello 200 conditional", "GET pulls:octo/hello 304 conditional"], Routes(served, requests));
            Assert.Equal((N.TopicTitle, string.Empty, N.Topic, N.Main, false, false), Body(PostSince(served, requests)));
            var six = Assert.Single(served.GitHub.PullRequests(N.Repository), pull => pull.Number == N.NewNumber);
            Assert.Equal((true, N.TopicTitle, string.Empty, "octo", N.Topic, (string?)null, N.Main, false), (six.IsOpen, six.Title, six.Body, six.Author, six.HeadRef, six.HeadRepository, six.BaseRef, six.IsDraft));
            Assert.Equal([(N.OperationTitle(N.OriginTopic), OperationOutcome.Succeeded)], OperationsSince(app, operations).Select(record => (record.Title, record.Outcome)));
            Assert.Equal((6, 6), (hello.HostingState.ByRef[N.TopicRef].Number, hello.HostingState.ByRef[N.OriginTopicRef].Number));
            AssertStatus(app, N.Created(6, false));
            Assert.True(hello.OperationStatus.HasOffer);

            // Step 4: the drop.
            var source = app.BoundsOf(await app.RefRowAsync("Local branches", N.LocalWork)).Center;
            var target = app.BoundsOf(await app.RefRowAsync("Remotes", "origin", N.Main)).Center;
            app.PressMouse(source);
            app.MoveMouseHeld(source + new Point(0, 12), steps: 3);
            app.MoveMouseHeld(target, steps: 6);
            app.ReleaseMouse(target);
            await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "the drop's menu");
            AssertMenu(app.Menu(), N.DropMenu);
            AssertHighlighted(app.Menu(), "Push local-work to origin/main");
            AssertMenuHeight(app, app.Menu(), N.DropMenuHeight);
            ShotN(app, N.N05b);
            form = await OpenFormFromMenuAsync(app);
            Assert.False(form.IsReadingBase);
            AssertForm(app, form, N.LocalWork, N.PushedFirstWithUpstream("origin/local-work"), N.OriginMain, N.LocalWorkTitle);
            Assert.True(app.FindByAutomationId("ConfirmButton").IsEffectivelyEnabled);
            AssertStatus(app, N.Created(6, false));
            Assert.True(hello.OperationStatus.HasOffer);
            ShotN(app, N.N05c);

            // Step 5: Esc.
            requests = served.GitHub.Requests.Count;
            operations = OperationMark(app);
            calls = await app.QuietCallMarkAsync();
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => form.IsClosed, "the form closed");
            Assert.Empty(ApiSince(served, requests));
            Assert.Empty(OperationsSince(app, operations));
            Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));

            End(app, data, served);
        }
        finally
        {
            app.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task Check_N6_signed_out_signs_in_first()
    {
        await using var served = await ServeAsync();
        await N.PrepareAsync(served.Hello);
        using var data = new TempDirectory("phase7a n6");
        var app = await OpenAsync(data, served, signIn: false);
        try
        {
            var hello = app.Session.Shell.ActiveTab.Repository!;
            await app.WaitForAsync(() => hello.HostingState.PullRequests == PullRequestsState.SignedOut, "signed out");
            await app.WaitForAsync(() => hello.LeftPanel.Rows.OfType<PullRequestMessageRow>().Any(row => row.Kind == PullRequestMessageKind.SignIn), "the sign-in row");
            await app.WaitForWatchersQuietAsync();
            Assert.Empty(served.GitHub.Requests);
            var section = Section(app);
            var operations = OperationMark(app);
            var storeMark = app.Tokens.Calls.Count;
            var logMark = app.LogMark();

            // Step 2: the sign-in first.
            app.Click(await app.RefRowAsync("Local branches", "topic"));
            AssertMenu(await MenuOfAsync(app, "Local branches", "topic", "no-pr"), N.TopicMenu(P.CreatePullRequest));
            app.ClickMenuItem(P.CreatePullRequest);
            var signIn = await app.WaitForDialogAsync<SignInDialogViewModel>();
            await app.WaitForAsync(() => app.FocusedAutomationId() == "SignInToken", "the focus in the token's box");
            Assert.Equal(string.Empty, ((TextBox)app.FindByAutomationId("SignInToken")).Text ?? string.Empty);
            Assert.IsType<SignInDialogViewModel>(app.Session.Services.DialogHost.Current);
            Assert.Empty(served.GitHub.Requests);
            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, open => open is CreatePullRequestDialogViewModel);
            ShotN(app, N.N06a);

            // Step 3: Cancel.
            app.Click(app.FindByAutomationId("CancelButton"));
            await app.WaitForAsync(() => signIn.IsClosed, "the sign-in dialog closed");
            app.Settle();
            Assert.Null(app.Session.Services.DialogHost.Current);
            Assert.Equal(PullRequestsState.SignedOut, hello.HostingState.PullRequests);
            Assert.Empty(served.GitHub.Requests);
            Assert.Equal(["probe"], app.Tokens.Calls.Skip(storeMark));
            Assert.Empty(OperationsSince(app, operations));
            Assert.Equal(section, Section(app));

            // Step 4: a sign-in goes on to the form.
            var requests = served.GitHub.Requests.Count;
            logMark = app.LogMark();
            await MenuOfAsync(app, "Local branches", "topic", "no-pr");
            app.ClickMenuItem(P.CreatePullRequest);
            await app.WaitForDialogAsync<SignInDialogViewModel>();
            app.TypeToken(served.OctoToken);
            app.PressKey(Key.Enter);
            var form = await app.WaitForDialogAsync<CreatePullRequestDialogViewModel>();
            await app.WaitForAsync(() => form.BaseText == N.OriginMain, "the form's base");
            await WaitForHelloAsync(app, logMark);
            AssertForm(app, form, N.Topic, null, N.OriginMain, N.TopicTitle);
            Assert.Equal(["GET user 200", "GET repo:octo/hello 200", "GET pulls:octo/hello 200"], Routes(served, requests));
            Assert.All(ApiSince(served, requests), request => Assert.True(request.TokenPresent));
            AssertSectionRows(app, [.. P.HelloRows]);
            Assert.Contains("save github:127.0.0.1:" + served.GitHub.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":octo", app.Tokens.Calls.Skip(storeMark));
            ShotN(app, N.N06b);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => form.IsClosed, "the form closed");
            Assert.DoesNotContain(served.GitHub.Requests, request => request.Route == "create:octo/hello");

            End(app, data, served);
        }
        finally
        {
            app.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task Check_N9_refused_and_a_lost_answer()
    {
        await using var served = await ServeAsync();
        await N.PrepareAsync(served.Hello);
        using var data = new TempDirectory("phase7a n9");
        var app = await OpenAsync(data, served);
        try
        {
            var hello = app.Session.Shell.ActiveTab.Repository!;
            await WaitForHelloAsync(app);

            // Step 2: a 403.
            app.Click(await app.RefRowAsync("Local branches", "topic"));
            await MenuOfAsync(app, "Local branches", "topic", "no-pr");
            var form = await OpenFormFromMenuAsync(app);
            served.GitHub.FailNext("create:octo/hello", FakeGitHubFault.Forbidden);
            using var dialogs = new DialogRecorder(app);
            var requests = served.GitHub.Requests.Count;
            var logMark = app.LogMark();
            app.Click(app.FindByAutomationId("ConfirmButton"));
            await WaitForEndAsync(app, N.Topic, logMark, OperationOutcome.Failed);
            await app.WaitForAsync(() => !form.IsCreating, "the form idle again");
            await app.WaitForWatchersQuietAsync();
            ShotN(app, N.N09a);
            Assert.Equal(["POST create:octo/hello 403"], Routes(served, requests));
            Assert.True(PostSince(served, requests).TokenPresent);
            Assert.Equal(AccountState.SignedIn, app.Session.Services.Hosting.Account?.State);
            AssertForm(app, form, N.Topic, null, N.OriginMain, N.TopicTitle, N.RefusedWithPermission);
            AssertIdle(app, form);
            Assert.Same(form, app.Session.Services.DialogHost.Current);
            AssertStatus(app, N.Failed(N.Topic), OperationStatusTone.Danger);
            AssertRefusedPostWarnings(app, logMark, 403, "Forbidden", "Resource not accessible by personal access token");
            Assert.DoesNotContain(served.GitHub.PullRequests(N.Repository), pull => pull.Number == 6);

            // Step 3: a lost answer counted as created (D316).
            served.GitHub.FailNext("create:octo/hello", FakeGitHubFault.CreatedThenDropped);
            requests = served.GitHub.Requests.Count;
            logMark = app.LogMark();
            app.Click(app.FindByAutomationId("ConfirmButton"));
            var end = await WaitForEndAsync(app, N.Topic, logMark, OperationOutcome.Succeeded);
            var endAt = app.LogLines().ToList().IndexOf(end);
            await app.WaitForAsync(() => app.LogLines().Skip(endAt + 1).Any(AppLogFile.PullRequestsListed(Phase7aExpectations.Hello).IsMatch), "D289's list after the end");
            await app.WaitForWatchersQuietAsync();
            Assert.True(form.IsClosed);
            AssertStatus(app, N.Created(6, false));
            Assert.True(hello.OperationStatus.HasOffer);
            Assert.Empty(dialogs.Opened);
            var api = ApiSince(served, requests);
            Assert.Single(api, request => request.Route == "create:octo/hello");
            Assert.Equal((N.TopicTitle, string.Empty, N.Topic, N.Main, false, false), Body(PostSince(served, requests)));
            var lists = api.Where(request => request.Route == "pulls:octo/hello").ToList();
            Assert.Equal(200, lists[0].Status);
            Assert.Equal((true, 304), (lists[^1].Conditional, lists[^1].Status));
            Assert.DoesNotContain(api, request => request.Status == 401);
            var six = Assert.Single(served.GitHub.PullRequests(N.Repository), pull => pull.Number == N.NewNumber);
            Assert.Equal((true, N.TopicTitle, string.Empty, "octo", N.Topic, (string?)null, N.Main, false), (six.IsOpen, six.Title, six.Body, six.Author, six.HeadRef, six.HeadRepository, six.BaseRef, six.IsDraft));
            AssertSectionRows(app, [("#6 Try an idea", "octo", false), .. P.HelloRows]);
            Assert.Equal((6, 6), (hello.HostingState.ByRef[N.TopicRef].Number, hello.HostingState.ByRef[N.OriginTopicRef].Number));

            End(app, data, served);
        }
        finally
        {
            app.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task Check_N10_the_form_at_1920x1080_and_light()
    {
        await using var served = await ServeAsync();
        await N.PrepareAsync(served.Hello);
        using var data = new TempDirectory("phase7a n10");
        var app = await OpenAsync(data, served, width: 1920, height: 1080);
        try
        {
            await WaitForHelloAsync(app);
            var requests = served.GitHub.Requests.Count;
            var rest = new Point(N.RestPoint1920.X, N.RestPoint1920.Y);

            app.Click(await app.RefRowAsync("Local branches", "topic"));
            await MenuOfAsync(app, "Local branches", "topic", "no-pr");
            var form = await OpenFormFromMenuAsync(app);
            AssertForm(app, form, N.Topic, null, N.OriginMain, N.TopicTitle);
            AssertFormPlaces(app, N.CardLeft1920);
            AssertFormLayout(app);
            app.MoveMouse(rest);
            ShotN(app, N.N10a, keepPointer: true);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => form.IsClosed, "the form closed");

            app.Click(app.FindByAutomationId("ThemeSwitch"));
            await MenuOfAsync(app, "Local branches", "topic", "no-pr");
            form = await OpenFormFromMenuAsync(app);
            AssertForm(app, form, N.Topic, null, N.OriginMain, N.TopicTitle);
            AssertFormPlaces(app, N.CardLeft1920);
            AssertFormLayout(app);
            var card = app.FindByAutomationId("CreatePullRequestDialog").GetVisualDescendants().OfType<Border>().First(border => border.Name == "CreatePullRequestDialogCard");
            Assert.Equal(Color.Parse("#F5F6F8"), ((ISolidColorBrush)card.Background!).Color);
            var secondary = card.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.Text is N.From or N.Into || block.Name is "PullRequestHeadRepository" or "PullRequestBaseRepository")
                .ToList();
            Assert.Equal(4, secondary.Count);
            Assert.All(secondary, block => Assert.Equal(Color.Parse("#5C6572"), ((ISolidColorBrush)block.Foreground!).Color));
            Assert.Same(app.Window.FindResource("VcPrimaryButton"), ((Button)app.FindByAutomationId("ConfirmButton")).Theme);
            app.MoveMouse(rest);
            ShotN(app, N.N10b, keepPointer: true);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => form.IsClosed, "the form closed");
            Assert.Empty(ApiSince(served, requests));

            End(app, data, served);
        }
        finally
        {
            app.Dispose();
        }
    }
}
