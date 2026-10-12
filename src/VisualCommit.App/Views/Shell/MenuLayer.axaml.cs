using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VisualCommit.App.Controls;
using VisualCommit.App.ViewModels;
using VisualCommit.App.ViewModels.Dialogs;
using VisualCommit.App.ViewModels.Menus;
using ModelItem = VisualCommit.App.ViewModels.Menus.MenuItem;

namespace VisualCommit.App.Views.Shell;

/// <summary>
/// The in-window context menu (docs/phase-3-design.md 4a, D86, D105): an adapter that draws the
/// <see cref="MenuLayerViewModel.Current"/> menu with Avalonia's stock <see cref="MenuFlyout"/> on
/// the window's overlay layer, so that it is inside the main window on every platform, in its UI
/// Automation tree and in a capture of its client area. The stock flyout brings the keyboard,
/// light dismiss (a press outside, Esc, the window losing activation) and the focus's return; the
/// adapter adds the items' Invoke peer (<see cref="ContextMenuItem"/>), 4a's placement, the
/// height limit, the first enabled item highlighted on opening, and closing when a dialog opens,
/// the active tab changes or the wheel turns.
/// </summary>
public partial class MenuLayer : UserControl
{
    /// <summary>The automation id of the open menu (control type Menu).</summary>
    public const string MenuAutomationId = "ContextMenu";

    /// <summary>The automation id of the menu's header (control type Text).</summary>
    public const string HeaderAutomationId = "ContextMenuHeader";

    /// <summary>The automation id of each item (control type MenuItem).</summary>
    public const string ItemAutomationId = "ContextMenuItem";

    /// <summary>The class the menu's presenter carries, for <c>Theme/Menus.axaml</c>.</summary>
    public const string PresenterClass = "vcContextMenu";

    /// <summary>The class of the header entry.</summary>
    public const string HeaderClass = "vcMenuHeader";

    /// <summary>The class of an item drawn in the danger colour (delete, drop, force push, …).</summary>
    public const string DangerClass = "vcDanger";

    /// <summary>How far a menu opened by the pointer is from it (4a).</summary>
    public const double PointerGap = 2;

    /// <summary>How far a menu stays inside the window's edges (4a); a taller menu is this far from the top and bottom and scrolls.</summary>
    public const double EdgeInset = 4;

    /// <summary>The header's text is cut with "…" past this width (4a).</summary>
    public const double HeaderMaxTextWidth = 340;

    /// <summary>The narrowest a menu is (4a).</summary>
    public const double MinMenuWidth = 200;

    /// <summary>
    /// The widest a menu is (4a): a menu is as wide as its longest label needs up to this, and a
    /// label longer still is cut with "…", its whole text in its tooltip. <c>Theme/Menus.axaml</c>
    /// sets the same numbers.
    /// </summary>
    public const double MaxMenuWidth = 520;

