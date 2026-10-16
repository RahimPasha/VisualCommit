using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Menus;
using VisualCommit.App.Views.Shell;
using VisualCommit.Core.Logging;
using VisualCommit.Core.Settings;
using VisualCommit.Testing;
using VisualCommit.Testing.Headless;
using Xunit;
using Item = VisualCommit.App.ViewModels.Menus.MenuItem;

namespace VisualCommit.App.Tests;

/// <summary>
/// The menu adapter (docs/phase-3-design.md 4a, D105, D106): a <see cref="MenuModel"/> drawn by
/// Avalonia's stock menu flyout on the window's overlay layer, its placement, size and look, its
/// keyboard, its light dismiss, Invoke through UI Automation, Shift+F10 and the Menu key once per
/// press, and the automation ids as control elements.
/// </summary>
public class ContextMenuAdapterTests
{
    private const double MenuInset = 8;
    private const double HeaderHeight = 24;
    private const double ItemHeight = 26;
    private const double SeparatorHeight = 9;

    // The dark theme's tokens (Theme/Tokens.axaml), as the report gives them.
    private static readonly Color PanelBackground = Color.Parse("#1A1D24");
    private static readonly Color Border = Color.Parse("#323845");
    private static readonly Color TextPrimary = Color.Parse("#E4E7EC");
    private static readonly Color TextSecondary = Color.Parse("#9BA3B0");
    private static readonly Color TextDisabled = Color.Parse("#5F6775");
    private static readonly Color Danger = Color.Parse("#E5606B");
    private static readonly Color Hover = Color.Parse("#2F3542");

    // --- The Shift+F10 rule (D106) ---------------------------------------------------------

    [Fact]
    public void The_app_raises_Shift_F10_itself_only_where_the_platform_list_lacks_it()
    {
        // Headless: the Menu key only. Win32: the Menu key and Shift+F10 (the spike of 4a).
        Assert.True(ContextMenuKeys.AppRaisesShiftF10([new KeyGesture(Key.Apps)]));
        Assert.False(ContextMenuKeys.AppRaisesShiftF10([new KeyGesture(Key.Apps), new KeyGesture(Key.F10, KeyModifiers.Shift)]));
        Assert.True(ContextMenuKeys.AppRaisesShiftF10([]));
        Assert.True(ContextMenuKeys.AppRaisesShiftF10(null));
        Assert.True(ContextMenuKeys.AppRaisesShiftF10([new KeyGesture(Key.F10), new KeyGesture(Key.F10, KeyModifiers.Control | KeyModifiers.Shift)]));
    }

    // --- Placement (4a) ----------------------------------------------------------------------

    [Theory]
    [InlineData(300, 200, 302, 202)] // room on every side: 2 right of and 2 below the pointer
    [InlineData(1090, 200, 888, 202)] // past the right edge: its right edge 2 left of the pointer
    [InlineData(300, 660, 302, 458)] // past the bottom: its bottom edge 2 above the pointer
    [InlineData(1090, 660, 888, 458)] // both
    [InlineData(0, 0, 4, 4)] // the top-left corner: moved 4 inside
    [InlineData(1099, 699, 896, 496)] // the bottom-right corner: flipped, then moved 4 inside
    public void A_menu_opened_by_the_pointer_follows_the_position_rules(double x, double y, double left, double top)
    {
        var at = MenuLayer.PlaceMenu(new Size(1100, 700), new Point(x, y), new Size(200, 200), MenuOpenedBy.Pointer);

        Assert.Equal(new Point(left, top), at);
    }

    [Fact]
    public void A_menu_flipped_past_the_top_left_is_moved_4_inside()
    {
        var at = MenuLayer.PlaceMenu(new Size(300, 300), new Point(150, 210), new Size(200, 250), MenuOpenedBy.Pointer);

        Assert.Equal(new Point(4, 4), at);
    }

