using System.Security.Cryptography;
using System.Text;
using VisualCommit.Core.Logging;

namespace VisualCommit.Hosting.Tokens;

/// <summary>
/// Tokens in the macOS keychain through <c>/usr/bin/security</c> (docs/phase-7a-design.md 3.5): a
/// generic password per key, the token as its UTF-8 in base64, so that the one line a save writes
/// to <c>security -i</c>'s standard input holds nothing but base64 inside its quotes, and the token
/// is never an argument. With <c>keychainFile</c> every command names that keychain (CI's
/// throw-away one); without, the user's default. The constructor does nothing.
/// <para>
/// An item made through <c>security</c> trusts <c>/usr/bin/security</c>, so any process of the user
/// can read it through <c>security</c> without a prompt, as on Windows and Linux (ruling 20, a plan
/// risk until phase 8).
/// </para>
/// </summary>
public sealed class MacKeychainStore(
    IAppLog log,
    ITokenProcessRunner? runner = null,
    string? keychainFile = null,
    string application = TokenEntryNames.Application) : ITokenStore
{
    /// <summary>The program, always by full path.</summary>
    internal const string SecurityPath = "/usr/bin/security";

    /// <summary>errSecItemNotFound's exit code.</summary>
    internal const int ExitNotFound = 44;

    /// <summary>A command's limit: macOS may show its own unlock dialog.</summary>
    internal static readonly TimeSpan CallLimit = TimeSpan.FromSeconds(60);

    internal static readonly TimeSpan ProbeLimit = TimeSpan.FromSeconds(5);

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ITokenProcessRunner _runner = runner ?? TokenProcessRunner.Instance;

    /// <summary>Whether a file exists: <c>security</c> itself and the keychain file. Tests put their own.</summary>
    internal Func<string, bool> FileExists { get; init; } = File.Exists;

    public string Name => "macOS keychain";

    public bool KeepsTokensAcrossRestarts => true;

    // On the thread pool, as Windows' and Linux' probes are: it looks for files first (Q2).
    public Task<TokenStoreProbe> ProbeAsync(CancellationToken ct) => Task.Run(() => ProbeCoreAsync(ct), ct);

    private async Task<TokenStoreProbe> ProbeCoreAsync(CancellationToken ct)
    {
        if (!FileExists(SecurityPath))
        {
            log.Warning($"{Name}: {SecurityPath} was not found.");
            return new TokenStoreProbe(false, TokenStoreReasons.NoKeychain);
        }

        if (keychainFile is not null)
        {
            if (FileExists(keychainFile))
            {
                log.Debug($"{Name}: available (a keychain file).");
                return new TokenStoreProbe(true, null);
            }

            log.Warning($"{Name}: the keychain file was not found.");
            return new TokenStoreProbe(false, TokenStoreReasons.NoKeychain);
        }

        var result = await RunAsync("probe", null, ["default-keychain", "-d", "user"], null, ProbeLimit, ct).ConfigureAwait(false);
        if (result is { ExitCode: 0 } && !string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            log.Debug($"{Name}: available.");
            return new TokenStoreProbe(true, null);
        }

        if (result is { ExitCode: null })
        {
            log.Warning($"{Name}: security default-keychain did not answer within {ProbeLimit.TotalSeconds:0} s.");
            return new TokenStoreProbe(false, TokenStoreReasons.NoAnswer);
        }

        if (result is not null)
        {
            log.Warning($"{Name}: no default keychain (exit code {result.ExitCode}: {FirstLine(result.StandardError)}).");
        }

        return new TokenStoreProbe(false, TokenStoreReasons.NoKeychain);
    }

    public async Task<TokenStoreResult> SaveAsync(TokenKey key, string token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(token);
        ct.ThrowIfCancellationRequested();
        if (Names(key) is not { } names)
        {
            return new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
        }

        var bytes = Encoding.UTF8.GetBytes(token);
        string base64;
        try
        {
            base64 = Convert.ToBase64String(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }

        var line = $"add-generic-password -U -s '{names.MacService}' -a '{names.MacAccount}' -l '{names.MacLabel}' -w '{base64}'"
            + (keychainFile is null ? string.Empty : $" '{keychainFile}'")
            + "\n";
        var result = await RunAsync("save", key, ["-i"], line, CallLimit, ct).ConfigureAwait(false);
        if (result is null)
        {
            return new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
        }

        if (result.ExitCode is null)
        {
            return new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.NoAnswer);
        }

        // These two end the save at once: a read-back would only meet the same answer or a second dialog.
        if (KnownError(result.StandardError) is { } known)
        {
            LogFailure("save", key, result, token, base64);
            return new TokenStoreResult(TokenStoreOutcome.Failed, known);
        }

        // security -i's exit code after a failed command is not known: the entry read back decides.
        var back = await ReadCoreAsync(key, names, ct).ConfigureAwait(false);
        if (back.Outcome == TokenStoreOutcome.Done && string.Equals(back.Token, token, StringComparison.Ordinal))
        {
            log.Debug($"{Name}: saved the token for {key.Host}/{key.Account}.");
            return new TokenStoreResult(TokenStoreOutcome.Done);
        }

        LogFailure("save", key, result, token, base64);
        log.Warning($"{Name}: the entry read back after saving the token for {key.Host}/{key.Account} "
            + (back.Outcome == TokenStoreOutcome.Done ? "holds another token." : $"was {back.Outcome}."));
        return new TokenStoreResult(
            TokenStoreOutcome.Failed,
            back.Outcome == TokenStoreOutcome.Failed ? back.Reason ?? TokenStoreReasons.Refused : TokenStoreReasons.Refused);
    }

    public async Task<TokenRead> ReadAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        ct.ThrowIfCancellationRequested();
        return Names(key) is { } names
            ? await ReadCoreAsync(key, names, ct).ConfigureAwait(false)
            : new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Refused);
    }

    public async Task<TokenStoreResult> DeleteAsync(TokenKey key, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        ct.ThrowIfCancellationRequested();
        if (Names(key) is not { } names)
        {
            return new TokenStoreResult(TokenStoreOutcome.Failed, TokenStoreReasons.Refused);
        }

        var result = await RunAsync("delete", key, ["delete-generic-password", "-s", names.MacService, "-a", names.MacAccount, .. KeychainArgument()], null, CallLimit, ct)
            .ConfigureAwait(false);
        if (result is { ExitCode: 0 })
        {
            log.Debug($"{Name}: deleted the token for {key.Host}/{key.Account}.");
            return new TokenStoreResult(TokenStoreOutcome.Done);
        }

        if (result is not null && IsNotFound(result))
        {
            return new TokenStoreResult(TokenStoreOutcome.NotFound);
        }

        return new TokenStoreResult(TokenStoreOutcome.Failed, Failure("delete", key, result));
    }

    private async Task<TokenRead> ReadCoreAsync(TokenKey key, TokenEntryNames names, CancellationToken ct)
    {
        var result = await RunAsync("read", key, ["find-generic-password", "-s", names.MacService, "-a", names.MacAccount, "-w", .. KeychainArgument()], null, CallLimit, ct)
            .ConfigureAwait(false);
        if (result is { ExitCode: 0 })
        {
            if (Decode(result.StandardOutput) is { } token)
            {
                log.Debug($"{Name}: read the token for {key.Host}/{key.Account}.");
                return new TokenRead(TokenStoreOutcome.Done, token, null);
            }

            log.Warning($"{Name}: the entry for {key.Host}/{key.Account} is not base64 of UTF-8 text.");
            return new TokenRead(TokenStoreOutcome.Failed, null, TokenStoreReasons.Unreadable);
        }

        if (result is not null && IsNotFound(result))
        {
            log.Debug($"{Name}: no token for {key.Host}/{key.Account}.");
            return new TokenRead(TokenStoreOutcome.NotFound, null, null);
        }

        return new TokenRead(TokenStoreOutcome.Failed, null, Failure("read", key, result));
    }

    /// <summary>The token of <c>find-generic-password -w</c>'s output: one line feed removed, strict base64, strict UTF-8.</summary>
    internal static string? Decode(string output)
    {
        var text = output.EndsWith('\n') ? output[..^1] : output;
        if (text.Length == 0 || text.Length % 4 != 0 || !text.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '/' or '='))
        {
            return null;
        }

        var bytes = new byte[text.Length / 4 * 3];
        try
        {
            if (!Convert.TryFromBase64String(text, bytes, out var written) || written == 0)
            {
                return null;
            }

            return StrictUtf8.GetString(bytes, 0, written);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private TokenEntryNames? Names(TokenKey key)
    {
        if (keychainFile is not null && (keychainFile.Length == 0 || keychainFile.Any(c => char.IsControl(c) || c is '\'' or '\\')))
        {
            log.Warning($"{Name}: the keychain file's path cannot be quoted for security; no call was made.");
            return null;
        }

        if (TokenEntryNames.TryFor(key, application, out var names))
        {
            return names;
        }

        log.Warning($"{Name}: a part of the key {TokenEntryNames.ForLog(key)} cannot name an entry; no call was made.");
        return null;
    }

    private string[] KeychainArgument() => keychainFile is null ? [] : [keychainFile];

    // The runner's result, or null when it could not run security at all (logged).
    private async Task<TokenProcessResult?> RunAsync(
        string call, TokenKey? key, IReadOnlyList<string> arguments, string? standardInput, TimeSpan limit, CancellationToken ct)
    {
        try
        {
            var result = await _runner.RunAsync(SecurityPath, arguments, standardInput, limit, ct).ConfigureAwait(false);
            if (result.ExitCode is null)
            {
                log.Warning($"{Name}: security's {call}{For(key)} did not answer within {limit.TotalSeconds:0} s.");
            }

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // An exception's text could quote the input: only its type is logged.
            log.Warning($"{Name}: security could not be run for the {call}{For(key)} ({exception.GetType().Name}).");
            return null;
        }
    }

    private string Failure(string call, TokenKey key, TokenProcessResult? result)
    {
        if (result is null)
        {
            return TokenStoreReasons.Refused;
        }

        if (result.ExitCode is null)
        {
            return TokenStoreReasons.NoAnswer;
        }

        LogFailure(call, key, result);
        return KnownError(result.StandardError) ?? TokenStoreReasons.Refused;
    }

    private void LogFailure(string call, TokenKey key, TokenProcessResult result, params string[] secrets)
    {
        var line = FirstLine(result.StandardError);
        foreach (var secret in secrets)
        {
            line = line.Replace(secret, "***", StringComparison.Ordinal);
        }

        log.Warning($"{Name}: security's {call} for {key.Host}/{key.Account} failed (exit code {result.ExitCode}: {line}).");
    }

    private static string For(TokenKey? key) => key is null ? string.Empty : $" for {key.Host}/{key.Account}";

    private static bool IsNotFound(TokenProcessResult result) =>
        result.ExitCode == ExitNotFound || result.StandardError.Contains("could not be found", StringComparison.Ordinal);

    // errSecInteractionNotAllowed and errSecUserCanceled (an unlock dialog dismissed).
    private static string? KnownError(string standardError) =>
        standardError.Contains("User interaction is not allowed", StringComparison.Ordinal) ? TokenStoreReasons.CannotUse
        : standardError.Contains("User canceled the operation", StringComparison.Ordinal) ? TokenStoreReasons.Locked
        : null;

    private static string FirstLine(string text)
    {
        var trimmed = text.TrimStart();
        var end = trimmed.IndexOfAny(['\r', '\n']);
        return (end < 0 ? trimmed : trimmed[..end]).Trim();
    }
}
