using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Controls.Conflicts;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Conflicts;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.Views.Conflicts;
using VisualCommit.Core.Git;
using VisualCommit.Testing.Headless;
using Xunit;
using static VisualCommit.App.Tests.ResolverFixtures;

namespace VisualCommit.App.Tests;

/// <summary>
/// Headless tests of the conflict resolver's view (<see cref="ConflictResolverView"/>,
/// docs/phase-4-design.md 11.1, 11.2, 11.5, 11.10, 11.12, 7.4; the report's part C) hosted alone
/// in a window the size the graph and right-panel columns have at 1100×700 with a one-line banner
/// (840×550): its layout in pixels, the automation ids, the bars' buttons and states, the focus,
/// the resolver's own undo and redo, Esc, the keys, the tab order, its menus, the card, a tab
/// switch, and the double-click guard of 11.1.
/// </summary>
public class ConflictResolverViewTests
{
    private const double Width = 840;
    private const double Height = 550;

    private static readonly Color Chrome = Color.Parse("#20242C");
    private static readonly Color Border = Color.Parse("#323845");
    private static readonly Color Background = Color.Parse("#14161B");
    private static readonly Color Ours = Color.Parse("#15323A");
    private static readonly Color Theirs = Color.Parse("#2C2140");
    private static readonly Color Marker = Color.Parse("#3A2E16");
    private static readonly Color Used = Color.Parse("#1A2E24");
    private static readonly Color Hunk = Color.Parse("#1C2130");
    private static readonly Color Danger = Color.Parse("#E5606B");
    private static readonly Color Accent = Color.Parse("#7C8CFF");
    private static readonly Color TextSecondary = Color.Parse("#9BA3B0");
    private static readonly Color TextDisabled = Color.Parse("#5F6775");

    // --- Layout (11.2; the report's part C, "The resolver") ------------------------------------

    [AvaloniaFact]
    public async Task The_resolver_s_layout_matches_the_report_at_1100_by_700()
    {
        using var host = await ResolverHost.OpenMergeAsync();
        var view = host.View;
        var shot = host.Capture();
        TestPictures.Save(host.Host, "resolver-merge-dark");

        // Header and toolbar 36 high each, across the whole width.
        Assert.Equal(new Rect(0, 0, 840, 36), Bounds(host, "ResolverHeader"));
        Assert.Equal(new Rect(0, 36, 840, 36), Bounds(host, "ResolverToolbar"));

        // The halves split the rest equally with a 1-px line between: (550 - 72 - 1) / 2 = 238.5.
        var oursTitle = Bounds(host, "OursTitleStrip");
        var theirsTitle = Bounds(host, "TheirsTitleStrip");
        // Layout rounding puts each part on whole pixels: within one of the design's numbers.
        AssertNear(new Rect(0, 72, 419.5, 26), oursTitle, "the ours title strip");
        AssertNear(new Rect(420.5, 72, 419.5, 26), theirsTitle, "the theirs title strip");
        var resultTitle = Bounds(host, "ResultTitleStrip");
        AssertNear(new Rect(0, 72 + 238.5 + 1, 840, 26), resultTitle, "the result's title strip");
        var result = host.BoundsOf(view.ResultEditor);
        Assert.Equal(resultTitle.Bottom, result.Top, 0.5);
        Assert.Equal(212.5, result.Height, 1.0);
        AssertNear(new Rect(0, 98, 840, 212.5), host.BoundsOf(view.ConflictPanes), "the panes");
        Assert.Equal(Height, result.Bottom, 0.5);

        // The texts and automation ids of 7.4.
        Assert.Equal("U", Text(host, "ResolverFileStatus"));
        Assert.Equal("notes.txt", Text(host, "ResolverFileName"));
        Assert.Equal(string.Empty, Text(host, "ResolverFolder"));
        Assert.Equal("Both modified", Text(host, "ResolverKind"));
        Assert.Equal("File 1 of 1", Text(host, "ResolverFilePosition"));
        Assert.Equal("Conflict 1 of 2", Text(host, "ResolverConflictPosition"));
        Assert.Equal("Ours: main", Text(host, "ConflictOursTitle"));
        Assert.Equal("Theirs: feature/theirs", Text(host, "ConflictTheirsTitle"));
        Assert.Equal("Result", Text(host, "ConflictResultTitle"));
        Assert.Equal("2 conflicts left", Text(host, "ConflictResultCount"));
        Assert.Equal("Take ours", ((Button)host.Find("ResolverTakeOurs")).Content);
        Assert.Equal("Take theirs", ((Button)host.Find("ResolverTakeTheirs")).Content);
        Assert.Equal("Merge tool…", ((Button)host.Find("ResolverMergeTool")).Content);
        Assert.Equal("Mark resolved", ((Button)host.Find("ResolverMarkResolved")).Content);
        foreach (var id in new[] { "ResolverPreviousFile", "ResolverNextFile", "ResolverCloseButton", "ResolverPreviousConflict", "ResolverNextConflict", "ConflictOurs", "ConflictTheirs", "ConflictResult" })
        {
            Assert.NotNull(host.Find(id));
        }

        // Merge tool… is disabled with its reason as its tooltip; Mark resolved is the primary button at the right.
        var mergeTool = (Button)host.Find("ResolverMergeTool");
        Assert.False(mergeTool.IsEffectivelyEnabled);
        Assert.Equal("Set merge.tool in git's configuration to use a merge tool", ToolTip.GetTip(mergeTool));
        Assert.True(ToolTip.GetShowOnDisabled(mergeTool));
        var markResolved = host.Find("ResolverMarkResolved");
        Assert.Equal(840 - 8, host.BoundsOf(markResolved).Right, 0.5);
        var markBounds = host.BoundsOf(markResolved);
        DiffColors.AssertColor(Accent, shot.PixelAt(new Point(markBounds.Left + 4, markBounds.Center.Y)), "Mark resolved's background");

        // The status letter in the danger colour; the strips in the chrome colour; the 1-px lines.
        DiffColors.AssertBrush(Danger, ((TextBlock)host.Find("ResolverFileStatus")).Foreground, "U");
        DiffColors.AssertColor(Chrome, shot.PixelAt(new Point(oursTitle.Right - 4, oursTitle.Top + 4)), "the ours title strip");
        DiffColors.AssertColor(Border, shot.PixelAt(new Point(420, 200)), "the line between the panes");
        DiffColors.AssertColor(Border, shot.PixelAt(new Point(600, 72 + 238.5 + 0.5)), "the line between the halves");

        // The panes: PM, a bar before each block; both bars in view; one number column, no sign column.
        var oursRows = view.ConflictPanes.RowsOf(ConflictPaneSide.Ours);
        Assert.Equal(14, oursRows.Count);
        Assert.Equal(
            [(1, "line 1"), (2, "line 2 ours"), (3, "line 3"), (4, "line 4"), (5, "line 5"), (6, "line 6 theirs"), (7, "line 7"), (8, "line 8"), (9, "line 9"), (10, "line 10 ours"), (11, "line 11"), (12, "line 12")],
            oursRows.Where(row => !row.IsBar).Select(row => (row.Number ?? 0, row.Text)));
        Assert.Equal([1, 10], oursRows.Select((row, index) => (row, index)).Where(pair => pair.row.IsBar).Select(pair => pair.index));
        Assert.Equal(ConflictTint.Ours, oursRows[2].Tint);
        Assert.Equal(ConflictTint.Theirs, view.ConflictPanes.RowsOf(ConflictPaneSide.Theirs)[2].Tint);
        Assert.NotNull(view.ConflictPanes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs));
        Assert.NotNull(view.ConflictPanes.BarButton(1, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs));
        Assert.Equal(30.4, view.ConflictPanes.TextLeft(ConflictPaneSide.Ours)!.Value, 1.0);

