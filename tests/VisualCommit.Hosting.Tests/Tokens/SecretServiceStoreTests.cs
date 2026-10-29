using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Tmds.DBus.Protocol;
using VisualCommit.Hosting.Tokens;
using Xunit;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// <see cref="SecretServiceStore"/> without a session bus (docs/phase-7a-design.md 3.6, 3.9): where
/// it looks for the bus, and how soon it gives up when there is none, the socket is missing, or a
/// bus never answers (a listener on 127.0.0.1 that never writes). No call reaches a real bus.
/// </summary>
public sealed class SecretServiceStoreTests
{
    private static readonly TokenKey Octocat = new("github", "github.com", "octocat");

    private static readonly SecretServiceLimits Short = new(
        TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300));

    // The design's bound for a bus that does not answer: the connect limit plus 2 s.
    private static readonly TimeSpan Bound = Short.Connect + TimeSpan.FromSeconds(2);

    private readonly ListLog _log = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public void The_bus_address_of_the_environment_is_taken_as_it_is()
    {
        var address = SecretServiceAddress.Resolve(
            Environment(("DBUS_SESSION_BUS_ADDRESS", "unix:path=/run/user/1000/bus,guid=0123"), ("XDG_RUNTIME_DIR", "/run/user/1000")),
            _ => throw new InvalidOperationException("No socket is looked for when the address is set."));

        Assert.Equal("unix:path=/run/user/1000/bus,guid=0123", address);
    }

    [Theory]
    [InlineData("/run/user/1000", "/run/user/1000/bus", "unix:path=/run/user/1000/bus")]
    [InlineData("/run/user/1000/", "/run/user/1000/bus", "unix:path=/run/user/1000/bus")]
    [InlineData("/tmp/a b,c=d;e%f", "/tmp/a b,c=d;e%f/bus", "unix:path=/tmp/a%20b%2cc%3dd%3be%25f/bus")]
    [InlineData("/tmp/ü*\\x_y-z.", "/tmp/ü*\\x_y-z./bus", "unix:path=/tmp/%c3%bc*\\x_y-z./bus")]
    public void Without_an_address_the_runtime_folders_bus_is_taken_when_its_socket_exists(string runtime, string socket, string expected)
    {
        var asked = new List<string>();

        var address = SecretServiceAddress.Resolve(
            Environment(("DBUS_SESSION_BUS_ADDRESS", string.Empty), ("XDG_RUNTIME_DIR", runtime)),
            path =>
            {
                asked.Add(path);
                return true;
            });

        Assert.Equal(expected, address);
        Assert.Equal([socket], asked);
    }

    [Fact]
    public void Without_an_address_and_a_socket_there_is_no_bus()
    {
        Assert.Null(SecretServiceAddress.Resolve(Environment(("XDG_RUNTIME_DIR", "/run/user/1000")), _ => false));
        Assert.Null(SecretServiceAddress.Resolve(Environment(), _ => true));
        Assert.Null(SecretServiceAddress.Resolve(Environment(("XDG_RUNTIME_DIR", string.Empty)), _ => true));
    }

    [Fact]
    public async Task Without_a_bus_the_probe_finds_no_keychain_at_once_and_connects_nowhere()
    {
        var connections = 0;
        var store = new SecretServiceStore(_log, new SecretServiceOptions
        {
            ReadEnvironment = Environment(),
            SocketExists = _ => false,
            ConnectionOptions = address =>
            {
                connections++;
                return new DBusConnectionOptions(address);
            },
            Limits = Short,
        });
        var clock = Stopwatch.StartNew();

        var probe = await store.ProbeAsync(_ct);

        Assert.Equal(new TokenStoreProbe(false, TokenStoreReasons.NoKeychain), probe);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"The probe took {clock.Elapsed}.");
        Assert.Equal(0, connections);
        Assert.Single(_log.Lines);
    }

    [Fact]
    public async Task Without_a_bus_every_call_finds_no_keychain_and_connects_nowhere()
    {
        var connections = 0;
        var store = new SecretServiceStore(_log, new SecretServiceOptions
        {
            ReadEnvironment = Environment(),
            SocketExists = _ => false,
            ConnectionOptions = address =>
            {
                connections++;
                return new DBusConnectionOptions(address);
            },
            Limits = Short,
        });

        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.NoKeychain), await store.SaveAsync(Octocat, "ghp_FAKE-octocat-1", _ct));
        Assert.Equal(TokenStoreReasons.NoKeychain, (await store.ReadAsync(Octocat, _ct)).Reason);
        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.NoKeychain), await store.DeleteAsync(Octocat, _ct));
        Assert.Equal(0, connections);
        AssertNoTokenInTheLog();
    }

    [Fact]
    public async Task A_key_that_cannot_name_an_entry_is_refused_before_any_connection()
    {
        var store = new SecretServiceStore(_log, new SecretServiceOptions
        {
            ReadEnvironment = _ => throw new InvalidOperationException("The bus is not looked for."),
            SocketExists = _ => throw new InvalidOperationException("The bus is not looked for."),
        });
        var key = new TokenKey("github", "github.com", "octo cat");

        Assert.Equal(TokenStoreReasons.Refused, (await store.SaveAsync(key, "ghp_FAKE-octocat-1", _ct)).Reason);
        Assert.Equal(TokenStoreReasons.Refused, (await store.ReadAsync(key, _ct)).Reason);
        Assert.Equal(TokenStoreReasons.Refused, (await store.DeleteAsync(key, _ct)).Reason);
        AssertNoTokenInTheLog();
    }

    [Fact]
    public async Task A_missing_socket_finds_no_keychain_within_the_connect_limit_and_2_seconds()
    {
        var missing = Path.Combine(Path.GetTempPath(), "VisualCommit.Tests", "no-bus-" + Guid.NewGuid().ToString("N"), "bus");
        var store = new SecretServiceStore(_log, new SecretServiceOptions
        {
            ReadEnvironment = Environment(("DBUS_SESSION_BUS_ADDRESS", "unix:path=" + SecretServiceAddress.Escape(missing))),
            Limits = Short,
        });
        var clock = Stopwatch.StartNew();

        var probe = await store.ProbeAsync(_ct);
        var read = await store.ReadAsync(Octocat, _ct);

        Assert.Equal(new TokenStoreProbe(false, TokenStoreReasons.NoKeychain), probe);
        Assert.Equal(TokenStoreOutcome.Failed, read.Outcome);
        Assert.True(clock.Elapsed < Bound * 2, $"The probe and the read took {clock.Elapsed}.");
        AssertNoTokenInTheLog();
    }

    [Fact]
    public async Task A_bus_that_never_answers_finds_no_keychain_within_the_connect_limit_and_2_seconds()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var store = new SecretServiceStore(_log, new SecretServiceOptions
        {
            ReadEnvironment = Environment(("DBUS_SESSION_BUS_ADDRESS", "unix:path=/silent/bus")),
            ConnectionOptions = _ => new SilentBusOptions(port),
            Limits = Short,
        });

        var clock = Stopwatch.StartNew();
        var probe = await store.ProbeAsync(_ct);
        var probed = clock.Elapsed;
        clock.Restart();
        var saved = await store.SaveAsync(Octocat, "ghp_FAKE-octocat-1", _ct);
        var saving = clock.Elapsed;

        Assert.Equal(new TokenStoreProbe(false, TokenStoreReasons.NoKeychain), probe);
        Assert.True(probed < Bound, $"The probe took {probed}.");
        Assert.Equal(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.NoAnswer), saved);
        Assert.True(saving < Bound, $"The save took {saving}.");
        Assert.Contains(_log.Lines, line => line.Message == "Secret Service: the session bus did not answer connect in time.");
        AssertNoTokenInTheLog();
    }

    [Fact]
    public async Task A_cancel_ends_the_wait_for_a_bus_that_never_answers()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var store = new SecretServiceStore(_log, new SecretServiceOptions
        {
            ReadEnvironment = Environment(("DBUS_SESSION_BUS_ADDRESS", "unix:path=/silent/bus")),
            ConnectionOptions = _ => new SilentBusOptions(port),
            Limits = SecretServiceLimits.Default,
        });
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(200));
        var clock = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadAsync(Octocat, cancel.Token));

        Assert.True(clock.Elapsed < SecretServiceLimits.Default.Connect, $"The cancel took {clock.Elapsed}.");
    }

    [Fact]
    public void The_store_names_itself_for_the_log_and_its_constructor_does_nothing()
    {
        var store = new SecretServiceStore(_log, new SecretServiceOptions
        {
            ReadEnvironment = _ => throw new InvalidOperationException("The constructor reads nothing."),
            SocketExists = _ => throw new InvalidOperationException("The constructor looks for nothing."),
            ConnectionOptions = _ => throw new InvalidOperationException("The constructor connects nowhere."),
        });

        Assert.Equal("Secret Service", store.Name);
        Assert.True(store.KeepsTokensAcrossRestarts);
        Assert.Equal(new SecretServiceLimits(
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(120)),
            SecretServiceLimits.Default);
        Assert.Empty(_log.Lines);
    }

    private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
        name => variables.FirstOrDefault(variable => variable.Name == name).Value;

    private void AssertNoTokenInTheLog() =>
        Assert.All(_log.Lines, line => Assert.DoesNotContain("FAKE", line.Message, StringComparison.Ordinal));

    /// <summary>A bus that takes the connection and never writes a byte: the authentication waits forever.</summary>
    private sealed class SilentBusOptions(int port) : DBusConnectionOptions
    {
        protected override async ValueTask<SetupResult> SetupAsync(CancellationToken cancellationToken)
        {
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
            return new SetupResult
            {
                ConnectionStream = client.GetStream(),
                TeardownToken = client,
                UserId = "1000",
                MachineId = "0123456789abcdef0123456789abcdef",
            };
        }

        protected override void Teardown(object? token) => (token as IDisposable)?.Dispose();
    }
}
