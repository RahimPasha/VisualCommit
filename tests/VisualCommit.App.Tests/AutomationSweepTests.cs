using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VisualCommit.App.Views.Dialogs;
using VisualCommit.Testing.Headless;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The sweep of names (docs/phase-8a-design.md 1.12, 9.3.8) on windows built by hand: each of its
/// four rules with a name that breaks it and one that does not, the one exemption, what it walks
/// and what it leaves, and the failure's message, one problem per line with its id and class.
/// </summary>
public sealed class AutomationSweepTests
{
    private const string State = "hand-built";

    [AvaloniaFact]
    public void A_button_with_no_name_or_a_blank_one_is_empty_and_a_named_one_passes()
    {
        var bare = Button("Bare", name: null);
        var blank = Button("Blank", name: "   ");
        var named = Button("Named", name: "Save");
        using var host = Show(bare, blank, named);

        var problems = AutomationSweep.FindProblems(host.Window, State);

        Assert.Equal([("Bare", AutomationSweep.Empty), ("Blank", AutomationSweep.Empty)], Ids(problems));
        Assert.All(problems, problem => Assert.Equal((State, AutomationControlType.Button, "Button"), (problem.State, problem.ControlType, problem.ClassName)));
    }

    [AvaloniaFact]
    public void A_name_that_is_a_type_name_breaks_the_type_name_rule_and_a_file_name_passes()
    {
        // A panel as a button's content gives its type's full name (Content.ToString()).
        var panel = new Button { Content = new StackPanel { Children = { new Border() } } };
        AutomationProperties.SetAutomationId(panel, "Panel");
        using var host = Show(
            panel,
            Button("Ours", name: "VisualCommit.App.ViewModels.Something"),
            Button("System", name: "System.Object"),
            Button("Generic", name: "Microsoft.Extensions.Thing`1"),
            Button("File", name: "Reader.cs"),
            Button("Word", name: "Avalonia"));

        var problems = AutomationSweep.FindProblems(host.Window, State);

        Assert.Equal(
            [("Panel", AutomationSweep.TypeName), ("Ours", AutomationSweep.TypeName), ("System", AutomationSweep.TypeName), ("Generic", AutomationSweep.TypeName)],
            Ids(problems));
        Assert.Equal("Avalonia.Controls.StackPanel", problems[0].Name);
    }

    [AvaloniaFact]
    public void A_name_ending_in_its_keys_breaks_the_key_text_rule_and_a_count_in_parentheses_passes()
    {
        var withOwnKey = Button("OwnKey", name: "Fetch (Ctrl+Shift+F)");
        AutomationProperties.SetAcceleratorKey(withOwnKey, "Ctrl+Shift+F");
        var settings = Button("Settings", name: "Settings (Ctrl+,)");
        AutomationProperties.SetAcceleratorKey(settings, "Ctrl+,");
        using var host = Show(
            Button("Close", name: "Close (Esc)"),
            Button("Previous", name: "Previous match (Shift+Enter, Shift+F3)"),
            withOwnKey,
            settings,
            Button("Guide", name: "Open the user guide (F1)"),
            Button("Tabs", name: "Go to tab (Ctrl+1 to Ctrl+8)"),
            Button("Mac", name: "Delete (Cmd+Forward Delete)"),
            Button("Count", name: "Pull requests (3)"),
            Button("Word", name: "Show remote (origin)"),
            Button("Plain", name: "Previous match"),
            Button("Bare", name: "Escape (Q)"));

        var problems = AutomationSweep.FindProblems(host.Window, State);

        Assert.Equal(
            [("Close", AutomationSweep.KeyText), ("Previous", AutomationSweep.KeyText), ("OwnKey", AutomationSweep.KeyText), ("Settings", AutomationSweep.KeyText),
                ("Guide", AutomationSweep.KeyText), ("Tabs", AutomationSweep.KeyText), ("Mac", AutomationSweep.KeyText)],
            Ids(problems));
    }

