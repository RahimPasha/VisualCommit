using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using Xunit;
using static VisualCommit.App.Tests.DashboardTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// Fetch all and Pull all (docs/phase-6-design.md 4.2 to 4.7, 4.9, 4.12, 4.13; D195, D210, D211)
/// over fake repositories whose fetches and pulls the tests hold: which rows a run acts on and the
/// texts of those it leaves out, the four slots and the common git folder, the rows still being
/// read and those decided again at their turn, Cancel all, a row's own Cancel and
/// <see cref="BulkRun.StopStarting"/>, failures kept in their rows, an abandoned run, removal while
/// a run holds a row, the result texts and the log lines. The runs over real git (the fetch hold,
/// a pull undone from the tab that adopts its row) are in BulkRunTests.Git.cs.
/// </summary>
public partial class BulkRunTests
{
    private static readonly string Upstream = DashboardTestSupport.Upstream;

    [AvaloniaFact]
    public async Task Fetch_all_leaves_out_by_the_toolbars_rule_and_its_rows_in_line_wait_to_fetch()
    {
        using var provider = new BulkProvider();
        var gate = new TaskCompletionSource();
        foreach (var name in new[] { "api", "web-app", "docs", "mobile", "design-system", "infra" })
        {
            provider.Add(name, name == "infra" ? Detached() : null).FetchGate = gate;
        }

        provider.Add("notes", DashboardTestSupport.MainRefs(remote: false));
        using var host = await BulkHost.StartAsync(provider, ["api", "web-app", "docs", "mobile", "design-system", "infra", "notes", "old-site"]);

        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");
        await SettleAsync();

        // Four running in row order, the others with a remote in line; notes left out with the
        // toolbar's reason; old-site by its State, nothing new in its Operation cell.
        Assert.Equal(
            [BulkRowState.Running, BulkRowState.Running, BulkRowState.Running, BulkRowState.Running, BulkRowState.InLine, BulkRowState.InLine, BulkRowState.Skipped, BulkRowState.Skipped],
            host.Bulk.Current!.Rows.Select(row => row.State));
        Assert.Equal(("Fetching from all remotes…", true), (host.Operation("api"), host["api"].ShowsCancel));
        foreach (var waiting in new[] { "design-system", "infra" })
        {
            Assert.Equal(new DashboardCellText("Waiting to fetch…", DashboardTone.Secondary, "Waiting to fetch…"), host[waiting].Operation);
            Assert.False(host[waiting].ShowsCancel);
        }

        Assert.Equal(new DashboardCellText("This repository has no remotes", DashboardTone.Secondary, "This repository has no remotes"), host["notes"].Operation);
        Assert.Equal(("No remote", "Folder not found", string.Empty), (host["notes"].AheadBehind.Text, host["old-site"].State.Text, host.Operation("old-site")));
        Assert.Equal(("Fetching 6 repositories…", 0, "2 waiting"), (host.Bulk.RunText, host.Bulk.Percent, host.Bulk.WaitingText));
        Assert.Contains("Web: Fetch all: started for 6 repositories, 2 skipped", host.Log.Messages);
        Assert.All(new[] { "api", "infra" }, name => Assert.Equal("Wait for Fetch all to finish", host[name].HeldByRunReason));
        Assert.Null(host["notes"].HeldByRunReason);

        gate.SetResult();
        await run.WaitAsync(Patience);
        await SettleAsync();

        Assert.Equal(4, provider.MaxFetchesAtOnce);
        Assert.All(new[] { "api", "web-app", "docs", "mobile", "design-system", "infra" }, name => Assert.Equal("Fetched from all remotes", host.Operation(name)));
        Assert.Equal(("Fetched 6 repositories; 2 skipped", OperationStatusTone.Secondary), (host.Bulk.RunText, host.Bulk.RunTone));
        Assert.Equal("This repository has no remotes", host.Operation("notes"));
        Assert.Contains("Web: Fetch all: Fetched 6 repositories; 2 skipped", host.Log.Messages);
        Assert.All(host.Dashboard.Rows, row => Assert.Null(row.HeldByRunReason));
        Assert.Equal(0, host.Services.BulkSlots.Held);
    }

    [AvaloniaFact]
    public async Task Pull_all_leaves_out_each_row_with_the_first_reason_in_the_toolbars_order()
    {
        using var provider = new BulkProvider();
        provider.Add("docs").Pulled = new PullResult(PullOutcome.FastForward, 4);
        provider.Add("billing", status: WorkingTreeStatus.Clean with { InProgress = new InProgressState(InProgressKind.Merge, Head, "Merge", null, null, 0, false) });
        provider.Add("infra", Detached());
        provider.Add("scratch", Branch("experiment", upstream: false));
        provider.Add("notes", DashboardTestSupport.MainRefs(remote: false));
        provider.Add("legacy", Branch("main", upstream: true, gone: true));
        using var host = await BulkHost.StartAsync(provider, ["docs", "billing", "infra", "scratch", "notes", "legacy"]);

        await host.Bulk.PullAllAsync().WaitAsync(Patience);
        await SettleAsync();

        Assert.Equal("Pulled 4 commits into main (fast-forward)", host.Operation("docs"));
        Assert.Equal(
            ["Finish or abort the merge first", "HEAD is detached: check out a branch to pull", "experiment has no upstream: set one first", "main has no upstream: set one first", "origin/main no longer exists"],
            new[] { "billing", "infra", "scratch", "notes", "legacy" }.Select(host.Operation));
        Assert.All(new[] { "billing", "infra", "scratch", "notes", "legacy" }, name => Assert.Equal((DashboardTone.Secondary, host.Operation(name)), (host[name].Operation.Tone, host[name].Operation.ToolTip)));
        Assert.Equal(("Pulled 1 repository; 5 skipped", OperationStatusTone.Secondary), (host.Bulk.RunText, host.Bulk.RunTone));
        Assert.Equal(1, provider["docs"].Pulls);
        Assert.Equal(1, provider["docs"].Fetches);
        Assert.All(new[] { "billing", "infra", "scratch", "notes", "legacy" }, name => Assert.Equal(0, provider[name].Fetches));
        Assert.Contains("Web: Pull all: started for 1 repository, 5 skipped", host.Log.Messages);
        Assert.Contains("Web: Pull all: Pulled 1 repository; 5 skipped", host.Log.Messages);
    }

