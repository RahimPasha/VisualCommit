using VisualCommit.Core.Operations;

namespace VisualCommit.Testing;

/// <summary>
/// Answers git's and ssh's prompts from a script of answers queued by the test, in place of the
/// app's prompt dialogs (docs/phase-3-design.md 3.5, 7.4): each prompt takes the next answer in
/// line, and every prompt is recorded, in order, for the test to assert. A prompt that finds no
/// answer queued is refused (<see cref="PromptAnswer.Refused"/>), as a dialog closed without an
/// answer would be, so git fails instead of waiting. Inject it through <c>AppSession.Start</c>'s
/// <c>promptHandler</c> (or <c>ShellDriver.Start</c>'s), or hand it to a prompt broker directly.
/// Safe to use from any thread: the broker calls it from the thread pool.
/// </summary>
public sealed class ScriptedPromptHandler : IGitPromptHandler
{
    private readonly Lock _gate = new();
    private readonly Queue<Func<GitPrompt, CancellationToken, Task<PromptAnswer>>> _answers = new();
    private readonly List<GitPrompt> _prompts = [];
    private readonly List<(int Count, TaskCompletionSource Reached)> _waiters = [];

    /// <summary>Every prompt that reached the handler, in order, the refused ones included.</summary>
    public IReadOnlyList<GitPrompt> Prompts
    {
        get
        {
            lock (_gate)
            {
                return [.. _prompts];
            }
        }
    }

    /// <summary>How many queued answers no prompt has taken yet.</summary>
    public int Remaining
    {
        get
        {
            lock (_gate)
            {
                return _answers.Count;
            }
        }
    }

    /// <summary>Queues <paramref name="text"/> as the answer to the next prompt (<see cref="PromptAnswer.Of"/>).</summary>
    public ScriptedPromptHandler Answer(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Then(PromptAnswer.Of(text));
    }

    /// <summary>Queues a Cancel of the user for the next prompt (<see cref="PromptAnswer.Cancelled"/>): the operation then ends as cancelled.</summary>
    public ScriptedPromptHandler Cancel() => Then(PromptAnswer.Cancelled);

    /// <summary>Queues a refusal for the next prompt (<see cref="PromptAnswer.Refused"/>): no answer, and no Cancel of the user.</summary>
    public ScriptedPromptHandler Refuse() => Then(PromptAnswer.Refused);

    /// <summary>Queues <paramref name="answer"/> for the next prompt.</summary>
    public ScriptedPromptHandler Then(PromptAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return Then((_, _) => Task.FromResult(answer));
    }

    /// <summary>
    /// Queues a function that answers the next prompt: for an answer that depends on the prompt,
    /// or one that waits for the test (a prompt held open while the test looks at the app). Its
    /// token is the broker's, cancelled when the prompt's scope ends; an answer that comes after
    /// that is replaced by <see cref="PromptAnswer.Refused"/>.
    /// </summary>
    public ScriptedPromptHandler Then(Func<GitPrompt, CancellationToken, Task<PromptAnswer>> answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        lock (_gate)
        {
            _answers.Enqueue(answer);
        }

        return this;
    }

    /// <summary>Completes when at least <paramref name="count"/> prompts have reached the handler.</summary>
    /// <exception cref="TimeoutException">Fewer came within <paramref name="timeout"/>.</exception>
    public async Task WaitForPromptsAsync(int count, TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        TaskCompletionSource reached;
        lock (_gate)
        {
            if (_prompts.Count >= count)
            {
                return;
            }

            reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((count, reached));
        }

        try
        {
            await reached.Task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Timed out after {timeout.TotalSeconds:0.###} s waiting for {count} prompt(s); {Prompts.Count} came."));
        }
    }

    public async Task<PromptAnswer> AnswerAsync(GitPrompt prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        Func<GitPrompt, CancellationToken, Task<PromptAnswer>>? answer;
        List<TaskCompletionSource> reached = [];
        lock (_gate)
        {
            _prompts.Add(prompt);
            answer = _answers.Count > 0 ? _answers.Dequeue() : null;
            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                if (_prompts.Count >= _waiters[i].Count)
                {
                    reached.Add(_waiters[i].Reached);
                    _waiters.RemoveAt(i);
                }
            }
        }

        foreach (var waiter in reached)
        {
            waiter.TrySetResult();
        }

        if (answer is null || cancellationToken.IsCancellationRequested)
        {
            return PromptAnswer.Refused;
        }

        try
        {
            var given = await answer(prompt, cancellationToken).ConfigureAwait(false);
            return cancellationToken.IsCancellationRequested ? PromptAnswer.Refused : given;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PromptAnswer.Refused;
        }
    }
}
