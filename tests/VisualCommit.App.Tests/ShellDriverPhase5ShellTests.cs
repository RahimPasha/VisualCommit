using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Operations;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The driver's part for phase 5's shell (ShellDriver.Phase5.Shell.cs; the report's decision AD12),
/// tried on the app: the wait for the clean-up's record at opening, the activity log read through
/// it with the clean-up's real record and git calls, its rows found by name and occurrence, and the
/// keys' helpers' limits. The keys themselves are <c>WindowKeysTests</c>', the panel's layout
/// <c>ActivityLogViewTests</c>'.
/// </summary>
public class ShellDriverPhase5ShellTests
{
    [AvaloniaFact]
    public async Task WaitForOperationRecordAsync_waits_for_the_clean_up_at_opening_whose_notes_and_calls_the_log_shows()
    {
        // A2's start (the report's part A): "conflicts" with old and many backups.
        using var repo = await Scenarios.ConflictsAsync();
        await Scenarios.AddOldAndManyBackupsAsync(repo);
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        await app.WaitForRepositoryAsync();

        var record = await app.WaitForOperationRecordAsync(RepositoryViewModel.CleanUpBackupsTitle);
        await app.WaitForIdleAsync();

        Assert.Equal((true, OperationKind.Local, OperationOutcome.Succeeded, "Removed 3 backups"), (record.IsBackground, record.Kind, record.Outcome, record.Message));
        Assert.Equal(
            [
                "Removed backup reset-20251120T090001000Z (older than 30 days)",
                "Removed backup branch-20251120T090002000Z (older than 30 days)",
                "Removed backup reset-20251220T090001000Z (more than 50 backups)",
            ],
            record.Notes);

        app.ClickActivityLogToggle();

        // As A2's step 1 writes it: "[IconCheck, #3FB97F] 09:00:00 · Clean up backups · Removed 3 backups · 0 ms", closed.
        var cleanUp = app.ActivityLogRow(RepositoryViewModel.CleanUpBackupsTitle);
        Assert.Equal(
            ("IconChevronRight", "IconCheck", SuccessColor(app), "09:00:00", RepositoryViewModel.CleanUpBackupsTitle, "Removed 3 backups", "0 ms", false),
            (cleanUp.Chevron, cleanUp.Glyph, cleanUp.GlyphColor, cleanUp.Time, cleanUp.Title, cleanUp.Message, cleanUp.Duration, cleanUp.IsOpen));

        app.ClickActivityLogRow(RepositoryViewModel.CleanUpBackupsTitle);
        var rows = app.ActivityLog().Rows;
        (ActivityLogRowKind Kind, string Name, string? Chevron, double Indent)[] opened =
        [
            (ActivityLogRowKind.Operation, RepositoryViewModel.CleanUpBackupsTitle, "IconChevronDown", 0),
            (ActivityLogRowKind.Note, record.Notes[0], null, 24),
            (ActivityLogRowKind.Note, record.Notes[1], null, 24),
            (ActivityLogRowKind.Note, record.Notes[2], null, 24),
            (ActivityLogRowKind.Call, "git for-each-ref \"--format=%(refname) %(objectname)\" refs/visualcommit/backup/", "IconChevronRight", 24),
            (ActivityLogRowKind.Call, "git update-ref --stdin", "IconChevronRight", 24),
        ];
        Assert.Equal(opened, rows.Select(row => (row.Kind, row.AutomationName, row.Chevron, row.Indent ?? double.NaN)));
        Assert.Equal(("Removed 3 backups", "09:00:00", "0 ms"), (rows[0].Message, rows[0].Time, rows[0].Duration));

        // The box's lines wrap: the list keeps to its end (AD15) and the call's row goes up out of view.
        app.ClickActivityLogRow("git update-ref --stdin");
        var input = Assert.Single(app.ActivityLog().Rows.Single(row => row.Kind == ActivityLogRowKind.Output).Parts!);
        var head = (await repo.GitAsync("rev-parse", "HEAD")).StandardOutput.Trim();
        Assert.Equal(
            ("Input", $"delete refs/visualcommit/backup/reset-20251120T090001000Z {head}\ndelete refs/visualcommit/backup/branch-20251120T090002000Z/old-idea {head}\ndelete refs/visualcommit/backup/reset-20251220T090001000Z {head}"),
            (input.Title, input.Text));
    }

    [AvaloniaFact]
    public async Task ActivityLogRow_counts_the_rows_of_a_name_from_the_top_and_names_what_it_cannot_find()
    {
        using var repo = await Scenarios.HistoryAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path]);
        var repository = await app.WaitForRepositoryAsync();
        app.ClickActivityLogToggle();
        var key = Core.Git.WorktreeKey.Of(repository.Repository.GitDirectory);
        foreach (var id in new[] { "op-1", "op-2" })
        {
            var sequence = RecordSequence.Next();
            app.Session.Operations.Add(new OperationRecord(id, "history", "Fetch", DateTimeOffset.UnixEpoch, TimeSpan.Zero, OperationOutcome.Succeeded, "Fetched") { RepositoryKey = key, Sequence = sequence });
        }

        app.Settle();
        var first = app.ActivityLogRow("Fetch");
        var second = app.ActivityLogRow("Fetch", 1);
        Assert.True(second.Bounds!.Value.Top > first.Bounds!.Value.Top);
        Assert.Equal((0, 1), (first.Index, second.Index));
        Assert.Throws<InvalidOperationException>(() => app.ActivityLogRow("Fetch", 2));
        Assert.Throws<InvalidOperationException>(() => app.ActivityLogRow("Push"));
        Assert.Throws<ArgumentOutOfRangeException>(() => app.PressGitRedo(app.RedoGestures.Count));

        app.ClickActivityLogToggle();
        Assert.Throws<InvalidOperationException>(() => app.DragActivityLogSplitter(-10));
    }

    /// <summary>The theme's <c>VcSuccessBrush</c>, the glyph of an operation that succeeded (#3FB97F in the dark theme).</summary>
    private static Color SuccessColor(ShellDriver app) =>
        app.Window.TryFindResource("VcSuccessBrush", app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException("No brush VcSuccessBrush.");
}
