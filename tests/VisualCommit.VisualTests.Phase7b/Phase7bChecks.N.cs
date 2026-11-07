using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Hosting;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
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
/// Part N, a new pull or merge request (docs/test-reports/phase-7b.md, part N, checks N1 to N9;
/// design 13.8): one method per app start, as the report's methods table gives them. Every method
/// prepares the clones (<see cref="PrepareRequestsAsync"/>) before it opens the app. Methods 1 and 2
/// are the R twins (the branch's menu, the form, the create), 3 and 4 start signed out (the sign-in
/// first, the remote branch's, <c>local-work</c>'s and the drop's items, the push first, Draft), 5 and
/// 6 make the refusals (6 also the long names), and 7 the form at 1920×1080 and in light.
/// </summary>
public sealed partial class Phase7bChecksN : Phase7bChecks
{
    [AvaloniaFact]
    public Task Check_N1_N2_N6_gitlab_branch_menu_form_and_created() => BranchMenuFormAndCreatedAsync(GitLab);

    [AvaloniaFact]
    public Task Check_N1_N2_N6_azure_branch_menu_form_and_created() => BranchMenuFormAndCreatedAsync(Azure);

    /// <summary>Methods 1 and 2: N1's local branch, N2's form and N6's create, for <paramref name="p"/>.</summary>
    private static async Task BranchMenuFormAndCreatedAsync(HostingProvider p)
    {
        await using var served = await ServeAsync();
        await PrepareRequestsAsync(served.MobileApp, p);
        using var data = new TempDirectory($"phase7b n1 {p}");
        var app = await OpenAsync(data, served, N.RepositoryOf(p), signIn: []);
        var number = N.NextOf(p);
        var shown = N.Of(p);
        try
        {
            var repository = app.Session.Shell.ActiveTab.Repository!;

            // N1 step 1: signed in through Settings, Accounts.
            var mark = Mark(served);
            var stored = app.Tokens.Calls.Count;
            var logMark = app.LogMark();
            await SignInThroughSettingsAsync(app, p, TokenOf(served, p));
            await WaitForListedAsync(app, p, 3, logMark);
            await AssertNoDialogAsync(app);
            AssertSignInRequests(served, p, RequestsSince(served, p, mark));
            Assert.Equal(["probe", KeyOf(served, p)], StoreCallsSince(app, stored));
            Assert.Equal([$"v {P.TitleOf(p)} 3", .. P.RowsOf(p).Select(row => row.Name)], Section(app));
            var topic = (RefItemRow)app.RefRowModel("Local branches", N.TopicFolder);
            Assert.Equal((true, false), (topic.IsContainer, topic.IsOpen));

            // N1 step 2: the prepared branch's menu.
            mark = Mark(served);
            app.Click(await app.RefRowAsync("Local branches", N.TopicFolder));
            await MenuOfAsync(app, "Local branches", N.TopicFolder, N.NoRequest);
            AssertWholeMenu(app, N.LocalBranchMenuOf(p), $"Check out {N.Head}", N.LocalBranchMenuHeight);
            AssertNoRequests(served, mark);

            // N2 step 3: the form.
            var operations = OperationMark(app);
            var form = await OpenFormFromMenuAsync(app, p);
            await AssertN2FormAsync(app, form, p, N.CardLeft);

            Assert.Empty(OperationsSince(app, operations));
            AssertNoRequests(served, mark);
            ShotN(app, shown.FormPicture);

            // N6 step 4: Create.
            mark = Mark(served);
            var gitRequests = GitRequestsOf(served, p).Count;
            logMark = app.LogMark();
            var calls = await app.QuietCallMarkAsync();
            using (var dialogs = new DialogRecorder(app))
            {
                app.Click(app.FindByAutomationId("ConfirmButton"));
                await WaitForEndAsync(app, p, N.Head, logMark, OperationOutcome.Succeeded);
                await WaitForListedAsync(app, p, N.CountAfterCreate, logMark, times: 2);
                ShotN(app, shown.CreatedPicture);
                Assert.Empty(dialogs.Opened);
            }

            Assert.Equal([(shown.OperationTitle, OperationOutcome.Succeeded, shown.StatusText)], OperationsSince(app, operations).Select(record => (record.Title, record.Outcome, record.Message)));
            Assert.True(form.IsClosed);
            Assert.Null(app.Session.Services.DialogHost.Current);
            Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
            Assert.Equal(gitRequests, GitRequestsOf(served, p).Count);
            var requests = RequestsSince(served, p, mark);
            Assert.Equal(
                p == GitLab
                    ? [$"GET {N.GitLabSourceRoute} 304", $"POST {N.GitLabCreateRoute} 201", $"GET {N.GitLabListRoute} 200", $"GET {N.GitLabListRoute} 304"]
                    : [$"POST {N.AzureDevOpsCreateRoute} 201", $"GET {N.AzureDevOpsListRoute} 200", $"GET {N.AzureDevOpsListRoute} 200"],
                requests.Select(Describe));
            AssertSentAsAda(served, p, requests);
            var post = PostSince(served, p, mark);
            Assert.Equal(p == GitLab ? (N.GitLabCreatePath, string.Empty) : (N.AzureDevOpsCreatePath, N.AzureDevOpsCreateQuery), (post.Path, post.Query));
            AssertBody(post, shown.Body, shown.AbsentMembers);
            Assert.All(requests.Where(request => request.Route == N.ListRouteOf(p)), request => Assert.Equal(p == GitLab ? N.GitLabListQuery : N.AzureDevOpsListQuery, request.Query));
            if (p == GitLab)
            {
                Assert.Equal(N.GitLabSourcePath, requests[0].Path);
                var created = Assert.Single(served.GitLab.MergeRequests(served.GitLab.Project(WebAppPath)), request => request.Iid == number);
                var project = served.GitLab.Project(WebAppPath);
                Assert.Equal(("opened", N.HeadTitle, false, AdaLogin, N.Head, "main", string.Empty), (created.State, created.Title, created.Draft, created.Author, created.SourceBranch, created.TargetBranch, created.Description ?? string.Empty));
                Assert.Same(project, created.Source);
                Assert.Same(project, created.Target);
                Assert.Equal(await GitAsync(served.WebApp, "rev-parse", N.HeadRef), (await GitInAsync(Bare(served, p), "rev-parse", "refs/merge-requests/24/head")).Output);
            }
            else
            {
                var created = Assert.Single(served.AzureDevOps.PullRequests(served.AzureDevOps.Repository(Organization, Project, MobileApp)), request => request.Id == number);
                Assert.Equal((FakeAzurePullRequestStatus.Active, N.HeadTitle, false, N.Head, "main"), (created.Status, created.Title, created.IsDraft, created.SourceBranch, created.TargetBranch));
                Assert.Same(served.AzureDevOps.User(Organization, AdaName), created.Creator);
                Assert.Null(created.Fork);
                Assert.Equal("succeeded", created.MergeStatus);
                Assert.Equal(0, (await GitInAsync(Bare(served, p), "rev-parse", "--verify", "--quiet", "refs/pull/14/merge")).Exit);
                Assert.Equal(1, (await GitInAsync(Bare(served, p), "rev-parse", "--verify", "--quiet", "refs/pull/14/head")).Exit);
            }

            var state = repository.HostingState;
            Assert.Equal(PullRequestsState.Loaded, state.PullRequests);
            Assert.False(state.List!.MoreExist);
            Assert.Equal([number, .. P.RowsOf(p).Select(row => row.Number)], state.List.PullRequests.Select(pull => pull.Number));
            Assert.Equal((number, number), (state.ByRef[N.HeadRef].Number, state.ByRef[N.RemoteHeadRef].Number));
            AssertStatus(app, shown.StatusText);
            Assert.Equal(shown.StatusText, ToolTip.GetTip(app.FindByAutomationId("OperationStatus")));
            var offer = AssertOfferShown(app, repository);
            var author = p == GitLab ? AdaLogin : AdaName;
            AssertSectionAfterCreate(app, p, number, N.HeadTitle, author, N.Head, draft: false);
            Assert.Equal(shown.NewRowName, PullRequestRows(app)[0].Text);
            await AssertBranchNumberAsync(app, p, number, N.HeadTitle, "Local branches", N.TopicFolder, N.NoRequest);
            AssertNewBadge(app, p, 1, N.Head, number, N.HeadTitle, draft: false);
            AssertOtherBadges(app, p, 1);
            AssertInfoOnce(app, logMark, N.IntoLineOf(p, N.Head, false, null));
            AssertInfoOnce(app, logMark, N.CreatedLineOf(p, N.Head, number));
            Assert.DoesNotContain(LogSince(app, logMark), line => line.StartsWith("[WRN]", StringComparison.Ordinal));

            // N6 step 5: the offer's click.
            mark = Mark(served);
            app.Click(offer);
            await app.WaitForAsync(() => app.Launcher.Requests.Count > 0, "the launcher's request");
            Assert.Equal([WithPorts(N.WebUrlOf(p, number), served)], app.Launcher.Requests.Where(request => request.Kind == FileLaunchKind.OpenUrl).Select(request => request.FullPath));
            Assert.Empty(OperationsSince(app, operations).Skip(1));
            AssertStatus(app, shown.StatusText);
            Assert.True(repository.OperationStatus.HasOffer);

            // N6 step 6: the branch's menu and the section's.
            var menu = await MenuOfAsync(app, "Local branches", N.TopicFolder, N.NoRequest);
            AssertWholeMenu(app, [.. N.LocalBranchMenuOf(p).Select(line => line == N.CreateItemOf(p) ? N.OpenExistingOf(p, number) : line)], $"Check out {N.Head}", N.LocalBranchMenuHeight);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            var header = app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<Button>()
                .First(button => button.DataContext is RefSectionRow { Section: RefSection.PullRequests } && button.IsEffectivelyVisible);
            var headerCentre = app.BoundsOf(header).Center;
            app.RightClick(headerCentre);
            await app.WaitForAsync(() => app.Session.Shell.Menu.IsOpen, "the section's menu");
            AssertWholeMenu(app, P.SectionMenu(p), P.RefreshOf(p), P.SectionMenuHeight);
            AssertMenuPlaced(app, app.Menu(), headerCentre);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            AssertNoRequests(served, mark);

            // N6 step 7: the next operation ends the offer (P11's checkout).
            mark = Mark(served);
            var checkoutMark = app.LogMark();
            var checkedOut = p == GitLab ? "feature/search" : "feature/sign-in";
            app.DoubleClick(await RequestRowAsync(app, p == GitLab ? 21 : 11));
            await app.WaitForAsync(() => repository.OperationStatus.Text == P.CheckedOut(checkedOut), "the checkout's end");
            await app.WaitForWatchersQuietAsync();
            AssertStatus(app, P.CheckedOut(checkedOut));
            Assert.False(repository.OperationStatus.HasOffer);
            Assert.False(offer.IsVisible);
            AssertNoRequests(served, mark);
            Assert.Equal(0, ListedSince(app, p, checkoutMark));
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [TokenOf(served, p)], [N.WebUrlOf(p, number)], keptOut: [N.HeadTitle]);
    }

