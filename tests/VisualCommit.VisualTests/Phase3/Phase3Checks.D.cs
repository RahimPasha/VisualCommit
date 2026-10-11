// Part D of phase 3's scripted walk-through (docs/test-reports/phase-3.md, "Part D: remote
// operations, credentials and prompts, remotes, the light theme and closing the window"): checks
// 17, 17b, 18, 18b, 19, 20, 21, 21b, 22, 27, 28, 29, 30 and 31. This class is part D's alone; its
// expected texts go into Phase3Expectations.D.cs (Phase3Expectations.PartD), and further files of
// its own hold more of the class: Phase3Checks.D.Helpers.cs (what its checks share),
// .D.Sync.cs (18 to 20), .D.Prompts.cs (21, 21b), .D.Remotes.cs (22, 28 to 30) and .D.Light.cs
// (27). This file has the fetch with its progress and Cancel (17, 17b) and closing the window
// while a fetch runs (31). Everything the checks share is in Phase3Checks.Support.cs; read its
// header for the conventions. The server scenarios are AuthAtServerAsync and SyncAtServerAsync
// (port 47391, or ServerScenario.Port and WithPort when it was taken). Only this class starts
// servers, and xunit runs its tests one at a time, so they never compete for the port. "Wait
// until idle" is ShellDriver.WaitForIdleAsync; "wait until the status bar shows a percentage" is
// ShellDriver.WaitForProgressPercentAsync.
using System.Security.Cryptography;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.Core.Git;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

