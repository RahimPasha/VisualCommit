using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// WP0's smoke tests (docs/phase-7a-design.md 1.10): the environment's choices of where GitHub is
/// and where tokens are kept, as every test process and the harnesses' apps start (1.8, ruling 24).
/// </summary>
public sealed class HostingSmokeTests
{
    [Fact]
    public void None_turns_GitHub_off_without_a_word_in_the_log()
    {
        var log = new ListLog();

        var options = HostingOptions.FromEnvironment(Variable(HostingOptions.GitHubVariable, "none"), log);

        Assert.Same(HostingOptions.Off, options);
        Assert.Null(options.GitHub);
        Assert.Empty(log.Lines);
    }

    [Fact]
    public async Task Memory_keeps_tokens_in_the_process_and_says_so_once()
    {
        var log = new ListLog();
        var key = TokenKey.Of(new AccountKey(HostingProvider.GitHub, "GitHub.com", "OctoCat"));
        var ct = TestContext.Current.CancellationToken;

        var store = TokenStores.FromEnvironment(Variable(TokenStores.Variable, "memory"), log);

        Assert.IsType<MemoryTokenStore>(store);
        Assert.Equal("memory", store.Name);
        Assert.False(store.KeepsTokensAcrossRestarts);
        Assert.Equal([(LogLevel.Info, "Token store: memory.")], log.Lines);
        Assert.Equal(new TokenStoreProbe(true, null), await store.ProbeAsync(ct));
        Assert.Equal(TokenStoreOutcome.Done, (await store.SaveAsync(key, "ghp_FAKE-octocat-1", ct)).Outcome);
        var read = await store.ReadAsync(key, ct);
        Assert.Equal((TokenStoreOutcome.Done, "ghp_FAKE-octocat-1"), (read.Outcome, read.Token));
        Assert.DoesNotContain("ghp_", read.ToString(), StringComparison.Ordinal);
        Assert.Equal(TokenStoreOutcome.Done, (await store.DeleteAsync(key, ct)).Outcome);
        Assert.Equal(TokenStoreOutcome.NotFound, (await store.ReadAsync(key, ct)).Outcome);
    }

    [Fact]
    public void Every_test_process_starts_with_GitHub_off_and_tokens_in_memory()
    {
        var log = new ListLog();

        Assert.Equal("none", Environment.GetEnvironmentVariable(HostingOptions.GitHubVariable));
        Assert.Equal("memory", Environment.GetEnvironmentVariable(TokenStores.Variable));
        Assert.All(
            ["GH_TOKEN", "GITHUB_TOKEN", "GH_ENTERPRISE_TOKEN", "GITHUB_ENTERPRISE_TOKEN", "GH_HOST", "VISUALCOMMIT_LIVE_GITHUB_TOKEN", "VISUALCOMMIT_LIVE_GITHUB_REPO"],
            variable => Assert.Null(Environment.GetEnvironmentVariable(variable)));
        Assert.Same(HostingOptions.Off, HostingOptions.FromEnvironment(Environment.GetEnvironmentVariable, log));
        Assert.Equal("memory", TokenStores.FromEnvironment(Environment.GetEnvironmentVariable, log).Name);
    }

    [Fact]
    public void The_null_session_is_off_and_binds_nothing()
    {
        var session = NullHostingSession.Instance;

        Assert.True(session.IsOff);
        Assert.False(session.BindsGit);
        Assert.Null(session.Recognise("https://github.com/octocat/hello.git"));
        Assert.Empty(session.BindingsForClone("https://github.com/octocat/hello.git"));
        Assert.Same(NullRepositoryHosting.Instance, session.ForRepository(Path.GetTempPath()));
        Assert.Equal(PullRequestsState.NotGitHub, NullRepositoryHosting.Instance.State.PullRequests);
    }

    private static Func<string, string?> Variable(string name, string value) =>
        variable => variable == name ? value : null;
}
