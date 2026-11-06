using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace VisualCommit.Git;

/// <summary>
/// A Windows job object holding one git process and everything it starts, so that all of it can
/// be stopped at once. <see cref="Process.Kill(bool)"/> finds child processes by their parent id,
/// and that misses the children of git's bundled shell (hooks, shell aliases, ssh), which
/// re-parent themselves. A job keeps track of them regardless.
/// <para>
/// The job has no "kill on close" limit: closing it after a call that ran to the end leaves
/// helpers that are meant to live on, such as git's file-system monitor, alone.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsJob : IDisposable
{
    /// <summary><c>JOBOBJECTINFOCLASS.JobObjectBasicLimitInformation</c>.</summary>
    private const int BasicLimitInformation = 2;

    /// <summary><c>JOB_OBJECT_LIMIT_PRIORITY_CLASS</c>.</summary>
    private const uint LimitPriorityClass = 0x00000020;

    /// <summary><c>BELOW_NORMAL_PRIORITY_CLASS</c>.</summary>
    private const uint BelowNormalPriorityClass = 0x00004000;

    private readonly SafeJobHandle _handle;

    private WindowsJob(SafeJobHandle handle, bool holdsBelowNormal)
    {
        _handle = handle;
        HoldsBelowNormal = holdsBelowNormal;
    }

    /// <summary>
    /// Whether the job has Windows' priority-class limit at below normal (<see cref="TryAttach"/>):
    /// Windows then puts every process in the job at below normal as it joins, and keeps it there,
    /// however its class was chosen when it was created (docs/phase-6-design.md 8.10).
    /// </summary>
    public bool HoldsBelowNormal { get; }

    /// <summary>
    /// Puts a process that has just been started into a new job. Returns null when Windows
    /// refuses; the caller then falls back to killing by process tree. With
    /// <paramref name="belowNormal"/>, the job first gets the priority-class limit at below normal
    /// (<see cref="HoldsBelowNormal"/>), so the process joins it below normal, and so does every
    /// process it starts from then on, as Windows adds a new process to its creator's job. Should
    /// Windows refuse the limit, the job is made without it.
    /// </summary>
    public static WindowsJob? TryAttach(Process process, bool belowNormal = false)
    {
        var handle = CreateJobObjectW(IntPtr.Zero, null);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        var holdsBelowNormal = false;
        if (belowNormal)
        {
            var limit = new BasicLimit { LimitFlags = LimitPriorityClass, PriorityClass = BelowNormalPriorityClass };
            holdsBelowNormal = SetInformationJobObject(handle, BasicLimitInformation, ref limit, Marshal.SizeOf<BasicLimit>());
        }

        try
        {
            if (AssignProcessToJobObject(handle, process.SafeHandle))
            {
                return new WindowsJob(handle, holdsBelowNormal);
            }
        }
        catch (InvalidOperationException)
        {
            // The process has already exited.
        }

        handle.Dispose();
        return null;
    }

    /// <summary>
    /// Adds a process that is not in the job yet: one that its creator started before the creator
    /// joined the job. True when it was added; false when it was in the job already, or Windows
    /// refused.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="process"/> has exited.</exception>
    public bool TryAdd(Process process) => !Contains(process) && AssignProcessToJobObject(_handle, process.SafeHandle);

    /// <summary>Whether <paramref name="process"/> is in the job.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="process"/> has exited.</exception>
    public bool Contains(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        return IsProcessInJob(process.SafeHandle, _handle, out var inJob) && inJob;
    }

    /// <summary>Stops every process in the job.</summary>
    public void Terminate()
    {
        if (!_handle.IsClosed)
        {
            TerminateJobObject(_handle, 1);
        }
    }

    public void Dispose() => _handle.Dispose();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeJobHandle CreateJobObjectW(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeJobHandle job, SafeProcessHandle process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeJobHandle job, int informationClass, ref BasicLimit information, int length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(SafeProcessHandle process, SafeJobHandle job, [MarshalAs(UnmanagedType.Bool)] out bool result);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(SafeJobHandle job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary><c>JOBOBJECT_BASIC_LIMIT_INFORMATION</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimit
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }
}
