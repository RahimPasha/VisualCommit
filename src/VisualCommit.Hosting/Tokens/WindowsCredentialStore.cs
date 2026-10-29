using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using VisualCommit.Core.Logging;
using static VisualCommit.Hosting.Tokens.WindowsCredentialNative;

namespace VisualCommit.Hosting.Tokens;

/// <summary>
/// Tokens in Windows' Credential Manager (docs/phase-7a-design.md 3.4): a generic credential per
/// key, kept for the local machine, the token as UTF-16LE bytes. Every call runs on the thread pool
/// (Q2); off Windows every call answers that there is no keychain. The constructor does nothing.
/// </summary>
public sealed class WindowsCredentialStore(IAppLog log, string application = TokenEntryNames.Application) : ITokenStore
{
    /// <summary>The longest token that fits: CRED_MAX_CREDENTIAL_BLOB_SIZE is 2,560 bytes of UTF-16LE.</summary>
    internal const int MaxTokenLength = 1280;

    // A blob that is not whole UTF-16 is not the app's.
    private static readonly UnicodeEncoding StrictUtf16 = new(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);

    public string Name => "Windows Credential Manager";

    public bool KeepsTokensAcrossRestarts => true;

    /// <summary>Whether a token fits in a credential's blob; a save asks this before any call.</summary>
    internal static bool Fits(string token) => token.Length <= MaxTokenLength;

    public Task<TokenStoreProbe> ProbeAsync(CancellationToken ct) => Task.Run(Probe, ct);

    public Task<TokenStoreResult> SaveAsync(TokenKey key, string token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(token);
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<TokenStoreResult>(ct);
        }

