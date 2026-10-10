using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The working changes without a UI: the stage panel's rules (<see cref="WorkingChangesViewModel"/>)
/// with a fake repository, and the repository's view model with real git on the changes scenario:
/// the status it reads, how the working-changes row moves the graph, and its writes (D71).
/// </summary>
public class WorkingChangesTests
{
    private static readonly IGitRunner Runner = new GitRunner(
        GitLocator.FindExecutable(GitSearchContext.FromSystem())
        ?? throw new InvalidOperationException("The tests need git, and no git executable was found."));

    private static readonly WorkingTreeStatus ThreeStaged = new(
        [new ChangedFile("a.txt", FileChangeKind.Modified), new ChangedFile("b.txt", FileChangeKind.Added), new ChangedFile("c.txt", FileChangeKind.Deleted)],
        [new ChangedFile("d.txt", FileChangeKind.Added)],
        new HashSet<string>(StringComparer.Ordinal) { "d.txt" });

    [Fact]
    public void The_commit_buttons_label_and_state_follow_the_staged_files_the_summary_and_amend()
    {
        var host = new FakeHost();
        var panel = new WorkingChangesViewModel(host, new MainWindowViewModelTests.FakeSettings());

        Assert.Equal("Stage files to commit", panel.CommitButtonText);
        Assert.False(panel.CanCommit);

        panel.Update(ThreeStaged, hasHead: true);
        Assert.Equal("Commit 3 files", panel.CommitButtonText);
        Assert.False(panel.CanCommit);

        panel.Summary = "   ";
        Assert.False(panel.CanCommit);
        panel.Summary = "Change";
        Assert.True(panel.CanCommit);
        Assert.True(panel.CommitCommand.CanExecute(null));

        panel.Update(new WorkingTreeStatus([ThreeStaged.Staged[0]], [], new HashSet<string>()), hasHead: true);
        Assert.Equal("Commit 1 file", panel.CommitButtonText);

        panel.Update(WorkingTreeStatus.Clean, hasHead: true);
        Assert.False(panel.CanCommit);
        panel.IsAmend = true;
        Assert.Equal("Amend previous commit", panel.CommitButtonText);
        Assert.True(panel.CanCommit);
    }

    [Fact]
    public void The_counter_shows_what_is_left_of_72_characters()
    {
        var panel = new WorkingChangesViewModel(new FakeHost(), new MainWindowViewModelTests.FakeSettings());

        Assert.Equal("72", panel.SummaryCounter);
        panel.Summary = new string('x', 22);
        Assert.Equal("50", panel.SummaryCounter);
        Assert.False(panel.IsSummaryOverLimit);
        panel.Summary = new string('x', 75);
        Assert.Equal("-3", panel.SummaryCounter);
        Assert.True(panel.IsSummaryOverLimit);
    }

    [Fact]
    public async Task Ticking_amend_fills_empty_boxes_with_heads_message_and_leaves_typed_ones()
    {
        var host = new FakeHost { HeadMessage = ("Subject", "Body") };
        var panel = new WorkingChangesViewModel(host, new MainWindowViewModelTests.FakeSettings());
        panel.Update(ThreeStaged, hasHead: true);

        panel.IsAmend = true;
        await host.HeadMessageRead.Task;
        Assert.Equal("Subject", panel.Summary);
        Assert.Equal("Body", panel.Description);

        panel.IsAmend = false;
        panel.Summary = "Keep";
        panel.Description = string.Empty;
        panel.IsAmend = true;
        Assert.Equal("Keep", panel.Summary);
        Assert.Equal(string.Empty, panel.Description);
    }

    [Fact]
    public async Task A_commit_sends_the_summary_and_description_and_clears_them_when_it_succeeds()
    {
        var host = new FakeHost();
        var panel = new WorkingChangesViewModel(host, new MainWindowViewModelTests.FakeSettings());
        panel.Update(ThreeStaged, hasHead: true);
        panel.Summary = "Subject";
        panel.Description = "Line one\nLine two";

        await panel.CommitCommand.ExecuteAsync(null);

        Assert.Equal(("Subject\n\nLine one\nLine two", false), host.Committed.Single());
        Assert.Equal(string.Empty, panel.Summary);
        Assert.Equal(string.Empty, panel.Description);

        host.CommitError = "Commit blocked by the test hook";
        panel.Summary = "Blocked";
        await panel.CommitCommand.ExecuteAsync(null);
        Assert.Equal("Blocked", panel.Summary);
        Assert.Equal("Commit blocked by the test hook", panel.ErrorText);
        Assert.Equal(("Blocked", false), host.Committed[1]);
    }

