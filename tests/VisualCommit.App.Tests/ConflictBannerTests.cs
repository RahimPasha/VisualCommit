using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The conflict banner (docs/phase-3-design.md section 5, D89, D110, D113): its text for every
/// operation and state, the Abort button and its question, and how it looks above the graph in
/// both themes on the stopped scenarios of 7.1; and what the stage panel shows during an
/// operation (checks 9, 9b and 25 of docs/test-reports/phase-3.md).
/// </summary>
public class ConflictBannerTests
{
    private const string Conflicting = "078ef05aaf3ad9b3d0a6a1d6c1a6d50db5b9a0f1";
    private const string MainWork = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

    [Theory]
    [InlineData(1, "Merging: \"Merge branch 'feature/conflict'\". 1 file has conflicts.")]
    [InlineData(2, "Merging: \"Merge branch 'feature/conflict'\". 2 files have conflicts.")]
    [InlineData(0, "Merging: \"Merge branch 'feature/conflict'\". Conflicts resolved: commit to finish the merge.")]
    public void A_merge_says_what_it_merges_and_how_many_files_conflict(int conflicts, string text)
    {
        var state = new InProgressState(InProgressKind.Merge, Conflicting, "Merge branch 'feature/conflict'", null, null, 0, false);

        Assert.Equal(text, ConflictBannerViewModel.TextFor(state, conflicts));
        Assert.Equal("Abort merge", ConflictBannerViewModel.AbortButtonText(state.Kind));
    }

    [Theory]
    [InlineData(1, "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts.")]
    [InlineData(0, "Cherry-picking 078ef05 \"Conflicting change\". Conflicts resolved: commit to finish the cherry-pick.")]
    public void A_single_cherry_pick_keeps_phase_3s_text(int conflicts, string text)
    {
        var state = new InProgressState(InProgressKind.CherryPick, Conflicting, "Conflicting change", null, null, 0, false) { HasPickHead = true };

        Assert.Equal(text, ConflictBannerViewModel.TextFor(state, conflicts));
        Assert.Equal("Abort cherry-pick", ConflictBannerViewModel.AbortButtonText(state.Kind));
    }

    [Theory]
    [InlineData(1, 0, "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts.")]
    [InlineData(1, 1, "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts. 1 more commit waits: Continue picks it.")]
    [InlineData(0, 1, "Cherry-picking 078ef05 \"Conflicting change\". Conflicts resolved: continue to commit it. 1 more commit waits: Continue picks it.")]
    [InlineData(1, 2, "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts. 2 more commits wait: Continue picks them.")]
    [InlineData(0, 0, "Cherry-picking 078ef05 \"Conflicting change\". Conflicts resolved: continue to commit it.")]
    public void A_pick_in_a_list_says_what_waits_and_that_Continue_picks_it(int conflicts, int remaining, string text)
    {
        // docs/phase-4-design.md 5.2 (new): git's list keeps the commits that wait.
        var state = new InProgressState(InProgressKind.CherryPick, Conflicting, "Conflicting change", null, null, remaining, false) { Sequenced = true, HasPickHead = true };

        Assert.Equal(text, ConflictBannerViewModel.TextFor(state, conflicts));
    }

    [Fact]
    public void A_revert_in_a_list_says_Continue_reverts()
    {
        var state = new InProgressState(InProgressKind.Revert, MainWork, "Main work", null, null, 2, false) { Sequenced = true, HasPickHead = true };

        Assert.Equal("Reverting 88d6b91 \"Main work\". 1 file has conflicts. 2 more commits wait: Continue reverts them.", ConflictBannerViewModel.TextFor(state, 1));
        Assert.Equal("Reverting 88d6b91 \"Main work\". Conflicts resolved: continue to commit it. 1 more commit waits: Continue reverts it.", ConflictBannerViewModel.TextFor(state with { Remaining = 1 }, 0));
    }

    [Theory]
    [InlineData(InProgressKind.CherryPick, 1, "Cherry-pick paused: 1 more commit waits. Continue picks it.")]
    [InlineData(InProgressKind.CherryPick, 2, "Cherry-pick paused: 2 more commits wait. Continue picks them.")]
    [InlineData(InProgressKind.CherryPick, 0, "Cherry-pick paused with no commits left: continue to end it.")]
    [InlineData(InProgressKind.Revert, 1, "Revert paused: 1 more commit waits. Continue reverts it.")]
    public void A_list_paused_between_commits_says_how_many_wait(InProgressKind kind, int remaining, string text)
    {
        // 5.2 (new): the stopped commit was committed in a terminal (AbortKeepsHead, D110).
        var state = new InProgressState(kind, Conflicting, "Conflicting change", null, null, remaining, false) { Sequenced = true, AbortKeepsHead = true };

        Assert.Equal(text, ConflictBannerViewModel.TextFor(state, 0));
    }

    [Fact]
    public void A_list_stopped_on_a_commit_git_could_not_apply_says_to_skip_it_or_abort()
    {
        var pick = new InProgressState(InProgressKind.CherryPick, Conflicting, "Conflicting change", null, null, 1, false) { Sequenced = true };
        var revert = pick with { Kind = InProgressKind.Revert };

        Assert.Equal("Cherry-pick stopped at 078ef05 \"Conflicting change\": git could not pick it. Skip it, or abort.", ConflictBannerViewModel.TextFor(pick, 0));
        Assert.Equal("Revert stopped at 078ef05 \"Conflicting change\": git could not revert it. Skip it, or abort.", ConflictBannerViewModel.TextFor(revert, 0));
    }

