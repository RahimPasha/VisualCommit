using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// <see cref="WindowsCredentialStore"/> without the Credential Manager (docs/phase-7a-design.md
/// 3.4, 3.9, D270): the length check, refused keys, and every call off Windows. On Windows the only
/// calls made are those refused before any call (a save too long, a key that cannot name an entry);
/// the real store's calls are <c>WindowsTokenStoreRealTests</c>' alone.
/// </summary>
public sealed class WindowsCredentialStoreTests
{
    private static readonly TokenKey Octocat = new("github", "github.com", "octocat");

    private readonly ListLog _log = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public void A_token_of_1280_characters_fits_and_one_of_1281_does_not()
    {
        Assert.True(WindowsCredentialStore.Fits(new string('a', 1280)));
        Assert.False(WindowsCredentialStore.Fits(new string('a', 1281)));
        Assert.True(WindowsCredentialStore.Fits("ghp_FAKE-octocat-1"));
    }

    [Fact]
    public async Task A_token_of_1281_characters_is_too_long_before_any_call()
    {
        var store = new WindowsCredentialStore(_log, TestApplication());
        var token = "ghp_FAKE-" + new string('x', 1281 - "ghp_FAKE-".Length);

        var result = await store.SaveAsync(Octocat, token, _ct);

        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.TooLong), result);
        Assert.Single(_log.Lines);
        AssertNoTokenInTheLog(token);
    }

    [Fact]
    public void The_store_names_itself_for_the_log_and_keeps_tokens_across_restarts()
    {
        var store = new WindowsCredentialStore(_log);

        Assert.Equal("Windows Credential Manager", store.Name);
        Assert.True(store.KeepsTokensAcrossRestarts);
        Assert.Empty(_log.Lines);
    }

    [Fact]
    public async Task Off_Windows_every_call_finds_no_keychain()
    {
        Assert.SkipWhen(
            OperatingSystem.IsWindows(),
            "On Windows these calls would reach the Credential Manager (D270); there this class makes only calls refused before any call.");
        var store = new WindowsCredentialStore(_log, TestApplication());
        var token = "ghp_FAKE-" + new string('x', 1280 - "ghp_FAKE-".Length);

        Assert.Equal(new TokenStoreProbe(false, TokenStoreReasons.NoKeychain), await store.ProbeAsync(_ct));
        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.NoKeychain), await store.SaveAsync(Octocat, token, _ct));
        var read = await store.ReadAsync(Octocat, _ct);
        Assert.Equal((TokenStoreOutcome.Failed, TokenStoreReasons.NoKeychain), (read.Outcome, read.Reason));
        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.NoKeychain), await store.DeleteAsync(Octocat, _ct));
        AssertNoTokenInTheLog(token);
    }

    [Theory]
    [InlineData("octo cat")]
    [InlineData("octo\ncat")]
    public async Task A_key_that_cannot_name_an_entry_is_refused_before_any_call(string account)
    {
        // The key's check comes before the thread pool and advapi32, as the length check does.
        var store = new WindowsCredentialStore(_log, TestApplication());
        var key = new TokenKey("github", "github.com", account);

        Assert.Equal(TokenStoreReasons.Refused, (await store.SaveAsync(key, "ghp_FAKE-octocat-1", _ct)).Reason);
        Assert.Equal(TokenStoreReasons.Refused, (await store.ReadAsync(key, _ct)).Reason);
        Assert.Equal(TokenStoreReasons.Refused, (await store.DeleteAsync(key, _ct)).Reason);
        Assert.All(_log.Lines, line => Assert.False(line.Message.Any(char.IsControl), line.Message));
        AssertNoTokenInTheLog("ghp_FAKE-octocat-1");
    }

    private static string TestApplication() => "VisualCommit-test:" + Guid.NewGuid();

    private void AssertNoTokenInTheLog(string token) =>
        Assert.All(_log.Lines, line =>
        {
            Assert.DoesNotContain(token, line.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("FAKE", line.Message, StringComparison.Ordinal);
        });
}