    [AvaloniaFact]
    public async Task Two_worktrees_of_one_repository_never_fetch_at_once()
    {
        using var provider = new BulkProvider();
        var (gate, other) = (new TaskCompletionSource(), new TaskCompletionSource());
        var api = provider.Add("api");
        api.FetchGate = gate;
        var worktree = provider.Add("api-wt");
        worktree.Common = api.GitDirectory;
        worktree.FetchGate = other;
        using var host = await BulkHost.StartAsync(provider, ["api", "api-wt"]);

        var run = host.Bulk.FetchAllAsync();
        await api.FetchWaiting.Task.WaitAsync(Patience);
        await SettleAsync();

        Assert.Equal(BulkRowState.InLine, host.Bulk.Current!.Rows[1].State);
        Assert.Equal("Waiting to fetch…", host.Operation("api-wt"));
        Assert.True(host.Services.BulkSlots.IsHeldFor(WorktreeKey.Of(api.GitDirectory)));
        Assert.Equal((1, 0), (api.Fetches, worktree.Fetches));

        gate.SetResult();
        await worktree.FetchWaiting.Task.WaitAsync(Patience);
        Assert.Equal(1, provider.FetchesNow);
        other.SetResult();
        await run.WaitAsync(Patience);

        Assert.Equal(1, provider.MaxFetchesAtOnce);
        Assert.Equal("Fetched 2 repositories", host.Bulk.RunText);
    }

    [AvaloniaFact]
    public async Task A_row_waits_while_another_worktrees_tab_fetches_from_its_common_folder_and_starts_when_that_fetch_ends()
    {
        using var provider = new BulkProvider();
        var docs = provider.Add("docs");
        var sibling = provider.Add("docs-wt");
        sibling.Common = docs.GitDirectory;
        var services = Services(provider, ["docs"]);
        using var tab = await TabAsync(services, sibling);
        var finish = new TaskCompletionSource();
        var tabsFetch = tab.Queue.RunAsync(new GitOperation("Fetch", "Fetching from all remotes…", OperationKind.Network, RefreshScope.None, async context =>
        {
            await finish.Task.WaitAsync(context.CancellationToken);
            return OperationReport.Done("Fetched from all remotes");
        }));
        await WaitUntilAsync(() => tab.Queue.Current is not null, "the tab's fetch");
        using var host = await BulkHost.StartAsync(provider, ["docs"], services: services);

        var run = host.Bulk.FetchAllAsync();
        await SettleAsync();

        Assert.Equal(BulkRowState.InLine, host.Bulk.Current!.Rows[0].State);
        Assert.Equal(("Waiting to fetch…", 0), (host.Operation("docs"), docs.Fetches));
        Assert.Equal(0, host.Services.BulkSlots.Held);

        finish.SetResult();
        await tabsFetch.WaitAsync(Patience);
        await run.WaitAsync(Patience);

        Assert.Equal((1, "Fetched 1 repository"), (docs.Fetches, host.Bulk.RunText));
    }

    [AvaloniaFact]
    public async Task A_row_whose_state_changed_while_it_waited_is_left_out_at_its_turn_and_gives_its_slot_back()
    {
        using var provider = new BulkProvider();
        var gate = new TaskCompletionSource();
        foreach (var name in new[] { "a", "b", "c", "d", "e" })
        {
            provider.Add(name).FetchGate = gate;
        }

        using var host = await BulkHost.StartAsync(provider, ["a", "b", "c", "d", "e"]);
        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");

        // e's remote was removed in a terminal, and its view model read it again.
        provider["e"].Refs = DashboardTestSupport.MainRefs(remote: false);
        await host["e"].Repository!.RefreshAsync();
        Assert.Equal("Waiting to fetch…", host.Operation("e"));

        gate.SetResult();
        await run.WaitAsync(Patience);

        Assert.Equal((BulkRowState.Skipped, "This repository has no remotes"), (host.Bulk.Current!.Rows[4].State, host.Bulk.Current.Rows[4].Reason));
        Assert.Equal(("This repository has no remotes", 0), (host.Operation("e"), provider["e"].Fetches));
        Assert.Equal("Fetched 4 repositories; 1 skipped", host.Bulk.RunText);
    }

