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
/// (<see cref="ShellDriver"/>; not always inside the window); the same with the tab's read held and
/// failed, a fetch right after a sign-in held and failed, a remote the rules give another user bound
/// to no token, and the auto-fetch, which waits for a later tick, on a tab built by hand whose reads
/// the test holds or fails (<see cref="HeldRulesTab"/>). Each binds octo's token: git's requests by
/// git's version (part B's convention), no prompt. On GitLab and Azure DevOps, whose hosting changes
/// no more once the list has answered, the auto-fetch after the tab's read failed: the due tick
/// asks for the read, at most once an interval, and the next fetches with the account's token. On
/// 0.4's lagged list.
/// </summary>
public sealed class SignInBindingsWindowTests
{
    /// <summary>The interval of <see cref="DueScheduler"/>'s auto-fetch.</summary>
    private static readonly TimeSpan DueInterval = TimeSpan.FromMinutes(10);

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    /// <summary>
    /// The hunt's case (WP-N hunt 3, the tokens lens): signed out, Create on <c>local-work</c>, the
    /// stacked sign-in in GitHub's dialog, Create as soon as the form has its base. Before D388 a push
    /// started inside D290's window copied the tab's empty bindings, git asked for a user name and the
    /// create failed. Through the app the press does not always come inside the window: the tab's
    /// read may answer before the push starts (in WP-N's product review 2 of 15 runs of the three
    /// at-once tests did not reach it), and such a run passes on the code before D388 too. So this
    /// proves that the at-once flow pushes with octo's token; D388's proof is the held and failed
    /// variants (<see cref="Signed_out_a_create_waits_for_the_held_rules_read_or_reads_them_itself_when_it_failed"/>),
    /// which are always inside the window.
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

