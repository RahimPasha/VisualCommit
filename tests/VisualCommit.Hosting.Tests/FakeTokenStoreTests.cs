using VisualCommit.Core.Hosting;
using VisualCommit.Hosting.Tokens;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// The harness's keychain (docs/phase-7a-design.md 2.5): its probe, saves, reads and deletes,
/// the failures and holds a check sets, the calls it records without a token, and one instance
/// across two "starts", the scripted restart.
/// </summary>
public sealed class FakeTokenStoreTests
{
    private const string Token = "github_pat_FAKE-octo-1";

    private static readonly TokenKey Octo = TokenKey.Of(new AccountKey(HostingProvider.GitHub, "127.0.0.1:47395", "Octo"));

    private static CancellationToken Cancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_saved_token_is_read_and_deleted_and_the_calls_name_only_the_key()
    {
        var store = new FakeTokenStore();

        Assert.Equal("fake", store.Name);
        Assert.True(store.KeepsTokensAcrossRestarts);
        Assert.Equal(new TokenStoreProbe(true, null), await store.ProbeAsync(Cancelled));
        Assert.Equal(TokenStoreOutcome.NotFound, (await store.ReadAsync(Octo, Cancelled)).Outcome);
        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Done), await store.SaveAsync(Octo, Token, Cancelled));
        Assert.True(store.Holds(Octo));

        var read = await store.ReadAsync(Octo, Cancelled);
        Assert.Equal((TokenStoreOutcome.Done, Token), (read.Outcome, read.Token));
        Assert.Equal(TokenStoreOutcome.Done, (await store.DeleteAsync(Octo, Cancelled)).Outcome);
        Assert.Equal(TokenStoreOutcome.NotFound, (await store.DeleteAsync(Octo, Cancelled)).Outcome);
        Assert.False(store.Holds(Octo));

        Assert.Equal(
            ["probe", "read github:127.0.0.1:47395:octo", "save github:127.0.0.1:47395:octo", "read github:127.0.0.1:47395:octo", "delete github:127.0.0.1:47395:octo", "delete github:127.0.0.1:47395:octo"],
            store.Calls);
        Assert.DoesNotContain(store.Calls, call => call.Contains(Token, StringComparison.Ordinal));
        Assert.DoesNotContain(Token, store.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unavailable_it_says_so_and_refuses_saves_and_a_failure_set_once_fails_one_save()
    {
        var store = new FakeTokenStore { Available = false };

        Assert.Equal(new TokenStoreProbe(false, FakeTokenStore.UnavailableReason), await store.ProbeAsync(Cancelled));
        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, FakeTokenStore.UnavailableReason), await store.SaveAsync(Octo, Token, Cancelled));
        Assert.False(store.Holds(Octo));

        store.Available = true;
        store.FailNextSave("The keychain is locked.");
        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, "The keychain is locked."), await store.SaveAsync(Octo, Token, Cancelled));
        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Done), await store.SaveAsync(Octo, Token, Cancelled));
        Assert.True(store.Holds(Octo));
    }

    [Fact]
    public async Task A_hold_keeps_the_next_call_waiting_until_it_is_disposed()
    {
        var store = new FakeTokenStore();
        await store.SaveAsync(Octo, Token, Cancelled);

        var hold = store.HoldNext();
        Assert.Same(hold, store.HoldNext());
        var read = store.ReadAsync(Octo, Cancelled);
        await hold.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        await Task.Delay(100, Cancelled);
        Assert.False(read.IsCompleted, "The held read went on before the hold was disposed.");

        hold.Dispose();
        Assert.Equal(Token, (await read.WaitAsync(TimeSpan.FromSeconds(30), Cancelled)).Token);

        // The next call is not held; a hold disposed before any call arrived is dropped.
        Assert.Equal(TokenStoreOutcome.Done, (await store.ReadAsync(Octo, Cancelled)).Outcome);
        var dropped = store.HoldNext();
        dropped.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dropped.Arrived);
        Assert.Equal(TokenStoreOutcome.Done, (await store.ReadAsync(Octo, Cancelled).WaitAsync(TimeSpan.FromSeconds(30), Cancelled)).Outcome);

        // A held call is cancelled with its token.
        using var cancel = new CancellationTokenSource();
        using var third = store.HoldNext();
        var cancelled = store.ProbeAsync(cancel.Token);
        await third.Arrived.WaitAsync(TimeSpan.FromSeconds(30), Cancelled);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
    }

    [Fact]
    public async Task One_store_across_two_starts_keeps_the_token_and_a_removed_one_is_gone()
    {
        var store = new FakeTokenStore();

        // The first "start" signs in and keeps the token; the second reads it back.
        await StartAsync(store, sign: true);
        Assert.Equal(Token, await StartAsync(store, sign: false));

        // A token the user deleted from the keychain behind the app's back is gone at the next start.
        store.Remove(Octo);
        Assert.Null(await StartAsync(store, sign: false));
        Assert.Equal(["probe", "save github:127.0.0.1:47395:octo", "probe", "read github:127.0.0.1:47395:octo", "probe", "read github:127.0.0.1:47395:octo"], store.Calls);
    }

    /// <summary>What a session's start does with the store: a probe, then a save at sign-in or a read of the kept token.</summary>
    private static async Task<string?> StartAsync(ITokenStore store, bool sign)
    {
        await store.ProbeAsync(Cancelled);
        if (sign)
        {
            await store.SaveAsync(Octo, Token, Cancelled);
            return Token;
        }

        return (await store.ReadAsync(Octo, Cancelled)).Token;
    }
}
