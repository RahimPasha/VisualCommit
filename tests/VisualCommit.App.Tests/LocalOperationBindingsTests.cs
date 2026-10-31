using System.Net;
using VisualCommit.AskPass;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Only network operations carry the account's bindings (docs/phase-7a-design.md 0.2, 4.7, D255,
/// D329): a local operation of a bound tab runs git as an unbound tab does, with the inherited
/// command-line configuration and no credential pair, while a fetch in the same tab still takes the
/// token through the app's helper.
/// </summary>
public class LocalOperationBindingsTests
{
    private const string InheritedEmail = "work@corp.example";

    [Fact]
    public async Task A_local_merge_in_a_bound_tab_keeps_the_inherited_command_line_configuration_and_a_fetch_still_binds()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.Credentials = new NetworkCredential(AskPassProtocol.CredentialUserName, CredentialTestSupport.Token);
        using var repo = await Scenarios.AuthRemoteAsync(server);
        await repo.CreateBranchAsync("side");
        await repo.CheckoutAsync("side");
        await repo.CommitFileAsync("side.txt", "side\n", "Side work");
        await repo.CheckoutAsync("main");

        // A post-merge hook records what a git started by the merge's git would get.
        var seen = repo.SiblingPath("post-merge.txt");
        var hook = Path.Combine(repo.Path, ".git", "hooks", "post-merge");
        Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        File.WriteAllText(hook, $"#!/bin/sh\necho \"count=${{GIT_CONFIG_COUNT-unset}} params=${{GIT_CONFIG_PARAMETERS-unset}}\" > '{seen.Replace('\\', '/')}'\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        // Inherited as from `git -c user.email=... <alias>` (D329), through the session's git
        // environment rather than the test process's, which every git call beside this test would get.
        var parameters = $"'user.email'='{InheritedEmail}'";
        var stub = CredentialTestSupport.Stub(server);
        stub.RepositoryBindings = [new CredentialBinding(server.Url, stub.Key)];
        var handler = new ScriptedPromptHandler();
        using var app = await OperationsHarness.OpenAsync(repo, _ => handler, hosting: stub, gitEnvironment: new Dictionary<string, string?> { ["GIT_CONFIG_PARAMETERS"] = parameters });

        await app.Operations.MergeAsync("refs/heads/side", MergeMode.NoFastForward);

        // A merge commit (HEAD and its two parents), made by the inherited identity.
        Assert.Equal(3, (await repo.GitAsync("rev-list", "--parents", "-n", "1", "HEAD")).StandardOutput.Trim().Split(' ').Length);
        Assert.Equal($"{InheritedEmail} {InheritedEmail}", (await repo.GitAsync("log", "-1", "--format=%ae %ce")).StandardOutput.Trim());
        Assert.Equal($"count=unset params={parameters}", File.ReadAllText(seen).Trim());
        Assert.DoesNotContain(app.Log.Messages, message => message.Contains("bound to the account", StringComparison.Ordinal));

        await app.Operations.FetchAsync("origin");

        Assert.Empty(handler.Prompts);
        Assert.Equal([stub.Key], stub.Credentials.Gets);
        Assert.Contains(server.Requests, request => request.Status == 200 && request.UserName == AskPassProtocol.CredentialUserName);
        Assert.Contains(app.Log.Messages, message => message.EndsWith(": 1 of 1 URLs bound to the account (Ask).", StringComparison.Ordinal));
        Assert.Contains(app.Log.Messages, message => message.EndsWith(": the inherited command-line git configuration is left out of calls bound to the account.", StringComparison.Ordinal));
    }
}
