using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The lead's hooks of phase 4 (docs/phase-4-design.md 2.2, 11.1, 11.2) in the whole app on real
/// repositories: the resolver opened from a conflicted file across the graph and right-panel
/// columns with the right panel hidden, the interactive list in place of the graph, and where the
/// focus goes when each closes; a list whose Start waits in the queue cannot be left, and the
/// repository starts and closes only the list it shows.
/// </summary>
public class GraphAreaHooksTests
{
    [AvaloniaFact]
    public async Task A_conflicted_file_opens_the_resolver_across_the_graph_and_right_panel_columns_and_closing_it_focuses_the_graph()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.ConflictsStoppedAsync(Scenarios.ConflictsStop.Merge);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitForRepositoryAsync();
        var tab = app.Session.Shell.ActiveTab;
        var repository = tab.Repository!;
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read", TimeSpan.FromSeconds(30));

        repository.IsWorkingRowSelected = true;
        var file = Assert.Single(repository.Status.Unstaged, candidate => candidate.Kind == FileChangeKind.Conflicted);
        repository.OpenDiff(file, staged: false);
        await app.WaitForResolverAsync("notes.txt");

        Assert.True(repository.IsResolverOpen);
        Assert.False(repository.IsDiffOpen);
        Assert.False(repository.ShowsGraph);
        Assert.False(tab.ShowsRightPanel);
        Assert.False(tab.ShowsStagePanel);
        Assert.Equal(2, tab.GraphColumnSpan);
        var bounds = app.BoundsOf(app.ResolverView);
        Assert.Equal(260, bounds.X, 1);
        Assert.Equal(840, bounds.Width, 1);
        Assert.False(app.Window.GetVisualDescendants().OfType<GridSplitter>().Single(splitter => splitter.Name == "RightSplitter").IsEffectivelyVisible);

        repository.Resolver.CloseResolverCommand.Execute(null);
        await app.WaitForResolverClosedAsync();
        await app.WaitForAsync(
            () => app.Window.FocusManager?.GetFocusedElement() is CommitGraphControl,
            "the focus on the graph (the banner's Continue is disabled while the file conflicts)",
            TimeSpan.FromSeconds(10));

        Assert.False(repository.IsResolverOpen);
        Assert.True(repository.ShowsGraph);
        Assert.True(tab.ShowsStagePanel);
        Assert.Equal(1, tab.GraphColumnSpan);
        Assert.True(app.Window.GetVisualDescendants().OfType<GridSplitter>().Single(splitter => splitter.Name == "RightSplitter").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Opening_a_repository_cleans_up_its_old_backups_in_the_background_and_keeps_the_new_ones()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.InteractiveAsync();
        await repo.GitAsync("update-ref", "refs/visualcommit/backup/reset-20250101T120000000Z/main", "HEAD");
        var recent = "refs/visualcommit/backup/reset-" + DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture) + "/main";
        await repo.GitAsync("update-ref", recent, "HEAD");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitForRepositoryAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;

        await app.WaitForAsync(
            () => !Task.Run(() => repo.GitAsync("for-each-ref", "--format=%(refname)", "refs/visualcommit/backup/")).GetAwaiter().GetResult().StandardOutput.Contains("20250101", StringComparison.Ordinal),
            "the backup of 2025-01-01 removed",
            TimeSpan.FromSeconds(30));
        await app.WaitForAsync(() => repository.Queue.Current is null, "the clean-up ended", TimeSpan.FromSeconds(30));

