using VisualCommit.Core.Logging;
using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// <see cref="TokenVault"/> (docs/phase-7a-design.md 3.7, D272) over a scripted store whose calls
/// can be held: where a token is kept, how often the store is probed and read, and what a cancel
/// stops.
/// </summary>
public sealed class TokenVaultTests
{
    private const string Token = "ghp_FAKE-octocat-1";
    private const string OlderToken = "ghp_FAKE-octocat-0";

    private static readonly TokenKey Octocat = new("github", "github.com", "octocat");

    private readonly ScriptedTokenStore _store = new();
    private readonly ListLog _log = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task With_a_keychain_the_token_is_saved_there_and_read_from_memory()
    {
        var vault = new TokenVault(_store, _log);

        var kept = await vault.KeepAsync(Octocat, Token, _ct);
        var read = await vault.ReadAsync(Octocat, _ct);

        Assert.Equal((TokenPlace.Keychain, (string?)null), kept);
        Assert.Equal((TokenStoreOutcome.Done, Token), (read.Outcome, read.Token));
        Assert.Equal(1, _store.CountOf("save"));
        Assert.Equal(0, _store.CountOf("read"));
        Assert.Contains((LogLevel.Debug, "Token for github.com/octocat kept in the keychain."), _log.Lines);
        AssertNoTokenInTheLog();
    }

    [Fact]
    public async Task Without_a_keychain_the_token_is_kept_for_the_session_and_no_save_is_tried()
    {
        _store.Probe = new TokenStoreProbe(false, TokenStoreReasons.NoKeychain);
        var vault = new TokenVault(_store, _log);

        var kept = await vault.KeepAsync(Octocat, Token, _ct);
        var read = await vault.ReadAsync(Octocat, _ct);

        Assert.Equal((TokenPlace.Session, TokenStoreReasons.NoKeychain), kept);
        Assert.Equal(Token, read.Token);
        Assert.Equal(0, _store.CountOf("save"));
        Assert.False(await vault.KeychainAvailableAsync(_ct));
        Assert.Contains(
            (LogLevel.Info, "Token for github.com/octocat kept for this session: no keychain was found on this computer."),
            _log.Lines);
        AssertNoTokenInTheLog();
    }

    [Fact]
    public async Task A_failed_save_keeps_the_token_readable_with_its_reason_and_leaves_the_store_alone()
    {
        _store.SaveAnswer = new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Locked);
        var vault = new TokenVault(_store, _log);

        var kept = await vault.KeepAsync(Octocat, Token, _ct);
        var read = await vault.ReadAsync(Octocat, _ct);

