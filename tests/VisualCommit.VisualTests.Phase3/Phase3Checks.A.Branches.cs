// Part A, checks 2 and 3 (docs/test-reports/phase-3.md): the menus of branches, remote branches, a
// local upstream, tags, stashes, remotes, section headers and the empty graph area, and the objects
// that have no menu.
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase3.Phase3Expectations;

namespace VisualCommit.VisualTests.Phase3;

public sealed partial class Phase3ChecksA
{
    /// <summary>Check 2, steps 1 to 5: local branches from the left panel and the graph's labels, the current branch, remote branches (ops).</summary>
    [AvaloniaFact]
    public async Task Check_02_branch_menus()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        var before = await GitStateAsync(repo);
        AssertLeftPanel(app, PartA.OpsLeftPanel);

        // Step 1: open the folder feature under Local branches and right-click its alpha.
        app.Click(await app.RefRowAsync("Local branches", "feature"));
        var alpha = await app.RefRowAsync("Local branches", "feature", "alpha");
        var pointer = app.BoundsOf(alpha).Center;
        app.RightClick(pointer);
        var menu = app.Menu();
        var shot = Shot(app, "02a-local-branch-menu");
        Assert.Equal(["  ▾ feature", "    alpha", "    conflict", "    ff"], LeftPanelLines(app).Skip(1).Take(4));
        AssertMenu(menu, PartA.FeatureAlphaMenu);
        AssertHighlighted(menu, "Check out feature/alpha");
        AssertMenuLook(app, menu, shot);
        Assert.Equal(PartA.FeatureAlphaMenuHeight, menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuAtPointer(menu, pointer);
        Assert.True(repository.Selection.IsEmpty, "The right-click selected a commit.");
        Assert.Equal(-1, repository.SelectedIndex);
        var placeholder = (TextBlock)app.FindByAutomationId("DetailsPlaceholder");
        Assert.True(placeholder.IsEffectivelyVisible);
        Assert.Equal("Select a commit to see its details.", placeholder.Text);
        await AssertRefHighlightedAsync(app, null);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 2: the label feature/ff selects its row and opens the branch's menu.
        pointer = app.GraphLabelBounds(PartA.FastForwardMeRow, "feature/ff").Center;
        app.RightClick(pointer);
        menu = app.Menu();
        await WaitForDetailsAsync(app, "Fast-forward me");
        shot = Shot(app, "02b-label-menu-no-upstream");
        AssertRowSelected(app, PartA.FastForwardMeRow);
        AssertMenu(menu, PartA.FeatureFfMenu);
        AssertHighlighted(menu, "Check out feature/ff");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 3: the current branch: its delete is disabled, in the disabled colour, its reason shown.
        pointer = app.GraphLabelBounds(PartA.MainWorkRow, "main").Center;
        app.RightClick(pointer);
        app.Menu();
        await WaitForDetailsAsync(app, "Main work");
        var tip = await app.RestPointerOnMenuItemAsync("Delete main…");
        shot = Shot(app, "02c-current-branch-delete-disabled", keepPointer: true);
        Assert.Equal(PartA.CannotDeleteCurrent, tip);
        Assert.True(ToolTip.GetIsOpen(app.MenuItemControl("Delete main…")));
        AssertRowSelected(app, PartA.MainWorkRow);
        menu = app.Menu();
        AssertMenu(menu, PartA.MainMenu);
        AssertHighlighted(menu, "Pull from origin/main");
        AssertMenuLook(app, menu, shot);
        Assert.Equal(PaletteOf(app).TextDisabled, menu.Item("Delete main…").TextColor);
        AssertMenuAtPointer(menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 4: under Remotes › origin, open feature and right-click its alpha.
        app.Click(await app.RefRowAsync("Remotes", "origin", "feature"));
        var remoteAlpha = await app.RefRowAsync("Remotes", "origin", "feature", "alpha");
        pointer = app.BoundsOf(remoteAlpha).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "02d-remote-branch-menu");
        Assert.Contains("    ▾ feature", LeftPanelLines(app));
        Assert.Equal(new[] { "    ▾ feature", "      alpha", "    main" }, LeftPanelLines(app).SkipWhile(line => line != "    ▾ feature").Take(3));
        AssertMenu(menu, PartA.OriginFeatureAlphaMenu);
        AssertHighlighted(menu, "Check out origin/feature/alpha");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        AssertRowSelected(app, PartA.MainWorkRow);
        app.ClickMenuItem("Copy branch name");
        await app.WaitForAsync(() => app.Clipboard.Text is not null, "the branch name on the clipboard");
        Assert.Equal(["origin/feature/alpha"], app.Clipboard.Texts);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 5: origin/main, whose local main is checked out: its checkout is disabled.
        var remoteMain = await app.RefRowAsync("Remotes", "origin", "main");
        pointer = app.BoundsOf(remoteMain).Center;
        app.RightClick(pointer);
        app.Menu();
        tip = await app.RestPointerOnMenuItemAsync("Check out origin/main");
        shot = Shot(app, "02e-remote-branch-checked-out", keepPointer: true);
        Assert.Equal(PartA.MainAlreadyCheckedOut, tip);
        menu = app.Menu();
        AssertMenu(menu, PartA.OriginMainCheckedOutMenu);
        AssertHighlighted(menu, "Merge origin/main into main");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        AssertRowSelected(app, PartA.MainWorkRow);
        await AssertRefHighlightedAsync(app, null);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        Assert.Equal(before, await GitStateAsync(repo));
    }

