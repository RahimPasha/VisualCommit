// Part G, check G25 (docs/test-reports/phase-6.md): fetch refspecs (D222, D201, design 10). The
// drop's push onto a renamed remote branch, a ref one remote fetches from two refs (R2), its
// checkout without an upstream and the push that would stop every fetch; a renamed ref's menu and
// its deletion by its name on the remote; a ref that is not a branch (R4) and Pull's tooltip naming
// its upstream by its tracking ref (GD15). Two methods, part B on a fresh copy (GD12).
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

public sealed partial class Phase6ChecksG
{
    /// <summary>
    /// Check G25, part A (steps 1 to 7): <c>topic</c> dropped onto the renamed <c>fork/up/main</c>
    /// pushes fork's <c>main</c>, and git moves the tracking ref; dropped onto the R2 ref
    /// <c>origin/pr/1</c> both pushes are disabled; that ref's deletion disabled, its checkout makes
    /// <c>pr/1</c> without an upstream (D201) and, with <c>main</c> checked out again, its
    /// double-click checks out that <c>pr/1</c>; the push of <c>pr/1</c> to origin refused before git.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G25_renamed_and_ambiguous_remote_branches()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 1: RS0; the folders up and pr opened.
        AssertGraph(app, PartG.Rs0, PartG.RsLanes, showsDate: true);
        AssertDashedLines(app);
        AssertDrawnLabels(app, PartG.Rs0Drawn);
        AssertLeftPanel(app, PartG.RsPanel(PartG.RsLocalMain, PartG.RsRemotesClosed));
        AssertBranch(app, "main");
        AssertNothingToUndo(app);
        await OpenFolderAsync(app, "Remotes", "fork", "up");
        await OpenFolderAsync(app, "Remotes", "origin", "pr");
        AssertLeftPanel(app, PartG.RsPanel(PartG.RsLocalMain, PartG.RsRemotesUpAndPrOpen));
        AssertNothingSelected(app);

        // Step 2: topic dropped onto fork/up/main: the push names fork's main (10.4 #8, GD24).
        var release = await DragRowAsync(app, ["Local branches", "topic"], ["Remotes", "fork", "up", "main"]);
        var menu = app.Menu();
        AssertNoDragDrawn(app);
        AssertNothingSelected(app);
        AssertWholeMenu(app, menu, PartG.DropOntoRenamedMenu, PartG.PushTopicItem, PartG.DropMenuHeight, release);
        Shot(app, "g25a-drop-onto-renamed");
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        using (var running = app.RecordStatusTexts())
        {
            app.ClickMenuItem(PartG.PushTopicItem);
            await WaitForSuccessAsync(app, repo, PartG.PushTopicToRenamed, mark, NetworkTimeout);
            Assert.Contains(PartG.PushingTopicToRenamed, running.Texts);
        }

        var shot = Shot(app, "g25b-pushed-to-renamed");
        var since = Since(app, calls);
        var push = since[IndexOf(since, PartG.PushTopicCommand)];
        Assert.True(push.ExitCode == 0, $"\"{push.CommandText}\" exited {push.ExitCode}.\n{push.StandardError}");
        Assert.Contains(PartG.PushTopicKey, Printed(push.StandardError));
        Assert.DoesNotContain(since, call => call.CommandText.Contains("refs/heads/topic:refs/heads/up/main", StringComparison.Ordinal));
        AssertStatus(app, PartG.PushedTopicToRenamed);
        Assert.Equal(["refs/heads/main " + PartG.TopicSha], await RemoteRefsAsync(repo, "fork"));
        Assert.Equal(PartG.TopicSha, await RevParseAsync(repo, "refs/remotes/fork/up/main"));
        Assert.Null(await RevParseAsync(repo, "refs/remotes/fork/up/up/main"));
        AssertGraph(app, PartG.Rs1, PartG.RsLanes, showsDate: true, picture: shot);
        AssertLeftPanel(app, PartG.RsPanel(PartG.RsLocalMain, PartG.RsRemotesUpAndPrOpen));

