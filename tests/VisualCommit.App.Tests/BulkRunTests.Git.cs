using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Bulk runs over real git through the app's window (docs/phase-6-design.md 4.13): the fetch hold
/// (17.2, <see cref="FetchHold.HoldIn"/> in the held repositories alone) keeps four of six fetches
/// waiting, and a pull of Pull all is undone from the tab that adopts its row.
/// </summary>
public partial class BulkRunTests
{
    private static readonly TimeSpan GitPatience = TimeSpan.FromSeconds(90);

    [AvaloniaFact]
    public async Task Under_the_fetch_hold_never_more_than_four_fetch_and_a_repository_without_remotes_is_left_out()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("bulk-hold");
        using var hold = FetchHold.Install(data.Path);
        string[] held = ["api", "web-app", "docs", "mobile", "design-system", "billing"];
        hold.HoldIn([.. held.Select(copy.PathOf)]);
        using var app = await OpenAsync(copy, data, [.. held, "notes"]);
        try
        {
            await app.FetchAllAsync();
            await hold.WaitForHeldAsync(4);
            await app.WaitForRowAsync("billing", row => row.Operation.Text == "Waiting to fetch…", GitPatience);
            await DashboardTestSupport.SettleAsync();

            var snapshot = app.DashboardSnapshot();
            Assert.All(held[..4], name => Assert.Equal(("Fetching from all remotes…", true), (snapshot.Row(name).Operation.Text, snapshot.Row(name).ShowsCancel)));
            Assert.All(held[4..], name => Assert.Equal(("Waiting to fetch…", false), (snapshot.Row(name).Operation.Text, snapshot.Row(name).ShowsCancel)));
            Assert.Equal(("This repository has no remotes", "No remote"), (snapshot.Row("notes").Operation.Text, snapshot.Row("notes").AheadBehind.Text));
            Assert.Equal("Fetching 6 repositories…", snapshot.BulkText);
            Assert.Equal(4, hold.Held);

            // An F5 read under the hold: notes is still without a remote and left out, and no
            // fifth fetch started.
            await app.ActiveDashboard!.RefreshCommand.ExecuteAsync(null);
            await DashboardTestSupport.SettleAsync();
            snapshot = app.DashboardSnapshot();
            Assert.Equal(("This repository has no remotes", "No remote"), (snapshot.Row("notes").Operation.Text, snapshot.Row("notes").AheadBehind.Text));
            Assert.Equal(4, hold.Held);
            Assert.Equal(4, app.ActiveDashboard.Bulk.Current!.RunningRows);

            hold.Release();
            var run = await app.WaitForBulkRunAsync(GitPatience);

            snapshot = app.DashboardSnapshot();
            Assert.All(held, name => Assert.Equal("Fetched from all remotes", snapshot.Row(name).Operation.Text));
            Assert.Equal("Fetched 6 repositories; 1 skipped", snapshot.BulkText);
            Assert.Equal((6, 1), (run.Succeeded, run.Skipped));
            Assert.Null(app.Session.Shell.Dialogs.Current);
            Assert.Contains(app.LogLines(), line => line.Contains("Web: Fetch all: started for 6 repositories, 1 skipped", StringComparison.Ordinal));
            Assert.Contains(app.LogLines(), line => line.Contains("Web: Fetch all: Fetched 6 repositories; 1 skipped", StringComparison.Ordinal));
        }
        finally
        {
            hold.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task A_pull_of_Pull_all_is_undone_from_the_tab_that_adopts_its_row()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("bulk-undo");
        using var app = await OpenAsync(copy, data, ["docs"]);
        var docs = copy.Repo("docs");
        var before = await docs.HeadAsync();

        await app.PullAllAsync();
        await app.WaitForBulkRunAsync(GitPatience);
        Assert.Equal("Pulled 4 commits into main (fast-forward)", app.DashboardSnapshot().Row("docs").Operation.Text);
        Assert.NotEqual(before, await docs.HeadAsync());
        var summary = app.ActiveDashboard!.Rows[0].Repository!;

        // The tab adopts the row's summary-mode view model (3.3), whose history recorded the pull (4.11).
        await app.OpenRowAsync("docs");
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.Repository is { HasFirstRead: true, Name: "docs" }, "docs' tab", GitPatience);
        await app.WaitForIdleAsync(timeout: GitPatience);
        var tab = app.Session.Shell.ActiveTab;
        Assert.Same(summary, tab.Repository);
        Assert.True(tab.Toolbar.CanUndo);
        Assert.StartsWith("Undo pull into main", tab.Toolbar.UndoToolTip, StringComparison.Ordinal);

