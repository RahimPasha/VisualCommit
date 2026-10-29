using System.Net;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.AskPass;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A dashboard row without a tab and the account (docs/phase-7a-design.md 4.7, 4.8, D332): its
/// view model is in summary mode, whose hosting binds nothing, so a network operation reads the
/// URL rules once and binds the row's remotes through the session; signed out of git nothing is
/// read and the user's own helper answers as before. Real git against <see cref="AuthRemoteServer"/>,
/// which takes the account's token alone, with the user's <c>store</c> helper in the app session's
/// own global file (<see cref="CredentialDashboard"/>).
/// </summary>
public class DashboardCredentialTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);

    [AvaloniaTheory]
    [InlineData(BulkKind.Fetch)]
    [InlineData(BulkKind.Pull)]
    public async Task Rows_without_a_tab_take_the_account_for_a_bulk_run_started_while_they_read_and_for_a_rows_fetch(BulkKind kind)
    {
        await using var setup = await CredentialDashboard.StartAsync(bindsGit: true);
        var dashboard = setup.Dashboard;

        // The run starts while every row is still read: its rows wait for their reads (4.3), and
        // each operation reads the rules once the row's refs are there.
        dashboard.OnBecameActive();
        Assert.True(dashboard.IsPassRunning);
        Assert.All(dashboard.Rows, row => Assert.False(row.HasRepository));
        Assert.True(kind == BulkKind.Fetch ? dashboard.Bulk.CanFetchAll : dashboard.Bulk.CanPullAll);
        await (kind == BulkKind.Fetch ? dashboard.Bulk.FetchAllAsync() : dashboard.Bulk.PullAllAsync()).WaitAsync(Patience);

        var run = dashboard.Bulk.Current!;
        Assert.Equal((2, 0, 0), (run.Succeeded, run.Failed, run.Skipped));
        Assert.All(dashboard.Rows, row => Assert.True(row.Repository is { IsSummary: true }));
        Assert.Equal(2, setup.UrlRulesReads);
        Assert.Equal([setup.Stub.Key, setup.Stub.Key], setup.Stub.Credentials.Gets);

        // A row's own Fetch, outside any run.
        var docs = dashboard.Rows[1];
        await ((IDashboardRowActions)dashboard).FetchAsync(docs).WaitAsync(Patience);

        Assert.Equal(OperationOutcome.Succeeded, docs.Repository!.Queue.LastResult?.Outcome);
        Assert.Equal(3, setup.UrlRulesReads);
        Assert.Equal(3, setup.Stub.Credentials.Gets.Count);
        Assert.Empty(setup.Handler.Prompts);
        Assert.False(File.Exists(setup.UserStore));
        Assert.All(setup.Server.Requests.Where(request => request.UserName is not null), request => Assert.Equal(AskPassProtocol.CredentialUserName, request.UserName));
        setup.AssertTokenNowhere();
    }

    [AvaloniaFact]
    public async Task Without_the_switch_nothing_is_read_and_the_users_helper_answers_and_the_switch_binds_the_next_operation()
    {
        await using var setup = await CredentialDashboard.StartAsync(bindsGit: false);
        setup.Server.Credentials = new NetworkCredential("alice", "alice-secret");
        File.WriteAllText(setup.UserStore, $"http://alice:alice-secret@127.0.0.1:{setup.Server.Port}\n");
        var dashboard = setup.Dashboard;
        dashboard.OnBecameActive();
        await DashboardTestSupport.WaitUntilAsync(() => dashboard.Rows.All(row => row.HasRepository) && !dashboard.IsPassRunning, "the rows' first read");
        var docs = dashboard.Rows[1];

        // BindsGit false: no rules read, the session never asked; the user's helper answers alice's.
        await ((IDashboardRowActions)dashboard).FetchAsync(docs).WaitAsync(Patience);

        Assert.Equal(OperationOutcome.Succeeded, docs.Repository!.Queue.LastResult?.Outcome);
        Assert.Equal(0, setup.UrlRulesReads);
        Assert.Equal(0, setup.Stub.BindingsForCalls);
        Assert.Empty(setup.Stub.Credentials.Gets);
        Assert.Contains(setup.Server.Requests, request => request is { UserName: "alice", Status: 200 });

        // The switch turned on while the dashboard is open: the next operation is bound. The
        // server now takes the token alone, so had the user's helper been asked first, alice's
        // credential would have failed the fetch and git would have erased it from the file.
        setup.Server.Credentials = new NetworkCredential(AskPassProtocol.CredentialUserName, CredentialTestSupport.Token);
        setup.Stub.BindsGit = true;
        var before = setup.Server.Requests.Count;
        await ((IDashboardRowActions)dashboard).FetchAsync(docs).WaitAsync(Patience);

        Assert.Equal(OperationOutcome.Succeeded, docs.Repository!.Queue.LastResult?.Outcome);
        Assert.Equal(1, setup.UrlRulesReads);
        Assert.Equal([setup.Stub.Key], setup.Stub.Credentials.Gets);
        Assert.Contains(setup.Server.Requests.Skip(before), request => request is { UserName: AskPassProtocol.CredentialUserName, Status: 200 });
        Assert.Empty(setup.Handler.Prompts);

        // The user's file holds alice's credential alone, as git stored it again after the first
        // fetch (its own spelling of the port's colon): the bound call neither erased nor stored.
        Assert.StartsWith("http://alice:alice-secret@127.0.0.1", Assert.Single(File.ReadAllLines(setup.UserStore)), StringComparison.Ordinal);
        setup.AssertTokenNowhere();
    }
}
