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
    [InlineData(1, 0, "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts.")]
    [InlineData(1, 1, "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts. 1 more commit waits: continue it in a terminal (git cherry-pick --continue).")]
    [InlineData(0, 1, "Cherry-picking 078ef05 \"Conflicting change\". Conflicts resolved: commit to finish the cherry-pick. 1 more commit waits: continue it in a terminal (git cherry-pick --continue).")]
    [InlineData(1, 2, "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts. 2 more commits wait: continue them in a terminal (git cherry-pick --continue).")]
    [InlineData(0, 0, "Cherry-picking 078ef05 \"Conflicting change\". Conflicts resolved: commit to finish the cherry-pick.")]
    public void A_cherry_pick_names_its_commit_and_the_commits_a_terminal_sequence_has_left(int conflicts, int remaining, string text)
    {
        var state = new InProgressState(InProgressKind.CherryPick, Conflicting, "Conflicting change", null, null, remaining, false);

        Assert.Equal(text, ConflictBannerViewModel.TextFor(state, conflicts));
        Assert.Equal("Abort cherry-pick", ConflictBannerViewModel.AbortButtonText(state.Kind));
    }

    [Theory]
    [InlineData(InProgressKind.CherryPick, 1, "Cherry-pick stopped with 1 commit left: continue in a terminal, or abort.")]
    [InlineData(InProgressKind.CherryPick, 2, "Cherry-pick stopped with 2 commits left: continue in a terminal, or abort.")]
    [InlineData(InProgressKind.CherryPick, 0, "Cherry-pick stopped with no commits left: abort to end it.")]
    [InlineData(InProgressKind.Revert, 1, "Revert stopped with 1 commit left: continue in a terminal, or abort.")]
    public void A_sequence_stopped_between_commits_says_how_many_are_left(InProgressKind kind, int remaining, string text)
    {
        var state = new InProgressState(kind, null, null, null, null, remaining, false) { AbortKeepsHead = true };

        Assert.Equal(text, ConflictBannerViewModel.TextFor(state, 0));
    }

    [Fact]
    public void An_empty_pick_says_it_changes_nothing_and_to_abort()
    {
        var pick = new InProgressState(InProgressKind.CherryPick, "dff8fbf0000000000000000000000000000000aa", "Alpha one", null, null, 0, true);
        var revert = pick with { Kind = InProgressKind.Revert, Subject = "Main work" };

        Assert.Equal("Cherry-picking dff8fbf \"Alpha one\" changes nothing: its changes are already here. Abort to go on.", ConflictBannerViewModel.TextFor(pick, 0));
        Assert.Equal("Reverting dff8fbf \"Main work\" changes nothing: it is already undone. Abort to go on.", ConflictBannerViewModel.TextFor(revert, 0));
    }

    [Fact]
    public void A_revert_a_rebase_git_am_and_conflicts_without_an_operation_have_their_own_texts()
    {
        var revert = new InProgressState(InProgressKind.Revert, MainWork, "Main work", null, null, 0, false);
        var rebase = new InProgressState(InProgressKind.Rebase, null, null, "feature/conflict", MainWork, 0, false);
        var am = new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, 1, false);

        Assert.Equal("Reverting 88d6b91 \"Main work\". 1 file has conflicts.", ConflictBannerViewModel.TextFor(revert, 1));
        Assert.Equal("Reverting 88d6b91 \"Main work\". Conflicts resolved: commit to finish the revert. 2 more commits wait: continue them in a terminal (git revert --continue).", ConflictBannerViewModel.TextFor(revert with { Remaining = 2 }, 0));
        Assert.Equal("Rebasing feature/conflict onto 88d6b91. 1 file has conflicts.", ConflictBannerViewModel.TextFor(rebase, 1));
        Assert.Equal("Rebasing feature/conflict onto 88d6b91. Conflicts resolved: continue in a terminal (git rebase --continue), or abort.", ConflictBannerViewModel.TextFor(rebase, 0));
        Assert.Equal("Applying patches (git am). 1 file has conflicts.", ConflictBannerViewModel.TextFor(am, 1));
        Assert.Equal("1 file has conflicts. Resolve it, then stage it.", ConflictBannerViewModel.TextFor(InProgressState.None, 1));
        Assert.Equal("3 files have conflicts. Resolve them, then stage them.", ConflictBannerViewModel.TextFor(InProgressState.None, 3));
        Assert.Null(ConflictBannerViewModel.TextFor(InProgressState.None, 0));
        Assert.Equal(["Abort revert", "Abort rebase", "Abort", ""], new[] { revert, rebase, am, InProgressState.None }.Select(state => ConflictBannerViewModel.AbortButtonText(state.Kind)));
    }

    [Fact]
    public void The_view_model_shows_hides_and_offers_Abort_only_while_an_operation_is_in_progress()
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
        Assert.False(banner.AbortCommand.CanExecute(null));
        Assert.Equal(ConflictBannerViewModel.ConflictsToolTip, banner.ToolTip);

        var merge = new InProgressState(InProgressKind.Merge, Conflicting, "Merge branch 'feature/conflict'", null, null, 0, false);
        banner.Show(merge, 1, null);
        Assert.Equal("Abort merge", banner.AbortText);
        Assert.Equal(
            "Continue and Skip, and the conflict resolver, come in a later version. Resolve the files in your editor and stage them, or abort.",
            banner.ToolTip);
        banner.AbortCommand.Execute(null);
        Assert.Equal([merge], aborted);

        banner.Show(InProgressState.None, 0, null);
        Assert.False(banner.IsVisible);
        Assert.False(banner.HasAbort);
    }

    [Fact]
    public void Continue_and_Skip_go_to_their_delegates_with_the_state_shown_and_a_rebases_onto_name_is_kept()
    {
        // Phase 4's contract (docs/phase-4-design.md 5.2, 5.4): no button shows them yet.
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
        var rebase = new InProgressState(InProgressKind.Rebase, null, null, "feature/rebase", Conflicting, 1, false);

        banner.ContinueCommand.Execute(null);
        banner.Show(rebase, 1, "main");
        banner.ContinueCommand.Execute(null);
        banner.SkipCommand.Execute(null);

        Assert.Equal(["continue Rebase", "skip Rebase"], calls);
        Assert.Equal("main", banner.OntoName);
        Assert.Equal(ConflictBannerViewModel.TextFor(rebase, 1), banner.Text);
        banner.Show(InProgressState.None, 0, null);
        Assert.Null(banner.OntoName);
    }

    [Fact]
    public void The_Abort_question_names_what_is_lost_and_keeps_HEAD_when_git_keeps_it()
    {
        var merge = new InProgressState(InProgressKind.Merge, Conflicting, "Merge branch 'feature/conflict'", null, null, 0, false);
        var pick = new InProgressState(InProgressKind.CherryPick, Conflicting, "Conflicting change", null, null, 1, false);
        var committed = new InProgressState(InProgressKind.CherryPick, null, null, null, null, 1, false) { AbortKeepsHead = true };
        var revert = new InProgressState(InProgressKind.Revert, MainWork, "Main work", null, null, 0, false);
        var rebase = new InProgressState(InProgressKind.Rebase, null, null, "feature/conflict", MainWork, 0, false);

        Assert.Equal(
            new ConfirmationRequest("Abort the merge?", "Abort merging \"Merge branch 'feature/conflict'\"? Changes made to resolve conflicts are lost.", "A backup is saved first, so this can be undone.", "Abort merge"),
            ConflictBannerViewModel.AbortConfirmation(merge, hasSequencer: false));
        Assert.Equal(
            new ConfirmationRequest("Abort the cherry-pick?", "Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost. Commits this cherry-pick already made are dropped too.", "A backup is saved first, so this can be undone.", "Abort cherry-pick"),
            ConflictBannerViewModel.AbortConfirmation(pick, hasSequencer: true));
        Assert.Equal(
            "Abort the cherry-pick? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost.",
            ConflictBannerViewModel.AbortConfirmation(pick with { Remaining = 0, Empty = true }, hasSequencer: false).Question);
        Assert.Equal(
            "Abort the cherry-pick? The 1 commit left is not picked. HEAD and the commits made so far stay as they are.",
            ConflictBannerViewModel.AbortConfirmation(committed, hasSequencer: true).Question);
        Assert.Equal(
            "Abort the revert? The 2 commits left are not reverted. HEAD and the commits made so far stay as they are.",
            ConflictBannerViewModel.AbortConfirmation(committed with { Kind = InProgressKind.Revert, Remaining = 2 }, hasSequencer: true).Question);
        Assert.Equal(
            new ConfirmationRequest("Abort the revert?", "Abort the revert? HEAD goes back to where it was before it started, and changes made to resolve conflicts are lost.", "A backup is saved first, so this can be undone.", "Abort revert"),
            ConflictBannerViewModel.AbortConfirmation(revert, hasSequencer: false));
        Assert.Equal(
            new ConfirmationRequest("Abort the rebase?", "Abort the rebase of feature/conflict? The branch goes back to where it was before it started.", "A backup is saved first, so this can be undone.", "Abort rebase"),
            ConflictBannerViewModel.AbortConfirmation(rebase, hasSequencer: false));
        Assert.All(
            new[] { merge, pick, revert, rebase }.Select(state => ConflictBannerViewModel.AbortConfirmation(state, hasSequencer: false)),
            request => Assert.True(request.IsDestructive));

        // git am (2.15, section 5): its own title and question, the button "Abort git am"; the
        // banner's own button says "Abort".
        var am = new InProgressState(InProgressKind.ApplyMailbox, null, null, null, null, 0, false);
        Assert.Equal(
            new ConfirmationRequest("Abort git am?", "Abort git am? HEAD goes back to where it was before it started, and the patches not applied yet are dropped.", "A backup is saved first, so this can be undone.", "Abort git am"),
            ConflictBannerViewModel.AbortConfirmation(am, hasSequencer: false));
        Assert.Equal("Abort", ConflictBannerViewModel.AbortButtonText(am.Kind));
        Assert.Throws<ArgumentException>(() => ConflictBannerViewModel.AbortConfirmation(InProgressState.None, hasSequencer: false));
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
        // icon, the text, Abort at the right; its colours are the tokens of 6.4.
        var banner = app.FindByAutomationId("ConflictBanner");
        var text = (TextBlock)app.FindByAutomationId("BannerText");
        var abort = (Button)app.FindByAutomationId("BannerAbortButton");
        Assert.Equal("Merging: \"Merge branch 'feature/conflict'\". 1 file has conflicts.", text.Text);
        Assert.Equal("Abort merge", abort.Content);
        Assert.Equal("Abort merge", AutomationName(abort));
        var bounds = app.BoundsOf(banner);
        var headers = app.BoundsOf(app.Find<Border>("ColumnHeaders"));
        Assert.Equal(bounds.Bottom, headers.Top, 0.5);
        Assert.Equal(app.BoundsOf(app.FindByAutomationId("CommitGraph")).Width, bounds.Width, 0.5);
        Assert.True(bounds.Height >= 36, $"The banner is {bounds.Height} high.");
        Assert.True(app.BoundsOf(abort).Right <= bounds.Right - 7.5);
        Assert.DoesNotContain(LayoutAudit.FindClippedText(app.Window), problem => problem.Contains("Merging", StringComparison.Ordinal));
        Assert.Equal(ConflictBannerViewModel.OperationToolTip, ToolTip.GetTip(app.Find<Border>("BannerInfo")));
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
        Assert.Equal("Conflict: resolve it in your editor, then stage it", row.RowToolTip);
        Assert.Equal("Merge branch 'feature/conflict'", repository.Changes.Summary);
        Assert.Equal(string.Empty, repository.Changes.Description);
        var commit = (Button)app.FindByAutomationId("CommitButton");
        Assert.Equal("Stage files to commit", commit.Content);
        Assert.False(commit.IsEffectivelyEnabled);
        Pictures(app, "banner-merge-stage-panel");

        // Resolved and staged: the banner says so, the row loses its count, and the merge commits.
        repo.WriteFile("src/core.txt", "core\nmain work\nconflict\n");
        await repo.GitAsync("add", "src/core.txt");
        await app.WaitForAsync(() => text.Text!.EndsWith("Conflicts resolved: commit to finish the merge.", StringComparison.Ordinal), "the resolved banner");
        Assert.Equal("Working changes", repository.WorkingRowText);
        Assert.Equal("Commit 1 file", commit.Content);
        Assert.True(commit.IsEffectivelyEnabled);
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
    public async Task A_rebases_banner_knows_the_branch_at_its_new_base_and_hands_Continue_and_Skip_to_the_actions()
    {
        // Phase 4's contract (docs/phase-4-design.md 5.2, 5.4): the repository names the branch
        // at onto, and the banner's Continue and Skip reach IInProgressActions; no button shows
        // them yet, and the banner's text is phase 3's.
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Rebase);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await BannerShownAsync(app);
        var actions = new RecordingActions();
        repository.InProgressActions = actions;

        Assert.Equal("main", repository.Banner.OntoName);
        Assert.Equal("Rebasing feature/conflict onto 88d6b91. 1 file has conflicts.", ((TextBlock)app.FindByAutomationId("BannerText")).Text);
        repository.Banner.ContinueCommand.Execute(null);
        repository.Banner.SkipCommand.Execute(null);

        await app.WaitForAsync(() => actions.Continued.Count == 1 && actions.Skipped.Count == 1, "Continue and Skip");
        Assert.Equal((InProgressKind.Rebase, "feature/conflict", (string?)null), (actions.Continued[0].State.Kind, actions.Continued[0].State.Branch, actions.Continued[0].Message));
        Assert.Equal(InProgressKind.Rebase, actions.Skipped[0].Kind);
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
            "Cherry-picking 078ef05 \"Conflicting change\". 1 file has conflicts. 1 more commit waits: continue it in a terminal (git cherry-pick --continue).",
            ((TextBlock)app.FindByAutomationId("BannerText")).Text);
        Assert.Equal("Abort cherry-pick", ((Button)app.FindByAutomationId("BannerAbortButton")).Content);
        Assert.DoesNotContain(LayoutAudit.FindClippedText(app.Window), problem => problem.Contains("Cherry-picking", StringComparison.Ordinal));
        Pictures(app, "banner-cherry-pick-sequence");

        repository.IsWorkingRowSelected = true;
        await app.WaitForAsync(() => repository.Changes.Summary.Length > 0, "the message from MERGE_MSG");
        Assert.Equal(("Conflicting change", string.Empty), (repository.Changes.Summary, repository.Changes.Description));
        Assert.Equal("Unstaged files (1, 1 with conflicts)", repository.Changes.UnstagedTitle);

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
        await app.WaitForAsync(() => repository.Banner.IsVisible, "the banner", TimeSpan.FromSeconds(10));
        app.Settle();

        // In a wide window the text fits on one line: the banner is 36 high (section 5).
        Assert.Equal(36, app.BoundsOf(app.FindByAutomationId("ConflictBanner")).Height, 0.5);
        Assert.Equal("Merging: \"Merge branch 'feature/conflict'\". 1 file has conflicts.", ((TextBlock)app.FindByAutomationId("BannerText")).Text);

        await repo.GitAsync("merge", "--abort");
        await app.WaitForAsync(() => !repository.Banner.IsVisible, "the banner to go", TimeSpan.FromSeconds(10));
        app.Settle();
        Assert.False(app.FindByAutomationId("ConflictBanner").IsEffectivelyVisible);
        Assert.Equal("Working changes", repository.WorkingRowText);
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