    /// <summary>An item's label: its text, cut with "…" (and then its whole text as its tooltip) only when the widest menu cannot show it.</summary>
    private static readonly FuncDataTemplate<string> LabelTemplate = new(
        (label, _) => new TrimmedTextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis },
        supportsRecycling: false);

    private MenuLayerViewModel? _viewModel;
    private MenuFlyoutWithLimit? _flyout;
    private TopLevel? _topLevel;
    private IDisposable? _escape;
    private MainWindowViewModel? _shell;
    private DialogHostViewModel? _dialogs;
    private IInputElement? _focusBeforeMenu;

    public MenuLayer()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow(DataContext as MenuLayerViewModel);
    }

    /// <summary>The open menu's presenter (automation id <see cref="MenuAutomationId"/>), or null.</summary>
    public Control? OpenMenu => _flyout is { IsOpen: true } flyout ? flyout.Presenter : null;

    /// <summary>
    /// The element that should have the focus once the menus are gone: <paramref name="focused"/>,
    /// unless it is in a context menu (an item of it), whose focus is the menu's own; then the
    /// element that had the focus before the menu opened. The window keeps it when a dialog opens
    /// over a menu, which closes the menu, to give the focus back there when the dialog closes.
    /// </summary>
    public IInputElement? FocusOutsideMenus(IInputElement? focused) =>
        focused is ContextMenuItem
        || (focused is Visual visual && visual.GetSelfAndVisualAncestors().OfType<MenuFlyoutPresenter>().Any(presenter => presenter.Classes.Contains(PresenterClass)))
            ? _focusBeforeMenu
            : focused;

    /// <summary>
    /// Where a menu of <paramref name="menu"/>'s size opens in an area of <paramref name="area"/>'s
    /// size when asked for at <paramref name="at"/> (4a). By the pointer: its top-left 2 right of
    /// and 2 below the pointer; when it would cross the right edge it opens to the left of the
    /// pointer (its right edge 2 left of it), past the bottom above it (its bottom edge 2 above
    /// it); then it is moved to stay 4 inside the area. By the keyboard the same, without the gap
    /// (its top-left at the point: the focused row's bottom-left corner).
    /// </summary>
    public static Point PlaceMenu(Size area, Point at, Size menu, MenuOpenedBy openedBy)
    {
        var gap = openedBy == MenuOpenedBy.Pointer ? PointerGap : 0;
        var x = at.X + gap;
        var y = at.Y + gap;
        if (x + menu.Width > area.Width)
        {
            x = at.X - gap - menu.Width;
        }

        if (y + menu.Height > area.Height)
        {
            y = at.Y - gap - menu.Height;
        }

        // The far edge first, so that a menu larger than the area keeps its top-left inside.
        x = Math.Max(Math.Min(x, area.Width - EdgeInset - menu.Width), EdgeInset);
        y = Math.Max(Math.Min(y, area.Height - EdgeInset - menu.Height), EdgeInset);
        return new Point(x, y);
    }

    /// <summary>
    /// What the window losing activation does: closes the menu. The stock flyout already closes
    /// on it (the spike of 4a saw it on Win32); this handler makes it so on every platform and is
    /// tested headless, where windows never lose activation.
    /// </summary>
    public void HandleWindowDeactivated() => _viewModel?.Close();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is null)
        {
            return;
        }

        if (_topLevel is MainWindow window)
        {
            _escape = window.AddEscapeHandler(new EscapeHandler(() => _viewModel?.Close() ?? false), EscapeOrder.Menu);
        }

        if (_topLevel is WindowBase windowBase)
        {
            windowBase.Deactivated += OnWindowDeactivated;
        }

        _topLevel.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel, handledEventsToo: true);
        _topLevel.DataContextChanged += OnTopLevelDataContextChanged;
        FollowShell(_topLevel.DataContext as MainWindowViewModel);
        ShowCurrent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        HideFlyout();
        _escape?.Dispose();
        _escape = null;
        if (_topLevel is not null)
        {
            if (_topLevel is WindowBase windowBase)
            {
                windowBase.Deactivated -= OnWindowDeactivated;
            }

            _topLevel.RemoveHandler(PointerWheelChangedEvent, OnWheel);
            _topLevel.DataContextChanged -= OnTopLevelDataContextChanged;
        }

        FollowShell(null);
        _topLevel = null;
    }

    private void Follow(MenuLayerViewModel? viewModel)
    {
        if (ReferenceEquals(viewModel, _viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
        }

        HideFlyout();
        _viewModel = viewModel;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelChanged;
            ShowCurrent();
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MenuLayerViewModel.Current))
        {
            ShowCurrent();
        }
    }

    private void OnTopLevelDataContextChanged(object? sender, EventArgs e) =>
        FollowShell(_topLevel?.DataContext as MainWindowViewModel);

    /// <summary>Watches the window's view model: a dialog opening or another tab becoming active closes the menu (4a).</summary>
    private void FollowShell(MainWindowViewModel? shell)
    {
        if (ReferenceEquals(shell, _shell))
        {
            return;
        }

        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellChanged;
        }

        if (_dialogs is not null)
        {
            _dialogs.PropertyChanged -= OnDialogsChanged;
        }

        _shell = shell;
        _dialogs = shell?.Dialogs;
        if (_shell is not null)
        {
            _shell.PropertyChanged += OnShellChanged;
        }

        if (_dialogs is not null)
        {
            _dialogs.PropertyChanged += OnDialogsChanged;
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ActiveTab))
        {
            _viewModel?.Close();
        }
    }

    private void OnDialogsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DialogHostViewModel.IsOpen) && _dialogs is { IsOpen: true })
        {
            _viewModel?.Close();
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => HandleWindowDeactivated();

    /// <summary>The wheel anywhere in the window closes the menu, except over the menu itself, which scrolls when it is taller than the window (4a).</summary>
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (OpenMenu is { } menu && !(e.Source is Visual source && (ReferenceEquals(source, menu) || menu.IsVisualAncestorOf(source))))
        {
            _viewModel?.Close();
        }
    }

    /// <summary>Shows the view model's menu, or closes the flyout when it has none.</summary>
    private void ShowCurrent()
    {
        if (_viewModel?.Current is not { } menu)
        {
            HideFlyout();
            return;
        }

        if (_flyout is not null && ReferenceEquals(_flyout.Model, menu))
        {
            return;
        }

        HideFlyout();
        if (_topLevel is null || MenuPanel.Bounds.Width <= 0)
        {
            // Not in a window (yet): shown once the layer is attached.
            return;
        }

        Show(menu, _viewModel.Position, _viewModel.OpenedBy);
    }

    private void Show(MenuModel menu, MenuPosition position, MenuOpenedBy openedBy)
    {
        var area = MenuPanel.Bounds.Size;
        var origin = MenuPanel.TranslatePoint(default, _topLevel!) ?? default;
        var at = new Point(position.X - origin.X, position.Y - origin.Y);

        var flyout = new MenuFlyoutWithLimit(menu, Math.Max(0, area.Height - (2 * EdgeInset)));
        foreach (var entry in menu.Entries)
        {
            flyout.Items.Add(entry switch
            {
                MenuHeader header => CreateHeader(header),
                MenuSeparator => new Separator(),
                ModelItem item => CreateItem(item),
                _ => throw new NotSupportedException($"A menu entry of type {entry.GetType().Name} cannot be drawn."),
            });
        }

        flyout.Popup.ShouldUseOverlayLayer = true;
        flyout.Placement = PlacementMode.Custom;
        flyout.CustomPopupPlacementCallback = placement =>
        {
            placement.Anchor = PopupAnchor.TopLeft;
            placement.Gravity = PopupGravity.BottomRight;
            placement.AnchorRectangle = new Rect(0, 0, 1, 1);
            placement.Offset = PlaceMenu(MenuPanel.Bounds.Size, at, placement.PopupSize, openedBy);
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.None;
        };
        var hasEnabledItem = true;
        flyout.Opened += (_, _) =>
        {
            LetTheShadowThrough(flyout);
            hasEnabledItem = HighlightFirstItem(flyout);
        };
        flyout.Closed += (_, _) =>
        {
            if (ReferenceEquals(_flyout, flyout))
            {
                _flyout = null;
                _viewModel?.Close();
            }

            // A menu without an enabled item ran nothing: the focus goes back where it was
            // before it opened (4a), as Avalonia does for a menu whose item had the focus.
            if (!hasEnabledItem && _focusBeforeMenu is Control before && before.IsEffectivelyVisible && before.IsEffectivelyEnabled)
            {
                before.Focus(NavigationMethod.Unspecified);
            }
        };

        // Where the focus was before the menu takes it; a menu that replaces another keeps what
        // the first one found.
        _focusBeforeMenu = FocusOutsideMenus(_topLevel?.FocusManager?.GetFocusedElement());
        _flyout = flyout;
        flyout.ShowAt(MenuPanel);
    }

    /// <summary>
    /// Gives the focus and the highlight to the menu's first enabled item when it opens, however it
    /// was opened (D105), where the stock flyout has not done so already. A menu without an
    /// enabled item highlights none and keeps the focus on its presenter, so that it does not go to
    /// the window's first focusable control (the resolver's pane menu, phase 4). Returns whether
    /// the menu has an enabled item.
    /// </summary>
    private static bool HighlightFirstItem(MenuFlyoutWithLimit flyout)
    {
        var items = flyout.Items.OfType<ContextMenuItem>().ToList();
        if (items.Any(item => item.IsSelected))
        {
            return true;
        }

        if (items.FirstOrDefault(item => item.IsEffectivelyEnabled) is { } first)
        {
            first.Focus(NavigationMethod.Directional);
            first.IsSelected = true;
            return true;
        }

        if (flyout.Presenter is { } presenter)
        {
            presenter.Focusable = true;
            presenter.Focus(NavigationMethod.Unspecified);
        }

        return false;
    }

    /// <summary>
    /// The popup's host and its parts clip to the menu's bounds, which would hide the menu's
    /// shadow: they are this menu's own (a new flyout and host for every menu), so they stop
    /// clipping. Nothing else is drawn in them.
    /// </summary>
    private static void LetTheShadowThrough(MenuFlyoutWithLimit flyout)
    {
        if (flyout.Presenter is not { } presenter)
        {
            return;
        }

        foreach (var part in presenter.GetVisualAncestors().OfType<Control>())
        {
            part.ClipToBounds = false;
            if (part is Avalonia.Controls.Primitives.OverlayPopupHost)
            {
                break;
            }
        }
    }

    private void HideFlyout()
    {
        if (_flyout is not { } flyout)
        {
            return;
        }

        _flyout = null;
        flyout.Hide();
    }

    private static Avalonia.Controls.MenuItem CreateHeader(MenuHeader header)
    {
        // Left-aligned like the items' labels: in a menu wider than the header's limit, a stretched
        // text block with a MaxWidth would be centred.
        var text = new TextBlock
        {
            Text = header.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = HeaderMaxTextWidth,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        };
        var item = new Avalonia.Controls.MenuItem
        {
            Header = text,
            Focusable = false,
        };
        item.Classes.Add(HeaderClass);
        AutomationProperties.SetAutomationId(item, HeaderAutomationId);
        AutomationProperties.SetName(item, header.Text);
        AutomationProperties.SetControlTypeOverride(item, AutomationControlType.Text);
        ToolTip.SetTip(item, header.ToolTip ?? header.Text);

        // A click on the header does nothing and keeps the menu open.
        item.AddHandler(PointerPressedEvent, (_, e) => e.Handled = true, RoutingStrategies.Tunnel);
        item.AddHandler(PointerReleasedEvent, (_, e) => e.Handled = true, RoutingStrategies.Tunnel);
        return item;
    }

    private ContextMenuItem CreateItem(ModelItem model)
    {
        // The label stays the header (harnesses find an item by it); the template draws it as text
        // that is cut with "…" past the widest menu, never clipped.
        var item = new ContextMenuItem
        {
            Header = model.Label,
            HeaderTemplate = LabelTemplate,
            IsEnabled = model.IsEnabled,
        };
        AutomationProperties.SetAutomationId(item, ItemAutomationId);
        AutomationProperties.SetName(item, model.Label);
        if (model.IsDestructive)
        {
            item.Classes.Add(DangerClass);
            if (model.IsEnabled)
            {
                // A disabled item, a destructive one too, is drawn as disabled (D114).
                item.Bind(ForegroundProperty, item.GetResourceObservable("VcDangerBrush"));
            }
        }

        if (!model.IsEnabled && !string.IsNullOrEmpty(model.DisabledReason))
        {
            ToolTip.SetTip(item, model.DisabledReason);
            ToolTip.SetShowOnDisabled(item, true);
            AutomationProperties.SetHelpText(item, model.DisabledReason);
        }

        var invoke = model.Invoke;
        item.Click += (_, e) =>
        {
            e.Handled = true;

            // The menu closes first; the action runs after it, on the UI thread (4a).
            _viewModel?.Close();
            HideFlyout();
            Dispatcher.UIThread.Post(invoke);
        };
        return item;
    }

    /// <summary>
    /// The stock menu flyout, with its presenter made the window's context menu for UI Automation
    /// (automation id <see cref="MenuAutomationId"/>, control type Menu instead of the stock List)
    /// and limited to the window's height less 8, so that a taller menu scrolls (4a).
    /// </summary>
    private sealed class MenuFlyoutWithLimit(MenuModel model, double maxHeight) : MenuFlyout
    {
        public MenuModel Model { get; } = model;

        public Control? Presenter { get; private set; }

        protected override Control CreatePresenter()
        {
            var presenter = base.CreatePresenter();
            presenter.MaxHeight = maxHeight;

            // Its shadow (VcMenuShadow) is drawn outside its bounds: nothing up to the overlay
            // layer may clip it (Opened lets the popup's host through too). The window still cuts
            // it where the menu is nearer its edge than the shadow reaches (4a).
            presenter.ClipToBounds = false;
            presenter.Classes.Add(PresenterClass);
            AutomationProperties.SetAutomationId(presenter, MenuAutomationId);
            AutomationProperties.SetControlTypeOverride(presenter, AutomationControlType.Menu);
            Presenter = presenter;
            return presenter;
        }
    }
}
