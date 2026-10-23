using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Commands;
using VisualCommit.App.ViewModels.Diff;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using Xunit;
using MenuItem = VisualCommit.App.ViewModels.Menus.MenuItem;

namespace VisualCommit.App.Tests;

/// <summary>
/// Which menu items carry a key (docs/phase-6-design.md 6.7, 6.12, D215): exactly the items that
/// run the keyed command on the same object, on phase 3's "ops" repository as its menus read it
/// (<see cref="RepositoryViewModel.MenuContext"/>): the empty graph area's, the working changes',
/// the sections', the checked-out branch's pull and push and not another branch's, the newest
/// stash's pop only, the open diff's file history and blame and not a file row's, and the
/// conflict resolver's and the interactive list's own keys.
/// </summary>
public class RepositoryMenusKeyTests
{
    [Fact]
    public async Task The_menus_of_the_repository_carry_exactly_6_7s_keys()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var (actions, _) = RecordingActions.Create();
        var context = app.Repository.MenuContext;

        Assert.Equal(
            [("Fetch all remotes", AppCommand.Fetch), ("Pull", AppCommand.Pull), ("Push", AppCommand.Push), ("Create branch…", AppCommand.CreateBranch), ("Stash changes…", AppCommand.Stash), ("Show reflog", AppCommand.ShowReflog), ("Show backups", null), ("Manage remotes…", null)],
            Keyed(RepositoryMenus.ForEmptyGraphArea(context, actions)));

        // The checked-out branch's pull and push are the toolbar's; another branch's are not keyed.
        var main = Keyed(RepositoryMenus.ForLocalBranch(context, "refs/heads/main", actions));
        Assert.Equal([("Pull from origin/main", AppCommand.Pull), ("Push main to origin/main", AppCommand.Push)], main.Where(item => item.Command is not null));
        Assert.Contains(("Pull from origin/main (rebase)", (AppCommand?)null), main);
        var alpha = Keyed(RepositoryMenus.ForLocalBranch(context, "refs/heads/feature/alpha", actions));
        Assert.Contains(("Push feature/alpha to origin/feature/alpha", (AppCommand?)null), alpha);
        Assert.DoesNotContain(alpha, item => item.Command is not null);
        Assert.DoesNotContain(Keyed(RepositoryMenus.ForRemoteBranch(context, "refs/remotes/origin/main", actions)), item => item.Command is not null);
        Assert.DoesNotContain(Keyed(RepositoryMenus.ForTag(context, "refs/tags/v1.0", actions)), item => item.Command is not null);

        // Only the newest stash's Pop stash is the toolbar's Pop.
        var stashes = context.Refs.Stashes;
        var older = stashes[0] with { Index = 1, Sha = new string('f', 40), Message = "On main: Older" };
        var two = context with { Refs = context.Refs with { Stashes = [stashes[0], older] } };
        Assert.Equal([("Apply stash", null), ("Pop stash", AppCommand.Pop), ("Drop stash…", null), ("Copy SHA", null)], Keyed(RepositoryMenus.ForStash(two, stashes[0].Sha, actions)));
        Assert.DoesNotContain(Keyed(RepositoryMenus.ForStash(two, older.Sha, actions)), item => item.Command is not null);

