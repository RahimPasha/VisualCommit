using System.Diagnostics;
using VisualCommit.Core.Git;
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
        var runner = new GitRunner(GitPath);
        if (!OperatingSystem.IsWindows())
        {
            // Ignored elsewhere: the call runs as any other.
            var elsewhere = await runner.RunAsync(new GitCommand("--version") { BelowNormalPriority = true }, TestCancelled);
            Assert.True(elsewhere.Succeeded, elsewhere.StandardError);
            return;
        }

        // No other test runs beside this one (its collection), so no other git child is below normal.
        Assert.Empty(GitChildrenAt(ProcessPriorityClass.BelowNormal));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestCancelled);
        var call = runner.RunAsync(new GitCommand("-c", "alias.script=!sleep 120", "script") { BelowNormalPriority = true }, cancel.Token);
        try
        {
            var clock = Stopwatch.StartNew();
            List<int> lowered;
            while ((lowered = GitChildrenAt(ProcessPriorityClass.BelowNormal)).Count == 0)
            {
                Assert.False(call.IsCompleted, "The call ended before its git was seen.");
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), "No git child of this process runs below normal.");
                await Task.Delay(TimeSpan.FromMilliseconds(20), TestCancelled);
            }

            // Whatever git has started by now (the real git behind Git for Windows' launcher, its
            // shell, sleep) runs below normal too, however the start and the lowering raced.
            var git = Assert.Single(lowered);
            while (DescendantsOf(git).Where(process => process.Priority != ProcessPriorityClass.BelowNormal).ToList() is { Count: > 0 } normal)
            {
                Assert.True(
                    clock.Elapsed < TimeSpan.FromSeconds(60),
                    "Processes git started run at another priority: " + string.Join(", ", normal.Select(process => $"{process.Name} {process.Priority}")));
                await Task.Delay(TimeSpan.FromMilliseconds(20), TestCancelled);
            }
        }
        finally
        {
            await cancel.CancelAsync();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    [Fact]
    public async Task A_tree_that_started_before_its_priority_was_set_is_lowered_whole()
    {
        // The race 8.10's lowering must survive: Git for Windows' cmd\git.exe starts the real git
        // at once, which may come before the runner sets the class, and the shells it starts would
        // then run at normal priority. Here the whole tree runs, at normal priority, before it is
        // lowered: every process in it ends below normal.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var start = new ProcessStartInfo(GitPath)
        {
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

            GitRunner.LowerTreeBelowNormal(git);

            Assert.Equal(ProcessPriorityClass.BelowNormal, git.PriorityClass);
            var after = DescendantsOf(git.Id);
            Assert.Equal(before.Select(process => process.Id).Order(), after.Select(process => process.Id).Order());
            Assert.All(after, process => Assert.Equal(ProcessPriorityClass.BelowNormal, process.Priority));
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