    [Fact]
    public void An_empty_pick_says_it_changes_nothing_and_to_skip_it()
    {
        var pick = new InProgressState(InProgressKind.CherryPick, "dff8fbf0000000000000000000000000000000aa", "Alpha one", null, null, 0, true) { HasPickHead = true };
        var revert = pick with { Kind = InProgressKind.Revert, Subject = "Main work" };

        Assert.Equal("Cherry-picking dff8fbf \"Alpha one\" changes nothing: its changes are already here. Skip it to go on.", ConflictBannerViewModel.TextFor(pick, 0));
        Assert.Equal("Reverting dff8fbf \"Main work\" changes nothing: it is already undone. Skip it to go on.", ConflictBannerViewModel.TextFor(revert, 0));

        // In a list the sentence of the list follows; an empty revert of a list has no pick head (section 4).
        Assert.Equal(
            "Cherry-picking dff8fbf \"Alpha one\" changes nothing: its changes are already here. Skip it to go on. 1 more commit waits: Continue picks it.",
            ConflictBannerViewModel.TextFor(pick with { Sequenced = true, Remaining = 1 }, 0));
        Assert.Equal(
            "Reverting dff8fbf \"Main work\" changes nothing: it is already undone. Skip it to go on.",
            ConflictBannerViewModel.TextFor(revert with { Sequenced = true, HasPickHead = false }, 0));
    }

    [Fact]
    public void A_revert_a_rebase_git_am_and_conflicts_without_an_operation_have_their_own_texts()
    {
        var revert = new InProgressState(InProgressKind.Revert, MainWork, "Main work", null, null, 0, false) { HasPickHead = true };
        var rebase = new InProgressState(InProgressKind.Rebase, Conflicting, "Conflicting change", "feature/conflict", MainWork, 0, false) { StopKind = RebaseStopKind.Pick, Done = 1, Total = 1 };
        var am = new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, 1, false);