    [AvaloniaFact]
    public async Task A_row_in_line_whose_folder_an_F5_read_finds_gone_is_skipped_not_cancelled()
    {
        using var provider = new BulkProvider();
        var gate = new TaskCompletionSource();
        foreach (var name in new[] { "a", "b", "c", "d", "e" })
        {
            provider.Add(name).FetchGate = gate;
        }

        using var host = await BulkHost.StartAsync(provider, ["a", "b", "c", "d", "e"]);
        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");

        host.Gone.Add(provider.PathOf("e"));
        await host.Dashboard.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("Folder not found", host["e"].State.Text);

        gate.SetResult();
        await run.WaitAsync(Patience);

        Assert.Equal(BulkRowState.Skipped, host.Bulk.Current!.Rows[4].State);
        Assert.Equal((string.Empty, 0), (host.Operation("e"), provider["e"].Fetches));
        Assert.Equal("Fetched 4 repositories; 1 skipped", host.Bulk.RunText);
        Assert.Equal(0, host.Bulk.Current.Cancelled);
    }

    [AvaloniaFact]
    public async Task A_row_in_line_whose_last_tab_closes_is_read_again_and_fetched_in_the_background_and_the_run_ends()
    {
        using var provider = new BulkProvider();
        var gate = new TaskCompletionSource();
        foreach (var name in new[] { "a", "b", "c", "d" })
        {
            provider.Add(name).FetchGate = gate;
        }

        var e = provider.Add("e");
        var services = Services(provider, ["a", "b", "c", "d", "e"]);
        var tab = await TabAsync(services, e);
        using var host = await BulkHost.StartAsync(provider, ["a", "b", "c", "d", "e"], services: services, active: false);
        Assert.True(host["e"].FollowsTab);

        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");
        Assert.Equal("Waiting to fetch…", host.Operation("e"));

        // Its tab closes (3.6): the row keeps its values and has no view model at its turn.
        services.OpenRepositories.UnregisterTab(tab);
        tab.Dispose();
        Assert.Null(host["e"].Repository);

        gate.SetResult();
        await run.WaitAsync(Patience);

        Assert.Equal((1, "Fetched from all remotes"), (e.Fetches, host.Operation("e")));
        Assert.False(host["e"].FollowsTab);
        Assert.Equal("Fetched 5 repositories", host.Bulk.RunText);
    }

    [AvaloniaFact]
    public async Task Pull_all_started_while_a_followed_tabs_refs_read_is_held_waits_to_pull_and_decides_the_row_when_it_ends()
    {
        using var provider = new BulkProvider();
        var docs = provider.Add("docs");
        docs.RefsGate = new TaskCompletionSource();
        var services = Services(provider, ["docs"]);
        using var tab = Tab(services, docs);
        var start = tab.StartAsync();
        using var host = await BulkHost.StartAsync(provider, ["docs"], services: services, read: false);
        Assert.True(host["docs"].IsNotReadYet);
        Assert.True(host.Bulk.CanPullAll);

        var run = host.Bulk.PullAllAsync();
        await SettleAsync();
        Assert.Equal((BulkRowState.Reading, "Waiting to pull…"), (host.Bulk.Current!.Rows[0].State, host.Operation("docs")));
        Assert.Equal("Pulling 1 repository…", host.Bulk.RunText);

        docs.RefsGate.SetResult();
        await start.WaitAsync(Patience);
        await run.WaitAsync(Patience);

        Assert.Equal((1, "main is already up to date"), (docs.Pulls, host.Operation("docs")));
        Assert.Equal("Pulled 1 repository", host.Bulk.RunText);
    }

    [AvaloniaFact]
    public async Task With_the_refs_read_ended_and_the_status_read_held_a_merge_in_progress_leaves_the_row_out()
    {
        using var provider = new BulkProvider();
        var docs = provider.Add("docs", status: WorkingTreeStatus.Clean with { InProgress = new InProgressState(InProgressKind.Merge, Head, "Merge", null, null, 0, false) });
        docs.StatusGate = new TaskCompletionSource();
        var services = Services(provider, ["docs"]);
        using var tab = Tab(services, docs);
        var start = tab.StartAsync();
        await WaitUntilAsync(() => tab.Refs is not null, "the tab's refs");
        using var host = await BulkHost.StartAsync(provider, ["docs"], services: services, read: false);

        var run = host.Bulk.PullAllAsync();
        await SettleAsync();
        Assert.Equal("Waiting to pull…", host.Operation("docs"));

        docs.StatusGate.SetResult();
        await start.WaitAsync(Patience);
        await run.WaitAsync(Patience);

        Assert.Equal(("Finish or abort the merge first", 0, 0), (host.Operation("docs"), docs.Fetches, docs.Pulls));
        Assert.Equal("Pulled no repositories; 1 skipped", host.Bulk.RunText);
    }