    [Fact]
    public void The_restore_bar_names_one_file_or_counts_several_and_amend_needs_a_head()
    {
        var panel = new WorkingChangesViewModel(new FakeHost(), new MainWindowViewModelTests.FakeSettings());

        panel.ShowRestore(["src/Calculator.cs"]);
        Assert.Equal("Discarded changes to Calculator.cs.", panel.RestoreText);
        panel.ShowRestore(["a", "b/c", "d"]);
        Assert.Equal("Discarded changes to 3 files.", panel.RestoreText);
        panel.DismissRestoreCommand.Execute(null);
        Assert.False(panel.HasRestore);

        panel.IsAmend = true;
        panel.Update(ThreeStaged, hasHead: false);
        Assert.False(panel.CanAmend);
        Assert.False(panel.IsAmend);
    }

    [Fact]
    public async Task The_changes_scenario_shows_the_working_changes_row_with_its_lists()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var viewModel = await OpenAsync(repo);

        Assert.True(viewModel.HasWorkingChanges);
        Assert.Equal("Unstaged files (7)", viewModel.Changes.UnstagedTitle);
        Assert.Equal("Staged files (3)", viewModel.Changes.StagedTitle);
        Assert.True(viewModel.Status.IsUntracked("docs/guide.md"));
        Assert.Equal("Commit 3 files", viewModel.Changes.CommitButtonText);
    }

    [Fact]
    public async Task When_the_row_comes_or_goes_while_scrolled_the_rows_in_view_stay_put()
    {
        using var repo = await Scenarios.LinearAsync();
        using var viewModel = await OpenAsync(repo);
        Assert.False(viewModel.HasWorkingChanges);

        viewModel.ScrollOffset = 40;
        repo.WriteFile("notes.txt", "A note\n");
        await viewModel.RefreshStatusAsync();
        Assert.True(viewModel.HasWorkingChanges);
        Assert.Equal(66, viewModel.ScrollOffset);

        File.Delete(Path.Combine(repo.Path, "notes.txt"));
        await viewModel.RefreshStatusAsync();
        Assert.False(viewModel.HasWorkingChanges);
        Assert.Equal(40, viewModel.ScrollOffset);

        // At the top, the new row is shown.
        viewModel.ScrollOffset = 0;
        repo.WriteFile("notes.txt", "A note\n");
        await viewModel.RefreshStatusAsync();
        Assert.Equal(0, viewModel.ScrollOffset);
    }

    [Fact]
    public async Task When_the_selected_row_goes_nothing_is_selected()
    {
        using var repo = await Scenarios.LinearAsync();
        repo.WriteFile("notes.txt", "A note\n");
        using var viewModel = await OpenAsync(repo);
        viewModel.IsWorkingRowSelected = true;

        File.Delete(Path.Combine(repo.Path, "notes.txt"));
        await viewModel.RefreshStatusAsync();

        Assert.False(viewModel.IsWorkingRowSelected);
        Assert.Equal(-1, viewModel.SelectedIndex);
    }

    [Fact]
    public async Task Selecting_the_row_or_a_commit_clears_the_other()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var viewModel = await OpenAsync(repo);

        viewModel.SelectedIndex = 0;
        viewModel.IsWorkingRowSelected = true;
        Assert.Equal(-1, viewModel.SelectedIndex);

        viewModel.SelectedIndex = 1;
        Assert.False(viewModel.IsWorkingRowSelected);
    }

    [Fact]
    public async Task Writes_pause_the_watcher_and_refresh_once_after()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = new PausingRepository(await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken));
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        await viewModel.StartAsync();

        var error = await viewModel.StageAsync([new ChangedFile("docs/guide.md", FileChangeKind.Added)]);

        Assert.Null(error);
        Assert.Equal(1, repository.Watcher.Pauses);
        Assert.Equal(0, repository.Watcher.Paused);
        Assert.Equal("Staged files (4)", viewModel.Changes.StagedTitle);
    }

    [Fact]
    public async Task A_discard_asks_first_and_does_nothing_when_cancelled()
    {
        using var repo = await Scenarios.ChangesAsync();
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        var dialogs = new AnsweringDialogs(answer: false);
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc), dialogs: dialogs);
        await viewModel.StartAsync();
        var file = Path.Combine(repo.Path, "src", "Calculator.cs");

        await viewModel.DiscardAsync([new ChangedFile("src/Calculator.cs", FileChangeKind.Modified)]);

        Assert.Equal(new ConfirmationRequest("Discard changes?", "Discard the changes to Calculator.cs?", "A snapshot is saved first, so the changes can be restored.", "Discard"), dialogs.Asked.Single());
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking, File.ReadAllText(file));
        Assert.Empty((await repo.GitAsync("for-each-ref", "refs/visualcommit/")).StandardOutput);
        Assert.False(viewModel.Changes.HasRestore);
    }

    [Fact]
    public async Task Opening_a_diff_and_selecting_a_ref_closes_it()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var viewModel = await OpenAsync(repo);

        viewModel.OpenDiff(new ChangedFile("README.md", FileChangeKind.Modified), staged: false);
        Assert.True(viewModel.IsDiffOpen);
        Assert.Equal(DiffSide.Unstaged, viewModel.Diff!.Target.Side);
        Assert.True(viewModel.Changes.Unstaged.Rows.Single(row => row.Key == "README.md").IsSelected);
        Assert.False(viewModel.Changes.Staged.Rows.Single(row => row.Key == "README.md").IsSelected);

        viewModel.Select(viewModel.Graph.CommitAt(1).Sha, reveal: false);
        Assert.False(viewModel.IsDiffOpen);
        Assert.All(viewModel.Changes.Unstaged.Rows, row => Assert.False(row.IsSelected));
    }

    private static async Task<RepositoryViewModel> OpenAsync(TempRepo repo)
    {
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        await viewModel.StartAsync();
        return viewModel;
    }

    /// <summary>The stage panel's host, answering from what the test sets.</summary>
    private sealed class FakeHost : IWorkingChangesHost
    {
        public (string Subject, string Body)? HeadMessage { get; init; }

        public string? CommitError { get; set; }

        public List<(string Message, bool Amend)> Committed { get; } = [];

        public TaskCompletionSource HeadMessageRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool HasHead => true;

        public Task<string?> StageAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> UnstageAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> DiscardAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> RestoreAsync() => Task.FromResult<string?>(null);

        public Task<string?> CommitAsync(string message, bool amend)
        {
            Committed.Add((message, amend));
            return Task.FromResult(CommitError);
        }

        public Task<(string Subject, string Body)?> ReadHeadMessageAsync()
        {
            HeadMessageRead.TrySetResult();
            return Task.FromResult(HeadMessage);
        }

        public void OpenDiff(ChangedFile file, bool staged)
        {
        }
    }

    /// <summary>Answers every confirmation the same way, and keeps what was asked.</summary>
    private sealed class AnsweringDialogs(bool answer) : IDialogService
    {
        public List<ConfirmationRequest> Asked { get; } = [];

        public Task<bool> ConfirmAsync(ConfirmationRequest request)
        {
            Asked.Add(request);
            return Task.FromResult(answer);
        }
    }

    /// <summary>A real repository whose watcher counts its pauses.</summary>
    private sealed class PausingRepository(GitRepository inner) : FakeRepositoryBase
    {
        public CountingWatcher Watcher { get; } = new();

        public override string WorkingDirectory => inner.WorkingDirectory;

        public override string GitDirectory => inner.GitDirectory;

        public override string CommonDirectory => inner.CommonDirectory;

        public override string Name => inner.Name;

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) => inner.ReadRefsAsync(cancellationToken);

        public override Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default) =>
            inner.LoadCommitsAsync(refs, onPage, cancellationToken);

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default) => inner.ReadCommitDetailsAsync(sha, cancellationToken);

        public override Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default) => inner.ReadStatusAsync(cancellationToken);

        public override Task StageAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
        {
            Assert.Equal(1, Watcher.Paused);
            return inner.StageAsync(paths, cancellationToken);
        }

        public override IRepositoryWatcher CreateWatcher() => Watcher;
    }

    private sealed class CountingWatcher : IRepositoryWatcher
    {
        public int Pauses { get; private set; }

        public int Paused { get; private set; }

        public event EventHandler<RepositoryChangedEventArgs>? Changed
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public IDisposable Pause()
        {
            Pauses++;
            Paused++;
            return new Resume(this);
        }

        public void Dispose()
        {
        }

        private sealed class Resume(CountingWatcher watcher) : IDisposable
        {
            public void Dispose() => watcher.Paused--;
        }
    }
}