        Assert.Equal("Reverting 88d6b91 \"Main work\". 1 file has conflicts.", ConflictBannerViewModel.TextFor(revert, 1));
        Assert.Equal("Rebasing feature/conflict onto main: stopped at 078ef05 \"Conflicting change\" (1 of 1). 1 file has conflicts.", ConflictBannerViewModel.TextFor(rebase, 1, "main"));
        Assert.Equal("Rebasing feature/conflict onto 88d6b91: stopped at 078ef05 \"Conflicting change\" (1 of 1). 1 file has conflicts.", ConflictBannerViewModel.TextFor(rebase, 1));
        Assert.Equal("Rebasing feature/conflict onto main: stopped at 078ef05 \"Conflicting change\" (1 of 1). Conflicts resolved: continue to commit it and go on.", ConflictBannerViewModel.TextFor(rebase, 0, "main"));
        Assert.Equal("Applying patches (git am). 1 file has conflicts.", ConflictBannerViewModel.TextFor(am, 1));
        Assert.Equal("Applying patches (git am). Continue in a terminal (git am --continue), or abort.", ConflictBannerViewModel.TextFor(am, 0));
        Assert.Equal("1 file has conflicts. Resolve it, then stage it.", ConflictBannerViewModel.TextFor(InProgressState.None, 1));
        Assert.Equal("3 files have conflicts. Resolve them, then stage them.", ConflictBannerViewModel.TextFor(InProgressState.None, 3));
        Assert.Null(ConflictBannerViewModel.TextFor(InProgressState.None, 0));
        Assert.Equal(["Abort revert", "Abort rebase", "Abort", ""], new[] { revert, rebase, am, InProgressState.None }.Select(state => ConflictBannerViewModel.AbortButtonText(state.Kind)));
    }

    [Fact]
    public void Each_kind_of_rebase_stop_has_its_own_text()
    {
        // docs/phase-4-design.md 5.2's rebase rows (new), {onto} a branch's name or the short SHA.
        var stop = new InProgressState(InProgressKind.Rebase, Conflicting, "Conflicting change", "feature/x", MainWork, 1, false) { StopKind = RebaseStopKind.Pick, Done = 2, Total = 3 };

        Assert.Equal("Rebasing feature/x onto main: stopped at 078ef05 \"Conflicting change\" (2 of 3). It changes nothing now: continue to leave it out.", ConflictBannerViewModel.TextFor(stop with { StopKind = RebaseStopKind.Empty }, 0, "main"));
        Assert.Equal("Rebasing feature/x onto main: stopped at 078ef05 \"Conflicting change\" (2 of 3). It changes nothing now: abort, or change it.", ConflictBannerViewModel.TextFor(stop with { StopKind = RebaseStopKind.Empty, NextIsSquash = true }, 0, "main"));
        Assert.Equal("Rebasing feature/x onto main: git could not apply 078ef05 \"Conflicting change\" (2 of 3): an untracked file is in the way. Move or remove it, then continue.", ConflictBannerViewModel.TextFor(stop with { StopKind = RebaseStopKind.Rescheduled }, 0, "main"));
        Assert.Equal("Rebasing feature/x onto main: the new message of 078ef05 was not applied (2 of 3). Continue tries again; Skip goes on without it.", ConflictBannerViewModel.TextFor(stop with { StopKind = RebaseStopKind.OwnAmendFailed }, 0, "main"));
        Assert.Equal("Rebasing feature/x onto main: a command failed after 078ef05 (2 of 3). Continue goes on.", ConflictBannerViewModel.TextFor(stop with { StopKind = RebaseStopKind.ExecFailed }, 0, "main"));
        Assert.Equal("Rebasing feature/x onto main: stopped to edit 078ef05 \"Conflicting change\" (2 of 3). Continue when ready.", ConflictBannerViewModel.TextFor(stop with { StopKind = RebaseStopKind.Edit }, 0, "main"));
        Assert.Equal("Rebasing onto main: stopped at 078ef05 \"Conflicting change\" (2 of 3). 2 files have conflicts.", ConflictBannerViewModel.TextFor(stop with { Branch = null }, 2, "main"));
    }

    [Fact]
    public void Continue_and_Skip_are_offered_state_by_state_with_their_reasons()
    {
        // docs/phase-4-design.md 5.1's table and 10.4's reasons.
        var merge = new InProgressState(InProgressKind.Merge, Conflicting, "Merge branch 'feature/conflict'", null, null, 0, false);
        var single = new InProgressState(InProgressKind.CherryPick, Conflicting, "Conflicting change", null, null, 0, false) { HasPickHead = true };
        var inList = single with { Sequenced = true, Remaining = 1 };
        var paused = inList with { HasPickHead = false, AbortKeepsHead = true };
        var refused = inList with { HasPickHead = false };
        var rebase = new InProgressState(InProgressKind.Rebase, Conflicting, "Conflicting change", "feature/x", MainWork, 0, false) { StopKind = RebaseStopKind.Pick, Done = 1, Total = 1 };
        var am = new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, 0, false);

        Assert.Equal((true, "Resolve the conflicts first (1 file)"), ConflictBannerViewModel.ContinueFor(merge, 1));
        Assert.Equal((true, "Resolve the conflicts first (2 files)"), ConflictBannerViewModel.ContinueFor(single, 2));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.ContinueFor(merge, 0));
        Assert.Equal((false, (string?)null), ConflictBannerViewModel.SkipFor(merge));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.SkipFor(single));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.ContinueFor(inList, 0));
        Assert.Equal((true, "This cherry-pick changes nothing: skip it"), ConflictBannerViewModel.ContinueFor(single with { Empty = true }, 0));
        Assert.Equal((true, "This revert changes nothing: skip it"), ConflictBannerViewModel.ContinueFor(single with { Kind = InProgressKind.Revert, Empty = true }, 0));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.ContinueFor(paused, 0));
        Assert.Equal((true, "There is nothing to skip: the stopped commit was committed"), ConflictBannerViewModel.SkipFor(paused));
        Assert.Equal((true, "Git would leave 078ef05 out without a word: skip it or abort"), ConflictBannerViewModel.ContinueFor(refused, 0));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.SkipFor(refused));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.ContinueFor(rebase, 0));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.SkipFor(rebase));
        Assert.Equal(
            (true, "078ef05 changes nothing now, and git would join the commits squashed into it to the commit before it: abort, or change it"),
            ConflictBannerViewModel.ContinueFor(rebase with { StopKind = RebaseStopKind.Empty, NextIsSquash = true }, 0));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.ContinueFor(rebase with { NextIsSquash = true }, 0));
        Assert.Equal(
            (true, "Skipping 078ef05 would join the commits squashed into it to the commit before it: abort, or resolve it and continue"),
            ConflictBannerViewModel.SkipFor(rebase with { NextIsSquash = true }));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.ContinueFor(rebase with { StopKind = RebaseStopKind.Empty }, 0));
        Assert.Equal((true, "Skip would try 078ef05 again: move or remove what is in the way, or abort"), ConflictBannerViewModel.SkipFor(rebase with { StopKind = RebaseStopKind.Rescheduled }));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.SkipFor(rebase with { StopKind = RebaseStopKind.OwnAmendFailed }));
        Assert.Equal((true, "There is nothing to skip: continue to go on"), ConflictBannerViewModel.SkipFor(rebase with { StopKind = RebaseStopKind.ExecFailed }));
        Assert.Equal((true, "Skip does not leave out a commit that is being edited: continue or abort"), ConflictBannerViewModel.SkipFor(rebase with { StopKind = RebaseStopKind.Edit }));
        Assert.Equal((false, (string?)null), ConflictBannerViewModel.ContinueFor(am, 0));
        Assert.Equal((false, (string?)null), ConflictBannerViewModel.SkipFor(am));
        Assert.Equal((false, (string?)null), ConflictBannerViewModel.ContinueFor(InProgressState.None, 1));
    }

    [Fact]
    public void A_rebases_Continue_is_disabled_while_other_tracked_files_are_unstaged()
    {
        // The review's finding 1: git's --continue refuses while a tracked file that is not
        // conflicted has unstaged changes, so Continue is disabled with its reason, after the
        // conflicts' reason; a merge's or a pick's commit is not concerned.
        const string reason = "Stage or stash the other changed files first: git continues a rebase only when nothing else is unstaged";
        var rebase = new InProgressState(InProgressKind.Rebase, Conflicting, "Conflicting change", "feature/x", MainWork, 0, false) { StopKind = RebaseStopKind.Pick, Done = 1, Total = 1 };
        var merge = new InProgressState(InProgressKind.Merge, Conflicting, "Merge branch 'feature/conflict'", null, null, 0, false);
        var inList = new InProgressState(InProgressKind.CherryPick, Conflicting, "Conflicting change", null, null, 1, false) { HasPickHead = true, Sequenced = true };

        Assert.Equal(reason, OperationTexts.ContinueNeedsNothingUnstaged);
        Assert.Equal((true, reason), ConflictBannerViewModel.ContinueFor(rebase, 0, otherUnstaged: true));
        foreach (var kind in Enum.GetValues<RebaseStopKind>())
        {
            Assert.Equal((true, reason), ConflictBannerViewModel.ContinueFor(rebase with { StopKind = kind }, 0, otherUnstaged: true));
        }

        Assert.Equal((true, "Resolve the conflicts first (1 file)"), ConflictBannerViewModel.ContinueFor(rebase, 1, otherUnstaged: true));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.ContinueFor(merge, 0, otherUnstaged: true));
        Assert.Equal((true, (string?)null), ConflictBannerViewModel.ContinueFor(inList, 0, otherUnstaged: true));

        var banner = new ConflictBannerViewModel(_ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask);
        banner.Show(rebase, 0, "main", "HEAD", otherUnstaged: true);
        Assert.Equal((true, false, reason), (banner.HasContinue, banner.CanContinue, banner.ContinueToolTip));
        Assert.False(banner.ContinueCommand.CanExecute(null));
        banner.Show(rebase, 0, "main", "HEAD");
        Assert.Equal((true, true, (string?)null), (banner.HasContinue, banner.CanContinue, banner.ContinueToolTip));
    }

    [Fact]
    public void Which_unstaged_changes_stop_a_rebases_Continue()
    {
        // Tracked, not conflicted, not a submodule (git's --continue runs diff-files with
        // --ignore-submodules); untracked files and staged changes do not count.
        static WorkingTreeStatus Status(IReadOnlyList<ChangedFile> unstaged, IReadOnlySet<string>? untracked = null, IReadOnlySet<string>? submodules = null) =>
            new([new ChangedFile("staged.txt", FileChangeKind.Modified)], unstaged, untracked ?? new HashSet<string>(StringComparer.Ordinal))
            {
                Submodules = submodules ?? new HashSet<string>(StringComparer.Ordinal),
            };

        Assert.False(Status([]).HasUnstagedTrackedChanges);
        Assert.True(Status([new ChangedFile("g.txt", FileChangeKind.Modified)]).HasUnstagedTrackedChanges);
        Assert.True(Status([new ChangedFile("gone.txt", FileChangeKind.Deleted)]).HasUnstagedTrackedChanges);
        Assert.False(Status([new ChangedFile("f.txt", FileChangeKind.Conflicted)]).HasUnstagedTrackedChanges);
        Assert.False(Status([new ChangedFile("new.txt", FileChangeKind.Added)], untracked: new HashSet<string>(["new.txt"], StringComparer.Ordinal)).HasUnstagedTrackedChanges);
        Assert.False(Status([new ChangedFile("sub", FileChangeKind.Modified)], submodules: new HashSet<string>(["sub"], StringComparer.Ordinal)).HasUnstagedTrackedChanges);
    }

    [Fact]
    public void The_icon_and_text_tooltip_says_what_each_button_does()
    {
        // docs/phase-4-design.md 10.3.
        var merge = new InProgressState(InProgressKind.Merge, Conflicting, "Merge branch 'feature/conflict'", null, null, 0, false);
        var single = new InProgressState(InProgressKind.CherryPick, Conflicting, "Conflicting change", null, null, 0, false) { HasPickHead = true };
        var inList = single with { Sequenced = true, Remaining = 1 };
        var rebase = new InProgressState(InProgressKind.Rebase, Conflicting, "Conflicting change", "feature/rebase", MainWork, 0, false) { StopKind = RebaseStopKind.Pick, Done = 1, Total = 2 };

        Assert.Equal("Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.", ConflictBannerViewModel.ToolTipFor(merge, 1, "main"));
        Assert.Equal(
            "Continue commits the resolved files with the message in the stage panel. Skip leaves this commit out. Abort puts main back where it was before the cherry-pick.",
            ConflictBannerViewModel.ToolTipFor(single, 1, "main"));
        Assert.Equal(
            "Continue commits the resolved files and goes on with the commits that wait. Skip leaves this commit out and goes on. Abort puts main back where it was before the cherry-pick: the commits it already made are dropped too.",
            ConflictBannerViewModel.ToolTipFor(inList, 1, "main"));
        Assert.Equal(
            "Continue picks the commits that wait. Abort ends the cherry-pick and keeps main and the commits made so far.",
            ConflictBannerViewModel.ToolTipFor(inList with { HasPickHead = false, AbortKeepsHead = true }, 0, "main"));
        Assert.Equal(
            "Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts feature/rebase back where it was before the rebase.",
            ConflictBannerViewModel.ToolTipFor(rebase, 1, "HEAD"));
        Assert.Equal(
            "Resolve the files and stage them, then continue in a terminal (git am --continue), or abort.",
            ConflictBannerViewModel.ToolTipFor(new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, 0, false), 1, "main"));
        Assert.Equal("Click a conflicted file to resolve it, then mark it resolved.", ConflictBannerViewModel.ToolTipFor(InProgressState.None, 1, "main"));
    }

    [Fact]
    public void The_view_model_shows_hides_and_offers_its_buttons_as_the_state_says()
    {
        var aborted = new List<InProgressState>();
        var banner = new ConflictBannerViewModel(
            state =>
            {
                aborted.Add(state);
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask,
            _ => Task.CompletedTask);
        Assert.False(banner.IsVisible);

        banner.Show(InProgressState.None, 1, null);
        Assert.True(banner.IsVisible);
        Assert.False(banner.HasAbort);
        Assert.False(banner.HasContinue);
        Assert.False(banner.HasSkip);
        Assert.False(banner.AbortCommand.CanExecute(null));
        Assert.Equal("Click a conflicted file to resolve it, then mark it resolved.", banner.ToolTip);

        var merge = new InProgressState(InProgressKind.Merge, Conflicting, "Merge branch 'feature/conflict'", null, null, 0, false);
        banner.Show(merge, 1, null, "main");
        Assert.Equal("Abort merge", banner.AbortText);
        Assert.Equal((true, false, "Resolve the conflicts first (1 file)"), (banner.HasContinue, banner.CanContinue, banner.ContinueToolTip));
        Assert.False(banner.ContinueCommand.CanExecute(null));
        Assert.False(banner.HasSkip);
        Assert.Equal("Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.", banner.ToolTip);
        banner.AbortCommand.Execute(null);
        Assert.Equal([merge], aborted);

        banner.Show(merge, 0, null, "main");
        Assert.Equal((true, true, (string?)null), (banner.HasContinue, banner.CanContinue, banner.ContinueToolTip));
        Assert.True(banner.ContinueCommand.CanExecute(null));

        banner.Show(InProgressState.None, 0, null);
        Assert.False(banner.IsVisible);
        Assert.False(banner.HasAbort);
        Assert.False(banner.HasContinue);
    }

    [Fact]
    public void Continue_and_Skip_go_to_their_delegates_with_the_state_shown_and_a_rebases_onto_name_is_kept()
    {
        var calls = new List<string>();
        var banner = new ConflictBannerViewModel(
            _ => Task.CompletedTask,
            state =>
            {
                calls.Add($"continue {state.Kind}");
                return Task.CompletedTask;
            },
            state =>
            {
                calls.Add($"skip {state.Kind}");
                return Task.CompletedTask;
            });
        var rebase = new InProgressState(InProgressKind.Rebase, Conflicting, "Conflicting change", "feature/rebase", Conflicting, 1, false) { StopKind = RebaseStopKind.Pick, Done = 1, Total = 2 };

        banner.ContinueCommand.Execute(null);
        banner.Show(rebase, 0, "main");
        banner.ContinueCommand.Execute(null);
        banner.SkipCommand.Execute(null);

        Assert.Equal(["continue Rebase", "skip Rebase"], calls);
        Assert.Equal("main", banner.OntoName);
        Assert.Equal(ConflictBannerViewModel.TextFor(rebase, 0, "main"), banner.Text);
        banner.Show(InProgressState.None, 0, null);
        Assert.Null(banner.OntoName);
    }

    [Fact]
    public void The_Abort_question_names_what_is_lost_and_keeps_HEAD_when_git_keeps_it()
    {
        var merge = new InProgressState(InProgressKind.Merge, Conflicting, "Merge branch 'feature/conflict'", null, null, 0, false);
        var pick = new InProgressState(InProgressKind.CherryPick, Conflicting, "Conflicting change", null, null, 1, false) { HasPickHead = true, Sequenced = true };
        var committed = new InProgressState(InProgressKind.CherryPick, null, null, null, null, 1, false) { AbortKeepsHead = true, Sequenced = true };
        var revert = new InProgressState(InProgressKind.Revert, MainWork, "Main work", null, null, 0, false) { HasPickHead = true };
        var rebase = new InProgressState(InProgressKind.Rebase, null, null, "feature/conflict", MainWork, 0, false);

        Assert.Equal(
            new ConfirmationRequest("Abort the merge?", "Abort merging \"Merge branch 'feature/conflict'\"? Changes made to resolve conflicts are lost.", "A backup is saved first, so this can be undone.", "Abort merge"),
            ConflictBannerViewModel.AbortConfirmation(merge));
        Assert.Equal(
            new ConfirmationRequest("Abort the cherry-pick?", "Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost. Commits this cherry-pick already made are dropped too.", "A backup is saved first, so this can be undone.", "Abort cherry-pick"),
            ConflictBannerViewModel.AbortConfirmation(pick));
        Assert.Equal(
            "Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost.",
            ConflictBannerViewModel.AbortConfirmation(pick with { Remaining = 0, Empty = true, Sequenced = false }).Question);
        Assert.Equal(
            "Abort the cherry-pick? The 1 commit left is not picked. HEAD and the commits made so far stay as they are.",
            ConflictBannerViewModel.AbortConfirmation(committed).Question);
        Assert.Equal(
            "Abort the revert? The 2 commits left are not reverted. HEAD and the commits made so far stay as they are.",
            ConflictBannerViewModel.AbortConfirmation(committed with { Kind = InProgressKind.Revert, Remaining = 2 }).Question);
        Assert.Equal(
            new ConfirmationRequest("Abort the revert?", "Abort the revert? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost.", "A backup is saved first, so this can be undone.", "Abort revert"),
            ConflictBannerViewModel.AbortConfirmation(revert));
        Assert.Equal(
            new ConfirmationRequest("Abort the rebase?", "Abort the rebase of feature/conflict? The branch goes back to where it was before it started.", "A backup is saved first, so this can be undone.", "Abort rebase"),
            ConflictBannerViewModel.AbortConfirmation(rebase));

        // A rebase that "Check out {A} and rebase it" began checks the user's branch out again (1.3, 14.3).
        Assert.Equal(
            "Abort the rebase of feature/conflict? The branch goes back to where it was before it started, and main is checked out again.",
            ConflictBannerViewModel.AbortConfirmation(rebase, returnBranch: "main").Question);
        Assert.All(
            new[] { merge, pick, revert, rebase }.Select(state => ConflictBannerViewModel.AbortConfirmation(state)),
            request => Assert.True(request.IsDestructive));

        // git am (2.15, section 5): its own title and question, the button "Abort git am"; the
        // banner's own button says "Abort".
        var am = new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, 0, false);
        Assert.Equal(
            new ConfirmationRequest("Abort git am?", "Abort git am? HEAD goes back to where it was before it started, and the patches not applied yet are dropped.", "A backup is saved first, so this can be undone.", "Abort git am"),
            ConflictBannerViewModel.AbortConfirmation(am));
        Assert.Equal("Abort", ConflictBannerViewModel.AbortButtonText(am.Kind));
        Assert.Throws<ArgumentException>(() => ConflictBannerViewModel.AbortConfirmation(InProgressState.None));
    }

    [Theory]
    [InlineData(440, 200, true)]
    [InlineData(840, 300, false)]
    [InlineData(560, 268, false)]
    [InlineData(560, 269, true)]
    [InlineData(440, 0, false)]
    public void The_buttons_stand_beside_the_text_while_it_keeps_240_px(double width, double buttons, bool below)
    {
        // 5.2 (amending D122): 36 for the icon, 8 for the text's margin, the buttons, 8 at the right edge.
        Assert.Equal(below, Views.Shell.BannerLayoutPanel.PutsButtonsBelow(width, buttons));
    }

    [AvaloniaFact]
    public async Task A_stopped_merge_shows_the_banner_above_the_graph_and_the_conflict_in_the_stage_panel()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await BannerShownAsync(app);

        // The banner: above the graph's column headers, as wide as the graph area, the warning
        // icon, the text, Continue and Abort; its colours are the tokens of 6.4. In the 440-wide
        // graph area the buttons stand on a row of their own under the text (phase 4 design 5.2).
        var banner = app.FindByAutomationId("ConflictBanner");
        var text = (TextBlock)app.FindByAutomationId("BannerText");
        var abort = (Button)app.FindByAutomationId("BannerAbortButton");
        var @continue = (Button)app.FindByAutomationId("BannerContinueButton");
        Assert.Equal("Merging: \"Merge branch 'feature/conflict'\". 1 file has conflicts.", text.Text);
        Assert.Equal("Abort merge", abort.Content);
        Assert.Equal("Abort merge", AutomationName(abort));
        Assert.Equal(("Continue", false, "Resolve the conflicts first (1 file)"), (@continue.Content, @continue.IsEffectivelyEnabled, ToolTip.GetTip(@continue)));
        Assert.True(ToolTip.GetShowOnDisabled(@continue));
        Assert.False(app.FindByAutomationId("BannerSkipButton").IsVisible);
        var bounds = app.BoundsOf(banner);
        var headers = app.BoundsOf(app.Find<Border>("ColumnHeaders"));
        Assert.Equal(bounds.Bottom, headers.Top, 0.5);
        Assert.Equal(app.BoundsOf(app.FindByAutomationId("CommitGraph")).Width, bounds.Width, 0.5);
        Assert.True(bounds.Height > 36, $"The banner is {bounds.Height} high.");
        Assert.Equal(bounds.Right - 8, app.BoundsOf(abort).Right, 0.5);
        Assert.Equal(app.BoundsOf(@continue).Right + 6, app.BoundsOf(abort).Left, 0.5);
        Assert.Equal((26.0, 26.0), (app.BoundsOf(@continue).Height, app.BoundsOf(abort).Height));
        Assert.True(app.BoundsOf(@continue).Top >= app.BoundsOf(text).Bottom - 0.5, "The buttons are not under the text.");
        Assert.True(app.Find<Views.Shell.BannerLayoutPanel>("BannerLayout").ButtonsBelow);
        Assert.DoesNotContain(LayoutAudit.FindClippedText(app.Window), problem => problem.Contains("Merging", StringComparison.Ordinal));
        Assert.Equal(
            "Continue commits the merge with the message in the stage panel. Abort puts main back where it was before the merge.",
            ToolTip.GetTip(app.Find<Border>("BannerInfo")));
        AssertColours(app, banner);
        Assert.Equal("Working changes · 1 conflict", repository.WorkingRowText);
        Assert.Equal("Working changes · 1 conflict", app.Find<CommitGraphControl>("GraphRows").WorkingRowText);
        Pictures(app, "banner-merge");

        // The stage panel (check 9 step 4): the title counts the conflict, the row says what to
        // do, the message comes from MERGE_MSG without its comments, and nothing can be committed.
        repository.IsWorkingRowSelected = true;
        await app.WaitForAsync(() => repository.Changes.Summary.Length > 0, "the message from MERGE_MSG");
        app.Settle();
        Assert.Equal("Unstaged files (1, 1 with conflicts)", app.Find<TextBlock>("UnstagedTitle").Text);
        Assert.Equal("Staged files (0)", app.Find<TextBlock>("StagedTitle").Text);
        var row = repository.Changes.Unstaged.Rows.Single();
        Assert.Equal(("U", true), (row.StatusLetter, row.IsDeleted));
        Assert.Equal("Conflict: click to resolve it", row.RowToolTip);
        Assert.Equal("Merge branch 'feature/conflict'", repository.Changes.Summary);
        Assert.Equal(string.Empty, repository.Changes.Description);
        var commit = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal("Stage files to commit", commit.Content);
        Assert.False(commit.IsEffectivelyEnabled);
        Assert.True(app.FindByAutomationId("AmendCheckBox").IsEffectivelyEnabled);
        Pictures(app, "banner-merge-stage-panel");

        // Resolved and staged: the banner says so, Continue is enabled, the row loses its count,
        // and the merge commits (phase 3's Commit stays for a merge, 5.3).
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        await app.WaitForAsync(() => text.Text!.EndsWith("Conflicts resolved: commit to finish the merge.", StringComparison.Ordinal), "the resolved banner");
        Assert.Equal("Working changes", repository.WorkingRowText);
        Assert.Equal("Commit 1 file", commit.Content);
        Assert.True(commit.IsEffectivelyEnabled);
        Assert.True(@continue.IsEffectivelyEnabled);
        Assert.Null(ToolTip.GetTip(@continue));
    }

    [AvaloniaFact]
    public async Task Abort_asks_section_5s_question_and_hands_the_state_to_the_actions_only_when_confirmed()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await BannerShownAsync(app);
        var actions = new RecordingActions();
        repository.InProgressActions = actions;

        app.Click(app.FindByAutomationId("BannerAbortButton"));
        var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal(
            ("Abort the merge?", "Abort merging \"Merge branch 'feature/conflict'\"? Changes made to resolve conflicts are lost.", "A backup is saved first, so this can be undone.", "Abort merge", true),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive));
        Pictures(app, "banner-abort-question");
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is null, "the question to close");
        Assert.Empty(actions.Aborted);

        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => actions.Aborted.Count == 1, "the abort");
        Assert.Equal(InProgressKind.Merge, actions.Aborted[0].Kind);
        Assert.Equal("Merge branch 'feature/conflict'", actions.Aborted[0].Subject);
    }

    [AvaloniaFact]
    public async Task A_rebases_banner_offers_Continue_and_Skip_and_Skip_asks_first()
    {
        // docs/phase-4-design.md 5.1, 5.2, 7.2: the repository names the branch at onto, Skip's
        // question comes before the actions get it, and Continue is enabled once nothing conflicts.
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Rebase);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await BannerShownAsync(app);
        var actions = new RecordingActions();
        repository.InProgressActions = actions;
        var text = (TextBlock)app.FindByAutomationId("BannerText");

        Assert.Equal("main", repository.Banner.OntoName);
        Assert.Equal("Rebasing feature/conflict onto main: stopped at 078ef05 \"Conflicting change\" (1 of 1). 1 file has conflicts.", text.Text);
        var @continue = (Button)app.FindByAutomationId("BannerContinueButton");
        var skip = (Button)app.FindByAutomationId("BannerSkipButton");
        Assert.Equal(("Continue", false, "Resolve the conflicts first (1 file)"), (@continue.Content, @continue.IsEffectivelyEnabled, ToolTip.GetTip(@continue)));
        Assert.Equal(("Skip", true), (skip.Content, skip.IsEffectivelyEnabled));
        Assert.Equal(
            "Continue commits the resolved files and replays the rest. Skip leaves this commit out. Abort puts feature/conflict back where it was before the rebase.",
            ToolTip.GetTip(app.Find<Border>("BannerInfo")));
        Assert.DoesNotContain(LayoutAudit.FindClippedText(app.Window), problem => problem.Contains("Rebasing", StringComparison.Ordinal));
        Pictures(app, "banner-rebase");

        // Skip asks first (7.2); Cancel keeps everything.
        app.Click(skip);
        var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal(
            ("Skip 078ef05?", "\"Conflicting change\" is left out of the rebase, and changes made to resolve its conflicts are lost.", "A backup is saved first, so this can be undone.", "Skip", true),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive));
        Assert.Equal("Cancel", question.CancelText);
        Pictures(app, "banner-skip-question");
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is null, "the question to close");
        Assert.Empty(actions.Skipped);

        app.Click(skip);
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await app.WaitForAsync(() => actions.Skipped.Count == 1, "the skip");
        Assert.Equal((InProgressKind.Rebase, "feature/conflict"), (actions.Skipped[0].Kind, actions.Skipped[0].Branch));

        // Resolved outside the app: Continue is enabled and goes to the actions with the operation's own message.
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        await app.WaitForAsync(() => text.Text!.EndsWith("Conflicts resolved: continue to commit it and go on.", StringComparison.Ordinal), "the resolved banner");
        app.Settle();
        Assert.True(@continue.IsEffectivelyEnabled);
        app.Click(@continue);
        await app.WaitForAsync(() => actions.Continued.Count == 1, "Continue");
        Assert.Equal((InProgressKind.Rebase, "feature/conflict", (string?)null), (actions.Continued[0].State.Kind, actions.Continued[0].State.Branch, actions.Continued[0].Message));
        Assert.Empty(actions.Aborted);
    }

    [AvaloniaFact]
    public async Task A_cherry_pick_sequence_from_a_terminal_shows_the_commits_left_and_the_question_that_drops_them()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.CherryPick);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await BannerShownAsync(app);

        Assert.Equal(
            "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts. 1 more commit waits: Continue picks it.",
            ((TextBlock)app.FindByAutomationId("BannerText")).Text);
        Assert.Equal("Abort cherry-pick", ((Button)app.FindByAutomationId("BannerAbortButton")).Content);
        Assert.True(app.FindByAutomationId("BannerContinueButton").IsVisible);
        Assert.True(app.FindByAutomationId("BannerSkipButton").IsEffectivelyEnabled);
        Assert.Equal(
            "Continue commits the resolved files and goes on with the commits that wait. Skip leaves this commit out and goes on. Abort puts main back where it was before the cherry-pick: the commits it already made are dropped too.",
            ToolTip.GetTip(app.Find<Border>("BannerInfo")));
        Assert.DoesNotContain(LayoutAudit.FindClippedText(app.Window), problem => problem.Contains("Cherry-picking", StringComparison.Ordinal));
        Pictures(app, "banner-cherry-pick-sequence");

        // The stage panel continues the list (5.3): "Continue cherry-pick", enabled as Continue is.
        repository.IsWorkingRowSelected = true;
        await app.WaitForAsync(() => repository.Changes.Summary.Length > 0, "the message from MERGE_MSG");
        app.Settle();
        Assert.Equal(("Conflicting change", string.Empty), (repository.Changes.Summary, repository.Changes.Description));
        Assert.Equal("Unstaged files (1, 1 with conflicts)", repository.Changes.UnstagedTitle);
        var commit = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal(("Continue cherry-pick", false, "Resolve the conflicts first (1 file)"), (commit.Content, commit.IsEffectivelyEnabled, ToolTip.GetTip(commit)));
        var amend = app.FindByAutomationId("AmendCheckBox");
        Assert.Equal((false, "Finish or abort the cherry-pick first"), (amend.IsEffectivelyEnabled, ToolTip.GetTip(amend)));

        repository.InProgressActions = new RecordingActions();
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal(
            "Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost. Commits this cherry-pick already made are dropped too.",
            question.Question);
        app.Click(app.FindByAutomationId("CancelButton"));
    }

    [AvaloniaFact]
    public async Task A_revert_shows_its_banner_and_fills_the_message_with_its_description()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Revert);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await BannerShownAsync(app);

        Assert.Equal("Reverting 88d6b91 \"Main work\". 1 file has conflicts.", ((TextBlock)app.FindByAutomationId("BannerText")).Text);
        Assert.Equal("Abort revert", ((Button)app.FindByAutomationId("BannerAbortButton")).Content);
        Pictures(app, "banner-revert");

        repository.IsWorkingRowSelected = true;
        await app.WaitForAsync(() => repository.Changes.Summary.Length > 0, "the message from MERGE_MSG");
        var mainWork = (await repo.GitAsync("rev-parse", "refs/tags/v1.1")).StandardOutput.Trim();
        Assert.Equal(("Revert \"Main work\"", $"This reverts commit {mainWork}."), (repository.Changes.Summary, repository.Changes.Description));
        Pictures(app, "banner-revert-stage-panel");
    }

    [AvaloniaFact]
    public async Task Conflicts_without_an_operation_show_the_banner_without_a_button()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.None);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await BannerShownAsync(app);

        Assert.Equal("1 file has conflicts. Resolve it, then stage it.", ((TextBlock)app.FindByAutomationId("BannerText")).Text);
        Assert.False(app.FindByAutomationId("BannerAbortButton").IsVisible);
        Assert.Equal(36, app.BoundsOf(app.FindByAutomationId("ConflictBanner")).Height, 0.5);
        repository.IsWorkingRowSelected = true;
        app.Settle();
        Assert.Equal((string.Empty, string.Empty), (repository.Changes.Summary, repository.Changes.Description));
        Pictures(app, "banner-stash-conflicts");
    }

    [AvaloniaFact]
    public async Task A_merge_started_in_a_terminal_while_the_app_is_open_brings_the_banner_and_an_abort_in_a_terminal_takes_it_away()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1920, height: 1080);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        Assert.False(app.FindByAutomationId("ConflictBanner").IsEffectivelyVisible);

        Assert.Equal(1, (await repo.GitAllowingFailureAsync("merge", "feature/conflict")).ExitCode);

        // A refresh reads git status before MERGE_HEAD: one whose status came before the merge's
        // conflicted index and whose MERGE_HEAD came after it shows "Conflicts resolved" until the
        // watcher's next refresh (the first attempt of CI run 41172878322). So the wait is for the
        // merge's own text.
        const string merging = "Merging: \"Merge branch 'feature/conflict'\". 1 file has conflicts.";
        await app.WaitForAsync(() => repository.Banner.IsVisible && repository.Banner.Text == merging, "the banner with the merge's conflict");
        app.Settle();

        // In a wide window the text fits on one line with Continue and Abort beside it: the banner
        // is 36 high (section 5; phase 4 design 5.2).
        Assert.Equal(36, app.BoundsOf(app.FindByAutomationId("ConflictBanner")).Height, 0.5);
        Assert.Equal(merging, ((TextBlock)app.FindByAutomationId("BannerText")).Text);
        Assert.False(app.Find<Views.Shell.BannerLayoutPanel>("BannerLayout").ButtonsBelow);
        Assert.True(app.BoundsOf(app.FindByAutomationId("BannerContinueButton")).Left >= app.BoundsOf(app.FindByAutomationId("BannerText")).Right - 0.5);

        await repo.GitAsync("merge", "--abort");
        await app.WaitForAsync(() => !repository.Banner.IsVisible, "the banner to go");
        app.Settle();
        Assert.False(app.FindByAutomationId("ConflictBanner").IsEffectivelyVisible);
        Assert.Equal("Working changes", repository.WorkingRowText);
    }

    [AvaloniaFact]
    public async Task A_rebase_stopped_in_a_terminal_fills_the_stage_panels_message_also_after_a_refresh_saw_the_stop_before_its_message()
    {
        // Git names the stopped pick in rebase-merge/done before it merges and writes the message
        // (the first attempt of CI run 41172878322). A merge driver that waits for a file holds git
        // there until the app has read the stop; its loop ends by itself after 1,200 short sleeps,
        // a minute on Linux and macOS and about two on Windows, where each sleep starts a process.
        using var repo = await Scenarios.ConflictsAsync();
        using var held = new TempDirectory("held");
        var go = Path.Combine(held.Path, "go").Replace('\\', '/');
        Directory.CreateDirectory(Path.Combine(repo.Path, ".git", "info"));
        File.WriteAllText(Path.Combine(repo.Path, ".git", "info", "attributes"), "notes.txt merge=held\n");
        await repo.GitAsync("config", "merge.held.driver", $"i=0; while [ ! -f '{go}' ] && [ $i -lt 1200 ]; do sleep 0.05; i=$((i+1)); done; exit 1");
        await repo.GitAsync("switch", "feature/picks");
        var conflicting = (await repo.GitAsync("rev-parse", "feature/picks~1")).StandardOutput.Trim();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");

        var rebase = repo.GitAllowingFailureAsync("rebase", "main");
        GitResult rebased;
        try
        {
            await app.WaitForAsync(
                () => repository.Status.InProgress is { Kind: InProgressKind.Rebase } stop && stop.Commit == conflicting && !repository.Changes.IsFillingMessage,
                "a refresh that sees the rebase at \"Pick conflicting\"");
            Assert.False(File.Exists(Path.Combine(repo.Path, ".git", "rebase-merge", "message")));
            Assert.Equal((string.Empty, string.Empty), (repository.Changes.Summary, repository.Changes.Description));
        }
        finally
        {
            File.WriteAllText(go, string.Empty);
            rebased = await rebase;
        }

        Assert.Equal(1, rebased.ExitCode);

        // Git writes the message, the watcher's refresh follows, and the panel reads it again.
        await app.WaitForAsync(() => repository.Changes.Summary == "Pick conflicting", "the stage panel's summary \"Pick conflicting\"");
        Assert.Equal(string.Empty, repository.Changes.Description);
        Assert.Equal(conflicting, repository.Status.InProgress.Commit);
    }

    private static async Task<RepositoryViewModel> BannerShownAsync(ShellDriver app)
    {
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0 && repository.Banner.IsVisible, "the graph and the banner");
        await app.WaitForIdleAsync(repository);
        app.MoveMouse(new Point(130, 687));
        app.Settle();
        return repository;
    }

    /// <summary>The background is <c>VcBannerBackgroundBrush</c>, the bottom line <c>VcWarningBrush</c>, the icon drawn in the warning colour.</summary>
    private static void AssertColours(ShellDriver app, Control banner)
    {
        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            app.Window.RequestedThemeVariant = theme;
            app.Settle();
            var picture = app.Capture();
            var bounds = app.BoundsOf(banner);
            Assert.Equal(Brush(app, "VcBannerBackgroundBrush"), picture.PixelAt(new Point(bounds.X + 4, bounds.Y + 4)));
            Assert.Equal(Brush(app, "VcWarningBrush"), picture.PixelAt(new Point(bounds.X + 4, bounds.Bottom - 0.5)));
            var icon = app.BoundsOf(app.Find<Icon>("BannerIcon"));
            Assert.Equal(new Size(16, 16), icon.Size);
            Assert.Equal(12, icon.X - bounds.X, 0.5);
            Assert.Equal(Brush(app, "VcWarningBrush"), ((ISolidColorBrush)app.Find<Icon>("BannerIcon").Foreground!).Color);
        }

        app.Window.RequestedThemeVariant = ThemeVariant.Dark;
        app.Settle();
    }

    private static string? AutomationName(Control control) => Avalonia.Automation.AutomationProperties.GetName(control);

    private static Color Brush(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");

    private static void Pictures(ShellDriver app, string name)
    {
        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            app.Window.RequestedThemeVariant = theme;
            app.Settle();
            TestPictures.Save(app, $"{name}-{(theme == ThemeVariant.Dark ? "dark" : "light")}");
        }

        app.Window.RequestedThemeVariant = ThemeVariant.Dark;
        app.Settle();
    }

    /// <summary>The banner's actions, recorded.</summary>
    private sealed class RecordingActions : IInProgressActions
    {
        public List<InProgressState> Aborted { get; } = [];

        public List<(InProgressState State, string? Message)> Continued { get; } = [];

        public List<InProgressState> Skipped { get; } = [];

        public Task AbortAsync(InProgressState state)
        {
            Aborted.Add(state);
            return Task.CompletedTask;
        }

        public Task ContinueAsync(InProgressState state, string? message)
        {
            Continued.Add((state, message));
            return Task.CompletedTask;
        }

        public Task SkipAsync(InProgressState state)
        {
            Skipped.Add(state);
            return Task.CompletedTask;
        }
    }
}
