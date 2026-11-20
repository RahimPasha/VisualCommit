using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VisualCommit.App.Services;
using VisualCommit.Core;
using VisualCommit.Core.Logging;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The single-instance guard (docs/phase-8a-design.md 1.11, 4.3.2 to 4.3.5, 4.5; D421, D422, D448),
/// on temporary data folders named <c>data folder</c>, with a space. It runs in <c>Program.Main</c>,
/// which no harness reaches, so these tests are its proof on every platform. Each guard has the
/// options of its own side: one expected to answer, serve, take or wait gets <see cref="Patient"/>,
/// and every wait ends as soon as its condition holds; 300 ms only on the side whose expected result
/// is that no answer comes, where a stall under load can only make that result surer. No test asserts
/// an elapsed time under a second (D237).
/// </summary>
public sealed class SingleInstanceGuardTests
{
    /// <summary>Connect 5 s, Reply 5 s, WaitWhileClosing 30 s: the side expected to answer, serve, take or wait.</summary>
    private static readonly SingleInstanceOptions Patient = new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

    /// <summary>The longest any test waits for a condition, such as a line in a log.</summary>
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    private static readonly int Pid = Environment.ProcessId;

    [Fact]
    public void A_second_guard_on_a_taken_folder_is_held_and_one_on_another_folder_takes_it()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        var other = new AppPaths(root.Combine("other data folder"));
        var log = new RecordingLog();

        using var first = SingleInstanceGuard.TryTake(paths, log, Patient);
        using var second = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        using var third = SingleInstanceGuard.TryTake(other, new RecordingLog(), Patient);

        Assert.Equal(GuardOutcome.Taken, first.Outcome);
        Assert.NotNull(first.Endpoint);
        Assert.Equal(GuardOutcome.Held, second.Outcome);
        Assert.Null(second.Endpoint);
        Assert.Equal(GuardOutcome.Taken, third.Outcome);
        Assert.True(File.Exists(paths.InstanceLockFile));
        Assert.True(File.Exists(paths.InstanceFile));
        Assert.Contains(Info($"This VisualCommit holds the data folder {paths.DataDirectory}: a second start on it is sent here."), log.Entries.Select(Line));

        using var file = JsonDocument.Parse(File.ReadAllBytes(paths.InstanceFile));
        Assert.Equal(1, file.RootElement.GetProperty("v").GetInt32());
        Assert.Equal(Pid, file.RootElement.GetProperty("pid").GetInt32());
        Assert.Equal(first.Endpoint, file.RootElement.GetProperty("endpoint").GetString());
        Assert.True(DateTimeOffset.TryParse(file.RootElement.GetProperty("started").GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
    }

    [Fact]
    public async Task A_second_starts_ask_reaches_the_holders_come_forward_once_and_without_one_it_is_starting()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        var holderLog = new RecordingLog();
        using var holder = SingleInstanceGuard.TryTake(paths, holderLog, Patient);
        var calls = 0;
        holder.ComeForward = () =>
        {
            Interlocked.Increment(ref calls);
            return ForwardAnswer.Activated;
        };
        var askerLog = new RecordingLog();
        using var asker = SingleInstanceGuard.TryTake(paths, askerLog, Patient);

        Assert.Equal(ForwardAnswer.Activated, asker.AskToComeForward());
        Assert.Equal(1, Volatile.Read(ref calls));
        await WaitForLineAsync(holderLog, Info($"A second VisualCommit (process {Pid}) started on this data folder: answered activated."));
        Assert.Contains(Info($"VisualCommit already runs on the data folder {paths.DataDirectory} (process {Pid}); it was asked to come to the front, and this start ends."), askerLog.Entries.Select(Line));

        holder.ComeForward = null;
        Assert.Equal(ForwardAnswer.Starting, asker.AskToComeForward());
        Assert.Equal(1, Volatile.Read(ref calls));
        await WaitForLineAsync(holderLog, Info($"A second VisualCommit (process {Pid}) started on this data folder: answered starting."));
        Assert.Contains(Info($"VisualCommit is starting on the data folder {paths.DataDirectory} (process {Pid}); this start ends."), askerLog.Entries.Select(Line));
        Assert.Equal(GuardOutcome.Held, asker.Outcome);
    }

