using VisualCommit.AskPass;
using VisualCommit.Core.Git;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.PromptTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The variables of a scope with <see cref="CredentialBinding"/>s (docs/phase-7a-design.md 4.6,
/// D274, D277, D329): the <c>GIT_CONFIG_COUNT</c> pairs and when they are given, the deny scope's
/// registration, and that every scope that uses no binding is today's, byte for byte.
/// </summary>
/// <remarks>
/// The process's own <c>GIT_CONFIG_COUNT</c> is not set here: the test process runs git beside
/// these tests, and the variable would reach every call. A session environment that names the
/// variable stands for it, as the broker reads the session's before the process's.
/// </remarks>
public class AskPassBrokerBindingTests
{
    private static readonly AccountKey Octocat = new(HostingProvider.GitHub, "github.com", "octocat");
    private static readonly CredentialBinding Repo = new("https://github.com/o/r.git", Octocat);
    private static readonly CredentialBinding Upstream = new("https://github.com/u/r.git", Octocat);

    [Fact]
    public void Two_pairs_per_binding_numbered_from_zero_without_an_inherited_count()
    {
        using var broker = Broker();
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask, "op-1", bindings: [Repo, Upstream]);

        var environment = scope.Environment;

        var helper = $"!'{broker.HelperPath.Replace('\\', '/')}' --vc-credential";
        Assert.Equal("4", environment["GIT_CONFIG_COUNT"]);
        Assert.Equal(
            [
                ("credential.https://github.com/o/r.git.helper", string.Empty),
                ("credential.https://github.com/o/r.git.helper", helper),
                ("credential.https://github.com/u/r.git.helper", string.Empty),
                ("credential.https://github.com/u/r.git.helper", helper),
            ],
            Pairs(environment, 0, 4));
        Assert.Equal("1", environment["GIT_TRACE_REDACT"]);
        Assert.True(environment.TryGetValue("GIT_CONFIG_PARAMETERS", out var parameters));
        Assert.Null(parameters);
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void The_pairs_come_after_the_count_git_already_gets(string? inherited, int first)
    {
        using var broker = Broker(session: new Dictionary<string, string?> { ["GIT_CONFIG_COUNT"] = inherited });
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask, "op-1", bindings: [Repo]);

        Assert.Equal((first + 2).ToString(System.Globalization.CultureInfo.InvariantCulture), scope.Environment["GIT_CONFIG_COUNT"]);
        Assert.Equal("credential.https://github.com/o/r.git.helper", scope.Environment[$"GIT_CONFIG_KEY_{first}"]);
        Assert.Equal(string.Empty, scope.Environment[$"GIT_CONFIG_VALUE_{first}"]);
        Assert.StartsWith("!'", scope.Environment[$"GIT_CONFIG_VALUE_{first + 1}"], StringComparison.Ordinal);
        Assert.False(scope.Environment.ContainsKey($"GIT_CONFIG_KEY_{first + 2}"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData(" 1")]
    public void An_invalid_inherited_count_gives_no_pair_and_one_warning(string inherited)
    {
        var log = new ListLog();
        using var broker = Broker(session: new Dictionary<string, string?> { ["GIT_CONFIG_COUNT"] = inherited }, log: log);

        AssertToday(broker, PromptMode.Ask);
        AssertToday(broker, PromptMode.Deny);
        Assert.Single(log.Entries, entry => entry == (LogLevel.Warning, "GIT_CONFIG_COUNT is not a number git accepts; GitHub's token is not given to git."));
    }

    [Fact]
    public void Without_the_helper_without_a_source_or_in_mode_None_no_pair_is_given()
    {
        using (var missing = new AskPassBroker(new RecordingPromptHandler(), helperPath: Path.Combine(AppContext.BaseDirectory, "no-such-helper"), credentials: new RecordingCredentialSource("t")))
        {
            AssertToday(missing, PromptMode.Ask);
        }

        using (var sourceless = new AskPassBroker(new RecordingPromptHandler()))
        {
            AssertToday(sourceless, PromptMode.Ask);
            AssertToday(sourceless, PromptMode.Deny);
        }

        using var broker = Broker();
        using var none = broker.Begin("Fetch", "repo", PromptMode.None, "op-n", bindings: [Repo]);
        Assert.Empty(none.Environment);
    }

    [Theory]
    [InlineData(2, 30, 0, false)]
    [InlineData(2, 31, 0, true)]
    [InlineData(2, 56, 0, true)]
    public void Git_older_than_2_31_gets_todays_variables_and_2_31_or_later_the_pairs(int major, int minor, int patch, bool pairs)
    {
        using var broker = Broker(version: () => new GitVersion(major, minor, patch));

        if (pairs)
        {
            using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask, "op-1", bindings: [Repo]);
            Assert.Equal("2", scope.Environment["GIT_CONFIG_COUNT"]);
        }
        else
        {
            AssertToday(broker, PromptMode.Ask);
            AssertToday(broker, PromptMode.Deny);
        }
    }

    [Fact]
    public void A_version_not_yet_known_counts_as_2_31()
    {
        using var broker = Broker(version: () => null);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask, "op-1", bindings: [Repo]);

        Assert.Equal("2", scope.Environment["GIT_CONFIG_COUNT"]);
    }

    [Fact]
    public void A_deny_scope_without_bindings_is_todays_and_is_not_registered()
    {
        using var broker = Broker();
        using var scope = broker.Begin("Auto-fetch", "repo", PromptMode.Deny, "op-1");

        Assert.Equal(
            new Dictionary<string, string?>
            {
                ["GIT_ASKPASS"] = broker.HelperPath,
                ["SSH_ASKPASS"] = broker.HelperPath,
                ["SSH_ASKPASS_REQUIRE"] = "force",
                ["GCM_INTERACTIVE"] = "never",
                [AskPassProtocol.DenyVariable] = "1",
                [AskPassProtocol.PipeVariable] = null,
                [AskPassProtocol.TokenVariable] = null,
                [AskPassProtocol.OperationVariable] = null,
            },
            scope.Environment);

        // Registered under its id, a second scope with the id would throw.
        using var again = broker.Begin("Auto-fetch", "repo", PromptMode.Deny, "op-1");
    }

    [Fact]
    public async Task A_deny_scope_with_bindings_is_registered_with_the_pipe_and_answers_gits_get()
    {
        var source = new RecordingCredentialSource("github_pat_FAKE-octocat-1");
        using var broker = Broker(source);
        using var scope = broker.Begin("Auto-fetch", "repo", PromptMode.Deny, "op-1", bindings: [Repo]);

        Assert.Equal(("1", "never", broker.PipeName, "op-1"), (scope.Environment[AskPassProtocol.DenyVariable], scope.Environment["GCM_INTERACTIVE"], scope.Environment[AskPassProtocol.PipeVariable], scope.Environment[AskPassProtocol.OperationVariable]));
        Assert.NotNull(scope.Environment[AskPassProtocol.TokenVariable]);
        Assert.Throws<InvalidOperationException>(() => broker.Begin("Auto-fetch", "repo", PromptMode.Deny, "op-1", bindings: [Repo]));

        var result = await AskPassRunner.RunCredentialAsync("get", "protocol=https\nhost=github.com\n\n", scope.Environment, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("username=x-access-token\npassword=github_pat_FAKE-octocat-1\n", result.StandardOutput);
        Assert.Equal([Octocat], source.Gets);
    }

    [Fact]
    public async Task A_prompt_to_a_deny_scope_never_reaches_the_handler()
    {
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"));
        using var broker = new AskPassBroker(handler, credentials: new RecordingCredentialSource("t"));
        using var scope = broker.Begin("Auto-fetch", "repo", PromptMode.Deny, "op-1", bindings: [Repo]);

        // As if the deny variable were lost on the way: the broker still refuses.
        var environment = new Dictionary<string, string?>(scope.Environment) { [AskPassProtocol.DenyVariable] = null };
        var result = await AskPassRunner.RunAsync(environment, "Username for 'https://github.com': ", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(handler.Prompts);
    }

    [Fact]
    public void A_helper_path_with_a_space_and_an_apostrophe_is_quoted_for_sh()
    {
        using var folder = new TempDirectory("helper's copy");
        var helper = CopyHelperTo(Path.Combine(folder.Path, "it's a folder"));
        using var broker = new AskPassBroker(new RecordingPromptHandler(), helperPath: helper, credentials: new RecordingCredentialSource("t"));
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask, "op-1", bindings: [Repo]);

        var expected = "!'" + broker.HelperPath.Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal) + "' --vc-credential";
        Assert.Equal(expected, scope.Environment["GIT_CONFIG_VALUE_1"]);
        Assert.Contains("'\\''s a folder/", expected, StringComparison.Ordinal);
        Assert.DoesNotContain('\\', expected.Replace("'\\''", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Bindings_are_used_once_each()
    {
        using var broker = Broker();
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask, "op-1", bindings: [Repo, Repo with { }, Upstream, Repo]);

        Assert.Equal("4", scope.Environment["GIT_CONFIG_COUNT"]);
    }

    [Fact]
    public void Without_bindings_there_is_no_redaction_and_no_GIT_CONFIG_PARAMETERS_key()
    {
        using var broker = Broker();
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask, "op-1");

        Assert.False(scope.Environment.ContainsKey("GIT_CONFIG_PARAMETERS"));
        Assert.False(scope.Environment.ContainsKey("GIT_TRACE_REDACT"));
        Assert.False(scope.Environment.ContainsKey("GIT_CONFIG_COUNT"));
    }

    [Theory]
    [InlineData("'core.quotepath'='false'", 1)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void An_inherited_command_line_configuration_is_left_out_with_one_debug_line_per_bound_scope(string? inherited, int lines)
    {
        var log = new ListLog();
        using var broker = Broker(session: new Dictionary<string, string?> { ["GIT_CONFIG_PARAMETERS"] = inherited }, log: log);
        using var bound = broker.Begin("Fetch from origin", "repo", PromptMode.Ask, "op-1", bindings: [Repo]);
        using var unbound = broker.Begin("Fetch from origin", "repo", PromptMode.Ask, "op-2");

        Assert.Null(bound.Environment["GIT_CONFIG_PARAMETERS"]);
        Assert.False(unbound.Environment.ContainsKey("GIT_CONFIG_PARAMETERS"));
        Assert.Equal(
            lines,
            log.Entries.Count(entry => entry == (LogLevel.Debug, "repo: Fetch from origin: the inherited command-line git configuration is left out of calls bound to the account.")));
    }

    private static AskPassBroker Broker(
        RecordingCredentialSource? source = null,
        IReadOnlyDictionary<string, string?>? session = null,
        Func<GitVersion?>? version = null,
        IAppLog? log = null) =>
        new(new RecordingPromptHandler(), log, credentials: source ?? new RecordingCredentialSource("t"), sessionEnvironment: session, gitVersion: version);

    /// <summary>A scope that is offered bindings but may not use them has exactly the variables of one offered none.</summary>
    private static void AssertToday(AskPassBroker broker, PromptMode mode)
    {
        var id = Guid.NewGuid().ToString("N");
        using var offered = broker.Begin("Fetch", "repo", mode, id + "-a", bindings: [Repo]);
        using var none = broker.Begin("Fetch", "repo", mode, id + "-b");

        var expected = new Dictionary<string, string?>(none.Environment);
        if (expected.ContainsKey(AskPassProtocol.OperationVariable) && expected[AskPassProtocol.OperationVariable] is not null)
        {
            expected[AskPassProtocol.OperationVariable] = offered.Id;
        }

        Assert.Equal(expected, offered.Environment);
    }

    private static List<(string Key, string Value)> Pairs(IReadOnlyDictionary<string, string?> environment, int first, int count) =>
        [.. Enumerable.Range(first, count).Select(i => (environment[$"GIT_CONFIG_KEY_{i}"]!, environment[$"GIT_CONFIG_VALUE_{i}"]!))];
}
