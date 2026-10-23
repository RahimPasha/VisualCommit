using System.Diagnostics;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The tests' isolation from what the machine adds to git's environment beyond its configuration
/// (D112): askpass programs, ssh commands, proxies, editors (D151) and auto-fetch, in the test process, in
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
        Assert.Equal(["EDITOR", "VISUAL", "GIT_EDITOR", "GIT_SEQUENCE_EDITOR"], GitIsolation.EditorVariables);
        Assert.Equal("VISUALCOMMIT_AUTO_FETCH_MINUTES", GitIsolation.AutoFetchMinutesVariable);
    }

    [Fact]
    public async Task No_editor_of_the_machine_reaches_the_test_process_or_a_temp_repos_calls()
    {
        Assert.All(GitIsolation.EditorVariables, variable => Assert.Null(Environment.GetEnvironmentVariable(variable)));

        using var repo = await TempRepo.CreateAsync();
        Assert.Null(repo.Environment["EDITOR"]);
        Assert.Null(repo.Environment["VISUAL"]);
        Assert.Equal(":", repo.Environment["GIT_EDITOR"]);
        Assert.Equal(":", repo.Environment["GIT_SEQUENCE_EDITOR"]);
    }

    [Fact]
    public async Task A_temp_repos_rebase_continue_after_a_conflict_does_not_wait_for_an_editor()
    {
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("f.txt", "base\n", "Initial commit");
        await repo.CreateBranchAsync("topic");
        await repo.CommitFileAsync("f.txt", "main\n", "Main edit");
        await repo.CheckoutAsync("topic");
        await repo.CommitFileAsync("f.txt", "topic\n", "Topic edit");
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("rebase", "main")).ExitCode);
        repo.WriteFile("f.txt", "resolved\n");
        await repo.GitAsync("add", "f.txt");

        // A machine whose editors would fail (or open a window and wait): the temp repo's own
        // environment, applied over it, wins, as it does for any process its variables are given.
        var machine = GitIsolation.EditorVariables.ToDictionary(variable => variable, _ => (string?)"false");
        foreach (var (variable, value) in repo.Environment)
        {
            machine[variable] = value;
        }

        var command = new GitCommand("rebase", "--continue") { WorkingDirectory = repo.Path, Environment = machine };
        var result = await repo.Runner.RunAsync(command, TestCancelled);

        Assert.True(result.Succeeded, result.StandardError);
        Assert.False(Directory.Exists(Path.Combine(repo.Path, ".git", "rebase-merge")));
        Assert.Equal("Topic edit", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.Equal("resolved\n", File.ReadAllText(Path.Combine(repo.Path, "f.txt")));
    }

    [Fact]
    public async Task The_shell_that_started_the_tests_does_not_reach_them_and_git_sets_its_own()
    {
        Assert.Equal(["MSYSTEM"], GitIsolation.ShellVariables);
        Assert.Null(Environment.GetEnvironmentVariable("MSYSTEM"));

        var startInfo = new ProcessStartInfo("unused");
        startInfo.Environment["MSYSTEM"] = "MINGW64";
        GitIsolation.ApplyMachineIndependence(startInfo.Environment);
        Assert.DoesNotContain("MSYSTEM", startInfo.Environment.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase));

        // A shell git starts for an alias still runs without it; on Windows git sets it again.
        // The alias has a name of its own: git never lets an alias stand for a built-in command,
        // and `git shell` is one where git-shell is installed (Linux, macOS).
        using var repo = await TempRepo.CreateAsync();
        Assert.Null(repo.Environment["MSYSTEM"]);
        var command = new GitCommand("-c", "alias.vc-shell-ran=!echo \"shell ran: $MSYSTEM\"", "vc-shell-ran") { WorkingDirectory = repo.Path, Environment = repo.Environment };
        var result = await repo.Runner.RunAsync(command, TestCancelled);
        Assert.True(result.Succeeded, result.StandardError);
        Assert.StartsWith("shell ran:", result.StandardOutput.Trim(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_started_process_loses_the_machines_editors()
    {
        var startInfo = new ProcessStartInfo("unused");
        startInfo.Environment["EDITOR"] = "vim";
        startInfo.Environment["VISUAL"] = "code --wait";
        startInfo.Environment["GIT_EDITOR"] = "notepad";
        startInfo.Environment["GIT_SEQUENCE_EDITOR"] = "interactive-rebase-tool";

        GitIsolation.ApplyMachineIndependence(startInfo.Environment);

        var names = startInfo.Environment.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(GitIsolation.EditorVariables, variable => Assert.DoesNotContain(variable, names));
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

    [Fact]
    public async Task The_machines_identity_reaches_no_test_while_the_app_commit_dates_stay()
    {
        Assert.Equal(["GIT_AUTHOR_NAME", "GIT_AUTHOR_EMAIL", "GIT_COMMITTER_NAME", "GIT_COMMITTER_EMAIL", "EMAIL"], GitIsolation.IdentityVariables);
        Assert.All(GitIsolation.IdentityVariables, variable => Assert.Null(Environment.GetEnvironmentVariable(variable)));
        Assert.Equal("2026-01-02T09:00:00+00:00", Environment.GetEnvironmentVariable("GIT_AUTHOR_DATE"));
        Assert.Equal("2026-01-02T09:00:00+00:00", Environment.GetEnvironmentVariable("GIT_COMMITTER_DATE"));
        Assert.Equal(".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC", Environment.GetEnvironmentVariable("PATHEXT"));

        // What RealApp.Launch does to the app's environment: the machine's identity goes, the
        // harness's dates stay, PATHEXT is Windows' default.
        var startInfo = new ProcessStartInfo("unused");
        startInfo.Environment["GIT_AUTHOR_NAME"] = "Machine Author";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "machine@example.com";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "Machine Committer";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "committer@example.com";
        startInfo.Environment["EMAIL"] = "someone@example.com";
        startInfo.Environment["GIT_AUTHOR_DATE"] = "2026-01-02T09:00:00+00:00";
        startInfo.Environment["PATHEXT"] = ".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC;.PY;.PYW;.CPL";
        GitIsolation.ApplyMachineIndependence(startInfo.Environment);
        var names = startInfo.Environment.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(GitIsolation.IdentityVariables, variable => Assert.DoesNotContain(variable, names));
        Assert.Equal("2026-01-02T09:00:00+00:00", startInfo.Environment["GIT_AUTHOR_DATE"]);
        Assert.Equal(".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC", startInfo.Environment["PATHEXT"]);

        // A temp repo's calls lose the machine's identity and set their own after it, so its
        // commits keep the scenarios' author.
        using var repo = await TempRepo.CreateAsync();
        Assert.Null(repo.Environment["EMAIL"]);
        Assert.Equal(TempRepo.AuthorName, repo.Environment["GIT_AUTHOR_NAME"]);
        Assert.Equal(TempRepo.AuthorEmail, repo.Environment["GIT_COMMITTER_EMAIL"]);
        await repo.CommitFileAsync("f.txt", "f\n", "Initial commit");
        Assert.Equal(
            $"{TempRepo.AuthorName} <{TempRepo.AuthorEmail}> {TempRepo.AuthorName} <{TempRepo.AuthorEmail}>",
            (await repo.GitAsync("log", "-1", "--format=%an <%ae> %cn <%ce>")).StandardOutput.Trim());
    }

    [Fact]
    public async Task The_test_process_reads_no_global_configuration_and_cannot_write_one()
    {
        // The safety net (docs/phase-6-design.md 17.2): a global file in a folder nobody makes and
        // an empty XDG folder of the tests'.
        Assert.Equal(GitIsolation.NoGlobalConfigFile, Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL"));
        Assert.Equal(Path.Combine(GitIsolation.CeilingDirectory, "isolation", "no-global", "gitconfig"), GitIsolation.NoGlobalConfigFile);
        Assert.Equal(GitIsolation.ProcessXdgConfigHome, Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"));
        Assert.Equal(Path.Combine(GitIsolation.CeilingDirectory, "isolation", "xdg"), GitIsolation.ProcessXdgConfigHome);
        Assert.True(Directory.Exists(GitIsolation.ProcessXdgConfigHome));

        // A git call that inherits the process's environment, as the app's calls in a test do.
        using var folder = new TempDirectory("no-global");
        var runner = new GitRunner(TempRepo.GitPath);
        var list = await runner.RunAsync(new GitCommand("config", "--list", "--show-scope") { WorkingDirectory = folder.Path }, TestCancelled);
        Assert.True(list.Succeeded, list.StandardError);
        Assert.DoesNotContain("global\t", list.StandardOutput, StringComparison.Ordinal);

        var read = await runner.RunAsync(new GitCommand("config", "--global", "user.name") { WorkingDirectory = folder.Path }, TestCancelled);
        Assert.Equal(1, read.ExitCode);

        // A write fails where it is made, and makes no folder for the next test to read.
        var write = await runner.RunAsync(new GitCommand("config", "--global", "vc.tripwire", "written") { WorkingDirectory = folder.Path }, TestCancelled);
        Assert.Equal(255, write.ExitCode);
        Assert.False(Directory.Exists(Path.GetDirectoryName(GitIsolation.NoGlobalConfigFile)));
    }

    [Fact]
    public void The_tripwire_names_each_trace_of_a_global_configuration_written_outside_a_session()
    {
        // The rules over a folder of this test's, laid out as the tests' isolation folder: the
        // one every test process shares is never touched.
        using var isolation = new TempDirectory("tripwire");
        Directory.CreateDirectory(isolation.Combine("xdg"));
        File.WriteAllText(isolation.Combine("gitconfig"), string.Empty);
        Assert.Empty(GitIsolation.GlobalConfigurationWrittenOutsideASession(isolation.Path));

        Directory.CreateDirectory(isolation.Combine("no-global"));
        var noGlobal = Assert.Single(GitIsolation.GlobalConfigurationWrittenOutsideASession(isolation.Path));
        Assert.StartsWith(isolation.Combine("no-global") + " exists", noGlobal, StringComparison.Ordinal);

        Directory.Delete(isolation.Combine("no-global"));
        Directory.CreateDirectory(Path.Combine(isolation.Combine("xdg"), "git"));
        var xdg = Assert.Single(GitIsolation.GlobalConfigurationWrittenOutsideASession(isolation.Path));
        Assert.StartsWith(Path.Combine(isolation.Combine("xdg"), "git") + " exists", xdg, StringComparison.Ordinal);

        Directory.Delete(Path.Combine(isolation.Combine("xdg"), "git"));
        File.WriteAllText(isolation.Combine("gitconfig"), "[user]\n\tname = Stale\n");
        var stale = Assert.Single(GitIsolation.GlobalConfigurationWrittenOutsideASession(isolation.Path));
        Assert.StartsWith(isolation.Combine("gitconfig") + " is not empty", stale, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sessions_git_environment_puts_its_global_configuration_in_its_data_folder()
    {
        using var data = new TempDirectory("data");
        var environment = GitIsolation.SessionGitEnvironment(data.Path);

        var global = Path.Combine(data.Path, "harness", "global.gitconfig");
        var xdg = Path.Combine(data.Path, "harness", "xdg");
        Assert.Equal(2, environment.Count);
        Assert.Equal(global, environment["GIT_CONFIG_GLOBAL"]);
        Assert.Equal(xdg, environment["XDG_CONFIG_HOME"]);
        Assert.Equal(global, GitIsolation.SessionGlobalConfigFile(data.Path));
        Assert.True(Directory.Exists(xdg));
        Assert.False(File.Exists(global));

        // The first write makes the file (0.10); the session's runner reads it back.
        var runner = new GitRunner(TempRepo.GitPath, environment: environment);
        var write = await runner.RunAsync(new GitCommand("config", "--global", "user.name", "Session User") { WorkingDirectory = data.Path }, TestCancelled);
        Assert.True(write.Succeeded, write.StandardError);
        Assert.True(File.Exists(global));
        var read = await runner.RunAsync(new GitCommand("config", "--global", "user.name") { WorkingDirectory = data.Path }, TestCancelled);
        Assert.Equal("Session User", read.StandardOutput.Trim());

        // A runner without the session's environment sees none of it.
        var outside = await new GitRunner(TempRepo.GitPath).RunAsync(new GitCommand("config", "--global", "user.name") { WorkingDirectory = data.Path }, TestCancelled);
        Assert.Equal(1, outside.ExitCode);
        Assert.Empty(GitIsolation.GlobalConfigurationWrittenOutsideASession());
    }

    private static Task<GitResult> LsRemoteAsync(TempRepo repo, IReadOnlyDictionary<string, string?> environment) =>
        repo.Runner.RunAsync(
            new GitCommand("ls-remote", "origin", "refs/heads/main") { WorkingDirectory = repo.Path, Environment = environment },
            TestCancelled);
}

/// <summary>
/// The isolation's tripwire (docs/phase-6-design.md 17.2), run after the tests of this process
/// that run side by side: xunit runs a collection without parallelization after the parallel
/// ones, so every git call of theirs has ended when it looks. The isolation folder is every test
/// process's, so it sees what the projects beside it wrote until then too.
/// </summary>
[Collection(GitIsolationTripwireTests.Last)]
public class GitIsolationTripwireTests
{
    /// <summary>The collection that runs the tripwire after the parallel tests.</summary>
    public const string Last = "The isolation's tripwire, after the parallel tests";

    [Fact]
    public void Nothing_wrote_a_global_configuration_outside_a_session()
    {
        var findings = GitIsolation.GlobalConfigurationWrittenOutsideASession();
        Assert.True(findings.Count == 0, string.Join('\n', findings));
    }
}

/// <summary>Runs <see cref="GitIsolationTripwireTests"/> after the parallel tests, with none beside it.</summary>
[CollectionDefinition(GitIsolationTripwireTests.Last, DisableParallelization = true)]
public sealed class GitIsolationTripwireCollection
{
}