        // Opening on block 1 scrolls nothing: each pane's first row is line 1, the result's first line is line 1.
        var firstRow = view.ConflictPanes.RowBounds(ConflictPaneSide.Ours, 0)!.Value;
        Assert.Equal(0, firstRow.Y, 0.5);
        Assert.Equal(0, view.ResultEditor.ScrollOffset.Y, 0.5);

        // Tints, sampled where no text reaches.
        DiffColors.AssertColor(Ours, shot.PixelAt(host.ToWindow(view.ConflictPanes, new Point(400, view.ConflictPanes.RowBounds(ConflictPaneSide.Ours, 2)!.Value.Center.Y))), "ours line 2");
        DiffColors.AssertColor(Theirs, shot.PixelAt(host.ToWindow(view.ConflictPanes, new Point(820, view.ConflictPanes.RowBounds(ConflictPaneSide.Theirs, 2)!.Value.Center.Y))), "theirs line 2");
        DiffColors.AssertColor(Hunk, shot.PixelAt(host.ToWindow(view.ConflictPanes, new Point(400, view.ConflictPanes.RowBounds(ConflictPaneSide.Ours, 1)!.Value.Y + 1))), "block 1's bar");
        foreach (var (line, color, what) in new[] { (1, Background, "line 1"), (2, Marker, "the < line"), (3, Ours, "ours"), (4, Marker, "the = line"), (5, Theirs, "theirs"), (6, Marker, "the > line") })
        {
            var y = view.ResultEditor.PointAt(line, 1)!.Value.Y;
            DiffColors.AssertColor(color, shot.PixelAt(host.ToWindow(view.ResultEditor, new Point(820, y))), $"the result's {what}");
        }

        // The focus in the result, without a focus outline, the caret at the start of line 2.
        Assert.True(view.ResultEditor.TextArea.IsFocused);
        Assert.DoesNotContain(":focus-visible", view.ResultEditor.TextArea.Classes);
        Assert.Equal(new AvaloniaEdit.Document.TextLocation(2, 1), view.ResultEditor.CaretPosition);

