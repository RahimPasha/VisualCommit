using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using VisualCommit.Core.Git;
using VisualCommit.Git;

namespace VisualCommit.Testing;

/// <summary>What the tests' git does with the user name of a URL once the server has refused it (<see cref="GitUrlUser"/>).</summary>
public enum UrlUserAfterRefusal
{
    /// <summary>Git keeps the URL's user and asks for its password: <c>Password for 'http://bob@127.0.0.1:&lt;port&gt;': </c>.</summary>
    Kept,

    /// <summary>Git loses the URL's user and asks for a user name for the bare origin: <c>Username for 'http://127.0.0.1:&lt;port&gt;': </c>.</summary>
    Lost,
}

/// <summary>
/// Whether the tests' git keeps the user name of an HTTP URL that names a user and no password once
/// the server has refused the request, measured once per test process: the prompt that follows
/// names that user, or asks a user name for the bare origin.
/// <para>
/// Git v2.56.0 and older compare the URL they asked for with libcurl's
/// <c>CURLINFO_EFFECTIVE_URL</c> as plain text; a libcurl that removes the credentials from that URL
/// (curl 8.23.0, or any build with CVE-2026-94266's fix, curl 7a6bd027d0) makes git take its own
/// request for a redirect, and git rebuilds its credential from the stripped URL, without the user.
/// Git's 76524c2604bc ("http: handle curl stripping creds from effective url") compares the URL
/// without its credentials too; Git for Windows 2.56.0.windows.3 and later and Ubuntu's patched git
/// packages carry it, Homebrew's 2.56.0_2 on macOS does not. The libcurl git loads at run time
/// decides, so no version of git tells: the git is asked (measured on 2026-11-07: Git for Windows
/// 2.56.0.windows.4, with the fix over curl 8.23.0, and windows.2 over its own curl 8.22.0 keep the
/// user; windows.2 over windows.4's curl 8.23.0 loses it, as CI's macOS job did in run 42437572741).
/// </para>
/// <para>
/// The measure: a listener on 127.0.0.1 that answers every request 401 with a Basic challenge, and
/// <c>git -c credential.helper= ls-remote http://probe@127.0.0.1:&lt;port&gt;/x.git</c> with
/// <see cref="TempRepo.IsolatedEnvironment"/> (no system or user configuration, no askpass program
/// of the machine), no credential helper, <c>GIT_TERMINAL_PROMPT=0</c>, <c>GCM_INTERACTIVE=never</c>
/// and <see cref="GitRunner"/>'s English locale. Git sends the request without credentials, libcurl
/// sends it again with <c>probe</c> and an empty password, and git, refused twice, fails at its
/// prompt; its one line on standard error is the classification:
/// <c>fatal: could not read Password for 'http://probe@127.0.0.1:&lt;port&gt;': terminal prompts disabled</c>
/// (<see cref="UrlUserAfterRefusal.Kept"/>) or
/// <c>fatal: could not read Username for 'http://127.0.0.1:&lt;port&gt;': terminal prompts disabled</c>
/// (<see cref="UrlUserAfterRefusal.Lost"/>). Anything else fails, with git's output and the
/// listener's requests: nothing is guessed.
/// </para>
/// </summary>
public static class GitUrlUser
{
    /// <summary>The user name of the probe's URL.</summary>
    public const string ProbeUser = "probe";

    /// <summary>How long the probe's git may take before the measure fails.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private static readonly Lazy<Task<UrlUserAfterRefusal>> Measured = new(MeasureAsync);

    /// <summary>What the tests' git does with a URL's user after a refusal, measured at the first call of the process and then kept.</summary>
    /// <exception cref="InvalidOperationException">Git printed neither form, or did not end in time.</exception>
    public static Task<UrlUserAfterRefusal> AfterRefusalAsync() => Measured.Value;

    /// <summary>
    /// The classification of the probe's result on <paramref name="port"/>: exit 128 and exactly the
    /// one line of <see cref="UrlUserAfterRefusal.Kept"/> or of <see cref="UrlUserAfterRefusal.Lost"/>
    /// on standard error; null for anything else.
    /// </summary>
    internal static UrlUserAfterRefusal? Classify(GitResult result, int port)
    {
        ArgumentNullException.ThrowIfNull(result);
        var authority = FormattableString.Invariant($"127.0.0.1:{port}");
        var kept = $"fatal: could not read Password for 'http://{ProbeUser}@{authority}': terminal prompts disabled";
        var lost = $"fatal: could not read Username for 'http://{authority}': terminal prompts disabled";
        var lines = result.StandardError.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToList();
        return (result.ExitCode, lines) switch
        {
            (128, [var line]) when line == kept => UrlUserAfterRefusal.Kept,
            (128, [var line]) when line == lost => UrlUserAfterRefusal.Lost,
            _ => null,
        };
    }

