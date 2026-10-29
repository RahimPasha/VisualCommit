using VisualCommit.Hosting.Tokens;

namespace VisualCommit.Hosting.Tests.Tokens;

/// <summary>
/// A process runner that runs nothing (<c>MacKeychainStoreTests</c>): it records each call and
/// answers with the next scripted result, or exit code 0 and no output when none is left.
/// </summary>
internal sealed class RecordingProcessRunner : ITokenProcessRunner
{
    private readonly Lock _lock = new();
    private readonly Queue<Func<Call, TokenProcessResult>> _answers = [];
    private readonly List<Call> _calls = [];

    public IReadOnlyList<Call> Calls
    {
        get
        {
            lock (_lock)
            {
                return [.. _calls];
            }
        }
    }

    public RecordingProcessRunner Answer(int? exitCode, string standardOutput = "", string standardError = "") =>
        Answer(_ => new TokenProcessResult(exitCode, standardOutput, standardError));

    public RecordingProcessRunner Answer(Func<Call, TokenProcessResult> answer)
    {
        lock (_lock)
        {
            _answers.Enqueue(answer);
        }

        return this;
    }

    public Task<TokenProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? standardInput, TimeSpan limit, CancellationToken ct)
    {
        var call = new Call(fileName, [.. arguments], standardInput, limit);
        Func<Call, TokenProcessResult>? answer;
        lock (_lock)
        {
            _calls.Add(call);
            _answers.TryDequeue(out answer);
        }

        return Task.FromResult(answer?.Invoke(call) ?? new TokenProcessResult(0, string.Empty, string.Empty));
    }

    public sealed record Call(string FileName, IReadOnlyList<string> Arguments, string? StandardInput, TimeSpan Limit);
}