        Assert.Equal((TokenPlace.Session, TokenStoreReasons.Locked), kept);
        Assert.Equal((TokenStoreOutcome.Done, Token), (read.Outcome, read.Token));
        Assert.Equal(["probe", "save"], _store.Calls.Select(c => c.Call));
        AssertNoTokenInTheLog();
    }

    [Fact]
    public async Task A_save_that_throws_is_a_refusal_kept_for_the_session()
    {
        var vault = new TokenVault(new ThrowingStore(), _log);

        var kept = await vault.KeepAsync(Octocat, Token, _ct);

        Assert.Equal((TokenPlace.Session, TokenStoreReasons.Refused), kept);
        Assert.Equal(Token, (await vault.ReadAsync(Octocat, _ct)).Token);
        AssertNoTokenInTheLog();
    }

    [Fact]
    public async Task Memory_is_asked_before_the_store()
    {
        _store.Put(Octocat, OlderToken);
        var vault = new TokenVault(_store, _log);

        await vault.KeepAsync(Octocat, Token, _ct);
        var read = await vault.ReadAsync(Octocat, _ct);

        Assert.Equal(Token, read.Token);
        Assert.Equal(0, _store.CountOf("read"));
    }

    [Fact]
    public async Task The_store_is_read_once_per_key_and_run_when_it_has_the_token()
    {
        _store.Put(Octocat, Token);
        var vault = new TokenVault(_store, _log);

        var first = await vault.ReadAsync(Octocat, _ct);
        var second = await vault.ReadAsync(Octocat, _ct);

        Assert.Equal(Token, first.Token);
        Assert.Equal(Token, second.Token);
        Assert.Equal(1, _store.CountOf("read"));
        Assert.All(_store.Calls, call => Assert.False(call.Stoppable));
        AssertNoTokenInTheLog();
    }

    [Fact]
    public async Task The_store_is_read_once_per_key_and_run_also_when_it_found_nothing()
    {
        var vault = new TokenVault(_store, _log);

        var first = await vault.ReadAsync(Octocat, _ct);
        _store.Put(Octocat, Token);
        var second = await vault.ReadAsync(Octocat, _ct);

        Assert.Equal(TokenStoreOutcome.NotFound, first.Outcome);
        Assert.Equal(TokenStoreOutcome.NotFound, second.Outcome);
        Assert.Equal(1, _store.CountOf("read"));
    }

    [Fact]
    public async Task The_store_is_read_once_per_key_and_run_also_when_the_read_failed()
    {
        _store.ReadAnswer = new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.NoAnswer);
        var vault = new TokenVault(_store, _log);

        var first = await vault.ReadAsync(Octocat, _ct);
        _store.ReadAnswer = null;
        var second = await vault.ReadAsync(Octocat, _ct);

        Assert.Equal((TokenStoreOutcome.Failed, TokenStoreReasons.NoAnswer), (first.Outcome, first.Reason));
        Assert.Equal((TokenStoreOutcome.Failed, TokenStoreReasons.NoAnswer), (second.Outcome, second.Reason));
        Assert.Equal(1, _store.CountOf("read"));
    }

    [Fact]
    public async Task Each_key_has_its_own_read()
    {
        var other = new TokenKey("github", "127.0.0.1:47395", "octocat");
        _store.Put(Octocat, Token);
        var vault = new TokenVault(_store, _log);

        Assert.Equal(Token, (await vault.ReadAsync(Octocat, _ct)).Token);
        Assert.Equal(TokenStoreOutcome.NotFound, (await vault.ReadAsync(other, _ct)).Outcome);
        Assert.Equal([Octocat, other], _store.Calls.Where(c => c.Call == "read").Select(c => c.Key));
    }

    [Fact]
    public async Task Two_concurrent_reads_share_one_store_call()
    {
        _store.Put(Octocat, Token);
        _store.HoldRead = new TaskCompletionSource();
        var vault = new TokenVault(_store, _log);

        var first = vault.ReadAsync(Octocat, _ct);
        var second = vault.ReadAsync(Octocat, _ct);
        await WaitUntilAsync(() => _store.CountOf("read") == 1);
        Assert.False(first.IsCompleted || second.IsCompleted);
        _store.HoldRead.SetResult();

        Assert.Equal(Token, (await first).Token);
        Assert.Equal(Token, (await second).Token);
        Assert.Equal(1, _store.CountOf("read"));
    }

    [Fact]
    public async Task A_cancelled_wait_for_a_read_leaves_the_read_to_finish_for_the_next_caller()
    {
        _store.Put(Octocat, Token);
        _store.HoldRead = new TaskCompletionSource();
        var vault = new TokenVault(_store, _log);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_ct);

        var waiting = vault.ReadAsync(Octocat, cancel.Token);
        await WaitUntilAsync(() => _store.CountOf("read") == 1);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        _store.HoldRead.SetResult();

        Assert.Equal(Token, (await vault.ReadAsync(Octocat, _ct)).Token);
        Assert.Equal(1, _store.CountOf("read"));
    }

    [Fact]
    public async Task A_read_that_began_before_a_keep_does_not_bring_back_the_older_token()
    {
        _store.Put(Octocat, OlderToken);
        _store.HoldRead = new TaskCompletionSource();
        var vault = new TokenVault(_store, _log);

        var reading = vault.ReadAsync(Octocat, _ct);
        await WaitUntilAsync(() => _store.CountOf("read") == 1);
        await vault.KeepAsync(Octocat, Token, _ct);
        _store.HoldRead.SetResult();
        await reading;

        Assert.Equal(Token, (await vault.ReadAsync(Octocat, _ct)).Token);
    }

    [Fact]
    public async Task A_cancel_before_the_save_keeps_nothing()
    {
        var vault = new TokenVault(_store, _log);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => vault.KeepAsync(Octocat, Token, new CancellationToken(true)));

        Assert.Equal(TokenStoreOutcome.NotFound, (await vault.ReadAsync(Octocat, _ct)).Outcome);
        Assert.Equal(0, _store.CountOf("save"));
    }

    [Fact]
    public async Task A_cancel_after_the_save_began_is_ignored()
    {
        _store.HoldSave = new TaskCompletionSource();
        var vault = new TokenVault(_store, _log);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_ct);

        var keeping = vault.KeepAsync(Octocat, Token, cancel.Token);
        await WaitUntilAsync(() => _store.CountOf("save") == 1);
        await cancel.CancelAsync();
        _store.HoldSave.SetResult();

        Assert.Equal((TokenPlace.Keychain, (string?)null), await keeping);
        Assert.False(_store.Calls.Single(c => c.Call == "save").Stoppable);
        Assert.Equal(Token, (await vault.ReadAsync(Octocat, _ct)).Token);
    }

    [Fact]
    public async Task An_empty_token_is_refused()
    {
        var vault = new TokenVault(_store, _log);

        await Assert.ThrowsAsync<ArgumentException>(() => vault.KeepAsync(Octocat, string.Empty, _ct));

        Assert.Empty(_store.Calls);
    }

    [Fact]
    public async Task Forget_clears_memory_and_the_read_and_deletes_the_entry()
    {
        _store.Put(Octocat, Token);
        var vault = new TokenVault(_store, _log);
        Assert.Equal(Token, (await vault.ReadAsync(Octocat, _ct)).Token);

        var reason = await vault.ForgetAsync(Octocat, _ct);
        var after = await vault.ReadAsync(Octocat, _ct);

        Assert.Null(reason);
        Assert.Equal(1, _store.CountOf("delete"));
        Assert.Equal(TokenStoreOutcome.NotFound, after.Outcome);
        Assert.Equal(2, _store.CountOf("read"));
    }

    [Fact]
    public async Task Forget_of_a_token_the_keychain_never_had_is_no_failure()
    {
        var vault = new TokenVault(_store, _log);

        Assert.Null(await vault.ForgetAsync(Octocat, _ct));
        Assert.Equal(1, _store.CountOf("delete"));
    }

    [Fact]
    public async Task Forget_returns_a_failed_deletes_reason_and_logs_the_entry_to_remove_by_hand()
    {
        _store.DeleteAnswer = new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Locked);
        var vault = new TokenVault(_store, _log);
        await vault.KeepAsync(Octocat, Token, _ct);

        var reason = await vault.ForgetAsync(Octocat, _ct);

        Assert.Equal(TokenStoreReasons.Locked, reason);
        Assert.Contains(
            (LogLevel.Warning, "The keychain kept the entry VisualCommit:github:github.com:octocat: the keychain stayed locked. Remove it by hand."),
            _log.Lines);
        Assert.Equal(TokenStoreOutcome.Done, (await vault.ReadAsync(Octocat, _ct)).Outcome);
        AssertNoTokenInTheLog();
    }

    [Fact]
    public async Task Without_a_keychain_a_read_fails_and_a_forget_has_nothing_to_delete()
    {
        _store.Probe = new TokenStoreProbe(false, TokenStoreReasons.NoKeychain);
        var vault = new TokenVault(_store, _log);

        var read = await vault.ReadAsync(Octocat, _ct);
        var reason = await vault.ForgetAsync(Octocat, _ct);

        Assert.Equal((TokenStoreOutcome.Failed, TokenStoreReasons.NoKeychain), (read.Outcome, read.Reason));
        Assert.Null(reason);
        Assert.Equal(["probe"], _store.Calls.Select(c => c.Call));
    }

    [Fact]
    public async Task The_store_is_probed_once_per_run_and_the_answer_logged_once()
    {
        _store.HoldProbe = new TaskCompletionSource();
        var vault = new TokenVault(_store, _log);

        var first = vault.KeychainAvailableAsync(_ct);
        var second = vault.KeychainAvailableAsync(_ct);
        _store.HoldProbe.SetResult();
        Assert.True(await first);
        Assert.True(await second);
        await vault.KeepAsync(Octocat, Token, _ct);
        await vault.ReadAsync(new TokenKey("github", "github.com", "hubot"), _ct);
        await vault.ForgetAsync(Octocat, _ct);

        Assert.Equal(1, _store.CountOf("probe"));
        Assert.False(_store.Calls.Single(c => c.Call == "probe").Stoppable);
        Assert.Single(_log.Lines, line => line.Message.StartsWith("Keychain:", StringComparison.Ordinal));
        Assert.Contains((LogLevel.Info, "Keychain: available."), _log.Lines);
    }

    [Fact]
    public async Task An_unavailable_keychain_is_logged_with_the_probes_reason()
    {
        _store.Probe = new TokenStoreProbe(false, TokenStoreReasons.CannotUse);
        var vault = new TokenVault(_store, _log);

        Assert.False(await vault.KeychainAvailableAsync(_ct));
        Assert.False(await vault.KeychainAvailableAsync(_ct));

        Assert.Equal([(LogLevel.Info, "Keychain: not available (the keychain cannot be used in this session).")], _log.Lines);
    }

    [Fact]
    public async Task A_cancelled_wait_for_the_probe_leaves_the_probe_running()
    {
        _store.HoldProbe = new TaskCompletionSource();
        var vault = new TokenVault(_store, _log);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_ct);

        var waiting = vault.KeychainAvailableAsync(cancel.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        _store.HoldProbe.SetResult();

        Assert.True(await vault.KeychainAvailableAsync(_ct));
        Assert.Equal(1, _store.CountOf("probe"));
    }

    private void AssertNoTokenInTheLog() =>
        Assert.All(_log.Lines, line => Assert.DoesNotContain("FAKE-octocat", line.Message, StringComparison.Ordinal));

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The store's call did not come within 10 s.");
            await Task.Delay(10, _ct);
        }
    }

    /// <summary>A store whose save throws, as no store should.</summary>
    private sealed class ThrowingStore : ITokenStore
    {
        public string Name => "throwing";

        public bool KeepsTokensAcrossRestarts => true;

        public Task<TokenStoreProbe> ProbeAsync(CancellationToken ct) => Task.FromResult(new TokenStoreProbe(true, null));

        public Task<TokenStoreResult> SaveAsync(TokenKey key, string token, CancellationToken ct) =>
            throw new InvalidOperationException("The store broke.");

        public Task<TokenRead> ReadAsync(TokenKey key, CancellationToken ct) =>
            Task.FromResult(new TokenRead(TokenStoreOutcome.NotFound, null, null));

        public Task<TokenStoreResult> DeleteAsync(TokenKey key, CancellationToken ct) =>
            Task.FromResult(new TokenStoreResult(TokenStoreOutcome.NotFound));
    }
}
