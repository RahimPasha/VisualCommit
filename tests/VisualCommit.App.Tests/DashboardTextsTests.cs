using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Git;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>The dashboard's cells and header texts (docs/phase-6-design.md 2.5, 2.6, 14.3; WD4, WD5): pure.</summary>
public class DashboardTextsTests
{
    private static readonly string Head = new('a', 40);

    private static readonly string Sha = new('c', 40);

    [Theory]
    [InlineData(0, "No repositories")]
    [InlineData(1, "1 repository")]
    [InlineData(10, "10 repositories")]
    public void The_header_counts_the_repositories(int repositories, string text) =>
        Assert.Equal(text, DashboardTexts.CountText(repositories));

    [Fact]
    public void The_headers_other_texts()
    {
        Assert.Equal("Web has no repositories yet.", DashboardTexts.EmptyText("Web"));
        Assert.Equal("Add a repository to Web", DashboardTexts.AddRepositoryToolTip("Web"));
        // Refresh's key (F5, 6.1) comes from the key texts, which work package WP-K fills.
        Assert.Equal(
            ViewModels.Commands.KeyTexts.Current.WithKey("Read the repositories of Web again", ViewModels.Commands.AppCommand.Refresh),
            DashboardTexts.RefreshToolTip("Web"));
        Assert.StartsWith("Read the repositories of Web again", DashboardTexts.RefreshToolTip("Web"), StringComparison.Ordinal);
        Assert.Equal("workspaces.json could not be saved, so changes to workspaces are lost when VisualCommit closes.", DashboardTexts.SaveFailedNotice);
        Assert.Equal(
            ["Repository", "Folder", "Branch", "Ahead / behind", "Changes", "State", "Operation"],
            [DashboardTexts.RepositoryHeading, DashboardTexts.FolderHeading, DashboardTexts.BranchHeading, DashboardTexts.AheadBehindHeading, DashboardTexts.ChangesHeading, DashboardTexts.StateHeading, DashboardTexts.OperationHeading]);
    }

    [Fact]
    public void The_branch_cell_names_the_branch_the_detached_commit_or_the_rebased_branch()
    {
        Assert.Equal("main", DashboardTexts.BranchText(Refs(ahead: 0, behind: 0), WorkingTreeStatus.Clean));
        Assert.Equal("main", DashboardTexts.BranchText(new RepoRefs(new HeadState("main", null), [], [], []), WorkingTreeStatus.Clean));
        Assert.Equal("Detached at 09d320a", DashboardTexts.BranchText(new RepoRefs(new HeadState(null, "09d320a17f4690c9317493bf1d77ff7e3f7209c7"), [], [], ["origin"]), WorkingTreeStatus.Clean));
        var rebasing = WorkingTreeStatus.Clean with { InProgress = new InProgressState(InProgressKind.Rebase, Sha, "Work", "topic", Head, 3, false) { Done = 2, Total = 5 } };
        Assert.Equal("topic", DashboardTexts.BranchText(new RepoRefs(new HeadState(null, Sha), [], [], ["origin"]), rebasing));
    }

    [Theory]
    [InlineData(1, 0, "1 ahead", "1 commit ahead of origin/main")]
    [InlineData(2, 0, "2 ahead", "2 commits ahead of origin/main")]
    [InlineData(0, 1, "1 behind", "1 commit behind origin/main")]
    [InlineData(0, 3, "3 behind", "3 commits behind origin/main")]
    [InlineData(1, 2, "1 ahead, 2 behind", "1 commit ahead of and 2 behind origin/main")]
    [InlineData(4, 5, "4 ahead, 5 behind", "4 commits ahead of and 5 behind origin/main")]
    public void Ahead_and_behind_show_the_arrows_with_their_counts(int ahead, int behind, string name, string toolTip)
    {
        var cell = DashboardTexts.AheadBehind(Refs(ahead, behind), WorkingTreeStatus.Clean);

        Assert.True(cell.ShowsArrows);
        Assert.Equal((ahead > 0, behind > 0), (cell.ShowsAhead, cell.ShowsBehind));
        Assert.Equal((ahead, behind), (cell.Ahead, cell.Behind));
        Assert.Equal(string.Empty, cell.Text);
        Assert.Equal(name, cell.AutomationName);
        Assert.Equal(toolTip, cell.ToolTip);
        Assert.Equal(DashboardTone.Secondary, cell.Tone);
    }

