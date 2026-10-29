using System.IO.Pipes;
using VisualCommit.AskPass;
using VisualCommit.Core.Hosting;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using VisualCommit.Git.Prompts;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.PromptTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The prompt channel's listening loop (<see cref="AskPassBroker"/>) with a helper that connects
/// while another one is being served. On macOS and Linux every instance of the pipe shares one
/// listening Unix socket, which .NET closes with the last instance, and a connection queued on it
/// that nobody accepted yet is dropped then. An answer without UI (git's credential get and erase,
/// docs/phase-7a-design.md 4.6) can be served to its end, its instance disposed, before the loop
/// makes the next instance: CI's macOS run lost one of a bulk run's credential requests so (run
/// 40878208671).
/// </summary>
/// <remarks>
/// The broker is held between an accept and its serve (<see cref="AskPassBroker.AfterAccept"/>):
/// the first client is accepted, the second connects, and on macOS and Linux both have written
/// their requests (the second's waits in the listening socket's queue) before the broker goes on;
/// on Windows the second's connect waits for the next instance. Raw clients, as the helper connects.
/// </remarks>
public class AskPassChannelTests
{
    private const string Token = "github_pat_FAKE-octocat-1";

    private static readonly AccountKey Octocat = new(HostingProvider.GitHub, "github.com", "octocat");

    /// <summary>The broker's warning when no instance is left while it listens, which the keeper prevents on macOS and Linux.</summary>
    private static readonly (LogLevel, string) ClosedBetweenConnections =
        (LogLevel.Warning, "The prompt channel closed its socket between two connections; a helper connecting then would go unanswered.");

    [Theory]
    [InlineData("get")]
    [InlineData("erase")]
    public async Task A_credential_request_waiting_while_another_is_answered_without_UI_is_answered_too(string action)
    {
        var source = new RecordingCredentialSource(Token);
        var log = new ListLog();
        using var broker = Broker(source, log);
        using var api = broker.Begin("Fetch", "api", PromptMode.Ask, "op-1", bindings: [new("https://github.com/o/api.git", Octocat)]);
        using var docs = broker.Begin("Fetch", "docs", PromptMode.Ask, "op-2", bindings: [new("https://github.com/o/docs.git", Octocat)]);
        if (action == "erase")
        {
            // An erase marks only a token its scope gave git: each scope hands it out first, unheld.
            foreach (var scope in new[] { api, docs })
            {
                await using var get = Client.Start(scope, CredentialRequest(scope, "get"));
                Assert.Equal("answer: " + Token, await get.OutcomeAsync());
            }
        }

        var (first, second) = await OneQueuedBehindTheOtherAsync(
            broker,
            () => Client.Start(api, CredentialRequest(api, action)),
            () => Client.Start(docs, CredentialRequest(docs, action)));

        var expected = action == "get" ? "answer: " + Token : "answer: null";
        AssertAnswered(("the first client (api's " + action + ")", expected, first), ("the second client (docs's " + action + "), queued behind it", expected, second));
        if (action == "get")
        {
            Assert.Equal([Octocat, Octocat], source.Gets);
        }
        else
        {
            Assert.Equal((Octocat, Octocat), (api.RefusedAccount, docs.RefusedAccount));
            Assert.Equal([Octocat, Octocat], source.Refusals);
        }

        Assert.DoesNotContain(ClosedBetweenConnections, log.Entries);
    }

    [Fact]
    public async Task A_prompt_waiting_while_a_credential_request_is_answered_is_answered_too()
    {
        var handler = new RecordingPromptHandler(PromptAnswer.Of("alice"));
        var log = new ListLog();
        using var broker = Broker(new RecordingCredentialSource(Token), log, handler);
        using var api = broker.Begin("Fetch", "api", PromptMode.Ask, "op-1", bindings: [new("https://github.com/o/api.git", Octocat)]);
        using var web = broker.Begin("Fetch", "web", PromptMode.Ask, "op-2");

        var (first, second) = await OneQueuedBehindTheOtherAsync(
            broker,
            () => Client.Start(api, CredentialRequest(api, "get")),
            () => Client.Start(web, AskPassProtocol.FormatRequest(new AskPassRequest(TokenOf(web), web.Id, "Username for 'https://example.com': ", null))));

        AssertAnswered(("the first client (api's get)", "answer: " + Token, first), ("the second client (web's prompt), queued behind it", "answer: alice", second));
        Assert.Equal(GitPromptKind.Username, Assert.Single(handler.Prompts).Kind);
        Assert.DoesNotContain(ClosedBetweenConnections, log.Entries);
    }

    /// <summary>A broker with bindings in use: the helper next to the tests, a credential source, no inherited <c>GIT_CONFIG_COUNT</c>.</summary>
    private static AskPassBroker Broker(RecordingCredentialSource source, IAppLog log, RecordingPromptHandler? handler = null) =>
        new(handler ?? new RecordingPromptHandler(), log, null, source, null, null, _ => null);

    private static string TokenOf(PromptScope scope) => scope.Environment[AskPassProtocol.TokenVariable]!;

