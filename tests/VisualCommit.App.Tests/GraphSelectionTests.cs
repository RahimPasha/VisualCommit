using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.App.ViewModels.Shell;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Git;
using VisualCommit.Core.Graph;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// Several commits selected in the graph (docs/phase-3-design.md 4h, D88): the gestures with the
/// platform's command modifier, Shift and the arrow keys, the selection kept as SHAs across a
/// reload, the selected rows' look, what a right-click keeps, and the details panel's list of
/// them (<c>MultiSelectionView</c>). The ops scenario's rows are those of the phase 3 report.
/// </summary>
public class GraphSelectionTests
{
    // The ops scenario's rows (docs/test-reports/phase-3.md, "Ops scenario").
    private const int StashRow = 0;
    private const int ConflictingChange = 1;
    private const int FastForwardMe = 2;
    private const int MainWork = 3;
    private const int AlphaTwo = 4;
    private const int AlphaOne = 5;
    private const int AddCore = 6;

    [AvaloniaFact]
    public async Task The_command_modifier_click_adds_and_removes_rows_and_the_details_list_them_in_row_order()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path], width: 1400, height: 900);
        var repository = await GraphShownAsync(app);
        var graph = Graph(app);

        // The gesture's modifier is the platform's own, read from its hotkey configuration (4h).
        var platform = app.Window.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.None;
        Assert.Equal(platform == KeyModifiers.None ? KeyModifiers.Control : platform, graph.CommandModifiers);
        Assert.Equal(app.CommandModifier, graph.CommandModifiers);

        // Check 11 step 5: click "Alpha one", then the command modifier on "Alpha two".
        app.Click(RowPoint(app, AlphaOne));
        Assert.Equal(AlphaOne, repository.SelectedIndex);
        Assert.Null(repository.MultiSelection);
        app.Click(RowPoint(app, AlphaTwo), app.CommandModifier);

        Assert.Equal([Sha(repository, AlphaOne), Sha(repository, AlphaTwo)], repository.Selection.Shas);
        Assert.Equal(Sha(repository, AlphaTwo), repository.Selection.Anchor);
        Assert.Equal(Sha(repository, AlphaTwo), repository.Selection.Focus);
        Assert.Equal(-1, repository.SelectedIndex);
        Assert.True(repository.Details.IsMultiSelection);
        Assert.False(repository.Details.ShowsPlaceholder);
        var multi = Assert.IsType<MultiSelectionViewModel>(repository.MultiSelection);
        Assert.Equal("2 commits selected", multi.Title);
        Assert.Equal([("ce5ea6a", "Alpha two"), ("dff8fbf", "Alpha one")], multi.Items.Select(item => (item.ShortSha, item.Subject)));

        var view = app.Window.GetVisualDescendants().OfType<MultiSelectionView>().Single(candidate => candidate.IsEffectivelyVisible);
        Assert.Equal("2 commits selected", ((TextBlock)app.FindByAutomationId("MultiSelectionTitle")).Text);
        Assert.Equal(MultiSelectionViewModel.HintText, ((TextBlock)app.FindByAutomationId("MultiSelectionHint")).Text);
        Assert.Equal("Right-click a selected commit for what can be done with all of them.", MultiSelectionViewModel.HintText);
        var items = view.GetVisualDescendants().OfType<Control>().Where(control => AutomationProperties.GetAutomationId(control) == "MultiSelectionItem").ToList();
        Assert.Equal(["ce5ea6a Alpha two", "dff8fbf Alpha one"], items.Select(AutomationProperties.GetName));
        var shaText = items[0].GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == "MultiSelectionSha");
        Assert.Equal(Resource(app, "VcTextSecondaryBrush"), ((ISolidColorBrush)shaText.Foreground!).Color);
        Assert.Equal(app.Window.FindResource("VcCodeFontFamily"), shaText.FontFamily);
        Assert.False(app.FindByAutomationId("DetailsPlaceholder").IsEffectivelyVisible);
        Assert.False(app.FindByAutomationId("FileList").IsEffectivelyVisible);

        // Its root is a control element named by its title, so UI Automation can find it.
        var peer = ControlAutomationPeer.CreatePeerForElement(view);
        Assert.True(peer.IsControlElement());
        Assert.Equal("MultiSelectionView", peer.GetAutomationId());
        Assert.Equal("2 commits selected", peer.GetName());

        // Both rows have the selection background; the others do not.
        var shot = app.Capture();
        foreach (var row in new[] { AlphaTwo, AlphaOne })
        {
            Assert.Equal(Resource(app, "VcSelectionBrush"), shot.PixelAt(BackgroundPoint(app, row)));
        }

        Assert.Equal(Resource(app, "VcWindowBackgroundBrush"), shot.PixelAt(BackgroundPoint(app, MainWork)));
        Pictures(app, "graph-selection-two");

        // Check 11 step 6: a third row, then the command modifier on it again removes it (step 7).
        app.Click(RowPoint(app, MainWork), app.CommandModifier);
        Assert.Equal("3 commits selected", ((MultiSelectionViewModel)repository.MultiSelection!).Title);
        Assert.Equal(["88d6b91", "ce5ea6a", "dff8fbf"], ((MultiSelectionViewModel)repository.MultiSelection!).Items.Select(item => item.ShortSha));
        app.Click(RowPoint(app, MainWork), app.CommandModifier);

        Assert.Equal([Sha(repository, AlphaOne), Sha(repository, AlphaTwo)], repository.Selection.Shas);
        Assert.Equal(Sha(repository, MainWork), repository.Selection.Anchor);

        // The focused row went: the focus moves to the nearest selected row (none is above it here).
        Assert.Equal(Sha(repository, AlphaTwo), repository.Selection.Focus);

        // Removing all but one leaves a single selection, with its details.
        app.Click(RowPoint(app, AlphaTwo), app.CommandModifier);
        Assert.Equal(AlphaOne, repository.SelectedIndex);
        Assert.Null(repository.MultiSelection);
        Assert.False(repository.Details.IsMultiSelection);
        await app.WaitForAsync(() => repository.Details.Subject == "Alpha one", "the details of Alpha one");
    }

    [AvaloniaFact]
    public async Task Shift_selects_from_the_anchor_and_Shift_with_the_arrow_keys_extends_from_it()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path], width: 1400, height: 900);
        var repository = await GraphShownAsync(app);

        // Check 11 step 8: click row 4, then Shift+click row 5.
        app.Click(RowPoint(app, AlphaTwo));
        app.Click(RowPoint(app, AlphaOne), KeyModifiers.Shift);
        Assert.Equal(Rows(repository, AlphaTwo, AlphaOne), Selected(repository));

        // Step 9: Shift+Down adds row 6, Shift+Up takes it away again.
        app.PressKey(Key.Down, RawInputModifiers.Shift);
        Assert.Equal(Rows(repository, AlphaTwo, AlphaOne, AddCore), Selected(repository));
        Assert.Equal(Sha(repository, AlphaTwo), repository.Selection.Anchor);
        Assert.Equal(Sha(repository, AddCore), repository.Selection.Focus);
        Assert.Equal(["ce5ea6a", "dff8fbf", "8beca34"], ((MultiSelectionViewModel)repository.MultiSelection!).Items.Select(item => item.ShortSha));
        Pictures(app, "graph-selection-shift-down");
        app.PressKey(Key.Up, RawInputModifiers.Shift);
        Assert.Equal(Rows(repository, AlphaTwo, AlphaOne), Selected(repository));

        // Shift+Up past the anchor selects upwards from it; Shift+Home stops at the first commit:
        // the stash on row 0 never joins.
        app.PressKey(Key.Up, RawInputModifiers.Shift);
        app.PressKey(Key.Up, RawInputModifiers.Shift);
        Assert.Equal(Rows(repository, AlphaTwo, MainWork), Selected(repository));
        app.PressKey(Key.Home, RawInputModifiers.Shift);
        Assert.Equal(Rows(repository, AlphaTwo, MainWork, FastForwardMe, ConflictingChange), Selected(repository));
        Assert.DoesNotContain(Sha(repository, StashRow), repository.Selection.Shas);

        // A plain arrow key moves a single selection from the focused row.
        app.PressKey(Key.Down);
        Assert.Equal(FastForwardMe, repository.SelectedIndex);
        Assert.Equal(1, repository.Selection.Count);

        // Shift+click with the command modifier adds the range to what is selected.
        app.Click(RowPoint(app, AddCore), app.CommandModifier);
        app.Click(RowPoint(app, AlphaOne), app.CommandModifier | KeyModifiers.Shift);
        Assert.Equal(Rows(repository, FastForwardMe, AlphaOne, AddCore), Selected(repository));
    }

    [AvaloniaFact]
    public async Task The_working_row_and_a_stash_never_join_a_multi_selection()
    {
        using var scenario = await Scenarios.OperationsAsync();
        scenario.WriteFile("scratch.txt", "scratch\n");
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path], width: 1400, height: 900);
        var repository = await GraphShownAsync(app);
        await app.WaitForAsync(() => repository.HasWorkingChanges, "the working-changes row");

        app.Click(RowPoint(app, AlphaTwo));
        app.Click(RowPoint(app, AlphaOne), app.CommandModifier);
        Assert.True(repository.Selection.IsMultiple);

        // Check 11 step 12: the command modifier on the stash acts as a plain click.
        app.Click(RowPoint(app, StashRow), app.CommandModifier);
        Assert.Equal(StashRow, repository.SelectedIndex);
        Assert.Equal([Sha(repository, StashRow)], repository.Selection.Shas);
        Assert.Null(repository.MultiSelection);

        // ... and so does Shift; a commit added with the command modifier starts afresh.
        app.Click(RowPoint(app, AlphaTwo), KeyModifiers.Shift);
        Assert.Equal(AlphaTwo, repository.SelectedIndex);
        app.Click(RowPoint(app, AlphaOne), app.CommandModifier);
        app.Click(RowPoint(app, StashRow), KeyModifiers.Shift);
        Assert.Equal([Sha(repository, StashRow)], repository.Selection.Shas);

        // The working-changes row with the command modifier: selected alone, the stage panel shows.
        app.Click(RowPoint(app, AlphaTwo));
        app.Click(RowPoint(app, AlphaOne), app.CommandModifier);
        app.Click(WorkingRowPoint(app), app.CommandModifier);
        Assert.True(repository.IsWorkingRowSelected);
        Assert.True(repository.Selection.IsEmpty);
        Assert.Null(repository.MultiSelection);
        Assert.False(repository.Details.IsMultiSelection);
    }

    [AvaloniaFact]
    public async Task A_right_click_keeps_a_multi_selection_it_is_part_of_and_selects_a_row_outside_it_alone()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path], width: 1400, height: 900);
        var repository = await GraphShownAsync(app);
        var graph = Graph(app);
        var asked = new List<GraphContextRequestedEventArgs>();
        graph.ContextRequestedAt += (_, e) => asked.Add(e);

        app.Click(RowPoint(app, AlphaTwo));
        app.Click(RowPoint(app, AlphaOne), KeyModifiers.Shift);

        // Check 11 step 10: right-click a selected row; the selection stays.
        app.RightClick(RowPoint(app, AlphaOne));
        Assert.Equal(Rows(repository, AlphaTwo, AlphaOne), Selected(repository));
        Assert.Equal((GraphHitKind.Commit, AlphaOne), (asked[^1].Hit.Kind, asked[^1].Hit.Row));
        Assert.Equal("2 commits selected", ((MultiSelectionViewModel)repository.MultiSelection!).Title);

        // A label of a selected row keeps it too.
        var label = graph.LabelsInRow(AlphaTwo)[0];
        app.RightClick(graph.TranslatePoint(label.Bounds.Center, app.Window)!.Value);
        Assert.Equal(Rows(repository, AlphaTwo, AlphaOne), Selected(repository));
        Assert.Equal((GraphHitKind.Label, "refs/heads/feature/alpha"), (asked[^1].Hit.Kind, asked[^1].Hit.Label!.RefName));

        // Below the last row: nothing changes.
        app.RightClick(graph.TranslatePoint(new Point(400, graph.RowBounds(repository.Graph.Count - 1).Bottom + 40), app.Window)!.Value);
        Assert.Equal(Rows(repository, AlphaTwo, AlphaOne), Selected(repository));
        Assert.Equal(GraphHitKind.None, asked[^1].Hit.Kind);

        // Outside it: that row alone.
        app.RightClick(RowPoint(app, MainWork));
        Assert.Equal(MainWork, repository.SelectedIndex);
        Assert.Equal([Sha(repository, MainWork)], repository.Selection.Shas);
        Assert.Null(repository.MultiSelection);
    }

    [AvaloniaFact]
    public async Task A_press_on_a_row_of_a_multi_selection_keeps_it_until_a_release_without_a_drag()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path], width: 1400, height: 900);
        var repository = await GraphShownAsync(app);

        app.Click(RowPoint(app, AlphaTwo));
        app.Click(RowPoint(app, AlphaOne), app.CommandModifier);

        app.PressMouse(RowPoint(app, AlphaOne));
        Assert.Equal(Rows(repository, AlphaTwo, AlphaOne), Selected(repository));
        app.ReleaseMouse(RowPoint(app, AlphaOne));

        Assert.Equal(AlphaOne, repository.SelectedIndex);
        Assert.Equal([Sha(repository, AlphaOne)], repository.Selection.Shas);
    }

    [AvaloniaFact]
    public async Task A_reload_keeps_the_selected_commits_by_their_ids_and_a_selection_whose_commits_went_ends()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path], width: 1400, height: 900);
        var repository = await GraphShownAsync(app);
        var alphaTwo = Sha(repository, AlphaTwo);
        var alphaOne = Sha(repository, AlphaOne);
        var fastForward = Sha(repository, FastForwardMe);

        app.Click(RowPoint(app, AlphaTwo));
        app.Click(RowPoint(app, AlphaOne), app.CommandModifier);

        // A commit made outside moves every row down by one: the same commits stay selected.
        var before = repository.Graph;
        await scenario.CommitFileAsync("outside.txt", "outside\n", "Outside commit");
        await repository.RefreshAsync();
        await app.WaitForAsync(() => !ReferenceEquals(repository.Graph, before), "the reloaded graph");
        Assert.Equal([alphaTwo, alphaOne], repository.Selection.Shas);
        Assert.Equal([AlphaTwo + 1, AlphaOne + 1], Selected(repository));
        Assert.Equal(["ce5ea6a", "dff8fbf"], ((MultiSelectionViewModel)repository.MultiSelection!).Items.Select(item => item.ShortSha));
        var shot = app.Capture();
        Assert.Equal(Resource(app, "VcSelectionBrush"), shot.PixelAt(BackgroundPoint(app, AlphaTwo + 1)));
        Assert.Equal(Resource(app, "VcSelectionBrush"), shot.PixelAt(BackgroundPoint(app, AlphaOne + 1)));

        // A selected commit that leaves the history is dropped; one left is the single selection.
        app.Click(RowPoint(app, FastForwardMe + 1), app.CommandModifier);
        Assert.Equal(3, repository.Selection.Count);
        before = repository.Graph;
        await scenario.GitAsync("branch", "-D", "feature/ff");
        await repository.RefreshAsync();
        await app.WaitForAsync(() => !ReferenceEquals(repository.Graph, before), "the reloaded graph");
        Assert.DoesNotContain(fastForward, repository.Selection.Shas);
        Assert.Equal(2, repository.Selection.Count);

        before = repository.Graph;
        await scenario.GitAsync("branch", "-D", "feature/alpha");
        await scenario.GitAsync("update-ref", "-d", "refs/remotes/origin/feature/alpha");
        await repository.RefreshAsync();
        await app.WaitForAsync(() => !ReferenceEquals(repository.Graph, before), "the reloaded graph");
        Assert.True(repository.Selection.IsEmpty);
        Assert.Equal(-1, repository.SelectedIndex);
        Assert.Null(repository.MultiSelection);
        Assert.True(repository.Details.ShowsPlaceholder);
    }

    [AvaloniaFact]
    public async Task A_menu_asked_for_with_the_keyboard_opens_at_the_focused_rows_message_cell()
    {
        using var scenario = await Scenarios.OperationsAsync();
        using var data = new TempDirectory("data");
        using var app = ShellDriver.StartWithTabs(data.Path, [scenario.Path], width: 1400, height: 900);
        var repository = await GraphShownAsync(app);
        var graph = Graph(app);
        var asked = new List<GraphContextRequestedEventArgs>();
        graph.ContextRequestedAt += (_, e) => asked.Add(e);

        app.Click(RowPoint(app, AlphaTwo));
        app.Click(RowPoint(app, AddCore), KeyModifiers.Shift);
        app.PressKey(Key.Apps);

        var request = Assert.Single(asked);
        Assert.Equal((GraphHitKind.Commit, AddCore), (request.Hit.Kind, request.Hit.Row));
        Assert.Equal(new Point(graph.Columns.Message.X, graph.RowBounds(AddCore).Bottom), request.Position);
        Assert.Equal(3, repository.Selection.Count);
    }

    [Fact]
    public void Reachability_from_HEAD_follows_the_parents_over_the_rows_loaded_so_far()
    {
        // HEAD on "c3"; "b2" is on a side branch; "c1" is reached only once its page arrives.
        var c1 = Commit("c1");
        var c2 = Commit("c2", "c1");
        var b2 = Commit("b2", "c1");
        var c3 = Commit("c3", "c2");
        var data = new CommitGraphData(new RepoRefs(new HeadState("main", "c3"), [new GitRef("refs/heads/main", "main", RefKind.LocalBranch, "c3", IsHead: true)], [], []));
        var layout = new GraphLayout();
        data.Append([c3, b2, c2], [layout.Add(c3), layout.Add(b2), layout.Add(c2)]);

        Assert.True(data.IsReachableFromHead("c3"));
        Assert.True(data.IsReachableFromHead("c2"));
        Assert.False(data.IsReachableFromHead("b2"));

        // "c1" is not loaded yet: not known to be reachable, so a menu leaves the pick to git.
        Assert.False(data.IsReachableFromHead("c1"));

        data.Append([c1], [layout.Add(c1)]);
        data.Complete();
        Assert.True(data.IsReachableFromHead("c1"));
        Assert.False(data.IsReachableFromHead("b2"));

        var unborn = new CommitGraphData(new RepoRefs(new HeadState("main", null), [], [], []));
        Assert.False(unborn.IsReachableFromHead("c1"));
    }

    [Fact]
    public void A_multi_selection_lists_its_commits_with_the_title_of_their_count()
    {
        var model = new MultiSelectionViewModel([new MultiSelectionItem("88d6b91e", "88d6b91", "Main work"), new MultiSelectionItem("ce5ea6ab", "ce5ea6a", "Alpha two"), new MultiSelectionItem("dff8fbf0", "dff8fbf", "Alpha one")]);

        Assert.Equal("3 commits selected", model.Title);
        Assert.Equal("88d6b91 Main work", model.Items[0].AccessibleName);
        Assert.Equal(MultiSelectionViewModel.HintText, model.Hint);
    }

    private static CommitInfo Commit(string sha, params string[] parents)
    {
        var date = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        return new CommitInfo(sha, parents, "Test Author", "author@example.com", date, date, sha);
    }

    internal static async Task<RepositoryViewModel> GraphShownAsync(ShellDriver app)
    {
        await app.WaitUntilReadyAsync();
        var repository = app.Session.Shell.ActiveTab.Repository!;
        await app.WaitForAsync(() => repository.Graph.IsComplete && repository.Graph.Count > 0, "the graph");
        await app.WaitForIdleAsync(repository);
        app.Capture();
        return repository;
    }

    internal static CommitGraphControl Graph(ShellDriver app) => app.Find<CommitGraphControl>("GraphRows");

    /// <summary>A point in a commit row's Message column, in the window.</summary>
    internal static Point RowPoint(ShellDriver app, int row)
    {
        var graph = Graph(app);
        return graph.TranslatePoint(new Point(graph.Columns.Message.X + 30, graph.RowBounds(row).Center.Y), app.Window)!.Value;
    }

    private static Point WorkingRowPoint(ShellDriver app)
    {
        var graph = Graph(app);
        return graph.TranslatePoint(new Point(graph.Columns.Message.X + 30, graph.WorkingRowBounds.Center.Y), app.Window)!.Value;
    }

    /// <summary>A point of a row's background: near the right end of the Message column, past its text.</summary>
    private static Point BackgroundPoint(ShellDriver app, int row)
    {
        var graph = Graph(app);
        return graph.TranslatePoint(new Point(graph.Columns.Message.Right - 4, graph.RowBounds(row).Y + 3), app.Window)!.Value;
    }

    private static string Sha(RepositoryViewModel repository, int row) => repository.Graph.CommitAt(row).Sha;

    private static List<int> Rows(RepositoryViewModel repository, params int[] rows) => [.. rows.Order()];

    /// <summary>The selected rows, in row order.</summary>
    private static List<int> Selected(RepositoryViewModel repository) =>
        [.. repository.Selection.Shas.Select(repository.Graph.IndexOf).Order()];

    private static Color Resource(ShellDriver app, string key) =>
        app.Window.TryFindResource(key, app.Window.ActualThemeVariant, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key}.");

    internal static void Pictures(ShellDriver app, string name)
    {
        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            app.Window.RequestedThemeVariant = theme;
            app.Settle();
            TestPictures.Save(app, string.Create(CultureInfo.InvariantCulture, $"{name}-{(theme == ThemeVariant.Dark ? "dark" : "light")}"));
        }

        app.Window.RequestedThemeVariant = ThemeVariant.Dark;
        app.Settle();
    }
}
