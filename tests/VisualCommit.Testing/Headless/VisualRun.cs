using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VisualCommit.Testing.Headless;

/// <summary>
/// Where the scripted walk-throughs keep their screenshots:
/// <c>artifacts/visual/phase-N/scripted/</c> under the repo root. The folder is git-ignored.
/// Shared by the walk-through projects (<c>VisualCommit.VisualTests</c> for phases 0 to 2,
/// <c>VisualCommit.VisualTests.Phase3</c> and <c>.Phase4</c> for phases 3 and 4, and
/// <c>VisualCommit.VisualTests.Phase5</c> and <c>.Phase5Undo</c>, which both fill phase 5's
/// folder, docs/phase-5-design.md 15.3). Since phase 7a a phase is named by a string ("7a",
/// docs/phase-7a-design.md 0.3, 2.7); the <c>int</c> forms of phases 0 to 6 name the same folders.
/// </summary>
public static class VisualRun
{
    /// <summary>
    /// The file in a phase's picture folder that names the run whose pictures it holds
    /// (<see cref="RunKey"/>); the walk-throughs' processes take turns at it.
    /// </summary>
    internal const string RunFile = ".run";

    /// <summary>How long a process waits for another to finish starting the same folder's run.</summary>
    private static readonly TimeSpan RunFileWait = TimeSpan.FromSeconds(30);

    private static readonly string RepoRoot =
        typeof(VisualRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(attribute => attribute.Key == "RepoRoot").Value!;

    private static readonly Lock Gate = new();
    private static readonly HashSet<string> PhasesStarted = new(StringComparer.Ordinal);
    private static readonly Lazy<string> RunKeyOfThisProcess = new(ReadRunKey);

    /// <summary>The screenshot folder of a phase's scripted walk-through.</summary>
    public static string FolderFor(int phase) => FolderFor(phase.ToString(CultureInfo.InvariantCulture));

    /// <summary>The screenshot folder of a phase's scripted walk-through, the phase named as the docs write it: <c>artifacts/visual/phase-7a/scripted</c> for "7a".</summary>
    public static string FolderFor(string phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        return Path.Combine(RepoRoot, "artifacts", "visual", $"phase-{phase}", "scripted");
    }

    /// <summary>
    /// The test run this process belongs to: the process that started it when that is the dotnet
    /// CLI, which starts every test project of one <c>dotnet test</c> (or <c>dotnet run</c>) as
    /// its own child; otherwise this process alone (a test program started from a shell or an
    /// IDE). A process is named by its id and its start (<see cref="KeyOf"/>), so that a later
    /// process with the same id is another.
    /// </summary>
    internal static string RunKey => RunKeyOfThisProcess.Value;

    /// <summary>
    /// Saves a screenshot of a phase's walk-through as <c>&lt;name&gt;.png</c> and returns its path.
    /// The first save of a phase in a test run empties the phase's folder, so every picture in it
    /// comes from that run and none is left over from a check that was renamed or removed. After
    /// a run of only some checks the folder therefore holds only their pictures. A run is
    /// <see cref="RunKey"/>'s, not a process's: phase 5's two walk-through projects, two
    /// processes of one <c>dotnet test</c>, empty its folder once between them (<see cref="StartRun"/>).
    /// </summary>
    public static string Save(this Screenshot screenshot, int phase, string name) =>
        screenshot.Save(phase.ToString(CultureInfo.InvariantCulture), name);

    /// <summary>Saves a screenshot of a phase's walk-through, the phase named as the docs write it ("7a"), as the <c>int</c> form does.</summary>
    public static string Save(this Screenshot screenshot, string phase, string name)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        lock (Gate)
        {
            if (PhasesStarted.Add(phase))
            {
                StartRun(FolderFor(phase), RunKey);
            }
        }

        return screenshot.Save(Path.Combine(FolderFor(phase), name + ".png"));
    }