    /// <summary>git's <paramref name="action"/> for github.com in <paramref name="scope"/>, as the helper sends it; an erase names <see cref="Token"/>'s hash.</summary>
    private static byte[] CredentialRequest(PromptScope scope, string action) =>
        AskPassProtocol.FormatCredentialRequest(new AskPassCredentialRequest(
            TokenOf(scope),
            scope.Id,
            action,
            "https",
            "github.com",
            AskPassProtocol.CredentialUserName,
            action == "erase" ? AskPassProtocol.SecretHash(Token) : null));

    /// <summary>
    /// Holds the broker after its next accept: <paramref name="startFirst"/>'s client is accepted,
    /// then <paramref name="startSecond"/>'s connects; on macOS and Linux both requests are written
    /// (the second's into the listening socket's queue) before the broker serves the first. Returns
    /// each client's <see cref="Client.OutcomeAsync"/>.
    /// </summary>
    private static async Task<(string First, string Second)> OneQueuedBehindTheOtherAsync(AskPassBroker broker, Func<Client> startFirst, Func<Client> startSecond)
    {
        using var hold = new Hold(broker);
        await using var first = startFirst();
        await hold.Accepted.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);
        await using var second = startSecond();
        if (!OperatingSystem.IsWindows())
        {
            // A Unix socket's connect ends once the kernel has queued it, and its write once the
            // bytes are in the kernel's buffer, whether the server accepted it or not. On Windows
            // the second connect waits for the next instance, which the hold keeps from being made.
            await first.Sent.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);
            await second.Sent.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);
        }

        hold.Release();
        return (await first.OutcomeAsync(), await second.OutcomeAsync());
    }

    /// <summary>Fails with every client that did not get its expected answer named.</summary>
    private static void AssertAnswered(params (string Client, string Expected, string Outcome)[] clients)
    {
        var wrong = clients.Where(client => client.Outcome != client.Expected).Select(client => $"{client.Client} got {client.Outcome}, not {client.Expected}").ToList();
        Assert.True(wrong.Count == 0, string.Join("; ", wrong) + ".");
    }

    /// <summary>
    /// Holds the broker's listening loop after its first accept from now on, until
    /// <see cref="Release"/> or the end of the test.
    /// </summary>
    private sealed class Hold : IDisposable
    {
        private readonly TaskCompletionSource _accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _held;

        public Hold(AskPassBroker broker) => broker.AfterAccept = () =>
        {
            if (Interlocked.Exchange(ref _held, 1) != 0)
            {
                return Task.CompletedTask;
            }

            _accepted.TrySetResult();
            return _release.Task;
        };

        /// <summary>Completes when the held accept has happened.</summary>
        public Task Accepted => _accepted.Task;

        /// <summary>Lets the loop go on: it serves the accepted connection.</summary>
        public void Release() => _release.TrySetResult();

        public void Dispose() => Release();
    }

    /// <summary>
    /// A raw client of the pipe, as the helper connects (CredentialMode, Program): it connects,
    /// writes one request, then reads one line; every step bounded by <see cref="PromptTestSupport.StepTimeout"/>.
    /// </summary>
    private sealed class Client : IAsyncDisposable
    {
        private readonly NamedPipeClientStream _pipe;
        private readonly CancellationTokenSource _patience = new(StepTimeout);
        private readonly Task<string> _outcome;

        private Client(string pipeName, byte[] request)
        {
            var options = OperatingSystem.IsWindows() ? PipeOptions.Asynchronous : PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous;
            _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, options);
            Sent = SendAsync(request);
            _outcome = ReadAsync();
        }

        /// <summary>Completes when the request is written.</summary>
        public Task Sent { get; }

        public static Client Start(PromptScope scope, byte[] request) => new(scope.Environment[AskPassProtocol.PipeVariable]!, request);

        /// <summary>
        /// <c>answer: &lt;text&gt;</c> (<c>answer: null</c> for an answer that refuses), or what
        /// came instead: a malformed line, or <c>no answer</c> with what ended the connection.
        /// </summary>
        public Task<string> OutcomeAsync() => _outcome.WaitAsync(StepTimeout, RepositoryTestSupport.TestCancelled);

        public async ValueTask DisposeAsync()
        {
            await _pipe.DisposeAsync();
            _patience.Dispose();
        }

        private async Task SendAsync(byte[] request)
        {
            await _pipe.ConnectAsync(StepTimeout, _patience.Token).ConfigureAwait(false);
            await _pipe.WriteAsync(request, _patience.Token).ConfigureAwait(false);
            await _pipe.FlushAsync(_patience.Token).ConfigureAwait(false);
        }

        private async Task<string> ReadAsync()
        {
            try
            {
                await Sent.ConfigureAwait(false);
                var line = await AskPassProtocol.ReadLineAsync(_pipe, _patience.Token).ConfigureAwait(false);
                if (line is null)
                {
                    return "no answer (the connection ended without a line)";
                }

                return AskPassProtocol.TryParseAnswer(line, out var answer) ? "answer: " + (answer ?? "null") : "a malformed answer";
            }
            catch (IOException ex)
            {
                return $"no answer (the connection broke: {ex.Message})";
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
            {
                return $"no answer within {StepTimeout.TotalSeconds} s ({ex.GetType().Name})";
            }
        }
    }
}
