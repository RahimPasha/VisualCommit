using System.Diagnostics;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The runner's <see cref="GitCommand.BelowNormalPriority"/> (docs/phase-6-design.md 8.10), in a
/// collection of its own that runs alone: the test tells its git process from every other by
/// looking at this process's git children, so no other test may start git meanwhile.
/// </summary>
[Collection(GitRunnerPriorityTests.Alone)]
public class GitRunnerPriorityTests
{
    /// <summary>The collection that runs with no other test of the assembly beside it.</summary>
    public const string Alone = "Git runner priority, alone";

    private static readonly string GitPath =
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found.");

    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_below_normal_priority_call_starts_git_below_normal_on_windows()
    {
        // docs/phase-6-design.md 8.10: the runner lowers the process it started right after the
        // start, with what that has started by then; Windows gives the processes they start from
        // then on the same class. The test looks at this process's own git children (Toolhelp's
        // snapshot names each process's parent) while the call waits, and needs no work of the
        // lowered processes: under a full test run a chain of below-normal shells starves behind
        // the normal-priority work, which is what the class is for, so a test that waited for
        // their output hung. It waits only for the runner's own lowering of git's descendants.
        // The calls run in a folder of their own, outside the project's repository.
        using var folder = new TempDirectory("priority");
        var runner = new GitRunner(GitPath);
        GitCommand Sleep(bool belowNormal) => new("-c", "alias.script=!sleep 120", "script") { WorkingDirectory = folder.Path, BelowNormalPriority = belowNormal };
        if (!OperatingSystem.IsWindows())
        {
            // Ignored elsewhere: the call runs as any other.
            var elsewhere = await runner.RunAsync(new GitCommand("--version") { WorkingDirectory = folder.Path, BelowNormalPriority = true }, TestCancelled);
            Assert.True(elsewhere.Succeeded, elsewhere.StandardError);
            return;
        }

        // This process at normal priority for the test, whatever started it: a host that runs below
        // normal, as CI's runner runs it, gives every git it starts its class, and the test would
        // pass without the runner lowering anything (run 39190992859).
        using var self = Process.GetCurrentProcess();
        var priorityBefore = self.PriorityClass;
        self.PriorityClass = ProcessPriorityClass.Normal;
        try
        {
            // No other test runs beside this one (its collection), so no other git child is below normal.
            Assert.Empty(GitChildrenAt(ProcessPriorityClass.BelowNormal));

            // A call without the flag starts git at normal priority: what the lowering then changes.
            using (var stopNormal = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled))
            {
                var held = runner.RunAsync(Sleep(belowNormal: false), stopNormal.Token);
                try
                {
                    await WaitForGitChildAsync(held, ProcessPriorityClass.Normal, Stopwatch.StartNew());
                    Assert.Empty(GitChildrenAt(ProcessPriorityClass.BelowNormal));
                }
                finally
                {
                    await stopNormal.CancelAsync();
                }

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => held);
            }

            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);
            var call = runner.RunAsync(Sleep(belowNormal: true), cancel.Token);
            try
            {
                var clock = Stopwatch.StartNew();
                var git = await WaitForGitChildAsync(call, ProcessPriorityClass.BelowNormal, clock);

                // Whatever git has started by now (the real git behind Git for Windows' launcher, its
                // shell, sleep) runs below normal too, however the start and the lowering raced.
                while (DescendantsOf(git).Where(process => process.Priority != ProcessPriorityClass.BelowNormal).ToList() is { Count: > 0 } others)
                {
                    Assert.True(
                        clock.Elapsed < TimeSpan.FromSeconds(60),
                        "Processes git started run at another priority: " + string.Join(", ", others.Select(process => $"{process.Name} {process.Priority}")));
                    await Task.Delay(TimeSpan.FromMilliseconds(20), TestCancelled);
                }
            }
            finally
            {
                await cancel.CancelAsync();
            }

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        }
        finally
        {
            self.PriorityClass = priorityBefore;
        }
    }

    /// <summary>The one git child of this process at <paramref name="priority"/> while <paramref name="call"/> runs, waited for up to 60 s (Windows).</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static async Task<int> WaitForGitChildAsync(Task call, ProcessPriorityClass priority, Stopwatch clock)
    {
        List<int> found;
        while ((found = GitChildrenAt(priority)).Count == 0)
        {
            Assert.False(call.IsCompleted, "The call ended before its git was seen.");
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"No git child of this process runs at {priority}.");
            await Task.Delay(TimeSpan.FromMilliseconds(20), TestCancelled);
        }

        return Assert.Single(found);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_process_that_joins_the_tree_after_the_lowering_at_normal_priority_runs_below_normal_only_in_the_job(bool inTheCallsJob)
    {
        // CI run 42216604663 failed the test above with git's console host at normal priority.
        // Windows can take a new process's class from its creator well before the process shows in
        // a snapshot: git's launcher, which creates its console host as it begins, took the class
        // before the runner lowered it, and the console host showed only after the runner's last
        // snapshot (GitRunner.LowerTreeBelowNormal). Its class was chosen as normal, it joined git's
        // tree after the lowering, and nothing lowered it. A below-normal call's git is now in a job
        // whose limit Windows applies to each process as it joins (WindowsJob.HoldsBelowNormal),
        // however its class was chosen.
        //
        // The test makes such a process on purpose, without a race and without any work of git's
        // tree: after the runner's steps (the job, then the lowering), this process creates one with
        // normal priority named for it and git as its parent (PROC_THREAD_ATTRIBUTE_PARENT_PROCESS:
        // Windows then gives it git's job, as for what git starts itself), suspended, so it does
        // nothing. Without the job, as the runner lowered before this fix, it runs at normal
        // priority, as the console host did; in the job, below normal.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var folder = new TempDirectory("priority-late");
        var start = new ProcessStartInfo(GitPath)
        {
            WorkingDirectory = folder.Path,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "-c", "alias.script=!sleep 120", "script" })
        {
            start.ArgumentList.Add(argument);
        }

        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
        Process? late = null;
        try
        {
            using var job = inTheCallsJob ? WindowsJob.TryAttach(git, belowNormal: true) : null;
            Assert.Equal(inTheCallsJob, job is { HoldsBelowNormal: true });
            GitRunner.LowerTreeBelowNormal(git, job);
            Assert.Equal(ProcessPriorityClass.BelowNormal, git.PriorityClass);

            late = LateChild.StartSuspended(git, Path.Combine(Environment.SystemDirectory, "PING.EXE"));
            Assert.Contains(DescendantsOf(git.Id), process => process.Id == late.Id);
            if (job is null)
            {
                // The class alone: the process runs at the class chosen for it.
                Assert.Equal(ProcessPriorityClass.Normal, late.PriorityClass);
            }
            else
            {
                Assert.True(job.Contains(late), "The process git's tree gained is not in git's job.");
                Assert.Equal(ProcessPriorityClass.BelowNormal, late.PriorityClass);
            }
        }
        finally
        {
            late?.Kill();
            late?.Dispose();
            git.Kill(entireProcessTree: true);
            await git.WaitForExitAsync(TestCancelled);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_tree_that_started_before_its_priority_was_set_is_lowered_whole(bool inTheCallsJob)
    {
        // The race 8.10's lowering must survive: Git for Windows' cmd\git.exe starts the real git
        // at once, which may come before the runner sets the class, and the shells it starts would
        // then run at normal priority. Here the whole tree runs, at normal priority, before it is
        // lowered: every process in it ends below normal. In the job a below-normal call gets, which
        // git joins only after its start, the lowering also adds to the job what git started before,
        // so that what those start later joins it too.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // In a folder of its own, outside the project's repository.
        using var folder = new TempDirectory("priority-tree");
        var start = new ProcessStartInfo(GitPath)
        {
            WorkingDirectory = folder.Path,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "-c", "alias.script=!sleep 120", "script" })
        {
            start.ArgumentList.Add(argument);
        }

        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
        try
        {
            // Normal-priority work: the tree reaches sleep within seconds, even under a full run.
            var clock = Stopwatch.StartNew();
            while (!DescendantsOf(git.Id).Any(process => string.Equals(process.Name, "sleep.exe", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.False(git.HasExited, "git ended before it started sleep.");
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), "git started no sleep within 60 s.");
                await Task.Delay(TimeSpan.FromMilliseconds(20), TestCancelled);
            }

            // The tree is put at normal priority first, whatever the machine started it at: Windows
            // gives a child its parent's class when the parent runs below normal, as CI's runner runs
            // the test host (run 39190992859 found the whole tree below normal already).
            foreach (var id in DescendantsOf(git.Id).Select(process => process.Id).Prepend(git.Id))
            {
                try
                {
                    using var process = Process.GetProcessById(id);
                    process.PriorityClass = ProcessPriorityClass.Normal;
                }
                catch (ArgumentException)
                {
                    // Ended meanwhile: the comparison of the trees below tells.
                }
            }

            var before = DescendantsOf(git.Id);
            Assert.All(before, process => Assert.Equal(ProcessPriorityClass.Normal, process.Priority));

            using var job = inTheCallsJob ? WindowsJob.TryAttach(git, belowNormal: true) : null;
            if (inTheCallsJob)
            {
                Assert.NotNull(job);
                Assert.True(job.HoldsBelowNormal);
                foreach (var process in before)
                {
                    using var found = Process.GetProcessById(process.Id);
                    Assert.False(job.Contains(found), $"{process.Name} is in the job before the lowering.");
                }
            }

            GitRunner.LowerTreeBelowNormal(git, job);

            Assert.Equal(ProcessPriorityClass.BelowNormal, git.PriorityClass);
            var after = DescendantsOf(git.Id);
            Assert.Equal(before.Select(process => process.Id).Order(), after.Select(process => process.Id).Order());
            Assert.All(after, process => Assert.Equal(ProcessPriorityClass.BelowNormal, process.Priority));
            if (job is not null)
            {
                Assert.True(job.Contains(git));
                foreach (var process in after)
                {
                    using var found = Process.GetProcessById(process.Id);
                    Assert.True(job.Contains(found), $"{process.Name} is not in the job.");
                }
            }
        }
        finally
        {
            git.Kill(entireProcessTree: true);
            await git.WaitForExitAsync(TestCancelled);
        }
    }

    /// <summary>
    /// The processes <paramref name="root"/> has started, directly or not, with their names and
    /// priority classes (Windows): by Toolhelp's parents, leaving out a child that started before
    /// its parent (whose parent's id was reused) and one that ended meanwhile.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static List<(int Id, string Name, ProcessPriorityClass Priority)> DescendantsOf(int root)
    {
        var snapshot = ProcessSnapshot.Take();
        var found = new List<(int Id, string Name, ProcessPriorityClass Priority)>();
        DateTime rootStart;
        try
        {
            using var process = Process.GetProcessById(root);
            rootStart = process.StartTime;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return found;
        }

        var parents = new Queue<(int Id, DateTime Start)>([(root, rootStart)]);
        var visited = new HashSet<int> { root };
        while (parents.TryDequeue(out var parent))
        {
            foreach (var (id, _, name) in snapshot.Where(entry => entry.Parent == parent.Id && visited.Add(entry.Id)))
            {
                try
                {
                    using var child = Process.GetProcessById(id);
                    if (child.StartTime < parent.Start)
                    {
                        continue;
                    }

                    found.Add((id, name, child.PriorityClass));
                    parents.Enqueue((id, child.StartTime));
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Ended meanwhile.
                }
            }
        }

        return found;
    }

    /// <summary>The ids of this process's child processes named <c>git.exe</c> whose priority class is <paramref name="priority"/> (Windows).</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static List<int> GitChildrenAt(ProcessPriorityClass priority)
    {
        var children = new List<int>();
        foreach (var (id, parent, name) in ProcessSnapshot.Take())
        {
            if (parent != Environment.ProcessId || !string.Equals(name, "git.exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(id);
                if (process.PriorityClass == priority)
                {
                    children.Add(id);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Ended meanwhile.
            }
        }

        return children;
    }

    /// <summary>
    /// Starts a process with a given parent and normal priority named for it, suspended, so that it
    /// does nothing (Windows; PROC_THREAD_ATTRIBUTE_PARENT_PROCESS: the new process takes its job,
    /// among other things, from that parent).
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static class LateChild
    {
        private const uint CreateSuspended = 0x00000004;
        private const uint NormalPriorityClass = 0x00000020;
        private const uint ExtendedStartupInfoPresent = 0x00080000;
        private const uint CreateNoWindow = 0x08000000;
        private static readonly IntPtr ParentProcessAttribute = new(0x00020000);

        public static Process StartSuspended(Process parent, string executable)
        {
            var size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            var attributes = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            var parentHandle = System.Runtime.InteropServices.Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size))
                {
                    throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                }

                try
                {
                    System.Runtime.InteropServices.Marshal.WriteIntPtr(parentHandle, parent.SafeHandle.DangerousGetHandle());
                    if (!UpdateProcThreadAttribute(attributes, 0, ParentProcessAttribute, parentHandle, IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                    {
                        throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                    }

                    var startup = new StartupInfoEx { AttributeList = attributes };
                    startup.StartupInfo.Size = System.Runtime.InteropServices.Marshal.SizeOf<StartupInfoEx>();
                    var commandLine = new System.Text.StringBuilder($"\"{executable}\" -n 120 127.0.0.1");
                    if (!CreateProcessW(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, CreateSuspended | NormalPriorityClass | ExtendedStartupInfoPresent | CreateNoWindow, IntPtr.Zero, null, ref startup, out var created))
                    {
                        throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                    }

                    try
                    {
                        return Process.GetProcessById(created.ProcessId);
                    }
                    finally
                    {
                        CloseHandle(created.Thread);
                        CloseHandle(created.Process);
                    }
                }
                finally
                {
                    DeleteProcThreadAttributeList(attributes);
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(parentHandle);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(attributes);
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private struct StartupInfo
        {
            public int Size;
            public string? Reserved;
            public string? Desktop;
            public string? Title;
            public int X;
            public int Y;
            public int XSize;
            public int YSize;
            public int XCountChars;
            public int YCountChars;
            public int FillAttribute;
            public int Flags;
            public short ShowWindow;
            public short Reserved2Size;
            public IntPtr Reserved2;
            public IntPtr StdInput;
            public IntPtr StdOutput;
            public IntPtr StdError;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct StartupInfoEx
        {
            public StartupInfo StartupInfo;
            public IntPtr AttributeList;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public int ProcessId;
            public int ThreadId;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returnSize);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern void DeleteProcThreadAttributeList(IntPtr list);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool CreateProcessW(string? application, System.Text.StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool inheritHandles, uint flags, IntPtr environment, string? directory, ref StartupInfoEx startup, out ProcessInformation information);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }

    /// <summary>Every process of the machine with its parent's id and its file name, from Toolhelp's snapshot (Windows).</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static class ProcessSnapshot
    {
        private const uint SnapProcess = 0x00000002;

        public static List<(int Id, int Parent, string Name)> Take()
        {
            var processes = new List<(int Id, int Parent, string Name)>();
            var snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
            if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            {
                throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            }

            try
            {
                var entry = new ProcessEntry { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<ProcessEntry>() };
                for (var more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
                {
                    processes.Add(((int)entry.ProcessId, (int)entry.ParentProcessId, entry.ExeFile));
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }

            return processes;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private struct ProcessEntry
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;

            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExeFile;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}

/// <summary>Runs <see cref="GitRunnerPriorityTests"/> with no other test beside it.</summary>
[CollectionDefinition(GitRunnerPriorityTests.Alone, DisableParallelization = true)]
public sealed class GitRunnerPriorityCollection
{
}
