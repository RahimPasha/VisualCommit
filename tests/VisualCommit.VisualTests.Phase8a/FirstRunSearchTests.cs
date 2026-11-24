using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase8a.Phase8aExpectations;

namespace VisualCommit.VisualTests.Phase8a;

/// <summary>
/// The searches for git in the shell, no picture (docs/phase-8a-design.md 1.2, 5.3; D423; the
/// review before the gate): a git saved in Settings while the start's search runs is searched for
/// once that search has ended, and a probe of a file chosen on the first-run screen ends with the
/// window. The probe is the harness's fake, held until the test lets it answer.
/// </summary>
public sealed class FirstRunSearchTests : Phase8aChecks
{
    [AvaloniaFact]
    public async Task A_git_saved_in_Settings_while_the_starts_search_runs_is_searched_for_once_that_search_has_ended()
    {
        using var data = new TempDirectory("phase8a search");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing)
            .AnswerFor(F.Chosen, FakeGitAnswer.RealAt(F.Chosen, GitSource.Setting));
        var start = probe.HoldNext();
        using var app = ShellDriver.Start(data.Path, gitProbe: probe);
        await start.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        app.Settle();
        Assert.Null(app.Session.Shell.Git);

        // The settings screen while git is looked for: Browse gives the chosen git, then Save.
        app.Session.Shell.OpenSettingsCommand.Execute(SettingsSection.Git);
        var dialog = await app.WaitForDialogAsync<SettingsDialogViewModel>();
        app.Folders.AnswerFile(F.Chosen);
        await dialog.GitProgram.BrowseCommand.ExecuteAsync(null);
        Assert.Equal(F.Chosen, dialog.GitProgram.Choice);
        var saving = dialog.ConfirmCommand.ExecuteAsync(null);
        app.Settle();

        // The start's search ends with what it was told, no git; the chosen one is then looked for.
        // (Save's read back of git's settings waits for the start's search, through the runner.)
        start.Release();
        await saving.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
        await app.WaitForAsync(() => dialog.IsClosed && app.SettingsDialog is null, "the settings screen to close after Save");
        Assert.Equal(F.Chosen, app.Session.Settings.Current.GitExecutable);
        await app.WaitForAsync(
            () => app.Session.Shell.Git is { ExecutablePath: var path } && path == F.Chosen && !app.Session.Shell.CheckGitAgainCommand.IsRunning,
            $"the chosen git in use (it is {app.Session.Shell.Git?.ExecutablePath ?? "none"})");
        Assert.Equal([null, F.Chosen, F.Chosen], probe.Lookups.Select(lookup => lookup.ChosenPath));
        Assert.Equal(GitAvailability.Available, app.Session.Shell.Git!.Availability);
    }

    [AvaloniaFact]
    public async Task Closing_the_window_while_Choose_probes_a_file_ends_the_probe()
    {
        using var data = new TempDirectory("phase8a search");
        var probe = new FakeGitSetupProbe(FakeGitSetupProbe.Windows(), FakeGitAnswer.Missing)
            .AnswerFor(F.Chosen, FakeGitAnswer.RealAt(F.Chosen, GitSource.Setting));
        var app = await StartEmptyAsync(data, gitProbe: probe);
        try
        {
            await app.WaitForGitSetupAsync(choose: F.ChooseGitExe);
            app.Folders.AnswerFile(F.Chosen);
            var hold = probe.HoldNext();
            app.Click(app.GitSetupPart(F.ChooseId)!);
            await hold.Started.WaitAsync(ShellDriver.DefaultWaitTimeout, TestContext.Current.CancellationToken);
            Assert.False(hold.Token.IsCancellationRequested);
            Assert.True(hold.Token.CanBeCanceled, "The probe of the chosen file was given a token nothing can cancel.");

            app.Dispose();

            Assert.True(hold.Token.IsCancellationRequested, "The probe of the chosen file outlives the window.");
            Assert.Null(app.Session.Settings.Current.GitExecutable);
        }
        finally
        {
            app.Dispose();
        }
    }
}
