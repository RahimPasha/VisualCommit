using Avalonia;
using Avalonia.Headless.XUnit;
using VisualCommit.Core;
using VisualCommit.Testing;
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
}