    [Fact]
    public void A_menu_opened_by_the_keyboard_has_its_top_left_at_the_point_and_still_stays_inside()
    {
        Assert.Equal(new Point(454, 246), MenuLayer.PlaceMenu(new Size(1100, 700), new Point(454, 246), new Size(211, 232), MenuOpenedBy.Keyboard));
        Assert.Equal(new Point(454, 458), MenuLayer.PlaceMenu(new Size(1100, 700), new Point(454, 690), new Size(211, 232), MenuOpenedBy.Keyboard));
        Assert.Equal(new Point(885, 458), MenuLayer.PlaceMenu(new Size(1100, 700), new Point(1096, 690), new Size(211, 232), MenuOpenedBy.Keyboard));
    }

    [Fact]
    public void A_menu_taller_than_the_area_starts_4_from_the_top()
    {
        var at = MenuLayer.PlaceMenu(new Size(1100, 700), new Point(300, 500), new Size(220, 692), MenuOpenedBy.Pointer);

        Assert.Equal(new Point(302, 4), at);
    }

    // --- The view model ------------------------------------------------------------------------

    [Fact]
    public void The_layer_logs_each_menu_it_opens_with_how_it_was_opened_and_keeps_one_menu()
    {
        var log = new RecordingLog();
        var repository = "ops";
        var layer = new MenuLayerViewModel(log, () => repository);
        var first = Sample("ce5ea6a Alpha two");
        var second = Sample("v1.0");

        layer.Open(first, new MenuPosition(10, 20));
        layer.Open(second, new MenuPosition(30, 40), MenuOpenedBy.Keyboard);

        Assert.Same(second, layer.Current);
        Assert.Equal(new MenuPosition(30, 40), layer.Position);
        Assert.Equal(MenuOpenedBy.Keyboard, layer.OpenedBy);
        Assert.Equal(
            ["ops: Context menu opened: ce5ea6a Alpha two by pointer", "ops: Context menu opened: v1.0 by keyboard"],
            log.Entries.Where(entry => entry.Level == LogLevel.Debug).Select(entry => entry.Message));
        Assert.True(layer.Close());
        Assert.False(layer.IsOpen);
        Assert.False(layer.Close());
    }

    // --- Drawing ----------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_menu_opens_at_the_pointer_with_its_header_items_separators_and_size()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);

        app.Session.Shell.Menu.Open(Sample("ce5ea6a Alpha two"), new MenuPosition(300, 200));
        var menu = app.Menu();

        Assert.Equal(
            ["ce5ea6a Alpha two", "Check out this commit", "Create branch here…", "Create tag here…", "—", "Cherry-pick onto main (disabled: \"This commit is already on main\")", "Revert this commit", "Reset main to this commit…", "—", "Delete feature/alpha… (danger)"],
            menu.Lines());
        Assert.Equal(new Point(302, 202), menu.Bounds.TopLeft);
        Assert.Equal(MenuInset + HeaderHeight + (7 * ItemHeight) + (2 * SeparatorHeight), menu.Bounds.Height);
        Assert.InRange(menu.Bounds.Width, 200, 360);
        Assert.All(menu.Items, item => Assert.Equal(ItemHeight, item.Bounds.Height));
        Assert.Equal(1, app.MenuCount);
        Assert.Equal("ce5ea6a Alpha two", menu.HeaderToolTip);

