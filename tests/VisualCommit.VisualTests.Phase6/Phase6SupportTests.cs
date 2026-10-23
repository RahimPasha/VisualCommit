using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Operations;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core;
using VisualCommit.Core.Session;
using VisualCommit.Core.Settings;
using VisualCommit.Core.Workspaces;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.VisualTests.Phase6.Phase6Expectations;

namespace VisualCommit.VisualTests.Phase6;

/// <summary>
/// The assertions every part of phase 6's walk-through shares (Phase6Checks.Support.cs) and the
/// helpers of Phase6Expectations.Common.cs, tried once; the isolation's tripwire (design 17.2); and
/// the settings scenario opened in the app with the harness's per-session git environment, so that
/// a check that fails points at the app and not at the assertion, the harness or the scenario. No
/// pictures: these are not checks of the report.
/// </summary>
public sealed class Phase6SupportTests : Phase6Checks
{
    [Fact]
    public void Nothing_wrote_a_global_configuration_outside_a_session()
    {
        var findings = GitIsolation.GlobalConfigurationWrittenOutsideASession();
        Assert.True(findings.Count == 0, string.Join('\n', findings));
    }

    [Theory]
    [InlineData("2026-01-02T09:00:00+00:00")]
    [InlineData("2026-01-02T09:00:00Z")]
    public void A_git_date_in_UTC_passes_whichever_way_the_git_version_writes_it(string gitText)
    {
        AssertGitDate(AppCommitTime, gitText);
        AssertGitDate(AppCommitTime, gitText + "\n");
        Assert.ThrowsAny<Exception>(() => AssertGitDate(AppCommitTime, "2026-01-02T09:00:00+01:00"));
    }

    [Fact]
    public async Task The_working_tree_is_the_full_folder_git_names_without_a_trailing_separator()
    {
        using var repo = await TempRepo.CreateAsync();
        var workingTree = await WorkingTreeAsync(repo);

        Assert.Equal(Path.GetFullPath(workingTree), workingTree);
        Assert.False(Path.EndsInDirectorySeparator(workingTree), $"Ends with a separator: {workingTree}");
        var marker = $"same-folder-{Guid.NewGuid():N}";
        File.WriteAllText(Path.Combine(repo.Path, marker), string.Empty);
        Assert.True(File.Exists(Path.Combine(workingTree, marker)), $"{workingTree} is not the folder {repo.Path}.");
    }

