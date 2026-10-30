using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.Tests.Tokens;
using VisualCommit.Hosting.Tokens;
using VisualCommit.Testing;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// Sign-in, sign-out and the switch (docs/phase-7a-design.md 7.5, 7.13; D253, D256, D263, D288,
/// rulings 18, 19): one <c>GET /user</c> with the token only in <c>Authorization</c>; no keychain;
/// a failed save; a refused token and a network failure changing nothing; a cancellation before
/// GitHub's answer and after it; another login replacing the account; the switch carried; signing
/// out with and without a kept entry; sign-in and sign-out one at a time; the switch and the
/// sign-out writing off the caller's thread; a sign-out completing when the session closes.
/// </summary>
public sealed class HostingSessionSignInTests
{
    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_sign_in_asks_github_once_keeps_the_token_in_the_keychain_and_writes_the_record()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        var raised = 0;
        await setup.Thread.RunAsync(() => setup.Session.AccountChanged += (_, _) => raised++);

        var status = await setup.SignInAsync();
        await setup.Thread.IdleAsync();

        Assert.Equal((AccountState.SignedIn, TokenPlace.Keychain, AccountNoteKind.None, (string?)null), (status.State, status.Place, status.NoteKind, status.Note));
        Assert.Equal(("octo", 1001L, "Octo Cat", true), (status.Account.Login, status.Account.Id, status.Account.Name, status.Account.UseForGit));
        Assert.Equal(new AccountKey(HostingProvider.GitHub, "127.0.0.1:1", "octo"), status.Account.Key);
        Assert.Same(status, setup.Session.Account);
        Assert.True(setup.Session.BindsGit);
        Assert.Equal(1, raised);
        var sent = Assert.Single(setup.Sent);
        Assert.Equal(("GET", "/api/v3/user", "Bearer " + setup.OctoToken), (sent.Method, sent.Url.AbsolutePath, sent.Header("Authorization")));
        Assert.All(sent.Headers.Where(header => header.Key != "Authorization"), header => Assert.DoesNotContain(setup.OctoToken, header.Value, StringComparison.Ordinal));
        Assert.DoesNotContain(setup.OctoToken, sent.Url.ToString(), StringComparison.Ordinal);
        Assert.True(setup.FakeTokens.Holds(TokenKey.Of(status.Account.Key)));
        var record = Assert.Single(Load(setup).Accounts);
        Assert.Equal(("github", "127.0.0.1:1", "octo", AccountTokenStates.Keychain, true), (record.Provider, record.Host, record.Login, record.TokenState, record.UseForGit));
        Assert.Contains("GitHub account octo on 127.0.0.1:1: signed in; the token is kept in the keychain.", setup.Log.Lines.Select(line => line.Message));
    }

    [Fact]
    public async Task Without_a_keychain_the_token_is_kept_for_the_session_and_a_failed_save_says_why()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        setup.FakeTokens.Available = false;
        await setup.Session.StartAsync(Cancelled);

        var noKeychain = await setup.Session.SignInAsync(setup.OctoToken, Cancelled);

        Assert.Equal((TokenPlace.Session, AccountNoteKind.NoKeychain, (string?)null, (string?)null),
            (noKeychain.Status!.Place, noKeychain.Status.NoteKind, noKeychain.Status.Note, noKeychain.SaveFailedReason));
        Assert.Equal(AccountTokenStates.Session, Load(setup).Accounts[0].TokenState);
        Assert.True(setup.Session.BindsGit);

        var other = setup.NewSession();
        await other.StartAsync(Cancelled);
        Assert.Equal((AccountState.NeedsSignIn, AccountNoteKind.SessionEnded), (other.Account!.State, other.Account.NoteKind));

        setup.FakeTokens.Available = true;
        setup.FakeTokens.FailNextSave(TokenStoreReasons.Locked);
        var failed = await other.SignInAsync(setup.Served.OctoClassicToken, Cancelled);

        Assert.Equal((AccountState.SignedIn, TokenPlace.Session, AccountNoteKind.SaveFailed, TokenStoreReasons.Locked, TokenStoreReasons.Locked),
            (failed.Status!.State, failed.Status.Place, failed.Status.NoteKind, failed.Status.Note, failed.SaveFailedReason));
        Assert.Equal(AccountTokenStates.Session, Load(setup).Accounts[0].TokenState);
    }

    [Fact]
    public async Task A_refused_token_and_a_network_failure_change_nothing_and_never_mark_the_current_account()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        var status = await setup.SignInAsync();
        var file = File.ReadAllText(setup.Paths.AccountsFile);

        var refused = await setup.Session.SignInAsync("github_pat_FAKE-nobody-9", Cancelled);
        setup.NetworkDown = true;
        var unreached = await setup.Session.SignInAsync(setup.Served.AdaToken, Cancelled);
        setup.NetworkDown = false;

        Assert.Equal((HostingFailureKind.BadCredentials, (AccountStatus?)null), (refused.Failure!.Kind, refused.Status));
        Assert.Equal(HostingFailureKind.Network, unreached.Failure!.Kind);
        Assert.Same(status, setup.Session.Account);
        Assert.Equal(file, File.ReadAllText(setup.Paths.AccountsFile));
        Assert.True(setup.FakeTokens.Holds(TokenKey.Of(status.Account.Key)));
    }

    [Fact]
    public async Task A_cancellation_before_githubs_answer_changes_nothing_and_after_it_the_sign_in_completes()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.Session.StartAsync(Cancelled);
        using (var hold = setup.Fake.HoldNext("user"))
        using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancelled))
        {
            var early = setup.Session.SignInAsync(setup.OctoToken, cancel.Token);
            await hold.Arrived.WaitAsync(Cancelled);
            await cancel.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => early);
        }

        Assert.Null(setup.Session.Account);
        Assert.False(File.Exists(setup.Paths.AccountsFile));
        Assert.DoesNotContain(setup.FakeTokens.Calls, call => call.StartsWith("save", StringComparison.Ordinal));

        // The keychain's probe is made first, so that the hold takes the save.
        await setup.Session.KeychainAvailableAsync(Cancelled);
        using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancelled))
        {
            var storeHold = setup.FakeTokens.HoldNext();
            var late = setup.Session.SignInAsync(setup.OctoToken, cancel.Token);
            await storeHold.Arrived.WaitAsync(Cancelled);
            await cancel.CancelAsync();
            storeHold.Dispose();

            var result = await late.WaitAsync(Cancelled);
            Assert.Equal(AccountState.SignedIn, result.Status!.State);
        }

        Assert.Equal(AccountTokenStates.Keychain, Load(setup).Accounts[0].TokenState);
    }

    [Fact]
    public async Task Another_login_forgets_the_old_key_and_the_switch_is_carried()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        var octo = await setup.SignInAsync();
        setup.Session.SetUseForGit(false);
        await setup.Session.Switching.WaitAsync(Cancelled);
        Assert.False(setup.Session.Account!.Account.UseForGit);
        Assert.False(setup.Session.BindsGit);

        var again = await setup.Session.SignInAsync(setup.Served.OctoClassicToken, Cancelled);
        Assert.False(again.Status!.Account.UseForGit);
        Assert.DoesNotContain(setup.FakeTokens.Calls, call => call.StartsWith("delete", StringComparison.Ordinal));

        var ada = await setup.Session.SignInAsync(setup.Served.AdaToken, Cancelled);

        // The switch is the previous record's, whoever signs in (7.5 step 4): one account, one setting.
        Assert.Equal(("ada", false), (ada.Status!.Account.Login, ada.Status.Account.UseForGit));
        Assert.Contains("delete github:127.0.0.1:1:octo", setup.FakeTokens.Calls);
        Assert.False(setup.FakeTokens.Holds(TokenKey.Of(octo.Account.Key)));
        Assert.True(setup.FakeTokens.Holds(TokenKey.Of(ada.Status.Account.Key)));
        Assert.Equal(["ada"], Load(setup).Accounts.Select(record => record.Login));
    }

    [Fact]
    public async Task A_sign_out_forgets_the_token_and_the_record_also_when_the_keychain_keeps_its_entry()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        var octo = await setup.SignInAsync();
        var raised = 0;
        await setup.Thread.RunAsync(() => setup.Session.AccountChanged += (_, _) => raised++);

        Assert.Null(await setup.Session.SignOutAsync(Cancelled));
        await setup.Thread.IdleAsync();

        Assert.Null(setup.Session.Account);
        Assert.False(setup.Session.BindsGit);
        Assert.Equal(1, raised);
        Assert.False(setup.FakeTokens.Holds(TokenKey.Of(octo.Account.Key)));
        Assert.Empty(Load(setup).Accounts);
        Assert.Null(await setup.Session.SignOutAsync(Cancelled));

        var scripted = new ScriptedTokenStore { DeleteAnswer = new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Locked) };
        var session = setup.NewSession(scripted);
        await session.StartAsync(Cancelled);
        Assert.Null((await session.SignInAsync(setup.OctoToken, Cancelled)).Failure);

        Assert.Equal(TokenStoreReasons.Locked, await session.SignOutAsync(Cancelled));
        Assert.Null(session.Account);
        Assert.Empty(Load(setup).Accounts);
        Assert.Contains(setup.Log.Lines, line => line.Message.EndsWith("signed out; the keychain kept its entry: " + TokenStoreReasons.Locked + ".", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_sign_in_and_a_sign_out_run_one_at_a_time()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.Session.StartAsync(Cancelled);
        await setup.Session.KeychainAvailableAsync(Cancelled);
        var storeHold = setup.FakeTokens.HoldNext();

        var signIn = setup.Session.SignInAsync(setup.OctoToken, Cancelled);
        await storeHold.Arrived.WaitAsync(Cancelled);
        var signOut = setup.Session.SignOutAsync(Cancelled);
        await Task.Delay(100, Cancelled);
        Assert.False(signOut.IsCompleted);
        storeHold.Dispose();

        Assert.Equal(AccountState.SignedIn, (await signIn).Status!.State);
        Assert.Null(await signOut);
        Assert.Null(setup.Session.Account);
        Assert.Equal(["probe", "save github:127.0.0.1:1:octo", "delete github:127.0.0.1:1:octo"], setup.FakeTokens.Calls);
        Assert.Empty(Load(setup).Accounts);
    }

    [Fact]
    public async Task A_sign_out_deletes_the_images_the_index_and_the_maps_also_with_an_image_held()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await RepositoryHostingAvatarsTests.OpenHelloAsync(setup);
        await RepositoryHostingAvatarsTests.ShowAsync(setup, hosting, Scenarios.OctoEmail);
        Assert.True(File.Exists(Path.Combine(setup.Paths.AvatarsDirectory, AvatarCache.IndexFileName)));
        Assert.True(File.Exists(Path.Combine(setup.Paths.AvatarsDirectory, "authors", "github_127.0.0.1_1_octo_hello.json")));
        Assert.True(File.Exists(Path.Combine(setup.Paths.AvatarsDirectory, "1001.png")));

        Task abandoned;
        using (var hold = setup.Fake.HoldNext("avatar:1003"))
        {
            abandoned = hold.Abandoned;
            var run = await setup.Thread.RunAsync<Task>(() =>
            {
                hosting.ShowAvatarsFor([Scenarios.OctoEmail, Scenarios.AdaEmail]);
                return hosting.AvatarRun;
            });
            await hold.Arrived.WaitAsync(Cancelled);

            Assert.Null(await setup.Session.SignOutAsync(Cancelled));

            Assert.True(run.IsCompleted);
            Assert.False(Directory.Exists(setup.Paths.AvatarsDirectory));
        }

        // The held download gave its request up, so no image of it can come after the sign-out.
        await setup.Thread.IdleAsync();
        await abandoned.WaitAsync(TimeSpan.FromSeconds(60), Cancelled);
        Assert.False(Directory.Exists(setup.Paths.AvatarsDirectory));
        Assert.Same(AuthorAvatars.None, hosting.Avatars);
    }

    [Fact]
    public async Task The_switch_and_a_sign_out_without_a_keychain_write_off_the_callers_thread()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        setup.FakeTokens.Available = false;
        var log = new ThreadLog();
        var session = setup.NewSession(log: log);
        await session.StartAsync(Cancelled);
        Assert.Null((await session.SignInAsync(setup.OctoToken, Cancelled)).Failure);
        var caller = await setup.Thread.RunAsync(() => Environment.CurrentManagedThreadId);

        // The UI thread at Settings' Save and at the dashboard's "Sign out": the vault answers at
        // once without a keychain, so only the session itself keeps the writes off it.
        await setup.Thread.RunAsync(() => session.SetUseForGit(false));
        await session.Switching.WaitAsync(Cancelled);
        Assert.Null(await setup.Thread.RunAsync(() => session.SignOutAsync(Cancelled)));

        Assert.Empty(Load(setup).Accounts);
        Assert.NotEqual(caller, log.ThreadOf("GitHub account octo on 127.0.0.1:1: not used for git over HTTPS."));
        Assert.NotEqual(caller, log.ThreadOf("GitHub account octo on 127.0.0.1:1: signed out."));
    }

    [Fact]
    public async Task A_sign_out_begun_completes_when_the_session_closes_meanwhile()
    {
        await using var setup = await HostingSessionSetup.StartAsync();
        await setup.SignInAsync();
        var hosting = await RepositoryHostingAvatarsTests.OpenHelloAsync(setup);
        await RepositoryHostingAvatarsTests.ShowAsync(setup, hosting, Scenarios.OctoEmail);
        Assert.True(Directory.Exists(setup.Paths.AvatarsDirectory));

        Task<string?> signOut;
        using (var storeHold = setup.FakeTokens.HoldNext())
        {
            signOut = setup.Session.SignOutAsync(Cancelled);
            await storeHold.Arrived.WaitAsync(Cancelled);
            setup.Session.Dispose();
        }

        Assert.Null(await signOut.WaitAsync(Cancelled));
        Assert.Equal(["probe", "save github:127.0.0.1:1:octo", "delete github:127.0.0.1:1:octo"], setup.FakeTokens.Calls);
        Assert.Empty(Load(setup).Accounts);
        Assert.False(Directory.Exists(setup.Paths.AvatarsDirectory));
    }

    internal static AccountsFile Load(HostingSessionSetup setup) => new JsonAccountStore(setup.Paths.AccountsFile, new ListLog()).Load();

    /// <summary>A log that keeps the thread each line was written on.</summary>
    private sealed class ThreadLog : IAppLog
    {
        private readonly List<(string Message, int Thread)> _lines = [];

        /// <summary>The thread of the one line <paramref name="message"/>.</summary>
        public int ThreadOf(string message)
        {
            lock (_lines)
            {
                return _lines.Single(line => line.Message == message).Thread;
            }
        }

        public void Write(LogLevel level, string message, Exception? exception = null)
        {
            lock (_lines)
            {
                _lines.Add((message, Environment.CurrentManagedThreadId));
            }
        }
    }
}
