using System.Text.Json;
using Avalonia.Headless.XUnit;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.CreatePullRequestTexts;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// A new merge or pull request per provider with real git (docs/phase-7b-design.md 13.3, 13.5,
/// 13.7; D334, D351, D355, D356, D362; ruling 20), the app started through <see cref="ShellDriver"/>
/// with "hosting7b"'s fakes on port 0 and signed in. GitLab: one POST to the project with the title
/// and the description as typed (a <c>/close</c> line too), <c>Draft: </c> first with the box; the
/// row, the branch's request, <c>Created draft merge request !24</c> and the offer opening its
/// <c>web_url</c>; <c>local-work</c> pushed first with <c>x-access-token</c>; a fork's head posted to
/// the fork with <c>target_project_id</c>; the 409, then <c>Open merge request !21</c>; the 400 forms;
/// a 403 with and without the scope code. Azure DevOps: <c>CreateStatus</c> 201 and 200; <c>isDraft</c>;
/// a fork's <c>forkSource.repository.id</c>; TF401179 and TF401398 in the App's words. Both: a lost
/// answer (<c>CreatedThenDropped</c>) counted as created; <c>manifest/</c> starts nothing and asks
/// nothing, also when its sign-in lands in another organization; a head in another repository whose
/// read 404s named with the base; signed out, that provider's sign-in first; the tripwire after a
/// create that pushed. On 0.4's lagged list.
/// </summary>
public sealed class CreateRequestOperationProvidersTests
{
    private const string WebAppProjectPath = "/api/v4/projects/5001/merge_requests";
    private const string MobileAppPullRequestsPath = "/Fernhill-Labs/Mobile%20App/_apis/git/repositories/mobile-app/pullrequests";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public async Task GitLab_pushes_local_work_first_and_creates_a_draft_with_the_title_and_description_as_typed()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("create providers");
        using var app = await OpenAsync(data, served, served.WebApp, HostingProvider.GitLab);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var requests = served.GitLab.Requests.Count;
        var gitRequests = served.GitLab.GitRequests.Count;
        var calls = app.CallMark();

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/local-work");
        Assert.Equal("Create merge request", form.Title);
        Assert.Equal(("local-work", "northfield/platform/web-app"), (form.Head.Text, form.Head.Repository));
        Assert.Equal("Not on GitLab yet: Create pushes it to origin/local-work first and sets that as its upstream.", form.HeadNote);
        Assert.Equal(("origin/main", "northfield/platform/web-app", "Local change"), (form.BaseText, form.BaseRepository, form.PullRequestTitle));
        form.Description = "A note for the reviewers.\n/close";
        form.IsDraft = true;
        Assert.Equal("Title on GitLab: Draft: Local change", form.SentTitle);
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();

