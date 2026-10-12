using System.IO.Pipes;
using System.Security.Principal;
using System.Text.RegularExpressions;
using VisualCommit.AskPass;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.PromptTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The askpass helper and <see cref="AskPassBroker"/> end to end, as git and ssh run them
/// (docs/phase-3-design.md 3.1, 3.4, 3.5): the real helper process, a real pipe, real git
/// (<c>git credential fill</c>, which asks through askpass when no credential helper answers) and
/// ssh-keygen on an encrypted key. The prompts are answered by a scripted handler.
/// </summary>
public class AskPassTests
{
    private const string Url = "https://example.com";

    [Fact]
    public void The_helper_is_in_the_output_folder_with_what_it_needs_to_start()
    {
        // Risk 8 of 7.7: the helper reaches every test project's output through the reference.
        var helper = AskPassLocator.Find();
        Assert.NotNull(helper);
        Assert.True(File.Exists(Path.Combine(AskPassLocator.Folder, "visualcommit-askpass.dll")));
        Assert.True(File.Exists(Path.Combine(AskPassLocator.Folder, "visualcommit-askpass.runtimeconfig.json")));
    }

    [Fact]
    public async Task Git_gets_the_user_name_and_the_password_the_handler_gives()
    {
        using var repo = await TempRepo.CreateAsync();
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"), PromptAnswer.Of("s3cret"));
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask, "op-askpass-1");

        var result = await GitAsync(repo, scope.Environment, ["credential", "fill"], CredentialRequest("https", "example.com"));

