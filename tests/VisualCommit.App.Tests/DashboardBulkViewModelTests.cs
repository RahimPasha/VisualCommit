using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using Xunit;
using static VisualCommit.App.Tests.DashboardTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The dashboard tab's status bar (docs/phase-6-design.md 4.9, 4.13; D208):
/// <see cref="DashboardBulkViewModel"/> as <see cref="IStatusBarOperation"/> from its run: "Ready"
/// before the first, then each of 4.9's texts with its determinate percent and the rows in line as
/// "{w} waiting", Cancel as Cancel all with its tooltip, hidden once clicked, and ssh's notice in
/// place of the text while one shows (the header's run text keeps the run's).
/// </summary>
public class DashboardBulkViewModelTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public async Task Before_the_first_run_the_status_bar_says_ready_and_the_header_nothing()
    {
        using var provider = new BulkProvider();
        provider.Add("api");
        using var host = await BulkHost.StartAsync(provider, ["api"]);
        IStatusBarOperation status = host.Bulk;

        Assert.Equal(("Ready", OperationStatusTone.Secondary, (string?)null), (status.Text, status.Tone, status.ToolTip));
        Assert.Equal((false, false, (int?)null, false, false, 0), (status.IsRunning, status.IsIndeterminate, status.Percent, status.CanCancel, status.HasWaiting, status.Waiting));
        Assert.False(status.CancelCommand.CanExecute(null));
        Assert.Null(host.Bulk.RunText);
        Assert.False(host.Dashboard.ShowsBulkText);
    }

    [AvaloniaFact]
    public async Task During_a_run_the_status_bar_counts_what_ended_and_what_waits_and_cancels_the_rest()
    {
        using var provider = new BulkProvider();
        var gates = new Dictionary<string, TaskCompletionSource>();
        foreach (var name in new[] { "a", "b", "c", "d", "e", "f" })
        {
            provider.Add(name).FetchGate = gates[name] = new TaskCompletionSource();
        }

        using var host = await BulkHost.StartAsync(provider, ["a", "b", "c", "d", "e", "f"]);
        IStatusBarOperation status = host.Bulk;
        var run = host.Bulk.FetchAllAsync();
        await WaitUntilAsync(() => provider.FetchesNow == 4, "four fetches");

        Assert.Equal(("Fetching 6 repositories…", OperationStatusTone.Secondary, "Fetching 6 repositories…"), (status.Text, status.Tone, status.ToolTip));
        Assert.Equal((true, false, (int?)0), (status.IsRunning, status.IsIndeterminate, status.Percent));
        Assert.Equal((2, "2 waiting", true), (status.Waiting, status.WaitingText, status.HasWaiting));
        Assert.Equal((true, "Cancel the rest of Fetch all"), (status.CanCancel, status.CancelToolTip));
        Assert.True(status.CancelCommand.CanExecute(null));
        Assert.True(host.Dashboard.ShowsBulkText);

        gates["a"].SetResult();
        await provider["e"].FetchWaiting.Task.WaitAsync(Patience);
        await SettleAsync();
        Assert.Equal(("Fetching 6 repositories… 1 done", (int?)16, 1, "1 waiting"), (status.Text, status.Percent, status.Waiting, status.WaitingText));

        gates["b"].SetResult();
        await provider["f"].FetchWaiting.Task.WaitAsync(Patience);
        await SettleAsync();
        Assert.Equal(("Fetching 6 repositories… 2 done", (int?)33, 0, false), (status.Text, status.Percent, status.Waiting, status.HasWaiting));

        // Cancel = Cancel all: hidden from the click to the run's end.
        status.CancelCommand.Execute(null);
        Assert.False(status.CanCancel);
        Assert.False(status.CancelCommand.CanExecute(null));
        Assert.StartsWith("Cancelling: waiting for ", status.Text, StringComparison.Ordinal);
        await run.WaitAsync(Patience);
        await SettleAsync();

        Assert.Equal(("Fetched 2 repositories; 4 cancelled", OperationStatusTone.Secondary), (status.Text, status.Tone));
        Assert.Equal((false, (int?)null, false, 0), (status.IsRunning, status.Percent, status.CanCancel, status.Waiting));
        Assert.Equal(status.Text, host.Bulk.RunText);
    }

    [AvaloniaFact]
    public async Task The_results_tone_is_danger_with_a_failure()
    {
        using var provider = new BulkProvider();
        provider.Add("api").FetchFailure = BulkRepository.CannotFetch("../remotes/missing.git");
        using var host = await BulkHost.StartAsync(provider, ["api"]);
        IStatusBarOperation status = host.Bulk;

        await host.Bulk.FetchAllAsync().WaitAsync(Patience);
        await SettleAsync();

        Assert.Equal(("Fetched no repositories; 1 failed", OperationStatusTone.Danger, "Fetched no repositories; 1 failed"), (status.Text, status.Tone, status.ToolTip));
        Assert.Equal(OperationStatusTone.Danger, host.Bulk.RunTone);
    }

    [AvaloniaFact]
    public async Task Sshs_notice_shows_in_place_of_the_text_while_it_lasts_and_the_header_keeps_the_runs()
    {
        using var provider = new BulkProvider();
        provider.Add("api");
        using var host = await BulkHost.StartAsync(provider, ["api"]);
        IStatusBarOperation status = host.Bulk;
        await host.Bulk.FetchAllAsync().WaitAsync(Patience);
        var changed = new List<string?>();
        status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        var notice = host.Services.DialogHost.Notices.Show("Confirm user presence for key ED25519-SK SHA256:abc");
        Assert.Equal(("Confirm user presence for key ED25519-SK SHA256:abc", OperationStatusTone.Secondary, "Confirm user presence for key ED25519-SK SHA256:abc"), (status.Text, status.Tone, status.ToolTip));
        Assert.Equal("Fetched 1 repository", host.Bulk.RunText);
        Assert.Contains(nameof(IStatusBarOperation.Text), changed);

        notice.Dispose();
        Assert.Equal("Fetched 1 repository", status.Text);
    }
}
