using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Git;
using VisualCommit.Testing;
using Xunit;
using MenuItem = VisualCommit.App.ViewModels.Menus.MenuItem;

namespace VisualCommit.App.Tests;

/// <summary>
/// The menus' snapshot (<see cref="RepositoryMenuContext"/>, built by
/// <see cref="RepositoryViewModel.MenuContext"/>) on real scenario repositories, and the shared
/// parts of the menus that WP7-core built: <see cref="RepositoryMenus.Build"/>'s separators
/// (D114), <see cref="RepositoryMenus.Item"/>, and <see cref="RepositoryViewModel.OpenMenu"/>.
/// The builders themselves are tested in <c>RepositoryMenusTests</c> (WP7-menus).
/// </summary>
public class RepositoryMenuContextTests
{
    [Fact]
    public async Task The_context_holds_the_refs_the_status_the_toolbar_and_the_selected_commits_in_row_order()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var head = await app.ShaAsync("HEAD");
        var alpha = await app.ShaAsync("refs/heads/feature/alpha");
        var initial = await app.ShaAsync("refs/heads/main~2");
        var stash = await app.ShaAsync("refs/stash");

        app.Select(initial, alpha, stash, head);
        var context = app.Repository.MenuContext;

        Assert.Equal(app.Repository.Name, context.RepositoryName);
        Assert.Equal(app.Repository.Path, context.WorkingDirectory);
        Assert.Equal("main", context.CurrentBranch?.Name);
        Assert.Equal("main", context.CurrentName);
        Assert.False(context.IsDetached);
        Assert.False(context.IsUnborn);
        Assert.True(context.HasRemotes);
        Assert.Equal(["origin"], context.Remotes);
        Assert.Equal(["feature/alpha", "feature/conflict", "feature/ff", "main"], context.LocalBranches.Select(gitRef => gitRef.Name));
        Assert.Equal(["origin/feature/alpha", "origin/main"], context.RemoteBranches.Select(gitRef => gitRef.Name));
        Assert.Equal(["v1.0", "v1.1"], context.Tags.Select(gitRef => gitRef.Name));
        Assert.Equal("stash@{0}", context.FindStash(stash)?.Name);
        Assert.Equal(RefKind.Tag, context.FindRef("refs/tags/v1.1")?.Kind);
        Assert.Equal(InProgressKind.None, context.InProgress.Kind);
        Assert.False(context.Status.HasChanges);
        Assert.False(context.IsOperationRunning);
        Assert.Empty(context.OtherWorktrees);
        Assert.Null(context.Drop);

        // The toolbar as 4d says for a clean main that tracks origin/main and a stash.
        Assert.Equal(new ToolbarAvailability(true, null), context.Toolbar.Fetch);
        Assert.Equal(new ToolbarAvailability(true, null), context.Toolbar.Pull);
        Assert.Equal(new ToolbarAvailability(false, "There are no changes to stash"), context.Toolbar.Stash);
        Assert.Equal(new ToolbarAvailability(true, null), context.Toolbar.Pop);

        // Row order, top first: the stash (12:07), Main work (HEAD), Alpha two, Initial commit.
        Assert.True(context.HasMultiSelection);
        Assert.Equal([stash, head, alpha, initial], context.SelectedCommits.Select(commit => commit.Sha));
        Assert.Equal(context.SelectedCommits.Select(commit => commit.Row).Order(), context.SelectedCommits.Select(commit => commit.Row));
        var byName = context.SelectedCommits.ToDictionary(commit => commit.Sha);
        Assert.True(byName[stash].IsStash);
        Assert.False(byName[stash].IsReachableFromHead);
        Assert.True(byName[head].IsReachableFromHead);
        Assert.Equal("Main work", byName[head].Subject);
        Assert.Equal(head[..7], byName[head].ShortSha);
        Assert.False(byName[alpha].IsReachableFromHead);
        Assert.True(byName[initial].IsReachableFromHead);
        Assert.All(context.SelectedCommits, commit => Assert.False(commit.IsMerge));
        Assert.Equal(byName[alpha], context.FindSelected(alpha));
        Assert.Null(context.FindSelected(new string('0', 40)));

