using System.Globalization;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Rebase;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using MenuItem = VisualCommit.App.ViewModels.Menus.MenuItem;

namespace VisualCommit.App.Tests;

/// <summary>
/// The menus of every object (docs/phase-3-design.md 4b, 4g, 4h; D77, D92, D93, D114, D119,
/// D120): each builder of <see cref="RepositoryMenus"/> against the lists of 4b and of the visual
/// checks 1 to 4, 7, 9, 11, 13, 23, 26 and 30 in several states, and the entry points of
/// <see cref="RepositoryViewModel"/> that the views call, run on real scenario repositories
/// without a window (what a right-click on each object opens and selects, the double-clicks, the
/// drop's facts). Menus are written as the report writes them: the header, each label, "—" for a
/// separator, " (danger)" after an enabled destructive item, <c> (disabled: "reason")</c> after a
/// disabled one. The menus in the window, opened by real input, are in <c>RepositoryMenusWindowTests</c>.
/// </summary>
public partial class RepositoryMenusTests
{
    private const string AlphaTwo = "ce5ea6ab16759bede02ce706dd5c6865443c2051";
    private const string MainWork = "88d6b91e821a43cd39c94aa7892c8095b628caf8";

    // --- Commits (check 1, 11, 13) ------------------------------------------------------------

    [Fact]
    public async Task A_commit_off_the_current_branch_has_every_item_enabled_and_a_right_click_selects_its_row()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var (actions, recorder) = RecordingActions.Create();
        app.Repository.MenuActions = actions;

        Assert.True(app.Repository.OpenCommitMenu(layer, AlphaTwo, new MenuPosition(500, 230), MenuOpenedBy.Pointer));

        Assert.Equal(AlphaTwo, app.Repository.Selection.Single);
        Assert.Equal(4, app.Repository.SelectedIndex);
        Assert.Equal(
            ["ce5ea6a Alpha two", "Check out this commit", "Create branch here…", "Create tag here…", "—", "Cherry-pick onto main", "Revert this commit", "Rebase main onto this commit", "Interactive rebase of main from here… (disabled: \"This commit is not on main\")", "Reset main to this commit…", "—", "Copy SHA"],
            Lines(layer.Current!));
        Assert.Equal(284, Height(layer.Current!));
        Assert.Equal((new MenuPosition(500, 230), MenuOpenedBy.Pointer), (layer.Position, layer.OpenedBy));

