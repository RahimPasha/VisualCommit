using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The account's token for git and refusals (docs/phase-7a-design.md 7.6, 7.7, 7.13; rulings 4, 6,
/// D330, D332): git's request by key, state and switch; the API's 401 marking the account at once
/// and for the next start; git's erase marking it only when <c>GET /user</c> then answers 401,
/// checked once at a time; the bindings of a dashboard row and of a clone.
/// </summary>
public sealed class HostingSessionCredentialTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gits_request_gets_the_token_only_for_the_signed_in_account_with_the_switch_on()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        var octo = HostingSessionStartTests.OctoKey(setup);
        await setup.Session.StartAsync(Cancelled);
        Assert.Null(await setup.Session.GetAsync(octo, Cancelled));

        await setup.SignInAsync();

        var credential = await setup.Session.GetAsync(octo, Cancelled);
        Assert.Equal((GitCredential.UserName, setup.OctoToken), (GitCredential.UserName, credential!.Token));
        Assert.Null(await setup.Session.GetAsync(new AccountKey(HostingProvider.GitHub, octo.Host, "ada"), Cancelled));
        Assert.Null(await setup.Session.GetAsync(new AccountKey(HostingProvider.GitHub, "github.com", "octo"), Cancelled));

        setup.Session.SetUseForGit(false);
        await setup.Session.Switching.WaitAsync(Cancelled);
        Assert.Null(await setup.Session.GetAsync(octo, Cancelled));
        Assert.False(Load(setup).UseForGit);

        setup.Session.SetUseForGit(true);
        await setup.Session.Switching.WaitAsync(Cancelled);
        Assert.NotNull(await setup.Session.GetAsync(octo, Cancelled));
        Assert.True(Load(setup).UseForGit);
    }

    [Fact]
    public async Task The_apis_401_marks_the_account_refused_and_the_next_start_reads_no_store()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        setup.Fake.Revoke(setup.OctoToken);

        var listed = await setup.Session.ListRepositoriesAsync(Cancelled);

        Assert.Equal(HostingFailureKind.BadCredentials, listed.Failure!.Kind);
        var account = setup.Session.Account!;
        Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.Refused, (TokenPlace?)null), (account.State, account.NoteKind, account.Place));
        Assert.Null(await setup.Session.GetAsync(account.Account.Key, Cancelled));
        Assert.False(setup.Session.BindsGit);
        Assert.Equal(AccountTokenStates.Refused, Load(setup).TokenState);
        Assert.Contains(setup.Warnings, warning => warning.StartsWith("GitHub account octo on 127.0.0.1:1: GitHub refused the token (the API, request ", StringComparison.Ordinal));

        // Signed out of use: no request at all (D257).
        var requests = setup.Fake.Requests.Count;
        Assert.Equal(("Not signed in.", requests), ((await setup.Session.ListRepositoriesAsync(Cancelled)).Failure!.Message, setup.Fake.Requests.Count));

        var reads = setup.FakeTokens.Calls.Count(call => call.StartsWith("read", StringComparison.Ordinal));
        var next = setup.NewSession();
        await next.StartAsync(Cancelled);
        Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.Refused), (next.Account!.State, next.Account.NoteKind));
        Assert.Equal(reads, setup.FakeTokens.Calls.Count(call => call.StartsWith("read", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Gits_erase_marks_the_account_only_when_github_then_refuses_the_token()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var octo = HostingSessionStartTests.OctoKey(setup);
        var written = File.GetLastWriteTimeUtc(setup.Paths.AccountsFile);
        var file = File.ReadAllText(setup.Paths.AccountsFile);

        // The token is valid: an http.extraHeader or a redirect made git erase it (D330).
        setup.Session.Refused(octo);
        await WaitForWarningsAsync(setup, 1);
        Assert.Equal("GitHub account octo on 127.0.0.1:1: git was refused, yet the token is valid (an http.extraHeader, a proxy or a redirect?).", setup.Warnings[^1]);
        Assert.Equal(AccountState.SignedIn, setup.Session.Account!.State);

        // The network fails: the account stays too.
        setup.NetworkDown = true;
        setup.Session.Refused(octo);
        await WaitForWarningsAsync(setup, 2);
        setup.NetworkDown = false;
        Assert.StartsWith("GitHub account octo on 127.0.0.1:1: git was refused; the token could not be checked: ", setup.Warnings[^1], StringComparison.Ordinal);
        Assert.Equal(AccountState.SignedIn, setup.Session.Account!.State);
        Assert.Equal((written, file), (File.GetLastWriteTimeUtc(setup.Paths.AccountsFile), File.ReadAllText(setup.Paths.AccountsFile)));

        // Another account's erase is ignored without a request.
        var requests = setup.UserRequests;
        setup.Session.Refused(new AccountKey(HostingProvider.GitHub, octo.Host, "ada"));
        Assert.Equal(requests, setup.UserRequests);

        // Revoked: GET /user answers 401 and the account needs signing in, its source git.
        setup.Fake.Revoke(setup.OctoToken);
        setup.Session.Refused(octo);
        await WaitForAsync(() => setup.Session.Account!.State == AccountState.NeedsSignIn);
        Assert.Equal(AccountNoteKind.Refused, setup.Session.Account!.NoteKind);
        Assert.Equal(AccountTokenStates.Refused, Load(setup).TokenState);
        Assert.Contains("GitHub account octo on 127.0.0.1:1: GitHub refused the token (git); it needs signing in again.", setup.Warnings);
    }

    [Fact]
    public async Task Two_erases_while_the_check_is_held_make_one_request()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var octo = HostingSessionStartTests.OctoKey(setup);
        var before = setup.UserRequests;

        using (var hold = setup.Fake.HoldNext("user"))
        {
            setup.Session.Refused(octo);
            await hold.Arrived.WaitAsync(Cancelled);
            setup.Session.Refused(octo);
        }

        await WaitForWarningsAsync(setup, 1);
        Assert.Equal(before + 1, setup.UserRequests);
        Assert.Single(setup.Warnings);
    }

    [Fact]
    public async Task BindsGit_and_the_dashboard_rows_bindings_follow_the_account_and_its_switch()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        RemoteUrl[] urls =
        [
            new("origin", "http://127.0.0.1:1/octo/hello.git", false),
            new("fork", "fake:ada/hello.git", false),
            new("other", "http://127.0.0.1:2/octo/hello.git", false),
        ];
        var rewrites = new UrlRewrites([new UrlRewrite("http://127.0.0.1:1/", "fake:", ForPush: false)]);
        var off = setup.NewSession(off: HostingOptions.Off);
        Assert.Equal((false, 0), (off.BindsGit, off.BindingsFor(urls, rewrites).Count));
        Assert.Empty(off.BindingsForClone("http://127.0.0.1:1/octo/hello.git"));

        await setup.Session.StartAsync(Cancelled);
        Assert.Equal((false, 0), (setup.Session.BindsGit, setup.Session.BindingsFor(urls, rewrites).Count));

        var octo = (await setup.SignInAsync()).Account.Key;

        Assert.True(setup.Session.BindsGit);
        Assert.Equal(
            [new CredentialBinding("http://127.0.0.1:1/octo/hello.git", octo), new CredentialBinding("http://127.0.0.1:1/ada/hello.git", octo)],
            setup.Session.BindingsFor(urls, rewrites));
        Assert.Equal([new CredentialBinding("http://127.0.0.1:1/octo/new.git", octo)], setup.Session.BindingsForClone("http://127.0.0.1:1/octo/new.git"));
        Assert.Empty(setup.Session.BindingsForClone("http://alice@127.0.0.1:1/octo/new.git"));

        setup.Session.SetUseForGit(false);
        await setup.Session.Switching.WaitAsync(Cancelled);
        Assert.Equal((false, 0, 0), (setup.Session.BindsGit, setup.Session.BindingsFor(urls, rewrites).Count, setup.Session.BindingsForClone("http://127.0.0.1:1/octo/new.git").Count));

        setup.Session.SetUseForGit(true);
        await setup.Session.Switching.WaitAsync(Cancelled);
        setup.Fake.Revoke(setup.OctoToken);
        await setup.Session.ListRepositoriesAsync(Cancelled);
        Assert.Equal((false, 0), (setup.Session.BindsGit, setup.Session.BindingsFor(urls, rewrites).Count));
    }

    [Fact]
    public async Task Recognition_and_the_push_guard_go_by_the_configured_url()
    {
        await using var setup = await HostingSessionSetup.StartAsync();

        Assert.Equal("octo/hello", setup.Session.Recognise("http://127.0.0.1:1/octo/hello.git")!.FullName);
        Assert.Null(setup.Session.Recognise("http://127.0.0.1:47391/octo/hello.git"));
        Assert.False(setup.Session.SourceCanExist("http://127.0.0.1:1/octo/hello.git", "refs/pull/fix/head"));
        Assert.True(setup.Session.SourceCanExist("http://127.0.0.1:1/octo/hello.git", "refs/pull/12/head"));
        Assert.True(setup.Session.SourceCanExist("http://127.0.0.1:2/octo/hello.git", "refs/pull/fix/head"));
        Assert.Empty(setup.Fake.Requests);
    }

    private static AccountRecord Load(HostingSessionSetup setup) => Assert.Single(HostingSessionSignInTests.Load(setup).Accounts);

    private static Task WaitForWarningsAsync(HostingSessionSetup setup, int count) => WaitForAsync(() => setup.Warnings.Count >= count);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var limit = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < limit, "The condition did not hold within 30 s.");
            await Task.Delay(10, Cancelled);
        }
    }
}