    [AvaloniaFact]
    public void Two_controls_with_one_id_and_one_name_in_one_list_are_duplicates_and_other_names_or_lists_pass()
    {
        var first = new ItemsControl { Items = { Button("RowStageButton", name: "Stage"), Button("RowStageButton", name: "Stage"), Button("RowDiscardButton", name: "Discard a"), Button("RowDiscardButton", name: "Discard b") } };
        AutomationProperties.SetAutomationId(first, "UnstagedFileList");
        var second = new ItemsControl { Items = { Button("RowDiscardButton", name: "Discard a") } };
        AutomationProperties.SetAutomationId(second, "StagedFileList");
        using var host = Show(first, second, Button("CloseDiffButton", name: "Close"), Button("CloseDiffButton", name: "Close"));

        var problems = AutomationSweep.FindProblems(host.Window, State);

        // Outside any list two same buttons are not compared; across two lists neither.
        Assert.Equal([("RowStageButton", AutomationSweep.Duplicate), ("RowStageButton", AutomationSweep.Duplicate)], Ids(problems));
        Assert.All(problems, problem => Assert.Equal("Stage", problem.Name));
    }

    [AvaloniaFact]
    public void The_branch_lists_rows_are_exempt_from_the_duplicate_rule_and_no_other_list_is()
    {
        var refs = new ItemsControl { Items = { Button("RefItem", name: "main"), Button("RefItem", name: "main"), Button("RefHiddenToggle", name: "Show"), Button("RefHiddenToggle", name: "Show") } };
        AutomationProperties.SetAutomationId(refs, "RefList");
        var other = new ItemsControl { Items = { Button("RefItem", name: "main"), Button("RefItem", name: "main") } };
        AutomationProperties.SetAutomationId(other, "OtherList");
        using var host = Show(refs, other);

        var problems = AutomationSweep.FindProblems(host.Window, State);

        Assert.Equal(
            [("RefHiddenToggle", AutomationSweep.Duplicate), ("RefHiddenToggle", AutomationSweep.Duplicate), ("RefItem", AutomationSweep.Duplicate), ("RefItem", AutomationSweep.Duplicate)],
            Ids(problems));
    }

    [AvaloniaFact]
    public void A_dialog_is_its_own_container_so_two_stacked_dialogs_cancels_pass_and_one_name_twice_in_a_dialog_does_not()
    {
        // The window's dialog layer: the settings screen with a sign-in dialog over it, each with a Cancel (D456 (2)).
        var settings = Dialog(Button("CancelButton", name: "Cancel"), Button("ConfirmButton", name: "Save"));
        var signIn = Dialog(Button("CancelButton", name: "Cancel"), Button("CopyButton", name: "Copy"), Button("CopyButton", name: "Copy"));
        var layer = new ItemsControl { ItemsPanel = new FuncTemplate<Panel?>(() => new Panel()), Items = { settings, signIn } };
        AutomationProperties.SetAutomationId(layer, "DialogLayer");
        using var host = Show(layer);

        var problems = AutomationSweep.FindProblems(host.Window, State);

        Assert.Equal([("CopyButton", AutomationSweep.Duplicate), ("CopyButton", AutomationSweep.Duplicate)], Ids(problems));
    }

    [AvaloniaFact]
    public void A_scroll_bar_and_what_lies_inside_it_are_not_checked_and_an_unnamed_button_beside_it_is()
    {
        // A scrolling control's template part, without a name (D456 (1)), its line buttons and thumb
        // shown. Fluent names those parts ("Line up", "Position"); the thumb's name is taken away
        // here, so that a part inside the bar breaks a rule too.
        var bar = new ScrollBar { Orientation = Orientation.Vertical, Height = 200, Maximum = 100, ViewportSize = 20, Visibility = ScrollBarVisibility.Visible, AllowAutoHide = false };
        AutomationProperties.SetAutomationId(bar, "PART_VerticalScrollBar");
        using var host = Show(bar, Button("Bare", name: null));
        var thumb = bar.GetVisualDescendants().OfType<Thumb>().Single();
        AutomationProperties.SetAutomationId(thumb, "Thumb");
        AutomationProperties.SetName(thumb, string.Empty);
        var peers = new[] { ControlAutomationPeer.CreatePeerForElement(bar), ControlAutomationPeer.CreatePeerForElement(thumb) };

        var problems = AutomationSweep.FindProblems(host.Window, State);

        Assert.Equal(
            [(AutomationControlType.ScrollBar, true, AutomationSweep.Empty), (AutomationControlType.Thumb, true, AutomationSweep.Empty)],
            peers.Select(peer => (peer.GetAutomationControlType(), peer.IsControlElement(), AutomationSweep.RuleBroken(peer.GetName(), peer.GetAcceleratorKey()))));
        Assert.Equal([("Bare", AutomationSweep.Empty)], Ids(problems));
    }