        var left = (await repo.GitAsync("for-each-ref", "--format=%(refname)", "refs/visualcommit/backup/")).StandardOutput.Trim();
        Assert.Equal(recent, left);
        Assert.Equal("Ready", repository.OperationStatus.Text);
    }

    [AvaloniaFact]
    public async Task The_interactive_list_replaces_the_graph_and_Cancel_gives_the_focus_back_to_the_graph()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.InteractiveAsync();
        var baseSha = (await repo.GitAsync("rev-parse", "main~6")).StandardOutput.Trim();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitForRepositoryAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;

        var list = await repository.Repository.ReadRebaseListAsync(baseSha, "refs/heads/main", fromCommit: true, cancellationToken: TestContext.Current.CancellationToken);
        var request = new InteractiveRebaseRequest(new RebaseTarget(baseSha, baseSha[..7]), "refs/heads/main", FromHere: true, CheckoutFirst: false);
        await repository.ShowInteractiveRebaseAsync(request, list);
        await app.WaitForRebaseListAsync();

        Assert.True(repository.IsInteractiveRebaseOpen);
        Assert.False(repository.ShowsGraph);
        Assert.True(repository.ShowsInteractiveRebase);
        Assert.Equal("Base", repository.InteractiveRebase!.BaseRow.Subject);

        app.ClickRebaseCancel();
        await app.WaitForAsync(() => !repository.IsInteractiveRebaseOpen, "the list closed", TimeSpan.FromSeconds(10));
        await app.WaitForAsync(
            () => app.Window.FocusManager?.GetFocusedElement() is CommitGraphControl,
            "the focus on the graph",
            TimeSpan.FromSeconds(10));
        Assert.True(repository.ShowsGraph);
    }

    [AvaloniaFact]
    public async Task While_the_lists_Start_waits_in_the_queue_nothing_else_takes_the_graph_area_and_the_rebases_end_closes_the_list()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.InteractiveAsync();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitForRepositoryAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForIdleAsync(repository);
        var (request, list) = await ReadListFromBaseAsync(repo, repository);
        await repository.ShowInteractiveRebaseAsync(request, list);
        await app.WaitForRebaseListAsync();
        var first = repository.InteractiveRebase!;
        await first.SetActionAsync(first.Rows[0], RebaseAction.Drop);

        // A fetch runs: Start's operation waits behind it.
        var gate = new TaskCompletionSource();
        var fetch = repository.Queue.RunAsync(new GitOperation("Fetch", "Fetching…", OperationKind.Network, RefreshScope.None, async _ =>
        {
            await gate.Task;
            return OperationReport.Done("Fetched");
        }));
        var starting = first.StartCommand.ExecuteAsync(null);
        try
        {
            app.Settle();
            Assert.True(first.IsBusy);

            // Another list, a diff and the resolver from the stage panel, and Esc: declined at once, without a question.
            var second = repository.ShowInteractiveRebaseAsync(request, list);
            repository.OpenDiff(new ChangedFile("src/base.txt", FileChangeKind.Modified), staged: false);
            repository.OpenResolver(new ChangedFile("src/one.txt", FileChangeKind.Conflicted));
            app.PressKey(Key.Escape);
            app.Settle();
            Assert.True(second.IsCompleted, "Opening another list waits for no answer.");
            Assert.False(await repository.TryLeaveGraphAreaAsync());
            Assert.Empty(app.Session.Shell.Dialogs.Open);
            Assert.Same(first, repository.InteractiveRebase);
            Assert.True(repository.ShowsInteractiveRebase);
            Assert.False(repository.IsDiffOpen);
            Assert.False(repository.IsResolverOpen);
            Assert.True(first.IsBusy);
        }
        finally
        {
            // The fetch ends: the rebase runs.
            gate.SetResult();
        }

        // The list closes itself and the graph takes the focus.
        await fetch;
        await app.WaitForAsync(() => starting.IsCompleted, "the interactive rebase to end", TimeSpan.FromSeconds(60));
        await starting;
        Assert.True(first.IsClosed);
        Assert.False(repository.IsInteractiveRebaseOpen);
        await app.WaitForAsync(
            () => app.Window.FocusManager?.GetFocusedElement() is CommitGraphControl,
            "the focus on the graph",
            TimeSpan.FromSeconds(10));
        Assert.Equal("Four", (await repo.GitAsync("log", "-1", "--format=%s", "main")).StandardOutput.Trim());
    }

    [AvaloniaFact]
    public async Task A_list_no_longer_shown_neither_closes_the_list_shown_nor_starts_a_rebase()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.InteractiveAsync();
        var tip = (await repo.GitAsync("rev-parse", "main")).StandardOutput.Trim();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitForRepositoryAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var (request, list) = await ReadListFromBaseAsync(repo, repository);

        // Unchanged lists are left without a question: each replaces the one before.
        await repository.ShowInteractiveRebaseAsync(request, list);
        await app.WaitForRebaseListAsync();
        var first = repository.InteractiveRebase!;
        await repository.ShowInteractiveRebaseAsync(request, list);
        var second = repository.InteractiveRebase!;
        await repository.ShowInteractiveRebaseAsync(request, list);
        var shown = repository.InteractiveRebase!;
        Assert.Empty(app.Session.Shell.Dialogs.Open);
        Assert.NotSame(first, shown);
        Assert.NotSame(second, shown);

        // The first list closes: the list shown stays.
        await first.CancelCommand.ExecuteAsync(null);
        Assert.True(first.IsClosed);
        Assert.Same(shown, repository.InteractiveRebase);

        // The second is changed and started: nothing runs, and the list shown stays.
        await second.SetActionAsync(second.Rows[0], RebaseAction.Drop);
        await second.StartCommand.ExecuteAsync(null);
        await app.WaitForIdleAsync(repository);
        Assert.False(second.IsClosed);
        Assert.Same(shown, repository.InteractiveRebase);
        Assert.True(repository.ShowsInteractiveRebase);
        Assert.Equal(tip, (await repo.GitAsync("rev-parse", "main")).StandardOutput.Trim());
    }

    /// <summary>"Interactive rebase of main from here…" on Base (<c>main~6</c>), and the list the git layer reads for it.</summary>
    private static async Task<(InteractiveRebaseRequest Request, RebaseList List)> ReadListFromBaseAsync(TempRepo repo, RepositoryViewModel repository)
    {
        var baseSha = (await repo.GitAsync("rev-parse", "main~6")).StandardOutput.Trim();
        var list = await repository.Repository.ReadRebaseListAsync(baseSha, "refs/heads/main", fromCommit: true, cancellationToken: TestContext.Current.CancellationToken);
        return (new InteractiveRebaseRequest(new RebaseTarget(baseSha, baseSha[..7]), "refs/heads/main", FromHere: true, CheckoutFirst: false), list);
    }
}
