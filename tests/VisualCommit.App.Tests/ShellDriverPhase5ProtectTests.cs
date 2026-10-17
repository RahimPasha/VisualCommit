using Avalonia.Headless.XUnit;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.RepositoryOperationsTestSupport;

namespace VisualCommit.App.Tests;

/// <summary>
/// The driver's part P helpers (ShellDriver.Phase5.Protect.cs, WP4) in the headless window on the
/// "protect" scenario: the status bar's text read as laid out, whole or cut, the stash calls since
/// a mark, and the wait for a status read after a change made outside the app, which counts only
/// reads logged after its mark.
/// </summary>
public class ShellDriverPhase5ProtectTests
{
    /// <summary>A branch whose "Checked out {b}" is wider than the status bar's 520.</summary>
    private const string LongBranch = "a-branch-whose-name-is-long-enough-that-the-status-bar-cuts-the-message-of-its-checkout-with-an-ellipsis";

    [AvaloniaFact]
    public async Task The_automatic_stashs_result_is_read_whole_with_its_tooltip_and_its_stash_calls_alone()
    {
        using var repo = await Scenarios.ProtectAsync();
        await repo.GitAsync("branch", LongBranch, "main");
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

        // A text wider than the status bar's 520 is cut with "…" and is not whole; its tooltip is
        // (the cut that AssertNoClippedText cannot tell, PD14).
        await repository.Operations.CheckoutAsync("refs/heads/" + LongBranch);
        await app.WaitForIdleAsync(repository);
        var cut = app.OperationStatusLayout();
        Assert.Equal(("Checked out " + LongBranch, false, "Checked out " + LongBranch), (cut.Text, cut.IsWhole, cut.ToolTip));
        Assert.True(cut.Width <= 520, "The status text is wider than its MaxWidth.");
    }

    [AvaloniaFact]
    public async Task A_status_read_with_the_counts_a_step_names_is_waited_for_after_a_mark_and_then_its_status()
    {
        using var repo = await Scenarios.ProtectAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitUntilReadyAsync();
        var repository = await GraphShownAsync(app);

        // The opening's read was 0 and 0; the untracked notes makes a read of 0 and 1 after the mark.
        var mark = app.LogMark();
        repo.WriteFile("notes", "my own notes\n");
        await app.WaitForStatusReadAsync(0, 1, mark, timeout: TimeSpan.FromSeconds(30));
        Assert.True(repository.Status.IsUntracked("notes"));

        // Only reads after the mark count: the opening's 0 and 0 is logged before it, and no read
        // after it can have those counts while notes is there. The timeout says what it waited for.
        var timedOut = await Assert.ThrowsAsync<TimeoutException>(() => app.WaitForStatusReadAsync(0, 0, app.LogMark(), timeout: TimeSpan.FromMilliseconds(500)));
        Assert.Contains("0 staged and 0 unstaged", timedOut.Message, StringComparison.Ordinal);

        // P24 step 7: notes deleted and notes/today.txt written, so the counts stay 0 and 1, as
        // the read before the mark had them: the status the step names decides.
        mark = app.LogMark();
        File.Delete(Path.Combine(repo.Path, "notes"));
        repo.WriteFile("notes/today.txt", "today\n");
        await app.WaitForStatusReadAsync(0, 1, mark, status => status.IsUntracked("notes/today.txt") && !status.IsUntracked("notes"), TimeSpan.FromSeconds(30));
        Assert.True(repository.Status.IsUntracked("notes/today.txt"));
        Assert.False(repository.Status.IsUntracked("notes"));
    }
}
