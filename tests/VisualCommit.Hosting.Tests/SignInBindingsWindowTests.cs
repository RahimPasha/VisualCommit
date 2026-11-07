using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Hosting;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// D290's window after a sign-in (D388): a tab has no bindings between a sign-in and its first read
/// of the URL rewrites, so a network operation started in it waits for that read in flight, adding
/// no git call, or with none in flight reads the rules itself at its start (D332's read), and binds
/// from the rules as they are, never from unrewritten URLs (D278). On 7a's GitHub (<c>hello</c>, the
/// fake on port 0): Create pressed at once after the stacked sign-in, through the app
/// (<see cref="ShellDriver"/>); the same with the tab's read held and failed, a fetch right after a
/// sign-in held and failed, a remote the rules give another user bound to no token, and the
/// auto-fetch, which waits for a later tick, on a tab built by hand whose reads the test holds or
/// fails (<see cref="HeldRulesTab"/>). Each binds octo's token: git's requests by git's version
/// (part B's convention), no prompt. On 0.4's lagged list.
/// </summary>
public sealed class SignInBindingsWindowTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>
    /// The hunt's case (WP-N hunt 3, the tokens lens): signed out, Create on <c>local-work</c>, the
    /// stacked sign-in in GitHub's dialog, Create as soon as the form has its base. Before D388 the
    /// push copied the tab's empty bindings, git asked for a user name and the create failed.
    /// </summary>
    [AvaloniaFact]
    public async Task Signed_out_a_create_pressed_at_once_after_the_sign_in_pushes_with_octo_s_token()
    {
        var copy = await Scenarios.HostingAsync();
        ServedHosting served;
        try
        {
            served = await Scenarios.ServeHostingAsync(copy, port: 0, ct: Cancelled, ownsCopy: true);
        }
        catch
        {
            copy.Dispose();
            throw;
        }

        await using var _ = served;
        using var data = new TempDirectory("sign-in window");
        var prompts = new ScriptedPromptHandler();
        using var app = ShellDriver.StartWithTabs(data.Path, [served.Hello.Path], promptHandler: prompts, gitHub: served.GitHub);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.SignedOut, "signed out");
        await app.WaitForWatchersQuietAsync();
        var gitMark = served.GitHub.GitRequests.Count;
        var log = app.LogMark();
        var calls = app.CallMark();

        var opening = repository.Operations.CreatePullRequestAsync("refs/heads/local-work", null);
        var signIn = await app.WaitForDialogAsync<SignInDialogViewModel>();
        await signIn.Opened;
        Assert.Equal(HostingProvider.GitHub, signIn.Provider);
        signIn.Token = served.OctoToken;
        await signIn.ConfirmCommand.ExecuteAsync(null);
        var form = await app.WaitForDialogAsync<CreatePullRequestDialogViewModel>();
        await form.Starting;
        Assert.Equal("origin/main", form.BaseText);
        await form.CreateCommand.ExecuteAsync(null);
        Assert.True(form.IsClosed, form.Error ?? "The form is still open.");
        await opening;
        await app.WaitForIdleAsync();

        Assert.Empty(prompts.Prompts);
        AssertThroughHelper(app, served.GitHub.GitRequests.Skip(gitMark).ToList());
        Assert.Single(served.GitHub.Requests, request => request.Method == "POST" && request.Route == "create:octo/hello" && request.Status == 201);
        Assert.Equal("Created pull request #6", repository.OperationStatus.Text);
        var lines = app.LogLinesSince(log);
        Assert.Single(lines, line => line.Contains(" URLs bound to the account (Ask).", StringComparison.Ordinal));
        Assert.Single(lines, line => line.EndsWith("hello: Create pull request for local-work: 1 of 1 URLs bound to the account (Ask).", StringComparison.Ordinal));
        var before = app.GitCalls().Where(call => call.Sequence > calls).OrderBy(call => call.Sequence).TakeWhile(call => !call.CommandText.StartsWith("git push", StringComparison.Ordinal)).ToList();
        Assert.Equal(1, before.Count(call => call.CommandText == HeldRulesTab.RulesRead));

        var all = app.Session.GitCalls;
        app.Dispose();
        foreach (var token in new[] { served.OctoToken, served.OctoClassicToken, served.AdaToken })
        {
            TokenTripwire.AssertAbsent(token, [data.Path, served.Hello.Path, served.Tool.Path], all);
        }

        Assert.Empty(served.GitHub.Errors);
        Assert.Empty(app.RefusedRequests.Addresses);
    }

    /// <summary>The same create on a tab whose first rules read after the stacked sign-in is held (Create waits for it) or failed (Create reads the rules itself).</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Signed_out_a_create_waits_for_the_held_rules_read_or_reads_them_itself_when_it_failed(bool readFails)
    {
        await using var tab = await HeldRulesTab.OpenAsync(HostingProvider.GitHub, Cancelled);
        await tab.CreateOnLocalWorkInTheWindowAsync(readFails, "Create pull request for local-work", "Created pull request #6", Cancelled);
    }

    /// <summary>
    /// A fetch right after a sign-in, while the tab's rules read is held: nothing of the fetch runs
    /// (no git call, no request) until the read ends; then it takes the tab's bindings, octo's URL,
    /// and adds no rules read.
    /// </summary>
    [AvaloniaFact]
    public async Task A_fetch_right_after_a_sign_in_waits_for_the_held_rules_read_and_takes_the_tab_s_bindings()
    {
        await using var tab = await HeldRulesTab.OpenAsync(HostingProvider.GitHub, Cancelled);
        var repository = tab.Repository;
        tab.Reads.Hold();
        await tab.SignInAsync(Cancelled);
        await HeldRulesTab.WaitForAsync(() => tab.Reads.Waiting == 1, "the tab's held rules read");
        Assert.Empty(repository.Hosting.Bindings);
        var gitMark = tab.GitRequestsOf().Count;
        var logMark = tab.Log.Lines.Count;
        var callMark = tab.CallMark();

        var fetching = repository.Operations.FetchAsync("origin");
        await HeldRulesTab.SettleAsync();
        Assert.False(fetching.IsCompleted, "The fetch ended while the tab's rules read was held.");
        Assert.Null(repository.Queue.Current);
        Assert.DoesNotContain(tab.CallsSince(callMark), call => call.OperationId is not null);
        Assert.Equal(gitMark, tab.GitRequestsOf().Count);
        tab.Reads.Release();
        await fetching.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        await HeldRulesTab.WaitForAsync(() => repository.Queue.Current is null && repository.IsWatcherQuiet, "the fetch's end");

        Assert.Equal("Fetched from origin", repository.OperationStatus.Text);
        Assert.Empty(tab.Prompts.Prompts);
        tab.AssertThroughHelper(gitMark, "git-upload-pack");
        var messages = tab.MessagesSince(logMark);
        Assert.Equal(["hello: Fetch origin: 1 URLs bound to the account once the tab's URL rules were read."], messages.Where(message => message.Contains(" URLs bound to the account once ", StringComparison.Ordinal)));
        Assert.Equal(["hello: Fetch origin: 1 of 1 URLs bound to the account (Ask)."], messages.Where(message => message.EndsWith(" URLs bound to the account (Ask).", StringComparison.Ordinal)));

        // Before the fetch's own calls one rules read ran git: the tab's, held.
        Assert.Equal(1, tab.CallsSince(callMark).TakeWhile(call => call.OperationId is null).Count(call => call.CommandText == HeldRulesTab.RulesRead));
    }

    /// <summary>
    /// A fetch right after a sign-in whose rules reads fail (<see cref="RulesReads.FailTabReads"/>): no
    /// read is in flight, so the fetch reads the rules itself at its start (one <c>git config --null --get-regexp ^url\.</c>, D332's read),
    /// binds octo's URL through them and fetches with the token.
    /// </summary>
    [AvaloniaFact]
    public async Task A_fetch_right_after_a_sign_in_whose_rules_read_failed_reads_the_rules_itself()
    {
        await using var tab = await HeldRulesTab.OpenAsync(HostingProvider.GitHub, Cancelled);
        var repository = tab.Repository;
        tab.Reads.FailTabReads();
        await tab.SignInAsync(Cancelled);
        const string Failed = "hello: the URL rewrites could not be read; the last ones read are kept.";
        await HeldRulesTab.WaitForAsync(() => tab.Log.Lines.Any(line => line.Message == Failed), "the tab's failed rules read");
        Assert.Empty(repository.Hosting.Bindings);
        var gitMark = tab.GitRequestsOf().Count;
        var logMark = tab.Log.Lines.Count;
        var callMark = tab.CallMark();

        await repository.Operations.FetchAsync("origin").WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        await HeldRulesTab.WaitForAsync(() => repository.Queue.Current is null && repository.IsWatcherQuiet, "the fetch's end");

        Assert.Equal("Fetched from origin", repository.OperationStatus.Text);
        Assert.Empty(tab.Prompts.Prompts);
        tab.AssertThroughHelper(gitMark, "git-upload-pack");
        var messages = tab.MessagesSince(logMark);
        Assert.Equal(["hello: Fetch origin: 1 URLs bound to the account once the URL rules were read."], messages.Where(message => message.Contains(" URLs bound to the account once ", StringComparison.Ordinal)));
        Assert.Equal(["hello: Fetch origin: 1 of 1 URLs bound to the account (Ask)."], messages.Where(message => message.EndsWith(" URLs bound to the account (Ask).", StringComparison.Ordinal)));
        Assert.Equal(1, tab.CallsSince(callMark).TakeWhile(call => call.OperationId is null).Count(call => call.CommandText == HeldRulesTab.RulesRead));
    }

    /// <summary>
    /// D278 inside the window: the user's rule rewrites <c>origin</c> to a URL that names another user
    /// (<c>bob@</c>, the URL itself left as it was). Whether the fetch waits for the tab's held read or
    /// reads the rules itself after it failed, it binds the URL git uses, bob's, to nothing: no
    /// request carries <c>x-access-token</c>, and git's prompt (refused) names bob; bindings made from
    /// the unrewritten URL would have answered bob's with octo's token.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inside_the_window_a_remote_the_rules_give_another_user_is_bound_to_no_token(bool readFails)
    {
        await using var tab = await HeldRulesTab.OpenAsync(HostingProvider.GitHub, Cancelled, async (clone, webBase) =>
        {
            var authority = webBase.GetLeftPart(UriPartial.Authority);
            var bob = new UriBuilder(webBase) { UserName = "bob" }.Uri.GetLeftPart(UriPartial.Authority);
            await clone.GitAsync("config", $"url.{bob}/.insteadOf", authority + "/");
        });
        var repository = tab.Repository;
        const string Failed = "hello: the URL rewrites could not be read; the last ones read are kept.";
        if (readFails)
        {
            tab.Reads.FailTabReads();
        }
        else
        {
            tab.Reads.Hold();
        }

        await tab.SignInAsync(Cancelled);
        if (readFails)
        {
            await HeldRulesTab.WaitForAsync(() => tab.Log.Lines.Any(line => line.Message == Failed), "the tab's failed rules read");
        }
        else
        {
            await HeldRulesTab.WaitForAsync(() => tab.Reads.Waiting == 1, "the tab's held rules read");
        }

        var gitMark = tab.GitRequestsOf().Count;
        var logMark = tab.Log.Lines.Count;
        var fetching = repository.Operations.FetchAsync("origin");
        if (!readFails)
        {
            await HeldRulesTab.SettleAsync();
            tab.Reads.Release();
        }

        // Git's prompt refused, the fetch fails and its error dialog waits to be closed.
        await HeldRulesTab.WaitForAsync(() => fetching.IsCompleted || tab.Services.DialogHost.Current is not null, "the fetch's end or its error dialog");
        Assert.IsType<ErrorDialogViewModel>(tab.Services.DialogHost.Current);
        tab.Services.DialogHost.CancelCurrent();
        await fetching.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        await HeldRulesTab.WaitForAsync(() => repository.Queue.Current is null && repository.IsWatcherQuiet, "the fetch's end");

        Assert.Empty(repository.Hosting.Bindings);
        var requests = tab.GitRequestsOf().Skip(gitMark).ToList();
        Assert.NotEmpty(requests);
        Assert.DoesNotContain(requests, request => request.GivenUserName == "x-access-token" || request.Status == 200);
        Assert.NotEmpty(tab.Prompts.Prompts);
        Assert.All(tab.Prompts.Prompts, prompt => Assert.Contains("bob@", prompt.Text, StringComparison.Ordinal));
        var messages = tab.MessagesSince(logMark);
        Assert.Equal(
            [readFails ? "hello: Fetch origin: 0 URLs bound to the account once the URL rules were read." : "hello: Fetch origin: 0 URLs bound to the account once the tab's URL rules were read."],
            messages.Where(message => message.Contains(" URLs bound to the account", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The auto-fetch, due, inside the window: its tick starts nothing while the tab's rules read is
    /// held (it would fetch without the account's token, which a denying prompt cannot ask for); the
    /// tick after the read fetches with octo's token.
    /// </summary>
    [AvaloniaFact]
    public async Task Inside_the_window_the_auto_fetch_waits_for_a_later_tick_which_fetches_with_octo_s_token()
    {
        await using var tab = await HeldRulesTab.OpenAsync(HostingProvider.GitHub, Cancelled);
        var repository = tab.Repository;
        using var scheduler = new AutoFetchScheduler(() => repository, () => false, tab.Services.Settings, TimeProvider.System, tab.Log)
        {
            ReadEnvironment = name => name == AutoFetchScheduler.MinutesVariable ? "10" : null,
            SinceOpening = _ => TimeSpan.FromHours(1),
        };
        tab.Reads.Hold();
        await tab.SignInAsync(Cancelled);
        await HeldRulesTab.WaitForAsync(() => tab.Reads.Waiting == 1, "the tab's held rules read");
        var gitMark = tab.GitRequestsOf().Count;
        var logMark = tab.Log.Lines.Count;

        scheduler.Tick(DateTimeOffset.UtcNow);
        await HeldRulesTab.SettleAsync();
        Assert.Null(repository.Queue.Current);
        Assert.DoesNotContain("hello: auto-fetch started", tab.MessagesSince(logMark));
        Assert.Equal(gitMark, tab.GitRequestsOf().Count);

        tab.Reads.Release();
        await HeldRulesTab.WaitForAsync(() => repository.Hosting.Bindings.Count == 1 && repository.IsWatcherQuiet, "the tab's bindings");
        scheduler.Tick(DateTimeOffset.UtcNow);
        Assert.Contains("hello: auto-fetch started", tab.MessagesSince(logMark));
        await HeldRulesTab.WaitForAsync(() => repository.Queue.Current is null && repository.IsWatcherQuiet, "the auto-fetch's end");

        Assert.Empty(tab.Prompts.Prompts);
        tab.AssertThroughHelper(gitMark, "git-upload-pack");
        Assert.Equal(["hello: Auto-fetch: 1 of 1 URLs bound to the account (Deny)."], tab.MessagesSince(logMark).Where(message => message.Contains(" URLs bound to the account", StringComparison.Ordinal)));
    }

    /// <summary>The push's git requests by git's version (<see cref="HeldRulesTab.AssertThroughHelper"/>'s rule), on GitHub's URL without a user.</summary>
    private static void AssertThroughHelper(ShellDriver app, List<AuthRemoteRequest> requests)
    {
        var version = app.Session.Shell.Git?.Version;
        (string? GivenUserName, int Status)[] first = version >= HeldRulesTab.ProactiveAuthVersion ? [("x-access-token", 200)] : [(null, 401)];
        Assert.True(
            requests.Take(first.Length).Select(request => (request.GivenUserName, request.Status)).SequenceEqual(first),
            $"The push's first requests were {string.Join("; ", requests.Take(first.Length).Select(request => $"{request.Method} {request.Path} as {request.GivenUserName ?? "nobody"} {request.Status}"))}, with git {version}.");
        Assert.All(requests.Skip(first.Length), request => Assert.True(
            request is { GivenUserName: "x-access-token", Status: 200 },
            $"{request.Method} {request.Path} as {request.GivenUserName ?? "nobody"} answered {request.Status} after the first requests, with git {version}."));
        var receive = Assert.Single(requests, request => request.Method == "POST" && request.Path.EndsWith("git-receive-pack", StringComparison.Ordinal));
        Assert.Equal((200, "x-access-token"), (receive.Status, receive.GivenUserName));
    }
}
