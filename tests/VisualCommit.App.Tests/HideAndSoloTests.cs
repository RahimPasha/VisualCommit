using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Services;
using VisualCommit.App.ViewModels.Panels;
using VisualCommit.Core;
using VisualCommit.Core.Session;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Solo and hide in the whole app on the filters scenario (docs/phase-5-design.md 8, D159, D178,
/// report part F's F14, FD13, FD14, FD16, FD19): the branch menus' items, the graph reloaded with
/// git's own filter, the left panel's marks and toggles (a press on one starts no drag, a
/// double-click checks nothing out), a click on a hidden branch whose commit is no row, the
/// session's entry kept across a restart and removed when nothing is hidden; and the driver's
/// helpers for them (ShellDriver.Phase5.Find.cs).
/// </summary>
public class HideAndSoloTests
{
    private const string Revisions = "--no-show-signature --no-color --encoding=UTF-8 ";
    private const string AlphaOne = "7c60fc46a4178e4f2f88df0f73c4b51808be230a";

    [AvaloniaFact]
    public async Task Hiding_a_branch_hides_its_upstream_marks_both_in_the_left_panel_and_showing_it_undoes_both()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.FiltersAsync();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        Assert.Equal(15, repository.Graph.Count);
        await OpenFoldersAsync(app);

        // F14 step 2: the current branch cannot be hidden.
        app.RightClick(await app.RefRowAsync("Local branches", "main"));
        var main = app.Menu();
        Assert.Equal("Hide main (disabled: \"The checked-out branch is always shown\")", main.Lines()[^4]);
        Assert.Equal("Solo main", main.Lines()[^3]);
        Assert.DoesNotContain(main.Lines(), line => line.StartsWith("Compare main", StringComparison.Ordinal));
        app.PressKey(Key.Escape);

        // Step 3: Hide feature/alpha, with its upstream.
        var refsBefore = (await repo.GitAsync("for-each-ref")).StandardOutput;
        app.RightClick(await app.RefRowAsync("Local branches", "feature", "alpha"));
        Assert.Equal(["Compare feature/alpha with main", "Show reflog of feature/alpha", "Hide feature/alpha", "Solo feature/alpha", "—", "Copy branch name"], app.Menu().Lines().TakeLast(6));
        app.ClickMenuItem("Hide feature/alpha");
        await app.WaitForGraphReloadAsync();
        Assert.Equal(12, repository.Graph.Count);
        Assert.Equal(-1, repository.Graph.IndexOf(AlphaOne));
        Assert.EndsWith(
            Revisions + "--exclude=feature/alpha --branches --exclude=origin/feature/alpha --exclude=origin/HEAD --remotes --tags --ignore-missing HEAD --",
            app.GitCalls().Last(call => call.CommandText.StartsWith("git log --date-order", StringComparison.Ordinal)).CommandText,
            StringComparison.Ordinal);
        Assert.Equal(refsBefore, (await repo.GitAsync("for-each-ref")).StandardOutput);

        // The left panel lists every ref; both alpha rows are drawn in the disabled colour with the eye.
        var disabled = Brush(app, "VcTextDisabledBrush");
        foreach (var path in new[] { new[] { "Local branches", "feature", "alpha" }, ["Remotes", "origin", "feature", "alpha"] })
        {
            await app.RefRowAsync(path);
            var marks = app.RefRowMarks(path);
            Assert.Equal(disabled, marks.NameColor);
            Assert.True(marks.ShowsHiddenToggle);
            Assert.False(marks.ShowsSoloToggle);
            Assert.Equal(18, marks.ToggleBounds!.Value.Width, 1);
            Assert.Equal(18, marks.ToggleBounds!.Value.Height, 1);
            Assert.Equal("Hidden in the graph. Click to show it", marks.ToggleToolTip);
        }

        // The local one's eye lies before its "↓1".
        var behind = app.Find<ItemsControl>("RefList").GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.Name == "BehindCount" && text.DataContext is RefItemRow { Key: "ref:refs/heads/feature/alpha" });
        Assert.True(app.RefRowMarks("Local branches", "feature", "alpha").ToggleBounds!.Value.Right <= app.BoundsOf(behind).Left);
        Assert.False(app.RefRowMarks("Local branches", "feature", "beta").ShowsHiddenToggle);

        // The session: one entry for the repository, keyed by its folder as git reports it (as the
        // tabs are: macOS's /private/var where the test's folder is under /var), with the upstream recorded.
        var entry = Assert.Single(Session(data).GraphFilters);
        Assert.Equal(repository.Path, entry.RepositoryPath);
        Assert.Equal(["refs/heads/feature/alpha", "refs/remotes/origin/feature/alpha"], entry.Hidden);
        Assert.Empty(entry.Soloed);
        Assert.Empty(entry.HiddenRemotes);
        Assert.Equal([new BranchUpstream("refs/heads/feature/alpha", "refs/remotes/origin/feature/alpha")], entry.Upstreams);
        Assert.Equal(["origin"], entry.Remotes);

        // Step 4: a click on the hidden row selects nothing (Alpha one is no row) and highlights nothing
        // (FD19), also when a commit was selected before: its details give way to the placeholder.
        app.Click(await app.RefRowAsync("Local branches", "feature", "beta"));
        await app.WaitForAsync(() => repository.Selection.Single is not null && repository.Details.HasCommit, "feature/beta's commit to be selected");
        app.Click(app.RefRowNamePoint("Local branches", "feature", "alpha"));
        Assert.True(repository.Selection.IsEmpty);
        Assert.Equal(-1, repository.SelectedIndex);
        Assert.DoesNotContain(repository.LeftPanel.Rows.OfType<RefItemRow>(), row => row.IsSelected);
        await app.WaitForAsync(() => repository.Details.ShowsPlaceholder, "the details' placeholder");
        Assert.Equal("feature/alpha is hidden in the graph", app.RefRowMarks("Local branches", "feature", "alpha").NameToolTip);

        // A right-click on its eye opens the row's menu (4b), as on the row; it shows nothing.
        var graphBefore = repository.Graph;
        app.RightClick(app.RefRowMarks("Local branches", "feature", "alpha").ToggleBounds!.Value.Center);
        var eyeMenu = app.Menu();
        Assert.Equal("feature/alpha", eyeMenu.Header);
        Assert.Contains("Show feature/alpha", eyeMenu.Lines());
        app.PressKey(Key.Escape);
        Assert.Same(graphBefore, repository.Graph);
        Assert.True(app.RefRowMarks("Local branches", "feature", "alpha").ShowsHiddenToggle);

        // Step 5: a comparison whose To is no row: its link is disabled, and the counts' singular.
        app.RightClick(await app.RefRowAsync("Local branches", "feature", "alpha"));
        Assert.Contains("Show feature/alpha", app.Menu().Lines());
        app.ClickMenuItem("Compare feature/alpha with main");
        await app.WaitForCompareAsync();
        var compare = app.Compare()!;
        Assert.True(compare.FromEnabled);
        Assert.False(compare.ToEnabled);
        Assert.Equal("This commit is not in the graph", compare.ToToolTip);
        Assert.Equal("1 commit only on feature/alpha, 3 only on main", compare.Counts);
        Assert.Equal(["A alpha.txt src"], compare.Files.Select(row => $"{row.StatusLetter} {row.Name} {row.Folder}"));
        app.Click(app.FindByAutomationId("CompareClose"));

        // Step 6: Show feature/alpha shows both, and the session's entry goes (FD14).
        app.RightClick(await app.RefRowAsync("Local branches", "feature", "alpha"));
        app.ClickMenuItem("Show feature/alpha");
        await app.WaitForGraphReloadAsync();
        Assert.Equal(15, repository.Graph.Count);
        Assert.EndsWith(
            Revisions + "--branches --remotes --tags --ignore-missing HEAD --",
            app.GitCalls().Last(call => call.CommandText.StartsWith("git log --date-order", StringComparison.Ordinal)).CommandText,
            StringComparison.Ordinal);
        Assert.False(app.RefRowMarks("Remotes", "origin", "feature", "alpha").ShowsHiddenToggle);
        Assert.Empty(Session(data).GraphFilters);
    }

    [AvaloniaFact]
    public async Task Soloing_a_branch_solos_its_upstream_leaves_the_other_labels_out_and_the_sections_end_it()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.FiltersAsync();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        await OpenFoldersAsync(app);

        // F14 step 7.
        app.RightClick(await app.RefRowAsync("Local branches", "feature", "beta"));
        app.ClickMenuItem("Solo feature/beta");
        await app.WaitForGraphReloadAsync();
        Assert.Equal(12, repository.Graph.Count);
        Assert.EndsWith(
            Revisions + "--ignore-missing refs/heads/feature/beta refs/remotes/origin/feature/beta HEAD --",
            app.GitCalls().Last(call => call.CommandText.StartsWith("git log --date-order", StringComparison.Ordinal)).CommandText,
            StringComparison.Ordinal);
        var graph = app.Find<VisualCommit.App.Controls.CommitGraphControl>("GraphRows");
        Assert.Equal(["main ✓"], graph.LabelsInRow(4).Select(label => label.FullText + (label.Kind == VisualCommit.App.ViewModels.Graph.RefLabelKind.CurrentBranch ? " ✓" : string.Empty)));
        Assert.Empty(graph.LabelsInRow(7));

        var accent = Brush(app, "VcAccentBrush");
        var disabled = Brush(app, "VcTextDisabledBrush");
        foreach (var path in new[] { new[] { "Local branches", "feature", "beta" }, ["Remotes", "origin", "feature", "beta"] })
        {
            await app.RefRowAsync(path);
            var marks = app.RefRowMarks(path);
            Assert.True(marks.ShowsSoloToggle);
            Assert.Equal("Soloed. Click to stop soloing it", marks.ToggleToolTip);
            Assert.Equal(accent, marks.ToggleColor);
            Assert.NotEqual(disabled, marks.NameColor);
        }

        foreach (var path in new[] { new[] { "Local branches", "feature", "alpha" }, ["Local branches", "feature", "merged"], ["Remotes", "origin", "feature", "alpha"], ["Remotes", "origin", "main"] })
        {
            await app.RefRowAsync(path);
            Assert.Equal(disabled, app.RefRowMarks(path).NameColor);
            Assert.False(app.RefRowMarks(path).ShowsSoloToggle);
        }

        Assert.Equal(accent, app.RefRowMarks("Local branches", "main").NameColor);
        var entry = Assert.Single(Session(data).GraphFilters);
        Assert.Equal(["refs/heads/feature/beta", "refs/remotes/origin/feature/beta"], entry.Soloed);
        Assert.Empty(entry.Hidden);

        // Step 8: the section's menu, and Stop soloing.
        app.RightClick(await app.RefRowAsync("Local branches"));
        var section = app.Menu().Lines();
        Assert.Equal("Show all hidden branches (disabled: \"No branch is hidden\")", section[^2]);
        Assert.Equal("Stop soloing", section[^1]);
        app.ClickMenuItem("Stop soloing");
        await app.WaitForGraphReloadAsync();
        Assert.Equal(15, repository.Graph.Count);
        Assert.Empty(Session(data).GraphFilters);
        Assert.NotEqual(disabled, app.RefRowMarks("Local branches", "feature", "alpha").NameColor);
        app.RightClick(await app.RefRowAsync("Local branches"));
        Assert.Equal("Stop soloing (disabled: \"No branch is soloed\")", app.Menu().Lines()[^1]);
        app.PressKey(Key.Escape);
    }

    [AvaloniaFact]
    public async Task A_hidden_branch_stays_hidden_after_a_restart_and_its_eye_shows_it_without_a_drag()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.FiltersAsync();

        // F14 part C's first start: hide feature/alpha, then close the app.
        using (var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700))
        {
            await app.WaitForRepositoryAsync();
            app.Click(await app.RefRowAsync("Local branches", "feature"));
            app.RightClick(await app.RefRowAsync("Local branches", "feature", "alpha"));
            app.ClickMenuItem("Hide feature/alpha");
            await app.WaitForGraphReloadAsync();
        }

        Assert.Single(Session(data).GraphFilters);

        // The second start loads the hidden graph from its first call, with no reload.
        using var restarted = ShellDriver.Start(data.Path, 1100, 700);
        var repository = await restarted.WaitForRepositoryAsync();
        Assert.Equal(12, repository.Graph.Count);
        var loads = restarted.GitCalls().Where(call => call.CommandText.StartsWith("git log --date-order", StringComparison.Ordinal)).ToList();
        Assert.EndsWith("--exclude=feature/alpha --branches --exclude=origin/feature/alpha --exclude=origin/HEAD --remotes --tags --ignore-missing HEAD --", Assert.Single(loads).CommandText, StringComparison.Ordinal);
        await OpenFoldersAsync(restarted);
        Assert.True(restarted.RefRowMarks("Local branches", "feature", "alpha").ShowsHiddenToggle);

        // A press and a move on the eye start no drag; a double-click on it checks nothing out.
        var eye = restarted.RefRowMarks("Local branches", "feature", "alpha").ToggleBounds!.Value.Center;
        restarted.PressMouse(eye);
        restarted.MoveMouseHeld(eye + new Avalonia.Point(0, 40), steps: 4);
        Assert.False(restarted.BranchDrag.IsDragging);
        restarted.ReleaseMouse(eye + new Avalonia.Point(0, 40));
        await restarted.WaitForIdleAsync();
        Assert.True(restarted.RefRowMarks("Local branches", "feature", "alpha").ShowsHiddenToggle);

        // F14 step 11: a click on the eye shows the branch with its upstream; nothing is selected.
        restarted.Click(eye);
        await restarted.WaitForGraphReloadAsync();
        Assert.Equal(15, repository.Graph.Count);
        Assert.True(repository.Selection.IsEmpty);
        Assert.Empty(Session(data).GraphFilters);
        Assert.Equal("main", repository.Refs!.Head.BranchName);
    }

    [AvaloniaFact]
    public async Task A_double_click_on_a_toggle_checks_nothing_out()
    {
        using var data = new TempDirectory("data");
        using var repo = await Scenarios.FiltersAsync();
        using var app = ShellDriver.StartWithTabs(data.Path, [repo.Path], width: 1100, height: 700);
        var repository = await app.WaitForRepositoryAsync();
        await OpenFoldersAsync(app);
        app.RightClick(await app.RefRowAsync("Local branches", "feature", "beta"));
        app.ClickMenuItem("Solo feature/beta");
        await app.WaitForGraphReloadAsync();

        await app.RefRowAsync("Local branches", "feature", "beta");
        var solo = app.RefRowMarks("Local branches", "feature", "beta").ToggleBounds!.Value.Center;
        app.DoubleClick(solo);
        await app.WaitForGraphReloadAsync();
        await app.WaitForIdleAsync();
        Assert.Equal("main", repository.Refs!.Head.BranchName);
        Assert.Null(repository.Queue.LastResult);
        Assert.Empty(Session(data).GraphFilters);
    }

    /// <summary>Opens the <c>feature</c> folders under Local branches and Remotes › origin (F14 step 1).</summary>
    private static async Task OpenFoldersAsync(ShellDriver app)
    {
        app.Click(await app.RefRowAsync("Local branches", "feature"));
        app.Click(await app.RefRowAsync("Remotes", "origin", "feature"));
    }

    private static SessionState Session(TempDirectory data) =>
        new JsonSessionStore(new AppPaths(data.Path).SessionFile).Current;

    private static Color Brush(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");
}
