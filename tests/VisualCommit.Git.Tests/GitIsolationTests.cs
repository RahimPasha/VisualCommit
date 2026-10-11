using System.Diagnostics;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The tests' isolation from what the machine adds to git's environment beyond its configuration
/// (D112): askpass programs, ssh commands, proxies and auto-fetch, in the test process, in
/// <see cref="TempRepo"/>'s git calls and in a process the tests start (the real-window pass's app).
/// </summary>
public class GitIsolationTests
{
    [Fact]
    public void The_test_process_has_no_askpass_program_ssh_command_or_proxy_and_no_auto_fetch()
    {
        Assert.All(GitIsolation.PromptVariables, variable => Assert.Null(Environment.GetEnvironmentVariable(variable)));
        Assert.All(GitIsolation.ProxyVariables, variable => Assert.Null(Environment.GetEnvironmentVariable(variable)));
        Assert.Equal("127.0.0.1,localhost", Environment.GetEnvironmentVariable("no_proxy"));
        Assert.Equal("127.0.0.1,localhost", Environment.GetEnvironmentVariable("NO_PROXY"));
        Assert.Equal("0", Environment.GetEnvironmentVariable("VISUALCOMMIT_AUTO_FETCH_MINUTES"));
    }

    [Fact]
    public void D112s_variables_are_all_named()
    {
        Assert.Equal(
            ["GIT_ASKPASS", "SSH_ASKPASS", "SSH_ASKPASS_REQUIRE", "GCM_INTERACTIVE", "GIT_SSH", "GIT_SSH_COMMAND", "GIT_SSH_VARIANT"],
            GitIsolation.PromptVariables);
        Assert.Equal(["http_proxy", "https_proxy", "all_proxy", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY"], GitIsolation.ProxyVariables);
        Assert.Equal("VISUALCOMMIT_AUTO_FETCH_MINUTES", GitIsolation.AutoFetchMinutesVariable);
    }

    [Fact]
    public async Task A_temp_repos_git_calls_get_the_same_isolation()
    {
        using var repo = await TempRepo.CreateAsync();

        Assert.All(GitIsolation.PromptVariables, variable => Assert.Null(repo.Environment[variable]));
        Assert.All(GitIsolation.ProxyVariables, variable => Assert.Null(repo.Environment[variable]));
        Assert.Equal("127.0.0.1,localhost", repo.Environment["no_proxy"]);
        Assert.Equal("127.0.0.1,localhost", repo.Environment["NO_PROXY"]);
    }

    [Fact]
    public void A_started_process_loses_the_machines_variables_and_gets_the_tests_own()
    {
        // What RealApp.Launch does to the app's environment, on a ProcessStartInfo as it does.
        var startInfo = new ProcessStartInfo("unused");
        startInfo.Environment["GIT_ASKPASS"] = "C:/Program Files/Microsoft VS Code/askpass.sh";
        startInfo.Environment["SSH_ASKPASS"] = "/usr/lib/ssh/x11-ssh-askpass";
        startInfo.Environment["SSH_ASKPASS_REQUIRE"] = "prefer";
        startInfo.Environment["GCM_INTERACTIVE"] = "always";
        startInfo.Environment["GIT_SSH"] = "C:/Program Files/TortoiseGit/bin/TortoiseGitPlink.exe";
        startInfo.Environment["http_proxy"] = "http://proxy.example:3128";
        startInfo.Environment["HTTPS_PROXY"] = "http://proxy.example:3128";
        startInfo.Environment["all_proxy"] = "socks5://proxy.example:1080";
        startInfo.Environment["no_proxy"] = "example.com";
        startInfo.Environment["VISUALCOMMIT_AUTO_FETCH_MINUTES"] = "5";
        startInfo.Environment["PATH_TO_KEEP"] = "kept";

        GitIsolation.ApplyMachineIndependence(startInfo.Environment);

        var names = startInfo.Environment.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All([.. GitIsolation.PromptVariables, .. GitIsolation.ProxyVariables], variable => Assert.DoesNotContain(variable, names));
        Assert.Equal("127.0.0.1,localhost", startInfo.Environment["no_proxy"]);
        Assert.Equal("127.0.0.1,localhost", startInfo.Environment["NO_PROXY"]);
        Assert.Equal("0", startInfo.Environment["VISUALCOMMIT_AUTO_FETCH_MINUTES"]);
        Assert.Equal("kept", startInfo.Environment["PATH_TO_KEEP"]);
    }

    [Fact]
    public async Task A_proxy_of_the_machine_no_longer_carries_a_request_for_the_test_server()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        server.RequireAuth = false;

        // A machine whose proxy (here a port nothing listens on) would carry every request. Both
        // cases are named: on Windows they are one variable, and the dictionary's later entry wins.
        var machine = new Dictionary<string, string?>(repo.Environment)
        {
            ["http_proxy"] = "http://127.0.0.1:1",
            ["HTTP_PROXY"] = "http://127.0.0.1:1",
            ["no_proxy"] = null,
            ["NO_PROXY"] = null,
        };
        var throughProxy = await LsRemoteAsync(repo, machine);
        Assert.False(throughProxy.Succeeded, "git reached the server although the proxy it was told to use does not exist.");
        Assert.Empty(server.Requests);

        GitIsolation.ApplyMachineIndependence(machine);
        var isolated = await LsRemoteAsync(repo, machine);

        Assert.True(isolated.Succeeded, isolated.StandardError);
        Assert.EndsWith("\trefs/heads/main", isolated.StandardOutput.Trim(), StringComparison.Ordinal);
        Assert.NotEmpty(server.Requests);
        Assert.Empty(server.Errors);
    }

    private static Task<GitResult> LsRemoteAsync(TempRepo repo, IReadOnlyDictionary<string, string?> environment) =>
        repo.Runner.RunAsync(
            new GitCommand("ls-remote", "origin", "refs/heads/main") { WorkingDirectory = repo.Path, Environment = environment },
            TestCancelled);
}
