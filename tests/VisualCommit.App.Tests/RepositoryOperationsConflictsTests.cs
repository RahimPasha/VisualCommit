using Avalonia.Headless.XUnit;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;
using Stop = VisualCommit.Testing.Scenarios.ConflictsStop;

namespace VisualCommit.App.Tests;

/// <summary>
/// WP4: the conflict resolver's actions (<see cref="IConflictActions"/>, docs/phase-4-design.md
/// 11.6 to 11.9, D148, D149) as <see cref="RepositoryOperations"/> runs them against real git
/// through <see cref="OperationsHarness"/>: "Resolve {file}" for a result, a side, a deletion;
/// the merge tool as an operation that can be cancelled, and nothing without <c>merge.tool</c>;
/// the reads; and the question that staging a file with conflict markers asks (7.1, 7.2).
/// </summary>
[Collection(RepositoryOperationsTestSupport.RebaseCollection)]
public class RepositoryOperationsConflictsTests
{
    [Fact]
    public async Task Mark_resolved_writes_the_result_and_stages_it_as_one_operation()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        IConflictActions actions = app.Operations;
        var result = "line 1\nline 2 ours\nresolved\n"u8.ToArray();
        var entry = app.Operations.Status.FindConflict("notes.txt")!;
        var read = File.ReadAllBytes(Path.Combine(repo.Path, "notes.txt"));

        Assert.Equal(new MarkResolvedResult(MarkResolvedOutcome.Resolved), await actions.MarkResolvedAsync(entry, result, read));