        // No text cut but the titles a tooltip shows whole.
        Assert.Empty(LayoutAudit.FindClippedText(host.Host.Window, allowShortenedWithToolTip: true, allowScrolledOutOfView: true));
    }

    [AvaloniaFact]
    public async Task UI_Automation_finds_the_resolver_and_reads_the_result_s_text()
    {
        using var host = await ResolverHost.OpenMergeAsync();

        var root = ControlAutomationPeer.CreatePeerForElement(host.View);
        Assert.Equal("ConflictResolver", root.GetAutomationId());
        Assert.True(root.IsControlElement());
        var result = ControlAutomationPeer.CreatePeerForElement(host.View.ResultEditor);
        Assert.Equal("ConflictResult", result.GetAutomationId());
        Assert.Equal(Notes, result.GetProvider<IValueProvider>()!.Value);
    }

    // --- Choosing per block (11.5) -----------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_bar_s_button_chooses_shows_the_bars_states_and_gives_the_focus_to_the_result()
    {
        using var host = await ResolverHost.OpenMergeAsync();
        var panes = host.View.ConflictPanes;
        var button = panes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs)!;
        host.Host.Click(button, new Point(button.Bounds.Width / 2, button.Bounds.Height / 2));
        var shot = host.Capture();
        TestPictures.Save(host.Host, "resolver-block1-ours");

        Assert.Equal(R1, host.View.ResultEditor.Text);
        Assert.Equal("1 conflict left", Text(host, "ConflictResultCount"));
        Assert.Equal("Conflict 1 of 2", Text(host, "ResolverConflictPosition"));
        Assert.Equal("Used in the result", panes.BarText(0, ConflictPaneSide.Ours));
        Assert.Equal("Not used", panes.BarText(0, ConflictPaneSide.Theirs));
        Assert.Null(panes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs));
        Assert.NotNull(panes.BarButton(1, ConflictPaneSide.Ours, ConflictChoice.UseOurs));
        Assert.Equal(ConflictTint.Used, panes.RowsOf(ConflictPaneSide.Ours)[2].Tint);
        Assert.Equal(ConflictTint.None, panes.RowsOf(ConflictPaneSide.Theirs)[2].Tint);
        Assert.Equal(ConflictSideState.Used, panes.RowsOf(ConflictPaneSide.Ours)[1].BarState);

        // The bar's text has its automation id, in the secondary colour.
        var states = host.View.GetVisualDescendants().OfType<TextBlock>().Where(text => AutomationProperties.GetAutomationId(text) == "ConflictBarState").ToList();
        Assert.Equal(["Not used", "Used in the result"], states.Select(text => text.Text).Order());
        DiffColors.AssertBrush(Color.Parse("#9BA3B0"), states[0].Foreground, "a bar's state");

        DiffColors.AssertColor(Used, shot.PixelAt(host.ToWindow(panes, new Point(400, panes.RowBounds(ConflictPaneSide.Ours, 2)!.Value.Center.Y))), "ours line 2, used");
        DiffColors.AssertColor(Background, shot.PixelAt(host.ToWindow(panes, new Point(820, panes.RowBounds(ConflictPaneSide.Theirs, 2)!.Value.Center.Y))), "theirs line 2, not used");
        DiffColors.AssertColor(Background, shot.PixelAt(host.ToWindow(host.View.ResultEditor, new Point(820, host.View.ResultEditor.PointAt(2, 1)!.Value.Y))), "the chosen line in the result");

        Assert.True(host.View.ResultEditor.TextArea.IsFocused);
        Assert.DoesNotContain(":focus-visible", host.View.ResultEditor.TextArea.Classes);
        Assert.Equal(new AvaloniaEdit.Document.TextLocation(2, 1), host.View.ResultEditor.CaretPosition);
    }

    [AvaloniaFact]
    public async Task The_next_conflict_scrolls_block_2_into_view_in_the_panes_and_the_result()
    {
        using var host = await ResolverHost.OpenMergeAsync(height: 400);
        var panes = host.View.ConflictPanes;
        Assert.Null(panes.RowBounds(ConflictPaneSide.Ours, 13));

        host.Host.Click(host.Find("ResolverNextConflict"), new Point(11, 11));
        host.Capture();

        Assert.Equal("Conflict 2 of 2", Text(host, "ResolverConflictPosition"));
        Assert.Equal(new AvaloniaEdit.Document.TextLocation(14, 1), host.View.ResultEditor.CaretPosition);
        Assert.NotNull(panes.BarButton(1, ConflictPaneSide.Ours, ConflictChoice.UseOurs));
        Assert.NotNull(panes.BarButton(1, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs));
        foreach (var line in Enumerable.Range(14, 5))
        {
            Assert.NotNull(host.View.ResultEditor.PointAt(line, 1));
        }

        // The < line is the third row of the result in view.
        Assert.Equal(11 * 15.84, host.View.ResultEditor.ScrollOffset.Y, 1.0);
    }

    // --- Undo, redo, Esc and the keys (11.5, 11.10) ----------------------------------------------------

    [AvaloniaFact]
    public async Task The_platform_s_undo_and_redo_are_the_resolver_s_wherever_the_focus_is_in_it()
    {
        using var host = await ResolverHost.OpenMergeAsync();
        var button = host.View.ConflictPanes.BarButton(1, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs)!;
        host.Host.Click(button, new Point(4, 4));
        Assert.EndsWith("line 9\nline 10 theirs\nline 11\nline 12\n", host.View.ResultEditor.Text, StringComparison.Ordinal);

        // The focus on a toolbar button: the platform's undo still undoes the result.
        host.Find("ResolverTakeOurs").Focus();
        host.Press(host.Keys.Undo[0]);
        Assert.Equal(Notes, host.View.ResultEditor.Text);
        Assert.Equal("Use theirs", host.View.ConflictPanes.BarButton(1, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs)?.Content);

        host.View.ResultEditor.TextArea.Focus();
        host.Press(host.Keys.Redo[0]);
        Assert.EndsWith("line 10 theirs\nline 11\nline 12\n", host.View.ResultEditor.Text, StringComparison.Ordinal);
        Assert.Equal("Used in the result", host.View.ConflictPanes.BarText(1, ConflictPaneSide.Theirs));
    }

    [AvaloniaFact]
    public async Task Esc_does_nothing_inside_the_result_and_closes_the_resolver_elsewhere_asking_for_a_modified_result()
    {
        using var host = await ResolverHost.OpenMergeAsync();
        var closed = 0;
        host.Resolver.Closed += (_, _) => closed++;
        host.Host.Click(host.View.ResultEditor, host.View.ResultEditor.PointAt(3, 5)!.Value);
        host.Host.Window.KeyTextInput("x");
        ResolverFixtures.Settle();

        host.Host.PressKey(Key.Escape);
        Assert.Equal(0, closed);
        Assert.True(host.Esc[^1]);
        Assert.Empty(host.Actions.Questions);
        Assert.True(host.View.ResultEditor.TextArea.IsFocused);

        host.Find("ResolverTakeOurs").Focus();
        host.Actions.Answer(false);
        host.Host.PressKey(Key.Escape);
        ResolverFixtures.Settle();
        Assert.Equal("Discard your changes to notes.txt?", Assert.Single(host.Actions.Questions).Title);
        Assert.Equal(0, closed);

        host.Actions.Answer(true);
        host.Host.PressKey(Key.Escape);
        ResolverFixtures.Settle();
        Assert.Equal(1, closed);

        // A closed resolver declines Esc.
        Assert.False(host.View.HandleEscape());
    }

    [AvaloniaFact]
    public async Task Alt_with_the_arrows_goes_to_the_conflicts_and_the_files()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("a.txt", Notes);
        actions.SetText("b.txt", Notes);
        using var host = await ResolverHost.OpenAsync(actions, Status(Merge(), Entry("a.txt"), Entry("b.txt")), "a.txt");

        host.Host.PressKey(Key.Down, RawInputModifiers.Alt);
        Assert.Equal("Conflict 2 of 2", Text(host, "ResolverConflictPosition"));
        host.Host.PressKey(Key.Up, RawInputModifiers.Alt);
        Assert.Equal("Conflict 1 of 2", Text(host, "ResolverConflictPosition"));

        host.Host.PressKey(Key.Right, RawInputModifiers.Alt);
        ResolverFixtures.Settle();
        Assert.Equal(("b.txt", "File 2 of 2"), (Text(host, "ResolverFileName"), Text(host, "ResolverFilePosition")));
        host.Host.PressKey(Key.Left, RawInputModifiers.Alt);
        ResolverFixtures.Settle();
        Assert.Equal("a.txt", Text(host, "ResolverFileName"));
    }

    [AvaloniaFact]
    public async Task A_disabled_arrow_is_drawn_in_the_disabled_text_colour_as_the_toolbar_s_buttons()
    {
        string[] arrows = ["ResolverPreviousFile", "ResolverNextFile", "ResolverPreviousConflict", "ResolverNextConflict"];

        // One conflicted file: the file arrows have nowhere to go; the conflict arrows do.
        using (var single = await ResolverHost.OpenMergeAsync())
        {
            AssertArrows(single, ["ResolverPreviousConflict", "ResolverNextConflict"], ["ResolverPreviousFile", "ResolverNextFile"]);
        }

        // Two files: all four are enabled, in the secondary colour.
        var actions = new RecordingConflictActions();
        actions.SetText("a.txt", Notes);
        actions.SetText("b.txt", Notes);
        using var host = await ResolverHost.OpenAsync(actions, Status(Merge(), Entry("a.txt"), Entry("b.txt")), "a.txt");
        AssertArrows(host, arrows, []);

        // While Mark resolved writes, all four are disabled, in the disabled colour.
        actions.Hold = new TaskCompletionSource();
        actions.Answer(true);
        var marking = host.Resolver.MarkResolvedCommand.ExecuteAsync(null);
        ResolverFixtures.Settle();
        Assert.True(host.Resolver.IsBusy);
        AssertArrows(host, [], arrows);

        actions.Release();
        await marking;

        static void AssertArrows(ResolverHost host, string[] enabled, string[] disabled)
        {
            foreach (var (id, isEnabled) in enabled.Select(id => (id, true)).Concat(disabled.Select(id => (id, false))))
            {
                var button = (Button)host.Find(id);
                Assert.True(button.IsEffectivelyEnabled == isEnabled, $"{id} is {(button.IsEffectivelyEnabled ? "enabled" : "disabled")}.");
                var icon = button.GetVisualDescendants().OfType<VisualCommit.App.Controls.Icon>().Single();
                DiffColors.AssertBrush(isEnabled ? TextSecondary : TextDisabled, icon.Foreground, $"{id}'s arrow, {(isEnabled ? "enabled" : "disabled")}");
            }
        }
    }

    [AvaloniaFact]
    public async Task Tab_goes_through_the_header_the_toolbar_the_bars_and_the_result_in_11_10_s_order()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("a.txt", Notes);
        actions.SetText("b.txt", Notes);
        using var host = await ResolverHost.OpenAsync(actions, Status(Merge(), Entry("a.txt"), Entry("b.txt")), "a.txt", mergeTool: "fake");
        host.Find("ResolverPreviousFile").Focus(NavigationMethod.Tab);

        var order = new List<string?> { Id(host.Focused) };
        for (var press = 0; press < 16; press++)
        {
            host.Host.PressKey(Key.Tab);
            order.Add(Id(host.Focused));
        }

        Assert.Equal(
            [
                "ResolverPreviousFile", "ResolverNextFile", "ResolverCloseButton",
                "ResolverPreviousConflict", "ResolverNextConflict", "ResolverTakeOurs", "ResolverTakeTheirs", "ResolverMergeTool", "ResolverMarkResolved",
                "ConflictUseOurs", "ConflictUseBothOursFirst", "ConflictUseOurs", "ConflictUseBothOursFirst",
                "ConflictUseTheirs", "ConflictUseBothTheirsFirst", "ConflictUseTheirs", "ConflictUseBothTheirsFirst",
            ],
            order.Take(17));

        // Then the result; inside it Tab types a tab and Ctrl+Tab leaves it.
        host.Host.PressKey(Key.Tab);
        Assert.True(host.View.ResultEditor.TextArea.IsFocused);
        var caret = host.View.ResultEditor.CaretOffset;
        host.Host.PressKey(Key.Tab);
        Assert.Equal('\t', host.View.ResultEditor.Text[caret]);
        Assert.Equal(Notes.Length + 1, host.View.ResultEditor.Text.Length);
        host.Host.PressKey(Key.Tab, RawInputModifiers.Control);
        Assert.False(host.View.ResultEditor.TextArea.IsKeyboardFocusWithin);

        static string? Id(IInputElement? element) => element is Control control
            ? AutomationProperties.GetAutomationId(control) ?? AutomationProperties.GetAutomationId(control.FindAncestorOfType<ConflictResultEditor>() ?? control) ?? control.GetType().Name + "<" + string.Join("<", control.GetVisualAncestors().Take(4).Select(a => a.GetType().Name))
            : null;
    }

    // --- Menus (11.12, 14.3) ----------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_right_click_opens_the_result_s_menu_a_pane_s_menu_or_the_file_s_menu_at_the_pointer()
    {
        using var host = await ResolverHost.OpenMergeAsync();
        MenuModel fileMenu = RepositoryMenus.Build(new MenuHeader("notes.txt"), [new ViewModels.Menus.MenuItem("Resolve conflict", () => { })]);
        host.Resolver.FileMenu = file => file == new ChangedFile("notes.txt", FileChangeKind.Conflicted) ? fileMenu : null;
        var layer = host.Shell.Menu;

        var inResult = host.ToWindow(host.View.ResultEditor, host.View.ResultEditor.PointAt(5, 1)!.Value);
        host.RightClick(inResult);
        Assert.Equal("notes.txt (result)", layer.Current?.HeaderText);
        Assert.Equal(["Undo", "Redo", "Cut", "Copy", "Paste", "Select all"], layer.Current!.Items.Select(item => item.Label));
        Assert.Equal([false, false, false, false, true, true], layer.Current.Items.Select(item => item.IsEnabled));
        Assert.Equal(new MenuPosition(inResult.X, inResult.Y), layer.Position);
        Assert.Equal(MenuOpenedBy.Pointer, layer.OpenedBy);
        layer.Close();

        // After a choice there is something to undo: Undo is enabled, and it undoes.
        host.Host.Click(host.View.ConflictPanes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs)!, new Point(4, 4));
        host.RightClick(inResult);
        var undo = layer.Current!.Items.First();
        Assert.True(undo.IsEnabled);
        undo.Invoke();
        Assert.Equal(Notes, host.View.ResultEditor.Text);
        layer.Close();

        var inOurs = host.ToWindow(host.View.ConflictPanes, new Point(100, host.View.ConflictPanes.RowBounds(ConflictPaneSide.Ours, 0)!.Value.Center.Y));
        host.RightClick(inOurs);
        Assert.Equal("Ours: main", layer.Current?.HeaderText);
        Assert.Equal(["Copy"], layer.Current!.Items.Select(item => item.Label));
        Assert.False(layer.Current.Items.Single().IsEnabled);
        layer.Close();

        var inTheirs = host.ToWindow(host.View.ConflictPanes, new Point(600, host.View.ConflictPanes.RowBounds(ConflictPaneSide.Theirs, 0)!.Value.Center.Y));
        host.RightClick(inTheirs);
        Assert.Equal("Theirs: feature/theirs", layer.Current?.HeaderText);
        layer.Close();

        host.RightClick(host.BoundsOf(host.Find("ResolverFileName")).Center);
        Assert.Same(fileMenu, layer.Current);
        layer.Close();

        // The toolbar has no menu.
        host.RightClick(host.BoundsOf(host.Find("ResolverConflictPosition")).Center);
        Assert.Null(layer.Current);
    }

    // --- The card (11.8) --------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_conflict_without_text_shows_its_card_in_the_middle_with_an_empty_toolbar_row()
    {
        var actions = new RecordingConflictActions();
        actions.Contents["other.txt"] = new ConflictContent(Encoding.UTF8.GetBytes("other file\nchanged on main\n"), 7, false, false, false, true);
        using var host = await ResolverHost.OpenAsync(actions, Status(Merge("feature/kinds"), Entry("other.txt", ConflictKind.DeletedByThem)), "other.txt");
        host.Capture();
        TestPictures.Save(host.Host, "resolver-card-dark");

        var card = host.BoundsOf(host.Find("ConflictCard"));
        Assert.True(ControlAutomationPeer.CreatePeerForElement(host.Find("ConflictCard")).IsControlElement());
        Assert.Equal(420, card.Center.X, 1.0);
        Assert.Equal(72 + ((Height - 72) / 2), card.Center.Y, 1.0);
        Assert.Equal("main changed other.txt; feature/kinds deleted it.", Text(host, "ConflictCardText"));
        Assert.Equal("Keep the file", ((Button)host.Find("ConflictKeepFile")).Content);
        Assert.True(host.Find("ConflictKeepFile").IsFocused);
        Assert.DoesNotContain(":focus-visible", host.Find("ConflictKeepFile").Classes);
        Assert.Equal("Delete it", ((Button)host.Find("ConflictDeleteFile")).Content);
        Assert.Equal(new Rect(0, 36, 840, 36), Bounds(host, "ResolverToolbar"));
        foreach (var id in new[] { "ResolverConflictPosition", "ResolverTakeOurs", "ResolverTakeTheirs", "ResolverMergeTool", "ResolverMarkResolved", "ResolverText" })
        {
            Assert.False(host.Find(id).IsEffectivelyVisible, id);
        }

        host.Host.Click(host.Find("ConflictKeepFile"), new Point(10, 10));
        ResolverFixtures.Settle();
        Assert.Equal(["TakeSide other.txt Ours"], actions.Writes);
    }

    // --- A tab switch (11.1) -----------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task Another_tab_s_resolver_in_the_view_keeps_each_one_s_result_caret_and_undo()
    {
        using var host = await ResolverHost.OpenMergeAsync();
        var first = host.Resolver;
        host.Host.Click(host.View.ConflictPanes.BarButton(0, ConflictPaneSide.Ours, ConflictChoice.UseOurs)!, new Point(4, 4));
        host.Host.Click(host.View.ResultEditor, host.View.ResultEditor.PointAt(9, 3)!.Value);

        var otherActions = new RecordingConflictActions();
        otherActions.SetText("notes.txt", Notes);
        var second = Resolver(otherActions, new ManualDelays(), Status(Merge(), Entry("notes.txt")));
        await second.OpenAsync("notes.txt");
        host.View.DataContext = second;
        host.Capture();
        Assert.Equal(Notes, host.View.ResultEditor.Text);

        host.View.DataContext = first;
        host.Capture();
        Assert.Equal(R1, host.View.ResultEditor.Text);
        Assert.Equal(new AvaloniaEdit.Document.TextLocation(9, 3), host.View.ResultEditor.CaretPosition);
        Assert.True(first.Undo());
        Assert.Equal(Notes, host.View.ResultEditor.Text);
    }

    [AvaloniaFact]
    public async Task While_a_write_runs_the_result_is_read_only_and_the_bars_are_disabled()
    {
        using var host = await ResolverHost.OpenMergeAsync();
        host.Actions.Hold = new TaskCompletionSource();
        host.Actions.Answer(true);

        var marking = host.Resolver.MarkResolvedCommand.ExecuteAsync(null);
        ResolverFixtures.Settle();

        Assert.True(host.View.ResultEditor.IsReadOnly);
        Assert.False(host.View.ConflictPanes.IsEffectivelyEnabled);
        Assert.False(host.Find("ResolverMarkResolved").IsEffectivelyEnabled);

        host.Actions.Release();
        await marking;
    }

    [AvaloniaFact]
    public async Task Esc_does_nothing_while_mark_resolved_or_the_merge_tool_runs()
    {
        var actions = new RecordingConflictActions();
        actions.SetText("a.txt", Notes);
        actions.SetText("b.txt", Notes);
        using var host = await ResolverHost.OpenAsync(actions, Status(Merge(), Entry("a.txt"), Entry("b.txt")), "a.txt", mergeTool: "fake");
        var closed = 0;
        host.Resolver.Closed += (_, _) => closed++;

        // 14.4: while a write runs, Esc is disabled as the close button is; the focus is outside the result.
        host.Find("ResolverTakeOurs").Focus();
        actions.Hold = new TaskCompletionSource();
        actions.Answer(true);
        var marking = host.Resolver.MarkResolvedCommand.ExecuteAsync(null);
        ResolverFixtures.Settle();
        Assert.True(host.Resolver.IsBusy);
        Assert.False(host.View.ResultEditor.TextArea.IsKeyboardFocusWithin);

        host.Host.PressKey(Key.Escape);
        ResolverFixtures.Settle();
        Assert.True(host.Esc[^1]);
        Assert.Equal((0, "a.txt"), (closed, host.Resolver.Path));
        await host.Resolver.CloseResolverCommand.ExecuteAsync(null);
        Assert.Equal(0, closed);

        // The write ends: the resolver moves on to b.txt as it would have.
        actions.BeforeWriteEnds = _ => host.Resolver.Update(Status(Merge(), Entry("b.txt")), "main", null);
        actions.Release();
        await marking;
        Assert.Equal((0, "b.txt"), (closed, host.Resolver.Path));

        // The merge tool likewise: Esc leaves the resolver waiting for the tool.
        actions.Hold = new TaskCompletionSource();
        actions.WritesResolve = false;
        var tool = host.Resolver.RunMergeToolCommand.ExecuteAsync(null);
        ResolverFixtures.Settle();
        Assert.Equal("Waiting for fake to close…", host.Resolver.ResultTitle);
        host.Host.PressKey(Key.Escape);
        ResolverFixtures.Settle();
        Assert.Equal((0, "b.txt", true), (closed, host.Resolver.Path, host.Resolver.IsBusy));

        actions.Release();
        await tool;
        Assert.Equal(("Result", false), (host.Resolver.ResultTitle, host.Resolver.IsBusy));

        // Idle again, Esc outside the result closes it.
        host.Find("ResolverTakeOurs").Focus();
        host.Host.PressKey(Key.Escape);
        ResolverFixtures.Settle();
        Assert.Equal(1, closed);
    }

    // --- The double-click guard (11.1) ---------------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_double_click_s_second_press_within_500_ms_of_opening_changes_nothing_and_after_500_ms_the_same_press_chooses()
    {
        // The guard's clock moves only when the test moves it, so a slow machine cannot run past
        // the 500 ms between the reopening and the press.
        var time = new ManualTime();
        using var host = await ResolverHost.OpenMergeAsync(time: time);
        var button = host.View.ConflictPanes.BarButton(0, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs)!;
        var left = host.BoundsOf(button).TopLeft;
        var point = left + new Point(3, 11);

        // The first press of the double-click lands outside the resolver (the stage panel, in the
        // app): here, the resolver is hidden while it is pressed. Then the resolver opens and the
        // second press, within the double-click time and size, lands on its Use theirs.
        host.View.IsVisible = false;
        ResolverFixtures.Settle();
        host.Host.Window.MouseDown(point, MouseButton.Left);
        host.Host.Window.MouseUp(point, MouseButton.Left);
        host.View.IsVisible = true;
        await host.Resolver.OpenAsync("notes.txt");
        ResolverFixtures.Settle();
        Assert.True(host.Resolver.IsInDoubleClickGuard);

        // The panes are laid out again for the reopened file; wait until the button is under the point.
        Button? Hit() => (host.Host.Window.InputHitTest(point) as Visual)?.FindAncestorOfType<Button>(includeSelf: true);
        for (var tries = 0; tries < 200 && !ReferenceEquals(Hit(), host.View.ConflictPanes.BarButton(0, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs)); tries++)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
            ResolverFixtures.Settle();
        }

        Assert.Same(host.View.ConflictPanes.BarButton(0, ConflictPaneSide.Theirs, ConflictChoice.UseTheirs), Hit());
        Assert.True(host.Resolver.IsInDoubleClickGuard);
        var counts = new List<int>();
        host.View.AddHandler(InputElement.PointerPressedEvent, (_, e) => counts.Add(e.ClickCount), RoutingStrategies.Tunnel, handledEventsToo: true);
        host.Host.Window.MouseDown(point, MouseButton.Left);
        host.Host.Window.MouseUp(point, MouseButton.Left);
        ResolverFixtures.Settle();

        Assert.Equal([2], counts);
        Assert.Equal(Notes, host.View.ResultEditor.Text);
        Assert.False(host.Resolver.IsModified);
        Assert.Empty(host.Actions.Writes);

        // After 500 ms the same press, the second of a double-click whose first press was on the bar
        // beside the button, chooses.
        time.Advance(ConflictResolverViewModel.DoubleClickGuard + TimeSpan.FromMilliseconds(50));
        ResolverFixtures.Settle();
        Assert.False(host.Resolver.IsInDoubleClickGuard);
        var beside = left + new Point(-1, 11);
        var on = left + new Point(0.5, 11);
        host.Host.Window.MouseDown(beside, MouseButton.Left);
        host.Host.Window.MouseUp(beside, MouseButton.Left);
        host.Host.Window.MouseDown(on, MouseButton.Left);
        host.Host.Window.MouseUp(on, MouseButton.Left);
        ResolverFixtures.Settle();

        Assert.Equal(2, counts[^1]);
        Assert.StartsWith("line 1\nline 2 theirs\nline 3\n", host.View.ResultEditor.Text, StringComparison.Ordinal);
    }

    private static Rect Bounds(ResolverHost host, string name) => host.BoundsOf(host.Find(name));

    private static void AssertNear(Rect expected, Rect actual, string what)
    {
        Assert.True(
            Math.Abs(expected.X - actual.X) <= 1 && Math.Abs(expected.Y - actual.Y) <= 1 && Math.Abs(expected.Width - actual.Width) <= 1 && Math.Abs(expected.Height - actual.Height) <= 1,
            $"{what}: expected {expected}, found {actual}.");
    }

    private static string? Text(ResolverHost host, string id) => ((TextBlock)host.Find(id)).Text;

    /// <summary>
    /// The resolver's view alone in a window 840 wide (the graph and right-panel columns at
    /// 1100×700), dark, whose data context is a shell, so that the view's menus open on the shell's
    /// menu layer; Esc goes to the view's handler as the main window's chain sends it.
    /// </summary>
    internal sealed class ResolverHost : IDisposable
    {
        private ResolverHost(DiffHost host, ConflictResolverView view, ConflictResolverViewModel resolver, RecordingConflictActions actions, MainWindowViewModel shell)
        {
            Host = host;
            View = view;
            Resolver = resolver;
            Actions = actions;
            Shell = shell;
            host.Window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
            {
                if (e.Key == Key.Escape && !e.Handled)
                {
                    var handled = view.HandleEscape();
                    Esc.Add(handled);
                    e.Handled = handled;
                }
            }, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        public DiffHost Host { get; }

        public ConflictResolverView View { get; }

        public ConflictResolverViewModel Resolver { get; }

        public RecordingConflictActions Actions { get; }

        public MainWindowViewModel Shell { get; }

        /// <summary>What the view's Esc handler answered, press by press.</summary>
        public List<bool> Esc { get; } = [];

        public IInputElement? Focused => Host.Window.FocusManager?.GetFocusedElement();

        public Avalonia.Input.Platform.PlatformHotkeyConfiguration Keys => Host.Window.GetPlatformSettings()!.HotkeyConfiguration;

        public static Task<ResolverHost> OpenMergeAsync(double height = Height, TimeProvider? time = null)
        {
            var actions = new RecordingConflictActions();
            actions.SetText("notes.txt", Notes);
            return OpenAsync(actions, Status(Merge(), Entry("notes.txt")), "notes.txt", height: height, time: time);
        }

        public static async Task<ResolverHost> OpenAsync(RecordingConflictActions actions, WorkingTreeStatus status, string path, string? mergeTool = null, double height = Height, TimeProvider? time = null)
        {
            var resolver = Resolver(actions, new ManualDelays(), status, time: time);
            resolver.MergeTool = mergeTool;
            await resolver.OpenAsync(path);
            var shell = new MainWindowViewModel(new MainWindowViewModelTests.FakeSettings(), new MainWindowViewModelTests.FakeThemes(), _ => Task.FromResult(new GitDetection(GitAvailability.Available, "git", new GitVersion(2, 36, 0), null)));
            var view = new ConflictResolverView { DataContext = resolver };
            var host = DiffHost.Show(view, Width, height, ThemeVariant.Dark);
            host.Window.DataContext = shell;
            view.DataContext = resolver;
            host.Capture();
            return new ResolverHost(host, view, resolver, actions, shell);
        }

        public Control Find(string id) =>
            View.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == id || control.Name == id)
            ?? throw new InvalidOperationException($"No control {id} in the resolver.");

        public Rect BoundsOf(Visual visual) => new(Host.ToWindow(visual, default), visual.Bounds.Size);

        public Point ToWindow(Visual visual, Point point) => Host.ToWindow(visual, point);

        public Screenshot Capture() => Host.Capture();

        public void RightClick(Point position)
        {
            Host.Window.MouseMove(position);
            Host.Window.MouseDown(position, MouseButton.Right);
            Host.Window.MouseUp(position, MouseButton.Right);
            ResolverFixtures.Settle();
        }

        public void Press(KeyGesture gesture)
        {
            var modifiers = RawInputModifiers.None;
            if (gesture.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                modifiers |= RawInputModifiers.Control;
            }

            if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                modifiers |= RawInputModifiers.Shift;
            }

            if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                modifiers |= RawInputModifiers.Alt;
            }

            if (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta))
            {
                modifiers |= RawInputModifiers.Meta;
            }

            Host.PressKey(gesture.Key, modifiers);
        }

        public void Dispose() => Host.Dispose();
    }
}
