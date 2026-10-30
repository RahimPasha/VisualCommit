using System.Text.Json;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Undo;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;
using Texts = VisualCommit.App.ViewModels.Operations.OperationTexts.CreatePullRequestTexts;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// A new pull request through the app (docs/phase-7a-design.md 12.3 to 12.6, 12.7; D312 to D318,
/// D334; design 17 item 8, rulings 54 and 61), started through <see cref="ShellDriver"/> with the
/// fake of GitHub on port 0 and signed in: the head's place and push (a pushed branch, one not on
/// GitHub, one ahead, one tracking another name, a remote branch, a fork's), the POST's body, the
/// report, the offer, the list, the undo entry of the push, Cancel offered while pushing only,
/// 12.6's error rows (GitHub's and git's: a rejected push, a declined one, git's credentials
/// refused), the note read again after a failure, a lost answer counted as created, the bases of
/// a fork, a renamed repository's default base, and signed out.
/// </summary>
public sealed class CreatePullRequestOperationTests
{
    private const string TopicRef = "refs/heads/topic/no-pr";

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [AvaloniaFact]
    public async Task A_branch_GitHub_holds_is_not_pushed_and_its_pull_request_is_listed_with_the_offer()
    {
        await using var served = await ServeAsync();
        await served.Hello.GitAsync("branch", "--track", "topic/no-pr", "origin/topic/no-pr");
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var undo = repository.UndoHistory.Entries.Count;
        var calls = app.CallMark();
        var requests = served.GitHub.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, TopicRef);
        Assert.Equal(new PullRequestEnd("topic/no-pr", "octo/hello") { Branch = "topic/no-pr" }, form.Head);
        Assert.Equal(("origin/main", "octo/hello", "Try an idea"), (form.BaseText, form.BaseRepository, form.PullRequestTitle));
        Assert.Null(form.HeadNote);
        Assert.Null(form.Error);
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();

        Assert.True(form.IsClosed);
        Assert.True(await form.Result);
        var post = Post(served, "create:octo/hello", requests);
        Assert.Equal(201, post.Status);
        Assert.Equal(("Try an idea", string.Empty, "topic/no-pr", "main", false, false), Body(post));
        Assert.Equal("Created pull request #6", repository.OperationStatus.Text);
        Assert.Equal((true, Texts.OpenInBrowser), (repository.OperationStatus.HasOffer, repository.OperationStatus.OfferText));
        Assert.Equal(6, repository.HostingState.List!.PullRequests[0].Number);
        Assert.Equal(6, repository.HostingState.ByRef[TopicRef].Number);
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
        Assert.Equal(undo, repository.UndoHistory.Entries.Count);
        Assert.Contains(AppLogFile.ReadLines(data.Path), line => line.EndsWith("hello: Create pull request for topic/no-pr: into octo/hello main, draft no, no push", StringComparison.Ordinal));