    [AvaloniaFact]
    public async Task A_followed_tabs_first_read_failing_leaves_the_row_out_and_the_run_ends()
    {
        using var provider = new BulkProvider();
        var docs = provider.Add("docs");
        docs.RefsGate = new TaskCompletionSource();
        docs.RefsFailure = new GitException("git for-each-ref", 128, "fatal: detected dubious ownership in repository\n");
        var services = Services(provider, ["docs"]);
        using var tab = Tab(services, docs);
        var start = tab.StartAsync();
        using var host = await BulkHost.StartAsync(provider, ["docs"], services: services, read: false);

        var run = host.Bulk.PullAllAsync();
        await SettleAsync();
        Assert.Equal("Waiting to pull…", host.Operation("docs"));

        docs.RefsGate.SetResult();
        await start.WaitAsync(Patience);
        await run.WaitAsync(Patience);

        Assert.Equal(("Git could not open it", string.Empty), (host["docs"].State.Text, host.Operation("docs")));
        Assert.Equal(("Pulled no repositories; 1 skipped", 0), (host.Bulk.RunText, docs.Pulls));
    }

    [AvaloniaFact]
    public async Task Rows_whose_folder_checks_are_held_hold_up_no_other_row_and_are_decided_when_they_return_or_cancelled_in_line()
    {
        using var provider = new BulkProvider();
        provider.Add("api");
        provider.Add("docs");
        provider.Add("mobile");
        using var host = await BulkHost.StartAsync(provider, ["api", "docs", "mobile"], read: false);
        var (docsCheck, mobileCheck) = (new TaskCompletionSource(), new TaskCompletionSource());
        host.HeldChecks[provider.PathOf("docs")] = docsCheck;
        host.HeldChecks[provider.PathOf("mobile")] = mobileCheck;
        host.Dashboard.OnBecameActive();
        await WaitUntilAsync(() => host["api"].HasRepository, "api's read");

        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => host.Operation("api") == "Fetched from all remotes", "api's fetch");
        Assert.Equal(("Waiting to fetch…", "Waiting to fetch…", "Fetching 3 repositories… 1 done"), (host.Operation("docs"), host.Operation("mobile"), host.Bulk.RunText));

        // docs' check returns: it is decided, read and fetched.
        docsCheck.SetResult();
        await WaitUntilAsync(() => host.Operation("docs") == "Fetched from all remotes", "docs' fetch");
        Assert.True(host.Bulk.IsRunning);

