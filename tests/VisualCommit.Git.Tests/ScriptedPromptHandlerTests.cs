using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.Git.Tests.RepositoryTestSupport;

namespace VisualCommit.Git.Tests;

/// <summary><see cref="ScriptedPromptHandler"/>: answers from its queue, in order, and a record of every prompt.</summary>
public class ScriptedPromptHandlerTests
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Prompts_take_the_queued_answers_in_order_and_are_recorded()
    {
        var handler = new ScriptedPromptHandler().Answer("alice").Answer("s3cret").Cancel().Refuse();

        var answers = new List<PromptAnswer>();
        foreach (var text in new[] { "Username for 'http://127.0.0.1': ", "Password for 'http://alice@127.0.0.1': ", "Enter passphrase: ", "Other: " })
        {
            answers.Add(await handler.AnswerAsync(Prompt(text), TestCancelled));
        }

        Assert.Equal([PromptAnswer.Of("alice"), PromptAnswer.Of("s3cret"), PromptAnswer.Cancelled, PromptAnswer.Refused], answers);
        Assert.Equal(
            ["Username for 'http://127.0.0.1': ", "Password for 'http://alice@127.0.0.1': ", "Enter passphrase: ", "Other: "],
            handler.Prompts.Select(prompt => prompt.Text));
        Assert.Equal(0, handler.Remaining);
    }

    [Fact]
    public async Task A_prompt_that_finds_no_answer_is_refused_and_still_recorded()
    {
        var handler = new ScriptedPromptHandler();

        var answer = await handler.AnswerAsync(Prompt("Username for 'http://127.0.0.1': "), TestCancelled);

        Assert.Same(PromptAnswer.Refused, answer);
        Assert.Single(handler.Prompts);
    }

    [Fact]
    public async Task A_function_answers_from_the_prompt_and_can_wait_for_the_test()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ScriptedPromptHandler().Then(async (prompt, cancellationToken) =>
        {
            await release.Task.WaitAsync(cancellationToken);
            return PromptAnswer.Of(prompt.Kind == GitPromptKind.Passphrase ? "s3cret" : "?");
        });

        var answering = handler.AnswerAsync(Prompt("Enter passphrase: ", GitPromptKind.Passphrase), TestCancelled);
        await handler.WaitForPromptsAsync(1, StepTimeout);
        Assert.False(answering.IsCompleted);

        release.SetResult();
        Assert.Equal(PromptAnswer.Of("s3cret"), await answering.WaitAsync(StepTimeout, TestCancelled));
    }

    [Fact]
    public async Task A_prompt_whose_scope_ends_while_it_waits_is_refused()
    {
        var handler = new ScriptedPromptHandler().Then(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return PromptAnswer.Of("never");
        });
        using var scope = new CancellationTokenSource();

        var answering = handler.AnswerAsync(Prompt("Enter passphrase: "), scope.Token);
        await scope.CancelAsync();

        Assert.Same(PromptAnswer.Refused, await answering.WaitAsync(StepTimeout, TestCancelled));

        // An answer queued for a prompt whose scope has already ended is used up and not given.
        handler.Answer("too late");
        Assert.Same(PromptAnswer.Refused, await handler.AnswerAsync(Prompt("Enter passphrase: "), scope.Token));
        Assert.Equal(0, handler.Remaining);
    }

    [Fact]
    public async Task Waiting_for_more_prompts_than_come_times_out_with_the_count()
    {
        var handler = new ScriptedPromptHandler();
        await handler.AnswerAsync(Prompt("Other: "), TestCancelled);

        await handler.WaitForPromptsAsync(1, StepTimeout);
        var error = await Assert.ThrowsAsync<TimeoutException>(() => handler.WaitForPromptsAsync(2, TimeSpan.FromMilliseconds(100)));
        Assert.Contains("2 prompt(s); 1 came", error.Message, StringComparison.Ordinal);
    }

    private static GitPrompt Prompt(string text, GitPromptKind kind = GitPromptKind.Other) =>
        new(kind, text, "op-1", "Fetch origin", "repo");
}
