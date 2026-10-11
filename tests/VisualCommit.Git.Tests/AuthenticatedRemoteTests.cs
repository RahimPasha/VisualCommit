using System.Diagnostics;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.PromptTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// HTTPS sign-in against a real server (docs/phase-3-design.md 3.5, D97): plain git, run through
/// <see cref="GitRunner"/> with a prompt scope's variables as the operation queue gives them, asks
/// <see cref="AuthRemoteServer"/>'s Basic auth through the real helper and <see cref="AskPassBroker"/>.
/// The same through <c>IGitRepository.FetchAsync</c> and the queue comes with work package WP2.
/// </summary>
public class AuthenticatedRemoteTests
{
    [Fact]
    public async Task A_fetch_that_needs_credentials_gets_them_through_the_broker()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"), PromptAnswer.Of("s3cret"));
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Fetch", "auth", PromptMode.Ask);

        var fetch = await GitAsync(repo, scope.Environment, ["fetch", "origin"]);

        Assert.True(fetch.Succeeded, fetch.StandardError);
        Assert.Equal("Remote work", (await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")).StandardOutput.Trim());
        var host = $"127.0.0.1:{server.Port}";
        Assert.Collection(
            handler.Prompts,
            username =>
            {
                Assert.Equal(GitPromptKind.Username, username.Kind);
                Assert.Equal(host, GitPromptClassifier.HostOf(username.Url!));
                Assert.Equal(("Fetch", "auth", scope.Id), (username.Title, username.RepositoryName, username.ScopeId));
            },
            password =>
            {
                Assert.Equal(GitPromptKind.Password, password.Kind);
                Assert.Equal(GitPromptClassifier.PasswordPromptAfter(handler.Prompts[0].Url!, "alice"), password.Text);
            });
        Assert.Contains(server.Requests, request => request.Status == 200 && request.UserName == "alice" && request.Path == "/remote.git/git-upload-pack");
        Assert.Empty(server.Errors);
    }

    [Fact]
    public async Task A_push_that_needs_credentials_gets_them_through_the_broker()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"), PromptAnswer.Of("s3cret"));
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Push main", "auth", PromptMode.Ask);
        var local = await repo.CommitFileAsync("src/local.txt", "local\n", "Local work");

        var push = await GitAsync(repo, scope.Environment, ["push", "--force", "origin", "refs/heads/main:refs/heads/main"]);

        Assert.True(push.Succeeded, push.StandardError);
        var remoteMain = await repo.GitAsync("--git-dir=" + server.RepositoryPath, "rev-parse", "refs/heads/main");
        Assert.Equal(local, remoteMain.StandardOutput.Trim());
        Assert.Equal([GitPromptKind.Username, GitPromptKind.Password], handler.Prompts.Select(prompt => prompt.Kind));
        Assert.Contains(server.Requests, request => request.Status == 200 && request.UserName == "alice" && request.Path == "/remote.git/git-receive-pack");
    }

    [Fact]
    public async Task A_wrong_password_fails_as_an_authentication_failure()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"), PromptAnswer.Of("wrong"));
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Fetch", "auth", PromptMode.Ask);

        var fetch = await GitAsync(repo, scope.Environment, ["fetch", "origin"]);

        Assert.False(fetch.Succeeded);
        Assert.Contains("Authentication failed", fetch.StandardError, StringComparison.Ordinal);
        Assert.Equal(2, handler.Prompts.Count);
        Assert.False(scope.UserCancelled);
        Assert.Equal("Initial commit", (await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")).StandardOutput.Trim());
    }

    [Fact]
    public async Task A_cancelled_prompt_fails_the_fetch_at_once_and_the_scope_tells_it_was_the_user()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new RecordingPromptHandler(PromptAnswer.Cancelled);
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Fetch", "auth", PromptMode.Ask);

        var stopwatch = Stopwatch.StartNew();
        var fetch = await GitAsync(repo, scope.Environment, ["fetch", "origin"]);
        stopwatch.Stop();

        // Git neither waits for a terminal nor asks again: it fails as soon as the helper refuses.
        Assert.False(fetch.Succeeded);
        Assert.Contains("could not read Username", fetch.StandardError, StringComparison.Ordinal);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"The fetch took {stopwatch.Elapsed}.");
        Assert.Single(handler.Prompts);
        Assert.True(scope.UserCancelled);
        Assert.All(server.Requests, request => Assert.Equal(401, request.Status));
    }

    [Fact]
    public async Task A_deny_scope_fails_the_fetch_without_asking()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"), PromptAnswer.Of("s3cret"));
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Auto-fetch", "auth", PromptMode.Deny);

        var fetch = await GitAsync(repo, scope.Environment, ["fetch", "origin"]);

        Assert.False(fetch.Succeeded);
        Assert.Contains("could not read Username", fetch.StandardError, StringComparison.Ordinal);
        Assert.Empty(handler.Prompts);
        Assert.All(server.Requests, request => Assert.Equal(401, request.Status));
    }
}
