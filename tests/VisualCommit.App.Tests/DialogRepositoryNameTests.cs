using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Design 4f's one rule, which the window feeds: while more than one tab has a repository, every
/// dialog that an operation of a repository opens names it under its title ("In &lt;repo&gt;",
/// <c>DialogRepository</c>); with one, none does but the error dialog, which always names it
/// (<c>ErrorRepository</c>). The confirmations of the repository's own view model (the banner's
/// Abort, phase 2's discards) follow the same rule as the operations' dialogs.
/// </summary>
public class DialogRepositoryNameTests
{
    [AvaloniaFact]
    public async Task With_two_repository_tabs_every_dialog_names_its_repository_and_with_one_only_the_error_dialog()
    {
        using var ops = await Scenarios.OperationsAsync();
        using var stopped = await Scenarios.StoppedAsync(Scenarios.InProgressKind.Merge);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [ops.Path, stopped.Path], activeTab: 1);
        await app.WaitUntilReadyAsync();
        var shell = app.Session.Shell;
        await app.WaitForAsync(() => shell.Tabs.All(tab => tab.Repository is { Refs: not null } repository && repository.Graph.IsComplete), "both repositories");
        await app.WaitForIdleAsync();
        var repository = shell.ActiveTab.Repository!;
        var named = $"In {repository.Name}";
        Assert.True(shell.NamesRepositoryInDialogs);

        // The banner's Abort question, which the repository's view model asks.
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal((named, true), RepositoryLine(app));
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForAsync(() => !app.Session.Services.DialogHost.IsOpen, "the question to close");

        // A confirmation and a name dialog of the operations.
        var delete = repository.Operations.DeleteBranchAsync("refs/heads/feature/ff");
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal((named, true), RepositoryLine(app));
        TestPictures.Save(app, "two-tabs-confirmation");
        app.Click(app.FindByAutomationId("CancelButton"));
        await delete;

        var create = repository.Operations.CreateBranchAsync(null);
        await app.WaitForDialogAsync<BranchNameDialogViewModel>();
        Assert.Equal((named, true), RepositoryLine(app));
        app.Click(app.FindByAutomationId("CancelButton"));
        await create;

        // The error dialog names it as always.
        var stash = repository.Operations.StashAsync();
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.Equal(named, ((TextBlock)app.FindByAutomationId(app.TopDialogView!, "ErrorRepository")).Text);
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await stash;

        // One repository tab left: no dialog names it, but the error dialog.
        shell.CloseTabCommand.Execute(shell.Tabs[0]);
        await app.WaitForAsync(() => shell.Tabs.Count == 1, "the first tab to close");
        Assert.False(shell.NamesRepositoryInDialogs);
        app.Click(app.FindByAutomationId("BannerAbortButton"));
        await app.WaitForDialogAsync<ConfirmationViewModel>();
        Assert.Equal((string.Empty, false), RepositoryLine(app));
        app.Click(app.FindByAutomationId("CancelButton"));
        await app.WaitForAsync(() => !app.Session.Services.DialogHost.IsOpen, "the question to close");

        create = repository.Operations.CreateBranchAsync(null);
        await app.WaitForDialogAsync<BranchNameDialogViewModel>();
        Assert.Equal((string.Empty, false), RepositoryLine(app));
        app.Click(app.FindByAutomationId("CancelButton"));
        await create;

        stash = repository.Operations.StashAsync();
        await app.WaitForDialogAsync<ErrorDialogViewModel>();
        Assert.Equal(named, ((TextBlock)app.FindByAutomationId(app.TopDialogView!, "ErrorRepository")).Text);
        app.Click(app.FindByAutomationId("ConfirmButton"));
        await stash;
    }

    /// <summary>The top dialog's "In &lt;repo&gt;" line: its text and whether it is shown.</summary>
    private static (string Text, bool Shown) RepositoryLine(ShellDriver app)
    {
        var line = (TextBlock)app.FindByAutomationId(app.TopDialogView!, "DialogRepository");
        return (line.Text ?? string.Empty, line.IsEffectivelyVisible);
    }
}
