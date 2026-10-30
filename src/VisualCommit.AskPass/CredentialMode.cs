using System.IO.Pipes;
using System.Text;

namespace VisualCommit.AskPass;

/// <summary>
/// <c>visualcommit-askpass --vc-credential &lt;action&gt;</c>: the helper as git's credential
/// helper for the URLs bound to a hosting account (docs/phase-7a-design.md 4.5, D255). git runs it
/// through <c>sh</c> with <c>get</c>, <c>store</c> or <c>erase</c> appended and its request on
/// standard input. For <c>get</c> it asks the app over the scope's pipe and prints
/// <c>username=x-access-token</c> and the account's token; for <c>erase</c> it tells the app the
/// SHA-256 of the refused password, never the password (D273); <c>store</c> does nothing. It
/// always exits 0 and writes nothing to standard error, so that git goes on as with a helper that
/// knows nothing. <c>VISUALCOMMIT_ASKPASS_DENY</c> does not stop it: a deny scope's bound URLs are
/// answered too (D274).
/// </summary>
internal static class CredentialMode
{
    public static async Task<int> RunAsync(string? action)
    {
        try
        {
            await AnswerAsync(action).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A broken pipe, an app that went away, a timeout: git hears nothing.
        }

        return 0;
    }

    private static async Task AnswerAsync(string? action)
    {
        var input = await ReadInputAsync().ConfigureAwait(false);
        if (input is null || action is not ("get" or "erase"))
        {
            // store (D273), an action git may add later, too much input: nothing to do.
            return;
        }

        var credential = AskPassProtocol.ParseGitCredentialInput(input);
        var pipeName = Environment.GetEnvironmentVariable(AskPassProtocol.PipeVariable);
        var token = Environment.GetEnvironmentVariable(AskPassProtocol.TokenVariable);
        if (string.IsNullOrEmpty(credential.Protocol)
            || string.IsNullOrEmpty(credential.Host)
            || string.IsNullOrEmpty(pipeName)
            || string.IsNullOrEmpty(token)
            || (action == "erase" && credential.Password is null))
        {
            return;
        }

        var request = new AskPassCredentialRequest(
            token,
            Environment.GetEnvironmentVariable(AskPassProtocol.OperationVariable) ?? string.Empty,
            action,
            credential.Protocol,
            credential.Host,
            credential.Username,
            action == "erase" ? AskPassProtocol.SecretHash(credential.Password!) : null);

        // As a prompt connects (Program.cs): only the user's own app may get the request.
        var options = OperatingSystem.IsWindows() ? PipeOptions.Asynchronous : PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous;
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, options);
        await pipe.ConnectAsync((int)AskPassProtocol.ConnectTimeout.TotalMilliseconds).ConfigureAwait(false);
        if (OperatingSystem.IsWindows() && !WindowsPipeUser.OwnsPipe(pipe))
        {
            return;
        }

        await pipe.WriteAsync(AskPassProtocol.FormatCredentialRequest(request)).ConfigureAwait(false);
        await pipe.FlushAsync().ConfigureAwait(false);

        // The app answers without UI. Through git it never waits for the keychain's first read:
        // nothing is bound before that read ends (the session's GetAsync).
        using var waiting = new CancellationTokenSource(AskPassProtocol.CredentialAnswerTimeout);
        var line = await AskPassProtocol.ReadLineAsync(pipe, waiting.Token).ConfigureAwait(false);
        if (action != "get" || line is null || !AskPassProtocol.TryParseAnswer(line, out var answer))
        {
            return;
        }

        // Never capability[]: git then sends nothing this helper does not read.
        if (AskPassProtocol.CredentialAnswerText(answer) is { } text)
        {
            using var output = Console.OpenStandardOutput();
            output.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));
            output.Flush();
        }
    }

    /// <summary>
    /// Standard input to its end, as UTF-8; null when it holds more than
    /// <see cref="AskPassProtocol.MaxLineBytes"/>, whose rest is still read, so that git's write
    /// never meets a closed pipe.
    /// </summary>
    private static async Task<string?> ReadInputAsync()
    {
        using var input = Console.OpenStandardInput();
        var buffer = new byte[AskPassProtocol.MaxLineBytes];
        var length = 0;
        var tooLong = false;
        var drain = new byte[4096];
        while (true)
        {
            var read = tooLong
                ? await input.ReadAsync(drain).ConfigureAwait(false)
                : await input.ReadAsync(buffer.AsMemory(length)).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (!tooLong)
            {
                length += read;
                if (length == buffer.Length)
                {
                    // Full: one more byte means too much.
                    tooLong = await input.ReadAsync(drain.AsMemory(0, 1)).ConfigureAwait(false) > 0;
                    if (!tooLong)
                    {
                        break;
                    }
                }
            }
        }

        return tooLong ? null : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(buffer, 0, length);
    }
}