    [AvaloniaFact]
    public async Task Check_N8_N1_N4_N3_gitlab_signed_out_drop_pushed_first_and_draft()
    {
        await using var served = await ServeAsync();
        await PrepareRequestsAsync(served.MobileApp, GitLab);
        using var data = new TempDirectory("phase7b n8 gitlab");
        var app = await OpenAsync(data, served, WebApp, signIn: []);
        string tip;
        try
        {
            var webApp = app.Session.Shell.ActiveTab.Repository!;

            // N8 step 1: signed out.
            await WaitForStateAsync(app, webApp, PullRequestsState.SignedOut, PullRequestMessageKind.SignIn);
            AssertSignedOut(app, served, webApp, GitLab);
            Assert.Equal(1, (await served.WebApp.GitAllowingFailureAsync(["config", "--get-regexp", @"^branch\.local-work\."])).ExitCode);
            Assert.Equal(1, (await GitInAsync(Bare(served, GitLab), "rev-parse", "--verify", "--quiet", N.LocalWorkRef)).Exit);
            var mark = Mark(served);
            var stored = app.Tokens.Calls.Count;
            var section = Section(app);
            var operations = OperationMark(app);

            // N1 step 2: the remote branch's menu, signed out.
            await OpenFolderAsync(app, "Remotes", "origin", N.TopicFolder);
            await MenuOfAsync(app, "Remotes", "origin", N.TopicFolder, N.NoRequest);
            AssertWholeMenu(app, N.RemoteBranchMenuOf(GitLab), $"Check out {N.RemoteHead}", N.RemoteBranchMenuHeight);
            Assert.True(app.Menu().Items.Single(item => item.Label == N.GitLabCreateItem).IsEnabled);
            AssertNoRequests(served, mark);

            // N8 step 3: the sign-in first, then Cancel.
            app.ClickMenuItem(N.GitLabCreateItem);
            var signIn = await app.WaitForDialogAsync<SignInDialogViewModel>();
            await app.WaitForAsync(() => app.FocusedAutomationId() == "SignInToken", "the focus in SignInToken");
            Assert.Equal(string.Empty, ((TextBox)app.FindByAutomationId("SignInToken")).Text ?? string.Empty);
            AssertSignInFirst(app, signIn, GitLab, string.Empty);
            Assert.Same(signIn, app.Session.Services.DialogHost.Current);
            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, open => open is CreatePullRequestDialogViewModel);
            Assert.Equal([signIn], app.Session.Services.DialogHost.Open.ToList());
            AssertNoRequests(served, mark);
            ShotN(app, N.N08a);
            app.Click(app.FindByAutomationId("CancelButton"));
            await app.WaitForAsync(() => signIn.IsClosed, "the sign-in dialog closed");
            app.Settle();
            Assert.Null(app.Session.Services.DialogHost.Current);
            Assert.Equal(PullRequestsState.SignedOut, webApp.HostingState.PullRequests);
            AssertNoRequests(served, mark);
            Assert.Equal(["probe"], StoreCallsSince(app, stored));
            Assert.Empty(OperationsSince(app, operations));
            Assert.Equal(section, Section(app));

            // N8 step 4: a sign-in goes on to the remote branch's form (N1's n01a).
            mark = Mark(served);
            var logMark = app.LogMark();
            await MenuOfAsync(app, "Remotes", "origin", N.TopicFolder, N.NoRequest);
            app.ClickMenuItem(N.GitLabCreateItem);
            await app.WaitForDialogAsync<SignInDialogViewModel>();
            app.TypeToken(served.GitLabToken);
            app.PressKey(Key.Enter);
            var form = await WaitForFormAsync(app);
            await WaitForListedAsync(app, GitLab, 3, logMark);
            await AssertNewFormAsync(app, form);
            AssertForm(app, form, GitLab, N.RemoteHead, N.GitLabFullName, null, N.Base, N.GitLabFullName, N.HeadTitle);
            AssertFormPlaces(app, N.CardLeft);
            AssertSignInRequests(served, GitLab, RequestsSince(served, GitLab, mark));
            Assert.Equal(["probe", KeyOf(served, GitLab)], StoreCallsSince(app, stored));
            Assert.Equal([$"v {P.GitLabTitle} 3", .. P.GitLabRows.Select(row => row.Name)], Section(app));
            ShotN(app, N.N01a);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => form.IsClosed, "the form closed");

