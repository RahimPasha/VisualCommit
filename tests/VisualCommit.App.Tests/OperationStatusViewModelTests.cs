using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>What the status bar shows of a repository's operations (docs/phase-3-design.md 1.9, 4e, D95).</summary>
public class OperationStatusViewModelTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static CancellationToken TestCancelled => TestContext.Current.CancellationToken;

    [Fact]
    public Task It_shows_the_running_text_with_progress_and_cancel_then_the_result_until_the_next_operation() => WithoutContext(async () =>
    {
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        Assert.Equal((OperationStatusViewModel.ReadyText, false, false), (status.Text, status.IsRunning, status.CanCancel));
        var progressed = new TaskCompletionSource();
        var finish = new TaskCompletionSource();

        var fetch = queue.RunAsync(new GitOperation("Fetch", "Fetching from origin…", OperationKind.Network, RefreshScope.None, async context =>
        {
            context.Progress.Report(new OperationProgress("Receiving objects", 45, "Receiving objects:  45% (9/20)"));
            progressed.SetResult();
            await finish.Task;
            return OperationReport.Done("Fetched from origin");
        }));
        await progressed.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.Percent == 45);

        Assert.Equal("Fetching from origin… Receiving objects 45%", status.Text);
        Assert.True(status.IsRunning);
        Assert.False(status.IsIndeterminate);
        Assert.True(status.CanCancel);
        Assert.True(status.CancelCommand.CanExecute(null));

        finish.SetResult();
        await fetch.WaitAsync(Patience, TestCancelled);
        Assert.Equal(("Fetched from origin", OperationStatusTone.Secondary, false, false), (status.Text, status.Tone, status.IsRunning, status.CanCancel));

        await queue.RunAsync(new GitOperation("Merge", "Merging…", OperationKind.Local, RefreshScope.None, _ =>
            Task.FromResult(OperationReport.Conflicts("Merge stopped: 1 file has conflicts"))));
        Assert.Equal(("Merge stopped: 1 file has conflicts", OperationStatusTone.Warning), (status.Text, status.Tone));

        await queue.RunAsync(new GitOperation("Push main", "Pushing…", OperationKind.Network, RefreshScope.None, _ =>
            throw new GitException("git push", 1, " ! [rejected]        main -> main (fetch first)")));
        Assert.Equal(("Push main failed", OperationStatusTone.Danger), (status.Text, status.Tone));
    });

    [Fact]
    public Task Cancel_stops_the_running_network_operation() => WithoutContext(async () =>
    {
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        var started = new TaskCompletionSource();
        var fetch = queue.RunAsync(new GitOperation("Fetch", "Fetching…", OperationKind.Network, RefreshScope.None, async context =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done(string.Empty);
        }));
        await started.Task.WaitAsync(Patience, TestCancelled);
        Assert.True(status.IsIndeterminate);

        status.CancelCommand.Execute(null);

        Assert.Equal(OperationOutcome.Cancelled, (await fetch.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.Equal(("Fetch cancelled", OperationStatusTone.Secondary), (status.Text, status.Tone));
    });

    [Fact]
    public Task A_background_operation_shows_nothing_unless_it_fails_and_what_waits_for_it_says_so() => WithoutContext(async () =>
    {
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        await queue.RunAsync(new GitOperation("Pull", "Pulling…", OperationKind.Network, RefreshScope.None, _ => Task.FromResult(OperationReport.Done("main is already up to date"))));
        var gate = new TaskCompletionSource();

        Assert.True(queue.TryStartInBackground(new GitOperation("Auto-fetch", "Fetching…", OperationKind.Network, RefreshScope.None, async _ =>
        {
            await gate.Task;
            throw new GitException("git fetch --all", 1, "remote: Counting objects: 100% (5/5), done.\nfatal: unable to access 'https://example.com/x.git/': Could not resolve host: example.com");
        })));
        Assert.Equal(("main is already up to date", false), (status.Text, status.IsRunning));

        var stage = queue.RunAsync(new GitOperation("Push", "Pushing…", OperationKind.Network, RefreshScope.None, _ => Task.FromResult(OperationReport.Done("Pushed main to origin/main"))));
        Assert.Equal((OperationStatusViewModel.WaitingForAutoFetchText, true, "1 waiting"), (status.Text, status.HasWaiting, status.WaitingText));
        gate.SetResult();
        await stage.WaitAsync(Patience, TestCancelled);
        Assert.Equal("Pushed main to origin/main", status.Text);

        // On its own, a failed auto-fetch says so, with git's first error line as tooltip.
        var failed = new TaskCompletionSource();
        queue.Finished += (_, _) => failed.TrySetResult();
        Assert.True(queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.None, _ =>
            throw new GitException("git fetch --all", 1, "fatal: unable to access 'https://example.com/x.git/': Could not resolve host: example.com"))));
        await failed.Task.WaitAsync(Patience, TestCancelled);
        await queue.WhenIdleAsync().WaitAsync(Patience, TestCancelled);
        Assert.Equal(
            (OperationStatusViewModel.AutoFetchFailedText, OperationStatusTone.Secondary, "fatal: unable to access 'https://example.com/x.git/': Could not resolve host: example.com"),
            (status.Text, status.Tone, status.ToolTip));
    });

    [Fact]
    public Task The_stage_panels_operations_leave_ready() => WithoutContext(async () =>
    {
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        await queue.RunAsync(new GitOperation("Fetch", "Fetching…", OperationKind.Network, RefreshScope.None, _ => Task.FromResult(OperationReport.Done("Fetched from all remotes"))));

        await queue.RunAsync(new GitOperation("Stage", "Staging…", OperationKind.Local, RefreshScope.None, _ =>
            throw new GitException("git add", 128, "fatal: x"))
        {
            Errors = ErrorDisplay.StagePanel,
        });

        Assert.Equal((OperationStatusViewModel.ReadyText, OperationStatusTone.Secondary), (status.Text, status.Tone));
    });

    [Fact]
    public void A_tab_without_a_repository_is_ready()
    {
        using var status = new OperationStatusViewModel(null);

        Assert.Equal((OperationStatusViewModel.ReadyText, false, false, false), (status.Text, status.IsRunning, status.CanCancel, status.HasWaiting));
    }

    [Fact]
    public Task An_ssh_notice_is_the_text_until_it_goes_and_the_operations_text_comes_back() => WithoutContext(async () =>
    {
        var notices = new PromptNotices();
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue, notices);
        using var empty = new OperationStatusViewModel(null, notices);
        const string Presence = "Confirm user presence for key ED25519-SK SHA256:skokdAxs76eWz";

        var shown = notices.Show(Presence);
        Assert.Equal((Presence, OperationStatusTone.Secondary, Presence), (status.Text, status.Tone, status.ToolTip));
        Assert.Equal(Presence, empty.Text);

        // While git runs, the progress bar and Cancel stay; the notice keeps the text.
        var started = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var fetch = queue.RunAsync(new GitOperation("Fetch", "Fetching from origin…", OperationKind.Network, RefreshScope.None, async _ =>
        {
            started.SetResult();
            await finish.Task;
            return OperationReport.Done("Fetched from origin");
        }));
        await started.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.IsRunning);
        Assert.Equal((Presence, true, true), (status.Text, status.IsRunning, status.CanCancel));

        shown.Dispose();
        Assert.Equal(("Fetching from origin…", true), (status.Text, status.IsRunning));
        Assert.Null(status.ToolTip);
        Assert.Equal(OperationStatusViewModel.ReadyText, empty.Text);

        // A failure's colour comes back too when a notice over it goes.
        finish.SetResult();
        await fetch.WaitAsync(Patience, TestCancelled);
        await queue.RunAsync(new GitOperation("Push main", "Pushing…", OperationKind.Network, RefreshScope.None, _ =>
            throw new GitException("git push", 1, " ! [rejected]        main -> main (fetch first)")));
        using (notices.Show(Presence))
        {
            Assert.Equal((Presence, OperationStatusTone.Secondary), (status.Text, status.Tone));
        }

        Assert.Equal(("Push main failed", OperationStatusTone.Danger), (status.Text, status.Tone));

        // A disposed view model no longer follows the notices.
        status.Dispose();
        using (notices.Show("Another notice"))
        {
            Assert.Equal("Push main failed", status.Text);
        }
    });

    /// <summary>Runs a test where no synchronization context is current, so the view model updates at once.</summary>
    private static Task WithoutContext(Func<Task> test) => Task.Run(test, TestCancelled);

    private static OperationQueue NewQueue() =>
        new("graph", Path.Combine(Path.GetTempPath(), "no-such-git-folder"), () => null, _ => Task.CompletedTask, null, null, NullAppLog.Instance);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var wait = 0; !condition() && wait < 500; wait++)
        {
            await Task.Delay(10, TestCancelled);
        }

        Assert.True(condition());
    }
}
