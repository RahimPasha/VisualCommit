using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.Git.Tests;

/// <summary>
/// The status bar's offer through the queue (D317, docs/phase-7a-design.md 12.2):
/// <see cref="OperationReport.Offer"/> reaches <see cref="OperationResult.Offer"/> and
/// <see cref="OperationQueue.Finished"/> only for a success; a deduplicated caller shares it.
/// </summary>
public class OperationQueueOfferTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_succeeded_reports_offer_reaches_the_result_and_Finished()
    {
        using var queue = NewQueue();
        var offer = new OperationOffer("Open in browser", () => Task.CompletedTask);
        var finished = new List<OperationResult>();
        queue.Finished += (_, e) => finished.Add(e.Result);

        var result = await queue.RunAsync(Network("Create pull request for topic", _ =>
            Task.FromResult(OperationReport.Done("Created pull request #9") with { Offer = offer }))).WaitAsync(Patience, TestCancelled);

        Assert.Equal(OperationOutcome.Succeeded, result.Outcome);
        Assert.Equal("Created pull request #9", result.Message);
        Assert.Same(offer, result.Offer);
        Assert.Same(offer, Assert.Single(finished).Offer);
        Assert.Same(offer, queue.LastResult?.Offer);
    }

    [Fact]
    public async Task A_failed_a_conflicts_and_a_cancelled_report_carry_no_offer()
    {
        using var queue = NewQueue();
        var offer = new OperationOffer("Open in browser", () => Task.CompletedTask);
        var finished = new List<OperationResult>();
        queue.Finished += (_, e) => finished.Add(e.Result);

        var failed = await queue.RunAsync(Network("Failing", _ => Task.FromResult(
            OperationReport.Failed("Failing failed", new GitFailure(GitFailureKind.Other, "No.", string.Empty, string.Empty, null)) with { Offer = offer })))
            .WaitAsync(Patience, TestCancelled);
        var conflicts = await queue.RunAsync(Network("Conflicting", _ => Task.FromResult(
            OperationReport.Conflicts("Merge stopped: 1 file has conflicts") with { Offer = offer })))
            .WaitAsync(Patience, TestCancelled);
        var cancelled = await queue.RunAsync(Network("Cancelling", _ => Task.FromResult(
            new OperationReport(OperationOutcome.Cancelled, string.Empty) { Offer = offer })))
            .WaitAsync(Patience, TestCancelled);
        var thrown = await queue.RunAsync(Network("Stopped", _ => Task.FromException<OperationReport>(new OperationCanceledException())))
            .WaitAsync(Patience, TestCancelled);

        Assert.Equal(
            [OperationOutcome.Failed, OperationOutcome.Conflicts, OperationOutcome.Cancelled, OperationOutcome.Cancelled],
            new[] { failed, conflicts, cancelled, thrown }.Select(result => result.Outcome));
        Assert.All([failed, conflicts, cancelled, thrown], result => Assert.Null(result.Offer));
        Assert.Equal(4, finished.Count);
        Assert.All(finished, result => Assert.Null(result.Offer));
    }

    [Fact]
    public async Task A_deduplicated_second_caller_gets_the_same_result_with_its_offer()
    {
        using var queue = NewQueue();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var offer = new OperationOffer("Open in browser", () => Task.CompletedTask);
        var runs = 0;
        GitOperation Create() => new("Create", "Creating…", OperationKind.Network, RefreshScope.None, async _ =>
        {
            Interlocked.Increment(ref runs);
            await gate.Task;
            return OperationReport.Done("Created pull request #9") with { Offer = offer };
        })
        { DedupeKey = "create:topic" };

        // A running operation is never shared (D123): the shared one is the waiting one.
        var running = queue.RunAsync(Create());
        var first = queue.RunAsync(Create());
        var second = queue.RunAsync(Create());
        Assert.Same(first, second);
        gate.SetResult();

        var results = await Task.WhenAll(running, first, second).WaitAsync(Patience, TestCancelled);

        Assert.Equal(2, runs);
        Assert.Same(results[1], results[2]);
        Assert.All(results, result => Assert.Same(offer, result.Offer));
    }

    private static GitOperation Network(string title, Func<OperationContext, Task<OperationReport>> run) =>
        new(title, title + "…", OperationKind.Network, RefreshScope.None, run);

    private static OperationQueue NewQueue() => new(
        "graph",
        Path.Combine(Path.GetTempPath(), "no-such-git-folder-" + Guid.NewGuid().ToString("N")),
        () => new Released(),
        _ => Task.CompletedTask,
        null,
        null,
        new ListLog(),
        null);

    private sealed class Released : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