        // The sections.
        Assert.Equal([AppCommand.CreateBranch, AppCommand.ShowReflog], Commands(RepositoryMenus.ForSection(context, RefSection.LocalBranches, actions)!));
        Assert.Equal([AppCommand.Fetch], Commands(RepositoryMenus.ForSection(context, RefSection.Remotes, actions)!));
        Assert.Equal([AppCommand.Stash], Commands(RepositoryMenus.ForSection(context, RefSection.Stashes, actions)!));
        Assert.Empty(Commands(RepositoryMenus.ForSection(context, RefSection.Tags, actions)!));
    }

    [Fact]
    public async Task The_working_changes_and_the_open_diff_carry_their_keys_and_a_file_row_none()
    {
        using var repo = await Scenarios.OperationsAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "src", "core.txt"), "core\nmain work\nmore\n", TestContext.Current.CancellationToken);
        using var app = await OperationsHarness.OpenAsync(repo);
        var (actions, _) = RecordingActions.Create();
        var context = app.Repository.MenuContext;
        Assert.True(context.Status.HasChanges);

        Assert.Equal(
            [("Stage all", AppCommand.StageAll), ("Unstage all", AppCommand.UnstageAll), ("Discard all changes…", null), ("Stash changes…", AppCommand.Stash), ("Create branch here…", AppCommand.CreateBranch), ("Show reflog", AppCommand.ShowReflog), ("Show backups", null), ("Show in folder", null)],
            Keyed(RepositoryMenus.ForWorkingChanges(context, actions)));

        var file = new FileMenuTarget(new ChangedFile("src/core.txt", FileChangeKind.Modified), IsUntracked: false, ExistsInWorkingTree: true);
        var diff = Keyed(RepositoryMenus.ForDiff(context, new DiffMenuTarget(file, DiffSide.Unstaged, HasSelectedLines: false, Hunk: null, ActsOnLines: false), actions, new NoLines()));
        Assert.Equal([("File history", AppCommand.FileHistoryOfDiff), ("Blame", AppCommand.BlameOfDiff)], diff.Where(item => item.Command is not null));
        Assert.DoesNotContain(Keyed(RepositoryMenus.ForUnstagedFile(context, file, actions)), item => item.Command is not null);
        Assert.DoesNotContain(Keyed(RepositoryMenus.ForCommitFile(context, file with { CommitSha = "88d6b91e821a43cd39c94aa7892c8095b628caf8" }, actions)), item => item.Command is not null);
    }

    [Fact]
    public void The_resolvers_menus_show_the_editors_text_keys()
    {
        var actions = new ConflictResultMenuActions(() => { }, () => { }, () => { }, () => { }, () => { }, () => { });
        Assert.Equal(
            [("Undo", AppCommand.TextUndo), ("Redo", AppCommand.TextRedo), ("Cut", AppCommand.Cut), ("Copy", AppCommand.Copy), ("Paste", AppCommand.Paste), ("Select all", AppCommand.SelectAll)],
            Keyed(ConflictMenus.ForConflictResult("notes.txt", new ConflictResultMenuState(true, true, true), actions)));
        Assert.Equal([("Copy", AppCommand.Copy)], Keyed(ConflictMenus.ForConflictPane("Ours: main", hasSelection: true, () => { })));
        Assert.Equal(
            ["Ctrl+Z", "Ctrl+Y", "Ctrl+X", "Ctrl+C", "Ctrl+V", "Ctrl+A"],
            Commands(ConflictMenus.ForConflictResult("notes.txt", new ConflictResultMenuState(true, true, true), actions)).Select(command => new KeyTexts(CommandKey.Ctrl).Of(command)));
        Assert.Equal(
            ["Cmd+Z", "Cmd+Shift+Z", "Cmd+X", "Cmd+C", "Cmd+V", "Cmd+A"],
            Commands(ConflictMenus.ForConflictResult("notes.txt", new ConflictResultMenuState(true, true, true), actions)).Select(command => new KeyTexts(CommandKey.Cmd).Of(command)));
    }

    private static List<(string Label, AppCommand? Command)> Keyed(MenuModel menu) => [.. menu.Items.Select(item => (item.Label, item.Command))];

    private static List<AppCommand> Commands(MenuModel menu) => [.. menu.Items.Select(item => item.Command).OfType<AppCommand>()];

    private sealed class NoLines : IDiffMenuActions
    {
        public Task StageLinesAsync() => Task.CompletedTask;

        public Task UnstageLinesAsync() => Task.CompletedTask;

        public Task DiscardLinesAsync() => Task.CompletedTask;

        public Task RunHunkActionAsync(int hunk, LineAction action) => Task.CompletedTask;
    }
}
