using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VisualCommit.Core;
using VisualCommit.Core.Logging;

namespace VisualCommit.App.Services;

/// <summary>What <see cref="SingleInstanceGuard.TryTake"/> found.</summary>
public enum GuardOutcome
{
    /// <summary>This process holds the data folder's lock and serves its endpoint.</summary>
    Taken,

    /// <summary>Another process holds the lock: this start asks it to come to the front.</summary>
    Held,

    /// <summary>The lock file could not be opened at all: the app starts as before, unguarded, with a Warning.</summary>
    Unavailable,
}

/// <summary>How the instance that holds the data folder answered a second start's request.</summary>
public enum ForwardAnswer
{
    /// <summary>Its window was brought to the front.</summary>
    Activated,

    /// <summary>It has no window yet: it is starting, and will show one.</summary>
    Starting,

    /// <summary>Its window has closed: it is ending and will let the lock go.</summary>
    Closing,

    /// <summary>No answer came, or none this version reads.</summary>
    NotAnswered,
}

/// <summary>How a second start's request ended (<see cref="SingleInstanceGuard.ResolveHeld"/>).</summary>
public enum HeldResolution
{
    /// <summary>The lock came free: this process holds the data folder now and starts there.</summary>
    Taken,

    /// <summary>The instance that holds the data folder answered that it came to the front or is starting: this start ends.</summary>
    HandedOver,

    /// <summary>Another process still holds the lock and gave no such answer.</summary>
    NotAnswered,
}

/// <summary>What the desktop app shows at its start.</summary>
public enum AppStartMode
{
    /// <summary>The session, as always.</summary>
    Normal,

    /// <summary>The notice that another VisualCommit holds the data folder and did not answer; no session.</summary>
    NotAnswered,
}

/// <summary>A guard's waits; each guard has its own.</summary>
/// <param name="Connect">How long a second start keeps trying to reach the holder's endpoint.</param>
/// <param name="Reply">How long each side waits for the other's line, and the holder for its window to come forward.</param>
/// <param name="WaitWhileClosing">How long a second start waits for a closing holder to let the lock go.</param>
public sealed record SingleInstanceOptions(TimeSpan Connect, TimeSpan Reply, TimeSpan WaitWhileClosing)
{
    /// <summary>5 s to connect, 3 s for a reply, 30 s for a closing instance: at most about 8.5 s before a second start gives up, the lock's 250 ms before the ask and 250 ms after it included.</summary>
    public static SingleInstanceOptions Default { get; } = new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(30));
}

/// <summary>
/// One instance per data folder (D421), since <c>session.json</c> and the other files are rewritten
/// whole and two instances would undo each other's changes. The first start opens
/// <see cref="AppPaths.InstanceLockFile"/> alone and holds it for its process's life, serves an
/// endpoint of its own (a named pipe on Windows, a socket under the temp folder elsewhere) and names
/// it in <see cref="AppPaths.InstanceFile"/>. A second start on the same folder finds the lock held,
/// asks the first over that endpoint to come to the front (one JSON line each way) and ends; when no
/// answer comes, the app shows a notice instead of a session. It runs in <c>Program.Main</c> before
/// Avalonia exists, so it uses no Avalonia type; its waits come before any UI thread exists.
/// <para>
/// The endpoint is random per run and holds nothing of the data folder's path; its server admits
/// the current user only (D448). Two data folders still run two instances.
/// </para>
/// </summary>
public sealed partial class SingleInstanceGuard : IDisposable
{
    /// <summary>
    /// How the server is made (D448): only the current user can connect (on Windows the pipe admits
    /// that user alone; on macOS and Linux the peer's user id is checked), and its reads and waits can
    /// be cancelled. Public for the tests, since the app has no <c>InternalsVisibleTo</c>.
    /// </summary>
    public const PipeOptions ServerPipeOptions = PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous;

    /// <summary>How a second start connects: to a server of the current user only.</summary>
    private const PipeOptions ClientPipeOptions = PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous;

    /// <summary>The longest line either side reads; anything longer gets no answer.</summary>
    private const int MaxLineBytes = 4096;

    /// <summary>A socket path longer than this falls back to <c>/tmp</c> (macOS allows 104 bytes).</summary>
    private const int MaxSocketPathBytes = 100;

    private const string EndpointPrefix = "VisualCommit-";

