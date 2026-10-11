using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>
/// The assertions every part of phase 3's walk-through repeats (Phase3Checks.Support.cs), tried
/// on the real app once, on states the report describes, so that a check that fails points at the
/// app and not at the assertion. No pictures: these are not checks of the report.
/// </summary>
public sealed class Phase3SupportTests : Phase3Checks
{
    [AvaloniaFact]
    public async Task The_banner_confirmation_error_dialog_and_absence_assertions_hold_on_a_stopped_merge()
    {
        using var repo = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        await AssertNoDialogAsync(app);
        await AssertNoMenuAsync(app, logMark: 0);

        // Check 9b step 2: the banner of a merge stopped in a terminal; two lines at 1100×700.
        AssertBanner(app, "Merging: \"Merge branch 'feature/conflict'\". 1 file has conflicts.", "Abort merge", oneLine: false);

        // Check 9 step 7: a checkout git refuses during the merge.
        var repository = app.Session.Shell.ActiveTab.Repository!;
        var checkout = repository.Operations.CheckoutAsync("refs/heads/feature/alpha");
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(
            "Checkout failed",
            "Git could not check out feature/alpha.",
            "ops",
            "git switch --no-guess feature/alpha",
            ["Copy output", "Close"],
            ["fatal: cannot switch branch while merging", "Consider \"git merge --quit\" or \"git worktree add\"."],
            KeyLine: "fatal: cannot switch branch while merging"));
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await checkout.WaitAsync(NetworkTimeout, TestContext.Current.CancellationToken);
        AssertStatus(app, "Check out feature/alpha failed", OperationStatusTone.Danger);

        // Check 9 step 8: the banner's Abort asks first.
        var mark = app.LogMark();
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await AssertConfirmationAsync(
            app,
            "Abort the merge?",
            "Abort merging \"Merge branch 'feature/conflict'\"? Changes made to resolve conflicts are lost.",
            "A backup is saved first, so this can be undone.",
            "Abort merge");

        // The assertions do fail when the state is not the expected one.
        await Assert.ThrowsAnyAsync<Exception>(() => AssertConfirmationAsync(app, "Abort the merge?", "Another question?", string.Empty, "Abort merge"));
        await Assert.ThrowsAnyAsync<Exception>(() => AssertNoDialogAsync(app));

        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        await AssertNoMenuAsync(app, mark);
        Assert.True(File.Exists(Path.Combine(repo.Path, ".git", "MERGE_HEAD")), "Esc aborted the merge.");

        app.RightClick(app.GraphRowPoint(4));
        app.Menu();
        await Assert.ThrowsAnyAsync<Exception>(() => AssertNoMenuAsync(app, mark));
        app.PressKey(Key.Escape);
    }
}
