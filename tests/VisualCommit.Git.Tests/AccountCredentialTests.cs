using System.Diagnostics;
using System.Net;
using System.Text;
using VisualCommit.AskPass;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.PromptTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The account's token reaching git through the app's helper against a real server
/// (docs/phase-7a-design.md 4.2, 4.6, 4.8, D255, D273, D277, D329, D330): <see cref="AuthRemoteServer"/>
/// takes <c>x-access-token</c> and the scenario's token, a <see cref="RecordingCredentialSource"/>
/// stands for the hosting session, and the user's own helper is a <c>store</c> helper with a file
/// of its own in the repo's global configuration, so that any token git hands to the user's
/// helpers would show in that file.
/// </summary>
public class AccountCredentialTests
{
    private const string Token = "github_pat_FAKE-octocat-1";

    [Fact]
    public async Task A_bound_fetch_takes_the_token_without_a_prompt_and_the_users_helper_gets_nothing()
    {
        await using var setup = await Setup.StartAsync();
        using var scope = setup.Broker.Begin("Fetch", "auth", PromptMode.Ask, bindings: [setup.Binding]);

        var fetch = await GitAsync(setup.Repo, scope.Environment, ["fetch", "origin"]);

        Assert.True(fetch.Succeeded, fetch.StandardError);
        Assert.Empty(setup.Handler.Prompts);
        Assert.Equal([setup.Account], setup.Source.Gets);
        Assert.Contains(setup.Server.Requests, request => request.Status == 200 && request.UserName == "x-access-token");
        Assert.False(File.Exists(setup.UserStore));
    }

    [Fact]
    public async Task A_bound_push_takes_the_token_without_a_prompt()
    {
        await using var setup = await Setup.StartAsync();
        using var scope = setup.Broker.Begin("Push main", "auth", PromptMode.Ask, bindings: [setup.Binding]);
        await setup.Repo.CommitFileAsync("src/local.txt", "local\n", "Local work");

        var push = await GitAsync(setup.Repo, scope.Environment, ["push", "--force", "origin", "refs/heads/main:refs/heads/main"]);

        Assert.True(push.Succeeded, push.StandardError);
        Assert.Empty(setup.Handler.Prompts);
        Assert.Contains(setup.Server.Requests, request => request.Status == 200 && request.UserName == "x-access-token" && request.Path == "/remote.git/git-receive-pack");
        Assert.False(File.Exists(setup.UserStore));
    }

    [Fact]
    public async Task A_bound_clone_takes_the_token_and_writes_no_credential_key_or_token_into_any_configuration()
    {
        await using var setup = await Setup.StartAsync();
        var before = await setup.ConfigListAsync(setup.Repo.Path);
        using var scope = setup.Broker.Begin("Clone", "auth", PromptMode.Ask, bindings: [setup.Binding]);
        var target = setup.Repo.SiblingPath("clone");

        var clone = await GitAsync(setup.Repo, scope.Environment, ["clone", setup.Server.Url, target]);

        Assert.True(clone.Succeeded, clone.StandardError);
        Assert.Empty(setup.Handler.Prompts);
        Assert.Equal(before, await setup.ConfigListAsync(setup.Repo.Path));
        var cloned = (await setup.Repo.GitAsync("-C", target, "config", "--local", "--list")).StandardOutput;
        Assert.DoesNotContain("credential", cloned, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"remote.origin.url={setup.Server.Url}", cloned, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, File.ReadAllText(Path.Combine(target, ".git", "config")), StringComparison.Ordinal);
        Assert.False(File.Exists(setup.UserStore));
    }

    [Fact]
    public async Task A_revoked_token_fails_at_once_without_a_prompt_and_marks_the_account_refused_once()
    {
        await using var setup = await Setup.StartAsync();
        setup.Source.Token = "github_pat_FAKE-octocat-0";
        await setup.Repo.GitAsync("remote", "add", "second", setup.Server.Url);
        using var scope = setup.Broker.Begin("Fetch", "auth", PromptMode.Ask, bindings: [setup.Binding]);

        var stopwatch = Stopwatch.StartNew();
        var fetch = await GitAsync(setup.Repo, scope.Environment, ["fetch", "origin"]);
        stopwatch.Stop();

        Assert.False(fetch.Succeeded);
        Assert.Equal(GitFailureKind.AuthenticationFailed, GitFailureClassifier.Classify(fetch.StandardError, fetch.StandardOutput, fetch.ExitCode));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"The fetch took {stopwatch.Elapsed}.");
        Assert.Empty(setup.Handler.Prompts);
        Assert.Equal([setup.Account], setup.Source.Refusals);
        Assert.Equal(setup.Account, scope.RefusedAccount);