    [AvaloniaFact]
    public void An_invisible_button_is_not_walked_and_one_drawn_at_opacity_zero_is()
    {
        var hidden = Button("Hidden", name: null);
        hidden.IsVisible = false;
        var inHiddenPanel = Button("InHiddenPanel", name: null);
        var panel = new StackPanel { IsVisible = false, Children = { inHiddenPanel } };
        var transparent = Button("Transparent", name: null);
        transparent.Opacity = 0;
        using var host = Show(hidden, panel, transparent);

        Assert.Equal([("Transparent", AutomationSweep.Empty)], Ids(AutomationSweep.FindProblems(host.Window, State)));
    }

    [AvaloniaFact]
    public void Only_interactive_controls_are_checked_by_type_or_by_pattern()
    {
        var text = new TextBlock();
        AutomationProperties.SetAutomationId(text, "Text");
        var border = new Border();
        AutomationProperties.SetAutomationId(border, "Border");
        var box = new TextBox();
        AutomationProperties.SetAutomationId(box, "Box");
        var check = new CheckBox();
        AutomationProperties.SetAutomationId(check, "Check");
        var toggle = new ToggleButton();
        AutomationProperties.SetAutomationId(toggle, "Toggle");
        var progress = new ProgressBar();
        AutomationProperties.SetAutomationId(progress, "Progress");
        using var host = Show(text, border, box, check, toggle, progress);

        var problems = AutomationSweep.FindProblems(host.Window, State);

        // An edit box by its type; a check box, a toggle button and a progress bar by their patterns.
        Assert.Equal(
            [("Box", AutomationSweep.Empty), ("Check", AutomationSweep.Empty), ("Toggle", AutomationSweep.Empty), ("Progress", AutomationSweep.Empty)],
            Ids(problems));
    }

    [AvaloniaFact]
    public void A_list_item_with_no_name_is_checked()
    {
        var list = new ListBox { Items = { new ListBoxItem(), new ListBoxItem { Content = "Second" } } };
        AutomationProperties.SetAutomationId(list, "List");
        using var host = Show(list);

        var problem = Assert.Single(AutomationSweep.FindProblems(host.Window, State));

        Assert.Equal((AutomationControlType.ListItem, AutomationSweep.Empty), (problem.ControlType, problem.Rule));
    }

    [AvaloniaFact]
    public void Assert_named_passes_a_window_of_named_controls_and_fails_with_every_problem_one_per_line()
    {
        using (var named = Show(Button("Save", name: "Save"), Button("Open", name: "Open")))
        {
            AutomationSweep.AssertNamed(named.Window, State);
        }

        using var host = Show(Button("Close", name: "Close (Esc)"), Button("Bare", name: null));
        var failure = Assert.Throws<AutomationSweepException>(() => AutomationSweep.AssertNamed(host.Window, "the ops tab"));
        var lines = failure.Message.Split('\n');

        Assert.Contains("the ops tab", lines[0], StringComparison.Ordinal);
        Assert.Contains("2", lines[0], StringComparison.Ordinal);
        Assert.Equal(3, lines.Length);
        Assert.Equal("key text: Button Close (class Button), named \"Close (Esc)\"", lines[1].Trim());
        Assert.Equal("empty: Button Bare (class Button), named \"\"", lines[2].Trim());
    }

    [AvaloniaFact]
    public void The_sweep_starts_at_the_windows_own_peer()
    {
        using var host = Show(Button("Save", name: "Save"));

        AutomationSweep.FindProblems(host.Window, State);

        Assert.NotNull(ControlAutomationPeer.FromElement(host.Window));
    }

    private static Button Button(string automationId, string? name)
    {
        var button = new Button();
        AutomationProperties.SetAutomationId(button, automationId);
        if (name is not null)
        {
            AutomationProperties.SetName(button, name);
        }

        return button;
    }

    /// <summary>A dialog's root, as the window's dialog layer holds each open dialog, around <paramref name="controls"/>.</summary>
    private static DialogView Dialog(params Control[] controls)
    {
        var panel = new StackPanel();
        panel.Children.AddRange(controls);
        return new DialogView { Content = panel };
    }

    private static DiffHost Show(params Control[] controls)
    {
        var panel = new StackPanel();
        panel.Children.AddRange(controls);
        return DiffHost.Show(panel, 600, 600, ThemeVariant.Dark);
    }

    private static List<(string? Id, string Rule)> Ids(IEnumerable<AutomationProblem> problems) =>
        [.. problems.Select(problem => (problem.AutomationId, problem.Rule))];
}