        // Step 3: topic dropped onto origin/pr/1 (R2): both pushes disabled; Esc.
        var refs = await RefsAsync(repo);
        calls = app.GitCalls().Count;
        release = await DragRowAsync(app, ["Local branches", "topic"], ["Remotes", "origin", "pr", "1"]);
        menu = app.Menu();
        AssertNoDragDrawn(app);
        AssertWholeMenu(app, menu, PartG.DropOntoAmbiguousMenu, "Check out topic and rebase it onto origin/pr/1 interactively…", PartG.DropMenuHeight, release);
        Shot(app, "g25c-drop-onto-ambiguous");
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.DoesNotContain(Since(app, calls), call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        Assert.Equal(refs, await RefsAsync(repo));

        // Step 4: origin/pr/1's menu; its checkout makes pr/1 without an upstream (D201, TG5).
        var (refMenu, pointer) = await RightClickRefAsync(app, "Remotes", "origin", "pr", "1");
        AssertNothingSelected(app);
        AssertWholeMenu(app, refMenu, PartG.AmbiguousRefMenu, PartG.CheckOutOriginPr1Item, PartG.RemoteBranchMenuHeight, pointer);
        Shot(app, "g25d-ambiguous-ref-menu");
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.CheckOutOriginPr1Item);
        await WaitForSuccessAsync(app, repo, PartG.CheckOutOriginPr1, mark);
        shot = Shot(app, "g25e-created-without-upstream");
        since = Since(app, calls);
        var create = since[IndexOf(since, PartG.CreatePr1Command)];
        Assert.True(create.ExitCode == 0, $"\"{create.CommandText}\" exited {create.ExitCode}.\n{create.StandardError}");
        await AssertGitPrintedAsync(create.StandardOutput, [], null, "The checkout's standard output");
        await AssertGitPrintedAsync(create.StandardError, [PartG.SwitchedToNewPr1], PartG.SwitchedToNewPr1, "The checkout's standard error");
        Assert.DoesNotContain(since, call => call.CommandText.Split(' ').Contains("--track"));
        AssertStatus(app, PartG.CreatedPr1WithoutUpstream);
        var layout = app.OperationStatusLayout();
        Assert.Equal(PartG.CreatedPr1WithoutUpstream, layout.ToolTip);
        Assert.False(layout.IsWhole, "The status bar draws D201's long result whole; GD14 has it cut with \"…\".");
        Assert.True(layout.Width <= PartG.StatusTextMaxWidth + 0.5, $"The status text is {layout.Width} wide, more than {PartG.StatusTextMaxWidth}.");
        AssertBranch(app, "pr/1");
        AssertGraph(app, PartG.Rs2, PartG.RsLanes, showsDate: true, picture: shot);
        AssertLeftPanel(app, PartG.RsPanel(PartG.RsLocalPr1, PartG.RsRemotesUpAndPrOpen));
        await AssertOnBranchAsync(repo, "pr/1", PartG.TopicSha);
        Assert.Empty(await BranchKeysAsync(repo, @"^branch\.pr/1\."));
        Assert.Equal(PartG.Pr1StatusBranch, await StatusBranchLinesAsync(repo));

        // Step 5: a double-click on Local branches › main (TG6).
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.DoubleClick(await app.RefRowAsync("Local branches", "main"));
        await WaitForSuccessAsync(app, repo, PartG.CheckOutMain, mark);
        since = Since(app, calls);
        var switchMain = since[IndexOf(since, PartG.SwitchMainCommand)];
        Assert.True(switchMain.ExitCode == 0, $"\"{switchMain.CommandText}\" exited {switchMain.ExitCode}.\n{switchMain.StandardError}");
        await AssertGitPrintedAsync(switchMain.StandardOutput, ["Your branch is up to date with 'origin/main'."], null, "The checkout's standard output");
        await AssertGitPrintedAsync(switchMain.StandardError, [PartG.SwitchedToMain], PartG.SwitchedToMain, "The checkout's standard error");
        AssertStatus(app, PartG.CheckedOutMain);
        AssertBranch(app, "main");
        AssertGraph(app, PartG.Rs3, PartG.RsLanes, showsDate: true);
        AssertLeftPanel(app, PartG.RsPanel(PartG.RsLocalPrClosed, PartG.RsRemotesUpAndPrOpen));

        // Step 6: a double-click on Remotes › origin › pr › 1 checks out the pr/1 that exists (10.4 #10, D201, GD21).
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        var pr1 = app.RefRowModel("Remotes", "origin", "pr", "1");
        app.DoubleClick(await app.RefRowAsync("Remotes", "origin", "pr", "1"));
        await WaitForSuccessAsync(app, repo, PartG.CheckOutOriginPr1, mark);
        Assert.Equal([pr1.Key], SelectedRefRows(app));
        await AssertRowSelectedAsync(app, 0, "Work on the topic");
        since = Since(app, calls);
        var switchPr1 = since[IndexOf(since, PartG.SwitchPr1Command)];
        Assert.True(switchPr1.ExitCode == 0, $"\"{switchPr1.CommandText}\" exited {switchPr1.ExitCode}.\n{switchPr1.StandardError}");
        await AssertGitPrintedAsync(switchPr1.StandardOutput, [], null, "The checkout's standard output");
        await AssertGitPrintedAsync(switchPr1.StandardError, [PartG.SwitchedToPr1], PartG.SwitchedToPr1, "The checkout's standard error");
        Assert.DoesNotContain(since, call => call.CommandText.StartsWith("git switch --create", StringComparison.Ordinal));
        AssertStatus(app, PartG.CheckedOutPr1WithoutUpstream);
        AssertBranch(app, "pr/1");
        AssertGraph(app, PartG.Rs2, PartG.RsLanes, showsDate: true);
        await AssertOnBranchAsync(repo, "pr/1", PartG.TopicSha);
        Assert.Empty(await BranchKeysAsync(repo, @"^branch\.pr/1\."));
        refs = await RefsAsync(repo);

