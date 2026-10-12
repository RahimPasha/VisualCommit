using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The stage panel while an operation is in progress (docs/phase-3-design.md section 5, D89),
/// without a UI: the list's title with its conflicts, the conflicted row's tooltip, committing a
/// merge with nothing staged, the empty pick's disabled button, and the message filled from
/// <c>MERGE_MSG</c>.
/// </summary>
public class WorkingChangesInProgressTests
{
    private const string Theirs = "078ef05aaf3ad9b3d0a6a1d6c1a6d50db5b9a0f1";

    private static readonly InProgressState Merge = new(InProgressKind.Merge, Theirs, "Merge branch 'feature/conflict'", null, null, 0, false);

    private static readonly ChangedFile Conflicted = new("src/core.txt", FileChangeKind.Conflicted);

    private static readonly ChangedFile Modified = new("README.md", FileChangeKind.Modified);

    [Fact]
    public void The_unstaged_title_counts_the_conflicts_and_a_conflicted_row_says_what_to_do()
    {
        var panel = Panel(new FakeHost());

        panel.Update(Status([], [Conflicted, Modified], Merge), hasHead: true);

        Assert.Equal("Unstaged files (2, 1 with conflicts)", panel.UnstagedTitle);
        var rows = panel.Unstaged.Rows.Where(row => row.IsFile).ToDictionary(row => row.Key);
        Assert.Equal("Conflict: resolve it in your editor, then stage it", rows["src/core.txt"].RowToolTip);
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
    public void An_empty_pick_or_revert_keeps_the_button_disabled_and_says_to_abort()
    {
        var panel = Panel(new FakeHost());
        var pick = new InProgressState(InProgressKind.CherryPick, Theirs, "Alpha one", null, null, 0, true);
        panel.Summary = "Alpha one";

        panel.Update(Status([], [], pick), hasHead: true);
        Assert.False(panel.CanCommit);
        Assert.Equal("This cherry-pick changes nothing: abort it", panel.CommitToolTip);

        panel.Update(Status([], [], pick with { Kind = InProgressKind.Revert }), hasHead: true);
        Assert.Equal("This revert changes nothing: abort it", panel.CommitToolTip);

        panel.Update(Status([], [], pick with { Empty = false }), hasHead: true);
        Assert.Null(panel.CommitToolTip);
    }

    [Fact]
    public void The_message_is_filled_from_MERGE_MSG_once_without_its_comments_and_cleared_when_the_operation_ends()
    {
        var host = new FakeHost { MergeMessage = "Revert \"Main work\"\r\n\r\nThis reverts commit 88d6b91.\r\n\r\n# Conflicts:\r\n#\tsrc/core.txt\r\n" };
        var panel = Panel(host);
        var revert = new InProgressState(InProgressKind.Revert, Theirs, "Main work", null, null, 0, false);

        panel.Update(Status([], [Conflicted], revert), hasHead: true);
        Assert.Equal(("Revert \"Main work\"", "This reverts commit 88d6b91."), (panel.Summary, panel.Description));

        // Cleared by the user, it stays clear while the same operation goes on.
        panel.Summary = string.Empty;
        panel.Description = string.Empty;
        panel.Update(Status([], [Conflicted], revert), hasHead: true);
        Assert.Equal(1, host.Reads);
        Assert.Equal(string.Empty, panel.Summary);

        // A new operation fills it again; when it ends, the untouched message goes.
        host.MergeMessage = "Merge branch 'feature/conflict'\n\n# Conflicts:\n#\tsrc/core.txt\n";
        panel.Update(Status([], [Conflicted], Merge), hasHead: true);
        Assert.Equal(("Merge branch 'feature/conflict'", string.Empty), (panel.Summary, panel.Description));
        panel.Update(Status([], [Modified], InProgressState.None), hasHead: true);
        Assert.Equal((string.Empty, string.Empty), (panel.Summary, panel.Description));

        // A message the user typed is never replaced, nor cleared.
        panel.Summary = "My own words";
        panel.Update(Status([], [Conflicted], Merge with { Commit = new string('9', 40) }), hasHead: true);
        panel.Update(Status([], [Modified], InProgressState.None), hasHead: true);
        Assert.Equal("My own words", panel.Summary);
        Assert.Equal(2, host.Reads);
    }

    [Fact]
    public void A_stopped_sequence_without_a_pick_in_progress_or_a_rebase_fills_nothing()
    {
        var host = new FakeHost { MergeMessage = "Something\n" };
        var panel = Panel(host);

        panel.Update(Status([], [], new InProgressState(InProgressKind.CherryPick, null, null, null, null, 1, false)), hasHead: true);
        panel.Update(Status([], [Conflicted], new InProgressState(InProgressKind.Rebase, null, null, "topic", Theirs, 0, false)), hasHead: true);

        Assert.Equal(0, host.Reads);
        Assert.Equal(string.Empty, panel.Summary);
    }

    [Theory]
    [InlineData("Subject\n", "Subject", "")]
    [InlineData("Subject\n\nBody line\n\n# Conflicts:\n#\ta.txt\n", "Subject", "Body line")]
    [InlineData("# only a comment\n\nSubject  \nsecond\n\n\nthird\n", "Subject", "second\n\n\nthird")]
    [InlineData("", "", "")]
    public void SplitMessage_takes_the_first_line_as_the_summary_and_the_rest_as_the_description(string text, string summary, string description)
    {
        Assert.Equal((summary, description), WorkingChangesViewModel.SplitMessage(text));
    }

    private static WorkingChangesViewModel Panel(FakeHost host) => new(host, new MainWindowViewModelTests.FakeSettings());

    private static WorkingTreeStatus Status(IReadOnlyList<ChangedFile> staged, IReadOnlyList<ChangedFile> unstaged, InProgressState state) =>
        new(staged, unstaged, new HashSet<string>(StringComparer.Ordinal)) { InProgress = state };

    /// <summary>A stage panel host whose MERGE_MSG the test sets; it counts the reads, which complete at once.</summary>
    private sealed class FakeHost : IWorkingChangesHost
    {
        public string? MergeMessage { get; set; }

        public int Reads { get; private set; }

        public bool HasHead => true;

        public Task<string?> ReadMergeMessageAsync()
        {
            Reads++;
            return Task.FromResult(MergeMessage);
        }

        public Task<string?> StageAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> UnstageAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> DiscardAsync(IReadOnlyList<ChangedFile> files) => Task.FromResult<string?>(null);

        public Task<string?> RestoreAsync() => Task.FromResult<string?>(null);

        public Task<string?> CommitAsync(string message, bool amend) => Task.FromResult<string?>(null);

        public Task<(string Subject, string Body)?> ReadHeadMessageAsync() => Task.FromResult<(string, string)?>(null);

        public void OpenDiff(ChangedFile file, bool staged)
        {
        }
    }
}