    /// <summary>On macOS and Linux, a lock file the guard makes: read and write for the user alone.</summary>
    private const UnixFileMode UserOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>On macOS and Linux, a folder the guard makes: the user's alone.</summary>
    private const UnixFileMode UserOnlyFolder = UserOnlyFile | UnixFileMode.UserExecute;

    /// <summary>The variable through which .NET is told to take no file locks on macOS and Linux.</summary>
    private const string LockingOffVariable = "DOTNET_SYSTEM_IO_DISABLEFILELOCKING";

    /// <summary>The runtime switch that does the same.</summary>
    private const string LockingOffSwitch = "System.IO.DisableFileLocking";

    /// <summary>A virus scanner or the indexer may hold the lock file for a moment: the first open is tried this long.</summary>
    private static readonly TimeSpan FirstOpenRetries = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan FirstOpenPause = TimeSpan.FromMilliseconds(50);

    /// <summary>The pause between two attempts to reach the holder, and the limit of one connection attempt.</summary>
    private static readonly TimeSpan AttemptPause = TimeSpan.FromMilliseconds(100);

    /// <summary>How often <see cref="WaitToTake"/> tries the lock.</summary>
    private static readonly TimeSpan TakePause = TimeSpan.FromMilliseconds(250);

    /// <summary>How long the holder keeps trying to write its instance file while a second start reads it.</summary>
    private static readonly TimeSpan InstanceFileRetries = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan InstanceFilePause = TimeSpan.FromMilliseconds(20);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _gate = new();
    private readonly AppPaths _paths;
    private readonly IAppLog _log;
    private readonly SingleInstanceOptions _options;
    private FileStream? _lock;
    private CancellationTokenSource? _serving;
    private Task? _server;
    private string? _endpoint;

    /// <summary>The endpoint an ask last reached (<see cref="AskOnce"/>), which <see cref="WaitToTake"/> tells a new holder's from.</summary>
    private string? _askedEndpoint;

    private bool _disposed;

    private SingleInstanceGuard(AppPaths paths, IAppLog log, SingleInstanceOptions options)
    {
        _paths = paths;
        _log = log;
        _options = options;
    }

    /// <summary>What the first attempt found; <see cref="GuardOutcome.Held"/> turns <see cref="GuardOutcome.Taken"/> after a successful <see cref="TryTakeNow"/> or <see cref="ResolveHeld"/>.</summary>
    public GuardOutcome Outcome { get; private set; }

    /// <summary>This guard's own endpoint while it is <see cref="GuardOutcome.Taken"/>; null otherwise.</summary>
    public string? Endpoint
    {
        get
        {
            lock (_gate)
            {
                return Outcome == GuardOutcome.Taken ? _endpoint : null;
            }
        }
    }

    /// <summary>
    /// While <see cref="GuardOutcome.Taken"/>, what a second start's request does: run on a pool
    /// thread and waited for at most <see cref="SingleInstanceOptions.Reply"/>; null answers
    /// <see cref="ForwardAnswer.Starting"/> (no window yet). The app sets it when its window opens and
    /// again when it closes.
    /// </summary>
    public Func<ForwardAnswer>? ComeForward { get; set; }

    private string Folder => _paths.DataDirectory;

    /// <summary>
    /// Opens the data folder's lock file alone, creating the folder and the file as needed. Never
    /// throws: a lock held by another process gives <see cref="GuardOutcome.Held"/>, one that cannot
    /// be opened at all <see cref="GuardOutcome.Unavailable"/> with a Warning. Taken, it starts the
    /// server and writes <see cref="AppPaths.InstanceFile"/>.
    /// </summary>
    public static SingleInstanceGuard TryTake(AppPaths paths, IAppLog log, SingleInstanceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(log);
        var guard = new SingleInstanceGuard(paths, log, options ?? SingleInstanceOptions.Default);
        guard.TakeFirst();
        return guard;
    }

    /// <summary>
    /// The Warning that a second VisualCommit on <paramref name="dataDirectory"/> is not stopped,
    /// because .NET takes no file locks on macOS and Linux while <c>DOTNET_SYSTEM_IO_DISABLEFILELOCKING</c>
    /// is <c>1</c> or <c>true</c> (in any case) or the switch <c>System.IO.DisableFileLocking</c> is
    /// on; null on Windows, where locks are always taken, and when neither is set. Pure.
    /// </summary>
    public static string? LockingOffWarning(string dataDirectory, string? variableValue, bool switchOn, bool windows)
    {
        var off = switchOn || variableValue == "1" || string.Equals(variableValue, "true", StringComparison.OrdinalIgnoreCase);
        return windows || !off
            ? null
            : $"File locking is turned off ({LockingOffVariable} or {LockingOffSwitch}): a second VisualCommit on the data folder {dataDirectory} is not stopped.";
    }