    /// <summary>
    /// Makes <paramref name="folder"/> the picture folder of the run <paramref name="runKey"/>:
    /// unless its <see cref="RunFile"/> names that run already, deletes the pictures in it
    /// (<c>*.png</c>, nothing else) and writes the run's key there. Processes take turns, the file
    /// held open by one at a time, so that of the processes of one run exactly one empties the
    /// folder, before any of them saves a picture there. Returns whether this call emptied it.
    /// </summary>
    internal static bool StartRun(string folder, string runKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentException.ThrowIfNullOrEmpty(runKey);
        Directory.CreateDirectory(folder);
        using var file = OpenAlone(Path.Combine(folder, RunFile));
        using var reader = new StreamReader(file, leaveOpen: true);
        if (string.Equals(reader.ReadToEnd(), runKey, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var old in Directory.EnumerateFiles(folder, "*.png"))
        {
            File.Delete(old);
        }

        file.SetLength(0);
        using var writer = new StreamWriter(file, leaveOpen: true);
        writer.Write(runKey);
        return true;
    }

    /// <summary>Opens a file for this process alone, creating it when missing; waits while another holds it.</summary>
    private static FileStream OpenAlone(string path)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (waited.Elapsed < RunFileWait)
            {
                Thread.Sleep(20);
            }
        }
    }

    private static string ReadRunKey()
    {
        using var self = Process.GetCurrentProcess();
        try
        {
            if (ParentProcessId() is { } parentId)
            {
                // A parent that started after this process is another that took the exited
                // parent's id: Windows hands out the ids of processes that have ended again.
                // Compared in UTC, since local time goes back an hour when summer time ends.
                using var parent = Process.GetProcessById(parentId);
                if (string.Equals(parent.ProcessName, "dotnet", StringComparison.OrdinalIgnoreCase) && parent.StartTime.ToUniversalTime() <= self.StartTime.ToUniversalTime())
                {
                    return KeyOf(parent);
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException or FormatException)
        {
            // The parent has exited, or cannot be read on this system: the run is this process's.
        }

        return KeyOf(self);
    }

    /// <summary>
    /// A process as <see cref="RunKey"/> names it: its id and its start, which every process reads
    /// alike. On Linux the start is the clock tick after boot that the system records
    /// (<see cref="StartTicksOnLinux"/>), not <see cref="Process.StartTime"/>: .NET adds those
    /// ticks to a boot time that each process works out for itself from a coarse clock, so two
    /// processes of one run would name their parent by times up to a few milliseconds apart, and
    /// the second to save a picture would empty the first one's. Windows and macOS record the
    /// start time itself, which is taken in UTC.
    /// </summary>
    internal static string KeyOf(Process process) =>
        OperatingSystem.IsLinux()
            ? string.Create(CultureInfo.InvariantCulture, $"{process.Id} {StartTicksOnLinux(process.Id)}")
            : string.Create(CultureInfo.InvariantCulture, $"{process.Id} {process.StartTime.ToUniversalTime():O}");

    /// <summary>
    /// When a process started, on Linux: clock ticks after the system's boot, field 22 of
    /// <c>/proc/&lt;id&gt;/stat</c>.
    /// </summary>
    private static long StartTicksOnLinux(int processId) =>
        long.Parse(StatFieldsOnLinux(processId.ToString(CultureInfo.InvariantCulture))[19], NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>The id of the process that started this one, or null where it cannot be read.</summary>
    internal static int? ParentProcessId()
    {
        if (OperatingSystem.IsWindows())
        {
            var information = default(ProcessBasicInformation);
            return NtQueryInformationProcess(CurrentProcess, ProcessBasicInformationClass, ref information, Marshal.SizeOf<ProcessBasicInformation>(), out _) == 0
                ? (int)information.InheritedFromUniqueProcessId
                : null;
        }

        if (OperatingSystem.IsLinux())
        {
            return int.Parse(StatFieldsOnLinux("self")[1], NumberStyles.None, CultureInfo.InvariantCulture);
        }

        return GetParentProcessId();
    }

    /// <summary>
    /// The fields of <c>/proc/&lt;process&gt;/stat</c> from the third, the state, on. The file
    /// reads "&lt;pid&gt; (&lt;name&gt;) &lt;state&gt; &lt;parent id&gt; …", and the name may hold
    /// spaces and parentheses.
    /// </summary>
    private static string[] StatFieldsOnLinux(string process)
    {
        var stat = File.ReadAllText($"/proc/{process}/stat");
        return stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
    }

    /// <summary><c>GetCurrentProcess()</c>'s pseudo handle: this process.</summary>
    private static readonly IntPtr CurrentProcess = new(-1);

    /// <summary><c>ProcessBasicInformation</c> of <c>PROCESSINFOCLASS</c>.</summary>
    private const int ProcessBasicInformationClass = 0;

    /// <summary><c>PROCESS_BASIC_INFORMATION</c>, whose last field is the parent's id.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr process, int informationClass, ref ProcessBasicInformation information, int length, out int returnLength);

    [DllImport("libc", EntryPoint = "getppid")]
    private static extern int GetParentProcessId();
}
