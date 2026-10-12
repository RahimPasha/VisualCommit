using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The stash and tag operations (docs/phase-3-design.md 2.24 to 2.28, D91, D108, D119, D121;
/// visual checks 15, 15b and 16) against real scenario repositories: the dialogs and
/// confirmations with their texts, the backups and what they hold, the repository read back with
/// plain git, the status bar, the failure dialogs, a cancel, a closed tab, and that each
/// operation runs once.
/// </summary>
[Collection(RepositoryOperationsTestSupport.StashTagsCollection)]
public class RepositoryOperationsStashTagsTests
{
    private const string Experiment = "On main: Experiment";

    // 2.24: stash save.

    [Fact]
    public async Task Stash_saves_the_changes_and_the_untracked_files_through_the_dialog()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmore work\n").WriteFile("todo.txt", "Later\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Stash("Save my work");

        await app.Operations.StashAsync();

        var dialog = Assert.IsType<StashDialogViewModel>(Assert.Single(app.Dialogs.Shown));
        Assert.Equal(("Stash changes", "Stash", "Include untracked files", "Message (optional)"), (dialog.Title, dialog.ConfirmText, dialog.OptionText, dialog.MessagePlaceholder));
        Assert.True(dialog.IsOptionChecked);
        Assert.False(dialog.ShowsName);
        Assert.Equal(["On main: Save my work", Experiment], (await repo.StashListAsync()).Select(entry => entry.Message));
        Assert.Equal(string.Empty, (await repo.GitAsync("status", "--porcelain")).StandardOutput);
        Assert.Equal("todo.txt", (await repo.GitAsync("ls-tree", "-r", "--name-only", "stash@{0}^3")).StandardOutput.Trim());
        Assert.False(File.Exists(Path.Combine(repo.Path, "todo.txt")));
        Assert.Equal("Stashed the changes", app.StatusText);
        Assert.Equal(OperationStatusTone.Secondary, app.StatusTone);
        Assert.False(app.Repository.HasWorkingChanges);
        Assert.Equal(2, app.Operations.Refs!.Stashes.Count);
        Assert.True(app.Settings.Current.StashIncludeUntracked);
        Assert.Empty(await repo.BackupRefsAsync());
        AssertRanOnce(app, "Stash");
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Stash_without_untracked_files_leaves_them_and_the_dialog_remembers_the_confirmed_choice_only()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmore work\n").WriteFile("todo.txt", "Later\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var main = await app.ShaAsync("main");
        app.Dialogs.Stash(includeUntracked: false).Answer<StashDialogViewModel>(dialog =>
        {
            // Opened unticked (the last confirmed choice); ticked here, then cancelled: a
            // cancelled dialog saves nothing (6.2).
            Assert.False(dialog.IsOptionChecked);
            dialog.IsOptionChecked = true;
            dialog.Cancel();
        }).Cancel();

        await app.Operations.StashAsync();
        await app.Operations.StashAsync();
        await app.Operations.StashAsync();

        var shown = app.Dialogs.Shown.Cast<StashDialogViewModel>().ToList();
        Assert.Equal(3, shown.Count);
        Assert.Equal(string.Empty, shown[0].Message);
        Assert.False(app.Settings.Current.StashIncludeUntracked);
        Assert.False(shown[2].IsOptionChecked);
        Assert.Equal([$"WIP on main: {main[..7]} Main work", Experiment], (await repo.StashListAsync()).Select(entry => entry.Message));
        Assert.Equal("?? todo.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
        Assert.Equal("Later\n", File.ReadAllText(Path.Combine(repo.Path, "todo.txt")));
        Assert.Equal("Stashed the changes", app.StatusText);
        AssertRanOnce(app, "Stash");
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Stash_with_nothing_to_stash_says_so_in_the_status_bar()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Stash();

        await app.Operations.StashAsync();

        Assert.Equal("There was nothing to stash", app.StatusText);
        Assert.Equal(OperationStatusTone.Secondary, app.StatusTone);
        Assert.Equal(OperationOutcome.Succeeded, app.LastResult?.Outcome);
        Assert.Equal([Experiment], (await repo.StashListAsync()).Select(entry => entry.Message));
        Assert.Empty(app.Dialogs.Errors);
        app.AssertScriptFollowed();
    }

    [Theory]
    [InlineData(Scenarios.InProgressKind.Merge, "Finish or abort the merge first")]
    [InlineData(Scenarios.InProgressKind.CherryPick, "Finish or abort the cherry-pick first")]
    [InlineData(Scenarios.InProgressKind.Rebase, "Finish or abort the rebase first")]
    [InlineData(Scenarios.InProgressKind.None, "Resolve the conflicts first")]
    public async Task Stash_is_refused_while_an_operation_or_its_conflicts_are_in_progress(Scenarios.InProgressKind kind, string reason)
    {
        using var repo = await Scenarios.StoppedAsync(kind);
        using var app = await OperationsHarness.OpenAsync(repo);
        var stashes = await repo.StashListAsync();
        var status = (await repo.GitAsync("status", "--porcelain")).StandardOutput;
        app.Dialogs.CloseError();

        await app.Operations.StashAsync();

        Assert.Empty(app.Dialogs.Shown.OfType<StashDialogViewModel>());
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Stash failed", reason), (error.Title, error.Summary));
        Assert.Equal("Stash failed", app.StatusText);
        Assert.Equal(OperationStatusTone.Danger, app.StatusTone);
        Assert.Equal(stashes, await repo.StashListAsync());
        Assert.Equal(status, (await repo.GitAsync("status", "--porcelain")).StandardOutput);
        if (kind == Scenarios.InProgressKind.Merge)
        {
            // D119: the pending merge is still there (a stash would have removed MERGE_HEAD).
            Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));
        }

        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Stash_is_refused_before_the_first_commit_with_the_toolbars_reason()
    {
        // The guard gives the toolbar's and the menus' reason, in 2.24's order (D119): an unborn
        // HEAD first, although the working tree has something to stash.
        using var repo = await TempRepo.CreateAsync("unborn");
        repo.WriteFile("notes.txt", "Notes\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        Assert.Equal("Make the first commit first", app.Repository.Toolbar.StashToolTip);
        app.Dialogs.CloseError();

        await app.Operations.StashAsync();

        Assert.Empty(app.Dialogs.Shown.OfType<StashDialogViewModel>());
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Stash failed", "Make the first commit first"), (error.Title, error.Summary));
        Assert.Equal(("Stash failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        Assert.Equal("?? notes.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Stash_is_refused_when_an_operation_started_while_its_dialog_was_open()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("todo.txt", "Later\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.Answer<StashDialogViewModel>(dialog =>
        {
            // A merge started in a terminal while the dialog is open: it stops with a conflict.
            repo.GitAllowingFailureAsync("merge", "feature/conflict").GetAwaiter().GetResult();
            dialog.ConfirmCommand.Execute(null);
        }).CloseError();

        await app.Operations.StashAsync();

        Assert.Equal(("Stash failed", "Finish or abort the merge first"), (app.Dialogs.Errors.Single().Title, app.Dialogs.Errors.Single().Summary));
        Assert.Equal([Experiment], (await repo.StashListAsync()).Select(entry => entry.Message));
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")));
        app.AssertScriptFollowed();
    }

    // 2.25: apply and pop.

    [Fact]
    public async Task Apply_applies_the_stash_and_keeps_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var stash = await app.ShaAsync("refs/stash");

        await app.Operations.ApplyStashAsync(stash);

        Assert.Equal("Applied stash@{0}", app.StatusText);
        Assert.Equal([stash], (await repo.StashListAsync()).Select(entry => entry.Sha));
        Assert.Equal(Scenarios.OperationsFiles.ReadmeStashed, File.ReadAllText(Path.Combine(repo.Path, "README.md")));
        Assert.Equal(" M README.md", (await repo.GitAsync("status", "--porcelain")).StandardOutput.TrimEnd());
        Assert.True(app.Repository.HasWorkingChanges);
        Assert.Empty(app.Dialogs.Shown);
        AssertRanOnce(app, "Apply stash@{0}");
    }

    [Fact]
    public async Task Pop_and_the_toolbars_pop_apply_the_newest_stash_and_drop_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nmore work\n");
        await repo.StashAsync("Second");
        using var app = await OperationsHarness.OpenAsync(repo);
        var experiment = (await repo.StashListAsync())[1].Sha;

        await app.Operations.PopNewestStashAsync();

        Assert.Equal("Popped stash@{0}", app.StatusText);
        Assert.Equal([experiment], (await repo.StashListAsync()).Select(entry => entry.Sha));
        Assert.Equal("core\nmain work\nmore work\n", File.ReadAllText(Path.Combine(repo.Path, "src", "core.txt")));
        Assert.Single(app.Operations.Refs!.Stashes);

        // The other stash by its commit: README.md does not clash with core.txt.
        await app.Operations.PopStashAsync(experiment);

        Assert.Equal("Popped stash@{0}", app.StatusText);
        Assert.Empty(await repo.StashListAsync());
        Assert.Equal(Scenarios.OperationsFiles.ReadmeStashed, File.ReadAllText(Path.Combine(repo.Path, "README.md")));
        Assert.Equal(2, app.Log.Messages.Count(message => message.Contains(": Pop stash@{0}: Succeeded after ", StringComparison.Ordinal)));
        Assert.Empty(app.Dialogs.Shown);
    }

    [Fact]
    public async Task A_stash_is_found_by_its_commit_and_named_as_it_is_listed_when_it_runs()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var experiment = await app.ShaAsync("refs/stash");

        // Outside the app a new stash pushes Experiment to stash@{1}: the commit the menu captured
        // still names it.
        repo.WriteFile("src/core.txt", "core\nmain work\nmore work\n");
        await repo.StashAsync("Newer");
        await app.Repository.RefreshAsync();
        await app.Operations.ApplyStashAsync(experiment);

        Assert.Equal("Applied stash@{1}", app.StatusText);
        Assert.Equal(Scenarios.OperationsFiles.ReadmeStashed, File.ReadAllText(Path.Combine(repo.Path, "README.md")));
        Assert.Equal("core\nmain work\n", File.ReadAllText(Path.Combine(repo.Path, "src", "core.txt")));
    }

    [Fact]
    public async Task A_pop_refused_by_local_changes_opens_the_error_dialog_and_changes_nothing()
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.CommitFileAsync("README.md", "# Operations\n\nDifferent.\n", "Change README");
        repo.WriteFile("README.md", "# Operations\n\nLocal edit.\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var stash = await app.ShaAsync("refs/stash");
        app.Dialogs.CloseError();

        await app.Operations.PopNewestStashAsync();

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal("Applying the stash failed", error.Title);
        Assert.Equal("Your changes to these files would be overwritten by applying stash@{0}.", error.Summary);
        Assert.Contains("Your local changes to the following files would be overwritten by merge:", error.Output, StringComparison.Ordinal);
        Assert.Contains("README.md", error.Output, StringComparison.Ordinal);
        Assert.Equal("git stash pop stash@{0}", error.Command);
        Assert.False(error.HasExtraAction);
        Assert.Equal($"In {app.Repository.Name}", error.RepositoryText);
        Assert.Equal("Pop stash@{0} failed", app.StatusText);
        Assert.Equal(OperationStatusTone.Danger, app.StatusTone);
        Assert.Equal([stash], (await repo.StashListAsync()).Select(entry => entry.Sha));
        Assert.Equal("# Operations\n\nLocal edit.\n", File.ReadAllText(Path.Combine(repo.Path, "README.md")));
        AssertRanOnce(app, "Pop stash@{0}");
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_pop_whose_untracked_files_exist_says_that_the_tracked_changes_were_applied_and_the_stash_is_kept()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("src/core.txt", "core\nmain work\nstashed\n").WriteFile("notes.txt", "stashed notes\n");
        await repo.GitAsync("stash", "push", "--include-untracked", "--message", "With notes");
        repo.WriteFile("notes.txt", "my notes\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var stash = await app.ShaAsync("refs/stash");
        app.Dialogs.CloseError();

        await app.Operations.PopNewestStashAsync();

        // Git applied the tracked part, left notes.txt alone and kept the entry.
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal("Applying the stash failed", error.Title);
        Assert.Equal(
            "The tracked changes of stash@{0} were applied, but its untracked files were not: files of those names already exist. The stash is kept.",
            error.Summary);
        Assert.Contains("notes.txt already exists, no checkout", error.Output, StringComparison.Ordinal);
        Assert.Equal("git stash pop stash@{0}", error.Command);
        Assert.Equal("Pop stash@{0} failed", app.StatusText);
        Assert.Equal("core\nmain work\nstashed\n", File.ReadAllText(Path.Combine(repo.Path, "src", "core.txt")));
        Assert.Equal("my notes\n", File.ReadAllText(Path.Combine(repo.Path, "notes.txt")));
        Assert.Contains(stash, (await repo.StashListAsync()).Select(entry => entry.Sha));
        app.AssertScriptFollowed();
    }

    [Theory]
    [InlineData(true, "Applying the stash stopped: 1 file has conflicts. The stash is kept.")]
    [InlineData(false, "Applying the stash stopped: 1 file has conflicts")]
    public async Task A_stash_that_conflicts_stops_without_a_dialog_and_keeps_the_stash(bool pop, string message)
    {
        using var repo = await Scenarios.OperationsAsync();
        await repo.CommitFileAsync("README.md", "# Operations\n\nDifferent.\n", "Change README");
        using var app = await OperationsHarness.OpenAsync(repo);
        var stash = await app.ShaAsync("refs/stash");

        await (pop ? app.Operations.PopStashAsync(stash) : app.Operations.ApplyStashAsync(stash));

        Assert.Equal(OperationOutcome.Conflicts, app.LastResult?.Outcome);
        Assert.Equal(message, app.StatusText);
        Assert.Equal(OperationStatusTone.Warning, app.StatusTone);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal([stash], (await repo.StashListAsync()).Select(entry => entry.Sha));
        Assert.Equal("UU README.md", (await repo.GitAsync("status", "--porcelain")).StandardOutput.TrimEnd());
        Assert.Contains("<<<<<<< Updated upstream", File.ReadAllText(Path.Combine(repo.Path, "README.md")), StringComparison.Ordinal);
        Assert.Equal(InProgressKind.None, app.Operations.Status.InProgress.Kind);
        Assert.Contains(app.Operations.Status.Unstaged, file => file is { Path: "README.md", Kind: FileChangeKind.Conflicted });
    }

    [Fact]
    public async Task A_stash_that_no_longer_exists_says_so_whether_the_app_had_read_it_or_not()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var stash = await app.ShaAsync("refs/stash");
        app.Dialogs.CloseError().CloseError().CloseError();

        // Dropped in a terminal: the app still lists it, so git is asked and the stash is not found.
        await repo.GitAsync("stash", "drop");
        await app.Operations.ApplyStashAsync(stash);

        // Read again: the app no longer lists it, and nothing runs.
        await app.Repository.RefreshAsync();
        await app.Operations.PopStashAsync(stash);
        await app.Operations.DropStashAsync(stash);

        Assert.Equal(3, app.Dialogs.Errors.Count);
        Assert.Equal(("Applying the stash failed", "This stash no longer exists."), (app.Dialogs.Errors[0].Title, app.Dialogs.Errors[0].Summary));
        Assert.Equal(("Applying the stash failed", "This stash no longer exists."), (app.Dialogs.Errors[1].Title, app.Dialogs.Errors[1].Summary));
        Assert.Equal(("Dropping the stash failed", "This stash no longer exists."), (app.Dialogs.Errors[2].Title, app.Dialogs.Errors[2].Summary));
        Assert.Equal($"Drop {stash[..7]} failed", app.StatusText);
        Assert.Empty(app.Dialogs.Confirmations);
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(string.Empty, (await repo.GitAsync("status", "--porcelain")).StandardOutput);
        app.AssertScriptFollowed();
    }

    // 2.26: drop.

    [Fact]
    public async Task Drop_asks_first_then_backs_the_stash_up_and_drops_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var stash = await app.ShaAsync("refs/stash");
        var main = await app.ShaAsync("main");
        app.Dialogs.Confirm(yes: false).Confirm();

        await app.Operations.DropStashAsync(stash);

        Assert.Equal([stash], (await repo.StashListAsync()).Select(entry => entry.Sha));
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);

        await app.Operations.DropStashAsync(stash);

        Assert.Equal(2, app.Dialogs.Confirmations.Count);
        var question = app.Dialogs.Confirmations[1];
        Assert.Equal(("Drop stash?", "Drop stash@{0} \"On main: Experiment\"?", "A backup is saved first, so this can be undone.", "Drop", true),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive));
        Assert.Empty(await repo.StashListAsync());
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches("^refs/visualcommit/backup/stash-[0-9]{8}T[0-9]{9}Z$", backup.Name);
        Assert.Equal(stash, backup.Sha);
        Assert.Equal(main, await app.ShaAsync(backup.Name + "^1"));
        Assert.Equal("Dropped stash@{0}", app.StatusText);
        Assert.Empty(app.Operations.Refs!.Stashes);
        AssertRanOnce(app, "Drop stash@{0}");

        // The backup brings the stash back, its index and all (2.0).
        await repo.GitAsync("stash", "store", "--message", Experiment, backup.Sha);
        Assert.Equal([stash], (await repo.StashListAsync()).Select(entry => entry.Sha));
        app.AssertScriptFollowed();
    }

    // 2.27: tag create.

    [Fact]
    public async Task Create_tag_makes_an_annotated_tag_that_keeps_its_message_as_typed()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var alphaTwo = await app.ShaAsync("feature/alpha");
        var errors = new List<string>();
        var enabled = new List<bool>();
        app.Dialogs.Answer<TagDialogViewModel>(dialog =>
        {
            errors.Add(dialog.NameError);
            enabled.Add(dialog.CanConfirm);
            foreach (var name in new[] { "v1.0", "v1 2", "v1.2" })
            {
                dialog.Name = name;
                errors.Add(dialog.NameError);
                enabled.Add(dialog.CanConfirm);
            }

            dialog.Message = "#42 hotfix";
            dialog.ConfirmCommand.Execute(null);
        });

        await app.Operations.CreateTagAsync(alphaTwo);

        var shown = Assert.IsType<TagDialogViewModel>(Assert.Single(app.Dialogs.Shown));
        Assert.Equal(("Create tag", "Create tag", $"At {alphaTwo[..7]} \"Alpha two\"", "Tag name", "Message (optional: makes an annotated tag)"),
            (shown.Title, shown.ConfirmText, shown.Context, shown.NamePlaceholder, shown.MessagePlaceholder));
        Assert.Equal([string.Empty, "A tag named v1.0 exists.", "This is not a valid tag name: it contains a space.", string.Empty], errors);
        Assert.Equal([false, false, false, true], enabled);
        Assert.Equal("tag", (await repo.GitAsync("cat-file", "-t", "v1.2")).StandardOutput.Trim());
        var tag = (await repo.GitAsync("cat-file", "-p", "v1.2")).StandardOutput;
        Assert.StartsWith($"object {alphaTwo}\ntype commit\ntag v1.2\ntagger Test Author <author@example.com> ", tag, StringComparison.Ordinal);
        Assert.EndsWith("\n\n#42 hotfix\n", tag, StringComparison.Ordinal);
        Assert.Equal("Created tag v1.2", app.StatusText);
        Assert.Equal(["v1.0", "v1.1", "v1.2"], app.Operations.TagNames);
        Assert.Empty(await repo.BackupRefsAsync());
        AssertRanOnce(app, "Create tag v1.2");
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Create_tag_is_lightweight_without_a_message_and_goes_on_the_commit_of_a_branch_a_tag_or_HEAD()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var ff = await app.ShaAsync("feature/ff");
        var core = await app.ShaAsync("v1.0^{commit}");
        var head = await app.ShaAsync("HEAD");
        app.Dialogs.EnterName("ff-point").EnterName("on-v1.0", "Annotated on an annotated tag").EnterName("at-head");

        await app.Operations.CreateTagAsync("refs/heads/feature/ff");
        await app.Operations.CreateTagAsync("refs/tags/v1.0");
        await app.Operations.CreateTagAsync(null);

        Assert.Equal(
            [$"At {ff[..7]} \"Fast-forward me\"", $"At {core[..7]} \"Add core\"", $"At {head[..7]} \"Main work\""],
            app.Dialogs.Shown.Cast<TagDialogViewModel>().Select(dialog => dialog.Context));
        Assert.Equal("commit", (await repo.GitAsync("cat-file", "-t", "ff-point")).StandardOutput.Trim());
        Assert.Equal(ff, await repo.RefTargetAsync("refs/tags/ff-point"));

        // D121: the tag of a tag points at the commit, not at the tag object.
        Assert.StartsWith($"object {core}\ntype commit\n", (await repo.GitAsync("cat-file", "-p", "on-v1.0")).StandardOutput, StringComparison.Ordinal);
        Assert.Equal(head, await repo.RefTargetAsync("refs/tags/at-head"));
        Assert.Equal("Created tag at-head", app.StatusText);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Create_tag_shows_gits_refusal_and_does_nothing_when_cancelled()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);

        app.Dialogs.Cancel().Answer<TagDialogViewModel>(dialog =>
        {
            // Made in a terminal while the dialog is open: the dialog cannot know it, git refuses.
            repo.TagAsync("v9", "feature/ff").GetAwaiter().GetResult();
            dialog.Name = "v9";
            dialog.ConfirmCommand.Execute(null);
        }).CloseError();

        await app.Operations.CreateTagAsync(null);
        Assert.Equal(OperationStatusViewModel.ReadyText, app.StatusText);
        await app.Operations.CreateTagAsync(null);

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Creating the tag failed", "Git could not create the tag v9."), (error.Title, error.Summary));
        Assert.Contains("already exists", error.Output, StringComparison.Ordinal);
        Assert.StartsWith("git tag v9 ", error.Command, StringComparison.Ordinal);
        Assert.Equal("Create tag v9 failed", app.StatusText);
        Assert.Equal(await app.ShaAsync("feature/ff"), await repo.RefTargetAsync("refs/tags/v9"));
        app.AssertScriptFollowed();
    }

    // 2.28: tag delete.

    [Fact]
    public async Task Delete_tag_asks_first_then_backs_the_tag_object_up_and_deletes_it()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var tagObject = await repo.RefTargetAsync("refs/tags/v1.0");
        app.Dialogs.Confirm(yes: false).Confirm();

        await app.Operations.DeleteTagAsync("refs/tags/v1.0");

        Assert.Equal(tagObject, await repo.RefTargetAsync("refs/tags/v1.0"));
        Assert.Empty(await repo.BackupRefsAsync());

        await app.Operations.DeleteTagAsync("refs/tags/v1.0");

        var question = app.Dialogs.Confirmations[1];
        Assert.Equal(("Delete tag?", "Delete the tag v1.0 here? It stays on remotes it was pushed to.", "A backup is saved first, so this can be undone.", "Delete", true),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive));
        Assert.Null(await repo.RefTargetAsync("refs/tags/v1.0"));
        var backup = Assert.Single(await repo.BackupRefsAsync());
        Assert.Matches("^refs/visualcommit/backup/tag-[0-9]{8}T[0-9]{9}Z/v1.0$", backup.Name);
        Assert.Equal(tagObject, backup.Sha);
        Assert.Equal("tag", (await repo.GitAsync("cat-file", "-t", backup.Name)).StandardOutput.Trim());
        Assert.Contains("\ntag v1.0\n", (await repo.GitAsync("cat-file", "-p", backup.Name)).StandardOutput, StringComparison.Ordinal);
        Assert.Equal("Deleted tag v1.0", app.StatusText);
        Assert.Equal(["v1.1"], app.Operations.TagNames);
        AssertRanOnce(app, "Delete tag v1.0");
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task Delete_tag_of_a_tag_that_is_gone_fails_before_it_changes_anything()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        await repo.GitAsync("tag", "--delete", "v1.1");
        app.Dialogs.Confirm().CloseError();

        await app.Operations.DeleteTagAsync("refs/tags/v1.1");

        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Deleting the tag failed", "Git could not delete the tag v1.1."), (error.Title, error.Summary));
        Assert.Equal("Delete tag v1.1 failed", app.StatusText);
        Assert.Empty(await repo.BackupRefsAsync());
        app.AssertScriptFollowed();
    }

    // Every operation of this part after its tab closed.

    [Fact]
    public async Task After_the_tab_closed_no_stash_or_tag_operation_shows_or_changes_anything()
    {
        using var repo = await Scenarios.OperationsAsync();
        repo.WriteFile("todo.txt", "Later\n");
        using var app = await OperationsHarness.OpenAsync(repo);
        var stash = await app.ShaAsync("refs/stash");
        var refs = (await repo.GitAsync("for-each-ref")).StandardOutput;
        app.Repository.Dispose();

        await app.Operations.StashAsync();
        await app.Operations.ApplyStashAsync(stash);
        await app.Operations.PopStashAsync(stash);
        await app.Operations.PopNewestStashAsync();
        await app.Operations.DropStashAsync(stash);
        await app.Operations.CreateTagAsync(null);
        await app.Operations.DeleteTagAsync("refs/tags/v1.0");

        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal(refs, (await repo.GitAsync("for-each-ref")).StandardOutput);
        Assert.Equal("?? todo.txt", (await repo.GitAsync("status", "--porcelain")).StandardOutput.Trim());
        Assert.DoesNotContain(app.Log.Messages, message => message.Contains("Succeeded after", StringComparison.Ordinal));
    }
}
