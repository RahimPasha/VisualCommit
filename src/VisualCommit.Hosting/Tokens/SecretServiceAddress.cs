using System.Text;

namespace VisualCommit.Hosting.Tokens;

/// <summary>
/// Where the session bus is (docs/phase-7a-design.md 3.6): a non-empty
/// <c>DBUS_SESSION_BUS_ADDRESS</c>, else <c>$XDG_RUNTIME_DIR/bus</c> when that socket exists, else
/// none. Never Tmds.DBus' own <c>DBusAddress.Session</c>, whose X11 fallback this store must not
/// try: with no bus, the store finds no keychain at once.
/// </summary>
internal static class SecretServiceAddress
{
    public const string AddressVariable = "DBUS_SESSION_BUS_ADDRESS";

    public const string RuntimeDirectoryVariable = "XDG_RUNTIME_DIR";

    /// <summary>The session bus' address, or null when there is none.</summary>
    public static string? Resolve(Func<string, string?> readEnvironment, Func<string, bool> socketExists)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(socketExists);
        var address = readEnvironment(AddressVariable);
        if (!string.IsNullOrEmpty(address))
        {
            return address;
        }

        var runtime = readEnvironment(RuntimeDirectoryVariable);
        if (string.IsNullOrEmpty(runtime))
        {
            return null;
        }

        var socket = runtime.TrimEnd('/') + "/bus";
        return socketExists(socket) ? "unix:path=" + Escape(socket) : null;
    }

    /// <summary>A D-Bus address value: bytes other than <c>[-0-9A-Za-z_/.\*]</c> as <c>%xx</c> of their UTF-8.</summary>
    internal static string Escape(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '/' or '.' or '\\' or '*')
            {
                escaped.Append(c);
            }
            else
            {
                escaped.Append('%').Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return escaped.ToString();
    }
}