        // Step 7: "Push pr/1…" to origin through the push dialog: refused before git (10.4's guard, GD13).
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        (menu, _) = await RightClickRefAsync(app, "Local branches", "pr", "1");
        AssertItemEnabled(menu, "pr/1", PartG.PushPr1Item);
        AssertMenuKey(app, PartG.PushPr1Item, PartG.PushKey);
        app.ClickMenuItem(PartG.PushPr1Item);
        await app.WaitForDialogAsync<PushDialogViewModel>();
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The push dialog has no view.");
        Assert.Equal(PartG.PushPr1, ((TextBlock)app.FindByAutomationId(view, "PushTitle")).Text);
        Assert.Equal(PartG.PushDialogRemotes, PushRemoteOptions(app));
        Assert.Equal("pr/1", ((TextBox)app.FindByAutomationId(view, "PushBranchBox")).Text);
        Assert.True(((CheckBox)app.FindByAutomationId(view, "PushSetUpstream")).IsChecked == true, "\"Set as upstream\" is not ticked.");
        Assert.Equal(["Cancel", "Push"], view.GetVisualDescendants().OfType<Button>().Where(button => button is not Avalonia.Controls.Primitives.ToggleButton && button.IsEffectivelyVisible && button.Content is string).OrderBy(button => app.BoundsOf(button).X).Select(button => (string)button.Content!));
        app.Click(PushRemoteOption(app, "origin"));
        app.Click(app.FindByAutomationId(view, "ConfirmButton"));
        await WaitForFailureAsync(app, repo, PartG.PushPr1, mark);
        Shot(app, "g25f-push-would-stop-fetch");
        Assert.DoesNotContain(Since(app, calls), call => call.CommandText.StartsWith("git push", StringComparison.Ordinal));
        await AssertErrorDialogAsync(app, new ExpectedErrorDialog(PartG.PushFailedTitle, PartG.PushWouldStopFetch, PartG.Refspecs, null, PartG.CloseOnly, []));
        AssertStatus(app, PartG.Failed(PartG.PushPr1), OperationStatusTone.Danger);
        Assert.Equal(
            ["refs/heads/feature/x " + PartG.ParserSha, "refs/heads/main " + PartG.ParserSha, "refs/pull/1/head " + PartG.TopicSha],
            await RemoteRefsAsync(repo, "origin"));
        Assert.Empty(await BranchKeysAsync(repo, @"^branch\.pr/1\."));
        Assert.Equal(refs, await RefsAsync(repo));
        AssertNoClippedText(app);
        await CloseDialogAsync(app);
    }

    /// <summary>
    /// Check G25, part B (steps 8 to 12), on a fresh copy (GD12): the renamed ref's menu, its checkout
    /// disabled while HEAD is on its name and its deletion by that name with the mapped lease and
    /// backup (10.4 #9); the R4 ref's menu, its deletion disabled and its checkout tracking it as git
    /// does; Pull's tooltip naming that upstream by its tracking ref (GD15).
    /// </summary>
    [AvaloniaFact]
    public async Task Check_G25_a_renamed_deletion_and_a_ref_that_is_not_a_branch()
    {
        using var repo = await Scenarios.RenamingRefspecAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        // Step 8: RS0; fork/up/main's menu.
        AssertGraph(app, PartG.Rs0, PartG.RsLanes, showsDate: true);
        await OpenFolderAsync(app, "Remotes", "fork", "up");
        AssertLeftPanel(app, PartG.RsPanel(PartG.RsLocalMain, PartG.RsRemotesUpOpen));
        var (menu, pointer) = await RightClickRefAsync(app, "Remotes", "fork", "up", "main");
        AssertWholeMenu(app, menu, PartG.RenamedRefMenu, "Merge fork/up/main into main", PartG.RemoteBranchMenuHeight, pointer);
        Shot(app, "g25g-renamed-ref-menu");

        // Step 9: "Delete main on fork…": the question names the branch on fork.
        app.ClickMenuItem(PartG.DeleteMainOnForkItem);
        await AssertConfirmationAsync(app, PartG.DeleteMainOnForkQuestionTitle, PartG.DeleteMainOnForkQuestion, PartG.BackupDetail, PartG.DeleteOnFork);
        Shot(app, "g25h-delete-renamed-question");

        // Step 10: "Delete on fork": the lease by the mapping (TG7), the backup, RS4.
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The question has no view.");
        using (var running = app.RecordStatusTexts())
        {
            app.Click(app.FindByAutomationId(view, "ConfirmButton"));
            await WaitForSuccessAsync(app, repo, PartG.DeleteMainOnFork, mark, NetworkTimeout);
            Assert.Contains(PartG.DeletingMainOnFork, running.Texts);
        }

        var since = Since(app, calls);
        var delete = since[IndexOf(since, PartG.DeleteMainOnForkCommand)];
        Assert.True(delete.ExitCode == 0, $"\"{delete.CommandText}\" exited {delete.ExitCode}.\n{delete.StandardError}");
        await AssertGitPrintedAsync(delete.StandardError, ["To ../fork.git", PartG.DeletedKey], PartG.DeletedKey, "The deletion's standard error");
        Assert.DoesNotContain(since, call => call.CommandText.Contains("refs/heads/up/main", StringComparison.Ordinal));
        AssertStatus(app, PartG.DeletedMainOnFork);
        Assert.Empty(await RemoteRefsAsync(repo, "fork"));
        Assert.Null(await RevParseAsync(repo, "refs/remotes/fork/up/main"));
        Assert.Equal([new RefEntry(PartG.DeletionBackup, PartG.ParserSha)], await repo.BackupRefsAsync());
        AssertGraph(app, PartG.Rs4, PartG.RsLanes, showsDate: true);
        AssertLeftPanel(app, PartG.RsPanel(PartG.RsLocalMain, PartG.Rs4Remotes));
        AssertUndoEnabled(app);

        // Step 11: upstream/pr/7's menu (R4); its checkout tracks refs/pull/7/head as git does (TG8).
        await OpenFolderAsync(app, "Remotes", "upstream", "pr");
        (menu, pointer) = await RightClickRefAsync(app, "Remotes", "upstream", "pr", "7");
        AssertWholeMenu(app, menu, PartG.NotABranchMenu, PartG.CheckOutUpstreamPr7Item, PartG.RemoteBranchMenuHeight, pointer);
        Shot(app, "g25i-not-a-branch-menu");
        mark = app.LogMark();
        calls = app.GitCalls().Count;
        app.ClickMenuItem(PartG.CheckOutUpstreamPr7Item);
        await WaitForSuccessAsync(app, repo, PartG.CheckOutUpstreamPr7, mark);
        since = Since(app, calls);
        var create = since[IndexOf(since, PartG.CreatePr7Command)];
        Assert.True(create.ExitCode == 0, $"\"{create.CommandText}\" exited {create.ExitCode}.\n{create.StandardError}");
        await AssertGitPrintedAsync(create.StandardOutput, [PartG.Pr7TrackingLine], null, "The checkout's standard output");
        await AssertGitPrintedAsync(create.StandardError, [PartG.SwitchedToNewPr7], PartG.SwitchedToNewPr7, "The checkout's standard error");
        AssertStatus(app, PartG.CreatedPr7Tracking);
        AssertBranch(app, "pr/7");
        AssertGraph(app, PartG.Rs5, PartG.RsLanes, showsDate: true);
        AssertLeftPanel(app, PartG.RsPanel(PartG.RsLocalPr7, PartG.Rs5Remotes));
        Assert.Equal(["branch.pr/7.remote upstream", "branch.pr/7.merge refs/pull/7/head"], await BranchKeysAsync(repo, @"^branch\.pr/7\."));
        Assert.Equal(PartG.Pr7StatusBranch, await StatusBranchLinesAsync(repo));

        // Step 12: idle, the refresh included; Pull's tooltip names the upstream by its tracking ref (GD15).
        await app.WaitForIdleAsync();
        AssertEnabledToolTip(app, "PullButton", PartG.PullFromUpstreamPr7);
    }

    /// <summary>The push dialog's remotes (its radio buttons, <c>ChoiceOption</c>), in order, each with whether it is checked.</summary>
    private static List<(string Remote, bool Checked)> PushRemoteOptions(ShellDriver app)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        return [.. view.GetVisualDescendants().OfType<RadioButton>()
            .Where(button => AutomationProperties.GetAutomationId(button) == "ChoiceOption" && button.IsEffectivelyVisible)
            .Select(button => (AutomationProperties.GetName(button) ?? string.Empty, button.IsChecked == true))];
    }

    /// <summary>The push dialog's radio button of <paramref name="remote"/>.</summary>
    private static RadioButton PushRemoteOption(ShellDriver app, string remote)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("No dialog is open.");
        return view.GetVisualDescendants().OfType<RadioButton>()
            .Single(button => AutomationProperties.GetAutomationId(button) == "ChoiceOption" && AutomationProperties.GetName(button) == remote && button.IsEffectivelyVisible);
    }
}
