using System.Diagnostics;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.PromptTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// HTTPS sign-in against a real server (docs/phase-3-design.md 3.5, D97): git asks
/// <see cref="AuthRemoteServer"/>'s Basic auth through the real helper and <see cref="AskPassBroker"/>,
/// and a <see cref="ScriptedPromptHandler"/> answers as the dialogs would. First plain git run
/// through <see cref="GitRunner"/> with a prompt scope's variables, then the app's own path:
/// <see cref="IGitRepository.FetchAsync"/> run by the <see cref="OperationQueue"/>, which opens the
/// operation's scope.
/// </summary>
public class AuthenticatedRemoteTests
{
    [Fact]
    public async Task A_fetch_that_needs_credentials_gets_them_through_the_broker()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new ScriptedPromptHandler().Answer("alice").Answer("s3cret");
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
        var handler = new ScriptedPromptHandler().Answer("alice").Answer("s3cret");
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
        var handler = new ScriptedPromptHandler().Answer("alice").Answer("wrong");
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
        var handler = new ScriptedPromptHandler().Cancel();
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

    [Theory]
    [InlineData(false, OperationOutcome.Succeeded)]
    [InlineData(true, OperationOutcome.Cancelled)]
    public async Task Through_the_operation_queue_a_fetch_signs_in_or_ends_cancelled_when_the_user_cancels(bool cancel, OperationOutcome outcome)
    {
        // The queue opens the operation's scope with the operation's id (1.3, D107); a prompt the
        // user cancelled turns the failed git call into a cancelled operation (2.16).
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = cancel
            ? new ScriptedPromptHandler().Cancel()
            : new ScriptedPromptHandler().Answer("alice").Answer("s3cret");
        using var broker = new AskPassBroker(handler);
        using var queue = NewQueue(repo, broker);
        string? operationId = null;

        var result = await queue.RunAsync(new GitOperation("Fetch", "Fetching…", OperationKind.Network, RefreshScope.None, async context =>
        {
            operationId = context.OperationId;
            var fetch = await StoppableGitAsync(repo, context.Environment, ["fetch", "origin"], null, null, context.CancellationToken);
            fetch.EnsureSuccess(new GitCommand("fetch", "origin"));
            return OperationReport.Done("Fetched");
        })).WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);

