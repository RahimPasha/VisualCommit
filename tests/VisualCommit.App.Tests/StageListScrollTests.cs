using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// A status read that keeps the number of rows of a scrolled stage list and changes some of them
/// leaves the list where it was: its offset and its extent stay, and every row it keeps in view
/// stays at its place in the window with its control, so that a press on such a row is still a
/// click when its release comes after the read. The unstaged list of the real stage panel, 24 px a
/// row, shows the fifteen files a.txt to o.txt, each modified; the window is short enough that it
/// scrolls. A row replaced at the top of the list's view used to leave an empty slot whose height
/// Avalonia's layout left out, so that the rows below it moved up a row and the list's offset with
/// them, or the list jumped to its top.
/// </summary>
public class StageListScrollTests
{
    private const double RowHeight = 24;

    private const string List = "UnstagedFileList";

    /// <summary>The fifteen files the unstaged list shows, a.txt to o.txt.</summary>
    private static readonly string[] Shown = [.. Enumerable.Range(0, 15).Select(number => string.Create(CultureInfo.InvariantCulture, $"{(char)('a' + number)}.txt"))];

    /// <summary>A file committed with the others and left unchanged, which sorts between e.txt and f.txt.</summary>
    private const string Between = "ea.txt";

    [AvaloniaFact]
    public async Task A_top_row_that_shows_something_new_leaves_the_offset_the_extent_and_the_rows_below_it_as_they_were()
    {
        // c.txt, the top row in view, is deleted: its row reads D instead of M, and the count stays.
        await AssertReadKeepsTheListAsync(
            offset: 50,
            change: repo => repo.DeleteFile("c.txt"),
            changed: ["c.txt"],
            watched: "f.txt");
    }

    [AvaloniaFact]
    public async Task A_top_row_that_shows_something_new_under_an_offset_smaller_than_a_row_leaves_the_list_where_it_was()
    {
        // a.txt, the top row in view, is deleted while the list is scrolled by less than a row.
        await AssertReadKeepsTheListAsync(
            offset: 12,
            change: repo => repo.DeleteFile("a.txt"),
            changed: ["a.txt"],
            watched: "d.txt");
    }

    [AvaloniaFact]
    public async Task The_first_rows_changed_row_by_row_with_the_count_kept_leave_the_list_where_it_was()
    {
        // a.txt is back as committed and ea.txt is modified: the list reads b.txt to e.txt, ea.txt,
        // f.txt to o.txt, fifteen rows still, the first five of them each showing a new file.
        await AssertReadKeepsTheListAsync(
            offset: 50,
            change: repo => repo.WriteFile("a.txt", Committed("a.txt")).WriteFile(Between, Committed(Between) + "changed\n"),
            changed: ["a.txt", "b.txt", "c.txt", "d.txt", "e.txt"],
            watched: "g.txt");
    }

    [AvaloniaFact]
    public async Task A_row_above_the_view_that_shows_something_new_leaves_the_list_where_it_was()
    {
        await AssertReadKeepsTheListAsync(
            offset: 50,
            change: repo => repo.DeleteFile("a.txt"),
            changed: ["a.txt"],
            watched: "e.txt");
    }

    [AvaloniaFact]
    public async Task The_last_row_in_view_that_shows_something_new_leaves_the_list_where_it_was()
    {
        await AssertReadKeepsTheListAsync(
            offset: 50,
            change: null,
            changed: null,
            watched: "e.txt");
    }

    [AvaloniaFact]
    public async Task Two_rows_in_view_that_show_something_new_leave_every_row_between_them_with_its_control_and_its_place()
    {
        // e.txt and h.txt are deleted; f.txt and g.txt, between them, show the same.
        await AssertReadKeepsTheListAsync(
            offset: 50,
            change: repo => repo.DeleteFile("e.txt").DeleteFile("h.txt"),
            changed: ["e.txt", "h.txt"],
            watched: "f.txt");
    }

