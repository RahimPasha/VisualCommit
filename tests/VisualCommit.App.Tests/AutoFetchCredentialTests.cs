using System.Net;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.AskPass;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Operations;
using VisualCommit.Hosting;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Auto-fetch with the account's binding (docs/phase-7a-design.md 4.7, 4.8, D255, D79): a bound
/// fetch takes the token and still never prompts; without the binding it fails at once.
/// </summary>
public class AutoFetchCredentialTests
{
    [Fact]
    public async Task Auto_fetch_with_a_binding_succeeds_without_a_prompt_and_without_one_fails_at_once()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.Credentials = new NetworkCredential(AskPassProtocol.CredentialUserName, CredentialTestSupport.Token);
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var stub = CredentialTestSupport.Stub(server);
        var handler = new ScriptedPromptHandler();
        using var app = await OperationsHarness.OpenAsync(repo, _ => handler, hosting: stub);
        var binding = new CredentialBinding(server.Url, stub.Key);

        var bound = await app.Repository.Queue.RunAsync(AutoFetchScheduler.CreateOperation(app.Repository.Repository, [binding]));
        var unbound = await app.Repository.Queue.RunAsync(AutoFetchScheduler.CreateOperation(app.Repository.Repository));

        Assert.Equal(OperationOutcome.Succeeded, bound.Outcome);
        Assert.Equal((OperationOutcome.Failed, GitFailureKind.AuthenticationFailed), (unbound.Outcome, unbound.Failure?.Kind));
        Assert.Empty(handler.Prompts);
        Assert.Equal([stub.Key], stub.Credentials.Gets);
        Assert.Contains(server.Requests, request => request.Status == 200 && request.UserName == AskPassProtocol.CredentialUserName);
    }

    [Fact]
    public async Task The_schedulers_tick_gives_its_auto_fetch_the_tabs_bindings()
    {
        await using var server = await AuthRemoteServer.StartAsync();
        server.Credentials = new NetworkCredential(AskPassProtocol.CredentialUserName, CredentialTestSupport.Token);
        using var repo = await Scenarios.AuthRemoteAsync(server);
        var stub = CredentialTestSupport.Stub(server);
        var handler = new ScriptedPromptHandler();
        using var app = await OperationsHarness.OpenAsync(repo, _ => handler, hosting: stub);
        stub.RepositoryBindings = [new CredentialBinding(server.Url, stub.Key)];

        // Opened an hour ago with a five-minute interval: the first tick starts the auto-fetch.
        var now = DateTimeOffset.UtcNow;
        using var scheduler = new AutoFetchScheduler(() => app.Repository, () => false, new MainWindowViewModelTests.FakeSettings(), TimeProvider.System, new RecordingLog())
        {
            ReadEnvironment = _ => "5",
            SinceOpening = _ => TimeSpan.FromHours(1),
        };
        var finished = new List<(bool Background, OperationOutcome Outcome)>();
        app.Repository.Queue.Finished += (_, e) => finished.Add((e.Operation.IsBackground, e.Result.Outcome));
        scheduler.Tick(now);
        await app.Repository.Queue.WhenIdleAsync();

        Assert.Equal([(true, OperationOutcome.Succeeded)], finished);
        Assert.Empty(handler.Prompts);
        Assert.Equal([stub.Key], stub.Credentials.Gets);
        Assert.Contains(server.Requests, request => request.Status == 200 && request.UserName == AskPassProtocol.CredentialUserName);
    }
}