        await repository.OperationStatus.OfferCommand.ExecuteAsync(null);
        Assert.Equal([new Uri(served.GitHub.Endpoints.WebBase, "octo/hello/pull/6").AbsoluteUri], OpenedUrls(app));
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_renamed_repository_creates_into_its_default_base_under_its_new_name()
    {
        await using var served = await ServeAsync();
        served.GitHub.Rename("octo/hello", "greeting");
        await served.Hello.GitAsync("branch", "--track", "topic/no-pr", "origin/topic/no-pr");
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var requests = served.GitHub.Requests.Count;

        // origin's URL names octo/hello; GitHub's answers name octo/greeting, and so does the form,
        // whose default base is origin's main (7.8, 12.3).
        var (form, opening) = await OpenFormAsync(app, repository, TopicRef);
        Assert.Equal(new PullRequestEnd("topic/no-pr", "octo/greeting") { Branch = "topic/no-pr" }, form.Head);
        Assert.Equal(("origin/main", "octo/greeting"), (form.BaseText, form.BaseRepository));
        Assert.Null(form.Error);
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();

        var post = Post(served, "create:octo/greeting", requests);
        Assert.Equal("/api/v3/repos/octo/greeting/pulls", post.Path);
        Assert.Equal(("Try an idea", string.Empty, "topic/no-pr", "main", false, false), Body(post));
        Assert.Equal("Created pull request #6", repository.OperationStatus.Text);
        Assert.Equal(6, repository.HostingState.List!.PullRequests[0].Number);
        Assert.Equal(6, repository.HostingState.ByRef[TopicRef].Number);
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_branch_not_on_GitHub_is_pushed_first_in_the_same_operation_with_Cancel_only_while_it_pushes()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var undo = repository.UndoHistory.Entries.Count;
        var calls = app.CallMark();
        var requests = served.GitHub.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/local-work");
        Assert.Equal(Texts.PushedFirstWithUpstream("origin/local-work"), form.HeadNote);
        Assert.Equal("Local change", form.PullRequestTitle);
        form.IsDraft = true;
        form.Description = "A note for the reviewers.";

        using var texts = app.RecordStatusTexts();
        var pushHold = served.GitHub.HoldNext("git:octo/hello");
        var postHold = served.GitHub.HoldNext("create:octo/hello");
        try
        {
            var creating = form.CreateCommand.ExecuteAsync(null);
            await app.WaitForAsync(() => pushHold.Arrived.IsCompleted, "the push held");
            Assert.True(form.IsCreating);
            Assert.True(form.CanCancelForm);
            Assert.True(repository.Queue.Current?.CanCancel);
            pushHold.Dispose();

            await app.WaitForAsync(() => postHold.Arrived.IsCompleted, "the POST held");
            Assert.True(form.IsCreating);
            Assert.False(form.CanCancelForm);
            Assert.False(form.CancelFormCommand.CanExecute(null));
            Assert.False(repository.Queue.Current?.CanCancel);
            Assert.Equal(Texts.CreatingPullRequest, repository.OperationStatus.Text);
            postHold.Dispose();
            await creating;
        }
        finally
        {
            pushHold.Dispose();
            postHold.Dispose();
        }

        await opening;
        await app.WaitForIdleAsync();
        Assert.True(await form.Result);
        var shown = texts.Texts;
        var pushing = shown.ToList().FindIndex(text => text.StartsWith("Pushing local-work to origin/local-work…", StringComparison.Ordinal));
        var posting = shown.ToList().IndexOf(Texts.CreatingPullRequest);
        var created = shown.ToList().IndexOf("Created draft pull request #6");
        Assert.True(pushing >= 0 && pushing < posting && posting < created, string.Join(" | ", shown));
        Assert.DoesNotContain(shown.Take(pushing), text => text == Texts.CreatingPullRequest);
        Assert.Equal(("Local change", "A note for the reviewers.", "local-work", "main", true, false), Body(Post(served, "create:octo/hello", requests)));
        Assert.Contains("git push --progress --set-upstream origin refs/heads/local-work:refs/heads/local-work", CallsSince(app, calls));
        Assert.Equal("origin", await GitAsync(served.Hello, "config", "branch.local-work.remote"));
        Assert.Equal("refs/heads/local-work", await GitAsync(served.Hello, "config", "branch.local-work.merge"));
        Assert.Single(AppLogFile.ReadLines(data.Path), AppLogFile.OperationEnded("hello", "Create pull request for local-work").IsMatch);
        Assert.DoesNotContain(AppLogFile.ReadLines(data.Path), AppLogFile.OperationEnded("hello", "Push local-work").IsMatch);
        Assert.DoesNotContain(AppLogFile.ReadLines(data.Path), line => line.Contains("A note for the reviewers.", StringComparison.Ordinal));
        Assert.Contains(AppLogFile.ReadLines(data.Path), line => line.EndsWith("hello: Create pull request for local-work: into octo/hello main, draft yes, push to origin/local-work first", StringComparison.Ordinal));

        // D314: the push is in the undo history as any push's.
        var entry = Assert.Single(repository.UndoHistory.Entries.Skip(undo));
        Assert.Equal(UndoKind.Push, entry.Kind);
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_branch_ahead_of_its_upstream_is_pushed_to_it_without_set_upstream()
    {
        await using var served = await ServeAsync();
        await served.Hello.GitAsync("switch", "--quiet", "--track", "-c", "topic/no-pr", "origin/topic/no-pr");
        await served.Hello.CommitFileAsync("idea.txt", "more\n", "More of the idea");
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var calls = app.CallMark();

        var (form, opening) = await OpenFormAsync(app, repository, TopicRef);
        Assert.Equal(Texts.AheadPushedFirst(1, "origin/topic/no-pr"), form.HeadNote);
        Assert.Equal("1 commit is not on GitHub yet: Create pushes it to origin/topic/no-pr first.", form.HeadNote);
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();

        Assert.True(await form.Result);
        Assert.Contains("git push --progress origin refs/heads/topic/no-pr:refs/heads/topic/no-pr", CallsSince(app, calls));
        Assert.Equal(await GitAsync(served.Hello, "rev-parse", TopicRef), (await GitInAsync(Bare(served, "octo/hello"), "rev-parse", TopicRef)).Trim());
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_branch_tracking_another_name_is_pushed_under_its_own_and_its_upstream_kept()
    {
        await using var served = await ServeAsync();
        await served.Hello.GitAsync("switch", "--quiet", "--track", "-c", "next", "origin/main");
        await served.Hello.CommitFileAsync("next.txt", "topic\n", "Start the topic");
        var mainOnGitHub = (await GitInAsync(Bare(served, "octo/hello"), "rev-parse", "refs/heads/main")).Trim();
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var calls = app.CallMark();

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/next");
        Assert.Equal(("next", "origin/main"), (form.Head.Text, form.BaseText));
        Assert.Null(form.Error);
        Assert.Equal(Texts.PushedFirst("origin/next"), form.HeadNote);
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();

        Assert.True(await form.Result);
        Assert.Contains("git push --progress origin refs/heads/next:refs/heads/next", CallsSince(app, calls));
        Assert.Equal(mainOnGitHub, (await GitInAsync(Bare(served, "octo/hello"), "rev-parse", "refs/heads/main")).Trim());
        Assert.Equal("refs/heads/main", await GitAsync(served.Hello, "config", "branch.next.merge"));

        // On GitHub as it is (the design's name, topic, cannot be a branch beside origin's topic/no-pr): no note; one more commit: ahead by one, read by a count.
        var (again, reopened) = await OpenFormAsync(app, repository, "refs/heads/next");
        Assert.Null(again.HeadNote);
        again.Cancel();
        await reopened;
        await served.Hello.CommitFileAsync("next.txt", "topic\nmore\n", "More of the topic");
        await app.WaitForAsync(() => repository.Refs!.Refs.Any(gitRef => gitRef.FullName == "refs/heads/next" && gitRef.Ahead == 2), "the new commit read");
        await app.WaitForWatchersQuietAsync();
        var (third, opened) = await OpenFormAsync(app, repository, "refs/heads/next");
        Assert.Equal(Texts.AheadPushedFirst(1, "origin/next"), third.HeadNote);
        third.Cancel();
        await opened;
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_remote_branch_is_never_pushed_and_GitHub_s_branch_is_the_head()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var calls = app.CallMark();
        var requests = served.GitHub.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/remotes/origin/topic/no-pr");
        Assert.Equal(("origin/topic/no-pr", "octo/hello"), (form.Head.Text, form.Head.Repository));
        Assert.Null(form.HeadNote);
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();

        Assert.Equal(("Try an idea", string.Empty, "topic/no-pr", "main", false, false), Body(Post(served, "create:octo/hello", requests)));
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
        Assert.Equal(6, repository.HostingState.ByRef["refs/remotes/origin/topic/no-pr"].Number);
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task In_a_fork_the_head_goes_to_origin_by_the_remotes_order_and_the_base_is_the_parent_s_default_branch()
    {
        await using var served = await ServeAsync();
        await served.Tool.GitAsync("switch", "--quiet", "--track", "-c", "idea", "upstream/main");
        await served.Tool.CommitFileAsync("idea.txt", "idea\n", "An idea for the tool");
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served, served.Tool, count: 2);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var undo = repository.UndoHistory.Entries.Count;
        var calls = app.CallMark();
        var requests = served.GitHub.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/idea");
        Assert.Equal(("idea", "octo/tool"), (form.Head.Text, form.Head.Repository));
        Assert.Equal(Texts.PushedFirst("origin/idea"), form.HeadNote);
        Assert.Equal(("upstream/main", "acme/tool"), (form.BaseText, form.BaseRepository));

        // octo's fine-grained token cannot open one in another owner's repository (12.6); its classic one can.
        // The push ended before the refused POST: idea is on origin now, and the note read again says nothing (12.4).
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal(Texts.ForkTokenRefused("acme/tool"), form.Error);
        Assert.Contains(served.GitHub.Requests.Skip(requests), request => request.Route == "create:acme/tool" && request.Status == 403);
        Assert.Null(form.HeadNote);
        await app.SignInAsync(served.OctoClassicToken, Cancelled);
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.Loaded, "signed in with the classic token");

        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();
        Assert.True(await form.Result);
        Assert.Contains("git push --progress origin refs/heads/idea:refs/heads/idea", CallsSince(app, calls));
        Assert.Equal("upstream", await GitAsync(served.Tool, "config", "branch.idea.remote"));
        Assert.Equal("refs/heads/main", await GitAsync(served.Tool, "config", "branch.idea.merge"));
        var post = Post(served, "create:acme/tool", requests);
        Assert.Equal(201, post.Status);
        Assert.Equal(("An idea for the tool", string.Empty, "octo:idea", "main", false, true), Body(post));
        Assert.Contains(served.GitHub.PullRequests("acme/tool"), pull => pull.HeadRef == "idea" && pull.IsOpen);

        // D314: one entry, for the push of the failed create; the second pushed nothing.
        Assert.Equal(UndoKind.Push, Assert.Single(repository.UndoHistory.Entries.Skip(undo)).Kind);

        // "Already exists" for one the list cannot hold (the list is acme/tool's, the base octo/tool): GitHub's words (12.6).
        served.GitHub.AddPullRequest("octo", "tool", "An idea for the tool", "octo", "idea", "main");
        (form, opening) = await OpenFormAsync(app, repository, "refs/heads/idea");
        Assert.Null(form.HeadNote);
        await ChooseAsync(app, form, "origin/main");
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("Validation Failed: A pull request already exists for octo:idea.", form.Error);
        Assert.Null(form.ExistingNumber);
        Assert.False(form.IsClosed);
        form.Cancel();
        await opening;
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_fork_without_its_parent_s_remote_offers_the_parent_s_default_branch_as_owner_and_branch()
    {
        await using var served = await ServeAsync();
        await served.Tool.GitAsync("remote", "remove", "upstream");
        await served.Tool.GitAsync("switch", "--quiet", "-c", "idea");
        await served.Tool.CommitFileAsync("idea.txt", "idea\n", "An idea for the tool");
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served, served.Tool, count: 2);
        var repository = app.Session.Shell.ActiveTab.Repository!;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/main");
        Assert.Equal(("acme:main", "acme/tool"), (form.BaseText, form.BaseRepository));
        Assert.Null(form.Error);

        var choosing = form.ChooseBaseCommand.ExecuteAsync(null);
        var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        Assert.Equal(Texts.ChooseBaseTitle, pick.Title);
        Assert.Equal(["origin/fix-typo", "origin/main", "acme:main"], pick.Options.Select(option => option.Text));
        Assert.Equal(["octo/tool", "octo/tool", "acme/tool"], pick.Options.Select(option => option.Detail));
        Assert.Equal("acme:main", pick.Selected);
        pick.HandleEscape();
        await choosing;
        Assert.False(form.IsClosed);
        Assert.Equal("acme:main", form.BaseText);
        form.Cancel();
        await opening;

        // Into acme:main, a repository no remote is (ruling 61): idea pushed to origin, the POST into acme/tool from octo:idea.
        await app.SignInAsync(served.OctoClassicToken, Cancelled);
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.Loaded, "signed in with the classic token");
        var calls = app.CallMark();
        var requests = served.GitHub.Requests.Count;
        (form, opening) = await OpenFormAsync(app, repository, "refs/heads/idea");
        Assert.Equal(("acme:main", "acme/tool"), (form.BaseText, form.BaseRepository));
        Assert.Equal(Texts.PushedFirstWithUpstream("origin/idea"), form.HeadNote);
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();
        Assert.Contains("git push --progress --set-upstream origin refs/heads/idea:refs/heads/idea", CallsSince(app, calls));
        Assert.Equal(("An idea for the tool", string.Empty, "octo:idea", "main", false, true), Body(Post(served, "create:acme/tool", requests)));
        Assert.Single(served.GitHub.Requests.Skip(requests), request => request.Route == "create:acme/tool");
        Assert.Contains(served.GitHub.PullRequests("acme/tool"), pull => pull.HeadRef == "idea" && pull.IsOpen);
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task GitHub_s_refusals_reach_the_form_in_12_6_s_words_and_a_lost_answer_counts_as_created()
    {
        await using var served = await ServeAsync();
        await served.Hello.GitAsync("branch", "--track", "topic/no-pr", "origin/topic/no-pr");
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var (form, opening) = await OpenFormAsync(app, repository, TopicRef);

        var rows = new (FakeGitHubFault Fault, string Words)[]
        {
            (FakeGitHubFault.Forbidden, "GitHub refused to create a pull request in octo/hello with this token. It needs the permission pull_requests=write."),
            (FakeGitHubFault.NotFound, "This account cannot see octo/hello on GitHub."),
            (FakeGitHubFault.SsoRequired, "acme requires single sign-on: authorise the token for it on GitHub, then try again."),
            (FakeGitHubFault.SecondaryRateLimit, "GitHub's rate limit was reached. Try again after "),
            (FakeGitHubFault.Malformed, "GitHub's answer could not be read."),
            (FakeGitHubFault.ServerError, "GitHub had a problem. Try again later."),
            (FakeGitHubFault.BadCredentials, OperationTexts.TokenRefused("octo")),
        };
        foreach (var (fault, words) in rows)
        {
            served.GitHub.FailNext("create:octo/hello", fault);
            await form.CreateCommand.ExecuteAsync(null);
            Assert.False(form.IsClosed);
            Assert.NotNull(form.Error);
            Assert.StartsWith(words, form.Error, StringComparison.Ordinal);
            Assert.True(form.IsIdle);
            Assert.Equal("Create pull request for topic/no-pr failed", repository.OperationStatus.Text);
            Assert.False(repository.OperationStatus.HasOffer);
            if (fault == FakeGitHubFault.BadCredentials)
            {
                await app.SignInAsync(served.OctoToken, Cancelled);
                await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.Loaded, "signed in again");
            }
        }

        Assert.DoesNotContain(served.GitHub.PullRequests("octo/hello"), pull => pull.Number == 6);

        // A lost answer after GitHub made it (D316): the list read again finds it.
        served.GitHub.FailNext("create:octo/hello", FakeGitHubFault.CreatedThenDropped);
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();
        Assert.True(await form.Result);
        Assert.Equal("Created pull request #6", repository.OperationStatus.Text);
        Assert.True(repository.OperationStatus.HasOffer);
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task GitHub_s_422s_name_what_it_refused()
    {
        await using var served = await ServeAsync();
        await served.Hello.GitAsync("branch", "--track", "topic/no-pr", "origin/topic/no-pr");
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;

        // Branches the app still lists that GitHub no longer has.
        var bare = Bare(served, "octo/hello");
        await GitInAsync(bare, "update-ref", "-d", "refs/heads/docs/readme");
        await GitInAsync(bare, "update-ref", "-d", "refs/heads/feature/login");
        await served.GitHub.SyncRefsAsync("octo/hello", Cancelled);

        var (form, opening) = await OpenFormAsync(app, repository, TopicRef);
        await ChooseAsync(app, form, "origin/docs/readme");
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("GitHub has no branch docs/readme in octo/hello. Choose another branch to merge into.", form.Error);
        form.Cancel();
        await opening;

        (form, opening) = await OpenFormAsync(app, repository, "refs/remotes/origin/feature/login");
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("GitHub does not accept octo:feature/login as the branch to merge from. It must be in octo/hello or in a fork of it.", form.Error);
        form.Cancel();
        await opening;

        (form, opening) = await OpenFormAsync(app, repository, "refs/heads/main");
        Assert.Equal(Texts.SameBranch("main"), form.Error);
        Assert.False(form.CreateCommand.CanExecute(null));
        await ChooseAsync(app, form, "origin/topic/no-pr");
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("origin/topic/no-pr already has every commit of main: there is nothing to merge.", form.Error);
        form.Cancel();
        await opening;
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task Git_s_credentials_refused_during_the_push_give_their_own_words()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var requests = served.GitHub.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/local-work");
        served.GitHub.Revoke(served.OctoToken);
        await form.CreateCommand.ExecuteAsync(null);

        Assert.Equal(OperationTexts.GitCredentialsRefused("octo"), form.Error);
        Assert.False(app.Session.Services.DialogHost.Current is ErrorDialogViewModel);
        Assert.DoesNotContain(served.GitHub.Requests.Skip(requests), request => request.Route == "create:octo/hello");
        form.Cancel();
        await opening;
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_rejected_push_and_one_GitHub_declines_give_git_s_words_and_post_nothing()
    {
        await using var served = await ServeAsync();
        await served.Hello.GitAsync("switch", "--quiet", "--track", "-c", "topic/no-pr", "origin/topic/no-pr");
        await served.Hello.CommitFileAsync("idea.txt", "more\n", "More of the idea");

        // GitHub's topic/no-pr moved on to a commit the clone has never seen: the push is no fast-forward.
        var bare = Bare(served, "octo/hello");
        var tip = (await GitInAsync(bare, "rev-parse", TopicRef)).Trim();
        var elsewhere = (await GitInAsync(bare, "commit-tree", tip + "^{tree}", "-p", tip, "-m", "Pushed from elsewhere")).Trim();
        await GitInAsync(bare, "update-ref", TopicRef, elsewhere, tip);
        await served.GitHub.SyncRefsAsync("octo/hello", Cancelled);
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var requests = served.GitHub.Requests.Count;

        var (form, opening) = await OpenFormAsync(app, repository, TopicRef);
        Assert.Equal(Texts.AheadPushedFirst(1, "origin/topic/no-pr"), form.HeadNote);
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("origin has commits that your topic/no-pr does not have. Pull first, then push again.", form.Error);
        Assert.Equal(OperationTexts.PushRejected("origin", "topic/no-pr"), form.Error);
        Assert.Same(form, app.Session.Services.DialogHost.Current);
        Assert.True(form.IsIdle);
        Assert.Equal(Texts.AheadPushedFirst(1, "origin/topic/no-pr"), form.HeadNote);
        Assert.Equal("Create pull request for topic/no-pr failed", repository.OperationStatus.Text);
        Assert.Equal(elsewhere, (await GitInAsync(bare, "rev-parse", TopicRef)).Trim());
        form.Cancel();
        await opening;

        // GitHub's git declines the push (a pre-receive hook): git's first line about it, after the pack's lines and "To".
        InstallPreReceive(bare, "echo 'Pushes to octo/hello are closed today.' >&2\nexit 1");
        (form, opening) = await OpenFormAsync(app, repository, "refs/heads/local-work");
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Equal("Git could not push local-work to origin/local-work: remote: Pushes to octo/hello are closed today.", form.Error);
        Assert.Same(form, app.Session.Services.DialogHost.Current);
        Assert.True(form.IsIdle);
        Assert.Equal(Texts.PushedFirstWithUpstream("origin/local-work"), form.HeadNote);
        Assert.Equal("Create pull request for local-work failed", repository.OperationStatus.Text);
        Assert.Equal(string.Empty, (await GitInAsync(bare, "for-each-ref", "refs/heads/local-work")).Trim());
        form.Cancel();
        await opening;

        Assert.DoesNotContain(served.GitHub.Requests.Skip(requests), request => request.Route == "create:octo/hello");
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task A_push_that_ended_before_a_refused_create_is_not_noted_or_pushed_again()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("create pull request");
        using var app = await OpenAsync(data, served);
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var calls = app.CallMark();

        var (form, opening) = await OpenFormAsync(app, repository, "refs/heads/local-work");
        Assert.Equal(Texts.PushedFirstWithUpstream("origin/local-work"), form.HeadNote);
        served.GitHub.FailNext("create:octo/hello", FakeGitHubFault.Forbidden);
        await form.CreateCommand.ExecuteAsync(null);

        // 12.4: the note read again from the refs the operation left, where local-work is on origin with that upstream.
        Assert.Equal("GitHub refused to create a pull request in octo/hello with this token. It needs the permission pull_requests=write.", form.Error);
        Assert.Single(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
        Assert.Equal("refs/heads/local-work", await GitAsync(served.Hello, "config", "branch.local-work.merge"));
        Assert.Null(form.HeadNote);

        calls = app.CallMark();
        await CreateAsync(form, opening);
        await app.WaitForIdleAsync();
        Assert.DoesNotContain(CallsSince(app, calls), call => call.StartsWith("git push", StringComparison.Ordinal));
        Assert.Equal("Created pull request #6", repository.OperationStatus.Text);
        await EndAsync(app, data, served);
    }

    [AvaloniaFact]
    public async Task Signed_out_the_sign_in_comes_first_and_refusing_it_opens_no_form()
    {
        await using var served = await ServeAsync();
        using var data = new TempDirectory("create pull request");
        using var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path], gitHub: served.GitHub);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.SignedOut, "signed out");

        var opening = repository.Operations.CreatePullRequestAsync("refs/heads/local-work", null);
        var signIn = await app.WaitForDialogAsync<SignInDialogViewModel>();
        signIn.Cancel();
        await opening;

        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Empty(served.GitHub.Requests);
        Assert.Equal(OperationStatusViewModel.ReadyText, repository.OperationStatus.Text);
        await EndAsync(app, data, served);
    }

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

