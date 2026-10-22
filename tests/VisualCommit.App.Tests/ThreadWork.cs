using System.Runtime.InteropServices;

namespace VisualCommit.App.Tests;

/// <summary>
/// The work the calling thread has done so far, as the processor counts it: its cycles on Windows,
/// its processor time in nanoseconds elsewhere. Unlike a stopwatch it does not grow while the
/// thread waits for a processor that other test projects keep busy, so the work of two runs on one
/// thread can be compared under load. The two units differ, so only values read on one machine are
/// compared, as ratios.
/// </summary>
internal static class ThreadWork
{
    /// <summary><c>GetCurrentThread()</c>'s pseudo handle: the calling thread.</summary>
    private static readonly IntPtr CurrentThread = new(-2);

    /// <summary><c>CLOCK_THREAD_CPUTIME_ID</c>: 3 on Linux, 16 on macOS.</summary>
    private static readonly int ThreadClock = OperatingSystem.IsMacOS() ? 16 : 3;

    public static ulong Now()
    {
        if (OperatingSystem.IsWindows())
        {
            if (!QueryThreadCycleTime(CurrentThread, out var cycles))
                throw new InvalidOperationException($"QueryThreadCycleTime failed: {Marshal.GetLastPInvokeError()}.");
            return cycles;
        }

        if (ClockGetTime(ThreadClock, out var time) != 0)
            throw new InvalidOperationException($"clock_gettime failed: {Marshal.GetLastPInvokeError()}.");
        return (ulong)time.Seconds * 1_000_000_000 + (ulong)time.Nanoseconds;
    }

    /// <summary><c>struct timespec</c> of a 64-bit Linux or macOS.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct TimeSpec
    {
        public long Seconds;
        public long Nanoseconds;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryThreadCycleTime(IntPtr thread, out ulong cycles);

    [DllImport("libc", EntryPoint = "clock_gettime", SetLastError = true)]
    private static extern int ClockGetTime(int clock, out TimeSpec time);
}
