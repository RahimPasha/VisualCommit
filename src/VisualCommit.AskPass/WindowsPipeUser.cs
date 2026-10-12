using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace VisualCommit.AskPass;

/// <summary>
/// Whose the pipe is on Windows (docs/phase-3-design.md 3.4): the user that the app and the
/// helper run as, the user of their access tokens. Both ends check that instead of using
/// <see cref="PipeOptions.CurrentUserOnly"/>, which on Windows compares the token's default
/// owner. That owner is not the same for the two ends in one case that matters: in an elevated
/// process (an app run as administrator, CI's runners) it is the Administrators group, but the
/// MSYS2 runtime of Git for Windows' ssh and ssh-keygen sets it to the user at start-up, and the
/// helper that ssh starts inherits that. With <see cref="PipeOptions.CurrentUserOnly"/> the
/// helper refused the app's pipe, and every ssh prompt of an elevated app failed unasked.
/// <para>
/// On macOS and Linux both ends keep <see cref="PipeOptions.CurrentUserOnly"/>: there the pipe is
/// a Unix socket, and the check compares user ids.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsPipeUser
{
    /// <summary>The access the server gives: full control for the user, which also lets it open further instances; nobody else.</summary>
    public static PipeSecurity ServerSecurity()
    {
        var user = CurrentUser();
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.SetOwner(user);
        return security;
    }

    /// <summary>Whether the connected <paramref name="pipe"/> belongs to the user this process runs as.</summary>
    public static bool OwnsPipe(PipeStream pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        return pipe.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner
            && owner == CurrentUser();
    }

    /// <summary>The user of this process's access token (not its default owner).</summary>
    public static SecurityIdentifier CurrentUser()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User ?? throw new InvalidOperationException("The process's access token names no user.");
    }
}