        Assert.Contains(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal) && call.Contains("--set-upstream", StringComparison.Ordinal));
        var git = served.GitLab.GitRequests.Skip(gitRequests).ToList();
        Assert.Contains(git, request => request.GivenUserName == "x-access-token" && request.Status == 200);
        Assert.DoesNotContain(git, request => request.GivenUserName is { } user && user != "x-access-token");
        var post = Assert.Single(Posts(served.GitLab.Requests.Skip(requests)));
        Assert.Equal((WebAppProjectPath, 201), (post.Path, post.Status));
        using (var body = JsonDocument.Parse(post.Body!))
        {
            var root = body.RootElement;
            Assert.Equal(
                ["description", "source_branch", "target_branch", "title"],
                root.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
            Assert.Equal("Draft: Local change", root.GetProperty("title").GetString());
            Assert.Equal("A note for the reviewers.\n/close", root.GetProperty("description").GetString());
            Assert.Equal(("local-work", "main"), (root.GetProperty("source_branch").GetString(), root.GetProperty("target_branch").GetString()));
        }

        var created = served.GitLab.MergeRequests(served.GitLab.Project(Scenarios.Hosting7bWebApp)).Single(request => request.Iid == 24);
        Assert.Equal(("Draft: Local change", "local-work", "ada"), (created.Title, created.SourceBranch, created.Author));
        Assert.Equal("Created draft merge request !24", repository.OperationStatus.Text);
        Assert.Equal((true, Texts.OpenInBrowser), (repository.OperationStatus.HasOffer, repository.OperationStatus.OfferText));
        await app.WaitForAsync(() => repository.HostingState.List?.PullRequests.Count == 4, "four merge requests listed");
        var listed = repository.HostingState.List!.PullRequests[0];
        Assert.Equal((24, true, HostingProvider.GitLab), (listed.Number, listed.IsDraft, listed.BaseRepository.Provider));
        Assert.Equal(24, repository.HostingState.ByRef["refs/heads/local-work"].Number);

        // The row (the section's first) and the badge (the graph's local-work label), as the window shows them.
        await app.WaitForWatchersQuietAsync();
        app.Settle();
        var row = app.Session.Shell.ActiveTab.LeftPanel.Rows.OfType<PullRequestRow>().First();
        Assert.Equal(("!24 Draft: Local change", true, "ada"), (row.Text, row.IsDraft, row.Author));
        var graph = app.Find<CommitGraphControl>("GraphRows");
        var labels = Enumerable.Range(0, Math.Min(repository.Graph.Count, 50)).SelectMany(index => graph.LabelsInRow(index)).ToList();
        Assert.Equal(new PullRequestBadge(24, "Draft: Local change", true, HostingProvider.GitLab), Assert.Single(labels, label => label.FullText == "local-work").Badge);
        Assert.Equal(1, labels.Count(label => label.Badge is { Number: 24 }));
        Assert.Contains(AppLogFile.ReadLines(data.Path), line => line.EndsWith("web-app: Create merge request for local-work: into northfield/platform/web-app main, draft yes, push to origin/local-work first", StringComparison.Ordinal));
        Assert.Contains(AppLogFile.ReadLines(data.Path), line => line.EndsWith("web-app: Create merge request for local-work: created !24 in northfield/platform/web-app.", StringComparison.Ordinal));
        Assert.DoesNotContain(AppLogFile.ReadLines(data.Path), line => line.Contains("A note for the reviewers", StringComparison.Ordinal) || line.Contains("Local change", StringComparison.Ordinal));

        await repository.OperationStatus.OfferCommand.ExecuteAsync(null);
        Assert.Equal([new Uri(served.GitLab.Endpoints.WebBase, "northfield/platform/web-app/-/merge_requests/24").AbsoluteUri], OpenedUrls(app));
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task GitLab_posts_a_fork_s_head_to_the_fork_with_the_target_project_id()
    {
        await using var served = await ServeAsync();
        var forkBare = served.GitLab.Project(Scenarios.Hosting7bWebAppFork).BarePath!;
        await GitInAsync(forkBare, "branch", "fork-idea", "fix-typo");
        await served.WebApp.GitAsync("remote", "add", "fork", Scenarios.GitLabUrl(Scenarios.Hosting7bWebAppFork, served.GitLab.Port));
        await served.WebApp.GitAsync("fetch", forkBare, "+refs/heads/fork-idea:refs/remotes/fork/fork-idea");
        using var data = new TempDirectory("create providers");
        using var app = await OpenAsync(data, served, served.WebApp, HostingProvider.GitLab);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        Assert.True(repository.HostingState.RequestRemotes.ContainsKey("fork"));
        var requests = served.GitLab.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/remotes/fork/fork-idea");
        Assert.Equal(("fork/fork-idea", "ada/web-app"), (form.Head.Text, form.Head.Repository));
        Assert.Equal(("origin/main", "northfield/platform/web-app"), (form.BaseText, form.BaseRepository));
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();

        var post = Assert.Single(Posts(served.GitLab.Requests.Skip(requests)));
        Assert.Equal(("/api/v4/projects/5002/merge_requests", 201), (post.Path, post.Status));
        using (var body = JsonDocument.Parse(post.Body!))
        {
            Assert.Equal(5001, body.RootElement.GetProperty("target_project_id").GetInt64());
            Assert.Equal(("fork-idea", "main"), (body.RootElement.GetProperty("source_branch").GetString(), body.RootElement.GetProperty("target_branch").GetString()));
        }

        var created = served.GitLab.MergeRequests(served.GitLab.Project(Scenarios.Hosting7bWebApp)).Single(request => request.Iid == 24);
        Assert.Equal((Scenarios.Hosting7bWebAppFork, "fork-idea"), (created.Source.Path, created.SourceBranch));
        Assert.Equal("Created merge request !24", repository.OperationStatus.Text);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task GitLab_s_409_reads_the_list_again_and_offers_Open_merge_request_21()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("create providers");
        using var app = await OpenAsync(data, served, served.WebApp, HostingProvider.GitLab);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var requests = served.GitLab.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/feature/search");
        await form.CreateCommand.ExecuteAsync(null);

        Assert.False(form.IsClosed);
        Assert.Equal("Merge request !21 is already open from feature/search into origin/main.", form.Error);
        Assert.Equal((true, "Open merge request !21"), (form.HasExisting, form.OpenExistingText));
        var sent = served.GitLab.Requests.Skip(requests).ToList();
        Assert.Equal(409, Assert.Single(Posts(sent)).Status);
        Assert.Contains(sent, request => request.Route == "mrs:northfield/platform/web-app" && request.Method == "GET");
        await form.OpenExistingCommand.ExecuteAsync(null);
        await opening;
        Assert.True(form.IsClosed);
        Assert.Equal([new Uri(served.GitLab.Endpoints.WebBase, "northfield/platform/web-app/-/merge_requests/21").AbsoluteUri], OpenedUrls(app));
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task GitLab_s_400_forms_name_the_branch_the_base_or_both_in_the_App_s_words()
    {
        await using var served = await ServeAsync();
        var bare = served.GitLab.Project(Scenarios.Hosting7bWebApp).BarePath!;
        await served.WebApp.GitAsync("branch", "--track", "topic/no-request", "origin/topic/no-request");
        await served.WebApp.GitAsync("update-ref", "refs/remotes/origin/old-base", "refs/remotes/origin/main");
        using var data = new TempDirectory("create providers");
        using var app = await OpenAsync(data, served, served.WebApp, HostingProvider.GitLab);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var tip = await GitInAsync(bare, "rev-parse", "refs/heads/topic/no-request");
        await GitInAsync(bare, "update-ref", "-d", "refs/heads/topic/no-request");
        var requests = served.GitLab.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/topic/no-request");
        Assert.Null(form.HeadNote);
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("GitLab has no branch topic/no-request in northfield/platform/web-app to merge from. Fetch, then try again.", form.Error);

        await ChooseAsync(app, form, "origin/old-base");
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("GitLab cannot find topic/no-request or old-base. Fetch, then try again.", form.Error);

        await GitInAsync(bare, "update-ref", "refs/heads/topic/no-request", tip);
        form.PullRequestTitle += " ";
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("GitLab has no branch old-base in northfield/platform/web-app. Choose another branch to merge into.", form.Error);
        Assert.Null(form.ExistingNumber);

        Assert.Equal([400, 400, 400], Posts(served.GitLab.Requests.Skip(requests)).Select(post => post.Status));
        Assert.Equal(3, served.GitLab.MergeRequests(served.GitLab.Project(Scenarios.Hosting7bWebApp)).Count);
        form.Cancel();
        await opening;
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task GitLab_s_403_names_the_scope_only_with_its_code()
    {
        await using var served = await ServeAsync();
        await served.WebApp.GitAsync("branch", "--track", "topic/no-request", "origin/topic/no-request");
        using var data = new TempDirectory("create providers");
        using var app = await OpenAsync(data, served, served.WebApp, HostingProvider.GitLab);
        var repository = app.Session.Shell.ActiveTab.Repository!;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/topic/no-request");
        served.GitLab.FailNext("create:northfield/platform/web-app", FakeGitLabFault.InsufficientScope);
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("GitLab refused to create a merge request in northfield/platform/web-app with this token. It needs the api scope, or Merge Request: Create.", form.Error);

        served.GitLab.FailNext("create:northfield/platform/web-app", FakeGitLabFault.Forbidden);
        form.PullRequestTitle += " ";
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("GitLab refused to create a merge request in northfield/platform/web-app with this token.", form.Error);
        form.Cancel();
        await opening;
        Assert.Equal(3, served.GitLab.MergeRequests(served.GitLab.Project(Scenarios.Hosting7bWebApp)).Count);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Azure_DevOps_creates_with_201_and_200_and_sends_isDraft()
    {
        await using var served = await ServeAsync();
        await served.MobileApp.GitAsync("branch", "--track", "topic/no-request", "origin/topic/no-request");
        using var data = new TempDirectory("create providers");
        using var app = await OpenAsync(data, served, served.MobileApp, HostingProvider.AzureDevOps);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var requests = served.AzureDevOps.Requests.Count;
        var gitRequests = served.AzureDevOps.GitRequests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/topic/no-request");
        Assert.Equal("Create pull request", form.Title);
        Assert.Equal(("origin/main", "Fernhill-Labs/Mobile App/mobile-app"), (form.BaseText, form.BaseRepository));
        form.IsDraft = true;
        Assert.False(form.ShowsSentTitle);
        Assert.False(form.ShowsQuickActionsNote);
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();

        var post = Assert.Single(Posts(served.AzureDevOps.Requests.Skip(requests)));
        Assert.Equal((MobileAppPullRequestsPath, 201), (post.Path, post.Status));
        using (var body = JsonDocument.Parse(post.Body!))
        {
            var root = body.RootElement;
            Assert.Equal(("refs/heads/topic/no-request", "refs/heads/main"), (root.GetProperty("sourceRefName").GetString(), root.GetProperty("targetRefName").GetString()));
            Assert.Equal(("Try an idea", true), (root.GetProperty("title").GetString(), root.GetProperty("isDraft").GetBoolean()));
            Assert.False(root.TryGetProperty("forkSource", out _));
        }

        Assert.Equal("Created draft pull request !14", repository.OperationStatus.Text);
        await app.WaitForAsync(() => repository.HostingState.List?.PullRequests.Count == 4, "four pull requests listed");
        Assert.Equal(14, repository.HostingState.ByRef["refs/heads/topic/no-request"].Number);
        await repository.OperationStatus.OfferCommand.ExecuteAsync(null);
        Assert.Equal([new Uri(served.AzureDevOps.Endpoints.WebBase, "Fernhill-Labs/Mobile%20App/_git/mobile-app/pullrequest/14").AbsoluteUri], OpenedUrls(app));

        // A 200 is a success as well (g1 G0.12); local-work is pushed first through the app's helper.
        served.AzureDevOps.CreateStatus = 200;
        requests = served.AzureDevOps.Requests.Count;
        (form, opening) = await OpenFormAsync(app, repository, "refs/heads/local-work");
        Assert.Equal("Not on Azure DevOps yet: Create pushes it to origin/local-work first and sets that as its upstream.", form.HeadNote);
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();
        post = Assert.Single(Posts(served.AzureDevOps.Requests.Skip(requests)));
        Assert.Equal(200, post.Status);
        using (var body = JsonDocument.Parse(post.Body!))
        {
            Assert.False(body.RootElement.GetProperty("isDraft").GetBoolean());
        }

        Assert.Equal("Created pull request !15", repository.OperationStatus.Text);
        var git = served.AzureDevOps.GitRequests.Skip(gitRequests).ToList();
        Assert.Contains(git, request => request.GivenUserName == "x-access-token" && request.Status == 200);
        Assert.DoesNotContain(git, request => request.Status == 200 && request.GivenUserName != "x-access-token");
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Azure_DevOps_sends_a_fork_s_head_as_forkSource_with_its_repository_id()
    {
        await using var served = await ServeAsync();
        var fork = served.AzureDevOps.Repository(served.Organization, Scenarios.Hosting7bProject, "mobile-app-fork");
        await GitInAsync(fork.BarePath, "branch", "fork-idea", "fix-typo");
        await served.MobileApp.GitAsync("remote", "add", "fork", Scenarios.AzureDevOpsUrl(served.Organization, Scenarios.Hosting7bProject, "mobile-app-fork", served.AzureDevOps.Port));
        await served.MobileApp.GitAsync("fetch", fork.BarePath, "+refs/heads/fork-idea:refs/remotes/fork/fork-idea");
        using var data = new TempDirectory("create providers");
        using var app = await OpenAsync(data, served, served.MobileApp, HostingProvider.AzureDevOps);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var requests = served.AzureDevOps.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/remotes/fork/fork-idea");
        Assert.Equal(("fork/fork-idea", "Fernhill-Labs/Mobile App/mobile-app-fork"), (form.Head.Text, form.Head.Repository));
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();

        var post = Assert.Single(Posts(served.AzureDevOps.Requests.Skip(requests)));
        Assert.Equal((MobileAppPullRequestsPath, 201), (post.Path, post.Status));
        using (var body = JsonDocument.Parse(post.Body!))
        {
            Assert.Equal(fork.Id, Guid.Parse(body.RootElement.GetProperty("forkSource").GetProperty("repository").GetProperty("id").GetString()!));
            Assert.Equal("refs/heads/fork-idea", body.RootElement.GetProperty("sourceRefName").GetString());
        }

        var created = served.AzureDevOps.PullRequests(served.AzureDevOps.Repository(served.Organization, Scenarios.Hosting7bProject, "mobile-app")).Single(pull => pull.Id == 14);
        Assert.Same(fork, created.Fork);
        Assert.Equal("Created pull request !14", repository.OperationStatus.Text);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Azure_DevOps_TF401179_and_TF401398_are_worded_by_the_App()
    {
        await using var served = await ServeAsync();
        await served.MobileApp.GitAsync("branch", "--track", "topic/no-request", "origin/topic/no-request");
        using var data = new TempDirectory("create providers");
        using var app = await OpenAsync(data, served, served.MobileApp, HostingProvider.AzureDevOps);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var requests = served.AzureDevOps.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/feature/sign-in");
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("Pull request !11 is already open from feature/sign-in into origin/main.", form.Error);
        Assert.Equal("Open pull request !11", form.OpenExistingText);
        form.Cancel();
        await opening;

        var bare = served.AzureDevOps.Repository(served.Organization, Scenarios.Hosting7bProject, "mobile-app").BarePath;
        await GitInAsync(bare, "update-ref", "-d", "refs/heads/topic/no-request");
        (form, opening) = await OpenFormAsync(app, repository, "refs/heads/topic/no-request");
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("Azure DevOps cannot find topic/no-request or main. Fetch, then try again.", form.Error);
        Assert.Null(form.ExistingNumber);
        form.Cancel();
        await opening;

        Assert.Equal([409, 400], Posts(served.AzureDevOps.Requests.Skip(requests)).Select(post => post.Status));
        End(app, data, served);
    }

    [AvaloniaTheory]
    [InlineData(HostingProvider.GitLab)]
    [InlineData(HostingProvider.AzureDevOps)]
    public async Task A_lost_answer_whose_request_is_listed_counts_as_created(HostingProvider provider)
    {
        await using var served = await ServeAsync();
        var clone = provider == HostingProvider.GitLab ? served.WebApp : served.MobileApp;
        await clone.GitAsync("branch", "--track", "topic/no-request", "origin/topic/no-request");
        using var data = new TempDirectory("create providers");
        using var app = await OpenAsync(data, served, clone, provider);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        if (provider == HostingProvider.GitLab)
        {
            served.GitLab.FailNext("create:northfield/platform/web-app", FakeGitLabFault.CreatedThenDropped);
        }
        else
        {
            served.AzureDevOps.FailNext("create:fernhill-labs/mobile app/mobile-app", FakeAzureDevOpsFault.CreatedThenDropped);
        }

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/topic/no-request");
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();

        Assert.Equal(provider == HostingProvider.GitLab ? "Created merge request !24" : "Created pull request !14", repository.OperationStatus.Text);
        Assert.True(repository.OperationStatus.HasOffer);
        End(app, data, served);
    }

    [AvaloniaFact]
    public async Task Manifest_in_another_organization_starts_nothing_and_asks_nothing()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("create providers");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.Manifest.Path], gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        var repository = await app.WaitForRepositoryAsync();
        await app.SignInAsync(HostingProvider.AzureDevOps, served.AzureDevOpsToken, served.Organization, Cancelled);
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.OtherOrganization, "another organization");
        await app.WaitForWatchersQuietAsync();
        var requests = (served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count, served.GitHub.Requests.Count);
        var log = app.LogMark();

        await repository.Operations.CreatePullRequestAsync("refs/heads/main", null);
        await repository.Operations.CreatePullRequestAsync("refs/heads/main", "refs/remotes/origin/main");

        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Equal(requests, (served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count, served.GitHub.Requests.Count));
        Assert.Equal(2, app.LogLinesSince(log).Count(line => line.Contains("[INF] manifest: Create pull request for main: not started, the signed-in account is in another organization.", StringComparison.Ordinal)));
        End(app, data, served);
    }

    /// <summary>
    /// D356 after the sign-in the create stacks first (13.3): <c>manifest/</c> signed out, its sign-in
    /// lands in another organization; the guard reads the hosting's own state, which its Apply has set
    /// when the sign-in returns (the view model's copy follows later), so no form opens and nothing is asked.
    /// </summary>
    [AvaloniaFact]
    public async Task Manifest_signed_out_whose_sign_in_lands_in_another_organization_opens_no_form()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("create providers");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.Manifest.Path], gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.SignedOut, "signed out");
        await app.WaitForWatchersQuietAsync();
        var opened = new List<DialogViewModel>();
        app.Session.Services.DialogHost.Open.CollectionChanged += (_, change) => opened.AddRange(change.NewItems?.OfType<DialogViewModel>() ?? []);
        var log = app.LogMark();

        var opening = repository.Operations.CreatePullRequestAsync("refs/heads/main", null);
        var signIn = await app.WaitForDialogAsync<SignInDialogViewModel>();
        await signIn.Opened;
        Assert.Equal(HostingProvider.AzureDevOps, signIn.Provider);
        Assert.NotEqual(served.Organization, signIn.Organization);
        signIn.Organization = served.Organization;
        signIn.Token = served.AzureDevOpsToken;
        var requests = (served.AzureDevOps.Requests.Count, served.GitLab.Requests.Count, served.GitHub.Requests.Count);
        await signIn.ConfirmCommand.ExecuteAsync(null);
        await app.WaitForAsync(() => opening.IsCompleted || opened.Any(dialog => dialog is CreatePullRequestDialogViewModel), "the create's end or its form");
        Assert.DoesNotContain(opened, dialog => dialog is CreatePullRequestDialogViewModel);
        await opening;
        Assert.Null(app.Session.Services.DialogHost.Current);
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.OtherOrganization, "another organization");
        await app.WaitForWatchersQuietAsync();

        Assert.Equal([typeof(SignInDialogViewModel)], opened.Select(dialog => dialog.GetType()));
        Assert.Equal(1, app.LogLinesSince(log).Count(line => line.Contains("[INF] manifest: Create pull request for main: not started, the signed-in account is in another organization.", StringComparison.Ordinal)));
        Assert.Equal(["connection:fernhill-labs", "areas"], served.AzureDevOps.Requests.Skip(requests.Item1).Select(request => request.Route));
        Assert.Equal((requests.Item2, requests.Item3), (served.GitLab.Requests.Count, served.GitHub.Requests.Count));
        End(app, data, served);
    }

    /// <summary>
    /// A head in another repository whose read fails (13.5): GitLab reads the source project first and
    /// returns its 404 as it came, which does not say which of the two repositories failed, so the
    /// App's words name both, the head's first, never the base's alone; nothing is posted.
    /// </summary>
    [AvaloniaFact]
    public async Task GitLab_s_404_on_another_repository_s_head_names_both_repositories()
    {
        await using var served = await ServeAsync();
        await served.WebApp.GitAsync("remote", "add", "gone", Scenarios.GitLabUrl("ada/gone-app", served.GitLab.Port));
        await served.WebApp.GitAsync("update-ref", "refs/remotes/gone/idea", "refs/remotes/origin/main");
        using var data = new TempDirectory("create providers");
        using var app = await OpenAsync(data, served, served.WebApp, HostingProvider.GitLab);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        Assert.True(repository.HostingState.RequestRemotes.ContainsKey("gone"));
        var requests = served.GitLab.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/remotes/gone/idea");
        Assert.Equal(("gone/idea", "ada/gone-app"), (form.Head.Text, form.Head.Repository));
        Assert.Equal(("origin/main", "northfield/platform/web-app"), (form.BaseText, form.BaseRepository));
        await form.CreateCommand.ExecuteAsync(null);

        Assert.False(form.IsClosed);
        Assert.Equal("This account cannot see ada/gone-app or northfield/platform/web-app on GitLab.", form.Error);
        Assert.Equal(["project:ada/gone-app 404"], served.GitLab.Requests.Skip(requests).Select(request => $"{request.Route} {request.Status}"));
        Assert.Empty(Posts(served.GitLab.Requests));
        form.Cancel();
        await opening;
        End(app, data, served);
    }

    [AvaloniaTheory]
    [InlineData(HostingProvider.GitLab)]
    [InlineData(HostingProvider.AzureDevOps)]
    public async Task Signed_out_the_listed_provider_s_sign_in_comes_first(HostingProvider provider)
    {
        await using var served = await ServeAsync();
        var clone = provider == HostingProvider.GitLab ? served.WebApp : served.MobileApp;
        using var data = new TempDirectory("create providers");
        using var app = ShellDriver.StartWithTabs(data.Path, [clone.Path], gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.SignedOut, "signed out");
        await app.WaitForWatchersQuietAsync();

        // Cancelled: no form, nothing asked.
        var opening = repository.Operations.CreatePullRequestAsync("refs/heads/local-work", null);
        var signIn = await app.WaitForDialogAsync<SignInDialogViewModel>();
        Assert.Equal(provider, signIn.Provider);
        signIn.Cancel();
        await opening;
        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Empty(served.GitLab.Requests);
        Assert.Empty(served.AzureDevOps.Requests);

        // Signed in: the form follows, in the provider's words.
        opening = repository.Operations.CreatePullRequestAsync("refs/heads/local-work", null);
        signIn = await app.WaitForDialogAsync<SignInDialogViewModel>();
        await signIn.Opened;
        Assert.Equal(provider, signIn.Provider);
        if (provider == HostingProvider.AzureDevOps)
        {
            Assert.Equal(served.Organization, signIn.Organization);
        }

        signIn.Token = provider == HostingProvider.GitLab ? served.GitLabToken : served.AzureDevOpsToken;
        await signIn.ConfirmCommand.ExecuteAsync(null);
        var form = await app.WaitForDialogAsync<CreatePullRequestDialogViewModel>();
        await form.Starting;
        Assert.Equal(provider == HostingProvider.GitLab ? "Create merge request" : "Create pull request", form.Title);
        Assert.Equal("origin/main", form.BaseText);
        Assert.Empty(served.GitHub.Requests);
        form.Cancel();
        await opening;
        Assert.Empty(Posts(provider == HostingProvider.GitLab ? served.GitLab.Requests : served.AzureDevOps.Requests));
        End(app, data, served);
    }

    private static IEnumerable<FakeServiceRequest> Posts(IEnumerable<FakeServiceRequest> requests) =>
        requests.Where(request => request.Method == "POST" && request.Route is { } route && route.StartsWith("create:", StringComparison.Ordinal));

    private static async Task<(CreatePullRequestDialogViewModel Form, Task Opening)> OpenFormAsync(ShellDriver app, RepositoryViewModel repository, string headRef, string? baseRef = null)
    {
        var opening = repository.Operations.CreatePullRequestAsync(headRef, baseRef);
        var form = await app.WaitForDialogAsync<CreatePullRequestDialogViewModel>();
        await form.Starting;
        return (form, opening);
    }

    /// <summary>Create, which closes the form: its error line is the failure's message when it does not.</summary>
    private static async Task CreateAsync(CreatePullRequestDialogViewModel form, Task opening)
    {
        await form.CreateCommand.ExecuteAsync(null);
        Assert.True(form.IsClosed, form.Error ?? "The form is still open.");
        await opening;
    }

    private static async Task ChooseAsync(ShellDriver app, CreatePullRequestDialogViewModel form, string baseText)
    {
        var choosing = form.ChooseBaseCommand.ExecuteAsync(null);
        var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        pick.Selected = baseText;
        pick.ConfirmCommand.Execute(null);
        await choosing;
        Assert.Equal(baseText, form.BaseText);
    }

    private static async Task<ServedHosting7b> ServeAsync()
    {
        var copy = await Scenarios.Hosting7bAsync();
        try
        {
            return await Scenarios.ServeHosting7bAsync(copy, azureDevOpsPort: 0, gitLabPort: 0, gitHubPort: 0, ownsCopy: true, ct: Cancelled);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    /// <summary>One tab on <paramref name="clone"/>, signed in to <paramref name="provider"/>, once its three requests are listed and the watchers are quiet.</summary>
    private static async Task<ShellDriver> OpenAsync(TempDirectory data, ServedHosting7b served, TempRepo clone, HostingProvider provider)
    {
        var app = ShellDriver.StartWithTabs(data.Path, [clone.Path], promptHandler: new ScriptedPromptHandler(), gitHub: served.GitHub, azureDevOps: served.AzureDevOps, gitLab: served.GitLab);
        try
        {
            var repository = await app.WaitForRepositoryAsync();
            if (provider == HostingProvider.AzureDevOps)
            {
                await app.SignInAsync(provider, served.AzureDevOpsToken, served.Organization, Cancelled);
            }
            else
            {
                await app.SignInAsync(provider, served.GitLabToken, cancellationToken: Cancelled);
            }

            await app.WaitForAsync(() => repository.HostingState.List?.PullRequests.Count == 3, "three requests listed");
            await app.WaitForWatchersQuietAsync();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    private static List<string> CallsSince(ShellDriver app, long mark) =>
        [.. app.GitCalls().Where(call => call.Sequence > mark).Select(call => call.CommandText)];

    private static List<string> OpenedUrls(ShellDriver app) =>
        [.. app.Launcher.Requests.Where(request => request.Kind == FileLaunchKind.OpenUrl).Select(request => request.FullPath)];

    /// <summary>Git in a bare folder of a fake (7a's form, <c>CreatePullRequestOperationTests</c>).</summary>
    private static async Task<string> GitInAsync(string gitDirectory, params string[] arguments)
    {
        using var scratch = await TempRepo.CreateAsync();
        return (await scratch.GitAsync(["--git-dir", gitDirectory, .. arguments])).StandardOutput.Trim();
    }

    /// <summary>Closes the app, then checks that no token reached the data folder, the clones or git's calls and that no fake saw anything wrong.</summary>
    private static void End(ShellDriver app, TempDirectory data, ServedHosting7b served)
    {
        var calls = app.Session.GitCalls;
        app.Dispose();
        foreach (var token in new[] { served.AzureDevOpsToken, served.GitLabToken, served.GitLabLegacyToken, served.GitHubToken })
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.MobileApp.Path, served.Manifest.Path, served.WebApp.Path], calls);
        }

        Assert.Empty(served.AzureDevOps.Errors);
        Assert.Empty(served.GitLab.Errors);
        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(app.RefusedRequests.Addresses);
    }
}
