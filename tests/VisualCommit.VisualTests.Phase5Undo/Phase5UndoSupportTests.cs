using Avalonia.Headless.XUnit;
using VisualCommit.App.ViewModels;
using VisualCommit.Core;
using VisualCommit.Core.Git;
using VisualCommit.Testing;
using VisualCommit.VisualTests.Phase5;
using Xunit;

namespace VisualCommit.VisualTests.Phase5Undo;

/// <summary>
/// What the undo checks' project (part U, docs/phase-5-design.md 15.3) takes from the walk-through
/// of parts A, B, F and P, tried once here: its picture names, and its start on a scenario, on the
/// reflog scenario, whose backups the clean-up at opening judges by the harness's backup clock
/// (design 2.3). No pictures: these are not checks of the report.
/// </summary>
public sealed class Phase5UndoSupportTests : Phase5Checks
{
    [Theory]
    [InlineData("u16a-commit")]
    [InlineData("u17b-hard-reset-undone")]
    [InlineData("u23c2-refused")]
    public void A_picture_name_of_part_u_is_the_reports(string name) => Assert.Matches(PictureName(), name);

    /// <summary>
    /// The reflog scenario opened in the app (Phase5ScenarioTests pins it with git): its nine
    /// commits, and the clean-up at opening, which removes the two backups older than 30 days by
    /// the harness's fixed backup clock and keeps the fifteen younger ones, each of a kind the
    /// backups list shows.
    /// </summary>
    [AvaloniaFact]
    public async Task The_reflog_scenario_opens_and_the_clean_up_removes_only_its_two_old_backups()
    {
        using var repo = await Scenarios.ReflogAsync();
        using var data = new TempDirectory("data");
        using var app = await OpenAsync(data, repo);

        var rows = Graph(app).Data ?? throw new Xunit.Sdk.XunitException("The graph has no data.");
        Assert.Equal(
            ["Draft work", "Write the notes", "Topic work", "Clash notes", "Clash notes", "Clashing work", "Main work", "Add the app", "Initial commit"],
            Enumerable.Range(0, rows.Count).Select(row => rows.CommitAt(row).Subject));

        await app.WaitForAsync(
            () => app.Session.Operations.Snapshot().Any(record => record.Title == RepositoryViewModel.CleanUpBackupsTitle),
            "the backups' clean-up at opening",
            OutsideChangeTimeout);
        await app.WaitForIdleAsync();
        var cleanUp = Assert.Single(app.Session.Operations.Snapshot(), record => record.Title == RepositoryViewModel.CleanUpBackupsTitle);
        Assert.Equal(("Removed 2 backups", true), (cleanUp.Message, cleanUp.IsBackground));
        Assert.Equal(
            [.. Scenarios.ReflogFiles.OldBackups.Select(name => $"Removed backup {name} (older than {BackupRetention.Default.Days} days)")],
            cleanUp.Notes);

        var backups = await BackupsAsync(repo);
        Assert.Equal(15, backups.Select(backup => backup.Ref.Stem).Distinct().Count());
        Assert.All(backups, backup => Assert.True(backup.Ref.Time >= AppClocks.HarnessTime.AddDays(-BackupRetention.Default.Days), $"{backup.Name} is older than 30 days."));
        await AssertNoDialogAsync(app);
    }
}