    [Fact]
    public void A_held_lock_without_instance_json_is_not_answered_once_connect_has_passed()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        Directory.CreateDirectory(paths.DataDirectory);
        using var held = new FileStream(paths.InstanceLockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var options = new SingleInstanceOptions(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(30));
        var log = new RecordingLog();
        using var asker = SingleInstanceGuard.TryTake(paths, log, options);
        Assert.Equal(GuardOutcome.Held, asker.Outcome);

        var clock = Stopwatch.StartNew();
        var answer = asker.AskToComeForward();
        var elapsed = clock.Elapsed;

        Assert.Equal(ForwardAnswer.NotAnswered, answer);
        Assert.True(elapsed >= options.Connect, $"The ask gave up after {elapsed.TotalMilliseconds:F0} ms, before Connect's {options.Connect.TotalMilliseconds:F0} ms.");
        Assert.True(elapsed <= options.Connect + TimeSpan.FromSeconds(5), $"The ask took {elapsed.TotalMilliseconds:F0} ms.");
        Assert.Contains(Warning($"VisualCommit holds the data folder {paths.DataDirectory} but did not answer (no instance.json)."), log.Entries.Select(Line));
        Assert.False(File.Exists(paths.InstanceFile));
    }

    [Fact]
    public async Task A_closing_holder_is_waited_for_and_the_waiter_takes_the_folder_and_serves()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        var holder = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        try
        {
            holder.ComeForward = () => ForwardAnswer.Closing;
            var log = new RecordingLog();
            using var second = SingleInstanceGuard.TryTake(paths, log, Patient);

            Assert.Equal(ForwardAnswer.Closing, second.AskToComeForward());
            Assert.Contains(Info($"VisualCommit on the data folder {paths.DataDirectory} (process {Pid}) is closing; this start waits up to 30 s for it."), log.Entries.Select(Line));

            var disposal = Task.Run(
                async () =>
                {
                    await Task.Delay(300, TestContext.Current.CancellationToken);
                    holder.Dispose();
                },
                TestContext.Current.CancellationToken);
            Assert.True(second.WaitToTake());
            await disposal;

            Assert.Equal(GuardOutcome.Taken, second.Outcome);
            Assert.NotNull(second.Endpoint);
            Assert.Contains(Info("The other VisualCommit closed; this start goes on."), log.Entries.Select(Line));
            using (var file = JsonDocument.Parse(File.ReadAllBytes(paths.InstanceFile)))
            {
                Assert.Equal(second.Endpoint, file.RootElement.GetProperty("endpoint").GetString());
            }

            second.ComeForward = () => ForwardAnswer.Activated;
            using var third = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
            Assert.Equal(GuardOutcome.Held, third.Outcome);
            Assert.Equal(ForwardAnswer.Activated, third.AskToComeForward());
        }
        finally
        {
            holder.Dispose();
        }
    }

    [Fact]
    public void A_lock_left_unheld_and_a_stale_endpoint_give_the_next_guard_the_folder_and_it_serves()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        Directory.CreateDirectory(paths.DataDirectory);
        File.WriteAllText(paths.InstanceLockFile, string.Empty);
        var stale = StaleEndpoint();
        try
        {
            WriteInstanceFile(paths, InstanceJson(4242, stale));

            using var guard = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
            Assert.Equal(GuardOutcome.Taken, guard.Outcome);
            Assert.NotEqual(stale, guard.Endpoint);
            using var asker = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
            Assert.Equal(ForwardAnswer.Starting, asker.AskToComeForward());
            if (!OperatingSystem.IsWindows())
            {
                Assert.False(File.Exists(stale), $"The stale socket {stale} is still there.");
            }
        }
        finally
        {
            if (!OperatingSystem.IsWindows())
            {
                File.Delete(stale);
            }
        }
    }

    [Fact]
    public async Task An_ask_that_reads_a_stale_endpoint_reaches_the_holder_once_its_file_is_back()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        using var holder = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        holder.ComeForward = () => ForwardAnswer.Activated;
        var original = File.ReadAllText(paths.InstanceFile);
        var stale = StaleEndpoint();
        try
        {
            WriteInstanceFile(paths, InstanceJson(4242, stale));
            using var asker = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
            Assert.Equal(GuardOutcome.Held, asker.Outcome);

            var ask = Task.Run(asker.AskToComeForward, TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);
            WriteInstanceFile(paths, original);

            Assert.Equal(ForwardAnswer.Activated, await ask);
        }
        finally
        {
            if (!OperatingSystem.IsWindows())
            {
                File.Delete(stale);
            }
        }
    }

    [Fact]
    public void The_endpoint_is_random_holds_nothing_of_the_data_folder_and_admits_the_current_user_only()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        using var first = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        using var second = SingleInstanceGuard.TryTake(new AppPaths(root.Combine("other data folder")), new RecordingLog(), Patient);

        foreach (var endpoint in new[] { first.Endpoint, second.Endpoint })
        {
            Assert.NotNull(endpoint);
            if (OperatingSystem.IsWindows())
            {
                Assert.Matches(new Regex("^VisualCommit-[0-9a-f]{16}$"), endpoint);
            }
            else
            {
                var name = Path.GetFileName(endpoint);
                Assert.Matches(new Regex("^VisualCommit-[0-9a-f]{16}$"), name);
                Assert.True(endpoint == Path.GetTempPath() + name || endpoint == "/tmp/" + name, $"{endpoint} is neither in the temp folder nor in /tmp.");
                Assert.True(Encoding.UTF8.GetByteCount(endpoint) <= 100, $"{endpoint} is over 100 bytes.");
            }

            var below = Path.GetRelativePath(Path.GetTempPath(), paths.DataDirectory);
            foreach (var segment in below.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                Assert.DoesNotContain(segment, endpoint, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.NotEqual(first.Endpoint, second.Endpoint);
        Assert.True(SingleInstanceGuard.ServerPipeOptions.HasFlag(PipeOptions.CurrentUserOnly));
    }

    [Fact]
    public void A_data_folder_under_a_file_is_unavailable_with_a_warning_and_nothing_thrown()
    {
        using var root = new TempDirectory("guard");
        var file = root.Combine("a-file");
        File.WriteAllText(file, "not a folder");
        var paths = new AppPaths(Path.Combine(file, "data"));
        var log = new RecordingLog();

        using var guard = SingleInstanceGuard.TryTake(paths, log, Patient);

        Assert.Equal(GuardOutcome.Unavailable, guard.Outcome);
        Assert.Null(guard.Endpoint);
        var warning = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.StartsWith($"The data folder {paths.DataDirectory} could not be locked against a second VisualCommit (", warning.Message, StringComparison.Ordinal);
        Assert.EndsWith("); this one starts without that guard.", warning.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => guard.AskToComeForward());
    }

    [Fact]
    public void The_locking_off_warning_is_for_macOS_and_Linux_when_dotnet_takes_no_lock()
    {
        const string Folder = "/home/demo/.config/VisualCommit";
        const string Text = "File locking is turned off (DOTNET_SYSTEM_IO_DISABLEFILELOCKING or System.IO.DisableFileLocking): a second VisualCommit on the data folder /home/demo/.config/VisualCommit is not stopped.";

        Assert.Equal(Text, SingleInstanceGuard.LockingOffWarning(Folder, "1", switchOn: false, windows: false));
        Assert.Equal(Text, SingleInstanceGuard.LockingOffWarning(Folder, "TRUE", switchOn: false, windows: false));
        Assert.Equal(Text, SingleInstanceGuard.LockingOffWarning(Folder, null, switchOn: true, windows: false));
        Assert.Null(SingleInstanceGuard.LockingOffWarning(Folder, null, switchOn: false, windows: false));
        Assert.Null(SingleInstanceGuard.LockingOffWarning(Folder, "0", switchOn: false, windows: false));
        Assert.Null(SingleInstanceGuard.LockingOffWarning(@"C:\Users\demo\AppData\Roaming\VisualCommit", "1", switchOn: true, windows: true));
        Assert.Null(SingleInstanceGuard.LockingOffWarning(@"C:\Users\demo\AppData\Roaming\VisualCommit", "TRUE", switchOn: false, windows: true));
    }

    [Fact]
    public async Task A_malformed_line_another_version_and_a_silent_client_get_no_answer_and_the_next_is_served()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        using var holder = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        holder.ComeForward = () => ForwardAnswer.Activated;
        using var asker = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);

        Assert.Empty(await ExchangeAsync(holder.Endpoint!, "this is no JSON\n"));
        Assert.Equal(ForwardAnswer.Activated, asker.AskToComeForward());

        Assert.Empty(await ExchangeAsync(holder.Endpoint!, $"{{\"v\":2,\"request\":\"come-forward\",\"pid\":{Pid},\"args\":[]}}\n"));
        Assert.Equal(ForwardAnswer.Activated, asker.AskToComeForward());

        Assert.Empty(await ExchangeAsync(holder.Endpoint!, $"{{\"v\":1,\"request\":\"open\",\"pid\":{Pid},\"args\":[]}}\n"));
        Assert.Equal(ForwardAnswer.Activated, asker.AskToComeForward());

        // Silent: the holder drops it once its Reply has passed.
        Assert.Empty(await ExchangeAsync(holder.Endpoint!, null));
        Assert.Equal(ForwardAnswer.Activated, asker.AskToComeForward());
    }

    [Fact]
    public async Task A_come_forward_that_throws_leaves_the_asker_unanswered_and_the_holder_serves_the_next()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        var holderLog = new RecordingLog();
        using var holder = SingleInstanceGuard.TryTake(paths, holderLog, Patient);
        holder.ComeForward = () => throw new InvalidOperationException("The window is gone.");
        var askerLog = new RecordingLog();
        using var asker = SingleInstanceGuard.TryTake(paths, askerLog, Impatient);

        Assert.Equal(ForwardAnswer.NotAnswered, asker.AskToComeForward());
        Assert.Contains(Warning($"VisualCommit holds the data folder {paths.DataDirectory} but did not answer (no reply within 0.3 s)."), askerLog.Entries.Select(Line));
        await WaitForLineAsync(holderLog, Warning($"A second VisualCommit (process {Pid}) started on this data folder, and the window did not come forward within 5 s."));

        holder.ComeForward = () => ForwardAnswer.Activated;
        using var next = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        Assert.Equal(ForwardAnswer.Activated, next.AskToComeForward());
    }

    [Fact]
    public async Task A_come_forward_that_blocks_leaves_the_asker_unanswered_and_the_holder_serves_the_next()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        var holderLog = new RecordingLog();
        using var holder = SingleInstanceGuard.TryTake(paths, holderLog, Patient);
        using var gate = new ManualResetEventSlim();
        var cancel = TestContext.Current.CancellationToken;
        holder.ComeForward = () =>
        {
            gate.Wait(cancel);
            return ForwardAnswer.Activated;
        };
        var askerLog = new RecordingLog();
        using var asker = SingleInstanceGuard.TryTake(paths, askerLog, Impatient);
        var warning = Warning($"A second VisualCommit (process {Pid}) started on this data folder, and the window did not come forward within 5 s.");

        try
        {
            Assert.Equal(ForwardAnswer.NotAnswered, asker.AskToComeForward());
            Assert.Contains(Warning($"VisualCommit holds the data folder {paths.DataDirectory} but did not answer (no reply within 0.3 s)."), askerLog.Entries.Select(Line));
            await WaitForLineAsync(holderLog, warning);
        }
        finally
        {
            gate.Set();
        }

        holder.ComeForward = () => ForwardAnswer.Activated;
        using var next = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        Assert.Equal(ForwardAnswer.Activated, next.AskToComeForward());
        Assert.Single(holderLog.Entries, entry => Line(entry) == warning);
    }

    [Fact]
    public void Disposing_twice_is_harmless_and_leaves_the_lock_file_without_instance_json()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        var guard = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        Assert.Equal(GuardOutcome.Taken, guard.Outcome);

        guard.Dispose();
        guard.Dispose();

        Assert.False(File.Exists(paths.InstanceFile));
        Assert.True(File.Exists(paths.InstanceLockFile));
        using var next = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        Assert.Equal(GuardOutcome.Taken, next.Outcome);
    }

    [Fact]
    public void A_held_guard_disposed_after_an_answer_or_none_touches_none_of_the_holders_files()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        using var holder = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        holder.ComeForward = () => ForwardAnswer.Activated;
        var before = File.ReadAllBytes(paths.InstanceFile);

        var answered = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        Assert.Equal(ForwardAnswer.Activated, answered.AskToComeForward());
        answered.Dispose();
        Assert.Equal(before, File.ReadAllBytes(paths.InstanceFile));
        using (var third = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient))
        {
            Assert.Equal(ForwardAnswer.Activated, third.AskToComeForward());
        }

        holder.ComeForward = () => throw new InvalidOperationException("No window yet.");
        var unanswered = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Impatient);
        Assert.Equal(ForwardAnswer.NotAnswered, unanswered.AskToComeForward());
        unanswered.Dispose();
        Assert.Equal(before, File.ReadAllBytes(paths.InstanceFile));
        Assert.True(File.Exists(paths.InstanceLockFile));
        holder.ComeForward = () => ForwardAnswer.Activated;
        using var fourth = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        Assert.Equal(GuardOutcome.Held, fourth.Outcome);
        Assert.Equal(ForwardAnswer.Activated, fourth.AskToComeForward());
    }

    /// <summary>Connect 5 s and Reply 300 ms: an asker that must connect and whose expected result is that no answer comes.</summary>
    private static SingleInstanceOptions Impatient { get; } = new(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(30));

    private static AppPaths DataFolder(TempDirectory root) => new(root.Combine("data folder"));

    private static string Line((LogLevel Level, string Message, Exception? Exception) entry) => $"{entry.Level}: {entry.Message}";

    private static string Info(string message) => $"{LogLevel.Info}: {message}";

    private static string Warning(string message) => $"{LogLevel.Warning}: {message}";

    /// <summary>An endpoint of the guard's form that nobody serves; on macOS and Linux a plain file is at its path, as a crashed run's socket would be.</summary>
    private static string StaleEndpoint()
    {
        var name = "VisualCommit-" + RandomNumberGenerator.GetHexString(16, lowercase: true);
        if (OperatingSystem.IsWindows())
        {
            return name;
        }

        var path = Path.GetTempPath() + name;
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private static string InstanceJson(int pid, string endpoint)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", 1);
            writer.WriteNumber("pid", pid);
            writer.WriteString("endpoint", endpoint);
            writer.WriteString("started", new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Replaces <c>instance.json</c> as the guard does (written beside it, then moved over it), again while a reader holds it for a moment.</summary>
    private static void WriteInstanceFile(AppPaths paths, string text)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                DurableFile.Replace(paths.InstanceFile, text);
                return;
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && clock.Elapsed < WaitLimit)
            {
                Thread.Sleep(10);
            }
        }
    }

    /// <summary>
    /// Connects to <paramref name="endpoint"/> as a second start does, sends <paramref name="line"/>
    /// (nothing when null) and reads until the holder ends the connection: the bytes it answered.
    /// </summary>
    private static async Task<byte[]> ExchangeAsync(string endpoint, string? line)
    {
        var cancel = TestContext.Current.CancellationToken;
        using var client = new NamedPipeClientStream(".", endpoint, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        await client.ConnectAsync((int)WaitLimit.TotalMilliseconds, cancel);
        if (line is not null)
        {
            await client.WriteAsync(Encoding.UTF8.GetBytes(line), cancel);
            await client.FlushAsync(cancel);
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(WaitLimit);
        var answer = new MemoryStream();
        var buffer = new byte[256];
        while (true)
        {
            var read = await client.ReadAsync(buffer, limit.Token);
            if (read == 0)
            {
                return answer.ToArray();
            }

            answer.Write(buffer, 0, read);
        }
    }

    private static async Task WaitForLineAsync(RecordingLog log, string line)
    {
        var clock = Stopwatch.StartNew();
        while (!log.Entries.Select(Line).Contains(line))
        {
            Assert.True(clock.Elapsed < WaitLimit, $"The log never had \"{line}\". It has:\n" + string.Join('\n', log.Entries.Select(Line)));
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }
}