    /// <summary>
    /// The app on a repository whose unstaged list shows <see cref="Shown"/>, scrolled to
    /// <paramref name="offset"/>; then <paramref name="change"/> in the working tree and one status
    /// read. Asserts that the offset and the extent are as before, that every row in view before
    /// that <paramref name="changed"/> does not name is at the same place in the window with the
    /// same button, and that <paramref name="watched"/> is one of them. With no change given, the
    /// last row in view is deleted.
    /// </summary>
    private static async Task AssertReadKeepsTheListAsync(double offset, Action<TempRepo>? change, string[]? changed, string watched)
    {
        using var repo = await FifteenChangedAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        await app.WaitForWatchersQuietAsync();
        app.Click(app.WorkingRowPoint());
        await app.WaitForAsync(() => app.StageRows(List).Count > 0, "the stage panel's unstaged rows");
        await app.WaitForIdleAsync();
        app.MoveToNeutral();
        Assert.Equal(Shown, repository.Changes.Unstaged.Rows.Select(row => row.Key));

        var scroller = Scroller(app);
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height + (3 * RowHeight), $"The unstaged list does not scroll: extent {scroller.Extent.Height}, view {scroller.Viewport.Height}.");
        scroller.Offset = new Vector(0, offset);
        app.Settle();
        Assert.Equal(offset, scroller.Offset.Y);
        var extent = scroller.Extent.Height;
        Assert.Equal(Shown.Length * RowHeight, extent);
        var before = InView(app, scroller);
        if (change is null)
        {
            var last = before.Keys.Last();
            change = r => r.DeleteFile(last);
            changed = [last];
        }

        Assert.True(before.ContainsKey(watched), $"{watched} is not in view: {string.Join(", ", before.Keys)}.");
        Assert.DoesNotContain(watched, changed!);

        change(repo);
        await repository.RefreshStatusAsync();
        app.Settle();
        await app.WaitForWatchersQuietAsync();
        await app.WaitForIdleAsync();
        app.Settle();

        var after = InView(app, scroller);
        var problems = new List<string>();
        if (scroller.Offset.Y != offset)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"offset {scroller.Offset.Y} for {offset}"));
        }

        if (scroller.Extent.Height != extent)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"extent {scroller.Extent.Height} for {extent}"));
        }

        foreach (var (path, row) in before.Where(entry => !changed!.Contains(entry.Key)))
        {
            if (!after.TryGetValue(path, out var now))
            {
                problems.Add($"{path} is no longer in view");
                continue;
            }

            if (now.Y != row.Y)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"{path} at y {now.Y} for {row.Y}"));
            }

            if (!ReferenceEquals(now.Button, row.Button))
            {
                problems.Add($"{path} has another button");
            }
        }

        Assert.True(problems.Count == 0, $"After the read: {string.Join("; ", problems)}. In view before: {Describe(before)}; after: {Describe(after)}.");
        Assert.True(after.ContainsKey(watched), $"{watched} is not in view after the read.");
    }

    /// <summary>The unstaged list's scroll viewer.</summary>
    private static ScrollViewer Scroller(ShellDriver app) =>
        app.FindByAutomationId(List).GetVisualDescendants().OfType<ScrollViewer>().First();

    /// <summary>The file rows that meet the list's view, top to bottom: each one's place in the window and its own button.</summary>
    private static Dictionary<string, (double Y, Button Button)> InView(ShellDriver app, ScrollViewer scroller)
    {
        var view = app.BoundsOf(scroller);
        var rows = new Dictionary<string, (double Y, Button Button)>(StringComparer.Ordinal);
        foreach (var row in app.StageRows(List).OrderBy(row => app.BoundsOf(row).Y))
        {
            var bounds = app.BoundsOf(row);
            if (bounds.Bottom > view.Top && bounds.Top < view.Bottom && row.DataContext is ChangedFileRow { Key: var key })
            {
                rows[key] = (bounds.Y, ShellDriver.RowButton(row, "StageFileRow"));
            }
        }

        return rows;
    }

    private static string Describe(Dictionary<string, (double Y, Button Button)> rows) =>
        string.Join(", ", rows.Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Key}@{entry.Value.Y}")));

    private static string Committed(string name) => name + "\n";

    /// <summary>A repository with <see cref="Shown"/> and <see cref="Between"/> committed, and each of <see cref="Shown"/> modified in the working tree.</summary>
    private static async Task<TempRepo> FifteenChangedAsync()
    {
        var repo = await TempRepo.CreateAsync();
        try
        {
            foreach (var name in Shown.Append(Between))
            {
                repo.WriteFile(name, Committed(name));
            }

            await repo.CommitAsync("Add sixteen files");
            foreach (var name in Shown)
            {
                repo.WriteFile(name, Committed(name) + "changed\n");
            }

            return repo;
        }
        catch
        {
            repo.Dispose();
            throw;
        }
    }
}