        // Phase 4 (docs/phase-4-design.md 1.1, 2.1): where each is on HEAD's linear line, the
        // branch tips HEAD reaches (as git's merge-base says), and no merge tool read yet.
        Assert.Equal(0, byName[head].HeadLineDepth);
        Assert.Equal(2, byName[initial].HeadLineDepth);
        Assert.Null(byName[alpha].HeadLineDepth);
        Assert.Null(byName[stash].HeadLineDepth);
        var tips = context.LocalBranches.Concat(context.RemoteBranches).Select(gitRef => gitRef.TargetSha).Distinct().ToList();
        var onHead = new List<string>();
        foreach (var tip in tips)
        {
            if ((await repo.GitAllowingFailureAsync("merge-base", "--is-ancestor", tip, "HEAD")).ExitCode == 0)
            {
                onHead.Add(tip);
            }
        }

        Assert.Equal(onHead.Order(StringComparer.Ordinal), context.TipsOnHead.Order(StringComparer.Ordinal));
        Assert.Contains(head, context.TipsOnHead);
        Assert.DoesNotContain(alpha, context.TipsOnHead);
        Assert.Null(context.MergeTool);

        app.Select(alpha);
        Assert.False(app.Repository.MenuContext.HasMultiSelection);
        Assert.Equal([alpha], app.Repository.MenuContext.SelectedCommits.Select(commit => commit.Sha));
        app.Select();
        Assert.Empty(app.Repository.MenuContext.SelectedCommits);
    }

    [Fact]
    public async Task A_merge_commit_and_a_merge_in_progress_show_in_the_context()
    {
        using var repo = await Scenarios.OperationsAsync();
        var merge = await repo.MergeAsync("feature/alpha");
        Assert.False((await repo.GitAllowingFailureAsync("merge", "feature/conflict")).Succeeded);
        using var app = await OperationsHarness.OpenAsync(repo);

        app.Select(merge);
        var context = app.Repository.MenuContext;

        var commit = Assert.Single(context.SelectedCommits);
        Assert.True(commit.IsMerge);
        Assert.True(commit.IsReachableFromHead);
        Assert.Equal(InProgressKind.Merge, context.InProgress.Kind);
        Assert.Equal(new ToolbarAvailability(false, "Finish or abort the merge first"), context.Toolbar.Pull);

        // HEAD is the merge commit: it is HEAD's linear line on its own (phase 4 design 2.1).
        Assert.Equal(0, commit.HeadLineDepth);
        app.Select(await app.ShaAsync("HEAD~1"));
        Assert.Null(Assert.Single(app.Repository.MenuContext.SelectedCommits).HeadLineDepth);
        Assert.Equal([merge], app.Repository.Graph.LinearLineFromHead());
    }

    [Fact]
    public async Task A_drop_reads_whether_its_source_contains_a_local_or_remote_target()
    {
        // Phase 4 design 1.1: "{A} is up to date with {B}" for the drop's rebase items; the
        // fast-forward's own fact (4g) stays as phase 3 read it.
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        Assert.Equal(new DropFacts(false) { SourceContainsTarget = false }, await app.Repository.ReadDropFactsAsync("refs/heads/feature/alpha", "refs/heads/main"));
        Assert.Equal(new DropFacts(true) { SourceContainsTarget = true }, await app.Repository.ReadDropFactsAsync("refs/heads/feature/ff", "refs/heads/main"));
        Assert.Equal(new DropFacts(true) { SourceContainsTarget = true }, await app.Repository.ReadDropFactsAsync("refs/heads/feature/alpha", "refs/remotes/origin/feature/alpha"));
        Assert.Equal(new DropFacts(true) { SourceContainsTarget = false }, await app.Repository.ReadDropFactsAsync("refs/heads/main", "refs/remotes/origin/feature/alpha"));

        // A read that fails leaves both to git: the fast-forward enabled, the rebase items not known.
        Assert.Equal(new DropFacts(true) { SourceContainsTarget = null }, await app.Repository.ReadDropFactsAsync("refs/heads/main", "refs/remotes/origin/no-such-branch"));
    }

    [Fact]
    public async Task On_a_detached_head_the_context_names_HEAD_and_finds_the_other_worktrees_once_read()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        var before = app.Repository.MenuContext;
        var context = await app.Repository.ReadMenuContextAsync();

        Assert.True(context.IsDetached);
        Assert.Null(context.CurrentBranch);
        Assert.Equal("HEAD", context.CurrentName);
        Assert.Empty(before.OtherWorktrees);
        var worktree = Assert.Single(context.OtherWorktrees);
        Assert.Equal("refs/heads/side", worktree.Branch);
        Assert.Equal(Scenarios.EdgeCasesWorktree, Path.GetFileName(worktree.Path));
        Assert.Equal(worktree.Path, context.OtherWorktreeOf("refs/heads/side"));
        Assert.Null(context.OtherWorktreeOf("refs/heads/main"));
        Assert.Single(app.Repository.MenuContext.OtherWorktrees);

        var loc = context.FindRef("refs/heads/loc")!;
        Assert.True(loc.HasLocalUpstream);
        Assert.Equal("main", RepositoryMenuContext.UpstreamText(loc));
        var remote = context.FindRef("refs/remotes/origin/main")!;
        Assert.Equal("main", RepositoryMenuContext.LocalNameOf(remote));
        Assert.Null(RepositoryMenuContext.LocalNameOf(context.FindRef("refs/heads/origin/main")!));
    }

    [Fact]
    public async Task Before_the_refs_are_read_the_context_is_empty_with_every_toolbar_button_disabled()
    {
        using var repo = await Scenarios.LinearAsync();
        var git = await GitRepository.OpenAsync(
            new GitRunner(GitLocator.FindExecutable(GitSearchContext.FromSystem())!), repo.Path, TestContext.Current.CancellationToken);
        using var repository = new RepositoryViewModel(git, new MainWindowViewModelTests.FakeSettings(), new DateDisplay(TimeZoneInfo.Utc));

        var context = repository.MenuContext;

        Assert.True(context.IsUnborn);
        Assert.Empty(context.Refs.Refs);
        Assert.Empty(context.SelectedCommits);
        Assert.Same(ToolbarMenuState.Disabled, context.Toolbar);
    }

    [Fact]
    public void File_targets_know_their_name_extension_and_kind()
    {
        var log = new FileMenuTarget(new ChangedFile("logs/run.log", FileChangeKind.Added), IsUntracked: true, ExistsInWorkingTree: true);
        Assert.Equal(("run.log", "log", false, false), (log.FileName, log.Extension, log.IsConflicted, log.IsNestedRepository));
        Assert.Null(new FileMenuTarget(new ChangedFile("Makefile", FileChangeKind.Modified), false, true).Extension);
        Assert.Null(new FileMenuTarget(new ChangedFile(".gitignore", FileChangeKind.Modified), false, true).Extension);
        Assert.True(new FileMenuTarget(new ChangedFile("src/core.txt", FileChangeKind.Conflicted), false, true).IsConflicted);
        var nested = new FileMenuTarget(new ChangedFile("vendor/lib/", FileChangeKind.Added), true, true);
        Assert.Equal(("lib", true), (nested.FileName, nested.IsNestedRepository));
    }

    [Fact]
    public void A_menu_has_its_header_first_and_a_separator_only_between_groups_that_keep_items()
    {
        var (actions, _) = RecordingActions.Create();
        MenuItem Item(string label) => RepositoryMenus.Item(actions, label, () => Task.CompletedTask);

        var menu = RepositoryMenus.Build(
            new MenuHeader("feature/alpha"),
            [null, null],
            [Item("Check out feature/alpha"), null, Item("Merge feature/alpha into main")],
            [],
            [null],
            [Item("Copy branch name")],
            [null]);

        Assert.Equal("feature/alpha", menu.HeaderText);
        Assert.Equal(
            ["header feature/alpha", "Check out feature/alpha", "Merge feature/alpha into main", "separator", "Copy branch name"],
            menu.Entries.Select(entry => entry switch
            {
                MenuHeader header => "header " + header.Text,
                MenuSeparator => "separator",
                MenuItem item => item.Label,
                _ => "?",
            }));
        Assert.Equal(["header x"], RepositoryMenus.Build(new MenuHeader("x"), new MenuItem?[] { null }).Entries.Select(entry => "header " + ((MenuHeader)entry).Text));
    }

    [Fact]
    public async Task An_item_is_disabled_by_its_reason_and_starts_its_action_logging_what_fails_past_it()
    {
        var (actions, recorder) = RecordingActions.Create();

        var enabled = RepositoryMenus.Item(actions, "Fetch origin", () => actions.FetchAsync("origin"));
        var disabled = RepositoryMenus.Item(actions, "Pull", () => actions.PullAsync(), "HEAD is detached: check out a branch to pull");
        var destructive = RepositoryMenus.Item(actions, "Delete feature/ff…", () => actions.DeleteBranchAsync("refs/heads/feature/ff"), isDestructive: true);
        var failing = RepositoryMenus.Item(actions, "Broken", () => Task.FromException(new InvalidOperationException("boom")));
        var throwing = RepositoryMenus.Item(actions, "Throws", () => throw new InvalidOperationException("at once"));

        Assert.Equal((true, null, false), (enabled.IsEnabled, enabled.DisabledReason, enabled.IsDestructive));
        Assert.Equal((false, "HEAD is detached: check out a branch to pull"), (disabled.IsEnabled, disabled.DisabledReason));
        Assert.True(destructive.IsDestructive);

        enabled.Invoke();
        destructive.Invoke();
        failing.Invoke();
        throwing.Invoke();

        Assert.Equal(["FetchAsync(origin)", "DeleteBranchAsync(refs/heads/feature/ff)"], recorder.Calls.Select(call => call.ToString()));
        var until = DateTime.UtcNow.AddSeconds(10);
        while (recorder.Log.Messages.Count < 2 && DateTime.UtcNow < until)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Contains("The menu item \"Broken\" failed.", recorder.Log.Messages);
        Assert.Contains("The menu item \"Throws\" failed.", recorder.Log.Messages);
    }

    [Fact]
    public async Task OpenMenu_opens_a_menu_with_items_on_the_layer_and_nothing_once_the_tab_closed()
    {
        using var repo = await Scenarios.LinearAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var (actions, _) = RecordingActions.Create();
        var layer = new MenuLayerViewModel();
        var menu = RepositoryMenus.Build(new MenuHeader("main"), [RepositoryMenus.Item(actions, "Copy branch name", () => Task.CompletedTask)]);

        Assert.False(app.Repository.OpenMenu(layer, null, new MenuPosition(1, 2), MenuOpenedBy.Pointer));
        Assert.False(app.Repository.OpenMenu(layer, RepositoryMenus.Build(new MenuHeader("empty")), new MenuPosition(1, 2), MenuOpenedBy.Pointer));
        Assert.False(layer.IsOpen);

        Assert.True(app.Repository.OpenMenu(layer, menu, new MenuPosition(10, 20), MenuOpenedBy.Keyboard));
        Assert.Same(menu, layer.Current);
        Assert.Equal((new MenuPosition(10, 20), MenuOpenedBy.Keyboard), (layer.Position, layer.OpenedBy));

        layer.Close();
        app.Repository.Dispose();
        Assert.False(app.Repository.OpenMenu(layer, menu, new MenuPosition(10, 20), MenuOpenedBy.Pointer));
        Assert.False(layer.IsOpen);
    }
}