    [Fact]
    public void A_level_branch_is_up_to_date()
    {
        var cell = DashboardTexts.AheadBehind(Refs(0, 0), WorkingTreeStatus.Clean);

        Assert.Equal(("Up to date", DashboardTone.Secondary, "main is level with origin/main"), (cell.Text, cell.Tone, cell.ToolTip));
        Assert.False(cell.ShowsArrows);
    }

    [Fact]
    public void A_gone_upstream_says_so_in_the_warning_colour()
    {
        var refs = new RepoRefs(
            new HeadState("feature", Head),
            [new GitRef("refs/heads/feature", "feature", RefKind.LocalBranch, Head, Upstream: "origin/feature", UpstreamGone: true, IsHead: true, UpstreamRef: "refs/remotes/origin/feature", UpstreamRemote: "origin", UpstreamRemoteRef: "refs/heads/feature")],
            [],
            ["origin"]);

        var cell = DashboardTexts.AheadBehind(refs, WorkingTreeStatus.Clean);

        Assert.Equal(("origin/feature is gone", DashboardTone.Warning, "origin/feature no longer exists"), (cell.Text, cell.Tone, cell.ToolTip));
    }

    [Fact]
    public void A_local_upstream_is_named_by_its_branch()
    {
        var refs = new RepoRefs(
            new HeadState("topic", Head),
            [
                new GitRef("refs/heads/develop", "develop", RefKind.LocalBranch, Sha),
                new GitRef("refs/heads/topic", "topic", RefKind.LocalBranch, Head, Upstream: "develop", Ahead: 1, IsHead: true, UpstreamRef: "refs/heads/develop", UpstreamRemote: ".", UpstreamRemoteRef: "refs/heads/develop"),
            ],
            [],
            []);

        var cell = DashboardTexts.AheadBehind(refs, WorkingTreeStatus.Clean);

        Assert.Equal(("1 ahead", "1 commit ahead of develop"), (cell.AutomationName, cell.ToolTip));
    }

    [Fact]
    public void No_remote_is_tested_before_no_upstream()
    {
        // WD4: notes has no remote and no upstream; its cell says the first, its Pull reason the second.
        var notes = new RepoRefs(new HeadState("main", Head), [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, Head, IsHead: true)], [], []);
        var noRemote = DashboardTexts.AheadBehind(notes, WorkingTreeStatus.Clean);
        Assert.Equal(("No remote", DashboardTone.Secondary, "This repository has no remotes"), (noRemote.Text, noRemote.Tone, noRemote.ToolTip));
        Assert.Equal("main has no upstream: set one first", ToolbarViewModel.StatesFor(notes, WorkingTreeStatus.Clean).Pull.Reason);

