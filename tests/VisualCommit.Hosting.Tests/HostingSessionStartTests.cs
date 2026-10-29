using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.Tokens;
using VisualCommit.Testing.Headless;
using VisualCommit.Hosting.Tests.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The hosting session's start (docs/phase-7a-design.md 7.4, 7.13; ruling 6, D287): no record, no
/// store call; each <c>tokenState</c> and read outcome to its state and note; one store read; off
/// reads nothing; <c>AccountChanged</c> once; git's request waiting for a held read, and going
/// without the token after 10 s on the session's clock.
/// </summary>
public sealed class HostingSessionStartTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Without_a_record_for_the_host_no_store_is_touched_and_nothing_is_raised()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        WriteRecord(setup, AccountTokenStates.Keychain, host: "github.com");
        var raised = 0;
        await setup.Thread.RunAsync(() => setup.Session.AccountChanged += (_, _) => raised++);

        await setup.Session.StartAsync(Cancelled);
        await setup.Thread.IdleAsync();

        Assert.Null(setup.Session.Account);
        Assert.Empty(setup.FakeTokens.Calls);
        Assert.Empty(setup.Fake.Requests);
        Assert.Equal(0, raised);
        Assert.Contains(setup.Log.Lines, line => line.Message == "GitHub: 127.0.0.1:1.");
        Assert.Contains(setup.Log.Lines, line => line.Message == "GitHub: no account on 127.0.0.1:1.");
    }

    [Fact]
    public async Task Each_token_state_and_read_outcome_gives_its_state_and_note_with_at_most_one_store_read()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        var key = TokenKey.Of(OctoKey(setup));
        await setup.FakeTokens.SaveAsync(key, setup.OctoToken, Cancelled);

        var cases = new (string TokenState, ITokenStore Store, AccountState State, TokenPlace? Place, AccountNoteKind Note, string? Reason, int Reads)[]
        {
            (AccountTokenStates.Keychain, setup.FakeTokens, AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None, null, 1),
            (AccountTokenStates.Keychain, new FakeTokenStore(), AccountState.NeedsSignIn, null, AccountNoteKind.TokenMissing, null, 1),
            (AccountTokenStates.Keychain, new ScriptedTokenStore { ReadAnswer = new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Locked) }, AccountState.NeedsSignIn, null, AccountNoteKind.ReadFailed, TokenStoreReasons.Locked, 1),
            (AccountTokenStates.Session, new FakeTokenStore(), AccountState.NeedsSignIn, null, AccountNoteKind.SessionEnded, null, 0),
            (AccountTokenStates.Refused, new FakeTokenStore(), AccountState.NeedsSignIn, null, AccountNoteKind.Refused, null, 0),
            ("unknown", setup.FakeTokens, AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None, null, 1),
        };
        foreach (var (tokenState, store, state, place, note, reason, reads) in cases)
        {
            WriteRecord(setup, tokenState);
            var before = Reads(store);
            var session = setup.NewSession(store);

            await session.StartAsync(Cancelled);
            await session.StartAsync(Cancelled);

            var account = session.Account!;
            Assert.Equal((state, place, note, reason), (account.State, account.Place, account.NoteKind, account.Note));
            Assert.Equal(("octo", 1001L, "Octo Cat", false), (account.Account.Login, account.Account.Id, account.Account.Name, account.Account.UseForGit));
            Assert.Equal(reads, Reads(store) - before);
            Assert.False(session.BindsGit);
            session.Dispose();
        }

        // No request to GitHub at any start (D287).
        Assert.Empty(setup.Fake.Requests);
    }

    [Fact]
    public async Task Off_reads_nothing_asks_nothing_and_logs_that_github_is_off()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        WriteRecord(setup, AccountTokenStates.Keychain);
        var session = setup.NewSession(off: HostingOptions.Off);

        await session.StartAsync(Cancelled);
        var signIn = await session.SignInAsync(setup.OctoToken, Cancelled);

        Assert.True(session.IsOff);
        Assert.Null(session.Account);
        Assert.Null(session.GitHub);
        Assert.False(session.BindsGit);
        Assert.Same(NullRepositoryHosting.Instance, session.ForRepository(setup.Data.Path));
        Assert.Null(await session.GetAsync(OctoKey(setup), Cancelled));
        Assert.Equal((HostingFailureKind.Validation, "GitHub is turned off."), (signIn.Failure!.Kind, signIn.Failure.Message));
        Assert.Equal(HostingFailureKind.Validation, (await session.ListRepositoriesAsync(Cancelled)).Failure!.Kind);
        Assert.Null(await session.SignOutAsync(Cancelled));
        Assert.False(await session.KeychainAvailableAsync(Cancelled));
        Assert.True(session.SourceCanExist("http://127.0.0.1:1/octo/hello.git", "refs/pull/x/head"));
        Assert.Null(session.Recognise("http://127.0.0.1:1/octo/hello.git"));
        Assert.Empty(setup.FakeTokens.Calls);
        Assert.Empty(setup.Fake.Requests);
        Assert.Contains(setup.Log.Lines, line => line.Message == "GitHub: off.");
    }

    [Fact]
    public async Task A_start_with_an_account_raises_AccountChanged_once_on_the_post_thread()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        WriteRecord(setup, AccountTokenStates.Session);
        var raised = new List<bool>();
        await setup.Thread.RunAsync(() => setup.Session.AccountChanged += (_, _) => raised.Add(setup.Thread.IsCurrent));

        await setup.Session.StartAsync(Cancelled);
        await setup.Thread.IdleAsync();

        Assert.Equal([true], raised);
        Assert.Contains(setup.Log.Lines, line => line.Level == LogLevel.Info && line.Message == "GitHub account octo on 127.0.0.1:1: its token was kept for the last session only; it needs signing in.");
    }

    [Fact]
    public async Task Gits_request_waits_for_a_held_read_and_goes_without_the_token_after_ten_seconds()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        WriteRecord(setup, AccountTokenStates.Keychain, useForGit: true);
        var key = OctoKey(setup);
        await setup.FakeTokens.SaveAsync(TokenKey.Of(key), setup.OctoToken, Cancelled);
        await setup.Session.KeychainAvailableAsync(Cancelled);
        var hold = setup.FakeTokens.HoldNext();

        var start = Task.Run(() => setup.Session.StartAsync(Cancelled), Cancelled);
        await hold.Arrived.WaitAsync(Cancelled);
        var early = setup.Session.GetAsync(key, Cancelled);
        var timers = setup.Clock.ArmedTimers;
        var late = setup.Session.GetAsync(key, Cancelled);
        await WaitUntilAsync(() => setup.Clock.ArmedTimers >= timers + 1);
        Assert.False(early.IsCompleted);

        setup.Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Null(await early.WaitAsync(Cancelled));
        Assert.Null(await late.WaitAsync(Cancelled));
        Assert.Contains("GitHub: the keychain's read had not ended after 10 s; git's request for 127.0.0.1:1 went without the account's token.", setup.Warnings);

        var waiting = setup.Session.GetAsync(key, Cancelled);
        await WaitUntilAsync(() => setup.Clock.ArmedTimers >= 1);
        hold.Dispose();
        await start.WaitAsync(Cancelled);

        var credential = await waiting.WaitAsync(Cancelled);
        Assert.Equal(setup.OctoToken, credential!.Token);
        Assert.Equal("GitCredential(***)", credential.ToString());
        Assert.Equal(AccountState.SignedIn, setup.Session.Account!.State);
    }

    internal static AccountKey OctoKey(HostingSessionSetup setup) => new(HostingProvider.GitHub, setup.Fake.Endpoints.Host, "octo");

    internal static void WriteRecord(HostingSessionSetup setup, string tokenState, string? host = null, bool useForGit = false) =>
        new JsonAccountStore(setup.Paths.AccountsFile, setup.Log).Save(new AccountsFile
        {
            Accounts =
            [
                new AccountRecord
                {
                    Provider = AccountRecord.GitHubProvider,
                    Host = host ?? setup.Fake.Endpoints.Host,
                    Login = "octo",
                    Id = 1001,
                    Name = "Octo Cat",
                    UseForGit = useForGit,
                    TokenState = tokenState,
                },
            ],
        });

    private static int Reads(ITokenStore store) => store switch
    {
        FakeTokenStore fake => fake.Calls.Count(call => call.StartsWith("read ", StringComparison.Ordinal)),
        ScriptedTokenStore scripted => scripted.CountOf("read"),
        _ => throw new ArgumentException("An unknown store.", nameof(store)),
    };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var limit = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < limit, "The condition did not hold within 30 s.");
            await Task.Delay(10, Cancelled);
        }
    }
}
