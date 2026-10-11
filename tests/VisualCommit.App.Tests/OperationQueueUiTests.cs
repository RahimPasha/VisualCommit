using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The operation queue on the headless UI thread (docs/phase-3-design.md 1.3, 1.4): operations
/// run on the UI thread, and phase 2's writes go through the queue with their behaviour unchanged.
/// The queue's own rules are tested without a UI in VisualCommit.Git.Tests (<c>OperationQueueTests</c>).
/// </summary>
public class OperationQueueUiTests
{
    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    [AvaloniaFact]
    public async Task Operations_run_on_the_UI_thread_and_a_discard_and_its_restore_go_through_the_queue()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var viewModel = await OpenAsync(repo);
        var finished = new List<(string Title, bool OnUiThread, OperationOutcome Outcome)>();
        viewModel.Queue.Finished += (_, e) => finished.Add((e.Operation.Title, Dispatcher.UIThread.CheckAccess(), e.Result.Outcome));
        var onUiThread = new List<bool>();

        var probe = await viewModel.Queue.RunAsync(new GitOperation("Probe", "Probing…", OperationKind.Local, RefreshScope.Status, async _ =>
        {
            onUiThread.Add(Dispatcher.UIThread.CheckAccess());
            await Task.Delay(10, TestContext.Current.CancellationToken);
            onUiThread.Add(Dispatcher.UIThread.CheckAccess());
            return OperationReport.Done("Probed");
        }));

        // The restore bar is view-model state bound to the view: changed on the UI thread, or an
        // exception would end the write as a failure.
        var discarded = await viewModel.DiscardAsync([new ChangedFile("src/Calculator.cs", FileChangeKind.Modified)]);
        Assert.Null(discarded);
        Assert.True(viewModel.Changes.HasRestore);
        Assert.Equal(Scenarios.ChangesFiles.CalculatorCommitted, File.ReadAllText(Path.Combine(repo.Path, "src", "Calculator.cs")).ReplaceLineEndings("\n"));
        var restored = await viewModel.RestoreAsync();

        Assert.Equal(OperationOutcome.Succeeded, probe.Outcome);
        Assert.Equal([true, true], onUiThread);
        Assert.Null(restored);
        Assert.False(viewModel.Changes.HasRestore);
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking, File.ReadAllText(Path.Combine(repo.Path, "src", "Calculator.cs")).ReplaceLineEndings("\n"));
        Assert.Equal(
            [("Probe", true, OperationOutcome.Succeeded), ("Discard", true, OperationOutcome.Succeeded), ("Restore", true, OperationOutcome.Succeeded)],
            finished);

        // The stage panel's operations leave no message in the status bar (1.9).
        Assert.Equal(OperationStatusViewModel.ReadyText, viewModel.OperationStatus.Text);
    }

    [AvaloniaFact]
    public async Task Stages_of_two_files_asked_for_together_both_run_in_order()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var viewModel = await OpenAsync(repo);
        var order = new List<string>();
        viewModel.Queue.Finished += (_, e) => order.Add(e.Operation.Title);
        var calculator = viewModel.Status.Unstaged.Single(file => file.Path == "src/Calculator.cs");
        var readme = viewModel.Status.Unstaged.Single(file => file.Path == "README.md");

        var first = viewModel.StageAsync([calculator]);
        var second = viewModel.StageAsync([readme]);
        Assert.Equal(1, viewModel.Queue.Waiting);

        Assert.Null(await first);
        Assert.Null(await second);
        Assert.Equal(["Stage", "Stage"], order);
        Assert.Contains(viewModel.Status.Staged, file => file.Path == "src/Calculator.cs");
        Assert.DoesNotContain(viewModel.Status.Unstaged, file => file.Path == "README.md");
    }

    [AvaloniaFact]
    public async Task A_write_that_fails_shows_gits_message_in_the_stage_panel_and_none_in_the_status_bar()
    {
        using var repo = await Scenarios.LinearAsync();
        using var viewModel = await OpenAsync(repo);

        var error = await viewModel.DiscardAsync([new ChangedFile("missing.txt", FileChangeKind.Modified)]);

        Assert.NotNull(error);
        Assert.Contains("missing.txt", error, StringComparison.Ordinal);
        Assert.Equal(error, viewModel.Changes.ErrorText);
        Assert.Equal(OperationOutcome.Failed, viewModel.Queue.LastResult!.Outcome);
        Assert.Equal((OperationStatusViewModel.ReadyText, OperationStatusTone.Secondary), (viewModel.OperationStatus.Text, viewModel.OperationStatus.Tone));
    }

    [AvaloniaFact]
    public async Task A_selected_row_is_the_selection_and_a_selection_of_one_commit_is_the_selected_row()
    {
        using var repo = await Scenarios.LinearAsync();
        using var viewModel = await OpenAsync(repo);
        var seen = new List<GraphSelection>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RepositoryViewModel.Selection))
            {
                seen.Add(viewModel.Selection);
            }
        };

        viewModel.SelectedIndex = 1;
        Assert.Equal(GraphSelection.Of(viewModel.Graph.CommitAt(1).Sha), viewModel.Selection);

        viewModel.Selection = GraphSelection.Of(viewModel.Graph.CommitAt(2).Sha);
        Assert.Equal(2, viewModel.SelectedIndex);

        viewModel.Selection = new GraphSelection([viewModel.Graph.CommitAt(0).Sha, viewModel.Graph.CommitAt(2).Sha]);
        Assert.Equal(-1, viewModel.SelectedIndex);
        Assert.True(viewModel.Selection.IsMultiple);

        viewModel.SelectedIndex = 0;
        Assert.Equal(GraphSelection.Of(viewModel.Graph.CommitAt(0).Sha), viewModel.Selection);
        viewModel.IsWorkingRowSelected = false;
        viewModel.SelectedIndex = -1;
        Assert.Equal(GraphSelection.Empty, viewModel.Selection);
        Assert.Equal(5, seen.Count);
    }

    private static async Task<RepositoryViewModel> OpenAsync(TempRepo repo)
    {
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc), dialogs: new Yes());
        await viewModel.StartAsync();
        return viewModel;
    }

    private sealed class Yes : IDialogService
    {
        public Task<bool> ConfirmAsync(ConfirmationRequest request) => Task.FromResult(true);
    }
}
