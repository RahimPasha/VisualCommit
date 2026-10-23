using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Diff;
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
    public async Task An_amend_ticked_before_head_moved_refuses_in_d124s_words_and_amends_nothing()
    {
        // Amend was ticked on HEAD's commit, whose message it loaded (phase 2's rule); then HEAD moved
        // (a Pull all, another tab's operation, a terminal: here a commit made outside). The commit
        // would amend the new HEAD with the old message; it refuses inside its operation instead
        // (D124), says so in the stage panel, and the new HEAD stays as it is.
        using var repo = await Scenarios.LinearAsync();
        using var viewModel = await OpenAsync(repo);
        var ticked = (await repo.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim();
        var tickedSubject = (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim();
        viewModel.Changes.IsAmend = true;
        await WaitUntilAsync(() => viewModel.Changes.Summary == tickedSubject, "HEAD's message in the boxes");

        var moved = await repo.CommitFileAsync("pulled.txt", "pulled\n", "Pulled from elsewhere");
        await viewModel.RefreshAsync();
        Assert.True(viewModel.Changes.IsAmend);
        Assert.True(viewModel.Changes.CanCommit);

        await viewModel.Changes.CommitCommand.ExecuteAsync(null);

        Assert.Equal($"main moved while this waited: it was at {ticked[..7]} and is at {moved[..7]} now. Nothing was amended.", viewModel.Changes.ErrorText);
        Assert.Equal(moved, (await repo.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim());
        Assert.Equal("Pulled from elsewhere", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.Equal(tickedSubject, viewModel.Changes.Summary);

        // Ticked again on the new HEAD, the amend goes ahead.
        viewModel.Changes.IsAmend = false;
        viewModel.Changes.Summary = string.Empty;
        viewModel.Changes.Description = string.Empty;
        viewModel.Changes.IsAmend = true;
        await WaitUntilAsync(() => viewModel.Changes.Summary == "Pulled from elsewhere", "the new HEAD's message");
        viewModel.Changes.Summary = "Pulled, amended";
        await viewModel.Changes.CommitCommand.ExecuteAsync(null);
        Assert.Null(viewModel.Changes.ErrorText);
        Assert.Equal("Pulled, amended", (await repo.GitAsync("log", "-1", "--format=%s")).StandardOutput.Trim());
        Assert.Equal(ticked, (await repo.GitAsync("rev-parse", "HEAD~1")).StandardOutput.Trim());
    }

    [Fact]
    public async Task Ticking_amend_keeps_the_head_it_was_ticked_on_for_the_commit()
    {
        var host = new FakeHost { HeadMessage = ("Subject", "Body"), CurrentHead = new HeadState("main", new string('a', 40)) };
        var panel = new WorkingChangesViewModel(host, new MainWindowViewModelTests.FakeSettings());
        panel.Update(ThreeStaged, hasHead: true);

        panel.IsAmend = true;
        await host.HeadMessageRead.Task;
        host.CurrentHead = new HeadState("main", new string('b', 40));
        await panel.CommitCommand.ExecuteAsync(null);

        Assert.Equal(new HeadState("main", new string('a', 40)), Assert.Single(host.Amended));

        // A commit that is no amend checks no HEAD.
        panel.Summary = "Plain";
        await panel.CommitCommand.ExecuteAsync(null);
        Assert.Equal(("Plain", false), host.Committed[^1]);
        Assert.Single(host.Amended);
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
    public async Task When_the_row_goes_at_the_bottom_of_the_graph_the_rows_in_view_stay_put()
    {
        using var repo = await Scenarios.LinearAsync();
        repo.WriteFile("notes.txt", "A note\n");
        using var viewModel = await OpenAsync(repo);
        Assert.True(viewModel.HasWorkingChanges);

        // Scrolled to the bottom: 60 is as far as the graph goes with the row, 34 without. The
        // control clamps the offset as soon as its rows get fewer, as the real one does.
        viewModel.ScrollOffset = 60;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RepositoryViewModel.HasWorkingChanges) && !viewModel.HasWorkingChanges)
            {
                viewModel.ScrollOffset = Math.Min(viewModel.ScrollOffset, 34);
            }
        };
        File.Delete(Path.Combine(repo.Path, "notes.txt"));
        await viewModel.RefreshStatusAsync();

        Assert.False(viewModel.HasWorkingChanges);
        Assert.Equal(34, viewModel.ScrollOffset);
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
        var dialogs = new AnsweringDialogs(false);
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
    public async Task Restore_asks_when_the_file_changed_since_the_discard_and_keeps_the_newer_version_in_a_snapshot()
    {
        using var repo = await Scenarios.ChangesAsync();
        var dialogs = new AnsweringDialogs(true, false, true);
        using var viewModel = await OpenAsync(repo, dialogs);
        var file = Path.Combine(repo.Path, "src", "Calculator.cs");
        await viewModel.DiscardAsync([new ChangedFile("src/Calculator.cs", FileChangeKind.Modified)]);
        Assert.True(viewModel.Changes.HasRestore);
        File.WriteAllText(file, "newer\n");

        // Cancelled: nothing changes, and the bar stays.
        Assert.Null(await viewModel.RestoreAsync());
        Assert.Equal(
            new ConfirmationRequest("Restore discarded changes?", "Calculator.cs has changed since the discard. Put back the version from before the discard?", "A snapshot of the newer changes is saved first.", "Restore"),
            dialogs.Asked[1]);
        Assert.Equal("newer\n", File.ReadAllText(file));
        Assert.True(viewModel.Changes.HasRestore);

        Assert.Null(await viewModel.RestoreAsync());
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking, File.ReadAllText(file));
        Assert.False(viewModel.Changes.HasRestore);
        var snapshots = (await repo.GitAsync("for-each-ref", "--sort=refname", "--format=%(refname)", "refs/visualcommit/")).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, snapshots.Length);
        Assert.Equal("newer\n", (await repo.GitAsync("show", snapshots[1] + ":src/Calculator.cs")).StandardOutput);
    }

    [Fact]
    public async Task Restore_without_changes_since_the_discard_asks_nothing()
    {
        using var repo = await Scenarios.ChangesAsync();
        var dialogs = new AnsweringDialogs(true);
        using var viewModel = await OpenAsync(repo, dialogs);

        await viewModel.DiscardAsync([new ChangedFile("src/Calculator.cs", FileChangeKind.Modified), new ChangedFile("docs/guide.md", FileChangeKind.Added)]);
        Assert.Null(await viewModel.RestoreAsync());

        Assert.Single(dialogs.Asked);
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking, File.ReadAllText(Path.Combine(repo.Path, "src", "Calculator.cs")));
        Assert.True(File.Exists(Path.Combine(repo.Path, "docs", "guide.md")));
        Assert.False(viewModel.Changes.HasRestore);
    }

    [Fact]
    public async Task Discarding_leaves_conflicted_files_out_and_says_so()
    {
        using var repo = await Scenarios.ConflictAsync();
        repo.WriteFile("other.txt", "one, changed\n");
        var dialogs = new AnsweringDialogs(true);
        using var viewModel = await OpenAsync(repo, dialogs);
        Assert.Equal(
            [("both.txt", FileChangeKind.Conflicted), ("other.txt", FileChangeKind.Modified)],
            viewModel.Status.Unstaged.Select(file => (file.Path, file.Kind)));

        // The conflicted file alone: nothing is asked or done.
        var error = await viewModel.DiscardAsync([viewModel.Status.Unstaged[0]]);
        Assert.Equal("both.txt has a conflict: resolve it before discarding.", error);
        Assert.Empty(dialogs.Asked);

        Assert.Null(await viewModel.DiscardAsync(viewModel.Status.Unstaged));
        Assert.Equal("Discard the changes to other.txt? both.txt is left as it is.", dialogs.Asked.Single().Question);
        Assert.Equal("one\n", File.ReadAllText(Path.Combine(repo.Path, "other.txt")));
        Assert.Contains("UU both.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput, StringComparison.Ordinal);
        Assert.Equal("Discarded changes to other.txt.", viewModel.Changes.RestoreText);
    }

    [Fact]
    public async Task A_repository_inside_the_working_tree_is_never_discarded_and_its_diff_says_why()
    {
        using var repo = await Scenarios.LinearAsync();
        await repo.GitAsync("init", "--quiet", "inner");
        repo.WriteFile("inner/notes.txt", "Notes\n");
        var dialogs = new AnsweringDialogs(true);
        using var viewModel = await OpenAsync(repo, dialogs);
        Assert.Equal(["inner/"], viewModel.Status.Unstaged.Select(file => file.Path));

        var error = await viewModel.DiscardAsync(viewModel.Status.Unstaged);

        Assert.Equal("inner is a repository of its own: it is not discarded.", error);
        Assert.Empty(dialogs.Asked);
        Assert.True(Directory.Exists(Path.Combine(repo.Path, "inner", ".git")));
        viewModel.OpenDiff(viewModel.Status.Unstaged[0], staged: false);
        await viewModel.Diff!.LoadAsync(viewModel.Diff.Target);
        Assert.Equal(DiffBody.Error, viewModel.Diff.Body);
        Assert.Equal("This folder is a repository of its own: its files are not shown here.", viewModel.Diff.ErrorText);
    }

    [Fact]
    public async Task A_discard_that_fails_shows_gits_message_and_no_restore_bar()
    {
        using var repo = await Scenarios.LinearAsync();
        using var viewModel = await OpenAsync(repo, new AnsweringDialogs(true));

        var error = await viewModel.DiscardAsync([new ChangedFile("missing.txt", FileChangeKind.Modified)]);

        Assert.NotNull(error);
        Assert.Contains("missing.txt", error, StringComparison.Ordinal);
        Assert.Equal(error, viewModel.Changes.ErrorText);
        Assert.False(viewModel.Changes.HasRestore);
    }

    [Fact]
    public async Task Closing_the_tab_while_a_discard_waits_for_its_answer_does_nothing()
    {
        using var repo = await Scenarios.ChangesAsync();
        var dialogs = new WaitingDialogs();
        var viewModel = await OpenAsync(repo, dialogs);
        var discarding = viewModel.DiscardAsync([new ChangedFile("src/Calculator.cs", FileChangeKind.Modified)]);
        await dialogs.Opened.Task;

        viewModel.Dispose();
        dialogs.Answer(true);

        Assert.Null(await discarding);
        Assert.Equal(Scenarios.ChangesFiles.CalculatorWorking, File.ReadAllText(Path.Combine(repo.Path, "src", "Calculator.cs")));
        Assert.Empty((await repo.GitAsync("for-each-ref", "refs/visualcommit/")).StandardOutput);
    }

    [Fact]
    public async Task A_diff_opened_while_a_commit_waits_to_be_selected_stays_open()
    {
        var repository = new TwoPageRepository();
        using var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));
        var starting = viewModel.StartAsync();
        await repository.FirstPageSent.Task;
        for (var wait = 0; viewModel.Graph.Count == 0 && wait < 500; wait++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        // The second commit is not loaded yet: it waits to be selected. A diff opened meanwhile wins.
        viewModel.Select(TwoPageRepository.Second.Sha, reveal: true);
        viewModel.OpenDiff(new ChangedFile("a.txt", FileChangeKind.Modified), staged: false);
        repository.SendSecondPage();
        await starting;

        Assert.Equal(2, viewModel.Graph.Count);
        Assert.True(viewModel.IsDiffOpen);
        Assert.NotEqual(1, viewModel.SelectedIndex);
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

    /// <summary>Waits until <paramref name="condition"/> holds, looking every few milliseconds, for at most 30 seconds.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting for {what}.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static async Task<RepositoryViewModel> OpenAsync(TempRepo repo, IDialogService? dialogs = null)
    {
        var repository = await GitRepository.OpenAsync(Runner, repo.Path, TestContext.Current.CancellationToken);
        var viewModel = new RepositoryViewModel(repository, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc), dialogs: dialogs);
        await viewModel.StartAsync();
        return viewModel;
    }

    /// <summary>The stage panel's host, answering from what the test sets.</summary>
    private sealed class FakeHost : IWorkingChangesHost
    {
        public (string Subject, string Body)? HeadMessage { get; init; }

        public string? CommitError { get; set; }

        public List<(string Message, bool Amend)> Committed { get; } = [];

        /// <summary>The HEAD each amend was for, as the panel passed it.</summary>
        public List<HeadState?> Amended { get; } = [];

        public HeadState? CurrentHead { get; set; }

        public HeadState? Head => CurrentHead;

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

        public Task<string?> CommitAsync(string message, bool amend, HeadState? amended)
        {
            if (amend)
            {
                Amended.Add(amended);
            }

            return CommitAsync(message, amend);
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

    /// <summary>Answers the confirmations in the order given, the last answer from then on, and keeps what was asked.</summary>
    private sealed class AnsweringDialogs(params bool[] answers) : IDialogService
    {
        public List<ConfirmationRequest> Asked { get; } = [];

        public Task<bool> ConfirmAsync(ConfirmationRequest request)
        {
            Asked.Add(request);
            return Task.FromResult(answers[Math.Min(Asked.Count, answers.Length) - 1]);
        }
    }

    /// <summary>A history of two commits whose second page waits until the test sends it; a.txt is changed.</summary>
    private sealed class TwoPageRepository : FakeRepositoryBase
    {
        public static readonly CommitInfo First = Commit(2, [new string('1', 40)]);

        public static readonly CommitInfo Second = Commit(1, []);

        private readonly TaskCompletionSource _second = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstPageSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string WorkingDirectory => Path.Combine(Path.GetTempPath(), "fake");

        public override string GitDirectory => Path.Combine(WorkingDirectory, ".git");

        public override string CommonDirectory => GitDirectory;

        public override string Name => "fake";

        public void SendSecondPage() => _second.TrySetResult();

        public override Task<RepoRefs> ReadRefsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepoRefs(new HeadState("main", First.Sha), [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, First.Sha, IsHead: true)], [], []));

        public override async Task LoadCommitsAsync(RepoRefs refs, Action<IReadOnlyList<CommitInfo>> onPage, CancellationToken cancellationToken = default)
        {
            onPage([First]);
            FirstPageSent.TrySetResult();
            await _second.Task.WaitAsync(cancellationToken);
            onPage([Second]);
        }

        public override Task<CommitDetails> ReadCommitDetailsAsync(string sha, CancellationToken cancellationToken = default)
        {
            var commit = sha == First.Sha ? First : Second;
            return Task.FromResult(new CommitDetails(commit.Sha, commit.Parents, commit.AuthorName, commit.AuthorEmail, commit.AuthorDate, commit.AuthorName, commit.AuthorEmail, commit.CommitDate, commit.Subject, string.Empty, []));
        }

        public override Task<WorkingTreeStatus> ReadStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkingTreeStatus([], [new ChangedFile("a.txt", FileChangeKind.Modified)], new HashSet<string>()));

        public override Task<FileDiff> ReadDiffAsync(DiffTarget target, CancellationToken cancellationToken = default) =>
            Task.FromResult(FileDiff.Empty);

        public override IRepositoryWatcher CreateWatcher() => new CountingWatcher();

        private static CommitInfo Commit(int number, string[] parents)
        {
            var date = new DateTimeOffset(2026, 1, 1, 12, number, 0, TimeSpan.Zero);
            return new CommitInfo(new string((char)('0' + number), 40), parents, "Test Author", "author@example.com", date, date, $"Commit {number}");
        }
    }

    /// <summary>Keeps a confirmation open until the test answers it.</summary>
    private sealed class WaitingDialogs : IDialogService
    {
        private readonly TaskCompletionSource<bool> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> ConfirmAsync(ConfirmationRequest request)
        {
            Opened.TrySetResult();
            return _answer.Task;
        }

        public void Answer(bool answer) => _answer.TrySetResult(answer);
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