        await tab.Toolbar.UndoCommand.ExecuteAsync(null);
        await app.WaitForIdleAsync(timeout: GitPatience);

        Assert.Equal("Undid pull into main", tab.OperationStatus.Text);
        Assert.Equal(before, await docs.HeadAsync());
    }

    [AvaloniaFact]
    public async Task The_sign_in_prompts_of_one_run_carry_its_prompt_group()
    {
        // 4.4: the run's group reaches every prompt of its rows' fetches (the prompt dialogs answer
        // a group's same prompt once, PromptDialogsTests'); a fetch outside a run has none.
        using var copy = await Scenarios.WorkspaceAsync();
        await using var server = await AuthRemoteServer.StartForFolderAsync(Path.GetDirectoryName(copy.RemoteOf("docs"))!);
        server.RequireAuth = true;
        foreach (var name in new[] { "docs", "mobile" })
        {
            await copy.Repo(name).GitAsync("remote", "set-url", "origin", server.UrlOf(name));
        }

        var prompts = new SignIn(server.Credentials.UserName, server.Credentials.Password);
        using var data = new TempDirectory("bulk-prompts");
        using var app = await OpenAsync(copy, data, ["docs", "mobile"], prompts);

        await app.FetchAllAsync();
        var run = await app.WaitForBulkRunAsync(GitPatience);

        Assert.Equal("Fetched 2 repositories", app.DashboardSnapshot().BulkText);
        Assert.Equal(["docs", "docs", "mobile", "mobile"], prompts.Prompts.Select(prompt => prompt.RepositoryName).Order(StringComparer.Ordinal));
        Assert.All(prompts.Prompts, prompt => Assert.Same(run.PromptGroup, prompt.Group));
        Assert.True(run.PromptGroup.IsEnded);

        await app.ActiveDashboard!.Rows[0].Repository!.Operations.FetchAllAsync();
        Assert.Equal(6, prompts.Prompts.Count);
        Assert.All(prompts.Prompts.Skip(4), prompt => Assert.Null(prompt.Group));
    }

    /// <summary>The app on <paramref name="copy"/>'s workspace "Web" of <paramref name="names"/>, its dashboard open and read.</summary>
    private static async Task<ShellDriver> OpenAsync(TempWorkspace copy, TempDirectory data, string[] names, Core.Operations.IGitPromptHandler? prompts = null)
    {
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [new Workspace("web", "Web", [.. names.Select(name => new WorkspaceRepository(copy.PathOf(name)))])] });
        var app = ShellDriver.Start(data.Path, 1100, 700, prompts);
        await app.WaitUntilReadyAsync();
        var mark = app.LogMark();
        app.Session.Shell.ShowWorkspace("web", app.Session.Shell.ActiveTab);
        await app.WaitForDashboardReadAsync(mark, GitPatience);
        await app.WaitForAsync(() => app.ActiveDashboard!.Rows.All(row => row.Repository is not { } repository || repository.Queue.Current is null), "every row's queue idle", GitPatience);
        return app;
    }

    /// <summary>Answers git's user name and password prompts with the server's credentials, and records every prompt.</summary>
    private sealed class SignIn(string user, string password) : Core.Operations.IGitPromptHandler
    {
        private readonly List<Core.Operations.GitPrompt> _prompts = [];

        public IReadOnlyList<Core.Operations.GitPrompt> Prompts
        {
            get
            {
                lock (_prompts)
                {
                    return [.. _prompts];
                }
            }
        }

        public Task<Core.Operations.PromptAnswer> AnswerAsync(Core.Operations.GitPrompt prompt, CancellationToken cancellationToken)
        {
            lock (_prompts)
            {
                _prompts.Add(prompt);
            }

            return Task.FromResult(prompt.Kind switch
            {
                Core.Operations.GitPromptKind.Username => Core.Operations.PromptAnswer.Of(user),
                Core.Operations.GitPromptKind.Password => Core.Operations.PromptAnswer.Of(password),
                _ => Core.Operations.PromptAnswer.Refused,
            });
        }
    }
}