        // Each item starts its action on the commit the menu captured.
        InvokeAll(layer.Current!);
        Assert.Equal(
            [$"CheckoutAsync({AlphaTwo})", $"CreateBranchAsync({AlphaTwo})", $"CreateTagAsync({AlphaTwo})", $"CherryPickAsync({AlphaTwo})", $"RevertAsync({AlphaTwo})", Rebase(AlphaTwo, "ce5ea6a"), $"ResetAsync({AlphaTwo})", $"CopyShaAsync({AlphaTwo})"],
            recorder.Calls.Select(call => call.ToString()));
    }

    [Fact]
    public async Task A_commit_already_on_the_current_branch_cannot_be_cherry_picked()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var addCore = await app.ShaAsync("v1.0^{commit}");

        Assert.True(app.Repository.OpenCommitMenu(layer, addCore, default, MenuOpenedBy.Pointer));
        Assert.Contains("Cherry-pick onto main (disabled: \"This commit is already on main\")", Lines(layer.Current!));

        Assert.True(app.Repository.OpenCommitMenu(layer, MainWork, default, MenuOpenedBy.Keyboard));
        Assert.Equal(
            ["88d6b91 Main work", "Check out this commit", "Create branch here…", "Create tag here…", "—", "Cherry-pick onto main (disabled: \"This commit is already on main\")", "Revert this commit", "Rebase main onto this commit (disabled: \"This commit is already on main\")", "Interactive rebase of main from here… (disabled: \"main has no commits after this one\")", "Reset main to this commit…", "—", "Copy SHA"],
            Lines(layer.Current!));
        Assert.Equal(MenuOpenedBy.Keyboard, layer.OpenedBy);
    }

    [Fact]
    public async Task On_a_detached_HEAD_the_commit_menu_names_HEAD_and_its_own_commit_is_checked_out_already()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var head = await app.ShaAsync("HEAD");

        // The "HEAD" label stands for its commit.
        Assert.True(app.Repository.OpenLabelMenu(layer, head, new RefLabel("HEAD", RefLabelKind.DetachedHead), default, MenuOpenedBy.Pointer));

        Assert.Equal(
            ["419190f Detached work", "Check out this commit (disabled: \"HEAD is already at this commit\")", "Create branch here…", "Create tag here…", "—", "Cherry-pick onto HEAD (disabled: \"This commit is already on HEAD\")", "Revert this commit", "Rebase HEAD onto this commit (disabled: \"This commit is already on HEAD\")", "Interactive rebase of HEAD from here… (disabled: \"HEAD has no commits after this one\")", "Reset HEAD to this commit…", "—", "Copy SHA"],
            Lines(layer.Current!));
        Assert.Equal(head, app.Repository.Selection.Single);
    }

    [Fact]
    public async Task A_merge_commit_gives_the_first_of_its_reasons_and_reverts_as_a_merge()
    {
        using var repo = await Scenarios.GraphAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var merge = (await repo.GitAsync("log", "--merges", "-1", "--format=%H", "main")).StandardOutput.Trim();

        Assert.True(app.Repository.OpenCommitMenu(layer, merge, default, MenuOpenedBy.Pointer));

        Assert.Equal(
            [$"{merge[..7]} Merge branch 'feature/login'", "Check out this commit", "Create branch here…", "Create tag here…", "—", "Cherry-pick onto main (disabled: \"Merge commits cannot be cherry-picked here\")", "Revert this merge commit", "Rebase main onto this commit (disabled: \"This commit is already on main\")", "Interactive rebase of main from here…", "Reset main to this commit…", "—", "Copy SHA"],
            Lines(layer.Current!));
    }

    [Fact]
    public async Task Several_commits_have_their_own_menu_on_any_of_them_and_a_row_outside_is_selected_alone()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var (actions, recorder) = RecordingActions.Create();
        app.Repository.MenuActions = actions;
        var alphaOne = await app.ShaAsync("feature/alpha~1");

        // Check 11 step 6: one of them is on main.
        app.Select(MainWork, AlphaTwo, alphaOne);
        Assert.True(app.Repository.OpenCommitMenu(layer, MainWork, default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["3 commits", "Squash 3 commits… (disabled: \"Only commits of main can be squashed\")", "Cherry-pick 3 commits onto main (disabled: \"Some of these commits are already on main\")", "Revert 3 commits", "—", "Copy SHAs"],
            Lines(layer.Current!));
        Assert.Equal(145, Height(layer.Current!));
        Assert.Equal(3, app.Repository.Selection.Count);

        // Step 10: two that are not; the SHAs go in the graph's row order, top first.
        app.Select(alphaOne, AlphaTwo);
        Assert.True(app.Repository.OpenCommitMenu(layer, alphaOne, default, MenuOpenedBy.Pointer));
        Assert.Equal(["2 commits", "Squash 2 commits… (disabled: \"Only commits of main can be squashed\")", "Cherry-pick 2 commits onto main", "Revert 2 commits", "—", "Copy SHAs"], Lines(layer.Current!));
        InvokeAll(layer.Current!);
        var shas = $"[{AlphaTwo}, {alphaOne}]";
        Assert.Equal([$"CherryPickSeveralAsync({shas})", $"RevertSeveralAsync({shas})", $"CopyShasAsync({shas})"], recorder.Calls.Select(call => Format(call)));

        // A row outside the selection is selected alone and has the one commit's menu.
        Assert.True(app.Repository.OpenCommitMenu(layer, MainWork, default, MenuOpenedBy.Pointer));
        Assert.Equal(MainWork, app.Repository.Selection.Single);
        Assert.Equal("88d6b91 Main work", layer.Current!.HeaderText);
    }

    [Fact]
    public void Several_commits_with_a_merge_or_a_stash_are_neither_picked_nor_reverted_together()
    {
        var (actions, _) = RecordingActions.Create();
        var merge = new CommitMenuInfo("m".PadRight(40, '0'), "Merge", ["a", "b"], CommitKind.Commit, 1, false);
        var plain = new CommitMenuInfo("p".PadRight(40, '0'), "Plain", ["a"], CommitKind.Commit, 2, false);
        var context = Context(OpsLikeRefs(), selected: [merge, plain]);

        Assert.Equal(
            ["2 commits", "Squash 2 commits… (disabled: \"Merge commits cannot be squashed\")", "Cherry-pick 2 commits onto main (disabled: \"Merge commits and stashes cannot be cherry-picked\")", "Revert 2 commits (disabled: \"Revert merge commits one at a time\")", "—", "Copy SHAs"],
            Lines(RepositoryMenus.ForCommits(context, actions)));
    }

    [Fact]
    public void A_merge_on_HEADs_line_disables_from_here_and_squash_below_it_before_HEADs_own_state()
    {
        // The review's finding 24 (docs/phase-4-design.md 2.1, section 3, 10.4): main's line runs
        // from HEAD to the merge M; A and B are below M, reachable but off the linear line.
        var (actions, _) = RecordingActions.Create();
        var (head, merge, a, b) = (Sha('h'), Sha('m'), Sha('a'), Sha('b'));
        var headCommit = new CommitMenuInfo(head, "Head", [merge], CommitKind.Commit, 0, true) { HeadLineDepth = 0 };
        var mergeCommit = new CommitMenuInfo(merge, "Merge side", [b, Sha('s')], CommitKind.Commit, 1, true) { HeadLineDepth = 1 };
        var bCommit = new CommitMenuInfo(b, "B", [a], CommitKind.Commit, 2, true);
        var aCommit = new CommitMenuInfo(a, "A", [Sha('r')], CommitKind.Commit, 3, true);
        var refs = OpsLikeRefs() with { Head = new HeadState("main", head) };

        Assert.Contains(
            "Interactive rebase of main from here… (disabled: \"A merge lies between this commit and main\")",
            Lines(RepositoryMenus.ForCommit(Context(refs, selected: [aCommit]), a, actions)));
        Assert.Contains(
            "Squash 2 commits… (disabled: \"A merge lies between these commits and main\")",
            Lines(RepositoryMenus.ForCommits(Context(refs, selected: [bCommit, aCommit]), actions)));

        // The commits' own reason comes before HEAD's state (detached here): 1.1's order.
        var detached = refs with { Head = new HeadState(null, head) };
        Assert.Contains(
            "Interactive rebase of HEAD from here… (disabled: \"A merge lies between this commit and HEAD\")",
            Lines(RepositoryMenus.ForCommit(Context(detached, selected: [aCommit]), a, actions)));
        Assert.Contains(
            "Squash 2 commits… (disabled: \"A merge lies between these commits and HEAD\")",
            Lines(RepositoryMenus.ForCommits(Context(detached, selected: [bCommit, aCommit]), actions)));

        // On the line, HEAD and M squash; detached, it is HEAD's reason.
        Assert.Contains("Squash 2 commits…", Lines(RepositoryMenus.ForCommits(Context(refs, selected: [headCommit, mergeCommit with { Parents = [b] }]), actions)));
        Assert.Contains(
            "Squash 2 commits… (disabled: \"Check out a branch to squash\")",
            Lines(RepositoryMenus.ForCommits(Context(detached, selected: [headCommit, mergeCommit with { Parents = [b] }]), actions)));
    }

    [Fact]
    public void Conflicts_left_by_a_stash_disable_the_rebase_interactive_and_squash_items_with_their_reason()
    {
        // The review's finding 24 (10.4, D119): files conflict without an operation in progress.
        var (actions, _) = RecordingActions.Create();
        var (head, parent, other) = (Sha('h'), Sha('p'), Sha('o'));
        var headCommit = new CommitMenuInfo(head, "Head", [parent], CommitKind.Commit, 0, true) { HeadLineDepth = 0 };
        var parentCommit = new CommitMenuInfo(parent, "Parent", [Sha('g')], CommitKind.Commit, 1, true) { HeadLineDepth = 1 };
        var otherCommit = new CommitMenuInfo(other, "Elsewhere", [Sha('g')], CommitKind.Commit, 2, false);
        var feature = new GitRef("refs/heads/feature", "feature", RefKind.LocalBranch, other);
        var refs = OpsLikeRefs(feature) with { Head = new HeadState("main", head) };
        var status = new WorkingTreeStatus([], [new ChangedFile("src/core.txt", FileChangeKind.Conflicted)], new HashSet<string>(StringComparer.Ordinal));
        const string Reason = "(disabled: \"Resolve the conflicts first\")";

        var commitMenu = Lines(RepositoryMenus.ForCommit(Context(refs, status, selected: [otherCommit]), other, actions));
        Assert.Contains($"Rebase main onto this commit {Reason}", commitMenu);
        Assert.Contains($"Interactive rebase of main from here… (disabled: \"This commit is not on main\")", commitMenu);
        Assert.Contains($"Interactive rebase of main from here… {Reason}", Lines(RepositoryMenus.ForCommit(Context(refs, status, selected: [parentCommit]), parent, actions)));
        var branchMenu = Lines(RepositoryMenus.ForLocalBranch(Context(refs, status), feature.FullName, actions));
        Assert.Contains($"Rebase main onto feature {Reason}", branchMenu);
        Assert.Contains($"Interactive rebase of main onto feature… {Reason}", branchMenu);
        Assert.Contains($"Squash 2 commits… {Reason}", Lines(RepositoryMenus.ForCommits(Context(refs, status, selected: [headCommit, parentCommit]), actions)));
    }

    /// <summary>A made-up 40-character commit id, one per letter (as the other hand-built menus make them).</summary>
    private static string Sha(char letter) => letter.ToString().PadRight(40, '0');

    [Fact]
    public async Task A_stash_row_and_its_label_open_the_stash_menu()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var (actions, recorder) = RecordingActions.Create();
        app.Repository.MenuActions = actions;
        var stash = await app.ShaAsync("refs/stash");
        string[] expected = ["stash@{0} On main: Experiment", "Apply stash", "Pop stash", "Drop stash… (danger)", "—", "Copy SHA"];

        Assert.True(app.Repository.OpenCommitMenu(layer, stash, default, MenuOpenedBy.Pointer));
        Assert.Equal(expected, Lines(layer.Current!));
        Assert.Equal(stash, app.Repository.Selection.Single);

        app.Select(MainWork);
        Assert.True(app.Repository.OpenLabelMenu(layer, stash, new RefLabel("stash@{0}", RefLabelKind.Stash), default, MenuOpenedBy.Pointer));
        Assert.Equal(expected, Lines(layer.Current!));
        Assert.Equal(stash, app.Repository.Selection.Single);

        InvokeAll(layer.Current!);
        Assert.Equal([$"ApplyStashAsync({stash})", $"PopStashAsync({stash})", $"DropStashAsync({stash})", $"CopyShaAsync({stash})"], recorder.Calls.Select(call => call.ToString()));
    }

    // --- Branches (check 2, 9, 13, 28, 30) ----------------------------------------------------

    [Fact]
    public async Task The_branch_menus_of_the_ops_scenario_are_those_of_check_2_and_a_left_panel_row_selects_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var (actions, recorder) = RecordingActions.Create();
        app.Repository.MenuActions = actions;
        var panel = app.Repository.LeftPanel;
        Open(panel, "local:feature");
        Open(panel, "remote:origin:feature");

        // Step 1: the left panel's feature/alpha, up to date with its upstream.
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "ref:refs/heads/feature/alpha"), new MenuPosition(100, 200), MenuOpenedBy.Pointer));
        await WaitForMenuAsync(layer, "feature/alpha");
        Assert.Equal(
            ["feature/alpha", "Check out feature/alpha", "Merge feature/alpha into main", "Merge feature/alpha into main (create a merge commit)", "Rebase main onto feature/alpha", "Interactive rebase of main onto feature/alpha…", "Fast-forward feature/alpha to origin/feature/alpha (disabled: \"feature/alpha is up to date with origin/feature/alpha\")", "—", "Push feature/alpha to origin/feature/alpha", "Force push feature/alpha… (danger)", "—", "Create branch here…", "Create tag here…", "Rename feature/alpha…", "Delete feature/alpha… (danger)", "—", "Change upstream…", "Stop tracking origin/feature/alpha", "—", "Copy branch name"],
            Lines(layer.Current!));
        Assert.Equal(458, Height(layer.Current!));
        Assert.True(app.Repository.Selection.IsEmpty);
        Assert.DoesNotContain(panel.Rows.OfType<RefItemRow>(), row => row.IsSelected);

        InvokeAll(layer.Current!);
        const string Alpha = "refs/heads/feature/alpha";
        Assert.Equal(
            [$"CheckoutAsync({Alpha})", $"MergeAsync({Alpha}, Default)", $"MergeAsync({Alpha}, NoFastForward)", Rebase(AlphaTwo, "feature/alpha"), Interactive(AlphaTwo, "feature/alpha", "refs/heads/main"), $"PushBranchAsync({Alpha})", $"ForcePushAsync({Alpha})", $"CreateBranchAsync({Alpha})", $"CreateTagAsync({Alpha})", $"RenameBranchAsync({Alpha})", $"DeleteBranchAsync({Alpha})", $"SetUpstreamAsync({Alpha})", $"UnsetUpstreamAsync({Alpha})", $"CopyRefNameAsync({Alpha})"],
            recorder.Calls.Select(call => call.ToString()));

        // Step 2: feature/ff's label, no upstream; the label's row is selected.
        var ff = await app.ShaAsync("feature/ff");
        Assert.True(app.Repository.OpenLabelMenu(layer, ff, new RefLabel("feature/ff", RefLabelKind.LocalBranch, "refs/heads/feature/ff"), default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["feature/ff", "Check out feature/ff", "Merge feature/ff into main", "Merge feature/ff into main (create a merge commit)", "Rebase main onto feature/ff", "Interactive rebase of main onto feature/ff…", "—", "Push feature/ff…", "—", "Create branch here…", "Create tag here…", "Rename feature/ff…", "Delete feature/ff… (danger)", "—", "Set upstream…", "—", "Copy branch name"],
            Lines(layer.Current!));
        Assert.Equal(ff, app.Repository.Selection.Single);

        // Step 3: the current branch: pull, and no check-out, merge or separator after the header.
        Assert.True(app.Repository.OpenLabelMenu(layer, MainWork, new RefLabel("main", RefLabelKind.CurrentBranch, "refs/heads/main"), default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["main", "Pull from origin/main", "Pull from origin/main (rebase)", "—", "Push main to origin/main", "Force push main… (danger)", "—", "Create branch here…", "Create tag here…", "Rename main…", "Delete main… (disabled: \"You cannot delete the branch you are on\")", "—", "Change upstream…", "Stop tracking origin/main", "—", "Copy branch name"],
            Lines(layer.Current!));
        var delete = layer.Current!.Items.Single(item => item.Label == "Delete main…");
        Assert.True(delete.IsDestructive);
        Assert.False(delete.IsEnabled);

        // Step 4: the remote branch, under Remotes › origin; HEAD is on main, so it can be checked out.
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "ref:refs/remotes/origin/feature/alpha"), default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["origin/feature/alpha", "Check out origin/feature/alpha", "Merge origin/feature/alpha into main", "Merge origin/feature/alpha into main (create a merge commit)", "Rebase main onto origin/feature/alpha", "Interactive rebase of main onto origin/feature/alpha…", "—", "Create branch here…", "Create tag here…", "—", "Delete feature/alpha on origin… (danger)", "—", "Copy branch name"],
            Lines(layer.Current!));

        // "Check out origin/main" while HEAD is on main (D114).
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "ref:refs/remotes/origin/main"), default, MenuOpenedBy.Pointer));
        Assert.Contains("Check out origin/main (disabled: \"main is already checked out\")", Lines(layer.Current!));
    }

    [Fact]
    public async Task A_local_upstream_is_named_by_its_branch_and_has_no_push_and_a_local_branch_named_like_a_remote_one_is_a_branch()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        await repo.CheckoutAsync("loc");
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var panel = app.Repository.LeftPanel;

        // Step 5: loc, current, tracking the local main.
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "ref:refs/heads/loc"), default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["loc", "Pull from main", "Pull from main (rebase)", "—", "Create branch here…", "Create tag here…", "Rename loc…", "Delete loc… (disabled: \"You cannot delete the branch you are on\")", "—", "Change upstream…", "Stop tracking main", "—", "Copy branch name"],
            Lines(layer.Current!));

        // Step 6: side, checked out in another worktree: the menu leaves the checkout to git.
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "ref:refs/heads/side"), default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["side", "Check out side", "Merge side into loc", "Merge side into loc (create a merge commit)", "Rebase loc onto side (disabled: \"loc is up to date with side\")", "Interactive rebase of loc onto side…", "—", "Push side…", "—", "Create branch here…", "Create tag here…", "Rename side…", "Delete side… (danger)", "—", "Set upstream…", "—", "Copy branch name"],
            Lines(layer.Current!));

        // Steps 7 and 8: the local branch origin/main and the remote branch origin/main.
        Open(panel, "local:origin");
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "ref:refs/heads/origin/main"), default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["origin/main", "Check out origin/main", "Merge origin/main into loc", "Merge origin/main into loc (create a merge commit)", "Rebase loc onto origin/main (disabled: \"loc is up to date with origin/main\")", "Interactive rebase of loc onto origin/main…", "—", "Push origin/main…", "—", "Create branch here…", "Create tag here…", "Rename origin/main…", "Delete origin/main… (danger)", "—", "Set upstream…", "—", "Copy branch name"],
            Lines(layer.Current!));
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "ref:refs/remotes/origin/main"), default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["origin/main", "Check out origin/main", "Merge origin/main into loc", "Merge origin/main into loc (create a merge commit)", "Rebase loc onto origin/main", "Interactive rebase of loc onto origin/main…", "—", "Create branch here…", "Create tag here…", "—", "Delete main on origin… (danger)", "—", "Copy branch name"],
            Lines(layer.Current!));
    }

    [Fact]
    public void A_branch_with_an_upstream_fast_forwards_only_when_it_is_behind_and_not_ahead_and_not_checked_out_elsewhere()
    {
        var (actions, recorder) = RecordingActions.Create();
        string FastForward(GitRef topic, params WorktreeInfo[] worktrees) =>
            Lines(RepositoryMenus.ForLocalBranch(Context(OpsLikeRefs(topic), worktrees: worktrees), topic.FullName, actions))
                .Single(line => line.StartsWith("Fast-forward", StringComparison.Ordinal));
        var behind = Tracking("topic", "published") with { Behind = 1 };

        Assert.Equal("Fast-forward topic to origin/published", FastForward(behind));
        Assert.Equal("Fast-forward topic to origin/published (disabled: \"topic is up to date with origin/published\")", FastForward(behind with { Behind = 0 }));
        Assert.Equal("Fast-forward topic to origin/published (disabled: \"topic has commits that origin/published does not have\")", FastForward(behind with { Ahead = 1 }));
        Assert.Equal("Fast-forward topic to origin/published (disabled: \"topic has commits that origin/published does not have\")", FastForward(behind with { Behind = 0, Ahead = 1 }));
        Assert.Equal("Fast-forward topic to origin/published (disabled: \"origin/published no longer exists\")", FastForward(behind with { UpstreamGone = true }));
        Assert.Equal(
            "Fast-forward topic to origin/published (disabled: \"topic is checked out in another worktree (C:\\work\\topic-wt)\")",
            FastForward(behind, new WorktreeInfo("C:\\work\\topic-wt", "refs/heads/topic", IsMain: false)));

        // It fast-forwards to the upstream's full ref (2.7).
        var menu = RepositoryMenus.ForLocalBranch(Context(OpsLikeRefs(behind)), behind.FullName, actions);
        menu.Items.Single(item => item.Label.StartsWith("Fast-forward", StringComparison.Ordinal)).Invoke();
        Assert.Equal(["FastForwardBranchAsync(refs/heads/topic, refs/remotes/origin/published)"], recorder.Calls.Select(call => call.ToString()));
    }

    [Fact]
    public void A_gone_upstream_disables_pull_fast_forward_and_force_push_and_leaves_push()
    {
        var (actions, _) = RecordingActions.Create();
        var published = Tracking("published", "published") with { UpstreamGone = true };

        Assert.Equal(
            ["published", "Check out published", "Merge published into main", "Merge published into main (create a merge commit)", "Rebase main onto published", "Interactive rebase of main onto published…", "Fast-forward published to origin/published (disabled: \"origin/published no longer exists\")", "—", "Push published to origin/published", "Force push published… (disabled: \"origin/published no longer exists: push normally\")", "—", "Create branch here…", "Create tag here…", "Rename published…", "Delete published… (danger)", "—", "Change upstream…", "Stop tracking origin/published", "—", "Copy branch name"],
            Lines(RepositoryMenus.ForLocalBranch(Context(OpsLikeRefs(published)), published.FullName, actions)));

        // On the current branch the pull says the same, as the toolbar's Pull.
        var main = Tracking("main", "main", isHead: true) with { UpstreamGone = true };
        var refs = new RepoRefs(new HeadState("main", MainWork), [main], [], ["origin"]);
        Assert.Equal("Pull from origin/main (disabled: \"origin/main no longer exists\")", Lines(RepositoryMenus.ForLocalBranch(Context(refs), main.FullName, actions))[1]);
    }

    [Fact]
    public void Without_remotes_a_branch_cannot_be_pushed_and_without_remote_branches_no_upstream_can_be_set()
    {
        var (actions, _) = RecordingActions.Create();
        var main = new GitRef("refs/heads/main", "main", RefKind.LocalBranch, MainWork, IsHead: true);
        var refs = new RepoRefs(new HeadState("main", MainWork), [main], [], []);

        Assert.Equal(
            ["main", "Push main… (disabled: \"Add a remote first\")", "—", "Create branch here…", "Create tag here…", "Rename main…", "Delete main… (disabled: \"You cannot delete the branch you are on\")", "—", "Set upstream… (disabled: \"No remote branches to track: fetch or push first\")", "—", "Copy branch name"],
            Lines(RepositoryMenus.ForLocalBranch(Context(refs), main.FullName, actions)));
    }

    [Fact]
    public async Task During_a_merge_merges_picks_reverts_and_stash_are_disabled_and_checkout_is_left_to_git()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        var context = app.Repository.MenuContext;
        var (actions, _) = RecordingActions.Create();
        const string Finish = "Finish or abort the merge first";

        // Check 9 step 5.
        Assert.Equal(
            ["feature/alpha", "Check out feature/alpha", $"Merge feature/alpha into main (disabled: \"{Finish}\")", $"Merge feature/alpha into main (create a merge commit) (disabled: \"{Finish}\")", $"Rebase main onto feature/alpha (disabled: \"{Finish}\")", $"Interactive rebase of main onto feature/alpha… (disabled: \"{Finish}\")", "Fast-forward feature/alpha to origin/feature/alpha (disabled: \"feature/alpha is up to date with origin/feature/alpha\")", "—", "Push feature/alpha to origin/feature/alpha", "Force push feature/alpha… (danger)", "—", "Create branch here…", "Create tag here…", "Rename feature/alpha…", "Delete feature/alpha… (danger)", "—", "Change upstream…", "Stop tracking origin/feature/alpha", "—", "Copy branch name"],
            Lines(RepositoryMenus.ForLocalBranch(context, "refs/heads/feature/alpha", actions)));
        Assert.Equal(458, Height(RepositoryMenus.ForLocalBranch(context, "refs/heads/feature/alpha", actions)));

        // D119: no stash while the merge waits, in every menu that offers it and in the toolbar.
        Assert.Contains($"Stash changes… (disabled: \"{Finish}\")", Lines(RepositoryMenus.ForWorkingChanges(context, actions)));
        Assert.Equal(["Stashes", $"Stash changes… (disabled: \"{Finish}\")"], Lines(RepositoryMenus.ForSection(context, RefSection.Stashes, actions)!));
        Assert.Contains($"Stash changes… (disabled: \"{Finish}\")", Lines(RepositoryMenus.ForEmptyGraphArea(context, actions)));
        Assert.False(app.Repository.Toolbar.CanStash);
        Assert.Equal(Finish, app.Repository.Toolbar.StashToolTip);

        // A pick and a revert wait too; apply and pop of a stash are left to git (D119).
        app.Select(AlphaTwo);
        Assert.Contains($"Cherry-pick onto main (disabled: \"{Finish}\")", Lines(RepositoryMenus.ForCommit(app.Repository.MenuContext, AlphaTwo, actions)));
        Assert.Contains($"Revert this commit (disabled: \"{Finish}\")", Lines(RepositoryMenus.ForCommit(app.Repository.MenuContext, AlphaTwo, actions)));
        var stash = await app.ShaAsync("refs/stash");
        Assert.DoesNotContain(Lines(RepositoryMenus.ForStash(context, stash, actions)), line => line.Contains("disabled", StringComparison.Ordinal));
    }

    [Fact]
    public void Conflicts_left_by_a_stash_stop_a_stash_with_their_own_reason()
    {
        var (actions, _) = RecordingActions.Create();
        var status = new WorkingTreeStatus([], [new ChangedFile("src/core.txt", FileChangeKind.Conflicted)], new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal(["Stashes", "Stash changes… (disabled: \"Resolve the conflicts first\")"], Lines(RepositoryMenus.ForSection(Context(OpsLikeRefs(), status), RefSection.Stashes, actions)!));
    }

    [Theory]
    [InlineData(InProgressKind.CherryPick, "Finish or abort the cherry-pick first")]
    [InlineData(InProgressKind.ApplyMailbox, "Finish or abort the git am first")]
    public void Every_Stash_changes_item_gives_the_toolbars_reason_in_its_order(InProgressKind kind, string reason)
    {
        // 2.24's order (D119): the operation in progress comes before "nothing to stash", in each
        // of the three menus that offer "Stash changes…" as in the toolbar.
        var (actions, _) = RecordingActions.Create();
        var status = WorkingTreeStatus.Clean with { InProgress = new InProgressState(kind, AlphaTwo, "Alpha two", null, null, 0, Empty: true) };
        var context = Context(OpsLikeRefs(), status);
        var item = $"Stash changes… (disabled: \"{reason}\")";

        Assert.Contains(item, Lines(RepositoryMenus.ForWorkingChanges(context, actions)));
        Assert.Equal(["Stashes", item], Lines(RepositoryMenus.ForSection(context, RefSection.Stashes, actions)!));
        Assert.Contains(item, Lines(RepositoryMenus.ForEmptyGraphArea(context, actions)));
        Assert.Equal(new ToolbarAvailability(false, reason), context.Toolbar.Stash);

        // Before the first commit, that comes first.
        var unborn = Context(OpsLikeRefs() with { Head = new HeadState("main", null) }, status);
        Assert.Equal(["Stashes", "Stash changes… (disabled: \"Make the first commit first\")"], Lines(RepositoryMenus.ForSection(unborn, RefSection.Stashes, actions)!));
    }

    // --- Tags, remotes, sections, the empty area (check 3, 23) --------------------------------

    [Fact]
    public async Task The_tag_stash_remote_section_and_empty_area_menus_are_those_of_check_3()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var (actions, recorder) = RecordingActions.Create();
        app.Repository.MenuActions = actions;
        var panel = app.Repository.LeftPanel;

        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "ref:refs/tags/v1.0"), default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["v1.0", "Check out v1.0", "Create branch here…", "—", "Push v1.0 to origin", "Delete v1.0… (danger)", "Delete v1.0 on origin… (danger)", "—", "Copy tag name"],
            Lines(layer.Current!));
        InvokeAll(layer.Current!);

        var stash = await app.ShaAsync("refs/stash");
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "stash:" + stash), default, MenuOpenedBy.Pointer));
        Assert.Equal(["stash@{0} On main: Experiment", "Apply stash", "Pop stash", "Drop stash… (danger)", "—", "Copy SHA"], Lines(layer.Current!));

        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "remote:origin"), default, MenuOpenedBy.Pointer));
        Assert.Equal(["origin", "Fetch origin", "—", "Change URL…", "Rename origin…", "Remove origin… (danger)", "—", "Copy URL", "Manage remotes…"], Lines(layer.Current!));
        InvokeAll(layer.Current!);

        // Nothing selected and nothing highlighted by any of them (D114).
        Assert.True(app.Repository.Selection.IsEmpty);
        Assert.DoesNotContain(panel.Rows.OfType<RefItemRow>(), row => row.IsSelected);

        // The section headers; "Pull requests" and a folder have no menu.
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "section:LocalBranches"), default, MenuOpenedBy.Pointer));
        Assert.Equal(["Local branches", "Create branch…"], Lines(layer.Current!));
        InvokeAll(layer.Current!);
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "section:Remotes"), default, MenuOpenedBy.Pointer));
        Assert.Equal(["Remotes", "Add remote…", "Fetch all remotes", "Manage remotes…"], Lines(layer.Current!));
        InvokeAll(layer.Current!);
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "section:Tags"), default, MenuOpenedBy.Pointer));
        Assert.Equal(["Tags", "Create tag at HEAD…"], Lines(layer.Current!));
        InvokeAll(layer.Current!);
        Assert.True(app.Repository.OpenRefRowMenu(layer, Row(panel, "section:Stashes"), default, MenuOpenedBy.Pointer));
        Assert.Equal(["Stashes", "Stash changes… (disabled: \"There are no changes to stash\")"], Lines(layer.Current!));
        layer.Close();
        Assert.False(app.Repository.OpenRefRowMenu(layer, Row(panel, "section:PullRequests"), default, MenuOpenedBy.Pointer));
        Assert.False(app.Repository.OpenRefRowMenu(layer, Row(panel, "local:feature"), default, MenuOpenedBy.Pointer));
        Assert.False(layer.IsOpen);

        // Step 6: the empty area, with the stash selected by step 2; the selection stays.
        app.Select(stash);
        Assert.True(app.Repository.OpenEmptyGraphAreaMenu(layer, new MenuPosition(520, 424), MenuOpenedBy.Pointer));
        Assert.Equal(
            ["ops", "Fetch all remotes", "Pull", "Push", "—", "Create branch…", "Stash changes… (disabled: \"There are no changes to stash\")", "—", "Manage remotes…"],
            Lines(layer.Current!));
        Assert.Equal(206, Height(layer.Current!));
        Assert.Equal(stash, app.Repository.Selection.Single);
        InvokeAll(layer.Current!);

        Assert.Equal(
            ["CheckoutAsync(refs/tags/v1.0)", "CreateBranchAsync(refs/tags/v1.0)", "PushTagAsync(refs/tags/v1.0)", "DeleteTagAsync(refs/tags/v1.0)", "DeleteRemoteTagAsync(refs/tags/v1.0)", "CopyRefNameAsync(refs/tags/v1.0)",
             "FetchAsync(origin)", "ShowEditRemoteAsync(origin)", "ShowEditRemoteAsync(origin)", "RemoveRemoteAsync(origin)", "CopyRemoteUrlAsync(origin)", "ManageRemotesAsync()",
             "CreateBranchAsync()", "ShowAddRemoteAsync()", "FetchAllAsync()", "ManageRemotesAsync()", "CreateTagAsync()",
             "FetchAllAsync()", "PullAsync()", "PushCurrentBranchAsync()", "CreateBranchAsync()", "ManageRemotesAsync()"],
            recorder.Calls.Select(call => call.ToString()));
    }

    [Fact]
    public void A_tag_with_several_remotes_or_none_asks_or_cannot_reach_one_and_a_tag_HEAD_is_detached_at_is_checked_out()
    {
        var (actions, _) = RecordingActions.Create();
        var tag = new GitRef("refs/tags/v1.0", "v1.0", RefKind.Tag, MainWork);
        var several = new RepoRefs(new HeadState(null, MainWork), [tag], [], ["mirror", "origin"]);
        var none = new RepoRefs(new HeadState("main", MainWork), [tag], [], []);

        Assert.Equal(
            ["v1.0", "Check out v1.0 (disabled: \"HEAD is already at this commit\")", "Create branch here…", "—", "Push v1.0…", "Delete v1.0… (danger)", "Delete v1.0 on a remote… (danger)", "—", "Copy tag name"],
            Lines(RepositoryMenus.ForTag(Context(several), tag.FullName, actions)));
        Assert.Equal(
            ["v1.0", "Check out v1.0", "Create branch here…", "—", "Push v1.0… (disabled: \"Add a remote first\")", "Delete v1.0… (danger)", "Delete v1.0 on a remote… (disabled: \"Add a remote first\")", "—", "Copy tag name"],
            Lines(RepositoryMenus.ForTag(Context(none), tag.FullName, actions)));
    }

    [Fact]
    public async Task An_unborn_branch_with_a_remote_has_the_empty_area_menu_of_check_23()
    {
        using var repo = await TempRepo.CreateAsync("unborn");
        await repo.AddBareRemoteAsync();
        repo.WriteFile("notes.txt", "notes\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();

        Assert.True(app.Repository.OpenEmptyGraphAreaMenu(layer, default, MenuOpenedBy.Pointer));

        Assert.Equal(
            ["unborn", "Fetch all remotes", "Pull (disabled: \"main has no upstream: set one first\")", "Push (disabled: \"Make the first commit first\")", "—", "Create branch… (disabled: \"Make the first commit first\")", "Stash changes… (disabled: \"Make the first commit first\")", "—", "Manage remotes…"],
            Lines(layer.Current!));
    }

    [Fact]
    public void Before_the_refs_are_read_the_empty_area_items_are_disabled_without_a_reason()
    {
        var (actions, _) = RecordingActions.Create();
        var context = Context(OpsLikeRefs()) with { Toolbar = ToolbarMenuState.Disabled };

        var menu = RepositoryMenus.ForEmptyGraphArea(context, actions);

        Assert.All(menu.Items.Where(item => item.Label != "Manage remotes…"), item => Assert.Equal((false, null), (item.IsEnabled, item.DisabledReason)));
    }

    // --- The working-changes row and files (check 4, 26) --------------------------------------

    [Fact]
    public async Task The_working_changes_row_selects_itself_and_its_items_follow_the_lists()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var (actions, recorder) = RecordingActions.Create();
        app.Repository.MenuActions = actions;

        Assert.True(app.Repository.OpenWorkingChangesMenu(layer, default, MenuOpenedBy.Pointer));

        Assert.True(app.Repository.IsWorkingRowSelected);
        Assert.Equal(
            ["Working changes", "Stage all", "Unstage all", "Discard all changes… (danger)", "—", "Stash changes…", "Create branch here…", "—", "Show in folder"],
            Lines(layer.Current!));
        InvokeAll(layer.Current!);
        Assert.Equal(["StageAllAsync()", "UnstageAllAsync()", "DiscardAllAsync()", "StashAsync()", "CreateBranchAsync()", "ShowInFolderAsync()"], recorder.Calls.Select(call => call.ToString()));

        // Step 10: everything staged.
        var staged = new WorkingTreeStatus([new ChangedFile("README.md", FileChangeKind.Modified)], [], new HashSet<string>(StringComparer.Ordinal));
        Assert.Equal(
            ["Working changes", "Stage all (disabled: \"Nothing to stage\")", "Unstage all", "Discard all changes… (disabled: \"Nothing to discard\")", "—", "Stash changes…", "Create branch here…", "—", "Show in folder"],
            Lines(RepositoryMenus.ForWorkingChanges(Context(OpsLikeRefs(), staged), actions)));
    }

    [Fact]
    public async Task A_file_has_its_menu_in_each_list_and_a_right_click_changes_no_selection()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var (actions, recorder) = RecordingActions.Create();
        app.Repository.MenuActions = actions;
        var status = app.Repository.Status;
        ChangedFile Unstaged(string path) => status.Unstaged.Single(file => file.Path == path);
        var calculator = Unstaged("src/Calculator.cs");

        // Step 1: a tracked file: the ignore items say why not.
        Assert.True(app.Repository.OpenFileMenu(layer, calculator, FileMenuPlace.Unstaged, default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["Calculator.cs", "Stage", "Discard changes…", "—", "Ignore Calculator.cs (disabled: \"Only untracked files can be ignored\")", "Ignore all *.cs files (disabled: \"Only untracked files can be ignored\")", "—", "Open file", "Show in folder", "Copy path"],
            Lines(layer.Current!));
        Assert.False(app.Repository.IsDiffOpen);
        Assert.False(app.Repository.IsWorkingRowSelected);

        // Step 2: an untracked file.
        Assert.True(app.Repository.OpenFileMenu(layer, Unstaged("docs/guide.md"), FileMenuPlace.Unstaged, default, MenuOpenedBy.Pointer));
        Assert.Equal(
            ["guide.md", "Stage", "Discard changes…", "—", "Ignore guide.md", "Ignore all *.md files", "—", "Open file", "Show in folder", "Copy path"],
            Lines(layer.Current!));
        InvokeAll(layer.Current!);

        // Step 3: a staged file.
        var settings = status.Staged.Single(file => file.Path == "config/settings.json");
        Assert.True(app.Repository.OpenFileMenu(layer, settings, FileMenuPlace.Staged, default, MenuOpenedBy.Pointer));
        Assert.Equal(["settings.json", "Unstage", "—", "Open file", "Show in folder", "Copy path"], Lines(layer.Current!));
        InvokeAll(layer.Current!);

        // A deleted file cannot be opened.
        Assert.True(app.Repository.OpenFileMenu(layer, Unstaged("docs/old-notes.txt"), FileMenuPlace.Unstaged, default, MenuOpenedBy.Pointer));
        Assert.Contains("Open file (disabled: \"The file is not in the working tree\")", Lines(layer.Current!));

        // A commit's changed file.
        Assert.True(app.Repository.OpenFileMenu(layer, new ChangedFile("src/Calculator.cs", FileChangeKind.Added), FileMenuPlace.CommitFiles, default, MenuOpenedBy.Pointer));
        Assert.Equal(["Calculator.cs", "Open file", "Show in folder", "Copy path"], Lines(layer.Current!));

        Assert.Equal(
            ["StageFilesAsync([docs/guide.md])", "DiscardFilesAsync([docs/guide.md])", "IgnoreFileAsync(docs/guide.md)", "IgnoreExtensionAsync(docs/guide.md)", "OpenFileAsync(docs/guide.md)", "ShowInFolderAsync(docs/guide.md)", "CopyPathAsync(docs/guide.md)",
             "UnstageFilesAsync([config/settings.json])", "OpenFileAsync(config/settings.json)", "ShowInFolderAsync(config/settings.json)", "CopyPathAsync(config/settings.json)"],
            recorder.Calls.Select(call => Format(call)));
    }

    [Fact]
    public void A_conflicted_file_is_marked_resolved_and_not_discarded_a_nested_repository_is_not_discarded_and_a_name_without_extension_has_one_ignore_item()
    {
        var (actions, _) = RecordingActions.Create();
        var context = Context(OpsLikeRefs());

        Assert.Equal(
            ["core.txt", "Resolve conflict", "Open in merge tool (disabled: \"Set merge.tool in git's configuration to use a merge tool\")", "Mark as resolved", "Discard changes… (disabled: \"Resolve the conflict first\")", "—", "Ignore core.txt (disabled: \"Only untracked files can be ignored\")", "Ignore all *.txt files (disabled: \"Only untracked files can be ignored\")", "—", "Open file", "Show in folder", "Copy path"],
            Lines(RepositoryMenus.ForUnstagedFile(context, new FileMenuTarget(new ChangedFile("src/core.txt", FileChangeKind.Conflicted), false, true), actions)));
        Assert.Contains(
            "Discard changes… (disabled: \"A repository of its own is not discarded\")",
            Lines(RepositoryMenus.ForUnstagedFile(context, new FileMenuTarget(new ChangedFile("vendor/lib/", FileChangeKind.Added), true, true), actions)));

        // Check 26 step 5.
        Assert.Equal(
            ["TODO", "Stage", "Discard changes…", "—", "Ignore TODO", "—", "Open file", "Show in folder", "Copy path"],
            Lines(RepositoryMenus.ForUnstagedFile(context, new FileMenuTarget(new ChangedFile("TODO", FileChangeKind.Added), true, true), actions)));
    }

    // --- The diff view (check 4, 26) ----------------------------------------------------------

    [Fact]
    public void The_diff_menu_offers_the_selected_lines_the_hunk_under_the_pointer_the_file_and_for_an_untracked_file_its_ignore_items()
    {
        var (actions, recorder) = RecordingActions.Create();
        var lines = new RecordingDiffLines();
        var context = Context(OpsLikeRefs());
        var calculator = new FileMenuTarget(new ChangedFile("src/Calculator.cs", FileChangeKind.Modified), false, true);
        var trace = new FileMenuTarget(new ChangedFile("trace.log", FileChangeKind.Added), true, true);

        // Check 4 step 5: no lines selected, in the first hunk.
        Assert.Equal(
            ["Calculator.cs", "Stage hunk", "Discard hunk…", "—", "Stage file", "Discard file…", "—", "Open file", "Show in folder", "Copy path"],
            Lines(RepositoryMenus.ForDiff(context, new DiffMenuTarget(calculator, DiffSide.Unstaged, false, 0, true), actions, lines)));

        // Step 6: a line selected, in the second hunk.
        var menu = RepositoryMenus.ForDiff(context, new DiffMenuTarget(calculator, DiffSide.Unstaged, true, 1, true), actions, lines);
        Assert.Equal(
            ["Calculator.cs", "Stage selected lines", "Discard selected lines…", "—", "Stage hunk", "Discard hunk…", "—", "Stage file", "Discard file…", "—", "Open file", "Show in folder", "Copy path"],
            Lines(menu));
        InvokeAll(menu);
        Assert.Equal(["StageLines", "DiscardLines", "Hunk 1 Stage", "Hunk 1 Discard"], lines.Calls);
        Assert.Equal(
            ["StageFilesAsync([src/Calculator.cs])", "DiscardFilesAsync([src/Calculator.cs])", "OpenFileAsync(src/Calculator.cs)", "ShowInFolderAsync(src/Calculator.cs)", "CopyPathAsync(src/Calculator.cs)"],
            recorder.Calls.Select(call => Format(call)));

        // Check 26 step 3: an untracked file's diff has its ignore items.
        Assert.Equal(
            ["trace.log", "Stage hunk", "Discard hunk…", "—", "Stage file", "Discard file…", "—", "Ignore trace.log", "Ignore all *.log files", "—", "Open file", "Show in folder", "Copy path"],
            Lines(RepositoryMenus.ForDiff(context, new DiffMenuTarget(trace, DiffSide.Unstaged, false, 0, true), actions, lines)));

        // Outside every hunk (the header), and a type change, whose lines cannot be acted on (D76).
        Assert.Equal(
            ["Calculator.cs", "Stage file", "Discard file…", "—", "Open file", "Show in folder", "Copy path"],
            Lines(RepositoryMenus.ForDiff(context, new DiffMenuTarget(calculator, DiffSide.Unstaged, true, 0, false), actions, lines)));

        // The staged side, and a commit's diff.
        lines.Calls.Clear();
        menu = RepositoryMenus.ForDiff(context, new DiffMenuTarget(calculator, DiffSide.Staged, true, 2, true), actions, lines);
        Assert.Equal(["Calculator.cs", "Unstage selected lines", "—", "Unstage hunk", "—", "Unstage file", "—", "Open file", "Show in folder", "Copy path"], Lines(menu));
        InvokeAll(menu);
        Assert.Equal(["UnstageLines", "Hunk 2 Unstage"], lines.Calls);
        Assert.Equal(
            ["Calculator.cs", "Open file", "Show in folder", "Copy path"],
            Lines(RepositoryMenus.ForDiff(context, new DiffMenuTarget(calculator with { CommitSha = MainWork }, DiffSide.Commit, false, 0, true), actions, lines)));
    }

    [Fact]
    public async Task The_diff_menu_stages_the_selected_lines_through_the_open_diff()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var calculator = app.Repository.Status.Unstaged.Single(file => file.Path == "src/Calculator.cs");
        app.Repository.OpenDiff(calculator, staged: false);
        var diff = app.Repository.Diff!;
        await WaitUntilAsync(() => diff.Body == DiffBody.Text, "the diff");
        var hunks = diff.Diff!.Hunks;
        Assert.True(hunks.Count >= 2);
        var line = hunks[1].Lines.Select((entry, index) => (entry, index)).First(pair => pair.entry.IsChange).index;
        diff.SelectedChanges = new HashSet<DiffLineRef> { new(1, line) };

        Assert.True(app.Repository.OpenDiffMenu(layer, diff, 1, default, MenuOpenedBy.Pointer));
        Assert.Equal("Stage selected lines", layer.Current!.Items.First().Label);
        layer.Current.Items.First().Invoke();

        await WaitUntilAsync(() => app.Repository.Status.Staged.Any(file => file.Path == "src/Calculator.cs"), "the line staged");
        var cached = (await repo.GitAsync("diff", "--cached", "--", "src/Calculator.cs")).StandardOutput;
        Assert.Single(cached.Split('\n'), text => text.StartsWith("@@", StringComparison.Ordinal));

        // A hunk number the diff does not have is no hunk.
        Assert.True(app.Repository.OpenDiffMenu(layer, app.Repository.Diff!, 99, default, MenuOpenedBy.Pointer));
        Assert.DoesNotContain(layer.Current!.Items, item => item.Label.EndsWith("hunk", StringComparison.Ordinal) || item.Label.EndsWith("hunk…", StringComparison.Ordinal));
    }

    // --- The drop (check 7, 30) ---------------------------------------------------------------

    [Fact]
    public async Task A_drop_reads_whether_the_target_can_fast_forward_and_offers_check_7s_items()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var (actions, recorder) = RecordingActions.Create();
        app.Repository.MenuActions = actions;

        Assert.True(await app.Repository.OpenDropMenuAsync(layer, "refs/heads/feature/alpha", "refs/heads/main", new MenuPosition(400, 200)));
        Assert.Equal(
            ["feature/alpha → main", "Merge feature/alpha into main", "Merge feature/alpha into main (create a merge commit)", "Fast-forward main to feature/alpha (disabled: \"main has commits that feature/alpha does not have\")", "—", "Check out feature/alpha and rebase it onto main", "Check out feature/alpha and rebase it onto main interactively…"],
            Lines(layer.Current!));
        Assert.Equal(171, Height(layer.Current!));
        Assert.Equal((new MenuPosition(400, 200), MenuOpenedBy.Pointer), (layer.Position, layer.OpenedBy));

        Assert.True(await app.Repository.OpenDropMenuAsync(layer, "refs/heads/feature/ff", "refs/heads/main", default));
        Assert.Equal(["feature/ff → main", "Merge feature/ff into main", "Merge feature/ff into main (create a merge commit)", "Fast-forward main to feature/ff", "—", "Check out feature/ff and rebase it onto main (disabled: \"feature/ff is up to date with main\")", "Check out feature/ff and rebase it onto main interactively…"], Lines(layer.Current!));
        InvokeAll(layer.Current!);

        Assert.True(await app.Repository.OpenDropMenuAsync(layer, "refs/heads/feature/alpha", "refs/heads/feature/conflict", default));
        Assert.Equal(
            ["feature/alpha → feature/conflict", "Fast-forward feature/conflict to feature/alpha (disabled: \"feature/conflict has commits that feature/alpha does not have\")", "Check out feature/conflict and merge feature/alpha into it", "—", "Check out feature/alpha and rebase it onto feature/conflict", "Check out feature/alpha and rebase it onto feature/conflict interactively…"],
            Lines(layer.Current!));
        InvokeAll(layer.Current!);

        Assert.True(await app.Repository.OpenDropMenuAsync(layer, "refs/heads/main", "refs/remotes/origin/feature/alpha", default));
        Assert.Equal(["main → origin/feature/alpha", "Push main to origin/feature/alpha", "Push main to origin/feature/alpha and track it", "—", "Rebase main onto origin/feature/alpha", "Interactive rebase of main onto origin/feature/alpha…"], Lines(layer.Current!));
        InvokeAll(layer.Current!);

        // A remote branch dropped onto another local branch.
        Assert.True(await app.Repository.OpenDropMenuAsync(layer, "refs/remotes/origin/main", "refs/heads/feature/ff", default));
        Assert.Equal("origin/main → feature/ff", layer.Current!.HeaderText);

        // The rebase items change the source, onto the target's tip as the menu read it (phase 4 design 1.1).
        var conflict = await app.ShaAsync("feature/conflict");
        Assert.Equal(
            ["MergeAsync(refs/heads/feature/ff, Default)", "MergeAsync(refs/heads/feature/ff, NoFastForward)", "MergeAsync(refs/heads/feature/ff, FastForwardOnly)",
             Interactive(MainWork, "main", "refs/heads/feature/ff", checkoutFirst: true),
             "CheckoutAndMergeAsync(refs/heads/feature/conflict, refs/heads/feature/alpha)",
             $"CheckoutAndRebaseAsync(refs/heads/feature/alpha, {new RebaseTarget(conflict, "feature/conflict")})",
             Interactive(conflict, "feature/conflict", "refs/heads/feature/alpha", checkoutFirst: true),
             "PushBranchToAsync(refs/heads/main, refs/remotes/origin/feature/alpha, False)", "PushBranchToAsync(refs/heads/main, refs/remotes/origin/feature/alpha, True)",
             Rebase(AlphaTwo, "origin/feature/alpha"), Interactive(AlphaTwo, "origin/feature/alpha", "refs/heads/main")],
            recorder.Calls.Select(call => call.ToString()));
    }

    [Fact]
    public async Task A_drop_onto_a_branch_checked_out_in_another_worktree_neither_fast_forwards_nor_checks_it_out()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        // The folder as git lists it, in the platform's form (backslashes on Windows; check 30).
        var worktree = Path.TrimEndingDirectorySeparator(Path.GetFullPath((await repo.GitAsync("worktree", "list", "--porcelain")).StandardOutput
            .Split('\n').Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("worktree ", StringComparison.Ordinal)).Select(line => line["worktree ".Length..])
            .Single(path => path.EndsWith(Scenarios.EdgeCasesWorktree, StringComparison.Ordinal))));

        Assert.True(await app.Repository.OpenDropMenuAsync(layer, "refs/heads/main", "refs/heads/side", default));

        var reason = $"side is checked out in another worktree ({worktree})";
        Assert.Equal(
            ["main → side", $"Fast-forward side to main (disabled: \"{reason}\")", $"Check out side and merge main into it (disabled: \"{reason}\")", "—", "Check out main and rebase it onto side (disabled: \"main is up to date with side\")", "Check out main and rebase it onto side interactively…"],
            Lines(layer.Current!));
    }

    [Fact]
    public void A_remote_that_maps_its_branches_differently_offers_no_push_by_drop()
    {
        var (actions, _) = RecordingActions.Create();
        var context = Context(OpsLikeRefs()) with { Drop = new DropFacts(true, RemoteMapsBranchesDifferently: true) };

        Assert.Equal(
            ["main → origin/main", "Push main to origin/main (disabled: \"origin maps its branches differently: use Push main…\")", "Push main to origin/main and track it (disabled: \"origin maps its branches differently: use Push main…\")", "—", "Rebase main onto origin/main", "Interactive rebase of main onto origin/main…"],
            Lines(RepositoryMenus.ForDrop(context, "refs/heads/main", "refs/remotes/origin/main", actions)));
    }

    // --- Double-clicks (check 6, D93) ---------------------------------------------------------

    [Fact]
    public async Task A_double_click_checks_out_a_branch_applies_a_stash_in_the_left_panel_and_does_nothing_more_elsewhere()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var (actions, recorder) = RecordingActions.Create();
        app.Repository.MenuActions = actions;
        var panel = app.Repository.LeftPanel;
        Open(panel, "local:feature");
        Open(panel, "remote:origin:feature");
        var stash = await app.ShaAsync("refs/stash");

        app.Repository.DoubleTapRefRow(Row(panel, "ref:refs/heads/feature/ff"));
        app.Repository.DoubleTapRefRow(Row(panel, "ref:refs/heads/main"));
        app.Repository.DoubleTapRefRow(Row(panel, "ref:refs/remotes/origin/feature/alpha"));
        app.Repository.DoubleTapRefRow(Row(panel, "ref:refs/remotes/origin/main"));
        app.Repository.DoubleTapRefRow(Row(panel, "ref:refs/tags/v1.0"));
        app.Repository.DoubleTapRefRow(Row(panel, "stash:" + stash));
        app.Repository.DoubleTapRefRow(Row(panel, "remote:origin"));
        app.Repository.DoubleTapRefRow(Row(panel, "section:Tags"));
        app.Repository.DoubleTapLabel(new RefLabel("feature/conflict", RefLabelKind.LocalBranch, "refs/heads/feature/conflict"));
        app.Repository.DoubleTapLabel(new RefLabel("main", RefLabelKind.CurrentBranch, "refs/heads/main"));
        app.Repository.DoubleTapLabel(new RefLabel("origin/feature/alpha", RefLabelKind.RemoteBranch, "refs/remotes/origin/feature/alpha"));
        app.Repository.DoubleTapLabel(new RefLabel("v1.0", RefLabelKind.Tag, "refs/tags/v1.0"));
        app.Repository.DoubleTapLabel(new RefLabel("stash@{0}", RefLabelKind.Stash));
        app.Repository.DoubleTapLabel(new RefLabel("HEAD", RefLabelKind.DetachedHead));

        Assert.Equal(
            ["CheckoutAsync(refs/heads/feature/ff)", "CheckoutAsync(refs/remotes/origin/feature/alpha)", $"ApplyStashAsync({stash})",
             "CheckoutAsync(refs/heads/feature/conflict)", "CheckoutAsync(refs/remotes/origin/feature/alpha)"],
            recorder.Calls.Select(call => call.ToString()));
    }

    [Fact]
    public async Task Nothing_opens_once_the_tab_closed()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var layer = new MenuLayerViewModel();
        var panel = app.Repository.LeftPanel;
        var tags = Row(panel, "section:Tags");
        app.Repository.Dispose();

        Assert.False(app.Repository.OpenCommitMenu(layer, MainWork, default, MenuOpenedBy.Pointer));
        Assert.False(app.Repository.OpenRefRowMenu(layer, tags, default, MenuOpenedBy.Pointer));
        Assert.False(app.Repository.OpenEmptyGraphAreaMenu(layer, default, MenuOpenedBy.Pointer));
        Assert.False(await app.Repository.OpenDropMenuAsync(layer, "refs/heads/feature/ff", "refs/heads/main", default));
        Assert.False(layer.IsOpen);
    }

    // --- Helpers -------------------------------------------------------------------------------

    /// <summary>A menu as the report writes it (the same form as <c>MenuSnapshot.Lines</c>).</summary>
    internal static List<string> Lines(MenuModel menu) =>
    [
        .. menu.Entries.Select(entry => entry switch
        {
            MenuHeader header => header.Text,
            MenuSeparator => "—",
            MenuItem { IsEnabled: false, DisabledReason: { } reason } item => string.Create(CultureInfo.InvariantCulture, $"{item.Label} (disabled: \"{reason}\")"),
            MenuItem { IsEnabled: false } item => item.Label + " (disabled)",
            MenuItem { IsDestructive: true } item => item.Label + " (danger)",
            MenuItem item => item.Label,
            _ => "?",
        }),
    ];

    /// <summary>4a's height of a menu that fits: 8 + 24 + 26 × items + 9 × separators.</summary>
    internal static int Height(MenuModel menu) =>
        8 + 24 + (26 * menu.Items.Count()) + (9 * menu.Entries.OfType<MenuSeparator>().Count());

    /// <summary>Invokes every enabled item, in order.</summary>
    private static void InvokeAll(MenuModel menu)
    {
        foreach (var item in menu.Items.Where(item => item.IsEnabled))
        {
            item.Invoke();
        }
    }

    /// <summary>How the recorder writes a rebase item's call (docs/phase-4-design.md 1.1): onto the target's SHA as the menu read it, named for the texts.</summary>
    private static string Rebase(string sha, string name) => $"RebaseAsync({new RebaseTarget(sha, name)})";

    /// <summary>How the recorder writes an "Interactive rebase…" item's call (2.1).</summary>
    private static string Interactive(string sha, string name, string branchRef, bool fromHere = false, bool checkoutFirst = false) =>
        $"OpenInteractiveRebaseAsync({new InteractiveRebaseRequest(new RebaseTarget(sha, name), branchRef, fromHere, checkoutFirst)})";

    /// <summary>A call with its list arguments written out: <c>CopyShasAsync([a, b])</c>.</summary>
    private static string Format(RecordedAction call) =>
        $"{call.Method}({string.Join(", ", call.Arguments.Select(argument => argument switch
        {
            IEnumerable<string> shas => "[" + string.Join(", ", shas) + "]",
            IEnumerable<ChangedFile> files => "[" + string.Join(", ", files.Select(file => file.Path)) + "]",
            _ => argument?.ToString(),
        }))})";

    private static RefListRow Row(LeftPanelViewModel panel, string key) =>
        panel.Rows.SingleOrDefault(row => row.Key == key) ?? throw new InvalidOperationException($"The left panel has no row {key}: {string.Join(", ", panel.Rows.Select(row => row.Key))}.");

    /// <summary>Opens a closed folder or remote of the left panel, as a click does.</summary>
    private static void Open(LeftPanelViewModel panel, string key)
    {
        if (Row(panel, key) is RefItemRow { IsOpen: false } row)
        {
            panel.Activate(row);
        }
    }

    private static async Task WaitForMenuAsync(MenuLayerViewModel layer, string header) =>
        await WaitUntilAsync(() => layer.Current?.HeaderText == header, $"the menu {header}");

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > until)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A local branch tracking <c>origin/&lt;upstream&gt;</c>, 0 ahead and 0 behind.</summary>
    private static GitRef Tracking(string name, string upstream, bool isHead = false) => new(
        "refs/heads/" + name, name, RefKind.LocalBranch, MainWork,
        Upstream: "origin/" + upstream, IsHead: isHead,
        UpstreamRef: "refs/remotes/origin/" + upstream, UpstreamRemote: "origin", UpstreamRemoteRef: "refs/heads/" + upstream);

    /// <summary>HEAD on a clean <c>main</c> tracking <c>origin/main</c>, with the remote <c>origin</c>, plus <paramref name="more"/>.</summary>
    private static RepoRefs OpsLikeRefs(params GitRef[] more) => new(
        new HeadState("main", MainWork),
        [Tracking("main", "main", isHead: true), new GitRef("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, MainWork, RemoteName: "origin"), .. more],
        [],
        ["origin"]);

    private static RepositoryMenuContext Context(
        RepoRefs refs,
        WorkingTreeStatus? status = null,
        IReadOnlyList<CommitMenuInfo>? selected = null,
        IReadOnlyList<WorktreeInfo>? worktrees = null)
    {
        status ??= WorkingTreeStatus.Clean;
        return new RepositoryMenuContext("repo", "C:/repo", refs, status, selected ?? [], ToolbarViewModel.StatesFor(refs, status), IsOperationRunning: false)
        {
            OtherWorktrees = worktrees ?? [],
        };
    }

    /// <summary>The diff view's line actions, recorded.</summary>
    private sealed class RecordingDiffLines : IDiffMenuActions
    {
        public List<string> Calls { get; } = [];

        public Task StageLinesAsync() => Record("StageLines");

        public Task UnstageLinesAsync() => Record("UnstageLines");

        public Task DiscardLinesAsync() => Record("DiscardLines");

        public Task RunHunkActionAsync(int hunk, LineAction action) => Record(string.Create(CultureInfo.InvariantCulture, $"Hunk {hunk} {action}"));

        private Task Record(string call)
        {
            Calls.Add(call);
            return Task.CompletedTask;
        }
    }
}