/// <summary>The checks of part D: remote operations, credentials and prompts, remotes, the light theme and closing the window.</summary>
public sealed partial class Phase3ChecksD : Phase3Checks
{
    /// <summary>
    /// Check 17: the auth scenario at the server without a sign-in. A fetch of origin from the
    /// remote's menu, held at a percentage and cancelled; then the toolbar's Fetch, held, then let go.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_17_fetch_progress_and_cancel()
    {
        await using var scenario = await AuthAtServerAsync(requireAuth: false);
        var repo = scenario.Repo;
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: as the repository opens.
        Shot(app, "17a-before-fetch");
        Assert.Equal(["auth"], app.Session.Shell.Tabs.Select(tab => tab.Title));
        Assert.Equal(-1, Graph(app).SelectedIndex);
        AssertGraph(app, PartD.AuthGraph, lanes: 1);
        AssertAuthLeftPanel(app, behind: 0);
        AssertBranch(app, "main");
        AssertIdleStatus(app, "Ready");
        foreach (var enabled in PartD.EnabledOnAuth)
        {
            Assert.True(ToolbarButton(app, enabled).IsEffectivelyEnabled, $"{enabled} is disabled.");
        }

        Assert.False(ToolbarButton(app, "StashButton").IsEffectivelyEnabled);
        AssertTip(ToolbarButton(app, "StashButton"), "There are no changes to stash");
        Assert.False(ToolbarButton(app, "PopButton").IsEffectivelyEnabled);
        AssertTip(ToolbarButton(app, "PopButton"), "There are no stashes");
        Assert.Empty(scenario.Server.Requests);
        AssertNoClippedText(app);
        var refsBefore = await RefsAsync(repo);

        // Step 2: the server holds the next pack; "Fetch origin" from the remote's menu.
        _ = scenario.Server.HoldNextFetchPack();
        var (menu, pointer) = await RightClickRowAsync(app, "Remotes", "origin");
        AssertMenu(menu, PartD.OriginMenu);
        AssertHighlighted(menu, "Fetch origin");
        AssertMenuShape(app, menu);
        AssertMenuAtPointer(menu, pointer);
        AssertNothingSelected(app);
        app.ClickMenuItem("Fetch origin");
        await app.WaitForProgressPercentAsync(NetworkTimeout);
        Shot(app, "17b-fetch-progress");
        AssertHeldProgress(app, PartD.FetchingFromOrigin);
        AssertGraph(app, PartD.AuthGraph, lanes: 1);
        AssertAuthLeftPanel(app, behind: 0);
        Assert.Equal(refsBefore, await RefsAsync(repo));
        Assert.Empty(LockFiles(repo));

        // Step 3: Cancel stops git while it receives the pack.
        var mark = app.LogMark();
        ClickId(app, "OperationCancelButton");
        await app.WaitForIdleAsync(timeout: NetworkTimeout);
        scenario.Server.Release();
        Shot(app, "17c-fetch-cancelled");
        AssertIdleStatus(app, PartD.FetchOriginCancelled);
        await AssertNoDialogAsync(app);
        AssertGraph(app, PartD.AuthGraph, lanes: 1);
        AssertAuthLeftPanel(app, behind: 0);
        Assert.Equal(refsBefore, await RefsAsync(repo));
        Assert.Equal(await CommitBySubjectAsync(repo, "Initial commit"), await RevParseAsync(repo, "refs/remotes/origin/main"));
        Assert.Empty(LockFiles(repo));
        Assert.Contains(app.LogLinesSince(mark), line => line.Contains("] auth: Fetch origin: Cancelled after ", StringComparison.Ordinal));

        // Step 4: the toolbar's Fetch, held, then let go.
        _ = scenario.Server.HoldNextFetchPack();
        var calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "FetchButton"));
        await app.WaitForProgressPercentAsync(NetworkTimeout);
        Shot(app, "17d-fetch-all-progress");
        AssertHeldProgress(app, PartD.FetchingFromAll);
        scenario.Server.Release();
        await app.WaitForIdleAsync(timeout: NetworkTimeout);
        Shot(app, "17e-fetched");
        AssertIdleStatus(app, PartD.FetchedFromAll);
        Assert.Equal([PartD.FetchOriginCommand], app.GitCalls().Skip(calls).Select(call => call.CommandText).Where(text => text.StartsWith("git fetch", StringComparison.Ordinal)));
        AssertGraph(app, PartD.AuthFetchedGraph, lanes: 1);
        AssertAuthLeftPanel(app, behind: 1);
        Assert.Equal(PartD.AuthBehindOne, await StatusLineAsync(repo));
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded("auth", "Fetch", OperationOutcome.Succeeded, PartD.FetchedFromAll));
        AssertNoClippedText(app);
    }

    /// <summary>
    /// Check 17b: a lock file that a stopped git would leave, found after the cancel and offered
    /// for removal; the fetch after it succeeds.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_17b_lock_files_left_by_a_cancel()
    {
        await using var scenario = await AuthAtServerAsync(requireAuth: false);
        var repo = scenario.Repo;
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: as check 17, steps 1 and 2.
        _ = scenario.Server.HoldNextFetchPack();
        await RightClickRowAsync(app, "Remotes", "origin");
        app.ClickMenuItem("Fetch origin");
        await app.WaitForProgressPercentAsync(NetworkTimeout);

        // Step 2: the lock file a git stopped while it updates the ref would leave, then Cancel.
        var lockFile = Path.Combine(repo.Path, ".git", "refs", "remotes", "origin", "main.lock");
        File.WriteAllBytes(lockFile, []);
        ClickId(app, "OperationCancelButton");
        scenario.Server.Release();
        await app.WaitForDialogAsync<ErrorDialogViewModel>(NetworkTimeout);
        Shot(app, "17b1-lock-files");
        await AssertErrorDialogAsync(app, Expected(PartD.LockFilesDialog, scenario.Port));
        AssertCard(app, "ErrorDialogCard", 400);
        AssertIdleStatus(app, PartD.FetchOriginCancelled);
        AssertNoClippedText(app);

        // Step 3: "Remove lock files" deletes it and closes the dialog; nothing else under .git changes.
        var before = DotGitHashes(repo);
        ClickId(app, "ErrorExtraButton");
        await AssertNoDialogAsync(app);
        Assert.False(File.Exists(lockFile), "The lock file is still there.");
        var after = DotGitHashes(repo);
        before.Remove(PartD.LockFile);
        Assert.Equal(before.OrderBy(entry => entry.Key), after.OrderBy(entry => entry.Key));

        // Step 4: the fetch of origin succeeds.
        await RightClickRowAsync(app, "Remotes", "origin");
        app.ClickMenuItem("Fetch origin");
        await app.WaitForIdleAsync(timeout: NetworkTimeout);
        AssertIdleStatus(app, PartD.FetchedFromOrigin);
        AssertGraph(app, PartD.AuthFetchedGraph, lanes: 1);
        AssertAuthLeftPanel(app, behind: 1);
        Assert.Equal(PartD.AuthBehindOne, await StatusLineAsync(repo));
    }

    /// <summary>
    /// Check 31, steps 1 to 4: the window closed while a held fetch runs and another waits; the
    /// finishing dialog, "Keep open", and the window closing by itself once the fetch ends.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_31_finishing_dialog_keep_open_and_close_by_itself()
    {
        await using var scenario = await AuthAtServerAsync(requireAuth: false);
        var repo = scenario.Repo;
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: a held fetch of origin, and the toolbar's Fetch waiting behind it.
        await HoldFetchAndQueueAnotherAsync(app, scenario);
        AssertHeldProgress(app, PartD.FetchingFromOrigin, waiting: PartD.OneWaiting);

        // Step 2: close the window; the app keeps it open and asks.
        var mark = app.LogMark();
        Assert.False(app.RequestClose(), "The window closed while a fetch ran.");
        await app.WaitForDialogAsync<FinishingDialogViewModel>();
        Shot(app, "31a-finishing");
        AssertFinishingDialog(app);

        // "1 waiting" has gone: the waiting Fetch was dropped without running (design 1.8).
        Assert.Equal(0, app.Session.Shell.ActiveTab.OperationStatus.Waiting);
        Assert.DoesNotContain(app.LogLinesSince(mark), line => line.Contains("] auth: Fetch: ", StringComparison.Ordinal));
        AssertHeldProgress(app, PartD.FetchingFromOrigin);
        AssertNoClippedText(app);

        // Step 3: "Keep open".
        ClickId(app, "CancelButton");
        Shot(app, "31b-kept-open");
        Assert.Null(app.Session.Services.DialogHost.Current);
        Assert.Null(app.TopDialogView);
        Assert.False(app.IsWindowClosed);
        AssertHeldProgress(app, PartD.FetchingFromOrigin);

        // Step 4: close again; once the fetch is let go, the window closes by itself.
        Assert.False(app.RequestClose(), "The window closed while a fetch ran.");
        await app.WaitForDialogAsync<FinishingDialogViewModel>();
        scenario.Server.Release();
        await app.WaitForAsync(() => app.IsWindowClosed, "the window to close by itself");
        Assert.Contains(app.LogLines(), line => AppLogFile.OperationEnded("auth", "Fetch origin", OperationOutcome.Succeeded, PartD.FetchedFromOrigin).IsMatch(line));
        Assert.Single(scenario.Server.Requests, request => request.Command == "fetch");
        Assert.Equal(await CommitBySubjectAsync(repo, "Remote work"), await RevParseAsync(repo, "refs/remotes/origin/main"));
        Assert.Equal(PartD.AuthBehindOne, await StatusLineAsync(repo));
    }

    /// <summary>Check 31, step 5: a new app on a new copy; steps 1 and 2 again, then "Quit now".</summary>
    [AvaloniaFact]
    public async Task Check_31_quit_now()
    {
        await using var scenario = await AuthAtServerAsync(requireAuth: false);
        var repo = scenario.Repo;
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        await HoldFetchAndQueueAnotherAsync(app, scenario);
        Assert.False(app.RequestClose(), "The window closed while a fetch ran.");
        await app.WaitForDialogAsync<FinishingDialogViewModel>();
        AssertFinishingDialog(app);

        ClickId(app, "ConfirmButton");
        await app.WaitForAsync(() => app.IsWindowClosed, "the window to close after \"Quit now\"", TimeSpan.FromSeconds(10));

        // The desktop app's lifetime ends the session when its main window has closed
        // (App.axaml.cs: Exit disposes it), which stops git; the driver's Dispose is that end here.
        app.Dispose();
        await app.WaitForAsync(
            () => app.GitCalls().Any(call => call.CommandText == PartD.FetchOriginCommand),
            "the stopped fetch's call record");
        var fetch = app.LastCall(PartD.FetchOriginCommand);
        Assert.Equal(GitCallOutcome.Cancelled, fetch.Outcome);
        scenario.Server.Release();
        Assert.Equal(await CommitBySubjectAsync(repo, "Initial commit"), await RevParseAsync(repo, "refs/remotes/origin/main"));
        Assert.Empty(LockFiles(repo));
    }

    /// <summary>Check 31 step 1: the server holds the next pack, "Fetch origin" from the remote's menu runs to a percentage, and the toolbar's Fetch waits behind it.</summary>
    private static async Task HoldFetchAndQueueAnotherAsync(ShellDriver app, ServerScenario scenario)
    {
        _ = scenario.Server.HoldNextFetchPack();
        await RightClickRowAsync(app, "Remotes", "origin");
        app.ClickMenuItem("Fetch origin");
        await app.WaitForProgressPercentAsync(NetworkTimeout);
        app.Click(ToolbarButton(app, "FetchButton"));
        await app.WaitForAsync(() => app.Session.Shell.ActiveTab.OperationStatus.Waiting == 1, "the toolbar's Fetch to wait");
    }

    /// <summary>The finishing dialog of check 31 step 2: its card, texts, the one operation and the buttons.</summary>
    private static void AssertFinishingDialog(ShellDriver app)
    {
        var dialog = app.Dialog<FinishingDialogViewModel>();
        AssertCard(app, "FinishingDialogCard", 400);
        Assert.Equal(PartD.FinishingTitle, DialogText(app, "FinishingTitle"));
        Assert.Equal(PartD.FinishingText, DialogText(app, "FinishingText"));
        var view = app.TopDialogView!;
        var items = view.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => AutomationProperties.GetAutomationId(block) == "FinishingItem" && block.IsEffectivelyVisible)
            .Select(block => block.Text)
            .ToList();
        Assert.Equal([PartD.FinishingItem], items);
        Assert.DoesNotContain(
            view.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible),
            block => block.Text == FinishingDialogViewModel.RemainingStepsText);
        Assert.Equal(["Keep open", "Quit now"], DialogButtons(app));
        var quit = DialogButton(app, "ConfirmButton");
        Assert.Equal(PaletteOf(app).Danger, (quit.Background as ISolidColorBrush)?.Color);
        Assert.Single(dialog.Items);
    }

    /// <summary>The left panel of auth (the report's "Scenario graphs"), with main behind by <paramref name="behind"/>.</summary>
    private static void AssertAuthLeftPanel(ShellDriver app, int behind)
    {
        AssertSections(app, ("Local branches", 1), ("Remotes", 1), ("Pull requests", 0), ("Tags", 0), ("Stashes", 0));
        AssertLocalBranch(app, "main", ahead: 0, behind: behind, head: true);
        Assert.Equal([("origin", true)], Remotes(app));
        Assert.Equal(["main"], RemoteChildren(app, "origin"));
    }

    /// <summary>The error dialog a check expects, with the server's port in place of 47391.</summary>
    private static ExpectedErrorDialog Expected(PartD.ErrorTexts texts, int port = ServerPort) => new(
        texts.Title,
        WithPort(texts.Summary, port),
        texts.Repository,
        texts.Command is null ? null : WithPort(texts.Command, port),
        texts.Buttons,
        [.. texts.Lines.Select(line => WithPort(line, port))],
        texts.KeyLine is null ? null : WithPort(texts.KeyLine, port),
        texts.OutputFromGit);

    /// <summary>A hash of every file under the repository's .git folder, by its path relative to the working tree.</summary>
    private static Dictionary<string, string> DotGitHashes(TempRepo repo)
    {
        var dotGit = Path.Combine(repo.Path, ".git");
        return Directory.EnumerateFiles(dotGit, "*", SearchOption.AllDirectories).ToDictionary(
            file => Path.GetRelativePath(repo.Path, file).Replace('\\', '/'),
            file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
    }
}