        // Git asked the app's helper, and the broker logged its answer (what D278's case asserts it never logs).
        Assert.Contains(messages.Where(IsBrokerCredentialLine), message => message.StartsWith("hello: Fetch origin: git's credential get for 127.0.0.1:", StringComparison.Ordinal) && message.EndsWith(" answered from the account.", StringComparison.Ordinal));

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
    /// reads the rules itself after it failed, it binds the URL git uses, bob's, to nothing: git reads
    /// origin as bob's URL; the fake sees the fetch's request without credentials and libcurl's retry
    /// as bob with an empty password, both refused, none with <c>x-access-token</c>; no credential
    /// request of git's reaches the app's broker; and git's one prompt, refused, ends the fetch.
    /// Bindings made from the unrewritten URL would have answered bob's with octo's token.
    /// <para>
    /// The prompt's text depends on the git that runs, not on the app, so the test asks
    /// <see cref="GitUrlUser"/> (measured once per process) which one to expect. A git that keeps a
    /// URL's user after a refusal asks bob's password: <c>Password for 'http://bob@127.0.0.1:&lt;port&gt;': </c>.
    /// Git v2.56.0 and older without git's 76524c2604bc, over a libcurl that strips the credentials
    /// from <c>CURLINFO_EFFECTIVE_URL</c> (curl 8.23.0, CVE-2026-94266's fix), takes its own request
    /// for a redirect and loses bob: <c>Username for 'http://127.0.0.1:&lt;port&gt;': </c>, as Homebrew's
    /// 2.56.0_2 did on CI's macOS (run 42437572741). Either way the retry as bob shows that git
    /// fetched bob's URL.
    /// </para>
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inside_the_window_a_remote_the_rules_give_another_user_is_bound_to_no_token(bool readFails)
    {
        string authority = null!, bob = null!;
        await using var tab = await HeldRulesTab.OpenAsync(HostingProvider.GitHub, Cancelled, async (clone, webBase) =>
        {
            authority = webBase.GetLeftPart(UriPartial.Authority);
            bob = new UriBuilder(webBase) { UserName = "bob" }.Uri.GetLeftPart(UriPartial.Authority);
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

        // Git's own reading of origin's URL goes through the user's rule: bob's.
        Assert.Equal($"{bob}/octo/hello.git", (await tab.Clone.GitAsync("remote", "get-url", "origin")).StandardOutput.Trim());

        // The fetch's requests: without credentials, then libcurl's retry with the URL's user, bob, and
        // an empty password (docs/phase-7b-design.md 8.6, H2), each refused; git's prompt ends the fetch.
        (string? GivenUserName, string? UserName, int Status)[] expectedRequests = [(null, null, 401), ("bob", null, 401)];
        Assert.Equal(expectedRequests, requests.Select(request => (request.GivenUserName, request.UserName, request.Status)));

        // Git's one prompt: bob's password, or on a git that loses a URL's user after a refusal a user
        // name for the bare origin (GitUrlUser, measured).
        var urlUser = await GitUrlUser.AfterRefusalAsync();
        var expectedPrompt = urlUser == UrlUserAfterRefusal.Kept ? $"Password for '{bob}': " : $"Username for '{authority}': ";
        var prompt = Assert.Single(tab.Prompts.Prompts);
        Assert.True(
            prompt.Text == expectedPrompt,
            $"Git's prompt was \"{prompt.Text}\" where \"{expectedPrompt}\" was expected: the git that runs ({tab.Version}) measures {urlUser} for a URL's user after a refusal (GitUrlUser).");

        // No credential request of git's reached the app's broker: the scope had no key for git to match.
        Assert.DoesNotContain(tab.MessagesSince(logMark), IsBrokerCredentialLine);
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

    /// <summary>
    /// The auto-fetch inside the window after the tab's only read since the sign-in failed, once
    /// GitLab's or Azure DevOps' list had answered: their hosting changes no more, so nothing else
    /// reads the rules again and the window stays open by itself (WP-N's product review, finding 1).
    /// The due tick starts no fetch and asks the tab for its read (one
    /// <c>git config --null --get-regexp ^url\.</c>), whose answer gives the bindings; the next tick
    /// fetches with the account's token. Before, every tick waited until a refs change.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(HostingProvider.GitLab)]
    [InlineData(HostingProvider.AzureDevOps)]
    public async Task Inside_the_window_after_a_failed_read_the_due_tick_reads_the_rules_and_the_next_fetches_with_the_token(HostingProvider provider)
    {
        await using var tab = await HeldRulesTab.OpenAsync(provider, Cancelled);
        var repository = tab.Repository;
        var name = repository.Name;
        using var scheduler = DueScheduler(tab);

        // The tab's only read since the sign-in, held until the list has answered, then failed.
        tab.Reads.Hold(failTabReadOnRelease: true);
        await tab.SignInAsync(Cancelled);
        await HeldRulesTab.WaitForAsync(() => tab.Reads.Waiting == 1, "the tab's held rules read");

        // Not quiet while the read is held (IsWatcherQuiet waits for it).
        await HeldRulesTab.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.Loaded, "the list");
        await HeldRulesTab.SettleAsync();
        var logMark = tab.Log.Lines.Count;
        tab.Reads.Release();
        await HeldRulesTab.WaitForAsync(() => tab.MessagesSince(logMark).Contains(FailedOf(name)) && repository.IsWatcherQuiet, "the tab's failed rules read");
        await HeldRulesTab.SettleAsync();
        var reads = tab.Reads.Reads;
        var gitMark = tab.GitRequestsOf().Count;
        var callMark = tab.CallMark();
        Assert.Empty(repository.Hosting.Bindings);

        // The due tick: no fetch, the tab's read asked for.
        var now = DateTimeOffset.UtcNow;
        scheduler.Tick(now);
        Assert.Null(repository.Queue.Current);
        Assert.Equal(reads + 1, tab.Reads.Reads);
        await HeldRulesTab.WaitForAsync(() => repository.Hosting.Bindings.Count == 1 && repository.IsWatcherQuiet, "the tab's bindings");
        Assert.Equal(gitMark, tab.GitRequestsOf().Count);
        Assert.Equal([$"{name}: auto-fetch not started, the URL rules are read again"], tab.MessagesSince(logMark).Where(message => message.Contains("auto-fetch", StringComparison.Ordinal)));

        // The next tick fetches with the account's token.
        scheduler.Tick(now + AutoFetchScheduler.TickInterval);
        Assert.Contains($"{name}: auto-fetch started", tab.MessagesSince(logMark));
        await HeldRulesTab.WaitForAsync(() => repository.Queue.Current is null && repository.IsWatcherQuiet, "the auto-fetch's end");
        Assert.Empty(tab.Prompts.Prompts);
        tab.AssertThroughHelper(gitMark, "git-upload-pack");
        Assert.Equal([$"{name}: Auto-fetch: 1 of 1 URLs bound to the account (Deny)."], tab.MessagesSince(logMark).Where(message => message.Contains(" URLs bound to the account", StringComparison.Ordinal)));

        // Before the fetch's own calls one rules read ran git: the one the tick asked for.
        Assert.Equal(1, tab.CallsSince(callMark).TakeWhile(call => call.OperationId is null).Count(call => call.CommandText == HeldRulesTab.RulesRead));
    }

    /// <summary>
    /// The same window while every read of the tab fails (<see cref="RulesReads.FailTabReads"/>): the
    /// due tick asks for one read, the ticks of the next interval ask for none, and the first tick an
    /// interval later asks again; none starts a fetch, so nothing reaches the fake. The ticks' reads
    /// come no oftener than the auto-fetch's own fetches would.
    /// </summary>
    [AvaloniaFact]
    public async Task Inside_the_window_with_the_reads_failing_the_tick_reads_the_rules_once_an_interval_and_fetches_nothing()
    {
        await using var tab = await HeldRulesTab.OpenAsync(HostingProvider.GitLab, Cancelled);
        var repository = tab.Repository;
        var name = repository.Name;
        using var scheduler = DueScheduler(tab);
        tab.Reads.FailTabReads();
        await tab.SignInAsync(Cancelled);
        await HeldRulesTab.WaitForAsync(() => repository.HostingState.PullRequests == PullRequestsState.Loaded && repository.IsWatcherQuiet, "the list");
        await HeldRulesTab.WaitForAsync(() => tab.MessagesSince(0).Contains(FailedOf(name)), "the tab's failed rules read");
        await HeldRulesTab.SettleAsync();
        var reads = tab.Reads.Reads;
        var gitMark = tab.GitRequestsOf().Count;
        var logMark = tab.Log.Lines.Count;

        var now = DateTimeOffset.UtcNow;
        scheduler.Tick(now);
        await HeldRulesTab.WaitForAsync(() => tab.MessagesSince(logMark).Count(message => message == FailedOf(name)) == 1, "the read the tick asked for, failed");
        Assert.Equal(reads + 1, tab.Reads.Reads);
        for (var tick = AutoFetchScheduler.TickInterval; tick < DueInterval; tick += AutoFetchScheduler.TickInterval)
        {
            scheduler.Tick(now + tick);
        }

        await HeldRulesTab.SettleAsync();
        Assert.Equal(reads + 1, tab.Reads.Reads);
        scheduler.Tick(now + DueInterval);
        await HeldRulesTab.WaitForAsync(() => tab.MessagesSince(logMark).Count(message => message == FailedOf(name)) == 2, "the read of the next interval, failed");
        Assert.Equal(reads + 2, tab.Reads.Reads);

        Assert.Null(repository.Queue.Current);
        Assert.Empty(repository.Hosting.Bindings);
        Assert.Equal(gitMark, tab.GitRequestsOf().Count);
        var lines = tab.MessagesSince(logMark);
        Assert.DoesNotContain($"{name}: auto-fetch started", lines);
        Assert.Equal(2, lines.Count(message => message == $"{name}: auto-fetch not started, the URL rules are read again"));
    }

    private static string FailedOf(string name) => $"{name}: the URL rewrites could not be read; the last ones read are kept.";

    /// <summary>
    /// Whether a log line is the app's askpass broker's about a credential request of git's (its
    /// helper's credential mode, docs/phase-7a-design.md 4.6; <c>AskPassBroker.AnswerCredentialAsync</c>):
    /// a request refused for a wrong token or for no open scope bound to an account
    /// (<c>A credential request for …</c>), a scope's every line about a get or an erase
    /// (<c>hello: Fetch origin: git's credential get for 127.0.0.1:&lt;port&gt; …</c>), and a refused token's mark.
    /// </summary>
    private static bool IsBrokerCredentialLine(string message) =>
        message.Contains(": git's credential ", StringComparison.Ordinal)
        || message.StartsWith("A credential request for ", StringComparison.Ordinal)
        || message.StartsWith("The prompt channel received a credential request ", StringComparison.Ordinal)
        || message.EndsWith(" refused the account's token.", StringComparison.Ordinal);

    /// <summary>An auto-fetch scheduler on the tab, every 10 minutes, whose first tick is due (the tab opened an hour ago).</summary>
    private static AutoFetchScheduler DueScheduler(HeldRulesTab tab)
    {
        var repository = tab.Repository;
        return new AutoFetchScheduler(() => repository, () => false, tab.Services.Settings, TimeProvider.System, tab.Log)
        {
            ReadEnvironment = name => name == AutoFetchScheduler.MinutesVariable ? "10" : null,
            SinceOpening = _ => TimeSpan.FromHours(1),
        };
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