    /// <summary>Check 2, steps 6 to 9: the edges scenario with loc checked out: a local upstream, a worktree's branch, a local branch named origin/main beside the remote one.</summary>
    [AvaloniaFact]
    public async Task Check_02_local_upstream_menus()
    {
        using var repo = await Scenarios.EdgeCasesAsync();
        await repo.CheckoutAsync("loc");
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        var before = await GitStateAsync(repo);

        // The edges-with-loc graph and left panel as "Scenario repos" gives; nothing selected.
        AssertGraph(app, PartA.EdgesLocGraph);
        Assert.Equal("## loc...main [behind 1]", (await GitLinesAsync(repo, "status", "-sb"))[0]);
        AssertBranch(app, "loc");
        var panel = LeftPanelLines(app);
        Assert.Equal(PartA.EdgesLocLocalBranches, panel.Take(PartA.EdgesLocLocalBranches.Length));
        Assert.Contains("▾ Remotes 1", panel);
        Assert.Contains("▾ Stashes 0", panel);
        Assert.Equal(PaletteOf(app).Accent, await RefNameColourAsync(app, "Local branches", "loc"));
        Assert.True(repository.Selection.IsEmpty);

        // Step 6: loc, whose upstream is the local main: no push items (D101).
        var pointer = app.BoundsOf(await app.RefRowAsync("Local branches", "loc")).Center;
        app.RightClick(pointer);
        var menu = app.Menu();
        var shot = Shot(app, "02f-local-upstream-menu");
        AssertMenu(menu, PartA.LocMenu);
        AssertHighlighted(menu, "Pull from main");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        Assert.True(repository.Selection.IsEmpty);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 7: side, checked out in the linked worktree: every item enabled, git decides.
        pointer = app.BoundsOf(await app.RefRowAsync("Local branches", "side")).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        AssertMenu(menu, PartA.SideMenu);
        Assert.All(menu.Items, item => Assert.True(item.IsEnabled, $"\"{item.Label}\" is disabled."));
        AssertHighlighted(menu, "Check out side");
        AssertMenuAtPointer(menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 8: the local branch origin/main, in the folder origin under Local branches.
        app.Click(await app.RefRowAsync("Local branches", "origin"));
        pointer = app.BoundsOf(await app.RefRowAsync("Local branches", "origin", "main")).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "02g-local-origin-main-menu");
        AssertMenu(menu, PartA.LocalOriginMainMenu);
        AssertHighlighted(menu, "Check out origin/main");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 9: the remote branch origin/main: its checkout is enabled, HEAD is on loc.
        pointer = app.BoundsOf(await app.RefRowAsync("Remotes", "origin", "main")).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "02h-remote-origin-main-menu");
        AssertMenu(menu, PartA.RemoteOriginMainMenu);
        AssertHighlighted(menu, "Check out origin/main");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        Assert.Equal(before, await GitStateAsync(repo));
    }

    /// <summary>Check 3: tag, stash, remote, section-header and empty-area menus; objects with no menu; the Remotes and tag dialogs (ops).</summary>
    [AvaloniaFact]
    public async Task Check_03_other_menus()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = Repo(app);
        var remotes = await GitTextAsync(repo, "remote", "-v");

        // Step 1: the tag v1.0; "Copy tag name".
        var pointer = app.BoundsOf(await app.RefRowAsync("Tags", "v1.0")).Center;
        app.RightClick(pointer);
        var menu = app.Menu();
        var shot = Shot(app, "03a-tag-menu");
        AssertMenu(menu, PartA.TagMenu);
        AssertHighlighted(menu, "Check out v1.0");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        Assert.True(repository.Selection.IsEmpty);
        await AssertRefHighlightedAsync(app, null);
        app.ClickMenuItem("Copy tag name");
        await app.WaitForAsync(() => app.Clipboard.Text is not null, "the tag name on the clipboard");
        Assert.Equal(["v1.0"], app.Clipboard.Texts);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 2: the stash in the left panel, then its label in the graph.
        pointer = app.BoundsOf(await app.RefRowAsync("Stashes", "On main: Experiment")).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "03b-stash-menu");
        AssertMenu(menu, PartA.StashMenu);
        AssertHighlighted(menu, "Apply stash");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        Assert.True(repository.Selection.IsEmpty);
        await AssertRefHighlightedAsync(app, null);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        pointer = app.GraphLabelBounds(PartA.StashRow, "stash@{0}").Center;
        app.RightClick(pointer);
        menu = app.Menu();
        AssertMenu(menu, PartA.StashMenu);
        AssertHighlighted(menu, "Apply stash");
        AssertMenuAtPointer(menu, pointer);
        AssertRowSelected(app, PartA.StashRow);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 3: the remote origin; "Copy URL" copies the URL as git prints it.
        pointer = app.BoundsOf(await app.RefRowAsync("Remotes", "origin")).Center;
        app.RightClick(pointer);
        menu = app.Menu();
        shot = Shot(app, "03c-remote-menu");
        AssertMenu(menu, PartA.RemoteMenu);
        AssertHighlighted(menu, "Fetch origin");
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        app.ClickMenuItem("Copy URL");
        await app.WaitForAsync(() => app.Clipboard.Texts.Count == 2, "the URL on the clipboard");
        Assert.Equal(await GitTextAsync(repo, "remote", "get-url", "origin"), app.Clipboard.Text);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 4: the section headers' menus; a right-click on a header does not toggle it.
        foreach (var (section, lines, highlighted) in new[]
        {
            ("Local branches", PartA.LocalBranchesHeaderMenu, "Create branch…"),
            ("Remotes", PartA.RemotesHeaderMenu, "Add remote…"),
            ("Tags", PartA.TagsHeaderMenu, "Create tag at HEAD…"),
        })
        {
            var open = ((RefSectionRow)app.RefRowModel(section)).IsOpen;
            pointer = app.BoundsOf(await app.RefRowAsync(section)).Center;
            app.RightClick(pointer);
            menu = app.Menu();
            AssertMenu(menu, lines);
            AssertHighlighted(menu, highlighted);
            AssertMenuAtPointer(menu, pointer);
            app.PressKey(Key.Escape);
            await AssertNoMenuAsync(app, logMark: null);
            Assert.Equal(open, ((RefSectionRow)app.RefRowModel(section)).IsOpen);
        }

        pointer = app.BoundsOf(await app.RefRowAsync("Stashes")).Center;
        app.RightClick(pointer);
        app.Menu();
        var tip = await app.RestPointerOnMenuItemAsync("Stash changes…");
        shot = Shot(app, "03d-stashes-header-disabled-tooltip", keepPointer: true);
        Assert.Equal(PartA.NoChangesToStash, tip);
        menu = app.Menu();
        AssertMenu(menu, PartA.StashesHeaderMenu);
        AssertHighlighted(menu, null);
        AssertMenuLook(app, menu, shot);
        AssertMenuAtPointer(menu, pointer);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);
        Assert.True(((RefSectionRow)app.RefRowModel("Stashes")).IsOpen);

        // Step 5: no menu for "Pull requests", a folder or the tab; nothing opens or closes.
        var tabs = app.Session.Shell.Tabs.ToList();
        var active = app.Session.Shell.ActiveTab;
        var mark = app.LogMark();
        app.RightClick(await app.RefRowAsync("Pull requests"));
        await AssertNoMenuAsync(app, mark);
        app.RightClick(await app.RefRowAsync("Local branches", "feature"));
        await AssertNoMenuAsync(app, mark);
        app.RightClick(app.FindByAutomationId("RepoTab"));
        await AssertNoMenuAsync(app, mark);
        Shot(app, "03e-no-menu");
        var pullRequests = (RefSectionRow)app.RefRowModel("Pull requests");
        Assert.False(pullRequests.IsOpen);
        Assert.Equal(0, pullRequests.Count);
        Assert.False(((RefItemRow)app.RefRowModel("Local branches", "feature")).IsOpen);
        Assert.Equal(tabs, app.Session.Shell.Tabs);
        Assert.Same(active, app.Session.Shell.ActiveTab);

        // Step 6: the empty graph area: the menu at (522, 426); the selection stays on the stash.
        var empty = new Avalonia.Point(PartA.EmptyAreaPoint.X, PartA.EmptyAreaPoint.Y);
        app.RightClick(empty);
        menu = app.Menu();
        shot = Shot(app, "03f-empty-area-menu");
        AssertRowSelected(app, PartA.StashRow);
        AssertMenu(menu, PartA.OpsEmptyAreaMenu);
        AssertHighlighted(menu, "Fetch all remotes");
        AssertMenuLook(app, menu, shot);
        Assert.Equal(PartA.EmptyAreaMenuHeight, menu.Bounds.Height, WindowLayout.MenuHeightTolerance);
        AssertMenuAtPointer(menu, empty);
        Assert.Equal(new Avalonia.Point(522, 426), menu.Bounds.TopLeft);
        tip = await app.RestPointerOnMenuItemAsync("Stash changes…");
        Assert.Equal(PartA.NoChangesToStash, tip);
        AssertHighlighted(app.Menu(), "Fetch all remotes");

        // Step 7: "Manage remotes…" opens the Remotes dialog. The pointer goes down to it as a
        // user's does, over the separator first, where the tooltip it leaves closes: the tooltip
        // opened below the pointer, over "Manage remotes…".
        var stashItem = app.MenuItemControl("Stash changes…");
        var separator = app.Menu().Entries.Last(entry => entry.IsSeparator);
        app.MoveMouse(separator.Bounds.Center);
        await app.WaitForAsync(() => !ToolTip.GetIsOpen(stashItem), "the tooltip of \"Stash changes…\" to close once the pointer leaves it");
        app.ClickMenuItem("Manage remotes…");
        await app.WaitForDialogAsync<RemotesDialogViewModel>();
        Assert.False(app.IsMenuOpen);
        Shot(app, "03g-remotes-dialog");
        await AssertRemotesDialogAsync(app, repo);
        app.Click(app.FindByAutomationId("CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.Equal(remotes, await GitTextAsync(repo, "remote", "-v"));

        // Step 8: "Create tag at HEAD…" opens the tag dialog; Esc creates nothing.
        app.RightClick(await app.RefRowAsync("Tags"));
        app.Menu();
        app.ClickMenuItem("Create tag at HEAD…");
        await AssertNameDialogAsync(app, new NameDialogExpected(
            "Create tag", PartA.MainWorkTagContext, "Tag name", "Message (optional: makes an annotated tag)", true, null, false, "Create tag", false));
        Shot(app, "03h-tag-dialog");
        AssertNoClippedText(app);
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(["v1.0", "v1.1"], await GitLinesAsync(repo, "tag"));
    }

    /// <summary>
    /// Check 3 step 7: the Remotes dialog (card 460 wide): title "Remotes"; one row "origin" with its
    /// URL in the secondary colour (cut with "…", the whole in its tooltip), "Edit" and "Remove" (in
    /// the danger colour); "Add remote"; the one button "Close".
    /// </summary>
    private static async Task AssertRemotesDialogAsync(ShellDriver app, TempRepo repo)
    {
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The Remotes dialog has no view.");
        Assert.Equal("RemotesDialog", Avalonia.Automation.AutomationProperties.GetAutomationId(view));
        Assert.Equal(460, view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "RemotesDialogCard").Bounds.Width, 0.5);
        Assert.Equal("Remotes", ((TextBlock)app.FindByAutomationId(view, "RemotesTitle")).Text);
        var rows = view.GetVisualDescendants().OfType<Control>()
            .Where(control => Avalonia.Automation.AutomationProperties.GetAutomationId(control) == "RemoteRow" && control.IsEffectivelyVisible)
            .ToList();
        var row = Assert.Single(rows);
        Assert.Equal("origin", Avalonia.Automation.AutomationProperties.GetName(row));
        var url = (TextBlock)app.FindByAutomationId(row, "RemoteUrl");
        var expectedUrl = await GitTextAsync(repo, "remote", "get-url", "origin");
        Assert.Equal(expectedUrl, (url.Text ?? string.Empty).Replace("﻿", string.Empty, StringComparison.Ordinal));
        Assert.Equal(url.Text, ToolTip.GetTip(url) as string);
        Assert.Equal(PaletteOf(app).TextSecondary, (url.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal("Edit", ((Button)app.FindByAutomationId(row, "RemoteEditButton")).Content);
        var remove = (Button)app.FindByAutomationId(row, "RemoteRemoveButton");
        Assert.Equal("Remove", remove.Content);
        Assert.Equal(PaletteOf(app).Danger, (remove.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal("Add remote", ((Button)app.FindByAutomationId(view, "AddRemoteButton")).Content);
        var buttons = view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible && button.Classes.Contains("remoteAction") is false && button.Content is string)
            .Select(button => (string)button.Content!)
            .ToList();
        Assert.Equal(["Add remote", "Close"], buttons);
        Assert.Equal("Close", ((Button)app.FindByAutomationId(view, "CancelButton")).Content);
    }
}
