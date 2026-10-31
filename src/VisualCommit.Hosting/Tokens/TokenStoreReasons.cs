namespace VisualCommit.Hosting.Tokens;

/// <summary>
/// Why a token store could not keep, read or delete a token (docs/phase-7a-design.md 3.3, D268):
/// seven fixed texts that name no store and no operating system, so that the app may show one and
/// the pictures stay the same on every system. The platform's own detail goes to the log only.
/// </summary>
public static class TokenStoreReasons
{
    /// <summary>The probe reached no keychain.</summary>
    public const string NoKeychain = "no keychain was found on this computer";

    /// <summary>Windows' error 1312 or a session type below local machine; macOS' "User interaction is not allowed".</summary>
    public const string CannotUse = "the keychain cannot be used in this session";

    /// <summary>An unlock prompt was dismissed, or the keychain answered that it is locked.</summary>
    public const string Locked = "the keychain stayed locked";

    /// <summary>A call ran out of its time limit.</summary>
    public const string NoAnswer = "the keychain did not answer in time";

    /// <summary>Windows only: a token over 1,280 characters.</summary>
    public const string TooLong = "the token is too long for the keychain";

    /// <summary>The entry is not valid base64 or UTF-8.</summary>
    public const string Unreadable = "the keychain's entry could not be read";

    /// <summary>Anything else, including a key the stores refuse before any call.</summary>
    public const string Refused = "the keychain refused it";

    /// <summary>Every reason, in the order above.</summary>
    public static IReadOnlyList<string> All { get; } = [NoKeychain, CannotUse, Locked, NoAnswer, TooLong, Unreadable, Refused];
}