        // The header: the secondary colour, not focusable, a Text element named by its full text.
        var header = (Avalonia.Controls.MenuItem)app.FindByAutomationId(MenuLayer.HeaderAutomationId);
        Assert.False(header.Focusable);
        Assert.Equal(HeaderHeight, header.Bounds.Height);
        Assert.Equal(TextSecondary, ((ISolidColorBrush)header.Foreground!).Color);
    }

    [AvaloniaFact]
    public async Task Items_are_drawn_in_the_menu_tokens_and_a_disabled_one_shows_its_reason()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var model = new MenuModel([
            new MenuHeader("Tags"),
            new Item("Push v1.0 to origin", () => { }),
            new Item("Delete v1.0…", () => { }, IsDestructive: true),
            new Item("Delete v1.0 on origin…", () => { }, false, "origin has no v1.0", IsDestructive: true),
        ]);

        // Below the welcome page's text and buttons: the window's plain background around it.
        app.Session.Shell.Menu.Open(model, new MenuPosition(300, 450));
        var menu = app.Menu();

        Assert.Equal(TextPrimary, menu.Item("Push v1.0 to origin").TextColor);
        Assert.Equal(Danger, menu.Item("Delete v1.0…").TextColor);

        // A disabled item, a destructive one too, is drawn as disabled (D114) and gives its reason.
        var disabled = menu.Item("Delete v1.0 on origin…");
        Assert.False(disabled.IsEnabled);
        Assert.True(disabled.IsDestructive);
        Assert.Equal(TextDisabled, disabled.TextColor);
        Assert.Equal("origin has no v1.0", disabled.ToolTip);

        // The first enabled item is highlighted (D105), on the hover background.
        Assert.Equal("Push v1.0 to origin", menu.Highlighted?.Label);
        var shot = app.Capture();
        var highlighted = menu.Item("Push v1.0 to origin").Bounds;
        Assert.Equal(Hover, shot.PixelAt(new Point(highlighted.Right - 4, highlighted.Center.Y)));
        Assert.Equal(PanelBackground, shot.PixelAt(new Point(menu.Item("Delete v1.0…").Bounds.Right - 4, menu.Item("Delete v1.0…").Bounds.Center.Y)));
        Assert.Equal(Border, shot.PixelAt(new Point(menu.Bounds.X + 20, menu.Bounds.Y)));

        // The shadow (VcMenuShadow, 6 down) darkens the window's background just below the menu.
        var below = shot.PixelAt(new Point(menu.Bounds.Center.X, menu.Bounds.Bottom + 4));
        var far = shot.PixelAt(new Point(menu.Bounds.Right + 60, menu.Bounds.Bottom + 4));
        Assert.True(below.R + below.G + below.B < far.R + far.G + far.B, $"No shadow below the menu: {below} against {far}.");

        // Resting the pointer on the disabled item shows its reason inside the window, after the
        // tooltip's delay, and leaves the highlight where it was.
        app.MoveMouse(app.MenuItemBounds("Delete v1.0 on origin…").Center);
        var item = (Avalonia.Controls.MenuItem)app.Window.GetVisualDescendants().OfType<ContextMenuItem>().Single(control => (string?)control.Header == "Delete v1.0 on origin…");
        await app.WaitForAsync(() => ToolTip.GetIsOpen(item), "the disabled item's tooltip", TimeSpan.FromSeconds(5));
        var tip = app.Window.GetVisualDescendants().OfType<ToolTip>().Single();
        Assert.Equal("origin has no v1.0", tip.Content);
        Assert.Same(app.Window, TopLevel.GetTopLevel(tip));
        Assert.True(ToolTip.GetShouldUseOverlayLayer(item));
        Assert.Equal("Push v1.0 to origin", app.Menu().Highlighted?.Label);
    }

    [AvaloniaFact]
    public async Task Every_tooltip_of_the_app_is_drawn_in_the_overlay_layer()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var theme = app.FindByAutomationId("ThemeSwitch");
        Assert.False(theme.IsSet(ToolTip.ShouldUseOverlayLayerProperty));

        app.MoveMouse(theme);
        await app.WaitForAsync(() => ToolTip.GetIsOpen(theme), "the theme button's tooltip", TimeSpan.FromSeconds(5));

        Assert.True(ToolTip.GetShouldUseOverlayLayer(theme));
        Assert.Same(app.Window, TopLevel.GetTopLevel(app.Window.GetVisualDescendants().OfType<ToolTip>().Single()));
    }

    [AvaloniaFact]
    public async Task A_menu_with_no_enabled_item_highlights_none_and_a_click_on_its_header_keeps_it_open()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var model = new MenuModel([new MenuHeader("Stashes"), new Item("Stash changes…", () => { }, false, "There are no changes to stash")]);

        app.Session.Shell.Menu.Open(model, new MenuPosition(300, 200));
        Assert.Null(app.Menu().Highlighted);

        app.Click(app.FindByAutomationId(MenuLayer.HeaderAutomationId));
        Assert.True(app.IsMenuOpen);
        app.Click(app.MenuItemBounds("Stash changes…").Center);
        Assert.True(app.IsMenuOpen);
        Assert.True(app.Session.Shell.Menu.IsOpen);
    }

    [AvaloniaFact]
    public async Task A_menu_stays_inside_the_window_at_every_edge()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var layer = app.Session.Shell.Menu;
        var height = MenuInset + HeaderHeight + (7 * ItemHeight) + (2 * SeparatorHeight);

        layer.Open(Sample("right"), new MenuPosition(1090, 200));
        var right = app.Menu().Bounds;
        Assert.Equal(1088, right.Right);
        Assert.Equal(202, right.Top);

        layer.Open(Sample("bottom"), new MenuPosition(690, 660));
        var bottom = app.Menu().Bounds;
        Assert.Equal(658, bottom.Bottom);
        Assert.Equal(692, bottom.Left);
        Assert.Equal(height, bottom.Height);

        layer.Open(Sample("corner"), new MenuPosition(1099, 699));
        var corner = app.Menu().Bounds;
        Assert.Equal(1096, corner.Right);
        Assert.Equal(696, corner.Bottom);

        layer.Open(Sample("top-left"), new MenuPosition(0, 0));
        Assert.Equal(new Point(4, 4), app.Menu().Bounds.TopLeft);

        Assert.Equal(1, app.MenuCount);
    }

    [AvaloniaFact]
    public async Task A_menu_taller_than_the_window_is_limited_to_its_height_less_8_and_scrolls()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var entries = new List<MenuEntry> { new MenuHeader("Many") };
        entries.AddRange(Enumerable.Range(1, 40).Select(n => new Item($"Item {n}", () => { })));

        app.Session.Shell.Menu.Open(new MenuModel(entries), new MenuPosition(300, 500));
        var menu = app.Menu();

        Assert.Equal(700 - 8, menu.Bounds.Height);
        Assert.Equal(new Point(302, 4), menu.Bounds.TopLeft);
        var scroller = app.FindByAutomationId(MenuLayer.MenuAutomationId).GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height);

        // The wheel over the menu scrolls it and leaves it open.
        app.Wheel(app.MenuItemBounds("Item 5").Center, -1);
        Assert.True(app.IsMenuOpen);
    }

    // --- Keyboard and pointer --------------------------------------------------------------------

    [AvaloniaFact]
    public async Task The_keyboard_moves_the_highlight_over_enabled_items_only_and_Enter_runs_one_after_closing()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var ran = new List<string>();
        var openWhenRun = new List<bool>();
        var model = Sample("ce5ea6a Alpha two", label =>
        {
            ran.Add(label);
            openWhenRun.Add(app.Session.Shell.Menu.IsOpen || app.MenuCount > 0);
        });

        app.Session.Shell.Menu.Open(model, new MenuPosition(300, 200));
        Assert.Equal("Check out this commit", app.Menu().Highlighted?.Label);
        Assert.IsType<ContextMenuItem>(app.Window.FocusManager?.GetFocusedElement());

        app.PressKey(Key.Down);
        Assert.Equal("Create branch here…", app.Menu().Highlighted?.Label);
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        Assert.Equal("Revert this commit", app.Menu().Highlighted?.Label); // past the separator and the disabled item
        app.PressKey(Key.Up);
        Assert.Equal("Create tag here…", app.Menu().Highlighted?.Label);
        app.PressKey(Key.End);
        Assert.Equal("Delete feature/alpha…", app.Menu().Highlighted?.Label);
        app.PressKey(Key.Home);
        Assert.Equal("Check out this commit", app.Menu().Highlighted?.Label);
        app.PressKey(Key.Up);
        Assert.Equal("Delete feature/alpha…", app.Menu().Highlighted?.Label); // wraps
        app.PressKey(Key.Down);
        app.PressKey(Key.Down);
        Assert.Equal("Create branch here…", app.Menu().Highlighted?.Label);
        Assert.Single(app.Menu().Entries, entry => entry.IsHighlighted);

        app.PressKey(Key.Enter);

        Assert.False(app.IsMenuOpen);
        Assert.False(app.Session.Shell.Menu.IsOpen);
        Assert.Equal(["Create branch here…"], ran);
        Assert.Equal([false], openWhenRun);
    }

    [AvaloniaFact]
    public async Task Esc_closes_the_menu_without_running_anything_and_gives_the_focus_back()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var ran = new List<string>();
        var theme = app.FindByAutomationId("ThemeSwitch");
        theme.Focus(NavigationMethod.Tab);
        app.Settle();

        app.Session.Shell.Menu.Open(Sample("menu", ran.Add), new MenuPosition(300, 200));
        app.Menu();
        app.PressKey(Key.Escape);

        Assert.False(app.IsMenuOpen);
        Assert.False(app.Session.Shell.Menu.IsOpen);
        Assert.Empty(ran);
        Assert.Same(theme, app.Window.FocusManager?.GetFocusedElement());
    }

    [AvaloniaFact]
    public async Task The_Esc_chain_closes_an_open_menu_first()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        app.Session.Shell.Menu.Open(Sample("menu"), new MenuPosition(300, 200));
        app.Menu();

        // An Esc that reaches the window (the focus outside the menu) goes to the menu's link first.
        app.FindByAutomationId("ThemeSwitch").Focus();
        app.Settle();
        app.Window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = app.Window });
        app.Settle();

        Assert.False(app.Session.Shell.Menu.IsOpen);
        Assert.False(app.IsMenuOpen);
    }

    [AvaloniaFact]
    public async Task Moving_the_pointer_over_an_enabled_item_highlights_it_and_a_click_runs_it()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var ran = new List<string>();
        app.MoveMouse(new Point(300, 200));
        app.Session.Shell.Menu.Open(Sample("menu", ran.Add), new MenuPosition(300, 200));

        // From the pointer's place straight onto the disabled item: it takes no highlight, and
        // the first enabled item keeps it (check 1 step 4).
        app.MoveMouse(app.MenuItemBounds("Cherry-pick onto main").Center);
        Assert.Equal("Check out this commit", app.Menu().Highlighted?.Label);

        app.MoveMouse(app.MenuItemBounds("Revert this commit").Center);
        Assert.Equal("Revert this commit", app.Menu().Highlighted?.Label);
        Assert.Equal("Revert this commit", (app.Window.FocusManager?.GetFocusedElement() as Avalonia.Controls.MenuItem)?.Header);

        app.ClickMenuItem("Reset main to this commit…");

        Assert.False(app.IsMenuOpen);
        Assert.Equal(["Reset main to this commit…"], ran);
        Assert.Throws<TimeoutException>(() => app.Menu(TimeSpan.FromMilliseconds(200)));
    }

    [AvaloniaFact]
    public async Task UI_Automation_invokes_an_item_after_closing_the_menu_and_refuses_a_disabled_one()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var ran = new List<string>();
        var openWhenRun = new List<bool>();
        app.Session.Shell.Menu.Open(
            Sample("menu", label =>
            {
                ran.Add(label);
                openWhenRun.Add(app.Session.Shell.Menu.IsOpen || app.MenuCount > 0);
            }),
            new MenuPosition(300, 200));
        app.Menu();
        var items = app.Window.GetVisualDescendants().OfType<ContextMenuItem>().ToList();
        var disabled = ControlAutomationPeer.CreatePeerForElement(items.Single(item => (string?)item.Header == "Cherry-pick onto main"));
        var revert = ControlAutomationPeer.CreatePeerForElement(items.Single(item => (string?)item.Header == "Revert this commit"));

        Assert.Throws<InvalidOperationException>(() => disabled.GetProvider<IInvokeProvider>()!.Invoke());
        Assert.True(app.IsMenuOpen);

        revert.GetProvider<IInvokeProvider>()!.Invoke();
        app.Settle();

        Assert.False(app.IsMenuOpen);
        Assert.False(app.Session.Shell.Menu.IsOpen);
        Assert.Equal(["Revert this commit"], ran);
        Assert.Equal([false], openWhenRun);
    }

    // --- Light dismiss -----------------------------------------------------------------------

    [AvaloniaFact]
    public async Task A_press_outside_closes_the_menu_and_is_not_passed_on()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var ran = new List<string>();
        var shell = app.Session.Shell;

        shell.Menu.Open(Sample("menu", ran.Add), new MenuPosition(300, 200));
        app.Menu();
        app.Click(app.FindByAutomationId("ThemeSwitch"));

        Assert.False(app.IsMenuOpen);
        Assert.False(shell.Menu.IsOpen);
        Assert.True(shell.IsDarkTheme, "The press that closed the menu must not reach the theme button.");

        shell.Menu.Open(Sample("menu", ran.Add), new MenuPosition(300, 200));
        app.Menu();
        app.RightClick(app.FindByAutomationId("ThemeSwitch"));

        Assert.False(app.IsMenuOpen);
        Assert.False(shell.Menu.IsOpen);
        Assert.True(shell.IsDarkTheme);
        Assert.Empty(ran);
    }

    [AvaloniaFact]
    public async Task A_dialog_opening_closes_the_menu()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var shell = app.Session.Shell;
        shell.Menu.Open(Sample("menu"), new MenuPosition(300, 200));
        app.Menu();

        var answer = shell.Dialogs.ConfirmAsync(new ConfirmationRequest("Discard changes?", "Discard?", string.Empty, "Discard"));
        app.Settle();

        Assert.False(app.IsMenuOpen);
        Assert.False(shell.Menu.IsOpen);
        shell.Dialogs.CancelCurrent();
        Assert.False(await answer);
    }

    [AvaloniaFact]
    public async Task Another_tab_becoming_active_closes_the_menu()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var shell = app.Session.Shell;
        shell.Menu.Open(Sample("menu"), new MenuPosition(300, 200));
        app.Menu();

        shell.NewTabCommand.Execute(null);
        app.Settle();

        Assert.False(app.IsMenuOpen);
        Assert.False(shell.Menu.IsOpen);
    }

    [AvaloniaFact]
    public async Task The_wheel_anywhere_in_the_window_closes_the_menu()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var shell = app.Session.Shell;
        shell.Menu.Open(Sample("menu"), new MenuPosition(300, 200));
        app.Menu();

        app.Wheel(new Point(100, 400), -1);

        Assert.False(app.IsMenuOpen);
        Assert.False(shell.Menu.IsOpen);
    }

    [AvaloniaFact]
    public async Task The_window_losing_activation_closes_the_menu()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var shell = app.Session.Shell;
        shell.Menu.Open(Sample("menu"), new MenuPosition(300, 200));
        app.Menu();

        // Headless windows never lose activation (section 0): the handler is called as the
        // window's Deactivated event calls it.
        app.Find<MenuLayer>("MenuLayer").HandleWindowDeactivated();
        app.Settle();

        Assert.False(app.IsMenuOpen);
        Assert.False(shell.Menu.IsOpen);
    }

    [AvaloniaFact]
    public async Task Opening_a_menu_closes_the_one_that_was_open()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var shell = app.Session.Shell;

        shell.Menu.Open(Sample("first"), new MenuPosition(300, 200));
        app.Menu();
        shell.Menu.Open(Sample("second"), new MenuPosition(500, 300));

        var menu = app.Menu();
        Assert.Equal("second", menu.Header);
        Assert.Equal(new Point(502, 302), menu.Bounds.TopLeft);
        Assert.Equal(1, app.MenuCount);
        Assert.True(shell.Menu.IsOpen);
    }

    [AvaloniaFact]
    public async Task Closing_the_view_model_closes_the_menu_and_the_menu_closing_clears_the_view_model()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var shell = app.Session.Shell;

        shell.Menu.Open(Sample("menu"), new MenuPosition(300, 200));
        app.Menu();
        Assert.True(shell.Menu.Close());
        app.Settle();
        Assert.False(app.IsMenuOpen);

        shell.Menu.Open(Sample("menu"), new MenuPosition(300, 200));
        app.Menu();
        Assert.NotNull(app.Find<MenuLayer>("MenuLayer").OpenMenu);
        app.PressKey(Key.Escape);
        Assert.Null(shell.Menu.Current);
        Assert.Null(app.Find<MenuLayer>("MenuLayer").OpenMenu);
    }

    // --- Opening by keyboard (D106) -----------------------------------------------------------

    [AvaloniaTheory]
    [InlineData(Key.F10, RawInputModifiers.Shift)]
    [InlineData(Key.Apps, RawInputModifiers.None)]
    public async Task Shift_F10_and_the_Menu_key_ask_for_one_menu_per_press(Key key, RawInputModifiers modifiers)
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var target = app.FindByAutomationId("AddRepoButton");
        var requests = 0;
        target.ContextRequested += (_, e) =>
        {
            requests++;
            e.Handled = true;
            var corner = app.BoundsOf(target).BottomLeft;
            app.Session.Shell.Menu.Open(Sample("Theme"), new MenuPosition(corner.X, corner.Y), e.TryGetPosition(app.Window, out Point _) ? MenuOpenedBy.Pointer : MenuOpenedBy.Keyboard);
        };
        var opened = 0;
        app.Session.Shell.Menu.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MenuLayerViewModel.Current) && app.Session.Shell.Menu.Current is not null)
            {
                opened++;
            }
        };
        target.Focus(NavigationMethod.Tab);
        app.Settle();

        app.PressKey(key, modifiers);

        Assert.Equal(1, requests);
        Assert.Equal(1, opened);
        var menu = app.Menu();
        Assert.Equal(app.BoundsOf(target).BottomLeft, menu.Bounds.TopLeft);
        Assert.Equal(MenuOpenedBy.Keyboard, app.Session.Shell.Menu.OpenedBy);
        Assert.Equal("Check out this commit", menu.Highlighted?.Label);

        // The app's log has one line for the press, the line the real-window pass counts (D106),
        // read as the app may still write it (AppLogFile shares the file for writing; a plain
        // File.ReadAllText is refused on Windows while an entry is being written).
        Assert.Single(AppLogFile.ReadLines(data.Path), line => line.EndsWith("] Context menu opened: Theme by keyboard", StringComparison.Ordinal));

        // Headless the platform lists only the Menu key: the app raised Shift+F10 itself (D106).
        Assert.True(ContextMenuKeys.AppRaisesShiftF10(app.Window.GetPlatformSettings()?.HotkeyConfiguration.OpenContextMenu));
    }

    // --- UI Automation ------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task The_menu_its_header_and_its_items_are_UI_Automation_control_elements()
    {
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        app.Session.Shell.Menu.Open(Sample("ce5ea6a Alpha two"), new MenuPosition(300, 200));
        app.Menu();

        var peers = new List<AutomationPeer>();
        Walk(ControlAutomationPeer.CreatePeerForElement(app.Window), peers);

        var menu = Assert.Single(peers, peer => peer.GetAutomationId() == MenuLayer.MenuAutomationId);
        Assert.True(menu.IsControlElement());
        Assert.Equal(AutomationControlType.Menu, menu.GetAutomationControlType());

        var header = Assert.Single(peers, peer => peer.GetAutomationId() == MenuLayer.HeaderAutomationId);
        Assert.True(header.IsControlElement());
        Assert.Equal(AutomationControlType.Text, header.GetAutomationControlType());
        Assert.Equal("ce5ea6a Alpha two", header.GetName());

        var items = peers.Where(peer => peer.GetAutomationId() == MenuLayer.ItemAutomationId).ToList();
        Assert.Equal(
            ["Check out this commit", "Create branch here…", "Create tag here…", "Cherry-pick onto main", "Revert this commit", "Reset main to this commit…", "Delete feature/alpha…"],
            items.Select(item => item.GetName()));
        Assert.All(items, item =>
        {
            Assert.True(item.IsControlElement());
            Assert.Equal(AutomationControlType.MenuItem, item.GetAutomationControlType());
            Assert.NotNull(item.GetProvider<IInvokeProvider>());
        });
        var disabled = items.Single(item => item.GetName() == "Cherry-pick onto main");
        Assert.False(disabled.IsEnabled());
        Assert.Equal("This commit is already on main", disabled.GetHelpText());
    }

    private static void Walk(AutomationPeer peer, List<AutomationPeer> into)
    {
        into.Add(peer);
        foreach (var child in peer.GetChildren())
        {
            Walk(child, into);
        }
    }

    private static async Task<ShellDriver> StartAsync(string data)
    {
        var app = ShellDriver.Start(data);
        await app.WaitUntilReadyAsync();
        return app;
    }

    /// <summary>A commit menu as 4b gives it, with a disabled item and a destructive one; <paramref name="ran"/> hears each item's label when it runs.</summary>
    [AvaloniaFact]
    public async Task A_menu_without_an_enabled_item_keeps_the_focus_and_Esc_gives_it_back()
    {
        // Phase 4's resolver pane menu is [Copy] with nothing selected: no item can take the
        // focus. The focus stays with the menu while it is open, not on the first focusable
        // control of the window, and goes back where it was when Esc closes it (phase 3's 4a).
        using var data = new TempDirectory("data");
        using var app = await StartAsync(data.Path);
        var theme = app.FindByAutomationId("ThemeSwitch");
        theme.Focus(NavigationMethod.Tab);
        app.Settle();
        var model = new MenuModel([new MenuHeader("Ours: main"), new Item("Copy", () => { }, false, "Select some text first", false)]);

        app.Session.Shell.Menu.Open(model, new MenuPosition(300, 200));
        app.Menu();
        app.Settle();
        var inMenu = app.Window.FocusManager?.GetFocusedElement() as Visual;
        Assert.NotNull(inMenu);
        Assert.Contains(inMenu.GetSelfAndVisualAncestors(), visual => visual is MenuFlyoutPresenter);

        app.PressKey(Key.Escape);

        Assert.False(app.IsMenuOpen);
        Assert.Same(theme, app.Window.FocusManager?.GetFocusedElement());
    }

    private static MenuModel Sample(string header, Action<string>? ran = null)
    {
        Item Make(string label, bool enabled = true, string? reason = null, bool destructive = false) =>
            new(label, () => ran?.Invoke(label), enabled, reason, destructive);

        return new MenuModel([
            new MenuHeader(header),
            Make("Check out this commit"),
            Make("Create branch here…"),
            Make("Create tag here…"),
            new MenuSeparator(),
            Make("Cherry-pick onto main", false, "This commit is already on main"),
            Make("Revert this commit"),
            Make("Reset main to this commit…"),
            new MenuSeparator(),
            Make("Delete feature/alpha…", destructive: true),
        ]);
    }
}
