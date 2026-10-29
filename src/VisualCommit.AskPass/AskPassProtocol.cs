using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VisualCommit.AskPass;

/// <summary>
/// What the askpass helper and the app's broker say to each other over the pipe
/// (docs/phase-3-design.md 3.1): one UTF-8 line of JSON from the helper, one line back, then the
/// connection ends. Shared by both ends so that they cannot drift apart.
/// </summary>
public static class AskPassProtocol
{
    /// <summary>The pipe's name, <c>vc-&lt;16 hex&gt;</c>, in the environment of an <c>Ask</c> scope.</summary>
    public const string PipeVariable = "VISUALCOMMIT_ASKPASS_PIPE";

    /// <summary>The app session's secret, 32 random bytes as hex.</summary>
    public const string TokenVariable = "VISUALCOMMIT_ASKPASS_TOKEN";

    /// <summary>The id of the operation (prompt scope) the git process belongs to.</summary>
    public const string OperationVariable = "VISUALCOMMIT_OPERATION_ID";

    /// <summary>Set to <c>1</c> in a <c>Deny</c> scope: the helper refuses at once.</summary>
    public const string DenyVariable = "VISUALCOMMIT_ASKPASS_DENY";

    /// <summary>What OpenSSH sets to tell the kind of prompt: <c>confirm</c>, <c>none</c>, or unset.</summary>
    public const string SshPromptVariable = "SSH_ASKPASS_PROMPT";

    /// <summary>The longest line either end reads, its line end included.</summary>
    public const int MaxLineBytes = 64 * 1024;

    /// <summary>How long the helper tries to reach the app before it gives up.</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The helper's first argument in its credential mode (docs/phase-7a-design.md 4.5, D255): git
    /// runs <c>!'&lt;helper&gt;' --vc-credential</c> as a credential helper and appends <c>get</c>,
    /// <c>store</c> or <c>erase</c>.
    /// </summary>
    public const string CredentialArgument = "--vc-credential";

    /// <summary>The user name the helper gives git with an account's token, never the login (0.2); equal to Core's <c>GitCredential.UserName</c>.</summary>
    public const string CredentialUserName = "x-access-token";

    /// <summary>How long the helper waits for the app's answer to a credential request.</summary>
    public static readonly TimeSpan CredentialAnswerTimeout = TimeSpan.FromSeconds(30);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The helper's request as one line, its <c>\n</c> included.</summary>
    public static byte[] FormatRequest(AskPassRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Line(writer =>
        {
            writer.WriteString("token", request.Token);
            writer.WriteString("operation", request.Operation);
            writer.WriteString("prompt", request.Prompt);
            if (request.SshPrompt is null)
            {
                writer.WriteNull("sshPrompt");
            }
            else
            {
                writer.WriteString("sshPrompt", request.SshPrompt);
            }
        });
    }

    /// <summary>The app's answer as one line; a null <paramref name="answer"/> refuses the prompt.</summary>
    public static byte[] FormatAnswer(string? answer) => Line(writer =>
    {
        if (answer is null)
        {
            writer.WriteNull("answer");
        }
        else
        {
            writer.WriteString("answer", answer);
        }
    });

