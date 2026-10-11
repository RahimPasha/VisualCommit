using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// <see cref="AskPassRunner"/>, which runs the askpass helper as OpenSSH would (design 3.5). The
/// helper itself is work package WP3's; until it exists these tests run git in its place, which
/// shows how the runner passes the argument and the environment and collects the output.
/// </summary>
public class AskPassRunnerTests
{
    [Fact]
    public async Task The_program_gets_the_scopes_environment_and_its_output_is_collected()
    {
        using var repo = await TempRepo.CreateAsync();
        var marker = repo.SiblingPath("exec path with spaces");

        // `git --exec-path` prints GIT_EXEC_PATH as the environment gives it.
        var result = await AskPassRunner.RunAsync(
            new Dictionary<string, string?> { ["GIT_EXEC_PATH"] = marker },
            "--exec-path",
            helperPath: GitPath(repo),
            cancellationToken: TestCancelled);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(marker, result.Answer);
        Assert.Equal(marker + "\n", result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_prompt_is_one_argument_whatever_it_holds_and_a_failure_has_no_answer()
    {
        using var repo = await TempRepo.CreateAsync();

        // An unknown command: git repeats it, as it got it, and exits with an error.
        var result = await AskPassRunner.RunAsync(new Dictionary<string, string?>(), FakeSsh.HostKeyPrompt, helperPath: GitPath(repo), cancellationToken: TestCancelled);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Null(result.Answer);
        Assert.Contains("'" + FakeSsh.HostKeyPrompt + "'", result.StandardError.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void SSH_ASKPASS_PROMPT_is_set_only_when_ssh_would_set_it_and_the_scope_wins_over_the_process()
    {
        var environment = new Dictionary<string, string?>
        {
            ["VISUALCOMMIT_ASKPASS_PIPE"] = "vc-0123456789abcdef",
            ["VISUALCOMMIT_ASKPASS_TOKEN"] = "token",
            ["VISUALCOMMIT_OPERATION_ID"] = "op-7",
            ["SSH_ASKPASS_PROMPT"] = "none",
            ["GIT_DIR"] = null,
        };

        var confirm = AskPassRunner.CreateStartInfo(environment, "Allow use of key?", "confirm", helperPath: "helper");
        var plain = AskPassRunner.CreateStartInfo(environment, FakeSsh.PassphrasePrompt, helperPath: "helper");

        Assert.Equal("helper", confirm.FileName);
        Assert.Equal(["Allow use of key?"], confirm.ArgumentList);
        Assert.Equal([FakeSsh.PassphrasePrompt], plain.ArgumentList);
        Assert.Equal("confirm", confirm.Environment["SSH_ASKPASS_PROMPT"]);
        Assert.False(plain.Environment.ContainsKey("SSH_ASKPASS_PROMPT"));
        Assert.All([confirm, plain], startInfo =>
        {
            Assert.Equal("vc-0123456789abcdef", startInfo.Environment["VISUALCOMMIT_ASKPASS_PIPE"]);
            Assert.Equal("token", startInfo.Environment["VISUALCOMMIT_ASKPASS_TOKEN"]);
            Assert.Equal("op-7", startInfo.Environment["VISUALCOMMIT_OPERATION_ID"]);
            Assert.False(startInfo.Environment.ContainsKey("GIT_DIR"));
            Assert.False(startInfo.UseShellExecute);
            Assert.True(startInfo.RedirectStandardInput);
        });
    }

    [Fact]
    public async Task A_scope_gives_its_environment_and_a_closed_one_is_refused()
    {
        using var repo = await TempRepo.CreateAsync();
        var scope = NullGitPromptBroker.Instance.Begin("Fetch fake-ssh", "repo", PromptMode.Ask, "op-1");

        var result = await AskPassRunner.RunAsync(scope, "--version", helperPath: GitPath(repo), cancellationToken: TestCancelled);
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("git version ", result.Answer, StringComparison.Ordinal);

        scope.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => AskPassRunner.RunAsync(scope, "--version", helperPath: GitPath(repo), cancellationToken: TestCancelled));
    }

    [Fact]
    public async Task The_helper_is_looked_for_next_to_the_tests_and_a_missing_one_is_named()
    {
        var expected = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "visualcommit-askpass.exe" : "visualcommit-askpass");
        Assert.Equal(expected, AskPassRunner.HelperPath);

        var missing = Path.Combine(AppContext.BaseDirectory, "no-such-helper" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
        var error = await Assert.ThrowsAsync<FileNotFoundException>(() => AskPassRunner.RunAsync(new Dictionary<string, string?>(), "Password: ", helperPath: missing, cancellationToken: TestCancelled));
        Assert.Equal(missing, error.FileName);
    }

    private static string GitPath(TempRepo repo) => ((GitRunner)repo.Runner).ExecutablePath;
}