    private static FakeGitHubRequest Post(ServedHosting served, string route, int mark) =>
        Assert.Single(served.GitHub.Requests.Skip(mark), request => request.Route == route && request.Method == "POST" && request.Status == 201);

    private static (string Title, string Body, string Head, string Base, bool Draft, bool MaintainerCanModify) Body(FakeGitHubRequest post)
    {
        using var json = JsonDocument.Parse(post.Body!);
        var root = json.RootElement;
        return (
            root.GetProperty("title").GetString()!,
            root.GetProperty("body").GetString()!,
            root.GetProperty("head").GetString()!,
            root.GetProperty("base").GetString()!,
            root.GetProperty("draft").GetBoolean(),
            root.TryGetProperty("maintainer_can_modify", out var modify) && modify.GetBoolean());
    }

    private static List<string> CallsSince(ShellDriver app, long mark) =>
        [.. app.GitCalls().Where(call => call.Sequence > mark).Select(call => call.CommandText)];

    private static List<string> OpenedUrls(ShellDriver app) =>
        [.. app.Launcher.Requests.Where(request => request.Kind == FileLaunchKind.OpenUrl).Select(request => request.FullPath)];

    private static string Bare(ServedHosting served, string fullName) => served.GitHub.Repository(fullName).BarePath!;

