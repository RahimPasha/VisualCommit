using VisualCommit.App.Services;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The search for git that the session's services wait on (docs/phase-8a-design.md 1.10), with no
/// session: a listener of <see cref="GitAccess.Changed"/> that throws changes neither how the search
/// ended nor what the other listeners hear, and a fixed search is <see cref="GitAccess.Known"/> as
/// soon as its task has completed.
/// </summary>
public class GitAccessTests
{
    private static readonly GitDetection NotFound = new(GitAvailability.NotFound, null, null, null);

    [Fact]
    public async Task A_listener_that_throws_changes_neither_the_searchs_result_nor_what_the_other_listeners_hear()
    {
        var log = new RecordingLog();
        var access = new GitAccess(new AnsweringProbe(NotFound), () => new GitLookup(null, null), null, log, null, null);
        var heard = 0;
        access.Changed += (_, _) => throw new InvalidOperationException("A listener's fault.");
        access.Changed += (_, _) => heard++;

        var found = await access.DetectAsync(TestContext.Current.CancellationToken);

        Assert.Same(NotFound, found);
        Assert.Same(NotFound, await access.Detection);
        Assert.Same(NotFound, access.Known);
        Assert.Equal(1, heard);
        var entry = Assert.Single(log.Entries);
        Assert.Equal((LogLevel.Error, "A listener of the search for git failed."), (entry.Level, entry.Message));
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task A_cancelled_search_still_ends_cancelled_when_a_listener_throws()
    {
        var access = new GitAccess(new AnsweringProbe(NotFound), () => new GitLookup(null, null), null, new RecordingLog(), null, null);
        access.Changed += (_, _) => throw new InvalidOperationException("A listener's fault.");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => access.DetectAsync(cancellation.Token));

        Assert.True(access.Detection.IsCanceled);
        Assert.Null(access.Known);
    }

    [Fact]
    public void A_fixed_search_is_known_as_soon_as_its_task_has_completed()
    {
        // The task's continuations run later, on the pool: Known must not wait for them.
        var search = new TaskCompletionSource<GitDetection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var access = new GitAccess(search.Task);
        Assert.Null(access.Known);

        search.SetResult(NotFound);

        Assert.Same(NotFound, access.Known);
    }

    [Fact]
    public void A_fixed_search_that_failed_is_not_known()
    {
        var access = new GitAccess(Task.FromException<GitDetection>(new InvalidOperationException("No search.")));

        Assert.Null(access.Known);
    }

    /// <summary>A search for git that answers with one detection, or ends cancelled when its token is.</summary>
    private sealed class AnsweringProbe(GitDetection answer) : IGitSetupProbe
    {
        public Task<GitDetection> DetectAsync(GitLookup lookup, IGitCallLog? callLog, IAppLog log, TimeProvider? recordClock, IReadOnlyDictionary<string, string?>? environment, CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested ? Task.FromCanceled<GitDetection>(cancellationToken) : Task.FromResult(answer);

        public GitGuidance ReadGuidance() => new(GuidancePlatform.Linux, null, null);
    }
}
