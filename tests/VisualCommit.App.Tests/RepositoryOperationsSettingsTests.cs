using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The settings screen's writes of "This repository" (docs/phase-6-design.md 8.3, 8.5, 7.8 step 5,
/// 8.13) against real repositories: one operation of the queue with no undo entry, dropped while
/// it waits when the screen's token is cancelled, stopping at the first failure, whose error
/// dialog (a held <c>.git/config.lock</c> among them) has the operation's texts.
/// </summary>
public class RepositoryOperationsSettingsTests
{
    [Fact]
    public async Task The_writes_run_as_one_operation_of_the_queue_in_order_without_an_undo_entry()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var since = app.Calls.Snapshot().Count;

        var result = await app.Operations.SaveRepositorySettingsAsync(
            [new GitConfigWrite("user.name", "Bo Local"), new GitConfigWrite("user.email", "bo@example.org"), new GitConfigWrite("pull.rebase", null)],
            CancellationToken.None);

        Assert.Equal(OperationOutcome.Succeeded, result.Outcome);
        Assert.Equal("Saved the repository's settings", app.StatusText);
        Assert.Equal(
            ["git config --local --replace-all user.name \"Bo Local\"", "git config --local --replace-all user.email bo@example.org", "git config --local --unset-all pull.rebase"],
            app.Calls.Snapshot().Skip(since).Select(call => call.CommandText).Where(text => text.StartsWith("git config --local --", StringComparison.Ordinal)));
        Assert.Equal("Bo Local", await ConfigAsync(repo, "user.name"));
        Assert.Equal("bo@example.org", await ConfigAsync(repo, "user.email"));
        Assert.Empty(app.Repository.UndoHistory.Entries);
        Assert.Empty(app.Dialogs.Shown);
        Assert.Equal("Save repository settings", app.Services.Operations.Snapshot()[^1].Title);
    }

    [Fact]
    public async Task Cancelled_while_it_waits_behind_another_operation_none_of_its_writes_run()
    {
        // 7.8 step 5: the screen's Cancel drops the operation while it waits (4.5's RunOptions.Cancellation).
        using var repo = await Scenarios.GitSettingsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holding = app.Operations.RunAsync(
            new OperationText("Hold", "Holding…", "Hold failed", "The hold failed."),
            OperationKind.Local,
            RefreshScope.None,
            async _ =>
            {
                started.SetResult();
                await hold.Task;
                return OperationReport.Done("Held");
            });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        using var cancel = new CancellationTokenSource();
        var since = app.Calls.Snapshot().Count;

        var saving = app.Operations.SaveRepositorySettingsAsync([new GitConfigWrite("user.name", "Bo Local")], cancel.Token);
        Assert.Equal("Hold", app.Repository.Queue.Current?.Title);
        Assert.Equal(1, app.Repository.Queue.Waiting);
        cancel.Cancel();
        hold.SetResult();
        await holding;
        var result = await saving;

        Assert.Equal(OperationOutcome.Cancelled, result.Outcome);
        Assert.DoesNotContain(app.Calls.Snapshot().Skip(since), call => call.CommandText.StartsWith("git config --local", StringComparison.Ordinal));
        Assert.Null(await ConfigAsync(repo, "user.name"));
        Assert.Empty(app.Dialogs.Shown);

        // A token cancelled before the call queues nothing at all.
        var none = await app.Operations.SaveRepositorySettingsAsync([new GitConfigWrite("user.name", "Bo Local")], cancel.Token);
        Assert.Equal(OperationOutcome.Cancelled, none.Outcome);
        Assert.Null(await ConfigAsync(repo, "user.name"));
    }

    [Fact]
    public async Task The_first_failure_stops_the_writes_and_opens_the_error_dialog_with_the_operations_texts()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        app.Dialogs.CloseError();

        // An invalid key (exit 1 or 2) is a bug's write (8.5): the dialog with git's line.
        var result = await app.Operations.SaveRepositorySettingsAsync(
            [new GitConfigWrite("user.name", "Bo Local"), new GitConfigWrite("nodot", "x"), new GitConfigWrite("user.email", "bo@example.org")],
            CancellationToken.None);

        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        Assert.Equal("Save repository settings failed", app.StatusText);
        Assert.Equal("Bo Local", await ConfigAsync(repo, "user.name"));
        Assert.Null(await ConfigAsync(repo, "user.email"));
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Saving the settings failed", "Git could not save the repository's settings."), (error.Title, error.Summary));
        Assert.Equal("git config --local --replace-all nodot x", error.Command);
        Assert.False(string.IsNullOrEmpty(error.Output));
        Assert.Empty(app.Repository.UndoHistory.Entries);
        app.AssertScriptFollowed();
    }

    [Fact]
    public async Task A_held_config_lock_fails_with_gits_line_and_writes_nothing()
    {
        // 8.5: "error: could not lock config file <file>: File exists", exit 255; no offer to remove it (D126).
        using var repo = await Scenarios.GitSettingsAsync();
        using var app = await OperationsHarness.OpenAsync(repo);
        var lockFile = Path.Combine(repo.Path, ".git", "config.lock");
        await File.WriteAllTextAsync(lockFile, string.Empty, TestContext.Current.CancellationToken);
        app.Dialogs.CloseError();

        var result = await app.Operations.SaveRepositorySettingsAsync([new GitConfigWrite("user.name", "Bo Local")], CancellationToken.None);

        Assert.Equal(OperationOutcome.Failed, result.Outcome);
        var error = Assert.Single(app.Dialogs.Errors);
        Assert.Equal(("Saving the settings failed", "Git could not save the repository's settings.", false), (error.Title, error.Summary, error.HasExtraAction));
        Assert.Contains("could not lock config file", error.Output, StringComparison.Ordinal);
        Assert.Equal("git config --local --replace-all user.name \"Bo Local\"", error.Command);
        Assert.True(File.Exists(lockFile));
        File.Delete(lockFile);
        Assert.Null(await ConfigAsync(repo, "user.name"));
        app.AssertScriptFollowed();
    }

    /// <summary>The repository's own value of <paramref name="key"/> (<c>git config --local --get</c>), or null when unset.</summary>
    private static async Task<string?> ConfigAsync(TempRepo repo, string key)
    {
        var result = await repo.GitAllowingFailureAsync("config", "--local", "--get", key);
        return result.ExitCode == 0 ? result.StandardOutput.TrimEnd('\n', '\r') : null;
    }
}