    /// <summary>
    /// Asks the instance that holds the lock to come to the front: until
    /// <see cref="SingleInstanceOptions.Connect"/> has passed, an attempt every 100 ms, each reading
    /// <see cref="AppPaths.InstanceFile"/> again (a missing or half-written file, and an endpoint that
    /// refuses the connection, are tried again), then one request and at most
    /// <see cref="SingleInstanceOptions.Reply"/> for its answer. Logs what came.
    /// </summary>
    /// <exception cref="InvalidOperationException">The guard is not <see cref="GuardOutcome.Held"/>.</exception>
    public ForwardAnswer AskToComeForward()
    {
        if (Outcome != GuardOutcome.Held)
        {
            throw new InvalidOperationException($"Only a guard whose lock another process holds asks it to come forward; this one is {Outcome}.");
        }

        var clock = Stopwatch.StartNew();
        while (true)
        {
            string why;
            if (ReadInstanceFile() is { } holder)
            {
                if (AskOnce(holder) is { } answer)
                {
                    if (answer == ForwardAnswer.Closing)
                    {
                        LogClosing(holder.Pid, _options.WaitWhileClosing);
                    }

                    return answer;
                }

                why = "its endpoint refused the connection";
            }
            else
            {
                why = "no instance.json";
            }

            if (clock.Elapsed >= _options.Connect)
            {
                _log.Warning($"VisualCommit holds the data folder {Folder} but did not answer ({why}).");
                return ForwardAnswer.NotAnswered;
            }

            Thread.Sleep(AttemptPause);
        }
    }

    /// <summary>One attempt at the lock, with no retries: on success the guard is <see cref="GuardOutcome.Taken"/>, its server runs and <see cref="AppPaths.InstanceFile"/> is written.</summary>
    public bool TryTakeNow()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            if (Outcome == GuardOutcome.Taken)
            {
                return true;
            }

            if (Outcome != GuardOutcome.Held)
            {
                return false;
            }

            try
            {
                _lock = OpenLock();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }

