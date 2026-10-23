using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Mod+Enter (docs/phase-6-design.md 6.10, 6.12, D199, D215): in the commit message's two boxes it
/// commits and types no line break; elsewhere the router leaves it; with nothing to commit it is
/// consumed and commits nothing; in a dialog's box of several lines it confirms; and the palette's
/// commit entry commits from anywhere it is listed. Real commits on "ops".
/// </summary>
public class CommitKeyTests
{
    [AvaloniaFact]
    public async Task Mod_Enter_commits_from_the_summary_and_the_description_and_only_there()
    {
        using var repo = await Scenarios.OperationsAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "src", "core.txt"), "core\nmain work\nkeys\n");
        await repo.GitAsync("add", "src/core.txt");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        app.Click(app.WorkingRowPoint());
        var changes = repository.Changes;
        var summary = (TextBox)app.FindByAutomationId("CommitSummary");
        var description = (TextBox)app.FindByAutomationId("CommitDescription");

        // Outside the boxes the router leaves the key (6.10).
        Assert.Equal("GraphRows", app.FocusedAutomationId());
        Assert.False(app.PressKeyHandled(Key.Enter, RawInputModifiers.Control));

        // In the summary with no summary: the commit cannot run; the key is consumed and nothing is committed.
        summary.Focus();
        app.Settle();
        Assert.False(changes.CanCommit);
        Assert.True(app.PressKeyHandled(Key.Enter, RawInputModifiers.Control));
        Assert.Equal(string.Empty, changes.Summary);
        Assert.DoesNotContain(app.GitCalls(), call => call.CommandText.StartsWith("git commit", StringComparison.Ordinal));

        // In the description: it commits the summary and types no line break, read right after
        // the key, before the commit empties the boxes.
        app.Type("Keys only");
        description.Focus();
        app.Settle();
        Assert.True(app.PressKeyHandled(Key.Enter, RawInputModifiers.Control));
        Assert.Equal((string.Empty, "Keys only"), (description.Text ?? string.Empty, summary.Text));
        await app.WaitForAsync(() => app.GitCalls().Any(call => call.CommandText.StartsWith("git commit", StringComparison.Ordinal)), "the commit");
        await app.WaitForIdleAsync();
        await app.WaitForAsync(() => !repository.HasWorkingChanges, "the status after the commit");
        Assert.Equal("Keys only\n\n", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.ReplaceLineEndings("\n"));

        // In the summary: commits too, and its text stays one line.
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "README.md"), "# Operations\n\nMore.\n");
        await repo.GitAsync("add", "README.md");
        await repository.RefreshStatusAsync();
        Assert.Single(repository.Status.Staged);
        app.Click(app.WorkingRowPoint());
        summary.Focus();
        app.Type("Second");
        Assert.True(app.PressKeyHandled(Key.Enter, RawInputModifiers.Control));
        Assert.Equal(("Second", string.Empty), (summary.Text, description.Text ?? string.Empty));
        await app.WaitForAsync(() => repository.Status.Staged.Count == 0 && !repository.HasWorkingChanges, "the second commit");
        Assert.Equal("Second\n\n", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.ReplaceLineEndings("\n"));
    }

    [AvaloniaFact]
    public async Task The_palettes_commit_entry_commits_with_the_focus_in_the_graph_and_Mod_Enter_confirms_a_dialogs_description()
    {
        using var repo = await Scenarios.OperationsAsync();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "src", "core.txt"), "core\nmain work\nkeys\n");
        await repo.GitAsync("add", "src/core.txt");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        app.Click(app.WorkingRowPoint());
        repository.Changes.Summary = "From the palette";
        app.Click(app.GraphRowPoint(1));
        Assert.Equal("GraphRows", app.FocusedAutomationId());

        // 5.4's row 6: labelled as the button, its key Ctrl+Enter, run from the graph.
        app.PressPalette();
        app.TypeInPalette("commit 1 file");
        Assert.Equal(("Commit 1 file", "Ctrl+Enter", true), (app.Palette.Highlighted!.Label, app.Palette.Highlighted.KeyText, app.Palette.Highlighted.IsEnabled));
        app.PressKey(Key.Enter);
        await app.WaitForAsync(() => !repository.HasWorkingChanges, "the commit");
        Assert.Equal("From the palette", (await repo.GitAsync("log", "-1", "--format=%B")).StandardOutput.Trim());

        // A dialog's box of several lines: Mod+Enter confirms; plain Enter types a line break.
        var dialog = new MessageDialogViewModel("Reword commit", "Main work\n\nFirst line", "Reword");
        var answer = app.Session.Services.DialogHost.ShowAsync(dialog);
        await app.WaitForAsync(() => app.TopDialogView is not null, "the message dialog");
        var box = (TextBox)app.FindByAutomationId(app.TopDialogView!, "MessageDescription");
        box.Focus();
        box.CaretIndex = box.Text?.Length ?? 0;
        app.Settle();
        app.PressKey(Key.Enter);
        Assert.False(answer.IsCompleted);
        Assert.Equal("First line\n", dialog.Description.ReplaceLineEndings("\n"));
        Assert.True(app.PressKeyHandled(Key.Enter, RawInputModifiers.Control));
        Assert.True(answer.IsCompleted);
        Assert.Equal("Main work\n\nFirst line", (await answer)?.ReplaceLineEndings("\n").TrimEnd());
    }
}