        if (!Fits(token))
        {
            log.Warning($"{Name}: the token for {TokenEntryNames.ForLog(key)} is longer than {MaxTokenLength} characters.");
            return Task.FromResult(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.TooLong));
        }

        return Names(key) is { } names
            ? Task.Run(() => Save(key, names, token), ct)
            : Task.FromResult(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused));
    }

    public Task<TokenRead> ReadAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<TokenRead>(ct);
        }

        return Names(key) is { } names
            ? Task.Run(() => Read(key, names), ct)
            : Task.FromResult(new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Refused));
    }

    public Task<TokenStoreResult> DeleteAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<TokenStoreResult>(ct);
        }

        return Names(key) is { } names
            ? Task.Run(() => Delete(key, names), ct)
            : Task.FromResult(new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused));
    }

    private TokenEntryNames? Names(TokenKey key)
    {
        if (TokenEntryNames.TryFor(key, application, out var names))
        {
            return names;
        }

        log.Warning($"{Name}: a part of the key {TokenEntryNames.ForLog(key)} cannot name an entry; no call was made.");
        return null;
    }

    private TokenStoreProbe Probe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new TokenStoreProbe(false, TokenStoreReasons.NoKeychain);
        }

        try
        {
            var persist = new uint[CredTypeMaximum];
            if (!CredGetSessionTypes(CredTypeMaximum, persist))
            {
                var error = Marshal.GetLastPInvokeError();
                log.Warning($"{Name}: the session's credential types could not be read (Win32 error {error}).");
                return new TokenStoreProbe(false, TokenStoreReasons.CannotUse);
            }

            if (persist[CredTypeGeneric] < CredPersistLocalMachine)
            {
                log.Warning($"{Name}: this session keeps generic credentials only at persist type {persist[CredTypeGeneric]}, below local machine.");
                return new TokenStoreProbe(false, TokenStoreReasons.CannotUse);
            }

            log.Debug($"{Name}: available.");
            return new TokenStoreProbe(true, null);
        }
        catch (Exception exception)
        {
            log.Warning($"{Name}: the probe failed.", exception);
            return new TokenStoreProbe(false, TokenStoreReasons.CannotUse);
        }
    }

    private TokenStoreResult Save(TokenKey key, TokenEntryNames names, string token)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.NoKeychain);
        }

        var bytes = Encoding.Unicode.GetBytes(token);
        var blob = IntPtr.Zero;
        try
        {
            blob = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new CREDENTIALW
            {
                Type = CredTypeGeneric,
                TargetName = names.WindowsTarget,
                Comment = names.WindowsComment,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = CredPersistLocalMachine,
                UserName = names.WindowsUser,
            };

            // CredWriteW replaces an entry of the same target.
            if (CredWriteW(ref credential, 0))
            {
                log.Debug($"{Name}: saved the token for {key.Host}/{key.Account}.");
                return new TokenStoreResult(TokenStoreOutcome.Done);
            }

            return new TokenStoreResult(TokenStoreOutcome.Failed, Failure("save", key, Marshal.GetLastPInvokeError()));
        }
        catch (Exception exception)
        {
            log.Warning($"{Name}: saving the token for {key.Host}/{key.Account} failed.", exception);
            return new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
        }
        finally
        {
            if (blob != IntPtr.Zero)
            {
                Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
                Marshal.FreeHGlobal(blob);
            }

            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private TokenRead Read(TokenKey key, TokenEntryNames names)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.NoKeychain);
        }

        try
        {
            if (!CredReadW(names.WindowsTarget, CredTypeGeneric, 0, out var pointer))
            {
                var error = Marshal.GetLastPInvokeError();
                if (error == ErrorNotFound)
                {
                    log.Debug($"{Name}: no token for {key.Host}/{key.Account}.");
                    return new TokenRead(TokenStoreOutcome.NotFound, null, null);
                }

                return new TokenRead(TokenStoreOutcome.Failed, null, Failure("read", key, error));
            }

            try
            {
                var credential = Marshal.PtrToStructure<CREDENTIALW>(pointer);
                var bytes = new byte[credential.CredentialBlobSize];
                try
                {
                    if (bytes.Length > 0)
                    {
                        Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                    }

                    if (bytes.Length == 0 || bytes.Length % 2 != 0)
                    {
                        log.Warning($"{Name}: the entry for {key.Host}/{key.Account} holds {bytes.Length} bytes, no UTF-16 text.");
                        return new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Unreadable);
                    }

                    var token = StrictUtf16.GetString(bytes);
                    log.Debug($"{Name}: read the token for {key.Host}/{key.Account}.");
                    return new TokenRead(TokenStoreOutcome.Done, token, null);
                }
                catch (DecoderFallbackException)
                {
                    log.Warning($"{Name}: the entry for {key.Host}/{key.Account} is not valid UTF-16.");
                    return new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Unreadable);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                }
            }
            finally
            {
                CredFree(pointer);
            }
        }
        catch (Exception exception)
        {
            log.Warning($"{Name}: reading the token for {key.Host}/{key.Account} failed.", exception);
            return new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Refused);
        }
    }

    private TokenStoreResult Delete(TokenKey key, TokenEntryNames names)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.NoKeychain);
        }

        try
        {
            if (CredDeleteW(names.WindowsTarget, CredTypeGeneric, 0))
            {
                log.Debug($"{Name}: deleted the token for {key.Host}/{key.Account}.");
                return new TokenStoreResult(TokenStoreOutcome.Done);
            }

            var error = Marshal.GetLastPInvokeError();
            return error == ErrorNotFound
                ? new TokenStoreResult(TokenStoreOutcome.NotFound)
                : new TokenStoreResult(TokenStoreOutcome.Failed, Failure("delete", key, error));
        }
        catch (Exception exception)
        {
            log.Warning($"{Name}: deleting the token for {key.Host}/{key.Account} failed.", exception);
            return new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
        }
    }

    // Error 1312 on any call: no logon session keeps credentials here (a service, a runner).
    private string Failure(string call, TokenKey key, int error)
    {
        log.Warning($"{Name}: could not {call} the token for {key.Host}/{key.Account} (Win32 error {error}).");
        return error == ErrorNoSuchLogonSession ? TokenStoreReasons.CannotUse : TokenStoreReasons.Refused;
    }
}
