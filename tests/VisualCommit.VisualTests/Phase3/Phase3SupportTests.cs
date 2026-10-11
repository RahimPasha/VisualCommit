using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.Core.Git;
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

    /// <summary>
    /// The half of the error dialog's assertion that does not use the app's cleaner
    /// (<see cref="Phase3Checks.AssertGitOutputShown"/>): what a regression of the cleaner would
    /// show on a git other than 2.36, where the report's lines are not compared, fails it.
    /// </summary>
    [Fact]
    public void The_output_check_of_the_error_dialog_holds_without_the_apps_cleaner()
    {
        // A push rejected, with git's progress before the refusal, as the call log records it.
        var push = new GitCallRecord(
            DateTimeOffset.UnixEpoch,
            TimeSpan.FromSeconds(1),
            "git push --progress origin refs/heads/main:refs/heads/main",
            null,
            GitCallOutcome.Completed,
            1,
            string.Empty,
            "Enumerating objects: 5, done.\nCounting objects:  50% (2/4)\rCounting objects: 100% (4/4), done.\nTo http://127.0.0.1:47391/origin.git\n ! [rejected]        main -> main (fetch first)\nerror: failed to push some refs to 'http://127.0.0.1:47391/origin.git'\nhint: Updates were rejected because the remote contains work that you do   \n");
        const string Key = " ! [rejected]        main -> main (fetch first)";
        const string Shown =
            "Enumerating objects: 5, done.\nTo http://127.0.0.1:47391/origin.git\n ! [rejected]        main -> main (fetch first)\nerror: failed to push some refs to 'http://127.0.0.1:47391/origin.git'\nhint: Updates were rejected because the remote contains work that you do";
        AssertGitOutputShown(push, Key, Shown);

        // The key line dropped, an error line dropped, a progress line kept, a line made up.
        Assert.ThrowsAny<Exception>(() => AssertGitOutputShown(push, Key, Shown.Replace(Key + "\n", string.Empty, StringComparison.Ordinal)));
        Assert.ThrowsAny<Exception>(() => AssertGitOutputShown(push, Key, Shown.Replace("\nerror: failed to push some refs to 'http://127.0.0.1:47391/origin.git'", string.Empty, StringComparison.Ordinal)));
        Assert.ThrowsAny<Exception>(() => AssertGitOutputShown(push, Key, "Counting objects:  50% (2/4)\n" + Shown));
        Assert.ThrowsAny<Exception>(() => AssertGitOutputShown(push, Key, Shown + "\nfatal: something git never said"));

        // A key line git never printed is not proven by the dialog alone.
        Assert.ThrowsAny<Exception>(() => AssertGitOutputShown(push, "fatal: Authentication failed", Shown + "\nfatal: Authentication failed"));

        // An empty revert writes its failure to standard output, with nothing on standard error.
        var revert = push with
        {
            CommandText = "git revert --no-edit 88d6b91",
            StandardOutput = "On branch main\nnothing to commit, working tree clean\n",
            StandardError = string.Empty,
        };
        AssertGitOutputShown(revert, "nothing to commit, working tree clean", "On branch main\nnothing to commit, working tree clean");
    }
}
