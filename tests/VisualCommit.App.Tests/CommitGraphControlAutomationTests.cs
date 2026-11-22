using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Graph;
using VisualCommit.Core;
using VisualCommit.Core.Hosting;
using VisualCommit.Hosting;
using Xunit;
using static VisualCommit.App.Tests.CommitGraphControlTests;

namespace VisualCommit.App.Tests;

/// <summary>
/// The commit graph in UI Automation (D435, amending D49): a list whose one child is the row a
/// screen reader is given, the selected commit (the focus row while several are selected) or the
/// working-changes row, with its name, hint, bounds and selection, and the property changes that
/// make Windows speak it, raised only when the row becomes another or the graph takes the focus.
/// Each test makes the graph's peer itself, as a client's first read does; nothing else does.
/// </summary>
public sealed class CommitGraphControlAutomationTests
{
    /// <summary>The graph area at 1100×700, as <see cref="CommitGraphControlTests"/> shows it.</summary>
    private const double AreaWidth = 440;

    private const double RowsHeight = 558;

    private static readonly DateDisplay Utc = new(TimeZoneInfo.Utc);

    [AvaloniaFact]
    public void Without_an_automation_client_no_peer_is_made_however_the_selection_moves()
    {
        var control = new CommitGraphControl { Data = Linear(100_000), Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        control.Focus();

        for (var move = 0; move < 100; move++)
        {
            host.PressKey(Key.Down);
        }

        Assert.Equal(99, control.SelectedIndex);
        Assert.Null(ControlAutomationPeer.FromElement(control));
    }

    [AvaloniaFact]
    public void The_graph_is_a_list_that_may_select_several_rows_and_needs_none()
    {
        var control = new CommitGraphControl { Data = Linear(5), Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);

        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var selection = graph.GetProvider<ISelectionProvider>();

        Assert.Equal(("GraphRows", "Commit graph", AutomationControlType.List), (graph.GetAutomationId(), graph.GetName(), graph.GetAutomationControlType()));
        Assert.True(graph.IsControlElement());
        Assert.True(graph.IsContentElement());
        Assert.NotNull(selection);
        Assert.True(selection.CanSelectMultiple);
        Assert.False(selection.IsSelectionRequired);

        // Nothing selected: no row is exposed.
        Assert.Empty(graph.GetChildren());
        Assert.Empty(selection.GetSelection());
    }

    [AvaloniaFact]
    public void The_selected_commit_is_the_one_child_a_polite_list_item_with_the_rows_words()
    {
        var control = new CommitGraphControl { Data = Linear(5, [Branch("main", head: true), Remote("origin/main"), Tag("release-2026")]), Dates = Utc, SelectedIndex = 0 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);

        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        var item = row.GetProvider<ISelectionItemProvider>();

        Assert.Equal(("GraphSelectedRow", "CommitGraphRow", AutomationControlType.ListItem), (row.GetAutomationId(), row.GetClassName(), row.GetAutomationControlType()));
        Assert.Equal("Commit 5, Test Author, 2026-01-01 12:04, current branch main, remote branch origin/main, tag release-2026", row.GetName());
        Assert.Equal("Commit 0000000", row.GetHelpText());
        Assert.Equal(AutomationLiveSetting.Polite, row.GetLiveSetting());
        Assert.True(row.IsControlElement());
        Assert.True(row.IsContentElement());
        Assert.Null(row.GetLabeledBy());
        Assert.Null(row.GetAcceleratorKey());
        Assert.Null(row.GetAccessKey());
        Assert.Null(row.GetItemStatus());
        Assert.Empty(row.GetChildren());
        Assert.Same(graph, row.GetParent());
        Assert.NotNull(item);
        Assert.True(item.IsSelected);
        Assert.Same(graph, item.SelectionContainer);
        Assert.Same(row, Assert.Single(graph.GetProvider<ISelectionProvider>()!.GetSelection()));
    }

    [AvaloniaFact]
    public void The_rows_help_text_is_its_hint_never_the_graphs_tooltip()
    {
        var control = new CommitGraphControl { Data = Linear(5, [Branch("main", head: true), Remote("origin/main")]), Dates = Utc, SelectedIndex = 1 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        var row = Assert.Single(ControlAutomationPeer.CreatePeerForElement(control).GetChildren());

        // Over row 0's labels the graph's tooltip names them.
        host.MoveMouse(control, control.LabelsInRow(0)[0].Bounds.Center);
        Assert.Equal("main\norigin/main", ToolTip.GetTip(control));

        Assert.Equal("Commit 0000000", row.GetHelpText());
    }

    [AvaloniaFact]
    public void The_working_changes_row_while_selected_is_the_child_with_its_text_read_and_no_hint()
    {
        var control = new CommitGraphControl
        {
            Data = Linear(3, [Branch("main", head: true)]),
            Dates = Utc,
            ShowsWorkingRow = true,
            WorkingRowText = "Working changes · 1 conflict",
            IsWorkingRowSelected = true,
        };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);

        var row = Assert.Single(ControlAutomationPeer.CreatePeerForElement(control).GetChildren());

        Assert.Equal("GraphSelectedRow", row.GetAutomationId());
        Assert.Equal("Working changes, 1 conflict", row.GetName());
        Assert.Equal(string.Empty, row.GetHelpText());
    }

    [AvaloniaFact]
    public void One_row_peer_serves_end_home_and_ten_page_downs_with_one_name_change_each()
    {
        var control = new CommitGraphControl { Data = Linear(100_000), Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        control.Focus();
        host.PressKey(Key.Down);

        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        var events = new Recorder(graph, row);

        host.PressKey(Key.End);
        Assert.Equal(new (string?, string?)[] { (null, "Commit 1, Test Author, 2026-01-01 12:00") }, events.Names);
        host.PressKey(Key.Home);
        Assert.Equal(2, events.Names.Count);

        // 99,999 minutes after the first commit's 12:00: 69 days, 10 hours and 39 minutes.
        Assert.Equal(("Commit 1, Test Author, 2026-01-01 12:00", "Commit 100000, Test Author, 2026-03-11 22:39"), events.Names[^1]);
        for (var page = 0; page < 10; page++)
        {
            host.PressKey(Key.PageDown);
            Assert.Equal(3 + page, events.Names.Count);
            Assert.Same(row, Assert.Single(graph.GetChildren()));
        }

        // 21 rows a page: the tenth Page Down lands on row 210.
        Assert.Equal(210, control.SelectedIndex);
        Assert.Equal(GraphRowSpeechOf(control, 210), events.Names[^1].New);
        Assert.Equal(0, events.ChildrenChanged);
        Assert.Equal(12, events.SelectionChanges);
        Assert.Same(row, Assert.Single(graph.GetChildren()));
    }

    [AvaloniaFact]
    public void None_to_a_row_renews_the_children_and_raises_the_selection_and_the_name_with_no_old_value()
    {
        // The graph keeps one row peer for its life: followed while a row is exposed, it is the
        // same peer that comes back after a time with none.
        var control = new CommitGraphControl { Data = Linear(10), Dates = Utc, SelectedIndex = 5 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());

        // A name raised before the time with none: the row that comes back still has no old value.
        var before = new Recorder(graph, row);
        control.SelectedIndex = 4;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new (string?, string?)[] { (null, "Commit 6, Test Author, 2026-01-01 12:05") }, before.Names);
        control.SelectedIndex = -1;
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(graph.GetChildren());
        var events = new Recorder(graph, row);

        // A view model selects the commit, as a ref clicked in the left panel does.
        control.SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(row, Assert.Single(graph.GetChildren()));
        Assert.Equal(1, events.ChildrenChanged);
        Assert.Equal(1, events.SelectionChanges);
        Assert.Equal(new (string?, string?)[] { (null, "Commit 8, Test Author, 2026-01-01 12:07") }, events.Names);
    }

    [AvaloniaFact]
    public void A_row_to_none_renews_the_children_and_raises_the_selection_alone()
    {
        var control = new CommitGraphControl { Data = Linear(10), Dates = Utc, SelectedIndex = 2 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        var events = new Recorder(graph, row);

        control.SelectedIndex = -1;
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(graph.GetChildren());
        Assert.Empty(graph.GetProvider<ISelectionProvider>()!.GetSelection());
        Assert.Equal(1, events.ChildrenChanged);
        Assert.Equal(1, events.SelectionChanges);
        Assert.Empty(events.Names);
    }

    [AvaloniaFact]
    public void Another_row_raises_the_selection_and_the_name_the_old_value_being_the_last_name_raised()
    {
        var control = new CommitGraphControl { Data = Linear(10), Dates = Utc, SelectedIndex = 2 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        control.Focus();
        Dispatcher.UIThread.RunJobs();
        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        var events = new Recorder(graph, row);

        host.PressKey(Key.Down);
        host.PressKey(Key.Down);

        // The first change after the peer was made has no old value: no name had been raised.
        Assert.Equal(
            new (string?, string?)[] { (null, "Commit 7, Test Author, 2026-01-01 12:06"), ("Commit 7, Test Author, 2026-01-01 12:06", "Commit 6, Test Author, 2026-01-01 12:05") },
            events.Names);
        Assert.Equal(2, events.SelectionChanges);
        Assert.Equal(0, events.ChildrenChanged);
    }

    [AvaloniaFact]
    public void The_same_row_with_new_words_raises_nothing_and_with_a_new_hint_raises_its_help_text()
    {
        var control = new CommitGraphControl { Data = Linear(5, [Branch("main", head: true)]), Dates = Utc, SelectedIndex = 0 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        var events = new Recorder(graph, row);

        // Another time zone, and a pull request on the branch: the name is read anew, nothing is raised.
        control.Dates = new DateDisplay(TimeZoneInfo.CreateCustomTimeZone("Plus two", TimeSpan.FromHours(2), "Plus two", "Plus two"));
        control.PullRequests = ByRef(("refs/heads/main", Pull(1, "Add the login form", HostingProvider.GitHub)));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Commit 5, Test Author, 2026-01-01 14:04, current branch main (pull request #1: Add the login form)", row.GetName());
        Assert.Empty(events.All);

        // A search that marks the row: its hint gains the search, one help text change.
        control.Search = new GraphSearchMarks([control.Data!.CommitAt(0).Sha], isActive: true);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            new (AutomationProperty, object?, object?)[] { (AutomationElementIdentifiers.HelpTextProperty, "Commit 0000000", "Commit 0000000, matches the search") },
            events.All);
        Assert.Equal("Commit 0000000, matches the search", row.GetHelpText());
    }

    [AvaloniaFact]
    public void The_graph_taking_the_focus_raises_the_rows_name_once_and_the_row_follows_the_graphs_focus()
    {
        var box = new TextBox();
        var control = new CommitGraphControl { Data = Linear(5), Dates = Utc, SelectedIndex = 3 };
        using var host = Host.Show(new DockPanel { Children = { Top(box), control } }, AreaWidth, RowsHeight, ThemeVariant.Dark);
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        var events = new Recorder(graph, row);
        Assert.False(row.HasKeyboardFocus());

        control.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.True(row.HasKeyboardFocus());
        Assert.Equal(new (string?, string?)[] { (null, "Commit 2, Test Author, 2026-01-01 12:01") }, events.Names);

        box.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.False(row.HasKeyboardFocus());
        Assert.Single(events.Names);

        // Each arrival speaks the row anew: no old value, though its name was raised before.
        control.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, events.Names.Count);
        Assert.Equal((null, "Commit 2, Test Author, 2026-01-01 12:01"), events.Names[1]);
        Assert.Equal(0, events.SelectionChanges);
    }

    [AvaloniaFact]
    public void A_click_that_focuses_the_graph_and_moves_the_row_raises_one_name_change()
    {
        var box = new TextBox();
        var control = new CommitGraphControl { Data = Linear(8), Dates = Utc, SelectedIndex = 1 };
        using var host = Host.Show(new DockPanel { Children = { Top(box), control } }, AreaWidth, RowsHeight, ThemeVariant.Dark);
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var events = new Recorder(graph, Assert.Single(graph.GetChildren()));

        host.Click(control, control.RowBounds(4).Center);

        Assert.True(control.IsFocused);
        Assert.Equal(new (string?, string?)[] { (null, "Commit 4, Test Author, 2026-01-01 12:03") }, events.Names);
    }

    [AvaloniaFact]
    public void A_move_while_the_focus_is_elsewhere_is_spoken_too()
    {
        var box = new TextBox();
        var control = new CommitGraphControl { Data = Linear(5), Dates = Utc, SelectedIndex = 0 };
        using var host = Host.Show(new DockPanel { Children = { Top(box), control } }, AreaWidth, RowsHeight, ThemeVariant.Dark);
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        var events = new Recorder(graph, row);

        // A view model moves the selection, as a ref clicked in the left panel or a search's Enter does.
        control.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new (string?, string?)[] { (null, "Commit 4, Test Author, 2026-01-01 12:03") }, events.Names);
        Assert.Equal(1, events.SelectionChanges);
        Assert.False(row.HasKeyboardFocus());
        Assert.True(box.IsFocused);
    }

    [AvaloniaFact]
    public void With_no_row_exposed_the_graph_taking_the_focus_raises_nothing()
    {
        var box = new TextBox();
        var control = new CommitGraphControl { Data = Linear(5), Dates = Utc, SelectedIndex = 1 };
        using var host = Host.Show(new DockPanel { Children = { Top(box), control } }, AreaWidth, RowsHeight, ThemeVariant.Dark);
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        control.SelectedIndex = -1;
        Dispatcher.UIThread.RunJobs();
        var events = new Recorder(graph, row);

        control.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.True(control.IsFocused);
        Assert.Empty(graph.GetChildren());
        Assert.Equal((0, 0), (events.ChildrenChanged, events.SelectionChanges));
        Assert.Empty(events.All);
    }

    [AvaloniaFact]
    public void Shift_with_arrows_exposes_the_focus_row_and_the_hint_counts_the_selected_commits()
    {
        var control = new CommitGraphControl { Data = Linear(10), Dates = Utc, SelectedIndex = 4 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        control.Focus();
        Dispatcher.UIThread.RunJobs();
        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        var events = new Recorder(graph, row);

        PressShift(host, Key.Down);
        PressShift(host, Key.Down);

        Assert.Equal(3, control.Selection.Count);
        Assert.Equal(["Commit 5, Test Author, 2026-01-01 12:04", "Commit 4, Test Author, 2026-01-01 12:03"], events.Names.Select(change => change.New));
        Assert.Equal("Commit 4, Test Author, 2026-01-01 12:03", row.GetName());
        Assert.Equal("Commit 0000000, 3 commits selected", row.GetHelpText());
        Assert.Same(row, Assert.Single(graph.GetProvider<ISelectionProvider>()!.GetSelection()));
    }

    [AvaloniaFact]
    public void Shift_end_over_100000_rows_keeps_one_selected_peer_and_the_hint_has_the_count()
    {
        var control = new CommitGraphControl { Data = Linear(100_000), Dates = Utc };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        control.Focus();
        host.PressKey(Key.Down);
        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        var events = new Recorder(graph, row);

        PressShift(host, Key.End);

        Assert.Equal(100_000, control.Selection.Count);
        Assert.Same(row, Assert.Single(graph.GetProvider<ISelectionProvider>()!.GetSelection()));
        Assert.Same(row, Assert.Single(graph.GetChildren()));
        Assert.Equal("Commit 1, Test Author, 2026-01-01 12:00", row.GetName());
        Assert.Equal("Commit 0000000, 100000 commits selected", row.GetHelpText());
        Assert.Single(events.Names);
    }

    [AvaloniaFact]
    public void The_rows_bounds_are_its_drawn_row_in_the_windows_coordinates_clipped_to_the_graph()
    {
        var control = new CommitGraphControl { Data = Linear(60), Dates = Utc, SelectedIndex = 3 };
        var frame = new Border { Padding = new Thickness(30, 40, 0, 0), Child = control };
        using var host = Host.Show(frame, AreaWidth + 30, RowsHeight + 40, ThemeVariant.Dark);
        host.Capture();
        var row = Assert.Single(ControlAutomationPeer.CreatePeerForElement(control).GetChildren());

        Assert.Equal(new Rect(host.ToWindow(control, control.RowBounds(3).TopLeft), control.RowBounds(3).Size), row.GetBoundingRectangle());
        Assert.Equal(26, row.GetBoundingRectangle().Height);
        Assert.Equal(new Point(30, 40 + (3 * 26)), row.GetBoundingRectangle().TopLeft);
        Assert.False(row.IsOffscreen());

        // Half the row above the view: only its visible half.
        control.ScrollOffset = (3 * 26) + 13;
        host.Capture();
        Assert.Equal(new Rect(30, 40, AreaWidth, 13), row.GetBoundingRectangle());
        Assert.False(row.IsOffscreen());

        // Scrolled away: off screen.
        control.ScrollOffset = 20 * 26;
        host.Capture();
        Assert.True(row.IsOffscreen());

        // Brought into view by its peer.
        row.BringIntoView();
        host.Capture();
        Assert.InRange(control.RowBounds(3).Top, 0, control.ViewportHeight - 26);
        Assert.False(row.IsOffscreen());
    }

    [AvaloniaFact]
    public void The_working_rows_bounds_are_its_drawn_row()
    {
        var control = new CommitGraphControl { Data = Linear(3, [Branch("main", head: true)]), Dates = Utc, ShowsWorkingRow = true, IsWorkingRowSelected = true };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        host.Capture();
        var row = Assert.Single(ControlAutomationPeer.CreatePeerForElement(control).GetChildren());

        Assert.Equal(new Rect(host.ToWindow(control, control.WorkingRowBounds.TopLeft), control.WorkingRowBounds.Size), row.GetBoundingRectangle());
    }

    [AvaloniaFact]
    public void The_request_words_in_the_rows_name_equal_the_labels_tooltip_lines()
    {
        var control = new CommitGraphControl
        {
            Data = Linear(3, [Branch("feature/login"), Remote("origin/feature/login"), Branch("docs"), Branch("main", head: true)]),
            Dates = Utc,
            SelectedIndex = 0,
            PullRequests = ByRef(
                ("refs/heads/feature/login", Pull(12, "Add the login form", HostingProvider.GitHub)),
                ("refs/remotes/origin/feature/login", Pull(12, "Add the login form", HostingProvider.GitHub)),
                ("refs/heads/docs", Pull(21, "Update the readme", HostingProvider.GitLab, draft: true))),
        };
        using var host = Host.Show(control, 1400, RowsHeight, ThemeVariant.Dark);
        var row = Assert.Single(ControlAutomationPeer.CreatePeerForElement(control).GetChildren());

        host.MoveMouse(control, control.LabelsInRow(0)[0].Bounds.Center);
        var lines = Assert.IsType<string>(ToolTip.GetTip(control)).Split('\n');

        // The labels in drawing order: the current branch, the local ones, then the remote one.
        Assert.Equal(
            ["main", "docs (draft merge request !21: Update the readme)", "feature/login (pull request #12: Add the login form)", "origin/feature/login (pull request #12: Add the login form)"],
            lines);
        Assert.Equal(
            "Commit 3, Test Author, 2026-01-01 12:02, current branch " + lines[0] + ", branch " + lines[1] + ", branch " + lines[2] + ", remote branch " + lines[3],
            row.GetName());
    }

    [AvaloniaFact]
    public void The_rows_select_selects_it_alone_and_adding_or_removing_does_nothing()
    {
        var control = new CommitGraphControl { Data = Linear(10), Dates = Utc, SelectedIndex = 4 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        control.Focus();
        PressShift(host, Key.Down);
        PressShift(host, Key.Down);
        var row = Assert.Single(ControlAutomationPeer.CreatePeerForElement(control).GetChildren());
        var item = row.GetProvider<ISelectionItemProvider>()!;

        item.AddToSelection();
        item.RemoveFromSelection();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, control.Selection.Count);

        item.Select();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(6, control.SelectedIndex);
        Assert.Equal(1, control.Selection.Count);
    }

    [AvaloniaFact]
    public void The_rows_set_focus_focuses_the_graph_and_its_context_menu_is_the_keyboards()
    {
        var box = new TextBox();
        var control = new CommitGraphControl { Data = Linear(8), Dates = Utc, SelectedIndex = 2 };
        using var host = Host.Show(new DockPanel { Children = { Top(box), control } }, AreaWidth, RowsHeight, ThemeVariant.Dark);
        box.Focus();
        host.Capture();
        var asked = new List<GraphContextRequestedEventArgs>();
        control.ContextRequestedAt += (_, e) =>
        {
            asked.Add(e);
            e.Handled = true;
        };
        var row = Assert.Single(ControlAutomationPeer.CreatePeerForElement(control).GetChildren());

        row.SetFocus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(control.IsFocused);
        Assert.True(row.HasKeyboardFocus());

        Assert.True(row.ShowContextMenu());
        var request = Assert.Single(asked);
        Assert.Equal((GraphHitKind.Commit, 2), (request.Hit.Kind, request.Hit.Row));
        Assert.Equal(control.RowBounds(2).Bottom, request.Position.Y);
    }

    [AvaloniaFact]
    public void A_frame_before_the_peer_and_one_after_ten_moves_and_back_are_the_same_bytes()
    {
        var control = new CommitGraphControl { Data = Linear(50, [Branch("main", head: true), Remote("origin/main"), Tag("v1")]), Dates = Utc, SelectedIndex = 3 };
        using var host = Host.Show(control, AreaWidth, RowsHeight, ThemeVariant.Dark);
        control.Focus();

        // The keys first, so that both frames show the graph as the keys leave it.
        host.PressKey(Key.Down);
        host.PressKey(Key.Up);
        var before = FrameBytes(host);

        var graph = ControlAutomationPeer.CreatePeerForElement(control);
        var row = Assert.Single(graph.GetChildren());
        _ = (row.GetName(), row.GetHelpText(), row.GetBoundingRectangle());
        for (var move = 0; move < 10; move++)
        {
            host.PressKey(Key.Down);
        }

        for (var move = 0; move < 10; move++)
        {
            host.PressKey(Key.Up);
        }

        Assert.Equal(3, control.SelectedIndex);
        Assert.Equal(before, FrameBytes(host));
    }

    /// <summary>What the row's peer should say for commit row <paramref name="index"/>, from the words' own class.</summary>
    private static string GraphRowSpeechOf(CommitGraphControl control, int index) =>
        GraphRowSpeech.Name(control.Data!.CommitAt(index), control.Data.LabelsAt(index), Utc.Format);

    private static TextBox Top(TextBox box)
    {
        DockPanel.SetDock(box, Dock.Top);
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        return box;
    }

    private static void PressShift(Host host, Key key)
    {
        host.Window.KeyPress(key, RawInputModifiers.Shift, PhysicalKey.None, null);
        host.Window.KeyRelease(key, RawInputModifiers.Shift, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The window's rendered frame, every byte of it.</summary>
    private static byte[] FrameBytes(Host host)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame = host.Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The window has not rendered a frame.");
        using var pixels = frame.Lock();
        var bytes = new byte[pixels.RowBytes * pixels.Size.Height];
        Marshal.Copy(pixels.Address, bytes, 0, bytes.Length);
        return bytes;
    }

    private static HostedRepository Repository(HostingProvider provider) => new(provider, "example.com", "demo", "app");

    private static PullRequest Pull(int number, string title, HostingProvider provider, bool draft = false) =>
        new(number, title, "demo", draft, new Uri("https://example.com/demo/app/" + number), "feature", new string('0', 40), Repository(provider), "main", Repository(provider), new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private static IReadOnlyDictionary<string, PullRequest> ByRef(params (string RefName, PullRequest Pull)[] entries) =>
        entries.ToDictionary(entry => entry.RefName, entry => entry.Pull, StringComparer.Ordinal);

    /// <summary>
    /// Stands for an automation client's handlers: the graph peer's children changes and its
    /// Selection changes, and every property change of the row peers it follows, in order.
    /// </summary>
    private sealed class Recorder
    {
        private readonly List<(AutomationPeer Peer, AutomationProperty Property, object? Old, object? New)> _changes = [];

        public Recorder(AutomationPeer graph, AutomationPeer? row = null)
        {
            graph.ChildrenChanged += (_, _) => ChildrenChanged++;
            graph.PropertyChanged += (_, e) =>
            {
                if (e.Property == SelectionPatternIdentifiers.SelectionProperty)
                {
                    SelectionChanges++;
                }
            };
            if (row is not null)
            {
                Follow(row);
            }
        }

        public int ChildrenChanged { get; private set; }

        public int SelectionChanges { get; private set; }

        /// <summary>Every property change of the followed rows: which, the old and the new value.</summary>
        public IReadOnlyList<(AutomationProperty Property, object? Old, object? New)> All =>
            [.. _changes.Select(change => (change.Property, change.Old, change.New))];

        /// <summary>The followed rows' Name changes: old and new.</summary>
        public IReadOnlyList<(string? Old, string? New)> Names =>
            [.. _changes.Where(change => change.Property == AutomationElementIdentifiers.NameProperty).Select(change => (change.Old as string, change.New as string))];

        public void Follow(AutomationPeer row) => row.PropertyChanged += (_, e) => _changes.Add((row, e.Property, e.OldValue, e.NewValue));

        public IReadOnlyList<(string? Old, string? New)> NamesOf(AutomationPeer row) =>
            [.. _changes.Where(change => change.Peer == row && change.Property == AutomationElementIdentifiers.NameProperty).Select(change => (change.Old as string, change.New as string))];
    }
}
