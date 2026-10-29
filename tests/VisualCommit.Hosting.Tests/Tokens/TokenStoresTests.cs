using VisualCommit.Core.Logging;
using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// <see cref="TokenStores.FromEnvironment"/> and <see cref="MemoryTokenStore"/>
/// (docs/phase-7a-design.md 1.6, 3.7, D253, D269): which store <c>VISUALCOMMIT_KEYCHAIN</c> gives,
/// with one log line each, and no store touched.
/// </summary>
public sealed class TokenStoresTests
{
    private static readonly TokenKey Octocat = new("github", "github.com", "octocat");

    private readonly ListLog _log = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Unset_gives_the_platforms_store_without_a_call(string? value)
    {
        var store = TokenStores.FromEnvironment(Variable(value), _log);

        var (type, name) = OperatingSystem.IsWindows() ? (typeof(WindowsCredentialStore), "Windows Credential Manager")
            : OperatingSystem.IsMacOS() ? (typeof(MacKeychainStore), "macOS keychain")
            : OperatingSystem.IsLinux() ? (typeof(SecretServiceStore), "Secret Service")
            : (typeof(MemoryTokenStore), "memory, no keychain");
        Assert.IsType(type, store);
        Assert.Equal([(LogLevel.Info, $"Token store: {name}.")], _log.Lines);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData(" Memory ")]
    [InlineData("MEMORY")]
    public void Memory_keeps_tokens_in_the_process(string value)
    {
        var store = TokenStores.FromEnvironment(Variable(value), _log);

        Assert.IsType<MemoryTokenStore>(store);
        Assert.Equal("memory", store.Name);
        Assert.Equal([(LogLevel.Info, "Token store: memory.")], _log.Lines);
    }

    [Fact]
    public async Task Unavailable_is_memory_without_a_keychain()
    {
        var store = TokenStores.FromEnvironment(Variable("unavailable"), _log);

        Assert.IsType<MemoryTokenStore>(store);
        Assert.Equal("memory, no keychain", store.Name);
        Assert.False(store.KeepsTokensAcrossRestarts);
        Assert.Equal([(LogLevel.Info, "Token store: memory, no keychain.")], _log.Lines);
        Assert.Equal(new TokenStoreProbe(false, TokenStoreReasons.NoKeychain), await store.ProbeAsync(_ct));
        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.NoKeychain), await store.SaveAsync(Octocat, "ghp_FAKE-octocat-1", _ct));
        Assert.Equal(TokenStoreOutcome.NotFound, (await store.ReadAsync(Octocat, _ct)).Outcome);
    }

    [Theory]
    [InlineData("windows")]
    [InlineData("keychain")]
    [InlineData("ghp_FAKE-octocat-1")]
    public void An_unknown_value_gives_memory_and_a_warning_that_does_not_repeat_it(string value)
    {
        var store = TokenStores.FromEnvironment(Variable(value), _log);

        Assert.IsType<MemoryTokenStore>(store);
        Assert.Equal("memory", store.Name);
        var line = Assert.Single(_log.Lines);
        Assert.Equal((LogLevel.Warning, "VISUALCOMMIT_KEYCHAIN is neither memory nor unavailable: tokens are kept in memory."), line);
        Assert.DoesNotContain(value, line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_keychain_variable_is_read()
    {
        var read = new List<string>();

        TokenStores.FromEnvironment(
            name =>
            {
                read.Add(name);
                return "memory";
            },
            _log);

        Assert.Equal([TokenStores.Variable], read);
    }

    private static Func<string, string?> Variable(string? value) =>
        name => name == TokenStores.Variable ? value : null;
}
