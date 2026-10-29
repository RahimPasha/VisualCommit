using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core;
using VisualCommit.Core.Git;
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
/// helpers of Phase6Expectations.Common.cs, tried once (the isolation's tripwire, design 17.2, is
/// <see cref="Phase6TripwireTests"/> below, run last); and the settings scenario opened in the app
/// with the harness's per-session git environment, so that
/// a check that fails points at the app and not at the assertion, the harness or the scenario. No
/// pictures: these are not checks of the report.
/// </summary>
public sealed class Phase6SupportTests : Phase6Checks
{
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
    /// The watcher's refresh as the app makes it, on "settings": a write to <c>.git/config</c>
    /// outside the app starts it, <see cref="ShellDriver.QuietCallsSinceAsync"/> waits for its end,
    /// and its calls are whole refreshes by part S's rule (<see cref="GitCallBudget.RefreshForms"/>
    /// are the app's own forms), as is a refresh asked for. Part S's rule itself is tried in
    /// <c>GitCallBudgetTests</c> (App.Tests).
    /// </summary>
    [AvaloniaFact]
    public async Task A_write_to_the_git_folder_outside_the_app_is_waited_out_as_whole_refreshes()
    {
        using var repo = await Scenarios.GitSettingsAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);
        var repository = app.Session.Shell.ActiveTab.Repository!;

        // The opening's clean-up decided too (its backups' read, outside every operation).
        await app.WaitForTabOpenedAsync(repository.Path, OperationChainTimeout);
        await app.WaitForWatchersQuietAsync();
        Assert.True(app.WatchersQuiet);

        var mark = await app.QuietCallMarkAsync();
        await repository.RefreshAsync();
        var asked = await app.QuietCallsSinceAsync(mark);
        Assert.True(asked.Count == GitCallBudget.RefreshForms.Count && GitCallBudget.NotWholeRefreshes(asked) is null, "A refresh is not one whole refresh:\n  " + string.Join("\n  ", asked.Select(call => call.CommandText)));

        mark = await app.QuietCallMarkAsync();
        await repo.ConfigAsync("pull.ff", "true");
        await app.WaitForAsync(() => app.CallsSince(mark).Any(call => call.CommandText.StartsWith("git status --porcelain=v2 ", StringComparison.Ordinal)), "the watcher's refresh after the write");
        var refreshed = await app.QuietCallsSinceAsync(mark);
        Assert.True(refreshed.Count >= GitCallBudget.RefreshForms.Count && GitCallBudget.NotWholeRefreshes(refreshed) is null, "The watcher's refresh is not whole refreshes:\n  " + string.Join("\n  ", refreshed.Select(call => call.CommandText)));
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
        // W2's read: one pass of every row, old-site's folder check among them (WD27); the window's
        // activation at its showing folds into it and reads nothing twice, however long it takes (2.10).
        var passes = app.LogLines().Where(line => ShellDriver.ReadPassLine("Web").IsMatch(line)).ToList();
        Assert.True(
            passes.Count == 1 && passes[0].Contains("Web: read 10 repositories in ", StringComparison.Ordinal),
            "The start did not read the ten rows in one pass (WD27, 2.10); its pass lines:\n" + string.Join('\n', passes));
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
        AssertDashboardHeader(app, ExpectedDashboardHeader.Idle("Web", "10 repositories"));

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

    /// <summary>
    /// SameFolder (part W's harness additions) matches a folder of the copy as given and as git
    /// spells it, once PrepareSameFolderAsync has worked that out (refused before); old-site, which
    /// git never sees, by its text alone; a folder the check made inside the copy (W2's
    /// archive/old-site) through TempWorkspace.RepoAt, which refuses a folder outside the copy.
    /// </summary>
    [Fact]
    public async Task SameFolder_matches_a_folder_as_given_and_as_git_spells_it()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        var api = copy.PathOf("api");
        Assert.Throws<InvalidOperationException>(() => SameFolder(copy, api, api));

        await PrepareSameFolderAsync(copy);
        var gitsApi = await WorkingTreeAsync(copy.Repo("api"));
        Assert.True(SameFolder(copy, api, api));
        Assert.True(SameFolder(copy, api, gitsApi));
        Assert.True(SameFolder(copy, api, api + Path.DirectorySeparatorChar));
        Assert.False(SameFolder(copy, api, copy.PathOf("docs")));
        Assert.False(SameFolder(copy, api, await WorkingTreeAsync(copy.Repo("docs"))));
        Assert.True(SameFolder(copy, copy.PathOf("old-site"), copy.PathOf("old-site")));
        Assert.False(SameFolder(copy, copy.PathOf("old-site"), gitsApi));

        var archive = Path.Combine(copy.Root, "archive", "old-site");
        Directory.CreateDirectory(archive);
        using (var made = copy.RepoAt(archive))
        {
            await made.GitAsync("init", "--quiet");
        }