        Assert.True(result.Succeeded, result.StandardError);
        Assert.Equal("alice", CredentialValue(result.StandardOutput, "username"));
        Assert.Equal("s3cret", CredentialValue(result.StandardOutput, "password"));
        Assert.Collection(
            handler.Prompts,
            username =>
            {
                Assert.Equal(GitPromptKind.Username, username.Kind);
                Assert.Equal($"Username for '{Url}': ", username.Text);
                Assert.Equal(Url, username.Url);
                Assert.Equal(("op-askpass-1", "Fetch", "repo"), (username.ScopeId, username.Title, username.RepositoryName));
            },
            password =>
            {
                Assert.Equal(GitPromptKind.Password, password.Kind);
                Assert.Equal("Password for 'https://alice@example.com': ", password.Text);
                Assert.Equal("alice", password.User);
                Assert.Equal("op-askpass-1", password.ScopeId);
            });
        Assert.False(scope.UserCancelled);
    }

    [Fact]
    public async Task The_helper_works_from_a_folder_with_spaces_in_its_path()
    {
        // Git runs GIT_ASKPASS without a shell, the prompt as its one argument (section 0).
        using var repo = await TempRepo.CreateAsync();
        var helper = CopyHelperTo(repo.SiblingPath("folder with spaces"));
        var handler = new RecordingPromptHandler(PromptAnswer.Of("bob"), PromptAnswer.Of("pa ss"));
        using var broker = new AskPassBroker(handler, helperPath: helper);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask);

        Assert.Equal(helper, scope.Environment["GIT_ASKPASS"]);
        Assert.Contains(" ", helper, StringComparison.Ordinal);
        var result = await GitAsync(repo, scope.Environment, ["credential", "fill"], CredentialRequest("https", "example.com"));

        Assert.True(result.Succeeded, result.StandardError);
        Assert.Equal("bob", CredentialValue(result.StandardOutput, "username"));
        Assert.Equal("pa ss", CredentialValue(result.StandardOutput, "password"));
        Assert.Equal(2, handler.Prompts.Count);
    }

    [Fact]
    public async Task A_cancelled_prompt_fails_git_and_marks_the_scope_as_cancelled_by_the_user()
    {
        using var repo = await TempRepo.CreateAsync();
        var handler = new RecordingPromptHandler(PromptAnswer.Cancelled);
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask);

        var result = await GitAsync(repo, scope.Environment, ["credential", "fill"], CredentialRequest("https", "example.com"));

        Assert.False(result.Succeeded);
        Assert.Contains("could not read Username", result.StandardError, StringComparison.Ordinal);
        Assert.Single(handler.Prompts);
        Assert.True(scope.UserCancelled);
    }

    [Fact]
    public async Task A_refused_prompt_fails_git_without_marking_a_cancel()
    {
        using var repo = await TempRepo.CreateAsync();
        var handler = new RecordingPromptHandler(PromptAnswer.Refused);
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask);

        var result = await GitAsync(repo, scope.Environment, ["credential", "fill"], CredentialRequest("https", "example.com"));

        Assert.False(result.Succeeded);
        Assert.Contains("could not read Username", result.StandardError, StringComparison.Ordinal);
        Assert.Single(handler.Prompts);
        Assert.False(scope.UserCancelled);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("operation")]
    [InlineData("closed scope")]
    public async Task A_request_with_a_wrong_token_or_without_an_open_scope_is_refused_without_asking(string wrong)
    {
        using var repo = await TempRepo.CreateAsync();
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"), PromptAnswer.Of("s3cret"));
        using var broker = new AskPassBroker(handler);
        var scope = broker.Begin("Fetch", "repo", PromptMode.Ask);
        var environment = new Dictionary<string, string?>(scope.Environment);
        switch (wrong)
        {
            case "token":
                var token = environment[AskPassProtocol.TokenVariable]!;
                environment[AskPassProtocol.TokenVariable] = (token[0] == 'a' ? "b" : "a") + token[1..];
                break;
            case "operation":
                environment[AskPassProtocol.OperationVariable] = "op-unknown";
                break;
            default:
                scope.Dispose();
                break;
        }

        var result = await GitAsync(repo, environment, ["credential", "fill"], CredentialRequest("https", "example.com"));
        scope.Dispose();

        Assert.False(result.Succeeded);
        Assert.Contains("could not read Username", result.StandardError, StringComparison.Ordinal);
        Assert.Empty(handler.Prompts);
    }

    [Fact]
    public async Task A_deny_scope_fails_every_prompt_at_once_without_asking()
    {
        using var repo = await TempRepo.CreateAsync();
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"), PromptAnswer.Of("s3cret"));
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Auto-fetch", "repo", PromptMode.Deny);

        Assert.Equal(broker.HelperPath, scope.Environment["GIT_ASKPASS"]);
        Assert.Equal(broker.HelperPath, scope.Environment["SSH_ASKPASS"]);
        Assert.Equal("force", scope.Environment["SSH_ASKPASS_REQUIRE"]);
        Assert.Equal("never", scope.Environment["GCM_INTERACTIVE"]);
        Assert.Equal("1", scope.Environment[AskPassProtocol.DenyVariable]);
        Assert.Null(scope.Environment[AskPassProtocol.PipeVariable]);
        Assert.Null(scope.Environment[AskPassProtocol.TokenVariable]);

        var result = await GitAsync(repo, scope.Environment, ["credential", "fill"], CredentialRequest("https", "example.com"));

        Assert.False(result.Succeeded);
        Assert.Contains("could not read Username", result.StandardError, StringComparison.Ordinal);
        Assert.Empty(handler.Prompts);
    }

    [Fact]
    public void Each_mode_gives_the_variables_of_3_1()
    {
        using var broker = new AskPassBroker(new RecordingPromptHandler());
        using var ask = broker.Begin("Push main", "repo", PromptMode.Ask, "op-77");
        using var none = broker.Begin("Stage", "repo", PromptMode.None, "op-78");

        Assert.Equal(broker.HelperPath, ask.Environment["GIT_ASKPASS"]);
        Assert.Equal(broker.HelperPath, ask.Environment["SSH_ASKPASS"]);
        Assert.Equal("force", ask.Environment["SSH_ASKPASS_REQUIRE"]);
        Assert.Equal(broker.PipeName, ask.Environment[AskPassProtocol.PipeVariable]);
        Assert.Matches("^vc-[0-9a-f]{16}$", broker.PipeName);
        Assert.Matches("^[0-9a-f]{64}$", ask.Environment[AskPassProtocol.TokenVariable]);
        Assert.Equal("op-77", ask.Environment[AskPassProtocol.OperationVariable]);
        Assert.Equal("op-77", ask.Id);
        Assert.False(ask.Environment.ContainsKey("GCM_INTERACTIVE"));
        Assert.Empty(none.Environment);
        Assert.Equal("op-78", none.Id);

        // A clone opens its scope without an id: the broker makes one.
        using var clone = broker.Begin("Clone", "x", PromptMode.Ask);
        Assert.StartsWith("prompt-", clone.Id, StringComparison.Ordinal);
        Assert.Equal(clone.Id, clone.Environment[AskPassProtocol.OperationVariable]);
        Assert.Throws<InvalidOperationException>(() => broker.Begin("Push main", "repo", PromptMode.Ask, "op-77"));
    }

    [Fact]
    public async Task Without_the_app_the_helper_gives_up_and_git_fails()
    {
        using var repo = await TempRepo.CreateAsync();
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"));
        var broker = new AskPassBroker(handler);
        var environment = broker.Begin("Fetch", "repo", PromptMode.Ask).Environment;
        broker.Dispose();

        var result = await GitAsync(repo, environment, ["credential", "fill"], CredentialRequest("https", "example.com"));

        Assert.False(result.Succeeded);
        Assert.Contains("could not read Username", result.StandardError, StringComparison.Ordinal);
        Assert.Empty(handler.Prompts);
    }

    [Fact]
    public async Task Closing_the_scope_closes_its_open_prompt_and_git_fails()
    {
        using var repo = await TempRepo.CreateAsync();
        var handler = new RecordingPromptHandler().ThenHold();
        using var broker = new AskPassBroker(handler);
        var scope = broker.Begin("Fetch", "repo", PromptMode.Ask);

        var fill = GitAsync(repo, scope.Environment, ["credential", "fill"], CredentialRequest("https", "example.com"));
        await handler.HoldBegan.Task.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);
        scope.Dispose();
        await handler.HeldPromptClosed.Task.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);
        var result = await fill.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);

        Assert.False(result.Succeeded);
        Assert.Contains("could not read Username", result.StandardError, StringComparison.Ordinal);
        Assert.False(scope.UserCancelled);
    }

    [Fact]
    public async Task Stopping_git_while_it_asks_returns_at_once_and_the_scope_closes_the_prompt()
    {
        using var repo = await TempRepo.CreateAsync();
        var handler = new RecordingPromptHandler().ThenHold();
        using var broker = new AskPassBroker(handler);
        var scope = broker.Begin("Fetch", "repo", PromptMode.Ask);
        using var cancel = new CancellationTokenSource();

        var fill = StoppableGitAsync(repo, scope.Environment, ["credential", "fill"], CredentialRequest("https", "example.com"), null, cancel.Token);
        await handler.HoldBegan.Task.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);
        await cancel.CancelAsync();

        // On Windows the runner stops git and the helper with it; on macOS and Linux only git
        // (D37), and the helper waits until the operation's scope ends.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fill.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled));
        scope.Dispose();
        await handler.HeldPromptClosed.Task.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);
    }

    [Fact]
    public async Task Run_as_ssh_runs_it_the_helper_passes_the_prompt_kind_and_prints_the_answer_with_a_line_end()
    {
        // As OpenSSH runs it for ssh-agent's confirmation: SSH_ASKPASS_PROMPT=confirm (3.2).
        var handler = new RecordingPromptHandler(PromptAnswer.Of("yes"), PromptAnswer.Refused);
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask);
        var environment = new Dictionary<string, string?>(scope.Environment) { [AskPassProtocol.SshPromptVariable] = "confirm" };

        var confirmed = await RunProgramAsync(broker.HelperPath, environment, "Allow use of key /home/u/.ssh/id_ed25519?");
        var refused = await RunProgramAsync(broker.HelperPath, environment, "Allow use of key /home/u/.ssh/id_ed25519?");

        Assert.Equal((0, "yes\n"), (confirmed.ExitCode, confirmed.Output));
        Assert.Equal((1, string.Empty), (refused.ExitCode, refused.Output));
        Assert.All(handler.Prompts, prompt => Assert.Equal(GitPromptKind.Confirm, prompt.Kind));
        Assert.Equal(2, handler.Prompts.Count);
    }

    [Fact]
    public async Task When_the_helper_goes_away_its_open_prompt_closes()
    {
        var handler = new RecordingPromptHandler().ThenHold();
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask);
        var startInfo = new System.Diagnostics.ProcessStartInfo(broker.HelperPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        startInfo.ArgumentList.Add("Enter passphrase for key '/home/u/.ssh/id_ed25519': ");
        foreach (var (name, value) in scope.Environment)
        {
            startInfo.Environment[name] = value;
        }

        using var helper = System.Diagnostics.Process.Start(startInfo)!;
        try
        {
            await handler.HoldBegan.Task.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);
        }
        finally
        {
            helper.Kill();
        }

        await handler.HeldPromptClosed.Task.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);
        var prompt = Assert.Single(handler.Prompts);
        Assert.Equal((GitPromptKind.Passphrase, "/home/u/.ssh/id_ed25519"), (prompt.Kind, prompt.KeyPath));
        Assert.False(scope.IsDisposed);
    }

    [Fact]
    public async Task Several_prompts_at_once_in_different_scopes_are_all_answered()
    {
        // More helpers than the pipe has instances (four): the others wait for a free one.
        using var repo = await TempRepo.CreateAsync();
        var handler = new AnswerByScopeHandler();
        using var broker = new AskPassBroker(handler);
        var scopes = Enumerable.Range(1, AskPassBroker.MaxConnections + 2)
            .Select(n => broker.Begin("Fetch", "repo", PromptMode.Ask, $"op-many-{n}"))
            .ToList();
        try
        {
            var results = await Task.WhenAll(scopes.Select(scope =>
                GitAsync(repo, scope.Environment, ["credential", "fill"], CredentialRequest("https", "example.com"))));

            for (var i = 0; i < scopes.Count; i++)
            {
                Assert.True(results[i].Succeeded, results[i].StandardError);
                Assert.Equal($"user-op-many-{i + 1}", CredentialValue(results[i].StandardOutput, "username"));
                Assert.Equal($"secret-op-many-{i + 1}", CredentialValue(results[i].StandardOutput, "password"));
            }
        }
        finally
        {
            scopes.ForEach(scope => scope.Dispose());
        }
    }

    [Fact]
    public async Task Ssh_keygen_asks_for_an_encrypted_keys_passphrase_through_the_helper()
    {
        // OpenSSH 8.4 and later use SSH_ASKPASS with SSH_ASKPASS_REQUIRE=force even without a
        // display, and ssh-keygen -y reads the passphrase of an encrypted key (3.5). On Windows
        // this is Git for Windows' ssh-keygen, an MSYS2 program: the helper it starts runs with
        // the user as its token's default owner, which an elevated test run (CI's) does not
        // have (WindowsPipeUser).
        var keygen = FindSshKeygen();
        using var folder = new TempDirectory("ssh");
        var key = folder.Combine("id_test");
        var created = await RunProgramAsync(keygen, NoAskPass(), "-q", "-t", "ed25519", "-N", "s3cret", "-C", "visualcommit-test", "-f", key);
        Assert.True(created.ExitCode == 0, created.Error);

        var handler = new RecordingPromptHandler(PromptAnswer.Of("s3cret"));
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask);
        var environment = new Dictionary<string, string?>(NoAskPass());
        foreach (var (name, value) in scope.Environment)
        {
            environment[name] = value;
        }

        var shown = await RunProgramAsync(keygen, environment, "-y", "-f", key);

        // The prompt first: when it does not reach the app, ssh-keygen only says that the
        // passphrase was wrong (the empty one it uses when the helper gives none).
        Assert.True(
            handler.Prompts.Count == 1,
            $"{handler.Prompts.Count} prompts reached the app; {keygen} exited with {shown.ExitCode}: {shown.Error}");
        var prompt = handler.Prompts[0];
        Assert.Equal(GitPromptKind.Passphrase, prompt.Kind);
        Assert.StartsWith("Enter passphrase", prompt.Text, StringComparison.Ordinal);
        Assert.True(shown.ExitCode == 0, shown.Error);
        Assert.StartsWith("ssh-ed25519 ", shown.Output, StringComparison.Ordinal);
        Assert.Equal(File.ReadAllText(key + ".pub").Split(' ')[1], shown.Output.Split(' ')[1]);
    }

    [Fact]
    public async Task Ssh_keygen_fails_at_once_in_a_deny_scope()
    {
        var keygen = FindSshKeygen();
        using var folder = new TempDirectory("ssh");
        var key = folder.Combine("id_test");
        var created = await RunProgramAsync(keygen, NoAskPass(), "-q", "-t", "ed25519", "-N", "s3cret", "-f", key);
        Assert.True(created.ExitCode == 0, created.Error);

        var handler = new RecordingPromptHandler(PromptAnswer.Of("s3cret"));
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Auto-fetch", "repo", PromptMode.Deny);
        var environment = new Dictionary<string, string?>(NoAskPass());
        foreach (var (name, value) in scope.Environment)
        {
            environment[name] = value;
        }

        var shown = await RunProgramAsync(keygen, environment, "-y", "-f", key);

        Assert.NotEqual(0, shown.ExitCode);
        Assert.DoesNotContain("ssh-ed25519", shown.Output, StringComparison.Ordinal);
        Assert.Empty(handler.Prompts);
    }

    [Fact]
    public async Task The_pipe_is_a_short_socket_path_on_macOS_and_Linux()
    {
        // Risk 9 of 7.7: a Unix socket's path is limited to 104 characters on macOS.
        using var broker = new AskPassBroker(new RecordingPromptHandler());
        if (OperatingSystem.IsWindows())
        {
            Assert.Null(broker.SocketPath);
            return;
        }

        var socket = broker.SocketPath!;
        Assert.True(socket.Length < 104, $"{socket} is {socket.Length} characters long.");
        var deadline = DateTime.UtcNow + StepTimeout;
        while (!File.Exists(socket) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, RepositoryTestSupport.TestCancelled);
        }

        Assert.True(File.Exists(socket), $"No socket at {socket}.");
    }

    [Fact]
    public async Task On_Windows_the_pipe_belongs_to_the_user_not_to_the_tokens_default_owner()
    {
        // In an elevated process (an app run as administrator, CI's runner) the token's default
        // owner is the Administrators group, while a helper that an MSYS2 program such as Git
        // for Windows' ssh starts has the user (WindowsPipeUser). Both share the user, so the
        // pipe must belong to it; where process and user are the same, this proves less.
        using var broker = new AskPassBroker(new RecordingPromptHandler());
        if (!OperatingSystem.IsWindows())
        {
            // A Unix socket, whose user .NET checks itself.
            Assert.NotNull(broker.SocketPath);
            return;
        }

        using var client = new NamedPipeClientStream(".", broker.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(StepTimeout, RepositoryTestSupport.TestCancelled);

        using var identity = WindowsIdentity.GetCurrent();
        Assert.Equal(identity.User, client.GetAccessControl().GetOwner(typeof(SecurityIdentifier)));
        Assert.True(WindowsPipeUser.OwnsPipe(client));
    }

    [Fact]
    public async Task Neither_the_answers_nor_the_token_reach_a_call_record_or_the_log()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var calls = new GitCallLog();
        var log = new ListLog();
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"), PromptAnswer.Of("s3cret"));
        using var broker = new AskPassBroker(handler, log);
        using var scope = broker.Begin("Fetch", "repo", PromptMode.Ask);
        var runner = RepositoryTestSupport.NewRunner(calls, log);

        var fetch = await GitAsync(repo, scope.Environment, ["fetch", "origin"], runner: runner);
        var refused = await GitAsync(repo, scope.Environment, ["ls-remote", "origin"], runner: runner);

        Assert.True(fetch.Succeeded, fetch.StandardError);
        Assert.False(refused.Succeeded);
        AssertNowhere("s3cret", calls, log);
        AssertNowhere(scope.Environment[AskPassProtocol.TokenVariable]!, calls, log);
        Assert.Contains(log.Entries, entry => Regex.IsMatch(entry.Message, "git asks \\(Username\\)"));
    }

    private static Dictionary<string, string?> NoAskPass() => new()
    {
        ["SSH_ASKPASS"] = null,
        ["SSH_ASKPASS_REQUIRE"] = null,
        ["SSH_ASKPASS_PROMPT"] = null,
        ["DISPLAY"] = null,
        ["WAYLAND_DISPLAY"] = null,
        ["GIT_ASKPASS"] = null,
    };

    /// <summary>Answers every prompt with a value made of its scope's id.</summary>
    private sealed class AnswerByScopeHandler : IGitPromptHandler
    {
        public Task<PromptAnswer> AnswerAsync(GitPrompt prompt, CancellationToken cancellationToken) =>
            Task.FromResult(PromptAnswer.Of(
                prompt.Kind == GitPromptKind.Username ? "user-" + prompt.ScopeId : "secret-" + prompt.ScopeId));
    }
}
