using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The registry of the session's repository view models by worktree (docs/phase-6-design.md 3.4,
/// 3.6, 4.3, D209): tabs in registration order, summaries with their holders, the idle task a
/// closed queue leaves, and the network queue on a common folder.
/// </summary>
public class OpenRepositoriesTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public void A_tab_is_found_by_its_key_and_by_its_folder_as_the_same_folder()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        using var tab = Full(services, provider.Add("api"));
        var changes = new List<string>();
        registry.Changed += (_, key) => changes.Add(key);

        registry.RegisterTab(tab);

        var key = WorktreeKey.Of(tab.Repository.GitDirectory);
        Assert.Equal([key], changes);
        Assert.Same(tab, registry.TabFor(key));
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            // The platform compares paths ignoring case there (WorktreeKey.Comparer).
            Assert.Same(tab, registry.TabFor(key.ToUpperInvariant()));
        }
        Assert.Same(tab, registry.TabForFolder(tab.Path));
        Assert.Same(tab, registry.TabForFolder(tab.Path + Path.DirectorySeparatorChar));
        Assert.Null(registry.TabForFolder(provider.PathOf("other")));
        Assert.Equal([tab], registry.Tabs);
    }

    [AvaloniaFact]
    public void The_first_registered_tab_serves_the_worktree_and_the_next_when_it_closes()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var repository = provider.Add("api");
        var first = Full(services, repository);
        using var second = Full(services, repository);
        registry.RegisterTab(first);
        registry.RegisterTab(second);
        var key = OpenRepositories.KeyOf(first);
        Assert.Same(first, registry.TabFor(key));

        registry.UnregisterTab(first);
        first.Dispose();

        Assert.Same(second, registry.TabFor(key));
        Assert.True(registry.WhenIdleAsync(key).IsCompleted);
    }

    [AvaloniaFact]
    public async Task Two_holders_share_one_summary_disposed_at_the_last_release()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var repository = provider.Add("api");
        var (web, tools) = (new object(), new object());

        var first = await registry.AcquireSummaryAsync(repository, web, CancellationToken.None);
        var second = await registry.AcquireSummaryAsync(repository, tools, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.True(first.IsSummary);
        Assert.Same(first, Assert.Single(registry.Summaries));
        Assert.True(registry.Holds(first, web) && registry.Holds(first, tools));

        registry.ReleaseSummary(first, web);
        Assert.False(first.IsClosed);
        Assert.Same(first, Assert.Single(registry.Summaries));

        registry.ReleaseSummary(first, tools);
        Assert.True(first.IsClosed);
        Assert.Empty(registry.Summaries);
        Assert.Empty(registry.Closing);
    }

    [AvaloniaFact]
    public async Task An_adoption_takes_the_summary_away_with_its_holders()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var repository = provider.Add("api");
        var holder = new object();
        var summary = (await registry.AcquireSummaryAsync(repository, holder, CancellationToken.None))!;
        var key = OpenRepositories.KeyOf(summary);
        var changes = new List<string>();
        registry.Changed += (_, changed) => changes.Add(changed);

        Assert.Same(summary, registry.TryAdopt(key));

        // Silent: the adopting tab's registration is the change the rows follow (3.5, 4.6).
        Assert.Empty(registry.Summaries);
        Assert.False(registry.Holds(summary, holder));
        Assert.Empty(changes);
        Assert.Null(registry.TryAdopt(key));
        registry.RegisterTab(summary);
        Assert.Equal([key], changes);
        registry.UnregisterTab(summary);

        // A release by the old holder changes nothing now.
        registry.ReleaseSummary(summary, holder);
        Assert.False(summary.IsClosed);
        summary.Dispose();
    }

    [AvaloniaFact]
    public async Task Releasing_the_last_holder_during_a_rows_fetch_cancels_it()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var repository = provider.Add("api");
        var holder = new object();
        var summary = (await registry.AcquireSummaryAsync(repository, holder, CancellationToken.None))!;
        await summary.StartAsync();
        var started = new TaskCompletionSource();
        var fetch = summary.Queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.Refs, async context =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return OperationReport.Done("Fetched from all remotes");
        }));
        await started.Task.WaitAsync(Patience);

        registry.ReleaseSummary(summary, holder);

        var result = await fetch.WaitAsync(Patience);
        Assert.Equal((OperationOutcome.Cancelled, "Fetch cancelled"), (result.Outcome, result.Message));
        Assert.True(summary.IsClosed);
        Assert.Empty(registry.Summaries);
        await DashboardTestSupport.WaitUntilAsync(() => registry.Closing.Count == 0, "the closing summary to go");
    }

    [AvaloniaFact]
    public async Task Releasing_the_last_holder_during_a_local_operation_lets_it_finish_and_keeps_it_closing()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var repository = provider.Add("api");
        var holder = new object();
        var summary = (await registry.AcquireSummaryAsync(repository, holder, CancellationToken.None))!;
        await summary.StartAsync();
        var running = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var pull = summary.Queue.RunAsync(new GitOperation("Pull", "Pulling origin/main into main…", OperationKind.Local, RefreshScope.Refs, async _ =>
        {
            running.SetResult();
            await finish.Task;
            return OperationReport.Done("Pulled 1 commit into main (fast-forward)");
        }));
        await running.Task.WaitAsync(Patience);
        var key = OpenRepositories.KeyOf(summary);

        registry.ReleaseSummary(summary, holder);

        Assert.True(summary.IsClosed);
        Assert.Empty(registry.Summaries);
        Assert.Same(summary, Assert.Single(registry.Closing));
        Assert.Same(summary, registry.ClosedRunningOn(key));
        var idle = registry.WhenIdleAsync(key);
        Assert.False(idle.IsCompleted);

        finish.SetResult();
        Assert.Equal(OperationOutcome.Succeeded, (await pull.WaitAsync(Patience)).Outcome);
        await idle.WaitAsync(Patience);
        await DashboardTestSupport.WaitUntilAsync(() => registry.Closing.Count == 0 && registry.WhenIdleAsync(key).IsCompleted, "the closing summary to go");
        Assert.Null(registry.ClosedRunningOn(key));
    }

    [AvaloniaFact]
    public async Task A_released_summarys_background_fetch_holds_the_worktree_as_a_closed_tabs_does()
    {
        // The lead's ruling at status step 22 (3.4): one rule for a closed tab and a released
        // summary, background operations included: no new queue runs beside a running one.
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var repository = provider.Add("api");
        var holder = new object();
        var summary = (await registry.AcquireSummaryAsync(repository, holder, CancellationToken.None))!;
        await summary.StartAsync();
        var (running, finish) = (new TaskCompletionSource(), new TaskCompletionSource());
        Assert.True(summary.Queue.TryStartInBackground(new GitOperation(AutoFetchScheduler.Title, string.Empty, OperationKind.Network, RefreshScope.None, async _ =>
        {
            // Held past the cancel that the release's dispose sends, as a git process may take its time to end.
            running.SetResult();
            await finish.Task;
            return OperationReport.Done(string.Empty);
        })
        {
            IsBackground = true,
            Prompts = PromptMode.Deny,
        }));
        await running.Task.WaitAsync(Patience);
        var key = OpenRepositories.KeyOf(summary);

        registry.ReleaseSummary(summary, holder);

        Assert.True(summary.IsClosed);
        Assert.Same(summary, Assert.Single(registry.Closing));
        Assert.Same(summary, registry.ClosedRunningOn(key));
        Assert.False(registry.WhenIdleAsync(key).IsCompleted);
        Assert.Same(summary.Queue, registry.NetworkQueueOn(WorktreeKey.Of(repository.CommonDirectory), except: null));

        // A new summary is made only once that queue is idle.
        var acquiring = registry.AcquireSummaryAsync(repository, new object(), CancellationToken.None);
        Assert.False(acquiring.IsCompleted);
        Assert.Empty(registry.Summaries);

        finish.SetResult();
        var next = await acquiring.WaitAsync(Patience);
        Assert.NotNull(next);
        Assert.NotSame(summary, next);
        Assert.True(summary.Queue.Current is null);
        await DashboardTestSupport.WaitUntilAsync(() => registry.Closing.Count == 0 && registry.WhenIdleAsync(key).IsCompleted, "the closing summary to go");
        next.Dispose();
    }

    [AvaloniaFact]
    public async Task When_idle_covers_a_closed_tab_and_an_acquisition_waits_for_it_and_follows_a_tab_registered_meanwhile()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var repository = provider.Add("api");
        var closed = Full(services, repository);
        registry.RegisterTab(closed);
        var running = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var merge = closed.Queue.RunAsync(new GitOperation("Merge", "Merging…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            running.SetResult();
            await finish.Task;
            return OperationReport.Done("Merged");
        }));
        await running.Task.WaitAsync(Patience);
        registry.UnregisterTab(closed);
        closed.Dispose();
        var key = OpenRepositories.KeyOf(closed);
        Assert.False(registry.WhenIdleAsync(key).IsCompleted);

        var acquiring = registry.AcquireSummaryAsync(repository, new object(), CancellationToken.None);
        await DashboardTestSupport.SettleAsync();
        Assert.False(acquiring.IsCompleted);
        Assert.Empty(registry.Summaries);

        using var reopened = Full(services, repository);
        registry.RegisterTab(reopened);
        finish.SetResult();
        await merge.WaitAsync(Patience);

        Assert.Null(await acquiring.WaitAsync(Patience));
        Assert.Empty(registry.Summaries);
        Assert.Same(reopened, registry.TabFor(key));
    }

    [AvaloniaFact]
    public async Task An_acquisition_after_the_idle_task_completed_but_before_its_removal_ran_does_not_wait()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var repository = provider.Add("api");
        var closed = Full(services, repository);
        registry.RegisterTab(closed);
        var running = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var merge = closed.Queue.RunAsync(new GitOperation("Merge", "Merging…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            running.SetResult();
            await finish.Task;
            return OperationReport.Done("Merged");
        }));
        await running.Task.WaitAsync(Patience);
        registry.UnregisterTab(closed);
        closed.Dispose();
        var key = OpenRepositories.KeyOf(closed);

        // The operation ends here, on the UI thread: the kept idle task completes, and its removal
        // is posted to the UI thread, which has not run it yet.
        finish.SetResult();
        Assert.True(registry.WhenIdleAsync(key).IsCompleted, "Setup: the idle task has completed.");
        Assert.NotNull(registry.ClosedRunningOn(key));

        // An acquisition in that window returns at once (it never awaits the completed task in a loop).
        var acquiring = registry.AcquireSummaryAsync(repository, new object(), CancellationToken.None);
        Assert.True(acquiring.IsCompleted);
        var summary = await acquiring;
        Assert.NotNull(summary);
        Assert.Same(summary, Assert.Single(registry.Summaries));

        await merge.WaitAsync(Patience);
        await DashboardTestSupport.WaitUntilAsync(() => registry.ClosedRunningOn(key) is null, "the removal to run");
        summary.Dispose();
    }

    [AvaloniaFact]
    public async Task A_holder_whose_token_is_cancelled_while_it_waits_gets_nothing()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var repository = provider.Add("api");
        var closed = Full(services, repository);
        registry.RegisterTab(closed);
        var running = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var merge = closed.Queue.RunAsync(new GitOperation("Merge", "Merging…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            running.SetResult();
            await finish.Task;
            return OperationReport.Done("Merged");
        }));
        await running.Task.WaitAsync(Patience);
        registry.UnregisterTab(closed);
        closed.Dispose();
        using var lifetime = new CancellationTokenSource();

        var acquiring = registry.AcquireSummaryAsync(repository, new object(), lifetime.Token);
        lifetime.Cancel();

        Assert.Null(await acquiring.WaitAsync(Patience));
        Assert.Empty(registry.Summaries);
        finish.SetResult();
        await merge.WaitAsync(Patience);
    }

    [AvaloniaFact]
    public async Task After_dispose_all_nothing_is_held_and_nothing_can_be_acquired()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var api = (await registry.AcquireSummaryAsync(provider.Add("api"), new object(), CancellationToken.None))!;
        var docs = (await registry.AcquireSummaryAsync(provider.Add("docs"), new object(), CancellationToken.None))!;
        Assert.Equal(2, registry.Summaries.Count);

        registry.DisposeAll();

        Assert.True(api.IsClosed && docs.IsClosed);
        Assert.Empty(registry.Summaries);
        Assert.Null(await registry.AcquireSummaryAsync(provider.Add("web-app"), new object(), CancellationToken.None));
        Assert.Empty(registry.Summaries);
    }

    [AvaloniaFact]
    public async Task The_network_queue_on_a_common_folder_is_a_siblings_or_a_second_tabs_never_the_one_left_out()
    {
        using var provider = new FakeProvider();
        var services = DashboardTestSupport.Services(provider);
        var registry = services.OpenRepositories;
        var main = provider.Add("api");
        var linked = provider.Add("api-feature");
        linked.Common = main.GitDirectory;
        var commonKey = WorktreeKey.Of(main.GitDirectory);
        var tab = Full(services, main);
        registry.RegisterTab(tab);
        var sibling = (await registry.AcquireSummaryAsync(linked, new object(), CancellationToken.None))!;
        using var second = Full(services, main);
        registry.RegisterTab(second);
        Assert.Null(registry.NetworkQueueOn(commonKey, except: null));

        // The sibling worktree's summary fetches.
        var (started, finish) = (new TaskCompletionSource(), new TaskCompletionSource());
        var fetch = sibling.Queue.RunAsync(Network(started, finish));
        await started.Task.WaitAsync(Patience);
        Assert.Same(sibling.Queue, registry.NetworkQueueOn(commonKey, except: tab.Queue));
        Assert.Null(registry.NetworkQueueOn(commonKey, except: sibling.Queue));
        finish.SetResult();
        await fetch.WaitAsync(Patience);
        Assert.Null(registry.NetworkQueueOn(commonKey, except: null));

        // A second tab on the same worktree fetches.
        (started, finish) = (new TaskCompletionSource(), new TaskCompletionSource());
        var other = second.Queue.RunAsync(Network(started, finish));
        await started.Task.WaitAsync(Patience);
        Assert.Same(second.Queue, registry.NetworkQueueOn(commonKey, except: tab.Queue));
        Assert.Null(registry.NetworkQueueOn(WorktreeKey.Of(provider.PathOf("elsewhere")), except: null));
        finish.SetResult();
        await other.WaitAsync(Patience);
        Assert.Null(registry.NetworkQueueOn(commonKey, except: null));
        registry.UnregisterTab(tab);
        tab.Dispose();
        sibling.Dispose();
    }

    private static GitOperation Network(TaskCompletionSource started, TaskCompletionSource finish) =>
        new("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.None, async _ =>
        {
            started.SetResult();
            await finish.Task;
            return OperationReport.Done("Fetched from all remotes");
        });

    private static RepositoryViewModel Full(TabServices services, IGitRepository repository) =>
        new(repository, services.Settings, services.Dates, services.Log, dialogs: services.Dialogs, services: services);
}