            Outcome = GuardOutcome.Taken;
            BeginServing();
            return true;
        }
    }

    /// <summary>
    /// A <see cref="GuardOutcome.Held"/> start's whole request, which <c>Program.Main</c> and the
    /// notice's "Try again" both make: <see cref="AskToComeForward"/>, whose
    /// <see cref="ForwardAnswer.Activated"/> or <see cref="ForwardAnswer.Starting"/> hands over, whose
    /// <see cref="ForwardAnswer.Closing"/> is waited for (<see cref="WaitToTake"/>), and whose
    /// <see cref="ForwardAnswer.NotAnswered"/> has the lock tried again for 250 ms
    /// (<see cref="TakeAfterNoAnswer"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The guard is not <see cref="GuardOutcome.Held"/>.</exception>
    public HeldResolution ResolveHeld() => AskToComeForward() switch
    {
        ForwardAnswer.Activated or ForwardAnswer.Starting => HeldResolution.HandedOver,
        ForwardAnswer.Closing => WaitToTake(),
        _ => TakeAfterNoAnswer(),
    };

    /// <summary>
    /// After a <see cref="ForwardAnswer.Closing"/>: <see cref="TryTakeNow"/> every 250 ms until it
    /// succeeds (<see cref="HeldResolution.Taken"/>) or <see cref="SingleInstanceOptions.WaitWhileClosing"/>
    /// has passed (<see cref="HeldResolution.NotAnswered"/>). Another start may take the lock the
    /// closing instance lets go: while <see cref="AppPaths.InstanceFile"/> names an endpoint other than
    /// the one asked last, that instance is asked instead, one attempt between two tries of the lock,
    /// so that a new holder that ends before it answers leaves the lock to this start at once. Its
    /// answer counts as the first one's would: a <see cref="ForwardAnswer.Closing"/> is waited for
    /// within the same time, and the log tells it what is left of that time. Logs which.
    /// </summary>
    public HeldResolution WaitToTake()
    {
        var clock = Stopwatch.StartNew();

        // What the last "is closing" line said this start waits: the whole wait for the first
        // instance, what was left of it for one that answered while this start waited.
        var waitsFor = _options.WaitWhileClosing;
        string? announced = null;
        var refused = false;
        while (true)
        {
            if (TryTakeNow())
            {
                _log.Info("The other VisualCommit closed; this start goes on.");
                return HeldResolution.Taken;
            }

            if (clock.Elapsed >= _options.WaitWhileClosing)
            {
                // A new holder whose endpoint refused the last attempt holds the folder now: the
                // closing one did close.
                _log.Warning(refused
                    ? $"VisualCommit holds the data folder {Folder} but did not answer (its endpoint refused the connection)."
                    : $"The other VisualCommit did not close within {Seconds(waitsFor)} s.");
                return HeldResolution.NotAnswered;
            }

            // No file: the closing instance deleted its own before letting the lock go, and no new
            // holder has written one yet. The lock is tried again.
            refused = false;
            if (ReadInstanceFile() is { } holder && holder.Endpoint != _askedEndpoint)
            {
                if (holder.Endpoint != announced)
                {
                    _log.Info("Another VisualCommit took the data folder while this start waited; it is asked instead.");
                    announced = holder.Endpoint;
                }

                switch (AskOnce(holder))
                {
                    case ForwardAnswer.Activated or ForwardAnswer.Starting:
                        return HeldResolution.HandedOver;
                    case ForwardAnswer.NotAnswered:
                        return TakeAfterNoAnswer();
                    case ForwardAnswer.Closing:
                        waitsFor = LeftOfWait(clock.Elapsed);
                        LogClosing(holder.Pid, waitsFor);
                        continue;
                    case null:
                        refused = true;
                        break;
                }
            }

            Thread.Sleep(TakePause);
        }
    }

    /// <summary>
    /// Taken: stops the server, deletes <see cref="AppPaths.InstanceFile"/> (best effort) and only
    /// then lets the lock go, so that the file it deletes is its own. Held or Unavailable: touches no
    /// file, since the instance file belongs to whoever holds the lock. The lock file stays: deleted,
    /// two processes could lock two files of one name. Twice is harmless.
    /// </summary>
    public void Dispose()
    {
        Task? server;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_lock is null)
            {
                return;
            }

            _serving?.Cancel();
            server = _server;
        }

        // The server's loop ends at once on the cancellation; a ComeForward that blocks is not
        // waited for.
        try
        {
            server?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // The loop's own failures are logged where they happen.
        }

        try
        {
            File.Delete(_paths.InstanceFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a second start that reads a stale endpoint tries again and then shows the notice.
        }

        lock (_gate)
        {
            _lock.Dispose();
            _lock = null;
            _serving?.Dispose();
            _serving = null;
        }
    }

    private static string Seconds(TimeSpan span) => span.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// What is left of <see cref="SingleInstanceOptions.WaitWhileClosing"/> after <paramref name="waited"/>,
    /// in tenths of a second rounded down, so that a line that names it never names more; never less than 0.
    /// </summary>
    private TimeSpan LeftOfWait(TimeSpan waited) =>
        TimeSpan.FromTicks(Math.Max(0L, (long)Math.Floor((_options.WaitWhileClosing - waited).TotalSeconds * 10)) * (TimeSpan.TicksPerSecond / 10));

    /// <summary>The line for an instance that answered <see cref="ForwardAnswer.Closing"/>, with how long this start waits for it.</summary>
    private void LogClosing(int pid, TimeSpan waitsFor) =>
        _log.Info($"VisualCommit on the data folder {Folder} (process {pid.ToString(CultureInfo.InvariantCulture)}) is closing; this start waits up to {Seconds(waitsFor)} s for it.");

    /// <summary>
    /// After an ask that got no answer: the lock tried again for 250 ms, as the first attempt is,
    /// since a holder that ended during the ask (closed, crashed, or ended by the user as the notice
    /// advises) has let it go, and the notice would then name a VisualCommit that no longer runs.
    /// <see cref="HeldResolution.Taken"/>, or <see cref="HeldResolution.NotAnswered"/> while another
    /// process still holds it.
    /// </summary>
    private HeldResolution TakeAfterNoAnswer()
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            if (TryTakeNow())
            {
                _log.Info($"The data folder {Folder} is free now; this start goes on.");
                return HeldResolution.Taken;
            }

            if (clock.Elapsed >= FirstOpenRetries)
            {
                return HeldResolution.NotAnswered;
            }

            Thread.Sleep(FirstOpenPause);
        }
    }

    /// <summary>The first attempt: the folder, the lock (tried for 250 ms while the file exists), then <see cref="BeginServing"/>.</summary>
    private void TakeFirst()
    {
        if (!OperatingSystem.IsWindows())
        {
            var switchOn = AppContext.TryGetSwitch(LockingOffSwitch, out var on) && on;
            if (LockingOffWarning(Folder, Environment.GetEnvironmentVariable(LockingOffVariable), switchOn, windows: false) is { } warning)
            {
                _log.Warning(warning);
            }
        }

        var clock = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                CreateFolder();
                var stream = OpenLock();
                lock (_gate)
                {
                    _lock = stream;
                    Outcome = GuardOutcome.Taken;
                    BeginServing();
                }

                return;
            }
            catch (IOException) when (LockFileExists())
            {
                // Held by another process, or for a moment by a scanner: no HResult is read, since
                // Unix's for a held flock is not checked.
                if (clock.Elapsed >= FirstOpenRetries)
                {
                    Outcome = GuardOutcome.Held;
                    return;
                }

                Thread.Sleep(FirstOpenPause);
            }
            catch (Exception ex)
            {
                // Whatever else stops the open (no access, a path under a file, a name the system
                // refuses): the app starts as before, since the guard must never stop it.
                Outcome = GuardOutcome.Unavailable;
                _log.Warning($"The data folder {Folder} could not be locked against a second VisualCommit ({ex.Message}); this one starts without that guard.");
                return;
            }
        }
    }

    private bool LockFileExists()
    {
        try
        {
            return File.Exists(_paths.InstanceLockFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// The data folder, made with each missing folder above it. On macOS and Linux a folder made here
    /// is the user's alone; one that exists keeps its mode.
    /// </summary>
    private void CreateFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(Folder);
        }
        else
        {
            Directory.CreateDirectory(Folder, UserOnlyFolder);
        }
    }

    /// <summary>
    /// The lock file, opened alone. On macOS and Linux a file made here is the user's alone, since
    /// another user who could open it could hold its lock, and every start of this user's would then
    /// find the folder held; one that exists keeps its mode.
    /// </summary>
    private FileStream OpenLock() => OperatingSystem.IsWindows()
        ? new(_paths.InstanceLockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
        : new(_paths.InstanceLockFile, new FileStreamOptions
        {
            Mode = FileMode.OpenOrCreate,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            UnixCreateMode = UserOnlyFile,
        });

    /// <summary>
    /// The holder's steps, under <see cref="_gate"/> with the lock just opened: the old instance
    /// file's socket removed if it names one of ours that no server answers, the server started on a
    /// new endpoint, the instance file written.
    /// </summary>
    private void BeginServing()
    {
        RemoveStaleSocket();
        _endpoint = NewEndpoint();
        NamedPipeServerStream first;
        try
        {
            first = NewServer(_endpoint);
        }
        catch (Exception ex) when (ex is IOException or SocketException or UnauthorizedAccessException or PlatformNotSupportedException or ArgumentException)
        {
            // The guard stays Taken: a second start then gets the notice, not a second session.
            _log.Warning($"The single-instance endpoint could not be opened ({ex.Message}): a second start on this data folder will be told that this one did not answer.");
            return;
        }

        _serving = new CancellationTokenSource();
        var token = _serving.Token;
        _server = Task.Run(() => ServeAsync(first, token), CancellationToken.None);

        if (WriteInstanceFile(_endpoint) is { } failure)
        {
            _log.Warning($"The data folder {Folder} has no instance.json ({failure.Message}): a second start on it will be told that this one did not answer.");
            return;
        }

        _log.Info($"This VisualCommit holds the data folder {Folder}: a second start on it is sent here.");
    }

    /// <summary>
    /// Writes <see cref="AppPaths.InstanceFile"/> naming <paramref name="endpoint"/>, again every
    /// 20 ms for up to a second while that fails: a second start that reads the file holds it open
    /// for a moment, and Windows replaces no file that is open. Null once written; the last failure
    /// when it never was.
    /// </summary>
    private Exception? WriteInstanceFile(string endpoint)
    {
        var text = InstanceJson(Environment.ProcessId, endpoint, DateTimeOffset.Now);
        var clock = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                DurableFile.Replace(_paths.InstanceFile, text);
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (clock.Elapsed >= InstanceFileRetries)
                {
                    return ex;
                }

                Thread.Sleep(InstanceFilePause);
            }
        }
    }

    /// <summary>
    /// A crashed run's socket, named in the instance file it left, exactly of this guard's form, that
    /// no server answers; deleted only then. A free lock does not prove its owner gone: a data folder
    /// copied while its instance ran names that instance's live socket, which a start on the copy
    /// must leave to it (D421).
    /// </summary>
    private void RemoveStaleSocket()
    {
        if (ReadInstanceFile() is not { } old || !IsOurSocketPath(old.Endpoint))
        {
            return;
        }

        try
        {
            if (!File.Exists(old.Endpoint))
            {
                return;
            }

            if (TryConnect(old.Endpoint) is { } live)
            {
                // Hung up at once: its server drops a client that sends nothing, unanswered and unlogged.
                live.Dispose();
                return;
            }

            File.Delete(old.Endpoint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left: the new endpoint has another name.
        }
    }

    private static bool IsOurSocketPath(string endpoint)
    {
        var name = Path.GetFileName(endpoint);
        return EndpointName().IsMatch(name) && (endpoint == Path.GetTempPath() + name || endpoint == "/tmp/" + name);
    }

    /// <summary>
    /// A new endpoint: on Windows the pipe name <c>VisualCommit-&lt;16 hex&gt;</c>; on macOS and Linux
    /// the socket path of that name in the temp folder, or in <c>/tmp</c> when that path is over 100
    /// bytes. Nothing of the data folder's path.
    /// </summary>
    private static string NewEndpoint()
    {
        var name = EndpointPrefix + RandomNumberGenerator.GetHexString(16, lowercase: true);
        if (OperatingSystem.IsWindows())
        {
            return name;
        }

        var path = Path.GetTempPath() + name;
        return Utf8.GetByteCount(path) <= MaxSocketPathBytes ? path : "/tmp/" + name;
    }

    /// <summary>Two instances, so that the next one listens before the one that served is let go.</summary>
    private static NamedPipeServerStream NewServer(string endpoint) =>
        new(endpoint, PipeDirection.InOut, 2, PipeTransmissionMode.Byte, ServerPipeOptions);

    /// <summary>
    /// The server: one client at a time, each read for at most <see cref="SingleInstanceOptions.Reply"/>
    /// and 4 KB. A connection it refuses (another user's) is dropped unanswered; a malformed line,
    /// another version or another request gets no answer; the next client is awaited after each.
    /// </summary>
    private async Task ServeAsync(NamedPipeServerStream first, CancellationToken token)
    {
        var server = first;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    await AnswerAsync(server, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is IOException or SocketException or UnauthorizedAccessException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
                {
                    // Dropped unanswered: the client went, broke the pipe or is not the current user.
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                NamedPipeServerStream next;
                try
                {
                    next = NewServer(_endpoint!);
                }
                catch (Exception ex) when (ex is IOException or SocketException or UnauthorizedAccessException or PlatformNotSupportedException or ArgumentException)
                {
                    _log.Warning($"The single-instance endpoint could not be opened ({ex.Message}): a second start on this data folder will be told that this one did not answer.");
                    return;
                }

                await server.DisposeAsync().ConfigureAwait(false);
                server = next;
            }
        }
        finally
        {
            await server.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Reads the request; a valid one gets the window's answer, sent within the reply's time, and the line that says so.</summary>
    private async Task AnswerAsync(NamedPipeServerStream server, CancellationToken token)
    {
        using var reply = CancellationTokenSource.CreateLinkedTokenSource(token);
        reply.CancelAfter(_options.Reply);
        var line = await ReadLineAsync(server, reply.Token).ConfigureAwait(false);
        if (line is null || ReadRequest(line) is not { } pid)
        {
            return;
        }

        var (answer, failure) = await RunComeForwardAsync(token).ConfigureAwait(false);
        if (answer is not { } given)
        {
            _log.Warning($"A second VisualCommit (process {pid}) started on this data folder, and the window did not come forward within {Seconds(_options.Reply)} s.", failure);
            return;
        }

        var word = given switch
        {
            ForwardAnswer.Activated => "activated",
            ForwardAnswer.Closing => "closing",
            _ => "starting",
        };
        using var send = CancellationTokenSource.CreateLinkedTokenSource(token);
        send.CancelAfter(_options.Reply);
        await server.WriteAsync(AnswerJson(word), send.Token).ConfigureAwait(false);
        await server.FlushAsync(send.Token).ConfigureAwait(false);
        _log.Info($"A second VisualCommit (process {pid}) started on this data folder: answered {word}.");

        // The asker hangs up once it has read the answer; until then, within the same time, the
        // connection stays open, so that closing it can never drop an answer not yet read.
        await DrainAsync(server, send.Token).ConfigureAwait(false);
    }

    /// <summary>Reads and drops whatever comes until the other side ends the connection or the token is cancelled.</summary>
    private static async Task DrainAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[256];
        try
        {
            while (await stream.ReadAsync(buffer, token).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
            // Hung up, broken, or out of time: the connection ends either way.
        }
    }

    /// <summary>
    /// <see cref="ComeForward"/>'s answer, run on a pool thread and waited for at most the reply's
    /// time (a dispatch to a busy UI thread may take longer; it is not waited for); null after a
    /// timeout or an answer that is no answer, and null with the exception after a throw.
    /// </summary>
    private async Task<(ForwardAnswer? Answer, Exception? Failure)> RunComeForwardAsync(CancellationToken token)
    {
        if (ComeForward is not { } comeForward)
        {
            return (ForwardAnswer.Starting, null);
        }

        try
        {
            var answer = await Task.Run(comeForward, CancellationToken.None).WaitAsync(_options.Reply, token).ConfigureAwait(false);
            return (answer == ForwardAnswer.NotAnswered ? null : answer, null);
        }
        catch (TimeoutException)
        {
            return (null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, ex);
        }
    }

    /// <summary>
    /// One attempt to reach <paramref name="holder"/>: a connection within 100 ms and, made, the
    /// request and its answer (<see cref="Exchange"/>); null when the endpoint refused the connection.
    /// </summary>
    private ForwardAnswer? AskOnce((int Pid, string Endpoint) holder)
    {
        if (OperatingSystem.IsWindows())
        {
            // Lets the holder take the foreground, which this process, started by the user, may give
            // away; the holder's own attempt alone is refused by Windows.
            _ = AllowSetForegroundWindow(holder.Pid);
        }

        if (TryConnect(holder.Endpoint) is not { } connection)
        {
            return null;
        }

        _askedEndpoint = holder.Endpoint;
        using (connection)
        {
            return Exchange(connection, holder.Pid.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// A connection to <paramref name="endpoint"/>, or null when it refused it within the attempt's
    /// 100 ms. A name the pipe client refuses as it is made (the instance file is read as it stands)
    /// counts as a refused connection.
    /// </summary>
    private static NamedPipeClientStream? TryConnect(string endpoint)
    {
        NamedPipeClientStream? client = null;
        try
        {
            client = new NamedPipeClientStream(".", endpoint, PipeDirection.InOut, ClientPipeOptions);
            client.Connect((int)AttemptPause.TotalMilliseconds);
            return client;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or SocketException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException or InvalidOperationException)
        {
            client?.Dispose();
            return null;
        }
    }

    /// <summary>The request and its answer over a connected pipe, logged; a <see cref="ForwardAnswer.Closing"/> is logged by the caller (<see cref="LogClosing"/>).</summary>
    private ForwardAnswer Exchange(NamedPipeClientStream connection, string pid)
    {
        string? line;
        try
        {
            using var reply = new CancellationTokenSource(_options.Reply);
            connection.WriteAsync(RequestJson(Environment.ProcessId), reply.Token).AsTask().GetAwaiter().GetResult();
            connection.FlushAsync(reply.Token).GetAwaiter().GetResult();
            line = ReadLineAsync(connection, reply.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            line = null;
        }

        if (line is null)
        {
            _log.Warning($"VisualCommit holds the data folder {Folder} but did not answer (no reply within {Seconds(_options.Reply)} s).");
            return ForwardAnswer.NotAnswered;
        }

        switch (ReadAnswer(line))
        {
            case ForwardAnswer.Activated:
                _log.Info($"VisualCommit already runs on the data folder {Folder} (process {pid}); it was asked to come to the front, and this start ends.");
                return ForwardAnswer.Activated;
            case ForwardAnswer.Starting:
                _log.Info($"VisualCommit is starting on the data folder {Folder} (process {pid}); this start ends.");
                return ForwardAnswer.Starting;
            case ForwardAnswer.Closing:
                // Logged by the caller, which knows how long it waits (LogClosing).
                return ForwardAnswer.Closing;
            default:
                _log.Warning($"VisualCommit holds the data folder {Folder} but did not answer (a reply this version does not read).");
                return ForwardAnswer.NotAnswered;
        }
    }

    /// <summary>
    /// One line, up to its <c>\n</c>, of at most 4 KB; null when the other side ends the connection
    /// first, sends more, or the token is cancelled. Anything after the line is ignored.
    /// </summary>
    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[MaxLineBytes + 1];
        var length = 0;
        try
        {
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(length), token).ConfigureAwait(false);
                if (read == 0)
                {
                    return null;
                }

                var end = Array.IndexOf(buffer, (byte)'\n', length, read);
                length += read;
                if (end >= 0)
                {
                    return end <= MaxLineBytes ? Utf8.GetString(buffer, 0, end) : null;
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
            return null;
        }

        return null;
    }

    /// <summary>The second start's process id, from a request of this version; null for anything else.</summary>
    private static int? ReadRequest(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("v", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var v) && v == 1
                && root.TryGetProperty("request", out var request) && request.ValueKind == JsonValueKind.String && request.GetString() == "come-forward"
                && root.TryGetProperty("pid", out var pid) && pid.ValueKind == JsonValueKind.Number && pid.TryGetInt32(out var id)
                ? id
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The holder's answer, from an answer of this version; <see cref="ForwardAnswer.NotAnswered"/> for anything else.</summary>
    private static ForwardAnswer ReadAnswer(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("v", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v) || v != 1
                || !root.TryGetProperty("answer", out var answer) || answer.ValueKind != JsonValueKind.String)
            {
                return ForwardAnswer.NotAnswered;
            }

            return answer.GetString() switch
            {
                "activated" => ForwardAnswer.Activated,
                "starting" => ForwardAnswer.Starting,
                "closing" => ForwardAnswer.Closing,
                _ => ForwardAnswer.NotAnswered,
            };
        }
        catch (JsonException)
        {
            return ForwardAnswer.NotAnswered;
        }
    }

    /// <summary>The holder's process id and endpoint from <see cref="AppPaths.InstanceFile"/>; null while it is missing, half-written or of another version.</summary>
    private (int Pid, string Endpoint)? ReadInstanceFile()
    {
        try
        {
            // Shared for writing and deleting, so that the holder can replace or delete it meanwhile.
            using var stream = new FileStream(_paths.InstanceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("v", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var v) && v == 1
                && root.TryGetProperty("pid", out var pid) && pid.ValueKind == JsonValueKind.Number && pid.TryGetInt32(out var id)
                && root.TryGetProperty("endpoint", out var endpoint) && endpoint.ValueKind == JsonValueKind.String
                && endpoint.GetString() is { Length: > 0 } name)
            {
                return (id, name);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary><c>{"v":1,"pid":…,"endpoint":"…","started":"&lt;ISO 8601 with offset&gt;"}</c>.</summary>
    private static string InstanceJson(int pid, string endpoint, DateTimeOffset started) =>
        Utf8.GetString(Json(writer =>
        {
            writer.WriteNumber("v", 1);
            writer.WriteNumber("pid", pid);
            writer.WriteString("endpoint", endpoint);
            writer.WriteString("started", started);
        }));

    /// <summary><c>{"v":1,"request":"come-forward","pid":…,"args":[]}</c> and its line end. The arguments stay empty: a second start opens nothing.</summary>
    private static byte[] RequestJson(int pid) =>
        Line(Json(writer =>
        {
            writer.WriteNumber("v", 1);
            writer.WriteString("request", "come-forward");
            writer.WriteNumber("pid", pid);
            writer.WriteStartArray("args");
            writer.WriteEndArray();
        }));

    /// <summary><c>{"v":1,"answer":"…"}</c> and its line end.</summary>
    private static byte[] AnswerJson(string word) =>
        Line(Json(writer =>
        {
            writer.WriteNumber("v", 1);
            writer.WriteString("answer", word);
        }));

    private static byte[] Json(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static byte[] Line(byte[] json) => [.. json, (byte)'\n'];

    [GeneratedRegex("^VisualCommit-[0-9a-f]{16}$", RegexOptions.CultureInvariant)]
    private static partial Regex EndpointName();

    /// <summary>Lets the process <paramref name="processId"/> set the foreground window (user32); failure is ignored.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