        // Cancel all ends mobile's wait in line.
        host.Bulk.CancelCommand.Execute(null);
        await run.WaitAsync(Patience);
        Assert.Equal((BulkRowState.CancelledInLine, "Fetch cancelled"), (host.Bulk.Current!.Rows[2].State, host.Operation("mobile")));
        Assert.Equal("Fetched 2 repositories; 1 cancelled", host.Bulk.RunText);
        Assert.Equal(0, provider["mobile"].Fetches);
    }

    [AvaloniaFact]
    public async Task Remove_is_disabled_while_a_run_holds_a_row_and_a_skipped_rows_removal_leaves_the_runs_counts()
    {
        using var provider = new BulkProvider();
        var gate = new TaskCompletionSource();
        foreach (var name in new[] { "a", "b", "c", "d", "e" })
        {
            provider.Add(name).FetchGate = gate;
        }

        provider.Add("notes", DashboardTestSupport.MainRefs(remote: false));
        using var host = await BulkHost.StartAsync(provider, ["a", "b", "c", "d", "e", "notes"]);
        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");

        // Running (a), in line (e): held; skipped (notes): free (4.7).
        foreach (var held in new[] { "a", "e" })
        {
            var state = host.Dashboard.MenuStateOf(host[held]);
            Assert.Equal(("Wait for Fetch all to finish", "Wait for Fetch all to finish"), (state.RemoveReason, state.FetchReason));
            host.Dashboard.SelectedRow = host[held];
            host.Dashboard.RemoveSelected();
            Assert.Contains(host.Dashboard.Rows, row => row.Name == held);
        }

        Assert.Null(host.Dashboard.MenuStateOf(host["notes"]).RemoveReason);
        host.Dashboard.SelectedRow = host["notes"];
        host.Dashboard.RemoveSelected();
        Assert.DoesNotContain(host.Dashboard.Rows, row => row.Name == "notes");
        Assert.Equal("Fetching 5 repositories…", host.Bulk.RunText);

        gate.SetResult();
        await run.WaitAsync(Patience);
        Assert.Equal("Fetched 5 repositories; 1 skipped", host.Bulk.RunText);
        Assert.Null(host.Dashboard.MenuStateOf(host["a"]).RemoveReason);
    }

    [AvaloniaFact]
    public async Task Cancel_all_ends_the_rows_in_line_cancels_the_running_ones_and_waits_for_them()
    {
        using var provider = new BulkProvider();
        var gate = new TaskCompletionSource();
        foreach (var name in new[] { "a", "b", "c", "d", "e", "f" })
        {
            provider.Add(name).FetchGate = gate;
        }

        using var host = await BulkHost.StartAsync(provider, ["a", "b", "c", "d", "e", "f"]);
        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");
        Assert.Equal(("Cancel the rest of Fetch all", true), (host.Bulk.CancelToolTip, host.Bulk.CanCancel));
        var texts = new List<string>();
        host.Bulk.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DashboardBulkViewModel.RunText) && host.Bulk.RunText is { } text && (texts.Count == 0 || texts[^1] != text))
            {
                texts.Add(text);
            }
        };

        host.Bulk.CancelCommand.Execute(null);

        Assert.Equal(["Fetch cancelled", "Fetch cancelled"], new[] { "e", "f" }.Select(host.Operation));
        Assert.False(host.Bulk.CanCancel);
        Assert.Equal("Cancelling: waiting for 4 repositories…", texts[0]);
        await run.WaitAsync(Patience);
        await SettleAsync();

        Assert.All(new[] { "a", "b", "c", "d" }, name => Assert.Equal("Fetch cancelled", host.Operation(name)));
        Assert.Equal(("Fetched no repositories; 6 cancelled", OperationStatusTone.Secondary), (host.Bulk.RunText, host.Bulk.RunTone));
        Assert.Equal("Fetched no repositories; 6 cancelled", texts[^1]);
        Assert.All(texts[..^1], text => Assert.Matches(@"^Cancelling: waiting for [1-4] repositor(y|ies)…$", text));
        Assert.False(host.Bulk.CanCancel);
        Assert.Equal((0, 0), (host.Services.BulkSlots.Held, host.Services.BulkSlots.Waiting));
        Assert.Equal(0, provider["e"].Fetches + provider["f"].Fetches);
    }

    [AvaloniaFact]
    public async Task A_tabs_waiting_fetch_merged_with_a_rows_ends_cancelled_in_the_tab_and_the_row_after_Cancel_all()
    {
        // The tab's toolbar Fetch waits behind its auto-fetch, and the row's merges into it (D123).
        using var provider = new BulkProvider();
        var docs = provider.Add("docs");
        var services = Services(provider, ["docs"]);
        using var tab = await TabAsync(services, docs);
        var hold = new TaskCompletionSource();
        Assert.True(tab.Queue.TryStartInBackground(new GitOperation(AutoFetchScheduler.Title, string.Empty, OperationKind.Network, RefreshScope.None, async _ =>
        {
            await hold.Task;
            return OperationReport.Done(string.Empty);
        })
        {
            IsBackground = true,
            Prompts = PromptMode.Deny,
        }));
        var toolbars = tab.Operations.FetchAllAsync();
        using var host = await BulkHost.StartAsync(provider, ["docs"], services: services);

        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => host.Bulk.Current!.Rows[0].State == BulkRowState.Running, "the row's turn");
        await SettleAsync();
        Assert.Equal(1, tab.Queue.Waiting);

        host.Bulk.CancelCommand.Execute(null);
        await Task.WhenAll(run, toolbars).WaitAsync(Patience);
        await SettleAsync();

        // One git call for both, cancelled before it started: the tab's click and the row's call
        // both end cancelled, and the row says so (the queue records no result for an operation
        // it never started, so the tab's status bar keeps its text).
        Assert.Single(host.Log.Messages, message => message == "docs: Fetch: cancelled before it started.");
        Assert.Equal(("Fetch cancelled", 0), (host.Operation("docs"), docs.Fetches));
        Assert.Equal(OperationOutcome.Cancelled, host.Bulk.Current!.Rows[0].Result?.Outcome);
        Assert.Equal("Fetched no repositories; 1 cancelled", host.Bulk.RunText);
        hold.SetResult();
        await tab.Queue.WhenIdleAsync().WaitAsync(Patience);
    }

    [AvaloniaFact]
    public async Task Stop_starting_ends_the_rows_in_line_lets_the_running_ones_end_and_a_later_run_works()
    {
        using var provider = new BulkProvider();
        var gate = new TaskCompletionSource();
        foreach (var name in new[] { "a", "b", "c", "d", "e", "f" })
        {
            provider.Add(name).FetchGate = gate;
        }

        using var host = await BulkHost.StartAsync(provider, ["a", "b", "c", "d", "e", "f"]);
        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");

        host.Bulk.StopStarting();

        Assert.Equal([BulkRowState.CancelledInLine, BulkRowState.CancelledInLine], host.Bulk.Current!.Rows.Skip(4).Select(row => row.State));
        Assert.Equal(["Fetch cancelled", "Fetch cancelled"], new[] { "e", "f" }.Select(host.Operation));
        Assert.Equal(0, host.Services.BulkSlots.Waiting);
        Assert.True(host.Bulk.CanCancel);
        Assert.Equal("Fetching 6 repositories… 2 done", host.Bulk.RunText);

        gate.SetResult();
        await run.WaitAsync(Patience);
        Assert.Equal("Fetched 4 repositories; 2 cancelled", host.Bulk.RunText);

        await host.Bulk.FetchAllAsync().WaitAsync(Patience);
        Assert.Equal("Fetched 6 repositories", host.Bulk.RunText);
        Assert.Equal(["Fetched from all remotes", "Fetched from all remotes"], new[] { "e", "f" }.Select(host.Operation));
    }

    [AvaloniaFact]
    public async Task Closing_the_dashboard_cancels_its_run_and_gives_its_slots_back()
    {
        using var provider = new BulkProvider();
        var gate = new TaskCompletionSource();
        foreach (var name in new[] { "a", "b", "c", "d", "e" })
        {
            provider.Add(name).FetchGate = gate;
        }

        var host = await BulkHost.StartAsync(provider, ["a", "b", "c", "d", "e"]);
        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");
        var current = host.Bulk.Current!;

        host.Dispose();
        await run.WaitAsync(Patience);

        Assert.True(current.IsAbandoned);
        Assert.Equal((5, 0), (current.Cancelled, current.Succeeded));
        Assert.Equal((0, 0), (host.Services.BulkSlots.Held, host.Services.BulkSlots.Waiting));
        Assert.Equal(0, provider.FetchesNow);
    }

    [AvaloniaFact]
    public async Task An_abandoned_runs_pull_that_fails_after_the_close_opens_its_dialog_in_the_followed_tab_and_gives_its_slot_back()
    {
        using var provider = new BulkProvider();
        var docs = provider.Add("docs");
        docs.PullGate = new TaskCompletionSource();
        docs.PullFailure = BulkRepository.NotFastForward();
        var services = Services(provider, ["docs"]);
        using var tab = await TabAsync(services, docs);
        var host = await BulkHost.StartAsync(provider, ["docs"], services: services);
        using var dialogs = new ScriptedDialogs(services.DialogHost);
        dialogs.Keep();

        var run = host.Bulk.PullAllAsync();
        await docs.PullIntegrating.Task.WaitAsync(Patience);
        host.Dispose();
        docs.PullGate.SetResult();
        await run.WaitAsync(Patience);

        await WaitUntilAsync(() => dialogs.Errors.Count == 1, "the pull's error dialog");
        var error = dialogs.Errors[0];
        Assert.Equal(
            ("Pull failed", "main and origin/main have diverged, and pull.ff is set to only: merge origin/main into main or rebase main onto origin/main.", "docs", "git merge --ff-only --no-overwrite-ignore FETCH_HEAD"),
            (error.Title, error.Summary, error.RepositoryName, error.Command));

        // The run's slot is back while the dialog is open.
        Assert.Same(error, services.DialogHost.Current);
        Assert.Equal(0, services.BulkSlots.Held);
        error.ConfirmCommand.Execute(null);
    }

    [AvaloniaFact]
    public async Task An_abandoned_runs_pull_in_a_released_summary_opens_no_dialog_and_is_logged()
    {
        using var provider = new BulkProvider();
        var docs = provider.Add("docs");
        docs.PullGate = new TaskCompletionSource();
        docs.PullFailure = BulkRepository.NotFastForward();
        var host = await BulkHost.StartAsync(provider, ["docs"]);
        using var dialogs = new ScriptedDialogs(host.Services.DialogHost);
        var summary = host["docs"].Repository!;

        var run = host.Bulk.PullAllAsync();
        await docs.PullIntegrating.Task.WaitAsync(Patience);
        host.Dispose();
        docs.PullGate.SetResult();
        await run.WaitAsync(Patience);
        await summary.Queue.WhenIdleAsync().WaitAsync(Patience);
        await SettleAsync();

        Assert.True(summary.IsClosed);
        Assert.Empty(dialogs.Shown);
        Assert.Contains(host.Log.Messages, message => message.StartsWith("docs: Pull failed after the tab closed: ", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task A_rows_own_Cancel_stops_that_row_only_and_the_next_row_takes_its_slot()
    {
        using var provider = new BulkProvider();
        var gate = new TaskCompletionSource();
        foreach (var name in new[] { "a", "b", "c", "d", "e" })
        {
            provider.Add(name).FetchGate = gate;
        }

        using var host = await BulkHost.StartAsync(provider, ["a", "b", "c", "d", "e"]);
        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");
        await WaitUntilAsync(() => host["a"].ShowsCancel, "a's Cancel");

        host["a"].CancelCommand.Execute(null);
        await provider["e"].FetchWaiting.Task.WaitAsync(Patience);
        await SettleAsync();

        Assert.Equal(("Fetch cancelled", "Fetching from all remotes…"), (host.Operation("a"), host.Operation("e")));
        Assert.Equal("Fetching 5 repositories… 1 done", host.Bulk.RunText);
        Assert.True(host.Bulk.CanCancel);

        gate.SetResult();
        await run.WaitAsync(Patience);
        Assert.Equal("Fetched 4 repositories; 1 cancelled", host.Bulk.RunText);
    }

    [AvaloniaFact]
    public async Task Failures_stay_in_their_rows_with_Details_which_open_the_dialog_as_often_as_asked()
    {
        using var provider = new BulkProvider();
        provider.Add("api").FetchFailure = BulkRepository.CannotFetch("../remotes/missing.git");
        provider.Add("docs");
        using var host = await BulkHost.StartAsync(provider, ["api", "docs"]);
        using var dialogs = new ScriptedDialogs(host.Services.DialogHost);

        await host.Bulk.FetchAllAsync().WaitAsync(Patience);
        await SettleAsync();

        Assert.Empty(dialogs.Shown);
        Assert.Equal(new DashboardCellText("Fetch failed", DashboardTone.Danger, "Fetch failed"), host["api"].Operation);
        Assert.Equal((true, false), (host["api"].ShowsDetails, host["api"].ShowsCancel));
        Assert.True(host.Dashboard.MenuStateOf(host["api"]).HasKeptDialog);
        Assert.False(host["docs"].ShowsDetails);
        Assert.Equal(("Fetched 1 repository; 1 failed", OperationStatusTone.Danger), (host.Bulk.RunText, host.Bulk.RunTone));
        Assert.Equal(OperationStatusTone.Danger, host.Bulk.Tone);

        dialogs.CloseError().CloseError();
        await host["api"].ShowDetailsCommand.ExecuteAsync(null);
        await host["api"].ShowDetailsCommand.ExecuteAsync(null);
        Assert.Equal(2, dialogs.Errors.Count);
        Assert.All(dialogs.Errors, error => Assert.Equal(("Fetch failed", "Git could not fetch from origin.", "api", "git fetch --progress origin"), (error.Title, error.Summary, error.RepositoryName, error.Command)));
        Assert.EndsWith("error: Could not fetch origin", dialogs.Errors[0].Output, StringComparison.Ordinal);
        Assert.True(host["api"].ShowsDetails);

        // The row's own Fetch (4.7): its dialog at once, and the kept one goes as its queue starts it.
        dialogs.CloseError();
        await host["api"].Repository!.Operations.FetchAllAsync();
        Assert.Equal(3, dialogs.Errors.Count);
        Assert.Equal(("Fetch failed", false), (host.Operation("api"), host["api"].ShowsDetails));
        Assert.Null(host["api"].KeptDialog);
        Assert.Equal("Fetched 1 repository; 1 failed", host.Bulk.RunText);
    }

    [AvaloniaFact]
    public async Task A_kept_dialog_goes_when_the_next_run_starts_or_the_rows_view_model_changes()
    {
        using var provider = new BulkProvider();
        var api = provider.Add("api");
        api.FetchFailure = BulkRepository.CannotFetch("../remotes/missing.git");
        var docs = provider.Add("docs");
        docs.FetchFailure = BulkRepository.CannotFetch("../remotes/gone.git");
        var services = Services(provider, ["api", "docs"]);
        var tab = await TabAsync(services, docs);
        using var host = await BulkHost.StartAsync(provider, ["api", "docs"], services: services);

        await host.Bulk.FetchAllAsync().WaitAsync(Patience);
        Assert.Equal((true, true), (host["api"].ShowsDetails, host["docs"].ShowsDetails));

        // docs follows a tab that closes (3.6): its dialog goes with the view model.
        services.OpenRepositories.UnregisterTab(tab);
        tab.Dispose();
        Assert.Null(host["docs"].KeptDialog);

        // The next run starts: what the last one kept goes, and api, fixed, keeps nothing.
        api.FetchFailure = null;
        api.FetchGate = new TaskCompletionSource();
        var run = host.Bulk.FetchAllAsync();
        Assert.Null(host["api"].KeptDialog);
        api.FetchGate.SetResult();
        await run.WaitAsync(Patience);
        Assert.Equal(("Fetched from all remotes", false), (host.Operation("api"), host["api"].ShowsDetails));
    }

    [AvaloniaFact]
    public async Task A_pulls_rebase_stop_with_nothing_to_look_at_keeps_its_notice_for_Details()
    {
        // 4.6: the pull's "Rebase stopped" notice of a stop with nothing to look at (an exec that
        // failed) is deferred as the failures are; the row says the stop in the warning tone.
        using var provider = new BulkProvider();
        var docs = provider.Add("docs");
        docs.Pulled = new PullResult(PullOutcome.Conflicts, 0);
        docs.PullGate = new TaskCompletionSource();
        using var host = await BulkHost.StartAsync(provider, ["docs"]);
        using var dialogs = new ScriptedDialogs(host.Services.DialogHost);

        var run = host.Bulk.PullAllAsync();
        await docs.PullIntegrating.Task.WaitAsync(Patience);
        docs.Status = WorkingTreeStatus.Clean with
        {
            InProgress = new InProgressState(InProgressKind.Rebase, Head, "Local edit", "main", Upstream, 0, false) { StopKind = RebaseStopKind.ExecFailed },
        };
        docs.PullGate.SetResult();
        await run.WaitAsync(Patience);
        await SettleAsync();

        Assert.Empty(dialogs.Shown);
        Assert.Equal(DashboardTone.Warning, host["docs"].Operation.Tone);
        Assert.StartsWith("Rebase stopped at ", host.Operation("docs"), StringComparison.Ordinal);
        Assert.Equal(("Rebase stopped", true), (host["docs"].KeptDialog?.Display?.Title, host["docs"].ShowsDetails));
        Assert.Equal(("Pulled no repositories; 1 stopped", OperationStatusTone.Warning), (host.Bulk.RunText, host.Bulk.RunTone));

        dialogs.CloseError();
        await host["docs"].ShowDetailsCommand.ExecuteAsync(null);
        Assert.Equal("Rebase stopped", Assert.Single(dialogs.Errors).Title);
    }

    [AvaloniaFact]
    public async Task A_stopped_pull_makes_the_result_warning_and_a_failure_danger()
    {
        using var provider = new BulkProvider();
        provider.Add("api").Pulled = new PullResult(PullOutcome.Conflicts, 0);
        provider.Add("docs");
        using var host = await BulkHost.StartAsync(provider, ["api", "docs"]);

        await host.Bulk.PullAllAsync().WaitAsync(Patience);
        Assert.Equal(("Pulled 1 repository; 1 stopped", OperationStatusTone.Warning), (host.Bulk.RunText, host.Bulk.RunTone));

        provider["docs"].PullFailure = new GitException("git merge --no-edit --no-overwrite-ignore FETCH_HEAD", 128, "fatal: refusing to merge unrelated histories\n");
        await host.Bulk.PullAllAsync().WaitAsync(Patience);
        Assert.Equal(("Pulled no repositories; 1 failed; 1 stopped", OperationStatusTone.Danger), (host.Bulk.RunText, host.Bulk.RunTone));
    }

    [Fact]
    public void The_result_says_one_and_several_and_leaves_out_what_is_zero_failures_first()
    {
        Assert.Equal("Fetched 1 repository", OperationTexts.BulkResult(BulkKind.Fetch, 1, 0, 0, 0, 0));
        Assert.Equal("Fetched no repositories", OperationTexts.BulkResult(BulkKind.Fetch, 0, 0, 0, 0, 0));
        Assert.Equal("Fetched 9 repositories; 1 failed", OperationTexts.BulkResult(BulkKind.Fetch, 9, 1, 0, 0, 0));
        Assert.Equal("Pulled 6 repositories; 1 failed; 2 skipped", OperationTexts.BulkResult(BulkKind.Pull, 6, 1, 0, 0, 2));
        Assert.Equal("Pulled 4 repositories; 1 stopped; 3 skipped", OperationTexts.BulkResult(BulkKind.Pull, 4, 0, 1, 0, 3));
        Assert.Equal("Fetched 3 repositories; 5 cancelled; 2 skipped", OperationTexts.BulkResult(BulkKind.Fetch, 3, 0, 0, 5, 2));
        Assert.Equal("Pulled 2 repositories; 2 failed; 2 stopped; 2 cancelled; 2 skipped", OperationTexts.BulkResult(BulkKind.Pull, 2, 2, 2, 2, 2));
        Assert.Equal(("Fetching 1 repository…", "Fetching 10 repositories… 4 done", "Pulling 6 repositories… 1 done"), (OperationTexts.BulkRunning(BulkKind.Fetch, 1, 0), OperationTexts.BulkRunning(BulkKind.Fetch, 10, 4), OperationTexts.BulkRunning(BulkKind.Pull, 6, 1)));
        Assert.Equal(("Cancelling: waiting for 1 repository…", "Cancelling: waiting for 2 repositories…"), (OperationTexts.BulkCancelling(1), OperationTexts.BulkCancelling(2)));
        Assert.Equal(("Waiting to fetch…", "Waiting to pull…"), (OperationTexts.WaitingFor(BulkKind.Fetch), OperationTexts.WaitingFor(BulkKind.Pull)));
        Assert.Equal(("Fetch cancelled", "Pull cancelled"), (OperationTexts.BulkRowCancelled(BulkKind.Fetch), OperationTexts.BulkRowCancelled(BulkKind.Pull)));
        Assert.Equal(("Wait for Fetch all to finish", "Wait for Pull all to finish"), (OperationTexts.WaitForBulkRun(BulkKind.Fetch), OperationTexts.WaitForBulkRun(BulkKind.Pull)));
        Assert.Equal(("Cancel the rest of Fetch all", "Cancel the rest of Pull all"), (OperationTexts.CancelTheRestOf(BulkKind.Fetch), OperationTexts.CancelTheRestOf(BulkKind.Pull)));
        Assert.Equal("Web: Pull all: started for 1 repository, 0 skipped", OperationTexts.BulkStartedLine("Web", BulkKind.Pull, 1, 0));
    }

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static RepoRefs Detached() =>
        new(new HeadState(null, Head), [new GitRef("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, Upstream, RemoteName: "origin")], [], ["origin"]);

    /// <summary>HEAD on <paramref name="name"/> in a repository with the remote <c>origin</c>, tracking <c>origin/{name}</c> or nothing; its upstream gone when <paramref name="gone"/>.</summary>
    private static RepoRefs Branch(string name, bool upstream, bool gone = false) =>
        new(
            new HeadState(name, Head),
            [
                new GitRef($"refs/heads/{name}", name, RefKind.LocalBranch, Head, Upstream: upstream ? $"origin/{name}" : null, IsHead: true, UpstreamRef: upstream ? $"refs/remotes/origin/{name}" : null, UpstreamRemote: upstream ? "origin" : null, UpstreamRemoteRef: upstream ? $"refs/heads/{name}" : null, UpstreamGone: gone),
                .. gone ? Array.Empty<GitRef>() : [new GitRef($"refs/remotes/origin/{name}", $"origin/{name}", RefKind.RemoteBranch, Upstream, RemoteName: "origin")],
            ],
            [],
            ["origin"]);

    private static TabServices Services(BulkProvider provider, string[] names) =>
        DashboardTestSupport.Services(provider, DashboardTestSupport.Store([.. names.Select(provider.PathOf)]), new Testing.ManualTimeProvider(), new RecordingLog());

    /// <summary>A tab's full view model on <paramref name="repository"/>, registered (3.4), not started.</summary>
    private static RepositoryViewModel Tab(TabServices services, IGitRepository repository)
    {
        var tab = new RepositoryViewModel(repository, services.Settings, services.Dates, services.Log, dialogs: services.Dialogs, services: services);
        services.OpenRepositories.RegisterTab(tab);
        return tab;
    }

    /// <summary>A tab's full view model on <paramref name="repository"/>, registered and started.</summary>
    private static async Task<RepositoryViewModel> TabAsync(TabServices services, IGitRepository repository)
    {
        var tab = Tab(services, repository);
        await tab.StartAsync();
        return tab;
    }
}
