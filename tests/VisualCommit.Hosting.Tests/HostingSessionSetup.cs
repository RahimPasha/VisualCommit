using VisualCommit.Core;
using VisualCommit.Core.Logging;
using VisualCommit.Hosting.Tokens;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.Testing.Hosting;
using Xunit;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// A hosting session over a copy of "hosting" served in process (docs/phase-7a-design.md 7.13):
/// the fake's handler behind a recorder, a <see cref="FakeTokenStore"/> unless given, a data folder
/// of its own, a <see cref="ManualTimeProvider"/> at the harness's time and a
/// <see cref="HostingTestThread"/> as the post delegate. The session is made, not started. Disposing
/// it checks that no token reached the data folder or the log and that the fake saw no error.
/// </summary>
internal sealed class HostingSessionSetup : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly List<RecordingHttpHandler> _recorders = [];
    private readonly List<HostingSession> _sessions = [];
    private bool _networkDown;
    private Func<HttpRequestMessage, Exception?>? _fault;

    private HostingSessionSetup(ServedHosting served, ITokenStore tokens)
    {
        Served = served;
        Tokens = tokens;
        Paths = new AppPaths(Data.Path);
        Session = NewSession();
    }

    public ServedHosting Served { get; }

    public FakeGitHub Fake => Served.GitHub;

    public string OctoToken => Served.OctoToken;

    public ITokenStore Tokens { get; }

    /// <summary><see cref="Tokens"/> as the harness's store, when it is one.</summary>
    public FakeTokenStore FakeTokens => (FakeTokenStore)Tokens;

    public TempDirectory Data { get; } = new("hosting-data");

    public AppPaths Paths { get; }

    public HostingTestThread Thread { get; } = new();

    public ListLog Log { get; } = new();

    public ManualTimeProvider Clock { get; } = new(AppClocks.HarnessTime);

    public HostingSession Session { get; }

    /// <summary>While set, every request fails as a network failure before it reaches the fake.</summary>
    public bool NetworkDown
    {
        get => Volatile.Read(ref _networkDown);
        set => Volatile.Write(ref _networkDown, value);
    }

    /// <summary>
    /// A made-up exception's text as .NET's can quote what a server or a proxy sent (D380, D382): the
    /// token echoed back, a proxy's user and password, an address. None of <see cref="ThrownSecrets"/>
    /// may reach the log.
    /// </summary>
    public string ThrownText => $"Received an invalid header line: 'X-Echo: Bearer {OctoToken}'; proxy 'http://proxyuser:PROXY-SECRET@127.0.0.1:9/'; ada@example.com";

    /// <summary>The parts of <see cref="ThrownText"/> that no log line, nor an exception given to the log, may hold.</summary>
    public IReadOnlyList<string> ThrownSecrets => [OctoToken, "X-Echo", "PROXY-SECRET", "proxyuser", "ada@example.com", "invalid header line"];

    /// <summary>Asserts that nothing logged so far, an exception given to the log included, holds a part of <see cref="ThrownText"/>.</summary>
    public void AssertNothingThrownLogged()
    {
        var written = Log.Written;
        foreach (var secret in ThrownSecrets)
        {
            Assert.DoesNotContain(secret, written, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// While set, a request for which it gives an exception throws that exception before it reaches
    /// the fake (D382: one the core does not catch reaches the catch-alls of the session and the tabs).
    /// </summary>
    public Func<HttpRequestMessage, Exception?>? Fault
    {
        get => Volatile.Read(ref _fault);
        set => Volatile.Write(ref _fault, value);
    }

    /// <summary>Every request the sessions sent, in order.</summary>
    public IReadOnlyList<RecordingHttpHandler.SentRequest> Sent
    {
        get
        {
            lock (_gate)
            {
                return [.. _recorders.SelectMany(recorder => recorder.Sent)];
            }
        }
    }

    /// <summary>The fake's requests of <c>GET /user</c> (A1) so far.</summary>
    public int UserRequests => Fake.Requests.Count(request => request.Path == "/api/v3/user");

    public static async Task<HostingSessionSetup> StartAsync(ITokenStore? tokens = null)
    {
        var hello = await Scenarios.HostingAsync();
        try
        {
            var served = await Scenarios.ServeHostingAsync(hello, inProcess: true, ct: TestContext.Current.CancellationToken, ownsCopy: true);
            return new HostingSessionSetup(served, tokens ?? new FakeTokenStore());
        }
        catch
        {
            hello.Dispose();
            throw;
        }
    }

    /// <summary>Another session over the same data folder, store and fake: the app's next start; <see cref="Log"/> unless given.</summary>
    public HostingSession NewSession(ITokenStore? tokens = null, HostingOptions? off = null, IAppLog? log = null)
    {
        var options = off ?? new HostingOptions(Fake.Endpoints, () =>
        {
            var recorder = new RecordingHttpHandler(Fake.CreateHandler(), request =>
            {
                if (NetworkDown)
                {
                    throw new HttpRequestException("No connection could be made.");
                }

                if (Fault?.Invoke(request) is { } fault)
                {
                    throw fault;
                }
            });
            lock (_gate)
            {
                _recorders.Add(recorder);
            }

            return recorder;
        });
        var session = new HostingSession(Paths, options, tokens ?? Tokens, log ?? Log, Thread.Post, Clock);
        lock (_gate)
        {
            _sessions.Add(session);
        }

        return session;
    }

    /// <summary>Starts <see cref="Session"/> and signs in with <paramref name="token"/> (octo's unless given), asserting it worked.</summary>
    public async Task<AccountStatus> SignInAsync(string? token = null)
    {
        await Session.StartAsync(TestContext.Current.CancellationToken);
        var result = await Session.SignInAsync(token ?? OctoToken, TestContext.Current.CancellationToken);
        Assert.Null(result.Failure);
        return result.Status!;
    }

    /// <summary>The warnings logged so far.</summary>
    public IReadOnlyList<string> Warnings => [.. Log.Lines.Where(line => line.Level == LogLevel.Warning).Select(line => line.Message)];

    public async ValueTask DisposeAsync()
    {
        HostingSession[] sessions;
        lock (_gate)
        {
            sessions = [.. _sessions];
        }

        foreach (var session in sessions)
        {
            session.Dispose();
        }

        await Thread.IdleAsync();
        Thread.Dispose();
        try
        {
            foreach (var token in new[] { Served.OctoToken, Served.OctoClassicToken, Served.AdaToken })
            {
                TokenTripwire.AssertAbsent(token, [Data.Path], null);
            }

            Assert.All(Log.Lines, line => Assert.DoesNotContain("_FAKE-", line.Message, StringComparison.Ordinal));
            Assert.Empty(Thread.Errors);
            Assert.Empty(Fake.Errors);
        }
        finally
        {
            await Served.DisposeAsync();
            Data.Dispose();
        }
    }
}