        var scratch = new RepoRefs(new HeadState("experiment", Head), [new GitRef("refs/heads/experiment", "experiment", RefKind.LocalBranch, Head, IsHead: true)], [], ["origin"]);
        var noUpstream = DashboardTexts.AheadBehind(scratch, WorkingTreeStatus.Clean);
        Assert.Equal(("No upstream", DashboardTone.Secondary, "experiment has no upstream"), (noUpstream.Text, noUpstream.Tone, noUpstream.ToolTip));
    }

    [Fact]
    public void An_unborn_branch_has_no_commits_yet()
    {
        var cell = DashboardTexts.AheadBehind(new RepoRefs(new HeadState("main", null), [], [], ["origin"]), WorkingTreeStatus.Clean);

        Assert.Equal(("No commits yet", DashboardTone.Secondary, null), (cell.Text, cell.Tone, cell.ToolTip));
    }

    [Fact]
    public void Detached_and_rebasing_leave_ahead_and_behind_empty()
    {
        Assert.Equal(DashboardAheadBehind.Empty, DashboardTexts.AheadBehind(new RepoRefs(new HeadState(null, Head), [], [], ["origin"]), WorkingTreeStatus.Clean));
        var rebasing = WorkingTreeStatus.Clean with { InProgress = new InProgressState(InProgressKind.Rebase, Sha, "Work", "main", Head, 1, false) };
        Assert.Equal(DashboardAheadBehind.Empty, DashboardTexts.AheadBehind(Refs(1, 1), rebasing));
    }

    [Fact]
    public void A_clean_tree_is_clean()
    {
        var cell = DashboardTexts.Changes(WorkingTreeStatus.Clean);

        Assert.Equal(("Clean", DashboardTone.Secondary, null), (cell.Text, cell.Tone, cell.ToolTip));
    }

    [Fact]
    public void The_unstaged_count_leaves_out_untracked_and_conflicted_entries()
    {
        // WD5: design-system's status as the app reads it (scenario facts 2.2): icons.txt is in
        // Unstaged as Added and in Untracked.
        var designSystem = new WorkingTreeStatus(
            [new ChangedFile("colours.txt", FileChangeKind.Modified)],
            [new ChangedFile("fonts.txt", FileChangeKind.Deleted), new ChangedFile("icons.txt", FileChangeKind.Added), new ChangedFile("spacing.txt", FileChangeKind.Modified)],
            new HashSet<string>(StringComparer.Ordinal) { "icons.txt" });
        var cell = DashboardTexts.Changes(designSystem);
        Assert.Equal(("1 staged, 2 unstaged, 1 untracked", DashboardTone.Primary, "1 staged, 2 unstaged, 1 untracked"), (cell.Text, cell.Tone, cell.ToolTip));

        // billing: one conflicted file, in Unstaged as Conflicted.
        var billing = new WorkingTreeStatus([], [new ChangedFile("invoice.txt", FileChangeKind.Conflicted)], new HashSet<string>(StringComparer.Ordinal));
        var conflict = DashboardTexts.Changes(billing);
        Assert.Equal(("1 conflict", DashboardTone.Danger, "1 conflict"), (conflict.Text, conflict.Tone, conflict.ToolTip));

        // scratch with one new file.
        var scratch = new WorkingTreeStatus([], [new ChangedFile("todo.txt", FileChangeKind.Added)], new HashSet<string>(StringComparer.Ordinal) { "todo.txt" });
        Assert.Equal("1 untracked", DashboardTexts.Changes(scratch).Text);
    }

    [Fact]
    public void A_file_staged_and_changed_again_counts_twice_and_the_kinds_come_in_their_order()
    {
        var twice = new WorkingTreeStatus([new ChangedFile("a.txt", FileChangeKind.Modified)], [new ChangedFile("a.txt", FileChangeKind.Modified)], new HashSet<string>(StringComparer.Ordinal));
        Assert.Equal("1 staged, 1 unstaged", DashboardTexts.Changes(twice).Text);

        var all = new WorkingTreeStatus(
            [new ChangedFile("s1", FileChangeKind.Added), new ChangedFile("s2", FileChangeKind.Modified)],
            [new ChangedFile("c1", FileChangeKind.Conflicted), new ChangedFile("c2", FileChangeKind.Conflicted), new ChangedFile("u1", FileChangeKind.Modified), new ChangedFile("n1", FileChangeKind.Added), new ChangedFile("n2", FileChangeKind.Added)],
            new HashSet<string>(StringComparer.Ordinal) { "n1", "n2" });
        var cell = DashboardTexts.Changes(all);
        Assert.Equal(("2 conflicts, 2 staged, 1 unstaged, 2 untracked", DashboardTone.Danger), (cell.Text, cell.Tone));
    }

    [Theory]
    [InlineData(InProgressKind.Merge, 0, 0, "Merge in progress")]
    [InlineData(InProgressKind.CherryPick, 0, 0, "Cherry-pick in progress")]
    [InlineData(InProgressKind.Revert, 0, 0, "Revert in progress")]
    [InlineData(InProgressKind.Rebase, 0, 0, "Rebase in progress")]
    [InlineData(InProgressKind.Rebase, 2, 5, "Rebase in progress (2 of 5)")]
    [InlineData(InProgressKind.ApplyMailbox, 0, 0, "git am in progress")]
    public void What_is_in_progress_shows_in_the_warning_colour_with_the_banners_tooltip(InProgressKind kind, int done, int total, string text)
    {
        var state = new InProgressState(kind, Sha, "Merge branch 'feature/tax'", kind == InProgressKind.Rebase ? "main" : null, kind == InProgressKind.Rebase ? Head : null, 0, false) { Done = done, Total = total };
        var status = new WorkingTreeStatus([], [new ChangedFile("invoice.txt", FileChangeKind.Conflicted)], new HashSet<string>(StringComparer.Ordinal)) { InProgress = state };

        var cell = DashboardTexts.InProgress(Refs(0, 0), status);

        Assert.Equal((text, DashboardTone.Warning), (cell.Text, cell.Tone));
        Assert.Equal(ConflictBannerViewModel.ToolTipFor(state, 1, "main"), cell.ToolTip);
    }

    [Fact]
    public void Billings_merge_has_the_banners_tooltip()
    {
        var state = new InProgressState(InProgressKind.Merge, "8236f530eac42c26fdf90b73daa64df549f264fd", "Merge branch 'feature/tax'", null, null, 0, false);
        var status = new WorkingTreeStatus([], [new ChangedFile("invoice.txt", FileChangeKind.Conflicted)], new HashSet<string>(StringComparer.Ordinal)) { InProgress = state };

        Assert.Equal(
            "Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.",
            DashboardTexts.InProgress(Refs(0, 0), status).ToolTip);
        Assert.Equal(DashboardCellText.Empty, DashboardTexts.InProgress(Refs(0, 0), WorkingTreeStatus.Clean));
    }

    [Fact]
    public void The_folder_states_and_a_failed_read()
    {
        var path = Path.Combine("D:", "src", "old-site");
        var gone = DashboardTexts.FolderState(WorkspaceFolderProbe.FolderNotFound(path));
        Assert.Equal(("Folder not found", DashboardTone.Warning, $"{path} does not exist"), (gone.Text, gone.Tone, gone.ToolTip));
        var notRepository = DashboardTexts.FolderState(WorkspaceFolderProbe.NotARepository(path));
        Assert.Equal(("Not a repository", $"{path} is not a git repository"), (notRepository.Text, notRepository.ToolTip));
        var inside = DashboardTexts.FolderState(WorkspaceFolderProbe.NotARepositoryInside(path, "D:\\src"));
        Assert.Equal($"{path} is not a git repository of its own: it lies inside D:\\src", inside.ToolTip);

        var failed = DashboardTexts.CouldNotOpen(path, "fatal: detected dubious ownership in repository at 'D:/src/old-site'\nTo add an exception …");
        Assert.Equal(
            ("Git could not open it", DashboardTone.Warning, $"Git could not open {path}: fatal: detected dubious ownership in repository at 'D:/src/old-site'"),
            (failed.Text, failed.Tone, failed.ToolTip));
    }

    [Fact]
    public void The_menus_reasons_and_the_logs_lines()
    {
        Assert.Equal("Wait for api to be read", DashboardTexts.WaitToBeRead("api"));
        Assert.Equal("Cancel Fetch", DashboardTexts.CancelTitle("Fetch"));
        Assert.Equal("Pull can no longer be cancelled", DashboardTexts.NoLongerCancellable("Pull"));
        Assert.Equal("Web has no repositories", DashboardTexts.NoRepositoriesText("Web"));
        Assert.Equal("Select a repository to see its activity", DashboardTexts.SelectRepositoryText);
        Assert.Equal("Waiting for Pull to finish…", DashboardTexts.WaitingToFinish("Pull"));
        Assert.Equal("Web: read 1 repository in 12 ms", DashboardTexts.ReadLine("Web", 1, 12));
        Assert.Equal("Web: read 10 repositories in 4737 ms", DashboardTexts.ReadLine("Web", 10, 4737));
        Assert.Equal("api: summary read in 3 ms", DashboardTexts.SummaryReadLine("api", 3));
        Assert.Equal("old-site", DashboardTexts.NameOf(Path.Combine("D:", "src", "old-site") + Path.DirectorySeparatorChar));
    }

    private static RepoRefs Refs(int ahead, int behind) => DashboardTestSupport.MainRefs(ahead, behind);
}