        Assert.Equal(outcome, result.Outcome);
        Assert.All(handler.Prompts, prompt => Assert.Equal((operationId, "Fetch", "auth"), (prompt.ScopeId, prompt.Title, prompt.RepositoryName)));
        Assert.Equal(cancel ? 1 : 2, handler.Prompts.Count);
    }

    [Fact]
    public async Task A_deny_scope_fails_the_fetch_without_asking()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new ScriptedPromptHandler().Answer("alice").Answer("s3cret");
        using var broker = new AskPassBroker(handler);
        using var scope = broker.Begin("Auto-fetch", "auth", PromptMode.Deny);

        var fetch = await GitAsync(repo, scope.Environment, ["fetch", "origin"]);

        Assert.False(fetch.Succeeded);
        Assert.Contains("could not read Username", fetch.StandardError, StringComparison.Ordinal);
        Assert.Empty(handler.Prompts);
        Assert.All(server.Requests, request => Assert.Equal(401, request.Status));
    }

    [Fact]
    public async Task FetchAsync_run_by_the_queue_signs_in_with_the_answers_and_updates_the_remote_branch()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new ScriptedPromptHandler().Answer("alice").Answer("s3cret");
        using var broker = new AskPassBroker(handler);
        using var queue = NewQueue(repo, broker);
        var repository = await RepositoryTestSupport.OpenAsync(repo);

        var result = await RunAsync(queue, Fetch(repository));

        Assert.Equal(OperationOutcome.Succeeded, result.Outcome);
        Assert.Equal("Fetched from all remotes", result.Message);
        Assert.Equal("Remote work", (await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")).StandardOutput.Trim());
        Assert.Equal([GitPromptKind.Username, GitPromptKind.Password], handler.Prompts.Select(prompt => prompt.Kind));
        Assert.All(handler.Prompts, prompt => Assert.Equal(("Fetch", "auth"), (prompt.Title, prompt.RepositoryName)));
        Assert.Equal(0, handler.Remaining);
        Assert.Contains(server.Requests, request => request.Status == 200 && request.UserName == "alice" && request.Command == "fetch");
    }

    [Fact]
    public async Task FetchAsync_whose_prompt_the_user_cancelled_ends_cancelled_quickly_and_changes_nothing()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new ScriptedPromptHandler().Cancel();
        using var broker = new AskPassBroker(handler);
        using var queue = NewQueue(repo, broker);
        var repository = await RepositoryTestSupport.OpenAsync(repo);
        var before = await repo.RefTargetAsync("refs/remotes/origin/main");

        var stopwatch = Stopwatch.StartNew();
        var result = await RunAsync(queue, Fetch(repository));
        stopwatch.Stop();

        // Git does not wait for a terminal or ask again: the helper's refusal ends it (2.16).
        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        Assert.Equal("Fetch cancelled", result.Message);
        Assert.Null(result.Failure);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"The fetch took {stopwatch.Elapsed}.");
        Assert.Single(handler.Prompts);
        Assert.Equal(before, await repo.RefTargetAsync("refs/remotes/origin/main"));
        Assert.All(server.Requests, request => Assert.Equal(401, request.Status));
    }

    [Fact]
    public async Task FetchAsync_with_a_wrong_password_fails_as_an_authentication_failure_with_gits_output()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new ScriptedPromptHandler().Answer("alice").Answer("wrong");
        using var broker = new AskPassBroker(handler);
        using var queue = NewQueue(repo, broker);
        var repository = await RepositoryTestSupport.OpenAsync(repo);

        var result = await RunAsync(queue, Fetch(repository, "origin"));

        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Equal("Fetch origin failed", result.Message);
        var failure = Assert.IsType<GitFailure>(result.Failure);
        Assert.Equal(GitFailureKind.AuthenticationFailed, failure.Kind);
        Assert.Contains($"fatal: Authentication failed for '{server.Url}/'", failure.GitOutput, StringComparison.Ordinal);
        Assert.Equal("git fetch --progress origin", failure.CommandText);
        Assert.DoesNotContain("wrong", failure.GitOutput, StringComparison.Ordinal);
        Assert.Equal(2, handler.Prompts.Count);
        Assert.Equal("Initial commit", (await repo.GitAsync("log", "-1", "--format=%s", "refs/remotes/origin/main")).StandardOutput.Trim());
    }

    [Fact]
    public async Task FetchAsync_in_a_deny_operation_fails_without_asking()
    {
        // Auto-fetch's mode (3.3): the helper refuses at once and the handler is never called.
        await using var server = await AuthRemoteServer.StartAsync();
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var handler = new ScriptedPromptHandler().Answer("alice").Answer("s3cret");
        using var broker = new AskPassBroker(handler);
        using var queue = NewQueue(repo, broker);
        var repository = await RepositoryTestSupport.OpenAsync(repo);

        var result = await RunAsync(queue, Fetch(repository) with { Prompts = PromptMode.Deny });

        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Contains("could not read Username", result.Failure!.GitOutput, StringComparison.Ordinal);
        Assert.Empty(handler.Prompts);
        Assert.Equal(2, handler.Remaining);
        Assert.All(server.Requests, request => Assert.Equal(401, request.Status));
    }

    private static OperationQueue NewQueue(TempRepo repo, IGitPromptBroker broker) =>
        new("auth", Path.Combine(repo.Path, ".git"), () => null, _ => Task.CompletedTask, broker, null, new ListLog());

    /// <summary>The fetch as the app starts it (2.16, 2.33): of all remotes, or of one.</summary>
    private static GitOperation Fetch(IGitRepository repository, string? remote = null) => new(
        remote is null ? "Fetch" : $"Fetch {remote}",
        remote is null ? "Fetching from all remotes…" : $"Fetching from {remote}…",
        OperationKind.Network,
        RefreshScope.None,
        async context =>
        {
            await repository.FetchAsync(remote, NetworkOptions.From(context), cancellationToken: context.CancellationToken);
            return OperationReport.Done(remote is null ? "Fetched from all remotes" : $"Fetched from {remote}");
        });

    private static Task<OperationResult> RunAsync(OperationQueue queue, GitOperation operation) =>
        queue.RunAsync(operation).WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);
}
