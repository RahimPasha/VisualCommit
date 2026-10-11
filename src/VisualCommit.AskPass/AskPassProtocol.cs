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
