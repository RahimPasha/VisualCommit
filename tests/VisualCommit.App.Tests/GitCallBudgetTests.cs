using System.Diagnostics;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// S8's counts of git calls (docs/phase-7a-design.md 5.3, D280, D281): <see cref="GitCallBudget"/>'s
/// whole refreshes, its optional form and its budgets, tried on calls made up here; and the
/// operations harness's quiet mark and read around the watcher's refresh of a real repository.
/// </summary>
public class GitCallBudgetTests
{
    private const string StatusRead = "git status --porcelain=v2 -z --untracked-files=all --renames";

    /// <summary>One refresh's calls outside every operation, in an order of their own: the forms are matched, not the order.</summary>
    private static readonly GitCallRecord[] Refresh =
    [
        Call("git stash list --no-show-signature --no-color --encoding=UTF-8 --format=%gd"),
        Call("git for-each-ref --format=%(refname) refs/heads refs/remotes refs/tags"),
        Call("git rev-parse -q --verify HEAD"),
        Call("git config --null --get-regexp ^remote\\."),
        Call("git symbolic-ref -q HEAD"),
        Call(StatusRead),
    ];

    /// <summary>
    /// Part S's rule for calls besides a step's own (its conventions' "The screen's reads"): only
    /// whole refreshes outside every operation, the refs read's five forms and the status, each as
    /// often as the others. Moved from <c>Phase6SupportTests</c>.
    /// </summary>
    [Fact]
    public void Calls_besides_a_steps_own_pass_only_as_whole_refreshes_outside_every_operation()
    {
        Assert.Null(GitCallBudget.NotWholeRefreshes([]));
        Assert.Null(GitCallBudget.NotWholeRefreshes(Refresh));
        Assert.Null(GitCallBudget.NotWholeRefreshes([.. Refresh, .. Enumerable.Reverse(Refresh)]));
        Assert.Contains("not whole refreshes", GitCallBudget.NotWholeRefreshes(Refresh[..5]), StringComparison.Ordinal);
        Assert.Contains("not whole refreshes", GitCallBudget.NotWholeRefreshes([.. Refresh, Refresh[5]]), StringComparison.Ordinal);
        Assert.Contains("git config --show-scope --show-origin -z --list is no refresh's call", GitCallBudget.NotWholeRefreshes([.. Refresh, Call("git config --show-scope --show-origin -z --list")]), StringComparison.Ordinal);
        Assert.Contains("(in op-3) is no refresh's call", GitCallBudget.NotWholeRefreshes([.. Refresh[..5], Call(Refresh[5].CommandText, "op-3")]), StringComparison.Ordinal);
    }

    /// <summary>An optional form (D281) passes counted 0 times or as often as the others, and fails counted 1 time in 2 refreshes or alone.</summary>
    [Fact]
    public void An_optional_form_is_whole_counted_never_or_as_often_as_the_others()
    {
        const string rewritesRead = "git config --null --get-regexp ^url\\.";
        RefreshForm[] forms = [.. GitCallBudget.RefreshForms, new(rewritesRead, text => text == rewritesRead, Optional: true)];
        var rewrites = Call(rewritesRead);

        Assert.Null(GitCallBudget.NotWholeRefreshes(Refresh, forms));
        Assert.Null(GitCallBudget.NotWholeRefreshes([.. Refresh, .. Refresh], forms));
        Assert.Null(GitCallBudget.NotWholeRefreshes([.. Refresh, rewrites], forms));
        Assert.Null(GitCallBudget.NotWholeRefreshes([.. Refresh, rewrites, .. Refresh, rewrites], forms));
        Assert.Contains("not whole refreshes", GitCallBudget.NotWholeRefreshes([.. Refresh, rewrites, .. Refresh], forms), StringComparison.Ordinal);
        Assert.Contains("not whole refreshes", GitCallBudget.NotWholeRefreshes([rewrites], forms), StringComparison.Ordinal);

        // Without the optional form the read is no refresh's call.
        Assert.Contains($"{rewritesRead} is no refresh's call", GitCallBudget.NotWholeRefreshes([.. Refresh, rewrites]), StringComparison.Ordinal);
    }