        Assert.Equal(("Resolved notes.txt", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        AssertRanOnce(app, "Resolve notes.txt");
        Assert.Equal(result, File.ReadAllBytes(Path.Combine(repo.Path, "notes.txt")));
        Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal("Merging: \"Merge branch 'feature/theirs'\". Conflicts resolved: commit to finish the merge.", app.Repository.Banner.Text);
    }

    [Fact]
    public async Task Mark_resolved_over_a_file_changed_since_it_was_read_writes_nothing_and_says_so_without_a_dialog()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        IConflictActions actions = app.Operations;
        var entry = app.Operations.Status.FindConflict("notes.txt")!;
        var read = File.ReadAllBytes(Path.Combine(repo.Path, "notes.txt"));
        repo.WriteFile("notes.txt", "resolved in an editor\n");

        // 11.7: the check runs inside "Resolve notes.txt", just before the write.
        var outcome = await actions.MarkResolvedAsync(entry, "the resolver's result\n"u8.ToArray(), read);

        Assert.Equal(MarkResolvedOutcome.ChangedOnDisk, outcome.Outcome);
        Assert.Equal("resolved in an editor\n"u8.ToArray(), outcome.OnDisk);
        Assert.Equal("resolved in an editor\n", Read(repo, "notes.txt"));
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));
        Assert.Equal(("notes.txt changed on disk: nothing was written", OperationStatusTone.Warning), (app.StatusText, app.StatusTone));
        AssertRanOnce(app, "Resolve notes.txt");
        Assert.Empty(app.Dialogs.Shown);

        // Expecting the file as it is now, the result is written and staged.
        Assert.Equal(MarkResolvedOutcome.Resolved, (await actions.MarkResolvedAsync(entry, "the resolver's result\n"u8.ToArray(), outcome.OnDisk)).Outcome);
        Assert.Equal("the resolver's result\n", Read(repo, "notes.txt"));
        Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
    }

    [AvaloniaFact]
    public async Task A_file_changed_while_mark_resolved_waits_in_the_queue_is_not_overwritten_without_asking()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitForRepositoryAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read", TimeSpan.FromSeconds(30));
        repository.OpenResolver(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted));
        await app.WaitForResolverAsync("notes.txt");
        var resolver = repository.Resolver;
        resolver.Choose(0, ConflictChoice.UseOurs);
        resolver.Choose(1, ConflictChoice.UseOurs);

        // Another operation runs: "Resolve notes.txt" waits behind it, the watcher paused (D81).
        var gate = new TaskCompletionSource();
        var other = repository.Queue.RunAsync(new GitOperation("Hold", "Holding…", OperationKind.Local, RefreshScope.None, async _ =>
        {
            await gate.Task;
            return OperationReport.Done(string.Empty);
        }));
        var marking = resolver.MarkResolvedCommand.ExecuteAsync(null);
        await app.WaitForAsync(() => resolver.IsBusy, "Mark resolved waiting", TimeSpan.FromSeconds(10));

        // Meanwhile an editor saves the file; then the other operation ends.
        repo.WriteFile("notes.txt", "resolved in an editor\n");
        gate.SetResult();
        await other;

        // 7.2's question; Cancel keeps the editor's file, unresolved, and the resolver's result.
        var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal(("notes.txt changed on disk.", "Overwrite"), (question.Title, question.ConfirmText));
        question.Cancel();
        await marking;
        await app.WaitForIdleAsync();
        Assert.Equal("resolved in an editor\n", Read(repo, "notes.txt"));
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));
        Assert.True(resolver.IsModified);
        Assert.Equal("notes.txt", resolver.Path);
    }

    [AvaloniaFact]
    public async Task A_file_changed_while_the_markers_question_is_open_is_asked_about_and_the_click_s_text_is_what_overwrites_it()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitForRepositoryAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read", TimeSpan.FromSeconds(30));
        repository.OpenResolver(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted));
        await app.WaitForResolverAsync("notes.txt");
        var resolver = repository.Resolver;
        var clicked = resolver.Document!.Text;

        // Mark resolved on the file as git left it: the markers question opens.
        var marking = resolver.MarkResolvedCommand.ExecuteAsync(null);
        var markers = await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal("notes.txt still has conflict markers.", markers.Title);

        // While it is open, an editor saves the file and a refresh reads the status.
        repo.WriteFile("notes.txt", "resolved in an editor\n");
        await repository.RefreshStatusAsync();
        await app.WaitForIdleAsync();
        Assert.Equal(clicked, resolver.Document!.Text);

        // Going ahead, the operation finds the file changed and writes nothing; 7.2 asks.
        markers.ConfirmCommand.Execute(null);
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is ConfirmationViewModel { Title: "notes.txt changed on disk." }, "the changed-on-disk question", TimeSpan.FromSeconds(30));
        var changed = app.Dialog<ConfirmationViewModel>();
        Assert.Equal("resolved in an editor\n", Read(repo, "notes.txt"));

        // Overwrite writes the text the click took.
        changed.ConfirmCommand.Execute(null);
        await marking;
        await app.WaitForIdleAsync();
        Assert.Equal(clicked.ReplaceLineEndings("\n"), Read(repo, "notes.txt").ReplaceLineEndings("\n"));
        Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
    }

    [Fact]
    public async Task A_card_keeps_or_deletes_a_file_and_takes_a_binary_side_each_as_one_operation()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.MergeKinds);
        using var app = await OperationsHarness.OpenAsync(repo);
        IConflictActions actions = app.Operations;

        // 11.8: "Keep the file" (ours, deleted by them), then "Use theirs" for the binary file, then "Delete it".
        Assert.True(await actions.TakeSideAsync("other.txt", ConflictSide.Ours));
        Assert.Equal("Resolved other.txt", app.StatusText);
        Assert.Equal(Scenarios.ConflictsFiles.OtherOurs, Read(repo, "other.txt"));

        Assert.True(await actions.TakeSideAsync("image.png", ConflictSide.Theirs));
        Assert.Equal("Resolved image.png", app.StatusText);
        Assert.Equal(Scenarios.ConflictsFiles.ImageKinds, File.ReadAllBytes(Path.Combine(repo.Path, "image.png")));

        Assert.True(await actions.RemoveAsync("new.txt"));
        Assert.Equal("Resolved new.txt", app.StatusText);
        Assert.False(File.Exists(Path.Combine(repo.Path, "new.txt")));
        Assert.DoesNotContain(app.Operations.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted);
        Assert.Equal(3, Ran(app, "Resolve other.txt") + Ran(app, "Resolve image.png") + Ran(app, "Resolve new.txt"));
    }

    [Fact]
    public async Task A_side_of_a_path_that_does_not_conflict_fails_in_the_error_dialog()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        IConflictActions actions = app.Operations;
        app.Dialogs.CloseError();

        Assert.False(await actions.TakeSideAsync("README.md", ConflictSide.Ours));

        var dialog = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Resolving failed", "README.md could not be marked resolved."), (dialog.Title, dialog.Summary));
        Assert.Equal(("Resolve README.md failed", OperationStatusTone.Danger), (app.StatusText, app.StatusTone));
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task The_merge_tool_runs_as_an_operation_that_can_be_cancelled_and_resolves_the_file()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge, mergeTool: true);
        using var app = await OperationsHarness.OpenAsync(repo);
        IConflictActions actions = app.Operations;
        var running = new List<(string Title, string Text, bool CanCancel)>();
        app.Repository.Queue.Finished += (_, e) => running.Add((e.Operation.Title, e.Operation.RunningText, e.Operation.CanBeCancelled));

        Assert.True(await actions.RunMergeToolAsync("notes.txt"));

        // 11.9, D149: "Merge tool for {file}", waiting for the tool, a local operation the user can cancel.
        Assert.Equal([("Merge tool for notes.txt", "Waiting for fake to close…", true)], running);
        Assert.Equal(("Resolved notes.txt in fake", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        Assert.Equal(Scenarios.ConflictsFiles.FakeMergeToolResult, Read(repo, "notes.txt"));
        Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
        Assert.Contains(app.Calls.Snapshot(), call => call.CommandText == "git -c mergetool.keepBackup=false mergetool --tool=fake --no-prompt -- notes.txt");
    }

    [AvaloniaFact]
    public async Task Open_in_merge_tool_on_the_file_the_resolver_shows_runs_the_resolvers_own_and_asks_first_for_a_modified_result()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge, mergeTool: true);
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitForRepositoryAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read", TimeSpan.FromSeconds(30));
        repository.OpenResolver(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted));
        await app.WaitForResolverAsync("notes.txt");
        var resolver = repository.Resolver;
        await app.WaitForAsync(() => resolver.RunMergeToolCommand.CanExecute(null), "the resolver's merge tool", TimeSpan.FromSeconds(10));
        resolver.Choose(0, ConflictChoice.UseOurs);
        Assert.True(resolver.IsModified);

        // 11.9 and 7.2: the file menu's "Open in merge tool" asks as the resolver's own button does; "Keep editing" starts nothing.
        var keep = repository.Operations.OpenMergeToolAsync("notes.txt");
        var question = await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal(("Discard your changes to notes.txt?", "Keep editing"), (question.Title, question.CancelText));
        question.Cancel();
        await keep;
        Assert.True(resolver.IsModified);
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.Contains("mergetool", StringComparison.Ordinal));

        // Discard: the tool runs and resolves the file.
        var run = repository.Operations.OpenMergeToolAsync("notes.txt");
        (await app.WaitForDialogAsync<ConfirmationViewModel>()).ConfirmCommand.Execute(null);
        await run;
        await app.WaitForIdleAsync();
        Assert.Contains(app.GitCalls(), call => call.CommandText == "git -c mergetool.keepBackup=false mergetool --tool=fake --no-prompt -- notes.txt");
        Assert.Equal(Scenarios.ConflictsFiles.FakeMergeToolResult, Read(repo, "notes.txt"));
        Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
    }

    [AvaloniaFact]
    public async Task A_resolver_opened_on_a_file_whose_merge_tool_the_file_menu_started_waits_for_the_tool_read_only()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge, mergeTool: true);
        var started = repo.SiblingPath("tool-started");
        var release = repo.SiblingPath("tool-release");

        // A tool that waits to be released (at the latest after a minute), then resolves the file.
        await repo.ConfigAsync("mergetool.fake.cmd", "touch ../tool-started; i=0; while [ ! -f ../tool-release ] && [ $i -lt 600 ]; do sleep 0.1; i=$((i+1)); done; printf 'resolved by the tool\\n' > \"$MERGED\"");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        try
        {
            await app.WaitForRepositoryAsync();
            var repository = app.Session.Shell.ActiveTab.Repository!;
            await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1, "the conflict read", TimeSpan.FromSeconds(30));
            var notes = Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted);

            // The file menu's "Open in merge tool" with the resolver closed; then a click on the file.
            var tool = repository.Operations.OpenMergeToolAsync("notes.txt");

            // git mergetool's start takes 32 to 64 s under the default run's load (D185): it is no
            // hang, and has a limit of its own.
            using (HangWatchdogAttribute.WaitingForSlowProgram("git mergetool to start"))
            {
                await app.WaitForAsync(() => File.Exists(started), "the merge tool to start", Scenarios.MergeToolTimeout);
            }

            repository.OpenResolver(notes);
            await app.WaitForResolverAsync("notes.txt");
            var resolver = repository.Resolver;

            // 11.9: read-only, waiting for the tool; nothing can be marked resolved over it.
            await app.WaitForAsync(() => resolver.IsBusy, "the resolver to wait for the tool", TimeSpan.FromSeconds(10));
            Assert.Equal("Waiting for fake to close…", resolver.ResultTitle);
            Assert.True(app.ResultEditor.IsReadOnly);
            Assert.False(resolver.MarkResolvedCommand.CanExecute(null));

            // The tool resolves the file: the resolver closes, the tool's result staged.
            File.WriteAllText(release, string.Empty);
            await tool;
            await app.WaitForResolverClosedAsync(TimeSpan.FromSeconds(30));
            Assert.Equal("resolved by the tool\n", Read(repo, "notes.txt"));
            Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
        }
        finally
        {
            File.WriteAllText(release, string.Empty);
        }
    }

    [AvaloniaFact]
    public async Task After_a_pull_that_merges_and_conflicts_theirs_is_named_after_the_upstream()
    {
        // main tracks origin/main; both changed the same line; the pull merges FETCH_HEAD as the
        // app's own pull does (MERGE_MSG: "Merge branch 'main' of <url>").
        using var data = new TempDirectory("data");
        using var repo = await TempRepo.CreateAsync();
        await repo.CommitFileAsync("notes.txt", "line 1\nline 2\n", "Base");
        var bare = await repo.AddBareRemoteAsync("origin");
        await repo.PushAsync("origin", setUpstream: true, "main");
        var other = await repo.CloneSiblingAsync("other", bare);
        await other.GitAsync("checkout", "--quiet", "-B", "main", "origin/main");
        await other.CommitFileAsync("notes.txt", "line 1\nline 2 upstream\n", "Upstream");
        await other.PushAsync("origin", setUpstream: false, "main");
        await repo.CommitFileAsync("notes.txt", "line 1\nline 2 local\n", "Local");
        await repo.GitAsync("fetch", "--quiet", "origin");
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("merge", "--no-edit", "FETCH_HEAD")).ExitCode);
        Assert.StartsWith("Merge branch 'main' of ", File.ReadAllLines(Path.Combine(repo.Path, ".git", "MERGE_MSG"))[0], StringComparison.Ordinal);

        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitForRepositoryAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 1 && repository.Refs is not null, "the conflict read", TimeSpan.FromSeconds(30));
        repository.OpenResolver(Assert.Single(repository.Status.Unstaged, file => file.Kind == FileChangeKind.Conflicted));
        await app.WaitForResolverAsync("notes.txt");

        Assert.Equal(("Ours: main", "Theirs: origin/main"), (repository.Resolver.OursTitle, repository.Resolver.TheirsTitle));

        // Another branch of that URL: no remote branch of its name at MERGE_HEAD, and not the upstream.
        IConflictActions actions = repository.Operations;
        Assert.Null(actions.MergedRemoteBranch("feature", repository.Status.InProgress.Commit));
    }

    [AvaloniaFact]
    public async Task With_the_stage_panel_as_a_tree_the_resolver_counts_and_goes_through_the_files_in_the_tree_s_order()
    {
        using var data = new TempDirectory("data");
        using var repo = await TempRepo.CreateAsync();
        repo.WriteFile("a.txt", "line\n").WriteFile("src/b.txt", "line\n");
        await repo.CommitAsync("Base");
        await repo.CreateBranchAsync("feature");
        repo.WriteFile("a.txt", "ours\n").WriteFile("src/b.txt", "ours\n");
        await repo.CommitAsync("Ours");
        await repo.CheckoutAsync("feature");
        repo.WriteFile("a.txt", "theirs\n").WriteFile("src/b.txt", "theirs\n");
        await repo.CommitAsync("Theirs");
        await repo.CheckoutAsync("main");
        Assert.Equal(1, (await repo.GitAllowingFailureAsync("merge", "--no-edit", "feature")).ExitCode);

        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitForRepositoryAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Status.Conflicts.Count == 2, "the conflicts read", TimeSpan.FromSeconds(30));
        repository.Changes.ShowTreeCommand.Execute(null);

        // The tree shows src/ > b.txt above a.txt (11.2: the stage list's order).
        Assert.Equal(["src/b.txt", "a.txt"], repository.Changes.Unstaged.Rows.Where(row => row.IsFile).Select(row => row.Key));
        repository.OpenResolver(repository.Status.Unstaged.Single(file => file.Path == "src/b.txt"));
        await app.WaitForResolverAsync("b.txt");
        var resolver = repository.Resolver;
        Assert.Equal("File 1 of 2", resolver.FilePositionText);

        await resolver.NextFileCommand.ExecuteAsync(null);
        Assert.Equal(("a.txt", "File 2 of 2"), (resolver.Path, resolver.FilePositionText));
    }

    [Fact]
    public async Task Without_merge_tool_in_gits_configuration_nothing_runs()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);

        await app.Operations.OpenMergeToolAsync("notes.txt");

        Assert.Equal(0, Ran(app, "Merge tool for notes.txt"));
        Assert.DoesNotContain(app.Calls.Snapshot(), call => call.CommandText.Contains("mergetool", StringComparison.Ordinal));
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));
    }

    [Fact]
    public async Task The_resolver_reads_a_conflict_and_its_sides_and_a_missing_side_is_null()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.MergeKinds);
        using var app = await OperationsHarness.OpenAsync(repo);
        IConflictActions actions = app.Operations;
        var status = app.Operations.Status;

        var newTxt = status.FindConflict("new.txt")!;
        var content = await actions.ReadAsync(newTxt);
        Assert.Equal((7, false, true), (content.MarkerSize, content.OursBinary, content.WorkingTreeExists));
        Assert.Equal(Scenarios.ConflictsFiles.NewOurs, System.Text.Encoding.UTF8.GetString((await actions.ReadSideAsync(newTxt, ConflictSide.Ours))!));
        Assert.Null(await actions.ReadSideAsync(status.FindConflict("other.txt")!, ConflictSide.Theirs));
    }

    [Fact]
    public async Task Staging_a_conflicted_file_that_still_has_markers_asks_first_wherever_it_is_staged()
    {
        using var repo = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        using var app = await OperationsHarness.OpenAsync(repo);
        var notes = app.Operations.Status.Unstaged.Single(file => file.Path == "notes.txt");

        // "Mark as resolved" from the menu: Cancel stages nothing (7.2).
        app.Dialogs.Confirm(yes: false);
        await app.Operations.StageFilesAsync([notes]);
        var question = Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(
            ("notes.txt still has conflict markers.", "Mark it resolved anyway? The markers are committed with it.", string.Empty, "Mark resolved", false, "Cancel"),
            (question.Title, question.Question, question.Detail, question.ConfirmText, question.IsDestructive, question.CancelText));
        Assert.Equal(["UU notes.txt"], await PorcelainAsync(repo));

        // The stage panel's own buttons ask through the repository (IConflictMarkerQuestions).
        app.Dialogs.Confirm(yes: false);
        Assert.False(await app.Repository.ConfirmStagingAsync([notes]));

        // "Stage all": the same question; "Mark resolved" stages the markers.
        app.Dialogs.Confirm();
        await app.Operations.StageAllAsync();
        Assert.Equal(3, app.Dialogs.Confirmations.Count);
        Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
        app.AssertScriptFollowed();

        // Without markers nothing is asked; a file that is not conflicted is never read.
        using var resolved = await Scenarios.ConflictsStoppedAsync(Stop.Merge);
        resolved.WriteFile("notes.txt", Scenarios.ConflictsFiles.NotesTheirs);
        resolved.WriteFile("README.md", "# changed\n");
        using var other = await OperationsHarness.OpenAsync(resolved);
        await other.Operations.StageAllAsync();
        Assert.Empty(other.Dialogs.Shown);
        Assert.Equal(["M  README.md", "M  notes.txt"], await PorcelainAsync(resolved));
    }

    [Fact]
    public void The_texts_of_the_marker_questions_count_their_files()
    {
        Assert.Equal(
            new ConfirmationRequest("2 files still have conflict markers.", "Mark them resolved anyway? The markers are committed with them.", string.Empty, "Mark resolved", IsDestructive: false),
            OperationTexts.ConfirmStagingWithMarkers(["a.txt", "b.txt"]));
        Assert.Equal(
            new ConfirmationRequest("3 files still have conflict markers.", "Continue anyway? The markers are committed with them.", string.Empty, "Continue", IsDestructive: false),
            OperationTexts.ConfirmContinueWithMarkers(3));
    }
}
