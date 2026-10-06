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
    private readonly SafeJobHandle _handle;

    private WindowsJob(SafeJobHandle handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Puts a process that has just been started into a new job. Returns null when Windows
    /// refuses; the caller then falls back to killing by process tree.
    /// </summary>
    public static WindowsJob? TryAttach(Process process)
    {
        var handle = CreateJobObjectW(IntPtr.Zero, null);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        try
        {
            if (AssignProcessToJobObject(handle, process.SafeHandle))
            {
                return new WindowsJob(handle);
            }
        }
        catch (InvalidOperationException)
        {
            // The process has already exited.
        }

        handle.Dispose();
        return null;
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
    private static extern bool TerminateJobObject(SafeJobHandle job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }
}
