using System.IO.Pipes;
using System.Text;

namespace VisualCommit.AskPass;

/// <summary>
/// <c>visualcommit-askpass &lt;prompt&gt;</c>: run by git as <c>GIT_ASKPASS</c> and by OpenSSH as
/// <c>SSH_ASKPASS</c> (docs/phase-3-design.md 3.1, D82). It asks the app that started the git
/// process, prints the answer and a line end on standard output and exits 0. It exits 1, printing
/// nothing, when the prompt is refused or cancelled, when it runs in a deny scope, and when the
/// app cannot be reached; git or ssh then fails as it would without an answer.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            return await RunAsync(args).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A broken pipe, an app that went away, a timeout: no answer.
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (Environment.GetEnvironmentVariable(AskPassProtocol.DenyVariable) == "1")
        {
            return 1;
        }

        var pipeName = Environment.GetEnvironmentVariable(AskPassProtocol.PipeVariable);
        var token = Environment.GetEnvironmentVariable(AskPassProtocol.TokenVariable);
        if (string.IsNullOrEmpty(pipeName) || string.IsNullOrEmpty(token))
        {
            return 1;
        }

        var request = new AskPassRequest(
            token,
            Environment.GetEnvironmentVariable(AskPassProtocol.OperationVariable) ?? string.Empty,
            args.Length > 0 ? args[0] : string.Empty,
            Environment.GetEnvironmentVariable(AskPassProtocol.SshPromptVariable));

        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        await pipe.ConnectAsync((int)AskPassProtocol.ConnectTimeout.TotalMilliseconds).ConfigureAwait(false);

        await pipe.WriteAsync(AskPassProtocol.FormatRequest(request)).ConfigureAwait(false);
        await pipe.FlushAsync().ConfigureAwait(false);

        // The answer can take as long as the user takes: no time limit. The app closes the pipe
        // when the operation ends, which ends the read.
        var line = await AskPassProtocol.ReadLineAsync(pipe, CancellationToken.None).ConfigureAwait(false);
        if (line is null || !AskPassProtocol.TryParseAnswer(line, out var answer) || answer is null)
        {
            return 1;
        }

        using var output = Console.OpenStandardOutput();
        output.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(answer + "\n"));
        output.Flush();
        return 0;
    }
}
