// Checks L30 and L31 of part L (docs/test-reports/phase-6.md): the sweep's dashboard of "Web" (every
// row's state, a row's menu, the scan's checklist with its folder line), and the overlays and phase
// 6's screens on "ops" (the activity log, the deletion question, the palette, the settings screen's
// General and Git with the merge tool's Pick dialog, an enabled tooltip, Light saved and the screen
// again) and on "auth" (the sign-in prompt and the held fetch with a waiting operation), each in
// both themes by one pair but the single l31d-settings-light. The part's map is in Phase6Checks.L.cs.
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Core;
using VisualCommit.Core.Operations;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using VisualCommit.VisualTests.Phase6;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6Look;

public sealed partial class Phase6ChecksL
{
    /// <summary>
    /// Check L30: the dashboard of "Web" with every row's state, old-site's row menu and the scan's
    /// checklist on the copy's root, its folder line drawn by <c>PathTextBlock</c>, each in both
    /// themes (2.5 to 2.7, 1.9, 11.14, 13.2; LD12, LD13). Nothing is added and no git call writes.
    /// "Web", System with the fake dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L30_dashboard_in_both_themes()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");

        // Step 1: "Web" and a session of its dashboard; the first read.
        using var app = await OpenWorkspaceAsync(data, copy, PartL.Size.Width, PartL.Size.Height, AppTheme.System);
        AssertSystemDarkStart(app);
        var oldSite = copy.PathOf("old-site");
        var workspacesFile = File.ReadAllBytes(new AppPaths(data.Path).WorkspacesFile);
        await ShotInBothThemesAsync(app, "l30a-dashboard", assertEach: (picture, palette) =>
        {
            AssertDashboardLook(app, picture, palette, oldSite);
            Assert.Null(app.DashboardSnapshot().SelectedRow);
            var toggle = (Button)app.FindByAutomationId("ActivityLogToggle");
            Assert.False(toggle.IsEffectivelyEnabled, "ActivityLogToggle is enabled with no row selected.");
            Assert.Equal(PartL.SelectRepository, ToolTip.GetTip(toggle) as string);
        });

