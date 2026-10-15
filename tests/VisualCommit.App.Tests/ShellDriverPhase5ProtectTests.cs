using Avalonia.Headless.XUnit;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The driver's part P helpers (ShellDriver.Phase5.Protect.cs, WP4) in the headless window on the
/// "protect" scenario: the status bar's text read as laid out, the stash calls since a mark, and
/// the wait for a status read after a change made outside the app.
/// </summary>
public class ShellDriverPhase5ProtectTests
{
    [AvaloniaFact]
    public async Task The_automatic_stashs_result_is_read_whole_with_its_tooltip_and_its_stash_calls_alone()
    {
        using var repo = await Scenarios.ProtectAsync();
        repo.WriteFile("src/app.txt", "app\nunstaged\n").WriteFile("scratch.txt", "scratch\n");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);
        var calls = app.GitCalls().Count;
        Assert.Empty(app.StashCallsSince(calls));

        await repository.Operations.CheckoutAsync("f39ea2d9addec45f926151cd2267cb57ea0b16aa");
        await app.WaitForIdleAsync(repository);

        // Design 9.2 step 6 and the report's PD14: the text fits the status bar whole, its tooltip the same.
        const string Result = "Your changes are in the stash \"Auto stash before checking out f39ea2d\".";
        var status = app.OperationStatusLayout();
        Assert.Equal((Result, true, Result), (status.Text, status.IsWhole, status.ToolTip));
        Assert.True(status.Width <= 520, "The status text is wider than its MaxWidth.");

        // The refs read's "git stash list" is no stash call; the push is the only one.
        var stash = Assert.Single(app.StashCallsSince(calls));
        Assert.Equal("git stash push --include-untracked --message \"Auto stash before checking out f39ea2d\"", stash.CommandText);
        Assert.Contains(app.GitCalls().Skip(calls), call => call.CommandText.StartsWith("git stash list", StringComparison.Ordinal));
        Assert.Empty(app.StashCallsSince(app.GitCalls().Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => app.StashCallsSince(-1));
    }

    [AvaloniaFact]
    public async Task A_status_read_with_the_counts_a_step_names_is_waited_for_after_a_mark_and_then_its_status()
    {
        using var repo = await Scenarios.ProtectAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);

        // The opening's read was 0 and 0; a read after the mark is waited for, not that one.
        var mark = app.LogMark();
        repo.WriteFile("src/app.txt", "app\nlocal\n");
        await app.WaitForStatusReadAsync(0, 1, mark, timeout: TimeSpan.FromSeconds(30));
        Assert.True(repository.Status.HasChanges);

        // Counts that do not change (P24 step 7): the status names the new path.
        mark = app.LogMark();
        repo.WriteFile("notes/today.txt", "today\n");
        File.Delete(Path.Combine(repo.Path, "src", "app.txt"));
        await app.WaitForStatusReadAsync(0, 2, mark, status => status.IsUntracked("notes/today.txt"), TimeSpan.FromSeconds(30));
        Assert.True(repository.Status.IsUntracked("notes/today.txt"));

        // Nothing that matches: the wait times out and says what it waited for.
        var timedOut = await Assert.ThrowsAsync<TimeoutException>(() => app.WaitForStatusReadAsync(3, 3, app.LogMark(), timeout: TimeSpan.FromMilliseconds(200)));
        Assert.Contains("3 staged and 3 unstaged", timedOut.Message, StringComparison.Ordinal);
    }
}
