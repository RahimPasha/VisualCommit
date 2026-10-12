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

        Assert.True(await actions.MarkResolvedAsync("notes.txt", result));

        Assert.Equal(("Resolved notes.txt", OperationStatusTone.Secondary), (app.StatusText, app.StatusTone));
        AssertRanOnce(app, "Resolve notes.txt");
        Assert.Equal(result, File.ReadAllBytes(Path.Combine(repo.Path, "notes.txt")));
        Assert.Equal(["M  notes.txt"], await PorcelainAsync(repo));
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal("Merging: \"Merge branch 'feature/theirs'\". Conflicts resolved: commit to finish the merge.", app.Repository.Banner.Text);
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