        // Step 2: old-site's row menu by a right-click in its Repository cell.
        var logMark = app.LogMark();
        var point = app.DashboardRowPoint("old-site");
        Assert.True(Math.Abs(point.X - PartL.OldSitePoint.X) <= PartL.Tolerance && Math.Abs(point.Y - PartL.OldSitePoint.Y) <= PartL.Tolerance, $"old-site's row is clicked at {point}, not (66, 399).");
        app.RightClick(point);
        var menu = app.Menu();
        Assert.Equal(PartL.OldSiteMenu(oldSite), menu.Lines());
        Assert.Equal(oldSite, menu.HeaderToolTip);
        AssertHighlighted(menu, PartL.CopyPath);
        AssertMenuShape(app, menu);
        Assert.Equal(PartL.OldSiteMenuHeight, menu.Bounds.Height, PartL.Tolerance);
        Assert.True(Math.Abs(menu.Bounds.X - PartL.OldSiteMenuTopLeft.X) <= PartL.Tolerance && Math.Abs(menu.Bounds.Y - PartL.OldSiteMenuTopLeft.Y) <= PartL.Tolerance, $"The menu's top-left is {menu.Bounds.TopLeft}, not (68, 401).");
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartL.RowMenuOpened("old-site")), logMark));
        await ShotInBothThemesAsync(app, "l30b-row-menu", keepPointer: true, assertEach: (picture, palette) =>
        {
            var shown = app.Menu();
            Assert.Equal("old-site", app.DashboardSnapshot().SelectedRow);
            AssertColour(palette.Selection, picture, new Point(1090, PartL.DashboardRowCentre(9) - 10), "old-site's row background (VcSelectionBrush)");
            var copyPath = shown.Item(PartL.CopyPath);
            AssertColour(palette.Hover, picture, new Point(copyPath.Bounds.Right - 6, copyPath.Bounds.Top + 3), "the highlighted Copy path (VcHoverBackgroundBrush)");
            foreach (var item in shown.Items.Where(item => !item.IsEnabled))
            {
                AssertInk(picture, item.Bounds, palette.TextDisabled, palette.Panel, $"the disabled {item.Label} (VcTextDisabledBrush)");
            }

            var toggle = (Button)app.FindByAutomationId("ActivityLogToggle");
            Assert.Equal(PartL.DoesNotExist(oldSite), ToolTip.GetTip(toggle) as string);
        });
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        // Step 3: Add repository… on the copy's root: the checklist, every candidate already in "Web".
        app.Folders.Answer(copy.Root);
        app.Click(app.FindByAutomationId("DashboardAddRepository"));
        var checklist = await app.WaitForAddRepositoriesDoneAsync(OperationChainTimeout);
        Assert.Equal(PartL.FoundIn(copy.Root), checklist.FolderText);
        await ShotInBothThemesAsync(app, "l30c-scan-checklist", assertEach: (picture, palette) =>
        {
            Assert.Equal([PartL.AddRepositoryToolTip], app.Folders.Titles);
            var shown = app.AddRepositories();
            AssertCard(app, picture, palette, PartL.WideCardWidth, PartL.WideCardLeft);
            Assert.Equal(PartL.AddRepositoriesTitle, shown.Title);
            Assert.Equal(PartL.AddRepositoriesTitle, ((TextBlock)app.FindByAutomationId(app.TopDialogView!, "AddRepositoriesTitle")).Text);
            AssertPathLines(shown.FolderLines, shown.FolderText, copy.Root, "ScanFolderText");
            Assert.Null(shown.ProgressText);
            Assert.Null(shown.StoppedText);
            Assert.Null(shown.FailedText);
            Assert.Equal(PartL.Checklist, shown.Rows.Select(row => row.Name));
            foreach (var (row, index) in shown.Rows.Select((row, index) => (row, index)))
            {
                Assert.Equal((PartL.AlreadyInThisWorkspace, true, false), (row.Detail, row.IsChecked, row.IsEnabled));
                Assert.Equal(PartL.ChecklistRowHeight, row.Bounds.Height, 0.5);
                Assert.Equal(index < 8, row.InView);
            }

            Assert.Equal(PartL.ChecklistListHeight, shown.ListBounds!.Value.Height, 0.5);
            var view = app.TopDialogView!;
            var first = view.GetVisualDescendants().OfType<DialogRow>().First(candidate => AutomationProperties.GetName(candidate) == "api");
            var name = first.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "FoundRepositoryName");
            var detail = first.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "FoundRepositoryDetail");
            Assert.Equal((palette.TextPrimary, palette.TextSecondary), ((name.Foreground as ISolidColorBrush)?.Color, (detail.Foreground as ISolidColorBrush)?.Color));
            Assert.Equal((PartL.AddRepositories, false), (shown.ConfirmText, shown.ConfirmEnabled));
            Assert.Equal([PartL.Cancel, PartL.AddRepositories], ButtonsOf(app, view));
        });
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(workspacesFile, File.ReadAllBytes(new AppPaths(data.Path).WorkspacesFile));
        Assert.Equal(10, app.DashboardSnapshot().Rows.Count);
    }

    /// <summary>
    /// Check L31, part A (steps 1 to 7) on "ops": the activity log open after the toolbar's Fetch;
    /// the deletion question with the danger button's text token; the palette; the settings screen
    /// on General with System checked; its Git section on <c>All repositories (global)</c> with the
    /// merge tool's Pick dialog stacked; Fetch's tooltip with its key; then Light chosen and saved and
    /// the screen opened again by its key, the single picture L34 compares with. Nothing changes git.
    /// System with the fake dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L31_overlays_and_settings_in_both_themes()
    {
        using var repo = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, PartL.Size.Width, PartL.Size.Height, AppTheme.System, systemDark: true);
        AssertSystemDarkStart(app);
        var exact = await repo.ExactStateAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;

        // Step 1: the toolbar's Fetch, then the activity log opened.
        var mark = app.LogMark();
        var calls = app.GitCalls().Count;
        app.Click(ToolbarButton(app, "FetchButton"));
        await app.WaitForLogLineAsync(AppLogFile.OperationEnded("ops", "Fetch", OperationOutcome.Succeeded, PartL.FetchedFromAll), mark: mark, timeout: NetworkTimeout);
        await app.WaitForIdleAsync();
        var fetch = Assert.Single(app.GitCalls().Skip(calls), call => call.CommandText.StartsWith("git fetch", StringComparison.Ordinal));
        Assert.Equal(PartL.FetchCommand, fetch.CommandText);
        Assert.Equal((0, string.Empty), (fetch.ExitCode, fetch.StandardOutput.Trim() + fetch.StandardError.Trim()));
        AssertStatus(app, PartL.FetchedFromAll);
        Assert.Equal(exact, await repo.ExactStateAsync());
        app.ClickActivityLogToggle();
        await app.WaitForAsync(() => Shown(app, "ActivityLog") is not null && app.ActivityLog().Rows.Count == 1, "the activity log with its one row");
        await ShotInBothThemesAsync(app, "l31a-activity-log", assertEach: (picture, palette) => AssertActivityLog(app, picture, palette));
        app.ClickActivityLogToggle();
        await app.WaitForAsync(() => Shown(app, "ActivityLog") is null, "the activity log to close");
        AssertGraph(app, PartL.Ops0, PartL.Ops0Lanes);

        // Step 2: Local branches › feature opened; its alpha's menu; Delete feature/alpha…: the question.
        app.Click(await app.RefRowAsync("Local branches", "feature"));
        await app.WaitForAsync(() => app.RefRowModel("Local branches", "feature") is RefItemRow { IsOpen: true }, "Local branches › feature to open");
        app.RightClick(await app.RefRowAsync("Local branches", "feature", "alpha"));
        var menu = app.Menu();
        AssertMenu(menu, [.. PartL.AlphaMenu]);
        Assert.Equal(PartL.AlphaMenuHeight, menu.Bounds.Height, PartL.Tolerance);
        Assert.Equal(-1, repository.SelectedIndex);
        app.ClickMenuItem(PartL.DeleteAlpha);
        await AssertConfirmationAsync(app, PartL.DeleteBranchTitle, PartL.DeleteAlphaQuestion, PartL.BackupFirst, PartL.Delete, destructive: true);
        await ShotInBothThemesAsync(app, "l31b-delete-question", assertEach: (picture, palette) =>
        {
            AssertCard(app, picture, palette, PartL.CardWidth, PartL.CardLeft);
            Assert.Equal(-1, repository.SelectedIndex);
            Assert.True(InDialog<Button>(app, "CancelButton").IsFocused, "Cancel does not have the keyboard focus.");
            var delete = InDialog<Button>(app, "ConfirmButton");
            Assert.True(delete.Classes.Contains("danger"), "Delete is not the danger button.");
            var bounds = app.BoundsOf(delete);
            AssertColour(palette.Danger, picture, new Point(bounds.Left + 4, bounds.Center.Y), "Delete's background (VcDangerBrush, 11.10)");
            var label = delete.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(text => text.IsEffectivelyVisible);
            var area = label is null ? bounds.Deflate(new Thickness(8, 4)) : app.BoundsOf(label);
            AssertInk(picture, area, palette.DangerText, palette.Danger, "Delete's label (VcDangerTextBrush, 11.11)");
            Assert.Equal(palette.DangerText, (delete.Foreground as ISolidColorBrush)?.Color);
            var variant = IsDark(palette) ? ThemeVariant.Dark : ThemeVariant.Light;
            Assert.True(Application.Current!.TryGetResource("VcDangerTextBrush", variant, out var resource) && resource is ISolidColorBrush brush && brush.Color == Colors.White, $"VcDangerTextBrush is not #FFFFFF in the {variant} theme.");
            AssertBackdrop(picture, palette);
        });
        app.Click(InDialog<Button>(app, "CancelButton"));
        await AssertNoDialogAsync(app);
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Empty(await repo.BackupRefsAsync());
        Assert.Equal(UndoRedoButtons.Nothing, app.UndoRedoState());

        // Step 3: the pointer to the neutral point; Ctrl+Shift+P: the palette.
        app.MoveToNeutral();
        mark = app.LogMark();
        app.PressPalette();
        await app.WaitForAsync(() => app.IsPaletteOpen && app.TopDialogView is VisualCommit.App.Views.Commands.CommandPaletteView, "the command palette");
        await app.WaitForAsync(() => app.PaletteBox.IsFocused, "the palette's box to have the keyboard focus");
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartL.PaletteOpened), mark));
        await ShotInBothThemesAsync(app, "l31c-palette", assertEach: (picture, palette) => AssertPalette(app, picture, palette));
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => !app.IsPaletteOpen, "the palette to close");
        Assert.Equal(exact, await repo.ExactStateAsync());

        // Step 4: SettingsButton: the settings screen on General, System checked.
        var settingsFile = File.ReadAllText(Path.Combine(data.Path, "settings.json"));
        calls = app.GitCalls().Count;
        await app.OpenSettingsAsync(SettingsOpening.Button);
        await app.WaitForAsync(() => app.FindByAutomationId("SettingsNavGeneral").IsFocused, "General to have the keyboard focus");
        await ShotInBothThemesAsync(app, "l31d-settings-system", assertEach: (picture, palette) => AssertGeneral(app, picture, palette, theme: AppTheme.System));

        // Step 5: Git, read on All repositories (global); Choose…: the Pick dialog stacked on it.
        app.ChooseSection(SettingsSection.Git);
        await app.WaitForSettingsReadsAsync();
        AssertGitGlobal(app, repo, data);
        app.Click(app.FindByAutomationId("MergeToolChooseButton"));
        await app.WaitForDialogAsync<PickDialogViewModel>();
        await WaitForDialogFocusAsync(app, "PickFilter");
        await ShotInBothThemesAsync(app, "l31e-settings-git", assertEach: (picture, palette) => AssertPick(app, picture, palette));
        app.PressKey(Key.Escape);
        await app.WaitForAsync(() => app.Session.Services.DialogHost.Current is SettingsDialogViewModel, "the Pick dialog to close");
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(settingsFile, File.ReadAllText(Path.Combine(data.Path, "settings.json")));
        Assert.False(File.Exists(ShellDriver.GlobalGitConfigPath(data.Path)), "The session's global git configuration was written.");
        Assert.All(app.GitCalls().Skip(calls), call => Assert.True(IsGitRead(call.CommandText), $"The settings screen made the git call \"{call.CommandText}\", which writes."));

        // Step 6: the pointer rests on Fetch: its tooltip with its key.
        var fetchButton = ToolbarButton(app, "FetchButton");
        Assert.Equal(PartL.FetchToolTip, await app.RestPointerOnAsync(fetchButton));
        Assert.Equal(PartL.FetchToolTip, AutomationProperties.GetHelpText(fetchButton));
        Assert.Equal(PartL.FetchKey, ShellDriver.AcceleratorKeyOf(fetchButton));
        await ShotInBothThemesAsync(app, "l31f-enabled-tooltip", keepPointer: true, assertEach: (picture, palette) => AssertToolTipInside(app, PartL.FetchToolTip));

        // Step 7: Ctrl+, ; Light; Save: the window light while the fake stays dark; Ctrl+, again.
        app.MoveToNeutral();
        mark = app.LogMark();
        await app.OpenSettingsAsync(SettingsOpening.Key);
        app.Click(app.FindByAutomationId("SettingsThemeLight"));
        Assert.True(await app.SaveSettingsAsync(), "The settings screen did not close on Save.");
        await app.WaitForAsync(() => !app.ShowsDarkTheme, "the window to show the light theme");
        Assert.Equal("Light", SettingsJson(data).GetProperty("theme").GetString());
        Assert.Equal(1, app.CountLogLines(AppLogFile.Containing(PartL.ThemeChangedToLight), mark));
        Assert.True(app.SystemTheme.IsDark, "The fake system is no longer dark.");
        AssertThemeSwitch(app, "IconMoon", PartL.SwitchToDark);
        app.MoveToNeutral();
        await app.OpenSettingsAsync(SettingsOpening.Key);
        var single = Shot(app, PartL.SettingsLight);
        AssertGeneral(app, single, Light, theme: AppTheme.Light);
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal(exact, await repo.ExactStateAsync());
        Assert.Empty(await repo.BackupRefsAsync());
    }

    /// <summary>
    /// Check L31, part B (steps 8 to 10) on "auth" at part L's server (47394): the sign-in prompt of
    /// <c>Fetch origin</c>; the status bar while the fetch is held at 60 % with the toolbar's Fetch
    /// waiting; then Cancel, the waiting Fetch's prompt cancelled. Refs unchanged, no lock file.
    /// System with the fake dark, 1100×700.
    /// </summary>
    [AvaloniaFact]
    public async Task Check_L31_sign_in_and_held_fetch_in_both_themes()
    {
        await using var server = await AuthRemoteServer.StartForVisualChecksAsync(port: AuthRemoteServer.Phase6LookVisualChecksPort);
        server.RequireAuth = true;
        server.Credentials = new System.Net.NetworkCredential("alice", "s3cret");
        using var repo = await Scenarios.AuthRemoteAsync(server);
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, PartL.Size.Width, PartL.Size.Height, AppTheme.System, systemDark: true);
        AssertSystemDarkStart(app);
        var port = server.Port;
        var refs = await RefsAsync(repo);

        // Step 8: the hold at 60; Fetch origin from the remote's menu: the sign-in prompt.
        var held = server.HoldNextFetchPack(PartL.HoldPercent);
        app.RightClick(await app.RefRowAsync("Remotes", "origin"));
        app.Menu();
        app.ClickMenuItem(PartL.FetchOrigin);
        await app.WaitForDialogAsync<CredentialsViewModel>(NetworkTimeout);
        await WaitForDialogFocusAsync(app, "CredentialUsername");
        await ShotInBothThemesAsync(app, "l31g-credentials", assertEach: (picture, palette) =>
        {
            AssertCard(app, picture, palette, PartL.CardWidth, PartL.CardLeft);
            Assert.Equal(PartL.WithLookPort(PartL.CredentialsTitle, port), DialogText(app, "DialogTitle"));
            Assert.Equal(PartL.WithLookPort(PartL.CredentialsText, port), DialogText(app, "CredentialsText"));
            Assert.Null(Shown(app.TopDialogView!, "DialogRepository"));
            var texts = TextsIn(app.TopDialogView!);
            Assert.True(texts.IndexOf(PartL.UserName) >= 0 && texts.IndexOf(PartL.Password) > texts.IndexOf(PartL.UserName), "User name and Password are not shown above their boxes.");
            var user = InDialog<TextBox>(app, "CredentialUsername");
            Assert.True(user.IsFocused && string.IsNullOrEmpty(user.Text), "The user name box is not empty with the keyboard focus.");
            Assert.True(string.IsNullOrEmpty(InDialog<TextBox>(app, "CredentialPassword").Text), "The password box is not empty.");
            Assert.Equal(["Cancel", PartL.SignIn], ButtonsOf(app, app.TopDialogView!));
            Assert.False(InDialog<Button>(app, "ConfirmButton").IsEffectivelyEnabled, "Sign in is enabled with an empty user name.");
            Assert.Equal(PartL.FetchingFromOrigin, app.Session.Shell.ActiveTab.OperationStatus.Text);
            Assert.True(app.FindByAutomationId("OperationProgress").IsEffectivelyVisible && app.FindByAutomationId("OperationCancelButton").IsEffectivelyVisible, "The status bar shows no bar or Cancel behind the prompt.");
            var request = Assert.Single(server.Requests);
            Assert.Equal(("GET", "/remote.git/info/refs", "service=git-upload-pack", (string?)null, 401), (request.Method, request.Path, request.Query, request.UserName, request.Status));
            AssertBackdrop(picture, palette);
        });

        // Step 9: alice and s3cret; Sign in; held at 60 %; the toolbar's Fetch waits.
        app.TypeInto("CredentialUsername", "alice");
        app.TypeInto("CredentialPassword", "s3cret");
        app.Click(InDialog<Button>(app, "ConfirmButton"));
        await app.WaitForAsync(
            () => held.IsCompletedSuccessfully && app.Session.Shell.ActiveTab.OperationStatus.Text == PartL.HeldAt60,
            $"the fetch to be held at \"{PartL.HeldAt60}\"",
            NetworkTimeout);
        app.Click(ToolbarButton(app, "FetchButton"));
        await app.WaitForAsync(() => app.Find<TextBlock>("OperationWaiting") is { IsEffectivelyVisible: true, Text: PartL.OneWaiting }, "OperationWaiting to read 1 waiting");
        await AssertNoDialogAsync(app, waitForIdle: false);
        await ShotInBothThemesAsync(app, "l31h-operation-held", assertEach: (picture, palette) =>
        {
            var status = app.Session.Shell.ActiveTab.OperationStatus;
            Assert.Equal((PartL.HeldAt60, 1), (status.Text, status.Waiting));
            var text = (TextBlock)app.FindByAutomationId("OperationStatus");
            AssertInk(picture, app.BoundsOf(text), palette.TextSecondary, palette.Chrome, "the held fetch's text (VcTextSecondaryBrush)");
            AssertColour(palette.Chrome, picture, ShellDriver.NeutralPoint, "the status bar (VcChromeBackgroundBrush)");
            var bar = (ProgressBar)app.FindByAutomationId("OperationProgress");
            Assert.False(bar.IsIndeterminate, "The bar is indeterminate while held at 60 %.");
            Assert.Equal(PartL.HoldPercent, (int)Math.Round(bar.Value));
            var cancel = (Button)app.FindByAutomationId("OperationCancelButton");
            Assert.True(cancel.IsEffectivelyVisible, "Cancel does not show.");
            Assert.Equal(PartL.StopGit, ToolTip.GetTip(cancel) as string);
            Assert.Equal(PartL.OneWaiting, app.Find<TextBlock>("OperationWaiting").Text);
            var requests = server.Requests;
            Assert.Equal(4, requests.Count);
            Assert.Equal(("GET", "/remote.git/info/refs", "alice", 200), (requests[1].Method, requests[1].Path, requests[1].UserName, requests[1].Status));
            Assert.Equal(("POST", "/remote.git/git-upload-pack", "ls-refs", "alice"), (requests[2].Method, requests[2].Path, requests[2].Command, requests[2].UserName));
            Assert.Equal(("POST", "/remote.git/git-upload-pack", "fetch", "alice", true), (requests[3].Method, requests[3].Path, requests[3].Command, requests[3].UserName, requests[3].Held));
        });
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Empty(LockFiles(repo));

        // Step 10: Cancel; the waiting Fetch runs and asks again; its Cancel.
        using (var recorder = app.RecordStatusTexts())
        {
            var mark = app.LogMark();
            app.Click(app.FindByAutomationId("OperationCancelButton"));
            await app.WaitForLogLineAsync(new Regex(@"\] auth: Fetch origin: Cancelled after ", RegexOptions.CultureInvariant), mark: mark, timeout: NetworkTimeout);
            await app.WaitForDialogAsync<CredentialsViewModel>(NetworkTimeout);
            app.Click(InDialog<Button>(app, "CancelButton"));
            await app.WaitForIdleAsync(timeout: NetworkTimeout);
            server.Release();
            var texts = recorder.Texts.ToList();
            var cancelled = texts.IndexOf(PartL.FetchOriginCancelled);
            Assert.True(cancelled >= 0 && texts.IndexOf(PartL.FetchingFromAll, cancelled) > cancelled, "The status bar did not show \"Fetch origin cancelled\" before \"Fetching from all remotes…\": " + string.Join(" | ", texts));
            Assert.Equal(PartL.FetchCancelled, texts[^1]);
        }

        await AssertNoDialogAsync(app);
        AssertStatus(app, PartL.FetchCancelled);
        Assert.Equal(refs, await RefsAsync(repo));
        Assert.Equal(PartL.AuthStatus, (await repo.GitAsync("status", "-sb")).StandardOutput.Split('\n')[0].TrimEnd('\r'));
        Assert.Empty(LockFiles(repo));
        AssertNotWritten(app, "s3cret");
    }

    // ---- L30's dashboard ------------------------------------------------------------------------

    /// <summary>
    /// L30 step 1's dashboard in the variant shown (2.5, 2.6, scenario facts 2.3): the one tab, the
    /// dashboard across the window, its header, headings and ten rows as the report writes them, their
    /// tones in the picture, the toolbar and the status bar of a dashboard tab.
    /// </summary>
    private static void AssertDashboardLook(ShellDriver app, Screenshot picture, Palette palette, string oldSite)
    {
        var strip = app.TabStrip();
        var tab = Assert.Single(strip.Tabs);
        Assert.Equal((PartL.DashboardTabId, PartL.Workspace, true), (tab.AutomationId, tab.Title, tab.IsActive));
        Assert.Equal(palette.TextPrimary, (tab.IconForeground as ISolidColorBrush)?.Color);
        var dashboard = app.BoundsOf((Control)app.FindByAutomationId("Dashboard"));
        Assert.True(Math.Abs(dashboard.Left) <= 0.5 && Math.Abs(dashboard.Right - PartL.Size.Width) <= 0.5, $"The dashboard is at {dashboard}, not across the window.");
        Assert.False(app.Find<GridSplitter>("LeftSplitter").IsEffectivelyVisible, "The left splitter shows on a dashboard tab.");
        var header = (Border)app.FindByAutomationId("DashboardHeader");
        var headerBounds = app.BoundsOf(header);
        Assert.True(Math.Abs(headerBounds.Top - 88) <= PartL.Tolerance && Math.Abs(headerBounds.Bottom - 124) <= PartL.Tolerance, $"The header is at {headerBounds}, not y 88 to 124.");
        Assert.Equal(palette.Chrome, (header.Background as ISolidColorBrush)?.Color);
        AssertColour(palette.Chrome, picture, new Point(600, 92), "the header's background (VcChromeBackgroundBrush)");
        var icon = header.GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>().First(candidate => candidate.IsEffectivelyVisible);
        Assert.True(ReferenceEquals(Application.Current!.FindResource("IconWorkspace"), icon.Data), "The header's icon is not IconWorkspace.");
        Assert.True(Math.Abs(app.BoundsOf(icon).Left - 8) <= PartL.Tolerance && Math.Abs(app.BoundsOf(icon).Width - 14) <= 0.5, $"The header's icon is at {app.BoundsOf(icon)}, not 14 wide from x 8.");
        var title = (TextBlock)app.FindByAutomationId("DashboardTitle");
        Assert.Equal((13d, FontWeight.SemiBold), (title.FontSize, title.FontWeight));
        var buttons = new[] { "DashboardAddRepository", "DashboardFetchAll", "DashboardPullAll", "DashboardRefresh", "DashboardMore" }
            .Select(id => app.BoundsOf((Control)app.FindByAutomationId(id))).ToList();
        Assert.True(Math.Abs(buttons[^1].Right - 1092) <= PartL.Tolerance, $"DashboardMore ends at x {buttons[^1].Right}, not 1092.");
        for (var index = 1; index < buttons.Count; index++)
        {
            Assert.True(Math.Abs(buttons[index].Left - buttons[index - 1].Right - 6) <= PartL.Tolerance, $"The header's buttons {index} and {index + 1} are {buttons[index].Left - buttons[index - 1].Right} apart, not 6.");
        }

        AssertDashboardHeader(app, ExpectedDashboardHeader.Idle(PartL.Workspace, PartL.Count(10)));
        var headings = (Control)app.FindByAutomationId("DashboardColumnHeaders");
        var headingBounds = app.BoundsOf(headings);
        Assert.True(Math.Abs(headingBounds.Top - 124) <= PartL.Tolerance && Math.Abs(headingBounds.Bottom - 152) <= PartL.Tolerance, $"The headings are at {headingBounds}, not y 124 to 152.");
        Assert.Equal(PartL.Headings, headings.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && text.Bounds.Width > 0).OrderBy(text => app.BoundsOf(text).X).Select(text => text.Text));

        // The ten rows as scenario facts 2.3 give them, each in its place.
        var snapshot = app.DashboardSnapshot();
        var expected = PartL.DashboardRows(oldSite);
        Assert.Equal(expected.Select(row => row.Name), snapshot.Rows.Select(row => row.Name));
        foreach (var (row, index) in expected.Select((row, index) => (row, index)))
        {
            AssertRow(app, row.Name, new ExpectedRow(row.Branch, Cell(row.AheadBehind), Cell(row.Changes), Cell(row.State), Cell(row.Operation)), snapshot);
            var bounds = app.BoundsOf(app.DashboardRowControl(row.Name));
            Assert.True(Math.Abs(bounds.Center.Y - PartL.DashboardRowCentre(index)) <= PartL.Tolerance && Math.Abs(bounds.Height - 26) <= PartL.Tolerance, $"The row {row.Name} is at {bounds}, not centred at y {PartL.DashboardRowCentre(index)}, 26 high.");
            Assert.False(snapshot.Rows[index].IsReading, $"The row {row.Name} still reads.");
        }

        // "The repository names contain …": the ten names' ink together (the Repository column of the
        // ten rows); each name's own brush is AssertRow's. A three-letter name alone ("api") can lack
        // a pixel whose red subpixel is fully covered in the light picture.
        var names = expected.Select(row => CellText(app, row.Name, row.Name)).Aggregate((a, b) => a.Union(b));
        AssertInk(picture, names, palette.TextPrimary, palette.Window, "the repository names (VcTextPrimaryBrush)");

        AssertInk(picture, CellText(app, "billing", "1 conflict"), palette.Danger, palette.Window, "billing's 1 conflict (VcDangerBrush, 11.10)");
        AssertInk(picture, CellText(app, "billing", "Merge in progress"), palette.Warning, palette.Window, "billing's Merge in progress (VcWarningBrush, 11.10)");
        AssertInk(picture, CellText(app, "old-site", "Folder not found"), palette.Warning, palette.Window, "old-site's Folder not found (VcWarningBrush)");
        AssertInk(picture, CellText(app, "api", "Up to date"), palette.TextSecondary, palette.Window, "api's Up to date (VcTextSecondaryBrush)");
        AssertInk(picture, CellText(app, "scratch", "No upstream"), palette.TextSecondary, palette.Window, "scratch's No upstream");
        AssertInk(picture, CellText(app, "notes", "No remote"), palette.TextSecondary, palette.Window, "notes' No remote");
        foreach (var arrow in app.DashboardRowControl("mobile").GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>().Where(candidate => candidate.IsEffectivelyVisible))
        {
            Assert.Equal((10d, 10d), (arrow.Bounds.Width, arrow.Bounds.Height));
            AssertIconColour(app, picture, arrow, palette.TextSecondary, "mobile's arrow (VcTextSecondaryBrush)");
        }

        AssertColour(palette.Window, picture, new Point(1090, 500), "the list's background (VcWindowBackgroundBrush)");

        // The toolbar and the status bar of a dashboard tab (4.10, 2.8).
        AssertButton(app, "FetchButton", true, PartL.FetchAllToolTip);
        AssertButton(app, "PullButton", true, PartL.PullAllToolTip);
        foreach (var id in PartL.DisabledOnDashboard)
        {
            AssertButton(app, id, false, null);
        }

        AssertButton(app, "SettingsButton", true, PartL.SettingsToolTip);
        Assert.True(ToolbarButton(app, "ThemeSwitch").IsEffectivelyEnabled, "Theme is disabled.");
        Assert.True(app.FindByAutomationId("StatusWorkspaceIcon").IsEffectivelyVisible, "StatusWorkspaceIcon does not show.");
        AssertBranch(app, PartL.Workspace);
        AssertStatus(app, PartL.Ready);
    }

    /// <summary>A cell of PartL as the support's <see cref="ExpectedCell"/>.</summary>
    private static ExpectedCell Cell(PartL.Cell cell) =>
        cell.IsEmpty && cell.ToolTip is null
            ? ExpectedCell.Empty
            : new ExpectedCell(cell.Text, cell.Tone switch
            {
                PartL.Primary => DashboardTone.Primary,
                PartL.Warning => DashboardTone.Warning,
                PartL.Danger => DashboardTone.Danger,
                _ => DashboardTone.Secondary,
            }, cell.ToolTip, cell.Name);

    /// <summary>The bounds of the text <paramref name="text"/> as laid out in the row <paramref name="row"/>.</summary>
    private static Rect CellText(ShellDriver app, string row, string text)
    {
        var block = app.DashboardRowControl(row).GetVisualDescendants().OfType<TextBlock>().First(candidate => candidate.IsEffectivelyVisible && candidate.Text == text);
        var bounds = app.BoundsOf(block);
        return new Rect(bounds.X, bounds.Y, Math.Min(block.TextLayout.Width, bounds.Width), bounds.Height);
    }

    /// <summary>
    /// 13.2's rule for a folder drawn by <c>PathTextBlock</c> (L30 step 3, L32): every drawn line but
    /// the last ends with a separator or a space (a line that ends one of the text's own lines
    /// excepted, LD20); no line ends with <c>C:</c> or <c>'C:</c>; no folder name of
    /// <paramref name="folder"/> is split across lines; the lines joined (the text's own line feeds
    /// between them) equal the text.
    /// </summary>
    private static void AssertPathLines(IReadOnlyList<DrawnLine> lines, string text, string folder, string what)
    {
        for (var index = 0; index < lines.Count - 1; index++)
        {
            var line = lines[index];
            Assert.True(
                line.EndsWithLineBreak || line.Text.EndsWith('\\') || line.Text.EndsWith('/') || line.Text.EndsWith(' '),
                $"{what}'s line \"{line.Text}\" ends inside a folder's name (13.2).");
        }

        foreach (var line in lines)
        {
            var trimmed = line.Text.TrimEnd();
            Assert.False(trimmed.EndsWith("C:", StringComparison.OrdinalIgnoreCase) || trimmed.EndsWith("'C:", StringComparison.OrdinalIgnoreCase), $"{what}'s line \"{line.Text}\" ends after the drive (13.2).");
        }

        foreach (var name in folder.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Where(name => name.Length > 2))
        {
            Assert.True(lines.Any(line => line.Text.Contains(name, StringComparison.Ordinal)), $"{what}'s folder name \"{name}\" is split across its lines: {string.Join(" | ", lines.Select(line => line.Text))}.");
        }

        Assert.Equal(text, string.Concat(lines.Select(line => line.Text + (line.EndsWithLineBreak ? "\n" : string.Empty))).TrimEnd('\n'));
    }

    // ---- L31's screens -----------------------------------------------------------------------

    /// <summary>
    /// L31 step 1's activity log in the variant shown (phase 5's part A): the panel, the splitter
    /// and the graph area above it; its header; the one Fetch row; the toggle selected with its tooltip.
    /// </summary>
    private static void AssertActivityLog(ShellDriver app, Screenshot picture, Palette palette)
    {
        var log = app.ActivityLog();
        Assert.True(Math.Abs(log.Bounds.Top - PartL.LogTop) <= 0.5 && Math.Abs(log.Bounds.Bottom - 674) <= 0.5, $"The activity log is at {log.Bounds}, not y 474 to 674.");
        var splitter = app.BoundsOf((Control)app.FindByAutomationId("ActivityLogSplitter"));
        Assert.True(Math.Abs(splitter.Top - PartL.SplitterTop) <= 0.5 && Math.Abs(splitter.Bottom - PartL.LogTop) <= 0.5, $"The splitter is at {splitter}, not y 469 to 474.");
        Assert.True(Graph(app).Bounds.Height > 0 && app.BoundsOf(Graph(app)).Bottom <= PartL.SplitterTop + 0.5, "The graph area does not end above the splitter.");
        Assert.True(Graph(app).RowBounds(PartL.Ops0.Count - 1).Bottom + app.BoundsOf(Graph(app)).Top <= PartL.SplitterTop + 0.5, "OPS0's 8 rows are not whole above the activity log.");
        Assert.Equal((PartL.ActivityLog, "ops", false), (log.Title, log.RepositoryName, log.ShowReads));
        Assert.Equal(palette.TextSecondary, log.RepositoryNameColor);
        var row = Assert.Single(log.Rows);
        Assert.Equal((ActivityLogRowKind.Operation, "IconCheck", "09:00:00", "Fetch", PartL.FetchedFromAll, "0 ms", false, "IconChevronRight"), (row.Kind, row.Glyph, row.Time, row.Title, row.Message, row.Duration, row.IsOpen, row.Chevron));
        Assert.Equal(palette.Success, row.GlyphColor);
        Assert.Equal(palette.TextSecondary, row.MessageColor);
        var glyph = app.ActivityLogView.GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>()
            .First(icon => icon.IsEffectivelyVisible && ReferenceEquals(icon.Data, Application.Current!.FindResource("IconCheck")));
        AssertIconColour(app, picture, glyph, palette.Success, "the Fetch row's IconCheck (VcSuccessBrush, 11.10)");
        var toggle = (Button)app.FindByAutomationId("ActivityLogToggle");
        Assert.True(toggle.Classes.Contains("selected"), "ActivityLogToggle is not selected.");
        Assert.Equal(PartL.HideActivityLog, ToolTip.GetTip(toggle) as string);
        Assert.Equal(PartL.HideActivityLog, AutomationProperties.GetHelpText(toggle));
        AssertColour(palette.Control, picture, PartL.ToggleSample, "ActivityLogToggle's selected background (VcControlBackgroundBrush)");
    }

    /// <summary>
    /// L31 step 3's palette in the variant shown (5.3, 5.4, 5.7): no backdrop; the card and the box
    /// at 5.3's places; the box empty with its placeholder; the first 12 commands of "ops" with
    /// their keys, reasons and colours, the first highlighted; the rest further down the list.
    /// </summary>
    private static void AssertPalette(ShellDriver app, Screenshot picture, Palette palette)
    {
        var shown = app.PaletteSnapshot();
        AssertColour(palette.Window, picture, PartL.BackdropSample, "the graph area behind the palette (no backdrop, 5.2)");
        Assert.True(Near(shown.CardBounds, PartL.PaletteCard), $"The palette's card is at {shown.CardBounds}, not {PartL.PaletteCard}.");
        Assert.True(Near(shown.BoxBounds, PartL.PaletteBox), $"The palette's box is at {shown.BoxBounds}, not {PartL.PaletteBox}.");
        var card = (Border)app.FindByAutomationId(app.PaletteView, "CommandPaletteCard");
        Assert.Equal((palette.Panel, palette.Border, new Thickness(1), new CornerRadius(8)), ((card.Background as ISolidColorBrush)?.Color, (card.BorderBrush as ISolidColorBrush)?.Color, card.BorderThickness, card.CornerRadius));
        AssertColour(palette.Panel, picture, new Point(shown.CardBounds.Right - 6, shown.CardBounds.Bottom - 4), "the palette card's background (VcPanelBackgroundBrush)");
        Assert.Equal(string.Empty, shown.Query);
        Assert.True(app.PaletteBox.IsFocused, "The palette's box does not have the keyboard focus.");
        Assert.Equal(PartL.PalettePlaceholder, app.PaletteBox.PlaceholderText);
        var rows = shown.Rows.Where(row => row.Bounds.Bottom <= shown.CardBounds.Bottom + 0.5).ToList();
        Assert.Equal(PartL.PaletteFirst12.Count, rows.Count);
        foreach (var (row, (entry, index)) in rows.Zip(PartL.PaletteFirst12.Select((entry, index) => (entry, index))))
        {
            Assert.Equal((entry.Label, entry.Enabled, entry.Reason, entry.Key), (row.Label, row.IsEnabled, row.Reason, row.KeyText));
            Assert.True(row.Reason is not null || row.Detail is null, $"{row.Label} shows the detail \"{row.Detail}\".");
            Assert.Equal(index == 0, row.IsHighlighted);
            Assert.Equal(PartL.PaletteRowCentre(index), row.Bounds.Center.Y, PartL.Tolerance);
            var background = index == 0 ? palette.Selection : palette.Panel;
            var item = app.PaletteView.GetVisualDescendants().OfType<ListBoxItem>().Single(candidate => candidate.IsEffectivelyVisible && app.BoundsOf(candidate) == row.Bounds);
            var blocks = item.GetVisualDescendants().OfType<TextBlock>().ToList();
            var label = blocks.First(block => block.Name == "CommandPaletteItemLabel");
            AssertInk(picture, TextBounds(app, label), entry.Enabled ? palette.TextPrimary : palette.TextDisabled, background, $"{entry.Label}'s label");
            if (entry.Key is not null)
            {
                var key = blocks.First(block => block.Name == "CommandPaletteItemKey");
                AssertInk(picture, app.BoundsOf(key), entry.Enabled ? palette.TextSecondary : palette.TextDisabled, background, $"{entry.Label}'s key");
            }

            if (entry.Reason is not null)
            {
                var reason = blocks.First(block => block.Name == "CommandPaletteItemDetail");
                AssertInk(picture, TextBounds(app, reason), palette.TextDisabled, background, $"{entry.Label}'s reason");
            }
        }

        AssertColour(palette.Selection, picture, new Point(rows[0].Bounds.Left + 4, rows[0].Bounds.Center.Y), "the highlighted row's background (VcSelectionBrush)");
        var all = app.Palette.Shown.Select(entry => entry.Label).ToList();
        Assert.Equal(PartL.PaletteFirst12.Select(entry => entry.Label), all.Take(PartL.PaletteFirst12.Count));
        Assert.Equal(PartL.PaletteThirteenth, all[PartL.PaletteFirst12.Count]);
        Assert.Contains(PartL.UseTheDarkTheme, all);
        Assert.Contains(PartL.UseTheLightTheme, all);
        Assert.DoesNotContain(PartL.FollowTheSystem, all);
    }

    /// <summary>
    /// L31 steps 4 and 7's settings screen on General (7.1, 7.3): the card at 7.1's place, its
    /// title and sections, General's fields with <paramref name="theme"/> checked, the buttons, the colours.
    /// </summary>
    private static void AssertGeneral(ShellDriver app, Screenshot picture, Palette palette, AppTheme theme)
    {
        var settings = app.SettingsSnapshot();
        Assert.Equal(SettingsSection.General, settings.Section);
        var view = app.TopDialogView ?? throw new Xunit.Sdk.XunitException("The settings screen is not open.");
        var card = view.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("dialogCard") && border.IsEffectivelyVisible);
        Assert.True(Near(app.BoundsOf(card), PartL.SettingsCard), $"The settings card is at {app.BoundsOf(card)}, not {PartL.SettingsCard}.");
        AssertColour(palette.Panel, picture, app.BoundsOf(card).TopLeft + new Point(10, 10), "the settings card's background (VcPanelBackgroundBrush)");
        Assert.Equal("Settings", settings.Field("SettingsTitle")?.Text);
        var nav = app.BoundsOf(app.FindByAutomationId("SettingsNav"));
        Assert.True(Math.Abs(nav.Left - PartL.SettingsNavLeft) <= PartL.Tolerance && Math.Abs(nav.Right - PartL.SettingsNavRight) <= PartL.Tolerance, $"SettingsNav is at {nav}, not x 140 to 316.");
        Assert.Equal(PartL.SettingsSections, new[] { "SettingsNavGeneral", "SettingsNavGit", "SettingsNavTools", "SettingsNavShortcuts" }.Select(id => settings.Field(id)?.Text));
        Assert.True(app.FindByAutomationId("SettingsNavGeneral").Classes.Contains("selected"), "General is not selected.");
        Assert.False(settings.Shows("SettingsNavError"), "A section shows an error dot.");
        Assert.Equal(
            (false, theme == AppTheme.Light, theme == AppTheme.System),
            (settings.Field("SettingsThemeDark")?.IsChecked == true, settings.Field("SettingsThemeLight")?.IsChecked == true, settings.Field("SettingsThemeSystem")?.IsChecked == true));
        Assert.Equal((PartL.FetchEvery, true), (settings.Field("AutoFetchCheckBox")?.Text, settings.Field("AutoFetchCheckBox")?.IsChecked == true));
        Assert.Equal("5", settings.Field("AutoFetchMinutesBox")?.Text);
        Assert.False(settings.Shows("AutoFetchError"), "AutoFetchError shows.");
        Assert.Equal((PartL.KeepIgnoredFiles, true), (settings.Field("KeepIgnoredCheckBox")?.Text, settings.Field("KeepIgnoredCheckBox")?.IsChecked == true));
        Assert.Equal((PartL.IncludeUntracked, true), (settings.Field("StashUntrackedCheckBox")?.Text, settings.Field("StashUntrackedCheckBox")?.IsChecked == true));
        Assert.Equal(PartL.RunnableIntro, settings.Field("RunnableIntro")?.Text);
        Assert.Equal(PartL.RunnableList, AutomationProperties.GetName(app.FindByAutomationId("RunnableList")));
        Assert.Equal(OperatingSystem.IsWindows() ? PartL.RunnablePathExt : null, settings.Field("RunnablePathExt")?.Text);
        Assert.Equal((string.Empty, PartL.RunnablePlaceholder), (settings.Field("RunnableExtraBox")?.Text ?? string.Empty, settings.Field("RunnableExtraBox")?.Placeholder));
        Assert.False(settings.Shows("RunnableError"), "RunnableError shows.");
        Assert.Equal(PartL.RunnableUnix, settings.Field("RunnableUnix")?.Text);
        var texts = TextsIn(app.FindByAutomationId("SettingsSection"));
        foreach (var note in new[] { PartL.SystemFollows, PartL.OnlyActiveTab, PartL.KeepIgnoredNote, PartL.StashNote, "Also ask for", "minutes" })
        {
            Assert.Contains(note, texts);
        }

        var scroller = (ScrollViewer)app.FindByAutomationId("SettingsSection");
        Assert.True(scroller.Extent.Height <= scroller.Viewport.Height + 0.5, $"General scrolls: {scroller.Extent.Height} of content in {scroller.Viewport.Height}.");
        Assert.Equal(["Cancel", "Save"], new[] { "CancelButton", "ConfirmButton" }.Select(id => settings.Field(id)?.Text));
        var save = (Button)app.FindByAutomationId("ConfirmButton");
        Assert.Equal(palette.Accent, (save.Background as ISolidColorBrush)?.Color);
        AssertColour(palette.Accent, picture, new Point(app.BoundsOf(save).Left + 4, app.BoundsOf(save).Center.Y), "Save's background (VcAccentBrush)");
        var label = view.GetVisualDescendants().OfType<TextBlock>().First(text => text.IsEffectivelyVisible && text.Classes.Contains("settingsLabel") && text.Text == "Theme");
        AssertInk(picture, TextBounds(app, label), palette.TextSecondary, palette.Panel, "General's label Theme (VcTextSecondaryBrush)");
    }

    /// <summary>L31 step 5's Git section on <c>All repositories (global)</c> before the Pick dialog (7.4, D200, D202).</summary>
    private static void AssertGitGlobal(ShellDriver app, TempRepo repo, TempDirectory data)
    {
        var settings = app.SettingsSnapshot();
        Assert.Equal(SettingsSection.Git, settings.Section);
        Assert.False(settings.Shows("GitReading"), "GitReading shows.");
        Assert.True(app.FindByAutomationId("GitScopeGlobal").Classes.Contains("selected"), "All repositories (global) is not selected.");
        Assert.Equal(PartL.AllRepositoriesGlobal, settings.Field("GitScopeGlobal")?.Text);
        var scope = settings.Field("GitScopeRepository");
        Assert.Equal((PartL.ThisRepository, true), (scope?.Text, scope?.IsEnabled == true));
        Assert.StartsWith("ops — ", scope?.ToolTip, StringComparison.Ordinal);
        Assert.True(WorkspacePathsSame(scope!.ToolTip!["ops — ".Length..], repo.Path), $"This repository's tooltip names {scope.ToolTip}, not ops's working tree.");
        var caption = settings.Field("GitScopeCaption");
        Assert.Equal(PartL.GlobalCaption, caption?.Text);
        Assert.True(WorkspacePathsSame(caption!.ToolTip!, ShellDriver.GlobalGitConfigPath(data.Path)), $"GitScopeCaption's tooltip is {caption.ToolTip}, not the session's global file.");
        Assert.Equal(caption.ToolTip, caption.HelpText);
        Assert.Equal((string.Empty, PartL.NotSet), (settings.Field("UserNameBox")?.Text ?? string.Empty, settings.Field("UserNameBox")?.Placeholder));
        Assert.Equal((string.Empty, PartL.NotSet), (settings.Field("UserEmailBox")?.Text ?? string.Empty, settings.Field("UserEmailBox")?.Placeholder));
        foreach (var id in new[] { "UserNameOrigin", "UserNameError", "UserEmailOrigin", "UserEmailError", "UserEmailWarning", "IdentityInEffect", "PullOther", "PullNotices", "MergeToolStatus", "MergeToolNotices" })
        {
            Assert.False(settings.Shows(id), $"{id} shows on the global scope: \"{settings.Field(id)?.Text}\".");
        }

        Assert.Equal(PartL.PullModes, new[] { "PullNotSet", "PullMerge", "PullMergeCommit", "PullRebase", "PullRebaseMerges", "PullFastForwardOnly" }.Select(id => settings.Field(id)?.Text));
        Assert.Equal([true, false, false, false, false, false], new[] { "PullNotSet", "PullMerge", "PullMergeCommit", "PullRebase", "PullRebaseMerges", "PullFastForwardOnly" }.Select(id => settings.Field(id)?.IsChecked == true));
        Assert.Equal(PartL.NotSet, settings.Field("MergeToolValue")?.Text);
        Assert.True(settings.Field("MergeToolChooseButton")?.IsEnabled == true, "Choose… is disabled.");
        Assert.Equal(PartL.MergeToolNote, settings.Field("MergeToolNote")?.Text);
    }

    /// <summary>
    /// L31 step 5's Pick dialog stacked over the settings screen (7.7, 8.10, 12.9): the wide card, its
    /// title, the filter with the keyboard focus, the fixed catalogue's rows with their details, Not
    /// set selected, the buttons, the settings card behind the backdrop.
    /// </summary>
    private static void AssertPick(ShellDriver app, Screenshot picture, Palette palette)
    {
        AssertCard(app, picture, palette, PartL.WideCardWidth, PartL.WideCardLeft);
        Assert.NotNull(app.SettingsDialog);
        Assert.IsType<PickDialogViewModel>(app.Session.Services.DialogHost.Current);
        Assert.Equal(PartL.ChooseAMergeTool, DialogText(app, "PickTitle"));
        var filter = InDialog<TextBox>(app, "PickFilter");
        Assert.True(filter.IsFocused && string.IsNullOrEmpty(filter.Text), "The filter is not empty with the keyboard focus.");
        Assert.Equal(PartL.Filter, filter.PlaceholderText);
        var list = InDialog<ListBox>(app, "PickList");
        var items = list.GetVisualDescendants().OfType<ListBoxItem>().Where(item => item.IsEffectivelyVisible).ToList();
        var rows = items.Select(item => ((string)item.DataContext!, item.GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == "PickItemDetail").Text ?? string.Empty)).ToList();
        Assert.Equal(PartL.MergeTools, rows);
        Assert.Equal(PartL.NotSet, list.SelectedItem as string);
        foreach (var item in items)
        {
            var text = item.GetVisualDescendants().OfType<TextBlock>().First(block => block.Name != "PickItemDetail");
            var detail = item.GetVisualDescendants().OfType<TextBlock>().First(block => block.Name == "PickItemDetail");
            Assert.Equal(palette.TextSecondary, (detail.Foreground as ISolidColorBrush)?.Color);
            Assert.Equal(12, app.BoundsOf(detail).Left - (app.BoundsOf(text).Left + text.TextLayout.Width), PartL.Tolerance);
        }

        var lastDetail = items[^1].GetVisualDescendants().OfType<TextBlock>().First(block => block.Name == "PickItemDetail");
        AssertInk(picture, TextBounds(app, lastDetail), palette.TextSecondary, palette.Panel, "winmerge's detail (VcTextSecondaryBrush)");
        Assert.Equal(["Cancel", PartL.Choose], ButtonsOf(app, app.TopDialogView!));
    }

    // ---- Small helpers -----------------------------------------------------------------------

    /// <summary>Two rectangles alike within the report's ±2.</summary>
    private static bool Near(Rect actual, Rect expected) =>
        Math.Abs(actual.X - expected.X) <= PartL.Tolerance && Math.Abs(actual.Y - expected.Y) <= PartL.Tolerance
            && Math.Abs(actual.Width - expected.Width) <= PartL.Tolerance && Math.Abs(actual.Height - expected.Height) <= PartL.Tolerance;

    /// <summary>A text block's text as laid out (its width the text's, not the block's).</summary>
    private static Rect TextBounds(ShellDriver app, TextBlock block)
    {
        var bounds = app.BoundsOf(block);
        return new Rect(bounds.X, bounds.Y, Math.Min(block.TextLayout.Width + 1, bounds.Width), bounds.Height);
    }

    /// <summary>Whether two folder paths name one folder.</summary>
    private static bool WorkspacePathsSame(string a, string b) => VisualCommit.Core.Workspaces.WorkspacePaths.Same(a, b);

    /// <summary>Whether a call the settings screen made is a read (<c>git config --get…</c>, <c>--list</c>, <c>--show-origin</c>, …): none of git's writes.</summary>
    private static bool IsGitRead(string command) =>
        !(command.Contains(" config --global --unset", StringComparison.Ordinal)
            || command.Contains(" config --global --replace-all", StringComparison.Ordinal)
            || Regex.IsMatch(command, @" config (--global |--local )?(?!--)[a-z]+\.[a-z.]+ ", RegexOptions.CultureInvariant));

    /// <summary>The <c>*.lock</c> files under a repository's git folder.</summary>
    private static List<string> LockFiles(TempRepo repo) =>
        [.. Directory.EnumerateFiles(Path.Combine(repo.Path, ".git"), "*.lock", SearchOption.AllDirectories)];

    /// <summary>"No dialog" without the wait for idle, for a step whose operation is held on purpose.</summary>
    private static Task AssertNoDialogAsync(ShellDriver app, bool waitForIdle)
    {
        if (waitForIdle)
        {
            return AssertNoDialogAsync(app);
        }

        app.Settle();
        Assert.Null(app.Session.Services.DialogHost.Current);
        return Task.CompletedTask;
    }
}