            // N1 step 5: local-work's menu and form, then the drop's menu.
            mark = Mark(served);
            await MenuOfAsync(app, "Local branches", N.LocalWork);
            AssertWholeMenu(app, N.LocalWorkMenuOf(GitLab), $"Check out {N.LocalWork}", N.LocalWorkMenuHeight);
            form = await OpenFormFromMenuAsync(app, GitLab);
            await AssertNewFormAsync(app, form);
            AssertForm(app, form, GitLab, N.LocalWork, N.GitLabFullName, N.PushedFirstWithUpstreamOf(GitLab), N.Base, N.GitLabFullName, N.LocalWorkTitle);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => form.IsClosed, "the form closed");
            Assert.Empty(OperationsSince(app, operations));
            await DropLocalWorkOnOriginMainAsync(app);
            AssertWholeMenu(app, N.DropMenuOf(GitLab), $"Push {N.LocalWork} to {N.Base}", N.DropMenuHeight);
            ShotN(app, N.N01b);
            AssertNoRequests(served, mark);

            // N4 step 6: the drop's form, pushed first, its base given (never Reading GitLab…). On
            // GitLab, with A3's answer kept from the list's read, a form not given its base reads A3
            // inside its constructor, so it too is (false, origin/main) when added: this recorder
            // holds but cannot tell a given base from a dropped one; method 4's can (open point 45).
            using (var bases = new BaseRecorder(app))
            {
                form = await OpenFormFromMenuAsync(app, GitLab);
                bases.AssertGivenFromTheStart(GitLab);
            }

            await AssertNewFormAsync(app, form);
            AssertForm(app, form, GitLab, N.LocalWork, N.GitLabFullName, N.PushedFirstWithUpstreamOf(GitLab), N.Base, N.GitLabFullName, N.LocalWorkTitle);
            var headNote = app.FindByAutomationId("PullRequestHeadNote");
            Assert.Contains("settingsNote", headNote.Classes);
            Assert.True(app.BoundsOf(headNote).Top >= app.BoundsOf(app.FindByAutomationId("PullRequestHead")).Bottom - 0.5, "The note is not under the head.");
            var heightWithNote = AssertFormPlaces(app, N.CardLeft);
            AssertFormBrushes(app, light: false);
            Assert.True(app.FindByAutomationId("ConfirmButton").IsEffectivelyEnabled);
            AssertNoRequests(served, mark);
            ShotN(app, N.N04a);

            // N3 step 7: Draft, its sent title, and a description.
            app.Click(app.FindByAutomationId("PullRequestDraft"));
            app.TypeInto("PullRequestDescription", N.Description);
            app.Settle();
            Assert.Equal((true, N.Description, N.LocalWorkTitle), (form.IsDraft, form.Description, form.PullRequestTitle));
            AssertSentTitle(app, N.LocalWorkSentTitle);
            Assert.Equal(N.LocalWorkTitle, ((TextBox)app.FindByAutomationId("PullRequestTitle")).Text);
            var sentHeight = app.BoundsOf(app.FindByAutomationId("PullRequestSentTitle")).Height;
            Assert.Equal(heightWithNote + sentHeight + N.LineGap, AssertFormPlaces(app, N.CardLeft), 1.0);
            AssertFormBrushes(app, light: false);
            AssertNoRequests(served, mark);
            ShotN(app, N.N03a);

            // N4 step 8: Create; the push first, then the POST held.
            mark = Mark(served);
            var gitRequests = served.GitLab.GitRequests.Count;
            logMark = app.LogMark();
            var createOperations = OperationMark(app);
            var calls = await app.QuietCallMarkAsync();
            using var dialogs = new DialogRecorder(app);
            var hold = served.GitLab.HoldNext(N.GitLabCreateRoute);
            try
            {
                app.Click(app.FindByAutomationId("ConfirmButton"));
                await app.WaitForAsync(() => hold.Arrived.IsCompleted, "the POST held");
                await app.WaitForAsync(() => webApp.OperationStatus.Text == N.GitLabCreating, "the POST's running text");
                await app.WaitForWatchersQuietAsync();
                AssertCreating(app, form, cancelEnabled: false);
                Assert.True(webApp.OperationStatus.IsIndeterminate);
                Assert.False(app.FindByAutomationId("OperationCancelButton").IsVisible);
                AssertCalledInOrder(app, calls, @"git config --null --get-regexp ^remote\.", "git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work");
                Assert.Single(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
                AssertPushedThroughHelper(app, served, GitLab, gitRequests);
                Assert.Equal(await GitAsync(served.WebApp, "rev-parse", N.LocalWorkRef), (await GitInAsync(Bare(served, GitLab), "rev-parse", N.LocalWorkRef)).Output);
                var held = RequestsSince(served, GitLab, mark);
                Assert.Equal([$"GET {N.GitLabSourceRoute} 304"], held.Select(Describe));
                Assert.Equal(N.GitLabSourcePath, held[0].Path);
                AssertSentAsAda(served, GitLab, held);
                Assert.Null(webApp.Refs!.Refs.FirstOrDefault(gitRef => gitRef.FullName == "refs/remotes/origin/local-work"));
                Assert.Null(webApp.Refs.Refs.Single(gitRef => gitRef.FullName == N.LocalWorkRef).Upstream);
                ShotN(app, N.N04b);
                app.PressKey(Key.Escape);
                app.Settle();
                Assert.False(form.IsClosed);
            }
            finally
            {
                // N3 step 9: the hold released.
                hold.Dispose();
            }

            await WaitForEndAsync(app, GitLab, N.LocalWork, logMark, OperationOutcome.Succeeded);
            await WaitForListedAsync(app, GitLab, N.CountAfterCreate, logMark, times: 2);
            ShotN(app, N.N03b);
            var created = N.CreatedOf(GitLab, N.GitLabNext, draft: true);
            var records = OperationsSince(app, createOperations);
            Assert.Equal([(N.OperationOf(GitLab, N.LocalWork).Title, OperationOutcome.Succeeded, created)], records.Select(record => (record.Title, record.Outcome, record.Message)));
            Assert.Equal([(N.OperationOf(GitLab, N.LocalWork).Title, OperationOutcome.Succeeded)], OperationsSince(app, operations).Select(record => (record.Title, record.Outcome)));
            Assert.Contains(app.GitCalls(), call => call.OperationId == records[0].Id && call.CommandText == "git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work");
            Assert.Empty(dialogs.Opened);
            AssertStatus(app, created);
            AssertOfferShown(app, webApp);
            var requests = RequestsSince(served, GitLab, mark);
            Assert.Equal([$"GET {N.GitLabSourceRoute} 304", $"POST {N.GitLabCreateRoute} 201", $"GET {N.GitLabListRoute} 200", $"GET {N.GitLabListRoute} 304"], requests.Select(Describe));
            AssertSentAsAda(served, GitLab, requests);
            AssertBody(PostSince(served, GitLab, mark), BodyFor(GitLab, N.LocalWork, N.LocalWorkTitle, N.Description, draft: true), ["draft", "target_project_id", "allow_collaboration"]);
            var project = served.GitLab.Project(WebAppPath);
            var request = Assert.Single(served.GitLab.MergeRequests(project), candidate => candidate.Iid == N.GitLabNext);
            Assert.Equal(("opened", N.LocalWorkDraftTitle, true, AdaLogin, N.LocalWork, "main", N.Description), (request.State, request.Title, request.Draft, request.Author, request.SourceBranch, request.TargetBranch, request.Description));
            Assert.Same(project, request.Source);
            Assert.Same(project, request.Target);
            Assert.Equal([N.GitLabNext, 23, 22, 21], webApp.HostingState.List!.PullRequests.Select(pull => pull.Number));
            Assert.Equal((N.GitLabNext, N.GitLabNext), (webApp.HostingState.ByRef[N.LocalWorkRef].Number, webApp.HostingState.ByRef["refs/remotes/origin/local-work"].Number));
            AssertSectionAfterCreate(app, GitLab, N.GitLabNext, N.LocalWorkDraftTitle, AdaLogin, N.LocalWork, draft: true);
            await AssertBranchNumberAsync(app, GitLab, N.GitLabNext, N.LocalWorkDraftTitle, "Local branches", N.LocalWork);
            await app.RefRowAsync("Remotes", "origin", N.LocalWork);
            AssertNewBadge(app, GitLab, 0, N.LocalWork, N.GitLabNext, N.LocalWorkDraftTitle, draft: true);
            AssertOtherBadges(app, GitLab, 0);
            AssertInfoOnce(app, logMark, N.IntoLineOf(GitLab, N.LocalWork, true, "origin/local-work"));
            Assert.DoesNotContain(app.LogLines(), line => line.Contains(N.Description, StringComparison.Ordinal));
            tip = await GitAsync(served.WebApp, "rev-parse", N.LocalWorkRef);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [GitLabToken], [], keptOut: [N.Description, N.LocalWorkTitle, N.HeadTitle]);

        // N4 step 10: the app closed.
        await AssertPushedFirstAsync(served, GitLab, tip);
        Assert.Equal(tip, (await GitInAsync(Bare(served, GitLab), "rev-parse", "refs/merge-requests/24/head")).Output);
    }

    [AvaloniaFact]
    public async Task Check_N8_N1_N4_N3_azure_signed_out_drop_pushed_first_and_draft()
    {
        await using var served = await ServeAsync();
        await PrepareRequestsAsync(served.MobileApp, Azure);
        using var data = new TempDirectory("phase7b n8 azure");
        var app = await OpenAsync(data, served, signIn: []);
        string tip;
        try
        {
            var mobileApp = app.Session.Shell.ActiveTab.Repository!;

            // N8 step 1: signed out.
            await WaitForStateAsync(app, mobileApp, PullRequestsState.SignedOut, PullRequestMessageKind.SignIn);
            AssertSignedOut(app, served, mobileApp, Azure);
            Assert.Equal(1, (await served.MobileApp.GitAllowingFailureAsync(["config", "--get-regexp", @"^branch\.local-work\."])).ExitCode);
            Assert.Equal(1, (await GitInAsync(Bare(served, Azure), "rev-parse", "--verify", "--quiet", N.LocalWorkRef)).Exit);
            var stored = app.Tokens.Calls.Count;
            var operations = OperationMark(app);

            // N1 step 2: the three menus, signed out.
            await OpenFolderAsync(app, "Remotes", "origin", N.TopicFolder);
            await MenuOfAsync(app, "Remotes", "origin", N.TopicFolder, N.NoRequest);
            AssertWholeMenu(app, N.RemoteBranchMenuOf(Azure), $"Check out {N.RemoteHead}", N.RemoteBranchMenuHeight);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            await MenuOfAsync(app, "Local branches", N.LocalWork);
            AssertWholeMenu(app, N.LocalWorkMenuOf(Azure), $"Check out {N.LocalWork}", N.LocalWorkMenuHeight);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, null);
            await DropLocalWorkOnOriginMainAsync(app);
            AssertWholeMenu(app, N.DropMenuOf(Azure), $"Push {N.LocalWork} to {N.Base}", N.DropMenuHeight);
            AssertNoRequests(served, default);

            // N8 step 3: the drop's sign-in first, prefilled; the sign-in goes on to the drop's form.
            app.ClickMenuItem(N.AzureDevOpsCreateItem);
            var signIn = await app.WaitForDialogAsync<SignInDialogViewModel>();
            await app.WaitForAsync(() => app.FocusedAutomationId() == "SignInToken", "the focus in SignInToken");
            Assert.Equal(Organization, signIn.Organization);
            Assert.Equal(Organization, ((TextBox)app.FindByAutomationId("SignInOrganization")).Text);
            Assert.False(app.FindByAutomationId(app.TopDialogView!, "ConfirmButton").IsEffectivelyEnabled);
            AssertSignInFirst(app, signIn, Azure, Organization);
            Assert.DoesNotContain(app.Session.Services.DialogHost.Open, open => open is CreatePullRequestDialogViewModel);
            Assert.Equal([signIn], app.Session.Services.DialogHost.Open.ToList());
            AssertNoRequests(served, default);
            ShotN(app, N.N08b);
            var mark = Mark(served);
            var logMark = app.LogMark();
            app.TypeToken(served.AzureDevOpsToken);
            CreatePullRequestDialogViewModel form;

            // The drop's base given across the sign-in (open point 45): from the form's first state
            // on, Into reads origin/main, never Reading Azure DevOps… (A3 is not kept before the
            // sign-in's list, so a form not given its base would show that first).
            using (var bases = new BaseRecorder(app))
            {
                app.PressKey(Key.Enter);
                form = await WaitForFormAsync(app);
                bases.AssertGivenFromTheStart(Azure);
            }

            await WaitForListedAsync(app, Azure, 3, logMark);
            AssertSignInRequests(served, Azure, RequestsSince(served, Azure, mark));
            Assert.Equal(["probe", KeyOf(served, Azure)], StoreCallsSince(app, stored));

            // N4 step 4: the drop's form after the sign-in, its base given (open point 45).
            mark = Mark(served);
            await AssertNewFormAsync(app, form);
            AssertForm(app, form, Azure, N.LocalWork, N.AzureDevOpsFullName, N.PushedFirstWithUpstreamOf(Azure), N.Base, N.AzureDevOpsFullName, N.LocalWorkTitle);
            var height = AssertFormPlaces(app, N.CardLeft);
            AssertFormBrushes(app, light: false);
            Assert.True(app.FindByAutomationId("ConfirmButton").IsEffectivelyEnabled);
            ShotN(app, N.N04c);

            // N3 step 5: Draft and a description; neither GitLab line.
            app.Click(app.FindByAutomationId("PullRequestDraft"));
            app.TypeInto("PullRequestDescription", N.Description);
            app.Settle();
            Assert.Equal((true, N.Description), (form.IsDraft, form.Description));
            Assert.False(Shown(app, "PullRequestSentTitle"));
            Assert.False(Shown(app, "PullRequestQuickActionsNote"));
            Assert.Equal(height, AssertFormPlaces(app, N.CardLeft), 1.0);
            AssertNoRequests(served, mark);

            // N4 step 6: Create, pushed first.
            mark = Mark(served);
            var gitRequests = served.AzureDevOps.GitRequests.Count;
            logMark = app.LogMark();
            var createOperations = OperationMark(app);
            var calls = await app.QuietCallMarkAsync();
            using (var dialogs = new DialogRecorder(app))
            {
                app.Click(app.FindByAutomationId("ConfirmButton"));
                await WaitForEndAsync(app, Azure, N.LocalWork, logMark, OperationOutcome.Succeeded);
                await WaitForListedAsync(app, Azure, N.CountAfterCreate, logMark, times: 2);
                ShotN(app, N.N03c);
                Assert.Empty(dialogs.Opened);
            }

            var created = N.CreatedOf(Azure, N.AzureDevOpsNext, draft: true);
            var records = OperationsSince(app, createOperations);
            Assert.Equal([(N.OperationOf(Azure, N.LocalWork).Title, OperationOutcome.Succeeded, created)], records.Select(record => (record.Title, record.Outcome, record.Message)));
            Assert.Single(OperationsSince(app, operations));
            Assert.Contains(app.GitCalls(), call => call.OperationId == records[0].Id && call.CommandText == "git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work");
            AssertCalledInOrder(app, calls, @"git config --null --get-regexp ^remote\.", "git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work");
            Assert.Single(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
            AssertPushedThroughHelper(app, served, Azure, gitRequests);
            AssertStatus(app, created);
            AssertOfferShown(app, mobileApp);
            var requests = RequestsSince(served, Azure, mark);
            Assert.Equal([$"POST {N.AzureDevOpsCreateRoute} 201", $"GET {N.AzureDevOpsListRoute} 200", $"GET {N.AzureDevOpsListRoute} 200"], requests.Select(Describe));
            AssertSentAsAda(served, Azure, requests);
            AssertBody(PostSince(served, Azure, mark), BodyFor(Azure, N.LocalWork, N.LocalWorkTitle, N.Description, draft: true), ["forkSource"]);
            var request = Assert.Single(served.AzureDevOps.PullRequests(served.AzureDevOps.Repository(Organization, Project, MobileApp)), candidate => candidate.Id == N.AzureDevOpsNext);
            Assert.Equal((FakeAzurePullRequestStatus.Active, N.LocalWorkTitle, true, N.LocalWork, "main", N.Description), (request.Status, request.Title, request.IsDraft, request.SourceBranch, request.TargetBranch, request.Description));
            Assert.Same(served.AzureDevOps.User(Organization, AdaName), request.Creator);
            Assert.Null(request.Fork);
            AssertSectionAfterCreate(app, Azure, N.AzureDevOpsNext, N.LocalWorkTitle, AdaName, N.LocalWork, draft: true);
            await app.RefRowAsync("Remotes", "origin", N.LocalWork);
            await AssertBranchNumberAsync(app, Azure, N.AzureDevOpsNext, N.LocalWorkTitle, "Local branches", N.LocalWork);
            AssertNewBadge(app, Azure, 0, N.LocalWork, N.AzureDevOpsNext, N.LocalWorkTitle, draft: true);
            AssertOtherBadges(app, Azure, 0);
            AssertInfoOnce(app, logMark, N.IntoLineOf(Azure, N.LocalWork, true, "origin/local-work"));
            tip = await GitAsync(served.MobileApp, "rev-parse", N.LocalWorkRef);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [AzureDevOpsToken], [], keptOut: [N.Description, N.LocalWorkTitle]);

        // N4 step 7: the app closed.
        await AssertPushedFirstAsync(served, Azure, tip);
        Assert.Equal(0, (await GitInAsync(Bare(served, Azure), "rev-parse", "--verify", "--quiet", "refs/pull/14/merge")).Exit);
        Assert.Equal(1, (await GitInAsync(Bare(served, Azure), "rev-parse", "--verify", "--quiet", "refs/pull/14/head")).Exit);
    }

    [AvaloniaFact]
    public async Task Check_N5_gitlab_missing_branch_and_already_open()
    {
        await using var served = await ServeAsync();
        await PrepareRequestsAsync(served.MobileApp, GitLab);
        using var data = new TempDirectory("phase7b n5 gitlab");
        var app = await OpenAsync(data, served, WebApp);
        try
        {
            await MissingBranchAndAlreadyOpenAsync(app, served, GitLab);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [GitLabToken], [N.WebUrlOf(GitLab, N.GitLabNext)], refusedPost: true, keptOut: [N.HeadTitle]);
    }

    [AvaloniaFact]
    public async Task Check_N5_N7_azure_missing_branch_already_open_and_long_names()
    {
        await using var served = await ServeAsync();
        await PrepareRequestsAsync(served.MobileApp, Azure);
        using var data = new TempDirectory("phase7b n5 azure");
        var app = await OpenAsync(data, served);
        try
        {
            var mobileApp = app.Session.Shell.ActiveTab.Repository!;
            await MissingBranchAndAlreadyOpenAsync(app, served, Azure);

            // N7 step 5: the remote long and its two refs.
            var operations = OperationMark(app);
            Assert.Null(app.Session.Services.DialogHost.Current);
            var origin = await GitAsync(served.MobileApp, "rev-parse", N.BaseRef);
            var topic = await GitAsync(served.MobileApp, "rev-parse", N.RemoteHeadRef);
            await served.MobileApp.GitAsync(["remote", "add", N.LongRemote, WithPorts(N.LongUrl, served)]);
            await served.MobileApp.GitAsync(["update-ref", "refs/remotes/long/main", origin]);
            await served.MobileApp.GitAsync(["update-ref", "refs/remotes/long/topic/no-request", topic]);
            await app.WaitForAsync(() => mobileApp.LeftPanel.Rows.OfType<RefItemRow>().Any(row => row.Text == N.LongRemote) && mobileApp.HostingState.Remotes.ContainsKey(N.LongRemote), "the remote long");
            await app.WaitForWatchersQuietAsync();
            var state = mobileApp.HostingState;
            var mobile = state.Remotes["origin"];
            Assert.Equal(new HostedRepository(Azure, mobile.Host, $"{Organization}/{Project}", LongRepository), state.Remotes[N.LongRemote]);
            Assert.Equal([N.LongRemote, "origin"], state.RequestRemotes.Keys.Order(StringComparer.Ordinal));
            Assert.Equal(mobile, state.Repository);
            Assert.Equal($"v {P.AzureDevOpsTitle} 4", Section(app)[0]);
            var mark = Mark(served);

            // N7 step 6: long/topic/no-request's form, then long/main from the Pick.
            await OpenFolderAsync(app, "Remotes", N.LongRemote);
            await OpenFolderAsync(app, "Remotes", N.LongRemote, N.TopicFolder);
            await MenuOfAsync(app, "Remotes", N.LongRemote, N.TopicFolder, N.NoRequest);
            AssertWholeMenu(app, N.RemoteBranchMenuOf(Azure, N.LongRemote), $"Check out {N.LongHead}", N.RemoteBranchMenuHeight);
            Assert.True(app.Menu().Items.Single(item => item.Label == N.AzureDevOpsCreateItem).IsEnabled);
            var form = await OpenFormFromMenuAsync(app, Azure);
            AssertForm(app, form, Azure, N.LongHead, N.LongFullName, null, N.Base, N.AzureDevOpsFullName, N.HeadTitle);
            Assert.True(IsCut((TextBlock)app.FindByAutomationId("PullRequestHeadRepository")), "The long head repository is not cut.");
            Assert.Equal(N.ColumnRight, app.BoundsOf(app.FindByAutomationId("PullRequestHeadRepository")).Right, 1.0);
            AssertFormPlaces(app, N.CardLeft);
            app.Click(app.FindByAutomationId("PullRequestBaseChooseButton"));
            var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
            Assert.Equal(N.ChooseBaseTitle, pick.Title);
            string[] options = ["long/main", N.LongHead, "origin/docs/guide", "origin/feature/sign-in", N.Base, N.RemoteHead];
            Assert.Equal(options, pick.Options.Select(option => option.Text));
            Assert.Equal(options.Select(option => option.StartsWith("long/", StringComparison.Ordinal) ? N.LongFullName : N.AzureDevOpsFullName), pick.Options.Select(option => option.Detail));
            Assert.Equal(N.Base, pick.Selected);
            await app.PickAsync("long/main");
            await app.WaitForAsync(() => form.BaseText == "long/main", "the chosen base");
            app.Settle();
            AssertForm(app, form, Azure, N.LongHead, N.LongFullName, null, "long/main", N.LongFullName, N.HeadTitle);
            var baseRepository = (TextBlock)app.FindByAutomationId("PullRequestBaseRepository");
            Assert.True(IsCut(baseRepository) && baseRepository.TextLayout.TextLines.Count == 1, "The long base repository is not cut on one line.");
            Assert.Equal(N.ColumnRight, app.BoundsOf(app.FindByAutomationId("PullRequestBaseChooseButton")).Right, 1.0);
            Assert.Equal(N.CardHeight, AssertFormPlaces(app, N.CardLeft), 1.0);
            Assert.True(app.FindByAutomationId("ConfirmButton").IsEffectivelyEnabled);
            ShotN(app, N.N07a);
            AssertNoRequests(served, mark);

            // N7 step 7: the tooltips.
            Assert.Equal(N.LongFullName, await app.RestPointerOnAsync(app.FindByAutomationId("PullRequestHeadRepository")));
            ShotN(app, N.N07b, keepPointer: true);
            Assert.Equal(N.LongFullName, await app.RestPointerOnAsync(baseRepository));

            // N7 step 8: Esc.
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => form.IsClosed, "the form closed");
            Assert.Null(app.Session.Services.DialogHost.Current);
            Assert.Empty(OperationsSince(app, operations));
            AssertNoRequests(served, mark);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [AzureDevOpsToken], [N.WebUrlOf(Azure, N.AzureDevOpsNext)], refusedPost: true, keptOut: [N.HeadTitle]);
    }

    /// <summary>Methods 5 and 6, N5's steps 1 to 4: the missing branch, then "already open" and its button.</summary>
    private static async Task MissingBranchAndAlreadyOpenAsync(ShellDriver app, ServedHosting7b served, HostingProvider p)
    {
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var number = N.NextOf(p);
        var shown = N.Of(p);

        // Step 1: the head's branch deleted on the fake behind the app.
        await WaitForListedAsync(app, p, 3, 0);
        var bare = Bare(served, p);
        var sha = (await GitInAsync(bare, "rev-parse", N.HeadRef)).Output;
        Assert.Equal(0, (await GitInAsync(bare, "update-ref", "-d", N.HeadRef)).Exit);
        Assert.Equal($"v {P.TitleOf(p)} 3", Section(app)[0]);
        Assert.Equal(1, (await GitInAsync(bare, "rev-parse", "--verify", "--quiet", N.HeadRef)).Exit);
        Assert.Equal(sha, await GitAsync(Clone(served, p), "rev-parse", N.RemoteHeadRef));

        // Step 2: Create refused, the branch missing (step 1's no-request without !n, read once its folder is open).
        var opened = Mark(served);
        app.Click(await app.RefRowAsync("Local branches", N.TopicFolder));
        await AssertBranchNumberAsync(app, p, null, null, "Local branches", N.TopicFolder, N.NoRequest);
        await MenuOfAsync(app, "Local branches", N.TopicFolder, N.NoRequest);
        AssertWholeMenu(app, N.LocalBranchMenuOf(p), $"Check out {N.Head}", N.LocalBranchMenuHeight);
        var form = await OpenFormFromMenuAsync(app, p);
        await AssertN2FormAsync(app, form, p, N.CardLeft);
        AssertNoRequests(served, opened);
        var mark = Mark(served);
        var logMark = app.LogMark();
        var operations = OperationMark(app);
        var calls = await app.QuietCallMarkAsync();
        using var dialogs = new DialogRecorder(app);
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await WaitForEndAsync(app, p, N.Head, logMark, OperationOutcome.Failed);
        await app.WaitForAsync(() => !form.IsCreating, "the form idle again");
        await app.WaitForWatchersQuietAsync();
        ShotN(app, p == GitLab ? N.N05a : N.N05c);
        var failed = N.OperationOf(p, N.Head).Title;
        Assert.Equal([(failed, OperationOutcome.Failed)], OperationsSince(app, operations).Select(record => (record.Title, record.Outcome)));
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
        var requests = RequestsSince(served, p, mark);
        Assert.Equal(p == GitLab ? [$"GET {N.GitLabSourceRoute} 304", $"POST {N.GitLabCreateRoute} 400"] : [$"POST {N.AzureDevOpsCreateRoute} 400"], requests.Select(Describe));
        AssertSentAsAda(served, p, requests);
        AssertBody(PostSince(served, p, mark), shown.Body, shown.AbsentMembers);
        var missing = p == GitLab ? N.GitLabHeadUnknown : N.AzureDevOpsBranchesUnknown;
        AssertForm(app, form, p, N.Head, N.FullNameOf(p), null, N.Base, N.FullNameOf(p), N.HeadTitle, missing);
        AssertIdle(app, form);
        Assert.Same(form, app.Session.Services.DialogHost.Current);
        Assert.Empty(dialogs.Opened);
        AssertStatus(app, N.FailedOf(failed), OperationStatusTone.Danger);
        Assert.False(repository.OperationStatus.HasOffer);
        Assert.Equal($"v {P.TitleOf(p)} 3", Section(app)[0]);
        var message = N.MissingMessageOf(p);
        AssertWarningOnce(app, logMark, N.CoreWarningOf(p, 400));
        AssertWarningOnce(app, logMark, N.SessionWarningOf(p, message));
        AssertWarningOnce(app, logMark, $"{N.RepositoryOf(p)}: {failed}: the request to create it failed: Validation, {message} (request {{id}})");
        if (p == GitLab)
        {
            Assert.Equal([21, 22, 23], served.GitLab.MergeRequests(served.GitLab.Project(WebAppPath)).Select(request => request.Iid).Order());
        }

        // Step 3: the branch back, a request from it opened on the fake: already open.
        Assert.Equal(0, (await GitInAsync(bare, "update-ref", N.HeadRef, sha)).Exit);
        if (p == GitLab)
        {
            served.GitLab.AddMergeRequest(served.GitLab.Project(WebAppPath), N.HeadTitle, OctoLogin, N.Head, "main");
        }
        else
        {
            served.AzureDevOps.AddPullRequest(served.AzureDevOps.Repository(Organization, Project, MobileApp), N.HeadTitle, served.AzureDevOps.User(Organization, OctoName), N.Head, "main");
        }

        mark = Mark(served);
        logMark = app.LogMark();
        calls = await app.QuietCallMarkAsync();
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await WaitForEndAsync(app, p, N.Head, logMark, OperationOutcome.Failed);
        await WaitForListedAsync(app, p, N.CountAfterCreate, logMark);
        await app.WaitForAsync(() => !form.IsCreating, "the form idle again");
        await app.WaitForWatchersQuietAsync();
        ShotN(app, p == GitLab ? N.N05b : N.N05d);
        Assert.Equal([(failed, OperationOutcome.Failed), (failed, OperationOutcome.Failed)], OperationsSince(app, operations).Select(record => (record.Title, record.Outcome)));
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
        requests = RequestsSince(served, p, mark);
        Assert.Equal(
            p == GitLab
                ? [$"GET {N.GitLabSourceRoute} 304", $"POST {N.GitLabCreateRoute} 409", $"GET {N.GitLabListRoute} 200"]
                : [$"POST {N.AzureDevOpsCreateRoute} 409", $"GET {N.AzureDevOpsListRoute} 200"],
            requests.Select(Describe));
        AssertSentAsAda(served, p, requests);
        AssertBody(PostSince(served, p, mark), shown.Body, shown.AbsentMembers);
        AssertForm(app, form, p, N.Head, N.FullNameOf(p), null, N.Base, N.FullNameOf(p), N.HeadTitle, N.AlreadyOpenOf(p, number), number);
        AssertIdle(app, form);
        AssertStatus(app, N.FailedOf(failed), OperationStatusTone.Danger);
        var octo = p == GitLab ? OctoLogin : OctoName;
        Assert.Equal($"v {P.TitleOf(p)} 4", Section(app)[0]);
        Assert.Equal(($"{N.NumberOf(number)} {N.HeadTitle}", octo), (PullRequestRows(app)[0].Text, PullRequestRows(app)[0].Author));
        await AssertBranchNumberAsync(app, p, number, N.HeadTitle, "Local branches", N.TopicFolder, N.NoRequest);
        Assert.Equal(1, ListedSince(app, p, logMark));

        // The 409's message, which the fake's record does not hold: GitLab's, and Azure DevOps'
        // TF401179 of the typeKey GitPullRequestExistsException, through the core's and the session's Warnings.
        AssertWarningOnce(app, logMark, N.CoreWarningOf(p, 409));
        AssertWarningOnce(app, logMark, N.SessionWarningOf(p, N.AlreadyOpenMessageOf(p)));
        if (p == GitLab)
        {
            var open = served.GitLab.MergeRequests(served.GitLab.Project(WebAppPath));
            Assert.Equal([21, 22, 23, 24], open.Select(request => request.Iid).Order());
            Assert.Equal(OctoLogin, open.Single(request => request.Iid == 24).Author);
        }

        // Step 4: Open … !n opens it.
        mark = Mark(served);
        var status = repository.OperationStatus.Text;
        app.Click(app.FindByAutomationId("PullRequestOpenExisting"));
        await app.WaitForAsync(() => app.Launcher.Requests.Count > 0, "the launcher's request");
        await app.WaitForAsync(() => form.IsClosed, "the form closed");
        Assert.Equal([$"{FileLaunchKind.OpenUrl} {WithPorts(N.WebUrlOf(p, number), served)}"], app.Launcher.Requests.Select(request => $"{request.Kind} {request.FullPath}"));
        AssertNoRequests(served, mark);
        Assert.Equal(status, repository.OperationStatus.Text);
        Assert.Empty(dialogs.Opened);
    }

    [AvaloniaFact]
    public async Task Check_N9_gitlab_form_at_1920x1080_and_light()
    {
        await using var served = await ServeAsync();
        await PrepareRequestsAsync(served.MobileApp, GitLab);
        using var data = new TempDirectory("phase7b n9");
        var app = await OpenAsync(data, served, WebApp, width: 1920, height: 1080);
        try
        {
            await WaitForListedAsync(app, GitLab, 3, 0);
            var mark = Mark(served);
            var rest = new Point(N.RestPoint1920.X, N.RestPoint1920.Y);

            // Step 2: dark.
            app.Click(await app.RefRowAsync("Local branches", N.TopicFolder));
            await MenuOfAsync(app, "Local branches", N.TopicFolder, N.NoRequest);
            var form = await OpenFormFromMenuAsync(app, GitLab);
            await AssertN2FormAsync(app, form, GitLab, N.CardLeft1920);
            app.MoveMouse(rest);
            ShotN(app, N.N09a, keepPointer: true);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => form.IsClosed, "the form closed");
            AssertNoRequests(served, mark);

            // Step 3: light.
            app.Click(app.FindByAutomationId("ThemeSwitch"));
            await MenuOfAsync(app, "Local branches", N.TopicFolder, N.NoRequest);
            form = await OpenFormFromMenuAsync(app, GitLab);
            await AssertN2FormAsync(app, form, GitLab, N.CardLeft1920, light: true);
            Assert.Same(app.Window.FindResource("VcPrimaryButton"), ((Button)app.FindByAutomationId("ConfirmButton")).Theme);
            app.MoveMouse(rest);
            ShotN(app, N.N09b, keepPointer: true);
            app.PressKey(Key.Escape);
            await app.WaitForAsync(() => form.IsClosed, "the form closed");
            AssertNoRequests(served, mark);
        }
        finally
        {
            app.Dispose();
        }

        End(app, data, served, [GitLabToken], [], keptOut: [N.HeadTitle]);
    }

    // ---- Method helpers --------------------------------------------------------------------------

    /// <summary>
    /// N2's form as the menu opens it (part N's conventions, "The form"): 7a's new form, N2's texts,
    /// Draft unticked, part N's places; GitLab's quick-actions note 4 under the description at x 340
    /// (its card's left plus 20) on one line, Azure DevOps' card 386 high; the theme's brushes.
    /// </summary>
    private static async Task AssertN2FormAsync(ShellDriver app, CreatePullRequestDialogViewModel form, HostingProvider p, double cardLeft, bool light = false)
    {
        await AssertNewFormAsync(app, form);
        AssertForm(app, form, p, N.Head, N.FullNameOf(p), null, N.Base, N.FullNameOf(p), N.HeadTitle);
        Assert.False(form.IsDraft);
        var height = AssertFormPlaces(app, cardLeft);
        if (p == GitLab)
        {
            var note = app.FindByAutomationId("PullRequestQuickActionsNote");
            Assert.Equal(app.BoundsOf(app.FindByAutomationId("PullRequestDescription")).Bottom + N.LineGap, app.BoundsOf(note).Top, 1.0);
            Assert.Equal(cardLeft + (340 - N.CardLeft), app.BoundsOf(note).X, 1.0);
            Assert.Single(((TextBlock)note).TextLayout.TextLines);
        }
        else
        {
            Assert.Equal(N.CardHeight, height, 1.0);
        }

        AssertFormBrushes(app, light);
    }

    /// <summary>
    /// Theme brushes only (part N's conventions): the card <c>#1A1D24</c> (light <c>#F5F6F8</c>);
    /// From, Into, both repository texts and every shown note (the quick-actions note, the head's
    /// note, the sent title) the secondary text <c>#9BA3B0</c> (light <c>#5C6572</c>).
    /// </summary>
    private static void AssertFormBrushes(ShellDriver app, bool light)
    {
        var card = Card(app);
        Assert.Equal(Color.Parse(light ? "#F5F6F8" : "#1A1D24"), ((ISolidColorBrush)card.Background!).Color);
        string[] notes = ["PullRequestQuickActionsNote", "PullRequestHeadNote", "PullRequestSentTitle"];
        var secondary = card.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.Text is N.From or N.Into || block.Name is "PullRequestHeadRepository" or "PullRequestBaseRepository" || (notes.Contains(block.Name) && block.IsEffectivelyVisible))
            .ToList();
        Assert.Equal(4 + notes.Count(id => Shown(app, id)), secondary.Count);
        Assert.All(secondary, block => Assert.Equal(Color.Parse(light ? "#5C6572" : "#9BA3B0"), ((ISolidColorBrush)block.Foreground!).Color));
    }

    /// <summary>The status bar's offer after a create (7a's N1): <c>Open in browser</c>, by its text and name, shown 6 right of the text; no progress bar, no Cancel. Returns the offer.</summary>
    private static Button AssertOfferShown(ShellDriver app, RepositoryViewModel repository)
    {
        Assert.True(repository.OperationStatus.HasOffer);
        var offer = (Button)app.FindByAutomationId("OperationOfferButton");
        Assert.Equal((N.OpenInBrowser, N.OpenInBrowser), (repository.OperationStatus.OfferText, Avalonia.Automation.AutomationProperties.GetName(offer)));
        Assert.False(repository.OperationStatus.IsIndeterminate);
        app.Settle();
        Assert.True(offer.IsEffectivelyVisible, "OperationOfferButton is not shown.");
        Assert.Equal(app.BoundsOf(app.FindByAutomationId("OperationStatus")).Right + 6, app.BoundsOf(offer).X, 1.0);
        Assert.False(app.FindByAutomationId("OperationProgress").IsVisible);
        Assert.False(app.FindByAutomationId("OperationCancelButton").IsVisible);
        return offer;
    }

    /// <summary>N8 step 1: no request since the start (D257), signed out, the hosting's repository and <c>RequestRemotes</c>, the section's message row.</summary>
    private static void AssertSignedOut(ShellDriver app, ServedHosting7b served, RepositoryViewModel repository, HostingProvider p)
    {
        AssertNoRequests(served, default);
        var state = repository.HostingState;
        Assert.Equal(PullRequestsState.SignedOut, state.PullRequests);
        Assert.Equal((p, p == GitLab ? "northfield/platform" : $"{Organization}/{Project}", p == GitLab ? WebApp : MobileApp), (state.Repository!.Provider, state.Repository.Owner, state.Repository.Name));
        Assert.Equal(["origin"], state.RequestRemotes.Keys);
        Assert.Equal([$"v {P.TitleOf(p)} 0", "[SignIn] " + P.SignInToSeeOf(p)], Section(app));
    }

    /// <summary>After the app has closed (N4's last step): the upstream set, the branch and its remote ref one SHA, no credential and no URL changed in the clone's configuration, the branch in the bare folder.</summary>
    private static async Task AssertPushedFirstAsync(ServedHosting7b served, HostingProvider p, string tip)
    {
        var clone = Clone(served, p);
        Assert.Equal("origin", await GitAsync(clone, "config", "--get", "branch.local-work.remote"));
        Assert.Equal(N.LocalWorkRef, await GitAsync(clone, "config", "--get", "branch.local-work.merge"));
        Assert.Equal(tip, await GitAsync(clone, "rev-parse", "refs/remotes/origin/local-work"));
        Assert.Equal(N.LocalWorkTitle, await GitAsync(clone, "log", "-1", "--format=%s", N.LocalWorkRef));
        var config = (await GitAsync(clone, "config", "--local", "--list")).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.DoesNotContain(config, line => line.StartsWith("credential", StringComparison.OrdinalIgnoreCase));
        var urls = config.Where(line => line.StartsWith("remote.", StringComparison.Ordinal) && line.Contains(".url=", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            p == GitLab
                ? ["remote.origin.url=" + WithPorts(WebAppUrl, served), "remote.upstream.url=" + WithPorts(WebAppUpstreamUrl, served)]
                : ["remote.origin.url=" + WithPorts(MobileAppUrl, served)],
            urls.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(config, line => line.Contains(".pushurl=", StringComparison.Ordinal));
        Assert.Equal(tip, (await GitInAsync(Bare(served, p), "rev-parse", N.LocalWorkRef)).Output);
    }

    /// <summary>Clicks each folder of <paramref name="path"/> that is closed, top down (a click opens a folder).</summary>
    private static async Task OpenFolderAsync(ShellDriver app, params string[] path)
    {
        for (var length = 2; length <= path.Length; length++)
        {
            var prefix = path[..length];
            if (app.RefRowModel(prefix) is RefItemRow { IsContainer: true, IsOpen: false })
            {
                app.Click(await app.RefRowAsync(prefix));
                await app.WaitForAsync(() => app.RefRowModel(prefix) is RefItemRow { IsOpen: true }, $"{string.Join(" › ", prefix)} open");
            }
        }
    }

    /// <summary>A request row's control in the section, scrolled into view.</summary>
    private static async Task<Control> RequestRowAsync(ShellDriver app, int number)
    {
        Control? found = null;
        var list = app.Find<ItemsControl>("RefList");
        await app.WaitForAsync(
            () =>
            {
                var rows = app.Session.Shell.ActiveTab.LeftPanel.Rows.ToList();
                var index = rows.FindIndex(row => row is PullRequestRow pull && pull.Number == number);
                if (index < 0)
                {
                    return false;
                }

                found = list.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => ReferenceEquals(button.DataContext, rows[index]) && button.IsEffectivelyVisible && button.Bounds.Height > 0);
                if (found is null)
                {
                    list.ScrollIntoView(index);
                }

                return found is not null;
            },
            $"request {number}'s row");
        return found!;
    }
}
