using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The stage panel while an operation is in progress (docs/phase-3-design.md section 5, D89; as
/// docs/phase-4-design.md 5.3 and 7.1 change it), without a UI: the list's title with its
/// conflicts, the conflicted row's tooltip, committing a merge with nothing staged, the empty
/// pick's disabled button, the message filled for the stopped commit, the button that continues a
/// list or a rebase, Amend disabled during them, and the questions about conflict markers.
/// </summary>
public class WorkingChangesInProgressTests
{
    private const string Theirs = "078ef05aaf3ad9b3d0a6a1d6c1a6d50db5b9a0f1";

    private static readonly InProgressState Merge = new(InProgressKind.Merge, Theirs, "Merge branch 'feature/conflict'", null, null, 0, false);

    private static readonly InProgressState PickInList = new(InProgressKind.CherryPick, Theirs, "Conflicting change", null, null, 1, false) { Sequenced = true, HasPickHead = true };

    private static readonly InProgressState RebaseStop = new(InProgressKind.Rebase, Theirs, "First change", "feature/rebase", new string('d', 40), 1, false) { StopKind = RebaseStopKind.Pick, Done = 1, Total = 2 };

    private static readonly ChangedFile Conflicted = new("src/core.txt", FileChangeKind.Conflicted);

    private static readonly ChangedFile Modified = new("README.md", FileChangeKind.Modified);

    [Fact]
    public void The_unstaged_title_counts_the_conflicts_and_a_conflicted_row_says_a_click_resolves_it()
    {
        var panel = Panel(new FakeHost());

        panel.Update(Status([], [Conflicted, Modified], Merge), hasHead: true);

        Assert.Equal("Unstaged files (2, 1 with conflicts)", panel.UnstagedTitle);
        var rows = panel.Unstaged.Rows.Where(row => row.IsFile).ToDictionary(row => row.Key);
        Assert.Equal("Conflict: click to resolve it", rows["src/core.txt"].RowToolTip);
        Assert.Equal("README.md", rows["README.md"].RowToolTip);

        panel.Update(Status([], [Modified], InProgressState.None), hasHead: true);
        Assert.Equal("Unstaged files (1)", panel.UnstagedTitle);
    }

    [Fact]
    public void A_merge_without_conflicts_left_can_be_committed_with_nothing_staged()
    {
        var host = new FakeHost();
        var panel = Panel(host);

        // A conflict is left: nothing to commit yet.
        panel.Update(Status([], [Conflicted], Merge), hasHead: true);
        panel.Summary = "Merge branch 'feature/conflict'";
        Assert.Equal("Stage files to commit", panel.CommitButtonText);
        Assert.False(panel.CanCommit);
        Assert.True(panel.CanAmend);
        Assert.Null(panel.AmendToolTip);

        // Resolved to "ours": the index equals HEAD and git still records the merge.
        panel.Update(Status([], [], Merge), hasHead: true);
        Assert.Equal("Commit merge", panel.CommitButtonText);
        Assert.True(panel.CanCommit);
        Assert.True(panel.CommitCommand.CanExecute(null));
        Assert.Null(panel.CommitToolTip);

        // Nothing staged and no merge: phase 2's rule.
        panel.Update(Status([], [Modified], InProgressState.None), hasHead: true);
        Assert.False(panel.CanCommit);
        Assert.Equal("Stage files to commit", panel.CommitButtonText);
    }

    [Fact]
    public void An_empty_pick_or_revert_keeps_the_button_disabled_and_says_to_skip_it()
    {
        var panel = Panel(new FakeHost());
        var pick = new InProgressState(InProgressKind.CherryPick, Theirs, "Alpha one", null, null, 0, true) { HasPickHead = true };
        panel.Summary = "Alpha one";

        panel.Update(Status([], [], pick), hasHead: true);
        Assert.False(panel.CanCommit);
        Assert.Equal("This cherry-pick changes nothing: skip it", panel.CommitToolTip);

        panel.Update(Status([], [], pick with { Kind = InProgressKind.Revert }), hasHead: true);
        Assert.Equal("This revert changes nothing: skip it", panel.CommitToolTip);

        panel.Update(Status([], [], pick with { Empty = false }), hasHead: true);
        Assert.Null(panel.CommitToolTip);
    }

    [Fact]
    public void A_list_or_a_rebase_turns_the_commit_button_into_Continue_and_disables_Amend()
    {
        var panel = Panel(new FakeHost());
        panel.IsAmend = true;

        // A pick of a list with a conflict: Continue cherry-pick, disabled with Continue's reason.
        panel.Update(Status([], [Conflicted], PickInList), hasHead: true);
        Assert.Equal("Continue cherry-pick", panel.CommitButtonText);
        Assert.False(panel.CanCommit);
        Assert.Equal("Resolve the conflicts first (1 file)", panel.CommitToolTip);
        Assert.False(panel.IsAmend);
        Assert.False(panel.CanAmend);
        Assert.Equal("Finish or abort the cherry-pick first", panel.AmendToolTip);

        // Resolved: enabled, also with an empty summary (the operation's own message is used then).
        panel.Summary = string.Empty;
        panel.Update(Status([Modified], [], PickInList), hasHead: true);
        Assert.True(panel.CanCommit);
        Assert.Null(panel.CommitToolTip);

        panel.Update(Status([], [Conflicted], PickInList with { Kind = InProgressKind.Revert }), hasHead: true);
        Assert.Equal("Continue revert", panel.CommitButtonText);
        Assert.Equal("Finish or abort the revert first", panel.AmendToolTip);

        panel.Update(Status([], [Conflicted], RebaseStop), hasHead: true);
        Assert.Equal("Continue rebase", panel.CommitButtonText);
        Assert.Equal("Resolve the conflicts first (1 file)", panel.CommitToolTip);
        Assert.Equal("Finish or abort the rebase first", panel.AmendToolTip);

        // A paused list (committed in a terminal) continues too; a merge and a single pick commit.
        panel.Update(Status([], [], PickInList with { HasPickHead = false, AbortKeepsHead = true }), hasHead: true);
        Assert.Equal("Continue cherry-pick", panel.CommitButtonText);
        Assert.True(panel.CanCommit);
        panel.Update(Status([], [Conflicted], PickInList with { Sequenced = false, Remaining = 0 }), hasHead: true);
        Assert.Equal("Stage files to commit", panel.CommitButtonText);
        Assert.True(panel.CanAmend);
    }

    [Fact]
    public async Task Continue_passes_no_message_while_the_boxes_hold_the_filled_one_and_the_joined_one_once_edited()
    {
        var host = new FakeHost { OperationMessage = "First change\n" };
        var panel = Panel(host);
        panel.Update(Status([Modified], [], RebaseStop), hasHead: true);
        Assert.Equal(("First change", string.Empty), (panel.Summary, panel.Description));

        // The operation goes on to the next stop: the filled message is replaced by the next one.
        host.OnContinue = () =>
        {
            host.OperationMessage = "Second change\n";
            panel.Update(Status([], [Conflicted], RebaseStop with { Commit = new string('5', 40), Done = 2 }), hasHead: true);
        };
        await panel.CommitCommand.ExecuteAsync(null);
        Assert.Equal([null], host.Continued);
        Assert.Equal("Second change", panel.Summary);

        // An edited message is passed as phase 3's commit joins it, and goes once the operation has gone on.
        panel.Update(Status([Modified], [], RebaseStop with { Commit = new string('5', 40), Done = 2 }), hasHead: true);
        panel.Summary = "Second change, edited";
        panel.Description = "#7 a line of its own";
        host.OnContinue = () => panel.Update(Status([], [], InProgressState.None), hasHead: true);
        await panel.CommitCommand.ExecuteAsync(null);
        Assert.Equal([null, "Second change, edited\n\n#7 a line of its own"], host.Continued);
        Assert.Equal((string.Empty, string.Empty), (panel.Summary, panel.Description));

        // A Continue that did not go on (refused, failed) keeps what the user typed.
        panel.Update(Status([Modified], [], RebaseStop), hasHead: true);
        panel.Summary = "Kept";
        host.OnContinue = null;
        await panel.CommitCommand.ExecuteAsync(null);
        Assert.Equal("Kept", panel.Summary);
    }

    [Fact]
    public void The_message_is_filled_once_per_stopped_commit_with_its_own_hash_lines_and_cleared_when_it_goes_on()
    {
        // The git layer leaves out git's comment block (ReadOperationMessageAsync); a commit's own
        // "#" lines stay (5.3, D68).
        var host = new FakeHost { OperationMessage = "Revert \"Main work\"\r\n\r\nThis reverts commit 88d6b91.\r\n#7 kept\r\n" };
        var panel = Panel(host);
        var revert = new InProgressState(InProgressKind.Revert, Theirs, "Main work", null, null, 0, false) { HasPickHead = true };

        panel.Update(Status([], [Conflicted], revert), hasHead: true);
        Assert.Equal(("Revert \"Main work\"", "This reverts commit 88d6b91.\n#7 kept"), (panel.Summary, panel.Description));
        Assert.Equal([revert], host.ReadFor);

        // Cleared by the user, it stays clear while the same commit is stopped.
        panel.Summary = string.Empty;
        panel.Description = string.Empty;
        panel.Update(Status([], [Conflicted], revert), hasHead: true);
        Assert.Single(host.ReadFor);
        Assert.Equal(string.Empty, panel.Summary);

        // Another stopped commit fills it again; when the operation ends, the untouched message goes.
        host.OperationMessage = "Merge branch 'feature/conflict'\n";
        panel.Update(Status([], [Conflicted], Merge), hasHead: true);
        Assert.Equal(("Merge branch 'feature/conflict'", string.Empty), (panel.Summary, panel.Description));
        panel.Update(Status([], [Modified], InProgressState.None), hasHead: true);
        Assert.Equal((string.Empty, string.Empty), (panel.Summary, panel.Description));

        // A rebase's stop is filled from its message, once per stopped commit.
        host.OperationMessage = "First change\n";
        panel.Update(Status([], [Conflicted], RebaseStop), hasHead: true);
        Assert.Equal("First change", panel.Summary);

        // A message the user typed is never replaced, nor cleared.
        panel.Summary = "My own words";
        panel.Update(Status([], [Conflicted], Merge with { Commit = new string('9', 40) }), hasHead: true);
        panel.Update(Status([], [Modified], InProgressState.None), hasHead: true);
        Assert.Equal("My own words", panel.Summary);
        Assert.Equal(3, host.ReadFor.Count);
    }

    [Fact]
    public void A_list_paused_between_commits_and_git_am_fill_nothing()
    {
        var host = new FakeHost { OperationMessage = "Something\n" };
        var panel = Panel(host);

        panel.Update(Status([], [], new InProgressState(InProgressKind.CherryPick, Theirs, "Alpha", null, null, 1, false) { Sequenced = true, AbortKeepsHead = true }), hasHead: true);
        panel.Update(Status([], [Conflicted], new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, 1, false)), hasHead: true);

        Assert.Empty(host.ReadFor);
        Assert.Equal(string.Empty, panel.Summary);
    }

    [Theory]
    [InlineData("Subject\n", "Subject", "")]
    [InlineData("Subject\n\nBody line\n#1 kept\n", "Subject", "Body line\n#1 kept")]
    [InlineData("\n\nSubject  \n\nsecond\n\n\nthird\n\n", "Subject", "second\n\n\nthird")]
    [InlineData("# a commit's own first line\n", "# a commit's own first line", "")]
    [InlineData("", "", "")]
    public void SplitMessage_keeps_every_line_and_takes_the_first_as_the_summary(string text, string summary, string description)
    {
        Assert.Equal((summary, description), WorkingChangesViewModel.SplitMessage(text));
    }

    [Fact]
    public async Task Staging_and_the_commit_that_ends_a_merge_ask_the_hosts_questions_first()
    {
        // 7.1, 7.2, 14.3: a host that asks about conflict markers is asked; "no" stages and commits nothing.
        var host = new FakeHost { Answer = false };
        var panel = Panel(host);
        panel.Update(Status([], [Conflicted, Modified], Merge), hasHead: true);

        await panel.StageAllCommand.ExecuteAsync(null);
        await panel.StageFileCommand.ExecuteAsync(panel.Unstaged.Rows.Single(row => row.Key == "src/core.txt"));
        Assert.Equal(2, host.StagingQuestions.Count);
        Assert.Equal(["src/core.txt", "README.md"], host.StagingQuestions[0].Select(file => file.Path));
        Assert.Empty(host.Staged);

        host.Answer = true;
        await panel.StageFileCommand.ExecuteAsync(panel.Unstaged.Rows.Single(row => row.Key == "src/core.txt"));
        Assert.Equal(["src/core.txt"], host.Staged.Single().Select(file => file.Path));

        // The commit that concludes the merge asks; one that is no operation's does not.
        panel.Update(Status([Conflicted], [], Merge), hasHead: true);
        panel.Summary = "Merge branch 'feature/conflict'";
        host.Answer = false;
        await panel.CommitCommand.ExecuteAsync(null);
        Assert.Equal([Merge], host.ConcludingQuestions);
        Assert.Empty(host.Commits);

        host.Answer = true;
        await panel.CommitCommand.ExecuteAsync(null);
        Assert.Equal(["Merge branch 'feature/conflict'"], host.Commits);

        panel.Update(Status([Modified], [], InProgressState.None), hasHead: true);
        panel.Summary = "Plain";
        await panel.CommitCommand.ExecuteAsync(null);
        Assert.Equal(2, host.ConcludingQuestions.Count);
        Assert.Equal(["Merge branch 'feature/conflict'", "Plain"], host.Commits);
    }

    private static WorkingChangesViewModel Panel(FakeHost host) => new(host, new MainWindowViewModelTests.FakeSettings());

    private static WorkingTreeStatus Status(IReadOnlyList<ChangedFile> staged, IReadOnlyList<ChangedFile> unstaged, InProgressState state) =>
        new(staged, unstaged, new HashSet<string>(StringComparer.Ordinal)) { InProgress = state };

    /// <summary>
    /// A stage panel host whose operation message the test sets; it records the reads, the
    /// Continues, the commits and stages and the questions, which complete at once.
    /// </summary>
    private sealed class FakeHost : IWorkingChangesHost, IConflictMarkerQuestions
    {
        public string? OperationMessage { get; set; }

        public List<InProgressState> ReadFor { get; } = [];

        public List<string?> Continued { get; } = [];

        public Action? OnContinue { get; set; }

        public bool Answer { get; set; } = true;

        public List<IReadOnlyList<ChangedFile>> StagingQuestions { get; } = [];

        public List<InProgressState> ConcludingQuestions { get; } = [];

        public List<IReadOnlyList<ChangedFile>> Staged { get; } = [];

        public List<string> Commits { get; } = [];

        public bool HasHead => true;

        public Task<string?> ReadOperationMessageAsync(InProgressState state)
        {
            ReadFor.Add(state);
            return Task.FromResult(OperationMessage);
        }

        public Task<string?> ContinueAsync(string? message)
        {
            Continued.Add(message);
            OnContinue?.Invoke();
            return Task.FromResult<string?>(null);
        }

        public Task<bool> ConfirmStagingAsync(IReadOnlyList<ChangedFile> files)
        {
            StagingQuestions.Add(files);
            return Task.FromResult(Answer);
        }

        public Task<bool> ConfirmConcludingAsync(InProgressState state)
        {
            ConcludingQuestions.Add(state);
            return Task.FromResult(Answer);
        }

        public Task<string?> StageAsync(IReadOnlyList<ChangedFile> files)
        {
            Staged.Add(files);
            return Task.FromResult<string?>(null);
        }

        public Task<string?> UnstageAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> DiscardAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> RestoreAsync() => Task.FromResult<string?>(null);

        public Task<string?> CommitAsync(string message, bool amend)
        {
            Commits.Add(message);
            return Task.FromResult<string?>(null);
        }

        public Task<(string Subject, string Body)?> ReadHeadMessageAsync() => Task.FromResult<(string, string)?>(null);

        public void OpenDiff(ChangedFile file, bool staged)
        {
        }
    }
}
