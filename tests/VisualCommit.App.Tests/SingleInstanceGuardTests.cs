using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
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

    /// <summary>
    /// A process id no process has (no Windows id is odd, Linux's and macOS's stay far below it), and
    /// not -1, which <c>AllowSetForegroundWindow</c> takes as "any": since holder and asker share this
    /// test's process, a line that names it shows whose id it took.
    /// </summary>
    private const int NoProcess = int.MaxValue;

    /// <summary>The caller that asks as <c>Program.Main</c> does (<see cref="SingleInstanceGuard.ResolveHeld"/>).</summary>
    private const string SecondStart = "a second start";

    /// <summary>The caller that asks as the notice's "Try again" does (<see cref="InstanceNoticeViewModel.RetryOn"/>).</summary>
    private const string TryAgain = "Try again";

    /// <summary>Every bit that lets a user other than the owner read, write or enter.</summary>
    private const UnixFileMode GroupOrOther =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

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

        // The holder's line names the pid of the request, and the asker's the pid in instance.json.
        holder.ComeForward = () => ForwardAnswer.Activated;
        var answer = await ExchangeLineAsync(holder.Endpoint!, $"{{\"v\":1,\"request\":\"come-forward\",\"pid\":{NoProcess},\"args\":[]}}\n");
        Assert.Equal("{\"v\":1,\"answer\":\"activated\"}\n", answer);
        await WaitForLineAsync(holderLog, Info($"A second VisualCommit (process {NoProcess}) started on this data folder: answered activated."));

        WriteInstanceFile(paths, InstanceJson(NoProcess, holder.Endpoint!));
        Assert.Equal(ForwardAnswer.Activated, asker.AskToComeForward());
        Assert.Contains(Info($"VisualCommit already runs on the data folder {paths.DataDirectory} (process {NoProcess}); it was asked to come to the front, and this start ends."), askerLog.Entries.Select(Line));
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

            // The line names the pid in instance.json, which holder and asker would share here.
            WriteInstanceFile(paths, InstanceJson(NoProcess, holder.Endpoint!));
            Assert.Equal(ForwardAnswer.Closing, second.AskToComeForward());
            Assert.Contains(Info($"VisualCommit on the data folder {paths.DataDirectory} (process {NoProcess}) is closing; this start waits up to 30 s for it."), log.Entries.Select(Line));

            var disposal = Task.Run(
                async () =>
                {
                    await Task.Delay(300, TestContext.Current.CancellationToken);
                    holder.Dispose();
                },
                TestContext.Current.CancellationToken);
            Assert.Equal(HeldResolution.Taken, second.WaitToTake());
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

    [Theory]
    [InlineData(SecondStart)]
    [InlineData(TryAgain)]
    public async Task Two_starts_that_wait_for_a_closing_holder_end_one_taken_and_the_other_handed_over_to_it(string caller)
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        var holder = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        try
        {
            holder.ComeForward = () => ForwardAnswer.Closing;
            RecordingLog[] logs = [new(), new()];
            using var first = SingleInstanceGuard.TryTake(paths, logs[0], Patient);
            using var second = SingleInstanceGuard.TryTake(paths, logs[1], Patient);
            SingleInstanceGuard[] guards = [first, second];
            Assert.All(guards, guard => Assert.Equal(GuardOutcome.Held, guard.Outcome));

            // Each on a thread of its own, which a busy pool cannot hold up. The holder lets the
            // folder go only once both have its answer, so that neither ask meets its end.
            var waits = guards.Select(guard => Task.Factory.StartNew(() => Resolve(guard, caller), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
            var closing = Info($"VisualCommit on the data folder {paths.DataDirectory} (process {Pid}) is closing; this start waits up to 30 s for it.");
            await WaitForLineAsync(logs[0], closing);
            await WaitForLineAsync(logs[1], closing);
            holder.Dispose();
            var results = await Task.WhenAll(waits);

            // One takes the folder; the other asks it, as it would have asked the closing one.
            Assert.Equal([HeldResolution.Taken, HeldResolution.HandedOver], results.Order().ToArray());
            var winner = Array.IndexOf(results, HeldResolution.Taken);
            var loser = 1 - winner;
            Assert.Equal(GuardOutcome.Taken, guards[winner].Outcome);
            Assert.Equal(GuardOutcome.Held, guards[loser].Outcome);
            Assert.Contains(Info("The other VisualCommit closed; this start goes on."), logs[winner].Entries.Select(Line));
            var lost = logs[loser].Entries.Select(Line).ToList();
            Assert.Contains(Info("Another VisualCommit took the data folder while this start waited; it is asked instead."), lost);
            Assert.Contains(Info($"VisualCommit is starting on the data folder {paths.DataDirectory} (process {Pid}); this start ends."), lost);
            Assert.DoesNotContain(lost, line => line.Contains("did not close within", StringComparison.Ordinal));
            using var file = JsonDocument.Parse(File.ReadAllBytes(paths.InstanceFile));
            Assert.Equal(guards[winner].Endpoint, file.RootElement.GetProperty("endpoint").GetString());
        }
        finally
        {
            holder.Dispose();
        }
    }

    [Theory]
    [InlineData(SecondStart)]
    [InlineData(TryAgain)]
    public async Task A_holder_that_ends_while_it_is_asked_leaves_the_folder_to_the_asker(string caller)
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        Directory.CreateDirectory(paths.DataDirectory);
        var endpoint = FreeEndpoint();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(WaitLimit);

        // The test is the holder: it holds the lock and serves the endpoint that instance.json names.
        var held = new FileStream(paths.InstanceLockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var server = new NamedPipeServerStream(endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, SingleInstanceGuard.ServerPipeOptions);
        try
        {
            WriteInstanceFile(paths, InstanceJson(NoProcess, endpoint));
            var log = new RecordingLog();
            using var asker = SingleInstanceGuard.TryTake(paths, log, Patient);
            Assert.Equal(GuardOutcome.Held, asker.Outcome);

            // On a thread of its own, which a busy pool cannot hold up. The holder reads the request,
            // then ends as a closed, crashed or ended VisualCommit does: the lock goes, then the pipe.
            var resolution = Task.Factory.StartNew(() => Resolve(asker, caller), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            await server.WaitForConnectionAsync(limit.Token);
            Assert.Contains("\"request\":\"come-forward\"", await ReadRequestAsync(server, limit.Token), StringComparison.Ordinal);
            held.Dispose();
            server.Dispose();

            Assert.Equal(HeldResolution.Taken, await resolution);
            Assert.Equal(GuardOutcome.Taken, asker.Outcome);
            Assert.NotNull(asker.Endpoint);
            Assert.Contains(Warning($"VisualCommit holds the data folder {paths.DataDirectory} but did not answer (no reply within 5 s)."), log.Entries.Select(Line));
            Assert.Contains(Info($"The data folder {paths.DataDirectory} is free now; this start goes on."), log.Entries.Select(Line));
            using var file = JsonDocument.Parse(File.ReadAllBytes(paths.InstanceFile));
            Assert.Equal(asker.Endpoint, file.RootElement.GetProperty("endpoint").GetString());
        }
        finally
        {
            server.Dispose();
            held.Dispose();
            if (!OperatingSystem.IsWindows())
            {
                File.Delete(endpoint);
            }
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
            WriteInstanceFile(paths, InstanceJson(NoProcess, stale));

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
    public void A_folder_copied_while_its_instance_runs_leaves_that_instances_endpoint_to_it()
    {
        using var root = new TempDirectory("guard");
        var original = DataFolder(root);
        var copy = new AppPaths(root.Combine("copied data folder"));
        var runningLog = new RecordingLog();
        using var running = SingleInstanceGuard.TryTake(original, runningLog, Patient);
        Assert.Equal(GuardOutcome.Taken, running.Outcome);

        // The copy's lock is free, and its instance.json names the running instance's live endpoint.
        Directory.CreateDirectory(copy.DataDirectory);
        File.Copy(original.InstanceFile, copy.InstanceFile);
        using (var onCopy = SingleInstanceGuard.TryTake(copy, new RecordingLog(), Patient))
        {
            Assert.Equal(GuardOutcome.Taken, onCopy.Outcome);
            Assert.NotEqual(running.Endpoint, onCopy.Endpoint);
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.Exists(running.Endpoint), $"The running instance's socket {running.Endpoint} was deleted by a start on a copy of its data folder.");
        }

        using var asker = SingleInstanceGuard.TryTake(original, new RecordingLog(), Patient);
        Assert.Equal(GuardOutcome.Held, asker.Outcome);
        Assert.Equal(ForwardAnswer.Starting, asker.AskToComeForward());
        Assert.DoesNotContain(runningLog.Entries, entry => entry.Level == LogLevel.Warning);
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
            // A pid no process has: on Windows the asker lets the pid it reads take the foreground.
            WriteInstanceFile(paths, InstanceJson(NoProcess, stale));
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

        // Random per run, not per folder: the same folder taken again gets another endpoint.
        var firstRun = first.Endpoint;
        first.Dispose();
        using var nextRun = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);
        Assert.Equal(GuardOutcome.Taken, nextRun.Outcome);
        Assert.NotNull(nextRun.Endpoint);
        Assert.NotEqual(firstRun, nextRun.Endpoint);
    }

    [Fact]
    public void A_stale_instance_json_naming_a_file_in_another_folder_or_of_another_form_leaves_that_file()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        Directory.CreateDirectory(paths.DataDirectory);
        var hex = RandomNumberGenerator.GetHexString(16, lowercase: true);
        string[] files =
        [
            // The guard's name, in a folder that is neither the temp folder nor /tmp.
            root.Combine("VisualCommit-" + hex),

            // The temp folder, with names of another form: 15 hex digits, and a suffix.
            Path.GetTempPath() + "VisualCommit-" + hex[..15],
            Path.GetTempPath() + "VisualCommit-" + hex + "-old",
        ];
        try
        {
            foreach (var file in files)
            {
                File.WriteAllText(file, "not a socket of the guard's");
                WriteInstanceFile(paths, InstanceJson(NoProcess, file));

                using (var guard = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient))
                {
                    Assert.Equal(GuardOutcome.Taken, guard.Outcome);
                }

                Assert.True(File.Exists(file), $"{file} was deleted, though it is no socket path of the guard's form.");
            }
        }
        finally
        {
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task The_holder_writes_instance_json_also_while_a_second_start_has_it_open_for_a_moment()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        Directory.CreateDirectory(paths.DataDirectory);
        WriteInstanceFile(paths, InstanceJson(NoProcess, "VisualCommit-" + RandomNumberGenerator.GetHexString(16, lowercase: true)));
        var log = new RecordingLog();

        // Opened as a second start reads it, and let go 300 ms later by a thread of its own, which
        // a busy thread pool cannot hold up.
        using var reader = new FileStream(paths.InstanceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var release = Task.Factory.StartNew(
            () =>
            {
                Thread.Sleep(300);
                reader.Dispose();
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        using var guard = SingleInstanceGuard.TryTake(paths, log, Patient);
        await release;

        Assert.Equal(GuardOutcome.Taken, guard.Outcome);
        Assert.DoesNotContain(log.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(Info($"This VisualCommit holds the data folder {paths.DataDirectory}: a second start on it is sent here."), log.Entries.Select(Line));
        using var file = JsonDocument.Parse(File.ReadAllBytes(paths.InstanceFile));
        Assert.Equal(Pid, file.RootElement.GetProperty("pid").GetInt32());
        Assert.Equal(guard.Endpoint, file.RootElement.GetProperty("endpoint").GetString());
    }

    [Fact]
    public void An_instance_json_naming_an_endpoint_no_pipe_can_have_is_not_answered_and_nothing_is_thrown()
    {
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        Directory.CreateDirectory(paths.DataDirectory);
        using var held = new FileStream(paths.InstanceLockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        // A name the pipe client refuses as it is made: "anonymous" is reserved.
        WriteInstanceFile(paths, InstanceJson(NoProcess, "anonymous"));
        var options = new SingleInstanceOptions(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(30));
        var log = new RecordingLog();
        using var asker = SingleInstanceGuard.TryTake(paths, log, options);
        Assert.Equal(GuardOutcome.Held, asker.Outcome);

        Assert.Equal(ForwardAnswer.NotAnswered, asker.AskToComeForward());
        Assert.Contains(Warning($"VisualCommit holds the data folder {paths.DataDirectory} but did not answer (its endpoint refused the connection)."), log.Entries.Select(Line));
    }

    [Fact]
    public void On_macOS_and_Linux_the_lock_file_and_the_folders_the_guard_makes_are_the_users_alone()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no Unix file modes; CI's macOS and Linux jobs run this.");
        using var root = new TempDirectory("guard");
        var parent = root.Combine("parent");
        var paths = new AppPaths(Path.Combine(parent, "data folder"));

        using var guard = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);

        // Another user who could open the lock file could hold its lock, and every start of the
        // owner's would then find the folder held. The umask can only take bits away.
        Assert.Equal(GuardOutcome.Taken, guard.Outcome);
        Assert.Equal(UnixFileMode.None, UnixMode(paths.InstanceLockFile) & GroupOrOther);
        foreach (var folder in new[] { parent, paths.DataDirectory })
        {
            Assert.Equal(UnixFileMode.None, UnixMode(folder) & GroupOrOther);
        }
    }

    [Fact]
    public void On_macOS_and_Linux_a_lock_file_and_a_data_folder_that_exist_keep_their_modes()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no Unix file modes; CI's macOS and Linux jobs run this.");
        using var root = new TempDirectory("guard");
        var paths = DataFolder(root);
        Directory.CreateDirectory(paths.DataDirectory);
        File.WriteAllText(paths.InstanceLockFile, string.Empty);
        const UnixFileMode Readable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        const UnixFileMode Listable = Readable | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        SetUnixMode(paths.DataDirectory, Listable);
        SetUnixMode(paths.InstanceLockFile, Readable);

        using var guard = SingleInstanceGuard.TryTake(paths, new RecordingLog(), Patient);

        // The guard narrows only what it makes itself.
        Assert.Equal(GuardOutcome.Taken, guard.Outcome);
        Assert.Equal(Listable, UnixMode(paths.DataDirectory));
        Assert.Equal(Readable, UnixMode(paths.InstanceLockFile));
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

    /// <summary>
    /// A held guard's request as <paramref name="caller"/> makes it: <see cref="SecondStart"/>, as
    /// <c>Program.Main</c> does, or <see cref="TryAgain"/>, the notice's, which first tries the lock;
    /// the notice's result told as the guard's.
    /// </summary>
    private static HeldResolution Resolve(SingleInstanceGuard guard, string caller) => caller switch
    {
        SecondStart => guard.ResolveHeld(),
        TryAgain => InstanceNoticeViewModel.RetryOn(guard) switch
        {
            NoticeRetry.Free => HeldResolution.Taken,
            NoticeRetry.HandedOver => HeldResolution.HandedOver,
            _ => HeldResolution.NotAnswered,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(caller), caller, "No such caller."),
    };

    private static UnixFileMode UnixMode(string path) =>
        OperatingSystem.IsWindows() ? throw new PlatformNotSupportedException("Windows has no Unix file modes.") : File.GetUnixFileMode(path);

    private static void SetUnixMode(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows has no Unix file modes.");
        }

        File.SetUnixFileMode(path, mode);
    }

    /// <summary>An endpoint of the guard's form with nothing at it.</summary>
    private static string FreeEndpoint()
    {
        var name = "VisualCommit-" + RandomNumberGenerator.GetHexString(16, lowercase: true);
        return OperatingSystem.IsWindows() ? name : Path.GetTempPath() + name;
    }

    /// <summary>What a second start sent, up to its <c>\n</c>, as a holder reads it.</summary>
    private static async Task<string> ReadRequestAsync(Stream stream, CancellationToken cancel)
    {
        var request = new MemoryStream();
        var buffer = new byte[256];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancel);
            request.Write(buffer, 0, read);
            if (read == 0 || Array.IndexOf(buffer, (byte)'\n', 0, read) >= 0)
            {
                return Encoding.UTF8.GetString(request.ToArray());
            }
        }
    }

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

    /// <summary>
    /// Connects to <paramref name="endpoint"/> as a second start does, sends <paramref name="line"/>
    /// and reads the answer up to its <c>\n</c>, then hangs up, as a second start does, so that the
    /// holder need not wait out its reply's time: the answer with its <c>\n</c>, or what came before
    /// the holder ended the connection.
    /// </summary>
    private static async Task<string> ExchangeLineAsync(string endpoint, string line)
    {
        var cancel = TestContext.Current.CancellationToken;
        using var client = new NamedPipeClientStream(".", endpoint, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        await client.ConnectAsync((int)WaitLimit.TotalMilliseconds, cancel);
        await client.WriteAsync(Encoding.UTF8.GetBytes(line), cancel);
        await client.FlushAsync(cancel);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(WaitLimit);
        var answer = new MemoryStream();
        var buffer = new byte[256];
        while (true)
        {
            var read = await client.ReadAsync(buffer, limit.Token);
            answer.Write(buffer, 0, read);
            if (read == 0 || Array.IndexOf(buffer, (byte)'\n', 0, read) >= 0)
            {
                return Encoding.UTF8.GetString(answer.ToArray());
            }
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