    [Fact]
    public void A_backup_refs_name_is_taken_apart()
    {
        var rebase = BackupRef.Parse("refs/visualcommit/backup/rebase-20260102T090000123Z/feature/topic");
        Assert.Equal(("rebase", (int?)null, false, "feature/topic"), (rebase.Kind, rebase.Number, rebase.IsChanges, rebase.Name));
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 9, 0, 0, 123, TimeSpan.Zero), rebase.Time);
        Assert.Equal("rebase-20260102T090000123Z", rebase.Stem);
        Assert.Equal(14, BackupKinds.Count);
        Assert.Throws<FormatException>(() => BackupRef.Parse("refs/heads/main"));
        Assert.Throws<FormatException>(() => BackupRef.Parse("refs/visualcommit/backup/merge-20260102T090000000Z"));
    }

    [Fact]
    public void The_menu_height_is_4as_formula()
    {
        Assert.Equal(232, MenuHeight(7, 2));
        Assert.Equal(84, MenuHeight(2, 0));
    }

    [Theory]
    [InlineData("w1a-welcome-workspaces")]
    [InlineData("w4b-fetch-held")]
    [InlineData("k10a-palette")]
    [InlineData("s15b-system-light")]
    [InlineData("g22a-checkout-blocked")]
    [InlineData("l30c-scan-checklist-dark")]
    [InlineData("l31d-settings-light")]
    [InlineData("15a-numbered")]
    public void A_picture_name_takes_phase_6s_part_letters(string name) => Assert.Matches(PictureName(), name);

    [Theory]
    [InlineData("a1a-toolbar")]
    [InlineData("u16a-undo")]
    [InlineData("c12a-protected")]
    [InlineData("t4a-tab")]
    [InlineData("w4a")]
    [InlineData("W4a-fetch")]
    [InlineData("w4abcd-fetch")]
    [InlineData("w4a-Fetch")]
    [InlineData("w4a--fetch")]
    public void A_name_of_no_part_or_not_of_the_reports_form_is_refused(string name) => Assert.DoesNotMatch(PictureName(), name);

    [Fact]
    public void A_picture_name_is_taken_once_per_run()
    {
        // A name no report gives, so that no check of the run meets it.
        ClaimPictureName("99zz-support-claim");
        Assert.Throws<InvalidOperationException>(() => ClaimPictureName("99zz-support-claim"));
        Assert.Throws<ArgumentException>(() => ClaimPictureName("a1a-toolbar"));
    }

    [Fact]
    public void Phase_6s_colours_are_the_designs()
    {
        Assert.Equal(Avalonia.Media.Color.Parse("#895A17"), Light.Warning);
        Assert.Equal(Avalonia.Media.Color.Parse("#187248"), Light.Success);
        Assert.Equal(Avalonia.Media.Color.Parse("#B7323D"), Light.Danger);
        Assert.Equal(
            ["#FF4353D8", "#FF1C8655", "#FFA16A1B", "#FFC93A46", "#FF1D7EA6", "#FF8048C7", "#FFB54A8C", "#FF59811C"],
            Light.Lanes.Select(colour => colour.ToString().ToUpperInvariant()));
        Assert.Equal(Avalonia.Media.Color.Parse("#E5606B"), Dark.Danger);
        Assert.Equal(Avalonia.Media.Colors.White, Dark.DangerText);
        Assert.Equal(Avalonia.Media.Colors.White, Light.DangerText);
    }

    [Fact]
    public void A_text_with_the_servers_port_follows_a_port_the_server_fell_back_to()
    {
        Assert.Equal(47393, ServerPort);
        Assert.Equal("Sign in to http://127.0.0.1:47393/mobile.git", WithPort("Sign in to http://127.0.0.1:47393/mobile.git", ServerPort));
        Assert.Equal("Sign in to http://127.0.0.1:50123/mobile.git", WithPort("Sign in to http://127.0.0.1:47393/mobile.git", 50123));
    }

    /// <summary>
    /// The settings scenario opened in the app (Phase6ScenarioTests pins it with git): its three
    /// commits in the graph's order, main checked out, no dialog and no menu; the session's git
    /// calls with a global configuration of their own in the data folder, the fixed tool
    /// catalogues and the fake system theme (design 17.2).
    /// </summary>
    [AvaloniaFact]
    public async Task The_settings_scenario_opens_with_the_harnesss_own_git_environment()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo, systemDark: false);

        var graph = Graph(app);
        var rows = graph.Data ?? throw new Xunit.Sdk.XunitException("The graph has no data.");
        Assert.True(rows.IsComplete, "The graph's history is not complete.");
        Assert.Equal(["Local work", "Remote work", "Initial commit"], Enumerable.Range(0, rows.Count).Select(row => rows.CommitAt(row).Subject));
        AssertBranch(app, "main");
        await AssertNoDialogAsync(app);
        await AssertNoMenuAsync(app, logMark: 0);

        var runner = (VisualCommit.Git.GitRunner)app.Session.Shell.Git!.Runner!;
        Assert.Equal(Path.Combine(data.Path, "harness", "global.gitconfig"), runner.SessionEnvironment["GIT_CONFIG_GLOBAL"]);
        Assert.Equal(Path.Combine(data.Path, "harness", "xdg"), runner.SessionEnvironment["XDG_CONFIG_HOME"]);
        Assert.Same(ToolCatalogues.Fixed, app.Session.Services.Tools);
        Assert.False(app.SystemTheme.IsDark);
        Assert.Empty(GitIsolation.GlobalConfigurationWrittenOutsideASession());
    }

    /// <summary>
    /// OpenWorkspaceAsync (design 17.2, part W's Defaults) on a fresh copy of "workspace": the files
    /// it writes, one tab, the dashboard of "Web", its ten rows in the workspace's order, all read
    /// (old-site's folder check among them, WD27) in one pass, none reading, no pass under way and
    /// every row's queue idle; the theme the fake system's dark.
    /// </summary>
    [AvaloniaFact]
    public async Task A_workspace_opens_on_its_dashboard_with_all_ten_rows_read()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenWorkspaceAsync(data, copy);

        var workspace = Assert.Single(app.Session.Services.Workspaces.Current.Workspaces);
        Assert.Equal(("web", "Web"), (workspace.Id, workspace.Name));
        Assert.Equal(copy.Names.Select(copy.PathOf), workspace.Repositories.Select(repository => repository.Path));
        var tab = Assert.Single(app.Session.Shell.Tabs);
        Assert.Equal("web", tab.Dashboard?.WorkspaceId);
        Assert.Same(tab, app.Session.Shell.ActiveTab);

        var snapshot = app.DashboardSnapshot();
        Assert.Equal(TempWorkspace.DashboardOrder, snapshot.Rows.Select(row => row.Name));
        Assert.False(snapshot.IsPassRunning);
        Assert.All(snapshot.Rows, row => Assert.False(row.IsReading, $"{row.Name} is reading."));
        Assert.All(snapshot.Rows, row => Assert.True(row.IsDrawn, $"{row.Name} is not drawn."));
        var rows = app.ActiveDashboard!.Rows;
        Assert.All(rows.Where(row => row.Name != "old-site"), row => Assert.True(row.HasRepository, $"{row.Name} has not been read."));
        Assert.All(rows.Select(row => row.Repository).OfType<RepositoryViewModel>(), repository => Assert.True(repository.Queue.Current is null && repository.Queue.Waiting == 0, $"{repository.Name}'s queue is busy."));
        Assert.Equal("Folder not found", snapshot.Row("old-site").State.Text);
        // The first pass read every row, old-site's folder check among them (WD27). A later pass may
        // follow it at a loaded start: the window's activation at its showing folds into one more
        // pass, which reads the rows then read 10 s ago or more (2.10's rule), so only the first is
        // asserted here.
        var passes = app.LogLines().Where(line => ShellDriver.ReadPassLine("Web").IsMatch(line)).ToList();
        Assert.True(
            passes.Count >= 1 && passes[0].Contains("Web: read 10 repositories in ", StringComparison.Ordinal),
            "The start's first pass did not read the ten rows (WD27); its pass lines:\n" + string.Join('\n', passes));
        Assert.True(app.ShowsDarkTheme);
        Assert.True(app.SystemTheme.IsDark);
    }

    /// <summary>
    /// AssertRow and AssertDashboardHeader on W2's first row and header (scenario facts 2.3), and on
    /// mobile's arrows and old-site's folder state; a wrong cell or count fails with both rows in the
    /// report's form and the difference in words.
    /// </summary>
    [AvaloniaFact]
    public async Task A_row_and_the_header_pass_as_the_report_writes_them_and_a_wrong_cell_is_named()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenWorkspaceAsync(data, copy);

        var api = new ExpectedRow("main", ExpectedCell.Secondary("Up to date", "main is level with origin/main"), ExpectedCell.Secondary("Clean"), ExpectedCell.Empty, ExpectedCell.Empty)
        {
            Folder = copy.PathOf("api"),
        };
        AssertRow(app, "api", api);
        AssertRow(app, "mobile", new ExpectedRow("main", ExpectedCell.Arrows(1, 2, "1 ahead, 2 behind", "1 commit ahead of and 2 behind origin/main"), ExpectedCell.Secondary("Clean"), ExpectedCell.Empty, ExpectedCell.Empty));
        AssertRow(app, "old-site", new ExpectedRow(string.Empty, ExpectedCell.Empty, ExpectedCell.Empty, ExpectedCell.Warning("Folder not found", copy.PathOf("old-site") + " does not exist"), ExpectedCell.Empty));
        // The three keyed buttons' tooltips end with their keys, as TD writes them, once work package
        // WP-K's key table gives Fetch, Pull and Refresh their keys (6.8, AppShortcuts.Shown); until
        // then the app's tooltips carry none, so they are the app's own texts here.
        // WP-K: once your key table is in, assert ExpectedDashboardHeader.Idle("Web", "10 repositories") as it stands.
        AssertDashboardHeader(app, ExpectedDashboardHeader.Idle("Web", "10 repositories") with
        {
            FetchAll = ExpectedHeaderButton.Enabled(OperationTexts.FetchEveryRepository("Web")),
            PullAll = ExpectedHeaderButton.Enabled(OperationTexts.PullEveryRepository("Web")),
            Refresh = ExpectedHeaderButton.Enabled(DashboardTexts.RefreshToolTip("Web")),
        });

        var wrongCell = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertRow(app, "api", api with { Changes = ExpectedCell.Primary("Clean") }));
        Assert.Contains("The row api is not the expected one.", wrongCell.Message, StringComparison.Ordinal);
        Assert.Contains("Expected: api · `main` · `Up to date` (secondary; `main is level with origin/main`) · `Clean` (primary; no tooltip) · empty · empty", wrongCell.Message, StringComparison.Ordinal);
        Assert.Contains("Actual:   api · `main` · `Up to date` (secondary; `main is level with origin/main`) · `Clean` (secondary; no tooltip) · empty · empty", wrongCell.Message, StringComparison.Ordinal);
        Assert.Contains("- Changes: the tone is secondary, expected primary.", wrongCell.Message, StringComparison.Ordinal);

        var selected = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertRow(app, "api", api with { Selected = true }));
        Assert.Contains("- Selected: no, expected yes.", selected.Message, StringComparison.Ordinal);
        var missing = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertRow(app, "archive", api));
        Assert.Contains("The dashboard Web has no row archive.", missing.Message, StringComparison.Ordinal);

        var wrongCount = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertDashboardHeader(app, ExpectedDashboardHeader.Idle("Web", "9 repositories")));
        Assert.Contains("The header of Web is not the expected one.", wrongCount.Message, StringComparison.Ordinal);
        Assert.Contains("- DashboardCount is \"10 repositories\", expected \"9 repositories\".", wrongCount.Message, StringComparison.Ordinal);
        var running = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertDashboardHeader(app, ExpectedDashboardHeader.Running("Web", "10 repositories", BulkKind.Fetch, "Fetching 8 repositories…", 0)));
        Assert.Contains("- Fetch all is enabled, expected disabled.", running.Message, StringComparison.Ordinal);
        Assert.Contains("- The run's text is none, expected \"Fetching 8 repositories…\".", running.Message, StringComparison.Ordinal);
        Assert.Contains("- Cancel all does not show.", running.Message, StringComparison.Ordinal);
    }

    /// <summary>The arrows' cell as the report writes it, and what it refuses.</summary>
    [Fact]
    public void The_arrows_are_written_as_the_report_writes_them()
    {
        Assert.Equal("↑ 2", ExpectedCell.Arrows(2, 0, "2 ahead", "2 commits ahead of origin/main").Text);
        Assert.Equal("↓ 3", ExpectedCell.Arrows(0, 3, "3 behind", "3 commits behind origin/main").Text);
        var both = ExpectedCell.Arrows(1, 2, "1 ahead, 2 behind", "1 commit ahead of and 2 behind origin/main");
        Assert.Equal("↑ 1 ↓ 2", both.Text);
        Assert.Equal("`↑ 1 ↓ 2` (secondary; `1 commit ahead of and 2 behind origin/main`; name `1 ahead, 2 behind`)", both.ToString());
        Assert.Equal("empty", ExpectedCell.Empty.ToString());
        Assert.Throws<ArgumentException>(() => ExpectedCell.Arrows(0, 0, "none", "none"));
    }

    /// <summary>
    /// ShotInBothThemesAsync (11.14, part L's "The pairs") on a dashboard started with System and the
    /// fake dark, under names no check uses: both pictures saved, the step's assertions run once per
    /// variant, the window dark again; a second pair of one name refused before anything flips; and
    /// the pair with a tooltip (LD19), a menu and a dialog (LD1's card) open, each still up.
    /// </summary>
    [AvaloniaFact]
    public async Task A_pair_saves_its_dark_and_light_pictures_once_and_keeps_the_screen_up()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenWorkspaceAsync(data, copy, theme: AppTheme.System);

        var seen = new List<Palette>();
        var (dark, light) = await ShotInBothThemesAsync(app, "99zz-support-pair", assertEach: (_, palette) => seen.Add(palette));
        Assert.Equal([Dark, Light], seen);
        Assert.True(File.Exists(Path.Combine(VisualRun.FolderFor(Phase), "99zz-support-pair-dark.png")));
        Assert.True(File.Exists(Path.Combine(VisualRun.FolderFor(Phase), "99zz-support-pair-light.png")));
        Assert.Equal(Dark.Chrome, dark.PixelAt(ShellDriver.NeutralPoint));
        Assert.Equal(Light.Chrome, light.PixelAt(ShellDriver.NeutralPoint));
        Assert.True(app.ShowsDarkTheme);

        var mark = app.LogMark();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ShotInBothThemesAsync(app, "99zz-support-pair"));
        Assert.True(app.ShowsDarkTheme);
        Assert.True(app.SystemTheme.IsDark);
        Assert.Equal(0, app.CountLogLines(AppLogFile.Containing("The system's theme changed to"), mark));

        await app.RestPointerOnAsync(app.FindByAutomationId("DashboardRefresh"));
        await ShotInBothThemesAsync(app, "99zz-support-tip", keepPointer: true);
        Assert.True(ToolTip.GetIsOpen(app.FindByAutomationId("DashboardRefresh")));

        app.MoveToNeutral();
        var menu = await app.RowMenuAsync("api");
        Assert.Equal("api", menu.Header);
        await ShotInBothThemesAsync(app, "99zz-support-menu");
        Assert.Equal("api", app.Menu().Header);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        app.Click(app.FindByAutomationId("DashboardMore"));
        app.ClickMenuItem("Rename workspace…");
        var rename = await app.WaitForDialogAsync<WorkspaceNameDialogViewModel>();
        await ShotInBothThemesAsync(app, "99zz-support-dialog");
        Assert.Same(rename, app.Session.Services.DialogHost.Current);
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);
        Assert.Equal("System", new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Current.Theme.ToString());
    }

    /// <summary>
    /// WaitForAllRowsIdleAsync (17.2) after a refresh (2.10's trigger 3, F5 and the header's
    /// Refresh): the refresh starts a pass that reads the rows at once, and the wait ends only once
    /// it has ended, no row reads and every row's queue is idle; the pass logged its line.
    /// </summary>
    [AvaloniaFact]
    public async Task All_rows_are_idle_again_after_a_refreshs_pass()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenWorkspaceAsync(data, copy);

        // Trigger 3 by the header's Refresh: F5 reaches it only through work package WP-K's key
        // table (AppShortcuts.All), which is not in yet.
        // WP-K: once your key table is in, press F5 here (app.PressKey(Key.F5)), as the lead asked.
        var mark = app.LogMark();
        app.Click(app.FindByAutomationId("DashboardRefresh"));
        app.MoveToNeutral();
        var started = app.DashboardSnapshot();
        Assert.True(started.IsPassRunning, "The refresh started no pass.");
        Assert.Contains(started.Rows, row => row.IsReading);

        await WaitForAllRowsIdleAsync(app, OperationChainTimeout);
        var dashboard = app.ActiveDashboard!;
        Assert.False(dashboard.IsPassRunning);
        Assert.All(dashboard.Rows, row => Assert.False(row.IsReading, $"{row.Name} is reading."));
        Assert.All(dashboard.Rows.Select(row => row.Repository).OfType<RepositoryViewModel>(), repository => Assert.True(repository.Queue.Current is null && repository.Queue.Waiting == 0, $"{repository.Name}'s queue is busy."));
        await app.WaitForLogLineAsync(ShellDriver.ReadPassLine("Web"), mark: mark);
    }

    /// <summary>
    /// Start and StartWithTabs take other tool catalogues than the fixed ones (17.2, 12.9): WP-SET's
    /// HeldMergeTools, whose merge-tool list the session's early lookup (8.10) then waits for.
    /// </summary>
    [AvaloniaFact]
    public async Task A_start_takes_held_merge_tools_in_place_of_the_fixed_catalogues()
    {
        var held = new HeldMergeTools();
        try
        {
            using (var data = new TempDirectory("data"))
            using (var app = ShellDriver.Start(data.Path, tools: held.Catalogues))
            {
                await app.WaitUntilReadyAsync();
                Assert.Same(held.Catalogues, app.Session.Services.Tools);
                await app.WaitForAsync(() => held.Requests == 1, "the session's early lookup of the merge tools");
            }

            using (var data = new TempDirectory("data"))
            using (var app = ShellDriver.StartWithTabs(data.Path, [(string?)null], tools: held.Catalogues))
            {
                await app.WaitUntilReadyAsync();
                Assert.Same(held.Catalogues, app.Session.Services.Tools);
            }
        }
        finally
        {
            held.Release();
        }
    }

    /// <summary>
    /// WaitForTabOpenedAsync (part W's harness additions): a repository's tab restored behind the
    /// active dashboard opens in the background; the wait ends with its history loaded, its status
    /// read, its clean-up at opening's read made outside any operation in its folder, and its queue
    /// idle, while the dashboard stays active.
    /// </summary>
    [AvaloniaFact]
    public async Task A_tab_restored_behind_the_dashboard_is_waited_for_until_it_has_opened()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        ShellDriver.WriteWorkspaces(data.Path, new WorkspaceList { Workspaces = [copy.ToWorkspace()] });
        using var app = ShellDriver.StartWithTabs(data.Path, [new TabState(null, "web"), new TabState(copy.PathOf("web-app"))]);

        var repository = await app.WaitForTabOpenedAsync(copy.PathOf("web-app"), OperationChainTimeout);
        Assert.NotNull(app.ActiveDashboard);
        var tab = app.Session.Shell.Tabs[1];
        Assert.False(tab.IsActive);
        Assert.Same(tab.Repository, repository);
        Assert.False(repository.IsSummary);
        Assert.True(repository.Graph.IsComplete);
        Assert.Equal(3, repository.Graph.Count);
        Assert.True(app.CountLogLines(AppLogFile.WorkingTreeRead("web-app")) > 0);
        Assert.Contains(app.GitCalls(), call => call.CommandText == ShellDriver.OpeningCleanUpRead
            && call.OperationId is null
            && call.WorkingDirectory is { } folder
            && WorkspacePaths.Same(folder, repository.Path));
        Assert.True(repository.Queue.Current is null && repository.Queue.Waiting == 0);
    }
}
