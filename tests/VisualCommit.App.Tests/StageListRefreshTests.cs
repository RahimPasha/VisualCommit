using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core.Diff;
using VisualCommit.Core.Git;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A status read that changes nothing replaces none of the stage lists' rows, so a click on a row
/// that such a read reaches between its press and its release still counts. macOS's file watcher
/// delivers the index's change after a write's own refresh, and the read it starts used to replace
/// every row, which lost phase 2's check 10's click on CI (runs 38122509278 and 39094444954).
/// </summary>
public class StageListRefreshTests
{
    [Fact]
    public void A_list_read_again_keeps_the_rows_that_show_the_same_and_replaces_only_the_changed_one()
    {
        var list = new ChangedFileList();
        list.SetMode(FileListMode.Flat);
        ChangedFile[] files = [new("a.txt", FileChangeKind.Modified), new("b.txt", FileChangeKind.Added), new("c.txt", FileChangeKind.Deleted)];
        list.Show(files, keepFolders: true);
        list.SelectedPath = "b.txt";
        var rows = list.Rows.ToList();
        var changes = 0;
        list.Rows.CollectionChanged += (_, _) => changes++;

        // The same files, read again as new objects (each status read makes its own).
        list.Show([.. files.Select(file => file with { })], keepFolders: true);
        Assert.Equal(0, changes);
        Assert.Equal(rows, list.Rows);
        Assert.True(list.Rows[1].IsSelected);

        // b.txt is modified now: its row alone is replaced.
        list.Show([files[0], files[1] with { Kind = FileChangeKind.Modified }, files[2]], keepFolders: true);
        Assert.Same(rows[0], list.Rows[0]);
        Assert.NotSame(rows[1], list.Rows[1]);
        Assert.Same(rows[2], list.Rows[2]);
        Assert.Equal("M", list.Rows[1].StatusLetter);
        Assert.True(list.Rows[1].IsSelected);
    }

    [AvaloniaFact]
    public async Task A_status_read_between_the_press_and_the_release_of_a_file_row_still_opens_its_diff()
    {
        using var repo = await Scenarios.ChangesAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        await app.WaitUntilReadyAsync();
        var tab = app.Session.Shell.ActiveTab;
        await app.WaitForAsync(() => tab.Repository is { Graph.IsComplete: true, HasWorkingChanges: true }, "the history and the working-changes row");
        var repository = tab.Repository!;
        repository.IsWorkingRowSelected = true;
        Button? row = null;
        await app.WaitForAsync(() => (row = FileRow(app, "StagedFileList", "README.md")) is not null, "the staged row of README.md");

        // The press, a status read that finds nothing new (the watcher's refresh of an outside
        // change, or a late one after the app's own write), then the release: still one click.
        var point = app.BoundsOf(row!).Center;
        app.PressMouse(point);
        await repository.RefreshStatusAsync();
        app.ReleaseMouse(point);

        await app.WaitForAsync(() => repository.Diff is { Target: { Side: DiffSide.Staged, Path: "README.md" } }, "the staged diff of README.md");
    }

    private static Button? FileRow(ShellDriver app, string list, string path) =>
        app.Find<ItemsControl>(list).GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => button.IsEffectivelyVisible
                && AutomationProperties.GetAutomationId(button) == "StageFileRow"
                && AutomationProperties.GetName(button) == path);
}