        // The same scope's next remote: no token, and git's prompt for it gets no dialog (D273).
        var second = await GitAsync(setup.Repo, scope.Environment, ["fetch", "second"]);

        Assert.False(second.Succeeded);
        Assert.Single(setup.Source.Gets);
        Assert.Empty(setup.Handler.Prompts);
        Assert.Single(setup.Source.Refusals);
        Assert.False(File.Exists(setup.UserStore));
    }

    [Fact]
    public async Task An_unbound_url_goes_to_the_users_helper_then_to_the_handler_as_today()
    {
        await using var setup = await Setup.StartAsync();
        setup.Server.Credentials = new NetworkCredential("alice", "s3cret");
        setup.Handler.Then(PromptAnswer.Of("alice")).Then(PromptAnswer.Of("s3cret"));
        var elsewhere = new CredentialBinding($"http://127.0.0.1:{setup.Server.Port}/other.git", setup.Account);
        using var scope = setup.Broker.Begin("Fetch", "auth", PromptMode.Ask, bindings: [elsewhere]);

        var fetch = await GitAsync(setup.Repo, scope.Environment, ["fetch", "origin"]);

        Assert.True(fetch.Succeeded, fetch.StandardError);
        Assert.Equal([GitPromptKind.Username, GitPromptKind.Password], setup.Handler.Prompts.Select(prompt => prompt.Kind));
        // git's prompt names the host only: the user name prompt carries the host's binding's account, alice's password prompt none.
        Assert.Equal([setup.Account, null], setup.Handler.Prompts.Select(prompt => prompt.Account));
        Assert.Empty(setup.Source.Gets);

        // git stores alice's password in the user's helper, its own way.
        Assert.Contains("alice:s3cret", File.ReadAllText(setup.UserStore), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_deny_scope_fetches_a_bound_url_and_fails_an_unbound_one_at_once()
    {
        await using var setup = await Setup.StartAsync();
        using (var bound = setup.Broker.Begin("Auto-fetch", "auth", PromptMode.Deny, bindings: [setup.Binding]))
        {
            var fetch = await GitAsync(setup.Repo, bound.Environment, ["fetch", "origin"]);
            Assert.True(fetch.Succeeded, fetch.StandardError);
        }

        var elsewhere = new CredentialBinding($"http://127.0.0.1:{setup.Server.Port}/other.git", setup.Account);
        using var unbound = setup.Broker.Begin("Auto-fetch", "auth", PromptMode.Deny, bindings: [elsewhere]);
        var stopwatch = Stopwatch.StartNew();
        var failed = await GitAsync(setup.Repo, unbound.Environment, ["fetch", "origin"]);
        stopwatch.Stop();

        // At once: the deny scope's helper refuses git's prompt, which git does not ask again and
        // no dialog sees; 30 s, as AuthenticatedRemoteTests allows, under the default run's load (D237).
        Assert.False(failed.Succeeded);
        Assert.Contains("could not read Username", failed.StandardError, StringComparison.Ordinal);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"The fetch took {stopwatch.Elapsed}.");
        Assert.Empty(setup.Handler.Prompts);
        Assert.Single(setup.Source.Gets);
        Assert.False(File.Exists(setup.UserStore));
    }

    [Fact]
    public async Task The_helper_works_from_a_folder_with_a_space_and_an_apostrophe()
    {
        await using var setup = await Setup.StartAsync();
        var helper = CopyHelperTo(setup.Repo.SiblingPath("it's the helper"));
        using var broker = new AskPassBroker(setup.Handler, helperPath: helper, credentials: setup.Source);
        using var scope = broker.Begin("Fetch", "auth", PromptMode.Ask, bindings: [setup.Binding]);

        var fetch = await GitAsync(setup.Repo, scope.Environment, ["fetch", "origin"]);

        Assert.True(fetch.Succeeded, fetch.StandardError);
        Assert.Empty(setup.Handler.Prompts);
    }

    [Fact]
    public async Task An_inherited_GIT_CONFIG_COUNT_pair_still_applies_beside_the_bindings()
    {
        await using var setup = await Setup.StartAsync();
        var inherited = new Dictionary<string, string?> { ["GIT_CONFIG_COUNT"] = "1", ["GIT_CONFIG_KEY_0"] = "vc.inherited", ["GIT_CONFIG_VALUE_0"] = "kept" };
        using var broker = new AskPassBroker(setup.Handler, credentials: setup.Source, sessionEnvironment: inherited);
        using var scope = broker.Begin("Fetch", "auth", PromptMode.Ask, bindings: [setup.Binding]);
        var environment = Combine(inherited, scope.Environment);

        var fetch = await GitAsync(setup.Repo, environment, ["fetch", "origin"]);
        var value = await GitAsync(setup.Repo, environment, ["config", "--get", "vc.inherited"]);

        Assert.True(fetch.Succeeded, fetch.StandardError);
        Assert.Equal("3", scope.Environment["GIT_CONFIG_COUNT"]);
        Assert.Equal("kept", value.StandardOutput.Trim());
        Assert.Empty(setup.Handler.Prompts);
    }

    [Fact]
    public async Task Curl_tracing_under_an_inherited_GIT_TRACE_REDACT_0_shows_neither_the_token_nor_its_base64()
    {
        await using var setup = await Setup.StartAsync();
        using var scope = setup.Broker.Begin("Fetch", "auth", PromptMode.Ask, bindings: [setup.Binding]);
        var environment = Combine(new Dictionary<string, string?> { ["GIT_TRACE_REDACT"] = "0", ["GIT_TRACE_CURL"] = "1" }, scope.Environment);

        var fetch = await GitAsync(setup.Repo, environment, ["fetch", "origin"]);

        Assert.True(fetch.Succeeded, fetch.StandardError);
        Assert.Contains("Authorization: Basic <redacted>", fetch.StandardError, StringComparison.Ordinal);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{Token}"));
        Assert.DoesNotContain(Token, fetch.StandardError + fetch.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(basic, fetch.StandardError + fetch.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task What_git_older_than_2_31_sees_asks_the_users_helper_and_the_handler_and_never_the_account()
    {
        // git before 2.31 ignores GIT_CONFIG_COUNT: the scope's pairs are as good as absent (D277).
        await using var setup = await Setup.StartAsync();
        setup.Handler.Then(PromptAnswer.Cancelled);
        using var broker = new AskPassBroker(setup.Handler, credentials: setup.Source, gitVersion: () => null);
        using var scope = broker.Begin("Fetch", "auth", PromptMode.Ask, bindings: [setup.Binding]);
        var stripped = scope.Environment
            .Where(variable => !variable.Key.StartsWith("GIT_CONFIG_", StringComparison.Ordinal))
            .ToDictionary(variable => variable.Key, variable => variable.Value);

        var fetch = await GitAsync(setup.Repo, stripped, ["fetch", "origin"]);

        Assert.False(fetch.Succeeded);
        var prompt = Assert.Single(setup.Handler.Prompts);
        Assert.Equal(GitPromptKind.Username, prompt.Kind);
        Assert.Equal(setup.Account, prompt.Account);
        Assert.Empty(setup.Source.Gets);
        setup.AssertUserStoreHasNoToken();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_inherited_command_line_helper_never_sees_a_bound_call_and_is_asked_for_an_unbound_one(bool throughInclude)
    {
        await using var setup = await Setup.StartAsync();
        var inheritedStore = setup.Repo.SiblingPath("inherited-store.txt");
        var helper = $"store --file='{inheritedStore.Replace('\\', '/')}'";
        string parameters;
        if (throughInclude)
        {
            var included = setup.Repo.SiblingPath("included.gitconfig");
            await setup.Repo.GitAsync("config", "--file", included, "credential.helper", helper);
            parameters = $"'include.path'='{included.Replace('\\', '/')}'";
        }
        else
        {
            parameters = $"'credential.helper'='{helper.Replace("'", "'\\''", StringComparison.Ordinal)}'";
        }

        // In the runner's session environment, after the repo's isolation, which would remove it.
        var inherited = new Dictionary<string, string?> { ["GIT_CONFIG_PARAMETERS"] = parameters };
        using var broker = new AskPassBroker(setup.Handler, credentials: setup.Source, sessionEnvironment: inherited);
        using (var bound = broker.Begin("Fetch", "auth", PromptMode.Ask, bindings: [setup.Binding]))
        {
            var fetch = await GitAsync(setup.Repo, Combine(inherited, bound.Environment), ["fetch", "origin"]);

            Assert.True(fetch.Succeeded, fetch.StandardError);
            Assert.False(File.Exists(inheritedStore));
        }

        setup.Server.Credentials = new NetworkCredential("alice", "s3cret");
        setup.Handler.Then(PromptAnswer.Of("alice")).Then(PromptAnswer.Of("s3cret"));
        using var unbound = broker.Begin("Fetch", "auth", PromptMode.Ask);
        var asked = await GitAsync(setup.Repo, Combine(inherited, unbound.Environment), ["fetch", "origin"]);

        Assert.True(asked.Succeeded, asked.StandardError);
        Assert.Contains("alice:s3cret", File.ReadAllText(inheritedStore), StringComparison.Ordinal);
        Assert.DoesNotContain(Token, File.ReadAllText(inheritedStore), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stale_authorization_header_makes_git_erase_the_token_once_and_the_users_helper_gets_nothing()
    {
        // actions/checkout leaves such a header in .git/config (4.2, D330): git's request carries it
        // in place of the helper's credential, the server refuses, and git erases the valid token.
        await using var setup = await Setup.StartAsync();
        var stale = Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:stale"));
        await setup.Repo.GitAsync("config", $"http.http://127.0.0.1:{setup.Server.Port}/.extraheader", $"Authorization: Basic {stale}");
        using var scope = setup.Broker.Begin("Fetch", "auth", PromptMode.Ask, bindings: [setup.Binding]);

        var fetch = await GitAsync(setup.Repo, scope.Environment, ["fetch", "origin"]);

        Assert.False(fetch.Succeeded);
        Assert.Equal(GitFailureKind.AuthenticationFailed, GitFailureClassifier.Classify(fetch.StandardError, fetch.StandardOutput, fetch.ExitCode));
        Assert.Equal([setup.Account], setup.Source.Refusals);
        Assert.Empty(setup.Handler.Prompts);
        Assert.False(File.Exists(setup.UserStore));
    }

    /// <summary><paramref name="first"/>'s variables, then <paramref name="then"/>'s on top: the session's environment, then the scope's.</summary>
    private static Dictionary<string, string?> Combine(IReadOnlyDictionary<string, string?> first, IReadOnlyDictionary<string, string?> then)
    {
        var environment = new Dictionary<string, string?>(first);
        foreach (var (name, value) in then)
        {
            environment[name] = value;
        }

        return environment;
    }

    /// <summary>The server, its scenario repo with the user's store helper, a broker and the account's source.</summary>
    private sealed class Setup : IAsyncDisposable
    {
        private Setup(AuthRemoteServer server, TempRepo repo)
        {
            Server = server;
            Repo = repo;
            Account = new AccountKey(HostingProvider.GitHub, $"127.0.0.1:{server.Port}", "octocat");
            Binding = new CredentialBinding(server.Url, Account);
            Broker = new AskPassBroker(Handler, credentials: Source);
            UserStore = repo.SiblingPath("user-store");
        }

        public AuthRemoteServer Server { get; }

        public TempRepo Repo { get; }

        public AccountKey Account { get; }

        public CredentialBinding Binding { get; }

        public RecordingPromptHandler Handler { get; } = new();

        public RecordingCredentialSource Source { get; } = new(Token);

        public AskPassBroker Broker { get; }

        /// <summary>The user's own <c>store</c> helper's file.</summary>
        public string UserStore { get; }

        public static async Task<Setup> StartAsync()
        {
            var server = await AuthRemoteServer.StartAsync();
            server.Credentials = new NetworkCredential(AskPassProtocol.CredentialUserName, Token);
            var repo = await Scenarios.AuthRemoteAsync(server);
            var setup = new Setup(server, repo);
            await repo.GitAsync("config", "--file", repo.Environment["GIT_CONFIG_GLOBAL"]!, "credential.helper", $"store --file='{setup.UserStore.Replace('\\', '/')}'");
            return setup;
        }

        /// <summary>The user's helper's file, which git may write alice's line to, holds neither the token nor its user name.</summary>
        public void AssertUserStoreHasNoToken()
        {
            if (File.Exists(UserStore))
            {
                Assert.DoesNotContain(Token, File.ReadAllText(UserStore), StringComparison.Ordinal);
                Assert.DoesNotContain("x-access-token", File.ReadAllText(UserStore), StringComparison.Ordinal);
            }
        }

        /// <summary><c>git config --list --show-origin</c> in <paramref name="folder"/>: the repository's and the global file's keys.</summary>
        public async Task<string> ConfigListAsync(string folder) =>
            (await Repo.GitAsync("-C", folder, "config", "--list", "--show-origin")).StandardOutput;

        public async ValueTask DisposeAsync()
        {
            Broker.Dispose();
            Repo.Dispose();
            await Server.DisposeAsync();
        }
    }
}
