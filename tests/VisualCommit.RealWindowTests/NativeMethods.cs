using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VisualCommit.RealWindowTests;

/// <summary>The few Win32 calls the real-window pass needs for window sizes and display scaling, and the high contrast setting it reads (phase 6's S15).</summary>
internal static class NativeMethods
{
    public const uint SwpNoZOrder = 0x0004;
    public const uint SwpNoMove = 0x0002;
    public const uint SwpNoSize = 0x0001;
    public const uint SwpShowWindow = 0x0040;
    public const uint MonitorDefaultToNearest = 2;

    public static readonly IntPtr HwndTopMost = new(-1);

    private static readonly IntPtr DpiAwarenessPerMonitorV2 = new(-4);

    /// <summary>
    /// Makes this test process see real pixels. Without it Windows scales every coordinate and
    /// screenshot for the process on a display that is not at 100%.
    /// </summary>
    [ModuleInitializer]
    internal static void UseRealPixels() => SetProcessDpiAwarenessContext(DpiAwarenessPerMonitorV2);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ClientToScreen(IntPtr window, ref Point point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsZoomed(IntPtr window);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);

    /// <summary>SystemParametersInfo's action that reads the high contrast settings (<c>SPI_GETHIGHCONTRAST</c>).</summary>
    public const uint SpiGetHighContrast = 0x0042;

    /// <summary>The flag of <see cref="HighContrast.Flags"/> that says high contrast is on (<c>HCF_HIGHCONTRASTON</c>).</summary>
    public const uint HcfHighContrastOn = 0x00000001;

    /// <summary>
    /// Reads a system-wide setting (<c>SystemParametersInfoW</c>). The pass only reads with it
    /// (<c>fWinIni</c> 0, an SPI_GET action): it never changes a setting of the desktop (D224,
    /// docs/phase-6-design.md 11.8).
    /// </summary>
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SystemParametersInfo(uint action, uint parameter, ref HighContrast value, uint winIni);

    /// <summary>Every process of the machine with its parent's id, from a Toolhelp snapshot: to find the processes an app started (check 31).</summary>
    public static IReadOnlyList<(int Id, int ParentId, string ExeName)> Processes()
    {
        var snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snapshot == InvalidHandle)
        {
            throw new InvalidOperationException($"CreateToolhelp32Snapshot failed with error {Marshal.GetLastWin32Error()}.");
        }

        try
        {
            var result = new List<(int, int, string)>();
            var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
            for (var more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
            {
                result.Add(((int)entry.ProcessId, (int)entry.ParentProcessId, entry.ExeFile));
            }

            return result;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private const uint SnapProcess = 0x00000002;

    private static readonly IntPtr InvalidHandle = new(-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
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

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }

    /// <summary>The high contrast settings as <see cref="SystemParametersInfo"/> reads them (<c>HIGHCONTRASTW</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HighContrast
    {
        public uint Size;
        public uint Flags;
        public IntPtr DefaultScheme;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