    /// <summary>Reads a request line; null when it is not one (malformed, wrong types, a field missing).</summary>
    public static AskPassRequest? ParseRequest(ReadOnlySpan<byte> line)
    {
        try
        {
            using var document = JsonDocument.Parse(Trim(line).ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || String(root, "token") is not { } token
                || String(root, "operation") is not { } operation
                || String(root, "prompt") is not { } prompt
                || !root.TryGetProperty("sshPrompt", out var ssh)
                || ssh.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            {
                return null;
            }

            return new AskPassRequest(token, operation, prompt, ssh.ValueKind == JsonValueKind.String ? ssh.GetString() : null);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or DecoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>A credential request as one line, its <c>\n</c> included (docs/phase-7a-design.md 4.5).</summary>
    public static byte[] FormatCredentialRequest(AskPassCredentialRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Line(writer =>
        {
            writer.WriteString("token", request.Token);
            writer.WriteString("operation", request.Operation);
            writer.WriteString("credential", request.Action);
            writer.WriteString("protocol", request.Protocol);
            writer.WriteString("host", request.Host);
            WriteNullable(writer, "username", request.Username);
            WriteNullable(writer, "secretHash", request.SecretHash);
        });
    }

    /// <summary>
    /// Reads a credential request line; null when it is not one: malformed, <c>credential</c>
    /// neither <c>get</c> nor <c>erase</c>, or a string missing. A prompt's line is not one, and
    /// <see cref="ParseRequest"/> refuses this one.
    /// </summary>
    public static AskPassCredentialRequest? ParseCredentialRequest(ReadOnlySpan<byte> line)
    {
        try
        {
            using var document = JsonDocument.Parse(Trim(line).ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || String(root, "token") is not { } token
                || String(root, "operation") is not { } operation
                || String(root, "credential") is not { } action
                || action is not ("get" or "erase")
                || String(root, "protocol") is not { } protocol
                || String(root, "host") is not { } host
                || !TryNullable(root, "username", out var username)
                || !TryNullable(root, "secretHash", out var secretHash))
            {
                return null;
            }

            return new AskPassCredentialRequest(token, operation, action, protocol, host, username, secretHash);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or DecoderFallbackException)
        {
            return null;
        }
    }

    /// <summary>
    /// What git gives a credential helper on standard input (docs/phase-7a-design.md 4.2, 4.5):
    /// <c>key=value</c> lines, each split at its first <c>=</c>, a trailing <c>\r</c> dropped, up
    /// to an empty line or the end. Keeps <c>protocol</c>, <c>host</c>, <c>username</c> and
    /// <c>password</c>; ignores every other key (<c>capability[]</c>, <c>wwwauth[]</c>,
    /// <c>state[]</c>, <c>path</c>, <c>url</c>, <c>password_expiry_utc</c>) and a line without
    /// <c>=</c>. A key given twice keeps its last value.
    /// </summary>
    public static GitCredentialInput ParseGitCredentialInput(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        string? protocol = null, host = null, username = null, password = null;
        foreach (var raw in input.Split('\n'))
        {
            var line = raw.EndsWith('\r') ? raw[..^1] : raw;
            if (line.Length == 0)
            {
                break;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
            {
                continue;
            }

            var value = line[(equals + 1)..];
            switch (line[..equals])
            {
                case "protocol":
                    protocol = value;
                    break;
                case "host":
                    host = value;
                    break;
                case "username":
                    username = value;
                    break;
                case "password":
                    password = value;
                    break;
            }
        }

        return new GitCredentialInput(protocol, host, username, password);
    }

    /// <summary>
    /// What the helper prints for git's <c>get</c> (4.5): <c>username=x-access-token</c> and the
    /// token, each line ended by <c>\n</c>; null when <paramref name="token"/> is null, empty, or
    /// holds a carriage return, a line feed or a NUL, which would break git's format.
    /// </summary>
    public static string? CredentialAnswerText(string? token) =>
        string.IsNullOrEmpty(token) || token.AsSpan().IndexOfAny('\r', '\n', '\0') >= 0
            ? null
            : $"username={CredentialUserName}\npassword={token}\n";

    /// <summary>
    /// The lower-case hex SHA-256 of <paramref name="secret"/>'s UTF-8 bytes: an erase names the
    /// refused password by it, never by the password (D273).
    /// </summary>
    public static string SecretHash(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }

    /// <summary>
    /// Reads an answer line. Returns false when the line is malformed; otherwise true, with
    /// <paramref name="answer"/> null when the app refused the prompt.
    /// </summary>
    public static bool TryParseAnswer(ReadOnlySpan<byte> line, out string? answer)
    {
        answer = null;
        try
        {
            using var document = JsonDocument.Parse(Trim(line).ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("answer", out var value))
            {
                return false;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.Null:
                    return true;
                case JsonValueKind.String:
                    answer = value.GetString();
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads one line from <paramref name="stream"/>: the bytes up to the first <c>\n</c> (not
    /// included), or up to the end of the stream when no line end comes. Null when the stream
    /// ended without a byte, or when more than <see cref="MaxLineBytes"/> came without a line end.
    /// </summary>
    public static async Task<byte[]?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var buffer = new byte[MaxLineBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return length == 0 ? null : buffer[..length];
            }

            var end = Array.IndexOf(buffer, (byte)'\n', length, read);
            if (end >= 0)
            {
                return buffer[..end];
            }

            length += read;
        }

        return null;
    }

    private static byte[] Line(Action<Utf8JsonWriter> writeFields)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writeFields(writer);
            writer.WriteEndObject();
        }

        output.WriteByte((byte)'\n');
        return output.ToArray();
    }

    private static void WriteNullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }

    /// <summary>A field that must be there, as a string or null.</summary>
    private static bool TryNullable(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var element))
        {
            return false;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return true;
            case JsonValueKind.String:
                value = element.GetString();
                return true;
            default:
                return false;
        }
    }

    private static string? String(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> line) =>
        line.Length > 0 && line[^1] == (byte)'\r' ? line[..^1] : line;
}

/// <summary>One prompt as the helper sends it.</summary>
/// <param name="Token">The session's secret from <see cref="AskPassProtocol.TokenVariable"/>.</param>
/// <param name="Operation">The scope's id from <see cref="AskPassProtocol.OperationVariable"/>.</param>
/// <param name="Prompt">The helper's one argument: the prompt as git or ssh wrote it.</param>
/// <param name="SshPrompt"><c>SSH_ASKPASS_PROMPT</c>, or null when it is not set.</param>
public sealed record AskPassRequest(string Token, string Operation, string Prompt, string? SshPrompt);

/// <summary>One credential request of the helper's credential mode (docs/phase-7a-design.md 4.5).</summary>
/// <param name="Token">The session's secret from <see cref="AskPassProtocol.TokenVariable"/>.</param>
/// <param name="Operation">The scope's id from <see cref="AskPassProtocol.OperationVariable"/>.</param>
/// <param name="Action">git's action: <c>get</c> or <c>erase</c>.</param>
/// <param name="Protocol">git's <c>protocol</c>.</param>
/// <param name="Host">git's <c>host</c> as git wrote it, with a port when the URL has one.</param>
/// <param name="Username">git's <c>username</c>, when it gave one.</param>
/// <param name="SecretHash">For <c>erase</c>: <see cref="AskPassProtocol.SecretHash"/> of the refused password, never the password.</param>
public sealed record AskPassCredentialRequest(string Token, string Operation, string Action, string Protocol, string Host, string? Username, string? SecretHash);

/// <summary>What the helper keeps of git's credential input (4.5). Its text never shows the password.</summary>
/// <param name="Protocol">git's <c>protocol</c>, or null.</param>
/// <param name="Host">git's <c>host</c>, or null.</param>
/// <param name="Username">git's <c>username</c>, or null.</param>
/// <param name="Password">git's <c>password</c> (on <c>store</c> and <c>erase</c>), or null.</param>
public sealed record GitCredentialInput(string? Protocol, string? Host, string? Username, string? Password)
{
    public override string ToString() =>
        $"GitCredentialInput {{ Protocol = {Protocol}, Host = {Host}, Username = {Username}, Password = {(Password is null ? "null" : "***")} }}";
}