    /// <summary>A <c>pre-receive</c> hook in the bare repository, which the fake's <c>git http-backend</c> runs (executable on Linux and macOS; git for Windows runs any file there).</summary>
    private static void InstallPreReceive(string bare, string body)
    {
        var path = Path.Combine(bare, "hooks", "pre-receive");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "#!/bin/sh\ncat >/dev/null\n" + body.ReplaceLineEndings("\n") + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>The scenario "hosting" served on port 0: the clones' URLs are the fake's, so the app recognises them (design 16.3).</summary>
    private static async Task<ServedHosting> ServeAsync()
    {
        var copy = await Scenarios.HostingAsync();
        try
        {
            return await Scenarios.ServeHostingAsync(copy, port: 0, ct: Cancelled, ownsCopy: true);
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    /// <summary>One tab on <paramref name="repo"/> (<c>hello</c> by default), signed in as octo, once its <paramref name="count"/> pull requests are listed and the watchers are quiet.</summary>
    private static async Task<ShellDriver> OpenAsync(TempDirectory data, ServedHosting served, TempRepo? repo = null, int count = 4)
    {
        var app = ShellDriver.StartWithTabs(data.Path, [(repo ?? served.Hello).Path], gitHub: served.GitHub);
        try
        {
            var repository = await app.WaitForRepositoryAsync();
            await app.SignInAsync(served.OctoToken, Cancelled);
            await app.WaitForAsync(() => repository.HostingState.List?.PullRequests.Count == count, $"{count} pull requests listed");
            await app.WaitForWatchersQuietAsync();
            return app;
        }
        catch
        {
            app.Dispose();
            throw;
        }
    }

    private static async Task<string> GitAsync(TempRepo repo, params string[] arguments) => (await repo.GitAsync(arguments)).StandardOutput.Trim();

    private static async Task<string> GitInAsync(string gitDirectory, params string[] arguments)
    {
        using var scratch = await TempRepo.CreateAsync();
        return (await scratch.GitAsync(["--git-dir", gitDirectory, .. arguments])).StandardOutput;
    }

    /// <summary>Closes the app, then checks that no token reached the data folder, the clones or git's calls, and that the fake saw nothing wrong.</summary>
    private static Task EndAsync(ShellDriver app, TempDirectory data, ServedHosting served)
    {
        var calls = app.Session.GitCalls;
        app.Dispose();
        foreach (var token in new[] { served.OctoToken, served.OctoClassicToken, served.AdaToken })
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.Hello.Path, served.Tool.Path], calls);
        }

        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(app.RefusedRequests.Addresses);
        return Task.CompletedTask;
    }
}
