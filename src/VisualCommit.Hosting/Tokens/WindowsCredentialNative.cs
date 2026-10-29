using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace VisualCommit.Hosting.Tokens;

/// <summary>
/// advapi32's credential calls (docs/phase-7a-design.md 3.4), which <see cref="WindowsCredentialStore"/>
/// makes on the thread pool. Generic credentials only.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsCredentialNative
{
    public const uint CredTypeGeneric = 1;

    /// <summary>CRED_TYPE_MAXIMUM: the length of <see cref="CredGetSessionTypes"/>' array.</summary>
    public const uint CredTypeMaximum = 7;

    public const uint CredPersistLocalMachine = 2;

    public const int ErrorNotFound = 1168;

    public const int ErrorNoSuchLogonSession = 1312;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CREDENTIALW
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredWriteW(ref CREDENTIALW credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredReadW(string targetName, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredDeleteW(string targetName, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern void CredFree(IntPtr buffer);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CredGetSessionTypes(uint maximumPersistCount, [Out] uint[] maximumPersist);
}
