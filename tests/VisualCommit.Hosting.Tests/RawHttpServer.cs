using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VisualCommit.Hosting.Tests;

/// <summary>
/// A server on 127.0.0.1, on a port the system gives, that reads each connection's one request
/// head and answers it with the bytes a script gives, as they are, then closes the connection: for
/// answers no HTTP library writes (D380's findings: an invalid header line, a chunk line that is no
/// number, a body its <c>Content-Encoding</c> does not decode, a proxy's 407), sent to the real
/// handler. Nothing leaves the machine.
/// </summary>
internal sealed class RawHttpServer : IAsyncDisposable
{
    private const int MaxHeadBytes = 64 * 1024;

    private readonly TcpListener _listener;
    private readonly Func<string, byte[]> _answer;
    private readonly CancellationTokenSource _stop = new();
    private readonly List<string> _heads = [];
    private readonly Task _accepting;

    /// <summary>Answers each request with what <paramref name="answer"/> gives for its head (the request line and headers, as Latin-1 text).</summary>
    public RawHttpServer(Func<string, byte[]> answer)
    {
        _answer = answer;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _accepting = Task.Run(AcceptAsync);
    }

    public int Port { get; }

    /// <summary><c>http://127.0.0.1:&lt;port&gt;/</c>.</summary>
    public Uri Base => new($"http://127.0.0.1:{Port}/");

    /// <summary>The heads of the requests read, in order.</summary>
    public IReadOnlyList<string> Heads
    {
        get
        {
            lock (_heads)
            {
                return [.. _heads];
            }
        }
    }

    /// <summary>An answer's bytes from its text, one byte per character (Latin-1), as HTTP/1.1 writes a head.</summary>
    public static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>A request head's header of <paramref name="name"/>, without case; null when it has none.</summary>
    public static string? HeaderOf(string head, string name)
    {
        foreach (var line in head.Split("\r\n"))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && string.Equals(line[..colon].Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return line[(colon + 1)..].Trim();
            }
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _accepting;
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // Stopped.
        }

        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var head = await ReadHeadAsync(stream);
                if (head is null)
                {
                    return;
                }

                lock (_heads)
                {
                    _heads.Add(head);
                }

                await stream.WriteAsync(_answer(head), _stop.Token);
                await stream.FlushAsync(_stop.Token);
                client.Client.Shutdown(SocketShutdown.Send);

                // Until the client closes its side (or 10 s), so that closing sends no reset
                // before it has read the answer.
                using var linger = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                linger.CancelAfter(TimeSpan.FromSeconds(10));
                var buffer = new byte[1024];
                while (await stream.ReadAsync(buffer, linger.Token) > 0)
                {
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // The client went first, or the server stopped.
            }
        }
    }

    private async Task<string?> ReadHeadAsync(NetworkStream stream)
    {
        var head = new List<byte>();
        var one = new byte[1];
        while (head.Count < MaxHeadBytes)
        {
            if (await stream.ReadAsync(one, _stop.Token) == 0)
            {
                return null;
            }

            head.Add(one[0]);
            if (head.Count >= 4 && head[^4] == '\r' && head[^3] == '\n' && head[^2] == '\r' && head[^1] == '\n')
            {
                return Encoding.Latin1.GetString([.. head]);
            }
        }

        return null;
    }
}