    [Fact]
    public void A_budget_passes_at_its_count_and_fails_above_it_with_every_call()
    {
        GitCallRecord[] calls = [Call("git rev-parse --git-dir"), Call("git merge-base --is-ancestor a b", "op-2"), Call("git rev-list --count a..b")];

        GitCallBudget.AssertAtMost(calls[..2], 2, "the step");
        GitCallBudget.AssertAtMost(calls[..2].Select(call => call.CommandText).ToList(), 2, "the step");
        var failure = Assert.Throws<GitCallBudgetException>(() => GitCallBudget.AssertAtMost(calls, 2, "the step"));
        Assert.StartsWith("the step: budget 2, used 3. The calls:", failure.Message, StringComparison.Ordinal);
        Assert.All(calls, call => Assert.Contains(call.CommandText, failure.Message, StringComparison.Ordinal));
        Assert.Contains("(in op-2)", failure.Message, StringComparison.Ordinal);

        failure = Assert.Throws<GitCallBudgetException>(() => GitCallBudget.AssertAtMost(calls.Select(call => call.CommandText).ToList(), 2, "the refs read"));
        Assert.StartsWith("the refs read: budget 2, used 3. The calls:", failure.Message, StringComparison.Ordinal);
        Assert.All(calls, call => Assert.Contains(call.CommandText, failure.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Whole_refreshes_set_aside_are_not_counted_and_a_lone_status_read_fails()
    {
        var own = Call("git rev-parse --git-dir");

        GitCallBudget.AssertAtMost([.. Refresh, own, .. Refresh], 1, "the step", setAsideWholeRefreshes: true);
        var failure = Assert.Throws<GitCallBudgetException>(() => GitCallBudget.AssertAtMost([.. Refresh, own], 1, "the step"));
        Assert.StartsWith("the step: budget 1, used 7.", failure.Message, StringComparison.Ordinal);

        failure = Assert.Throws<GitCallBudgetException>(() => GitCallBudget.AssertAtMost([Call(StatusRead), own], 1, "the step", setAsideWholeRefreshes: true));
        Assert.StartsWith("the step made git calls that are no whole refresh (the refresh calls are not whole refreshes:", failure.Message, StringComparison.Ordinal);

        // A refresh form inside an operation is the operation's call: counted, never set aside.
        failure = Assert.Throws<GitCallBudgetException>(() => GitCallBudget.AssertAtMost([Call(StatusRead, "op-4")], 0, "the step", setAsideWholeRefreshes: true));
        Assert.StartsWith("the step: budget 0, used 1.", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_whole_refreshes_pass_and_a_failure_prints_every_call()
    {
        GitCallBudget.AssertOnlyWholeRefreshes([], "the step");
        GitCallBudget.AssertOnlyWholeRefreshes([.. Refresh, .. Refresh], "the step");
        var failure = Assert.Throws<GitCallBudgetException>(() => GitCallBudget.AssertOnlyWholeRefreshes([.. Refresh, Call("git worktree list --porcelain")], "the step"));
        Assert.StartsWith("the step made git calls that are no whole refresh (git worktree list --porcelain is no refresh's call):", failure.Message, StringComparison.Ordinal);
        Assert.All(Refresh, call => Assert.Contains(call.CommandText, failure.Message, StringComparison.Ordinal));
    }

    /// <summary>
    /// The harness's quiet mark and read (5.3's rule 5): a write of git config outside the app
    /// starts the watcher's refresh, which awaits its status read last; read with the watcher
    /// quiet again, the calls are whole refreshes. The opening's clean-up reads its backups
    /// outside every operation once the first load is done; the mark waits for that read too, as
    /// <see cref="ShellDriver.WaitForTabOpenedAsync"/> does.
    /// </summary>
    [Fact]
    public async Task The_harness_reads_the_watchers_refresh_after_a_write_to_the_git_config_as_whole_refreshes()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var harness = await OperationsHarness.OpenAsync(repo);
        await WaitForCallAsync(harness, 0, call => call.CommandText == ShellDriver.OpeningCleanUpRead, "the opening's clean-up's read of the backups");

        var mark = await harness.QuietCallMarkAsync();
        await harness.Repo.ConfigAsync("pull.ff", "true");
        await WaitForCallAsync(harness, mark, call => call.CommandText == StatusRead, "a status read after the write to .git/config");

        var calls = await harness.QuietCallsSinceAsync(mark);
        Assert.True(calls.Count >= GitCallBudget.RefreshForms.Count, "The watcher's refresh made fewer calls than a refresh's forms:\n  " + string.Join("\n  ", calls.Select(call => call.CommandText)));
        GitCallBudget.AssertOnlyWholeRefreshes(calls, "the watcher's refresh after a write to .git/config");
    }

    /// <summary>Waits, polling the harness's calls every 15 ms for at most <see cref="ShellDriver.DefaultWaitTimeout"/>, for a call after <paramref name="sequence"/> that <paramref name="matches"/>.</summary>
    private static async Task WaitForCallAsync(OperationsHarness harness, long sequence, Func<GitCallRecord, bool> matches, string what)
    {
        var waited = Stopwatch.StartNew();
        while (!harness.Calls.Snapshot().Any(call => call.Sequence > sequence && matches(call)))
        {
            Assert.True(waited.Elapsed < ShellDriver.DefaultWaitTimeout, $"Timed out after {ShellDriver.DefaultWaitTimeout.TotalSeconds:F0} s waiting for {what}.");
            await Task.Delay(15, TestContext.Current.CancellationToken);
        }
    }

    private static GitCallRecord Call(string command, string? operation = null) =>
        new(DateTimeOffset.UnixEpoch, TimeSpan.Zero, command, "/repo", GitCallOutcome.Completed, 0, string.Empty, string.Empty, operation);
}
