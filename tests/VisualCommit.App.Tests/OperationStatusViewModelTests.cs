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

        // A success said in the warning colour (docs/phase-4-design.md 10.2): no conflict.
        await queue.RunAsync(new GitOperation("Pull (rebase)", "Pulling…", OperationKind.Local, RefreshScope.None, _ =>
            Task.FromResult(OperationReport.Warning("Pulled and rebased main onto origin/main; git left out 1 commit that origin/main once had"))));
        Assert.Equal(("Pulled and rebased main onto origin/main; git left out 1 commit that origin/main once had", OperationStatusTone.Warning), (status.Text, status.Tone));
        Assert.Equal((OperationOutcome.Succeeded, true), (queue.LastResult!.Outcome, queue.LastResult.IsWarning));

        await queue.RunAsync(new GitOperation("Push main", "Pushing…", OperationKind.Network, RefreshScope.None, _ =>
            throw new GitException("git push", 1, " ! [rejected]        main -> main (fetch first)")));
        Assert.Equal(("Push main failed", OperationStatusTone.Danger), (status.Text, status.Tone));
    });

    [Fact]
    public Task Cancel_says_it_stops_the_merge_tool_too_while_the_merge_tool_runs() => WithoutContext(async () =>
    {
        // docs/phase-4-design.md 11.9: the merge tool's operation is a local one that can be
        // cancelled (D149), and Cancel stops git, its shell and the tool.
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        Assert.Equal(OperationStatusViewModel.StopGitText, status.CancelToolTip);
        var started = new TaskCompletionSource();
        var tool = queue.RunAsync(new GitOperation("Merge tool for notes.txt", "Waiting for fake to close…", OperationKind.Local, RefreshScope.None, async context =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Resolved notes.txt");
        })
        {
            CanBeCancelled = true,
        });
        await started.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.CanCancel);

        Assert.Equal("Stop git and the merge tool", status.CancelToolTip);
        status.CancelCommand.Execute(null);
        Assert.Equal(OperationOutcome.Cancelled, (await tool.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.Equal((false, OperationStatusViewModel.StopGitText), (status.CanCancel, status.CancelToolTip));

        // A network operation's Cancel stops git alone.
        var fetching = new TaskCompletionSource();
        var fetch = queue.RunAsync(new GitOperation("Fetch", "Fetching from origin…", OperationKind.Network, RefreshScope.None, async context =>
        {
            fetching.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Fetched from origin");
        }));
        await fetching.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.CanCancel);
        Assert.Equal("Stop git", status.CancelToolTip);
        status.CancelCommand.Execute(null);
        await fetch.WaitAsync(Patience, TestCancelled);
    });

    [Fact]
    public Task An_operation_of_several_steps_names_the_step_it_is_at() => WithoutContext(async () =>
    {
        // docs/phase-4-design.md 10.1: "Checking out {A}…", then "Rebasing {A} onto {B}…".
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        var checkingOut = new TaskCompletionSource();
        var rebasing = new TaskCompletionSource();
        var next = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        GitOperation? finished = null;
        queue.Finished += (_, e) => finished = e.Operation;

        var run = queue.RunAsync(new GitOperation("Check out feature/other and rebase it onto main", "Checking out feature/other…", OperationKind.Local, RefreshScope.None, async context =>
        {
            context.Progress.Report(new OperationProgress("Updating files", 50, "Updating files:  50% (1/2)"));
            checkingOut.SetResult();
            await next.Task;
            context.SetRunningText("Rebasing feature/other onto main…");
            rebasing.SetResult();
            await finish.Task;
            return OperationReport.Done("Rebased feature/other onto main");
        }));
        await checkingOut.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.Percent == 50);
        Assert.Equal("Checking out feature/other… Updating files 50%", status.Text);

        // The new step's text, without the step before's progress.
        next.SetResult();
        await rebasing.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.Text == "Rebasing feature/other onto main…");
        Assert.Equal(("Rebasing feature/other onto main…", true), (queue.Current!.RunningText, status.IsRunning));
        Assert.Null(status.Percent);

        finish.SetResult();
        await run.WaitAsync(Patience, TestCancelled);
        Assert.Equal("Rebased feature/other onto main", status.Text);

        // The operation itself keeps its texts: its title and the first running text.
        Assert.Equal(("Check out feature/other and rebase it onto main", "Checking out feature/other…"), (finished!.Title, finished.RunningText));
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
    public Task While_an_operation_waits_for_a_stalled_auto_fetch_Cancel_is_offered_and_stops_it() => WithoutContext(async () =>
    {
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        Assert.True(queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.None, async context =>
        {
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done(string.Empty);
        })));
        Assert.False(status.CanCancel);

        var stage = queue.RunAsync(new GitOperation("Stage", "Staging…", OperationKind.Local, RefreshScope.None, _ => Task.FromResult(OperationReport.Done("Staged"))));

        Assert.Equal((OperationStatusViewModel.WaitingForAutoFetchText, true), (status.Text, status.CanCancel));
        Assert.True(status.CancelCommand.CanExecute(null));
        status.CancelCommand.Execute(null);

        Assert.Equal(OperationOutcome.Succeeded, (await stage.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.Equal(("Staged", false), (status.Text, status.CanCancel));
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
    public Task Another_background_operation_never_shows_auto_fetchs_texts() => WithoutContext(async () =>
    {
        // Phase 4's clean-up of the backups (docs/phase-4-design.md section 9) runs in the
        // background too: while it runs, and when it fails, the status bar keeps its text.
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        await queue.RunAsync(new GitOperation("Pull", "Pulling…", OperationKind.Network, RefreshScope.None, _ => Task.FromResult(OperationReport.Done("main is already up to date"))));
        var gate = new TaskCompletionSource();
        Assert.True(queue.TryStartInBackground(new GitOperation("Clean up backups", string.Empty, OperationKind.Local, RefreshScope.None, async _ =>
        {
            await gate.Task;
            return OperationReport.Done(string.Empty);
        })));

        var stage = queue.RunAsync(new GitOperation("Stage", "Staging…", OperationKind.Local, RefreshScope.None, _ => Task.FromResult(OperationReport.Done("Staged"))));
        Assert.Equal(("main is already up to date", false, true, "1 waiting"), (status.Text, status.CanCancel, status.HasWaiting, status.WaitingText));
        gate.SetResult();
        await stage.WaitAsync(Patience, TestCancelled);
        Assert.Equal("Staged", status.Text);

        var failed = new TaskCompletionSource();
        queue.Finished += (_, _) => failed.TrySetResult();
        Assert.True(queue.TryStartInBackground(new GitOperation("Clean up backups", string.Empty, OperationKind.Local, RefreshScope.None, _ =>
            throw new GitException("git update-ref --stdin", 128, "fatal: cannot lock ref 'refs/visualcommit/backup/x'"))));
        await failed.Task.WaitAsync(Patience, TestCancelled);
        await queue.WhenIdleAsync().WaitAsync(Patience, TestCancelled);
        Assert.Equal(("Staged", OperationStatusTone.Secondary, "Staged"), (status.Text, status.Tone, status.ToolTip));
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

    [Fact]
    public Task A_rows_compact_text_has_the_percentage_without_the_stage_and_its_tooltip_the_whole_text() => WithoutContext(async () =>
    {
        // docs/phase-6-design.md 2.6: a dashboard row shows the running text and git's
        // percentage, the result in its tone, or nothing for Ready; never ssh's notice.
        var notices = new PromptNotices();
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue, notices);
        Assert.Equal((string.Empty, OperationStatusTone.Secondary, (string?)null), (status.CompactText, status.CompactTone, status.CompactToolTip));
        var progressed = new TaskCompletionSource();
        var finish = new TaskCompletionSource();

        var fetch = queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.None, async context =>
        {
            context.Progress.Report(new OperationProgress("Counting objects", 45, "remote: Counting objects:  45% (5/11)"));
            progressed.SetResult();
            await finish.Task;
            return OperationReport.Done("Fetched from all remotes");
        }));
        await progressed.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.Percent == 45);
        Assert.Equal(
            ("Fetching from all remotes… 45%", OperationStatusTone.Secondary, "Fetching from all remotes… Counting objects 45%"),
            (status.CompactText, status.CompactTone, status.CompactToolTip));

        // ssh's notice takes the status bar's text, never a row's.
        using (notices.Show("Confirm user presence for key ED25519-SK SHA256:x"))
        {
            Assert.StartsWith("Confirm user presence", status.Text, StringComparison.Ordinal);
            Assert.Equal("Fetching from all remotes… 45%", status.CompactText);
        }

        finish.SetResult();
        await fetch.WaitAsync(Patience, TestCancelled);
        Assert.Equal(("Fetched from all remotes", OperationStatusTone.Secondary, "Fetched from all remotes"), (status.CompactText, status.CompactTone, status.CompactToolTip));

        await queue.RunAsync(new GitOperation("Pull", "Pulling origin/main into main…", OperationKind.Network, RefreshScope.None, _ =>
            Task.FromResult(OperationReport.Conflicts("Pull stopped: 1 file has conflicts"))));
        Assert.Equal(("Pull stopped: 1 file has conflicts", OperationStatusTone.Warning), (status.CompactText, status.CompactTone));

        await queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.None, _ =>
            throw new GitException("git fetch --progress origin", 128, "fatal: '../remotes/missing.git' does not appear to be a git repository")));
        Assert.Equal(("Fetch failed", OperationStatusTone.Danger, "Fetch failed"), (status.CompactText, status.CompactTone, status.CompactToolTip));

        // A stage-panel operation leaves Ready: the row shows nothing.
        await queue.RunAsync(new GitOperation("Stage", "Staging…", OperationKind.Local, RefreshScope.None, _ => Task.FromResult(OperationReport.Done(string.Empty)))
        {
            Errors = ErrorDisplay.StagePanel,
        });
        Assert.Equal((string.Empty, (string?)null), (status.CompactText, status.CompactToolTip));
    });

    [Fact]
    public Task A_rows_compact_text_waits_for_auto_fetch_without_a_tooltip() => WithoutContext(async () =>
    {
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        var gate = new TaskCompletionSource();
        Assert.True(queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.None, async _ =>
        {
            await gate.Task;
            return OperationReport.Done(string.Empty);
        })));
        Assert.Equal(string.Empty, status.CompactText);

        var push = queue.RunAsync(new GitOperation("Push main", "Pushing…", OperationKind.Network, RefreshScope.None, _ => Task.FromResult(OperationReport.Done("Pushed main to origin/main"))));
        Assert.Equal((OperationStatusViewModel.WaitingForAutoFetchText, (string?)null), (status.CompactText, status.CompactToolTip));
        gate.SetResult();
        await push.WaitAsync(Patience, TestCancelled);
    });

    [Fact]
    public Task The_running_title_and_whether_it_could_be_cancelled() => WithoutContext(async () =>
    {
        // docs/phase-6-design.md 2.7: a row's "Cancel {title}" shows for a network operation and
        // the merge tool, enabled while CanCancel, and never for a checkout.
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        Assert.Equal(((string?)null, false), (status.RunningTitle, status.RunningIsCancellable));

        // A pull in its fetch, then past its cancel window.
        var fetching = new TaskCompletionSource();
        var pastFetch = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var pull = queue.RunAsync(new GitOperation("Pull", "Pulling origin/main into main…", OperationKind.Network, RefreshScope.None, async context =>
        {
            fetching.SetResult();
            await pastFetch.Task;
            context.EndCancelWindow();
            await finish.Task;
            return OperationReport.Done("main is already up to date");
        }));
        await fetching.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.CanCancel);
        Assert.Equal(("Pull", true, true), (status.RunningTitle, status.RunningIsCancellable, status.CanCancel));
        pastFetch.SetResult();
        await WaitForAsync(() => !status.CanCancel);
        Assert.Equal(("Pull", true, false), (status.RunningTitle, status.RunningIsCancellable, status.CanCancel));
        finish.SetResult();
        await pull.WaitAsync(Patience, TestCancelled);
        Assert.Equal(((string?)null, false), (status.RunningTitle, status.RunningIsCancellable));

        // The merge tool: a local operation that can be cancelled (D149), also once Cancel was asked.
        var toolStarted = new TaskCompletionSource();
        var tool = queue.RunAsync(new GitOperation("Merge tool for notes.txt", "Waiting for kdiff3 to close…", OperationKind.Local, RefreshScope.None, async context =>
        {
            toolStarted.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Resolved notes.txt");
        })
        {
            CanBeCancelled = true,
        });
        await toolStarted.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.CanCancel);
        Assert.Equal(("Merge tool for notes.txt", true), (status.RunningTitle, status.RunningIsCancellable));
        status.CancelCommand.Execute(null);
        Assert.False(status.CanCancel);
        await tool.WaitAsync(Patience, TestCancelled);

        // A checkout never could be cancelled.
        var checkingOut = new TaskCompletionSource();
        var done = new TaskCompletionSource();
        var checkout = queue.RunAsync(new GitOperation("Check out main", "Checking out main…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            checkingOut.SetResult();
            await done.Task;
            return OperationReport.Done("Checked out main");
        }));
        await checkingOut.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.IsRunning);
        Assert.Equal(("Check out main", false, false), (status.RunningTitle, status.RunningIsCancellable, status.CanCancel));
        done.SetResult();
        await checkout.WaitAsync(Patience, TestCancelled);

        // A background operation is no operation of the user's.
        var gate = new TaskCompletionSource();
        Assert.True(queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.None, async _ =>
        {
            await gate.Task;
            return OperationReport.Done(string.Empty);
        })));
        Assert.Equal(((string?)null, false), (status.RunningTitle, status.RunningIsCancellable));
        gate.SetResult();
        await queue.WhenIdleAsync().WaitAsync(Patience, TestCancelled);
    });

    [Fact]
    public Task A_waiting_operation_the_queue_cancels_says_so_and_leaves_the_merge_tools_cancel_as_it_was() => WithoutContext(async () =>
    {
        // docs/phase-6-design.md 4.5: Cancel all cancels a tab's waiting Fetch merged with a row's;
        // the tab's status bar says "Fetch cancelled", as the row does.
        using var queue = NewQueue();
        using var status = new OperationStatusViewModel(queue);
        var gate = new TaskCompletionSource();
        Assert.True(queue.TryStartInBackground(new GitOperation("Auto-fetch", string.Empty, OperationKind.Network, RefreshScope.None, async _ =>
        {
            await gate.Task;
            return OperationReport.Done(string.Empty);
        })
        {
            IsBackground = true,
        }));
        var fetch = new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.None, _ => Task.FromResult(OperationReport.Done("Fetched from all remotes")));
        var waiting = queue.RunAsync(fetch);
        await WaitForAsync(() => status.Text == OperationStatusViewModel.WaitingForAutoFetchText);

        queue.Cancel(fetch);

        Assert.Equal(OperationOutcome.Cancelled, (await waiting.WaitAsync(Patience, TestCancelled)).Outcome);
        await WaitForAsync(() => status.Text == "Fetch cancelled");
        Assert.Equal(("Fetch cancelled", OperationStatusTone.Secondary), (status.CompactText, status.CompactTone));
        Assert.Equal("Fetch cancelled", queue.LastResult?.Message);
        gate.SetResult();
        await queue.WhenIdleAsync().WaitAsync(Patience, TestCancelled);
        Assert.Equal("Fetch cancelled", status.Text);

        // While the merge tool runs, a waiting operation cancelled before it started leaves the
        // tool's Cancel judged as it was.
        var toolStarted = new TaskCompletionSource();
        var tool = queue.RunAsync(new GitOperation("Merge tool for notes.txt", "Waiting for kdiff3 to close…", OperationKind.Local, RefreshScope.None, async context =>
        {
            toolStarted.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Resolved notes.txt");
        })
        {
            CanBeCancelled = true,
        });
        await toolStarted.Task.WaitAsync(Patience, TestCancelled);
        await WaitForAsync(() => status.CanCancel);
        var stage = new GitOperation("Stage", "Staging…", OperationKind.Local, RefreshScope.None, _ => Task.FromResult(OperationReport.Done("Staged")));
        var staged = queue.RunAsync(stage);
        queue.Cancel(stage);
        Assert.Equal(OperationOutcome.Cancelled, (await staged.WaitAsync(Patience, TestCancelled)).Outcome);
        Assert.Equal(("Merge tool for notes.txt", true, true), (status.RunningTitle, status.RunningIsCancellable, status.CanCancel));
        status.CancelCommand.Execute(null);
        await tool.WaitAsync(Patience, TestCancelled);
        Assert.Equal(((string?)null, false), (status.RunningTitle, status.RunningIsCancellable));
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