    private static async Task<UrlUserAfterRefusal> MeasureAsync()
    {
        using var folder = new TempDirectory("url user probe");
        await using var listener = RefusingListener.Start();
        var url = FormattableString.Invariant($"http://{ProbeUser}@127.0.0.1:{listener.Port}/x.git");
        var environment = new Dictionary<string, string?>(TempRepo.IsolatedEnvironment(folder.Combine("home")))
        {
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GCM_INTERACTIVE"] = "never",
        };
        var command = new GitCommand("-c", "credential.helper=", "ls-remote", url)
        {
            WorkingDirectory = folder.Path,
            Environment = environment,
        };

        GitResult result;
        using (var limit = new CancellationTokenSource(Limit))
        {
            try
            {
                result = await new GitRunner(TempRepo.GitPath).RunAsync(command, limit.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"{command.DisplayText} with the git at {TempRepo.GitPath} did not end within {Limit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s. The listener saw: {listener.Describe()}.");
            }
        }

        return Classify(result, listener.Port) ?? throw new InvalidOperationException(
            $"{command.DisplayText} with the git at {TempRepo.GitPath} exited {result.ExitCode.ToString(CultureInfo.InvariantCulture)}, "
            + $"which tells neither whether git keeps a URL's user after a refusal nor that it loses it. The listener saw: {listener.Describe()}. "
            + $"Standard error:\n{result.StandardError}");
    }

    /// <summary>A listener on 127.0.0.1, port 0, that answers every request 401 with a Basic challenge and <c>Connection: close</c>, one connection at a time, and records each request's line and the user name of its Basic header (never the password).</summary>
    private sealed class RefusingListener : IAsyncDisposable
    {
        /// <summary>The most a request's line and headers may take.</summary>
        private const int MaxHeaderBytes = 64 * 1024;

        private static readonly byte[] Refusal = Encoding.ASCII.GetBytes(
            "HTTP/1.1 401 Unauthorized\r\n"
            + $"WWW-Authenticate: Basic realm=\"{AuthRemoteServer.Realm}\"\r\n"
            + "Content-Length: 0\r\n"
            + "Connection: close\r\n\r\n");

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stopping = new();
        private readonly ConcurrentQueue<string> _requests = new();
        private Task _accepting = Task.CompletedTask;

        private RefusingListener(TcpListener listener)
        {
            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public int Port { get; }

        public static RefusingListener Start()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
            }
            catch
            {
                listener.Dispose();
                throw;
            }

            var refusing = new RefusingListener(listener);
            refusing._accepting = Task.Run(refusing.AcceptAsync);
            return refusing;
        }

        /// <summary>The requests so far, as <c>GET /x.git/info/refs?service=git-upload-pack as probe</c>, or none.</summary>
        public string Describe() => _requests.IsEmpty ? "no request" : string.Join("; ", _requests);

        public async ValueTask DisposeAsync()
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
            _listener.Stop();
            await _accepting.ConfigureAwait(false);
            _listener.Dispose();
            _stopping.Dispose();
        }

        private async Task AcceptAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException or InvalidOperationException)
                {
                    return;
                }

                using (client)
                {
                    try
                    {
                        await RefuseAsync(client.GetStream()).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
                    {
                        // Git went away, or the listener is stopping.
                    }
                }
            }
        }

        /// <summary>Reads one request's line and headers, records it and answers it 401.</summary>
        private async Task RefuseAsync(NetworkStream stream)
        {
            using var reading = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            reading.CancelAfter(Limit);
            var head = new List<byte>();
            var buffer = new byte[4096];
            while (!EndsHead(head))
            {
                var read = await stream.ReadAsync(buffer, reading.Token).ConfigureAwait(false);
                if (read == 0 || head.Count + read > MaxHeaderBytes)
                {
                    _requests.Enqueue("a request whose head did not end");
                    return;
                }

                head.AddRange(new ArraySegment<byte>(buffer, 0, read));
            }

            var lines = Encoding.Latin1.GetString(head.ToArray()).Split("\r\n");
            var user = lines.Skip(1)
                .Where(line => line.StartsWith("Authorization: Basic ", StringComparison.OrdinalIgnoreCase))
                .Select(line => UserNameOf(line["Authorization: Basic ".Length..]))
                .FirstOrDefault();
            _requests.Enqueue($"{lines[0]} as {user ?? "nobody"}");
            await stream.WriteAsync(Refusal, reading.Token).ConfigureAwait(false);
            await stream.FlushAsync(reading.Token).ConfigureAwait(false);
        }

        private static bool EndsHead(List<byte> head) =>
            head.Count >= 4 && head[^4] == '\r' && head[^3] == '\n' && head[^2] == '\r' && head[^1] == '\n';

        /// <summary>The user name of a Basic header's value, never its password; <c>(unreadable)</c> when it cannot be read.</summary>
        private static string UserNameOf(string encoded)
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Trim()));
                var colon = decoded.IndexOf(':', StringComparison.Ordinal);
                return colon < 0 ? "(unreadable)" : decoded[..colon];
            }
            catch (FormatException)
            {
                return "(unreadable)";
            }
        }
    }
}