        Assert.Throws<InvalidOperationException>(() => SameFolder(copy, archive, archive));
        await PrepareSameFolderAsync(copy, archive);
        using var repo = copy.RepoAt(archive);
        Assert.True(SameFolder(copy, archive, await WorkingTreeAsync(repo)));
        Assert.False(SameFolder(copy, archive, copy.PathOf("old-site")));
        Assert.Throws<ArgumentException>(() => copy.RepoAt(Path.GetTempPath()));
        Assert.Throws<ArgumentException>(() => copy.RepoAt(copy.Root));
    }

    /// <summary>
    /// AssertGitOutputShown's app lines (part W: a failed fetch's <c>error: Could not fetch origin</c>,
    /// D109): the dialog's last lines, held apart from the "a line git printed" rule; refused when they
    /// are not last, and not passed as git's when they are not named.
    /// </summary>
    [Fact]
    public void The_apps_own_last_lines_are_held_apart_from_the_lines_git_printed()
    {
        const string key = "fatal: '../remotes/missing.git' does not appear to be a git repository";
        var call = new GitCallRecord(
            DateTimeOffset.UnixEpoch,
            TimeSpan.Zero,
            "git fetch --progress origin",
            null,
            GitCallOutcome.Completed,
            128,
            string.Empty,
            key + "\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\n");
        var git = ShellDriver.CleanedOutputOf(call);
        const string appLine = "error: Could not fetch origin";

        AssertGitOutputShown(call, key, git);
        AssertGitOutputShown(call, key, git + "\n" + appLine, [appLine]);
        var unnamed = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertGitOutputShown(call, key, git + "\n" + appLine));
        Assert.Contains($"The dialog shows a line git did not print: \"{appLine}\".", unnamed.Message, StringComparison.Ordinal);
        var first = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertGitOutputShown(call, key, appLine + "\n" + git, [appLine]));
        Assert.Contains("The dialog does not end with the app's own lines", first.Message, StringComparison.Ordinal);
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
    /// fake dark, under names no check uses, its pictures in a temporary folder and never under
    /// artifacts/visual/phase-6/scripted/, which the gate lists: both pictures saved, the step's
    /// assertions run once per variant, the window dark again; a second pair of one name refused
    /// before anything flips; and the pair with a tooltip (LD19), a menu, a name dialog, the settings
    /// screen (<c>Border.dialogCard.settings</c>) and the Pick dialog stacked on it (LD1's card of the
    /// dialog on top) open, each still up.
    /// </summary>
    [AvaloniaFact]
    public async Task A_pair_saves_its_dark_and_light_pictures_once_and_keeps_the_screen_up()
    {
        using var copy = await Scenarios.WorkspaceAsync();
        using var data = new TempDirectory("data");
        using var pictures = new TempDirectory("pairs");
        using var app = await OpenWorkspaceAsync(data, copy, theme: AppTheme.System);
        Task<(Screenshot Dark, Screenshot Light)> PairAsync(string name, bool keepPointer = false, Action<Screenshot, Palette>? assertEach = null) =>
            ShotInBothThemesAsync(app, name, keepPointer, assertEach, pictures.Path);

        var seen = new List<Palette>();
        var (dark, light) = await PairAsync("99zz-support-pair", assertEach: (_, palette) => seen.Add(palette));
        Assert.Equal([Dark, Light], seen);
        Assert.Equal(["99zz-support-pair-dark.png", "99zz-support-pair-light.png"], Directory.GetFiles(pictures.Path).Select(file => Path.GetFileName(file)).Order(StringComparer.Ordinal));
        Assert.False(File.Exists(Path.Combine(VisualRun.FolderFor(Phase), "99zz-support-pair-dark.png")));
        Assert.Equal(Dark.Chrome, dark.PixelAt(ShellDriver.NeutralPoint));
        Assert.Equal(Light.Chrome, light.PixelAt(ShellDriver.NeutralPoint));
        Assert.True(app.ShowsDarkTheme);

        var mark = app.LogMark();
        await Assert.ThrowsAsync<InvalidOperationException>(() => PairAsync("99zz-support-pair"));
        Assert.True(app.ShowsDarkTheme);
        Assert.True(app.SystemTheme.IsDark);
        Assert.Equal(0, app.CountLogLines(AppLogFile.Containing("The system's theme changed to"), mark));

        await app.RestPointerOnAsync(app.FindByAutomationId("DashboardRefresh"));
        await PairAsync("99zz-support-tip", keepPointer: true);
        Assert.True(ToolTip.GetIsOpen(app.FindByAutomationId("DashboardRefresh")));

        app.MoveToNeutral();
        var menu = await app.RowMenuAsync("api");
        Assert.Equal("api", menu.Header);
        await PairAsync("99zz-support-menu");
        Assert.Equal("api", app.Menu().Header);
        app.PressKey(Key.Escape);
        await AssertNoMenuAsync(app, logMark: null);

        app.Click(app.FindByAutomationId("DashboardMore"));
        app.ClickMenuItem("Rename workspace…");
        var rename = await app.WaitForDialogAsync<WorkspaceNameDialogViewModel>();
        await PairAsync("99zz-support-dialog");
        Assert.Same(rename, app.Session.Services.DialogHost.Current);
        app.PressKey(Key.Escape);
        await AssertNoDialogAsync(app);

        // The settings screen's card, then the Pick dialog's stacked on it: LD1's pixel is the top card's.
        app.MoveToNeutral();
        var settings = await app.OpenSettingsAsync();
        var settingsCard = app.Find<Border>("SettingsCard");
        Assert.True(settingsCard.Classes.Contains("dialogCard") && settingsCard.Classes.Contains("settings"));
        var (settingsDark, settingsLight) = await PairAsync("99zz-support-settings");
        var settingsPixel = app.BoundsOf(settingsCard).TopLeft + new Point(10, 10);
        Assert.Equal((Dark.Panel, Light.Panel), (settingsDark.PixelAt(settingsPixel), settingsLight.PixelAt(settingsPixel)));
        Assert.NotEqual(Dark.Chrome, settingsDark.PixelAt(ShellDriver.NeutralPoint));

        app.ChooseSection(SettingsSection.Git);
        app.Click(app.FindByAutomationId("MergeToolChooseButton"));
        var pick = await app.WaitForDialogAsync<PickDialogViewModel>();
        var pickCard = app.TopDialogView!.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("dialogCard"));
        var (pickDark, pickLight) = await PairAsync("99zz-support-pick");
        var pickPixel = app.BoundsOf(pickCard).TopLeft + new Point(10, 10);
        Assert.Equal((Dark.Panel, Light.Panel), (pickDark.PixelAt(pickPixel), pickLight.PixelAt(pickPixel)));
        Assert.Same(pick, app.Session.Services.DialogHost.Current);
        Assert.Same(settings, app.SettingsDialog);

        app.Click(app.FindByAutomationId(app.TopDialogView!, "CancelButton"));
        await app.WaitForAsync(() => pick.IsClosed, "the Pick dialog to close");
        app.CancelSettings();
        await AssertNoDialogAsync(app);
        Assert.Equal("System", new JsonSettingsStore(Path.Combine(data.Path, "settings.json")).Current.Theme.ToString());
        Assert.Equal(12, Directory.GetFiles(pictures.Path).Length);
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

        // Trigger 3 by F5, the dashboard's Refresh.
        var mark = app.LogMark();
        app.PressKey(Key.F5);
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
    /// Start, StartWithTabs, StartWithWorkspace and OpenWorkspaceAsync take other tool catalogues than
    /// the fixed ones (17.2, 12.9): WP-SET's HeldMergeTools, whose merge-tool list the session's early
    /// lookup (8.10) then waits for.
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

            using (var data = new TempDirectory("data"))
            using (var app = ShellDriver.StartWithWorkspace(data.Path, new WorkspaceList(), tools: held.Catalogues))
            {
                await app.WaitUntilReadyAsync();
                Assert.Same(held.Catalogues, app.Session.Services.Tools);
            }

            using var copy = await Scenarios.WorkspaceAsync();
            using (var data = new TempDirectory("data"))
            using (var app = await OpenWorkspaceAsync(data, copy, tools: held.Catalogues))
            {
                Assert.Same(held.Catalogues, app.Session.Services.Tools);
                Assert.Equal(10, app.DashboardSnapshot().Rows.Count);
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

/// <summary>
/// The isolation's tripwire (design 17.2) for this project, run after its tests that run side by
/// side: xunit runs a collection without parallelization after the parallel ones, so every check's
/// git calls have ended when it looks. The isolation folder is every test process's, so it sees
/// what the projects beside it wrote until then too.
/// </summary>
[Collection(Phase6TripwireTests.Last)]
public sealed class Phase6TripwireTests
{
    /// <summary>The collection that runs the tripwire after the parallel tests.</summary>
    public const string Last = "Phase 6's isolation tripwire, after the parallel tests";

    [Fact]
    public void Nothing_wrote_a_global_configuration_outside_a_session()
    {
        var findings = GitIsolation.GlobalConfigurationWrittenOutsideASession();
        Assert.True(findings.Count == 0, string.Join('\n', findings));
    }
}

/// <summary>Runs <see cref="Phase6TripwireTests"/> after the parallel tests, with none beside it.</summary>
[CollectionDefinition(Phase6TripwireTests.Last, DisableParallelization = true)]
public sealed class Phase6TripwireCollection
{
}
